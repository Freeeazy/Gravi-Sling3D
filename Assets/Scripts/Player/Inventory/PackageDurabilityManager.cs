using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PackageDurabilityManager : MonoBehaviour
{
    [Header("Quest Manager")]
    public NPCQuestManager questManager;
    public static PackageDurabilityManager Instance { get; private set; }

    [System.Serializable]
    public class DeliveryItemEntry
    {
        public string itemName = "Poptarts";
    }

    private class TrackedPackage
    {
        public NPCQuestManager.ActiveQuest quest;
        public string deliveryItem;
        public float integrity;
        public float timeRemaining;
        public ActiveContractCardUI card;
    }

    [Header("Refs")]
    public Transform player;
    public ActiveContractCardUI contractCardPrefab;
    public Transform contractCardParent;

    [Header("Delivery Items")]
    public List<DeliveryItemEntry> deliveryItems = new List<DeliveryItemEntry>()
    {
        new DeliveryItemEntry { itemName = "Poptarts" },
        new DeliveryItemEntry { itemName = "Space Pizza" },
        new DeliveryItemEntry { itemName = "Quantum Battery" },
        new DeliveryItemEntry { itemName = "Suspicious Crate" },
        new DeliveryItemEntry { itemName = "Frozen Burrito" }
    };

    [Header("Durability")]
    public float startingIntegrity = 100f;

    [Header("Durability Damage")]
    [Tooltip("Lowest lossFrac needed before packages take damage.")]
    public float minSeverityForDamage = 0.05f;

    [Tooltip("Damage dealt at minimum severity.")]
    public float lightDamage = 3f;

    [Tooltip("Damage dealt at heavy severity.")]
    public float heavyDamage = 18f;

    [Tooltip("Severity value considered a heavy smash. Usually around 0.35.")]
    public float heavySeverity = 0.35f;

    [Header("Delivery Timer")]
    [Tooltip("Base time given per delivery in seconds.")]
    public float baseDeliveryTime = 30f;

    [Tooltip("Extra seconds given per 1000 units of delivery distance.")]
    public float secondsPer1000Units = 10f;

    [Tooltip("How many seconds late a package can be before it expires. Example: 30 means it expires at -00:30.")]
    public float maxLateSecondsBeforeExpire = 30f;

    [Header("UI Updates")]
    public float distanceUpdateInterval = 0.25f;

    [Header("Average Delivery Durability")]
    public TMP_Text averageDurabilityText;
    public int maxStoredDeliveries = 100;

    private readonly Dictionary<int, TrackedPackage> _packagesByQuestId = new Dictionary<int, TrackedPackage>();

    private float _distanceUpdateTimer;

    private readonly List<float> _recentDeliveryDurabilities = new List<float>();

    private void Awake()
    {
        Instance = this;
        RefreshAverageDurabilityText();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        TickPackageTimers();

        _distanceUpdateTimer += Time.deltaTime;

        if (_distanceUpdateTimer >= distanceUpdateInterval)
        {
            _distanceUpdateTimer = 0f;
            RefreshAllCards();
        }
    }
    private void TickPackageTimers()
    {
        if (_packagesByQuestId.Count == 0)
            return;

        List<int> expiredQuestIds = null;

        foreach (var pair in _packagesByQuestId)
        {
            TrackedPackage package = pair.Value;
            package.timeRemaining -= Time.deltaTime;

            if (package.timeRemaining <= -maxLateSecondsBeforeExpire)
            {
                if (expiredQuestIds == null)
                    expiredQuestIds = new List<int>();

                expiredQuestIds.Add(pair.Key);
            }
        }

        if (expiredQuestIds == null)
            return;

        foreach (int questId in expiredQuestIds)
        {
            ExpirePackage(questId);
        }
    }

    public void RegisterQuest(NPCQuestManager.ActiveQuest quest)
    {
        if (_packagesByQuestId.ContainsKey(quest.questId))
            return;

        if (contractCardPrefab == null || contractCardParent == null)
        {
            Debug.LogWarning("[PackageDurabilityManager] Missing contract card prefab or parent.");
            return;
        }

        ActiveContractCardUI card = Instantiate(contractCardPrefab, contractCardParent);

        if (ContractCardJitterManager.Instance != null)
        {
            ContractCardJitterManager.Instance.RegisterCard(card.transform as RectTransform);
        }

        float startingTime =
            baseDeliveryTime +
            ((quest.distanceAtAccept / 1000f) * secondsPer1000Units);

        var package = new TrackedPackage
        {
            quest = quest,
            deliveryItem = PickDeliveryItemName(quest),
            integrity = startingIntegrity,
            timeRemaining = startingTime,
            card = card
        };

        _packagesByQuestId.Add(quest.questId, package);
        RefreshCard(package);
    }

    public void RemoveQuest(int questId)
    {
        if (!_packagesByQuestId.TryGetValue(questId, out var package))
            return;

        RecordCompletedDelivery(package.integrity);

        if (ContractCardJitterManager.Instance != null && package.card != null)
        {
            ContractCardJitterManager.Instance.UnregisterCard(package.card.transform as RectTransform);
        }

        if (package.card != null)
            Destroy(package.card.gameObject);

        _packagesByQuestId.Remove(questId);
    }
    private void ExpirePackage(int questId)
    {
        if (!_packagesByQuestId.TryGetValue(questId, out var package))
            return;

        Debug.Log($"[PackageDurabilityManager] Package expired: {package.deliveryItem} for quest {questId}");

        package.integrity = 0f;

        RecordCompletedDelivery(package.integrity);

        if (ContractCardJitterManager.Instance != null && package.card != null)
        {
            ContractCardJitterManager.Instance.UnregisterCard(package.card.transform as RectTransform);
        }

        if (package.card != null)
            Destroy(package.card.gameObject);

        _packagesByQuestId.Remove(questId);

        NPCQuestManager manager = questManager != null
            ? questManager
            : FindFirstObjectByType<NPCQuestManager>();

        if (manager != null)
        {
            manager.FailQuest(questId);
        }
        else
        {
            Debug.LogWarning("[PackageDurabilityManager] Could not find NPCQuestManager to fail expired quest.");
        }
    }
    public enum DeliveryQuality
    {
        Perfect,
        Good,
        Damaged,
        BarelyDelivered,
        Failed
    }

    public bool TryGetPackageIntegrity(int questId, out float integrity)
    {
        integrity = 0f;

        if (!_packagesByQuestId.TryGetValue(questId, out var package))
            return false;

        integrity = Mathf.Clamp(package.integrity, 0f, 100f);
        return true;
    }

    public DeliveryQuality GetDeliveryQuality(float integrity)
    {
        integrity = Mathf.Clamp(integrity, 0f, 100f);

        if (integrity <= 0f)
            return DeliveryQuality.Failed;

        if (integrity < 30f)
            return DeliveryQuality.BarelyDelivered;

        if (integrity < 60f)
            return DeliveryQuality.Damaged;

        if (integrity < 90f)
            return DeliveryQuality.Good;

        return DeliveryQuality.Perfect;
    }

    public float GetRewardMultiplier(DeliveryQuality quality)
    {
        switch (quality)
        {
            case DeliveryQuality.Perfect:
                return 1.25f; // full pay + bonus

            case DeliveryQuality.Good:
                return 1f; // normal pay

            case DeliveryQuality.Damaged:
                return 0.5f; // reduced pay

            case DeliveryQuality.BarelyDelivered:
                return 0.15f; // tiny pay

            case DeliveryQuality.Failed:
                return 0f; // no pay

            default:
                return 0f;
        }
    }

    public void ApplyImpactDamage(float impactSeverity)
    {
        if (_packagesByQuestId.Count == 0)
            return;

        if (impactSeverity < minSeverityForDamage)
            return;

        float t = Mathf.InverseLerp(minSeverityForDamage, heavySeverity, impactSeverity);
        float damage = Mathf.Lerp(lightDamage, heavyDamage, t);

        foreach (var pair in _packagesByQuestId)
        {
            TrackedPackage package = pair.Value;

            float oldIntegrity = package.integrity;

            package.integrity -= damage;
            package.integrity = Mathf.Clamp(package.integrity, 0f, 100f);

            float actualDamageTaken = oldIntegrity - package.integrity;

            RefreshCard(package);

            if (package.card != null)
                package.card.ShowIntegrityDamage(actualDamageTaken);
        }

        if (ContractCardJitterManager.Instance != null)
        {
            ContractCardJitterManager.Instance.JitterAllCards();
        }

        Debug.Log($"[PackageDurabilityManager] Package damage applied: -{damage:0}% from severity {impactSeverity:0.00}");
    }

    private void RefreshAllCards()
    {
        foreach (var pair in _packagesByQuestId)
        {
            RefreshCard(pair.Value);
        }
    }

    private void RefreshCard(TrackedPackage package)
    {
        if (package == null || package.card == null)
            return;

        float distance = package.quest.distanceAtAccept;

        if (player != null)
            distance = Vector3.Distance(player.position, package.quest.toWorldPos);

        string destinationName = "Target Station";

        package.card.SetInfo(
            package.deliveryItem,
            destinationName,
            distance,
            package.integrity,
            FormatPackageTimer(package.timeRemaining)
        );
    }

    private string PickDeliveryItemName(NPCQuestManager.ActiveQuest quest)
    {
        if (deliveryItems == null || deliveryItems.Count == 0)
            return "Package";

        int index = Mathf.Abs(quest.questId) % deliveryItems.Count;
        return deliveryItems[index].itemName;
    }
    private void RecordCompletedDelivery(float finalIntegrity)
    {
        _recentDeliveryDurabilities.Add(Mathf.Clamp(finalIntegrity, 0f, 100f));

        if (_recentDeliveryDurabilities.Count > maxStoredDeliveries)
            _recentDeliveryDurabilities.RemoveAt(0);

        RefreshAverageDurabilityText();
    }

    private void RefreshAverageDurabilityText()
    {
        if (averageDurabilityText == null)
            return;

        if (_recentDeliveryDurabilities.Count == 0)
        {
            averageDurabilityText.text = "--%";
            return;
        }

        float total = 0f;

        for (int i = 0; i < _recentDeliveryDurabilities.Count; i++)
        {
            total += _recentDeliveryDurabilities[i];
        }

        float average = total / _recentDeliveryDurabilities.Count;

        averageDurabilityText.text = FormatAveragePercent(average);
    }

    private string FormatAveragePercent(float value)
    {
        value = Mathf.Clamp(value, 0f, 100f);

        if (Mathf.Approximately(value, 100f))
            return "100%";

        float roundedToOneDecimal = Mathf.Round(value * 10f) / 10f;

        if (Mathf.Approximately(roundedToOneDecimal % 1f, 0f))
            return $"{Mathf.RoundToInt(roundedToOneDecimal)}%";

        return $"{roundedToOneDecimal:0.#}%";
    }
    private string FormatPackageTimer(float seconds)
    {
        bool isLate = seconds < 0f;
        float absSeconds = Mathf.Abs(seconds);

        int minutes = Mathf.FloorToInt(absSeconds / 60f);
        int secs = Mathf.FloorToInt(absSeconds % 60f);

        string formatted = $"{minutes:00}:{secs:00}";

        return isLate ? $"<color=#FF3D3D>-{formatted}</color>" : formatted;
    }
    public bool TryGetPackageTimer(int questId, out float timeRemaining)
    {
        timeRemaining = 0f;

        if (!_packagesByQuestId.TryGetValue(questId, out var package))
            return false;

        timeRemaining = package.timeRemaining;
        return true;
    }

    public bool IsPackageLate(int questId)
    {
        return TryGetPackageTimer(questId, out float timeRemaining) && timeRemaining <= 0f;
    }
    public bool HasActivePackage()
    {
        return _packagesByQuestId.Count > 0;
    }
}