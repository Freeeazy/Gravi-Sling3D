using UnityEngine;
using UnityEngine.UI;

public class ContinueButton : MonoBehaviour
{
    [SerializeField] private Button continueButton;

    private void Awake()
    {
        if (continueButton == null)
            continueButton = GetComponent<Button>();

        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);
    }

    private void OnEnable()
    {
        RefreshButton();
    }

    public void RefreshButton()
    {
        if (continueButton == null)
            return;

        bool hasSave =
            SaveManager.Instance != null &&
            SaveManager.Instance.HasAnySave();

        continueButton.interactable = hasSave;
    }

    private void ContinueGame()
    {
        if (SaveManager.Instance == null)
            return;

        SaveManager.Instance.ContinueMostRecentSave();
    }
}