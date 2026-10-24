
using System.Collections;
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

    private Coroutine flashRoutine;
    private bool scanning;

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
        SetProgress(0f);
        SetVisible(false);
    }

    public void StartScan()
    {
        if (scanning)
            return;

        scanning = true;

        SetProgress(0f);
        SetVisible(true);

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void UpdateScan(float progress)
    {
        if (!scanning)
            return;

        SetProgress(progress);
    }

    public void EndScan(bool completed)
    {
        if (!scanning)
            return;

        scanning = false;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetProgress(completed ? 1f : 0f);
        SetVisible(false);
    }

    private IEnumerator FlashRoutine()
    {
        while (scanning)
        {
            warningText.text = "WARNING";
            textPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                warningWidth
            );

            yield return new WaitForSeconds(warningDuration);

            warningText.text = "SCAN DETECTED";
            textPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                detectedWidth
            );

            yield return new WaitForSeconds(detectedDuration);
        }

        flashRoutine = null;
    }

    private void SetProgress(float progress)
    {
        float width = Mathf.Clamp01(progress) * maxProgressWidth;

        progressFill.SetSizeWithCurrentAnchors(
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
