using UnityEngine;
using UnityEngine.UI;

public class SlingshotEngineLight : MonoBehaviour
{
    public static SlingshotEngineLight Instance { get; private set; }

    [Header("Engine Light")]
    [SerializeField] private Graphic engineLight;
    [SerializeField] private Material orbitingMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // Automatically find an Image/RawImage on this object.
        if (!engineLight)
            engineLight = GetComponent<Graphic>();

        SetOrbiting(false);
    }

    public void SetOrbiting(bool orbiting)
    {
        if (!engineLight) return;

        Material nextMaterial = orbiting ? orbitingMaterial : null;
        Material effectiveMaterial = nextMaterial
            ? nextMaterial
            : engineLight.defaultMaterial;

        if (engineLight.material != effectiveMaterial)
            engineLight.material = nextMaterial;
    }

    private void OnDisable()
    {
        if (Instance == this)
            SetOrbiting(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}