using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class PreGravityPullZone : MonoBehaviour
{
    [Header("Filtering")]
    [Tooltip("Must match SlingshotPlanet3D.requiredTag (or leave empty to ignore).")]
    public string requiredTag = "Player";

    [Tooltip("Optional: also require a Rigidbody (recommended).")]
    public bool requireRigidbody = true;

    [Header("Pull Tuning")]
    [Tooltip("Acceleration toward the planet center (m/s^2) at the *outer* edge.")]
    public float accelAtEdge = 2f;

    [Tooltip("Acceleration toward the planet center (m/s^2) near the *inner* edge.")]
    public float accelAtInner = 18f;

    [Tooltip("How close to the center counts as 'inner'. 0 = center, 1 = at trigger radius.")]
    [Range(0.05f, 1f)]
    public float innerRadius01 = 0.35f;

    [Tooltip("Drag-like damping applied to velocity along the radial direction (helps prevent slingshot jitter).")]
    [Range(0f, 2f)]
    public float radialDamping = 0.2f;

    [Header("Launch Cooldown")]
    [Tooltip("Seconds to ignore pre-gravity after launching from orbit.")]
    public float postLaunchDisableTime = 1f;

    [Header("References")]
    [Tooltip("Planet orbit component (used to stop pulling when orbiting).")]
    public SlingshotPlanet3D planet;

    private SphereCollider _sc;
    private float _disabledUntilTime;

    private void Awake()
    {
        _sc = GetComponent<SphereCollider>();
        _sc.isTrigger = true;

        if (!planet)
            planet = GetComponentInParent<SlingshotPlanet3D>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!planet) return;
        if (Time.time < _disabledUntilTime) return;

        // Stop pulling once orbit capture has begun.
        // Your SlingshotPlanet3D sets isOrbiting=true at capture start
        if (planet.IsOrbiting) return; 

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        var rb = other.attachedRigidbody;
        if (requireRigidbody && !rb) return;
        if (!rb) return;

        // Pull toward planet center
        Vector3 center = planet.transform.position;
        Vector3 toCenter = (center - rb.worldCenterOfMass);
        float dist = toCenter.magnitude;
        if (dist < 0.001f) return;

        Vector3 dir = toCenter / dist;

        // Normalize distance within this trigger radius (world space)
        float triggerRadius = _sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float t01 = Mathf.Clamp01(dist / Mathf.Max(triggerRadius, 0.001f)); // 0=center, 1=edge

        // Stronger pull as you get closer (ease curve)
        float inner01 = Mathf.Clamp01(innerRadius01);
        float k = (t01 <= inner01)
            ? 1f
            : Mathf.InverseLerp(1f, inner01, t01); // 0 at edge -> 1 near inner band

        float accel = Mathf.Lerp(accelAtEdge, accelAtInner, k);

        // Apply acceleration (ForceMode.Acceleration ignores mass)
        rb.AddForce(dir * accel, ForceMode.Acceleration);

        // Optional: damp only the radial component to reduce bounce
        if (radialDamping > 0f)
        {
            float vRad = Vector3.Dot(rb.linearVelocity, dir);
            rb.linearVelocity -= dir * (vRad * radialDamping * Time.fixedDeltaTime);
        }
    }
    public void DisableForSeconds(float seconds)
    {
        _disabledUntilTime = Mathf.Max(_disabledUntilTime, Time.time + Mathf.Max(0f, seconds));
    }

    public void DisablePostLaunch()
    {
        DisableForSeconds(postLaunchDisableTime);
    }
}
