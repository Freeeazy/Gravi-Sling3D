using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpeedometerHUD : MonoBehaviour
{
    public static SpeedometerHUD Instance { get; private set; }

    [Header("Meter Root")]
    public RectTransform meterRoot;

    [SerializeField] private Rigidbody shipRb;

    [Header("Materials")]
    public Material greenOn;
    public Material greenOff;
    public Material redOn;
    public Material redOff;

    [Header("Speed Mapping (m/s)")]
    public float freeFlightMaxSpeed = 200f;          // fills first chunk
    [Range(0f, 1f)] public float freeFlightFillPortion = 0.25f;

    public float redlineSpeed = 800f;               // "full" at this speed
    public float maxSpeed = 1100f;                  // overspeed scaling (flicker)

    [Header("Redline Visual Region")]
    [Range(0f, 1f)] public float redlineStart01 = 0.78f; // last ~22% of columns are red

    [Header("Overspeed Flicker")]
    public float flickerHzMin = 3f;   // at just over redline
    public float flickerHzMax = 18f;  // near max speed
    [Range(0f, 1f)] public float flickerDuty = 0.75f; // higher = looks "more full" while flickering
    [Range(0f, 1f)] public float overspeedFillBias = 0.35f; // makes overspeed look more full

    [Header("Smoothing")]
    public float speedSmooth = 12f;

    // Internal data: columns -> ordered peg renderers
    private readonly List<List<Graphic>> _columns = new();
    private float _currentSpeed;
    private float _externalSpeed;
    private bool _hasExternalSpeed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CacheColumns();
    }

    void OnEnable()
    {
        // In case builder rebuilds at runtime
        if (_columns.Count == 0) CacheColumns();
    }

    void CacheColumns()
    {
        _columns.Clear();
        if (!meterRoot)
        {
            Debug.LogError("SpeedometerHUD: meterRoot not assigned.");
            return;
        }

        // Grab PEG_Column_* children in hierarchy order
        for (int i = 0; i < meterRoot.childCount; i++)
        {
            var col = meterRoot.GetChild(i) as RectTransform;
            if (!col) continue;

            // Collect ONLY active peg children with a Graphic (RawImage or Image).
            var pegs = new List<Graphic>();
            for (int p = 0; p < col.childCount; p++)
            {
                var pegGO = col.GetChild(p).gameObject;
                if (!pegGO.activeInHierarchy) continue;

                var g = pegGO.GetComponent<Graphic>(); // RawImage / Image both inherit Graphic
                if (!g) continue;

                pegs.Add(g);
            }

            // Sort bottom -> top by anchoredPosition.y (works because each peg is a RectTransform)
            pegs.Sort((a, b) =>
            {
                var ra = a.rectTransform.anchoredPosition.y;
                var rb = b.rectTransform.anchoredPosition.y;
                return ra.CompareTo(rb);
            });

            if (pegs.Count > 0)
                _columns.Add(pegs);
        }

        if (_columns.Count == 0)
            Debug.LogWarning("SpeedometerHUD: No active peg Graphics found under meterRoot.");
    }

    void Update()
    {
        float targetSpeed = GetTargetSpeed();
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 1f - Mathf.Exp(-speedSmooth * Time.unscaledDeltaTime));

        float fill01 = SpeedToFill01(_currentSpeed);
        ApplyFill(fill01, _currentSpeed);
    }

    float GetTargetSpeed()
    {
        if (_hasExternalSpeed)
            return _externalSpeed;

        if (shipRb)
            return shipRb.linearVelocity.magnitude; // Unity 6

        return 0f;
    }

    float SpeedToFill01(float speed)
    {
        // Piecewise mapping:
        // 0..freeFlightMaxSpeed -> 0..freeFlightFillPortion
        // freeFlightMaxSpeed..redlineSpeed -> freeFlightFillPortion..1
        if (redlineSpeed <= 0f) return 0f;

        if (speed <= freeFlightMaxSpeed)
        {
            float t = Mathf.InverseLerp(0f, Mathf.Max(0.01f, freeFlightMaxSpeed), speed);
            return Mathf.Lerp(0f, freeFlightFillPortion, t);
        }
        else
        {
            float t = Mathf.InverseLerp(freeFlightMaxSpeed, Mathf.Max(freeFlightMaxSpeed + 0.01f, redlineSpeed), speed);
            return Mathf.Lerp(freeFlightFillPortion, 1f, t);
        }
    }

    void ApplyFill(float baseFill01, float speed)
    {
        if (_columns.Count == 0) return;

        int colCount = _columns.Count;

        // Overspeed effects
        bool overspeed = speed > redlineSpeed;
        float over01 = Mathf.InverseLerp(redlineSpeed, Mathf.Max(redlineSpeed + 0.01f, maxSpeed), speed);

        // Flicker phase: faster as over01 rises
        float hz = Mathf.Lerp(flickerHzMin, flickerHzMax, over01);
        float phase = Mathf.Repeat(Time.unscaledTime * hz, 1f);
        bool flickerOn = phase < flickerDuty;

        // Fill bias: make it look “more full” when overspeeding even if flicker drops some pegs
        float biasedFill01 = baseFill01;
        if (overspeed)
            biasedFill01 = Mathf.Clamp01(Mathf.Max(baseFill01, 1f - (1f - over01) * overspeedFillBias));

        // Determine how many columns are considered filled overall
        // (column-based fill; then peg-based within each column)
        float filledColsF = biasedFill01 * colCount;

        for (int c = 0; c < colCount; c++)
        {
            var pegs = _columns[c];
            int pegCount = pegs.Count;

            // How filled is this column (0..1), based on global fill
            // Example: if filledColsF = 10.5, columns < 10 are full, column 10 is half, rest off
            float colFill01 = Mathf.Clamp01(filledColsF - c);

            int pegsOn = Mathf.RoundToInt(colFill01 * pegCount);

            bool isRedColumn = (c / (float)(colCount - 1)) >= redlineStart01;

            // If overspeed, only the red region flickers (looks like a redline bounce)
            bool applyFlickerHere = overspeed && isRedColumn;

            for (int p = 0; p < pegCount; p++)
            {
                bool shouldBeOn = p < pegsOn;

                if (applyFlickerHere)
                {
                    // In overspeed, keep most red pegs on, but let them flicker
                    shouldBeOn = shouldBeOn && flickerOn;
                }

                SetPegMaterial(pegs[p], isRedColumn, shouldBeOn);
            }
        }
    }

    void SetPegMaterial(Graphic g, bool redRegion, bool on)
    {
        if (!g) return;

        if (redRegion)
            g.material = on ? redOn : redOff;
        else
            g.material = on ? greenOn : greenOff;
    }

    // Call this from orbit code (SlingshotPlanet3D) when orbiting.
    public void SetSpeed(float speed)
    {
        _externalSpeed = Mathf.Max(0f, speed);
        _hasExternalSpeed = true;
    }

    // Call when leaving orbit so we go back to Rigidbody speed.
    public void ClearExternalSpeed()
    {
        _hasExternalSpeed = false;
    }
}
