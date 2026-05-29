using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShieldVisualHelper : MonoBehaviour
{
    public static ShieldVisualHelper Instance { get; private set; }

    [Header("Text")]
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private Color textOnColor = Color.white;
    [SerializeField] private Color textOffColor = Color.gray;

    [Header("Images")]
    [SerializeField] private Image imageOne;
    [SerializeField] private Image imageTwo;
    [SerializeField] private Image imageThree;

    [Header("Image One Materials")]
    [SerializeField] private Material imageOneOnMaterial;
    [SerializeField] private Material imageOneOffMaterial;

    [Header("Image Two Materials")]
    [SerializeField] private Material imageTwoOnMaterial;
    [SerializeField] private Material imageTwoOffMaterial;

    [Header("Image Three Materials")]
    [SerializeField] private Material imageThreeOnMaterial;
    [SerializeField] private Material imageThreeOffMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    private void Start()
    {
        UpdateShieldVisual(0);
    }

    /// <summary>
    /// Updates the shield text and visual on/off state.
    /// If shieldValue is below 0, visuals are set to OFF.
    /// Otherwise, visuals are set to ON.
    /// </summary>
    public void UpdateShieldVisual(int shieldValue)
    {
        UpdateShieldText(shieldValue);

        bool isOn = shieldValue > 0;
        SetShieldVisualState(isOn);
    }

    /// <summary>
    /// Only updates the TMP text value.
    /// Example: 5 displays as "5".
    /// </summary>
    public void UpdateShieldText(int shieldValue)
    {
        if (shieldText != null)
        {
            shieldText.text = $"<space=40px>{shieldValue.ToString()}";
        }
    }

    /// <summary>
    /// Manually sets the visuals to on/off.
    /// </summary>
    public void SetShieldVisualState(bool isOn)
    {
        if (shieldText != null)
        {
            shieldText.color = isOn ? textOnColor : textOffColor;
        }

        if (imageOne != null)
        {
            imageOne.material = isOn ? imageOneOnMaterial : imageOneOffMaterial;
        }

        if (imageTwo != null)
        {
            imageTwo.material = isOn ? imageTwoOnMaterial : imageTwoOffMaterial;
        }

        if (imageThree != null)
        {
            imageThree.material = isOn ? imageThreeOnMaterial : imageThreeOffMaterial;
        }
    }
}