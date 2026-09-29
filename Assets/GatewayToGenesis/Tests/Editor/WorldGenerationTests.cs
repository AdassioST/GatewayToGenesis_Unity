using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The world generator's rules with no scene (Docs/Planning/WORLD_GENERATION.md §9, roadmap WG01-WG13): the
/// index-7 hierarchy, the stencil and handmade tiles, the slot solver, the continent (ocean ring, mainland, seams,
/// water), the magic (junctions by family, Age changes, silver rivers), features and the zoom rules. Uses its own
/// small catalog and a copy of the stencil, so it needs no Resources; ContentTests checks the real content.
/// </summary>
public class WorldGenerationTests
{
    // The owner's stencil (Resources/World/Composition.txt, Sept 26, 2026).
    private const string Stencil = @"
W  W  W  W  W  W  W  W  I  I  I  I  W  W  W  W  W  W  W  W
W  W  W  W  W  W  W  W  I  Q7 Q7 I  W  W  W  W  W  W  W  W
W  W  W  W  W  I  I  I  I  Q7 Q7 I  I  I  I  W  W  W  W  W
W  W  W  W  W  I  Q4 Q4 I  Q7 Q7 I  Q4 Q4 I  W  W  W  W  W
W  W  W  W  W  I  Q4 Q4 I  Q7 Q7 I  Q4 Q4 I  W  W  W  W  W
W  W  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  W  W
W  W  I  Q4 Q4 I  Q3 Q3 I  Q3 Q3 I  Q3 Q3 I  Q4 Q4 I  W  W
W  W  I  Q4 Q4 I  Q3 Q3 I  Q3 Q3 I  Q3 Q3 I  Q4 Q4 I  W  W
I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I
I  Q7 Q7 Q7 Q7 I  Q2 Q2 I  Q1 Q1 I  Q2 Q2 I  Q7 Q7 Q7 Q7 I
I  Q7 Q7 Q7 Q7 I  Q2 Q2 I  Q1 Q1 I  Q2 Q2 I  Q7 Q7 Q7 Q7 I
I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I
W  W  I  Q6 Q6 I  Q6 Q6 I  Q3 Q3 I  Q5 Q5 I  Q5 Q5 I  W  W
W  W  I  Q6 Q6 I  Q6 Q6 I  Q3 Q3 I  Q5 Q5 I  Q5 Q5 I  W  W
W  W  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  I  W  W
W  W  W  W  W  I  Q6 Q6 I  Q7 Q7 I  Q5 Q5 I  W  W  W  W  W
W  W  W  W  W  I  Q6 Q6 I  Q7 Q7 I  Q5 Q5 I  W  W  W  W  W
W  W  W  W  W  I  I  I  I  Q7 Q7 I  I  I  I  W  W  W  W  W
W  W  W  W  W  W  W  W  I  Q7 Q7 I  W  W  W  W  W  W  W  W
W  W  W  W  W  W  W  W  I  I  I  I  W  W  W  W  W  W  W  W";

    private const string RingGlade = @"
tile: grove/ring-glade
macrobiome: grove
weight: 2
rotations: any
legend: V=wood G=glade L=lake
heights: L=0.31
---
  V V V
 V G G V
V G L G V
 V G G V
  V V V";

    private const string Ridge = @"
tile: taiga/ridge
macrobiome: taiga
rotations: 0 3
legend: T=wood P=peaks
---
T T P P P T T";

    private static readonly string[] Q5Catalog = { "taiga", "rift", "wind", "auric" };

    private static TerrainRule Rule(string terrain, float weight = 1f, float minE = 0f, float maxE = 1f) =>
        new TerrainRule { terrain = terrain, weight = weight, minElevation = minE, maxElevation = maxE };

    private static MacroBiomeSpec Biome(string id, string ground, float elevation = 0.45f, params int[] orientations)
    {
        var biome = new MacroBiomeSpec { id = id, name = id, elevation = elevation, tileCoverage = 0.4f };
        biome.terrains.Add(Rule(ground, 3f, 0f, 0.8f));
        biome.terrains.Add(Rule("peaks", 2f, 0.8f, 1f));
        biome.orientations.AddRange(orientations);
        return biome;
    }

    internal static WorldGenSettings Settings(int cellsPerStencilCell = 4)
    {
        var s = new WorldGenSettings { cellsPerStencilCell = cellsPerStencilCell, seamReach = cellsPerStencilCell + 2, riverThreshold = 14f, majorRiverThreshold = 60f, startRevealRadius = 3 };
        foreach (var water in new[] { "deep-ocean", "shallows", "lake" }) s.terrains.Add(new TerrainSpec { id = water, name = water, water = true, passable = false, fertility = 0f });
        s.terrains.Add(new TerrainSpec { id = "peaks", name = "Peaks", passable = false, fertility = 0f });
        foreach (var ground in new[] { "plain", "wood", "glade", "ash", "ruin", "steppe", "scar" }) s.terrains.Add(new TerrainSpec { id = ground, name = ground, fertility = 0.5f });
        s.intersectionTerrains.Add(Rule("peaks", 3f, 0.8f, 1f));
        s.intersectionTerrains.Add(Rule("plain", 2f, 0f, 0.82f));
        s.macroBiomes.Add(Biome("start", "plain", 0.4f));
        s.macroBiomes.Add(Biome("grove", "wood"));
        s.macroBiomes.Add(Biome("expanse", "steppe", 0.42f));
        s.macroBiomes.Add(Biome("ruins", "ruin"));
        s.macroBiomes.Add(Biome("ashes", "ash"));
        s.macroBiomes.Add(Biome("taiga", "wood", 0.55f, 1, 2));
        s.macroBiomes.Add(Biome("rift", "scar", 0.5f));
        s.macroBiomes.Add(Biome("wind", "plain", 0.58f));
        s.macroBiomes.Add(Biome("auric", "glade", 0.4f));
        s.macroBiomes.Add(Biome("outer", "plain", 0.5f));
        s.quadrants.Add(new QuadrantSpec { id = "Q1", macroBiomes = { "start" } });
        s.quadrants.Add(new QuadrantSpec { id = "Q2", macroBiomes = { "grove", "expanse" } });
        s.quadrants.Add(new QuadrantSpec { id = "Q3", macroBiomes = { "ruins" }, allowRepeats = true });
        s.quadrants.Add(new QuadrantSpec { id = "Q4", macroBiomes = { "ashes" }, allowRepeats = true });
        s.quadrants.Add(new QuadrantSpec { id = "Q5", macroBiomes = Q5Catalog.ToList(), backbone = 0.35f });
        s.quadrants.Add(new QuadrantSpec { id = "Q6", macroBiomes = { "ruins" }, allowRepeats = true });
        s.quadrants.Add(new QuadrantSpec { id = "Q7", macroBiomes = { "outer" }, allowRepeats = true });
        s.features.Add(new FeatureSpec { id = "capital" });
        s.features.Add(new FeatureSpec { id = "orchard", tag = "fertile", count = 4, minDistance = 3, maxDistance = 40, spacing = 3, prefers = FeatureSpec.Preference.LandFertility });
        s.features.Add(new FeatureSpec { id = "tower", count = 2, minDistance = 5, spacing = 8, visibleFromAfar = true });
        s.features.Add(new FeatureSpec { id = "enclave", count = 3, minAge = 1, mayAppearOnExplored = true });
        return s;
    }

    internal static WorldStencil ParsedStencil() => WorldStencil.Parse(Stencil);

    internal static List<TileTemplate> Tiles() => new List<TileTemplate> { TileTemplate.Parse(RingGlade), TileTemplate.Parse(Ridge) };

    private static readonly Dictionary<int, WorldMap> Cache = new Dictionary<int, WorldMap>();

    // Worlds are cached per seed: tests only read them (or copy what they change).
    private static WorldMap World(int seed)
    {
        if (!Cache.TryGetValue(seed, out var map)) Cache[seed] = map = WorldGenerator.Generate(seed, Settings(), ParsedStencil(), Tiles());
        return map;
    }

    private static WorldMap FreshWorld(int seed) => WorldGenerator.Generate(seed, Settings(), ParsedStencil(), Tiles());

    // ===== HIERARCHY (WG02) =====

    [Test]
    public void Hierarchy_EveryChildHasExactlyOneParentAndEveryParentSevenChildren()
    {
        var owners = new Dictionary<HexCoord, HexCoord>();
        for (int q = -12; q <= 12; q++)
        {
            for (int r = -12; r <= 12; r++)
            {
                var parent = new HexCoord(q, r);
                var children = HexHierarchy.Children(parent).ToList();
                Assert.AreEqual(7, children.Distinct().Count(), $"children of {parent}");
                foreach (var child in children)
                {
                    Assert.AreEqual(parent, HexHierarchy.Parent(child), $"child {child} of {parent}");
                    Assert.IsFalse(owners.ContainsKey(child), $"{child} is owned twice");
                    owners[child] = parent;
                }
            }
        }
        // Every hex of a patch (negative coordinates too) has a parent whose children include it.
        foreach (var hex in HexCoord.Spiral(new HexCoord(-40, 25), 20))
            CollectionAssert.Contains(HexHierarchy.Children(HexHierarchy.Parent(hex)).ToList(), hex);
    }

    [Test]
    public void Hierarchy_AMacroAggregateHolds49CellsAnd343MicroHexes()
    {
        var macro = new HexCoord(-3, 5);
        var meso = HexHierarchy.Children(macro).SelectMany(HexHierarchy.Children).ToList();
        Assert.AreEqual(49, meso.Distinct().Count());
        Assert.IsTrue(meso.All(m => HexHierarchy.Ancestor(m, 2) == macro));
        var micro = meso.SelectMany(HexHierarchy.Children).ToList();
        Assert.AreEqual(343, micro.Distinct().Count());
        Assert.IsTrue(micro.All(m => HexHierarchy.Ancestor(m, 3) == macro));
    }

    [Test]
    public void Hierarchy_AWorldPointFindsTheCellThatOwnsIt()
    {
        foreach (var meso in HexCoord.Spiral(new HexCoord(4, -9), 6))
        {
            HexHierarchy.ToWorld(meso, HexHierarchy.Meso, out float x, out float y);
            Assert.AreEqual(meso, HexHierarchy.CellAt(x, y, HexHierarchy.Meso));
            Assert.AreEqual(meso, HexHierarchy.CellAt(x + 0.4f, y - 0.3f, HexHierarchy.Meso), "anywhere in the centre hex");
        }
        Assert.AreEqual(Math.Sqrt(3.0) * Math.Sqrt(7.0), HexHierarchy.Spacing(HexHierarchy.Meso), 1e-9);
    }

    [Test]
    public void Hex_RotationTurnsTheDirectionsAndSixTurnsAreWhole()
    {
        for (int i = 0; i < 6; i++) Assert.AreEqual(HexCoord.Directions[(i + 1) % 6], HexCoord.Directions[i].Rotate(1));
        var hex = new HexCoord(3, -7);
        Assert.AreEqual(hex, hex.Rotate(6));
        Assert.AreEqual(hex, hex.Rotate(2).Rotate(-2));
        Assert.AreEqual(HexCoord.Distance(HexCoord.Zero, hex), HexCoord.Distance(HexCoord.Zero, hex.Rotate(4)));
    }

    // ===== STENCIL AND TILES (WG01) =====

    [Test]
    public void Stencil_FindsTheSlotsTheirInstancesAndTags()
    {
        var stencil = ParsedStencil();
        Assert.AreEqual(20, stencil.Width);
        Assert.AreEqual(20, stencil.Height);
        Assert.AreEqual(1, stencil.SlotsOf("Q1").Count());
        Assert.AreEqual(2, stencil.SlotsOf("Q2").Count());
        Assert.AreEqual(3, stencil.SlotsOf("Q5").Count());
        Assert.AreEqual(1, stencil.SlotsOf("Q5").Select(s => s.instance).Distinct().Count(), "Q5's blocks, only an intersection apart, are one instance");
        Assert.AreEqual(2, stencil.SlotsOf("Q2").Select(s => s.instance).Distinct().Count(), "Q2's blocks lie either side of Q1");
        Assert.AreEqual(4, stencil.SlotsOf("Q7").Select(s => s.instance).Distinct().Count());
        Assert.AreEqual(stencil.Slots.Count, stencil.Slots.Select(s => s.id).Distinct().Count(), "slot ids are unique");
        var start = stencil.SlotsOf("Q1").Single();
        Assert.IsTrue(start.tags.Contains("core") && start.tags.Contains("inland"));
        Assert.IsTrue(stencil.SlotsOf("Q7").Any(s => s.tags.Contains("north")) && stencil.SlotsOf("Q7").Any(s => s.tags.Contains("south")));
        Assert.IsTrue(start.neighbours.Select(n => stencil.Slots[n].quadrant).Contains("Q2"));
    }

    [Test]
    public void Stencil_RejectsRaggedRowsAndUnknownCells()
    {
        Assert.Throws<FormatException>(() => WorldStencil.Parse("W W W\nW P"));
        Assert.Throws<FormatException>(() => WorldStencil.Parse("W X W"));
        Assert.Throws<FormatException>(() => WorldStencil.Parse("# only a comment"));
    }

    [Test]
    public void Tile_ReadsTheHoneycombCentresItAndTurnsIt()
    {
        var tile = TileTemplate.Parse(RingGlade);
        Assert.AreEqual("grove/ring-glade", tile.id);
        Assert.AreEqual(19, tile.cells.Count);
        Assert.AreEqual(2, tile.Radius);
        Assert.AreEqual("lake", tile.cells[HexCoord.Zero]);
        Assert.AreEqual(6, tile.rotations.Count);
        Assert.AreEqual(12, tile.cells.Values.Count(v => v == "wood"), "the ring of woods");
        foreach (int rotation in tile.rotations)
        {
            var turned = tile.Rotated(rotation).ToList();
            Assert.AreEqual(19, turned.Select(p => p.Key).Distinct().Count());
            Assert.AreEqual("lake", turned.Single(p => p.Key == HexCoord.Zero).Value);
            Assert.AreEqual(0.31f, tile.HeightAt(HexCoord.Zero, rotation, out bool has), 1e-6);
            Assert.IsTrue(has);
        }
        var ridge = TileTemplate.Parse(Ridge);
        CollectionAssert.AreEqual(new[] { 0, 3 }, ridge.rotations);
        Assert.AreEqual(7, ridge.cells.Count);
    }

    [Test]
    public void Tile_ReportsDrawingMistakes()
    {
        Assert.Throws<FormatException>(() => TileTemplate.Parse("tile: a\nbiome: b\nlegend: A=x\n---\nA A\n A  A"), "a cell between two cells");
        Assert.Throws<FormatException>(() => TileTemplate.Parse("tile: a\nbiome: b\nlegend: A=x\n---\nA B"), "a character outside the legend");
        Assert.Throws<FormatException>(() => TileTemplate.Parse("tile: a\nlegend: A=x\n---\nA"), "no biome");
        Assert.Throws<FormatException>(() => TileTemplate.Parse("tile: a\nbiome: b\nrotations: 7\nlegend: A=x\n---\nA"), "rotation out of range");
    }

    // ===== SLOTS (WG04) =====

    [Test]
    public void Solver_Q2GetsBothItsMacroBiomesInEitherOrder()
    {
        var stencil = ParsedStencil();
        var orders = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var solution = SlotSolver.Solve(stencil, Settings(), seed);
            CollectionAssert.IsEmpty(solution.errors, $"seed {seed}");
            var s2 = solution.placements.Where(p => p.slot.quadrant == "Q2").OrderBy(p => p.slot.centerCol).Select(p => p.macroBiome.id).ToList();
            CollectionAssert.AreEquivalent(new[] { "grove", "expanse" }, s2, $"seed {seed}");
            orders.Add(string.Join(",", s2));
        }
        Assert.AreEqual(2, orders.Count, "both left/right assignments occur");
    }

    [Test]
    public void Solver_Q5DrawsThreeDistinctMacroBiomesOfItsFour()
    {
        var stencil = ParsedStencil();
        var trios = new HashSet<string>();
        var layouts = new HashSet<string>();
        for (int seed = 1; seed <= 60; seed++)
        {
            var solution = SlotSolver.Solve(stencil, Settings(), seed);
            var s5 = solution.placements.Where(p => p.slot.quadrant == "Q5").OrderBy(p => p.slot.index).ToList();
            Assert.AreEqual(3, s5.Count);
            Assert.AreEqual(3, s5.Select(p => p.macroBiome.id).Distinct().Count(), $"seed {seed}: no biome twice");
            Assert.IsTrue(s5.All(p => Q5Catalog.Contains(p.macroBiome.id)));
            trios.Add(string.Join(",", s5.Select(p => p.macroBiome.id).OrderBy(id => id)));
            layouts.Add(string.Join(",", s5.Select(p => p.macroBiome.id)));
        }
        Assert.Greater(trios.Count, 1, "the seed chooses which three appear");
        Assert.Greater(layouts.Count, 4, "and where they go");
    }

    [Test]
    public void Solver_KeepsEachBiomesAllowedOrientations()
    {
        var stencil = ParsedStencil();
        var seen = new HashSet<int>();
        for (int seed = 1; seed <= 60; seed++)
        {
            foreach (var p in SlotSolver.Solve(stencil, Settings(), seed).placements.Where(p => p.macroBiome != null))
            {
                Assert.That(p.orientation, Is.InRange(0, 5));
                if (p.macroBiome.id == "taiga")
                {
                    CollectionAssert.Contains(new[] { 1, 2 }, p.orientation, $"seed {seed}: taiga's uphill faces north");
                    seen.Add(p.orientation);
                }
            }
        }
        Assert.AreEqual(2, seen.Count, "both allowed orientations occur");
    }

    [Test]
    public void Solver_ReportsCatalogsThatCannotFillTheirSlots()
    {
        var settings = Settings();
        settings.quadrants.RemoveAll(s => s.id == "Q6");
        settings.Quadrant("Q3").macroBiomes.Add("no-such-biome");
        settings.Quadrant("Q4").allowRepeats = false;
        var solution = SlotSolver.Solve(ParsedStencil(), settings, 3);
        Assert.IsTrue(solution.errors.Any(e => e.Contains("Q6") && e.Contains("no QuadrantSpec")));
        Assert.IsTrue(solution.errors.Any(e => e.Contains("no-such-biome")));
        Assert.IsTrue(solution.errors.Any(e => e.Contains("Q4") && e.Contains("repeats")));
        Assert.IsTrue(solution.placements.Where(p => p.slot.quadrant == "Q6").All(p => p.macroBiome == null), "an unfilled slot is left to the intersections, not given a guess");
    }

    [Test]
    public void Solver_RelaxesAnImpossibleRequirementAndSaysSo()
    {
        var settings = Settings();
        settings.MacroBiome("start").requires.Add("coast"); // Q1 is inland
        var solution = SlotSolver.Solve(ParsedStencil(), settings, 5);
        Assert.IsTrue(solution.repairs.Any(r => r.Contains("tag requirements")));
        Assert.IsTrue(solution.errors.Any(e => e.Contains("requires coast")));
    }

    // ===== THE CONTINENT (WG03, WG05, WG06) =====

    private static string Fingerprint(WorldMap map) =>
        string.Join(";", map.Tiles.Select(t => $"{t.coord}{t.terrain}{t.macroBiome}{t.sector}{t.feature}{t.river}{t.explored}{t.elevation:0.0000}"));

    [Test]
    public void World_TheSameSeedVersionAndCatalogMakeTheSameWorld()
    {
        Assert.AreEqual(Fingerprint(World(42)), Fingerprint(FreshWorld(42)));
        Assert.AreNotEqual(Fingerprint(World(42)), Fingerprint(World(43)));
        Assert.AreEqual(World(42).Report.catalogHash, World(43).Report.catalogHash);
        Assert.AreEqual(WorldGenerator.Version, World(42).Report.version);
    }

    [Test]
    public void World_EveryCellHasOneOwnerAndGround()
    {
        var map = World(42);
        Assert.AreEqual(map.Count, map.Tiles.Select(t => t.coord).Distinct().Count());
        Assert.IsTrue(map.Tiles.All(t => !string.IsNullOrEmpty(t.terrain)), "no gaps");
        Assert.IsTrue(map.Tiles.All(t => t.water == (Settings().Terrain(t.terrain)?.water == true)), "water cells carry water ground and only they do");
        Assert.Greater(map.Tiles.Count(t => t.composition == WorldComposition.Intersection && !t.water), 0, "the intersections are land in places");
        Assert.Greater(map.Tiles.Count(t => t.composition == WorldComposition.MacroBiome), map.Count / 10);
        CollectionAssert.IsEmpty(map.Report.errors, map.Report.ToString());
    }

    private static bool[] Mainland(WorldMap map)
    {
        var reached = new bool[map.Count];
        var capital = map.Get(map.Capital);
        var open = new Queue<int>();
        open.Enqueue(capital.index);
        reached[capital.index] = true;
        while (open.Count > 0)
        {
            int c = open.Dequeue();
            for (int d = 0; d < 6; d++)
            {
                int n = map.Neighbour(c, d);
                if (n >= 0 && !reached[n] && !map[n].water)
                {
                    reached[n] = true;
                    open.Enqueue(n);
                }
            }
        }
        return reached;
    }

    [TestCase(42)]
    [TestCase(7)]
    public void World_TheMainlandIsConnectedAndTheOceanRingEnclosesIt(int seed)
    {
        var map = World(seed);
        var mainland = Mainland(map);
        foreach (var slot in map.Stencil.Slots)
            Assert.IsTrue(map.Tiles.Any(t => t.slot == slot.index && mainland[t.index]), $"{slot.id} joins the mainland");
        // The ring: every cell at the world's edge is open sea, and none of the mainland comes near it.
        var edge = map.Tiles.Where(t => Enumerable.Range(0, 6).Any(d => map.Neighbour(t.index, d) < 0)).ToList();
        Assert.IsTrue(edge.All(t => t.water && !t.lake));
        int width = map.Tiles.Where(t => mainland[t.index]).Min(t => edge.Min(e => HexCoord.Distance(e.coord, t.coord)));
        Assert.GreaterOrEqual(width, Settings().oceanRingWidth);
    }

    [Test]
    public void World_ASuiteOfSeedsBuildsWithoutErrorsAndKeepsItsContracts()
    {
        for (int seed = 100; seed < 116; seed++)
        {
            var map = FreshWorld(seed);
            CollectionAssert.IsEmpty(map.Report.errors, $"seed {seed}:\n{map.Report}");
            var mainland = Mainland(map);
            Assert.IsTrue(map.Stencil.Slots.All(slot => map.Tiles.Any(t => t.slot == slot.index && mainland[t.index])), $"seed {seed}: every slot joins the mainland");
            Assert.AreEqual("Q1", map.Get(map.Capital).quadrant, $"seed {seed}");
            var s2 = map.Placements.Where(p => p.slot.quadrant == "Q2").Select(p => p.macroBiome.id).ToList();
            CollectionAssert.AreEquivalent(new[] { "grove", "expanse" }, s2, $"seed {seed}");
            Assert.AreEqual(3, map.Placements.Where(p => p.slot.quadrant == "Q5").Select(p => p.macroBiome.id).Distinct().Count(), $"seed {seed}");
        }
    }

    [Test]
    public void World_WaterRunsDownhillToTheSea()
    {
        var map = World(42);
        foreach (var t in map.Tiles.Where(t => !t.water || t.lake))
        {
            int steps = 0;
            var c = t;
            while (c.downstream >= 0 && steps++ < map.Count)
            {
                var next = map[c.downstream];
                Assert.LessOrEqual(next.elevation, c.elevation + 1e-4f, $"{c.coord} runs uphill to {next.coord}");
                c = next;
            }
            Assert.IsTrue(c.water && !c.lake, $"{t.coord} drains to the sea");
        }
        Assert.Greater(map.Rivers.Count, 0);
        foreach (var river in map.Rivers)
        {
            var last = map[river.cells[river.cells.Count - 1]];
            Assert.IsTrue(last.water || last.river, "a river ends in water or in another river");
            for (int i = 1; i < river.cells.Count; i++)
            {
                var a = map[river.cells[i - 1]];
                if (a.lake) continue; // a lake's outlet
                Assert.AreEqual(river.cells[i], a.downstream, "a river follows the drainage");
            }
        }
    }

    [Test]
    public void Sectors_PointsFallInTheirCompassWedgeOrTheCentre()
    {
        Assert.AreEqual(CompassSector.Central, WorldSectors.Of(0.1f, -0.2f, 3f));
        Assert.AreEqual(CompassSector.North, WorldSectors.Of(0f, 2f, 3f), "y grows north");
        Assert.AreEqual(CompassSector.South, WorldSectors.Of(0.2f, -2f, 3f));
        Assert.AreEqual(CompassSector.East, WorldSectors.Of(2f, 0.3f, 3f));
        Assert.AreEqual(CompassSector.West, WorldSectors.Of(-2f, -0.3f, 3f));
        Assert.AreEqual(CompassSector.NorthEast, WorldSectors.Of(2f, 2f, 3f));
        Assert.AreEqual(CompassSector.SouthWest, WorldSectors.Of(-2f, -2f, 3f));
        Assert.AreEqual(CompassSector.NorthWest, WorldSectors.Of(-2f, 2f, 3f));
        Assert.AreEqual(CompassSector.SouthEast, WorldSectors.Of(2f, -2f, 3f));
        Assert.AreEqual("North-East Sector", WorldSectors.Name(CompassSector.NorthEast));
        Assert.IsNull(WorldSectors.Name(CompassSector.None));
    }

    [Test]
    public void Sectors_DivideEveryMacroBiomeIntoNine()
    {
        var map = World(42);
        Assert.IsTrue(map.Tiles.All(t => (t.sector != CompassSector.None) == (t.composition == WorldComposition.MacroBiome && t.slot >= 0)),
            "every Macro Biome cell has a Sector; intersections and the ocean have none");
        foreach (var slot in map.Tiles.Where(t => t.sector != CompassSector.None).GroupBy(t => t.slot))
        {
            var cells = slot.ToList();
            var bySector = cells.GroupBy(t => t.sector).ToDictionary(g => g.Key, g => g.ToList());
            Assert.AreEqual(9, bySector.Count, $"slot {slot.Key} has all nine Sectors");
            float central = bySector[CompassSector.Central].Count / (float)cells.Count;
            Assert.That(central, Is.InRange(0.05f, 0.2f), $"slot {slot.Key}: the Central Sector is about a ninth");
            Assert.Greater(bySector[CompassSector.North].Average(t => t.y), bySector[CompassSector.South].Average(t => t.y), $"slot {slot.Key}: north lies north");
            Assert.Greater(bySector[CompassSector.East].Average(t => t.x), bySector[CompassSector.West].Average(t => t.x), $"slot {slot.Key}: east lies east");
        }
    }

    [TestCase(42)]
    [TestCase(11)]
    public void World_TheCapitalStandsInQ1OnFreshwater(int seed)
    {
        var map = World(seed);
        var capital = map.Get(map.Capital);
        Assert.AreEqual("Q1", capital.quadrant);
        Assert.AreEqual(WorldComposition.MacroBiome, capital.composition);
        Assert.IsFalse(capital.water);
        Assert.AreEqual("capital", capital.feature);
        Assert.IsTrue(capital.explored);
        Assert.IsTrue(HexCoord.Spiral(map.Capital, 3).Select(map.Get).Any(t => t != null && (t.river || t.lake)), "freshwater within three cells");
        var around = HexCoord.Spiral(map.Capital, 5).Select(map.Get).Where(t => t != null).ToList();
        Assert.Greater(around.Count(t => !t.water), around.Count * 0.6f, "the capital stands on land, not on an island in a lake");
        // No hollow floods into a vast lake: generated lakes stay within the size limit (handmade pools aside).
        var seen = new HashSet<int>();
        foreach (var lake in map.Tiles.Where(t => t.lake && t.handmadeTile == null))
        {
            if (!seen.Add(lake.index)) continue;
            int size = 0;
            var open = new Stack<WorldTile>();
            open.Push(lake);
            while (open.Count > 0)
            {
                var c = open.Pop();
                size++;
                foreach (var n in map.NeighboursOf(c).Where(n => n.lake && n.handmadeTile == null && seen.Add(n.index))) open.Push(n);
            }
            Assert.LessOrEqual(size, Settings().maxLakeCells, $"a lake of {size} cells at {lake.coord}");
        }
        Assert.IsTrue(HexCoord.Spiral(map.Capital, 3).Select(map.Get).All(t => t == null || t.revealed));
    }

    [Test]
    public void World_HandmadeTilesAreStampedWholeInsideTheirBiome()
    {
        var map = World(42);
        var stamped = map.Tiles.Where(t => t.handmadeTile != null).ToList();
        Assert.Greater(stamped.Count, 0);
        Assert.IsTrue(stamped.All(t => t.composition == WorldComposition.MacroBiome && !t.seam), "only in protected interiors");
        Assert.IsTrue(stamped.Where(t => t.handmadeTile == "grove/ring-glade").All(t => t.macroBiome == "grove"));
        Assert.IsTrue(stamped.Where(t => t.handmadeTile == "taiga/ridge").All(t => t.macroBiome == "taiga"));
        // The glade's pool is a lake; each stamp keeps the drawing's 19 cells.
        var glades = map.Report.tiles.Count(t => t.Contains("grove/ring-glade"));
        Assert.AreEqual(glades * 19, stamped.Count(t => t.handmadeTile == "grove/ring-glade"));
        Assert.AreEqual(glades, stamped.Count(t => t.handmadeTile == "grove/ring-glade" && t.lake));
    }

    [Test]
    public void World_SeamsBlendTheNeighboursAndQ5ReadsAsOneRange()
    {
        var map = World(42);
        var settings = Settings();
        // The intersections carry the neighbouring Macro Biomes' ground, not only its own.
        var landP = map.Tiles.Where(t => t.composition == WorldComposition.Intersection && !t.water).ToList();
        Assert.Greater(landP.Count(t => t.terrain != "plain" && t.terrain != "peaks"), 0);
        // The intersections inside Q5 stand higher than Q5's slots on average: a range, not a flat seam.
        var s5Slots = map.Tiles.Where(t => t.quadrant == "Q5" && t.composition == WorldComposition.MacroBiome && !t.water).ToList();
        var s5Seams = map.Tiles.Where(t => t.quadrant == "Q5" && t.composition == WorldComposition.Intersection && !t.water).ToList();
        Assert.Greater(s5Seams.Count, 0);
        Assert.Greater(s5Seams.Max(t => t.elevation), settings.seaLevel + 0.4f, "Q5's range has peaks");
        // No cliff walls: neighbouring land cells in the intersections never differ by more than the steepest relief allows.
        float step = map.Tiles.Where(t => t.seam && !t.water).SelectMany(t => map.NeighboursOf(t).Where(n => !n.water).Select(n => Math.Abs(n.elevation - t.elevation))).DefaultIfEmpty(0f).Max();
        Assert.Less(step, 0.35f);
    }

    [Test]
    public void World_ExploringAroundATargetRevealsAndExtendsTheFrontier()
    {
        var map = FreshWorld(8);
        Func<string, bool> passable = id => Settings().Terrain(id)?.passable != false;
        var frontier = map.Frontier(passable);
        Assert.Greater(frontier.Count, 0);
        Assert.IsTrue(frontier.All(t => !t.explored && t.revealed && !t.water && map.TouchesExplored(t.coord)));
        var target = frontier[0];
        int before = map.ExploredCount;
        var found = map.ExploreAround(target.coord, 1, 3, passable);
        Assert.Greater(found.Count, 0);
        Assert.AreEqual(before + found.Count, map.ExploredCount);
        Assert.IsTrue(found.All(t => HexCoord.Distance(t.coord, target.coord) <= 1 && !t.water));
        Assert.IsTrue(HexCoord.Spiral(target.coord, 3).Select(map.Get).All(t => t == null || t.revealed));
        Assert.AreEqual(0, map.ExploreAround(target.coord, 0, 0, passable).Count, "arriving twice explores nothing new");
    }

    // ===== FEATURES (WG10) =====

    [Test]
    public void Features_Age0IsPlacedApartOnDryPassableGroundAndAgeIWaits()
    {
        var map = World(5);
        var orchards = map.WithFeature("orchard").ToList();
        Assert.AreEqual(4, orchards.Count);
        Assert.IsTrue(orchards.All(t => !t.water && t.terrain != "peaks" && map.StepsFromCapital(t.coord) >= 3));
        Assert.IsTrue(orchards.All(a => orchards.All(b => a == b || HexCoord.Distance(a.coord, b.coord) >= 3)));
        Assert.AreEqual(0, map.WithFeature("enclave").Count());

        var a = FreshWorld(9);
        var b = FreshWorld(9);
        var placedA = WorldGenerator.PlaceFeatures(a, Settings(), 1);
        CollectionAssert.AreEqual(placedA, WorldGenerator.PlaceFeatures(b, Settings(), 1), "reproducible");
        Assert.AreEqual(3, placedA.Count);
        Assert.IsTrue(placedA.All(c => a.Get(c).featureAge == 1));
    }

    // ===== MAGIC (WG07-WG09, WG12) =====

    private static WorldMap Strip(int cells)
    {
        var map = new WorldMap(1);
        for (int q = 0; q < cells; q++) map.Add(new WorldTile { coord = new HexCoord(q, 0) });
        return map;
    }

    [Test]
    public void Magic_JunctionsCountFamiliesNotSegments()
    {
        // Two families crossing: a Convergence.
        var map = Strip(8);
        map[1].leylines = 0b011;
        var junctions = WorldMagic.FindJunctions(map);
        Assert.AreEqual(1, junctions.Count);
        Assert.IsFalse(junctions[0].IsBasin);
        Assert.AreEqual(2, junctions[0].families.Count);

        // A third family meeting in the same footprint: a Basin.
        map[2].leylines = 0b110;
        junctions = WorldMagic.FindJunctions(map);
        Assert.AreEqual(1, junctions.Count);
        Assert.IsTrue(junctions[0].IsBasin);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, junctions[0].families);

        // One family doubling back on itself is no junction; two pairs far apart are two Convergences, not a Basin.
        var apart = Strip(10);
        apart[0].leylines = 0b001;
        apart[1].leylines = 0b001;
        apart[2].leylines = 0b011;
        apart[7].leylines = 0b101;
        junctions = WorldMagic.FindJunctions(apart);
        Assert.AreEqual(2, junctions.Count);
        Assert.IsTrue(junctions.All(j => !j.IsBasin));
    }

    [Test]
    public void Magic_AnAgeMovesTheLeylinesNotTheLandAndCommitsOnce()
    {
        var map = FreshWorld(21);
        string land = string.Join(";", map.Tiles.Select(t => $"{t.terrain}{t.elevation:0.0000}{t.river}{t.water}"));
        var sacred = map.Magic.SacredSites.ToList();
        var before = map.Magic.Leylines.Select(l => string.Join(",", l.cells)).ToList();
        var forecast = map.Magic.Forecast(map, 1).Select(l => string.Join(",", l.cells)).ToList();

        Assert.IsTrue(map.Magic.Apply(map, 1));
        var after = map.Magic.Leylines.Select(l => string.Join(",", l.cells)).ToList();
        CollectionAssert.AreEqual(forecast, after, "the forecast is what the Age brings");
        CollectionAssert.AreNotEqual(before, after, "the leylines moved");
        CollectionAssert.AreEqual(map.Magic.Leylines.Select(l => l.family), map.Magic.Seeds.Where(s => !s.dissonance).Select(s => s.id), "the families keep their identity");
        Assert.AreEqual(land, string.Join(";", map.Tiles.Select(t => $"{t.terrain}{t.elevation:0.0000}{t.river}{t.water}")), "geography and ordinary rivers stay");
        CollectionAssert.AreEqual(sacred, map.Magic.SacredSites);
        Assert.IsTrue(sacred.All(s => map[s].sacred && map[s].coherence >= 0.9f), "Sacred Sites keep their harmony");
        int version = map.Magic.Version;
        Assert.IsFalse(map.Magic.Apply(map, 1), "a repeated call changes nothing");
        Assert.AreEqual(version, map.Magic.Version);
    }

    [Test]
    public void Magic_SilverRunsDownstreamFromWhereALeylineCrossesWater()
    {
        var map = World(42);
        Assert.IsTrue(map.Tiles.Where(t => t.silver).All(t => t.river || t.lake), "only water is silver");
        foreach (var t in map.Tiles.Where(t => t.silver && t.leylines == 0))
        {
            bool fedFromUpstream = map.NeighboursOf(t).Any(n => n.downstream == t.index && n.lunehymn > 0f);
            Assert.IsTrue(fedFromUpstream, $"{t.coord} is silver without a leyline or silver water upstream");
        }
        // Crossings infuse fully.
        Assert.IsTrue(map.Tiles.Where(t => (t.river || t.lake) && t.leylines != 0).All(t => t.lunehymn >= 0.999f));
        // Land and magical fertility are separate fields.
        var land = map.Tiles.Where(t => !t.water).ToList();
        Assert.IsTrue(land.Any(t => t.landFertility > 0.4f && t.magicFertility < 0.15f) || land.Any(t => t.magicFertility > t.landFertility + 0.2f));
    }

    // ===== ZOOM (WG11) =====

    [Test]
    public void Zoom_ThreeReadingsBlendAndTheCursorStaysPut()
    {
        Assert.AreEqual(WorldScale.Micro, WorldZoom.ScaleOf(WorldZoom.MicroSize));
        Assert.AreEqual(WorldScale.Meso, WorldZoom.ScaleOf(WorldZoom.MesoSize));
        Assert.AreEqual(WorldScale.Macro, WorldZoom.ScaleOf(WorldZoom.MacroSize));
        Assert.AreEqual(0f, WorldZoom.Level(WorldZoom.CapitalSize));
        float last = -1f;
        for (float size = WorldZoom.Closest; size < 500f; size *= 1.05f)
        {
            float level = WorldZoom.Level(size);
            Assert.GreaterOrEqual(level, last, "zooming out never reads closer");
            Assert.That(level, Is.InRange(0f, 2f));
            last = level;
        }
        Assert.AreEqual(WorldZoom.Closest, WorldZoom.Step(WorldZoom.Closest, 5f, 400f), "never closer than the closest");
        Assert.AreEqual(400f, WorldZoom.Step(390f, -5f, 400f), "never farther than the whole world");
        Assert.IsTrue(WorldZoom.AtClosest(WorldZoom.Closest));

        WorldZoom.ZoomAbout(10f, 20f, 30f, -5f, 50f, 25f, out float x, out float y);
        // The point under the cursor keeps its place on screen: its offset from the centre halves with the zoom.
        Assert.AreEqual((30f - 10f) / 50f, (30f - x) / 25f, 1e-5);
        Assert.AreEqual((-5f - 20f) / 50f, (-5f - y) / 25f, 1e-5);
    }

    [TestCase(21)]
    [TestCase(42)]
    public void Magic_FlowsDownhillToWaterAndEnrichesNearbyGround(int seed)
    {
        var map = FreshWorld(seed);
        foreach (var line in map.Magic.Leylines)
        {
            Assert.IsTrue(map[line.cells.Last()].water, "Each generated headwater must reach a water body");
            for (int i = 1; i < line.cells.Count; i++)
            {
                Assert.AreEqual(1, HexCoord.Distance(map[line.cells[i-1]].coord, map[line.cells[i]].coord));
                Assert.Less(map[line.cells[i]].elevation, map[line.cells[i-1]].elevation);
            }
        }
        Assert.IsTrue(map.Tiles.Any(t => t.leylines == 0 && t.leylineInfluence > 0f &&
            map.NeighboursOf(t).All(n => n.leylines == 0)), "drift must extend beyond immediate neighbours");
        Assert.IsTrue(map.Tiles.Where(t => t.leylines != 0).All(t => t.leylineInfluence == 1f));
    }

    [Test]
    public void Authority_IsSeparateFromExplorationAndCannotCrossBarriers()
    {
        var map = new WorldMap(1);
        for (int q = 0; q < 5; q++) map.Add(new WorldTile { coord = new HexCoord(q, 0), terrain = "plain" });
        map[2].impassable = true;
        WorldAuthority.Project(map, map[0].coord, WorldAuthority.Player, 5);
        Assert.AreEqual(WorldAuthority.Player, map[1].authorityId);
        Assert.AreEqual(WorldAuthority.Wilderness, map[2].authorityId);
        Assert.AreEqual(WorldAuthority.Wilderness, map[3].authorityId);
        map.Explore(map[3].coord);
        Assert.AreEqual(WorldAuthority.Wilderness, map[3].authorityId, "discovery is not annexation");
        WorldAuthority.Project(map, map[4].coord, "enclave:4", 3);
        Assert.AreEqual("enclave:4", map[3].authorityId);
        Assert.AreEqual(WorldAuthority.Player, map[1].authorityId);
    }

    [Test]
    public void World_AllTilesHaveExplicitAuthorityAndAccess()
    {
        var map = FreshWorld(42);
        Assert.IsTrue(map.Tiles.All(t => !string.IsNullOrEmpty(t.authorityId)));
        Assert.AreEqual(WorldAuthority.Player, map.Get(map.Capital).authorityId);
        Assert.IsTrue(map.Tiles.Any(t => !t.water && t.authorityId == WorldAuthority.Wilderness));
        Assert.IsTrue(map.Tiles.Where(t => t.impassable || t.water).All(t => t.authorityId == WorldAuthority.Wilderness));
        Assert.IsTrue(map.Tiles.Where(t => t.impassable).All(t => t.Desirability == 0f));
    }

    [Test]
    public void Catalog_ChangedHandmadeGroundChangesFingerprint()
    {
        var tiles = Tiles();
        string before = WorldGenerator.CatalogHash(Settings(), tiles);
        var key = tiles[0].cells.Keys.First();
        tiles[0].cells[key] = "peaks";
        Assert.AreNotEqual(before, WorldGenerator.CatalogHash(Settings(), tiles));
    }
    [Test]
    public void Features_RespectWaterCoherenceAndEstablishIndependentAuthority()
    {
        var settings = Settings();
        var map = FreshWorld(42);
        settings.features.Clear();
        settings.features.Add(new FeatureSpec { id = "review-port", count = 2, requiresCoast = true, minDistance = 8, maxDistance = 999, spacing = 9, authorityId = "port", authorityRadius = 2 });
        settings.features.Add(new FeatureSpec { id = "review-refuge", count = 2, requiresFreshwater = true, minDistance = 8, maxDistance = 999, spacing = 9 });
        settings.features.Add(new FeatureSpec { id = "review-impossible", count = 2, minimumCoherence = 2f, maxDistance = 999 });
        WorldGenerator.PlaceFeatures(map, settings, 0);
        var ports = map.WithFeature("review-port").ToList();
        Assert.IsNotEmpty(ports);
        Assert.IsTrue(ports.All(t => map.NeighboursOf(t).Any(n => n.water && !n.lake)));
        Assert.IsTrue(ports.All(t => t.authorityId.StartsWith("port:")));
        var refuges = map.WithFeature("review-refuge").ToList();
        Assert.IsNotEmpty(refuges);
        Assert.IsTrue(refuges.All(t => t.river || map.NeighboursOf(t).Any(n => n.river || n.lake)));
        Assert.IsEmpty(map.WithFeature("review-impossible"));
    }
}
