using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A bonus of a default council seat (<see cref="CouncilSeatData.bonuses"/>).
/// </summary>
[System.Serializable]
public class DefaultSeatBonus
{
    public SeatBonusType bonusType;
    [Tooltip("Target stat for pillar/substat/derived bonuses (e.g., 'waltz', 'legendEffectiveness')")]
    public string targetStat;
    public float modifierValue;
    public ModifierType modifierType;
}

/// <summary>
/// Government type from the political compass, plus the council of legends.
///
/// Compass: see <see cref="GovernmentCompass"/>. Recomputed at the end of any frame in which stats changed.
///
/// Council: a Head of State plus six regular positions, of which <see cref="unlockedSeatCount"/> are open: at first only
/// the Head of State (<see cref="openSeatsAtStart"/>), then technologies open more (TechUnlockableType.CouncilSeat:
/// The Rekindling and Edicts of Stone and Bone, so the council holds three by the end of Act I). Each
/// holds one of the default seats (positions such as High Arbiter, each open to legends of several classes:
/// Resources/Council, <see cref="CouncilSeatData"/>) or a seat granted by an active civic. A seat's bonuses apply as soon as a legend sits in it; the legend's
/// own bonuses apply after <see cref="SeatActivationSevenths"/>. The council's contribution is re-applied from
/// scratch through <see cref="EffectRouter"/> whenever anything about it changes, so it can never stack or leave
/// residue. Timing, phases and scaling are <see cref="CouncilRules"/>.
/// </summary>
public class GovernmentLogic : SingletonBehaviour<GovernmentLogic>
{
    private const LogChannel Log = LogChannel.Council;
    public const int HeadOfStateIndex = -1;
    public const int RegularSeatCount = 6;

    [Header("Threshold Configuration")]
    [SerializeField] private int centristThreshold = 25; // Net difference 0-24 = Centrist
    [SerializeField] private int leaningThreshold = 50; // Net difference 25-49 = Leaning
    [SerializeField] private int pillarThreshold = 75; // Net difference 50-74 = Pillar
    [SerializeField] private int fanaticThreshold = 75; // Net difference 75+ = Fanatic

    [Header("Head of State Configuration")]
    [SerializeField] private float headOfStateMultiplier = 2.0f; // Multiplier for legend bonuses when assigned to Head of State

    [Header("Council Change Cooldowns")]
    [SerializeField] private int councilSeatCooldownSevenths = 1;
    [SerializeField] private int headOfStateCooldownSevenths = 3;

    [Tooltip("Regular council positions open at the start besides the Head of State (max 6); technologies open the rest. The default seats themselves are assets in Resources/Council.")]
    [SerializeField] private int openSeatsAtStart;

    // Regular positions open now (saved; starts at openSeatsAtStart).
    private int unlockedSeatCount;

    // ===== STATE =====

    private CouncilSeat _headOfState;
    private readonly CouncilSeat[] activeRegularSeats = new CouncilSeat[RegularSeatCount];
    private readonly List<CouncilSeat> defaultSeatTemplates = new List<CouncilSeat>();
    // Civic name → seat template registered while the civic is active.
    private readonly Dictionary<string, CouncilSeat> civicCouncilSeats = new Dictionary<string, CouncilSeat>(StringComparer.OrdinalIgnoreCase);

    // Change cooldowns per position (Head of State = -1); they only ever count down or restart.
    private readonly Countdowns<int> _cooldowns = new Countdowns<int>();

    private int waltzRegaliaCoordinate;
    private int chorusAureusCoordinate;
    private GovernmentType currentGovernmentType = GovernmentType.TrueCentrist;

    // Council application
    private bool _applyingCouncil;
    private bool _councilDirty;
    private bool _governmentDirty;
    private float lastKnownLegendEffectiveness = -1f;
    private readonly HashSet<string> _appliedCouncilSources = new HashSet<string>();

    // ===== EVENTS =====

    public System.Action<GovernmentType, string, string> OnGovernmentChanged; // (type, name, description)
    public System.Action<int, int> OnCoordinatesChanged;                      // (waltzRegalia, chorusAureus)
    public System.Action<GovernmentType> OnGovernmentTypeChangedForCivics;
    public System.Action<CouncilSeat> OnCouncilSeatUnlocked;
    public System.Action<CouncilSeat> OnCouncilSeatChanged;
    public System.Action<CouncilSeat, LegendData> OnLeaderAssigned;
    public System.Action<CouncilSeat> OnLeaderRemoved;
    public System.Action OnCouncilCompositionChanged;
    public System.Action OnCivicPoolChanged;
    public System.Action OnLeaderPoolChanged;
    /// <summary>A seat's activation timer finished and its legend's bonuses now apply.</summary>
    public System.Action<CouncilSeat> OnCouncilSeatActivated;

    /// <summary>Sevenths a newly seated legend waits before its bonuses apply (configured on LegendLeaderLogic).</summary>
    public int SeatActivationSevenths => LegendLeaderLogic.Instance != null ? Mathf.Max(0, LegendLeaderLogic.Instance.GetSeventhsForActivation()) : 3;

    /// <summary>How much a Head of State's legend bonuses are multiplied (tooltips read it from here).</summary>
    public float HeadOfStateMultiplier => headOfStateMultiplier;

    public static string LegendSource(LegendData legend) => $"Legend: {legend.legendName}";

    public static string SeatSource(CouncilSeat seat) => $"Council Seat: {seat.GetEffectiveTitle()}";

    private static readonly LegendClass[] AllLegendClasses = (LegendClass[])Enum.GetValues(typeof(LegendClass));

    // ===== LIFECYCLE =====

    private void Start()
    {
        ContentValidator.ValidateAll();
        InitializeCouncilSystem();

        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnDerivedChanged += OnDerivedStatChanged;
            StatManager.Instance.OnStatsChanged += OnStatsChanged;
            lastKnownLegendEffectiveness = StatManager.Instance.GetDerivedValue("legendEffectiveness");
        }
        TimeSystemLogic.WhenReady(this, time => time.OnSeventhChange += ProcessSeventhChange);

        CalculateGovernmentCoordinates();
        DetermineGovernmentType();
        ApplyCouncil();
    }

    protected override void OnSingletonDestroy()
    {
        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnDerivedChanged -= OnDerivedStatChanged;
            StatManager.Instance.OnStatsChanged -= OnStatsChanged;
        }
        if (TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= ProcessSeventhChange;
    }

    private void LateUpdate()
    {
        if (_councilDirty) ApplyCouncil();
        if (_governmentDirty)
        {
            _governmentDirty = false;
            CalculateGovernmentCoordinates();
            DetermineGovernmentType();
        }
    }

    private void OnStatsChanged() => _governmentDirty = true;

    /// <summary>
    /// Legend bonuses scale with Legend Effectiveness, so a change from outside the council
    /// (civics, weather, events) re-applies it. Changes made by the council itself are ignored.
    /// </summary>
    private void OnDerivedStatChanged(string statName, float newValue)
    {
        if (_applyingCouncil || !string.Equals(statName, "legendEffectiveness", StringComparison.OrdinalIgnoreCase)) return;
        if (Mathf.Abs(newValue - lastKnownLegendEffectiveness) <= 0.001f) return;
        _councilDirty = true;
    }

    /// <summary>Request a council re-application at the end of the frame.</summary>
    public void MarkCouncilDirty() => _councilDirty = true;

    // ===== COOLDOWNS =====

    /// <summary>True when the seat's legend can be changed now. Empty seats are never on cooldown.</summary>
    public bool CanChangeSeat(int seatIndex) => GetSeatCooldownRemaining(seatIndex) <= 0;

    /// <summary>Sevenths until the seat can change again (0 when ready or empty).</summary>
    public int GetSeatCooldownRemaining(int seatIndex)
    {
        var seat = GetCouncilSeat(seatIndex);
        return seat == null || seat.assignedLegend == null ? 0 : _cooldowns.Remaining(seatIndex);
    }

    private void StartCooldown(int seatIndex) => _cooldowns.Start(seatIndex, CouncilRules.CooldownFor(seatIndex, councilSeatCooldownSevenths, headOfStateCooldownSevenths));

    /// <summary>Validate a change request from another system and start the seat's cooldown if allowed.</summary>
    public bool RequestSeatChange(int seatIndex, string requestingSystem = "Unknown")
    {
        if (!CanChangeSeat(seatIndex))
        {
            GameLog.Event($"Seat change by {requestingSystem} denied: seat {seatIndex} on cooldown for {GetSeatCooldownRemaining(seatIndex)} sevenths", Log);
            return false;
        }
        StartCooldown(seatIndex);
        OnLeaderPoolChanged?.Invoke();
        return true;
    }

    public Dictionary<int, int> GetAllSeatCooldowns()
    {
        var cooldowns = new Dictionary<int, int> { [HeadOfStateIndex] = GetSeatCooldownRemaining(HeadOfStateIndex) };
        for (int i = 0; i < RegularSeatCount; i++) cooldowns[i] = GetSeatCooldownRemaining(i);
        return cooldowns;
    }

    public void ForceResetAllCooldowns()
    {
        _cooldowns.Clear();
        OnLeaderPoolChanged?.Invoke();
    }

    // ===== POLITICAL COMPASS =====

    public void CalculateGovernmentCoordinates()
    {
        var stats = StatManager.Instance;
        if (stats == null) return;

        int oldX = waltzRegaliaCoordinate, oldY = chorusAureusCoordinate;
        waltzRegaliaCoordinate = GovernmentCompass.AxisCoordinate(stats.GetPillarValue("regalia") - stats.GetPillarValue("waltz"), centristThreshold, leaningThreshold, pillarThreshold);
        chorusAureusCoordinate = GovernmentCompass.AxisCoordinate(stats.GetPillarValue("chorus") - stats.GetPillarValue("aureus"), centristThreshold, leaningThreshold, pillarThreshold);

        if (oldX != waltzRegaliaCoordinate || oldY != chorusAureusCoordinate)
        {
            GameLog.Event($"Political compass ({oldX},{oldY}) → ({waltzRegaliaCoordinate},{chorusAureusCoordinate})", Log);
            OnCoordinatesChanged?.Invoke(waltzRegaliaCoordinate, chorusAureusCoordinate);
        }
    }

    public void DetermineGovernmentType()
    {
        var oldType = currentGovernmentType;
        currentGovernmentType = GovernmentCompass.Classify(waltzRegaliaCoordinate, chorusAureusCoordinate);
        if (oldType == currentGovernmentType) return;

        GameLog.Event($"Government {GovernmentCompass.Name(oldType)} → {GovernmentCompass.Name(currentGovernmentType)}", Log);
        OnGovernmentChanged?.Invoke(currentGovernmentType, GovernmentCompass.Name(currentGovernmentType), GovernmentCompass.Description(currentGovernmentType));
        OnGovernmentTypeChangedForCivics?.Invoke(currentGovernmentType);
    }

    public (GovernmentType type, string name, string description, int waltzRegalia, int chorusAureus) GetCurrentGovernment()
    {
        return (currentGovernmentType, GovernmentCompass.Name(currentGovernmentType), GovernmentCompass.Description(currentGovernmentType), waltzRegaliaCoordinate, chorusAureusCoordinate);
    }

    public (int waltzRegalia, int chorusAureus) GetCurrentCoordinates() => (waltzRegaliaCoordinate, chorusAureusCoordinate);

    public (int centrist, int leaning, int pillar, int fanatic) GetThresholds() => (centristThreshold, leaningThreshold, pillarThreshold, fanaticThreshold);

    /// <summary>Recompute the compass now and log the full government state.</summary>
    [ContextMenu("Log Government Info")]
    public void ForceRecalculation()
    {
        CalculateGovernmentCoordinates();
        DetermineGovernmentType();
        LogGovernmentInfo();
    }

    public bool CheckGovernmentTypeRequirement(string requiredGovernmentType)
    {
        return string.IsNullOrEmpty(requiredGovernmentType) || string.Equals(currentGovernmentType.ToString(), requiredGovernmentType, StringComparison.OrdinalIgnoreCase);
    }

    public string GetCurrentGovernmentTypeString() => currentGovernmentType.ToString();

    // ===== COUNCIL SETUP =====

    private void InitializeCouncilSystem()
    {
        _headOfState = new CouncilSeat("Head of State", HeadOfStateIndex, true)
        {
            isUnlocked = true,
            roleplayDescription = "The supreme leader of the civilization, representing the will of the people and the authority of the state."
        };
        _headOfState.allowedLegendClasses.AddRange(AllLegendClasses);

        defaultSeatTemplates.Clear();
        foreach (var data in GameCatalog.CouncilSeats.All.Where(s => s != null && !string.IsNullOrEmpty(s.title)).OrderBy(s => s.order).ThenBy(s => s.title))
        {
            defaultSeatTemplates.Add(CreateSeatFromData(data));
        }

        unlockedSeatCount = Mathf.Clamp(openSeatsAtStart, 0, RegularSeatCount);
        for (int i = 0; i < RegularSeatCount; i++)
        {
            activeRegularSeats[i] = i < unlockedSeatCount && i < defaultSeatTemplates.Count ? CreateSeatFromTemplate(defaultSeatTemplates[i], i) : null;
        }
        if (unlockedSeatCount > defaultSeatTemplates.Count)
        {
            GameLog.Warning($"{unlockedSeatCount} seats are unlocked but only {defaultSeatTemplates.Count} default seats are configured.", Log);
        }
        GameLog.Event($"Council ready: Head of State + {unlockedSeatCount} open positions, {defaultSeatTemplates.Count} default seat templates", Log);
    }

    private static CouncilSeat CreateSeatFromData(CouncilSeatData data)
    {
        var seat = new CouncilSeat(data.title, -999) { roleplayDescription = data.description, seatIcon = data.icon, requiredStars = Math.Max(0, Math.Min(LegendGreats.MaxStars, data.requiredStars)) };
        if (data.allowedClasses != null) seat.allowedLegendClasses.AddRange(data.allowedClasses);
        if (data.areas != null) seat.areas.AddRange(data.areas);
        foreach (var bonus in data.bonuses ?? Array.Empty<DefaultSeatBonus>())
        {
            if (bonus == null) continue;
            seat.seatBonuses.Add(new SeatBonus
            {
                bonusType = bonus.bonusType,
                targetStat = bonus.targetStat,
                modifierValue = bonus.modifierValue,
                modifierType = bonus.modifierType
            });
        }
        return seat;
    }

    /// <summary>A fresh seat instance for a council position, carrying everything that identifies its template.</summary>
    private static CouncilSeat CreateSeatFromTemplate(CouncilSeat template, int position)
    {
        var seat = new CouncilSeat(template.seatTitle, position)
        {
            isUnlocked = true,
            seatIcon = template.seatIcon,
            roleplayDescription = template.roleplayDescription,
            sourceCivic = template.sourceCivic,
            civicSeatTitle = template.civicSeatTitle,
            requiredStars = template.requiredStars
        };
        seat.allowedLegendClasses.AddRange(template.allowedLegendClasses);
        seat.areas.AddRange(template.areas);
        foreach (var bonus in template.seatBonuses)
        {
            seat.seatBonuses.Add(new SeatBonus
            {
                bonusType = bonus.bonusType,
                targetStat = bonus.targetStat,
                modifierValue = bonus.modifierValue,
                modifierType = bonus.modifierType,
                description = bonus.GetAutoDescription()
            });
        }
        return seat;
    }

    // ===== SEAT ACCESS =====

    /// <summary>Head of State (-1) or the seat at a regular position (0-5). Null for locked positions.</summary>
    public CouncilSeat GetCouncilSeat(int seatIndex)
    {
        if (seatIndex == HeadOfStateIndex) return _headOfState;
        return seatIndex >= 0 && seatIndex < RegularSeatCount ? activeRegularSeats[seatIndex] : null;
    }

    public CouncilSeat GetSeatAtPosition(int position) => position >= 0 && position < RegularSeatCount ? activeRegularSeats[position] : null;

    /// <summary>The six regular positions; locked positions are null.</summary>
    public CouncilSeat[] GetActiveRegularSeats() => activeRegularSeats;

    /// <summary>Head of State followed by every seated regular position.</summary>
    private IEnumerable<CouncilSeat> SeatedCouncil()
    {
        if (_headOfState != null) yield return _headOfState;
        foreach (var seat in activeRegularSeats)
        {
            if (seat != null && seat.isUnlocked) yield return seat;
        }
    }

    /// <summary>Every seated council seat plus the templates of registered civic seats.</summary>
    public List<CouncilSeat> GetAllCouncilSeats() => SeatedCouncil().Concat(civicCouncilSeats.Values).ToList();

    public List<CouncilSeat> GetUnlockedCouncilSeats() => SeatedCouncil().ToList();

    public int GetUnlockedCouncilSeatCount() => SeatedCouncil().Count();

    public int GetUnlockedSeatCount() => unlockedSeatCount;

    public bool CanUnlockCouncilSeat(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < RegularSeatCount && activeRegularSeats[seatIndex] == null && unlockedSeatCount < RegularSeatCount;
    }

    /// <summary>Open a locked regular position with the first default seat not already in the council.</summary>
    public bool UnlockCouncilSeat(int seatIndex)
    {
        if (!CanUnlockCouncilSeat(seatIndex))
        {
            GameLog.Warning($"Council position {seatIndex} cannot be unlocked (already open, out of range, or all positions open).", Log);
            return false;
        }
        var template = FindAvailableDefaultTemplate();
        if (template == null)
        {
            GameLog.Warning("No default seat template is configured; cannot open a council position.", Log);
            return false;
        }
        var seat = CreateSeatFromTemplate(template, seatIndex);
        activeRegularSeats[seatIndex] = seat;
        unlockedSeatCount++;

        OnCouncilSeatUnlocked?.Invoke(seat);
        OnCouncilSeatChanged?.Invoke(seat);
        OnCouncilCompositionChanged?.Invoke();
        MarkCouncilDirty();
        GameLog.Event($"Opened council position {seatIndex}: {seat.GetEffectiveTitle()} ({unlockedSeatCount}/{RegularSeatCount})", Log);
        return true;
    }

    /// <summary>Open the lowest locked position. Returns false when every position is open.</summary>
    public bool UnlockNextCouncilSeat()
    {
        for (int i = 0; i < RegularSeatCount; i++)
        {
            if (activeRegularSeats[i] == null) return UnlockCouncilSeat(i);
        }
        return false;
    }

    private CouncilSeat FindAvailableDefaultTemplate()
    {
        foreach (var template in defaultSeatTemplates)
        {
            if (!IsSeatTitleActive(template.seatTitle)) return template;
        }
        return defaultSeatTemplates.Count > 0 ? defaultSeatTemplates[0] : null;
    }

    // ===== COUNCIL EFFECTS =====

    /// <summary>Re-apply the whole council now (normally coalesced to the end of the frame).</summary>
    public void ProcessAllSeatBonuses() => ApplyCouncil();

    [ContextMenu("Force Clear and Reapply All Seat Bonuses")]
    public void ForceClearAndReapplyAllSeatBonuses() => ApplyCouncil();

    private IEnumerable<CouncilRules.SeatState> SeatStates()
    {
        foreach (var seat in SeatedCouncil())
        {
            var legend = seat.assignedLegend;
            yield return new CouncilRules.SeatState
            {
                seatSource = SeatSource(seat),
                seatBonuses = seat.seatBonuses,
                hasLegend = legend != null,
                legendSource = legend != null ? LegendSource(legend) : null,
                legendBonuses = legend != null ? legend.bonuses : null,
                seventhsUntilActive = seat.seventhsUntilActive,
                isHeadOfState = seat.isHeadOfState,
                legendGrowth = legend != null && LegendProgress.Instance != null ? LegendProgress.Instance.CouncilMultiplier(legend.legendName) : 1f
            };
        }
    }

    /// <summary>Remove everything the council applied last time and apply the current council, phase by phase (<see cref="CouncilRules"/>).</summary>
    private void ApplyCouncil()
    {
        _councilDirty = false;
        if (_applyingCouncil) return;
        _applyingCouncil = true;
        try
        {
            foreach (var source in _appliedCouncilSources) EffectRouter.RemoveSource(source);
            _appliedCouncilSources.Clear();

            var effects = CouncilRules.Collect(SeatStates(), headOfStateMultiplier);
            // Authority and direct Legend Effectiveness first: they feed the LE that scales the rest.
            foreach (var entry in effects.Where(e => e.Phase != CouncilRules.Phase.Scaled)) ApplyCouncilEffect(entry, 1f);

            float legendEffectiveness = StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f;
            float effectivenessMultiplier = CouncilRules.EffectivenessMultiplier(legendEffectiveness);
            foreach (var entry in effects.Where(e => e.Phase == CouncilRules.Phase.Scaled)) ApplyCouncilEffect(entry, effectivenessMultiplier);

            GameLog.Event($"Council applied: {effects.Count} effects from {_appliedCouncilSources.Count} sources (LE {legendEffectiveness:F1}%)", Log);
        }
        finally
        {
            lastKnownLegendEffectiveness = StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f;
            _applyingCouncil = false;
        }
    }

    private void ApplyCouncilEffect(in CouncilRules.CouncilEffect entry, float effectivenessMultiplier)
    {
        if (EffectRouter.Apply(entry.effect, entry.source, CouncilRules.Multiplier(entry, effectivenessMultiplier))) _appliedCouncilSources.Add(entry.source);
    }

    // ===== LEGEND ASSIGNMENT =====

    public bool ValidateCouncilSeatOperation(int seatIndex, string operation)
    {
        if (GetCouncilSeat(seatIndex) != null) return true;
        GameLog.Warning($"No council seat at index {seatIndex} for {operation}.", Log);
        return false;
    }

    /// <summary>The seat is open and the legend qualifies for it (class, or Head of State eligibility).</summary>
    public bool CanAssignLegendToSeat(LegendData legend, int seatIndex)
    {
        var seat = GetCouncilSeat(seatIndex);
        return legend != null && seat != null && seat.isUnlocked && seat.CanAssignLegend(legend);
    }

    /// <summary>
    /// Seat a legend (<see cref="CouncilRules.PlanAssignment"/>). If the legend already sits elsewhere and the target
    /// seat is occupied, the two legends swap when the occupant qualifies for the other seat; otherwise the legend
    /// moves. The seat's bonuses apply at once; the legend's own bonuses after <see cref="SeatActivationSevenths"/>.
    /// </summary>
    public bool AssignLegendToSeat(LegendData legend, int seatIndex, bool bypassCooldown = false)
    {
        var seat = GetCouncilSeat(seatIndex);
        var previousSeat = legend != null ? GetSeatWithLegend(legend.legendName) : null;
        var occupant = seat?.assignedLegend;
        var plan = CouncilRules.PlanAssignment(
            targetOnCooldown: !bypassCooldown && !CanChangeSeat(seatIndex),
            eligible: CanAssignLegendToSeat(legend, seatIndex),
            alreadyInTarget: occupant != null && occupant == legend,
            hasPreviousSeat: previousSeat != null,
            previousOnCooldown: previousSeat != null && !bypassCooldown && !CanChangeSeat(previousSeat.seatIndex),
            targetOccupied: occupant != null,
            occupantFitsPreviousSeat: previousSeat != null && occupant != null && previousSeat.CanAssignLegend(occupant));

        switch (plan)
        {
            case CouncilRules.Assignment.TargetOnCooldown:
                GameLog.Event($"Assignment blocked: seat {seatIndex} on cooldown for {GetSeatCooldownRemaining(seatIndex)} sevenths", Log);
                return false;
            case CouncilRules.Assignment.NotEligible:
                GameLog.Event($"{legend?.legendName ?? "null"} cannot sit in seat {seatIndex}", Log);
                return false;
            case CouncilRules.Assignment.AlreadySeated:
                return true;
            case CouncilRules.Assignment.PreviousSeatOnCooldown:
                GameLog.Event($"Assignment blocked: {legend.legendName} is locked into {DescribeSeat(previousSeat)} by its cooldown", Log);
                return false;
            case CouncilRules.Assignment.Swap:
                SeatLegend(seat, legend);
                SeatLegend(previousSeat, occupant);
                StartCooldown(seat.seatIndex);
                StartCooldown(previousSeat.seatIndex);
                NotifyCouncilChanged(seat, previousSeat);
                GameLog.Event($"Swapped {legend.legendName} → {DescribeSeat(seat)} and {occupant.legendName} → {DescribeSeat(previousSeat)}", Log);
                return true;
        }

        if (previousSeat != null) VacateSeat(previousSeat, startCooldown: true);
        if (occupant != null)
        {
            seat.RemoveLegend();
            OnLeaderRemoved?.Invoke(seat);
            GameLog.Event($"{occupant.legendName} left {DescribeSeat(seat)}", Log);
        }
        SeatLegend(seat, legend);
        StartCooldown(seatIndex);

        NotifyCouncilChanged(seat);
        GameLog.Event($"Seated {legend.legendName} as {DescribeSeat(seat)}: seat bonuses now, legend active in {seat.seventhsUntilActive} sevenths", Log);
        return true;
    }

    private void SeatLegend(CouncilSeat seat, LegendData legend)
    {
        seat.assignedLegend = legend;
        seat.seventhsUntilActive = SeatActivationSevenths;
        OnLeaderAssigned?.Invoke(seat, legend);
    }

    private void VacateSeat(CouncilSeat seat, bool startCooldown)
    {
        if (seat.assignedLegend == null) return;
        seat.RemoveLegend();
        if (startCooldown) StartCooldown(seat.seatIndex);
        OnLeaderRemoved?.Invoke(seat);
        OnCouncilSeatChanged?.Invoke(seat);
    }

    /// <summary>Unseat the legend at a position. Its bonuses disappear immediately.</summary>
    public bool RemoveLegendFromSeat(int seatIndex, bool bypassCooldown = false)
    {
        if (!bypassCooldown && !CanChangeSeat(seatIndex))
        {
            GameLog.Event($"Removal blocked: seat {seatIndex} on cooldown for {GetSeatCooldownRemaining(seatIndex)} sevenths", Log);
            return false;
        }
        var seat = GetCouncilSeat(seatIndex);
        if (seat == null || seat.assignedLegend == null) return false;

        string legendName = seat.assignedLegend.legendName;
        VacateSeat(seat, startCooldown: true);
        NotifyCouncilChanged();
        GameLog.Event($"Removed {legendName} from {DescribeSeat(seat)}", Log);
        return true;
    }

    private void NotifyCouncilChanged(params CouncilSeat[] changedSeats)
    {
        foreach (var seat in changedSeats) OnCouncilSeatChanged?.Invoke(seat);
        OnCouncilCompositionChanged?.Invoke();
        OnLeaderPoolChanged?.Invoke();
        MarkCouncilDirty();
        int seated = activeRegularSeats.Count(seat => seat != null && seat.assignedLegend != null);
        Achievements.Report(AchievementEvent.Of(AchievementSignal.CouncilChanged, seated, _headOfState != null && _headOfState.assignedLegend != null)
            .From($"council:{seated}", _headOfState?.assignedLegend != null ? _headOfState.assignedLegend.legendName : null));
    }

    private static string DescribeSeat(CouncilSeat seat) => seat.seatIndex == HeadOfStateIndex ? "Head of State" : $"{seat.GetEffectiveTitle()} (position {seat.seatIndex})";

    public CouncilSeat GetSeatWithLegend(string legendName)
    {
        if (string.IsNullOrEmpty(legendName)) return null;
        return SeatedCouncil().FirstOrDefault(seat => seat.assignedLegend != null && seat.assignedLegend.legendName == legendName);
    }

    /// <summary>Legends that qualify for a seat, hiding those locked into a seat that is on cooldown.</summary>
    public List<LegendData> GetAvailableLegendsForSeat(int seatIndex)
    {
        var seat = GetCouncilSeat(seatIndex);
        if (seat == null) return new List<LegendData>();
        return GetAllAvailableLegends().Where(legend =>
        {
            if (!seat.CanAssignLegend(legend)) return false;
            var current = GetSeatWithLegend(legend.legendName);
            return current == null || CanChangeSeat(current.seatIndex);
        }).ToList();
    }

    public List<LegendData> GetAllAvailableLegends() => LegendLeaderLogic.Instance != null ? LegendLeaderLogic.Instance.GetAvailableLegends() : new List<LegendData>();

    /// <summary>The Head of State's legend, or null.</summary>
    public string HeadOfStateLegend => _headOfState != null && _headOfState.assignedLegend != null ? _headOfState.assignedLegend.legendName : null;

    /// <summary>
    /// The seated legend who answers for an area of affairs (<see cref="CouncilAreaRules.Find"/>): a seat that covers
    /// it, else the closest related one. Not found when no seat can; the caller decides on the Head of State.
    /// </summary>
    public CouncilAreaRules.Answer AnswerFor(string area, Func<string, bool> free = null)
    {
        var seats = SeatedCouncil().Where(s => s != null && !s.isHeadOfState).Select(s => new CouncilAreaRules.SeatView
        {
            title = s.GetEffectiveTitle(),
            areas = s.areas,
            holder = s.assignedLegend != null ? s.assignedLegend.legendName : null,
            order = s.seatIndex,
        });
        return CouncilAreaRules.Find(area, seats, CouncilAreaCatalog.Current, CouncilAreaCatalog.CurrentMaxDistance, free);
    }

    public List<(CouncilSeat seat, LegendData legend)> GetAllAssignedLegends()
    {
        return SeatedCouncil().Where(seat => seat.assignedLegend != null).Select(seat => (seat, seat.assignedLegend)).ToList();
    }

    // ===== SEVENTH TICK =====

    /// <summary>Count down cooldowns and activation timers; seats that finish activating join the council.</summary>
    public void ProcessSeventhChange(int newSeventh)
    {
        _cooldowns.Tick();

        foreach (var seat in SeatedCouncil().ToList())
        {
            if (seat.assignedLegend == null || seat.seventhsUntilActive <= 0) continue;
            seat.ProcessSeventh();
            if (seat.seventhsUntilActive > 0) continue;
            GameLog.Event($"{seat.assignedLegend.legendName} is now active as {DescribeSeat(seat)}", Log);
            OnCouncilSeatActivated?.Invoke(seat);
            OnCouncilSeatChanged?.Invoke(seat);
            MarkCouncilDirty();
        }
        OnLeaderPoolChanged?.Invoke();
    }

    // ===== CIVIC SEATS =====

    /// <summary>Make an active civic's council seat available for any regular position.</summary>
    public void RegisterCivicCouncilPosition(CivicData civic)
    {
        if (civic == null || !civic.grantsCouncilPosition) return;
        var (isValid, error) = civic.ValidateLegendClassRestrictions();
        if (!isValid) GameLog.Warning($"{error} All legend classes are allowed until it is fixed.", Log);

        var position = civic.councilPosition;
        string title = !string.IsNullOrEmpty(position?.title) ? position.title : civic.civicName;
        var seat = new CouncilSeat(title, -999)
        {
            isUnlocked = true,
            sourceCivic = civic,
            civicSeatTitle = title,
            seatIcon = position?.icon != null ? position.icon : civic.icon,
            roleplayDescription = position != null ? position.GetAutoDescription() : civic.civicName
        };

        bool anyClass = position == null || position.allowAnyLegendClass || position.allowedClasses == null || position.allowedClasses.Length == 0;
        seat.allowedLegendClasses.AddRange(anyClass ? AllLegendClasses : position.allowedClasses);
        seat.requiredStars = Math.Max(0, Math.Min(LegendGreats.MaxStars, position?.requiredStars ?? 0));
        if (position?.areas != null) seat.areas.AddRange(position.areas);

        foreach (var bonus in position?.bonuses ?? Array.Empty<CivicSeatBonus>())
        {
            if (bonus == null) continue;
            seat.seatBonuses.Add(new SeatBonus
            {
                bonusType = bonus.bonusType,
                targetStat = bonus.targetStat,
                modifierValue = bonus.modifierValue,
                modifierType = bonus.modifierType,
                description = bonus.GetAutoDescription()
            });
        }

        civicCouncilSeats[civic.civicName] = seat;
        OnCivicPoolChanged?.Invoke();
        GameLog.Event($"Civic seat '{title}' from {civic.civicName} is available ({string.Join(", ", seat.allowedLegendClasses)})", Log);
    }

    /// <summary>Withdraw a removed civic's seat; a position holding it falls back to a default seat.</summary>
    public void UnregisterCivicCouncilPosition(CivicData civic)
    {
        if (civic == null || !civicCouncilSeats.Remove(civic.civicName)) return;
        for (int i = 0; i < RegularSeatCount; i++)
        {
            if (activeRegularSeats[i]?.sourceCivic == civic) ResetSeatToDefault(i);
        }
        OnCivicPoolChanged?.Invoke();
        GameLog.Event($"Civic seat of {civic.civicName} withdrawn", Log);
    }

    /// <summary>True when the civic currently provides a council seat.</summary>
    public bool IsCivicUnlocked(string civicName) => civicName != null && civicCouncilSeats.ContainsKey(civicName);

    /// <summary>Every active civic (seat-granting or not).</summary>
    public List<string> GetUnlockedCivicNames()
    {
        return CivicManager.Instance != null ? CivicManager.Instance.GetAllActiveCivics().Select(c => c.civicName).ToList() : civicCouncilSeats.Keys.ToList();
    }

    public List<string> GetLockedCivicNames()
    {
        if (CivicManager.Instance == null) return new List<string>();
        var active = new HashSet<string>(GetUnlockedCivicNames(), StringComparer.OrdinalIgnoreCase);
        return CivicManager.Instance.GetAllAvailableCivicNames().Where(name => !active.Contains(name)).ToList();
    }

    public (List<string> unlocked, List<string> locked, List<string> availableForSeats) GetCivicStatus() => (GetUnlockedCivicNames(), GetLockedCivicNames(), GetAvailableSeatTitles());

    public List<CouncilSeat> GetCivicCouncilSeats() => civicCouncilSeats.Values.ToList();

    public List<string> GetAvailableCivicSeatNames() => civicCouncilSeats.Keys.ToList();

    public int GetTotalCouncilSeatCount() => GetUnlockedCouncilSeatCount() + civicCouncilSeats.Count;

    /// <summary>Unlock a civic through <see cref="CivicManager"/> (which registers its seat, if any).</summary>
    public bool UnlockCivic(string civicName, string source = "GovernmentLogic")
    {
        if (CivicManager.Instance == null)
        {
            GameLog.Error("CivicManager not found; cannot unlock civics.", Log);
            return false;
        }
        return CivicManager.Instance.IsCivicActive(civicName) || CivicManager.Instance.UnlockCivic(civicName, source);
    }

    public (bool canUnlock, List<string> reasons) CanUnlockCivic(string civicName)
    {
        return CivicManager.Instance != null ? CivicManager.Instance.CanUnlockCivic(civicName) : (false, new List<string> { "CivicManager not found" });
    }

    // ===== SEAT REPLACEMENT =====

    /// <summary>Every seat that can fill a position: default seats, then active civic seats.</summary>
    private IEnumerable<CouncilSeat> SeatPool() => defaultSeatTemplates.Concat(civicCouncilSeats.Values);

    private CouncilSeat FindSeatInPool(string seatTitle) => SeatPool().FirstOrDefault(seat => seat.seatTitle == seatTitle);

    public List<string> GetAvailableSeatTitles() => SeatPool().Select(seat => seat.seatTitle).ToList();

    public bool CanReplaceSeatAtPosition(int position)
    {
        var seat = GetSeatAtPosition(position);
        return seat != null && seat.isUnlocked;
    }

    public bool ReplaceSeatWithAvailable(int position, string seatTitle)
    {
        if (!CanReplaceSeatAtPosition(position)) return false;
        var template = FindSeatInPool(seatTitle);
        if (template == null)
        {
            GameLog.Warning($"Seat '{seatTitle}' is not available.", Log);
            return false;
        }
        if (IsSeatInUseAtOtherPosition(seatTitle, position))
        {
            GameLog.Event($"Seat '{seatTitle}' is already in the council", Log);
            return false;
        }
        ReplaceSeatAtPosition(position, template);
        return true;
    }

    public bool ReplaceSeatWithCivic(int position, string civicName)
    {
        if (!CanReplaceSeatAtPosition(position) || civicName == null || !civicCouncilSeats.TryGetValue(civicName, out var template)) return false;
        return ReplaceSeatWithAvailable(position, template.seatTitle);
    }

    /// <summary>Put a different seat at a position. A legend sitting there is unseated (no cooldown applies).</summary>
    private void ReplaceSeatAtPosition(int position, CouncilSeat template)
    {
        var oldSeat = activeRegularSeats[position];
        if (oldSeat?.assignedLegend != null) VacateSeat(oldSeat, startCooldown: false);

        var newSeat = CreateSeatFromTemplate(template, position);
        activeRegularSeats[position] = newSeat;
        NotifyCouncilChanged(newSeat);
        GameLog.Event($"Position {position}: {oldSeat?.GetEffectiveTitle() ?? "empty"} → {newSeat.GetEffectiveTitle()}", Log);
    }

    /// <summary>Exchange two positions, legends and activation state included. Cooldowns stay with positions.</summary>
    public bool SwapSeats(int position1, int position2)
    {
        if (!CanReplaceSeatAtPosition(position1) || !CanReplaceSeatAtPosition(position2)) return false;
        if (position1 == position2) return true;

        var seat1 = activeRegularSeats[position1];
        var seat2 = activeRegularSeats[position2];
        seat1.seatIndex = position2;
        seat2.seatIndex = position1;
        activeRegularSeats[position1] = seat2;
        activeRegularSeats[position2] = seat1;
        NotifyCouncilChanged(seat1, seat2);
        return true;
    }

    private bool IsSeatInUseAtOtherPosition(string seatTitle, int excludePosition)
    {
        for (int i = 0; i < RegularSeatCount; i++)
        {
            if (i != excludePosition && activeRegularSeats[i] != null && activeRegularSeats[i].seatTitle == seatTitle) return true;
        }
        return false;
    }

    public bool IsSeatTitleActive(string seatTitle) => GetSeatPosition(seatTitle) >= 0;

    public int GetSeatPosition(string seatTitle)
    {
        for (int i = 0; i < RegularSeatCount; i++)
        {
            if (activeRegularSeats[i] != null && activeRegularSeats[i].seatTitle == seatTitle) return i;
        }
        return -1;
    }

    /// <summary>Put the first default seat not already in the council at a position.</summary>
    public bool ResetSeatToDefault(int position)
    {
        if (!CanReplaceSeatAtPosition(position)) return false;
        var current = activeRegularSeats[position];
        var template = defaultSeatTemplates.FirstOrDefault(t => !IsSeatInUseAtOtherPosition(t.seatTitle, position)) ?? defaultSeatTemplates.FirstOrDefault();
        if (template == null)
        {
            GameLog.Warning($"No default seat template to reset position {position}.", Log);
            return false;
        }
        if (current != null && current.sourceCivic == null && current.seatTitle == template.seatTitle) return true;
        ReplaceSeatAtPosition(position, template);
        return true;
    }

    public List<int> GetReplaceablePositions() => Enumerable.Range(0, RegularSeatCount).Where(CanReplaceSeatAtPosition).ToList();

    public (int unlockedCount, int totalPositions, List<string> availableSeats, List<string> activeSeats) GetSeatConfigurationInfo()
    {
        var active = new List<string>();
        for (int i = 0; i < RegularSeatCount; i++)
        {
            if (activeRegularSeats[i] != null) active.Add($"{i}: {activeRegularSeats[i].GetEffectiveTitle()}");
        }
        return (unlockedSeatCount, RegularSeatCount, GetAvailableSeatTitles(), active);
    }

    public List<(int position, string title, string type, bool isUnlocked, bool hasLegend, string legendName)> GetDetailedSeatInfo()
    {
        var info = new List<(int, string, string, bool, bool, string)>();
        for (int i = 0; i < RegularSeatCount; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat == null) continue;
            info.Add((i, seat.GetEffectiveTitle(), DescribeSeatType(seat), seat.isUnlocked, seat.assignedLegend != null, seat.assignedLegend?.legendName ?? ""));
        }
        return info;
    }

    public List<(string title, string type, bool isCurrentlyActive)> GetReplacementOptions(int position)
    {
        return SeatPool().Select(seat => (seat.seatTitle, DescribeSeatType(seat), IsSeatTitleActive(seat.seatTitle))).ToList();
    }

    private static string DescribeSeatType(CouncilSeat seat) => seat.sourceCivic != null ? $"Civic: {seat.sourceCivic.civicName}" : "Default";

    // ===== DISPLAY =====

    public (string title, string effects, string leaderClasses, Sprite icon) GetSeatDisplayInfo(string seatTitle)
    {
        var seat = FindSeatInPool(seatTitle);
        if (seat == null) return (seatTitle, "Effects: N/A", "Leader Classes: N/A", null);
        string effects = seat.seatBonuses.Count > 0 ? string.Join("\n", seat.seatBonuses.Select(b => b.GetAutoDescription())) : "No bonuses";
        return (seat.GetEffectiveTitle(), effects, FormatLeaderClasses(seat.allowedLegendClasses, seat.requiredStars), seat.seatIcon);
    }

    public CivicData GetCivicDataForSeatTitle(string seatTitle) => civicCouncilSeats.Values.FirstOrDefault(seat => seat.seatTitle == seatTitle)?.sourceCivic;

    private static string FormatLeaderClasses(List<LegendClass> allowedClasses, int requiredStars)
    {
        if ((allowedClasses == null || allowedClasses.Count == 0) && requiredStars > 0) return "No Greats allowed";
        return "Council Position for " + LegendGreats.Requirement(allowedClasses, requiredStars);
    }

    // ===== DEBUG =====

    public void LogGovernmentInfo()
    {
        if (!GameLog.IsEnabled(Log)) return;
        var stats = StatManager.Instance;
        GameLog.Event($"=== GOVERNMENT: {GovernmentCompass.Name(currentGovernmentType)} at ({waltzRegaliaCoordinate},{chorusAureusCoordinate}) ===", Log);
        if (stats != null)
        {
            GameLog.Event($"Pillars: waltz {stats.GetPillarValue("waltz")}, regalia {stats.GetPillarValue("regalia")}, chorus {stats.GetPillarValue("chorus")}, aureus {stats.GetPillarValue("aureus")}", Log);
        }
        GameLog.Event($"Thresholds: centrist {centristThreshold}, leaning {leaningThreshold}, pillar {pillarThreshold}", Log);
        LogCouncil();
    }

    private void LogCouncil()
    {
        foreach (var seat in SeatedCouncil())
        {
            string legend = seat.assignedLegend != null ? $"{seat.assignedLegend.legendName} ({(seat.IsActive() ? "active" : $"active in {seat.seventhsUntilActive}")})" : "empty";
            GameLog.Event($"  {DescribeSeat(seat)} [{DescribeSeatType(seat)}]: {legend}, cooldown {GetSeatCooldownRemaining(seat.seatIndex)}", Log);
        }
        GameLog.Event($"  Seat pool: {string.Join(", ", GetAvailableSeatTitles())}", Log);
        GameLog.Event($"  Council sources: {string.Join(", ", _appliedCouncilSources)}", Log);
    }

    public void LogCivicPipeline()
    {
        if (!GameLog.IsEnabled(Log)) return;
        var (unlocked, locked, seats) = GetCivicStatus();
        GameLog.Event($"Civics active: {string.Join(", ", unlocked)} | inactive: {string.Join(", ", locked)} | seat pool: {string.Join(", ", seats)}", Log);
    }

    [ContextMenu("Debug All Council Seats")]
    public void DebugAllCouncilSeats()
    {
        LogCouncil();
        foreach (var seat in SeatedCouncil())
        {
            foreach (var bonus in seat.seatBonuses)
            {
                GameLog.Event($"  {seat.GetEffectiveTitle()}: {bonus.GetAutoDescription()}", Log);
            }
        }
    }

    [ContextMenu("Debug Cooldown System")]
    public void DebugCooldownSystem()
    {
        foreach (var pair in GetAllSeatCooldowns()) GameLog.Event($"  Seat {pair.Key}: {(pair.Value > 0 ? $"{pair.Value} sevenths" : "ready")}", Log);
    }

    [ContextMenu("Force Reset All Cooldowns")]
    public void DebugForceResetAllCooldowns() => ForceResetAllCooldowns();

    [ContextMenu("Unlock Next Council Seat")]
    private void DebugUnlockNextCouncilSeat() => UnlockNextCouncilSeat();
}
