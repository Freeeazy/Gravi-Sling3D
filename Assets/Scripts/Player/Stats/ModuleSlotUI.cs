using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ModuleSlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image iconImage;

    public ModuleData EquippedModule { get; private set; }

    public bool IsEmpty => EquippedModule == null;

    public void SetModule(ModuleData data)
    {
        EquippedModule = data;

        Debug.Log($"Slot {name} received module: {data.moduleName}");
        Debug.Log($"Icon is null? {data.icon == null}");

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = true;

            Debug.Log($"Assigned sprite to iconImage on {name}");
        }
        else
        {
            Debug.LogWarning($"iconImage is NULL on {name}");
        }
    }

    public void ClearModule()
    {
        EquippedModule = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (ModuleLoadoutManager.Instance != null)
            ModuleLoadoutManager.Instance.RecalculateStats();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"CLICK DETECTED on {name} | Button: {eventData.button}");

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("RIGHT CLICK CONFIRMED");

            if (!IsEmpty)
            {
                Debug.Log("SLOT NOT EMPTY -> CLEARING");
                ClearModule();
            }
            else
            {
                Debug.Log("SLOT EMPTY -> NOTHING TO CLEAR");
            }
        }
    }
}