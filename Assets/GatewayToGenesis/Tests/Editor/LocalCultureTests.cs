using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Local cultures and routes of exchange (T02) with no scene: settlement profiles, contact from real roads
/// (<see cref="ContactSnapshot"/>, <see cref="CulturalContactMap"/>), exposure, participation and adoption
/// (<see cref="LocalCultureRules"/>), visits, founders and arrivals with honest provenance. Pure, so they also run
/// outside Unity.
/// </summary>
public class LocalCultureTests
{
    private const int River = 1, Orchard = 2, Hill = 3;
    private const string RiverGround = " plain grassland river", OrchardGround = " plain auric-orchard orchard";
    private static readonly LocalCultureTuning Tuning = new LocalCultureTuning();

    private static ContactSnapshot Towns(bool road, string blocked = null, bool hill = false)
    {
        var standing = new List<(int, string)> { (River, "Riverford"), (Orchard, "Goldbough") };
        if (hill) standing.Add((Hill, "Highcairn"));
        var routes = road ? new List<ContactRoute> { new ContactRoute(1, River, Orchard, blocked) } : new List<ContactRoute>();
        return ContactSnapshot.Build(standing, routes);
    }

    private static LocalCultureState State() => LocalCultureRules.Ensure(new LocalCultureState());

    private static LocalPractice At(LocalCultureState s, int settlement, string practice) =>
        LocalCultureRules.Practice(LocalCultureRules.Profile(s, settlement), practice);

    // Each town gathers for what its own ground knows: the river town sings at the fords, the orchard town names its first fruit.
    private static LocalCultureState TwoTowns()
    {
        var s = State();
        var r = LocalCultureRules.Gather(s, River, "Riverford", "crossing-songs", RiverGround, 1, "age-of-desolation", 0, Tuning);
        var o = LocalCultureRules.Gather(s, Orchard, "Goldbough", "golden-fruit", OrchardGround, 1, "age-of-desolation", 0, Tuning);
        Assert.AreEqual(PracticeChannel.Originated, r.channel);
        Assert.AreEqual(PracticeChannel.Originated, o.channel);
        return s;
    }

    private static PartyRepertoire Party(LocalCultureState s, int from, string fromName, params string[] practices)
    {
        foreach (var p in practices) LocalCultureRules.Carry(s, 7, "Cultural Party of Vaelia", LocalCultureRules.Profile(s, from), p, 2);
        return LocalCultureRules.Repertoire(s, 7);
    }

    // ===== LOCAL DIFFERENCE =====

    [Test]
    public void TwoSettlements_DevelopDifferentPractices_FromTheirOwnGround()
    {
        var s = TwoTowns();
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, River, "crossing-songs").stage);
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, Orchard, "golden-fruit").stage);
        Assert.IsNull(At(s, River, "golden-fruit"), "the river town has no orchard rite of its own");
        Assert.IsNull(At(s, Orchard, "crossing-songs"));
        Assert.IsNotNull(LocalCultureRules.WhyNotGather(LocalCultureRules.Profile(s, Orchard), "crossing-songs", OrchardGround, 0, 20, Tuning),
            "a custom no one here knows, whose ground is elsewhere, cannot simply be held");
        Assert.AreEqual("Crossing Songs of Riverford", At(s, River, "crossing-songs").variant);
    }

    [Test]
    public void DisconnectedCommunities_NeverExchange()
    {
        var s = TwoTowns();
        for (int seventh = 2; seventh < 200; seventh++) LocalCultureRules.Seventh(s, Towns(road: false), Tuning, seventh, "age-of-desolation", 0);
        Assert.IsNull(At(s, River, "golden-fruit"), "no road, no visit, no arrival: nothing spreads spontaneously");
        Assert.IsNull(At(s, Orchard, "crossing-songs"));
    }

    // ===== ROADS =====

    [Test]
    public void ARoad_GivesExposure_ButAdoptionNeedsParticipation()
    {
        var s = TwoTowns();
        int seventh = 2;
        for (; seventh < 40; seventh++)
        {
            // Each town keeps its own custom alive.
            if (seventh % 7 == 0)
            {
                LocalCultureRules.Gather(s, River, "Riverford", "crossing-songs", RiverGround, seventh, null, 0, Tuning);
                LocalCultureRules.Gather(s, Orchard, "Goldbough", "golden-fruit", OrchardGround, seventh, null, 0, Tuning);
            }
            LocalCultureRules.Seventh(s, Towns(road: true), Tuning, seventh, "age-of-desolation", 0);
        }
        var heard = At(s, Orchard, "crossing-songs");
        Assert.IsNotNull(heard);
        Assert.AreEqual(LocalPracticeStage.Exposed, heard.stage, "exposure alone never makes a custom theirs");
        Assert.GreaterOrEqual(heard.exposure, Tuning.adoptExposure);
        Assert.AreEqual(PracticeChannel.Road, heard.origin.channel);
        Assert.AreEqual(River, heard.origin.fromSettlement, "provenance names the town it came from");
        seventh = 50;
        Assert.IsNull(LocalCultureRules.WhyNotGather(LocalCultureRules.Profile(s, Orchard), "crossing-songs", OrchardGround, 0, seventh, Tuning));
        var change = LocalCultureRules.Gather(s, Orchard, "Goldbough", "crossing-songs", OrchardGround, seventh, null, 0, Tuning);
        Assert.AreEqual(LocalPracticeStage.Practiced, change.stage, "exposure and participation together: they take it up");
        Assert.AreEqual(PracticeChannel.Road, At(s, Orchard, "crossing-songs").origin.channel, "its first provenance is never rewritten");
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, Orchard, "golden-fruit").stage, "and it keeps its own rite: two traditions, not one");
    }

    [Test]
    public void AnInterruptedRoad_StopsFutureContact_ButLearnedCustomsStay()
    {
        var s = TwoTowns();
        var hear = new PracticeProvenance { channel = PracticeChannel.Road, fromSettlement = River, fromName = "Riverford" };
        LocalCultureRules.Expose(s, Orchard, "Goldbough", "crossing-songs", 1f, hear, 0);
        LocalCultureRules.Gather(s, Orchard, "Goldbough", "crossing-songs", OrchardGround, 2, null, 0, Tuning);
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, Orchard, "crossing-songs").stage);
        // The river town begins a new custom after the road is cut: it must not arrive.
        LocalCultureRules.Gather(s, River, "Riverford", "evening-song", RiverGround, 3, null, 0, Tuning);
        var cut = Towns(road: true, blocked: "a threat at the ford");
        Assert.IsFalse(cut.Edges.Single().open);
        Assert.AreEqual("a threat at the ford", cut.Edges.Single().blockedBy);
        for (int seventh = 4; seventh < 120; seventh++) LocalCultureRules.Seventh(s, cut, Tuning, seventh, null, 0);
        Assert.IsNull(At(s, Orchard, "evening-song"), "no contact across an interrupted road");
        var learned = At(s, Orchard, "crossing-songs");
        Assert.IsNotNull(learned, "what was learned is never erased");
        Assert.IsTrue(learned.Kept);
        Assert.AreEqual(LocalPracticeStage.Quiet, learned.stage, "a long silence makes it quiet, not forgotten");
        LocalCultureRules.Gather(s, Orchard, "Goldbough", "crossing-songs", OrchardGround, 120, null, 0, Tuning);
        Assert.AreEqual(LocalPracticeStage.Practiced, learned.stage, "a gathering revives it");
    }

    [Test]
    public void Contact_RunsThroughTheRuinsOfAFallenSettlement_AndPrefersAnOpenWay()
    {
        var standing = new List<(int, string)> { (1, "A"), (3, "C") };
        // 2 has fallen: its roads still join A and C.
        var through = ContactSnapshot.Build(standing, new[] { new ContactRoute(1, 1, 2), new ContactRoute(2, 2, 3) });
        Assert.IsTrue(through.Edge(1, 3).open);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, through.Edge(1, 3).routes);
        var twoWays = ContactSnapshot.Build(standing, new[] { new ContactRoute(1, 1, 3, "held by another authority"), new ContactRoute(2, 1, 2), new ContactRoute(3, 2, 3) });
        Assert.IsTrue(twoWays.Edge(1, 3).open, "one open way is enough");
        var neither = ContactSnapshot.Build(standing, new[] { new ContactRoute(1, 1, 3, "x"), new ContactRoute(2, 1, 2, "y"), new ContactRoute(3, 2, 3) });
        Assert.IsFalse(neither.Edge(1, 3).open);
        Assert.IsEmpty(ContactSnapshot.Build(standing, new ContactRoute[0]).Edges, "no road, no contact (distance means nothing)");
    }

    // ===== VISITS =====

    [Test]
    public void ACompletedVisit_TransfersAttributedExposureOnce()
    {
        var s = TwoTowns();
        Assert.IsNotNull(LocalCultureRules.WhyNotCarry(s, 7, LocalCultureRules.Profile(s, River), "golden-fruit", Tuning), "a party takes up only what the town it stands in keeps");
        var party = Party(s, River, "Riverford", "crossing-songs");
        var legends = new List<string> { "Vaelia", "Oren" };
        var first = LocalCultureRules.Visit(s, "visit:7:2:1", Orchard, "Goldbough", party, legends, 5, "age-of-desolation", 0, Tuning);
        Assert.AreEqual(1, first.Count);
        var p = At(s, Orchard, "crossing-songs");
        Assert.AreEqual(Tuning.visitExposure, p.exposure, 1e-4f);
        Assert.AreEqual(LocalPracticeStage.Exposed, p.stage, "seeing it performed once is not keeping it");
        Assert.AreEqual(PracticeChannel.Visit, p.origin.channel);
        Assert.AreEqual("Cultural Party of Vaelia", p.origin.carrier);
        CollectionAssert.AreEqual(legends, p.origin.legends);
        Assert.AreEqual("Riverford", p.origin.fromName);
        StringAssert.Contains("Cultural Party of Vaelia (Vaelia, Oren) from Riverford", LocalCultureRules.ProvenanceWords(p.origin));
        var again = LocalCultureRules.Visit(s, "visit:7:2:1", Orchard, "Goldbough", party, legends, 5, "age-of-desolation", 0, Tuning);
        Assert.IsEmpty(again, "the same completed visit never counts twice (reload, repeated callback)");
        Assert.AreEqual(Tuning.visitExposure, p.exposure, 1e-4f);
        // The host takes part in its own rite at the festival.
        Assert.Greater(At(s, Orchard, "golden-fruit").participation, Tuning.gatheringParticipation);
    }

    [Test]
    public void TravelersCarryBothTraditions_AndTheOrchardTownLaterSharesTheRiverSong()
    {
        var s = TwoTowns();
        var party = Party(s, River, "Riverford", "crossing-songs");
        LocalCultureRules.Visit(s, "visit:7:2:1", Orchard, "Goldbough", party, new[] { "Vaelia" }, 3, null, 0, Tuning);
        // At the orchard town the party also takes up its rite, and carries both on.
        Assert.IsNull(LocalCultureRules.WhyNotCarry(s, 7, LocalCultureRules.Profile(s, Orchard), "golden-fruit", Tuning));
        LocalCultureRules.Carry(s, 7, "Cultural Party of Vaelia", LocalCultureRules.Profile(s, Orchard), "golden-fruit", 3);
        CollectionAssert.AreEquivalent(new[] { "crossing-songs", "golden-fruit" }, LocalCultureRules.Repertoire(s, 7).carried.Select(c => c.practice));
        LocalCultureRules.Gather(s, River, "Riverford", "evening-song", RiverGround, 3, null, 0, Tuning);
        StringAssert.Contains("at most", LocalCultureRules.WhyNotCarry(s, 7, LocalCultureRules.Profile(s, River), "evening-song", Tuning), "the repertoire is bounded");
        // The orchard town gathers for what it saw: it shares the river town's song now, and still keeps its own.
        var change = LocalCultureRules.Gather(s, Orchard, "Goldbough", "crossing-songs", OrchardGround, 4, null, 0, Tuning);
        Assert.AreEqual(LocalPracticeStage.Practiced, change.stage);
        Assert.AreEqual("Crossing Songs of Goldbough", At(s, Orchard, "crossing-songs").variant, "its own form of the shared custom");
        // National recognition keeps each town's variant.
        Assert.AreEqual(2, LocalCultureRules.Recognize(s, "crossing-songs"));
        Assert.AreEqual("Crossing Songs of Riverford", At(s, River, "crossing-songs").variant);
        Assert.AreEqual("Crossing Songs of Goldbough", At(s, Orchard, "crossing-songs").variant);
        Assert.IsTrue(At(s, River, "crossing-songs").recognized && At(s, Orchard, "crossing-songs").recognized);
        Assert.AreEqual(PracticeChannel.Visit, At(s, Orchard, "crossing-songs").origin.channel);
    }

    // ===== FOUNDERS AND ARRIVALS =====

    [Test]
    public void Founders_BringTheirHomeCustoms_Once()
    {
        var s = TwoTowns();
        var changes = LocalCultureRules.Found(s, "founding:9", 9, "Riverford Outskirts I", River, "Riverford", "the people of Riverford", 4, null, 0, Tuning);
        Assert.AreEqual(1, changes.Count);
        var p = At(s, 9, "crossing-songs");
        Assert.AreEqual(PracticeChannel.Founders, p.origin.channel);
        Assert.AreEqual(Tuning.founderExposure, p.exposure, 1e-4f);
        Assert.IsEmpty(LocalCultureRules.Found(s, "founding:9", 9, "Riverford Outskirts I", River, "Riverford", null, 4, null, 0, Tuning));
        Assert.IsEmpty(LocalCultureRules.Found(s, "founding:10", 10, "Nowhere", -1, null, null, 4, null, 0, Tuning), "unknown founders bring nothing");
    }

    [Test]
    public void UnknownMigrantOrigins_RemainUnknown_AndBringNoCustom()
    {
        var s = TwoTowns();
        var caravan = new ArrivalRecord { key = "admission:1", settlement = River, people = 6, originKind = ArrivalOriginKind.Unknown, channel = "caravan" };
        var found = new ArrivalRecord { key = "admission:2", settlement = River, people = 3, originKind = ArrivalOriginKind.Place, originId = "431", originLabel = "the Violet Grove", channel = "survivors" };
        Assert.IsEmpty(LocalCultureRules.Arrive(s, caravan, Tuning, 2, 0, "Riverford"));
        Assert.IsEmpty(LocalCultureRules.Arrive(s, found, Tuning, 2, 0, "Riverford"), "a known place is not a known culture");
        CollectionAssert.AreEqual(new[] { "crossing-songs" }, LocalCultureRules.Profile(s, River).practices.Select(p => p.practice),
            "the river town gained nothing from anonymous arrivals: no custom is assumed");
        Assert.AreEqual(ArrivalOriginKind.Unknown, s.arrivals[0].originKind);
        Assert.IsNull(s.arrivals[0].originId);
        Assert.AreEqual((0, 3, 6), LocalCultureRules.ArrivalsAt(s, River));
        Assert.IsEmpty(LocalCultureRules.Arrive(s, caravan, Tuning, 2, 0, "Riverford"), "the same admission is recorded once");
        Assert.AreEqual(2, s.arrivals.Count);
        Assert.AreEqual("6 people (caravan): origin unknown", new ArrivalView { people = 6, channel = "caravan" }.Text);
        // People from one of your settlements do bring its customs, attributed to them.
        var settlers = new ArrivalRecord { key = "admission:3", settlement = River, people = 4, originKind = ArrivalOriginKind.Settlement, originId = "2", originLabel = "Goldbough", channel = "settlers" };
        Assert.AreEqual(1, LocalCultureRules.Arrive(s, settlers, Tuning, 3, 0, "Riverford").Count);
        Assert.AreEqual(PracticeChannel.Arrival, At(s, River, "golden-fruit").origin.channel);
    }

    [Test]
    public void ArrivalRecords_StayBounded_WithSummaries()
    {
        var s = State();
        var tuning = new LocalCultureTuning { arrivalsKept = 3 };
        for (int i = 0; i < 10; i++) LocalCultureRules.Arrive(s, new ArrivalRecord { key = "admission:" + i, settlement = 0, people = 2, channel = "caravan" }, tuning, i, 0, "The Capital");
        Assert.AreEqual(3, s.arrivals.Count);
        Assert.AreEqual((0, 0, 20), LocalCultureRules.ArrivalsAt(s, 0), "older arrivals are summed, never lost");
    }

    [Test]
    public void AFallenSettlementsCustoms_BecomeHistory_AndAreNeverInheritedByANewOne()
    {
        var s = TwoTowns();
        // The orchard town falls: only the river town stands.
        LocalCultureRules.Seventh(s, ContactSnapshot.Build(new[] { (River, "Riverford") }, new ContactRoute[0]), Tuning, 2, null, 0);
        Assert.IsNull(LocalCultureRules.Profile(s, Orchard));
        var history = s.former.Single(p => p.settlement == Orchard);
        Assert.IsTrue(history.gone);
        Assert.AreEqual(LocalPracticeStage.Practiced, history.practices.Single().stage, "its customs are remembered as they were");
        // A new settlement takes the same id: it starts with nothing of the fallen town's.
        LocalCultureRules.Retire(s, Orchard);
        var fresh = LocalCultureRules.EnsureProfile(s, Orchard, "New Goldbough");
        Assert.IsEmpty(fresh.practices);
        LocalCultureRules.Seventh(s, Towns(road: true), Tuning, 3, null, 0);
        Assert.IsNull(At(s, Orchard, "golden-fruit"));
        Assert.AreEqual(1, s.former.Count);
    }

    // ===== AGES AND STATE =====

    [Test]
    public void AgeGates_KeepLaterCustomsFromAppearingEarly()
    {
        var s = State();
        Assert.IsNotNull(LocalCultureRules.WhyNotGather(null, "flavor-log", " plain", 0, 1, Tuning), "Flavor Logs belong to Ages I-III");
        Assert.IsNull(LocalCultureRules.WhyNotGather(null, "flavor-log", " plain", 1, 1, Tuning));
        Assert.IsFalse(LocalCultureRules.Expose(s, 1, "A", "flavor-log", 1f, null, 0), "nor can it be carried in before its Age");
        Assert.IsNotNull(LocalCultureRules.WhyNotGather(null, "golden-fruit", OrchardGround, 1, 1, Tuning), "the Hunger's orchard rite is of the Age of Desolation");
        foreach (var spec in LocalPracticeCatalog.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(spec.canonNote), spec.id + " explains its canon status");
            if (spec.canon != CanonStatus.NewGameRule) Assert.IsFalse(string.IsNullOrEmpty(spec.vault), spec.id + " cites its vault note");
        }
    }

    [Test]
    public void LocalState_SurvivesTheSaveCodec_AndAnOlderEnvelopeLoads()
    {
        var s = TwoTowns();
        Party(s, River, "Riverford", "crossing-songs");
        LocalCultureRules.Arrive(s, new ArrivalRecord { key = "admission:1", settlement = River, people = 2, channel = "caravan" }, Tuning, 1, 0, "Riverford");
        var node = SaveStateCodec.Write(s, typeof(LocalCultureState));
        var back = (LocalCultureState)SaveStateCodec.Read(node, typeof(LocalCultureState));
        Assert.AreEqual(LocalPracticeStage.Practiced, At(back, River, "crossing-songs").stage);
        Assert.AreEqual(PracticeChannel.Originated, At(back, Orchard, "golden-fruit").origin.channel);
        Assert.AreEqual("crossing-songs", LocalCultureRules.Repertoire(back, 7).carried.Single().practice);
        Assert.AreEqual(ArrivalOriginKind.Unknown, back.arrivals.Single().originKind);
        // An envelope saved before local cultures existed: the field is optional, a fresh state is made.
        var envelope = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        envelope.children.RemoveAll(c => c.name == "local");
        var older = (CultureExtensionState)SaveStateCodec.Read(envelope, typeof(CultureExtensionState));
        Assert.IsNotNull(older.local);
        Assert.IsEmpty(older.local.profiles, "nothing is invented for an older save");
    }

    // ===== ON A GENERATED WORLD =====

    [Test]
    public void OnTheMap_RoadsMakeContact_ThreatsInterruptIt_AndAdoptionMovesNoLand()
    {
        var settings = WorldGenerationTests.Settings();
        var map = WorldGenerator.Generate(42, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        var rules = new SettlementRules();
        WorldCivilization.EnsureCapital(map, settings);
        var target = map.Tiles.Where(t => t.authorityId == WorldAuthority.Wilderness && !t.water && !t.impassable && t.enclave < 0
                && HexCoord.Distance(t.coord, map.Capital) >= 14 && map.Enclaves.All(e => HexCoord.Distance(e.coord, t.coord) >= rules.spacing))
            .OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).First();
        map.Reveal(target.coord, 0);
        var outpost = WorldCivilization.Found(map, settings, rules, target.coord, SettlementKind.Outpost, 0);
        var capital = WorldCivilization.Capital(map);
        Assert.IsEmpty(CulturalContactMap.Build(map).Edges, "before the road they are strangers");
        Assert.IsTrue(WorldCivilization.PlanRoad(map, settings, outpost, out var path, out int to));
        var route = WorldCivilization.BuildRoad(map, settings, rules, outpost, path, to);
        var open = CulturalContactMap.Build(map);
        Assert.IsTrue(open.Edge(capital.id, outpost.id).open);

        var authority = map.Tiles.Select(t => t.authorityId).ToList();
        var s = State();
        LocalCultureRules.Gather(s, capital.id, capital.name, "evening-song", CulturalContactMap.Ground(map, capital), 1, null, 0, Tuning);
        for (int seventh = 2; seventh < 30; seventh++)
        {
            if (seventh % 7 == 0) LocalCultureRules.Gather(s, capital.id, capital.name, "evening-song", "", seventh, null, 0, Tuning);
            LocalCultureRules.Seventh(s, open, Tuning, seventh, null, 0);
        }
        LocalCultureRules.Gather(s, outpost.id, outpost.name, "evening-song", "", 30, null, 0, Tuning);
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, outpost.id, "evening-song").stage);
        CollectionAssert.AreEqual(authority, map.Tiles.Select(t => t.authorityId).ToList(), "cultural adoption does not change territory");

        // A threat's source on the road interrupts it.
        int middle = map.Get(route.cells[route.cells.Count / 2]).index;
        map.Threats.Add(new ThreatSite { spec = "test", cell = middle, strength = 1f, radius = 1f });
        var cut = CulturalContactMap.Build(map);
        Assert.IsFalse(cut.Edge(capital.id, outpost.id).open);
        StringAssert.StartsWith("a threat at", cut.Edge(capital.id, outpost.id).blockedBy);
        Assert.AreEqual(LocalPracticeStage.Practiced, At(s, outpost.id, "evening-song").stage, "what was learned stays");
    }
}
