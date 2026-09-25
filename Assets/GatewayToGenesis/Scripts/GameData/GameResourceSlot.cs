using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Storage-tab slot for one resource: current amount, capacity and net rate.</summary>
public class GameResourceSlot : MonoBehaviour, IGameUnitSlot, ITooltipSource
{
    public GameUnit gameUnit { get; set; }

    /// <summary>Current stock, always within [0, maxAmount].</summary>
    public float amount { get; set; }
    public float maxAmount { get; set; } = 200f;

    /// <summary>Net rate per second, written by GlobalProductionManager.</summary>
    public float productionRate = 0;
    /// <summary>Click power before upgrades and modifiers; effective power never drops below it.</summary>
    public float baseClickPower = 1f;

    public Image icon, fill;

    public TMP_Text amountText, productionRateText;

    private Color originalProductionRateColor;
    private bool _initialized;

    /// <summary>Effective click power (see <see cref="GameUnitsLogic.GetEffectiveClickPower"/>).</summary>
    public float clickPower => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetEffectiveClickPower(this) : baseClickPower;

    public void Start()
    {
        InitialiseResource(gameUnit);
    }

    public void InitialiseResource(GameUnit newResource)
    {
        if (newResource == null) return;
        gameUnit = newResource;
        icon.sprite = newResource.icon;
        if (!_initialized)
        {
            originalProductionRateColor = productionRateText.color;
            _initialized = true;
        }
        RefreshProductionAmount();
    }

    /// <summary>Add (or remove, when negative) stock, clamped to capacity, and refresh the display.</summary>
    public float ChangeAmount(float delta)
    {
        float before = amount;
        amount = Mathf.Clamp(amount + delta, 0f, maxAmount);
        RefreshProductionAmount();
        return amount - before;
    }

    public void RefreshProductionAmount()
    {
        if (gameUnit == null || GameUnitsLogic.Instance == null) return;
        amountText.text = GameUnitsLogic.Instance.FormatValue(amount);
        productionRateText.text = GameUnitsLogic.Instance.FormatValue(productionRate) + "/s";

        // Food shows progress towards the next population growth instead of storage.
        if (gameUnit.role == ResourceRole.Food && PopGrowthLogic.Instance != null)
        {
            fill.fillAmount = Mathf.Clamp01(amount / Mathf.Max(1f, PopGrowthLogic.Instance.GetEffectiveFoodThreshold()));
        }
        else
        {
            fill.fillAmount = maxAmount > 0f ? amount / maxAmount : 0f;
        }

        if (_initialized) productionRateText.color = productionRate < 0 ? Color.red : originalProductionRateColor;
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Resource(this, trigger, data);
}
