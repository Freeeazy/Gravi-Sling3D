using System.Collections;
using UnityEngine;

public class OpenPanel : MonoBehaviour
{
    [Header("Panel")]
    public CanvasGroup panelToToggle;

    [Header("Settings")]
    public bool startClosed = true;

    private const float Delay = 0.1f;

    private void Start()
    {
        if (panelToToggle != null && startClosed)
        {
            panelToToggle.alpha = 0f;
            panelToToggle.interactable = false;
            panelToToggle.blocksRaycasts = false;
        }
    }

    public void TogglePanel()
    {
        StartCoroutine(TogglePanelDelayed());
    }

    public void Open()
    {
        StartCoroutine(OpenDelayed());
    }

    public void Close()
    {
        StartCoroutine(CloseDelayed());
    }

    private IEnumerator TogglePanelDelayed()
    {
        yield return new WaitForSecondsRealtime(Delay);

        if (panelToToggle != null)
        {
            panelToToggle.alpha = panelToToggle.alpha == 0f ? 1f : 0f;
            panelToToggle.interactable = !panelToToggle.interactable;
            panelToToggle.blocksRaycasts = !panelToToggle.blocksRaycasts;
        }
    }

    private IEnumerator OpenDelayed()
    {
        yield return new WaitForSecondsRealtime(Delay);

        if (panelToToggle != null)
        {
            panelToToggle.alpha = 1f;
            panelToToggle.interactable = true;
            panelToToggle.blocksRaycasts = true;
        }
    }

    private IEnumerator CloseDelayed()
    {
        yield return new WaitForSecondsRealtime(Delay);

        if (panelToToggle != null)
        {
            panelToToggle.alpha = 0f;
            panelToToggle.interactable = false;
            panelToToggle.blocksRaycasts = false;
        }
    }
}