using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Great Plague's moths (vault: Arcanorian Ecology.md, "The Great Plague's Moths") with no scene: trade routes carry them, a Domestication Enclave
/// trades them to your settlements, they breed on the sick while the plague runs, their part is revealed midway, and the
/// trading Enclave can be asked to close its farms. Uses the catalog of <see cref="EcologyTests"/>.
/// </summary>
public class GreatPlagueTests
{
    private static WorldGenSettings Settings()
    {
        var s = EcologyTests.Settings();
        s.species.Add(new SpeciesSpec { id = "moth", name = "Moth", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, size = CreatureSize.Small, structure = 0.9f, vector = 0.6f });
        var den = new ResourceSiteSpec { id = "swarm", name = "Swarm", kind = ResourceKind.Fauna, species = "moth", count = 0, size = 1, spacing = 6, minDistance = 2 };
        den.terrains.AddRange(new[] { "plain", "steppe", "wood", "glade" });
        s.resourceSites.Add(den);
        s.enclaves.Add(new EnclaveSpec { id = "farm", name = "Farm", family = EnclaveFamily.Domestication, trades = "moth" });
        s.enclaves.Add(new EnclaveSpec { id = "herders", name = "Herders", family = EnclaveFamily.Domestication });
        s.greatPlague.vector = "moth";
        return s;
    }

    // A bare world: no routes, Enclaves or settlements of its own, so each test places what it needs.
    private static WorldMap Bare(WorldGenSettings settings)
    {
        var map = EcologyTests.Fresh(42, settings);
        map.Routes.Clear();
        map.Enclaves.Clear();
        map.Settlements.Clear();
        return map;
    }

    private static int[] MothSlots(WorldMap map, WorldGenSettings settings) =>
        WorldEcology.Habitat(map, settings, "moth", 0).Where(x => x.Value.Count > 0).Select(x => x.Key).OrderBy(x => x).ToArray();

    private static WorldTile CellOf(WorldMap map, WorldGenSettings settings, int slot) =>
        map[WorldEcology.Habitat(map, settings, "moth", 0)[slot][0]];

    [Test]
    public void Revealed_MidwayThroughThePlagueOrOnceUnderstood()
    {
        var g = new GreatPlagueSettings();
        Assert.IsFalse(WorldGreatPlague.Revealed(g, "age-of-renewal", -1, false, SpeciesLevel.Identified), "before the crisis no one suspects them");
        Assert.IsFalse(WorldGreatPlague.Revealed(g, "age-of-renewal", 0, false, SpeciesLevel.Observed), "nor in its first stage");
        Assert.IsTrue(WorldGreatPlague.Revealed(g, "age-of-renewal", 1, false, SpeciesLevel.Unknown), "The Moths Are Everywhere");
        Assert.IsFalse(WorldGreatPlague.Revealed(g, "age-of-seeds", 3, false, SpeciesLevel.Unknown), "another Age's crisis says nothing of them");
        Assert.IsTrue(WorldGreatPlague.Revealed(g, "age-of-seeds", -1, true, SpeciesLevel.Unknown), "once the plague has passed, it is history");
        Assert.IsTrue(WorldGreatPlague.Revealed(g, "age-of-seeds", -1, false, SpeciesLevel.Understood), "an Auric study finds it early");
    }

    [Test]
    public void Echo_TradeRoutesCarryTheMothsIntoTheLandsTheyPass()
    {
        var settings = Settings();
        var map = Bare(settings);
        var slots = MothSlots(map, settings);
        Assert.GreaterOrEqual(slots.Length, 2);
        int from = slots[0], to = slots[1];
        map.Populations.Add(new Population { slot = from, species = "moth", groups = 2f, capacity = 4f });
        map.Routes.Add(new TradeRoute { cells = { CellOf(map, settings, from).coord, CellOf(map, settings, to).coord } });
        var echo = WorldGreatPlague.Echo(map, settings, 0, 1, new PlagueInputs());
        CollectionAssert.AreEqual(new[] { to }, echo.carried);
        Assert.AreEqual(settings.greatPlague.routeCarry * 2f, WorldEcology.Find(map, to, "moth").groups, 1e-4);
        Assert.Greater(WorldEcology.Find(map, to, "moth").capacity, 0f, "the new range knows what its land holds");
        Assert.AreEqual(2f, WorldEcology.Find(map, from, "moth").groups, 1e-4, "carrying them costs the source nothing");
    }

    [Test]
    public void Echo_TheTradingEnclaveBringsThemUntilItsFarmsAreClosed()
    {
        var settings = Settings();
        var map = Bare(settings);
        int slot = MothSlots(map, settings)[0];
        var cell = CellOf(map, settings, slot);
        map.Settlements.Add(new Settlement { id = 1, name = "Here", coord = cell.coord });
        var farm = new Enclave { index = 0, spec = "farm", name = "Farm", family = EnclaveFamily.Domestication, coord = cell.coord };
        var herders = new Enclave { index = 1, spec = "herders", name = "Herders", family = EnclaveFamily.Domestication, coord = cell.coord };
        map.Enclaves.Add(farm);
        map.Enclaves.Add(herders);
        Assert.IsTrue(WorldGreatPlague.Trades(settings, farm));
        Assert.IsFalse(WorldGreatPlague.Trades(settings, herders));

        var echo = WorldGreatPlague.Echo(map, settings, 0, 1, new PlagueInputs());
        Assert.AreEqual(settings.greatPlague.tradeGroups, echo.traded, 1e-4, "one trading Enclave, one Macro Biome of yours");
        Assert.AreEqual(settings.greatPlague.tradeGroups, WorldEcology.Find(map, slot, "moth").groups, 1e-4);

        StringAssert.Contains("trades no moths", WorldGreatPlague.WhyNotRestrict(map, settings, herders, 1, true));
        cell.revealed = false;
        StringAssert.Contains("Find it first", WorldGreatPlague.WhyNotRestrict(map, settings, farm, 1, true));
        cell.revealed = true;
        StringAssert.Contains("No one yet knows", WorldGreatPlague.WhyNotRestrict(map, settings, farm, 1, false));
        StringAssert.Contains("standing", WorldGreatPlague.WhyNotRestrict(map, settings, farm, 1, true));
        farm.influence = 50f;
        Assert.IsNull(WorldGreatPlague.WhyNotRestrict(map, settings, farm, 1, true));

        WorldGreatPlague.Restrict(settings, farm, 3);
        Assert.AreEqual(3 + settings.greatPlague.restrictEchoes, farm.restrictedUntil);
        Assert.AreEqual(50f - settings.greatPlague.restrictStandingCost, farm.influence, 1e-4);
        StringAssert.Contains("already restricted", WorldGreatPlague.WhyNotRestrict(map, settings, farm, 4, true));
        Assert.AreEqual(0f, WorldGreatPlague.Echo(map, settings, 0, 4, new PlagueInputs()).traded, "closed farms send nothing");
        Assert.AreEqual(settings.greatPlague.tradeGroups, WorldGreatPlague.Echo(map, settings, 0, farm.restrictedUntil, new PlagueInputs()).traded, 1e-4, "and trade again once the Echoes are up");

        farm.suzerain = true;
        farm.influence = 0f;
        Assert.IsNull(WorldGreatPlague.WhyNotRestrict(map, settings, farm, farm.restrictedUntil, true), "a suzerain always agrees");
        WorldGreatPlague.Restrict(settings, farm, farm.restrictedUntil);
        Assert.AreEqual(0f, farm.influence, "and spends no standing");
    }

    [Test]
    public void Echo_WhileThePlagueRunsTheMothsBreedOnTheSick()
    {
        var settings = Settings();
        var map = Bare(settings);
        int slot = MothSlots(map, settings)[0];
        map.Settlements.Add(new Settlement { id = 1, name = "Here", coord = CellOf(map, settings, slot).coord });
        var moths = new Population { slot = slot, species = "moth", groups = 2f, capacity = 4f };
        map.Populations.Add(moths);
        Assert.AreEqual(0f, WorldGreatPlague.Echo(map, settings, 0, 1, new PlagueInputs { running = false, burden = 0.8f }).bred, "no plague, no feast");
        Assert.AreEqual(0f, WorldGreatPlague.Echo(map, settings, 0, 2, new PlagueInputs { running = true, burden = 0f }).bred, "no sick, no feast");
        var echo = WorldGreatPlague.Echo(map, settings, 0, 3, new PlagueInputs { running = true, burden = 0.5f });
        Assert.AreEqual(2f * settings.greatPlague.plagueBreeding * 0.5f, echo.bred, 1e-4);
        Assert.AreEqual(2f + echo.bred, moths.groups, 1e-4);
    }
}
