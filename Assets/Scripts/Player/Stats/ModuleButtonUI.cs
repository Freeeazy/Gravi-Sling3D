using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleButtonUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public ModuleData moduleData;
    public DraggedModuleUI draggedModulePrefab;
    public RectTransform dragCanvas;
    public ScrollRect parentScrollRect;
    public Camera uiCamera;

    [Header("Inventory Display")]
    public TMP_Text amountText;

    private int currentAmount;
    private DraggedModuleUI currentDragged;

    public void SetAmount(int amount)
    {
        currentAmount = amount;

        if (amountText != null)
            amountText.text = amount > 1 ? amount.ToString() : "";
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"BeginDrag on: {name}");

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
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (moduleData == null)
            return;

        if (ModuleLoadoutManager.Instance == null)
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