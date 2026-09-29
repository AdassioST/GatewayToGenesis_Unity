using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Encounters on the map with no scene (<see cref="WorldPursuit"/>): running endurance (a runner slows as it tires and a
/// winded one cannot move until it has caught its breath; people outlast their prey), how a band thinks by its nature,
/// rivers that stop a thinking pursuer but not a beast or anyone running for its life, the leash on a chase, a save made
/// before encounters existed, colours and who fights whom (a party hunting a timid band), and spent sides in battle.
/// </summary>
public class WorldPursuitTests
{
    private static WorldGenSettings Gen()
    {
        var s = new WorldGenSettings();
        s.terrains.Add(new TerrainSpec { id = "plain", name = "Plain", moveCost = 1f });
        s.terrains.Add(new TerrainSpec { id = "peaks", name = "Peaks", passable = false });
        s.species.Add(new SpeciesSpec { id = "hare", name = "Meadow Hare", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Frightful, size = CreatureSize.Small });
        s.species.Add(new SpeciesSpec { id = "wolf", name = "Grey Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, prey = { "hare" } });
        s.species.Add(new SpeciesSpec { id = "bear", name = "Cave Bear", diet = CreatureDiet.Omnivore, subgroup = CreatureSubgroup.Smart, size = CreatureSize.Large });
        return s;
    }

    private static WorldMap Disk(int radius)
    {
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        foreach (var coord in HexCoord.Spiral(HexCoord.Zero, radius))
        {
            HexHierarchy.ToWorld(coord, HexHierarchy.Meso, out float x, out float y);
            map.Add(new WorldTile { coord = coord, x = x, y = y, terrain = "plain", explored = true, revealed = true, known = true, settlement = -1 });
        }
        return map;
    }

    // A river down the q = 0 column of cells, from one impassable edge row to the other: no way round it.
    private static WorldMap Wet(int radius)
    {
        var map = Disk(radius);
        foreach (var t in map.Tiles.Where(t => System.Math.Abs(t.coord.r) == radius)) { t.terrain = "peaks"; t.impassable = true; }
        var river = new RiverPath();
        for (int r = -radius; r <= radius; r++) river.cells.Add(map.Get(new HexCoord(0, r)).index);
        map.Rivers.Add(river);
        return map;
    }

    private static HexCoord Centre(int q, int r = 0) => MicroNavigation.Center(new HexCoord(q, r));

    private static WorldUnit Band(int id, string species, HexCoord at, BandIntelligence mind = BandIntelligence.Instinctive)
    {
        var u = new WorldUnit { id = id, faction = WorldBattles.Wild, species = species, creatures = 4, encounterInitialized = true, intelligence = mind, pursuitLimit = 12, territoryRadius = 4 };
        WorldUnits.Place(u, at);
        return u;
    }

    private static WorldUnit Party(int id, HexCoord at)
    {
        var u = new WorldUnit { id = id, name = "Iris's Expedition", leader = "Iris" };
        WorldUnits.Place(u, at);
        return u;
    }

    private static readonly UnitSpec Walker = new UnitSpec { id = "walker", stamina = 6f, sight = 4, fatiguePerTravelCost = 0f };

    // ===== RUNNING =====

    [Test]
    public void ARunnerSlowsAsItTires_AndAWindedOneCannotMoveUntilItHasCaughtItsBreath()
    {
        var gen = Gen();
        var map = Disk(8);
        var hare = Band(1, "hare", Centre(-8));
        hare.sprintPace = 1.7f;
        hare.wind = 1f;
        hare.bandActivity = BandActivity.Fleeing;
        Assert.IsTrue(MicroNavigation.ToNearest(map, gen, Centre(-8), Centre(8), out var path, out _, out _));
        WorldUnits.Order(map, hare, path);
        Assert.AreEqual(1.7f, WorldPursuit.RunPace(hare), 1e-4f, "fresh, it runs at its full sprint");
        bool slowed = false;
        for (int i = 0; i < 400 && !hare.winded && hare.Moving; i++)
        {
            WorldUnits.Move(map, gen, hare, Walker, 0.05f);
            if (!hare.winded && hare.endurance < WorldPursuit.FullPaceEndurance) slowed |= WorldPursuit.RunPace(hare) < 1.7f - 1e-3f;
        }
        Assert.IsTrue(hare.winded, $"run long enough, it is winded (endurance {hare.endurance:0.#}, still moving: {hare.Moving})");
        Assert.IsTrue(slowed, "it slowed before it gave out");
        var before = WorldUnits.MicroPosition(hare);
        var step = WorldUnits.Move(map, gen, hare, Walker, 1f);
        Assert.AreEqual(0, step.entered.Count, "winded, it cannot move");
        Assert.AreEqual(before, WorldUnits.MicroPosition(hare));
        WorldPursuit.Recover(hare, 1f);
        Assert.IsTrue(hare.winded, "a Seventh's rest is not enough");
        WorldPursuit.Recover(hare, 1f);
        Assert.IsFalse(hare.winded, $"it goes on once back to {WorldPursuit.ResumeEndurance}");
    }

    [Test]
    public void Walking_NeitherSpendsNorSpeeds_AndPeopleOutlastTheirPrey()
    {
        var walker = new WorldUnit { id = 1, endurance = 60f };
        Assert.AreEqual(1f, WorldPursuit.RunPace(walker), "walking is its own pace");
        WorldPursuit.Spend(walker, 10f);
        Assert.AreEqual(60f, walker.endurance, "walking spends no endurance");
        walker.path.Add(HexCoord.Zero);
        WorldPursuit.Recover(walker, 1f);
        Assert.AreEqual(60f + WorldPursuit.WalkRecovery, walker.endurance, 1e-3f, "walking, it slowly gets its wind back");

        var gen = Gen();
        var hare = Band(2, "hare", Centre(0));
        hare.encounterInitialized = false;
        WorldPursuit.Initialize(gen, hare);
        var party = Party(3, Centre(1));
        WorldPursuit.Initialize(gen, party);
        Assert.Greater(hare.sprintPace, party.sprintPace, "a hare is quicker off the mark");
        hare.bandActivity = BandActivity.Fleeing;
        party.sprinting = true;
        WorldPursuit.Spend(hare, 10f);
        WorldPursuit.Spend(party, 10f);
        Assert.Greater(party.endurance, hare.endurance, "people are persistence hunters: they tire slowest");
    }

    [Test]
    public void ASpentSide_FightsWithLessNerveAndAWeakerGuard()
    {
        var section = new CombatSection { maxIntegrity = 50f, maxComposure = 40f, defense = 10f };
        section.Reset();
        var side = new BattleSide { sections = { section } };
        WorldPursuit.Tire(side, WorldPursuit.Tiredness(new WorldUnit { endurance = 100f }));
        Assert.AreEqual(40f, section.composure, 1e-3f, "fresh: no change");
        WorldPursuit.Tire(side, WorldPursuit.Tiredness(new WorldUnit { winded = true }));
        Assert.AreEqual(20f, section.composure, 1e-3f, "run down: half its nerve");
        Assert.AreEqual(7f, section.defense, 1e-3f);
    }

    // ===== HOW IT THINKS =====

    [Test]
    public void BeastsAreInstinctive_PackHuntersAndOmnivoresRegular_TheSmartSubgroupSmart()
    {
        var gen = Gen();
        Assert.AreEqual(BandIntelligence.Instinctive, WorldPursuit.IntelligenceOf(BandIdentity.Creature, gen.Species("hare")));
        Assert.AreEqual(BandIntelligence.Regular, WorldPursuit.IntelligenceOf(BandIdentity.Creature, gen.Species("wolf")));
        Assert.AreEqual(BandIntelligence.Smart, WorldPursuit.IntelligenceOf(BandIdentity.Creature, gen.Species("bear")));
        Assert.AreEqual(BandIntelligence.Regular, WorldPursuit.IntelligenceOf(BandIdentity.Atonalis, new SpeciesSpec { diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Marauder }));
        Assert.AreEqual(BandIntelligence.Smart, WorldPursuit.IntelligenceOf(BandIdentity.Human, null), "people think");
        Assert.AreEqual(BandIntelligence.Smart, WorldPursuit.IntelligenceOf(BandIdentity.Creature, new SpeciesSpec { intelligence = BandIntelligence.Smart }), "a species can be set cleverer than its nature");
        Assert.Greater(WorldPursuit.PursuitOf(BandIdentity.Creature, null, ThreatResponse.Hunts), WorldPursuit.PursuitOf(BandIdentity.Creature, null, ThreatResponse.Lures), "pack hunters run far, ambushers hardly at all");
        Assert.AreEqual(9, WorldPursuit.PursuitOf(BandIdentity.Creature, new SpeciesSpec { pursuitTiles = 9 }, ThreatResponse.Hunts), "the species' own leash wins");
    }

    [Test]
    public void ASaveMadeBeforeEncounters_IsSetUpOnce()
    {
        var gen = Gen();
        // Fields a save did not have load as zero: quarry 0 would be a real unit id, endurance 0 a winded runner.
        var old = new WorldUnit { id = 7, faction = WorldBattles.Wild, species = "hare", creatures = 3, stance = EnemyStance.Wary, quarryId = 0, endurance = 0f, pursuitLimit = 0 };
        WorldPursuit.Initialize(gen, old);
        Assert.AreEqual(-1, old.quarryId);
        Assert.AreEqual(100f, old.endurance);
        Assert.AreEqual(EnemyStance.Timid, old.stance, "a hare flees on sight: timid, not wary");
        Assert.AreEqual(ThreatResponse.FleesOnSight, old.response);
        Assert.Greater(old.pursuitLimit, 0);
        Assert.AreEqual(1.7f, old.sprintPace, 1e-4f, "small and quick");
        old.quarryId = 5;
        WorldPursuit.Initialize(gen, old);
        Assert.AreEqual(5, old.quarryId, "only once");
        var yours = new WorldUnit { id = 8, quarryId = 0, endurance = 0f };
        WorldPursuit.Initialize(gen, yours);
        Assert.AreEqual(-1, yours.quarryId);
        Assert.AreEqual(WorldPursuit.PeopleWind, yours.wind);
    }

    // ===== THE GROUND =====

    [Test]
    public void ARiverStopsAThinkingPursuer_ButNotABeast_OrAnyoneRunningForItsLife()
    {
        var gen = Gen();
        var map = Wet(3);
        var target = Centre(2);
        var beast = Band(1, "hare", Centre(-2), BandIntelligence.Instinctive);
        Assert.IsTrue(WorldPursuit.Route(map, gen, beast, target, true, out var across), "a beast plunges across");
        var grid = MicroNavigation.Grid(map, gen);
        Assert.IsTrue(across.Any(id => (grid.Marks(id) & (MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord)) != 0), "through a ford");
        var wolf = Band(2, "wolf", Centre(-2, 1), BandIntelligence.Regular);
        Assert.IsFalse(WorldPursuit.Route(map, gen, wolf, target, true, out _), "the far bank ends a thinking band's chase");
        Assert.IsTrue(WorldPursuit.Route(map, gen, wolf, target, true, out _, fleeing: true), "running for its life, it swims");
        Assert.IsTrue(WorldPursuit.Route(map, gen, wolf, Centre(-1, -1), true, out _), "on its own bank it goes where it likes");
        var party = Party(3, Centre(-2, -1));
        Assert.IsTrue(WorldPursuit.Route(map, gen, party, target, true, out _), "your parties cross where you send them");
    }

    [Test]
    public void TheLeash_EndsAChase()
    {
        var map = Disk(3);
        var band = Band(1, "wolf", Centre(0), BandIntelligence.Instinctive);
        band.homeCell = map.Get(HexCoord.Zero).index;
        band.pursuitLimit = 5;
        Assert.IsNull(WorldPursuit.Leash(map, band, Centre(1)));
        band.chaseTiles = 5;
        Assert.IsNotNull(WorldPursuit.Leash(map, band, Centre(1)), "it has run as far as it will");
        band.chaseTiles = 0;
        band.response = ThreatResponse.DefendsTerritory;
        band.territoryRadius = 2;
        Assert.IsNull(WorldPursuit.Leash(map, band, Centre(0).Neighbor(0)), "inside its ground it defends it");
        Assert.IsNotNull(WorldPursuit.Leash(map, band, Centre(3)), "once you leave its ground it lets you go");
        band.response = ThreatResponse.Hunts;
        map.Get(new HexCoord(2, 0)).settlement = 0;
        Assert.IsNull(WorldPursuit.Leash(map, band, Centre(2)), "a beast follows you anywhere");
        band.intelligence = BandIntelligence.Regular;
        Assert.IsNotNull(WorldPursuit.Leash(map, band, Centre(2)), "a Regular band will not come near a settlement");
        map.Get(new HexCoord(1, 0)).authorityId = WorldAuthority.Player;
        Assert.IsNull(WorldPursuit.Leash(map, band, Centre(1)));
        band.intelligence = BandIntelligence.Smart;
        Assert.IsNotNull(WorldPursuit.Leash(map, band, Centre(1)), "a Smart band will not follow you into your territory");
    }

    [Test]
    public void AFleeingBand_PutsGroundBetweenThem_AndAStalkerKeepsItsDistance()
    {
        var gen = Gen();
        var map = Disk(3);
        var hare = Band(1, "hare", Centre(0));
        var danger = Centre(-1);
        var escape = WorldPursuit.Escape(map, gen, hare, danger);
        Assert.Greater(escape.Count, 0);
        Assert.Greater(HexCoord.Distance(MicroNavigation.Coord(map, escape[escape.Count - 1]), danger), HexCoord.Distance(Centre(0), danger));
        Assert.IsTrue(MicroNavigation.ToNearest(map, gen, Centre(-2), Centre(2), out var way, out _, out _));
        var kept = WorldPursuit.KeepOff(map, way, Centre(2), 2);
        Assert.Less(kept.Count, way.Count);
        Assert.GreaterOrEqual(HexCoord.Distance(MicroNavigation.Coord(map, kept[kept.Count - 1]), Centre(2)), 2, "it waits two hexes off");
    }

    // ===== COLOURS AND FIGHTS =====

    [Test]
    public void ATimidBand_IsMutedOrange_AndFightsOnlyWhenHuntedDown()
    {
        var map = Disk(2);
        var hare = Band(1, "hare", Centre(0));
        hare.stance = EnemyStance.Timid;
        Assert.AreEqual(WorldBattles.Timid, WorldBattles.ColorOf(hare));
        var party = Party(2, Centre(0).Neighbor(0));
        Assert.IsFalse(WorldBattles.Hostile(party, hare), "it means no harm and you mean none");
        Assert.AreEqual(0, WorldBattles.Clashes(new[] { party, hare }).Count);
        party.quarryId = hare.id;
        party.path.Add(Centre(0));
        Assert.IsTrue(WorldBattles.Hostile(party, hare), "caught by a party giving chase, it has to fight");
        var clash = WorldBattles.Clashes(new[] { party, hare }).Single();
        Assert.AreEqual(party, clash.attacker, "the hunter attacks");
        Assert.IsFalse(WorldBattles.Hostile(Band(3, "wolf", Centre(1)), hare), "wild creatures meet as predator and prey, not in battle");
    }

    [Test]
    public void AWaryBand_IsProvokedInsideItsTerritory()
    {
        var map = Disk(3);
        var band = Band(1, "wolf", Centre(0));
        band.stance = EnemyStance.Wary;
        band.homeCell = map.Get(HexCoord.Zero).index;
        band.territoryRadius = 5;
        var party = Party(2, Centre(0).Neighbor(0).Neighbor(0).Neighbor(0));
        Assert.IsFalse(WorldBattles.Provokes(party, band), "three hexes off and standing: not close enough by itself");
        Assert.IsTrue(WorldPursuit.Provokes(map, party, band, 4), "but it stands inside the band's ground, in its sight");
        band.territoryRadius = 1;
        Assert.IsFalse(WorldPursuit.Provokes(map, party, band, 4));
        band.response = ThreatResponse.Tolerates;
        party.path.Add(Centre(0));
        WorldUnits.Place(party, Centre(0).Neighbor(0));
        Assert.IsFalse(WorldPursuit.Provokes(map, party, band, 4), "one that has learned you mean no harm lets you pass");
    }
}
