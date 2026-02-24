using System;
using UnityEngine;

/// <summary>
/// Deterministic station placement per chunk coord with neighbor exclusion.
/// Produces 0 or 1 station per chunk (for now).
/// </summary>
public static class StationRuntimeGenerator
{
    [Serializable]
    public class Settings
    {
        [Header("Chunk")]
        public float chunkSize = 1000f;

        [Header("Station density")]
        [Tooltip("1 in N chunks becomes a 'candidate' before neighbor exclusion. Example: 12 => ~8% candidates.")]
        [Min(1)] public int candidateModulo = 12;

        [Header("Neighbor Exclusion")]
        [Tooltip("Chunk radius to exclude other stations. 1 => blocks all 26 neighbors (3x3x3 minus center).")]
        [Range(0, 4)] public int excludeNeighborRadius = 1;

        [Header("Station size")]
        public float orbitRadius = 280f;

        [Header("Placement inside chunk")]
        [Tooltip("How far from chunk borders we keep the station local position.")]
        [Min(0f)] public float borderPadding = 150f;
    }

    /// <summary>
    /// Returns true if this coord should have a station anchor using "winner in neighborhood".
    /// </summary>
    public static bool IsStationAnchor(Settings s, int globalSeed, Vector3Int coord)
    {
        if (s == null) return false;

        // Must be a candidate at all
        if (!IsCandidate(s, globalSeed, coord))
            return false;

        // Score-based winner among candidate neighbors within radius
        float myScore = Score01(globalSeed, coord);

        int r = Mathf.Max(0, s.excludeNeighborRadius);

        for (int dz = -r; dz <= r; dz++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;

                    Vector3Int nb = new Vector3Int(coord.x + dx, coord.y + dy, coord.z + dz);

                    if (!IsCandidate(s, globalSeed, nb))
                        continue;

                    float nbScore = Score01(globalSeed, nb);

                    // If any neighbor has a higher score, we lose
                    if (nbScore > myScore)
                        return false;

                    // Tie-breaker: if equal scores (super rare), pick lexicographically smaller coord as winner
                    if (Mathf.Approximately(nbScore, myScore))
                    {
                        if (LexicographicallySmaller(nb, coord))
                            return false;
                    }
                }

        return true;
    }

    public static StationFieldData GenerateChunk(Settings s, Vector3 chunkWorldOrigin, Vector3Int coord, int globalSeed)
    {
        var data = ScriptableObject.CreateInstance<StationFieldData>();
        FillExistingChunk(data, s, chunkWorldOrigin, coord, globalSeed);
        return data;
    }

    public static void FillExistingChunk(StationFieldData data, Settings s, Vector3 chunkWorldOrigin, Vector3Int coord, int globalSeed)
    {
        if (data == null) return;
        if (s == null) throw new ArgumentNullException(nameof(s));

        data.Clear();

        float cs = Mathf.Max(1f, s.chunkSize);
        Vector3 fieldSize = new Vector3(cs, cs, cs);
        Vector3 fieldCenter = chunkWorldOrigin + fieldSize * 0.5f;

        data.fieldCenter = fieldCenter;
        data.fieldSize = fieldSize;

        // Make the per-chunk seed stable
        int seed = HashSeed(globalSeed, coord);
        data.useFixedSeed = true;
        data.seed = seed;

        bool has = IsStationAnchor(s, globalSeed, coord);
        data.hasStation = has;
        data.orbitRadius = Mathf.Max(0.01f, s.orbitRadius);

        if (!has)
            return;

        // Deterministic local position + rotation derived from seed
        // (local space: within chunk)
        var rng = new System.Random(seed);

        float pad = Mathf.Clamp(s.borderPadding, 0f, cs * 0.49f);

        float x = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float y = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float z = Lerp((float)rng.NextDouble(), pad, cs - pad);

        data.localPosition = new Vector3(x, y, z);
        data.localRotation = RandomRotation(rng);
    }

    // --- helpers ---

    private static bool IsCandidate(Settings s, int globalSeed, Vector3Int coord)
    {
        // Candidate test uses a stable hash bucket
        int mod = Mathf.Max(1, s.candidateModulo);
        uint h = HashU32(globalSeed, coord);
        return (h % (uint)mod) == 0u;
    }

    private static float Score01(int globalSeed, Vector3Int coord)
    {
        // Stable 0..1 score from hash
        uint h = HashU32(globalSeed ^ unchecked((int)0x9E3779B9), coord);
        // Convert to [0,1)
        return (h & 0x00FFFFFF) / 16777216f;
    }

    private static bool LexicographicallySmaller(Vector3Int a, Vector3Int b)
    {
        if (a.x != b.x) return a.x < b.x;
        if (a.y != b.y) return a.y < b.y;
        return a.z < b.z;
    }

    private static uint HashU32(int seed, Vector3Int c)
    {
        unchecked
        {
            // A quick integer hash mix. Stable across platforms.
            uint h = (uint)seed;
            h ^= (uint)(c.x * 0x8DA6B343);
            h = (h ^ (h >> 13)) * 0xC2B2AE35;
            h ^= (uint)(c.y * 0xD8163841);
            h = (h ^ (h >> 13)) * 0x27D4EB2F;
            h ^= (uint)(c.z * 0x165667B1);
            h ^= h >> 16;
            return h;
        }
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

    private static float Lerp(float t, float a, float b) => a + (b - a) * Mathf.Clamp01(t);

    private static Quaternion RandomRotation(System.Random rng)
    {
        // Uniform random quaternion
        double u1 = rng.NextDouble();
        double u2 = rng.NextDouble();
        double u3 = rng.NextDouble();

        double sqrt1MinusU1 = Math.Sqrt(1.0 - u1);
        double sqrtU1 = Math.Sqrt(u1);

        float x = (float)(sqrt1MinusU1 * Math.Sin(2.0 * Math.PI * u2));
        float y = (float)(sqrt1MinusU1 * Math.Cos(2.0 * Math.PI * u2));
        float z = (float)(sqrtU1 * Math.Sin(2.0 * Math.PI * u3));
        float w = (float)(sqrtU1 * Math.Cos(2.0 * Math.PI * u3));

        return new Quaternion(x, y, z, w);
    }
    public static bool TryGetStationPose_NoAlloc(
    Settings s,
    Vector3 chunkWorldOrigin,
    Vector3Int coord,
    int globalSeed,
    out Vector3 worldPos,
    out Quaternion worldRot)
    {
        worldPos = default;
        worldRot = default;

        if (s == null) return false;

        // EXACT same rule as FillExistingChunk
        bool has = IsStationAnchor(s, globalSeed, coord);
        if (!has) return false;

        float cs = Mathf.Max(1f, s.chunkSize);

        int seed = HashSeed(globalSeed, coord);
        var rng = new System.Random(seed);

        float pad = Mathf.Clamp(s.borderPadding, 0f, cs * 0.49f);

        float x = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float y = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float z = Lerp((float)rng.NextDouble(), pad, cs - pad);

        Vector3 localPos = new Vector3(x, y, z);
        worldPos = chunkWorldOrigin + localPos;
        worldRot = RandomRotation(rng);

        return true;
    }
}
