using System.Collections.Generic;
using UnityEngine;

public class PackageDurabilityManager : MonoBehaviour
{
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

    [Header("UI Updates")]
    public float distanceUpdateInterval = 0.25f;

    private readonly Dictionary<int, TrackedPackage> _packagesByQuestId = new Dictionary<int, TrackedPackage>();

    private float _distanceUpdateTimer;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        _distanceUpdateTimer += Time.deltaTime;

        if (_distanceUpdateTimer >= distanceUpdateInterval)
        {
            _distanceUpdateTimer = 0f;
            RefreshAllCards();
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

        var package = new TrackedPackage
        {
            quest = quest,
            deliveryItem = PickDeliveryItemName(quest),
            integrity = startingIntegrity,
            card = card
        };

        _packagesByQuestId.Add(quest.questId, package);
        RefreshCard(package);
    }

    public void RemoveQuest(int questId)
    {
        if (!_packagesByQuestId.TryGetValue(questId, out var package))
            return;

        if (package.card != null)
            Destroy(package.card.gameObject);

        _packagesByQuestId.Remove(questId);
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

            package.integrity -= damage;
            package.integrity = Mathf.Clamp(package.integrity, 0f, 100f);

            RefreshCard(package);
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
            "--:--"
        );
    }

    private string PickDeliveryItemName(NPCQuestManager.ActiveQuest quest)
    {
        if (deliveryItems == null || deliveryItems.Count == 0)
            return "Package";

        int index = Mathf.Abs(quest.questId) % deliveryItems.Count;
        return deliveryItems[index].itemName;
    }
}