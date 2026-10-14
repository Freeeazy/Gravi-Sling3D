
using UnityEngine;
using UnityEngine.UI;

public class BoostManager : MonoBehaviour
{
    public static BoostManager Instance { get; private set; }

    public enum Mode
    {
        FreeFlight,
        OrbitIdle,
        OrbitCharging
    }

    [Header("Mode (Read Only)")]
    [SerializeField] private Mode _mode = Mode.FreeFlight;

    [Header("Energy")]
    public float capacity = 100f;
    public bool startFull = true;
    public float startEnergy = 100f;
    public float drainPerSecond = 18f;
    public bool dontDrain = false;
    public float regenPerSecond = 35f;

    [Header("Boost Smoothing")]
    public float boostRampUp = 1.2f;
    public float boostRampDown = 2.0f;

    [Header("UI Pegs")]
    public Transform pegsParent;
    public Material pegLitMat;
    public Material pegUnlitMat;
    public bool reversePegOrder = false;
    [Min(1)] public int pegCount = 10;

    [Header("Peg Recharge Animation")]
    [SerializeField] private bool animateRecharge = true;

    [Tooltip("Tint applied to unlit pegs during the passing recharge wave.")]
    [SerializeField] private Color rechargeTint = new Color(0.3f, 0.9f, 1f, 1f);

    [Tooltip("Time for one recharge wave to cross all pegs.")]
    [Min(0.1f)]
    [SerializeField] private float rechargeWaveDuration = 0.85f;

    [Tooltip("How much the recharge wave enlarges each peg.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float rechargeScale = 0.12f;

    [Tooltip("How wide the passing wave appears.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float rechargeWaveWidth = 0.22f;

    [Header("Peg Overcharge Animation")]
    [SerializeField] private bool animateOvercharge = true;

    [Tooltip("Time for one overcharge wave to cross all pegs.")]
    [Min(0.1f)]
    [SerializeField] private float overchargeWaveDuration = 0.65f;

    [Tooltip("Maximum scale increase of a peg during overcharge.")]
    [Range(0f, 1f)]
    [SerializeField] private float overchargeScale = 0.3f;

    [SerializeField]
    private Color overchargeTint = new Color(0.5f, 1f, 1f, 1f);

    [Range(0.05f, 0.5f)]
    [SerializeField] private float overchargeWaveWidth = 0.20f;

    [Header("Cargo Boost Override")]
    [SerializeField] private bool _forcedBoostActive = false;

    [Header("Boost Light")]
    [SerializeField] private Image boostLight;

    [Tooltip("Optional red material for the draining state. Falls back to pegLitMat.")]
    [SerializeField] private Material boostDrainMat;

    [Header("Boost Light Animation")]
    [SerializeField] private float rechargePulseSpeed = 2.5f;
    [SerializeField] private float drainPulseSpeed = 8f;
    [SerializeField] private float overchargePulseSpeed = 12f;

    [Range(0f, 1f)]
    [SerializeField] private float minimumPulseBrightness = 0.4f;

    private Color _boostLightBaseColor;
    private bool _isDraining;
    public bool IsForcedBoostActive => _forcedBoostActive;

    public GameObject OverchargeParticle;

    private float _energy;
    private float _boost01;

    private bool _boostHeld;
    private bool _hasMoveInput;
    private bool _isRecharging;

    private Image[] _pegImages;
    private Vector3[] _pegBaseScales;
    private Color[] _pegBaseColors;

    private float _rechargeWaveTimer;
    private float _overchargeWaveTimer;

    public float Energy => _energy;

    public float Energy01 =>
        capacity <= 0.0001f
            ? 0f
            : Mathf.Clamp01(_energy / capacity);

    public float Boost01 => _boost01;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _energy = startFull
            ? capacity
            : Mathf.Clamp(startEnergy, 0f, capacity);

        CachePegImages();

        if (boostLight != null)
        {
            _boostLightBaseColor = boostLight.color;
            boostLight.material = null;
        }

        ApplyPegMats(force: true);
        SyncOverchargeParticle();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        ResetPegVisuals();

        if (boostLight != null)
        {
            boostLight.material = null;
            boostLight.color = _boostLightBaseColor;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float previousEnergy = _energy;
        _isDraining = false;

        bool canBoostState = _mode == Mode.FreeFlight;
        bool hasEnergy = _energy > 0.0001f;

        bool forcedBoosting = canBoostState && _forcedBoostActive;

        bool normalBoosting = canBoostState && _boostHeld && _hasMoveInput && hasEnergy;

        bool boostingWanted = forcedBoosting || normalBoosting;

        float delta = boostingWanted ? boostRampUp : -boostRampDown;

        _boost01 = Mathf.Clamp01(_boost01 + delta * dt);

        // Normal energy drain.
        if (normalBoosting && !forcedBoosting && !dontDrain && drainPerSecond > 0f)
        {
            _energy -= drainPerSecond * _boost01 * dt;

            if (_energy <= 0f)
            {
                _energy = 0f;
                _boost01 = 0f;
            }
        }

        // Forced boost energy regeneration.
        if (forcedBoosting && capacity > 0f)
        {
            _energy = Mathf.MoveTowards(
                _energy,
                capacity,
                regenPerSecond * dt
            );
        }

        // Orbit idle regeneration.
        if (_mode == Mode.OrbitIdle && capacity > 0f)
        {
            _energy += capacity * 0.005f * dt;
            _energy = Mathf.Min(_energy, capacity);
        }

        // Orbit charging regeneration.
        if (_mode == Mode.OrbitCharging && regenPerSecond > 0f)
        {
            _energy += regenPerSecond * dt;
            _energy = Mathf.Min(_energy, capacity);
        }

        // Detect actual energy regeneration.
        _isRecharging = _energy > previousEnergy + 0.000001f && _energy < capacity - 0.0001f;

        // Debug refill.
        if (Input.GetKeyDown(KeyCode.B))
        {
            _energy = capacity;
            _boost01 = 0f;
            _isRecharging = false;

            Debug.Log("[BoostManager] Cheat refill activated.");
        }

        // Detect actual energy consumption.
        _isDraining = _energy < previousEnergy - 0.000001f;

        ApplyPegMats();
    }

    private void Update()
    {
        UpdatePegAnimations(Time.deltaTime);
        UpdateBoostLight();
    }

    private void CachePegImages()
    {
        if (pegsParent == null)
            return;

        Image[] found =
            pegsParent.GetComponentsInChildren<Image>(true);

        Image parentImage = pegsParent.GetComponent<Image>();

        int count = 0;

        foreach (Image img in found)
        {
            if (img != parentImage)
                count++;
        }

        _pegImages = new Image[count];
        _pegBaseScales = new Vector3[count];
        _pegBaseColors = new Color[count];

        int index = 0;

        foreach (Image img in found)
        {
            if (img == parentImage)
                continue;

            _pegImages[index] = img;
            _pegBaseScales[index] = img.rectTransform.localScale;
            _pegBaseColors[index] = img.color;

            index++;
        }
    }

    private void ApplyPegMats(bool force = false)
    {
        if (_pegImages == null ||
            pegLitMat == null ||
            pegUnlitMat == null)
            return;

        int n = Mathf.Min(pegCount, _pegImages.Length);

        if (n <= 0)
            return;

        int lit = Mathf.FloorToInt(Energy01 * n);

        if (capacity > 0f &&
            _energy >= capacity - 0.0001f)
        {
            lit = n;
        }

        lit = Mathf.Clamp(lit, 0, n);

        for (int i = 0; i < n; i++)
        {
            int index = reversePegOrder
                ? n - 1 - i
                : i;

            Image img = _pegImages[index];

            if (img == null)
                continue;

            Material target =
                i < lit ? pegLitMat : pegUnlitMat;

            if (force || img.material != target)
                img.material = target;
        }
    }

    private void UpdatePegAnimations(float dt)
    {
        if (_pegImages == null)
            return;

        int n = Mathf.Min(pegCount, _pegImages.Length);

        if (n <= 0)
            return;

        bool overcharging =
            _forcedBoostActive && animateOvercharge;

        bool recharging =
            _isRecharging &&
            animateRecharge &&
            !overcharging;

        // Keep wave timing independent.
        if (overcharging)
        {
            _overchargeWaveTimer += dt;
        }
        else
        {
            _overchargeWaveTimer = 0f;
        }

        if (recharging)
        {
            _rechargeWaveTimer += dt;
        }
        else
        {
            _rechargeWaveTimer = 0f;
        }

        int lit = Mathf.Clamp(
            Mathf.FloorToInt(Energy01 * n),
            0,
            n
        );

        if (capacity > 0f &&
            _energy >= capacity - 0.0001f)
        {
            lit = n;
        }

        for (int i = 0; i < n; i++)
        {
            int index = reversePegOrder
                ? n - 1 - i
                : i;

            Image img = _pegImages[index];

            if (img == null)
                continue;

            float intensity = 0f;
            float scaleAmount = 0f;
            Color targetTint = _pegBaseColors[index];

            // Normalized position within the stack.
            // i = 0 is the first filled/bottom peg.
            float pegPosition = (i + 0.5f) / n;

            if (overcharging)
            {
                intensity = CalculateWave(
                    _overchargeWaveTimer,
                    overchargeWaveDuration,
                    pegPosition,
                    overchargeWaveWidth
                );

                scaleAmount = overchargeScale;

                targetTint = Color.Lerp(
                    _pegBaseColors[index],
                    overchargeTint,
                    intensity
                );
            }
            else if (recharging && i >= lit)
            {
                // Animate only unlit pegs.
                intensity = CalculateWave(
                    _rechargeWaveTimer,
                    rechargeWaveDuration,
                    pegPosition,
                    rechargeWaveWidth
                );

                scaleAmount = rechargeScale;

                targetTint = Color.Lerp(
                    _pegBaseColors[index],
                    rechargeTint,
                    intensity
                );
            }

            img.rectTransform.localScale =
                _pegBaseScales[index] *
                (1f + intensity * scaleAmount);

            img.color = targetTint;
        }
    }

    private float CalculateWave(
        float timer,
        float duration,
        float pegPosition,
        float width)
    {
        duration = Mathf.Max(0.1f, duration);
        width = Mathf.Max(0.01f, width);

        // Move from 0 to 1, then repeat.
        float wavePosition =
            Mathf.Repeat(timer / duration, 1f);

        // Shortest wrapped distance to wave.
        float distance = Mathf.Abs(
            pegPosition - wavePosition
        );

        distance = Mathf.Min(
            distance,
            1f - distance
        );

        // Convert distance into a smooth pulse.
        float t = Mathf.Clamp01(
            1f - distance / width
        );

        return t * t * (3f - 2f * t);
    }

    private void ResetPegVisuals()
    {
        if (_pegImages == null)
            return;

        for (int i = 0; i < _pegImages.Length; i++)
        {
            if (_pegImages[i] == null)
                continue;

            _pegImages[i].rectTransform.localScale =
                _pegBaseScales[i];

            _pegImages[i].color =
                _pegBaseColors[i];
        }
    }

    private void SyncOverchargeParticle()
    {
        if (OverchargeParticle != null)
        {
            OverchargeParticle.SetActive(
                _forcedBoostActive
            );
        }
    }

    public void SetForcedBoost(bool active)
    {
        _forcedBoostActive = active;
        SyncOverchargeParticle();
    }

    public void SetBoostInput(
        bool boostHeld,
        bool hasMoveInput)
    {
        _boostHeld = boostHeld;
        _hasMoveInput = hasMoveInput;
    }

    public void SetMode(Mode m)
    {
        _mode = m;

        if (_mode != Mode.FreeFlight)
            _boost01 = 0f;
    }

    public void SetCapacity(float value)
    {
        float oldCapacity = capacity;

        capacity = Mathf.Max(0f, value);

        _energy = Mathf.Clamp(
            _energy + capacity - oldCapacity,
            0f,
            capacity
        );

        ApplyPegMats(force: true);
    }

    public float GetCapacity() => capacity;

    public void SetDrainPerSecond(float value)
    {
        drainPerSecond = value;
    }

    public float GetDrainPerSecond() =>
        drainPerSecond;

    public void SetRegenPerSecond(float value)
    {
        regenPerSecond = value;
    }

    public float GetRegenPerSecond() =>
        regenPerSecond;

    public void AddEnergy(float amount)
    {
        _energy = Mathf.Clamp(
            _energy + amount,
            0f,
            capacity
        );

        ApplyPegMats();
    }

    private void UpdateBoostLight()
    {
        if (boostLight == null)
            return;

        // Priority: Overcharge > Drain > Recharge > Idle
        bool overcharging = _forcedBoostActive;
        bool draining = _isDraining && !overcharging;
        bool recharging = _isRecharging && !overcharging && !draining;

        Material targetMaterial = null;
        float brightness = 1f;

        if (overcharging)
        {
            targetMaterial = pegLitMat;

            // Fast, energetic pulse.
            float pulse = Mathf.Sin(
                Time.time * overchargePulseSpeed
            );

            brightness = Mathf.Lerp(
                minimumPulseBrightness,
                1f,
                (pulse + 1f) * 0.5f
            );
        }
        else if (draining)
        {
            targetMaterial = boostDrainMat != null
                ? boostDrainMat
                : pegLitMat;

            // Faster, somewhat irregular flickering.
            float pulse = Mathf.Sin(
                Time.time * drainPulseSpeed
            );

            float flicker = Mathf.Sin(
                Time.time * drainPulseSpeed * 2.7f
            ) * 0.15f;

            float intensity = Mathf.Clamp01(
                (pulse + 1f) * 0.5f + flicker
            );

            brightness = Mathf.Lerp(
                minimumPulseBrightness,
                1f,
                intensity
            );
        }
        else if (recharging)
        {
            targetMaterial = pegLitMat;

            // Slower, gentler breathing animation.
            float pulse = Mathf.Sin(
                Time.time * rechargePulseSpeed
            );

            brightness = Mathf.Lerp(
                minimumPulseBrightness,
                1f,
                (pulse + 1f) * 0.5f
            );
        }

        // Swap only when the material needs changing.
        if (boostLight.material != targetMaterial)
            boostLight.material = targetMaterial;

        // Tint intensity while preserving original color.
        Color targetColor = _boostLightBaseColor;
        targetColor.r *= brightness;
        targetColor.g *= brightness;
        targetColor.b *= brightness;

        boostLight.color = targetColor;
    }

}
