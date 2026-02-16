using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central boost/energy manager (singleton).
/// - Drains energy while in FreeFlight AND boost is held AND player has movement input.
/// - Does NOT drain or regen while OrbitIdle.
/// - Regens ONLY while OrbitCharging (holding charge in SlingshotPlanet3D).
/// - Exposes Boost01 (0..1) for SimpleMove's additive boost logic.
/// - Drives a UI RectTransform bar by changing its width (sizeDelta.x) 0..maxWidth.
/// </summary>
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

    [Tooltip("Energy drained per second at Boost01 = 1 (FreeFlight).")]
    public float drainPerSecond = 18f;

    public bool dontDrain = false;

    [Tooltip("Energy regenerated per second (ONLY during OrbitCharging).")]
    public float regenPerSecond = 35f;

    [Header("Boost Smoothing (matches old boostCharge feel)")]
    [Tooltip("How fast Boost01 rises when boosting (per second).")]
    public float boostRampUp = 1.2f;

    [Tooltip("How fast Boost01 falls when not boosting (per second).")]
    public float boostRampDown = 2.0f;

    [Header("UI Pegs (Material Swap)")]
    [Tooltip("Parent that contains 10 peg Images as children (can be under a VerticalLayoutGroup).")]
    public Transform pegsParent;

    [Tooltip("Material used for a lit peg.")]
    public Material pegLitMat;

    [Tooltip("Material used for an unlit peg.")]
    public Material pegUnlitMat;

    [Tooltip("If true, peg index order is reversed (useful if your layout fills top->bottom or bottom->top).")]
    public bool reversePegOrder = false;

    [Tooltip("How many pegs to use. Default 10.")]
    [Min(1)] public int pegCount = 10;

    private float _energy;
    private float _boost01;            // smoothed boost intensity 0..1

    // Inputs from SimpleMove each FixedUpdate
    private bool _boostHeld;
    private bool _hasMoveInput;

    // Cached peg Images
    private Image[] _pegImages;
    public float Energy => _energy;
    public float Energy01 => (capacity <= 0.0001f) ? 0f : Mathf.Clamp01(_energy / capacity);

    /// <summary>Use this in SimpleMove instead of its private boostCharge.</summary>
    public float Boost01 => _boost01;

    private void Awake()
    {
        // Singleton
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _energy = startFull ? capacity : Mathf.Clamp(startEnergy, 0f, capacity);

        CachePegImages();
        ApplyPegMats(force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Decide if we *want* boosting this tick (only in free flight, with input)
        bool canBoostState = (_mode == Mode.FreeFlight);
        bool hasEnergy = _energy > 0.0001f;
        bool boostingWanted = canBoostState && _boostHeld && _hasMoveInput && hasEnergy;

        // Smooth Boost01 up/down (so SimpleMove keeps same feel)
        float delta = boostingWanted ? boostRampUp : -boostRampDown;
        _boost01 = Mathf.Clamp01(_boost01 + delta * dt);

        // Drain energy ONLY while boostingWanted (optionally scale by boost01)
        if (boostingWanted && !dontDrain && drainPerSecond > 0f)
        {
            _energy -= drainPerSecond * _boost01 * dt;
            if (_energy <= 0f)
            {
                _energy = 0f;
                _boost01 = 0f; // hard drop if empty
            }
        }

        // Regen ONLY during orbit charging
        if (_mode == Mode.OrbitCharging && regenPerSecond > 0f)
        {
            _energy += regenPerSecond * dt;
            if (_energy > capacity) _energy = capacity;
        }

        // --- Cheat: Ctrl + B = refill energy ---
        if (Input.GetKeyDown(KeyCode.B))
        {
            _energy = capacity;
            _boost01 = 0f; // optional: prevents weird instant boost spike

            Debug.Log("[BoostManager] Cheat refill activated.");
        }

        ApplyPegMats();
    }
    private void CachePegImages()
    {
        if (pegsParent == null) return;

        // Grab Images from children (including inactive, since UI often starts disabled)
        _pegImages = pegsParent.GetComponentsInChildren<Image>(includeInactive: true);

        // If parent itself has an Image, it will be included; we usually only want children.
        // Remove the parent's Image if present.
        var parentImg = pegsParent.GetComponent<Image>();
        if (parentImg != null && _pegImages != null && _pegImages.Length > 0)
        {
            // Filter out the parent's Image
            int count = 0;
            for (int i = 0; i < _pegImages.Length; i++)
                if (_pegImages[i] != parentImg) count++;

            if (count != _pegImages.Length)
            {
                var filtered = new Image[count];
                int idx = 0;
                for (int i = 0; i < _pegImages.Length; i++)
                {
                    if (_pegImages[i] == parentImg) continue;
                    filtered[idx++] = _pegImages[i];
                }
                _pegImages = filtered;
            }
        }
    }
    private void ApplyPegMats(bool force = false)
    {
        if (_pegImages == null || _pegImages.Length == 0 || pegLitMat == null || pegUnlitMat == null)
            return;

        int n = Mathf.Min(pegCount, _pegImages.Length);

        // Hard bucket logic:
        // - 0..(just under 10%) => 0
        // - 10..(just under 20%) => 1
        // - ...
        // - 90..(just under 100%) => 9
        // - exactly 100% => 10
        float e01 = Energy01;

        int lit = Mathf.FloorToInt(e01 * n);
        if (_energy >= capacity - 0.0001f) lit = n;  // allow full only when truly full
        lit = Mathf.Clamp(lit, 0, n);

        for (int i = 0; i < n; i++)
        {
            int pegIndex = reversePegOrder ? (n - 1 - i) : i;
            var img = _pegImages[pegIndex];
            if (img == null) continue;

            Material target = (i < lit) ? pegLitMat : pegUnlitMat;

            // Use material (not sharedMaterial) so we don't accidentally affect prefabs/other UI.
            if (force || img.material != target)
                img.material = target;
        }
    }
    /// <summary>
    /// Called by SimpleMove each FixedUpdate (or Update) to tell BoostManager current input state.
    /// </summary>
    public void SetBoostInput(bool boostHeld, bool hasMoveInput)
    {
        _boostHeld = boostHeld;
        _hasMoveInput = hasMoveInput;
    }

    /// <summary>
    /// Called by SlingshotPlanet3D to tell BoostManager the orbit/charging state.
    /// </summary>
    public void SetMode(Mode m)
    {
        _mode = m;

        // If we left FreeFlight, drop boost intensity so we never "carry" boost into orbit
        if (_mode != Mode.FreeFlight)
            _boost01 = 0f;
    }

    // Optional helper if you want manual refills later
    public void AddEnergy(float amount)
    {
        _energy = Mathf.Clamp(_energy + amount, 0f, capacity);
        ApplyPegMats();
    }
}
