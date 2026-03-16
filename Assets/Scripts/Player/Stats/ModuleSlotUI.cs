using UnityEngine;
using UnityEngine.UI;

public class ModuleSlotUI : MonoBehaviour
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
    }
}