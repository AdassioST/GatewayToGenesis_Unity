using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameResourceSlot : MonoBehaviour, IGameUnitSlot
{
    //INTERFACES
    public GameUnit gameUnit { get; set; }
    
    // Click power with validation - cannot go below base value
    private float _clickPower = 1.0f;
    public float clickPower 
    { 
        get => _clickPower;
        set => _clickPower = ValidateClickPower(value);
    }
    
    public float amount { get; set; }
    public float maxAmount { get; set; } = 200f;

    //VARIABLES
    public float productionRate = 0, baseClickPower = 1f;

    public Image icon, fill;

    public TMP_Text amountText, productionRateText;

    private Color originalProductionRateColor;

    /// <summary>
    /// Validates click power to ensure it never goes below base value
    /// </summary>
    private float ValidateClickPower(float newValue)
    {
        // Ensure click power never goes below base value
        float minAllowed = Mathf.Max(0.001f, baseClickPower); // Prevent 0 click power
        return Mathf.Max(minAllowed, newValue);
    }

    /// <summary>
    /// Ensures current click power is at least at base value
    /// </summary>
    public void EnsureMinimumClickPower()
    {
        if (_clickPower < baseClickPower)
        {
            _clickPower = ValidateClickPower(baseClickPower);
        }
    }

    /// <summary>
    /// Resets click power to base value
    /// </summary>
    public void ResetClickPowerToBase()
    {
        _clickPower = ValidateClickPower(baseClickPower);
    }

    public void Start()
    {
        InitialiseResource(gameUnit);
    }
    
    public void InitialiseResource(GameUnit newResource)
    {
        gameUnit = newResource;
        icon.sprite = newResource.icon;
        clickPower = baseClickPower; // This will now use the validated setter

        originalProductionRateColor = productionRateText.color;

        RefreshProductionAmount();
    }

    public void RefreshProductionAmount()
    {
        amountText.text = GameUnitsLogic.Instance.FormatValue(amount);
        productionRateText.text = GameUnitsLogic.Instance.FormatValue(productionRate) + "/s";

        // For Food, visualize progress toward growth threshold rather than storage cap
        if (string.Equals(gameUnit.name, "Food", System.StringComparison.OrdinalIgnoreCase) && PopGrowthLogic.Instance != null)
        {
            float threshold = Mathf.Max(1f, PopGrowthLogic.Instance.foodThreshold);
            fill.fillAmount = Mathf.Clamp01(amount / threshold);
        }
        else
        {
            fill.fillAmount = amount / maxAmount;
        }

        if (productionRate < 0)
        {
            productionRateText.color = Color.red;
        }
        else
        {
            productionRateText.color = originalProductionRateColor;
        }
    }

}
