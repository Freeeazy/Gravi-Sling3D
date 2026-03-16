using System.Collections.Generic;
using UnityEngine;

public class ModuleLoadoutManager : MonoBehaviour
{
    public static ModuleLoadoutManager Instance { get; private set; }

    public List<ModuleSlotUI> slots = new List<ModuleSlotUI>();

    [Header("Base Stats")]
    public float baseChargeRate = 60f;
    public float baseMaxSpeed = 200f;
    public float baseAcceleration = 100f;

    private void Awake()
    {
        Instance = this;
    }

    public void RecalculateStats()
    {
        float chargeBonus = 0f;
        float speedBonus = 0f;
        float accelBonus = 0f;

        foreach (var slot in slots)
        {
            if (slot == null || slot.EquippedModule == null)
                continue;

            chargeBonus += slot.EquippedModule.chargeRateBonus;
            speedBonus += slot.EquippedModule.maxSpeedBonus;
            accelBonus += slot.EquippedModule.accelerationBonus;
        }

        float finalChargeRate = baseChargeRate + chargeBonus;
        float finalMaxSpeed = baseMaxSpeed + speedBonus;
        float finalAcceleration = baseAcceleration + accelBonus;

        if (StatManager.Instance != null)
        {
            StatManager.Instance.SetOrbitChargeRate(finalChargeRate);
            StatManager.Instance.SetMaxSpeed(finalMaxSpeed);
            StatManager.Instance.SetAcceleration(finalAcceleration);
            StatManager.Instance.ApplyRuntimeStats();
        }
    }
}