using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to a structure's root. Child collision relays are created at runtime;
/// existing colliders, rigidbodies, rotation and bounce settings are unchanged.
/// </summary>
[DisallowMultipleComponent]
public class PackageImpactHazard : MonoBehaviour
{
    [Header("Colliders")]
    [Tooltip("Optional explicit colliders. Leave empty to use non-trigger MeshColliders on this object and its children. Assigned colliders must belong to this hazard's hierarchy.")]
    [SerializeField] private Collider[] hazardColliders;

    [Header("Impact Strength")]
    [Tooltip("Minimum speed into the surface (units/second) needed to damage cargo or consume a shield charge.")]
    [SerializeField, Min(0.01f)] private float minimumImpactSpeed = 14f;

    [Tooltip("Speed into the surface that applies the durability manager's heavy damage.")]
    [SerializeField, Min(0.01f)] private float heavyImpactSpeed = 100f;

    [Tooltip("Seconds between accepted hits, shared by every collider on this structure. Shield-blocked hits also start this cooldown.")]
    [SerializeField, Min(0f)] private float impactCooldown = 0.3f;

    [Header("Shields")]
    [Tooltip("Allow one shield charge to block each accepted impact, as with asteroids.")]
    [SerializeField] private bool respectShields = true;

    private readonly HashSet<Collider> registeredColliders = new HashSet<Collider>();
    private float nextImpactTime = float.NegativeInfinity;
    private float lastImpactFixedTime = float.NegativeInfinity;

    private void Awake()
    {
        Collider[] colliders = hazardColliders != null && hazardColliders.Length > 0
            ? hazardColliders
            : GetComponentsInChildren<MeshCollider>(true);

        foreach (Collider hazardCollider in colliders)
        {
            if (hazardCollider == null || hazardCollider.isTrigger ||
                !hazardCollider.transform.IsChildOf(transform))
                continue;

            // A nested hazard owns its own colliders and cooldown.
            if (hazardCollider.GetComponentInParent<PackageImpactHazard>() != this)
                continue;

            registeredColliders.Add(hazardCollider);

            PackageImpactHazardCollisionRelay relay =
                hazardCollider.GetComponent<PackageImpactHazardCollisionRelay>();
            if (relay == null)
                relay = hazardCollider.gameObject.AddComponent<PackageImpactHazardCollisionRelay>();

            relay.Owner = this;
        }
    }

    internal void HandleCollision(Collision collision)
    {
        // Unity can deliver collision messages to disabled behaviours. Explicitly
        // respect the root's enabled state, including when its relays remain active.
        if (!isActiveAndEnabled || Time.time < nextImpactTime ||
            Time.fixedTime == lastImpactFixedTime)
            return;

        Rigidbody hitBody = collision.rigidbody;
        if (hitBody == null)
            return;

        SimpleMove player = SimpleMove.Instance;
        if (player == null || player.rb != hitBody)
            player = hitBody.GetComponentInParent<SimpleMove>();

        if (player == null || !player.isActiveAndEnabled || player.rb != hitBody)
            return;

        PackageDurabilityManager durability = PackageDurabilityManager.Instance;
        if (durability == null || !durability.HasActivePackage())
            return;

        float impactSpeed = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (!registeredColliders.Contains(contact.thisCollider))
                continue;

            // Use the collision's relative velocity, not the player's velocity
            // after the bounce. Tangential motion alone should not damage cargo.
            float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            impactSpeed = Mathf.Max(impactSpeed, normalSpeed);
        }

        float minimumSpeed = Mathf.Max(0.01f, minimumImpactSpeed);
        if (impactSpeed < minimumSpeed)
            return;

        float heavySpeed = Mathf.Max(minimumSpeed + 0.01f, heavyImpactSpeed);
        float strength = Mathf.InverseLerp(minimumSpeed, heavySpeed, impactSpeed);
        float severity = Mathf.Lerp(durability.minSeverityForDamage,
            Mathf.Max(durability.minSeverityForDamage, durability.heavySeverity), strength);

        // Claim the hit before consuming a shield, so simultaneous ring contacts
        // cannot consume several charges. Even a zero cooldown permits only one
        // accepted hit per physics step.
        lastImpactFixedTime = Time.fixedTime;
        nextImpactTime = Time.time + Mathf.Max(0f, impactCooldown);

        if (respectShields && StatManager.Instance != null &&
            StatManager.Instance.TryConsumeShieldCharge())
            return;

        durability.ApplyImpactDamage(severity);
    }

    private void OnValidate()
    {
        minimumImpactSpeed = Mathf.Max(0.01f, minimumImpactSpeed);
        heavyImpactSpeed = Mathf.Max(minimumImpactSpeed + 0.01f, heavyImpactSpeed);
        impactCooldown = Mathf.Max(0f, impactCooldown);
    }
}

// Runtime-only helper kept in this file so the root is the only component to set up.
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class PackageImpactHazardCollisionRelay : MonoBehaviour
{
    internal PackageImpactHazard Owner { get; set; }

    private void OnCollisionEnter(Collision collision)
    {
        if (Owner != null)
            Owner.HandleCollision(collision);
    }
}
