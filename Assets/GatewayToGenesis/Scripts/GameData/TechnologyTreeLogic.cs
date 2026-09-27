using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GameTechnologySlot;

/// <summary>
/// One Age's technology tree in the research tab. It builds its slots from <see cref="eraTechnologies"/> (list order is
/// grid order, column by column; an empty entry leaves a cell free) and keeps them honest:
/// - What shows (<see cref="TechTreeRules.Visibility"/>): researched and researchable technologies, one step beyond
///   them, anything enlightened, and the technology the Age is waiting for. Everything else is hidden, pointer
///   included, so no tooltip or click gives it away. Slots are initialized as they are built, so even the first frame
///   hides what is not uncovered.
/// - The lines between them (<see cref="TechTreeConnectors"/>) and the research plan's numbers on the slots.
/// - Each technology's Enlightenment goals (its eurekas), checked a few times a second: once all are met, the
///   technology is enlightened (<see cref="GameUnitsLogic.EnlightenTechnology(GameTechnologySlot, string)"/>).
/// It refreshes itself when research completes, a technology is enlightened, the plan changes or the Age starts
/// waiting for a technology.
/// </summary>
public class TechnologyTreeLogic : MonoBehaviour
{
    [SerializeField] private GameObject emptySlotPrefab, slots;
    [SerializeField] private List<TechnologyData> eraTechnologies = new List<TechnologyData>();
    [Tooltip("Grid position (0-based, column by column) of the research tab's first technology, which exists before the tree is built.")]
    [SerializeField] private int initializationUnitIndex = 1;
    [Tooltip("Seconds between checks of the Enlightenment goals (real time).")]
    [SerializeField] private float enlightenmentCheckSeconds = 0.5f;

    private static readonly List<TechnologyTreeLogic> Trees = new List<TechnologyTreeLogic>();

    private TabBuilderLogic tabBuilderLogic;
    private readonly List<GameTechnologySlot> _slots = new List<GameTechnologySlot>();
    private TechTreeConnectors _connectors;
    private GameUnitsLogic _planSource;
    private float _nextCheck;
    private string _waitingGate;
    private bool _built;

    /// <summary>The technologies of this tree, in grid order.</summary>
    public IReadOnlyList<GameTechnologySlot> TreeSlots => _slots;

    /// <summary>The technology under the pointer, whose way the lines light (null when none).</summary>
    public GameTechnologySlot Hovered { get; private set; }

    /// <summary>The research plan (<see cref="GameUnitsLogic.ResearchPlan"/>).</summary>
    public IReadOnlyList<string> Plan => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.ResearchPlan : (IReadOnlyList<string>)Array.Empty<string>();

    private void OnEnable()
    {
        if (!Trees.Contains(this)) Trees.Add(this);
        Researched += OnResearched;
        Enlightened += OnEnlightened;
    }

    private void OnDisable()
    {
        Trees.Remove(this);
        Researched -= OnResearched;
        Enlightened -= OnEnlightened;
        if (_planSource != null) _planSource.ResearchPlanChanged -= OnPlanChanged;
        _planSource = null;
    }

    private void Start()
    {
        tabBuilderLogic = GetComponentInParent<TabBuilderLogic>();

        if (tabBuilderLogic == null)
        {
            GameLog.Error("TechnologyTreeLogic needs a TabBuilderLogic in its parents; the tree is not built.", LogChannel.Units);
            return;
        }

        tabBuilderLogic.OnUnitAdded += OnUnitAdded;
        InitializeTree(eraTechnologies);
    }

    private void OnDestroy()
    {
        if (tabBuilderLogic != null) tabBuilderLogic.OnUnitAdded -= OnUnitAdded;
    }

    public void InitializeTree(List<TechnologyData> technologies)
    {
        BuildTechTree(technologies);
    }

    private void BuildTechTree(List<TechnologyData> technologies)
    {
        foreach (var techData in technologies)
        {
            if (techData == null || techData.gameUnit == null)
            {
                Instantiate(emptySlotPrefab, slots.transform);

                continue;
            }

            var techSlotObj = tabBuilderLogic.AddNewUnit(techData.gameUnit);

            if (techSlotObj == null || techSlotObj.transform.parent != slots.transform)
            {
                GameLog.Error($"Technology slot '{techData.gameUnit.name}' was not created under '{slots.name}' (is its GameUnit's section this tree's?).", LogChannel.Units);

                continue;
            }
        }

        PlaceInitializationUnit();

        foreach (Transform child in slots.transform)
        {
            if (child.TryGetComponent(out GameTechnologySlot techSlot) && techSlot.gameUnit != null) Adopt(techSlot);
        }

        _connectors = TechTreeConnectors.Create(this, (RectTransform)slots.transform);
        _built = true;
        _waitingGate = WaitingGate();
        Refresh();
    }

    // The research tab creates its first technology before the tree is built; the layout gives it its own cell.
    private void PlaceInitializationUnit()
    {
        if (!tabBuilderLogic.hasInitializationUnit || tabBuilderLogic.initializationUnit == null) return;
        if (!tabBuilderLogic.TryGetSlot(tabBuilderLogic.initializationUnit.name, out var first) || first.transform.parent != slots.transform) return;
        first.transform.SetSiblingIndex(Mathf.Clamp(initializationUnitIndex, 0, slots.transform.childCount - 1));
    }

    private void Adopt(GameTechnologySlot techSlot)
    {
        if (techSlot == null || _slots.Contains(techSlot)) return;

        // Each age has its own tree: the slot refreshes this one, not whichever tree a scene search finds.
        techSlot.technologyTreeLogic = this;
        // Its data now rather than at its Start, so the very first frame already hides what is not uncovered.
        techSlot.EnsureInitializedForSave();
        _slots.Add(techSlot);

        var pointer = techSlot.GetComponent<TechnologySlotPointer>();
        if (pointer == null) pointer = techSlot.gameObject.AddComponent<TechnologySlotPointer>();
        pointer.Bind(this, techSlot);
    }

    // Technologies added to the research tab later (a save's slots, other systems) join the tree they land in.
    private void OnUnitAdded(GameUnit unit, GameObject slotObject)
    {
        if (!_built || slotObject == null || slotObject.transform.parent != slots.transform) return;
        if (!slotObject.TryGetComponent(out GameTechnologySlot techSlot) || techSlot.gameUnit == null) return;
        Adopt(techSlot);
        Refresh();
    }

    // ===== WHAT SHOWS =====

    /// <summary>Re-read the whole tree: what is uncovered, the plan's numbers and the lines.</summary>
    public void Refresh()
    {
        if (!_built) return;
        foreach (var techSlot in _slots)
        {
            if (techSlot == null || techSlot.gameUnit == null) continue;
            techSlot.techState = ToState(VisibilityOf(techSlot));
            techSlot.ApplyVisibility();
        }
        RefreshPlanMarks();
        if (_connectors != null) _connectors.MarkDirty();
    }

    /// <summary>Kept for callers that changed one technology: any change can uncover others, so the whole tree is read.</summary>
    public void DetermineTechnologyVisibility(GameTechnologySlot techSlot) => Refresh();

    public void DetermineTechnologyVisibilityForAllSlots() => Refresh();

    /// <summary>Refresh every tree (after a save is loaded, for instance).</summary>
    public static void RefreshAll()
    {
        foreach (var tree in Trees.ToArray()) if (tree != null) tree.Refresh();
    }

    public TechVisibility VisibilityOf(GameTechnologySlot techSlot)
    {
        if (techSlot == null || techSlot.gameUnit == null) return TechVisibility.Hidden;
        return TechTreeRules.Visibility(techSlot.gameUnit.name, Prerequisites, IsResearched, IsEnlightened, IsRevealed);
    }

    private static TechnologyState ToState(TechVisibility visibility)
    {
        switch (visibility)
        {
            case TechVisibility.Researched: return TechnologyState.Unlocked;
            case TechVisibility.Available: return TechnologyState.CurrentResearchOption;
            case TechVisibility.Preview: return TechnologyState.NextResearchOption;
            default: return TechnologyState.Invisible;
        }
    }

    private void RefreshPlanMarks()
    {
        var plan = Plan;
        var active = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.activeTechnologySlot : null;
        foreach (var techSlot in _slots)
        {
            if (techSlot == null || techSlot.gameUnit == null) continue;
            techSlot.ShowPlanPosition(TechTreeRules.PlanPosition(plan, techSlot.gameUnit.name), techSlot == active);
        }
    }

    // The game asks for the technology the Age waits on (its notice names it), so it shows even before its way does.
    private bool IsRevealed(string technology) => !string.IsNullOrEmpty(_waitingGate) && string.Equals(_waitingGate, technology, StringComparison.OrdinalIgnoreCase);

    private static string WaitingGate() => AgeProgression.Instance != null ? AgeProgression.Instance.WaitingGate : null;

    // ===== ONE GRAPH FOR EVERY TREE =====

    /// <summary>A technology's prerequisites as authored (<see cref="TechnologyData.techRequirements"/>); null for one that exists nowhere.</summary>
    public static IEnumerable<string> Prerequisites(string technology) =>
        !string.IsNullOrWhiteSpace(technology) && GameCatalog.Technologies.TryGet(technology, out var data) && data != null ? data.techRequirements : null;

    public static bool IsResearched(string technology) => GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.IsTechnologyUnlocked(technology);

    public static bool IsEnlightened(string technology)
    {
        var techSlot = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetTechnologySlot(technology) : null;
        return techSlot != null && techSlot.enlightenedCompleted;
    }

    /// <summary>How a technology shows in whichever tree holds it (researched or hidden when no tree does).</summary>
    public static TechVisibility GlobalVisibility(string technology)
    {
        foreach (var tree in Trees)
        {
            var techSlot = tree != null ? tree.Find(technology) : null;
            if (techSlot != null) return tree.VisibilityOf(techSlot);
        }
        return IsResearched(technology) ? TechVisibility.Researched : TechVisibility.Hidden;
    }

    /// <summary>Uncovered (shown) in its tree: tooltips may name it.</summary>
    public static bool IsUncovered(string technology) => TechTreeRules.IsUncovered(GlobalVisibility(technology));

    /// <summary>The uncovered technologies that need this one, in tree order.</summary>
    public static List<string> UncoveredSuccessors(string technology)
    {
        var next = new List<string>();
        foreach (var tree in Trees)
        {
            if (tree == null) continue;
            foreach (var techSlot in tree._slots)
            {
                if (techSlot == null || techSlot.gameUnit == null) continue;
                string name = techSlot.gameUnit.name;
                if (!TechTreeRules.Needs(name, Prerequisites).Contains(technology, StringComparer.OrdinalIgnoreCase)) continue;
                if (TechTreeRules.IsUncovered(tree.VisibilityOf(techSlot)) && !next.Contains(name)) next.Add(name);
            }
        }
        return next;
    }

    private GameTechnologySlot Find(string technology)
    {
        foreach (var techSlot in _slots)
            if (techSlot != null && techSlot.gameUnit != null && string.Equals(techSlot.gameUnit.name, technology, StringComparison.OrdinalIgnoreCase)) return techSlot;
        return null;
    }

    // ===== THE POINTER =====

    /// <summary>The pointer entered (or left) a technology: the lines light the way to it.</summary>
    public void SetHovered(GameTechnologySlot techSlot, bool hovered)
    {
        var next = hovered ? techSlot : (Hovered == techSlot ? null : Hovered);
        if (next == Hovered) return;
        Hovered = next;
        if (_connectors != null) _connectors.MarkDirty();
    }

    // ===== KEEPING UP =====

    private void Update()
    {
        WirePlan();
        if (!_built || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + Mathf.Max(0.1f, enlightenmentCheckSeconds);

        CheckEnlightenment();

        string gate = WaitingGate();
        if (!string.Equals(gate, _waitingGate, StringComparison.OrdinalIgnoreCase))
        {
            _waitingGate = gate;
            Refresh();
        }
    }

    // GameUnitsLogic owns the plan; the tree follows it once the singleton exists.
    private void WirePlan()
    {
        var units = GameUnitsLogic.Instance;
        if (units == _planSource) return;
        if (_planSource != null) _planSource.ResearchPlanChanged -= OnPlanChanged;
        _planSource = units;
        if (_planSource != null) _planSource.ResearchPlanChanged += OnPlanChanged;
        if (_built) RefreshPlanMarks();
    }

    // A technology whose every Enlightenment goal is met is enlightened; the others show how far along they are.
    private void CheckEnlightenment()
    {
        var units = GameUnitsLogic.Instance;
        bool mayEnlighten = units != null && !SaveSession.Restoring && !SaveMenu.BlocksGameplay;
        foreach (var techSlot in _slots.ToArray())
        {
            if (techSlot == null || techSlot.gameUnit == null) continue;
            var data = techSlot.technologyData;
            if (mayEnlighten && !techSlot.isUnlocked && !techSlot.enlightenedCompleted && data != null && data.EnlightenmentMet())
            {
                units.EnlightenTechnology(techSlot, data.DescribeEnlightenment());
                continue;
            }
            techSlot.RefreshLiveText();
        }
    }

    private void OnResearched(GameTechnologySlot techSlot) => Refresh();

    private void OnEnlightened(GameTechnologySlot techSlot, string reason) => Refresh();

    private void OnPlanChanged()
    {
        if (!_built) return;
        RefreshPlanMarks();
        if (_connectors != null) _connectors.MarkDirty();
    }
}
