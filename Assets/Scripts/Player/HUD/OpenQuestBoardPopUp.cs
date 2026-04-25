using UnityEngine;

public class OpenQuestBoardPopUp : MonoBehaviour
{
    public static OpenQuestBoardPopUp Instance { get; private set; }

    [Header("Quest Board UI")]
    public GameObject questBoardUI;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // --- Public API ---

    public void OpenQuestBoard()
    {
        if (questBoardUI)
            questBoardUI.SetActive(true);
    }

    public void CloseQuestBoard()
    {
        if (questBoardUI)
            questBoardUI.SetActive(false);
    }

    public void ToggleQuestBoard()
    {
        if (questBoardUI)
            questBoardUI.SetActive(!questBoardUI.activeSelf);
    }
}