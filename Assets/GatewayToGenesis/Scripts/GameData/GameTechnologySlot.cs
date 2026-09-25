using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameTechnologySlot : MonoBehaviour, IGameUnitSlot, ITooltipSource
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

        if (gameUnit != null)
        {
            InitializeTechnologyData();
        }

        // Set by the tree that built this slot; a slot placed by hand uses the tree above it.
        if (technologyTreeLogic == null) technologyTreeLogic = GetComponentInParent<TechnologyTreeLogic>();

        InitializeTechnology(gameUnit);
    }

    private void InitializeTechnologyData()
    {
        if (gameUnit == null) return;

        technologyData = GameCatalog.Technologies.Get(gameUnit.name, nameof(GameTechnologySlot));
        if (technologyData == null) return;

        researchCost = technologyData.resourceAmount.Count > 0 ? technologyData.resourceAmount[0] : 0f;
        if (technologyData.enlightenedConditions.Count > 0 && enlightenedText != null)
        {
            enlightenedText.text = technologyData.enlightenedConditions[0].description;
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
                    GameLog.Warning("The tech unlockable slot prefab has no TechUnlockableSlot component.", LogChannel.Units);
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

        if (technologyData != null)
        {
            foreach (var unlockable in technologyData.techUnlockables) gameUnitsLogic.HandleTechUnlockable(unlockable, this);
        }

        // Apply satisfaction effects from the technology
        if (technologyData != null && technologyData.satisfactionPoints != 0 && StatManager.Instance != null)
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

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Technology(this, data);

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
            GameLog.Event($"Event technology '{technologyName}' unlocked; checking for events", LogChannel.Units);
        }
        else
        {
            GameLog.Warning($"EventSystemLogic is missing; event technology '{technologyName}' cannot trigger events.", LogChannel.Units);
        }
    }

}
