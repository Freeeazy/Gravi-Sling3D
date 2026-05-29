using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialLoadManager : MonoBehaviour
{
    public static TutorialLoadManager Instance { get; private set; }

    private const string TutorialCompletedKey = "TutorialCompleted";

    [Header("Scene Names")]
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string defaultMainSceneName = "MainGame";

    [Header("Loading")]
    [SerializeField] private float defaultDelay = 1f;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool HasCompletedTutorial()
    {
        return PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
    }

    public void SetTutorialCompleted(bool completed)
    {
        PlayerPrefs.SetInt(TutorialCompletedKey, completed ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResetTutorialCompletion()
    {
        PlayerPrefs.DeleteKey(TutorialCompletedKey);
        PlayerPrefs.Save();
    }

    public void StartGame()
    {
        StartGame(defaultMainSceneName, defaultDelay);
    }

    public void StartGame(string mainSceneName)
    {
        StartGame(mainSceneName, defaultDelay);
    }

    public void StartGame(string mainSceneName, float delay)
    {
        string sceneToLoad = HasCompletedTutorial() ? mainSceneName : tutorialSceneName;
        LoadScene(sceneToLoad, delay);
    }

    public void LoadMainGame()
    {
        LoadScene(defaultMainSceneName, defaultDelay);
    }

    public void CompleteTutorialAndLoadMainGame()
    {
        SetTutorialCompleted(true);
        LoadScene(defaultMainSceneName, defaultDelay);
    }

    public void LoadScene(string sceneName)
    {
        LoadScene(sceneName, defaultDelay);
    }

    public void LoadScene(string sceneName, float delay)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("TutorialLoadManager tried to load an empty scene name.");
            return;
        }

        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadSceneWithDelay(sceneName, delay));
    }

    private IEnumerator LoadSceneWithDelay(string sceneName, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SceneManager.LoadScene(sceneName);
    }
}