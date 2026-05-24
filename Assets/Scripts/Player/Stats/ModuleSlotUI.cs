using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;

    public ModuleData EquippedModule { get; private set; }

    public bool IsEmpty => EquippedModule == null;

    public void SetModule(ModuleData data)
    {
        if (data == null)
            return;

        EquippedModule = data;

        RefreshDisplay();

        if (ModuleLoadoutManager.Instance != null && ModuleLoadoutManager.Instance.isActiveAndEnabled)
            ModuleLoadoutManager.Instance.RecalculateStats();
    }

    public void ClearModule()
    {
        ClearModule(true);
    }

    public void ClearModule(bool returnToInventory)
    {
        if (IsEmpty)
            return;

        ModuleData removedModule = EquippedModule;
        EquippedModule = null;

        RefreshDisplay();

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();

        if (returnToInventory && ModuleInventoryManager.Instance != null && removedModule != null)
            ModuleInventoryManager.Instance.AddModule(removedModule, 1);

        if (ModuleLoadoutManager.Instance != null && ModuleLoadoutManager.Instance.isActiveAndEnabled)
            ModuleLoadoutManager.Instance.RecalculateStats();
    }

    private void RefreshDisplay()
    {
        if (iconImage == null)
            return;

        if (EquippedModule == null)
        {
            iconImage.sprite = null;
            iconImage.color = Color.white;
            iconImage.enabled = false;
            return;
        }

        iconImage.sprite = EquippedModule.icon;
        iconImage.enabled = EquippedModule.icon != null;

        if (ModuleInventoryManager.Instance != null)
            iconImage.color = ModuleInventoryManager.Instance.GetTierColor(EquippedModule.moduleTier);
        else
            iconImage.color = Color.white;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EquippedModule == null)
            return;

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.ShowDelayed(EquippedModule);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (!IsEmpty)
            ClearModule();
    }
}