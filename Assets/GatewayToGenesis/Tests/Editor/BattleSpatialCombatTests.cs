using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleSpatialCombatTests
{
    private static CombatSection Unit(string name, int hex, FormationRow row = FormationRow.Front, float attack = 0f) =>
        new CombatSection { name = name, battleHex = hex, row = row, count = 10, maxIntegrity = 100f, integrity = 100f,
            maxComposure = 100f, composure = 100f, attack = attack, defense = 10f, breakthrough = 10f, dread = 1f };

    private static CombatSettings Settings(int measures = 1) => new CombatSettings
    { tuning = new CombatTuning { maxMeasures = measures, variance = 0f, fatigueFrom = 100, conductorExposure = 0f } };

    private static BattleSetup Setup(params CombatSection[] attackers) => new BattleSetup
    { seed = 42, attacker = new BattleSide { name = "A", manual = true, sections = attackers.ToList() },
        defender = new BattleSide { name = "D", manual = true, sections = new List<CombatSection> { Unit("Enemy", 8) } }, field = new Battlefield() };

    private static BattleResolver.BattleRun Begin(BattleSetup setup, CombatSettings settings = null) =>
        BattleResolver.Begin(setup, settings ?? Settings(), forecastRuns: 1);

    private static CombatCard Card(CardOp op, CardAim aim, float amount = 1f) => new CombatCard
    { id = "spatial-test", name = "Spatial test", effects = new List<CardEffect> { new CardEffect(op, aim, amount) } };

    private static void Deck(BattleSide side, CombatCard card, int voice = 0) => side.deck.Add(new DeckCard { card = card, voice = voice });

    [Test]
    public void DrilledMeleeCannotHitAcrossTheBoard_ButRangedCanReachThreeHexes()
    {
        foreach (bool ranged in new[] { false, true })
        {
            var setup = Setup(Unit("Shooter", 6, ranged ? FormationRow.Back : FormationRow.Front, 20f));
            setup.defender.sections[0].battleHex = 9;
            var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.Order(true, 0, BattleStandingOrder.Hold)); run.ResolveMeasure();
            Assert.AreEqual(ranged, setup.defender.sections[0].integrity < 100f, "held in place, only the bow reaches three hexes");
        }
    }

    [Test]
    public void ReachUsesPhysicalBlockers_AndHighGroundExtendsRangedReach()
    {
        var setup = Setup(Unit("Archer", 5, FormationRow.Back));
        setup.defender.sections[0].battleHex = 9;
        var run = Begin(setup);
        Assert.IsEmpty(run.SpatialRules.Targets(true, setup.attacker.sections[0], true, run.SpatialRules.Reach(setup.attacker.sections[0])));
        run.Spatial.Terrain[5].highGround = true;
        Assert.AreEqual(1, run.SpatialRules.Targets(true, setup.attacker.sections[0], true, run.SpatialRules.Reach(setup.attacker.sections[0])).Count);
        foreach (int h in new[] { 2, 7, 13 }) run.Spatial.Terrain[h].blocked = true;
        Assert.AreEqual(int.MaxValue, run.SpatialRules.Distance(5, 9));
    }

    [TestCase(BattleProximity.Nearest, "Near")]
    [TestCase(BattleProximity.Furthest, "Far")]
    [TestCase(BattleProximity.SameLane, "Near,Far")]
    [TestCase(BattleProximity.ForwardMost, "Near")]
    [TestCase(BattleProximity.RearMost, "Far")]
    public void RankProximityFiltersTheReachablePool(BattleProximity rule, string expected)
    {
        var setup = Setup(Unit("Actor", 6));
        setup.defender.sections = new List<CombatSection> { Unit("Near", 7), Unit("Wing", 3), Unit("Far", 10) };
        var run = Begin(setup);
        CollectionAssert.AreEquivalent(expected.Split(','), run.SpatialRules.Targets(true, setup.attacker.sections[0], true, 8, rule).Select(x => x.name));
    }

    [Test]
    public void AdjacentAllyAndNeutralContactUseHexEdges()
    {
        var setup = Setup(Unit("Actor", 6), Unit("Adjacent", 1), Unit("Remote", 15));
        setup.defender.sections = new List<CombatSection> { Unit("Touching", 9), Unit("Rear", 10) };
        var run = Begin(setup);
        CollectionAssert.AreEqual(new[] { "Adjacent" }, run.SpatialRules.Targets(true, setup.attacker.sections[0], false, 8, BattleProximity.AdjacentAlly).Select(x => x.name));
        CollectionAssert.AreEqual(new[] { "Touching" }, run.SpatialRules.Targets(true, setup.attacker.sections[0], true, 8, BattleProximity.NeutralContact).Select(x => x.name));
    }

    [Test]
    public void FriendlyStackingPenalizesBothAttackAndDefense_AndBothOccupants()
    {
        var setup = Setup(Unit("First", 7), Unit("Second", 6)); var run = Begin(setup);
        float attack = run.SpatialRules.AttackFactor(true, setup.attacker.sections[0]);
        float defense = run.SpatialRules.DefenseFactor(true, setup.attacker.sections[0]);
        setup.attacker.sections[1].battleHex = 7;
        foreach (var unit in setup.attacker.sections)
        {
            Assert.Less(run.SpatialRules.AttackFactor(true, unit), attack * .5f);
            Assert.Less(run.SpatialRules.DefenseFactor(true, unit), defense * .5f);
        }
    }

    [Test]
    public void MovementUsesPassableEdges_AnEngagedUnitIsPinned_AndTheScoreLocksAtCommitment()
    {
        var setup = Setup(Unit("Actor", 7)); var run = Begin(setup); run.BeginMeasure();
        Assert.IsNotNull(run.CommitMovement(true, 0, 10), "a Step crosses one shared edge");
        run.Spatial.Terrain[6].blocked = true;
        Assert.IsNotNull(run.CommitMovement(true, 0, 6), "blocked ground");
        Assert.IsNull(run.CommitMovement(true, 0, 12)); Assert.IsNull(run.CommitMovement(true, 0, 1), "held time: a path is rewritten freely until the Score is committed");
        Assert.IsNull(run.Perform()); Assert.IsNotNull(run.CommitMovement(true, 0, 2), "a committed Score is a promise");
        var engaged = Setup(Unit("Actor", 7)); engaged.defender.sections[0].battleHex = 7; var pinned = Begin(engaged); pinned.BeginMeasure();
        StringAssert.Contains("Withdraw", pinned.CommitMovement(true, 0, 12), "engaged units cannot Step away at all");
    }

    [Test]
    public void ForcedRetreatIntoAnAllyCreatesStacking_AndWeakensBoth()
    {
        var setup = Setup(Unit("Victim", 7), Unit("Fallback", 6)); setup.defender.sections[0].battleHex = 7;
        setup.field.hexes.Add(new BattleHexTerrain { hex = 1, blocked = true });
        setup.field.hexes.Add(new BattleHexTerrain { hex = 12, blocked = true });
        Deck(setup.defender, Card(CardOp.Push, CardAim.Enemy));
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.AreEqual(6, setup.attacker.sections[0].battleHex);
        Assert.IsTrue(run.Spatial.IsStacked(6, true));
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.Stacking));
        Assert.IsTrue(setup.attacker.sections.All(x => run.SpatialRules.DefenseFactor(true, x) < .5f));
    }

    [Test]
    public void IntegritySkirmishLoserFallsBackExactlyOneHex()
    {
        var setup = Setup(Unit("Weaker", 7, attack: 5f)); setup.defender.sections[0].battleHex = 7; setup.defender.sections[0].attack = 20f;
        var run = Begin(setup); run.ResolveMeasure();
        Assert.IsTrue(BattleHexLayout.AreAdjacent(7, setup.attacker.sections[0].battleHex));
        Assert.Less(BattleHexLayout.At(setup.attacker.sections[0].battleHex).X, BattleHexLayout.At(7).X);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.SkirmishLost && e.attacker));
        Assert.AreEqual(7, setup.defender.sections[0].battleHex);
    }

    [Test]
    public void MereAdjacencyDoesNotCreateAnIntegritySkirmish()
    {
        var setup = Setup(Unit("Actor", 7, attack: 5f)); setup.defender.sections[0].attack = 20f;
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.Order(true, 0, BattleStandingOrder.Hold)); Assert.IsNull(run.Order(false, 0, BattleStandingOrder.Hold));
        run.ResolveMeasure();
        Assert.IsFalse(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.SkirmishLost));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NoBackwardExitRoutesWithoutTeleporting(bool rear)
    {
        var victim = Unit("Victim", rear ? 5 : 7); var setup = Setup(victim);
        setup.defender.sections[0].battleHex = rear ? 6 : 8;
        if (!rear) foreach (int h in new[] { 1, 6, 12 }) setup.field.hexes.Add(new BattleHexTerrain { hex = h, blocked = true });
        Deck(setup.defender, Card(CardOp.Push, CardAim.Enemy));
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.IsTrue(victim.fled); Assert.AreEqual(rear ? 5 : 7, victim.battleHex);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.Rout));
    }

    [Test]
    public void EncirclementUsesEveryPhysicalExit_AndCanSubjugateAtFullIntegrity()
    {
        var victim = Unit("Victim", 7); var setup = Setup(victim); setup.defender.takesCaptives = true;
        foreach (int h in BattleHexLayout.Adjacent(7).Where(h => h != 8)) setup.field.hexes.Add(new BattleHexTerrain { hex = h, blocked = true });
        Deck(setup.defender, Card(CardOp.Push, CardAim.Enemy));
        var run = Begin(setup); Assert.IsTrue(run.SpatialRules.Surrounded(true, victim));
        run.BeginMeasure(); Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.IsTrue(victim.captured); Assert.AreEqual(100f, victim.integrity); Assert.AreEqual(7, victim.battleHex);
        Assert.AreEqual(1, run.Report.captives.Count);
    }

    [Test]
    public void MovingTheHealerChangesCardAccess_WithoutConsumingTheCardOnRejection()
    {
        var setup = Setup(Unit("Patient", 7), Unit("Healer", 6, FormationRow.Support)); setup.attacker.sections[0].integrity = 50f;
        Deck(setup.attacker, Card(CardOp.Mend, CardAim.Ally, .2f), 1);
        var run = Begin(setup); run.BeginMeasure(); Assert.Contains(setup.attacker.sections[0], run.LegalTargets(true, 0).ToList());
        Assert.IsNull(run.CommitMovement(true, 1, 5));
        Assert.IsFalse(run.LegalTargets(true, 0).Contains(setup.attacker.sections[0]));
        Assert.IsNotNull(run.CommitCard(true, 0, 0)); Assert.AreEqual(1, run.Hand(true).Count);
    }

    [Test]
    public void PassiveMendingAlsoRequiresTheProvidersCurrentAdjacency()
    {
        float Damage(bool move)
        {
            var setup = Setup(Unit("Patient", 7), Unit("Healer", 6, FormationRow.Support)); setup.attacker.sections[1].mending = .5f;
            setup.defender.sections[0].attack = 20f;
            var run = Begin(setup); run.BeginMeasure(); if (move) Assert.IsNull(run.CommitMovement(true, 1, 5)); run.ResolveMeasure();
            return setup.attacker.sections[0].integrity;
        }
        Assert.Greater(Damage(false), Damage(true));
    }

    [Test]
    public void DisplacedCommittedTargetFails_InsteadOfRetargetingOrRefunding()
    {
        var setup = Setup(Unit("Actor", 7)); Deck(setup.attacker, Card(CardOp.Strike, CardAim.Enemy));
        setup.attacker.deck[0].card.windUp = 2;
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0, 0));
        Assert.IsNull(run.CommitMovement(false, 0, 9)); run.ResolveMeasure();
        Assert.IsTrue(run.Report.plays.Single().failed); Assert.IsEmpty(run.Hand(true), "failed commitment is not refunded");
        Assert.AreEqual(100f, setup.defender.sections[0].integrity);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.PreparationFailed));
    }

    [Test]
    public void OneOfficerFallCascadesThroughPanicRetreatStackAndStanceBreak()
    {
        var officer = Unit("Officer", 6, FormationRow.Support); officer.eliteRole = BattleEliteRole.Legend; officer.integrity = 1f;
        var scared = Unit("Scared", 7); scared.composure = 15f;
        var setup = Setup(officer, scared, Unit("Fallback", 6)); setup.attacker.stanceStability = 28f;
        foreach (int h in new[] { 1, 12 }) setup.field.hexes.Add(new BattleHexTerrain { hex = h, blocked = true });
        setup.defender.sections[0].battleHex = 7;
        var kill = Card(CardOp.Strike, CardAim.Enemy, 100f); kill.effects[0].ignoreGuard = true; Deck(setup.defender, kill);
        var run = Begin(setup); run.BeginMeasure(); for (int i = 0; i < 3; i++) Assert.IsNull(run.Order(true, i, BattleStandingOrder.Hold));
        Assert.IsNull(run.CommitCard(false, 0, 0)); run.ResolveMeasure();
        Assert.IsTrue(officer.deathKnell); Assert.IsFalse(officer.destroyed); Assert.IsTrue(scared.mindBroken); Assert.AreEqual(6, scared.battleHex);
        Assert.IsTrue(run.Spatial.IsStacked(6, true)); Assert.IsTrue(setup.attacker.StanceBroken);
        foreach (var cause in new[] { BattleSpatialCause.EliteCasualty, BattleSpatialCause.MindBreak, BattleSpatialCause.ForcedRetreat, BattleSpatialCause.Stacking, BattleSpatialCause.StanceBreak })
            Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == cause), cause.ToString());
        Assert.Less(setup.defender.sections[0].composure, 100f, "the document says the Stance Break shock crosses the entire battlefield");
    }

    [Test]
    public void DoctrineBonusesRequireShapeAndCohesion_AndNeverAutowin()
    {
        var setup = Setup(Unit("Spear", 6)); setup.attacker.stance = BattleStance.Spearhead;
        var run = Begin(setup); var actor = setup.attacker.sections[0];
        Assert.IsTrue(run.SpatialRules.DoctrineHeld(true)); float bonus = run.SpatialRules.AttackFactor(true, actor);
        actor.battleHex = 1; Assert.IsFalse(run.SpatialRules.DoctrineHeld(true)); Assert.Less(run.SpatialRules.AttackFactor(true, actor), bonus);
        actor.battleHex = 6; actor.mindBroken = true; Assert.IsFalse(run.SpatialRules.DoctrineHeld(true));
        actor.mindBroken = false; setup.attacker.stanceStability = 0f; Assert.IsFalse(run.SpatialRules.DoctrineHeld(true));
    }

    [Test]
    public void StanceBreakExposesRearRanks_AndLeavesNonmagicalCardAgencyAvailable()
    {
        var setup = Setup(Unit("Actor", 7)); setup.defender.sections = new List<CombatSection> { Unit("Guard", 8), Unit("Rear", 9, FormationRow.Back) };
        Deck(setup.attacker, Card(CardOp.Guard, CardAim.Self)); var run = Begin(setup); run.BeginMeasure();
        Assert.IsFalse(run.SpatialRules.Targets(true, setup.attacker.sections[0], true, 4).Contains(setup.defender.sections[1]));
        setup.defender.stanceStability = 0f;
        Assert.Contains(setup.defender.sections[1], run.SpatialRules.Targets(true, setup.attacker.sections[0], true, 4));
        setup.attacker.stanceStability = 0f; Assert.IsNull(run.WhyNotPlay(true, 0)); Assert.IsNull(run.CommitCard(true, 0));
    }

    [Test]
    public void SeededAutoAndTheSamePerformedManualTraceHaveIdenticalSpatialEvents()
    {
        var source = Setup(Unit("First", 6, attack: 20f), Unit("Second", 1, attack: 15f)); source.defender.sections[0].battleHex = 9; source.defender.sections[0].attack = 20f;
        source.attacker.manual = source.defender.manual = false;
        Deck(source.attacker, Card(CardOp.Strike, CardAim.Enemy)); Deck(source.defender, Card(CardOp.Strike, CardAim.Enemy));
        var automatic = source.Clone(42); var played = source.Clone(42);
        var expected = BattleResolver.Resolve(automatic, Settings(4));
        var oracle = Begin(source.Clone(42), Settings(4)); played.attacker.manual = played.defender.manual = true;
        var run = Begin(played, Settings(4));
        while (!run.Over)
        {
            oracle.BeginMeasure(); run.BeginMeasure();
            foreach (bool attacker in new[] { true, false })
                foreach (var path in oracle.MovementIntent(attacker).GroupBy(x => x.section))
                    Assert.IsNull(run.ComposePath(attacker, path.Key, path.Select(x => x.to).ToList(), path.Select(x => BattleMeasureMath.Rel(x.beat)).ToList()));
            foreach (bool attacker in new[] { true, false })
                foreach (var intent in oracle.Intent(attacker))
                {
                    int hand = run.Hand(attacker).ToList().FindIndex(c => c.card.id == intent.card);
                    int target = (attacker ? run.Defender : run.Attacker).sections.FindIndex(x => x.name == intent.target);
                    Assert.IsNull(run.CommitCard(attacker, hand, target, intent.rendition, BattleMeasureMath.Rel(intent.beat)));
                }
            oracle.ResolveMeasure(); run.ResolveMeasure();
        }
        var actual = run.Finish();
        CollectionAssert.AreEqual(expected.spatialEvents.Select(e => $"{e.measure}:{e.attacker}:{e.section}:{e.cause}:{e.from}:{e.to}:{e.stabilityDamage}"),
            actual.spatialEvents.Select(e => $"{e.measure}:{e.attacker}:{e.section}:{e.cause}:{e.from}:{e.to}:{e.stabilityDamage}"));
        CollectionAssert.AreEqual(automatic.attacker.sections.Select(x => x.integrity), played.attacker.sections.Select(x => x.integrity));
        Assert.AreEqual(actual.measures + 1, actual.timeline.Count); Assert.AreEqual(3, actual.timeline[0].positions.Count);
    }

    [Test]
    public void AreaRankRuleHitsEveryEligibleContact_AndLeavesRearHexesAlone()
    {
        var setup = Setup(Unit("Actor", 6)); setup.defender.sections = new List<CombatSection> { Unit("Contact", 9), Unit("Wing", 3), Unit("Rear", 10) };
        var card = Card(CardOp.Dread, CardAim.EnemyLine, 10f); card.effects[0].proximity = BattleProximity.NeutralContact;
        Deck(setup.attacker, card); var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0)); run.ResolveMeasure();
        Assert.Less(setup.defender.sections[0].composure, 100f); Assert.Less(setup.defender.sections[1].composure, 100f);
        Assert.AreEqual(100f, setup.defender.sections[2].composure);
    }

    [Test]
    public void FriendlyFireUsesActualAlliedGeometry_AndDamagesStability()
    {
        var setup = Setup(Unit("Actor", 7, attack: 20f), Unit("Ally", 6));
        Deck(setup.attacker, Card(CardOp.Strike, CardAim.Ally)); var run = Begin(setup); run.BeginMeasure();
        Assert.IsNull(run.CommitCard(true, 0, 1)); run.ResolveMeasure();
        Assert.Less(setup.attacker.sections[1].integrity, 100f);
        Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.FriendlyFire && e.attacker && e.stabilityDamage > 0f));
    }

    [Test]
    public void LostGuardAdjacencyRemovesTheActualParryBonus()
    {
        float Remaining(bool push)
        {
            var setup = Setup(Unit("Patient", 7), Unit("Provider", 6, FormationRow.Support)); setup.defender.sections[0].attack = 20f;
            // One Major per Track: a second enemy Track pushes the provider while the first strikes.
            setup.defender.sections.Add(Unit("Pusher", 9));
            Deck(setup.attacker, Card(CardOp.Guard, CardAim.Ally, 10f), 1);
            var card = Card(CardOp.Push, CardAim.Enemy); card.effects[0].ignoreGuard = true; card.effects[0].range = 3; Deck(setup.defender, card, 1);
            var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0, 0, beat: 1)); if (push) Assert.IsNull(run.CommitCard(false, 0, 1, beat: 2));
            run.ResolveMeasure();
            if (push) Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.GuardLost));
            return setup.attacker.sections[0].integrity;
        }
        Assert.Greater(Remaining(false), Remaining(true));
    }

    [Test]
    public void PreparedPositionCannotFollowItsVoiceAfterCommitment()
    {
        var setup = Setup(Unit("Actor", 7)); var card = Card(CardOp.Guard, CardAim.Self); card.requiresPosition = true; card.windUp = 3; Deck(setup.attacker, card);
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0)); Assert.IsNull(run.CommitMovement(true, 0, 6)); run.ResolveMeasure();
        Assert.IsTrue(run.Report.plays.Single().failed); StringAssert.Contains("position", run.Report.plays.Single().failure);
    }

    [Test]
    public void AreaSpellReachesAllContacts_AndPaysForOneWorking()
    {
        var caster = Unit("Caster", 6); caster.potency = 20f; caster.primary = SpellBinding.Flux;
        var setup = Setup(caster); setup.field.age = 2;
        setup.defender.sections = new List<CombatSection> { Unit("Contact", 9), Unit("Wing", 3), Unit("Rear", 10, FormationRow.Back) };
        var card = Card(CardOp.Spell, CardAim.EnemyLine); card.kind = CardKind.Spell; card.binding = SpellBinding.Flux; card.flicker = 0f;
        card.effects[0].proximity = BattleProximity.NeutralContact; Deck(setup.attacker, card);
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0));
        run.ResolveMeasure();
        Assert.AreEqual(1, caster.casts, "the area card is one working: one Essence payment, and the Track has no other Major");
        Assert.Less(setup.defender.sections[0].integrity, 100f); Assert.Less(setup.defender.sections[1].integrity, 100f);
        Assert.AreEqual(100f, setup.defender.sections[2].integrity);
    }

    [Test]
    public void AllThreeDoctrineCountersLoseTheirMatchupBonusWhenEnemyShapeFails()
    {
        foreach (var stance in new[] { BattleStance.Spearhead, BattleStance.Line, BattleStance.Crescent })
        {
            var countered = stance == BattleStance.Spearhead ? BattleStance.Line : stance == BattleStance.Line ? BattleStance.Crescent : BattleStance.Spearhead;
            var setup = Setup(stance == BattleStance.Crescent ? Unit("Actor", 1) : Unit("Actor", 6)); setup.attacker.stance = stance; setup.defender.stance = countered;
            if (stance == BattleStance.Crescent) setup.attacker.sections.Add(Unit("Other wing", 12));
            setup.defender.sections = countered == BattleStance.Crescent ? new List<CombatSection> { Unit("Wing", 3), Unit("Other wing", 14) } :
                new List<CombatSection> { Unit("Enemy", 9) };
            var run = Begin(setup); Assert.IsTrue(run.SpatialRules.DoctrineHeld(true)); Assert.IsTrue(run.SpatialRules.DoctrineHeld(false));
            float advantage = run.SpatialRules.AttackFactor(true, setup.attacker.sections[0]);
            setup.defender.sections[0].mindBroken = true;
            Assert.Less(run.SpatialRules.AttackFactor(true, setup.attacker.sections[0]), advantage);
        }
    }
}
