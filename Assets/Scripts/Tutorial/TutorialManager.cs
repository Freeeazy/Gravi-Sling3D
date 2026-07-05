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

    [System.Serializable]
    public class ProgressCheck
    {
        [Header("Manual Stage Sync")]
        public int afterStageIndex = 0;

        [Header("Timing")]
        public float goodTimeUnder = 8f;
        public float badTimeOver = 20f;
        public float messageHoldTime = 2.5f;

        [Header("Text")]
        public string title = "FLIGHT INSTRUCTOR";
    }

    [Header("UI")]
    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] private GameObject tutorialPanel;

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private bool useTypewriterEffect = true;

    [Header("Stages")]
    [SerializeField] private TutorialStage[] stages;

    [Header("Progress Checks")]
    [SerializeField] private bool useProgressChecks = true;
    [SerializeField] private ProgressCheck[] progressChecks;

    [Header("Global Progress Check Messages")]
    [TextArea(2, 5)]
    [SerializeField] private string[] goodProgressMessages;

    [TextArea(2, 5)]
    [SerializeField] private string[] okayProgressMessages;

    [TextArea(2, 5)]
    [SerializeField] private string[] badProgressMessages;

    [Header("Scene Flow")]
    [SerializeField] private string sceneToLoadAfterTutorial = "MainGame";
    [SerializeField] private bool allowEnterToExitAfterFinished = true;

    private int currentStageIndex = -1;
    private int currentRingCount = 0;
    private bool tutorialFinished = false;
    private Coroutine stageTextRoutine;
    private string currentVisibleBody = "";
    private int stageToken = 0;

    private float stageStartTime = 0f;
    private bool showingProgressCheck = false;

    private int[] goodMessageBag;
    private int[] okayMessageBag;
    private int[] badMessageBag;

    private int goodMessageBagPosition = 0;
    private int okayMessageBagPosition = 0;
    private int badMessageBagPosition = 0;

    private void Start()
    {
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
        stageToken++;
        showingProgressCheck = false;

        if (stageTextRoutine != null)
        {
            StopCoroutine(stageTextRoutine);
            stageTextRoutine = null;
        }

        currentVisibleBody = "";

        if (stageIndex < 0 || stageIndex >= stages.Length)
        {
            FinishTutorial();
            return;
        }

        currentStageIndex = stageIndex;
        currentRingCount = 0;
        stageStartTime = Time.time;

        TutorialStage stage = stages[currentStageIndex];

        ApplyStageSettings(stage);

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        stageTextRoutine = StartCoroutine(ShowStageRoutine(stage, stageToken));
    }

    public void NextStage()
    {
        if (showingProgressCheck)
            return;

        int completedStageIndex = currentStageIndex;
        int nextStageIndex = currentStageIndex + 1;

        if (TryStartProgressCheck(completedStageIndex, nextStageIndex))
            return;

        GoToStage(nextStageIndex);
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
        if (TutorialLoadManager.Instance != null)
        {
            TutorialLoadManager.Instance.SetTutorialCompleted(true);
            TutorialLoadManager.Instance.LoadScene(sceneToLoadAfterTutorial);
        }
        else
        {
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene(sceneToLoadAfterTutorial);
        }
    }

    private IEnumerator ShowStageRoutine(TutorialStage stage, int token)
    {
        if (tutorialText == null)
            yield break;

        if (token != stageToken)
            yield break;

        if (!useTypewriterEffect)
        {
            UpdateTutorialText(stage, stage.message);
        }
        else
        {
            yield return TypeStageText(stage, token);
        }

        if (token != stageToken)
            yield break;

        if (stage.autoAdvance && stage.ringsRequired <= 0)
        {
            yield return new WaitForSeconds(stage.autoAdvanceDelay);

            if (token != stageToken)
                yield break;

            NextStage();
        }
    }

    private IEnumerator TypeStageText(TutorialStage stage, int token)
    {
        string fullMessage = stage.message;
        currentVisibleBody = "";

        for (int i = 0; i < fullMessage.Length; i++)
        {
            if (token != stageToken)
                yield break;

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

                    if (token != stageToken)
                        yield break;

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

        if (token != stageToken)
            yield break;

        UpdateTutorialText(stage, currentVisibleBody);
    }

    private void FinishTutorial()
    {
        stageToken++;

        if (stageTextRoutine != null)
        {
            StopCoroutine(stageTextRoutine);
            stageTextRoutine = null;
        }

        tutorialFinished = true;
        currentVisibleBody = "";

        if (tutorialText != null)
        {
            tutorialText.text =
                "<size=85%><color=#7FF9FF><b>TRAINING COMPLETE</b></color></size>\n" +
                "You can keep practicing here, or press <b>ENTER</b> to begin.";
        }
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

        string header = "";

        if (!string.IsNullOrWhiteSpace(stage.title))
        {
            header = $"<size=85%><color=#7FF9FF><b>{stage.title}</b></color></size>\n";
        }

        string body = bodyText;

        if (stage.ringsRequired > 0)
        {
            body += $"\n\n<size=85%><color=#B8FFF8>Progress: {currentRingCount}/{stage.ringsRequired}</color></size>";
        }

        tutorialText.text = header + body;
    }
    private bool TryStartProgressCheck(int completedStageIndex, int nextStageIndex)
    {
        if (!useProgressChecks || progressChecks == null || progressChecks.Length == 0)
            return false;

        ProgressCheck check = GetProgressCheckForStage(completedStageIndex);

        if (check == null)
            return false;

        float stageTime = Time.time - stageStartTime;

        string message = GetProgressCheckMessage(check, stageTime);

        if (string.IsNullOrWhiteSpace(message))
            return false;

        stageToken++;

        if (stageTextRoutine != null)
        {
            StopCoroutine(stageTextRoutine);
            stageTextRoutine = null;
        }

        stageTextRoutine = StartCoroutine(ShowProgressCheckRoutine(check, message, nextStageIndex, stageToken));
        return true;
    }

    private ProgressCheck GetProgressCheckForStage(int completedStageIndex)
    {
        foreach (ProgressCheck check in progressChecks)
        {
            if (check != null && check.afterStageIndex == completedStageIndex)
                return check;
        }

        return null;
    }

    private string GetProgressCheckMessage(ProgressCheck check, float stageTime)
    {
        if (stageTime <= check.goodTimeUnder)
            return GetNextBagMessage(goodProgressMessages, ref goodMessageBag, ref goodMessageBagPosition);

        if (stageTime >= check.badTimeOver)
            return GetNextBagMessage(badProgressMessages, ref badMessageBag, ref badMessageBagPosition);

        return GetNextBagMessage(okayProgressMessages, ref okayMessageBag, ref okayMessageBagPosition);
    }
    private string GetNextBagMessage(string[] messages, ref int[] bag, ref int bagPosition)
    {
        if (messages == null || messages.Length == 0)
            return "";

        if (bag == null || bag.Length != messages.Length || bagPosition >= bag.Length)
        {
            bag = BuildShuffledBag(messages.Length);
            bagPosition = 0;
        }

        int messageIndex = bag[bagPosition];
        bagPosition++;

        return messages[messageIndex];
    }

    private int[] BuildShuffledBag(int count)
    {
        int[] bag = new int[count];

        for (int i = 0; i < count; i++)
        {
            bag[i] = i;
        }

        for (int i = 0; i < bag.Length; i++)
        {
            int randomIndex = Random.Range(i, bag.Length);

            int temp = bag[i];
            bag[i] = bag[randomIndex];
            bag[randomIndex] = temp;
        }

        return bag;
    }

    private IEnumerator ShowProgressCheckRoutine(ProgressCheck check, string message, int nextStageIndex, int token)
    {
        showingProgressCheck = true;
        currentVisibleBody = "";

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        if (tutorialText != null)
        {
            string header = "";

            if (!string.IsNullOrWhiteSpace(check.title))
            {
                header = $"<size=85%><color=#7FF9FF><b>{check.title}</b></color></size>\n";
            }

            tutorialText.text = header + message;
        }

        yield return new WaitForSeconds(check.messageHoldTime);

        if (token != stageToken)
            yield break;

        showingProgressCheck = false;
        GoToStage(nextStageIndex);
    }
}