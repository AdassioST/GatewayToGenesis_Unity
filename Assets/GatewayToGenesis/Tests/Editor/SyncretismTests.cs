using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Syncretism and ruin inheritance with no scene (T07, SyncretismRules): blends only by authored rule, only where the
/// parents met and are kept; side by side always possible; replace refused where a community keeps a parent as its own;
/// depth capped; a ruin's records decide what it can justify, a ruin digested once, a ruin nobody knows offers only a
/// guess; renewal needs an Age actually passed; a tradition merged into a blend takes no credit of its own.
/// </summary>
public class SyncretismTests
{
    private static readonly SyncretismTuning T = new SyncretismTuning();

    private static ParentPresence Tradition(string def, int settlement, bool sustained = true, string instance = null) =>
        new ParentPresence { key = "tradition:" + def, settlement = settlement, sustained = sustained, instance = instance ?? $"trad-{def}-{settlement}" };

    private static ParentPresence Custom(string practice, int settlement, bool kept = true, bool sustained = true, int from = -1) =>
        new ParentPresence { key = "local:" + practice, settlement = settlement, kept = kept, sustained = kept && sustained, fromSettlement = from, exchanged = from >= 0 && from != settlement, provenance = from >= 0 ? $"provenance:{settlement}:{practice}" : null };

    private static List<HybridOffer> Offers(IList<ParentPresence> presence, IList<ContactLink> links = null, SyncretismState s = null, int age = 0, int now = 100,
        System.Func<string, int, bool> resultAt = null) =>
        SyncretismRules.Offers(T, presence, links ?? new List<ContactLink>(), age, _ => true, _ => true, resultAt ?? ((_, __) => false), s ?? new SyncretismState(), now, k => k, p => p < 0 ? "the nation" : "place " + p);

    // ===== MEETINGS =====

    [Test]
    public void TwoTraditionsKeptTogether_MayBlend_OnceWhereTheyMet()
    {
        var presence = new List<ParentPresence> { Tradition("ash-loaf-table", -1), Tradition("naming-of-the-lost", -1), Custom("crossing-songs", 3) };
        var offers = Offers(presence);
        var loaf = offers.Single(o => o.rule.id == "loaf-of-names");
        Assert.AreEqual(-1, loaf.settlement, "kept by the nation: offered to the nation, not once per settlement");
        CollectionAssert.Contains(loaf.evidence, "together:-1");
        Assert.IsTrue(loaf.evidence.Any(e => e.StartsWith("tradition:")), "the traditions themselves are the evidence");
    }

    [Test]
    public void TwoTraditionsWithoutContact_CannotBlend_EvenWithARoadAlone()
    {
        var song = Tradition("evening-song", 1);
        var fords = Custom("crossing-songs", 2);
        var presence = new List<ParentPresence> { song, fords };
        Assert.IsEmpty(Offers(presence).Where(o => o.rule.id == "crossing-chorus"), "kept apart, no road");
        StringAssert.Contains("never met", SyncretismRules.WhyNotMet(T.Hybrid("crossing-chorus"), 1, presence, new List<ContactLink>(), k => k, out _, out _, out _, out _));
        var road = new List<ContactLink> { new ContactLink { a = 1, b = 2, open = true } };
        Assert.IsEmpty(Offers(presence, road).Where(o => o.rule.id == "crossing-chorus"), "a road nobody has crossed is no meeting");

        // Something crossed that road (a custom of place 2 reached place 1 by it): now they have met.
        presence.Add(Custom("evening-song", 1, kept: false, from: 2));
        var both = Offers(presence, road).Where(o => o.rule.id == "crossing-chorus").ToList();
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, both.Select(o => o.settlement), "each end of the road met the other: each decides for itself");
        var met = both.Single(o => o.settlement == 1);
        Assert.IsTrue(met.evidence.Any(e => e.StartsWith("exchange:2->1")), string.Join(", ", met.evidence));
        StringAssert.Contains("road", met.contact);

        // The road is cut: future exchange stops (what was learned stays, but no new blend across it).
        road[0].open = false;
        Assert.IsEmpty(Offers(presence, road).Where(o => o.rule.id == "crossing-chorus"));
    }

    [Test]
    public void AParentNotKeptLongEnough_IsNotBlendedYet()
    {
        var presence = new List<ParentPresence> { Tradition("ash-loaf-table", -1), Tradition("naming-of-the-lost", -1, sustained: false) };
        Assert.IsEmpty(Offers(presence));
        StringAssert.Contains("not kept long enough", SyncretismRules.WhyNotMet(T.Hybrid("loaf-of-names"), -1, presence, null, k => k, out _, out _, out _, out _));
        var gatheredOnce = new List<ParentPresence> { Tradition("evening-song", 4), Custom("crossing-songs", 4, sustained: false) };
        Assert.IsEmpty(Offers(gatheredOnce), "a custom gathered for only once is not yet a custom to blend");
    }

    [Test]
    public void ADecision_ClosesTheMeeting_SideBySideIsOfferedAgainLater()
    {
        var presence = new List<ParentPresence> { Tradition("ash-loaf-table", -1), Tradition("naming-of-the-lost", -1) };
        var s = new SyncretismState();
        s.decisions.Add(new SyncretismDecision { key = SyncretismRules.OfferKey("loaf-of-names", -1), mode = SyncretismMode.SideBySide, stamp = new CultureStamp { cultureSeventh = 100 } });
        Assert.IsEmpty(Offers(presence, s: s, now: 100 + T.reofferSevenths - 1));
        Assert.AreEqual(1, Offers(presence, s: s, now: 100 + T.reofferSevenths).Count, "kept side by side a while: offered again");
        s.decisions.Add(new SyncretismDecision { key = SyncretismRules.OfferKey("loaf-of-names", -1), mode = SyncretismMode.Adapt, stamp = new CultureStamp { cultureSeventh = 200 } });
        Assert.IsEmpty(Offers(presence, s: s, now: 999), "blended: never offered again");
        Assert.IsEmpty(Offers(presence, resultAt: (d, p) => d == "loaf-of-names" && p == -1), "its new form is kept there already");
    }

    [Test]
    public void TheModes_SideBySideIsAlwaysViable_ReplaceRespectsWhatACommunityKeeps()
    {
        var a = Tradition("evening-song", 2);
        var b = Custom("crossing-songs", 2);
        var offer = new HybridOffer { rule = T.Hybrid("crossing-chorus"), settlement = 2, place = "Riverside", a = a, b = b };
        Assert.IsNull(SyncretismRules.WhyNotMode(offer, SyncretismMode.SideBySide, 0f, T, k => k, out float cost), "free, whatever the stores");
        Assert.AreEqual(0f, cost);
        StringAssert.Contains("Needs 20 Unity", SyncretismRules.WhyNotMode(offer, SyncretismMode.Adapt, 5f, T, k => k, out _));
        Assert.IsNull(SyncretismRules.WhyNotMode(offer, SyncretismMode.Replace, 100f, T, k => k, out cost));
        Assert.AreEqual(10f, cost);
        a.recognized = true;
        SyncretismRules.WhyNotMode(offer, SyncretismMode.Replace, 100f, T, k => k, out cost);
        Assert.AreEqual(10f + T.recognizedPremium, cost, "the nation recognised it: replacing it costs more");
        a.preserved = true;
        StringAssert.Contains("keep tradition:evening-song as their own", SyncretismRules.WhyNotMode(offer, SyncretismMode.Replace, 100f, T, k => k, out _), "a local answer");
        Assert.IsNull(SyncretismRules.WhyNotMode(offer, SyncretismMode.Adapt, 100f, T, k => k, out _), "but they would blend beside it");
        var national = new HybridOffer { rule = T.Hybrid("crossing-chorus"), settlement = 2, a = Tradition("evening-song", -1), b = b };
        StringAssert.Contains("not kept here", SyncretismRules.WhyNotMode(national, SyncretismMode.Replace, 100f, T, k => k, out _), "one settlement cannot replace the nation's own");

        var (retained, lost) = SyncretismRules.Outcome(offer, SyncretismMode.Replace, k => k, "The Crossing Chorus");
        Assert.AreEqual(2, retained.Count);
        Assert.IsTrue(lost.Any(l => l.Contains("tradition:evening-song")), "what is let go is said");
        (retained, lost) = SyncretismRules.Outcome(offer, SyncretismMode.SideBySide, k => k, "The Crossing Chorus");
        Assert.IsTrue(retained.All(r => r.EndsWith("as it was")));
    }

    [Test]
    public void DepthIsCapped_AndRulesKeepToTheirAges()
    {
        Assert.AreEqual(0, SyncretismRules.Depth("evening-song", T));
        Assert.AreEqual(1, SyncretismRules.Depth("loaf-of-names", T));
        var deep = new SyncretismTuning { maxDepth = 1 };
        deep.hybrids.Add(new HybridRule { id = "deeper", parentA = "tradition:loaf-of-names", parentB = "tradition:evening-song", result = "deeper-form" });
        Assert.AreEqual(2, SyncretismRules.Depth("deeper-form", deep));
        StringAssert.Contains("reworked 2 times", SyncretismRules.WhyNotRule(deep.Hybrid("deeper"), 0, _ => true, _ => true, deep));
        deep.hybrids.Add(new HybridRule { id = "loop", parentA = "tradition:loop-form", parentB = "tradition:evening-song", result = "loop-form" });
        Assert.IsNotNull(SyncretismRules.WhyNotRule(deep.Hybrid("loop"), 0, _ => true, _ => true, deep), "a rule that feeds itself is never offered");

        StringAssert.Contains("belonged", SyncretismRules.WhyNotRule(T.Hybrid("first-fruit-songs"), 1, _ => true, _ => true, T), "the first fruit's songs are Age 0's");
        Assert.IsNull(SyncretismRules.WhyNotRule(T.Hybrid("first-fruit-songs"), 0, _ => true, _ => true, T));
    }

    [Test]
    public void TheAuthoredBlends_AreThreeAndNamed_WithAMemorialTableAndACommunalPerformance()
    {
        var traditions = new TraditionTuning();
        Assert.AreEqual(3, T.hybrids.Count);
        foreach (var r in T.hybrids)
        {
            var d = traditions.Definition(r.result);
            Assert.IsNotNull(d, r.id);
            Assert.IsTrue(d.linkedOnly, $"{r.id}: made only by the people's choice, never by occurrences alone");
            Assert.IsFalse(string.IsNullOrEmpty(r.canonSource), r.id);
            Assert.AreNotEqual(r.parentA, r.parentB);
        }
        Assert.IsTrue(traditions.Definition("loaf-of-names").food, "a culinary memorial tradition");
        Assert.IsTrue(traditions.Definition("crossing-chorus").triggers.Any(t => t.kind == CulturalOccurrenceKind.Performance), "a communal performance tradition");
    }

    // ===== RUINS =====

    private static RuinRecord Ruin(bool investigated = true) => new RuinRecord
    {
        id = 4, name = "Ashford", investigated = investigated, kind = SettlementKind.Town, foundedAge = 0, fallenAge = 0, cause = WorldRuins.Pillage,
        civic = "Cooking Guilds", civicFamily = "Agromagical", binding = "Crystal", groundFamily = "Domestication",
    };

    [Test]
    public void ARuinsRecords_DecideWhatItCanJustify()
    {
        var h = SyncretismRules.Read(Ruin());
        CollectionAssert.AreEqual(new[] { "Agromagical" }, h.families, "its civic, and not its binding or its fall");
        Assert.IsFalse(h.unknown);
        Assert.IsTrue(h.lost.Any(l => l.Contains("Crystal")), "the binding is remembered, not taken in");
        var q = new InheritanceRequest { ruin = 4, choice = InheritanceChoice.AdaptWays, family = "Militant" };
        StringAssert.Contains("Nothing in the records of Ashford speaks of the Militant ways", SyncretismRules.WhyNotInherit(h, q, null), "an unrelated history justifies nothing");
        q.family = "Agromagical";
        Assert.IsNull(SyncretismRules.WhyNotInherit(h, q, null));
        StringAssert.Contains("records of Ashford are known", SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 4, choice = InheritanceChoice.ReadTheStones }, null), "no guessing where the records speak");

        var digested = Ruin();
        digested.digested = true;
        StringAssert.Contains("digested already", SyncretismRules.WhyNotInherit(SyncretismRules.Read(digested), q, null), "a ruin feeds one reform");

        var adopted = Ruin();
        adopted.civicAdopted = true;
        var ha = SyncretismRules.Read(adopted);
        StringAssert.Contains("adopted from these ruins already", SyncretismRules.WhyNotInherit(ha, new InheritanceRequest { ruin = 4, choice = InheritanceChoice.PreserveCivic }, null));
        Assert.IsNull(SyncretismRules.WhyNotInherit(ha, q, null), "adopting its civic leaves its ways to digest, once (the policy)");

        var unread = SyncretismRules.Read(Ruin(investigated: false));
        Assert.IsEmpty(unread.families);
        StringAssert.Contains("Investigate", SyncretismRules.WhyNotInherit(unread, q, null));
    }

    [Test]
    public void ARuinNobodyKnows_OffersOnlyAnHonestGuess()
    {
        var r = new RuinRecord { id = 9, name = "a city of the Old World", investigated = true, ancient = true, binding = "Void", groundFamily = "Esoteric" };
        var h = SyncretismRules.Read(r);
        Assert.IsTrue(h.unknown, "a binding alone says nothing of how they lived");
        Assert.IsEmpty(h.families);
        StringAssert.Contains("Nobody knows", h.summary);
        StringAssert.Contains("Nothing of who lived", SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 9, choice = InheritanceChoice.AdaptWays, family = "Esoteric" }, null));
        Assert.IsNull(SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 9, choice = InheritanceChoice.ReadTheStones }, null));
        Assert.IsTrue(h.clues.Any(c => c.kind == RuinClueKind.Ground && c.text.Contains("guess")));
        Assert.Less(T.uncertainShare, 1f, "and a smaller one");
        r.groundFamily = null;
        StringAssert.Contains("suggests nothing", SyncretismRules.WhyNotInherit(SyncretismRules.Read(r), new InheritanceRequest { ruin = 9, choice = InheritanceChoice.ReadTheStones }, null));
    }

    [Test]
    public void WhatARuinsPeopleKept_CanBeTakenUpOnce_AndMissingDataNeverCrashes()
    {
        var r = Ruin();
        r.practices.Add(("crossing-songs", "Crossing Songs", "Trading", true));
        r.practices.Add(("golden-fruit", "Rite of the First Golden Fruit", "Agromagical", false));
        r.traditions.Add(("trad-7", "hearth-tales", "Tales at the Hearth", "Auric", true));
        var h = SyncretismRules.Read(r);
        CollectionAssert.AreEquivalent(new[] { "Agromagical", "Trading", "Auric" }, h.families, "the customs and traditions they kept speak too");
        Assert.IsTrue(h.lost.Any(l => l.Contains("only met")), "a custom they had only met is not theirs to give");
        var tales = h.clues.Single(c => c.kind == RuinClueKind.Tradition);
        Assert.AreEqual("ruin:4:tradition:trad-7", tales.evidence);
        var q = new InheritanceRequest { ruin = 4, choice = InheritanceChoice.Revive, evidence = tales.evidence };
        Assert.IsNull(SyncretismRules.WhyNotInherit(h, q, null));
        var s = new SyncretismState();
        s.decisions.Add(new SyncretismDecision { key = SyncretismRules.RuinKey(4, InheritanceChoice.Revive, tales.evidence) });
        StringAssert.Contains("already", SyncretismRules.WhyNotInherit(h, q, s));
        var custom = h.clues.Single(c => c.kind == RuinClueKind.Practice);
        StringAssert.Contains("choose one", SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 4, choice = InheritanceChoice.Revive, evidence = custom.evidence, settlement = -1 }, s), "a custom is a settlement's");

        r.onMap = false;
        h = SyncretismRules.Read(r);
        Assert.IsTrue(h.lost.Any(l => l.Contains("gone from the map")));
        StringAssert.Contains("no longer stand", SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 4, choice = InheritanceChoice.PreserveCivic }, null));
        StringAssert.Contains("no longer stand", SyncretismRules.WhyNotInherit(h, new InheritanceRequest { ruin = 4, choice = InheritanceChoice.AdaptWays, family = "Agromagical" }, null));
        Assert.IsNull(SyncretismRules.WhyNotInherit(h, q, null), "what they kept can still be taken up");

        var nothing = SyncretismRules.Read(null);
        Assert.IsTrue(nothing.unknown);
        Assert.IsNotNull(SyncretismRules.WhyNotInherit(nothing, q, null));
        Assert.IsNotNull(SyncretismRules.WhyNotInherit(h, null, null));
    }

    // ===== RENEWAL AND MERGING =====

    [Test]
    public void Renewal_NeedsAnAgeActuallyPassed_AndIsCapped()
    {
        StringAssert.Contains("Only a tradition", SyncretismRules.WhyNotRenew(null, 3, T));
        var v = new VariantRecord { tradition = "trad-9", depth = 1, agePasses = 2 };
        StringAssert.Contains("No Age has passed", SyncretismRules.WhyNotRenew(v, 2, T), "the Age's number is not enough: a passage must be in the history");
        Assert.IsNull(SyncretismRules.WhyNotRenew(v, 3, T));
        v.depth = T.maxDepth;
        StringAssert.Contains("reworked", SyncretismRules.WhyNotRenew(v, 3, T));
    }

    [Test]
    public void ATraditionMergedIntoABlend_TakesNoCreditOfItsOwn_WhileTheBlendDoes()
    {
        var traditions = new TraditionTuning();
        var state = new TraditionState();
        var parent = TraditionRules.Create(state, traditions.Definition("ash-loaf-table"), -1, null, new TraditionOrigin(), 0);
        var blend = TraditionRules.Create(state, traditions.Definition("loaf-of-names"), -1, null, new TraditionOrigin(), 0);
        parent.mergedInto = blend.id;
        parent.stage = TraditionStage.Dormant;
        var bake = new CulturalOccurrence
        {
            key = "cook:ash-loaf:1", kind = CulturalOccurrenceKind.Production, subject = CultureEntityRef.Of(CultureEntityKind.Recipe, "ash-loaf"), settlement = -1,
            quantity = 1f, unit = CultureQuantityUnit.Batches, stamp = new CultureStamp { cultureSeventh = 1 },
        };
        TraditionRules.Advance(state, traditions.definitions, new[] { bake }, 1, traditions);
        Assert.AreEqual(0, parent.participations, "it lives on in the blend");
        Assert.AreEqual(1, blend.participations);
        Assert.AreEqual(1, state.instances.Count(i => i.definition == "ash-loaf-table"), "and no second Ash-Loaf Table arises beside it");
        Assert.AreEqual(TraditionStage.Dormant, parent.stage);
    }

    [Test]
    public void Ensure_RepairsAnOldOrPartialState()
    {
        var s = new SyncretismState { nextId = 1, decisions = { new SyncretismDecision { id = "syn-5", evidence = null, lost = null }, null }, variants = { new VariantRecord { tradition = null } } };
        SyncretismRules.Ensure(s);
        Assert.AreEqual(6, s.nextId);
        Assert.AreEqual(1, s.decisions.Count);
        Assert.IsNotNull(s.decisions[0].evidence);
        Assert.IsEmpty(s.variants, "a variant of nothing is dropped");
        Assert.AreEqual(SyncretismState.CurrentVersion, s.version);
    }
}
