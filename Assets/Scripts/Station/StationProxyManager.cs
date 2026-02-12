using System;
using System.Collections.Generic;
using UnityEngine;

public class StationProxyManager : MonoBehaviour
{
    [Header("Source")]
    public StationPosManager posManager;
    public Transform player;

    [Header("Proxy Prefab")]
    public StationProxy proxyPrefab;

    [Header("Runtime")]
    [Tooltip("Optional: only keep proxies within this many chunks of the player center. 1 means 3x3x3.")]
    public int maxChunkRadius = 1;

    private readonly Dictionary<Vector3Int, StationProxy> _active = new();
    private readonly Stack<StationProxy> _pool = new();

    private void OnEnable()
    {
        if (!posManager) return;
        posManager.OnChunkCreated += HandleChunkCreated;

        // Bootstrap existing chunks so we don't miss the initial grid
        if (posManager.Chunks != null && player)
        {
            foreach (var kv in posManager.Chunks)
                HandleChunkCreated(kv.Key, kv.Value);
        }
    }

    private void OnDisable()
    {
        if (!posManager) return;
        posManager.OnChunkCreated -= HandleChunkCreated;
    }

    private void HandleChunkCreated(Vector3Int coord, StationFieldData data)
    {
        if (!player || !posManager) return;

        // Optional: keep proxy count bounded by distance-in-chunks
        Vector3Int center = posManager.WorldToChunkCoord(player.position);
        int dx = Mathf.Abs(coord.x - center.x);
        int dy = Mathf.Abs(coord.y - center.y);
        int dz = Mathf.Abs(coord.z - center.z);
        bool within = (dx <= maxChunkRadius && dy <= maxChunkRadius && dz <= maxChunkRadius);

        bool shouldHaveProxy = data && data.hasStation && within;

        if (!shouldHaveProxy)
        {
            ReleaseProxy(coord);
            return;
        }

        // Ensure proxy exists
        var proxy = GetOrCreateProxy(coord);

        // Compute world pose
        Vector3 origin = posManager.ChunkCoordToWorldOrigin(coord);
        Vector3 worldPos = data.WorldPosition(origin);
        Quaternion worldRot = data.WorldRotation(); // currently returns localRotation

        // Apply pose + config
        proxy.Assign(coord, worldPos, worldRot, data);
    }

    private StationProxy GetOrCreateProxy(Vector3Int coord)
    {
        if (_active.TryGetValue(coord, out var existing) && existing)
            return existing;

        StationProxy p = (_pool.Count > 0) ? _pool.Pop() : Instantiate(proxyPrefab, transform);
        p.gameObject.SetActive(true);
        _active[coord] = p;
        return p;
    }
    public bool TryGetProxy(Vector3Int coord, out StationProxy proxy)
    {
        return _active.TryGetValue(coord, out proxy) && proxy;
    }
    public void ForEachActiveProxy(Action<StationProxy> fn)
    {
        foreach (var kv in _active)
        {
            if (kv.Value) fn?.Invoke(kv.Value);
        }
    }
    private void ReleaseProxy(Vector3Int coord)
    {
        if (!_active.TryGetValue(coord, out var p) || !p) return;

        _active.Remove(coord);
        p.gameObject.SetActive(false);
        _pool.Push(p);
    }
}
