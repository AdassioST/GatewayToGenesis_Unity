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

        // One hex at a time: the first touches the held land and is yours at once; the cell stays wilderness until all seven are.
        var progress = new Dictionary<string, float> { [candidates[0].seat.key] = 0.99f };
        float drift = 0f;
        var before = WorldTerritory.Realm(map, settings, context);
        var result = WorldTerritory.Tick(map, settings, context, 0.02f, progress, ref drift);
        var cell = map[candidates[0].cell];
        Assert.AreEqual(1, result.settled.Count, "one hex settled");
        Assert.AreEqual(cell.index, result.settled[0] / MicroNavigation.PerCell, "in the best candidate");
        Assert.IsEmpty(result.adopted, "a hex is not the cell");
        Assert.AreEqual(WorldAuthority.Wilderness, cell.authorityId);
        Assert.AreEqual(1, WorldMap.SettledHexes(cell));
        Assert.IsTrue(Enumerable.Range(0, 6).Select(d => MicroNavigation.Neighbour(map, result.settled[0], d)).Any(nb => nb >= 0 && map[nb / MicroNavigation.PerCell].authorityId == WorldAuthority.Player),
            "the first hex touches the land already held");
        Assert.IsTrue(WorldTerritory.HexHeld(map, result.settled[0], WorldAuthority.Player), "the hex is yours");
        Assert.AreEqual(1f / WorldMap.OpenHexes(cell), WorldTerritory.HeldShare(map, cell), 1e-5f, "a share of the cell");
        var after = WorldTerritory.Realm(map, settings, context);
        Assert.AreEqual(1, after.hexes);
        Assert.AreEqual(before.land + WorldTerritory.HeldShare(map, cell), after.land, 1e-4f);
        Assert.Greater(after.load, before.load, "the hex weighs on the administration at once");
        Assert.Less(progress[candidates[0].seat.key], 1f, "the fraction of the next hex carries over");
        Assert.AreEqual(cell.index, WorldTerritory.Candidates(map, settings, 1)[0].cell, "a cell begun is finished first");

        // The Capital settles a hex a Seventh: the rest of the cell in the Sevenths after. The fourth hex makes the cell
        // yours de facto, the last core.
        int open = WorldMap.OpenHexes(cell), deFacto = WorldHoldings.DeFactoHexes(map);
        bool ruled = false;
        for (int i = 1; i < open; i++)
        {
            Assert.AreNotEqual(HoldStatus.Core, WorldHoldings.Status(map, cell, WorldAuthority.Player), "not yet core");
            result = WorldTerritory.Tick(map, settings, context, 1f, progress, ref drift);
            int hexes = WorldMap.SettledHexes(cell);
            if (hexes < deFacto) Assert.AreEqual(WorldAuthority.Wilderness, cell.authorityId, "too few hexes to rule it");
            else if (hexes < open)
            {
                Assert.AreEqual(WorldAuthority.Player, cell.authorityId, "yours de facto");
                Assert.AreEqual(HoldStatus.DeFacto, cell.hold);
                Assert.AreEqual((float)hexes / open, WorldTerritory.HeldShare(map, cell), 1e-5f, "de facto still yields by the hexes held");
                if (hexes == deFacto) CollectionAssert.AreEqual(new[] { cell.index }, result.deFacto, "the fourth hex rules it");
                ruled = true;
            }
        }
        Assert.IsTrue(ruled || open <= deFacto, "a de facto stage on the way");
        CollectionAssert.AreEqual(new[] { cell.index }, result.adopted, "the last hex adopts the cell as core");
        Assert.AreEqual(HoldStatus.Core, cell.hold);
        Assert.IsTrue(WorldTerritory.FullySettled(cell));
        Assert.AreEqual(WorldAuthority.Player, cell.authorityId);
        CollectionAssert.Contains(map.Adopted, cell.index);
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
        Assert.AreEqual(4f, seat.held, 1e-4f, "the Capital holds as many cells as it may");
        Assert.IsFalse(map.Tiles.Any(t => t.authorityId == WorldAuthority.Wilderness && t.microHeldMask != 0), "no hex held beyond its room");
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

        Adopt(map, settings, new RealmContext { policy = BorderPolicy.Expand }, 160f);
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
            int n = Adopt(map, settings, new RealmContext { governmentCapacity = 0, policy = policy }, 420f);
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
        int adopted = Adopt(map, settings, new RealmContext { policy = BorderPolicy.Expand }, 40f);
        Assert.Greater(adopted, 3);

        rules.baseCapacity = 0.5f;
        rules.perGovernmentCapacity = 0f;
        rules.coherenceCapacity = 0f;
        // A cell held in part may slip away too; a claimed hex never does.
        var claimed = map.Tiles.First(t => t.authorityId == WorldAuthority.Wilderness && WorldAuthority.WhyNotClaim(map, t) == null);
        int claimedHex = WorldTerritory.NextHex(map, claimed, WorldAuthority.Player);
        Assert.IsTrue(WorldTerritory.ClaimHex(map, settings, claimedHex, out _));
        // Core adopted cells, and cells held in part or de facto (not core).
        var before = map.Adopted.Concat(map.Tiles.Where(t => t.microHeldMask != 0 && !t.whole && t.hold != HoldStatus.Core
            && (t.authorityId == WorldAuthority.Wilderness || WorldAuthority.IsPlayers(t.authorityId))).Select(t => t.index)).ToList();
        float drift = 0f;
        var result = WorldTerritory.Tick(map, settings, new RealmContext(), 4f, new Dictionary<string, float>(), ref drift);
        Assert.AreEqual((int)(rules.driftPerSeventh * 4f), result.lost.Count, "the fringe slips away at the drift rate");
        foreach (int c in result.lost)
        {
            CollectionAssert.Contains(before, c);
            Assert.AreEqual(WorldAuthority.Wilderness, map[c].authorityId);
            Assert.AreEqual(map[c].microClaimMask, map[c].microHeldMask, "its settlers leave with it");
        }
        Assert.IsTrue(WorldTerritory.HexHeld(map, claimedHex, WorldAuthority.Player), "a claimed hex stays");
        Assert.IsEmpty(result.adopted, "an overextended realm adopts nothing");
    }

    // ===== PEOPLE, CHANCE AND CLAIMS =====

    [Test]
    public void Population_NoOneSettlesBelowTheMinimumAndMorePeopleSettleMore()
    {
        var rules = new TerritoryRules();
        Assert.AreEqual(0f, WorldTerritory.PopulationShare(rules, rules.adoptionMinPopulation - 1), "too few citizens");
        Assert.AreEqual(rules.adoptionLeastShare, WorldTerritory.PopulationShare(rules, rules.adoptionMinPopulation), 1e-5f);
        Assert.AreEqual(1f, WorldTerritory.PopulationShare(rules, rules.adoptionFullPopulation), 1e-5f);
        Assert.AreEqual(1f, WorldTerritory.PopulationShare(rules, -1), "not counted: full pace");
        Assert.Less(WorldTerritory.PopulationShare(rules, 40), WorldTerritory.PopulationShare(rules, 70));

        var settings = Gen();
        int Settled(int population)
        {
            var map = Fresh(7, settings);
            Know(map, settings, map.Capital, 4);
            float drift = 0f;
            var result = WorldTerritory.Tick(map, settings, new RealmContext { population = population }, 10f, new Dictionary<string, float>(), ref drift);
            var realm = WorldTerritory.Realm(map, settings, new RealmContext { population = population });
            if (population < rules.adoptionMinPopulation) Assert.AreEqual(0f, realm.adoptionRate, "the Realm says it waits for people");
            return result.settled.Count;
        }
        Assert.AreEqual(0, Settled(rules.adoptionMinPopulation - 1), "no one to send");
        int few = Settled(rules.adoptionMinPopulation), many = Settled(rules.adoptionFullPopulation);
        Assert.Greater(few, 0, "the minimum is enough to begin");
        Assert.Greater(many, few, "more citizens, more hexes settled");
    }

    [Test]
    public void Chance_TheSeatsPaceIsAChanceRolledEachSeventh()
    {
        var settings = Gen();
        var rules = new TerritoryRules();
        rules.seats.Find(s => s.kind == SeatKind.Capital).adoptPerSeventh = 0.3f;
        int Settled(double draw, float sevenths)
        {
            var map = Fresh(7, settings, rules);
            Know(map, settings, map.Capital, 4);
            float drift = 0f;
            int rolls = 0;
            var progress = new Dictionary<string, float>();
            var result = WorldTerritory.Tick(map, settings, new RealmContext(), sevenths, progress, ref drift, () => { rolls++; return draw; });
            Assert.AreEqual((int)sevenths, rolls, "one roll per seat and whole Seventh");
            return result.settled.Count;
        }
        Assert.AreEqual(0, Settled(0.5, 5f), "a draw above the chance settles nothing");
        Assert.AreEqual(5, Settled(0.1, 5f), "a draw under it settles a hex each Seventh");
        Assert.AreEqual(0, Settled(0.1, 0.5f), "half a Seventh rolls nothing yet");
    }

    [Test]
    public void Claims_TakeOneHexAtATimeBorderingLandYouHold()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        Know(map, settings, map.Capital, 3);
        var near = map.NeighboursOf(map.Get(map.Capital)).First(t => !t.water && !t.impassable && t.microBlockedMask == 0 && WorldAuthority.WhyNotClaim(map, t) == null);
        int open = WorldMap.OpenHexes(near);
        // A hex touching nothing you hold cannot be claimed; the heart of a cell beside yours never touches it.
        int heart = near.index * MicroNavigation.PerCell;
        Assert.AreEqual("It must border a hex you hold.", WorldAuthority.WhyNotClaimHex(map, heart));

        int first = WorldTerritory.NextHex(map, near, WorldAuthority.Player);
        Assert.Greater(WorldTerritory.Touching(map, first, WorldAuthority.Player), 0);
        Assert.IsTrue(WorldTerritory.ClaimHex(map, settings, first, out var status));
        Assert.AreEqual(HoldStatus.Partial, status, "one hex is not the cell");
        Assert.AreEqual(WorldAuthority.Wilderness, near.authorityId, "the rest stays wilderness");
        Assert.IsTrue(WorldTerritory.HexHeld(map, first, WorldAuthority.Player), "the hex is yours at once");
        Assert.AreEqual(1, WorldTerritory.ClaimedHexes(map));
        Assert.AreEqual("This hex is already yours.", WorldAuthority.WhyNotClaimHex(map, first));
        Assert.IsNull(WorldAuthority.WhyNotClaimHex(map, heart), "the heart touches the claimed hex now");
        Assert.AreEqual(1f / open, WorldTerritory.HeldShare(map, near), 1e-5f);

        // Claim the rest hex by hex: the fourth makes it yours de facto (still claimable), the last core.
        for (int i = 1; i < open; i++)
        {
            Assert.AreNotEqual(HoldStatus.Core, status, "not yet core");
            Assert.IsTrue(WorldTerritory.ClaimHex(map, settings, WorldTerritory.NextHex(map, near, WorldAuthority.Player), out status));
            if (i + 1 >= WorldHoldings.DeFactoHexes(map) && i + 1 < open)
            {
                Assert.AreEqual(HoldStatus.DeFacto, status);
                Assert.AreEqual(WorldAuthority.Player, near.authorityId, "de facto yours: your authority");
                Assert.IsNull(WorldAuthority.WhyNotClaim(map, near), "its free hexes can still be claimed");
            }
        }
        Assert.AreEqual(HoldStatus.Core, status, "the last hex makes it core");
        Assert.AreEqual(WorldAuthority.Player, near.authorityId);
        CollectionAssert.Contains(map.Claims, near.index);
        Assert.AreEqual(open, WorldTerritory.ClaimedHexes(map));
        Assert.AreEqual("It is already yours.", WorldAuthority.WhyNotClaim(map, near));
        Assert.IsFalse(WorldTerritory.ClaimHex(map, settings, first, out _), "nothing left to claim");
        Assert.AreEqual(1, map.Claims.Count(c => c == near.index), "claimed once");

        // A cell society had begun and a claim finished joins the Claims (claimed land never slips away).
        var other = map.Tiles.First(t => t.authorityId == WorldAuthority.Wilderness && t.microBlockedMask == 0 && WorldAuthority.WhyNotClaim(map, t) == null);
        while (WorldMap.SettledHexes(other) < WorldMap.OpenHexes(other) - 1 && WorldTerritory.SettleHex(map, other, WorldAuthority.Player) >= 0) { }
        Assert.IsTrue(WorldTerritory.ClaimHex(map, settings, WorldTerritory.NextHex(map, other, WorldAuthority.Player), out status));
        Assert.AreEqual(HoldStatus.Core, status);
        CollectionAssert.Contains(map.Claims, other.index);
        CollectionAssert.DoesNotContain(map.Adopted, other.index);
    }

    [Test]
    public void Claims_LeftHalfSettledByAnOlderSaveFinishAtOnce()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        Know(map, settings, map.Capital, 3);
        var near = map.NeighboursOf(map.Get(map.Capital)).First(t => !t.water && !t.impassable && WorldAuthority.WhyNotClaim(map, t) == null);
        map.Claiming.Add(near.index);
        StringAssert.Contains("already settling", WorldAuthority.WhyNotClaim(map, near));
        Assert.AreEqual("It is being claimed.", WorldTerritory.WhyNotAdopt(map, settings, near, map.territory.Seat(near.pullSeat)), "society leaves a claim to its settlers");

        var result = WorldTerritory.AdvanceClaims(map, settings);
        CollectionAssert.AreEqual(new[] { near.index }, result.claimed);
        Assert.AreEqual(WorldMap.OpenHexes(near), result.settled.Count);
        Assert.AreEqual(WorldAuthority.Player, near.authorityId);
        CollectionAssert.Contains(map.Claims, near.index);
        CollectionAssert.IsEmpty(map.Claiming);
    }

    [Test]
    public void Forecast_NamesTheHexesSocietySettlesNextInOrderAndWhen()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        Know(map, settings, map.Capital, 4);
        var seat = map.territory.Seats.First(s => s.IsPlayers && s.HasRoom && s.adoptPerSeventh > 0f && WorldTerritory.Candidates(map, settings, 1, s).Count > 0);
        var plans = WorldTerritory.Forecast(map, settings, new RealmContext());
        var plan = plans.First(p => p.seats.Contains(seat.name));
        var t = map[plan.cell];
        int mask = t.microHeldMask;
        Assert.AreEqual(WorldMap.OpenHexes(t) - WorldMap.SettledHexes(t), plan.hexes.Count, "every hex still to settle");
        Assert.AreEqual(plan.hexes.Count, plan.sevenths.Count);
        for (int i = 1; i < plan.sevenths.Count; i++) Assert.GreaterOrEqual(plan.sevenths[i], plan.sevenths[i - 1], "later hexes come later");
        Assert.AreEqual(mask, t.microHeldMask, "forecasting changes nothing");
        Assert.AreEqual(WorldTerritory.Candidates(map, settings, 1, seat)[0].cell, plan.cell, "the cell the seat settles next");
        Assert.AreEqual(WorldTerritory.NextHex(map, t, WorldAuthority.Player), plan.hexes[0], "the hex Tick settles next");

        // Time already waited brings the next hex nearer; too few citizens stop it altogether.
        var waited = WorldTerritory.Forecast(map, settings, new RealmContext(), new Dictionary<string, float> { [seat.key] = 0.5f });
        Assert.Less(waited.First(p => p.cell == plan.cell).sevenths[0], plan.sevenths[0]);
        var few = WorldTerritory.Forecast(map, settings, new RealmContext { population = WorldTerritory.RulesOf(map).adoptionMinPopulation - 1 });
        CollectionAssert.IsEmpty(few, "no settlers below the minimum population");
    }

    [Test]
    public void NextHex_FillsACellFromTheBorderInwardAndSkipsCrags()
    {
        var settings = Gen();
        var map = Fresh(7, settings);
        var near = map.NeighboursOf(map.Get(map.Capital)).First(t => !t.water && !t.impassable);
        near.microBlockedMask = 1 << 4;
        var order = new List<int>();
        int id;
        while ((id = WorldTerritory.SettleHex(map, near, WorldAuthority.Player)) >= 0) order.Add(id % MicroNavigation.PerCell);
        Assert.AreEqual(6, order.Count, "every hex but the crag");
        CollectionAssert.DoesNotContain(order, 4);
        Assert.AreNotEqual(0, order[0], "the heart is not where settling starts");
        Assert.IsTrue(WorldTerritory.FullySettled(near), "the crag comes with the rest");
    }
}
