using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The world beyond the capital (roadmap S07/S09, section 3.1): the generated world (<see cref="WorldGenerator"/>),
/// the units that walk it, what they find, what held land yields and how each Age moves the magic.
///
/// - Units (<see cref="WorldUnits"/>, <see cref="WorldSettings.units"/>): expeditions of legends explore and, escorting
///   settlers, found Developing Towns, Outposts and Religious Havens (<see cref="Expeditions"/>), and improve
///   resource hotspots once <see cref="ExpeditionSettings.improveTechnology"/> is known: everything on the map is
///   legend-run, and each deed pays its legends Lyrical Fragments (<see cref="FragmentTuning"/>). An expedition forms at
///   a settlement around a Director, takes on companions while slots allow (<see cref="ExpeditionSlots"/>: Government
///   Capacity and Hollow Watchposts), and walks as one party. Units walk the micro hexes by travel fatigue (<see cref="MicroGrid"/>): each spends its
///   stamina per Seventh, continuously, and stops while time is paused or a story is open. An order given at the meso
///   scale heads for the cell's centre hex (or the nearest one it can reach), at the micro scale for the hex itself.
///   An expedition comes to know the hexes around each one it enters; surveys (<see cref="UnitAbilities"/>) survey
///   micro hexes, and a cell whose every hex is surveyed is explored: its sites pay their reward, may bring a legend
///   and may offer a story.
/// - The road and the legends: an expedition's hardship strains its members' Composure every Seventh
///   (<see cref="HardshipOf"/>, read by <see cref="LegendProgress"/>), and when things go bad mishaps strike its
///   legends (<see cref="Expeditions.RollMishap"/>). Worn out entirely, it breaks: its legends limp home strained and
///   any settlers are lost. A legend lost to Dissonance on the road leaves its companions grieving.
/// - Provisions (<see cref="WorldUnits.Needs"/>, <see cref="WorldSettings.provisions"/>): units eat rations, drawn from
///   your stores inside your authority and gathered from the land in camp; they tire, rest in camp and wear down with
///   hunger, strain, harsh weather, danger and dissonance. Exhausted, they make camp by themselves; worn out, they are
///   lost. Warnings reach the notices once each (<see cref="UnitNotice"/>).
/// - The map opens with <see cref="WorldSettings.mapTechnology"/> (Pathfinder Training), which also sends out the first
///   expedition free (<see cref="ExpeditionSettings.firstFree"/>). Authority begins at the Capital's own cell; explored
///   wilderness bordering it is claimed cell by cell for resources (<see cref="Claim"/>).
/// - Land (<see cref="WorldUnits.LandYields"/>): the ground of held, explored cells and every explored feature yield
///   every second, raised by builders' improvements; settlements yield by City Development
///   (<see cref="WorldCivilization.Yields"/>); all flat production through <see cref="EffectRouter"/>.
/// - Great deeds award Era Score (<see cref="AgeProgression.Award"/>): a town, an outpost, a haven, a Trade Nexus
///   settled, a Major Settlement, a suzerainty, landmarks and Sacred Sites found, an enclave met, a masterwork.
/// - Each new Age turns the Grand Thread Rings (<see cref="WorldMagic.Apply"/>) and places its own features,
///   grandfields, threats and enclaves (<see cref="WorldSites.PlaceAge"/>).
///
/// Created by <see cref="GenesisLoop"/>; no scene setup.
/// </summary>
public partial class WorldSystem : SingletonBehaviour<WorldSystem>
{
    private const LogChannel Log = LogChannel.World;
    /// <summary>A journey of at least this many micro hexes (about nine meso cells) counts as completed (the Rekindling's "Complete an Expedition").</summary>
    public const int JourneySteps = 24;
    /// <summary>The simulation's step, in Sevenths: needs, work and walking advance by whole steps, whatever the frame rate.</summary>
    private const float Quantum = 1f / 60f;
    // Time not yet simulated (saved, so a save never loses a partial step).
    private float _unitTimeRemainder;

    public WorldSettings Settings { get; private set; }
    public WorldMap Map { get; private set; }
    /// <summary>The legend who led the last expedition to find a story (the <c>fragment:leader</c> role when no one else is cast).</summary>
    public string LastLeader { get; private set; }
    /// <summary>What the map says happened last, for the world view and the Age banner.</summary>
    public string LastNotice { get; private set; }
    /// <summary>Journeys of <see cref="JourneySteps"/> or more micro hexes completed by any unit.</summary>
    public int JourneysCompleted => _journeys;

    public event Action Changed;
    public event Action<string> Notice;
    /// <summary>Something happened to one unit worth a notice of its own (title, text): hunger, exhaustion, attrition, loss.</summary>
    public event Action<WorldUnit, string, string> UnitNotice;

    private readonly HashSet<int> _agesPlaced = new HashSet<int>();
    // Echoes of the game's clock the land has grown through (sterile blooms sprouting, turning, fading; saved).
    [SaveOptionalField] private int _echoesGrown;
    private readonly List<string> _yieldSources = new List<string>();
    private bool _subscribed, _yieldsDirty;
    private float _yieldsAt;
    // The civilization's map state (the same lists as the map's; saved here, handed back to the map on restore).
    private List<Settlement> _settlements = new List<Settlement>();
    private List<TradeRoute> _routes = new List<TradeRoute>();
    private List<Enclave> _enclaves = new List<Enclave>();
    private List<WorldUnit> _units = new List<WorldUnit>();
    private List<int> _claims = new List<int>();
    // Land adopted by territorial pull (the map's list), the fraction of the next cell adopted or lost, and the player's border policy (saved).
    private List<int> _adopted = new List<int>();
    private Dictionary<string, float> _adoptionProgress = new Dictionary<string, float>();
    private float _driftProgress;
    private BorderPolicy _borderPolicy = BorderPolicy.Measured;
    // Claims paid for whose hexes were still being settled (the map's list; a claim takes its land at once now, so only
    // older saves carry any), the time those claims kept toward their next hex (retired, kept so older saves load), and
    // the territory rolls made so far (seeds the next, so a save replays the same fortune).
    [SaveOptionalField] private List<int> _claiming = new List<int>();
#pragma warning disable CS0169, CS0414, CS0649
    [SaveOptionalField] private float _claimProgress;
#pragma warning restore CS0169, CS0414, CS0649
    [SaveOptionalField] private int _adoptionRolls;
    // The realm's administration, recomputed at most once a second or when the civilization changed.
    private RealmReport _realm;
    private int _realmVersion = -1;
    private float _realmAt;
    // Units already given free when their technology was researched (unit ids).
    private List<string> _granted = new List<string>();
    // How far each kind of site's expedition line has been told (feature id: knots offered).
    private Dictionary<string, int> _expeditionLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int _journeys, _nextUnitId;
    // Deeds already rewarded with Era Score (a place is honoured once).
    private HashSet<string> _eraAwarded = new HashSet<string>();
    // Each expedition's unit for its party (derived: never saved), rebuilt when the party or its Director's binding changes.
    private readonly Dictionary<int, (string key, UnitSpec from, UnitSpec spec)> _partySpecs = new Dictionary<int, (string, UnitSpec, UnitSpec)>();
    // The first expedition, sent free when the map opens (a key in _granted).
    private const string FirstExpedition = "first-expedition";

    public SettlementRules Rules => Settings != null ? Settings.settlements : null;

    public ProvisionRules Provisions => Settings != null ? Settings.provisions : null;

    public ExpeditionSettings ExpeditionRules => Settings != null ? Settings.expeditions : null;

    // ===== LIFECYCLE =====

    protected override void OnSingletonAwake()
    {
        Settings = GameCatalog.World.All.FirstOrDefault();
        if (Settings == null)
        {
            GameLog.Warning("No WorldSettings in Resources/World: the world map is disabled.", Log);
            return;
        }
        var gen = Settings.generation;
        var stencil = LoadStencil(gen);
        if (stencil == null) return;
        var tiles = LoadTiles(gen);
        int seed = SaveSession.GenerationSeed ?? (gen.fixedSeed != 0 ? gen.fixedSeed : Environment.TickCount & 0x7fffffff);
        Map = WorldGenerator.Generate(seed, gen, stencil, tiles);
        _settlements = Map.Settlements;
        _routes = Map.Routes;
        _enclaves = Map.Enclaves;
        _units = Map.Units;
        _claims = Map.Claims;
        _adopted = Map.Adopted;
        _claiming = Map.Claiming;
        _plantings = Map.Plantings;
        _resourceSites = Map.ResourceSites;
        _populations = Map.Populations;
        _behaviors = Map.Behaviors;
        _outbreaks = Map.Outbreaks;
        _ruins = Map.Ruins;
        Map.territoryRules = Rules.territory;
        Map.settlementRules = Rules;
        // The world ended once already: the Old World's ruins and broken roads (the roads are laid again from the seed on
        // every load; the ruins are saved with what became of them).
        WorldRuins.PlaceOldWorld(Map, gen, Rules.loss.oldWorld);
        // Fated and Forsaken Flowers and Glimmerfern grow where the Old World left its history and its sorrow.
        WorldResources.GrowEcho(Map, gen, 0, 0, initial: true);
        WorldCivilization.Rebuild(Map, gen);
        _agesPlaced.Add(0);
        // The first scout sets out with the survivors (a proposal: the map is worth walking from the first minute).
        foreach (var id in Settings.startingUnits)
        {
            var spec = Settings.Unit(id);
            var capital = WorldCivilization.Capital(Map);
            if (spec != null && capital != null) Deploy(spec, capital.coord);
        }
        GameLog.Event(Map.Report.ToString(), Log);
        foreach (var error in Map.Report.errors) GameLog.Warning($"World generation: {error}", Log);
        GameLog.Event($"World: {Map.Count} cells, features: {string.Join(", ", Map.Tiles.Where(t => t.HasFeature).GroupBy(t => t.feature).Select(g => $"{g.Key} x{g.Count()}"))}", Log);
    }

    /// <summary>The composition stencil named by the settings (null, with a warning, when missing or malformed).</summary>
    public static WorldStencil LoadStencil(WorldGenSettings gen)
    {
        var asset = Resources.Load<TextAsset>(gen.stencil);
        if (asset == null)
        {
            GameLog.Warning($"No world stencil at Resources/{gen.stencil}: the world map is disabled.", Log);
            return null;
        }
        try
        {
            return WorldStencil.Parse(asset.text);
        }
        catch (FormatException e)
        {
            GameLog.Warning($"The world stencil Resources/{gen.stencil} cannot be read: {e.Message}", Log);
            return null;
        }
    }

    /// <summary>Every handmade tile in the settings' folder; a malformed tile is reported and left out.</summary>
    public static List<TileTemplate> LoadTiles(WorldGenSettings gen)
    {
        var tiles = new List<TileTemplate>();
        foreach (var asset in Resources.LoadAll<TextAsset>(gen.tilesFolder).OrderBy(a => a.name, StringComparer.Ordinal))
        {
            try
            {
                tiles.Add(TileTemplate.Parse(asset.text, asset.name));
            }
            catch (FormatException e)
            {
                GameLog.Warning($"Handmade tile {asset.name}: {e.Message}", Log);
            }
        }
        return tiles;
    }

    private void Start()
    {
        if (Map == null) return;
        TimeSystemLogic.WhenReady(this, time =>
        {
            time.OnSeventhChange += OnSeventh;
            time.OnEchoChange += OnEcho;
            time.OnPhaseChange += OnPhase;
            time.OnRitualSeventh += OnRitualSeventh;
            SyncCalendar();
            _subscribed = true;
        });
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.AgeBegan += OnAgeBegan;
            ages.Changed += OnAgesChanged;
            if (ages.Current != null) OnAgeBegan(ages.Current);
        }
        GameTechnologySlot.Researched += OnResearched;
        RecomputeYields();
    }

    protected override void OnSingletonDestroy()
    {
        if (_subscribed && TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange -= OnSeventh;
            TimeSystemLogic.Instance.OnEchoChange -= OnEcho;
            TimeSystemLogic.Instance.OnPhaseChange -= OnPhase;
            TimeSystemLogic.Instance.OnRitualSeventh -= OnRitualSeventh;
        }
        if (AgeProgression.Instance != null) AgeProgression.Instance.AgeBegan -= OnAgeBegan;
        if (AgeProgression.Instance != null) AgeProgression.Instance.Changed -= OnAgesChanged;
        GameTechnologySlot.Researched -= OnResearched;
        foreach (var source in _yieldSources) EffectRouter.RemoveSource(source);
        _yieldSources.Clear();
    }

    private void Update()
    {
        if (Map == null) return;
        float sevenths = SeventhsPassing(Time.deltaTime);
        // Developer shortcut (Ctrl+E): every unit walks a whole Seventh at once.
        if (InputUtils.DebugKeyDown(Key.E)) sevenths += 1f;
        if (sevenths > 0f) MoveUnits(sevenths);
        if (_yieldsDirty && Time.unscaledTime >= _yieldsAt) RecomputeYields();
    }

    // ===== TIME =====

    /// <summary>
    /// The share of a Seventh that <paramref name="seconds"/> of play make (Cycle.md: a Seventh is four Days). Units keep
    /// this pace even before Horology lets the civilization count the Sevenths; they stop while time is paused, a story
    /// is open or the save menu has the screen.
    /// </summary>
    public float SeventhsPassing(float seconds)
    {
        var time = TimeSystemLogic.Instance;
        if (time == null || SaveMenu.BlocksGameplay || Time.timeScale <= 0f) return 0f;
        if (time.canTrackTime && time.isTimePaused) return 0f;
        var events = EventSystemLogic.Instance;
        if (events != null && events.IsEventActive()) return 0f;
        float perSeventh = time.canTrackTime ? time.GetEffectiveSecondsPerSeventh() : time.BaseSecondsPerSeventh;
        return perSeventh > 0f ? seconds / perSeventh : 0f;
    }

    public float SecondsPerSeventh
    {
        get
        {
            var time = TimeSystemLogic.Instance;
            return time == null ? 180f : time.canTrackTime ? time.GetEffectiveSecondsPerSeventh() : time.BaseSecondsPerSeventh;
        }
    }

    // ===== RULES THE WORLD VIEW ASKS =====

    /// <summary>The world map can be opened (its technology is researched); before that only the Capital is known.</summary>
    public bool MapUnlocked => Map != null && Researched(Settings.mapTechnology);

    /// <summary>The civilization may build beyond the walls (the unlock technology is researched).</summary>
    public bool IsOpen
    {
        get
        {
            if (Map == null) return false;
            if (string.IsNullOrEmpty(Settings.unlockTechnology)) return true;
            return Researched(Settings.unlockTechnology);
        }
    }

    private static bool Researched(string technology)
    {
        if (string.IsNullOrEmpty(technology)) return true;
        var units = GameUnitsLogic.Instance;
        return units != null && units.IsTechnologyUnlocked(technology);
    }

    public bool IsPassable(string terrain)
    {
        var spec = Settings != null ? Settings.generation.Terrain(terrain) : null;
        return spec == null || (spec.passable && !spec.water);
    }

    public int SettlementCount(string kind) => Map == null ? 0 : string.IsNullOrEmpty(kind)
        ? Map.Settlements.Count(s => s.kind != SettlementKind.Capital && s.kind != SettlementKind.Tributary)
        : Map.Settlements.Count(s => string.Equals(s.kind.ToString(), kind, StringComparison.OrdinalIgnoreCase));

    // ===== UNITS =====

    /// <summary>The unit a map unit walks as: its spec, or for an expedition the spec for its party (<see cref="Expeditions.Effective"/>).</summary>
    public UnitSpec SpecOf(WorldUnit unit)
    {
        if (unit == null) return null;
        var spec = Settings.Unit(unit.spec);
        // Enemies wear their stance's colour: red hostile, orange wary.
        if (spec != null && !WorldBattles.IsPlayers(unit)) return Tinted(spec, unit);
        if (spec == null || spec.role != UnitRole.Expedition) return spec;
        string binding = DirectorBinding(unit);
        bool improve = CanImprove;
        var ambition = CivilizationProperties.Expedition;
        string key = $"{string.Join("|", Expeditions.Members(unit))}#{unit.settlers}#{binding}#{improve}#{unit.retreating}#{ambition.cost:0.####}#{ambition.time:0.####}#{Expeditions.BurdenStep(unit, ExpeditionRules):0.##}#{unit.charter}";
        if (_partySpecs.TryGetValue(unit.id, out var cached) && cached.key == key && cached.from == spec) return cached.spec;
        var party = Expeditions.Effective(spec, unit, binding, ExpeditionRules, improve, ambition);
        _partySpecs[unit.id] = (key, spec, party);
        return party;
    }

    /// <summary>An expedition (a unit whose kind is <see cref="UnitRole.Expedition"/>).</summary>
    public bool IsExpedition(WorldUnit unit) => unit != null && Settings.Unit(unit.spec)?.role == UnitRole.Expedition;

    /// <summary>The primary binding of an expedition's Director's Soul Leitmotif (null without one).</summary>
    public static string DirectorBinding(WorldUnit unit) =>
        unit?.leader != null && LegendProgress.Instance != null ? LegendProgress.Instance.Soul(unit.leader)?.leitmotif : null;

    public WorldUnit UnitById(int id) => Map?.Units.FirstOrDefault(u => u.id == id);

    public IEnumerable<WorldUnit> UnitsAt(HexCoord coord) => Map.Units.Where(u => !u.Missing && u.coord == coord);

    /// <summary>Units of <paramref name="spec"/> on the map (all units for an empty id).</summary>
    public int UnitCount(string spec) =>
        Map == null ? 0 : string.IsNullOrEmpty(spec) ? Map.Units.Count : Map.Units.Count(u => string.Equals(u.spec, spec, StringComparison.OrdinalIgnoreCase));

    /// <summary>Expeditions improve hotspots once the civilization knows how (<see cref="ExpeditionSettings.improveTechnology"/>; empty: always).</summary>
    public bool CanImprove => string.IsNullOrEmpty(ExpeditionRules.improveTechnology) || Researched(ExpeditionRules.improveTechnology);

    // A technology researched: the map opens with its technology, and the first expedition sets out free from the
    // Capital, directed by a legend no seat holds (none free: the notices ask for one).
    private void OnResearched(GameTechnologySlot slot)
    {
        string technology = slot != null && slot.gameUnit != null ? slot.gameUnit.name : null;
        if (Map == null || string.IsNullOrEmpty(technology) || !string.Equals(technology, Settings.mapTechnology, StringComparison.OrdinalIgnoreCase)) return;
        Say("The world beyond the walls opens: scroll out of the Capital or press M to see it.");
        var capital = WorldCivilization.Capital(Map);
        if (ExpeditionRules.firstFree && capital != null && !_granted.Contains(FirstExpedition))
        {
            var government = GovernmentLogic.Instance;
            string director = Candidates().FirstOrDefault(n => government == null || government.GetSeatWithLegend(n) == null);
            if (director != null)
            {
                _granted.Add(FirstExpedition);
                var unit = FormExpedition(capital, director, free: true);
                if (unit != null) Say($"{unit.name} waits at {capital.name}, ready to explore.");
            }
        }
        Changed?.Invoke();
    }

    // Citizens leave with settlers (they are not dead: they found the new town, or come home when the party disbands).
    private static void TakeCitizens(int count)
    {
        var pop = PopGrowthLogic.Instance;
        if (pop == null || count <= 0) return;
        pop.population = Math.Max(0, pop.population - count);
        pop.RefreshHUD();
    }

    private WorldUnit Deploy(UnitSpec spec, HexCoord at, HexCoord? micro = null, string leader = null)
    {
        _nextUnitId = Math.Max(_nextUnitId, Map.Units.Count == 0 ? 0 : Map.Units.Max(u => u.id) + 1);
        int number = Map.Units.Count(u => u.spec == spec.id) + 1;
        // An expedition is known by the legend who formed it; other units by their kind and number.
        string name = spec.role == UnitRole.Expedition && leader != null ? $"{leader}'s {spec.name}" : $"{spec.name} {AgeRules.Roman(number)}";
        var unit = new WorldUnit { id = _nextUnitId++, spec = spec.id, name = name, coord = at, charges = spec.charges, leader = leader };
        Map.Units.Add(unit);
        // It sets out from the heart of the cell (a settlement's own hex) with full rations, unless placed elsewhere.
        if (micro.HasValue) WorldUnits.Place(unit, micro.Value);
        var party = SpecOf(unit);
        WorldUnits.Initialize(unit, party);
        Look(unit, party, new List<string>());
        return unit;
    }

    /// <summary>The units standing on one micro hex.</summary>
    public IEnumerable<WorldUnit> UnitsOn(HexCoord micro) => Map.Units.Where(u => !u.Missing && WorldUnits.MicroPosition(u) == micro);

    /// <summary>
    /// Why <paramref name="unit"/> cannot go to the meso cell <paramref name="target"/>, or null, with the way (micro
    /// ids), its travel fatigue and where it ends: the cell's centre hex, or the nearest hex the unit can reach.
    /// </summary>
    public string WhyNotGo(WorldUnit unit, HexCoord target, out List<int> path, out float fatigue, out HexCoord reached, int maxVisits = int.MaxValue)
    {
        path = null;
        fatigue = 0f;
        reached = MicroNavigation.Center(target);
        if (unit != null && !WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit == null) return "No unit selected.";
        if (unit.Missing) return "No one knows where it is.";
        if (Map.Get(target) == null) return "Beyond the edge of the world.";
        var gen = Settings.generation;
        if (!WorldUnits.PlanFromStep(Map, gen, unit, (HexCoord from, out List<int> p, out float f, out HexCoord r) =>
                MicroNavigation.ToMeso(Map, gen, from, target, out p, out f, out r, MicroNavigation.MaxPlan, maxVisits), out path, out fatigue, out reached))
            return NoWay;
        return path.Count == 0 && !WorldUnits.MidStep(Map, gen, unit, out _, out _, out _) ? "It is already there." : null;
    }

    public string WhyNotGo(WorldUnit unit, HexCoord target, out List<int> path, out float fatigue) => WhyNotGo(unit, target, out path, out fatigue, out _);

    /// <summary>Why a unit cannot go somewhere when no ground joins it there (or none was found within a bounded search).</summary>
    public const string NoWay = "No way over land leads there.";
    /// <summary>Only your own parties take orders: a band on the map is never yours to move.</summary>
    public const string NotYours = "Only your own parties take orders.";

    /// <summary>Whether ground joins the unit to <paramref name="target"/> (a micro hex, or a meso cell's heart) or near it, without searching the way.</summary>
    public bool CanReach(WorldUnit unit, HexCoord target, bool micro) =>
        unit != null && Map != null && MicroNavigation.Reachable(Map, Settings.generation, WorldUnits.MicroPosition(unit), micro ? target : MicroNavigation.Center(target));

    /// <summary>Why <paramref name="unit"/> cannot go to the micro hex <paramref name="target"/> (or the nearest one it can reach), or null.</summary>
    public string WhyNotGoMicro(WorldUnit unit, HexCoord target, out List<int> path, out float fatigue, out HexCoord reached, int maxVisits = int.MaxValue)
    {
        path = null;
        fatigue = 0f;
        reached = target;
        if (unit != null && !WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit == null) return "No unit selected.";
        if (unit.Missing) return "No one knows where it is.";
        if (MicroNavigation.Parent(Map, target) == null) return "Beyond the edge of the world.";
        var gen = Settings.generation;
        if (!WorldUnits.PlanFromStep(Map, gen, unit, (HexCoord from, out List<int> p, out float f, out HexCoord r) =>
                MicroNavigation.ToNearest(Map, gen, from, target, out p, out f, out r, MicroNavigation.MaxPlan, maxVisits), out path, out fatigue, out reached))
            return NoWay;
        return path.Count == 0 && !WorldUnits.MidStep(Map, gen, unit, out _, out _, out _) ? "It is already here." : null;
    }

    /// <summary>Send a unit to a meso cell (its centre hex, or the nearest one it can reach); <paramref name="then"/> starts on arrival.</summary>
    public bool Go(WorldUnit unit, HexCoord target, UnitTask then = UnitTask.None) => Send(unit, WhyNotGo(unit, target, out var path, out _, out _), path, then);

    /// <summary>Send a unit to a micro hex (or the nearest one it can reach); <paramref name="then"/> starts on arrival.</summary>
    public bool GoMicro(WorldUnit unit, HexCoord target, UnitTask then = UnitTask.None) => Send(unit, WhyNotGoMicro(unit, target, out var path, out _, out _), path, then);

    private bool Send(WorldUnit unit, string why, List<int> path, UnitTask then)
    {
        if (why != null)
        {
            Say(why);
            return false;
        }
        // A walk within the cell it surveys is part of the survey (it goes on from where it stops); an order elsewhere
        // sets the survey aside with its progress kept.
        bool withinSurvey = (unit.surveying || unit.surveyPaused) && path.Count > 0
            && Map[path[path.Count - 1] / MicroNavigation.PerCell].coord == unit.surveyCell;
        if (withinSurvey) KeepSurveyWork(unit);
        else
        {
            PauseSurvey(unit);
            unit.autoSurvey = false;
        }
        WorldUnits.Order(Map, unit, path);
        unit.autoExplore = false;
        unit.returning = false;
        unit.retreating = false;
        unit.surveyWait = 0f;
        unit.quarryId = -1; // a new order ends any chase
        var ability = UnitAbilities.For(then);
        unit.onArrival = ability != null && WorldUnits.Can(SpecOf(unit), ability.ability) ? then : UnitTask.None;
        Changed?.Invoke();
        return true;
    }

    // ===== SURVEYS SET ASIDE AND TAKEN UP AGAIN =====

    // The work done on the hex it is surveying, kept for when the survey goes on (an interruption loses none of it).
    private void KeepSurveyWork(WorldUnit unit)
    {
        if (!unit.surveying || !unit.Working || unit.task != UnitTask.SurveyMeso) return;
        unit.surveyWorkHex = WorldUnits.MicroPosition(unit);
        unit.surveyWork = Math.Max(0f, unit.workTotal - unit.workLeft);
    }

    // Another order sets the survey aside: its progress is kept, and it can be taken up again (ResumeSurvey), or goes
    // on by itself once the party is back in the cell.
    private void PauseSurvey(WorldUnit unit)
    {
        if (!unit.surveying) return;
        KeepSurveyWork(unit);
        unit.surveying = false;
        unit.surveyPaused = true;
        unit.surveyWait = 0f;
    }

    // The survey is over or given up: nothing of it is kept but the hexes already surveyed (those stay surveyed).
    private static void ForgetSurvey(WorldUnit unit)
    {
        unit.surveying = false;
        unit.surveyPaused = false;
        unit.surveyWork = 0f;
        unit.surveyWait = 0f;
    }

    /// <summary>Why the unit cannot take up the survey it set aside, or null.</summary>
    public string WhyNotResumeSurvey(WorldUnit unit)
    {
        if (unit == null) return "No unit selected.";
        if (!unit.surveyPaused) return "It has no survey to take up.";
        return WhyNotSurveyCell(unit, unit.surveyCell);
    }

    /// <summary>Take up the survey the unit set aside, where it left off.</summary>
    public bool ResumeSurvey(WorldUnit unit) => unit != null && unit.surveyPaused && SurveyCell(unit, unit.surveyCell);

    /// <summary>Stop surveying altogether (the hexes already surveyed stay surveyed); it also stops surveying by itself.</summary>
    public void StopSurvey(WorldUnit unit)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit)) return;
        bool working = unit.surveying && unit.Working && unit.task == UnitTask.SurveyMeso;
        ForgetSurvey(unit);
        unit.autoSurvey = false;
        if (working)
        {
            unit.task = UnitTask.None;
            unit.workLeft = 0f;
        }
        if (unit.Moving) StopWhereItStands(unit);
        Changed?.Invoke();
    }

    // Stop walking: caught between two hexes it stops on the nearer one (it walks on into the next, or back to the one it left).
    private void StopWhereItStands(WorldUnit unit)
    {
        if (WorldUnits.MidStep(Map, Settings.generation, unit, out var next, out float back, out float on))
        {
            var keep = unit.onArrival;
            WorldUnits.Order(Map, unit, on <= back ? new List<int> { MicroNavigation.Index(Map, next) } : new List<int>());
            unit.onArrival = keep;
        }
        else WorldUnits.Stop(unit);
    }

    /// <summary>Survey the meso cell <paramref name="target"/> (<see cref="SurveyCell"/>).</summary>
    public bool GoAndSurvey(WorldUnit unit, HexCoord target) => SurveyCell(unit, target);

    /// <summary>Survey the meso cell the micro hex <paramref name="target"/> lies in (<see cref="SurveyCell"/>).</summary>
    public bool GoAndSurveyMicro(WorldUnit unit, HexCoord target) => SurveyCell(unit, HexHierarchy.Parent(target));

    public void Halt(WorldUnit unit)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit)) return;
        // Halting sets a survey aside, its progress kept (Resume survey takes it up again).
        PauseSurvey(unit);
        unit.autoSurvey = false;
        StopWhereItStands(unit);
        unit.autoExplore = false;
        unit.returning = false;
        unit.retreating = false;
        unit.onArrival = UnitTask.None;
        unit.quarryId = -1;
        if (!unit.Camping) unit.task = UnitTask.None;
        unit.workLeft = 0f;
        Changed?.Invoke();
    }

    /// <summary>Make camp where the unit stands: it rests, eats less, gathers what the land gives and takes on rations inside your authority.</summary>
    public void MakeCamp(WorldUnit unit)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit) || (unit.Camping && !unit.resting)) return;
        unit.autoExplore = false;
        unit.returning = false;
        // A survey waits in camp with its progress kept; breaking camp takes it up again.
        PauseSurvey(unit);
        unit.quarryId = -1;
        WorldUnits.Camp(unit);
        Changed?.Invoke();
    }

    /// <summary>Break camp: a unit that kept its road walks on; one that camped in the middle of a survey takes it up again.</summary>
    public void BreakCamp(WorldUnit unit)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit) || !unit.Camping) return;
        WorldUnits.BreakCamp(unit);
        if (unit.surveyPaused && !unit.Moving && unit.coord == unit.surveyCell && WhyNotResumeSurvey(unit) == null) ResumeSurvey(unit);
        Changed?.Invoke();
    }

    /// <summary>Why the unit cannot walk back to a settlement for rations now, or null (with the way and its fatigue).</summary>
    public string WhyNotReturn(WorldUnit unit, out List<int> path, out float fatigue)
    {
        path = null;
        fatigue = 0f;
        if (unit == null) return "No unit selected.";
        if (!WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit.Missing) return "No one knows where it is.";
        if (Map.Get(unit.coord)?.settlement >= 0) return "It stands in a settlement already.";
        if (!WorldUnits.WayToResupply(Map, Settings.generation, unit, out path, out fatigue)) return "No settlement can be reached.";
        return path.Count == 0 ? "It stands in a settlement already." : null;
    }

    /// <summary>Walk back to the nearest settlement to take on rations and rest there.</summary>
    public bool ReturnToResupply(WorldUnit unit)
    {
        string why = WhyNotReturn(unit, out var path, out _);
        if (why != null)
        {
            Say(why);
            return false;
        }
        PauseSurvey(unit);
        unit.autoSurvey = false;
        WorldUnits.Order(Map, unit, path);
        unit.autoExplore = false;
        unit.returning = true;
        unit.retreating = false;
        unit.quarryId = -1;
        Changed?.Invoke();
        return true;
    }

    // ===== EXPEDITIONS: SLOTS AND LEGENDS =====

    /// <summary>The expeditions in the field.</summary>
    public IEnumerable<WorldUnit> ExpeditionUnits => Map != null ? Map.Units.Where(IsExpedition) : Enumerable.Empty<WorldUnit>();

    /// <summary>The civilization's expedition slots: one per legend in the field (<see cref="Expeditions.Slots"/>).</summary>
    public int ExpeditionSlots
    {
        get
        {
            var x = ExpeditionRules;
            if (x == null) return 0;
            var units = GameUnitsLogic.Instance;
            int buildings = units != null && !string.IsNullOrEmpty(x.slotBuilding) ? Mathf.FloorToInt(units.GetProductionUnitCount(x.slotBuilding)) : 0;
            // Militant Districts muster more parties.
            int districts = Map != null ? WorldTributaries.ExpeditionSlots(Map, Rules.tributaries) : 0;
            return Expeditions.Slots(x, GovernmentCapacity, buildings) + districts;
        }
    }

    public int ExpeditionSlotsUsed => Expeditions.SlotsUsed(ExpeditionUnits);

    public int FreeExpeditionSlots => Math.Max(0, ExpeditionSlots - ExpeditionSlotsUsed);

    /// <summary>The expedition <paramref name="legend"/> walks with, or null.</summary>
    public WorldUnit ExpeditionOf(string legend) => string.IsNullOrEmpty(legend) ? null : ExpeditionUnits.FirstOrDefault(u => Expeditions.IsMember(u, legend));

    /// <summary>
    /// What the road does to <paramref name="legend"/>'s Composure this Seventh (<see cref="Expeditions.Hardship"/>): 0
    /// when it walks with no expedition. <paramref name="expedition"/> is its party, <paramref name="resting"/> true while
    /// the party is camped in one of your settlements (the legend rests as at home).
    /// </summary>
    public float HardshipOf(string legend, out WorldUnit expedition, out bool resting)
    {
        expedition = ExpeditionOf(legend);
        resting = false;
        if (expedition == null) return 0f;
        var at = UnitSurroundings.Of(Map, expedition);
        // A camp in one of your settlements, or in a healing bloom's light, is rest as at home.
        resting = expedition.Camping && (at.settlement || at.sanctuary > 0.01f);
        bool director = string.Equals(expedition.leader, legend, StringComparison.OrdinalIgnoreCase);
        return Expeditions.Hardship(expedition, at, director, DirectorBinding(expedition), ExpeditionRules);
    }

    /// <summary>Legends that could join an expedition now (met, not lost, with no expedition), seated ones last, then by name.</summary>
    public List<string> Candidates()
    {
        var legends = LegendProgress.Instance;
        if (legends == null || Map == null) return new List<string>();
        var government = GovernmentLogic.Instance;
        return legends.RecruitedNames.Where(n => ExpeditionOf(n) == null && !legends.IsMissing(n))
            .OrderBy(n => government != null && government.GetSeatWithLegend(n) != null ? 1 : 0).ThenBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The candidate after <paramref name="current"/> (the first when it is none or no longer free), for the cards' choosers.</summary>
    public string NextCandidate(string current)
    {
        var free = Candidates();
        if (free.Count == 0) return null;
        int at = current == null ? -1 : free.FindIndex(n => string.Equals(n, current, StringComparison.OrdinalIgnoreCase));
        return free[(at + 1) % free.Count];
    }

    /// <summary>Why <paramref name="legend"/> cannot set out now, or null. A seated legend leaves its seat (unless the seat is on cooldown).</summary>
    public string WhyNotJoin(string legend)
    {
        var legends = LegendProgress.Instance;
        if (string.IsNullOrEmpty(legend) || legends == null) return "No legend is free to go.";
        if (legends.IsLost(legend)) return $"{legend} is lost to Dissonance.";
        if (!legends.IsRecruited(legend)) return $"{legend} has not been met yet.";
        if (legends.IsMissing(legend)) return $"{legend} is missing in action.";
        var with = ExpeditionOf(legend);
        if (with != null) return $"{legend} already walks with {with.name}.";
        var seat = GovernmentLogic.Instance != null ? GovernmentLogic.Instance.GetSeatWithLegend(legend) : null;
        if (seat != null && !GovernmentLogic.Instance.CanChangeSeat(seat.seatIndex))
            return $"{legend} cannot leave the {seat.GetEffectiveTitle()} seat for {GovernmentLogic.Instance.GetSeatCooldownRemaining(seat.seatIndex)} Sevenths.";
        return null;
    }

    /// <summary>The council seat <paramref name="legend"/> would leave to set out, or null.</summary>
    public CouncilSeat SeatLeftBy(string legend) => GovernmentLogic.Instance != null && legend != null ? GovernmentLogic.Instance.GetSeatWithLegend(legend) : null;

    // A legend setting out leaves the council (its seat's cooldown starts as for any change).
    private void LeaveCouncil(string legend)
    {
        var seat = SeatLeftBy(legend);
        if (seat != null) GovernmentLogic.Instance.RemoveLegendFromSeat(seat.seatIndex);
    }

    /// <summary>Walk to unknown ground by itself whenever it is idle (scouts, expeditions), turning back for rations in time.</summary>
    public void SetAutoExplore(WorldUnit unit, bool on)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit) || !WorldUnits.Can(SpecOf(unit), UnitAbility.AutoExplore)) return;
        unit.autoExplore = on;
        if (!on) unit.returning = false;
        if (on)
        {
            PauseSurvey(unit);
            unit.autoSurvey = false;
            unit.quarryId = -1;
        }
        // A camp made by hand ends when it is sent exploring; one it made by itself runs its course.
        if (on && unit.Camping && !unit.resting) WorldUnits.BreakCamp(unit);
        Changed?.Invoke();
    }

    // An idle explorer looks after itself: it rests when weary, walks back to a settlement while its rations still
    // carry it there, stops when neither the stores nor any settlement can feed it, and otherwise heads for the
    // nearest ground no unit has walked (a search capped at a few Sevenths' walk).
    private void AutoExplore(WorldUnit unit, UnitSpec spec)
    {
        if (!unit.autoExplore || unit.Moving || unit.Working || unit.Camping) return;
        var gen = Settings.generation;
        var care = LookAfterItself(unit, spec);
        if (care == SelfCare.Acted) return;
        if (care == SelfCare.Stranded)
        {
            // Nothing will feed it here: better to wait for orders than to starve on.
            unit.autoExplore = false;
            UnitSays(unit, $"{unit.name} stops exploring", StrandedText(unit));
            return;
        }
        // About ten Sevenths' walk: far enough to cross whatever it already knows.
        float reach = Math.Max(24f, spec.stamina * 10f);
        if (WorldUnits.NextToScout(Map, gen, unit, reach, out _, out var path))
        {
            WorldUnits.Order(Map, unit, path);
            return;
        }
        unit.autoExplore = false;
        UnitSays(unit, "Nothing left to explore nearby", $"{unit.name} finds no unknown ground within reach and waits for orders.");
    }

    private enum SelfCare { None, Acted, Stranded }

    // A party left to itself (exploring or surveying) looks after itself before going on: it rests when weary and walks
    // back to a settlement while its rations still carry it there (it keeps its survey; it takes it up again once
    // rested and fed). Stranded: short of rations, and nothing can feed it.
    private SelfCare LookAfterItself(WorldUnit unit, UnitSpec spec)
    {
        if (unit.fatigue >= Math.Min(Provisions.exhaustion, 80f))
        {
            WorldUnits.Camp(unit, resting: true);
            return SelfCare.Acted;
        }
        if (spec.supplyUsePerSeventh <= 0f || !MightNeedRations(unit, spec)) return SelfCare.None;
        bool home = WorldUnits.WayToResupply(Map, Settings.generation, unit, out var back, out float backFatigue);
        float needed = WorldUnits.RationsFor(unit, spec, backFatigue) * 1.25f + spec.supplyUsePerSeventh;
        if (home && back.Count > 0 && unit.supplies <= needed)
        {
            WorldUnits.Order(Map, unit, back);
            unit.returning = true;
            return SelfCare.Acted;
        }
        return unit.supplies < spec.supplyCapacity * 0.25f && (!home || back.Count == 0) ? SelfCare.Stranded : SelfCare.None;
    }

    private string StrandedText(WorldUnit unit) => Map.Get(unit.coord)?.settlement >= 0
        ? $"{unit.name} cannot take on rations in {Place(Map.Get(unit.coord))}: the stores are empty. It waits for orders."
        : $"{unit.name} is short of rations and no settlement can be reached from {Place(Map.Get(unit.coord))}. It waits for orders: make camp on fertile ground, or send it on.";

    // A cheap first look before searching the way home: the nearest settlement as the crow flies, over rough ground.
    private bool MightNeedRations(WorldUnit unit, UnitSpec spec)
    {
        var here = Map.Get(unit.coord);
        if (here == null || here.settlement >= 0 || unit.supplies >= spec.supplyCapacity) return here != null && here.settlement >= 0 && unit.supplies < spec.supplyCapacity * 0.25f;
        var at = WorldUnits.MicroPosition(unit);
        int nearest = Map.Settlements.Count == 0 ? int.MaxValue : Map.Settlements.Min(s => HexCoord.Distance(at, MicroNavigation.Center(s.coord)));
        if (nearest == int.MaxValue) return unit.supplies < spec.supplyCapacity * 0.25f;
        return unit.supplies <= WorldUnits.RationsFor(unit, spec, nearest * 2.5f) * 1.25f + spec.supplyUsePerSeventh;
    }

    private void MoveUnits(float sevenths)
    {
        // A fixed simulation step makes needs, work and walking independent of the frame rate; the remainder is saved.
        _unitTimeRemainder += Math.Max(0f, sevenths);
        int steps = 0;
        while (_unitTimeRemainder + 1e-7f >= Quantum && steps++ < 600)
        {
            _unitTimeRemainder = Math.Max(0f, _unitTimeRemainder - Quantum);
            StepUnits(Quantum);
        }
    }

    private void StepUnits(float sevenths)
    {
        // The threats send their bands out (red and orange enemies), whether or not any of your units walk the map.
        bool changed = TickThreats(sevenths);
        // Known dens send a band out now and then (it heads home after a while).
        if (TickDens(sevenths)) changed = true;
        // The land's suffering fades, feeds the Formless Masses and pools into new ones; hunters' signs fade and warn.
        var notice = new List<string>();
        if (TickEcosystem(sevenths, notice)) changed = true;
        if (Map.Units.Count == 0)
        {
            if (notice.Count > 0) Say(string.Join(" ", notice));
            if (changed) Changed?.Invoke();
            return;
        }
        var gen = Settings.generation;
        var rules = Provisions;
        foreach (var unit in Map.Units.ToList())
        {
            // A legend gone to ground is off the map until it turns up (TickMissing, once a Seventh).
            if (unit.Missing) continue;
            var spec = SpecOf(unit);
            if (spec == null) continue;
            if (unit.truce > 0f) unit.truce = Math.Max(0f, unit.truce - sevenths);
            // Wild bands (and other peoples' units) go their own way: no rations, no notices, nothing revealed.
            if (!WorldBattles.IsPlayers(unit))
            {
                if (StepBand(unit, spec, sevenths, notice)) changed = true;
                continue;
            }
            var activity =unit.Camping ? UnitActivity.Camping : unit.Working ? UnitActivity.Working : unit.Moving ? UnitActivity.Moving : UnitActivity.Idle;
            float toll = unit.Working ? UnitAbilities.For(unit.task)?.fatigue ?? 1f : 1f;
            var report = WorldUnits.Needs(unit, spec, rules, UnitSurroundings.Of(Map, unit), activity, sevenths, DrawRations, toll);
            // A party shaken in battle finds its nerve again, faster at rest.
            if (unit.nerveLost > 0f)
                unit.nerveLost = Math.Max(0f, unit.nerveLost - NerveRecovery * sevenths * (unit.Camping || Map.Get(unit.coord)?.settlement >= 0 ? 3f : 1f));
            if (Condition(unit, spec))
            {
                changed = true;
                continue;
            }
            // Running endurance comes back; a party giving chase re-plans toward its quarry.
            WorldPursuit.Initialize(gen, unit);
            bool wasWinded = unit.winded;
            WorldPursuit.Recover(unit, sevenths);
            if (wasWinded && !unit.winded) changed = true;
            if (unit.quarryId >= 0 && TrackQuarry(unit, spec, sevenths, notice)) changed = true;
            // Cargo carried home is unloaded in any of your settlements.
            if (unit.cargo != null && unit.cargo.Count > 0 && Map.Get(unit.coord)?.settlement >= 0)
            {
                DeliverCargo(unit, notice);
                changed = true;
            }
            if (unit.Camping)
            {
                if (!unit.resting || !DoneResting(unit, spec, report)) continue;
                // Rested (and provisioned): it walks on along any road it kept.
                WorldUnits.BreakCamp(unit);
                changed = true;
            }
            if (unit.Working)
            {
                // A surveyor worn out at its work makes camp; the hex's survey waits, none of it lost.
                if (unit.surveying && unit.task == UnitTask.SurveyMeso && unit.fatigue >= rules.exhaustion)
                {
                    KeepSurveyWork(unit);
                    WorldUnits.Camp(unit, resting: true);
                    Flag(unit, UnitWarning.Exhausted, true, false, $"{unit.name} is exhausted", $"{unit.name} can survey no longer and makes camp at {Place(Map.Get(unit.coord))}; it takes the survey up again once rested.");
                    changed = true;
                    continue;
                }
                unit.workLeft -= sevenths * WorldUnits.Efficiency(unit);
                if (unit.workLeft <= 0f)
                {
                    FinishWork(unit, spec, notice);
                    changed = true;
                }
                continue;
            }
            if (!unit.Moving)
            {
                if (unit.surveying || unit.autoSurvey)
                {
                    // A survey goes on by itself after a rest, a retreat, a walk back for rations or a way blocked.
                    if (TickSurvey(unit, spec, sevenths, notice)) changed = true;
                }
                else AutoExplore(unit, spec);
                if (!unit.Moving)
                {
                    if (unit.Camping || unit.Working) changed = true;
                    continue;
                }
                changed = true;
            }
            bool fresh = !unit.winded;
            var goingTo = unit.path[unit.path.Count - 1];
            var held = WorldBattles.Held(Map.Units, unit);
            var step = WorldUnits.Move(Map, gen, unit, spec, sevenths, held);
            if (fresh && unit.winded)
            {
                notice.Add($"{unit.name} is winded and stops to catch its breath.");
                changed = true;
            }
            if (step.entered.Count > 0 || step.blocked || step.halted) changed = true;
            foreach (int cell in step.entered) Look(unit, spec, notice, MicroNavigation.Coord(Map, cell));
            // Ground that closed or others in the way: it finds another way on if there is a fair one.
            bool rerouted = (step.blocked || (step.halted && unit.quarryId < 0)) && Reroute(unit, goingTo, step.halted);
            if (step.blocked && !rerouted) notice.Add($"{unit.name} can go no further.");
            if (step.halted && unit.quarryId < 0 && !rerouted && !unit.surveying) notice.Add($"{unit.name} halts: others hold the ground ahead.");
            if (step.arrived) Arrive(unit, spec, notice);
            else if (unit.Moving && unit.fatigue >= rules.exhaustion)
            {
                // Too weary to go on: it makes camp, keeps its road and walks on once rested.
                WorldUnits.Camp(unit, resting: true);
                Flag(unit, UnitWarning.Exhausted, true, false, $"{unit.name} is exhausted", $"{unit.name} can walk no further and makes camp at {Place(Map.Get(unit.coord))}; it goes on once rested.");
                changed = true;
            }
        }
        // Hostile units that have come into contact (the same or adjacent micro hexes) fight where they stand.
        if (FightClashes(notice)) changed = true;
        if (notice.Count > 0) Say(string.Join(" ", notice));
        if (changed) Changed?.Invoke();
    }

    // A journey's end: its deeds, a return for rations, and the work it was sent to do there.
    private void Arrive(WorldUnit unit, UnitSpec spec, List<string> notice)
    {
        // Giving chase: it re-plans toward its quarry (TrackQuarry). Any other run ends where it was going.
        if (unit.quarryId >= 0) return;
        unit.sprinting = false;
        var here = Map.Get(unit.coord);
        if (unit.journeyLength >= JourneySteps) _journeys++;
        if (unit.retreating)
        {
            // Out of danger: it catches its breath where it stopped.
            unit.retreating = false;
            WorldUnits.Camp(unit, resting: true);
            notice.Add($"{UnitLabel(unit)} is out of danger at {Place(here)} and makes camp.");
            return;
        }
        if (unit.returning)
        {
            unit.returning = false;
            WorldUnits.Camp(unit, resting: true);
            if (!unit.autoExplore) notice.Add($"{UnitLabel(unit)} is back in {Place(here)} and makes camp to take on rations.");
            return;
        }
        // Explorers arriving by themselves and surveyors going from hex to hex stay quiet; ordered journeys are reported.
        if (!unit.autoExplore && !unit.surveying && !unit.autoSurvey) notice.Add($"{UnitLabel(unit)} reached {Place(here)}.");
        if (unit.leader != null && LegendProgress.Instance != null && unit.journeyLength >= JourneySteps)
        {
            // A long road honours every legend who walked it; the Director led it.
            LastLeader = unit.leader;
            foreach (var member in Expeditions.Members(unit).ToList())
                LegendProgress.Instance.Award(member, LegendLore.FragmentTuning.journey, member == unit.leader ? $"Led {unit.name} to {Place(here)}" : $"Walked with {unit.name} to {Place(here)}", againstTheOdds: unit.mishaps > 0);
        }
        var then = UnitAbilities.For(unit.onArrival);
        unit.onArrival = UnitTask.None;
        if (unit.surveying) ContinueSurvey(unit, spec, notice);
        else if (then != null && WhyNotWork(unit, then) == null) StartTask(unit, spec, then);
        // Back in the cell whose survey it set aside: it takes the survey up again where it stopped.
        else if (unit.surveyPaused && unit.coord == unit.surveyCell && WhyNotResumeSurvey(unit) == null)
        {
            unit.surveyPaused = false;
            unit.surveying = true;
            unit.surveyWait = 0f;
            notice.Add($"{UnitLabel(unit)} takes up its survey of {Place(here)} again.");
            ContinueSurvey(unit, spec, notice);
        }
    }

    // A camp the unit made by itself ends once it is rested and, while rations still come in quickly (from the
    // stores), full; the land alone is never worth the wait.
    private bool DoneResting(WorldUnit unit, UnitSpec spec, NeedsReport report)
    {
        if (unit.fatigue > Provisions.rested) return false;
        if (unit.supplies >= spec.supplyCapacity * 0.95f) return true;
        float eaten = Math.Max(0f, spec.supplyUsePerSeventh) * Provisions.campRationShare * Quantum;
        return report.drawn + report.gathered < eaten * 2f;
    }

    // Warnings, each raised once until the danger passes, and the loss of a unit worn out entirely. True when it was lost.
    private bool Condition(WorldUnit unit, UnitSpec spec)
    {
        if (unit.attrition >= 100f && Provisions.lostAtFullAttrition)
        {
            // A legend alone does not break: it goes to ground and turns up later in your territory.
            if (IsExpedition(unit) && PartyShapeOf(unit).canVanish && Expeditions.PartySize(unit) > 0)
            {
                GoMissing(unit, forced: true);
                return true;
            }
            Map.Units.Remove(unit);
            _partySpecs.Remove(unit.id);
            string cause = unit.hungry ? "hunger and hardship wore it out" : "hardship wore it out";
            if (IsExpedition(unit))
            {
                // It breaks: its legends limp home carrying the road with them; its settlers are lost.
                var members = Expeditions.Members(unit).ToList();
                string settlers = unit.settlers > 0 ? $" The {unit.settlers} settlers it escorted are lost." : string.Empty;
                UnitSays(unit, $"{unit.name} breaks", $"{unit.name} breaks at {Place(Map.Get(unit.coord))}: {cause}.{settlers} {string.Join(", ", members)} limp home.");
                GameLog.Event($"{unit.name} broke at {unit.coord} (attrition 100)", Log);
                foreach (var m in members) LegendProgress.Instance?.Strain(m, ExpeditionRules.breakStrain, $"{unit.name} broke");
                // Letting go of the road is its own growth (Fragments of Acceptance), for those still whole.
                foreach (var m in members) LegendProgress.Instance?.Award(m, LegendLore.FragmentTuning.brokenRoad, $"Came home from {unit.name}");
                return true;
            }
            string people = spec.populationCost > 0 ? $" The {spec.populationCost} citizens it carried are lost with it." : string.Empty;
            UnitSays(unit, $"{unit.name} is lost", $"{unit.name} is lost at {Place(Map.Get(unit.coord))}: {cause}.{people}");
            GameLog.Event($"{unit.name} was lost at {unit.coord} (attrition 100)", Log);
            return true;
        }
        if (spec.supplyCapacity > 0f && spec.supplyUsePerSeventh > 0f)
        {
            Flag(unit, UnitWarning.Starving, unit.supplies <= 0f, unit.supplies > 0.5f, $"{unit.name} is starving",
                $"{unit.name} has run out of rations at {Place(Map.Get(unit.coord))}: it falters and wears down. Bring it into your authority, or make camp on fertile ground.");
            Flag(unit, UnitWarning.LowRations, unit.supplies > 0f && unit.supplies < spec.supplyCapacity * 0.25f, unit.supplies >= spec.supplyCapacity * 0.5f, "Rations run low",
                $"{unit.name} has {unit.supplies:0.#} rations left, about {WorldUnits.RationSevenths(unit, spec):0.#} Sevenths.");
        }
        Flag(unit, UnitWarning.Worn, unit.attrition >= 50f, unit.attrition < 35f, $"{unit.name} is worn down",
            $"{unit.name} is worn down (attrition {unit.attrition:0}): rest it in camp while it has rations, or in a settlement.");
        Flag(unit, UnitWarning.Failing, unit.attrition >= 80f, unit.attrition < 65f, $"{unit.name} is failing",
            $"{unit.name} is close to collapse (attrition {unit.attrition:0}); at 100 it is lost.");
        if (unit.fatigue <= Provisions.rested) unit.warnings &= ~(int)UnitWarning.Exhausted;
        return false;
    }

    private void Flag(WorldUnit unit, UnitWarning warning, bool raise, bool clear, string title, string text)
    {
        int bit = (int)warning;
        if (raise && (unit.warnings & bit) == 0)
        {
            unit.warnings |= bit;
            UnitSays(unit, title, text);
        }
        else if (clear) unit.warnings &= ~bit;
    }

    private void UnitSays(WorldUnit unit, string title, string text)
    {
        LastNotice = text;
        if (UnitNotice != null) UnitNotice(unit, title, text);
        else Notice?.Invoke(text);
    }

    // Rations for units inside your authority: food value from your stores (stored food first, the most perishable
    // kinds first), then Food itself. Returns what it got.
    private float DrawRations(float value)
    {
        if (value <= 0f) return 0f;
        float got = 0f;
        float stored = Pantry.StoredValue;
        if (stored > 1e-3f)
        {
            float take = Math.Min(value, stored);
            if (Pantry.TrySpend(take)) got += take;
        }
        var units = GameUnitsLogic.Instance;
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        float rest = value - got;
        if (rest > 1e-5f && units != null && !string.IsNullOrEmpty(food))
        {
            float take = Math.Min(rest, Math.Max(0f, units.GetResourceAmountExact(food)));
            if (take > 0f)
            {
                units.ChangeResourceFromName(food, -take, false);
                got += take;
            }
        }
        return got;
    }

    // "Wynnievere's Expedition", or "Vittoria Frauter, leading Wynnievere's Expedition," once another legend directs it.
    private string UnitLabel(WorldUnit unit) =>
        unit.leader != null && !unit.name.StartsWith(unit.leader + "'s", StringComparison.Ordinal) ? $"{unit.leader}, leading {unit.name}," : unit.name;

    // What a unit learns where it stands: its sight brings cells out of the fog; the hexes it walks on (and, for
    // scouts, those around it) become known, and so do their cells (known land is land a scout passed over). Sites on
    // newly known ground are sighted, not yet investigated: that takes a survey.
    private void Look(WorldUnit unit, UnitSpec spec, List<string> notice, HexCoord? at = null)
    {
        var micro = at ?? WorldUnits.MicroPosition(unit);
        if (MicroNavigation.Parent(Map, micro) == null) return;
        int sight = WorldUnits.Sight(Map, unit, spec, Rules);
        // Cover blocks the view: the party sees the edge of a forest, not what lies beyond (from a ridge, over it).
        foreach (var c in WorldCover.Visible(Map, micro, sight, WorldUnits.HighGround(Map.Get(unit.coord)))) Map.Reveal(HexHierarchy.Parent(c), 0);
        // Crags it passes are known too (it has seen the rock up close); water and impassable cells are never walked.
        var found = new List<WorldTile>();
        var seen = new List<WorldTile>();
        foreach (var c in MicroNavigation.Area(Map, micro, AbilityReach.Around, spec.knowRadius))
        {
            var cell = MicroNavigation.Parent(Map, c);
            if (cell == null || !MicroNavigation.Walkable(cell, Settings.generation)) continue;
            if (Map.KnowMicro(c)) found.Add(cell);
            if (!seen.Contains(cell)) seen.Add(cell);
        }
        // A cell whose every hex has now been seen up close is explored in passing (one a party was sent to survey waits
        // for its survey): its sites are investigated and, now and then, something more turns up.
        var passed = new List<WorldTile>();
        foreach (var cell in seen)
            if (!cell.explored && WorldMap.FullySeen(cell) && SurveyorOf(cell) == null && Map.Explore(cell.coord)) passed.Add(cell);
        var sighted = found.Where(t => t.HasFeature && !t.explored && !t.concealed && t.coord != Map.Capital).Select(t => Settings.generation.Feature(t.feature)).Where(f => f != null).ToList();
        if (sighted.Count > 0) notice.Add($"{unit.name} sighted {string.Join(", ", sighted.Select(f => f.name))}: survey to investigate.");
        foreach (var cell in passed)
        {
            Investigate(cell, unit, spec, notice);
            RollFinds(cell, unit, spec, false, notice);
            RollSurvivors(cell, unit, false, false);
        }
        // Enclaves met and Sacred Sites seen are honoured once.
        foreach (var e in Map.Enclaves)
            if (Map.Get(e.coord)?.revealed == true && EraOnce("enclave:" + e.index, Rules.eraEnclaveMet, $"Met {e.name}")) notice.Add($"You meet {e.name}, a {e.family} enclave.");
        if (Map.Magic != null)
            foreach (int site in Map.Magic.SacredSites)
                if (Map[site].revealed && EraOnce("sacred:" + site, Rules.eraSacredSite, "Found a Sacred Site")) notice.Add("A Sacred Site rises from the fog.");
    }

    // ===== SETTLING AND BUILDERS =====

    /// <summary>Why the expedition cannot found <paramref name="kind"/> where it stands, or null. Its settlers are the cost.</summary>
    public string WhyNotSettle(WorldUnit unit, SettlementKind kind)
    {
        var spec = SpecOf(unit);
        if (!WorldUnits.Can(spec, UnitAbility.Settle)) return IsExpedition(unit) ? "Take on settlers in one of your settlements first." : "Only an expedition escorting settlers founds settlements.";
        if (unit.Moving) return "Stop the expedition first.";
        return WorldCivilization.WhyNotFound(Map, Settings.generation, Rules, unit.coord, kind);
    }

    /// <summary>The settlers found <paramref name="kind"/> where the expedition stands; its legends walk on, each honoured.</summary>
    public bool Settle(WorldUnit unit, SettlementKind kind)
    {
        string why = WhyNotSettle(unit, kind);
        if (why != null) { Say(why); return false; }
        var tile = Map.Get(unit.coord);
        bool nexus = tile.nexus != null || Map.NeighboursOf(tile).Any(n => n.nexus != null);
        var s = WorldCivilization.Found(Map, Settings.generation, Rules, unit.coord, kind, AgeNumber);
        CulturalFounding(unit, s);
        unit.settlers = 0;
        _partySpecs.Remove(unit.id);
        var legends = LegendProgress.Instance;
        foreach (var member in Expeditions.Members(unit).ToList()) legends?.Award(member, LegendLore.FragmentTuning.founding, $"Founded {s.name}");
        GameLog.Event($"{unit.name} founded {s.name} at {unit.coord}", Log);
        int points = kind == SettlementKind.Town ? Rules.eraTown : kind == SettlementKind.Haven ? Rules.eraHaven : Rules.eraOutpost;
        EraOnce("settlement:" + s.id, points, $"Founded {s.name}");
        if (nexus) EraOnce("nexus:" + tile.index, Rules.eraNexus, $"Settled a Trade Nexus ({WorldSites.NexusName(tile.nexus ?? Map.NeighboursOf(tile).First(n => n.nexus != null).nexus)})");
        string reclaim = Reclaim(s);
        AfterCivilizationChange($"{s.name} is founded{(nexus ? " on a Trade Nexus" : string.Empty)}." + (reclaim != null ? " " + reclaim : string.Empty));
        return true;
    }

    /// <summary>Why the expedition cannot improve the hotspot where it stands, or null.</summary>
    public string WhyNotImprove(WorldUnit unit)
    {
        var spec = SpecOf(unit);
        if (!WorldUnits.Can(spec, UnitAbility.Improve))
            return IsExpedition(unit) && !CanImprove ? $"Research {ExpeditionRules.improveTechnology} first." : "It cannot improve hotspots.";
        if (unit.Moving) return "Stop the expedition first.";
        if (unit.Working) return "Already at work.";
        return WorldUnits.WhyNotImprove(Map, Settings.generation, Rules, Map.Get(unit.coord)) ?? Unaffordable(ExpeditionRules.improveCost);
    }

    /// <summary>Improve the hotspot where the expedition stands: its materials are paid now, the work takes the legends' time.</summary>
    public bool Improve(WorldUnit unit)
    {
        string why = WhyNotImprove(unit);
        if (why != null) { Say(why); return false; }
        Pay(ExpeditionRules.improveCost);
        // Started directly: checking again after paying would refuse a party that had just enough.
        StartTask(unit, SpecOf(unit), UnitAbilities.Improve);
        return true;
    }

    // ===== ABILITIES =====

    /// <summary>Why the unit cannot start <paramref name="ability"/> where it stands now, or null.</summary>
    public string WhyNotWork(WorldUnit unit, AbilityInfo ability)
    {
        if (unit == null) return "No unit selected.";
        if (!WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit.Missing) return "No one knows where it is.";
        if (ability == null) return "It cannot do that.";
        var spec = SpecOf(unit);
        switch (ability.task)
        {
            case UnitTask.Survey:
            case UnitTask.SurveyMeso: return WorldUnits.WhyNotSurvey(Map, Settings.generation, unit, spec, ability);
            case UnitTask.Forage: return WorldUnits.WhyNotForage(Map, Settings.generation, unit, spec, AgeNumber);
            case UnitTask.Improve: return WhyNotImprove(unit);
            case UnitTask.Harvest: return WhyNotHarvest(unit, spec);
            case UnitTask.Plant: return WhyNotPlantHere(unit, spec);
            case UnitTask.Investigate: return WhyNotInvestigate(unit);
            case UnitTask.Festival: return WhyNotFestival(unit);
            default: return "It cannot do that.";
        }
    }

    /// <summary>Start an ability where the unit stands (it breaks camp to work).</summary>
    public bool Work(WorldUnit unit, AbilityInfo ability)
    {
        string why = WhyNotWork(unit, ability);
        if (why != null) { Say(why); return false; }
        unit.quarryId = -1;
        // Other work sets a survey aside (a survey of the hexes around it is a survey too).
        if (ability.task != UnitTask.Survey && ability.task != UnitTask.SurveyMeso) PauseSurvey(unit);
        StartTask(unit, SpecOf(unit), ability);
        return true;
    }

    /// <summary>Sevenths <paramref name="ability"/> would take for the unit where it stands.</summary>
    public float WorkSevenths(WorldUnit unit, AbilityInfo ability) => unit == null ? 0f : UnitAbilities.Duration(Map, Settings.generation, unit, SpecOf(unit), ability);

    public string WhyNotSurvey(WorldUnit unit) => WhyNotWork(unit, UnitAbilities.Survey);

    /// <summary>Survey the micro hexes around the unit (its survey radius): each becomes surveyed; a cell whose every hex is surveyed is explored and its sites investigated.</summary>
    public bool Survey(WorldUnit unit) => Work(unit, UnitAbilities.Survey);

    public string WhyNotSurveyMeso(WorldUnit unit) => WhyNotWork(unit, UnitAbilities.SurveyMeso);

    /// <summary>Survey all seven micro hexes of the meso hex the unit stands in, in one longer sweep.</summary>
    public bool SurveyMeso(WorldUnit unit) => Work(unit, UnitAbilities.SurveyMeso);

    public string WhyNotForage(WorldUnit unit) => WhyNotWork(unit, UnitAbilities.Forage);

    /// <summary>Gather the ground's forage (stored food) where the unit stands; once per Age per cell.</summary>
    public bool Forage(WorldUnit unit) => Work(unit, UnitAbilities.Forage);

    /// <summary>What a forage here would bring back (for the card).</summary>
    public List<ResourceAmount> ForagePreview(WorldUnit unit) => unit == null ? new List<ResourceAmount>() : WorldUnits.ForageOf(Settings.generation, Map.Get(unit.coord), (SpecOf(unit)?.rewardMultiplier ?? 1f) * WorldUnits.ForageRichness(Provisions, Map.Get(unit.coord)));

    private void StartTask(WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        if (unit.Camping) WorldUnits.BreakCamp(unit);
        unit.task = ability.task;
        unit.workLeft = unit.workTotal = Math.Max(0.1f, UnitAbilities.Duration(Map, Settings.generation, unit, spec, ability));
        unit.autoExplore = false;
        Changed?.Invoke();
    }

    private void FinishWork(WorldUnit unit, UnitSpec spec, List<string> notice)
    {
        var task = unit.task;
        unit.workLeft = 0f;
        unit.task = UnitTask.None;
        var tile = Map.Get(unit.coord);
        if (tile == null) return;
        switch (task)
        {
            case UnitTask.SurveyMeso when unit.surveying: SurveyHexHere(unit, spec, notice); return;
            case UnitTask.Survey:
            case UnitTask.SurveyMeso: FinishSurvey(unit, spec, UnitAbilities.For(task), notice); return;
            case UnitTask.Forage: FinishForage(unit, spec, tile, notice); return;
            case UnitTask.Improve: FinishImprove(unit, tile, notice); return;
            case UnitTask.Harvest: FinishHarvest(unit, spec, tile, notice); return;
            case UnitTask.Plant: FinishPlant(unit, tile, notice); return;
            case UnitTask.Investigate: FinishInvestigate(unit, notice); return;
            case UnitTask.Festival: FinishFestival(unit, notice); return;
        }
    }

    // The hexes in reach become surveyed; each cell with every hex surveyed is explored and its sites investigated
    // (rewards, legends, stories).
    private void FinishSurvey(WorldUnit unit, UnitSpec spec, AbilityInfo ability, List<string> notice)
    {
        var hexes = UnitAbilities.Targets(Map, Settings.generation, unit, spec, ability);
        if (hexes.Count == 0) return;
        var explored = new List<WorldTile>();
        foreach (var c in hexes)
            if (Map.SurveyMicro(c)) explored.Add(MicroNavigation.Parent(Map, c));
        var here = Map.Get(unit.coord);
        if (explored.Count == 0)
        {
            int left = WorldMap.UnsurveyedHexes(here);
            notice.Add($"{UnitLabel(unit)} surveyed {hexes.Count} hex{(hexes.Count == 1 ? string.Empty : "es")} around {Place(here)}{(left > 0 ? $"; {left} more to explore it" : string.Empty)}.");
        }
        else notice.Add(explored.Count == 1 ? $"{UnitLabel(unit)} explored {Place(explored[0])}." : $"{UnitLabel(unit)} explored {explored.Count} cells around {Place(here)}.");
        foreach (var t in explored.Where(t => t.HasFeature).OrderBy(t => t.coord == unit.coord ? 0 : 1).ThenBy(t => t.index))
            notice.AddRange(Discover(t, unit, spec));
        IdentifySites(explored, unit, spec, notice);
        if (explored.Count == 0) return;
        foreach (var t in explored) RollSurvivors(t, unit, true, false);
        MarkYieldsDirty();
        Map.CivilizationVersion++;
    }

    // ===== SURVEYING A CELL =====

    /// <summary>The unit sent to survey <paramref name="tile"/>, or null.</summary>
    public WorldUnit SurveyorOf(WorldTile tile) => tile == null || Map == null ? null : Map.Units.FirstOrDefault(u => u.surveying && !u.Missing && u.surveyCell == tile.coord);

    /// <summary>Hexes of <paramref name="tile"/> a survey still has to visit.</summary>
    public int HexesToSurvey(WorldTile tile) => UnitAbilities.CellTargets(Map, Settings.generation, tile).Count;

    /// <summary>Why <paramref name="unit"/> cannot be sent to survey the meso cell <paramref name="cell"/>, or null.</summary>
    public string WhyNotSurveyCell(WorldUnit unit, HexCoord cell)
    {
        if (unit == null) return "No unit selected.";
        if (!WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit.Missing) return "No one knows where it is.";
        var spec = SpecOf(unit);
        if (!WorldUnits.Can(spec, UnitAbility.SurveyMeso) && !WorldUnits.Can(spec, UnitAbility.Survey)) return $"{unit.name} cannot survey.";
        var tile = Map.Get(cell);
        if (tile == null) return "Beyond the edge of the world.";
        if (tile.water) return "Open water cannot be surveyed.";
        if (tile.impassable) return "No one can walk this ground.";
        if (HexesToSurvey(tile) == 0) return "Already surveyed, hex by hex.";
        var other = SurveyorOf(tile);
        if (other != null && other != unit) return $"{other.name} is surveying it already.";
        if (!CanReach(unit, cell, false)) return NoWay;
        return null;
    }

    /// <summary>
    /// Send <paramref name="unit"/> to survey the meso cell <paramref name="cell"/>: it walks the cell's hexes still to
    /// survey in the order that walks least (<see cref="MicroNavigation.NextOnTour"/>, re-planned at each hex) and spends
    /// <see cref="UnitAbilities.HexSevenths"/> surveying each. With all of them walked the cell is explored (if parties
    /// passing by had not done so), its sites investigated, and the survey is far likelier than a passing party to turn
    /// up an event or spare resources. Rest, a retreat or a walk back for rations only interrupt it; an order elsewhere
    /// sets it aside with its progress kept (<see cref="ResumeSurvey"/>).
    /// </summary>
    public bool SurveyCell(WorldUnit unit, HexCoord cell)
    {
        string why = WhyNotSurveyCell(unit, cell);
        if (why != null) { Say(why); return false; }
        var spec = SpecOf(unit);
        if (unit.Camping) WorldUnits.BreakCamp(unit);
        // Work on a hex of another cell is dropped; a survey of this cell set aside is taken up where it stopped.
        if (unit.surveyCell != cell) unit.surveyWork = 0f;
        else KeepSurveyWork(unit);
        if (unit.Working)
        {
            unit.task = UnitTask.None;
            unit.workLeft = 0f;
        }
        // Caught between two hexes it first finishes the step (or turns back), then goes on with the survey.
        StopWhereItStands(unit);
        unit.quarryId = -1;
        unit.autoExplore = false;
        unit.returning = false;
        unit.retreating = false;
        unit.onArrival = UnitTask.None;
        unit.surveying = true;
        unit.surveyPaused = false;
        unit.surveyWait = 0f;
        unit.surveyCell = cell;
        var notice = new List<string>();
        if (!unit.Moving) ContinueSurvey(unit, spec, notice);
        if (notice.Count > 0) Say(string.Join(" ", notice));
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// About how many Sevenths a survey of <paramref name="cell"/> would take <paramref name="unit"/>: the work on every
    /// hex still to survey (slower in dense cover) and the walk between them (the way there comes on top).
    /// </summary>
    public float SurveySevenths(WorldUnit unit, HexCoord cell)
    {
        var spec = SpecOf(unit);
        var tile = Map?.Get(cell);
        if (spec == null || tile == null) return 0f;
        var left = UnitAbilities.CellTargets(Map, Settings.generation, tile);
        if (left.Count == 0) return 0f;
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        // Each hex after the first is a step from a neighbour, at about what entering it costs.
        float walk = left.Skip(1).Sum(id => grid.Enter(id)) / Math.Max(0.1f, spec.stamina);
        float work = left.Count * HexWork(spec, tile);
        if (unit.surveyWork > 0f && HexHierarchy.Parent(unit.surveyWorkHex) == cell) work = Math.Max(0f, work - unit.surveyWork);
        return (work + walk) / Math.Max(0.1f, WorldUnits.Efficiency(unit));
    }

    // Sevenths of work surveying one hex of the cell takes (dense cover slows it, WorldCover).
    private static float HexWork(UnitSpec spec, WorldTile tile) => Math.Max(0.05f, UnitAbilities.HexSevenths(spec) * Math.Max(0.2f, tile?.coverSurvey ?? 1f));

    /// <summary>
    /// How much of the survey the unit is on (or set aside) is done, 0-1: the cell's hexes surveyed out of those that can
    /// be walked, with the hex under way counted by its share of work. -1 when it has no survey.
    /// </summary>
    public float SurveyProgress(WorldUnit unit)
    {
        if (unit == null || Map == null || (!unit.surveying && !unit.surveyPaused)) return -1f;
        var tile = Map.Get(unit.surveyCell);
        if (tile == null) return -1f;
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        int total = 0, done = 0;
        for (int k = 0; k < MicroNavigation.PerCell; k++)
        {
            int id = tile.index * MicroNavigation.PerCell + k;
            bool surveyed = MicroNavigation.Surveyed(Map, id);
            if (!surveyed && float.IsPositiveInfinity(grid.Enter(id))) continue;
            total++;
            if (surveyed) done++;
        }
        if (total == 0) return 1f;
        var spec = SpecOf(unit);
        float partial = unit.surveying && unit.Working && unit.task == UnitTask.SurveyMeso ? WorldUnits.WorkProgress(unit)
            : unit.surveyWork > 0f && HexHierarchy.Parent(unit.surveyWorkHex) == tile.coord && spec != null ? Math.Min(0.99f, unit.surveyWork / HexWork(spec, tile)) : 0f;
        return Math.Min(1f, (done + partial) / total);
    }

    /// <summary>
    /// The survey plan of the unit (on or set aside): the hexes of its cell still to survey, in the order it will walk
    /// them from where it stands (the one it works on first). Empty when it has no survey.
    /// </summary>
    public List<HexCoord> SurveyPlan(WorldUnit unit) =>
        unit == null || (!unit.surveying && !unit.surveyPaused) ? new List<HexCoord>() : SurveyPlan(unit, unit.surveyCell);

    /// <summary>
    /// The order <paramref name="unit"/> would walk the hexes of <paramref name="cell"/> still to survey, starting from
    /// where it stands (the plan the map shows, and the preview while picking a cell to survey). Hexes it cannot reach
    /// are left out. Bounded like its own planning, so it never stalls a frame.
    /// </summary>
    public List<HexCoord> SurveyPlan(WorldUnit unit, HexCoord cell)
    {
        var plan = new List<HexCoord>();
        var tile = Map?.Get(cell);
        if (unit == null || tile == null) return plan;
        var gen = Settings.generation;
        var left = UnitAbilities.CellTargets(Map, gen, tile);
        if (left.Count == 0) return plan;
        var order = new List<int>();
        var at = WorldUnits.MicroPosition(unit);
        // From afar the tour starts at the hex nearest the way in: plan it from the cell's heart side the unit comes from.
        var from = HexHierarchy.Parent(at) == cell ? at : EntryHex(tile, at);
        if (!MicroNavigation.NextOnTour(Map, gen, from, left, cell, out _, out _, out _, MicroNavigation.MaxPlan, PlanVisits, null, order)) return plan;
        foreach (int id in order) plan.Add(MicroNavigation.Coord(Map, id));
        return plan;
    }

    // The walkable hex of the cell nearest a unit coming from afar (where its survey tour will begin).
    private HexCoord EntryHex(WorldTile tile, HexCoord from)
    {
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        var best = MicroNavigation.Center(tile.coord);
        int bestDistance = int.MaxValue;
        foreach (var hex in HexHierarchy.Children(tile.coord))
        {
            int id = MicroNavigation.Index(Map, hex);
            if (id < 0 || float.IsPositiveInfinity(grid.Enter(id))) continue;
            int d = HexCoord.Distance(hex, from);
            if (d < bestDistance) { best = hex; bestDistance = d; }
        }
        return best;
    }

    // Others standing on or in the way of the hexes left: a surveyor waits this long before it tries again.
    private const float SurveyRetrySevenths = 0.25f;
    // Most hexes a re-planned way may search (keeps a hopeless search from stalling a frame).
    private const int PlanVisits = 60000;

    // A surveyor's next step, whenever it stands idle: survey the hex it stands on when that is still to do, else walk
    // to the first hex of the cheapest tour of those left (around others standing in the way); with none left the
    // survey is done. Weary, it rests first; short of rations, it walks back to a settlement first (keeping its survey).
    private void ContinueSurvey(WorldUnit unit, UnitSpec spec, List<string> notice)
    {
        var tile = Map.Get(unit.surveyCell);
        var gen = Settings.generation;
        var left = UnitAbilities.CellTargets(Map, gen, tile);
        if (left.Count == 0)
        {
            CompleteSurvey(unit, spec, tile, tile != null && Map.Explore(tile.coord), notice);
            return;
        }
        var care = LookAfterItself(unit, spec);
        if (care == SelfCare.Acted) return;
        if (care == SelfCare.Stranded && unit.autoSurvey)
        {
            unit.autoSurvey = false;
            UnitSays(unit, $"{unit.name} stops surveying by itself", StrandedText(unit));
        }
        var at = WorldUnits.MicroPosition(unit);
        int here = MicroNavigation.Index(Map, at);
        if (left.Contains(here))
        {
            StartSurveyHex(unit, spec, tile, at);
            return;
        }
        var held = HeldHexes(unit);
        var open = held == null ? left : left.Where(id => !held(id)).ToList();
        if (open.Count > 0 && MicroNavigation.NextOnTour(Map, gen, at, open, tile.coord, out var path, out _, out _, MicroNavigation.MaxPlan, PlanVisits, held) && path.Count > 0)
        {
            WorldUnits.Order(Map, unit, path);
            unit.surveyWait = 0f;
            return;
        }
        // Only others stand in the way (on the hexes left, or across the way to them): it waits and tries again.
        if (held != null && MicroNavigation.NextOnTour(Map, gen, at, left, tile.coord, out _, out _, out _, MicroNavigation.MaxPlan, PlanVisits))
        {
            if (unit.surveyWait <= 0f) GameLog.Event($"{unit.name} waits to survey {tile.coord}: others stand in the way", Log);
            unit.surveyWait = SurveyRetrySevenths;
            return;
        }
        ForgetSurvey(unit);
        notice.Add($"{UnitLabel(unit)} can reach no more of {Place(tile)}: the survey ends with {WorldMap.SurveyedHexes(tile)} of its {WorldMap.OpenHexes(tile)} hexes surveyed.");
    }

    // Start the work on the hex it stands on, picking up any work already done there.
    private void StartSurveyHex(WorldUnit unit, UnitSpec spec, WorldTile tile, HexCoord at)
    {
        float full = HexWork(spec, tile);
        float done = unit.surveyWork > 0f && unit.surveyWorkHex == at ? Math.Min(unit.surveyWork, full * 0.95f) : 0f;
        unit.surveyWork = 0f;
        unit.task = UnitTask.SurveyMeso;
        unit.workTotal = full;
        unit.workLeft = Math.Max(0.01f, full - done);
    }

    // The work on one hex is done: it is surveyed, and the surveyor goes on to the next (or finishes).
    private void SurveyHexHere(WorldUnit unit, UnitSpec spec, List<string> notice)
    {
        var at = WorldUnits.MicroPosition(unit);
        var tile = Map.Get(unit.surveyCell);
        bool explored = tile != null && HexHierarchy.Parent(at) == tile.coord && Map.SurveyMicro(at);
        if (tile != null && HexesToSurvey(tile) == 0) CompleteSurvey(unit, spec, tile, explored || Map.Explore(tile.coord), notice);
        else ContinueSurvey(unit, spec, notice);
    }

    // Every hex walked and surveyed: the cell is explored (unless parties passing by had done it), its sites
    // investigated, and the survey rolls for an event and spare resources.
    private void CompleteSurvey(WorldUnit unit, UnitSpec spec, WorldTile tile, bool newlyExplored, List<string> notice)
    {
        ForgetSurvey(unit);
        if (tile == null) return;
        notice.Add($"{UnitLabel(unit)} surveyed {Place(tile)}, all {WorldMap.OpenHexes(tile)} of its hexes.");
        if (newlyExplored) Investigate(tile, unit, spec, notice);
        RollFinds(tile, unit, spec, true, notice);
        RollSurvivors(tile, unit, true, !newlyExplored);
        Map.CivilizationVersion++;
    }

    // The micro hexes others stand on (bands, other peoples: units of another side never share a hex), or null when none do.
    private Func<int, bool> HeldHexes(WorldUnit unit)
    {
        HashSet<int> held = null;
        foreach (var u in Map.Units)
        {
            if (u == unit || u.Missing || !WorldBattles.OtherSides(u, unit)) continue;
            int id = MicroNavigation.Index(Map, WorldUnits.MicroPosition(u));
            if (id < 0) continue;
            (held ??= new HashSet<int>()).Add(id);
        }
        return held == null ? null : (Func<int, bool>)held.Contains;
    }

    // A unit whose way closed (ground that turned impassable) or was barred by others finds another way on to where it
    // was going. Around others it takes only a fair detour (at most about twice the way plus a few hexes); where they
    // stand on the very hex it was going to, it stops beside them as before. True when it walks on.
    private bool Reroute(WorldUnit unit, HexCoord goingTo, bool aroundOthers)
    {
        var gen = Settings.generation;
        var from = WorldUnits.MicroPosition(unit);
        var avoid = aroundOthers ? HeldHexes(unit) : null;
        int goal = MicroNavigation.Index(Map, goingTo);
        if (goal < 0 || (avoid != null && avoid(goal))) return false;
        if (!MicroNavigation.ToNearest(Map, gen, from, goingTo, out var path, out float cost, out _, MicroNavigation.MaxPlan, PlanVisits, avoid) || path.Count == 0) return false;
        if (avoid != null && MicroNavigation.ToNearest(Map, gen, from, goingTo, out _, out float direct, out _, MicroNavigation.MaxPlan, PlanVisits)
            && cost > direct * 2f + 6f * MicroNavigation.Grid(Map, gen).MinStep()) return false;
        var then = unit.onArrival;
        WorldUnits.Order(Map, unit, path);
        unit.onArrival = then;
        return true;
    }

    // ===== SURVEYING BY ITSELF =====

    /// <summary>Why the unit cannot survey by itself, or null.</summary>
    public string WhyNotAutoSurvey(WorldUnit unit)
    {
        if (unit == null) return "No unit selected.";
        if (!WorldBattles.IsPlayers(unit)) return NotYours;
        if (unit.Missing) return "No one knows where it is.";
        return WorldUnits.Can(SpecOf(unit), UnitAbility.SurveyMeso) ? null : $"{unit.name} cannot survey.";
    }

    /// <summary>
    /// Survey cell after cell by itself: the survey it is on (or set aside) first, then the nearest walkable ground in
    /// sight still to survey that no other party surveys, hex by hex; it rests when weary and walks back for rations in
    /// time, then goes on. Any order but a move within its cell ends it.
    /// </summary>
    public void SetAutoSurvey(WorldUnit unit, bool on)
    {
        if (on && WhyNotAutoSurvey(unit) != null) return;
        if (unit == null || !WorldBattles.IsPlayers(unit)) return;
        unit.autoSurvey = on;
        if (on)
        {
            unit.autoExplore = false;
            unit.quarryId = -1;
            if (unit.Camping && !unit.resting) WorldUnits.BreakCamp(unit);
            if (!unit.surveying && unit.surveyPaused && WhyNotResumeSurvey(unit) == null) ResumeSurvey(unit);
        }
        Changed?.Invoke();
    }

    // A surveyor standing idle: after a wait for others to move, it goes on with its survey; one surveying by itself
    // with no survey under way takes up the one it set aside or picks the next cell. True when anything changed.
    private bool TickSurvey(WorldUnit unit, UnitSpec spec, float sevenths, List<string> notice)
    {
        if (unit.Working || unit.Camping || unit.Moving) return false;
        if (unit.surveyWait > 0f)
        {
            unit.surveyWait = Math.Max(0f, unit.surveyWait - sevenths);
            if (unit.surveyWait > 0f) return false;
        }
        if (unit.surveying)
        {
            ContinueSurvey(unit, spec, notice);
            return true;
        }
        if (!unit.autoSurvey) return false;
        if (unit.surveyPaused && WhyNotResumeSurvey(unit) == null)
        {
            unit.surveyPaused = false;
            unit.surveying = true;
            ContinueSurvey(unit, spec, notice);
            return true;
        }
        unit.surveyPaused = false;
        var care = LookAfterItself(unit, spec);
        if (care == SelfCare.Acted) return true;
        if (care == SelfCare.Stranded)
        {
            unit.autoSurvey = false;
            UnitSays(unit, $"{unit.name} stops surveying by itself", StrandedText(unit));
            return true;
        }
        if (NextToSurvey(unit, spec, out var cell))
        {
            unit.surveying = true;
            unit.surveyCell = cell;
            unit.surveyWork = 0f;
            ContinueSurvey(unit, spec, notice);
            return true;
        }
        unit.autoSurvey = false;
        UnitSays(unit, "Nothing left to survey nearby", $"{unit.name} finds no ground in sight still to survey within reach and waits for orders.");
        return true;
    }

    // The next cell for a party surveying by itself: the nearest by walking (within about ten Sevenths' walk) of the
    // walkable cells out of the fog with hexes still to survey, outside settlements, that no other party is surveying.
    private bool NextToSurvey(WorldUnit unit, UnitSpec spec, out HexCoord cell)
    {
        cell = unit.coord;
        var gen = Settings.generation;
        var grid = MicroNavigation.Grid(Map, gen);
        var taken = new HashSet<HexCoord>(Map.Units.Where(u => u != unit && u.surveying && !u.Missing).Select(u => u.surveyCell));
        var candidates = new HashSet<int>();
        foreach (var t in Map.Tiles)
        {
            if (!t.revealed || t.water || t.impassable || t.settlement >= 0 || taken.Contains(t.coord)) continue;
            if (((t.microSurveyMask | t.microBlockedMask) & 127) == 127) continue;
            candidates.Add(t.index);
        }
        if (candidates.Count == 0) return false;
        float reach = Math.Max(24f, spec.stamina * 10f);
        bool found = MicroNavigation.FindNearest(Map, gen, WorldUnits.MicroPosition(unit),
            id => candidates.Contains(id / MicroNavigation.PerCell) && !MicroNavigation.Surveyed(Map, id) && !float.IsPositiveInfinity(grid.Enter(id)),
            out var path, out _, reach, PlanVisits);
        if (!found) return false;
        int end = path.Count > 0 ? path[path.Count - 1] : MicroNavigation.Index(Map, WorldUnits.MicroPosition(unit));
        cell = Map[end / MicroNavigation.PerCell].coord;
        return true;
    }

    // A cell just explored: its feature is investigated (rewards, legends, stories) and its resource sites identified.
    private void Investigate(WorldTile tile, WorldUnit unit, UnitSpec spec, List<string> notice)
    {
        if (tile.HasFeature) notice.AddRange(Discover(tile, unit, spec));
        IdentifySites(new[] { tile }, unit, spec, notice);
        MarkYieldsDirty();
        Map.CivilizationVersion++;
    }

    // What exploring a cell turned up besides its sites, drawn from the world's seed and the cell (a party passing by
    // and a survey draw apart; a survey is far likelier to find something): spare resources, straight into the stores,
    // and an event (Expeditions.RollSurvey).
    private void RollFinds(WorldTile tile, WorldUnit unit, UnitSpec spec, bool surveyed, List<string> notice)
    {
        var x = ExpeditionRules;
        if (x == null || tile == null || unit == null || tile.coord == Map.Capital || tile.settlement >= 0) return;
        int stream = WorldNoise.Stream(Map.seed, surveyed ? "survey-finds" : "passing-finds");
        // Cover keeps its secrets: finds are likelier inside it.
        var (find, cache) = Expeditions.RollSurvey(x, surveyed, WorldNoise.Hash01(stream, tile.index, 0), WorldNoise.Hash01(stream, tile.index, 1), WorldNoise.Hash01(stream, tile.index, 2), tile.coverFinds);
        if (cache)
        {
            var spare = Expeditions.Cache(x, Settings.generation, tile, spec != null ? spec.rewardMultiplier : 1f);
            var units = GameUnitsLogic.Instance;
            foreach (var a in spare) units?.ChangeResourceFromName(a.resource, a.amount, false);
            if (spare.Count > 0) notice.Add($"{UnitLabel(unit)} finds spare resources at {Place(tile)}: {string.Join(", ", spare.Select(a => $"+{a.amount:0.#} {a.resource}"))}.");
        }
        if (find != null) ApplyFind(find, tile, unit, spec);
    }

    // Survivors sheltering in a cell just explored (Expeditions.SurvivorBand, one draw per cell from the world's seed):
    // the party tells them the way, and they set out for the Capital, where they wait at the gates for rations.
    private void RollSurvivors(WorldTile tile, WorldUnit unit, bool surveyed, bool exploredBefore)
    {
        var x = ExpeditionRules;
        var pop = PopGrowthLogic.Instance;
        if (x == null || pop == null || tile == null || unit == null || tile.water || tile.coord == Map.Capital || tile.settlement >= 0) return;
        int stream = WorldNoise.Stream(Map.seed, "survivors");
        int band = Expeditions.SurvivorBand(x, surveyed, exploredBefore, tile.macroBiome, WorldNoise.Hash01(stream, tile.index, 0), WorldNoise.Hash01(stream, tile.index, 1));
        if (band <= 0) return;
        int sevenths = Expeditions.SurvivorTravel(x, HexCoord.Distance(tile.coord, Map.Capital));
        pop.SurvivorsFound(band, sevenths, "survivors:" + tile.index);
        GameLog.Event($"Survivors ({band}) found at {tile.coord} by {unit.name}; at the Capital in {sevenths} Seventh(s)", Log);
        UnitSays(unit, "Survivors", $"{UnitLabel(unit)} finds {band} survivors sheltering at {Place(tile)} and shows them the way home: they reach the Capital in {sevenths} Seventh{(sevenths == 1 ? string.Empty : "s")}.");
    }

    // A survey event: its effect on the land or the party, and a notice of its own.
    private void ApplyFind(SurveyFindSpec find, WorldTile tile, WorldUnit unit, UnitSpec spec)
    {
        string place = Place(tile);
        string text = !string.IsNullOrEmpty(find.text) ? find.text.Replace("{0}", place) : $"finds {find.name} at {place}.";
        switch (find.kind)
        {
            case SurveyFindKind.Vantage:
                Map.Reveal(tile.coord, Math.Max(1, find.reveal));
                break;
            case SurveyFindKind.Spring:
                if (spec != null) unit.supplies = Math.Max(unit.supplies, spec.supplyCapacity);
                unit.fatigue = Math.Max(0f, unit.fatigue - Math.Max(0f, find.rest));
                break;
            case SurveyFindKind.Relic:
                var legends = LegendProgress.Instance;
                if (legends != null && find.fragments > 0)
                    foreach (var member in Expeditions.Members(unit).ToList()) legends.Award(member, LegendLore.FragmentTuning.discovery, find.fragments, $"Found {find.name} at {place}");
                break;
            case SurveyFindKind.Story:
                Tell(find.story, unit);
                break;
        }
        GameLog.Event($"Survey find '{find.name}' at {tile.coord} ({unit.name})", Log);
        UnitSays(unit, find.name, $"{UnitLabel(unit)} {text}");
    }

    private void FinishForage(WorldUnit unit, UnitSpec spec, WorldTile tile, List<string> notice)
    {
        var gathered = WorldUnits.ForageOf(Settings.generation, tile, spec.rewardMultiplier * WorldUnits.ForageRichness(Provisions, tile));
        tile.foragedAge = AgeNumber + 1;
        var units = GameUnitsLogic.Instance;
        foreach (var a in gathered) units?.ChangeResourceFromName(a.resource, a.amount, false);
        if (gathered.Count > 0) notice.Add($"{UnitLabel(unit)} foraged {Place(tile)}: {string.Join(", ", gathered.Select(a => $"+{a.amount:0.#} {a.resource}"))}.");
    }

    private void FinishImprove(WorldUnit unit, WorldTile tile, List<string> notice)
    {
        if (WorldUnits.WhyNotImprove(Map, Settings.generation, Rules, tile) != null) return;
        tile.improvement++;
        notice.Add($"{UnitLabel(unit)} improved {Place(tile)} to level {tile.improvement} (+{Rules.improvementBonus * tile.improvement:P0} yields).");
        bool masterwork = tile.improvement >= Rules.maxImprovement;
        if (masterwork) EraOnce("masterwork:" + tile.index, Rules.eraMasterwork, $"A masterwork at {Place(tile)}");
        // Creation is growth (Fragments of Vision): every legend who worked it, more for a masterwork.
        var legends = LegendProgress.Instance;
        foreach (var member in Expeditions.Members(unit).ToList())
        {
            legends?.Award(member, LegendLore.FragmentTuning.improvement, $"Improved {Place(tile)}");
            if (masterwork) legends?.Award(member, LegendLore.FragmentTuning.masterwork, $"A masterwork at {Place(tile)}");
        }
        Map.CivilizationVersion++;
        RecomputeYields();
    }

    // ===== EXPEDITIONS: FORMING AND THE PARTY =====

    // Ambition's Expedition Cost lightens the outfit too: forming, each companion, and settlers' goods and food.
    private static float OutfitScale => CivilizationProperties.Expedition.cost;

    private float SettlerFoodValue => ExpeditionRules.settlerFoodValue * OutfitScale;

    /// <summary>What forming an expedition or adding a companion costs now, for the buttons ("12 Elderwood").</summary>
    public string OutfitCostText => CostText(ExpeditionRules.outfitCost, OutfitScale);

    // The settlement an expedition stands in, when it can outfit one there (not an Outpost; a tributary only as a Militant District).
    private Settlement OutfittingAt(WorldUnit unit)
    {
        var t = unit != null ? Map.Get(unit.coord) : null;
        var s = t != null && t.settlement >= 0 && t.settlement < Map.Settlements.Count ? Map.Settlements[t.settlement] : null;
        return CanOutfit(s) ? s : null;
    }

    /// <summary>Expeditions form and take on companions at <paramref name="s"/>: any settlement but an Outpost, and among tributaries only a district that outfits them (a Militant District).</summary>
    public bool CanOutfit(Settlement s) =>
        s != null && s.kind != SettlementKind.Outpost && (s.kind != SettlementKind.Tributary || WorldTributaries.Outfits(Rules.tributaries, s));

    /// <summary>Why no expedition can form at <paramref name="at"/> around <paramref name="director"/> now, or null.</summary>
    public string WhyNotForm(Settlement at, string director)
    {
        if (Map == null) return "There is no world.";
        if (!MapUnlocked) return $"Research {Settings.mapTechnology} to send expeditions.";
        if (at == null) return "Expeditions form at a settlement.";
        if (at.kind == SettlementKind.Outpost) return "An Outpost cannot outfit an expedition.";
        if (!CanOutfit(at)) return "Among the tributaries only a Militant District outfits expeditions.";
        return WhyNotJoin(director) ?? Expeditions.WhyNotAddLegend(0, FreeExpeditionSlots, ExpeditionRules) ?? Unaffordable(ExpeditionRules.outfitCost, OutfitScale);
    }

    /// <summary>
    /// Form an expedition at <paramref name="at"/> directed by <paramref name="director"/> (who leaves the council if
    /// seated), paying its outfit unless <paramref name="free"/>. It takes one slot; companions join it while it stands
    /// in a settlement.
    /// </summary>
    public WorldUnit FormExpedition(Settlement at, string director, bool free = false) => FormExpedition(at, director, ExpeditionCharter.Expedition, free);

    /// <summary>Form an expedition with a charter: to explore, to build (Builders) or to celebrate (a Cultural Party).</summary>
    public WorldUnit FormExpedition(Settlement at, string director, ExpeditionCharter charter, bool free = false)
    {
        string why = WhyNotForm(at, director) ?? WhyNotCharter(charter);
        if (why != null && !(free && why == Unaffordable(ExpeditionRules.outfitCost, OutfitScale))) { Say(why); return null; }
        var spec = Settings.Unit(ExpeditionRules.unit);
        if (spec == null || spec.role != UnitRole.Expedition)
        {
            GameLog.Warning($"No expedition unit '{ExpeditionRules.unit}' in WorldSettings.units.", Log);
            return null;
        }
        if (!free) Pay(ExpeditionRules.outfitCost, OutfitScale);
        LeaveCouncil(director);
        var unit = Deploy(spec, at.coord, leader: director);
        if (charter != ExpeditionCharter.Expedition)
        {
            unit.charter = charter;
            unit.name = $"{director}'s {CharterName(charter, plural: false)}";
            _partySpecs.Remove(unit.id);
        }
        GameLog.Event($"{unit.name} forms at {at.name} (Director {director})", Log);
        Say($"{director} forms {unit.name} at {at.name}.");
        Changed?.Invoke();
        return unit;
    }

    /// <summary>Why <paramref name="legend"/> cannot join <paramref name="unit"/> now, or null (companions join in a settlement).</summary>
    public string WhyNotAddCompanion(WorldUnit unit, string legend)
    {
        if (!IsExpedition(unit)) return "Only an expedition takes companions.";
        if (OutfittingAt(unit) == null) return "Companions join while the expedition stands in one of your settlements (not an Outpost).";
        return WhyNotJoin(legend) ?? Expeditions.WhyNotAddLegend(Expeditions.PartySize(unit), FreeExpeditionSlots, ExpeditionRules) ?? Unaffordable(ExpeditionRules.outfitCost, OutfitScale);
    }

    public bool AddCompanion(WorldUnit unit, string legend)
    {
        string why = WhyNotAddCompanion(unit, legend);
        if (why != null) { Say(why); return false; }
        Pay(ExpeditionRules.outfitCost, OutfitScale);
        LeaveCouncil(legend);
        unit.companions.Add(legend);
        LegendProgress.Instance?.ShareExperience(Expeditions.Members(unit), "join:" + System.Guid.NewGuid(), $"{legend} joined {unit.name}.");
        Say($"{legend} joins {unit.name}.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>Why <paramref name="legend"/> cannot leave <paramref name="unit"/> now, or null (in a settlement, and never the last legend: disband instead).</summary>
    public string WhyNotSendHome(WorldUnit unit, string legend)
    {
        if (!IsExpedition(unit) || !Expeditions.IsMember(unit, legend)) return $"{legend} is not with this expedition.";
        if ((Map.Get(unit.coord)?.settlement ?? -1) < 0) return "Legends leave the party in one of your settlements.";
        if (Expeditions.PartySize(unit) <= 1) return "The last legend disbands the expedition instead.";
        return null;
    }

    /// <summary>Send <paramref name="legend"/> home from the party (the next companion directs if it was the Director).</summary>
    public bool SendHome(WorldUnit unit, string legend)
    {
        string why = WhyNotSendHome(unit, legend);
        if (why != null) { Say(why); return false; }
        Expeditions.Remove(unit, legend);
        Say($"{legend} leaves {unit.name} and goes home{(unit.leader != null ? $"; {unit.leader} directs it" : string.Empty)}.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>Put a companion at the head of the party (anywhere: the party agrees on the road).</summary>
    public bool SetDirector(WorldUnit unit, string legend)
    {
        if (!IsExpedition(unit) || !Expeditions.MakeDirector(unit, legend)) return false;
        Say($"{legend} now directs {unit.name}.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>Why the expedition cannot disband now, or null: its legends go home from one of your settlements.</summary>
    public string WhyNotDisband(WorldUnit unit)
    {
        if (!IsExpedition(unit)) return "Only an expedition disbands.";
        if (unit.Missing) return "No one knows where it is.";
        if (unit.Working) return "Already at work.";
        if ((Map.Get(unit.coord)?.settlement ?? -1) < 0) return "An expedition disbands in one of your settlements; send it back first.";
        return null;
    }

    /// <summary>The expedition ends: its legends go home and its settlers rejoin your citizens.</summary>
    public bool Disband(WorldUnit unit)
    {
        string why = WhyNotDisband(unit);
        if (why != null) { Say(why); return false; }
        Map.Units.Remove(unit);
        _partySpecs.Remove(unit.id);
        ReturnSettlers(unit, SettlementAt(unit));
        Say($"{unit.name} disbands at {Place(Map.Get(unit.coord))}: {string.Join(", ", Expeditions.Members(unit))} return home.");
        Changed?.Invoke();
        return true;
    }

    private static void ReturnSettlers(WorldUnit unit, Settlement at)
    {
        var pop = PopGrowthLogic.Instance;
        if (unit.settlers <= 0 || pop == null) return;
        // Housed where there is room, the rest as vagrants; admitted where the expedition disbands, from where they were taken on.
        pop.AddMigrants(unit.settlers, SettlersReturnOrigin(unit), "settlers", at != null ? at.id : -1);
        unit.settlers = 0;
        pop.RefreshHUD();
    }

    /// <summary>Why the expedition cannot take on settlers here now, or null.</summary>
    public string WhyNotTakeSettlers(WorldUnit unit)
    {
        if (!IsExpedition(unit)) return "Only an expedition escorts settlers.";
        if (unit.charter != ExpeditionCharter.Expedition) return $"{CharterName(unit.charter)} escort no settlers: only an exploring expedition does.";
        if (unit.settlers > 0) return $"It already escorts {unit.settlers} settlers.";
        if (OutfittingAt(unit) == null) return "Settlers join in one of your settlements (not an Outpost).";
        if (!Researched(SettlersTechnology)) return $"Research {SettlersTechnology} first.";
        int count = Math.Max(1, ExpeditionRules.settlers);
        if (PopGrowthLogic.Instance == null || PopGrowthLogic.Instance.population <= count) return $"Needs {count} citizens to set out.";
        return Pantry.WhyNotAfford(SettlerFoodValue) ?? Unaffordable(ExpeditionRules.settlerCost, OutfitScale);
    }

    /// <summary>What taking on settlers costs, for the card ("5 citizens, 15 Elderwood").</summary>
    public string SettlerCostText
    {
        get
        {
            var x = ExpeditionRules;
            var parts = new List<string> { $"{Math.Max(1, x.settlers)} citizens" };
            if (SettlerFoodValue > 0f) parts.Add($"{SettlerFoodValue:0.#} stored food value");
            if (x.settlerCost.Any(c => c != null && c.amount > 0f)) parts.Add(CostText(x.settlerCost, OutfitScale));
            return string.Join(", ", parts);
        }
    }

    // Settlers need what settlers always needed: the map's technology (Pathfinder Training teaches both).
    private string SettlersTechnology => Settings.mapTechnology;

    /// <summary>Take on settlers (citizens and their cost): the party slows, and can found one settlement.</summary>
    public bool TakeSettlers(WorldUnit unit)
    {
        string why = WhyNotTakeSettlers(unit);
        if (why != null) { Say(why); return false; }
        if (!Pantry.TrySpend(SettlerFoodValue)) { Say(Pantry.WhyNotAfford(SettlerFoodValue)); return false; }
        Pay(ExpeditionRules.settlerCost, OutfitScale);
        int count = Math.Max(1, ExpeditionRules.settlers);
        TakeCitizens(count);
        unit.settlers = count;
        CulturalSettlersTaken(unit, OutfittingAt(unit));
        Say($"{count} settlers join {unit.name}, carrying a hearth to new ground.");
        Changed?.Invoke();
        return true;
    }

    // ===== EXPEDITIONS: THE ROAD =====

    /// <summary>The legends of an expedition as the rules see them (Composure from <see cref="LegendProgress"/>).</summary>
    public List<PartyMember> Party(WorldUnit unit)
    {
        var legends = LegendProgress.Instance;
        return Expeditions.Members(unit).Select(n => new PartyMember
        {
            name = n,
            director = string.Equals(n, unit.leader, StringComparison.OrdinalIgnoreCase),
            state = legends != null ? legends.Composure(n) : ComposureState.Clouded,
            strain = legends?.Soul(n)?.strain ?? 0f,
        }).ToList();
    }

    /// <summary>The chance a mishap strikes the expedition this Seventh (for the card).</summary>
    public float MishapRisk(WorldUnit unit) => IsExpedition(unit) ? Expeditions.MishapRisk(unit, UnitSurroundings.Of(Map, unit), Party(unit), ExpeditionRules) : 0f;

    // Once a Seventh, fortune turns for each expedition: whether a mishap strikes, which and whom, drawn from the
    // world's seed and the expedition's own count of rolls (so a save replays the same fortune).
    private void RollMishaps()
    {
        var legends = LegendProgress.Instance;
        if (legends == null) return;
        int stream = WorldNoise.Stream(Map.seed, "expedition-mishaps");
        // How the party answers is drawn from its own stream, so the mishaps themselves replay as before.
        int answers = WorldNoise.Stream(Map.seed, "expedition-answers");
        foreach (var unit in ExpeditionUnits.ToList())
        {
            if (unit.Missing) continue;
            int roll = unit.mishapRolls++;
            double chance = WorldNoise.Hash01(stream, unit.id, roll * 3), pick = WorldNoise.Hash01(stream, unit.id, roll * 3 + 1), aim = WorldNoise.Hash01(stream, unit.id, roll * 3 + 2);
            var party = Party(unit);
            var mishap = Expeditions.RollMishap(unit, UnitSurroundings.Of(Map, unit), party, ExpeditionRules, chance, pick, aim);
            if (mishap == null) continue;
            PartyShapes.Respond(mishap, party, ExpeditionRules, WorldNoise.Hash01(answers, unit.id, roll));
            Strike(unit, mishap);
        }
    }

    // A mishap lands as the party answered it (mishap.outcome): in full; eased by a companion, who earns the deed; or
    // slipped (an ambush), after which a party small enough withdraws to safe ground.
    private void Strike(WorldUnit unit, Mishap mishap)
    {
        var spec = mishap.spec;
        var x = ExpeditionRules;
        var (title, text) = Expeditions.Describe(mishap, unit.name, Place(Map.Get(unit.coord)));
        Expeditions.Afflict(unit, spec, PartyShapes.WearShare(mishap.outcome, x), CivilizationProperties.Expedition.cost);
        float strainShare = PartyShapes.StrainShare(mishap.outcome, x);
        var members = Expeditions.Members(unit).ToList();
        if (spec.kind == MishapKind.Desertion && mishap.outcome == MishapOutcome.Struck)
        {
            // The deserter makes for home; those left behind carry it.
            Expeditions.Remove(unit, mishap.target);
            members.Remove(mishap.target);
        }
        UnitSays(unit, title, text);
        GameLog.Event($"Mishap: {title} ({unit.name}, {mishap.outcome}, risk {MishapRisk(unit):P0})", Log);
        var legends = LegendProgress.Instance;
        string cause = $"{spec.name ?? spec.kind.ToString()} on the road";
        string relationshipMoment = "road:" + System.Guid.NewGuid();
        if (spec.kind == MishapKind.Quarrel && mishap.outcome == MishapOutcome.Struck && mishap.second != null)
        {
            legends?.Relate(mishap.target, mishap.second, relationshipMoment, $"Quarrelled during {cause}.", -1, "Cindergale", true, -18);
            legends?.Relate(mishap.second, mishap.target, relationshipMoment, $"Quarrelled during {cause}.", -1, "Cindergale", true, -18);
        }
        if (spec.kind == MishapKind.Desertion && mishap.outcome == MishapOutcome.Struck)
        {
            foreach (var member in members)
                legends?.Relate(member, mishap.target, relationshipMoment, $"{mishap.target} deserted {unit.name}.", -1, "Strand", true, -30);
        }
        else if (mishap.outcome == MishapOutcome.Eased && mishap.helper != null)
        {
            legends?.Relate(mishap.target, mishap.helper, relationshipMoment, $"{mishap.helper} helped them through {cause}.", 1, "Void", true, 18);
            legends?.Relate(mishap.helper, mishap.target, relationshipMoment, $"Stayed with {mishap.target} through {cause}.", 1, "Void", true, 18);
        }
        // Surviving a real injury together tests an existing thread; an evaded mishap is only a shared memory.
        bool sharedHardship = spec.kind != MishapKind.Quarrel && spec.kind != MishapKind.Desertion && mishap.outcome != MishapOutcome.Evaded && (spec.strain > 0f || spec.partyStrain > 0f);
        legends?.ShareExperience(members, relationshipMoment, $"Faced {cause} together.", sharedHardship ? 1 : 0, "Void", sharedHardship, sharedHardship ? 10 : 0);
        if (spec.kind != MishapKind.Desertion && spec.strain > 0f)
        {
            legends.Strain(mishap.target, spec.strain * strainShare, cause);
            if (mishap.second != null) legends.Strain(mishap.second, spec.strain * strainShare, cause);
            // Those it struck and who still stand have overcome it (against the odds: an Underdog earns double).
            legends.Award(mishap.target, LegendLore.FragmentTuning.mishapEndured, $"Endured {cause}", againstTheOdds: true);
            if (mishap.second != null) legends.Award(mishap.second, LegendLore.FragmentTuning.mishapEndured, $"Endured {cause}", againstTheOdds: true);
        }
        if (spec.partyStrain > 0f)
            foreach (var m in members.Where(m => m != mishap.target && m != mishap.second)) legends.Strain(m, spec.partyStrain * strainShare, cause);
        if (mishap.outcome == MishapOutcome.Eased && mishap.helper != null)
            legends.Award(mishap.helper, LegendLore.FragmentTuning.mishapEndured, $"Saw {mishap.target} through {cause}");
        _partySpecs.Remove(unit.id);
        if (mishap.outcome == MishapOutcome.Evaded && WhyNotRetreat(unit) == null) Retreat(unit, bySelf: true);
        Changed?.Invoke();
    }

    /// <summary>
    /// A legend was lost to Dissonance (<see cref="LegendProgress"/>): it leaves its expedition, whose other legends grieve;
    /// an expedition left without a legend is lost with any settlers it escorted.
    /// </summary>
    public void OnLegendLost(string legend)
    {
        var unit = ExpeditionOf(legend);
        if (unit == null) return;
        Expeditions.Remove(unit, legend);
        var place = Place(Map.Get(unit.coord));
        var left = Expeditions.Members(unit).ToList();
        if (left.Count == 0)
        {
            Map.Units.Remove(unit);
            _partySpecs.Remove(unit.id);
            UnitSays(unit, $"{unit.name} is lost", $"With {legend} gone, no legend is left to lead {unit.name} at {place}.{(unit.settlers > 0 ? $" The {unit.settlers} settlers it escorted scatter." : string.Empty)}");
        }
        else
        {
            UnitSays(unit, $"{legend} falls at {place}", $"{string.Join(", ", left)} watched {legend}'s Soul Leitmotif break on the road.");
            var legends = LegendProgress.Instance;
            foreach (var m in left) legends?.Strain(m, ExpeditionRules.lossStrain, $"Watched {legend} fall");
            foreach (var m in left) legends?.Award(m, LegendLore.FragmentTuning.companionLost, $"Let {legend} go");
        }
        Changed?.Invoke();
    }

    private void OnSeventh(int _)
    {
        SyncCalendar();
        // Settlements grow toward their City Development potential (slower when the administration is overstretched);
        // yields follow whole points.
        if (Map.Settlements.Count > 0 && WorldCivilization.Tick(Map, Rules, 1, Realm.efficiency))
        {
            RecomputeYields();
            Changed?.Invoke();
        }
        TerritoryTick(1f);
        if (!SaveSession.Restoring)
        {
            // Settlements are harmed and heal; the fallen become ruins (WorldRuins).
            LossTick(1);
            TickMissing();
            RollMishaps();
            GrievanceTick();
        }
    }

    // ===== DISCOVERY =====

    /// <summary>The name of a place as the map knows it: its settlement, its feature, else its ground.</summary>
    public string Place(WorldTile tile)
    {
        if (tile == null) return "unknown ground";
        var gen = Settings.generation;
        if (tile.settlement >= 0 && tile.settlement < Map.Settlements.Count) return Map.Settlements[tile.settlement].name;
        var ruin = tile.known ? WorldRuins.At(Map, tile.coord) : null;
        if (ruin != null) return $"the ruins of {ruin.name}";
        var feature = tile.HasFeature ? gen.Feature(tile.feature) : null;
        // What stands inside concealing cover keeps its name until the cell is explored: it is just "Deep Forest".
        if (feature != null && !WorldCover.Hides(tile) && (tile.known || (feature.visibleFromAfar && tile.revealed))) return feature.name;
        var cover = WorldCover.SpecAt(gen, tile);
        if (cover != null && (tile.known || tile.revealed)) return cover.name;
        var terrain = gen.Terrain(tile.terrain);
        // Land nobody has walked is unknown wilderness, even out of the fog.
        return tile.known && terrain != null ? terrain.name : tile.revealed ? "unknown wilderness" : "the fog";
    }

    /// <summary>
    /// What investigating a site does (a survey reaching it): its reward (multiplied for an expedition), a legend, its
    /// story (for an expedition, the next knot of the site's expedition line) and Era Score for a landmark. Returns the
    /// notice lines.
    /// </summary>
    private List<string> Discover(WorldTile tile, WorldUnit by, UnitSpec spec)
    {
        var notice = new List<string>();
        var feature = tile.HasFeature ? Settings.generation.Feature(tile.feature) : null;
        if (feature == null || tile.coord == Map.Capital) return notice;
        string leader = by?.leader;
        float multiplier = spec != null ? Math.Max(0f, spec.rewardMultiplier) : 1f;
        notice.Add($"Found: {feature.name}.");
        var units = GameUnitsLogic.Instance;
        foreach (var reward in feature.rewards.Where(r => r != null && !string.IsNullOrEmpty(r.resource) && r.amount != 0f))
        {
            float amount = reward.amount * multiplier;
            if (units != null) units.ChangeResourceFromName(reward.resource, amount, false);
            notice.Add($"+{amount:0.#} {reward.resource}.");
        }
        var legends = LegendProgress.Instance;
        if (feature.recruitsLegend && legends != null)
        {
            var met = legends.RecruitNext($"Met at {feature.name}");
            if (met != null) notice.Add($"{met.legendName} joins your civilization.");
        }
        // Every legend of the party that found it is remembered for it.
        if (by != null && legends != null && feature.renown > 0)
            foreach (var member in Expeditions.Members(by).ToList()) legends.Award(member, LegendLore.FragmentTuning.discovery, feature.renown, $"Found {feature.name}");
        // Landmarks: seen from afar or tagged so; unique ones (a single one in the world) are worth the most.
        bool landmark = feature.visibleFromAfar || string.Equals(feature.tag, "landmark", StringComparison.OrdinalIgnoreCase);
        if (landmark) EraOnce("landmark:" + tile.index, feature.count == 1 ? Rules.eraUniqueLandmark : Rules.eraLandmark, $"Discovered {feature.name}");
        string story = feature.story;
        if (spec != null && spec.role == UnitRole.Expedition && feature.expeditionStories != null)
        {
            _expeditionLines.TryGetValue(feature.id, out int told);
            if (told < feature.expeditionStories.Count && !string.IsNullOrEmpty(feature.expeditionStories[told]))
            {
                story = feature.expeditionStories[told];
                _expeditionLines[feature.id] = told + 1;
            }
        }
        if (!string.IsNullOrEmpty(story) && !Tell(story, by)) GameLog.Warning($"Feature '{feature.id}': story '{story}' was not found in Resources/Events.", Log);
        GameLog.Event($"Found {feature.id} at {tile.coord}{(leader != null ? $" ({leader})" : string.Empty)}", Log);
        return notice;
    }

    // Offer an ink story found on the land: the party that found it plays it, its Director the protagonist and its
    // companions co-protagonists. False when the story does not exist.
    private bool Tell(string story, WorldUnit by)
    {
        if (string.IsNullOrEmpty(story)) return false;
        string leader = by?.leader;
        if (leader != null) LastLeader = leader;
        if (leader != null && EventSystemLogic.Instance != null)
            EventSystemLogic.Instance.Summon(story, leader, Expeditions.Members(by).Where(m => !string.Equals(m, leader, StringComparison.OrdinalIgnoreCase)));
        var volumes = EventVolumeManager.Instance;
        if (volumes == null || !volumes.UnlockStory(story)) return false;
        EventSystemLogic.Instance?.TriggerEventCheck();
        return true;
    }

    /// <summary>Award Era Score for a deed once (keyed so the same place never pays twice). True when it was awarded.</summary>
    private bool EraOnce(string key, int points, string reason)
    {
        if (points <= 0 || AgeProgression.Instance == null || AgeProgression.Instance.Current == null || !_eraAwarded.Add(key)) return false;
        AgeProgression.Award(points, reason);
        return true;
    }

    // ===== YIELDS =====

    private void MarkYieldsDirty()
    {
        if (_yieldsDirty) return;
        _yieldsDirty = true;
        _yieldsAt = Time.unscaledTime + 0.5f;
    }

    /// <summary>Everything the map produces, as flat production and percent sources (one per kind of land, settlement, field).</summary>
    private void RecomputeYields()
    {
        _yieldsDirty = false;
        foreach (var source in _yieldSources) EffectRouter.RemoveSource(source);
        _yieldSources.Clear();
        if (Map == null) return;
        var all = WorldUnits.LandYields(Map, Settings.generation, Rules).Concat(WorldCivilization.Yields(Map, Settings.generation, Rules));
        // An overstretched administration gets less out of the land and the towns (WorldTerritory.Efficiency).
        float efficiency = ComputeRealm().efficiency * CapitalOutput;
        foreach (var group in all.GroupBy(y => y.source))
        {
            var effects = group.Where(y => GameCatalog.IsResource(y.resource))
                .Select(y => y.percent
                    ? new GameEffect(GameEffectType.ProductionModifier, y.amount, ModifierType.Percentage, y.resource)
                    : new GameEffect(GameEffectType.ResourceModifier, y.amount * efficiency, ModifierType.Add, y.resource)).ToList();
            if (effects.Count == 0) continue;
            EffectRouter.ApplySet(group.Key, effects);
            _yieldSources.Add(group.Key);
        }
        // The pillar districts raise their pillar (Auric Aureus, Weaver Waltz, Regal Regalia, Esoteric Chorus), others a substat or morale.
        foreach (var group in WorldTributaries.StatEffects(Map, Rules.tributaries).GroupBy(e => e.source))
        {
            EffectRouter.ApplySet(group.Key, group.Select(e => e.effect).ToList());
            _yieldSources.Add(group.Key);
        }
        // The outskirts house the Capital's people (desirable ground fills its hamlets fastest).
        var pop = PopGrowthLogic.Instance;
        if (pop != null)
        {
            int housing = WorldTributaries.Housing(Map, Rules.tributaries);
            if (pop.GetBaseHousing(TributaryHousing) != housing) pop.SetBaseHousing(housing, TributaryHousing);
        }
    }

    /// <summary>The housing source the outskirt tributaries give (<see cref="PopGrowthLogic.SetBaseHousing"/>).</summary>
    public const string TributaryHousing = "Outskirt tributaries";

    /// <summary>Resource per second the land and the settlements yield, by resource (for the world view).</summary>
    public Dictionary<string, float> TotalYields()
    {
        var totals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (Map == null) return totals;
        float efficiency = Realm.efficiency * CapitalOutput;
        foreach (var y in WorldUnits.LandYields(Map, Settings.generation, Rules).Concat(WorldCivilization.Yields(Map, Settings.generation, Rules)).Where(y => !y.percent))
        {
            totals.TryGetValue(y.resource, out float sum);
            totals[y.resource] = sum + y.amount * efficiency;
        }
        return totals;
    }

    /// <summary>What one cell yields per second now (its ground if held, its feature, its improvement), for the hover card.</summary>
    public List<ResourceAmount> CellYields(WorldTile t)
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (t == null || t.water || !t.explored) return new List<ResourceAmount>();
        // As the ledger counts it: raised by improvements, by the beauty people work willingly, lowered by an overstretched administration.
        float scale = (1f + Rules.improvementBonus * t.improvement) * Realm.efficiency * CapitalOutput;
        float work = Math.Max(0f, 1f + Rules.territory.beautyWork * t.beauty);
        var gen = Settings.generation;
        if (t.authorityId == WorldAuthority.Player)
            foreach (var y in gen.Terrain(t.terrain)?.yields ?? new List<ResourceAmount>()) if (y != null) result[y.resource] = (result.TryGetValue(y.resource, out var a) ? a : 0f) + y.amount * scale * work;
        if (t.HasFeature)
            foreach (var y in gen.Feature(t.feature)?.yields ?? new List<ResourceAmount>()) if (y != null) result[y.resource] = (result.TryGetValue(y.resource, out var a) ? a : 0f) + y.amount * scale;
        if (WorldAuthority.IsPlayers(t.authorityId))
            foreach (var y in WorldResources.YieldsAt(Map, gen, t).Concat(WorldCover.YieldsOf(gen, t))) result[y.resource] = (result.TryGetValue(y.resource, out var a) ? a : 0f) + y.amount * scale * work;
        return result.Where(p => p.Value != 0f).Select(p => new ResourceAmount { resource = p.Key, amount = p.Value }).ToList();
    }

    // ===== AGES =====

    /// <summary>The current Age's number (0 before the Ages start).</summary>
    public int AgeNumber => AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.number : 0;

    /// <summary>The leylines the next Age would bring (the Age preview; changes nothing).</summary>
    public List<Leyline> ForecastNextAge() => Map?.Magic != null ? Map.Magic.Forecast(Map, AgeNumber + 1) : new List<Leyline>();

    private void OnAgeBegan(AgeDefinition age)
    {
        if (age == null || Map == null) return;
        var messages = new List<string>();
        if (Map.Magic != null && Map.Magic.Apply(Map, age.number) && age.number > 0)
        {
            int silver = Map.Tiles.Count(t => t.silver);
            messages.Add($"The Grand Thread Rings turn: the leylines shift ({Map.Magic.Junctions.Count(j => j.IsBasin)} Basins, {Map.Magic.Junctions.Count(j => !j.IsBasin)} Convergences) and {silver} river reaches run silver.");
        }
        if (_agesPlaced.Add(age.number))
        {
            var placed = WorldGenerator.PlaceFeatures(Map, Settings.generation, age.number);
            int enclavesBefore = Map.Enclaves.Count, threatsBefore = Map.Threats.Count;
            WorldSites.PlaceAge(Map, Settings.generation, age.number);
            WorldCivilization.AgePassed(Map);
            // The aftermath (WorldAftermath): the creatures crash by the ended crisis and their Pure Light, ranges shift, new
            // lineages settle the emptied Macro Biomes, and their memories ease back toward their nature (scarred ones keep more).
            if (age.number > 0) messages.AddRange(AftermathLines(age.number));
            var arrivals = Map.Enclaves.Skip(enclavesBefore).Select(e => $"{e.name} ({e.family} enclave)").ToList();
            if (arrivals.Count > 0) messages.Add($"New enclaves take shape among the survivors: {string.Join(", ", arrivals)}.");
            if (Map.Threats.Count > threatsBefore) messages.Add($"{Map.Threats.Count - threatsBefore} new threat(s) stir in the low-Coherence wilds.");
            // What arises on ground already explored is found at once (it grew where people live).
            foreach (var coord in placed)
            {
                var tile = Map.Get(coord);
                if (tile != null && tile.explored) Discover(tile, null, null);
            }
            if (placed.Count > 0)
            {
                var kinds = placed.Select(c => Map.Get(c)).Where(t => t != null).GroupBy(t => t.feature)
                    .Select(g => $"{g.Count()} x {Settings.generation.Feature(g.Key)?.name ?? g.Key}");
                messages.Add($"The {age.title} brings new things to the world: {string.Join(", ", kinds)}.");
            }
        }
        // The roads stay where they were built; their efficiency follows the moved leylines.
        WorldCivilization.RefreshEfficiency(Map);
        RecomputeYields();
        if (messages.Count > 0) Say(string.Join(" ", messages));
        Changed?.Invoke();
    }

    // ===== QUERIES =====

    /// <summary>Explored cells whose feature carries <paramref name="tag"/> (all explored cells for an empty tag).</summary>
    public int ExploredWithTag(string tag)
    {
        if (Map == null) return 0;
        if (string.IsNullOrEmpty(tag)) return Map.ExploredCount;
        return Map.Tiles.Count(t => t.explored && t.HasFeature && string.Equals(Settings.generation.Feature(t.feature)?.tag, tag, StringComparison.OrdinalIgnoreCase));
    }

    private void Say(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        LastNotice = message;
        Notice?.Invoke(message);
    }

    // ===== CIVILIZATION: SETTLEMENTS, ROADS, ANCHORS, ENCLAVES =====

    /// <summary>Major Settlements the Capital can hold: the base plus each capacity building (Capital improvements).</summary>
    public int GovernmentCapacity
    {
        get
        {
            var units = GameUnitsLogic.Instance;
            int buildings = units != null && !string.IsNullOrEmpty(Rules.capacityBuilding) ? Mathf.FloorToInt(units.GetProductionUnitCount(Rules.capacityBuilding)) : 0;
            return WorldCivilization.GovernmentCapacity(Rules, buildings);
        }
    }

    /// <summary>What is missing to pay <paramref name="costs"/> (scaled), or null.</summary>
    public string Unaffordable(IEnumerable<ResourceAmount> costs, float scale = 1f)
    {
        var units = GameUnitsLogic.Instance;
        foreach (var cost in costs ?? Enumerable.Empty<ResourceAmount>())
        {
            if (cost == null || string.IsNullOrEmpty(cost.resource) || cost.amount <= 0f) continue;
            float need = cost.amount * scale;
            if (units == null || Held(units, cost.resource) + 1e-4f < need) return $"Not enough {cost.resource} ({Mathf.CeilToInt(need)} needed{(IsFood(cost.resource) ? ", from Food in hand and your stores" : string.Empty)}).";
        }
        return null;
    }

    // Food in a cost is paid from the Food in hand, then from the stores' food value (surplus Food is banked there once
    // Resource Storage is known, so the counter itself rarely holds much).
    private static bool IsFood(string resource) => string.Equals(resource, GameCatalog.ResourceNameFor(ResourceRole.Food), StringComparison.OrdinalIgnoreCase);

    private static float Held(GameUnitsLogic units, string resource) =>
        units.GetResourceAmountExact(resource) + (IsFood(resource) ? Pantry.StoredValue : 0f);

    public static string CostText(IEnumerable<ResourceAmount> costs, float scale = 1f)
    {
        var parts = (costs ?? Enumerable.Empty<ResourceAmount>()).Where(c => c != null && !string.IsNullOrEmpty(c.resource) && c.amount > 0f)
            .Select(c => $"{Mathf.CeilToInt(c.amount * scale)} {c.resource}").ToList();
        return parts.Count > 0 ? string.Join(", ", parts) : "free";
    }

    private void Pay(IEnumerable<ResourceAmount> costs, float scale = 1f)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return;
        foreach (var cost in costs ?? Enumerable.Empty<ResourceAmount>())
        {
            if (cost == null || string.IsNullOrEmpty(cost.resource) || cost.amount <= 0f) continue;
            float need = cost.amount * scale;
            if (IsFood(cost.resource))
            {
                float hand = Mathf.Min(need, units.GetResourceAmountExact(cost.resource));
                if (hand > 0f) units.ChangeResourceFromName(cost.resource, -hand, false);
                if (need - hand > 1e-4f) Pantry.TrySpend(Mathf.Min(need - hand, Pantry.StoredValue));
                continue;
            }
            units.ChangeResourceFromName(cost.resource, -need, false);
        }
    }

    /// <summary>Roads and works need the same knowledge as building beyond the walls (the unlock technology).</summary>
    private string Locked() => Map == null ? "There is no world." : !IsOpen ? $"Research {Settings.unlockTechnology} to build beyond the walls." : null;

    public string WhyNotPromote(Settlement s) => Locked() ?? WorldCivilization.WhyNotPromote(Map, Rules, s, GovernmentCapacity) ?? Unaffordable(Rules.promoteCost);

    public bool Promote(Settlement s)
    {
        string why = WhyNotPromote(s);
        if (why != null) { Say(why); return false; }
        Pay(Rules.promoteCost);
        WorldCivilization.Promote(Map, Settings.generation, Rules, s);
        EraOnce("major:" + s.id, Rules.eraMajor, $"{s.name} became a Major Settlement");
        AfterCivilizationChange($"{s.name} becomes a Major Settlement, attuned to {s.binding}.");
        return true;
    }

    public void CycleBinding(Settlement s)
    {
        WorldCivilization.CycleBinding(s);
        AfterCivilizationChange(null);
    }

    public string WhyNotIncorporate(Settlement s) => Locked() ?? WorldCivilization.WhyNotIncorporate(Map, s) ?? Unaffordable(Rules.incorporateCost);

    public bool Incorporate(Settlement s)
    {
        string why = WhyNotIncorporate(s);
        if (why != null) { Say(why); return false; }
        Pay(Rules.incorporateCost);
        WorldCivilization.Incorporate(Map, Settings.generation, Rules, s);
        AfterCivilizationChange($"{s.name} joins your Administrative Authority.");
        return true;
    }

    // ===== TRIBUTARIES AND DISTRICTS =====

    /// <summary>Outskirt tributaries can be raised (their technology, The Rekindling, is researched).</summary>
    public bool TributariesUnlocked => Map != null && Researched(Rules.tributaries.technology);

    /// <summary>What raising the next tributary costs, as a multiple of its cost (each one standing makes the next dearer).</summary>
    public float TributaryCostScale => Map == null ? 1f : WorldTributaries.CostScale(Map, Rules.tributaries);

    public string TributaryCostText => CostText(Rules.tributaries.cost, TributaryCostScale);

    /// <summary>The hubs a tributary at <paramref name="tile"/> could serve, nearest first.</summary>
    public List<Settlement> TributaryHubs(WorldTile tile) => Map == null || tile == null ? new List<Settlement>() : WorldTributaries.Hubs(Map, Rules.tributaries, tile.coord);

    /// <summary>Cells where a tributary could be raised now for some hub, costs aside (the Desirability lens draws them bright).</summary>
    public HashSet<int> TributarySites()
    {
        var cells = new HashSet<int>();
        if (!TributariesUnlocked) return cells;
        foreach (var t in Map.Tiles)
        {
            if (t.authorityId != WorldAuthority.Player || t.water || t.settlement >= 0) continue;
            if (TributaryHubs(t).Any(h => WorldTributaries.WhyNotFound(Map, Settings.generation, Rules.tributaries, t.coord, h) == null)) cells.Add(t.index);
        }
        return cells;
    }

    /// <summary>Why no tributary of <paramref name="hub"/> can be raised at <paramref name="tile"/> now, or null.</summary>
    public string WhyNotRaiseTributary(WorldTile tile, Settlement hub)
    {
        if (Map == null) return "There is no world.";
        if (!TributariesUnlocked) return $"Research {Rules.tributaries.technology} to raise outskirt tributaries.";
        if (tile == null) return "Nowhere to raise it.";
        return WorldTributaries.WhyNotFound(Map, Settings.generation, Rules.tributaries, tile.coord, hub) ?? Unaffordable(Rules.tributaries.cost, TributaryCostScale);
    }

    /// <summary>
    /// Raise an outskirt tributary of <paramref name="hub"/> on held land: a generalist hamlet joined to its hub by road,
    /// a seat of territorial pull and a Trade Node, to be upgraded to a district later.
    /// </summary>
    public Settlement RaiseTributary(WorldTile tile, Settlement hub)
    {
        string why = WhyNotRaiseTributary(tile, hub);
        if (why != null) { Say(why); return null; }
        Pay(Rules.tributaries.cost, TributaryCostScale);
        var s = WorldTributaries.Found(Map, Settings.generation, Rules, tile.coord, hub, AgeNumber);
        // The culture names its new hamlet in its own voice (once it is founded).
        CultureSystem.NameSettlement(s, null, null);
        CulturalTributaryFounding(s, hub);
        GameLog.Event($"Raised {s.name} at {tile.coord} for {hub.name}", Log);
        EraOnce("tributary:" + s.id, Rules.tributaries.eraTributary, $"Raised {s.name}");
        float desire = WorldDesirability.Of(Map, tile);
        AfterCivilizationChange($"{s.name} rises on the outskirts of {hub.name}, joined to it by road: {WorldDesirability.Word(desire)} ground ({desire:P0}), it develops toward {WorldTributaries.Target(Map, Rules.tributaries, s):0}." + (Reclaim(s) is string back ? " " + back : string.Empty));
        return s;
    }

    /// <summary>What turning <paramref name="s"/> into <paramref name="district"/> costs now.</summary>
    public string SpecializeCostText(Settlement s, string district)
    {
        var spec = Rules.tributaries.District(district);
        float scale = WorldTributaries.SpecializeCostScale(Rules.tributaries, s, district);
        return spec == null || scale <= 0f ? "free" : CostText(spec.cost, scale);
    }

    /// <summary>Why <paramref name="s"/> cannot become <paramref name="district"/> now, or null.</summary>
    public string WhyNotSpecialize(Settlement s, string district)
    {
        if (Map == null) return "There is no world.";
        if (!TributariesUnlocked) return $"Research {Rules.tributaries.technology} first.";
        var spec = Rules.tributaries.District(district);
        return WorldTributaries.WhyNotSpecialize(Rules.tributaries, s, district) ?? Unaffordable(spec?.cost, WorldTributaries.SpecializeCostScale(Rules.tributaries, s, district));
    }

    /// <summary>Upgrade a tributary to a district (or turn it back into the generalist).</summary>
    public bool Specialize(Settlement s, string district)
    {
        string why = WhyNotSpecialize(s, district);
        if (why != null) { Say(why); return false; }
        var spec = Rules.tributaries.District(district);
        Pay(spec.cost, WorldTributaries.SpecializeCostScale(Rules.tributaries, s, district));
        string was = s.name;
        WorldTributaries.Specialize(Map, Settings.generation, Rules.tributaries, s, district);
        if (!WorldTributaries.IsGeneralist(Rules.tributaries, s)) EraOnce("district:" + spec.id, Rules.tributaries.eraDistrict, $"The first {spec.name}");
        float adjacency = WorldTributaries.AdjacencyTotal(Map, Rules.tributaries, s);
        // The culture names the district it became (the generalist keeps the name it had).
        string called = WorldTributaries.IsGeneralist(Rules.tributaries, s) ? null
            : CultureSystem.NameSettlement(s, spec.id, CultureRules.TryFamily(spec.enclave, out var family) ? family : (EnclaveFamily?)null);
        AfterCivilizationChange($"{was} becomes {WorldTributaries.Article(spec.name)} ({spec.role}), adjacency {adjacency:+0%;-0%;0%}." + (called != null ? $" {char.ToUpperInvariant(CultureSystem.PeopleWord[0])}{CultureSystem.PeopleWord.Substring(1)} call it {called}." : string.Empty));
        return true;
    }

    /// <summary>Why no road can be built from <paramref name="s"/> now, or null; <paramref name="cells"/> is its new road.</summary>
    public string WhyNotRoad(Settlement s, out List<int> path, out int target, out int cells)
    {
        path = null;
        target = -1;
        cells = 0;
        string locked = Locked();
        if (locked != null) return locked;
        if (s == null) return "No settlement here.";
        if (WorldCivilization.Networked(Map).Contains(s.id)) return "Already joined to the Capital by road.";
        if (!WorldCivilization.PlanRoad(Map, Settings.generation, s, out path, out target)) return "No way over land joins it to your roads.";
        cells = WorldCivilization.NewRoadCells(Map, path);
        // Old World road on the way is restored at a share of the cost.
        return Unaffordable(Rules.roadCostPerCell, WorldRuins.RoadCostScale(Map, LossRules?.oldWorld, path));
    }

    public bool BuildRoad(Settlement s)
    {
        string why = WhyNotRoad(s, out var path, out int target, out int cells);
        if (why != null) { Say(why); return false; }
        Pay(Rules.roadCostPerCell, WorldRuins.RoadCostScale(Map, LossRules?.oldWorld, path));
        var route = WorldCivilization.BuildRoad(Map, Settings.generation, Rules, s, path, target);
        int restored = WorldRuins.RestoredCount(route);
        AfterCivilizationChange($"A road joins {s.name} to {WorldCivilization.Get(Map, target)?.name} ({route.cells.Count} cells, {route.nodes.Count} Trade Nodes, efficiency {route.efficiency:P0})" + (restored > 0 ? $"; {restored} cells restore an Old World road, a lesser road until rebuilt." : "."));
        return true;
    }

    public string WhyNotAnchor(Settlement s) => Locked() ?? WorldCivilization.WhyNotAnchor(s) ?? Unaffordable(Rules.anchorCost);

    public bool BuildAnchor(Settlement s)
    {
        string why = WhyNotAnchor(s);
        if (why != null) { Say(why); return false; }
        Pay(Rules.anchorCost);
        s.anchor = true;
        WorldCivilization.SyncAnchors(Map, Rules, reroute: true);
        AfterCivilizationChange($"A Resonance Anchor rises at {s.name}: the local leylines bend toward it and Coherence steadies.");
        return true;
    }

    public string WhyNotEnvoy(Enclave e) => Locked() ?? WorldCivilization.WhyNotEnvoy(Map, e) ?? Unaffordable(Rules.envoyCost);

    public bool SendEnvoy(Enclave e)
    {
        string why = WhyNotEnvoy(e);
        if (why != null) { Say(why); return false; }
        Pay(Rules.envoyCost);
        // It answers the enclave's grievances first; what is left of its goodwill raises your standing.
        float grievances = GrievanceOf(e.AuthorityId);
        float goodwill = AnswerGrievances(e, Rules.envoyInfluence);
        e.influence = Math.Min(100f, e.influence + Math.Max(0f, goodwill));
        if (e.influence >= 100f) e.suzerain = true;
        if (e.suzerain) EraOnce("suzerain:" + e.index, Rules.eraSuzerainty, $"Suzerainty over {e.name}");
        string answered = grievances > 0f ? $" It answers their grievances ({grievances:0} -> {GrievanceOf(e.AuthorityId):0})." : string.Empty;
        AfterCivilizationChange(e.suzerain ? $"{e.name} accepts your Suzerainty, keeping its autonomy.{answered}{KeepersLine(e)}" : $"Your envoy reaches {e.name} (standing {e.influence:0}/100).{answered}");
        return true;
    }

    // ===== CLAIMING LAND =====

    /// <summary>What claiming one more cell costs now (it grows with every cell claimed).</summary>
    public float ClaimScale => WorldAuthority.ClaimScale(Map != null ? Map.Claims.Count + Map.Claiming.Count : 0, Rules.claimCostGrowth);

    /// <summary>Food value the next claim takes from the stores.</summary>
    public float ClaimFoodValue => Rules.claimFoodValue * ClaimScale;

    public string ClaimCostText
    {
        get
        {
            string food = ClaimFoodValue > 0f ? $"{ClaimFoodValue:0.#} stored food value" : null;
            string other = Rules.claimCost.Any(c => c != null && c.amount > 0f) ? CostText(Rules.claimCost, ClaimScale) : null;
            return string.Join(", ", new[] { food, other }.Where(s => s != null).DefaultIfEmpty("free"));
        }
    }

    /// <summary>
    /// Known wilderness bordering your authority that could be claimed right now: the land allows it and the stores and
    /// the cost can be paid (every claim costs the same now, so one check covers them all). 0 when none can.
    /// </summary>
    public int ClaimableCount() => MapUnlocked && CanPayForClaim() ? WorldAuthority.Claimable(Map).Count() : 0;

    // Whether the next claim could be paid for now (and claiming is open at all).
    private bool CanPayForClaim() => Locked() == null && Pantry.WhyNotAfford(ClaimFoodValue) == null && Unaffordable(Rules.claimCost, ClaimScale) == null;

    /// <summary>Why <paramref name="tile"/> cannot be claimed now, or null.</summary>
    public string WhyNotClaim(WorldTile tile) => Locked() ?? WorldAuthority.WhyNotClaim(Map, tile) ?? Pantry.WhyNotAfford(ClaimFoodValue) ?? Unaffordable(Rules.claimCost, ClaimScale);

    /// <summary>
    /// Claim a known wilderness cell bordering your authority, paid in stored food (any kind, the most perishable
    /// first): it joins your authority at once, every hex of it (<see cref="WorldTerritory.ClaimNow"/>).
    /// </summary>
    public bool Claim(WorldTile tile)
    {
        string why = WhyNotClaim(tile);
        if (why != null) { Say(why); return false; }
        if (!Pantry.TrySpend(ClaimFoodValue)) { Say(Pantry.WhyNotAfford(ClaimFoodValue)); return false; }
        Pay(Rules.claimCost, ClaimScale);
        WorldTerritory.ClaimNow(Map, Settings.generation, tile.index);
        _realmVersion = -1;
        Map.CivilizationVersion++;
        AfterCivilizationChange($"{Place(tile)} is claimed: all of it answers to your authority now.");
        return true;
    }

    // ===== TERRITORY: PULL, ADOPTION AND ADMINISTRATIVE CAPACITY =====

    /// <summary>The realm's administration (capacity, load, strain, efficiency, which way of growing pays), refreshed at most once a second.</summary>
    public RealmReport Realm
    {
        get
        {
            if (Map == null) return new RealmReport();
            if (_realm == null || _realmVersion != Map.CivilizationVersion || Time.unscaledTime >= _realmAt) ComputeRealm();
            return _realm;
        }
    }

    private RealmReport ComputeRealm()
    {
        if (Map == null) return _realm = new RealmReport();
        _realm = WorldTerritory.Realm(Map, Settings.generation, AdministrationContext());
        _realmVersion = Map.CivilizationVersion;
        _realmAt = Time.unscaledTime + 1f;
        return _realm;
    }

    /// <summary>How far society adopts land by itself (saved; the player sets it in the world view's Realm panel).</summary>
    public BorderPolicy BorderPolicy => _borderPolicy;

    public void SetBorderPolicy(BorderPolicy policy)
    {
        if (_borderPolicy == policy) return;
        _borderPolicy = policy;
        _adoptionProgress.Clear();
        _realmVersion = -1;
        _forecast = null;
        Say($"Border policy: {RealmReport.Name(policy)}. {RealmReport.Describe(policy)}");
        Changed?.Invoke();
    }

    /// <summary>
    /// What raises Administrative Capacity beyond the map: Government Capacity, researched technologies and Capital
    /// buildings listed in <see cref="TerritoryRules"/>, and the legend answering for its council area.
    /// </summary>
    public RealmContext AdministrationContext()
    {
        var territory = Rules.territory;
        var context = new RealmContext { governmentCapacity = GovernmentCapacity, policy = _borderPolicy, population = PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.population : 0 };
        foreach (var tech in territory.technologies)
            if (tech != null && !string.IsNullOrEmpty(tech.id) && Researched(tech.id)) context.extra.Add((tech.id, tech.capacity));
        var units = GameUnitsLogic.Instance;
        if (units != null)
            foreach (var building in territory.buildings)
            {
                if (building == null || string.IsNullOrEmpty(building.id)) continue;
                int count = Mathf.FloorToInt(units.GetProductionUnitCount(building.id));
                if (count > 0) context.extra.Add(($"{building.id} x{count}", building.capacity * count));
            }
        var government = GovernmentLogic.Instance;
        if (government != null && !string.IsNullOrEmpty(territory.councilArea) && territory.councilCapacity != 0f)
        {
            var answer = government.AnswerFor(territory.councilArea);
            if (answer.Found) context.extra.Add(($"{answer.legend} ({answer.seat})", answer.distance == 0 ? territory.councilCapacity : territory.councilCapacity * 0.5f));
        }
        return context;
    }

    /// <summary>Cells society will adopt next, best first (for the Realm panel and the Territory lens).</summary>
    public List<WorldTerritory.Candidate> NextAdoptions(int count) =>
        Map == null || !MapUnlocked || !IsOpen || _borderPolicy == BorderPolicy.Hold ? new List<WorldTerritory.Candidate>() : WorldTerritory.Candidates(Map, Settings.generation, count);

    /// <summary>
    /// The hexes society will settle next by itself and about when (<see cref="WorldTerritory.Forecast"/>), for the
    /// map's settling plan; refreshed when the land changes and at most once a second (its timings count down).
    /// </summary>
    public List<WorldTerritory.AdoptionPlan> AdoptionForecast()
    {
        if (Map == null || !MapUnlocked || !IsOpen || _borderPolicy == BorderPolicy.Hold) return _noForecast;
        if (_forecast != null && _forecastVersion == Map.CivilizationVersion && Time.unscaledTime < _forecastAt) return _forecast;
        float elapsed = TimeSystemLogic.Instance != null ? TimeSystemLogic.Instance.GetSeventhProgress() : 0f;
        _forecast = WorldTerritory.Forecast(Map, Settings.generation, AdministrationContext(), _adoptionProgress, elapsed);
        _forecastVersion = Map.CivilizationVersion;
        _forecastAt = Time.unscaledTime + 1f;
        return _forecast;
    }

    private static readonly List<WorldTerritory.AdoptionPlan> _noForecast = new List<WorldTerritory.AdoptionPlan>();
    private List<WorldTerritory.AdoptionPlan> _forecast;
    private int _forecastVersion = -1;
    private float _forecastAt;

    // A Seventh of territorial life: claims an older save left half settled are finished; society settles land hex by
    // hex by the seats' pull while the administration and the Capital's citizens allow (a roll per seat, from the world's seed); past
    // collapse the weakest-held land slips away. Starts with the map (people need somewhere known to go).
    private void TerritoryTick(float sevenths)
    {
        if (Map == null || !MapUnlocked || !IsOpen) return;
        var gen = Settings.generation;
        var claims = WorldTerritory.AdvanceClaims(Map, gen);
        int stream = WorldNoise.Stream(Map.seed, "territory-adoption");
        var result = WorldTerritory.Tick(Map, gen, AdministrationContext(), sevenths, _adoptionProgress, ref _driftProgress,
            () => WorldNoise.Hash01(stream, _adoptionRolls++, 0));
        if (!claims.Any && !result.Any) return;
        _realmVersion = -1;
        // A single hex settled changes nothing but the map's look.
        Map.CivilizationVersion++;
        if (claims.claimed.Count > 0)
            Say($"{string.Join(", ", claims.claimed.Select(c => Place(Map[c])))} {(claims.claimed.Count == 1 ? "is" : "are")} claimed: all of it answers to your authority now.");
        if (result.adopted.Count > 0)
        {
            var names = result.adopted.Select(c => Map[c]).Select(t => $"{Place(t)}{(Map.territory?.Seat(t.pullSeat) is TerritorySeat s ? $" (drawn by {s.name})" : string.Empty)}");
            Say($"Your society settles new land: {string.Join(", ", names)}.");
        }
        if (result.lost.Count > 0)
            Say($"Your administration cannot hold {string.Join(", ", result.lost.Select(c => Place(Map[c])))}: it slips back into the wilderness. Develop and connect what you hold.");
        if (claims.Changed || result.Changed) RecomputeYields();
        Changed?.Invoke();
    }

    private void AfterCivilizationChange(string message)
    {
        RecomputeYields();
        message = string.Join(" ", new[] { message, RehomedNotice() }.Where(m => !string.IsNullOrEmpty(m)));
        if (!string.IsNullOrEmpty(message)) Say(message);
        Changed?.Invoke();
    }

    // The tributaries that rejoined another hub since the last notice, in words (null when none did).
    private string RehomedNotice()
    {
        if (Map?.rehomed == null || Map.rehomed.Count == 0) return null;
        var lines = Map.rehomed.Select(r => (s: WorldCivilization.Get(Map, r.tributary), hub: WorldCivilization.Get(Map, r.hub)))
            .Where(r => r.s != null && r.hub != null).Select(r => $"{r.s.name} lost its hub and now serves {r.hub.name}.").ToList();
        Map.rehomed.Clear();
        return lines.Count > 0 ? string.Join(" ", lines) : null;
    }

    /// <summary>After a save is restored: hand the saved lists back to the map and re-derive what follows from them.</summary>
    public void AfterRestore()
    {
        if (Map == null) return;
        Map.Settlements = _settlements ?? new List<Settlement>();
        Map.Routes = _routes ?? new List<TradeRoute>();
        Map.Enclaves = _enclaves ?? new List<Enclave>();
        Map.Units = _units ?? new List<WorldUnit>();
        Map.Claims = _claims ?? new List<int>();
        Map.Adopted = _adopted = _adopted ?? new List<int>();
        Map.Claiming = _claiming = _claiming ?? new List<int>();
        Map.Plantings = _plantings = _plantings ?? new List<Planting>();
        Map.Ruins = _ruins = _ruins ?? new List<Ruin>();
        Map.Populations = _populations = _populations ?? new List<Population>();
        Map.Behaviors = _behaviors = _behaviors ?? new List<SpeciesBehavior>();
        Map.Outbreaks = _outbreaks = _outbreaks ?? new List<Outbreak>();
        // The land's suffering, cell by cell (a save made before it existed holds none).
        WorldSuffering.Apply(Map, _suffering = _suffering ?? new List<SufferingScar>());
        SyncCalendar();
        // The sites stand where they were placed (placing them again now would read today's magic, settlements and
        // enclaves, and could move a site already identified).
        if (_resourceSites != null) Map.ResourceSites = _resourceSites;
        else _resourceSites = Map.ResourceSites;
        WorldResources.Mark(Map);
        _grievances = _grievances ?? new Dictionary<string, float>();
        _adoptionProgress = _adoptionProgress ?? new Dictionary<string, float>();
        Map.territoryRules = Rules.territory;
        Map.settlementRules = Rules;
        _realmVersion = -1;
        _granted = _granted ?? new List<string>();
        _expeditionLines = _expeditionLines ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _eraAwarded = _eraAwarded ?? new HashSet<string>();
        _partySpecs.Clear();
        foreach (var unit in Map.Units)
        {
            unit.companions = unit.companions ?? new List<string>();
            unit.cargo = unit.cargo ?? new List<ResourceAmount>();
            unit.seeds = unit.seeds ?? new List<string>();
            // Seeds of what has since turned out sterile (Glimmerfern) cannot be planted: they are dropped.
            unit.seeds.RemoveAll(id => Settings.generation.ResourceSite(id)?.seeds != true);
        }
        var gen = Settings.generation;
        for (int age = 1; age <= AgeNumber; age++)
        {
            WorldSites.PlaceGrandfields(Map, gen, age);
            WorldSites.PlaceThreats(Map, gen, age);
            WorldResources.PlaceAge(Map, gen, age);
        }
        WorldCivilization.Rebuild(Map, gen);
        WorldCivilization.SyncAnchors(Map, Rules, reroute: true);
        // Districts that keep watch ward the danger around them again.
        WorldSites.RecomputeDanger(Map);
        RecomputeYields();
        Changed?.Invoke();
    }
}
