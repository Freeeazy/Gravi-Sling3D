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

    [Header("Asteroid Density")]
    public bool useDensity = true;

    [Tooltip("Lowest possible density multiplier. 0.25 means 25% of the generated asteroids can show.")]
    [Range(0f, 1f)] public float minDensity = 0.35f;

    [Tooltip("Highest possible density multiplier. 0.75 means at most 75% of generated asteroids can show.")]
    [Range(0f, 1f)] public float maxDensity = 0.75f;

    [Tooltip("How zoomed-in/out the density noise is. Smaller = smoother larger regions.")]
    [Min(0.0001f)] public float densityFrequency = 0.12f;

    [Tooltip("Separate seed just for density noise.")]
    public int densitySeed = 9999;

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

            if (logChunkCreates)
            {
                float density = GetDensityForChunk(newCoord);
                int visible = GetVisibleCountForChunk(newCoord, data);
                Debug.Log($"[AsteroidDensity] Chunk {newCoord} density={density:0.00}, visible={visible}/{data.count}");
            }
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

            int visible = GetVisibleCountForChunk(centerChunk, data);
            collisionDetector.Rebuild(visible);
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
    public float GetDensityForChunk(Vector3Int coord)
    {
        if (!useDensity)
            return 1f;

        // Sample at chunk center in "chunk coordinate space"
        Vector3 sample = new Vector3(
            coord.x * densityFrequency,
            coord.y * densityFrequency,
            coord.z * densityFrequency
        );

        float noise01 = SmoothValueNoise3D(sample, densitySeed);

        return Mathf.Lerp(minDensity, maxDensity, noise01);
    }

    public int GetVisibleCountForChunk(Vector3Int coord, AsteroidFieldData data)
    {
        if (data == null || data.count <= 0)
            return 0;

        float density = GetDensityForChunk(coord);
        return Mathf.Clamp(Mathf.RoundToInt(data.count * density), 0, data.count);
    }
    private static float SmoothValueNoise3D(Vector3 p, int seed)
    {
        int x0 = Mathf.FloorToInt(p.x);
        int y0 = Mathf.FloorToInt(p.y);
        int z0 = Mathf.FloorToInt(p.z);

        int x1 = x0 + 1;
        int y1 = y0 + 1;
        int z1 = z0 + 1;

        float tx = SmoothStep01(p.x - x0);
        float ty = SmoothStep01(p.y - y0);
        float tz = SmoothStep01(p.z - z0);

        float c000 = Hash01(x0, y0, z0, seed);
        float c100 = Hash01(x1, y0, z0, seed);
        float c010 = Hash01(x0, y1, z0, seed);
        float c110 = Hash01(x1, y1, z0, seed);

        float c001 = Hash01(x0, y0, z1, seed);
        float c101 = Hash01(x1, y0, z1, seed);
        float c011 = Hash01(x0, y1, z1, seed);
        float c111 = Hash01(x1, y1, z1, seed);

        float x00 = Mathf.Lerp(c000, c100, tx);
        float x10 = Mathf.Lerp(c010, c110, tx);
        float x01 = Mathf.Lerp(c001, c101, tx);
        float x11 = Mathf.Lerp(c011, c111, tx);

        float y0v = Mathf.Lerp(x00, x10, ty);
        float y1v = Mathf.Lerp(x01, x11, ty);

        return Mathf.Lerp(y0v, y1v, tz);
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Hash01(int x, int y, int z, int seed)
    {
        unchecked
        {
            int h = seed;
            h = h * 374761393 + x * 668265263;
            h = h * 1274126177 + y * 2246822519.GetHashCode();
            h = h * 3266489917.GetHashCode() + z * 1597334677;

            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;

            uint u = (uint)h;
            return u / (float)uint.MaxValue;
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
