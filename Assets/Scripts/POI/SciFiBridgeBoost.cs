using UnityEngine;

/// <summary>
/// Put one instance on each end's BoxCollider and assign them as Paired Exit.
/// Uses a dynamic 3D Rigidbody. Does not rotate or recenter the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[DefaultExecutionOrder(1000)]
public class SciFiBridgeBoost : MonoBehaviour
{
    [Header("Tunnel Ends")]
    [SerializeField] private SciFiBridgeBoost pairedExit;
    [SerializeField] private string playerTag = "Player";

    [Header("Launch Direction")]
    [Tooltip("Optional transform whose local X defines the tunnel axis. Defaults to this object.")]
    [SerializeField] private Transform directionReference;
    [Tooltip("Launch along local -X instead of +X. Aim each end INTO the tunnel.")]
    [SerializeField] private bool reverseXDirection;

    [Header("Boost")]
    [SerializeField, Min(0.02f)] private float boostDuration = 1.5f;
    [Tooltip("Speed added to the incoming speed over the entire boost (units/second).")]
    [SerializeField, Min(0f)] private float addedSpeed = 50f;
    [Tooltip("Minimum time after boosting before the pair can reset. Also waits for clearance.")]
    [SerializeField, Min(0f)] private float rearmDelay = 0.5f;
    [SerializeField, Min(0f)] private float clearancePadding = 0.5f;

    private BoxCollider trigger;
    private SciFiBridgeBoost owner;
    private SciFiBridgeBoost lockedExit;
    private BoxCollider disabledExitCollider;
    private bool exitWasEnabled;
    private Rigidbody playerBody;
    private Collider[] playerColliders;
    private Vector3 launchDirection;
    private float startingSpeed;
    private float elapsed;
    private float activeDuration;
    private float activeAddedSpeed;
    private bool boosting;
    private bool savedGravity;
    private float savedDamping;

    public bool IsBoosting => owner != null && owner.boosting;
    public Rigidbody BoostedBody => IsBoosting ? owner.playerBody : null;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    private Vector3 LaunchDirection()
    {
        Transform axis = directionReference != null ? directionReference : transform;
        return axis.right * (reverseXDirection ? -1f : 1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || owner != null)
            return;

        Rigidbody body = other.attachedRigidbody;
        if (body == null || body.isKinematic)
            return;

        // Supports a tagged collider, Rigidbody object, or parent player object.
        bool isPlayer = false;
        for (Transform current = other.transform; current != null; current = current.parent)
        {
            if (current.CompareTag(playerTag))
            {
                isPlayer = true;
                break;
            }
        }
        if (!isPlayer)
            return;

        if (pairedExit != null && (pairedExit == this ||
            !pairedExit.isActiveAndEnabled || pairedExit.owner != null))
            return;

        owner = this;
        lockedExit = pairedExit;
        if (lockedExit != null)
        {
            lockedExit.owner = this;
            disabledExitCollider = lockedExit.GetComponent<BoxCollider>();
            exitWasEnabled = disabledExitCollider.enabled;
            disabledExitCollider.enabled = false;
        }

        playerBody = body;
        playerColliders = body.GetComponentsInChildren<Collider>();
        launchDirection = LaunchDirection().normalized;
        startingSpeed = GetVelocity(body).magnitude;
        activeDuration = Mathf.Max(0.02f, boostDuration);
        activeAddedSpeed = Mathf.Max(0f, addedSpeed);
        elapsed = 0f;
        savedGravity = body.useGravity;
        savedDamping = GetDamping(body);
        body.useGravity = false;
        SetDamping(body, 0f);
        boosting = true;

        // Redirect immediately so sideways momentum cannot carry us into a wall.
        SetVelocity(body, launchDirection * startingSpeed);
    }

    private void FixedUpdate()
    {
        if (owner != this)
            return;

        if (playerBody == null || !playerBody.gameObject.activeInHierarchy || playerBody.isKinematic)
        {
            ReleasePair();
            return;
        }

        elapsed += Time.fixedDeltaTime;
        if (boosting)
        {
            float progress = Mathf.Clamp01(elapsed / activeDuration);
            SetVelocity(playerBody, launchDirection * (startingSpeed + activeAddedSpeed * progress));
            if (progress >= 1f)
                RestorePhysics();
        }

        // Do not rely on OnTriggerExit from a disabled collider. Compute its bounds
        // from its current transform, so moving tunnels/origin shifts are supported.
        if (!boosting && elapsed >= activeDuration + rearmDelay &&
            !PlayerOverlaps(trigger) && !PlayerOverlaps(disabledExitCollider))
            ReleasePair();
    }

    private bool PlayerOverlaps(BoxCollider box)
    {
        if (box == null || playerColliders == null)
            return false;

        Vector3 half = box.size * 0.5f;
        Vector3 x = box.transform.TransformVector(new Vector3(half.x, 0f, 0f));
        Vector3 y = box.transform.TransformVector(new Vector3(0f, half.y, 0f));
        Vector3 z = box.transform.TransformVector(new Vector3(0f, 0f, half.z));
        Vector3 extents = new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        Bounds bounds = new Bounds(box.transform.TransformPoint(box.center), extents * 2f);
        bounds.Expand(Mathf.Max(0f, clearancePadding) * 2f);

        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider != null && playerCollider.enabled &&
                playerCollider.gameObject.activeInHierarchy &&
                playerCollider.attachedRigidbody == playerBody &&
                bounds.Intersects(playerCollider.bounds))
                return true;
        }
        return false;
    }

    private void RestorePhysics()
    {
        if (!boosting)
            return;
        if (playerBody != null)
        {
            playerBody.useGravity = savedGravity;
            SetDamping(playerBody, savedDamping);
        }
        boosting = false;
        // Keep the outgoing velocity. Normal movement/physics resumes from here.
    }

    private void ReleasePair()
    {
        RestorePhysics();
        if (disabledExitCollider != null)
            disabledExitCollider.enabled = exitWasEnabled;
        if (lockedExit != null && lockedExit.owner == this)
            lockedExit.owner = null;
        lockedExit = null;
        disabledExitCollider = null;
        playerBody = null;
        playerColliders = null;
        owner = null;
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.ReleasePair();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 direction = LaunchDirection();
        Vector3 tip = transform.position + direction * 4f;
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawSphere(tip, 0.15f);
    }

    // Unity 6 and earlier Unity versions use different property names.
    private static Vector3 GetVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody body, Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;
#else
        body.velocity = velocity;
#endif
    }

    private static float GetDamping(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearDamping;
#else
        return body.drag;
#endif
    }

    private static void SetDamping(Rigidbody body, float damping)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = damping;
#else
        body.drag = damping;
#endif
    }
}
