using System.Collections.Generic;
using UnityEngine;

public class BoundsMarkerUI : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public Transform player;

    [Header("Existing Marker UI")]
    public QuestPointerMarker pointerMarker;
    public QuestBeaconMarker beaconMarker;

    [Header("Safe Zones / Marker Targets")]
    public List<Collider> safeZoneColliders = new List<Collider>();

    [Tooltip("Optional. If empty, this script uses the safe zone collider transforms.")]
    public List<Transform> markerTargets = new List<Transform>();

    [Header("Behavior")]
    public bool hideMarkersWhenInsideBounds = true;

    private void Update()
    {
        if (!player || !playerCamera)
            return;

        bool insideAnySafeZone = IsInsideAnyActiveSafeZone();

        if (insideAnySafeZone && hideMarkersWhenInsideBounds)
        {
            SetMarkersActive(false);
            return;
        }

        Transform target = GetClosestActiveTarget();

        if (!target)
        {
            SetMarkersActive(false);
            return;
        }

        SetMarkersActive(true);

        if (pointerMarker)
            pointerMarker.SetTarget(playerCamera, target.position, true);

        if (beaconMarker)
            beaconMarker.SetTarget(playerCamera, target.position, true);
    }

    private bool IsInsideAnyActiveSafeZone()
    {
        foreach (Collider zone in safeZoneColliders)
        {
            if (!IsZoneUsable(zone))
                continue;

            if (zone.bounds.Contains(player.position))
                return true;
        }

        return false;
    }

    private Transform GetClosestActiveTarget()
    {
        Transform closest = null;
        float closestDistSqr = float.MaxValue;

        // Prefer custom marker targets if assigned
        if (markerTargets != null && markerTargets.Count > 0)
        {
            foreach (Transform target in markerTargets)
            {
                if (!target || !target.gameObject.activeInHierarchy)
                    continue;

                float d = (target.position - player.position).sqrMagnitude;

                if (d < closestDistSqr)
                {
                    closestDistSqr = d;
                    closest = target;
                }
            }

            return closest;
        }

        // Otherwise use active safe zone collider transforms
        foreach (Collider zone in safeZoneColliders)
        {
            if (!IsZoneUsable(zone))
                continue;

            float d = (zone.transform.position - player.position).sqrMagnitude;

            if (d < closestDistSqr)
            {
                closestDistSqr = d;
                closest = zone.transform;
            }
        }

        return closest;
    }

    private bool IsZoneUsable(Collider zone)
    {
        return zone != null &&
               zone.enabled &&
               zone.gameObject.activeInHierarchy;
    }

    private void SetMarkersActive(bool on)
    {
        if (pointerMarker)
            pointerMarker.SetSlotActive(on);

        if (beaconMarker)
            beaconMarker.SetSlotActive(on);
    }
}