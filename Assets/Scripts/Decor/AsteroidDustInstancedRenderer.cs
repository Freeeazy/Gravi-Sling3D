using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Minimal renderer for AsteroidDustPosManager using GPU instancing.
/// - Randomly assigns each instance a mesh from a list (sticky assignment).
/// - Draws in 1023-sized batches via Graphics.DrawMeshInstanced.
/// - Can skip draw for a few frames after wrap using posManager.IsHidden(i).
/// </summary>
public class AsteroidDustInstancedRenderer : MonoBehaviour
{
    [Header("Refs")]
    public AsteroidDustPosManager posManager;

    [Header("Rendering")]
    public Material material;
    public List<Mesh> meshes = new List<Mesh>();

    [Tooltip("If set, overrides posManager.player for render bounds.")]
    public Transform boundsCenterOverride;

    [Tooltip("Layer used for instanced rendering.")]
    public int layer = 0;

    [Tooltip("Shadow settings (dust usually off).")]
    public ShadowCastingMode shadows = ShadowCastingMode.Off;
    public bool receiveShadows = false;

    [Header("Bounds / Culling")]
    [Tooltip("Big bounds to avoid Unity culling your instances. Should cover your outer box extents.")]
    public float boundsPadding = 50f;

    [Tooltip("If true, only renders when mesh+material are valid.")]
    public bool disableIfInvalid = true;

    [Header("Distance Band Scaling")]
    public Transform distanceFrom; // usually camera or player
    [Tooltip("If true, normalize distance using posManager.outerHalfExtents magnitude.")]
    public bool normalizeByOuterBox = true;

    [Tooltip("Manual max distance if not normalizing by outer box.")]
    public float maxDistance = 600f;

    [Tooltip("Band curve: x = 0 near, x = 1 far. y = scale multiplier (0..1).")]
    public AnimationCurve scaleBand = AnimationCurve.Linear(0, 0, 0.5f, 1);

    [Range(0f, 2f)] public float bandStrength = 1f; // 0 disables, 1 full
    [Tooltip("Clamp very small scales to 0 to effectively hide.")]
    public float cullScaleThreshold = 0.02f;

    [Header("Random Assignment")]
    public int meshSeed = 1337;

    [Header("View Alignment (ParticleSystem-style)")]
    [Tooltip("If true, instances use the view/camera rotation (like ParticleSystem Render Alignment = View).")]
    public bool alignToView = false;

    [Tooltip("Camera/transform to align to. If null, uses Camera.main.")]
    public Transform viewTransform;

    [Tooltip("If true, each instance gets a stable random spin around the view forward axis.")]
    public bool randomSpinAroundViewForward = true;

    [Tooltip("Seed for stable view-spin randomization.")]
    public int spinSeed = 9001;

    [Header("Per-Instance Tint Palette")]
    public bool useTintPalette = false;

    [Tooltip("Per-instance ShaderGraph color property reference name.")]
    public string tintProperty = "_Tint";

    [Tooltip("Palette entries (2-3 is fine). If empty, tint is disabled.")]
    public List<Color> tintPalette = new List<Color>()
    {
        new Color(0.60f, 0.75f, 1.00f, 1f), // cool blue
        new Color(0.50f, 0.80f, 0.95f, 1f), // teal-ish
        new Color(0.70f, 0.70f, 0.90f, 1f), // lavender-grey
    };

    [Tooltip("Seed for stable per-instance tint assignment.")]
    public int tintSeed = 4242;

    [Range(0f, 1f)]
    [Tooltip("Chance of using palette[0]. Remaining probability is spread across other entries.")]
    public float tintBiasToFirst = 0.0f; // keep 0 unless you want “mostly blue”

    // Stable assignments: rebuilt only when generation/assignment settings change.
    private List<int>[] _perMeshIndices;
    private Quaternion[] _viewSpinRotations;
    private int[] _tintId;
    private int _assignedCount = -1;
    private int _assignedMeshCount = -1;
    private int _assignedMeshSeed;
    private int _assignedSpinSeed;
    private int _assignedTintSeed;
    private int _assignedPaletteCount;
    private float _assignedTintBias;

    // Temp batch buffer
    private static readonly Matrix4x4[] _batchMatrices = new Matrix4x4[1023];
    private static readonly Vector4[] _batchTints = new Vector4[1023];
    private MaterialPropertyBlock _mpb;
    private string _cachedTintProperty;
    private int _tintPropertyId;
    private AsteroidDustPosManager _registeredManager;

    private void Awake()
    {
        if (!posManager) posManager = GetComponent<AsteroidDustPosManager>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        SyncManager();
        // Regeneration may have happened while this renderer was disabled.
        _assignedCount = -1;
    }

    private void OnDisable()
    {
        DetachManager();
    }

    private void SyncManager()
    {
        if (_registeredManager == posManager) return;
        DetachManager();
        _registeredManager = posManager;
        if (_registeredManager)
        {
            _registeredManager.OnRegenerated += RebuildAssignments;
            _registeredManager.RegisterRenderer(this);
        }
        _assignedCount = -1;
    }

    private void DetachManager()
    {
        if (_registeredManager)
        {
            _registeredManager.OnRegenerated -= RebuildAssignments;
            _registeredManager.UnregisterRenderer(this);
        }
        _registeredManager = null;
    }

    private void LateUpdate()
    {
        SyncManager();
        if (!posManager || posManager.Positions == null || !IsValid()) return;
        EnsureAssignments();

        Vector3[] positions = posManager.Positions;
        Quaternion[] rotations = posManager.Rotations;
        float[] scales = posManager.Scales;
        bool viewAligned = alignToView;
        bool applySpin = viewAligned && randomSpinAroundViewForward;
        bool tintEnabled = useTintPalette && tintPalette != null && tintPalette.Count > 0;

        Quaternion viewRot = Quaternion.identity;
        if (viewAligned)
        {
            Transform vt = viewTransform;
            if (!vt)
            {
                Camera cam = Camera.main;
                if (cam) vt = cam.transform;
            }
            if (vt) viewRot = NormalizeSafe(vt.rotation);
        }

        // Resolve values shared by every instance once per frame.
        float strength = Mathf.Clamp01(bandStrength);
        bool applyBand = strength > 0f && scaleBand != null;
        Vector3 distanceOrigin = Vector3.zero;
        float inverseMaxDistance = 1f;
        if (applyBand)
        {
            Transform origin = distanceFrom ? distanceFrom : posManager.player;
            if (origin) distanceOrigin = origin.position;
            float denominator = normalizeByOuterBox ? posManager.outerHalfExtents.magnitude : maxDistance;
            inverseMaxDistance = 1f / Mathf.Max(0.0001f, denominator);
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        // Also removes a previous frame's tint if tinting has been disabled.
        _mpb.Clear();
        if (tintEnabled && _cachedTintProperty != tintProperty)
        {
            _cachedTintProperty = tintProperty;
            _tintPropertyId = Shader.PropertyToID(tintProperty);
        }

        for (int m = 0; m < _perMeshIndices.Length; m++)
        {
            Mesh mesh = meshes[m];
            if (!mesh) continue;

            List<int> indices = _perMeshIndices[m];
            int batchCount = 0;
            for (int j = 0; j < indices.Count; j++)
            {
                int i = indices[j];
                if (posManager.IsHidden(i)) continue;

                Vector3 pos = positions[i];
                float finalScale = scales[i];
                if (applyBand)
                {
                    float distance01 = Mathf.Clamp01((pos - distanceOrigin).magnitude * inverseMaxDistance);
                    float band = Mathf.Clamp01(scaleBand.Evaluate(distance01));
                    finalScale *= Mathf.Lerp(1f, band, strength);
                }
                if (finalScale <= cullScaleThreshold) continue;

                Quaternion rotation = viewAligned ? viewRot : NormalizeSafe(rotations[i]);
                if (applySpin)
                    rotation *= _viewSpinRotations[i];

                _batchMatrices[batchCount] = Matrix4x4.TRS(pos, rotation, Vector3.one * finalScale);
                if (tintEnabled)
                {
                    Color color = tintPalette[_tintId[i]];
                    _batchTints[batchCount] = new Vector4(color.r, color.g, color.b, color.a);
                }

                batchCount++;
                if (batchCount == _batchMatrices.Length)
                {
                    DrawBatch(mesh, batchCount, tintEnabled);
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
                DrawBatch(mesh, batchCount, tintEnabled);
        }
    }
    private void DrawBatch(Mesh mesh, int count, bool tintEnabled)
    {
        if (tintEnabled)
            _mpb.SetVectorArray(_tintPropertyId, _batchTints);

        Graphics.DrawMeshInstanced(
            mesh, 0, material, _batchMatrices, count, _mpb,
            shadows, receiveShadows, layer, null, LightProbeUsage.Off, null);
    }

    private bool IsValid()
    {
        if (!material || meshes == null || meshes.Count == 0) return false;
        for (int i = 0; i < meshes.Count; i++)
            if (meshes[i]) return true;
        return false;
    }

    private void EnsureAssignments()
    {
        int paletteCount = tintPalette != null ? tintPalette.Count : 0;
        if (_perMeshIndices == null || _assignedCount != posManager.Positions.Length ||
            _assignedMeshCount != meshes.Count || _assignedMeshSeed != meshSeed ||
            _assignedSpinSeed != spinSeed || _assignedTintSeed != tintSeed ||
            _assignedPaletteCount != paletteCount || _assignedTintBias != tintBiasToFirst)
        {
            RebuildAssignments();
        }
    }

    private void RebuildAssignments()
    {
        if (!posManager || posManager.Positions == null) return;

        int n = posManager.Positions.Length;
        int meshCount = meshes != null ? meshes.Count : 0;
        int paletteCount = tintPalette != null ? tintPalette.Count : 0;

        _perMeshIndices = new List<int>[meshCount];
        for (int m = 0; m < meshCount; m++)
            _perMeshIndices[m] = new List<int>(Mathf.Max(64, n / Mathf.Max(1, meshCount)));

        // Match the original RNG calls/order to preserve seeded assignments.
        var meshRng = new System.Random(meshSeed);
        for (int i = 0; i < n; i++)
        {
            int meshId = meshRng.Next(Mathf.Max(1, meshCount));
            if (meshCount > 0) _perMeshIndices[meshId].Add(i);
        }

        // view spin
        _viewSpinRotations = new Quaternion[n];
        var spinRng = new System.Random(spinSeed);
        for (int i = 0; i < n; i++)
        {
            float angle = (float)(spinRng.NextDouble() * 360.0);
            _viewSpinRotations[i] = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        _tintId = new int[n];
        var tintRng = new System.Random(tintSeed);
        if (paletteCount > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (tintBiasToFirst > 0f && tintRng.NextDouble() < tintBiasToFirst)
                    _tintId[i] = 0;
                else
                    _tintId[i] = tintRng.Next(paletteCount);
            }
        }

        _assignedCount = n;
        _assignedMeshCount = meshCount;
        _assignedMeshSeed = meshSeed;
        _assignedSpinSeed = spinSeed;
        _assignedTintSeed = tintSeed;
        _assignedPaletteCount = paletteCount;
        _assignedTintBias = tintBiasToFirst;
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        float lengthSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-12f)
            return Quaternion.identity;
        float inverseLength = 1f / Mathf.Sqrt(lengthSquared);
        return new Quaternion(q.x * inverseLength, q.y * inverseLength,
            q.z * inverseLength, q.w * inverseLength);
    }
}
