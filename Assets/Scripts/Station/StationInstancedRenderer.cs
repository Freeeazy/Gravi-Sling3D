using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a station prefab using Graphics.DrawMeshInstanced by caching all child meshes/materials once.
/// Supports multi-mesh prefabs (children) by baking each child's local-to-root matrix.
/// </summary>
public class StationInstancedRenderer : MonoBehaviour
{
    [Header("Prefab Source (same for all stations)")]
    public GameObject stationPrefab;

    [Header("Data Source")]
    public StationPosManager posManager;

    [Tooltip("If null, uses Camera.main.")]
    public Camera renderCamera;

    [Header("Rendering")]
    public ShadowCastingMode shadowCasting = ShadowCastingMode.On;
    public bool receiveShadows = true;
    public int renderLayer = 0;

    [Tooltip("Only render in play mode? If false, will also render in edit mode.")]
    public bool onlyRenderInPlayMode = false;

    private const int MaxInstancesPerCall = 1023;

    [Serializable]
    private struct Part
    {
        public Mesh mesh;
        public int subMeshIndex;
        public Material material;

        // Transform from prefab root -> this mesh (baked once)
        public Matrix4x4 localToRoot;
    }

    private readonly List<Part> _parts = new List<Part>(32);
    private readonly List<Matrix4x4> _stationRootMatrices = new List<Matrix4x4>(64);

    // Scratch buffer for one DrawMeshInstanced call.
    private static class MatrixBufferCache
    {
        private static Matrix4x4[] _buffer;
        public static Matrix4x4[] Get()
        {
            if (_buffer == null || _buffer.Length != MaxInstancesPerCall)
                _buffer = new Matrix4x4[MaxInstancesPerCall];
            return _buffer;
        }
    }

    private void OnEnable()
    {
        RebuildPrefabCache();
    }

    [ContextMenu("Rebuild Prefab Cache")]
    public void RebuildPrefabCache()
    {
        _parts.Clear();
        if (!stationPrefab) return;

#if UNITY_EDITOR
        // If this is a scene instance, we can cache directly
        if (stationPrefab.scene.IsValid())
        {
            CacheFromRoot(stationPrefab.transform);
            return;
        }

        // Editor prefab-asset path: loads prefab contents into a staging scene
        var path = UnityEditor.AssetDatabase.GetAssetPath(stationPrefab);
        var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        try
        {
            CacheFromRoot(root.transform);
        }
        finally
        {
            UnityEditor.PrefabUtility.UnloadPrefabContents(root);
        }
#else
    CacheFromPrefabByInstantiating();
#endif
    }
    private void CacheFromPrefabByInstantiating()
    {
        // Instantiate once, cache, then destroy. Works in builds.
        var temp = Instantiate(stationPrefab);
        temp.name = $"{stationPrefab.name}_CACHE_TEMP";
        temp.hideFlags = HideFlags.HideAndDontSave;

        // Keep it from doing anything weird if it has scripts/animators etc.
        temp.SetActive(false);

        try
        {
            CacheFromRoot(temp.transform);
        }
        finally
        {
            Destroy(temp);
        }
    }

    private void CacheFromRoot(Transform root)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;

            var mr = mf.GetComponent<MeshRenderer>();
            if (!mr) continue;

            var mats = mr.sharedMaterials;
            if (mats == null || mats.Length == 0) continue;

            Matrix4x4 localToRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;

            int useCount = Mathf.Min(mesh.subMeshCount, mats.Length);
            for (int sub = 0; sub < useCount; sub++)
            {
                var mat = mats[sub];
                if (!mat) continue;

                if (!mat.enableInstancing)
                    mat.enableInstancing = true;

                _parts.Add(new Part
                {
                    mesh = mesh,
                    subMeshIndex = sub,
                    material = mat,
                    localToRoot = localToRoot
                });
            }
        }
    }

    private void Update()
    {
        if (onlyRenderInPlayMode && !Application.isPlaying)
            return;

        if (!stationPrefab || !posManager)
            return;

        var cam = renderCamera ? renderCamera : Camera.main;
        if (!cam)
            return;

        // Gather station root matrices (world TRS) from the manager
        _stationRootMatrices.Clear();
        posManager.FillStationRootMatrices(_stationRootMatrices);

        int stationCount = _stationRootMatrices.Count;
        if (stationCount == 0 || _parts.Count == 0)
            return;

        // For each part of the prefab, draw all station instances
        for (int p = 0; p < _parts.Count; p++)
        {
            Part part = _parts[p];

            int offset = 0;
            while (offset < stationCount)
            {
                int batchCount = Mathf.Min(MaxInstancesPerCall, stationCount - offset);
                var buffer = MatrixBufferCache.Get();

                // Each instance matrix = stationRoot * partLocalToRoot
                for (int i = 0; i < batchCount; i++)
                {
                    buffer[i] = _stationRootMatrices[offset + i] * part.localToRoot;
                }

                Graphics.DrawMeshInstanced(
                    part.mesh,
                    part.subMeshIndex,
                    part.material,
                    buffer,
                    batchCount,
                    null,
                    shadowCasting,
                    receiveShadows,
                    renderLayer,
                    cam,
                    LightProbeUsage.Off,
                    null
                );

                offset += batchCount;
            }
        }
    }
}
