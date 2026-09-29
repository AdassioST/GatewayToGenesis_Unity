using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Eleos Blooms with no scene (<see cref="WorldResources"/>, kind Bloom): Emotional Residue, vigor and withering,
/// listeners drinking Dissonance, predators casting danger and luring expeditions, healers easing the road, and Lumen
/// Seeds that only take where there is feeling. Uses the small catalog of <see cref="WorldGenerationTests"/> plus three
/// blooms of its own.
/// </summary>
public class EleosBloomTests
{
    private static WorldGenSettings Settings(float need = 0.3f)
    {
        var s = WorldGenerationTests.Settings();
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "bells", name = "Bells", kind = ResourceKind.Bloom, niche = BloomNiche.Listener, resource = "Eleos Tea", count = 3, size = 1, spacing = 4, minDistance = 2,
            terrains = { "plain", "steppe", "wood", "glade", "ash", "ruin" }, landValue = 2f, residueNeed = need, dissonanceAura = -0.1f, auraRadius = 2, minimumDissonance = 0.03f,
            yields = { new ResourceAmount { resource = "Eleos Tea", amount = 0.02f } },
            harvest = { new ResourceAmount { resource = "Eleos Tea", amount = 10f } }, seeds = true, plantFertility = 0f,
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "candle", name = "Candle", kind = ResourceKind.Bloom, niche = BloomNiche.Healer, count = 2, size = 1, spacing = 4, minDistance = 2,
            landValue = 3f, residueNeed = need, soothe = 3f, untouchable = "Leave it be.",
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "maw", name = "Maw", kind = ResourceKind.Bloom, niche = BloomNiche.Predator, count = 2, size = 1, spacing = 4, minDistance = 3,
            landValue = -2f, residueNeed = need, dangerAura = 0.6f, auraRadius = 1, untouchable = "Nothing comes back out.",
        });
        return s;
    }

    private static WorldMap Fresh(int seed, WorldGenSettings settings) =>
        WorldGenerator.Generate(seed, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    private static ResourceSite FirstOf(WorldMap map, string spec) => map.ResourceSites.First(s => s.spec == spec && !s.planted);

    private static void Identify(WorldMap map, ResourceSite site)
    {
        foreach (int c in site.cells) map[c].known = map[c].explored = map[c].revealed = true;
    }

    // ===== EMOTIONAL RESIDUE =====

    [Test]
    public void Residue_PeopleRuinsAndDissonanceFeedIt()
    {
        var settings = Settings();
        var map = Fresh(7, settings);
        var eleos = settings.eleos;
        var capital = map[map.Get(map.Capital).index];
        Assert.GreaterOrEqual(capital.residue, eleos.ambientResidue + eleos.capitalResidue - 1e-4f, "the Capital's people leave feeling in the land");
        var far = map.Tiles.Where(t => !t.water && map.StepsFromCapital(t.coord) > eleos.settlementReach + 2 && WorldResources.RawDissonance(map, t) == 0f).ToList();
        Assert.IsNotEmpty(far);
        foreach (var t in far.Take(20)) Assert.AreEqual(eleos.ambientResidue, t.residue, 1e-4f, "far from anyone, only the wild's own");

        var calm = far[0];
        map.Ruins.Add(new Ruin { id = 1, name = "Old Hall", kind = SettlementKind.Town, coord = calm.coord });
        WorldResources.Residue(map, eleos);
        Assert.AreEqual(eleos.ambientResidue + eleos.ruinResidue, calm.residue, 1e-4f, "the ruins of the fallen hold grief");

        var dissonant = map.Tiles.Where(t => !t.water && WorldResources.RawDissonance(map, t) > 0.05f && map.StepsFromCapital(t.coord) > eleos.settlementReach + 2).ToList();
        Assert.IsNotEmpty(dissonant, "the test world has dissonant ground");
        foreach (var t in dissonant.Take(10)) Assert.Greater(t.residue, eleos.ambientResidue, "Dissonance is feeling left unmetabolized");
    }

    // ===== VIGOR =====

    [Test]
    public void Vigor_AStarvedBloomWithersAndGivesNothing()
    {
        var starved = Settings(need: 1f);
        starved.eleos.ambientResidue = 0f;
        starved.eleos.dissonanceResidue = 0f;
        var map = Fresh(7, starved);
        var bells = map.ResourceSites.Where(s => s.spec == "bells").OrderByDescending(s => map.StepsFromCapital(map[s.center].coord)).First();
        Identify(map, bells);
        WorldResources.Refresh(map, starved);
        var t = map[bells.center];
        if (t.residue < starved.eleos.witherBelow)
        {
            Assert.IsTrue(WorldResources.Withering(bells, starved), "with no feeling a bloom can neither move nor shine");
            Assert.AreEqual(0f, bells.landValue, "a withering bloom adds nothing to the land");
            Assert.IsEmpty(WorldResources.YieldsAt(map, starved, t).Where(y => y.amount > 0f), "nor yields");
            Assert.AreEqual(10f * starved.eleos.witheredHarvest, WorldResources.HarvestAt(map, starved, t, 1f).Single().amount, 1e-4f, "only its fallen leaves");
        }

        var fed = Settings(need: 0.1f);
        var lush = Fresh(7, fed);
        var thriving = FirstOf(lush, "bells");
        Identify(lush, thriving);
        WorldResources.Refresh(lush, fed);
        Assert.AreEqual(1f, thriving.vigor, 1e-4f, "the wild's own feeling is enough for a bloom that needs little");
        Assert.AreEqual(2f, thriving.landValue, 1e-4f);
        Assert.AreEqual(10f, WorldResources.HarvestAt(lush, fed, lush[thriving.center], 1f).Single().amount, 1e-4f);
    }

    [Test]
    public void Vigor_NonBloomsAreUntouched()
    {
        var site = new ResourceSite { spec = "x", kind = ResourceKind.Crop, vigor = 0f };
        Assert.IsFalse(WorldResources.Withering(site, null));
        Assert.AreEqual(1f, WorldResources.Gift(site, null));
        Assert.AreEqual(1f, WorldResources.HarvestShare(site, null));
    }

    // ===== WHAT BLOOMS DO =====

    [Test]
    public void Listeners_DrinkDissonanceAroundThem()
    {
        var settings = Settings(need: 0.05f);
        var map = Fresh(42, settings);
        var drunk = map.Tiles.Where(t => t.siteDissonance < -0.001f && WorldResources.RawDissonance(map, t) > 0.02f).ToList();
        Assert.IsNotEmpty(drunk, "some listener reaches dissonant ground");
        foreach (var t in drunk) Assert.Less(t.dissonance, WorldResources.RawDissonance(map, t), "the magic hears it: Dissonance falls");
        foreach (var t in map.Tiles) Assert.GreaterOrEqual(t.dissonance, 0f, "never below none");

        var before = map.Tiles.Select(t => t.dissonance).ToArray();
        WorldResources.Refresh(map, settings);
        WorldResources.Refresh(map, settings);
        for (int i = 0; i < map.Count; i++) Assert.AreEqual(before[i], map[i].dissonance, 1e-5f, "refreshing again changes nothing");
    }

    [Test]
    public void Predators_CastDangerAndLureExpeditions()
    {
        var settings = Settings(need: 0.05f);
        var map = Fresh(7, settings);
        var maw = FirstOf(map, "maw");
        var t = map[maw.center];
        Assert.Greater(t.lure, 0.9f, "its own cell is the heart of the lure");
        if (!t.sacred) Assert.GreaterOrEqual(t.danger, 0.6f * maw.vigor - 1e-4f, "its danger is the world's danger");
        foreach (var n in map.NeighboursOf(t).Where(n => !n.water)) Assert.Greater(n.lure, 0f, "and it reaches its neighbours");

        var x = new ExpeditionSettings();
        var unit = new WorldUnit { id = 1, leader = "Vaelia", companions = new List<string>() };
        var party = new List<PartyMember> { new PartyMember { name = "Vaelia", director = true } };
        var at = new UnitSurroundings { weather = 1f, lure = 1f };
        var calm = new UnitSurroundings { weather = 1f };
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Lure, unit, at, party, x), "a party beside a predator can be lured");
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Lure, unit, calm, party, x), "and nowhere else");
        Assert.Greater(Expeditions.MishapRisk(unit, at, party, x), Expeditions.MishapRisk(unit, calm, party, x), "a lure courts mishaps");
        Assert.IsTrue(Expeditions.DefaultMishaps.Any(m => m.kind == MishapKind.Lure));
        var mishap = new Mishap { spec = Expeditions.DefaultMishaps.First(m => m.kind == MishapKind.Lure), target = "Vaelia" };
        StringAssert.Contains("Eleos Bloom", Expeditions.Describe(mishap, "The Wayfarers", "the ford").text);
    }

    [Test]
    public void Predators_WitheredCastNothing()
    {
        var settings = Settings(need: 1f);
        settings.eleos.ambientResidue = settings.eleos.dissonanceResidue = 0f;
        var map = Fresh(7, settings);
        foreach (var maw in map.ResourceSites.Where(s => s.spec == "maw" && WorldResources.Withering(s, settings)))
        {
            Assert.AreEqual(0f, map[maw.center].lure);
            Assert.AreEqual(0f, map[maw.center].siteDanger);
        }
    }

    [Test]
    public void Healers_EaseTheRoadAndShelterACamp()
    {
        var settings = Settings(need: 0.05f);
        var map = Fresh(7, settings);
        var candle = FirstOf(map, "candle");
        Assert.AreEqual(3f, map[candle.center].sanctuary, 1e-4f, "its own cell is a sanctuary");

        var x = new ExpeditionSettings { shapes = new List<PartyShape> { new PartyShape { name = "Neutral" } } };
        var unit = new WorldUnit { id = 1, leader = "Vaelia", companions = new List<string>(), attrition = 100f };
        var wild = new UnitSurroundings { weather = 1f };
        var sheltered = new UnitSurroundings { weather = 1f, sanctuary = 1f };
        float h = Expeditions.Hardship(unit, wild, false, null, x);
        Assert.Greater(h, 1f);
        Assert.AreEqual(h - 1f, Expeditions.Hardship(unit, sheltered, false, null, x), 1e-4f, "the bloom's light takes some of the weight");
        Assert.AreEqual(0f, Expeditions.Hardship(new WorldUnit { leader = "Vaelia", companions = new List<string>() }, sheltered, false, null, x), "never below none");
    }

    // ===== STERILE BLOOMS THAT GROW BY THEMSELVES =====


    // Fated Flowers on historic ground that decay into Forsaken Flowers where sorrow gathers (faster with Dissonance) and
    // heal back where Coherence is high; Glimmerfern around the Forsaken or on hurtful ground, along silver rivers and
    // around the lakes they run into.
    private static WorldGenSettings Sterile()
    {
        var s = WorldGenerationTests.Settings();
        s.eleos.historicTerrains = new List<string> { "ruin", "ash", "scar" };
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "fated", name = "Fated", kind = ResourceKind.Bloom, count = 4, size = 2, spacing = 4, minDistance = 2, residueNeed = 0f,
            sprouts = true, sproutPerEcho = 2, minimumHistory = 0.3f, maximumHurt = 0.25f, untouchable = "No.",
            turnsInto = "forsaken", turnHurt = 0.3f, holdCoherence = 0.75f, turnPerEcho = 0.35f, turnPerDissonance = 2.5f,
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "forsaken", name = "Forsaken", kind = ResourceKind.Bloom, count = 2, size = 2, spacing = 4, minDistance = 2, residueNeed = 0f,
            sprouts = true, minimumHistory = 0.3f, minimumHurt = 0.3f, untouchable = "No.",
            turnsInto = "fated", turnCoherence = 0.7f, turnPerEcho = 0.5f, turnPerDissonance = -1.5f,
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "fern", name = "Fern", kind = ResourceKind.Bloom, niche = BloomNiche.Healer, soothe = 1f, count = 6, size = 1, spacing = 3, minDistance = 2,
            residueNeed = 0.1f, sprouts = true, minimumHurt = 0.3f, nearSites = { "forsaken" }, nearReach = 2, fadePerEcho = 0.5f,
            harvest = { new ResourceAmount { resource = "Glimmerfern", amount = 5f } },
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "moon", name = "Moon", kind = ResourceKind.Bloom, niche = BloomNiche.Healer, soothe = 1f, count = 2, size = 2, spacing = 3, minDistance = 2,
            sprouts = true, sproutPerEcho = 2, silverRiverReach = 1, fadePerEcho = 0.5f, harvest = { new ResourceAmount { resource = "Glimmerfern", amount = 5f } },
        });
        s.resourceSites.Add(new ResourceSiteSpec
        {
            id = "shore", name = "Shore", kind = ResourceKind.Bloom, niche = BloomNiche.Healer, soothe = 1f, count = 1, size = 12, spacing = 8, minDistance = 1,
            sprouts = true, silverLakeReach = 2, fadePerEcho = 0.5f, harvest = { new ResourceAmount { resource = "Glimmerfern", amount = 5f } },
        });
        return s;
    }

    [Test]
    public void History_RuinsLandmarksAndHistoricGroundHoldIt()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        var eleos = settings.eleos;
        var calm = map.Tiles.First(t => !t.water && !t.HasFeature && !t.sacred && !t.oldRoad && t.history == 0f && t.hurt == 0f && map.StepsFromCapital(t.coord) > 6);
        map.Ruins.Add(new Ruin { id = 1, name = "Old Hall", kind = SettlementKind.Town, coord = calm.coord });
        WorldResources.Residue(map, eleos);
        Assert.AreEqual(eleos.ruinHistory, calm.history, 1e-4f, "a ruin is history");
        Assert.AreEqual(eleos.ruinResidue, calm.hurt, 1e-4f, "and its grief is hurtful");
        foreach (var t in map.Tiles.Where(t => t.HasFeature)) Assert.GreaterOrEqual(t.history, eleos.featureHistory - 1e-4f, "a landmark remembers");
        foreach (var t in map.Tiles.Where(t => t.terrain == "ruin")) Assert.GreaterOrEqual(t.history, eleos.historicTerrainHistory - 1e-4f, "so does ground that is history itself");
        var capital = map.Get(map.Capital);
        Assert.Less(capital.hurt, capital.residue, "a calm Capital's people leave feeling, not sorrow");
    }

    [Test]
    public void Sprouting_SterileBloomsGrowByThemselvesEchoByEcho()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        Assert.IsFalse(map.ResourceSites.Any(s => s.spec == "fated"), "they are not placed with an Age");
        var changes = WorldResources.GrowEcho(map, settings, 0, 0, initial: true);
        var fated = map.ResourceSites.Where(s => s.spec == "fated").ToList();
        Assert.Greater(fated.Count, 2, "the world starts with its fields (up to count, past the per-Echo pace)");
        Assert.LessOrEqual(fated.Count, 4);
        Assert.IsTrue(changes.Any(c => c.kind == GrowthKind.Sprouted && c.site.spec == "fated"));
        foreach (var site in fated) Assert.GreaterOrEqual(map[site.center].history, 0.3f, "only where the land remembers");

        map.ResourceSites.RemoveAll(s => s.spec == "fated");
        WorldResources.Mark(map);
        WorldResources.GrowEcho(map, settings, 1, 0);
        Assert.LessOrEqual(map.ResourceSites.Count(s => s.spec == "fated"), 2, "an Echo brings sproutPerEcho at most");
        for (int echo = 2; echo <= 5; echo++) WorldResources.GrowEcho(map, settings, echo, 0);
        Assert.LessOrEqual(map.ResourceSites.Count(s => s.spec == "fated"), 4, "no more than count stand");

        var again = Fresh(7, settings);
        WorldResources.GrowEcho(again, settings, 0, 0, initial: true);
        CollectionAssert.AreEqual(fated.Select(s => s.center), again.ResourceSites.Where(s => s.spec == "fated").Select(s => s.center), "the same world grows the same way");
    }

    [Test]
    public void Turning_FatedDecayOverEchoesAndDissonanceHastensIt()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        WorldResources.GrowEcho(map, settings, 0, 0, initial: true);
        var site = map.ResourceSites.First(s => s.spec == "fated");
        WorldResources.GrowEcho(map, settings, 1, 0);
        Assert.AreEqual("fated", site.spec, "calm ground keeps them golden");
        Assert.AreEqual(0f, site.turning);

        // A settlement falls beside them: its grief reaches the field, and the decay runs an Echo at a time.
        map.Ruins.Add(new Ruin { id = 9, name = "Fallen", kind = SettlementKind.Town, coord = map[site.center].coord });
        foreach (int c in site.cells) { map[c].dissonance = 0f; map[c].coherence = 0.5f; }
        WorldResources.GrowEcho(map, settings, 2, 0);
        Assert.AreEqual("fated", site.spec, "one Echo is not enough without Dissonance");
        Assert.AreEqual(0.35f, site.turning, 1e-4f);
        List<GrowthChange> changes = null;
        for (int echo = 3; echo <= 5 && site.spec == "fated"; echo++)
        {
            foreach (int c in site.cells) { map[c].dissonance = 0f; map[c].coherence = 0.5f; }
            changes = WorldResources.GrowEcho(map, settings, echo, 0);
        }
        Assert.AreEqual("forsaken", site.spec, "the sorrow has run its course: three Echoes");
        Assert.AreEqual("Forsaken", site.name);
        Assert.IsTrue(changes.Any(c => c.kind == GrowthKind.Turned && c.site == site && c.from == "fated"));

        // Where Dissonance runs high the same decay takes a single Echo.
        var fast = Fresh(7, settings);
        WorldResources.GrowEcho(fast, settings, 0, 0, initial: true);
        var other = fast.ResourceSites.First(s => s.spec == "fated");
        fast.Ruins.Add(new Ruin { id = 9, name = "Fallen", kind = SettlementKind.Town, coord = fast[other.center].coord });
        foreach (int c in other.cells) { fast[c].dissonance = 0.5f; fast[c].coherence = 0.5f; }
        WorldResources.GrowEcho(fast, settings, 1, 0);
        Assert.AreEqual("forsaken", other.spec, "high Dissonance withers them within one Echo");
    }

    [Test]
    public void Turning_ForsakenHealWhereCoherenceIsHighAndCoherentGroundHoldsTheFated()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        WorldResources.GrowEcho(map, settings, 0, 0, initial: true);
        var site = map.ResourceSites.First(s => s.spec == "fated");
        map.Ruins.Add(new Ruin { id = 9, name = "Fallen", kind = SettlementKind.Town, coord = map[site.center].coord });
        foreach (int c in site.cells) { map[c].dissonance = 0.5f; map[c].coherence = 0.5f; }
        WorldResources.GrowEcho(map, settings, 1, 0);
        Assert.AreEqual("forsaken", site.spec);

        // The ruin's sorrow stays, but the land around sings in tune: the Forsaken heal and coherent ground holds them.
        for (int echo = 2; echo <= 4 && site.spec == "forsaken"; echo++)
        {
            foreach (int c in site.cells) { map[c].dissonance = 0f; map[c].coherence = 0.8f; }
            WorldResources.GrowEcho(map, settings, echo, 0);
        }
        Assert.AreEqual("fated", site.spec, "high Coherence heals Forsaken Flowers back into Fated ones");
        foreach (int c in site.cells) { map[c].dissonance = 0f; map[c].coherence = 0.8f; }
        WorldResources.GrowEcho(map, settings, 5, 0);
        Assert.AreEqual("fated", site.spec, "and holds them golden while it lasts");
        Assert.AreEqual(0f, site.turning, 1e-4f);
    }

    [Test]
    public void Glimmerfern_GrowsBesideTheForsakenOrOnHurtAndFadesWhenItsSourceIsGone()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        WorldResources.GrowEcho(map, settings, 0, 0, initial: true);
        var ferns = map.ResourceSites.Where(s => s.spec == "fern").ToList();
        var forsakenCells = map.ResourceSites.Where(s => s.spec == "forsaken").SelectMany(s => s.cells).Select(c => map[c].coord).ToList();
        foreach (var fern in ferns)
        {
            var at = map[fern.center];
            Assert.IsTrue(forsakenCells.Any(c => HexCoord.Distance(c, at.coord) <= 2) || at.hurt >= 0.3f, "Glimmerfern grows beside Forsaken Flowers or on hurtful ground");
        }

        // A fern on ground that no longer holds it fades over two Echoes and is gone.
        var lone = new ResourceSite { index = map.ResourceSites.Count, spec = "fern", name = "Fern", kind = ResourceKind.Bloom, center = map.Tiles.First(t => !t.water && t.resourceSite < 0 && t.hurt < 0.3f && t.settlement < 0 && map.StepsFromCapital(t.coord) > 8 && !WorldResources.Rooted(map, t, settings.ResourceSite("fern"))).index };
        lone.cells.Add(lone.center);
        map.ResourceSites.Add(lone);
        WorldResources.Mark(map);
        WorldResources.GrowEcho(map, settings, 1, 0);
        Assert.AreEqual(0.5f, lone.fading, 1e-4f);
        var changes = WorldResources.GrowEcho(map, settings, 2, 0);
        Assert.IsFalse(map.ResourceSites.Contains(lone), "faded away");
        Assert.IsTrue(changes.Any(c => c.kind == GrowthKind.Faded && c.site == lone));
        Assert.IsTrue(map.ResourceSites.Select((s, i) => s.index == i).All(ok => ok), "the rest are numbered again");
    }

    [Test]
    public void Glimmerfern_ThrivesAlongSilverRiversAndAroundTheLakesTheyFeed()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        foreach (var t in map.Tiles) t.silver = false;
        // A silver river far from anything: a leyline running down real water.
        var bank = map.Tiles.First(t => !t.water && !t.lake && t.resourceSite < 0 && t.settlement < 0 && map.StepsFromCapital(t.coord) > 6
                                        && map.NeighboursOf(t).Count(n => !n.water) >= 5);
        bank.river = bank.silver = true;
        WorldResources.GrowEcho(map, settings, 1, 0);
        var moon = map.ResourceSites.Where(s => s.spec == "moon").ToList();
        Assert.IsNotEmpty(moon, "Glimmerfern thrives in the moonlit grove along the silver");
        foreach (var site in moon) foreach (int c in site.cells) Assert.LessOrEqual(map[c].silverRiverSteps, 1, "on the banks of the silver river");

        // The leylines leave the river: the moonlit ferns fade within two Echoes.
        bank.silver = false;
        WorldResources.GrowEcho(map, settings, 2, 0);
        WorldResources.GrowEcho(map, settings, 3, 0);
        Assert.IsFalse(map.ResourceSites.Any(s => moon.Contains(s)), "no silver, no moonlit grove");

        // A silver river that runs all the way into a lake lights its shores for hexes around.
        var lake = map.Tiles.FirstOrDefault(t => t.lake && map.NeighboursOf(t).Any(n => !n.water && n.resourceSite < 0 && n.settlement < 0));
        if (lake == null) { Assert.Inconclusive("the test world has no lake"); return; }
        var inflow = map.NeighboursOf(lake).First(n => !n.water && n.resourceSite < 0 && n.settlement < 0);
        inflow.river = inflow.silver = true;
        inflow.downstream = lake.index;
        WorldResources.GrowEcho(map, settings, 4, 0);
        var shore = map.ResourceSites.FirstOrDefault(s => s.spec == "shore");
        Assert.IsNotNull(shore, "the lake's shores fill with glimmerfern");
        Assert.Greater(shore.cells.Count, 2, "a wide grove, not a single patch");
        foreach (int c in shore.cells) Assert.LessOrEqual(map[c].silverLakeSteps, 2, "all around the silver-fed lake");
    }

    [Test]
    public void Sterile_TheyCannotBePlanted()
    {
        var settings = Sterile();
        var map = Fresh(7, settings);
        var ground = map.Tiles.First(t => !t.water && !t.impassable && t.resourceSite < 0 && t.settlement < 0);
        ground.authorityId = WorldAuthority.Player;
        ground.explored = true;
        StringAssert.Contains("sterile", WorldResources.WhyNotPlant(map, settings, ground, "fern"));
    }

    // ===== LUMEN SEEDS =====

    [Test]
    public void Planting_LumenSeedsNeedFeelingToTake()
    {
        var settings = Settings(need: 0.6f);
        var map = Fresh(7, settings);
        var ground = map.Tiles.First(t => !t.water && !t.impassable && t.resourceSite < 0 && t.settlement < 0 && map.StepsFromCapital(t.coord) > 6);
        ground.authorityId = WorldAuthority.Player;
        ground.explored = true;
        ground.residue = 0.1f;
        StringAssert.Contains("Emotional Residue", WorldResources.WhyNotPlant(map, settings, ground, "bells"), "a seed with no imprint grows pale and sterile");
        ground.residue = 0.5f;
        Assert.IsNull(WorldResources.WhyNotPlant(map, settings, ground, "bells"), "near your people it takes");
    }
}
