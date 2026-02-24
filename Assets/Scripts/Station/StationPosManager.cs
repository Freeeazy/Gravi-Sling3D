using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a 3x3x3 grid of StationFieldData around the player.
/// Deterministic station placement per chunk coord with neighbor exclusion.
/// </summary>
public class StationPosManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Chunk Volume")]
    [Min(1f)] public float chunkSize = 1000f;
    [Min(1)] public int gridWidth = 3; // keep odd

    [Header("Generation")]
    public int globalSeed = 12345;
    public StationRuntimeGenerator.Settings settings = new StationRuntimeGenerator.Settings();

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool logChunkCreates = true;

    /// <summary>
    /// Raised whenever a chunk coord is assigned a StationFieldData.
    /// For station data, contents DO change when moved (we refill for new coord).
    /// </summary>
    public event Action<Vector3Int, StationFieldData> OnChunkCreated;

    private readonly Dictionary<Vector3Int, StationFieldData> _chunks = new Dictionary<Vector3Int, StationFieldData>(64);

    private readonly List<Vector3Int> _toRemove = new List<Vector3Int>(64);
    private readonly List<Vector3Int> _toAdd = new List<Vector3Int>(64);

    private Vector3Int _lastCenterChunk;

    public IReadOnlyDictionary<Vector3Int, StationFieldData> Chunks => _chunks;

    [System.Serializable]
    public struct StationPose
    {
        public Vector3 worldPos;
        public Quaternion worldRot;
        public float uniformScale;
    }

    private void Awake()
    {
        if (!player) player = Camera.main ? Camera.main.transform : transform;

        gridWidth = Mathf.Max(1, gridWidth);
        if (gridWidth % 2 == 0) gridWidth += 1;

        chunkSize = Mathf.Max(1f, chunkSize);

        if (settings == null) settings = new StationRuntimeGenerator.Settings();
        settings.chunkSize = chunkSize;
    }

    private void Start()
    {
        if (generateOnStart)
            EnsureGrid();

        _lastCenterChunk = WorldToChunkCoord(player.position);
    }

    private void Update()
    {
        Vector3Int newCenter = WorldToChunkCoord(player.position);
        if (newCenter == _lastCenterChunk) return;

        ShiftGrid(newCenter);
        _lastCenterChunk = newCenter;
    }

    private void EnsureGrid()
    {
        _chunks.Clear();

        Vector3Int center = WorldToChunkCoord(player ? player.position : Vector3.zero);
        int half = gridWidth / 2;

        for (int dz = -half; dz <= half; dz++)
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    Vector3Int coord = new Vector3Int(center.x + dx, center.y + dy, center.z + dz);
                    CreateChunk(coord);
                }
    }

    private void CreateChunk(Vector3Int coord)
    {
        Vector3 origin = ChunkCoordToWorldOrigin(coord);

        StationFieldData data = StationRuntimeGenerator.GenerateChunk(
            settings,
            origin,
            coord,
            globalSeed
        );

        _chunks[coord] = data;

        if (logChunkCreates)
        {
            string s = data.hasStation ? $"STATION local={data.localPosition}" : "no station";
            //Debug.Log($"[StationPosManager] Created chunk {coord} origin={origin} -> {s}");
        }

        OnChunkCreated?.Invoke(coord, data);
    }

    private void RefillChunkDataForCoord(StationFieldData data, Vector3Int coord)
    {
        if (data == null) return;
        Vector3 origin = ChunkCoordToWorldOrigin(coord);

        StationRuntimeGenerator.FillExistingChunk(
            data,
            settings,
            origin,
            coord,
            globalSeed
        );

        if (logChunkCreates)
        {
            string s = data.hasStation ? $"STATION local={data.localPosition}" : "no station";
            //Debug.Log($"[StationPosManager] Refilled data for coord {coord} origin={origin} -> {s}");
        }

        OnChunkCreated?.Invoke(coord, data);
    }

    private void ShiftGrid(Vector3Int newCenter)
    {
        int half = gridWidth / 2;

        _toRemove.Clear();
        _toAdd.Clear();

        foreach (var kv in _chunks)
        {
            Vector3Int c = kv.Key;
            if (Mathf.Abs(c.x - newCenter.x) > half ||
                Mathf.Abs(c.y - newCenter.y) > half ||
                Mathf.Abs(c.z - newCenter.z) > half)
            {
                _toRemove.Add(c);
            }
        }

        for (int dz = -half; dz <= half; dz++)
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    Vector3Int want = new Vector3Int(newCenter.x + dx, newCenter.y + dy, newCenter.z + dz);
                    if (!_chunks.ContainsKey(want))
                        _toAdd.Add(want);
                }

        int moves = Mathf.Min(_toRemove.Count, _toAdd.Count);

        // Reuse data objects, but REFILL for their new coord (stations depend on coord!)
        for (int i = 0; i < moves; i++)
        {
            Vector3Int oldCoord = _toRemove[i];
            Vector3Int newCoord = _toAdd[i];

            StationFieldData data = _chunks[oldCoord];
            _chunks.Remove(oldCoord);

            _chunks[newCoord] = data;

            RefillChunkDataForCoord(data, newCoord);
        }

        // Any remaining adds get new allocations
        for (int i = moves; i < _toAdd.Count; i++)
            CreateChunk(_toAdd[i]);
    }

    public Vector3Int WorldToChunkCoord(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x / chunkSize);
        int cy = Mathf.FloorToInt(worldPos.y / chunkSize);
        int cz = Mathf.FloorToInt(worldPos.z / chunkSize);
        return new Vector3Int(cx, cy, cz);
    }

    public Vector3 ChunkCoordToWorldOrigin(Vector3Int coord)
    {
        return new Vector3(coord.x * chunkSize, coord.y * chunkSize, coord.z * chunkSize);
    }

    /// <summary>
    /// Called by StationInstancedRenderer every frame.
    /// Appends root/world matrices for all currently-active stations.
    /// </summary>
    public void FillStationRootMatrices(List<Matrix4x4> outMatrices)
    {
        if (outMatrices == null) return;

        // caller clears; but being defensive is fine if you prefer
        // outMatrices.Clear();

        foreach (var kv in _chunks)
        {
            Vector3Int coord = kv.Key;
            StationFieldData data = kv.Value;
            if (!data || !data.hasStation)
                continue;

            Vector3 origin = ChunkCoordToWorldOrigin(coord);

            // local -> world
            Vector3 worldPos = data.WorldPosition(origin);
            Quaternion worldRot = data.WorldRotation();

            worldRot = data.localRotation;

            // Scale
            float s = 1f;

            // OPTION A: if StationFieldData has it:
            // s = (data.uniformScale <= 0f) ? 1f : data.uniformScale;

            // OPTION B: if scale is only in settings:
            // s = Mathf.Max(0.0001f, settings.uniformScale);

            outMatrices.Add(Matrix4x4.TRS(worldPos, worldRot, Vector3.one * s));
        }
    }
    /// <summary>
    /// Lightweight snapshot for quest picking + beacon targeting.
    /// </summary>
    public struct StationWorldInfo
    {
        public Vector3Int coord;
        public Vector3 worldPos;
        public Quaternion worldRot;
        public StationFieldData data;
    }

    /// <summary>
    /// Appends all currently-active stations with their world pose + coord.
    /// </summary>
    public void FillActiveStations(List<StationWorldInfo> outStations)
    {
        if (outStations == null) return;

        foreach (var kv in _chunks)
        {
            var coord = kv.Key;
            var data = kv.Value;
            if (!data || !data.hasStation) continue;

            Vector3 origin = ChunkCoordToWorldOrigin(coord);
            Vector3 worldPos = data.WorldPosition(origin);

            // Your data currently uses localRotation already.
            Quaternion worldRot = data.localRotation;

            outStations.Add(new StationWorldInfo
            {
                coord = coord,
                worldPos = worldPos,
                worldRot = worldRot,
                data = data
            });
        }
    }
    public bool TryGetStationWorldPose(Vector3Int coord, out Vector3 worldPos, out Quaternion worldRot)
    {
        worldPos = default;
        worldRot = default;

        // If it's loaded, use real data.
        if (_chunks.TryGetValue(coord, out var data) && data && data.hasStation)
        {
            Vector3 origin = ChunkCoordToWorldOrigin(coord);
            worldPos = data.WorldPosition(origin);
            worldRot = data.localRotation;
            return true;
        }

        // Otherwise do virtual lookup.
        Vector3 origin2 = ChunkCoordToWorldOrigin(coord);
        return StationRuntimeGenerator.TryGetStationPose_NoAlloc(
            settings,
            origin2,
            coord,
            globalSeed,
            out worldPos,
            out worldRot
        );
    }

#if UNITY_EDITOR
    [ContextMenu("Regenerate All Stations (Editor Only)")]
    private void RegenerateAllEditorOnly()
    {
        foreach (var kv in _chunks)
            UnityEngine.Object.DestroyImmediate(kv.Value, allowDestroyingAssets: true);

        _chunks.Clear();
        EnsureGrid();
    }
#endif
}
