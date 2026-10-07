using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FPSCycleButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text buttonText;

    [Header("FPS Options")]
    [SerializeField]
    private int[] fpsOptions =
    {
        30,
        60,
        120,
        240,
        -1 // Unlimited
    };

    private int currentIndex;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(CycleFPS);
    }

    private void Start()
    {
        LoadCurrentSelection();
        UpdateButtonText();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(CycleFPS);
    }

    private void LoadCurrentSelection()
    {
        int currentFPS;

        if (FPSCounterTMP.Instance != null)
        {
            currentFPS = FPSCounterTMP.Instance.GetTargetFPS();
        }
        else
        {
            // Fallback in case the persistent manager hasn't been created yet.
            currentFPS = PlayerPrefs.GetInt(
                FPSCounterTMP.FPS_PREF_KEY,
                FPSCounterTMP.DEFAULT_FPS
            );
        }

        currentIndex = 0;

        for (int i = 0; i < fpsOptions.Length; i++)
        {
            if (fpsOptions[i] == currentFPS)
            {
                currentIndex = i;
                break;
            }
        }
    }

    public void CycleFPS()
    {
        currentIndex++;

        if (currentIndex >= fpsOptions.Length)
            currentIndex = 0;

        int selectedFPS = fpsOptions[currentIndex];

        if (FPSCounterTMP.Instance != null)
        {
            FPSCounterTMP.Instance.SetTargetFPS(selectedFPS);
        }
        else
        {
            // Fallback if no manager currently exists.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = selectedFPS;

            PlayerPrefs.SetInt(
                FPSCounterTMP.FPS_PREF_KEY,
                selectedFPS
            );

            PlayerPrefs.Save();
        }

        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (buttonText == null)
            return;

        int selectedFPS = fpsOptions[currentIndex];

        if (selectedFPS == FPSCounterTMP.UNLIMITED_FPS)
            buttonText.text = "Unlimited";
        else
            buttonText.text = $"{selectedFPS} FPS";
    }
}