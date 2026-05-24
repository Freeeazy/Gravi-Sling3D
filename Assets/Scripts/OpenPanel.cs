using UnityEngine;

public class OpenPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelToToggle;

    [Header("Settings")]
    public bool startClosed = true;

    private void Start()
    {
        if (panelToToggle != null && startClosed)
            panelToToggle.SetActive(false);
    }

    public void TogglePanel()
    {
        if (panelToToggle == null)
            return;

        panelToToggle.SetActive(!panelToToggle.activeSelf);
    }

    public void Open()
    {
        if (panelToToggle != null)
            panelToToggle.SetActive(true);
    }

    public void Close()
    {
        if (panelToToggle != null)
            panelToToggle.SetActive(false);
    }
}