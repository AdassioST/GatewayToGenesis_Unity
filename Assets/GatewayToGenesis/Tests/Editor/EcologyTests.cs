using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Macro Biomes' creatures with no scene (<see cref="WorldEcology"/>; vault: Arcanorian Ecology.md, "Where Creatures Live"): populations founded
/// where dens stand, capacity from habitat, people's pressure and Pure Light fragility, predators and prey, hunting to
/// depletion and recovery, migration by ranging, and determinism. Uses the small catalog of <see cref="WorldGenerationTests"/>.
/// </summary>
public class EcologyTests
{
    internal static WorldGenSettings Settings()
    {
        var s = WorldGenerationTests.Settings();
        s.species.Add(new SpeciesSpec { id = "deer", name = "Deer", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, size = CreatureSize.Large, structure = 0.9f });
        s.species.Add(new SpeciesSpec { id = "wolf", name = "Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Medium, structure = 0.9f, prey = { "deer" } });
        s.species.Add(new SpeciesSpec { id = "mole", name = "Mole", diet = CreatureDiet.Detritivore, subgroup = CreatureSubgroup.Hiding, size = CreatureSize.Small, structure = 0.95f });
        s.species.Add(new SpeciesSpec { id = "sprite", name = "Sprite", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Frightful, size = CreatureSize.Small, structure = 0.1f });
        string[] open = { "plain", "steppe", "wood", "glade" };
        s.resourceSites.Add(Den("herd", "deer", 3, open));
        s.resourceSites.Add(Den("pack", "wolf", 2, open));
        s.resourceSites.Add(Den("burrow", "mole", 2, open));
        return s;
    }

    private static ResourceSiteSpec Den(string id, string species, int count, string[] terrains)
    {
        var spec = new ResourceSiteSpec
        {
            id = id, name = id, kind = ResourceKind.Fauna, species = species, count = count, size = 1, spacing = 6, minDistance = 2, landValue = 1f,
            yields = { new ResourceAmount { resource = "Game Meat", amount = 0.1f } },
            harvest = { new ResourceAmount { resource = "Game Meat", amount = 10f } },
        };
        spec.terrains.AddRange(terrains);
        return spec;
    }

    internal static WorldMap Fresh(int seed, WorldGenSettings settings) =>
        WorldGenerator.Generate(seed, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    internal static ResourceSite DenOf(WorldMap map, string spec) => map.ResourceSites.First(s => s.spec == spec);

    [Test]
    public void Tick_FoundsAPopulationWhereverADenStands()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        Assert.AreEqual(1f, WorldEcology.AbundanceAt(map, settings, DenOf(map, "herd")), "before the first Echo a den is full");
        WorldEcology.Tick(map, settings, 0, 1);
        foreach (var den in map.ResourceSites.Where(s => WorldEcology.SpeciesAt(settings, s) != null))
        {
            var p = WorldEcology.PopulationAt(map, settings, den);
            Assert.IsNotNull(p, $"den {den.spec} has its Macro Biome's population");
            Assert.AreEqual(map[den.center].habitatSlot, p.slot);
            Assert.Greater(p.capacity, 0f);
            Assert.That(p.groups, Is.GreaterThan(0f).And.LessThanOrEqualTo(p.capacity * 1.001f));
        }
        Assert.IsTrue(map.Tiles.Where(t => t.composition == WorldComposition.MacroBiome).All(t => t.habitatSlot == t.slot), "a Macro Biome cell's range is its own slot");
        Assert.IsTrue(map.Tiles.Any(t => t.composition == WorldComposition.Intersection && t.habitatSlot >= 0), "intersections belong to the nearest range");
    }

    [Test]
    public void Hunting_DepletesADenAndLeavingItAloneLetsItRecover()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = DenOf(map, "herd");
        var tile = map[den.center];
        tile.explored = true; // identified
        float fullHarvest = WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount);
        for (int i = 0; i < 30 && !WorldEcology.Depleted(map, settings, den); i++) WorldEcology.Hunt(map, settings, den, WorldAuthority.Player, 1f);
        Assert.IsTrue(WorldEcology.Depleted(map, settings, den), "hunting again and again thins it out");
        Assert.AreEqual(0f, WorldResources.Plenty(map, settings, den));
        Assert.IsEmpty(WorldResources.YieldsAt(map, settings, tile), "a depleted den yields nothing");
        StringAssert.Contains("Too few", WorldResources.WhyNotHarvest(map, settings, tile, 0, 2));
        var changes = new List<EcologyChange>();
        for (int echo = 2; echo < 40 && WorldEcology.Depleted(map, settings, den); echo++) changes.AddRange(WorldEcology.Tick(map, settings, 0, echo));
        Assert.IsFalse(WorldEcology.Depleted(map, settings, den), "left alone, it grows back");
        Assert.IsTrue(changes.Any(c => c.kind == EcologyChangeKind.Recovered && c.site == den));
        Assert.Less(WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount), fullHarvest + 0.001f, "a harvest never exceeds a full den's");
        Assert.Greater(WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount), 0f);
    }

    [Test]
    public void Hunting_ADenOnceAPhaseAndOtherSitesOnceAnAge()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = DenOf(map, "herd");
        var tile = map[den.center];
        tile.explored = true;
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, tile, 0, 4, 12));
        den.huntedPhase = 4;
        string why = WorldResources.WhyNotHarvest(map, settings, tile, 0, 4, 12);
        StringAssert.Contains("this Phase", why);
        StringAssert.Contains("ready again in 12 Sevenths", why, "the cooldown says when it lifts");
        StringAssert.Contains("in 1 Seventh.", WorldResources.WhyNotHarvest(map, settings, tile, 0, 4, 1));
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, tile, 0, 5, 21), "the next Phase it can be hunted again");
        tile.harvestedAge = 1;
        Assert.IsNull(WorldResources.WhyNotHarvest(map, settings, tile, 0, 5, 21), "the once-an-Age rule is not a den's");
    }

    [Test]
    public void Hunting_OneHuntBringsAShareOfTheHarvest()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        var den = DenOf(map, "herd");
        var tile = map[den.center];
        tile.explored = true;
        float full = settings.ResourceSite("herd").harvest.Sum(a => a.amount);
        Assert.AreEqual(full * settings.ecology.huntYield, WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount), 0.01f,
            "a full den gives one hunt's share of its listed harvest");
    }

    // Echoes of hunting a herd (medium pace, no predators) every so many Phases: whether it is ever depleted.
    private static bool DepletedHunting(int huntsPerEcho, int echoes)
    {
        var settings = Settings();
        settings.Species("deer").reproduction = ReproductionPace.Medium;
        settings.Species("wolf").prey.Clear();
        var map = Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = DenOf(map, "herd");
        for (int echo = 2; echo <= echoes; echo++)
        {
            for (int h = 0; h < huntsPerEcho; h++)
            {
                if (WorldEcology.Depleted(map, settings, den)) return true;
                WorldEcology.Hunt(map, settings, den, WorldAuthority.Player, 1f);
            }
            WorldEcology.Tick(map, settings, 0, echo);
            if (WorldEcology.Depleted(map, settings, den)) return true;
        }
        return false;
    }

    [Test]
    public void Hunting_EveryPhaseWearsAHerdDownWhileOnceAnEchoItHolds()
    {
        Assert.IsTrue(DepletedHunting(3, 12), "hunted every Phase, a medium-pace herd is thinned out within a few Echoes");
        Assert.IsFalse(DepletedHunting(1, 24), "hunted once an Echo, it holds");
    }

    [Test]
    public void Pressure_SettlementsAndHeldLandPressHardestOnSpeciesThatFlee()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        var e = settings.ecology;
        var pressure = WorldEcology.Pressure(map, e);
        var capital = map.Get(map.Capital);
        Assert.AreEqual(e.settlementPressure + (WorldEcology.Held(capital) ? e.heldPressure : 0f), pressure[capital.index], 0.001f);
        var far = map.Tiles.First(t => HexCoord.Distance(t.coord, map.Capital) > 10 && !WorldEcology.Held(t) && !t.water);
        Assert.AreEqual(0f, pressure[far.index]);
        var deer = settings.Species("deer");
        var wolf = settings.Species("wolf");
        var ground = new WorldTile { coherence = 0.8f };
        Assert.Less(WorldEcology.Quality(e, deer, ground, 0.3f), WorldEcology.Quality(e, wolf, ground, 0.3f), "a docile herd flees people; a pack does not");
        Assert.AreEqual(1f, WorldEcology.Quality(e, wolf, ground, 0f));
    }

    [Test]
    public void PureLight_SpeciesNeedCoherenceAndStructuredOnesDoNot()
    {
        var settings = Settings();
        var e = settings.ecology;
        var sprite = settings.Species("sprite");
        var deer = settings.Species("deer");
        var coherent = new WorldTile { coherence = 0.9f };
        var wounded = new WorldTile { coherence = 0.2f };
        Assert.AreEqual(1f, WorldEcology.Quality(e, sprite, coherent, 0f));
        Assert.Less(WorldEcology.Quality(e, sprite, wounded, 0f), 0.01f, "at 90% Pure Light a sprite needs 0.54 Coherence and fades well above 0.2");
        Assert.AreEqual(1f, WorldEcology.Quality(e, deer, wounded, 0f), "a 90% Structure beast barely notices");
    }

    [Test]
    public void Predators_HoldLessWhereTheirPreyIsGone()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var pack = WorldEcology.PopulationAt(map, settings, DenOf(map, "pack"));
        float withPrey = pack.capacity;
        foreach (var p in map.Populations.Where(p => p.species == "deer")) p.groups = 0f;
        WorldEcology.Tick(map, settings, 0, 2);
        Assert.Less(pack.capacity, withPrey + 0.0001f);
        var habitat = WorldEcology.Habitat(map, settings, "wolf", 0);
        float land = WorldEcology.Capacity(map, settings.ecology, settings.Species("wolf"), habitat[pack.slot], WorldEcology.Pressure(map, settings.ecology));
        Assert.AreEqual(land * settings.ecology.preyFloor, pack.capacity, 0.001f, "with no prey at all the land holds only the floor");
    }

    [Test]
    public void Migration_RoamingHerdsSpreadAndSettledBurrowersStayPut()
    {
        var settings = Settings();
        var map = Fresh(42, settings);
        var deerSlots = new HashSet<int>(map.ResourceSites.Where(s => s.spec == "herd").Select(s => map[s.center].habitatSlot));
        var moleSlots = new HashSet<int>(map.ResourceSites.Where(s => s.spec == "burrow").Select(s => map[s.center].habitatSlot));
        var changes = new List<EcologyChange>();
        for (int echo = 1; echo <= 24; echo++) changes.AddRange(WorldEcology.Tick(map, settings, 0, echo));
        var deer = map.Populations.Where(p => p.species == "deer").ToList();
        Assert.IsTrue(deer.Any(p => !deerSlots.Contains(p.slot)), "a roaming herd spreads into neighbouring Macro Biomes");
        Assert.IsTrue(map.Populations.Where(p => p.species == "mole").All(p => moleSlots.Contains(p.slot)), "settled creatures never leave");
        foreach (var arrival in changes.Where(c => c.kind == EcologyChangeKind.Arrived))
        {
            Assert.AreEqual(arrival.population.slot, map[arrival.site.center].habitatSlot, "a new den stands in the range it spread to");
            Assert.AreEqual(arrival.population.species, WorldEcology.SpeciesAt(settings, arrival.site).id);
        }
    }

    // The real bestiary (World.asset) on a real world, over two Ages of Echoes: every species with dens lives
    // somewhere, nothing runs away to absurd numbers, and one Echo stays quick. Needs the Test Runner (Resources).
    [Test]
    public void Content_TheRealBestiaryLivesOnARealWorld()
    {
        GameCatalog.InvalidateAll();
        var world = GameCatalog.World.All.FirstOrDefault();
        Assert.IsNotNull(world, "No WorldSettings in Resources/World");
        var gen = world.generation;
        var map = WorldGenerator.Generate(1234, gen, WorldSystem.LoadStencil(gen), WorldSystem.LoadTiles(gen));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        WorldEcology.Tick(map, gen, 0, 1);
        long first = watch.ElapsedMilliseconds;
        int echo = 1;
        for (int age = 0; age <= 1; age++)
        {
            if (age > 0) WorldSites.PlaceAge(map, gen, age);
            for (int e = 0; e < 8; e++)
            {
                watch.Restart();
                // The calendar turns (WorldRhythm): the Echo just lived shapes the creatures, and each Phase ends on a Ritual Seventh.
                map.echo = echo % TimeSystemLogic.EchoesPerCycle + 1;
                int season = WorldRhythm.Previous(map.echo);
                WorldEcology.Tick(map, gen, age, ++echo, season);
                WorldBehavior.Tick(map, gen, age, season);
                for (int phase = 1; phase <= TimeSystemLogic.PhasesPerEcho; phase++)
                {
                    map.echoPhase = phase;
                    map.ritualSeventh = true;
                    WorldRhythm.Attune(map, gen, age);
                }
                map.ritualSeventh = false;
                // The first Echo of an Age rebuilds the habitat (once an Age, like the first Echo of a world); the rest stay quick.
                Assert.Less(watch.ElapsedMilliseconds, e == 0 ? 1000 : 100, $"Echo {echo} took {watch.ElapsedMilliseconds} ms");
            }
        }
        var dens = map.ResourceSites.Where(s => WorldEcology.SpeciesAt(gen, s) != null).Select(s => WorldEcology.SpeciesAt(gen, s).id).Distinct().ToList();
        CollectionAssert.IsNotEmpty(dens);
        foreach (string species in dens)
            Assert.IsTrue(map.Populations.Any(p => p.species == species && p.groups > 0f), $"{species} lives somewhere");
        Assert.IsTrue(map.Populations.All(p => p.groups <= p.capacity * 1.5f + 0.01f), "no population runs far past its land");
        Assert.IsTrue(map.Behaviors.SelectMany(s => s.bonds).All(b => b.temper >= -1f && b.temper <= 1f), "tempers stay within -1 and +1");
        UnityEngine.Debug.Log($"Ecology on seed 1234: first Echo {first} ms (habitat), {map.Populations.Count} populations: " +
            string.Join(", ", map.Populations.GroupBy(p => p.species).Select(g => $"{g.Key} {g.Count()} ranges {g.Sum(p => p.groups):0} groups")));
    }

    [Test]
    public void Tick_TheSameWorldAndEchoesGiveTheSameCreatures()
    {
        string Run()
        {
            var settings = Settings();
            var map = Fresh(11, settings);
            for (int echo = 1; echo <= 8; echo++) WorldEcology.Tick(map, settings, 0, echo);
            return string.Join(";", map.Populations.Select(p => $"{p.slot}:{p.species}:{p.groups:0.0000}")) + "|" + map.ResourceSites.Count;
        }
        Assert.AreEqual(Run(), Run());
    }
}
