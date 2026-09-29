using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Battles on the world map, with no scene state (tested in <c>WorldBattleTests</c>).
///
/// Every unit can fight and be fought: an army carries its stack of companies (military: far more Integrity and
/// Composure), every other party (expeditions, builders, cultural parties, settlers, scouts) fights as itself, its
/// legends leading it (<see cref="PartySide"/>), and an enemy band as its creatures or a threat's beings. Units of
/// different sides never share a micro hex (<see cref="Held"/>: a unit halts beside the other), so a battle starts when
/// two hostile units stand on adjacent hexes. Enemies come in two kinds: red (<see cref="EnemyStance.Hostile"/>) hunt your
/// units; orange (<see cref="EnemyStance.Wary"/>) turn hostile for a while when one of your units comes adjacent or passes
/// close by (<see cref="Provokes"/>). Threats send them out (<see cref="ThreatSpec"/>).
///
/// Where each stands decides the field (<see cref="Field"/>): the defender's hex gives the ground, cover and a
/// settlement; a ford on either hex makes the attacker cross a river; the height between the two hexes favours whoever
/// stands higher; the attacker's sections fight by their own hex's footing. Who attacks: the one that walked into contact,
/// else the one bent on the fight. Every number is a proposal.
/// </summary>
public static class WorldBattles
{
    public const string ArmySpec = "army", BandSpec = "creature-band";
    /// <summary>The faction of wild creatures and of the threats' beings.</summary>
    public const string Wild = "wild";
    /// <summary>Sevenths two units that fought stay apart before they can clash again.</summary>
    public const float TruceSevenths = 1f;
    /// <summary>A defender on a high ridge or an escarpment's lip stands this much higher besides the ground's own height.</summary>
    public const float VantageHeight = 0.08f;
    /// <summary>Sevenths an orange enemy stays hostile once provoked.</summary>
    public const float ProvokedSevenths = 3f;
    /// <summary>Micro hexes within which a unit on the move provokes an orange enemy (standing, only adjacent).</summary>
    public const int PassingReach = 2;

    public static readonly Color Red = new Color(0.9f, 0.15f, 0.15f), Orange = new Color(1f, 0.55f, 0.1f);
    /// <summary>A timid band's muted orange: it flees from you.</summary>
    public static readonly Color Timid = new Color(0.84f, 0.68f, 0.47f);

    /// <summary>The built-in kinds of unit for armies and bands, used when World.asset lists none of that id.</summary>
    public static UnitSpec DefaultUnit(string id)
    {
        if (string.Equals(id, ArmySpec, StringComparison.OrdinalIgnoreCase))
            return new UnitSpec
            {
                id = ArmySpec, name = "Army", role = UnitRole.Army, color = new Color(0.85f, 0.3f, 0.25f),
                description = "A stack of conscripted companies under a legend's command.",
                stamina = 5f, sight = 4, supplyCapacity = 14f, supplyUsePerSeventh = 1.5f, fatiguePerTravelCost = 1f, limit = 0,
            };
        if (string.Equals(id, BandSpec, StringComparison.OrdinalIgnoreCase))
            return new UnitSpec
            {
                id = BandSpec, name = "Enemy band", role = UnitRole.Band, color = Red,
                description = "Enemies on the move: a pack, a swarm, a threat's beings.",
                stamina = 6f, sight = 4, supplyCapacity = 0f, supplyUsePerSeventh = 0f, fatiguePerTravelCost = 0.5f, wearMultiplier = 0f, limit = 0,
            };
        return null;
    }

    // ===== SIDES =====

    public static bool IsPlayers(WorldUnit u) => u != null && string.IsNullOrEmpty(u.faction);

    /// <summary>A unit that can fight a battle: every one of yours, an army, or a band with creatures left.</summary>
    public static bool Fights(WorldUnit u) =>
        u != null && !u.Missing && (IsPlayers(u) || !string.IsNullOrEmpty(u.armyStack) || u.creatures > 0);

    /// <summary>An enemy that means you harm now: red, or orange and provoked.</summary>
    public static bool Angry(WorldUnit u) => u != null && !IsPlayers(u) && (u.stance == EnemyStance.Hostile || u.provoked > 0f);

    /// <summary>Its colour on the map: red when hostile, orange while wary, muted orange when timid (it flees).</summary>
    public static Color ColorOf(WorldUnit u) => Angry(u) ? Red : u != null && u.stance == EnemyStance.Timid ? Timid : Orange;

    /// <summary>Two sides: yours and another, or two different factions that are not both wild.</summary>
    public static bool OtherSides(WorldUnit a, WorldUnit b)
    {
        string fa = a?.faction ?? string.Empty, fb = b?.faction ?? string.Empty;
        if (string.Equals(fa, fb, StringComparison.OrdinalIgnoreCase)) return false;
        return fa.Length == 0 || fb.Length == 0 || !(string.Equals(fa, Wild, StringComparison.OrdinalIgnoreCase) && string.Equals(fb, Wild, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Two fighting units of different sides, neither in a truce; a wary or timid enemy only once provoked, or when one
    /// of your parties is hunting it (it has to turn and fight when caught).
    /// </summary>
    public static bool Hostile(WorldUnit a, WorldUnit b)
    {
        if (a == null || b == null || a == b || !Fights(a) || !Fights(b) || a.truce > 0f || b.truce > 0f || !OtherSides(a, b)) return false;
        if (!IsPlayers(a) && !Angry(a) && !Hunted(b, a)) return false;
        if (!IsPlayers(b) && !Angry(b) && !Hunted(a, b)) return false;
        return true;
    }

    /// <summary>One of your parties giving chase to that band.</summary>
    public static bool Hunted(WorldUnit hunter, WorldUnit band) => IsPlayers(hunter) && !IsPlayers(band) && hunter.quarryId >= 0 && hunter.quarryId == band.id;

    /// <summary>On the same micro hex or adjacent ones.</summary>
    public static bool InContact(WorldUnit a, WorldUnit b) => HexCoord.Distance(WorldUnits.MicroPosition(a), WorldUnits.MicroPosition(b)) <= 1;

    /// <summary>Whether one of your units provokes a wary enemy: standing adjacent, or passing within <see cref="PassingReach"/>.</summary>
    public static bool Provokes(WorldUnit yours, WorldUnit enemy)
    {
        if (!IsPlayers(yours) || IsPlayers(enemy) || yours.Missing || enemy.Missing || enemy.stance != EnemyStance.Wary || enemy.truce > 0f) return false;
        int d = HexCoord.Distance(WorldUnits.MicroPosition(yours), WorldUnits.MicroPosition(enemy));
        return d <= 1 || (yours.Moving && d <= PassingReach);
    }

    /// <summary>
    /// For <see cref="WorldUnits.Move"/>: a hex <paramref name="mover"/> may not enter, because a unit of another side
    /// stands on it (units of different sides never share a hex).
    /// </summary>
    public static Func<HexCoord, bool> Held(IEnumerable<WorldUnit> units, WorldUnit mover)
    {
        var others = (units ?? Enumerable.Empty<WorldUnit>()).Where(u => u != mover && !u.Missing && OtherSides(u, mover)).Select(WorldUnits.MicroPosition).ToList();
        return hex => others.Contains(hex);
    }

    /// <summary>
    /// The battles to fight now: each hostile pair in contact, the attacker first (<see cref="Attacker"/>). A unit fights
    /// one battle at a time; pairs are taken by id, so the same map gives the same battles.
    /// </summary>
    public static List<(WorldUnit attacker, WorldUnit defender)> Clashes(IEnumerable<WorldUnit> units, Func<WorldUnit, bool> aggressive = null)
    {
        var list = (units ?? Enumerable.Empty<WorldUnit>()).Where(Fights).OrderBy(u => u.id).ToList();
        var engaged = new HashSet<int>();
        var clashes = new List<(WorldUnit, WorldUnit)>();
        for (int i = 0; i < list.Count; i++)
        {
            if (engaged.Contains(list[i].id)) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];
                if (engaged.Contains(b.id) || !Hostile(a, b) || !InContact(a, b)) continue;
                var attacker = Attacker(a, b, aggressive ?? Angry);
                clashes.Add(attacker == a ? (a, b) : (b, a));
                engaged.Add(a.id);
                engaged.Add(b.id);
                break;
            }
        }
        return clashes;
    }

    /// <summary>
    /// Who attacks: the one on the move into the other (the other standing), else the one bent on the fight (an angry
    /// enemy), else the one with the lower id.
    /// </summary>
    public static WorldUnit Attacker(WorldUnit a, WorldUnit b, Func<WorldUnit, bool> aggressive = null)
    {
        if (a.Moving && !b.Moving) return a;
        if (b.Moving && !a.Moving) return b;
        bool aa = aggressive?.Invoke(a) ?? false, ab = aggressive?.Invoke(b) ?? false;
        if (aa != ab) return aa ? a : b;
        return a.id <= b.id ? a : b;
    }

    // ===== WHO FIGHTS AS WHAT =====

    /// <summary>A party's Integrity, Composure, attack and defense before its legends: its spec's, else its role's.</summary>
    public static (float integrity, float composure, float attack, float defense) Profile(UnitSpec spec)
    {
        (float, float, float, float) role;
        switch (spec?.role ?? UnitRole.Expedition)
        {
            case UnitRole.Scout: role = (30f, 30f, 4f, 8f); break;
            case UnitRole.Settler: role = (30f, 25f, 2f, 6f); break;
            case UnitRole.Builder: role = (40f, 30f, 3f, 10f); break;
            default: role = (50f, 40f, 6f, 12f); break;
        }
        return (spec != null && spec.battleIntegrity > 0f ? spec.battleIntegrity : role.Item1,
                spec != null && spec.battleComposure > 0f ? spec.battleComposure : role.Item2,
                spec != null && spec.battleAttack > 0f ? spec.battleAttack : role.Item3,
                spec != null && spec.battleDefense > 0f ? spec.battleDefense : role.Item4);
    }

    /// <summary>What each legend adds to its party (its own share of the party, which it leads).</summary>
    public const float LegendIntegrity = 40f, LegendComposure = 35f, LegendAttack = 5f, LegendDefense = 6f;
    /// <summary>Integrity each settler an expedition escorts adds (they walk behind, and fight poorly).</summary>
    public const float SettlerIntegrity = 5f;

    /// <summary>
    /// A party that is not an army as a side of battle: the Director at its head (its commander) and leading the party's
    /// first section, each companion leading a section of its own, settlers walking behind. Its wounds (attrition) and
    /// lost nerve carry over from its last battle.
    /// </summary>
    public static BattleSide PartySide(WorldUnit unit, UnitSpec spec, IList<string> members, Func<string, BattleLegend> legendOf)
    {
        var p = Profile(spec);
        float health = Math.Max(0.05f, 1f - Mathf.Clamp01(unit.attrition / 100f));
        float nerve = Math.Max(0.05f, 1f - Mathf.Clamp01(unit.nerveLost));
        var side = new BattleSide { name = unit.name, takesCaptives = true };
        var legends = (members ?? new List<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        int parts = Math.Max(1, legends.Count);
        for (int i = 0; i < parts; i++)
        {
            string legend = i < legends.Count ? legends[i] : null;
            var sec = new CombatSection
            {
                name = legend == null ? unit.name : i == 0 ? $"{unit.name}" : $"{legend}'s circle",
                row = FormationRow.Front, kind = SectionKind.Infantry,
                maxIntegrity = p.integrity / parts + (legend != null ? LegendIntegrity : 0f),
                maxComposure = p.composure / parts + (legend != null ? LegendComposure : 0f),
                attack = p.attack / parts + (legend != null ? LegendAttack : 0f),
                defense = p.defense / parts + (legend != null ? LegendDefense : 0f),
                breakthrough = (p.defense / parts + (legend != null ? LegendDefense : 0f)) * 0.7f,
                piercing = 4f, speed = 4f, count = legend != null ? 1 : 0,
                leader = legend != null ? legendOf?.Invoke(legend) : null,
            };
            sec.Reset();
            sec.integrity = sec.maxIntegrity * health;
            sec.composure = sec.maxComposure * nerve;
            side.sections.Add(sec);
        }
        if (unit.settlers > 0)
        {
            var settlers = new CombatSection
            {
                name = $"Settlers ({unit.settlers})", row = FormationRow.Back, kind = SectionKind.Support, count = unit.settlers,
                maxIntegrity = unit.settlers * SettlerIntegrity, maxComposure = 20f, attack = 0f, defense = 2f, breakthrough = 1f, speed = 3f,
            };
            settlers.Reset();
            settlers.integrity = settlers.maxIntegrity * health;
            settlers.composure = settlers.maxComposure * nerve;
            side.sections.Add(settlers);
        }
        side.conductor = legends.Count > 0 ? legendOf?.Invoke(legends[0]) : null;
        return side;
    }

    /// <summary>A threat's own beings as a species the battle can read (its band's drawn binding as their primary).</summary>
    public static SpeciesSpec Being(ThreatSpec threat, SpellBinding binding) => threat == null ? null : new SpeciesSpec
    {
        id = "threat:" + threat.id, name = string.IsNullOrEmpty(threat.beingName) ? threat.name : threat.beingName,
        size = threat.beingSize, structure = threat.beingStructure, diet = threat.beingDiet, subgroup = threat.beingSubgroup,
        primaryBinding = binding, niche = HarmonicNiche.Discordant,
    };

    /// <summary>The binding a threat's new band carries: one of its allowed bindings (any of the seven when it lists none), drawn by <paramref name="roll"/> (0-1).</summary>
    public static SpellBinding DrawBinding(ThreatSpec threat, double roll)
    {
        var pool = (threat?.beingBindings ?? new List<SpellBinding>()).Where(b => b != SpellBinding.Unattuned).Distinct().ToList();
        if (pool.Count == 0) pool = HarmonicCircle.Seven.ToList();
        int i = Math.Max(0, Math.Min(pool.Count - 1, (int)(Math.Max(0d, Math.Min(0.999999d, roll)) * pool.Count)));
        return pool[i];
    }

    /// <summary>Creatures that go looking for a fight (hunters, the ones that attack on sight, lurers, spreaders).</summary>
    public static bool Aggressive(SpeciesSpec species)
    {
        var profile = species == null ? null : CreatureTaxonomy.Profile(species.diet, species.subgroup);
        switch (profile?.response ?? ThreatResponse.AvoidsConflict)
        {
            case ThreatResponse.AttacksOnSight:
            case ThreatResponse.Hunts:
            case ThreatResponse.HuntsLoudly:
            case ThreatResponse.Lures:
            case ThreatResponse.Expands:
                return true;
            default:
                return false;
        }
    }

    // ===== THE FIELD =====

    /// <summary>
    /// The field of a clash between the attacker on <paramref name="attackerHex"/> and the defender on
    /// <paramref name="defenderHex"/> (micro hexes): the defender's hex gives the ground (a rim hex may carry its
    /// neighbour's), cover, the Loom's state and a settlement (on its cell's centre hex); a ford on either hex (unless the
    /// defender's hex carries a road over it) makes the attacker cross; the height between the two hexes favours whoever
    /// stands higher (a ridge or an escarpment's lip more so); the attacker fights by its own hex's footing.
    /// </summary>
    public static Battlefield Field(WorldMap map, WorldGenSettings gen, CombatSettings combat, HexCoord attackerHex, HexCoord defenderHex, int age, int echo)
    {
        combat = combat ?? new CombatSettings();
        var defCell = MicroNavigation.Parent(map, defenderHex);
        var atkCell = MicroNavigation.Parent(map, attackerHex);
        var defGround = MicroNavigation.GroundCell(map, gen, defenderHex) ?? defCell;
        var atkGround = MicroNavigation.GroundCell(map, gen, attackerHex) ?? atkCell;
        var field = Battlefield.From(defGround, null, gen, combat, age, echo);
        if (defCell == null) return field;

        // The Loom's state, the weather and the settlement are the defender's cell's, whatever ground its rim hex borrows.
        field.coherence = defCell.coherence;
        field.dissonance = defCell.dissonance;
        field.leyline = defCell.leylines != 0;
        field.sacred = defCell.sacred;
        field.fallout = defCell.fallout;
        field.weather = defCell.weatherTravelMultiplier;
        field.settlement = defCell.settlement >= 0 && defenderHex == MicroNavigation.Center(defCell.coord);

        float defHeight = defCell.elevation + (WorldUnits.HighGround(defCell) ? VantageHeight : 0f);
        float atkHeight = (atkCell?.elevation ?? defCell.elevation) + (WorldUnits.HighGround(atkCell) ? VantageHeight : 0f);
        field.height = Math.Max(0f, defHeight - atkHeight);
        field.downhill = Math.Max(0f, atkHeight - defHeight);

        var grid = map.microGrid;
        if (grid != null)
        {
            int d = MicroNavigation.Index(map, defenderHex), a = MicroNavigation.Index(map, attackerHex);
            var fords = MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord;
            var defMarks = d >= 0 ? grid.Marks(d) : MicroGrid.Mark.None;
            var atkMarks = a >= 0 ? grid.Marks(a) : MicroGrid.Mark.None;
            bool bridged = (defMarks & MicroGrid.Mark.Road) != 0;
            field.riverCrossing = !bridged && ((defMarks & fords) != 0 || (atkMarks & fords) != 0);
        }
        else if (atkCell != null && atkCell != defCell)
            field.riverCrossing = defCell.river && defCell.downstream != atkCell.index && atkCell.downstream != defCell.index;

        if (atkGround != null && !string.Equals(atkGround.terrain, defGround?.terrain, StringComparison.OrdinalIgnoreCase))
            field.attackerGround = combat.GroundOf(atkGround.terrain).ground;
        return field;
    }

    // ===== THE HUNT AND THE ROAM =====

    /// <summary>
    /// The nearest of <paramref name="targets"/> a band can see (within its sight, in micro hexes) and the way to it, or
    /// null when none is in sight.
    /// </summary>
    public static WorldUnit Quarry(WorldMap map, WorldGenSettings gen, WorldUnit band, int sight, IEnumerable<WorldUnit> targets, out List<int> path)
    {
        path = null;
        var from = WorldUnits.MicroPosition(band);
        foreach (var t in (targets ?? Enumerable.Empty<WorldUnit>()).Where(t => t != null && !t.Missing)
                     .OrderBy(t => HexCoord.Distance(from, WorldUnits.MicroPosition(t))).ThenBy(t => t.id))
        {
            var at = WorldUnits.MicroPosition(t);
            if (HexCoord.Distance(from, at) > Math.Max(1, sight)) break;
            if (MicroNavigation.ToNearest(map, gen, from, at, out var way, out _, out _) && way.Count > 0)
            {
                path = way;
                return t;
            }
        }
        return null;
    }

    /// <summary>A hex for a band to wander to within <paramref name="roam"/> cells of its home cell, drawn by <paramref name="roll"/> (0-1).</summary>
    public static HexCoord RoamTarget(WorldMap map, int homeCell, int roam, double roll)
    {
        var home = homeCell >= 0 && homeCell < map.Count ? map[homeCell].coord : HexCoord.Zero;
        var cells = HexCoord.Spiral(home, Math.Max(0, roam)).Select(map.Get).Where(t => t != null && !t.water && !t.impassable).ToList();
        if (cells.Count == 0) return MicroNavigation.Center(home);
        int i = Math.Max(0, Math.Min(cells.Count - 1, (int)(Math.Max(0d, Math.Min(0.999999d, roll)) * cells.Count)));
        return MicroNavigation.Center(cells[i].coord);
    }
}
