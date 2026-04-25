using System.Collections.Generic;
using UnityEngine;

public class ModuleLoadoutManager : MonoBehaviour
{
    public static ModuleLoadoutManager Instance { get; private set; }

    public List<ModuleSlotUI> slots = new List<ModuleSlotUI>();

    [Header("Station Stats")]
    public float baseChargeRate = 60f;

    [Header("Player Movement Stats")]
    public float baseMaxSpeed = 200f;
    public float baseAcceleration = 100f;
    public float baseBoostAccelAdd = 50f;

    [Header("Boost Stats")]
    public float baseBoostMaxSpeed = 900f;
    public float baseCapacity = 100f;
    public float baseDrainPerSecond = 18f;
    public float baseRegenPerSecond = 10f;

    private void Awake()
    {
        Instance = this;
    }

    public void RecalculateStats()
    {
        float chargeBonus = 0f;

        float speedBonus = 0f;
        float accelBonus = 0f;
        float boostAccelAddBonus = 0f;

        float boostMaxSpeedBonus = 0f;
        float capBonus = 0f;
        float drainPerSecBonus = 0f;
        float regenPerSecBonus = 0f;

        foreach (var slot in slots)
        {
            if (slot == null || slot.EquippedModule == null)
                continue;

            chargeBonus += slot.EquippedModule.chargeRateBonus;

            speedBonus += slot.EquippedModule.maxSpeedBonus;
            accelBonus += slot.EquippedModule.accelerationBonus;
            boostAccelAddBonus += slot.EquippedModule.boostAccelAddBonus;

            boostMaxSpeedBonus += slot.EquippedModule.boostMaxBonus;
            capBonus += slot.EquippedModule.capacityBonus;
            drainPerSecBonus += slot.EquippedModule.drainPerSecondBonus;
            regenPerSecBonus += slot.EquippedModule.regenPerSecondBonus;
        }

        //  Orbit
        float finalChargeRate = baseChargeRate + chargeBonus;

        //  Player Movement
        float finalMaxSpeed = baseMaxSpeed + speedBonus;
        float finalAcceleration = baseAcceleration + accelBonus;
        float finalBoostAccelAdd = baseBoostAccelAdd + boostAccelAddBonus;

        //  Boosting
        float finalBoostMaxSpeed = baseBoostMaxSpeed + boostMaxSpeedBonus;
        float finalCapacity = baseCapacity + capBonus;
        float finalDrainPerSecond = baseDrainPerSecond + drainPerSecBonus;
        float finalRegenPerSecond = baseRegenPerSecond + regenPerSecBonus;

        if (StatManager.Instance != null)
        {
            StatManager.Instance.SetOrbitChargeRate(finalChargeRate);

            StatManager.Instance.SetMaxSpeed(finalMaxSpeed);
            StatManager.Instance.SetAcceleration(finalAcceleration);
            StatManager.Instance.SetBoostAccelAdd(finalBoostAccelAdd);

            StatManager.Instance.SetBoostMaxSpeed(finalBoostMaxSpeed);
            StatManager.Instance.SetCapacity(finalCapacity);
            StatManager.Instance.SetDrainPerSecond(finalDrainPerSecond);
            StatManager.Instance.SetRegenPerSecond(finalRegenPerSecond);

            StatManager.Instance.ApplyRuntimeStats();
        }
    }
}