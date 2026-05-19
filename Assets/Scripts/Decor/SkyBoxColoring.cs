using UnityEngine;

public class SkyBoxColoring : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Tooltip("Skybox material using the ProceduralSpace shader.")]
    public Material skyboxMaterial;

    [Header("Sampling")]
    [Tooltip("Bigger value = skybox changes over larger distances.")]
    [Min(1f)] public float regionSize = 8000f;

    [Tooltip("Extra seed for procedural skybox region variation.")]
    public int noiseSeed = 777;

    [Tooltip("How quickly material values interpolate to the target.")]
    [Min(0.01f)] public float transitionSpeed = 0.5f;

    [Header("Shader Seed Scroll")]
    public bool animateSeed = true;

    [Tooltip("Base seed value for the shader.")]
    public float baseShaderSeed = 900f;

    [Tooltip("Very small value. This should barely move.")]
    public float seedScrollAmount = 0.025f;

    [Header("Dust")]
    [Range(0f, 1f)] public float minDustAmount = 0.04f;
    [Range(0f, 1f)] public float maxDustAmount = 0.18f;

    [Header("Nebula Strengths")]
    [Range(0f, 1f)] public float minNebula1Strength = 0.05f;
    [Range(0f, 1f)] public float maxNebula1Strength = 0.42f;

    [Range(0f, 1f)] public float minNebula2Strength = 0.00f;
    [Range(0f, 1f)] public float maxNebula2Strength = 0.22f;

    [Header("Star Colors")]
    public Gradient starColor1Gradient;
    public Gradient starColor2Gradient;

    [Header("Nebula 1 Colors")]
    public Gradient nebula1MainGradient;
    public Gradient nebula1MidGradient;

    [Header("Nebula 2 Colors")]
    public Gradient nebula2Color1Gradient;
    public Gradient nebula2Color2Gradient;

    [Header("Asteroid Base Repainting")]
    public bool repaintAsteroidBaseColors = true;

    [Range(0f, 1f)]
    [Tooltip("0 = keep original asteroid base color, 1 = fully repaint asteroid base color.")]
    public float asteroidBaseRepaintStrength = 1f;

    [Tooltip("Gradient used for asteroid base colors on materials 0, 2, and 4.")]
    public Gradient asteroidBaseGradientA;

    [Tooltip("Gradient used for asteroid base colors on materials 1 and 3.")]
    public Gradient asteroidBaseGradientB;

    [Range(0.05f, 0.4f)]
    [Tooltip("How far apart the 5 asteroid material color samples are on the gradient. Higher = less sibling-like colors.")]
    public float asteroidBaseColorSpacing = 0.19f;

    [Range(0f, 2f)]
    [Tooltip("Brightness multiplier for asteroid base repaint colors.")]
    public float asteroidBaseColorIntensity = 0.85f;

    [Header("Asteroid Material Tinting")]
    public bool tintAsteroidMaterials = true;

    [Tooltip("The 5 shared asteroid materials used by the instanced renderer.")]
    public Material[] asteroidMaterials;

    [Tooltip("Create runtime copies of asteroid materials so project assets are not overwritten.")]
    public bool instanceAsteroidMaterials = true;

    [Range(0f, 1f)]
    [Tooltip("How strongly the skybox color influences asteroid base color.")]
    public float asteroidBaseTintStrength = 0.15f;

    [Range(0f, 3f)]
    [Tooltip("Brightness multiplier for the rim lighting color.")]
    public float asteroidRimColorIntensity = 1.25f;

    [Range(0f, 1f)]
    [Tooltip("How strongly the environment color affects rim color.")]
    public float asteroidRimTintStrength = 0.85f;

    [Tooltip("Optional fallback if material does not already have a base color.")]
    public Color fallbackAsteroidBaseColor = Color.gray;

    [Tooltip("Renderer whose 15 type materials should be replaced with the 5 runtime asteroid materials.")]
    public AsteroidFieldInstancedRenderer asteroidRenderer;

    [Tooltip("How many renderer type entries share each asteroid material. For 15 types / 5 materials, use 3.")]
    [Min(1)] public int rendererTypesPerMaterial = 3;

    [Header("Particle Trail Coloring")]
    public ParticleSystem trailParticleSystem;

    [Range(0f, 1f)]
    public float trailNebula2Mix = 0.35f;

    [Range(0f, 1f)]
    public float trailWhiteBlend = 0.75f;

    [Range(0f, 3f)]
    public float trailColorIntensity = 1.25f;

    public bool useRandomBetweenTwoTrailGradients = true;

    private static readonly int SeedID = Shader.PropertyToID("_Seed");
    private static readonly int StarColor1ID = Shader.PropertyToID("_StarColor1");
    private static readonly int StarColor2ID = Shader.PropertyToID("_StarColor2");
    private static readonly int DustAmountID = Shader.PropertyToID("_DustAmount");
    private static readonly int Nebula1StrengthID = Shader.PropertyToID("_Nebular1Strength");
    private static readonly int Nebula1ColorMainID = Shader.PropertyToID("_Nebular1ColorMain");
    private static readonly int Nebula1ColorMidID = Shader.PropertyToID("_Nebular1ColorMid");
    private static readonly int Nebula2StrengthID = Shader.PropertyToID("_Nebular2Strength");
    private static readonly int Nebula2Color1ID = Shader.PropertyToID("_Nebular2Color1");
    private static readonly int Nebula2Color2ID = Shader.PropertyToID("_Nebular2Color2");
    private static readonly int AsteroidBaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int AsteroidRimNearColorID = Shader.PropertyToID("_RimNearColor");

    private Material _runtimeSkyboxMaterial;
    private Material[] _runtimeAsteroidMaterials;
    private Color[] _originalAsteroidBaseColors;
    private Color[] _originalAsteroidRimColors;

    private void Reset()
    {
        player = Camera.main ? Camera.main.transform : null;
        skyboxMaterial = RenderSettings.skybox;

        SetupDefaultGradients();
    }
    private void OnDestroy()
    {
        if (_runtimeSkyboxMaterial != null)
        {
            Destroy(_runtimeSkyboxMaterial);
            _runtimeSkyboxMaterial = null;
        }

        if (_runtimeAsteroidMaterials != null)
        {
            for (int i = 0; i < _runtimeAsteroidMaterials.Length; i++)
            {
                if (_runtimeAsteroidMaterials[i] != null)
                    Destroy(_runtimeAsteroidMaterials[i]);
            }

            _runtimeAsteroidMaterials = null;
        }
    }

    private void Awake()
    {
        if (!player)
            player = Camera.main ? Camera.main.transform : transform;

        if (!skyboxMaterial)
            skyboxMaterial = RenderSettings.skybox;

        if (skyboxMaterial)
        {
            _runtimeSkyboxMaterial = new Material(skyboxMaterial);
            _runtimeSkyboxMaterial.name = skyboxMaterial.name + " (Runtime Instance)";

            RenderSettings.skybox = _runtimeSkyboxMaterial;
            skyboxMaterial = _runtimeSkyboxMaterial;
        }

        EnsureGradientsExist();
        SetupAsteroidMaterialInstances();

        ApplyColorState(1f); // force starting values immediately
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        ApplyColorState(t);
    }

    private void EnsureGradientsExist()
    {
        if (starColor1Gradient == null || starColor1Gradient.colorKeys.Length == 0 ||
            asteroidBaseGradientA == null || asteroidBaseGradientA.colorKeys.Length == 0 ||
            asteroidBaseGradientB == null || asteroidBaseGradientB.colorKeys.Length == 0)
        {
            SetupDefaultGradients();
        }
    }

    [ContextMenu("Setup Default Gradients")]
    private void SetupDefaultGradients()
    {
        starColor1Gradient = MakeGradient(
            new Color(0.75f, 0.95f, 1f),
            new Color(1f, 0.88f, 0.65f),
            new Color(0.95f, 0.75f, 1f)
        );

        starColor2Gradient = MakeGradient(
            new Color(0.45f, 0.85f, 1f),
            new Color(0.9f, 1f, 0.75f),
            new Color(1f, 0.72f, 0.55f)
        );

        nebula1MainGradient = MakeGradient(
            new Color(0.02f, 0.08f, 0.35f),
            new Color(0.25f, 0.02f, 0.35f),
            new Color(0.35f, 0.08f, 0.02f)
        );

        nebula1MidGradient = MakeGradient(
            new Color(0.15f, 0.75f, 1f),
            new Color(0.85f, 0.45f, 1f),
            new Color(0.95f, 0.8f, 0.35f)
        );

        nebula2Color1Gradient = MakeGradient(
            new Color(0.0f, 0.2f, 0.8f),
            new Color(0.1f, 0.7f, 0.9f),
            new Color(0.6f, 0.1f, 0.9f)
        );

        nebula2Color2Gradient = MakeGradient(
            new Color(0.0f, 0.75f, 1f),
            new Color(0.9f, 0.25f, 1f),
            new Color(1f, 0.45f, 0.1f)
        );

        asteroidBaseGradientA = MakeGradient(
            new Color(0.18f, 0.28f, 0.38f),   // blue gray
            new Color(0.38f, 0.22f, 0.42f),   // muted purple
            new Color(0.42f, 0.30f, 0.18f)    // dusty bronze
        );

        asteroidBaseGradientB = MakeGradient(
            new Color(0.16f, 0.36f, 0.32f),   // muted teal
            new Color(0.34f, 0.32f, 0.42f),   // cool slate purple
            new Color(0.46f, 0.24f, 0.20f)    // rusty red-brown
        );
    }

    private Gradient MakeGradient(Color a, Color b, Color c)
    {
        Gradient g = new Gradient();

        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(a, 0f),
                new GradientColorKey(b, 0.5f),
                new GradientColorKey(c, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return g;
    }

    private static float SmoothValueNoise3D(Vector3 p, int seed)
    {
        int x0 = Mathf.FloorToInt(p.x);
        int y0 = Mathf.FloorToInt(p.y);
        int z0 = Mathf.FloorToInt(p.z);

        int x1 = x0 + 1;
        int y1 = y0 + 1;
        int z1 = z0 + 1;

        float tx = SmoothStep01(p.x - x0);
        float ty = SmoothStep01(p.y - y0);
        float tz = SmoothStep01(p.z - z0);

        float c000 = Hash01(x0, y0, z0, seed);
        float c100 = Hash01(x1, y0, z0, seed);
        float c010 = Hash01(x0, y1, z0, seed);
        float c110 = Hash01(x1, y1, z0, seed);

        float c001 = Hash01(x0, y0, z1, seed);
        float c101 = Hash01(x1, y0, z1, seed);
        float c011 = Hash01(x0, y1, z1, seed);
        float c111 = Hash01(x1, y1, z1, seed);

        float x00 = Mathf.Lerp(c000, c100, tx);
        float x10 = Mathf.Lerp(c010, c110, tx);
        float x01 = Mathf.Lerp(c001, c101, tx);
        float x11 = Mathf.Lerp(c011, c111, tx);

        float y0v = Mathf.Lerp(x00, x10, ty);
        float y1v = Mathf.Lerp(x01, x11, ty);

        return Mathf.Lerp(y0v, y1v, tz);
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Hash01(int x, int y, int z, int seed)
    {
        unchecked
        {
            int h = seed;
            h = h * 374761393 + x * 668265263;
            h = h * 1274126177 + y * 1442695041;
            h = h * 326648991 + z * 1597334677;

            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;

            uint u = (uint)h;
            return u / (float)uint.MaxValue;
        }
    }
    private void SetupAsteroidMaterialInstances()
    {
        if (!tintAsteroidMaterials || asteroidMaterials == null || asteroidMaterials.Length == 0)
            return;

        _originalAsteroidBaseColors = new Color[asteroidMaterials.Length];
        _originalAsteroidRimColors = new Color[asteroidMaterials.Length];

        if (instanceAsteroidMaterials)
            _runtimeAsteroidMaterials = new Material[asteroidMaterials.Length];

        for (int i = 0; i < asteroidMaterials.Length; i++)
        {
            Material mat = asteroidMaterials[i];
            if (!mat)
                continue;

            if (instanceAsteroidMaterials)
            {
                Material runtimeMat = new Material(mat);
                runtimeMat.name = mat.name + " (Runtime Instance)";

                _runtimeAsteroidMaterials[i] = runtimeMat;
                asteroidMaterials[i] = runtimeMat;
                mat = runtimeMat;
            }

            _originalAsteroidBaseColors[i] = mat.HasProperty(AsteroidBaseColorID)
                ? mat.GetColor(AsteroidBaseColorID)
                : fallbackAsteroidBaseColor;

            _originalAsteroidRimColors[i] = mat.HasProperty(AsteroidRimNearColorID)
                ? mat.GetColor(AsteroidRimNearColorID)
                : Color.white;
        }

        ApplyAsteroidMaterialsToRendererByIndex();
    }
    private void ApplyAsteroidMaterialsToRendererByIndex()
    {
        if (!asteroidRenderer || asteroidRenderer.typeRenders == null)
            return;

        if (asteroidMaterials == null || asteroidMaterials.Length == 0)
            return;

        int groupSize = Mathf.Max(1, rendererTypesPerMaterial);

        for (int typeIndex = 0; typeIndex < asteroidRenderer.typeRenders.Length; typeIndex++)
        {
            var tr = asteroidRenderer.typeRenders[typeIndex];
            if (tr == null)
                continue;

            int matIndex = typeIndex / groupSize;

            if (matIndex < 0 || matIndex >= asteroidMaterials.Length)
                continue;

            Material mat = asteroidMaterials[matIndex];
            if (!mat)
                continue;

            tr.material = mat;
        }
    }

    private void UpdateAsteroidMaterials(Color environmentColor, Vector3 samplePos, float t)
    {
        if (asteroidMaterials == null || asteroidMaterials.Length == 0)
            return;

        Color rimTarget = environmentColor * asteroidRimColorIntensity;
        rimTarget.a = 1f;

        float asteroidNoiseA = SmoothValueNoise3D(
            samplePos + new Vector3(101.7f, 12.4f, 67.9f),
            noiseSeed + 811
        );

        float asteroidNoiseB = SmoothValueNoise3D(
            samplePos + new Vector3(44.2f, 93.6f, 18.5f),
            noiseSeed + 1249
        );

        for (int i = 0; i < asteroidMaterials.Length; i++)
        {
            Material mat = asteroidMaterials[i];
            if (!mat)
                continue;

            Color originalBase = GetOriginalBaseColor(i);
            Color originalRim = GetOriginalRimColor(i);

            Color targetBase;

            if (repaintAsteroidBaseColors)
            {
                float baseNoise = i % 2 == 0 ? asteroidNoiseA : asteroidNoiseB;

                // Offsets each asteroid material along the gradient so the 5 colors are related,
                // but not nearly identical.
                float colorSample = Repeat01(baseNoise + i * asteroidBaseColorSpacing);

                Gradient selectedGradient = i % 2 == 0 ? asteroidBaseGradientA : asteroidBaseGradientB;

                targetBase = selectedGradient.Evaluate(colorSample) * asteroidBaseColorIntensity;
                targetBase.a = originalBase.a;

                // Allows you to blend between original asteroid color and full repaint.
                targetBase = Color.Lerp(originalBase, targetBase, asteroidBaseRepaintStrength);
                targetBase.a = originalBase.a;
            }
            else
            {
                targetBase = Color.Lerp(originalBase, environmentColor, asteroidBaseTintStrength);
                targetBase.a = originalBase.a;
            }

            Color targetRim = Color.Lerp(originalRim, rimTarget, asteroidRimTintStrength);
            targetRim.a = originalRim.a;

            if (mat.HasProperty(AsteroidBaseColorID))
                mat.SetColor(AsteroidBaseColorID, Color.Lerp(mat.GetColor(AsteroidBaseColorID), targetBase, t));

            if (mat.HasProperty(AsteroidRimNearColorID))
                mat.SetColor(AsteroidRimNearColorID, Color.Lerp(mat.GetColor(AsteroidRimNearColorID), targetRim, t));
        }
    }

    private Color GetOriginalBaseColor(int index)
    {
        if (_originalAsteroidBaseColors == null || index < 0 || index >= _originalAsteroidBaseColors.Length)
            return fallbackAsteroidBaseColor;

        return _originalAsteroidBaseColors[index];
    }

    private Color GetOriginalRimColor(int index)
    {
        if (_originalAsteroidRimColors == null || index < 0 || index >= _originalAsteroidRimColors.Length)
            return Color.white;

        return _originalAsteroidRimColors[index];
    }
    private static float Repeat01(float value)
    {
        return value - Mathf.Floor(value);
    }
    private void ApplyColorState(float t)
    {
        if (!player || !skyboxMaterial)
            return;

        Vector3 samplePos = player.position / Mathf.Max(1f, regionSize);

        float colorNoiseA = SmoothValueNoise3D(samplePos, noiseSeed);
        float colorNoiseB = SmoothValueNoise3D(samplePos + new Vector3(31.7f, 11.2f, 5.9f), noiseSeed + 91);
        float strengthNoise = SmoothValueNoise3D(samplePos + new Vector3(7.1f, 43.3f, 19.4f), noiseSeed + 217);
        float dustNoise = SmoothValueNoise3D(samplePos + new Vector3(63.0f, 2.5f, 28.8f), noiseSeed + 503);

        Color targetStar1 = starColor1Gradient.Evaluate(colorNoiseA);
        Color targetStar2 = starColor2Gradient.Evaluate(colorNoiseB);

        Color targetNeb1Main = nebula1MainGradient.Evaluate(colorNoiseA);
        Color targetNeb1Mid = nebula1MidGradient.Evaluate(colorNoiseB);

        Color targetNeb2Color1 = nebula2Color1Gradient.Evaluate(colorNoiseB);
        Color targetNeb2Color2 = nebula2Color2Gradient.Evaluate(colorNoiseA);

        UpdateParticleTrailColors(targetNeb1Main, targetNeb1Mid, targetNeb2Color1, targetNeb2Color2);

        Color asteroidEnvironmentColor = Color.Lerp(targetNeb1Main, targetNeb1Mid, 0.5f);

        float targetDust = Mathf.Lerp(minDustAmount, maxDustAmount, dustNoise);
        float targetNeb1Strength = Mathf.Lerp(minNebula1Strength, maxNebula1Strength, strengthNoise);
        float targetNeb2Strength = Mathf.Lerp(minNebula2Strength, maxNebula2Strength, colorNoiseB);

        skyboxMaterial.SetColor(StarColor1ID, Color.Lerp(skyboxMaterial.GetColor(StarColor1ID), targetStar1, t));
        skyboxMaterial.SetColor(StarColor2ID, Color.Lerp(skyboxMaterial.GetColor(StarColor2ID), targetStar2, t));

        skyboxMaterial.SetColor(Nebula1ColorMainID, Color.Lerp(skyboxMaterial.GetColor(Nebula1ColorMainID), targetNeb1Main, t));
        skyboxMaterial.SetColor(Nebula1ColorMidID, Color.Lerp(skyboxMaterial.GetColor(Nebula1ColorMidID), targetNeb1Mid, t));

        skyboxMaterial.SetColor(Nebula2Color1ID, Color.Lerp(skyboxMaterial.GetColor(Nebula2Color1ID), targetNeb2Color1, t));
        skyboxMaterial.SetColor(Nebula2Color2ID, Color.Lerp(skyboxMaterial.GetColor(Nebula2Color2ID), targetNeb2Color2, t));

        skyboxMaterial.SetFloat(DustAmountID, Mathf.Lerp(skyboxMaterial.GetFloat(DustAmountID), targetDust, t));
        skyboxMaterial.SetFloat(Nebula1StrengthID, Mathf.Lerp(skyboxMaterial.GetFloat(Nebula1StrengthID), targetNeb1Strength, t));
        skyboxMaterial.SetFloat(Nebula2StrengthID, Mathf.Lerp(skyboxMaterial.GetFloat(Nebula2StrengthID), targetNeb2Strength, t));

        if (tintAsteroidMaterials)
        {
            UpdateAsteroidMaterials(asteroidEnvironmentColor, samplePos, t);
        }

        if (animateSeed)
        {
            float combinedAxisValue =
                samplePos.x * 0.57f +
                samplePos.y * 0.31f +
                samplePos.z * 0.73f;

            float targetSeed = baseShaderSeed + combinedAxisValue * seedScrollAmount;

            skyboxMaterial.SetFloat(SeedID, Mathf.Lerp(skyboxMaterial.GetFloat(SeedID), targetSeed, t));
        }
    }

    private void UpdateParticleTrailColors(Color neb1Main, Color neb1Mid, Color neb2Color1, Color neb2Color2)
    {
        if (trailParticleSystem == null)
            return;

        Color trailColor = MakeTrailTintFromNebula(neb1Mid);

        var trails = trailParticleSystem.trails;
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailColor);
    }

    private Color MakeTrailTintFromNebula(Color sourceColor)
    {
        Color.RGBToHSV(sourceColor, out float h, out float s, out float v);

        // Hard-coded "color picker square" position:
        // Hue comes from the nebula/color wheel.
        // Saturation and brightness stay fixed so trails remain visible.
        s = 0.5f;
        v = 1.0f;

        Color result = Color.HSVToRGB(h, s, v);

        result.a = 1f;
        return result;
    }
}