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
        Map.territoryRules = Rules.territory;
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
            _subscribed = true;
        });
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.AgeBegan += OnAgeBegan;
            if (ages.Current != null) OnAgeBegan(ages.Current);
        }
        GameTechnologySlot.Researched += OnResearched;
        RecomputeYields();
    }

    protected override void OnSingletonDestroy()
    {
        if (_subscribed && TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventh;
        if (AgeProgression.Instance != null) AgeProgression.Instance.AgeBegan -= OnAgeBegan;
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
        ? Map.Settlements.Count(s => s.kind != SettlementKind.Capital)
        : Map.Settlements.Count(s => string.Equals(s.kind.ToString(), kind, StringComparison.OrdinalIgnoreCase));

    // ===== UNITS =====

    /// <summary>The unit a map unit walks as: its spec, or for an expedition the spec for its party (<see cref="Expeditions.Effective"/>).</summary>
    public UnitSpec SpecOf(WorldUnit unit)
    {
        if (unit == null) return null;
        var spec = Settings.Unit(unit.spec);
        if (spec == null || spec.role != UnitRole.Expedition) return spec;
        string binding = DirectorBinding(unit);
        bool improve = CanImprove;
        string key = $"{string.Join("|", Expeditions.Members(unit))}#{unit.settlers}#{binding}#{improve}#{unit.retreating}";
        if (_partySpecs.TryGetValue(unit.id, out var cached) && cached.key == key && cached.from == spec) return cached.spec;
        var party = Expeditions.Effective(spec, unit, binding, ExpeditionRules, improve);
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
        if (unit == null) return "No unit selected.";
        if (unit.Missing) return "No one knows where it is.";
        if (Map.Get(target) == null) return "Beyond the edge of the world.";
        if (!MicroNavigation.ToMeso(Map, Settings.generation, WorldUnits.MicroPosition(unit), target, out path, out fatigue, out reached, MicroNavigation.MaxPlan, maxVisits))
            return NoWay;
        return path.Count == 0 ? "It is already there." : null;
    }

    public string WhyNotGo(WorldUnit unit, HexCoord target, out List<int> path, out float fatigue) => WhyNotGo(unit, target, out path, out fatigue, out _);

    /// <summary>Why a unit cannot go somewhere when no ground joins it there (or none was found within a bounded search).</summary>
    public const string NoWay = "No way over land leads there.";

    /// <summary>Whether ground joins the unit to <paramref name="target"/> (a micro hex, or a meso cell's heart) or near it, without searching the way.</summary>
    public bool CanReach(WorldUnit unit, HexCoord target, bool micro) =>
        unit != null && Map != null && MicroNavigation.Reachable(Map, Settings.generation, WorldUnits.MicroPosition(unit), micro ? target : MicroNavigation.Center(target));

    /// <summary>Why <paramref name="unit"/> cannot go to the micro hex <paramref name="target"/> (or the nearest one it can reach), or null.</summary>
    public string WhyNotGoMicro(WorldUnit unit, HexCoord target, out List<int> path, out float fatigue, out HexCoord reached, int maxVisits = int.MaxValue)
    {
        path = null;
        fatigue = 0f;
        reached = target;
        if (unit == null) return "No unit selected.";
        if (unit.Missing) return "No one knows where it is.";
        if (MicroNavigation.Parent(Map, target) == null) return "Beyond the edge of the world.";
        if (!MicroNavigation.ToNearest(Map, Settings.generation, WorldUnits.MicroPosition(unit), target, out path, out fatigue, out reached, MicroNavigation.MaxPlan, maxVisits))
            return NoWay;
        return path.Count == 0 ? "It is already here." : null;
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
        WorldUnits.Order(Map, unit, path);
        unit.autoExplore = false;
        unit.returning = false;
        unit.retreating = false;
        var ability = UnitAbilities.For(then);
        unit.onArrival = ability != null && WorldUnits.Can(SpecOf(unit), ability.ability) ? then : UnitTask.None;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Go to the meso cell <paramref name="target"/> and survey it on arrival: the whole hex if the unit can sweep one, else around where it arrives.</summary>
    public bool GoAndSurvey(WorldUnit unit, HexCoord target)
    {
        var spec = SpecOf(unit);
        if (!WorldUnits.Can(spec, UnitAbility.Survey) && !WorldUnits.Can(spec, UnitAbility.SurveyMeso))
        {
            Say($"{unit?.name} cannot survey.");
            return false;
        }
        return Go(unit, target, WorldUnits.Can(spec, UnitAbility.SurveyMeso) ? UnitTask.SurveyMeso : UnitTask.Survey);
    }

    /// <summary>Go to the micro hex <paramref name="target"/> and survey around it on arrival.</summary>
    public bool GoAndSurveyMicro(WorldUnit unit, HexCoord target)
    {
        var spec = SpecOf(unit);
        if (!WorldUnits.Can(spec, UnitAbility.Survey) && !WorldUnits.Can(spec, UnitAbility.SurveyMeso))
        {
            Say($"{unit?.name} cannot survey.");
            return false;
        }
        return GoMicro(unit, target, WorldUnits.Can(spec, UnitAbility.Survey) ? UnitTask.Survey : UnitTask.SurveyMeso);
    }

    public void Halt(WorldUnit unit)
    {
        if (unit == null) return;
        WorldUnits.Stop(unit);
        unit.autoExplore = false;
        unit.returning = false;
        unit.retreating = false;
        unit.onArrival = UnitTask.None;
        if (!unit.Camping) unit.task = UnitTask.None;
        unit.workLeft = 0f;
        Changed?.Invoke();
    }

    /// <summary>Make camp where the unit stands: it rests, eats less, gathers what the land gives and takes on rations inside your authority.</summary>
    public void MakeCamp(WorldUnit unit)
    {
        if (unit == null || (unit.Camping && !unit.resting)) return;
        unit.autoExplore = false;
        unit.returning = false;
        WorldUnits.Camp(unit);
        Changed?.Invoke();
    }

    /// <summary>Break camp: a unit that kept its road walks on.</summary>
    public void BreakCamp(WorldUnit unit)
    {
        if (unit == null || !unit.Camping) return;
        WorldUnits.BreakCamp(unit);
        Changed?.Invoke();
    }

    /// <summary>Why the unit cannot walk back to a settlement for rations now, or null (with the way and its fatigue).</summary>
    public string WhyNotReturn(WorldUnit unit, out List<int> path, out float fatigue)
    {
        path = null;
        fatigue = 0f;
        if (unit == null) return "No unit selected.";
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
        WorldUnits.Order(Map, unit, path);
        unit.autoExplore = false;
        unit.returning = true;
        unit.retreating = false;
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
            return Expeditions.Slots(x, GovernmentCapacity, buildings);
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
        resting = expedition.Camping && at.settlement;
        bool director = string.Equals(expedition.leader, legend, StringComparison.OrdinalIgnoreCase);
        return Expeditions.Hardship(expedition, at, director, DirectorBinding(expedition), ExpeditionRules);
    }

    /// <summary>Legends that could join an expedition now (met, not lost, with no expedition), seated ones last, then by name.</summary>
    public List<string> Candidates()
    {
        var legends = LegendProgress.Instance;
        if (legends == null || Map == null) return new List<string>();
        var government = GovernmentLogic.Instance;
        return legends.RecruitedNames.Where(n => ExpeditionOf(n) == null)
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
        if (unit == null || !WorldUnits.Can(SpecOf(unit), UnitAbility.AutoExplore)) return;
        unit.autoExplore = on;
        if (!on) unit.returning = false;
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
        if (unit.fatigue >= Math.Min(Provisions.exhaustion, 80f))
        {
            WorldUnits.Camp(unit, resting: true);
            return;
        }
        if (spec.supplyUsePerSeventh > 0f && MightNeedRations(unit, spec))
        {
            bool home = WorldUnits.WayToResupply(Map, gen, unit, out var back, out float backFatigue);
            float needed = WorldUnits.RationsFor(unit, spec, backFatigue) * 1.25f + spec.supplyUsePerSeventh;
            if (home && back.Count > 0 && unit.supplies <= needed)
            {
                WorldUnits.Order(Map, unit, back);
                unit.returning = true;
                return;
            }
            if (unit.supplies < spec.supplyCapacity * 0.25f && (!home || back.Count == 0))
            {
                // Nothing will feed it here: better to wait for orders than to starve on.
                unit.autoExplore = false;
                UnitSays(unit, $"{unit.name} stops exploring", home
                    ? $"{unit.name} cannot take on rations in {Place(Map.Get(unit.coord))}: the stores are empty. It waits for orders."
                    : $"{unit.name} is short of rations and no settlement can be reached from {Place(Map.Get(unit.coord))}. It waits for orders: make camp on fertile ground, or send it on.");
                return;
            }
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
        if (Map.Units.Count == 0) return;
        bool changed = false;
        var notice = new List<string>();
        var gen = Settings.generation;
        var rules = Provisions;
        foreach (var unit in Map.Units.ToList())
        {
            // A legend gone to ground is off the map until it turns up (TickMissing, once a Seventh).
            if (unit.Missing) continue;
            var spec = SpecOf(unit);
            if (spec == null) continue;
            var activity =unit.Camping ? UnitActivity.Camping : unit.Working ? UnitActivity.Working : unit.Moving ? UnitActivity.Moving : UnitActivity.Idle;
            float toll = unit.Working ? UnitAbilities.For(unit.task)?.fatigue ?? 1f : 1f;
            var report = WorldUnits.Needs(unit, spec, rules, UnitSurroundings.Of(Map, unit), activity, sevenths, DrawRations, toll);
            if (Condition(unit, spec))
            {
                changed = true;
                continue;
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
                AutoExplore(unit, spec);
                if (!unit.Moving)
                {
                    if (unit.Camping) changed = true;
                    continue;
                }
                changed = true;
            }
            var step = WorldUnits.Move(Map, gen, unit, spec, sevenths);
            if (step.entered.Count > 0 || step.blocked) changed = true;
            foreach (int cell in step.entered) Look(unit, spec, notice, MicroNavigation.Coord(Map, cell));
            if (step.blocked) notice.Add($"{unit.name} can go no further.");
            if (step.arrived) Arrive(unit, spec, notice);
            else if (unit.Moving && unit.fatigue >= rules.exhaustion)
            {
                // Too weary to go on: it makes camp, keeps its road and walks on once rested.
                WorldUnits.Camp(unit, resting: true);
                Flag(unit, UnitWarning.Exhausted, true, false, $"{unit.name} is exhausted", $"{unit.name} can walk no further and makes camp at {Place(Map.Get(unit.coord))}; it goes on once rested.");
                changed = true;
            }
        }
        if (notice.Count > 0) Say(string.Join(" ", notice));
        if (changed) Changed?.Invoke();
    }

    // A journey's end: its deeds, a return for rations, and the work it was sent to do there.
    private void Arrive(WorldUnit unit, UnitSpec spec, List<string> notice)
    {
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
        // Explorers arriving by themselves stay quiet; ordered journeys are reported.
        if (!unit.autoExplore) notice.Add($"{UnitLabel(unit)} reached {Place(here)}.");
        if (unit.leader != null && LegendProgress.Instance != null && unit.journeyLength >= JourneySteps)
        {
            // A long road honours every legend who walked it; the Director led it.
            LastLeader = unit.leader;
            foreach (var member in Expeditions.Members(unit).ToList())
                LegendProgress.Instance.Award(member, LegendLore.FragmentTuning.journey, member == unit.leader ? $"Led {unit.name} to {Place(here)}" : $"Walked with {unit.name} to {Place(here)}", againstTheOdds: unit.mishaps > 0);
        }
        var then = UnitAbilities.For(unit.onArrival);
        unit.onArrival = UnitTask.None;
        if (then != null && WhyNotWork(unit, then) == null) StartTask(unit, spec, then);
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
        foreach (var c in HexCoord.Spiral(micro, sight)) Map.Reveal(HexHierarchy.Parent(c), 0);
        // Crags it passes are known too (it has seen the rock up close); water and impassable cells are never walked.
        var found = new List<WorldTile>();
        foreach (var c in MicroNavigation.Area(Map, micro, AbilityReach.Around, spec.knowRadius))
        {
            var cell = MicroNavigation.Parent(Map, c);
            if (cell != null && MicroNavigation.Walkable(cell, Settings.generation) && Map.KnowMicro(c)) found.Add(cell);
        }
        var sighted = found.Where(t => t.HasFeature && !t.explored && t.coord != Map.Capital).Select(t => Settings.generation.Feature(t.feature)).Where(f => f != null).ToList();
        if (sighted.Count > 0) notice.Add($"{unit.name} sighted {string.Join(", ", sighted.Select(f => f.name))}: survey to investigate.");
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
        unit.settlers = 0;
        _partySpecs.Remove(unit.id);
        var legends = LegendProgress.Instance;
        foreach (var member in Expeditions.Members(unit).ToList()) legends?.Award(member, LegendLore.FragmentTuning.founding, $"Founded {s.name}");
        GameLog.Event($"{unit.name} founded {s.name} at {unit.coord}", Log);
        int points = kind == SettlementKind.Town ? Rules.eraTown : kind == SettlementKind.Haven ? Rules.eraHaven : Rules.eraOutpost;
        EraOnce("settlement:" + s.id, points, $"Founded {s.name}");
        if (nexus) EraOnce("nexus:" + tile.index, Rules.eraNexus, $"Settled a Trade Nexus ({WorldSites.NexusName(tile.nexus ?? Map.NeighboursOf(tile).First(n => n.nexus != null).nexus)})");
        AfterCivilizationChange($"{s.name} is founded{(nexus ? " on a Trade Nexus" : string.Empty)}.");
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
        return Work(unit, UnitAbilities.Improve);
    }

    // ===== ABILITIES =====

    /// <summary>Why the unit cannot start <paramref name="ability"/> where it stands now, or null.</summary>
    public string WhyNotWork(WorldUnit unit, AbilityInfo ability)
    {
        if (unit == null) return "No unit selected.";
        if (unit.Missing) return "No one knows where it is.";
        if (ability == null) return "It cannot do that.";
        var spec = SpecOf(unit);
        switch (ability.task)
        {
            case UnitTask.Survey:
            case UnitTask.SurveyMeso: return WorldUnits.WhyNotSurvey(Map, Settings.generation, unit, spec, ability);
            case UnitTask.Forage: return WorldUnits.WhyNotForage(Map, Settings.generation, unit, spec, AgeNumber);
            case UnitTask.Improve: return WhyNotImprove(unit);
            default: return "It cannot do that.";
        }
    }

    /// <summary>Start an ability where the unit stands (it breaks camp to work).</summary>
    public bool Work(WorldUnit unit, AbilityInfo ability)
    {
        string why = WhyNotWork(unit, ability);
        if (why != null) { Say(why); return false; }
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
    public List<ResourceAmount> ForagePreview(WorldUnit unit) => WorldUnits.ForageOf(Settings.generation, Map.Get(unit.coord), SpecOf(unit)?.rewardMultiplier ?? 1f);

    private void StartTask(WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        if (unit.Camping) WorldUnits.BreakCamp(unit);
        unit.task = ability.task;
        unit.workLeft = Math.Max(0.1f, UnitAbilities.Duration(Map, Settings.generation, unit, spec, ability));
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
            case UnitTask.Survey:
            case UnitTask.SurveyMeso: FinishSurvey(unit, spec, UnitAbilities.For(task), notice); return;
            case UnitTask.Forage: FinishForage(unit, spec, tile, notice); return;
            case UnitTask.Improve: FinishImprove(unit, tile, notice); return;
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
        if (explored.Count == 0) return;
        MarkYieldsDirty();
        Map.CivilizationVersion++;
    }

    private void FinishForage(WorldUnit unit, UnitSpec spec, WorldTile tile, List<string> notice)
    {
        var gathered = WorldUnits.ForageOf(Settings.generation, tile, spec.rewardMultiplier);
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

    // The settlement an expedition stands in, when it can outfit one there (not an Outpost).
    private Settlement OutfittingAt(WorldUnit unit)
    {
        var t = unit != null ? Map.Get(unit.coord) : null;
        var s = t != null && t.settlement >= 0 && t.settlement < Map.Settlements.Count ? Map.Settlements[t.settlement] : null;
        return s != null && s.kind != SettlementKind.Outpost ? s : null;
    }

    /// <summary>Why no expedition can form at <paramref name="at"/> around <paramref name="director"/> now, or null.</summary>
    public string WhyNotForm(Settlement at, string director)
    {
        if (Map == null) return "There is no world.";
        if (!MapUnlocked) return $"Research {Settings.mapTechnology} to send expeditions.";
        if (at == null) return "Expeditions form at a settlement.";
        if (at.kind == SettlementKind.Outpost) return "An Outpost cannot outfit an expedition.";
        return WhyNotJoin(director) ?? Expeditions.WhyNotAddLegend(0, FreeExpeditionSlots, ExpeditionRules) ?? Unaffordable(ExpeditionRules.outfitCost);
    }

    /// <summary>
    /// Form an expedition at <paramref name="at"/> directed by <paramref name="director"/> (who leaves the council if
    /// seated), paying its outfit unless <paramref name="free"/>. It takes one slot; companions join it while it stands
    /// in a settlement.
    /// </summary>
    public WorldUnit FormExpedition(Settlement at, string director, bool free = false)
    {
        string why = WhyNotForm(at, director);
        if (why != null && !(free && why == Unaffordable(ExpeditionRules.outfitCost))) { Say(why); return null; }
        var spec = Settings.Unit(ExpeditionRules.unit);
        if (spec == null || spec.role != UnitRole.Expedition)
        {
            GameLog.Warning($"No expedition unit '{ExpeditionRules.unit}' in WorldSettings.units.", Log);
            return null;
        }
        if (!free) Pay(ExpeditionRules.outfitCost);
        LeaveCouncil(director);
        var unit = Deploy(spec, at.coord, leader: director);
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
        return WhyNotJoin(legend) ?? Expeditions.WhyNotAddLegend(Expeditions.PartySize(unit), FreeExpeditionSlots, ExpeditionRules) ?? Unaffordable(ExpeditionRules.outfitCost);
    }

    public bool AddCompanion(WorldUnit unit, string legend)
    {
        string why = WhyNotAddCompanion(unit, legend);
        if (why != null) { Say(why); return false; }
        Pay(ExpeditionRules.outfitCost);
        LeaveCouncil(legend);
        unit.companions.Add(legend);
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
        ReturnSettlers(unit);
        Say($"{unit.name} disbands at {Place(Map.Get(unit.coord))}: {string.Join(", ", Expeditions.Members(unit))} return home.");
        Changed?.Invoke();
        return true;
    }

    private static void ReturnSettlers(WorldUnit unit)
    {
        var pop = PopGrowthLogic.Instance;
        if (unit.settlers <= 0 || pop == null) return;
        pop.population += unit.settlers;
        unit.settlers = 0;
        pop.RefreshHUD();
    }

    /// <summary>Why the expedition cannot take on settlers here now, or null.</summary>
    public string WhyNotTakeSettlers(WorldUnit unit)
    {
        if (!IsExpedition(unit)) return "Only an expedition escorts settlers.";
        if (unit.settlers > 0) return $"It already escorts {unit.settlers} settlers.";
        if (OutfittingAt(unit) == null) return "Settlers join in one of your settlements (not an Outpost).";
        if (!Researched(SettlersTechnology)) return $"Research {SettlersTechnology} first.";
        int count = Math.Max(1, ExpeditionRules.settlers);
        if (PopGrowthLogic.Instance == null || PopGrowthLogic.Instance.population <= count) return $"Needs {count} citizens to set out.";
        return Pantry.WhyNotAfford(ExpeditionRules.settlerFoodValue) ?? Unaffordable(ExpeditionRules.settlerCost);
    }

    /// <summary>What taking on settlers costs, for the card ("5 citizens, 15 Elderwood").</summary>
    public string SettlerCostText
    {
        get
        {
            var x = ExpeditionRules;
            var parts = new List<string> { $"{Math.Max(1, x.settlers)} citizens" };
            if (x.settlerFoodValue > 0f) parts.Add($"{x.settlerFoodValue:0.#} stored food value");
            if (x.settlerCost.Any(c => c != null && c.amount > 0f)) parts.Add(CostText(x.settlerCost));
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
        if (!Pantry.TrySpend(ExpeditionRules.settlerFoodValue)) { Say(Pantry.WhyNotAfford(ExpeditionRules.settlerFoodValue)); return false; }
        Pay(ExpeditionRules.settlerCost);
        int count = Math.Max(1, ExpeditionRules.settlers);
        TakeCitizens(count);
        unit.settlers = count;
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
        Expeditions.Afflict(unit, spec, PartyShapes.WearShare(mishap.outcome, x));
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
        if (mishap.outcome == MishapOutcome.Evaded && WhyNotRetreat(unit) == null) Retreat(unit);
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
            TickMissing();
            RollMishaps();
        }
    }

    // ===== DISCOVERY =====

    /// <summary>The name of a place as the map knows it: its settlement, its feature, else its ground.</summary>
    public string Place(WorldTile tile)
    {
        if (tile == null) return "unknown ground";
        var gen = Settings.generation;
        if (tile.settlement >= 0 && tile.settlement < Map.Settlements.Count) return Map.Settlements[tile.settlement].name;
        var feature = tile.HasFeature ? gen.Feature(tile.feature) : null;
        if (feature != null && (tile.known || (feature.visibleFromAfar && tile.revealed))) return feature.name;
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
        if (!string.IsNullOrEmpty(story))
        {
            // The party that found the story plays it: its Director the protagonist, its companions co-protagonists.
            if (leader != null) LastLeader = leader;
            if (leader != null && by != null && EventSystemLogic.Instance != null)
                EventSystemLogic.Instance.Summon(story, leader, Expeditions.Members(by).Where(m => !string.Equals(m, leader, StringComparison.OrdinalIgnoreCase)));
            var volumes = EventVolumeManager.Instance;
            if (volumes != null && volumes.UnlockStory(story))
            {
                var events = EventSystemLogic.Instance;
                if (events != null) events.TriggerEventCheck();
            }
            else GameLog.Warning($"Feature '{feature.id}': story '{story}' was not found in Resources/Events.", Log);
        }
        GameLog.Event($"Found {feature.id} at {tile.coord}{(leader != null ? $" ({leader})" : string.Empty)}", Log);
        return notice;
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
        float efficiency = ComputeRealm().efficiency;
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
    }

    /// <summary>Resource per second the land and the settlements yield, by resource (for the world view).</summary>
    public Dictionary<string, float> TotalYields()
    {
        var totals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (Map == null) return totals;
        float efficiency = Realm.efficiency;
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
        float scale = (1f + Rules.improvementBonus * t.improvement) * Realm.efficiency;
        float work = Math.Max(0f, 1f + Rules.territory.beautyWork * t.beauty);
        var gen = Settings.generation;
        if (t.authorityId == WorldAuthority.Player)
            foreach (var y in gen.Terrain(t.terrain)?.yields ?? new List<ResourceAmount>()) if (y != null) result[y.resource] = (result.TryGetValue(y.resource, out var a) ? a : 0f) + y.amount * scale * work;
        if (t.HasFeature)
            foreach (var y in gen.Feature(t.feature)?.yields ?? new List<ResourceAmount>()) if (y != null) result[y.resource] = (result.TryGetValue(y.resource, out var a) ? a : 0f) + y.amount * scale;
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
            if (units == null || units.GetResourceAmountExact(cost.resource) < need) return $"Not enough {cost.resource} ({Mathf.CeilToInt(need)} needed).";
        }
        return null;
    }

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
            if (cost != null && !string.IsNullOrEmpty(cost.resource) && cost.amount > 0f) units.ChangeResourceFromName(cost.resource, -cost.amount * scale, false);
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
        return Unaffordable(Rules.roadCostPerCell, cells);
    }

    public bool BuildRoad(Settlement s)
    {
        string why = WhyNotRoad(s, out var path, out int target, out int cells);
        if (why != null) { Say(why); return false; }
        Pay(Rules.roadCostPerCell, cells);
        var route = WorldCivilization.BuildRoad(Map, Settings.generation, Rules, s, path, target);
        AfterCivilizationChange($"A road joins {s.name} to {WorldCivilization.Get(Map, target)?.name} ({route.cells.Count} cells, {route.nodes.Count} Trade Nodes, efficiency {route.efficiency:P0}).");
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
        WorldCivilization.SendEnvoy(e, Rules);
        if (e.suzerain) EraOnce("suzerain:" + e.index, Rules.eraSuzerainty, $"Suzerainty over {e.name}");
        AfterCivilizationChange(e.suzerain ? $"{e.name} accepts your Suzerainty, keeping its autonomy." : $"Your envoy reaches {e.name} (standing {e.influence:0}/100).");
        return true;
    }

    // ===== CLAIMING LAND =====

    /// <summary>What claiming one more cell costs now (it grows with every cell claimed).</summary>
    public float ClaimScale => WorldAuthority.ClaimScale(Map != null ? Map.Claims.Count : 0, Rules.claimCostGrowth);

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

    /// <summary>Known wilderness bordering your authority, ready to be claimed (cost aside).</summary>
    public int ClaimableCount() => MapUnlocked && IsOpen ? WorldAuthority.Claimable(Map).Count() : 0;

    /// <summary>Why <paramref name="tile"/> cannot be claimed now, or null.</summary>
    public string WhyNotClaim(WorldTile tile) => Locked() ?? WorldAuthority.WhyNotClaim(Map, tile) ?? Pantry.WhyNotAfford(ClaimFoodValue) ?? Unaffordable(Rules.claimCost, ClaimScale);

    /// <summary>Bring a known wilderness cell bordering your authority inside it, paid in stored food (any kind, the most perishable first).</summary>
    public bool Claim(WorldTile tile)
    {
        string why = WhyNotClaim(tile);
        if (why != null) { Say(why); return false; }
        if (!Pantry.TrySpend(ClaimFoodValue)) { Say(Pantry.WhyNotAfford(ClaimFoodValue)); return false; }
        Pay(Rules.claimCost, ClaimScale);
        Map.Claims.Add(tile.index);
        WorldCivilization.Rebuild(Map, Settings.generation);
        Map.CivilizationVersion++;
        AfterCivilizationChange($"{Place(tile)} is claimed: it answers to your authority now.");
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
        var context = new RealmContext { governmentCapacity = GovernmentCapacity, policy = _borderPolicy };
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

    // A Seventh of territorial life: society adopts land by the seats' pull while the administration allows, and
    // past collapse the weakest-held land slips away. Starts with the map (people need somewhere known to go).
    private void TerritoryTick(float sevenths)
    {
        if (Map == null || !MapUnlocked || !IsOpen) return;
        var result = WorldTerritory.Tick(Map, Settings.generation, AdministrationContext(), sevenths, _adoptionProgress, ref _driftProgress);
        if (!result.Changed) return;
        _realmVersion = -1;
        if (result.adopted.Count > 0)
        {
            var names = result.adopted.Select(c => Map[c]).Select(t => $"{Place(t)}{(Map.territory?.Seat(t.pullSeat) is TerritorySeat s ? $" (drawn by {s.name})" : string.Empty)}");
            Say($"Your society settles new land: {string.Join(", ", names)}.");
        }
        if (result.lost.Count > 0)
            Say($"Your administration cannot hold {string.Join(", ", result.lost.Select(c => Place(Map[c])))}: it slips back into the wilderness. Develop and connect what you hold.");
        RecomputeYields();
        Changed?.Invoke();
    }

    private void AfterCivilizationChange(string message)
    {
        RecomputeYields();
        if (!string.IsNullOrEmpty(message)) Say(message);
        Changed?.Invoke();
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
        _adoptionProgress = _adoptionProgress ?? new Dictionary<string, float>();
        Map.territoryRules = Rules.territory;
        _realmVersion = -1;
        _granted = _granted ?? new List<string>();
        _expeditionLines = _expeditionLines ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _eraAwarded = _eraAwarded ?? new HashSet<string>();
        _partySpecs.Clear();
        foreach (var unit in Map.Units) unit.companions = unit.companions ?? new List<string>();
        var gen = Settings.generation;
        for (int age = 1; age <= AgeNumber; age++)
        {
            WorldSites.PlaceGrandfields(Map, gen, age);
            WorldSites.PlaceThreats(Map, gen, age);
        }
        WorldCivilization.Rebuild(Map, gen);
        WorldCivilization.SyncAnchors(Map, Rules, reroute: true);
        RecomputeYields();
        Changed?.Invoke();
    }
}
