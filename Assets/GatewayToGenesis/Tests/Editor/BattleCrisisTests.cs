using System;
using System.Linq;
using NUnit.Framework;

public class BattleCrisisTests
{
    [Test]
    public void PersistentBattleAftermathStopsAtSpiralingUnlessParasiticPressureCrossesSurrender()
    {
        var tuning = new ComposureTuning(); float before = 95;
        float ordinary = ComposureRules.BattleStrain(before, 40, 0, tuning);
        Assert.AreEqual(ComposureState.Spiraling, ComposureRules.StateOf(before + ordinary, tuning));
        float parasitic = ComposureRules.BattleStrain(before, 40, 8, tuning);
        Assert.AreEqual(ComposureState.Surrender, ComposureRules.StateOf(before + parasitic, tuning));
        Assert.AreEqual(0, ComposureRules.BattleStrain(before + ordinary, 40, 0, tuning), .001, "repeated ordinary defeats cannot cross the boundary");
    }
    [Test]
    public void FullyAbjuredInstinctsCannotEarnTheCorrodedGambit()
    {
        var setup = Setup(); setup.defender.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "ward", purpose = SpellPurpose.Defensive,
            effects = { new CardEffect(CardOp.Guard, CardAim.Self, 1000) { flat = true } } } });
        var run = Begin(setup); Assert.IsNull(run.CommitCard(false, 0, beat: 1));
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 2));
        run.Perform(2); Assert.AreEqual(0, run.Crises(true).Single().resolvedThisMeasure);
        Assert.IsFalse(run.Crises(true).Single().cadenzaReady); Assert.IsTrue(run.Attacker.sections[0].mindBroken);
        Assert.AreEqual(500, run.Defender.sections[0].integrity);
    }
    private static CombatSection Unit(string name, int hex, float composure = 100) => new CombatSection { name = name, battleHex = hex, eliteRole = BattleEliteRole.Guard,
        count = 1, maxIntegrity = 500, integrity = 500, maxComposure = 100, composure = composure, row = FormationRow.Front, speed = 0 };
    private static BattleSetup Setup() => new BattleSetup { seed = 77, field = new Battlefield { age = 3 },
        attacker = new BattleSide { manual = true, sections = { Unit("A", 7, 20) } },
        defender = new BattleSide { manual = true, sections = { Unit("D", 8) } } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup = null)
    {
        setup = setup ?? Setup();
        var run = BattleResolver.Begin(setup, new CombatSettings { tuning = new CombatTuning { variance = 0, maxMeasures = 6, fatigueFrom = 100 },
            symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 30 } } }, 1); run.BeginMeasure(); return run;
    }
    private static int Instinct(BattleResolver.BattleRun run, int voice = 0) => run.Hand(true).ToList().FindIndex(c => c.voice == voice && c.card.corrupted);

    [Test]
    public void SpiralingSubstitutesTheHandWithoutTakingTheEliteOffTheField()
    {
        var setup = Setup(); setup.attacker.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "normal", name = "Normal" } });
        var run = Begin(setup);
        Assert.IsTrue(run.Attacker.sections[0].Standing); Assert.IsTrue(run.Attacker.sections[0].mindBroken);
        Assert.AreEqual(2, run.Hand(true).Count); Assert.IsTrue(run.Hand(true).All(c => c.card.corrupted));
        Assert.AreEqual(1, setup.attacker.deck.Count, "temporary compulsions do not pollute the persistent deck");
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1));
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 2), "compulsions bypass the Major limit");
    }

    [Test]
    public void QueuingDoesNotEarnTheGambit_ResolvingTwiceUnlocksImmediateCadenza()
    {
        var setup = Setup(); setup.attacker.sections[0].leader = new BattleLegend { name = "A", strain = 80 };
        var run = Begin(setup); float strain = run.Attacker.sections[0].leader.strain;
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 2));
        Assert.IsFalse(run.Crises(true).Single().cadenzaReady);
        run.Perform(); Assert.AreEqual(1, run.Crises(true).Single().resolvedThisMeasure); Assert.IsTrue(run.Attacker.sections[0].mindBroken);
        run.Perform(); Assert.IsFalse(run.Attacker.sections[0].mindBroken); Assert.IsTrue(run.Crises(true).Single().cadenzaReady);
        Assert.AreEqual(strain, run.Attacker.sections[0].leader.strain);
        int cadenza = run.Hand(true).ToList().FindIndex(c => c.card.cathartic);
        Assert.IsNull(run.CommitCrisisCard(true, cadenza, 3)); run.Perform();
        Assert.IsFalse(run.Crises(true).Single().cadenzaReady); Assert.AreEqual(1, run.Report.crises.Count(e => e.cause == BattleCrisisCause.CorrodedGambit));
    }

    [Test]
    public void CrossMeasureAndDifferentOwnersCannotPoolGambitResolutions()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("B", 6, 20)); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); Assert.IsNull(run.CommitCard(true, Instinct(run, 1), beat: 2));
        run.ResolveMeasure(); Assert.IsFalse(run.Report.crises.Any(e => e.cause == BattleCrisisCause.CorrodedGambit));
        run.BeginMeasure(); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); run.Perform();
        Assert.IsTrue(run.Attacker.sections[0].mindBroken); Assert.AreEqual(1, run.Crises(true).First(c => c.voice == 0).resolvedThisMeasure);
    }

    [Test]
    public void FailedPromisesDoNotCountAsCorruptedResolutions()
    {
        var run = Begin(); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1));
        run.Defender.sections[0].fled = true; run.Perform();
        Assert.AreEqual(0, run.Crises(true).Single().resolvedThisMeasure); Assert.IsTrue(run.Attacker.sections[0].mindBroken);
    }

    [Test]
    public void CrisisChoicesCannotRewritePastBeatsOrNormalCommittedCards()
    {
        var run = Begin(); run.Perform();
        Assert.IsNotNull(run.CommitCrisisCard(true, Instinct(run), 1));
        Assert.IsNull(run.CommitCrisisCard(true, Instinct(run), 2));
        Assert.IsNotNull(run.CommitCard(true, Instinct(run), beat: 3));
    }

    [Test]
    public void BreakThemUsesNearestRankOneAndDamagesOnlyTwoNearestAllies()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("B", 7)); setup.attacker.sections.Add(Unit("C", 7)); setup.attacker.sections.Add(Unit("Far", 0));
        setup.defender.sections[0].battleHex = 9; setup.defender.sections.Add(Unit("Rear", 10)); var run = Begin(setup);
        run.Order(true, 1, BattleStandingOrder.Hold); run.Order(true, 2, BattleStandingOrder.Hold); run.Order(true, 3, BattleStandingOrder.Hold);
        Assert.IsNull(run.CommitCard(true, Instinct(run), target: 1, beat: 1)); run.Perform();
        Assert.Less(run.Defender.sections[0].integrity, 500); Assert.AreEqual(500, run.Defender.sections[1].integrity);
        Assert.AreEqual(85, run.Attacker.sections[1].composure); Assert.AreEqual(85, run.Attacker.sections[2].composure);
        Assert.AreEqual(100, run.Attacker.sections[3].composure);
        Assert.AreNotEqual(7, run.Attacker.sections[1].battleHex); Assert.AreNotEqual(7, run.Attacker.sections[2].battleHex);
    }

    [Test]
    public void CadenzaRetargetsByGeometryAndCanStrikeFriendlyPositions()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Ally", 6)); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 2)); run.Perform(2);
        int index = run.Hand(true).ToList().FindIndex(c => c.card.cathartic); Assert.IsNull(run.CommitCrisisCard(true, index, 3));
        run.Defender.sections[0].fled = true; float before = run.Attacker.sections[1].integrity; run.Perform();
        Assert.Less(run.Attacker.sections[1].integrity, before); Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.FriendlyFire));
    }

    [Test]
    public void TraitAndWoundInputsChangeTheMaladaptiveCard()
    {
        var unit = Unit("A", 7); var careful = new BattleLegend { traits = { "Caution" } }; var confident = new BattleLegend { traits = { "Confidence" } };
        Assert.AreEqual("Paralysis", BattleCrisisLogic.Expression(careful, unit));
        float Harm(CombatCard card) => card.effects.Where(e => e.op == CardOp.IntegrityDamage).Sum(e => e.amount);
        Assert.Greater(Harm(BattleCrisisLogic.Instinct(confident, unit, 0)), Harm(BattleCrisisLogic.Instinct(new BattleLegend(), unit, 0)), "reckless certainty hits harder than raw instinct");
        float whole = Harm(BattleCrisisLogic.Instinct(confident, unit, 0));
        unit.integrity = 100; Assert.Greater(Harm(BattleCrisisLogic.Instinct(confident, unit, 0)), whole, "current wounds feed the compulsion");
        unit.composure = 0; Assert.Greater(Harm(BattleCrisisLogic.Instinct(confident, unit, 0)), whole * 1.15f, "Emotional Authenticity: output rises as regulation fails");
    }

    [TestCase("Empathetic", "Self-erasure")] [TestCase("Selfless", "Self-erasure")] [TestCase("Confident", "Reckless certainty")] [TestCase("Fearless", "Reckless certainty")]
    [TestCase("Devoted", "Obsession")] [TestCase("Vengeful", "Obsession")] [TestCase("Cautious", "Paralysis")] [TestCase("Coward", "Paralysis")]
    [TestCase("Ambitious", "Grandiosity")] [TestCase("Possessive", "Possessive control")] [TestCase("Protective", "Possessive control")]
    [TestCase("Manipulative", "Paranoia")] [TestCase("Paranoid", "Paranoia")] [TestCase("Analytical", "Raw instinct")]
    public void PersonalityTraitsMapToTheirMaladaptiveFace(string trait, string face)
    { Assert.AreEqual(face, BattleCrisisLogic.Expression(new BattleLegend { traits = { trait } }, Unit("A", 7))); }

    [Test]
    public void EveryFaceHasTwoDistinctCompulsionsAndACadenzaOfItsOpposite()
    {
        var unit = Unit("A", 7);
        foreach (BattleMaladaptation face in System.Enum.GetValues(typeof(BattleMaladaptation)))
        {
            var legend = new BattleLegend();
            if (face == BattleMaladaptation.AbandonmentPanic) legend.memories.Add("Lost a sister at the ford");
            else if (face == BattleMaladaptation.DesperateDefiance) legend.memories.Add("Won at Ash Hollow");
            else if (face != BattleMaladaptation.RawInstinct) legend.traits.Add(new[] { "", "Confident", "Empathetic", "Devoted", "Cautious", "Ambitious", "Possessive", "Paranoid" }[(int)face]);
            Assert.AreEqual(face, BattleCrisisLogic.Maladaptation(legend, unit));
            var first = BattleCrisisLogic.Instinct(legend, unit, 0); var second = BattleCrisisLogic.Instinct(legend, unit, 1);
            Assert.IsTrue(first.corrupted && second.corrupted && first.exhaust); Assert.AreNotEqual(first.name, second.name, face.ToString());
            Assert.IsTrue(first.effects.Concat(second.effects).Any(e => e.aim == CardAim.Enemy || e.aim == CardAim.EnemyLine), "a compulsion still reaches the enemy");
            var cadenza = BattleCrisisLogic.Cadenza(legend, unit);
            Assert.IsTrue(cadenza.cathartic && cadenza.effects.Any(e => e.op == CardOp.CatharticBlast), "every Cadenza can still strike friendly positions");
            StringAssert.StartsWith(BattleCrisisLogic.Opposite(face), cadenza.catchline);
        }
        Assert.AreEqual("Break Them Before They Leave", BattleCrisisLogic.Instinct(new BattleLegend { traits = { "Reckless" } }, unit, 0).name);
    }

    [Test]
    public void OrdinaryHarmDoesNotAdvancePersistentSurrender_ParasiticPressureIsExplicit()
    {
        var setup = Setup(); setup.attacker.sections[0].composure = 100; setup.attacker.sections[0].leader = new BattleLegend { name = "A", strain = 30 };
        setup.defender.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "feed", effects = { new CardEffect(CardOp.ParasiticResonance, CardAim.Enemy, 8) { range = 16 } } } });
        var run = Begin(setup); Assert.IsNull(run.CommitCard(false, 0, 0, beat: 1)); run.Perform();
        Assert.AreEqual(8, run.Attacker.sections[0].parasiticStrain); Assert.AreEqual(30, run.Attacker.sections[0].leader.strain);
        Assert.IsTrue(run.Report.crises.Any(e => e.cause == BattleCrisisCause.ParasiticResonance));
    }

    [Test]
    public void RehearsalCannotConsumeLiveInstinctsOrUnlockLiveCadenza()
    {
        var run = Begin(); var before = run.Hand(true).Count;
        var preview = run.Preview(new[] { new BattleCommand { kind = BattleCommandKind.Card, attacker = true, handIndex = Instinct(run), beat = 1 },
            new BattleCommand { kind = BattleCommandKind.Wait, beats = 1 } });
        Assert.IsEmpty(preview.failures); Assert.AreEqual(before, run.Hand(true).Count); Assert.AreEqual(0, run.Beat);
        Assert.AreEqual(0, run.Crises(true).Single().resolvedThisMeasure);
    }

    // ===== THE CONDUCTOR'S MIND BREAK =====

    private static CombatSection Ordinary(string name, int hex, int count, float composure = 100) => new CombatSection { name = name, battleHex = hex, count = count,
        eliteRole = BattleEliteRole.None, maxIntegrity = 500, integrity = 500, maxComposure = 100, composure = composure, row = FormationRow.Front, speed = 3, attack = 1 };
    /// <summary>A Conductor section (Spiraling at 20 Battle Composure) with the given traits, a formation behind it, and an enemy across Neutral Ground.</summary>
    private static BattleSetup ConductorSetup(params string[] traits)
    {
        var legend = new BattleLegend { name = "Maestra" }; legend.traits.AddRange(traits);
        var conductor = Unit("Maestra", 5, 20); conductor.eliteRole = BattleEliteRole.Conductor; conductor.leader = legend;
        var setup = new BattleSetup { seed = 91, field = new Battlefield { age = 3 },
            attacker = new BattleSide { manual = true, conductor = legend, sections = { conductor, Ordinary("Company", 11, 50), Ordinary("Rear Guard", 0, 50, 60) } },
            defender = new BattleSide { manual = true, sections = { Unit("D", 9) } } };
        setup.attacker.deck.Add(new DeckCard { voice = 1, card = new CombatCard { id = "drill", name = "drill", effects = { new CardEffect(CardOp.Strike, CardAim.Enemy, 1) } } });
        return setup;
    }

    [TestCase("Confident", BattleConductorMaladaptation.Aggressive)] [TestCase("Cautious", BattleConductorMaladaptation.Fearful)]
    [TestCase("Possessive", BattleConductorMaladaptation.Controlling)] [TestCase("Analytical", BattleConductorMaladaptation.None)]
    public void AMindBrokenConductorsArmyInheritsItsMaladaptation(string trait, BattleConductorMaladaptation expected)
    {
        var run = Begin(ConductorSetup(trait)); Assert.IsNull(run.Order(true, 2, BattleStandingOrder.Hold)); // the Rear Guard stays beside the Conductor
        run.ResolveMeasure();
        // A broken Conductor lends no Resonance mastery to Bandwidth; a controlling one takes one more Track away.
        int bandwidth = new CombatSettings().Measure.bandwidthBase;
        Assert.AreEqual(expected, run.ConductorMaladaptation(true));
        Assert.AreEqual(expected != BattleConductorMaladaptation.None, run.Report.crises.Any(e => e.cause == BattleCrisisCause.ConductorMaladaptation));
        run.BeginMeasure();
        Assert.IsNull(run.Order(true, 1, expected == BattleConductorMaladaptation.Fearful ? BattleStandingOrder.Advance : BattleStandingOrder.Hold)); run.Commit();
        var company = run.Score(true).Single(t => t.voice == 1);
        bool moves = company.steps.Skip(1).Any(h => h >= 0);
        if (expected == BattleConductorMaladaptation.Aggressive)
        {
            Assert.IsTrue(moves, "a Hold order becomes a reckless advance");
            Assert.Greater(run.Visualize().units.Single(u => u.attacker && u.section == 1).exposed, 0f, "it guards itself less");
        }
        if (expected == BattleConductorMaladaptation.Fearful) Assert.IsFalse(moves, "an Advance order becomes a defensive Hold");
        if (expected == BattleConductorMaladaptation.Controlling)
        {
            Assert.AreEqual(Math.Max(0, bandwidth - 1), run.Bandwidth(true), "it smothers improvisation");
            Assert.IsTrue(run.Report.spatialEvents.Any(e => e.cause == BattleSpatialCause.FriendlyFire), "its fear spills onto those beside it");
        }
        Assert.AreEqual(BattleTuningState.Detuned, run.TuningOf(true, 1), "a broken Conductor stops carrying the tempo");
    }

    [Test]
    public void AFearfulConductorsArmyAbandonsTheFightSooner()
    {
        var calm = Begin(ConductorSetup("Analytical")); var fearful = Begin(ConductorSetup("Cautious"));
        foreach (var run in new[] { calm, fearful }) { run.ResolveMeasure(); foreach (var unit in run.Attacker.sections.Where(x => x.eliteRole == BattleEliteRole.None)) unit.composure = 15; }
        calm.BeginMeasure(); calm.ResolveMeasure(); fearful.BeginMeasure(); fearful.ResolveMeasure();
        Assert.IsTrue(fearful.Over); Assert.IsFalse(calm.Over);
    }

    [Test]
    public void TheConductorsCorrodedGambitReachesTheWholeArmy()
    {
        var setup = ConductorSetup("Cautious"); var run = Begin(setup); run.ResolveMeasure();
        Assert.AreEqual(BattleConductorMaladaptation.Fearful, run.ConductorMaladaptation(true));
        run.BeginMeasure(); float before = run.Attacker.sections[2].composure, stability = run.Attacker.stanceStability;
        Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 1)); Assert.IsNull(run.CommitCard(true, Instinct(run), beat: 2)); run.Perform(2);
        Assert.IsTrue(run.Report.crises.Any(e => e.cause == BattleCrisisCause.CorrodedGambit));
        Assert.AreEqual(BattleConductorMaladaptation.None, run.ConductorMaladaptation(true));
        Assert.AreEqual(before + 15, run.Attacker.sections[2].composure, .5, "every standing ally hears the carrier signal return");
        Assert.GreaterOrEqual(run.Attacker.stanceStability, stability);
        Assert.AreEqual(BattleTuningState.InTune, run.TuningOf(true, 2));
    }

    [Test]
    public void AConductorWithoutASectionAlsoBreaksAtSpiraling_AndHoldsCompulsions()
    {
        var legend = new BattleLegend { name = "Voice" };
        var setup = new BattleSetup { seed = 5, field = new Battlefield { age = 3 },
            attacker = new BattleSide { manual = true, conductor = legend, sections = { Unit("A", 0) } },
            defender = new BattleSide { manual = true, sections = { Unit("D", 8) } } };
        setup.defender.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "terror", effects = { new CardEffect(CardOp.Dread, CardAim.Enemy, 900) { range = 16, flat = true } } } });
        var run = Begin(setup); Assert.IsNull(run.CommitCard(false, 0, 0, beat: 1)); run.ResolveMeasure();
        run.BeginMeasure();
        Assert.IsTrue(run.Hand(true).Any(c => c.voice < 0 && c.card.corrupted), "the Conductor's hand becomes its compulsions");
        Assert.IsTrue(run.Report.crises.Any(e => e.cause == BattleCrisisCause.MindBreak && e.voice < 0));
    }

    // ===== CO-REGULATION =====

    [Test]
    public void CoRegulationSteadiesThePatient_ItsCompulsionsLoseTheirCollateral()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Helper", 6));
        setup.attacker.deck.Add(new DeckCard { voice = 1, card = new CombatCard { id = "hum", name = "hum", effects = { new CardEffect(CardOp.CoRegulate, CardAim.Ally, 1) { durationBeats = 3, range = 2 } } } });
        var run = Begin(setup);
        Assert.IsTrue(run.Hand(true).Where(c => c.card.corrupted).Any(c => c.card.effects.Any(e => e.op == CardOp.SpatialChaos)));
        Assert.IsNull(run.CommitCard(true, run.Hand(true).ToList().FindIndex(c => c.card.id == "hum"), 0)); run.Perform(2);
        var crisis = run.Crises(true).Single(c => c.voice == 0);
        Assert.IsTrue(crisis.mindBroken); Assert.IsTrue(crisis.steadied);
        Assert.IsTrue(run.Hand(true).Where(c => c.card.corrupted).All(c => c.card.name.EndsWith("(steadied)") && !c.card.effects.Any(e => e.op == CardOp.SpatialChaos)));
        Assert.IsTrue(run.Report.crises.Any(e => e.cause == BattleCrisisCause.Steadied));
    }

    // ===== ORDINARY SECTIONS BREAK BY SCALE =====

    private static BattleSetup OrdinarySetup(CombatSection broken, int enemyHex = 9) => new BattleSetup { seed = 13, field = new Battlefield { age = 3 },
        attacker = new BattleSide { manual = true, sections = { broken } },
        defender = new BattleSide { manual = true, sections = { Ordinary("Foe", enemyHex, 50) } } };

    [Test]
    public void AHandfulFreezes_ACompanyRefusesToAdvance_AnArmyRouts()
    {
        var handful = Begin(OrdinarySetup(Ordinary("Scouts", 6, 5, 0))); handful.Order(true, 0, BattleStandingOrder.Advance); handful.Commit();
        Assert.IsFalse(handful.Score(true).Single().steps.Skip(1).Any(h => h >= 0), "frozen: no Steps");
        Assert.IsFalse(handful.Countdowns.Any(c => c.attacker && c.kind == BattleActionKind.DrilledAction), "frozen: no drilled Note");

        var company = Begin(OrdinarySetup(Ordinary("Company", 6, 50, 0))); company.Order(true, 0, BattleStandingOrder.Advance); company.Commit();
        Assert.IsFalse(company.Score(true).Single().steps.Skip(1).Any(h => h >= 0), "panic: it refuses to advance");

        var army = Begin(OrdinarySetup(Ordinary("Host", 6, 2000, 0)));
        Assert.AreEqual(5, army.Attacker.sections[0].battleHex, "a broken army routs toward home every Measure");
        Assert.IsTrue(army.Report.crises.Any(e => e.cause == BattleCrisisCause.OrdinaryBreak && e.detail == "Rout"));
    }

    [TestCase(.55f, true)] [TestCase(.8f, false)]
    public void ABrokenSectionHeldInContactSurrendersWithItsBodyStillWhole(float integrity, bool surrenders)
    {
        var broken = Ordinary("Company", 7, 50, 0); broken.integrity = broken.maxIntegrity * integrity;
        var run = Begin(OrdinarySetup(broken, 7)); run.ResolveMeasure();
        Assert.AreEqual(surrenders, run.Report.captives.Any(c => c.name == "Company"));
    }
}
