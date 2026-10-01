using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Battles on the world map with no scene (<see cref="WorldBattles"/>): who can fight and who is hostile, contact on the
/// same or adjacent micro hexes, one battle per unit at a time, who attacks, and the field read from where each side
/// stands (the defender's ground and settlement, the height between the two hexes, the attacker's own footing).
/// </summary>
public class WorldBattleTests
{
    private static WorldGenSettings Gen()
    {
        var s = new WorldGenSettings();
        s.terrains.Add(new TerrainSpec { id = "plains", name = "Plains", moveCost = 1f });
        s.terrains.Add(new TerrainSpec { id = "woodland", name = "Woodland", moveCost = 1.5f });
        return s;
    }

    private static WorldMap Line(int n, Dictionary<int, string> ground = null, Dictionary<int, float> elevation = null)
    {
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        for (int q = 0; q < n; q++)
        {
            HexHierarchy.ToWorld(new HexCoord(q, 0), HexHierarchy.Meso, out float x, out float y);
            map.Add(new WorldTile
            {
                coord = new HexCoord(q, 0), x = x, y = y, explored = true, revealed = true, known = true, settlement = -1,
                terrain = ground != null && ground.TryGetValue(q, out var g) ? g : "plains",
                elevation = elevation != null && elevation.TryGetValue(q, out var e) ? e : 0.3f,
            });
        }
        return map;
    }

    private static WorldUnit Army(int id, HexCoord micro) { var u = new WorldUnit { id = id, armyStack = "stack-" + id }; WorldUnits.Place(u, micro); return u; }
    private static WorldUnit Band(int id, HexCoord micro) { var u = new WorldUnit { id = id, faction = WorldBattles.Wild, species = "wolf", creatures = 6 }; WorldUnits.Place(u, micro); return u; }

    private static HexCoord Centre(int q) => MicroNavigation.Center(new HexCoord(q, 0));

    [Test]
    public void OnlyFightingUnitsOfDifferentSidesAreHostile()
    {
        var army = Army(1, Centre(0));
        var band = Band(2, Centre(0));
        Assert.IsTrue(WorldBattles.Hostile(army, band));
        Assert.IsFalse(WorldBattles.Hostile(band, Band(3, Centre(0))), "wild creatures do not fight each other");
        Assert.IsFalse(WorldBattles.Hostile(army, Army(4, Centre(0))), "your armies are one side");
        var expedition = new WorldUnit { id = 5, leader = "Iris" };
        Assert.IsTrue(WorldBattles.Fights(expedition), "every one of your units can be attacked");
        Assert.IsTrue(WorldBattles.Hostile(expedition, band));
        band.truce = 0.5f;
        Assert.IsFalse(WorldBattles.Hostile(army, band), "a truce after a battle keeps them apart");
        band.truce = 0f;
        band.creatures = 0;
        Assert.IsFalse(WorldBattles.Fights(band), "a band with no creatures left");
    }

    [Test]
    public void ContactIsTheSameOrAnAdjacentHex()
    {
        var here = Centre(0);
        Assert.IsTrue(WorldBattles.InContact(Army(1, here), Band(2, here)));
        Assert.IsTrue(WorldBattles.InContact(Army(1, here), Band(2, here.Neighbor(0))));
        Assert.IsFalse(WorldBattles.InContact(Army(1, here), Band(2, here.Neighbor(0).Neighbor(0))));
    }

    [Test]
    public void EachUnitFightsOneBattleAndTheMoverAttacks()
    {
        var here = Centre(1);
        var army = Army(1, here);
        var wolves = Band(2, here.Neighbor(0));
        var boars = Band(3, here.Neighbor(3));
        wolves.path.Add(here);
        var clashes = WorldBattles.Clashes(new[] { army, wolves, boars });
        Assert.AreEqual(1, clashes.Count, "the army fights one battle at a time");
        Assert.AreEqual(wolves, clashes[0].attacker, "the band walking into the army attacks it");
        Assert.AreEqual(army, clashes[0].defender);
        Assert.AreEqual(boars, WorldBattles.Attacker(army, boars, u => u.species != null), "neither moving: the one bent on the fight");
        Assert.AreEqual(army, WorldBattles.Attacker(army, boars), "else the lower id");
    }

    [Test]
    public void TheFieldIsReadFromWhereEachSideStands()
    {
        var gen = Gen();
        var map = Line(3, new Dictionary<int, string> { [1] = "woodland" }, new Dictionary<int, float> { [0] = 0.2f, [1] = 0.45f, [2] = 0.6f });
        var combat = new CombatSettings();
        // An army on the plain attacks a band in the wood above it.
        var f = WorldBattles.Field(map, gen, combat, Centre(0), Centre(1), 1, 0);
        Assert.AreEqual(BattleGround.Forest, f.ground, "the defender's hex gives the ground");
        Assert.AreEqual(BattleGround.Open, f.attackerGround, "the attacker fights by its own footing");
        Assert.AreEqual(0.25f, f.height, 1e-4f, "the defender stands higher");
        Assert.AreEqual(0f, f.downhill);
        // Coming down from the heights instead.
        var down = WorldBattles.Field(map, gen, combat, Centre(2), Centre(1), 1, 0);
        Assert.AreEqual(0f, down.height);
        Assert.AreEqual(0.15f, down.downhill, 1e-4f, "the attacker comes downhill");
        // A settlement is fought for only on its own hex, its cell's centre.
        map[1].settlement = 0;
        Assert.IsTrue(WorldBattles.Field(map, gen, combat, Centre(0), Centre(1), 1, 0).settlement);
        Assert.IsFalse(WorldBattles.Field(map, gen, combat, Centre(0), Centre(1).Neighbor(0), 1, 0).settlement);
        // Same ground on both hexes: no separate footing.
        Assert.IsNull(WorldBattles.Field(map, gen, combat, Centre(1), Centre(1), 1, 0).attackerGround);
    }

    private static readonly CombatSettings Settings = new CombatSettings();

    private static BattleSetup Clash(Battlefield field, int seed = 5) => new BattleSetup
    {
        attacker = new BattleSide { name = "Raiders", sections = { CombatSection.Raise(Settings.Section("warband")) } },
        defender = new BattleSide { name = "Wall", sections = { CombatSection.Raise(Settings.Section("shieldwall")) } },
        field = field, seed = seed,
    };

    [Test]
    public void ComingDownhillCarriesTheAttacker()
    {
        var flat = BattleResolver.Resolve(Clash(new Battlefield { age = 1 }), Settings);
        var down = BattleResolver.Resolve(Clash(new Battlefield { age = 1, downhill = 0.2f }), Settings);
        Assert.Less(down.timeline[1].defender.integrity, flat.timeline[1].defender.integrity);
    }

    [Test]
    public void TheAttackerFightsByItsOwnFooting()
    {
        // Ash Warbands fight worse while still among trees. Movement onto open ground must remove that penalty.
        BattleReport Holding(BattleGround footing)
        {
            var setup = Clash(new Battlefield { age = 1, ground = BattleGround.Open, attackerGround = footing });
            setup.attacker.manual = setup.defender.manual = true;
            setup.attacker.sections[0].battleHex = 1; setup.defender.sections[0].battleHex = 2;
            var run = BattleResolver.Begin(setup, Settings, forecastRuns: 1); run.ResolveMeasure(); return run.Report;
        }
        var open = Holding(BattleGround.Open);
        var fromWood = Holding(BattleGround.Forest);
        Assert.Greater(fromWood.timeline[1].defender.integrity, open.timeline[1].defender.integrity);
    }

    // ---- Red and orange ------------------------------------------------------------------------------------------

    [Test]
    public void OrangeEnemies_TurnHostileWhenYouComeClose()
    {
        var here = Centre(1);
        var wary = Band(2, here);
        wary.stance = EnemyStance.Wary;
        var party = new WorldUnit { id = 1, leader = "Iris" };
        WorldUnits.Place(party, here.Neighbor(0).Neighbor(0).Neighbor(0));
        Assert.IsFalse(WorldBattles.Hostile(party, wary), "orange: wary, not hostile");
        Assert.AreEqual(WorldBattles.Orange, WorldBattles.ColorOf(wary));
        Assert.IsFalse(WorldBattles.Provokes(party, wary), "three hexes off, standing");
        WorldUnits.Place(party, here.Neighbor(0).Neighbor(0));
        Assert.IsFalse(WorldBattles.Provokes(party, wary), "two hexes off, standing");
        party.path.Add(here.Neighbor(3).Neighbor(3));
        Assert.IsTrue(WorldBattles.Provokes(party, wary), "passing close by provokes it");
        party.path.Clear();
        WorldUnits.Place(party, here.Neighbor(0));
        Assert.IsTrue(WorldBattles.Provokes(party, wary), "adjacent provokes it");
        wary.provoked = WorldBattles.ProvokedSevenths;
        Assert.IsTrue(WorldBattles.Hostile(party, wary), "provoked, it turns red");
        Assert.AreEqual(WorldBattles.Red, WorldBattles.ColorOf(wary));
        Assert.AreEqual(WorldBattles.Red, WorldBattles.ColorOf(Band(3, here)), "red: hostile from the start");
    }

    [Test]
    public void DifferentSides_NeverShareAHex()
    {
        var gen = Gen();
        var map = Line(3);
        var army = Army(1, Centre(0));
        var wolves = Band(2, Centre(1));
        var held = WorldBattles.Held(new[] { army, wolves }, army);
        Assert.IsTrue(held(Centre(1)), "the band holds its hex against you");
        Assert.IsFalse(held(Centre(0)));
        Assert.IsFalse(WorldBattles.Held(new[] { army, Army(3, Centre(1)) }, army)(Centre(1)), "your own units may stand together");
        Assert.IsTrue(MicroNavigation.ToNearest(map, gen, Centre(0), Centre(1), out var path, out _, out _));
        WorldUnits.Order(map, army, path);
        var spec = new UnitSpec { id = "army", stamina = 50f };
        var step = WorldUnits.Move(map, gen, army, spec, 1f, WorldBattles.Held(new[] { army, wolves }, army));
        Assert.IsTrue(step.halted, "it halts beside the band");
        Assert.AreEqual(1, HexCoord.Distance(WorldUnits.MicroPosition(army), Centre(1)), "adjacent: where the battle starts");
        Assert.AreEqual(1, WorldBattles.Clashes(new[] { army, wolves }).Count);
    }

    [Test]
    public void EveryParty_FightsAsItself()
    {
        var expedition = new WorldUnit { id = 1, name = "Iris's Expedition", leader = "Iris", companions = { "Kael" }, settlers = 6 };
        var spec = new UnitSpec { id = "expedition", role = UnitRole.Expedition };
        var side = WorldBattles.PartySide(expedition, spec, new List<string> { "Iris", "Kael" }, n => new BattleLegend { name = n });
        Assert.AreEqual("Iris", side.conductor.name, "the Director commands");
        Assert.AreEqual(8, side.sections.Count, "Elite scale retains two legends and six individual settlers");
        Assert.AreEqual("Kael", side.sections[1].leader.name);
        Assert.AreEqual(6, side.sections.Where(s => s.row == FormationRow.Back).Sum(s => s.count));
        expedition.attrition = 50f;
        expedition.nerveLost = 0.5f;
        var worn = WorldBattles.PartySide(expedition, spec, new List<string> { "Iris", "Kael" }, n => new BattleLegend { name = n });
        Assert.AreEqual(side.sections[0].integrity * 0.5f, worn.sections[0].integrity, 1e-3f, "wounds carry into the next battle");
        Assert.AreEqual(side.sections[0].composure * 0.5f, worn.sections[0].composure, 1e-3f, "and lost nerve");
        var builders = WorldBattles.PartySide(new WorldUnit { id = 2, name = "Builders I" }, new UnitSpec { role = UnitRole.Builder }, new List<string>(), null);
        Assert.AreEqual(1, builders.sections.Count);
        Assert.IsNull(builders.conductor);
        float company = new CombatSettings().Section("grave-warden").integrity;
        Assert.Greater(company * 2f, builders.LineMaxIntegrity, "military units carry far more");
    }

    [Test]
    public void EveryAtonalis_CarriesABinding()
    {
        var nest = new ThreatSpec { id = "atonalis-nest", name = "Atonalis Nest", beingName = "Nascent Atonalis", beingStructure = 0.4f };
        var seen = new HashSet<SpellBinding>();
        for (int i = 0; i < 70; i++) seen.Add(WorldBattles.DrawBinding(nest, i / 70.0));
        CollectionAssert.AreEquivalent(HarmonicCircle.Seven, seen, "with none listed, any of the seven");
        Assert.IsFalse(seen.Contains(SpellBinding.Unattuned));
        nest.beingBindings.Add(SpellBinding.Void);
        Assert.AreEqual(SpellBinding.Void, WorldBattles.DrawBinding(nest, 0.9));
        var being = WorldBattles.Being(nest, SpellBinding.Strand);
        Assert.AreEqual("Nascent Atonalis", being.name);
        Assert.AreEqual(SpellBinding.Strand, being.primaryBinding);
        Assert.AreEqual(HarmonicNiche.Discordant, being.niche, "Fallout does not harm them");
        var band = CreatureCombat.Sections(being, 3);
        Assert.IsTrue(band.All(s => s.primary == SpellBinding.Strand && s.Casts), "their pseudo-spellweaving is rooted in it, and it is their weakness");
    }

    [Test]
    public void BuiltInUnitsExistForArmiesAndBands()
    {
        Assert.AreEqual(UnitRole.Army, WorldBattles.DefaultUnit(WorldBattles.ArmySpec).role);
        Assert.AreEqual(UnitRole.Band, WorldBattles.DefaultUnit(WorldBattles.BandSpec).role);
        Assert.AreEqual(0f, WorldBattles.DefaultUnit(WorldBattles.BandSpec).supplyUsePerSeventh, "wild creatures live off the land");
        Assert.IsNull(WorldBattles.DefaultUnit("nothing"));
    }
}
