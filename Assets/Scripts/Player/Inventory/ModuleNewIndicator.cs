using UnityEngine;
using UnityEngine.UI;

public sealed class ModuleNewIndicator : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Material newModuleMaterial;

    [Header("Pulse")]
    [SerializeField] private float minStrength = 2f;
    [SerializeField] private float maxStrength = 5f;
    [SerializeField, Min(0.01f)] private float cycleSeconds = 2f;

    [Header("Background Breathing")]
    [SerializeField] private Button slotButton;

    [Tooltip("Fraction of the HSV Value gap from Normal to Highlighted.")]
    [SerializeField, Range(0f, 1f)]
    private float backgroundLift = 0.3f;

    [Tooltip("Adds a gentle linger near the bright peak. Zero is a regular cosine.")]
    [SerializeField, Range(0f, 0.25f)]
    private float waveFlair = 0.12f;

    private ColorBlock originalButtonColors;
    private bool buttonColorsCached;
    private bool backgroundPulseApplied;

    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

    private Material originalMaterial;
    private Material pulseMaterial;
    private bool isNew;
    private float elapsed;

    private void Awake()
    {
        if (targetImage == null)
            return;

        // Cache the reference; never modify this material.
        originalMaterial = targetImage.material;

        if (newModuleMaterial == null)
            return;

        if (!newModuleMaterial.HasProperty(EmissionStrengthId))
        {
            Debug.LogWarning(
                "[ModuleNewIndicator] Material has no _EmissionStrength property.",
                this);
            return;
        }

        if (slotButton == null)
            slotButton = GetComponent<Button>();

        if (slotButton != null)
        {
            originalButtonColors = slotButton.colors;
            buttonColorsCached = true;
        }

        // Clone once per slot, rather than modifying the project asset.
        pulseMaterial = new Material(newModuleMaterial)
        {
            name = newModuleMaterial.name + " (Runtime Slot)",
            hideFlags = HideFlags.HideAndDontSave
        };

        pulseMaterial.SetFloat(EmissionStrengthId, minStrength);
    }

    public void SetNew(bool value)
    {
        if (isNew == value)
            return;

        isNew = value;
        elapsed = 0f;
        ApplyMaterial();

        if (isNew && isActiveAndEnabled)
            ApplyBackgroundPulse(0f);
        else
            RestoreBackground();
    }

    private void OnEnable()
    {
        ApplyMaterial();

        if (isNew)
            ApplyBackgroundPulse(0f);
    }

    private void OnDisable()
    {
        RestoreBackground();
        RestoreMaterial();
    }

    private void ApplyMaterial()
    {
        if (targetImage == null)
            return;

        if (isNew && isActiveAndEnabled && pulseMaterial != null)
        {
            pulseMaterial.SetFloat(EmissionStrengthId, minStrength);
            targetImage.material = pulseMaterial;
        }
        else
        {
            RestoreMaterial();
        }
    }

    private void LateUpdate()
    {
        if (!isNew)
            return;

        elapsed = Mathf.Repeat(
            elapsed + Time.unscaledDeltaTime,
            Mathf.Max(0.01f, cycleSeconds));

        float phase =
            elapsed / Mathf.Max(0.01f, cycleSeconds) * Mathf.PI * 2f;

        // Smooth breathing, with a little extra time near the bright peak.
        float shapedPhase = phase + waveFlair * Mathf.Sin(phase);
        float t = (1f - Mathf.Cos(shapedPhase)) * 0.5f;

        ApplyBackgroundPulse(t);

        if (targetImage == null || pulseMaterial == null)
            return;

        float strength = Mathf.Lerp(minStrength, maxStrength, t);
        pulseMaterial.SetFloat(EmissionStrengthId, strength);

        Material renderedMaterial = targetImage.materialForRendering;

        if (renderedMaterial != null &&
            renderedMaterial != pulseMaterial &&
            renderedMaterial.HasProperty(EmissionStrengthId))
        {
            renderedMaterial.SetFloat(EmissionStrengthId, strength);
        }
    }

    private void RestoreMaterial()
    {
        if (targetImage != null &&
            pulseMaterial != null &&
            targetImage.material == pulseMaterial)
        {
            targetImage.material = originalMaterial;
        }
    }

    private void OnDestroy()
    {
        RestoreBackground();
        RestoreMaterial();

        if (pulseMaterial != null)
            Destroy(pulseMaterial);
    }
    private void ApplyBackgroundPulse(float t)
    {
        if (!buttonColorsCached || slotButton == null ||
            !slotButton.isActiveAndEnabled ||
            slotButton.transition != Selectable.Transition.ColorTint)
        {
            RestoreBackground();
            return;
        }

        Color normal = originalButtonColors.normalColor;

        Color.RGBToHSV(normal, out float h, out float s, out float normalV);

        Color.RGBToHSV(
            originalButtonColors.highlightedColor,
            out float unusedH,
            out float unusedS,
            out float highlightedV);

        // Never darken the normal color if Highlighted happens to be darker.
        float peakV = Mathf.Lerp(
            normalV,
            Mathf.Max(normalV, highlightedV),
            backgroundLift);

        Color pulsedColor = Color.HSVToRGB(
            h, s, Mathf.Lerp(normalV, peakV, t));

        pulsedColor.a = normal.a;

        ColorBlock colors = originalButtonColors;
        colors.normalColor = pulsedColor;

        // The waveform already supplies the smoothing. Avoid restarting
        // the Button's color fade every frame.
        colors.fadeDuration = 0f;

        slotButton.colors = colors;
        backgroundPulseApplied = true;
    }

    private void RestoreBackground()
    {
        if (!backgroundPulseApplied)
            return;

        if (buttonColorsCached && slotButton != null)
            slotButton.colors = originalButtonColors;

        backgroundPulseApplied = false;
    }
}