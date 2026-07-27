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

    [System.Serializable]
    public struct DeliveryTypeVisual
    {
        public NPCQuestManager.DeliveryType deliveryType;
        public string label;
        public Color color;
    }

    [Header("Refs")]
    public NPCQuestManager questManager;

    [Tooltip("Distance text in the dropdown panel")]
    public TMP_Text distanceText;

    public TMP_Text creditsText;
    public TMP_Text reputationRewardText;
    public TMP_Text timerText;

    public TMP_Text acceptText;
    public Button acceptButton;

    [Header("Quest Flavor")]
    public TMP_Text questTitleText;
    public TMP_Text questDescriptionText;
    public TMP_Text questDescriptionBonusText;

    public string fallbackQuestTitle = "Delivery Contract";
    [TextArea] public string fallbackQuestDescription = "Deliver cargo to the destination station.";

    [Header("Delivery Type Pill")]
    public Image deliveryTypePillImage;
    public TMP_Text deliveryTypePillText;

    public DeliveryTypeVisual[] deliveryTypeVisuals =
{
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Urgent, label = "Urgent", color = new Color(1.00f, 0.30f, 0.22f, 1f) },
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Standard, label = "Standard", color = new Color(0.42f, 0.76f, 1.00f, 1f) },
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Relaxed, label = "Relaxed", color = new Color(0.48f, 0.92f, 0.58f, 1f) }
    };

    public Color deliveryTypePillOffColor = new Color(0.16f, 0.16f, 0.18f, 0.65f);

    [Header("Difficulty")]
    public TMP_Text difficultyText;
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

            UpdateRewardDisplay(offer);
            UpdateDifficultyDisplay(offer.difficulty);
            UpdateDeliveryTypeDisplay(offer);
            UpdateTimerDisplay(offer);
            UpdateQuestFlavorDisplay(offer);
        }
        else
        {
            if (distanceText)
                distanceText.text = "----";

            UpdateRewardDisplay(default);
            UpdateDifficultyDisplay(0);
            UpdateDeliveryTypeDisplay(default);
            UpdateTimerDisplay(default);
            UpdateQuestFlavorDisplay(default);
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
        UpdateRewardDisplay(default);
        UpdateDifficultyDisplay(0);
        UpdateDeliveryTypeDisplay(default);
        UpdateTimerDisplay(default);
        UpdateQuestFlavorDisplay(default);
    }

    private void UpdateRewardDisplay(NPCQuestManager.QuestOffer offer)
    {
        if (questManager && offer.valid)
        {
            float rewardMultiplier = offer.deliveryRewardMultiplier > 0f ? offer.deliveryRewardMultiplier : 1f;

            if (creditsText)
                creditsText.text = FormatRewardText(
                    "Credits",
                    questManager.GetPreviewBaseCreditReward(offer),
                    rewardMultiplier,
                    string.Empty
                );

            if (reputationRewardText)
                reputationRewardText.text = FormatRewardText(
                    "Rep",
                    questManager.GetPreviewBaseReputationExpReward(offer),
                    rewardMultiplier,
                    " XP"
                );

            return;
        }

        if (creditsText)
            creditsText.text = "Credits +0";

        if (reputationRewardText)
            reputationRewardText.text = "Rep +0 XP";
    }
    private string FormatRewardText(string label, float baseAmount, float multiplier, string suffix)
    {
        string text = $"{label} +{baseAmount:N0}";

        if (!Mathf.Approximately(multiplier, 1f))
            text += $" x {multiplier:0.##}";

        return text + suffix;
    }
    private void UpdateDeliveryTypeDisplay(NPCQuestManager.QuestOffer offer)
    {
        if (!offer.valid)
        {
            if (deliveryTypePillText)
            {
                deliveryTypePillText.text = "--";
                deliveryTypePillText.color = Color.white;
            }

            if (deliveryTypePillImage)
                deliveryTypePillImage.color = deliveryTypePillOffColor;

            return;
        }

        DeliveryTypeVisual visual = GetDeliveryTypeVisual(offer.deliveryType);

        if (deliveryTypePillText)
        {
            deliveryTypePillText.text = visual.label;
            deliveryTypePillText.color = Color.white;
        }

        if (deliveryTypePillImage)
            deliveryTypePillImage.color = visual.color;
    }

    private void UpdateTimerDisplay(NPCQuestManager.QuestOffer offer)
    {
        if (!timerText)
            return;

        if (!questManager || !offer.valid)
        {
            timerText.text = "--:--";
            return;
        }

        float deliverySeconds = questManager.GetPreviewDeliveryTimeSeconds(offer);

        timerText.text = FormatTimerText(deliverySeconds);
    }

    private string FormatTimerText(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);

        return $"{minutes:00}:{secs:00}";
    }
    private void UpdateDifficultyDisplay(int difficulty)
    {
        DifficultyVisual visual = GetDifficultyVisual(difficulty);

        if (difficultyText)
        {
            difficultyText.text = difficulty > 0 ? visual.label : "--";
            difficultyText.color = difficulty > 0 ? visual.color : difficultyBarOffColor;
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
    private DeliveryTypeVisual GetDeliveryTypeVisual(NPCQuestManager.DeliveryType deliveryType)
    {
        if (deliveryTypeVisuals != null)
        {
            for (int i = 0; i < deliveryTypeVisuals.Length; i++)
            {
                if (deliveryTypeVisuals[i].deliveryType == deliveryType)
                {
                    DeliveryTypeVisual visual = deliveryTypeVisuals[i];

                    if (string.IsNullOrEmpty(visual.label))
                        visual.label = deliveryType.ToString();

                    return visual;
                }
            }
        }

        return new DeliveryTypeVisual
        {
            deliveryType = deliveryType,
            label = deliveryType.ToString(),
            color = Color.white
        };
    }
    private void UpdateQuestFlavorDisplay(NPCQuestManager.QuestOffer offer)
    {
        if (!offer.valid)
        {
            if (questTitleText)
                questTitleText.text = fallbackQuestTitle;

            if (questDescriptionText)
                questDescriptionText.text = fallbackQuestDescription;

            if (questDescriptionBonusText)
                questDescriptionBonusText.text = "";

            return;
        }

        if (questTitleText)
        {
            questTitleText.text = !string.IsNullOrWhiteSpace(offer.questTitle)
                ? offer.questTitle
                : fallbackQuestTitle;
        }

        if (questDescriptionText)
        {
            questDescriptionText.text = !string.IsNullOrWhiteSpace(offer.fullDescription)
                ? offer.fullDescription
                : !string.IsNullOrWhiteSpace(offer.shortDescription)
                    ? offer.shortDescription
                    : fallbackQuestDescription;
        }

        if (questDescriptionBonusText)
        {
            questDescriptionBonusText.text = !string.IsNullOrWhiteSpace(offer.deliveryItemName)
                ? ToTitleCase(offer.deliveryItemName)
                : "";
        }
    }
    private static string ToTitleCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        string[] words = text.Trim().Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(words[i]))
                continue;

            string word = words[i].ToLowerInvariant();
            words[i] = char.ToUpperInvariant(word[0]) + word.Substring(1);
        }

        return string.Join(" ", words);
    }
}
