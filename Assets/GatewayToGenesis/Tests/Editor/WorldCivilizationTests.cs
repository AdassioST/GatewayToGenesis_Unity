using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The civilization layer over the generated world, with no scene: Trade Nexus sites, multi-cell grandfields,
/// threats and Age-gated enclaves (<see cref="WorldSites"/>); Developing Towns, Outposts, Major Settlements, roads,
/// Resonance Anchors and yields (<see cref="WorldCivilization"/>); City Development (<see cref="CityDevelopment"/>),
/// travel (<see cref="WorldPaths"/>) and the lenses (<see cref="WorldLenses"/>). Uses the small catalog of
/// <see cref="WorldGenerationTests"/> plus its own sites.
/// </summary>
public class WorldCivilizationTests
{
    private static WorldGenSettings Settings()
    {
        var s = WorldGenerationTests.Settings();
        s.grandfields.Add(new GrandfieldSpec { id = "timber", name = "Timber", resource = "Elderwood", count = 3, size = 8, minDistance = 5, terrains = { "wood", "plain" } });
        s.grandfields.Add(new GrandfieldSpec { id = "late", name = "Late", resource = "Duskstone", minAge = 1, count = 2, size = 6, minDistance = 5 });
        s.enclaves.Add(new EnclaveSpec { id = "watch", name = "Watch", family = EnclaveFamily.Militant, count = 2, site = EnclaveSite.Any, minDistance = 8, spacing = 10 });
        s.enclaves.Add(new EnclaveSpec { id = "traders", name = "Traders", family = EnclaveFamily.Trading, count = 1, site = EnclaveSite.Any, minDistance = 8, spacing = 10 });
        s.enclaves.Add(new EnclaveSpec { id = "gardens", name = "Gardens", family = EnclaveFamily.Agromagical, minAge = 1, count = 1, site = EnclaveSite.Any, minDistance = 8 });
        s.threats.Add(new ThreatSpec { id = "fallout", name = "Fallout", onDissonanceSeeds = true, strength = 0.6f, radius = 6f });
        s.threats.Add(new ThreatSpec { id = "nest", name = "Nest", minAge = 2, count = 2, strength = 0.8f, radius = 5f, minDistance = 10 });
        return s;
    }

    private static readonly Dictionary<int, WorldMap> Cache = new Dictionary<int, WorldMap>();

    private static WorldMap World(int seed)
    {
        if (!Cache.TryGetValue(seed, out var map)) Cache[seed] = map = Fresh(seed);
        return map;
    }

    private static WorldMap Fresh(int seed) => WorldGenerator.Generate(seed, Settings(), WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    private static SettlementRules Rules() => new SettlementRules();

    // A passable cell inside the capital's authority, far enough from the capital to hold a town.
    private static WorldTile TownSite(WorldMap map, SettlementRules rules)
    {
        return map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && !t.water && !t.impassable && t.enclave < 0
                && HexCoord.Distance(t.coord, map.Capital) >= rules.spacing)
            .OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).ThenBy(t => t.index).First();
    }

    // ===== SITES =====

    [TestCase(7)]
    [TestCase(42)]
    public void Sites_NexusGrandfieldsThreatsAndEnclavesFollowTheirRules(int seed)
    {
        var map = World(seed);
        var settings = Settings();

        Assert.IsNotEmpty(map.NexusSites, "some gifted geography can hold a Trade Nexus");
        foreach (int site in map.NexusSites)
        {
            var t = map[site];
            Assert.IsFalse(t.water || t.impassable, "a nexus site is dry, passable land");
            CollectionAssert.Contains(new[] { "harbor", "estuary", "pass", "confluence" }, t.nexus);
            Assert.IsTrue(map.NexusSites.Where(o => o != site).All(o => HexCoord.Distance(map[o].coord, t.coord) >= settings.nexus.spacing), "nexus sites are spaced apart");
        }

        Assert.IsNotEmpty(map.Grandfields, "grandfields are placed");
        Assert.IsFalse(map.Grandfields.Any(g => g.spec == "late"), "a later Age's grandfields wait for it");
        foreach (var field in map.Grandfields)
        {
            Assert.GreaterOrEqual(field.cells.Count, 4, "a grandfield is a multi-cell footprint");
            Assert.AreEqual(1f, map[field.center].grandfieldDensity, 1e-4f, "densest at its heart");
            Assert.IsTrue(field.cells.All(c => map[c].grandfield == field.index && map[c].grandfieldDensity <= 1f && map[c].grandfieldDensity > 0f));
            Assert.IsTrue(field.cells.Any(c => map[c].grandfieldDensity < 1f), "density falls toward its edge");
            Assert.IsTrue(field.cells.All(c => !map[c].water && !map[c].impassable && !map[c].sacred));
            // Connected: every cell reaches the heart through the field.
            var reached = new HashSet<int> { field.center };
            var open = new Queue<int>(reached);
            while (open.Count > 0)
                foreach (var n in map.NeighboursOf(map[open.Dequeue()]))
                    if (n.grandfield == field.index && reached.Add(n.index)) open.Enqueue(n.index);
            Assert.AreEqual(field.cells.Count, reached.Count, "a grandfield is one connected patch");
        }

        Assert.IsTrue(map.Threats.All(t => t.spec == "fallout"), "only Age 0 threats stand at Age 0");
        foreach (int s in map.Magic.SacredSites)
            foreach (var coord in HexCoord.Spiral(map[s].coord, WorldSites.SacredCalm))
                Assert.AreEqual(0f, map.Get(coord)?.danger ?? 0f, "Sacred ground stays calm");
        if (map.Threats.Count > 0) Assert.IsTrue(map.Tiles.Any(t => t.danger > 0.3f), "threats cast danger around them");

        Assert.IsNotEmpty(map.Enclaves, "the first enclaves stand among the survivors");
        Assert.IsTrue(map.Enclaves.All(e => e.family == EnclaveFamily.Militant || e.family == EnclaveFamily.Trading), "Age 0 knows only Militant and Trading enclaves");
        foreach (var e in map.Enclaves)
        {
            var t = map.Get(e.coord);
            Assert.AreEqual(e.AuthorityId, t.authorityId, "an enclave holds its own ground");
            Assert.AreEqual(map.Enclaves.IndexOf(e), t.enclave);
            Assert.GreaterOrEqual(HexCoord.Distance(e.coord, map.Capital), 8);
            CollectionAssert.Contains(CityDevelopment.Bindings, e.binding);
            Assert.IsTrue(e.woundResonance >= 0f && e.woundResonance <= 100f);
        }
        Assert.AreEqual(WorldAuthority.Player, map.Get(map.Capital).authorityId, "enclaves never take the capital's ground");
    }

    [Test]
    public void Sites_AreDeterministicAndLaterAgesAddTheirOwn()
    {
        var a = Fresh(99);
        var b = Fresh(99);
        CollectionAssert.AreEqual(a.NexusSites, b.NexusSites);
        CollectionAssert.AreEqual(a.Grandfields.SelectMany(g => g.cells), b.Grandfields.SelectMany(g => g.cells));
        CollectionAssert.AreEqual(a.Enclaves.Select(e => e.coord), b.Enclaves.Select(e => e.coord));

        var settings = Settings();
        a.Magic.Apply(a, 1);
        WorldSites.PlaceAge(a, settings, 1);
        Assert.IsTrue(a.Enclaves.Any(e => e.family == EnclaveFamily.Agromagical), "the Age of Renewal brings Agromagical enclaves");
        Assert.IsTrue(a.Grandfields.Any(g => g.spec == "late"));
        int enclaves = a.Enclaves.Count;
        WorldSites.PlaceAge(a, settings, 1);
        Assert.AreEqual(enclaves, a.Enclaves.Count, "placing an Age twice adds nothing");
        a.Magic.Apply(a, 2);
        WorldSites.PlaceAge(a, settings, 2);
        Assert.IsTrue(a.Threats.Any(t => t.spec == "nest"), "later Ages add their threats");
        Assert.IsTrue(a.Threats.Where(t => t.spec == "nest").All(t => !a.Magic.SacredSites.Any(s => HexCoord.Distance(a[s].coord, a[t.cell].coord) <= WorldSites.SacredShade)),
            "Atonalis avoid Sacred Sites");
    }

    // ===== SETTLEMENTS =====

    [Test]
    public void Settlements_OnlyWholeAgesCountTowardTenure()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = Rules();
        var site = TownSite(map, rules);
        map.Explore(site.coord);
        var town = WorldCivilization.Found(map, settings, rules, site.coord, SettlementKind.Town, 0); // founded mid-Age 0
        var capital = WorldCivilization.Capital(map);

        for (int passed = 1; passed <= 3; passed++) WorldCivilization.AgePassed(map);
        Assert.AreEqual(3, town.agesPresent, "it stood through three Age transitions");
        Assert.AreEqual(2, town.fullAges, "but only Ages I and II entirely: a mid-Age founding does not prove three whole Ages");
        Assert.AreEqual(3, capital.fullAges, "the Capital stood from the world's birth");
        WorldCivilization.AgePassed(map);
        Assert.AreEqual(3, town.fullAges);
    }

    [Test]
    public void Settlements_TownsStandInsideAuthorityAndApartOutpostsStandOutside()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = Rules();
        var capital = WorldCivilization.Capital(map);
        Assert.IsNotNull(capital, "the Capital is the first settlement");
        Assert.AreEqual(map.Capital, capital.coord);

        var site = TownSite(map, rules);
        Assert.AreEqual("Survey it first.", WorldCivilization.WhyNotFound(map, settings, rules, site.coord, SettlementKind.Town));
        map.Explore(site.coord);
        Assert.IsNull(WorldCivilization.WhyNotFound(map, settings, rules, site.coord, SettlementKind.Town));
        var near = map.NeighboursOf(map.Get(map.Capital)).First(t => !t.water && !t.impassable);
        map.Explore(near.coord);
        StringAssert.StartsWith("Too close", WorldCivilization.WhyNotFound(map, settings, rules, near.coord, SettlementKind.Town));

        int ownedBefore = map.Tiles.Count(t => t.authorityId == WorldAuthority.Player);
        var town = WorldCivilization.Found(map, settings, rules, site.coord, SettlementKind.Town, 0);
        Assert.AreEqual(SettlementKind.Town, town.kind);
        Assert.IsFalse(town.detached);
        Assert.AreEqual(capital.id, town.governedBy, "a town answers to the Capital when no Major Settlement is near");
        Assert.Greater(map.Tiles.Count(t => t.authorityId == WorldAuthority.Player), ownedBefore, "a town extends Administrative Authority");
        Assert.IsTrue(map.Get(site.coord).explored && map.NeighboursOf(map.Get(site.coord)).Where(t => !t.water && !t.impassable).All(t => t.explored), "a town explores the ground it walks to");

        var wild = map.Tiles.Where(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable && t.enclave < 0
                && map.Settlements.All(s => HexCoord.Distance(s.coord, t.coord) >= rules.spacing) && map.Enclaves.All(e => HexCoord.Distance(e.coord, t.coord) >= rules.spacing))
            .OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).First();
        map.Explore(wild.coord);
        StringAssert.Contains("inside your Administrative Authority", WorldCivilization.WhyNotFound(map, settings, rules, wild.coord, SettlementKind.Town));
        Assert.IsNull(WorldCivilization.WhyNotFound(map, settings, rules, wild.coord, SettlementKind.Outpost));
        var outpost = WorldCivilization.Found(map, settings, rules, wild.coord, SettlementKind.Outpost, 0);
        Assert.IsTrue(outpost.detached);
        Assert.AreEqual(WorldAuthority.Outpost, map.Get(wild.coord).authorityId, "an Outpost holds its cell outside Administrative Authority");
        Assert.IsTrue(map.NeighboursOf(map.Get(wild.coord)).All(n => n.authorityId != WorldAuthority.Outpost), "and only its cell");
    }

    [Test]
    public void Settlements_GrowTowardTheirPotentialAndPromotionRespectsGovernmentCapacity()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = Rules();
        var site = TownSite(map, rules);
        map.Explore(site.coord);
        var town = WorldCivilization.Found(map, settings, rules, site.coord, SettlementKind.Town, 0);
        float potential = WorldCivilization.Breakdown(map, rules, town).Potential;
        Assert.Greater(potential, 0f);
        WorldCivilization.Tick(map, rules, 10);
        Assert.AreEqual(Math.Min(potential, rules.growthPerSeventh * 10), town.development, 1e-3f, "growth follows the rate");
        WorldCivilization.Tick(map, rules, 100000);
        Assert.AreEqual(WorldCivilization.Breakdown(map, rules, town).Potential, town.development, 1e-3f, "it settles at its potential");

        town.development = rules.majorThreshold - 1f;
        StringAssert.StartsWith("Needs", WorldCivilization.WhyNotPromote(map, rules, town, 1));
        town.development = rules.majorThreshold;
        StringAssert.StartsWith("Government Capacity is full", WorldCivilization.WhyNotPromote(map, rules, town, 0));
        Assert.IsNull(WorldCivilization.WhyNotPromote(map, rules, town, 1));
        WorldCivilization.Promote(map, settings, rules, town);
        Assert.AreEqual(SettlementKind.Major, town.kind);
        Assert.AreEqual(rules.majorAuthorityRadius, town.authorityRadius);
        CollectionAssert.Contains(CityDevelopment.Bindings, town.binding, "a Major Settlement is attuned to one of the seven bindings");
        Assert.AreEqual(1, WorldCivilization.MajorCount(map));
        string before = town.binding;
        WorldCivilization.CycleBinding(town);
        Assert.AreNotEqual(before, town.binding);
    }

    // ===== CITY DEVELOPMENT =====

    [Test]
    public void CityDevelopment_TermsSumCoherenceCapsAndTheFieldAgrees()
    {
        var map = World(42);
        var rules = Rules();
        var land = map.Tiles.Where(t => !t.water && !t.impassable).ToList();
        foreach (var t in land.Where((_, i) => i % 97 == 0))
        {
            var b = CityDevelopment.Evaluate(map, rules, t.index);
            Assert.AreEqual(b.terms.Sum(x => x.points), b.raw, 1e-3f, "the terms sum to the raw value");
            Assert.LessOrEqual(b.Potential, b.ceiling + 1e-4f, "Coherence caps City Development");
            Assert.AreEqual(rules.ceilingBase + (100f - rules.ceilingBase) * t.coherence, b.ceiling, 1e-3f);
        }
        var field = CityDevelopment.PotentialField(map, rules);
        foreach (var t in land.Where((_, i) => i % 211 == 0))
            Assert.AreEqual(CityDevelopment.Evaluate(map, rules, t.index).Potential, field[t.index], 1e-3f, "the Settle lens reads the same sums");
        Assert.IsTrue(map.Tiles.Where(t => t.water || t.impassable).All(t => field[t.index] == 0f));

        // More Coherence never lowers the ceiling; danger always weighs down.
        var probe = land[land.Count / 2];
        float coherence = probe.coherence, danger = probe.danger;
        probe.coherence = 0.2f;
        float low = CityDevelopment.Evaluate(map, rules, probe.index).ceiling;
        probe.coherence = 0.9f;
        Assert.Greater(CityDevelopment.Evaluate(map, rules, probe.index).ceiling, low);
        float calm = CityDevelopment.Evaluate(map, rules, probe.index).raw;
        probe.danger = 0.8f;
        Assert.Less(CityDevelopment.Evaluate(map, rules, probe.index).raw, calm);
        probe.coherence = coherence;
        probe.danger = danger;
    }

    // ===== ROADS, TRAVEL AND ANCHORS =====

    [Test]
    public void Roads_JoinTheCapitalOverLandAndRaiseTradeNodes()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = Rules();
        var target = map.Tiles.Where(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable && t.enclave < 0
                && HexCoord.Distance(t.coord, map.Capital) >= 14 && map.Enclaves.All(e => HexCoord.Distance(e.coord, t.coord) >= rules.spacing))
            .OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).First();
        map.Reveal(target.coord, 0);
        var outpost = WorldCivilization.Found(map, settings, rules, target.coord, SettlementKind.Outpost, 0);
        Assert.IsFalse(WorldCivilization.Networked(map).Contains(outpost.id));
        Assert.IsTrue(WorldCivilization.PlanRoad(map, settings, outpost, out var path, out int to));
        Assert.AreEqual(WorldCivilization.Capital(map).id, to);
        Assert.IsTrue(path.Skip(1).Take(path.Count - 2).All(c => !map[c].water && !map[c].impassable), "roads run over land");
        Assert.Greater(WorldCivilization.NewRoadCells(map, path), 0);
        var route = WorldCivilization.BuildRoad(map, settings, rules, outpost, path, to);
        Assert.IsTrue(WorldCivilization.Networked(map).Contains(outpost.id), "the road joins it to the Capital");
        Assert.IsTrue(route.cells.All(c => map.Get(c).road));
        Assert.IsTrue(route.efficiency >= 0f && route.efficiency <= 1f);
        if (route.cells.Count > rules.nodeSpacing + 1) Assert.IsNotEmpty(route.nodes, "Trade Nodes stand along a long road");
        Assert.IsTrue(route.nodes.All(c => map.Get(c).tradeNode && map.Get(c).settlement < 0));
        Assert.IsFalse(WorldCivilization.PlanRoad(map, settings, outpost, out _, out _), "a joined settlement needs no second road");
    }

    [Test]
    public void Travel_LeylinesAndRoadsAreFasterDangerAndDissonanceSlower()
    {
        var settings = Settings();
        var tile = new WorldTile { terrain = "plain" };
        float open = WorldPaths.StepCost(tile, settings);
        tile.leylines = 1;
        Assert.AreEqual(open * WorldPaths.LeylineFactor, WorldPaths.StepCost(tile, settings), 1e-5f, "units move faster on leylines");
        tile.road = true;
        Assert.Less(WorldPaths.StepCost(tile, settings), open * WorldPaths.LeylineFactor);
        tile.leylines = 0;
        tile.road = false;
        tile.danger = 0.5f;
        tile.dissonance = 0.3f;
        Assert.Greater(WorldPaths.StepCost(tile, settings), open, "low Coherence and danger are attrition zones");
        Assert.IsTrue(float.IsPositiveInfinity(WorldPaths.StepCost(new WorldTile { terrain = "peaks", impassable = true }, settings)));
        Assert.IsTrue(float.IsPositiveInfinity(WorldPaths.StepCost(new WorldTile { terrain = "plain", water = true }, settings)));

        var map = World(7);
        var capital = map.Get(map.Capital);
        var far = map.Tiles.Where(t => !t.water && !t.impassable).OrderByDescending(t => HexCoord.Distance(t.coord, map.Capital)).First(t => !float.IsPositiveInfinity(WorldPaths.TravelCost(map, settings, capital.index, t.index, out _)));
        WorldPaths.TravelCost(map, settings, capital.index, far.index, out var path);
        for (int i = 1; i < path.Count; i++) Assert.AreEqual(1, HexCoord.Distance(map[path[i - 1]].coord, map[path[i]].coord), "a path steps from neighbour to neighbour");
        Assert.IsTrue(path.All(c => !map[c].water && !map[c].impassable));
    }

    [Test]
    public void Anchors_RaiseCoherenceAroundThemAndLeaveTheGeographyAlone()
    {
        var map = Fresh(42);
        var rules = Rules();
        var spot = map.Tiles.Where(t => !t.water && !t.impassable && !t.sacred && t.coherence < 0.7f && t.leylineInfluence == 0f && t.junction == 0 && !t.silver).OrderBy(t => t.index).First();
        var rivers = map.Tiles.Select(t => t.river).ToArray();
        float before = spot.coherence;
        map.Magic.SetAnchors(new[] { spot.index }, rules.anchorCoherence, rules.anchorRadius, rules.anchorPull);
        int version = map.Magic.Version;
        map.Magic.Reapply(map);
        Assert.Greater(map.Magic.Version, version, "the current Age is re-committed");
        Assert.GreaterOrEqual(spot.coherence, Math.Min(1f, before + rules.anchorCoherence) - 1e-4f, "an Anchor raises Coherence where it stands");
        CollectionAssert.AreEqual(rivers, map.Tiles.Select(t => t.river).ToArray(), "ordinary rivers never move");
        Assert.AreEqual(0, map.Magic.Age);
    }

    // ===== YIELDS, ENCLAVES AND LENSES =====

    [Test]
    public void Grandfields_CountOncePerFieldAndStayUnderTheCap()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = Rules();
        var field = map.Grandfields.OrderByDescending(g => g.cells.Count).First();
        foreach (int c in field.cells.OrderByDescending(c => map[c].grandfieldDensity).Take(2))
            map.Settlements.Add(new Settlement { id = 100 + c, name = "Outpost " + c, kind = SettlementKind.Outpost, coord = map[c].coord, detached = true });
        WorldCivilization.Rebuild(map, settings);
        var extractions = WorldCivilization.Extractions(map);
        Assert.AreEqual(1, extractions.Count(x => x.field == field), "one extraction per field, however many Outposts stand on it");
        Assert.AreEqual(1f, extractions.First(x => x.field == field).density, 1e-4f, "the richest cell counts");
        var yields = WorldCivilization.Yields(map, settings, rules).Where(y => y.resource == field.resource).ToList();
        Assert.AreEqual(1, yields.Count(y => y.percent));
        Assert.LessOrEqual(yields.Where(y => y.percent).Sum(y => y.amount), rules.grandfieldCapPercent);

        rules.grandfieldCapPercent = 100f;
        foreach (var other in map.Grandfields.Where(g => g != field && g.resource == field.resource))
            map.Settlements.Add(new Settlement { id = 500 + other.index, name = "Outpost x" + other.index, kind = SettlementKind.Outpost, coord = map[other.center].coord, detached = true });
        WorldCivilization.Rebuild(map, settings);
        Assert.LessOrEqual(WorldCivilization.Yields(map, settings, rules).Where(y => y.percent && y.resource == field.resource).Sum(y => y.amount), 100f + 1e-3f, "the cap holds across fields");
    }

    [Test]
    public void Enclaves_EnvoysLeadToSuzerainty()
    {
        var map = Fresh(42);
        var rules = Rules();
        var enclave = map.Enclaves.First();
        Assert.AreEqual("Find it first.", WorldCivilization.WhyNotEnvoy(map, enclave));
        map.Reveal(enclave.coord, 0);
        for (int i = 0; i < 3; i++) WorldCivilization.SendEnvoy(enclave, rules);
        Assert.IsFalse(enclave.suzerain);
        WorldCivilization.SendEnvoy(enclave, rules);
        Assert.IsTrue(enclave.suzerain);
        Assert.AreEqual(100f, enclave.influence);
        Assert.IsNotNull(WorldCivilization.WhyNotEnvoy(map, enclave));
        enclave.woundResonance = 80f;
        Assert.AreEqual("raw wound", enclave.WoundState);
        enclave.woundResonance = 50f;
        Assert.AreEqual("integrated wound", enclave.WoundState);
        enclave.woundResonance = 10f;
        Assert.AreEqual("forgotten wound", enclave.WoundState);
    }

    [Test]
    public void Lenses_ReadEachCellWithinRangeAndTieToCityDevelopment()
    {
        var map = World(42);
        var rules = Rules();
        var potential = CityDevelopment.PotentialField(map, rules);
        foreach (var lens in WorldLenses.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(WorldLenses.Name(lens)));
            Assert.IsFalse(string.IsNullOrEmpty(WorldLenses.Legend(lens)));
            foreach (var t in map.Tiles.Where((_, i) => i % 53 == 0))
            {
                float v = WorldLenses.Value(lens, map, t, potential);
                Assert.IsTrue(v >= 0f && v <= 1f, $"{lens} reads {v} at {t.coord}");
                if (WorldLenses.Term(lens).HasValue || lens == WorldLens.Settle) Assert.IsNotNull(WorldLenses.Hover(lens, map, t, rules, potential), $"{lens} explains {t.coord}");
            }
        }
        var land = map.Tiles.First(t => !t.water && !t.impassable);
        Assert.AreEqual(potential[land.index] / 100f, WorldLenses.Value(WorldLens.Settle, map, land, potential), 1e-5f);
        Assert.IsTrue(WorldLenses.ShowsLeylines(WorldLens.Coherence));
        Assert.AreEqual(DevelopmentTerm.LandFertility, WorldLenses.Term(WorldLens.LandFertility));
    }

    // ===== CLAIMING LAND =====

    [Test]
    public void Claims_AuthorityStartsAtTheCapitalAndGrowsOnlyByClaimingExploredBorderingWilderness()
    {
        var settings = Settings();
        settings.capitalAuthorityRadius = 0;
        settings.startExploreRadius = 0;
        settings.startRevealRadius = 1;
        var map = WorldGenerator.Generate(7, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        var capital = map.Get(map.Capital);

        CollectionAssert.AreEqual(new[] { capital.index }, map.Tiles.Where(t => t.authorityId == WorldAuthority.Player).Select(t => t.index), "only the Capital's own cell is held");
        var near = map.NeighboursOf(capital).First(t => !t.water && !t.impassable && t.authorityId == WorldAuthority.Wilderness);
        Assert.IsTrue(near.revealed && !near.explored, "the fog begins beside the Capital: seen, not explored");
        Assert.AreEqual("A scout must pass over it first.", WorldAuthority.WhyNotClaim(map, near));
        Assert.IsEmpty(WorldAuthority.Claimable(map));

        map.ExploreAround(capital.coord, 3, 4, id => settings.Terrain(id)?.passable != false);
        Assert.IsNull(WorldAuthority.WhyNotClaim(map, near), "explored and bordering the Capital");
        var far = map.Tiles.First(t => t.explored && !t.water && !t.impassable && t.authorityId == WorldAuthority.Wilderness && HexCoord.Distance(t.coord, capital.coord) == 2);
        Assert.AreEqual("It must border your authority.", WorldAuthority.WhyNotClaim(map, far));

        map.Claims.Add(near.index);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Player, near.authorityId);
        Assert.AreEqual(WorldAuthority.ClaimedReach, near.administrativeAuthority, 1e-5f);
        Assert.AreEqual("It is already yours.", WorldAuthority.WhyNotClaim(map, near));
        if (map.NeighboursOf(near).Contains(far)) Assert.IsNull(WorldAuthority.WhyNotClaim(map, far), "a claim extends the border");
        Assert.AreEqual(1.2f, WorldAuthority.ClaimScale(2, 0.1f), 1e-5f, "each claim raises the next one's cost");
    }
}
