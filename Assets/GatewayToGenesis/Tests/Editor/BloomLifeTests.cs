using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Eleos Blooms among the living, with no scene (<see cref="WorldResources"/>): the blooms a settlement's life draws up
/// around it once Lumen Seeds reach the ground (<see cref="WorldResources.Volunteer"/>), patches that creep toward
/// better ground Echo by Echo (<see cref="WorldResources.Drift"/>), and the creatures drawn to blooms
/// (<see cref="WorldResources.BloomGround"/>, <see cref="WorldEcology.BloomGain"/>). Uses the small catalog of
/// <see cref="WorldGenerationTests"/> plus blooms of its own.
/// </summary>
public class BloomLifeTests
{
    private static WorldGenSettings Settings()
    {
        var s = WorldGenerationTests.Settings();
        s.eleos.volunteerChance = 1f;
        s.eleos.seedCoherence = 2f; // never: each test says how the seeds come
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "marigold", name = "Marigold", kind = ResourceKind.Bloom, niche = BloomNiche.Listener, count = 0, size = 1, residueNeed = 0.2f,
            drawnBy = BloomDraw.Content, untouchable = "No.",
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "moss", name = "Moss", kind = ResourceKind.Bloom, niche = BloomNiche.Listener, count = 0, size = 2, residueNeed = 0.2f,
            drawnBy = BloomDraw.Suffering, untouchable = "No.",
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "berry", name = "Berry", kind = ResourceKind.Bloom, niche = BloomNiche.Predator, dangerAura = 0.1f, count = 0, size = 1, residueNeed = 0.2f,
            districts = { "indulgent" }, untouchable = "No.",
        });
        s.species.Add(new SpeciesSpec { id = "bee", name = "Bee", diet = CreatureDiet.Herbivore, blooms = { "listener" } });
        s.species.Add(new SpeciesSpec { id = "ox", name = "Ox", diet = CreatureDiet.Herbivore });
        return s;
    }

    private static WorldMap Fresh(int seed, WorldGenSettings settings) =>
        WorldGenerator.Generate(seed, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    // The world's only settlement (its Capital) in the mood and with the district asked, and the residue it leaves.
    private static Settlement Settle(WorldMap map, WorldGenSettings settings, float strain = 0f, string district = null)
    {
        var s = WorldCivilization.Capital(map);
        if (s == null) map.Settlements.Add(s = new Settlement { id = 0, name = "Hearth", kind = SettlementKind.Capital, coord = map.Capital });
        map.Settlements.RemoveAll(x => x != s);
        s.strain = strain;
        s.district = district;
        WorldResources.Residue(map, settings.eleos);
        return s;
    }

    private static void SeedNear(WorldMap map, Settlement s, int reach = 2)
    {
        var cell = map.Tiles.First(t => !t.water && HexCoord.Distance(t.coord, s.coord) == reach);
        map.Plantings.Add(new Planting { cell = cell.index, spec = "rice", age = 0 });
    }

    // ===== VOLUNTEER BLOOMS =====

    [Test]
    public void Volunteer_NothingGrowsUntilLumenSeedsReachTheGround()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var s = Settle(map, settings);
        Assert.IsNull(WorldResources.SeedSource(map, settings, s));
        Assert.IsEmpty(WorldResources.Volunteer(map, settings, 1, 0), "no seeds, no blooms: feeling alone cannot make one");

        SeedNear(map, s);
        Assert.AreEqual("seeds", WorldResources.SeedSource(map, settings, s), "rice planted nearby carries lumen grains in its soil");
        var changes = WorldResources.Volunteer(map, settings, 1, 0);
        var grown = changes.Single();
        Assert.AreEqual(GrowthKind.Volunteered, grown.kind);
        Assert.AreEqual("marigold", grown.site.spec, "a Content settlement draws up the gentle blooms");
        Assert.AreEqual("ease", grown.reason);
        Assert.AreEqual(s.id, grown.site.tendedBy);
        foreach (int c in grown.site.cells)
        {
            int d = HexCoord.Distance(map[c].coord, s.coord);
            Assert.That(d >= 1 && d <= settings.eleos.volunteerReach, "beside the settlement, never on it");
            Assert.AreEqual(grown.site.index, map[c].resourceSite);
        }
    }

    [Test]
    public void Volunteer_HighCoherenceAndPollinatorsSeedTheGroundToo()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var s = Settle(map, settings);
        settings.eleos.seedCoherence = 0f;
        Assert.AreEqual("coherence", WorldResources.SeedSource(map, settings, s), "sprite pollinators live where the Loom is whole");

        settings.eleos.seedCoherence = 2f;
        var here = map.Get(s.coord);
        if (here.habitatSlot < 0) here.habitatSlot = 0;
        map.Populations.Add(new Population { slot = here.habitatSlot, species = "ox", groups = 1f, capacity = 1f });
        Assert.IsNull(WorldResources.SeedSource(map, settings, s), "a creature that cares nothing for blooms carries no seeds");
        map.Populations.Add(new Population { slot = here.habitatSlot, species = "bee", groups = 0.1f, capacity = 1f });
        Assert.IsNull(WorldResources.SeedSource(map, settings, s), "too few pollinators");
        map.Populations.Last().groups = 0.8f;
        Assert.AreEqual("pollinators", WorldResources.SeedSource(map, settings, s));
    }

    [Test]
    public void Volunteer_SufferingDrawsGriefAndTheGentleBloomsFade()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var s = Settle(map, settings);
        SeedNear(map, s);
        var gentle = WorldResources.Volunteer(map, settings, 1, 0).Single().site;
        Assert.AreEqual("marigold", gentle.spec);

        s.strain = settings.eleos.sufferingStrain + 10f;
        Assert.AreEqual(BloomDraw.Suffering, WorldResources.MoodOf(settings.eleos, s));
        var changes = new List<GrowthChange>();
        for (int echo = 2; echo <= 6; echo++) changes.AddRange(WorldResources.GrowEcho(map, settings, echo, 0));
        Assert.IsTrue(changes.Any(c => c.kind == GrowthKind.Volunteered && c.site.spec == "moss" && c.reason == "grief"), "grief draws up the dark blooms");
        Assert.IsTrue(changes.Any(c => c.kind == GrowthKind.Faded && c.from == "marigold"), "the gentle ones fade once the settlement suffers");
        Assert.IsFalse(map.ResourceSites.Any(x => x.spec == "marigold"));
        Assert.LessOrEqual(map.ResourceSites.Count(x => x.tendedBy == s.id), settings.eleos.volunteersPerSettlement, "a settlement keeps only so many");

        // Between the moods nothing new is drawn, and what grief drew fades once the settlement recovers.
        s.strain = (settings.eleos.contentStrain + settings.eleos.sufferingStrain) / 2f;
        Assert.AreEqual(BloomDraw.None, WorldResources.MoodOf(settings.eleos, s));
        for (int echo = 7; echo <= 10; echo++) WorldResources.GrowEcho(map, settings, echo, 0);
        Assert.IsFalse(map.ResourceSites.Any(x => x.tendedBy == s.id), "with no mood to feed them, the volunteers fade");
    }

    [Test]
    public void Volunteer_DistrictsDrawTheirOwnBlooms()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var s = Settle(map, settings, strain: 25f, district: "indulgent");
        SeedNear(map, s);
        var grown = WorldResources.Volunteer(map, settings, 1, 0).Single();
        Assert.AreEqual("berry", grown.site.spec, "the Indulgent District's revels draw Lust Berries, whatever the mood");
        Assert.AreEqual("indulgent", grown.reason);
        Assert.IsFalse(WorldResources.Draws(settings, new Settlement { strain = 25f, district = "weaver" }, settings.ResourceSite("berry")), "only their district draws them");
    }

    [Test]
    public void Volunteer_TheSameWorldGrowsTheSameWay()
    {
        var settings = Settings();
        var a = Fresh(7, settings);
        var b = Fresh(7, settings);
        foreach (var map in new[] { a, b })
        {
            var s = Settle(map, settings);
            SeedNear(map, s);
            for (int echo = 1; echo <= 4; echo++) WorldResources.GrowEcho(map, settings, echo, 0);
        }
        CollectionAssert.AreEqual(a.ResourceSites.Select(x => (x.spec, x.center)), b.ResourceSites.Select(x => (x.spec, x.center)));
    }

    [Test]
    public void Volunteer_SterileBloomsAndDensNeverVolunteer()
    {
        var s = new Settlement { strain = 0f };
        var settings = Settings();
        Assert.IsFalse(WorldResources.Draws(settings, s, new ResourceSiteSpec { id = "f", kind = ResourceKind.Bloom, drawnBy = BloomDraw.Content, sprouts = true }));
        Assert.IsFalse(WorldResources.Draws(settings, s, new ResourceSiteSpec { id = "t", kind = ResourceKind.Bloom, drawnBy = BloomDraw.Content, species = "bee" }));
        Assert.IsFalse(WorldResources.Draws(settings, s, new ResourceSiteSpec { id = "c", kind = ResourceKind.Crop, drawnBy = BloomDraw.Content }));
        Assert.IsTrue(WorldResources.Draws(settings, s, new ResourceSiteSpec { id = "e", kind = ResourceKind.Bloom, drawnBy = BloomDraw.Either }));
    }

    // ===== DRIFTING =====

    private static WorldGenSettings Fated()
    {
        var s = WorldGenerationTests.Settings();
        s.eleos.historicTerrains = new List<string> { "ruin", "ash", "scar" };
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "fated", name = "Fated", kind = ResourceKind.Bloom, count = 4, size = 2, spacing = 4, minDistance = 2, residueNeed = 0f,
            sprouts = true, sproutPerEcho = 2, minimumHistory = 0.3f, maximumHurt = 0.25f, driftPerEcho = 1f, fadePerEcho = 0.25f, untouchable = "No.",
        });
        return s;
    }

    [Test]
    public void Drift_FatedFlowersWanderOverGroundThatRemembers()
    {
        var settings = Fated();
        var map = Fresh(7, settings);
        WorldResources.GrowEcho(map, settings, 0, 0, initial: true);
        var fated = map.ResourceSites.Where(s => s.spec == "fated").ToList();
        Assert.IsNotEmpty(fated);
        var sizes = fated.ToDictionary(s => s, s => s.cells.Count);
        var before = fated.ToDictionary(s => s, s => s.cells.ToList());
        int moved = 0;
        for (int echo = 1; echo <= 6; echo++)
            foreach (var site in map.ResourceSites.Where(s => s.spec == "fated").ToList())
                if (WorldResources.Drift(map, settings, site, settings.ResourceSite("fated"), echo)) moved++;
        Assert.Greater(moved, 0, "the fields move, Echo by Echo");
        Assert.IsTrue(fated.Any(s => !s.cells.SequenceEqual(before[s])), "some field stands elsewhere now");
        foreach (var site in fated)
        {
            Assert.AreEqual(sizes[site], site.cells.Count, "a patch keeps its size as it creeps");
            Assert.Contains(site.center, site.cells);
            foreach (int c in site.cells)
            {
                Assert.AreEqual(site.index, map[c].resourceSite, "the map knows where it went");
                Assert.GreaterOrEqual(map[c].history, 0.3f, "only over ground that remembers");
                Assert.LessOrEqual(map[c].hurt, 0.25f, "and away from sorrow");
            }
        }
        Assert.AreEqual(map.ResourceSites.Sum(s => s.cells.Count), map.Tiles.Count(t => t.resourceSite >= 0), "no cell is left marked behind");
    }

    [Test]
    public void Drift_AWitheringBloomCannotMoveAndVolunteersStayHome()
    {
        var settings = WorldGenerationTests.Settings();
        settings.eleos.ambientResidue = 0f;
        settings.eleos.dissonanceResidue = 0f;
        var spec = new ResourceSiteSpec { id = "maw", name = "Maw", kind = ResourceKind.Bloom, niche = BloomNiche.Predator, dangerAura = 0.3f, residueNeed = 1f, driftPerEcho = 1f };
        settings.resourceSites.Add(spec);
        var map = Fresh(7, settings);
        WorldResources.Residue(map, settings.eleos);
        var far = map.Tiles.First(t => !t.water && !t.impassable && t.residue == 0f && t.resourceSite < 0 && map.StepsFromCapital(t.coord) > 6);
        var site = new ResourceSite { index = map.ResourceSites.Count, spec = "maw", name = "Maw", kind = ResourceKind.Bloom, center = far.index };
        site.cells.Add(far.index);
        map.ResourceSites.Add(site);
        WorldResources.Mark(map);
        Assert.IsFalse(WorldResources.Drift(map, settings, site, spec, 1), "no residue, no movement");

        spec.residueNeed = 0f;
        site.tendedBy = 3;
        Assert.IsFalse(WorldResources.Drift(map, settings, site, spec, 1), "a volunteer stays by the settlement that drew it");
        site.tendedBy = -1;
        spec.driftPerEcho = 0f;
        Assert.IsFalse(WorldResources.Drift(map, settings, site, spec, 1), "a bloom that never moves");
    }

    // ===== THE FOOD WEB =====

    [Test]
    public void FoodWeb_BloomsDrawTheirCreaturesAndTheirRangesHoldMore()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var s = Settle(map, settings);
        SeedNear(map, s);
        var bloom = WorldResources.Volunteer(map, settings, 1, 0).Single().site;
        var bee = settings.Species("bee");
        var ox = settings.Species("ox");
        Assert.IsTrue(WorldResources.DrawsCreature(bee, settings.ResourceSite(bloom.spec)), "a pollinator is drawn to a listener");
        Assert.IsFalse(WorldResources.DrawsCreature(ox, settings.ResourceSite(bloom.spec)));
        Assert.IsFalse(WorldResources.DrawsCreature(bee, settings.ResourceSite("berry")), "but not to a predator");

        var ground = WorldResources.BloomGround(map, settings, bee);
        Assert.IsTrue(ground.ContainsKey(bloom.center));
        Assert.IsEmpty(WorldResources.BloomGround(map, settings, ox));
        var e = settings.ecology;
        var range = map.Tiles.Where(t => !t.water && HexCoord.Distance(t.coord, map[bloom.center].coord) <= 6).Select(t => t.index).ToList();
        float draw = WorldEcology.BloomGain(e, ground, range);
        Assert.Greater(draw, 1f, "a range with blooms in it holds more of what they draw");
        Assert.LessOrEqual(draw, 1f + e.bloomDraw + 1e-4f);
        Assert.AreEqual(1f, WorldEcology.BloomGain(e, ground, map.Tiles.Where(t => !ground.ContainsKey(t.index)).Select(t => t.index).ToList()), 1e-4f, "and a range without them no more");
    }
}
