using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggedModuleUI : MonoBehaviour
{
    public Image iconImage;
    public bool logDropDebug = false;

    private ModuleData moduleData;
    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>(16);

    public void Initialize(ModuleData data, RectTransform parentCanvas, Camera cam)
    {
        moduleData = data;
        canvasRect = parentCanvas;
        canvasCamera = cam;
        rectTransform = GetComponent<RectTransform>();

        if (iconImage != null && data != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color = ModuleInventoryManager.Instance.GetTierColor(moduleData.moduleTier);
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

    public void TryDrop(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            Destroy(gameObject);
            return;
        }

        RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, RaycastResults);

        if (logDropDebug)
            Debug.Log($"TryDrop hit count: {RaycastResults.Count}");

        foreach (var result in RaycastResults)
        {
            if (logDropDebug)
                Debug.Log($"Raycast hit: {result.gameObject.name}");

            ModuleSlotUI slot = result.gameObject.GetComponentInParent<ModuleSlotUI>();
            if (slot != null)
            {
                if (logDropDebug)
                    Debug.Log($"Found slot: {slot.name}, IsEmpty: {slot.IsEmpty}");

                if (slot.IsEmpty)
                {
                    slot.SetModule(moduleData);

                    if (ModuleInventoryManager.Instance != null)
                        ModuleInventoryManager.Instance.RemoveModule(moduleData, 1);

                    if (ModuleLoadoutManager.Instance != null)
                        ModuleLoadoutManager.Instance.RecalculateStats();

                    Destroy(gameObject);
                    return;
                }
            }
        }

        if (logDropDebug)
            Debug.Log("No valid slot found. Destroying dragged module.");

        Destroy(gameObject);
    }
}