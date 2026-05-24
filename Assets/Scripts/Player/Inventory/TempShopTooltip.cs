using TMPro;
using UnityEngine;

public class TempShopTooltipUI : MonoBehaviour
{
    public static TempShopTooltipUI Instance { get; private set; }

    [Header("Refs")]
    public GameObject tooltipRoot;
    public TMP_Text tooltipText;

    [Header("Text")]
    public string randomModuleLabel = "Random Module";
    public string creditLabel = "credits";

    private void Awake()
    {
        Instance = this;

        if (tooltipRoot == null)
            tooltipRoot = gameObject;

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(ModuleShopButton shopButton)
    {
        if (tooltipRoot == null || tooltipText == null || shopButton == null)
            return;

        string moduleLabel = shopButton.useRandomType
            ? randomModuleLabel
            : $"{shopButton.forcedModuleType} Module";

        float cost = shopButton.GetFinalCost();

        tooltipText.text = $"Buy {moduleLabel}\nCost: {cost:0} {creditLabel}";

        tooltipRoot.SetActive(true);
    }

    public void Hide()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }
}