using UnityEngine;

public class StationProxy : MonoBehaviour
{
    [Header("Orbit (root)")]
    public SphereCollider orbitTrigger;          // same GO as SlingshotPlanet3D
    public SlingshotPlanet3D slingshot;

    [Header("Pre-Gravity (child)")]
    public SphereCollider preGravityTrigger;     // child GO
    public PreGravityPullZone preGravity;

    [Header("Quest Highlight (optional)")]
    [Tooltip("White sphere overlay (or any highlight GO). Will be toggled for the active quest target.")]
    public GameObject questHighlight;
    public StationDeliveryReactionEmitter reactionEmitter;

    [Header("Bubble Visuals")]
    public GameObject bubble;
    public Renderer bubbleRenderer;
    public Material CurrentBubbleMaterial { get; private set; }
    public Color CurrentBubbleLightColor { get; private set; }

    [Tooltip("Bubble materials that visually match the station material sets by index.")]
    public Material[] bubbleMaterials;

    [Header("Station Visuals")]
    public MeshRenderer stationRenderer;

    [Tooltip("One shared material used for every glowing/light slot on the station mesh.")]
    public Material sharedStationLightMaterial;

    [Tooltip("Auto-replace material slots whose names contain these words.")]
    public string[] lightMaterialNameKeywords = { "light", "emissive", "glow" };

    [Header("Shader Property Names")]
    [Tooltip("Reference name of BorderColor in your bubble Shader Graph.")]
    public string bubbleBorderColorProperty = "_BorderColor";

    [Tooltip("Color/emission property on the station light material.")]
    public string stationLightColorProperty = "_EmissionColor";

    [Header("Random Color Fallback")]
    [Tooltip("Use a random HSV color if the bubble material does not expose BorderColor.")]
    public bool useRandomColorIfMissing = true;

    [Range(0f, 1f)]
    public float randomSaturation = 0.75f; // 75% to the right in the HSV color square

    [Range(0f, 1f)]
    public float randomValue = 1f; // top of the HSV color square

    public float hdrIntensity = 5.4f;

    [Header("Station Light HDR Tuning")]
    [Tooltip("Multiplies the final station light HDR color. Lower than 1 dims it, higher than 1 boosts it.")]
    public float stationLightIntensityMultiplier = 0.65f;

    [Tooltip("If true, applies stationLightIntensityMultiplier after sampling/generating the light color.")]
    public bool useStationLightIntensityMultiplier = true;

    public Vector3Int Coord { get; private set; }

    private MaterialPropertyBlock _stationLightBlock;

    private void Reset()
    {
        // Try auto-wire for convenience
        orbitTrigger = GetComponent<SphereCollider>();
        slingshot = GetComponent<SlingshotPlanet3D>();

        preGravity = GetComponentInChildren<PreGravityPullZone>(true);
        if (preGravity) preGravityTrigger = preGravity.GetComponent<SphereCollider>();

        reactionEmitter = GetComponentInChildren<StationDeliveryReactionEmitter>(true);
    }

    public void Assign(Vector3Int coord, Vector3 worldPos, Quaternion worldRot, StationFieldData data)
    {
        ClearRuntimeState();

        Coord = coord;

        transform.SetPositionAndRotation(worldPos, worldRot);

        // --- radii ---
        float orbitR = Mathf.Max(0.1f, data.orbitRadius); // or data.orbitRadius if you add it
        float preR = orbitR * 1.8f;                       // or data.preGravityRadius

        if (orbitTrigger)
        {
            orbitTrigger.isTrigger = true;
            orbitTrigger.radius = orbitR;
        }

        if (preGravityTrigger)
        {
            preGravityTrigger.isTrigger = true;
            preGravityTrigger.radius = preR;
        }

        // --- wire references between scripts ---
        if (slingshot)
        {
            slingshot.orbitRadius = orbitR; // IMPORTANT: your script uses this internally
            slingshot.preGravityZone = preGravity;
            // slingshot.Bubble can be optional / or assigned in prefab
        }

        if (preGravity)
        {
            preGravity.planet = slingshot; // your PreGravityPullZone expects this
        }

        ApplyRandomBubbleAndStationLights();
    }
    public void AssignAtCurrentPose(Vector3Int coord, StationFieldData data)
    {
        ClearRuntimeState();

        if (!data)
        {
            Coord = coord;
            RandomizeBubbleAndStationLights();
            return;
        }

        Assign(coord, transform.position, transform.rotation, data);
    }
    public void RandomizeBubbleAndStationLights()
    {
        ApplyRandomBubbleAndStationLights();
    }
    private void ApplyRandomBubbleAndStationLights()
    {
        Material chosenBubbleMaterial = null;

        // Pick bubble material
        if (bubbleRenderer && bubbleMaterials != null && bubbleMaterials.Length > 0)
        {
            int idx = Random.Range(0, bubbleMaterials.Length);
            chosenBubbleMaterial = bubbleMaterials[idx];

            CurrentBubbleMaterial = chosenBubbleMaterial;

            bubbleRenderer.sharedMaterial = chosenBubbleMaterial;
        }

        // Make station mesh use one shared light material where applicable
        ApplySharedLightMaterialToStationSlots();

        // Pull color from bubble BorderColor, or generate fallback
        Color lightColor = GetLightColorFromBubble(chosenBubbleMaterial);
        CurrentBubbleLightColor = lightColor;

        // Apply per-station color without creating material instances
        ApplyStationLightColor(lightColor);
    }
    private void ApplySharedLightMaterialToStationSlots()
    {
        if (!stationRenderer || !sharedStationLightMaterial)
            return;

        Material[] mats = stationRenderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (!mat)
                continue;

            if (ShouldReplaceWithSharedLightMaterial(mat))
            {
                mats[i] = sharedStationLightMaterial;
                changed = true;
            }
        }

        if (changed)
            stationRenderer.sharedMaterials = mats;
    }
    private bool ShouldReplaceWithSharedLightMaterial(Material mat)
    {
        string matName = mat.name.ToLower();

        for (int i = 0; i < lightMaterialNameKeywords.Length; i++)
        {
            string keyword = lightMaterialNameKeywords[i];

            if (!string.IsNullOrWhiteSpace(keyword) && matName.Contains(keyword.ToLower()))
                return true;
        }

        return false;
    }

    private Color GetLightColorFromBubble(Material bubbleMat)
    {
        Color finalColor;

        if (bubbleMat && bubbleMat.HasProperty(bubbleBorderColorProperty))
        {
            finalColor = bubbleMat.GetColor(bubbleBorderColorProperty);
        }
        else if (useRandomColorIfMissing)
        {
            float hue = Random.value;

            Color randomColor = Color.HSVToRGB(
                hue,
                randomSaturation,
                randomValue
            );

            finalColor = randomColor * hdrIntensity;
        }
        else
        {
            finalColor = Color.white * hdrIntensity;
        }

        if (useStationLightIntensityMultiplier)
            finalColor *= stationLightIntensityMultiplier;

        return finalColor;
    }

    private void ApplyStationLightColor(Color lightColor)
    {
        if (!stationRenderer)
            return;

        if (_stationLightBlock == null)
            _stationLightBlock = new MaterialPropertyBlock();

        Material[] mats = stationRenderer.sharedMaterials;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != sharedStationLightMaterial)
                continue;

            stationRenderer.GetPropertyBlock(_stationLightBlock, i);

            _stationLightBlock.SetColor(stationLightColorProperty, lightColor);

            stationRenderer.SetPropertyBlock(_stationLightBlock, i);
        }
    }
    public void SetQuestHighlight(bool on)
    {
        if (questHighlight) questHighlight.SetActive(on);
    }
    public void ClearRuntimeState()
    {
        if (reactionEmitter == null)
            reactionEmitter = GetComponentInChildren<StationDeliveryReactionEmitter>(true);

        if (reactionEmitter != null)
            reactionEmitter.ClearReaction();
    }
}
