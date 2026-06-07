using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class ModuleTooltipUI : MonoBehaviour
{
    public static ModuleTooltipUI Instance { get; private set; }

    [Header("References")]
    public GameObject tooltipRoot;
    public TMP_Text tooltipText;

    [Header("Canvas Positioning")]
    public RectTransform canvasRect;
    public Camera uiCamera;
    public RectTransform tooltipRect;
    public RectTransform textRect;

    [Header("Timing")]
    public float showDelay = 0.75f;

    [Header("Positioning")]
    public bool followMouse = true;
    public Vector2 screenOffset = new Vector2(24f, -24f);

    [Header("Screen Bounds")]
    public bool keepOnScreen = true;
    public Vector2 screenPadding = new Vector2(24f, 24f);

    private Coroutine showRoutine;
    private ModuleData currentModule;
    private bool pivotLocked;

    private void Awake()
    {
        Instance = this;

        if (tooltipRoot == null)
            tooltipRoot = gameObject;

        if (tooltipRect == null && tooltipRoot != null)
            tooltipRect = tooltipRoot.GetComponent<RectTransform>();

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!followMouse || tooltipRoot == null || !tooltipRoot.activeSelf)
            return;

        UpdateTooltipPosition();
    }

    public void ShowDelayed(ModuleData moduleData)
    {
        if (moduleData == null)
            return;

        currentModule = moduleData;

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowAfterDelay(moduleData));
    }

    private IEnumerator ShowAfterDelay(ModuleData moduleData)
    {
        yield return new WaitForSecondsRealtime(showDelay);

        if (moduleData != currentModule)
            yield break;

        Show(moduleData);
    }

    public void Show(ModuleData moduleData)
    {
        if (moduleData == null)
            return;

        currentModule = moduleData;

        if (tooltipText != null)
            tooltipText.text = BuildTooltipText(moduleData);

        if (tooltipRoot != null)
            tooltipRoot.SetActive(true);

        pivotLocked = false;

        if (followMouse)
            UpdateTooltipPosition();

        pivotLocked = true;

        if (StatManager.Instance != null)
            StatManager.Instance.ShowModuleHoverPreview(moduleData);
    }

    public void Hide()
    {
        currentModule = null;
        pivotLocked = false;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        HideImmediate();

        if (StatManager.Instance != null)
            StatManager.Instance.HideModuleHoverPreview();
    }

    private void HideImmediate()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null || canvasRect == null)
            return;

        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            uiCamera,
            out localPoint
        );

        if (!pivotLocked)
            UpdateVerticalPivot(localPoint);

        Vector2 desiredPosition = localPoint + screenOffset;

        if (keepOnScreen)
            desiredPosition = ClampTooltipHorizontally(desiredPosition);

        tooltipRect.anchoredPosition = desiredPosition;
    }

    private string BuildTooltipText(ModuleData data)
    {
        StringBuilder sb = new StringBuilder();

        string tierHex = GetTierHexColor(data.moduleTier);
        sb.AppendLine($"<color=#{tierHex}>Tier {data.moduleTier} {data.moduleType} Module</color>");
        sb.AppendLine($"<color=#{tierHex}>{data.moduleName}</color>");

        AppendStatLine(sb, data.chargeRateBonus, "Charge Rate", false);
        AppendStatLine(sb, data.chargeRateBonus_Percent, "Charge Rate", true);

        AppendStatLine(sb, data.baseLaunchSpeedBonus, "Launch Speed", false);
        AppendStatLine(sb, data.baseLaunchSpeedBonus_Percent, "Launch Speed", true);

        AppendStatLine(sb, data.maxSpeedBonus, "Max Speed", false);
        AppendStatLine(sb, data.maxSpeedBonus_Percent, "Max Speed", true);

        AppendStatLine(sb, data.accelerationBonus, "Acceleration", false);
        AppendStatLine(sb, data.accelerationBonus_Percent, "Acceleration", true);

        AppendStatLine(sb, data.boostAccelAddBonus, "Boost Acceleration", false);
        AppendStatLine(sb, data.boostAccelAddBonus_Percent, "Boost Acceleration", true);

        AppendStatLine(sb, data.boostMaxBonus, "Boost Max Speed", false);
        AppendStatLine(sb, data.boostMaxBonus_Percent, "Boost Max Speed", true);

        AppendStatLine(sb, data.capacityBonus, "Boost Capacity", false);
        AppendStatLine(sb, data.capacityBonus_Percent, "Boost Capacity", true);

        AppendStatLine(sb, data.drainPerSecondBonus, "Boost Drain", false);
        AppendStatLine(sb, data.drainPerSecondBonus_Percent, "Boost Drain", true);

        AppendStatLine(sb, data.regenPerSecondBonus, "Boost Regen", false);
        AppendStatLine(sb, data.regenPerSecondBonus_Percent, "Boost Regen", true);

        AppendStatLine(sb, data.shieldChargeBonus, "Shield Charge", false);

        AppendStatLine(sb, data.packagePlatingBonus, "Package Plating", false);

        sb.AppendLine();
        sb.AppendLine("Right-click to equip");
        sb.AppendLine("Drag to slot");

        return sb.ToString();
    }

    private void AppendStatLine(StringBuilder sb, float value, string statName, bool isPercent)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        string formattedValue = isPercent
            ? FormatPercent(value)
            : FormatFlat(value);

        sb.AppendLine($"{formattedValue} {statName}");
    }

    private string FormatFlat(float value)
    {
        return $"{value:+0.#;-0.#;0}";
    }

    private string FormatPercent(float value)
    {
        return $"{value * 100f:+0.#;-0.#;0}%";
    }
    private void UpdateVerticalPivot(Vector2 localPoint)
    {
        if (textRect == null)
            return;

        bool mouseIsAboveCenter = localPoint.y >= 0f;

        Vector2 pivot = textRect.pivot;
        pivot.y = mouseIsAboveCenter ? 1f : 0f;
        textRect.pivot = pivot;
    }

    private Vector2 ClampTooltipHorizontally(Vector2 desiredPosition)
    {
        Vector2 canvasSize = canvasRect.rect.size;

        float tooltipWidth = textRect != null
            ? textRect.rect.width
            : tooltipRect.rect.width;

        Vector2 pivot = tooltipRect.pivot;

        float minX = -canvasSize.x * 0.5f + screenPadding.x + tooltipWidth * pivot.x;
        float maxX = canvasSize.x * 0.5f - screenPadding.x - tooltipWidth * (1f - pivot.x);

        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);

        return desiredPosition;
    }
    private string GetTierHexColor(int tier)
    {
        Color tierColor = Color.white;

        if (ModuleInventoryManager.Instance != null)
            tierColor = ModuleInventoryManager.Instance.GetTierColor(tier);

        return ColorUtility.ToHtmlStringRGB(tierColor);
    }
}