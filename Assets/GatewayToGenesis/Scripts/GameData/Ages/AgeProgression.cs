using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What one Age left behind: how its crisis went and where the world went next (in memory until save/load exists).</summary>
public class AgeRecord
{
    public string ageId, ageTitle, crisisTitle, nextAgeId, nextAgeTitle;
    public int ageNumber, populationBefore, deaths, eraScore;
    /// <summary>Era Score earned in each Act of Fate (Ages.md: the Act that earned the most shapes how history remembers the Age).</summary>
    public List<int> eraScoreByAct = new List<int>();
    public float severity;
    public List<CrisisFactor> factors = new List<CrisisFactor>();

    public int Survivors => Math.Max(0, populationBefore - deaths);
}

/// <summary>
/// The passage of the Ages: the loop resources → technology → world → legends → Acts of Fate → the Age Crisis
/// halfway through the Age → the next Age. Keeps the Age clock (sevenths into the current Age, advanced by
/// <see cref="TimeSystemLogic.OnSeventhChange"/>, so pausing stops it), and asks <see cref="AgeRules"/> what is due.
/// The technology tree drives it: an Act boundary or crisis stage gated by a technology
/// (<see cref="AgeDefinition.actTechnologies"/>, <see cref="CrisisStageSpec.technology"/>) waits until it is
/// researched and opens at once when researched early (<see cref="AgeRules.Advance"/>); only ungated beats move by time.
/// Era Score (Ages.md) is kept per Act: Event Technologies and the world's great deeds award it
/// (<see cref="AwardEraScore"/>), and a surplus eases the crisis. Enlightening an Event or Crisis Technology earns
/// <see cref="AgeDefinition.enlightenedTechnologyEraScore"/> too.
///
/// - **Acts of Fate** at each boundary between Acts: the Age's story for that boundary is offered.
/// - **Crisis stages** from halfway on: each stage's story is offered; once a stage is declared the crisis is named
///   on the HUD and cuts Food output while it lasts (the vault: "Age Crisis never announce themselves").
/// - **The end of the Age**: once every queued story has been told, the crisis resolves (<see cref="CrisisRules"/>:
///   deaths from its severity, never below the survivor floor), the Age is recorded, and the next Age begins
///   (<see cref="GameAge.Set"/>), bringing its new features to the world map.
///
/// Stories are authored with <c># locked: true</c> and a one-shot condition; they are unlocked one at a time and the
/// next waits until the previous one has been completed, so no beat is lost behind another notification.
/// Created by <see cref="GenesisLoop"/>; no scene setup. Nothing here is saved yet (S01 is on hold).
/// </summary>
public class AgeProgression : SingletonBehaviour<AgeProgression>
{
    private const LogChannel Log = LogChannel.Ages;
    /// <summary>Sevenths a crisis story may wait at the Age's end before the passage goes ahead without it.</summary>
    private const int StoryPatienceSevenths = 42;

    public AgeDefinition Current { get; private set; }
    /// <summary>Sevenths into the current Age.</summary>
    public int Sevenths { get; private set; }
    /// <summary>The crisis stage reached (0-based), -1 before the crisis begins.</summary>
    public int StageReached { get; private set; } = -1;
    public bool CrisisDeclared { get; private set; }
    /// <summary>The Age's time is spent; it passes once its last stories have been told.</summary>
    public bool Ending { get; private set; }

    public int Act => Current != null ? AgeRules.ActAt(Sevenths, Current.actSevenths) : 0;
    public float Progress => Current != null ? AgeRules.Progress(Sevenths, Current.actSevenths) : 0f;
    public bool CrisisBegun => StageReached >= 0;
    public HarvestQuality Harvest => CrisisRules.Harvest(CrisisBegun, CrisisDeclared);
    public CrisisStageSpec CurrentStage => Current != null && StageReached >= 0 && StageReached < Current.crisisStages.Count ? Current.crisisStages[StageReached] : null;
    public IReadOnlyList<AgeRecord> History => _history;
    /// <summary>The Age story offered and not yet told (null when none is waiting).</summary>
    public string WaitingStory => _waitingStory;
    /// <summary>The technology the Age is waiting for before it can go on (null when time moves it).</summary>
    public string WaitingGate => Current != null ? AgeRules.WaitingGate(_schedule, Gates, _nextBeat, Sevenths, Researched) : null;
    /// <summary>The next technology that moves the Age (the one it waits for, or the next gate ahead), or null.</summary>
    public string NextGate
    {
        get
        {
            var gates = Gates;
            for (int i = _nextBeat; gates != null && i < gates.Count; i++) if (!string.IsNullOrEmpty(gates[i])) return gates[i];
            return null;
        }
    }

    /// <summary>Era Score earned in this Age, and in the current Act.</summary>
    public int EraScore => _eraScoreByAct.Sum();
    public int EraScoreThisAct => Act < _eraScoreByAct.Count ? _eraScoreByAct[Act] : 0;
    public IReadOnlyList<int> EraScoreByAct => _eraScoreByAct;
    /// <summary>The last awards, newest first ("+2 The Rekindling (Event Technology)").</summary>
    public IReadOnlyList<string> EraScoreLog => _eraLog;
    /// <summary>Every award of Era Score since the world began, dated by Age, Act, Cycle, Echo, Phase and Seventh (<see cref="EraTimeline"/>).</summary>
    public IReadOnlyList<EraAward> EraTimelineAwards => _eraTimeline;
    /// <summary>Era Score was awarded: points and why.</summary>
    public event Action<int, string> EraScoreAwarded;

    /// <summary>Anything shown on the Age banner changed (clock, stage, Age).</summary>
    public event Action Changed;
    public event Action<AgeDefinition> AgeBegan;
    /// <summary>An Age ended: its record (the new Age has already begun when this is raised).</summary>
    public event Action<AgeRecord> AgePassed;

    private readonly List<AgeRecord> _history = new List<AgeRecord>();
    private readonly Queue<string> _stories = new Queue<string>();
    private List<AgeBeat> _schedule = new List<AgeBeat>();
    private int _nextBeat;
    private string _waitingStory;
    private int _waitingBaseline, _waitedSevenths;
    private float _appliedFoodPercent;
    private string _appliedCrisisSource;
    private bool _subscribed, _begun, _resting;
    private List<int> _eraScoreByAct = new List<int>();
    private List<string> _eraLog = new List<string>();
    // Every award, never cleared (the Chronicle); older saves have none.
    [SaveOptionalField] private List<EraAward> _eraTimeline = new List<EraAward>();
    private List<string> _gates;
    private string _gatesFor;

    // Derived from the Age and its schedule (never saved: a restored schedule re-derives them).
    private List<string> Gates
    {
        get
        {
            if (Current == null) return null;
            string stamp = Current.id + ":" + _schedule.Count;
            if (_gates == null || _gatesFor != stamp)
            {
                _gates = Current.Gates(_schedule);
                _gatesFor = stamp;
            }
            return _gates;
        }
    }

    private static bool Researched(string technology)
    {
        var units = GameUnitsLogic.Instance;
        return units != null && units.IsTechnologyUnlocked(technology);
    }

    private static IEnumerable<string> Prerequisites(string technology) =>
        GameCatalog.Technologies.TryGet(technology, out var data) && data != null ? data.techRequirements : Enumerable.Empty<string>();

    // ===== THE AGE MEASURED BY ITS TECHNOLOGY TREE =====

    /// <summary>
    /// The technologies that end the current Act (the one opening the next Act and its prerequisites not on an earlier
    /// Act's path), or null when time ends it (an Act with no technology yet, or the last Act).
    /// </summary>
    public List<string> ActPath => Current != null ? AgeRules.ActPath(Act, Current.actTechnologies, Prerequisites) : null;

    /// <summary>
    /// How much of the current Act has passed, 0-1: the share of <see cref="ActPath"/> researched, or of the Act's
    /// sevenths when time ends it. <paramref name="done"/>/<paramref name="total"/> count the path (0/0 for time).
    /// </summary>
    public float ActShare(out int done, out int total)
    {
        done = total = 0;
        if (Current == null) return 0f;
        if (Ending) return 1f;
        var path = ActPath;
        if (path != null && path.Count > 0)
        {
            total = path.Count;
            done = path.Count(Researched);
            return done / (float)total;
        }
        var acts = Current.actSevenths;
        int act = Act, start = AgeRules.ActStart(act, acts), length = act < acts.Count ? Math.Max(1, acts[act]) : 1;
        return Mathf.Clamp01((Sevenths - start) / (float)length);
    }

    /// <summary>How far through the Age, 0-1, as the banner's bar shows it: each Act an equal share, filled by <see cref="ActShare"/>.</summary>
    public float TreeProgress => Current != null ? AgeRules.BarProgress(Act, Math.Max(1, Current.actSevenths.Count), ActShare(out _, out _)) : 0f;

    // ===== LIFECYCLE =====

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time =>
        {
            time.OnSeventhChange += OnSeventh;
            _subscribed = true;
        });
        GameTechnologySlot.Researched += OnResearched;
        GameTechnologySlot.Enlightened += OnEnlightened;
        if (!_begun) Begin(StartingAge());
    }

    protected override void OnSingletonDestroy()
    {
        if (_subscribed && TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventh;
        GameTechnologySlot.Researched -= OnResearched;
        GameTechnologySlot.Enlightened -= OnEnlightened;
        RemoveCrisisEffect();
    }

    // A technology may open a gate of the Age; an Event Technology is a great deed of the Age.
    private void OnResearched(GameTechnologySlot slot)
    {
        if (Current == null || slot == null) return;
        string name = slot.gameUnit != null ? slot.gameUnit.name : slot.name;
        if (slot.technologyData != null && slot.technologyData.isEventTech && Current.eventTechnologyEraScore > 0)
            AwardEraScore(Current.eventTechnologyEraScore, $"{name} (Event Technology)");
        if (Current.advantageTechnologies != null && Current.advantageTechnologies.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
            GameLog.Event($"{name} grants {Current.advantagesPerTechnology} Crisis Advantages against {Current.crisisTitle}", Log);
        Advance(0);
    }

    // Enlightening an Event or Crisis Technology is a great deed of the Age as well (the owner's rule: 1); others earn nothing.
    private void OnEnlightened(GameTechnologySlot slot, string reason)
    {
        if (Current == null || slot == null) return;
        int points = TechTreeRules.EnlightenmentEraScore(slot.IsEventTechnology, slot.IsCrisisTechnology, Current.enlightenedTechnologyEraScore);
        if (points <= 0) return;
        string name = slot.gameUnit != null ? slot.gameUnit.name : slot.name;
        AwardEraScore(points, $"{name} enlightened ({(slot.IsCrisisTechnology ? "Crisis" : "Event")} Technology)");
    }

    // ===== ERA SCORE =====

    /// <summary>Award Era Score to the current Act (Ages.md: "which Act of Fate generated the most Era Score").</summary>
    public void AwardEraScore(int points, string reason)
    {
        if (Current == null || points == 0 || SaveSession.Restoring) return;
        while (_eraScoreByAct.Count <= Act) _eraScoreByAct.Add(0);
        _eraScoreByAct[Act] += points;
        _eraLog.Insert(0, $"{points:+0;-0} {reason}");
        if (_eraLog.Count > 12) _eraLog.RemoveAt(_eraLog.Count - 1);
        // The Chronicle: dated to the calendar and the Act.
        var time = TimeSystemLogic.Instance;
        _eraTimeline = _eraTimeline ?? new List<EraAward>();
        _eraTimeline.Add(new EraAward
        {
            points = points, reason = reason, ageId = Current.id, ageTitle = Current.title, ageNumber = Current.number, act = Act, ageSeventh = Sevenths,
            cycle = time != null ? time.CurrentCycle : 1, echo = time != null ? time.CurrentEcho : 1, phase = time != null ? time.CurrentPhase : 1,
            seventh = time != null ? time.CurrentSeventh : 1, cycleName = time?.CycleName, order = _eraTimeline.Count,
        });
        GameLog.Event($"Era Score {points:+0;-0}: {reason} (Act {Act + 1}: {EraScoreThisAct}, Age: {EraScore})", Log);
        EraScoreAwarded?.Invoke(points, reason);
        Changed?.Invoke();
    }

    /// <summary>Award Era Score from anywhere (no-op before the Ages start).</summary>
    public static void Award(int points, string reason) => Instance?.AwardEraScore(points, reason);

    private static AgeDefinition StartingAge()
    {
        if (GameCatalog.Ages.TryGet(GameAge.Id, out var age)) return age;
        return GameCatalog.Ages.All.OrderBy(a => a.number).FirstOrDefault();
    }

    private void Update()
    {
        if (SaveMenu.BlocksGameplay) return;
        if (_waitingStory != null) PumpStories();

        // Developer shortcuts (Ctrl, editor and development builds): Ctrl+G skips to the next beat, Ctrl+H a twelfth of the Age.
        if (Current == null) return;
        if (InputUtils.DebugKeyDown(Key.G)) Advance(Math.Max(1, NextBeatAt() - Sevenths));
        if (InputUtils.DebugKeyDown(Key.H)) Advance(Math.Max(1, Current.Length / 12));
    }

    private void OnSeventh(int _)
    {
        if (_waitingStory != null) _waitedSevenths++;
        Advance(1);
    }

    // ===== THE CLOCK =====

    /// <summary>Start <paramref name="age"/> from its first seventh.</summary>
    public void Begin(AgeDefinition age)
    {
        if (age == null)
        {
            GameLog.Warning("No Age to begin: add an AgeDefinition to Resources/Ages.", Log);
            return;
        }
        _begun = true;
        _resting = false;
        RemoveCrisisEffect();
        Current = age;
        Sevenths = 0;
        StageReached = -1;
        CrisisDeclared = false;
        Ending = false;
        _schedule = age.Schedule();
        _nextBeat = 0;
        _gates = null;
        _eraScoreByAct = Enumerable.Repeat(0, Math.Max(1, age.actSevenths.Count)).ToList();
        _eraLog = new List<string>();
        GameAge.Set(age.id, age.number);
        GameLog.Event($"{age.title} begins: {age.Length} sevenths, {age.actSevenths.Count} Acts, crisis '{age.crisisTitle}' ({string.Join(", ", _schedule)})", Log);
        Enqueue(age.openingStory);
        AgeBegan?.Invoke(age);
        PumpStories();
        Changed?.Invoke();
    }

    /// <summary>
    /// Move the Age clock forward (one seventh per seventh of game time; more from developer shortcuts; 0 to check
    /// the technology gates only). A closed gate holds the clock; a researched one opens at once.
    /// </summary>
    public void Advance(int sevenths)
    {
        if (Current == null) return;
        if (!Ending)
        {
            int clock = Sevenths;
            var due = AgeRules.Advance(_schedule, Gates, ref _nextBeat, ref clock, Math.Max(0, sevenths), Researched);
            Sevenths = clock;
            foreach (var beat in due) Handle(beat);
        }
        RefreshCrisisEffect();
        PumpStories();
        Changed?.Invoke();
    }

    private int NextBeatAt() => _nextBeat < _schedule.Count ? _schedule[_nextBeat].at : Sevenths + 1;

    private void Handle(AgeBeat beat)
    {
        switch (beat.kind)
        {
            case AgeBeatKind.ActOfFate:
                GameLog.Event($"{Current.title}: {Current.ActLabel(beat.index)} begins with an Act of Fate", Log);
                // An entry may queue several stories in order ("desolation_echoes_of_hunger, desolation_golden_orchard").
                if (beat.index - 1 < Current.actOfFateStories.Count)
                    foreach (var knot in (Current.actOfFateStories[beat.index - 1] ?? string.Empty).Split(',')) Enqueue(knot);
                if (LegendProgress.Instance != null)
                {
                    LegendProgress.Instance.HonourCouncil(LegendLore.FragmentTuning.actOfFate, $"Stood through an Act of Fate in the {Current.title}");
                    LegendProgress.Instance.ServeSeats($"Served through an Act of Fate in the {Current.title}");
                }
                break;
            case AgeBeatKind.CrisisStage:
                StageReached = beat.index;
                var stage = Current.crisisStages[beat.index];
                if (stage.declared && !CrisisDeclared)
                {
                    CrisisDeclared = true;
                    GameLog.Event($"The Age Crisis is named: {Current.crisisTitle}", Log);
                }
                GameLog.Event($"{Current.crisisTitle}: stage {beat.index + 1} ({stage.name})", Log);
                Enqueue(stage.story);
                break;
            case AgeBeatKind.AgeEnds:
                Ending = true;
                GameLog.Event($"{Current.title}: its time is spent; waiting for {_stories.Count + (_waitingStory != null ? 1 : 0)} stor{(_stories.Count == 1 ? "y" : "ies")} before the passage", Log);
                break;
        }
    }

    // ===== STORIES =====

    private void Enqueue(string knot)
    {
        if (!string.IsNullOrWhiteSpace(knot)) _stories.Enqueue(knot.Trim());
    }

    /// <summary>
    /// Offer the queued stories one at a time: a story is unlocked (and the event check asked to offer it) only once the
    /// previous one has been completed. At the Age's end, the passage follows the last story.
    /// </summary>
    private void PumpStories()
    {
        var events = EventSystemLogic.Instance;
        var volumes = EventVolumeManager.Instance;

        if (_waitingStory != null)
        {
            bool told = events != null && events.GetCompletionCount(_waitingStory) > _waitingBaseline;
            bool giveUp = Ending && _waitedSevenths >= StoryPatienceSevenths && (events == null || !events.IsEventActive());
            if (!told && !giveUp) return;
            if (giveUp && !told) GameLog.Warning($"'{_waitingStory}' was never told; the Age passes without it.", Log);
            _waitingStory = null;
        }

        while (_stories.Count > 0)
        {
            string knot = _stories.Dequeue();
            if (events == null || volumes == null || !volumes.UnlockStory(knot))
            {
                GameLog.Warning($"Age story '{knot}' was not found in Resources/Events; skipped.", Log);
                continue;
            }
            _waitingStory = knot;
            _waitingBaseline = events.GetCompletionCount(knot);
            _waitedSevenths = 0;
            GameLog.Event($"Offering Age story '{knot}'", Log);
            events.TriggerEventCheck();
            return;
        }

        if (Ending && (events == null || !events.IsEventActive())) Pass();
    }

    // ===== THE CRISIS =====

    /// <summary>Everyone the crisis can reach: citizens and vagrants (the vault's famine counts every hamlet, caravan and ruin-dweller).</summary>
    public static int People
    {
        get
        {
            var pop = PopGrowthLogic.Instance;
            return pop != null ? pop.population + pop.vagrants : 0;
        }
    }

    /// <summary>What the civilization brings into the crisis now.</summary>
    public CrisisInputs GatherInputs()
    {
        var inputs = new CrisisInputs();
        if (Current == null) return inputs;
        var units = GameUnitsLogic.Instance;
        var events = EventSystemLogic.Instance;
        inputs.population = People;
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        inputs.foodStored = (units != null && food != null ? units.GetResourceAmountExact(food) : 0f) + Pantry.StoredValue;
        var pantry = Pantry.Instance;
        inputs.storeVariety = pantry != null ? pantry.Variety : 0;
        inputs.peachShare = pantry != null ? pantry.PeachShare : 0f;
        inputs.preparedTechnologies = units != null ? Current.preparationTechnologies.Count(t => !string.IsNullOrEmpty(t) && units.IsTechnologyUnlocked(t)) : 0;
        inputs.preparationPoints = Score(events, Current.preparationScore);
        inputs.reliefPoints = Score(events, Current.reliefScore);
        inputs.aggravationPoints = Score(events, Current.aggravationScore);
        inputs.easingTiles = WorldSystem.Instance != null && !string.IsNullOrEmpty(Current.easingFeatureTag) ? WorldSystem.Instance.ExploredWithTag(Current.easingFeatureTag) : 0;
        inputs.advantages = units != null && Current.advantageTechnologies != null
            ? Current.advantageTechnologies.Count(t => !string.IsNullOrEmpty(t) && units.IsTechnologyUnlocked(t)) * Math.Max(0, Current.advantagesPerTechnology) : 0;
        inputs.eraScore = EraScore;
        return inputs;
    }

    private static int Score(EventSystemLogic events, string score) => events != null && !string.IsNullOrEmpty(score) ? events.GetEventScore(score) : 0;

    /// <summary>The severity breakdown now (empty without a crisis).</summary>
    public List<CrisisFactor> CurrentFactors() => Current != null && Current.HasCrisis ? CrisisRules.Factors(GatherInputs(), Current.tuning, Current.Labels) : new List<CrisisFactor>();

    public float CurrentSeverity => CrisisRules.Severity(CurrentFactors());

    // While a declared crisis lasts, Food output falls with its severity (re-applied only when the number changes).
    private void RefreshCrisisEffect()
    {
        if (Current == null || !CrisisDeclared || Ending)
        {
            RemoveCrisisEffect();
            return;
        }
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        if (food == null) return;
        float percent = Mathf.Round(CrisisRules.FoodOutputPercent(CurrentSeverity, Current.tuning));
        string source = $"Age Crisis: {Current.crisisTitle}";
        if (source == _appliedCrisisSource && Mathf.Approximately(percent, _appliedFoodPercent)) return;
        RemoveCrisisEffect();
        EffectRouter.ApplySet(source, new[] { new GameEffect(GameEffectType.ResourceModifier, percent, ModifierType.Percentage, food) });
        _appliedCrisisSource = source;
        _appliedFoodPercent = percent;
    }

    private void RemoveCrisisEffect()
    {
        if (_appliedCrisisSource == null) return;
        EffectRouter.RemoveSource(_appliedCrisisSource);
        _appliedCrisisSource = null;
        _appliedFoodPercent = 0f;
    }

    // ===== THE PASSAGE =====

    /// <summary>The crisis resolves and the next Age begins. Runs once per Age (Ending is cleared by Begin).</summary>
    private void Pass()
    {
        if (!Ending || Current == null || _resting) return;
        var ended = Current;
        var record = new AgeRecord { ageId = ended.id, ageTitle = ended.title, ageNumber = ended.number, crisisTitle = ended.crisisTitle, eraScore = EraScore, eraScoreByAct = new List<int>(_eraScoreByAct) };
        var pop = PopGrowthLogic.Instance;
        record.populationBefore = People;

        if (ended.HasCrisis)
        {
            record.factors = CurrentFactors();
            record.severity = CrisisRules.Severity(record.factors);
            record.deaths = CrisisRules.Deaths(record.populationBefore, record.severity, ended.tuning);
            if (pop != null && record.deaths > 0)
            {
                // The homeless starve first, then the citizens.
                int vagrants = Math.Min(pop.vagrants, record.deaths);
                if (vagrants > 0) pop.ModifyVagrants(-vagrants);
                if (record.deaths > vagrants) pop.ProcessEventDeaths(record.deaths - vagrants);
            }
        }
        RemoveCrisisEffect();

        string nextId = CrisisRules.NextAge(ended.NextAgeTable(), record.severity);
        AgeDefinition next = null;
        if (nextId != null && !GameCatalog.Ages.TryGet(nextId, out next)) GameLog.Warning($"{ended.title}: next Age '{nextId}' has no asset in Resources/Ages.", Log);
        record.nextAgeId = next != null ? next.id : null;
        record.nextAgeTitle = next != null ? next.title : null;
        _history.Add(record);
        GameLog.Event($"{ended.title} passes: {record.deaths} of {record.populationBefore} lost (severity {record.severity:P0}); next: {record.nextAgeTitle ?? "none yet"}", Log);

        if (LegendProgress.Instance != null) LegendProgress.Instance.HonourCouncil(LegendLore.FragmentTuning.crisisCarried, $"Carried the people through {ended.crisisTitle}", againstTheOdds: true);

        if (next != null) Begin(next);
        else
        {
            // The chronicle has no further Age yet: the clock stops at the end of this one.
            Ending = true;
            _resting = true;
            _stories.Clear();
            GameLog.Event("No further Age is authored yet; the chronicle rests here.", Log);
        }
        // Reported once the passage has committed (the record kept, the next Age begun); the pass count keys it.
        Achievements.Report(AchievementEvent.Of(AchievementSignal.AgeSurvived, ended.number).From($"age-passed:{ended.id}:{_history.Count}", ended.id));
        AgePassed?.Invoke(record);
        Changed?.Invoke();
    }

    /// <summary>True when the current Age has ended with no further Age authored.</summary>
    public bool ChronicleRests => _resting;
}
