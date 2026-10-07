using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FPSCounterTMP : MonoBehaviour
{
    public static FPSCounterTMP Instance { get; private set; }

    public const string FPS_PREF_KEY = "TargetFPS";
    public const int DEFAULT_FPS = 120;
    public const int UNLIMITED_FPS = -1;

    [Header("UI")]
    public TMP_Text fpsText;

    [Header("Update")]
    [Tooltip("How often to update the displayed FPS (seconds).")]
    public float updateInterval = 0.1f;

    [Header("Toggle")]
    [SerializeField] private Key toggleKey = Key.F12;

    private float _timer;
    private int _frames;
    private float _accumulatedTime;

    public int CurrentTargetFPS { get; private set; } = DEFAULT_FPS;

    private void Awake()
    {
        // Prevent duplicate FPS managers between scenes.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep this object alive between scene changes.
        DontDestroyOnLoad(gameObject);

        // Disable V-Sync so Application.targetFrameRate controls the cap.
        QualitySettings.vSyncCount = 0;

        // Load saved FPS setting.
        int savedFPS = PlayerPrefs.GetInt(FPS_PREF_KEY, DEFAULT_FPS);

        SetTargetFPS(savedFPS, false);
    }

    private void Update()
    {
        // Toggle FPS display with F12.
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (fpsText != null)
                fpsText.gameObject.SetActive(!fpsText.gameObject.activeSelf);
        }

        // Don't calculate FPS while hidden.
        if (fpsText == null || !fpsText.gameObject.activeSelf)
            return;

        float dt = Time.unscaledDeltaTime;

        _frames++;
        _accumulatedTime += dt;
        _timer += dt;

        if (_timer >= updateInterval)
        {
            float fps = (_accumulatedTime > 0f)
                ? (_frames / _accumulatedTime)
                : 0f;

            fpsText.text = $"{fps:0} FPS";

            _timer = 0f;
            _frames = 0;
            _accumulatedTime = 0f;
        }
    }

    /// <summary>
    /// Changes the application's FPS cap.
    /// Use -1 for unlimited.
    /// </summary>
    public void SetTargetFPS(int fps, bool save = true)
    {
        CurrentTargetFPS = fps;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;

        if (save)
        {
            PlayerPrefs.SetInt(FPS_PREF_KEY, fps);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Returns the currently selected FPS cap.
    /// </summary>
    public int GetTargetFPS()
    {
        return CurrentTargetFPS;
    }

    /// <summary>
    /// Returns a nice display string for the current setting.
    /// </summary>
    public string GetTargetFPSLabel()
    {
        return CurrentTargetFPS == UNLIMITED_FPS
            ? "Unlimited"
            : $"{CurrentTargetFPS} FPS";
    }
}