using System;
using System.Collections;
using UnityEngine;

public class SimpleFollowCamera : MonoBehaviour
{
    public static SimpleFollowCamera Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Tooltip("Optional. If set, we use this RB velocity for look-ahead.")]
    public Rigidbody targetRb;

    [Header("Follow")]
    public bool keepInitialOffset = true;
    public Vector3 manualOffset = new Vector3(0f, 6f, -10f);
    public float positionSmooth = 12f;

    [Header("Look Around (Mouse)")]
    public float mouseSensitivity = 3.5f;

    [Header("Roll (Q/E)")]
    public float rollSpeed = 120f; // degrees per second

    [Header("Velocity Look-Ahead")]
    public bool enableLookAhead = true;

    [Tooltip("Max distance (meters) the camera can shift in the velocity direction.")]
    public float lookAheadMaxDistance = 4.0f;

    [Tooltip("How quickly the look-ahead vector reacts (higher = snappier).")]
    public float lookAheadSharpness = 10f;

    [Tooltip("Ignore tiny velocity jitters under this speed.")]
    public float lookAheadMinSpeed = 0.5f;

    [Tooltip("If > 0, look-ahead scales by speed / this value (clamped 0..1).")]
    public float lookAheadFullAtSpeed = 40f;

    [Header("Impulse Shake")]
    public bool enableShake = true;

    [Tooltip("How quickly shake fades out. Higher = shorter shake.")]
    public float shakeDamping = 18f;

    [Tooltip("Max shake offset (meters).")]
    public float shakeMaxOffset = 0.6f;

    [Header("FOV (Optional)")]
    public bool enableFov = true;

    [Tooltip("If null, will auto-fetch Camera on this GameObject or children.")]
    public Camera cam;

    [Tooltip("If 0, uses current camera FOV at Start.")]
    public float baseFov = 0f;

    [Tooltip("Hard clamp for readability.")]
    public float minFov = 55f;
    public float maxFov = 95f;

    [Tooltip("Ignore tiny changes to prevent micro-wobble.")]
    public float fovDeadzone = 0.15f;

    public enum FovMode { None, FreeFlight, Boost, DriftHold, Orbiting }
    public FovMode fovMode = FovMode.FreeFlight;

    // Free-flight config
    [NonSerialized] public float freeMaxSpeed = 8f;         // will be set by caller
    [NonSerialized] public float freeMaxAdd = 4f;           // +0..+4 typically
    [NonSerialized] public float freeSharpness = 6f;        // smoothing rate

    // Boost config
    [NonSerialized] public float boostMaxSpeed = 16f;       // maxSpeed + boostMaxSpeedAdd
    [NonSerialized] public float boostExtraAdd = 5f;        // extra on top of free
    [NonSerialized] public float boostSharpnessUp = 12f;    // fast in
    [NonSerialized] public float boostSharpnessDown = 5f;   // slow out

    [Header("Stabilization (Optional)")]
    [Tooltip("0 = no auto-level. Higher = camera slowly untwists toward referenceUp.")]
    public float autoLevel = 0f;

    public Vector3 referenceUp = Vector3.up;

    private Vector3 _offset;
    private Vector3 _lookAheadCurrent; // smoothed world-space look-ahead
    private Vector3 _shakeOffset; // world-space or camera-space; we'll use camera-space

    private bool _cursorWasLocked;
    private bool _lastUIOpen;

    // Runtime FOV
    private float _currentFov;
    private float _targetFov;
    private float _driftHoldUntil = -1f;
    private float _driftHoldFovAdd = 0f;
    private Coroutine _fovKickRoutine;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (!target) return;

        if (cam)
        {
            if (baseFov <= 0.01f) baseFov = cam.fieldOfView;
            _currentFov = cam.fieldOfView;
            _targetFov = cam.fieldOfView;
        }

        _offset = keepInitialOffset ? (transform.position - target.position) : manualOffset;

        if (!targetRb)
        {
            targetRb = target.GetComponentInChildren<Rigidbody>();
            if (!targetRb) targetRb = target.GetComponent<Rigidbody>();
        }

        _lastUIOpen = UIBlock.IsUIOpen;
        ApplyCursorState(_lastUIOpen);
    }

    private void LateUpdate()
    {
        if (!target) return;

        bool uiOpen = UIBlock.IsUIOpen;
        if (uiOpen != _lastUIOpen)
        {
            _lastUIOpen = uiOpen;
            ApplyCursorState(uiOpen);
        }

        float dt = Time.deltaTime;

        UpdateFov(dt);

        // Only allow look controls when UI is closed
        if (!uiOpen)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            Vector3 yawAxis = transform.up;
            Vector3 pitchAxis = transform.right;

            transform.Rotate(yawAxis, mx, Space.World);
            transform.Rotate(pitchAxis, -my, Space.World);

            float rollInput = 0f;
            if (Input.GetKey(KeyCode.Q)) rollInput += 1f;
            if (Input.GetKey(KeyCode.E)) rollInput -= 1f;

            if (rollInput != 0f)
                transform.Rotate(transform.forward, rollInput * rollSpeed * dt, Space.World);

            if (autoLevel > 0f)
            {
                Vector3 f = transform.forward;
                if (f.sqrMagnitude > 1e-6f)
                {
                    Quaternion leveled = Quaternion.LookRotation(f, referenceUp);
                    float t = 1f - Mathf.Exp(-autoLevel * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, leveled, t);
                }
            }
        }

        // --- Velocity look-ahead (world-space) ---
        Vector3 desiredLookAhead = Vector3.zero;

        if (enableLookAhead && targetRb)
        {
            Vector3 v = targetRb.linearVelocity; // matches your usage elsewhere
            float speed = v.magnitude;

            if (speed > lookAheadMinSpeed)
            {
                float speed01 = 1f;
                if (lookAheadFullAtSpeed > 0.0001f)
                    speed01 = Mathf.Clamp01(speed / lookAheadFullAtSpeed);

                desiredLookAhead = v.normalized * (lookAheadMaxDistance * speed01);
            }
        }

        // Smooth the look-ahead so it doesn't jitter
        {
            float t = (lookAheadSharpness <= 0f) ? 1f : (1f - Mathf.Exp(-lookAheadSharpness * dt));
            _lookAheadCurrent = Vector3.Lerp(_lookAheadCurrent, desiredLookAhead, t);
        }

        // --- Shake decay ---
        if (enableShake)
        {
            float k = 1f - Mathf.Exp(-shakeDamping * dt);
            _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, k);
        }

        // --- Follow position using rotated offset + look-ahead + shakeOffset ---
        Vector3 desiredPos = target.position + (transform.rotation * _offset) + _lookAheadCurrent + _shakeOffset;

        if (positionSmooth <= 0f)
        {
            transform.position = desiredPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-positionSmooth * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        }
    }

    // ---------------------------
    // Optional “API” for other scripts (nice if you go state-based later)
    // ---------------------------
    public void SetTarget(Transform newTarget, Rigidbody newRb = null)
    {
        target = newTarget;
        targetRb = newRb;

        if (target && keepInitialOffset)
            _offset = (transform.position - target.position);
    }

    public void SetLookAheadEnabled(bool enabled) => enableLookAhead = enabled;

    public void SetLookAheadTuning(float maxDistance, float fullAtSpeed)
    {
        lookAheadMaxDistance = maxDistance;
        lookAheadFullAtSpeed = fullAtSpeed;
    }

    public void ClearLookAhead()
    {
        _lookAheadCurrent = Vector3.zero;
    }

    public void AddShakeImpulse(Vector3 worldDir, float strength)
    {
        if (!enableShake) return;

        // strength expected ~0..1+, clamp it
        float s = Mathf.Clamp01(strength);

        // Directional kick: convert world dir into camera space so it shakes relative to view
        Vector3 dir = (worldDir.sqrMagnitude > 1e-6f) ? worldDir.normalized : UnityEngine.Random.onUnitSphere;

        // Slight randomness so it doesn't feel robotic
        Vector3 rand = UnityEngine.Random.insideUnitSphere * 0.35f;

        // Shake in camera space (mostly opposite impact direction)
        Vector3 impulseWorld = (dir + rand).normalized * (s * shakeMaxOffset);

        _shakeOffset += impulseWorld;
    }
    private void ApplyCursorState(bool uiOpen)
    {
        if (uiOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---------------------------
    // FOV API (event / mode based)
    // ---------------------------
    public void SetFreeFlightFOV(float maxSpeed, float maxAdd = 4f, float sharpness = 6f)
    {
        freeMaxSpeed = Mathf.Max(0.01f, maxSpeed);
        freeMaxAdd = maxAdd;
        freeSharpness = Mathf.Max(0f, sharpness);
        fovMode = FovMode.FreeFlight;
    }

    public void SetBoostFOV(float maxBoostSpeed, float extraAdd = 5f, float sharpnessUp = 12f, float sharpnessDown = 5f)
    {
        boostMaxSpeed = Mathf.Max(0.01f, maxBoostSpeed);
        boostExtraAdd = extraAdd;
        boostSharpnessUp = Mathf.Max(0f, sharpnessUp);
        boostSharpnessDown = Mathf.Max(0f, sharpnessDown);
        fovMode = FovMode.Boost;
    }

    public void SetFovMode(FovMode mode)
    {
        fovMode = mode;
    }

    public void SetDriftHold(float fovAdd, float duration)
    {
        _driftHoldFovAdd = fovAdd;
        _driftHoldUntil = Time.time + Mathf.Max(0f, duration);
        fovMode = FovMode.DriftHold;
    }

    public void SetOrbiting()
    {
        fovMode = FovMode.Orbiting;
    }

    // “Launch punch” — quick kick that eases out, then returns to whatever mode you choose.
    public void PlaySlingshotKick(float kickAdd = 10f, float inTime = 0.08f, float holdTime = 0.05f, float outTime = 0.35f, FovMode returnMode = FovMode.FreeFlight)
    {
        if (_fovKickRoutine != null) StopCoroutine(_fovKickRoutine);
        _fovKickRoutine = StartCoroutine(FovKickRoutine(kickAdd, inTime, holdTime, outTime, returnMode));
    }
    private IEnumerator FovKickRoutine(float kickAdd, float inTime, float holdTime, float outTime, FovMode returnMode)
    {
        if (!enableFov || !cam) yield break;

        // Temporarily go None so the state machine doesn't fight the kick.
        var prevMode = fovMode;
        fovMode = FovMode.None;

        float start = _currentFov;
        float peak = Mathf.Clamp(baseFov + kickAdd, minFov, maxFov);

        // Ease in
        if (inTime > 0.0001f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / inTime;
                _targetFov = Mathf.Lerp(start, peak, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
        }
        _targetFov = peak;

        // Hold
        if (holdTime > 0.0001f)
            yield return new WaitForSeconds(holdTime);

        // Ease out
        float end = Mathf.Clamp(baseFov, minFov, maxFov);
        if (outTime > 0.0001f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / outTime;
                _targetFov = Mathf.Lerp(peak, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
        }
        _targetFov = end;

        fovMode = returnMode; // or prevMode if you prefer
        _fovKickRoutine = null;
    }

    private void UpdateFov(float dt)
    {
        if (!enableFov || !cam) return;

        // If in drift-hold mode, hold additive FOV for duration
        if (fovMode == FovMode.DriftHold)
        {
            if (Time.time <= _driftHoldUntil)
            {
                _targetFov = Mathf.Clamp(baseFov + _driftHoldFovAdd, minFov, maxFov);
            }
            else
            {
                // fall back to FreeFlight after hold ends (you can change this)
                fovMode = FovMode.FreeFlight;
            }
        }

        // Normal modes
        if (fovMode == FovMode.FreeFlight || fovMode == FovMode.Boost)
        {
            float speed = 0f;
            if (targetRb) speed = targetRb.linearVelocity.magnitude;

            float targetAdd = 0f;

            // Free-flight portion (0..freeMaxAdd)
            float free01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, freeMaxSpeed));
            targetAdd += free01 * freeMaxAdd;

            if (fovMode == FovMode.Boost)
            {
                // Extra boost portion only above free max range
                float boostRange = Mathf.Max(0.01f, boostMaxSpeed - freeMaxSpeed);
                float boost01 = Mathf.Clamp01((speed - freeMaxSpeed) / boostRange);
                targetAdd += boost01 * boostExtraAdd;
            }

            _targetFov = Mathf.Clamp(baseFov + targetAdd, minFov, maxFov);
        }

        // Deadzone to prevent micro-wobble
        if (Mathf.Abs(_targetFov - _currentFov) < fovDeadzone)
            _targetFov = _currentFov;

        // Mode-specific smoothing (Boost = fast up, slower down)
        float sharpness = freeSharpness;

        if (fovMode == FovMode.Boost)
            sharpness = (_targetFov > _currentFov) ? boostSharpnessUp : boostSharpnessDown;

        float t = (sharpness <= 0f) ? 1f : (1f - Mathf.Exp(-sharpness * dt));
        _currentFov = Mathf.Lerp(_currentFov, _targetFov, t);

        cam.fieldOfView = _currentFov;
    }
}
