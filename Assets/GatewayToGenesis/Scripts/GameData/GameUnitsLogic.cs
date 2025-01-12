using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameUnitsLogic : MonoBehaviour
{
    public static GameUnitsLogic Instance { get; private set; }

    [SerializeField] public TabBuilderLogic storageTab;
    [SerializeField] public TabBuilderLogic productionTab;
    [SerializeField] public TabBuilderLogic researchTab;

    private GlobalProductionManager global;

    private Dictionary<GameTechnologySlot, Dictionary<string, float>> technologyProgress = new();

    public GameTechnologySlot activeTechnologySlot;

    public bool switchedTechnologies;
    private Coroutine activeTechnologySlotCoroutine;


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

            if (!isUnit)
            {
                requiredAmount *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }

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
        if (!CanBuildProductionUnit(productionUnitName)) return false;

        GameObject productionSlotObj = productionTab.slots.Find(slot => slot.name == productionUnitName);
        GameProductionSlot productionSlot = productionSlotObj.GetComponent<GameProductionSlot>();

        ProductionUnitData productionUnitData = productionSlot.productionUnitData;

        bool isUnit = productionSlot.gameUnit.type == "Unit";

        for (int i = 0; i < productionUnitData.buildResourceRequirements.Count; i++)
        {
            string resourceName = productionUnitData.buildResourceRequirements[i];
            float baseCost = productionUnitData.buildRequirementsAmount[i];

            if (!isUnit)
            {
                baseCost *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }

            Debug.Log($"Building {productionUnitName}: Deducting {baseCost} from {resourceName}");
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

        if (technologyData == null) return false;

        foreach (string requiredTech in technologyData.techRequirements)
        {
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            if (requiredTechObj == null || !requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked)
            {
                return false;
            }
        }

        for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
        {
            string resourceName = technologyData.resourceRequirements[i];
            float requiredAmount = technologyData.resourceAmount[i];

            GameObject resourceSlotObj = storageTab.slots.Find(slot => slot.name == resourceName);
            GameResourceSlot resourceSlot = resourceSlotObj.GetComponent<GameResourceSlot>();

            if (resourceSlot == null || resourceSlot.amount < requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    public void StartTechnologyProgress(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null || technologySlot.isUnlocked || technologySlot.technologyData == null)
        {
            return; // Invalid or already unlocked slot
        }

        // Check if all required technologies are unlocked
        foreach (string requiredTech in technologySlot.technologyData.techRequirements)
        {
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            if (requiredTechObj == null || !requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked)
            {
                Debug.LogWarning($"Cannot start research on {technologySlot.name} because required technologies are not unlocked.");
                return;
            }
        }

        // If switching from a different technology, pause the current one
        if (activeTechnologySlot != null && activeTechnologySlot != technologySlot)
        {
            activeTechnologySlot.alreadyClicked = false;

            switchedTechnologies = true;

            if (activeTechnologySlotCoroutine != null)
            {
                StopCoroutine(activeTechnologySlotCoroutine);
            }

            Debug.Log($"Switched from {activeTechnologySlot.name} to {technologySlot.name}");
        }

        activeTechnologySlot = technologySlot;
        switchedTechnologies = false;

        // Progress tracking
        if (!technologyProgress.ContainsKey(technologySlot))
        {
            technologyProgress[technologySlot] = technologySlot.technologyData.resourceRequirements.ToDictionary(resource => resource, _ => 0f);
        }

        technologySlot.alreadyClicked = true;

        // Anti stacking check
        if (activeTechnologySlotCoroutine != null)
        {
            StopCoroutine(activeTechnologySlotCoroutine); 
        }
        activeTechnologySlotCoroutine = StartCoroutine(ProcessTechnologyProgress(technologySlot));
    }

    private IEnumerator ProcessTechnologyProgress(GameTechnologySlot technologySlot)
    {
        var technologyData = technologySlot.technologyData;
        var resourceProgress = technologyProgress[technologySlot];

        while (!technologySlot.isUnlocked)
        {
            // Exit if switching or pausing
            if (switchedTechnologies)
            {
                Debug.Log($"Progress interrupted for {technologySlot.name}");
                yield break;
            }

            bool allResourcesComplete = true;

            for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
            {
                var resourceName = technologyData.resourceRequirements[i];
                var requiredAmount = technologyData.resourceAmount[i];
                var processedAmount = resourceProgress[resourceName];

                // Calculate remaining and processable amounts
                var remainingAmount = requiredAmount - processedAmount;
                if (remainingAmount > 0)
                {
                    GameObject resourceSlotObj = storageTab.slots.Find(slot => slot.name == resourceName);
                    var resourceSlot = resourceSlotObj?.GetComponent<GameResourceSlot>();

                    if (resourceSlot == null) continue;

                    var availableAmount = Mathf.Min(resourceSlot.amount, remainingAmount);
                    var amountToProcess = Mathf.Min(availableAmount, requiredAmount * 0.1f);

                    resourceSlot.amount -= amountToProcess;
                    resourceProgress[resourceName] += amountToProcess;
                }

                if (resourceProgress[resourceName] < requiredAmount)
                {
                    allResourcesComplete = false;
                }
            }

            // Update progress and UI
            technologySlot.researchProgress = resourceProgress.Values.Sum() / technologyData.resourceAmount.Sum();
            technologySlot.UpdateProgressUI();

            if (allResourcesComplete)
            {
                technologySlot.UnlockTechnology();
                technologyProgress.Remove(technologySlot); // Clear progress tracking
                activeTechnologySlot = null; // Reset active slot
                activeTechnologySlotCoroutine = null; // Reset coroutine reference
                Debug.Log($"{technologySlot.name} unlocked.");
                yield break;
            }

            yield return new WaitForSeconds(1.0f);
        }

        // Clean up coroutine reference when finished
        activeTechnologySlotCoroutine = null;
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
            case TechUnlockableType.Unit:
                productionTab.AddNewUnit(unlockable.gameUnit);
                Debug.Log($"Unit/Building {unlockable.gameUnit.name} has been added to Production.");
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