using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static UnityEngine.Mesh;

public class GameTechnologySlot : MonoBehaviour, IGameUnitSlot
{
    // INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 0.0f;
    public float amount { get; set; }
    public float maxAmount { get; set; }

    // VARIABLES
    public float researchProgress { get; set; } = 0.0f;
    public float researchCost { get; private set; }

    [SerializeField] private GameObject enlightened, unlockFilter;

    public Image slotImage;

    public bool isUnlocked, enlightenedCompleted, alreadyClicked;
    public Sprite unlockedSprite, unlockedProgressBar;

    public TechnologyData technologyData;

    public Image icon;
    public TMP_Text nameText, enlightenedText, researchCostText;
    [SerializeField] private Image progressBar; // Image component for the ProgressBar

    private GameUnitsLogic gameUnitsLogic;

    // Static dictionary to hold TechnologyData
    private static Dictionary<string, TechnologyData> technologyDataDictionary;

    [SerializeField] private GameObject techUnlockableSlotPrefab, techUnlockables;

    private void Start()
    {
        gameUnitsLogic = GameUnitsLogic.Instance;

        // Initialize the technology data dictionary if not already done
        if (technologyDataDictionary == null)
        {
            InitializeTechnologyDataDictionary();
        }

        if (gameUnit != null)
        {
            InitializeTechnologyData();
        }

        InitializeTechnology(gameUnit);
    }

    private void InitializeTechnologyDataDictionary()
    {
        technologyDataDictionary = new Dictionary<string, TechnologyData>();

        // Load all TechnologyData from Resources
        TechnologyData[] technologyDataArray = Resources.LoadAll<TechnologyData>("Technology");

        foreach (var techData in technologyDataArray)
        {
            Debug.Log("Loaded TechnologyData: " + techData.name);
            if (!technologyDataDictionary.ContainsKey(techData.name))
            {
                technologyDataDictionary.Add(techData.name, techData);
            }
            else
            {
                Debug.LogWarning($"Duplicate TechnologyData found for {techData.name}, Skipping");
            }
        }
    }

    private void InitializeTechnologyData()
    {
        if (gameUnit == null) return;

        Debug.Log("Looking for TechnologyData for: " + gameUnit.name);

        // Fetch the TechnologyData from the dictionary
        if (technologyDataDictionary.ContainsKey(gameUnit.name))
        {
            technologyData = technologyDataDictionary[gameUnit.name];
            researchCost = technologyData.resourceAmount[0];

            if (technologyData.eurekaConditions.Count > 0)
            {
                enlightenedText.text = technologyData.eurekaConditions[0].description;
            }
        }
        else
        {
            Debug.LogWarning($"No TechnologyData found for {gameUnit.name}. Make sure the data is loaded correctly.");
        }
    }

    public void InitializeTechnology(GameUnit newTechnology)
    {
        gameUnit = newTechnology;

        if (gameUnit == null) return;

        icon.sprite = newTechnology.icon;

        // Initialize unlockables if any
        if (technologyData != null && technologyData.techUnlockables != null)
        {
            foreach (var techUnlockable in technologyData.techUnlockables)
            {
                GameObject unlockableSlot = Instantiate(techUnlockableSlotPrefab, techUnlockables.transform);
                TechUnlockableSlot slotComponent = unlockableSlot.GetComponent<TechUnlockableSlot>();

                if (slotComponent != null)
                {
                    slotComponent.InitializeTechUnlockable(techUnlockable);
                }
                else
                {
                    Debug.LogWarning("UnlockSlot prefab is missing the UnlockSlot component.");
                }
            }
        }

        RefreshTechnologyUI();
    }

    public void RefreshTechnologyUI()
    {
        nameText.text = gameUnit.name;
        enlightened.SetActive(enlightenedCompleted);

        researchCostText.text = researchCost.ToString(); //Change this to fill

        UpdateProgressUI();
    }

    public void UnlockTechnology()
    {
        if (isUnlocked) return;

        isUnlocked = true;
        enlightenedCompleted = true;

        gameUnitsLogic.activeTechnologySlot = null;

        slotImage.sprite = unlockedSprite;
        progressBar.sprite = unlockedProgressBar;

        unlockFilter.SetActive(false);

        RefreshTechnologyUI();

        foreach (var unlockable in technologyData.techUnlockables)
        {
            gameUnitsLogic.HandleTechUnlockable(unlockable);
        }

        Debug.Log($"{gameUnit.name} has been unlocked and its unlockables have been processed!");
    }

    public void UpdateProgressUI()
    {
        progressBar.fillAmount = researchProgress; // Update the ProgressBar fill amount
    }
}
