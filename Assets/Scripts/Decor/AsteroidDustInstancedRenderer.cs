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

    // Per-instance mesh id (sticky)
    private int[] _meshId;
    private float[] _viewSpinDeg;
    private int[] _tintId;

    // Reusable per-mesh matrix lists (to avoid allocations)
    private List<int>[] _perMeshIndices;

    // Temp batch buffer
    private static readonly Matrix4x4[] _batchMatrices = new Matrix4x4[1023];
    private static readonly Vector4[] _batchTints = new Vector4[1023];
    private MaterialPropertyBlock _mpb;
    private void Awake()
    {
        if (!posManager) posManager = GetComponent<AsteroidDustPosManager>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        RebuildAssignments();
    }

    private void OnEnable()
    {
        if (posManager != null)
            posManager.OnRegenerated += RebuildAssignments;
        ;
    }

    private void OnDisable()
    {
        if (posManager != null)
            posManager.OnRegenerated -= RebuildAssignments;
    }

    private void LateUpdate()
    {
        if (!posManager || posManager.Positions == null) return;
        if (!IsValid())
        {
            if (disableIfInvalid) return;
        }

        EnsureBuffers();

        // clear buckets
        for (int m = 0; m < _perMeshIndices.Length; m++)
            _perMeshIndices[m].Clear();

        int n = posManager.Positions.Length;

        // resolve view rotation once
        Quaternion viewRot = Quaternion.identity;
        if (alignToView)
        {
            Transform vt = viewTransform;
            if (!vt)
            {
                Camera cam = Camera.main;
                if (cam) vt = cam.transform;
            }
            if (vt) viewRot = vt.rotation;
        }

        // bucket instances by mesh
        for (int i = 0; i < n; i++)
        {
            if (posManager.IsHidden(i)) continue;

            int mid = _meshId[i];
            if ((uint)mid >= (uint)meshes.Count) mid = 0;

            // compute final scale early so we can skip tiny ones
            float d01 = ComputeDistance01(posManager.Positions[i]);
            float band = Mathf.Clamp01(scaleBand.Evaluate(d01));
            float bandScale = Mathf.Lerp(1f, band, Mathf.Clamp01(bandStrength));
            float finalS = posManager.Scales[i] * bandScale;

            if (finalS <= cullScaleThreshold) continue;

            _perMeshIndices[mid].Add(i);
        }

        // draw each mesh bucket in 1023 batches
        for (int m = 0; m < meshes.Count; m++)
        {
            Mesh mesh = meshes[m];
            if (!mesh) continue;

            var list = _perMeshIndices[m];
            int total = list.Count;
            int offset = 0;

            while (offset < total)
            {
                int take = Mathf.Min(1023, total - offset);

                // build matrices + per-instance tints
                for (int k = 0; k < take; k++)
                {
                    int i = list[offset + k];

                    Vector3 pos = posManager.Positions[i];
                    float s = posManager.Scales[i];

                    // apply band scale again (cheap, keeps your previous behavior)
                    float d01 = ComputeDistance01(pos);
                    float band = Mathf.Clamp01(scaleBand.Evaluate(d01));
                    float bandScale = Mathf.Lerp(1f, band, Mathf.Clamp01(bandStrength));
                    float finalS = s * bandScale;

                    Quaternion rot = alignToView ? viewRot : posManager.Rotations[i];
                    rot = NormalizeSafe(rot);

                    if (alignToView && randomSpinAroundViewForward && _viewSpinDeg != null)
                        rot = rot * Quaternion.AngleAxis(_viewSpinDeg[i], Vector3.forward);

                    _batchMatrices[k] = Matrix4x4.TRS(pos, rot, Vector3.one * finalS);

                    if (useTintPalette && tintPalette != null && tintPalette.Count > 0)
                    {
                        int tid = (_tintId != null && _tintId.Length > i) ? _tintId[i] : 0;
                        tid = Mathf.Clamp(tid, 0, tintPalette.Count - 1);
                        Color c = tintPalette[tid];
                        _batchTints[k] = new Vector4(c.r, c.g, c.b, c.a);
                    }
                }

                // MPB: only set vector array when tinting is enabled
                _mpb.Clear();
                if (useTintPalette && tintPalette != null && tintPalette.Count > 0)
                    _mpb.SetVectorArray(tintProperty, _batchTints);

                Graphics.DrawMeshInstanced(
                    mesh,
                    0,
                    material,
                    _batchMatrices,
                    take,
                    _mpb,
                    shadows,
                    receiveShadows,
                    layer,
                    null,
                    LightProbeUsage.Off,
                    null
                );

                offset += take;
            }
        }
    }

    private bool IsValid()
    {
        if (material == null) return false;
        if (meshes == null || meshes.Count == 0) return false;

        // Ensure at least one non-null mesh
        for (int i = 0; i < meshes.Count; i++)
            if (meshes[i] != null) return true;

        return false;
    }

    private void EnsureBuffers()
    {
        int n = posManager.Positions.Length;

        // mesh ids
        if (_meshId == null || _meshId.Length != n)
            RebuildAssignments();

        // per-mesh lists
        if (_perMeshIndices == null || _perMeshIndices.Length != meshes.Count)
        {
            _perMeshIndices = new List<int>[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
                _perMeshIndices[i] = new List<int>(Mathf.Max(64, n / Mathf.Max(1, meshes.Count)));
        }
    }

    private void RebuildAssignments()
    {
        if (!posManager || posManager.Positions == null) return;

        int n = posManager.Positions.Length;

        // mesh id
        _meshId = new int[n];
        int meshCount = Mathf.Max(1, meshes.Count);
        var rng = new System.Random(meshSeed);
        for (int i = 0; i < n; i++)
            _meshId[i] = rng.Next(meshCount);

        // view spin
        _viewSpinDeg = new float[n];
        var srng = new System.Random(spinSeed);
        for (int i = 0; i < n; i++)
            _viewSpinDeg[i] = (float)(srng.NextDouble() * 360.0);

        // tint id (only meaningful if tintPalette has entries)
        _tintId = new int[n];
        var trng = new System.Random(tintSeed);

        int palCount = (tintPalette != null) ? tintPalette.Count : 0;
        if (palCount <= 0)
        {
            for (int i = 0; i < n; i++) _tintId[i] = 0;
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                // optional bias towards palette[0]
                if (tintBiasToFirst > 0f && trng.NextDouble() < tintBiasToFirst)
                {
                    _tintId[i] = 0;
                }
                else
                {
                    _tintId[i] = trng.Next(palCount);
                }
            }
        }

        // buckets
        if (_perMeshIndices == null || _perMeshIndices.Length != meshes.Count)
        {
            _perMeshIndices = new List<int>[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
                _perMeshIndices[i] = new List<int>(Mathf.Max(64, n / Mathf.Max(1, meshes.Count)));
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        float ls = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (!float.IsFinite(ls) || ls < 1e-12f)
            return Quaternion.identity;
        float inv = 1.0f / Mathf.Sqrt(ls);
        return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
    }

    private float ComputeDistance01(Vector3 instancePos)
    {
        Vector3 fromPos = (distanceFrom ? distanceFrom.position :
                          (posManager.player ? posManager.player.position : Vector3.zero));

        float d = Vector3.Distance(fromPos, instancePos);

        float denom = (normalizeByOuterBox && posManager != null)
            ? posManager.outerHalfExtents.magnitude
            : Mathf.Max(0.0001f, maxDistance);

        return Mathf.Clamp01(d / Mathf.Max(0.0001f, denom));
    }
}
