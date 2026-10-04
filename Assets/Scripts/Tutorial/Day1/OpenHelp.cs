using UnityEngine;

public class OpenHelp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private CanvasGroup pausePanelCanvasGroup;

    public void OpenHelpPanel()
    {
        if (pausePanelCanvasGroup != null)
        {
            pausePanelCanvasGroup.alpha = 0f;
            pausePanelCanvasGroup.interactable = false;
            pausePanelCanvasGroup.blocksRaycasts = false;
        }

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);
    }

    public void ShowPausePanel()
    {
        if (pausePanelCanvasGroup != null)
        {
            pausePanelCanvasGroup.alpha = 1f;
            pausePanelCanvasGroup.interactable = true;
            pausePanelCanvasGroup.blocksRaycasts = true;
        }
    }
}