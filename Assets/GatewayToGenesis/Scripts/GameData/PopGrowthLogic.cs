using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class PopGrowthLogic : MonoBehaviour
{
    public static PopGrowthLogic Instance { get; private set; }

    public float foodThreshold = 100f, vagrantToPopulationRate = 1f, researchPerPopulation = 1f; 

    public int housing, population, vagrants, freeHousing;

    [SerializeField] private GameUnit research, food;

    private GameUnitsLogic unitsLogic;

    public TMP_Text foodText, freeHousingText, populationText, vagrantsText;

    private float lastResearchModifier = 0f;

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

        if (unitsLogic == null || unitsLogic.storageTab == null)
        {
            Debug.LogError("GameUnitsLogic or storageTab not found! Ensure dependencies are set up correctly.");
            enabled = false;
        }

        StartCoroutine(InitializePopulationResources());

        InvokeRepeating("ConvertVagrantsToPopulation", 1f, 1f); // Repeats every 1 second

    }

    private void Update()
    {
        ManagePopulationGrowth();

        RefreshHUD();

        if (Input.GetKeyDown("s"))
        {
            housing += 3;
        }
    }

    private void ManagePopulationGrowth()
    {
        freeHousing = housing - population;

        // Consume food and grow population or vagrants
        if (GetResourceSlotAmount("Food") >= foodThreshold)
        {
            unitsLogic.ChangeResourceFromName("Food", -foodThreshold, false);

            if (freeHousing > 0)
            {
                population += 1;
                UpdateResearchGenerationRate();
            }

            else
            {
                vagrants += 1;
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

        // Calculate the new modifier based on the current population
        float researchAmount = population * researchPerPopulation;

        // Apply the new modifier
        GlobalProductionManager.Instance.AdjustResourceModifier("Research", researchAmount, true, true);

        // Update the tracker with the new modifier
        lastResearchModifier = researchAmount;
    }


    private IEnumerator InitializePopulationResources()
    {
        yield return null; // Wait one frame

        unitsLogic.storageTab.AddNewUnit(food);
        unitsLogic.storageTab.AddNewUnit(research);

    }
    public void RefreshHUD()
    {
        foodText.text = GetResourceSlotAmount("Food").ToString();
        freeHousingText.text = freeHousing.ToString();

        populationText.text = population.ToString();
        vagrantsText.text = vagrants.ToString();
    }
    private float GetResourceSlotAmount(string resourceName)
    {
        GameObject slotObject = unitsLogic.storageTab.slots.Find(slot => slot.name == resourceName);
        return slotObject != null ? slotObject.GetComponent<GameResourceSlot>().amount : 0;
    }

    private void ConvertVagrantsToPopulation()
    {
        // Only convert if there are vagrants and free housing
        if (vagrants > 0 && freeHousing > 0)
        {
            int conversionAmount = Mathf.Min((int)vagrantToPopulationRate, Mathf.Min(vagrants, freeHousing));

            vagrants -= conversionAmount;
            population += conversionAmount;

            UpdateResearchGenerationRate();

            // Debug output for conversion process
            Debug.Log($"Converting {conversionAmount} vagrants to population.");
        }
    }
}
