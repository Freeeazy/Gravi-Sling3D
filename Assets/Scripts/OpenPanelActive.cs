using System.Collections;
using UnityEngine;

public class OpenPanelActive : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelToToggle;

    [Header("Settings")]
    public bool startClosed = true;

    private const float Delay = 0.1f;

    private void Start()
    {
        if (panelToToggle != null && startClosed)
        {
            panelToToggle.SetActive(false);
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
            panelToToggle.SetActive(!panelToToggle.activeSelf);
        }
    }

    private IEnumerator OpenDelayed()
    {
        yield return new WaitForSecondsRealtime(Delay);

        if (panelToToggle != null)
        {
            panelToToggle.SetActive(true);
        }
    }

    private IEnumerator CloseDelayed()
    {
        yield return new WaitForSecondsRealtime(Delay);

        if (panelToToggle != null)
        {
            panelToToggle.SetActive(false);
        }
    }
}