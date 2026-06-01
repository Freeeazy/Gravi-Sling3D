using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class WhatAmITouching : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool logOnClickOnly = true;
    [SerializeField] private bool includeInactiveParents = true;

    private PointerEventData pointerData;
    private readonly List<RaycastResult> results = new List<RaycastResult>();

    private void Awake()
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("WhatAmITouching: No EventSystem found in scene.");
            enabled = false;
            return;
        }

        pointerData = new PointerEventData(EventSystem.current);
    }

    private void Update()
    {
        if (logOnClickOnly)
        {
            if (Input.GetMouseButtonDown(0))
                LogObjectsUnderMouse();
        }
        else
        {
            LogObjectsUnderMouse();
        }
    }

    private void LogObjectsUnderMouse()
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("WhatAmITouching: No EventSystem found.");
            return;
        }

        results.Clear();

        pointerData.position = Input.mousePosition;
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.Log("UI Raycast: Nothing under mouse.");
            return;
        }

        Debug.Log($"UI Raycast found {results.Count} object(s) under mouse:");

        for (int i = 0; i < results.Count; i++)
        {
            GameObject obj = results[i].gameObject;

            string path = GetObjectPath(obj.transform);

            Debug.Log(
                $"#{i + 1} | {obj.name}" +
                $"\nPath: {path}" +
                $"\nCanvas: {GetCanvasName(obj)}" +
                $"\nRaycast Module: {results[i].module}" +
                $"\nSorting Layer: {results[i].sortingLayer}" +
                $"\nSorting Order: {results[i].sortingOrder}" +
                $"\nDepth: {results[i].depth}" +
                $"\nDistance: {results[i].distance}"
            );
        }
    }

    private string GetObjectPath(Transform target)
    {
        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            if (!includeInactiveParents && !current.gameObject.activeInHierarchy)
                break;

            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private string GetCanvasName(GameObject obj)
    {
        Canvas canvas = obj.GetComponentInParent<Canvas>();

        if (canvas == null)
            return "None";

        return canvas.name;
    }
}