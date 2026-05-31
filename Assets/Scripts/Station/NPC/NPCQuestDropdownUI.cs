using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCQuestDropdownUI : MonoBehaviour
{
    [System.Serializable]
    public struct DifficultyVisual
    {
        public string label;
        public Color color;
    }

    [Header("Refs")]
    public NPCQuestManager questManager;

    [Tooltip("Distance text in the dropdown panel")]
    public TMP_Text distanceText;

    public TMP_Text acceptText;
    public Button acceptButton;

    [Header("Difficulty")]
    public TMP_Text difficultyText;
    public TMP_Text difficultyText2;
    public Graphic difficultyTrait;
    public Graphic[] difficultyBars = new Graphic[0];
    public Color difficultyBarOffColor = new Color(0.16f, 0.16f, 0.18f, 0.65f);

    public DifficultyVisual[] difficultyVisuals =
    {
        new DifficultyVisual { label = "Very Easy", color = new Color(0.40f, 1.00f, 0.72f, 1f) },
        new DifficultyVisual { label = "Easy", color = new Color(0.34f, 0.92f, 0.42f, 1f) },
        new DifficultyVisual { label = "Standard", color = new Color(0.95f, 0.88f, 0.32f, 1f) },
        new DifficultyVisual { label = "Moderate", color = new Color(1.00f, 0.66f, 0.28f, 1f) },
        new DifficultyVisual { label = "Hard", color = new Color(1.00f, 0.38f, 0.24f, 1f) },
        new DifficultyVisual { label = "Very Hard", color = new Color(0.88f, 0.26f, 0.86f, 1f) },
        new DifficultyVisual { label = "Extreme", color = new Color(0.55f, 0.32f, 1.00f, 1f) }
    };

    private int _currentNpcId = -1;

    public void ShowForNpc(int npcId)
    {
        _currentNpcId = npcId;

        if (questManager && questManager.TryGetOffer(npcId, out var offer) && offer.valid)
        {
            if (distanceText)
                distanceText.text = $"{offer.distance:0} Units";

            UpdateDifficultyDisplay(offer.difficulty);
        }
        else
        {
            if (distanceText)
                distanceText.text = "----";

            UpdateDifficultyDisplay(0);
        }

        if (questManager && !questManager.HasActiveQuestFromNpc(npcId))
        {
            if (acceptButton)
                acceptButton.enabled = true;
            if (acceptText)
                acceptText.text = "Accept";
        }
        else
        {
            if (acceptButton)
                acceptButton.enabled = false;
            if (acceptText)
                acceptText.text = "Accepted";
        }
    }

    // Hook this to the Accept button OnClick in the inspector
    public void OnAcceptClicked()
    {
        if (!questManager)
        {
            Debug.LogWarning("[NPCQuestDropdownUI] questManager not assigned.");
            return;
        }

        if (_currentNpcId < 0)
        {
            Debug.LogWarning("[NPCQuestDropdownUI] No npc selected (_currentNpcId < 0). Did you call ShowForNpc()?");
            return;
        }

        bool ok = questManager.AcceptQuest(_currentNpcId);
        Debug.Log(ok
            ? $"[NPCQuestDropdownUI] Accepted quest from npcId={_currentNpcId}"
            : $"[NPCQuestDropdownUI] Accept failed for npcId={_currentNpcId} (max quests? duplicate? no offer?)");

        if (ok)
        {
            if (acceptButton)
                acceptButton.enabled = false;
            if (acceptText)
                acceptText.text = "Accepted";
        }
    }

    public void ResetUI()
    {
        _currentNpcId = -1;
        if (distanceText) distanceText.text = "----";
        UpdateDifficultyDisplay(0);
    }

    private void UpdateDifficultyDisplay(int difficulty)
    {
        DifficultyVisual visual = GetDifficultyVisual(difficulty);

        if (difficultyText)
        {
            difficultyText.text = difficulty > 0 ? visual.label : "--";
            difficultyText.color = difficulty > 0 ? visual.color : difficultyBarOffColor;
        }

        if (difficultyText2)
        {
            difficultyText2.text = difficulty > 0 ? visual.label : "--";
            if (difficultyTrait)
            {
                difficultyTrait.color = difficulty > 0 ? visual.color : difficultyBarOffColor;
            }
        }

        int barCount = difficultyBars != null ? difficultyBars.Length : 0;
        int visibleDifficulty = Mathf.Clamp(difficulty, 0, barCount);

        for (int i = 0; i < barCount; i++)
        {
            if (difficultyBars[i] == null)
                continue;

            difficultyBars[i].color = i < visibleDifficulty ? visual.color : difficultyBarOffColor;
        }
    }

    private DifficultyVisual GetDifficultyVisual(int difficulty)
    {
        if (difficultyVisuals == null || difficultyVisuals.Length == 0)
        {
            return new DifficultyVisual
            {
                label = difficulty > 0 ? $"Difficulty {difficulty}" : "--",
                color = Color.white
            };
        }

        int index = Mathf.Clamp(difficulty, 1, difficultyVisuals.Length) - 1;
        DifficultyVisual visual = difficultyVisuals[index];

        if (string.IsNullOrEmpty(visual.label))
            visual.label = $"Difficulty {difficulty}";

        return visual;
    }
}
