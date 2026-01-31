using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class AsteroidFieldInstancedRenderer : MonoBehaviour
{
    [Serializable]
    public class AsteroidTypeRender
    {
        public string name = "Type";
        public Material material;

        [Header("LOD Meshes")]
        public Mesh lod0Mesh;
        public Mesh lod1Mesh;
        public Mesh lod2Mesh;

        [Tooltip("Optional: override base radius for culling/LOD heuristics later. Not used in Step 2.")]
        public float baseRadius = 1f;

        public bool IsValid =>
            material != null &&
            lod0Mesh != null &&
            lod1Mesh != null &&
            lod2Mesh != null;
    }

    [Header("Input Data (Runtime Chunks)")]
    public List<AsteroidFieldData> fieldDatas = new List<AsteroidFieldData>();

    [Tooltip("If null, uses Camera.main.")]
    public Camera renderCamera;

    [Header("Type -> LOD Mesh Map (size should be 15)")]
    public AsteroidTypeRender[] typeRenders = new AsteroidTypeRender[15];

    [Header("Global LOD Distances (meters)")]
    [Tooltip("Distance < LOD0Distance => LOD0\n" +
             "LOD0Distance..LOD1Distance => LOD1\n" +
             ">= LOD1Distance => LOD2")]
    public float lod0Distance = 60f;

    public float lod1Distance = 140f;

    [Header("Rendering")]
    public ShadowCastingMode shadowCasting = ShadowCastingMode.Off;
    public bool receiveShadows = false;

    [Tooltip("Layer used for rendering (affects culling masks, etc.)")]
    public int renderLayer = 0;

    [Tooltip("Only render in play mode? If false, will also render in edit mode (Scene/Game view).")]
    public bool onlyRenderInPlayMode = false;

    [Header("Optional: Rotation Drift")]
    [Tooltip("If true, applies angularVelocityDeg from the data (degrees/sec) to spin asteroids. " +
             "This updates rotations & matrices each frame in play mode.")]
    public bool applyRotationDriftInPlayMode = false;

    [Header("Voxel Paint (World Cell Size per LOD)")]
    public float lod0VoxelCellSize = 0.25f;
    public float lod1VoxelCellSize = 0.5f;
    public float lod2VoxelCellSize = 1.0f;

    [Header("Chunk Culling")]
    [Tooltip("Pads the computed chunk bounds by this many meters to reduce pop-in at edges.")]
    public float chunkBoundsPadding = 10f;

    [Header("Optional: Auto hook PosManager")]
    public AsteroidPosManager posManager;

    private const int MaxInstancesPerCall = 1023;

    private Dictionary<AsteroidFieldData, BitArray> _hiddenByData;
    private static readonly int VoxelCellSizeID = Shader.PropertyToID("_VoxelCellSize");
    private MaterialPropertyBlock _mpb;

    // Per-chunk cache
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private sealed class ChunkCache
    {
        public int count;
        public Matrix4x4[] matrices;
        public Quaternion[] runtimeRotations;
        public List<int>[,] buckets;
        public Bounds localBounds;     // <-- local-space bounds
        public Bounds worldBounds;     // <-- cached world bounds for this frame/coord
        public Vector3 worldOrigin;    // <-- where this chunk is in world space
        public bool initialized;
    }

    private readonly Dictionary<AsteroidFieldData, ChunkCache> _cacheByData = new();

    private void OnEnable()
    {
        _hiddenByData ??= new Dictionary<AsteroidFieldData, BitArray>();

        _mpb ??= new MaterialPropertyBlock();

        if (posManager) posManager.OnChunkCreated += HandleChunkCreated;
    }

    private void OnDisable()
    {
        if (posManager) posManager.OnChunkCreated -= HandleChunkCreated;
        _cacheByData.Clear();
    }

    private void Update()
    {
        if (onlyRenderInPlayMode && !Application.isPlaying)
            return;

        if (posManager == null)
            return;

        var chunks = posManager.Chunks;
        if (chunks == null || chunks.Count == 0)
            return;

        var cam = renderCamera ? renderCamera : Camera.main;
        if (!cam)
            return;

        // Compute frustum planes once per frame
        GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);

        // Optional: cleanup dead entries (if chunks are destroyed/replaced)
        CleanupCache();

        foreach (var kv in posManager.Chunks) // coord -> data
        {
            var coord = kv.Key;
            var data = kv.Value;
            if (data == null || data.count <= 0) continue;

            Vector3 origin = posManager.ChunkCoordToWorldOrigin(coord);

            var cache = GetOrInitCache(data);
            cache.worldOrigin = origin;

            // Update world bounds from local bounds
            cache.worldBounds = cache.localBounds;
            cache.worldBounds.center += origin;

            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, cache.worldBounds))
                continue;

            if (applyRotationDriftInPlayMode && Application.isPlaying)
                ApplyRotationDrift(data, cache, Time.deltaTime);

            Render(data, cache, cam);
        }
    }
    public void ClearHidden(AsteroidFieldData data)
    {
        if (data == null) return;

        if (_hiddenByData != null && _hiddenByData.TryGetValue(data, out var mask) && mask != null)
            mask.SetAll(false);
    }
    private void CleanupCache()
    {
        // Remove cache entries whose data is gone or not referenced anymore
        // (keeps memory stable if you replace chunk objects)
        var toRemove = ListPool<AsteroidFieldData>.Get();
        foreach (var kvp in _cacheByData)
        {
            if (kvp.Key == null || fieldDatas == null || !fieldDatas.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }
        foreach (var d in toRemove)
            _cacheByData.Remove(d);
        ListPool<AsteroidFieldData>.Release(toRemove);
    }
    private ChunkCache GetOrInitCache(AsteroidFieldData data)
    {
        if (!_cacheByData.TryGetValue(data, out var cache) || cache == null)
        {
            cache = new ChunkCache();
            _cacheByData[data] = cache;
        }

        // Re-init if count changed (or never init)
        if (!cache.initialized || cache.count != data.count)
            InitCache(data, cache);

        return cache;
    }
    private void InitCache(AsteroidFieldData fieldData, ChunkCache cache)
    {
        int n = fieldData.count;
        cache.count = n;

        cache.matrices = new Matrix4x4[n];
        cache.runtimeRotations = new Quaternion[n];

        // Build matrices + compute bounds in the same pass (LOCAL bounds)
        bool boundsInit = false;
        Bounds b = default;

        for (int i = 0; i < n; i++)
        {
            Vector3 localPos = fieldData.positions[i];

            Quaternion rot = fieldData.rotations[i];
            float s = fieldData.scales[i];

            if (!IsFinite(rot) || (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f))
                rot = Quaternion.identity;
            else
                rot = Quaternion.Normalize(rot);

            cache.runtimeRotations[i] = rot;

            Vector3 worldPos = cache.worldOrigin + localPos;
            cache.matrices[i] = Matrix4x4.TRS(worldPos, rot, Vector3.one * s);

            if (!boundsInit)
            {
                b = new Bounds(localPos, Vector3.zero);
                boundsInit = true;
            }
            else
            {
                b.Encapsulate(localPos);
            }
        }

        // Pad LOCAL bounds (world bounds handled per-frame in Update)
        float pad = Mathf.Max(0f, chunkBoundsPadding);
        b.Expand(pad * 2f);
        cache.localBounds = b;

        int typeCount = (typeRenders != null && typeRenders.Length > 0) ? typeRenders.Length : 15;
        cache.buckets = new List<int>[typeCount, 3];

        for (int t = 0; t < typeCount; t++)
            for (int l = 0; l < 3; l++)
                cache.buckets[t, l] = new List<int>(256);

        cache.initialized = true;
    }

    private void ApplyRotationDrift(AsteroidFieldData fieldData, ChunkCache cache, float dt)
    {
        int n = fieldData.count;
        var ang = fieldData.angularVelocityDeg;
        if (ang == null || ang.Length != n)
            return;

        for (int i = 0; i < n; i++)
        {
            Vector3 av = ang[i];
            if (av.sqrMagnitude < 0.000001f)
                continue;

            Quaternion delta = Quaternion.Euler(av * dt);
            Quaternion q = cache.runtimeRotations[i] * delta;

            if (!IsFinite(q) || (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f))
                q = Quaternion.identity;
            else
                q = Quaternion.Normalize(q);

            cache.runtimeRotations[i] = q;
            Vector3 worldPos = cache.worldOrigin + fieldData.positions[i];
            cache.matrices[i] = Matrix4x4.TRS(worldPos, q, Vector3.one * fieldData.scales[i]);
        }
    }
    private void Render(AsteroidFieldData fieldData, ChunkCache cache, Camera cam)
    {
        var buckets = cache.buckets;

        int typeCount = buckets.GetLength(0);
        for (int t = 0; t < typeCount; t++)
            for (int l = 0; l < 3; l++)
                buckets[t, l].Clear();

        Vector3 camPos = cam.transform.position;
        float d0 = Mathf.Max(0f, lod0Distance);
        float d1 = Mathf.Max(d0, lod1Distance);

        int n = fieldData.count;
        var typeIds = fieldData.typeIds;

        for (int i = 0; i < n; i++)
        {
            if (IsHidden(fieldData, i))
                continue;

            int typeId = (typeIds != null && i < typeIds.Length) ? typeIds[i] : 0;
            if (typeId < 0 || typeId >= typeCount)
                continue;

            var tr = typeRenders != null && typeId < typeRenders.Length ? typeRenders[typeId] : null;
            if (tr == null || !tr.IsValid)
                continue;

            Vector3 worldPos = cache.worldOrigin + fieldData.positions[i];
            float dist = Vector3.Distance(camPos, worldPos);
            int lod = (dist < d0) ? 0 : (dist < d1 ? 1 : 2);

            buckets[typeId, lod].Add(i);
        }

        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var tr = typeRenders != null && typeId < typeRenders.Length ? typeRenders[typeId] : null;
            if (tr == null || !tr.IsValid)
                continue;

            DrawBucket(tr.lod0Mesh, tr.material, buckets[typeId, 0], cache, lod0VoxelCellSize, cam);
            DrawBucket(tr.lod1Mesh, tr.material, buckets[typeId, 1], cache, lod1VoxelCellSize, cam);
            DrawBucket(tr.lod2Mesh, tr.material, buckets[typeId, 2], cache, lod2VoxelCellSize, cam);
        }
    }

    private void DrawBucket(Mesh mesh, Material mat, List<int> indices, ChunkCache cache, float voxelCellSize, Camera cam)
    {
        int count = indices.Count;
        if (count <= 0) return;

        _mpb.SetFloat(VoxelCellSizeID, voxelCellSize);

        int offset = 0;
        while (offset < count)
        {
            int batchCount = Mathf.Min(MaxInstancesPerCall, count - offset);
            Matrix4x4[] buffer = MatrixBufferCache.Get();

            // Fill buffer directly from cache.matrices using indices
            for (int j = 0; j < batchCount; j++)
                buffer[j] = cache.matrices[indices[offset + j]];

            Graphics.DrawMeshInstanced(
                mesh, 0, mat,
                buffer, batchCount,
                _mpb, shadowCasting, receiveShadows,
                renderLayer, cam,
                LightProbeUsage.Off, null
            );

            offset += batchCount;
        }
    }

    /// <summary>
    /// Shared per-frame buffers to avoid allocating Matrix4x4[1023] repeatedly.
    /// </summary>
    private static class MatrixBufferCache
    {
        private static Matrix4x4[] _buffer1023;
        public static Matrix4x4[] Get()
        {
            if (_buffer1023 == null || _buffer1023.Length != MaxInstancesPerCall)
                _buffer1023 = new Matrix4x4[MaxInstancesPerCall];
            return _buffer1023;
        }
    }

    private static bool IsFinite(Quaternion q)
    {
        return float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    }

    private bool IsHidden(AsteroidFieldData data, int index)
    {
        if (_hiddenByData == null || data == null)
            return false;

        if (!_hiddenByData.TryGetValue(data, out var mask) || mask == null)
            return false;

        return (index >= 0 && index < mask.Length) && mask[index];
    }

    public void SetInstanceHidden(AsteroidFieldData data, int index, bool hidden)
    {
        if (data == null || data.count <= 0) return;
        int n = data.count;
        if (index < 0 || index >= n) return;

        if (!_hiddenByData.TryGetValue(data, out var mask) || mask == null || mask.Length != n)
        {
            mask = new BitArray(n, false);
            _hiddenByData[data] = mask;
        }

        mask[index] = hidden;
    }
    private void HandleChunkCreated(Vector3Int coord, AsteroidFieldData data)
    {
        if (data == null) return;

        // Reset any "destroyed/hidden" visual state when this data is reused elsewhere
        ClearHidden(data);

        if (_cacheByData.TryGetValue(data, out var cache) && cache != null)
            cache.initialized = false;
    }
    // tiny pooled list helper so CleanupCache doesn't allocate garbage every Update
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new();
        public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(64);
        public static void Release(List<T> list) { list.Clear(); _pool.Push(list); }
    }
}
