using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Damage, loss and ruins (<see cref="WorldRuins"/>), with no scene: danger and withering harm settlements, safety heals
/// them, the fallen become ruins that stay on the map (never the Capital), and a ruin is read once for what its failure
/// left behind. Uses the small catalog of <see cref="WorldGenerationTests"/>.
/// </summary>
public class WorldRuinTests
{
    private static WorldGenSettings Settings() => WorldGenerationTests.Settings();

    private static WorldMap Fresh(int seed) => WorldGenerator.Generate(seed, Settings(), WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    // A tributary of the Capital on the nearest free held cell.
    private static Settlement Tributary(WorldMap map, WorldGenSettings settings, SettlementRules rules)
    {
        var capital = WorldCivilization.Capital(map);
        foreach (var t in map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && HexCoord.Distance(t.coord, capital.coord) <= rules.tributaries.hubReach).ToList())
            map.Explore(t.coord);
        var site = map.Tiles.Where(t => WorldTributaries.WhyNotFound(map, settings, rules.tributaries, t.coord, capital) == null)
            .OrderBy(t => HexCoord.Distance(t.coord, capital.coord)).ThenBy(t => t.index).First();
        return WorldTributaries.Found(map, settings, rules, site.coord, capital, 0);
    }

    [Test]
    public void Composure_SettlementsReadTheLegendsFiveStatesAndStrainEasesTowardTheBaseline()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        map.settlementRules = rules;
        var loss = rules.loss;
        var t = loss.composure;
        var s = Tributary(map, settings, rules);
        s.development = 40f;
        var tile = map.Get(s.coord);

        // The same five states at the same thresholds as a legend's Soul Leitmotif.
        foreach (float strain in new[] { 0f, t.cloudedAt, t.fracturedAt, t.spiralingAt - 0.1f, t.surrenderAt })
        {
            s.strain = strain;
            Assert.AreEqual(ComposureRules.StateOf(strain, t), WorldRuins.StateOf(loss, s));
        }
        s.strain = t.fracturedAt + 1f;
        Assert.AreEqual(t.fracturedCouncil, WorldRuins.Output(loss, s), 1e-5f, "a Fractured settlement yields as a Fractured legend serves");
        s.strain = t.spiralingAt + 1f;
        Assert.AreEqual(t.spiralingCouncil, WorldRuins.Output(loss, s), 1e-5f);
        s.strain = t.baseline;
        Assert.AreEqual(1f, WorldRuins.Output(loss, s), 1e-5f, "Clouded is whole");

        tile.danger = 0f;
        Assert.IsEmpty(WorldRuins.Harm(map, loss, s), "nothing strains it on safe ground, joined to its hub");
        tile.danger = 0.9f;
        var harm = WorldRuins.Harm(map, loss, s).Single();
        Assert.AreEqual(WorldRuins.Danger, harm.cause);
        Assert.AreEqual(loss.dangerStrain * (0.9f - loss.dangerFloor) / (1f - loss.dangerFloor), harm.perSeventh, 1e-4f);
        bool networked = WorldCivilization.Networked(map).Contains(s.id);
        Assert.IsTrue(networked, "on the Capital's roads");
        float recovery = WorldRuins.Recovery(loss, s, networked);
        Assert.AreEqual(t.restRecovery * loss.networkRecovery, recovery, 1e-5f);
        float expected = ComposureRules.Ease(ComposureRules.Ease(t.baseline, harm.perSeventh, recovery, t), harm.perSeventh, recovery, t);
        WorldRuins.Tick(map, settings, loss, 2, 0);
        Assert.AreEqual(expected, s.strain, 1e-3f, "the same step as a legend's: strain added, then eased toward the baseline");
        Assert.AreEqual(40f - (expected - t.baseline) * loss.developmentPerStrain, s.development, 1e-3f, "strain wears City Development down");
        Assert.AreEqual(WorldRuins.Danger, s.harmedBy);

        tile.danger = 0f;
        float strained = s.strain;
        WorldRuins.Tick(map, settings, loss, 1, 0);
        Assert.AreEqual(Math.Max(t.baseline, strained - recovery), s.strain, 1e-3f, "it eases toward the Clouded baseline while safe");

        // Its road home cut: withering strains its Composure.
        map.Routes.RemoveAll(r => r.from == s.id || r.to == s.id);
        Assert.IsTrue(WorldRuins.IsWithering(map, s));
        Assert.AreEqual(loss.witherStrain, WorldRuins.Harm(map, loss, s).Single(h => h.cause == WorldRuins.Withering).perSeventh, 1e-5f);
        Assert.IsFalse(WorldRuins.IsWithering(map, WorldCivilization.Capital(map)), "only tributaries wither");

        // Pillage.
        s.strain = t.baseline;
        Assert.IsFalse(WorldRuins.Strain(loss, s, 30f, WorldRuins.Pillage));
        Assert.AreEqual(t.baseline + 30f, s.strain, 1e-4f);
        Assert.AreEqual(ComposureState.Fractured, WorldRuins.StateOf(loss, s));
        Assert.AreEqual(WorldRuins.Pillage, s.harmedBy);
        Assert.IsTrue(WorldRuins.Strain(loss, s, 500f, WorldRuins.Pillage), "Surrender");
        Assert.AreEqual(t.surrenderAt, s.strain, 1e-4f);
        Assert.AreEqual((float)Math.Ceiling((t.surrenderAt - t.baseline) / 10f), WorldRuins.MendScale(loss, s), "mending is paid per 10 points above the baseline");
    }

    [Test]
    public void Fall_TheSurrenderedBecomeRuinsThatStayOnTheMapAndTheCapitalSpiralsButNeverSurrenders()
    {
        var map = Fresh(42);
        var settings = Settings();
        var rules = new SettlementRules();
        map.settlementRules = rules;
        var loss = rules.loss;
        var s = Tributary(map, settings, rules);
        s.development = 30f;
        s.district = "auric";
        var coord = s.coord;
        s.strain = loss.composure.surrenderAt - 0.05f;
        map.Get(coord).danger = 1f;

        var result = WorldRuins.Tick(map, settings, loss, 1, 2);
        var ruin = result.fallen.Single();
        CollectionAssert.DoesNotContain(map.Settlements, s, "it is no longer a settlement");
        Assert.AreEqual(-1, map.Get(coord).settlement);
        Assert.AreSame(ruin, WorldRuins.At(map, coord), "its ruins stay on the map");
        Assert.AreEqual(SettlementKind.Tributary, ruin.kind);
        Assert.AreEqual("auric", ruin.district);
        Assert.AreEqual(2, ruin.fallenAge);
        Assert.AreEqual(WorldRuins.Danger, ruin.cause);
        Assert.IsFalse(ruin.detached || ruin.ancient);
        Assert.IsTrue(map.Routes.Any(r => r.from == s.id), "its road stays");
        Assert.AreSame(ruin, WorldRuins.Reclaimable(map, coord), "one of yours: founding there reclaims it");

        var capital = WorldCivilization.Capital(map);
        capital.strain = loss.composure.surrenderAt - 1f;
        map.Get(capital.coord).danger = 1f;
        var again = WorldRuins.Tick(map, settings, loss, 10, 2);
        Assert.IsEmpty(again.fallen, "the Capital never Surrenders");
        Assert.AreEqual(ComposureState.Spiraling, WorldRuins.StateOf(loss, capital), "it Spirals at worst");
        Assert.AreEqual(loss.composure.spiralingCouncil, WorldRuins.CapitalOutput(map, loss), 1e-4f, "and the realm yields less");
        Assert.IsNull(WorldRuins.Fall(map, settings, capital, WorldRuins.Pillage, 2));
        CollectionAssert.Contains(map.Settlements, capital);
    }

    [Test]
    public void OldWorld_RuinsAndBrokenRoadsAreLaidFromTheSeedAndWalkedFasterThanOpenGround()
    {
        var settings = Settings();
        var rules = new SettlementRules();
        var old = rules.loss.oldWorld;
        var map = Fresh(42);
        WorldRuins.PlaceOldWorld(map, settings, old);
        var ancient = map.Ruins.Where(r => r.ancient).ToList();
        Assert.IsNotEmpty(ancient, "the world ended once already");
        Assert.LessOrEqual(ancient.Count, old.ruins);
        Assert.AreEqual(1, ancient.Count(r => r.kind == SettlementKind.Major), "one of them was a city");
        foreach (var r in ancient)
        {
            var t = map.Get(r.coord);
            Assert.IsFalse(t.water || t.impassable);
            Assert.GreaterOrEqual(map.StepsFromCapital(r.coord), old.capitalDistance);
            Assert.IsTrue(ancient.Where(o => o != r).All(o => HexCoord.Distance(o.coord, r.coord) >= old.spacing));
            Assert.AreEqual(WorldRuins.Cataclysm, r.cause);
            Assert.IsNull(WorldRuins.Reclaimable(map, r.coord), "the Old World is no one's to reclaim");
            Assert.IsTrue(r.development >= old.minDevelopment && r.development <= old.maxDevelopment);
        }
        Assert.IsNotEmpty(map.OldRoads, "broken roads join them");
        Assert.IsTrue(map.OldRoads.All(road => road.All(c => map[c].oldRoad)));

        var again = Fresh(42);
        WorldRuins.PlaceOldWorld(again, settings, old);
        CollectionAssert.AreEqual(ancient.Select(r => r.coord).ToList(), again.Ruins.Select(r => r.coord).ToList(), "the same seed, the same Old World");
        CollectionAssert.AreEqual(map.Tiles.Where(t => t.oldRoad).Select(t => t.index).ToList(), again.Tiles.Where(t => t.oldRoad).Select(t => t.index).ToList());

        var cell = map.Tiles.First(t => t.oldRoad && !t.road);
        float onOld = WorldPaths.StepCost(cell, settings);
        cell.oldRoad = false;
        float off = WorldPaths.StepCost(cell, settings);
        cell.oldRoad = true;
        Assert.AreEqual(off * WorldPaths.OldRoadFactor, onOld, 1e-4f, "walking an old road beats open ground");
        Assert.Greater(WorldPaths.OldRoadFactor, WorldPaths.RoadFactor, "but not a road of yours");
        Assert.IsTrue(WorldRuins.Findings(map, rules, ancient[0]).resources.Any(), "its ruins can be read");
        Assert.AreEqual(WorldRuins.EnlightenChance(rules.loss, new Ruin { development = ancient[0].development }) + rules.loss.oldWorldEnlighten, WorldRuins.EnlightenChance(rules.loss, ancient[0]), 1e-5f, "the Old World knew more");
    }

    [Test]
    public void Roads_RestoringOldRoadIsCheapButALesserRoadUntilRebuilt()
    {
        var settings = Settings();
        var rules = new SettlementRules();
        var old = rules.loss.oldWorld;
        var map = Fresh(42);
        map.settlementRules = rules;
        WorldRuins.PlaceOldWorld(map, settings, old);
        var line = map.OldRoads.OrderByDescending(r => r.Count).First();
        var path = line.Where(c => map[c].settlement < 0 && !map[c].road).Take(6).ToList();
        Assert.GreaterOrEqual(path.Count, 3);
        var (restored, fresh) = WorldRuins.RoadCells(map, path);
        Assert.AreEqual(path.Count, restored);
        Assert.AreEqual(0, fresh);
        Assert.AreEqual(path.Count * old.restoreShare, WorldRuins.RoadCostScale(map, old, path), 1e-4f, "restoring costs a share of building");

        foreach (int c in path) map[c].coherence = 0.8f;
        var capital = WorldCivilization.Capital(map);
        var route = WorldCivilization.BuildRoad(map, settings, rules, capital, path, capital.id);
        Assert.AreEqual(path.Count, WorldRuins.RestoredCount(route));
        Assert.IsTrue(path.All(c => map[c].road && map[c].restoredRoad));
        Assert.IsEmpty(route.nodes, "no Trade Node on a restored stretch");
        Assert.AreEqual(0.3f, CityDevelopment.TileScore(map, map[path[0]], DevelopmentTerm.Trade), 1e-5f, "worth half a rebuilt road");
        float lesser = route.efficiency;
        Assert.AreEqual(path.Count * (1f - old.restoreShare), WorldRuins.RebuildScale(old, route), 1e-4f, "rebuilding pays the rest");
        WorldCivilization.RebuildRestored(map, settings, route);
        Assert.AreEqual(0, WorldRuins.RestoredCount(route));
        Assert.IsTrue(path.All(c => map[c].road && !map[c].restoredRoad));
        Assert.Greater(route.efficiency, lesser, "a rebuilt road carries trade in full");
        Assert.AreEqual(0.6f, CityDevelopment.TileScore(map, map[path[0]], DevelopmentTerm.Trade), 1e-5f);
        Assert.AreEqual((0f, 0), (WorldRuins.RoadCostScale(map, old, path), WorldRuins.RoadCells(map, path).restored), "a road already there costs nothing");
    }

    [Test]
    public void Reclaiming_LostLandIsRememberedUntilItComesBack()
    {
        var lost = new List<int>();
        Assert.AreEqual(0, WorldRuins.TrackLand(new[] { 1, 2, 3 }, new[] { 1, 2 }, lost));
        CollectionAssert.AreEqual(new[] { 3 }, lost, "cell 3 slipped away");
        Assert.AreEqual(0, WorldRuins.TrackLand(new[] { 1, 2 }, new[] { 1, 2, 9 }, lost), "new land is not reclaimed land");
        CollectionAssert.AreEqual(new[] { 3 }, lost);
        Assert.AreEqual(1, WorldRuins.TrackLand(new[] { 1, 2, 9 }, new[] { 1, 2, 3, 9 }, lost), "cell 3 is yours again");
        CollectionAssert.IsEmpty(lost);
        Assert.AreEqual(3, new SettlementRules().loss.eraReclaim, "+3 Era Score");
    }

    [Test]
    public void EraTimeline_OrdersAwardsByCycleEchoPhaseAndSeventhAndGroupsThemByAge()
    {
        Assert.AreEqual(0, EraTimeline.Stamp(1, 1, 1, 1));
        Assert.AreEqual(TimeSystemLogic.SeventhsPerPhase, EraTimeline.Stamp(1, 1, 2, 1), "a Phase is 21 Sevenths");
        Assert.AreEqual(TimeSystemLogic.SeventhsPerPhase * TimeSystemLogic.PhasesPerEcho, EraTimeline.Stamp(1, 2, 1, 1), "an Echo is 3 Phases");
        Assert.AreEqual(TimeSystemLogic.SeventhsPerPhase * TimeSystemLogic.PhasesPerEcho * TimeSystemLogic.EchoesPerCycle, EraTimeline.Stamp(2, 1, 1, 1), "a Cycle is 4 Echoes");

        var awards = new List<EraAward>
        {
            new EraAward { points = 2, reason = "Founded a town", ageId = "a0", ageTitle = "Age of Desolation", cycle = 1, echo = 2, phase = 1, seventh = 4, order = 2 },
            new EraAward { points = 1, reason = "Found a landmark", ageId = "a0", ageTitle = "Age of Desolation", cycle = 1, echo = 1, phase = 3, seventh = 9, order = 0 },
            new EraAward { points = 3, reason = "Reclaimed lost land", ageId = "a1", ageTitle = "Age of Renewal", ageNumber = 1, cycle = 2, echo = 1, phase = 1, seventh = 1, order = 3 },
            new EraAward { points = 1, reason = "Same Seventh, later", ageId = "a0", ageTitle = "Age of Desolation", cycle = 1, echo = 1, phase = 3, seventh = 9, order = 1 },
        };
        CollectionAssert.AreEqual(new[] { "Found a landmark", "Same Seventh, later", "Founded a town", "Reclaimed lost land" }, EraTimeline.Chronological(awards).Select(a => a.reason).ToList());
        var sections = EraTimeline.Sections(awards);
        Assert.AreEqual(2, sections.Count);
        StringAssert.StartsWith("Age of Desolation: +4", sections[0].heading);
        Assert.AreEqual(2, sections[0].echoes.Count, "grouped by Cycle and Echo");
        StringAssert.StartsWith("Cycle 1, Echo 1", sections[0].echoes[0].echo);
        StringAssert.StartsWith("Phase 3, Seventh 9: +1 Found a landmark", sections[0].echoes[0].lines[0]);
        StringAssert.StartsWith("Age of Renewal: +3", sections[1].heading);
    }

    [Test]
    public void Fall_ATributaryWhoseHubFallsRejoinsAnotherOrWithers()
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
        foreach (var t in map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && HexCoord.Distance(t.coord, town.coord) <= rules.tributaries.hubReach).ToList()) map.Explore(t.coord);
        var site = map.Tiles.Where(t => WorldTributaries.WhyNotFound(map, settings, rules.tributaries, t.coord, town) == null && HexCoord.Distance(t.coord, town.coord) < HexCoord.Distance(t.coord, capital.coord))
            .OrderBy(t => t.index).First();
        var s = WorldTributaries.Found(map, settings, rules, site.coord, town, 0);

        WorldRuins.Fall(map, settings, town, WorldRuins.Danger, 1);
        Assert.AreSame(capital, WorldTributaries.HubOf(map, s), "its hub fell: it rejoins the nearest hub");
        Assert.IsFalse(WorldRuins.IsWithering(map, s), "joined to it by road, it does not wither");
        Assert.IsNotNull(WorldRuins.At(map, town.coord));
    }

    [Test]
    public void Ruins_AreReadOnceForSalvageResearchAndSometimesAnEnlightenmentOrACivic()
    {
        var map = Fresh(42);
        var rules = new SettlementRules();
        var loss = rules.loss;
        var ruin = new Ruin { id = 3, name = "Old Lyceum", kind = SettlementKind.Tributary, district = "auric", development = 40f };
        Assert.IsNull(WorldRuins.WhyNotInvestigate(ruin));

        var findings = WorldRuins.Findings(map, rules, ruin);
        float Of(string resource) => findings.resources.Where(a => a.resource == resource).Sum(a => a.amount);
        Assert.AreEqual(loss.researchBase + loss.researchPerDevelopment * 40f + 0.03f * 4f * loss.salvageSeconds, Of("Research"), 0.1f, "its records, plus its former production");
        Assert.AreEqual(0.02f * 4f * loss.salvageSeconds, Of("Faith"), 0.1f, "an Auric ruin salvages Faith");
        Assert.AreEqual(10f, Of("Elderwood"), 0.1f, "and the base salvage");
        var again = WorldRuins.Findings(map, rules, ruin);
        Assert.AreEqual(findings.enlighten, again.enlighten, "the same ruin always holds the same");
        Assert.AreEqual(findings.civic, again.civic);
        Assert.AreEqual(findings.pick, again.pick);

        Assert.AreEqual(loss.enlightenChance + loss.enlightenPerDevelopment * 40f, WorldRuins.EnlightenChance(loss, ruin), 1e-5f);
        Assert.AreEqual(loss.civicChance + loss.civicCultureBonus, WorldRuins.CivicChance(loss, rules, ruin), 1e-5f, "an Auric district's ways are likelier to survive");
        var town = new Ruin { kind = SettlementKind.Town };
        Assert.AreEqual(loss.civicChance + loss.civicSettlementBonus, WorldRuins.CivicChance(loss, rules, town), 1e-5f);

        // Over many ruins the finds come about as often as their chances say.
        int civics = 0, lights = 0, n = 400;
        for (int i = 0; i < n; i++)
        {
            var f = WorldRuins.Findings(map, rules, new Ruin { id = i, kind = SettlementKind.Town, development = 40f });
            if (f.civic) civics++;
            if (f.enlighten) lights++;
        }
        Assert.AreEqual(loss.civicChance + loss.civicSettlementBonus, civics / (float)n, 0.08f);
        Assert.AreEqual(WorldRuins.EnlightenChance(loss, ruin), lights / (float)n, 0.08f);

        ruin.investigated = true;
        StringAssert.Contains("given up", WorldRuins.WhyNotInvestigate(ruin), "a ruin is read once");
        Assert.AreEqual("b", WorldRuins.Pick(new[] { "a", "b", "c" }, 0.5));
        Assert.AreEqual("c", WorldRuins.Pick(new[] { "a", "b", "c" }, 1.0));
        Assert.IsNull(WorldRuins.Pick(new string[0], 0.3));
        Assert.AreEqual(UnitTask.Investigate, UnitAbilities.For(UnitTask.Investigate).task);
    }

    [Test]
    public void Stories_PillageSettlementsThroughTheSettlementConsequence()
    {
        var problems = new List<string>();
        var pillage = EventScript.ParseConsequence("settlement:capital -20", problems);
        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual(EventConsequence.ConsequenceType.SettlementDamage, pillage.type);
        Assert.AreEqual("capital", pillage.targetName);
        Assert.AreEqual(-20, pillage.value);
        var repair = EventScript.ParseConsequence("settlement:Capital Outskirts I +15", problems);
        Assert.AreEqual("Capital Outskirts I", repair.targetName);
        Assert.AreEqual(15, repair.value);
    }
}
