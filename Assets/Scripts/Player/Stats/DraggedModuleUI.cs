using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggedModuleUI : MonoBehaviour
{
    public static bool IsDragging { get; private set; }

    public Image iconImage;
    public bool logDropDebug = false;
    public bool allowSideScreenUnequip = true;
    [Range(0.05f, 0.45f)] public float sideScreenDropPercent = 0.30f;

    private ModuleData moduleData;
    private ModuleSlotUI sourceSlot;
    private DragSource dragSource;
    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private ModuleSlotUI previewSlot;
    private bool showingPreview;
    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>(16);
    private enum DragSource
    {
        Inventory,
        EquippedSlot
    }

    public void Initialize(ModuleData data, RectTransform parentCanvas, Camera cam)
    {
        Initialize(data, null, DragSource.Inventory, parentCanvas, cam);
    }

    public void InitializeFromEquippedSlot(ModuleData data, ModuleSlotUI slot, RectTransform parentCanvas, Camera cam)
    {
        Initialize(data, slot, DragSource.EquippedSlot, parentCanvas, cam);
    }

    private void Initialize(ModuleData data, ModuleSlotUI slot, DragSource source, RectTransform parentCanvas, Camera cam)
    {
        moduleData = data;
        sourceSlot = slot;
        dragSource = source;
        IsDragging = true;
        canvasRect = parentCanvas;
        canvasCamera = cam;
        rectTransform = GetComponent<RectTransform>();

        if (iconImage != null && data != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color = ModuleInventoryManager.Instance != null
                ? ModuleInventoryManager.Instance.GetTierColor(moduleData.moduleTier)
                : Color.white;
        }

        if (logDropDebug && data != null)
            Debug.Log($"DraggedModuleUI Initialize: {data.moduleName}");
    }

    public void SetPosition(Vector2 screenPosition)
    {
        if (rectTransform == null || canvasRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvasCamera,
            out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
            //Debug.Log($"DraggedModuleUI moved to local: {localPoint}");
        }
    }
    public void UpdateHoverPreview(PointerEventData eventData)
    {
        ModuleSlotUI hoveredSlot = FindSlotUnderPointer(eventData);

        if (hoveredSlot == sourceSlot)
            hoveredSlot = null;

        if (hoveredSlot == previewSlot)
            return;

        HideHoverPreview();
        previewSlot = hoveredSlot;

        if (previewSlot == null || previewSlot.IsEmpty || StatManager.Instance == null)
            return;

        StatManager.Instance.ShowModuleReplacementPreview(moduleData, previewSlot.EquippedModule);
        showingPreview = true;
    }

    public void TryDrop(PointerEventData eventData)
    {
        HideHoverPreview();

        if (EventSystem.current == null)
        {
            Destroy(gameObject);
            return;
        }

        RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, RaycastResults);

        if (logDropDebug)
            Debug.Log($"TryDrop hit count: {RaycastResults.Count}");

        ModuleSlotUI slot = FindSlotInRaycastResults();

        if (slot != null)
        {
            if (logDropDebug)
                Debug.Log($"Found slot: {slot.name}, IsEmpty: {slot.IsEmpty}");

            if (slot == sourceSlot)
            {
                Destroy(gameObject);
                return;
            }

            DropOnSlot(slot);
            Destroy(gameObject);
            return;
        }

        if (dragSource == DragSource.EquippedSlot && IsSideScreenDrop(eventData.position))
        {
            ReturnSourceSlotModuleToInventory();
            Destroy(gameObject);
            return;
        }

        if (logDropDebug)
            Debug.Log("No valid slot found. Destroying dragged module.");

        Destroy(gameObject);
    }
    private ModuleSlotUI FindSlotUnderPointer(PointerEventData eventData)
    {
        if (EventSystem.current == null)
            return null;

        RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, RaycastResults);

        return FindSlotInRaycastResults();
    }

    private ModuleSlotUI FindSlotInRaycastResults()
    {
        foreach (var result in RaycastResults)
        {
            ModuleSlotUI slot = result.gameObject.GetComponentInParent<ModuleSlotUI>();

            if (slot != null)
                return slot;
        }

        return null;
    }

    private void DropOnSlot(ModuleSlotUI slot)
    {
        if (slot == null || moduleData == null)
            return;

        if (dragSource == DragSource.Inventory)
        {
            slot.ReplaceModule(moduleData, true);

            if (ModuleInventoryManager.Instance != null)
                ModuleInventoryManager.Instance.RemoveModule(moduleData, 1);

            return;
        }

        ModuleData replacedModule = slot.ReplaceModule(moduleData, false, false);

        if (sourceSlot != null)
        {
            if (replacedModule != null)
                sourceSlot.ReplaceModule(replacedModule, false, false);
            else
                sourceSlot.ClearModule(false);
        }

        if (ModuleLoadoutManager.Instance != null && ModuleLoadoutManager.Instance.isActiveAndEnabled)
            ModuleLoadoutManager.Instance.RecalculateStats();
    }

    private void ReturnSourceSlotModuleToInventory()
    {
        if (sourceSlot == null)
            return;

        sourceSlot.ClearModule(true);
    }

    private bool IsSideScreenDrop(Vector2 screenPosition)
    {
        if (!allowSideScreenUnequip || Screen.width <= 0)
            return false;

        float sideWidth = Screen.width * sideScreenDropPercent;
        return screenPosition.x <= sideWidth || screenPosition.x >= Screen.width - sideWidth;
    }

    private void HideHoverPreview()
    {
        previewSlot = null;

        if (!showingPreview)
            return;

        showingPreview = false;

        if (StatManager.Instance != null)
            StatManager.Instance.HideModuleHoverPreview();
    }

    private void OnDestroy()
    {
        HideHoverPreview();
        IsDragging = false;
    }
}