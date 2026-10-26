using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CargoEffectTooltipHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private NPCQuestDropdownUI dropdownUI;
    [SerializeField] private GameObject riskyTooltipRoot;

    [FormerlySerializedAs("tooltipRoot")]
    [SerializeField] private GameObject weirdTooltipRoot;

    private void Awake()
    {
        if (!dropdownUI)
            dropdownUI = GetComponent<NPCQuestDropdownUI>();

        ValidateTooltip(ref riskyTooltipRoot);
        ValidateTooltip(ref weirdTooltipRoot);
        HideTooltip();
    }

    private void ValidateTooltip(ref GameObject root)
    {
        if (root && transform.IsChildOf(root.transform))
        {
            Debug.LogError("Assign a separate tooltip object, not the hover object or its parent.", this);
            root = null;
        }
    }

    private void OnEnable() => HideTooltip();
    private void OnDisable() => HideTooltip();

    public void OnPointerEnter(PointerEventData eventData)
    {
        HideTooltip();
        if (!isActiveAndEnabled || !dropdownUI)
            return;

        NPCQuestManager.QuestOffer offer = dropdownUI.CurrentOffer;
        if (!offer.valid)
            return;

        // Heat takes priority when both are present.
        GameObject tooltip = offer.heatLevel > 0
            ? riskyTooltipRoot
            : offer.cargoEffectType != CargoEffectType.None
                ? weirdTooltipRoot
                : null;

        if (!tooltip)
            return;

        foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
        if (!group)
            group = tooltip.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
        tooltip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    public void HideTooltip()
    {
        if (riskyTooltipRoot)
            riskyTooltipRoot.SetActive(false);
        if (weirdTooltipRoot)
            weirdTooltipRoot.SetActive(false);
    }
}
