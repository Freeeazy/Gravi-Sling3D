using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple pool for asteroid smash VFX.
/// Supports a normal prefab and an optional muted/no-audio prefab.
/// </summary>
public class AsteroidVFXPoolManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab with a ParticleSystem and optional audio.")]
    public GameObject vfxPrefab;

    [Tooltip("Optional prefab with the same VFX but no audio.")]
    public GameObject mutedVfxPrefab;

    [Header("Pool Settings")]
    public int prewarmCount = 10;
    public int maxPoolSize = 20;
    public bool allowExpand = true;
    public float tintPercent = 0.05f;

    [Header("Playback")]
    public bool playOnSpawn = true;

    private readonly Queue<PooledVFX> _availableNormal = new Queue<PooledVFX>();
    private readonly HashSet<PooledVFX> _inUseNormal = new HashSet<PooledVFX>();

    private readonly Queue<PooledVFX> _availableMuted = new Queue<PooledVFX>();
    private readonly HashSet<PooledVFX> _inUseMuted = new HashSet<PooledVFX>();

    private void Awake()
    {
        if (!vfxPrefab)
            Debug.LogWarning($"{nameof(AsteroidVFXPoolManager)}: No normal vfxPrefab assigned.", this);

        if (!mutedVfxPrefab)
            Debug.LogWarning($"{nameof(AsteroidVFXPoolManager)}: No mutedVfxPrefab assigned. Muted spawns will fall back to normal VFX.", this);

        PrewarmNormal(prewarmCount);
        PrewarmMuted(prewarmCount);
    }

    public void PrewarmNormal(int count)
    {
        PrewarmPool(count, false);
    }

    public void PrewarmMuted(int count)
    {
        PrewarmPool(count, true);
    }

    private void PrewarmPool(int count, bool muted)
    {
        GameObject prefab = GetPrefab(muted);
        if (!prefab) return;

        Queue<PooledVFX> available = GetAvailableQueue(muted);
        HashSet<PooledVFX> inUse = GetInUseSet(muted);

        count = Mathf.Clamp(count, 0, maxPoolSize);

        while (available.Count + inUse.Count < count)
        {
            PooledVFX inst = CreateInstance(prefab, muted);
            if (!inst) break;

            ReturnToPool(inst);
        }
    }

    public void Spawn(Vector3 position, Quaternion rotation)
    {
        SpawnInternal(position, rotation, false);
    }

    public void SpawnMuted(Vector3 position, Quaternion rotation)
    {
        SpawnInternal(position, rotation, true);
    }

    public void Spawn(Vector3 position)
    {
        Spawn(position, Quaternion.identity);
    }

    public void SpawnMuted(Vector3 position)
    {
        SpawnMuted(position, Quaternion.identity);
    }

    private void SpawnInternal(Vector3 position, Quaternion rotation, bool muted)
    {
        PooledVFX vfx = GetFromPool(muted);
        if (!vfx) return;

        Transform t = vfx.transform;
        t.SetPositionAndRotation(position, rotation);
        t.gameObject.SetActive(true);

        if (playOnSpawn)
            vfx.PlayAll();
    }

    public void SpawnImpact(
        Vector3 position,
        Vector3 smashDirWorld,
        float dirSpeed,
        float radialSpeed,
        float randomSpeed,
        int count,
        Color tint)
    {
        SpawnImpactInternal(position, smashDirWorld, dirSpeed, radialSpeed, randomSpeed, count, tint, false);
    }

    public void SpawnImpactMuted(
        Vector3 position,
        Vector3 smashDirWorld,
        float dirSpeed,
        float radialSpeed,
        float randomSpeed,
        int count,
        Color tint)
    {
        SpawnImpactInternal(position, smashDirWorld, dirSpeed, radialSpeed, randomSpeed, count, tint, true);
    }

    private void SpawnImpactInternal(
        Vector3 position,
        Vector3 smashDirWorld,
        float dirSpeed,
        float radialSpeed,
        float randomSpeed,
        int count,
        Color tint,
        bool muted)
    {
        PooledVFX vfx = GetFromPool(muted);
        if (!vfx) return;

        vfx.transform.SetPositionAndRotation(position, Quaternion.identity);
        vfx.gameObject.SetActive(true);

        vfx.PlayImpactBurst(smashDirWorld, dirSpeed, radialSpeed, randomSpeed, count, tint, tintPercent);
    }

    private PooledVFX GetFromPool(bool muted)
    {
        Queue<PooledVFX> available = GetAvailableQueue(muted);
        HashSet<PooledVFX> inUse = GetInUseSet(muted);
        GameObject prefab = GetPrefab(muted);

        if (!prefab)
            return null;

        if (available.Count > 0)
        {
            PooledVFX vfx = available.Dequeue();
            inUse.Add(vfx);
            return vfx;
        }

        int total = available.Count + inUse.Count;

        if (allowExpand && total < maxPoolSize)
        {
            PooledVFX vfx = CreateInstance(prefab, muted);
            if (!vfx) return null;

            inUse.Add(vfx);
            return vfx;
        }

        return null;
    }

    private PooledVFX CreateInstance(GameObject prefab, bool muted)
    {
        if (!prefab)
            return null;

        GameObject go = Instantiate(prefab, transform);
        go.name = $"{prefab.name}_Pooled";

        PooledVFX pooled = go.GetComponent<PooledVFX>();
        if (!pooled)
            pooled = go.AddComponent<PooledVFX>();

        pooled.Bind(this, muted);

        go.SetActive(false);
        return pooled;
    }

    internal void ReturnToPool(PooledVFX vfx)
    {
        if (!vfx) return;

        Queue<PooledVFX> available = GetAvailableQueue(vfx.IsMuted);
        HashSet<PooledVFX> inUse = GetInUseSet(vfx.IsMuted);

        int total = available.Count + inUse.Count;

        if (total > maxPoolSize)
        {
            inUse.Remove(vfx);
            Destroy(vfx.gameObject);
            return;
        }

        inUse.Remove(vfx);

        vfx.StopAndClear();
        vfx.transform.SetParent(transform, worldPositionStays: false);
        vfx.gameObject.SetActive(false);

        if (!available.Contains(vfx))
            available.Enqueue(vfx);
    }

    private GameObject GetPrefab(bool muted)
    {
        if (muted && mutedVfxPrefab)
            return mutedVfxPrefab;

        return vfxPrefab;
    }

    private Queue<PooledVFX> GetAvailableQueue(bool muted)
    {
        return muted ? _availableMuted : _availableNormal;
    }

    private HashSet<PooledVFX> GetInUseSet(bool muted)
    {
        return muted ? _inUseMuted : _inUseNormal;
    }

    public class PooledVFX : MonoBehaviour
    {
        private AsteroidVFXPoolManager _pool;
        private ParticleSystem[] _systems;
        private bool _hasReturned;

        public bool IsMuted { get; private set; }

        public void Bind(AsteroidVFXPoolManager pool, bool muted)
        {
            _pool = pool;
            IsMuted = muted;
            _systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            _hasReturned = false;
        }

        private void OnEnable()
        {
            _hasReturned = false;
        }

        public void PlayAll()
        {
            if (_systems == null)
                _systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            for (int i = 0; i < _systems.Length; i++)
            {
                if (!_systems[i]) continue;

                _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _systems[i].Clear(true);
                _systems[i].Play(true);
            }
        }

        public void PlayImpactBurst(
            Vector3 smashDirWorld,
            float dirSpeed,
            float radialSpeed,
            float randomSpeed,
            int count,
            Color baseTint,
            float tintJitterPct = 0.05f)
        {
            if (_systems == null || _systems.Length == 0)
                _systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            if (_systems == null || _systems.Length == 0)
                return;

            ParticleSystem ps = _systems[0];

            smashDirWorld = smashDirWorld.sqrMagnitude > 1e-6f
                ? smashDirWorld.normalized
                : Vector3.forward;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = true
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 radial = Random.onUnitSphere * radialSpeed;
                Vector3 directional = smashDirWorld * dirSpeed;
                Vector3 chaos = Random.onUnitSphere * randomSpeed;

                emitParams.velocity = radial + directional + chaos;
                emitParams.startColor = VaryColor(baseTint, tintJitterPct);

                ps.Emit(emitParams, 1);
            }

            ps.Play(true);
        }

        public void StopAndClear()
        {
            if (_systems == null)
                _systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            for (int i = 0; i < _systems.Length; i++)
            {
                if (!_systems[i]) continue;

                _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _systems[i].Clear(true);
            }
        }

        public void SetTint(Color c)
        {
            if (_systems == null)
                _systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            for (int i = 0; i < _systems.Length; i++)
            {
                if (!_systems[i]) continue;

                ParticleSystem.MainModule main = _systems[i].main;

                Color baseCol = Color.white;

                ParticleSystem.MinMaxGradient existing = main.startColor;
                if (existing.mode == ParticleSystemGradientMode.Color)
                    baseCol = existing.color;

                c.a = baseCol.a;
                main.startColor = new ParticleSystem.MinMaxGradient(c);
            }
        }

        private void OnParticleSystemStopped()
        {
            if (_hasReturned)
                return;

            if (_pool == null)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            _hasReturned = true;
            _pool.ReturnToPool(this);
        }

        private static Color VaryColor(Color baseCol, float pct)
        {
            float r = 1f + Random.Range(-pct, pct);
            float g = 1f + Random.Range(-pct, pct);
            float b = 1f + Random.Range(-pct, pct);

            Color c = new Color(
                baseCol.r * r,
                baseCol.g * g,
                baseCol.b * b,
                baseCol.a
            );

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);

            return c;
        }
    }
}