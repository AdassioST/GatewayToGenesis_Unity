using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>Chord Layering in time (Combat System.md): the Hold Limit, Interference, Suspension, Collapse, Ensembles, Reactions.</summary>
public class BattleChordTests
{
    private static CombatSection Unit(string name, int hex) => new CombatSection
    { name = name, battleHex = hex, row = FormationRow.Back, count = 1, maxIntegrity = 500f, integrity = 500f, maxComposure = 1000f, composure = 1000f, speed = 3f };
    private static CombatCard Core(string id = "core", bool magic = true, float amount = 32f) => new CombatCard
    { id = id, name = id, kind = magic ? CardKind.Spell : CardKind.Attack, binding = magic ? SpellBinding.Flux : SpellBinding.Unattuned,
        flicker = 0f, effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, amount) { range = 6 } } };
    private static CombatCard Minor(string id, BattleChordModifier mod = null, SpellBinding binding = SpellBinding.Flux) => new CombatCard
    { id = id, name = id, kind = CardKind.Spell, binding = binding, flicker = 0f, noteRole = BattleNoteRole.Minor,
        chord = mod ?? new BattleChordModifier { binding = binding, requiresAdjacent = false } };
    private static CombatCard Tool(CardOp op, CardAim aim, float amount = 1f, string id = null) => new CombatCard
    { id = id ?? op.ToString(), name = id ?? op.ToString(), kind = CardKind.Skill, purpose = SpellPurpose.Modulation,
        effects = { new CardEffect(op, aim, amount) { range = 6 } } };
    private static void Add(BattleSide side, CombatCard card, int voice = 0) => side.deck.Add(new DeckCard { card = card, voice = voice });
    private static CombatSettings Settings() => new CombatSettings
    { tuning = new CombatTuning { maxMeasures = 12, variance = 0f, fatigueFrom = 100 },
        symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 40 } } };
    private static BattleSetup Setup() => new BattleSetup
    { seed = 321, field = new Battlefield { age = 6 }, attacker = new BattleSide { name = "A", manual = true, sections = { Unit("A", 7) } },
        defender = new BattleSide { name = "D", manual = true, sections = { Unit("D", 8) } } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup, CombatSettings settings = null)
    { var run = BattleResolver.Begin(setup, settings ?? Settings(), forecastRuns: 1); run.BeginMeasure(); return run; }
    private static int Index(BattleResolver.BattleRun run, string id, bool attacker = true) => run.Hand(attacker).ToList().FindIndex(c => c.card.id == id);
    private static string Compose(BattleResolver.BattleRun run, string core, params string[] minors) =>
        run.CommitChord(true, Index(run, core), minors.Select(id => Index(run, id)).ToList(), 0);
    private static BattleSetup WithMinors(int count, Action<BattleSetup> also = null)
    {
        var setup = Setup(); Add(setup.attacker, Core());
        for (int i = 0; i < count; i++) Add(setup.attacker, Minor("m" + i));
        also?.Invoke(setup); return setup;
    }

    // ===== THE CORE =====

    [Test]
    public void PiercingChargeKeepsItsCore_AndTheFullTetradFillsItsPerformersMeasure()
    {
        var library = new SymphonySettings();
        var core = library.Card("piercing-charge"); var notes = new[] { library.Card("crystal-stability"), library.Card("resonance-synchronization"), library.Card("strand-momentum") };
        var composed = BattleChordLogic.Compose(core, notes, new BattleChordTuning());
        Assert.AreEqual(core.id, composed.id); Assert.AreEqual(core.name, composed.name); Assert.AreEqual(core.purpose, composed.purpose);
        Assert.AreEqual(core.effects[0].amount * 2f, composed.effects[0].amount, 1e-4f, "Strand gives the Charge repeating momentum");
        var setup = Setup(); setup.attacker.sections[0].battleHex = 6; setup.defender.sections[0].battleHex = 9;
        Add(setup.attacker, core.Clone()); foreach (var n in notes) Add(setup.attacker, n.Clone());
        var run = Begin(setup);
        Assert.IsNull(Compose(run, "piercing-charge", "crystal-stability", "resonance-synchronization", "strand-momentum"));
        var row = run.Score(true).Single(t => t.voice == 0);
        CollectionAssert.AreEqual(new[] { "Minor: Crystal Stability", "Minor: Resonance Synchronization", "Minor: Strand Momentum", "Major: Piercing Charge" }, row.beats.Skip(1));
        var intent = run.Countdowns.Single(x => x.kind == BattleActionKind.Chord);
        Assert.AreEqual(BattleFooting.Braced, intent.footing, "every Tetrad is at least Braced");
        Assert.AreEqual(5, intent.holdLimit, "base 4 and a Crystal Minor +1");
        run.ResolveMeasure();
        Assert.AreEqual(7, setup.attacker.sections[0].battleHex, "the Charge's impact Step is part of its resolution");
        Assert.Less(setup.defender.sections[0].integrity, 500f);
        Assert.AreEqual("Piercing Charge", run.Report.plays.Last(p => !p.minor).cardName);
    }

    [Test]
    public void TheChordMarginsMatchTheHoldTable()
    {
        int Margin(int minors, bool legato = false)
        {
            var setup = WithMinors(minors); if (legato) setup.attacker.tempo = SpellTempo.Legato;
            var run = Begin(setup);
            Assert.IsNull(Compose(run, "core", Enumerable.Range(0, minors).Select(i => "m" + i).ToArray()));
            return run.PreparedWeaves.Single().margin;
        }
        Assert.AreEqual(3, Margin(0), "Staccato Unison");
        Assert.AreEqual(2, Margin(1), "Dyad");
        Assert.AreEqual(1, Margin(2), "Triad");
        Assert.AreEqual(1, Margin(0, legato: true), "Legato Unison");
        Assert.AreEqual(0, Margin(3), "Tetrad");
    }

    [Test]
    public void ABareTetradSurvivesNothing_ADyadSurvivesTwoInterferences()
    {
        var tetrad = Begin(WithMinors(3));
        Assert.IsNull(tetrad.CommitChord(true, Index(tetrad, "core"), new[] { "m0", "m1", "m2" }.Select(id => Index(tetrad, id)).ToList(), 0, rendition: .9f));
        tetrad.ResolveMeasure();
        var collapse = tetrad.Report.chordEvents.Single(e => e.cause == BattleChordCause.Collapsed);
        Assert.AreEqual(3, collapse.beat, "two Missed Notes leave a Hold of 2 against a third sounding Beat");
        Assert.AreEqual(500f, tetrad.Defender.sections[0].integrity); Assert.Less(tetrad.Attacker.sections[0].integrity, 500f);

        var dyad = WithMinors(1, s => Add(s.defender, Tool(CardOp.Interrupt, CardAim.Enemy, 1f)));
        var run = Begin(dyad);
        Assert.IsNull(run.CommitChord(true, Index(run, "core"), new[] { Index(run, "m0") }, 0, rendition: .9f));
        Assert.IsNull(run.CommitCard(false, 0, 0, beat: 3));
        run.ResolveMeasure();
        Assert.AreEqual(3, run.Report.chordEvents.Count(e => e.cause == BattleChordCause.Missed || e.cause == BattleChordCause.Interrupted), "the Core's Miss also places one Interference when it releases");
        Assert.IsFalse(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Collapsed));
        Assert.AreEqual(468f, run.Defender.sections[0].integrity, "a Dyad survives a Missed note and an Interruption");
    }

    [Test]
    public void ACrystalMinorRaisesTheHold_AndTuningMovesIt()
    {
        var setup = Setup(); Add(setup.attacker, Core()); Add(setup.attacker, Minor("crystal", binding: SpellBinding.Crystal)); var run = Begin(setup);
        Assert.IsNull(Compose(run, "core", "crystal")); Assert.AreEqual(5, run.PreparedWeaves.Single().holdLimit);
        var detuned = Setup(); detuned.attacker.sections[0].composure = 400f; Add(detuned.attacker, Core()); var low = Begin(detuned);
        Assert.AreEqual(BattleTuningState.Detuned, low.TuningOf(true, 0)); Assert.IsNull(low.CommitCard(true, 0, 0));
        Assert.AreEqual(3, low.PreparedWeaves.Single().holdLimit, "Fractured Battle Composure detunes: -1 Hold");
        var spiraling = Setup(); spiraling.attacker.sections[0].composure = 100f; Assert.AreEqual(BattleTuningState.OutOfTune, Begin(spiraling).TuningOf(true, 0));
    }

    [Test]
    public void TheMeasurePerformed_UmbralCrushFinishesItsOwnSongBadly()
    {
        // Combat System.md, "A Measure, Performed". The example reads Umbral Crush's Hold as four despite its Crystal Minor, while the
        // Hold extension list grants a Crystal Minor +1: the inconsistency is flagged in the review; this test follows the example.
        var settings = Settings(); settings.measure.crystalMinorHold = 0;
        var setup = new BattleSetup { seed = 7, field = new Battlefield { age = 6 },
            attacker = new BattleSide { name = "Expedition", manual = true, sections = { Unit("Healer", 0), Unit("Mage", 5), Unit("Scout", 12) } },
            defender = new BattleSide { name = "Host", manual = true, sections = { Unit("Void caster", 10), Unit("Shield section", 10) } } };
        setup.defender.sections[1].row = FormationRow.Front;
        var crush = Core("umbral-crush", amount: 50f); crush.binding = SpellBinding.Void; crush.footing = BattleFooting.Anchored; Add(setup.defender, crush);
        Add(setup.defender, Minor("flux-minor")); Add(setup.defender, Minor("crystal-minor", binding: SpellBinding.Crystal));
        var pin = Core("pinning-shot", amount: 5f); pin.binding = SpellBinding.Luminance; pin.effects.Add(new CardEffect(CardOp.Interrupt, CardAim.Enemy, 1f) { range = 6 }); Add(setup.attacker, pin, 2);
        var lance = Core("flame-lance", amount: 20f); lance.binding = SpellBinding.Cindergale; Add(setup.attacker, lance, 1);
        var chime = Minor("dissonant-chime", new BattleChordModifier { binding = SpellBinding.Resonance, requiresAdjacent = false }, SpellBinding.Resonance);
        chime.chord.effects.Add(new CardEffect(CardOp.Interrupt, CardAim.Enemy, 1f) { range = 6, proximity = BattleProximity.RearMost }); Add(setup.attacker, chime, 1);
        var run = Begin(setup, settings);
        Assert.IsNull(run.CommitChord(false, Index(run, "umbral-crush", false), new[] { Index(run, "flux-minor", false), Index(run, "crystal-minor", false) }, 0, minorBeats: new[] { 2, 3 }));
        var umbral = run.PreparedWeaves.Single(w => !w.attacker);
        Assert.AreEqual(1, umbral.margin, "three Beats of sounding against a limit of four: margin one");
        Assert.IsNull(run.ComposePath(true, 2, new[] { 13 }));
        Assert.IsNull(run.CommitCard(true, Index(run, "pinning-shot"), 0, rendition: 1.25f, beat: 2));
        Assert.IsNull(run.CommitChord(true, Index(run, "flame-lance"), new[] { Index(run, "dissonant-chime") }, 1, rendition: 1.25f));
        run.ResolveMeasure();
        var collapse = run.Report.chordEvents.Single(e => e.cause == BattleChordCause.Collapsed);
        Assert.IsFalse(collapse.attacker); Assert.AreEqual(4, collapse.beat, "on the Cadence's downstroke, before it can release");
        Assert.AreEqual(2, run.Report.chordEvents.Count(e => !e.attacker && e.cause == BattleChordCause.Interrupted));
        Assert.AreEqual(500f, setup.attacker.sections[0].integrity, "the Healer was never struck by the working aimed at her");
        Assert.Less(setup.defender.sections[0].integrity, 495f, "the backlash strikes the caster first");
        Assert.Less(setup.defender.sections[1].integrity, 500f - 20f + 1e-3f, "and spills onto the shield section beside it");
    }

    // ===== COLLAPSE AND ENSEMBLES =====

    [Test]
    public void CollapseReturnsAlongItsChannels_PerformerFirstThenContributorsThenNearestAllies()
    {
        var setup = Setup(); setup.attacker.conductor = new BattleLegend { name = "Baton" };
        setup.attacker.sections.Add(Unit("Contributor", 6)); setup.attacker.sections.Add(Unit("Bystander", 1));
        Add(setup.attacker, Core()); Add(setup.attacker, Minor("ensemble"), 1); Add(setup.defender, Tool(CardOp.Interrupt, CardAim.Enemy, 3f));
        var run = Begin(setup);
        Assert.IsNull(Compose(run, "core", "ensemble")); Assert.IsNull(run.CommitCard(false, 0, 0, beat: 3)); run.ResolveMeasure();
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Collapsed));
        float performer = 500f - setup.attacker.sections[0].integrity, contributor = 500f - setup.attacker.sections[1].integrity, bystander = 500f - setup.attacker.sections[2].integrity;
        Assert.Greater(performer, contributor); Assert.Greater(contributor, 0f); Assert.Greater(bystander, 0f);
        Assert.AreEqual(500f, setup.defender.sections[0].integrity, "a Collapse is a debt returning home, never aimed at the enemy");
    }

    [Test]
    public void AnEnsembleNeedsTheConductorsCarrierSignal_OrABond()
    {
        BattleResolver.BattleRun Build(bool conductor, float composure = 1000f, bool bonded = false)
        {
            var setup = Setup(); setup.attacker.sections.Add(Unit("Partner", 6)); setup.attacker.sections[1].composure = composure;
            if (conductor) setup.attacker.conductor = new BattleLegend { name = "Baton" };
            if (bonded) setup.attacker.echoingBonds.Add(new BattleEchoingBond { first = "A", second = "Partner", strength = 1f });
            Add(setup.attacker, Core()); Add(setup.attacker, Minor("ensemble"), 1); return Begin(setup);
        }
        StringAssert.Contains("synchronized", Compose(Build(false), "core", "ensemble"));
        Assert.IsNull(Compose(Build(true), "core", "ensemble"));
        StringAssert.Contains("Detuned", Compose(Build(true, 400f), "core", "ensemble"));
        var duet = Build(false, bonded: true); Assert.IsNull(Compose(duet, "core", "ensemble"));
        Assert.AreEqual(BattleTuningState.Resonant, duet.TuningOf(true, 0)); Assert.AreEqual(BattleTuningState.Resonant, duet.TuningOf(true, 1), "a duet makes both performers Resonant");
        Assert.AreEqual(6, duet.PreparedWeaves.Single().holdLimit, "base 4, a bonded Ensemble +1, Resonant +1");
    }

    [Test]
    public void AContributorWhoFallsLeavesItsNoteUnresolved()
    {
        var setup = Setup(); setup.attacker.conductor = new BattleLegend { name = "Baton" }; setup.attacker.sections.Add(Unit("Contributor", 6));
        Add(setup.attacker, Core()); Add(setup.attacker, Minor("ensemble"), 1); var run = Begin(setup);
        Assert.IsNull(Compose(run, "core", "ensemble")); setup.attacker.sections[1].destroyed = true; Assert.IsNull(run.Perform());
        Assert.AreEqual(1, run.PreparedWeaves.Single().interference);
        run.ResolveMeasure(); Assert.AreEqual(468f, setup.defender.sections[0].integrity, "the Core still resolves within its Hold, without the lost Minor");
    }

    // ===== SUSPENSION =====

    [Test]
    public void AnAdaptiveMajorFallsBackThroughRankProximity()
    {
        var setup = Setup(); setup.defender.sections.Add(Unit("Other", 9));
        var core = Core(); core.effects[0].proximity = BattleProximity.Nearest; Add(setup.attacker, core); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0, 0)); setup.defender.sections[0].destroyed = true; run.ResolveMeasure();
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Fallback)); Assert.AreEqual(468f, setup.defender.sections[1].integrity);
    }

    [Test]
    public void ASuspendedChordIsStabilizedNextComposition_AtThePriceOfOneInterference()
    {
        var setup = Setup(); setup.defender.sections.Add(Unit("Other", 9)); Add(setup.attacker, Core()); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0, 0)); setup.defender.sections[0].destroyed = true; run.ResolveMeasure();
        var held = run.PreparedWeaves.Single(); Assert.IsTrue(held.suspended);
        run.BeginMeasure(); Assert.IsNull(run.Stabilize(true, held.action, 1, beatOfMeasure: 1));
        Assert.AreEqual(1, run.PreparedWeaves.Single().interference);
        run.ResolveMeasure(); Assert.AreEqual(468f, setup.defender.sections[1].integrity, "a new valid target on an upcoming Beat");
    }

    [Test]
    public void AGroundedChordDischargesThroughItsPerformerAlone()
    {
        var setup = Setup(); setup.defender.sections.Add(Unit("Other", 9)); setup.attacker.sections.Add(Unit("Neighbour", 6));
        Add(setup.attacker, Core()); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0, 0)); setup.defender.sections[0].destroyed = true; run.ResolveMeasure();
        run.BeginMeasure(); Assert.IsNull(run.Ground(true, run.PreparedWeaves.Single().action)); run.ResolveMeasure();
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Grounded));
        Assert.Less(setup.attacker.sections[0].integrity, 500f); Assert.AreEqual(500f, setup.attacker.sections[1].integrity, "it spills onto no one else");
        Assert.IsEmpty(run.PreparedWeaves);
    }

    [Test]
    public void AnUnansweredSuspensionCollapsesWhenItsHoldRunsOut()
    {
        var setup = Setup(); setup.defender.sections.Add(Unit("Other", 9)); setup.attacker.sections.Add(Unit("Neighbour", 6)); Add(setup.attacker, Core()); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0, 0)); setup.defender.sections[0].destroyed = true; run.ResolveMeasure();
        run.BeginMeasure(); run.ResolveMeasure();
        var collapse = run.Report.chordEvents.Single(e => e.cause == BattleChordCause.Collapsed);
        Assert.AreEqual(8, collapse.beat, "sounding since Beat 4, it outlives a Hold of 4 on Beat 8");
        Assert.Less(setup.attacker.sections[1].integrity, 500f, "the universe resolves it with the composer's own side");
    }

    // ===== SILENCE AS A WEAPON =====

    [TestCase(true)] [TestCase(false)]
    public void DelayAloneTurnsEitherArmysWorkingAgainstItsOwnUnits(bool castingAttacker)
    {
        var setup = Setup(); var casting = castingAttacker ? setup.attacker : setup.defender; var disrupting = castingAttacker ? setup.defender : setup.attacker;
        Add(casting, Core()); Add(casting, Minor("m0")); Add(casting, Minor("m1")); Add(disrupting, Tool(CardOp.Delay, CardAim.Enemy, 2f));
        var run = Begin(setup);
        Assert.IsNull(run.CommitChord(castingAttacker, Index(run, "core", castingAttacker), new[] { Index(run, "m0", castingAttacker), Index(run, "m1", castingAttacker) }, 0));
        Assert.IsNull(run.CommitCard(!castingAttacker, 0, 0, beat: 2));
        run.ResolveMeasure(); run.BeginMeasure(); run.ResolveMeasure();
        var collapse = run.Report.chordEvents.Single(e => e.cause == BattleChordCause.Collapsed);
        Assert.AreEqual(castingAttacker, collapse.attacker); Assert.AreEqual(6, collapse.beat, "delayed two Beats, the Triad would sound a fifth Beat");
        Assert.Less(casting.sections[0].integrity, 500f); Assert.AreEqual(500f, disrupting.sections[0].integrity);
    }

    [Test]
    public void AnEnemyCanBeDefeatedByDelayWithoutAnOffensiveCard()
    {
        var setup = Setup(); setup.defender.sections[0].maxIntegrity = setup.defender.sections[0].integrity = 30f;
        Add(setup.defender, Core()); Add(setup.defender, Minor("m0")); Add(setup.defender, Minor("m1")); Add(setup.attacker, Tool(CardOp.Delay, CardAim.Enemy, 2f));
        var run = Begin(setup);
        Assert.IsNull(run.CommitChord(false, Index(run, "core", false), new[] { Index(run, "m0", false), Index(run, "m1", false) }, 0));
        Assert.IsNull(run.CommitCard(true, 0, 0, beat: 2));
        var report = run.Finish();
        Assert.IsTrue(report.AttackerWon); Assert.AreEqual(500f, run.Attacker.sections[0].integrity);
        Assert.IsTrue(report.plays.Where(p => p.attacker).All(p => p.purpose == SpellPurpose.Modulation));
    }

    [Test]
    public void AccelerandoLeavesUnsoundedMinorsUnresolved()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Modulator", 6));
        Add(setup.attacker, Core()); Add(setup.attacker, Minor("m0")); Add(setup.attacker, Minor("m1")); Add(setup.attacker, Tool(CardOp.Accelerate, CardAim.Ally, 1f), 1);
        var run = Begin(setup);
        Assert.IsNull(Compose(run, "core", "m0", "m1")); Assert.IsNull(run.CommitCard(true, Index(run, "Accelerate"), 0, beat: 1));
        run.ResolveMeasure();
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.NoteFailed && e.detail.Contains("pulled forward")));
        Assert.AreEqual(3, run.Report.plays.Single(p => p.card == "core").beat);
    }

    [Test]
    public void AHeavyBlowInterruptsABracedChannel_NotAFreeOne()
    {
        int Interruptions(BattleFooting footing)
        {
            var setup = WithMinors(2); setup.attacker.deck[0].card.footing = footing;
            Add(setup.defender, Core("blow", magic: false, amount: 60f)); var run = Begin(setup);
            Assert.IsNull(Compose(run, "core", "m0", "m1")); Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2)); run.ResolveMeasure();
            return run.Report.chordEvents.Count(e => e.cause == BattleChordCause.Interrupted);
        }
        Assert.AreEqual(1, Interruptions(BattleFooting.Braced)); Assert.AreEqual(0, Interruptions(BattleFooting.Free));
    }

    // ===== HOLDING =====

    [Test]
    public void AFermataHoldsAWorkingPastFourBeats_AtATaxOnTheMind()
    {
        var setup = WithMinors(3); setup.attacker.sections.Add(Unit("Holder", 6));
        Add(setup.attacker, Tool(CardOp.ExtendHold, CardAim.Ally, 2f), 1); Add(setup.defender, Tool(CardOp.Delay, CardAim.Enemy, 2f));
        var run = Begin(setup);
        Assert.IsNull(Compose(run, "core", "m0", "m1", "m2")); Assert.IsNull(run.CommitCard(true, Index(run, "ExtendHold"), 0, beat: 1));
        Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2));
        float before = setup.attacker.sections[0].composure;
        run.ResolveMeasure(); run.BeginMeasure(); run.ResolveMeasure();
        Assert.IsFalse(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.Collapsed), "Fermata +2 holds the delayed Tetrad to its sixth Beat");
        Assert.AreEqual(2, run.Report.chordEvents.Count(e => e.cause == BattleChordCause.HoldDrain));
        Assert.AreEqual(6, run.Report.plays.Single(p => p.card == "core").beat);
        Assert.LessOrEqual(setup.attacker.sections[0].composure, before - 10f);
    }

    [Test]
    public void SustainNotesAndFermataMinorsRaiseTheHold_TheFermataCappedAtTwo()
    {
        var setup = Setup(); setup.attacker.conductor = new BattleLegend { name = "Baton" }; setup.attacker.sections.Add(Unit("Sustainer", 6));
        Add(setup.attacker, Core()); Add(setup.attacker, Minor("fermata", new BattleChordModifier { binding = SpellBinding.Flux, holdBeats = 5 }));
        Add(setup.attacker, Minor("sustain", new BattleChordModifier { binding = SpellBinding.Flux, holdBeats = 1, requiresAdjacent = false }), 1);
        var run = Begin(setup); Assert.IsNull(Compose(run, "core", "fermata", "sustain"));
        Assert.AreEqual(4 + 2 + 1, run.PreparedWeaves.Single().holdLimit);
    }

    [Test]
    public void AHyperChordStabilizesAtEachCadence_AndAMissedStabilizationDetonates()
    {
        BattleResolver.BattleRun Build(float rendition)
        {
            var setup = Setup(); setup.attacker.sections.Add(Unit("Ally", 6)); setup.defender.sections[0].battleHex = 9;
            var hyper = Core(amount: 100f); hyper.hyperMeasures = 2; Add(setup.attacker, hyper); Add(setup.attacker, Core("spare"));
            for (int i = 0; i < 3; i++) Add(setup.attacker, Minor("m" + i));
            var run = Begin(setup);
            Assert.IsNull(run.CommitChord(true, Index(run, "core"), new[] { "m0", "m1", "m2" }.Select(id => Index(run, id)).ToList(), 0, rendition: rendition));
            Assert.AreEqual(8, run.Countdowns.Single(x => x.kind == BattleActionKind.Chord).Remaining(run.Beat), "eight Beats before this becomes real");
            return run;
        }
        var steady = Build(1f); steady.ResolveMeasure();
        Assert.IsTrue(steady.Report.chordEvents.Any(e => e.cause == BattleChordCause.StabilizationCadence));
        steady.BeginMeasure(); Assert.IsNotNull(steady.CommitCard(true, 0), "the Anchored caster's Track belongs to the working");
        steady.ResolveMeasure(); Assert.AreEqual(400f, steady.Defender.sections[0].integrity);
        Assert.AreEqual(4, steady.Report.chordEvents.Count(e => e.cause == BattleChordCause.HoldDrain));
        var missed = Build(.9f); missed.ResolveMeasure();
        Assert.IsTrue(missed.Report.chordEvents.Any(e => e.cause == BattleChordCause.StaticCriticality || e.cause == BattleChordCause.Collapsed));
    }

    [Test]
    public void PerfectEchoAddsPower_AndAFullyPerfectChordShedsAnInterference()
    {
        float Damage(float rendition)
        {
            var run = Begin(WithMinors(1));
            Assert.IsNull(run.CommitChord(true, Index(run, "core"), new[] { Index(run, "m0") }, 0, rendition: rendition)); run.ResolveMeasure();
            return 500f - run.Defender.sections[0].integrity;
        }
        Assert.AreEqual(32f, Damage(1f), 1e-3f); Assert.AreEqual(40f, Damage(1.25f), 1e-3f, "up to +25%");
        var shed = Begin(WithMinors(2, s => Add(s.defender, Tool(CardOp.Interrupt, CardAim.Enemy, 1f))));
        Assert.IsNull(shed.CommitChord(true, Index(shed, "core"), new[] { Index(shed, "m0"), Index(shed, "m1") }, 0, rendition: 1.25f));
        Assert.IsNull(shed.CommitCard(false, 0, 0, beat: 2)); shed.ResolveMeasure();
        Assert.IsTrue(shed.Report.chordEvents.Any(e => e.cause == BattleChordCause.PerfectEcho));
    }

    // ===== REACTIONS =====

    [Test]
    public void AReactionIsPrimedWithoutBeats_OnePerTrack_FiringOncePerMeasure()
    {
        var setup = Setup();
        var counter = Tool(CardOp.IntegrityDamage, CardAim.Enemy, 10f, "counter"); counter.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Incoming, range = 6 };
        var second = Tool(CardOp.IntegrityDamage, CardAim.Enemy, 10f, "second"); second.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Incoming, range = 6 };
        Add(setup.attacker, counter); Add(setup.attacker, second); Add(setup.attacker, Core("own"));
        Add(setup.defender, Core("first", false, 5f)); Add(setup.defender, Minor("x")); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, Index(run, "counter"))); StringAssert.Contains("Reaction", run.CommitCard(true, Index(run, "second")));
        Assert.IsNull(run.CommitCard(true, Index(run, "own"), 0), "a primed Reaction occupies no Beat and no Major");
        Assert.IsNull(run.CommitChord(false, Index(run, "first", false), new[] { Index(run, "x", false) }, 0, beat: 2));
        run.ResolveMeasure();
        Assert.AreEqual(1, run.Report.plays.Count(p => p.reaction), "fires once per Measure");
        Assert.AreEqual(500f - 10f - 32f, setup.defender.sections[0].integrity, 1e-3f);
    }

    [Test]
    public void ACascadeChainStopsAtItsLinks()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Second", 6));
        var guard = Tool(CardOp.Guard, CardAim.Ally, 5f, "guard"); guard.effects[0].flat = true; guard.purpose = SpellPurpose.Defensive;
        guard.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Incoming, range = 2 }; Add(setup.attacker, guard, 1);
        var riposte = Tool(CardOp.IntegrityDamage, CardAim.Enemy, 10f, "riposte"); riposte.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Guarded, range = 2 };
        Add(setup.attacker, riposte);
        Add(setup.defender, Core("hit", false, 20f));
        var settings = Settings(); settings.measure.cascadeLinks = 1; var run = Begin(setup, settings);
        Assert.IsNull(run.CommitCard(true, Index(run, "guard"), 0)); Assert.IsNull(run.CommitCard(true, Index(run, "riposte")));
        Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2)); run.ResolveMeasure();
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.cause == BattleChordCause.CascadeLimit), "the riposte would be a second link");
        Assert.AreEqual(500f, setup.defender.sections[0].integrity);
    }

    [Test]
    public void GuardCounterAndBondProtectionFormAVisibleCascade()
    {
        var setup = Setup(); setup.attacker.sections.Add(Unit("Bonded", 6)); setup.attacker.echoingBonds.Add(new BattleEchoingBond { first = "A", second = "Bonded", strength = 1f });
        var counter = Tool(CardOp.IntegrityDamage, CardAim.Enemy, 10f, "counter"); counter.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.Guarded, range = 2 };
        Add(setup.attacker, counter); Add(setup.defender, Core("charge", false, 30f)); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0)); Assert.IsNull(run.CommitCard(false, 0, 0, beat: 2)); run.ResolveMeasure();
        Assert.AreEqual(500f - 30f + 8f, setup.attacker.sections[0].integrity, 1e-3f, "the bonded partner throws themself before the blow");
        Assert.AreEqual(490f, setup.defender.sections[0].integrity, "and the guarded ally answers in the same instant");
        Assert.IsTrue(run.Report.chordEvents.Any(e => e.detail.Contains("bonded partner"))); Assert.IsTrue(run.Report.chordEvents.Any(e => e.detail.Contains("Guarded")));
    }

    [Test]
    public void AnInterceptStepsIntoTheEnemysPath()
    {
        var setup = Setup(); setup.attacker.sections[0].battleHex = 6; setup.defender.sections[0].battleHex = 9;
        var intercept = Tool(CardOp.Dread, CardAim.Enemy, 1f, "intercept"); intercept.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.EnemyEntersAdjacent, range = 1, intercept = true };
        Add(setup.attacker, intercept); var run = Begin(setup);
        Assert.IsNull(run.CommitCard(true, 0)); Assert.IsNull(run.ComposePath(false, 0, new[] { 8, 7, 6 }));
        run.ResolveMeasure();
        Assert.AreEqual(7, setup.attacker.sections[0].battleHex); Assert.AreEqual(7, setup.defender.sections[0].battleHex, "contact is forced early and the path stops there");
        Assert.IsTrue(run.Report.spatialEvents.Any(e => !e.attacker && e.cause == BattleSpatialCause.FailedMovementCommitment));
    }

    // ===== SCALE OF A WORKING =====

    [Test]
    public void AFifteenMinorEnsembleResolves_AndAStrongEnoughWardCancelsItOutright()
    {
        BattleResolver.BattleRun Build(float ward)
        {
            var setup = Setup(); setup.attacker.conductor = new BattleLegend { name = "Baton" };
            for (int i = 0; i < 5; i++) setup.attacker.sections.Add(Unit("Voice " + i, new[] { 6, 1, 12, 0, 11 }[i]));
            Add(setup.attacker, Core());
            for (int i = 0; i < 15; i++) Add(setup.attacker, Minor("m" + i, new BattleChordModifier { binding = SpellBinding.Flux, requiresAdjacent = false, power = 1.1f }), 1 + i / 3);
            if (ward > 0f) { var w = Tool(CardOp.Ward, CardAim.Self, ward, "ward"); w.effects[0].flat = true; w.purpose = SpellPurpose.Defensive; Add(setup.defender, w); }
            var run = Begin(setup);
            Assert.IsNull(run.CommitChord(true, Index(run, "core"), Enumerable.Range(0, 15).Select(i => Index(run, "m" + i)).ToList(), 0));
            if (ward > 0f) Assert.IsNull(run.CommitCard(false, 0, beat: 4));
            run.ResolveMeasure(); return run;
        }
        var open = Build(0f); Assert.AreEqual(500f - 32f * (float)Math.Pow(1.1, 15), open.Defender.sections[0].integrity, .5f);
        Assert.AreEqual(16, open.Report.plays.Count(p => p.action == open.Report.plays.First(x => x.card == "core").action));
        var answered = Build(200f); Assert.AreEqual(500f, answered.Defender.sections[0].integrity, "a strong Ward, prepared in time, erases an enormous stack");
    }

    // ===== SHARED RULES =====

    [Test]
    public void TheAutomaticComposerAndAManualReplayResolveTheSameChord()
    {
        var setup = WithMinors(1); var autoSetup = setup.Clone(321); autoSetup.attacker.manual = false; var automatic = Begin(autoSetup);
        Assert.IsTrue(automatic.Countdowns.Any(c => c.kind == BattleActionKind.Chord));
        var manual = Begin(setup); Assert.IsNull(Compose(manual, "core", "m0"));
        automatic.ResolveMeasure(); manual.ResolveMeasure();
        CollectionAssert.AreEqual(manual.Visualize().units.Select(u => u.integrity), automatic.Visualize().units.Select(u => u.integrity));
        CollectionAssert.AreEqual(manual.Report.chordEvents.Select(e => e.cause), automatic.Report.chordEvents.Select(e => e.cause));
    }

    [Test]
    public void RehearsingACollapseLeavesTheLiveBattleUntouched()
    {
        var run = Begin(WithMinors(3));
        var commands = new[] { new BattleCommand { kind = BattleCommandKind.Chord, handIndex = Index(run, "core"), minors = { Index(run, "m0"), Index(run, "m1"), Index(run, "m2") }, target = 0, rendition = .9f },
            new BattleCommand { kind = BattleCommandKind.Wait, beats = 4 } };
        var first = run.Preview(commands); var again = run.Preview(commands);
        Assert.AreEqual(0, run.Beat); Assert.AreEqual(4, run.Hand(true).Count); Assert.IsEmpty(run.PreparedWeaves);
        CollectionAssert.AreEqual(first.after.units.Select(u => u.integrity), again.after.units.Select(u => u.integrity));
        Assert.IsTrue(first.chordEvents.Any(e => e.cause == BattleChordCause.Collapsed));
        Assert.IsNull(run.CommitChord(true, Index(run, "core"), new[] { "m0", "m1", "m2" }.Select(id => Index(run, id)).ToList(), 0, rendition: .9f)); run.ResolveMeasure();
        CollectionAssert.AreEqual(first.after.units.Select(u => u.integrity), run.Visualize().units.Select(u => u.integrity));
    }

    [TestCase(false)] [TestCase(true)]
    public void PenetrationBypassesArmor(bool penetrate)
    {
        float Damage(bool pierce)
        {
            var setup = Setup(); setup.attacker.sections[0].attack = 40f; setup.defender.sections[0].armor = 1000f;
            var core = Core(magic: false); core.effects.Clear(); core.effects.Add(new CardEffect(CardOp.Strike, CardAim.Enemy, 1f) { range = 4 }); Add(setup.attacker, core);
            var mod = Minor("piercing", new BattleChordModifier { penetrate = pierce, requiresAdjacent = false }, SpellBinding.Unattuned); mod.kind = CardKind.Skill; Add(setup.attacker, mod);
            var run = Begin(setup); Assert.IsNull(Compose(run, "core", "piercing")); run.ResolveMeasure();
            return 500f - setup.defender.sections[0].integrity;
        }
        if (penetrate) Assert.Greater(Damage(true), Damage(false)); else Assert.Greater(Damage(false), 0f);
    }

    [Test]
    public void BattleCardAuthoringRoundTripsTheMeasureFields()
    {
        var card = Minor("authored", new BattleChordModifier { holdBeats = 2, penetrate = true, changeTargeting = true, targeting = BattleProximity.Furthest });
        card.tempo = CardTempo.Legato; card.footing = BattleFooting.Anchored; card.channel = 3; card.hyperMeasures = 2; card.abjuredOnlyBy.Add("crystal-aegis"); card.requiresChannel = true;
        card.reaction = new BattleReactionSpec { trigger = BattleReactionTrigger.EnemyEntersLane, bondOnly = true, intercept = true };
        var copy = (CombatCard)SaveStateCodec.Read(SaveStateCodec.Write(card, typeof(CombatCard)), typeof(CombatCard));
        Assert.AreEqual(BattleNoteRole.Minor, copy.noteRole); Assert.AreEqual(CardTempo.Legato, copy.tempo); Assert.AreEqual(BattleFooting.Anchored, copy.footing);
        Assert.AreEqual(3, copy.channel); Assert.AreEqual(2, copy.hyperMeasures); CollectionAssert.AreEqual(new[] { "crystal-aegis" }, copy.abjuredOnlyBy);
        Assert.IsTrue(copy.requiresChannel); Assert.AreEqual(2, copy.chord.holdBeats); Assert.IsTrue(copy.chord.penetrate); Assert.IsTrue(copy.reaction.intercept);
        var clone = copy.Clone(); clone.abjuredOnlyBy.Add("other"); clone.chord.effects.Add(new CardEffect(CardOp.Guard, CardAim.Self, 1f));
        Assert.AreEqual(1, copy.abjuredOnlyBy.Count); Assert.IsEmpty(copy.chord.effects);
    }
}
