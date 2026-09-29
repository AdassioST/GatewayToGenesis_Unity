using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The full creature schema (vault: Arcanorian Ecology.md, "The Nature of a Species") with no scene: Pure Light beings, harmonic niches, Vibrational
/// Fallout, commensal species, breeding Echoes, disease vectors feeding Disease Burden, resonance plagues, discovery
/// rewards, and the real bestiary's shape (AECOR's diet budget). Uses the catalog of <see cref="EcologyTests"/>.
/// </summary>
public class CreatureSchemaTests
{
    [Test]
    public void UnmergedSprites_RequireBothCoherenceAndARefuge()
    {
        var sprite = new SpeciesSpec { unmerged = true, structure = 0.1f };
        var tile = new WorldTile { coherence = 1f, leylines = 1 };
        var ecology = new EcologySettings();
        Assert.AreEqual(0f, WorldEcology.Quality(ecology, sprite, tile, 0f));
        tile.junction = 2;
        Assert.Greater(WorldEcology.Quality(ecology, sprite, tile, 0f), 0f);
        tile.coherence = 0.79f;
        Assert.AreEqual(0f, WorldEcology.Quality(ecology, sprite, tile, 0f));
        tile.coherence = 0.9f;
        tile.junction = 0;
        tile.sacred = true;
        Assert.Greater(WorldEcology.Quality(ecology, sprite, tile, 0f), 0f);
    }

    [Test]
    public void SlimeOffspring_NeedRenewalParentsAndAgromagicalContact()
    {
        var settings = EcologyTests.Settings();
        settings.species.Add(new SpeciesSpec { id = "slime", entrainedFrom = "deer", structure = 0.9f });
        settings.resourceSites.Add(new ResourceSiteSpec { id = "slime-den", species = "slime", kind = ResourceKind.Fauna, count = 0, minAge = 1 });
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var parent = map.Populations.First(p => p.species == "deer");
        var den = map.ResourceSites.First(d => d.spec == "herd" && map[d.center].habitatSlot == parent.slot);
        var pressure = WorldEcology.Pressure(map, settings.ecology);
        WorldEcology.Entrain(map, settings, 1, pressure);
        Assert.IsFalse(map.Populations.Any(p => p.species == "slime"), "no growers, no entrainment");
        map.Enclaves.Add(new Enclave { family = EnclaveFamily.Agromagical, coord = map[den.center].coord });
        WorldEcology.Entrain(map, settings, 0, pressure);
        Assert.IsFalse(map.Populations.Any(p => p.species == "slime"), "never before Renewal");
        float before = parent.groups;
        WorldEcology.Entrain(map, settings, 1, pressure);
        var child = map.Populations.Single(p => p.species == "slime" && p.slot == parent.slot);
        Assert.Greater(child.groups, 0f);
        Assert.AreEqual(before, parent.groups + child.groups, 0.001f);
        Assert.Greater(parent.groups, child.groups, "parents retain their original form");
    }

    [TestCase(1234)]
    [TestCase(42)]
    [TestCase(777)]
    public void SpriteDiscoveries_ArePresentAtWorldStartAndRemainRare(int seed)
    {
        GameCatalog.InvalidateAll();
        var gen = GameCatalog.World.All.First().generation;
        var map = WorldGenerator.Generate(seed, gen, WorldSystem.LoadStencil(gen), WorldSystem.LoadTiles(gen));
        var rare = map.ResourceSites.Where(s => s.spec == "elemental-sprite-refuge").ToList();
        Assert.AreEqual(1, rare.Count, "one rare discovery on this world");
        Assert.IsTrue(WorldResources.CoherentRefuge(map[rare[0].center]));
        Assert.LessOrEqual(map.StepsFromCapital(map[rare[0].center].coord), 30, "reachable in early exploration");
        Assert.IsFalse(map.ResourceSites.Any(s => s.spec.EndsWith("slime-haven")));
        Assert.Greater(map.ResourceSites.Count(s => s.spec.EndsWith("sprite-haven")), rare.Count);
        for (int echo = 1; echo <= 4; echo++) WorldEcology.Tick(map, gen, 0, echo);
        Assert.AreEqual(1, map.ResourceSites.Count(s => s.spec == "elemental-sprite-refuge"));
        Assert.Less(SpeciesKnowledge.LevelOf(map, gen, "elemental-sprite"), SpeciesLevel.Identified, "the name requires a survey");
        UnityEngine.Debug.Log($"Sprite refuge seed {seed}: {map.StepsFromCapital(map[rare[0].center].coord)} steps from capital");
    }

    private static SpeciesSpec Species(float structure, BindingOrgan organ = BindingOrgan.None, HarmonicNiche niche = HarmonicNiche.Coherent) =>
        new SpeciesSpec { id = "x", name = "X", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile, structure = structure, binding = organ, niche = niche };

    [Test]
    public void PureLightBeings_UnderSixtyFivePercentStructureOrWithAnOrgan()
    {
        Assert.IsTrue(CreatureTaxonomy.IsPureLightBeing(Species(0.45f)), "a Lanternback at 45% Structure");
        Assert.IsFalse(CreatureTaxonomy.IsPureLightBeing(Species(0.65f)), "65% is the line (Pure Light.md)");
        Assert.IsTrue(CreatureTaxonomy.IsPureLightBeing(Species(0.68f, BindingOrgan.Wings)), "a dedicated Coherence-Binding organ makes one whatever its Structure");
        Assert.IsFalse(CreatureTaxonomy.IsPureLightBeing(Species(0.95f)), "a roach is not");
        Assert.IsNull(CreatureTaxonomy.BindingWords(BindingOrgan.None));
        Assert.IsNull(CreatureTaxonomy.NicheWords(HarmonicNiche.Coherent));
    }

    [Test]
    public void Quality_TheNicheSaysWherePureLightFindsWhatItNeeds()
    {
        var e = new EcologySettings();
        var torn = new WorldTile { coherence = 0.1f, dissonance = 0.6f };
        Assert.AreEqual(0f, WorldEcology.Quality(e, Species(0.3f), torn, 0f), 1e-4, "a 70% Pure Light lineage needs 0.42 Coherence");
        Assert.AreEqual(1f, WorldEcology.Quality(e, Species(0.3f, niche: HarmonicNiche.Discordant), torn, 0f), 1e-4, "a Discordant one feeds on the Dissonance");
        var wild = new WorldTile { coherence = 0.8f };
        Assert.AreEqual(e.offLeyline, WorldEcology.Quality(e, Species(0.3f, niche: HarmonicNiche.Leyline), wild, 0f), 1e-4, "a Leyline lineage fails away from the leylines");
        wild.leylines = 1;
        Assert.AreEqual(1f, WorldEcology.Quality(e, Species(0.3f, niche: HarmonicNiche.Leyline), wild, 0f), 1e-4, "and thrives on one");
        var silverBank = new WorldTile { coherence = 0.8f, silverRiverSteps = 1 };
        Assert.AreEqual(1f, WorldEcology.Quality(e, Species(0.3f, niche: HarmonicNiche.Leyline), silverBank, 0f), 1e-4, "or beside a silver river");
    }

    [Test]
    public void Quality_FalloutWearsPureLightDownAndSparesTheRoach()
    {
        var e = new EcologySettings { falloutHarm = 2f };
        var fallout = new WorldTile { coherence = 0.9f, dissonance = 0.9f, fallout = 0.5f };
        Assert.AreEqual(1f - 0.5f * 0.05f * 2f, WorldEcology.Quality(e, Species(0.95f), fallout, 0f), 1e-4, "a cockroach walks through it (Soliton.md)");
        Assert.AreEqual(1f - 0.5f * 0.7f * 2f, WorldEcology.Quality(e, Species(0.3f), fallout, 0f), 1e-4, "a 70% Pure Light lineage suffers it");
        Assert.AreEqual(1f, WorldEcology.Quality(e, Species(0.3f, niche: HarmonicNiche.Discordant), fallout, 0f), 1e-4, "a Dead-zone lineage does not");
    }

    [Test]
    public void Quality_CommensalSpeciesLiveOffPeople()
    {
        var e = new EcologySettings();
        var rat = Species(0.9f);
        rat.commensal = 0.8f;
        var t = new WorldTile { coherence = 0.5f };
        float wild = WorldEcology.Quality(e, rat, t, 0f), town = WorldEcology.Quality(e, rat, t, e.settlementPressure + e.heldPressure);
        Assert.AreEqual(0.2f, wild, 1e-4, "it hardly lives in the wild");
        Assert.Greater(town, 1f, "a settlement's cell holds more than a wild one");
        Assert.LessOrEqual(town, e.commensalCap + 1e-4f);
        Assert.Less(WorldEcology.Quality(e, Species(0.9f), t, e.settlementPressure), 1f, "while wild creatures are pressed out");
    }

    [Test]
    public void Births_CrowdIntoTheBreedingEchoAndKeepTheCycle()
    {
        var settings = RhythmSettings();
        var cicada = Species(0.4f);
        cicada.breedingEcho = 2;
        float[] births = Enumerable.Range(1, 4).Select(s => WorldRhythm.Births(settings, cicada, s)).ToArray();
        Assert.AreEqual(settings.rhythm.breedingPeak, births[1], 1e-4, "summer's chorus");
        Assert.AreEqual(1f, births.Average(), 1e-4, "a Cycle keeps E2's balance");
        Assert.AreEqual(1f, WorldRhythm.Births(settings, cicada, 0), "no calendar, no season");
        var deer = Species(0.9f);
        Assert.AreEqual(1.6f, WorldRhythm.Births(settings, deer, 1), 1e-4, "without a breeding Echo it follows its diet's rhythm");
        var mole = Species(0.95f);
        mole.diet = CreatureDiet.Detritivore;
        Assert.AreEqual(1.8f, WorldRhythm.Births(settings, mole, 3), 1e-4, "decomposers follow decay");
    }

    private static WorldGenSettings RhythmSettings()
    {
        var s = EcologyTests.Settings();
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Resonance", growth = 1.6f, plagues = 0.6f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Crescendo", growth = 1.2f, plagues = 0.8f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Dissonance", growth = 0.8f, detritivoreGrowth = 1.8f, plagues = 1.8f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Silence", growth = 0.4f, detritivoreGrowth = 0.6f, plagues = 0.8f });
        return s;
    }

    [Test]
    public void Attune_AnOrganMakesAHighStructureBeastAttuneAndDiscordantOnesAttuneOnDissonance()
    {
        var settings = RhythmSettings();
        settings.Species("deer").binding = BindingOrgan.Gland;
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var p = WorldEcology.PopulationAt(map, settings, EcologyTests.DenOf(map, "herd"));
        p.groups = p.capacity * 0.5f;
        map.echo = 1;
        map.ritualSeventh = true;
        Assert.IsTrue(WorldRhythm.Attune(map, settings, 0).Any(c => c.population == p), "a 90% Structure beast with a Coherence-Binding organ attunes");

        settings.Species("deer").structure = 0.3f;
        settings.Species("deer").niche = HarmonicNiche.Discordant;
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0)[p.slot]) { map[i].coherence = 0f; map[i].dissonance = 0.8f; }
        p.groups = p.capacity * 0.5f;
        var change = WorldRhythm.Attune(map, settings, 0).First(c => c.population == p);
        Assert.IsTrue(change.surged, "the torn Loom feeds it");
    }

    // A world with rats (vector, commensal) and a settlement beside one of their nests.
    private static (WorldGenSettings settings, WorldMap map, ResourceSite nest) Rats()
    {
        var settings = EcologyTests.Settings();
        settings.species.Add(new SpeciesSpec { id = "rat", name = "Rat", diet = CreatureDiet.Omnivore, subgroup = CreatureSubgroup.Benign, size = CreatureSize.Small, structure = 0.9f, commensal = 0.8f, vector = 0.7f });
        settings.resourceSites.Add(new ResourceSiteSpec
        {
            id = "nest", name = "nest", kind = ResourceKind.Fauna, species = "rat", count = 2, size = 1, spacing = 6, minDistance = 2, landValue = -1f,
            harvest = { new ResourceAmount { resource = "Hides", amount = 1f } },
        });
        var map = EcologyTests.Fresh(42, settings);
        var nest = EcologyTests.DenOf(map, "nest");
        var home = HexCoord.Spiral(map[nest.center].coord, 1).Select(c => map.Get(c)).First(t => t != null && t.resourceSite < 0 && !t.water);
        map.Settlements.Add(new Settlement { id = 1, name = "Hearth", kind = SettlementKind.Capital, coord = home.coord });
        return (settings, map, nest);
    }

    [Test]
    public void Vectors_RatsNearYourSettlementsFeedDiseaseBurdenAndHuntingThemEasesIt()
    {
        var (settings, map, nest) = Rats();
        Assert.AreEqual(0f, WorldEcology.VectorPressure(map, settings, 0), "no creatures yet (before the first Echo)");
        WorldEcology.Tick(map, settings, 0, 1);
        float before = WorldEcology.VectorPressure(map, settings, 0);
        Assert.Greater(before, 0f, "rats in the granaries carry sickness in");
        Assert.LessOrEqual(before, 1f);
        var sources = WorldEcology.VectorSources(map, settings, 0);
        Assert.AreEqual("rat", sources.Single().species.id, "deer, wolves and moles carry nothing");
        float carried = sources.Single().pressure;
        for (int i = 0; i < 6; i++) WorldEcology.Hunt(map, settings, nest, WorldAuthority.Player, 1f);
        Assert.Less(WorldEcology.VectorSources(map, settings, 0).Single().pressure, carried, "culling them near the settlement eases it");
        map.Settlements.Add(new Settlement { id = 2, name = "Far", kind = SettlementKind.Town, coord = map.Tiles.Last(t => !t.water).coord });
        Assert.Less(WorldEcology.VectorSources(map, settings, 0).Single().pressure, carried * 0.9f, "a clean settlement lowers the average");
        map.Settlements.RemoveAt(1);
        map.Settlements.Clear();
        Assert.AreEqual(0f, WorldEcology.VectorPressure(map, settings, 0), "no settlement, no one to sicken");
    }

    // The deer made a 70% Pure Light lineage, with a plague tuned to it, on torn ground.
    private static (WorldGenSettings settings, WorldMap map, Population deer) Plagued(float chance = 1f, float spread = 0f)
    {
        var settings = RhythmSettings();
        settings.Species("deer").structure = 0.3f;
        settings.Species("wolf").prey.Clear();
        settings.plagues.Add(new ResonancePlagueSpec
        {
            id = "rot", name = "Glow Rot", folklore = "the dimming", description = "It drains the light.", hosts = { "deer" },
            dissonanceAt = 0.3f, chance = chance, lethality = 0.5f, duration = 2, spread = spread, immunity = 3,
        });
        var map = EcologyTests.Fresh(42, settings);
        foreach (var t in map.Tiles) { t.coherence = 0.6f; t.dissonance = 0f; }
        WorldEcology.Tick(map, settings, 0, 1);
        var deer = WorldEcology.PopulationAt(map, settings, EcologyTests.DenOf(map, "herd"));
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0)[deer.slot]) map[i].dissonance = 0.5f;
        return (settings, map, deer);
    }

    [Test]
    public void Plagues_BreakOutOnTornGroundKillPassAndLeaveTheSurvivorsImmune()
    {
        var (settings, map, deer) = Plagued();
        float groups = deer.groups;
        var broke = WorldPlagues.Tick(map, settings, 0, 2);
        Assert.IsTrue(broke.Any(c => c.kind == PlagueChangeKind.BrokeOut && c.outbreak.slot == deer.slot), "torn ground, a sure chance");
        Assert.AreEqual(groups, deer.groups, 1e-4, "it kills from the next Echo");
        WorldPlagues.Tick(map, settings, 0, 3);
        Assert.AreEqual(groups * 0.5f, deer.groups, 1e-3);
        var passed = WorldPlagues.Tick(map, settings, 0, 4);
        Assert.AreEqual(groups * 0.25f, deer.groups, 1e-3);
        Assert.IsTrue(passed.Any(c => c.kind == PlagueChangeKind.Passed), "after its two Echoes it passes");
        Assert.IsFalse(WorldPlagues.Tick(map, settings, 0, 5).Any(c => c.kind == PlagueChangeKind.BrokeOut && c.outbreak.slot == deer.slot), "the survivors carry its mark");
        Assert.IsTrue(WorldPlagues.Tick(map, settings, 0, 7).Any(c => c.kind == PlagueChangeKind.BrokeOut && c.outbreak.slot == deer.slot), "and it can return once immunity fades");
    }

    [Test]
    public void Plagues_NeedTornGroundAndSpareOtherSpecies()
    {
        var (settings, map, deer) = Plagued();
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0)[deer.slot]) map[i].dissonance = 0.1f;
        Assert.IsEmpty(WorldPlagues.Tick(map, settings, 0, 2), "a coherent land keeps it dormant");
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0)[deer.slot]) map[i].dissonance = 0.5f;
        var wolves = map.Populations.Where(p => p.species != "deer").ToDictionary(p => p, p => p.groups);
        WorldPlagues.Tick(map, settings, 0, 3);
        WorldPlagues.Tick(map, settings, 0, 4);
        foreach (var kv in wolves) Assert.AreEqual(kv.Value, kv.Key.groups, 1e-4, $"{kv.Key.species} is not a host");
        var den = EcologyTests.DenOf(map, "herd");
        Assert.AreEqual(1, WorldPlagues.At(map, settings, den).Count, "the den's card reads it");
        Assert.AreEqual("the dimming", WorldPlagues.Name(settings.Plague("rot"), false), "folklore first");
        Assert.AreEqual("Glow Rot", WorldPlagues.Name(settings.Plague("rot"), true));
    }

    [Test]
    public void Plagues_SpreadToNeighbouringHostsAndRollTheSameEveryTime()
    {
        string Run()
        {
            var (settings, map, _) = Plagued(chance: 0.5f, spread: 1f);
            foreach (var t in map.Tiles) t.dissonance = 0.5f;
            var log = new List<string>();
            for (int echo = 2; echo <= 12; echo++)
            {
                WorldEcology.Tick(map, settings, 0, echo);
                log.AddRange(WorldPlagues.Tick(map, settings, 0, echo).Select(c => $"{echo}:{c.kind}:{c.outbreak.slot}"));
            }
            return string.Join(",", log);
        }
        string run = Run();
        Assert.AreEqual(run, Run(), "deterministic");
        StringAssert.Contains(PlagueChangeKind.BrokeOut.ToString(), run);
    }

    [Test]
    public void Rewards_EachLevelPaysOnceWithTheSpeciesOwnOnTop()
    {
        var settings = EcologyTests.Settings();
        settings.Species("deer").discovery.Add(new DiscoveryReward { level = SpeciesLevel.Understood, resource = "Research", amount = 30f, eraScore = 2 });
        var state = new SpeciesLoreState();
        state.Ensure("deer").identified = true;
        var tuning = new SpeciesLoreTuning { identifiedEra = 1, observedEra = 0, understoodEra = 1, masteredEra = 1 };
        var level = SpeciesLevel.Identified;
        var paid = SpeciesLore.Rewards(state, settings, tuning, id => level);
        Assert.AreEqual(1, paid.Single().eraScore, "identifying it");
        Assert.IsEmpty(SpeciesLore.Rewards(state, settings, tuning, id => level), "once");
        level = SpeciesLevel.Understood;
        paid = SpeciesLore.Rewards(state, settings, tuning, id => level);
        Assert.AreEqual(1, paid.Count, "Observed pays nothing by default; Understood does");
        Assert.AreEqual(SpeciesLevel.Understood, paid[0].level);
        Assert.AreEqual(3, paid[0].eraScore, "the tuning's 1 and the species' own 2");
        Assert.AreEqual(30f, paid[0].resources.Single().amount);
        level = SpeciesLevel.Observed;
        Assert.IsEmpty(SpeciesLore.Rewards(state, settings, tuning, id => level), "knowledge falling back and rising again pays nothing twice");
        level = SpeciesLevel.Understood;
        Assert.IsEmpty(SpeciesLore.Rewards(state, settings, tuning, id => level));
        Assert.AreEqual(1, SpeciesLore.Rewards(state, settings, tuning, id => (SpeciesLevel)5).Single().eraScore, "Mastered (E7)");
        Assert.IsEmpty(SpeciesLore.Rewards(new SpeciesLoreState { species = { new SpeciesRecord { species = "wolf" } } }, settings, tuning, id => SpeciesLevel.Understood), "never identified, never paid");
    }

    // The real bestiary (World.asset): AECOR's diet budget, every species living somewhere, the vectors and plagues
    // wired, and the plagues tuned to Pure Light beings. Needs the Test Runner (Resources).
    [Test]
    public void Content_TheRealBestiaryKeepsAecorsBudget()
    {
        GameCatalog.InvalidateAll();
        var gen = GameCatalog.World.All.First().generation;
        var species = gen.species.Where(s => s != null && gen.resourceSites.Any(r => r != null && r.species == s.id)).ToList();
        // Environmental ecotypes and their descendants are one lineage, not twenty-one independent diet entries.
        var lineages = species.GroupBy(s => string.IsNullOrEmpty(s.ecotypeFamily) ? s.id : s.ecotypeFamily).Select(g => g.First()).ToList();
        float Share(CreatureDiet diet) => lineages.Count(s => s.diet == diet) / (float)lineages.Count;
        Assert.AreEqual(0.44f, Share(CreatureDiet.Herbivore), 0.04f, "herbivores 44%");
        Assert.AreEqual(0.26f, Share(CreatureDiet.Carnivore), 0.04f, "carnivores 26% (the Trapper blooms among them)");
        Assert.AreEqual(0.18f, Share(CreatureDiet.Omnivore), 0.04f, "omnivores 18%");
        Assert.AreEqual(0.12f, Share(CreatureDiet.Detritivore), 0.04f, "detritivores 12%");
        Assert.IsTrue(species.Any(s => s.diet == CreatureDiet.Detritivore && s.structure >= 0.95f), "the lineage that outlasts every aftermath (Pure Light.md: cockroach-like insects, 95%)");
        Assert.IsTrue(species.Any(s => s.vector > 0f), "something carries sickness to people");
        foreach (var plague in gen.plagues)
            foreach (string host in plague.hosts) Assert.IsTrue(CreatureTaxonomy.IsPureLightBeing(gen.Species(host)), $"{plague.name} is tuned to a Pure Light being");
        Assert.IsNotNull(gen.Plague("dragons-bane"), "canon's Dragon's Bane");
        Assert.IsNotNull(gen.Plague("slime-blight"), "canon's Slime Blight");
    }

    // The real bestiary on a real world (seed 1234) over three Ages of Echoes, with a Capital: the new niches (a
    // Discordant salamander, Leyline moths) still find somewhere to live, the vectors reach the Capital without
    // saturating, and the plagues stay occasional. Needs the Test Runner (Resources).
    [Test]
    public void Content_TheSchemaLivesOnARealWorld()
    {
        GameCatalog.InvalidateAll();
        var gen = GameCatalog.World.All.First().generation;
        var map = WorldGenerator.Generate(1234, gen, WorldSystem.LoadStencil(gen), WorldSystem.LoadTiles(gen));
        map.Settlements.Add(new Settlement { id = 1, name = "Capital", kind = SettlementKind.Capital, coord = map.Capital });
        var plagues = new List<PlagueChange>();
        float vectors = 0f;
        int echo = 0;
        for (int age = 0; age <= 2; age++)
        {
            if (age > 0) WorldSites.PlaceAge(map, gen, age);
            for (int e = 0; e < 8; e++)
            {
                map.echo = echo % TimeSystemLogic.EchoesPerCycle + 1;
                int season = WorldRhythm.Previous(map.echo);
                WorldEcology.Tick(map, gen, age, ++echo, season);
                plagues.AddRange(WorldPlagues.Tick(map, gen, age, echo, season));
                WorldBehavior.Tick(map, gen, age, season);
                vectors = System.Math.Max(vectors, WorldEcology.VectorPressure(map, gen, age));
            }
        }
        var dens = map.ResourceSites.Select(s => WorldEcology.SpeciesAt(gen, s)).Where(s => s != null).Select(s => s.id).Distinct().ToList();
        Assert.IsTrue(dens.Any(id => !string.IsNullOrEmpty(gen.Species(id).entrainedFrom)), "Renewal descendants accumulate enough offspring to establish a visible den");
        foreach (string id in new[] { "ember-salamander", "moonveil-moth", "granary-rat", "hearth-roach", "dawnhorn-behemoth" })
            if (dens.Contains(id)) Assert.IsTrue(map.Populations.Any(p => p.species == id && p.groups > 0f), $"{id} lives somewhere with its dens");
        Assert.Less(vectors, 1f, "the vectors never saturate Disease Burden on their own");
        int outbreaks = plagues.Count(c => c.kind != PlagueChangeKind.Passed);
        Assert.Less(outbreaks, 24, "plagues stay occasional over 24 Echoes");
        string torn = string.Join(", ", gen.plagues.Where(p => p.hosts.Count > 0).Select(p => $"{p.id} " + string.Join("/",
            map.Populations.Where(q => WorldPlagues.IsHost(p, q.species)).Select(q => q.slot).Distinct().Select(s => WorldPlagues.Torn(map, gen, p, s, 2).ToString("0.00")))));
        UnityEngine.Debug.Log($"E10 on seed 1234: torn shares {torn}; dens of {string.Join(", ", dens)}; peak vector pressure {vectors:0.00}; {outbreaks} outbreaks ({string.Join(", ", plagues.Where(c => c.kind != PlagueChangeKind.Passed).Select(c => c.plague.id + "@" + c.outbreak.slot))}); " +
            string.Join(", ", map.Populations.GroupBy(p => p.species).Select(g => $"{g.Key} {g.Count()} ranges {g.Sum(p => p.groups):0.#} groups")));
    }
}
