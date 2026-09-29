using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Cover with no scene (<see cref="WorldCover"/>): placement, line of sight, sight from inside, travel, hardship, survey
/// finds, hidden sites and what held cover gives. Uses the small catalog of <see cref="WorldGenerationTests"/> plus its
/// own cover; the last test lays the real catalog (World.asset) on real worlds and needs the Test Runner.
/// </summary>
public class WorldCoverTests
{
    private static WorldGenSettings Settings()
    {
        var s = WorldGenerationTests.Settings();
        s.covers.Add(new CoverSpec
        {
            id = "forest", name = "Deep Forest", count = 4, minSize = 3, maxSize = 8, spacing = 6, minDistance = 4, terrains = { "wood", "glade" },
            blocksSight = true, sightInside = 1, travel = 1.6f, survey = 1.5f, finds = 1.6f, beauty = 0.2f, governance = 1.3f,
            yields = { new ResourceAmount { resource = "Elderwood", amount = 0.01f } },
            forage = { new ResourceAmount { resource = "Game Meat", amount = 3f } },
        });
        s.covers.Add(new CoverSpec
        {
            id = "thicket", name = "Thicket", count = 3, minSize = 2, maxSize = 5, spacing = 6, minDistance = 4, terrains = { "plain", "steppe", "ruin" },
            blocksSight = false, sightInside = 3, travel = 1.8f, survey = 1.2f, hardship = 2f,
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "hollow", name = "Hollow", kind = ResourceKind.Flora, count = 2, size = 1, spacing = 4, minDistance = 3, covers = { "forest" }, landValue = 4f,
            harvest = { new ResourceAmount { resource = "Elderwood", amount = 20f } },
        });
        return s;
    }

    private static WorldMap Fresh(int seed, WorldGenSettings settings = null) =>
        WorldGenerator.Generate(seed, settings ?? Settings(), WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    // ===== PLACEMENT =====

    [TestCase(7)]
    [TestCase(42)]
    public void Placement_PatchesGrowOverTheirGroundClearOfTheCapital(int seed)
    {
        var settings = Settings();
        var map = Fresh(seed, settings);
        Assert.IsTrue(map.Covers.Any(p => p.spec == "forest"), "deep forests are laid with the world");
        foreach (var patch in map.Covers)
        {
            var spec = settings.Cover(patch.spec);
            Assert.LessOrEqual(patch.cells.Count, spec.maxSize);
            Assert.GreaterOrEqual(map.StepsFromCapital(map[patch.center].coord), spec.minDistance);
            foreach (int cell in patch.cells)
            {
                var t = map[cell];
                Assert.AreEqual(patch.spec, t.cover);
                Assert.AreEqual(patch.index, t.coverPatch);
                CollectionAssert.Contains(spec.terrains, t.terrain);
                Assert.GreaterOrEqual(map.StepsFromCapital(t.coord), WorldCover.CapitalClearance, "the survivors' own ground stays clear");
                Assert.AreEqual(spec.blocksSight, t.concealed);
                Assert.AreEqual(spec.travel, t.coverTravel, 1e-4f);
            }
            // A patch is one piece: every cell reaches its centre through the patch.
            var reached = new HashSet<int> { patch.center };
            var open = new Queue<int>(reached);
            while (open.Count > 0)
                foreach (var n in map.NeighboursOf(map[open.Dequeue()]))
                    if (n.coverPatch == patch.index && reached.Add(n.index)) open.Enqueue(n.index);
            Assert.AreEqual(patch.cells.Count, reached.Count, "a patch is connected");
        }
        Assert.AreEqual(map.Tiles.Count(t => t.cover != null), map.Covers.Sum(p => p.cells.Count), "no cell lies under two patches");

        var again = Fresh(seed, Settings());
        CollectionAssert.AreEqual(map.Covers.Select(p => $"{p.spec}:{string.Join(",", p.cells)}"), again.Covers.Select(p => $"{p.spec}:{string.Join(",", p.cells)}"), "the same seed lays the same cover (nothing is saved)");
    }

    // ===== SIGHT =====

    // Three cells in a row, with no cover of their own: the watcher's, the middle one and the one beyond.
    private static (WorldTile a, WorldTile b, WorldTile c) Row(WorldMap map)
    {
        foreach (var a in map.Tiles.Where(t => !t.water && t.cover == null))
            for (int d = 0; d < 6; d++)
            {
                var b = map.Get(a.coord + HexCoord.Directions[d]);
                var c = map.Get(a.coord + HexCoord.Directions[d] * 2);
                if (b != null && c != null && !b.water && !c.water && b.cover == null && c.cover == null) return (a, b, c);
            }
        throw new InvalidOperationException("no three open cells in a row");
    }

    [Test]
    public void Sight_ConcealingCoverHidesWhatLiesBeyondButNotItsEdge()
    {
        var map = Fresh(7, WorldGenerationTests.Settings());
        var (a, b, c) = Row(map);
        var from = HexHierarchy.ChildCenter(a.coord);
        var beyond = HexHierarchy.ChildCenter(c.coord);
        Assert.IsTrue(WorldCover.Clear(map, from, beyond, a.coord), "open ground hides nothing");

        b.concealed = true;
        Assert.IsFalse(WorldCover.Clear(map, from, beyond, a.coord), "a forest between hides what lies beyond it");
        int d = HexCoord.Distance(from, HexHierarchy.ChildCenter(b.coord));
        var visible = WorldCover.Visible(map, from, d + 4);
        Assert.IsTrue(visible.Any(h => HexHierarchy.Parent(h) == b.coord), "its edge is seen");
        CollectionAssert.DoesNotContain(visible, beyond);
        CollectionAssert.Contains(WorldCover.Visible(map, from, d + 4, overCover: true), beyond, "from high ground the view carries over the canopy");
        Assert.IsTrue(WorldCover.Clear(map, HexHierarchy.ChildCenter(b.coord), beyond, b.coord), "the cover a watcher stands in does not hide the way out");
    }

    [Test]
    public void Sight_IsNarrowedInsideCover()
    {
        var t = new WorldTile();
        Assert.AreEqual(6, WorldCover.SightFrom(t, 6), "no cover, no cap");
        t.coverSight = 1;
        Assert.AreEqual(1, WorldCover.SightFrom(t, 6));
        t.coverSight = 0;
        Assert.AreEqual(1, WorldCover.SightFrom(t, 6), "a party always sees the hex beside it");
    }

    // ===== EXPLORING IT =====

    [Test]
    public void Exploring_CoverSlowsTravelWearsPartiesAndHidesFinds()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var forest = map.Tiles.First(t => t.cover == "forest");
        var open = new WorldTile { coord = forest.coord, terrain = forest.terrain, weatherTravelMultiplier = forest.weatherTravelMultiplier, danger = forest.danger, dissonance = forest.dissonance, leylines = forest.leylines };
        Assert.AreEqual(MicroGrid.CellFactor(open) * 1.6f, MicroGrid.CellFactor(forest), 1e-4f, "travel through a forest is slower");

        var thicket = map.Tiles.FirstOrDefault(t => t.cover == "thicket");
        if (thicket != null) Assert.AreEqual(2f, UnitSurroundings.Of(map, new WorldUnit { coord = thicket.coord }).hardship, 1e-4f, "thorns wear a party down");

        var x = new ExpeditionSettings();
        double draw = x.surveyCacheChance * 1.2;
        Assert.IsFalse(Expeditions.RollSurvey(x, true, 1.0, 0.5, draw).cache, "in the open a middling draw finds nothing");
        Assert.IsTrue(Expeditions.RollSurvey(x, true, 1.0, 0.5, draw, forest.coverFinds).cache, "the same draw finds something in the forest");
        Assert.IsFalse(Expeditions.RollSurvey(x, true, 1.0, 0.5, 0.95, 50f).cache, "something can always stay hidden");
    }

    [Test]
    public void Exploring_WhatStandsInsideIsHiddenUntilExplored()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var hollows = map.ResourceSites.Where(s => s.spec == "hollow").ToList();
        Assert.IsNotEmpty(hollows, "a site that stands only in a forest finds one");
        foreach (var site in hollows) Assert.IsTrue(site.cells.All(c => map[c].cover == "forest"), "and only there");

        var t = map[hollows[0].center];
        t.known = true;
        t.explored = false;
        Assert.IsTrue(WorldCover.Hides(t));
        Assert.IsFalse(WorldResources.Sighted(t, hollows[0], settings.ResourceSite("hollow")), "walking past the forest does not show what is inside");
        Assert.IsNull(WorldResources.Label(map, settings, t));
        t.explored = true;
        Assert.IsFalse(WorldCover.Hides(t));
        Assert.AreEqual("Hollow", WorldResources.Label(map, settings, t), "exploring the cell finds it");
    }

    // ===== HOLDING IT =====

    [Test]
    public void Holding_CoverAddsForageYieldsBeautyAndGovernance()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var forest = map.Tiles.First(t => t.cover == "forest");
        Assert.AreEqual(3f, WorldUnits.ForageOf(settings, forest, 1f).Single(a => a.resource == "Game Meat").amount, 1e-4f, "the forest adds game to the forage");
        Assert.AreEqual(0.01f, WorldCover.YieldsOf(settings, forest).Single().amount, 1e-5f);

        forest.explored = true;
        forest.authorityId = WorldAuthority.Player;
        var sources = WorldUnits.LandYields(map, settings, new SettlementRules());
        Assert.IsTrue(sources.Any(s => s.source.StartsWith("Cover: Deep Forest") && s.resource == "Elderwood"), "held cover is worked");

        var bare = WorldGenerationTests.Settings();
        var plain = Fresh(7, bare);
        Assert.Greater(WorldTerritory.Difficulty(settings, forest), WorldTerritory.Difficulty(bare, plain[forest.index]), "dense cover is harder to administer");
    }

    // ===== THE REAL CATALOG =====

    /// <summary>Every cover of World.asset lies on real worlds, and every site that stands only in cover finds some (needs Resources: Test Runner).</summary>
    [Test]
    public void Content_EveryCoverLiesOnRealWorlds()
    {
        GameCatalog.InvalidateAll();
        var world = GameCatalog.World.All.FirstOrDefault();
        Assert.IsNotNull(world, "No WorldSettings in Resources/World");
        var gen = world.generation;
        Assert.GreaterOrEqual(gen.covers.Count, 6, "the proposal lists 6 kinds of cover");
        var stencil = WorldSystem.LoadStencil(gen);
        var tiles = WorldSystem.LoadTiles(gen);
        var placed = gen.covers.ToDictionary(c => c.id, c => 0);
        var cells = gen.covers.ToDictionary(c => c.id, c => 0);
        foreach (int seed in new[] { 3, 1234, 98765 })
        {
            var map = WorldGenerator.Generate(seed, gen, stencil, tiles);
            foreach (var patch in map.Covers) { placed[patch.spec]++; cells[patch.spec] += patch.cells.Count; }
            foreach (var spec in gen.resourceSites.Where(s => s.covers.Count > 0))
                foreach (var site in map.ResourceSites.Where(s => s.spec == spec.id))
                    Assert.IsTrue(site.cells.All(c => spec.covers.Contains(map[c].cover)), $"{spec.id} stands only in its cover");
        }
        UnityEngine.Debug.Log("Cover over 3 worlds: " + string.Join(", ", placed.Select(p => $"{p.Key} {p.Value} patches / {cells[p.Key]} cells")));
        CollectionAssert.IsEmpty(placed.Where(p => p.Value == 0).Select(p => p.Key), "every cover finds ground on some world");
    }
}
