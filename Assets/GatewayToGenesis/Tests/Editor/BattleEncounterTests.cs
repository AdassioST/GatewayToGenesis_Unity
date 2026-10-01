using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleEncounterTests
{
    [Test]
    public void SeerMustRemainLucidAndUnengaged_AndDeploymentChoiceSurvivesSave()
    {
        var setup = Setup(); setup.attacker.sections[0].leader = new BattleLegend { name = "Seer", scores = { ["Luminance"] = 42 } };
        setup.attacker.sections.Add(new CombatSection { name = "Scout", leader = new BattleLegend { name = "Scout", scores = { ["Luminance"] = 28 } },
            eliteRole = BattleEliteRole.CommandStaff, battleHex = 0, count = 1, integrity = 500, maxIntegrity = 500, composure = 100, maxComposure = 100 });
        var encounter = Encounter(setup); encounter.BeginMeasure(); Assert.AreEqual(4, encounter.Run.Clarity(true));
        Assert.IsNull(encounter.Command(new BattleCommand { kind = BattleCommandKind.Seer, section = 1 })); Assert.AreEqual(2, encounter.Run.Clarity(true));
        var saved = (BattleEncounterState)SaveStateCodec.Read(SaveStateCodec.Write(encounter.State, typeof(BattleEncounterState)), typeof(BattleEncounterState));
        var restored = new BattleEncounter(saved, Settings(), new BattlePremonitions()); Assert.AreEqual(1, restored.Run.SeerVoice(true));
        Assert.AreEqual(2, restored.Run.Clarity(true)); restored.Run.Defender.sections[0].battleHex = 0; Assert.AreEqual(1, restored.Run.Clarity(true));
        restored.Run.Attacker.sections[1].mindBroken = true; Assert.AreEqual(0, restored.Run.Clarity(true));
        restored.Run.Attacker.sections[1].mindBroken = false; restored.Run.Attacker.sections[1].deathKnell = true; Assert.AreEqual(0, restored.Run.Clarity(true));
    }
    [TestCase(33, 2)] [TestCase(42, 4)]
    public void ExpertReducesVeilsAndMasterPiercesThem(int score, int effective)
    {
        var setup = Setup(); setup.attacker.sections[0].leader = new BattleLegend { name = "Seer", scores = { ["Luminance"] = score } };
        setup.field.hexes.Add(new BattleHexTerrain { hex = 8, veil = 2 }); var encounter = Encounter(setup); encounter.BeginMeasure();
        Assert.AreEqual(effective, encounter.Run.IntentClarity(true, false, 0));
    }
    [TestCase(0, 1, false, true)] [TestCase(1, 2, true, true)] [TestCase(2, 2, true, true)]
    public void ClarityRevealsOnlyItsTierWithoutMutatingCommittedIntent(int clarity, int count, bool holdVisible, bool damageVisible)
    {
        var major = new BattleActionIntent { attacker = false, name = "Strike", card = "strike", confirmed = true, target = 0, dueBeat = 4,
            holdLimit = 5, expectedIntegrity = 30, damageCertain = true };
        var minor = major.Clone(); minor.minor = true;
        var visible = BattleIntentReading.Read(new[] { major, minor }, true, clarity, null);
        Assert.AreEqual(count, visible.Count); Assert.AreEqual(holdVisible ? 5 : 0, visible[0].holdLimit);
        Assert.AreEqual(damageVisible ? 30 : 0, visible[0].expectedIntegrity); Assert.AreEqual(damageVisible, visible[0].damageCertain);
        Assert.IsTrue(visible[0].confirmed); Assert.AreEqual(5, major.holdLimit); Assert.AreEqual(30, major.expectedIntegrity);
    }
    [TestCase(1, 4)] [TestCase(2, -1)]
    public void VeiledActivityCannotExposeTheIdentityTargetOrConfirmedFlag(int veil, int dueBeat)
    {
        var original = new BattleActionIntent { attacker = false, name = "Secret", card = "secret", confirmed = true, target = 3, dueBeat = 4,
            binding = SpellBinding.Flux, expectedIntegrity = 99, damageCertain = true };
        var visible = BattleIntentReading.Read(new[] { original }, true, 0, _ => veil).Single();
        Assert.IsTrue(visible.unverified); Assert.IsFalse(visible.confirmed); Assert.IsNull(visible.card); Assert.AreEqual(-1, visible.target);
        Assert.AreEqual(dueBeat, visible.dueBeat); Assert.AreEqual(0, visible.expectedIntegrity); Assert.IsTrue(original.confirmed);
        var own = BattleIntentReading.Read(new[] { original }, false, 0, _ => veil).Single(); Assert.AreEqual("secret", own.card);
    }
    [Test]
    public void PhantomRemainsUnverifiedUntilClarityExposesIt()
    {
        var phantom = new BattleActionIntent { attacker = false, phantom = true, confirmed = false, dueBeat = 4 };
        var visible = BattleIntentReading.Read(new[] { phantom }, true, 1, null).Single(); Assert.IsTrue(visible.unverified); Assert.IsFalse(visible.confirmed);
        Assert.IsEmpty(BattleIntentReading.Read(new[] { phantom }, true, 2, null));
    }
    private static CombatSettings Settings() => new CombatSettings { tuning = new CombatTuning { maxMeasures = 1, variance = 0, fatigueFrom = 100 },
        symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 30 } } };
    private static BattleSetup Setup() => new BattleSetup { seed = 94, field = new Battlefield { age = 3 },
        attacker = new BattleSide { name = "A", sections = { new CombatSection { name = "A", count = 1, battleHex = 7, eliteRole = BattleEliteRole.Guard, maxIntegrity = 500, integrity = 500, maxComposure = 100, composure = 100, speed = 0 } },
            deck = { new DeckCard { voice = 0, card = new CombatCard { id = "hit", name = "Hit", purpose = SpellPurpose.Offensive, effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 20) { range = 16 } } } } } },
        defender = new BattleSide { name = "D", sections = { new CombatSection { name = "D", count = 1, battleHex = 8, maxIntegrity = 500, integrity = 500, maxComposure = 100, composure = 100, speed = 0 } } } };
    private static BattleEncounter Encounter(BattleSetup setup = null, BattlePremonitions retries = null) => new BattleEncounter(new BattleEncounterState { id = "fight", playerAttacker = true,
        original = setup ?? Setup(), forecast = new BattleForecast { runs = 10, attackerWins = 5, defenderWins = 5 } }, Settings(), retries);
    private static void Echo(BattleEncounter encounter, BattleRhythmChallenge frame)
    {
        for (int lane = 0; lane < frame.lanes.Count; lane++) if (frame.lanes[lane].playable)
            foreach (double at in frame.lanes[lane].echo) Assert.IsNull(encounter.Input(lane, frame.start + at));
        Assert.IsNull(encounter.Complete(frame.end + .2));
    }
    private static void End(BattleEncounter encounter)
    {
        while (!encounter.Run.Over)
        {
            if (encounter.Run.AwaitingAbjuration) Echo(encounter, encounter.Abjuration(100 + encounter.Run.Beat));
            else Echo(encounter, encounter.Rhythm(100 + encounter.Run.Beat, new BattleRhythmOptions { assist = true }));
        }
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void MandatoryManualGatesResolveFinishAndClone(int flag)
    {
        var setup = Setup(); if (flag == 0) setup.boss = true; if (flag == 1) setup.majorEncounter = true; if (flag == 2) setup.decisive = true; if (flag == 3) setup.originalEight = true;
        Assert.IsTrue(setup.Clone(7).RequiresManual); Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(setup, Settings()));
        var encounter = Encounter(setup); Assert.IsFalse(encounter.CanAutoResolve); Assert.IsNotNull(encounter.AutoResolve());
        encounter.BeginMeasure(); Assert.Throws<InvalidOperationException>(() => encounter.Run.Finish());
        Assert.IsNotNull(encounter.Command(new BattleCommand { attacker = false, kind = BattleCommandKind.Wait }));
        End(encounter); Assert.IsNull(encounter.FinishAttempt()); Assert.IsTrue(encounter.State.accepted); Assert.IsNotNull(encounter.FinishAttempt());
        Assert.AreEqual(500, setup.attacker.sections[0].integrity, "campaign input remains untouched until the owner applies accepted aftermath");
    }
    [Test]
    public void ThreePremonitionsRestoreUntouchedContact_ThenTheFourthLossIsAccepted()
    {
        var retries = new BattlePremonitions { divination = 2, prophetical = 1 }; retries.BeginEncounter("fight", false, true, true);
        var encounter = Encounter(retries: retries);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.IsNotNull(encounter.AutoResolve()); Assert.IsTrue(encounter.Retried); Assert.IsNull(encounter.Report); Assert.IsFalse(encounter.State.started);
            Assert.AreEqual(500, encounter.State.original.attacker.sections[0].integrity); Assert.AreEqual(2 - attempt, retries.Remaining);
            Assert.AreEqual(5, encounter.State.forecast.attackerWins);
            var restored = (BattleEncounterState)SaveStateCodec.Read(SaveStateCodec.Write(encounter.State, typeof(BattleEncounterState)), typeof(BattleEncounterState));
            encounter = new BattleEncounter(restored, Settings(), retries.Clone()); retries = (BattlePremonitions)SaveStateCodec.Read(SaveStateCodec.Write(retries, typeof(BattlePremonitions)), typeof(BattlePremonitions));
            // Reload the same ledger; it cannot refill the boss boundary.
            retries.BeginEncounter("fight", true, true, true); encounter = new BattleEncounter(restored, Settings(), retries);
        }
        Assert.IsTrue(retries.exhaustedAchievement); Assert.IsNull(encounter.AutoResolve()); Assert.IsTrue(encounter.State.accepted); Assert.IsNotNull(encounter.Report);
        Assert.IsNotNull(encounter.AutoResolve(), "acceptance cannot be applied twice");
    }
    [Test]
    public void SaveInsideActiveEchoRestoresScorePilesInputAndFutureOutput()
    {
        var encounter = Encounter(); encounter.BeginMeasure(); Assert.IsNull(encounter.Spotlight(0));
        var cmd = new BattleCommand { kind = BattleCommandKind.Card, handIndex = 0, target = 0, beat = 4 }; Assert.IsNull(encounter.Command(cmd)); cmd.beat = 1;
        Echo(encounter, encounter.Rhythm(10, null)); Echo(encounter, encounter.Rhythm(20, null)); Echo(encounter, encounter.Rhythm(30, null));
        var frame = encounter.Rhythm(40, null); Assert.IsNull(encounter.Input(0, frame.start + frame.lanes[0].echo[0])); encounter.State.clock = frame.end - .1;
        var saved = SaveStateCodec.Write(encounter.State, typeof(BattleEncounterState));
        var restored = new BattleEncounter((BattleEncounterState)SaveStateCodec.Read(saved, typeof(BattleEncounterState)), Settings(), new BattlePremonitions());
        Assert.AreEqual(3, restored.Run.MeasureBeat); Assert.AreEqual(encounter.State.clock, restored.State.clock); Assert.AreEqual(1, restored.Run.Rhythm.inputs.Count);
        Assert.AreEqual(encounter.Run.Countdowns.Single(x => x.attacker).dueBeat, restored.Run.Countdowns.Single(x => x.attacker).dueBeat);
        Assert.IsNull(encounter.Complete(frame.end + .2)); Assert.IsNull(restored.Complete(frame.end + .2));
        if (encounter.Run.AwaitingAbjuration) { Echo(encounter, encounter.Abjuration(50)); Echo(restored, restored.Abjuration(50)); }
        Assert.IsNull(encounter.FinishAttempt()); Assert.IsNull(restored.FinishAttempt());
        Assert.AreEqual(encounter.Defender.sections[0].integrity, restored.Defender.sections[0].integrity);
        CollectionAssert.AreEqual(encounter.Report.rhythm.SelectMany(x => x.results).Select(x => x.grade), restored.Report.rhythm.SelectMany(x => x.results).Select(x => x.grade));
        Assert.AreEqual(500, encounter.State.original.defender.sections[0].integrity);
    }
    [Test]
    public void AutomaticAndCleanManualDecisionsProduceTheSameState()
    {
        var setup = Setup(); var auto = setup.Clone(setup.seed); var autoReport = BattleResolver.Resolve(auto, Settings());
        var manual = Encounter(setup); manual.BeginMeasure(); Assert.IsNull(manual.Command(new BattleCommand { kind = BattleCommandKind.Card, handIndex = 0, target = 0, beat = 4 }));
        End(manual); manual.FinishAttempt(); Assert.AreEqual(auto.defender.sections[0].integrity, manual.Defender.sections[0].integrity);
        Assert.AreEqual(auto.attacker.sections[0].composure, manual.Attacker.sections[0].composure); Assert.AreEqual(autoReport.winner, manual.Report.winner);
    }
    [Test]
    public void ElitePermanentLossDominatesSmallParty_OrdinaryArmyLossStaysProportional()
    {
        var small = Setup().attacker; small.sections.Add(small.sections[0].Clone()); small.sections[1].permanentDeath = true;
        var loss = new SideResult { integrityBefore = 1000, integrityAfter = 500 };
        Assert.AreEqual(BattleOutcome.PyrrhicVictory, BattleLossBurden.Victory(small, BattleLossBurden.Assess(small, loss), 1));
        small.sections.AddRange(Enumerable.Range(0, 8).Select(_ => new CombatSection { count = 1, eliteRole = BattleEliteRole.Guard }));
        Assert.AreEqual(BattleOutcome.CloseVictory, BattleLossBurden.Victory(small, .1f, 1));
        var army = new BattleSide { sections = { new CombatSection { count = 20000 } } };
        Assert.AreEqual(BattleOutcome.DecisiveVictory, BattleLossBurden.Victory(army, .0006f, 1));
        army.sections.Add(new CombatSection { count = 1, eliteRole = BattleEliteRole.Conductor, permanentDeath = true });
        Assert.GreaterOrEqual(BattleLossBurden.Assess(army, new SideResult { integrityBefore = 20000, integrityAfter = 19988 }), .5f);
    }
    [TestCase(.1f, BattleOutcome.DecisiveVictory)] [TestCase(.3f, BattleOutcome.CloseVictory)] [TestCase(.6f, BattleOutcome.PyrrhicVictory)]
    public void MythicalIsAdditiveAcrossAllThreeVictoryCategories(float loss, BattleOutcome tier)
    {
        var setup = Setup(); setup.attacker.manual = true; setup.defender.manual = true; setup.defender.sections[0].integrity = 1;
        setup.attacker.sections[0].integrity = 500 * (1 - loss);
        var run = BattleResolver.Begin(setup, Settings(), 0); run.Prediction = new BattleForecast { runs = 10, defenderWins = 10 };
        // Loss Burden's proportional baseline is the force that arrived; inflict the intended loss after battle initialization.
        run.Attacker.sections[0].integrity *= 1 - loss; run.BeginMeasure(); run.CommitCard(true, 0, 0); run.ResolveMeasure(); var report = run.Finish();
        Assert.IsTrue(report.attacker.mythical); Assert.AreEqual(tier, report.attacker.outcome); Assert.AreEqual(tier, report.outcome);
        Assert.IsFalse(BattleLossBurden.Mythical(true, false, 0)); Assert.IsFalse(BattleLossBurden.Mythical(true, true, .2f));
    }
    [Test]
    public void AchievementsUseCommittedEvidenceAndCannotTriggerVacuously()
    {
        Assert.IsFalse(AchievementTriggers.Rules["this-is-gateway-to-genesis"](AchievementEvent.Of(AchievementSignal.PremonitionsExhausted, 0)));
        Assert.IsTrue(AchievementTriggers.Rules["this-is-gateway-to-genesis"](AchievementEvent.Of(AchievementSignal.PremonitionsExhausted, 3)));
        Assert.IsTrue(AchievementTriggers.Rules["veni-vidi-vici"](AchievementEvent.Of(AchievementSignal.MythicalBattleWon)));
    }
}
