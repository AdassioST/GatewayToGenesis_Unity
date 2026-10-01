using System;
using System.Linq;
using NUnit.Framework;

public class BattlePollutionTests
{
    private static BattleSetup Setup() => new BattleSetup { seed = 5, field = new Battlefield { age = 3 },
        attacker = new BattleSide { manual = true, sections = { new CombatSection { name = "A", count = 1, battleHex = 7, eliteRole = BattleEliteRole.Legend,
            maxIntegrity = 100, integrity = 100, maxComposure = 100, composure = 40, row = FormationRow.Front, speed = 0, leader = new BattleLegend { name = "A" } } } },
        defender = new BattleSide { manual = true, sections = { new CombatSection { name = "D", count = 1, battleHex = 8, maxIntegrity = 500, integrity = 500,
            maxComposure = 100, composure = 100, row = FormationRow.Front, speed = 0 } } } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup) => BattleResolver.Begin(setup, new CombatSettings { tuning = new CombatTuning { variance = 0, maxMeasures = 3, fatigueFrom = 100 },
        symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 30 } } }, 1);

    [Test]
    public void AllNineStatusesOccupyRealDrawSlotsAndPurgeRestoresThePile()
    {
        var setup = Setup(); setup.attacker.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "purge", purpose = SpellPurpose.Utility, effects = { new CardEffect(CardOp.Purge, CardAim.Self, 1) } } });
        var run = Begin(setup); foreach (BattlePollution status in Enum.GetValues(typeof(BattlePollution))) if (status != BattlePollution.None) Assert.IsNull(run.Pollute(true, 0, status));
        Assert.AreEqual(10, run.DrawPile(true).Count); run.BeginMeasure(); Assert.AreEqual(10, run.Hand(true).Count);
        Assert.AreEqual(9, run.Hand(true).Count(c => c.card.pollution != BattlePollution.None)); Assert.IsNotNull(run.WhyNotPlay(true, run.Hand(true).ToList().FindIndex(c => c.card.pollution != BattlePollution.None)));
        Assert.AreEqual(95, run.Attacker.sections[0].integrity); Assert.AreEqual(35, run.Attacker.sections[0].composure);
        int purge = run.Hand(true).ToList().FindIndex(c => c.card.id == "purge"); Assert.IsNull(run.CommitCard(true, purge, beat: 1)); run.Perform();
        Assert.IsEmpty(run.Hand(true)); Assert.IsEmpty(run.DrawPile(true)); Assert.IsFalse(run.DiscardPile(true).Any(c => c.card.pollution != BattlePollution.None));
    }

    [Test]
    public void FieldAlterationAddsFiftyPercentBelowHalfAndLeavesAgonyInDiscard()
    {
        var setup = Setup(); var card = new CombatCard { id = "hit", effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 20) { range = 16 } } };
        setup.attacker.sections[0].leader.deckEvolution.alterations.Add(new BattleDeckAlteration { card = "hit" });
        setup.attacker.deck.Add(new DeckCard { voice = 0, legend = setup.attacker.sections[0].leader, card = card });
        var run = Begin(setup); run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, 0, 0, beat: 1)); run.Perform();
        Assert.AreEqual(470, run.Defender.sections[0].integrity); Assert.IsTrue(run.DiscardPile(true).Any(c => c.card.pollution == BattlePollution.Agony));
        Assert.AreEqual(20, card.effects[0].amount, "persistent card authoring is not mutated");
    }

    [Test]
    public void FieldRestKeepsTrauma_HomeChoiceIsAtomicAndLimitedToTwo()
    {
        var deck = new BattleDeckEvolution { pollution = { BattlePollution.Burn }, alterations = {
            new BattleDeckAlteration { card = "a" }, new BattleDeckAlteration { card = "b" }, new BattleDeckAlteration { card = "c" }, new BattleDeckAlteration { card = "trauma", trauma = true } } };
        deck.RestInField(); Assert.AreEqual(4, deck.alterations.Count);
        Assert.IsNotNull(deck.IntegrateHome(new[] { "a", "b", "c" })); Assert.AreEqual(4, deck.alterations.Count);
        Assert.IsNotNull(deck.IntegrateHome(new[] { "trauma" })); Assert.IsNull(deck.IntegrateHome(new[] { "a", "b" }));
        Assert.IsEmpty(deck.pollution); Assert.AreEqual(2, deck.alterations.Count); Assert.IsTrue(deck.alterations.All(a => a.opus && !a.trauma));
    }

    [Test]
    public void PreparedChargesRequireTechnologyCeremonyAndPayment_AndHaveThreeNormalSlots()
    {
        var state = new BattlePremonitions(); int paid = 0;
        Assert.IsNotNull(state.Prepare(BattlePremonitionKind.Divination, false, true, () => { paid++; return true; })); Assert.AreEqual(0, paid);
        Assert.IsNotNull(state.Prepare(BattlePremonitionKind.Divination, true, true, () => false));
        Assert.IsNull(state.Prepare(BattlePremonitionKind.Divination, true, true, () => true)); Assert.IsNull(state.Prepare(BattlePremonitionKind.Divination, true, true, () => true));
        Assert.IsNotNull(state.Prepare(BattlePremonitionKind.Divination, true, true, () => true));
        Assert.IsNull(state.Prepare(BattlePremonitionKind.Prophetical, true, true, () => true)); Assert.AreEqual(3, state.Remaining);
        state.BeginEncounter("fight", false, true, true); for (int i = 0; i < 3; i++) Assert.IsTrue(state.ConsumeLoss("fight"));
        Assert.IsTrue(state.exhaustedAchievement); Assert.IsFalse(state.ConsumeLoss("fight")); Assert.AreEqual(3, state.attemptsLost);
    }

    [Test]
    public void BossRechargeUsesOnlyUnlockedRegistersAndNeverRechargesOnRetry()
    {
        var state = new BattlePremonitions { advancedBossDivination = 1 }; state.BeginEncounter("boss", true, true, false);
        Assert.AreEqual(3, state.divination); Assert.AreEqual(0, state.prophetical);
        Assert.IsTrue(state.ConsumeLoss("boss")); state.BeginEncounter("boss", true, true, true); Assert.AreEqual(2, state.Remaining);
        state.BeginEncounter("next-boss", true, true, true); Assert.AreEqual(4, state.Remaining);
    }

    private sealed class Saved { public BattleDeckEvolution deck = new BattleDeckEvolution(); public BattlePremonitions retry = new BattlePremonitions(); }
    [Test]
    public void StatusFuelBuildBenefitsFromBurnWhileStillPayingItsDrawPenalty()
    {
        var setup = Setup(); setup.attacker.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "fuel", purpose = SpellPurpose.Offensive,
            effects = { new CardEffect(CardOp.StatusFuel, CardAim.Self, .5f) { pollution = BattlePollution.Burn }, new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 20) { range = 16 } } } });
        var run = Begin(setup); run.Pollute(true, 0, BattlePollution.Burn); run.BeginMeasure();
        Assert.AreEqual(97, run.Attacker.sections[0].integrity); Assert.IsNull(run.CommitCard(true, run.Hand(true).ToList().FindIndex(c => c.card.id == "fuel"), 0, beat: 1));
        run.Perform(); Assert.AreEqual(470, run.Defender.sections[0].integrity);
    }
    [Test]
    public void HomeIntegrationRetainsPreviouslyLearnedOpus()
    {
        var deck = new BattleDeckEvolution { alterations = { new BattleDeckAlteration { card = "old", opus = true }, new BattleDeckAlteration { card = "new" }, new BattleDeckAlteration { card = "trauma", trauma = true } } };
        Assert.IsNull(deck.IntegrateHome(new[] { "new" })); CollectionAssert.AreEquivalent(new[] { "old", "new" }, deck.alterations.Select(a => a.card)); Assert.IsTrue(deck.alterations.All(a => a.opus));
    }
    [Test]
    public void SaveCodecPreservesPollutionAlterationsOpusAndPreparedAttemptBoundaries()
    {
        var source = new Saved(); source.deck.pollution.Add(BattlePollution.Agony); source.deck.alterations.Add(new BattleDeckAlteration { card = "hit", opus = true });
        source.retry.BeginEncounter("boss", true, true, true); source.retry.ConsumeLoss("boss");
        var snapshot = SaveStateCodec.Capture(source, "deck", "retry"); var restored = new Saved(); SaveStateCodec.Restore(restored, snapshot, "deck", "retry");
        Assert.AreEqual(BattlePollution.Agony, restored.deck.pollution.Single()); Assert.IsTrue(restored.deck.alterations.Single().opus);
        Assert.AreEqual(2, restored.retry.Remaining); Assert.AreEqual("boss", restored.retry.encounter); Assert.AreEqual(1, restored.retry.attemptsLost);
    }
}
