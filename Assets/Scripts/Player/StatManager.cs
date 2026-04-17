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
    [SerializeField] private float maxSpeed = 400f;
    [SerializeField] private float acceleration = 100f;

    [Header("Stat Targets")]
    [SerializeField] private List<ScriptStatTarget> targets = new List<ScriptStatTarget>();

    private const BindingFlags FIELD_FLAGS =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public void SetOrbitChargeRate(float value) => orbitChargeRate = value;
    public void SetMaxSpeed(float value) => maxSpeed = value;
    public void SetAcceleration(float value) => acceleration = value;

    public float GetOrbitChargeRate() => orbitChargeRate;
    public float GetMaxSpeed() => maxSpeed;
    public float GetAcceleration() => acceleration;

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
            orbitChargeRateText.text = orbitChargeRate.ToString();
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
        orbitChargeRateText.text = orbitChargeRate.ToString();
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