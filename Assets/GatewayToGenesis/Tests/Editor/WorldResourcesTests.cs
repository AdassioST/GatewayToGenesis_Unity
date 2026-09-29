using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Resource sites with no scene (<see cref="WorldResources"/>): placement by Age, what the map knows of them, land value,
/// what they do to their neighbours (beauty, Coherence, fertility), harvests and the grievances they raise, planting
/// seeds, and the save shape. Uses the small catalog of <see cref="WorldGenerationTests"/> plus its own sites; the last
/// test places the real catalog (World.asset) on real worlds and needs the Test Runner.
/// </summary>
public class WorldResourcesTests
{
    private static WorldGenSettings Settings()
    {
        var s = WorldGenerationTests.Settings();
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "rice", name = "Rice", kind = ResourceKind.Crop, resource = "Highland Rice", count = 3, size = 2, spacing = 4, minDistance = 2,
            terrains = { "plain", "steppe", "wood", "glade" }, landValue = 3f, beauty = 0.2f, beautyAura = 0.2f, fertilityAura = 0.1f,
            yields = { new ResourceAmount { resource = "Highland Rice", amount = 0.04f } },
            harvest = { new ResourceAmount { resource = "Highland Rice", amount = 20f } }, seeds = true, grievance = 10f,
            plantFertility = 0.2f, plantedShare = 0.5f, plantedFertility = 0.1f,
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "spire", name = "Spire", kind = ResourceKind.Mineral, resource = "Emberwhisper", count = 2, size = 1, spacing = 6, minDistance = 3,
            landValue = 4f, beauty = 0.5f, beautyAura = 0.3f, coherenceAura = -0.1f, auraRadius = 2, visibleFromAfar = true,
            harvest = { new ResourceAmount { resource = "Emberwhisper", amount = 10f } },
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "seep", name = "Seep", kind = ResourceKind.Water, count = 2, size = 1, spacing = 6, minDistance = 3,
            landValue = -4f, beauty = -0.6f, beautyAura = -0.3f, coherenceAura = -0.05f, fertilityAura = -0.05f, untouchable = "Leave it be.",
        });
        s.resourceSites.Add(new ResourceSiteSpec { id = "late", name = "Late", kind = ResourceKind.Flora, minAge = 1, count = 2, size = 1, minDistance = 3, landValue = 2f });
        return s;
    }

    private static readonly Dictionary<int, WorldMap> Cache = new Dictionary<int, WorldMap>();

    // Worlds are cached per seed for tests that only read them.
    private static WorldMap World(int seed)
    {
        if (!Cache.TryGetValue(seed, out var map)) Cache[seed] = map = Fresh(seed);
        return map;
    }

    private static WorldMap Fresh(int seed, WorldGenSettings settings = null) =>
        WorldGenerator.Generate(seed, settings ?? Settings(), WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    private static ResourceSite FirstOf(WorldMap map, string spec) => map.ResourceSites.First(s => s.spec == spec && !s.planted);

    // ===== PLACEMENT =====

    [TestCase(7)]
    [TestCase(42)]
    public void Placement_SitesFollowTheirRulesAndWaitForTheirAge(int seed)
    {
        var map = World(seed);
        var settings = Settings();
        Assert.IsTrue(map.ResourceSites.Any(s => s.spec == "rice"), "crops are placed at Age 0");
        Assert.IsTrue(map.ResourceSites.Any(s => s.spec == "spire"));
        Assert.IsFalse(map.ResourceSites.Any(s => s.spec == "late"), "a later Age's sites wait for it");
        foreach (var site in map.ResourceSites)
        {
            var spec = settings.ResourceSite(site.spec);
            Assert.LessOrEqual(site.cells.Count, spec.size);
            CollectionAssert.Contains(site.cells, site.center);
            Assert.GreaterOrEqual(map.StepsFromCapital(map[site.center].coord), spec.minDistance);
            foreach (int cell in site.cells)
            {
                var t = map[cell];
                Assert.AreEqual(site.index, t.resourceSite, "each cell is marked with its site");
                Assert.IsFalse(t.water || t.impassable || t.coord == map.Capital || t.HasFeature || t.grandfield >= 0, "sites stand on open, passable land");
                if (spec.terrains.Count > 0) CollectionAssert.Contains(spec.terrains, t.terrain);
            }
            Assert.IsTrue(map.ResourceSites.Where(o => o != site && o.spec == site.spec).All(o => HexCoord.Distance(map[o.center].coord, map[site.center].coord) >= spec.spacing), "two of a kind keep their spacing");
        }
        Assert.LessOrEqual(map.ResourceSites.Count(s => s.spec == "rice"), 3);

        WorldResources.PlaceAge(map, settings, 1);
        Assert.IsTrue(map.ResourceSites.Any(s => s.spec == "late"), "the Age brings its sites");
        int count = map.ResourceSites.Count;
        WorldResources.PlaceAge(map, settings, 1);
        Assert.AreEqual(count, map.ResourceSites.Count, "an Age places its sites once");
        Cache.Remove(seed);
    }

    [Test]
    public void Placement_TheSameSeedPlacesTheSameSites()
    {
        var a = Fresh(11);
        var b = Fresh(11);
        CollectionAssert.AreEqual(a.ResourceSites.Select(s => $"{s.spec}@{string.Join(",", s.cells)}"), b.ResourceSites.Select(s => $"{s.spec}@{string.Join(",", s.cells)}"));
    }

    // ===== KNOWLEDGE AND LAND VALUE =====

    [Test]
    public void Knowledge_SightedShowsOnlyTheKindAndASurveyIdentifiesIt()
    {
        var map = Fresh(7);
        var settings = Settings();
        var site = FirstOf(map, "rice");
        var t = map[site.center];
        foreach (int c in site.cells) map[c].known = map[c].explored = map[c].revealed = false;

        Assert.IsNull(WorldResources.Label(map, settings, t), "nothing is known of it in the fog");
        Assert.AreEqual(0f, WorldResources.LandValue(map, t), "an unknown site adds nothing to the land's value");
        t.known = true;
        Assert.AreEqual("Unidentified crop", WorldResources.Label(map, settings, t));
        Assert.IsEmpty(WorldResources.YieldsAt(map, settings, t), "unidentified, it yields nothing");
        Assert.IsNotNull(WorldResources.WhyNotHarvest(map, settings, t, 0), "no harvest before a survey");
        t.explored = true;
        Assert.AreEqual("Rice", WorldResources.Label(map, settings, t));
        Assert.AreEqual(3f, WorldResources.LandValue(map, t), 1e-4f);
        Assert.AreEqual(0.04f / site.cells.Count, WorldResources.YieldsAt(map, settings, t).Single().amount, 1e-5f, "a patch's yields are shared among its cells");

        // A spire is seen from afar: revealed ground is enough.
        var spire = FirstOf(map, "spire");
        var s = map[spire.center];
        s.known = s.explored = false;
        s.revealed = true;
        Assert.AreEqual("Unidentified mineral", WorldResources.Label(map, settings, s));
    }

    [Test]
    public void LandValue_IdentifiedSitesAddCityDevelopmentAndBlightsTakeIt()
    {
        var map = Fresh(7);
        var settings = Settings();
        var rules = new SettlementRules { workRadius = 2 };
        var rice = FirstOf(map, "rice");
        foreach (int c in rice.cells) map[c].explored = false;
        float before = CityDevelopment.Evaluate(map, rules, rice.center).Points(DevelopmentTerm.Resources);
        foreach (int c in rice.cells) map[c].explored = true;
        float after = CityDevelopment.Evaluate(map, rules, rice.center).Points(DevelopmentTerm.Resources);
        Assert.AreEqual(rules.resourceWeight * 3f, after - before, 1e-3f, "an identified site adds its land value once, however many of its cells are worked");

        var seep = FirstOf(map, "seep");
        map[seep.center].explored = true;
        var nearSeep = CityDevelopment.Evaluate(map, rules, seep.center);
        Assert.Less(nearSeep.Points(DevelopmentTerm.Resources), 0f, "a blight lowers the land's value");
        Assert.AreEqual(DevelopmentTerm.Resources, WorldLenses.Term(WorldLens.Resources), "the Resources lens reads the resource term");
    }

    // ===== NEIGHBOURS =====

    [Test]
    public void Neighbours_SitesMakeTheLandAroundFairerOrUglierAndShiftItsCoherence()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var bare = Settings();
        foreach (var spec in bare.resourceSites) { spec.beautyAura = spec.beauty = spec.coherenceAura = spec.fertilityAura = 0f; }
        var plain = Fresh(7, bare);

        var spire = FirstOf(map, "spire");
        var ring = map.NeighboursOf(map[spire.center]).Where(n => !n.water && n.resourceSite < 0).ToList();
        Assert.IsNotEmpty(ring);
        foreach (var n in ring) Assert.Greater(n.siteBeauty, 0f, "the spire lends its beauty to its neighbours");
        Assert.Less(map[spire.center].siteCoherence, 0f, "and takes Coherence from its own cell");
        Assert.Less(map[spire.center].coherence, plain[spire.center].coherence, "the magic hears it: Coherence falls around the spire");

        var seep = FirstOf(map, "seep");
        foreach (var n in map.NeighboursOf(map[seep.center]).Where(n => !n.water && n.resourceSite < 0))
            Assert.Less(n.siteBeauty, 0f, "a blight makes its neighbours uglier");
        Assert.Less(map[seep.center].beauty, plain[seep.center].beauty, "and its own cell hideous");

        var rice = FirstOf(map, "rice");
        Assert.Greater(map[rice.center].landFertility, plain[rice.center].landFertility - 1e-4f, "a crop that lends fertility lends it to its own cell");
    }

    [Test]
    public void Neighbours_RefreshingAgainChangesNothing()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var fertility = map.Tiles.Select(t => t.landFertility).ToArray();
        var coherence = map.Tiles.Select(t => t.coherence).ToArray();
        WorldResources.Refresh(map, settings);
        WorldResources.Refresh(map, settings);
        for (int i = 0; i < map.Count; i++)
        {
            Assert.AreEqual(fertility[i], map[i].landFertility, 1e-5f, "a site's fertility is taken back before it is lent again");
            Assert.AreEqual(coherence[i], map[i].coherence, 1e-5f);
        }
    }

    // ===== HARVEST AND GRIEVANCES =====

    [Test]
    public void Harvest_OncePerAgeAndNothingFromABlight()
    {
        var map = Fresh(7);
        var settings = Settings();
        var rice = FirstOf(map, "rice");
        var t = map[rice.center];
        t.explored = true;
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, t, 0));
        var gathered = WorldResources.HarvestAt(map, settings, t, 1.5f);
        Assert.AreEqual("Highland Rice", gathered.Single().resource);
        Assert.AreEqual(30f, gathered.Single().amount, 1e-4f, "a wild site gives its whole harvest, times the party's reward");
        t.harvestedAge = 1;
        Assert.AreEqual("Already harvested this Age.", WorldResources.WhyNotHarvest(map, settings, t, 0));
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, t, 1), "the next Age it has grown back");

        var seep = map[FirstOf(map, "seep").center];
        seep.explored = true;
        Assert.AreEqual("Leave it be.", WorldResources.WhyNotHarvest(map, settings, seep, 0), "a blight says why nothing can be taken");
    }

    [Test]
    public void Grievances_OnlyOtherHoldersAreAggrievedAndTheyFade()
    {
        var t = new WorldTile { authorityId = WorldAuthority.Wilderness };
        Assert.IsNull(WorldResources.Owner(t), "the wilderness belongs to no one");
        t.authorityId = WorldAuthority.Player;
        Assert.IsNull(WorldResources.Owner(t), "nor does your own land");
        t.authorityId = WorldAuthority.Outpost;
        Assert.IsNull(WorldResources.Owner(t));
        t.authorityId = "enclave:2";
        Assert.AreEqual("enclave:2", WorldResources.Owner(t));

        var ledger = new Dictionary<string, float>();
        WorldResources.Raise(ledger, "enclave:2", 10f);
        WorldResources.Raise(ledger, "enclave:2", 5f);
        WorldResources.Raise(ledger, null, 5f);
        Assert.AreEqual(15f, ledger["enclave:2"], 1e-4f);
        Assert.AreEqual(1, ledger.Count);
        WorldResources.Decay(ledger, 14.995f);
        Assert.IsEmpty(ledger, "a grievance that fades to nothing is forgotten");
    }

    // ===== PLANTING =====

    [Test]
    public void Planting_SeedsTakeRootOnFertileGroundYouHold()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var ground = map.Tiles.First(t => !t.water && !t.impassable && t.resourceSite < 0 && t.settlement < 0 && t.coord != map.Capital && t.landFertility >= 0.3f && t.landFertility <= 0.8f);
        ground.authorityId = WorldAuthority.Wilderness;
        Assert.AreEqual("Plant on land you hold.", WorldResources.WhyNotPlant(map, settings, ground, "rice"));
        ground.authorityId = WorldAuthority.Player;
        ground.explored = false;
        Assert.AreEqual("Survey it first.", WorldResources.WhyNotPlant(map, settings, ground, "rice"));
        ground.explored = true;
        Assert.IsNull(WorldResources.WhyNotPlant(map, settings, ground, "rice"));

        float fertility = ground.landFertility;
        WorldResources.Plant(map, ground, "rice", 0);
        WorldResources.Refresh(map, settings);
        var planted = WorldResources.SiteAt(map, ground);
        Assert.IsNotNull(planted);
        Assert.IsTrue(planted.planted);
        Assert.IsTrue(WorldResources.Identified(map, planted), "you know what you planted");
        Assert.AreEqual(3f * 0.5f, planted.landValue, 1e-4f, "a planted patch is worth its share of a wild one");
        Assert.Greater(ground.landFertility, fertility, "rice seeds enrich the soil they are planted in");
        Assert.AreEqual("Something already grows here.", WorldResources.WhyNotPlant(map, settings, ground, "rice"));
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, ground, 0), "a planted patch can be harvested");
        float share = WorldResources.HarvestAt(map, settings, ground, 1f).Single().amount;
        Assert.Greater(share, 0f);
        Assert.Less(share, 20f, "but it gives less than a wild terrace");

        float after = ground.landFertility;
        WorldResources.Refresh(map, settings);
        Assert.AreEqual(after, ground.landFertility, 1e-5f, "refreshing a planted patch does not enrich the soil twice");
    }

    // ===== SAVES =====

    [Test]
    public void Saves_SitesPlantingsCargoAndGrievancesRoundTrip()
    {
        var map = Fresh(7);
        var sites = map.ResourceSites;
        var node = SaveStateCodec.Write(sites, typeof(List<ResourceSite>));
        var back = (List<ResourceSite>)SaveStateCodec.Read(node, typeof(List<ResourceSite>));
        CollectionAssert.AreEqual(sites.Select(s => $"{s.spec}@{s.center}:{string.Join(",", s.cells)}"), back.Select(s => $"{s.spec}@{s.center}:{string.Join(",", s.cells)}"));

        var plantings = new List<Planting> { new Planting { cell = 12, spec = "rice", age = 1 } };
        var p = (List<Planting>)SaveStateCodec.Read(SaveStateCodec.Write(plantings, typeof(List<Planting>)), typeof(List<Planting>));
        Assert.AreEqual((12, "rice", 1), (p[0].cell, p[0].spec, p[0].age));

        var grievances = new Dictionary<string, float> { { "enclave:1", 12.5f } };
        var g = (Dictionary<string, float>)SaveStateCodec.Read(SaveStateCodec.Write(grievances, typeof(Dictionary<string, float>)), typeof(Dictionary<string, float>));
        Assert.AreEqual(12.5f, g["enclave:1"], 1e-4f);

        // A restored list stands where it was saved: marking it puts every cell back on its site.
        foreach (var t in map.Tiles) t.resourceSite = -1;
        map.ResourceSites = back;
        WorldResources.Mark(map);
        foreach (var site in back) foreach (int c in site.cells) Assert.AreEqual(site.index, map[c].resourceSite);
        GameSnapshot.ValidateSchema();
    }

    // ===== THE REAL CATALOG =====

    /// <summary>Every resource site of World.asset finds ground on real worlds by the end of Act I (needs Resources: Test Runner).</summary>
    [Test]
    public void Content_EveryResourceSiteFindsGroundOnRealWorlds()
    {
        GameCatalog.InvalidateAll();
        var world = GameCatalog.World.All.FirstOrDefault();
        Assert.IsNotNull(world, "No WorldSettings in Resources/World");
        var gen = world.generation;
        Assert.GreaterOrEqual(gen.resourceSites.Count, 20, "the proposal lists 20 resource sites");
        var stencil = WorldSystem.LoadStencil(gen);
        var tiles = WorldSystem.LoadTiles(gen);
        var placed = gen.resourceSites.ToDictionary(s => s.id, s => 0);
        var report = new List<string>();
        foreach (int seed in new[] { 3, 1234, 98765 })
        {
            var map = WorldGenerator.Generate(seed, gen, stencil, tiles);
            // As a new world starts: the Old World's ruins, then what grows by itself from their history and sorrow.
            WorldRuins.PlaceOldWorld(map, gen, world.settlements.loss.oldWorld);
            // Patches that sprout and later fade or turn still count as finding ground.
            var sprouted = new HashSet<ResourceSite>(WorldResources.GrowEcho(map, gen, 0, 0, initial: true).Where(c => c.kind == GrowthKind.Sprouted).Select(c => c.site));
            int echo = 0;
            for (int age = 1; age <= 3; age++)
            {
                WorldSites.PlaceAge(map, gen, age);
                // Four Echoes a Cycle; a few Cycles an Age.
                for (int e = 0; e < 8; e++)
                    foreach (var change in WorldResources.GrowEcho(map, gen, ++echo, age)) if (change.kind == GrowthKind.Sprouted) sprouted.Add(change.site);
            }
            foreach (var site in map.ResourceSites.Where(s => !sprouted.Contains(s))) placed[site.spec]++;
            foreach (var site in sprouted) placed[site.spec]++;
            report.Add($"seed {seed}: {map.ResourceSites.Count} sites on {map.Count} cells");
        }
        UnityEngine.Debug.Log("Resource sites placed over 3 worlds: " + string.Join(", ", placed.Select(p => $"{p.Key} {p.Value}")) + " | " + string.Join("; ", report));
        CollectionAssert.IsEmpty(placed.Where(p => p.Value == 0 && gen.ResourceSite(p.Key).count > 0).Select(p => p.Key), "every generated resource site finds ground on some world; count-zero descendants arrive through ecology");
        foreach (var spec in gen.resourceSites)
        {
            foreach (var amount in spec.yields.Concat(spec.harvest)) Assert.IsTrue(GameCatalog.IsResource(amount.resource), $"{spec.id}: '{amount.resource}' is a resource");
        }
        var expedition = world.Unit(world.expeditions.unit);
        Assert.IsTrue(WorldUnits.Can(expedition, UnitAbility.Harvest) && WorldUnits.Can(expedition, UnitAbility.Plant), "expeditions can harvest and plant");
    }
}
