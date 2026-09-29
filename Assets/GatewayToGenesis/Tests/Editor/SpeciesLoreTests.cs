using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The bestiary's memory (SpeciesLore; vault: Arcanorian Ecology.md, "Knowledge of Creatures") on a small hand-made map:
/// identified species stay known when their den is gone, watching and hunting make a species Observed, study makes it
/// Understood, the living domains GameValues reads, and the validator's goal check.
/// </summary>
public class SpeciesLoreTests
{
    // Cells 0-9 in a row: 0-5 in the Violet Grove, 6-7 in an intersection beside it, 8-9 in no Macro Biome.
    private static WorldMap Map()
    {
        var map = new WorldMap(1);
        for (int i = 0; i < 10; i++)
        {
            map.Add(new WorldTile
            {
                coord = new HexCoord(i, 0),
                composition = i >= 6 && i <= 7 ? WorldComposition.Intersection : WorldComposition.MacroBiome,
                macroBiome = i < 8 ? "grove" : null,
                sector = i < 3 ? CompassSector.NorthEast : i < 6 ? CompassSector.Central : CompassSector.None,
                authorityId = WorldAuthority.Wilderness,
            });
        }
        return map;
    }

    private static WorldGenSettings Settings()
    {
        var s = new WorldGenSettings();
        s.macroBiomes.Add(new MacroBiomeSpec { id = "grove", name = "Violet Grove" });
        s.species.Add(new SpeciesSpec { id = "wolf", name = "Grey Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social });
        s.species.Add(new SpeciesSpec { id = "elk", name = "Taiga Elk", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, size = CreatureSize.Large });
        s.species.Add(new SpeciesSpec { id = "cushion", name = "Threshold Cushion", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Trapper });
        s.resourceSites.Add(new ResourceSiteSpec { id = "wolf-pack", name = "Wolf Pack", kind = ResourceKind.Fauna, species = "wolf" });
        s.resourceSites.Add(new ResourceSiteSpec { id = "elk-herd", name = "Elk Herd", kind = ResourceKind.Fauna, species = "elk" });
        s.resourceSites.Add(new ResourceSiteSpec { id = "cushion", name = "Threshold Cushion", kind = ResourceKind.Bloom, niche = BloomNiche.Predator, species = "cushion" });
        return s;
    }

    private static ResourceSite Place(WorldMap map, WorldGenSettings settings, string spec, params int[] cells)
    {
        var siteSpec = settings.ResourceSite(spec);
        var site = new ResourceSite { index = map.ResourceSites.Count, spec = spec, name = siteSpec.name, kind = siteSpec.kind, center = cells[0], cells = cells.ToList() };
        foreach (int c in cells) map[c].resourceSite = site.index;
        map.ResourceSites.Add(site);
        return site;
    }

    private static void Survey(WorldMap map, params int[] cells)
    {
        foreach (int c in cells) map[c].known = map[c].explored = true;
    }

    private static LoreView View(SpeciesLoreState state, bool understands = false) =>
        new LoreView { state = state, tuning = new SpeciesLoreTuning(), understands = understands };

    [Test]
    public void Refresh_RemembersAnIdentifiedSpecies_AfterItsBloomHasFaded()
    {
        var map = Map();
        var settings = Settings();
        var bloom = Place(map, settings, "cushion", 1);
        Survey(map, 1);
        var state = new SpeciesLoreState();
        SpeciesLore.Refresh(state, map, settings, new SpeciesLoreTuning());
        string place = SpeciesKnowledge.Place(map, settings, bloom);
        map.ResourceSites.Remove(bloom);

        Assert.AreEqual(SpeciesLevel.Unknown, SpeciesKnowledge.LevelOf(map, settings, "cushion"), "the map alone forgets it");
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "cushion", View(state)), "the memory does not");
        Assert.AreEqual(1, SpeciesKnowledge.IdentifiedCount(map, settings, View(state)));
        var known = SpeciesKnowledge.Identified(map, settings, View(state)).Single();
        Assert.IsEmpty(known.dens);
        CollectionAssert.AreEqual(new[] { place }, known.formerPlaces, "where it was found is kept, marked as a former place");
    }

    [Test]
    public void Refresh_KeepsNothingOfSpeciesOnlySighted()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 4);
        map[4].known = true;
        var state = new SpeciesLoreState();
        SpeciesLore.Refresh(state, map, settings, new SpeciesLoreTuning());
        Assert.IsEmpty(state.species, "a sighting is the map's to show, not the memory's");
    }

    [Test]
    public void Watch_CountsEchoesADenLivesNearYourLand_AndObservesItOnce()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 4);
        Survey(map, 4);
        map[6].authorityId = WorldAuthority.Player; // two cells away
        var state = new SpeciesLoreState();
        var tuning = new SpeciesLoreTuning { observeAt = 3, watchReach = 2 };

        Assert.IsEmpty(SpeciesLore.Watch(state, map, settings, tuning));
        Assert.IsEmpty(SpeciesLore.Watch(state, map, settings, tuning));
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "wolf", View(state)));
        CollectionAssert.AreEqual(new[] { "wolf" }, SpeciesLore.Watch(state, map, settings, tuning), "the third Echo watched makes it Observed");
        Assert.IsEmpty(SpeciesLore.Watch(state, map, settings, tuning), "and says so only once");
        Assert.AreEqual(4, state.Of("wolf").watched);
        Assert.AreEqual(SpeciesLevel.Observed, SpeciesKnowledge.LevelOf(map, settings, "wolf", View(state)));
    }

    [Test]
    public void Watch_IgnoresDensFarFromYourLand_AndLandOthersHold()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 1);
        Survey(map, 1);
        map[9].authorityId = WorldAuthority.Player; // eight cells away
        map[2].authorityId = "enclave-1";           // beside it, but not yours
        var state = new SpeciesLoreState();
        SpeciesLore.Watch(state, map, settings, new SpeciesLoreTuning());
        Assert.AreEqual(0, state.Of("wolf").watched);
        map[3].authorityId = WorldAuthority.Outpost; // your outposts are you
        SpeciesLore.Watch(state, map, settings, new SpeciesLoreTuning());
        Assert.AreEqual(1, state.Of("wolf").watched);
    }

    [Test]
    public void HuntsAndFurtherPlaces_AreObservationsToo()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "elk-herd", 1);
        Place(map, settings, "elk-herd", 4);
        Survey(map, 1, 4);
        var state = new SpeciesLoreState();
        var tuning = new SpeciesLoreTuning { observeAt = 3, perHunt = 1, perPlace = 1 };
        SpeciesLore.Refresh(state, map, settings, tuning);
        Assert.AreEqual(1, SpeciesLore.Observations(state.Of("elk"), map, tuning), "the second place a den was found (North-East, then Central)");

        WorldBehavior.Hunted(map, settings, "elk", WorldAuthority.Player, 1f);
        WorldBehavior.Hunted(map, settings, "elk", "enclave-1", 1f);
        Assert.AreEqual(2, SpeciesLore.Observations(state.Of("elk"), map, tuning), "only your own hunts teach your people");
        Assert.IsEmpty(SpeciesLore.Refresh(state, map, settings, tuning));
        WorldBehavior.Hunted(map, settings, "elk", WorldAuthority.Outpost, 1f);
        CollectionAssert.AreEqual(new[] { "elk" }, SpeciesLore.Refresh(state, map, settings, tuning));
    }

    [Test]
    public void Understood_NeedsObservationAndStudy()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 4);
        Survey(map, 4);
        var state = new SpeciesLoreState();
        SpeciesLore.Refresh(state, map, settings, new SpeciesLoreTuning());
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "wolf", View(state, understands: true)), "study cannot understand what no one has watched");
        state.Of("wolf").observed = true;
        Assert.AreEqual(SpeciesLevel.Observed, SpeciesKnowledge.LevelOf(map, settings, "wolf", View(state)));
        Assert.AreEqual(SpeciesLevel.Understood, SpeciesKnowledge.LevelOf(map, settings, "wolf", View(state, understands: true)));
        Assert.AreEqual(4f, SpeciesKnowledge.Value(map, settings, "species_known", "wolf", View(state, understands: true)));
        Assert.AreEqual(SpeciesLevel.Understood, SpeciesKnowledge.Identified(map, settings, View(state, understands: true)).Single().level);
    }

    [Test]
    public void LivingDomains_ReadNumbersAndTemper()
    {
        var map = Map();
        var settings = Settings();
        map.Populations.Add(new Population { slot = 0, species = "wolf", groups = 3f, capacity = 4f });
        map.Populations.Add(new Population { slot = 1, species = "wolf", groups = 1f, capacity = 4f });
        map.Populations.Add(new Population { slot = 0, species = "elk", groups = 8f, capacity = 8f });
        Assert.AreEqual(50f, SpeciesKnowledge.Value(map, settings, "species_population", "wolf"));
        Assert.AreEqual(0f, SpeciesKnowledge.Value(map, settings, "species_population", "cushion"), "lives nowhere");
        Assert.AreEqual(0f, SpeciesKnowledge.Value(map, settings, "species_population", ""), "a species is required");

        Assert.AreEqual(0f, SpeciesKnowledge.Value(map, settings, "species_behavior", "wolf"), "not met yet");
        WorldBehavior.Hunted(map, settings, "wolf", WorldAuthority.Player, 1f);
        float temper = WorldBehavior.Temper(map, "wolf", WorldAuthority.Player);
        Assert.Less(temper, 0f);
        Assert.AreEqual((float)System.Math.Round(temper * 100f), SpeciesKnowledge.Value(map, settings, "species_behavior", "wolf"));
    }

    [Test]
    public void GoalProblem_ChecksTheNewLevelsAndLivingDomains()
    {
        var species = new HashSet<string> { "wolf" };
        Assert.IsNull(SpeciesKnowledge.GoalProblem("species_known", "wolf", 4f, species, null), "understood is a level");
        Assert.IsNull(SpeciesKnowledge.GoalProblem("species_known", "wolf", 5f, species, null), "mastered is a level (E7)");
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("species_known", "wolf", 6f, species, null));
        Assert.IsNull(SpeciesKnowledge.GoalProblem("species_population", "wolf", 50f, species, null));
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("species_population", "", 50f, species, null), "a species is required");
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("species_behavior", "bear", 30f, species, null), "a real species");
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("species_behavior", "wolf", 0f, species, null), "met before anything happens");
        Assert.IsTrue(SpeciesKnowledge.IsDiscoveryDomain("species_behavior"), "never a requirement");
        Assert.IsTrue(SpeciesKnowledge.HidesProgress("species_population", "wolf"));
    }
}
