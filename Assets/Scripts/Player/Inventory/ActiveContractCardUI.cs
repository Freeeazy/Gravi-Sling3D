using System.Collections;
using System.Diagnostics.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveContractCardUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text contractTitleText;
    public TMP_Text contractDistanceText;
    public TMP_Text contractIntegrityText;
    public TMP_Text contractTimeText;

    [Header("Integrity Sliders")]
    public Image DefaultIntegritySlider;
    public Image OvershieldIntegritySlider;
    public Image DefaultIntegrityDamageSlider;
    public Image OvershieldIntegrityDamageSlider;

    [SerializeField] private float damageHoldTime = 0.25f;
    [SerializeField] private float damageDrainSpeed = 35f;

    private float defaultIntegritySliderFullWidth;
    private float overshieldIntegritySliderFullWidth;

    private float displayedIntegrity;
    private float damageDrainTimer;
    private bool initializedIntegrityDisplay;

    [Header("Damage Flash")]
    [SerializeField] private float damageTextHoldTime = 1f;
    [SerializeField] private string damageColor = "#FF3D3D";

    [Header("Package Colors")]
    [SerializeField] private Color fallbackPackageColor = Color.white;

    private string currentDeliveryItem;
    private string currentDestinationName;
    private float currentDistance;
    private float currentIntegrity;
    private string currentTimeBonusText = "--:--";
    private Color currentPackageColor = Color.white;

    private string integrityExtraText = "";
    private Coroutine damageTextRoutine;
    private void Awake()
    {
        if (DefaultIntegritySlider)
            defaultIntegritySliderFullWidth = DefaultIntegritySlider.rectTransform.rect.width;

        if (OvershieldIntegritySlider)
            overshieldIntegritySliderFullWidth = OvershieldIntegritySlider.rectTransform.rect.width;

        currentPackageColor = fallbackPackageColor;
        RefreshPackageColors();
    }
    public void SetInfo(
        string deliveryItem,
        string destinationName,
        float distance,
        float integrity,
        string timeBonusText = "--:--",
        Color? packageColor = null)
    {
        currentDeliveryItem = deliveryItem;
        currentDestinationName = destinationName;
        currentDistance = distance;
        currentTimeBonusText = timeBonusText;

        Color newPackageColor = packageColor ?? fallbackPackageColor;

        if (currentPackageColor != newPackageColor)
        {
            currentPackageColor = newPackageColor;
            RefreshPackageColors();
        }

        if (!initializedIntegrityDisplay)
        {
            displayedIntegrity = integrity;
            initializedIntegrityDisplay = true;
        }
        else if (integrity < currentIntegrity)
        {
            damageDrainTimer = damageHoldTime;
            displayedIntegrity = Mathf.Max(displayedIntegrity, currentIntegrity);
        }
        else if (integrity > currentIntegrity)
        {
            displayedIntegrity = integrity;
        }

        currentIntegrity = integrity;

        RefreshText();
    }
    private void Update()
    {
        if (!initializedIntegrityDisplay)
            return;

        if (damageDrainTimer > 0f)
        {
            damageDrainTimer -= Time.deltaTime;
        }
        else if (displayedIntegrity > currentIntegrity)
        {
            displayedIntegrity = Mathf.MoveTowards(
                displayedIntegrity,
                currentIntegrity,
                damageDrainSpeed * Time.deltaTime
            );

            RefreshIntegritySliders();
        }
    }

    // -------------------------------------------------
    // PACKAGE / FACTION COLORS
    // -------------------------------------------------

    private void RefreshPackageColors()
    {
        // Only the base integrity fill inherits
        // the faction or Weird cargo color.
        if (DefaultIntegritySlider != null)
        {
            DefaultIntegritySlider.color = currentPackageColor;
        }

        // Overshield and damage bars intentionally
        // retain their prefab/Inspector colors.
    }

    // -------------------------------------------------
    // DAMAGE TEXT
    // -------------------------------------------------

    public void ShowIntegrityDamage(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        if (damageTextRoutine != null)
            StopCoroutine(damageTextRoutine);

        integrityExtraText = $" <color={damageColor}>-{damageAmount:0}%</color>";

        RefreshText();

        damageTextRoutine = StartCoroutine(ClearDamageTextAfterDelay());
    }

    private IEnumerator ClearDamageTextAfterDelay()
    {
        yield return new WaitForSeconds(damageTextHoldTime);

        integrityExtraText = "";
        RefreshText();

        damageTextRoutine = null;
    }

    private void RefreshText()
    {
        if (!contractTitleText || !contractDistanceText || !contractIntegrityText || !contractTimeText)
            return;

        string hex = QuestColorUtil.GetHex(currentPackageColor);

        contractTitleText.text =
            $"<color={hex}>───────────────────────</color>\n" +
            $"Deliver {currentDeliveryItem} to {currentDestinationName}\n" +
            $"<color={hex}>───────────────────────</color>\n";

        contractDistanceText.text = $"{currentDistance:0} Units\n";
        contractIntegrityText.text = $"{currentIntegrity:0}%{integrityExtraText}\n";
        contractTimeText.text = $"Guarantee Window: {currentTimeBonusText}";

        RefreshIntegritySliders();
    }
    private void RefreshIntegritySliders()
    {
        float currentDefault = Mathf.Clamp(currentIntegrity, 0f, 100f);
        float shownDefault = Mathf.Clamp(displayedIntegrity, 0f, 100f);

        float currentOvershield = Mathf.Clamp(currentIntegrity - 100f, 0f, 100f);
        float shownOvershield = Mathf.Clamp(displayedIntegrity - 100f, 0f, 100f);

        SetBar(DefaultIntegritySlider, defaultIntegritySliderFullWidth, currentDefault / 100f);
        SetBar(OvershieldIntegritySlider, overshieldIntegritySliderFullWidth, currentOvershield / 100f);

        SetDamageBar(DefaultIntegrityDamageSlider, defaultIntegritySliderFullWidth, currentDefault, shownDefault);
        SetDamageBar(OvershieldIntegrityDamageSlider, overshieldIntegritySliderFullWidth, currentOvershield, shownOvershield);
    }
    private void SetBar(Image image, float fullWidth, float fill01)
    {
        if (!image)
            return;

        fill01 = Mathf.Clamp01(fill01);
        image.gameObject.SetActive(fill01 > 0f);
        image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth * fill01);
    }

    private void SetDamageBar(Image image, float fullWidth, float currentValue, float displayedValue)
    {
        if (!image)
            return;

        float damageValue = Mathf.Max(0f, displayedValue - currentValue);
        bool showDamage = damageValue > 0.01f;

        image.gameObject.SetActive(showDamage);

        if (!showDamage)
            return;

        float currentWidth = fullWidth * Mathf.Clamp01(currentValue / 100f);
        float damageWidth = fullWidth * Mathf.Clamp01(damageValue / 100f);

        image.rectTransform.anchoredPosition = new Vector2(
            currentWidth,
            image.rectTransform.anchoredPosition.y
        );

        image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, damageWidth);
    }
    public void Clear()
    {
        if (damageTextRoutine != null)
        {
            StopCoroutine(damageTextRoutine);
            damageTextRoutine = null;
        }

        integrityExtraText = "";

        if (contractTitleText)
            contractTitleText.text = "";
        if (contractDistanceText)
            contractDistanceText.text = "";
        if (contractIntegrityText)
            contractIntegrityText.text = "";
        if (contractTimeText)
            contractTimeText.text = "";
    }
}