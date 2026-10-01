using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>The four-Beat Measure (Combat System.md, "The Temporal Economy", "Movement and Contact", "Abjuration").</summary>
public class BattleMeasureTests
{
    private static CombatSection Unit(string name, int hex, float speed = 3f, float attack = 0f) => new CombatSection
    { name = name, battleHex = hex, count = 10, row = FormationRow.Front, maxIntegrity = 200f, integrity = 200f, maxComposure = 100f, composure = 100f, speed = speed, attack = attack };
    private static CombatCard Card(CardOp op, CardAim aim, float amount, bool flat = false, string id = null) => new CombatCard
    { id = id ?? op.ToString(), name = id ?? op.ToString(), effects = { new CardEffect(op, aim, amount) { flat = flat, range = 6 } } };
    private static CombatCard Minor(string id, SpellBinding binding = SpellBinding.Unattuned) => new CombatCard
    { id = id, name = id, kind = binding == SpellBinding.Unattuned ? CardKind.Skill : CardKind.Spell, binding = binding, flicker = 0f, noteRole = BattleNoteRole.Minor,
        chord = new BattleChordModifier { binding = binding, requiresAdjacent = false } };
    private static void Add(BattleSide side, CombatCard card, int voice = 0) => side.deck.Add(new DeckCard { card = card, voice = voice });
    private static CombatSettings Settings() => new CombatSettings
    { tuning = new CombatTuning { maxMeasures = 8, variance = 0f, fatigueFrom = 100 }, symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 20 } } };
    private static BattleSetup Setup() => new BattleSetup
    { seed = 123, attacker = new BattleSide { name = "A", manual = true, sections = { Unit("Patient", 7) } },
        defender = new BattleSide { name = "D", manual = true, sections = { Unit("Threat", 8) } }, field = new Battlefield { age = 3 } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup, CombatSettings settings = null)
    { var run = BattleResolver.Begin(setup, settings ?? Settings(), forecastRuns: 1); run.BeginMeasure(); return run; }
    private static int Index(BattleResolver.BattleRun run, string id, bool attacker = true) => run.Hand(attacker).ToList().FindIndex(c => c.card.id == id);

    // ===== THE MEASURE =====

    [Test]
    public void AMeasureIsFourBeats_AndCompositionIsHeldTime()
    {
        var setup = Setup(); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 10f)); var run = Begin(setup);
        Assert.AreEqual(0, run.Beat); Assert.AreEqual(BattlePhase.Composition, run.Phase);
        Assert.IsNull(run.CommitCard(true, 0, 0)); Assert.AreEqual(0, run.Beat, "composing consumes no battlefield time");
        run.ResolveMeasure();
        Assert.AreEqual(4, run.Beat); Assert.AreEqual(190f, run.Defender.sections[0].integrity);
        var phases = run.Report.phases.Where(p => p.measure == 1).Select(p => p.phase).ToList();
        Assert.AreEqual(BattlePhase.Visualization, phases[0]); Assert.AreEqual(BattlePhase.Composition, phases[1]);
        Assert.Less(phases.IndexOf(BattlePhase.Commitment), phases.IndexOf(BattlePhase.Execution));
        Assert.Less(phases.IndexOf(BattlePhase.Execution), phases.IndexOf(BattlePhase.Abjuration));
        Assert.Less(phases.LastIndexOf(BattlePhase.Abjuration), phases.IndexOf(BattlePhase.Toll));
        Assert.Less(phases.IndexOf(BattlePhase.Toll), phases.IndexOf(BattlePhase.Assessment));
        run.BeginMeasure(); Assert.AreEqual(4, run.Beat); Assert.AreEqual(0, run.MeasureBeat);
    }

    [Test]
    public void ThirtyTwoDamageInTwoBeatsIsCertain_AndAWardStandingByThenAnswersIt()
    {
        var setup = Setup(); Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 32f));
        Add(setup.attacker, Card(CardOp.Guard, CardAim.Self, 15f, flat: true)); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2));
        var threat = run.Countdowns.Single(x => !x.attacker && x.kind == BattleActionKind.Card);
        Assert.AreEqual(2, threat.Remaining(run.Beat)); Assert.AreEqual(32f, threat.expectedIntegrity); Assert.IsTrue(threat.damageCertain); Assert.IsTrue(threat.syncopated);
        Assert.IsNull(run.CommitCard(true, 0, beat: 2), "a Ward raised on Beat 2 stands when the syncopated strike lands");
        Assert.IsNull(run.Perform()); Assert.AreEqual(200f, run.Attacker.sections[0].integrity);
        Assert.IsNull(run.Perform()); Assert.AreEqual(183f, run.Attacker.sections[0].integrity, "32 declared output minus 15 standing Ward, no performance reduction off the Cadence");
    }

    [Test]
    public void CountdownsCountBeatsToTheCadence_AndCarryAcrossMeasures()
    {
        var setup = Setup(); Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 32f)); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(false, 0, 0));
        Assert.AreEqual(4, run.Countdowns.Single(x => x.kind == BattleActionKind.Card).Remaining(run.Beat), "4 at Visualization: this Measure's Cadence");
        Assert.IsNull(run.Perform(2)); Assert.AreEqual(2, run.Countdowns.Single(x => x.kind == BattleActionKind.Card).Remaining(run.Beat));
    }

    [Test]
    public void OneMajorNotePerTrackPerMeasure()
    {
        var setup = Setup(); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "first")); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "second"));
        var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, Index(run, "first"), 0));
        StringAssert.Contains("One Major Note", run.CommitCard(true, Index(run, "second"), 0));
        Assert.AreEqual(1, run.Hand(true).Count);
    }

    [Test]
    public void MinorNotesSoundOnTheBeatsBeforeTheirMajor_OneNotePerBeat()
    {
        foreach (int minors in new[] { 1, 2, 3 })
        {
            var setup = Setup(); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "core"));
            for (int i = 0; i < minors; i++) Add(setup.attacker, Minor("m" + i));
            var run = Begin(setup);
            Assert.IsNull(run.CommitChord(true, Index(run, "core"), Enumerable.Range(0, minors).Select(i => Index(run, "m" + i)).ToList(), 0));
            var row = run.Score(true).Single(t => t.voice == 0);
            StringAssert.StartsWith("Major", row.beats[4]);
            for (int b = 1; b <= 4 - minors - 1; b++) Assert.IsNull(row.beats[b], $"Beat {b} stays free for a chord of {minors} minors");
            for (int b = 4 - minors; b <= 3; b++) StringAssert.StartsWith("Minor", row.beats[b]);
        }
        var placed = Setup(); Add(placed.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "core")); Add(placed.attacker, Minor("m"));
        var explicitRun = Begin(placed);
        Assert.IsNotNull(explicitRun.CommitChord(true, Index(explicitRun, "core"), new[] { Index(explicitRun, "m") }, 0, minorBeats: new[] { 4 }));
        Assert.IsNull(explicitRun.CommitChord(true, Index(explicitRun, "core"), new[] { Index(explicitRun, "m") }, 0, minorBeats: new[] { 1 }));
        StringAssert.StartsWith("Minor", explicitRun.Score(true).Single(t => t.voice == 0).beats[1]);
    }

    [Test]
    public void OnlyAStaccatoMajorMaySyncopate_AndCarriesAtMostOneMinor()
    {
        var setup = Setup(); setup.attacker.tempo = SpellTempo.Legato;
        var spell = Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "legato"); spell.kind = CardKind.Spell; spell.binding = SpellBinding.Flux; spell.flicker = 0f;
        Add(setup.attacker, spell); var run = Begin(setup);
        StringAssert.Contains("Staccato", run.CommitCard(true, 0, 0, beat: 2));
        var quick = Setup(); Add(quick.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "jab")); Add(quick.attacker, Minor("a")); Add(quick.attacker, Minor("b"));
        var q = Begin(quick);
        StringAssert.Contains("at most one Minor", q.CommitChord(true, Index(q, "jab"), new[] { Index(q, "a"), Index(q, "b") }, 0, beat: 3));
        Assert.IsNull(q.CommitChord(true, Index(q, "jab"), new[] { Index(q, "a") }, 0, beat: 3));
        Assert.IsNull(q.Perform(3)); Assert.AreEqual(195f, q.Defender.sections[0].integrity, "the syncopated Dyad lands on Beat 3");
    }

    [TestCase(false, 2)]
    [TestCase(true, 3)]
    public void LegatoSoundsThreeBeatsToAnchor_OneSoonerOnAStableHarmonicChannel(bool channel, int begins)
    {
        var setup = Setup(); setup.attacker.tempo = SpellTempo.Legato;
        if (channel) setup.field.hexes.Add(new BattleHexTerrain { hex = 7, harmonicChannel = true });
        var spell = Card(CardOp.Guard, CardAim.Self, 15f, flat: true); spell.kind = CardKind.Spell; spell.binding = SpellBinding.Flux; spell.flicker = 0f;
        spell.effects[0].op = CardOp.IntegrityDamage; spell.effects[0].aim = CardAim.Enemy;
        Add(setup.attacker, spell); var run = Begin(setup); Assert.IsNull(run.CommitCard(true, 0, 0));
        var row = run.Score(true).Single(t => t.voice == 0);
        StringAssert.StartsWith("Major", row.beats[begins]);
        for (int b = begins + 1; b <= 4; b++) Assert.AreEqual("Sustained", row.beats[b]);
        Assert.AreEqual(BattleFooting.Braced, run.Countdowns.Single(x => x.kind == BattleActionKind.Card).footing, "a Legato working is at least Braced");
    }

    // ===== THE REDRAW BELL =====

    [Test]
    public void TheRedrawBellRestsBeatsOfEveryFriendlyTrack_AndCostsNoTime()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Second", 1));
        Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 32f));
        Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "core"));
        for (int i = 0; i < 3; i++) Add(setup.attacker, Minor("m" + i));
        var run = Begin(setup);
        Assert.IsNull(run.CommitCard(false, 0, 0)); var enemy = run.Countdowns.Single(x => !x.attacker && x.kind == BattleActionKind.Card).dueBeat;
        Assert.IsNull(run.Redraw(true)); Assert.AreEqual(0, run.Beat, "the Bell rests Beats, it does not perform them");
        Assert.IsTrue(run.Score(true).All(t => t.beats[1] == "Rest"));
        Assert.IsTrue(run.Score(false).All(t => t.beats[1] != "Rest"), "the enemy's committed Score performs all four Beats regardless");
        Assert.AreEqual(enemy, run.Countdowns.Single(x => !x.attacker && x.kind == BattleActionKind.Card).dueBeat);
        var hand = run.Hand(true).ToList();
        string tetrad = run.CommitChord(true, hand.FindIndex(c => c.card.id == "core"), new[] { "m0", "m1", "m2" }.Select(id => hand.FindIndex(c => c.card.id == id)).ToList(), 0);
        Assert.IsNotNull(tetrad, "one ring makes a Tetrad impossible this Measure");
        Assert.IsNull(run.Redraw(true)); Assert.IsTrue(run.Score(true).All(t => t.beats[2] == "Rest"));
        Assert.IsNotNull(run.Redraw(true), "at most two rings a Measure");
        Assert.IsNull(run.CommitMovement(true, 1, 2)); Assert.IsNull(run.Perform());
        Assert.AreEqual(2, run.Attacker.sections[1].battleHex, "Steps already ordered still happen on a Rest");
    }

    [Test]
    public void TheBellCannotRestABeatOnWhichAComposedNoteBegins()
    {
        var setup = Setup(); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "core")); Add(setup.attacker, Minor("m"));
        var run = Begin(setup);
        Assert.IsNull(run.CommitChord(true, Index(run, "core"), new[] { Index(run, "m") }, 0, minorBeats: new[] { 1 }));
        StringAssert.Contains("Beat 1", run.Redraw(true));
    }

    // ===== HONEST INTENT =====

    [Test]
    public void ACommittedScoreIsAPromise_NothingCanBeAddedOnceTheBeatsBegin()
    {
        var setup = Setup(); setup.defender.manual = false; Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 32f));
        Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f)); var run = Begin(setup);
        var intent = run.Countdowns.Single(x => !x.attacker && x.kind == BattleActionKind.Card);
        Assert.IsTrue(intent.confirmed, "a performed side's Score is Confirmed at Visualization");
        run.Defender.deck[0].card.effects[0].amount = 999f;
        Assert.IsNull(run.Perform()); StringAssert.Contains("committed", run.CommitCard(true, 0, 0));
        run.ResolveMeasure(); Assert.AreEqual(168f, run.Attacker.sections[0].integrity);
        Assert.AreEqual(32f, run.Report.commitments.Single(x => x.id == intent.id).expectedIntegrity);
    }

    [Test]
    public void RehearsalPreviewsTheMeasureWithoutTouchingTheLiveBattle()
    {
        var setup = Setup(); setup.defender.manual = false; Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 32f));
        Add(setup.attacker, Card(CardOp.Guard, CardAim.Self, 15f, flat: true)); var run = Begin(setup);
        var commands = new[] { new BattleCommand { kind = BattleCommandKind.Card, beat = 1 }, new BattleCommand { kind = BattleCommandKind.Wait, beats = 4 } };
        var first = run.Preview(commands); var second = run.Preview(commands);
        Assert.AreEqual(0, run.Beat); Assert.AreEqual(200f, run.Attacker.sections[0].integrity); Assert.AreEqual(1, run.Hand(true).Count);
        CollectionAssert.AreEqual(first.after.units.Select(x => x.integrity), second.after.units.Select(x => x.integrity));
        Assert.IsNull(run.CommitCard(true, 0, beat: 1)); run.ResolveMeasure();
        CollectionAssert.AreEqual(first.after.units.Select(x => x.integrity), run.Visualize().units.Select(x => x.integrity));
        var redraw = Setup(); Add(redraw.attacker, Card(CardOp.Guard, CardAim.Self, 1f)); var unknown = Begin(redraw);
        var hidden = unknown.Preview(new[] { new BattleCommand { kind = BattleCommandKind.Redraw }, new BattleCommand { kind = BattleCommandKind.Card } });
        StringAssert.Contains("unknown", hidden.failures.Single()); Assert.AreEqual(0, unknown.Rings(true));
    }

    [Test]
    public void TheCadenceHasNoInitiative_TwoReleasesStrikeEachOtherInTheSameInstant()
    {
        var setup = Setup(); setup.attacker.sections[0].recon = 5f;
        Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 300f)); Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 300f));
        var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0, 0)); Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.IsTrue(run.Attacker.sections[0].destroyed && run.Defender.sections[0].destroyed, "foresight, not initiative, is a defense");
    }

    // ===== MOVEMENT AND CONTACT =====

    [TestCase(2f, 1)]
    [TestCase(3f, 2)]
    [TestCase(5f, 3)]
    public void PaceLimitsTheStepsOfAMeasure_AndAPathContinuesNextMeasure(float speed, int steps)
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Walker", 11, speed); setup.defender.sections[0].battleHex = 4;
        var run = Begin(setup);
        Assert.IsNull(run.ComposePath(true, 0, new[] { 12, 13, 14, 15 }));
        Assert.AreEqual(steps, run.MovementIntent(true).Count);
        CollectionAssert.AreEqual(Enumerable.Range(1, steps), run.MovementIntent(true).Select(x => x.beat), "Steps take the earliest Beats");
        run.ResolveMeasure();
        Assert.AreEqual(new[] { 12, 13, 14, 15 }[steps - 1], run.Attacker.sections[0].battleHex);
        run.BeginMeasure();
        Assert.AreEqual(Math.Min(steps, 4 - steps), run.MovementIntent(true).Count, "the rest of the path is walked in the following Measure");
    }

    [Test]
    public void AStepCanBeDraggedToALaterBeat()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Walker", 11); setup.defender.sections[0].battleHex = 4; var run = Begin(setup);
        Assert.IsNull(run.ComposePath(true, 0, new[] { 12, 13 }, new[] { 2, 4 }));
        Assert.IsNull(run.Perform()); Assert.AreEqual(11, run.Attacker.sections[0].battleHex);
        Assert.IsNull(run.Perform()); Assert.AreEqual(12, run.Attacker.sections[0].battleHex);
        Assert.IsNull(run.Perform()); Assert.AreEqual(12, run.Attacker.sections[0].battleHex);
        Assert.IsNull(run.Perform()); Assert.AreEqual(13, run.Attacker.sections[0].battleHex);
    }

    [Test]
    public void ARiverCostsTwoSteps_AndSnowCostsSwiftUnitsAStep()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Wader", 1); setup.defender.sections[0].battleHex = 15;
        setup.field.hexes.Add(new BattleHexTerrain { hex = 2, river = true }); var run = Begin(setup);
        Assert.IsNull(run.ComposePath(true, 0, new[] { 2, 3 }));
        Assert.AreEqual(1, run.MovementIntent(true).Count); Assert.AreEqual(2, run.MovementIntent(true).Single().beat, "two Steps to cross, arriving on the second");
        var snow = Setup(); snow.attacker.sections[0] = Unit("Rider", 11, 5f); snow.defender.sections[0].battleHex = 4;
        snow.field.hexes.Add(new BattleHexTerrain { hex = 11, snow = true }); var cold = Begin(snow);
        Assert.AreEqual(2, cold.PaceOf(true, 0));
    }

    [Test]
    public void HostileUnitsEnteringOneEmptyHexBothEnter_AndContactPinsThem()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("A", 1); setup.defender.sections[0] = Unit("D", 3); var run = Begin(setup);
        Assert.IsNull(run.ComposePath(true, 0, new[] { 2, 3 })); Assert.IsNull(run.ComposePath(false, 0, new[] { 2, 1 }));
        Assert.IsNull(run.Perform());
        Assert.AreEqual(2, run.Attacker.sections[0].battleHex); Assert.AreEqual(2, run.Defender.sections[0].battleHex);
        Assert.IsTrue(run.Visualize().units.All(u => u.pinned), "engaged units are pinned");
        run.ResolveMeasure();
        Assert.AreEqual(2, run.Attacker.sections[0].battleHex, "the second Step is lost at contact");
        Assert.AreEqual(2, run.Report.spatialEvents.Count(e => e.cause == BattleSpatialCause.FailedMovementCommitment));
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.Contact));
    }

    [Test]
    public void HostileUnitsExchangingHexesClashAtTheSeam_AndFriendsRotate()
    {
        var setup = Setup(); var run = Begin(setup);
        Assert.IsNull(run.CommitMovement(true, 0, 8)); Assert.IsNull(run.CommitMovement(false, 0, 7)); Assert.IsNull(run.Perform());
        Assert.AreEqual(7, run.Attacker.sections[0].battleHex); Assert.AreEqual(8, run.Defender.sections[0].battleHex);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.Clash));
        Assert.IsTrue(run.Visualize().units.All(u => u.pinned), "a Clash is a Skirmish across the shared edge");
        var friends = Setup(); friends.attacker.sections.Add(Unit("Other", 6)); friends.defender.sections[0].battleHex = 15; var rotation = Begin(friends);
        Assert.IsNull(rotation.CommitMovement(true, 0, 6)); Assert.IsNull(rotation.CommitMovement(true, 1, 7)); Assert.IsNull(rotation.Perform());
        Assert.AreEqual(6, rotation.Attacker.sections[0].battleHex); Assert.AreEqual(7, rotation.Attacker.sections[1].battleHex);
        var stack = Setup(); stack.attacker.sections.Add(Unit("Other", 1)); stack.attacker.sections[0].battleHex = 12; stack.defender.sections[0].battleHex = 15; var crowd = Begin(stack);
        Assert.IsNull(crowd.CommitMovement(true, 0, 6)); Assert.IsNull(crowd.CommitMovement(true, 1, 6)); Assert.IsNull(crowd.Perform());
        Assert.IsTrue(crowd.Spatial.IsStacked(6, true), "two friends entering one hex stack");
    }

    [Test]
    public void TheSkirmishVerdictReadsPressure_NotTheBiggerStack()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Big", 7); setup.attacker.sections[0].maxIntegrity = setup.attacker.sections[0].integrity = 400f;
        setup.defender.sections[0] = Unit("Small", 7); setup.defender.sections[0].maxIntegrity = setup.defender.sections[0].integrity = 100f;
        Add(setup.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 80f)); Add(setup.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 12f));
        var run = Begin(setup); Assert.IsNull(run.CommitCard(true, 0, 0)); Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.AreEqual(320f, run.Attacker.sections[0].integrity); Assert.AreEqual(88f, run.Defender.sections[0].integrity);
        Assert.AreNotEqual(7, run.Attacker.sections[0].battleHex, "20% Pressure against 12%: the bigger stack loses the ground");
        Assert.AreEqual(7, run.Defender.sections[0].battleHex);
        var even = Setup(); even.attacker.sections[0].battleHex = 7; even.defender.sections[0].battleHex = 7;
        Add(even.defender, Card(CardOp.IntegrityDamage, CardAim.Enemy, 20f)); Add(even.attacker, Card(CardOp.IntegrityDamage, CardAim.Enemy, 21f));
        var held = Begin(even); Assert.IsNull(held.CommitCard(true, 0, 0)); Assert.IsNull(held.CommitCard(false, 0, 0)); held.ResolveMeasure();
        Assert.AreEqual(7, held.Attacker.sections[0].battleHex); Assert.AreEqual(7, held.Defender.sections[0].battleHex, "a slight difference holds and continues");
    }

    [Test]
    public void SkirmishAttritionBeginsOnTheBeatAfterContact()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("A", 1, attack: 20f); setup.defender.sections[0] = Unit("D", 2);
        setup.attacker.sections[0].defense = setup.defender.sections[0].defense = 0f; var run = Begin(setup);
        Assert.IsNull(run.CommitMovement(true, 0, 2)); Assert.IsNull(run.Order(true, 0, BattleStandingOrder.Hold));
        Assert.IsNull(run.Perform()); Assert.AreEqual(200f, run.Defender.sections[0].integrity, "contact on Beat 1; no blow is traded on the Beat of contact");
        Assert.IsNull(run.Perform()); Assert.Less(run.Defender.sections[0].integrity, 200f);
    }

    [Test]
    public void BracedAndAnchoredFootingHoldTheirPerformerStill()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Caster", 11); setup.defender.sections[0].battleHex = 4;
        var braced = Card(CardOp.Rally, CardAim.Self, 1f, id: "braced"); braced.footing = BattleFooting.Braced; Add(setup.attacker, braced); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0));
        Assert.IsNotNull(run.ComposePath(true, 0, new[] { 12 }, new[] { 3 }), "Braced: no Step on the Beat before the release");
        Assert.IsNull(run.ComposePath(true, 0, new[] { 12, 13 }));
        CollectionAssert.AreEqual(new[] { 1, 2 }, run.MovementIntent(true).Select(x => x.beat));
        var anchor = Setup(); anchor.attacker.sections[0] = Unit("Caster", 11); anchor.defender.sections[0].battleHex = 4;
        var anchored = Card(CardOp.Rally, CardAim.Self, 1f, id: "anchored"); anchored.footing = BattleFooting.Anchored; Add(anchor.attacker, anchored); Add(anchor.attacker, Minor("m"));
        var still = Begin(anchor); Assert.IsNull(still.CommitChord(true, Index(still, "anchored"), new[] { Index(still, "m") }, minorBeats: new[] { 2 }));
        Assert.IsNull(still.ComposePath(true, 0, new[] { 12, 13 }));
        CollectionAssert.AreEqual(new[] { 1 }, still.MovementIntent(true).Select(x => x.beat), "Anchored from its first Note");
    }

    [Test]
    public void DisplacingABracedPerformerIsAnInterruption()
    {
        var setup = Setup(); setup.attacker.sections[0].battleHex = 7; setup.defender.sections[0].battleHex = 8;
        var working = Card(CardOp.IntegrityDamage, CardAim.Enemy, 5f, id: "working"); working.footing = BattleFooting.Braced; Add(setup.attacker, working);
        Add(setup.attacker, Minor("m")); Add(setup.defender, Card(CardOp.Push, CardAim.Enemy, 1f, id: "shove"));
        var run = Begin(setup);
        Assert.IsNull(run.CommitChord(true, Index(run, "working"), new[] { Index(run, "m") }, 0, minorBeats: new[] { 1 }));
        Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2)); Assert.IsNull(run.Perform(2));
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Interrupted && e.attacker));
        Assert.AreEqual(1, run.PreparedWeaves.Single().interference);
    }

    // ===== COMMAND =====

    [Test]
    public void CommandBandwidthBoundsComposedFormationTracks_TheEliteCoreAlwaysComposes()
    {
        var setup = Setup(); setup.attacker.sections.Clear(); setup.defender.sections[0].battleHex = 9;
        for (int i = 0; i < 4; i++) { setup.attacker.sections.Add(Unit("Company " + i, new[] { 0, 1, 6, 12 }[i])); Add(setup.attacker, Card(CardOp.Rally, CardAim.Self, 1f, id: "c" + i), i); }
        setup.attacker.sections[3].leader = new BattleLegend { name = "Named" };
        var run = Begin(setup);
        Assert.AreEqual(2, run.Bandwidth(true), "2 with no Conductor");
        Assert.IsNull(run.CommitCard(true, Index(run, "c0"))); Assert.IsNull(run.CommitCard(true, Index(run, "c1")));
        StringAssert.Contains("Command Bandwidth", run.CommitCard(true, Index(run, "c2")));
        Assert.IsNull(run.CommitCard(true, Index(run, "c3")), "a named Legend is always composed directly");
        var led = Setup(); led.attacker.conductor = new BattleLegend { name = "Conductor", scores = { ["Resonance"] = 33 } }; var wide = Begin(led);
        Assert.AreEqual(5, wide.Bandwidth(true), "2 + Expert Resonance (3)");
    }

    [Test]
    public void StandingOrdersPersist_AndAnAdvanceWalksTowardContact()
    {
        var setup = Setup(); setup.attacker.sections[0] = Unit("Line", 6, attack: 5f); setup.defender.sections[0].battleHex = 9; var run = Begin(setup);
        Assert.IsNull(run.Order(true, 0, BattleStandingOrder.Hold)); run.ResolveMeasure(); Assert.AreEqual(6, run.Attacker.sections[0].battleHex);
        run.BeginMeasure(); Assert.IsNull(run.Order(true, 0, BattleStandingOrder.Advance)); run.ResolveMeasure();
        Assert.AreEqual(8, run.Attacker.sections[0].battleHex, "two Steps of Standard Pace toward the enemy");
        run.BeginMeasure(); run.ResolveMeasure(); Assert.AreEqual(9, run.Attacker.sections[0].battleHex, "the order persists into contact");
    }

    // ===== ABJURATION =====

    private static BattleResolver.BattleRun Abjured(float strength, float rendition, int raise = 4, SpellBinding ward = SpellBinding.Unattuned, SpellBinding release = SpellBinding.Unattuned, bool magical = false)
    {
        var setup = Setup();
        var hit = Card(CardOp.IntegrityDamage, CardAim.Enemy, 40f, id: "release"); hit.binding = release;
        if (magical) { hit.kind = CardKind.Spell; hit.flicker = 0f; }
        hit.effects.Add(new CardEffect(CardOp.Expose, CardAim.Enemy, .5f) { range = 6 });
        Add(setup.defender, hit);
        var guard = Card(magical ? CardOp.Ward : CardOp.Guard, CardAim.Self, strength, flat: true, id: "ward"); guard.binding = ward; Add(setup.attacker, guard);
        var run = Begin(setup);
        Assert.IsNull(run.CommitCard(false, 0, 0)); Assert.IsNull(run.CommitCard(true, 0, rendition: rendition, beat: raise));
        run.ResolveMeasure(); return run;
    }

    [TestCase(.9f, 165f)]
    [TestCase(1f, 176f)]
    [TestCase(1.25f, 183.5f)]
    public void AbjurationSubtractsTheWard_ThenShavesByPerformance(float rendition, float expected)
    {
        // Missed: the Ward is halved (5) and nothing is shaved; Clean: (40-10) less 20%; Perfect: (40-10) less 45%.
        Assert.AreEqual(expected, Abjured(10f, rendition).Attacker.sections[0].integrity, 1e-3f);
    }

    [Test]
    public void AWardAsStrongAsTheReleaseCancelsItOutright_StatusesIncluded()
    {
        var run = Abjured(50f, 1f);
        Assert.AreEqual(200f, run.Attacker.sections[0].integrity);
        Assert.AreEqual(0f, run.Visualize().units.Single(u => u.attacker).exposed, "the release's Minor effects and statuses are cancelled with it");
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Cancelled));
        Assert.Greater(Abjured(50f, .9f).Visualize().units.Single(u => u.attacker).exposed, 0f, "a Missed response never cancels");
    }

    [Test]
    public void AWardGrowsWithEveryBeatHeldBeforeTheCadence_AndFollowsTheCircle()
    {
        Assert.AreEqual(176f, Abjured(10f, 1f, raise: 4).Attacker.sections[0].integrity, 1e-3f);
        Assert.AreEqual(182f, Abjured(10f, 1f, raise: 1).Attacker.sections[0].integrity, 1e-3f, "x1.75 after three Beats: (40-17.5) less 20%");
        float counters = Abjured(10f, 1f, ward: SpellBinding.Crystal, release: SpellBinding.Resonance, magical: true).Attacker.sections[0].integrity;
        float countered = Abjured(10f, 1f, ward: SpellBinding.Crystal, release: SpellBinding.Cindergale, magical: true).Attacker.sections[0].integrity;
        Assert.Greater(counters, countered, "a Crystal Ward stands at 1.5x against Resonance and at half against Cindergale");
    }

    [Test]
    public void AbjurationDefendsOnlyWhatWasPrepared_UncoveredFrequenciesLandInFull()
    {
        var setup = Setup();
        var spell = Card(CardOp.IntegrityDamage, CardAim.Enemy, 40f, id: "spell"); spell.kind = CardKind.Spell; spell.binding = SpellBinding.Void; spell.flicker = 0f;
        Add(setup.defender, spell); Add(setup.attacker, Card(CardOp.Guard, CardAim.Self, 100f, flat: true));
        var run = Begin(setup); Assert.IsNull(run.CommitCard(false, 0, 0)); Assert.IsNull(run.CommitCard(true, 0)); run.ResolveMeasure();
        Assert.AreEqual(160f, run.Attacker.sections[0].integrity, "a physical Guard cannot answer a Void working");
    }
}
