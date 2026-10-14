using UnityEngine;
using UnityEngine.UI;

/// Optional replacement for the peg display only (not the numeric speed label).
/// Attach to a fresh UI RectTransform under a Canvas. No prefab is needed.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Retro Peg Meter")]
public sealed class RetroPegMeter_UI : MaskableGraphic
{
    [Header("Layout")]
    [SerializeField, Range(1, 128)] private int barCount = 48;
    [SerializeField, Range(1, 32)] private int tierRows = 6;
    [SerializeField, Range(0.1f, 0.95f)] private float barWidthRatio = 0.55f;
    [Tooltip("Gap in segment-height units; unaffected by load.")]
    [SerializeField, Range(0f, 1f)] private float tierGap = 0.35f;
    [SerializeField] private bool rightToLeft;

    [Header("Full-load silhouette")]
    [SerializeField]
    private AnimationCurve heightCurve = new AnimationCurve(
        new Keyframe(0f, 0.20f), new Keyframe(0.70f, 1f),
        new Keyframe(0.85f, 0.95f), new Keyframe(1f, 0.75f));
    [SerializeField]
    private AnimationCurve tierScaleCurve =
        AnimationCurve.Linear(0f, 1f, 1f, 0.75f);
    [SerializeField]
    private AnimationCurve tierDensityCurve =
        AnimationCurve.Linear(0f, 1f, 1f, 0.60f);

    [Header("Independent inputs / editor preview")]
    [Tooltip("Engine effort: extends individual pegs vertically.")]
    [SerializeField, Range(0f, 1f)] private float load01 = 0.25f;
    [Tooltip("Drive output or virtual RPM: advances illumination horizontally.")]
    [SerializeField, Range(0f, 1f)] private float output01 = 0.40f;
    [SerializeField, Range(0.05f, 1f)] private float idlePegHeight = 0.25f;

    [Header("Response (seconds to travel approximately 95% of the distance)")]
    [SerializeField, Min(0f)] private float heightAttack = 0.12f;
    [SerializeField, Min(0f)] private float heightRelease = 0.35f;
    [SerializeField, Min(0f)] private float sweepAttack = 0.08f;
    [SerializeField, Min(0f)] private float sweepRelease = 0.20f;
    [SerializeField] private bool useUnscaledTime;
    [Tooltip("Blend only the advancing column; otherwise switch whole columns.")]
    [SerializeField] private bool softSweep = true;

    [Header("Phosphor colors (multiplied by the Graphic Color)")]
    [SerializeField] private Color unlitColor = new Color(0.09f, 0.14f, 0.08f, 1f);
    [SerializeField] private Color litColor = new Color(0.25f, 1f, 0.62f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.48f, 0.08f, 1f);
    [SerializeField, Range(0f, 1f)] private float warningStartsAt = 0.85f;

    private float displayedLoad;
    private float displayedOutput;
    private float warningBrightness = 1f;
    private float warningTipRetraction;

    /// Visual-only warning modulation. Does not alter telemetry or green columns.
    /// tipRetraction01 is a fraction of the complete meter width.
    public void SetWarningEffect(float startsAt01, float brightness01, float tipRetraction01)
    {
        float start = Safe01(startsAt01);
        float brightness = Safe01(brightness01);
        float retraction = Safe01(tipRetraction01);
        if (warningStartsAt == start && warningBrightness == brightness &&
            warningTipRetraction == retraction) return;
        warningStartsAt = start;
        warningBrightness = brightness;
        warningTipRetraction = retraction;
        SetVerticesDirty();
    }

    /// Both inputs are normalized 0..1. This never creates or destroys objects.
    public void SetTelemetry(float engineLoad01, float driveOutput01)
    {
        load01 = Safe01(engineLoad01);
        output01 = Safe01(driveOutput01);
        if (!Application.isPlaying) SetVerticesDirty();
    }

    public void SnapToTelemetry()
    {
        displayedLoad = Safe01(load01);
        displayedOutput = Safe01(output01);
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        SnapToTelemetry();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float nextLoad = Follow(displayedLoad, Safe01(load01),
            heightAttack, heightRelease, dt);
        float nextOutput = Follow(displayedOutput, Safe01(output01),
            sweepAttack, sweepRelease, dt);
        if (nextLoad == displayedLoad && nextOutput == displayedOutput) return;
        displayedLoad = nextLoad;
        displayedOutput = nextOutput;
        SetVerticesDirty();
    }

    private static float Follow(float value, float target,
        float attack, float release, float dt)
    {
        if (dt <= 0f) return value;
        float duration = target > value ? attack : release;
        if (duration <= 0f) return target;
        float result = Mathf.Lerp(value, target, 1f - Mathf.Exp(-3f * dt / duration));
        return Mathf.Abs(result - target) < 0.0001f ? target : result;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = GetPixelAdjustedRect();
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        int columns = Mathf.Clamp(barCount, 1, 128);
        int rows = Mathf.Clamp(tierRows, 1, 32);
        float load = Application.isPlaying ? displayedLoad : Safe01(load01);
        float output = Application.isPlaying ? displayedOutput : Safe01(output01);
        float extension = Mathf.Lerp(Mathf.Clamp(idlePegHeight, 0.05f, 1f), 1f, load);

        // Normalize the full stack to fit inside the RectTransform, including gaps.
        float stackUnits = 0f;
        for (int row = 0; row < rows; row++)
        {
            float t = rows == 1 ? 0f : row / (float)(rows - 1);
            stackUnits += Evaluate(tierScaleCurve, t, 0.05f, 5f);
            if (row < rows - 1)
                stackUnits += Mathf.Clamp01(tierGap) *
                    Evaluate(tierDensityCurve, t, 0f, 5f);
        }

        float unit = bounds.height / stackUnits;
        float slot = bounds.width / columns;
        float width = slot * Mathf.Clamp(barWidthRatio, 0.1f, 0.95f);

        for (int column = 0; column < columns; column++)
        {
            // Logical progression stays the same; only its physical position flips.
            float tBar = columns == 1 ? 0f : column / (float)(columns - 1);
            int physicalColumn = rightToLeft ? columns - 1 - column : column;
            float x = bounds.xMin + (physicalColumn + 0.5f) * slot;
            float tierFill = Evaluate(heightCurve, tBar, 0f, 1f) * rows;
            bool isWarning = tBar >= warningStartsAt;
            float visualOutput = isWarning ? Mathf.Max(0f, output - warningTipRetraction) : output;
            float front = visualOutput * columns - column;
            float illumination = softSweep ? Mathf.Clamp01(front) : (front >= 1f ? 1f : 0f);
            if (isWarning) illumination *= warningBrightness;
            Color active = isWarning ? warningColor : litColor;
            Color32 tint = Color.Lerp(unlitColor, active, illumination) * color;
            float y = bounds.yMin;

            for (int row = 0; row < rows; row++)
            {
                float fill = Mathf.Clamp01(tierFill - row);
                if (fill <= 0f) break;
                float tTier = rows == 1 ? 0f : row / (float)(rows - 1);
                float height = unit * Evaluate(tierScaleCurve, tTier, 0.05f, 5f)
                    * fill * extension;
                AddQuad(mesh, x - width * 0.5f, y, width, height, tint);
                y += height + unit * Mathf.Clamp01(tierGap) *
                    Evaluate(tierDensityCurve, tTier, 0f, 5f);
            }
        }
    }

    private static void AddQuad(VertexHelper mesh, float x, float y,
        float width, float height, Color32 tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x, y, 0f), tint, new Vector2(0f, 0f));
        mesh.AddVert(new Vector3(x, y + height, 0f), tint, new Vector2(0f, 1f));
        mesh.AddVert(new Vector3(x + width, y + height, 0f), tint, new Vector2(1f, 1f));
        mesh.AddVert(new Vector3(x + width, y, 0f), tint, new Vector2(1f, 0f));
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }

    private static float Evaluate(AnimationCurve curve, float t, float min, float max)
    {
        float value = curve == null || curve.length == 0 ? 1f : curve.Evaluate(t);
        if (float.IsNaN(value) || float.IsInfinity(value)) value = 1f;
        return Mathf.Clamp(value, min, max);
    }

    private static float Safe01(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        barCount = Mathf.Clamp(barCount, 1, 128);
        tierRows = Mathf.Clamp(tierRows, 1, 32);
        load01 = Safe01(load01);
        output01 = Safe01(output01);
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(RetroPegMeter_UI))]
[UnityEditor.CanEditMultipleObjects]
public sealed class RetroPegMeter_UI_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UnityEditor.EditorGUILayout.HelpBox(
            "Size the RectTransform to the tach area. Load changes peg height; " +
            "Output changes the lit sweep. Edit-mode previews update immediately; " +
            "Play mode adds smoothing. Leave Material empty and Color white to start.",
            UnityEditor.MessageType.Info);
    }
}
#endif
