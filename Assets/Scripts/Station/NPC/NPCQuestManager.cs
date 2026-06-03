using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCQuestManager : MonoBehaviour
{
    private const int DefaultMaxQuestDifficulty = 7;
    private const int MinQuestDifficulty = 1;
    private int maxDifficulty = DefaultMaxQuestDifficulty;

    [Header("Refs")]
    public StationPosManager posManager;
    public ModuleInventoryManager inventoryManager;
    public PackageDurabilityManager packageDurabilityManager;

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

    [Tooltip("Credits gained per world unit traveled. 0.10 = 100 credits per 1000 units.")]
    public float distanceCreditRate = 0.10f;

    [Header("Delivery Type Modifiers")]
    [Tooltip("Chance weight for urgent delivery offers. Default weights are 25 urgent / 50 standard / 25 relaxed.")]
    public int urgentDeliveryWeight = 25;

    [Tooltip("Chance weight for standard delivery offers. Default weights are 25 urgent / 50 standard / 25 relaxed.")]
    public int standardDeliveryWeight = 50;

    [Tooltip("Chance weight for relaxed delivery offers. Default weights are 25 urgent / 50 standard / 25 relaxed.")]
    public int relaxedDeliveryWeight = 25;

    public Vector2 urgentTimeMultiplierRange = new Vector2(0.85f, 0.70f);
    public Vector2 urgentRewardMultiplierRange = new Vector2(1.25f, 1.50f);
    public Vector2 relaxedTimeMultiplierRange = new Vector2(1.50f, 2.00f);
    public Vector2 relaxedRewardMultiplierRange = new Vector2(0.90f, 0.75f);

    [Header("Reputation XP Quality Scaling")]
    public float perfectDeliveryReputationMultiplier = 1.25f;
    public float goodDeliveryReputationMultiplier = 1f;
    public float damagedDeliveryReputationMultiplier = 0.45f;
    public float barelyDeliveredReputationMultiplier = 0.15f;
    public float failedDeliveryReputationMultiplier = -0.50f;

    [Header("Rank Quest Offers")]
    public RankQuestConfig[] rankQuestConfigs =
    {
        new RankQuestConfig
        {
            rankName = "Rookie",
            minDifficulty = 1,
            maxDifficulty = 2,
            creditMultiplier = 1f,
            creditBonus = 0f,
            reputationMultiplier = 1f,
            reputationBonus = 0,
            difficultyWeights = new[]
            {
                new WeightedDifficulty { difficulty = 1, weight = 80 },
                new WeightedDifficulty { difficulty = 2, weight = 20 }
            }
        },
        new RankQuestConfig
        {
            rankName = "Runner",
            minDifficulty = 1,
            maxDifficulty = 4,
            creditMultiplier = 1.1f,
            creditBonus = 50f,
            reputationMultiplier = 1.1f,
            reputationBonus = 25,
            difficultyWeights = new[]
            {
                new WeightedDifficulty { difficulty = 1, weight = 25 },
                new WeightedDifficulty { difficulty = 2, weight = 45 },
                new WeightedDifficulty { difficulty = 3, weight = 25 },
                new WeightedDifficulty { difficulty = 4, weight = 5 }
            }
        },
        new RankQuestConfig
        {
            rankName = "Trusted",
            minDifficulty = 2,
            maxDifficulty = 5,
            creditMultiplier = 1.25f,
            creditBonus = 150f,
            reputationMultiplier = 1.25f,
            reputationBonus = 75,
            difficultyWeights = new[]
            {
                new WeightedDifficulty { difficulty = 2, weight = 25 },
                new WeightedDifficulty { difficulty = 3, weight = 40 },
                new WeightedDifficulty { difficulty = 4, weight = 25 },
                new WeightedDifficulty { difficulty = 5, weight = 10 }
            }
        },
        new RankQuestConfig
        {
            rankName = "Made Courier",
            minDifficulty = 3,
            maxDifficulty = 7,
            creditMultiplier = 1.45f,
            creditBonus = 350f,
            reputationMultiplier = 1.45f,
            reputationBonus = 150,
            difficultyWeights = new[]
            {
                new WeightedDifficulty { difficulty = 3, weight = 20 },
                new WeightedDifficulty { difficulty = 4, weight = 30 },
                new WeightedDifficulty { difficulty = 5, weight = 25 },
                new WeightedDifficulty { difficulty = 6, weight = 15 },
                new WeightedDifficulty { difficulty = 7, weight = 10 }
            }
        },
        new RankQuestConfig
        {
            rankName = "Family Legend",
            minDifficulty = 4,
            maxDifficulty = 7,
            creditMultiplier = 1.75f,
            creditBonus = 750f,
            reputationMultiplier = 1.75f,
            reputationBonus = 300,
            difficultyWeights = new[]
            {
                new WeightedDifficulty { difficulty = 4, weight = 20 },
                new WeightedDifficulty { difficulty = 5, weight = 30 },
                new WeightedDifficulty { difficulty = 6, weight = 25 },
                new WeightedDifficulty { difficulty = 7, weight = 25 }
            }
        }
    };

    [Header("Difficulty Rewards")]
    public DifficultyRewardConfig[] difficultyRewardConfigs =
    {
        new DifficultyRewardConfig { difficulty = 1, baseCredits = 150f, distanceCreditRate = 0.08f, baseReputationExp = 180 },
        new DifficultyRewardConfig { difficulty = 2, baseCredits = 250f, distanceCreditRate = 0.10f, baseReputationExp = 300 },
        new DifficultyRewardConfig { difficulty = 3, baseCredits = 400f, distanceCreditRate = 0.12f, baseReputationExp = 475 },
        new DifficultyRewardConfig { difficulty = 4, baseCredits = 600f, distanceCreditRate = 0.15f, baseReputationExp = 700 },
        new DifficultyRewardConfig { difficulty = 5, baseCredits = 850f, distanceCreditRate = 0.18f, baseReputationExp = 975 },
        new DifficultyRewardConfig { difficulty = 6, baseCredits = 1200f, distanceCreditRate = 0.22f, baseReputationExp = 1350 },
        new DifficultyRewardConfig { difficulty = 7, baseCredits = 1700f, distanceCreditRate = 0.28f, baseReputationExp = 1850 }
    };

    [Serializable]
    public struct WeightedDifficulty
    {
        [Min(1)]
        public int difficulty;

        [Min(0)]
        public int weight;
    }

    [Serializable]
    public struct RankQuestConfig
    {
        public string rankName;

        [Min(1)]
        public int minDifficulty;

        [Min(1)]
        public int maxDifficulty;

        [Tooltip("Weighted difficulties offered at this rank. If empty, the min/max range is used evenly.")]
        public WeightedDifficulty[] difficultyWeights;

        [Tooltip("Multiplier applied after the difficulty and distance credit reward are calculated.")]
        public float creditMultiplier;

        [Tooltip("Flat credit bonus added for every quest offered at this rank.")]
        public float creditBonus;

        [Tooltip("Multiplier applied after the difficulty reputation XP reward is calculated.")]
        public float reputationMultiplier;

        [Tooltip("Flat reputation XP bonus added for every quest offered at this rank.")]
        public int reputationBonus;
    }

    [Serializable]
    public struct DifficultyRewardConfig
    {
        [Min(1)]
        public int difficulty;

        public float baseCredits;

        public int baseReputationExp;

        [Tooltip("Credits gained per world unit traveled for this difficulty.")]
        public float distanceCreditRate;
    }
    public enum DeliveryType
    {
        Standard,
        Urgent,
        Relaxed
    }

    [Serializable]
    public struct QuestOffer
    {
        public int npcId;
        public Vector3Int fromCoord;
        public Vector3 fromWorldPos;

        public Vector3Int toCoord;
        public Vector3 toWorldPos;

        public float distance; // from station -> target (world distance)
        public int difficulty;
        public int reputationRankIndex;
        public DeliveryType deliveryType;
        public float deliveryTimeMultiplier;
        public float deliveryRewardMultiplier;
        public float expectedDeliveryTimeSeconds;
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
        public int reputationRankIndex;
        public DeliveryType deliveryType;
        public float deliveryTimeMultiplier;
        public float deliveryRewardMultiplier;
        public float expectedDeliveryTimeSeconds;
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
    private int _currentOfferRankIndex;
    private bool _hasStationContext;

    public IReadOnlyList<ActiveQuest> ActiveQuests => _active;

    /// Call this when opening quest board / arriving at a station.
    public void RefreshOffersForStation(Vector3 stationWorldPos)
    {
        if (!posManager) return;

        _currentStationWorldPos = stationWorldPos;
        _currentStationCoord = posManager.WorldToChunkCoord(stationWorldPos);
        _currentOfferRankIndex = GetCurrentReputationRankIndex();
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
    public float GetPreviewCreditReward(QuestOffer offer)
    {
        if (!offer.valid)
            return 0f;

        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = offer.deliveryRewardMultiplier,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
        };

        return Mathf.Round(CalculateQuestCredits(previewQuest));
    }
    public float GetPreviewBaseCreditReward(QuestOffer offer)
    {
        if (!offer.valid)
            return 0f;

        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = 1f,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
        };

        return Mathf.Round(CalculateQuestCredits(previewQuest));
    }
    public int GetPreviewReputationExpReward(QuestOffer offer)
    {
        if (!offer.valid)
            return 0;

        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = offer.deliveryRewardMultiplier,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
        };

        return CalculateQuestReputationExp(previewQuest, PackageDurabilityManager.DeliveryQuality.Good);
    }

    public int GetPreviewBaseReputationExpReward(QuestOffer offer)
    {
        if (!offer.valid)
            return 0;

        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = 1f,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
        };

        return CalculateQuestReputationExp(previewQuest, PackageDurabilityManager.DeliveryQuality.Good);
    }
    public float GetPreviewDeliveryTimeSeconds(QuestOffer offer)
    {
        if (!offer.valid)
            return 0f;

        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = offer.distance,
            difficulty = offer.difficulty,
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = offer.deliveryRewardMultiplier,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
        };

        if (offer.expectedDeliveryTimeSeconds > 0f)
            return offer.expectedDeliveryTimeSeconds;

        return CalculateExpectedDeliveryTimeSeconds(previewQuest);
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
            reputationRankIndex = offer.reputationRankIndex,
            deliveryType = offer.deliveryType,
            deliveryTimeMultiplier = offer.deliveryTimeMultiplier,
            deliveryRewardMultiplier = offer.deliveryRewardMultiplier,
            expectedDeliveryTimeSeconds = offer.expectedDeliveryTimeSeconds
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
    public bool FailQuest(int questId)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].questId == questId)
            {
                ActiveQuest failedQuest = _active[i];

                _active.RemoveAt(i);
                RefreshClosestQuest();

                HandleFailedQuestPenalty(failedQuest);

                Debug.Log($"[NPCQuestManager] Quest failed/expired. questId={questId}");

                return true;
            }
        }

        Debug.LogWarning($"[NPCQuestManager] Tried to fail quest, but no active quest matched questId={questId}");
        return false;
    }
    private void HandleFailedQuestPenalty(ActiveQuest quest)
    {
        int reputationExpReward = CalculateQuestReputationExp(quest, PackageDurabilityManager.DeliveryQuality.Failed);

        if (RewardPopupUI.Instance != null)
        {
            RewardPopupUI.Instance.ShowDeliveryReward(
                "<color=#FF3D3D>Delivery Failed</color>",
                0f,
                null,
                reputationExpReward
            );
        }

        if (FamilyReputationManager.Instance != null)
        {
            FamilyReputationManager.Instance.AddReputationExp(
                reputationExpReward
            );
        }
    }

    private QuestOffer GenerateOfferForNpc(int npcId)
    {
        var offer = new QuestOffer
        {
            npcId = npcId,
            fromCoord = _currentStationCoord,
            fromWorldPos = _currentStationWorldPos,
            valid = false,
            difficulty = 1,
            reputationRankIndex = _currentOfferRankIndex,
            deliveryType = DeliveryType.Standard,
            deliveryTimeMultiplier = 1f,
            deliveryRewardMultiplier = 1f,
            expectedDeliveryTimeSeconds = 0f
        };

        if (!_hasStationContext || !posManager) return offer;

        // Deterministic per NPC + station
        int seed = unchecked(globalSeed * 73856093 ^ npcId * 19349663 ^ _currentStationCoord.GetHashCode());
        var rng = new System.Random(seed);

        // Pick difficulty (random for now, deterministic due to seed)
        RankQuestConfig rankConfig = GetRankQuestConfig(_currentOfferRankIndex);
        int difficulty = PickDifficulty(rng, rankConfig);
        offer.difficulty = difficulty;
        ApplyDeliveryTypeModifiers(ref offer, rng);

        // Convert difficulty into a distance band inside [minTargetDistance .. pickRadius]
        float tMin01, tMax01;
        GetDifficultyBand01(difficulty, GetHighestConfiguredDifficulty(), out tMin01, out tMax01);

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
        offer.expectedDeliveryTimeSeconds = CalculateExpectedDeliveryTimeSeconds(offer.distance, offer.deliveryTimeMultiplier);
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
    private static void GetDifficultyBand01(int difficulty, int maxDifficulty, out float tMin, out float tMax)
    {
        maxDifficulty = Mathf.Max(MinQuestDifficulty, maxDifficulty);
        difficulty = Mathf.Clamp(difficulty, MinQuestDifficulty, maxDifficulty);

        float bandSize = 1f / maxDifficulty;
        tMin = (difficulty - MinQuestDifficulty) * bandSize;
        tMax = difficulty == maxDifficulty ? 1f : difficulty * bandSize;
    }

    private int GetHighestConfiguredDifficulty()
    {
        int highestDifficulty = Mathf.Max(MinQuestDifficulty, maxDifficulty);

        if (difficultyRewardConfigs != null)
        {
            for (int i = 0; i < difficultyRewardConfigs.Length; i++)
                highestDifficulty = Mathf.Max(highestDifficulty, difficultyRewardConfigs[i].difficulty);
        }

        if (rankQuestConfigs != null)
        {
            for (int i = 0; i < rankQuestConfigs.Length; i++)
                highestDifficulty = Mathf.Max(highestDifficulty, rankQuestConfigs[i].maxDifficulty);
        }

        return highestDifficulty;
    }
    private RankQuestConfig GetRankQuestConfig(int rankIndex)
    {
        RankQuestConfig config = default;

        if (rankQuestConfigs != null && rankQuestConfigs.Length > 0)
        {
            int configIndex = Mathf.Clamp(rankIndex, 0, rankQuestConfigs.Length - 1);
            config = rankQuestConfigs[configIndex];
        }

        if (config.minDifficulty <= 0)
            config.minDifficulty = MinQuestDifficulty;

        if (config.maxDifficulty <= 0)
            config.maxDifficulty = Mathf.Max(config.minDifficulty, maxDifficulty);

        config.minDifficulty = Mathf.Max(MinQuestDifficulty, config.minDifficulty);
        config.maxDifficulty = Mathf.Max(config.minDifficulty, config.maxDifficulty);

        if (config.creditMultiplier <= 0f)
            config.creditMultiplier = 1f;

        if (config.reputationMultiplier <= 0f)
            config.reputationMultiplier = 1f;

        return config;
    }
    private int PickDifficulty(System.Random rng, RankQuestConfig rankConfig)
    {
        int minDifficulty = Mathf.Max(MinQuestDifficulty, rankConfig.minDifficulty);
        int maxRankDifficulty = Mathf.Max(minDifficulty, rankConfig.maxDifficulty);

        if (rankConfig.difficultyWeights != null && rankConfig.difficultyWeights.Length > 0)
        {
            int totalWeight = 0;

            for (int i = 0; i < rankConfig.difficultyWeights.Length; i++)
            {
                int difficulty = Mathf.Clamp(rankConfig.difficultyWeights[i].difficulty, minDifficulty, maxRankDifficulty);
                int weight = Mathf.Max(0, rankConfig.difficultyWeights[i].weight);

                if (difficulty <= 0 || weight <= 0)
                    continue;

                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                int roll = rng.Next(0, totalWeight);
                int cursor = 0;

                for (int i = 0; i < rankConfig.difficultyWeights.Length; i++)
                {
                    int difficulty = Mathf.Clamp(rankConfig.difficultyWeights[i].difficulty, minDifficulty, maxRankDifficulty);
                    int weight = Mathf.Max(0, rankConfig.difficultyWeights[i].weight);

                    if (difficulty <= 0 || weight <= 0)
                        continue;

                    cursor += weight;

                    if (roll < cursor)
                        return difficulty;
                }
            }
        }

        return rng.Next(minDifficulty, maxRankDifficulty + 1);
    }
    private void ApplyDeliveryTypeModifiers(ref QuestOffer offer, System.Random rng)
    {
        DeliveryType deliveryType = PickDeliveryType(rng);
        float rankT = GetRankProgress01(offer.reputationRankIndex);

        offer.deliveryType = deliveryType;
        offer.deliveryTimeMultiplier = GetDeliveryTimeMultiplier(deliveryType, rankT);
        offer.deliveryRewardMultiplier = GetDeliveryRewardMultiplier(deliveryType, rankT);
    }
    private DeliveryType PickDeliveryType(System.Random rng)
    {
        int urgentWeight = Mathf.Max(0, urgentDeliveryWeight);
        int standardWeight = Mathf.Max(0, standardDeliveryWeight);
        int relaxedWeight = Mathf.Max(0, relaxedDeliveryWeight);
        int totalWeight = urgentWeight + standardWeight + relaxedWeight;

        if (totalWeight <= 0)
            return DeliveryType.Standard;

        int roll = rng.Next(0, totalWeight);

        if (roll < urgentWeight)
            return DeliveryType.Urgent;

        roll -= urgentWeight;

        if (roll < standardWeight)
            return DeliveryType.Standard;

        return DeliveryType.Relaxed;
    }
    private float GetRankProgress01(int rankIndex)
    {
        int maxRankIndex = rankQuestConfigs != null && rankQuestConfigs.Length > 0
            ? rankQuestConfigs.Length - 1
            : 0;

        if (maxRankIndex <= 0)
            return 0f;

        return Mathf.Clamp01((float)Mathf.Clamp(rankIndex, 0, maxRankIndex) / maxRankIndex);
    }
    private float GetDeliveryTimeMultiplier(DeliveryType deliveryType, float rankT)
    {
        switch (deliveryType)
        {
            case DeliveryType.Urgent:
                return Mathf.Lerp(urgentTimeMultiplierRange.x, urgentTimeMultiplierRange.y, rankT);

            case DeliveryType.Relaxed:
                return Mathf.Lerp(relaxedTimeMultiplierRange.x, relaxedTimeMultiplierRange.y, rankT);

            default:
                return 1f;
        }
    }
    private float GetDeliveryRewardMultiplier(DeliveryType deliveryType, float rankT)
    {
        switch (deliveryType)
        {
            case DeliveryType.Urgent:
                return Mathf.Lerp(urgentRewardMultiplierRange.x, urgentRewardMultiplierRange.y, rankT);

            case DeliveryType.Relaxed:
                return Mathf.Lerp(relaxedRewardMultiplierRange.x, relaxedRewardMultiplierRange.y, rankT);

            default:
                return 1f;
        }
    }
    private float CalculateExpectedDeliveryTimeSeconds(float distance, float timeMultiplier)
    {
        ActiveQuest previewQuest = new ActiveQuest
        {
            distanceAtAccept = distance,
            deliveryTimeMultiplier = timeMultiplier
        };

        return CalculateExpectedDeliveryTimeSeconds(previewQuest);
    }

    private float CalculateExpectedDeliveryTimeSeconds(ActiveQuest quest)
    {
        PackageDurabilityManager packageManager = packageDurabilityManager != null
            ? packageDurabilityManager
            : PackageDurabilityManager.Instance;
        if (packageManager == null)
            packageManager = FindFirstObjectByType<PackageDurabilityManager>();

        if (packageManager != null)
            return packageManager.CalculateStartingDeliveryTime(quest);

        return 0f;
    }
    private int GetCurrentReputationRankIndex()
    {
        if (FamilyReputationManager.Instance == null)
            return 0;

        return FamilyReputationManager.Instance.GetCurrentRankIndex();
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

        bool isLate = PackageDurabilityManager.Instance != null && PackageDurabilityManager.Instance.IsPackageLate(quest.questId);

        PackageDurabilityManager.DeliveryQuality quality =
            isLate
                ? PackageDurabilityManager.DeliveryQuality.Failed
                : PackageDurabilityManager.Instance != null
                    ? PackageDurabilityManager.Instance.GetDeliveryQuality(integrity)
                    : PackageDurabilityManager.DeliveryQuality.Good;

        float multiplier =
            PackageDurabilityManager.Instance != null
                ? PackageDurabilityManager.Instance.GetRewardMultiplier(quality)
                : 1f;

        float rawCredits = CalculateQuestCredits(quest);

        float finalCredits = Mathf.Round(rawCredits * multiplier);

        if (finalCredits > 0f)
            inv.GiveXCredits(finalCredits);

        float moduleRewardChance = GetModuleRewardChance(quality);
        bool shouldGiveModule = UnityEngine.Random.value <= moduleRewardChance;

        ModuleData rewardModule = null;

        if (shouldGiveModule)
            rewardModule = inv.GiveModuleRewardFromCurrentReputation();

        bool gaveModule = rewardModule != null;

        int reputationExpReward = CalculateQuestReputationExp(quest, quality);

        if (RewardPopupUI.Instance != null)
        {
            RewardPopupUI.Instance.ShowDeliveryReward(
                FormatDeliveryQuality(quality),
                finalCredits,
                rewardModule,
                reputationExpReward
            );
        }

        if (FamilyReputationManager.Instance != null)
        {
            FamilyReputationManager.Instance.AddReputationExp(
                reputationExpReward
            );
        }

        Debug.Log(
            $"[NPCQuestManager] Delivery complete. " +
            $"Quality={quality}, Late={isLate}, Integrity={integrity:0}%, " +
            $"Difficulty={quest.difficulty}, OfferRank={quest.reputationRankIndex}, " +
            $"Credits={finalCredits:0}, ReputationXP={reputationExpReward}, ModuleGiven={gaveModule}"
        );

        return GetDeliveryQualityIndex(quality);
    }
    private float CalculateQuestCredits(ActiveQuest quest)
    {
        RankQuestConfig rankConfig = GetRankQuestConfig(quest.reputationRankIndex);
        DifficultyRewardConfig difficultyConfig = GetDifficultyRewardConfig(quest.difficulty);

        float distanceRate = difficultyConfig.distanceCreditRate > 0f
            ? difficultyConfig.distanceCreditRate
            : distanceCreditRate;

        float difficultyCredits = difficultyConfig.baseCredits > 0f
            ? difficultyConfig.baseCredits
            : baseCreditReward + (quest.difficulty * rewardPerDifficulty);

        float distanceBonus = quest.distanceAtAccept * distanceRate;
        float rawCredits = difficultyCredits + distanceBonus;

        return (rawCredits * rankConfig.creditMultiplier) + rankConfig.creditBonus;
    }
    private int CalculateQuestReputationExp(ActiveQuest quest, PackageDurabilityManager.DeliveryQuality quality)
    {
        RankQuestConfig rankConfig = GetRankQuestConfig(quest.reputationRankIndex);
        DifficultyRewardConfig difficultyConfig = GetDifficultyRewardConfig(quest.difficulty);

        int baseReputationExp = difficultyConfig.baseReputationExp > 0
            ? difficultyConfig.baseReputationExp
            : GetFallbackBaseReputationExp(quest.difficulty);

        float rawReputationExp = (baseReputationExp * rankConfig.reputationMultiplier) + rankConfig.reputationBonus;
        float qualityMultiplier = GetReputationQualityMultiplier(quality);

        return Mathf.RoundToInt(rawReputationExp * qualityMultiplier);
    }
    private DifficultyRewardConfig GetDifficultyRewardConfig(int difficulty)
    {
        if (difficultyRewardConfigs != null)
        {
            for (int i = 0; i < difficultyRewardConfigs.Length; i++)
            {
                if (difficultyRewardConfigs[i].difficulty == difficulty)
                    return difficultyRewardConfigs[i];
            }
        }

        return new DifficultyRewardConfig
        {
            difficulty = difficulty,
            baseCredits = baseCreditReward + (difficulty * rewardPerDifficulty),
            distanceCreditRate = distanceCreditRate
        };
    }
    private int GetFallbackBaseReputationExp(int difficulty)
    {
        switch (difficulty)
        {
            case 1:
                return 180;

            case 2:
                return 300;

            case 3:
                return 475;

            case 4:
                return 700;

            case 5:
                return 975;

            case 6:
                return 1350;

            case 7:
                return 1850;

            default:
                return Mathf.Max(0, Mathf.RoundToInt(120f + (Mathf.Max(1, difficulty) * 120f)));
        }
    }
    private float GetReputationQualityMultiplier(PackageDurabilityManager.DeliveryQuality quality)
    {
        switch (quality)
        {
            case PackageDurabilityManager.DeliveryQuality.Perfect:
                return perfectDeliveryReputationMultiplier;

            case PackageDurabilityManager.DeliveryQuality.Good:
                return goodDeliveryReputationMultiplier;

            case PackageDurabilityManager.DeliveryQuality.Damaged:
                return damagedDeliveryReputationMultiplier;

            case PackageDurabilityManager.DeliveryQuality.BarelyDelivered:
                return barelyDeliveredReputationMultiplier;

            case PackageDurabilityManager.DeliveryQuality.Failed:
                return failedDeliveryReputationMultiplier;

            default:
                return 0f;
        }
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