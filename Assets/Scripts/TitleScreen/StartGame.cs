using UnityEngine;

public class StartGame : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainGame";
    [SerializeField] private float delay = 1f;

    public void LoadScene()
    {
        if (TutorialLoadManager.Instance != null)
        {
            TutorialLoadManager.Instance.StartGame(mainSceneName, delay);
        }
        else
        {
            Debug.LogWarning("No TutorialLoadManager found. Loading main scene directly as fallback.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }
}