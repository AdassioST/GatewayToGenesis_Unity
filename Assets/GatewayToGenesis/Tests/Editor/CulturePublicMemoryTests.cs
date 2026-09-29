using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Public memory and civic legitimacy (T09), with no scene (<see cref="PublicMemoryRules"/>): a promise is compared with
/// a bounded window of records only; a gap opens one dispute; each answer adds a version of the account and never
/// touches the records; a promise no longer in force lapses its link (effect gone, history kept); places stand by what
/// was recorded there; the culture's part of the accord is capped and explained; the whole record survives a save.
/// </summary>
public class CulturePublicMemoryTests
{
    private static readonly PublicMemoryTuning Tuning = new PublicMemoryTuning();
    private static PromiseSpec OpenGate => Tuning.Promise("open-gate");
    private static PromiseSpec Hunger => Tuning.Promise("lesson-of-hunger");

    // Two towns with the same records; a patron's private Sweets at Ashford and luxuries held back.
    private static PublicFacts HollowFacts(int now = 30)
    {
        var f = new PublicFacts { now = now, practiceName = "The Ash-Loaf Table" };
        f.settlements.Add((1, "Ashford"));
        f.settlements.Add((2, "Brightwater"));
        f.settlements.Add((3, "Quietmoor"));
        f.tables.Add(new TableFact { key = "table-1", policy = TablePolicy.PatronHosted, settlement = 1, place = "Ashford", patron = "Oren", seventh = now - 5, privateLuxuries = new[] { "Honeyed Grain Porridge" } });
        f.tables.Add(new TableFact { key = "table-2", policy = TablePolicy.PatronHosted, settlement = 2, place = "Brightwater", patron = "Isolde", seventh = now - 3, privateLuxuries = new[] { "Honeyed Grain Porridge" } });
        f.hoarded.Add("12 Honeyed Peach Tart");
        return f;
    }

    private static PublicLink LinkedGate(PublicMemoryState s, int now = 1) => PublicMemoryRules.Link(s, OpenGate, "trad-4", "The Ash-Loaf Table", now, "age-of-renewal");

    [Test]
    public void StoriesSelectTheirOwnEvidence_WhenDifferentDisputesAreOpen()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var first = PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, new PromiseComparison(), null, Tuning, 5, null);
        var second = PublicMemoryRules.Open(s, Tuning.Dispute("unbaked-loaf"), link, new PromiseComparison(), null, Tuning, 6, null);
        Assert.AreSame(first, PublicMemoryRules.StoryDispute(s, Tuning, Tuning.Dispute("hollow-table").story));
        Assert.AreSame(second, PublicMemoryRules.StoryDispute(s, Tuning, Tuning.Dispute("unbaked-loaf").story));
        Assert.IsNull(PublicMemoryRules.StoryDispute(s, Tuning, "another_story"));
        PublicMemoryRules.Resolve(s, first, Tuning.Dispute(first.spec), DisputeResolution.Sponsor, "Oren", null, 7, null);
        Assert.IsNull(PublicMemoryRules.StoryDispute(s, Tuning, Tuning.Dispute(first.spec).story), "the other story is never a fallback");
        Assert.AreEqual("Oren", first.sponsor, "the bound ending retains the actual sponsor");
    }

    [Test]
    public void EndedOrRepealedLinksGiveNoAcknowledgmentAccord()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var spec = Tuning.Dispute("hollow-table");
        var d = PublicMemoryRules.Open(s, spec, link, new PromiseComparison(), null, Tuning, 5, null);
        PublicMemoryRules.Resolve(s, d, spec, DisputeResolution.Acknowledge, null, null, 6, null);
        Assert.Greater(PublicMemoryRules.Accord(s, Tuning, 6).points, 0f);
        Assert.AreEqual(0f, PublicMemoryRules.Accord(s, Tuning, 6, _ => false).points, "repeal applies before the next Seventh");
        Assert.AreEqual(0f, PublicMemoryRules.Accord(s, Tuning, 5).points, "future acknowledgments do not count");
        PublicMemoryRules.Lapse(s, link, "law ended", 6);
        Assert.AreEqual(0f, PublicMemoryRules.Accord(s, Tuning, 6).points);
        Assert.AreEqual(2, s.accounts.Count, "history is kept");
    }

    [Test]
    public void InvalidDisputeAnswersDoNotChangeHistory()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var spec = Tuning.Dispute("hollow-table");
        var d = PublicMemoryRules.Open(s, spec, link, new PromiseComparison(), null, Tuning, 5, null);
        Assert.IsNull(PublicMemoryRules.Resolve(s, d, null, DisputeResolution.Acknowledge, null, null, 6, null));
        Assert.IsNull(PublicMemoryRules.Resolve(s, d, spec, (DisputeResolution)99, null, null, 6, null));
        link.status = LinkStatus.Lapsed;
        Assert.IsNull(PublicMemoryRules.Resolve(s, d, spec, DisputeResolution.Acknowledge, null, null, 6, null));
        s.links.Clear();
        Assert.IsNull(PublicMemoryRules.Resolve(s, d, spec, DisputeResolution.Acknowledge, null, null, 6, null));
        Assert.IsTrue(d.Open);
        Assert.AreEqual(1, s.accounts.Count);
    }

    [Test]
    public void FutureRecordsCannotSupportOrContradictTodaysPromise()
    {
        var f = new PublicFacts { now = 10 };
        f.tables.Add(new TableFact { seventh = 11, policy = TablePolicy.PublicWelcome });
        f.admissions.Add(new AdmissionFact { seventh = 11, people = 5 });
        var c = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.AreEqual(0, c.supports);
        Assert.AreEqual(0, c.contradicts);
    }

    [Test]
    public void LocalRemembranceOnlySupportsTheCausePromised()
    {
        var f = new PublicFacts { now = 10 };
        f.settlements.Add((1, "Ashford"));
        f.causes.Add(new CauseFact { id = "unrelated", cause = MemoryCause.Crisis, subject = "The Great Plague" });
        f.causes.Add(new CauseFact { id = "hunger", cause = Hunger.cause, subject = Hunger.causeSubject, lastQuiet = 9 });
        f.memorials.Add(new MemorialFact { evidence = "unrelated", settlement = 1, seventh = 9 });
        Assert.AreEqual(LocalStance.NoRecord, PublicMemoryRules.Local(Hunger, f, Tuning, false).Single().stance);
        f.memorials.Add(new MemorialFact { evidence = "hunger", settlement = 1, seventh = 9 });
        Assert.AreEqual(LocalStance.Supports, PublicMemoryRules.Local(Hunger, f, Tuning, false).Single().stance);
        var comparison = PublicMemoryRules.Compare(Hunger, f, Tuning);
        Assert.AreEqual(1, comparison.supports);
        Assert.AreEqual(0, comparison.contradicts, "a different crisis cannot contradict the Hunger promise either");
    }

    [Test]
    public void TheRecordsAreCompared_WithinTheWindow_AndOnlyTheRecords()
    {
        var f = HollowFacts();
        f.tables.Add(new TableFact { key = "table-0", policy = TablePolicy.PublicWelcome, settlement = 1, place = "Ashford", seventh = 30 - Tuning.window - 1 });
        f.admissions.Add(new AdmissionFact { key = "adm-1", settlement = 3, place = "Quietmoor", seventh = 28, people = 6, text = "6 people, origin unknown" });
        var c = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.AreEqual(3, c.contradicts, "two private luxuries and one hoard");
        Assert.AreEqual(1, c.supports, "the admission; the open table is older than the window");
        Assert.IsTrue(c.Gap(Tuning));
        Assert.IsTrue(c.actions.Any(a => a.text.Contains("origin unknown")), "an origin not recorded stays unknown");
        Assert.IsFalse(c.actions.Any(a => a.source == "table:table-0"), "records outside the window are not read");

        f.practiceLived = false;
        var quiet = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.IsTrue(quiet.practiceQuiet);
        Assert.IsFalse(quiet.Gap(Tuning), "a dormant practice shows nothing either way");
    }

    [Test]
    public void AnOfficialAccountCannotEraseTheEvidence()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var f = HollowFacts();
        var before = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        var d = PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, before, PublicMemoryRules.Local(OpenGate, f, Tuning, false), Tuning, 30, "age-of-renewal");
        var account = PublicMemoryRules.Resolve(s, d, Tuning.Dispute("hollow-table"), DisputeResolution.Sponsor, "Oren", new List<ResourceAmount> { new ResourceAmount { resource = "Unity", amount = 20f } }, 31, "age-of-renewal");

        Assert.AreEqual(2, account.version);
        Assert.AreEqual(AccountKind.Sponsored, account.kind);
        StringAssert.Contains("Oren", account.text);
        Assert.AreEqual(AccountKind.Official, s.AccountsOf(link.id).First().kind, "the first version is kept");
        var after = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.AreEqual(before.contradicts, after.contradicts, "what is said changes nothing that was recorded");
        Assert.AreEqual("Honeyed Grain Porridge", f.tables[0].privateLuxuries.Single());
        Assert.AreEqual(3, d.records.Count(r => r.bearing == RecordBearing.Contradicts), "the dispute keeps the records it answered");
        Assert.IsTrue(link.Active, "a sponsored account keeps the link");
        var contested = PublicMemoryRules.Local(OpenGate, f, Tuning, true);
        Assert.AreEqual(LocalStance.Doubts, contested.Single(p => p.settlement == 1).stance, "a place whose records disagree doubts the account");
    }

    [Test]
    public void PlacesStandByWhatWasRecordedThere_NotByWhoTheyAre()
    {
        var f = HollowFacts();
        var places = PublicMemoryRules.Local(OpenGate, f, Tuning, false);
        Assert.AreEqual(places.Single(p => p.settlement == 1).stance, places.Single(p => p.settlement == 2).stance, "the same records, the same standing");
        Assert.AreEqual(LocalStance.Divided, places.Single(p => p.settlement == 1).stance);
        var quiet = places.Single(p => p.settlement == 3);
        Assert.AreEqual(LocalStance.NoRecord, quiet.stance, "nothing recorded: no opinion is assumed");
        StringAssert.Contains("no opinion is assumed", quiet.reason);
        f.tables.Add(new TableFact { key = "table-3", policy = TablePolicy.PublicWelcome, settlement = 3, place = "Quietmoor", seventh = 29 });
        Assert.AreEqual(LocalStance.Supports, PublicMemoryRules.Local(OpenGate, f, Tuning, false).Single(p => p.settlement == 3).stance);
    }

    [Test]
    public void AGapOpensOneDispute_AndOnlyNewerRecordsReopenIt()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var f = HollowFacts();
        var c = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.IsTrue(PublicMemoryRules.ShouldOpen(s, link, c, Tuning, 30));
        var d = PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, c, null, Tuning, 30, null);
        Assert.IsFalse(PublicMemoryRules.ShouldOpen(s, link, c, Tuning, 31), "one open dispute at a time");
        PublicMemoryRules.Resolve(s, d, Tuning.Dispute("hollow-table"), DisputeResolution.Acknowledge, null, null, 32, null);
        Assert.IsNull(PublicMemoryRules.Resolve(s, d, Tuning.Dispute("hollow-table"), DisputeResolution.Revise, null, null, 33, null), "an answered dispute is not answered twice");
        Assert.IsFalse(PublicMemoryRules.ShouldOpen(s, link, c, Tuning, 40), "it rests after an answer");

        // After the rest, the same two private tables (from before the answer, still inside the window) are all there is.
        var later = HollowFacts(32 + Tuning.disputeRest);
        foreach (var t in later.tables) t.seventh = 32;
        later.hoarded.Clear();
        var old = PublicMemoryRules.Compare(OpenGate, later, Tuning);
        Assert.IsTrue(old.Gap(Tuning), "the gap is still there");
        Assert.IsFalse(PublicMemoryRules.ShouldOpen(s, link, old, Tuning, later.now), "the records it already answered do not reopen it");
        var fresh = PublicMemoryRules.Compare(OpenGate, HollowFacts(32 + Tuning.disputeRest), Tuning);
        Assert.IsTrue(PublicMemoryRules.ShouldOpen(s, link, fresh, Tuning, 32 + Tuning.disputeRest), "newer contradicting records can");
        Assert.AreEqual(1, s.disputes.Count);
    }

    [Test]
    public void APolicyReversal_RemovesItsEffect_AndKeepsItsHistory()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var f = HollowFacts();
        f.tables.Clear();
        f.hoarded.Clear();
        f.tables.Add(new TableFact { key = "table-5", policy = TablePolicy.PublicWelcome, settlement = 1, place = "Ashford", seventh = 29 });
        link.last = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        Assert.IsTrue(link.last.Kept);
        var (kept, terms) = PublicMemoryRules.Accord(s, Tuning, 30);
        Assert.AreEqual(Tuning.keptAccord, kept);
        StringAssert.Contains("The Ash-Loaf Table shows The Open Gate", terms.Single().reason);

        Assert.IsTrue(PublicMemoryRules.Lapse(s, link, "The Open Gate is no longer in force.", 31));
        Assert.AreEqual(0f, PublicMemoryRules.Accord(s, Tuning, 31).points, "its effect ends with the law");
        Assert.AreEqual(LinkStatus.Lapsed, s.Link(link.id).status);
        Assert.AreEqual(1, s.AccountsOf(link.id).Count(), "what was said stays");
        Assert.IsFalse(PublicMemoryRules.Lapse(s, link, "again", 32));
    }

    [Test]
    public void RevisingWithdrawsTheClaim_AndItCannotBeMadeAgainAtOnce()
    {
        var s = new PublicMemoryState();
        var link = LinkedGate(s);
        var d = PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, PublicMemoryRules.Compare(OpenGate, HollowFacts(), Tuning), null, Tuning, 30, null);
        var account = PublicMemoryRules.Resolve(s, d, Tuning.Dispute("hollow-table"), DisputeResolution.Revise, null, null, 31, null);
        Assert.AreEqual(AccountKind.Revised, account.kind);
        Assert.AreEqual(LinkStatus.Withdrawn, link.status);
        StringAssert.Contains("no longer stands for The Open Gate", account.text);
        StringAssert.Contains("revised", PublicMemoryRules.WhyNotLink(s, Tuning, OpenGate, "trad-4", "ash-loaf-table", true, true, 1, 40));
        Assert.IsNull(PublicMemoryRules.WhyNotLink(s, Tuning, OpenGate, "trad-4", "ash-loaf-table", true, true, 1, 31 + Tuning.relinkRest));
    }

    [Test]
    public void ALinkNeedsAPromiseInForce_WithinItsAges_AndAPractisedFittingTradition()
    {
        var s = new PublicMemoryState();
        var feast = Tuning.Promise("feast-of-abundance");
        StringAssert.Contains("not in force", PublicMemoryRules.WhyNotLink(s, Tuning, OpenGate, "trad-4", "ash-loaf-table", true, false, 1, 1));
        StringAssert.Contains("Ages I-III", PublicMemoryRules.WhyNotLink(s, Tuning, feast, "trad-4", "ash-loaf-table", true, true, 0, 1), "the Feast of Abundance belongs to Ages I-III");
        StringAssert.Contains("cannot be said to show", PublicMemoryRules.WhyNotLink(s, Tuning, OpenGate, "trad-1", "hearth-tales", true, true, 1, 1));
        StringAssert.Contains("practised now", PublicMemoryRules.WhyNotLink(s, Tuning, OpenGate, "trad-4", "ash-loaf-table", false, true, 1, 1));
        Assert.IsNull(PublicMemoryRules.WhyNotLink(s, Tuning, feast, "trad-4", "ash-loaf-table", true, true, 2, 1));
        LinkedGate(s);
        StringAssert.Contains("linked to a promise already", PublicMemoryRules.WhyNotLink(s, Tuning, feast, "trad-4", "ash-loaf-table", true, true, 2, 1));
    }

    [Test]
    public void RemembranceIsComparedWithTheDedications_AndHowTheyAreKept()
    {
        var f = new PublicFacts { now = 50, practiceName = "The Naming of the Lost" };
        var none = PublicMemoryRules.Compare(Hunger, f, Tuning);
        Assert.AreEqual(0, none.contradicts, "no loss recorded: nothing to answer to");
        Assert.IsFalse(none.Gap(Tuning));

        f.causes.Add(new CauseFact { id = "crisis:age-of-desolation:1", title = "The Inescapable Hunger", subject = "The Inescapable Hunger", cause = MemoryCause.Crisis });
        f.causes.Add(new CauseFact { id = "fall:3", title = "The fall of Emberlee", cause = MemoryCause.SettlementFall });
        var forgotten = PublicMemoryRules.Compare(Hunger, f, Tuning);
        Assert.AreEqual(1, forgotten.contradicts, "only the promised kind of loss (the Crisis) is compared");
        StringAssert.Contains("No memorial is dedicated to The Inescapable Hunger", forgotten.actions.Single(a => a.bearing == RecordBearing.Contradicts).text);

        f.dedications.Add(new DedicationFact { id = "ded-1", evidence = "crisis:age-of-desolation:1", label = "Ash-Loaf", status = DedicationStatus.Active, lastPracticed = 2 });
        f.dedications.Add(new DedicationFact { id = "ded-2", evidence = "crisis:age-of-desolation:1", label = "The Long Table", status = DedicationStatus.Dormant, dormantReason = "Ashford fell" });
        var lapsed = PublicMemoryRules.Compare(Hunger, f, Tuning);
        Assert.AreEqual(2, lapsed.contradicts, "a dedication not kept, and one asleep");
        Assert.IsTrue(lapsed.Gap(Tuning));
        StringAssert.Contains("Ashford fell", lapsed.actions.Single(a => a.source == "dedication:ded-2").text);

        f.dedications[0].lastPracticed = 48;
        f.causes[0].lastQuiet = 49;
        var kept = PublicMemoryRules.Compare(Hunger, f, Tuning);
        Assert.AreEqual(2, kept.supports);
        Assert.IsTrue(kept.Kept);
    }

    [Test]
    public void TheCulturesPartOfTheAccord_IsCappedAndExplained()
    {
        var s = new PublicMemoryState();
        for (int i = 0; i < 3; i++)
        {
            var link = PublicMemoryRules.Link(s, OpenGate, "trad-" + i, "Tradition " + i, 1, null);
            PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, PublicMemoryRules.Compare(OpenGate, HollowFacts(), Tuning), null, Tuning, 30, null);
        }
        var (points, terms) = PublicMemoryRules.Accord(s, Tuning, 30);
        Assert.AreEqual(-Tuning.accordCap, points, "three open disputes are capped");
        Assert.AreEqual(3, terms.Count);
        Assert.IsTrue(terms.All(t => t.reason.Contains("records disagree")));

        var d = s.disputes[0];
        PublicMemoryRules.Resolve(s, d, Tuning.Dispute("hollow-table"), DisputeResolution.Acknowledge, null, null, 31, null);
        var after = PublicMemoryRules.Accord(s, Tuning, 31);
        Assert.IsTrue(after.terms.Any(t => t.reason.Contains("owned the gap") && t.value == Tuning.acknowledgedAccord));
        Assert.IsFalse(PublicMemoryRules.Accord(s, Tuning, 31 + Tuning.acknowledgedSevenths + 1).terms.Any(t => t.reason.Contains("owned the gap")), "an acknowledgement is remembered for a while");
    }

    [Test]
    public void Save_PublicMemoryRoundTripsInsideTheCultureState_AndOlderEnvelopesLoad()
    {
        var culture = new CultureState();
        CultureMigration.Ensure(culture);
        var s = culture.extensions.publicMemory;
        var link = LinkedGate(s);
        var f = HollowFacts();
        link.last = PublicMemoryRules.Compare(OpenGate, f, Tuning);
        var d = PublicMemoryRules.Open(s, Tuning.Dispute("hollow-table"), link, link.last, PublicMemoryRules.Local(OpenGate, f, Tuning, false), Tuning, 30, "age-of-renewal");

        var loaded = (CultureState)SaveStateCodec.Read(SaveStateCodec.Write(culture, typeof(CultureState)), typeof(CultureState));
        var p = loaded.extensions.publicMemory;
        PublicMemoryRules.Ensure(p);
        Assert.AreEqual(1, p.links.Count);
        Assert.AreEqual(DisputeStatus.Open, p.Dispute(d.id).status);
        Assert.AreEqual(3, p.Dispute(d.id).records.Count(r => r.bearing == RecordBearing.Contradicts));
        Assert.AreEqual(LocalStance.Divided, p.Dispute(d.id).places.Single(x => x.settlement == 1).stance);
        Assert.IsFalse(PublicMemoryRules.ShouldOpen(p, p.Link(link.id), p.Link(link.id).last, Tuning, 31), "a reload opens nothing twice");
        Assert.AreEqual("link-2", PublicMemoryRules.Link(p, Hunger, "trad-9", "The Naming of the Lost", 32, null).id, "ids are never given twice");

        var older = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        older.children.RemoveAll(c => c.name == "publicMemory");
        var envelope = (CultureExtensionState)SaveStateCodec.Read(older, typeof(CultureExtensionState));
        Assert.IsNotNull(envelope.publicMemory);
        Assert.AreEqual(0, envelope.publicMemory.links.Count);
    }

    [Test]
    public void TheStoriesAnswerThroughTheCultureGrammar()
    {
        Assert.IsTrue(CultureRules.ParseConsequence("account hollow-table acknowledge", out string verb, out _));
        Assert.AreEqual("account", verb);
        Assert.IsTrue(CultureRules.ParseConsequence("account unbaked-loaf sponsor", out _, out _));
        Assert.IsFalse(CultureRules.ParseConsequence("account hollow-table forget", out _, out _), "only the three answers");
        Assert.AreEqual("The Council Revises Its Promise", CultureRules.Describe("account hollow-table revise", 1));
    }

    [Test]
    public void TheAuthoredPromisesAndDisputes_NameRealLawsAndStories_WithTheirCanon()
    {
        foreach (var p in Tuning.promises)
        {
            if (p.source == PromiseSource.Stance) Assert.IsNotNull(EdictCatalog.Stance(p.stance)?.Option(p.option), $"{p.id}: {p.stance}:{p.option}");
            else Assert.IsFalse(string.IsNullOrEmpty(p.civic));
            Assert.IsNotNull(Tuning.DisputeFor(p.kind), $"{p.id} has a dispute for its kind");
            Assert.IsFalse(string.IsNullOrEmpty(p.vault) || string.IsNullOrEmpty(p.canonNote), p.id);
            foreach (var d in p.traditions) Assert.IsNotNull(new TraditionTuning().Definition(d), $"{p.id}: {d}");
        }
        Assert.AreEqual(CanonStatus.ExplicitCanon, Tuning.Promise("feast-of-abundance").canon);
        Assert.AreEqual(2, Tuning.disputes.Count, "two authored disputes");
        string ink = File.ReadAllText("Assets/Resources/Events/PublicMemory.ink");
        foreach (var d in Tuning.disputes)
        {
            StringAssert.Contains($"=== {d.story} ===", ink);
            StringAssert.Contains($"conditions: culture:dispute:{d.id}", ink);
            foreach (var answer in new[] { "acknowledge", "revise", "sponsor" })
            {
                StringAssert.Contains($"consequences:culture:account {d.id} {answer}", ink);
                StringAssert.Contains($"requirements:culture:answer:{d.id}:{answer}", ink);
            }
        }
        Assert.IsTrue(File.Exists("Assets/Resources/Events/PublicMemory.json"), "compiled with the project's Ink compiler");
    }
}
