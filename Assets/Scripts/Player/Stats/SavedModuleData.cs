using System;
using UnityEngine;

// Plain values that JsonUtility can persist across game sessions.
// The sprite is resolved by name from the inventory's inspector catalog.
[Serializable]
public class SavedModuleData
{
    public int amount = 1;
    public string iconName;
    public string moduleName;
    public string moduleType;
    public int moduleTier;
    public float chargeRateBonus;
    public float chargeRateBonus_Percent;
    public float baseLaunchSpeedBonus;
    public float baseLaunchSpeedBonus_Percent;
    public float maxSpeedBonus;
    public float maxSpeedBonus_Percent;
    public float accelerationBonus;
    public float accelerationBonus_Percent;
    public float boostAccelAddBonus;
    public float boostAccelAddBonus_Percent;
    public float boostMaxBonus;
    public float boostMaxBonus_Percent;
    public float capacityBonus;
    public float capacityBonus_Percent;
    public float drainPerSecondBonus;
    public float drainPerSecondBonus_Percent;
    public float regenPerSecondBonus;
    public float regenPerSecondBonus_Percent;
    public float shieldChargeBonus;
    public float packagePlatingBonus;

    // Used by equipped modules. Inventory modules don't need a slot index.
    public int slotIndex = -1;

    // Reuse an existing entry when capturing each frame.
    public void Capture(ModuleData module, int stackAmount, string savedIconName)
    {
        amount = stackAmount;
        iconName = savedIconName;
        moduleName = module.moduleName;
        moduleType = module.moduleType;
        moduleTier = module.moduleTier;
        chargeRateBonus = module.chargeRateBonus;
        chargeRateBonus_Percent = module.chargeRateBonus_Percent;
        baseLaunchSpeedBonus = module.baseLaunchSpeedBonus;
        baseLaunchSpeedBonus_Percent = module.baseLaunchSpeedBonus_Percent;
        maxSpeedBonus = module.maxSpeedBonus;
        maxSpeedBonus_Percent = module.maxSpeedBonus_Percent;
        accelerationBonus = module.accelerationBonus;
        accelerationBonus_Percent = module.accelerationBonus_Percent;
        boostAccelAddBonus = module.boostAccelAddBonus;
        boostAccelAddBonus_Percent = module.boostAccelAddBonus_Percent;
        boostMaxBonus = module.boostMaxBonus;
        boostMaxBonus_Percent = module.boostMaxBonus_Percent;
        capacityBonus = module.capacityBonus;
        capacityBonus_Percent = module.capacityBonus_Percent;
        drainPerSecondBonus = module.drainPerSecondBonus;
        drainPerSecondBonus_Percent = module.drainPerSecondBonus_Percent;
        regenPerSecondBonus = module.regenPerSecondBonus;
        regenPerSecondBonus_Percent = module.regenPerSecondBonus_Percent;
        shieldChargeBonus = module.shieldChargeBonus;
        packagePlatingBonus = module.packagePlatingBonus;
    }

    public ModuleData Recreate(Sprite restoredIcon)
    {
        ModuleData module = ScriptableObject.CreateInstance<ModuleData>();
        module.name = moduleName;
        module.icon = restoredIcon;
        module.moduleName = moduleName;
        module.moduleType = moduleType;
        module.moduleTier = moduleTier;
        module.chargeRateBonus = chargeRateBonus;
        module.chargeRateBonus_Percent = chargeRateBonus_Percent;
        module.baseLaunchSpeedBonus = baseLaunchSpeedBonus;
        module.baseLaunchSpeedBonus_Percent = baseLaunchSpeedBonus_Percent;
        module.maxSpeedBonus = maxSpeedBonus;
        module.maxSpeedBonus_Percent = maxSpeedBonus_Percent;
        module.accelerationBonus = accelerationBonus;
        module.accelerationBonus_Percent = accelerationBonus_Percent;
        module.boostAccelAddBonus = boostAccelAddBonus;
        module.boostAccelAddBonus_Percent = boostAccelAddBonus_Percent;
        module.boostMaxBonus = boostMaxBonus;
        module.boostMaxBonus_Percent = boostMaxBonus_Percent;
        module.capacityBonus = capacityBonus;
        module.capacityBonus_Percent = capacityBonus_Percent;
        module.drainPerSecondBonus = drainPerSecondBonus;
        module.drainPerSecondBonus_Percent = drainPerSecondBonus_Percent;
        module.regenPerSecondBonus = regenPerSecondBonus;
        module.regenPerSecondBonus_Percent = regenPerSecondBonus_Percent;
        module.shieldChargeBonus = shieldChargeBonus;
        module.packagePlatingBonus = packagePlatingBonus;
        return module;
    }
}
