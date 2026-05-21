using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class StationInstancedRenderer : MonoBehaviour
{
    [Header("Prefab Source")]
    public GameObject stationPrefab;

    [Header("Data Source")]
    public StationPosManager posManager;

    [Tooltip("If null, uses Camera.main.")]
    public Camera renderCamera;

    [Header("Rendering")]
    public ShadowCastingMode shadowCasting = ShadowCastingMode.On;
    public bool receiveShadows = true;
    public int renderLayer = 0;

    [Header("Instance Scale")]
    public float stationScale = 1f;

    [Tooltip("Only render in play mode? If false, will also render in edit mode.")]
    public bool onlyRenderInPlayMode = false;

    private const int MaxInstancesPerCall = 1023;

    private Mesh _stationMesh;
    private Material[] _stationMaterials;

    private readonly List<Matrix4x4> _stationRootMatrices = new List<Matrix4x4>(64);

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

    private void OnValidate()
    {
        if (stationPrefab != null)
            RebuildPrefabCache();
    }

    [ContextMenu("Rebuild Prefab Cache")]
    public void RebuildPrefabCache()
    {
        _stationMesh = null;
        _stationMaterials = null;

        if (!stationPrefab)
            return;

        MeshFilter meshFilter = stationPrefab.GetComponentInChildren<MeshFilter>(true);

        if (!meshFilter)
        {
            Debug.LogWarning($"{stationPrefab.name} has no MeshFilter.");
            return;
        }

        MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();

        if (!meshRenderer)
        {
            Debug.LogWarning($"{meshFilter.name} has a MeshFilter but no MeshRenderer.");
            return;
        }

        _stationMesh = meshFilter.sharedMesh;
        _stationMaterials = meshRenderer.sharedMaterials;

        if (!_stationMesh)
        {
            Debug.LogWarning($"{meshFilter.name} has no mesh assigned.");
            return;
        }

        if (_stationMaterials == null || _stationMaterials.Length == 0)
        {
            Debug.LogWarning($"{meshRenderer.name} has no materials assigned.");
            return;
        }

        foreach (Material mat in _stationMaterials)
        {
            if (mat != null && !mat.enableInstancing)
                mat.enableInstancing = true;
        }

        Debug.Log($"Cached single mesh '{_stationMesh.name}' with {_stationMaterials.Length} materials from {stationPrefab.name}.");
    }

    private void Update()
    {
        if (onlyRenderInPlayMode && !Application.isPlaying)
            return;

        if (!stationPrefab || !posManager || !_stationMesh || _stationMaterials == null)
            return;

        Camera cam = renderCamera ? renderCamera : Camera.main;
        if (!cam)
            return;

        _stationRootMatrices.Clear();
        posManager.FillStationRootMatrices(_stationRootMatrices);

        int stationCount = _stationRootMatrices.Count;
        if (stationCount == 0)
            return;

        Matrix4x4 scaleMatrix = Matrix4x4.Scale(Vector3.one * stationScale);

        int subMeshDrawCount = Mathf.Min(_stationMesh.subMeshCount, _stationMaterials.Length);

        for (int subMeshIndex = 0; subMeshIndex < subMeshDrawCount; subMeshIndex++)
        {
            Material mat = _stationMaterials[subMeshIndex];

            if (!mat)
                continue;

            int offset = 0;

            while (offset < stationCount)
            {
                int batchCount = Mathf.Min(MaxInstancesPerCall, stationCount - offset);
                Matrix4x4[] buffer = MatrixBufferCache.Get();

                for (int i = 0; i < batchCount; i++)
                {
                    buffer[i] = _stationRootMatrices[offset + i] * scaleMatrix;
                }

                Graphics.DrawMeshInstanced(
                    _stationMesh,
                    subMeshIndex,
                    mat,
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