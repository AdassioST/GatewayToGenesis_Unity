using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq; // Added for .Sum()

public class PopGrowthLogic : MonoBehaviour
{
    [Header("Pop Growth Logic Settings")]
    [SerializeField] public bool enablePopGrowthLogicLogging; // Whether to log what the boss is doing

    public static PopGrowthLogic Instance { get; private set; }

    public float foodThreshold = 12f, vagrantToPopulationRate = 1f, researchPerPopulation = 1f, starvationRecoveryRate = 0.05f;
    
    public float foodDemandBuffer = -2.5f, demandRateConstant = 0.03f, sustainabilityTier = 1f, demandModifier = 1.0f;

    [Header("Population Management")]
    public int housing, population, vagrants, freeHousing, deaths, vagrantDeaths, trueDeaths;

    [SerializeField] private GameUnit research, food;

    private GameUnitsLogic unitsLogic;

    private GlobalCharacterManager globalCharacterManager;

    // Persistent housing bonus system (cannot be destroyed by normal housing changes)
    private Dictionary<string, int> housingBonuses = new Dictionary<string, int>();
    private int baseHousing = 0; // Base housing value without bonuses

    public TMP_Text foodText, freeHousingText, populationText, vagrantsText, foodStateText;

    private float lastResearchModifier, lastFoodModifier, foodDemand;

    public bool allowVagrants, isFoodScarce;
    
    // Note: When allowVagrants is false and no free housing exists, food thresholds won't trigger population growth
    // This prevents food consumption when no growth is possible, maintaining visual consistency
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

    private void OnDestroy()
    {
        // Clean up singleton reference if this is the instance
        if (Instance == this)
        {
            Instance = null;
        }
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

        // Initialize base housing with current housing value
        baseHousing = housing;

        StartCoroutine(InitializePopulationResources());

        InvokeRepeating("ProcessPopulationChanges", 1f, 1f); // Repeats every 1 second

        // Subscribe to food changes to process population growth immediately
        StartCoroutine(SubscribeToFoodChanges());
    }

    /// <summary>
    /// Subscribe to food changes to process population growth immediately when threshold is crossed
    /// </summary>
    private IEnumerator SubscribeToFoodChanges()
    {
        // Wait for systems to be ready
        while (unitsLogic == null || unitsLogic.storageTab == null)
        {
            yield return null;
        }
        
        // Find the food slot and add our change listener
        GameResourceSlot foodSlot = unitsLogic.GetResourceSlotFromName("Food");
        if (foodSlot != null)
        {
            // Store reference to monitor changes
            StartCoroutine(MonitorFoodChanges(foodSlot));
        }
    }

    /// <summary>
    /// Monitor food changes and process population growth when threshold is crossed
    /// </summary>
    private IEnumerator MonitorFoodChanges(GameResourceSlot foodSlot)
    {
        float lastFoodAmount = foodSlot.amount;
        float lastThreshold = foodThreshold;
        int consecutiveProcesses = 0;
        const int maxConsecutiveProcesses = 10; // Prevent infinite loops
        
        while (foodSlot != null)
        {
            float currentFood = foodSlot.amount;
            float currentThreshold = foodThreshold;
            
            // Check if food amount or threshold changed
            if (Mathf.Abs(currentFood - lastFoodAmount) > 0.001f || Mathf.Abs(currentThreshold - lastThreshold) > 0.001f)
            {
                // Safety check to prevent infinite loops
                if (consecutiveProcesses < maxConsecutiveProcesses)
                {
                    ProcessFoodChanges(currentFood, lastFoodAmount, currentThreshold, lastThreshold);
                    consecutiveProcesses++;
                }
                else
                {
                    if (enablePopGrowthLogicLogging)
                    {
                        Debug.LogWarning($"[PopGrowthLogic] Food processing loop detected, skipping to prevent infinite loop. Food: {currentFood}, Threshold: {currentThreshold}");
                    }
                    consecutiveProcesses = 0; // Reset counter
                }
                
                lastFoodAmount = currentFood;
                lastThreshold = currentThreshold;
            }
            else
            {
                // Reset consecutive counter if no changes
                consecutiveProcesses = 0;
            }
            
            yield return null;
        }
    }

    /// <summary>
    /// Centralized food processing that handles population growth when food crosses threshold
    /// </summary>
    private void ProcessFoodChanges(float currentFood, float previousFood, float currentThreshold, float previousThreshold)
    {
        // Skip if event system is active
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive())
            return;
            
        // Calculate how many thresholds worth of food we can process
        float availableThresholds = Mathf.Floor(currentFood / currentThreshold);
        float previousThresholds = Mathf.Floor(previousFood / previousThreshold);
        
        // If we have more thresholds available now than before, process growth
        if (availableThresholds > previousThresholds)
        {
            int thresholdsToProcess = Mathf.FloorToInt(availableThresholds - previousThresholds);
            ProcessPopulationGrowthFromThresholds(thresholdsToProcess, currentThreshold);
        }
        // If we dropped below threshold, ensure we don't have negative food
        else if (currentFood < 0)
        {
            // Safety check: prevent negative food
            GameResourceSlot foodSlot = unitsLogic.GetResourceSlotFromName("Food");
            if (foodSlot != null && foodSlot.amount < 0)
            {
                foodSlot.amount = 0f;
                foodSlot.RefreshProductionAmount();
                
                if (enablePopGrowthLogicLogging)
                {
                    Debug.LogWarning($"[PopGrowthLogic] Food amount was negative, corrected to 0");
                }
            }
        }
    }

    /// <summary>
    /// Process population growth based on available food thresholds
    /// </summary>
    private void ProcessPopulationGrowthFromThresholds(int thresholdsToProcess, float thresholdAmount)
    {
        if (thresholdsToProcess <= 0) return;
        
        // Calculate total food needed for all thresholds
        float totalFoodNeeded = thresholdsToProcess * thresholdAmount;
        
        // Check if we actually have enough food (safety check)
        float availableFood = GetResourceSlotAmount("Food");
        if (availableFood < totalFoodNeeded)
        {
            thresholdsToProcess = Mathf.FloorToInt(availableFood / thresholdAmount);
            if (thresholdsToProcess <= 0) return;
            totalFoodNeeded = thresholdsToProcess * thresholdAmount;
        }
        
        // CRITICAL FIX: Process each threshold individually to check housing availability
        int actualThresholdsProcessed = 0;
        float actualFoodConsumed = 0f;
        
        for (int i = 0; i < thresholdsToProcess; i++)
        {
            // Check if population growth is possible for this specific threshold
            int currentFreeHousing = housing - population;
            bool canGrowPopulation = currentFreeHousing > 0 || allowVagrants;
            
            if (!canGrowPopulation)
            {
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"[PopGrowthLogic] Stopping threshold processing at threshold {i + 1}/{thresholdsToProcess} - no more growth possible (housing: {housing}, population: {population}, allowVagrants: {allowVagrants})");
                }
                break; // Stop processing more thresholds
            }
            
            // Process this threshold
            if (currentFreeHousing > 0)
            {
                population += 1;
                UpdateResearchGenerationRate();
                
                // Spawn new villager when population increases
                if (globalCharacterManager != null)
                {
                    globalCharacterManager.SpawnCharacterFromName("Villager");
                }
                
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"[PopGrowthLogic] Food threshold {i + 1}/{thresholdsToProcess} met: population increased to {population}");
                }
            }
            else if (allowVagrants)
            {
                vagrants += 1;
                
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"[PopGrowthLogic] Food threshold {i + 1}/{thresholdsToProcess} met: vagrant increased to {vagrants}");
                }
            }
            
            actualThresholdsProcessed++;
            actualFoodConsumed += thresholdAmount;
        }
        
        // Only consume the food we actually processed
        if (actualFoodConsumed > 0f)
        {
            GameResourceSlot foodSlot = unitsLogic.GetResourceSlotFromName("Food");
            if (foodSlot != null)
            {
                foodSlot.amount = Mathf.Max(0f, foodSlot.amount - actualFoodConsumed);
                foodSlot.RefreshProductionAmount();
                
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"[PopGrowthLogic] Consumed {actualFoodConsumed:F1} food for {actualThresholdsProcessed} thresholds (requested: {totalFoodNeeded:F1} for {thresholdsToProcess})");
                }
            }
        }
        
        // Update HUD
        RefreshHUD();
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

        // The centralized food processing system now handles threshold-based population growth
        // This method only handles the base population growth logic for when food is naturally above threshold
        
        // Check if we have enough food for at least one population growth
        float currentFood = GetResourceSlotAmount("Food");
        if (currentFood + 1e-3f >= adjustedThreshold) // epsilon guard for float precision
        {
            // Check if we have enough food for exactly one threshold (not multiple)
            float availableThresholds = Mathf.Floor(currentFood / adjustedThreshold);
            if (availableThresholds >= 1f)
            {
                // CRITICAL FIX: Check if population growth is actually possible for this specific threshold
                int currentFreeHousing = housing - population;
                bool canGrowPopulation = currentFreeHousing > 0 || allowVagrants;
                
                if (!canGrowPopulation)
                {
                    if (enablePopGrowthLogicLogging)
                    {
                        Debug.Log($"[PopGrowthLogic] Food above threshold but no population growth possible (housing: {housing}, population: {population}, allowVagrants: {allowVagrants}) - skipping food consumption");
                    }
                    return; // Don't consume food if we can't grow population
                }
                
                // Withdraw exactly one threshold worth of food
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

                if (currentFreeHousing > 0)
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
        
        // Modify base housing (bonuses are preserved)
        baseHousing = Mathf.Max(0, baseHousing + change);
        
        // Recalculate total housing with bonuses
        RecalculateHousingWithBonuses();
        
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
                    Debug.Log($"Event housing reduction converted {populationToConvert} population to vagrants. New housing: {housing} (base: {baseHousing} + bonus: {GetTotalHousingBonus()}), population: {population}, vagrants: {vagrants}");
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

    /// <summary>
    /// Handle food changes from external sources (events, etc.) and process population growth immediately
    /// </summary>
    public void HandleExternalFoodChange(float oldAmount, float newAmount)
    {
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive())
            return;
            
        // Calculate how many thresholds worth of food we can process
        float adjustedThreshold = GetMoraleAdjustedFoodThreshold();
        float oldThresholds = Mathf.Floor(oldAmount / adjustedThreshold);
        float newThresholds = Mathf.Floor(newAmount / adjustedThreshold);
        
        // If we have more thresholds available now than before, process growth
        if (newThresholds > oldThresholds)
        {
            int thresholdsToProcess = Mathf.FloorToInt(newThresholds - oldThresholds);
            ProcessPopulationGrowthFromThresholds(thresholdsToProcess, adjustedThreshold);
        }
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

    /// <summary>
    /// Get the current housing value including all bonuses
    /// </summary>
    public int GetHousing()
    {
        return housing;
    }
    
    /// <summary>
    /// Get the base housing value without bonuses
    /// </summary>
    public int GetBaseHousing()
    {
        return baseHousing;
    }
    
    /// <summary>
    /// Get the total housing bonus from all sources
    /// </summary>
    public int GetTotalHousingBonus()
    {
        return housingBonuses.Values.Sum();
    }
    
    /// <summary>
    /// Add a housing bonus from a specific source (civic, legend, etc.)
    /// </summary>
    public void AddHousingBonus(int bonusValue, string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogWarning("[PopGrowthLogic] Cannot add housing bonus with empty source");
            return;
        }
        
        housingBonuses[source] = bonusValue;
        RecalculateHousingWithBonuses();
        
        if (enablePopGrowthLogicLogging)
        {
            Debug.Log($"[PopGrowthLogic] Added housing bonus: +{bonusValue} from {source} (Total bonus: {GetTotalHousingBonus()})");
        }
    }
    
    /// <summary>
    /// Remove a housing bonus from a specific source
    /// </summary>
    public void RemoveHousingBonus(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogWarning("[PopGrowthLogic] Cannot remove housing bonus with empty source");
            return;
        }
        
        if (housingBonuses.ContainsKey(source))
        {
            int removedBonus = housingBonuses[source];
            housingBonuses.Remove(source);
            RecalculateHousingWithBonuses();
            
            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"[PopGrowthLogic] Removed housing bonus: -{removedBonus} from {source} (Total bonus: {GetTotalHousingBonus()})");
            }
        }
    }
    
    /// <summary>
    /// Clear all housing bonuses from a specific source
    /// </summary>
    public void ClearHousingBonusesFromSource(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogWarning("[PopGrowthLogic] Cannot clear housing bonuses with empty source");
            return;
        }
        
        var sourcesToRemove = housingBonuses.Keys.Where(key => key.StartsWith(source)).ToList();
        int totalRemoved = 0;
        
        foreach (var key in sourcesToRemove)
        {
            totalRemoved += housingBonuses[key];
            housingBonuses.Remove(key);
        }
        
        if (totalRemoved > 0)
        {
            RecalculateHousingWithBonuses();
            
            if (enablePopGrowthLogicLogging)
            {
                Debug.Log($"[PopGrowthLogic] Cleared {totalRemoved} housing bonus from source: {source} (Total bonus: {GetTotalHousingBonus()})");
            }
        }
    }
    
    /// <summary>
    /// Get all housing bonus sources and their values
    /// </summary>
    public Dictionary<string, int> GetHousingBonusSources()
    {
        return new Dictionary<string, int>(housingBonuses);
    }
    
    /// <summary>
    /// Recalculate housing with all active bonuses
    /// </summary>
    private void RecalculateHousingWithBonuses()
    {
        int totalBonus = GetTotalHousingBonus();
        housing = Mathf.Max(0, baseHousing + totalBonus);
        
        // Update free housing calculation
        freeHousing = Mathf.Max(0, housing - population);
        
        if (enablePopGrowthLogicLogging)
        {
            Debug.Log($"[PopGrowthLogic] Recalculated housing: {baseHousing} base + {totalBonus} bonus = {housing} total");
        }
    }

    [ContextMenu("Print Population Info")]
    public void PrintPopulationInfo()
    {
        if (!enablePopGrowthLogicLogging) return;
        
        Debug.Log("=== POPULATION SYSTEM INFO ===");
        Debug.Log($"Population: {population}");
        Debug.Log($"Housing: {housing} (Base: {baseHousing} + Bonus: {GetTotalHousingBonus()})");
        Debug.Log($"Free Housing: {freeHousing}");
        Debug.Log($"Vagrants: {vagrants}");
        Debug.Log($"Food Threshold: {foodThreshold}");
        Debug.Log($"Food Demand: {foodDemand}");
        Debug.Log($"Allow Vagrants: {allowVagrants}");
        
        // Show housing bonus breakdown
        var housingSources = GetHousingBonusSources();
        if (housingSources.Count > 0)
        {
            Debug.Log("=== HOUSING BONUSES ===");
            foreach (var kvp in housingSources)
            {
                Debug.Log($"  +{kvp.Value} housing from {kvp.Key}");
            }
            Debug.Log($"  Total Bonus: +{GetTotalHousingBonus()}");
        }
        else
        {
            Debug.Log("  No housing bonuses active");
        }
        
        Debug.Log($"Deaths: {deaths}");
        Debug.Log($"Vagrant Deaths: {vagrantDeaths}");
        Debug.Log($"True Deaths: {trueDeaths}");
    }

}
