using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCQuestDropdownUI : MonoBehaviour
{
    [Header("Refs")]
    public NPCQuestManager questManager;

    [Tooltip("Distance text in the dropdown panel")]
    public TMP_Text distanceText;

    public TMP_Text acceptText;
    public Button acceptButton;

    [Header("Difficulty Stars")]
    public GameObject[] difficultyStars;

    private int _currentNpcId = -1;

    public void ShowForNpc(int npcId)
    {
        _currentNpcId = npcId;

        if (questManager && questManager.TryGetOffer(npcId, out var offer) && offer.valid)
        {
            if (distanceText)
                distanceText.text = $"{offer.distance:0} Units";

            UpdateDifficultyStars(offer.difficulty);
        }
        else
        {
            if (distanceText)
                distanceText.text = "----";

            UpdateDifficultyStars(0);
        }

        if (questManager && !questManager.HasActiveQuestFromNpc(npcId))
        {
            acceptButton.enabled = true;
            acceptText.text = "Accept";
        }
        else
        {
            acceptButton.enabled = false;
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
            acceptButton.enabled = false;
            acceptText.text = "Accepted";
        }
    }

    public void ResetUI()
    {
        _currentNpcId = -1;
        if (distanceText) distanceText.text = "----";
    }
    private void UpdateDifficultyStars(int difficulty)
    {
        difficulty = Mathf.Clamp(difficulty, 0, difficultyStars.Length);

        for (int i = 0; i < difficultyStars.Length; i++)
        {
            if (difficultyStars[i] == null)
                continue;

            difficultyStars[i].SetActive(i < difficulty);
        }
    }
}