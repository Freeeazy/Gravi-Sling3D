using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCQuestManager : MonoBehaviour
{
    [Header("Refs")]
    public StationPosManager posManager;
    public ModuleInventoryManager inventoryManager;

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

    [Header("Credit Rewards")]
    public float baseCreditReward = 100f;
    public float rewardPerDifficulty = 50f;

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
        public int rewardModuleIndex;
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
        public int difficulty;
        public int rewardModuleIndex;
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
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            rewardModuleIndex = offer.rewardModuleIndex
        };

        _active.Add(q);
        if (PackageDurabilityManager.Instance != null)
            PackageDurabilityManager.Instance.RegisterQuest(q);
        RefreshClosestQuest();
        return true;
    }

    private int CompleteQuestsByCoord(Vector3Int coord, out int completed)
    {
        completed = 0;

        // -1 means no delivery completed.
        int returnedQualityIndex = -1;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].toCoord == coord)
            {
                int qualityIndex = GiveQuestReward(_active[i]);

                // If multiple quests complete at the same station,
                // return the worst quality so the station reaction matches the harshest delivery.
                if (returnedQualityIndex == -1 || qualityIndex < returnedQualityIndex)
                    returnedQualityIndex = qualityIndex;

                if (PackageDurabilityManager.Instance != null)
                    PackageDurabilityManager.Instance.RemoveQuest(_active[i].questId);

                _active.RemoveAt(i);
                completed++;
            }
        }

        if (completed > 0)
            RefreshClosestQuest();

        return returnedQualityIndex;
    }

    public int NotifyArrivedAt(Vector3Int coord)
    {
        int completed;
        int qualityIndex = CompleteQuestsByCoord(coord, out completed);

        if (completed > 0)
            Debug.Log($"Completed {completed} quest(s) at {coord}. QualityIndex={qualityIndex}");

        return qualityIndex;
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

        int rewardIndex = PickRewardModuleIndex(rng);
        offer.rewardModuleIndex = rewardIndex;

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
    private int PickRewardModuleIndex(System.Random rng)
    {
        ModuleInventoryManager inv = inventoryManager ? inventoryManager : ModuleInventoryManager.Instance;

        if (inv == null || inv.modulePrefabs == null || inv.modulePrefabs.Count == 0)
            return -1;

        return rng.Next(0, inv.modulePrefabs.Count);
    }
    private int GiveQuestReward(ActiveQuest quest)
    {
        ModuleInventoryManager inv = inventoryManager ? inventoryManager : ModuleInventoryManager.Instance;

        if (inv == null)
        {
            Debug.LogWarning("[NPCQuestManager] Cannot give quest reward. No inventory manager found.");
            return 0;
        }

        float integrity = 100f;

        if (PackageDurabilityManager.Instance != null)
        {
            PackageDurabilityManager.Instance.TryGetPackageIntegrity(quest.questId, out integrity);
        }

        PackageDurabilityManager.DeliveryQuality quality =
            PackageDurabilityManager.Instance != null
                ? PackageDurabilityManager.Instance.GetDeliveryQuality(integrity)
                : PackageDurabilityManager.DeliveryQuality.Good;

        float multiplier =
            PackageDurabilityManager.Instance != null
                ? PackageDurabilityManager.Instance.GetRewardMultiplier(quality)
                : 1f;

        float rawCredits = baseCreditReward + (quest.difficulty * rewardPerDifficulty);
        float finalCredits = rawCredits * multiplier;

        if (finalCredits > 0f)
            inv.GiveXCredits(finalCredits);

        float moduleRewardChance = GetModuleRewardChance(quality);
        bool shouldGiveModule = UnityEngine.Random.value <= moduleRewardChance;

        bool gaveModule = false;

        if (shouldGiveModule)
            gaveModule = inv.TryGiveModuleByIndex(quest.rewardModuleIndex, 1);

        if (RewardPopupUI.Instance != null)
        {
            RewardPopupUI.Instance.ShowDeliveryReward(
                FormatDeliveryQuality(quality),
                finalCredits,
                gaveModule,
                gaveModule ? inv.modulePrefabs[quest.rewardModuleIndex].displayName : ""
            );
        }

        Debug.Log(
            $"[NPCQuestManager] Delivery complete. " +
            $"Quality={quality}, Integrity={integrity:0}%, " +
            $"Credits={finalCredits:0}, ModuleGiven={gaveModule}"
        );

        return GetDeliveryQualityIndex(quality);
    }
    private float GetModuleRewardChance(PackageDurabilityManager.DeliveryQuality quality)
    {
        switch (quality)
        {
            case PackageDurabilityManager.DeliveryQuality.Perfect:
                return 0.50f; // 50% chance

            case PackageDurabilityManager.DeliveryQuality.Good:
                return 0.25f; // 25% chance

            default:
                return 0f;
        }
    }
    private int GetDeliveryQualityIndex(PackageDurabilityManager.DeliveryQuality quality)
    {
        switch (quality)
        {
            case PackageDurabilityManager.DeliveryQuality.Failed:
                return 0;

            case PackageDurabilityManager.DeliveryQuality.BarelyDelivered:
                return 1;

            case PackageDurabilityManager.DeliveryQuality.Damaged:
                return 2;

            case PackageDurabilityManager.DeliveryQuality.Good:
                return 3;

            case PackageDurabilityManager.DeliveryQuality.Perfect:
                return 4;

            default:
                return -1;
        }
    }
    private string FormatDeliveryQuality(PackageDurabilityManager.DeliveryQuality quality)
    {
        switch (quality)
        {
            case PackageDurabilityManager.DeliveryQuality.Perfect:
                return "<color=#B84DFF>Perfect Delivery</color>";

            case PackageDurabilityManager.DeliveryQuality.Good:
                return "<color=#4DFF88>Good Delivery</color>";

            case PackageDurabilityManager.DeliveryQuality.Damaged:
                return "<color=#FFD84D>Damaged Delivery</color>";

            case PackageDurabilityManager.DeliveryQuality.BarelyDelivered:
                return "<color=#FF8A3D>Barely Delivered</color>";

            case PackageDurabilityManager.DeliveryQuality.Failed:
                return "<color=#FF3D3D>Delivery Failed</color>";

            default:
                return "<color=#FFFFFF>Delivery Complete</color>";
        }
    }
}