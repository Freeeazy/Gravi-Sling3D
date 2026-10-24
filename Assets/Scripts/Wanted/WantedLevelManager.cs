using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-local wanted state. Attach to an active GameObject in the gameplay scene.
/// Other systems should read CurrentWantedLevel and subscribe to WantedLevelChanged.
/// This manager does not spawn police or automatically decay wanted stars.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class WantedLevelManager : MonoBehaviour
{
    public static WantedLevelManager Instance { get; private set; }

    public const int SupportedMaxWantedLevel = 5;

    [Header("Wanted Level")]
    [SerializeField, Range(1, SupportedMaxWantedLevel)] private int maxWantedLevel = SupportedMaxWantedLevel;
    [SerializeField, Min(0)] private int startingWantedLevel = 0;

    [Header("Wanted UI")]
    [Tooltip("Assign left to right. Hidden stars become transparent, preserving layout slots. Keep these GameObjects active.")]
    [SerializeField]
    private List<Image> starImages = new List<Image>
    {
        null, null, null, null, null
    };
    [SerializeField] private TMP_Text escapeTimerText;

    [Serializable]
    public class HeatSettings
    {
        [Min(1)] public int policeCount = 3;
        [Min(0f)] public float beamMultiplier = 1f;
        [Min(0.1f)] public float speedMultiplier = 1f;
        [Min(0.1f)] public float accelerationMultiplier = 1f;
        [Min(1f)] public float incomingSpeed = 400f;
        [Tooltip("Initial distance as a multiple of the chase manager's spawn radius.")]
        public Vector2 incomingDistance = new Vector2(8f, 10f);
        [Tooltip("Degrees from the destination direction: 0 = ahead, 90 = sides, 180 = behind.")]
        public Vector2 approachAngles = new Vector2(135f, 180f);
        [Tooltip("Chance to favor the forward 70-degree cone within the allowed approach angles.")]
        [Range(0f, 1f)] public float forwardBias;

        public static HeatSettings ForLevel(int level)
        {
            int i = Mathf.Clamp(level, 1, SupportedMaxWantedLevel) - 1;
            return new HeatSettings
            {
                policeCount = 3 + i * 2,
                beamMultiplier = 1f + i * 0.25f,
                speedMultiplier = 1f + i * 0.08f,
                accelerationMultiplier = 1f + i * 0.15f,
                incomingSpeed = 400f + i * 35f,
                incomingDistance = new Vector2(9.6f - i * 0.6f, 11.2f - i * 0.6f),
                approachAngles = new[] { new Vector2(135f, 180f), new Vector2(95f, 165f),
                    new Vector2(60f, 120f), new Vector2(0f, 180f), new Vector2(0f, 180f) }[i],
                forwardBias = i == 3 ? 0.55f : i == 4 ? 0.8f : 0f
            };
        }
    }

    [Header("Heat Profiles (levels 1 through 5)")]
    [SerializeField] private HeatSettings[] heatSettings =
    {
        HeatSettings.ForLevel(1), HeatSettings.ForLevel(2), HeatSettings.ForLevel(3),
        HeatSettings.ForLevel(4), HeatSettings.ForLevel(5)
    };

    [Header("Testing")]
    [Tooltip("During Play mode, use the component context menu: Apply Test Heat Level.")]
    [SerializeField, Range(0, 5)] private int testHeatLevel = 1;
    [SerializeField] private bool logChanges = true;

    private readonly Dictionary<int, int> packageHeat = new Dictionary<int, int>();
    public int CargoHeatLevel { get; private set; }

    public int CurrentWantedLevel { get; private set; }
    public int MaxWantedLevel => maxWantedLevel;
    public bool IsWanted => CurrentWantedLevel > 0;
    public bool IsEscapeTimerActive { get; private set; }
    public float EscapeTimeRemaining { get; private set; }

    /// <summary>
    /// Arguments: previous level, new level. Only fires when the value changes.
    /// Initialization is not a change event: subscribers must also read the current
    /// value when they connect (normally in OnEnable/Start).
    /// </summary>
    public event Action<int, int> WantedLevelChanged;

    private Color[] visibleStarColors;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate WantedLevelManager removed.", this);
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
        maxWantedLevel = Mathf.Clamp(maxWantedLevel, 1, SupportedMaxWantedLevel);
        CurrentWantedLevel = Mathf.Clamp(startingWantedLevel, 0, maxWantedLevel);
        CacheStarColors();
        RefreshStars();
        ClearEscapeTimer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        WantedLevelChanged = null;
    }

    public HeatSettings GetHeatSettings(int level)
    {
        int index = Mathf.Clamp(level, 1, SupportedMaxWantedLevel) - 1;
        return heatSettings != null && index < heatSettings.Length && heatSettings[index] != null
            ? heatSettings[index] : HeatSettings.ForLevel(level);
    }

    [ContextMenu("Apply Test Heat Level")]
    private void ApplyTestHeatLevel()
    {
        if (Application.isPlaying) SetWantedLevel(testHeatLevel);
    }

    /// <summary>Packages use their highest heat, never a sum. Pickup alerts police once.</summary>
    public void RegisterPackageHeat(int questId, int heat)
    {
        heat = Mathf.Clamp(heat, 0, maxWantedLevel);
        if (heat == 0) return;
        packageHeat[questId] = heat;
        RefreshCargoHeat();
        SetWantedLevel(Mathf.Max(CurrentWantedLevel, CargoHeatLevel));
    }

    public void RemovePackageHeat(int questId)
    {
        if (!packageHeat.Remove(questId)) return;
        RefreshCargoHeat();
        // Delivery/failure may lower an active chase, but must not restart an escaped one.
        if (IsWanted) SetWantedLevel(Mathf.Min(CurrentWantedLevel, CargoHeatLevel));
    }

    private void RefreshCargoHeat()
    {
        CargoHeatLevel = 0;
        foreach (int heat in packageHeat.Values) CargoHeatLevel = Mathf.Max(CargoHeatLevel, heat);
    }

    public bool TryRescanCargo()
    {
        if (IsWanted || CargoHeatLevel < 3) return false;
        SetWantedLevel(CargoHeatLevel);
        return IsWanted;
    }

    /// <summary>Adds a positive number of stars, stopping at the maximum.</summary>
    public void AddStars(int amount = 1)
    {
        if (amount <= 0)
            return;

        // Limit before adding, so even a very large amount cannot overflow.
        int available = Mathf.Max(0, maxWantedLevel - CurrentWantedLevel);
        SetWantedLevel(CurrentWantedLevel + Mathf.Min(amount, available));
    }

    /// <summary>Removes a positive number of stars, stopping at zero.</summary>
    public void RemoveStars(int amount = 1)
    {
        if (amount <= 0)
            return;

        SetWantedLevel(CurrentWantedLevel - Mathf.Min(amount, CurrentWantedLevel));
    }

    public void ClearWantedLevel()
    {
        SetWantedLevel(0);
        ClearEscapeTimer();
    }

    public void SetWantedLevel(int level)
    {
        if (Instance != this)
            return;

        int newLevel = Mathf.Clamp(level, 0, maxWantedLevel);
        if (newLevel == CurrentWantedLevel)
            return;

        int previousLevel = CurrentWantedLevel;
        CurrentWantedLevel = newLevel;
        RefreshStars();
        if (newLevel == 0)
            ClearEscapeTimer();

        if (logChanges)
            Debug.Log($"Wanted level: {previousLevel} -> {newLevel}", this);

        WantedLevelChanged?.Invoke(previousLevel, newLevel);
    }

    private void CacheStarColors()
    {
        if (starImages == null)
            return;

        visibleStarColors = new Color[starImages.Count];
        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null)
                continue;

            visibleStarColors[i] = starImages[i].color;
            starImages[i].enabled = true;
        }
    }

    private void RefreshStars()
    {
        if (starImages == null || visibleStarColors == null)
            return;

        for (int i = 0; i < starImages.Count && i < visibleStarColors.Length; i++)
        {
            if (starImages[i] != null)
            {
                Color color = visibleStarColors[i];
                if (i >= CurrentWantedLevel)
                    color.a = 0f;
                starImages[i].color = color;
            }
        }
    }

    /// <summary>
    /// Displays the remaining escape time supplied by the chase manager.
    /// Does not run a countdown or clear stars when it reaches zero.
    /// Call each time the chase countdown changes.
    /// </summary>
    public void SetEscapeTimer(float secondsRemaining)
    {
        if (Instance != this)
            return;

        if (!IsWanted || float.IsNaN(secondsRemaining) || float.IsInfinity(secondsRemaining))
        {
            ClearEscapeTimer();
            return;
        }

        IsEscapeTimerActive = true;
        EscapeTimeRemaining = Mathf.Max(0f, secondsRemaining);
        if (escapeTimerText != null)
            escapeTimerText.SetText("Escape: {0:1}s", EscapeTimeRemaining);
    }

    /// <summary>Call when reacquired, when the chase ends, or before a countdown starts.</summary>
    public void ClearEscapeTimer()
    {
        if (Instance != this)
            return;

        IsEscapeTimerActive = false;
        EscapeTimeRemaining = 0f;
        if (escapeTimerText != null)
            escapeTimerText.text = "";
    }

    private void OnValidate()
    {
        maxWantedLevel = Mathf.Clamp(maxWantedLevel, 1, SupportedMaxWantedLevel);
        startingWantedLevel = Mathf.Clamp(startingWantedLevel, 0, maxWantedLevel);
        // Set configuration before entering Play mode. Use the public methods or
        // context menu to change the live level so listeners receive change events.
    }
}
