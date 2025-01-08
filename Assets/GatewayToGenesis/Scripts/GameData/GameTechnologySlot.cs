using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameTechnologySlot : MonoBehaviour, IGameUnitSlot
{
    // INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 0.0f;
    public float amount { get; set; }
    public float maxAmount { get; set; }

    // VARIABLES
    public float researchProgress { get; set; } = 0.0f;
    public float researchCost { get; set; }
    public bool isUnlocked;

    public TechnologyData technologyData;

    public Image icon;
    public TMP_Text nameText, eurekaText, researchProgressText;

    [SerializeField] private GameObject techUnlockableSlotPrefab, techUnlockables;

    private GameUnitsLogic gameUnitsLogic;

    // Dictionary to store TechnologyData by GameUnit name
    private static Dictionary<string, TechnologyData> technologyDataDictionary;

    // Start is called before the first frame update
    public void Start()
    {
        gameUnitsLogic = FindObjectOfType<GameUnitsLogic>();

        if (gameUnitsLogic == null)
        {
            Debug.LogError("GameUnitsLogic component not found in the scene.");
        }

        if (technologyDataDictionary == null)
        {
            InitializeTechnologyDataDictionary();
        }

        if (gameUnit != null)
        {
            if (technologyDataDictionary.ContainsKey(gameUnit.name))
            {
                technologyData = technologyDataDictionary[gameUnit.name];
                researchCost = technologyData.resourceAmount[0];

                if (technologyData.eurekaConditions.Count > 0)
                {
                    eurekaText.text = technologyData.eurekaConditions[0].description;
                }
            }

            else
            {
                Debug.LogWarning($"No TechnologyData found for {gameUnit.name}");
            }
        }

        InitializeTechnology(gameUnit);
    }

    public void InitializeTechnology(GameUnit newTechnology)
    {
        gameUnit = newTechnology;

        icon.sprite = newTechnology.icon;

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

        RefreshTechnologyUI();
    }

    public void RefreshTechnologyUI()
    {
        nameText.text = gameUnit.name;

        researchProgressText.text = $"Research Progress: {Mathf.Round(researchProgress)} / {researchCost}"; //Change this to fill
    }

    public void UnlockTechnology()
    {
        if (isUnlocked) return;

        isUnlocked = true;
        RefreshTechnologyUI();

        // Call for each unlockable associated with the technology
        foreach (TechUnlockable unlockable in technologyData.techUnlockables)
        {
            gameUnitsLogic.HandleTechUnlockable(unlockable);
        }

        Debug.Log($"{gameUnit.name} has been unlocked and its unlockables have been processed!");
    }

    private void InitializeTechnologyDataDictionary()
    {
        technologyDataDictionary = new Dictionary<string, TechnologyData>();

        // Load all TechnologyData from Resources
        TechnologyData[] technologyDataArray = Resources.LoadAll<TechnologyData>("Technology");

        foreach (var techData in technologyDataArray)
        {
            if (!technologyDataDictionary.ContainsKey(techData.gameUnit.name))
            {
                technologyDataDictionary.Add(techData.gameUnit.name, techData);
            }
            else
            {
                Debug.LogWarning($"Duplicate TechnologyData found for {techData.gameUnit.name} Skipping");
            }
        }
    }
}

