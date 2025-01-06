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

    //VARIABLES

    public float productionRate = 0, maxStorage = 1, baseClickPower = 1f;

    public Image icon, fill;

    public TMP_Text amountText, productionRateText;

    public void Start()
    {
        InitialiseResource(gameUnit);
    }
    public void InitialiseResource(GameUnit newResource)
    {
        gameUnit = newResource;
        icon.sprite = newResource.icon;
        clickPower = baseClickPower;

        RefreshProductionAmount();
    }

    public void RefreshProductionAmount()
    {
        amountText.text = amount.ToString();
        productionRateText.text = productionRate.ToString() + "/s";

        fill.fillAmount = amount / maxStorage;
    }

}
