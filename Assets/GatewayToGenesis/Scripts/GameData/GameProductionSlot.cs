using System.Collections;
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

    // Dictionary to store ProductionUnitData by GameUnit name
    private static Dictionary<string, ProductionUnitData> productionUnitDataDictionary;

    // Start is called before the first frame update
    public void Start()
    {
        if (productionUnitDataDictionary == null)
        {
            InitializeProductionUnitDataDictionary();
        }

        if (gameUnit != null)
        {
            // Try to assign the correct productionUnitData based on the name of the gameUnit
            if (productionUnitDataDictionary.ContainsKey(gameUnit.name))
            {
                productionUnitData = productionUnitDataDictionary[gameUnit.name];
            }
            else
            {
                Debug.LogWarning($"No ProductionUnitData found for {gameUnit.name}");
            }
        }

        InitialiseProductionUnit(gameUnit);
    }

    void Update()
    {
        if (gameUnit != null && productionUnitData != null)
        {
            UpdateMaxAmount();
        }
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

            //ADD HERE INSUFFICIENT AMOUNT EVENT TRIGGER
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

    private void InitializeProductionUnitDataDictionary()
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
                Debug.LogWarning($"Duplicate ProductionUnitData found for {unitData.name}. Skipping.");
            }
        }
    }

}

