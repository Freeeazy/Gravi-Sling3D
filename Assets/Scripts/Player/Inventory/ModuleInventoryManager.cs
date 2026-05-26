using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
 
public class ModuleInventoryManager : MonoBehaviour
{
    public static ModuleInventoryManager Instance { get; private set; }

    [Header("Inventory Setup")]
    public GameObject blankSlotPrefab;
    public GameObject emptySlotPrefab;
    public int totalSlots = 30;

    [Header("Random Module Reward")]
    public string[] rewardModuleTypes =
{
        "Engine",
        "Battery",
        "Orbit"
    };

    public bool saveGeneratedRewardAssetsInEditor = true;

    [Header("Currency")]
    public float credits = 0f;
    public TMP_Text creditsText;
    public TMP_Text[] extraCreditsTexts;

    [Header("Currency Animation")]
    public float creditTickDelay = 0.02f;
    public float creditAnimationDuration = 0.75f;

    [Header("Inventory Counter")]
    public TMP_Text inventoryCounterText;

    [Header("UI Parent")]
    public Transform moduleListParent;

    [Header("Runtime Drag References")]
    public RectTransform dragCanvas;
    public ScrollRect parentScrollRect;
    public Camera uiCamera;

    [Header("Module Tier Colors")]
    public Color tier0Color = new Color(0.45f, 0.45f, 0.45f, 1f); // Gray
    public Color tier1Color = Color.white;                        // White
    public Color tier2Color = new Color(0.3f, 1f, 0.3f, 1f);       // Green
    public Color tier3Color = new Color(0.3f, 0.6f, 1f, 1f);       // Blue
    public Color tier4Color = new Color(1f, 0.75f, 0.15f, 1f);     // Orange / Yellow
    public Color tier5Color = new Color(1f, 0.2f, 0.2f, 1f);       // Red
    public Color tier6Color = new Color(0.75f, 0.3f, 1f, 1f);      // Purple

    [Header("Sorting")]
    public TMP_Dropdown sortDropdown;

    private readonly Dictionary<ModuleData, int> ownedModules = new Dictionary<ModuleData, int>();

    private float _displayedCredits = 0f;
    private float _pendingCreditGain = 0f;
    private Coroutine _creditsRoutine;
    private bool _refreshInventoryQueued;
    private readonly List<KeyValuePair<ModuleData, int>> _sortedModules = new List<KeyValuePair<ModuleData, int>>(64);

    private enum InventorySortMode
    {
        Type,
        Tier,
        Amount
    }

    private InventorySortMode currentSortMode = InventorySortMode.Type;

    private void Awake()
    {
        Instance = this;

        if (moduleListParent == null)
            moduleListParent = transform;

        SetupSortDropdown();

        RefreshInventoryUI();

        _displayedCredits = credits;
        UpdateCreditsText();
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    private void OnDisable()
    {
        _refreshInventoryQueued = false;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.Alpha0))
            GiveModuleRewardFromCurrentReputation();
    }
    public ModuleData GiveModuleRewardFromCurrentReputation()
    {
        if (FamilyReputationManager.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot give module reward. FamilyReputationManager.Instance is null.");
            return null;
        }

        int currentRankIndex = FamilyReputationManager.Instance.GetCurrentRankIndex();

        ModuleData rewardModule = GenerateModuleRewardFromRank(currentRankIndex);

        if (rewardModule == null)
            return null;

        AddModule(rewardModule, 1);

        Debug.Log($"[Inventory] Gave reputation reward module: {rewardModule.moduleName} | Rank Index: {currentRankIndex}");

        return rewardModule;
    }
    public ModuleData GiveModuleRewardFromCurrentReputationAndType(string forcedModuleType)
    {
        if (FamilyReputationManager.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot give typed module reward. FamilyReputationManager.Instance is null.");
            return null;
        }

        int currentRankIndex = FamilyReputationManager.Instance.GetCurrentRankIndex();

        ModuleData rewardModule = GenerateModuleRewardFromRankAndType(currentRankIndex, forcedModuleType);

        if (rewardModule == null)
            return null;

        AddModule(rewardModule, 1);

        Debug.Log($"[Inventory] Gave typed reputation reward module: {rewardModule.moduleName} | Type: {forcedModuleType} | Rank Index: {currentRankIndex}");

        return rewardModule;
    }
    public ModuleData GenerateModuleRewardFromRankAndType(int rankIndex, string forcedModuleType)
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot generate typed reward module. ModuleGenerator.Instance is null.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(forcedModuleType))
        {
            Debug.LogWarning("[Inventory] Cannot generate typed reward module. forcedModuleType is empty.");
            return null;
        }

        int rolledTier = RollModuleTierFromRank(rankIndex);

        ModuleData generatedModule = ModuleGenerator.Instance.GenerateModule(
            forcedModuleType,
            rolledTier,
            saveGeneratedRewardAssetsInEditor
        );

        if (generatedModule == null)
        {
            Debug.LogWarning($"[Inventory] Typed reward module generation failed. Type: {forcedModuleType}, Tier: {rolledTier}");
            return null;
        }

        Debug.Log($"[Inventory] Generated typed reward module: {generatedModule.moduleName} | Type: {generatedModule.moduleType} | Tier: {generatedModule.moduleTier}");

        return generatedModule;
    }
    public ModuleData GenerateModuleRewardFromRank(int rankIndex)
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot generate reward module. ModuleGenerator.Instance is null.");
            return null;
        }

        if (rewardModuleTypes == null || rewardModuleTypes.Length == 0)
        {
            Debug.LogWarning("[Inventory] Cannot generate reward module. No reward module types assigned.");
            return null;
        }

        int rolledTier = RollModuleTierFromRank(rankIndex);
        string randomType = rewardModuleTypes[Random.Range(0, rewardModuleTypes.Length)];

        ModuleData generatedModule = ModuleGenerator.Instance.GenerateModule(
            randomType,
            rolledTier,
            saveGeneratedRewardAssetsInEditor
        );

        if (generatedModule == null)
        {
            Debug.LogWarning($"[Inventory] Reward module generation failed. Type: {randomType}, Tier: {rolledTier}");
            return null;
        }

        Debug.Log($"[Inventory] Generated reward module: {generatedModule.moduleName} | Type: {generatedModule.moduleType} | Tier: {generatedModule.moduleTier}");

        return generatedModule;
    }
    public ModuleData GenerateModuleByTypeAndTier(string moduleType, int tier)
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot generate module. ModuleGenerator.Instance is null.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(moduleType))
        {
            Debug.LogWarning("[Inventory] Cannot generate module. moduleType is empty.");
            return null;
        }

        tier = Mathf.Clamp(tier, 0, 6);

        ModuleData generatedModule = ModuleGenerator.Instance.GenerateModule(
            moduleType,
            tier,
            saveGeneratedRewardAssetsInEditor
        );

        if (generatedModule == null)
        {
            Debug.LogWarning($"[Inventory] Exact module generation failed. Type: {moduleType}, Tier: {tier}");
            return null;
        }

        Debug.Log($"[Inventory] Generated exact module: {generatedModule.moduleName} | Type: {generatedModule.moduleType} | Tier: {generatedModule.moduleTier}");

        return generatedModule;
    }
    public void AddModule(ModuleData moduleData, int amount = 1)
    {
        if (moduleData == null || amount <= 0)
            return;

        ModuleData existingMatch = FindMatchingOwnedModule(moduleData);

        if (existingMatch != null)
        {
            ownedModules[existingMatch] += amount;
        }
        else
        {
            ownedModules.Add(moduleData, amount);
        }

        RequestInventoryRefresh();
    }

    public void RemoveModule(ModuleData moduleData, int amount = 1)
    {
        if (moduleData == null || amount <= 0)
            return;

        if (!ownedModules.ContainsKey(moduleData))
            return;

        ownedModules[moduleData] -= amount;

        if (ownedModules[moduleData] <= 0)
            ownedModules.Remove(moduleData);

        RequestInventoryRefresh();
    }

    public int GetAmount(ModuleData moduleData)
    {
        if (moduleData == null)
            return 0;

        return ownedModules.TryGetValue(moduleData, out int amount) ? amount : 0;
    }

    public void RefreshInventoryUI()
    {
        _refreshInventoryQueued = false;

        ClearCurrentSlots();

        int usedSlots = 0;

        GetSortedOwnedModules(_sortedModules);

        foreach (var pair in _sortedModules)
        {
            ModuleData data = pair.Key;
            int amount = pair.Value;

            if (data == null || amount <= 0)
                continue;

            if (blankSlotPrefab == null)
            {
                Debug.LogWarning("[Inventory] blankSlotPrefab is missing.");
                continue;
            }

            GameObject newSlotObject = Instantiate(blankSlotPrefab, moduleListParent);

            ModuleButtonUI newSlot = newSlotObject.GetComponent<ModuleButtonUI>();

            if (newSlot == null)
            {
                Debug.LogWarning($"[Inventory] blankSlotPrefab does not have a ModuleButtonUI component.");
                continue;
            }

            newSlot.moduleData = data;
            newSlot.dragCanvas = dragCanvas;
            newSlot.parentScrollRect = parentScrollRect;
            newSlot.uiCamera = uiCamera;
            newSlot.SetAmount(amount);

            usedSlots++;
        }

        UpdateInventoryCounter();

        int emptySlotsToCreate = Mathf.Max(0, totalSlots - usedSlots);

        for (int i = 0; i < emptySlotsToCreate; i++)
        {
            if (emptySlotPrefab != null)
                Instantiate(emptySlotPrefab, moduleListParent);
        }
    }
    private void RequestInventoryRefresh()
    {
        if (!isActiveAndEnabled)
        {
            RefreshInventoryUI();
            return;
        }

        if (_refreshInventoryQueued)
            return;

        _refreshInventoryQueued = true;
        StartCoroutine(RefreshInventoryNextFrame());
    }

    private IEnumerator RefreshInventoryNextFrame()
    {
        yield return null;

        RefreshInventoryUI();
    }
    private void ClearCurrentSlots()
    {
        for (int i = moduleListParent.childCount - 1; i >= 0; i--)
        {
            Destroy(moduleListParent.GetChild(i).gameObject);
        }
    }
    private void UpdateInventoryCounter()
    {
        if (inventoryCounterText == null)
            return;

        int uniqueCount = ownedModules.Count;
        inventoryCounterText.text = $"{uniqueCount}/{totalSlots}";
    }

    public void GiveXCredits(float creditsToGive)
    {
        if (creditsToGive <= 0f)
            return;

        credits += creditsToGive;
        _pendingCreditGain += creditsToGive;

        if (_creditsRoutine != null)
            StopCoroutine(_creditsRoutine);

        _creditsRoutine = StartCoroutine(AnimateCreditsText());

        Debug.Log($"[Inventory] Credits added: {creditsToGive}. Total credits: {credits}");
    }
    private void UpdateCreditsText()
    {
        int shownCredits = Mathf.RoundToInt(_displayedCredits);
        int shownPending = Mathf.RoundToInt(_pendingCreditGain);

        string creditsMessage;

        if (shownPending > 0)
            creditsMessage = $"<color=#4DFF88>{shownPending}</color> + {shownCredits} credits";
        else
            creditsMessage = $"{shownCredits} credits";

        SetCreditsText(creditsText, creditsMessage);

        if (extraCreditsTexts == null)
            return;

        for (int i = 0; i < extraCreditsTexts.Length; i++)
        {
            SetCreditsText(extraCreditsTexts[i], creditsMessage);
        }
    }
    public bool CanAfford(float cost)
    {
        return credits >= cost;
    }

    public bool TrySpendCredits(float cost)
    {
        if (cost <= 0f)
            return true;

        if (credits < cost)
        {
            Debug.Log($"[Inventory] Not enough credits. Cost: {cost}, Current: {credits}");
            return false;
        }

        credits -= cost;

        // Keep displayed credits from drifting weirdly after a purchase.
        _displayedCredits = credits;
        _pendingCreditGain = 0f;

        if (_creditsRoutine != null)
        {
            StopCoroutine(_creditsRoutine);
            _creditsRoutine = null;
        }

        UpdateCreditsText();

        Debug.Log($"[Inventory] Spent {cost} credits. Remaining: {credits}");
        return true;
    }
    public ModuleData TryBuyModuleFromShop(float cost, bool useRandomType, string forcedModuleType = "")
    {
        if (!TrySpendCredits(cost))
            return null;

        ModuleData boughtModule = null;

        if (useRandomType)
        {
            boughtModule = GiveModuleRewardFromCurrentReputation();
        }
        else
        {
            boughtModule = GiveModuleRewardFromCurrentReputationAndType(forcedModuleType);
        }

        if (boughtModule == null)
        {
            // Refund if generation failed.
            GiveXCredits(cost);
            Debug.LogWarning("[Inventory] Module shop purchase failed during generation. Refunded credits.");
            return null;
        }

        if (RewardPopupUI.Instance != null)
            RewardPopupUI.Instance.ShowModuleReward(boughtModule);

        return boughtModule;
    }
    private IEnumerator AnimateCreditsText()
    {
        float startCredits = _displayedCredits;
        float targetCredits = credits;

        float startPending = _pendingCreditGain;

        float elapsed = 0f;

        while (elapsed < creditAnimationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / creditAnimationDuration);

            _displayedCredits = Mathf.Lerp(startCredits, targetCredits, t);
            _pendingCreditGain = Mathf.Lerp(startPending, 0f, t);

            UpdateCreditsText();

            yield return null;
        }

        _displayedCredits = targetCredits;
        _pendingCreditGain = 0f;

        UpdateCreditsText();

        _creditsRoutine = null;
    }
    private ModuleData FindMatchingOwnedModule(ModuleData newModule)
    {
        foreach (ModuleData ownedModule in ownedModules.Keys)
        {
            if (AreModulesEquivalent(ownedModule, newModule))
                return ownedModule;
        }

        return null;
    }
    private bool AreModulesEquivalent(ModuleData a, ModuleData b)
    {
        if (a == null || b == null)
            return false;

        return
            a.moduleName == b.moduleName &&
            a.moduleType == b.moduleType &&
            a.moduleTier == b.moduleTier &&

            Mathf.Approximately(a.chargeRateBonus, b.chargeRateBonus) &&
            Mathf.Approximately(a.chargeRateBonus_Percent, b.chargeRateBonus_Percent) &&

            Mathf.Approximately(a.baseLaunchSpeedBonus, b.baseLaunchSpeedBonus) &&
            Mathf.Approximately(a.baseLaunchSpeedBonus_Percent, b.baseLaunchSpeedBonus_Percent) &&

            Mathf.Approximately(a.maxSpeedBonus, b.maxSpeedBonus) &&
            Mathf.Approximately(a.maxSpeedBonus_Percent, b.maxSpeedBonus_Percent) &&

            Mathf.Approximately(a.accelerationBonus, b.accelerationBonus) &&
            Mathf.Approximately(a.accelerationBonus_Percent, b.accelerationBonus_Percent) &&

            Mathf.Approximately(a.boostAccelAddBonus, b.boostAccelAddBonus) &&
            Mathf.Approximately(a.boostAccelAddBonus_Percent, b.boostAccelAddBonus_Percent) &&

            Mathf.Approximately(a.boostMaxBonus, b.boostMaxBonus) &&
            Mathf.Approximately(a.boostMaxBonus_Percent, b.boostMaxBonus_Percent) &&

            Mathf.Approximately(a.capacityBonus, b.capacityBonus) &&
            Mathf.Approximately(a.capacityBonus_Percent, b.capacityBonus_Percent) &&

            Mathf.Approximately(a.drainPerSecondBonus, b.drainPerSecondBonus) &&
            Mathf.Approximately(a.drainPerSecondBonus_Percent, b.drainPerSecondBonus_Percent) &&

            Mathf.Approximately(a.regenPerSecondBonus, b.regenPerSecondBonus) &&
            Mathf.Approximately(a.regenPerSecondBonus_Percent, b.regenPerSecondBonus_Percent);
    }
    private void SetupSortDropdown()
    {
        currentSortMode = InventorySortMode.Type;

        if (sortDropdown == null)
            return;

        sortDropdown.ClearOptions();

        sortDropdown.AddOptions(new List<string>
    {
        "Type",
        "Tier",
        "Amount"
    });

        sortDropdown.value = 0;
        sortDropdown.RefreshShownValue();

        sortDropdown.onValueChanged.RemoveListener(OnSortDropdownChanged);
        sortDropdown.onValueChanged.AddListener(OnSortDropdownChanged);
    }
    private void OnSortDropdownChanged(int optionIndex)
    {
        switch (optionIndex)
        {
            case 0:
                currentSortMode = InventorySortMode.Type;
                break;

            case 1:
                currentSortMode = InventorySortMode.Tier;
                break;

            case 2:
                currentSortMode = InventorySortMode.Amount;
                break;

            default:
                currentSortMode = InventorySortMode.Type;
                break;
        }

        RequestInventoryRefresh();
    }
    private void GetSortedOwnedModules(List<KeyValuePair<ModuleData, int>> modules)
    {
        modules.Clear();

        foreach (var pair in ownedModules)
            modules.Add(pair);

        switch (currentSortMode)
        {
            case InventorySortMode.Type:
                modules.Sort(CompareByType);
                break;

            case InventorySortMode.Tier:
                modules.Sort(CompareByTier);
                break;

            case InventorySortMode.Amount:
                modules.Sort(CompareByAmount);
                break;
        }
    }
    private int CompareByType(KeyValuePair<ModuleData, int> a, KeyValuePair<ModuleData, int> b)
    {
        string aType = a.Key != null ? a.Key.moduleType : "";
        string bType = b.Key != null ? b.Key.moduleType : "";

        int typeCompare = string.Compare(aType, bType, System.StringComparison.Ordinal);

        if (typeCompare != 0)
            return typeCompare;

        int tierCompare = b.Key.moduleTier.CompareTo(a.Key.moduleTier);

        if (tierCompare != 0)
            return tierCompare;

        return string.Compare(a.Key.moduleName, b.Key.moduleName, System.StringComparison.Ordinal);
    }

    private int CompareByTier(KeyValuePair<ModuleData, int> a, KeyValuePair<ModuleData, int> b)
    {
        int tierCompare = b.Key.moduleTier.CompareTo(a.Key.moduleTier);

        if (tierCompare != 0)
            return tierCompare;

        string aType = a.Key != null ? a.Key.moduleType : "";
        string bType = b.Key != null ? b.Key.moduleType : "";

        int typeCompare = string.Compare(aType, bType, System.StringComparison.Ordinal);

        if (typeCompare != 0)
            return typeCompare;

        return string.Compare(a.Key.moduleName, b.Key.moduleName, System.StringComparison.Ordinal);
    }

    private int CompareByAmount(KeyValuePair<ModuleData, int> a, KeyValuePair<ModuleData, int> b)
    {
        bool aStacked = a.Value >= 2;
        bool bStacked = b.Value >= 2;

        if (aStacked && !bStacked)
            return -1;

        if (!aStacked && bStacked)
            return 1;

        if (aStacked && bStacked)
        {
            int amountCompare = b.Value.CompareTo(a.Value);

            if (amountCompare != 0)
                return amountCompare;

            string aType = a.Key != null ? a.Key.moduleType : "";
            string bType = b.Key != null ? b.Key.moduleType : "";

            int typeCompare = string.Compare(aType, bType, System.StringComparison.Ordinal);

            if (typeCompare != 0)
                return typeCompare;

            return b.Key.moduleTier.CompareTo(a.Key.moduleTier);
        }

        // Both are amount 1, so keep them mostly in the normal Type order.
        return CompareByType(a, b);
    }
    private int RollModuleTierFromRank(int rankIndex)
    {
        rankIndex = Mathf.Clamp(rankIndex, 0, 4);

        float roll = Random.value * 100f;

        switch (rankIndex)
        {
            // Rookie
            // 70% Tier 0, 30% Tier 1
            case 0:
                if (roll < 70f) return 0;
                return 1;

            // Runner
            // 15% Tier 0, 55% Tier 1, 30% Tier 2
            case 1:
                if (roll < 15f) return 0;
                if (roll < 70f) return 1;
                return 2;

            // Trusted
            // 20% Tier 1, 50% Tier 2, 30% Tier 3
            case 2:
                if (roll < 20f) return 1;
                if (roll < 70f) return 2;
                return 3;

            // Made Courier
            // 20% Tier 2, 50% Tier 3, 30% Tier 4
            case 3:
                if (roll < 20f) return 2;
                if (roll < 70f) return 3;
                return 4;

            // Family Legend
            // 20% Tier 3, 50% Tier 4, 30% Tier 5
            case 4:
                if (roll < 20f) return 3;
                if (roll < 70f) return 4;
                return 5;

            default:
                return 0;
        }
    }
    public Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 0:
                return tier0Color;
            case 1:
                return tier1Color;
            case 2:
                return tier2Color;
            case 3:
                return tier3Color;
            case 4:
                return tier4Color;
            case 5:
                return tier5Color;
            case 6:
                return tier6Color;
            default:
                return tier0Color;
        }
    }
    private void SetCreditsText(TMP_Text text, string message)
    {
        if (text == null)
            return;

        text.text = message;
    }
}