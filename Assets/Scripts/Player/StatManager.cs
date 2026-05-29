using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance { get; private set; }

    public SimpleMove simpleMove;

    [Header("Apply on startup")]
    [SerializeField] private bool applyOnAwake = true;

    [Header("Internal / Global Stats")]
    [SerializeField] private float orbitChargeRate = 60.0f;
    public TMP_Text orbitChargeRateText;
    [SerializeField] private float baseLaunchSpeed = 80.0f;
    public TMP_Text baseLaunchSpeedText;

    [SerializeField] private float maxSpeed = 400f;
    [SerializeField] private float acceleration = 100f;
    [SerializeField] private float boostAccelAdd = 50f;

    [SerializeField] private float boostMaxSpeed = 900f;
    [SerializeField] private float capacity = 100f;
    [SerializeField] private float drainPerSecond = 18f;
    [SerializeField] private float regenPerSecond = 10f;

    [Header("Hidden / Unique Stats")]
    [SerializeField] private float shieldCharge = 0;
    private int currentShieldCharges;
    public TMP_Text shieldChargeText;

    public int MaxShieldCharges => Mathf.Max(0, Mathf.FloorToInt(shieldCharge));
    public int CurrentShieldCharges => currentShieldCharges;

    [Header("Stat Targets")]
    [SerializeField] private List<ScriptStatTarget> targets = new List<ScriptStatTarget>();

    private const BindingFlags FIELD_FLAGS =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public void SetOrbitChargeRate(float value) => orbitChargeRate = value;
    public void SetBaseLaunchSpeed(float value) => baseLaunchSpeed = value;


    public void SetMaxSpeed(float value) => maxSpeed = value;
    public void SetAcceleration(float value) => acceleration = value;
    public void SetBoostAccelAdd(float value) => boostAccelAdd = value;


    public void SetBoostMaxSpeed(float value) => boostMaxSpeed = value;
    public void SetCapacity(float value) => capacity = value;
    public void SetDrainPerSecond(float value) => drainPerSecond = value;
    public void SetRegenPerSecond(float value) => regenPerSecond = value;


    public float GetOrbitChargeRate() => orbitChargeRate;
    public float GetBaseLaunchSpeed() => baseLaunchSpeed;


    public float GetMaxSpeed() => maxSpeed;
    public float GetAcceleration() => acceleration;
    public float GetBoostAccelAdd() => boostAccelAdd;

    public float GetBoostMaxSpeed() => boostMaxSpeed;
    public float GetCapacity() => capacity;
    public float GetDrainPerSecond() => drainPerSecond;
    public float GetRegenPerSecond() => regenPerSecond;


    // Unique Stats
    public void SetShieldCharge(float value)
    {
        shieldCharge = value;
        currentShieldCharges = Mathf.Clamp(currentShieldCharges, 0, MaxShieldCharges);
        if (ShieldVisualHelper.Instance != null)
            ShieldVisualHelper.Instance.UpdateShieldVisual(currentShieldCharges);
    }

    public bool TryConsumeShieldCharge()
    {
        if (currentShieldCharges <= 0)
            return false;

        currentShieldCharges--;
        if (ShieldVisualHelper.Instance != null)
            ShieldVisualHelper.Instance.UpdateShieldVisual(currentShieldCharges);
        return true;
    }

    public void RefillShieldCharges()
    {
        currentShieldCharges = MaxShieldCharges;
        if (ShieldVisualHelper.Instance != null)
            ShieldVisualHelper.Instance.UpdateShieldVisual(currentShieldCharges);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (applyOnAwake)
            ApplyAllStats();
    }

    public void ApplyRuntimeStats()
    {
        if (simpleMove != null)
        {
            simpleMove.maxSpeed = maxSpeed;
            simpleMove.acceleration = acceleration;
            simpleMove.boostMaxSpeed = boostMaxSpeed;
            simpleMove.boostAccelAdd = boostAccelAdd;
        }

        if (BoostManager.Instance != null)
        {
            BoostManager.Instance.SetCapacity(capacity);
            BoostManager.Instance.SetDrainPerSecond(drainPerSecond);
            BoostManager.Instance.SetRegenPerSecond(regenPerSecond);
        }

        RefreshAllStatDisplays();
    }

    [ContextMenu("Apply All Stats")]
    public void ApplyAllStats()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            ApplyTarget(targets[i]);
        }
        if (orbitChargeRateText)
            orbitChargeRateText.text = FormatWholeStat(orbitChargeRate);

        if (baseLaunchSpeedText)
            baseLaunchSpeedText.text = FormatWholeStat(baseLaunchSpeed);

        if (shieldChargeText)
            shieldChargeText.text = FormatWholeStat(shieldCharge);
    }

    public void ApplyTargetByName(string targetName)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].targetName == targetName)
            {
                ApplyTarget(targets[i]);
                return;
            }
        }

        Debug.LogWarning($"StatManager: No target found with name '{targetName}'.", this);
    }

    public void SetStatValue(string targetName, string statName, string newValue, bool applyImmediately = true)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].targetName != targetName)
                continue;

            for (int j = 0; j < targets[i].stats.Count; j++)
            {
                if (targets[i].stats[j].fieldName == statName)
                {
                    targets[i].stats[j].value = newValue;

                    if (applyImmediately)
                        ApplyTarget(targets[i]);
                    else
                        UpdateStatDisplayOnly(targets[i].stats[j]);

                    return;
                }
            }

            Debug.LogWarning($"StatManager: Target '{targetName}' exists, but stat '{statName}' was not found.", this);
            return;
        }

        Debug.LogWarning($"StatManager: No target found with name '{targetName}'.", this);
    }

    private void ApplyTarget(ScriptStatTarget target)
    {
        if (target == null || target.targetScript == null)
        {
            Debug.LogWarning("StatManager: Target or targetScript is null.", this);
            return;
        }

        Type targetType = target.targetScript.GetType();

        for (int i = 0; i < target.stats.Count; i++)
        {
            StatEntry entry = target.stats[i];

            if (string.IsNullOrWhiteSpace(entry.fieldName))
                continue;

            FieldInfo field = targetType.GetField(entry.fieldName, FIELD_FLAGS);

            if (field == null)
            {
                Debug.LogWarning(
                    $"StatManager: Field '{entry.fieldName}' was not found on script '{targetType.Name}'.",
                    target.targetScript
                );
                continue;
            }

            if (TryConvertValue(entry.value, field.FieldType, out object convertedValue))
            {
                field.SetValue(target.targetScript, convertedValue);

                // Read back actual value from the script and display it
                object liveValue = field.GetValue(target.targetScript);
                UpdateStatDisplay(entry, liveValue);
            }
            else
            {
                Debug.LogWarning(
                    $"StatManager: Could not convert value '{entry.value}' to type '{field.FieldType.Name}' " +
                    $"for field '{entry.fieldName}' on script '{targetType.Name}'.",
                    target.targetScript
                );
            }
        }
    }

    private void UpdateStatDisplayOnly(StatEntry entry)
    {
        if (entry == null || entry.valueText == null)
            return;

        entry.valueText.text = $"{entry.value}";
    }

    private void UpdateStatDisplay(StatEntry entry, object liveValue)
    {
        if (entry == null || entry.valueText == null)
            return;

        entry.valueText.text = $"{FormatValue(liveValue, entry)}";
    }

    private string FormatValue(object value, StatEntry entry)
    {
        if (value == null)
            return "null";

        if (value is float f)
            return f.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture);

        if (value is Vector2 v2)
            return $"({v2.x.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture)}, " +
                   $"{v2.y.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture)})";

        if (value is Vector3 v3)
            return $"({v3.x.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture)}, " +
                   $"{v3.y.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture)}, " +
                   $"{v3.z.ToString($"F{entry.decimalPlaces}", CultureInfo.InvariantCulture)})";

        return value.ToString();
    }

    private bool TryConvertValue(string rawValue, Type targetType, out object result)
    {
        result = null;

        try
        {
            if (targetType == typeof(string))
            {
                result = rawValue;
                return true;
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int intVal))
                {
                    result = intVal;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal))
                {
                    result = floatVal;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(rawValue, out bool boolVal))
                {
                    result = boolVal;
                    return true;
                }

                if (rawValue == "0")
                {
                    result = false;
                    return true;
                }
                if (rawValue == "1")
                {
                    result = true;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(Vector2))
            {
                string[] parts = rawValue.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float y))
                {
                    result = new Vector2(x, y);
                    return true;
                }
                return false;
            }

            if (targetType == typeof(Vector3))
            {
                string[] parts = rawValue.Split(',');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float z))
                {
                    result = new Vector3(x, y, z);
                    return true;
                }
                return false;
            }

            if (targetType.IsEnum)
            {
                result = Enum.Parse(targetType, rawValue, true);
                return true;
            }

            result = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public void RefreshAllStatDisplays()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            ScriptStatTarget target = targets[i];

            if (target == null || target.targetScript == null)
                continue;

            Type targetType = target.targetScript.GetType();

            for (int j = 0; j < target.stats.Count; j++)
            {
                StatEntry entry = target.stats[j];
                if (string.IsNullOrWhiteSpace(entry.fieldName))
                    continue;

                FieldInfo field = targetType.GetField(entry.fieldName, FIELD_FLAGS);
                if (field == null)
                    continue;

                object liveValue = field.GetValue(target.targetScript);
                UpdateStatDisplay(entry, liveValue);
            }
        }
        if (orbitChargeRateText)
            orbitChargeRateText.text = FormatWholeStat(orbitChargeRate);

        if (baseLaunchSpeedText)
            baseLaunchSpeedText.text = FormatWholeStat(baseLaunchSpeed);

        if (shieldChargeText)
            shieldChargeText.text = FormatWholeStat(shieldCharge);
    }
    public void ShowModuleHoverPreview(ModuleData module)
    {
        if (module == null)
            return;

        // Start from clean/current values first.
        RefreshAllStatDisplays();

        // Manual stat text hookups
        SetPreviewText(
            orbitChargeRateText,
            orbitChargeRate,
            module.chargeRateBonus,
            module.chargeRateBonus_Percent,
            0,
            false
        );

        SetPreviewText(
            baseLaunchSpeedText,
            baseLaunchSpeed,
            module.baseLaunchSpeedBonus,
            module.baseLaunchSpeedBonus_Percent,
            0,
            false
        );

        // StatEntry / valueText hookups
        SetPreviewForField(
            "maxSpeed",
            maxSpeed,
            module.maxSpeedBonus,
            module.maxSpeedBonus_Percent,
            false
        );

        SetPreviewForField(
            "acceleration",
            acceleration,
            module.accelerationBonus,
            module.accelerationBonus_Percent,
            false
        );

        SetPreviewForField(
            "boostAccelAdd",
            boostAccelAdd,
            module.boostAccelAddBonus,
            module.boostAccelAddBonus_Percent,
            false
        );

        SetPreviewForField(
            "boostMaxSpeed",
            boostMaxSpeed,
            module.boostMaxBonus,
            module.boostMaxBonus_Percent,
            false
        );

        SetPreviewForField(
            "capacity",
            capacity,
            module.capacityBonus,
            module.capacityBonus_Percent,
            false
        );

        // Lower drain is good, so color logic is inverted here.
        SetPreviewForField(
            "drainPerSecond",
            drainPerSecond,
            module.drainPerSecondBonus,
            module.drainPerSecondBonus_Percent,
            true
        );

        SetPreviewForField(
            "regenPerSecond",
            regenPerSecond,
            module.regenPerSecondBonus,
            module.regenPerSecondBonus_Percent,
            false
        );

        SetPreviewText(
            shieldChargeText,
            shieldCharge,
            module.shieldChargeBonus,
            0,
            0,
            false
        );
    }

    public void ShowModuleReplacementPreview(ModuleData incomingModule, ModuleData equippedModule)
    {
        if (incomingModule == null)
            return;

        if (equippedModule == null)
        {
            ShowModuleHoverPreview(incomingModule);
            return;
        }

        // Start from clean/current values first.
        RefreshAllStatDisplays();

        SetPreviewText(
            orbitChargeRateText,
            orbitChargeRate,
            incomingModule.chargeRateBonus - equippedModule.chargeRateBonus,
            incomingModule.chargeRateBonus_Percent - equippedModule.chargeRateBonus_Percent,
            0,
            false
        );

        SetPreviewText(
            baseLaunchSpeedText,
            baseLaunchSpeed,
            incomingModule.baseLaunchSpeedBonus - equippedModule.baseLaunchSpeedBonus,
            incomingModule.baseLaunchSpeedBonus_Percent - equippedModule.baseLaunchSpeedBonus_Percent,
            0,
            false
        );

        SetPreviewForField(
            "maxSpeed",
            maxSpeed,
            incomingModule.maxSpeedBonus - equippedModule.maxSpeedBonus,
            incomingModule.maxSpeedBonus_Percent - equippedModule.maxSpeedBonus_Percent,
            false
        );

        SetPreviewForField(
            "acceleration",
            acceleration,
            incomingModule.accelerationBonus - equippedModule.accelerationBonus,
            incomingModule.accelerationBonus_Percent - equippedModule.accelerationBonus_Percent,
            false
        );

        SetPreviewForField(
            "boostAccelAdd",
            boostAccelAdd,
            incomingModule.boostAccelAddBonus - equippedModule.boostAccelAddBonus,
            incomingModule.boostAccelAddBonus_Percent - equippedModule.boostAccelAddBonus_Percent,
            false
        );

        SetPreviewForField(
            "boostMaxSpeed",
            boostMaxSpeed,
            incomingModule.boostMaxBonus - equippedModule.boostMaxBonus,
            incomingModule.boostMaxBonus_Percent - equippedModule.boostMaxBonus_Percent,
            false
        );

        SetPreviewForField(
            "capacity",
            capacity,
            incomingModule.capacityBonus - equippedModule.capacityBonus,
            incomingModule.capacityBonus_Percent - equippedModule.capacityBonus_Percent,
            false
        );

        // Lower drain is good, so color logic is inverted here.
        SetPreviewForField(
            "drainPerSecond",
            drainPerSecond,
            incomingModule.drainPerSecondBonus - equippedModule.drainPerSecondBonus,
            incomingModule.drainPerSecondBonus_Percent - equippedModule.drainPerSecondBonus_Percent,
            true
        );

        SetPreviewForField(
            "regenPerSecond",
            regenPerSecond,
            incomingModule.regenPerSecondBonus - equippedModule.regenPerSecondBonus,
            incomingModule.regenPerSecondBonus_Percent - equippedModule.regenPerSecondBonus_Percent,
            false
        );

        SetPreviewText(
            shieldChargeText,
            shieldCharge,
            incomingModule.shieldChargeBonus - equippedModule.shieldChargeBonus,
            0,
            0,
            false
        );
    }
    public void HideModuleHoverPreview()
    {
        RefreshAllStatDisplays();
    }

    private void SetPreviewForField(string fieldName, float currentValue, float flatBonus, float percentBonus, bool lowerIsBetter)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            ScriptStatTarget target = targets[i];

            if (target == null)
                continue;

            for (int j = 0; j < target.stats.Count; j++)
            {
                StatEntry entry = target.stats[j];

                if (entry == null || entry.valueText == null)
                    continue;

                if (entry.fieldName != fieldName)
                    continue;

                SetPreviewText(
                    entry.valueText,
                    currentValue,
                    flatBonus,
                    percentBonus,
                    entry.decimalPlaces,
                    lowerIsBetter
                );
            }
        }
    }

    private void SetPreviewText(TMP_Text text, float currentValue, float flatBonus, float percentBonus, int decimalPlaces, bool lowerIsBetter)
    {
        if (text == null)
            return;

        bool hasFlat = !Mathf.Approximately(flatBonus, 0f);
        bool hasPercent = !Mathf.Approximately(percentBonus, 0f);

        if (!hasFlat && !hasPercent)
            return;

        string currentText = currentValue.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        string previewPrefix = BuildPreviewPrefix(flatBonus, percentBonus, lowerIsBetter);

        text.text = $"{previewPrefix} {currentText}";
    }

    private string BuildPreviewPrefix(float flatBonus, float percentBonus, bool lowerIsBetter)
    {
        List<string> parts = new List<string>();

        if (!Mathf.Approximately(flatBonus, 0f))
            parts.Add(ColorizeModifier(FormatSignedFlat(flatBonus), flatBonus, lowerIsBetter));

        if (!Mathf.Approximately(percentBonus, 0f))
            parts.Add(ColorizeModifier(FormatSignedPercent(percentBonus), percentBonus, lowerIsBetter));

        return string.Join(" ", parts);
    }

    private string FormatSignedFlat(float value)
    {
        return $"{value:+0.#;-0.#;0}";
    }

    private string FormatSignedPercent(float value)
    {
        return $"{value * 100f:+0.#;-0.#;0}%";
    }

    private string ColorizeModifier(string text, float rawValue, bool lowerIsBetter)
    {
        bool isGood = lowerIsBetter ? rawValue < 0f : rawValue > 0f;

        string color = isGood ? "#4DFF88" : "#FF5A5A";

        return $"<color={color}>{text}</color>";
    }
    private string FormatWholeStat(float value)
    {
        return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
    }
}

[Serializable]
public class ScriptStatTarget
{
    public string targetName;
    public MonoBehaviour targetScript;
    public List<StatEntry> stats = new List<StatEntry>();
}

[Serializable]
public class StatEntry
{
    public string fieldName;
    public string value;

    [Header("Optional UI Display")]
    public string displayLabel;
    public TMP_Text valueText;
    [Range(0, 4)] public int decimalPlaces = 2;
}