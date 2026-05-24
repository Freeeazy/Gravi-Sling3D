using System.Collections.Generic;
using UnityEngine;

public class ObjectToggleManager : MonoBehaviour
{
    public static ObjectToggleManager Instance { get; private set; }

    [Header("Objects To Toggle")]
    public List<GameObject> objectsToToggle = new List<GameObject>();

    [Header("Start State")]
    public bool setStateOnStart = false;
    public bool startEnabled = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (setStateOnStart)
            SetObjectsActive(startEnabled);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ToggleObjects()
    {
        foreach (GameObject obj in objectsToToggle)
        {
            if (obj == null)
                continue;

            obj.SetActive(!obj.activeSelf);
        }
    }

    public void EnableObjects()
    {
        SetObjectsActive(true);
    }

    public void DisableObjects()
    {
        SetObjectsActive(false);
    }

    public void SetObjectsActive(bool active)
    {
        foreach (GameObject obj in objectsToToggle)
        {
            if (obj == null)
                continue;

            obj.SetActive(active);
        }
    }
}