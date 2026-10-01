using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleTempoTests
{
    private static BattleRhythmRecord Record(bool defense = false, BattleExecution grade = BattleExecution.Perfect, bool playable = true, bool assisted = false, int measure = 1) =>
        new BattleRhythmRecord { measure = measure, attacker = true, abjuration = defense, results = { new BattleRhythmResult { grade = grade, playable = playable, assisted = assisted } } };
    private static readonly BattleRhythmRecord[] Execution = { Record() }, Attacked = { Record(), Record(true) };

    // ===== THE METER (vault: "Igniting and Sustaining the Flow") =====

    [Test]
    public void FourPerfectSequencesIgnite_TheTableOfEnemyPressureHolds()
    {
        // Attacking every Cadence: E·A·E·A ignites in 2 Measures.
        var fever = new BattleTempoFever();
        fever.Assess(Attacked, true, 1); Assert.AreEqual(2, fever.streak); Assert.AreEqual(0, fever.percentage);
        fever.Assess(Attacked, true, 1); Assert.AreEqual(25, fever.percentage); Assert.AreEqual(0, fever.streak); Assert.AreEqual("Ignited", fever.lastChange);
        // Attacking intermittently: E·A·E·E ignites in 3.
        fever = new BattleTempoFever();
        fever.Assess(Attacked, true, 1); fever.Assess(Execution, true, 1); Assert.AreEqual(0, fever.percentage); fever.Assess(Execution, true, 1);
        Assert.AreEqual(25, fever.percentage);
        // Not attacking: E·E·E·E ignites in 4.
        fever = new BattleTempoFever();
        for (int i = 1; i <= 3; i++) { fever.Assess(Execution, true, 1); Assert.AreEqual(i, fever.streak); Assert.AreEqual(0, fever.percentage); }
        fever.Assess(Execution, true, 1); Assert.AreEqual(25, fever.percentage); Assert.AreEqual("Tempo Crescendo", fever.Movement);
    }

    [TestCase(BattleExecution.Clean, false)] [TestCase(BattleExecution.Missed, false)] [TestCase(BattleExecution.Perfect, true)]
    public void BeforeIgnitionAnyImperfectSequenceResetsThePips(BattleExecution grade, bool assisted)
    {
        var fever = new BattleTempoFever(); for (int i = 0; i < 3; i++) fever.Assess(Execution, true, 1); Assert.AreEqual(3, fever.streak);
        fever.Assess(new[] { Record(grade: grade, assisted: assisted) }, true, 1); Assert.AreEqual(0, fever.streak); Assert.AreEqual(0, fever.percentage);
        // A Perfect Execution followed by an imperfect Abjuration ends the Measure on zero; the reverse keeps the later Perfect sequence.
        fever.Assess(new[] { Record(), Record(true, BattleExecution.Clean) }, true, 1); Assert.AreEqual(0, fever.streak);
        fever.Assess(new[] { Record(grade: BattleExecution.Clean), Record(true) }, true, 1); Assert.AreEqual(1, fever.streak);
    }

    [Test]
    public void SilenceResetsThePips_AndAnUnplayableAbjurationNeitherBreaksNorAdvances()
    {
        var fever = new BattleTempoFever(); fever.Assess(Attacked, true, 1); Assert.AreEqual(2, fever.streak);
        fever.Assess(new[] { Record(true) }, true, 1); Assert.AreEqual(0, fever.streak, "a Measure with no Spotlit phrase resets the streak");
        fever.Assess(new[] { Record(), Record(true, BattleExecution.Missed, playable: false) }, true, 1); Assert.AreEqual(1, fever.streak);
    }

    [Test]
    public void FlawlessMeasuresEscalateByFive_ChangeCategoryAtSixty_AndCapAtNinety()
    {
        var fever = new BattleTempoFever { percentage = 25 };
        for (int i = 30; i <= 90; i += 5) { fever.Assess(Execution, true, 1); Assert.AreEqual(i, fever.percentage); }
        Assert.AreEqual("Resolution Climax", fever.Movement); Assert.AreEqual(BattleClimaxTier.GrandResolution, fever.Tier);
        fever.Assess(Execution, true, 1); Assert.AreEqual(90, fever.percentage);
        Assert.AreEqual(BattleClimaxTier.Unison, BattleTempoFever.TierAt(60)); Assert.AreEqual(BattleClimaxTier.Dyad, BattleTempoFever.TierAt(65));
        Assert.AreEqual(BattleClimaxTier.Ensemble, BattleTempoFever.TierAt(70)); Assert.AreEqual(BattleClimaxTier.Linked, BattleTempoFever.TierAt(75));
        Assert.AreEqual(BattleClimaxTier.FusedFinale, BattleTempoFever.TierAt(80)); Assert.AreEqual(BattleClimaxTier.LinkedFinales, BattleTempoFever.TierAt(85));
        Assert.AreEqual(BattleClimaxTier.None, BattleTempoFever.TierAt(55));
    }

    [TestCase(BattleExecution.Clean, false)] [TestCase(BattleExecution.Missed, false)] [TestCase(BattleExecution.Perfect, true)]
    public void AnImperfectMeasureFalters_ASecondInARowBreaks(BattleExecution grade, bool assisted)
    {
        var fever = new BattleTempoFever { percentage = 50 }; var imperfect = new[] { Record(grade: grade, assisted: assisted) };
        fever.Assess(imperfect, true, 1); Assert.AreEqual(50, fever.percentage, "the Falter holds the percentage"); Assert.IsTrue(fever.faltering);
        fever.Assess(imperfect, true, 1); Assert.AreEqual(0, fever.percentage); Assert.IsFalse(fever.faltering); Assert.AreEqual("Broken", fever.lastChange);
        // A new Fever needs four fresh Perfect sequences.
        for (int i = 0; i < 3; i++) fever.Assess(Execution, true, 1);
        Assert.AreEqual(0, fever.percentage); fever.Assess(Execution, true, 1); Assert.AreEqual(25, fever.percentage);
    }

    [Test]
    public void AFlawlessMeasureAfterAFalterSteadiesAndResumesRising_CleanIsNotRecovery()
    {
        var fever = new BattleTempoFever { percentage = 40 };
        fever.Assess(new[] { Record(grade: BattleExecution.Clean) }, true, 1); Assert.AreEqual(40, fever.percentage);
        fever.Assess(Execution, true, 1); Assert.AreEqual(45, fever.percentage); Assert.IsFalse(fever.faltering); Assert.AreEqual("Steadied", fever.lastChange);
        fever.Assess(new[] { Record(grade: BattleExecution.Clean) }, true, 1); fever.Assess(new[] { Record(grade: BattleExecution.Clean) }, true, 1);
        Assert.AreEqual(0, fever.percentage, "recovery has to be flawless");
    }

    [Test]
    public void OnlyPlayableDefenseCounts_AndSilenceIsNeverFlawless()
    {
        var fever = new BattleTempoFever { percentage = 25 };
        fever.Assess(new[] { Record(), Record(true, BattleExecution.Missed, false) }, true, 1); Assert.AreEqual(30, fever.percentage);
        fever.Assess(new[] { Record(), Record(true, BattleExecution.Clean) }, true, 1); Assert.AreEqual(30, fever.percentage); Assert.IsTrue(fever.faltering);
        fever.Assess(new[] { Record(true) }, true, 1); Assert.AreEqual(0, fever.percentage, "a Perfect Abjuration cannot sustain flow through silence");
    }

    [Test]
    public void ResolutionAndShatteringEndTheFeverAtAssessmentWithoutAFalter()
    {
        var fever = new BattleTempoFever { percentage = 75, resolutionDeclared = true };
        fever.Assess(Execution, true, 1); Assert.AreEqual(0, fever.percentage); Assert.AreEqual("Resolved", fever.lastChange); Assert.IsFalse(fever.resolutionDeclared);
        fever = new BattleTempoFever { percentage = 60, shattered = true };
        fever.Assess(Execution, true, 1); Assert.AreEqual(0, fever.percentage); Assert.AreEqual("Shattered", fever.lastChange);
    }

    [TestCase(0, 3, 3)] [TestCase(25, 2, 3)] [TestCase(45, 3, 4)] [TestCase(50, 1, 2)] [TestCase(70, 3, 5)] [TestCase(80, 2, 4)] [TestCase(90, 3, 6)]
    [TestCase(25, 1, 1)] [TestCase(75, 1, 2)] [TestCase(75, 3, 5)] [TestCase(85, 3, 6)]
    public void InterferenceWeightRoundsHalvesUpDeterministically(int percent, int count, int weight)
    { Assert.AreEqual(weight, BattleTempoFever.InterferenceWeight(count, percent)); }

    [Test]
    public void TheScoreFollowsTheFever()
    {
        CollectionAssert.IsEmpty(BattleTempoFever.MusicLayers(0));
        CollectionAssert.AreEqual(new[] { "Percussion", "Countermelody" }, BattleTempoFever.MusicLayers(40));
        Assert.AreEqual(7, BattleTempoFever.MusicLayers(90).Count);
    }

    // ===== THE BATTLE =====

    private static readonly string[] Voices = { "Conductor", "Vanguard", "Mage" };
    private static BattleSetup Setup(int voices = 1)
    {
        var leader = new BattleLegend { name = "Conductor" };
        var setup = new BattleSetup { seed = 31, field = new Battlefield { age = 3 },
            attacker = new BattleSide { manual = true, conductor = leader, sections = { new CombatSection { name = "Conductor", leader = leader, eliteRole = BattleEliteRole.Conductor,
                battleHex = 7, count = 1, integrity = 10000, maxIntegrity = 10000, composure = 100, maxComposure = 100, speed = 0 } } },
            defender = new BattleSide { manual = true, takesCaptives = false, sections = { new CombatSection { name = "Enemy", battleHex = 8, count = 1,
                integrity = 10000, maxIntegrity = 10000, composure = 100, maxComposure = 100, speed = 0 } } } };
        int[] hexes = { 7, 1, 12 };
        for (int v = 1; v < voices; v++)
        {
            var legend = new BattleLegend { name = Voices[v] };
            setup.attacker.sections.Add(new CombatSection { name = Voices[v], leader = legend, eliteRole = BattleEliteRole.Guard, battleHex = hexes[v], count = 1,
                integrity = 10000, maxIntegrity = 10000, composure = 100, maxComposure = 100, speed = 0 });
        }
        for (int v = 0; v < voices; v++)
        {
            var legend = setup.attacker.sections[v].leader;
            foreach (string id in new[] { "hit", "extra", "third" }) setup.attacker.deck.Add(new DeckCard { voice = v, legend = legend, card = new CombatCard { id = id + (v == 0 ? "" : v.ToString()),
                effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 20) { range = 16 } } } });
            foreach (string id in new[] { "minor", "minorB", "minorC" }) setup.attacker.deck.Add(new DeckCard { voice = v, legend = legend, card = new CombatCard { id = id + (v == 0 ? "" : v.ToString()),
                noteRole = BattleNoteRole.Minor, chord = new BattleChordModifier { requiresAdjacent = false } } });
        }
        setup.attacker.deck.Add(new DeckCard { voice = 0, legend = leader, card = new CombatCard { id = "spell", kind = CardKind.Spell, binding = SpellBinding.Flux, flicker = 0,
            tempo = CardTempo.Staccato, effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 20) { range = 16 } } } });
        setup.attacker.deck.Add(new DeckCard { voice = 0, legend = leader, card = new CombatCard { id = "reaction", purpose = SpellPurpose.Defensive,
            reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Incoming }, effects = { new CardEffect(CardOp.Guard, CardAim.Self, 20) { flat = true } } } });
        return setup;
    }
    private static CombatSettings Settings() => new CombatSettings { tuning = new CombatTuning { maxMeasures = 24, variance = 0, fatigueFrom = 100 },
        symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 40 } } };
    private static BattleResolver.BattleRun Run(int voices = 1) { var run = BattleResolver.Begin(Setup(voices), Settings(), 1); run.BeginMeasure(); return run; }
    private static string Id(string id, int voice) => id + (voice == 0 ? "" : voice.ToString());
    private static int Index(BattleResolver.BattleRun run, string id, int voice = 0) => run.Hand(true).ToList().FindIndex(c => c.card.id == Id(id, voice));
    private static void Echo(BattleResolver.BattleRun run, BattleRhythmChallenge frame, double error = 0)
    {
        for (int lane = 0; lane < frame.lanes.Count; lane++) if (frame.lanes[lane].playable) foreach (double at in frame.lanes[lane].echo)
            Assert.IsNull(run.RhythmInput(lane, frame.start + at + error));
        Assert.IsNull(run.CompleteRhythm(frame.end + .3));
    }
    /// <summary>Performs the rest of the open Measure: every phrase (Overbeat and Abjuration included) Perfect.</summary>
    private static void Perform(BattleResolver.BattleRun run, double at = 30, double error = 0)
    {
        int measure = run.Measure;
        for (int guard = 0; guard < 12 && run.Measure == measure && run.Phase != BattlePhase.Assessment && !run.Over; guard++)
        {
            if (run.AwaitingAbjuration) Echo(run, run.BeginAbjuration(at + 9));
            else Echo(run, run.BeginRhythmBeat(true, at + guard), error);
        }
    }
    private static void PerfectMeasure(BattleResolver.BattleRun run, double at, params int[] voices)
    {
        foreach (int v in voices) Assert.IsNull(run.Spotlight(true, v));
        foreach (int v in voices) Assert.IsNull(run.CommitCard(true, Index(run, "hit", v), 0));
        Perform(run, at);
    }
    /// <summary>Flawless Measures until the Fever reaches <paramref name="percentage"/>; leaves the next Measure open in Composition.</summary>
    private static void Climb(BattleResolver.BattleRun run, int percentage, params int[] voices)
    {
        if (voices.Length == 0) voices = new[] { 0 };
        for (double at = 100; run.TempoPercentage(true) < percentage; at += 20)
        { PerfectMeasure(run, at, voices); Assert.IsFalse(run.Over); run.BeginMeasure(); }
        Assert.AreEqual(percentage, run.TempoPercentage(true));
        foreach (int v in voices) Assert.IsNull(run.Spotlight(true, v));
    }
    private static void Ignite(BattleResolver.BattleRun run) => Climb(run, 25);

    [Test]
    public void APlayedBattleShowsThePipsAndIgnitesAfterFourPerfectExecutions()
    {
        var run = Run();
        for (int measure = 1; measure <= 3; measure++)
        { PerfectMeasure(run, measure * 20, 0); Assert.AreEqual(measure, run.TempoStreak(true)); Assert.AreEqual(0, run.TempoPercentage(true)); run.BeginMeasure(); }
        PerfectMeasure(run, 80, 0); Assert.AreEqual(25, run.TempoPercentage(true)); Assert.AreEqual(0, run.TempoStreak(true));
    }

    [Test]
    public void OverbeatMajorReleasesAfterThreeWithoutAdvancingTimeOrOpeningAbjuration()
    {
        var run = Run(); Ignite(run); Assert.IsNull(run.CommitCard(true, Index(run, "hit"), 0));
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "extra"), 0)); Assert.IsNotNull(run.CommitOverbeat(true, Index(run, "third"), 0));
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 30 + b));
        Assert.IsTrue(run.AwaitingOverbeat); int before = run.Beat; float body = run.Defender.sections[0].integrity;
        var frame = run.BeginRhythmBeat(true, 34); Assert.IsTrue(frame.overbeat); Echo(run, frame);
        Assert.AreEqual(before, run.Beat); Assert.AreEqual(3, run.MeasureBeat); Assert.IsFalse(run.AwaitingAbjuration);
        Assert.AreEqual(body - 25, run.Defender.sections[0].integrity, .01);
        Echo(run, run.BeginRhythmBeat(true, 35)); if (run.AwaitingAbjuration) Echo(run, run.BeginAbjuration(36)); Assert.AreEqual(30, run.TempoPercentage(true));
    }

    [Test]
    public void AFalterKeepsTheOverbeat_TheSecondImperfectMeasureIsStillPerformedThenBreaks()
    {
        var run = Run(); Ignite(run); run.CommitCard(true, Index(run, "hit"), 0);
        Perform(run, 30, .06); Assert.AreEqual(25, run.TempoPercentage(true)); Assert.IsTrue(run.TempoFaltering(true));
        run.BeginMeasure(); run.Spotlight(true, 0); Assert.IsTrue(run.HasOverbeat(true, 0), "the Overbeat remains through a Falter");
        run.CommitCard(true, Index(run, "hit"), 0, beat: 1); Assert.IsNull(run.CommitOverbeat(true, Index(run, "extra"), 0));
        Echo(run, run.BeginRhythmBeat(true, 50), .06); Assert.AreEqual(25, run.TempoPercentage(true));
        for (int b = 0; b < 2; b++) Echo(run, run.BeginRhythmBeat(true, 51 + b));
        Assert.IsTrue(run.AwaitingOverbeat); float body = run.Defender.sections[0].integrity;
        Echo(run, run.BeginRhythmBeat(true, 54)); Assert.Less(run.Defender.sections[0].integrity, body, "the promised Overbeat is still performed");
        Perform(run, 55); Assert.AreEqual(0, run.TempoPercentage(true)); Assert.AreEqual("Broken", run.TempoChange(true));
    }

    [Test]
    public void OverbeatMinorAddsNoSoundingBeat_AndLosingSynchronizationLeavesItUnresolved()
    {
        var run = Run(); Ignite(run); run.CommitCard(true, Index(run, "hit"), 0); long working = run.PreparedWeaves.Single(w => w.attacker).action;
        int sounding = run.PreparedWeaves.Single(w => w.action == working).sounding;
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "minor"), working: working));
        Assert.AreEqual(sounding, run.PreparedWeaves.Single(w => w.action == working).sounding);
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 30 + b));
        run.Attacker.sections[0].composure = 30; int beat = run.Beat; Echo(run, run.BeginRhythmBeat(true, 34));
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.action == working && e.cause == BattleChordCause.NoteFailed && e.detail.Contains("synchronization")));
        Assert.AreEqual(beat, run.Beat, "the Overbeat adds no Beat");
    }

    [Test]
    public void OverbeatStepUsesSimultaneousMovementWithoutAnAdditionalBeat()
    {
        var run = Run(); Ignite(run); run.CommitCard(true, Index(run, "hit"), 0);
        Assert.IsNull(run.CommitOverbeatStep(true, 0, 6)); Assert.IsNotNull(run.CommitOverbeat(true, Index(run, "extra"), 0));
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 30 + b));
        int beat = run.Beat; Echo(run, run.BeginRhythmBeat(true, 34)); Assert.AreEqual(6, run.Attacker.sections[0].battleHex); Assert.AreEqual(beat, run.Beat, "the Overbeat adds no Beat");
    }

    [Test]
    public void ReactionOnOverbeatIsNotPrimedBeforeItsCompressedSlot()
    {
        var run = Run(); Ignite(run); run.CommitCard(true, Index(run, "hit"), 0);
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "reaction"))); Assert.IsEmpty(run.PreparedReactions);
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 30 + b));
        Assert.IsEmpty(run.PreparedReactions); int beat = run.Beat; Echo(run, run.BeginRhythmBeat(true, 34)); Assert.AreEqual(1, run.PreparedReactions.Count);
        Assert.AreEqual(beat, run.Beat, "the Overbeat adds no Beat");
    }

    [Test]
    public void SavedActiveOverbeatReplaysCrescendoAndInputsWithoutAddingTime()
    {
        var encounter = new BattleEncounter(new BattleEncounterState { id = "tempo-save", playerAttacker = true, original = Setup() }, Settings(), new BattlePremonitions());
        void EchoEncounter(BattleEncounter e, BattleRhythmChallenge phrase)
        {
            for (int lane = 0; lane < phrase.lanes.Count; lane++) if (phrase.lanes[lane].playable) foreach (double at in phrase.lanes[lane].echo)
                Assert.IsNull(e.Input(lane, phrase.start + at));
            Assert.IsNull(e.Complete(phrase.end + .3));
        }
        for (int measure = 0; measure < 4; measure++)
        {
            encounter.BeginMeasure(); encounter.Spotlight(0); encounter.Command(new BattleCommand { kind = BattleCommandKind.Card, handIndex = Index(encounter.Run, "hit"), target = 0 });
            for (int b = 0; b < 4; b++) EchoEncounter(encounter, encounter.Rhythm(10 + measure * 10 + b, null));
            if (encounter.Run.AwaitingAbjuration) EchoEncounter(encounter, encounter.Abjuration(15 + measure * 10));
        }
        Assert.AreEqual(25, encounter.Run.TempoPercentage(true));
        encounter.BeginMeasure(); encounter.Spotlight(0);
        encounter.Command(new BattleCommand { kind = BattleCommandKind.Card, handIndex = Index(encounter.Run, "hit"), target = 0 });
        Assert.IsNull(encounter.Command(new BattleCommand { kind = BattleCommandKind.OverbeatCard, handIndex = Index(encounter.Run, "extra"), target = 0 }));
        for (int b = 0; b < 3; b++) EchoEncounter(encounter, encounter.Rhythm(60 + b, null));
        var frame = encounter.Rhythm(64, null); Assert.IsTrue(frame.overbeat); encounter.Input(0, frame.start + frame.lanes[0].echo[0]);
        var saved = (BattleEncounterState)SaveStateCodec.Read(SaveStateCodec.Write(encounter.State, typeof(BattleEncounterState)), typeof(BattleEncounterState));
        var restored = new BattleEncounter(saved, Settings(), new BattlePremonitions()); Assert.AreEqual(25, restored.Run.TempoPercentage(true));
        Assert.IsTrue(restored.Run.Rhythm.overbeat); Assert.AreEqual(1, restored.Run.Rhythm.inputs.Count);
        int beat = encounter.Run.Beat;
        encounter.Complete(frame.end + .3); restored.Complete(frame.end + .3); Assert.AreEqual(beat, restored.Run.Beat);
        Assert.AreEqual(encounter.Defender.sections[0].integrity, restored.Defender.sections[0].integrity);
        EchoEncounter(encounter, encounter.Rhythm(65, null)); EchoEncounter(restored, restored.Rhythm(65, null));
        if (encounter.Run.AwaitingAbjuration) { EchoEncounter(encounter, encounter.Abjuration(66)); EchoEncounter(restored, restored.Abjuration(66)); }
        Assert.AreEqual(30, restored.Run.TempoPercentage(true)); Assert.AreEqual(encounter.Run.TempoPercentage(true), restored.Run.TempoPercentage(true));
    }

    // ===== SYMPATHETIC RESONANCE =====

    [Test]
    public void ClimaxMakesSynchronizedSpotlitTracksSympathetic_PlusOneHoldAndWiderWindows()
    {
        var run = Run(3); Climb(run, 55, 0, 1);
        Assert.AreEqual(BattleTuningState.InTune, run.TuningOf(true, 0));
        PerfectMeasure(run, 400, 0, 1); Assert.AreEqual(60, run.TempoPercentage(true)); run.BeginMeasure();
        Assert.AreEqual(BattleTuningState.Sympathetic, run.TuningOf(true, 0)); Assert.AreEqual(BattleTuningState.Sympathetic, run.TuningOf(true, 1));
        Assert.AreEqual(BattleTuningState.InTune, run.TuningOf(true, 2), "a Track that was not Spotlit when the Climax began is not Sympathetic");
        run.Spotlight(true, 0); run.Spotlight(true, 2); Assert.IsNull(run.CommitCard(true, Index(run, "hit"), 0)); Assert.IsNull(run.CommitCard(true, Index(run, "hit", 2), 0));
        Assert.AreEqual(5, run.PreparedWeaves.Single(w => w.core == "hit").holdLimit, "Sympathetic gives +1 Hold, as Resonant");
        Assert.AreEqual(4, run.PreparedWeaves.Single(w => w.core == "hit2").holdLimit);
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 410 + b));
        var cadence = run.BeginRhythmBeat(true, 414);
        var sympatheticLane = cadence.lanes.Single(l => l.voice == 0); var ordinaryLane = cadence.lanes.Single(l => l.voice == 2);
        Assert.Greater(sympatheticLane.perfectWindow, ordinaryLane.perfectWindow);
        Echo(run, cadence); if (run.AwaitingAbjuration) Echo(run, run.BeginAbjuration(420));
        run.BeginMeasure(); Assert.AreEqual(BattleTuningState.Sympathetic, run.TuningOf(true, 2), "a later Assessment adds a synchronized Spotlit Track");
    }

    [Test]
    public void ASympatheticCollapseRingsThroughEveryTrack_DetunesThem_AndShattersTheFever()
    {
        var run = Run(3); Climb(run, 60, 0, 1, 2);
        Assert.AreEqual(BattleTuningState.Sympathetic, run.TuningOf(true, 2));
        // The Conductor's magical Tetrad: one Missed Note weighs 2 at 60% and leaves 3 of its 5 Beats of Hold.
        Assert.IsNull(run.CommitChord(true, Index(run, "spell"), new[] { Index(run, "minor"), Index(run, "minorB"), Index(run, "minorC") }, 0));
        Assert.AreEqual(5, run.PreparedWeaves.Single(w => w.core == "spell").holdLimit);
        Assert.IsNull(run.CommitCard(true, Index(run, "hit", 1), 0)); Assert.IsNull(run.CommitCard(true, Index(run, "hit", 2), 0));
        float vanguard = run.Attacker.sections[1].integrity + run.Attacker.sections[1].composure;
        var first = run.BeginRhythmBeat(true, 600); Assert.IsNull(run.CompleteRhythm(first.end + .3)); // no echo at all: Missed
        Assert.AreEqual(3, run.PreparedWeaves.Single(w => w.core == "spell").holdLimit, "one Interference weighs 1.6, rounded to 2");
        Perform(run, 601);
        var collapse = run.Report.chordEvents.Single(e => e.cause == BattleChordCause.SympatheticCollapse);
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Collapsed && e.action == collapse.action));
        Assert.Less(run.Attacker.sections[1].integrity + run.Attacker.sections[1].composure, vanguard, "the backlash rings through Tracks that contributed nothing");
        Assert.AreEqual(0, run.TempoPercentage(true), "a Sympathetic Collapse shatters the Fever without a Falter");
        Assert.AreEqual("Shattered", run.TempoChange(true));
        run.BeginMeasure(); Assert.AreNotEqual(BattleTuningState.Sympathetic, run.TuningOf(true, 1));
    }

    // ===== RESOLUTION CLIMAX =====

    [Test]
    public void ClimaxUnisonReleasesAtTheCadence_AndDeclaringItEndsTheFever()
    {
        var run = Run(3); Climb(run, 60, 0, 1);
        Assert.IsNotNull(run.CommitClimaxMajor(true, Index(run, "extra", 2), 0), "only a synchronized Spotlit Track has a Climax Overbeat");
        Assert.IsNull(run.CommitCard(true, Index(run, "hit"), 0));
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra", 1), 0));
        Assert.IsNotNull(run.CommitClimaxMajor(true, Index(run, "extra"), 0), "Climax Unison permits one Climax Major");
        Assert.IsNotNull(run.CommitClimaxMajor(true, Index(run, "third", 1), 0, fuse: true));
        Assert.IsTrue(run.ResolutionComposed(true));
        float body = run.Defender.sections[0].integrity;
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 600 + b));
        var overbeat = run.BeginRhythmBeat(true, 603); Assert.IsTrue(overbeat.overbeat); Echo(run, overbeat);
        Assert.AreEqual(body, run.Defender.sections[0].integrity, .01, "nothing in a Resolution is syncopated");
        Perform(run, 604);
        Assert.AreEqual(body - 50, run.Defender.sections[0].integrity, .01);
        var record = run.Report.resolutions.Single(); Assert.AreEqual(BattleClimaxTier.Unison, record.tier); Assert.AreEqual("Resolved", record.outcome); Assert.AreEqual(60, record.percentage);
        Assert.AreEqual(0, run.TempoPercentage(true)); Assert.AreEqual("Resolved", run.TempoChange(true));
    }

    [Test]
    public void ClimaxDyadCarriesOneContribution_ClimaxEnsembleCarriesEveryTrack()
    {
        var run = Run(3); Climb(run, 65, 0, 1, 2);
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra"), 0)); long finale = run.PreparedWeaves.Single(w => w.core == "extra").action;
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "minor", 1), working: finale));
        Assert.IsNotNull(run.CommitOverbeat(true, Index(run, "minor", 2), working: finale), "Climax Dyad: one contribution");
        Perform(run, 700); Assert.AreEqual(BattleClimaxTier.Dyad, run.Report.resolutions.Single().tier);

        run = Run(3); Climb(run, 70, 0, 1, 2);
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra"), 0)); finale = run.PreparedWeaves.Single(w => w.core == "extra").action;
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "minor", 1), working: finale));
        Assert.IsNull(run.CommitOverbeat(true, Index(run, "minor", 2), working: finale));
        Assert.AreEqual(3, run.PreparedWeaves.Single(w => w.action == finale).load);
        Perform(run, 700);
        var record = run.Report.resolutions.Single(); Assert.AreEqual(BattleClimaxTier.Ensemble, record.tier); Assert.AreEqual("Resolved", record.outcome);
        CollectionAssert.AreEquivalent(Voices, record.contributors);
    }

    [Test]
    public void LinkedClimaxesCollapseTogether()
    {
        var run = Run(3); Climb(run, 75, 0, 1, 2);
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra", 1), 0)); Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra", 2), 0));
        Assert.IsNotNull(run.CommitClimaxMajor(true, Index(run, "extra"), 0), "a third Climax Major waits for Linked Finales at 85%");
        for (int b = 0; b < 3; b++) Echo(run, run.BeginRhythmBeat(true, 800 + b));
        run.Attacker.sections[2].composure = 10; // Spiraling: Out of Tune, so its Climax Overbeat goes unresolved.
        Perform(run, 803);
        Assert.AreEqual(2, run.Report.resolutions.Count); Assert.IsTrue(run.Report.resolutions.All(r => r.outcome == "Collapsed" && r.tier == BattleClimaxTier.Linked));
        Assert.AreEqual(0, run.TempoPercentage(true));
    }

    [Test]
    public void AFusedFinaleCarriesItsTracksChordAsASupportingMajor_AndRetractRestoresIt()
    {
        var run = Run(3); Climb(run, 80, 0, 1);
        Assert.IsNull(run.CommitChord(true, Index(run, "hit", 1), new[] { Index(run, "minor", 1) }, 0));
        Assert.IsNotNull(run.CommitClimaxMajor(true, Index(run, "extra"), 0, fuse: true), "the Conductor has no Chord of its own to fuse");
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra", 1), 0, fuse: true));
        var finale = run.PreparedWeaves.Single(w => w.attacker);
        CollectionAssert.AreEqual(new[] { "extra1", "hit1", "minor1" }, finale.notes);
        StringAssert.StartsWith("Supporting Major", run.Score(true).Single(t => t.voice == 1).beats[4]);
        Assert.IsNull(run.Retract(true, finale.action));
        Assert.AreEqual("hit1", run.PreparedWeaves.Single(w => w.attacker).core); Assert.GreaterOrEqual(Index(run, "extra", 1), 0);
        Assert.IsNull(run.CommitClimaxMajor(true, Index(run, "extra", 1), 0, fuse: true));
        float body = run.Defender.sections[0].integrity;
        Perform(run, 900);
        Assert.AreEqual(body - 50, run.Defender.sections[0].integrity, .01, "the supporting Major adds its full (Perfect) power to the release");
        var record = run.Report.resolutions.Single(); Assert.AreEqual(BattleClimaxTier.FusedFinale, record.tier); Assert.AreEqual("Resolved", record.outcome);
    }

    [Test]
    public void TheGrandResolutionMergesEverySynchronizedTrackIntoOneWorking()
    {
        var run = Run(3); Climb(run, 90, 0, 1, 2);
        Assert.IsNotNull(run.CommitGrandResolution(true, 0), "nothing to merge yet");
        foreach (int v in new[] { 0, 1 }) Assert.IsNull(run.CommitCard(true, Index(run, "hit", v), 0));
        Assert.IsNull(run.CommitCard(true, Index(run, "hit", 2), 0, beat: 2));
        Assert.IsNull(run.CommitGrandResolution(true, 0));
        var grand = run.PreparedWeaves.Single(w => w.attacker && w.dueBeat == run.Beat + 4);
        CollectionAssert.AreEqual(new[] { "hit", "hit1" }, grand.notes);
        Assert.AreEqual(2, run.PreparedWeaves.Count(w => w.attacker), "a syncopated working is not a finishing working and stays its own");
        Assert.AreEqual(5, grand.holdLimit, "one Hold, Sympathetic: +1");
        float body = run.Defender.sections[0].integrity;
        Perform(run, 950);
        Assert.AreEqual(body - 75, run.Defender.sections[0].integrity, .01);
        var record = run.Report.resolutions.Single(); Assert.AreEqual(BattleClimaxTier.GrandResolution, record.tier); Assert.AreEqual("Resolved", record.outcome);
        Assert.AreEqual(90, record.percentage); Assert.AreEqual(0, run.TempoPercentage(true));
    }

    [Test]
    public void RehearsalCopiesClimaxCompositionsWithoutTouchingTheBattle()
    {
        var run = Run(3); Climb(run, 80, 0, 1);
        Assert.IsNull(run.CommitCard(true, Index(run, "hit", 1), 0));
        var preview = run.Preview(new[] { new BattleCommand { kind = BattleCommandKind.ClimaxMajor, handIndex = Index(run, "extra", 1), target = 0, fuse = true },
            new BattleCommand { kind = BattleCommandKind.Wait, beats = 4 } });
        CollectionAssert.IsEmpty(preview.failures);
        Assert.AreEqual("hit1", run.PreparedWeaves.Single(w => w.attacker).core); Assert.AreEqual(80, run.TempoPercentage(true));
        Assert.IsTrue(preview.chordEvents.Any(e => e.cause == BattleChordCause.ClimaxFused));
    }
}
