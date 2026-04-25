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

    // Reuse buffers to avoid allocations
    private readonly List<Vector3> _uniqueWorldTargets = new List<Vector3>(16);
    private readonly HashSet<Vector3Int> _seenCoords = new HashSet<Vector3Int>();

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

        BuildUniqueTargets(active);

        int uniqueCount = _uniqueWorldTargets.Count;
        int count = Mathf.Min(uniqueCount, maxSlots);

        // Update active slots
        for (int i = 0; i < count; i++)
        {
            Vector3 target = _uniqueWorldTargets[i];

            bool isClosest = questManager.HasClosestQuest && TryGetCoordForTarget(target, active, out var coord) && coord == questManager.ClosestQuestCoord;

            if (pointers[i])
            {
                pointers[i].SetTarget(cam, target, hasTarget: true);
                pointers[i].SetSlotActive(true);
                pointers[i].SetHighlighted(isClosest);
            }

            if (beacons[i])
            {
                beacons[i].SetTarget(cam, target, hasTarget: true);
                beacons[i].SetSlotActive(true);
                beacons[i].SetHighlighted(isClosest);
            }
        }

        // Disable unused slots
        for (int i = count; i < maxSlots; i++)
        {
            if (pointers[i])
            {
                pointers[i].SetHighlighted(false);
                pointers[i].SetSlotActive(false);
            }

            if (beacons[i])
            {
                beacons[i].SetHighlighted(false);
                beacons[i].SetSlotActive(false);
            }
        }

        if (hideAllWhenNoQuests && uniqueCount == 0)
            SetAllActive(false);
    }

    private void BuildUniqueTargets(IReadOnlyList<NPCQuestManager.ActiveQuest> active)
    {
        _uniqueWorldTargets.Clear();
        _seenCoords.Clear();

        if (active == null || active.Count == 0)
            return;

        // Stable ordering: first quest that targets a coord "wins"
        for (int i = 0; i < active.Count; i++)
        {
            var q = active[i];
            if (_seenCoords.Add(q.toCoord))
                _uniqueWorldTargets.Add(q.toWorldPos);
        }
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

    private bool TryGetCoordForTarget(
    Vector3 target,
    IReadOnlyList<NPCQuestManager.ActiveQuest> active,
    out Vector3Int coord)
    {
        coord = default;

        if (active == null)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].toWorldPos == target)
            {
                coord = active[i].toCoord;
                return true;
            }
        }

        return false;
    }
}