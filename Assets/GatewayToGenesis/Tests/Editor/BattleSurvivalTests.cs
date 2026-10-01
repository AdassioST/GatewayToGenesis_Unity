using System;
using System.Linq;
using NUnit.Framework;

public class BattleSurvivalTests
{
    [Test]
    public void SecondaryConductorRestoresCommandWithoutErasingTheOriginalMindBreak()
    {
        var setup = Setup(); var primary = setup.attacker.sections[0]; primary.eliteRole = BattleEliteRole.Conductor; primary.composure = 20;
        setup.attacker.conductor = primary.leader;
        var secondary = Unit("B", 0); secondary.eliteRole = BattleEliteRole.CommandStaff; setup.attacker.sections.Add(secondary);
        var run = Begin(setup); run.Perform();
        Assert.AreEqual("B", run.Attacker.conductor.name); Assert.AreEqual(BattleEliteRole.Conductor, run.Attacker.sections[1].eliteRole);
        Assert.IsTrue(run.Attacker.sections[0].mindBroken); Assert.AreEqual("A", run.Attacker.sections[0].leader.name);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.ConductorDisrupted));
        Assert.IsTrue(run.Report.log.Any(line => line.Contains("takes the baton")));
    }
    [Test]
    public void DecentralizedCommandReducesTheShockOfLosingTheConductor()
    {
        float Remaining(BattleCommandCulture culture)
        {
            var setup = Setup(); setup.attacker.doctrine = "culture";
            var primary = setup.attacker.sections[0]; primary.eliteRole = BattleEliteRole.Conductor; primary.composure = 20; setup.attacker.conductor = primary.leader;
            setup.attacker.sections.Add(Unit("Company", 0, false));
            var settings = new CombatSettings { doctrines = { new BattleDoctrineSpec { id = "culture", stance = BattleStance.Line, commandCulture = culture } },
                tuning = new CombatTuning { variance = 0, maxMeasures = 3, fatigueFrom = 100 } };
            var run = BattleResolver.Begin(setup, settings, 1); run.BeginMeasure(); run.Perform(); return run.Attacker.sections[1].composure;
        }
        Assert.Greater(Remaining(BattleCommandCulture.Decentralized), Remaining(BattleCommandCulture.Standard));
        Assert.Greater(Remaining(BattleCommandCulture.Disciplined), Remaining(BattleCommandCulture.Standard));
    }
    private static CombatSection Unit(string name, int hex, bool elite = true) => new CombatSection { name = name, battleHex = hex,
        eliteRole = elite ? BattleEliteRole.Legend : BattleEliteRole.None, leader = elite ? new BattleLegend { name = name } : null,
        count = 1, maxIntegrity = 100, integrity = 100, maxComposure = 100, composure = 100, row = FormationRow.Front, speed = 0 };
    private static BattleSetup Setup(int seed = 10) => new BattleSetup { seed = seed, field = new Battlefield { age = 3 },
        attacker = new BattleSide { manual = true, takesCaptives = false, sections = { Unit("A", 7) } },
        defender = new BattleSide { manual = true, takesCaptives = false, sections = { Unit("D", 8) } } };
    private static void Hit(BattleSide side, string id, float damage) => side.deck.Add(new DeckCard { voice = 0,
        card = new CombatCard { id = id, name = id, effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, damage) { range = 16 } } } });
    private static BattleResolver.BattleRun Begin(BattleSetup setup, BattleSurvivalTuning survival = null)
    {
        var run = BattleResolver.Begin(setup, new CombatSettings { survival = survival ?? new BattleSurvivalTuning(),
            tuning = new CombatTuning { variance = 0, fatigueFrom = 100, maxMeasures = 3 },
            symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 30 } } }, 1); run.BeginMeasure(); return run;
    }
    private static int Index(BattleResolver.BattleRun run, string id) => run.Hand(false).ToList().FindIndex(c => c.card.id == id);

    [Test]
    public void ZeroIntegrityEntersDeathKnellWithoutAnImmediateDeathblow()
    {
        var setup = Setup(); Hit(setup.defender, "kill", 150); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(false, Index(run, "kill"), 0, beat: 1)); run.Perform();
        var unit = run.Attacker.sections[0]; Assert.IsTrue(unit.deathKnell); Assert.IsTrue(unit.Standing); Assert.IsFalse(unit.destroyed);
        Assert.AreEqual(0, unit.integrity); Assert.AreEqual(1, unit.Alive); Assert.AreEqual(1, run.PaceOf(true, 0)); Assert.AreEqual(0, unit.deathblowChecks);
        Assert.AreEqual(1, run.Report.survival.Count(e => e.cause == BattleSurvivalCause.DeathKnell));
    }

    [Test]
    public void SubsequentEventsCanKillPermanentlyAndProduceNamedFate()
    {
        BattleResolver.BattleRun doomed = null;
        for (int seed = 1; seed <= 30; seed++)
        {
            var setup = Setup(seed); setup.attacker.sections[0].integrity = 0; Hit(setup.defender, "hit", 1);
            var run = Begin(setup, new BattleSurvivalTuning { failureBase = .99f, maximumFailure = .99f });
            Assert.IsNull(run.CommitCard(false, Index(run, "hit"), 0, beat: 1)); run.Perform();
            if (run.Attacker.sections[0].permanentDeath) { doomed = run; break; }
        }
        Assert.IsNotNull(doomed); Assert.AreEqual(1, doomed.Attacker.sections[0].deathblowChecks); Assert.IsFalse(doomed.Attacker.sections[0].Standing);
        var fate = doomed.Finish().legends.Single(f => f.name == "A"); Assert.IsTrue(fate.dead); Assert.IsFalse(fate.missing); Assert.IsFalse(fate.captured);
    }

    [Test]
    public void RepeatedChecksAndWoundsRaiseDanger_PietyAndSupportNeverGuaranteeSurvival()
    {
        var tune = new BattleSurvivalTuning();
        float first = BattleSurvivalLogic.FailureChance(tune, 0, 0, 0, 0, false, false);
        Assert.Greater(BattleSurvivalLogic.FailureChance(tune, 1, 0, 0, 0, false, false), first);
        Assert.Greater(BattleSurvivalLogic.FailureChance(tune, 0, 1, 0, 0, false, false), first);
        float grace = BattleSurvivalLogic.FailureChance(tune, 0, 0, 100, 1, true, true); Assert.Less(grace, first); Assert.Greater(grace, 0);
        Assert.Less(BattleSurvivalLogic.FailureChance(tune, 100, 1, 0, 0, false, false), 1);
    }

    [Test]
    public void AHealthyBodyCanBeCapturedAfterItsResistanceFails()
    {
        var setup = Setup(); setup.attacker.sections[0].composure = 0; setup.defender.takesCaptives = true; var run = Begin(setup); run.Perform();
        Assert.IsTrue(run.Attacker.sections[0].captured); Assert.AreEqual(100, run.Attacker.sections[0].integrity);
        var fate = run.Finish().legends.Single(f => f.name == "A"); Assert.IsTrue(fate.captured); Assert.IsFalse(fate.missing); Assert.IsFalse(fate.dead);
    }

    [Test]
    public void SpiralingAloneDoesNotInstantlyCaptureThePlayersAgency()
    {
        var setup = Setup(); setup.attacker.sections[0].composure = 20; setup.defender.takesCaptives = true; var run = Begin(setup); run.Perform();
        Assert.IsFalse(run.Attacker.sections[0].captured); Assert.IsTrue(run.Hand(true).Any(c => c.card.corrupted));
    }

    [Test]
    public void CheckmateRecordsStrategicDangerWithoutDeletingTheElite()
    {
        var setup = Setup(); foreach (int hex in BattleHexLayout.Adjacent(7).Where(h => h != 8)) setup.field.hexes.Add(new BattleHexTerrain { hex = hex, blocked = true });
        var run = Begin(setup); run.Perform();
        Assert.IsTrue(run.Attacker.sections[0].eliteCheckmate); Assert.IsTrue(run.Attacker.sections[0].Standing); Assert.IsFalse(run.Attacker.sections[0].destroyed);
        Assert.IsTrue(run.Report.survival.Any(e => e.cause == BattleSurvivalCause.Checkmate)); Assert.IsNotNull(run.Evacuate(true, 0));
    }

    [Test]
    public void EvacuationCreatesMissingFateAndCancelsFuturePerformance()
    {
        var setup = Setup(); var run = Begin(setup); Assert.IsNull(run.Evacuate(true, 0));
        Assert.IsFalse(run.Attacker.sections[0].Standing); Assert.IsTrue(run.Attacker.sections[0].evacuated);
        var fate = run.Finish().legends.Single(f => f.name == "A"); Assert.IsTrue(fate.missing); Assert.IsFalse(fate.dead);
    }

    [Test]
    public void FormationPopulationsReconcileAllSixFatesExactly()
    {
        var unit = Unit("Company", 7, false); unit.count = 20000; unit.integrity = 20; unit.fled = true;
        var fate = BattleSurvivalLogic.Population(unit, 100, true, .6f); Assert.AreEqual(20000, fate.Accounted);
        Assert.Greater(fate.dead, 0); Assert.Greater(fate.wounded, 0); Assert.Greater(fate.recoverable, 0); Assert.Greater(fate.missing, 0);
        unit.fled = false; unit.captured = true; var taken = BattleSurvivalLogic.Population(unit, 100, true, .6f);
        Assert.AreEqual(20000, taken.Accounted); Assert.Greater(taken.captured, 0); Assert.AreEqual(0, taken.missing);
    }

    [Test]
    public void DeathKnellRejectsLongChannelsAndRushButKeepsQuickCards()
    {
        var setup = Setup(); setup.attacker.sections[0].integrity = 0;
        foreach (int length in new[] { 1, 3 }) setup.attacker.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "c" + length, channel = length } });
        var run = Begin(setup); int quick = run.Hand(true).ToList().FindIndex(c => c.card.id == "c1"); int slow = run.Hand(true).ToList().FindIndex(c => c.card.id == "c3");
        Assert.IsNull(run.WhyNotPlay(true, quick)); Assert.IsNotNull(run.WhyNotPlay(true, slow));
    }
}
