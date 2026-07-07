using System;
using UnityEngine;

public static class POIRuntimeGenerator
{
    [Serializable]
    public class WeightedPOIType
    {
        public POIType type = POIType.Station;

        [Min(0)]
        public int weight = 1;
    }

    [Serializable]
    public class Settings
    {
        [Header("Chunk")]
        public float chunkSize = 1000f;

        [Header("POI Density")]
        [Tooltip("1 in N chunks becomes a POI candidate before neighbor exclusion.")]
        [Min(1)] public int candidateModulo = 8;

        [Header("POI Type Weights")]
        public WeightedPOIType[] poiTypeWeights =
        {
            new WeightedPOIType { type = POIType.Station, weight = 50 },
            new WeightedPOIType { type = POIType.Anomaly, weight = 20 },
            new WeightedPOIType { type = POIType.AbandonedOutpost, weight = 15 },
            new WeightedPOIType { type = POIType.Wreckage, weight = 15 }
        };

        [Header("Neighbor Exclusion")]
        [Tooltip("Chunk radius to exclude other POIs. 1 blocks all neighboring chunks.")]
        [Range(0, 4)] public int excludeNeighborRadius = 1;

        [Header("Placement inside chunk")]
        [Tooltip("How far from chunk borders we keep the POI local position.")]
        [Min(0f)] public float borderPadding = 150f;

        [Header("Station Settings")]
        public float stationOrbitRadius = 280f;
        public float stationPreGravityRadius = 400f;

        [Header("Scale")]
        public Vector2 uniformScaleRange = new Vector2(1f, 1f);
    }

    public static POIFieldData GenerateChunk(Settings s, Vector3 chunkWorldOrigin, Vector3Int coord, int globalSeed)
    {
        POIFieldData data = ScriptableObject.CreateInstance<POIFieldData>();
        FillExistingChunk(data, s, chunkWorldOrigin, coord, globalSeed);
        return data;
    }

    public static void FillExistingChunk(POIFieldData data, Settings s, Vector3 chunkWorldOrigin, Vector3Int coord, int globalSeed)
    {
        if (data == null) return;
        if (s == null) throw new ArgumentNullException(nameof(s));

        data.Clear();

        float cs = Mathf.Max(1f, s.chunkSize);
        Vector3 fieldSize = new Vector3(cs, cs, cs);

        data.fieldSize = fieldSize;
        data.fieldCenter = chunkWorldOrigin + fieldSize * 0.5f;
        data.useFixedSeed = true;
        data.seed = HashSeed(globalSeed, coord);

        if (!IsPOIAnchor(s, globalSeed, coord))
            return;

        System.Random rng = new System.Random(data.seed);

        data.poiType = PickPOIType(s, rng);

        float pad = Mathf.Clamp(s.borderPadding, 0f, cs * 0.49f);

        float x = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float y = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float z = Lerp((float)rng.NextDouble(), pad, cs - pad);

        data.localPosition = new Vector3(x, y, z);
        data.localRotation = RandomRotation(rng);

        float minScale = Mathf.Max(0.0001f, Mathf.Min(s.uniformScaleRange.x, s.uniformScaleRange.y));
        float maxScale = Mathf.Max(minScale, Mathf.Max(s.uniformScaleRange.x, s.uniformScaleRange.y));
        data.uniformScale = Lerp((float)rng.NextDouble(), minScale, maxScale);

        if (data.poiType == POIType.Station)
        {
            data.orbitRadius = Mathf.Max(0.01f, s.stationOrbitRadius);
            data.preGravityRadius = Mathf.Max(data.orbitRadius, s.stationPreGravityRadius);
        }
    }

    public static bool TryGetPOIPoseNoAlloc(
        Settings s,
        Vector3 chunkWorldOrigin,
        Vector3Int coord,
        int globalSeed,
        out POIType poiType,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        poiType = POIType.None;
        worldPos = default;
        worldRot = default;

        if (s == null) return false;
        if (!IsPOIAnchor(s, globalSeed, coord)) return false;

        float cs = Mathf.Max(1f, s.chunkSize);
        int seed = HashSeed(globalSeed, coord);
        System.Random rng = new System.Random(seed);

        poiType = PickPOIType(s, rng);

        float pad = Mathf.Clamp(s.borderPadding, 0f, cs * 0.49f);

        float x = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float y = Lerp((float)rng.NextDouble(), pad, cs - pad);
        float z = Lerp((float)rng.NextDouble(), pad, cs - pad);

        worldPos = chunkWorldOrigin + new Vector3(x, y, z);
        worldRot = RandomRotation(rng);

        return poiType != POIType.None;
    }

    public static bool TryGetStationPoseNoAlloc(
        Settings s,
        Vector3 chunkWorldOrigin,
        Vector3Int coord,
        int globalSeed,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        bool hasPOI = TryGetPOIPoseNoAlloc(
            s,
            chunkWorldOrigin,
            coord,
            globalSeed,
            out POIType type,
            out worldPos,
            out worldRot
        );

        return hasPOI && type == POIType.Station;
    }

    public static bool IsPOIAnchor(Settings s, int globalSeed, Vector3Int coord)
    {
        if (s == null) return false;

        if (!IsCandidate(s, globalSeed, coord))
            return false;

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

                    if (nbScore > myScore)
                        return false;

                    if (Mathf.Approximately(nbScore, myScore) && LexicographicallySmaller(nb, coord))
                        return false;
                }

        return true;
    }

    private static POIType PickPOIType(Settings s, System.Random rng)
    {
        if (s.poiTypeWeights == null || s.poiTypeWeights.Length == 0)
            return POIType.Station;

        int totalWeight = 0;

        for (int i = 0; i < s.poiTypeWeights.Length; i++)
        {
            if (s.poiTypeWeights[i] == null) continue;
            if (s.poiTypeWeights[i].type == POIType.None) continue;

            totalWeight += Mathf.Max(0, s.poiTypeWeights[i].weight);
        }

        if (totalWeight <= 0)
            return POIType.None;

        int roll = rng.Next(0, totalWeight);
        int cursor = 0;

        for (int i = 0; i < s.poiTypeWeights.Length; i++)
        {
            WeightedPOIType entry = s.poiTypeWeights[i];
            if (entry == null) continue;
            if (entry.type == POIType.None) continue;

            cursor += Mathf.Max(0, entry.weight);

            if (roll < cursor)
                return entry.type;
        }

        return POIType.None;
    }

    private static bool IsCandidate(Settings s, int globalSeed, Vector3Int coord)
    {
        int mod = Mathf.Max(1, s.candidateModulo);
        uint h = HashU32(globalSeed, coord);
        return (h % (uint)mod) == 0u;
    }

    private static float Score01(int globalSeed, Vector3Int coord)
    {
        uint h = HashU32(globalSeed ^ unchecked((int)0x9E3779B9), coord);
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

    private static float Lerp(float t, float a, float b)
    {
        return a + (b - a) * Mathf.Clamp01(t);
    }

    private static Quaternion RandomRotation(System.Random rng)
    {
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
}