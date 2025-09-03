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

    public bool isUnlocked, enlightenedCompleted, alreadyClicked, isVisible;
    public Sprite unlockedSprite, unlockedProgressBar, eventEnlightenedSprite, crisisEnlightenedSprite, eventSlotSprite, crisisSlotSprite, eventProgressBarSprite, crisisProgressBarSprite;

    public TechnologyData technologyData;

    public Image icon;
    public TMP_Text nameText, enlightenedText, researchCostText;
    [SerializeField] private Image progressBar; // Image component for the ProgressBar

    private GameUnitsLogic gameUnitsLogic;

    // Static dictionary to hold TechnologyData
    private static Dictionary<string, TechnologyData> technologyDataDictionary;

    [SerializeField] private GameObject techUnlockableSlotPrefab, techUnlockables;
    
    public GameObject unavailableFilter, displayComponent;

    public TechnologyTreeLogic technologyTreeLogic; // Reference to TechnologyTreeLogic

    public enum TechnologyState
    {
        CurrentResearchOption,
        NextResearchOption,
        Invisible,
        Unlocked
    }

    public TechnologyState techState;

    [Header("Enlightened Settings")]
    [SerializeField] public float enlightenedBonusPercent = 0.3f; // 30% default
    [System.NonSerialized] public bool enlightenedBonusApplied = false; // applied once per tech

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

        if (technologyTreeLogic == null)
        {
            technologyTreeLogic = FindObjectOfType<TechnologyTreeLogic>(); // Find the instance at runtime
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

        // Fetch the TechnologyData from the dictionary
        if (technologyDataDictionary.ContainsKey(gameUnit.name))
        {
            technologyData = technologyDataDictionary[gameUnit.name];
            researchCost = technologyData.resourceAmount[0];

            if (technologyData.enlightenedConditions.Count > 0)
            {
                enlightenedText.text = technologyData.enlightenedConditions[0].description;
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

        if (gameUnit.type == "Event")
        {
            slotImage.sprite = eventSlotSprite; 
            progressBar.sprite = eventProgressBarSprite; 

            enlightened.GetComponent<Image>().sprite = eventEnlightenedSprite; 
        }
        else if (gameUnit.type == "Crisis")
        {
            slotImage.sprite = crisisSlotSprite; 
            progressBar.sprite = crisisProgressBarSprite; 

            enlightened.GetComponent<Image>().sprite = crisisEnlightenedSprite;
        }

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

        unlockFilter.SetActive(true);

        RefreshTechnologyUI();

        // Check if this is an event technology and trigger the corresponding event
        if (technologyData != null && technologyData.isEventTech)
        {
            TriggerEventTechnology(gameUnit.name);
            
            // Event technologies always give +10 satisfaction points
            if (StatManager.Instance != null)
            {
                StatManager.Instance.ChangeSatisfactionPoints(10, $"Event Technology {gameUnit.name}");
            }
        }
        
        // Crisis technologies always give -25 satisfaction points
        if (gameUnit.type == "Crisis" && StatManager.Instance != null)
        {
            StatManager.Instance.ChangeSatisfactionPoints(-25, $"Crisis Technology {gameUnit.name}");
        }

        foreach (var unlockable in technologyData.techUnlockables)
        {
            gameUnitsLogic.HandleTechUnlockable(unlockable, this);
        }

        // Apply satisfaction effects from the technology
        if (technologyData.satisfactionPoints != 0 && StatManager.Instance != null)
        {
            StatManager.Instance.ChangeSatisfactionPoints(technologyData.satisfactionPoints, $"Technology {gameUnit.name}");
        }

        // Refresh visibility directly after unlocking
        if (technologyTreeLogic != null)
        {
            technologyTreeLogic.DetermineTechnologyVisibilityForAllSlots(); // Recalculate visibility
        }

    }


    public void UpdateProgressUI()
    {
        progressBar.fillAmount = researchProgress; // Update the ProgressBar fill amount
    }

    /// <summary>
    /// Trigger an event when an event technology is unlocked
    /// </summary>
    private void TriggerEventTechnology(string technologyName)
    {
        if (string.IsNullOrEmpty(technologyName)) return;

        // Use the existing EventSystemLogic to trigger the event
        if (EventSystemLogic.Instance != null)
        {
            EventSystemLogic.Instance.TriggerEventCheck();
            Debug.Log($"[GameTechnologySlot] Event technology '{technologyName}' unlocked - triggering event check");
        }
        else
        {
            Debug.LogWarning($"[GameTechnologySlot] EventSystemLogic.Instance is null - cannot trigger event for '{technologyName}'");
        }
    }

}
