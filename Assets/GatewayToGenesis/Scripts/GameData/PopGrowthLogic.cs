using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PopGrowthLogic : MonoBehaviour
{
    [Header("Pop Growth Logic Settings")]
    [SerializeField] private bool enablePopGrowthLogicLogging; // Whether to log what the boss is doing

    public static PopGrowthLogic Instance { get; private set; }

    public float foodThreshold = 12f, vagrantToPopulationRate = 1f, researchPerPopulation = 1f, starvationRecoveryRate = 0.05f;

    public float foodDemandBuffer = -2.5f, demandRateConstant = 0.03f, sustainabilityTier = 1f, demandModifier = 1.0f;

    [Header("Population Management")]
    public int housing, population, vagrants, freeHousing, deaths, vagrantDeaths, trueDeaths;

    [SerializeField] private GameUnit research, food;

    private GameUnitsLogic unitsLogic;

    private GlobalCharacterManager globalCharacterManager;


    public TMP_Text foodText, freeHousingText, populationText, vagrantsText, foodStateText;

    private float lastResearchModifier, lastFoodModifier, foodDemand;

    public bool allowVagrants, isFoodScarce;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate PopGrowthLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        unitsLogic = GameUnitsLogic.Instance;

        globalCharacterManager = GlobalCharacterManager.Instance;

        if (unitsLogic == null || unitsLogic.storageTab == null)
        {
            Debug.LogError("GameUnitsLogic or storageTab not found! Ensure dependencies are set up correctly.");
            enabled = false;
        }

        StartCoroutine(InitializePopulationResources());

        InvokeRepeating("ProcessPopulationChanges", 1f, 1f); // Repeats every 1 second

    }

    private void Update()
    {
        // Pause population changes during active events
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive())
            return;
            
        ManagePopulationGrowth();
        UpdateFoodDemand();

        RefreshHUD();
    }

    private void ManagePopulationGrowth()
    {
        freeHousing = housing - population;

        // Morale-adjusted threshold: base threshold scaled by morale delta percent
        float adjustedThreshold = GetMoraleAdjustedFoodThreshold();

        // Consume food and grow population or vagrants based on the allowVagrants flag
        float currentFood = GetResourceSlotAmount("Food");
        if (currentFood + 1e-3f >= adjustedThreshold) // epsilon guard for float precision
        {
            // Only remove food threshold if population is growing or vagrants are allowed
            if (freeHousing > 0 || allowVagrants)
            {
                // Withdraw directly from slot to avoid clamping against Food.maxAmount and ensure exact subtraction
                GameResourceSlot foodSlot = unitsLogic.GetResourceSlotFromName("Food");
                if (foodSlot != null)
                {
                    foodSlot.amount = Mathf.Max(0f, foodSlot.amount - adjustedThreshold);
                    foodSlot.RefreshProductionAmount();
                }
                else
                {
                    unitsLogic.ChangeResourceFromName("Food", -adjustedThreshold, false);
                }

                if (freeHousing > 0)
                {
                    population += 1;
                    UpdateResearchGenerationRate();

                    // Spawn new villager when population increases
                    Vector3 spawnPosition = new Vector3(0, 0, 0); // Adjust spawn position as needed
                    globalCharacterManager.SpawnCharacterFromName("Villager");
                }
                else if (allowVagrants)
                {
                    vagrants += 1;
                }
            }
        }
    }

    private void UpdateResearchGenerationRate()
    {
        // Remove the previously applied modifier
        if (lastResearchModifier != 0f)
        {
            GlobalProductionManager.Instance.AdjustResourceModifier("Research", lastResearchModifier, true, false, "Citizen Research");
        }

        float researchAmount = population * researchPerPopulation;

        GlobalProductionManager.Instance.AdjustResourceModifier("Research", researchAmount, true, true, "Citizen Research");

        lastResearchModifier = researchAmount;
    }

    private void UpdateFoodDemand()
    {
        if (lastFoodModifier != 0f)
        {
            GlobalProductionManager.Instance.AdjustResourceModifier("Food", lastFoodModifier, false, false, "Population Food Demand");
        }

        // FOOD DEMAND FORMULA
        foodDemand = (float)(foodDemandBuffer + Mathf.Exp((demandRateConstant / sustainabilityTier) * population)) * demandModifier;

        // Only apply the modifier if foodDemand is negative
        if (foodDemand > 0)
        {
            GlobalProductionManager.Instance.AdjustResourceModifier("Food", foodDemand, false, true, "Population Food Demand");
            lastFoodModifier = foodDemand;
        }
        else
        {
            lastFoodModifier = 0f;
        }
    }

    private IEnumerator InitializePopulationResources()
    {
        yield return null; // Wait one frame

        unitsLogic.storageTab.AddNewUnit(food);
        unitsLogic.storageTab.AddNewUnit(research);

        ChangeFoodMaxThreshold(foodThreshold);

    }
    public void RefreshHUD()
    {
        foodText.text = GameUnitsLogic.Instance.FormatValue(GetResourceSlotAmount("Food"));
        freeHousingText.text = GameUnitsLogic.Instance.FormatValue(freeHousing);

        populationText.text = GameUnitsLogic.Instance.FormatValue(population);
        vagrantsText.text = GameUnitsLogic.Instance.FormatValue(vagrants);

        float netFoodRate = GlobalProductionManager.Instance.GetNetProductionRate("Food");

        float foodRateRatio = (netFoodRate / foodDemand) * 100;
        float deficitRatio = netFoodRate < 0 ? Mathf.Abs((netFoodRate / foodDemand) * 100) : 0;

        // Special case for stagnant production
        if (netFoodRate == 0)
        {
            foodStateText.text = "Stagnant";
        }
        else if (netFoodRate > 0) // Positive net production
        {
            switch (foodRateRatio)
            {
                case > 200:
                    foodStateText.text = "Thriving";
                    break;
                case > 150:
                    foodStateText.text = "Blooming";
                    break;
                default:
                    foodStateText.text = "Ripening";
                    break;
            }
        }
        else // Negative net production (starving categories)
        {
            switch (deficitRatio)
            {
                case > 50:
                    foodStateText.text = "Famine";
                    break;
                case > 30:
                    foodStateText.text = "Withering";
                    break;
                case > 10:
                    foodStateText.text = "Dwindling";
                    break;
                default:
                    foodStateText.text = "Stagnant";
                    break;
            }
        }

    }
    private float GetResourceSlotAmount(string resourceName)
    {
        GameObject slotObject = unitsLogic.storageTab.slots.Find(slot => slot.name == resourceName);
        return slotObject != null ? slotObject.GetComponent<GameResourceSlot>().amount : 0;
    }
    public void ChangeFoodMaxThreshold(float newFoodThreshold)
    {
        GameResourceSlot foodSlot = unitsLogic.storageTab.slots.Find(slot => slot.name == "Food").GetComponent<GameResourceSlot>();

        if (foodSlot != null)
        {
            // Growth threshold is a logical requirement, not a storage cap.
            // Do NOT tie storage max to the growth threshold to avoid blocking growth when threshold > max.
            foodThreshold = newFoodThreshold;
        }
        else
        {
            Debug.LogWarning("Food slot not found.");
        }
    }

    private float GetMoraleAdjustedFoodThreshold()
    {
        // Future: support tiered thresholds by population milestones; for now use current foodThreshold base
        float baseThreshold = foodThreshold;
        int moraleDelta = 0;
        if (StatManager.Instance != null)
        {
            moraleDelta = Mathf.RoundToInt(StatManager.Instance.GetMoraleDeltaPercent());
        }
        // Positive morale lowers threshold, negative morale raises it
        float factor = 1f - (moraleDelta / 100f);
        // Clamp to a reasonable range to avoid zero/negative
        factor = Mathf.Clamp(factor, 0.25f, 2.0f);
        float scaled = baseThreshold * factor;
        // If population size intended to influence base (e.g., after 100 pop, base -> 20), keep layout for future
        return Mathf.Max(1f, scaled);
    }
    
    /// <summary>
    /// Modify population amount from events - only handles REMOVAL (registers deaths)
    /// </summary>
    public void ModifyPopulation(int change)
    {
        if (change >= 0) 
        {
            Debug.LogWarning($"ModifyPopulation called with non-negative value {change}. Population can only be removed. Use ModifyVagrants for adding people.");
            return;
        }
        
        int oldPopulation = population;
        int peopleToRemove = Mathf.Min(-change, population);
        population = Mathf.Max(0, population - peopleToRemove);
        
        // Always register deaths when population is removed
        deaths += peopleToRemove;
        trueDeaths += peopleToRemove;
        
        // Remove characters
        for (int i = 0; i < peopleToRemove; i++)
        {
            if (globalCharacterManager.activeCharacters.Count > 0)
            {
                int randomIndex = Random.Range(0, globalCharacterManager.activeCharacters.Count);
                GameCharacterLogic characterToRemove = globalCharacterManager.activeCharacters[randomIndex];
                globalCharacterManager.RemoveCharacter(characterToRemove);
            }
        }
        
        UpdateResearchGenerationRate();

        if (enablePopGrowthLogicLogging)
        {
            Debug.Log($"Event removed {peopleToRemove} population (registered as deaths). New population: {population}, total deaths: {deaths}");
        }

        RefreshHUD();
    }
    
    /// <summary>
    /// Modify housing amount from events - handles population conversion to vagrants if needed
    /// </summary>
    public void ModifyHousing(int change)
    {
        if (change == 0) return;
        
        int oldHousing = housing;
        housing = Mathf.Max(0, housing + change);
        int actualChange = housing - oldHousing;
        
        if (actualChange < 0)
        {
            // Housing decreased - convert excess population to vagrants
            int housingDeficit = -actualChange;
            int occupiedHousing = Mathf.Min(population, oldHousing);
            int excessPopulation = Mathf.Max(0, occupiedHousing - housing);
            
            if (excessPopulation > 0)
            {
                int populationToConvert = Mathf.Min(excessPopulation, housingDeficit);
                population -= populationToConvert;
                vagrants += populationToConvert;
                
                for (int i = 0; i < populationToConvert; i++)
                {
                    if (globalCharacterManager.activeCharacters.Count > 0)
                    {
                        int randomIndex = Random.Range(0, globalCharacterManager.activeCharacters.Count);
                        GameCharacterLogic characterToRemove = globalCharacterManager.activeCharacters[randomIndex];
                        globalCharacterManager.RemoveCharacter(characterToRemove);
                    }
                }
                
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"Event housing reduction converted {populationToConvert} population to vagrants. New housing: {housing}, population: {population}, vagrants: {vagrants}");
                }
            }
        }
        
        RefreshHUD();
    }
    
    /// <summary>
    /// Modify vagrants amount from events - tracks vagrant deaths when removed
    /// </summary>
    public void ModifyVagrants(int change)
    {
        if (change == 0) return;
        
        if (change < 0)
        {
            // Vagrants are being removed - track as vagrant deaths
            int vagrantsToRemove = Mathf.Min(-change, vagrants);
            vagrants -= vagrantsToRemove;
            vagrantDeaths += vagrantsToRemove;

            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"Event removed {vagrantsToRemove} vagrants (registered as vagrant deaths). New vagrants: {vagrants}, total vagrant deaths: {vagrantDeaths}");
            }
        }
        else
        {
            // Adding vagrants
            vagrants += change;
            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"Event added {change} vagrants. New total: {vagrants}");
            }
        }
        
        RefreshHUD();
    }
    
    /// <summary>
    /// Process deaths from events - handles character removal and death tracking
    /// </summary>
    public void ProcessEventDeaths(int deathCount)
    {
        if (deathCount <= 0) return;
        
        int actualDeaths = Mathf.Min(deathCount, population);
        population -= actualDeaths;
        deaths += actualDeaths;
        trueDeaths += actualDeaths;
        
        // Remove the dead villagers
        for (int i = 0; i < actualDeaths; i++)
        {
            if (globalCharacterManager.activeCharacters.Count > 0)
            {
                int randomIndex = Random.Range(0, globalCharacterManager.activeCharacters.Count);
                GameCharacterLogic characterToRemove = globalCharacterManager.activeCharacters[randomIndex];
                globalCharacterManager.RemoveCharacter(characterToRemove);
            }
        }
        
        UpdateResearchGenerationRate();
        if (enablePopGrowthLogicLogging)
        {
            Debug.Log($"Event caused {actualDeaths} deaths. New population: {population}, total deaths: {deaths}");
        }
        RefreshHUD();
    }

    /// <summary>
    /// Revise death records for evil empire history manipulation (deaths can be reduced, trueDeaths cannot)
    /// </summary>
    public void ReviseDeathRecords(int change)
    {
        if (change == 0) return;
        
        int oldDeaths = deaths;
        deaths = Mathf.Max(0, deaths + change);
        int actualChange = deaths - oldDeaths;
        
        // If we're adding deaths to public records, also add them to trueDeaths
        // This ensures trueDeaths always represents the actual accumulated deaths
        if (actualChange > 0)
        {
            trueDeaths += actualChange;
            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"Death records revised: {actualChange} additional deaths added to public records. Public deaths: {deaths}, True deaths: {trueDeaths}");
            }
        }
        else if (actualChange < 0)
        {
            // When wiping deaths from public records, trueDeaths remains unchanged
            // This represents evil empire revisionism hiding deaths from history
            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"Death records revised: {Mathf.Abs(actualChange)} deaths wiped from public records. Public deaths: {deaths}, True deaths: {trueDeaths}");
            }
        }
        
        RefreshHUD();
    }

    private void ProcessPopulationChanges()
    {
        // Pause population changes during active events
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive())
            return;
            
        float foodNetRate = GlobalProductionManager.Instance.GetNetProductionRate("Food");

        // Check if food demand is negative (indicating food scarcity)
        bool isFoodScarce = foodNetRate < 0 && GetResourceSlotAmount("Food") <= 0;

        // Handle starvation (population death due to food scarcity)
        if (isFoodScarce && population > 0)
        {
            population -= 1;
            deaths += 1;
            trueDeaths += 1;

            // Remove a random character (villager) when a death occurs
            if (globalCharacterManager.activeCharacters.Count > 0)
            {
                // Get a random index to remove a character from the list
                int randomIndex = Random.Range(0, globalCharacterManager.activeCharacters.Count);

                // Get the random character to remove
                GameCharacterLogic characterToRemove = globalCharacterManager.activeCharacters[randomIndex];

                // Remove the character from the list
                globalCharacterManager.RemoveCharacter(characterToRemove);

            }

            UpdateResearchGenerationRate();

            unitsLogic.ChangeResourceFromName("Food", foodThreshold * starvationRecoveryRate, false);

            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"A population member has died of starvation. Total deaths: {deaths}");
            }

            //LOWER MORALE DUE TO PEOPLE DYING HERE
        }

        // If food is sufficient or no active demand, reset food scarcity state
        if (!isFoodScarce)
        {
            isFoodScarce = false;
        }

        // Handle vagrants converting to population (if applicable)
        if (vagrants > 0 && freeHousing > 0)
        {
            int conversionAmount = Mathf.Min((int)vagrantToPopulationRate, Mathf.Min(vagrants, freeHousing));

            vagrants -= conversionAmount;
            population += conversionAmount;
            UpdateResearchGenerationRate();

            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"Converting {conversionAmount} vagrants to population.");
            }

            // Spawn new villagers if vagrants convert to population
            for (int i = 0; i < conversionAmount; i++)
            {
                Vector3 spawnPosition = new Vector3(0, 0, 0); // Adjust spawn position as needed
                globalCharacterManager.SpawnCharacterFromName("Villager");
            }
        }
    }

}
