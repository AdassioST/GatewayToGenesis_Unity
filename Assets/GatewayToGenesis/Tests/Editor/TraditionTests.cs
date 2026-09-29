using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Living traditions with no scene (<see cref="TraditionRules"/>, <see cref="TraditionLifecycle"/>,
/// <see cref="CultureOccurrences"/>, <see cref="CultureMigration"/>): a practice emerges from what the people do,
/// becomes a custom only when repeated, lies dormant without deleting its history, revives without repeating its
/// firsts, keeps its own cause per place, and replays the same after a save. Pure, so they also run outside Unity.
/// </summary>
public class TraditionTests
{
    private static int _serial;

    private static CulturalOccurrence Rite(string activity, int seventh, int settlement = -1, params string[] actors) => new CulturalOccurrence
    {
        key = $"test:{activity}:{++_serial}", kind = CulturalOccurrenceKind.Gathering, subject = CultureEntityRef.Of(CultureEntityKind.Activity, activity, activity),
        settlement = settlement, quantity = 1f, unit = CultureQuantityUnit.Occasions, stamp = new CultureStamp { cultureSeventh = seventh },
        actors = actors.ToList(), text = $"{activity} at {settlement}",
    };

    private static CulturalOccurrence Festival(int settlement, int seventh, params string[] actors)
    {
        var o = Rite("festival", seventh, settlement, actors);
        o.kind = CulturalOccurrenceKind.Festival;
        o.subject = CultureEntityRef.Of(CultureEntityKind.Settlement, CultureIds.Settlement(settlement));
        o.text = $"a festival in settlement {settlement}";
        return o;
    }

    private static List<TraditionChange> Advance(TraditionState state, TraditionTuning tuning, int seventh, params CulturalOccurrence[] occurrences) =>
        TraditionRules.Advance(state, tuning.definitions, occurrences, seventh, tuning);

    // Tales at the Hearth kept on three Sevenths: a custom.
    private static (TraditionState state, TraditionTuning tuning, TraditionInstance tales) Established()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        Advance(state, tuning, 1, Rite("tales", 1));
        Advance(state, tuning, 2, Rite("tales", 2));
        Advance(state, tuning, 3, Rite("tales", 3));
        return (state, tuning, state.instances.Single());
    }

    [Test]
    public void APracticeEmerges_ThenBecomesACustomOnlyWhenRepeatedOverSeveralSevenths()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        var changes = Advance(state, tuning, 1, Rite("tales", 1), Rite("tales", 1), Rite("tales", 1), Rite("tales", 1));
        var tales = state.instances.Single();
        Assert.AreEqual("hearth-tales", tales.definition);
        Assert.AreEqual(TraditionStage.Emerging, tales.stage, "one burst of four tales is not a custom");
        Assert.AreEqual(4, tales.participations);
        Assert.AreEqual(1, tales.distinctSevenths);
        Assert.AreEqual(tuning.maxCreditPerSeventh, tales.momentum, 1e-4f, "a Seventh's credit is capped");
        Assert.AreEqual(TraditionChangeKind.Emerged, changes.Single().kind);
        Assert.AreEqual(TraditionRecognition.None, tales.recognition);

        Advance(state, tuning, 2, Rite("tales", 2));
        Assert.AreEqual(TraditionStage.Emerging, tales.stage);
        changes = Advance(state, tuning, 3, Rite("tales", 3));
        Assert.AreEqual(TraditionStage.Practiced, tales.stage);
        Assert.AreEqual(TraditionRecognition.Offered, tales.recognition, "the player is asked; nothing is decided for them");
        CollectionAssert.AreEqual(new[] { TraditionChangeKind.Established, TraditionChangeKind.Offered }, changes.Select(c => c.kind).ToArray());
        Assert.AreEqual(1, TraditionRules.Waiting(state).Count());
    }

    [Test]
    public void UnrelatedOccurrencesFeedNothing_AndLinkedOnlyTraditionsNeverArise()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        var eaten = new CulturalOccurrence { key = "eat:1", kind = CulturalOccurrenceKind.Consumption, subject = CultureEntityRef.Of(CultureEntityKind.Resource, "Peach Soup") };
        Advance(state, tuning, 1, Rite("feast-of-abundance", 1), eaten);
        Assert.IsEmpty(state.instances, "a feast feeds no authored tradition, and eating alone never makes a national table");
    }

    [Test]
    public void TechnologyGatedTraditionsWaitForTheirTechnology()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        TraditionRules.Advance(state, tuning.definitions, new[] { Rite("remembrance", 1) }, 1, tuning, d => string.IsNullOrEmpty(d.technology));
        Assert.IsEmpty(state.instances);
        TraditionRules.Advance(state, tuning.definitions, new[] { Rite("remembrance", 2) }, 2, tuning, d => true);
        Assert.AreEqual("naming-of-the-lost", state.instances.Single().definition);
    }

    [Test]
    public void ThreeInstancesInThreePlacesKeepTheirOwnCauses()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        Advance(state, tuning, 1, Rite("song", 1), Festival(4, 1, "Vaelia"), Festival(7, 1, "Orin", "Sela"));
        var songs = state.instances.Where(i => i.definition == "evening-song").OrderBy(i => i.settlement).ToList();
        Assert.AreEqual(3, songs.Count, "the nation's (a rite with no known place) and one per settlement");
        CollectionAssert.AreEqual(new[] { -1, 4, 7 }, songs.Select(i => i.settlement).ToArray());
        Assert.AreEqual(3, songs.Select(i => i.origin.key).Distinct().Count());
        Assert.AreEqual(CulturalOccurrenceKind.Gathering, songs[0].origin.kind);
        CollectionAssert.AreEqual(new[] { "Vaelia" }, songs[1].bearers);
        CollectionAssert.AreEqual(new[] { "Orin", "Sela" }, songs[2].bearers);
        CollectionAssert.IsEmpty(songs[0].bearers, "no one is invented for a rite whose participants were not recorded");
        StringAssert.Contains("settlement 7", TraditionRules.Explain(songs[2], tuning.Definition("evening-song")));
        Assert.AreEqual(3, songs.Select(i => i.id).Distinct().Count());
    }

    [Test]
    public void DormancyStopsTheBenefitsButKeepsTheRecord()
    {
        var (state, tuning, tales) = Established();
        var benefits = TraditionRules.Benefits(state, tuning);
        Assert.AreEqual(1, benefits.Count);
        Assert.IsTrue(benefits[0].Effects.Any(), "a lived custom gives its practice benefit");
        int history = tales.history.Count;

        var changes = Advance(state, tuning, 3 + tuning.lapseSevenths + 1);
        Assert.AreEqual(TraditionStage.Dormant, tales.stage);
        Assert.AreEqual(TraditionChangeKind.Lapsed, changes.Single().kind);
        Assert.IsEmpty(TraditionRules.Benefits(state, tuning), "dormant: no effect applies");
        Assert.IsEmpty(TraditionRules.LivedFamilies(state, tuning));
        Assert.AreEqual(history, tales.history.Count, "its history is kept");
        Assert.AreEqual(3, tales.participations);
        Assert.IsTrue(tales.Established);
        Assert.IsEmpty(TraditionRules.Waiting(state), "no decision is asked about a custom no one keeps");
        StringAssert.Contains("dormant", TraditionRules.WhyNotRecognize(state, tales, tuning.Definition("hearth-tales"), tuning));
    }

    [Test]
    public void RevivalRestoresTheBenefit_AndNeverRepeatsItsFirsts()
    {
        var (state, tuning, tales) = Established();
        TraditionRules.Recognize(tales, 3);
        int lapse = 3 + tuning.lapseSevenths + 1;
        Advance(state, tuning, lapse);
        Assert.AreEqual(TraditionStage.Dormant, tales.stage);
        Assert.AreEqual(TraditionRecognition.Recognized, tales.recognition, "recognition is part of the record");

        Advance(state, tuning, lapse + 1, Rite("tales", lapse + 1));
        Assert.AreEqual(TraditionStage.Dormant, tales.stage, "one evening does not revive it");
        var changes = Advance(state, tuning, lapse + 2, Rite("tales", lapse + 2));
        Assert.AreEqual(TraditionStage.Revived, tales.stage);
        Assert.IsTrue(changes.Single().first);
        var benefit = TraditionRules.Benefits(state, tuning).Single();
        Assert.IsTrue(benefit.recognized, "revived, its recognised benefit applies again");

        // It lapses and revives again: nothing is a first any more.
        int again = lapse + 2 + tuning.lapseSevenths + 1;
        var lapsed = Advance(state, tuning, again);
        Assert.IsFalse(lapsed.Single().first);
        Advance(state, tuning, again + 1, Rite("tales", again + 1));
        changes = Advance(state, tuning, again + 2, Rite("tales", again + 2));
        Assert.AreEqual(TraditionChangeKind.Revived, changes.Single().kind);
        Assert.IsFalse(changes.Single().first, "a second revival is not remembered or rewarded as the first");
        Assert.AreEqual(2, tales.revivals);
        Assert.AreEqual(1, tales.milestones.Count(m => m == TraditionRules.RevivedMilestone));
        Assert.IsFalse(TraditionRules.Recognize(tales, again + 2), "recognising again is never a first either");
    }

    [Test]
    public void AnEmergingPracticeThatLapsesEmergesAgainRatherThanReviving()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        Advance(state, tuning, 1, Rite("tales", 1));
        Advance(state, tuning, 2 + tuning.lapseSevenths);
        var tales = state.instances.Single();
        Assert.AreEqual(TraditionStage.Dormant, tales.stage);
        int s = 3 + tuning.lapseSevenths;
        Advance(state, tuning, s, Rite("tales", s));
        var changes = Advance(state, tuning, s + 1, Rite("tales", s + 1));
        Assert.AreEqual(TraditionStage.Emerging, tales.stage);
        Assert.AreEqual(TraditionChangeKind.Reemerging, changes.Single().kind);
    }

    [Test]
    public void PreservedCustomsLapseMoreSlowly_AndDeferredDecisionsReturn()
    {
        var (state, tuning, tales) = Established();
        Assert.IsNull(TraditionRules.WhyNotDefer(tales));
        TraditionRules.Defer(tales, 3, tuning);
        Assert.IsEmpty(TraditionRules.Waiting(state));
        Advance(state, tuning, 3 + tuning.deferSevenths - 1, Rite("tales", 3 + tuning.deferSevenths - 1));
        Assert.AreEqual(TraditionRecognition.Deferred, tales.recognition);
        var changes = Advance(state, tuning, 3 + tuning.deferSevenths, Rite("tales", 3 + tuning.deferSevenths));
        Assert.AreEqual(TraditionRecognition.Offered, tales.recognition, "offered again once the wait is over");
        Assert.IsFalse(changes.Single(c => c.kind == TraditionChangeKind.Offered).first);

        Assert.IsNull(TraditionRules.WhyNotPreserve(tales, tuning.Definition("hearth-tales"), tuning));
        Assert.IsTrue(TraditionRules.Preserve(tales, 30));
        Assert.AreEqual(tuning.lapseSevenths * 2, TraditionRules.LapseAfter(tales, tuning));
        Advance(state, tuning, tales.lastPracticed + tuning.lapseSevenths + 1);
        Assert.AreEqual(TraditionStage.Practiced, tales.stage, "a preserved custom outlasts an ordinary lapse");
    }

    [Test]
    public void BenefitsArePerDefinitionAndCapped_NotPerSettlement()
    {
        var tuning = new TraditionTuning { maxActiveBenefits = 1 };
        var state = new TraditionState();
        for (int s = 1; s <= 3; s++) Advance(state, tuning, s, Festival(4, s), Festival(7, s), Rite("song", s), Rite("tales", s), Rite("tales", s));
        var songs = state.instances.Where(i => i.definition == "evening-song").ToList();
        Assert.AreEqual(3, songs.Count);
        Assert.IsTrue(songs.All(i => i.Lived));
        var benefits = TraditionRules.Benefits(state, tuning);
        Assert.AreEqual(2, benefits.Count, "one entry per definition however many places keep it");
        Assert.AreEqual(1, benefits.Count(b => !b.capped));
        Assert.AreEqual(1, benefits.Count(b => b.capped));
        Assert.IsEmpty(benefits.Single(b => b.capped).Effects);
        Assert.AreEqual(3, benefits.Single(b => b.definition.id == "evening-song").through.Count);
        Assert.AreEqual(2, TraditionRules.LivedFamilies(state, tuning).Count, "Auric and Weaver, one share each");
    }

    [Test]
    public void RecognitionIsNeverDecidedByAThreshold_AndIsOncePerDefinition()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        for (int s = 1; s <= 3; s++) Advance(state, tuning, s, Festival(4, s), Festival(7, s));
        Assert.IsTrue(state.instances.All(i => i.recognition == TraditionRecognition.Offered));
        Assert.IsTrue(TraditionRules.Benefits(state, tuning).All(b => !b.recognized), "recognition waits for the player");
        var d = tuning.Definition("evening-song");
        var first = state.instances[0];
        Assert.IsNull(TraditionRules.WhyNotRecognize(state, first, d, tuning));
        TraditionRules.Recognize(first, 3);
        StringAssert.Contains("preserve", TraditionRules.WhyNotRecognize(state, state.instances[1], d, tuning));
        Assert.IsNull(TraditionRules.WhyNotPreserve(state.instances[1], d, tuning), "the other town can keep its own form");
    }

    [Test]
    public void TheSameHistoryGivesTheSameTraditionsAfterASave()
    {
        var tuning = new TraditionTuning();
        var script = new List<CulturalOccurrence[]>();
        for (int s = 1; s <= 12; s++)
            script.Add(s % 4 == 0 ? new CulturalOccurrence[0] : new[] { Rite("tales", s), Festival(4, s, "Vaelia"), Rite(s % 2 == 0 ? "song" : "tales", s) });

        var straight = new TraditionState();
        for (int s = 1; s <= 12; s++) Advance(straight, tuning, s, script[s - 1]);

        var saved = new TraditionState();
        for (int s = 1; s <= 6; s++) Advance(saved, tuning, s, script[s - 1]);
        var node = SaveStateCodec.Write(saved, typeof(TraditionState));
        var loaded = (TraditionState)SaveStateCodec.Read(node, typeof(TraditionState));
        for (int s = 7; s <= 12; s++) Advance(loaded, tuning, s, script[s - 1]);

        Assert.AreEqual(Describe(straight), Describe(loaded));
        Assert.AreEqual(straight.nextId, loaded.nextId);
    }

    private static string Describe(TraditionState state) => string.Join("\n", state.instances.Select(i =>
        $"{i.id} {i.definition}@{i.settlement} {i.stage} {i.recognition} m={i.momentum:0.####} p={i.participations} d={i.distinctSevenths} last={i.lastPracticed} " +
        $"origin={i.origin.key} bearers={string.Join(",", i.bearers)} hist={string.Join(",", i.history.Select(h => h.key))} ms={string.Join(",", i.milestones)}"));

    [Test]
    public void TheLedgerCountsOneActionOnce_EvenAcrossASave()
    {
        var ledger = new CultureOccurrenceLedger();
        var festival = Festival(4, 1);
        Assert.IsTrue(CultureOccurrences.Accept(ledger, festival, 1));
        Assert.IsFalse(CultureOccurrences.Accept(ledger, festival.Copy(), 1), "the same committed action reported twice");
        var node = SaveStateCodec.Write(ledger, typeof(CultureOccurrenceLedger));
        var loaded = (CultureOccurrenceLedger)SaveStateCodec.Read(node, typeof(CultureOccurrenceLedger));
        Assert.AreEqual(1, loaded.pending.Count, "what waits for the Seventh survives the save");
        Assert.IsFalse(CultureOccurrences.Accept(loaded, festival.Copy(), 1));
        var drained = CultureOccurrences.Drain(loaded, 2);
        Assert.AreEqual(1, drained.Count);
        Assert.IsEmpty(loaded.pending);
        Assert.IsFalse(CultureOccurrences.Accept(loaded, festival.Copy(), 2), "drained keys are still remembered");
        Assert.IsFalse(CultureOccurrences.Accept(loaded, new CulturalOccurrence(), 2), "no key, no count");
        CultureOccurrences.Prune(loaded, 2 + CultureOccurrences.DedupeWindow + 1);
        Assert.IsEmpty(loaded.accepted, "keys are forgotten after the window");
    }

    [Test]
    public void AnOldSaveLoadsWithoutInventingHistory_AndItsNationalFoodsGetHonestRecords()
    {
        // A culture state saved before the envelope existed: its record has no "extensions" field.
        var old = new CultureState { founded = true, sevenths = 40 };
        old.foodways.Add(new Foodway { resource = "Peach Soup", cuisine = FoodClass.Edible, national = true, nationalSeventh = 30, eaten = 500f, familiarity = 0.8f });
        old.foodways.Add(new Foodway { resource = "Earth-Beans", cuisine = FoodClass.Edible, eaten = 900f });
        old.activities.Add(new ActivityRecord { id = "song", count = 9, lastSeventh = 38 });
        var node = SaveStateCodec.Write(old, typeof(CultureState));
        node.children.RemoveAll(c => c.name == "extensions");
        foreach (var way in node.children.Single(c => c.name == "foodways").children) way.children.RemoveAll(c => c.name == "tradition");

        var loaded = (CultureState)SaveStateCodec.Read(node, typeof(CultureState));
        Assert.IsNotNull(loaded.extensions, "a missing envelope loads fresh");
        Assert.AreEqual(0, loaded.extensions.version);
        Assert.IsTrue(CultureMigration.Upgrade(loaded, new TraditionTuning()));
        Assert.AreEqual(CultureExtensionState.CurrentVersion, loaded.extensions.version);

        var record = loaded.extensions.traditions.instances.Single();
        Assert.AreEqual(TraditionRules.NationalTable, record.definition);
        Assert.AreEqual(record.id, loaded.foodways[0].tradition);
        Assert.IsTrue(record.origin.legacy);
        Assert.AreEqual(0, record.participations, "no occasion is made up from the old totals");
        CollectionAssert.IsEmpty(record.history);
        CollectionAssert.IsEmpty(record.bearers);
        Assert.AreEqual(TraditionRecognition.Recognized, record.recognition, "embracing it was the recognition");
        Assert.IsFalse(loaded.extensions.traditions.instances.Any(i => i.definition == "evening-song"), "nine old songs are not turned into a custom");

        Assert.IsFalse(CultureMigration.Upgrade(loaded, new TraditionTuning()), "migration is idempotent");
        Assert.AreEqual(1, loaded.extensions.traditions.instances.Count);
    }

    [Test]
    public void ANationalFoodKeptAtTheTableStaysLived_AndLapsesWhenNoLongerEaten()
    {
        var tuning = new TraditionTuning();
        var state = new TraditionState();
        var soup = new Foodway { resource = "Peach Soup", national = true, nationalSeventh = 5 };
        var record = TraditionRules.LinkNationalFood(state, soup, new TraditionOrigin { key = "national:Peach Soup", text = "embraced" }, 5);
        Assert.AreSame(record, TraditionRules.LinkNationalFood(state, soup, null, 6), "one record per food");
        Assert.IsTrue(record.Lived);
        Assert.IsEmpty(TraditionRules.Benefits(state, tuning), "its output bonus stays the national food's own");
        CulturalOccurrence Table(int s) => new CulturalOccurrence { key = $"table:Peach Soup:{s}", kind = CulturalOccurrenceKind.Consumption, subject = CultureEntityRef.Of(CultureEntityKind.Resource, "Peach Soup"), stamp = new CultureStamp { cultureSeventh = s } };
        for (int s = 6; s <= 10; s++) Advance(state, tuning, s, Table(s));
        Assert.AreEqual(5, record.participations);
        Assert.AreEqual(1, state.instances.Count, "eating never makes a second record");
        Advance(state, tuning, 10 + tuning.lapseSevenths + 1);
        Assert.AreEqual(TraditionStage.Dormant, record.stage);
    }

    [Test]
    public void TheLifecycleRunsInItsPhase_AndSeesOccurrencesRecordedEarlierInTheSeventh()
    {
        var state = new CultureState { founded = true, sevenths = 1 };
        CultureMigration.Ensure(state);
        var context = new CultureSeventh(null, state, new CultureStamp { cultureSeventh = 1 }, new List<CulturalOccurrence> { Rite("tales", 1) });
        CultureFeatures.Run(new ICultureFeature[] { new TraditionLifecycleFeature(), new TraditionEffectsFeature() }, context);
        Assert.AreEqual("hearth-tales", state.extensions.traditions.instances.Single().definition);
        var order = CultureFeatures.Types.Select(t => ((ICultureFeature)System.Activator.CreateInstance(t)).Phase).ToList();
        CollectionAssert.IsOrdered(order, "features run in phase order");
        Assert.Less(CultureFeatures.Types.ToList().IndexOf(typeof(TraditionLifecycleFeature)), CultureFeatures.Types.ToList().IndexOf(typeof(TraditionEffectsFeature)));
    }

    [Test]
    public void EveryAuthoredTraditionIsLabelledAndFedByRealActions()
    {
        var tuning = new TraditionTuning();
        var life = new CultureLifeTuning();
        Assert.GreaterOrEqual(tuning.definitions.Count(d => !d.linkedOnly), 3);
        Assert.IsTrue(tuning.definitions.Any(d => !d.food && !d.linkedOnly), "at least one non-food practice");
        Assert.AreEqual(tuning.definitions.Count, tuning.definitions.Select(d => d.id).Distinct().Count());
        foreach (var d in tuning.definitions)
        {
            Assert.IsFalse(string.IsNullOrEmpty(d.canonSource), d.id + " cites its vault note");
            Assert.IsNotEmpty(d.triggers, d.id);
            foreach (var t in d.triggers.Where(t => t.subjectKind == CultureEntityKind.Activity && !string.IsNullOrEmpty(t.subjectId) && !t.subjectId.StartsWith("local:")))
                Assert.IsNotNull(life.Activity(t.subjectId), $"{d.id} listens to rite '{t.subjectId}'");
            foreach (var t in d.triggers.Where(t => t.subjectKind == CultureEntityKind.Recipe && !string.IsNullOrEmpty(t.subjectId)))
                Assert.IsNotNull(life.Recipe(t.subjectId), $"{d.id} listens to recipe '{t.subjectId}'");
        }
        var tales = life.Activity("tales");
        Assert.AreEqual(0f, tales.foodValue);
        Assert.IsEmpty(tales.cost, "the simplest gathering costs nothing");
    }
}
