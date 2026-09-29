using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Enclaves and the creatures, with no scene (<see cref="WorldEnclaveEcology"/>; vault: Arcanorian Ecology.md, "Enclaves and Creatures"): keepers
/// keep what suits them around them and befriend it, Mastered while you hold their Suzerainty, kept hunts harm less, herds,
/// growers tending their land, Auric understanding, and the four commissions with their refusals. Uses the catalog of
/// <see cref="EcologyTests"/>.
/// </summary>
public class EnclaveEcologyTests
{
    private static WorldGenSettings Settings()
    {
        var s = EcologyTests.Settings();
        // Placed by hand (count 0: the generator leaves them out).
        s.enclaves.Add(new EnclaveSpec { id = "keepers", name = "Keepers", family = EnclaveFamily.Domestication, count = 0 });
        s.enclaves.Add(new EnclaveSpec { id = "sprite-keepers", name = "Sprite Keepers", family = EnclaveFamily.Domestication, count = 0, keepsPureLight = 0.5f });
        s.enclaves.Add(new EnclaveSpec { id = "hunters", name = "Hunters", family = EnclaveFamily.Militant, count = 0 });
        s.enclaves.Add(new EnclaveSpec { id = "growers", name = "Growers", family = EnclaveFamily.Agromagical, count = 0 });
        s.enclaves.Add(new EnclaveSpec { id = "scholars", name = "Scholars", family = EnclaveFamily.Auric, count = 0 });
        s.enclaves.Add(new EnclaveSpec { id = "traders", name = "Traders", family = EnclaveFamily.Trading, count = 0 });
        s.enclaveEcology = new EnclaveEcologySettings { herdYields = { new ResourceAmount { resource = "Food", amount = 0.1f } } };
        return s;
    }

    // A living world with the deer's herd identified, and an Enclave of the spec standing on the herd's den.
    private static (WorldMap map, WorldGenSettings settings, ResourceSite den) World()
    {
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = EcologyTests.DenOf(map, "herd");
        map[den.center].explored = true;
        return (map, settings, den);
    }

    private static Enclave Place(WorldMap map, WorldGenSettings settings, string spec, HexCoord at, bool suzerain = false, float influence = 0f)
    {
        var s = settings.Enclave(spec);
        var e = new Enclave { index = map.Enclaves.Count, spec = spec, name = s.name, family = s.family, coord = at, suzerain = suzerain, influence = suzerain ? 100f : influence };
        map.Enclaves.Add(e);
        var t = map.Get(at);
        if (t != null) t.revealed = true;
        return e;
    }

    private static HexCoord At(WorldMap map, ResourceSite den) => map[den.center].coord;

    // The real content (needs Unity's Resources): on a few seeds the Sprite-Light Conclave stands from Age 0 and keeps
    // Pure Light creatures after a few Echoes, and the Aureate Cloister arrives with the Age of Renewal.
    [Test]
    public void Content_TheConclaveKeepsPureLightCreaturesAndTheCloisterArrives()
    {
        GameCatalog.InvalidateAll();
        var world = GameCatalog.World.All.FirstOrDefault();
        Assert.IsNotNull(world, "No WorldSettings in Resources/World");
        var gen = world.generation;
        int keeping = 0;
        var seeds = new[] { 1234, 77, 2026 };
        foreach (int seed in seeds)
        {
            var map = WorldGenerator.Generate(seed, gen, WorldSystem.LoadStencil(gen), WorldSystem.LoadTiles(gen));
            var conclave = map.Enclaves.FirstOrDefault(e => e.family == EnclaveFamily.Domestication);
            Assert.IsNotNull(conclave, $"seed {seed}: no Domestication Enclave at Age 0 ({string.Join("; ", map.Report.notes)})");
            for (int echo = 1; echo <= 4; echo++) WorldEcology.Tick(map, gen, 0, echo);
            if (WorldEnclaveEcology.Kept(map, gen, conclave).Count > 0) keeping++;
            WorldSites.PlaceAge(map, gen, 1);
            Assert.IsTrue(map.Enclaves.Any(e => e.family == EnclaveFamily.Auric), $"seed {seed}: no Auric Enclave in the Age of Renewal");
        }
        Assert.Greater(keeping, 0, "the Conclave kept no creature on any seed: its site or keepsPureLight needs tuning");
    }

    [Test]
    public void Keeps_FollowsItsPureLightShare()
    {
        var settings = Settings();
        Assert.IsTrue(WorldEnclaveEcology.Keeps(settings.Enclave("keepers"), settings.Species("deer")), "a keeper of any creature keeps deer");
        Assert.IsFalse(WorldEnclaveEcology.Keeps(settings.Enclave("sprite-keepers"), settings.Species("deer")), "90% Structure is no Pure Light being");
        Assert.IsTrue(WorldEnclaveEcology.Keeps(settings.Enclave("sprite-keepers"), settings.Species("sprite")), "a 90% Pure Light sprite is");
    }

    [Test]
    public void Kept_TheSpeciesLivingAroundTheKeepers()
    {
        var (map, settings, den) = World();
        var keepers = Place(map, settings, "keepers", At(map, den));
        var picky = Place(map, settings, "sprite-keepers", At(map, den));
        var hunters = Place(map, settings, "hunters", At(map, den));
        CollectionAssert.Contains(WorldEnclaveEcology.Kept(map, settings, keepers), "deer");
        CollectionAssert.IsEmpty(WorldEnclaveEcology.Kept(map, settings, picky), "no Pure Light creature lives here");
        CollectionAssert.IsEmpty(WorldEnclaveEcology.Kept(map, settings, hunters), "only Domestication Enclaves keep creatures");
        Assert.IsFalse(WorldEnclaveEcology.KeptForYou(map, settings, "deer"), "kept, but not for you");
        keepers.suzerain = true;
        Assert.IsTrue(WorldEnclaveEcology.KeptForYou(map, settings, "deer"));
    }

    [Test]
    public void Echo_KeepersBefriendWhatTheyKeepAndShareItWithTheirSuzerain()
    {
        var (map, settings, den) = World();
        var keepers = Place(map, settings, "keepers", At(map, den));
        var first = WorldEnclaveEcology.Echo(map, settings);
        Assert.IsTrue(first.Any(c => c.kind == EnclaveEcologyChangeKind.Kept && c.species.id == "deer" && c.enclave == keepers));
        Assert.AreEqual(settings.enclaveEcology.keeperBefriend, WorldBehavior.Of(map, "deer").Bond(keepers.AuthorityId).temper, 0.001f, "gentle with its keepers");
        Assert.IsFalse(WorldBehavior.Met(map, "deer", WorldAuthority.Player), "nothing is shared with a stranger");
        Assert.IsFalse(WorldEnclaveEcology.Echo(map, settings).Any(c => c.species.id == "deer"), "kept already: told once");
        keepers.suzerain = true;
        WorldEnclaveEcology.Echo(map, settings);
        float yours = WorldBehavior.Temper(map, "deer", WorldAuthority.Player);
        WorldEnclaveEcology.Echo(map, settings);
        Assert.AreEqual(yours + settings.enclaveEcology.sharedBefriend, WorldBehavior.Temper(map, "deer", WorldAuthority.Player), 0.001f, "their suzerain is befriended too");
        // The herd gone: released.
        foreach (var p in map.Populations.Where(p => p.species == "deer")) p.groups = 0f;
        Assert.IsTrue(WorldEnclaveEcology.Echo(map, settings).Any(c => c.kind == EnclaveEcologyChangeKind.Released && c.species.id == "deer"));
    }

    [Test]
    public void Mastered_WhileYourSuzerainKeepersKeepWhatYouKnow()
    {
        var (map, settings, den) = World();
        var keepers = Place(map, settings, "keepers", At(map, den));
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "deer"));
        keepers.suzerain = true;
        Assert.AreEqual(SpeciesLevel.Mastered, SpeciesKnowledge.LevelOf(map, settings, "deer"));
        Assert.AreEqual(SpeciesLevel.Mastered, SpeciesKnowledge.Levels(map, settings)["deer"]);
        Assert.AreEqual(SpeciesLevel.Mastered, SpeciesKnowledge.Identified(map, settings).First(k => k.species.id == "deer").level);
        foreach (var site in map.ResourceSites.Where(s => s.spec == "herd")) foreach (int c in site.cells) map[c].explored = false;
        Assert.Less(SpeciesKnowledge.LevelOf(map, settings, "deer"), SpeciesLevel.Identified, "a species you do not know is never Mastered");
        Assert.IsNull(SpeciesKnowledge.GoalProblem(SpeciesKnowledge.SpeciesKnownDomain, "deer", 5f, new[] { "deer" }, new string[0]));
        Assert.IsNotNull(SpeciesKnowledge.GoalProblem(SpeciesKnowledge.SpeciesKnownDomain, "deer", 6f, new[] { "deer" }, new string[0]));
    }

    [Test]
    public void Hunting_ASpeciesYourKeepersKeepHarmsItLess()
    {
        var (map, settings, den) = World();
        WorldBehavior.Hunted(map, settings, "deer", WorldAuthority.Player, 1f);
        float wild = WorldBehavior.Temper(map, "deer", WorldAuthority.Player);
        var (kept, keptSettings, keptDen) = World();
        Place(kept, keptSettings, "keepers", At(kept, keptDen), suzerain: true);
        WorldBehavior.Hunted(kept, keptSettings, "deer", WorldAuthority.Player, 1f);
        Assert.AreEqual(wild * keptSettings.enclaveEcology.keptHuntHarm, WorldBehavior.Temper(kept, "deer", WorldAuthority.Player), 0.0001f);
        Assert.AreEqual(1, WorldBehavior.Of(kept, "deer").Bond(WorldAuthority.Player).hunts, "still a hunt");
    }

    [Test]
    public void Growers_TendTheLandAroundThemAndItsCreaturesRecover()
    {
        var (map, settings, den) = World();
        Place(map, settings, "growers", At(map, den));
        var p = WorldEcology.PopulationAt(map, settings, den);
        p.groups = p.capacity * 0.2f;
        WorldEnclaveEcology.Echo(map, settings);
        Assert.AreEqual(p.capacity * (0.2f + 0.8f * settings.enclaveEcology.restorePerEcho), p.groups, 0.001f);
    }

    [Test]
    public void Herds_OnlyForTheirSuzerainAndByTheirNumbers()
    {
        var (map, settings, den) = World();
        var keepers = Place(map, settings, "keepers", At(map, den));
        CollectionAssert.IsEmpty(WorldEnclaveEcology.Yields(map, settings));
        keepers.suzerain = true;
        var full = WorldEnclaveEcology.Yields(map, settings);
        Assert.AreEqual(1, full.Count);
        Assert.AreEqual("Herds: Keepers", full[0].source, "names the keepers, never the creatures");
        foreach (var p in map.Populations) p.groups *= 0.5f;
        Assert.Less(WorldEnclaveEcology.Yields(map, settings)[0].amount, full[0].amount, "fewer creatures, smaller herds");
        Assert.IsTrue(WorldCivilization.Yields(map, settings, new SettlementRules()).Any(y => y.source == "Herds: Keepers"), "the civilization's yields carry them");
    }

    [Test]
    public void Auric_TheirSuzerainUnderstandsAndTheirStudyMakesAnyKnownSpeciesUnderstood()
    {
        var (map, settings, den) = World();
        var scholars = Place(map, settings, "scholars", At(map, den));
        Assert.IsFalse(WorldEnclaveEcology.AuricUnderstanding(map));
        scholars.suzerain = true;
        Assert.IsTrue(WorldEnclaveEcology.AuricUnderstanding(map));
        var lore = new SpeciesLoreState();
        SpeciesLore.Refresh(lore, map, settings, new SpeciesLoreTuning());
        var view = new LoreView { state = lore, tuning = new SpeciesLoreTuning(), understands = false };
        Assert.AreEqual(SpeciesLevel.Identified, SpeciesKnowledge.LevelOf(map, settings, "deer", view));
        Assert.IsNull(WorldEnclaveEcology.WhyNotCommission(map, settings, scholars, den, 0, view));
        WorldEnclaveEcology.Commission(map, settings, scholars, den, 0, lore);
        Assert.AreEqual(SpeciesLevel.Understood, SpeciesKnowledge.LevelOf(map, settings, "deer", view), "studied: understood without the research");
        Assert.IsNotNull(WorldEnclaveEcology.WhyNotCommission(map, settings, scholars, den, 1, view), "already understood");
    }

    [Test]
    public void Commission_RefusedUntilTrustedNearAndOncePerPhase()
    {
        var (map, settings, den) = World();
        var hunters = Place(map, settings, "hunters", At(map, den), influence: 40f);
        var traders = Place(map, settings, "traders", At(map, den), suzerain: true);
        Assert.IsNotNull(WorldEnclaveEcology.WhyNotCommission(map, settings, traders, den, 0), "a Trading Enclave does not tend creatures");
        StringAssert.Contains("standing", WorldEnclaveEcology.WhyNotCommission(map, settings, hunters, den, 0));
        hunters.influence = 60f;
        Assert.IsNull(WorldEnclaveEcology.WhyNotCommission(map, settings, hunters, den, 0));
        map[den.center].explored = false;
        foreach (int c in den.cells) map[c].explored = false;
        Assert.IsNotNull(WorldEnclaveEcology.WhyNotCommission(map, settings, hunters, den, 0), "identify them first");
        map[den.center].explored = true;
        var far = map.Tiles.First(t => HexCoord.Distance(t.coord, At(map, den)) > settings.enclaveEcology.serviceReach);
        var away = Place(map, settings, "hunters", far.coord, influence: 60f);
        StringAssert.Contains("Too far", WorldEnclaveEcology.WhyNotCommission(map, settings, away, den, 0));
        WorldEnclaveEcology.Commission(map, settings, hunters, den, 3);
        Assert.AreEqual(60f - settings.enclaveEcology.standingCost, hunters.influence, 0.001f, "a favour spends standing");
        Assert.IsNotNull(WorldEnclaveEcology.WhyNotCommission(map, settings, hunters, den, 3), "once a Phase");
        Assert.IsNull(WorldEnclaveEcology.WhyNotCommission(map, settings, hunters, den, 4));
    }

    [Test]
    public void Commission_CullThinsThemAndTheyRememberWhoSentTheHunters()
    {
        var (map, settings, den) = World();
        var hunters = Place(map, settings, "hunters", At(map, den), suzerain: true);
        var p = WorldEcology.PopulationAt(map, settings, den);
        float before = p.groups, take = System.Math.Min(p.groups, settings.enclaveEcology.cullTake * p.capacity);
        WorldEnclaveEcology.Commission(map, settings, hunters, den, 0);
        Assert.AreEqual(before - take, p.groups, 0.001f);
        Assert.AreEqual(100f, hunters.influence, "a suzerain's standing is not spent");
        Assert.Less(WorldBehavior.Temper(map, "deer", WorldAuthority.Player), 0f, "you sent them");
        Assert.Less(WorldBehavior.Temper(map, "deer", hunters.AuthorityId), 0f, "and they did it");
    }

    [Test]
    public void Commission_TamingAndRestoring()
    {
        var (map, settings, den) = World();
        var keepers = Place(map, settings, "keepers", At(map, den), influence: 80f);
        WorldEnclaveEcology.Commission(map, settings, keepers, den, 0);
        Assert.Greater(WorldBehavior.Temper(map, "deer", WorldAuthority.Player), settings.enclaveEcology.tameAmount - 0.001f);
        var growers = Place(map, settings, "growers", At(map, den), influence: 80f);
        var p = WorldEcology.PopulationAt(map, settings, den);
        p.groups = 0.2f * p.capacity;
        Assert.IsNull(WorldEnclaveEcology.WhyNotCommission(map, settings, growers, den, 0));
        WorldEnclaveEcology.Commission(map, settings, growers, den, 0);
        Assert.AreEqual(p.capacity * (0.2f + 0.8f * settings.enclaveEcology.restoreShare), p.groups, 0.001f);
        p.groups = p.capacity;
        Assert.IsNotNull(WorldEnclaveEcology.WhyNotCommission(map, settings, growers, den, 1), "already as many as the land holds");
    }
}
