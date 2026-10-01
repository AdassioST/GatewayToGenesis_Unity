using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Append only: these values are persisted in world saves.
/// <summary>What a band on the map is: a creature, an Atonalis, or one of the peoples (each drawn with its own mark).</summary>
public enum BandIdentity { Creature, Atonalis, Human, Demihuman, Humanoid, FormlessMass }
/// <summary>How a band thinks: Instinctive beasts chase anywhere; Regular ones will not follow across a river; Smart ones also weigh the odds and the ground.</summary>
public enum BandIntelligence { Instinctive, Regular, Smart }
/// <summary>Where a creature can go: on land, in the water (lakes, the sea and rivers), or both.</summary>
public enum CreatureHabitat { Land, Water, Amphibious }
/// <summary>
/// The Eight-Born Paths (vault: Eight-Born Paths.md), in the vault's order: the wound an Atonalis embodies, which sets how it
/// hunts (<see cref="AtonalPaths"/>). None: not an Atonalis.
/// </summary>
public enum AtonalPath { None, Anxithor, Discant, Obsessian, Signath, Carnalix, Animach, Violux, Erosyx }
/// <summary>What a band is doing about the units around it.</summary>
public enum BandActivity { Roaming, Pursuing, Searching, Fleeing, Returning, Recovering, Stalking, Feeding, Cocooned }

/// <summary>
/// Encounters on the map, with no scene state (tested in <c>WorldPursuitTests</c>): who a band is, how it thinks, how
/// far it will chase, and the running endurance of every unit. Distances are micro hexes. Every number is a proposal.
///
/// - Endurance (0-100): running (a band pursuing or fleeing, a party of yours told to run or giving chase) goes faster but
///   spends it; below <see cref="FullPaceEndurance"/> the runner slows, at 0 it is winded and cannot move until it has
///   caught its breath (<see cref="ResumeEndurance"/>). A quarry run long enough can be run down: that is one way to hunt.
///   People are persistence hunters (they tire slowest); ambush predators sprint hard and tire fast.
/// - Leash: a band gives up once it has chased <see cref="WorldUnit.pursuitLimit"/> hexes, once its quarry leaves the
///   territory it defends, or (thinking) once the quarry reaches ground it will not follow onto.
/// - Intelligence: Regular and Smart bands will not cross a river or ford after you (the far bank ends the chase);
///   Smart ones also avoid climbing into danger, refuse fights they would lose, will not attack you on ground that
///   favours you (they stalk instead) and will not follow you into your own territory.
/// </summary>
public static class WorldPursuit
{
    /// <summary>Endurance a running unit spends per travel fatigue walked (times its <see cref="WorldUnit.wind"/>).</summary>
    public const float RunDrain = 4f;
    /// <summary>A winded unit moves again once back to this much endurance.</summary>
    public const float ResumeEndurance = 30f;
    /// <summary>Above this a runner keeps its full sprint; below it slows toward <see cref="SpentPace"/>.</summary>
    public const float FullPaceEndurance = 45f;
    /// <summary>A runner's pace (of its walk) as its endurance runs out.</summary>
    public const float SpentPace = 0.35f;
    /// <summary>Endurance regained per Seventh: walking, and standing (or winded).</summary>
    public const float WalkRecovery = 5f, RestRecovery = 16f;
    /// <summary>Sevenths between a band's decisions (it re-plans at this cadence, not every frame).</summary>
    public const float DecisionSevenths = 0.2f;
    /// <summary>Sevenths a predator rests after a kill before it hunts again.</summary>
    public const float SatedSevenths = 6f;
    /// <summary>Sevenths a band waits after giving up a chase before it starts another.</summary>
    public const float ChaseCooldown = 3f;
    /// <summary>A timid band flees one of your units this close (one that flees on sight: as soon as it sees it).</summary>
    public const int FleeReach = 3;
    /// <summary>Your parties sprint this much faster than they walk, and tire this slowly (people are persistence hunters).</summary>
    public const float PeopleSprint = 1.5f, PeopleWind = 0.6f;
    /// <summary>A Smart band backs off from a fight it would enter at less than this share of the other side's strength.</summary>
    public const float SmartOdds = 0.9f;

    // ===== WHO IT IS =====

    /// <summary>The species a band is (a threat's beings as one, a Formless Mass as its own kind), or null.</summary>
    public static SpeciesSpec Species(WorldGenSettings gen, WorldUnit u) => gen == null || u == null ? null
        : string.IsNullOrEmpty(u.species) ? WorldBattles.Being(ThreatOf(gen, u.threat), u.binding) : SpeciesById(gen, u.species);

    /// <summary>A threat by id, the Dissonance cocoon that hatches the land's own Atonalis included.</summary>
    public static ThreatSpec ThreatOf(WorldGenSettings gen, string id) =>
        string.IsNullOrEmpty(id) ? null : string.Equals(id, WorldSuffering.Hatchling.id, StringComparison.OrdinalIgnoreCase) ? WorldSuffering.Hatchling : gen?.Threat(id);

    /// <summary>A species by id, the Formless Mass included.</summary>
    public static SpeciesSpec SpeciesById(WorldGenSettings gen, string id) =>
        string.Equals(id, WorldSuffering.MassId, StringComparison.OrdinalIgnoreCase) ? WorldSuffering.Mass : gen?.Species(id);

    /// <summary>Where its bands can go: the species' habitat; a Pure Light being with fins lives in the water at least.</summary>
    public static CreatureHabitat HabitatOf(SpeciesSpec s) =>
        s == null ? CreatureHabitat.Land : s.habitat == CreatureHabitat.Land && s.binding == BindingOrgan.Fins ? CreatureHabitat.Water : s.habitat;

    /// <summary>A threat's own beings are Atonalis; any other band is what its species says.</summary>
    public static BandIdentity IdentityOf(WorldUnit u, SpeciesSpec s) =>
        u != null && string.IsNullOrEmpty(u.species) && !string.IsNullOrEmpty(u.threat) ? BandIdentity.Atonalis : s?.identity ?? BandIdentity.Creature;

    /// <summary>
    /// How it thinks: the species' own setting, raised by its nature (the Smart subgroup is Smart; omnivores and pack
    /// hunters are Regular; the peoples are at least Regular, humans and demihumans Smart). Beasts are Instinctive.
    /// </summary>
    public static BandIntelligence IntelligenceOf(BandIdentity identity, SpeciesSpec s)
    {
        var derived = BandIntelligence.Instinctive;
        if (s != null)
        {
            if (s.subgroup == CreatureSubgroup.Smart) derived = BandIntelligence.Smart;
            else if (s.diet == CreatureDiet.Omnivore || s.diet == CreatureDiet.Carnivore && s.subgroup == CreatureSubgroup.Social) derived = BandIntelligence.Regular;
        }
        if (identity == BandIdentity.Human || identity == BandIdentity.Demihuman) derived = BandIntelligence.Smart;
        else if (identity != BandIdentity.Creature && identity != BandIdentity.FormlessMass && derived < BandIntelligence.Regular) derived = BandIntelligence.Regular;
        var own = s?.intelligence ?? BandIntelligence.Instinctive;
        return own > derived ? own : derived;
    }

    /// <summary>Hexes it will chase: the species' own, else by how it meets others (pack hunters run far, ambushers hardly at all).</summary>
    public static int PursuitOf(BandIdentity identity, SpeciesSpec s, ThreatResponse response)
    {
        if (s != null && s.pursuitTiles > 0) return s.pursuitTiles;
        if (identity == BandIdentity.Atonalis) return 14;
        switch (response)
        {
            case ThreatResponse.Hunts: return 16;
            case ThreatResponse.HuntsLoudly: return 12;
            case ThreatResponse.AttacksOnSight: case ThreatResponse.Expands: return 10;
            case ThreatResponse.DefendsTerritory: return 6;
            case ThreatResponse.Lures: return 4;
            default: return 8;
        }
    }

    /// <summary>The territory it defends around home, in hexes: the species' own, else by how it ranges.</summary>
    public static int TerritoryOf(SpeciesSpec s)
    {
        if (s != null && s.territoryTiles > 0) return s.territoryTiles;
        var profile = s == null ? null : CreatureTaxonomy.Profile(s.diet, s.subgroup);
        switch (profile?.ranging ?? CreatureRanging.Territorial)
        {
            case CreatureRanging.Settled: return 2;
            case CreatureRanging.Roaming: return 6;
            case CreatureRanging.Nomadic: return 8;
            case CreatureRanging.Expanding: return 5;
            default: return 4;
        }
    }

    /// <summary>Its sprint (x its walk): the smaller, the quicker off the mark.</summary>
    public static float SprintOf(BandIdentity identity, SpeciesSpec s)
    {
        if (identity == BandIdentity.FormlessMass) return 0.8f;
        if (identity != BandIdentity.Creature) return identity == BandIdentity.Atonalis ? 1.5f : PeopleSprint;
        switch (s?.size ?? CreatureSize.Medium)
        {
            case CreatureSize.Small: return 1.7f;
            case CreatureSize.Large: return 1.45f;
            case CreatureSize.Gargantuan: return 1.2f;
            default: return 1.6f;
        }
    }

    /// <summary>How fast running tires it (x <see cref="RunDrain"/>): ambush predators burn out, pack hunters and the Atonalis last.</summary>
    public static float WindOf(BandIdentity identity, SpeciesSpec s)
    {
        if (identity == BandIdentity.Atonalis) return 0.7f;
        if (identity == BandIdentity.FormlessMass) return 1.5f;
        if (identity != BandIdentity.Creature) return PeopleWind + 0.1f;
        float wind;
        switch (s?.diet ?? CreatureDiet.Herbivore)
        {
            case CreatureDiet.Carnivore:
                wind = s.subgroup == CreatureSubgroup.Social ? 0.8f : s.subgroup == CreatureSubgroup.Marauder || s.subgroup == CreatureSubgroup.Solitary || s.subgroup == CreatureSubgroup.Trapper ? 1.4f : 1.1f;
                break;
            case CreatureDiet.Omnivore: wind = 0.9f; break;
            case CreatureDiet.Detritivore: wind = 1.2f; break;
            default: wind = 1f; break;
        }
        return s != null && s.size == CreatureSize.Gargantuan ? wind * 1.2f : wind;
    }

    /// <summary>Sevenths it keeps hunting a quarry it lost sight of.</summary>
    public static float MemoryOf(BandIntelligence i) => i == BandIntelligence.Smart ? 3f : i == BandIntelligence.Regular ? 1.5f : 0.6f;

    /// <summary>Hexes it sees beyond its kind's sight (Smart bands keep a lookout).</summary>
    public static int SightBonus(BandIntelligence i) => i == BandIntelligence.Smart ? 1 : 0;

    public static bool Fearful(ThreatResponse r) =>
        r == ThreatResponse.FleesOnSight || r == ThreatResponse.FleesWhenThreatened || r == ThreatResponse.Hides || r == ThreatResponse.AvoidsConflict;

    /// <summary>
    /// Sets up a unit's encounter state once (and again for a save made before it existed, whose fields load as zero):
    /// full endurance and no quarry; a band also learns who it is, how it thinks and how far it ranges.
    /// </summary>
    public static void Initialize(WorldGenSettings gen, WorldUnit u)
    {
        if (u == null || u.encounterInitialized) return;
        u.encounterInitialized = true;
        u.endurance = 100f;
        u.winded = false;
        u.quarryId = -1;
        u.chaseTiles = 0;
        if (WorldBattles.IsPlayers(u))
        {
            u.sprintPace = PeopleSprint;
            u.wind = PeopleWind;
            return;
        }
        var s = Species(gen, u);
        u.identity = IdentityOf(u, s);
        if (s != null) { u.majorEncounter |= s.majorEncounter; u.boss |= s.boss; u.decisiveEncounter |= s.decisiveEncounter; u.originalEight |= s.originalEight; u.battleObjective = u.battleObjective ?? s.battleObjective; }
        u.intelligence = IntelligenceOf(u.identity, s);
        u.response = s == null ? ThreatResponse.DefendsWhenThreatened : WorldBehavior.Nature(s);
        if (Fearful(u.response) && u.stance == EnemyStance.Wary) u.stance = EnemyStance.Timid;
        u.pursuitLimit = Math.Max(1, PursuitOf(u.identity, s, u.response));
        u.territoryRadius = Math.Max(1, TerritoryOf(s));
        u.sprintPace = SprintOf(u.identity, s);
        u.wind = WindOf(u.identity, s);
        u.habitat = HabitatOf(s);
        if (u.identity == BandIdentity.FormlessMass) u.stride = WorldSuffering.MassStride;
        if (u.atonalPath != AtonalPath.None) ApplyPath(u);
    }

    // ===== RUNNING =====

    /// <summary>Running: a band pursuing or fleeing, or a party of yours told to run.</summary>
    public static bool Running(WorldUnit u) => u != null && (u.sprinting || u.bandActivity == BandActivity.Pursuing || u.bandActivity == BandActivity.Fleeing);

    /// <summary>Its pace (x its walk): 0 winded, 1 walking; running, its sprint while fresh, slowing as its endurance runs out.</summary>
    public static float RunPace(WorldUnit u)
    {
        if (u.winded) return 0f;
        if (!Running(u)) return u.stride > 0f ? u.stride : 1f;
        float sprint = u.sprintPace > 0f ? u.sprintPace : PeopleSprint;
        return Mathf.Lerp(SpentPace, sprint, Mathf.Clamp01(u.endurance / FullPaceEndurance));
    }

    /// <summary>A running unit spends endurance for the travel fatigue it walked; spent, it is winded.</summary>
    public static void Spend(WorldUnit u, float effort)
    {
        if (!Running(u)) return;
        u.endurance = Mathf.Clamp(u.endurance - Math.Max(0f, effort) * RunDrain * (u.wind > 0f ? u.wind : 1f), 0f, 100f);
        if (u.endurance <= 0.001f) { u.endurance = 0f; u.winded = true; }
    }

    /// <summary>Endurance comes back: slowly while walking, fast standing (or winded); a winded unit goes on at <see cref="ResumeEndurance"/>.</summary>
    public static void Recover(WorldUnit u, float time)
    {
        if (u.winded || !u.Moving || !Running(u))
            u.endurance = Mathf.Min(100f, u.endurance + Math.Max(0f, time) * (u.Moving && !u.winded ? WalkRecovery : RestRecovery));
        if (u.winded && u.endurance >= ResumeEndurance) u.winded = false;
    }

    /// <summary>How spent a unit is for a battle (0 fresh, 1 winded): it fights with less nerve and a weaker guard.</summary>
    public static float Tiredness(WorldUnit u) => u == null ? 0f : u.winded ? 1f : Mathf.Clamp01(1f - u.endurance / 100f);

    /// <summary>A side fights as tired as its unit: Composure down by half its tiredness, defense by 30%.</summary>
    public static void Tire(BattleSide side, float tiredness)
    {
        if (side == null || tiredness <= 0.001f) return;
        foreach (var s in side.sections)
        {
            s.composure *= 1f - 0.5f * tiredness;
            s.defense *= 1f - 0.3f * tiredness;
        }
    }

    // ===== THE EIGHT-BORN PATHS =====

    /// <summary>An Atonalis band takes on its Path's mind, leash and memory (<see cref="AtonalPaths"/>).</summary>
    public static void ApplyPath(WorldUnit u)
    {
        if (u == null || u.atonalPath == AtonalPath.None) return;
        var t = AtonalPaths.Of(u.atonalPath);
        u.intelligence = t.intelligence;
        u.pursuitLimit = Math.Max(1, t.pursuit);
        if (t.territorial)
        {
            u.response = ThreatResponse.DefendsTerritory;
            u.territoryRadius = Math.Max(u.territoryRadius, 6);
        }
    }

    /// <summary>Sevenths it keeps looking for a lost quarry: its mind's memory, times its Path's.</summary>
    public static float MemoryOf(WorldUnit u) => MemoryOf(u.intelligence) * (u.atonalPath != AtonalPath.None ? AtonalPaths.Of(u.atonalPath).memory : 1f);

    // ===== WHO GOES AFTER WHOM =====

    /// <summary>
    /// A band that preys on the other: its species' prey, or any creature for a Carnalix (flesh is flesh). Where they
    /// live decides where they can meet (a land hunter reaches prey in the water only at the shore).
    /// </summary>
    public static bool Predates(WorldGenSettings gen, WorldUnit hunter, WorldUnit prey)
    {
        if (gen == null || hunter == null || prey == null || hunter == prey || WorldBattles.IsPlayers(hunter) || WorldBattles.IsPlayers(prey)) return false;
        if (prey.creatures <= 0 || string.IsNullOrEmpty(prey.species) || hunter.species == prey.species || prey.identity != BandIdentity.Creature) return false;
        if (hunter.atonalPath != AtonalPath.None) return AtonalPaths.Of(hunter.atonalPath).eatsFlesh;
        if (string.IsNullOrEmpty(hunter.species)) return false;
        var species = gen.Species(hunter.species);
        return species?.prey != null && species.prey.Any(p => string.Equals(p, prey.species, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a hex lies inside the territory the band defends around home.</summary>
    public static bool InsideHome(WorldMap map, WorldUnit u, HexCoord at) => u.homeCell >= 0 && u.homeCell < map.Count &&
        HexCoord.Distance(MicroNavigation.Center(map[u.homeCell].coord), at) <= u.territoryRadius;

    /// <summary>
    /// Whether one of your units provokes a wary band: coming adjacent, passing within <see cref="WorldBattles.PassingReach"/>,
    /// or walking into the territory it defends while it can see you.
    /// </summary>
    public static bool Provokes(WorldMap map, WorldUnit yours, WorldUnit band, int sight)
    {
        // One that has learned you mean no harm (or cares for nothing) lets you pass.
        if (band.response == ThreatResponse.Tolerates || band.response == ThreatResponse.Unmoved) return false;
        if (WorldBattles.Provokes(yours, band)) return true;
        if (!WorldBattles.IsPlayers(yours) || band.stance != EnemyStance.Wary || band.truce > 0f || yours.Missing) return false;
        var at = WorldUnits.MicroPosition(yours);
        return map != null && InsideHome(map, band, at) && HexCoord.Distance(WorldUnits.MicroPosition(band), at) <= sight;
    }

    /// <summary>
    /// Why a band stops chasing toward <paramref name="target"/>, or null: it has run its leash, the quarry left the
    /// territory it defends, a Smart band will not follow into your territory, a Regular one into a settlement.
    /// </summary>
    public static string Leash(WorldMap map, WorldUnit u, HexCoord target)
    {
        if (u.chaseTiles >= u.pursuitLimit) return "gives up the chase";
        if (u.response == ThreatResponse.DefendsTerritory && !InsideHome(map, u, target)) return "lets you go once you leave its ground";
        var tile = MicroNavigation.Parent(map, target);
        if (tile != null && u.intelligence == BandIntelligence.Smart && WorldAuthority.IsPlayers(tile.authorityId)) return "will not follow into your territory";
        if (tile != null && u.intelligence >= BandIntelligence.Regular && tile.settlement >= 0) return "will not come near the settlement";
        return null;
    }

    // ===== THE WAY =====

    /// <summary>Water: a lake's or the sea's hexes, or a river's (its fords).</summary>
    public static bool IsWater(WorldMap map, MicroGrid grid, int id)
    {
        if (id < 0 || id >= map.Count * MicroNavigation.PerCell) return false;
        var cell = map[id / MicroNavigation.PerCell];
        return cell.water || cell.lake || (grid.Marks(id) & (MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord)) != 0;
    }

    /// <summary>Travel fatigue of one swim step (the current and the depth are no burden to what lives there).</summary>
    public const float SwimCost = 1f;

    /// <summary>
    /// What a step costs this unit, or +infinity where it cannot go: the land's own cost for what walks (water cells are
    /// closed to it; a river is forded); a swim step for what lives in the water, which never leaves it (it comes no
    /// further than a river's hexes and the shore); an amphibious creature takes whichever is open.
    /// </summary>
    public static float StepCost(WorldMap map, MicroGrid grid, WorldUnit u, int from, int to)
    {
        var habitat = u == null || WorldBattles.IsPlayers(u) ? CreatureHabitat.Land : u.habitat;
        if (habitat == CreatureHabitat.Land) return grid.Step(from, to);
        bool wet = IsWater(map, grid, to);
        if (habitat == CreatureHabitat.Water) return wet ? SwimCost : float.PositiveInfinity;
        return wet ? SwimCost * 1.2f : grid.Enter(to);
    }

    /// <summary>A hex the unit can stand on at all (its habitat's ground).</summary>
    public static bool CanStand(WorldMap map, MicroGrid grid, WorldUnit u, int id)
    {
        if (id < 0) return false;
        var habitat = u == null || WorldBattles.IsPlayers(u) ? CreatureHabitat.Land : u.habitat;
        bool wet = IsWater(map, grid, id);
        if (habitat == CreatureHabitat.Water) return wet;
        bool dry = !float.IsPositiveInfinity(grid.Enter(id));
        return habitat == CreatureHabitat.Amphibious ? wet || dry : dry;
    }

    /// <summary>
    /// Whether a band may take the step: it must be able to go there at all (<see cref="StepCost"/>); then Instinctive
    /// ones go anywhere; Regular and Smart land bands will not cross a river or ford (a road over it is a bridge) unless
    /// they are running for their lives; Smart ones also keep off steep climbs, deeper danger and foul weather.
    /// </summary>
    public static bool SafeStep(WorldMap map, MicroGrid grid, WorldUnit u, int from, int to, bool fleeing = false)
    {
        if (float.IsInfinity(StepCost(map, grid, u, from, to))) return false;
        if (fleeing || WorldBattles.IsPlayers(u) || u.intelligence == BandIntelligence.Instinctive || u.habitat != CreatureHabitat.Land) return true;
        if (Crossing(grid, from, to)) return false;
        if (u.intelligence != BandIntelligence.Smart) return true;
        var a = map[from / MicroNavigation.PerCell];
        var b = map[to / MicroNavigation.PerCell];
        return b.elevation - a.elevation < 0.12f && Math.Max(b.danger, b.signs) <= Math.Max(a.danger, a.signs) + 0.25f && b.weatherTravelMultiplier <= 1.8f;
    }

    /// <summary>A step into or out of a river or ford hex that no road bridges.</summary>
    public static bool Crossing(MicroGrid grid, int from, int to)
    {
        var fords = MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord;
        return ((grid.Marks(from) | grid.Marks(to)) & fords) != 0 && (grid.Marks(to) & MicroGrid.Mark.Road) == 0;
    }

    /// <summary>
    /// A bounded search for the way to <paramref name="target"/> (beside it when <paramref name="adjacent"/>), keeping to
    /// the unit's <see cref="SafeStep"/> and around every other unit. It never substitutes a nearer hex: an unreachable
    /// quarry is false (the far bank of a river ends a thinking band's chase; the shore ends a fish's). The path excludes the start.
    /// </summary>
    public static bool Route(WorldMap map, WorldGenSettings gen, WorldUnit u, HexCoord target, bool adjacent, out List<int> path, int budget = 512, bool fleeing = false)
    {
        path = new List<int>();
        if (map == null || u == null) return false;
        int start = MicroNavigation.Index(map, WorldUnits.MicroPosition(u));
        if (start < 0 || MicroNavigation.Index(map, target) < 0) return false;
        var grid = MicroNavigation.Grid(map, gen);
        var blocked = new HashSet<int>(map.Units.Where(t => t != u && !t.Missing).Select(t => MicroNavigation.Index(map, WorldUnits.MicroPosition(t))));
        var costs = new Dictionary<int, float> { [start] = 0f };
        var previous = new Dictionary<int, int>();
        var open = new SortedSet<(float cost, int id)> { (0f, start) };
        int found = -1;
        while (open.Count > 0 && budget-- > 0)
        {
            var next = open.Min;
            open.Remove(next);
            if (costs[next.id] < next.cost) continue;
            var at = MicroNavigation.Coord(map, next.id);
            if (HexCoord.Distance(at, target) <= (adjacent ? 1 : 0)) { found = next.id; break; }
            for (int d = 0; d < 6; d++)
            {
                int n = MicroNavigation.Neighbour(map, next.id, d);
                if (n < 0 || blocked.Contains(n) || !SafeStep(map, grid, u, next.id, n, fleeing)) continue;
                float cost = next.cost + StepCost(map, grid, u, next.id, n);
                if (cost > 80f || costs.TryGetValue(n, out float old) && old <= cost) continue;
                costs[n] = cost;
                previous[n] = next.id;
                open.Add((cost, n));
            }
        }
        if (found < 0) return false;
        for (int n = found; n != start; n = previous[n]) path.Add(n);
        path.Reverse();
        return true;
    }

    /// <summary>
    /// How perilous a hex is to run into (0 none): a river to ford (for what walks), known danger or fresh signs of a
    /// hunter, Fallout, Dissonance, a hard cover, foul weather, a steep climb.
    /// </summary>
    public static float Peril(WorldMap map, MicroGrid grid, WorldUnit u, int from, int id)
    {
        var t = map[id / MicroNavigation.PerCell];
        var here = map[from / MicroNavigation.PerCell];
        float p = 0f;
        if (u.habitat == CreatureHabitat.Land && (grid.Marks(id) & (MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord)) != 0 && (grid.Marks(id) & MicroGrid.Mark.Road) == 0) p += 0.6f;
        p += Math.Max(t.danger, t.signs * 0.8f) * 1.2f;
        p += t.fallout * 1.5f + Math.Max(0f, t.dissonance - 0.3f);
        p += Math.Max(0f, t.coverHardship) * 2f;
        if (t.weatherTravelMultiplier > 1.8f) p += 0.4f;
        if (t.elevation - here.elevation > 0.12f) p += 0.4f;
        return p;
    }

    /// <summary>
    /// Where a band runs from <paramref name="danger"/>: of the hexes it can reach within five, the one that best puts
    /// ground between them for the least effort, without straying far from home and without running into peril
    /// (<see cref="Peril"/>) unless every way out is perilous. Hard ground costs it: a fleeing beast keeps to easy going by
    /// instinct. A Smart band also values a river between them (a thinking pursuer will not follow) and high ground.
    /// Empty when it is cornered.
    /// </summary>
    public static List<int> Escape(WorldMap map, WorldGenSettings gen, WorldUnit u, HexCoord danger, int reach = 5, int budget = 260)
    {
        var result = new List<int>();
        int start = MicroNavigation.Index(map, WorldUnits.MicroPosition(u));
        if (start < 0) return result;
        var grid = MicroNavigation.Grid(map, gen);
        var from = WorldUnits.MicroPosition(u);
        int d0 = HexCoord.Distance(from, danger);
        var home = u.homeCell >= 0 && u.homeCell < map.Count ? MicroNavigation.Center(map[u.homeCell].coord) : from;
        int homeRange = Math.Max(3, u.territoryRadius + 2);
        var blocked = new HashSet<int>(map.Units.Where(t => t != u && !t.Missing).Select(t => MicroNavigation.Index(map, WorldUnits.MicroPosition(t))));
        var costs = new Dictionary<int, float> { [start] = 0f };
        var peril = new Dictionary<int, float> { [start] = 0f };
        var crossed = new Dictionary<int, bool> { [start] = false };
        var previous = new Dictionary<int, int>();
        var open = new SortedSet<(float cost, int id)> { (0f, start) };
        int bestSafe = -1, bestAny = -1;
        float safeScore = float.NegativeInfinity, anyScore = float.NegativeInfinity;
        while (open.Count > 0 && budget-- > 0)
        {
            var next = open.Min;
            open.Remove(next);
            if (costs[next.id] < next.cost) continue;
            var at = MicroNavigation.Coord(map, next.id);
            int dn = HexCoord.Distance(at, danger);
            if (next.id != start && dn > d0)
            {
                float score = (dn - d0) * 2f - costs[next.id] * 0.35f - Math.Max(0, HexCoord.Distance(at, home) - homeRange) * 1.2f;
                if (u.intelligence == BandIntelligence.Smart)
                {
                    if (crossed[next.id]) score += 3f;
                    score += map[next.id / MicroNavigation.PerCell].elevation * 4f;
                }
                float p = peril[next.id];
                if (p < 0.3f && score > safeScore) { safeScore = score; bestSafe = next.id; }
                if (score - p * 4f > anyScore) { anyScore = score - p * 4f; bestAny = next.id; }
            }
            if (HexCoord.Distance(at, from) >= reach) continue;
            for (int d = 0; d < 6; d++)
            {
                int n = MicroNavigation.Neighbour(map, next.id, d);
                if (n < 0 || blocked.Contains(n) || HexCoord.Distance(MicroNavigation.Coord(map, n), danger) < 1) continue;
                float step = StepCost(map, grid, u, next.id, n);
                if (float.IsInfinity(step) || float.IsNaN(step)) continue;
                float cost = next.cost + step;
                if (costs.TryGetValue(n, out float old) && old <= cost) continue;
                costs[n] = cost;
                bool swim = u.habitat == CreatureHabitat.Land && Crossing(grid, next.id, n);
                crossed[n] = crossed[next.id] || swim;
                // A Smart band swims across on purpose; to anyone else a ford is a peril.
                float stepPeril = Peril(map, grid, u, next.id, n);
                if (u.intelligence == BandIntelligence.Smart && swim) stepPeril = Math.Max(0f, stepPeril - 0.6f);
                peril[n] = Math.Max(peril[next.id], stepPeril);
                previous[n] = next.id;
                open.Add((cost, n));
            }
        }
        int goal = bestSafe >= 0 ? bestSafe : bestAny;
        if (goal < 0) return result;
        for (int n = goal; n != start; n = previous[n]) result.Add(n);
        result.Reverse();
        return result;
    }

    /// <summary>The first steps of a way toward a quarry that stop <paramref name="keep"/> hexes short of it (a stalker's distance).</summary>
    public static List<int> KeepOff(WorldMap map, List<int> path, HexCoord quarry, int keep)
    {
        var kept = new List<int>();
        foreach (int id in path)
        {
            if (HexCoord.Distance(MicroNavigation.Coord(map, id), quarry) < keep) break;
            kept.Add(id);
        }
        return kept;
    }

    // ===== WORDS =====

    public static string IdentityName(BandIdentity i) => i == BandIdentity.FormlessMass ? "Formless Mass" : i == BandIdentity.Atonalis ? "Atonalis" : i == BandIdentity.Human ? "Humans"
        : i == BandIdentity.Demihuman ? "Demihumans" : i == BandIdentity.Humanoid ? "Humanoids" : "Creatures";

    public static string IntelligenceText(BandIntelligence i) => i == BandIntelligence.Smart
        ? "Smart: will not cross rivers after you, climb into danger or follow you into your territory; backs off from fights it would lose and waits out ground that favours you."
        : i == BandIntelligence.Regular
            ? "Regular: will not follow you across a river or into a settlement; remembers where it last saw you for a while."
            : "Instinctive: chases anywhere, rivers and all, until its leash runs out; forgets you quickly once you are out of sight.";

    /// <summary>What the band means to you, as its colour says it.</summary>
    public static string StanceText(WorldUnit u) => u.stance == EnemyStance.Timid
        ? "Timid: flees from your parties. Run it down while it tires to hunt it."
        : WorldBattles.Angry(u) ? (u.stance == EnemyStance.Hostile ? "Hostile: attacks your parties on sight." : "Provoked: attacking you until it calms down.")
        : "Wary: turns hostile if you come adjacent, pass within 2 hexes, or enter its territory.";

    /// <summary>What it is doing now, in a few words.</summary>
    public static string Status(WorldUnit u) => u.bandActivity == BandActivity.Cocooned ? $"A Dissonance cocoon: an Atonalis hatches in about {u.cocoon:0.#} Sevenths"
        : u.winded ? "Exhausted: catching its breath"
        : u.bandActivity == BandActivity.Pursuing ? $"Chasing: {Math.Max(0, u.pursuitLimit - u.chaseTiles)} hexes before it gives up"
        : u.bandActivity == BandActivity.Stalking ? "Stalking: waiting for you to leave your ground"
        : u.bandActivity == BandActivity.Fleeing ? "Fleeing"
        : u.bandActivity == BandActivity.Searching ? "Searching where it last saw its quarry"
        : u.bandActivity == BandActivity.Returning ? (u.denLife < 0f ? "Heading back to its den" : "Returning to its territory")
        : u.bandActivity == BandActivity.Feeding ? (u.identity == BandIdentity.FormlessMass ? "Drinking the suffering of the land" : "Feeding on a kill")
        : u.bandActivity == BandActivity.Recovering ? "Catching its breath"
        : u.stance == EnemyStance.Timid ? "Grazing warily" : WorldBattles.Angry(u) ? "On the prowl" : "Roaming its ground";

    /// <summary>An endurance bar for rich text: bright ticks for what is left (amber when slowing, red when winded), dim for the rest.</summary>
    public static string Bar(WorldUnit u, int cells = 20)
    {
        int full = Mathf.Clamp(Mathf.RoundToInt(u.endurance / 100f * cells), 0, cells);
        string tone = u.winded ? "E0503C" : u.endurance < FullPaceEndurance ? "E6A040" : "8FD07A";
        return $"<color=#{tone}>{new string('|', full)}</color><color=#4A4038>{new string('|', cells - full)}</color> {u.endurance:0}";
    }
}
