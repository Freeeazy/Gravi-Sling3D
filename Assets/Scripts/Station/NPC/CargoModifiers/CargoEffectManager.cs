using System.Collections.Generic;
using UnityEngine;

public class CargoEffectManager : MonoBehaviour
{
    public static CargoEffectManager Instance { get; private set; }

    [Header("Refs")]
    public PackageDurabilityManager packageDurabilityManager;

    [Header("Super Heavy")]
    public float superHeavyMaxSpeedMultiplier = 0.75f;
    public float superHeavyAccelerationMultiplier = 0.60f;
    public float superHeavyBoostAccelMultiplier = 0.75f;
    public float superHeavyBoostMaxSpeedMultiplier = 0.80f;
    public float superHeavyBoostDrainMultiplier = 1.25f;

    [Header("Ultra Light")]
    public float ultraLightMaxSpeedMultiplier = 1.15f;
    public float ultraLightAccelerationMultiplier = 1.25f;
    public float ultraLightBoostAccelMultiplier = 1.20f;
    public float ultraLightBoostMaxSpeedMultiplier = 1.10f;
    public float ultraLightBoostDrainMultiplier = 1.10f;

    private readonly Dictionary<int, CargoEffectType> _activeCargoEffects = new Dictionary<int, CargoEffectType>();
    private bool _subscribedToPackageManager;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        TrySubscribeToPackageManager();
    }

    private void Start()
    {
        TrySubscribeToPackageManager();
    }

    private void TrySubscribeToPackageManager()
    {
        if (_subscribedToPackageManager)
            return;

        if (packageDurabilityManager == null)
            packageDurabilityManager = PackageDurabilityManager.Instance;

        if (packageDurabilityManager != null)
        {
            packageDurabilityManager.PackageCargoStarted += HandlePackageCargoStarted;
            packageDurabilityManager.PackageCargoEnded += HandlePackageCargoEnded;
            _subscribedToPackageManager = true;
        }
    }

    private void OnDisable()
    {
        if (packageDurabilityManager != null)
        {
            packageDurabilityManager.PackageCargoStarted -= HandlePackageCargoStarted;
            packageDurabilityManager.PackageCargoEnded -= HandlePackageCargoEnded;
        }

        _subscribedToPackageManager = false;
        _activeCargoEffects.Clear();

        if (StatManager.Instance != null)
            StatManager.Instance.ClearCargoStatMultipliers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandlePackageCargoStarted(int questId, CargoEffectType effectType)
    {
        if (effectType == CargoEffectType.None)
            return;

        _activeCargoEffects[questId] = effectType;
        ApplyActiveCargoEffects();
    }

    private void HandlePackageCargoEnded(int questId, CargoEffectType effectType)
    {
        if (_activeCargoEffects.Remove(questId))
            ApplyActiveCargoEffects();
    }

    private void ApplyActiveCargoEffects()
    {
        float maxSpeedMultiplier = 1f;
        float accelerationMultiplier = 1f;
        float boostAccelMultiplier = 1f;
        float boostMaxSpeedMultiplier = 1f;
        float capacityMultiplier = 1f;
        float drainPerSecondMultiplier = 1f;
        float regenPerSecondMultiplier = 1f;

        foreach (CargoEffectType effectType in _activeCargoEffects.Values)
        {
            switch (effectType)
            {
                case CargoEffectType.SuperHeavy:
                    maxSpeedMultiplier *= superHeavyMaxSpeedMultiplier;
                    accelerationMultiplier *= superHeavyAccelerationMultiplier;
                    boostAccelMultiplier *= superHeavyBoostAccelMultiplier;
                    boostMaxSpeedMultiplier *= superHeavyBoostMaxSpeedMultiplier;
                    drainPerSecondMultiplier *= superHeavyBoostDrainMultiplier;
                    break;

                case CargoEffectType.UltraLight:
                    maxSpeedMultiplier *= ultraLightMaxSpeedMultiplier;
                    accelerationMultiplier *= ultraLightAccelerationMultiplier;
                    boostAccelMultiplier *= ultraLightBoostAccelMultiplier;
                    boostMaxSpeedMultiplier *= ultraLightBoostMaxSpeedMultiplier;
                    drainPerSecondMultiplier *= ultraLightBoostDrainMultiplier;
                    break;
            }
        }

        if (StatManager.Instance != null)
        {
            StatManager.Instance.SetCargoStatMultipliers(
                maxSpeedMultiplier,
                accelerationMultiplier,
                boostAccelMultiplier,
                boostMaxSpeedMultiplier,
                capacityMultiplier,
                drainPerSecondMultiplier,
                regenPerSecondMultiplier
            );
        }
    }
}
