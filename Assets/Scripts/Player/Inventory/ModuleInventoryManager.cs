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

    [Header("Random Module Debug")]
    public string[] debugModuleTypes =
{
        "Engine",
        "Battery",
        "Orbit"
    };

    public int minDebugTier = 0;
    public int maxDebugTier = 6;
    public bool saveGeneratedDebugAssetsInEditor = true;

    [Header("Currency")]
    public float credits = 0f;
    public TMP_Text creditsText;

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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.Alpha0))
            GenerateRandomDebugModule();
    }
    private void GenerateRandomDebugModule()
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot generate module. ModuleGenerator.Instance is null.");
            return;
        }

        if (debugModuleTypes == null || debugModuleTypes.Length == 0)
        {
            Debug.LogWarning("[Inventory] Cannot generate module. No debug module types assigned.");
            return;
        }

        string randomType = debugModuleTypes[Random.Range(0, debugModuleTypes.Length)];
        int randomTier = Random.Range(minDebugTier, maxDebugTier + 1);

        ModuleData generatedModule = ModuleGenerator.Instance.GenerateModule(
            randomType,
            randomTier,
            saveGeneratedDebugAssetsInEditor
        );

        if (generatedModule == null)
        {
            Debug.LogWarning($"[Inventory] Module generation failed. Type: {randomType}, Tier: {randomTier}");
            return;
        }

        AddModule(generatedModule, 1);

        Debug.Log($"[Inventory] Generated random module: {generatedModule.moduleName} | Type: {generatedModule.moduleType} | Tier: {generatedModule.moduleTier}");
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

        RefreshInventoryUI();
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

        RefreshInventoryUI();
    }

    public int GetAmount(ModuleData moduleData)
    {
        if (moduleData == null)
            return 0;

        return ownedModules.TryGetValue(moduleData, out int amount) ? amount : 0;
    }

    public void RefreshInventoryUI()
    {
        ClearCurrentSlots();

        int usedSlots = 0;

        List<KeyValuePair<ModuleData, int>> sortedModules = GetSortedOwnedModules();

        foreach (var pair in sortedModules)
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
        if (creditsText == null)
            return;

        int shownCredits = Mathf.RoundToInt(_displayedCredits);
        int shownPending = Mathf.RoundToInt(_pendingCreditGain);

        if (shownPending > 0)
            creditsText.text = $"<color=#4DFF88>{shownPending}</color> + {shownCredits} credits";
        else
            creditsText.text = $"{shownCredits} credits";
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

        RefreshInventoryUI();
    }
    private List<KeyValuePair<ModuleData, int>> GetSortedOwnedModules()
    {
        List<KeyValuePair<ModuleData, int>> modules = new List<KeyValuePair<ModuleData, int>>(ownedModules);

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

        return modules;
    }
    private int CompareByType(KeyValuePair<ModuleData, int> a, KeyValuePair<ModuleData, int> b)
    {
        int typeCompare = GetModuleStatSortRank(a.Key).CompareTo(GetModuleStatSortRank(b.Key));

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

        int typeCompare = GetModuleStatSortRank(a.Key).CompareTo(GetModuleStatSortRank(b.Key));

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

            int typeCompare = GetModuleStatSortRank(a.Key).CompareTo(GetModuleStatSortRank(b.Key));

            if (typeCompare != 0)
                return typeCompare;

            return b.Key.moduleTier.CompareTo(a.Key.moduleTier);
        }

        // Both are amount 1, so keep them mostly in the normal Type order.
        return CompareByType(a, b);
    }
    private int GetModuleStatSortRank(ModuleData module)
    {
        if (module == null)
            return 999;

        // 0 - Max Speed
        if (!Mathf.Approximately(module.maxSpeedBonus, 0f) ||
            !Mathf.Approximately(module.maxSpeedBonus_Percent, 0f))
            return 0;

        // 1 - Acceleration
        if (!Mathf.Approximately(module.accelerationBonus, 0f) ||
            !Mathf.Approximately(module.accelerationBonus_Percent, 0f))
            return 1;

        // 2 - Boost Acceleration
        if (!Mathf.Approximately(module.boostAccelAddBonus, 0f) ||
            !Mathf.Approximately(module.boostAccelAddBonus_Percent, 0f))
            return 2;

        // 3 - Boost Max Speed
        if (!Mathf.Approximately(module.boostMaxBonus, 0f) ||
            !Mathf.Approximately(module.boostMaxBonus_Percent, 0f))
            return 3;

        // 4 - Boost Capacity
        if (!Mathf.Approximately(module.capacityBonus, 0f) ||
            !Mathf.Approximately(module.capacityBonus_Percent, 0f))
            return 4;

        // 5 - Boost Drain
        if (!Mathf.Approximately(module.drainPerSecondBonus, 0f) ||
            !Mathf.Approximately(module.drainPerSecondBonus_Percent, 0f))
            return 5;

        // 6 - Charge Rate
        if (!Mathf.Approximately(module.chargeRateBonus, 0f) ||
            !Mathf.Approximately(module.chargeRateBonus_Percent, 0f))
            return 6;

        // 7 - Launch Speed
        if (!Mathf.Approximately(module.baseLaunchSpeedBonus, 0f) ||
            !Mathf.Approximately(module.baseLaunchSpeedBonus_Percent, 0f))
            return 7;

        return 999;
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
}