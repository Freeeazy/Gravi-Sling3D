using UnityEngine;
using UnityEngine.UI;

/// Maps ship speed to the RetroPegMeter_UI and supplies a continuous redline effect.
/// Keeps the original orbit API: Instance, SetSpeed and ClearExternalSpeed.
[DefaultExecutionOrder(-100)] // Supply targets before the meter's Update.
public class SpeedometerHUD : MonoBehaviour
{
    public static SpeedometerHUD Instance { get; private set; }

    [Header("Meter")]
    [SerializeField] private RetroPegMeter_UI pegMeter;
    [SerializeField] private Rigidbody shipRb;

    [Header("Speed Mapping (m/s)")]
    public float freeFlightMaxSpeed = 200f;
    [Range(0f, 1f)] public float freeFlightFillPortion = 0.25f;
    public float redlineSpeed = 800f;
    public float maxSpeed = 1100f;

    [Header("Height input")]
    [Tooltip("Temporary approximation until SetEngineLoad is connected. Speed is not engine effort.")]
    [SerializeField] private bool useSpeedAsLoadFallback = true;
    [SerializeField, Range(0f, 1f)] private float fallbackLoad01;

    [Header("Redline region")]
    [Range(0f, 1f)] public float redlineStart01 = 0.78f;

    [Header("Redline pulse - continuous, never fully black")]
    [SerializeField] private bool animateRedline = true;
    [SerializeField, Min(0f)] private float pulseHzMin = 2.2f;
    [SerializeField, Min(0f)] private float pulseHzMax = 5.0f;
    [Tooltip("At maximum overspeed, warning lamps dip by this fraction. Green stays steady.")]
    [SerializeField, Range(0f, 0.8f)] private float maximumDim = 0.45f;
    [Tooltip("Fraction of the full width. 0.035 is about 1.7 columns on a 48-column meter.")]
    [SerializeField, Range(0f, 0.1f)] private float maximumTipRetraction = 0.035f;
    [Tooltip("Seconds to approach 95% of a new overspeed effect strength.")]
    [SerializeField, Min(0f)] private float redlineAttack = 0.18f;
    [SerializeField, Min(0f)] private float redlineRelease = 0.45f;

    [Header("Material Indicator")]
    [SerializeField] private Graphic indicatorGraphic;
    [SerializeField] private Material indicatorOnMaterial;
    [SerializeField] private Material indicatorRedlineMaterial;

    [Tooltip("At or below this speed, the indicator uses no custom material.")]
    [SerializeField, Min(0f)] private float indicatorOffSpeed = 0.1f;

    [Header("Speed smoothing")]
    [Tooltip("Legacy exponential response rate. 0 means immediate. Set meter Sweep Attack/Release to 0 to avoid double smoothing.")]
    public float speedSmooth = 12f;
    [SerializeField] private bool useUnscaledTime = true;

    public float CurrentSpeed { get; private set; }
    private float externalSpeed;
    private bool hasExternalSpeed;
    private float engineLoad;
    private bool hasEngineLoad;
    private float redlineStrength;
    private float pulsePhase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SetIndicatorMaterial(null);
        if (!pegMeter)
            Debug.LogError("SpeedometerHUD: Assign the RetroPegMeter_UI component.", this);
    }

    private void Update()
    {
        if (Instance != this) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float targetSpeed = GetTargetSpeed();
        float rate = NonNegative(speedSmooth);
        float speedBlend = rate <= 0f ? 1f : 1f - Mathf.Exp(-rate * dt);
        CurrentSpeed = Mathf.Lerp(CurrentSpeed, targetSpeed, speedBlend);
        if (Mathf.Abs(CurrentSpeed - targetSpeed) < 0.001f) CurrentSpeed = targetSpeed;

        float fill = SpeedToFill01(CurrentSpeed);
        float load = hasEngineLoad ? engineLoad :
            (useSpeedAsLoadFallback ? fill : Safe01(fallbackLoad01));
        if (!pegMeter) return;
        pegMeter.SetTelemetry(load, fill);
        UpdateRedline(dt);
    }

    private float GetTargetSpeed()
    {
        if (hasExternalSpeed) return externalSpeed;
        if (!shipRb) return 0f;
#if UNITY_6000_0_OR_NEWER
        return NonNegative(shipRb.linearVelocity.magnitude);
#else
        return NonNegative(shipRb.velocity.magnitude);
#endif
    }

    private float SpeedToFill01(float speed)
    {
        float end = NonNegative(redlineSpeed);
        if (end <= 0f) return 0f;
        float split = NonNegative(freeFlightMaxSpeed);
        // Invalid/collapsed split gets a well-defined linear mapping.
        if (split <= 0f || split >= end) return Mathf.Clamp01(speed / end);
        float portion = Safe01(freeFlightFillPortion);
        return speed <= split
            ? Mathf.Lerp(0f, portion, Mathf.Clamp01(speed / split))
            : Mathf.Lerp(portion, 1f, Mathf.InverseLerp(split, end, speed));
    }

    private void UpdateRedline(float dt)
    {
        float start = NonNegative(redlineSpeed);
        float end = Mathf.Max(start + 0.01f, NonNegative(maxSpeed));
        float targetStrength = animateRedline && start > 0f ? Mathf.InverseLerp(start, end, CurrentSpeed) : 0f;
        float duration = NonNegative(targetStrength > redlineStrength ? redlineAttack : redlineRelease);

        if (dt > 0f)
        {
            float blend = duration <= 0f ? 1f : 1f - Mathf.Exp(-3f * dt / duration);
            redlineStrength = Mathf.Lerp(redlineStrength, targetStrength, blend);
            if (Mathf.Abs(redlineStrength - targetStrength) < 0.0001f)
                redlineStrength = targetStrength;
        }

        if (!animateRedline || redlineStrength <= 0f)
        {
            pulsePhase = 0f;
            redlineStrength = 0f;
            pegMeter.SetWarningEffect(redlineStart01, 1f, 0f);
            UpdateMaterialIndicator();
            return;
        }

        float minHz = NonNegative(pulseHzMin);
        float maxHz = Mathf.Max(minHz, NonNegative(pulseHzMax));
        float hz = Mathf.Lerp(minHz, maxHz, redlineStrength);
        // Integrate frequency: a speed change never jumps the oscillator phase.
        pulsePhase = Mathf.Repeat(pulsePhase + hz * dt, 1f);
        float wave = 0.5f - 0.5f * Mathf.Cos(pulsePhase * Mathf.PI * 2f);
        float dip = wave * wave * wave * redlineStrength;
        float brightness = 1f - Mathf.Clamp(maximumDim, 0f, 0.8f) * dip;
        float retraction = Mathf.Clamp(maximumTipRetraction, 0f, 0.1f) * dip;
        pegMeter.SetWarningEffect(redlineStart01, brightness, retraction);
        UpdateMaterialIndicator(wave);
    }

    private void UpdateMaterialIndicator(float wave = 0f)
    {
        Material nextMaterial = null;

        if (CurrentSpeed > NonNegative(indicatorOffSpeed))
        {
            if (redlineStrength > 0f)
            {
                // Red material during the bright half of the meter's pulse.
                // No custom material during the dim half.
                nextMaterial = wave < 0.5f
                    ? indicatorRedlineMaterial
                    : null;
            }
            else
            {
                nextMaterial = indicatorOnMaterial;
            }
        }

        SetIndicatorMaterial(nextMaterial);
    }

    private void SetIndicatorMaterial(Material nextMaterial)
    {
        if (!indicatorGraphic) return;

        // Unity's material getter returns the default material when none is assigned.
        Material effectiveMaterial = nextMaterial
            ? nextMaterial
            : indicatorGraphic.defaultMaterial;

        if (indicatorGraphic.material != effectiveMaterial)
            indicatorGraphic.material = nextMaterial;
    }

    // Existing orbit/slingshot callers can keep using these unchanged.
    public void SetSpeed(float speed)
    {
        externalSpeed = NonNegative(speed);
        hasExternalSpeed = true;
    }

    public void ClearExternalSpeed() { hasExternalSpeed = false; }

    // Supply actual normalized engine effort independently of ship speed.
    // This override persists until explicitly cleared, just like SetSpeed.
    public void SetEngineLoad(float load01)
    {
        engineLoad = Safe01(load01);
        hasEngineLoad = true;
    }

    public void ClearEngineLoad() { hasEngineLoad = false; }

    private void OnDisable()
    {
        if (Instance != this) return;
        redlineStrength = 0f;
        pulsePhase = 0f;
        if (pegMeter) pegMeter.SetWarningEffect(redlineStart01, 1f, 0f);
        SetIndicatorMaterial(null);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static float NonNegative(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    private static float Safe01(float value) { return Mathf.Clamp01(NonNegative(value)); }
}
