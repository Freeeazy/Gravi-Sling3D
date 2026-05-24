using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleShopButton : MonoBehaviour
{
    [Header("Purchase Settings")]
    public bool useRandomType = true;

    [Tooltip("Only used when useRandomType is false. Examples: Engine, Battery, Orbit")]
    public string forcedModuleType = "Engine";

    [Header("Cost Settings")]
    public float baseCost = 300f;
    public float specificTypeMultiplier = 1.75f;
    public bool scaleCostByRank = true;

    [Header("Optional UI")]
    public Button button;
    public TMP_Text labelText;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveListener(BuyModule);
            button.onClick.AddListener(BuyModule);
        }

        RefreshLabel();
    }

    private void OnEnable()
    {
        RefreshLabel();
    }

    public void BuyModule()
    {
        ModuleInventoryManager inv = ModuleInventoryManager.Instance;

        if (inv == null)
        {
            Debug.LogWarning("[ModuleShopButton] Cannot buy module. ModuleInventoryManager.Instance is null.");
            return;
        }

        float finalCost = GetFinalCost();

        ModuleData boughtModule = inv.TryBuyModuleFromShop(
            finalCost,
            useRandomType,
            forcedModuleType
        );

        if (boughtModule == null)
        {
            Debug.Log("[ModuleShopButton] Purchase failed.");
            return;
        }

        Debug.Log($"[ModuleShopButton] Bought module: {boughtModule.moduleName}");
        RefreshLabel();
    }

    public float GetFinalCost()
    {
        float cost = baseCost;

        if (!useRandomType)
            cost *= specificTypeMultiplier;

        if (scaleCostByRank && FamilyReputationManager.Instance != null)
        {
            int rankIndex = FamilyReputationManager.Instance.GetCurrentRankIndex();

            // Rookie 1.0x, Runner 1.5x, Trusted 2.25x, Made 3.5x, Legend 5.0x
            switch (rankIndex)
            {
                case 0:
                    cost *= 1f;
                    break;
                case 1:
                    cost *= 1.5f;
                    break;
                case 2:
                    cost *= 2.25f;
                    break;
                case 3:
                    cost *= 3.5f;
                    break;
                case 4:
                    cost *= 5f;
                    break;
            }
        }

        return Mathf.Round(cost);
    }

    private void RefreshLabel()
    {
        if (labelText == null)
            return;

        float finalCost = GetFinalCost();

        if (useRandomType)
        {
            labelText.text = $"Buy Random Module\n{finalCost:0} Credits";
        }
        else
        {
            labelText.text = $"Buy {forcedModuleType} Module\n{finalCost:0} Credits";
        }
    }
}