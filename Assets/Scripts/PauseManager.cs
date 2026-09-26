using UnityEngine;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup pauseCanvasGroup;

    [Header("Scripts To Disable When Paused")]
    [SerializeField] private MonoBehaviour[] scriptsToDisable;

    private bool isPaused = false;

    private void Start()
    {
        SetPauseUI(false);
    }

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

        SetPauseUI(true);

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

        SetPauseUI(false);

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

    private void SetPauseUI(bool visible)
    {
        if (pauseCanvasGroup == null)
            return;

        pauseCanvasGroup.alpha = visible ? 1f : 0f;
        pauseCanvasGroup.interactable = visible;
        pauseCanvasGroup.blocksRaycasts = visible;
    }
}