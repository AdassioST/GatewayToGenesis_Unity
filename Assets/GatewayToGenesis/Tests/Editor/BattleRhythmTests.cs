using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleRhythmTests
{
    [Test]
    public void CoveredStatusOnlyReleaseCanBeAbjuredBeforeItPollutesTheHand()
    {
        var setup = Setup(); Add(setup.attacker, Ward(100));
        Add(setup.defender, new CombatCard { id = "pollute", effects = { new CardEffect(CardOp.Pollute, CardAim.Enemy, 2) { pollution = BattlePollution.Burn, range = 6 } } });
        var run = Begin(setup); Assert.IsNull(run.CommitCard(true, 0)); Assert.IsNull(run.CommitCard(false, 0, 0));
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        Assert.IsTrue(run.AwaitingAbjuration); var phrase = run.BeginAbjuration(20); Assert.IsTrue(phrase.lanes.Single().playable); Echo(run, phrase);
        Assert.IsFalse(run.DrawPile(true).Any(c => c.card.pollution != BattlePollution.None));
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Cancelled));
    }
    private static CombatSection Unit(string name, int hex) => new CombatSection { name = name, battleHex = hex, count = 1, eliteRole = BattleEliteRole.Guard,
        maxIntegrity = 500, integrity = 500, maxComposure = 100, composure = 100, speed = 0, row = FormationRow.Front };
    private static CombatCard Hit(string id = "hit", float amount = 32) => new CombatCard { id = id, name = id,
        effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, amount) { range = 6 } } };
    private static CombatCard Ward(float amount = 10) => new CombatCard { id = "ward", name = "ward", purpose = SpellPurpose.Defensive,
        effects = { new CardEffect(CardOp.Guard, CardAim.Self, amount) { flat = true } } };
    private static CombatCard Minor(string id) => new CombatCard { id = id, name = id, noteRole = BattleNoteRole.Minor, chord = new BattleChordModifier { requiresAdjacent = false } };
    private static void Add(BattleSide side, CombatCard card, int voice = 0) => side.deck.Add(new DeckCard { card = card, voice = voice });
    private static BattleSetup Setup() => new BattleSetup { seed = 123, field = new Battlefield { age = 3 },
        attacker = new BattleSide { name = "A", manual = true, sections = { Unit("Helper", 7) } },
        defender = new BattleSide { name = "D", manual = true, sections = { Unit("Threat", 8) } } };
    private static CombatSettings Settings() => new CombatSettings { tuning = new CombatTuning { maxMeasures = 6, variance = 0, fatigueFrom = 100 },
        symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 30 } } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup, CombatSettings settings = null) { var run = BattleResolver.Begin(setup, settings ?? Settings(), 1); run.BeginMeasure(); return run; }
    private static int Index(BattleResolver.BattleRun run, string id, bool attacker = true) => run.Hand(attacker).ToList().FindIndex(x => x.card.id == id);
    private static void Echo(BattleResolver.BattleRun run, BattleRhythmChallenge frame, double error = 0)
    {
        for (int i = 0; i < frame.lanes.Count; i++) if (frame.lanes[i].playable)
            foreach (double cue in frame.lanes[i].echo) Assert.IsNull(run.RhythmInput(i, frame.start + cue + error));
        Assert.IsNull(run.CompleteRhythm(frame.end + .25));
    }
    /// <summary>Perfect Executions of the "hit" card, one Measure each (the open one first), until <paramref name="measures"/> have been performed.</summary>
    private static void Ignite(BattleResolver.BattleRun run, int measures)
    {
        for (int i = 0; i < measures; i++)
        {
            if (run.MeasureBeat != 0 || run.Phase == BattlePhase.Assessment) run.BeginMeasure();
            run.Spotlight(true, 0); Assert.IsNull(run.CommitCard(true, Index(run, "hit"), 0));
            for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 100 + 10 * i + b));
            if (run.AwaitingAbjuration) Echo(run, run.BeginAbjuration(105 + 10 * i));
        }
    }
    private static BattleRhythmChallenge Phrase(int complexity = 1, BattleRhythmOptions options = null, int focus = 0)
    { var lane = BattleRhythmLogic.Lane(1, 0, 0, "Test", SpellBinding.Flux, complexity, 1, new BattleRhythmTuning(), options, focus); return new BattleRhythmChallenge(10, 1, new[] { lane }, options); }

    [TestCase(0, BattleExecution.Perfect)]
    [TestCase(.06, BattleExecution.Clean)]
    [TestCase(.2, BattleExecution.Missed)]
    public void RecordedTimestampsGradeTheEcho(double error, BattleExecution expected)
    {
        var frame = Phrase(); Assert.IsNull(frame.Input(0, frame.start + frame.lanes[0].echo[0] + error));
        Assert.AreEqual(expected, frame.Complete(frame.end)[0].grade);
    }

    [Test]
    public void ListenIsNotEcho_AndSpamCannotEarnPerfect()
    {
        var frame = Phrase(); Assert.IsNull(frame.Input(0, frame.start + frame.lanes[0].call[0]));
        Assert.IsNull(frame.Input(0, frame.start + frame.lanes[0].echo[0]));
        var result = frame.Complete(frame.end)[0]; Assert.AreEqual(BattleExecution.Missed, result.grade); Assert.AreEqual(1, result.extra);
    }

    [Test]
    public void CalibrationCorrectsLatencyAndDoesNotChangeThePattern()
    {
        Assert.AreEqual(80, BattleRhythmLogic.Calibrate(new[] { 1d, 2, 3, 4, 5 }, new[] { 1.08, 2.08, 3.4, 4.08, 5.08 }), .001);
        var options = new BattleRhythmOptions { latencyMilliseconds = 80 }; var frame = Phrase(options: options);
        Assert.IsNull(frame.Input(0, frame.start + frame.lanes[0].echo[0] + .08));
        Assert.AreEqual(BattleExecution.Perfect, frame.Complete(frame.end + .08)[0].grade);
    }

    [Test]
    public void AccessibilityAndFocusWidenWindows_AssistanceRemainsClean()
    {
        var ordinary = Phrase(); var focus = Phrase(focus: 4); var wide = Phrase(options: new BattleRhythmOptions { windowScale = 2 });
        Assert.Greater(focus.lanes[0].perfectWindow, ordinary.lanes[0].perfectWindow);
        Assert.Greater(wide.lanes[0].perfectWindow, ordinary.lanes[0].perfectWindow);
        Assert.Greater(wide.lanes[0].cleanWindow, ordinary.lanes[0].cleanWindow);
        var assisted = Phrase(options: new BattleRhythmOptions { assist = true });
        var result = assisted.Complete(assisted.end)[0]; Assert.AreEqual(BattleExecution.Clean, result.grade); Assert.AreEqual(1, result.rendition); Assert.IsTrue(result.assisted);
    }

    [Test]
    public void InvalidDuplicateAndPrematureInputCannotAlterThePhrase()
    {
        var frame = Phrase(); Assert.IsNotNull(frame.Input(0, double.NaN)); Assert.IsNotNull(frame.Input(0, 9)); Assert.IsNotNull(frame.Input(1, 10.5));
        Assert.IsNull(frame.Input(0, 10.58)); Assert.IsNotNull(frame.Input(0, 10.58));
        Assert.Throws<InvalidOperationException>(() => frame.Complete(10.8));
        frame.Complete(11); Assert.IsNotNull(frame.Input(0, 10.8));
    }

    [Test]
    public void LongStacksHaveMorePulsesAndCallPrecedesEveryEcho()
    {
        Assert.Greater(Phrase(4).lanes[0].call.Count, Phrase().lanes[0].call.Count);
        Assert.Less(Phrase(4).lanes[0].call.Last(), Phrase(4).lanes[0].echo.First());
    }

    [Test]
    public void PerfectRecordedMajorIsCappedAtTwentyFivePercent_AndLocksComposition()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); var run = Begin(setup); run.Spotlight(true, 0); Assert.IsNull(run.CommitCard(true, 0, 0));
        for (int b = 1; b <= 4; b++)
        {
            var frame = run.BeginRhythmBeat(true, 10 + b);
            Assert.IsNotNull(run.Perform()); Assert.IsNotNull(run.Spotlight(true, 0));
            Echo(run, frame);
        }
        Assert.IsTrue(run.AwaitingAbjuration); Assert.AreEqual(500, run.Defender.sections[0].integrity, "all Cadence impacts wait for the single fermata");
        Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(460, run.Defender.sections[0].integrity); Assert.AreEqual(4, run.Beat);
        Assert.AreEqual(1, run.Report.rhythm.Count(x => x.abjuration));
    }

    [Test]
    public void AChannelMustBePerfectOnEveryBeat()
    {
        var setup = Setup(); var hit = Hit(); hit.channel = 3; Add(setup.attacker, hit); var run = Begin(setup); run.Spotlight(true, 0); run.CommitCard(true, 0, 0);
        Echo(run, run.BeginRhythmBeat(true, 10));
        var missed = run.BeginRhythmBeat(true, 11); run.CompleteRhythm(missed.end);
        Echo(run, run.BeginRhythmBeat(true, 12)); Echo(run, run.BeginRhythmBeat(true, 13)); Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(468, run.Defender.sections[0].integrity, "a later perfect echo cannot overwrite a missed channel Beat");
        Assert.AreEqual(1, run.Report.chordEvents.Count(x => x.cause == BattleChordCause.Missed));
        Assert.AreEqual(2, run.Report.chordEvents.Single(x => x.cause == BattleChordCause.Missed).beat, "performance events use the Beat they sound on");
    }

    [Test]
    public void UnspotlitTracksRemainCleanWithoutInput()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); var run = Begin(setup); run.CommitCard(true, 0, 0);
        for (int b = 0; b < 4; b++) { var frame = run.BeginRhythmBeat(true, 10 + b); Assert.AreEqual(0, frame.lanes.Count); run.CompleteRhythm(frame.end); }
        Echo(run, run.BeginAbjuration(20)); Assert.AreEqual(468, run.Defender.sections[0].integrity);
    }

    [TestCase(1f, 473.4625f)]
    [TestCase(30f, 500f)]
    public void APreparedWardGetsMeasuredMitigationOrFullCancellation(float ward, float expectedIntegrity)
    {
        var setup = Setup(); Add(setup.attacker, Ward(ward)); Add(setup.defender, Hit(amount: 50)); var run = Begin(setup);
        run.CommitCard(true, 0, beat: 1); run.CommitCard(false, 0, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        var answer = run.BeginAbjuration(20); Assert.IsTrue(answer.lanes.Single().playable); Assert.IsTrue(answer.lanes.Single().inverse);
        Echo(run, answer); Assert.AreEqual(expectedIntegrity, run.Attacker.sections[0].integrity, .01);
    }

    [Test]
    public void UncoveredLanesAreVisibleAndCannotBePlayed()
    {
        var setup = Setup(); Add(setup.defender, Hit(amount: 50)); var run = Begin(setup); run.CommitCard(false, 0, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        var frame = run.BeginAbjuration(20); Assert.IsFalse(frame.lanes.Single().playable); Assert.IsNotNull(run.RhythmInput(0, frame.start + frame.lanes[0].echo[0]));
        run.CompleteRhythm(frame.end); Assert.AreEqual(450, run.Attacker.sections[0].integrity);
    }

    [Test]
    public void MissedDefenseHalvesTheWard_AndHasNoPerformanceReduction()
    {
        var setup = Setup(); Add(setup.attacker, Ward(10)); Add(setup.defender, Hit(amount: 50)); var run = Begin(setup);
        run.CommitCard(true, 0, beat: 1); run.CommitCard(false, 0, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        var frame = run.BeginAbjuration(20); run.CompleteRhythm(frame.end); Assert.AreEqual(458.75f, run.Attacker.sections[0].integrity, .01);
    }

    [Test]
    public void MultipleCadenceReleasesShareOneWindow_AndSyncopationDoesNotOpenIt()
    {
        var setup = Setup(); setup.defender.sections.Add(Unit("Other", 9)); Add(setup.defender, Hit()); Add(setup.defender, Hit("other"), 1);
        var run = Begin(setup); run.CommitCard(false, Index(run, "hit", false), 0); run.CommitCard(false, Index(run, "other", false), 0, beat: 2);
        Echo(run, run.BeginRhythmBeat(true, 10)); Echo(run, run.BeginRhythmBeat(true, 11)); Assert.IsFalse(run.AwaitingAbjuration);
        Assert.AreEqual(468, run.Attacker.sections[0].integrity);
        Echo(run, run.BeginRhythmBeat(true, 12)); Echo(run, run.BeginRhythmBeat(true, 13)); Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(1, run.Report.rhythm.Count(x => x.abjuration)); Assert.AreEqual(436, run.Attacker.sections[0].integrity);
    }

    [Test]
    public void SpecialCounterNeedsBothTheRightCardAndEnoughPreparation()
    {
        var setup = Setup(); Add(setup.attacker, Ward(100)); var boss = Hit(amount: 50); boss.abjuredOnlyBy.Add("ward"); boss.minimumWardBeats = 4;
        Add(setup.defender, boss); var run = Begin(setup); run.CommitCard(true, 0, beat: 1); run.CommitCard(false, 0, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        var frame = run.BeginAbjuration(20); Assert.IsFalse(frame.lanes.Single().playable, "only three held Beats; the attack demands four");
        run.CompleteRhythm(frame.end); Assert.AreEqual(450, run.Attacker.sections[0].integrity);
    }

    [Test]
    public void HeartbeatRecoveryOccupiesHelperWhileEnemyCountdownsAdvance_AndNeverMendsIntegrity()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Patient", 6)); setup.attacker.sections[1].composure = 10; setup.attacker.sections[1].integrity = 300;
        Add(setup.attacker, new CombatCard { id = "help", effects = { new CardEffect(CardOp.CoRegulate, CardAim.Ally, 10) { durationBeats = 3, range = 2 } } });
        Add(setup.defender, Hit()); var run = Begin(setup); run.Spotlight(true, 0); Assert.IsNull(run.CommitCard(true, 0, 1)); run.CommitCard(false, 0, 0);
        float previous = float.MaxValue;
        for (int b = 0; b < 4; b++)
        {
            var frame = run.BeginRhythmBeat(true, 10 + b);
            if (frame.lanes.Count > 0) { Assert.AreEqual(BattleRhythmMode.Heartbeat, frame.lanes[0].mode); Assert.LessOrEqual(frame.lanes[0].bpm, previous); previous = frame.lanes[0].bpm; }
            Echo(run, frame); Assert.AreEqual(b + 1, run.Beat);
        }
        Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(run.ScoreBpm, previous, .01); Assert.AreEqual(47.5f, run.Attacker.sections[1].composure, .01);
        Assert.AreEqual(300, run.Attacker.sections[1].integrity); Assert.AreEqual(468, run.Attacker.sections[0].integrity);
        Assert.AreEqual(3, run.Report.chordEvents.Count(x => x.cause == BattleChordCause.CoRegulation));
    }

    [Test]
    public void FourPerfectExecutionsIgniteCrescendo_AndNormalBeatsKeepTheirMajorLimit()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); Add(setup.attacker, Minor("m0")); Add(setup.attacker, Minor("m1")); Add(setup.attacker, Minor("m2"));
        Add(setup.attacker, Hit("extra")); Add(setup.attacker, Hit("third")); var settings = Settings();
        var run = Begin(setup, settings); run.Spotlight(true, 0);
        run.CommitChord(true, Index(run, "hit"), new[] { Index(run, "m0"), Index(run, "m1"), Index(run, "m2") }, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b)); Echo(run, run.BeginAbjuration(20));
        Assert.IsFalse(run.TempoFever(true, 0)); Assert.AreEqual(1, run.TempoStreak(true), "one Perfect sequence fills one pip");
        Ignite(run, 3);
        Assert.IsTrue(run.TempoFever(true, 0)); Assert.AreEqual(25, run.TempoPercentage(true)); Assert.AreEqual(BattleTuningState.InTune, run.TuningOf(true, 0));
        run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, Index(run, "hit"), 0));
        Assert.IsNotNull(run.CommitCard(true, Index(run, "extra"), 0, beat: 2));
        Assert.IsNotNull(run.CommitCard(true, Index(run, "third"), 0, beat: 3));
    }

    [Test]
    public void AssistedPerformanceCannotFarmTempoFever()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); var settings = Settings();
        var run = Begin(setup, settings); run.Spotlight(true, 0); run.CommitCard(true, 0, 0);
        for (int b = 0; b < 4; b++) { var frame = run.BeginRhythmBeat(true, 10 + b, new BattleRhythmOptions { assist = true }); run.CompleteRhythm(frame.end); }
        Echo(run, run.BeginAbjuration(20)); Assert.IsFalse(run.TempoFever(true, 0)); Assert.AreEqual(468, run.Defender.sections[0].integrity);
    }

    [TestCase(true, 500f)]
    [TestCase(false, 450f)]
    public void EightBeatBossRequiresEarlyPreparedCounter(bool early, float expected)
    {
        var setup = Setup(); var ward = Ward(100); ward.effects[0].durationBeats = 8; Add(setup.attacker, ward);
        var boss = Hit(amount: 50); boss.hyperMeasures = 2; boss.minimumWardBeats = 4; boss.abjuredOnlyBy.Add("ward"); Add(setup.defender, boss);
        var run = Begin(setup); run.CommitCard(false, 0, 0);
        Assert.AreEqual(8, run.Countdowns.Single(x => !x.attacker).dueBeat);
        if (early) run.CommitCard(true, 0, beat: 1);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        Assert.IsFalse(run.AwaitingAbjuration); run.BeginMeasure();
        if (!early) run.CommitCard(true, 0, beat: 1);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 20 + b));
        var answer = run.BeginAbjuration(30); Assert.AreEqual(early, answer.lanes.Single().playable);
        Echo(run, answer); Assert.AreEqual(expected, run.Attacker.sections[0].integrity, .01);
    }

    [Test]
    public void CancellationAlsoErasesStatusAndForcedDisplacement()
    {
        var setup = Setup(); Add(setup.attacker, Ward(100)); var attack = Hit();
        attack.effects.Add(new CardEffect(CardOp.Blind, CardAim.Enemy, .5f) { range = 6 });
        attack.effects.Add(new CardEffect(CardOp.Push, CardAim.Enemy, 1) { range = 6 }); Add(setup.defender, attack);
        var run = Begin(setup); run.CommitCard(true, 0, beat: 1); run.CommitCard(false, 0, 0);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b)); Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(500, run.Attacker.sections[0].integrity); Assert.AreEqual(7, run.Attacker.sections[0].battleHex);
        Assert.AreEqual(0, run.Visualize().units.Single(x => x.attacker).blind);
    }

    [Test]
    public void ARecordedPerformanceReplaysToIdenticalState()
    {
        BattleResolver.BattleRun Prepare()
        {
            var setup = Setup(); Add(setup.attacker, Hit()); Add(setup.defender, Hit()); Add(setup.attacker, Minor("m"));
            var run = Begin(setup); run.Spotlight(true, 0); run.CommitChord(true, Index(run, "hit"), new[] { Index(run, "m") }, 0); run.CommitCard(false, 0, 0); return run;
        }
        var original = Prepare();
        for (int b = 0; b < 4; b++) Echo(original, original.BeginRhythmBeat(true, 10 + b), .015);
        Echo(original, original.BeginAbjuration(20));
        var replay = Prepare();
        foreach (var record in original.Report.rhythm)
        {
            var frame = record.abjuration ? replay.BeginAbjuration(record.start) : replay.BeginRhythmBeat(true, record.start, record.options);
            foreach (var input in record.inputs) Assert.IsNull(replay.RhythmInput(input.lane, input.dspTime));
            Assert.IsNull(replay.CompleteRhythm(frame.end + .25));
        }
        Assert.AreEqual(original.Attacker.sections[0].integrity, replay.Attacker.sections[0].integrity);
        Assert.AreEqual(original.Defender.sections[0].integrity, replay.Defender.sections[0].integrity);
        CollectionAssert.AreEqual(original.Report.chordEvents.Select(x => (x.beat, x.cause, x.interference)), replay.Report.chordEvents.Select(x => (x.beat, x.cause, x.interference)));
    }

    [Test]
    public void EveryMeasuredPerfectNoteShedsInterferenceOnceAtRelease()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); Add(setup.attacker, Minor("m"));
        Add(setup.defender, new CombatCard { id = "interrupt", effects = { new CardEffect(CardOp.Interrupt, CardAim.Enemy, 1) { range = 6 } } });
        var run = Begin(setup); run.Spotlight(true, 0); run.CommitChord(true, Index(run, "hit"), new[] { Index(run, "m") }, 0); run.CommitCard(false, 0, 0, beat: 3);
        for (int b = 0; b < 4; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b)); Echo(run, run.BeginAbjuration(20));
        Assert.AreEqual(1, run.Report.chordEvents.Count(x => x.cause == BattleChordCause.PerfectEcho));
        Assert.AreEqual(4, run.Report.chordEvents.Single(x => x.cause == BattleChordCause.PerfectEcho).beat);
    }

    [Test]
    public void CrescendoDoesNotCreateExtraChannelTimeOrEarlierLegatoAnchoring()
    {
        var setup = Setup(); Add(setup.attacker, Hit()); var legato = Hit("legato"); legato.kind = CardKind.Spell;
        legato.tempo = CardTempo.Legato; legato.binding = SpellBinding.Flux; legato.flicker = 0; legato.cost = 1; Add(setup.attacker, legato);
        var settings = Settings(); var run = Begin(setup, settings);
        Ignite(run, 4);
        run.BeginMeasure(); Assert.IsTrue(run.TempoFever(true, 0)); Assert.IsNull(run.CommitCard(true, Index(run, "legato"), 0));
        var track = run.Score(true).Single(); StringAssert.StartsWith("Major", track.beats[2]); Assert.AreEqual("Sustained", track.beats[3]);
    }

    [Test]
    public void AutomaticHelpersRecoverAtCleanUsingTheSameTrackTime()
    {
        var setup = Setup(); setup.attacker.manual = false; setup.attacker.sections.Add(Unit("Patient", 6)); setup.attacker.sections[1].composure = 10;
        setup.attacker.sections[1].eliteRole = BattleEliteRole.None;
        Add(setup.attacker, new CombatCard { id = "help", effects = { new CardEffect(CardOp.CoRegulate, CardAim.Ally, 10) { durationBeats = 3, range = 2 } } });
        var run = Begin(setup); run.ResolveMeasure();
        Assert.AreEqual(4, run.Beat); Assert.AreEqual(40, run.Attacker.sections[1].composure);
        Assert.AreEqual(3, run.Report.chordEvents.Count(x => x.cause == BattleChordCause.CoRegulation));
    }

    [TestCase(0, 0f)]
    [TestCase(42, 14f)]
    public void ResonanceMasteryStrengthensAndBroadensPreparedWards(int score, float expectedPatientWard)
    {
        var setup = Setup(); setup.attacker.sections[0].leader = new BattleLegend { name = "Helper", scores = { { "Resonance", score } } };
        setup.attacker.sections.Add(Unit("Patient", 0));
        Add(setup.attacker, new CombatCard { id = "ward", purpose = SpellPurpose.Defensive,
            effects = { new CardEffect(CardOp.Ward, CardAim.Allies, 10) { flat = true } } });
        var settings = Settings(); settings.spatial.supportReach = 1;
        var run = Begin(setup, settings);
        run.Attacker.sections[0].battleHex = 7; run.Attacker.sections[1].battleHex = 0;
        run.Order(true, 0, BattleStandingOrder.Hold); run.Order(true, 1, BattleStandingOrder.Hold);
        Assert.Greater(run.SpatialRules.Distance(7, 0), 1, "the patient is beyond ordinary support access");
        Assert.IsNull(run.CommitCard(true, 0, beat: 1)); run.Perform();
        Assert.AreEqual(expectedPatientWard, run.Visualize().units.Single(x => x.attacker && x.section == 1).ward, .01);
    }

    [Test]
    public void ASpotlitHyperChordMustPerformItsIntermediateStabilizationCadence()
    {
        var setup = Setup(); setup.defender.sections[0].battleHex = 9;
        var hyper = Hit(amount: 50); hyper.hyperMeasures = 2; hyper.kind = CardKind.Spell;
        hyper.binding = SpellBinding.Flux; hyper.flicker = 0; Add(setup.attacker, hyper);
        var run = Begin(setup); run.Spotlight(true, 0); Assert.IsNull(run.CommitCard(true, 0, 0));
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 10 + b));
        var cadence = run.BeginRhythmBeat(true, 13); Assert.AreEqual(1, cadence.lanes.Count, "stabilizing a held Hyper is performed, not free Clean execution");
        Assert.IsNull(run.CompleteRhythm(cadence.end));
        Assert.IsTrue(run.Report.chordEvents.Any(x => x.cause == BattleChordCause.StaticCriticality));
        Assert.Less(run.Attacker.sections[0].integrity, 500); Assert.AreEqual(500, run.Defender.sections[0].integrity);
    }
}
