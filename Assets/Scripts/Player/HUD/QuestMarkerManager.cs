using System.Collections.Generic;
using UnityEngine;

public class QuestMarkerManager : MonoBehaviour
{
    [Header("Refs")]
    public NPCQuestManager questManager;
    public Camera cam;

    [Header("Marker Slots (match indices)")]
    public QuestPointerMarker[] pointers; // 10
    public QuestBeaconMarker[] beacons;   // 10

    [Header("Behavior")]
    public bool hideAllWhenNoQuests = true;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (!questManager)
        {
            SetAllActive(false);
            return;
        }

        var active = questManager.ActiveQuests;
        int maxSlots = Mathf.Min(
            pointers != null ? pointers.Length : 0,
            beacons != null ? beacons.Length : 0
        );

        if (maxSlots <= 0)
            return;

        int count = Mathf.Min(active.Count, maxSlots);

        // Update active slots
        for (int i = 0; i < count; i++)
        {
            Vector3 target = active[i].toWorldPos;

            if (pointers[i])
            {
                pointers[i].SetTarget(cam, target, hasTarget: true);
                pointers[i].SetSlotActive(true);
            }

            if (beacons[i])
            {
                beacons[i].SetTarget(cam, target, hasTarget: true);
                beacons[i].SetSlotActive(true);
            }
        }

        // Disable unused slots
        for (int i = count; i < maxSlots; i++)
        {
            if (pointers[i]) pointers[i].SetSlotActive(false);
            if (beacons[i]) beacons[i].SetSlotActive(false);
        }

        if (hideAllWhenNoQuests && active.Count == 0)
            SetAllActive(false);
    }

    private void SetAllActive(bool on)
    {
        if (pointers != null)
            for (int i = 0; i < pointers.Length; i++)
                if (pointers[i]) pointers[i].SetSlotActive(on);

        if (beacons != null)
            for (int i = 0; i < beacons.Length; i++)
                if (beacons[i]) beacons[i].SetSlotActive(on);
    }
}