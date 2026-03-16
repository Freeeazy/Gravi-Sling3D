using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggedModuleUI : MonoBehaviour
{
    public Image iconImage;

    private ModuleData moduleData;
    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private Camera canvasCamera;

    public void Initialize(ModuleData data, RectTransform parentCanvas, Camera cam)
    {
        moduleData = data;
        canvasRect = parentCanvas;
        canvasCamera = cam;
        rectTransform = GetComponent<RectTransform>();

        if (iconImage != null && data != null)
            iconImage.sprite = data.icon;

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
            Debug.Log($"DraggedModuleUI moved to local: {localPoint}");
        }
    }

    public void TryDrop(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Debug.Log($"TryDrop hit count: {results.Count}");

        foreach (var result in results)
        {
            Debug.Log($"Raycast hit: {result.gameObject.name}");

            ModuleSlotUI slot = result.gameObject.GetComponentInParent<ModuleSlotUI>();
            if (slot != null)
            {
                Debug.Log($"Found slot: {slot.name}, IsEmpty: {slot.IsEmpty}");

                if (slot.IsEmpty)
                {
                    slot.SetModule(moduleData);

                    if (ModuleLoadoutManager.Instance != null)
                        ModuleLoadoutManager.Instance.RecalculateStats();

                    Destroy(gameObject);
                    return;
                }
            }
        }

        Debug.Log("No valid slot found. Destroying dragged module.");
        Destroy(gameObject);
    }
}