using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Ensembles and performance (T08) with no scene: deterministic evaluation (readiness, the ensemble, the Legends' own
/// readings of each other, the place), eligibility (the later Ages' chorus locked early), the booking lifecycle
/// (lapse, walk-away, a stopped festival, completion once), echoes (bounded, expiring, removed with their source),
/// the Coherence overlay, and that co-performance never makes a significant tie. Pure, so they also run outside Unity.
/// </summary>
public class PerformanceTests
{
    private static readonly PerformanceTuning Tuning = new PerformanceTuning();
    private static RepertoireSpec Song => Tuning.Repertoire("evening-song");
    private static RepertoireSpec Named => Tuning.Repertoire("song-of-the-named");
    private static RepertoireSpec Chorus => Tuning.Repertoire("polyphonic-chorus");

    private static CastMember Voice(string name, ComposureState c = ComposureState.Pristine, int binding = 5) => new CastMember { name = name, composure = c, binding = binding };

    private static CastTie Bond(string from, string to, int stage, bool wound = false) => new CastTie { from = from, to = to, stage = stage, significant = true, sharedWound = wound, encounters = 4 };

    private static PerformanceContext Context(PerformanceIntent intent, params CastMember[] cast) =>
        new PerformanceContext { repertoire = intent == PerformanceIntent.Remembrance ? Named : Song, intent = intent, cast = cast.ToList(), place = "Ashford" };

    // ===== EVALUATION =====

    [Test]
    public void TwoCasts_ProduceDifferentExplainedOutcomes()
    {
        var duet = Context(PerformanceIntent.Reassurance, Voice("Vaelia"), Voice("Oren"));
        duet.ties = new List<CastTie> { Bond("Vaelia", "Oren", 2), Bond("Oren", "Vaelia", 2) };
        var rivals = Context(PerformanceIntent.Reassurance, Voice("Vaelia"), Voice("Sable", ComposureState.Fractured));
        rivals.ties = new List<CastTie> { Bond("Vaelia", "Sable", -2), Bond("Sable", "Vaelia", -1) };
        var a = PerformanceRules.Evaluate(duet, Tuning);
        var b = PerformanceRules.Evaluate(rivals, Tuning);
        Assert.AreEqual(PerformanceBand.Moving, a.band, string.Join("\n", a.Lines));
        Assert.AreEqual(PerformanceBand.Steady, b.band, string.Join("\n", b.Lines));
        Assert.IsTrue(a.Lines.Any(l => l.Contains("Vaelia and Oren: Sworn Duet / Sworn Duet")), "the tie is named");
        Assert.IsTrue(b.Lines.Any(l => l.Contains("Forged Rivalry") && l.Contains("-")), "a dissonant tie is heard, and costs");
        Assert.IsTrue(b.Lines.Any(l => l.Contains("Sable Fractured")), "each voice's readiness is explained");
        Assert.AreEqual(a.score, PerformanceRules.Evaluate(duet, Tuning).score, 1e-6f, "deterministic");
    }

    [Test]
    public void AnEnsemble_CarriesAFalteringVoice_BetterThanASoloist()
    {
        var solo = PerformanceRules.Evaluate(Context(PerformanceIntent.Celebration, Voice("Sable", ComposureState.Fractured)), Tuning);
        var trio = PerformanceRules.Evaluate(Context(PerformanceIntent.Celebration, Voice("Sable", ComposureState.Fractured), Voice("Vaelia"), Voice("Oren")), Tuning);
        Assert.Greater(trio.score, solo.score + 0.15f, "Waltz: an orchestra over a soloist who can miss a note");
        Assert.IsTrue(trio.Lines.Any(l => l.Contains("the weakest counts for 1/3")));
        Assert.IsTrue(trio.Lines.Any(l => l.Contains("3 voices together")));
    }

    [Test]
    public void ThePlace_AndASharedWound_AreHeard()
    {
        var plain = Context(PerformanceIntent.Remembrance, Voice("Vaelia", binding: 5), Voice("Oren", binding: 5));
        plain.ties = new List<CastTie> { Bond("Vaelia", "Oren", 1), Bond("Oren", "Vaelia", 1) };
        var known = Context(PerformanceIntent.Remembrance, Voice("Vaelia", binding: 5), Voice("Oren", binding: 5));
        known.ties = new List<CastTie> { Bond("Vaelia", "Oren", 1, wound: true), Bond("Oren", "Vaelia", 1, wound: true) };
        known.lossHere = "The Inescapable Hunger";
        known.rootedness = 1f;
        float a = PerformanceRules.Evaluate(plain, Tuning).score, b = PerformanceRules.Evaluate(known, Tuning).score;
        Assert.AreEqual(Tuning.sharedWoundRemembrance * 2 + Tuning.lossHere + Tuning.rootedness, b - a, 1e-4f);
        var troubled = Context(PerformanceIntent.Reassurance, Voice("Vaelia"));
        troubled.strain = 60f;
        Assert.IsTrue(PerformanceRules.Evaluate(troubled, Tuning).Lines.Any(l => l.Contains("too troubled")));
        var home = Context(PerformanceIntent.Reassurance, Voice("Vaelia"));
        home.keepsCustom = true;
        Assert.AreEqual(Tuning.keepsCustom, PerformanceRules.Evaluate(home, Tuning).score - PerformanceRules.Evaluate(Context(PerformanceIntent.Reassurance, Voice("Vaelia")), Tuning).score, 1e-4f);
    }

    [Test]
    public void Ties_AreBounded_SoNoCastRunsAway()
    {
        var names = new[] { "A", "B", "C", "D", "E" };
        var c = Context(PerformanceIntent.Celebration, names.Select(n => Voice(n)).ToArray());
        foreach (var x in names) foreach (var y in names.Where(y => y != x)) c.ties.Add(Bond(x, y, 3));
        var e = PerformanceRules.Evaluate(c, Tuning);
        Assert.IsTrue(e.Lines.Any(l => l.Contains("within their bound")));
        Assert.LessOrEqual(e.score, 1f);
    }

    // ===== ELIGIBILITY =====

    [Test]
    public void TheLaterAgesChorus_IsLockedEarly_AndRestorationNeedsIt()
    {
        Func<string, bool> none = _ => false, all = _ => true;
        StringAssert.Contains("Age IV-VI", PerformanceRules.WhyNotRepertoire(Chorus, PerformanceIntent.Restoration, 0, all, all, false, 5), "advanced magic is inaccessible in an early Age");
        StringAssert.Contains("Polyphonic Choral Singers", PerformanceRules.WhyNotRepertoire(Chorus, PerformanceIntent.Restoration, 4, all, none, false, 5), "its institution must be in force");
        StringAssert.Contains("3 voices", PerformanceRules.WhyNotRepertoire(Chorus, PerformanceIntent.Restoration, 4, all, all, false, 2));
        Assert.IsNull(PerformanceRules.WhyNotRepertoire(Chorus, PerformanceIntent.Restoration, 4, all, all, false, 3));
        StringAssert.Contains("not performed for restoration", PerformanceRules.WhyNotRepertoire(Song, PerformanceIntent.Restoration, 4, all, all, false, 3), "an early song is no map-healing spell");
        StringAssert.Contains("loss the people remember", PerformanceRules.WhyNotRepertoire(Named, PerformanceIntent.Remembrance, 0, all, all, false, 1));
        Assert.IsNull(PerformanceRules.WhyNotRepertoire(Named, PerformanceIntent.Remembrance, 0, all, all, true, 1));
        foreach (var r in Tuning.repertoires)
        {
            Assert.IsFalse(string.IsNullOrEmpty(r.canonSource), $"{r.id} cites its vault note");
            Assert.IsNotEmpty(r.intents);
        }
        Assert.AreEqual(2, Tuning.repertoires.Where(r => r.minAge == 0).SelectMany(r => r.intents).Distinct().Count(i => i == PerformanceIntent.Reassurance || i == PerformanceIntent.Remembrance), "two early repertoires with different intents");
    }

    [Test]
    public void EarlyEchoes_NeverLendCoherence_EvenForRestoration()
    {
        var b = new PerformanceBooking { id = "perf-1", settlement = 1, place = "Ashford", repertoire = "polyphonic-chorus", intent = PerformanceIntent.Restoration };
        var locked = PerformanceRules.EchoOf(b, Chorus, PerformanceBand.Resonant, 10, 0, false, Tuning);
        Assert.AreEqual(0f, locked.coherence, "no Coherence without its Age and civic");
        var open = PerformanceRules.EchoOf(b, Chorus, PerformanceBand.Resonant, 10, 0, true, Tuning);
        Assert.Greater(open.coherence, 0f);
    }

    // ===== BOOKINGS =====

    private static BookingObservation Here(bool festival = false) => new BookingObservation { unitExists = true, inSettlement = true, settlementStanding = true, festivalRunning = festival, voices = 2 };

    [Test]
    public void ABooking_Lapses_IsCalledOffWhenThePartyLeaves_AndAStoppedFestivalGivesNothing()
    {
        var s = PerformanceRules.Ensure(null);
        var b = PerformanceRules.Book(s, 7, "Vaelia's Cultural Party", 1, "Ashford", "evening-song", PerformanceIntent.Reassurance, new[] { "Vaelia" }, null, 10);
        Assert.AreSame(b, PerformanceRules.ForUnit(s, 7));
        Assert.AreSame(b, PerformanceRules.AtSettlement(s, 1));
        Assert.IsNull(PerformanceRules.Check(b, Here(), 12, Tuning));
        StringAssert.Contains("within 7 Sevenths", PerformanceRules.Check(b, Here(), 17, Tuning), "a plan with no festival lapses");
        var walked = Here();
        walked.inSettlement = false;
        StringAssert.Contains("left Ashford", PerformanceRules.Check(b, walked, 11, Tuning));
        Assert.IsNull(PerformanceRules.Check(b, Here(festival: true), 11, Tuning));
        Assert.AreEqual(PerformanceStatus.Performing, b.status, "its festival began");
        StringAssert.Contains("stopped before its end", PerformanceRules.Check(b, Here(), 12, Tuning));
        var r = PerformanceRules.End(s, b, PerformanceStatus.Cancelled, "stopped", "The Evening Song", 12, null, Tuning);
        Assert.IsNull(PerformanceRules.ForUnit(s, 7), "the reservation is released");
        Assert.IsEmpty(s.echoes, "nothing given");
        Assert.AreEqual(0, s.completed);
        Assert.AreEqual(PerformanceStatus.Cancelled, s.history.Single().status);
        StringAssert.Contains("called off", r.text);
        Assert.IsNull(PerformanceRules.End(s, b, PerformanceStatus.Cancelled, "again", null, 13, null, Tuning), "ended once");
    }

    [Test]
    public void Completion_HappensOnce_AndAReplayChangesNothing()
    {
        var s = PerformanceRules.Ensure(null);
        var b = PerformanceRules.Book(s, 7, "Party", 1, "Ashford", "evening-song", PerformanceIntent.Celebration, new[] { "Vaelia", "Oren" }, null, 10);
        var e = PerformanceRules.Evaluate(Context(PerformanceIntent.Celebration, Voice("Vaelia"), Voice("Oren")), Tuning);
        var echo = PerformanceRules.EchoOf(b, Song, e.band, 5, 11, false, Tuning);
        Assert.IsNotNull(PerformanceRules.Complete(s, b, "The Evening Song", new[] { "Vaelia", "Oren" }, e, echo, 11, null, Tuning));
        Assert.IsNull(PerformanceRules.Complete(s, b, "The Evening Song", new[] { "Vaelia", "Oren" }, e, echo, 11, null, Tuning), "a replayed completion does nothing");
        Assert.AreEqual(1, s.completed);
        Assert.AreEqual(1, s.echoes.Count);
        Assert.AreEqual(1, s.history.Count);
        Assert.IsNotEmpty(s.history[0].factors, "the history keeps why");
    }

    [Test]
    public void AFalteringPerformance_LeavesNoEcho()
    {
        var b = new PerformanceBooking { id = "perf-1", settlement = 1, place = "Ashford", intent = PerformanceIntent.Reassurance };
        Assert.IsNull(PerformanceRules.EchoOf(b, Song, PerformanceBand.Faltering, 1, 0, false, Tuning));
        Assert.AreEqual(0, PerformanceRules.AffectionOf(PerformanceBand.Faltering, Tuning));
    }

    // ===== ECHOES =====

    private static PerformanceEcho Echo(string id, int settlement, PerformanceBand band, PerformanceIntent intent, int now)
    {
        var b = new PerformanceBooking { id = id, settlement = settlement, place = "Town " + settlement, repertoire = "evening-song", intent = intent };
        return PerformanceRules.EchoOf(b, Song, band, settlement, now, false, Tuning);
    }

    [Test]
    public void Echoes_AreBounded_Expire_AndOnePerSettlement()
    {
        var s = PerformanceRules.Ensure(null);
        var e1 = Echo("perf-1", 1, PerformanceBand.Moving, PerformanceIntent.Celebration, 0);
        Assert.AreEqual(1f, e1.morale, "Moving: the intent's morale");
        Assert.AreEqual(2f, Echo("x", 1, PerformanceBand.Resonant, PerformanceIntent.Celebration, 0).morale, "Resonant: half again, rounded");
        Assert.AreEqual(0f, Echo("x", 1, PerformanceBand.Steady, PerformanceIntent.Celebration, 0).morale, "only a moving performance lifts spirits");
        s.echoes.Add(e1);
        s.echoes.Add(Echo("perf-2", 2, PerformanceBand.Resonant, PerformanceIntent.Remembrance, 1));
        s.echoes.Add(Echo("perf-3", 3, PerformanceBand.Moving, PerformanceIntent.Celebration, 2));
        var inForce = PerformanceRules.MoraleEchoes(s, Tuning);
        Assert.AreEqual(Tuning.maxMoraleEchoes, inForce.Count, "only the strongest few apply their morale");
        Assert.AreEqual("perf-2", inForce[0].id);
        var gone = PerformanceRules.Expire(s, e1.until, id => true);
        CollectionAssert.Contains(gone.Select(g => g.id).ToList(), "perf-1");
        var fallen = PerformanceRules.Expire(s, 3, id => id != 2);
        Assert.AreEqual("perf-2", fallen.Single().id, "an echo in a fallen settlement fades");
        Assert.AreEqual(1, s.echoes.Count);

        var b = PerformanceRules.Book(s, 9, "Party", 3, "Town 3", "evening-song", PerformanceIntent.Reassurance, new[] { "Vaelia" }, null, 5);
        var e = PerformanceRules.Evaluate(Context(PerformanceIntent.Reassurance, Voice("Vaelia")), Tuning);
        PerformanceRules.Complete(s, b, "The Evening Song", new[] { "Vaelia" }, e, PerformanceRules.EchoOf(b, Song, e.band, 3, 5, false, Tuning), 5, null, Tuning);
        Assert.AreEqual(1, s.echoes.Count(x => x.settlement == 3), "a new echo replaces the old one in the same town");
    }

    [Test]
    public void TheCoherenceOverlay_IsBounded_Local_AndGoneWithItsEcho()
    {
        var s = PerformanceRules.Ensure(null);
        Func<int, int> distance = cell => Math.Abs(cell - 100);
        Assert.AreEqual(0f, PerformanceRules.CoherenceAt(s, distance, Tuning));
        for (int i = 0; i < 4; i++) s.echoes.Add(new PerformanceEcho { id = "c" + i, settlement = i, coherence = 0.15f, radius = 1, cell = 100, until = 50 });
        Assert.AreEqual(Tuning.coherenceCap, PerformanceRules.CoherenceAt(s, distance, Tuning), 1e-4f, "capped, however many choruses");
        Assert.AreEqual(0f, PerformanceRules.CoherenceAt(s, cell => 5, Tuning), "only within its radius");
        PerformanceRules.Expire(s, 50, id => true);
        Assert.AreEqual(0f, PerformanceRules.CoherenceAt(s, distance, Tuning), "removing the source removes its effect");
    }

    // ===== RELATIONSHIPS =====

    [Test]
    public void CoPerformance_NeverMakesASignificantTie_NorChangesAStage()
    {
        int affection = PerformanceRules.AffectionOf(PerformanceBand.Resonant, Tuning);
        var stranger = new LegendRelationship { other = "Oren" };
        var bonded = new LegendRelationship { other = "Oren", thread = "Strand", stage = 1, affection = 84 };
        for (int i = 0; i < 60; i++)
        {
            LegendRelationshipRules.Experience(stranger, "performance:perf-" + i, "sang", 0, "Strand", false, true, affection);
            LegendRelationshipRules.Experience(bonded, "performance:perf-" + i, "sang", 0, "Strand", false, true, affection);
        }
        Assert.IsFalse(stranger.Significant, "no shared wound, no direction: never a significant tie (capacity seven untouched)");
        Assert.AreEqual(60, stranger.encounters, "each performance is a memory");
        Assert.AreEqual(1, bonded.stage, "the meter can reach its threshold, the stage never moves without a meaningful test");
        Assert.AreEqual(100, bonded.affection);
        Assert.IsFalse(LegendRelationshipRules.Experience(bonded, "performance:perf-3", "again", 0, "Strand", false, true, affection), "the same performance counts once");
    }

    // ===== SAVES =====

    [Test]
    public void PerformanceState_SurvivesTheSaveCodec_AndAnOlderEnvelopeLoads()
    {
        var s = PerformanceRules.Ensure(null);
        var b = PerformanceRules.Book(s, 7, "Party", 1, "Ashford", "song-of-the-named", PerformanceIntent.Remembrance, new[] { "Vaelia" }, "crisis:age-of-desolation:1", 3);
        s.echoes.Add(Echo("perf-9", 2, PerformanceBand.Moving, PerformanceIntent.Celebration, 2));
        var back = PerformanceRules.Ensure((PerformanceState)SaveStateCodec.Read(SaveStateCodec.Write(s, typeof(PerformanceState)), typeof(PerformanceState)));
        var saved = back.bookings.Single();
        Assert.AreEqual(PerformanceIntent.Remembrance, saved.intent);
        Assert.AreEqual("crisis:age-of-desolation:1", saved.cause);
        Assert.AreEqual(PerformanceStatus.Planned, saved.status);
        Assert.AreEqual(1f, back.echoes.Single().morale);
        Assert.AreEqual("perf-2", PerformanceRules.Book(back, 8, "P", 3, "T", "evening-song", PerformanceIntent.Celebration, new[] { "Oren" }, null, 4).id, "ids are never handed out twice");
        var envelope = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        envelope.children.RemoveAll(c => c.name == "performance");
        var older = (CultureExtensionState)SaveStateCodec.Read(envelope, typeof(CultureExtensionState));
        Assert.IsNotNull(older.performance);
        Assert.IsEmpty(older.performance.history, "nothing is invented for an older save");
    }
}
