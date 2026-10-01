using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleSpatialStateTests
{
    private static CombatSection Section(FormationRow role = FormationRow.Front, int count = 1)
    {
        var section = new CombatSection
        {
            name = "Section", row = role, count = count, maxIntegrity = 100f, maxComposure = 100f,
            attack = 6f, defense = 6f, breakthrough = 6f,
        };
        section.Reset();
        return section;
    }

    private static BattleSetup Setup(int fronts = 1) => new BattleSetup
    {
        attacker = new BattleSide { name = "Attacker", sections = Enumerable.Range(0, fronts).Select(_ => Section()).ToList() },
        defender = new BattleSide { name = "Defender", sections = new List<CombatSection> { Section() } },
    };

    private static CombatSettings Settings() => new CombatSettings { tuning = new CombatTuning { maxMeasures = 1 } };
    private static BattleResolver.BattleRun Begin(BattleSetup setup) => BattleResolver.Begin(setup, Settings(), 1);

    [Test]
    public void Layout_HasSixNativePositionsPerSideAndFourNeutralHexes()
    {
        Assert.AreEqual(16, BattleHexLayout.Hexes.Count);
        CollectionAssert.AreEqual(new[] { 5, 6, 5 }, Enum.GetValues(typeof(BattleLane)).Cast<BattleLane>()
            .Select(lane => BattleHexLayout.Hexes.Count(h => h.Lane == lane)).ToArray());
        Assert.AreEqual(4, BattleHexLayout.Hexes.Count(h => h.Territory == BattleTerritory.Neutral));
        foreach (bool attacker in new[] { true, false })
        {
            Assert.AreEqual(6, BattleHexLayout.Native(attacker).Count());
            Assert.AreEqual(3, BattleHexLayout.Native(attacker, 1).Count());
            Assert.AreEqual(3, BattleHexLayout.Native(attacker, 2).Count());
        }
        Assert.AreEqual(1, BattleHexLayout.At(1).Rank);
        Assert.AreEqual(1, BattleHexLayout.At(3).Rank, "the enemy front rank is mirrored");
        Assert.AreEqual(2, BattleHexLayout.At(4).Rank);
    }

    [Test]
    public void Adjacency_IsSymmetricConnectedAndIncludesBothMiddleNeutralHexes()
    {
        Assert.IsTrue(BattleHexLayout.AreAdjacent(7, 8));
        Assert.IsTrue(BattleHexLayout.AreAdjacent(2, 7));
        Assert.IsTrue(BattleHexLayout.AreAdjacent(2, 8));
        Assert.IsFalse(BattleHexLayout.AreAdjacent(2, 13), "Top and Bottom do not share an edge");
        Assert.AreEqual(2, BattleHexLayout.Distance(2, 13));
        foreach (var hex in BattleHexLayout.Hexes)
        {
            Assert.IsFalse(BattleHexLayout.AreAdjacent(hex.Id, hex.Id));
            foreach (int neighbor in BattleHexLayout.Adjacent(hex.Id))
            {
                Assert.IsTrue(BattleHexLayout.AreAdjacent(neighbor, hex.Id));
                Assert.AreEqual(1, BattleHexLayout.Distance(hex.Id, neighbor));
            }
            foreach (var other in BattleHexLayout.Hexes)
                Assert.AreEqual(BattleHexLayout.Distance(hex.Id, other.Id), BattleHexLayout.Distance(other.Id, hex.Id));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleHexLayout.At(16));
    }

    [TestCase(1, BattleScale.EliteEngagement)]
    [TestCase(10, BattleScale.EliteEngagement)]
    [TestCase(11, BattleScale.MicroEngagement)]
    [TestCase(100, BattleScale.MicroEngagement)]
    [TestCase(101, BattleScale.FormationEngagement)]
    [TestCase(999, BattleScale.FormationEngagement)]
    [TestCase(1000, BattleScale.MediumArmy)]
    [TestCase(10000, BattleScale.MediumArmy)]
    [TestCase(10001, BattleScale.GrandArmy)]
    public void Scales_UseTheDocumentBoundaries(int size, BattleScale scale) => Assert.AreEqual(scale, BattleScaleRules.ForSize(size));

    [Test]
    public void Scale_CountsAConductorOnlyOnceAndRejectsNonpositiveInput()
    {
        var setup = Setup();
        setup.attacker.sections[0].leader = new BattleLegend { name = "Leader" };
        setup.attacker.conductor = new BattleLegend { name = "Leader" };
        Assert.AreEqual(1, setup.attacker.CombatantCount);
        setup.attacker.conductor.name = "Another conductor";
        Assert.AreEqual(2, setup.attacker.CombatantCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleScaleRules.ForSize(0));
    }

    [TestCase(BattleStance.Spearhead, BattleStance.Line)]
    [TestCase(BattleStance.Line, BattleStance.Crescent)]
    [TestCase(BattleStance.Crescent, BattleStance.Spearhead)]
    public void Stances_FollowTheDocumentTriangle(BattleStance stance, BattleStance other)
    {
        Assert.IsTrue(BattleStances.Counters(stance, other));
        Assert.IsFalse(BattleStances.Counters(other, stance));
        Assert.IsFalse(BattleStances.Counters(stance, stance));
    }

    [Test]
    public void FormationTemplate_CarriesItsDoctrineIntoTheSharedRun()
    {
        var settings = Settings();
        var template = new FormationTemplate
        {
            name = "Crescent", stance = BattleStance.Crescent,
            front = new List<FormationSlot> { new FormationSlot { section = "shieldwall" } },
        };
        var side = OrchestralFormations.Raise(template, settings, 1);
        Assert.AreEqual(BattleStance.Crescent, side.stance);
        var setup = Setup();
        setup.attacker = side;
        var run = Begin(setup);
        Assert.AreEqual(BattleStance.Crescent, run.Report.timeline[0].attacker.stance);
        Assert.AreEqual(1f, run.Report.timeline[0].attacker.StanceShare);
        Assert.IsFalse(run.Attacker.StanceBroken);
    }

    [Test]
    public void Deployment_UsesActualSectionsAndSeparatesRolesFromLanes()
    {
        var setup = Setup(3);
        setup.attacker.sections.Add(Section(FormationRow.Back));
        setup.attacker.sections.Add(Section(FormationRow.Support));
        var run = Begin(setup);
        Assert.AreSame(run.Attacker, run.Spatial.Attacker);
        Assert.AreSame(run.Defender, run.Spatial.Defender);
        foreach (var section in run.Attacker.sections)
        {
            Assert.IsTrue(BattleHexLayout.IsNative(section.battleHex, true));
            Assert.AreEqual(section.row == FormationRow.Front ? 1 : 2, BattleHexLayout.At(section.battleHex).Rank);
            Assert.AreSame(section, run.Spatial.Occupants(section.battleHex, true).First());
        }
        CollectionAssert.AreEquivalent(new[] { BattleLane.Top, BattleLane.Middle, BattleLane.Bottom },
            run.Attacker.sections.Where(s => s.row == FormationRow.Front).Select(s => BattleHexLayout.At(s.battleHex).Lane));
    }

    [Test]
    public void Blockers_ForceStackingWithoutPuttingUnitsOnBlockedGround()
    {
        var setup = Setup(7);
        setup.field.hexes.Add(new BattleHexTerrain { hex = 1, blocked = true });
        var run = Begin(setup);
        Assert.IsTrue(run.Attacker.sections.All(s => s.battleHex != 1));
        Assert.IsTrue(run.Attacker.sections.Select(s => s.battleHex).Any(hex => run.Spatial.IsStacked(hex, true)));
        Assert.AreEqual(7, run.Attacker.sections.Count, "crowding does not silently drop surplus sections");
        Assert.IsFalse(setup.field.hexes[0].river);
        run.Spatial.Terrain[1].river = true;
        Assert.IsFalse(setup.field.hexes[0].river, "runtime terrain belongs to the run");
    }

    [Test]
    public void InvalidAndDuplicateTerrainAndFullyBlockedDeploymentAreRejected()
    {
        var blocked = Setup();
        blocked.field.hexes = BattleHexLayout.Native(true).Select(h => new BattleHexTerrain { hex = h.Id, blocked = true }).ToList();
        Assert.Throws<ArgumentException>(() => Begin(blocked));
        var duplicate = Setup();
        duplicate.field.hexes.Add(new BattleHexTerrain { hex = 1 });
        duplicate.field.hexes.Add(new BattleHexTerrain { hex = 1 });
        Assert.Throws<ArgumentException>(() => Begin(duplicate));
        var invalid = Setup();
        invalid.attacker.sections[0].battleHex = 16;
        Assert.Throws<ArgumentOutOfRangeException>(() => Begin(invalid));
        invalid.attacker.sections[0].battleHex = 1;
        invalid.field.hexes.Add(new BattleHexTerrain { hex = 1, blocked = true });
        Assert.Throws<ArgumentException>(() => Begin(invalid));
    }

    [Test]
    public void Occupancy_PreservesExplicitPositionsAndExcludesElementsThatLeave()
    {
        var setup = Setup(2);
        setup.attacker.sections.ForEach(s => s.battleHex = 7);
        setup.defender.sections[0].battleHex = 7;
        var run = Begin(setup);
        Assert.IsTrue(run.Spatial.IsEngaged(7));
        Assert.IsTrue(run.Spatial.IsStacked(7, true));
        run.Attacker.sections[0].fled = true;
        Assert.IsFalse(run.Spatial.IsStacked(7, true));
        run.Attacker.sections[1].captured = true;
        Assert.IsFalse(run.Spatial.IsEngaged(7));
    }

    [Test]
    public void RiverCrossing_OccupiesAllFourNeutralHexesAndKeepsEachSidesFooting()
    {
        var setup = Setup();
        setup.field.riverCrossing = true;
        setup.field.ground = BattleGround.Hills;
        setup.field.attackerGround = BattleGround.Open;
        var run = Begin(setup);
        CollectionAssert.AreEquivalent(BattleHexLayout.Hexes.Where(h => h.Territory == BattleTerritory.Neutral).Select(h => h.Id),
            run.Spatial.Terrain.Where(t => t.river).Select(t => t.hex));
        Assert.AreEqual(BattleGround.Open, run.Spatial.Terrain[0].ground);
        Assert.AreEqual(BattleGround.Hills, run.Spatial.Terrain[4].ground);
    }

    [Test]
    public void Clone_PreservesConditionAndIsolatesMutableInputsWhileKeepingLegendAliases()
    {
        var setup = Setup();
        var section = setup.attacker.sections[0];
        section.integrity = 43f; section.composure = 12f; section.battleHex = 1;
        section.bonds = new Dictionary<string, int> { ["Leader"] = 2 };
        section.grounds.Add(new GroundModifier { ground = BattleGround.Open, attack = 2f });
        var leader = new BattleLegend { name = "Leader", composureTuning = new ComposureTuning(), grimoire = new List<string> { "learned" } };
        section.leader = setup.attacker.conductor = leader;
        setup.attacker.stance = BattleStance.Crescent;
        setup.attacker.stanceStability = 17f;
        setup.attacker.deck.Add(new DeckCard { voice = 0, legend = leader, card = new CombatCard { id = "test", effects = new List<CardEffect> { new CardEffect(CardOp.Strike, CardAim.Enemy, 1f) } } });
        setup.field.hexes.Add(new BattleHexTerrain { hex = 2, blocked = true });
        var copy = setup.Clone(42);
        Assert.AreEqual(42, copy.seed);
        Assert.AreEqual(43f, copy.attacker.sections[0].integrity);
        Assert.AreEqual(12f, copy.attacker.sections[0].composure);
        Assert.AreEqual(1, copy.attacker.sections[0].battleHex);
        Assert.AreEqual(17f, copy.attacker.stanceStability);
        Assert.AreEqual(BattleStance.Crescent, copy.attacker.stance);
        Assert.AreSame(copy.attacker.conductor, copy.attacker.sections[0].leader);
        Assert.AreSame(copy.attacker.conductor, copy.attacker.deck[0].legend);
        Assert.AreNotSame(leader, copy.attacker.conductor);
        copy.attacker.sections[0].bonds["Leader"] = 3;
        copy.attacker.sections[0].grounds[0].attack = 3f;
        copy.attacker.conductor.grimoire.Clear();
        copy.attacker.conductor.composureTuning.spiralingAt = 2f;
        copy.attacker.deck[0].card.effects[0].amount = 9f;
        copy.field.hexes[0].blocked = false;
        copy.field.dissonance = 1f;
        Assert.AreEqual(2, section.bonds["Leader"]);
        Assert.AreEqual(2f, section.grounds[0].attack);
        Assert.AreEqual(1, leader.grimoire.Count);
        Assert.AreEqual(70f, leader.composureTuning.spiralingAt);
        Assert.AreEqual(1f, setup.attacker.deck[0].card.effects[0].amount);
        Assert.IsTrue(setup.field.hexes[0].blocked);
        Assert.AreEqual(0f, setup.field.dissonance);
    }

    [Test]
    public void Forecast_LeavesWoundsComposurePositionAndStanceUntouched()
    {
        var setup = Setup();
        setup.attacker.sections[0].integrity = 43f;
        setup.attacker.sections[0].composure = 12f;
        setup.attacker.stanceStability = 17f;
        BattleResolver.Forecast(setup, Settings(), 3);
        Assert.AreEqual(-1, setup.attacker.sections[0].battleHex);
        Assert.AreEqual(43f, setup.attacker.sections[0].integrity);
        Assert.AreEqual(12f, setup.attacker.sections[0].composure);
        Assert.AreEqual(17f, setup.attacker.stanceStability);
        var run = Begin(setup.Clone(1));
        Assert.AreEqual(43f, run.Report.timeline[0].attacker.integrity, "forecast copies enter with the same wounds");
        Assert.AreEqual(12f, run.Report.timeline[0].attacker.composure);
        Assert.AreEqual(17f, run.Report.timeline[0].attacker.stanceStability);
        Assert.AreEqual(100f, run.Report.timeline[0].attacker.stanceStabilityMax);
    }

    [Test]
    public void ManualAndAuto_UseTheSameDeploymentAndStartingConditions()
    {
        var setup = Setup(4);
        setup.attacker.stance = BattleStance.Spearhead;
        setup.attacker.stanceStability = 25f;
        setup.attacker.sections[0].integrity = 50f;
        var automatic = setup.Clone(1);
        setup.attacker.manual = true;
        var run = Begin(setup);
        var report = BattleResolver.Resolve(automatic, Settings());
        CollectionAssert.AreEqual(run.Attacker.sections.Select(s => s.battleHex), report.timeline[0].positions.Where(p => p.attacker).Select(p => p.hex));
        Assert.AreEqual(run.Report.timeline[0].attacker.integrity, report.timeline[0].attacker.integrity);
        Assert.AreEqual(run.Report.timeline[0].attacker.stanceStability, report.timeline[0].attacker.stanceStability);
        Assert.AreEqual(BattleScale.EliteEngagement, run.Spatial.Scale(true));
    }

    [Test]
    public void HarmonicDefaults_MatchTheWrittenMultipliersExactly()
    {
        Assert.AreEqual(1.5f, HarmonicCircle.Multiplier(SpellBinding.Flux, SpellBinding.Cindergale, null));
        Assert.AreEqual(0.5f, HarmonicCircle.Multiplier(SpellBinding.Cindergale, SpellBinding.Flux, null));
        Assert.AreEqual(1.5f, HarmonicCircle.Multiplier(SpellBinding.Luminance, SpellBinding.Void, null));
        Assert.AreEqual(1.5f, HarmonicCircle.Multiplier(SpellBinding.Void, SpellBinding.Luminance, null));
    }
}
