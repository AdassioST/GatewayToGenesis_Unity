using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Where people want to live (<see cref="WorldDesirability"/>) and the outskirt tributaries, the civilization's minor
/// hubs (<see cref="WorldTributaries"/>): raised inside your authority for a hub nearby, joined to it by road, grown
/// toward their ground's desirability, upgraded to districts whose effects scale with adjacency. No scene; uses the
/// small catalog of <see cref="WorldGenerationTests"/>.
/// </summary>
public class WorldTributaryTests
{
    private static WorldGenSettings Settings() => WorldGenerationTests.Settings();

    private static WorldMap Fresh(int seed) => WorldGenerator.Generate(seed, Settings(), WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    // Held, passable, free cells where a tributary of the Capital may stand, nearest the Capital first (explored so they are known).
    private static List<WorldTile> Sites(WorldMap map, WorldGenSettings settings, SettlementRules rules, Settlement hub)
    {
        foreach (var t in map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && HexCoord.Distance(t.coord, hub.coord) <= rules.tributaries.hubReach).ToList())
            map.Explore(t.coord);
        return map.Tiles.Where(t => WorldTributaries.WhyNotFound(map, settings, rules.tributaries, t.coord, hub) == null)
            .OrderBy(t => HexCoord.Distance(t.coord, hub.coord)).ThenBy(t => t.index).ToList();
    }

    private static Settlement Raise(WorldMap map, WorldGenSettings settings, SettlementRules rules)
    {
        var capital = WorldCivilization.Capital(map);
        var site = Sites(map, settings, rules, capital).First();
        return WorldTributaries.Found(map, settings, rules, site.coord, capital, 0);
    }

    // ===== DESIRABILITY =====

    [Test]
    public void Desirability_RisesWithWhatPeopleSeekAndFallsWithDangerAndDissonance()
    {
        var map = Fresh(42);
        var rules = new DesirabilityRules();
        var t = map.Tiles.First(x => !x.water && !x.impassable && map.NeighboursOf(x).All(n => !n.water && !n.impassable));
        t.beauty = 0f; t.coherence = 0.3f; t.magicFertility = 0.2f; t.landFertility = 0.3f; t.danger = 0f; t.dissonance = 0f;
        float plain = WorldDesirability.Of(map, rules, t);
        Assert.IsTrue(plain > 0f && plain < 1f);

        foreach (Action<WorldTile> better in new Action<WorldTile>[] { x => x.beauty = 0.8f, x => x.coherence = 0.9f, x => x.magicFertility = 0.9f, x => x.landFertility = 0.9f, x => x.junction = 3 })
        {
            float before = WorldDesirability.Of(map, rules, t);
            better(t);
            Assert.Greater(WorldDesirability.Of(map, rules, t), before, "beauty, Coherence, magical and land fertility and leylines each draw people");
        }
        float fine = WorldDesirability.Of(map, rules, t);
        foreach (var n in map.NeighboursOf(t)) { n.beauty = 1f; n.coherence = 1f; n.landFertility = 1f; n.magicFertility = 1f; }
        Assert.Greater(WorldDesirability.Of(map, rules, t), fine, "fair land around raises it too");
        fine = WorldDesirability.Of(map, rules, t);
        t.danger = 0.5f;
        Assert.Less(WorldDesirability.Of(map, rules, t), fine, "danger drives people off");
        t.danger = 0f; t.dissonance = 0.5f;
        Assert.Less(WorldDesirability.Of(map, rules, t), fine, "so does dissonance");
        t.dissonance = 0f;

        // The breakdown's shares sum to the value (unclamped here).
        Assert.AreEqual(WorldDesirability.Of(map, t), WorldDesirability.Breakdown(map, t).Sum(p => p.share), 1e-4f);
        Assert.AreEqual(0f, WorldDesirability.Of(map, map.Tiles.First(x => x.water)), "no one lives on the water");

        Assert.AreEqual(rules.slowestGrowth, rules.GrowthFactor(0f), 1e-5f);
        Assert.AreEqual(rules.fastestGrowth, rules.GrowthFactor(1f), 1e-5f);
        Assert.Greater(rules.GrowthFactor(0.7f), rules.GrowthFactor(0.3f), "settlements grow faster on desirable ground");
        Assert.AreEqual(WorldDesirability.Of(map, t), WorldLenses.Value(WorldLens.Desirability, map, t, null), 1e-5f, "the lens paints it");
        Assert.IsNotNull(WorldLenses.Hover(WorldLens.Desirability, map, t, new SettlementRules(), null));
    }

    // ===== PLACEMENT =====

    [Test]
    public void Tributaries_StandInsideYourAuthorityNearAHubWithRoomAndNeverBesideAnotherSettlement()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var capital = WorldCivilization.Capital(map);

        var beside = map.NeighboursOf(map.Get(capital.coord)).First(t => !t.water && !t.impassable);
        map.Explore(beside.coord);
        StringAssert.StartsWith("Right beside", WorldTributaries.WhyNotFound(map, settings, tr, beside.coord, capital));

        var wild = map.Tiles.First(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable);
        StringAssert.Contains("inside your Administrative Authority", WorldTributaries.WhyNotFound(map, settings, tr, wild.coord, capital), "settlers found Outposts beyond it; tributaries stay home");

        var site = Sites(map, settings, rules, capital).First();
        StringAssert.StartsWith("Choose the hub", WorldTributaries.WhyNotFound(map, settings, tr, site.coord, null));
        CollectionAssert.Contains(WorldTributaries.Hubs(map, tr, site.coord), capital);
        Assert.AreEqual("Tributaries are raised from home, inside your authority, for a hub nearby.", WorldCivilization.WhyNotFound(map, settings, rules, site.coord, SettlementKind.Tributary));

        var s = WorldTributaries.Found(map, settings, rules, site.coord, capital, 0);
        Assert.AreEqual(SettlementKind.Tributary, s.kind);
        Assert.AreEqual(capital.id, s.parent);
        Assert.AreSame(capital, WorldTributaries.HubOf(map, s));
        Assert.AreEqual(capital.id, s.governedBy, "it answers to the hub it serves");
        Assert.IsTrue(WorldTributaries.IsGeneralist(tr, s), "every tributary begins as the generalist");
        Assert.AreEqual(tr.generalist, WorldTributaries.DistrictOf(tr, s).id);
        Assert.IsTrue(map.Routes.Any(r => r.from == s.id && r.to == capital.id), "founded with its road home");
        Assert.IsTrue(WorldCivilization.Networked(map).Contains(s.id), "so it joins the Capital's roads");
        Assert.IsTrue(map.Get(s.coord).tradeNode, "and stands as a Trade Node");
        Assert.AreEqual(SeatKind.Tributary, map.territory.OfSettlement(s.id).kind, "a seat of territorial pull");
        Assert.Greater(map.territory.OfSettlement(s.id).capacity, 0f, "that adds Administrative Capacity");
        StringAssert.StartsWith("A tributary is too small", WorldCivilization.WhyNotAnchor(s));
        Assert.AreEqual(1, WorldTributaries.Count(map));
        Assert.AreEqual(1f + tr.costGrowth, WorldTributaries.CostScale(map, tr), 1e-5f, "each one standing makes the next dearer");

        // Its hub has only so many places.
        tr.capitalSlots = 1;
        var next = Sites(map, settings, rules, capital).FirstOrDefault(t => HexCoord.Distance(t.coord, s.coord) >= tr.spacing);
        if (next != null) StringAssert.Contains("keeps all the tributaries it can", WorldTributaries.WhyNotFound(map, settings, tr, next.coord, capital));
        Assert.AreEqual(1 + (int)(capital.development / tr.developmentPerSlot), tr.Slots(SettlementKind.Capital, capital.development));
        Assert.AreEqual(0, tr.Slots(SettlementKind.Outpost, 100f), "an Outpost keeps none");
    }

    // ===== GROWTH =====

    [Test]
    public void Tributaries_DevelopTowardTheirGroundsDesirabilityNeverFarAboveTheirHubAndLendItDevelopment()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var capital = WorldCivilization.Capital(map);
        var s = Raise(map, settings, rules);

        float desire = WorldDesirability.Of(map, map.Get(s.coord));
        Assert.AreEqual(Math.Min(100f * desire, capital.development + tr.aboveHub), WorldTributaries.Target(map, tr, s), 1e-3f);
        capital.development = 0f;
        Assert.LessOrEqual(WorldTributaries.Target(map, tr, s), tr.aboveHub + 1e-4f, "it never outgrows its hub by much");

        WorldCivilization.Tick(map, rules, 100000);
        Assert.AreEqual(WorldTributaries.Target(map, tr, s), s.development, 1e-2f, "it settles at its target");

        s.development = 60f;
        var b = WorldCivilization.Breakdown(map, rules, capital);
        var outskirts = b.terms.FirstOrDefault(x => x.term == DevelopmentTerm.Outskirts);
        Assert.AreEqual(WorldTributaries.HubDevelopment(map, tr, capital), outskirts.points, 1e-4f, "its hub counts its outskirts");
        Assert.Greater(outskirts.points, 0f);
        Assert.LessOrEqual(WorldTributaries.HubDevelopment(map, tr, capital), tr.hubDevelopmentCap);

        // The generalist houses the Capital's people, more on higher development.
        int housing = WorldTributaries.Housing(map, tr);
        Assert.AreEqual((int)Math.Floor(2f * 6f * WorldTributaries.Scale(map, tr, s) + 1e-4f), housing);
        s.development = 20f;
        Assert.Less(WorldTributaries.Housing(map, tr), housing);
    }

    // ===== DISTRICTS =====

    [Test]
    public void Districts_UpgradeFromTheGeneralistAndRespecializingCostsMoreAndRebuilds()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var s = Raise(map, settings, rules);
        s.development = 12f;

        Assert.AreEqual("No such district.", WorldTributaries.WhyNotSpecialize(tr, s, "palace"));
        StringAssert.Contains("already", WorldTributaries.WhyNotSpecialize(tr, s, tr.generalist));
        StringAssert.Contains("needs development", WorldTributaries.WhyNotSpecialize(tr, s, "militant"), "a Militant District needs a larger tributary");
        Assert.IsNull(WorldTributaries.WhyNotSpecialize(tr, s, "trading"));
        Assert.AreEqual(1f, WorldTributaries.SpecializeCostScale(tr, s, "trading"), "the first upgrade pays the district's price");

        s.development = 40f;
        WorldTributaries.Specialize(map, settings, tr, s, "militant");
        Assert.AreEqual("militant", s.district);
        Assert.AreEqual(40f, s.development, 1e-4f, "an upgrade from the generalist keeps its development");
        Assert.IsTrue(WorldTributaries.Outfits(tr, s), "a Militant District outfits expeditions");
        Assert.GreaterOrEqual(WorldTributaries.ExpeditionSlots(map, tr), 1, "and adds an expedition slot");
        Assert.AreEqual(tr.respecializeCost, WorldTributaries.SpecializeCostScale(tr, s, "auric"), 1e-5f, "changing it again costs more");
        Assert.AreEqual(0f, WorldTributaries.SpecializeCostScale(tr, s, tr.generalist), "returning to the generalist is free");

        WorldTributaries.Specialize(map, settings, tr, s, "auric");
        Assert.AreEqual(40f * tr.respecializeKeeps, s.development, 1e-4f, "the quarter is rebuilt");
        Assert.IsFalse(WorldTributaries.Outfits(tr, s));
        Assert.AreEqual(0, WorldTributaries.ExpeditionSlots(map, tr));
        var research = WorldTributaries.Yields(map, tr).Single(y => y.resource == "Research");
        Assert.AreEqual(0.03f * s.development / 10f * WorldTributaries.Scale(map, tr, s), research.amount, 1e-5f, "yields per 10 development x (1 + adjacency)");
        Assert.IsNotEmpty(WorldTributaries.EffectLines(map, tr, s));

        foreach (var d in tr.AllDistricts)
        {
            Assert.IsFalse(string.IsNullOrEmpty(d.name) || string.IsNullOrEmpty(d.role) || string.IsNullOrEmpty(d.description), $"{d.id} is described");
            Assert.AreSame(d, tr.District(d.id.ToUpperInvariant()), "found by id, ignoring case");
        }
        Assert.AreEqual(11, tr.AllDistricts.Count(), "a generalist and one district for each of the ten Enclave categories");
    }

    [Test]
    public void Districts_AdjacencyReadsTheGroundAroundAndTheLinkedDistrictsAndIsBounded()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var s = Raise(map, settings, rules);
        s.development = 50f;
        WorldTributaries.Specialize(map, settings, tr, s, "auric");
        var area = HexCoord.Spiral(s.coord, tr.adjacencyRadius).Select(map.Get).Where(t => t != null).ToList();
        foreach (var t in area) { t.leylines = 0; t.junction = 0; t.sacred = false; t.coherence = 0f; t.magicFertility = 0f; t.elevation = 0.3f; t.escarpment = 0f; }
        float bare = WorldTributaries.AdjacencyTotal(map, tr, s);

        area[0].leylines = 1;
        area[1].coherence = 1f;
        float lined = WorldTributaries.AdjacencyTotal(map, tr, s);
        Assert.Greater(lined, bare, "an Auric District thrives on leylines and Coherence");
        Assert.IsTrue(WorldTributaries.Adjacency(map, tr, s).Any(a => a.what == "a leyline"));
        Assert.AreEqual(1f + lined, WorldTributaries.Scale(map, tr, s), 1e-5f);

        // The same ground read as another district: a preview before upgrading.
        Assert.AreNotEqual(lined, WorldTributaries.AdjacencyTotal(map, tr, s, tr.District("agromagical")));

        // A linked district two cells away counts: Militant drill yards trouble the Auric scholars.
        var other = map.Tiles.Where(t => HexCoord.Distance(t.coord, s.coord) == tr.linkDistance && !t.water && !t.impassable && t.settlement < 0 && t.enclave < 0)
            .OrderBy(t => t.index).First();
        map.Settlements.Add(new Settlement { id = map.Settlements.Max(x => x.id) + 1, name = "Drill Yard", kind = SettlementKind.Tributary, coord = other.coord, parent = s.parent, district = "militant", development = 30f });
        WorldCivilization.Rebuild(map, settings);
        CollectionAssert.Contains(WorldTributaries.Linked(map, tr, s).Select(x => x.name).ToList(), "Drill Yard");
        Assert.Less(WorldTributaries.AdjacencyTotal(map, tr, s), lined, "a linked Militant District lowers an Auric District's adjacency");
        Assert.IsTrue(WorldTributaries.Adjacency(map, tr, map.Settlements.Last()).Any(a => a.what.StartsWith("linked", StringComparison.Ordinal)), "and each counts toward the other");

        tr.maxAdjacency = 0.05f;
        foreach (var t in area) { t.leylines = 1; t.junction = 3; t.coherence = 1f; t.magicFertility = 1f; t.sacred = true; }
        Assert.AreEqual(0.05f, WorldTributaries.AdjacencyTotal(map, tr, s), 1e-5f, "never beyond its bound");
    }

    [Test]
    public void Districts_AMilitantDistrictWardsOffDangerHoldsItsLandAndShieldsTheFieldsBesideIt()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var s = Raise(map, settings, rules);
        s.development = 30f;
        WorldTributaries.Specialize(map, settings, tr, s, "militant");
        var spec = tr.District("militant");
        float ward = WorldTributaries.WardOf(map, tr, s);
        Assert.Greater(ward, 0f);
        Assert.LessOrEqual(ward, 0.9f);

        var near = HexCoord.Spiral(s.coord, spec.wardRadius).Select(map.Get).Where(t => t != null).ToList();
        foreach (var t in near) t.danger = 0.5f;
        WorldTributaries.Ward(map);
        Assert.IsTrue(near.All(t => Math.Abs(t.danger - 0.5f * (1f - ward)) < 1e-4f), "its watch lowers the danger around it");
        var held = WorldTributaries.HeldFast(map);
        Assert.IsTrue(near.All(t => held.Contains(t.index)), "and the land it watches never slips away");
        Assert.Greater(WorldTributaries.PullScale(map, tr, s), 1f, "its walls pull harder");

        // An Agromagical District reads the danger left after the watch.
        var fields = new Settlement { id = 99, name = "Fields", kind = SettlementKind.Tributary, coord = s.coord, district = "agromagical", development = 30f };
        foreach (var t in near) t.danger = 0.5f;
        float exposed = WorldTributaries.AdjacencyTotal(map, tr, fields);
        WorldTributaries.Ward(map);
        Assert.Greater(WorldTributaries.AdjacencyTotal(map, tr, fields), exposed, "a Militant District beside the fields shields them");
    }

    [Test]
    public void Districts_FollowTheTenEnclaveCategoriesAndThePillarDistrictsRaiseTheirPillar()
    {
        var tr = new TributaryRules();
        foreach (EnclaveFamily family in Enum.GetValues(typeof(EnclaveFamily)))
            Assert.AreEqual(1, tr.AllDistricts.Count(d => d.enclave == family.ToString()), $"one district follows the {family} Enclave");
        Assert.IsTrue(string.IsNullOrEmpty(tr.District(tr.generalist).enclave), "the hamlet follows none");
        foreach (var (district, pillar) in new[] { ("auric", "Aureus"), ("weaver", "Waltz"), ("regal", "Regalia"), ("esoteric", "Chorus") })
            Assert.IsTrue(tr.District(district).stats.Any(st => st.stat == pillar), $"the {district} district raises {pillar}");
        foreach (var d in tr.AllDistricts)
            foreach (var st in d.stats) Assert.IsTrue(WorldTributaries.StatEffectType(st.stat).HasValue, $"{d.id} raises a real stat ({st.stat})");
        Assert.AreEqual(GameEffectType.MoraleModifier, WorldTributaries.StatEffectType("morale"));
        Assert.AreEqual(GameEffectType.SubstatBonus, WorldTributaries.StatEffectType("Ambition"));
        Assert.AreEqual(GameEffectType.PillarBonus, WorldTributaries.StatEffectType("Aureus"));

        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var s = Raise(map, settings, rules);
        s.development = 100f;
        WorldTributaries.Specialize(map, settings, rules.tributaries, s, "auric");
        int expected = (int)Math.Floor(2f * WorldTributaries.Scale(map, rules.tributaries, s) + 1e-4f);
        Assert.GreaterOrEqual(expected, 1);
        var effect = WorldTributaries.StatEffects(map, rules.tributaries).Single();
        Assert.AreEqual(GameEffectType.PillarBonus, effect.effect.type);
        Assert.AreEqual("aureus", effect.effect.target);
        Assert.AreEqual(expected, effect.effect.value, 1e-4f, "one point of Aureus per 50 development, x (1 + adjacency)");
        s.development = 10f;
        Assert.IsEmpty(WorldTributaries.StatEffects(map, rules.tributaries), "a small quarter raises no whole point yet");
        StringAssert.StartsWith("an Auric District", WorldTributaries.Article("Auric District"));
    }

    [Test]
    public void Districts_KindredEnclavesNearbyAddToAdjacencyAndSuzeraintyCountsTwice()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        var tr = rules.tributaries;
        var s = Raise(map, settings, rules);
        s.development = 40f;
        WorldTributaries.Specialize(map, settings, tr, s, "trading");
        map.Enclaves.RemoveAll(e => HexCoord.Distance(e.coord, s.coord) <= tr.enclaveReach);
        float alone = WorldTributaries.AdjacencyTotal(map, tr, s);

        var traders = new Enclave { index = map.Enclaves.Count, name = "Test Traders", family = EnclaveFamily.Trading, coord = s.coord };
        map.Enclaves.Add(traders);
        Assert.AreEqual(alone + 0.3f, WorldTributaries.AdjacencyTotal(map, tr, s), 1e-4f, "a kindred Trading enclave within reach");
        traders.suzerain = true;
        Assert.AreEqual(alone + 0.6f, WorldTributaries.AdjacencyTotal(map, tr, s), 1e-4f, "held in Suzerainty it counts twice");
        traders.family = EnclaveFamily.Militant;
        Assert.AreEqual(alone, WorldTributaries.AdjacencyTotal(map, tr, s), 1e-4f, "another category is no kin");
        var regal = tr.District("regal");
        float courted = WorldTributaries.AdjacencyTotal(map, tr, s, regal);
        Assert.IsTrue(WorldTributaries.Adjacency(map, tr, s, regal).Any(a => a.what.StartsWith("enclaves within reach", StringComparison.Ordinal)));
        map.Enclaves.Remove(traders);
        Assert.AreEqual(WorldTributaries.AdjacencyTotal(map, tr, s, regal) + 0.3f, courted, 1e-4f, "a Regal District's envoys count every enclave within reach, whatever its category");
    }

    // ===== ORPHANS =====

    [Test]
    public void Orphans_RejoinTheNearestHubWhenTheirsIsLostAndLayARoadToIt()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        map.settlementRules = rules;
        var capital = WorldCivilization.Capital(map);
        var townSite = map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && !t.water && !t.impassable && t.enclave < 0 && HexCoord.Distance(t.coord, capital.coord) >= rules.spacing)
            .OrderBy(t => HexCoord.Distance(t.coord, capital.coord)).ThenBy(t => t.index).First();
        map.Explore(townSite.coord);
        var town = WorldCivilization.Found(map, settings, rules, townSite.coord, SettlementKind.Town, 0);
        var site = Sites(map, settings, rules, town).First(t => HexCoord.Distance(t.coord, town.coord) < HexCoord.Distance(t.coord, capital.coord));
        var s = WorldTributaries.Found(map, settings, rules, site.coord, town, 0);
        Assert.AreSame(town, WorldTributaries.HubOf(map, s));
        map.rehomed.Clear();

        // The town is lost to your authority: it is no longer a hub.
        town.detached = true;
        Assert.IsNull(WorldTributaries.HubOf(map, s), "a detached settlement is no hub");
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(capital.id, s.parent, "it rejoins the nearest hub still standing");
        Assert.AreSame(capital, WorldTributaries.HubOf(map, s));
        Assert.AreEqual(capital.id, s.governedBy);
        CollectionAssert.Contains(map.rehomed, (s.id, capital.id), "and the move is kept for the notice");
        Assert.IsTrue(WorldCivilization.Networked(map).Contains(s.id), "a road leads it back to its new hub");
        Assert.AreEqual("Outskirt Hamlet", WorldTributaries.DistrictOf(rules.tributaries, s).name, "it keeps its district");

        // Nothing moves while its hub stands.
        map.rehomed.Clear();
        int routes = map.Routes.Count;
        WorldCivilization.Rebuild(map, settings);
        Assert.IsEmpty(map.rehomed);
        Assert.AreEqual(routes, map.Routes.Count);

        // With no hub left at all it waits where it is.
        capital.detached = true;
        Assert.IsNull(WorldTributaries.NearestHub(map, rules.tributaries, s));
        Assert.IsEmpty(WorldTributaries.Rehome(map, settings, rules));
        Assert.AreEqual(capital.id, s.parent);
        capital.detached = false;
    }
}
