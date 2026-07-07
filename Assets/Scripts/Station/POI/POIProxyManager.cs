using System;
using System.Collections.Generic;
using UnityEngine;

public class POIProxyManager : MonoBehaviour
{
    [Serializable]
    public class POIPrefabEntry
    {
        public POIType type = POIType.Anomaly;
        public GameObject prefab;

        [Min(0)]
        public int weight = 1;

        [Header("Pose Offset")]
        public Vector3 localPositionOffset = Vector3.zero;
        public Vector3 localEulerOffset = Vector3.zero;

        [Header("Scale")]
        public bool useGeneratedUniformScale = true;
        public Vector3 localScaleMultiplier = Vector3.one;
    }

    private class ActiveProxy
    {
        public Vector3Int coord;
        public POIType type;
        public GameObject prefab;
        public GameObject instance;
        public POIPrefabEntry entry;
    }

    [Header("Source")]
    public StationPosManager posManager;
    public Transform player;

    [Header("POI Prefabs")]
    public POIPrefabEntry[] prefabEntries =
    {
        new POIPrefabEntry { type = POIType.Anomaly, weight = 1 },
        new POIPrefabEntry { type = POIType.AbandonedOutpost, weight = 1 },
        new POIPrefabEntry { type = POIType.Wreckage, weight = 1 }
    };

    [Header("Runtime")]
    [Tooltip("If enabled, only keep non-station POI proxies within maxChunkRadius chunks of the player center.")]
    public bool limitByChunkRadius = true;

    [Tooltip("1 means 3x3x3 around the player. 2 means 5x5x5.")]
    [Min(0)] public int maxChunkRadius = 1;

    [Tooltip("When the player's chunk changes, re-check already-loaded POIs against the radius limit.")]
    public bool refreshLoadedPOIsOnChunkChange = true;

    [Header("Debug")]
    public bool logMissingPrefabs = true;

    private readonly Dictionary<Vector3Int, ActiveProxy> _active = new Dictionary<Vector3Int, ActiveProxy>(64);
    private readonly Dictionary<GameObject, Stack<GameObject>> _poolByPrefab = new Dictionary<GameObject, Stack<GameObject>>(16);

    private readonly List<Vector3Int> _releaseBuffer = new List<Vector3Int>(64);
    private readonly HashSet<Vector3Int> _seenLoadedCoords = new HashSet<Vector3Int>();
    private readonly HashSet<POIType> _warnedMissingPrefabTypes = new HashSet<POIType>();

    private Vector3Int _lastPlayerChunk;

    private void Awake()
    {
        if (!player && posManager)
            player = posManager.player;

        if (!player)
            player = Camera.main ? Camera.main.transform : transform;

        maxChunkRadius = Mathf.Max(0, maxChunkRadius);

        if (posManager && player)
            _lastPlayerChunk = posManager.WorldToChunkCoord(player.position);
    }

    private void OnEnable()
    {
        if (!posManager)
            return;

        posManager.OnPOIChunkCreated += HandlePOIChunkCreated;
        posManager.OnPOIChunkRemoved += HandlePOIChunkRemoved;

        RefreshFromLoadedPOIs();
    }

    private void OnDisable()
    {
        if (posManager)
        {
            posManager.OnPOIChunkCreated -= HandlePOIChunkCreated;
            posManager.OnPOIChunkRemoved -= HandlePOIChunkRemoved;
        }

        ReleaseAllActive();
    }

    private void Update()
    {
        if (!refreshLoadedPOIsOnChunkChange || !posManager || !player)
            return;

        Vector3Int center = posManager.WorldToChunkCoord(player.position);

        if (center == _lastPlayerChunk)
            return;

        _lastPlayerChunk = center;
        RefreshFromLoadedPOIs();
    }

    private void HandlePOIChunkCreated(Vector3Int coord, POIFieldData data)
    {
        if (!posManager)
            return;

        if (!data || !data.HasPOI || data.poiType == POIType.Station)
        {
            ReleaseProxy(coord);
            return;
        }

        if (limitByChunkRadius && !IsWithinChunkRadius(coord))
        {
            ReleaseProxy(coord);
            return;
        }

        POIPrefabEntry entry = PickPrefabEntry(data.poiType, coord, data);

        if (entry == null || !entry.prefab)
        {
            WarnMissingPrefabOnce(data.poiType);
            ReleaseProxy(coord);
            return;
        }

        ActiveProxy active;

        if (_active.TryGetValue(coord, out active))
        {
            if (active == null || !active.instance || active.prefab != entry.prefab)
            {
                ReleaseProxy(coord);
                active = null;
            }
        }

        if (active == null)
        {
            active = new ActiveProxy
            {
                coord = coord,
                type = data.poiType,
                prefab = entry.prefab,
                entry = entry,
                instance = GetOrCreateInstance(entry.prefab)
            };

            _active[coord] = active;
        }

        active.coord = coord;
        active.type = data.poiType;
        active.prefab = entry.prefab;
        active.entry = entry;

        ApplyPose(active, coord, data);
    }

    private void HandlePOIChunkRemoved(Vector3Int coord)
    {
        ReleaseProxy(coord);
    }

    private bool IsWithinChunkRadius(Vector3Int coord)
    {
        if (!player || !posManager)
            return true;

        Vector3Int center = posManager.WorldToChunkCoord(player.position);

        int dx = Mathf.Abs(coord.x - center.x);
        int dy = Mathf.Abs(coord.y - center.y);
        int dz = Mathf.Abs(coord.z - center.z);

        return dx <= maxChunkRadius && dy <= maxChunkRadius && dz <= maxChunkRadius;
    }

    private POIPrefabEntry PickPrefabEntry(POIType type, Vector3Int coord, POIFieldData data)
    {
        if (prefabEntries == null || prefabEntries.Length == 0)
            return null;

        int totalWeight = 0;

        for (int i = 0; i < prefabEntries.Length; i++)
        {
            POIPrefabEntry entry = prefabEntries[i];

            if (entry == null || !entry.prefab)
                continue;

            if (entry.type != type)
                continue;

            totalWeight += Mathf.Max(0, entry.weight);
        }

        if (totalWeight <= 0)
            return null;

        int seed = GetPrefabPickSeed(type, coord, data);
        System.Random rng = new System.Random(seed);

        int roll = rng.Next(0, totalWeight);
        int cursor = 0;

        for (int i = 0; i < prefabEntries.Length; i++)
        {
            POIPrefabEntry entry = prefabEntries[i];

            if (entry == null || !entry.prefab)
                continue;

            if (entry.type != type)
                continue;

            cursor += Mathf.Max(0, entry.weight);

            if (roll < cursor)
                return entry;
        }

        return null;
    }

    private int GetPrefabPickSeed(POIType type, Vector3Int coord, POIFieldData data)
    {
        unchecked
        {
            int h = data ? data.seed : posManager ? posManager.globalSeed : 12345;

            h = (h * 397) ^ coord.x;
            h = (h * 397) ^ coord.y;
            h = (h * 397) ^ coord.z;
            h = (h * 397) ^ (int)type;

            return h;
        }
    }

    private GameObject GetOrCreateInstance(GameObject prefab)
    {
        if (!prefab)
            return null;

        Stack<GameObject> pool;

        if (_poolByPrefab.TryGetValue(prefab, out pool) && pool.Count > 0)
        {
            GameObject pooled = pool.Pop();

            if (pooled)
            {
                pooled.transform.SetParent(transform, false);
                pooled.SetActive(true);
                return pooled;
            }
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(true);
        return instance;
    }

    private void ApplyPose(ActiveProxy active, Vector3Int coord, POIFieldData data)
    {
        if (active == null || !active.instance || active.entry == null || !data || !posManager)
            return;

        Vector3 origin = posManager.ChunkCoordToWorldOrigin(coord);

        Vector3 basePos = data.WorldPosition(origin);
        Quaternion baseRot = data.WorldRotation();

        Vector3 finalPos = basePos + (baseRot * active.entry.localPositionOffset);
        Quaternion finalRot = baseRot * Quaternion.Euler(active.entry.localEulerOffset);

        active.instance.transform.SetPositionAndRotation(finalPos, finalRot);

        Vector3 prefabScale = active.prefab ? active.prefab.transform.localScale : Vector3.one;
        Vector3 scale = Vector3.Scale(prefabScale, active.entry.localScaleMultiplier);

        if (active.entry.useGeneratedUniformScale)
            scale *= Mathf.Max(0.0001f, data.uniformScale);

        active.instance.transform.localScale = scale;
    }

    public bool TryGetProxy(Vector3Int coord, out GameObject proxy)
    {
        proxy = null;

        ActiveProxy active;

        if (!_active.TryGetValue(coord, out active) || active == null || !active.instance)
            return false;

        proxy = active.instance;
        return true;
    }

    public void ForEachActiveProxy(Action<Vector3Int, POIType, GameObject> fn)
    {
        if (fn == null)
            return;

        foreach (var kv in _active)
        {
            ActiveProxy active = kv.Value;

            if (active != null && active.instance)
                fn(active.coord, active.type, active.instance);
        }
    }

    [ContextMenu("Refresh From Loaded POIs")]
    public void RefreshFromLoadedPOIs()
    {
        if (!posManager || posManager.POIChunks == null)
            return;

        _seenLoadedCoords.Clear();

        foreach (var kv in posManager.POIChunks)
        {
            _seenLoadedCoords.Add(kv.Key);
            HandlePOIChunkCreated(kv.Key, kv.Value);
        }

        _releaseBuffer.Clear();

        foreach (var kv in _active)
        {
            if (!_seenLoadedCoords.Contains(kv.Key))
                _releaseBuffer.Add(kv.Key);
        }

        for (int i = 0; i < _releaseBuffer.Count; i++)
            ReleaseProxy(_releaseBuffer[i]);

        _releaseBuffer.Clear();
    }

    private void ReleaseProxy(Vector3Int coord)
    {
        ActiveProxy active;

        if (!_active.TryGetValue(coord, out active))
            return;

        _active.Remove(coord);

        if (active == null || !active.instance)
            return;

        active.instance.SetActive(false);
        active.instance.transform.SetParent(transform, false);

        if (active.prefab)
        {
            Stack<GameObject> pool;

            if (!_poolByPrefab.TryGetValue(active.prefab, out pool))
            {
                pool = new Stack<GameObject>();
                _poolByPrefab[active.prefab] = pool;
            }

            pool.Push(active.instance);
        }
        else
        {
            Destroy(active.instance);
        }
    }

    [ContextMenu("Release All Active Proxies")]
    public void ReleaseAllActive()
    {
        _releaseBuffer.Clear();

        foreach (var kv in _active)
            _releaseBuffer.Add(kv.Key);

        for (int i = 0; i < _releaseBuffer.Count; i++)
            ReleaseProxy(_releaseBuffer[i]);

        _releaseBuffer.Clear();
    }

    private void WarnMissingPrefabOnce(POIType type)
    {
        if (!logMissingPrefabs)
            return;

        if (_warnedMissingPrefabTypes.Contains(type))
            return;

        _warnedMissingPrefabTypes.Add(type);
        Debug.LogWarning($"[POIProxyManager] No prefab entry assigned for POI type {type}.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxChunkRadius = Mathf.Max(0, maxChunkRadius);

        if (prefabEntries == null)
            return;

        for (int i = 0; i < prefabEntries.Length; i++)
        {
            if (prefabEntries[i] == null)
                continue;

            prefabEntries[i].weight = Mathf.Max(0, prefabEntries[i].weight);

            if (prefabEntries[i].localScaleMultiplier == Vector3.zero)
                prefabEntries[i].localScaleMultiplier = Vector3.one;
        }
    }
#endif
}