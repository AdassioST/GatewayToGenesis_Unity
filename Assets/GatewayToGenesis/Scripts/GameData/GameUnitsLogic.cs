using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameUnitsLogic : MonoBehaviour
{
    public static GameUnitsLogic Instance { get; private set; }

    [SerializeField] private TabBuilderLogic storageTab;
    [SerializeField] private TabBuilderLogic productionTab;
    [SerializeField] private TabBuilderLogic researchTab;

    private GlobalProductionManager global;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameUnitsLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }
    private void Start()
    {
        global = GetComponent<GlobalProductionManager>();
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

        if (Input.GetKeyDown("r"))
        {
            ChangeResourceFromName("Research", 100, false);
        }

        if (Input.GetKeyDown("t"))
        {
            UnlockTechnologyWithRequirements("Reconstruction");
        }

        if (Input.GetKeyDown("g"))
        {
            UnlockTechnologyWithRequirements("Woodcraft Mastery");
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

    public bool CanUnlockTechnology(string technologyName)
    {
        GameObject technologySlotObj = researchTab.slots.Find(slot => slot.name == technologyName);
        GameTechnologySlot technologySlot = technologySlotObj.GetComponent<GameTechnologySlot>();

        TechnologyData technologyData = technologySlot.technologyData;

        // Check if tech lock prerequisites are met
        foreach (string requiredTech in technologyData.techRequirements)
        {
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            if (requiredTechObj == null || !requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked)
            {
                Debug.LogWarning($"Prerequisite technology {requiredTech} not unlocked.");
                return false;
            }
        }

        // Check if enough resources are available
        for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
        {
            string resourceName = technologyData.resourceRequirements[i];
            float requiredAmount = technologyData.resourceAmount[i];

            GameObject resourceSlotObj = storageTab.slots.Find(slot => slot.name == resourceName);
            GameResourceSlot resourceSlot = resourceSlotObj.GetComponent<GameResourceSlot>();

            if (resourceSlot == null || resourceSlot.amount < requiredAmount)
            {
                Debug.LogWarning($"Not enough {resourceName} to unlock {technologyName}.");
                return false;
            }
        }

        return true;
    }

    public bool UnlockTechnologyWithRequirements(string technologyName)
    {
        GameObject technologySlotObj = researchTab.slots.Find(slot => slot.name == technologyName);
        GameTechnologySlot technologySlot = technologySlotObj.GetComponent<GameTechnologySlot>();

        if (!CanUnlockTechnology(technologyName) || technologySlot.isUnlocked)
        {
            return false;
        }

        TechnologyData technologyData = technologySlot.technologyData;

        // Deduct resources
        for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
        {
            string resourceName = technologyData.resourceRequirements[i];
            float requiredAmount = technologyData.resourceAmount[i];

            ChangeResourceFromName(resourceName, -requiredAmount, false);
        }

        technologySlot.UnlockTechnology();
        return true;
    }

    public void HandleTechUnlockable(TechUnlockable unlockable)
    {
        switch (unlockable.unlockableType)
        {
            case TechUnlockableType.ClickPower:

                storageTab.AddNewUnit(unlockable.gameUnit);
                Debug.Log($"ClickPower unit {unlockable.gameUnit.name} has been added to Storage.");
                break;

            case TechUnlockableType.Building:

                productionTab.AddNewUnit(unlockable.gameUnit);
                Debug.Log($"Building unit {unlockable.gameUnit.name} has been added to Production.");
                break;

            case TechUnlockableType.Unit:

                productionTab.AddNewUnit(unlockable.gameUnit);
                Debug.Log($"Unit {unlockable.gameUnit.name} has been added to Production.");
                break;

            case TechUnlockableType.Modifier:

                Debug.Log($"Modifier {unlockable.gameUnit.name} has been unlocked.");
                break;

            case TechUnlockableType.Arts:

                Debug.Log($"Arts unit {unlockable.gameUnit.name} has been unlocked.");
                break;

            case TechUnlockableType.Special:

                Debug.Log($"Special unlockable {unlockable.gameUnit.name} has been unlocked.");
                break;

            default:
                Debug.LogWarning($"Unknown unlockable type: {unlockable.unlockableType}");
                break;
        }
    }


}

