using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The bestiary's knowledge (SpeciesKnowledge; vault: Arcanorian Ecology.md, "Knowledge of Creatures") on a small hand-made map: levels
/// derived from the map, the spoiler rule (nothing about an unidentified species), the discovery values GameValues reads,
/// the validator's goal check, and den places.
/// </summary>
public class SpeciesKnowledgeTests
{
    // Cells 0-9 in a row: 0-5 in the Violet Grove (a Macro Biome), 6-7 in an intersection beside it, 8-9 in no Macro Biome.
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
        s.resourceSites.Add(new ResourceSiteSpec { id = "rice", name = "Rice", kind = ResourceKind.Crop });
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

    [Test]
    public void Level_UnknownUntilADenIsSighted_IdentifiedOnceOneIsSurveyed()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 1, 2);
        Place(map, settings, "wolf-pack", 4);
        Assert.AreEqual(SpeciesLevel.Unknown, SpeciesKnowledge.LevelOf(map, settings, "wolf"));
        map[2].known = true;
        Assert.AreEqual(SpeciesLevel.Sighted, SpeciesKnowledge.LevelOf(map, settings, "wolf"), "any cell of a den shows it");
        map[4].known = map[4].explored = true;
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "wolf"), "one surveyed den is enough");
        Assert.AreEqual(SpeciesLevel.Unknown, SpeciesKnowledge.LevelOf(map, settings, "elk"), "a species with no den on the map stays unknown");
    }

    [Test]
    public void Sighted_FollowsTheMapsOwnRule_CoverHidesADenUntilExplored()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "elk-herd", 3);
        map[3].known = true;
        map[3].concealed = true;
        Assert.AreEqual(SpeciesLevel.Unknown, SpeciesKnowledge.LevelOf(map, settings, "elk"), "inside concealing cover nothing shows until the cell is explored");
        Assert.IsEmpty(SpeciesKnowledge.UnidentifiedSightings(map, settings));
        map[3].explored = true;
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "elk"));
    }

    [Test]
    public void Levels_LeaveUnknownSpeciesOut_AndIdentifiedListsOnlyIdentifiedDens()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 1);
        var surveyed = Place(map, settings, "wolf-pack", 4);
        Place(map, settings, "elk-herd", 8);
        map[1].known = true;
        map[4].known = map[4].explored = true;
        var levels = SpeciesKnowledge.Levels(map, settings);
        CollectionAssert.AreEquivalent(new[] { "wolf" }, levels.Keys, "the elk's den was never seen: it is not even counted");
        Assert.AreEqual(SpeciesLevel.Identified, levels["wolf"]);
        var known = SpeciesKnowledge.Identified(map, settings);
        Assert.AreEqual(1, known.Count);
        Assert.AreEqual("wolf", known[0].species.id);
        CollectionAssert.AreEqual(new[] { surveyed }, known[0].dens, "a sighted, unsurveyed den is not known to be the wolves'");
        Assert.AreEqual(1, SpeciesKnowledge.IdentifiedCount(map, settings));
    }

    [Test]
    public void UnidentifiedSightings_OneByOne_FaunaOnly()
    {
        var map = Map();
        var settings = Settings();
        var a = Place(map, settings, "wolf-pack", 1);
        var b = Place(map, settings, "wolf-pack", 2);
        Place(map, settings, "cushion", 3);
        var identified = Place(map, settings, "elk-herd", 5);
        foreach (int c in new[] { 1, 2, 3, 5 }) map[c].known = true;
        map[5].explored = true;
        var sightings = SpeciesKnowledge.UnidentifiedSightings(map, settings);
        CollectionAssert.AreEqual(new[] { a, b }, sightings, "each den on its own (never grouped by species), no bloom (it would say it is a creature), nothing identified");
        CollectionAssert.DoesNotContain(sightings, identified);
        Assert.AreEqual(SpeciesLevel.Sighted, SpeciesKnowledge.LevelOf(map, settings, "cushion"), "the Trapper bloom is still sighted for the rules");
    }

    [Test]
    public void Identified_SkipsASpeciesTheCatalogDoesNotHave()
    {
        var map = Map();
        var settings = Settings();
        settings.resourceSites.Add(new ResourceSiteSpec { id = "stray", name = "Stray", kind = ResourceKind.Fauna, species = "no-such-creature" });
        Place(map, settings, "stray", 1);
        map[1].known = map[1].explored = true;
        Assert.IsEmpty(SpeciesKnowledge.Identified(map, settings));
    }

    [Test]
    public void SitesIdentified_ByKindOrId_PlantingIsNotDiscovery()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 1);
        Place(map, settings, "rice", 2);
        Place(map, settings, "rice", 3).planted = true;
        Place(map, settings, "elk-herd", 4);
        foreach (int c in new[] { 1, 2 }) map[c].known = map[c].explored = true;
        map[4].known = true;
        Assert.AreEqual(2, SpeciesKnowledge.SitesIdentified(map, settings));
        Assert.AreEqual(1, SpeciesKnowledge.SitesIdentified(map, settings, "fauna"));
        Assert.AreEqual(1, SpeciesKnowledge.SitesIdentified(map, settings, "Rice"));
        Assert.AreEqual(0, SpeciesKnowledge.SitesIdentified(map, settings, "elk-herd"), "sighted is not identified");
    }

    [Test]
    public void Value_AnswersTheDiscoveryDomains()
    {
        var map = Map();
        var settings = Settings();
        Place(map, settings, "wolf-pack", 1);
        Place(map, settings, "elk-herd", 4);
        map[1].known = map[1].explored = true;
        map[4].known = true;
        Assert.AreEqual(1f, SpeciesKnowledge.Value(map, settings, "species_known", ""), "species identified");
        Assert.AreEqual(2f, SpeciesKnowledge.Value(map, settings, "species_known", "wolf"));
        Assert.AreEqual(1f, SpeciesKnowledge.Value(map, settings, "species_known", "elk"));
        Assert.AreEqual(0f, SpeciesKnowledge.Value(map, settings, "species_known", "cushion"));
        Assert.AreEqual(1f, SpeciesKnowledge.Value(map, settings, "sites_identified", null));
        Assert.AreEqual(0f, SpeciesKnowledge.Value(map, settings, "population", null));
        Assert.IsTrue(SpeciesKnowledge.IsDiscoveryDomain("Species_Known"));
        Assert.IsFalse(SpeciesKnowledge.IsDiscoveryDomain("journeys"));
    }

    [Test]
    public void Progress_OfOneSpeciesIsASecret()
    {
        Assert.IsTrue(SpeciesKnowledge.HidesProgress("species_known", "wolf"), "(1/2) would say which unidentified den is the wolves'");
        Assert.IsFalse(SpeciesKnowledge.HidesProgress("species_known", ""), "a count of identified species hides nothing");
        Assert.IsFalse(SpeciesKnowledge.HidesProgress("sites_identified", "fauna"));
    }

    [Test]
    public void GoalProblem_NamesRealSpeciesAndSites()
    {
        var species = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "wolf" };
        var sites = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "wolf-pack" };
        Assert.IsNull(SpeciesKnowledge.GoalProblem("species_known", "", 3, species, sites));
        Assert.IsNull(SpeciesKnowledge.GoalProblem("species_known", "Wolf", 2, species, sites));
        StringAssert.Contains("unknown species", SpeciesKnowledge.GoalProblem("species_known", "dragon", 2, species, sites));
        StringAssert.Contains("level", SpeciesKnowledge.GoalProblem("species_known", "wolf", 6, species, sites));
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("species_known", "", 0, species, sites));
        Assert.IsNull(SpeciesKnowledge.GoalProblem("sites_identified", "fauna", 1, species, sites));
        Assert.IsNull(SpeciesKnowledge.GoalProblem("sites_identified", "wolf-pack", 1, species, sites));
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem("sites_identified", "dragon-lair", 1, species, sites));
    }

    [Test]
    public void Places_NameTheMacroBiomeAndSector_AndGroupDens()
    {
        var map = Map();
        var settings = Settings();
        var ne = Place(map, settings, "wolf-pack", 1);
        var ne2 = Place(map, settings, "wolf-pack", 2);
        var central = Place(map, settings, "wolf-pack", 4);
        var seam = Place(map, settings, "wolf-pack", 6);
        var wilds = Place(map, settings, "wolf-pack", 9);
        Assert.AreEqual("Violet Grove, North-East Sector", SpeciesKnowledge.Place(map, settings, ne));
        Assert.AreEqual("Violet Grove, Central Sector", SpeciesKnowledge.Place(map, settings, central));
        Assert.AreEqual("Violet Grove, an intersection", SpeciesKnowledge.Place(map, settings, seam));
        Assert.AreEqual("the wilds", SpeciesKnowledge.Place(map, settings, wilds));
        var places = SpeciesKnowledge.Places(map, settings, new[] { ne, central, ne2 });
        CollectionAssert.AreEqual(new[] { ("Violet Grove, North-East Sector", 2), ("Violet Grove, Central Sector", 1) }, places);
    }
}
