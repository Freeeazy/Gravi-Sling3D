using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ModuleGenerator : MonoBehaviour
{
    public static ModuleGenerator Instance { get; private set; }

    [System.Serializable]
    public class StatRollRange
    {
        public string statName;

        [Header("Flat Roll")]
        public bool canRollFlat = true;
        public Vector2 flatRange = new Vector2(0f, 10f);

        [Header("Percent Roll")]
        public bool canRollPercent = false;

        [Tooltip("Use decimal values. 0.10 = 10%.")]
        public Vector2 percentRange = new Vector2(0f, 0.10f);
    }

    [System.Serializable]
    public class ModuleTypeConfig
    {
        public string moduleType = "Engine";
        public Sprite icon;

        [Header("Possible Stats For This Type")]
        public List<StatRollRange> possibleStats = new List<StatRollRange>();
    }

    [System.Serializable]
    public class TierConfig
    {
        public int tier = 0;

        [Header("Roll Counts")]
        public int minStats = 1;
        public int maxStats = 1;

        [Header("Stat Scaling")]
        public float flatMultiplier = 1f;
        public float percentMultiplier = 1f;

        [Header("Percent Chance")]
        [Range(0f, 1f)]
        public float percentRollChance = 0f;
    }

    [Header("Generated Asset Folder")]
    public string generatedAssetFolder = "Assets/GeneratedModules";

    [Header("Module Types")]
    public List<ModuleTypeConfig> moduleTypes = new List<ModuleTypeConfig>();

    [Header("Tier Configs")]
    public List<TierConfig> tierConfigs = new List<TierConfig>();

    [Header("Name Parts")]
    public string[] prefixes =
    {
        "Jury-Rigged",
        "Ion-Bent",
        "Overclocked",
        "Stabilized",
        "Black Market",
        "Prototype",
        "Rustline",
        "Courier-Grade"
    };

    public string[] suffixes =
    {
        "Booster",
        "Capacitor",
        "Regulator",
        "Drive",
        "Core",
        "Stabilizer",
        "Accelerator",
        "Module"
    };

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public ModuleData GenerateModule(string moduleType, int tier, bool saveAssetInEditor = true)
    {
        ModuleTypeConfig typeConfig = GetModuleTypeConfig(moduleType);
        TierConfig tierConfig = GetTierConfig(tier);

        if (typeConfig == null)
        {
            Debug.LogWarning($"[ModuleGenerator] No ModuleTypeConfig found for type: {moduleType}");
            return null;
        }

        if (tierConfig == null)
        {
            Debug.LogWarning($"[ModuleGenerator] No TierConfig found for tier: {tier}");
            return null;
        }

        ModuleData module = ScriptableObject.CreateInstance<ModuleData>();

        module.moduleType = typeConfig.moduleType;
        module.moduleTier = tierConfig.tier;
        module.icon = typeConfig.icon;

        string statSignature = RollStats(module, typeConfig, tierConfig);

        module.moduleName = GenerateModuleName(typeConfig.moduleType, tierConfig.tier, statSignature);

#if UNITY_EDITOR
        if (saveAssetInEditor)
            SaveGeneratedAsset(module);
#endif

        return module;
    }

    public ModuleData GenerateRandomModuleByTier(int tier, bool saveAssetInEditor = true)
    {
        if (moduleTypes == null || moduleTypes.Count == 0)
        {
            Debug.LogWarning("[ModuleGenerator] Cannot generate random module. No module types configured.");
            return null;
        }

        TierConfig tierConfig = GetTierConfig(tier);

        if (tierConfig == null)
        {
            Debug.LogWarning($"[ModuleGenerator] No TierConfig found for tier: {tier}");
            return null;
        }

        List<ModuleTypeConfig> validTypes = new List<ModuleTypeConfig>();

        for (int i = 0; i < moduleTypes.Count; i++)
        {
            ModuleTypeConfig typeConfig = moduleTypes[i];

            if (typeConfig == null || string.IsNullOrWhiteSpace(typeConfig.moduleType))
                continue;

            validTypes.Add(typeConfig);
        }

        if (validTypes.Count == 0)
        {
            Debug.LogWarning("[ModuleGenerator] Cannot generate random module. No valid module types configured.");
            return null;
        }

        ModuleTypeConfig randomType = validTypes[Random.Range(0, validTypes.Count)];
        return GenerateModule(randomType.moduleType, tierConfig.tier, saveAssetInEditor);
    }

    public List<ModuleData> GenerateModules(string moduleType, int tier, int amount, bool saveAssetInEditor = true)
    {
        List<ModuleData> generatedModules = new List<ModuleData>();

        for (int i = 0; i < amount; i++)
        {
            ModuleData module = GenerateModule(moduleType, tier, saveAssetInEditor);

            if (module != null)
                generatedModules.Add(module);
        }

        return generatedModules;
    }

    private string RollStats(ModuleData module, ModuleTypeConfig typeConfig, TierConfig tierConfig)
    {
        if (typeConfig.possibleStats == null || typeConfig.possibleStats.Count == 0)
        {
            Debug.LogWarning($"[ModuleGenerator] Module type {typeConfig.moduleType} has no possible stats.");
            return "Empty";
        }

        int statCount = Random.Range(tierConfig.minStats, tierConfig.maxStats + 1);
        statCount = Mathf.Clamp(statCount, 1, typeConfig.possibleStats.Count);

        List<StatRollRange> availableStats = new List<StatRollRange>(typeConfig.possibleStats);
        List<string> statSignatureParts = new List<string>();

        for (int i = 0; i < statCount; i++)
        {
            if (availableStats.Count == 0)
                break;

            int randomIndex = Random.Range(0, availableStats.Count);
            StatRollRange chosenStat = availableStats[randomIndex];
            availableStats.RemoveAt(randomIndex);

            bool rollPercent =
                chosenStat.canRollPercent &&
                Random.value <= tierConfig.percentRollChance;

            if (rollPercent)
            {
                float percentValue = Random.Range(chosenStat.percentRange.x, chosenStat.percentRange.y);
                percentValue *= tierConfig.percentMultiplier;

                // Optional: keeps percent naming more consistent/readable.
                percentValue = Mathf.Round(percentValue * 100f) / 100f;

                ApplyStat(module, chosenStat.statName, percentValue, true);

                statSignatureParts.Add($"{chosenStat.statName}_{Mathf.RoundToInt(percentValue * 100f)}P");
            }
            else if (chosenStat.canRollFlat)
            {
                float flatValue = Random.Range(chosenStat.flatRange.x, chosenStat.flatRange.y);
                flatValue *= tierConfig.flatMultiplier;

                // Keeps flat values clean like +5, +10, +50 instead of +7.3842.
                flatValue = Mathf.Round(flatValue);

                ApplyStat(module, chosenStat.statName, flatValue, false);

                statSignatureParts.Add($"{chosenStat.statName}_{Mathf.RoundToInt(flatValue)}F");
            }
        }

        statSignatureParts.Sort();

        return string.Join("_", statSignatureParts);
    }

    private void ApplyStat(ModuleData module, string statName, float value, bool isPercent)
    {
        switch (statName)
        {
            case "ChargeRate":
                if (isPercent) module.chargeRateBonus_Percent += value;
                else module.chargeRateBonus += value;
                break;

            case "LaunchSpeed":
                if (isPercent) module.baseLaunchSpeedBonus_Percent += value;
                else module.baseLaunchSpeedBonus += value;
                break;

            case "MaxSpeed":
                if (isPercent) module.maxSpeedBonus_Percent += value;
                else module.maxSpeedBonus += value;
                break;

            case "Acceleration":
                if (isPercent) module.accelerationBonus_Percent += value;
                else module.accelerationBonus += value;
                break;

            case "BoostAcceleration":
                if (isPercent) module.boostAccelAddBonus_Percent += value;
                else module.boostAccelAddBonus += value;
                break;

            case "BoostMaxSpeed":
                if (isPercent) module.boostMaxBonus_Percent += value;
                else module.boostMaxBonus += value;
                break;

            case "BoostCapacity":
                if (isPercent) module.capacityBonus_Percent += value;
                else module.capacityBonus += value;
                break;

            case "BoostDrain":
                if (isPercent) module.drainPerSecondBonus_Percent += value;
                else module.drainPerSecondBonus += value;
                break;

            case "BoostRegen":
                if (isPercent) module.regenPerSecondBonus_Percent += value;
                else module.regenPerSecondBonus += value;
                break;

            case "ShieldCharge":
                if (isPercent)
                    Debug.LogWarning("[ModuleGenerator] ShieldCharge does not currently support percent rolls. Applying it as a flat bonus.");
                module.shieldChargeBonus += value;
                break;

            case "PackagePlating":
                if (isPercent) 
                    Debug.LogWarning("[ModuleGenerator] PackagePlating does not currently support percent rolls.");
                module.packagePlatingBonus += value;
                break;

            default:
                Debug.LogWarning($"[ModuleGenerator] Unknown stat name: {statName}");
                break;
        }
    }

    private string GenerateModuleName(string moduleType, int tier, string statSignature)
    {
        int seed = $"{moduleType}_T{tier}_{statSignature}".GetHashCode();

        System.Random seededRandom = new System.Random(seed);

        string prefix = prefixes.Length > 0
            ? prefixes[seededRandom.Next(0, prefixes.Length)]
            : "Generated";

        string suffix = suffixes.Length > 0
            ? suffixes[seededRandom.Next(0, suffixes.Length)]
            : "Module";

        return $"{prefix} {moduleType} {suffix}";
    }

    private ModuleTypeConfig GetModuleTypeConfig(string moduleType)
    {
        for (int i = 0; i < moduleTypes.Count; i++)
        {
            if (moduleTypes[i].moduleType == moduleType)
                return moduleTypes[i];
        }

        return null;
    }

    private TierConfig GetTierConfig(int tier)
    {
        for (int i = 0; i < tierConfigs.Count; i++)
        {
            if (tierConfigs[i].tier == tier)
                return tierConfigs[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void SaveGeneratedAsset(ModuleData module)
    {
        if (module == null)
            return;

        EnsureFolderExists(generatedAssetFolder);

        string safeName = MakeSafeAssetName(module.moduleName);
        string path = $"{generatedAssetFolder}/T{module.moduleTier}_{module.moduleType}_{safeName}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        AssetDatabase.CreateAsset(module, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ModuleGenerator] Saved generated module asset: {path}");
    }

    private void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');

        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    private string MakeSafeAssetName(string rawName)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            rawName = rawName.Replace(c, '_');

        return rawName.Replace(" ", "_");
    }
#endif
}