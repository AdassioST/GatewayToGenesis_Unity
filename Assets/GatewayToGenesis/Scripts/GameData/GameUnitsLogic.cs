using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameUnitsLogic : MonoBehaviour
{
    [SerializeField] private TabBuilderLogic storageTab;
    [SerializeField] private TabBuilderLogic productionTab;
    [SerializeField] private TabBuilderLogic researchTab;

    private GlobalProductionManager global;

    private void Start()
    {
        global = GetComponent<GlobalProductionManager>();
        Debug.Log(global);
    }

    public void ChangeResourceFromName(string name, float amount, bool changeFromClickPower)
    {
        bool isUnitPresent = storageTab.slots.Any(r => r.name == name);

        if (isUnitPresent)
        {
            GameObject slot = storageTab.slots.Find((x) => x.name == name);

            if (changeFromClickPower)
            {
                amount = slot.GetComponent<GameResourceSlot>().clickPower;
            }

            slot.GetComponent<ProductionLogic>().ChangeUnitAmount(amount);
        }
        else
        {
            Debug.LogWarning($"Resource {name} is not in the resource storage slots");
        }

    }

    public void ChangeProductionUnitFromName(string name, float amount)
    {
        bool isUnitPresent = productionTab.slots.Any(r => r.name == name);

        if (isUnitPresent)
        {
            GameObject slot = productionTab.slots.Find((x) => x.name == name);

            slot.GetComponent<ProductionLogic>().ChangeUnitAmount(amount);
        }
        else
        {
            Debug.LogWarning($"Production Unit {name} is not in the production storage slots");
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("s"))
        {
            ChangeResourceFromName("Elderwood", 3, false);
        }

        if (Input.GetKeyDown("g"))
        {
            ChangeResourceFromName("Elderwood", -2, false);
            ChangeProductionUnitFromName("Ancient Windmill", -5);
        }

        if (Input.GetKeyDown("b"))
        {
            BuildProductionUnit("Ancient Windmill");
        }
    }

    public bool CanBuildProductionUnit(string productionUnitName)
    {
        GameObject productionSlotObj = productionTab.slots.Find(slot => slot.name == productionUnitName);
        GameProductionSlot productionSlot = productionSlotObj.GetComponent<GameProductionSlot>();

        ProductionUnitData productionUnitData = productionSlot.productionUnitData;

        bool isUnit = productionSlot.gameUnit.type == "Unit";


        for (int i = 0; i < productionUnitData.buildResourceRequirements.Count; i++)
        {
            string resourceName = productionUnitData.buildResourceRequirements[i];
            float requiredAmount = productionUnitData.buildRequirementsAmount[i];

            // Calculate the cost creep only for buildings
            if (!isUnit)
            {
                requiredAmount *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }

            // Find the corresponding resource slot in storage
            GameObject resourceSlotObj = storageTab.slots.Find(slot => slot.name == resourceName);
            GameResourceSlot resourceSlot = resourceSlotObj.GetComponent<GameResourceSlot>();

            if (resourceSlot == null || resourceSlot.amount < requiredAmount)
            {
                return false;
            }
        }

        return true;
    }
    public bool BuildProductionUnit(string productionUnitName)
    {
        if (!CanBuildProductionUnit(productionUnitName))
        {
            return false;
        }

        GameObject productionSlotObj = productionTab.slots.Find(slot => slot.name == productionUnitName);
        GameProductionSlot productionSlot = productionSlotObj.GetComponent<GameProductionSlot>();

        ProductionUnitData productionUnitData = productionSlot.productionUnitData;

        bool isUnit = productionSlot.gameUnit.type == "Unit";

        for (int i = 0; i < productionUnitData.buildResourceRequirements.Count; i++)
        {
            string resourceName = productionUnitData.buildResourceRequirements[i];
            float baseCost = productionUnitData.buildRequirementsAmount[i];

            // Calculate the cost creep only for buildings
            if (!isUnit)
            {
                baseCost *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }

            Debug.Log($"Building {productionUnitName}: Deducting {baseCost} from {resourceName} (Available: {storageTab.slots.Find(slot => slot.name == resourceName).GetComponent<GameResourceSlot>().amount})");


            ChangeResourceFromName(resourceName, -baseCost, false);
        }

        ChangeProductionUnitFromName(productionUnitName, 1);
        return true;
    }
}

