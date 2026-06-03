using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCUILink : MonoBehaviour
{
    [Serializable]
    public class TagSlot
    {
        public GameObject root;     // parent GO for the tag pill (so we can SetActive)
        public Image background;    // image color
        public TMP_Text label;      // tag text
    }

    [Serializable]
    public struct DeliveryTypeVisual
    {
        public NPCQuestManager.DeliveryType deliveryType;
        public string label;
        public Color color;
    }

    [Header("Tag Sizing")]
    public float tagPaddingX = 12f;     // total extra width (left+right)
    public float tagMinWidth = 40f;     // optional: keep tiny tags from looking weird
    public float tagMaxWidth = 220f;    // optional clamp

    [Header("Row UI")]
    public TMP_Text nameText;
    public Image portraitImage;
    public TMP_Text distanceText;
    public TMP_Text creditsRewardText;
    public TMP_Text reputationRewardText;
    public TMP_Text expectedDeliveryTimeText;

    [Header("Interaction")]
    public Button rowButton;

    [Header("Delivery Type Pill")]
    public GameObject deliveryTypePillRoot;
    public Image deliveryTypePillImage;
    public TMP_Text deliveryTypePillText;

    public DeliveryTypeVisual[] deliveryTypeVisuals =
{
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Urgent, label = "Urgent", color = new Color(1.00f, 0.30f, 0.22f, 1f) },
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Standard, label = "Standard", color = new Color(0.42f, 0.76f, 1.00f, 1f) },
        new DeliveryTypeVisual { deliveryType = NPCQuestManager.DeliveryType.Relaxed, label = "Relaxed", color = new Color(0.48f, 0.92f, 0.58f, 1f) }
    };

    public Color deliveryTypePillOffColor = new Color(0.16f, 0.16f, 0.18f, 0.65f);

    [Header("Tag Slots (max 2)")]
    public TagSlot[] tagSlots = new TagSlot[4];

    [Header("Fallbacks")]
    public Sprite defaultPortrait;

    public int BoundNpcId { get; private set; } = -1;

    /// <summary>Call from NPCManager. Handles name + portrait + tags.</summary>
    public void Bind(NPCData npc)
    {
        BoundNpcId = npc.npcId;

        if (nameText) nameText.text = npc.displayName;

        if (portraitImage)
        {
            Sprite s = NPCUIAssets.GetPortraitForNpc(npc.npcId);
            portraitImage.sprite = s ? s : defaultPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        var tags = NPCUtil.GenerateTags(npc.npcId);
        ApplyTags(tags);
    }

    public void SetRowActive(bool on)
    {
        gameObject.SetActive(on);
        if (!on) Clear();
    }

    public void Clear()
    {
        if (nameText) nameText.text = "";

        if (portraitImage)
        {
            portraitImage.sprite = defaultPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        ClearDistance();
        ClearDeliveryType();
        ClearQuestPreview();

        // Hide all tags
        if (tagSlots == null) return;
        for (int i = 0; i < tagSlots.Length; i++)
        {
            if (tagSlots[i]?.root) tagSlots[i].root.SetActive(false);
        }
    }

    private void ApplyTags(NPCUtil.NPCTag[] tags)
    {
        if (tagSlots == null) return;

        // Disable all first
        for (int i = 0; i < tagSlots.Length; i++)
        {
            if (tagSlots[i]?.root) tagSlots[i].root.SetActive(false);
        }

        int slotCount = tagSlots.Length;
        int count = Mathf.Min(tags?.Length ?? 0, slotCount);

        for (int i = 0; i < count; i++)
        {
            var slot = tagSlots[i];
            if (slot == null) continue;

            if (slot.root) slot.root.SetActive(true);
            if (slot.label) slot.label.text = tags[i].label;
            if (slot.background) slot.background.color = tags[i].color;

            SizeTagToLabel(slot);
        }
    }
    private void SizeTagToLabel(TagSlot slot)
    {
        if (slot == null || slot.label == null) return;

        // Make TMP compute preferred sizes right now
        slot.label.ForceMeshUpdate();

        float w = slot.label.preferredWidth + tagPaddingX;
        w = Mathf.Clamp(w, tagMinWidth, tagMaxWidth);

        // Resize whatever rect you want: root or background (usually background)
        RectTransform rt = null;

        if (slot.background) rt = slot.background.rectTransform;
        else if (slot.root) rt = slot.root.GetComponent<RectTransform>();

        if (!rt) return;

        var size = rt.sizeDelta;
        size.x = w;
        rt.sizeDelta = size;
    }
    public void SetDistance(float distanceMeters)
    {
        if (!distanceText) return;

        if (distanceMeters <= 0f)
        {
            distanceText.text = "--";
            return;
        }

        distanceText.text = $"{distanceMeters:0000} Units";
    }

    public void ClearDistance()
    {
        if (!distanceText) return;
        distanceText.text = "";
    }

    public void SetQuestPreview(NPCQuestManager questManager, NPCQuestManager.QuestOffer offer)
    {
        if (!questManager || !offer.valid)
        {
            ClearQuestPreview();
            return;
        }

        float rewardMultiplier = offer.deliveryRewardMultiplier > 0f ? offer.deliveryRewardMultiplier : 1f;

        if (creditsRewardText)
        {
            creditsRewardText.text = FormatRewardText(
                string.Empty,
                questManager.GetPreviewBaseCreditReward(offer),
                rewardMultiplier,
                string.Empty
            );
        }

        if (reputationRewardText)
        {
            reputationRewardText.text = FormatRewardText(
                string.Empty,
                questManager.GetPreviewBaseReputationExpReward(offer),
                rewardMultiplier,
                string.Empty
            );
        }

        if (expectedDeliveryTimeText)
        {
            float deliverySeconds = questManager.GetPreviewDeliveryTimeSeconds(offer);

            expectedDeliveryTimeText.text = FormatTimerText(deliverySeconds);
        }
    }
    public void ClearQuestPreview()
    {
        if (creditsRewardText)
            creditsRewardText.text = "Credits +0";

        if (reputationRewardText)
            reputationRewardText.text = "Rep +0 XP";

        if (expectedDeliveryTimeText)
            expectedDeliveryTimeText.text = "--:--";
    }

    public void SetDeliveryType(NPCQuestManager.DeliveryType deliveryType)
    {
        DeliveryTypeVisual visual = GetDeliveryTypeVisual(deliveryType);

        if (deliveryTypePillRoot)
            deliveryTypePillRoot.SetActive(true);

        if (deliveryTypePillText)
            deliveryTypePillText.text = visual.label;

        if (deliveryTypePillImage)
            deliveryTypePillImage.color = visual.color;
    }

    public void ClearDeliveryType()
    {
        if (deliveryTypePillRoot)
            deliveryTypePillRoot.SetActive(false);

        if (deliveryTypePillText)
            deliveryTypePillText.text = "";

        if (deliveryTypePillImage)
            deliveryTypePillImage.color = deliveryTypePillOffColor;
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
    private string FormatRewardText(string label, float baseAmount, float multiplier, string suffix)
    {
        string text = $"{label} +{baseAmount:N0}";

        if (!Mathf.Approximately(multiplier, 1f))
            text += $" x {multiplier:0.##}";

        return text + suffix;
    }
    private string FormatTimerText(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);

        return $"{minutes:00}:{secs:00}";
    }

#if UNITY_EDITOR
    // Optional: auto-grab common references when you hit "Reset" in inspector
    private void Reset()
    {
        if (!nameText) nameText = GetComponentInChildren<TMP_Text>(true);

        // Try to find an Image named "Portrait" under this row
        if (!portraitImage)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img && img.gameObject.name.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    portraitImage = img;
                    break;
                }
            }
        }
    }
#endif
}