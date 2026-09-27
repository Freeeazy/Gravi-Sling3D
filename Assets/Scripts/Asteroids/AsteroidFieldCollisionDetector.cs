using UnityEngine;

/// <summary>
/// Runtime collision detector for instanced asteroids.
/// Reads AsteroidFieldData (positions/scales/baseRadii) and resolves collisions
/// against a player BoxCollider / Rigidbody without spawning asteroid GameObjects.
/// </summary>
public class AsteroidFieldCollisionDetector : MonoBehaviour
{
    [Header("References")]
    public AsteroidFieldData fieldData;
    public SimpleMove simpleMove;
    public Material shieldMaterial;

    [Tooltip("Player Rigidbody (likely the same child RB used by SimpleMove).")]
    public Rigidbody playerRb;

    [Tooltip("Player BoxCollider used for collisions.")]
    public BoxCollider playerBox;

    [Header("Grid")]
    [Tooltip("Legacy Inspector field. Spatial index cell size now comes from AsteroidFieldData / generator Grid Cell Size.")]
    public float cellSize = 20f;

    [Header("Chunk Space (local->world)")]
    public Vector3 chunkWorldOrigin;

    [Tooltip("How many neighbor cells to scan in each axis (1 = 3x3x3 = 27 cells).")]
    [Range(0, 3)] public int neighborRadius = 1;

    [Header("Collision Response")]
    [Tooltip("Extra separation added when resolving (prevents re-penetration jitter).")]
    public float separationSlop = 0.02f;

    [Tooltip("How much velocity to remove along the collision normal (0..1).")]
    [Range(0f, 1f)] public float normalDamp = 0.65f;

    [Tooltip("If true, also damp tangential velocity a bit (space 'scrape' feel).")]
    [Range(0f, 1f)] public float tangentDamp = 0.05f;

    [Tooltip("Max number of collisions resolved per FixedUpdate (safety cap).")]
    public int maxResolvesPerStep = 6;

    [Header("Smash Settings")]
    public float smashSpeedThreshold = 14f;
    public float smashMinAlignment = 0.65f;
    public float smashForwardNudge = 0.25f;
    public float extraSpeedFactor = 0.75f;         // extraSpeedFactor: how much harder side hits are vs head-on.
                                                   // 0.0 = angle doesn't matter, 1.0 = side hits need +100% speed, etc.

    [Header("Smash Momentum Loss")]
    [Tooltip("Design reference speed where smash loss reaches minimum (NOT a cap).")]
    public float smashDesignMaxSpeed = 250f;

    [Tooltip("Velocity loss when smashing at threshold speed (0.25 = lose 25%).")]
    [Range(0f, 1f)] public float smashLossAtThreshold = 0.35f;

    [Tooltip("Velocity loss when smashing at/above design max speed.")]
    [Range(0f, 1f)] public float smashLossAtMax = 0.06f;

    [Tooltip("Curve over normalized speed t (0=threshold, 1=design max). Output 0..1.")]
    public AnimationCurve smashLossCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Optional: keep at least this fraction of your speed after smash.")]
    [Range(0f, 1f)] public float smashMinSpeedFraction = 0.15f;

    [Tooltip("Renderer used to hide smashed instances (instanced rendering 'deletion').")]
    public AsteroidFieldInstancedRenderer instancedRenderer;

    [Tooltip("Place Asteroid VFX Particle Systems when asteroids are destroyed")]
    public AsteroidVFXPoolManager smashVfxPool;

    [Header("Density")]
    public AsteroidPosManager posManager;

    [Tooltip("How many asteroids are currently allowed to collide/render in this chunk.")]
    public int visibleCount = int.MaxValue;

    private void Awake()
    {
        if (!posManager && instancedRenderer) posManager = instancedRenderer.posManager;
    }

    // Compatibility entry points: selecting/revisiting a chunk does not reset its state.
    public void SetCurrentChunk(AsteroidFieldData data, Vector3 origin, int newVisibleCount)
    {
        fieldData = data;
        chunkWorldOrigin = origin;
        visibleCount = Mathf.Max(0, newVisibleCount);
    }

    public void Rebuild(int newVisibleCount)
    {
        visibleCount = Mathf.Max(0, newVisibleCount);
        // Geometry-array replacement is detected by Runtime. For edits to existing array
        // elements, call fieldData.InvalidateRuntimeCache() explicitly before this method.
        if (fieldData) _ = fieldData.Runtime;
    }

    private void FixedUpdate()
    {
        if (!playerRb || !playerBox) return;
        QueryLoadedChunks(playerBox.bounds, false);
    }

    private void QueryLoadedChunks(Bounds playerBounds, bool drawOnly)
    {
        int resolves = 0;
        if (posManager)
        {
            // Keep the player's own chunk first, preserving the previous priority.
            Vector3 trackedPosition = posManager.player ? posManager.player.position : playerBounds.center;
            Vector3Int currentCoord = posManager.WorldToChunkCoord(trackedPosition);
            if (posManager.Chunks.TryGetValue(currentCoord, out var currentData) && currentData)
            {
                Vector3 origin = posManager.ChunkCoordToWorldOrigin(currentCoord);
                int count = posManager.GetVisibleCountForChunk(currentCoord, currentData);
                if (!drawOnly) SetCurrentChunk(currentData, origin, count);
                if (QueryChunk(currentData, origin, count, playerBounds, drawOnly, ref resolves)) return;
            }

            // Cheap chunk-bounds rejection comes before any neighboring cell scan.
            foreach (var chunk in posManager.Chunks)
            {
                if (chunk.Key == currentCoord || !chunk.Value) continue;
                if (QueryChunk(chunk.Value, posManager.ChunkCoordToWorldOrigin(chunk.Key),
                    posManager.GetVisibleCountForChunk(chunk.Key, chunk.Value),
                    playerBounds, drawOnly, ref resolves)) return;
            }
        }
        else if (fieldData)
        {
            // Preserve manually assigned/single-field use.
            QueryChunk(fieldData, chunkWorldOrigin, visibleCount, playerBounds, drawOnly, ref resolves);
        }
    }

    private bool QueryChunk(AsteroidFieldData data, Vector3 origin, int allowedCount,
        Bounds playerBounds, bool drawOnly, ref int resolves)
    {
        if (data.count <= 0 || allowedCount <= 0) return false;
        AsteroidFieldData.RuntimeCache runtime = data.Runtime;
        int count = Mathf.Min(allowedCount, runtime.Count);
        if (count <= 0) return false;

        Bounds localPlayer = playerBounds;
        localPlayer.center -= origin;
        if (!runtime.CollisionBounds.Intersects(localPlayer)) return false;

        // Radius expansion already covers overlapping spheres; neighborRadius is kept
        // as an optional extra margin to preserve existing Inspector configurations.
        float expand = runtime.MaxRadius + Mathf.Max(0f, separationSlop);
        Vector3 extent = Vector3.one * expand;
        Vector3Int min = runtime.ToCell(localPlayer.min - extent);
        Vector3Int max = runtime.ToCell(localPlayer.max + extent);
        int margin = Mathf.Clamp(neighborRadius, 0, 3);
        min = Vector3Int.Max(min - Vector3Int.one * margin, runtime.MinCell);
        max = Vector3Int.Min(max + Vector3Int.one * margin, runtime.MaxCell);

        var destroyed = runtime.Destroyed;
        var positions = data.positions;
        var indices = runtime.CellIndices;
        var radii = runtime.Radii;
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if (!runtime.TryGetCellRange(x, y, z, out int start, out int end)) continue;
                    for (int entry = start; entry < end; entry++)
                    {
                        int i = indices[entry];
                        if (i >= count || destroyed[i]) continue;
                        if (drawOnly)
                        {
                            Gizmos.DrawWireSphere(origin + positions[i], radii[i]);
                            continue;
                        }

                        if (!SphereIntersectsAABB(positions[i], radii[i], localPlayer,
                            out Vector3 normal, out float pushDistance)) continue;
                        if (TrySmash(data, runtime, origin, i, normal)) continue;
                        Resolve(normal, pushDistance);
                        resolves++;
                        if (resolves >= Mathf.Max(1, maxResolvesPerStep)) return true;
                    }
                }
        return false;
    }

    private void Resolve(Vector3 normal, float pushDist)
    {
        // Push RB out
        Vector3 pos = playerRb.position;
        pos += normal * (pushDist + separationSlop);
        playerRb.MovePosition(pos);

        if (IsShieldActive())
            return;

        // Dampen velocity
        Vector3 v = playerRb.linearVelocity;
        float vn = Vector3.Dot(v, normal);

        // Remove some inward normal component
        if (vn < 0f)
        {
            Vector3 vN = vn * normal;
            Vector3 vT = v - vN;

            vN *= (1f - normalDamp);
            vT *= (1f - tangentDamp);

            playerRb.linearVelocity = vT + vN;
        }
    }

    // --- Sphere vs AABB test ---
    private static bool SphereIntersectsAABB(Vector3 c, float r, Bounds aabb, out Vector3 normal, out float pushDist)
    {
        // Closest point on AABB to sphere center
        Vector3 p = aabb.ClosestPoint(c);
        Vector3 d = p - c;
        float distSq = d.sqrMagnitude;

        if (distSq > r * r)
        {
            normal = Vector3.up;
            pushDist = 0f;
            return false;
        }

        if (distSq < 1e-12f)
        {
            // Sphere center lies inside the player box. Pick the minimum translation
            // that moves the BOX away from the sphere, including the sphere radius.
            Vector3 min = aabb.min;
            Vector3 max = aabb.max;
            float nearest = c.x - min.x;
            normal = Vector3.right;
            float candidate = max.x - c.x;
            if (candidate < nearest) { nearest = candidate; normal = Vector3.left; }
            candidate = c.y - min.y;
            if (candidate < nearest) { nearest = candidate; normal = Vector3.up; }
            candidate = max.y - c.y;
            if (candidate < nearest) { nearest = candidate; normal = Vector3.down; }
            candidate = c.z - min.z;
            if (candidate < nearest) { nearest = candidate; normal = Vector3.forward; }
            candidate = max.z - c.z;
            if (candidate < nearest) { nearest = candidate; normal = Vector3.back; }
            pushDist = r + Mathf.Max(0f, nearest);
            return true;
        }

        float dist = Mathf.Sqrt(distSq);
        normal = d / dist;
        pushDist = r - dist;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!playerBox) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        QueryLoadedChunks(playerBox.bounds, true);
        Gizmos.color = Color.cyan;
        Bounds bounds = playerBox.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private bool TrySmash(AsteroidFieldData hitData, AsteroidFieldData.RuntimeCache runtime,
        Vector3 hitOrigin, int index, Vector3 pushNormal)
    {
        if (smashSpeedThreshold <= 0f) return false;

        Vector3 v = playerRb.linearVelocity;
        float rbSpeed = v.magnitude;

        // If physics speed is basically stopped (damping / sleep),
        // fall back to HUD speed (already scaled & human-readable)
        float speed;

        if (rbSpeed < 1f && SpeedHUD.Instance != null)
        {
            speed = SpeedHUD.Instance.GetCurrentSpeed();
        }
        else
        {
            speed = rbSpeed;
        }

        if (speed < smashSpeedThreshold) return false;

        // pushNormal points ~ from asteroid -> player.
        // Head-on hit means velocity points into asteroid along -pushNormal.
        float align = 0f;
        if (speed > 0.0001f)
            align = Vector3.Dot(v / speed, -pushNormal);   // -1..1, usually 0..1 for contact

        align = Mathf.Clamp01((align + 1f) * 0.5f); // map -1..1 to 0..1

        // Required speed goes up as align goes down.
        float requiredSpeed = smashSpeedThreshold * (1f + (1f - align) * extraSpeedFactor);

        if (speed < requiredSpeed)
            return false;

        // Use RB velocity if we have it; otherwise infer direction from impact normal.
        // Magnitude should match our chosen 'speed' (HUD fallback).
        Vector3 effectiveVel;

        if (rbSpeed > 1e-3f)
        {
            effectiveVel = v;
        }
        else
        {
            // pushNormal is asteroid -> player, so -pushNormal roughly points "into" the asteroid.
            // For VFX we want a forward-ish direction; using -pushNormal is usually fine.
            effectiveVel = (-pushNormal) * speed;
        }

        Vector3 vel = effectiveVel;

        Vector3 velDir = (vel.sqrMagnitude > 1e-6f) ? vel.normalized : (-pushNormal);

        // PushNormal points asteroid -> player, so invert for "away from impact"
        Vector3 awayFromImpact = -pushNormal;

        // Blend: mostly velocity, slightly surface response
        Vector3 smashDir = Vector3.Normalize(velDir * 0.85f + awayFromImpact * 0.15f);

        // Scalar strength (tune this)            // How Much Speed Gets Applied - Min - Max
        float vfxSpeed = Mathf.Clamp(speed * 0.8f, 8f, 400f);

        DestroyAsteroid(hitData, runtime, hitOrigin, index, smashDir, vfxSpeed);

        // small nudge forward so we don't remain overlapping for a frame
        if (smashForwardNudge > 0f)
        {
            Vector3 dir = velDir;
            playerRb.MovePosition(playerRb.position + dir * smashForwardNudge);
        }

        // --- speed-scaled momentum loss on smash ---
        if (smashLossAtThreshold > 0f || smashLossAtMax > 0f)
        {
            float preSpeed = playerRb.linearVelocity.magnitude;

            // Normalize: 0 at threshold, 1 at design max (clamped)
            float denom = Mathf.Max(0.0001f, smashDesignMaxSpeed - smashSpeedThreshold);
            float t = Mathf.Clamp01((preSpeed - smashSpeedThreshold) / denom);

            // Curve shapes how quickly we "become a bullet"
            float k = smashLossCurve.Evaluate(t); // 0..1

            // Lerp loss: high t => closer to smashLossAtMax
            float loss = Mathf.Lerp(smashLossAtThreshold, smashLossAtMax, k);

            // Apply multiplicative shave (keeps direction)
            float keep = Mathf.Clamp01(1f - loss);

            // Optional floor so you never fully stall from a smash
            keep = Mathf.Max(keep, smashMinSpeedFraction);

            if (!IsShieldActive())
            {
                playerRb.linearVelocity *= keep;
            }

            if (SimpleFollowCamera.Instance)
            {
                // Fraction of speed removed this smash (0..1)
                float lossFrac = 1f - keep;

                float cargoSeverity = k;

                if (simpleMove != null && simpleMove.enabled && PackageDurabilityManager.Instance != null)
                {
                    bool hasPackage = PackageDurabilityManager.Instance.HasActivePackage();

                    if (hasPackage)
                    {
                        bool shieldBlocked =
                            StatManager.Instance != null &&
                            StatManager.Instance.TryConsumeShieldCharge();

                        if (!shieldBlocked)
                            PackageDurabilityManager.Instance.ApplyImpactDamage(cargoSeverity);
                    }
                }

                // Kick direction: opposite the impact (pushNormal is asteroid -> player)
                Vector3 kickDir = pushNormal;

                if (simpleMove != null && simpleMove.enabled)
                    SimpleFollowCamera.Instance.AddShakeImpulse(kickDir, lossFrac);
            }
        }

        return true;
    }
    private void DestroyAsteroid(AsteroidFieldData hitData, AsteroidFieldData.RuntimeCache runtime,
        Vector3 hitOrigin, int index, Vector3 smashDir, float vfxSpeed)
    {
        // Shared by collision and every renderer of this data assignment.
        runtime.Destroyed[index] = true;

        float rbSpeed = playerRb ? playerRb.linearVelocity.magnitude : 0f;

        float speed;
        if (rbSpeed < 1f && SpeedHUD.Instance != null)
        {
            speed = SpeedHUD.Instance.GetCurrentSpeed();
        }
        else
        {
            speed = rbSpeed;
        }

        if (smashVfxPool != null)
        {
            Color asteroidColor = Color.white;

            int typeId = (hitData.typeIds != null && index < hitData.typeIds.Length)
                ? hitData.typeIds[index]
                : 0;

            if (instancedRenderer != null &&
                instancedRenderer.typeRenders != null &&
                typeId >= 0 && typeId < instancedRenderer.typeRenders.Length &&
                instancedRenderer.typeRenders[typeId] != null &&
                instancedRenderer.typeRenders[typeId].material != null)
            {
                var mat = instancedRenderer.typeRenders[typeId].material;

                // Your shader graph property name appears to be "_BaseColor"
                if (mat.HasProperty("_BaseColor"))
                    asteroidColor = mat.GetColor("_BaseColor");
            }

            asteroidColor = BrightenMultiply(asteroidColor, 2.5f);

            // Example tuning knobs
            float t = Mathf.InverseLerp(smashSpeedThreshold, smashSpeedThreshold * 2f, speed);
            float dirSpeed = vfxSpeed;                                  // from your velocity-based scalar
            float radialSpeed = Mathf.Lerp(6f, 50f, t);                // explosion strength
            float randomSpeed = Mathf.Lerp(1f, 20f, t);                  // chaos
            int count = Mathf.RoundToInt(Mathf.Lerp(20f, 60f, t));      // particles

            Vector3 asteroidPos = hitOrigin + hitData.positions[index];
            Vector3 hitPos = playerBox.bounds.ClosestPoint(asteroidPos);

            bool useMutedVfx = simpleMove != null && !simpleMove.enabled;

            if (useMutedVfx)
            {
                smashVfxPool.SpawnImpactMuted(hitPos, smashDir, dirSpeed, radialSpeed, randomSpeed, count, asteroidColor);
            }
            else
            {
                smashVfxPool.SpawnImpact(hitPos, smashDir, dirSpeed, radialSpeed, randomSpeed, count, asteroidColor);
            }
        }

        // No separate renderer mask to synchronize: runtime.Destroyed is authoritative.
    }
    public static Color BrightenMultiply(Color c, float mult)
    {
        c.r *= mult;
        c.g *= mult;
        c.b *= mult;
        return c;
    }
    private bool IsShieldActive()
    {
        if (shieldMaterial == null)
            return false;

        if (!shieldMaterial.HasProperty("_ShieldColor"))
            return false;

        Color shieldColor = shieldMaterial.GetColor("_ShieldColor");

        const float threshold = 0.001f;

        return
            shieldColor.r > threshold ||
            shieldColor.g > threshold ||
            shieldColor.b > threshold;
    }
}
