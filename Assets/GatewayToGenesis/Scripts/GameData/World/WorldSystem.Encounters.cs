using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The living map (<see cref="WorldPursuit"/>, <see cref="AtonalPaths"/>, <see cref="WorldSuffering"/>): what every band
/// does about the units around it, the dens' visiting bands, predators running down their prey, your parties giving
/// chase, the signs hunters leave (the only warning an explorer gets), and the suffering battles and hunts leave on the
/// land, which pools into Formless Masses that drain it and, fed enough, cocoon and hatch into Atonalis.
///
/// A band decides every <see cref="WorldPursuit.DecisionSevenths"/>, in this order: flee what it fears (a predator of its
/// kind; your parties if it is timid, if it is Smart and would lose, or if it is an Erosyx and you come in numbers); go
/// home after a chase; chase its quarry (your parties while angry, its prey while hungry, a Violux's old victim on sight)
/// until its leash runs out, the ground stops it or, Smart, it would fight on ground that favours you (it stalks
/// instead); look where it last saw the quarry; else roam its ground (an Obsessian walks its loop). What lives in the
/// water never leaves it; what walks never swims but to cross a river.
/// </summary>
public partial class WorldSystem
{
    // Saved (GameSnapshot): the suffering the land holds, cell by cell.
    [SaveOptionalField] private List<SufferingScar> _suffering = new List<SufferingScar>();

    // Den visits, the land's suffering and the hunters' signs are weighed a few times a Seventh, not every quantum (not
    // saved: at most a moment is lost on load; the signs fade within a few Sevenths anyway).
    private float _denClock, _ecoClock, _ecoTime;
    private const float DenTick = 0.25f, EcoTick = 0.25f;
    private readonly Dictionary<int, Sign> _signs = new Dictionary<int, Sign>();

    /// <summary>Bumped whenever the land's feelings and signs move (the Feelings and Danger lenses repaint).</summary>
    public int FeelingsVersion { get; private set; }

    // Fresh signs of a hunter on a cell: how fresh (0-1), whose they are, and until when explorers were told of them.
    private sealed class Sign
    {
        public float fresh;
        public int band;
        public float warnedUntil;
    }

    /// <summary>At most this many den visitors are out at once.</summary>
    public const int DenVisitorCap = 24;
    /// <summary>Signs a hunter leaves on its cell per Seventh, and how fast they fade.</summary>
    public const float SignsPerSeventh = 0.6f, SignsFade = 0.3f;
    /// <summary>Signs this fresh (and the hunter out of sight) warn an explorer who comes within sight of them.</summary>
    public const float SignsWarn = 0.35f;

    /// <summary>The suffering the land holds (read-only view, for reports and tests).</summary>
    public IReadOnlyList<SufferingScar> Suffering => _suffering ?? (_suffering = new List<SufferingScar>());

    // ===== A BAND'S MOMENT =====

    private bool StepEncounter(WorldUnit band, UnitSpec spec, float time, List<string> notice)
    {
        var gen = Settings.generation;
        WorldPursuit.Initialize(gen, band);
        bool wasWinded = band.winded;
        WorldPursuit.Recover(band, time);
        bool changed = wasWinded != band.winded;
        band.provoked = Math.Max(0f, band.provoked - time);
        band.chaseCooldown = Math.Max(0f, band.chaseCooldown - time);
        band.memoryLeft = Math.Max(0f, band.memoryLeft - time);
        band.sated = Math.Max(0f, band.sated - time);
        band.decisionLeft -= time;
        if (band.denLife > 0f)
        {
            // A den's visitor stays out a while, then heads home and is gone once there.
            band.denLife = Math.Max(0f, band.denLife - time);
            if (band.denLife <= 0f) { band.denLife = -1f; band.decisionLeft = 0f; }
        }
        // Wild units have no provisions loop: ordinary wear eases whenever they are not running.
        if (!WorldPursuit.Running(band)) band.fatigue = Math.Max(0f, band.fatigue - time * 8f);
        if (Hunting(band)) LeaveSigns(band, time);
        if (band.bandActivity == BandActivity.Cocooned) return changed;
        if (band.winded)
        {
            if (!wasWinded) band.bandActivity = BandActivity.Recovering;
            return changed;
        }
        if (band.decisionLeft <= 0f)
        {
            band.decisionLeft = WorldPursuit.DecisionSevenths;
            var before = band.bandActivity;
            if (!DecideEncounter(band, spec, notice)) return true; // it left the map
            changed |= before != band.bandActivity;
        }
        if (!band.Moving) return changed;
        // The ground is weighed on every step, not only when the way was planned: a river it will not cross stops it,
        // and what lives in the water never takes a step out of it.
        var grid = MicroNavigation.Grid(Map, gen);
        int at = MicroNavigation.Index(Map, WorldUnits.MicroPosition(band));
        int next = MicroNavigation.Index(Map, band.path[0]);
        if (next < 0 || at < 0 || !WorldPursuit.SafeStep(Map, grid, band, at, next, band.bandActivity == BandActivity.Fleeing))
        {
            WorldUnits.Stop(band);
            return true;
        }
        var step = WorldUnits.Move(Map, gen, band, spec, time, h => Map.Units.Any(u => u != band && !u.Missing && WorldUnits.MicroPosition(u) == h),
            (a, b) => WorldPursuit.StepCost(Map, grid, band, a, b));
        if (band.bandActivity == BandActivity.Pursuing || band.bandActivity == BandActivity.Searching) band.chaseTiles += step.entered.Count;
        if (band.denLife < 0f && AtHome(band))
        {
            RemoveFromMap(band);
            return true;
        }
        return changed || step.entered.Count > 0 || step.blocked || step.halted;
    }

    // Decides what the band does now; false when it left the map.
    private bool DecideEncounter(WorldUnit band, UnitSpec spec, List<string> notice)
    {
        var gen = Settings.generation;
        var from = WorldUnits.MicroPosition(band);
        var traits = band.identity == BandIdentity.Atonalis ? AtonalPaths.Of(band.atonalPath) : null;
        if (band.denLife < 0f) return GoHome(band, vanish: true);
        if (band.truce > 0f)
        {
            // After a battle both sides part and draw breath.
            WorldUnits.Stop(band);
            band.quarryId = -1;
            band.bandActivity = BandActivity.Recovering;
            return true;
        }
        if (band.bandActivity == BandActivity.Feeding && band.identity != BandIdentity.FormlessMass && band.sated > WorldPursuit.SatedSevenths - 1f) return true;

        int sight = Math.Max(1, spec.sight) + WorldPursuit.SightBonus(band.intelligence);
        var nearby = Map.Units.Where(u => u != band && WorldBattles.Fights(u) && HexCoord.Distance(from, WorldUnits.MicroPosition(u)) <= sight)
            .OrderBy(u => HexCoord.Distance(from, WorldUnits.MicroPosition(u))).ThenBy(u => u.id).ToList();

        // A Formless Mass creeps to suffering and drinks it, unless a party in distress draws it.
        if (band.identity == BandIdentity.FormlessMass && DecideMass(band, nearby)) return true;

        // 1. Flee what it fears.
        var danger = Danger(band, nearby, from, traits);
        if (danger != null)
        {
            if (band.bandActivity != BandActivity.Fleeing && WorldBattles.IsPlayers(danger) && band.stance != EnemyStance.Timid)
                notice.Add($"{band.name} {(band.intelligence == BandIntelligence.Smart ? "size up" : "shy from")} {danger.name} and fall back.");
            band.quarryId = -1;
            band.lastSeen = WorldUnits.MicroPosition(danger);
            band.memoryLeft = WorldPursuit.MemoryOf(band);
            band.bandActivity = BandActivity.Fleeing;
            var escape = WorldPursuit.Escape(Map, gen, band, band.lastSeen);
            if (escape.Count > 0) WorldUnits.Order(Map, band, escape);
            else WorldUnits.Stop(band); // cornered: it stands and fights whoever catches it
            return true;
        }
        if (band.bandActivity == BandActivity.Fleeing)
        {
            // Out of sight of what it fled: it runs on a little, then catches its breath.
            if (band.Moving && band.memoryLeft > 0f) return true;
            WorldUnits.Stop(band);
            band.bandActivity = BandActivity.Recovering;
            return true;
        }
        // 2. Home after a chase.
        if (band.bandActivity == BandActivity.Returning) return GoHome(band, vanish: false);

        // 3. A quarry: the one it is after, a Violux's old victim, else the nearest it wants.
        var quarry = nearby.FirstOrDefault(u => u.id == band.quarryId && u.truce <= 0f);
        if (quarry == null && band.grudge > 0) quarry = nearby.FirstOrDefault(u => u.id == band.grudge - 1 && u.truce <= 0f && Wants(band, u, traits, true));
        if (quarry == null && band.quarryId < 0 && band.chaseCooldown <= 0f)
            quarry = nearby.FirstOrDefault(u => u.truce <= 0f && Wants(band, u, traits, false));
        if (quarry != null)
        {
            Chase(band, quarry, traits, notice);
            return true;
        }
        if (band.quarryId >= 0)
        {
            // Lost from sight: it looks where it saw it last, while it remembers.
            if (band.memoryLeft <= 0f || WorldPursuit.Leash(Map, band, band.lastSeen) != null || HexCoord.Distance(from, band.lastSeen) == 0 ||
                !WorldPursuit.Route(Map, gen, band, band.lastSeen, false, out var search) || search.Count == 0)
            {
                GiveUp(band, UnitById(band.quarryId), null, traits, notice);
                return true;
            }
            band.bandActivity = BandActivity.Searching;
            WorldUnits.Order(Map, band, search);
            return true;
        }
        // 4. Roam its ground.
        Roam(band, traits);
        return true;
    }

    // What the band runs from: a hungry predator of its kind; one of your parties if it is timid (as soon as it sees one
    // when it flees on sight, else when it comes close or gives chase); a party a Smart band would lose to, or a party of
    // more than one an Erosyx will not face, coming close.
    private WorldUnit Danger(WorldUnit band, List<WorldUnit> nearby, HexCoord from, PathTraits traits)
    {
        var gen = Settings.generation;
        foreach (var u in nearby)
        {
            if (WorldPursuit.Predates(gen, u, band) && u.sated <= 0f) return u;
            if (!WorldBattles.IsPlayers(u)) continue;
            int d = HexCoord.Distance(from, WorldUnits.MicroPosition(u));
            if (band.stance == EnemyStance.Timid && (band.response == ThreatResponse.FleesOnSight || d <= WorldPursuit.FleeReach || WorldBattles.Hunted(u, band)))
                return u;
            if (d > WorldPursuit.FleeReach) continue;
            if (traits != null && traits.soloOnly && Expeditions.PartySize(u) > 1) return u;
            if (band.intelligence == BandIntelligence.Smart && Power(band) < Power(u) * WorldPursuit.SmartOdds) return u;
        }
        return null;
    }

    // Whom it goes after: your parties while it is angry (an Erosyx only one who travels alone; a Violux its old victim
    // whenever it sees it), its prey while it is hungry.
    private bool Wants(WorldUnit band, WorldUnit other, PathTraits traits, bool grudge)
    {
        if (!WorldBattles.IsPlayers(other)) return band.sated <= 0f && WorldPursuit.Predates(Settings.generation, band, other);
        if (!grudge && !WorldBattles.Angry(band)) return false;
        return traits == null || !traits.soloOnly || Expeditions.PartySize(other) <= 1;
    }

    private void Chase(WorldUnit band, WorldUnit quarry, PathTraits traits, List<string> notice)
    {
        var gen = Settings.generation;
        var target = WorldUnits.MicroPosition(quarry);
        bool yours = WorldBattles.IsPlayers(quarry);
        string leash = WorldPursuit.Leash(Map, band, target);
        if (leash != null) { GiveUp(band, quarry, leash, traits, notice); return; }
        if (!WorldPursuit.Route(Map, gen, band, target, true, out var chase))
        {
            // What lives in the water will not leave it; a thinking band will not follow onto ground it keeps off (the far
            // bank of a river); a beast just loses the way.
            string why = band.habitat == CreatureHabitat.Water ? "will not leave the water after you"
                : band.intelligence != BandIntelligence.Instinctive && WorldPursuit.Route(Map, gen, band, target, true, out _, fleeing: true) ? "will not follow you across the river"
                : "loses the way to you";
            GiveUp(band, quarry, why, traits, notice);
            return;
        }
        band.quarryId = quarry.id;
        band.lastSeen = target;
        band.memoryLeft = WorldPursuit.MemoryOf(band);
        if (yours && band.intelligence == BandIntelligence.Smart && chase.Count > 0)
        {
            // A Smart band will not attack onto ground that favours you (across a river, uphill, in cover, in a
            // settlement): it keeps two hexes off and waits, and the waiting wears on its patience (its leash).
            var approach = MicroNavigation.Coord(Map, chase[chase.Count - 1]);
            var field = WorldBattles.Field(Map, gen, Combat, approach, target, GameAge.Number, 0);
            if (field.riverCrossing || field.height > 0.06f || field.settlement || field.concealed)
            {
                if (band.bandActivity != BandActivity.Stalking) notice.Add($"{band.name} will not attack {quarry.name} on that ground and stalk it instead.");
                band.bandActivity = BandActivity.Stalking;
                band.chaseTiles++;
                var kept = WorldPursuit.KeepOff(Map, chase, target, 2);
                if (kept.Count > 0) WorldUnits.Order(Map, band, kept);
                else WorldUnits.Stop(band);
                return;
            }
        }
        if (band.bandActivity != BandActivity.Pursuing && yours) notice.Add($"{band.name} give chase to {quarry.name}!");
        band.bandActivity = BandActivity.Pursuing;
        // A Signath moves across contradictory truths: now and then its step goes astray.
        if (traits != null && traits.erratic > 0f && chase.Count > 1 && WorldNoise.Hash01(Map.seed ^ 0x5197, band.id, band.mishapRolls++) < traits.erratic)
        {
            var astray = Astray(band);
            if (astray >= 0) { WorldUnits.Order(Map, band, new List<int> { astray }); return; }
        }
        if (chase.Count > 0) WorldUnits.Order(Map, band, chase);
        else WorldUnits.Stop(band); // beside it: the battle comes next
    }

    // A hex beside the band it can step onto, drawn by its rolls, or -1.
    private int Astray(WorldUnit band)
    {
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        int at = MicroNavigation.Index(Map, WorldUnits.MicroPosition(band));
        if (at < 0) return -1;
        int first = (int)(WorldNoise.Hash01(Map.seed ^ 0x2a57, band.id, band.mishapRolls++) * 6);
        for (int i = 0; i < 6; i++)
        {
            int n = MicroNavigation.Neighbour(Map, at, (first + i) % 6);
            if (n >= 0 && WorldPursuit.SafeStep(Map, grid, band, at, n) && !Map.Units.Any(u => u != band && MicroNavigation.Index(Map, WorldUnits.MicroPosition(u)) == n)) return n;
        }
        return -1;
    }

    // It breaks off the chase (told when the quarry was yours), calms down and heads home. A Violux remembers.
    private void GiveUp(WorldUnit band, WorldUnit quarry, string why, PathTraits traits, List<string> notice)
    {
        if (quarry != null && why != null && WorldBattles.IsPlayers(quarry)) notice.Add($"{band.name} {why}.");
        if (quarry != null && traits != null && traits.grudge && WorldBattles.IsPlayers(quarry)) band.grudge = quarry.id + 1;
        WorldUnits.Stop(band);
        band.quarryId = -1;
        band.memoryLeft = 0f;
        band.provoked = 0f;
        band.chaseCooldown = WorldPursuit.ChaseCooldown;
        band.bandActivity = BandActivity.Returning;
    }

    private HexCoord Home(WorldUnit band) => band.homeHexSet ? band.homeHex
        : band.homeCell >= 0 && band.homeCell < Map.Count ? MicroNavigation.Center(Map[band.homeCell].coord) : WorldUnits.MicroPosition(band);

    private bool AtHome(WorldUnit band) => HexCoord.Distance(WorldUnits.MicroPosition(band), Home(band)) <= 1;

    // Walks home; there it roams again (its chase forgotten), or, a den's visitor, is gone. False when it left the map.
    private bool GoHome(WorldUnit band, bool vanish)
    {
        band.bandActivity = BandActivity.Returning;
        if (AtHome(band))
        {
            if (vanish) { RemoveFromMap(band); return false; }
            WorldUnits.Stop(band);
            band.chaseTiles = 0;
            band.bandActivity = BandActivity.Roaming;
            return true;
        }
        if (band.Moving && HexCoord.Distance(band.path[band.path.Count - 1], Home(band)) <= 1) return true;
        if (WorldPursuit.Route(Map, Settings.generation, band, Home(band), true, out var back) && back.Count > 0)
        {
            WorldUnits.Order(Map, band, back);
            return true;
        }
        // No way home: a visitor slips away; anyone else makes this its home.
        if (vanish) { RemoveFromMap(band); return false; }
        WorldUnits.Stop(band);
        band.homeCell = Map.Get(band.coord)?.index ?? band.homeCell;
        band.homeHex = WorldUnits.MicroPosition(band);
        band.homeHexSet = true;
        band.chaseTiles = 0;
        band.bandActivity = BandActivity.Roaming;
        return true;
    }

    // Now and then it wanders to another hex of its territory it can stand on (an Obsessian walks its loop of three
    // places over and over).
    private void Roam(WorldUnit band, PathTraits traits)
    {
        band.bandActivity = BandActivity.Roaming;
        if (band.Moving) return;
        var home = Home(band);
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        var spots = HexCoord.Spiral(home, Math.Max(1, band.territoryRadius)).Where(h => WorldPursuit.CanStand(Map, grid, band, MicroNavigation.Index(Map, h))).ToList();
        if (spots.Count == 0) return;
        // An Atonalis follows the scent of its hunger (the pleasure parasites drift toward revels, a Carnalix toward a
        // slaughter ground, a Discant toward a festival's joy), and settles where it finds it.
        if (band.identity == BandIdentity.Atonalis && band.atonalPath != AtonalPath.None && WorldNoise.Hash01(Map.seed ^ 0x5ce7, band.id, band.mishapRolls++) < 0.3)
        {
            var scent = Scented(band);
            if (scent != null && scent.coord != band.coord &&
                WorldPursuit.Route(Map, Settings.generation, band, MicroNavigation.Center(scent.coord), true, out var drawn, 384) && drawn.Count > 0)
            {
                WorldUnits.Order(Map, band, drawn);
                band.homeCell = scent.index;
                band.homeHex = MicroNavigation.Center(scent.coord);
                band.homeHexSet = true;
                return;
            }
        }
        int pick;
        if (traits != null && traits.loops)
        {
            int stop = band.mishapRolls++ % 3;
            pick = Math.Min(spots.Count - 1, (int)(WorldNoise.Hash01(Map.seed ^ 0x0b5e, band.id, stop) * spots.Count));
        }
        else
        {
            // Rolled at the decision cadence (about once in eight decisions), not at the frame rate.
            if (WorldNoise.Hash01(Map.seed ^ 0x2c9f, band.id, band.mishapRolls++) >= 0.12) return;
            pick = Math.Min(spots.Count - 1, (int)(WorldNoise.Hash01(Map.seed ^ 0x7a1, band.id, band.mishapRolls) * spots.Count));
        }
        if (WorldPursuit.Route(Map, Settings.generation, band, spots[pick], false, out var roam, 256) && roam.Count > 0) WorldUnits.Order(Map, band, roam);
    }

    /// <summary>
    /// A unit's rough strength for a Smart band weighing a fight: its sides' Integrity and half its Composure, by how
    /// hard they hit and hold, less for how spent it is.
    /// </summary>
    public float Power(WorldUnit unit)
    {
        var side = SideOf(unit);
        if (side == null) return 0f;
        float power = side.sections.Sum(s => (Math.Max(0f, s.integrity) + 0.5f * Math.Max(0f, s.composure)) * (1f + (s.attack + s.defense) / 20f));
        return power * (1f - 0.5f * WorldPursuit.Tiredness(unit));
    }

    // ===== FORMLESS MASSES =====

    // A party whose nerve is shaken or whose legends are Spiraling: its disrupted emotions are raw Consciousness to a mass.
    private static bool Distressed(WorldUnit unit)
    {
        if (!WorldBattles.IsPlayers(unit)) return false;
        if (unit.nerveLost >= 0.3f) return true;
        var legends = LegendProgress.Instance;
        return legends != null && Expeditions.Members(unit).Any(n => legends.Composure(n) >= ComposureState.Spiraling);
    }

    // A mass's mind (true when it decided): drawn to a party in distress (it turns on it), else it drinks where the land
    // suffers, else creeps to the worst suffering within four cells.
    private bool DecideMass(WorldUnit band, List<WorldUnit> nearby)
    {
        if (band.bandActivity == BandActivity.Cocooned) { WorldUnits.Stop(band); return true; }
        var drawn = nearby.FirstOrDefault(Distressed);
        if (drawn != null && band.truce <= 0f) band.provoked = WorldBattles.ProvokedSevenths;
        if (WorldBattles.Angry(band)) return false;
        var here = Map.Get(band.coord);
        float Pressure(WorldTile t) => WorldSuffering.Pressure(t, t.suffering);
        if (here != null && Pressure(here) >= 0.05f)
        {
            WorldUnits.Stop(band);
            band.bandActivity = BandActivity.Feeding;
            return true;
        }
        band.bandActivity = BandActivity.Roaming;
        if (band.Moving) return true;
        var best = HexCoord.Spiral(band.coord, 4).Select(Map.Get).Where(t => t != null && t != here && MicroNavigation.Walkable(t, Settings.generation))
            .OrderByDescending(Pressure).ThenBy(t => t.index).FirstOrDefault();
        if (best == null || Pressure(best) < 0.05f) return false;
        if (WorldPursuit.Route(Map, Settings.generation, band, MicroNavigation.Center(best.coord), true, out var way, 256) && way.Count > 0) WorldUnits.Order(Map, band, way);
        return true;
    }

    // A mass drinks the imprint of the cell it feeds on, every feeling in it (and the Doubt the torn Loom gives off); its
    // emotional meter fills, and fed enough it cocoons, and the cocoon hatches into the Path its feeding most resembles.
    private void FeedMasses(float time, List<string> notice)
    {
        foreach (var mass in Map.Units.Where(u => u.identity == BandIdentity.FormlessMass && !WorldBattles.IsPlayers(u)).ToList())
        {
            var cell = Map.Get(mass.coord);
            if (cell == null) continue;
            mass.fed = mass.fed ?? new EmotionalRegister();
            if (mass.bandActivity == BandActivity.Cocooned)
            {
                mass.cocoon -= time;
                if (mass.cocoon <= 0f) Hatch(mass, notice);
                continue;
            }
            if (mass.bandActivity != BandActivity.Feeding) continue;
            var scar = WorldSuffering.At(_suffering, cell.index);
            if (scar != null) scar.feelings.Drain(Math.Min(1f, WorldSuffering.DrainShare * time * Math.Max(1, mass.creatures)), mass.fed);
            float loom = WorldSuffering.LoomDoubt(cell);
            if (loom > 0f) mass.fed.Add(Feeling.Doubt, loom * WorldSuffering.DissonanceFeed * time);
            if (mass.fed.Total < WorldSuffering.CocoonAt) continue;
            WorldUnits.Stop(mass);
            mass.bandActivity = BandActivity.Cocooned;
            mass.cocoon = WorldSuffering.CocoonSevenths;
            mass.name = $"Dissonance Cocoon ({mass.creatures})";
            var (path, hybrid) = EmotionalProfile.Match(mass.fed);
            if (Sees(mass))
                notice.Add($"A Formless Mass at {Place(cell)}, swollen with {mass.fed.Words(2)}, wraps itself in a Dissonance cocoon: a {WorldSuffering.PathName(path, hybrid)} stirs inside. Destroy it before it hatches.");
        }
    }

    // The cocoon bursts: a Nascent Atonalis of the Path its feeding decided (a hybrid when two came close). It keeps what
    // it was born of (its meter), for whoever reads its card.
    private void Hatch(WorldUnit mass, List<string> notice)
    {
        var gen = Settings.generation;
        var hatchling = WorldSuffering.Hatchling;
        var (path, hybrid) = EmotionalProfile.Match(mass.fed);
        int count = Math.Max(1, (int)Math.Round(mass.fed.Total));
        mass.species = null;
        mass.threat = hatchling.id;
        mass.binding = WorldBattles.DrawBinding(hatchling, WorldNoise.Hash01(Map.seed ^ 0x6e3, mass.id, mass.mishapRolls++));
        mass.atonalPath = path;
        mass.hybridPath = hybrid;
        mass.stance = EnemyStance.Hostile;
        mass.creatures = count;
        mass.cocoon = 0f;
        mass.stride = 0f;
        mass.bandActivity = BandActivity.Roaming;
        mass.encounterInitialized = false;
        WorldPursuit.Initialize(gen, mass);
        Rename(mass);
        var cell = Map.Get(mass.coord);
        string line = $"A Dissonance cocoon bursts at {Place(cell)} in a wave of Discordant Interference: {mass.name} is born of {mass.fed.Words(2)}.";
        if (Sees(mass) || cell != null && cell.known) notice.Add(line);
        NotificationFeed.Push("An Atonalis is born", line + $" It walks the Path of {path}: {AtonalPaths.Of(path).summary}", NotificationFeed.Topic.World, key: $"hatch:{mass.id}");
    }

    // ===== THE EMOTIONAL REGISTER OF THE LAND =====

    /// <summary>A feeling imprinted on a cell (a battle, a hunt, a kill, a loss, a people's life).</summary>
    public void AddSuffering(int cell, Feeling feeling, float amount)
    {
        if (Map == null || cell < 0 || cell >= Map.Count) return;
        _suffering = _suffering ?? new List<SufferingScar>();
        var scar = WorldSuffering.Add(_suffering, cell, feeling, amount);
        if (scar == null) return;
        var t = Map[cell];
        (t.imprint = t.imprint ?? new EmotionalRegister()).Add(feeling, amount);
        t.suffering = WorldSuffering.SufferingOf(t.imprint);
    }

    // What your people feel this moment, settlement by settlement: the realm's happiness, joy and health reach every one
    // (the Capital most); each one's own strain by what strains it; a revel's district. Given off into its cell.
    private void MeasureFeelings()
    {
        var culture = CultureSystem.Instance;
        var health = PopulationHealth.Instance;
        foreach (var s in Map.Settlements)
        {
            var mood = new SettlementMood
            {
                strain = s.strain, harmedBy = s.harmedBy, district = s.district,
                happiness = culture != null ? culture.Happiness : -1f, joy = culture != null ? culture.Joy : 0f,
                share = s.kind == SettlementKind.Capital ? 1f : s.kind == SettlementKind.Major ? 0.6f : 0.35f,
                development = s.development, holidays = culture != null ? culture.Holidays.Count : 0,
            };
            if (health != null)
            {
                mood.nutrition = health.Level(HealthPressure.Nutrition);
                mood.disease = health.Level(HealthPressure.DiseaseBurden);
                mood.sanitation = health.Level(HealthPressure.Sanitation);
                mood.exposure = health.Level(HealthPressure.Exposure);
                mood.harmonic = health.Level(HealthPressure.HarmonicStability);
            }
            s.feelings = WorldSuffering.SettlementFeelings(mood);
        }
    }

    // A festival imprints its joy and love where it is held (joy is what a Discant preys on: a mass gorged on it hatches one).
    private void OnFestival(Settlement s)
    {
        var t = s != null ? Map?.Get(s.coord) : null;
        if (t == null) return;
        AddSuffering(t.index, Feeling.Joy, WorldSuffering.FestivalJoy);
        AddSuffering(t.index, Feeling.Love, WorldSuffering.FestivalJoy * 0.4f);
    }

    // A holiday kept imprints devotion on the Capital's ground.
    private void OnHolidayKept(Holiday _)
    {
        var t = Map?.Get(Map.Capital);
        if (t != null) AddSuffering(t.index, Feeling.Devotion, WorldSuffering.HolidayDevotion);
    }
    private bool _festivalHooked;

    // The land's emotional year: every imprint fades a little; your people give off what they feel; the Eleos Blooms
    // drink what they catalogue; the masses feed, cocoon and hatch; where the imprint or the Loom's Dissonance runs too
    // high and nothing drinks it, a mass pools (on your own land too, if your people suffer).
    private bool TickSuffering(float time, List<string> notice)
    {
        _suffering = _suffering ?? new List<SufferingScar>();
        WorldSuffering.Fade(_suffering, time);
        MeasureFeelings();
        foreach (var s in Map.Settlements)
            if (Map.Get(s.coord) is WorldTile home) WorldSuffering.Add(_suffering, home.index, s.feelings, WorldSuffering.EmitPerSeventh * time);
        DrinkBlooms(time);
        FeedAtonalis(time);
        FeedMasses(time, notice);
        WorldSuffering.Apply(Map, _suffering);
        WarnImprints(notice);
        bool changed = false;
        var masses = Map.Units.Where(u => u.identity == BandIdentity.FormlessMass).ToList();
        // Masses pooled by the torn Loom alone respect the cap; real suffering can pool beyond it (up to twice), and your own
        // land and the heaviest imprint come first, so the wild's Dissonance never crowds out what your people poured out.
        if (masses.Count >= WorldSuffering.MassCap * 2) return false;
        foreach (var timer in _threatTimers.Where(t => t.threat == MassTimer)) timer.sevenths = Math.Max(0f, timer.sevenths - time);
        var sites = WorldSuffering.SpawnSites(Map, Settings.generation,
            cell => _threatTimers.Any(t => t.threat == MassTimer && t.cell == cell && t.sevenths > 0f), masses.Select(m => m.coord))
            .OrderByDescending(t => WorldAuthority.IsPlayers(t.authorityId)).ThenByDescending(t => t.suffering).ThenByDescending(t => WorldSuffering.Pressure(t, t.suffering)).ThenBy(t => t.index).ToList();
        int risen = 0, count0 = masses.Count;
        foreach (var t in sites)
        {
            if (risen >= 2) break;
            if (count0 + risen >= WorldSuffering.MassCap && t.suffering < WorldSuffering.SpawnPressure) continue;
            var hex = FreeHexNear(MicroNavigation.Center(t.coord));
            if (!hex.HasValue) continue;
            float pressure = WorldSuffering.Pressure(t, t.suffering);
            int count = 1 + (pressure > 1f ? 1 : 0) + (pressure > 1.5f ? 1 : 0);
            var mass = SpawnBand(WorldSuffering.MassId, count, hex.Value, EnemyStance.Wary, homeCell: t.index);
            if (mass == null) continue;
            var timer = _threatTimers.FirstOrDefault(x => x.threat == MassTimer && x.cell == t.index);
            if (timer == null) _threatTimers.Add(timer = new ThreatTimer { threat = MassTimer, cell = t.index });
            risen++;
            timer.sevenths = WorldSuffering.SpawnCooldown;
            var feelings = WorldSuffering.Feelings(t);
            if (WorldAuthority.IsPlayers(t.authorityId))
            {
                string line = $"The {feelings.Words(2)} your people have poured into {Place(t)} pools into a Formless Mass. Plant blooms that drink it, ease what hurts them, or destroy the mass before it cocoons.";
                notice.Add(line);
                NotificationFeed.Push("A Formless Mass on your land", line, NotificationFeed.Topic.World, key: $"mass:{mass.id}");
            }
            else if (Sees(mass)) notice.Add($"The lingering {feelings.Words(1)} at {Place(t)} pools into a Formless Mass.");
            changed = true;
        }
        return changed;
    }

    // Every Eleos Bloom that is not withering drinks the feelings it catalogues out of the imprint of its own cells and the
    // cells beside them (a healer more): the region's emotional filter, and the masses' rival for the same food.
    private void DrinkBlooms(float time)
    {
        var gen = Settings.generation;
        foreach (var site in Map.ResourceSites)
        {
            var spec = gen.ResourceSite(site.spec);
            var palate = spec != null && spec.kind == ResourceKind.Bloom ? WorldResources.PalateOf(site, spec) : null;
            if (palate == null || WorldResources.Withering(site, gen)) continue;
            var cells = site.cells.SelectMany(c => HexCoord.Spiral(Map[c].coord, 1)).Select(Map.Get).Where(t => t != null).Select(t => t.index).Distinct();
            // It drinks its own cocktail as it has evolved, as strongly as it thrives (a superloaded palate drinks deep).
            WorldSuffering.Drink(_suffering, cells, palate.recipe, spec.niche == BloomNiche.Healer, site.vigor * (site.potency > 0f ? site.potency : 1f), time);
        }
    }

    // Every Atonalis feeds where it stands: it drinks its Path's hunger out of the land's imprint, and the healthy notes it
    // preys on turn into their wound as it does (WorldSuffering.Prey).
    private void FeedAtonalis(float time)
    {
        foreach (var band in Map.Units.Where(u => u.identity == BandIdentity.Atonalis && u.atonalPath != AtonalPath.None && !u.Missing))
        {
            var cell = Map.Get(band.coord);
            var scar = cell != null ? WorldSuffering.At(_suffering, cell.index) : null;
            if (scar != null) WorldSuffering.Prey(scar, band.atonalPath, band.hybridPath, time);
        }
    }

    // How strongly a place calls an Atonalis: how closely its feelings match the Path's hunger, times how much of it there is.
    private float Scent(WorldTile t, EmotionalRegister hunger)
    {
        var feel = WorldSuffering.Feelings(t);
        return EmotionalAlchemy.Cosine(feel, hunger) * EmotionalAlchemy.Loose(feel, EmotionalRegister.All.Where(f => hunger[f] > 0f).Select(f => new FeelingWeight { feeling = f, weight = hunger[f] }));
    }

    // The cell within reach whose feelings call the Atonalis most (the pleasure parasites to revels, a Carnalix to a
    // slaughter ground), when anything calls at all.
    private WorldTile Scented(WorldUnit band, int reach = 6)
    {
        var hunger = EmotionalProfile.Of(band.atonalPath, band.hybridPath);
        WorldTile best = null;
        float most = 0.05f;
        foreach (var coord in HexCoord.Spiral(band.coord, reach))
        {
            var t = Map.Get(coord);
            if (t == null || !MicroNavigation.Walkable(t, Settings.generation)) continue;
            float s = Scent(t, hunger);
            if (s > most) { most = s; best = t; }
        }
        return best;
    }

    // ===== EVOLUTION =====

    /// <summary>A bloom lineage took a step at an Echo: (the site, what changed). The open end for other systems.</summary>
    public event Action<ResourceSite, LineageEvent> LineageEvolved;

    /// <summary>The Eleos Bloom lineages on the map (a site's own, or null while it eats as its kind first did).</summary>
    public IEnumerable<(ResourceSite site, Lineage lineage)> Lineages =>
        Map == null ? Enumerable.Empty<(ResourceSite, Lineage)>() : Map.ResourceSites.Where(s => s.lineage != null).Select(s => (s, s.lineage));

    /// <summary>
    /// A generation passes for every Eleos Bloom that eats a cocktail (<see cref="EmotionalEvolution.Evolve"/>): it is fed by
    /// the feelings over its cells, and may specialize, generalize, become a variety of its own or revert. Called each Echo;
    /// returns the lines to tell (new varieties and reversions of blooms your people have identified).
    /// </summary>
    public List<string> EvolveBlooms()
    {
        var lines = new List<string>();
        if (Map == null) return lines;
        var gen = Settings.generation;
        var eleos = gen.eleos ?? new EleosSettings();
        foreach (var site in Map.ResourceSites)
        {
            var spec = gen.ResourceSite(site.spec);
            if (spec == null || spec.kind != ResourceKind.Bloom || spec.sprouts || spec.flavors == null || spec.flavors.Count == 0) continue;
            site.lineage = site.lineage ?? EmotionalEvolution.Seed(spec.id, spec.flavors, spec.specificity);
            double roll = WorldNoise.Hash01(WorldNoise.Stream(Map.seed, "bloom-evolution"), site.center, _echoesGrown);
            var step = EmotionalEvolution.Evolve(site.lineage, WorldResources.FeelingsOver(Map, site.cells), spec.residueNeed, eleos.ambientResidue, roll, spec.name, eleos.evolution);
            if (step == null) continue;
            GameLog.Event($"{spec.name} at {Place(Map[site.center])}: {step.change} (generation {step.generation}) {step.from} -> {step.to}", Log);
            LineageEvolved?.Invoke(site, step);
            if (!WorldResources.Identified(Map, site)) continue;
            if (step.change == LineageChange.Diverged)
                lines.Add($"The {spec.name} at {Place(Map[site.center])} have drunk this land's feelings for {step.generation} generations: they are {step.variety} now ({EmotionalAlchemy.Words(site.lineage.palate.recipe)}).");
            else if (step.change == LineageChange.Reverted)
                lines.Add($"The {step.variety} at {Place(Map[site.center])} have turned back into plain {spec.name}.");
        }
        return lines;
    }

    // Tell when the imprint on one of your settlements' cells grows heavy (once, until it eases), naming what it is made of
    // and the blooms that would drink it.
    private void WarnImprints(List<string> notice)
    {
        foreach (var s in Map.Settlements)
        {
            var t = Map.Get(s.coord);
            if (t == null) continue;
            float pressure = WorldSuffering.Pressure(t, t.suffering);
            if (s.imprintWarned) { if (pressure < WorldSuffering.WarnImprint * 0.6f) s.imprintWarned = false; continue; }
            if (pressure < WorldSuffering.WarnImprint) continue;
            s.imprintWarned = true;
            var dominant = (t.imprint ?? new EmotionalRegister()).Dominant;
            var drinkers = Settings.generation.resourceSites.Where(r => r != null && r.kind == ResourceKind.Bloom && r.flavors != null && r.flavors.Any(f => f.feeling == dominant))
                .Select(r => r.name).Take(2).ToList();
            string line = $"The ground of {s.name} grows heavy with {t.imprint.Words(2)}. Left undrunk, it will pool into Formless Masses{(drinkers.Count > 0 ? $"; {string.Join(" or ", drinkers)} would drink its {dominant}" : string.Empty)}.";
            notice.Add(line);
            NotificationFeed.Push($"{s.name}'s ground grows heavy", line, NotificationFeed.Topic.World, key: $"imprint:{s.id}");
        }
    }

    private const string MassTimer = "mass";

    // ===== THE LIVING MAP'S SLOWER CLOCK =====

    // A few times a Seventh: the land's feelings move, the hunters' signs fade and warn your explorers, and legends held
    // by a band that is gone go free.
    private bool TickEcosystem(float time, List<string> notice)
    {
        if (Map == null) return false;
        if (!_festivalHooked && CultureSystem.Instance != null)
        {
            CultureSystem.Instance.FestivalHeld += OnFestival;
            CultureSystem.Instance.HolidayKept += OnHolidayKept;
            _festivalHooked = true;
        }
        _ecoClock += time;
        if (_ecoClock < EcoTick) return false;
        time = _ecoClock;
        _ecoClock = 0f;
        _ecoTime += time;
        FeelingsVersion++;
        if (_threatTimers == null) _threatTimers = new List<ThreatTimer>();
        bool changed = TickSuffering(time, notice);
        TickSigns(time, notice);
        var progress = LegendProgress.Instance;
        if (progress != null)
            foreach (string legend in progress.CaptiveNames.ToList())
            {
                string key = progress.CaptorOf(legend);
                if (key != null && key.StartsWith("band:", StringComparison.Ordinal) && int.TryParse(key.Substring(5), out int id) && UnitById(id) == null)
                    progress.Free(legend, 2, null);
            }
        return changed;
    }

    // ===== SIGNS: THE ONLY WARNING =====

    // Whether a band leaves the signs of a hunter: an angry or chasing band, a hungry predator, an Atonalis, a mass.
    private bool Hunting(WorldUnit band) =>
        band.identity == BandIdentity.Atonalis || band.identity == BandIdentity.FormlessMass || WorldBattles.Angry(band)
        || band.bandActivity == BandActivity.Pursuing || band.bandActivity == BandActivity.Stalking || band.bandActivity == BandActivity.Searching
        || band.sated <= 0f && (WorldPursuit.Species(Settings.generation, band)?.prey?.Count ?? 0) > 0;

    private void LeaveSigns(WorldUnit band, float time)
    {
        var cell = Map.Get(band.coord);
        if (cell == null) return;
        if (!_signs.TryGetValue(cell.index, out var sign)) _signs[cell.index] = sign = new Sign();
        sign.fresh = Math.Min(1f, sign.fresh + SignsPerSeventh * time);
        sign.band = band.id;
    }

    private void TickSigns(float time, List<string> notice)
    {
        foreach (var pair in _signs.ToList())
        {
            pair.Value.fresh -= SignsFade * time;
            if (pair.Value.fresh > 0.01f) continue;
            _signs.Remove(pair.Key);
            if (pair.Key < Map.Count) Map[pair.Key].signs = 0f;
        }
        foreach (var pair in _signs) if (pair.Key < Map.Count) Map[pair.Key].signs = pair.Value.fresh;
        // Your explorers read fresh signs of a hunter they cannot see: the warning that something hunts nearby.
        foreach (var unit in Map.Units.Where(u => WorldBattles.IsPlayers(u) && !u.Missing))
        {
            var spec = SpecOf(unit);
            if (spec == null) continue;
            var at = WorldUnits.MicroPosition(unit);
            int reach = WorldUnits.Sight(Map, unit, spec, Rules) + 3;
            foreach (var pair in _signs)
            {
                var sign = pair.Value;
                if (sign.fresh < SignsWarn || sign.warnedUntil > _ecoTime || pair.Key >= Map.Count) continue;
                var cell = Map[pair.Key];
                if (HexCoord.Distance(at, MicroNavigation.Center(cell.coord)) > reach) continue;
                var band = UnitById(sign.band);
                if (band != null && Sees(band)) continue;
                sign.warnedUntil = _ecoTime + 4f;
                notice.Add($"{unit.name} finds {ClueOf(band)} near {Place(cell)}: something is hunting nearby.");
            }
        }
    }

    // What an explorer finds of a hunter it cannot see: its kind's signs (named once your people know the species).
    private string ClueOf(WorldUnit band)
    {
        if (band == null) return "fresh signs of a hunt";
        switch (band.identity)
        {
            case BandIdentity.FormlessMass: return "a trail of dark red slime that still pulses";
            case BandIdentity.Atonalis:
                return band.atonalPath == AtonalPath.Carnalix ? "gnawed remains, and a static hum in the air"
                    : band.atonalPath == AtonalPath.Signath ? "tracks that begin and end nowhere, and a static hum"
                    : "a static hum, and husks drained of their warmth";
            case BandIdentity.Human: case BandIdentity.Demihuman: case BandIdentity.Humanoid: return "cold fires and fresh prints";
        }
        var species = WorldPursuit.Species(Settings.generation, band);
        if (band.habitat == CreatureHabitat.Water) return "a churned, bloodied shallow";
        string named = species != null && Knows(species.id, SpeciesLevel.Identified) ? $"fresh {species.name} tracks" : null;
        string tracks = named ?? (species != null && species.size >= CreatureSize.Large ? "the prints of something large" : band.creatures >= 4 ? "the prints of a pack" : "fresh tracks");
        return (species?.prey?.Count ?? 0) > 0 || WorldBattles.Angry(band) ? tracks + " and a torn carcass" : tracks;
    }

    // ===== PREDATORS AND PREY =====

    // A predator that catches its prey (in contact while giving chase) takes some of it; it rests on the kill, sated (a
    // Carnalix hardly at all), and the rest of the prey scatter. The prey's population in its land shrinks, and the kill
    // leaves suffering. Told when one of your parties is near enough to see it.
    private bool Predation(List<string> notice)
    {
        bool changed = false;
        foreach (var hunter in Map.Units.Where(u => !WorldBattles.IsPlayers(u) && u.quarryId >= 0 && u.sated <= 0f && u.creatures > 0).ToList())
        {
            var prey = UnitById(hunter.quarryId);
            if (prey == null || WorldBattles.IsPlayers(prey) || !WorldPursuit.Predates(Settings.generation, hunter, prey) || !WorldBattles.InContact(hunter, prey)) continue;
            int kills = Math.Max(1, Math.Min(prey.creatures, (int)Math.Round(hunter.creatures * 0.5f)));
            prey.creatures -= kills;
            Cull(prey, kills);
            WorldUnits.Stop(hunter);
            bool drains = hunter.identity == BandIdentity.Atonalis && AtonalPaths.Of(hunter.atonalPath).drains;
            hunter.sated = drains ? 1f : WorldPursuit.SatedSevenths;
            hunter.quarryId = -1;
            hunter.chaseTiles = 0;
            hunter.bandActivity = BandActivity.Feeding;
            var cell = Map.Get(prey.coord);
            if (cell != null) AddSuffering(cell.index, Feeling.Pain, WorldSuffering.PerKill * kills);
            string preyName = WorldPursuit.Species(Settings.generation, prey)?.name ?? prey.name;
            var at = WorldUnits.MicroPosition(prey);
            if (Map.Units.Any(u => WorldBattles.IsPlayers(u) && !u.Missing && HexCoord.Distance(WorldUnits.MicroPosition(u), at) <= 8))
                notice.Add($"{hunter.name} run down {kills} {preyName}.");
            if (prey.creatures <= 0) RemoveFromMap(prey);
            else Rename(prey);
            changed = true;
        }
        return changed;
    }

    // Creatures of a band taken out of its land's population (a hunt, a kill), in groups of its kind.
    private void Cull(WorldUnit band, int individuals)
    {
        if (band == null || individuals <= 0 || string.IsNullOrEmpty(band.species)) return;
        var species = Settings.generation.Species(band.species);
        if (species == null) return;
        var home = band.homeCell >= 0 && band.homeCell < Map.Count ? Map[band.homeCell] : Map.Get(band.coord);
        var pop = home != null ? WorldEcology.Find(Map, home.habitatSlot, species.id) : null;
        if (pop == null) return;
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        int group = species.groupMax > 0 ? species.groupMax : profile != null && profile.groupMax > 0 ? profile.groupMax : 6;
        pop.groups = Math.Max(0f, pop.groups - individuals / (float)Math.Max(1, group));
    }

    // A band's name counts its creatures; an Atonalis is named by its Path ("Nascent Carnalix (2)").
    private void Rename(WorldUnit band)
    {
        string kind = WorldPursuit.Species(Settings.generation, band)?.name ?? band.species;
        if (band.identity == BandIdentity.Atonalis && band.atonalPath != AtonalPath.None && kind != null)
        {
            string path = WorldSuffering.PathName(band.atonalPath, band.hybridPath);
            kind = kind.Contains("Atonalis") ? kind.Replace("Atonalis", path) : $"{kind} ({path})";
        }
        band.name = $"{kind} ({band.creatures})";
    }

    // ===== THE DENS' VISITORS =====

    // Each den you know sends a band out now and then (while its land holds the creatures): it stays out a while, roaming
    // and meeting whoever it meets as its species meets you, then heads home and is gone. One visit per den at a time.
    // What lives in the water rises in the water by its den; nothing rises where it cannot live.
    private bool TickDens(float time)
    {
        if (Map == null) return false;
        _denClock += time;
        if (_denClock < DenTick) return false;
        time = _denClock;
        _denClock = 0f;
        bool changed = false;
        if (_threatTimers == null) _threatTimers = new List<ThreatTimer>();
        int visitors = Map.Units.Count(u => u.denLife != 0f);
        foreach (var site in Map.ResourceSites)
        {
            var species = WorldEcology.SpeciesAt(Settings.generation, site);
            if (species == null || species.denVisitSevenths <= 0f || site.center < 0 || site.center >= Map.Count || !Map[site.center].revealed) continue;
            string key = "den:" + species.id;
            var timer = _threatTimers.FirstOrDefault(t => t.threat == key && t.cell == site.center);
            if (timer == null) _threatTimers.Add(timer = new ThreatTimer { threat = key, cell = site.center, sevenths = 2f });
            if (Map.Units.Any(u => u.species == species.id && u.homeCell == site.center && u.denLife != 0f)) continue;
            timer.sevenths = Math.Max(0f, timer.sevenths - time);
            if (timer.sevenths > 0f || visitors >= DenVisitorCap) continue;
            var pop = WorldEcology.Find(Map, Map[site.center].habitatSlot, species.id);
            if (pop == null || pop.groups < 1f) continue;
            var habitat = WorldPursuit.HabitatOf(species);
            var hex = FreeHexNear(MicroNavigation.Center(Map[site.center].coord), habitat, habitat == CreatureHabitat.Water ? 8 : 3);
            if (!hex.HasValue) { timer.sevenths = species.denVisitSevenths; continue; }
            var response = WorldBehavior.Toward(Map, Settings.generation, species, WorldAuthority.Player);
            var stance = WorldPursuit.Fearful(response) || response == ThreatResponse.Tolerates ? EnemyStance.Timid
                : WorldBattles.Aggressive(species) ? EnemyStance.Hostile : EnemyStance.Wary;
            var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
            int count = species.groupMin > 0 ? species.groupMin : profile != null && profile.groupMin > 0 ? profile.groupMin : 3;
            var band = SpawnBand(species.id, Math.Max(1, count), hex.Value, stance, homeCell: site.center);
            if (band == null) continue;
            // What it shows you is its species' memory of you (WorldBehavior), not only its nature.
            band.response = response;
            band.denLife = species.denVisitSevenths;
            timer.sevenths = species.denVisitSevenths + 7f;
            visitors++;
            changed = true;
        }
        return changed;
    }

    // ===== WHAT YOU SEE =====

    /// <summary>
    /// Whether you see a unit now: yours always (unless missing); a band only while one of your parties can see its hex
    /// (one beyond its sight: movement catches the eye) or it walks your own territory, where your people report it.
    /// </summary>
    public bool Sees(WorldUnit unit)
    {
        if (unit == null || unit.Missing || Map == null) return false;
        if (WorldBattles.IsPlayers(unit)) return true;
        var at = WorldUnits.MicroPosition(unit);
        var tile = MicroNavigation.Parent(Map, at);
        if (tile == null || !tile.revealed) return false;
        if (WorldAuthority.IsPlayers(tile.authorityId)) return true;
        foreach (var u in Map.Units)
        {
            if (!WorldBattles.IsPlayers(u) || u.Missing) continue;
            var spec = SpecOf(u);
            if (spec != null && HexCoord.Distance(WorldUnits.MicroPosition(u), at) <= WorldUnits.Sight(Map, u, spec, Rules) + 1) return true;
        }
        return false;
    }

    // ===== YOUR PARTIES GIVE CHASE =====

    /// <summary>Tells a party of yours to run (faster, but it spends endurance; winded, it must stop) or walk.</summary>
    public void SetSprint(WorldUnit unit, bool running)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit) || unit.Missing) return;
        WorldPursuit.Initialize(Settings.generation, unit);
        unit.sprinting = running;
        Changed?.Invoke();
    }

    /// <summary>Why a party of yours cannot give chase to a band, or null.</summary>
    public string WhyNotEngage(WorldUnit hunter, WorldUnit target)
    {
        if (hunter == null || target == null || !WorldBattles.IsPlayers(hunter) || WorldBattles.IsPlayers(target)) return "Only your parties hunt, and only bands.";
        if (hunter.Missing) return "Its whereabouts are unknown.";
        if (!WorldBattles.Fights(target)) return "Nothing is left of it.";
        if (!Sees(target)) return "None of your people can see it.";
        if (hunter.Working) return "Busy at its work.";
        if (target.truce > 0f || hunter.truce > 0f) return "Both sides are drawing breath after the last battle.";
        if (hunter.winded) return "Too winded to run.";
        return WorldPursuit.Route(Map, Settings.generation, hunter, WorldUnits.MicroPosition(target), true, out _) ? null
            : target.habitat == CreatureHabitat.Water ? "It is out in the water, beyond reach of the shore." : "No way to reach it from here.";
    }

    /// <summary>
    /// A party of yours gives chase to a band: it runs after it (re-planning as the band moves) and attacks it once it
    /// catches up. A timid quarry flees and tires: run it down to hunt it. It gives up once the band is lost from sight
    /// and its trail goes cold.
    /// </summary>
    public bool EngageBand(WorldUnit hunter, WorldUnit target)
    {
        string why = WhyNotEngage(hunter, target);
        if (why != null)
        {
            if (hunter != null && target != null && WorldBattles.IsPlayers(hunter)) Say($"{hunter.name} cannot give chase to {target.name}: {why}");
            return false;
        }
        WorldPursuit.Initialize(Settings.generation, hunter);
        if (!WorldPursuit.Route(Map, Settings.generation, hunter, WorldUnits.MicroPosition(target), true, out var path)) return false;
        if (hunter.Camping) WorldUnits.BreakCamp(hunter);
        hunter.autoExplore = false;
        PauseSurvey(hunter);
        hunter.autoSurvey = false;
        hunter.returning = false;
        hunter.retreating = false;
        hunter.onArrival = UnitTask.None;
        WorldUnits.Order(Map, hunter, path);
        hunter.quarryId = target.id;
        hunter.lastSeen = WorldUnits.MicroPosition(target);
        hunter.memoryLeft = WorldPursuit.MemoryOf(BandIntelligence.Smart);
        hunter.decisionLeft = WorldPursuit.DecisionSevenths;
        hunter.sprinting = true;
        Say($"{hunter.name} gives chase to {target.name}.");
        Changed?.Invoke();
        return true;
    }

    // A party giving chase re-plans toward its quarry as it moves; out of sight it heads for where it saw it last, and
    // gives up once the trail goes cold. True when it gave up.
    private bool TrackQuarry(WorldUnit unit, UnitSpec spec, float time, List<string> notice)
    {
        unit.memoryLeft = Math.Max(0f, unit.memoryLeft - time);
        unit.decisionLeft -= time;
        if (unit.quarryId < 0 || unit.decisionLeft > 0f) return false;
        unit.decisionLeft = WorldPursuit.DecisionSevenths;
        var target = UnitById(unit.quarryId);
        if (target == null || !WorldBattles.Fights(target)) { EndChase(unit, notice, $"{unit.name} has lost its quarry."); return true; }
        var at = WorldUnits.MicroPosition(target);
        int sight = WorldUnits.Sight(Map, unit, spec, Rules);
        if (HexCoord.Distance(WorldUnits.MicroPosition(unit), at) <= sight + 1)
        {
            unit.lastSeen = at;
            unit.memoryLeft = WorldPursuit.MemoryOf(BandIntelligence.Smart);
        }
        else if (unit.memoryLeft <= 0f) { EndChase(unit, notice, $"{unit.name} loses the trail of {target.name}."); return true; }
        bool seen = unit.lastSeen == at;
        if (WorldPursuit.Route(Map, Settings.generation, unit, unit.lastSeen, seen, out var path))
        {
            if (path.Count > 0) WorldUnits.Order(Map, unit, path);
            else if (!seen) { EndChase(unit, notice, $"{unit.name} loses the trail of {target.name}."); return true; }
            else WorldUnits.Stop(unit);
            return false;
        }
        EndChase(unit, notice, target.habitat == CreatureHabitat.Water ? $"{target.name} slips out into the water, beyond reach of {unit.name}." : $"{unit.name} cannot reach {target.name}.");
        return true;
    }

    private void EndChase(WorldUnit unit, List<string> notice, string why)
    {
        if (why != null) notice.Add(why);
        unit.quarryId = -1;
        unit.sprinting = false;
        unit.memoryLeft = 0f;
        WorldUnits.Stop(unit);
    }

    // What a hunt on the move brings home: the den's harvest (its meat and hides) for the creatures taken, as far as the
    // packs hold it. The species remembers being hunted (WorldBehavior) and its land's population shrinks.
    private void HuntSpoils(WorldUnit hunter, WorldUnit band, int taken, List<string> lines)
    {
        if (taken <= 0 || hunter == null || band == null || string.IsNullOrEmpty(band.species)) return;
        var gen = Settings.generation;
        var species = gen.Species(band.species);
        Cull(band, taken);
        var den = gen.resourceSites.FirstOrDefault(r => r != null && string.Equals(r.species, band.species, StringComparison.OrdinalIgnoreCase));
        WorldBehavior.Hunted(Map, gen, band.species, WorldAuthority.Player, 0.5f);
        if (species == null || den == null || den.harvest == null) return;
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        int group = species.groupMax > 0 ? species.groupMax : profile != null && profile.groupMax > 0 ? profile.groupMax : 6;
        float share = Math.Max(0.15f, Math.Min(1f, taken / (float)Math.Max(1, group)));
        float room = Math.Max(0f, CargoCapacity(hunter) - CargoLoad(hunter));
        var got = new List<string>();
        hunter.cargo = hunter.cargo ?? new List<ResourceAmount>();
        foreach (var h in den.harvest.Where(h => h != null && !string.IsNullOrEmpty(h.resource) && h.amount > 0f))
        {
            float weight = Math.Max(0.01f, Expeditions.WeightOf(ExpeditionRules, h.resource));
            float amount = Math.Min(h.amount * share, room / weight);
            if (amount <= 0.01f) continue;
            room -= amount * weight;
            var had = hunter.cargo.FirstOrDefault(c => c != null && string.Equals(c.resource, h.resource, StringComparison.OrdinalIgnoreCase));
            if (had != null) had.amount += amount;
            else hunter.cargo.Add(new ResourceAmount { resource = h.resource, amount = amount });
            got.Add($"{amount:0.#} {h.resource}");
        }
        if (got.Count > 0) lines.Add($"{hunter.name} carries off {string.Join(", ", got)}.");
        else lines.Add($"{hunter.name}'s packs are full: the kill is left behind.");
    }
}
