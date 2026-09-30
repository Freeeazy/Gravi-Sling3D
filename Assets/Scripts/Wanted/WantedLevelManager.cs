using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Prototype Debugging")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private bool allowDebugKeysWhilePaused = false;
    [SerializeField] private bool logChanges = true;

#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key increaseKey = Key.F6;
    [SerializeField] private Key decreaseKey = Key.F7;
    [SerializeField] private Key clearKey = Key.F8;
#else
    [SerializeField] private KeyCode increaseKey = KeyCode.F6;
    [SerializeField] private KeyCode decreaseKey = KeyCode.F7;
    [SerializeField] private KeyCode clearKey = KeyCode.F8;
#endif

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
        maxWantedLevel = Mathf.Max(1, maxWantedLevel);
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

    private void Update()
    {
        if (!enableDebugKeys || (!allowDebugKeysWhilePaused && Time.timeScale == 0f))
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (clearKey != Key.None && keyboard[clearKey].wasPressedThisFrame)
            ClearWantedLevel();
        else if (increaseKey != Key.None && keyboard[increaseKey].wasPressedThisFrame)
            AddStars();
        else if (decreaseKey != Key.None && keyboard[decreaseKey].wasPressedThisFrame)
            RemoveStars();
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
        maxWantedLevel = Mathf.Max(1, maxWantedLevel);
        startingWantedLevel = Mathf.Clamp(startingWantedLevel, 0, maxWantedLevel);
        // Set configuration before entering Play mode. Use the public methods or
        // debug keys to change the live level so listeners receive change events.
    }
}
