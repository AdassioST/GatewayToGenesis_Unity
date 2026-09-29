using System.Linq;
using NUnit.Framework;

/// <summary>
/// The creatures through an Age's end (vault: Arcanorian Ecology.md, "The Aftermath of Creatures") with no scene: the crash by the crisis' severity and
/// Pure Light, ranges shifting toward the habitat left, new lineages resettling emptied Macro Biomes (Dead-zone ones only
/// on Vibrational Fallout), and scar spectra. Uses the catalog of <see cref="EcologyTests"/>.
/// </summary>
public class AftermathTests
{
    private static WorldGenSettings Settings()
    {
        var s = EcologyTests.Settings();
        // Resettling lineages: their dens are never placed at generation (count 0); they arrive only through the aftermath.
        s.species.Add(new SpeciesSpec { id = "ash", name = "Ash Lineage", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, size = CreatureSize.Small, structure = 0.9f });
        s.species.Add(new SpeciesSpec { id = "dead-zone", name = "Dead-zone Lineage", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, size = CreatureSize.Small, structure = 0.25f, niche = HarmonicNiche.Discordant });
        foreach (var id in new[] { "ash", "dead-zone" })
        {
            var den = new ResourceSiteSpec { id = id + "-den", name = id, kind = ResourceKind.Fauna, species = id, count = 0, size = 1, spacing = 6, minDistance = 2 };
            den.terrains.AddRange(new[] { "plain", "steppe", "wood", "glade" });
            s.resourceSites.Add(den);
        }
        return s;
    }

    private static WorldMap Grown(WorldGenSettings settings)
    {
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        return map;
    }

    private static float Total(WorldMap map, string species) => map.Populations.Where(p => p.species == species).Sum(p => p.groups);

    [Test]
    public void Survival_PureLightFallsAndStructurePersists()
    {
        var a = new AftermathSettings();
        var roach = new SpeciesSpec { structure = 0.95f };
        var sprite = new SpeciesSpec { structure = 0.1f };
        Assert.AreEqual(1f, WorldAftermath.Survival(a, sprite, 0f), 1e-4, "no crisis, no crash");
        Assert.AreEqual(1f - 0.5f * (0.2f + 1.2f * 0.05f), WorldAftermath.Survival(a, roach, 0.5f), 1e-4);
        Assert.AreEqual(0f, WorldAftermath.Survival(a, sprite, 1f), 1e-4, "a 90% Pure Light lineage does not come through a full crisis");
        Assert.Greater(WorldAftermath.Survival(a, roach, 1f), WorldAftermath.Survival(a, sprite, 0.3f), "high Structure persists (Pure Light.md)");
    }

    [Test]
    public void Pass_TheCrashVanishesPureLightAndThinsTheRest()
    {
        var settings = Settings();
        var map = Grown(settings);
        var deer = map.Populations.First(p => p.species == "deer");
        map.Populations.Add(new Population { slot = deer.slot, species = "sprite", groups = 3f, capacity = 3f });
        float deerBefore = Total(map, "deer");
        var report = WorldAftermath.Pass(map, settings, 0, 1f);
        Assert.AreEqual(0f, Total(map, "sprite"));
        Assert.IsTrue(report.vanished.Any(p => p.species == "sprite"));
        Assert.AreEqual(3f, report.totals["sprite"].before, 1e-4);
        Assert.AreEqual(deerBefore * WorldAftermath.Survival(settings.aftermath, settings.Species("deer"), 1f), Total(map, "deer"), 1e-3, "deer at 90% Structure come through thinned");
        Assert.AreEqual(report.totals["deer"].after, Total(map, "deer"), 1e-3);
    }

    [Test]
    public void Pass_WithoutACrisisNothingCrashes()
    {
        var settings = Settings();
        var map = Grown(settings);
        var before = map.Populations.ToDictionary(p => (p.slot, p.species), p => p.groups);
        var report = WorldAftermath.Pass(map, settings, 0, 0f);
        Assert.IsEmpty(report.vanished);
        Assert.IsEmpty(report.resettled);
        Assert.IsEmpty(report.scarred);
        foreach (var p in map.Populations) Assert.AreEqual(before[(p.slot, p.species)], p.groups, 1e-4);
    }

    [Test]
    public void Pass_ARangeWithNoHabitatLeftShiftsToItsNeighbours()
    {
        var settings = Settings();
        var map = Grown(settings);
        // The new Age leaves one of the deer's Macro Biomes without habitat (the map caches it for the Age).
        var habitat = WorldEcology.Habitat(map, settings, "deer", 0);
        var lostPopulation = map.Populations.First(p => p.species == "deer" && p.groups > 0f &&
            map.Stencil.Slots[p.slot].neighbours.Any(n => habitat.TryGetValue(n, out var h) && h.Count > 0));
        int lost = lostPopulation.slot;
        habitat.Remove(lost);
        float total = Total(map, "deer");
        var report = WorldAftermath.Pass(map, settings, 0, 0f);
        Assert.AreEqual(0f, WorldEcology.Find(map, lost, "deer").groups);
        Assert.IsTrue(report.shifted.Any(x => x.species == "deer" && x.from == lost));
        Assert.IsTrue(report.shifted.All(x => habitat.ContainsKey(x.to)), "it moves only where its habitat is");
        Assert.AreEqual(total, Total(map, "deer"), 1e-3, "nothing lost in the move");
    }

    [Test]
    public void Pass_EmptiedMacroBiomesAreResettledFromFew()
    {
        var settings = Settings();
        settings.aftermath.baseLoss = 1f; // nothing comes through: every Macro Biome with creatures is emptied
        settings.aftermath.lineages.Add(new AftermathLineage { species = "ash" });
        var map = Grown(settings);
        var had = map.Populations.Where(p => p.groups > 0f).Select(p => p.slot).Distinct().ToList();
        var report = WorldAftermath.Pass(map, settings, 0, 1f);
        Assert.IsNotEmpty(report.resettled);
        Assert.IsTrue(report.resettled.All(p => p.species == "ash" && had.Contains(p.slot)), "only emptied Macro Biomes");
        foreach (var p in report.resettled)
        {
            Assert.Greater(p.capacity, 0f);
            Assert.AreEqual(p.capacity * settings.aftermath.resettleShare, p.groups, 1e-4, "it grows from few");
        }
        Assert.IsFalse(report.scarred.Contains("ash"), "a newcomer has nothing to be scarred by");
    }

    [Test]
    public void Pass_DeadZoneLineagesSettleOnlyOnFalloutAndComeFirst()
    {
        var settings = Settings();
        settings.aftermath.baseLoss = 1f;
        settings.aftermath.lineages.Add(new AftermathLineage { species = "ash" });
        settings.aftermath.lineages.Add(new AftermathLineage { species = "dead-zone", minFalloutShare = 0.5f });
        var map = Grown(settings);
        int slot = map.Populations.First(p => p.groups > 0f).slot;
        Assert.IsFalse(WorldAftermath.Candidates(map, settings, settings.aftermath, slot, 0).Any(l => l.species == "dead-zone"), "no fallout, no Dead-zone lineage");

        foreach (int i in WorldEcology.Habitat(map, settings, "dead-zone", 0)[slot]) map[i].fallout = 0.6f;
        map.habitat = null;
        Assert.AreEqual("dead-zone", WorldAftermath.Candidates(map, settings, settings.aftermath, slot, 0).First().species, "fallout calls its lineage first");
        var report = WorldAftermath.Pass(map, settings, 0, 1f);
        Assert.AreEqual("dead-zone", report.resettled.Single(p => p.slot == slot).species);
    }

    [Test]
    public void Pass_ScarredLineagesKeepMoreOfTheirMemory()
    {
        var settings = Settings();
        var map = Grown(settings);
        var deer = map.Populations.First(p => p.species == "deer");
        map.Populations.Add(new Population { slot = deer.slot, species = "sprite", groups = 3f, capacity = 3f });
        map.Behaviors.Add(new SpeciesBehavior { species = "deer", overall = -1f, bonds = { new BehaviorBond { authority = "player", temper = -1f } } });
        map.Behaviors.Add(new SpeciesBehavior { species = "sprite", overall = -1f, bonds = { new BehaviorBond { authority = "player", temper = -1f } } });
        var report = WorldAftermath.Pass(map, settings, 0, 1f);
        CollectionAssert.Contains(report.scarred, "sprite");
        CollectionAssert.DoesNotContain(report.scarred, "deer", "deer lost under half");
        Assert.AreEqual(-settings.aftermath.scarKept, map.Behaviors.Single(b => b.species == "sprite").bonds[0].temper, 1e-4);
        Assert.AreEqual(-settings.behavior.memoryKept, map.Behaviors.Single(b => b.species == "deer").bonds[0].temper, 1e-4, "the others ease back as usual");
    }
}
