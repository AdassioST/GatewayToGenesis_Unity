using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One technology card in a tree: name, icon, research cost, what it unlocks, the progress outline, the Enlightenment
/// bar (its goals with their progress, gold once enlightened) and the research plan's number. What shows is decided by
/// its <see cref="TechnologyTreeLogic"/> (<see cref="techState"/>, applied by <see cref="ApplyVisibility"/>); research
/// itself runs in <see cref="GameUnitsLogic"/>.
/// </summary>
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

    /// <summary>An Event Technology: researching it triggers an event check, and it is a great deed of the Age.</summary>
    public bool IsEventTechnology => technologyData != null && technologyData.isEventTech;

    /// <summary>A Crisis Technology (its unit's type is Crisis).</summary>
    public bool IsCrisisTechnology => gameUnit != null && gameUnit.type == "Crisis";

    [Header("Enlightened Settings")]
    [SerializeField] public float enlightenedBonusPercent = 0.3f; // 30% default
    [System.NonSerialized] public bool enlightenedBonusApplied = false; // applied once per tech

    // Text on the Enlightenment bar: its own colour on stone, a brighter ivory on the gold bar (dark text sinks into it).
    private static readonly Color GoldBarText = new Color(1f, 0.97f, 0.9f, 1f);
    // The research plan's badge: violet like the plan's lines, gold on the research under way.
    private static readonly Color PlannedRim = new Color(0.78f, 0.64f, 1f, 1f);
    private static readonly Color ActiveRim = new Color(1f, 0.86f, 0.5f, 1f);

    private Color _barTextColor;
    private bool _barTextColorKnown;
    private Image _hitArea;
    private Button _button;
    private RectTransform _badge;
    private Image _badgeRim;
    private TextMeshProUGUI _badgeText;

    private bool initializedForSave;
    private void Start() => EnsureInitializedForSave();
    public void EnsureInitializedForSave()
    {
        if (initializedForSave) return;
        initializedForSave = true;
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
        if (gameUnit == null) return;
        nameText.text = gameUnit.name;
        // The Enlightenment bar turns gold when the technology is enlightened, and stays gold on a researched card.
        enlightened.SetActive(enlightenedCompleted || isUnlocked);

        RefreshLiveText();

        UpdateProgressUI();
    }

    /// <summary>The parts of the card that follow the game: the research cost and the Enlightenment goals' progress.</summary>
    public void RefreshLiveText()
    {
        if (researchCostText != null)
        {
            string cost = CostText();
            if (researchCostText.text != cost) researchCostText.text = cost;
        }
        RefreshEnlightenment();
    }

    // The cost on the card: its first requirement after Discovery Efficiency (the tooltip lists every requirement).
    private string CostText()
    {
        if (technologyData == null || technologyData.resourceAmount.Count == 0) return string.Empty;
        var units = gameUnitsLogic != null ? gameUnitsLogic : GameUnitsLogic.Instance;
        float cost = units != null ? units.GetAdjustedTechCosts(this).FirstOrDefault() : researchCost;
        return TechTreeRules.Short(cost);
    }

    /// <summary>
    /// The Enlightenment bar: the technology's goals with how far along each is while it waits, "Enlightened: ..." once
    /// enlightened, and nothing once researched (the gold bar says it all).
    /// </summary>
    public void RefreshEnlightenment()
    {
        if (enlightenedText == null) return;
        if (!_barTextColorKnown)
        {
            _barTextColor = enlightenedText.color;
            _barTextColorKnown = true;
        }
        bool waiting = !isUnlocked && !enlightenedCompleted;
        string text = technologyData != null && !isUnlocked ? technologyData.DescribeEnlightenment(waiting) : string.Empty;
        if (enlightenedCompleted && !isUnlocked) text = string.IsNullOrEmpty(text) ? "Enlightened" : "Enlightened: " + text;
        text = KeywordMarkup.SafeGlyphs(text);
        if (enlightenedText.text != text) enlightenedText.text = text;
        var color = waiting ? _barTextColor : GoldBarText;
        if (enlightenedText.color != color) enlightenedText.color = color;
    }

    public void RefreshAfterLoad()
    {
        EnsureInitializedForSave();
        if (isUnlocked) { slotImage.sprite = unlockedSprite; progressBar.sprite = unlockedProgressBar; }
        unlockFilter.SetActive(isUnlocked);
        RefreshTechnologyUI();
    }

    // ===== WHAT SHOWS =====

    /// <summary>
    /// Show the card as <see cref="techState"/> says: locked technologies under the blue veil, hidden ones not at all.
    /// A hidden technology takes no pointer either (the slot's invisible hit area stops catching it), so no tooltip, hover
    /// or click can reveal or plan it.
    /// </summary>
    public void ApplyVisibility()
    {
        bool visible = techState != TechnologyState.Invisible;
        isVisible = visible;
        if (displayComponent != null && displayComponent.activeSelf != visible) displayComponent.SetActive(visible);
        if (unavailableFilter != null) unavailableFilter.SetActive(techState == TechnologyState.NextResearchOption);
        if (_hitArea == null) _hitArea = GetComponent<Image>();
        if (_hitArea != null) _hitArea.raycastTarget = visible;
        if (_button == null) _button = GetComponent<Button>();
        if (_button != null) _button.interactable = visible;
    }

    /// <summary>The research plan's number on the card (0: not planned); gold on the research under way.</summary>
    public void ShowPlanPosition(int position, bool active)
    {
        bool show = position > 0 && !isUnlocked;
        if (!show && _badge == null) return;
        if (_badge == null) BuildBadge();
        if (_badge == null) return;
        if (_badge.gameObject.activeSelf != show) _badge.gameObject.SetActive(show);
        if (!show) return;
        _badgeText.text = position.ToString();
        _badgeRim.color = active ? ActiveRim : PlannedRim;
    }

    // A small rhombus pinned to the card's top-left corner, like the capital's notices: rim, dark face, number.
    private void BuildBadge()
    {
        if (displayComponent == null) return;
        var theme = CodeUI.Theme(nameof(GameTechnologySlot));
        if (theme == null) return;

        _badge = CodeUI.Panel(displayComponent.transform, "PlanBadge", new Vector2(0f, 1f), new Vector2(0f, 1f));
        _badge.pivot = new Vector2(0.5f, 0.5f);
        _badge.sizeDelta = new Vector2(56f, 56f);
        _badge.anchoredPosition = new Vector2(30f, -26f);

        _badgeRim = Rhombus(_badge, "Rim", 40f, PlannedRim);
        Rhombus(_badge, "Face", 31f, new Color(0.11f, 0.08f, 0.06f, 0.96f));
        _badgeText = CodeUI.Label(_badge, "Number", string.Empty, 27f, new Color(1f, 0.95f, 0.84f, 1f), FontStyles.Bold, theme);
        _badgeText.alignment = TextAlignmentOptions.Center;
        _badgeText.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Stretch(_badgeText.rectTransform);
        _badge.SetAsLastSibling();
    }

    private static Image Rhombus(RectTransform parent, string name, float side, Color color)
    {
        var rect = CodeUI.Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.sizeDelta = new Vector2(side, side);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ===== RESEARCH =====

    /// <summary>A technology finished research this session (never raised by loading a save).</summary>
    public static event System.Action<GameTechnologySlot> Researched;

    /// <summary>A technology was enlightened this session: its goals were met, or a story enlightened it (slot, why).</summary>
    public static event System.Action<GameTechnologySlot, string> Enlightened;

    /// <summary>Announce an Enlightenment (<see cref="GameUnitsLogic.EnlightenTechnology(GameTechnologySlot, string)"/> does the work).</summary>
    public void NotifyEnlightened(string reason) => Enlightened?.Invoke(this, reason);

    public void UnlockTechnology()
    {
        if (isUnlocked) return;

        isUnlocked = true;
        researchProgress = 1f;
        if (gameUnitsLogic == null) gameUnitsLogic = GameUnitsLogic.Instance;

        slotImage.sprite = unlockedSprite;
        progressBar.sprite = unlockedProgressBar;

        unlockFilter.SetActive(true);

        RefreshTechnologyUI();
        ShowPlanPosition(0, false);

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

        if (technologyData != null && gameUnitsLogic != null)
        {
            foreach (var unlockable in technologyData.techUnlockables) gameUnitsLogic.HandleTechUnlockable(unlockable, this);
        }

        // Apply satisfaction effects from the technology
        if (technologyData != null && technologyData.satisfactionPoints != 0 && StatManager.Instance != null)
        {
            StatManager.Instance.ChangeSatisfactionPoints(technologyData.satisfactionPoints, $"Technology {gameUnit.name}");
        }

        // Its research is done with and the plan moves on before anyone hears of it; the trees refresh on Researched.
        if (gameUnitsLogic != null) gameUnitsLogic.OnTechnologyResearched(this);

        Researched?.Invoke(this);
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
