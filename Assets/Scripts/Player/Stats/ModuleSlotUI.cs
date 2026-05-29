using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("UI References")]
    public Image iconImage;

    [Header("Drag")]
    public DraggedModuleUI draggedModulePrefab;
    public RectTransform dragCanvas;
    public Camera uiCamera;
    public bool logInputDebug = false;

    public ModuleData EquippedModule { get; private set; }

    public bool IsEmpty => EquippedModule == null;

    private DraggedModuleUI currentDragged;


    public void SetModule(ModuleData data)
    {
        SetModule(data, true);
    }
    public void SetModule(ModuleData data, bool recalculateStats)
    {
        if (data == null)
            return;

        EquippedModule = data;

        RefreshDisplay();

        if (recalculateStats && ModuleLoadoutManager.Instance != null && ModuleLoadoutManager.Instance.isActiveAndEnabled)
            ModuleLoadoutManager.Instance.RecalculateStats();
    }
    public ModuleData ReplaceModule(ModuleData data, bool returnReplacedToInventory, bool recalculateStats = true)
    {
        if (data == null)
            return null;

        ModuleData replacedModule = EquippedModule;
        EquippedModule = data;

        RefreshDisplay();

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();

        if (returnReplacedToInventory && replacedModule != null && ModuleInventoryManager.Instance != null)
            ModuleInventoryManager.Instance.AddModule(replacedModule, 1);

        if (recalculateStats && ModuleLoadoutManager.Instance != null && ModuleLoadoutManager.Instance.isActiveAndEnabled)
            ModuleLoadoutManager.Instance.RecalculateStats();

        return replacedModule;
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
        if (DraggedModuleUI.IsDragging)
            return;

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
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty)
            return;

        if (ModuleTooltipUI.Instance != null)
            ModuleTooltipUI.Instance.Hide();

        RectTransform targetCanvas = dragCanvas;
        Camera targetCamera = uiCamera;

        if (targetCanvas == null && ModuleInventoryManager.Instance != null)
            targetCanvas = ModuleInventoryManager.Instance.dragCanvas;

        if (targetCamera == null && ModuleInventoryManager.Instance != null)
            targetCamera = ModuleInventoryManager.Instance.uiCamera;

        DraggedModuleUI targetDraggedPrefab = draggedModulePrefab != null
            ? draggedModulePrefab
            : ModuleButtonUI.DefaultDraggedModulePrefab;

        if (targetDraggedPrefab == null || targetCanvas == null)
        {
            Debug.LogWarning("ModuleSlotUI missing drag references.", this);
            return;
        }

        currentDragged = Instantiate(targetDraggedPrefab, targetCanvas);
        currentDragged.InitializeFromEquippedSlot(EquippedModule, this, targetCanvas, targetCamera);
        currentDragged.SetPosition(eventData.position);
        currentDragged.UpdateHoverPreview(eventData);

        if (logInputDebug)
            Debug.Log($"BeginDrag equipped module: {EquippedModule.moduleName}", this);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (currentDragged == null)
            return;

        currentDragged.SetPosition(eventData.position);
        currentDragged.UpdateHoverPreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (currentDragged == null)
            return;

        currentDragged.TryDrop(eventData);
        currentDragged = null;
    }
}