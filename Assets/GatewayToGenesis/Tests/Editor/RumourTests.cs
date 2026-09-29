using System.Linq;
using NUnit.Framework;

/// <summary>
/// Rumours (WorldRumours) on a small hand-made map: what is worth a rumour and what is not (found, too near), the compass
/// words from the capital, the Sector only once its Macro Biome was seen, an area near the thing but not on it, a limit on
/// open rumours, confirmation on finding, and creatures told of without their names.
/// </summary>
public class RumourTests
{
    // A 30 x 30 patch of cells; the capital at (15,15); x grows east, y grows north.
    private static WorldMap Map()
    {
        var map = new WorldMap(7);
        for (int r = 0; r < 30; r++)
            for (int q = 0; q < 30; q++)
                map.Add(new WorldTile { coord = new HexCoord(q, r), x = q, y = r, macroBiome = "grove", slot = 1, composition = WorldComposition.MacroBiome, sector = CompassSector.North });
        map.Capital = new HexCoord(15, 15);
        map.Get(map.Capital).revealed = map.Get(map.Capital).known = true;
        return map;
    }

    private static WorldGenSettings Settings()
    {
        var s = new WorldGenSettings();
        s.macroBiomes.Add(new MacroBiomeSpec { id = "grove", name = "Violet Grove" });
        s.features.Add(new FeatureSpec { id = "spire", name = "The Singing Spire", tag = "landmark", visibleFromAfar = true, count = 1 });
        s.features.Add(new FeatureSpec { id = "orchard", name = "Orchard", tag = "fertile", count = 5 });
        s.species.Add(new SpeciesSpec { id = "wolf", name = "Grey Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Large });
        s.resourceSites.Add(new ResourceSiteSpec { id = "wolf-pack", name = "Wolf Pack", kind = ResourceKind.Fauna, species = "wolf" });
        return s;
    }

    private static int Cell(WorldMap map, int q, int r) => map.Get(new HexCoord(q, r)).index;

    [Test]
    public void Subjects_LandmarksAndDens_NotOrdinaryFeatures_NorWhatIsFound()
    {
        var map = Map();
        var settings = Settings();
        map[Cell(map, 25, 25)].feature = "spire";
        map[Cell(map, 5, 5)].feature = "orchard";
        int den = Cell(map, 20, 5);
        map.ResourceSites.Add(new ResourceSite { index = 0, spec = "wolf-pack", name = "Wolf Pack", kind = ResourceKind.Fauna, center = den, cells = { den } });
        map[den].resourceSite = 0;
        var subjects = WorldRumours.Subjects(map, settings);
        CollectionAssert.AreEquivalent(new[] { "feature:" + Cell(map, 25, 25), "den:" + den }, subjects.Select(s => s.key));
        map[Cell(map, 25, 25)].revealed = true;
        Assert.IsFalse(WorldRumours.Subjects(map, settings).Any(s => s.kind == RumourKind.Landmark), "a landmark in sight is found");
    }

    [Test]
    public void Where_GivesTheCompassFromTheCapital_AndTheSectorOnlyOnceItsMacroBiomeIsSeen()
    {
        var map = Map();
        var settings = Settings();
        int ne = Cell(map, 25, 25);
        string before = WorldRumours.Where(map, settings, ne, new RumourTuning());
        StringAssert.StartsWith("to the north-east", before);
        // The capital's own cell is revealed and shares the Macro Biome: the Sector is named.
        StringAssert.Contains("North Sector of the Violet Grove", before);
        foreach (var t in map.Tiles) t.revealed = false;
        StringAssert.DoesNotContain("Violet Grove", WorldRumours.Where(map, settings, ne, new RumourTuning()));
        Assert.AreEqual("south-west", WorldRumours.Compass(map.Get(map.Capital), map[Cell(map, 5, 5)]));
        Assert.AreEqual("west", WorldRumours.Compass(map.Get(map.Capital), map[Cell(map, 2, 15)]));
    }

    [Test]
    public void Hear_TellsEachThingOnce_NearTheThingButNotOnIt_AndStopsAtTheOpenLimit()
    {
        var map = Map();
        var settings = Settings();
        foreach (var (q, r) in new[] { (25, 25), (3, 3), (27, 10), (10, 27) }) map[Cell(map, q, r)].feature = "spire";
        var state = new RumourState();
        var tuning = new RumourTuning { maxOpen = 3, fuzz = 2 };
        var heard = Enumerable.Range(0, 5).Select(_ => WorldRumours.Hear(state, map, settings, tuning, 0)).ToList();
        Assert.AreEqual(3, heard.Count(r => r != null), "three open at most");
        Assert.AreEqual(3, state.heard.Select(r => r.key).Distinct().Count());
        foreach (var r in state.heard)
        {
            Assert.AreNotEqual(r.cell, r.area, "the map marks near the thing, not the thing");
            Assert.LessOrEqual(HexCoord.Distance(map[r.cell].coord, map[r.area].coord), 2);
            StringAssert.StartsWith("Travellers speak of a shape against the sky", r.text);
            StringAssert.DoesNotContain("Singing Spire", r.text, "never its name");
        }
        // Finding one confirms it, names it, and frees a place for another.
        var first = state.heard[0];
        map[first.cell].revealed = true;
        var confirmed = WorldRumours.Confirm(state, map, settings);
        Assert.AreEqual(1, confirmed.Count);
        Assert.AreEqual("The Singing Spire", confirmed[0].truth);
        Assert.IsNotNull(WorldRumours.Hear(state, map, settings, tuning, 0));
        Assert.IsNull(WorldRumours.Hear(state, map, settings, tuning, 0), "nothing left to tell");
        Assert.AreEqual(1, WorldRumours.Confirmed(state, "landmark"));
    }

    [Test]
    public void Hear_SkipsWhatLiesNearTheCapital()
    {
        var map = Map();
        var settings = Settings();
        map[Cell(map, 16, 15)].feature = "spire";
        Assert.IsNull(WorldRumours.Hear(new RumourState(), map, settings, new RumourTuning { minDistance = 5 }, 0));
    }

    [Test]
    public void Creatures_AreToldOfBySizeAndWays_NeverByName()
    {
        var wolf = Settings().Species("wolf");
        string saying = WorldRumours.CreatureSaying(wolf);
        StringAssert.Contains("large beasts", saying);
        StringAssert.Contains("hunt together", saying);
        StringAssert.DoesNotContain("Wolf", saying);
    }

    [Test]
    public void Covering_FindsTheRumoursWhoseAreaReachesACell()
    {
        var map = Map();
        var state = new RumourState();
        state.heard.Add(new Rumour { key = "x", area = Cell(map, 20, 20), cell = Cell(map, 21, 20), text = "t" });
        var tuning = new RumourTuning { areaRadius = 3 };
        Assert.AreEqual(1, WorldRumours.Covering(state, map, Cell(map, 22, 20), tuning).Count());
        Assert.AreEqual(0, WorldRumours.Covering(state, map, Cell(map, 10, 10), tuning).Count());
    }
}
