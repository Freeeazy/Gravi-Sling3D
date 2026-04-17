using UnityEngine;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Scripts To Disable When Paused")]
    [SerializeField] private MonoBehaviour[] scriptsToDisable;

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        // Disable scripts
        foreach (MonoBehaviour script in scriptsToDisable)
        {
            if (script != null)
                script.enabled = false;
        }

        SimpleMove.Instance?.SetPaused(true);
        SimpleFollowCamera.Instance?.SetPaused(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        // Re-enable scripts
        foreach (MonoBehaviour script in scriptsToDisable)
        {
            if (script != null)
                script.enabled = true;
        }

        SimpleMove.Instance?.SetPaused(false);
        SimpleFollowCamera.Instance?.SetPaused(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}