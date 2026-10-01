using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleCompositionTests
{
    private static CombatSection Section(int count = 1, FormationRow role = FormationRow.Front)
    {
        var section = new CombatSection { name = "Company", row = role, count = count, maxIntegrity = 100f, maxComposure = 50f };
        section.Reset(); return section;
    }
    private static BattleSetup Setup(BattleStance stance = BattleStance.Line) => new BattleSetup
    {
        attacker = new BattleSide { name = "Host", stance = stance, sections = Enumerable.Range(0, 6).Select(_ => Section()).ToList() },
        defender = new BattleSide { name = "Enemy", sections = new List<CombatSection> { Section() } },
    };
    private static BattleResolver.BattleRun Begin(BattleSetup setup, CombatSettings settings = null) =>
        BattleResolver.Begin(setup, settings ?? new CombatSettings { tuning = new CombatTuning { maxMeasures = 1 } }, 0);

    [TestCase(BattleStance.Spearhead, 1)]
    [TestCase(BattleStance.Line, 3)]
    [TestCase(BattleStance.Crescent, 2)]
    public void InitialAdvance_PreservesConcentrationAndUnusedPositions(BattleStance stance, int occupied)
    {
        var run = Begin(Setup(stance));
        Assert.AreEqual(occupied, run.Attacker.sections.Select(s => s.battleHex).Distinct().Count());
        Assert.IsTrue(run.Attacker.sections.All(s => BattleHexLayout.At(s.battleHex).Rank == 1));
        if (stance == BattleStance.Crescent) Assert.IsTrue(run.Attacker.sections.All(s => BattleHexLayout.At(s.battleHex).Lane != BattleLane.Middle));
        if (stance == BattleStance.Spearhead) Assert.IsTrue(run.Spatial.IsStacked(6, true));
    }

    [Test]
    public void AuthoredDoctrine_IsMirroredAndItsBlockedWingStacks()
    {
        var settings = new CombatSettings();
        var doctrine = new BattleDoctrineSpec { id = "shock", name = "Shock", stance = BattleStance.Spearhead,
            slots = new List<BattleDeploymentSlot> { new BattleDeploymentSlot { role = FormationRow.Front, lane = BattleLane.Top, rank = 1 } } };
        settings.doctrines.Add(doctrine);
        var setup = Setup(); setup.attacker.doctrine = setup.defender.doctrine = doctrine.id;
        setup.field.hexes.Add(new BattleHexTerrain { hex = 1, blocked = true });
        var run = Begin(setup, settings);
        Assert.IsTrue(run.Attacker.sections.All(s => s.battleHex == 0), "the blocked wing uses its adjacent rear position");
        Assert.AreEqual(3, run.Defender.sections[0].battleHex);
        Assert.IsTrue(run.Spatial.IsStacked(0, true));
    }

    [Test]
    public void ExplicitTemplatePosition_OverridesDoctrineWithoutMovingDeployedSections()
    {
        var settings = new CombatSettings();
        var template = new FormationTemplate { stance = BattleStance.Spearhead,
            front = new List<FormationSlot> { new FormationSlot { section = "shieldwall", deployment = new BattleDeploymentSlot { lane = BattleLane.Bottom, rank = 2 } } } };
        var side = OrchestralFormations.Raise(template, settings, 1);
        var setup = Setup(); setup.attacker = side;
        Assert.AreEqual(11, Begin(setup).Attacker.sections[0].battleHex);
        side.sections[0].battleHex = 2;
        Assert.AreEqual(2, Begin(setup).Attacker.sections[0].battleHex);
    }

    [Test]
    public void SingleFord_HasOneContinuousCrossingAndBlocksNeutralWings()
    {
        var setup = Setup(BattleStance.Crescent); setup.field.riverCrossing = true;
        BattleTerrainLogic.Project(setup.field, null, null, null, new CombatSettings());
        var run = Begin(setup);
        CollectionAssert.AreEquivalent(new[] { 7, 8 }, run.Spatial.Terrain.Where(t => t.ford && !t.blocked).Select(t => t.hex));
        Assert.IsTrue(run.Spatial.Terrain[2].blocked && run.Spatial.Terrain[13].blocked);
        Assert.IsTrue(BattleHexLayout.AreAdjacent(7, 8));
        Assert.IsTrue(run.Spatial.Terrain.Where(t => t.river).All(t => BattleHexLayout.At(t.hex).Territory == BattleTerritory.Neutral));
    }

    [Test]
    public void Bridge_RemainsPhysicalChokepointWithoutFordFlag()
    {
        var field = new Battlefield();
        BattleTerrainLogic.Project(field, null, null, null, new CombatSettings(), true);
        Assert.IsFalse(field.riverCrossing);
        Assert.AreEqual(2, field.hexes.Count(t => t.bridge && !t.blocked));
        Assert.IsFalse(field.hexes.Any(t => t.ford));
    }

    [Test]
    public void WorldTerrain_ProjectsNativeBlockersAuthoredFeaturesAndLocalLoom()
    {
        var gen = new WorldGenSettings { terrains = new List<TerrainSpec> { new TerrainSpec { id = "forest" } },
            features = new List<FeatureSpec> { new FeatureSpec { id = "keep", combatHexes = new List<BattleHexTerrain>
            {
                new BattleHexTerrain { hex = 2, wall = true, blocked = true },
                new BattleHexTerrain { hex = 3, ground = BattleGround.Forest, forest = true, snow = true, highGround = true, harmonicChannel = true },
            } } } };
        var attacker = new WorldTile { terrain = "forest", microBlockedMask = 1 << 1, coherence = 0.2f, dissonance = 0.7f, leylines = 1 };
        var defender = new WorldTile { terrain = "forest", feature = "keep", coherence = 0.9f };
        var field = new Battlefield { ground = BattleGround.Forest };
        BattleTerrainLogic.Project(field, attacker, defender, gen, new CombatSettings());
        Assert.IsTrue(field.hexes[0].blocked);
        Assert.IsTrue(field.hexes[2].wall && field.hexes[2].blocked);
        Assert.IsTrue(field.hexes[3].snow && field.hexes[3].forest && field.hexes[3].highGround && field.hexes[3].harmonicChannel);
        Assert.AreEqual(0.9f, field.hexes[3].coherence);
        Assert.AreEqual(0.7f, field.hexes[6].dissonance);
        Assert.IsTrue(field.hexes[6].leyline);
        Assert.AreEqual(0, BattleTerrainLogic.Mirror(4));
    }

    [Test]
    public void Party_TwoPeopleRemainIndividualAndDirectorHasOneIdentity()
    {
        int calls = 0;
        var side = WorldBattles.PartySide(new WorldUnit { name = "Party", attrition = 25f, nerveLost = 0.2f }, null,
            new[] { "A", "B" }, name => { calls++; return new BattleLegend { name = name, leitmotif = SpellBinding.Flux }; });
        Assert.AreEqual(2, side.CombatantCount); Assert.AreEqual(2, calls);
        Assert.AreSame(side.sections[0].leader, side.conductor);
        Assert.IsTrue(side.sections.All(s => s.count == 1 && s.abstraction == BattleAbstraction.Individual));
        Assert.AreEqual(SpellBinding.Flux, side.sections[0].primary);
        Assert.AreEqual(0.75f, side.sections[0].IntegrityShare, 0.001f);
    }

    [TestCase(2, BattleScale.EliteEngagement, BattleAbstraction.Individual)]
    [TestCase(50, BattleScale.MicroEngagement, BattleAbstraction.Squad)]
    [TestCase(500, BattleScale.FormationEngagement, BattleAbstraction.Company)]
    [TestCase(5000, BattleScale.MediumArmy, BattleAbstraction.Battalion)]
    [TestCase(20000, BattleScale.GrandArmy, BattleAbstraction.ArmySection)]
    public void PopulationScales_AggregateOrdinaryBodiesAndKeepNamedElitesIndividual(int count, BattleScale scale, BattleAbstraction abstraction)
    {
        var roster = new ArmyRoster();
        roster.units.Add(new ConscriptUnit { id = "u", specId = "grave-warden", name = "Company", people = count, raisedPeople = count, integrity = 50f, leader = "Leader" });
        var stack = roster.FormStack("Host", "Conductor", new[] { "u" }, stance: BattleStance.Crescent);
        Assert.IsTrue(roster.AssignCore(stack.id, "Guard", BattleEliteRole.Guard));
        Assert.IsTrue(roster.AssignCore(stack.id, "Mender", BattleEliteRole.Specialist));
        var side = roster.Muster(stack.id, new CombatSettings(), name => new BattleLegend { name = name });
        Assert.AreEqual(count == 2 ? 6 : 5, side.sections.Count, "Elite scale splits bodies; large scale keeps a bounded population group");
        Assert.AreEqual(count + 4, side.CombatantCount);
        Assert.AreEqual(scale, BattleScaleRules.ForSize(side.CombatantCount));
        Assert.AreEqual(abstraction, side.sections[0].abstraction);
        Assert.AreEqual(50f, side.sections.Where(s => s.unitId == "u").Sum(s => s.integrity));
        Assert.AreSame(side.sections[0].leader, side.sections.Single(s => s.name == "Leader").leader);
        Assert.AreSame(side.conductor, side.sections.Single(s => s.eliteRole == BattleEliteRole.Conductor).leader);
        Assert.IsTrue(side.sections.Where(s => s.eliteRole != BattleEliteRole.None).All(s => s.count == 1 && s.abstraction == BattleAbstraction.Individual && s.unitId == null));
    }

    [Test]
    public void EliteCoreAssignment_ReservesOnePostAndMusterDoesNotDuplicatePieces()
    {
        var roster = new ArmyRoster(); var a = roster.FormStack("A", "Commander", Array.Empty<string>());
        var b = roster.FormStack("B", null, Array.Empty<string>());
        Assert.IsTrue(roster.AssignCore(a.id, "Guard", BattleEliteRole.Guard));
        Assert.IsFalse(roster.AssignCore(b.id, "Guard", BattleEliteRole.Guard));
        Assert.IsFalse(roster.Command(b.id, "Guard"));
        var settings = new CombatSettings(); var side = roster.Muster(a.id, settings, name => new BattleLegend { name = name });
        BattleCompositionLogic.AddEliteCore(side, settings);
        Assert.AreEqual(2, side.sections.Count);
        Assert.AreEqual(BattleEliteRole.Guard, side.sections.Single(s => s.name == "Guard").eliteRole);
    }

    [Test]
    public void SmallCompany_AftermathReconcilesBodiesAndAwardsMeritOnce()
    {
        var settings = new CombatSettings(); var roster = new ArmyRoster(); var bank = new Bank();
        roster.units.Add(new ConscriptUnit { id = "u", specId = "grave-warden", name = "Company", people = 2, raisedPeople = 2, integrity = 100f });
        var stack = roster.FormStack("Host", null, new[] { "u" });
        var side = roster.Muster(stack.id, settings);
        Assert.AreEqual(2, side.sections.Count); Assert.AreEqual(settings.Section("grave-warden").integrity, side.MaxIntegrity);
        side.sections[0].destroyed = true; side.sections[0].integrity = 0f;
        var report = new BattleReport { winner = 1, attacker = new SideResult { name = "Host" }, defender = new SideResult { name = "Enemy" } };
        roster.Record(stack.id, side, report, true, bank, settings);
        var unit = roster.Unit("u"); Assert.AreEqual(1, unit.battles); Assert.AreEqual(1, unit.people);
        Assert.AreEqual(50f, unit.integrity); Assert.AreEqual(1, bank.fallen);
    }

    [Test]
    public void SmallCompany_PartiallyCapturedBodiesDoNotBecomeDeaths()
    {
        var settings = new CombatSettings(); var roster = new ArmyRoster(); var bank = new Bank();
        roster.units.Add(new ConscriptUnit { id = "u", specId = "grave-warden", name = "Company", people = 2, raisedPeople = 2, integrity = 100f });
        var stack = roster.FormStack("Host", null, new[] { "u" }); var side = roster.Muster(stack.id, settings);
        side.sections[0].captured = true;
        roster.Record(stack.id, side, new BattleReport { winner = 1, attacker = new SideResult(), defender = new SideResult() }, true, bank, settings);
        Assert.AreEqual(1, roster.Unit("u").people); Assert.AreEqual(0, bank.fallen);
    }

    [Test]
    public void ArmyGrimoires_AreVoicedByTheIndividualEliteAndCloneAliasesSurvive()
    {
        var roster = new ArmyRoster(); roster.units.Add(new ConscriptUnit { id = "u", specId = "grave-warden", name = "Company", people = 500, integrity = 70f, leader = "L" });
        var stack = roster.FormStack("Host", "C", new[] { "u" }); var settings = new CombatSettings();
        var side = roster.Muster(stack.id, settings, name => new BattleLegend { name = name, leitmotif = SpellBinding.Flux });
        SymphonyDecks.Score(side, new DeckSources { age = 1 });
        var personal = side.deck.Where(d => d.source == "L's grimoire").ToList(); Assert.IsNotEmpty(personal);
        Assert.IsTrue(personal.All(d => side.sections[d.voice].eliteRole == BattleEliteRole.Legend));
        var copy = side.Clone(); var elite = copy.sections.Single(s => s.name == "L");
        Assert.AreSame(copy.sections[0].leader, elite.leader);
        Assert.AreSame(elite.leader, copy.deck.First(d => d.source == "L's grimoire").legend);
        Assert.AreEqual(70f, copy.sections[0].integrity);
    }

    [Test]
    public void Creature_EliteBodiesAreIndividualAndGrandSwarmIsBounded()
    {
        var species = new SpeciesSpec { id = "wolf", name = "Wolf", size = CreatureSize.Medium, structure = 0.75f };
        Assert.AreEqual(10, CreatureCombat.Sections(species, 10).Count);
        var army = CreatureCombat.Sections(species, 20000);
        Assert.LessOrEqual(army.Count, 8); Assert.AreEqual(20000, army.Sum(s => s.count));
    }

    [Test]
    public void MacroInputs_CaptureAllSevenRealSourcesAndCloneIndependently()
    {
        var side = Setup().attacker; var legend = new BattleLegend { name = "A", strain = 40f, leitmotif = SpellBinding.Flux };
        side.conductor = legend; side.sections[0].leader = legend;
        side.sections[0].integrity = 60f; side.sections[0].bonds = new Dictionary<string, int> { ["A"] = 2 };
        side.deck.Add(new DeckCard { card = new CombatCard { name = "An art" }, source = "Learned arts" });
        var score = BattleCompositionLogic.Describe(side, new Battlefield { dissonance = 0.6f }, new CombatSettings(),
            new WorldUnit { supplies = 2f, fatigue = 45f }, new UnitSpec { supplyCapacity = 10f }, new[] { "War civic" },
            name => new[] { new LegendRelationship { other = "A", affection = 65, thread = "shared battle" } });
        CollectionAssert.AreEquivalent(Enum.GetValues(typeof(BattleScoreInput)), score.facts.Select(f => f.input).Distinct());
        Assert.IsTrue(score.facts.Any(f => f.value.Contains("Supply 2/10")));
        Assert.IsTrue(score.facts.Any(f => f.value.Contains("Integrity 60/100")));
        Assert.IsTrue(score.facts.Any(f => f.value.Contains("affection 65")));
        side.scoreInputs = score; var copy = side.Clone(); copy.scoreInputs.facts[0].value = "Changed";
        Assert.AreNotEqual("Changed", side.scoreInputs.facts[0].value);
    }

    [Test]
    public void Deck_UsesUnlockedTrainingPurchasedGearDoctrineAndActiveCivicSources()
    {
        var symphony = new SymphonySettings(); var side = new BattleSide { sections = new List<CombatSection> { Section() } };
        var section = side.sections[0]; section.specId = "shieldwall"; section.trainingTechnology = "Training";
        section.equipment = "shield"; section.equipmentCards.Add(SymphonyCards.Endure);
        var sources = new DeckSources { symphony = symphony, hasTechnology = _ => false,
            doctrine = new BattleDoctrineSpec { name = "Special drill", technology = "Drill", cards = new List<string> { SymphonyCards.Struggle } },
            civicCards = new List<(string, CombatCard)> { ("War civic", symphony.Card(SymphonyCards.Endure)) } };
        var deck = SymphonyDecks.Build(side, sources);
        Assert.IsFalse(deck.Any(d => d.source == section.name || d.source == "Special drill"));
        Assert.IsTrue(deck.Any(d => d.source == "shield")); Assert.IsTrue(deck.Any(d => d.source == "War civic"));
        sources.hasTechnology = _ => true; deck = SymphonyDecks.Build(side, sources);
        Assert.IsTrue(deck.Any(d => d.source == section.name)); Assert.IsTrue(deck.Any(d => d.source == "Special drill"));
    }

    [Test]
    public void LearnedCardLookup_CannotFallBackToAnUnownedLibraryCard()
    {
        var legend = new BattleLegend { grimoire = new List<string> { SymphonyCards.Struggle } };
        Assert.IsFalse(LegendGrimoires.Cards(legend, 1, new SymphonySettings(), _ => null).Any(c => c.card.id == SymphonyCards.Struggle));
    }

    [Test]
    public void PartyTraining_DoesNotRequireAnUnlockedEquipmentKit()
    {
        var side = new BattleSide { sections = new List<CombatSection> { Section() } };
        var deck = SymphonyDecks.Build(side, new DeckSources { party = true, hasTechnology = _ => false });
        Assert.AreEqual(5, deck.Count(d => d.source == "the road"));
        Assert.IsFalse(deck.Any(d => d.source == "the fallback"));
    }

    [Test]
    public void AuthoredLocalLoom_DoesNotTurnSacredGroundIntoAChannelImplicitly()
    {
        var gen = new WorldGenSettings { terrains = new List<TerrainSpec> { new TerrainSpec { id = "plains", combatHexes = new List<BattleHexTerrain>
            { new BattleHexTerrain { hex = 3, overrideLoom = true, coherence = 0.2f, dissonance = 0.8f, harmonicChannel = true } } } } };
        var tile = new WorldTile { terrain = "plains", sacred = true, coherence = 0.9f };
        var field = new Battlefield(); BattleTerrainLogic.Project(field, null, tile, gen, new CombatSettings());
        Assert.AreEqual(0.8f, field.hexes[3].dissonance); Assert.AreEqual(0.2f, field.hexes[3].coherence);
        Assert.IsTrue(field.hexes[3].harmonicChannel); Assert.IsFalse(field.hexes[4].harmonicChannel);
    }

    [Test]
    public void GearProcurement_ChecksUnlocksFactoryTierAndCombinedCostBeforeSpending()
    {
        var unit = new ConscriptUnit { specId = "shieldwall" }; var bank = new Bank();
        var gear = new BattleEquipmentSpec { id = "shield", technology = "Forge", productionUnit = "Factory", productionTier = 2,
            armor = 3f, cards = new List<string> { SymphonyCards.Endure }, cost = new List<ResourceAmount>
            { new ResourceAmount { resource = "Metal", amount = 6f }, new ResourceAmount { resource = "Metal", amount = 6f } } };
        Assert.IsFalse(BattleEquipmentLogic.Equip(unit, gear, bank, _ => 3));
        bank.unlocked = true; Assert.IsFalse(BattleEquipmentLogic.Equip(unit, gear, bank, _ => 1));
        bank.money = 10; Assert.IsFalse(BattleEquipmentLogic.Equip(unit, gear, bank, _ => 3)); Assert.AreEqual(10, bank.money);
        bank.money = 20; Assert.IsTrue(BattleEquipmentLogic.Equip(unit, gear, bank, _ => 3)); Assert.AreEqual(8, bank.money);
        Assert.AreEqual(3, unit.equipmentTier);
        var section = Section(); BattleEquipmentLogic.Apply(section, gear, unit.equipmentTier);
        Assert.AreEqual(3f, section.armor); CollectionAssert.Contains(section.equipmentCards, SymphonyCards.Endure);
        Assert.IsFalse(BattleEquipmentLogic.Equip(unit, gear, bank, _ => 3)); Assert.AreEqual(8, bank.money);
    }

    [Test]
    public void SaveRoundTrip_PreservesNewAssignmentsAndLoadsOldRosterWithLineDefaults()
    {
        var roster = new ArmyRoster(); roster.units.Add(new ConscriptUnit { id = "u", equipment = "shield", equipmentTier = 3 });
        var stack = roster.FormStack("Host", null, new[] { "u" }, stance: BattleStance.Spearhead);
        roster.SetDoctrine(stack.id, new CombatSettings().Doctrine("spearhead")); roster.AssignCore(stack.id, "Guard", BattleEliteRole.Guard);
        var record = SaveStateCodec.Write(roster, typeof(ArmyRoster));
        var copy = (ArmyRoster)SaveStateCodec.Read(record, typeof(ArmyRoster));
        Assert.AreEqual(BattleStance.Spearhead, copy.stacks[0].stance); Assert.AreEqual("spearhead", copy.stacks[0].doctrine);
        Assert.AreEqual("Guard", copy.stacks[0].core[0].legend); Assert.AreEqual(3, copy.units[0].equipmentTier);
        foreach (var node in record.children.Single(c => c.name == "stacks").children) node.children.RemoveAll(c => c.name == "stance" || c.name == "doctrine" || c.name == "core");
        foreach (var node in record.children.Single(c => c.name == "units").children) node.children.RemoveAll(c => c.name == "equipment" || c.name == "equipmentTier");
        copy = (ArmyRoster)SaveStateCodec.Read(record, typeof(ArmyRoster));
        Assert.AreEqual(BattleStance.Line, copy.stacks[0].stance); Assert.IsEmpty(copy.stacks[0].core); Assert.IsNull(copy.units[0].equipment);
    }

    [Test]
    public void PreviewAndManualAutoSetup_ShareDeploymentWithoutMutatingTheOriginalPreviewSource()
    {
        var setup = Setup(BattleStance.Crescent); var before = setup.Clone(9);
        var preview = BattlePreview.Of(setup, runs: 0);
        Assert.IsTrue(setup.attacker.sections.All(s => s.battleHex == -1));
        var manual = Begin(setup.Clone(9));
        var automatic = BattleResolver.Resolve(before, new CombatSettings { tuning = new CombatTuning { maxMeasures = 1 } });
        CollectionAssert.AreEqual(manual.Attacker.sections.Select(s => s.battleHex), automatic.timeline[0].positions.Where(p => p.attacker).Select(p => p.hex));
        CollectionAssert.AreEqual(preview.attacker.deployment.Select(d => d.hex), manual.Attacker.sections.Select(s => s.battleHex));
        Assert.AreEqual(7, preview.attacker.scoreInputs.facts.Select(f => f.input).Distinct().Count());
    }

    private sealed class Bank : IConscriptionBank
    {
        public bool unlocked; public float money = 20; public int fallen;
        public int People => 100; public int Age => 1; public bool Has(string technology) => unlocked;
        public float Amount(string resource) => money; public void Spend(string resource, float amount) => money -= amount;
        public void Enlist(int people, string company) { } public void Discharge(int people, string company) { }
        public void Fallen(int people, string cause) { fallen += people; } public void AwardFragments(string legend, List<FragmentAward> reward, string deed) { }
        public void AwardEraScore(int points, string reason) { } public void ShareBattle(IEnumerable<string> legends, string key, string memory, bool wound) { }
        public void RequestName(ConscriptUnit unit, string legend) { }
    }
}
