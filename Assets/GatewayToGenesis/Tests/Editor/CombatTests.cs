using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Symphony of War's macro layer with no scene: the Elemental Harmonic Circle (canon order), the Age's command of
/// chords and tempos (Ages.md), Orchestral Formation templates, creatures as sections, the battlefield read from a
/// world cell, and the auto-resolve (determinism, element, ground, the Loom, the conductor).
/// </summary>
public class CombatTests
{
    private static readonly CombatSettings Settings = new CombatSettings();

    // ---- The circle ------------------------------------------------------------------------------------------------

    [TestCase(SpellBinding.Flux, SpellBinding.Cindergale)]
    [TestCase(SpellBinding.Cindergale, SpellBinding.Crystal)]
    [TestCase(SpellBinding.Crystal, SpellBinding.Resonance)]
    [TestCase(SpellBinding.Resonance, SpellBinding.Strand)]
    [TestCase(SpellBinding.Strand, SpellBinding.Flux)]
    public void PentatonicCircle_FollowsTheVault(SpellBinding attack, SpellBinding defender)
    {
        Assert.AreEqual(HarmonicMatch.Overcomes, HarmonicCircle.Match(attack, defender));
        Assert.AreEqual(HarmonicMatch.Resisted, HarmonicCircle.Match(defender, attack), "the reverse is resisted");
        Assert.Greater(HarmonicCircle.Multiplier(attack, defender, null), 1f);
        Assert.Less(HarmonicCircle.Multiplier(defender, attack, null), 1f);
        Assert.AreEqual(defender, HarmonicCircle.Overcomes(attack));
        Assert.AreEqual(attack, HarmonicCircle.OvercomeBy(defender));
    }

    [Test]
    public void LightAndShadow_DanceOutsideTheCircle()
    {
        Assert.AreEqual(HarmonicMatch.Dance, HarmonicCircle.Match(SpellBinding.Luminance, SpellBinding.Void));
        Assert.AreEqual(HarmonicMatch.Dance, HarmonicCircle.Match(SpellBinding.Void, SpellBinding.Luminance));
        Assert.Greater(HarmonicCircle.Multiplier(SpellBinding.Void, SpellBinding.Luminance, null), 1f, "each is effective against the other");
        foreach (var other in HarmonicCircle.Pentatonic)
        {
            Assert.AreEqual(HarmonicMatch.Neutral, HarmonicCircle.Match(SpellBinding.Luminance, other));
            Assert.AreEqual(HarmonicMatch.Neutral, HarmonicCircle.Match(other, SpellBinding.Void));
        }
        Assert.AreEqual(HarmonicMatch.Neutral, HarmonicCircle.Match(SpellBinding.Void, SpellBinding.Void));
        CollectionAssert.AreEqual(new[] { SpellBinding.Void }, HarmonicCircle.WeakTo(SpellBinding.Luminance).ToArray());
        CollectionAssert.IsEmpty(HarmonicCircle.Resists(SpellBinding.Luminance).ToArray());
    }

    [Test]
    public void Unattuned_HasNoWeaknessAndNoResistance()
    {
        foreach (var b in HarmonicCircle.Seven)
        {
            Assert.AreEqual(1f, HarmonicCircle.Multiplier(b, SpellBinding.Unattuned, null));
            Assert.AreEqual(1f, HarmonicCircle.Multiplier(SpellBinding.Unattuned, b, null));
        }
        CollectionAssert.IsEmpty(HarmonicCircle.WeakTo(SpellBinding.Unattuned).ToArray());
    }

    [Test]
    public void EveryElement_HasOneWeakness()
    {
        foreach (var b in HarmonicCircle.Seven) Assert.AreEqual(1, HarmonicCircle.WeakTo(b).Count(), b.ToString());
        StringAssert.Contains("weak to Flux", HarmonicCircle.Describe(SpellBinding.Cindergale));
        StringAssert.Contains("resists Crystal", HarmonicCircle.Describe(SpellBinding.Cindergale));
    }

    [Test]
    public void BindingNames_MatchTheVaultsOrder()
    {
        for (int i = 0; i < MagicBindings.All.Length; i++)
        {
            Assert.AreEqual(HarmonicCircle.Seven[i], HarmonicCircle.Of(MagicBindings.All[i]));
            Assert.AreEqual(MagicBindings.All[i], HarmonicCircle.Name(HarmonicCircle.Seven[i]));
        }
        Assert.AreEqual(SpellBinding.Unattuned, HarmonicCircle.Of("mana"));
    }

    [Test]
    public void Coverage_PicksTheSecondaryThatLandsHardest()
    {
        var roots = new[] { SpellBinding.Crystal, SpellBinding.Strand };
        Assert.AreEqual(SpellBinding.Strand, HarmonicCircle.BestAgainst(roots, SpellBinding.Flux, null));
        Assert.AreEqual(SpellBinding.Crystal, HarmonicCircle.BestAgainst(roots, SpellBinding.Resonance, null));
        Assert.AreEqual(SpellBinding.Crystal, HarmonicCircle.BestAgainst(roots, SpellBinding.Unattuned, null), "the first on a tie");
    }

    // ---- The Ages --------------------------------------------------------------------------------------------------

    [Test]
    public void AgeMagic_FollowsAgesMd()
    {
        CollectionAssert.AreEqual(new[] { SpellTempo.Staccato }, AgeMagic.Tempos(0).ToArray(), "Age 0: flickering Staccato");
        Assert.IsTrue(AgeMagic.TempoAvailable(SpellTempo.Legato, 1));
        Assert.IsFalse(AgeMagic.TempoAvailable(SpellTempo.Ritardando, 1));
        Assert.IsTrue(AgeMagic.TempoAvailable(SpellTempo.Ritardando, 2));
        Assert.IsTrue(AgeMagic.TempoAvailable(SpellTempo.Accelerando, 3));
        Assert.IsTrue(AgeMagic.TempoAvailable(SpellTempo.Polyrhythm, 4));
        Assert.AreEqual(ChordTier.Dyad, AgeMagic.MaxTier(0), "unreliable Unison and Dyad Spells");
        Assert.AreEqual(ChordTier.Dyad, AgeMagic.MaxTier(1));
        Assert.AreEqual(ChordTier.Triad, AgeMagic.MaxTier(2), "rare Triad Chords");
        Assert.AreEqual(ChordTier.Triad, AgeMagic.MaxTier(4));
        Assert.AreEqual(ChordTier.Tetrad, AgeMagic.MaxTier(5), "unreliable Tetrad Chords");
        Assert.IsFalse(AgeMagic.MajorNotes(0), "mostly Minor Note magic");
        Assert.IsTrue(AgeMagic.MajorNotes(1));
        var t = CombatTuning.Default;
        Assert.Greater(AgeMagic.Interference(ChordTier.Unison, 0, t), AgeMagic.Interference(ChordTier.Unison, 1, t), "first stable Unison in Age I");
        Assert.Greater(AgeMagic.Interference(ChordTier.Triad, 2, t), AgeMagic.Interference(ChordTier.Triad, 3, t), "first reliable Triads in Age III");
        Assert.Greater(AgeMagic.Interference(ChordTier.Tetrad, 5, t), AgeMagic.Interference(ChordTier.Tetrad, 6, t), "semi stable Tetrads in Age VI");
    }

    [Test]
    public void TempoCurves_HaveTheirShapes()
    {
        Assert.Greater(BattleResolver.TempoCurve(SpellTempo.Staccato, 1), BattleResolver.TempoCurve(SpellTempo.Staccato, 6));
        Assert.Less(BattleResolver.TempoCurve(SpellTempo.Accelerando, 1), BattleResolver.TempoCurve(SpellTempo.Accelerando, 6));
        Assert.AreEqual(BattleResolver.TempoCurve(SpellTempo.Legato, 1), BattleResolver.TempoCurve(SpellTempo.Legato, 9));
        Assert.Greater(BattleResolver.TempoCurve(SpellTempo.Ritardando, 1), BattleResolver.TempoCurve(SpellTempo.Ritardando, 8));
    }

    // ---- Formations ------------------------------------------------------------------------------------------------

    [TestCase("hearth-guard", 0)]
    [TestCase("ember-host", 0)]
    [TestCase("tidebound-choir", 1)]
    [TestCase("cinder-vanguard", 1)]
    [TestCase("prism-bastion", 2)]
    public void DefaultTemplates_AreRaisableInTheirAge(string id, int age)
    {
        var template = Settings.Template(id);
        Assert.IsNotNull(template, id);
        CollectionAssert.IsEmpty(OrchestralFormations.Validate(template, Settings, age));
        var side = OrchestralFormations.Raise(template, Settings, age);
        Assert.AreEqual(template.Slots.Count(), side.sections.Count);
    }

    [Test]
    public void Raise_CutsBackWhatTheAgeCannotPlay()
    {
        var template = Settings.Template("prism-bastion");
        var problems = new List<string>();
        var side = OrchestralFormations.Raise(template, Settings, 1, problems: problems);
        Assert.IsTrue(problems.Any(p => p.Contains("Blade Dancers")), "dancers wait for Age II");
        Assert.IsTrue(problems.Any(p => p.Contains("Triad")), "a Triad is beyond Age I");
        Assert.IsTrue(problems.Any(p => p.Contains("Ritardando")), "Ritardando arrives in Age II");
        Assert.AreEqual(SpellTempo.Staccato, side.tempo);
        Assert.IsFalse(side.sections.Any(s => s.specId == "blade-dancers" || s.specId == "crystalwrights"));
        var crystal = side.sections.First(s => s.specId == "weaver-circle" && s.primary == SpellBinding.Crystal);
        Assert.AreEqual(ChordTier.Dyad, crystal.Tier, "the Triad is cut to the Dyad Age I plays");
    }

    [Test]
    public void Validate_CatchesBadChordsAndLanes()
    {
        var t = new FormationTemplate { name = "Bad" };
        t.back.Add(new FormationSlot { section = "weaver-circle", binding = SpellBinding.Flux, harmony = { SpellBinding.Flux } });
        t.back.Add(new FormationSlot { section = "grave-warden" });
        t.back.Add(new FormationSlot { section = "weaver-circle" });
        var problems = OrchestralFormations.Validate(t, Settings, 6);
        Assert.IsTrue(problems.Any(p => p.Contains("no front lane")));
        Assert.IsTrue(problems.Any(p => p.Contains("Minor Notes must be distinct")));
        Assert.IsTrue(problems.Any(p => p.Contains("front lane, not the back")));
        Assert.IsTrue(problems.Any(p => p.Contains("needs a binding")));
    }

    [Test]
    public void Troops_AreUnattunedButSpellweaversCarryTheirBinding()
    {
        var side = OrchestralFormations.Raise(Settings.Template("tidebound-choir"), Settings, 1);
        Assert.IsTrue(side.sections.Where(s => !s.Casts).All(s => s.primary == SpellBinding.Unattuned));
        Assert.IsTrue(side.sections.Where(s => s.Casts).All(s => s.primary == SpellBinding.Flux), "a weaver's primary binding is its root, and its weakness");
    }

    [Test]
    public void DefaultGrounds_NameEachTerrainOnce()
    {
        var all = Settings.Grounds.SelectMany(g => g.terrains).ToList();
        Assert.AreEqual(all.Count, all.Distinct().Count());
        Assert.AreEqual(BattleGround.Forest, Settings.GroundOf("woodland").ground);
        Assert.AreEqual(BattleGround.Open, Settings.GroundOf("nowhere").ground);
        Assert.AreEqual(SpellBinding.Cindergale, Settings.ElementOf("ash-plains"));
    }

    // ---- Creatures -------------------------------------------------------------------------------------------------

    private static SpeciesSpec Wolf => new SpeciesSpec { id = "wolf", name = "Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Medium, structure = 0.9f };

    private static SpeciesSpec Salamander => new SpeciesSpec
    {
        id = "salamander", name = "Salamander", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Benign, size = CreatureSize.Medium,
        structure = 0.5f, binding = BindingOrgan.Hide, primaryBinding = SpellBinding.Cindergale, secondaryBindings = { SpellBinding.Crystal },
    };

    [Test]
    public void Creatures_FightByTheirNature()
    {
        var wolves = CreatureCombat.Sections(Wolf, 8);
        Assert.IsTrue(wolves.All(s => s.primary == SpellBinding.Unattuned && !s.Casts), "an ordinary animal is unattuned and casts nothing");
        var salamanders = CreatureCombat.Sections(Salamander, 10);
        Assert.IsTrue(salamanders.All(s => s.primary == SpellBinding.Cindergale && s.Casts && s.ward > 0f), "armored Coherence-Binding hide");
        CollectionAssert.AreEqual(new[] { SpellBinding.Cindergale, SpellBinding.Crystal }, salamanders[0].Roots.ToArray());
        var moth = new SpeciesSpec { name = "Moth", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Frightful, size = CreatureSize.Small, structure = 0.3f, binding = BindingOrgan.Wings, primaryBinding = SpellBinding.Luminance };
        Assert.AreEqual(FormationRow.Back, CreatureCombat.Sections(moth, 30)[0].row, "wings cast from above the fight");
        var giant = new SpeciesSpec { name = "Giant", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Venerable, size = CreatureSize.Gargantuan, structure = 0.55f };
        Assert.AreEqual(2f, CreatureCombat.Sections(giant, 1)[0].width);
        Assert.LessOrEqual(CreatureCombat.Sections(new SpeciesSpec { name = "Ant", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Jingoistic, size = CreatureSize.Small }, 500).Count, 8);
        StringAssert.Contains("weak to Flux", CreatureCombat.ElementWords(Salamander));
        StringAssert.Contains("also casts Crystal", CreatureCombat.ElementWords(Salamander));
        Assert.IsNull(CreatureCombat.ElementWords(Wolf));
    }

    [Test]
    public void ShyCreatures_LeaveTheFieldEarly()
    {
        Assert.Greater(CreatureCombat.WithdrawAt(ThreatResponse.FleesOnSight), CreatureCombat.WithdrawAt(ThreatResponse.DefendsTerritory));
        Assert.Less(CreatureCombat.WithdrawAt(ThreatResponse.Hunts), 0f, "hunters use the tuning's threshold");
    }

    // ---- The field -------------------------------------------------------------------------------------------------

    [Test]
    public void Battlefield_ReadsTheWorldCell()
    {
        var gen = new WorldGenSettings();
        gen.terrains.Add(new TerrainSpec { id = "marsh", name = "Marsh" });
        var tile = new WorldTile { index = 5, terrain = "marsh", river = true, downstream = 9, elevation = 0.5f, coherence = 0.8f, dissonance = 0.1f, leylines = 2, sacred = true, fallout = 0.3f, settlement = 0 };
        var from = new WorldTile { index = 4, terrain = "plains", elevation = 0.4f, downstream = -1 };
        var f = Battlefield.From(tile, from, gen, Settings, 2, 4);
        Assert.AreEqual(BattleGround.Marsh, f.ground);
        Assert.AreEqual(SpellBinding.Flux, f.element);
        Assert.IsTrue(f.riverCrossing);
        Assert.AreEqual(0.1f, f.height, 1e-4f);
        Assert.IsTrue(f.leyline && f.sacred && f.settlement);
        Assert.AreEqual(0.8f, gen.MagicAccess(2), 1e-4f);
        Assert.AreEqual(gen.MagicAccess(2), f.magicAccess, 1e-4f);
        var along = new WorldTile { index = 4, terrain = "marsh", downstream = 5, river = true };
        Assert.IsFalse(Battlefield.From(tile, along, gen, Settings, 2, 4).riverCrossing, "coming down the same river");
    }

    // ---- Resolution ------------------------------------------------------------------------------------------------

    private static BattleSetup Setup(string attacker, string defender, int age, int seed = 7, Battlefield field = null) => new BattleSetup
    {
        attacker = OrchestralFormations.Raise(Settings.Template(attacker), Settings, age),
        defender = OrchestralFormations.Raise(Settings.Template(defender), Settings, age),
        field = field ?? new Battlefield { age = age, magicAccess = 1f },
        seed = seed,
    };

    [Test]
    public void Resolve_IsDeterministicForASeed()
    {
        var setup = Setup("tidebound-choir", "cinder-vanguard", 1);
        var first = BattleResolver.Resolve(setup.Clone(11), Settings);
        var again = BattleResolver.Resolve(setup.Clone(11), Settings);
        Assert.AreEqual(first.outcome, again.outcome);
        Assert.AreEqual(first.measures, again.measures);
        Assert.AreEqual(first.attacker.dead, again.attacker.dead, 1e-4f);
        CollectionAssert.AreEqual(first.log, again.log);
        Assert.IsTrue(first.log.Count > 2);
        Assert.AreEqual(setup.attacker.sections[0].maxIntegrity, setup.attacker.sections[0].integrity, "clones leave the setup untouched");
    }

    [Test]
    public void Resolve_EndsWithAWinnerOrTheDefenderHolding()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var r = BattleResolver.Resolve(Setup("ember-host", "hearth-guard", 0, seed), Settings);
            Assert.LessOrEqual(r.measures, CombatTuning.Default.maxMeasures);
            Assert.AreEqual(r.outcome == BattleOutcome.Stalemate, r.winner == 0);
            Assert.GreaterOrEqual(r.attacker.dead + r.attacker.wounded, 0f);
            Assert.AreEqual(r.attacker.integrityBefore - r.attacker.integrityAfter, r.attacker.dead + r.attacker.wounded + r.attacker.capturedIntegrity, 0.01f);
        }
    }

    private static BattleSide Weavers(SpellBinding root, params SpellBinding[] harmony)
    {
        var t = new FormationTemplate { name = root.ToString(), tempo = SpellTempo.Legato };
        t.front.Add(new FormationSlot { section = "shieldwall" });
        t.back.Add(new FormationSlot { section = "weaver-circle", binding = root, harmony = harmony.ToList() });
        t.back.Add(new FormationSlot { section = "weaver-circle", binding = root, harmony = harmony.ToList() });
        return OrchestralFormations.Raise(t, Settings, 6);
    }

    [Test]
    public void TheCircle_DecidesHowHardSpellsLand()
    {
        // Flux overcomes Cindergale; Cindergale overcomes Crystal, so Crystal breaks on it.
        var field = new Battlefield { age = 6, magicAccess = 1f };
        var flux = BattleResolver.Forecast(new BattleSetup { attacker = Weavers(SpellBinding.Flux), defender = CreatureCombat.Side(Salamander, 20), field = field, seed = 3 }, Settings, 15);
        var crystal = BattleResolver.Forecast(new BattleSetup { attacker = Weavers(SpellBinding.Crystal), defender = CreatureCombat.Side(Salamander, 20), field = field, seed = 3 }, Settings, 15);
        Assert.Greater(flux.defenderLoss, crystal.defenderLoss);
        var report = BattleResolver.Resolve(new BattleSetup { attacker = Weavers(SpellBinding.Flux), defender = CreatureCombat.Side(Salamander, 20), field = field, seed = 3 }, Settings);
        Assert.IsTrue(report.matchups.ContainsKey("Flux on Cindergale"));
        Assert.IsTrue(report.log.Any(l => l.Contains("extinguish")), "the report gives the vault's reason");
    }

    private static BattleSide Dummy(float structure = 0.75f, HarmonicNiche niche = HarmonicNiche.Coherent) => new BattleSide
    {
        name = "Dummy",
        sections = { new CombatSection { name = "Target", row = FormationRow.Front, maxIntegrity = 5000f, integrity = 5000f, maxComposure = 5000f, composure = 5000f, defense = 1f, breakthrough = 1f, structure = structure, niche = niche } },
    };

    [Test]
    public void ResonanceMinorNote_CarriesSpellsPastSignalLoss()
    {
        // Two Dyads of equal tier: Resonance cuts Signal Loss, Strand only mends the caster.
        var field = new Battlefield { age = 6, magicAccess = 1f };
        var carried = BattleResolver.Resolve(new BattleSetup { attacker = Weavers(SpellBinding.Flux, SpellBinding.Resonance), defender = Dummy(), field = field, seed = 5 }, Settings);
        var plain = BattleResolver.Resolve(new BattleSetup { attacker = Weavers(SpellBinding.Flux, SpellBinding.Strand), defender = Dummy(), field = field, seed = 5 }, Settings);
        Assert.Greater(carried.defender.dead + carried.defender.wounded, plain.defender.dead + plain.defender.wounded);
    }

    [Test]
    public void Fallout_WearsPureLightBeingsOnly()
    {
        var field = new Battlefield { age = 6, fallout = 1f };
        var pure = BattleResolver.Resolve(new BattleSetup { attacker = Dummy(), defender = Dummy(0.3f), field = field, seed = 1 }, Settings);
        Assert.Greater(pure.defender.dead + pure.defender.wounded, 0f, "a Pure Light being suffers the torn Loom");
        Assert.AreEqual(0f, pure.attacker.dead + pure.attacker.wounded, 1e-4f, "Humanity's Structure does not");
        var discordant = BattleResolver.Resolve(new BattleSetup { attacker = Dummy(), defender = Dummy(0.3f, HarmonicNiche.Discordant), field = field, seed = 1 }, Settings);
        Assert.AreEqual(0f, discordant.defender.dead + discordant.defender.wounded, 1e-4f, "Dead-zone lineages feed there");
        Assert.AreEqual(BattleOutcome.Stalemate, pure.outcome);
    }

    [Test]
    public void RiversAndHeights_FavorTheDefender()
    {
        var open = BattleResolver.Forecast(Setup("ember-host", "hearth-guard", 0, 2, new Battlefield { age = 0 }), Settings, 15);
        var hard = BattleResolver.Forecast(Setup("ember-host", "hearth-guard", 0, 2, new Battlefield { age = 0, riverCrossing = true, height = 0.2f }), Settings, 15);
        Assert.Less(hard.Balance, open.Balance);
        Assert.Less(hard.defenderLoss, open.defenderLoss);
    }

    [Test]
    public void Ambush_ShakesTheAttackerFromCover()
    {
        var setup = Setup("hearth-guard", "ember-host", 0, 4, new Battlefield { age = 0, concealed = true, cover = "deep-forest", ground = BattleGround.Forest });
        var r = BattleResolver.Resolve(setup, Settings);
        Assert.IsTrue(r.log.Any(l => l.Contains("ambush")));
    }

    private static BattleLegend Legend(string name, SpellBinding leitmotif = SpellBinding.Cindergale, float strain = 20f, params (LegendClass great, int stars)[] greats) => new BattleLegend
    {
        name = name, leitmotif = leitmotif, strain = strain, greats = greats.ToDictionary(g => g.great, g => g.stars),
    };

    [Test]
    public void Conductor_OrnamentsWaitForOrnamentalMagic()
    {
        var conductor = Legend("Iris", SpellBinding.Crystal, 20f, (LegendClass.Architect, 1));
        conductor.ornaments.Add(SpellBinding.Flux);
        BattleReport Fight(int age) => BattleResolver.Resolve(new BattleSetup
        {
            attacker = new BattleSide { name = "Iris", conductor = conductor, sections = { CombatSection.Raise(Settings.Section("shieldwall")) } },
            defender = CreatureCombat.Side(Salamander, 6),
            field = new Battlefield { age = age, magicAccess = 1f },
            seed = 9,
        }, Settings);
        var early = Fight(1);
        Assert.IsTrue(early.matchups.ContainsKey("Crystal on Cindergale"), "only the Soul Leitmotif before Ornamental Magic");
        Assert.IsFalse(early.matchups.ContainsKey("Flux on Cindergale"));
        var later = Fight(AgeCapabilities.FirstAge[AgeCapabilities.OrnamentalMagic]);
        Assert.IsTrue(later.matchups.ContainsKey("Flux on Cindergale"), "the Ornament's coverage once the Age allows it");
    }

    [Test]
    public void Commander_GreatsAndRealStateMatter()
    {
        var great = Legend("A", greats: (LegendClass.Vanguard, 3));
        var unattuned = Legend("B");
        BattleSetup With(BattleLegend c) => new BattleSetup
        {
            attacker = OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), Settings, 1, c),
            defender = OrchestralFormations.Raise(Settings.Template("tidebound-choir"), Settings, 1),
            field = new Battlefield { age = 1, magicAccess = 0.6f }, seed = 21,
        };
        var greatF = BattleResolver.Forecast(With(great), Settings, 15);
        var plainF = BattleResolver.Forecast(With(unattuned), Settings, 15);
        Assert.GreaterOrEqual(greatF.Balance, plainF.Balance);
        Assert.Less(greatF.attackerLoss, plainF.attackerLoss, "a 3★ Great Vanguard's stack strikes harder and wins cheaper");
        Assert.AreEqual("3★ Great Vanguard", great.Title);
        Assert.AreEqual(LegendGreats.UnattunedTitle, unattuned.Title);
        var t = CombatTuning.Default;
        Assert.Less(Legend("S", strain: 80f).BattleComposure(t), Legend("C").BattleComposure(t), "a Spiraling legend starts the battle with less");
        var surrendered = Legend("C", strain: 100f);
        var problems = new List<string>();
        Assert.IsNull(OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), Settings, 1, surrendered, problems: problems).conductor);
        Assert.IsTrue(problems.Any(p => p.Contains("cannot command")));
    }

    // ---- The two bars ----------------------------------------------------------------------------------------------

    private static BattleSide Line(string section, string name, float composure = -1f)
    {
        var sec = CombatSection.Raise(Settings.Section(section));
        if (composure >= 0f) sec.composure = composure;
        return new BattleSide { name = name, sections = { sec } };
    }

    private static BattleSetup Duel(BattleSide attacker, BattleSide defender, int seed = 13) =>
        new BattleSetup { attacker = attacker, defender = defender, field = new Battlefield { age = 1, magicAccess = 1f }, seed = seed };

    [Test]
    public void MindBreak_FightsOnButCannotGuardItself()
    {
        var steady = BattleResolver.Resolve(Duel(Line("warband", "Raiders"), Line("shieldwall", "Wall")), Settings);
        var broken = BattleResolver.Resolve(Duel(Line("warband", "Raiders"), Line("shieldwall", "Wall", composure: 0f)), Settings);
        Assert.AreEqual(1, broken.timeline[0].defender.mindBroken, "Composure at 0: a Mind Break from the start");
        Assert.Less(broken.timeline[1].defender.integrity, steady.timeline[1].defender.integrity, "its parries and armor fall: steel bites deeper");
        Assert.Greater(broken.timeline[1].attacker.integrity, steady.timeline[1].attacker.integrity, "its own blows land softer");
        Assert.Greater(broken.timeline[1].defender.integrity, 0f, "a Mind Break is not defeat: Integrity decides");
    }

    [Test]
    public void Composure_IsTheMagicalReserve()
    {
        var t = new FormationTemplate { name = "Choir", tempo = SpellTempo.Legato };
        t.front.Add(new FormationSlot { section = "shieldwall" });
        t.back.Add(new FormationSlot { section = "weaver-circle", binding = SpellBinding.Flux });
        var drained = OrchestralFormations.Raise(t, Settings, 1);
        drained.sections[1].composure = 0f;
        var r = BattleResolver.Resolve(Duel(drained, CreatureCombat.Side(Salamander, 10)), Settings);
        Assert.AreEqual(0, r.attacker.casts, "a Mind Broken caster has nothing to pay a spell with");
        var fresh = BattleResolver.Resolve(Duel(OrchestralFormations.Raise(t, Settings, 1), CreatureCombat.Side(Salamander, 10)), Settings);
        Assert.Greater(fresh.attacker.casts, 0);
        Assert.Less(fresh.timeline[1].attacker.composure, fresh.timeline[0].attacker.composure, "every spell is paid in Composure");
    }

    [Test]
    public void Integrity_DecidesTheBattle()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            var r = BattleResolver.Resolve(Setup("cinder-vanguard", "tidebound-choir", 1, seed), Settings);
            Assert.AreEqual(r.measures + 1, r.timeline.Count, "the line-up and every measure");
            if (r.winner == 0) continue;
            var loser = r.winner > 0 ? r.defender : r.attacker;
            var last = r.timeline[r.timeline.Count - 1];
            var bars = r.winner > 0 ? last.defender : last.attacker;
            Assert.IsTrue(loser.beaten || loser.withdrew);
            Assert.LessOrEqual(bars.IntegrityShare, CombatTuning.Default.integrityBreak + 1e-4f, "the beaten line's Integrity gave out");
        }
    }

    [Test]
    public void BrokenLines_ShakeTheirNeighbours()
    {
        var sides = BattleResolver.Resolve(Setup("ember-host", "hearth-guard", 0, 5), Settings);
        Assert.IsTrue(sides.log.Any(l => l.Contains("Mind Break") || l.Contains("cut down")), "Composure or Integrity gives out somewhere");
        Assert.Greater(sides.attacker.everMindBroken + sides.defender.everMindBroken + sides.attacker.destroyed + sides.defender.destroyed, 0);
    }

    // ---- Captives --------------------------------------------------------------------------------------------------

    private static BattleSide BrokenPack(float integrityShare)
    {
        var pack = CreatureCombat.Side(Wolf, 12);
        pack.withdrawAt = 0f;
        var sec = pack.sections[0];
        sec.integrity = sec.maxIntegrity * integrityShare;
        sec.composure = 0f;
        return pack;
    }

    [Test]
    public void Capture_TakesTheMindBrokenAndBleedingAlive()
    {
        var r = BattleResolver.Resolve(Duel(Line("shieldwall", "Hunters"), BrokenPack(0.35f)), Settings);
        Assert.AreEqual(1, r.captives.Count, "Mind Broken below 40% Integrity: subdued, not cut down");
        var c = r.captives[0];
        Assert.IsTrue(c.takenByAttacker);
        Assert.AreEqual("wolf", c.speciesId);
        Assert.Greater(c.individuals, 0);
        Assert.Less(c.individuals, 12);
        Assert.AreEqual(1, r.defender.captured);
        Assert.IsTrue(r.AttackerWon, "a line taken whole is a line beaten");
        Assert.AreEqual(c.individuals, r.TakenBy(true).Sum(x => x.individuals));

        var healthy = BattleResolver.Resolve(Duel(Line("shieldwall", "Hunters"), BrokenPack(0.8f)), Settings);
        Assert.IsFalse(healthy.captives.Any(x => x.measure == 1), "above 40% Integrity it still fights");

        var killers = Line("shieldwall", "Hunters");
        killers.takesCaptives = false;
        Assert.IsEmpty(BattleResolver.Resolve(Duel(killers, BrokenPack(0.35f)), Settings).captives, "a side that takes no captives cuts them down");
        Assert.IsFalse(CreatureCombat.Side(Wolf, 6).takesCaptives, "wild creatures take no captives");
    }

    // ---- Legends ---------------------------------------------------------------------------------------------------

    [Test]
    public void SectionLeader_LendsItsGreatsToItsSection()
    {
        var plain = BattleResolver.Resolve(Duel(Line("warband", "Raiders"), Line("shieldwall", "Wall")), Settings);
        var led = Line("warband", "Raiders");
        led.sections[0].leader = Legend("Kael", greats: (LegendClass.Vanguard, 3));
        var r = BattleResolver.Resolve(Duel(led, Line("shieldwall", "Wall")), Settings);
        Assert.Less(r.timeline[1].defender.integrity, plain.timeline[1].defender.integrity, "a 3★ Great Vanguard's section strikes harder");
        Assert.IsTrue(r.log.Any(l => l.Contains("Kael (3★ Great Vanguard) leads")));
    }

    [Test]
    public void Commander_HasItsOwnBattleComposure()
    {
        var fragile = new CombatSettings { tuning = new CombatTuning { legendComposure = 4f } };
        var frail = Legend("Wren");
        var setup = new BattleSetup
        {
            attacker = OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), fragile, 1, frail),
            defender = OrchestralFormations.Raise(Settings.Template("tidebound-choir"), fragile, 1),
            field = new Battlefield { age = 1, magicAccess = 1f }, seed = 4,
        };
        var r = BattleResolver.Resolve(setup, fragile);
        Assert.AreEqual(4f, r.attacker.conductorBefore, 1e-3f, "its battle bar, not its real Composure");
        Assert.IsTrue(r.attacker.conductorBroke);
        Assert.IsTrue(r.log.Any(l => l.Contains("suffers a Mind Break") && l.Contains("without a commander")));
        var fate = r.legends.Single(f => f.name == "Wren");
        Assert.IsTrue(fate.mindBroken);
        Assert.GreaterOrEqual(fate.strain, fragile.tuning.mindBreakStrain, "a Mind Break reaches its real Composure");
        Assert.IsTrue(LegendConditions.Has(fate.conditions, LegendConditions.Traumatized));
        Assert.AreEqual(20f, frail.strain, "the legend's soul is the caller's to update");
    }

    [Test]
    public void Legends_LeaveADoomedBattleAndGoMissing()
    {
        var commander = Legend("Aldric", greats: (LegendClass.Sovereign, 1));
        var leader = Legend("Mira");
        var victor = Legend("Kael", greats: (LegendClass.Vanguard, 2));
        var attacker = OrchestralFormations.Raise(Settings.Template("hearth-guard"), Settings, 1, commander);
        attacker.sections[0].leader = leader;
        var setup = new BattleSetup
        {
            attacker = attacker,
            defender = OrchestralFormations.Raise(Settings.Template("cinder-vanguard"), Settings, 1, victor),
            field = new Battlefield { age = 1, magicAccess = 0.6f }, seed = 2,
        };
        var r = BattleResolver.Resolve(setup, Settings);
        Assert.Less(r.winner, 0, "the hearth guard cannot take the vanguard's ground");
        var t = CombatTuning.Default;
        var lost = r.legends.Single(f => f.name == "Aldric");
        Assert.IsTrue(lost.missing, "a doomed side's commander retreats alone");
        Assert.AreEqual(t.missingSevenths, lost.missingSevenths);
        Assert.GreaterOrEqual(lost.strain, t.defeatStrain + t.missingStrain);
        Assert.IsTrue(LegendConditions.Has(lost.conditions, LegendConditions.Haunted));
        Assert.IsTrue(lost.fragments.Any(f => f.kind == FragmentKind.Acceptance));
        Assert.IsTrue(r.legends.Single(f => f.name == "Mira").missing, "and so does every section leader");
        var won = r.legends.Single(f => f.name == "Kael");
        Assert.IsTrue(won.won && !won.missing);
        Assert.IsTrue(won.fragments.Any(f => f.kind == FragmentKind.Defiance), "a victory pays Defiance: the path to Great Vanguard");
        Assert.IsTrue(r.log.Any(l => l.Contains("missing in action")));
    }

    [Test]
    public void ConductorChord_IsTheLargestTheAgePlaysReliably()
    {
        Assert.AreEqual(ChordTier.Unison, BattleResolver.ConductorTier(0, null ?? CombatTuning.Default), "Age 0 plays nothing reliably");
        Assert.AreEqual(ChordTier.Unison, BattleResolver.ConductorTier(1, CombatTuning.Default));
        Assert.AreEqual(ChordTier.Dyad, BattleResolver.ConductorTier(2, CombatTuning.Default));
        Assert.AreEqual(ChordTier.Triad, BattleResolver.ConductorTier(3, CombatTuning.Default));
    }

    // ---- Content ---------------------------------------------------------------------------------------------------

    [Test]
    public void RealBestiary_PureLightBeingsCarryABinding()
    {
        GameCatalog.InvalidateAll();
        var gen = GameCatalog.World.All.First().generation;
        foreach (var species in gen.species.Where(s => s != null))
        {
            bool pure = CreatureTaxonomy.IsPureLightBeing(species);
            Assert.AreEqual(pure, species.primaryBinding != SpellBinding.Unattuned, $"{species.id}: Pure Light beings carry a binding, ordinary animals none");
            Assert.LessOrEqual(species.secondaryBindings.Count, 2, species.id);
            Assert.IsFalse(species.secondaryBindings.Contains(species.primaryBinding), species.id);
        }
    }
}
