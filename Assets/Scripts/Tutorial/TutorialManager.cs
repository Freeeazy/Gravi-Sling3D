using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStage
    {
        [Header("Text")]
        public string title = "FLIGHT TIP";

        [TextArea(3, 8)]
        public string message;

        [Header("Progress")]
        public int ringsRequired = 0;

        [Header("Auto Advance")]
        public bool autoAdvance = false;
        public float autoAdvanceDelay = 2f;

        [Header("Scripts To Enable During This Stage")]
        public MonoBehaviour[] scriptsToEnable;

        [Header("Scripts To Disable During This Stage")]
        public MonoBehaviour[] scriptsToDisable;

        [Header("Objects To Show During This Stage")]
        public GameObject[] objectsToEnable;

        [Header("Objects To Hide During This Stage")]
        public GameObject[] objectsToDisable;
    }

    [Header("UI")]
    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject exitPromptObject;

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private bool useTypewriterEffect = true;

    [Header("Stages")]
    [SerializeField] private TutorialStage[] stages;

    [Header("Scene Flow")]
    [SerializeField] private string sceneToLoadAfterTutorial = "MainGame";
    [SerializeField] private bool allowEnterToExitAfterFinished = true;

    private int currentStageIndex = -1;
    private int currentRingCount = 0;
    private bool tutorialFinished = false;
    private Coroutine stageTextRoutine;
    private string currentVisibleBody = "";
    private void Start()
    {
        if (exitPromptObject != null)
            exitPromptObject.SetActive(false);

        StartTutorial();
    }

    private void Update()
    {
        if (tutorialFinished && allowEnterToExitAfterFinished && Input.GetKeyDown(KeyCode.Return))
        {
            ExitTutorial();
        }
    }

    public void StartTutorial()
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogWarning("TutorialManager has no stages assigned.");
            return;
        }

        GoToStage(0);
    }

    public void GoToStage(int stageIndex)
    {
        if (stageTextRoutine != null)
        {
            StopCoroutine(stageTextRoutine);
            stageTextRoutine = null;
        }

        if (stageIndex < 0 || stageIndex >= stages.Length)
        {
            FinishTutorial();
            return;
        }

        currentStageIndex = stageIndex;
        currentRingCount = 0;

        TutorialStage stage = stages[currentStageIndex];

        ApplyStageSettings(stage);

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        stageTextRoutine = StartCoroutine(ShowStageRoutine(stage));
    }

    public void NextStage()
    {
        GoToStage(currentStageIndex + 1);
    }

    public void RegisterRingHit()
    {
        if (tutorialFinished || currentStageIndex < 0 || currentStageIndex >= stages.Length)
            return;

        TutorialStage stage = stages[currentStageIndex];

        if (stage.ringsRequired <= 0)
            return;

        currentRingCount++;
        UpdateTutorialText(stage, currentVisibleBody);

        if (currentRingCount >= stage.ringsRequired)
        {
            NextStage();
        }
    }

    public void ContinueTutorial()
    {
        if (!tutorialFinished)
            NextStage();
    }

    public void ExitTutorial()
    {
        SceneManager.LoadScene(sceneToLoadAfterTutorial);
    }

    private IEnumerator ShowStageRoutine(TutorialStage stage)
    {
        if (tutorialText == null)
            yield break;

        if (!useTypewriterEffect)
        {
            UpdateTutorialText(stage, stage.message);
        }
        else
        {
            yield return StartCoroutine(TypeStageText(stage));
        }

        if (stage.autoAdvance && stage.ringsRequired <= 0)
        {
            yield return new WaitForSeconds(stage.autoAdvanceDelay);
            NextStage();
        }
    }

    private IEnumerator TypeStageText(TutorialStage stage)
    {
        string fullMessage = stage.message;
        currentVisibleBody = "";

        for (int i = 0; i < fullMessage.Length; i++)
        {
            // Pause command: @2, @1.5, etc.
            if (fullMessage[i] == '@')
            {
                int start = i + 1;
                int end = start;

                while (end < fullMessage.Length &&
                       (char.IsDigit(fullMessage[end]) || fullMessage[end] == '.'))
                {
                    end++;
                }

                if (end > start && float.TryParse(fullMessage.Substring(start, end - start), out float pauseTime))
                {
                    yield return new WaitForSeconds(pauseTime);
                    i = end - 1;
                    continue;
                }
            }

            // Literal "\n" command = clear current body/page
            if (fullMessage[i] == '\\' && i + 1 < fullMessage.Length && fullMessage[i + 1] == 'n')
            {
                currentVisibleBody = "";
                UpdateTutorialText(stage, currentVisibleBody);
                i++; // skip the 'n'
                continue;
            }

            // TMP rich text tag: append instantly, don't type character-by-character
            if (fullMessage[i] == '<')
            {
                int tagEnd = fullMessage.IndexOf('>', i);

                if (tagEnd != -1)
                {
                    currentVisibleBody += fullMessage.Substring(i, tagEnd - i + 1);
                    UpdateTutorialText(stage, currentVisibleBody);
                    i = tagEnd;
                    continue;
                }
            }

            currentVisibleBody += fullMessage[i];
            UpdateTutorialText(stage, currentVisibleBody);

            yield return new WaitForSeconds(1f / Mathf.Max(1f, charactersPerSecond));
        }

        UpdateTutorialText(stage, currentVisibleBody);
    }

    private void FinishTutorial()
    {
        tutorialFinished = true;

        if (tutorialText != null)
        {
            tutorialText.text =
                "<size=85%><color=#7FF9FF><b>TRAINING COMPLETE</b></color></size>\n" +
                "You can keep practicing here, or press <b>ENTER</b> to begin.";
        }

        if (exitPromptObject != null)
            exitPromptObject.SetActive(true);
    }

    private void ApplyStageSettings(TutorialStage stage)
    {
        if (stage.scriptsToEnable != null)
        {
            foreach (MonoBehaviour script in stage.scriptsToEnable)
            {
                if (script != null)
                    script.enabled = true;
            }
        }

        if (stage.scriptsToDisable != null)
        {
            foreach (MonoBehaviour script in stage.scriptsToDisable)
            {
                if (script != null)
                    script.enabled = false;
            }
        }

        if (stage.objectsToEnable != null)
        {
            foreach (GameObject obj in stage.objectsToEnable)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
        }

        if (stage.objectsToDisable != null)
        {
            foreach (GameObject obj in stage.objectsToDisable)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
    }

    private void UpdateTutorialText(TutorialStage stage, string bodyText)
    {
        if (tutorialText == null)
            return;

        string header = $"<size=85%><color=#7FF9FF><b>{stage.title}</b></color></size>\n";

        string body = bodyText;

        if (stage.ringsRequired > 0)
        {
            body += $"\n\n<size=85%><color=#B8FFF8>Progress: {currentRingCount}/{stage.ringsRequired}</color></size>";
        }

        tutorialText.text = header + body;
    }
}