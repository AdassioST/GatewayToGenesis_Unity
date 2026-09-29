using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a unit is busy with where it stands.</summary>
public enum UnitTask { None, Survey, Forage, Improve, Camp, SurveyMeso, Harvest, Plant, Investigate, Festival }

/// <summary>What a unit is doing this moment, for its needs (<see cref="WorldUnits.Needs"/>).</summary>
public enum UnitActivity { Idle, Moving, Working, Camping }

/// <summary>Warnings a unit has already raised (each is raised once, until the danger passes).</summary>
[Flags]
public enum UnitWarning { None = 0, LowRations = 1, Starving = 2, Worn = 4, Failing = 8, Exhausted = 16 }

/// <summary>A unit on the map (saved with the world): where it stands, where it is going, what it is doing and how it fares.</summary>
[Serializable]
public class WorldUnit
{
    public int id;
    public string spec;
    public string name;
    /// <summary>The meso cell it stands in (settling, foraging, building and who stands together are the cell's).</summary>
    public HexCoord coord;
    /// <summary>The micro hex it stands on (where it walks, sees and surveys from); <see cref="coord"/> is its cell.</summary>
    public HexCoord microCoord;
    public bool microInitialized, provisionsInitialized;
    /// <summary>Rations carried.</summary>
    public float supplies;
    /// <summary>Weariness, 0-100: past 40 it walks and works slower; at the settings' exhaustion it must make camp.</summary>
    public float fatigue;
    /// <summary>Losses, sickness and wear, 0-100: it slows the unit; at 100 the unit is lost.</summary>
    public float attrition;
    /// <summary>It went without rations in its last moment (hungry units falter).</summary>
    public bool hungry;
    /// <summary>Micro hexes still to enter, the next first.</summary>
    public List<HexCoord> path = new List<HexCoord>();
    /// <summary>Travel fatigue already spent toward the next hex.</summary>
    public float progress;
    /// <summary>Micro hexes in the journey it was last ordered on (a long one counts as completed on arrival).</summary>
    public int journeyLength;
    /// <summary>A builder's improvements left.</summary>
    public int charges;
    /// <summary>Sevenths left on the task it is working at here (0: none).</summary>
    public float workLeft;
    /// <summary>The task those Sevenths are for, or <see cref="UnitTask.Camp"/> while camped.</summary>
    public UnitTask task;
    /// <summary>The legend leading it: an expedition's Director (null for workers such as builders).</summary>
    public string leader;
    /// <summary>An expedition's other legends (the Director is <see cref="leader"/>).</summary>
    public List<string> companions = new List<string>();
    /// <summary>Citizens an expedition escorts to found a settlement (0: none).</summary>
    public int settlers;
    /// <summary>Mishap rolls made so far (seeds the next one, so a save replays the same fortune).</summary>
    public int mishapRolls;
    /// <summary>Mishaps suffered on this expedition.</summary>
    public int mishaps;
    /// <summary>While idle it walks to the nearest unknown ground by itself (and back to resupply when rations run short).</summary>
    public bool autoExplore;
    /// <summary>What it starts where it arrives (<see cref="UnitTask.Survey"/> or <see cref="UnitTask.SurveyMeso"/>), or none.</summary>
    public UnitTask onArrival;
    /// <summary>It made camp by itself (exhausted, or to take on rations) and walks on once rested and provisioned.</summary>
    public bool resting;
    /// <summary>It is walking back to a settlement for rations (exploring by itself).</summary>
    public bool returning;
    /// <summary>Warnings already raised (<see cref="UnitWarning"/>).</summary>
    public int warnings;
    /// <summary>An expedition slipping away to safe ground at its retreat pace (<see cref="PartyShape.retreatPace"/>).</summary>
    public bool retreating;
    /// <summary>Sevenths until a legend gone to ground turns up (0: it is on the map). While missing it is not drawn and nothing befalls it.</summary>
    public float missingSevenths;
    /// <summary>The micro hex of your territory where a missing legend turns up.</summary>
    public HexCoord missingTo;
    /// <summary>Cargo and valuables it carries home from harvests (delivered in one of your settlements).</summary>
    [SaveOptionalField] public List<ResourceAmount> cargo = new List<ResourceAmount>();
    /// <summary>Seeds it carries (resource site ids, one entry per planting), planted on fertile land you hold.</summary>
    [SaveOptionalField] public List<string> seeds = new List<string>();
    /// <summary>It was sent to survey <see cref="surveyCell"/>: it walks to each of the cell's hexes in turn and surveys it
    /// there (WorldSystem.SurveyCell). A move within the cell keeps it going; rest, a retreat or a walk back for rations
    /// only interrupt it (it picks the survey up by itself); an order elsewhere pauses it (<see cref="surveyPaused"/>).</summary>
    [SaveOptionalField] public bool surveying;
    [SaveOptionalField] public HexCoord surveyCell;
    /// <summary>A survey of <see cref="surveyCell"/> set aside by another order: its progress is kept, and it resumes when
    /// told to, or by itself once the party is back in that cell.</summary>
    [SaveOptionalField] public bool surveyPaused;
    /// <summary>Work already done on a hex whose survey was interrupted (<see cref="surveyWork"/> Sevenths on
    /// <see cref="surveyWorkHex"/>; 0: none): picked up where it stopped.</summary>
    [SaveOptionalField] public HexCoord surveyWorkHex;
    [SaveOptionalField] public float surveyWork;
    /// <summary>It surveys cell after cell by itself: the nearest known ground still to survey, resting and walking back
    /// for rations as it needs.</summary>
    [SaveOptionalField] public bool autoSurvey;
    /// <summary>Sevenths it waits before trying its survey's next hex again (others stood in the way).</summary>
    [SaveOptionalField] public float surveyWait;
    /// <summary>Sevenths the task it is working at takes in all (its progress is 1 - <see cref="workLeft"/> / this).</summary>
    [SaveOptionalField] public float workTotal;
    /// <summary>What an expedition set out to do (explore, build, celebrate); chosen when it forms.</summary>
    [SaveOptionalField] public ExpeditionCharter charter;
    /// <summary>Whose it is: empty for yours, <see cref="WorldBattles.Wild"/> for wild creatures (other peoples later).</summary>
    [SaveOptionalField] public string faction;
    /// <summary>An army on the map: the stack of conscripted companies it carries (<see cref="ArmyRoster"/>).</summary>
    [SaveOptionalField] public string armyStack;
    /// <summary>A creature band: its species and how many of them.</summary>
    [SaveOptionalField] public string species;
    [SaveOptionalField] public int creatures;
    /// <summary>Sevenths after a battle before it can clash again (both sides draw breath and part).</summary>
    [SaveOptionalField] public float truce;
    /// <summary>An enemy's stance: red (hostile) or orange (wary until provoked).</summary>
    [SaveOptionalField] public EnemyStance stance;
    /// <summary>Sevenths a wary enemy stays hostile after one of your units came too close.</summary>
    [SaveOptionalField] public float provoked;
    /// <summary>The threat that sent it out (<see cref="ThreatSpec"/> id) and the cell of its site, or empty.</summary>
    [SaveOptionalField] public string threat;
    [SaveOptionalField] public int homeCell = -1;
    /// <summary>A threat's beings' primary binding (every Atonalis carries one).</summary>
    [SaveOptionalField] public SpellBinding binding;
    /// <summary>Share of its party's Composure lost in battle and not yet recovered (its Integrity's wounds are its attrition).</summary>
    [SaveOptionalField] public float nerveLost;
    // ----- Encounters (WorldPursuit). A save made before these existed loads them as zero: WorldPursuit.Initialize
    // sets them up once encounterInitialized is false, so no initializer below may be relied on after a load. -----
    [SaveOptionalField] public bool encounterInitialized;
    /// <summary>A band: what it is (its mark on the map), how it thinks, what it is doing, and how it meets you.</summary>
    [SaveOptionalField] public BandIdentity identity;
    [SaveOptionalField] public BandIntelligence intelligence;
    [SaveOptionalField] public BandActivity bandActivity;
    [SaveOptionalField] public ThreatResponse response;
    /// <summary>The unit it is chasing (a band's quarry, or the band a party of yours hunts), or -1.</summary>
    [SaveOptionalField] public int quarryId = -1;
    /// <summary>Where it last saw its quarry, and Sevenths it keeps looking there.</summary>
    [SaveOptionalField] public HexCoord lastSeen;
    [SaveOptionalField] public float memoryLeft, decisionLeft, chaseCooldown;
    /// <summary>Hexes chased so far, and how far it will chase; the territory it defends around home (hexes).</summary>
    [SaveOptionalField] public int chaseTiles;
    [SaveOptionalField] public int pursuitLimit = 12, territoryRadius = 4;
    /// <summary>Running endurance (0-100); winded at 0 until it has caught its breath. Sprinting: a party of yours told to run.</summary>
    [SaveOptionalField] public float endurance = 100f;
    [SaveOptionalField] public bool winded, sprinting;
    /// <summary>Its sprint (x its walk) and how fast running tires it (0: the defaults).</summary>
    [SaveOptionalField] public float sprintPace, wind;
    /// <summary>A den's visitor: Sevenths left before it heads home (-1: on its way home, gone on arrival; 0: not a visitor).</summary>
    [SaveOptionalField] public float denLife;
    /// <summary>Sevenths a predator rests after a kill before it hunts again.</summary>
    [SaveOptionalField] public float sated;
    /// <summary>Where it can go (a band: land, water or both; your parties walk the land).</summary>
    [SaveOptionalField] public CreatureHabitat habitat;
    /// <summary>An Atonalis band's Eight-Born Path (how it hunts, <see cref="AtonalPaths"/>); None for everything else.</summary>
    [SaveOptionalField] public AtonalPath atonalPath;
    /// <summary>Your legends an Atonalis band has taken captive (freed when the band is destroyed).</summary>
    [SaveOptionalField] public List<string> captives = new List<string>();
    /// <summary>Its walking pace (x its kind's; 0: 1). Formless Masses creep.</summary>
    [SaveOptionalField] public float stride;
    /// <summary>A unit it will not forget (a Violux's chosen victim), stored as id + 1 (0: none).</summary>
    [SaveOptionalField] public int grudge;
    /// <summary>A Formless Mass: the feelings it has drained (its emotional meter: they decide the Path it hatches into); an Atonalis keeps what it was born of.</summary>
    [SaveOptionalField] public EmotionalRegister fed = new EmotionalRegister();
    /// <summary>An Atonalis born a hybrid: its second Path (None: pure).</summary>
    [SaveOptionalField] public AtonalPath hybridPath;
    /// <summary>A Formless Mass's cocoon: Sevenths until the Atonalis inside hatches (0: not cocooned).</summary>
    [SaveOptionalField] public float cocoon;
    /// <summary>A band's home hex (where it rose: beside its den, in its den's water, at its nest); unset: its home cell's centre.</summary>
    [SaveOptionalField] public HexCoord homeHex;
    [SaveOptionalField] public bool homeHexSet;

    public bool Moving => path != null && path.Count > 0;
    public bool Missing => missingSevenths > 0f;
    public bool Working => workLeft > 0f;
    public bool Camping => task == UnitTask.Camp;
}

/// <summary>What one movement update did: the micro hexes a unit entered and whether its journey ended.</summary>
public struct UnitStep
{
    /// <summary>Micro ids entered, in order.</summary>
    public List<int> entered;
    public bool arrived;
    public bool blocked;
    /// <summary>It stopped short of a hex another side holds (units of different sides never share a hex).</summary>
    public bool halted;
    /// <summary>Travel fatigue actually walked.</summary>
    public float effort;
}

/// <summary>Where a unit stands, as its needs see it (read from its cell by <see cref="Of"/>).</summary>
public struct UnitSurroundings
{
    /// <summary>Inside your authority (or an Outpost's cell): rations can be drawn from the stores.</summary>
    public bool held;
    /// <summary>In one of your settlements: rations at the full rate, shelter, healers.</summary>
    public bool settlement;
    /// <summary>The local weather's travel multiplier (1 ordinary; above 1 harsh).</summary>
    public float weather;
    public float danger, dissonance;
    /// <summary>Land fertility, 0-1 (what a camp can gather).</summary>
    public float fertility;
    /// <summary>Attrition per Seventh the cover wears on a party (thorns, bog, choking ash; <see cref="WorldCover"/>).</summary>
    public float hardship;
    /// <summary>A predatory Eleos Bloom's lure here (0-1) and the strain a healing one eases per Seventh (<see cref="WorldResources"/>).</summary>
    public float lure, sanctuary;
    /// <summary>Vibrational Density, Vibrational Fallout and a Chaotic Resonant Cascade here, 0-1 (<see cref="WorldVibration"/>).
    /// A cell with no magic laid down reads as neither thin nor dense (<see cref="densityKnown"/> false).</summary>
    public float density, fallout, cascade;
    public bool densityKnown;
    /// <summary>How fair the place is (-1 to 1, <see cref="WorldBeauty"/>) and the solace its ground and sites give
    /// (<see cref="WorldTile.solace"/>); cell steps to a silver river and to a lake one runs into. The steps count only when
    /// <see cref="mapped"/> (read from a real cell).</summary>
    public float beauty, solace;
    public int silverRiverSteps, silverLakeSteps;
    public bool mapped;
    /// <summary>Living resource sites around (0-1) and a Resource Grandfield's density here, for a forager or a camp.</summary>
    public float bounty, grandfield;

    public static UnitSurroundings Of(WorldMap map, WorldUnit unit)
    {
        var t = map?.Get(unit.coord);
        if (t == null) return new UnitSurroundings { weather = 1f, silverRiverSteps = WorldResources.FarFromSilver, silverLakeSteps = WorldResources.FarFromSilver };
        return new UnitSurroundings
        {
            held = WorldAuthority.IsPlayers(t.authorityId) || t.settlement >= 0,
            settlement = t.settlement >= 0,
            weather = t.weatherTravelMultiplier,
            // Standing hazards, and fresh signs of something hunting here (the risk of an ambush is real).
            danger = Math.Max(t.danger, t.signs),
            dissonance = t.dissonance,
            fertility = t.landFertility,
            hardship = t.coverHardship,
            lure = t.lure,
            sanctuary = t.sanctuary,
            density = t.vibrationalDensity,
            densityKnown = map.Magic != null && map.Magic.Age >= 0,
            fallout = t.fallout,
            cascade = t.cascade,
            beauty = t.beauty,
            solace = t.solace,
            silverRiverSteps = t.silverRiverSteps,
            silverLakeSteps = t.silverLakeSteps,
            mapped = true,
            bounty = t.siteBounty,
            grandfield = t.grandfield >= 0 ? t.grandfieldDensity : 0f,
        };
    }

    /// <summary>How far the weather is above the ordinary (0 in fair weather).</summary>
    public float Exposure => Math.Max(0f, weather - 1f);
}

/// <summary>What a unit's needs did over a moment.</summary>
public struct NeedsReport
{
    /// <summary>Rations drawn from the stores and gathered from the land.</summary>
    public float drawn, gathered;
    /// <summary>Sevenths it went without rations.</summary>
    public float hungry;
}

/// <summary>
/// Units on the map, with no scene state (tested in <c>WorldUnitTests</c>).
/// - Travel: a unit spends its <see cref="UnitSpec.stamina"/> in travel fatigue per Seventh, continuously, walking the
///   micro hexes (<see cref="MicroGrid"/>): each hex entered costs its travel fatigue, so time passing, not turns,
///   carries it along its path; pausing time stops it. An order at the meso scale heads for the cell's centre hex (or
///   the nearest one it can reach); at the micro scale for the hex itself.
/// - Needs (<see cref="Needs"/>): it eats rations every Seventh (fewer in camp, more in harsh weather), drawn from your
///   stores inside your authority and gathered from the land in camp; it tires with every step and every task and
///   rests in camp; hunger, exhaustion, harsh weather, danger and dissonance wear it down (attrition), rest while fed
///   heals it. Tired, worn or hungry it walks and works slower (<see cref="Efficiency"/>) and sees less; exhausted it
///   makes camp; worn out entirely it is lost.
/// - Builders improve resource hotspots: each level adds <see cref="SettlementRules.improvementBonus"/> of the
///   hotspot's yields.
/// </summary>
public static class WorldUnits
{
    /// <summary>Fatigue below which a unit is at its best, and the least it can do however it fares.</summary>
    public const float FreshFatigue = 40f, MinEfficiency = 0.15f;
    /// <summary>Fatigue at which marching and working start to wear a unit down.</summary>
    public const float StrainFatigue = 90f;

    public static HexCoord MicroPosition(WorldUnit unit) => unit.microInitialized ? unit.microCoord : MicroNavigation.Center(unit.coord);

    /// <summary>Set a unit on a micro hex (and its cell).</summary>
    public static void Place(WorldUnit unit, HexCoord micro)
    {
        unit.microCoord = micro;
        unit.coord = HexHierarchy.Parent(micro);
        unit.microInitialized = true;
    }

    /// <summary>A new unit stands on its cell's centre hex with full rations.</summary>
    public static void Initialize(WorldUnit unit, UnitSpec spec)
    {
        if (!unit.microInitialized) Place(unit, MicroNavigation.Center(unit.coord));
        if (!unit.provisionsInitialized && spec != null)
        {
            unit.supplies = Math.Max(0f, spec.supplyCapacity);
            unit.provisionsInitialized = true;
        }
    }

    // ===== CONDITION =====

    /// <summary>
    /// How well a unit walks and works now, 0.15-1: weariness past 40 fatigue costs up to half, attrition up to half,
    /// hunger two fifths.
    /// </summary>
    public static float Efficiency(WorldUnit unit)
    {
        float tired = unit.fatigue <= FreshFatigue ? 1f : 1f - 0.5f * Math.Min(1f, (unit.fatigue - FreshFatigue) / (100f - FreshFatigue));
        float worn = 1f - 0.5f * Clamp01(unit.attrition / 100f);
        return Math.Max(MinEfficiency, tired * worn * (unit.hungry ? 0.6f : 1f));
    }

    /// <summary>Sevenths its rations last at the usual pace (+infinity when it needs none).</summary>
    public static float RationSevenths(WorldUnit unit, UnitSpec spec) =>
        spec == null || spec.supplyUsePerSeventh <= 0f ? float.PositiveInfinity : Math.Max(0f, unit.supplies) / spec.supplyUsePerSeventh;

    public static string FatigueWord(float fatigue) => fatigue < 25f ? "fresh" : fatigue < 50f ? "steady" : fatigue < 75f ? "weary" : fatigue < 100f ? "spent" : "exhausted";

    public static string AttritionWord(float attrition) => attrition < 10f ? "hale" : attrition < 35f ? "worn" : attrition < 65f ? "suffering" : "failing";

    /// <summary>
    /// A moment of a unit's life (<paramref name="sevenths"/> long): rations drawn inside your authority
    /// (<paramref name="drawStores"/> takes food value from the stores and returns what it got) and gathered from the
    /// land in camp, then eaten; fatigue shed at rest or gained at work (times <paramref name="workToll"/>, the
    /// ability's; travel fatigue is <see cref="Move"/>'s); and attrition from hunger, strain, weather, danger and
    /// dissonance, healed by rest while fed.
    /// </summary>
    public static NeedsReport Needs(WorldUnit unit, UnitSpec spec, ProvisionRules rules, UnitSurroundings at, UnitActivity activity, float sevenths,
        Func<float, float> drawStores = null, float workToll = 1f)
    {
        var report = new NeedsReport();
        if (spec == null || sevenths <= 0f) return report;
        Initialize(unit, spec);
        rules = rules ?? new ProvisionRules();
        bool camping = activity == UnitActivity.Camping;
        float exposure = at.Exposure;
        float capacity = Math.Max(0f, spec.supplyCapacity);
        float use = Math.Max(0f, spec.supplyUsePerSeventh) * (camping ? Clamp01(rules.campRationShare) : 1f) * (1f + 0.5f * exposure);

        // Rations: from the stores inside your authority, from the land in camp, then eaten.
        if (at.held && drawStores != null && capacity > 0f)
        {
            float room = capacity - unit.supplies + use * sevenths;
            float want = Math.Min(room, Math.Max(0f, rules.resupplyPerSeventh) * (at.settlement ? 1f : 0.5f) * sevenths);
            if (want > 1e-5f)
            {
                float perRation = Math.Max(0f, rules.foodValuePerRation);
                float got = perRation > 0f ? drawStores(want * perRation) / perRation : want;
                report.drawn = Math.Max(0f, Math.Min(want, got));
                unit.supplies += report.drawn;
            }
        }
        if (camping && spec.forageRationsPerSeventh > 0f)
        {
            // Fertile ground feeds a camp already; living sites and a grandfield around feed it more.
            float plenty = 1f + Math.Min(Math.Max(0f, rules.maxForageBonus), SitesBonus(rules, at));
            report.gathered = spec.forageRationsPerSeventh * Clamp01(at.fertility) * plenty * (1f - Clamp01(at.danger)) / (1f + exposure) * sevenths;
            unit.supplies += report.gathered;
        }
        float fed = use > 0f ? Math.Min(sevenths, Math.Max(0f, unit.supplies) / use) : sevenths;
        unit.supplies = Math.Max(0f, Math.Min(Math.Max(capacity, 0f), unit.supplies - use * sevenths));
        report.hungry = sevenths - fed;
        unit.hungry = report.hungry > 1e-6f;

        // Fatigue: rest heals it (camp best, a settlement better still), work adds to it.
        float rest = camping ? 1f : activity == UnitActivity.Idle ? Clamp01(rules.idleRecoveryShare) : 0f;
        unit.fatigue -= Math.Max(0f, spec.campRecoveryPerSeventh) * rest * (at.settlement ? 1.5f : 1f) / (1f + exposure) * sevenths;
        if (activity == UnitActivity.Working) unit.fatigue += Math.Max(0f, spec.workFatiguePerSeventh) * Math.Max(0f, workToll) * (1f + exposure) * sevenths;
        unit.fatigue = Math.Max(0f, Math.Min(100f, unit.fatigue));

        // Attrition: hunger, strain and the land wear it down; rest while fed heals it.
        float wear = report.hungry * Math.Max(0f, spec.starvationAttritionPerSeventh);
        if ((activity == UnitActivity.Moving || activity == UnitActivity.Working) && unit.fatigue >= StrainFatigue) wear += Math.Max(0f, rules.exhaustionAttrition) * sevenths;
        if (!at.settlement)
        {
            wear += exposure * Math.Max(0f, rules.exposureAttrition) * (camping ? 0.5f : 1f) * sevenths;
            wear += Clamp01(at.danger) * Math.Max(0f, rules.dangerAttrition) * sevenths;
            wear += Clamp01(at.dissonance) * Math.Max(0f, rules.dissonanceAttrition) * sevenths;
            wear += Clamp01(at.fallout) * Math.Max(0f, rules.falloutAttrition) * sevenths;
            wear += Math.Max(0f, at.hardship) * sevenths;
        }
        float heal = camping || at.settlement ? Math.Max(0f, spec.recoveryPerSeventh) * (at.settlement ? 3f : 1f) * fed : 0f;
        wear *= Math.Max(0f, spec.wearMultiplier);
        unit.attrition = Math.Max(0f, Math.Min(100f, unit.attrition + wear - heal));
        return report;
    }

    /// <summary>Make camp where it stands: it stops walking and working, rests, eats less and lives off the land.</summary>
    public static void Camp(WorldUnit unit, bool resting = false)
    {
        if (!resting) Stop(unit);
        unit.task = UnitTask.Camp;
        unit.workLeft = 0f;
        unit.resting = resting;
        if (!resting) unit.onArrival = UnitTask.None;
    }

    /// <summary>Break camp (it walks on along any path it kept).</summary>
    public static void BreakCamp(WorldUnit unit)
    {
        if (unit.task == UnitTask.Camp) unit.task = UnitTask.None;
        unit.resting = false;
    }

    // ===== MOVEMENT =====

    /// <summary>
    /// The cheapest way on foot from the unit to the meso cell <paramref name="meso"/>: its centre hex, or the nearest
    /// hex the unit can reach (micro ids after the start) and its travel fatigue.
    /// </summary>
    public static bool Plan(WorldMap map, WorldGenSettings settings, WorldUnit unit, HexCoord meso, out List<int> path, out float fatigue) =>
        MicroNavigation.ToMeso(map, settings, MicroPosition(unit), meso, out path, out fatigue, out _);

    /// <summary>The cheapest way to the micro hex <paramref name="micro"/>, or the nearest one the unit can reach.</summary>
    public static bool PlanMicro(WorldMap map, WorldGenSettings settings, WorldUnit unit, HexCoord micro, out List<int> path, out float fatigue) =>
        MicroNavigation.ToNearest(map, settings, MicroPosition(unit), micro, out path, out fatigue, out _);

    /// <summary>
    /// Set the unit on its way along <paramref name="path"/> (micro ids from the hex it stands on, from <see cref="Plan"/>);
    /// it stops any work and breaks camp. Caught between two hexes, it keeps its place on the ground: if the way goes on
    /// through the hex it was stepping into, the step already walked counts; otherwise it first walks back to the hex it
    /// left, as far as it had come (<see cref="PlanFromStep"/> picks the cheaper of the two). No order moves it in a blink.
    /// </summary>
    public static void Order(WorldMap map, WorldUnit unit, List<int> path)
    {
        Place(unit, MicroPosition(unit));
        var way = path.Select(c => MicroNavigation.Coord(map, c)).ToList();
        float kept = 0f;
        if (map.microGrid != null && StepCosts(map, map.microGrid, unit, out var next, out float ahead, out float behind))
        {
            if (way.Count > 0 && way[0] == next) kept = unit.progress;
            else if (!float.IsPositiveInfinity(behind))
            {
                // Turn about: it stands in the step from the next hex back to the one it left, as far along as it has left to go.
                var left = unit.microCoord;
                way.Insert(0, left);
                Place(unit, next);
                kept = Math.Max(0f, (1f - Math.Min(1f, unit.progress / ahead)) * behind);
            }
        }
        unit.path = way;
        unit.progress = kept;
        unit.workLeft = 0f;
        unit.task = UnitTask.None;
        unit.resting = false;
        unit.onArrival = UnitTask.None;
        unit.journeyLength = unit.path.Count;
    }

    public static void Stop(WorldUnit unit)
    {
        unit.path.Clear();
        unit.progress = 0f;
    }

    // The step a unit is part-way through: the hex it is stepping into and the travel fatigue of that step forward and
    // back (+infinity when it cannot be walked back). False when it is not between two hexes.
    private static bool StepCosts(WorldMap map, MicroGrid grid, WorldUnit unit, out HexCoord next, out float ahead, out float behind)
    {
        next = default;
        ahead = behind = 0f;
        if (unit == null || !unit.Moving || unit.progress <= 1e-4f) return false;
        var here = MicroPosition(unit);
        next = unit.path[0];
        int a = MicroNavigation.Index(map, here), b = MicroNavigation.Index(map, next);
        if (a < 0 || b < 0 || HexCoord.Distance(here, next) != 1) return false;
        ahead = grid.Step(a, b);
        if (float.IsPositiveInfinity(ahead) || float.IsNaN(ahead) || ahead <= 0f) return false;
        behind = grid.Step(b, a);
        if (float.IsNaN(behind)) behind = float.PositiveInfinity;
        return true;
    }

    /// <summary>
    /// A unit caught between two hexes: the hex it was stepping into, and the travel fatigue to turn back to the hex it
    /// left (<paramref name="back"/>: the ground already covered) or walk on into the next (<paramref name="on"/>: what is
    /// left of the step). False when it stands on a hex.
    /// </summary>
    public static bool MidStep(WorldMap map, WorldGenSettings settings, WorldUnit unit, out HexCoord next, out float back, out float on)
    {
        back = on = 0f;
        if (!StepCosts(map, MicroNavigation.Grid(map, settings), unit, out next, out float ahead, out float behind)) return false;
        back = float.IsPositiveInfinity(behind) ? float.PositiveInfinity : Math.Min(1f, unit.progress / ahead) * behind;
        on = Math.Max(0f, ahead - unit.progress);
        return true;
    }

    /// <summary>A way-finder from a micro hex: the way (micro ids after the start), its travel fatigue and where it ends.</summary>
    public delegate bool Planner(HexCoord from, out List<int> path, out float fatigue, out HexCoord reached);

    /// <summary>
    /// Plan a new order for a unit that may be caught between two hexes: from the hex it left (turning back first costs
    /// the ground already covered, so an about-face is dear) and from the hex it was stepping into (walking on costs only
    /// what is left of the step, so a turn to either side is cheap); the cheaper way wins. The way is always given from
    /// the hex it left (walking on puts the next hex first), as <see cref="Order"/> takes it; the fatigue is what is left
    /// to walk. Standing on a hex, it is just <paramref name="plan"/> from there.
    /// </summary>
    public static bool PlanFromStep(WorldMap map, WorldGenSettings settings, WorldUnit unit, Planner plan, out List<int> path, out float fatigue, out HexCoord reached)
    {
        bool ok = plan(MicroPosition(unit), out path, out fatigue, out reached);
        if (!MidStep(map, settings, unit, out var next, out float back, out float on)) return ok;
        int nextId = MicroNavigation.Index(map, next);
        if (ok)
        {
            if (path.Count > 0 && path[0] == nextId) fatigue = Math.Max(0f, fatigue - unit.progress);
            else if (float.IsPositiveInfinity(back)) ok = false;
            else fatigue += back;
        }
        if (plan(next, out var onPath, out float onFatigue, out var onReached) && (!ok || on + onFatigue < fatigue - 1e-4f))
        {
            onPath.Insert(0, nextId);
            path = onPath;
            fatigue = on + onFatigue;
            reached = onReached;
            ok = true;
        }
        return ok;
    }

    /// <summary>
    /// Move the unit through <paramref name="sevenths"/> of time: it spends stamina x sevenths (x its efficiency) of
    /// travel fatigue, entering each micro hex once what it spent covers that step, and tires by what it walked. A hex
    /// that cannot be entered ends the journey, and so does one another side holds (<paramref name="held"/>): the unit
    /// halts beside it.
    /// </summary>
    public static UnitStep Move(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, float sevenths, Func<HexCoord, bool> held = null, Func<int, int, float> stepCost = null)
    {
        var step = new UnitStep { entered = new List<int>() };
        if (!unit.Moving || spec == null || sevenths <= 0f) return step;
        Initialize(unit, spec);
        var grid = MicroNavigation.Grid(map, settings);
        int at = MicroNavigation.Index(map, unit.microCoord);
        if (unit.winded) return step;
        float effort = Math.Max(0f, spec.stamina) * Efficiency(unit) * WorldPursuit.RunPace(unit) * sevenths;
        // A runner goes no further than its endurance carries it this moment (then it is winded).
        if (WorldPursuit.Running(unit)) effort = Math.Min(effort, unit.endurance / (WorldPursuit.RunDrain * (unit.wind > 0f ? unit.wind : 1f)) + 1e-4f);
        unit.progress += effort;
        while (unit.path.Count > 0)
        {
            var next = unit.path[0];
            int id = MicroNavigation.Index(map, next);
            float cost = id < 0 || HexCoord.Distance(unit.microCoord, next) != 1 ? float.PositiveInfinity : stepCost != null ? stepCost(at, id) : grid.Step(at, id);
            if (float.IsPositiveInfinity(cost) || float.IsNaN(cost))
            {
                step.blocked = true;
                effort -= unit.progress;
                Stop(unit);
                break;
            }
            if (unit.progress < cost) break;
            if (held != null && held(next))
            {
                // Units of different sides never share a hex: it halts beside the other.
                step.halted = true;
                effort -= unit.progress;
                Stop(unit);
                break;
            }
            unit.progress -= cost;
            unit.microCoord = next;
            unit.coord = map[id / MicroNavigation.PerCell].coord;
            at = id;
            unit.path.RemoveAt(0);
            step.entered.Add(id);
        }
        if (unit.path.Count == 0 && !step.blocked)
        {
            effort -= unit.progress;
            unit.progress = 0f;
            step.arrived = step.entered.Count > 0;
        }
        step.effort = Math.Max(0f, effort);
        WorldPursuit.Spend(unit, step.effort);
        float exposure = Math.Max(0f, (map.Get(unit.coord)?.weatherTravelMultiplier ?? 1f) - 1f);
        unit.fatigue = Math.Min(100f, unit.fatigue + step.effort * Math.Max(0f, spec.fatiguePerTravelCost) * (1f + exposure));
        return step;
    }

    /// <summary>Travel fatigue left on the unit's journey.</summary>
    public static float FatigueLeft(WorldMap map, WorldGenSettings settings, WorldUnit unit)
    {
        if (!unit.Moving) return 0f;
        var grid = MicroNavigation.Grid(map, settings);
        float total = 0f;
        int at = MicroNavigation.Index(map, MicroPosition(unit));
        foreach (var c in unit.path)
        {
            int id = MicroNavigation.Index(map, c);
            float step = grid.Step(at, id);
            if (float.IsPositiveInfinity(step)) break;
            total += step;
            at = id;
        }
        return Math.Max(0f, total - unit.progress);
    }

    /// <summary>Sevenths a journey of <paramref name="fatigue"/> takes at the unit's present pace.</summary>
    public static float SeventhsFor(WorldUnit unit, UnitSpec spec, float fatigue) =>
        spec == null || spec.stamina <= 0f ? 0f : fatigue / (spec.stamina * Efficiency(unit));

    /// <summary>Sevenths until the unit arrives (0 when standing).</summary>
    public static float SeventhsLeft(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec) => SeventhsFor(unit, spec, FatigueLeft(map, settings, unit));

    /// <summary>Rations a journey of <paramref name="fatigue"/> eats at the usual pace.</summary>
    public static float RationsFor(WorldUnit unit, UnitSpec spec, float fatigue) => spec == null ? 0f : SeventhsFor(unit, spec, fatigue) * Math.Max(0f, spec.supplyUsePerSeventh);

    /// <summary>
    /// Rations a way (micro ids, from where the unit stands) eats outside your authority: inside it the stores keep
    /// the unit fed, so only the steps into unheld ground count.
    /// </summary>
    public static float RationsAway(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, IEnumerable<int> path)
    {
        if (spec == null || path == null) return 0f;
        var grid = MicroNavigation.Grid(map, settings);
        float away = 0f;
        int at = MicroNavigation.Index(map, MicroPosition(unit));
        foreach (int id in path)
        {
            float step = grid.Step(at, id);
            var cell = map[id / MicroNavigation.PerCell];
            if (!float.IsPositiveInfinity(step) && !WorldAuthority.IsPlayers(cell.authorityId) && cell.settlement < 0) away += step;
            at = id;
        }
        return RationsFor(unit, spec, away);
    }

    /// <summary>The rations its present journey eats outside your authority.</summary>
    public static float RationsAhead(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec) =>
        unit.Moving ? RationsAway(map, settings, unit, spec, unit.path.Select(c => MicroNavigation.Index(map, c)).Where(id => id >= 0)) : 0f;

    /// <summary>
    /// A unit standing idle for want of orders: not walking, working, camped (a camp is an order too), exploring by
    /// itself or walking back for rations.
    /// </summary>
    public static bool AwaitsOrders(WorldUnit unit) => unit != null && WorldBattles.IsPlayers(unit) && !unit.Missing && !unit.Moving && !unit.Working && !unit.Camping
        && !unit.autoExplore && !unit.returning && !unit.surveying && !unit.autoSurvey;

    /// <summary>How far along the task it works at is, 0-1 (0 when it is not working).</summary>
    public static float WorkProgress(WorldUnit unit) =>
        unit == null || !unit.Working || unit.workTotal <= 1e-4f ? 0f : Clamp01(1f - unit.workLeft / unit.workTotal);

    /// <summary>Where the unit is drawn: between its hex and the next, by the share of the next step already walked.</summary>
    public static (float x, float y) Position(WorldMap map, WorldGenSettings settings, WorldUnit unit)
    {
        var here = MicroPosition(unit);
        here.ToPixel(1f, out float x, out float y);
        if (!unit.Moving) return (x, y);
        unit.path[0].ToPixel(1f, out float nx, out float ny);
        float cost = MicroNavigation.Grid(map, settings).Step(MicroNavigation.Index(map, here), MicroNavigation.Index(map, unit.path[0]));
        if (float.IsPositiveInfinity(cost) || cost <= 0f) return (x, y);
        float t = Math.Max(0f, Math.Min(1f, unit.progress / cost));
        return (x + (nx - x) * t, y + (ny - y) * t);
    }

    /// <summary>
    /// How far (in micro hexes) the unit sees from where it stands: seers see farther on a leyline, anyone from a ridge
    /// or an escarpment's lip; weariness and hunger narrow it.
    /// </summary>
    public static int Sight(WorldMap map, WorldUnit unit, UnitSpec spec, SettlementRules rules)
    {
        var here = map.Get(unit.coord);
        int sight = spec != null ? Math.Max(0, spec.sight) : 1;
        if (here != null && here.leylines != 0 && rules != null) sight += Math.Max(0, rules.leylineRevealBonus);
        if (HighGround(here)) sight += 2;
        float efficiency = Efficiency(unit);
        if (efficiency < 0.6f) sight--;
        if (efficiency < 0.35f) sight--;
        // Inside dense cover a party sees only what is near (from a ridge it sees over the canopy).
        if (!HighGround(here)) sight = WorldCover.SightFrom(here, sight);
        return Math.Max(1, sight);
    }

    /// <summary>A vantage point: a high ridge, or the lip of an escarpment.</summary>
    public static bool HighGround(WorldTile t) => t != null && !t.water && (t.landform == "High ridge" || t.landform == "Escarpment");

    // ===== ABILITIES =====

    public static bool Can(UnitSpec spec, UnitAbility ability) => spec != null && (spec.abilities & ability) != 0;

    /// <summary>
    /// Why the unit cannot start <paramref name="ability"/> where it stands now (surveys only; foraging and building
    /// have their own rules below), or null.
    /// </summary>
    public static string WhyNotSurvey(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        ability = ability ?? UnitAbilities.Survey;
        if (!Can(spec, ability.ability)) return $"It cannot {ability.name.ToLowerInvariant()}.";
        if (unit.Moving) return "Stop it first.";
        if (unit.Working) return "Already at work.";
        return UnitAbilities.Targets(map, settings, unit, spec, ability).Count == 0 ? "Already surveyed." : null;
    }

    /// <summary>What foraging <paramref name="t"/> gives (its ground's forage, scaled), or empty.</summary>
    public static List<ResourceAmount> ForageOf(WorldGenSettings settings, WorldTile t, float multiplier)
    {
        var terrain = t != null ? settings.Terrain(t.terrain) : null;
        var result = (terrain?.forage ?? new List<ResourceAmount>()).Where(a => a != null && !string.IsNullOrEmpty(a.resource) && a.amount > 0f)
            .Select(a => new ResourceAmount { resource = a.resource, amount = a.amount * Math.Max(0f, multiplier) }).ToList();
        // Cover adds its own (game in the deep forest, roots in the brambles).
        foreach (var a in WorldCover.ForageOf(settings, t, multiplier))
        {
            var had = result.FirstOrDefault(r => string.Equals(r.resource, a.resource, StringComparison.OrdinalIgnoreCase));
            if (had != null) had.amount += a.amount;
            else result.Add(a);
        }
        return result;
    }

    /// <summary>
    /// How much more a forager gathers here, 1 or more: fertile land (past <see cref="ProvisionRules.fertileFrom"/>),
    /// living resource sites around (<see cref="WorldTile.siteBounty"/>) and a Resource Grandfield's heart, up to
    /// <see cref="ProvisionRules.maxForageBonus"/> more.
    /// </summary>
    public static float ForageRichness(ProvisionRules rules, UnitSurroundings at)
    {
        if (rules == null) return 1f;
        float from = Clamp01(rules.fertileFrom);
        float fertile = from >= 1f ? 0f : Math.Max(0f, Clamp01(at.fertility) - from) / (1f - from) * Math.Max(0f, rules.fertilityForage);
        return 1f + Math.Min(Math.Max(0f, rules.maxForageBonus), fertile + SitesBonus(rules, at));
    }

    /// <summary>What forage richness a cell has, read from its tile (see <see cref="ForageRichness(ProvisionRules, UnitSurroundings)"/>).</summary>
    public static float ForageRichness(ProvisionRules rules, WorldTile t) =>
        t == null ? 1f : ForageRichness(rules, new UnitSurroundings { fertility = t.landFertility, bounty = t.siteBounty, grandfield = t.grandfield >= 0 ? t.grandfieldDensity : 0f });

    // The living sites and grandfield around (fertility aside: a camp already gathers by it).
    private static float SitesBonus(ProvisionRules rules, UnitSurroundings at) =>
        Clamp01(at.bounty) * Math.Max(0f, rules.siteForage) + Clamp01(at.grandfield) * Math.Max(0f, rules.grandfieldForage);

    /// <summary>Why the unit cannot forage where it stands now (<paramref name="age"/> is the current Age's number), or null.</summary>
    public static string WhyNotForage(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, int age)
    {
        if (!Can(spec, UnitAbility.Forage)) return "It cannot forage.";
        if (unit.Moving) return "Stop it first.";
        if (unit.Working) return "Already at work.";
        var t = map.Get(unit.coord);
        if (t == null || !t.known) return "Nothing is known of this ground.";
        if (ForageOf(settings, t, 1f).Count == 0) return "Nothing grows or roams here to gather.";
        if (t.foragedAge == age + 1) return "Already foraged this Age.";
        return null;
    }

    /// <summary>
    /// Where a unit exploring by itself goes next: the nearest hex (by travel fatigue) of a cell no unit has passed
    /// over yet, so a scout ranges outward cell by cell instead of combing every hex around home. Within
    /// <paramref name="maxFatigue"/>; false when nothing is left in reach.
    /// </summary>
    public static bool NextToScout(WorldMap map, WorldGenSettings settings, WorldUnit unit, float maxFatigue, out HexCoord target) =>
        NextToScout(map, settings, unit, maxFatigue, out target, out _);

    /// <summary>As above, with the way there (micro ids after the start).</summary>
    public static bool NextToScout(WorldMap map, WorldGenSettings settings, WorldUnit unit, float maxFatigue, out HexCoord target, out List<int> path)
    {
        target = MicroPosition(unit);
        var grid = MicroNavigation.Grid(map, settings);
        if (!MicroNavigation.FindNearest(map, settings, target, id => !map[id / MicroNavigation.PerCell].known && !float.IsPositiveInfinity(grid.Enter(id)),
                out path, out _, maxFatigue) || path.Count == 0) return false;
        target = MicroNavigation.Coord(map, path[path.Count - 1]);
        return true;
    }

    /// <summary>The way to the nearest of your settlements (any hex of its cell), for rations; false when none can be reached.</summary>
    public static bool WayToResupply(WorldMap map, WorldGenSettings settings, WorldUnit unit, out List<int> path, out float fatigue, float maxFatigue = MicroNavigation.MaxPlan) =>
        MicroNavigation.FindNearest(map, settings, MicroPosition(unit), id => map[id / MicroNavigation.PerCell].settlement >= 0, out path, out fatigue, maxFatigue);

    // ===== HOTSPOTS =====

    /// <summary>A resource hotspot: known land that yields (its ground, its feature or a grandfield).</summary>
    public static bool IsHotspot(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        if (t == null || t.water || !t.explored) return false;
        if (t.grandfield >= 0) return true;
        if (WorldResources.YieldsAt(map, settings, t).Any(y => y.amount > 0f)) return true;
        if (WorldCover.YieldsOf(settings, t).Count > 0) return true;
        if (settings.Terrain(t.terrain)?.yields.Any(y => y != null && y.amount > 0f) == true) return true;
        return t.HasFeature && settings.Feature(t.feature)?.yields.Any(y => y != null && y.amount > 0f) == true;
    }

    /// <summary>The cell is held: inside your Administrative Authority, or a grandfield cell an Outpost stands on or beside.</summary>
    public static bool Held(WorldMap map, WorldTile t)
    {
        if (t == null) return false;
        if (t.authorityId == WorldAuthority.Player) return true;
        if (t.grandfield < 0) return false;
        return t.settlement >= 0 || map.NeighboursOf(t).Any(n => n.authorityId == WorldAuthority.Outpost || n.settlement >= 0);
    }

    public static string WhyNotImprove(WorldMap map, WorldGenSettings settings, SettlementRules rules, WorldTile t)
    {
        if (t == null) return "Nothing here.";
        if (!IsHotspot(map, settings, t)) return "No resource hotspot here to improve.";
        if (t.improvement >= Math.Max(1, rules.maxImprovement)) return "Improved as far as it goes.";
        if (!Held(map, t)) return "Hold it first: inside your authority, or a grandfield beside an Outpost.";
        return null;
    }

    // ===== LAND YIELDS =====

    /// <summary>
    /// What the land produces per second: the ground of held, explored cells and every explored feature, each raised by
    /// its improvement level, plus the improved cells of grandfields (a share of the field's base yield by density).
    /// One line per kind of ground or feature, so the ledger stays readable. The Capital's own cell is the city itself:
    /// it yields nothing here (its food comes from its buildings and clicks).
    /// </summary>
    public static List<WorldYield> LandYields(WorldMap map, WorldGenSettings settings, SettlementRules rules)
    {
        var sums = new Dictionary<(string source, string resource), float>();
        void Add(string source, string resource, float amount)
        {
            if (string.IsNullOrEmpty(resource) || amount == 0f) return;
            sums.TryGetValue((source, resource), out float s);
            sums[(source, resource)] = s + amount;
        }
        float bonus = rules != null ? rules.improvementBonus : 0.5f;
        foreach (var t in map.Tiles)
        {
            if (!t.explored || t.water || t.coord == map.Capital) continue;
            float scale = 1f + bonus * t.improvement;
            // People work fair land more willingly than hideous land (WorldBeauty).
            float work = Math.Max(0f, 1f + (rules?.territory?.beautyWork ?? 0f) * t.beauty);
            if (t.authorityId == WorldAuthority.Player)
            {
                var terrain = settings.Terrain(t.terrain);
                if (terrain != null) foreach (var y in terrain.yields) if (y != null) Add($"Land: {terrain.name}", y.resource, y.amount * scale * work);
            }
            // An identified resource site yields while its cell is yours (or your Outpost's).
            if (WorldAuthority.IsPlayers(t.authorityId) && t.resourceSite >= 0)
            {
                var site = WorldResources.SiteAt(map, t);
                foreach (var y in WorldResources.YieldsAt(map, settings, t)) Add($"Resource: {settings.ResourceSite(site.spec)?.name ?? site.name}", y.resource, y.amount * scale * work);
            }
            // Cover you hold is worked too: timber and game from the forest, peat from the fen.
            if (WorldAuthority.IsPlayers(t.authorityId) && t.cover != null)
                foreach (var y in WorldCover.YieldsOf(settings, t)) Add($"Cover: {settings.Cover(t.cover)?.name ?? t.cover}", y.resource, y.amount * scale * work);
            if (t.HasFeature)
            {
                var feature = settings.Feature(t.feature);
                if (feature != null) foreach (var y in feature.yields) if (y != null) Add($"Land: {feature.name}", y.resource, y.amount * scale);
            }
            if (t.grandfield >= 0 && t.improvement > 0 && t.grandfield < map.Grandfields.Count)
            {
                var spec = settings.Grandfield(map.Grandfields[t.grandfield].spec);
                if (spec != null) Add($"Improved grandfield: {spec.name}", spec.resource, spec.baseYield * t.grandfieldDensity * (rules != null ? rules.grandfieldImprovementShare : 0.5f) * t.improvement);
            }
        }
        return sums.Select(p => new WorldYield { source = p.Key.source, resource = p.Key.resource, amount = p.Value }).OrderBy(y => y.source, StringComparer.Ordinal).ThenBy(y => y.resource, StringComparer.Ordinal).ToList();
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
