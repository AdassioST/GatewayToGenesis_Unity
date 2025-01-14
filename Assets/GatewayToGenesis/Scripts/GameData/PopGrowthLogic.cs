using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PopGrowthLogic : MonoBehaviour
{
    public static PopGrowthLogic Instance { get; private set; }

    public float foodThreshold = 12f, vagrantToPopulationRate = 1f, researchPerPopulation = 1f, starvationRecoveryRate = 0.05f;

    public float foodDemandBuffer = -2.5f, demandRateConstant = 0.03f, sustainabilityTier = 1f, demandModifier = 1.0f;

    public int housing, population, vagrants, freeHousing, deaths;

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
        ManagePopulationGrowth();
        UpdateFoodDemand();

        RefreshHUD();
    }

    private void ManagePopulationGrowth()
    {
        freeHousing = housing - population;

        // Consume food and grow population or vagrants based on the allowVagrants flag
        if (GetResourceSlotAmount("Food") >= foodThreshold)
        {
            // Only remove food threshold if population is growing or vagrants are allowed
            if (freeHousing > 0 || allowVagrants)
            {
                unitsLogic.ChangeResourceFromName("Food", -foodThreshold, false);

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
            GlobalProductionManager.Instance.AdjustResourceModifier("Research", lastResearchModifier, true, false);
        }

        float researchAmount = population * researchPerPopulation;

        GlobalProductionManager.Instance.AdjustResourceModifier("Research", researchAmount, true, true);

        lastResearchModifier = researchAmount;
    }

    private void UpdateFoodDemand()
    {
        if (lastFoodModifier != 0f)
        {
            GlobalProductionManager.Instance.AdjustResourceModifier("Food", lastFoodModifier, false, false);
        }

        // FOOD DEMAND FORMULA
        foodDemand = (float)(foodDemandBuffer + Mathf.Exp((demandRateConstant / sustainabilityTier) * population)) * demandModifier;

        // Only apply the modifier if foodDemand is negative
        if (foodDemand > 0)
        {
            GlobalProductionManager.Instance.AdjustResourceModifier("Food", foodDemand, false, true);
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
            foodThreshold = newFoodThreshold;
            foodSlot.maxAmount = newFoodThreshold;
        }
        else
        {
            Debug.LogWarning("Food slot not found.");
        }
    }
    private void ProcessPopulationChanges()
    {
        float foodNetRate = GlobalProductionManager.Instance.GetNetProductionRate("Food");

        // Check if food demand is negative (indicating food scarcity)
        bool isFoodScarce = foodNetRate < 0 && GetResourceSlotAmount("Food") <= 0;

        // Handle starvation (population death due to food scarcity)
        if (isFoodScarce && population > 0)
        {
            population -= 1;
            deaths += 1;

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

            Debug.Log($"A population member has died of starvation. Total deaths: {deaths}");

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

            Debug.Log($"Converting {conversionAmount} vagrants to population.");

            // Spawn new villagers if vagrants convert to population
            for (int i = 0; i < conversionAmount; i++)
            {
                Vector3 spawnPosition = new Vector3(0, 0, 0); // Adjust spawn position as needed
                globalCharacterManager.SpawnCharacterFromName("Villager");
            }
        }
    }

}
