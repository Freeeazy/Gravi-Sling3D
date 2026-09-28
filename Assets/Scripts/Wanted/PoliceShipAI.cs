using System;
using UnityEngine;

/// <summary>
/// Prototype 3D pursuit. Place on the prefab ROOT with one Rigidbody.
/// Uses authoritative kinematic movement; collisions and gravity are disabled.
/// Initialize is called by PoliceChaseManager after spawning.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PoliceShipAI : MonoBehaviour
{
    public enum ChaseState { Pursuing, Holding, Searching }

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float maxSpeed = 24f;
    [SerializeField, Min(0.1f)] private float acceleration = 25f;
    [SerializeField, Min(0.1f)] private float braking = 40f;
    [SerializeField, Min(0.1f)] private float arrivalResponse = 3f;
    [SerializeField, Min(0f)] private float followDistance = 12f;
    [SerializeField, Min(0.01f)] private float arrivalTolerance = 0.15f;
    [Tooltip("Extra separation needed before a stopped cop starts following again.")]
    [SerializeField, Min(0.1f)] private float resumeDistanceBuffer = 1f;

    [Header("Tractor Beam Visuals")]
    [Tooltip("Assign only beam particle roots on this prefab. Their child particle systems are included. Keep the GameObjects active and enable Looping for a continuous beam.")]
    [SerializeField] private ParticleSystem[] tractorBeamParticles = new ParticleSystem[0];
    [Tooltip("Optional ship-side muzzle used to measure beam range. Defaults to the cop's Rigidbody position.")]
    [SerializeField] private Transform beamRangeOrigin;
    [SerializeField, Min(0f)] private float beamActivationDistance = 25f;
    [SerializeField, Min(0f)] private float beamReleaseDistance = 30f;
    [Tooltip("Clear existing particles immediately when the beam switches off. Otherwise let them finish naturally.")]
    [SerializeField] private bool clearBeamOnRelease = true;

    [Header("Tracking (Distance Only)")]
    [SerializeField, Min(1f)] private float loseContactDistance = 120f;
    [SerializeField, Min(1f)] private float reacquireDistance = 90f;
    [SerializeField, Min(0f)] private float loseContactGraceTime = 1f;

    [Header("Search Prediction")]
    [Tooltip("Fraction of Max Speed used while searching. 1 keeps full pursuit speed.")]
    [SerializeField, Range(0.1f, 1f)] private float searchSpeedMultiplier = 1f;
    [Tooltip("Steer this many seconds ahead along the remembered travel direction. Prevents stopping or circling at the old position.")]
    [SerializeField, Min(0.1f)] private float searchLookAheadTime = 1f;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 180f;
    [Tooltip("Mesh-axis correction, like SimpleMove.baseEulerOffset. Default assumes +Z forward.")]
    [SerializeField] private Vector3 baseEulerOffset = Vector3.zero;

    [Header("Runtime Debug")]
    [SerializeField] private ChaseState currentState = ChaseState.Pursuing;
    [SerializeField] private float distanceToPlayer;
    [SerializeField] private bool beamActive;
    [SerializeField] private float distanceFromBeamOrigin;

    public ChaseState CurrentState => currentState;
    public bool IsSearching => currentState == ChaseState.Searching;
    public float DistanceToPlayer => distanceToPlayer;
    public float LoseContactDistance => loseContactDistance;
    public Vector3 Velocity => velocity;
    // Visual state only; this script does not slow or capture the player.
    public bool IsBeamActive => beamActive;
    public event Action<PoliceShipAI, ChaseState> StateChanged;

    private Rigidbody body;
    private Rigidbody target;
    private Vector3 velocity;
    private Vector3 lastKnownPosition;
    private Vector3 lastKnownVelocity;
    private Vector3 lastKnownDirection;
    private bool observedLastStep;
    private float timeWithoutContact;
    private bool initialized;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.detectCollisions = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        PrepareBeamParticles();
    }

    public void Initialize(Rigidbody playerBody)
    {
        target = playerBody;
        initialized = target != null;
        velocity = Vector3.zero;
        timeWithoutContact = 0f;
        observedLastStep = false;
        lastKnownVelocity = Vector3.zero;
        currentState = ChaseState.Pursuing;
        SetBeamActive(false, true);
        if (initialized)
        {
            lastKnownPosition = target.position;
            lastKnownDirection = lastKnownPosition - body.position;
            if (lastKnownDirection.sqrMagnitude < 0.0001f)
                lastKnownDirection = transform.forward;
            lastKnownDirection.Normalize();
            ObserveTarget(lastKnownPosition, Time.fixedDeltaTime);
            distanceToPlayer = Vector3.Distance(body.position, target.position);
            FacePoint(lastKnownPosition, true);
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || target == null || !target.gameObject.activeInHierarchy)
        {
            velocity = Vector3.zero;
            observedLastStep = false;
            SetBeamActive(false);
            return;
        }

        float dt = Time.fixedDeltaTime;
        Vector3 playerPosition = target.position;
        distanceToPlayer = Vector3.Distance(body.position, playerPosition);

        // Age the last actual observation, including the grace period.
        timeWithoutContact += dt;

        if (IsSearching)
        {
            // The live position is used ONLY for detection until reacquired.
            // Search steering uses frozen observations, never unseen movement.
            if (distanceToPlayer <= reacquireDistance)
            {
                ObserveTarget(playerPosition, dt);
                SetState(ChaseState.Pursuing);
            }
            else
                observedLastStep = false;
        }
        else if (distanceToPlayer > loseContactDistance)
        {
            observedLastStep = false;
            if (timeWithoutContact >= loseContactGraceTime)
                SetState(ChaseState.Searching);
        }
        else
        {
            ObserveTarget(playerPosition, dt);
        }

        if (IsSearching)
        {
            SetBeamActive(false);
            SearchAlongLastKnownCourse(dt);
            return;
        }

        // During the grace period, continue from the remembered velocity too.
        Vector3 pursuitPoint = lastKnownPosition + lastKnownVelocity * timeWithoutContact;
        UpdateBeamRange(playerPosition);

        if (currentState == ChaseState.Holding)
        {
            if (lastKnownVelocity.sqrMagnitude > 0.01f ||
                Mathf.Abs(distanceToPlayer - followDistance) > Mathf.Max(resumeDistanceBuffer, arrivalTolerance))
                SetState(ChaseState.Pursuing);
            else
            {
                velocity = Vector3.zero;
                FacePoint(pursuitPoint, false);
                return;
            }
        }

        FollowMovingTarget(pursuitPoint, lastKnownVelocity, dt);
        if (Mathf.Abs(distanceToPlayer - followDistance) <= arrivalTolerance &&
            velocity.sqrMagnitude < 0.01f && lastKnownVelocity.sqrMagnitude < 0.01f)
            SetState(ChaseState.Holding);
        FacePoint(pursuitPoint, false);
    }

    private void ObserveTarget(Vector3 position, float dt)
    {
        // Position sampling also sees orbit/script-driven movement that might
        // not be represented by the player's Rigidbody velocity.
        // Never measure across a gap with no contact: that would include unseen turns.
        if (observedLastStep && dt > 0f)
            lastKnownVelocity = (position - lastKnownPosition) / dt;
        else
        {
#if UNITY_6000_0_OR_NEWER
            lastKnownVelocity = target.linearVelocity;
#else
            lastKnownVelocity = target.velocity;
#endif
        }

        if (lastKnownVelocity.sqrMagnitude > 0.01f)
            lastKnownDirection = lastKnownVelocity.normalized;

        lastKnownPosition = position;
        timeWithoutContact = 0f;
        observedLastStep = true;
    }

    private void SearchAlongLastKnownCourse(float dt)
    {
        float searchSpeed = maxSpeed * Mathf.Clamp(searchSpeedMultiplier, 0.1f, 1f);
        Vector3 predictedPosition = lastKnownPosition + lastKnownVelocity * timeWithoutContact;

        // Project both the predicted target and the cop onto the remembered
        // course. Always aim farther along it than either projection: otherwise
        // a faster cop could pass its prediction and keep turning back around.
        float predictedProgress = Vector3.Dot(predictedPosition - lastKnownPosition, lastKnownDirection);
        float copProgress = Vector3.Dot(body.position - lastKnownPosition, lastKnownDirection);
        float leadDistance = Mathf.Max(1f, searchSpeed * Mathf.Max(0.1f, searchLookAheadTime));
        float aimProgress = Mathf.Max(predictedProgress, copProgress) + leadDistance;
        Vector3 searchPoint = lastKnownPosition + lastKnownDirection * aimProgress;
        Vector3 direction = (searchPoint - body.position).normalized;

        // Cruise through the search course; do not use arrival braking here.
        // Acceleration still limits changes of velocity, including turns.
        Vector3 desiredVelocity = direction * searchSpeed;
        float rate = searchSpeed < velocity.magnitude ? braking : acceleration;
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, rate * dt);
        body.MovePosition(body.position + velocity * dt);
        FacePoint(searchPoint, false);
    }

    private void FollowMovingTarget(Vector3 point, Vector3 targetVelocity, float dt)
    {
        Vector3 offset = point - body.position;
        float distance = offset.magnitude;
        Vector3 direction = distance > 0.0001f ? offset / distance : lastKnownDirection;
        float distanceError = distance - Mathf.Max(0f, followDistance);
        float correctionSpeed = 0f;

        if (Mathf.Abs(distanceError) > arrivalTolerance)
        {
            float gap = Mathf.Abs(distanceError);
            correctionSpeed = Mathf.Sign(distanceError) * Mathf.Min(
                Mathf.Sqrt(2f * Mathf.Max(0.1f, braking) * gap),
                gap * Mathf.Max(0.1f, arrivalResponse));
        }

        // Match the player's travel velocity, then correct the relative gap.
        // Arrival braking applies to closing speed, not all forward movement.
        // Negative correction backs away if the player gets too close.
        Vector3 desiredVelocity = Vector3.ClampMagnitude(
            targetVelocity + direction * correctionSpeed, Mathf.Max(0.1f, maxSpeed));
        float rate = desiredVelocity.magnitude < velocity.magnitude ? braking : acceleration;
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, rate * dt);
        // Do not clamp world travel to the gap: even at the correct distance,
        // both ships still need to move together at the player's travel speed.
        body.MovePosition(body.position + velocity * dt);
    }

    private void PrepareBeamParticles()
    {
        if (tractorBeamParticles == null)
            return;
        foreach (ParticleSystem root in tractorBeamParticles)
        {
            if (root == null)
                continue;
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.playOnAwake = false;
                // These are reusable VFX; stopping must not destroy/disable the ship or emitters.
                main.stopAction = ParticleSystemStopAction.None;
            }
        }
        SetBeamActive(false, true);
    }

    private void UpdateBeamRange(Vector3 playerPosition)
    {
        Vector3 origin = beamRangeOrigin != null ? beamRangeOrigin.position : body.position;
        distanceFromBeamOrigin = Vector3.Distance(origin, playerPosition);
        float activation = Mathf.Max(0f, beamActivationDistance);
        float release = Mathf.Max(activation, beamReleaseDistance);
        float threshold = beamActive ? release : activation;
        // A cop with no current observation cannot keep a beam lock during grace/search.
        bool shouldPlay = observedLastStep && !IsSearching && distanceFromBeamOrigin <= threshold;
        SetBeamActive(shouldPlay);
    }

    private void SetBeamActive(bool active, bool forceClear = false)
    {
        // Call Play/Stop only on transitions so bursts are not restarted each tick.
        if (!forceClear && beamActive == active)
            return;
        beamActive = active;
        if (tractorBeamParticles == null)
            return;
        foreach (ParticleSystem system in tractorBeamParticles)
        {
            if (system == null)
                continue;
            if (active)
                system.Play(true);
            else
                system.Stop(true, forceClear || clearBeamOnRelease
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void FacePoint(Vector3 point, bool snap)
    {
        Vector3 direction = point - body.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        direction.Normalize();
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
            ? Vector3.forward : Vector3.up;
        Quaternion rotation = Quaternion.LookRotation(direction, up) * Quaternion.Euler(baseEulerOffset);
        if (snap)
            body.rotation = rotation;
        else
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, rotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void SetState(ChaseState state)
    {
        if (currentState == state)
            return;
        currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnDisable()
    {
        velocity = Vector3.zero;
        observedLastStep = false;
        SetBeamActive(false, true);
    }

    // Optional hook for a future floating-origin system. The caller must also
    // shift the cop's actual transform/Rigidbody by the same world-space delta.
    public void ShiftLastKnownPosition(Vector3 worldShift)
    {
        lastKnownPosition += worldShift;
    }

}
