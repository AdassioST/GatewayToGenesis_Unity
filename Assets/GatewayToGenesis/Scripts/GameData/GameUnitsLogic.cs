using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class GameUnitsLogic : MonoBehaviour
{

    [Header("Game Units Logic Settings")]
    [SerializeField] private bool enableGameUnitsLogicLogging; // Whether to log what the boss is doing

    public static GameUnitsLogic Instance { get; private set; }

    [SerializeField] public TabBuilderLogic storageTab;
    [SerializeField] public TabBuilderLogic productionTab;
    [SerializeField] public TabBuilderLogic researchTab;

    public Dictionary<GameTechnologySlot, Dictionary<string, float>> technologyProgress = new();
    // Cache of adjusted base costs per technology slot (after Discovery Efficiency integer-rounded reduction)
    private Dictionary<GameTechnologySlot, List<float>> adjustedTechCosts = new();

    public GameTechnologySlot activeTechnologySlot;

    public bool switchedTechnologies;
    private Coroutine activeTechnologySlotCoroutine;

    [SerializeField] private GameObject HUD, expandibleHUD, buildingMaterialButton;

    public Dictionary<string, Dictionary<string, float>> storageBreakdown = new Dictionary<string, Dictionary<string, float>>();

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
        // Ensure all resource slots have proper click power values
        StartCoroutine(ValidateClickPowerOnStart());
    }
    
    /// <summary>
    /// Validates all resource click power values after system initialization
    /// </summary>
    private IEnumerator ValidateClickPowerOnStart()
    {
        // Wait for all systems to be ready
        yield return new WaitForSeconds(0.1f);
        
        // Ensure all resources have at least their base click power
        EnsureAllResourceClickPowerMinimums();
        
        if (enableGameUnitsLogicLogging)
        {
            Debug.Log("[GameUnitsLogic] Validated all resource click power minimums on startup");
        }
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

            // Store old amount for food change notification
            float oldAmount = 0f;
            bool isFoodResource = string.Equals(name, "Food", System.StringComparison.OrdinalIgnoreCase);
            if (isFoodResource)
            {
                oldAmount = slot.GetComponent<GameResourceSlot>().amount;
            }

            slot.GetComponent<ProductionLogic>().ChangeUnitAmount(amount);

            // Notify PopGrowthLogic of food changes for immediate population processing
            if (isFoodResource && PopGrowthLogic.Instance != null)
            {
                float newAmount = slot.GetComponent<GameResourceSlot>().amount;
                PopGrowthLogic.Instance.HandleExternalFoodChange(oldAmount, newAmount);
            }

            TooltipSystemLogic.Instance.RefreshAllTooltips();
        }
        else
        {
            // Try to find the GameUnit in the existing system (same approach as TechUnlockables)
            GameUnit missingResource = FindGameUnitByName(name);
            
            if (missingResource != null)
            {
                // Add the new resource to the storage tab
                storageTab.AddNewUnit(missingResource);
                
                // Now try to change the resource amount again
                ChangeResourceFromName(name, amount, changeFromClickPower);
            }
            else
            {
                 Debug.LogWarning($"Resource {name} is not in the resource storage slots and could not be found in the system. Make sure the GameUnit asset exists in Assets/GatewayToGenesis/Scripts/GameData/GameObjects/Resources/");
            }
        }
    }

    public void ChangeProductionUnitFromName(string name, float amount)
    {
        bool isUnitPresent = productionTab.slots.Any(r => r.name == name);

        if (isUnitPresent)
        {
            GameObject slot = productionTab.slots.Find((x) => x.name == name);

            slot.GetComponent<ProductionLogic>().ChangeUnitAmount(amount);

            TooltipSystemLogic.Instance.RefreshAllTooltips();
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
                productionSlot.incrementalCost *= Mathf.Exp((GlobalProductionManager.Instance.costBalance / GlobalProductionManager.Instance.techTier) * productionSlot.maxAmount);

                float incrementalCost = productionSlot.CalculateIncrementalCost(resourceName, productionSlot.maxAmount);

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
                productionSlot.incrementalCost *= Mathf.Exp((GlobalProductionManager.Instance.costBalance / GlobalProductionManager.Instance.techTier) * productionSlot.maxAmount);
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

                if (!storageBreakdown.ContainsKey(storageResource))
                {
                    storageBreakdown[storageResource] = new Dictionary<string, float>{{ "Base", resourceSlot.maxAmount }};
                }

                resourceSlot.maxAmount += storageIncrease;

                resourceSlot.RefreshProductionAmount();

                if (!storageBreakdown[storageResource].ContainsKey(productionUnitName))
                {
                    storageBreakdown[storageResource][productionUnitName] = 0;
                }

                storageBreakdown[storageResource][productionUnitName] += storageIncrease;
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

        // Use adjusted base costs once, with integer-rounded DE reduction
        var adjustedRequired = GetAdjustedTechCosts(technologySlot);

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
            float requiredAmount = adjustedRequired.Count > i ? adjustedRequired[i] : technologyData.resourceAmount[i];

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
            return;
        }

        foreach (string requiredTech in technologySlot.technologyData.techRequirements)
        {
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            if (requiredTechObj == null || !requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked)
            {
                return;
            }
        }

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

        if (!technologyProgress.ContainsKey(technologySlot))
        {
            technologyProgress[technologySlot] = technologySlot.technologyData.resourceRequirements.ToDictionary(resource => resource, _ => 0f);
        }

        // Prime adjusted costs cache for this slot
        GetAdjustedTechCosts(technologySlot);

        technologySlot.alreadyClicked = true;

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
        var adjustedRequired = GetAdjustedTechCosts(technologySlot);

        while (!technologySlot.isUnlocked)
        {
            // Gate research ticks by time system state
            if (TimeSystemLogic.Instance != null && TimeSystemLogic.Instance.isTimePaused)
            {
                yield return null;
                continue;
            }
            if (switchedTechnologies)
            {
                yield break;
            }

            bool allResourcesComplete = true;

            for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
            {
                var resourceName = technologyData.resourceRequirements[i];
                var requiredAmount = adjustedRequired.Count > i ? adjustedRequired[i] : technologyData.resourceAmount[i];
                var processedAmount = resourceProgress[resourceName];

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

            // Apply Enlightened bonus once, as a proportion of total cost
            if (technologySlot.enlightenedCompleted && !technologySlot.enlightenedBonusApplied && technologySlot.enlightenedBonusPercent > 0f)
            {
                float totalRequired = adjustedRequired.Sum();
                if (totalRequired > 0f)
                {
                    float bonusAmount = totalRequired * Mathf.Clamp01(technologySlot.enlightenedBonusPercent);
                    // Distribute the bonus across resources proportionally to their requirements
                    for (int i = 0; i < technologyData.resourceRequirements.Count; i++)
                    {
                        var required = adjustedRequired.Count > i ? adjustedRequired[i] : technologyData.resourceAmount[i];
                        if (required <= 0f) continue;
                        float share = bonusAmount * (required / totalRequired);
                        resourceProgress[technologyData.resourceRequirements[i]] = Mathf.Min(required, resourceProgress[technologyData.resourceRequirements[i]] + share);
                    }
                    technologySlot.enlightenedBonusApplied = true;
                }
            }

            // Use adjusted total cost for progress to match tooltip and unlock thresholds
            float adjustedTotal = Mathf.Max(0.0001f, adjustedRequired.Sum());
            technologySlot.researchProgress = Mathf.Clamp01(resourceProgress.Values.Sum() / adjustedTotal);
            technologySlot.UpdateProgressUI();

            if (allResourcesComplete)
            {
                technologySlot.UnlockTechnology();
                technologyProgress.Remove(technologySlot);
                adjustedTechCosts.Remove(technologySlot);
                activeTechnologySlot = null;
                activeTechnologySlotCoroutine = null;

                // Ensure any visible tooltips refresh to show 0 remaining and green text post-unlock
                if (TooltipSystemLogic.Instance != null)
                {
                    TooltipSystemLogic.Instance.RefreshAllTooltips();
                }

                yield break;
            }

            yield return new WaitForSeconds(1.0f);
        }

        activeTechnologySlotCoroutine = null;
    }

    // Compute adjusted base costs for a technology slot once, applying Discovery Efficiency
    // Reduction is rounded to an integer, and capped via StatManager (centralized)
    public List<float> GetAdjustedTechCosts(GameTechnologySlot techSlot)
    {
        if (techSlot == null || techSlot.technologyData == null)
        {
            return new List<float>();
        }
        if (adjustedTechCosts.TryGetValue(techSlot, out var cached))
        {
            return cached;
        }

        var data = techSlot.technologyData;
        float eff01 = 0f;
        if (StatManager.Instance != null)
        {
            eff01 = StatManager.Instance.GetDiscoveryEfficiency01Capped();
        }

        var adjusted = new List<float>(data.resourceAmount.Count);
        for (int i = 0; i < data.resourceAmount.Count; i++)
        {
            float baseCost = data.resourceAmount[i];
            int reductionInt = Mathf.RoundToInt(baseCost * eff01);
            float newCost = Mathf.Max(0f, baseCost - reductionInt);
            adjusted.Add(newCost);
        }
        // Cache only once research has started for this slot to avoid freezing values from tooltip previews
        if (technologyProgress.ContainsKey(techSlot))
        {
            adjustedTechCosts[techSlot] = adjusted;
        }
        return adjusted;
    }

    public void HandleTechUnlockable(TechUnlockable unlockable, GameTechnologySlot techSlot)
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
                if (enableGameUnitsLogicLogging)
                {
                    Debug.Log($"Unit/Building {unlockable.gameUnit.name} has been added to Production.");
                }
                break;

            case TechUnlockableType.Modifier:

                if (unlockable.name.StartsWith("Food D"))
                {
                    PopGrowthLogic.Instance.demandModifier -= unlockable.resourceModifier / 100;
                }
                else
                {
                    GlobalProductionManager.Instance.AdjustPercentageModifier(unlockable.gameUnit.name, unlockable.resourceModifier, true, true, techSlot.gameUnit.name);
                }
                break;

            case TechUnlockableType.Arts:
                if (enableGameUnitsLogicLogging)
                {
                    Debug.Log($"Arts unit {unlockable.gameUnit.name} has been unlocked.");
                }
                break;

            case TechUnlockableType.Special:

                HandleSpecialUnlockable(unlockable);

                if (enableGameUnitsLogicLogging)
                {
                    Debug.Log($"Special unit {unlockable.name} has been unlocked.");
                }

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
    
    public int GetResourceAmount(string resourceName)
    {
        GameResourceSlot resourceSlot = GetResourceSlotFromName(resourceName);
        if (resourceSlot != null)
        {
            return Mathf.RoundToInt(resourceSlot.amount);
        }
        return 0;
    }

    public List<GameResourceSlot> GetAvailableResources()
    {
        return storageTab.slots.Select(slot => slot.GetComponent<GameResourceSlot>()).ToList();
    }

    // Adjust click power of a single resource by flat amount
    public void AdjustClickPower(string resourceName, float delta)
    {
        var slotObj = storageTab.slots.Find(slot => slot.name == resourceName);
        var resourceSlot = slotObj != null ? slotObj.GetComponent<GameResourceSlot>() : null;
        if (resourceSlot != null)
        {
            resourceSlot.clickPower += delta;
            // Ensure minimum click power is maintained
            resourceSlot.EnsureMinimumClickPower();
        }
    }

    // Adjust click power of a single resource by percent (positive or negative)
    public void AdjustClickPowerPercent(string resourceName, float percent)
    {
        var slotObj = storageTab.slots.Find(slot => slot.name == resourceName);
        var resourceSlot = slotObj != null ? slotObj.GetComponent<GameResourceSlot>() : null;
        if (resourceSlot != null)
        {
            resourceSlot.clickPower *= (1f + percent / 100f);
            // Ensure minimum click power is maintained
            resourceSlot.EnsureMinimumClickPower();
        }
    }

    // Adjust click power for all resources within a section by flat amount
    public void AdjustClickPowerForSection(string sectionName, float delta)
    {
        foreach (var slotGO in storageTab.slots)
        {
            var resSlot = slotGO.GetComponent<GameResourceSlot>();
            if (resSlot != null && resSlot.gameUnit != null && string.Equals(resSlot.gameUnit.section, sectionName, System.StringComparison.OrdinalIgnoreCase))
            {
                resSlot.clickPower += delta;
                // Ensure minimum click power is maintained
                resSlot.EnsureMinimumClickPower();
            }
        }
    }

    // Adjust click power for all resources within a section by percent
    public void AdjustClickPowerPercentForSection(string sectionName, float percent)
    {
        foreach (var slotGO in storageTab.slots)
        {
            var resSlot = slotGO.GetComponent<GameResourceSlot>();
            if (resSlot != null && resSlot.gameUnit != null && string.Equals(resSlot.gameUnit.section, sectionName, System.StringComparison.OrdinalIgnoreCase))
            {
                resSlot.clickPower *= (1f + percent / 100f);
                // Ensure minimum click power is maintained
                resSlot.EnsureMinimumClickPower();
            }
        }
    }
    public List<GameUnit> GetAvailableGameUnits()
    {
        List<GameUnit> availableUnits = new List<GameUnit>();
        
        foreach (GameObject slot in storageTab.slots)
        {
            IGameUnitSlot slotComponent = slot.GetComponent<IGameUnitSlot>();
            if (slotComponent != null && slotComponent.gameUnit != null)
            {
                availableUnits.Add(slotComponent.gameUnit);
            }
        }
        
        return availableUnits;
    }

    /// <summary>
    /// Public accessor that reuses the internal discovery logic to find a GameUnit by name.
    /// Prefer this over duplicating Resources.LoadAll logic elsewhere.
    /// </summary>
    public GameUnit GetGameUnitByName(string unitName)
    {
        return FindGameUnitByName(unitName);
    }

    /// <summary>
    /// Convenience helper to retrieve a GameUnit icon by name (returns null if not found).
    /// </summary>
    public Sprite GetGameUnitIconByName(string unitName)
    {
        var unit = GetGameUnitByName(unitName);
        return unit != null ? unit.icon : null;
    }

    /// <summary>
    /// Ensures all resource slots have at least their base click power
    /// </summary>
    public void EnsureAllResourceClickPowerMinimums()
    {
        foreach (var slotGO in storageTab.slots)
        {
            var resSlot = slotGO.GetComponent<GameResourceSlot>();
            if (resSlot != null)
            {
                resSlot.EnsureMinimumClickPower();
            }
        }
    }

    /// <summary>
    /// Resets all resource slots' click power to their base values
    /// </summary>
    public void ResetAllResourceClickPowerToBase()
    {
        foreach (var slotGO in storageTab.slots)
        {
            var resSlot = slotGO.GetComponent<GameResourceSlot>();
            if (resSlot != null)
            {
                resSlot.ResetClickPowerToBase();
            }
        }
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

                if(HUD.activeSelf) TimeSystemLogic.Instance.PauseTime(false);

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
        string[] units = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "O", "N", "D", "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Od", "Nd", "V", "Uv", "Dv", "Tv", "Qav", "Qiv", "Sxv", "Spv", "Ov", "Nv", "Tr", "Ut", "Dt", "G"};
        int unitIndex = 0;

        while (value >= 1000f && unitIndex < units.Length - 1)
        {
            value /= 1000f;
            unitIndex++;
        }

        return $"{value:0.##}{units[unitIndex]}";
    }
    
    /// <summary>
    /// Find a GameUnit by name from the existing system
    /// </summary>
    private GameUnit FindGameUnitByName(string unitName)
    {
        // First check if it's already in any of our tabs
        foreach (var unit in storageTab.units)
        {
            if (unit.name == unitName) return unit;
        }
        
        foreach (var unit in productionTab.units)
        {
            if (unit.name == unitName) return unit;
        }
        
        foreach (var unit in researchTab.units)
        {
            if (unit.name == unitName) return unit;
        }
        
        // If not found in tabs, try to find it in the Resources folder
        // This is the same approach used by TechUnlockables
        GameUnit[] allGameUnits = Resources.LoadAll<GameUnit>("");
        foreach (var unit in allGameUnits)
        {
            if (unit.name == unitName) return unit;
        }
        
        return null;
    }
}