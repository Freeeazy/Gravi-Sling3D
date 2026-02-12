using System;
using System.Collections.Generic;
using UnityEngine;

public class StationQuestNavigator : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera cam;

    [Tooltip("Reads station locations from here.")]
    public StationPosManager posManager;

    [Tooltip("Used to find the target station proxy to highlight + arm trigger.")]
    public StationProxyManager proxyManager;

    [Tooltip("World-space beacon prefab (quad/sphere) - ideally billboards to camera.")]
    public GameObject questBeaconPrefab;

    [Header("Quest Selection")]
    public int globalSeed = 12345;

    [Tooltip("Search stations within this radius of the player.")]
    public float pickRadius = 6000f;

    [Tooltip("Don't pick a station too close to the player.")]
    public float minTargetDistance = 1500f;

    [Tooltip("Max attempts to pick a new target (avoid same coord).")]
    public int pickAttempts = 8;

    [Header("Beacon Placement (inside far clip)")]
    [Range(0.1f, 0.95f)] public float beaconDepthFrac = 0.8f;
    public float beaconMaxDistance = 800f;

    [Header("Arrival / Trigger")]
    [Tooltip("When within this distance, hide beacon and arm target orbit trigger (if proxy exists).")]
    public float armTriggerDistance = 900f;

    [Tooltip("Optional: only count arrival when this tag enters the trigger.")]
    public string requiredPlayerTag = "";

    [Header("Debug")]
    public bool logPicks = true;

    private GameObject _beacon;

    private bool _hasTarget;
    private StationPosManager.StationWorldInfo _target;

    private Vector3Int? _lastTargetCoord;
    private int _questIndex = 0;

    private readonly List<StationPosManager.StationWorldInfo> _tmpStations = new(256);

    private void Awake()
    {
        if (!player) player = Camera.main ? Camera.main.transform : transform;
        if (!cam) cam = Camera.main;
    }

    private void Start()
    {
        StartCoroutine(StartupRoutine());
    }
    private System.Collections.IEnumerator StartupRoutine()
    {
        // Wait at least one frame so other Start() methods run
        yield return null;

        EnsureBeacon();

        // Wait until StationPosManager has generated something usable
        // (this also handles cases where generation happens a frame later)
        const float timeoutSeconds = 5f;
        float t = 0f;

        while (t < timeoutSeconds)
        {
            if (posManager)
            {
                _tmpStations.Clear();
                posManager.FillActiveStations(_tmpStations);

                if (_tmpStations.Count > 0)
                    break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Now safe to pick.
        PickNewTarget();
    }

    private void Update()
    {
        if (!_hasTarget || !player || !cam || !posManager) return;

        UpdateBeacon();

        float dist = Vector3.Distance(player.position, _target.worldPos);

        if (dist <= armTriggerDistance)
        {
            if (_beacon && _beacon.activeSelf) _beacon.SetActive(false);
            TryArmArrivalTriggerOnTargetProxy();
        }
        else
        {
            if (_beacon && !_beacon.activeSelf) _beacon.SetActive(true);
        }
    }

    private void EnsureBeacon()
    {
        if (_beacon) return;

        if (!questBeaconPrefab)
        {
            Debug.LogError("[StationQuestNavigator] questBeaconPrefab not assigned.");
            return;
        }

        _beacon = Instantiate(questBeaconPrefab);
        _beacon.name = "StationQuestBeacon";
        _beacon.SetActive(true);
    }

    private void UpdateBeacon()
    {
        if (!_beacon) return;

        Vector3 from = player.position;
        Vector3 to = _target.worldPos;

        Vector3 dir = (to - from);
        if (dir.sqrMagnitude < 0.0001f)
            dir = player.forward;

        dir.Normalize();

        float far = cam.farClipPlane;
        float d = Mathf.Min(far * beaconDepthFrac, beaconMaxDistance);

        _beacon.transform.position = from + dir * d;
    }

    public void PickNewTarget()
    {
        _questIndex++;

        if (!posManager)
        {
            _hasTarget = false;
            if (_beacon) _beacon.SetActive(false);
            return;
        }

        // Collect stations
        _tmpStations.Clear();
        posManager.FillActiveStations(_tmpStations);

        if (_tmpStations.Count == 0)
        {
            _hasTarget = false;
            if (logPicks) Debug.Log("[StationQuestNavigator] No active stations found (yet).");
            if (_beacon) _beacon.SetActive(false);
            ClearAllProxyHighlights();
            return;
        }

        // Filter by distance constraints
        Vector3 center = player.position;
        float r2 = pickRadius * pickRadius;
        float min2 = minTargetDistance * minTargetDistance;

        // Deterministic-ish RNG per quest
        int pickSeed = unchecked(globalSeed * 73856093 ^ _questIndex * 19349663);
        var rng = new System.Random(pickSeed);

        StationPosManager.StationWorldInfo chosen = default;
        bool found = false;

        for (int attempt = 0; attempt < Mathf.Max(1, pickAttempts); attempt++)
        {
            var cand = _tmpStations[rng.Next(0, _tmpStations.Count)];

            float d2 = (cand.worldPos - center).sqrMagnitude;
            if (d2 > r2) continue;
            if (d2 < min2) continue;

            if (_lastTargetCoord.HasValue && cand.coord == _lastTargetCoord.Value)
                continue;

            chosen = cand;
            found = true;
            break;
        }

        // Fallback: pick first valid by scan (still respects constraints)
        if (!found)
        {
            for (int i = 0; i < _tmpStations.Count; i++)
            {
                var cand = _tmpStations[i];
                float d2 = (cand.worldPos - center).sqrMagnitude;
                if (d2 > r2) continue;
                if (d2 < min2) continue;

                chosen = cand;
                found = true;
                break;
            }
        }

        if (!found)
        {
            _hasTarget = false;
            if (logPicks) Debug.Log("[StationQuestNavigator] No stations met distance constraints. Try larger pickRadius or smaller minTargetDistance.");
            if (_beacon) _beacon.SetActive(false);
            ClearAllProxyHighlights();
            return;
        }

        _target = chosen;
        _hasTarget = true;
        _lastTargetCoord = chosen.coord;

        if (_beacon) _beacon.SetActive(true);

        if (logPicks)
            Debug.Log($"[StationQuestNavigator] New target station coord={_target.coord} pos={_target.worldPos}");

        ApplyTargetHighlightOnly(_target.coord);
        DisarmOldTargetTriggers(); // optional cleanup hook
    }

    private void ApplyTargetHighlightOnly(Vector3Int targetCoord)
    {
        // We only know about spawned proxies. If the target proxy isn't spawned yet,
        // it'll get highlighted the first frame it exists (we also try in TryArm...).
        if (!proxyManager) return;

        // Turn off all current proxy highlights
        // (Small proxy count, so brute force is fine.)
        ClearAllProxyHighlights();

        if (proxyManager.TryGetProxy(targetCoord, out var proxy))
            proxy.SetQuestHighlight(true);
    }

    private void ClearAllProxyHighlights()
    {
        if (!proxyManager) return;
        proxyManager.ForEachActiveProxy(p => p.SetQuestHighlight(false));
    }

    private void TryArmArrivalTriggerOnTargetProxy()
    {
        if (!proxyManager) return;

        if (!proxyManager.TryGetProxy(_target.coord, out var proxy) || !proxy)
            return;

        // Ensure highlight is on (in case proxy spawned after target pick)
        proxy.SetQuestHighlight(true);

        if (!proxy.orbitTrigger) return;

        var trigger = proxy.orbitTrigger.GetComponent<StationQuestArrivedTrigger>();
        if (!trigger)
            trigger = proxy.orbitTrigger.gameObject.AddComponent<StationQuestArrivedTrigger>();

        trigger.Init(this, _target.coord, requiredPlayerTag);
    }

    private void DisarmOldTargetTriggers()
    {
        // Optional: If you want to *actively* turn off older triggers,
        // we'd need proxyManager enumeration + disable/remove StationQuestArrivedTrigger.
        // Totally safe to skip because the trigger checks the coord in NotifyArrived.
    }

    public void NotifyArrived(Vector3Int coord)
    {
        if (!_hasTarget) return;
        if (coord != _target.coord) return;

        if (logPicks)
            Debug.Log($"[StationQuestNavigator] Arrived at station coord={coord}. Picking new target...");

        if (_beacon) _beacon.SetActive(false);

        PickNewTarget();
    }
}
