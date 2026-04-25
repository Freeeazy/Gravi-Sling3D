using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCQuestManager : MonoBehaviour
{
    [Header("Refs")]
    public StationPosManager posManager;

    [Tooltip("Optional: used for consistent selection across stations.")]
    public int globalSeed = 12345;

    [Header("Offer Constraints")]
    public float pickRadius = 20000f;
    public float minTargetDistance = 1500f;
    public int pickAttempts = 10;

    [Header("Far Search (Option C)")]
    public int maxRingRadiusChunks = 40;   // 40 chunks * 1000 = 40k range potential
    public int samplesPerRing = 12;        // how many coords to test per ring

    [Header("Accepted Quests")]
    public int maxAcceptedQuests = 5;

    [Serializable]
    public struct QuestOffer
    {
        public int npcId;
        public Vector3Int fromCoord;
        public Vector3 fromWorldPos;

        public Vector3Int toCoord;
        public Vector3 toWorldPos;

        public float distance; // from station -> target (world distance)
        public int difficulty; // 1..5
        public bool valid;
    }

    [Serializable]
    public struct ActiveQuest
    {
        public int questId;        // can just be npcId for now
        public int npcId;
        public Vector3Int toCoord;
        public Vector3 toWorldPos;
        public float distanceAtAccept;
    }

    public bool HasClosestQuest { get; private set; }
    public ActiveQuest ClosestQuest { get; private set; }
    public Vector3Int ClosestQuestCoord => ClosestQuest.toCoord;

    // Offered quest per NPC at the *current station context*
    private readonly Dictionary<int, QuestOffer> _offersByNpc = new();

    // Accepted quests
    private readonly List<ActiveQuest> _active = new();

    // Cached station list (active stations)
    private readonly List<StationPosManager.StationWorldInfo> _tmpStations = new(256);

    // Current station context
    private Vector3Int _currentStationCoord;
    private Vector3 _currentStationWorldPos;
    private bool _hasStationContext;

    public IReadOnlyList<ActiveQuest> ActiveQuests => _active;

    /// Call this when opening quest board / arriving at a station.
    public void RefreshOffersForStation(Vector3 stationWorldPos)
    {
        if (!posManager) return;

        _currentStationWorldPos = stationWorldPos;
        _currentStationCoord = posManager.WorldToChunkCoord(stationWorldPos);
        _hasStationContext = true;

        // Just clear offers; they'll be regenerated lazily when asked
        _offersByNpc.Clear();
    }

    public bool TryGetOffer(int npcId, out QuestOffer offer)
    {
        offer = default;

        if (!_hasStationContext)
        {
            Debug.LogWarning($"[NPCQuestManager] TryGetOffer FAILED: no station context. Did you call RefreshOffersForStation() ?");
            return false;
        }

        if (!posManager)
        {
            Debug.LogWarning($"[NPCQuestManager] TryGetOffer FAILED: posManager is NULL.");
            return false;
        }

        if (_offersByNpc.TryGetValue(npcId, out offer) && offer.valid)
            return true;

        offer = GenerateOfferForNpc(npcId);
        _offersByNpc[npcId] = offer;

        if (!offer.valid)
            Debug.LogWarning($"[NPCQuestManager] GenerateOfferForNpc returned INVALID for npcId={npcId} (from={_currentStationCoord}).");

        return offer.valid;
    }

    public bool AcceptQuest(int npcId)
    {
        if (_active.Count >= maxAcceptedQuests)
            return false;

        if (!TryGetOffer(npcId, out var offer))
            return false;

        // prevent duplicates to same station for now (optional)
        //for (int i = 0; i < _active.Count; i++)
        //    if (_active[i].toCoord == offer.toCoord)
        //        return false;

        // 1 quest per NPC gate
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].npcId == npcId)
            {
                Debug.LogWarning($"[NPCQuestManager] Duplicate quest from same NPC blocked. npcId={npcId}");
                return false;
            }
        }

        var q = new ActiveQuest
        {
            questId = npcId, // simple for now
            npcId = npcId,
            toCoord = offer.toCoord,
            toWorldPos = offer.toWorldPos,
            distanceAtAccept = offer.distance
        };

        _active.Add(q);
        RefreshClosestQuest();
        return true;
    }

    public int RemoveAllQuestsByCoord(Vector3Int coord)
    {
        int removed = 0;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].toCoord == coord)
            {
                _active.RemoveAt(i);
                removed++;
            }
        }

        if (removed > 0)
            RefreshClosestQuest();

        return removed;
    }

    public void NotifyArrivedAt(Vector3Int coord)
    {
        // Complete ALL quests that target this coord.
        int removed = RemoveAllQuestsByCoord(coord);

        // Optional debug
        if (removed > 0) Debug.Log($"Completed {removed} quest(s) at {coord}");
    }

    private QuestOffer GenerateOfferForNpc(int npcId)
    {
        var offer = new QuestOffer
        {
            npcId = npcId,
            fromCoord = _currentStationCoord,
            fromWorldPos = _currentStationWorldPos,
            valid = false,
            difficulty = 1
        };

        if (!_hasStationContext || !posManager) return offer;

        // Deterministic per NPC + station
        int seed = unchecked(globalSeed * 73856093 ^ npcId * 19349663 ^ _currentStationCoord.GetHashCode());
        var rng = new System.Random(seed);

        // Pick difficulty (random for now, deterministic due to seed)
        int difficulty = PickDifficulty1to5(rng);
        offer.difficulty = difficulty;

        // Convert difficulty into a distance band inside [minTargetDistance .. pickRadius]
        float tMin01, tMax01;
        GetDifficultyBand01(difficulty, out tMin01, out tMax01);

        float minD = minTargetDistance;
        float maxD = pickRadius;

        // Band distances in world units
        float bandMin = Mathf.Lerp(minD, maxD, tMin01);
        float bandMax = Mathf.Lerp(minD, maxD, tMax01);

        float bandMin2 = bandMin * bandMin;
        float bandMax2 = bandMax * bandMax;

        // Helper local function: test a coord against the BAND (not global min/max)
        bool TryCoord(Vector3Int c, out Vector3 wpos)
        {
            wpos = default;

            if (c == _currentStationCoord) return false;

            if (!posManager.TryGetStationWorldPose(c, out var pos, out _))
                return false;

            float d2 = (pos - _currentStationWorldPos).sqrMagnitude;

            if (d2 < bandMin2) return false;
            if (d2 > bandMax2) return false;

            wpos = pos;
            return true;
        }

        // Convert band to ring range so we don't always start at ring 1 (near bias)
        float chunk = Mathf.Max(1f, posManager.chunkSize);
        int ringMin = Mathf.Max(1, Mathf.FloorToInt(bandMin / chunk));
        int ringMax = Mathf.Max(ringMin, Mathf.CeilToInt(bandMax / chunk));

        // Respect your global cap too
        int rMax = Mathf.Max(1, maxRingRadiusChunks);
        int maxRadiusByPick = Mathf.CeilToInt(pickRadius / chunk);
        rMax = Mathf.Min(rMax, Mathf.Max(1, maxRadiusByPick));

        ringMax = Mathf.Min(ringMax, rMax);

        Vector3Int chosenCoord = default;
        Vector3 chosenPos = default;
        bool found = false;

        // Search only within ringMin..ringMax to enforce the band + reduce near bias
        for (int ring = ringMin; ring <= ringMax && !found; ring++)
        {
            int tries = Mathf.Max(1, samplesPerRing);

            for (int t = 0; t < tries; t++)
            {
                int face = rng.Next(0, 6);

                int x = rng.Next(-ring, ring + 1);
                int y = rng.Next(-ring, ring + 1);
                int z = rng.Next(-ring, ring + 1);

                switch (face)
                {
                    case 0: x = ring; break;
                    case 1: x = -ring; break;
                    case 2: y = ring; break;
                    case 3: y = -ring; break;
                    case 4: z = ring; break;
                    case 5: z = -ring; break;
                }

                Vector3Int c = new Vector3Int(
                    _currentStationCoord.x + x,
                    _currentStationCoord.y + y,
                    _currentStationCoord.z + z
                );

                if (TryCoord(c, out var wpos))
                {
                    chosenCoord = c;
                    chosenPos = wpos;
                    found = true;
                    break;
                }
            }
        }

        // Fallback: if band failed, relax back toward your old logic (still bounded by pickRadius)
        if (!found)
        {
            float relaxedMin2 = (minTargetDistance * 0.25f) * (minTargetDistance * 0.25f);
            float max2 = pickRadius * pickRadius;

            for (int ring = 1; ring <= rMax && !found; ring++)
            {
                for (int t = 0; t < Mathf.Max(1, samplesPerRing); t++)
                {
                    int face = rng.Next(0, 6);

                    int x = rng.Next(-ring, ring + 1);
                    int y = rng.Next(-ring, ring + 1);
                    int z = rng.Next(-ring, ring + 1);

                    switch (face)
                    {
                        case 0: x = ring; break;
                        case 1: x = -ring; break;
                        case 2: y = ring; break;
                        case 3: y = -ring; break;
                        case 4: z = ring; break;
                        case 5: z = -ring; break;
                    }

                    Vector3Int c = new Vector3Int(
                        _currentStationCoord.x + x,
                        _currentStationCoord.y + y,
                        _currentStationCoord.z + z
                    );

                    if (c == _currentStationCoord) continue;

                    if (!posManager.TryGetStationWorldPose(c, out var pos, out _))
                        continue;

                    float d2 = (pos - _currentStationWorldPos).sqrMagnitude;
                    if (d2 < relaxedMin2) continue;
                    if (d2 > max2) continue;

                    chosenCoord = c;
                    chosenPos = pos;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
            return offer;

        offer.toCoord = chosenCoord;
        offer.toWorldPos = chosenPos;
        offer.distance = Vector3.Distance(_currentStationWorldPos, chosenPos);
        offer.valid = true;
        return offer;
    }
    public bool HasActiveQuestFromNpc(int npcId)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].npcId == npcId)
                return true;
        }

        return false;
    }
    private static void GetDifficultyBand01(int difficulty, out float tMin, out float tMax)
    {
        // difficulty 1 => [0.0, 0.2], 2 => [0.2, 0.4], ... 5 => [0.8, 1.0]
        difficulty = Mathf.Clamp(difficulty, 1, 5);
        tMin = (difficulty - 1) * 0.2f;
        tMax = difficulty * 0.2f;
        if (difficulty == 5) tMax = 1f; // ensure exact 1.0
    }

    private static int PickDifficulty1to5(System.Random rng)
    {
        // Random for now. Later you can weight this.
        return 1 + rng.Next(0, 5);
    }

    private void RefreshClosestQuest()
    {
        HasClosestQuest = false;
        ClosestQuest = default;

        if (_active.Count == 0)
            return;

        int bestIndex = 0;
        float bestDistance = _active[0].distanceAtAccept;

        for (int i = 1; i < _active.Count; i++)
        {
            if (_active[i].distanceAtAccept < bestDistance)
            {
                bestDistance = _active[i].distanceAtAccept;
                bestIndex = i;
            }
        }

        ClosestQuest = _active[bestIndex];
        HasClosestQuest = true;
    }
}