using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The living map's other half with no scene: suffering on the land (it gathers, fades, is drained, and decides the Path
/// a Formless Mass hatches into; masses pool where it or the Loom's Dissonance runs high, never crowded together); the
/// Eight-Born Paths (drawn by the vault's shares; Carnalix drains and takes no captives, every other Path captures;
/// cleverness by the vault's tiers); habitats (what lives in the water never leaves it, what walks never swims but to
/// ford a river); and a fleeing band that keeps off perilous ground unless there is no other way.
/// </summary>
public class WorldSufferingTests
{
    private static WorldGenSettings Gen()
    {
        var s = new WorldGenSettings();
        s.terrains.Add(new TerrainSpec { id = "plain", name = "Plain", moveCost = 1f });
        s.terrains.Add(new TerrainSpec { id = "lake", name = "Lake", passable = false, water = true });
        s.species.Add(new SpeciesSpec { id = "trout", name = "River Trout", diet = CreatureDiet.Omnivore, subgroup = CreatureSubgroup.Benign, size = CreatureSize.Small, habitat = CreatureHabitat.Water });
        s.species.Add(new SpeciesSpec { id = "hare", name = "Meadow Hare", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Frightful, size = CreatureSize.Small });
        return s;
    }

    // Cells within radius of the origin; the named ones are lake.
    private static WorldMap Disk(int radius, IEnumerable<HexCoord> lake = null)
    {
        var wet = new HashSet<HexCoord>(lake ?? Enumerable.Empty<HexCoord>());
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        foreach (var coord in HexCoord.Spiral(HexCoord.Zero, radius))
        {
            HexHierarchy.ToWorld(coord, HexHierarchy.Meso, out float x, out float y);
            bool water = wet.Contains(coord);
            map.Add(new WorldTile { coord = coord, x = x, y = y, terrain = water ? "lake" : "plain", water = water, lake = water, explored = true, revealed = true, known = true, settlement = -1 });
        }
        return map;
    }

    private static HexCoord Centre(int q, int r = 0) => MicroNavigation.Center(new HexCoord(q, r));

    private static WorldUnit Band(int id, string species, HexCoord at, CreatureHabitat habitat = CreatureHabitat.Land)
    {
        var u = new WorldUnit { id = id, faction = WorldBattles.Wild, species = species, creatures = 3, encounterInitialized = true, habitat = habitat, territoryRadius = 4, pursuitLimit = 12 };
        WorldUnits.Place(u, at);
        return u;
    }

    // ===== SUFFERING =====

    [Test]
    public void SufferingGathers_Fades_AndIsDrainedByKind()
    {
        var scars = new List<SufferingScar>();
        WorldSuffering.Add(scars, 4, Feeling.Dread, 0.3f);
        WorldSuffering.Add(scars, 4, Feeling.Pain, 0.5f);
        Assert.AreEqual(1, scars.Count, "one scar per cell");
        Assert.AreEqual(0.8f, scars[0].feelings.Total, 1e-4f);
        WorldSuffering.Fade(scars, 10f);
        Assert.Less(scars[0].feelings.Total, 0.8f, "the land heals slowly");
        Assert.Greater(scars[0].feelings.Total, 0.6f);
        var fed = new EmotionalRegister();
        scars[0].feelings.Drain(0.5f, fed);
        Assert.AreEqual(fed.Total, scars[0].feelings.Total, 1e-4f, "a mass drinks what the land loses");
        Assert.AreEqual(Feeling.Pain, fed.Dominant);
        WorldSuffering.Add(scars, 9, Feeling.Tumult, 0.005f);
        WorldSuffering.Fade(scars, 1f);
        Assert.IsNull(WorldSuffering.At(scars, 9), "a faint scar closes");
    }

    [Test]
    public void AMassHatchesIntoThePathOfWhatItFedOn()
    {
        EmotionalRegister Of(Feeling w) { var m = new EmotionalRegister(); m.Add(w, 1f); return m; }
        Assert.AreEqual(AtonalPath.Anxithor, WorldSuffering.PathOf(Of(Feeling.Dread)), "dread: fear");
        Assert.AreEqual(AtonalPath.Carnalix, WorldSuffering.PathOf(Of(Feeling.Pain)), "the body's pain");
        Assert.AreEqual(AtonalPath.Signath, WorldSuffering.PathOf(Of(Feeling.Doubt)), "the torn Loom");
        Assert.AreEqual(AtonalPath.Discant, WorldSuffering.PathOf(Of(Feeling.Tumult)), "tumult: grief and joy alike");
    }

    [Test]
    public void MassesPoolWhereSufferingOrDissonanceRunsHigh_NeverCrowded()
    {
        var gen = Gen();
        var map = Disk(6);
        var scars = new List<SufferingScar>();
        WorldSuffering.Add(scars, map.Get(new HexCoord(2, 0)).index, Feeling.Dread, 0.7f);
        WorldSuffering.Add(scars, map.Get(new HexCoord(-2, 0)).index, Feeling.Dread, 0.2f);
        map.Get(new HexCoord(0, 5)).dissonance = 0.9f;
        WorldSuffering.Apply(map, scars);
        var sites = WorldSuffering.SpawnSites(map, gen, null, null);
        CollectionAssert.AreEquivalent(new[] { new HexCoord(2, 0), new HexCoord(0, 5) }, sites.Select(t => t.coord), "too much suffering, or too much Dissonance");
        Assert.AreEqual(new HexCoord(0, 5), sites[0].coord, "the worst first");
        Assert.IsFalse(WorldSuffering.SpawnSites(map, gen, null, new[] { new HexCoord(3, 0) }).Any(t => t.coord == new HexCoord(2, 0)), "not beside another mass");
        Assert.IsFalse(WorldSuffering.SpawnSites(map, gen, cell => cell == map.Get(new HexCoord(2, 0)).index, null).Any(t => t.coord == new HexCoord(2, 0)), "a cell rests after one rose");
    }

    // ===== THE EIGHT-BORN PATHS =====

    [Test]
    public void ThePathsFollowTheVault()
    {
        var drawn = Enumerable.Range(0, 1000).Select(i => AtonalPaths.Draw(null, (i + 0.5) / 1000.0)).ToList();
        CollectionAssert.AreEquivalent(AtonalPaths.Shares.Select(s => s.path), drawn.Distinct(), "every Path is drawn");
        Assert.Greater(drawn.Count(p => p == AtonalPath.Anxithor), drawn.Count(p => p == AtonalPath.Erosyx) * 2, "by the vault's shares: Anxithor 16%, Erosyx 5.2%");
        Assert.AreEqual(AtonalPath.Violux, AtonalPaths.Draw(new List<AtonalPath> { AtonalPath.Violux }, 0.9), "a threat's own list wins");

        var carnalix = AtonalPaths.Of(AtonalPath.Carnalix);
        Assert.IsFalse(carnalix.captures, "Carnalix takes no captives");
        Assert.IsTrue(carnalix.drains, "it drains");
        Assert.AreEqual(BandIntelligence.Instinctive, carnalix.intelligence, "animal-like (tier 1)");
        foreach (var path in AtonalPaths.Shares.Select(s => s.path).Where(p => p != AtonalPath.Carnalix))
            Assert.IsTrue(AtonalPaths.Of(path).captures, $"{path} takes captives");
        Assert.AreEqual(BandIntelligence.Smart, AtonalPaths.Of(AtonalPath.Animach).intelligence, "the smartest (tier 4)");
        Assert.AreEqual(BandIntelligence.Smart, AtonalPaths.Of(AtonalPath.Violux).intelligence, "mirrors (tier 3)");
        Assert.IsTrue(AtonalPaths.Of(AtonalPath.Erosyx).soloOnly, "do not face them alone");
        Assert.Greater(AtonalPaths.Of(AtonalPath.Violux).pursuit, AtonalPaths.Of(AtonalPath.Discant).pursuit * 2, "a Violux may pursue for years");

        var band = new WorldUnit { id = 1, faction = WorldBattles.Wild, threat = "nest", creatures = 2, atonalPath = AtonalPath.Anxithor, territoryRadius = 3 };
        WorldPursuit.ApplyPath(band);
        Assert.AreEqual(ThreatResponse.DefendsTerritory, band.response, "Anxithor guards its ground");
        Assert.GreaterOrEqual(band.territoryRadius, 6);
    }

    [Test]
    public void ACarnalixPreysOnAnyCreature_OthersOnlyOnTheirPrey()
    {
        var gen = Gen();
        var hare = Band(2, "hare", Centre(1));
        hare.identity = BandIdentity.Creature;
        var carnalix = new WorldUnit { id = 1, faction = WorldBattles.Wild, threat = "nest", creatures = 2, identity = BandIdentity.Atonalis, atonalPath = AtonalPath.Carnalix };
        Assert.IsTrue(WorldPursuit.Predates(gen, carnalix, hare), "flesh is flesh");
        carnalix.atonalPath = AtonalPath.Discant;
        Assert.IsFalse(WorldPursuit.Predates(gen, carnalix, hare), "a Discant feeds on despair, not meat");
        var other = new WorldUnit { id = 3, faction = WorldBattles.Wild, threat = "nest", creatures = 2, identity = BandIdentity.Atonalis, atonalPath = AtonalPath.Carnalix };
        carnalix.atonalPath = AtonalPath.Carnalix;
        Assert.IsFalse(WorldPursuit.Predates(gen, carnalix, other), "not its own kind");
    }

    // ===== HABITATS =====

    [Test]
    public void WhatLivesInTheWaterNeverLeavesIt_AndWhatWalksNeverSwims()
    {
        var gen = Gen();
        var lake = new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(0, 1) };
        var map = Disk(3, lake);
        var grid = MicroNavigation.Grid(map, gen);
        var fish = Band(1, "trout", Centre(0), CreatureHabitat.Water);
        Assert.IsTrue(WorldPursuit.Route(map, gen, fish, Centre(1), false, out var swim), "across its lake");
        Assert.IsTrue(swim.All(id => WorldPursuit.IsWater(map, grid, id)), "every hex of the way is water");
        Assert.IsFalse(WorldPursuit.Route(map, gen, fish, Centre(-2), false, out _), "never onto land");
        var shore = Centre(-2);
        Assert.IsFalse(WorldPursuit.Route(map, gen, fish, shore, true, out _), "it will not follow you out: the shore is as far as it comes");

        var hare = Band(2, "hare", Centre(-2), CreatureHabitat.Land);
        Assert.IsFalse(WorldPursuit.CanStand(map, grid, hare, MicroNavigation.Index(map, Centre(0))), "what walks cannot stand in a lake");
        Assert.IsFalse(WorldPursuit.Route(map, gen, hare, Centre(0), false, out _));
        var eel = Band(3, "eel", Centre(-2, 1), CreatureHabitat.Amphibious);
        Assert.IsTrue(WorldPursuit.Route(map, gen, eel, Centre(1), false, out var both), "an amphibian goes both ways");
        Assert.IsTrue(both.Any(id => WorldPursuit.IsWater(map, grid, id)) && both.Any(id => !WorldPursuit.IsWater(map, grid, id)));
        var party = new WorldUnit { id = 4, leader = "Iris" };
        WorldUnits.Place(party, Centre(-2, -1));
        Assert.AreEqual(float.PositiveInfinity, WorldPursuit.StepCost(map, grid, party, MicroNavigation.Index(map, Centre(-1, 0)), MicroNavigation.Index(map, Centre(0))), "your parties walk");
    }

    // ===== SMARTER FLEEING =====

    [Test]
    public void AFleeingBand_KeepsOffPerilousGround_UnlessThereIsNoOtherWay()
    {
        var gen = Gen();
        var map = Disk(4);
        // Everything east of the hare is perilous (a predatory bloom's hazard); the threat comes from the west.
        foreach (var t in map.Tiles.Where(t => t.coord.q >= 1)) t.danger = 0.9f;
        var hare = Band(1, "hare", Centre(0));
        hare.homeCell = map.Get(HexCoord.Zero).index;
        var danger = Centre(-1);
        var escape = WorldPursuit.Escape(map, gen, hare, danger);
        Assert.Greater(escape.Count, 0);
        var grid = MicroNavigation.Grid(map, gen);
        var end = escape[escape.Count - 1];
        Assert.Greater(HexCoord.Distance(MicroNavigation.Coord(map, end), danger), HexCoord.Distance(Centre(0), danger), "away from the threat");
        Assert.Less(map[end / MicroNavigation.PerCell].danger, 0.5f, "not into the perilous ground");
        Assert.IsTrue(escape.All(id => map[id / MicroNavigation.PerCell].danger < 0.5f), "not even through it");

        // Nowhere safe left: it runs into peril rather than stand.
        foreach (var t in map.Tiles.Where(t => t.coord != HexCoord.Zero)) t.danger = 0.9f;
        Assert.Greater(WorldPursuit.Escape(map, gen, hare, danger).Count, 0, "no other way: it takes the perilous one");
    }
}
