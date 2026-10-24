using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses the quest marker visuals for incoming squads and enabled police ships.
/// Attach to an always-active HUD object and assign the police-only containers.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)] // Supply positions before the marker components' Update.
public class PoliceMarkerManager : MonoBehaviour
{
    [Header("HUD References")]
    [SerializeField] private PoliceChaseManager chaseManager;
    [SerializeField] private Camera cam;
    [SerializeField] private RectTransform pointerParent;
    [SerializeField] private RectTransform beaconParent;

    [Header("Prefabs for Additional Ships")]
    [Tooltip("Existing children are reused first. Assign the PolicePointer prefab for extra slots.")]
    [SerializeField] private QuestPointerMarker pointerPrefab;
    [Tooltip("Assign the PoliceBeacon prefab for extra ships and incoming squads.")]
    [SerializeField] private QuestBeaconMarker beaconPrefab;

    [Header("Police Ellipse")]
    [SerializeField] private Vector2 ellipseFrac = new Vector2(0.42f, 0.30f);
    [SerializeField] private Vector2 ellipsePaddingPx = new Vector2(10f, 10f);
    [SerializeField] private bool alwaysShowPointers = true;

    [Header("Police Beacon")]
    [Tooltip("Hide both the beacon icon and distance text CLOSER than this camera distance. Pointers remain visible.")]
    [SerializeField, Min(0f)] private float hideBeaconInsideDistance = 500f;

    [Header("Distance Label Grouping")]
    [SerializeField] private bool groupDistanceLabels = true;
    [Tooltip("Only visible beacons or pointers beyond this camera distance can share a label.")]
    [SerializeField, Min(0f)] private float groupLabelsBeyondDistance = 750f;
    [Tooltip("Maximum world distance from the cop displaying the group's label to join its group.")]
    [SerializeField, Min(0f)] private float groupJoinDistance = 50f;
    [Tooltip("Existing members split beyond this distance. Clamped to at least Join Distance.")]
    [SerializeField, Min(0f)] private float groupLeaveDistance = 65f;
    [Tooltip("Beacons must also be this close in canvas units to share a distance label.")]
    [SerializeField, Min(0f)] private float labelJoinSpacing = 100f;
    [SerializeField, Min(0f)] private float labelLeaveSpacing = 130f;

    [Header("Pointer Distance Label Grouping")]
    [SerializeField] private bool groupPointerDistanceLabels = true;
    [Tooltip("Pointers use their final positions on the ellipse, rather than target projections.")]
    [SerializeField, Min(0f)] private float pointerLabelJoinSpacing = 100f;
    [SerializeField, Min(0f)] private float pointerLabelLeaveSpacing = 130f;
    [Tooltip("Show distance on the beacon instead when it is visible and has text, even if its group hides that text.")]
    [SerializeField] private bool preferBeaconDistanceLabels = true;

    private readonly List<QuestPointerMarker> pointers = new List<QuestPointerMarker>();
    private readonly List<QuestBeaconMarker> beacons = new List<QuestBeaconMarker>();
    private bool reportedCapacityWarning;
    private readonly List<Transform> displayedShips = new List<Transform>();
    private readonly List<int> labelOwners = new List<int>();
    private readonly List<bool> groupingCandidates = new List<bool>();
    // Remember identities, not UI indices: slots shift when a ship despawns.
    private readonly Dictionary<Transform, Transform> previousOwners =
        new Dictionary<Transform, Transform>();
    private readonly Dictionary<Transform, Transform> previousPointerOwners =
        new Dictionary<Transform, Transform>();

    private void Awake()
    {
        if (!cam) cam = Camera.main;

        if (!pointerParent || !beaconParent || pointerParent == beaconParent)
        {
            Debug.LogError("PoliceMarkerManager: assign separate PolicePointer and PoliceBeacon containers.", this);
            enabled = false;
            return;
        }

        // Include inactive slots so reusing the scene's prebuilt UI needs no manual arrays.
        pointers.AddRange(pointerParent.GetComponentsInChildren<QuestPointerMarker>(true));
        beacons.AddRange(beaconParent.GetComponentsInChildren<QuestBeaconMarker>(true));
        if (!chaseManager) chaseManager = FindFirstObjectByType<PoliceChaseManager>();
        HideAll();
    }

    private void Update()
    {
        displayedShips.Clear();
        if (!cam) cam = Camera.main;
        if (!cam)
        {
            HideAll();
            return;
        }

        var ships = PoliceShipAI.EnabledShips;
        int slot = 0;
        // Incoming presence gets a normal marker slot and hands it over at deployment.
        if (chaseManager && chaseManager.IncomingPresence && EnsureSlot(slot))
            SetMarker(slot++, chaseManager.IncomingPresence);
        for (int i = 0; i < ships.Count; i++)
        {
            PoliceShipAI ship = ships[i];
            if (!ship || !ship.isActiveAndEnabled) continue;
            if (!EnsureSlot(slot)) break;
            SetMarker(slot++, ship.transform);
        }

        for (int i = slot; i < pointers.Count; i++)
            if (pointers[i]) pointers[i].SetSlotActive(false);
        for (int i = slot; i < beacons.Count; i++)
            if (beacons[i]) beacons[i].SetSlotActive(false);
    }

    private void SetMarker(int slot, Transform target)
    {
        QuestPointerMarker pointer = pointers[slot];
        QuestBeaconMarker beacon = beacons[slot];
        pointer.ellipseFrac = ellipseFrac;
        pointer.ellipsePaddingPx = ellipsePaddingPx;
        pointer.hideWhenInsideEllipse = !alwaysShowPointers;
        pointer.hideWhenBehindCamera = false;
        pointer.SetTarget(cam, target.position, true);
        pointer.SetHighlighted(false);
        pointer.SetSlotActive(true);
        beacon.ellipseFrac = ellipseFrac;
        beacon.ellipsePaddingPx = ellipsePaddingPx;
        beacon.maxDistance = hideBeaconInsideDistance;
        beacon.SetTarget(cam, target.position, true);
        beacon.SetHighlighted(false);
        beacon.SetSlotActive(true);
        displayedShips.Add(target);
    }

    private void LateUpdate()
    {
        GroupLabels(false, previousOwners);
        GroupLabels(true, previousPointerOwners);
        if (chaseManager && chaseManager.IncomingPresence && displayedShips.Count > 0 &&
            displayedShips[0] == chaseManager.IncomingPresence && cam)
        {
            float distance = Vector3.Distance(cam.transform.position, displayedShips[0].position);
            if (pointers[0].distanceText) pointers[0].distanceText.SetText("Incoming: {0:0} Units", distance);
            if (beacons[0].distanceText) beacons[0].distanceText.SetText("Incoming: {0:0} Units", distance);
        }
    }

    private bool HasBeaconLabel(int index)
    {
        var beacon = beacons[index];
        return beacon && beacon.IsBeaconVisible && beacon.distanceText &&
            beacon.distanceText.gameObject.activeInHierarchy;
    }

    private bool HasPointerLabel(int index)
    {
        var pointer = pointers[index];
        return pointer && pointer.IsPointerVisible && pointer.distanceText &&
            pointer.distanceText.gameObject.activeInHierarchy &&
            !(preferBeaconDistanceLabels && HasBeaconLabel(index));
    }

    private void GroupLabels(bool forPointers, Dictionary<Transform, Transform> previousOwners)
    {
        // Marker Update has resolved visibility and final positions. Hidden markers
        // must never own a group's sole label. Each marker type retains its own owners.
        labelOwners.Clear();
        groupingCandidates.Clear();
        for (int i = 0; i < displayedShips.Count; i++)
        {
            var ship = displayedShips[i];
            bool eligible = (forPointers ? groupPointerDistanceLabels : groupDistanceLabels) &&
                cam && ship && ship.gameObject.activeInHierarchy &&
                (forPointers ? HasPointerLabel(i) : HasBeaconLabel(i)) &&
                Vector3.Distance(cam.transform.position, ship.transform.position) >= groupLabelsBeyondDistance;
            groupingCandidates.Add(eligible);
            labelOwners.Add(-1);
        }

        // Give surviving label owners first choice, keeping labels from jumping
        // between formation members as their distances change slightly.
        for (int i = 0; i < displayedShips.Count; i++)
        {
            if (groupingCandidates[i] &&
                previousOwners.TryGetValue(displayedShips[i], out var owner) && owner == displayedShips[i])
                labelOwners[i] = i;
        }

        // Preserve membership within the wider leave thresholds.
        for (int i = 0; i < displayedShips.Count; i++)
        {
            if (!groupingCandidates[i] || labelOwners[i] >= 0) continue;
            if (!previousOwners.TryGetValue(displayedShips[i], out var owner)) continue;
            int ownerIndex = displayedShips.IndexOf(owner);
            if (ownerIndex >= 0 && labelOwners[ownerIndex] == ownerIndex &&
                CanShareLabel(i, ownerIndex, true, forPointers))
                labelOwners[i] = ownerIndex;
        }

        // A newly close group may contain multiple previous owners. Merge only
        // when every member fits the surviving owner's radius, avoiding chains
        // that could swallow a stray cop far from the actual label.
        for (int i = 0; i < displayedShips.Count; i++)
        {
            if (labelOwners[i] != i) continue;
            for (int j = i + 1; j < displayedShips.Count; j++)
            {
                if (labelOwners[j] != j) continue;
                bool fits = true;
                for (int k = 0; k < displayedShips.Count; k++)
                    if (labelOwners[k] == j && !CanShareLabel(k, i, false, forPointers))
                    {
                        fits = false;
                        break;
                    }
                if (!fits) continue;
                for (int k = 0; k < displayedShips.Count; k++)
                    if (labelOwners[k] == j) labelOwners[k] = i;
            }
        }

        // Unassigned cops join a nearby owner or receive their own label.
        for (int i = 0; i < displayedShips.Count; i++)
        {
            if (!groupingCandidates[i] || labelOwners[i] >= 0) continue;
            for (int j = 0; j < displayedShips.Count; j++)
            {
                if (labelOwners[j] == j && CanShareLabel(i, j, false, forPointers))
                {
                    labelOwners[i] = j;
                    break;
                }
            }
            if (labelOwners[i] < 0) labelOwners[i] = i;
        }

        previousOwners.Clear();
        for (int i = 0; i < displayedShips.Count; i++)
        {
            int owner = labelOwners[i];
            bool show = owner < 0 || owner == i;
            if (forPointers)
            {
                if (pointers[i]) pointers[i].SetDistanceLabelVisible(show && HasPointerLabel(i));
            }
            else if (beacons[i]) beacons[i].SetDistanceLabelVisible(show);
            if (owner >= 0) previousOwners[displayedShips[i]] = displayedShips[owner];
        }
    }

    private bool CanShareLabel(int member, int owner, bool wasMember, bool forPointers)
    {
        // An incoming reinforcement squad must keep its own arrival label.
        if (chaseManager && chaseManager.IncomingPresence &&
            (displayedShips[member] == chaseManager.IncomingPresence ||
             displayedShips[owner] == chaseManager.IncomingPresence)) return false;
        float worldLimit = Mathf.Max(0f, groupJoinDistance);
        float screenLimit = Mathf.Max(0f, forPointers ? pointerLabelJoinSpacing : labelJoinSpacing);
        if (wasMember)
        {
            worldLimit = Mathf.Max(worldLimit, groupLeaveDistance);
            screenLimit = Mathf.Max(screenLimit, forPointers ? pointerLabelLeaveSpacing : labelLeaveSpacing);
        }
        Vector2 memberPosition = forPointers ? pointers[member].CanvasPosition : beacons[member].CanvasPosition;
        Vector2 ownerPosition = forPointers ? pointers[owner].CanvasPosition : beacons[owner].CanvasPosition;
        return (displayedShips[member].transform.position - displayedShips[owner].transform.position).sqrMagnitude
                <= worldLimit * worldLimit &&
            (memberPosition - ownerPosition).sqrMagnitude
                <= screenLimit * screenLimit;
    }

    private bool EnsureSlot(int index)
    {
        bool needsPointer = index >= pointers.Count || !pointers[index];
        bool needsBeacon = index >= beacons.Count || !beacons[index];
        if ((needsPointer && (!pointerPrefab || !pointerParent)) ||
            (needsBeacon && (!beaconPrefab || !beaconParent)))
        {
            if (!reportedCapacityWarning)
                Debug.LogWarning("PoliceMarkerManager: not enough marker slots. Assign both police prefabs to allow additional ships.", this);
            reportedCapacityWarning = true;
            return false;
        }

        if (needsPointer)
        {
            var pointer = Instantiate(pointerPrefab, pointerParent, false);
            pointer.SetSlotActive(false);
            if (index < pointers.Count) pointers[index] = pointer;
            else pointers.Add(pointer);
        }
        if (needsBeacon)
        {
            var beacon = Instantiate(beaconPrefab, beaconParent, false);
            beacon.SetSlotActive(false);
            if (index < beacons.Count) beacons[index] = beacon;
            else beacons.Add(beacon);
        }
        return true;
    }

    private void HideAll()
    {
        foreach (var pointer in pointers)
            if (pointer) pointer.SetSlotActive(false);
        foreach (var beacon in beacons)
            if (beacon) beacon.SetSlotActive(false);
    }

    private void OnDisable()
    {
        previousOwners.Clear();
        previousPointerOwners.Clear();
        displayedShips.Clear();
        HideAll();
    }
}
