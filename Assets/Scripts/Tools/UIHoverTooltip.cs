using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipRoot;
    private void Awake() => Hide();
    private void OnDisable() => Hide();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipRoot)
            tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) => Hide();

    private void Hide()
    {
        if (tooltipRoot)
            tooltipRoot.SetActive(false);
    }
}