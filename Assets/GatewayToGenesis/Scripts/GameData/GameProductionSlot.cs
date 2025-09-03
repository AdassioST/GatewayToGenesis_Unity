using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameProductionSlot : MonoBehaviour, IGameUnitSlot
{
    // INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 0.0f;
    public float amount { get; set; }
    public float maxAmount { get; set; }

    // VARIABLES
    public bool insufficientProduction;

    public ProductionUnitData productionUnitData;

    public Image icon;
    public TMP_Text amountText, nameText, typeText;

    public float incrementalCost;
    public float activeBuildingsAmount;


    // Dictionary to store ProductionUnitData by GameUnit name
    public static Dictionary<string, ProductionUnitData> productionUnitDataDictionary;

    private void Update()
    {
        if (gameUnit != null && productionUnitData != null)
        {
            UpdateMaxAmount();
        }
    }

    // Initialize the slot with its GameUnit and associated data
    public void InitializeSlot(GameUnit newGameUnit)
    {
        if (productionUnitDataDictionary == null)
        {
            InitializeProductionUnitDataDictionary();
        }

        gameUnit = newGameUnit;

        // Assign the correct ProductionUnitData
        if (productionUnitDataDictionary.ContainsKey(gameUnit.name))
        {
            productionUnitData = productionUnitDataDictionary[gameUnit.name];
        }
        else
        {
            Debug.LogWarning($"No ProductionUnitData found for {gameUnit.name}");
        }

        InitialiseProductionUnit(newGameUnit);
    }

    public void InitialiseProductionUnit(GameUnit newProductionUnit)
    {
        gameUnit = newProductionUnit;
        icon.sprite = newProductionUnit.icon;
        RefreshProductionAmount();
    }

    public void UpdateMaxAmount()
    {
        if (insufficientProduction)
        {
            amount = maxAmount * 0.25f;

            // ADD HERE INSUFFICIENT AMOUNT EVENT TRIGGER
        }
        else
        {
            amount = maxAmount;
        }

        RefreshProductionAmount();
    }

    public void RefreshProductionAmount()
    {
        amountText.text = Mathf.Round(maxAmount).ToString();
        nameText.text = gameUnit.name.ToString();
        typeText.text = gameUnit.type.ToString();
    }

    public static void InitializeProductionUnitDataDictionary()
    {
        productionUnitDataDictionary = new Dictionary<string, ProductionUnitData>();

        // Load all ProductionUnitData from Resources
        ProductionUnitData[] productionUnitDataArray = Resources.LoadAll<ProductionUnitData>("Production");

        foreach (var unitData in productionUnitDataArray)
        {
            if (!productionUnitDataDictionary.ContainsKey(unitData.name))
            {
                productionUnitDataDictionary.Add(unitData.name, unitData);
            }
            else
            {
                Debug.LogWarning($"Duplicate ProductionUnitData found for {unitData.name}, Skipping");
            }
        }
    }

    public float CalculateIncrementalCost(string resourceName, float baseAmount)
    {
        bool isUnit = gameUnit.type == "Unit";

        float incrementalCost = baseAmount;

        if (!isUnit)
        {
            incrementalCost *= Mathf.Exp((GlobalProductionManager.Instance.costBalance / GlobalProductionManager.Instance.techTier) * amount);
        }

        return incrementalCost;
    }

    /// <summary>
    /// Get the effective production rate for this unit considering all modifiers
    /// </summary>
    /// <param name="resourceIndex">Index of the resource in producedResources list</param>
    /// <returns>Effective production rate with modifiers applied</returns>
    public float GetEffectiveProductionRate(int resourceIndex)
    {
        if (productionUnitData == null || resourceIndex < 0 || resourceIndex >= productionUnitData.productionRates.Count)
            return 0f;
        
        float baseRate = productionUnitData.productionRates[resourceIndex];
        
        if (GameUnitsLogic.Instance != null)
        {
            return GameUnitsLogic.Instance.GetEffectiveProductionRate(productionUnitData, baseRate);
        }
        
        return baseRate;
    }
    
    /// <summary>
    /// Get the effective construction cost for this unit considering all modifiers
    /// </summary>
    /// <param name="resourceIndex">Index of the resource in buildResourceRequirements list</param>
    /// <returns>Effective construction cost with modifiers applied</returns>
    public float GetEffectiveConstructionCost(int resourceIndex)
    {
        if (productionUnitData == null || resourceIndex < 0 || resourceIndex >= productionUnitData.buildRequirementsAmount.Count)
            return 0f;
        
        float baseCost = productionUnitData.buildRequirementsAmount[resourceIndex];
        
        if (GameUnitsLogic.Instance != null)
        {
            return GameUnitsLogic.Instance.GetEffectiveConstructionCost(productionUnitData, baseCost);
        }
        
        return baseCost;
    }
    
    /// <summary>
    /// Get a summary of all active modifiers affecting this production unit
    /// </summary>
    /// <returns>Formatted string showing active modifiers</returns>
    public string GetModifierSummary()
    {
        if (GameUnitsLogic.Instance == null || gameUnit == null) return "";
        
        var summary = new List<string>();
        
        // Production efficiency modifiers
        float productionModifier = GameUnitsLogic.Instance.GetProductionEfficiencyModifier(gameUnit.type, gameUnit.section);
        if (productionModifier != 0f)
        {
            string sign = productionModifier > 0 ? "+" : "";
            summary.Add($"Production: {sign}{productionModifier:F1}%");
        }
        
        // Construction cost modifiers
        float costModifier = GameUnitsLogic.Instance.GetConstructionCostModifier(gameUnit.type, gameUnit.section);
        if (costModifier != 0f)
        {
            string effect = costModifier > 0 ? "Cost +" : "Cost -";
            summary.Add($"{effect}{Mathf.Abs(costModifier):F1}%");
        }
        
        if (summary.Count > 0)
        {
            return string.Join(", ", summary);
        }
        
        return "";
    }

}

