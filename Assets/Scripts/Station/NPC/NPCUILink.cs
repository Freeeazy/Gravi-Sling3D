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

    [Header("Tag Sizing")]
    public float tagPaddingX = 12f;     // total extra width (left+right)
    public float tagMinWidth = 40f;     // optional: keep tiny tags from looking weird
    public float tagMaxWidth = 220f;    // optional clamp

    [Header("Row UI")]
    public TMP_Text nameText;
    public Image portraitImage;
    public TMP_Text distanceText;

    [Header("Interaction")]
    public Button rowButton;

    [Header("Tag Slots (max 4)")]
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