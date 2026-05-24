using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    [Header("Research Slots")]
    public List<ModuleSlotUI> researchSlots = new List<ModuleSlotUI>();

    [Header("Confirm Button")]
    public Button confirmButton;
    public int minModulesRequired = 3;

    [Header("Research Chances")]
    [Range(0f, 1f)] public float threeMatchingTierUpgradeChance = 0.40f;
    [Range(0f, 1f)] public float fourMatchingTierUpgradeChance = 0.65f;
    [Range(0f, 1f)] public float fiveMatchingTierUpgradeChance = 0.90f;

    [Header("Type Bias")]
    [Tooltip("If at least 3 slotted modules share a type, this is the chance the output uses that type.")]
    [Range(0f, 1f)] public float matchingTypeChance = 0.70f;

    [Header("Available Module Types")]
    public string[] moduleTypes =
    {
        "Max Speed",
        "Acceleration",
        "Boost Acceleration",
        "Boost Max Speed",
        "Boost Capacity",
        "Boost Drain",
        "Boost Regen",
        "Charge Rate",
        "Launch Speed"
    };

    [Header("Optional Override")]
    [Tooltip("When this object is enabled, right-clicking modules will go into research slots instead of loadout slots.")]
    public bool acceptRightClickModules = true;

    [Tooltip("Optional: disable the normal loadout manager while research is open.")]
    public ModuleLoadoutManager loadoutManagerToDisable;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (loadoutManagerToDisable != null)
            loadoutManagerToDisable.enabled = false;

        RefreshConfirmButton();
    }

    private void OnDisable()
    {
        if (loadoutManagerToDisable != null)
            loadoutManagerToDisable.enabled = true;
    }

    private void Update()
    {
        RefreshConfirmButton();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryAssignToFirstEmptyResearchSlot(ModuleData moduleData)
    {
        if (!acceptRightClickModules)
            return false;

        if (moduleData == null)
            return false;

        foreach (ModuleSlotUI slot in researchSlots)
        {
            if (slot == null)
                continue;

            if (!slot.IsEmpty)
                continue;

            slot.SetModule(moduleData);

            if (ModuleInventoryManager.Instance != null)
                ModuleInventoryManager.Instance.RemoveModule(moduleData, 1);

            Debug.Log($"[ResearchManager] Assigned {moduleData.moduleName} to research slot.");

            RefreshConfirmButton();
            return true;
        }

        Debug.Log("[ResearchManager] No empty research slot available.");
        return false;
    }

    public void ConfirmResearch()
    {
        List<ModuleData> modules = GetSlottedModules();

        if (modules.Count < minModulesRequired)
        {
            Debug.Log("[ResearchManager] Not enough modules to research.");
            return;
        }

        int baseTier = GetMostCommonTier(modules, out int matchingTierCount);
        float upgradeChance = GetUpgradeChance(matchingTierCount);

        bool upgraded = Random.value <= upgradeChance;
        int outputTier = upgraded ? baseTier + 1 : baseTier;
        outputTier = Mathf.Clamp(outputTier, 0, 6);

        string outputType = PickOutputType(modules);

        ModuleInventoryManager inv = ModuleInventoryManager.Instance;

        if (inv == null)
        {
            Debug.LogWarning("[ResearchManager] Cannot complete research. ModuleInventoryManager.Instance is null.");
            return;
        }

        ModuleData outputModule = inv.GenerateModuleByTypeAndTier(outputType, outputTier);

        if (outputModule == null)
        {
            Debug.LogWarning("[ResearchManager] Research failed during output generation. Inputs were not consumed.");
            return;
        }

        ConsumeSlottedModules();

        inv.AddModule(outputModule, 1);

        if (RewardPopupUI.Instance != null)
            RewardPopupUI.Instance.ShowModuleReward(outputModule);

        Debug.Log(
            $"[ResearchManager] Research complete. " +
            $"InputTier={baseTier}, MatchingTierCount={matchingTierCount}, " +
            $"UpgradeChance={upgradeChance:P0}, Upgraded={upgraded}, " +
            $"Output={outputModule.moduleName}, Type={outputType}, Tier={outputTier}"
        );

        RefreshConfirmButton();
    }

    private float GetUpgradeChance(int matchingTierCount)
    {
        if (matchingTierCount >= 5)
            return fiveMatchingTierUpgradeChance;

        if (matchingTierCount >= 4)
            return fourMatchingTierUpgradeChance;

        if (matchingTierCount >= 3)
            return threeMatchingTierUpgradeChance;

        return 0f;
    }

    private int GetMostCommonTier(List<ModuleData> modules, out int matchingTierCount)
    {
        Dictionary<int, int> tierCounts = new Dictionary<int, int>();

        foreach (ModuleData module in modules)
        {
            if (module == null)
                continue;

            int tier = module.moduleTier;

            if (!tierCounts.ContainsKey(tier))
                tierCounts.Add(tier, 0);

            tierCounts[tier]++;
        }

        int bestTier = 0;
        int bestCount = 0;

        foreach (var pair in tierCounts)
        {
            int tier = pair.Key;
            int count = pair.Value;

            if (count > bestCount)
            {
                bestTier = tier;
                bestCount = count;
            }
            else if (count == bestCount && tier < bestTier)
            {
                // Tie breaker: use lower tier so mixed-tier cheese is less generous.
                bestTier = tier;
            }
        }

        matchingTierCount = bestCount;
        return bestTier;
    }

    private string PickOutputType(List<ModuleData> modules)
    {
        string matchingType = GetTypeWithAtLeastXMatches(modules, 3);

        if (!string.IsNullOrEmpty(matchingType) && Random.value <= matchingTypeChance)
            return matchingType;

        return PickRandomModuleType();
    }

    private string GetTypeWithAtLeastXMatches(List<ModuleData> modules, int requiredMatches)
    {
        Dictionary<string, int> typeCounts = new Dictionary<string, int>();

        foreach (ModuleData module in modules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.moduleType))
                continue;

            if (!typeCounts.ContainsKey(module.moduleType))
                typeCounts.Add(module.moduleType, 0);

            typeCounts[module.moduleType]++;
        }

        List<string> validTypes = new List<string>();

        foreach (var pair in typeCounts)
        {
            if (pair.Value >= requiredMatches)
                validTypes.Add(pair.Key);
        }

        if (validTypes.Count == 0)
            return "";

        return validTypes[Random.Range(0, validTypes.Count)];
    }

    private string PickRandomModuleType()
    {
        if (moduleTypes == null || moduleTypes.Length == 0)
            return "Max Speed";

        return moduleTypes[Random.Range(0, moduleTypes.Length)];
    }

    private void ConsumeSlottedModules()
    {
        foreach (ModuleSlotUI slot in researchSlots)
        {
            if (slot == null || slot.IsEmpty)
                continue;

            slot.ClearModule(false);
        }
    }

    private void RefreshConfirmButton()
    {
        if (confirmButton == null)
            return;

        confirmButton.interactable = GetFilledSlotCount() >= minModulesRequired;
    }

    public int GetFilledSlotCount()
    {
        int count = 0;

        foreach (ModuleSlotUI slot in researchSlots)
        {
            if (slot != null && !slot.IsEmpty)
                count++;
        }

        return count;
    }

    public List<ModuleData> GetSlottedModules()
    {
        List<ModuleData> modules = new List<ModuleData>();

        foreach (ModuleSlotUI slot in researchSlots)
        {
            if (slot != null && slot.EquippedModule != null)
                modules.Add(slot.EquippedModule);
        }

        return modules;
    }

    public void ClearResearchSlotsAndReturnModules()
    {
        foreach (ModuleSlotUI slot in researchSlots)
        {
            if (slot != null && !slot.IsEmpty)
                slot.ClearModule(true);
        }

        RefreshConfirmButton();
    }
}