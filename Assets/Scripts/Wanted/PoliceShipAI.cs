using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prototype 3D pursuit. Place on the prefab ROOT with one Rigidbody.
/// Uses kinematic movement with elastic holding, individual formation slots,
/// and soft separation. Collisions and gravity remain disabled.
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
    [Tooltip("Extra distance beyond the holding zone before returning to pursuit.")]
    [SerializeField, Min(0.1f)] private float resumeDistanceBuffer = 1f;

    [Header("Elastic Holding")]
    [Tooltip("Enter Holding within Follow Distance (including personal variation) plus this range. Holding keeps steering; it does not stop the ship.")]
    [SerializeField, Min(0f)] private float holdingRange = 6f;
    [Tooltip("Position-error acceleration. Higher pulls the cop back into position more firmly.")]
    [SerializeField, Min(0.1f)] private float springStrength = 3f;
    [Tooltip("Damps velocity relative to the smoothed player velocity. Higher reduces overshoot.")]
    [SerializeField, Min(0.1f)] private float springDamping = 2.5f;
    [Tooltip("Seconds used to smooth observed player velocity. Higher makes cops react more slowly to maneuvers.")]
    [SerializeField, Min(0.01f)] private float velocityResponseTime = 0.65f;

    [Header("Individual Follow Positions")]
    [Tooltip("Sideways spread as a fraction of Follow Distance. Slots sit on a cone behind the remembered travel direction.")]
    [SerializeField, Range(0.1f, 0.95f)] private float formationSpread = 0.65f;
    [Tooltip("Fractional variation in each cop's follow distance and steering response. Rolled once when initialized.")]
    [SerializeField, Range(0f, 0.3f)] private float personalityVariation = 0.15f;
    [Tooltip("Maximum rate at which the formation follows changes in player travel direction. Independent of camera rotation.")]
    [SerializeField, Min(0f)] private float formationTurnSpeed = 45f;
    [Tooltip("Small, smooth movement of the preferred position, in world units. Zero disables drifting.")]
    [SerializeField, Min(0f)] private float driftAmplitude = 0.75f;
    [Tooltip("Drift phase speed in radians per second.")]
    [SerializeField, Min(0f)] private float driftSpeed = 0.6f;

    [Header("Police Separation")]
    [Tooltip("Soft avoidance radius between cop centers. Set larger than the ships' combined visible half-widths.")]
    [SerializeField, Min(0.1f)] private float separationRadius = 6f;
    [SerializeField, Min(0f)] private float separationStrength = 25f;
    [Tooltip("Extra separation acceleration when cops are moving toward one another.")]
    [SerializeField, Min(0f)] private float separationDamping = 3f;

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
    // Only a local range check can establish direct contact; radio reports cannot.
    public bool HasDirectContact => initialized && isActiveAndEnabled &&
        target != null && target.gameObject.activeInHierarchy &&
        lastContactRefreshTime == Time.fixedTime && observedLastStep;
    public Vector3 LastObservedPosition => lastKnownPosition;
    public Vector3 LastObservedVelocity => lastKnownVelocity;
    public float DistanceToPlayer => distanceToPlayer;
    public float LoseContactDistance => loseContactDistance;
    public Vector3 Velocity => velocity;
    // Visual state only; this script does not slow or capture the player.
    public bool IsBeamActive => beamActive;
    public event Action<PoliceShipAI, ChaseState> StateChanged;

    // Active cops register here for separation without scene searches or physics queries.
    private static readonly List<PoliceShipAI> enabledShips = new List<PoliceShipAI>();
    private int formationSlot = -1;
    private Quaternion formationFrame = Quaternion.identity;
    private Vector3 smoothedTargetVelocity;
    private float personalFollowDistance;
    private float responseMultiplier = 1f;
    private float slotAngle;
    private float driftPhase;
    private float driftClock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        enabledShips.Clear();
    }

    private void OnEnable()
    {
        if (!enabledShips.Contains(this))
            enabledShips.Add(this);
    }

    private Rigidbody body;
    private Rigidbody target;
    private Vector3 velocity;
    private Vector3 lastKnownPosition;
    private Vector3 lastKnownVelocity;
    private Vector3 lastKnownDirection;
    private bool observedLastStep;
    private float timeWithoutContact;
    private bool initialized;
    private float lastContactRefreshTime = float.NegativeInfinity;
    private float timeWithoutDirectContact;
    private bool locallySearching;
    private bool receivedSharedSighting;


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
        lastContactRefreshTime = float.NegativeInfinity;
        timeWithoutDirectContact = 0f;
        locallySearching = false;
        receivedSharedSighting = false;
        SetBeamActive(false, true);
        if (initialized)
        {
            lastKnownPosition = target.position;
            lastKnownDirection = lastKnownPosition - body.position;
            if (lastKnownDirection.sqrMagnitude < 0.0001f)
                lastKnownDirection = transform.forward;
            lastKnownDirection.Normalize();
            ObserveTarget(lastKnownPosition, Time.fixedDeltaTime);
            ConfigureFollowPosition();
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
        // Normally already sampled by the manager this physics tick. Standalone
        // cops still perform their own detection through this idempotent method.
        RefreshDirectContact();
        if (HasDirectContact || receivedSharedSighting)
        {
            if (IsSearching)
                SetState(ChaseState.Pursuing);
        }
        else if (timeWithoutContact >= Mathf.Max(0f, loseContactGraceTime))
        {
            SetState(ChaseState.Searching);
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

        float responseTime = Mathf.Max(0.01f, velocityResponseTime / responseMultiplier);
        smoothedTargetVelocity = Vector3.Lerp(smoothedTargetVelocity, lastKnownVelocity,
            1f - Mathf.Exp(-dt / responseTime));
        UpdateFormationFrame(dt);
        driftClock += dt;
        Vector3 desiredPosition = pursuitPoint + GetFollowOffset();

        float holdingDistance = personalFollowDistance + Mathf.Max(0f, holdingRange);
        if (currentState == ChaseState.Holding)
        {
            if (distanceToPlayer > holdingDistance + Mathf.Max(0.1f, resumeDistanceBuffer))
                SetState(ChaseState.Pursuing);
        }
        else if (distanceToPlayer <= holdingDistance)
        {
            SetState(ChaseState.Holding);
        }

        FollowMovingTarget(desiredPosition, dt);
        // Preserve facing/beam presentation. These are strafing spacecraft, so
        // their noses can still face the player while inertia carries them sideways.
        FacePoint(pursuitPoint, false);
    }

    /// <summary>
    /// Samples direct detection once per physics tick. Called for ALL cops before
    /// the manager shares reports, so recipient order cannot create radio loops.
    /// Local detection hysteresis is independent of the squad's pursuit state.
    /// </summary>
    public void RefreshDirectContact()
    {
        if (lastContactRefreshTime == Time.fixedTime)
            return;
        lastContactRefreshTime = Time.fixedTime;
        receivedSharedSighting = false;
        if (!initialized || !isActiveAndEnabled || target == null ||
            !target.gameObject.activeInHierarchy)
        {
            observedLastStep = false;
            return;
        }

        float dt = Time.fixedDeltaTime;
        timeWithoutContact += dt;
        timeWithoutDirectContact += dt;
        Vector3 position = target.position;
        distanceToPlayer = Vector3.Distance(body.position, position);
        float detectionRange = locallySearching ? reacquireDistance : loseContactDistance;
        if (distanceToPlayer <= detectionRange)
        {
            ObserveTarget(position, dt);
            timeWithoutDirectContact = 0f;
            locallySearching = false;
        }
        else
        {
            observedLastStep = false;
            if (timeWithoutDirectContact >= Mathf.Max(0f, loseContactGraceTime))
                locallySearching = true;
        }
    }

    /// <summary>
    /// Receives a current report from a real spotter. Updates pursuit memory,
    /// but never enables direct detection or a tractor beam by itself.
    /// </summary>
    public void ReceiveSharedSighting(Vector3 position, Vector3 observedVelocity)
    {
        if (!initialized || !isActiveAndEnabled || target == null ||
            !target.gameObject.activeInHierarchy)
            return;
        RefreshDirectContact();
        if (HasDirectContact)
            return; // Preserve this cop's own consecutive observation samples.

        receivedSharedSighting = true;
        lastKnownPosition = position;
        lastKnownVelocity = observedVelocity;
        if (observedVelocity.sqrMagnitude > 0.01f)
            lastKnownDirection = observedVelocity.normalized;
        else
        {
            Vector3 towardSighting = position - body.position;
            if (towardSighting.sqrMagnitude > 0.0001f)
                lastKnownDirection = towardSighting.normalized;
        }
        timeWithoutContact = 0f;
        // Do not set observedLastStep: reports must not turn into real sightings.
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

        // Retain the original forward search course, with neighbor avoidance.
        Vector3 desiredVelocity = direction * searchSpeed;
        float rate = searchSpeed < velocity.magnitude ? braking : acceleration;
        Vector3 steering = Vector3.ClampMagnitude(
            (desiredVelocity - velocity) / Mathf.Max(dt, 0.0001f), Mathf.Max(0.1f, rate));
        IntegrateMovement(steering, searchSpeed, dt);
        FacePoint(searchPoint, false);
    }

    private void ConfigureFollowPosition()
    {
        // First unused slot for this player. Existing slots never reshuffle when
        // a cop spawns or disappears. Five slots occupy distinct sides of the cone.
        formationSlot = -1;
        int candidate = 0;
        while (true)
        {
            bool occupied = false;
            foreach (PoliceShipAI other in enabledShips)
            {
                if (other != null && other != this && other.initialized &&
                    other.target == target && other.formationSlot == candidate)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
                break;
            candidate++;
        }
        formationSlot = candidate;
        slotAngle = (formationSlot * 36f + UnityEngine.Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
        float variation = Mathf.Clamp(personalityVariation, 0f, 0.3f);
        personalFollowDistance = Mathf.Max(0.1f, followDistance *
            UnityEngine.Random.Range(1f - variation, 1f + variation));
        responseMultiplier = UnityEngine.Random.Range(1f - variation, 1f + variation);
        driftPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        driftClock = 0f;
        smoothedTargetVelocity = lastKnownVelocity;

        // New reinforcements share the current formation orientation rather than
        // building their own frame from their random spawn direction.
        Vector3 forward = lastKnownVelocity.sqrMagnitude > 0.25f
            ? lastKnownVelocity.normalized : target.transform.forward;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f
            ? Vector3.forward : Vector3.up;
        formationFrame = Quaternion.LookRotation(forward, up);
        foreach (PoliceShipAI other in enabledShips)
        {
            if (other != null && other != this && other.initialized && other.target == target)
            {
                formationFrame = other.formationFrame;
                break;
            }
        }
    }

    private void UpdateFormationFrame(float dt)
    {
        // Keep the previous frame at rest. Transport its up axis through turns
        // to avoid rebuilding a world-up basis that flips during vertical flight.
        if (smoothedTargetVelocity.sqrMagnitude <= 0.25f)
            return;
        Vector3 forward = formationFrame * Vector3.forward;
        Quaternion desiredFrame = Quaternion.FromToRotation(
            forward, smoothedTargetVelocity.normalized) * formationFrame;
        formationFrame = Quaternion.RotateTowards(formationFrame, desiredFrame,
            Mathf.Max(0f, formationTurnSpeed) * dt);
    }

    private Vector3 GetFollowOffset()
    {
        float spread = Mathf.Clamp(formationSpread, 0.1f, 0.95f);
        float lateral = personalFollowDistance * spread;
        float trailing = personalFollowDistance * Mathf.Sqrt(1f - spread * spread);
        float phase = driftClock * Mathf.Max(0f, driftSpeed) + driftPhase;
        float drift = Mathf.Max(0f, driftAmplitude);
        Vector3 localOffset = new Vector3(
            Mathf.Cos(slotAngle) * lateral + Mathf.Sin(phase) * drift,
            Mathf.Sin(slotAngle) * lateral + Mathf.Sin(phase * 0.73f + driftPhase) * drift,
            -trailing + Mathf.Sin(phase * 0.51f) * drift * 0.35f);
        return formationFrame * localOffset;
    }

    private void FollowMovingTarget(Vector3 desiredPosition, float dt)
    {
        Vector3 error = desiredPosition - body.position;
        if (error.sqrMagnitude < arrivalTolerance * arrivalTolerance)
            error = Vector3.zero;

        Vector3 steering;
        if (currentState == ChaseState.Holding)
        {
            // Acceleration from a damped spring, not an assignment of player
            // velocity. Velocity remains continuous through turns and state changes.
            steering = error * Mathf.Max(0.1f, springStrength) * responseMultiplier
                + (smoothedTargetVelocity - velocity) * Mathf.Max(0.1f, springDamping);
        }
        else
        {
            // Far away, retain deliberate pursuit with arrival braking at the slot.
            float distance = error.magnitude;
            float closingSpeed = Mathf.Min(distance * Mathf.Max(0.1f, arrivalResponse),
                Mathf.Sqrt(2f * Mathf.Max(0.1f, braking) * distance));
            Vector3 desiredVelocity = Vector3.ClampMagnitude(
                smoothedTargetVelocity + error.normalized * closingSpeed, Mathf.Max(0.1f, maxSpeed));
            steering = (desiredVelocity - velocity) * Mathf.Max(0.1f, arrivalResponse) * responseMultiplier;
        }
        IntegrateMovement(steering, Mathf.Max(0.1f, maxSpeed), dt);
    }

    private Vector3 CalculateSeparation()
    {
        Vector3 separation = Vector3.zero;
        float radius = Mathf.Max(0.1f, separationRadius);
        foreach (PoliceShipAI other in enabledShips)
        {
            if (other == null || other == this || !other.isActiveAndEnabled ||
                !other.initialized || other.body == null || other.target != target)
                continue;
            Vector3 away = body.position - other.body.position;
            float distanceSquared = away.sqrMagnitude;
            if (distanceSquared >= radius * radius)
                continue;

            float distance = Mathf.Sqrt(distanceSquared);
            Vector3 direction;
            if (distance > 0.0001f)
                direction = away / distance;
            else
            {
                // Coincident centers still get opposite, deterministic steering.
                direction = GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left;
            }
            float weight = 1f - distance / radius;
            float closingSpeed = Mathf.Max(0f, -Vector3.Dot(velocity - other.velocity, direction));
            separation += direction * weight *
                (Mathf.Max(0f, separationStrength) + closingSpeed * Mathf.Max(0f, separationDamping));
        }
        return separation;
    }

    private void IntegrateMovement(Vector3 steering, float speedLimit, float dt)
    {
        float limit = Mathf.Max(0.1f, Vector3.Dot(steering, velocity) < 0f ? braking : acceleration);
        Vector3 avoidance = Vector3.ClampMagnitude(CalculateSeparation(), limit);
        // Give avoidance first use of the acceleration budget. Goal steering uses
        // what remains, instead of canceling avoidance near a crowded target slot.
        Vector3 appliedAcceleration = avoidance + Vector3.ClampMagnitude(steering,
            Mathf.Max(0f, limit - avoidance.magnitude));
        velocity = Vector3.ClampMagnitude(velocity + appliedAcceleration * dt,
            Mathf.Max(0.1f, speedLimit));
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
        enabledShips.Remove(this);
        lastContactRefreshTime = float.NegativeInfinity;
        receivedSharedSighting = false;
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
