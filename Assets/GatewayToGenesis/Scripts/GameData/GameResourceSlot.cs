using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameResourceSlot : MonoBehaviour, IGameUnitSlot
{
    //INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 1.0f;
    public float amount { get; set; }
    public float maxAmount { get; set; } = 200f;

    //VARIABLES

    public float productionRate = 0, baseClickPower = 1f;

    public Image icon, fill;

    public TMP_Text amountText, productionRateText;

    private Color originalProductionRateColor;

    public void Start()
    {
        InitialiseResource(gameUnit);
    }
    public void InitialiseResource(GameUnit newResource)
    {
        gameUnit = newResource;
        icon.sprite = newResource.icon;
        clickPower = baseClickPower;

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
