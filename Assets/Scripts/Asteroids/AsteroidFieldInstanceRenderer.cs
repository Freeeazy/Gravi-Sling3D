using System;
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
    private static readonly int VoxelCellSizeID = Shader.PropertyToID("_VoxelCellSize");
    private readonly Matrix4x4[] _batchBuffer = new Matrix4x4[MaxInstancesPerCall];
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private MaterialPropertyBlock _mpb;

    private sealed class ChunkCache
    {
        public AsteroidFieldData.RuntimeCache runtime;
        public Quaternion[] sourceRotations;
        public int[] sourceTypes;
        public Matrix4x4[] matrices;
        public Quaternion[] rotations;
        public List<int>[,] buckets;
        public Bounds localBounds;
        public Bounds worldBounds;
        public Vector3 worldOrigin;
        public int typeLayoutVersion;
        public float padding;
    }

    private readonly Dictionary<AsteroidFieldData, ChunkCache> _cacheByData =
        new Dictionary<AsteroidFieldData, ChunkCache>();
    private readonly HashSet<AsteroidFieldData> _activeData = new HashSet<AsteroidFieldData>();
    private readonly List<AsteroidFieldData> _toRemove = new List<AsteroidFieldData>();
    private AsteroidPosManager _cacheManager;
    private int _lastChunksVersion = -1;
    private bool[] _validTypes;
    private Mesh[] _lastMeshes;
    private float[] _typeBoundsRadii;
    private int _typeLayoutVersion;

    private void OnEnable()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _lastChunksVersion = -1;
    }

    private void OnDisable()
    {
        _cacheByData.Clear();
        _activeData.Clear();
        _cacheManager = null;
        _lastChunksVersion = -1;
        // Destruction belongs to the chunk assignment, not this renderer's lifetime.
    }

    private void LateUpdate()
    {
        if (onlyRenderInPlayMode && !Application.isPlaying) return;
        if (!posManager) return;
        SyncActiveChunks();
        if (posManager.Chunks.Count == 0) return;
        Camera cam = renderCamera ? renderCamera : Camera.main;
        if (!cam) return;

        CacheTypeSettings();
        if (_validTypes.Length == 0) return;
        GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
        Vector3 cameraPosition = cam.transform.position;
        float d0 = Mathf.Max(0f, lod0Distance);
        float d1 = Mathf.Max(d0, lod1Distance);
        float d0Squared = d0 * d0;
        float d1Squared = d1 * d1;
        bool rotate = applyRotationDriftInPlayMode && Application.isPlaying;
        float dt = Time.deltaTime;

        foreach (var chunk in posManager.Chunks)
        {
            AsteroidFieldData data = chunk.Value;
            if (!data || data.count <= 0) continue;
            AsteroidFieldData.RuntimeCache runtime = data.Runtime;
            int visibleCount = Mathf.Min(runtime.Count, posManager.GetVisibleCountForChunk(chunk.Key, data));
            if (visibleCount <= 0) continue;

            Vector3 origin = posManager.ChunkCoordToWorldOrigin(chunk.Key);
            ChunkCache cache = GetOrInitCache(data, runtime, origin);
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, cache.worldBounds)) continue;

            Render(data, cache, visibleCount, cameraPosition, d0Squared, d1Squared, rotate, dt, cam);
        }
    }

    private void SyncActiveChunks()
    {
        if (_cacheManager != posManager)
        {
            _cacheByData.Clear();
            _cacheManager = posManager;
            _lastChunksVersion = -1;
        }
        if (_lastChunksVersion == posManager.ChunksVersion) return;
        _lastChunksVersion = posManager.ChunksVersion;
        _activeData.Clear();
        foreach (var chunk in posManager.Chunks)
            if (chunk.Value) _activeData.Add(chunk.Value);
        _toRemove.Clear();
        foreach (var entry in _cacheByData)
            if (!entry.Key || !_activeData.Contains(entry.Key)) _toRemove.Add(entry.Key);
        for (int i = 0; i < _toRemove.Count; i++) _cacheByData.Remove(_toRemove[i]);
        _toRemove.Clear();
    }

    private void CacheTypeSettings()
    {
        int count = typeRenders != null ? typeRenders.Length : 0;
        if (_validTypes == null || _validTypes.Length != count)
        {
            _validTypes = new bool[count];
            _lastMeshes = new Mesh[count * 3];
            _typeBoundsRadii = new float[count];
            _typeLayoutVersion++;
        }
        bool meshesChanged = false;
        for (int t = 0; t < count; t++)
        {
            AsteroidTypeRender type = typeRenders[t];
            _validTypes[t] = type != null && type.IsValid;
            Mesh m0 = type != null ? type.lod0Mesh : null;
            Mesh m1 = type != null ? type.lod1Mesh : null;
            Mesh m2 = type != null ? type.lod2Mesh : null;
            int offset = t * 3;
            if (_lastMeshes[offset] == m0 && _lastMeshes[offset + 1] == m1 &&
                _lastMeshes[offset + 2] == m2) continue;
            _lastMeshes[offset] = m0;
            _lastMeshes[offset + 1] = m1;
            _lastMeshes[offset + 2] = m2;
            _typeBoundsRadii[t] = Mathf.Max(MeshRadius(m0), Mathf.Max(MeshRadius(m1), MeshRadius(m2)));
            meshesChanged = true;
        }
        if (meshesChanged) _typeLayoutVersion++;
    }

    private static float MeshRadius(Mesh mesh)
    {
        if (!mesh) return 0f;
        Bounds bounds = mesh.bounds;
        // A sphere about the transform origin covers every rotation of this mesh.
        return bounds.center.magnitude + bounds.extents.magnitude;
    }

    private ChunkCache GetOrInitCache(AsteroidFieldData data,
        AsteroidFieldData.RuntimeCache runtime, Vector3 origin)
    {
        if (!_cacheByData.TryGetValue(data, out var cache))
        {
            cache = new ChunkCache();
            _cacheByData.Add(data, cache);
        }
        bool geometryChanged = cache.runtime != runtime || cache.sourceRotations != data.rotations;
        if (geometryChanged)
        {
            cache.runtime = runtime;
            cache.sourceRotations = data.rotations;
            if (cache.matrices == null || cache.matrices.Length != runtime.Count)
            {
                cache.matrices = new Matrix4x4[runtime.Count];
                cache.rotations = new Quaternion[runtime.Count];
            }
            for (int i = 0; i < runtime.Count; i++)
            {
                Quaternion q = data.rotations != null && i < data.rotations.Length
                    ? NormalizeSafe(data.rotations[i]) : Quaternion.identity;
                cache.rotations[i] = q;
                float scale = data.scales != null && i < data.scales.Length ? data.scales[i] : 1f;
                cache.matrices[i] = Matrix4x4.TRS(origin + data.positions[i], q, Vector3.one * scale);
            }
        }
        else if (!cache.worldOrigin.Equals(origin))
        {
            // Recycle existing buffers; preserve rotation/scale and replace only translation.
            for (int i = 0; i < runtime.Count; i++)
            {
                Vector3 p = origin + data.positions[i];
                Matrix4x4 matrix = cache.matrices[i];
                matrix.m03 = p.x;
                matrix.m13 = p.y;
                matrix.m23 = p.z;
                cache.matrices[i] = matrix;
            }
        }
        cache.worldOrigin = origin;

        int typeCount = _validTypes.Length;
        if (cache.buckets == null || cache.buckets.GetLength(0) != typeCount)
        {
            cache.buckets = new List<int>[typeCount, 3];
            int capacity = Mathf.Max(8, runtime.Count / Mathf.Max(1, typeCount));
            for (int t = 0; t < typeCount; t++)
                for (int lod = 0; lod < 3; lod++) cache.buckets[t, lod] = new List<int>(capacity);
        }

        if (geometryChanged || cache.typeLayoutVersion != _typeLayoutVersion ||
            cache.sourceTypes != data.typeIds || cache.padding != chunkBoundsPadding)
        {
            // Unlike a center-only AABB, include asteroid size so edge meshes do not pop out.
            Bounds bounds = runtime.CollisionBounds;
            for (int i = 0; i < runtime.Count; i++)
            {
                int typeId = data.typeIds != null && i < data.typeIds.Length ? data.typeIds[i] : 0;
                if ((uint)typeId >= (uint)typeCount) continue;
                float scale = data.scales != null && i < data.scales.Length ? Mathf.Abs(data.scales[i]) : 1f;
                Vector3 extent = Vector3.one * (_typeBoundsRadii[typeId] * scale);
                bounds.Encapsulate(data.positions[i] - extent);
                bounds.Encapsulate(data.positions[i] + extent);
            }
            bounds.Expand(Mathf.Max(0f, chunkBoundsPadding) * 2f);
            cache.localBounds = bounds;
            cache.padding = chunkBoundsPadding;
            cache.sourceTypes = data.typeIds;
            cache.typeLayoutVersion = _typeLayoutVersion;
        }
        cache.worldBounds = cache.localBounds;
        cache.worldBounds.center += origin;
        return cache;
    }

    private void Render(AsteroidFieldData data, ChunkCache cache, int visibleCount,
        Vector3 cameraPosition, float d0Squared, float d1Squared, bool rotate, float dt, Camera cam)
    {
        var buckets = cache.buckets;
        int typeCount = _validTypes.Length;
        for (int t = 0; t < typeCount; t++)
            for (int lod = 0; lod < 3; lod++) buckets[t, lod].Clear();

        var destroyed = cache.runtime.Destroyed;
        var types = data.typeIds;
        var angular = data.angularVelocityDeg;
        rotate &= angular != null && angular.Length >= cache.runtime.Count;
        Vector3 localCamera = cameraPosition - cache.worldOrigin;

        for (int i = 0; i < visibleCount; i++)
        {
            if (destroyed[i]) continue;
            int typeId = types != null && i < types.Length ? types[i] : 0;
            if ((uint)typeId >= (uint)typeCount || !_validTypes[typeId]) continue;

            if (rotate && angular[i].sqrMagnitude >= 0.000001f)
            {
                Quaternion q = NormalizeSafe(cache.rotations[i] * Quaternion.Euler(angular[i] * dt));
                cache.rotations[i] = q;
                float scale = data.scales != null && i < data.scales.Length ? data.scales[i] : 1f;
                cache.matrices[i] = Matrix4x4.TRS(cache.worldOrigin + data.positions[i], q, Vector3.one * scale);
            }

            float distanceSquared = (localCamera - data.positions[i]).sqrMagnitude;
            int lod = distanceSquared < d0Squared ? 0 : (distanceSquared < d1Squared ? 1 : 2);
            buckets[typeId, lod].Add(i);
        }

        for (int t = 0; t < typeCount; t++)
        {
            if (!_validTypes[t]) continue;
            AsteroidTypeRender type = typeRenders[t];
            DrawBucket(type.lod0Mesh, type.material, buckets[t, 0], cache, lod0VoxelCellSize, cam);
            DrawBucket(type.lod1Mesh, type.material, buckets[t, 1], cache, lod1VoxelCellSize, cam);
            DrawBucket(type.lod2Mesh, type.material, buckets[t, 2], cache, lod2VoxelCellSize, cam);
        }
    }

    private void DrawBucket(Mesh mesh, Material material, List<int> indices,
        ChunkCache cache, float voxelCellSize, Camera cam)
    {
        if (indices.Count == 0) return;
        _mpb.SetFloat(VoxelCellSizeID, voxelCellSize);
        for (int offset = 0; offset < indices.Count; offset += MaxInstancesPerCall)
        {
            int count = Mathf.Min(MaxInstancesPerCall, indices.Count - offset);
            for (int j = 0; j < count; j++) _batchBuffer[j] = cache.matrices[indices[offset + j]];
            Graphics.DrawMeshInstanced(mesh, 0, material, _batchBuffer, count, _mpb,
                shadowCasting, receiveShadows, renderLayer, cam, LightProbeUsage.Off, null);
        }
    }

    // These compatibility methods now update the SAME state that collision reads.
    // Density is an independent visible-count limit and is never stored in this mask.
    public void ClearHidden(AsteroidFieldData data)
    {
        if (data) data.ResetRuntimeDestruction();
    }

    public void SetInstanceHidden(AsteroidFieldData data, int index, bool hidden)
    {
        if (!data) return;
        var runtime = data.Runtime;
        if ((uint)index < (uint)runtime.Count) runtime.Destroyed[index] = hidden;
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        float lengthSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-12f) return Quaternion.identity;
        float inverseLength = 1f / Mathf.Sqrt(lengthSquared);
        return new Quaternion(q.x * inverseLength, q.y * inverseLength, q.z * inverseLength, q.w * inverseLength);
    }
}
