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

    [Header("Quest Selection")]
    public int globalSeed = 12345;

    [Tooltip("Search stations within this radius of the player.")]
    public float pickRadius = 6000f;

    [Tooltip("Don't pick a station too close to the player.")]
    public float minTargetDistance = 1500f;

    [Tooltip("Max attempts to pick a new target (avoid same coord).")]
    public int pickAttempts = 8;

    [Header("Arrival / Trigger")]
    [Tooltip("When within this distance, hide beacon and arm target orbit trigger (if proxy exists).")]
    public float armTriggerDistance = 900f;

    [Tooltip("Optional: only count arrival when this tag enters the trigger.")]
    public string requiredPlayerTag = "";

    [Header("Debug")]
    public bool logPicks = true;

    private bool _hasTarget;
    private StationPosManager.StationWorldInfo _target;

    private Vector3Int? _lastTargetCoord;
    private int _questIndex = 0;

    private readonly List<StationPosManager.StationWorldInfo> _tmpStations = new(256);
    public bool HasTarget => _hasTarget;
    public Vector3 TargetWorldPos => _target.worldPos;
    public Vector3Int TargetCoord => _target.coord;

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

        float dist = Vector3.Distance(player.position, _target.worldPos);

        // Only arm triggers when close enough.
        if (dist <= armTriggerDistance)
        {
            TryArmArrivalTriggerOnTargetProxy();
        }
    }

    public void PickNewTarget()
    {
        _questIndex++;

        if (!posManager)
        {
            _hasTarget = false;
            return;
        }

        // Collect stations
        _tmpStations.Clear();
        posManager.FillActiveStations(_tmpStations);

        if (_tmpStations.Count == 0)
        {
            _hasTarget = false;
            if (logPicks) Debug.Log("[StationQuestNavigator] No active stations found (yet).");
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

        // Fallback: pick first valid by scan
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
            ClearAllProxyHighlights();
            return;
        }

        _target = chosen;
        _hasTarget = true;
        _lastTargetCoord = chosen.coord;

        if (logPicks)
            Debug.Log($"[StationQuestNavigator] New target station coord={_target.coord} pos={_target.worldPos}");

        ApplyTargetHighlightOnly(_target.coord);
        DisarmOldTargetTriggers();
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

        PickNewTarget();
    }
}
