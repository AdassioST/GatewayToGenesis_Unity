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

    [SerializeField] private GameObject expandibleHUD, buildingMaterialButton;

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

            TooltipSystemLogic.Instance.RefreshAllTooltips();
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
            productionSlot.incrementalCost = productionUnitData.buildRequirementsAmount[i];

            if (!isUnit)
            {
                productionSlot.incrementalCost *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }

            GameResourceSlot resourceSlot = GetResourceSlotFromName(resourceName);

            if (resourceSlot == null || resourceSlot.amount < productionSlot.incrementalCost)
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
            productionSlot.incrementalCost = productionUnitData.buildRequirementsAmount[i];

            if (!isUnit)
            {
                productionSlot.incrementalCost *= Mathf.Exp((global.costBalance / global.techTier) * productionSlot.amount);
            }
            ChangeResourceFromName(resourceName, -productionSlot.incrementalCost, false);
        }

        if (productionUnitData.housing > 0)
        {
            PopGrowthLogic.Instance.housing += productionUnitData.housing;
        }

        if (productionUnitData.storageResources != null && productionUnitData.storageResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.storageResources.Count; i++)
            {
                string storageResource = productionUnitData.storageResources[i];
                float storageIncrease = productionUnitData.storageAmount[i];

                GameResourceSlot resourceSlot = GetResourceSlotFromName(storageResource);

                resourceSlot.maxAmount += storageIncrease;

                resourceSlot.RefreshProductionAmount();
            }
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

            GameResourceSlot resourceSlot = GetResourceSlotFromName(resourceName);

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
                    GameResourceSlot resourceSlot = GetResourceSlotFromName(resourceName);

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

                if(unlockable.resourceModifier > 0)
                {
                    GameResourceSlot resourceSlot = GetResourceSlotFromName(unlockable.gameUnit.name);

                    resourceSlot.clickPower += unlockable.resourceModifier;
                }
                else
                {
                    storageTab.AddNewUnit(unlockable.gameUnit);
                }

                break;

            case TechUnlockableType.Building:
            case TechUnlockableType.Unit:
                productionTab.AddNewUnit(unlockable.gameUnit);
                Debug.Log($"Unit/Building {unlockable.gameUnit.name} has been added to Production.");
                break;

            case TechUnlockableType.Modifier:

                if (unlockable.name.StartsWith("Food D"))
                {
                    PopGrowthLogic.Instance.demandModifier -= unlockable.resourceModifier / 100;
                }
                else
                {
                    GlobalProductionManager.Instance.AdjustPercentageModifier(unlockable.gameUnit.name, unlockable.resourceModifier, true, true);
                }
                break;

            case TechUnlockableType.Arts:
                Debug.Log($"Arts unit {unlockable.gameUnit.name} has been unlocked.");
                break;

            case TechUnlockableType.Special:

                HandleSpecialUnlockable(unlockable);

                Debug.Log($"Special unit {unlockable.name} has been unlocked.");

                break;

            default:
                Debug.LogWarning($"Unknown unlockable type: {unlockable.unlockableType}");
                break;
        }
    }
    public GameResourceSlot GetResourceSlotFromName(string name)
    {
        GameObject resourceSlotObj = storageTab.slots.Find(slot => slot.name == name);
        return resourceSlotObj?.GetComponent<GameResourceSlot>();
    }

    public List<GameResourceSlot> GetAvailableResources()
    {
        return storageTab.slots.Select(slot => slot.GetComponent<GameResourceSlot>()).ToList();
    }

    private void HandleSpecialUnlockable(TechUnlockable unlockable)
    {
        switch (unlockable.name)
        {
            case "Vagrants":

                PopGrowthLogic.Instance.allowVagrants = true;

                break;

            case "Horology":
                TimeSystemLogic.Instance.canTrackTime = true;

                expandibleHUD.SetActive(true);
                break;


            case "Building Material Button":

                buildingMaterialButton.SetActive(true);
                break;

            default:

            Debug.LogWarning($"Unknown special type effects for: {unlockable.unlockableType}");
            break;
        }
    }
    public string FormatValue(float value)
    {
        string[] units = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "O", "N", "D", "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Od", "Nd", "V", "Uv", "Dv", "Tv", "Qav", "Qiv", "Sxv", "Spv", "Ov", "Nv", "Tr", "Ut", "Dt", "G"}; // Units for Thousand, Million, Billion, Trillion, up to Googol
        int unitIndex = 0;

        // Reduce the value and increment the unit index until it's in the desired range
        while (value >= 1000f && unitIndex < units.Length - 1)
        {
            value /= 1000f;
            unitIndex++;
        }

        return $"{value:0.##}{units[unitIndex]}";
    }
}