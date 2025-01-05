using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameProductionSlot : MonoBehaviour, IGameUnitSlot
{
    //INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 0.0f;

    //VARIABLES

    public float amount;

    public bool isUnique;

    public List<string> producedResources = new List<string>(), inputResources = new List<string>(), buildResourceRequirements = new List<string>();

    public List<float> productionRates = new List<float>(), inputRates = new List<float>(), buildRequirementsAmount = new List<float>();

    public Image icon;

    public TMP_Text amountText, nameText, typeText;

    public void Start()
    {
        InitialiseProductionUnit(gameUnit);
    }

    public void InitialiseProductionUnit(GameUnit newProductionUnit)
    {
        gameUnit = newProductionUnit;
        icon.sprite = newProductionUnit.icon;

        RefreshProductionAmount();
    }

    public void RefreshProductionAmount()
    {
        amountText.text = amount.ToString();

        nameText.text = gameUnit.name.ToString();
        typeText.text = gameUnit.type.ToString();
    }

}
