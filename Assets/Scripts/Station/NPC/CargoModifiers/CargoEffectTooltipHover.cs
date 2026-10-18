using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Attach to cargoEffectPillRoot, not the tooltip itself.
[DisallowMultipleComponent]
public class CargoEffectTooltipHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipRoot;

    private void Awake()
    {
        // Reject an assignment that would deactivate this helper's own object.
        if (tooltipRoot && transform.IsChildOf(tooltipRoot.transform))
        {
            Debug.LogError("Assign a separate tooltip object, not the pill or its parent.", this);
            tooltipRoot = null;
        }

        HideTooltip();
    }

    private void OnEnable()
    {
        HideTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !tooltipRoot)
            return;

        // Include inactive children so text and images cannot extend the hover area.
        foreach (Graphic graphic in tooltipRoot.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        CanvasGroup group = tooltipRoot.GetComponent<CanvasGroup>();
        if (!group)
            group = tooltipRoot.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;

        tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    public void HideTooltip()
    {
        if (tooltipRoot)
            tooltipRoot.SetActive(false);
    }
}
