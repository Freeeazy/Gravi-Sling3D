using System.Collections.Generic;
using UnityEngine;

public class ModuleLoadoutManager : MonoBehaviour
{
    public static ModuleLoadoutManager Instance { get; private set; }

    public List<ModuleSlotUI> slots = new List<ModuleSlotUI>();

    [Header("Station Stats")]
    public float baseChargeRate = 60f;
    public float baseLaunchSpeed = 80f;

    [Header("Player Movement Stats")]
    public float baseMaxSpeed = 200f;
    public float baseAcceleration = 100f;
    public float baseBoostAccelAdd = 50f;

    [Header("Boost Stats")]
    public float baseBoostMaxSpeed = 900f;
    public float baseCapacity = 100f;
    public float baseDrainPerSecond = 18f;
    public float baseRegenPerSecond = 10f;

    [Header("Unique Stats")]
    public float baseShieldCharge = 0;
    public float basePackagePlating = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void RecalculateStats()
    {
        // Flat bonuses
        float chargeBonus = 0f;
        float launchBonus = 0f;

        float speedBonus = 0f;
        float accelBonus = 0f;
        float boostAccelAddBonus = 0f;

        float boostMaxSpeedBonus = 0f;
        float capacityBonus = 0f;
        float drainPerSecBonus = 0f;
        float regenPerSecBonus = 0f;

        float shieldChargeBonus = 0;
        float packagePlatingBonus = 0;

        // Percent bonuses
        float chargeBonus_Percent = 0f;
        float launchBonus_Percent = 0f;

        float speedBonus_Percent = 0f;
        float accelBonus_Percent = 0f;
        float boostAccelAddBonus_Percent = 0f;

        float boostMaxSpeedBonus_Percent = 0f;
        float capacityBonus_Percent = 0f;
        float drainPerSecBonus_Percent = 0f;
        float regenPerSecBonus_Percent = 0f;

        foreach (var slot in slots)
        {
            if (slot == null || slot.EquippedModule == null)
                continue;

            ModuleData module = slot.EquippedModule;

            // Flat bonuses
            chargeBonus += module.chargeRateBonus;
            launchBonus += module.baseLaunchSpeedBonus;

            speedBonus += module.maxSpeedBonus;
            accelBonus += module.accelerationBonus;
            boostAccelAddBonus += module.boostAccelAddBonus;

            boostMaxSpeedBonus += module.boostMaxBonus;
            capacityBonus += module.capacityBonus;
            drainPerSecBonus += module.drainPerSecondBonus;
            regenPerSecBonus += module.regenPerSecondBonus;

            shieldChargeBonus += module.shieldChargeBonus;
            packagePlatingBonus += module.packagePlatingBonus;

            // Percent bonuses
            chargeBonus_Percent += module.chargeRateBonus_Percent;
            launchBonus_Percent += module.baseLaunchSpeedBonus_Percent;

            speedBonus_Percent += module.maxSpeedBonus_Percent;
            accelBonus_Percent += module.accelerationBonus_Percent;
            boostAccelAddBonus_Percent += module.boostAccelAddBonus_Percent;

            boostMaxSpeedBonus_Percent += module.boostMaxBonus_Percent;
            capacityBonus_Percent += module.capacityBonus_Percent;
            drainPerSecBonus_Percent += module.drainPerSecondBonus_Percent;
            regenPerSecBonus_Percent += module.regenPerSecondBonus_Percent;
        }

        // Station stats
        float finalChargeRate = ApplyFlatAndPercent(baseChargeRate, chargeBonus, chargeBonus_Percent);
        float finalLaunchSpeed = ApplyFlatAndPercent(baseLaunchSpeed, launchBonus, launchBonus_Percent);

        // Player movement stats
        float finalMaxSpeed = ApplyFlatAndPercent(baseMaxSpeed, speedBonus, speedBonus_Percent);
        float finalAcceleration = ApplyFlatAndPercent(baseAcceleration, accelBonus, accelBonus_Percent);
        float finalBoostAccelAdd = ApplyFlatAndPercent(baseBoostAccelAdd, boostAccelAddBonus, boostAccelAddBonus_Percent);

        // Boosting stats
        float finalBoostMaxSpeed = ApplyFlatAndPercent(baseBoostMaxSpeed, boostMaxSpeedBonus, boostMaxSpeedBonus_Percent);
        float finalCapacity = ApplyFlatAndPercent(baseCapacity, capacityBonus, capacityBonus_Percent);
        float finalDrainPerSecond = ApplyFlatAndPercent(baseDrainPerSecond, drainPerSecBonus, drainPerSecBonus_Percent);
        float finalRegenPerSecond = ApplyFlatAndPercent(baseRegenPerSecond, regenPerSecBonus, regenPerSecBonus_Percent);

        float finalShieldCharge = baseShieldCharge + shieldChargeBonus;
        float finalPackagePlating = basePackagePlating + packagePlatingBonus;

        if (StatManager.Instance != null)
        {
            StatManager.Instance.SetOrbitChargeRate(finalChargeRate);
            StatManager.Instance.SetBaseLaunchSpeed(finalLaunchSpeed);

            StatManager.Instance.SetMaxSpeed(finalMaxSpeed);
            StatManager.Instance.SetAcceleration(finalAcceleration);
            StatManager.Instance.SetBoostAccelAdd(finalBoostAccelAdd);

            StatManager.Instance.SetBoostMaxSpeed(finalBoostMaxSpeed);
            StatManager.Instance.SetCapacity(finalCapacity);
            StatManager.Instance.SetDrainPerSecond(finalDrainPerSecond);
            StatManager.Instance.SetRegenPerSecond(finalRegenPerSecond);

            StatManager.Instance.SetShieldCharge(finalShieldCharge);
            StatManager.Instance.SetPackagePlating(finalPackagePlating);

            StatManager.Instance.ApplyRuntimeStats();
        }
    }
    private float ApplyFlatAndPercent(float baseValue, float flatBonus, float percentBonus)
    {
        return (baseValue + flatBonus) * (1f + percentBonus);
    }
    public bool TryEquipToFirstEmptySlot(ModuleData moduleData)
    {
        if (moduleData == null)
            return false;

        foreach (var slot in slots)
        {
            if (slot == null)
                continue;

            if (!slot.IsEmpty)
                continue;

            slot.SetModule(moduleData);

            if (ModuleInventoryManager.Instance != null)
                ModuleInventoryManager.Instance.RemoveModule(moduleData, 1);

            RecalculateStats();

            return true;
        }

        return false;
    }
    public void CaptureSavedLoadout(List<SavedModuleData> destination)
    {
        if (destination == null)
            throw new System.ArgumentNullException(nameof(destination));

        ModuleInventoryManager inventory = ModuleInventoryManager.Instance;

        int entryIndex = 0;

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            ModuleSlotUI slot = slots[slotIndex];

            if (slot == null || slot.EquippedModule == null)
                continue;

            ModuleData module = slot.EquippedModule;

            if (entryIndex == destination.Count)
                destination.Add(new SavedModuleData());
            else if (destination[entryIndex] == null)
                destination[entryIndex] = new SavedModuleData();

            string iconName = inventory != null
                ? inventory.GetSavedIconName(module)
                : module.icon != null ? module.icon.name : "";

            SavedModuleData saved = destination[entryIndex];

            saved.Capture(module, 1, iconName);
            saved.slotIndex = slotIndex;

            entryIndex++;
        }

        if (destination.Count > entryIndex)
        {
            destination.RemoveRange(
                entryIndex,
                destination.Count - entryIndex);
        }
    }

    public void RestoreSavedLoadout(List<SavedModuleData> savedModules)
    {
        ModuleInventoryManager inventory = ModuleInventoryManager.Instance;

        if (inventory == null)
        {
            Debug.LogWarning(
                "[Loadout] Cannot restore equipment without ModuleInventoryManager.");
            return;
        }

        // Clear equipment without returning anything to inventory.
        foreach (ModuleSlotUI slot in slots)
        {
            if (slot != null)
                slot.RestoreSavedModule(null);
        }

        if (savedModules != null)
        {
            foreach (SavedModuleData saved in savedModules)
            {
                if (saved == null || saved.amount <= 0)
                    continue;

                ModuleData module = inventory.RecreateSavedModule(saved);

                bool validSlot =
                    saved.slotIndex >= 0 &&
                    saved.slotIndex < slots.Count &&
                    slots[saved.slotIndex] != null &&
                    slots[saved.slotIndex].IsEmpty;

                if (validSlot)
                {
                    slots[saved.slotIndex].RestoreSavedModule(module);
                }
                else
                {
                    // Preserve the item if its original slot no longer exists
                    // or another saved entry already occupied that slot.
                    inventory.AddModule(module, 1);

                    Debug.LogWarning(
                        $"[Loadout] Saved slot {saved.slotIndex} is unavailable. " +
                        $"Returned '{module.moduleName}' to inventory.");
                }
            }
        }

        // Apply bonuses once, after the entire loadout is restored.
        RecalculateStats();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}