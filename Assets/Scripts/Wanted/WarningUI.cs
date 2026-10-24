
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WarningUI : MonoBehaviour
{
    public static WarningUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private RectTransform textPanel;
    [SerializeField] private RectTransform progressFill;

    [Header("Text Timing")]
    [SerializeField] private float warningDuration = 0.5f;
    [SerializeField] private float detectedDuration = 0.75f;

    [Header("Panel Widths")]
    [SerializeField] private float warningWidth = 350f;
    [SerializeField] private float detectedWidth = 550f;

    [Header("Progress Bar")]
    [SerializeField] private float maxProgressWidth = 550f;

    [Header("Shared Scanner Network")]
    [Tooltip("Progress lost with no scanner in range, relative to the last active scan speed.")]
    [SerializeField, Min(0.01f)] private float scanDecayMultiplier = 0.5f;

    public float ScanProgress { get; private set; }
    public bool IsScanning { get; private set; }
    public bool IsDecaying => !IsScanning && ScanProgress > 0f;

    // Scanners report in Update; LateUpdate consumes those reports once per frame.
    private readonly Dictionary<PoliceScanner, float> scanReports = new();
    private float lastScanDuration = 1f;

    private Coroutine flashRoutine;
    private bool warningVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ResetNetwork();
    }

    public void ReportScan(PoliceScanner scanner, float duration)
    {
        if (!isActiveAndEnabled || !scanner || !scanner.isActiveAndEnabled)
            return;
        scanReports[scanner] = Mathf.Max(0.1f, duration);
    }

    public void UnregisterScanner(PoliceScanner scanner)
    {
        // Leaving or unloading one POI never clears shared progress.
        scanReports.Remove(scanner);
    }

    private void LateUpdate()
    {
        var wanted = WantedLevelManager.Instance;
        if (wanted == null || wanted.IsWanted || wanted.CargoHeatLevel < 3)
        {
            ResetNetwork();
            return;
        }

        float duration = float.PositiveInfinity;
        foreach (var report in scanReports)
            if (report.Key && report.Key.isActiveAndEnabled)
                duration = Mathf.Min(duration, report.Value);
        scanReports.Clear();

        IsScanning = !float.IsPositiveInfinity(duration);
        if (IsScanning)
        {
            lastScanDuration = duration;
            ScanProgress = Mathf.Clamp01(ScanProgress + Time.deltaTime / duration);
        }
        else
        {
            ScanProgress = Mathf.Max(0f, ScanProgress -
                Time.deltaTime * Mathf.Max(0.01f, scanDecayMultiplier) / lastScanDuration);
        }

        if (ScanProgress >= 1f)
        {
            wanted.TryRescanCargo();
            ResetNetwork();
            return;
        }

        if (IsScanning || ScanProgress > 0f) ShowWarning();
        else HideWarning();
        SetProgress(ScanProgress);
    }

    private void ResetNetwork()
    {
        scanReports.Clear();
        ScanProgress = 0f;
        IsScanning = false;
        SetProgress(0f);
        HideWarning();
    }

    private void ShowWarning()
    {
        if (warningVisible)
            return;

        warningVisible = true;

        SetVisible(true);

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private void HideWarning()
    {
        warningVisible = false;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetVisible(false);
    }

    private IEnumerator FlashRoutine()
    {
        while (warningVisible)
        {
            if (warningText) warningText.text = "WARNING";
            if (textPanel) textPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                warningWidth
            );

            yield return new WaitForSeconds(Mathf.Max(0.01f, warningDuration));

            if (warningText) warningText.text = "SCAN DETECTED";
            if (textPanel) textPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                detectedWidth
            );

            yield return new WaitForSeconds(Mathf.Max(0.01f, detectedDuration));
        }

        flashRoutine = null;
    }

    private void SetProgress(float progress)
    {
        float width = Mathf.Clamp01(progress) * maxProgressWidth;

        if (progressFill) progressFill.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
    }

    private void SetVisible(bool visible)
    {
        if (warningText)
            warningText.gameObject.SetActive(visible);

        if (textPanel)
            textPanel.gameObject.SetActive(visible);

        if (progressFill)
            progressFill.gameObject.SetActive(visible);
    }

    private void OnDisable()
    {
        ResetNetwork();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
