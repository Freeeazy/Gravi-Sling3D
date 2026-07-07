using System;
using System.Collections.Generic;
using UnityEngine;

public class POIPosManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Chunk Volume")]
    [Min(1f)] public float chunkSize = 1000f;
    [Min(1)] public int gridWidth = 3;

    [Header("Generation")]
    public int globalSeed = 12345;
    public POIRuntimeGenerator.Settings settings = new POIRuntimeGenerator.Settings();

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool logChunkCreates = true;

    public event Action<Vector3Int, POIFieldData> OnChunkCreated;
    public event Action<Vector3Int> OnChunkRemoved;

    private readonly Dictionary<Vector3Int, POIFieldData> _chunks = new Dictionary<Vector3Int, POIFieldData>(64);

    private readonly List<Vector3Int> _toRemove = new List<Vector3Int>(64);
    private readonly List<Vector3Int> _toAdd = new List<Vector3Int>(64);

    private Vector3Int _lastCenterChunk;

    public IReadOnlyDictionary<Vector3Int, POIFieldData> Chunks => _chunks;

    public struct POIWorldInfo
    {
        public Vector3Int coord;
        public POIType type;
        public Vector3 worldPos;
        public Quaternion worldRot;
        public POIFieldData data;
    }

    private void Awake()
    {
        if (!player)
            player = Camera.main ? Camera.main.transform : transform;

        gridWidth = Mathf.Max(1, gridWidth);
        if (gridWidth % 2 == 0)
            gridWidth += 1;

        chunkSize = Mathf.Max(1f, chunkSize);

        if (settings == null)
            settings = new POIRuntimeGenerator.Settings();

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
        if (newCenter == _lastCenterChunk)
            return;

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

        POIFieldData data = POIRuntimeGenerator.GenerateChunk(
            settings,
            origin,
            coord,
            globalSeed
        );

        _chunks[coord] = data;

        if (logChunkCreates)
        {
            string label = data && data.HasPOI ? data.poiType.ToString() : "None";
            // Debug.Log($"[POIPosManager] Created chunk {coord} -> {label}");
        }

        OnChunkCreated?.Invoke(coord, data);
    }

    private void RefillChunkDataForCoord(POIFieldData data, Vector3Int coord)
    {
        if (data == null) return;

        Vector3 origin = ChunkCoordToWorldOrigin(coord);

        POIRuntimeGenerator.FillExistingChunk(
            data,
            settings,
            origin,
            coord,
            globalSeed
        );

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

        for (int i = 0; i < moves; i++)
        {
            Vector3Int oldCoord = _toRemove[i];
            Vector3Int newCoord = _toAdd[i];

            POIFieldData data = _chunks[oldCoord];

            _chunks.Remove(oldCoord);
            OnChunkRemoved?.Invoke(oldCoord);

            _chunks[newCoord] = data;
            RefillChunkDataForCoord(data, newCoord);
        }

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

    public void FillActivePOIs(List<POIWorldInfo> outPOIs)
    {
        if (outPOIs == null) return;

        foreach (var kv in _chunks)
        {
            Vector3Int coord = kv.Key;
            POIFieldData data = kv.Value;

            if (!data || !data.HasPOI)
                continue;

            Vector3 origin = ChunkCoordToWorldOrigin(coord);

            outPOIs.Add(new POIWorldInfo
            {
                coord = coord,
                type = data.poiType,
                worldPos = data.WorldPosition(origin),
                worldRot = data.WorldRotation(),
                data = data
            });
        }
    }

    public void FillActiveStations(List<POIWorldInfo> outStations)
    {
        if (outStations == null) return;

        foreach (var kv in _chunks)
        {
            Vector3Int coord = kv.Key;
            POIFieldData data = kv.Value;

            if (!data || !data.IsStation)
                continue;

            Vector3 origin = ChunkCoordToWorldOrigin(coord);

            outStations.Add(new POIWorldInfo
            {
                coord = coord,
                type = data.poiType,
                worldPos = data.WorldPosition(origin),
                worldRot = data.WorldRotation(),
                data = data
            });
        }
    }

    public bool TryGetPOIWorldPose(Vector3Int coord, out POIType type, out Vector3 worldPos, out Quaternion worldRot)
    {
        type = POIType.None;
        worldPos = default;
        worldRot = default;

        if (_chunks.TryGetValue(coord, out POIFieldData data) && data && data.HasPOI)
        {
            Vector3 origin = ChunkCoordToWorldOrigin(coord);

            type = data.poiType;
            worldPos = data.WorldPosition(origin);
            worldRot = data.WorldRotation();

            return true;
        }

        Vector3 virtualOrigin = ChunkCoordToWorldOrigin(coord);

        return POIRuntimeGenerator.TryGetPOIPoseNoAlloc(
            settings,
            virtualOrigin,
            coord,
            globalSeed,
            out type,
            out worldPos,
            out worldRot
        );
    }

    public bool TryGetStationWorldPose(Vector3Int coord, out Vector3 worldPos, out Quaternion worldRot)
    {
        worldPos = default;
        worldRot = default;

        if (_chunks.TryGetValue(coord, out POIFieldData data) && data && data.IsStation)
        {
            Vector3 origin = ChunkCoordToWorldOrigin(coord);

            worldPos = data.WorldPosition(origin);
            worldRot = data.WorldRotation();

            return true;
        }

        Vector3 virtualOrigin = ChunkCoordToWorldOrigin(coord);

        return POIRuntimeGenerator.TryGetStationPoseNoAlloc(
            settings,
            virtualOrigin,
            coord,
            globalSeed,
            out worldPos,
            out worldRot
        );
    }
}