using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime chunk manager: keeps a 3x3x3 set of AsteroidFieldData chunks around the player.
/// For now: just guarantees the 27 exist and are stable. Recycling/shift logic can be added next.
/// </summary>
public class AsteroidPosManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player or camera root to track.")]
    public Transform player;

    [Header("Collision")]
    public AsteroidFieldCollisionDetector collisionDetector;

    [Header("Chunk Volume")]
    [Min(1f)] public float chunkSize = 1000f; // 1k x 1k x 1k
    [Tooltip("3 means 3x3x3. Keep odd.")]
    [Min(1)] public int gridWidth = 3;

    [Header("Generation")]
    public int globalSeed = 12345;
    public AsteroidFieldRuntimeGenerator.Settings settings = new AsteroidFieldRuntimeGenerator.Settings();

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool logChunkCreates = true;

    /// <summary>
    /// Raised whenever a chunk coord is assigned a data object.
    /// IMPORTANT: data contents don't change; only its coord changes.
    /// </summary>
    public event Action<Vector3Int, AsteroidFieldData> OnChunkCreated;

    // coord -> data currently occupying that coord (changes on shift)
    private readonly Dictionary<Vector3Int, AsteroidFieldData> _chunks = new Dictionary<Vector3Int, AsteroidFieldData>(64);

    // reusable list buffers to avoid GC in ShiftGrid
    private readonly List<Vector3Int> _toRemove = new List<Vector3Int>(64);
    private readonly List<Vector3Int> _toAdd = new List<Vector3Int>(64);

    private Vector3Int _lastCenterChunk;

    private void Awake()
    {
        if (!player) player = Camera.main ? Camera.main.transform : transform;
        gridWidth = Mathf.Max(1, gridWidth);

        if (gridWidth % 2 == 0) gridWidth += 1; // enforce odd

        if (!collisionDetector)
            collisionDetector = FindFirstObjectByType<AsteroidFieldCollisionDetector>();
    }

    private void Start()
    {
        if (generateOnStart)
            EnsureGrid();

        _lastCenterChunk = WorldToChunkCoord(player.position);
        UpdateCollisionChunk(_lastCenterChunk);
    }

    private void Update()
    {
        Vector3Int newCenter = WorldToChunkCoord(player.position);
        if (newCenter == _lastCenterChunk) return;

        ShiftGrid_NoRegen(newCenter);
        _lastCenterChunk = newCenter;

        UpdateCollisionChunk(newCenter);
    }

    public IReadOnlyDictionary<Vector3Int, AsteroidFieldData> Chunks => _chunks;

    private void EnsureGrid()
    {
        if (!AsteroidFieldRuntimeGenerator.CanGenerate(settings))
            return;

        _chunks.Clear();

        Vector3Int center = WorldToChunkCoord(player ? player.position : Vector3.zero);
        int half = gridWidth / 2;

        for (int dz = -half; dz <= half; dz++)
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    Vector3Int coord = new Vector3Int(center.x + dx, center.y + dy, center.z + dz);
                    CreateChunk_OneTime(coord);
                }
    }

    private void CreateChunk_OneTime(Vector3Int coord)
    {
        Vector3 origin = ChunkCoordToWorldOrigin(coord);
        int seed = HashSeed(globalSeed, coord);

        // IMPORTANT: generate asteroids WITHOUT planets so nothing ever forces regen.
        // (You’ll cull near planets in rendering/collision later.)
        AsteroidFieldData data = AsteroidFieldRuntimeGenerator.GenerateChunk(
            settings,
            origin,
            chunkSize,
            seed,
            localSpace: true
        );

        _chunks[coord] = data;

        if (logChunkCreates)
            Debug.Log($"[AsteroidPosManager] Created chunk {coord} origin={origin} count={data.count}");

        OnChunkCreated?.Invoke(coord, data);
    }

    // --- shifting WITHOUT regeneration ---
    private void ShiftGrid_NoRegen(Vector3Int newCenter)
    {
        // Compute desired coords around newCenter
        int half = gridWidth / 2;

        _toRemove.Clear();
        _toAdd.Clear();

        // mark which existing coords are now out of range
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

        // find coords we need that we don't have
        for (int dz = -half; dz <= half; dz++)
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    Vector3Int want = new Vector3Int(newCenter.x + dx, newCenter.y + dy, newCenter.z + dz);
                    if (!_chunks.ContainsKey(want))
                        _toAdd.Add(want);
                }

        // Reassign data objects from old coords to new coords
        int moves = Mathf.Min(_toRemove.Count, _toAdd.Count);

        for (int i = 0; i < moves; i++)
        {
            Vector3Int oldCoord = _toRemove[i];
            Vector3Int newCoord = _toAdd[i];

            AsteroidFieldData data = _chunks[oldCoord];
            _chunks.Remove(oldCoord);

            _chunks[newCoord] = data;

            if (logChunkCreates)
                Debug.Log($"[AsteroidPosManager] Moved chunk data {oldCoord} -> {newCoord}");

            // Tell listeners the coord assignment changed (data contents did NOT change)
            OnChunkCreated?.Invoke(newCoord, data);
        }

        // Safety: if something got out of sync, fill missing with new allocations (rare)
        for (int i = moves; i < _toAdd.Count; i++)
        {
            CreateChunk_OneTime(_toAdd[i]);
        }
    }
    private void UpdateCollisionChunk(Vector3Int centerChunk)
    {
        if (!collisionDetector) return;

        if (_chunks.TryGetValue(centerChunk, out var data))
        {
            collisionDetector.fieldData = data;
            collisionDetector.chunkWorldOrigin = ChunkCoordToWorldOrigin(centerChunk);
            collisionDetector.Rebuild();
        }
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

    private static int HashSeed(int baseSeed, Vector3Int c)
    {
        unchecked
        {
            int h = baseSeed;
            h = (h * 397) ^ c.x;
            h = (h * 397) ^ c.y;
            h = (h * 397) ^ c.z;
            return h;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Regenerate All Chunks (Editor Only)")]
    private void RegenerateAllEditorOnly()
    {
        foreach (var kv in _chunks)
            UnityEngine.Object.DestroyImmediate(kv.Value, allowDestroyingAssets: true);

        _chunks.Clear();
        EnsureGrid();
    }
#endif
}
