using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Production-tab slot for one building or unit type.
/// <see cref="maxAmount"/> is how many are built; <see cref="amount"/> is how many are effectively
/// producing (reduced while their inputs are missing).
/// </summary>
public class GameProductionSlot : MonoBehaviour, IGameUnitSlot, ITooltipSource
{
    public GameUnit gameUnit { get; set; }

    [Tooltip("Share of built units that keep producing while an input resource is depleted.")]
    [SerializeField] private float insufficientOutputShare = 0.25f;

    private float _built;
    private bool _insufficientProduction;

    /// <summary>Number of units built.</summary>
    public float maxAmount
    {
        get => _built;
        set
        {
            if (Mathf.Approximately(_built, value)) return;
            _built = value;
            UpdateMaxAmount();
        }
    }

    /// <summary>Units effectively producing this frame.</summary>
    public float amount { get; private set; }

    /// <summary>Set by GlobalProductionManager when a consumed resource runs out.</summary>
    public bool insufficientProduction
    {
        get => _insufficientProduction;
        set
        {
            if (_insufficientProduction == value) return;
            _insufficientProduction = value;
            UpdateMaxAmount();
        }
    }

    public ProductionUnitData productionUnitData;

    public Image icon;
    public TMP_Text amountText, nameText, typeText;

    /// <summary>Last price paid for the final build requirement (display only).</summary>
    public float incrementalCost;

    public void InitializeSlot(GameUnit newGameUnit)
    {
        gameUnit = newGameUnit;
        productionUnitData = GameCatalog.ProductionUnits.Get(newGameUnit.name, nameof(GameProductionSlot));
        InitialiseProductionUnit(newGameUnit);
    }

    public void InitialiseProductionUnit(GameUnit newProductionUnit)
    {
        gameUnit = newProductionUnit;
        icon.sprite = newProductionUnit.icon;
        RefreshProductionAmount();
    }

    /// <summary>Recompute the effective producing count and notify production.</summary>
    public void UpdateMaxAmount()
    {
        amount = _insufficientProduction ? _built * insufficientOutputShare : _built;
        GlobalProductionManager.Instance?.MarkDirty();
        RefreshProductionAmount();
    }

    public void RefreshProductionAmount()
    {
        if (gameUnit == null) return;
        amountText.text = Mathf.Round(_built).ToString();
        nameText.text = gameUnit.name;
        typeText.text = gameUnit.type;
    }

    /// <summary>Output per unit of one produced resource, including production efficiency modifiers.</summary>
    public float GetEffectiveProductionRate(int resourceIndex)
    {
        if (productionUnitData == null || resourceIndex < 0 || resourceIndex >= productionUnitData.productionRates.Count) return 0f;
        float efficiency = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetProductionEfficiencyPercent(gameUnit) : 0f;
        return productionUnitData.productionRates[resourceIndex] * (1f + efficiency / 100f);
    }

    /// <summary>Price of the next unit for one build requirement.</summary>
    public float GetEffectiveConstructionCost(int resourceIndex) => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetBuildCost(this, resourceIndex) : 0f;

    /// <summary>Active efficiency and cost modifiers, e.g. "Production: +10%, Cost -15%".</summary>
    public string GetModifierSummary()
    {
        if (GameUnitsLogic.Instance == null || gameUnit == null) return "";
        var summary = new List<string>();
        float production = GameUnitsLogic.Instance.GetProductionEfficiencyPercent(gameUnit);
        if (production != 0f) summary.Add($"Production: {(production > 0 ? "+" : "")}{production:F1}%");
        float cost = GameUnitsLogic.Instance.GetConstructionCostPercent(gameUnit);
        if (cost != 0f) summary.Add($"Cost {(cost > 0 ? "+" : "-")}{Mathf.Abs(cost):F1}%");
        return string.Join(", ", summary);
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Production(this, data);
}
