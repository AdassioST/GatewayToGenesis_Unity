using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Territorial pull, passive adoption and Administrative Capacity (<see cref="WorldTerritory"/>) and beauty
/// (<see cref="WorldBeauty"/>), with no scene. Uses the small catalog of <see cref="WorldGenerationTests"/>, with
/// authority starting at the Capital's own cell as in the game.
/// </summary>
public class WorldTerritoryTests
{
    private static WorldGenSettings Gen()
    {
        var s = WorldGenerationTests.Settings();
        s.capitalAuthorityRadius = 0;
        s.startExploreRadius = 0;
        s.startRevealRadius = 1;
        return s;
    }

    private static WorldMap Fresh(int seed, WorldGenSettings settings, TerritoryRules rules = null)
    {
        var map = WorldGenerator.Generate(seed, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        map.territoryRules = rules ?? new TerritoryRules();
        WorldCivilization.Rebuild(map, settings);
        return map;
    }

    // An expedition walked the land around the Capital: it is known, so society may adopt it.
    private static void Know(WorldMap map, WorldGenSettings settings, HexCoord at, int radius)
    {
        map.KnowAround(at, radius, radius + 1, id => settings.Terrain(id)?.passable != false);
        WorldCivilization.Rebuild(map, settings);
    }

    private static int Adopt(WorldMap map, WorldGenSettings settings, RealmContext context, float sevenths)
    {
        float drift = 0f;
        return WorldTerritory.Tick(map, settings, context, sevenths, new Dictionary<string, float>(), ref drift).adopted.Count;
    }

    // ===== BEAUTY =====

    [Test]
    public void Beauty_ReadsTheGroundWhatStandsOnItAndTheMagic()
    {
        var settings = Gen();
        settings.Terrain("ash").beauty = -0.6f;
        settings.Terrain("glade").beauty = 0.5f;
        var map = Fresh(42, settings);

        foreach (var t in map.Tiles)
        {
            Assert.IsTrue(t.beauty >= -1f && t.beauty <= 1f, $"beauty {t.beauty} at {t.coord}");
            if (t.water) Assert.AreEqual(0f, t.beauty, "water keeps no beauty of its own");
        }
        foreach (var t in map.Tiles.Where((t, i) => !t.water && i % 37 == 0))
        {
            float sum = WorldBeauty.Explain(map, settings, t).Sum(r => r.points);
            Assert.AreEqual(Math.Max(-1f, Math.Min(1f, sum)), t.beauty, 1e-4f, "the reasons add up to the beauty");
        }
        var ash = map.Tiles.FirstOrDefault(t => t.terrain == "ash" && !t.water);
        if (ash != null) Assert.IsTrue(WorldBeauty.Explain(map, settings, ash).Any(r => r.reason == "ash" && Math.Abs(r.points + 0.6f) < 1e-4f), "the ground's own beauty counts");

        var fair = map.Tiles.First(t => !t.water && !t.impassable && t.beauty > -0.5f && t.dissonance < 0.3f);
        float before = fair.beauty;
        fair.dissonance += 0.5f;
        WorldBeauty.Refresh(map, settings);
        Assert.Less(fair.beauty, before, "dissonance makes a place uglier");
        Assert.AreEqual("hideous", WorldBeauty.Word(-0.9f));
        Assert.AreEqual("sublime", WorldBeauty.Word(0.9f));
    }

    // ===== PULL =====

    [Test]
    public void Pull_TheCapitalPullsFromItsCellAndFadesWithGovernanceTravel()
    {
        var settings = Gen();
        settings.Terrain("wood").governance = 3f;
        var map = Fresh(42, settings);
        var rules = WorldTerritory.RulesOf(map);
        var capital = map.Get(map.Capital);
        var seat = map.territory.Seats.Single(s => s.kind == SeatKind.Capital);

        Assert.AreEqual(seat.index, capital.pullSeat);
        Assert.AreEqual(rules.Seat(SeatKind.Capital).strength, capital.pull, 1e-4f, "full strength at the seat");
        Assert.IsNotEmpty(seat.travel);
        foreach (var pair in seat.travel)
        {
            var t = map[pair.Key];
            Assert.IsFalse(t.water || t.impassable, "pull never crosses water or barriers");
            Assert.AreEqual(seat.strength * Math.Max(0f, 1f - pair.Value / seat.reach), seat.PullAt(pair.Key), 1e-4f);
            Assert.LessOrEqual(pair.Value, seat.reach + 1e-4f);
        }
        // Farther (in governance travel) always means weaker.
        var ordered = seat.travel.OrderBy(p => p.Value).Select(p => seat.PullAt(p.Key)).ToList();
        for (int i = 1; i < ordered.Count; i++) Assert.LessOrEqual(ordered[i], ordered[i - 1] + 1e-5f);

        var wood = map.Tiles.FirstOrDefault(t => t.terrain == "wood" && !t.water);
        if (wood != null) Assert.AreEqual(3f, WorldTerritory.Difficulty(settings, wood), 1e-5f, "a terrain's governance difficulty");
        Assert.IsTrue(float.IsPositiveInfinity(WorldTerritory.Difficulty(settings, map.Tiles.First(t => t.water))));
        var land = map.Tiles.First(t => !t.water && !t.impassable && !t.road && !t.river);
        float open = WorldTerritory.Step(rules, settings, land);
        land.road = true;
        Assert.AreEqual(open * rules.roadFactor, WorldTerritory.Step(rules, settings, land), 1e-5f, "roads carry the administration");
        land.road = false;
    }

    [Test]
    public void Priority_SocietyWantsFertileWateredBeautifulLandFirstAndHardDangerousLandLast()
    {
        var settings = Gen();
        var map = Fresh(42, settings);
        var rules = WorldTerritory.RulesOf(map);
        var t = map.Tiles.First(x => !x.water && !x.impassable && x.danger < 0.1f);
        float fertility = t.landFertility, beauty = t.beauty, danger = t.danger;
        float basis = WorldTerritory.Priority(map, settings, rules, t);
        t.landFertility = fertility + 0.3f;
        Assert.Greater(WorldTerritory.Priority(map, settings, rules, t), basis, "fertile land first");
        t.landFertility = fertility;
        t.beauty = beauty + 0.5f;
        Assert.Greater(WorldTerritory.Priority(map, settings, rules, t), basis, "beautiful land first");
        t.beauty = beauty;
        t.danger = danger + 0.5f;
        Assert.Less(WorldTerritory.Priority(map, settings, rules, t), basis, "dangerous land last");
        t.danger = danger;
        settings.Terrain(t.terrain).governance = 3f;
        Assert.Less(WorldTerritory.Priority(map, settings, rules, t), basis, "hard ground (mountains, bogs) last");
        settings.Terrain(t.terrain).governance = 0f;
        Assert.Greater(WorldTerritory.Priority(map, settings, rules, t), 0f);
    }

    // ===== ADOPTION =====

    [Test]
    public void Adoption_TakesTheBestKnownBorderingCellFirst()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        var capital = map.Get(map.Capital);
        var context = new RealmContext();

        var beside = map.NeighboursOf(capital).First(t => !t.water && !t.impassable && !t.known);
        Assert.IsNull(WorldTerritory.WhyNotAdopt(map, settings, beside, map.territory.Seat(beside.pullSeat)), "the locals know the ground beside the Capital");
        var farther = map.Tiles.First(t => !t.water && !t.impassable && !t.known && HexCoord.Distance(t.coord, capital.coord) == 2 && t.pullSeat >= 0);
        StringAssert.StartsWith("No one knows this land yet", WorldTerritory.WhyNotAdopt(map, settings, farther, map.territory.Seat(farther.pullSeat)));
        Know(map, settings, map.Capital, 4);

        var candidates = WorldTerritory.Candidates(map, settings);
        Assert.IsNotEmpty(candidates);
        for (int i = 1; i < candidates.Count; i++) Assert.LessOrEqual(candidates[i].score, candidates[i - 1].score, "best first");
        foreach (var c in candidates)
        {
            var t = map[c.cell];
            Assert.AreEqual(WorldAuthority.Wilderness, t.authorityId);
            Assert.IsTrue(map.NeighboursOf(t).Any(n => n.authorityId == WorldAuthority.Player), "it borders what is held");
            Assert.GreaterOrEqual(c.pull, WorldTerritory.RulesOf(map).adoptThreshold);
        }

        var progress = new Dictionary<string, float> { [candidates[0].seat.key] = 0.99f };
        float drift = 0f;
        var result = WorldTerritory.Tick(map, settings, context, 0.02f, progress, ref drift);
        CollectionAssert.AreEqual(new[] { candidates[0].cell }, result.adopted, "society adopts the best candidate");
        Assert.AreEqual(WorldAuthority.Player, map[candidates[0].cell].authorityId);
        CollectionAssert.Contains(map.Adopted, candidates[0].cell);
        Assert.Less(progress[candidates[0].seat.key], 1f, "the fraction of the next cell carries over");
    }

    [Test]
    public void Adoption_EachSeatHoldsAtMostItsCellsAndTheLandStaysWhole()
    {
        var settings = Gen();
        var rules = new TerritoryRules();
        rules.seats.Find(s => s.kind == SeatKind.Capital).maxCells = 4;
        var map = Fresh(7, settings, rules);
        Know(map, settings, map.Capital, 5);

        Adopt(map, settings, new RealmContext { policy = BorderPolicy.Expand }, 50f);
        var seat = map.territory.Seats.Single(s => s.kind == SeatKind.Capital);
        Assert.AreEqual(4, seat.held, "the Capital holds as many cells as it may");
        Assert.AreEqual(4, map.Tiles.Count(t => t.authorityId == WorldAuthority.Player));
        var outside = map.NeighboursOf(map[map.Adopted[0]]).First(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable);
        StringAssert.Contains("holds all it can", WorldTerritory.WhyNotAdopt(map, settings, outside, seat));

        // Every held cell is joined to the Capital through held land.
        var reached = new HashSet<int> { map.Get(map.Capital).index };
        var open = new Queue<int>(reached);
        while (open.Count > 0)
            foreach (var n in map.NeighboursOf(map[open.Dequeue()]))
                if (n.authorityId == WorldAuthority.Player && reached.Add(n.index)) open.Enqueue(n.index);
        Assert.AreEqual(4, reached.Count, "adopted land is contiguous");
    }

    [Test]
    public void Adoption_NoCellARivalPullsHarder()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        Know(map, settings, map.Capital, 4);
        var best = WorldTerritory.Candidates(map, settings, 1)[0];
        map[best.cell].rivalPull = 5f;
        Assert.AreEqual("A rival pulls harder here.", WorldTerritory.WhyNotAdopt(map, settings, map[best.cell], best.seat));
        Assert.IsFalse(WorldTerritory.Candidates(map, settings).Any(c => c.cell == best.cell));
    }

    [Test]
    public void Adoption_AnOutpostsPocketStaysDetached()
    {
        var settings = Gen();
        var srules = new SettlementRules();
        var map = Fresh(7, settings, srules.territory);
        var capital = map.Get(map.Capital);
        var site = map.Tiles.Where(t => !t.water && !t.impassable && t.authorityId == WorldAuthority.Wilderness && t.enclave < 0 && t.settlement < 0
                && HexCoord.Distance(t.coord, capital.coord) >= 14 && map.NeighboursOf(t).Count(n => !n.water && !n.impassable) == 6
                && map.Enclaves.All(e => HexCoord.Distance(e.coord, t.coord) > 8))
            .OrderBy(t => HexCoord.Distance(t.coord, capital.coord)).First();
        Know(map, settings, site.coord, 2);
        var outpost = WorldCivilization.Found(map, settings, srules, site.coord, SettlementKind.Outpost, 0);
        var seat = map.territory.OfSettlement(outpost.id);
        Assert.AreEqual(SeatKind.Outpost, seat.kind);
        Assert.AreEqual(WorldAuthority.Outpost, seat.authorityId);

        Adopt(map, settings, new RealmContext { policy = BorderPolicy.Expand }, 40f);
        var pocket = map.Tiles.Where(t => t.authorityId == WorldAuthority.Outpost && t.index != site.index).ToList();
        Assert.IsNotEmpty(pocket, "an Outpost pulls in a little land of its own");
        Assert.LessOrEqual(pocket.Count + 1, seat.maxCells, "no more than its seat holds");
        Assert.IsTrue(pocket.All(t => HexCoord.Distance(t.coord, site.coord) <= 2), "around the Outpost, outside Administrative Authority");
    }

    // ===== ADMINISTRATION =====

    [Test]
    public void Realm_HorizontalGivesWayToVerticalAsTheLandOutgrowsCapacity()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        var rules = WorldTerritory.RulesOf(map);
        var realm = WorldTerritory.Realm(map, settings, new RealmContext());

        Assert.AreEqual(1, realm.cells, "only the Capital's cell at first");
        Assert.AreEqual(Expansion.Horizontal, realm.favoured);
        Assert.AreEqual(1f, realm.efficiency);
        Assert.LessOrEqual(realm.comfortCells, realm.breakEvenCells);
        Assert.LessOrEqual(realm.breakEvenCells, realm.stopCells);
        Assert.Greater(realm.capacity, 0f);
        Assert.IsTrue(realm.capacitySources.Any(s => s.source.StartsWith("The Capital")));

        Assert.AreEqual(1f, WorldTerritory.Efficiency(rules, rules.comfortStrain), 1e-5f);
        Assert.AreEqual(1f - rules.efficiencyDrop * 0.2f, WorldTerritory.Efficiency(rules, rules.comfortStrain + 0.2f), 1e-5f);
        Assert.AreEqual(rules.efficiencyFloor, WorldTerritory.Efficiency(rules, 10f), 1e-5f);
        // Break-even: the strain at which N x efficiency stops growing.
        float be = WorldTerritory.BreakEvenStrain(rules), d = 0.01f;
        float Value(float strain) => strain * WorldTerritory.Efficiency(rules, strain);
        Assert.GreaterOrEqual(Value(be), Value(be - d) - 1e-5f);
        Assert.GreaterOrEqual(Value(be), Value(be + d) - 1e-5f);

        var wider = WorldTerritory.Realm(map, settings, new RealmContext { governmentCapacity = 3 });
        Assert.AreEqual(realm.capacity + 2f * rules.perGovernmentCapacity, wider.capacity, 1e-4f, "Government Capacity raises Administrative Capacity");
        var council = new RealmContext();
        council.extra.Add(("A governing legend", 4f));
        Assert.AreEqual(realm.capacity + 4f, WorldTerritory.Realm(map, settings, council).capacity, 1e-4f);
    }

    [Test]
    public void Realm_DevelopmentRaisesCapacityAndLandRaisesLoad()
    {
        var settings = Gen();
        settings.capitalAuthorityRadius = 5;
        var srules = new SettlementRules();
        var map = Fresh(42, settings, srules.territory);
        var site = map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && !t.water && !t.impassable && t.enclave < 0
                && HexCoord.Distance(t.coord, map.Capital) >= srules.spacing)
            .OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).ThenBy(t => t.index).First();
        map.Explore(site.coord);
        var town = WorldCivilization.Found(map, settings, srules, site.coord, SettlementKind.Town, 0);
        var spec = srules.territory.Seat(SeatKind.Town);

        town.development = 0f;
        WorldCivilization.Rebuild(map, settings);
        var low = WorldTerritory.Realm(map, settings, new RealmContext());
        town.development = 100f;
        WorldCivilization.Rebuild(map, settings);
        var high = WorldTerritory.Realm(map, settings, new RealmContext());
        Assert.AreEqual(spec.capacity, high.capacity - low.capacity, 1e-3f, "a developed town carries more of the administration (vertical growth)");
        Assert.Greater(high.developmentLever, 0f);

        float load = high.load;
        var extra = map.Tiles.First(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable && map.NeighboursOf(t).Any(n => n.authorityId == WorldAuthority.Player));
        map.Claims.Add(extra.index);
        WorldCivilization.Rebuild(map, settings);
        Assert.Greater(WorldTerritory.Realm(map, settings, new RealmContext()).load, load, "every cell held weighs on the administration (horizontal growth)");
    }

    [Test]
    public void Policy_HoldAdoptsNothingAndMeasuredStopsBeforeExpand()
    {
        var settings = Gen();
        TerritoryRules Small()
        {
            var r = new TerritoryRules { baseCapacity = 20f, perGovernmentCapacity = 0f, coherenceCapacity = 0f, councilCapacity = 0f };
            return r;
        }
        int Run(BorderPolicy policy, out RealmReport report)
        {
            var map = Fresh(7, settings, Small());
            Know(map, settings, map.Capital, 6);
            int n = Adopt(map, settings, new RealmContext { governmentCapacity = 0, policy = policy }, 60f);
            report = WorldTerritory.Realm(map, settings, new RealmContext { governmentCapacity = 0, policy = policy });
            return n;
        }
        Assert.AreEqual(0, Run(BorderPolicy.Hold, out _), "holding the borders adopts nothing");
        int measured = Run(BorderPolicy.Measured, out var m);
        int expand = Run(BorderPolicy.Expand, out var e);
        Assert.Greater(measured, 0);
        Assert.Greater(expand, measured, "expanding goes past the break-even");
        Assert.AreEqual(0f, m.adoptionRate, "measured growth stops at the break-even");
        Assert.GreaterOrEqual(e.strain, m.strain);
    }

    [Test]
    public void Drift_PastCollapseTheWeakestHeldLandSlipsAway()
    {
        var settings = Gen();
        var rules = new TerritoryRules();
        var map = Fresh(7, settings, rules);
        Know(map, settings, map.Capital, 5);
        int adopted = Adopt(map, settings, new RealmContext { policy = BorderPolicy.Expand }, 12f);
        Assert.Greater(adopted, 3);

        rules.baseCapacity = 0.5f;
        rules.perGovernmentCapacity = 0f;
        rules.coherenceCapacity = 0f;
        var before = map.Adopted.ToList();
        float drift = 0f;
        var result = WorldTerritory.Tick(map, settings, new RealmContext(), 4f, new Dictionary<string, float>(), ref drift);
        Assert.AreEqual((int)(rules.driftPerSeventh * 4f), result.lost.Count, "the fringe slips away at the drift rate");
        foreach (int c in result.lost)
        {
            CollectionAssert.Contains(before, c);
            Assert.AreEqual(WorldAuthority.Wilderness, map[c].authorityId);
        }
        Assert.IsEmpty(result.adopted, "an overextended realm adopts nothing");
    }
}
