using UnityEngine;
using UnityEngine.EventSystems;

public class TempShopTooltipTrigger : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Source")]
    public ModuleShopButton shopButton;

    private void Awake()
    {
        if (shopButton == null)
            shopButton = GetComponent<ModuleShopButton>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TempShopTooltipUI.Instance != null)
            TempShopTooltipUI.Instance.Show(shopButton);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TempShopTooltipUI.Instance != null)
            TempShopTooltipUI.Instance.Hide();
    }
}