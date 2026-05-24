using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleButtonUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public ModuleData moduleData;
    public DraggedModuleUI draggedModulePrefab;
    public RectTransform dragCanvas;
    public ScrollRect parentScrollRect;
    public Camera uiCamera;

    [Header("Inventory Display")]
    public Image iconImage;
    public TMP_Text amountText;

    private int currentAmount;
    private DraggedModuleUI currentDragged;

    private void Start()
    {
        RefreshDisplay();
    }

    public void SetAmount(int amount)
    {
        currentAmount = amount;

        if (amountText != null)
            amountText.text = amount > 1 ? amount.ToString() : "";
        
        RefreshDisplay();
    }
    private void RefreshDisplay()
    {
        if (moduleData == null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.white;
                iconImage.enabled = false;
            }

            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = moduleData.icon;
            iconImage.enabled = moduleData.icon != null;

            if (ModuleInventoryManager.Instance != null)
                iconImage.color = ModuleInventoryManager.Instance.GetTierColor(moduleData.moduleTier);
            else
                iconImage.color = Color.white;
        }
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (moduleData == null)
            return;

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.ShowDelayed(moduleData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"BeginDrag on: {name}");

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();

        if (moduleData == null || draggedModulePrefab == null || dragCanvas == null)
        {
            Debug.LogWarning("ModuleButtonUI missing references.");
            return;
        }

        if (parentScrollRect != null)
            parentScrollRect.enabled = false;

        currentDragged = Instantiate(draggedModulePrefab, dragCanvas);
        currentDragged.Initialize(moduleData, dragCanvas, uiCamera);
        currentDragged.SetPosition(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentDragged != null)
            currentDragged.SetPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"EndDrag on: {name}");

        if (parentScrollRect != null)
            parentScrollRect.enabled = true;

        if (currentDragged != null)
        {
            currentDragged.TryDrop(eventData);
            currentDragged = null;
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (moduleData == null)
            return;

        // If research panel/mode is active, right-click goes into research slots.
        if (ResearchManager.Instance != null &&
            ResearchManager.Instance.isActiveAndEnabled &&
            ResearchManager.Instance.acceptRightClickModules)
        {
            bool assignedToResearch = ResearchManager.Instance.TryAssignToFirstEmptyResearchSlot(moduleData);

            if (assignedToResearch)
            {
                Debug.Log($"Assigned {moduleData.name} to first empty research slot.");
            }
            else
            {
                Debug.Log("No empty research slot available.");
            }

            return;
        }

        // Otherwise, normal quick-equip behavior.
        if (ModuleLoadoutManager.Instance == null || !ModuleLoadoutManager.Instance.isActiveAndEnabled)
            return;

        bool equipped = ModuleLoadoutManager.Instance.TryEquipToFirstEmptySlot(moduleData);

        if (equipped)
        {
            Debug.Log($"Equipped {moduleData.name} to first empty slot.");
        }
        else
        {
            Debug.Log("No empty module slot available.");
        }
    }
}