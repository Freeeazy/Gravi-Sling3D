using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleMove : MonoBehaviour
{
    public static SimpleMove Instance { get; private set; }

    [Header("References")]
    public Rigidbody rb; // Assign your CHILD rigidbody here in the inspector.

    [Tooltip("Optional. If null, uses Camera.main.")]
    public Transform cameraTransform;

    [Header("Rigidbody Movement (Inertia)")]
    public float maxSpeed = 8f;
    public float acceleration = 25f;
    public float damping = 2f;                 // lower = more drift
    public bool normalizeInput = true;

    [Header("Boost (Hold LeftShift) - Additive")]
    [Tooltip("Adds this many m/s to maxSpeed at full boostCharge. (Additive, not multiplicative)")]
    public float boostMaxSpeed = 8f;     // e.g. maxSpeed 8 + 8 = 16 at full charge
    [Tooltip("How fast boostCharge fills while held (per second).")]
    public float boostRampUp = 1.2f;
    [Tooltip("How fast boostCharge drains when released (per second).")]
    public float boostRampDown = 2.0f;
    [Tooltip("Extra acceleration (m/s^2) added while boosting at full charge.")]
    public float boostAccelAdd = 40f;
    [Tooltip("Optional: limit base thrust (non-boost) to maxSpeed by stopping thrust past it. Boost ignores this.")]
    public bool limitBaseThrustToMaxSpeed = true;

    [Header("Rotation")]
    public bool enableRotation = true;
    public float rotationSpeed = 12f;          // higher = snappier
    public float minSpeedToRotate = 0.15f;     // ignore tiny velocity jitters

    [Tooltip("Base orientation to make your mesh face the 'right' way before steering is applied.")]
    public Vector3 baseEulerOffset = Vector3.zero;

    [Header("Manual Roll (Q/E)")]
    public float manualRollSpeed = 120f; // degrees per second
    public float _manualRoll;           // accumulated roll angle

    [Header("Auto Stabilization (Roll)")]
    public bool autoStabilizeRoll = true;          // master toggle
    public bool isAutoStabilizingRoll = false;     // <-- public for other scripts to reference

    [Tooltip("Seconds after letting go of Q/E before auto-stabilization starts.")]
    public float autoStabilizeDelay = 0.75f;

    [Tooltip("How fast we correct back toward 0 roll (degrees per second).")]
    public float autoStabilizeDegPerSec = 180f;

    [Tooltip("When abs(roll) is below this, snap to 0 and stop updating.")]
    public float autoStabilizeEpsilon = 0.15f;

    [Header("Style / Flair")]
    [Tooltip("Roll/bank amount while steering (degrees). This does NOT change direction; it's just style.")]
    public float bankDegrees = 20f;

    [Tooltip("How strongly bank responds to turning/side input.")]
    public float bankResponse = 10f;

    [Tooltip("Optional little wobble roll while moving (degrees). Set to 0 to disable.")]
    public float thrustWobbleDegrees = 5f;

    [Tooltip("Wobble speed (Hz-ish).")]
    public float thrustWobbleSpeed = 6f;

    [Header("Slingshot Slip (Coast Assist)")]
    public bool enableSlip = true;

    [Tooltip("Seconds it takes to return to normal handling with no input.")]
    public float slipReturnTimeNoInput = 2.5f;

    [Tooltip("Seconds it takes to return to normal handling while player is giving input.")]
    public float slipReturnTimeWithInput = 0.6f;

    [Tooltip("Multiply damping by this at full slip (0 = no damping, 1 = normal damping).")]
    [Range(0f, 1f)] public float slipDampingMultiplier = 0.08f;

    [Tooltip("Also reduce steering accel slightly while slipping (optional). 1 = unchanged.")]
    [Range(0.2f, 1f)] public float slipAccelMultiplier = 1.0f;

    [Tooltip("Slip starts at this value when script is enabled.")]
    [Range(0f, 1f)] public float slipStart = 1f;

    [NonSerialized] public float slipFactor = 0f; // 1 = fully slipping, 0 = normal

    [Header("Cruise Control (Toggle X)")]
    public KeyCode cruiseKey = KeyCode.X;

    [Tooltip("If true, pressing any movement/boost/roll input will disable cruise.")]
    public bool cruiseAutoDisengageOnInput = true;

    [NonSerialized] public bool cruiseControl = false;

    public Image CruiseControlBkRnd;

    [Tooltip("Material when Cruise Control is ON.")]
    public Material cruiseOnMat;

    [Tooltip("Material when Cruise Control is OFF.")]
    public Material cruiseOffMat;

    [Header("Auto Stabilize UI")]
    public Image AutoStabilizeBkRnd;

    [Tooltip("Material when Auto Stabilize is ENABLED.")]
    public Material autoStabilizeOnMat;

    [Tooltip("Material when Auto Stabilize is DISABLED.")]
    public Material autoStabilizeOffMat;

    [Header("Debug")]
    public bool debugVelocity = false;

    [Tooltip("TMP text to show current Rigidbody velocity.")]
    public TMP_Text velocityText;

    [Tooltip("If true, shows magnitude only. If false, shows full vector.")]
    public bool showMagnitudeOnly = true;

    [Tooltip("Decimal precision for velocity display.")]
    [Range(0, 4)] public int velocityDecimals = 2;

    private float _bank;
    private float _wobbleT;
    private float boostCharge = 0f;

    private float _lastSpeed = float.NaN;
    private Vector3 _cruiseMoveDir = Vector3.zero;   // stored world direction
    private Vector3 _cruiseBankInput = Vector3.zero; // stored input for styling/bank (optional)
    private bool _cruiseTogglePressed;
    private bool _lastCruiseState = false;
    private bool _wasBoosting;

    private float _rollIdleTimer = 0f;

    private bool _isPaused = false;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!rb)
        {
            Debug.LogError("SimpleMove: Assign the child Rigidbody in the inspector.");
            enabled = false;
            return;
        }

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!cameraTransform && Camera.main)
            cameraTransform = Camera.main.transform;

        UpdateCruiseUI(force: true);
        UpdateAutoStabilizeUI(force: true);
    }
    private void OnEnable()
    {
        if (enableSlip)
            slipFactor = slipStart;
    }

    private void OnDisable()
    {
        cruiseControl = false;
        UpdateCruiseUI(force: true);
        isAutoStabilizingRoll = false;
        UpdateAutoStabilizeUI(force: true);
        _rollIdleTimer = 0f;
    }
    private void Update()
    {
        if (!UIBlock.IsUIOpen && !_isPaused && Input.GetKeyDown(cruiseKey))
            _cruiseTogglePressed = true;
    }

    private void FixedUpdate()
    {
        if (!cameraTransform && Camera.main)
            cameraTransform = Camera.main.transform;

        float h = 0f;
        float v = 0f;
        float ud = 0f;
        float roll = 0f;
        bool boostHeld = false;
        bool inputBlocked = _isPaused || UIBlock.IsUIOpen;

        if (!inputBlocked)
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.Space)) ud += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) ud -= 1f;

            if (Input.GetKey(KeyCode.Q)) roll += 1f;
            if (Input.GetKey(KeyCode.E)) roll -= 1f;

            boostHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetMouseButton(0);
        }

        Vector3 input = new Vector3(h, ud, v);

        if (normalizeInput && input.sqrMagnitude > 1f)
            input.Normalize();

        // --- SLIP DECAY ---
        if (enableSlip && slipFactor > 0f)
        {
            float returnTime = (input.sqrMagnitude > 0.0001f) ? slipReturnTimeWithInput : slipReturnTimeNoInput;
            float rate = (returnTime <= 0.0001f) ? 999f : (1f / returnTime);
            slipFactor = Mathf.MoveTowards(slipFactor, 0f, rate * Time.fixedDeltaTime);
        }

        var cam = SimpleFollowCamera.Instance;
        if (cam)
        {
            bool isBoosting = boostHeld && boostCharge > 0.01f;

            if (isBoosting != _wasBoosting)
            {
                _wasBoosting = isBoosting;

                if (isBoosting)
                {
                    cam.SetBoostFOV(
                        maxBoostSpeed: maxSpeed + boostMaxSpeed,
                        extraAdd: 10f,            // tweak
                        sharpnessUp: 12f,
                        sharpnessDown: 5f
                    );

                    // Also ensure free-flight base is correct for the boost mode math
                    cam.SetFreeFlightFOV(maxSpeed, maxAdd: 6f, sharpness: 6f);
                    cam.SetFovMode(SimpleFollowCamera.FovMode.Boost);
                }
                else
                {
                    cam.SetFreeFlightFOV(maxSpeed, maxAdd: 6f, sharpness: 6f);
                    cam.SetFovMode(SimpleFollowCamera.FovMode.FreeFlight);
                }
            }
        }

        // "movement input" (WASD/space/ctrl)
        bool hasMoveInput = input.sqrMagnitude > 0.0001f;

        // Allow boost-only to count as intent
        bool boostOnlyIntent = boostHeld && !hasMoveInput;
        bool hasInput = hasMoveInput || boostOnlyIntent;

        if (BoostManager.Instance)
        {
            BoostManager.Instance.SetBoostInput(boostHeld, hasMoveInput || boostOnlyIntent);
            boostCharge = BoostManager.Instance.Boost01; // reuse your existing float
        }
        else
        {
            // Fallback: old behavior if BoostManager missing
            if (hasInput)
            {
                float up = boostHeld ? boostRampUp : -boostRampDown;
                boostCharge = Mathf.Clamp01(boostCharge + up * Time.fixedDeltaTime);
            }
            else boostCharge = 0f;
        }

        float dt = Time.fixedDeltaTime;

        // roll input is -1, 0, or +1 based on Q/E
        bool hasRollInput = Mathf.Abs(roll) > 0.0001f;

        if (hasRollInput)
        {
            // Manual roll overrides everything
            isAutoStabilizingRoll = false;
            _rollIdleTimer = 0f;

            _manualRoll += roll * manualRollSpeed * dt;
            _manualRoll = Mathf.Repeat(_manualRoll, 360f);
        }
        else
        {
            // No roll input
            if (autoStabilizeRoll && Mathf.Abs(_manualRoll) > autoStabilizeEpsilon)
            {
                _rollIdleTimer += dt;

                if (_rollIdleTimer >= autoStabilizeDelay)
                {
                    isAutoStabilizingRoll = true;

                    // Drive roll back to zero
                    _manualRoll = Mathf.MoveTowards(_manualRoll, 0f, autoStabilizeDegPerSec * dt);
                    _manualRoll = Mathf.Repeat(_manualRoll, 360f);

                    // Done? snap and stop updating
                    if (Mathf.Abs(_manualRoll) <= autoStabilizeEpsilon)
                    {
                        _manualRoll = 0f;
                        isAutoStabilizingRoll = false;
                    }
                }
            }
            else
            {
                // Already basically stable (or disabled)
                _manualRoll = (Mathf.Abs(_manualRoll) <= autoStabilizeEpsilon) ? 0f : _manualRoll;
                isAutoStabilizingRoll = false;
                _rollIdleTimer = 0f;
            }
        }

        // --- BUILD CAMERA-RELATIVE MOVE DIR ---
        Vector3 moveDir = Vector3.zero;

        if (cameraTransform)
        {
            Vector3 camF = cameraTransform.forward;
            Vector3 camR = cameraTransform.right;
            Vector3 camU = cameraTransform.up;

            // Camera-relative intent:
            // x = strafe, y = up/down, z = forward/back
            Vector3 dir =
                camR * input.x +
                camU * input.y +
                camF * input.z;

            if (dir.sqrMagnitude > 0.0001f)
                moveDir = dir.normalized;
        }
        else
        {
            Vector3 dir = new Vector3(input.x, input.y, input.z);
            if (dir.sqrMagnitude > 0.0001f)
                moveDir = dir.normalized;
        }

        // --- CRUISE TOGGLE (captured in Update, consumed here) ---
        if (_cruiseTogglePressed)
        {
            _cruiseTogglePressed = false;

            cruiseControl = !cruiseControl;

            if (cruiseControl)
            {
                Vector3 dirToStore = (rb.rotation * Vector3.forward);

                if (dirToStore.sqrMagnitude > 0.0001f)
                {
                    _cruiseMoveDir = dirToStore.normalized;
                    _cruiseBankInput = input;
                }
                else cruiseControl = false;
            }
            UpdateCruiseUI(force: true);
        }

        // Auto-disengage if player provides other input
        if (cruiseControl && cruiseAutoDisengageOnInput)
        {
            bool pressedMoveKeys =
                Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            bool pressedBoost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetMouseButton(0);

            // If they are trying to manually control anything (other than just toggling X), drop cruise.
            // (Mouse look does NOT count as input here.)
            if ((pressedMoveKeys || pressedBoost) && !_cruiseTogglePressed)
                cruiseControl = false;
        }
        UpdateCruiseUI();
        UpdateAutoStabilizeUI();

        // --- CRUISE OVERRIDE ---
        // If cruise is active, force intent even if player isn't holding movement keys.
        if (cruiseControl)
        {
            moveDir = _cruiseMoveDir;   // stored world direction
            input = _cruiseBankInput;   // stored input (optional flair)

            // Note: we don't change boostOnlyIntent here — cruise is "movement intent".
            hasMoveInput = true;
            hasInput = true;
        }

        // --- BOOSTED SPEED/ACCEL (ADDITIVE) ---
        float boostedMaxSpeed = maxSpeed + (boostMaxSpeed * boostCharge);

        // optional extra snap while boosting (additive)
        float boostedAccel = acceleration + (boostAccelAdd * boostCharge);

        // Slip influence: 1 at full slip, 0 at normal
        float slip01 = (enableSlip ? slipFactor : 0f);

        // Steering authority while slipping
        float slipAccelScale = Mathf.Lerp(1f, slipAccelMultiplier, slip01);
        float accelForSteering = boostedAccel * slipAccelScale;

        // Damping while slipping (when no input)
        float slipDampScale = Mathf.Lerp(1f, slipDampingMultiplier, slip01); // < 1 = less damping

        // If boosting with no movement input, boost along ship forward
        if (boostOnlyIntent)
        {
            // Use the ship's current forward direction
            moveDir = rb.rotation * Vector3.forward;
        }

        // --- DESIRED VELOCITY (camera-relative preserved) ---
        Vector3 desiredVel = moveDir * boostedMaxSpeed;

        // --- MOVE (INERTIA) ---
        Vector3 currentVel = rb.linearVelocity;
        Vector3 newVel = currentVel;

        if (hasInput && moveDir.sqrMagnitude > 0.0001f)
        {
            // Steer toward desired velocity, but slip can reduce steering authority.
            newVel = Vector3.MoveTowards(currentVel, desiredVel, accelForSteering * dt);
        }
        else
        {
            // No input: coast. Only apply drag/damping (slip reduces it further).
            newVel = currentVel;

            if (damping > 0f)
            {
                float effectiveDamping = damping * slipDampScale;
                float dampFactor = Mathf.Exp(-effectiveDamping * dt);
                newVel *= dampFactor;
            }
        }

        rb.linearVelocity = newVel;

        // Drive thruster VFX (free-flight only).
        if (PlayerThrustManager.Instance)
        {
            // This "input" already exists in your code.
            // speed01 normalized by max speed * boost cap (matches your velocity text logic).
            float speed01 = Mathf.InverseLerp(0f, maxSpeed + boostMaxSpeed, rb.linearVelocity.magnitude);

            // boostCharge is private in your file; if you keep it private, just pass 0f.
            // If you want, make a public getter for it. For now we’ll ignore boost:
            PlayerThrustManager.Instance.SetFreeFlight(input, speed01, boostCharge);
        }

        // --- ROTATION (keep existing flow, but make facing 3D + camera-relative) ---
        if (enableRotation)
        {
            // Consider us "wanting rotation" if we are moving OR rolling OR have some velocity
            bool hasVel = rb.linearVelocity.sqrMagnitude > (minSpeedToRotate * minSpeedToRotate);
            bool hasManualRollIntent = Mathf.Abs(_manualRoll) > 0.001f; // stored roll (or you can use roll key input)

            if (hasMoveInput || hasVel || hasManualRollIntent)
            {
                // Face direction preference:
                // 1) If player is inputting movement, face desiredVel (intent)
                // 2) else keep current forward (so roll still works while stationary)
                Vector3 faceDir;
                if (hasMoveInput && desiredVel.sqrMagnitude > 0.0001f)
                    faceDir = desiredVel.normalized;
                else
                    faceDir = rb.rotation * Vector3.forward;

                Quaternion target = ComputeFacing3D(faceDir);

                // If no movement input, don't inject bank (otherwise you’ll bank back to 0 weirdly)
                Vector3 bankInput = hasMoveInput ? input : Vector3.zero;

                Quaternion styled = ApplyBankAndWobble(target, bankInput, rb.linearVelocity);

                // Apply manual roll around the ship's forward axis AFTER styling (always)
                Vector3 shipForward = styled * Vector3.forward;
                Quaternion manualRollRot = Quaternion.AngleAxis(_manualRoll, shipForward);
                styled = manualRollRot * styled;

                float t = 1f - Mathf.Exp(-rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, styled, t));
            }
        }
    }
    private void LateUpdate()
    {
        if (!debugVelocity || velocityText == null || rb == null)
            return;

        float speed = rb.linearVelocity.magnitude;

        if (!float.IsNaN(_lastSpeed) && Mathf.Abs(speed - _lastSpeed) < 0.02f)
            return;

        _lastSpeed = speed;

        if (SpeedHUD.Instance)
            SpeedHUD.Instance.SetSpeed(rb.linearVelocity.magnitude);
    }

    private Quaternion ComputeFacing3D(Vector3 dir)
    {
        // Base orientation to correct mesh forward axis
        Quaternion baseRot = Quaternion.Euler(baseEulerOffset);

        // Use camera up if available (helps keep roll stable with a chase cam),
        // otherwise fall back to world up.
        Vector3 up = cameraTransform ? cameraTransform.up : Vector3.up;

        // Guard against degenerate look rotations
        if (dir.sqrMagnitude < 0.000001f)
            return rb.rotation;

        Quaternion steer = Quaternion.LookRotation(dir, up);
        return steer * baseRot;
    }

    private Quaternion ApplyBankAndWobble(Quaternion facing, Vector3 input, Vector3 vel)
    {
        // Bank based on strafe + forward (and a *little* on vertical)
        float targetBank = 0f;

        // Right/left = roll main driver
        targetBank += -input.x * bankDegrees;

        // Forward/back adds a little flair
        targetBank += -input.z * (bankDegrees * 0.25f);

        // Vertical adds subtle flair (optional but usually feels nice)
        targetBank += -input.y * (bankDegrees * 0.15f);

        float bankT = 1f - Mathf.Exp(-bankResponse * Time.fixedDeltaTime);
        _bank = Mathf.Lerp(_bank, targetBank, bankT);

        // Thrust wobble only while moving
        float wobble = 0f;
        if (thrustWobbleDegrees > 0f && vel.magnitude > minSpeedToRotate)
        {
            _wobbleT += Time.fixedDeltaTime * thrustWobbleSpeed;
            wobble = Mathf.Sin(_wobbleT) * thrustWobbleDegrees;
        }

        // Apply roll around the ship's forward axis in its faced orientation
        Vector3 forwardAxis = facing * Vector3.forward;
        Quaternion roll = Quaternion.AngleAxis(_bank + wobble, forwardAxis);

        return roll * facing;
    }
    private void UpdateCruiseUI(bool force = false)
    {
        if (CruiseControlBkRnd == null) return;

        if (!force && cruiseControl == _lastCruiseState) return;
        _lastCruiseState = cruiseControl;

        if (cruiseControl && cruiseOnMat != null)
            CruiseControlBkRnd.material = cruiseOnMat;
        else if (!cruiseControl && cruiseOffMat != null)
            CruiseControlBkRnd.material = cruiseOffMat;
    }
    private bool _lastAutoStabilizeState = false;

    private void UpdateAutoStabilizeUI(bool force = false)
    {
        if (AutoStabilizeBkRnd == null) return;

        bool state = isAutoStabilizingRoll;

        if (!force && state == _lastAutoStabilizeState) return;
        _lastAutoStabilizeState = state;

        if (state && autoStabilizeOnMat != null)
            AutoStabilizeBkRnd.material = autoStabilizeOnMat;
        else if (!state && autoStabilizeOffMat != null)
            AutoStabilizeBkRnd.material = autoStabilizeOffMat;
    }
    void OnCollisionStay(Collision c)
    {
        Debug.Log("Colliding with: " + c.collider.name);
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;

        if (paused)
        {
            // Kill temporary movement states that can get stuck
            _cruiseTogglePressed = false;
            cruiseControl = false;
            _wasBoosting = false;
            isAutoStabilizingRoll = false;
            _rollIdleTimer = 0f;

            // Optional: dump boost charge so boost can't resume weirdly
            boostCharge = 0f;

            if (BoostManager.Instance)
                BoostManager.Instance.SetBoostInput(false, false);

            UpdateCruiseUI(force: true);
            UpdateAutoStabilizeUI(force: true);
        }
    }
}
