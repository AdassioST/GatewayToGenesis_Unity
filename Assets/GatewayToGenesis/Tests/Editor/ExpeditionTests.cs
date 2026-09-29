using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Expeditions of legends (<see cref="Expeditions"/>): slots, the party and who directs it, the unit it walks as, the
/// road's hardship on its legends' Composure, and the mishaps that strike them when things go bad. Pure, so they also
/// run outside Unity; the world and the scene are checked by ContentTests and ExpeditionPlayTests.
/// </summary>
public class ExpeditionTests
{
    // One neutral party shape (changes nothing), so the tests below read the base rules; party size has its own tests.
    private static ExpeditionSettings Settings() => new ExpeditionSettings { shapes = new List<PartyShape> { new PartyShape { name = "Neutral" } } };

    // The proposed shapes: Solo, Duo, Trio, Company.
    private static ExpeditionSettings Shaped() => new ExpeditionSettings();

    private static readonly UnitSpec PerLegend = new UnitSpec
    {
        id = "expedition", name = "Expedition", role = UnitRole.Expedition, stamina = 6f, sight = 5, supplyCapacity = 12f, supplyUsePerSeventh = 1f,
        forageRationsPerSeventh = 1.2f, campRecoveryPerSeventh = 25f, rewardMultiplier = 1f, wearMultiplier = 1f,
        abilities = UnitAbility.Survey | UnitAbility.SurveyMeso | UnitAbility.Forage | UnitAbility.AutoExplore,
    };

    private static WorldUnit Party(string director, params string[] companions) =>
        new WorldUnit { id = 3, leader = director, companions = companions.ToList(), supplies = 20f };

    private static readonly UnitSurroundings Wild = new UnitSurroundings { weather = 1f };
    private static readonly UnitSurroundings Home = new UnitSurroundings { weather = 1f, held = true, settlement = true };

    private static PartyMember Member(string name, ComposureState state = ComposureState.Clouded, float strain = 20f, bool director = false) =>
        new PartyMember { name = name, state = state, strain = strain, director = director };

    // ===== SLOTS AND THE PARTY =====

    [Test]
    public void Slots_GrowWithGovernmentCapacityAndHollowWatchposts()
    {
        var x = Settings();
        Assert.AreEqual(3, Expeditions.Slots(x, 1, 0), "one, and two for the Capital's first point of Government Capacity");
        Assert.AreEqual(5, Expeditions.Slots(x, 2, 0), "a Capital improvement adds two");
        Assert.AreEqual(7, Expeditions.Slots(x, 2, 2), "each Hollow Watchpost one");
        Assert.AreEqual(0, Expeditions.Slots(new ExpeditionSettings { baseSlots = -5 }, 0, 0), "never below none");
    }

    [Test]
    public void Slots_EveryLegendInTheFieldTakesOne()
    {
        var parties = new[] { Party("Vaelia", "Orphael", "Sephira"), Party("Luminaire"), new WorldUnit { leader = "Amadea", companions = new List<string> { null, "", "Artus" } } };
        CollectionAssert.AreEqual(new[] { "Vaelia", "Orphael", "Sephira" }, Expeditions.Members(parties[0]).ToArray(), "the Director first");
        Assert.AreEqual(2, Expeditions.PartySize(parties[2]), "empty names are no one");
        Assert.AreEqual(6, Expeditions.SlotsUsed(parties));
        Assert.IsTrue(Expeditions.IsMember(parties[0], "orphael"));
        Assert.IsFalse(Expeditions.IsMember(parties[1], "Orphael"));
    }

    [Test]
    public void Party_HoldsAtMostFourAndNeedsAFreeSlot()
    {
        var x = Settings();
        Assert.IsNull(Expeditions.WhyNotAddLegend(0, 1, x));
        Assert.IsNull(Expeditions.WhyNotAddLegend(3, 2, x));
        StringAssert.Contains("at most 4", Expeditions.WhyNotAddLegend(4, 5, x));
        StringAssert.Contains("slot", Expeditions.WhyNotAddLegend(1, 0, x));
    }

    [Test]
    public void Party_TheFirstCompanionDirectsWhenTheDirectorGoes()
    {
        var unit = Party("Vaelia", "Orphael", "Sephira");
        Assert.IsTrue(Expeditions.Remove(unit, "Vaelia"));
        Assert.AreEqual("Orphael", unit.leader);
        CollectionAssert.AreEqual(new[] { "Sephira" }, unit.companions);
        Assert.IsTrue(Expeditions.Remove(unit, "sephira"));
        Assert.IsTrue(Expeditions.Remove(unit, "Orphael"));
        Assert.IsNull(unit.leader, "the last legend gone, no one directs");
        Assert.AreEqual(0, Expeditions.PartySize(unit));
        Assert.IsFalse(Expeditions.Remove(unit, "Nobody"));
    }

    [Test]
    public void Party_ACompanionCanTakeTheLead()
    {
        var unit = Party("Vaelia", "Orphael", "Sephira");
        Assert.IsTrue(Expeditions.MakeDirector(unit, "Sephira"));
        Assert.AreEqual("Sephira", unit.leader);
        CollectionAssert.AreEqual(new[] { "Vaelia", "Orphael" }, unit.companions, "the old Director steps back among the companions");
        Assert.IsFalse(Expeditions.MakeDirector(unit, "Luminaire"), "only a companion");
    }

    // ===== THE UNIT IT WALKS AS =====

    [Test]
    public void Effective_EveryLegendCarriesEatsAndForagesForItself()
    {
        var x = Settings();
        var solo = Expeditions.Effective(PerLegend, Party("Vaelia"), null, x);
        var four = Expeditions.Effective(PerLegend, Party("Vaelia", "Orphael", "Sephira", "Luminaire"), null, x);
        Assert.AreEqual(12f, solo.supplyCapacity, 1e-4);
        Assert.AreEqual(48f, four.supplyCapacity, 1e-4);
        Assert.AreEqual(4f, four.supplyUsePerSeventh, 1e-4);
        Assert.AreEqual(4.8f, four.forageRationsPerSeventh, 1e-4);
        Assert.AreEqual(6f, four.stamina, 1e-4, "the party walks at one pace");
        Assert.AreEqual(1f + 3 * x.companionRewards, four.rewardMultiplier, 1e-4, "more hands bring more back");
        Assert.AreEqual(12f, PerLegend.supplyCapacity, "the per-legend unit is left as it was");
        Assert.IsFalse(WorldUnits.Can(four, UnitAbility.Settle));
    }

    [Test]
    public void Effective_SettlersSlowThePartyEatAShareAndLetItFound()
    {
        var x = Settings();
        var unit = Party("Vaelia");
        unit.settlers = 5;
        var spec = Expeditions.Effective(PerLegend, unit, null, x);
        Assert.AreEqual(12f * (1f + 5 * x.settlerWeight), spec.supplyCapacity, 1e-4);
        Assert.AreEqual(6f * x.settlerPace, spec.stamina, 1e-4);
        Assert.IsTrue(WorldUnits.Can(spec, UnitAbility.Settle));
    }

    [TestCase("Luminance")]
    [TestCase("Cindergale")]
    [TestCase("Crystal")]
    [TestCase("Void")]
    [TestCase("Strand")]
    [TestCase("Flux")]
    [TestCase("Resonance")]
    public void Effective_TheDirectorsSoulLeitmotifGivesOneStrength(string binding)
    {
        var x = Settings();
        var plain = Expeditions.Effective(PerLegend, Party("Vaelia"), null, x);
        var led = Expeditions.Effective(PerLegend, Party("Vaelia"), binding, x);
        Assert.IsNotNull(Expeditions.Strength(binding, x), "the card names it");
        switch (binding)
        {
            case "Luminance": Assert.AreEqual(plain.sight + x.luminanceSight, led.sight); break;
            case "Cindergale": Assert.AreEqual(plain.stamina * x.cindergalePace, led.stamina, 1e-4); break;
            case "Crystal": Assert.AreEqual(x.crystalWear, led.wearMultiplier, 1e-4); break;
            case "Void": Assert.AreEqual(plain.supplyUsePerSeventh * x.voidRations, led.supplyUsePerSeventh, 1e-4); break;
            case "Strand": Assert.AreEqual(plain.rewardMultiplier * x.strandRewards, led.rewardMultiplier, 1e-4); break;
            case "Flux": Assert.AreEqual(plain.campRecoveryPerSeventh * x.fluxRest, led.campRecoveryPerSeventh, 1e-4); break;
            case "Resonance":
                // Its strength is the party's hardship, not the unit.
                var worn = Party("Vaelia");
                worn.hungry = true;
                Assert.AreEqual(Expeditions.Hardship(worn, Wild, false, null, x) * x.resonanceHardship, Expeditions.Hardship(worn, Wild, false, binding, x), 1e-4);
                break;
        }
    }

    [Test]
    public void Needs_ACrystalDirectorsPartyWearsSlower()
    {
        var rules = new ProvisionRules();
        var plain = PerLegend.Clone();
        var crystal = PerLegend.Clone();
        crystal.wearMultiplier = 0.5f;
        var a = new WorldUnit { supplies = 0f, provisionsInitialized = true, microInitialized = true };
        var b = new WorldUnit { supplies = 0f, provisionsInitialized = true, microInitialized = true };
        var danger = new UnitSurroundings { weather = 1f, danger = 1f };
        WorldUnits.Needs(a, plain, rules, danger, UnitActivity.Moving, 1f);
        WorldUnits.Needs(b, crystal, rules, danger, UnitActivity.Moving, 1f);
        Assert.Greater(a.attrition, 0f);
        Assert.AreEqual(a.attrition * 0.5f, b.attrition, 1e-4);
    }

    // ===== AMBITION =====

    [Test]
    public void Ambition_NoneChangesNothing()
    {
        var x = Settings();
        var without = Expeditions.Effective(PerLegend, Party("Vaelia"), null, x);
        var none = Expeditions.Effective(PerLegend, Party("Vaelia"), null, x, ambition: ExpeditionAmbition.None);
        Assert.AreEqual(without.stamina, none.stamina, 1e-5);
        Assert.AreEqual(without.wearMultiplier, none.wearMultiplier, 1e-5);
        Assert.AreEqual(without.supplyUsePerSeventh, none.supplyUsePerSeventh, 1e-5);
        Assert.IsNull(Expeditions.AmbitionSummary(ExpeditionAmbition.None), "nothing to say on the card");
    }

    [Test]
    public void Ambition_CostLightensTheWearAndTheRationsAndTimeQuickensTheMarch()
    {
        var x = Settings();
        var ambition = new ExpeditionAmbition { cost = 0.8f, time = 0.8f };
        var plain = Expeditions.Effective(PerLegend, Party("Vaelia", "Orphael"), null, x);
        var ambitious = Expeditions.Effective(PerLegend, Party("Vaelia", "Orphael"), null, x, ambition: ambition);
        Assert.AreEqual(plain.wearMultiplier * 0.8f, ambitious.wearMultiplier, 1e-5, "attrition: the road's cost");
        Assert.AreEqual(plain.supplyUsePerSeventh * 0.8f, ambitious.supplyUsePerSeventh, 1e-5, "rations: the road's cost");
        Assert.AreEqual(plain.stamina * 1.25f, ambitious.stamina, 1e-4, "80% of the time: it advances 25% faster");
        Assert.AreEqual(plain.supplyCapacity, ambitious.supplyCapacity, 1e-5, "it carries as much");
        Assert.AreEqual(plain.surveySevenths, ambitious.surveySevenths, 1e-5, "surveys take as long");

        // Over the same road: less wear taken, and the same journey done in 80% of the Sevenths.
        var danger = new UnitSurroundings { weather = 1f, danger = 1f };
        var a = new WorldUnit { supplies = 0f, provisionsInitialized = true, microInitialized = true };
        var b = new WorldUnit { supplies = 0f, provisionsInitialized = true, microInitialized = true };
        WorldUnits.Needs(a, plain, new ProvisionRules(), danger, UnitActivity.Moving, 1f);
        WorldUnits.Needs(b, ambitious, new ProvisionRules(), danger, UnitActivity.Moving, 1f);
        Assert.AreEqual(a.attrition * 0.8f, b.attrition, 1e-4);
        var walker = new WorldUnit();
        Assert.AreEqual(WorldUnits.SeventhsFor(walker, plain, 30f) * 0.8f, WorldUnits.SeventhsFor(walker, ambitious, 30f), 1e-4);

        StringAssert.Contains("20% cheaper outfit, less wear and rations", Expeditions.AmbitionSummary(ambition));
        StringAssert.Contains("walks 25% faster", Expeditions.AmbitionSummary(ambition));
    }

    [Test]
    public void Ambition_TheOutfitCostsLessRoundedUp()
    {
        var outfit = new List<ResourceAmount> { new ResourceAmount { resource = "Elderwood", amount = 15f }, new ResourceAmount { resource = "Food", amount = 4f } };
        Assert.AreEqual("15 Elderwood, 4 Food", WorldSystem.CostText(outfit));
        Assert.AreEqual("12 Elderwood, 4 Food", WorldSystem.CostText(outfit, 0.8f), "20% cheaper: 12 Elderwood, 3.2 Food rounds up to 4");
    }

    [Test]
    public void Ambition_AMishapsWearAndSpoiledRationsCostLessButItsWearinessDoesNot()
    {
        var plain = new WorldUnit { supplies = 10f };
        var ambitious = new WorldUnit { supplies = 10f };
        var spec = new MishapSpec { attrition = 10f, fatigue = 20f, rationsLost = 0.5f };
        Expeditions.Afflict(plain, spec);
        Expeditions.Afflict(ambitious, spec, cost: 0.5f);
        Assert.AreEqual(10f, plain.attrition, 1e-4);
        Assert.AreEqual(5f, ambitious.attrition, 1e-4, "half the cost: half the wear");
        Assert.AreEqual(5f, plain.supplies, 1e-4);
        Assert.AreEqual(7.5f, ambitious.supplies, 1e-4, "a quarter spoiled instead of half");
        Assert.AreEqual(plain.fatigue, ambitious.fatigue, 1e-4, "weariness is the time's, not the cost's");
    }

    // ===== HARDSHIP =====

    [Test]
    public void Hardship_NothingWhileAllGoesWell()
    {
        var unit = Party("Vaelia");
        unit.attrition = Settings().attritionFrom;
        Assert.AreEqual(0f, Expeditions.Hardship(unit, Wild, true, null, Settings()));
    }

    [Test]
    public void Hardship_GrowsWithWearHungerAndExhaustionAndTheDirectorCarriesMore()
    {
        var x = Settings();
        var unit = Party("Vaelia", "Orphael");
        unit.attrition = 100f;
        Assert.AreEqual(x.attritionStrain, Expeditions.Hardship(unit, Wild, false, null, x), 1e-4, "full wear");
        unit.attrition = x.attritionFrom + (100f - x.attritionFrom) / 2f;
        Assert.AreEqual(x.attritionStrain / 2f, Expeditions.Hardship(unit, Wild, false, null, x), 1e-4, "half way from the threshold");
        unit.hungry = true;
        unit.fatigue = 95f;
        float companion = Expeditions.Hardship(unit, Wild, false, null, x);
        Assert.AreEqual(x.attritionStrain / 2f + x.hungerStrain + x.exhaustionStrain, companion, 1e-4);
        Assert.AreEqual(companion * x.directorShare, Expeditions.Hardship(unit, Wild, true, null, x), 1e-4);
    }

    [Test]
    public void Hardship_DissonanceAndWeatherWeighOnlyOutsideYourSettlements()
    {
        var x = Settings();
        var unit = Party("Vaelia");
        var harsh = new UnitSurroundings { weather = 1.5f, dissonance = 0.5f };
        Assert.AreEqual(0.5f * x.dissonanceStrain + 0.5f * x.exposureStrain, Expeditions.Hardship(unit, harsh, false, null, x), 1e-4);
        harsh.settlement = true;
        Assert.AreEqual(0f, Expeditions.Hardship(unit, harsh, false, null, x));
    }

    [Test]
    public void Hardship_StrainsComposureOnTheRoadAndRestInASettlementHeals()
    {
        var t = new ComposureTuning();
        var road = new ComposureContext { onExpedition = true, hardship = 3f, crisisDeclared = true, crisisBegun = true, griefShare = 0.5f };
        Assert.AreEqual(3f, ComposureRules.Load(road, t), 1e-4, "the road's hardship, not the council's crisis or dead");
        Assert.AreEqual(30f + 3f - t.expeditionRecovery, ComposureRules.Next(30f, road, t), 1e-4);
        var camped = new ComposureContext { onExpedition = true, restingAtSettlement = true };
        Assert.AreEqual(t.restRecovery, ComposureRules.Recovery(camped, t), 1e-4, "camped in a settlement it rests as at home");
    }

    [Test]
    public void Weight_CargoAndRationsChangePaceAndTravelFatigue()
    {
        var x = Settings();
        x.loadPerLegend = 10f;
        x.rationWeight = 1f;
        x.overloadSlowdown = 0.6f;
        x.overloadFatigue = 0.5f;
        x.cargoWeights = new List<ResourceAmount> { new ResourceAmount { resource = "Sky Glass", amount = 2f } };
        var unit = Party("Vaelia");
        unit.supplies = 10f;
        unit.cargo = new List<ResourceAmount> { new ResourceAmount { resource = "Sky Glass", amount = 10f } };

        Assert.AreEqual(20f, Expeditions.CargoWeight(unit, x), 1e-4);
        Assert.AreEqual(30f, Expeditions.Load(unit, x), 1e-4);
        Assert.AreEqual(10f, Expeditions.LoadAtEase(unit, x), 1e-4);
        Assert.AreEqual(3f, Expeditions.Burden(unit, x), 1e-4);
        Assert.AreEqual(0.35f, Expeditions.LoadPace(x, 3f), 1e-4, "the configured floor protects a party from a negative pace");
        Assert.AreEqual(2f, Expeditions.LoadFatigue(x, 3f), 1e-4);
    }

    [Test]
    public void Solace_SilverWaterBeautyAndGroundStackAndFalloutDrownsIt()
    {
        var x = Settings();
        x.silverRiverSolace = 0.6f;
        x.silverLakeSolace = 0.8f;
        x.beautySolace = 1f;
        x.maxSolace = 4f;
        var at = new UnitSurroundings { mapped = true, solace = 1f, beauty = 0.5f, silverRiverSteps = 0, silverLakeSteps = 0 };
        Assert.AreEqual(2.9f, Expeditions.Solace(at, x), 1e-4);
        at.fallout = 0.5f;
        Assert.AreEqual(1.45f, Expeditions.Solace(at, x), 1e-4);
    }

    [Test]
    public void ForageRichness_FertilityAndLivingSitesIncreaseGathering()
    {
        var rules = new ProvisionRules { fertileFrom = 0.4f, fertilityForage = 0.8f, siteForage = 0.6f, grandfieldForage = 0.5f, maxForageBonus = 1.5f };
        Assert.AreEqual(1f, WorldUnits.ForageRichness(rules, new UnitSurroundings()), 1e-4);
        Assert.AreEqual(2.5f, WorldUnits.ForageRichness(rules, new UnitSurroundings { fertility = 1f, bounty = 1f, grandfield = 1f }), 1e-4);
    }

    [Test]
    public void Vibration_FalloutIsPermanentAndMakesTravelCostMore()
    {
        var v = new VibrationSettings { falloutFrom = 0.2f, falloutFull = 0.4f, travel = 2f };
        Assert.AreEqual(0f, WorldVibration.Fallout(v, 0.2f, false), 1e-4);
        Assert.AreEqual(0.5f, WorldVibration.Fallout(v, 0.3f, false), 1e-4);
        Assert.AreEqual(0f, WorldVibration.Fallout(v, 0.4f, true), 1e-4, "Sacred ground is protected from ordinary Dissonance");
        Assert.AreEqual(2f, WorldVibration.TravelFactor(v, 0.5f), 1e-4);
    }

    // ===== MISHAPS =====

    [Test]
    public void Risk_NoneWhileAllGoesWell()
    {
        var unit = Party("Vaelia", "Orphael");
        unit.attrition = 20f;
        Assert.AreEqual(0f, Expeditions.MishapRisk(unit, Wild, new[] { Member("Vaelia", director: true), Member("Orphael") }, Settings()));
    }

    [Test]
    public void Risk_GrowsAsThingsGoBadAndWithStrainedLegends()
    {
        var x = Settings();
        var unit = Party("Vaelia", "Orphael");
        var party = new[] { Member("Vaelia", director: true), Member("Orphael") };
        unit.attrition = 100f;
        Assert.AreEqual(x.attritionRisk, Expeditions.MishapRisk(unit, Wild, party, x), 1e-4);
        unit.hungry = true;
        var dangerous = new UnitSurroundings { weather = 1f, danger = 0.5f };
        Assert.AreEqual(x.attritionRisk + x.hungerRisk + 0.5f * x.dangerRisk, Expeditions.MishapRisk(unit, dangerous, party, x), 1e-4);
        var strained = new[] { Member("Vaelia", ComposureState.Fractured, 45f, true), Member("Orphael", ComposureState.Spiraling, 75f) };
        Assert.AreEqual(x.maxRisk, Expeditions.MishapRisk(unit, dangerous, strained, x), 1e-4, "capped");
        var calm = Party("Vaelia", "Orphael");
        Assert.AreEqual(x.fracturedRisk + x.spiralingRisk, Expeditions.MishapRisk(calm, Home, strained, x), 1e-4, "strained legends make mistakes even at home");
    }

    [Test]
    public void Mishaps_EachKindHasItsOwnCondition()
    {
        var unit = Party("Vaelia", "Orphael");
        var calm = new List<PartyMember> { Member("Vaelia", director: true), Member("Orphael") };
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Injury, unit, Wild, calm));
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Injury, unit, Home, calm), "no injuries at home");
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Fever, unit, Wild, calm));
        unit.hungry = true;
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Fever, unit, Wild, calm));
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.SpoiledRations, unit, Wild, calm));
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.LostBearings, unit, Wild, calm), "standing still in fair weather");
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Whispers, unit, Wild, calm));
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Whispers, unit, new UnitSurroundings { weather = 1f, dissonance = 0.3f }, calm));
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Ambush, unit, new UnitSurroundings { weather = 1f, danger = 0.3f }, calm));
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Quarrel, unit, Wild, calm));
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Desertion, unit, Wild, calm));
        var spiraling = new List<PartyMember> { Member("Vaelia", director: true), Member("Orphael", ComposureState.Spiraling, 80f) };
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Quarrel, unit, Home, spiraling), "quarrels break out anywhere");
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Desertion, unit, Home, spiraling));
        var spiralingDirector = new List<PartyMember> { Member("Vaelia", ComposureState.Spiraling, 80f, true), Member("Orphael") };
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Desertion, unit, Wild, spiralingDirector), "the Director never deserts");
    }

    [Test]
    public void Roll_NothingStrikesWhenTheDrawIsAboveTheRisk()
    {
        var x = Settings();
        var unit = Party("Vaelia");
        unit.hungry = true;
        var party = new List<PartyMember> { Member("Vaelia", director: true) };
        float risk = Expeditions.MishapRisk(unit, Wild, party, x);
        Assert.IsNull(Expeditions.RollMishap(unit, Wild, party, x, risk + 0.01, 0.5, 0.5));
        Assert.IsNotNull(Expeditions.RollMishap(unit, Wild, party, x, risk - 0.01, 0.5, 0.5));
    }

    [Test]
    public void Roll_PicksAmongWhatCanHappenByWeight()
    {
        var x = new ExpeditionSettings
        {
            maxRisk = 1f, hungerRisk = 1f,
            mishaps = new List<MishapSpec>
            {
                new MishapSpec { kind = MishapKind.Injury, name = "Injury", weight = 1f, strain = 8f },
                new MishapSpec { kind = MishapKind.Ambush, name = "Ambush", weight = 50f },
                new MishapSpec { kind = MishapKind.Fever, name = "Fever", weight = 3f, strain = 6f },
            },
        };
        var unit = Party("Vaelia");
        unit.hungry = true;
        var party = new List<PartyMember> { Member("Vaelia", director: true) };
        // No danger here, so no ambush: Injury holds a quarter of the weight, Fever the rest.
        Assert.AreEqual(MishapKind.Injury, Expeditions.RollMishap(unit, Wild, party, x, 0.0, 0.2, 0.5).spec.kind);
        Assert.AreEqual(MishapKind.Fever, Expeditions.RollMishap(unit, Wild, party, x, 0.0, 0.3, 0.5).spec.kind);
        Assert.AreEqual(MishapKind.Fever, Expeditions.RollMishap(unit, Wild, party, x, 0.0, 0.99, 0.5).spec.kind);
    }

    [Test]
    public void Roll_TargetsTheStrainedTheDirectorOrTheDeserter()
    {
        var unit = Party("Vaelia", "Orphael", "Sephira");
        unit.hungry = true;
        var party = new List<PartyMember>
        {
            Member("Vaelia", ComposureState.Clouded, 20f, true),
            Member("Orphael", ComposureState.Spiraling, 90f),
            Member("Sephira", ComposureState.Fractured, 50f),
        };
        MishapSpec Only(MishapKind kind) => new MishapSpec { kind = kind, name = kind.ToString(), weight = 1f };
        ExpeditionSettings With(MishapKind kind) => new ExpeditionSettings { maxRisk = 1f, hungerRisk = 1f, mishaps = new List<MishapSpec> { Only(kind) } };

        Assert.AreEqual("Vaelia", Expeditions.RollMishap(unit, Wild, party, With(MishapKind.SpoiledRations), 0, 0, 0.99).target, "the Director answers for the rations");
        Assert.AreEqual("Orphael", Expeditions.RollMishap(unit, Wild, party, With(MishapKind.Desertion), 0, 0, 0).target, "the Spiraling companion deserts");
        var quarrel = Expeditions.RollMishap(unit, Wild, party, With(MishapKind.Quarrel), 0, 0, 0.99);
        Assert.AreNotEqual(quarrel.target, quarrel.second);
        Assert.IsNotNull(quarrel.second);
        // Injuries find the strained likeliest: strain + 10 each (30, 100, 60 of 190).
        var injury = With(MishapKind.Injury);
        Assert.AreEqual("Vaelia", Expeditions.RollMishap(unit, Wild, party, injury, 0, 0, 0.1).target);
        Assert.AreEqual("Orphael", Expeditions.RollMishap(unit, Wild, party, injury, 0, 0, 0.4).target);
        Assert.AreEqual("Sephira", Expeditions.RollMishap(unit, Wild, party, injury, 0, 0, 0.9).target);
    }

    [Test]
    public void Afflict_WearsTiresAndSpoilsAndIsCounted()
    {
        var unit = Party("Vaelia");
        unit.attrition = 95f;
        Expeditions.Afflict(unit, new MishapSpec { attrition = 12f, fatigue = 20f, rationsLost = 0.25f });
        Assert.AreEqual(100f, unit.attrition, "never past full");
        Assert.AreEqual(20f, unit.fatigue);
        Assert.AreEqual(15f, unit.supplies, 1e-4);
        Assert.AreEqual(1, unit.mishaps);
    }

    [Test]
    public void Mishaps_EveryKindHasADefaultAndANotice()
    {
        var kinds = System.Enum.GetValues(typeof(MishapKind)).Cast<MishapKind>().ToList();
        CollectionAssert.AreEquivalent(kinds, Expeditions.DefaultMishaps.Select(m => m.kind).ToList());
        foreach (var spec in Expeditions.DefaultMishaps)
        {
            var (title, text) = Expeditions.Describe(new Mishap { spec = spec, target = "Vaelia", second = "Orphael" }, "Vaelia's Expedition", "the Great Expanse");
            StringAssert.Contains(spec.name, title);
            StringAssert.Contains("the Great Expanse", text);
        }
        Assert.AreSame(Expeditions.DefaultMishaps, Expeditions.Mishaps(new ExpeditionSettings()), "an empty list means the defaults");
    }

    // ===== PARTY SIZE: SOLO, DUO, TRIO, COMPANY =====

    private static PartyMember[] Members(int size, ComposureState state = ComposureState.Clouded) =>
        new[] { "Vaelia", "Orphael", "Sephira", "Luminaire", "Amadea" }.Take(size).Select((n, i) => Member(n, state, 20f + i, i == 0)).ToArray();

    private static WorldUnit OfSize(int size) =>
        Party("Vaelia", new[] { "Orphael", "Sephira", "Luminaire", "Amadea" }.Take(size - 1).ToArray());

    [Test]
    public void Shapes_EachSizeHasItsOwnAndTheLastCoversEveryLargerParty()
    {
        var x = Shaped();
        CollectionAssert.AreEqual(new[] { "Solo", "Duo", "Trio", "Company", "Company" }, Enumerable.Range(1, 5).Select(n => PartyShapes.Of(x, n).name).ToArray());
        Assert.AreEqual("Solo", PartyShapes.Of(x, 0).name, "an empty party reads as one legend");
        Assert.AreSame(PartyShapes.Defaults, PartyShapes.All(x), "an empty list means the defaults");
        var custom = new ExpeditionSettings { shapes = new List<PartyShape> { new PartyShape { name = "Pack", size = 3 }, new PartyShape { name = "Lone", size = 1 } } };
        Assert.AreEqual("Lone", PartyShapes.Of(custom, 2).name, "listed out of order, still the largest not above the party");
        Assert.AreEqual("Pack", PartyShapes.Of(custom, 7).name);
    }

    [Test]
    public void Shapes_FewerLegendsWalkFasterAndWearSlower()
    {
        var x = Shaped();
        var paces = Enumerable.Range(1, 4).Select(n => Expeditions.Effective(PerLegend, OfSize(n), null, x).stamina).ToArray();
        var wear = Enumerable.Range(1, 4).Select(n => Expeditions.Effective(PerLegend, OfSize(n), null, x).wearMultiplier).ToArray();
        for (int i = 1; i < 4; i++)
        {
            Assert.Less(paces[i], paces[i - 1], $"a party of {i + 1} is slower than one of {i}");
            Assert.Greater(wear[i], wear[i - 1], $"a party of {i + 1} wears faster than one of {i}");
        }
        Assert.AreEqual(6f * 1.25f, paces[0], 1e-4);
        var company = Expeditions.Effective(PerLegend, OfSize(4), null, x);
        Assert.AreEqual(PerLegend.sight + 1, company.sight, "more eyes");
        Assert.Less(company.surveySevenths, Expeditions.Effective(PerLegend, OfSize(1), null, x).surveySevenths, "more hands survey quicker");
    }

    [Test]
    public void Shapes_ARetreatIsFasterStillForThoseSmallEnough()
    {
        var x = Shaped();
        var solo = OfSize(1);
        float walking = Expeditions.Effective(PerLegend, solo, null, x).stamina;
        solo.retreating = true;
        Assert.AreEqual(walking * 1.5f, Expeditions.Effective(PerLegend, solo, null, x).stamina, 1e-4);
        var trio = OfSize(3);
        float trioWalking = Expeditions.Effective(PerLegend, trio, null, x).stamina;
        trio.retreating = true;
        Assert.AreEqual(trioWalking, Expeditions.Effective(PerLegend, trio, null, x).stamina, 1e-4, "a trio cannot slip away");
    }

    [Test]
    public void Shapes_SolitudeStrainsALoneLegendAndACompanyChafes()
    {
        var x = Shaped();
        Assert.AreEqual(0.75f * x.directorShare, Expeditions.Hardship(OfSize(1), Wild, true, null, x), 1e-4, "alone, even a good road weighs");
        Assert.AreEqual(0f, Expeditions.Hardship(OfSize(2), Wild, false, null, x), 1e-4, "a duo on a good road is at ease");
        Assert.AreEqual(0f, Expeditions.Hardship(OfSize(3), Wild, false, null, x), 1e-4);
        Assert.AreEqual(0.25f, Expeditions.Hardship(OfSize(4), Wild, false, null, x), 1e-4, "friction in a company");
        Assert.AreEqual(0f, Expeditions.Hardship(OfSize(1), Home, true, null, x), "at home no one is alone");

        // On a bad road the weight is shared best by two: solo > company > trio > duo.
        float Bad(int n) { var u = OfSize(n); u.hungry = true; u.attrition = 60f; return Expeditions.Hardship(u, Wild, false, null, x); }
        Assert.Greater(Bad(1), Bad(4));
        Assert.Greater(Bad(4), Bad(3));
        Assert.Greater(Bad(3), Bad(2));
    }

    [Test]
    public void Shapes_BiggerPartiesCourtMoreTroubleAndLoneLegendsGoUnseen()
    {
        var x = Shaped();
        float Risk(int n, UnitSurroundings at) => Expeditions.MishapRisk(OfSize(n), at, Members(n), x);
        Assert.AreEqual(0f, Risk(1, Wild));
        Assert.AreEqual(0f, Risk(2, Wild));
        Assert.AreEqual(0.02f * 1.1f, Risk(3, Wild), 1e-4, "a trio has its accidents even when all goes well");
        Assert.AreEqual(0.04f * 1.25f, Risk(4, Wild), 1e-4);
        Assert.AreEqual(0f, Risk(4, Home), "not in a settlement");
        var danger = new UnitSurroundings { weather = 1f, danger = 0.6f };
        for (int n = 2; n <= 4; n++) Assert.Greater(Risk(n, danger), Risk(n - 1, danger), $"in danger a party of {n} is likelier found than one of {n - 1}");
    }

    [Test]
    public void Shapes_QuarrelsNeedTwoAndACompanyQuarrelsWhileMerelyClouded()
    {
        var x = Shaped();
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Quarrel, OfSize(1), Wild, Members(1, ComposureState.Spiraling), x), "no one to quarrel with");
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Quarrel, OfSize(2), Wild, Members(2), x), "a calm duo keeps the peace");
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Quarrel, OfSize(2), Wild, Members(2, ComposureState.Fractured), x));
        Assert.IsFalse(Expeditions.CanHappen(MishapKind.Quarrel, OfSize(3), Wild, Members(3), x));
        Assert.IsTrue(Expeditions.CanHappen(MishapKind.Quarrel, OfSize(4), Wild, Members(4), x), "a company chafes while merely Clouded");

        // Among equal weights a company's trouble is likelier a quarrel than a trio's.
        var even = new ExpeditionSettings
        {
            maxRisk = 1f, hungerRisk = 1f,
            mishaps = new List<MishapSpec> { new MishapSpec { kind = MishapKind.Injury, name = "Injury", weight = 1f }, new MishapSpec { kind = MishapKind.Quarrel, name = "Quarrel", weight = 1f } },
        };
        var four = OfSize(4);
        four.hungry = true;
        // Injury 1, Quarrel 1.5 of 2.5: a pick of 0.45 lands on the quarrel (it would be the injury at even weights).
        Assert.AreEqual(MishapKind.Quarrel, Expeditions.RollMishap(four, Wild, Members(4, ComposureState.Fractured), even, 0, 0.45, 0.5).spec.kind);
    }

    [Test]
    public void Respond_AloneNoOneHelpsButAnAmbushCanBeSlipped()
    {
        var x = Shaped();
        var solo = Members(1);
        var injury = new Mishap { spec = new MishapSpec { kind = MishapKind.Injury }, target = "Vaelia" };
        Assert.AreEqual(MishapOutcome.Struck, PartyShapes.Respond(injury, solo, x, 0.0), "no one to bind the wound");
        var ambush = new Mishap { spec = new MishapSpec { kind = MishapKind.Ambush }, target = "Vaelia" };
        Assert.AreEqual(MishapOutcome.Evaded, PartyShapes.Respond(ambush, solo, x, 0.49));
        Assert.AreEqual(MishapOutcome.Struck, PartyShapes.Respond(ambush, solo, x, 0.51));
        Assert.AreEqual(MishapOutcome.Eased, PartyShapes.Respond(ambush, Members(3), x, 0.0), "a trio cannot slip away, but the others rally");
    }

    [Test]
    public void Respond_TheCalmestAbleCompanionStepsInAndMoreHandsHelpMoreOften()
    {
        var x = Shaped();
        var party = new List<PartyMember>
        {
            Member("Vaelia", ComposureState.Clouded, 30f, true),
            Member("Orphael", ComposureState.Spiraling, 5f),
            Member("Sephira", ComposureState.Clouded, 25f),
        };
        var fever = new Mishap { spec = new MishapSpec { kind = MishapKind.Fever }, target = "Vaelia" };
        Assert.AreEqual(MishapOutcome.Eased, PartyShapes.Respond(fever, party, x, 0.4));
        Assert.AreEqual("Sephira", fever.helper, "the calmest who can help: Orphael is Spiraling");
        Assert.AreEqual(MishapOutcome.Struck, PartyShapes.Respond(fever, party, x, 0.5), "a trio handles 45%");
        Assert.IsNull(fever.helper);

        Assert.AreEqual(MishapOutcome.Struck, PartyShapes.Respond(new Mishap { spec = fever.spec, target = "Vaelia" }, Members(2), x, 0.4), "a duo handles 30%");
        Assert.AreEqual(MishapOutcome.Eased, PartyShapes.Respond(new Mishap { spec = fever.spec, target = "Vaelia" }, Members(4), x, 0.5), "a company 55%");

        // A quarrel needs a third to mediate.
        var quarrel = new MishapSpec { kind = MishapKind.Quarrel };
        Assert.AreEqual(MishapOutcome.Struck, PartyShapes.Respond(new Mishap { spec = quarrel, target = "Vaelia", second = "Orphael" }, Members(2), x, 0.0));
        var mediated = new Mishap { spec = quarrel, target = "Vaelia", second = "Orphael" };
        Assert.AreEqual(MishapOutcome.Eased, PartyShapes.Respond(mediated, Members(3), x, 0.0));
        Assert.AreEqual("Sephira", mediated.helper);
    }

    [Test]
    public void Respond_AnEasedMishapLandsOnlyInPart()
    {
        var x = Shaped();
        var unit = Party("Vaelia", "Orphael");
        Expeditions.Afflict(unit, new MishapSpec { attrition = 10f, fatigue = 20f, rationsLost = 0.5f }, PartyShapes.WearShare(MishapOutcome.Eased, x));
        Assert.AreEqual(10f * x.easedShare, unit.attrition, 1e-4);
        Assert.AreEqual(20f * x.easedShare, unit.fatigue, 1e-4);
        Assert.AreEqual(20f * (1f - 0.5f * x.easedShare), unit.supplies, 1e-4);
        Assert.AreEqual(0f, PartyShapes.WearShare(MishapOutcome.Evaded, x), "a slipped ambush wears nothing...");
        Assert.AreEqual(x.easedShare, PartyShapes.StrainShare(MishapOutcome.Evaded, x), "...but it still frightens");
        var (title, text) = Expeditions.Describe(new Mishap { spec = Expeditions.DefaultMishaps.First(m => m.kind == MishapKind.Desertion), target = "Orphael", outcome = MishapOutcome.Eased, helper = "Vaelia" }, "Vaelia's Expedition", "the Great Expanse");
        StringAssert.Contains("averted", title);
        StringAssert.Contains("Vaelia talks them into staying", text);
    }

    [Test]
    public void Retreat_OnlyForSmallPartiesAndOnlyFromDanger()
    {
        var x = Shaped();
        var danger = new UnitSurroundings { weather = 1f, danger = 0.5f };
        Assert.IsNull(PartyShapes.WhyNotRetreat(OfSize(1), danger, x));
        Assert.IsNull(PartyShapes.WhyNotRetreat(OfSize(2), danger, x));
        StringAssert.Contains("too many", PartyShapes.WhyNotRetreat(OfSize(3), danger, x));
        StringAssert.Contains("Nothing here", PartyShapes.WhyNotRetreat(OfSize(1), Wild, x), "calm ground is safe already");
        StringAssert.Contains("Nothing here", PartyShapes.WhyNotRetreat(OfSize(1), new UnitSurroundings { weather = 1f, danger = 0.5f, held = true }, x), "your own ground is safe");
        Assert.IsNull(PartyShapes.WhyNotRetreat(OfSize(1), new UnitSurroundings { weather = 1f, dissonance = 0.4f }, x), "Dissonance is worth fleeing too");
    }

    [Test]
    public void Vanish_ALoneLegendDeepEnoughGoesToGroundAndTurnsUpLater()
    {
        var x = Shaped();
        var solo = OfSize(1);
        StringAssert.Contains("alone", PartyShapes.WhyNotVanish(OfSize(2), Wild, ComposureState.Spiraling, x));
        StringAssert.Contains("Clouded", PartyShapes.WhyNotVanish(solo, Wild, ComposureState.Clouded, x), "not desperate enough");
        StringAssert.Contains("safe here", PartyShapes.WhyNotVanish(solo, Home, ComposureState.Fractured, x));
        Assert.IsNull(PartyShapes.WhyNotVanish(solo, Wild, ComposureState.Fractured, x));
        solo.settlers = 5;
        StringAssert.Contains("settlers", PartyShapes.WhyNotVanish(solo, Wild, ComposureState.Fractured, x));
        solo.settlers = 0;

        float sevenths = PartyShapes.MissingSevenths(x, 2f);
        Assert.AreEqual(x.missingSevenths + 2f * x.missingSlowness, sevenths, 1e-4);
        var home = new HexCoord(3, -1);
        solo.path.Add(new HexCoord(9, 9));
        solo.hungry = true;
        solo.attrition = 100f;
        PartyShapes.Vanish(solo, home, sevenths);
        Assert.IsTrue(solo.Missing);
        Assert.IsFalse(solo.Moving, "its road is left behind");
        Assert.IsFalse(WorldUnits.AwaitsOrders(solo), "no one can order it");
        Assert.AreEqual(0f, Expeditions.Hardship(solo, Wild, true, null, x), "out of sight, the road weighs nothing");
        Assert.AreEqual(0f, Expeditions.MishapRisk(solo, Wild, Members(1), x), "and nothing befalls it");
        int ticks = 0;
        while (!PartyShapes.Tick(solo)) Assert.Less(++ticks, 20);
        Assert.AreEqual(4, ticks, "five Sevenths: it turns up on the fifth");
        PartyShapes.Reappear(solo, x);
        Assert.IsFalse(solo.Missing);
        Assert.AreEqual(home, solo.microCoord);
        Assert.AreEqual(x.reappearAttrition, solo.attrition, 1e-4);
        Assert.AreEqual(x.reappearFatigue, solo.fatigue, 1e-4);
        Assert.IsTrue(solo.Camping && solo.resting, "it makes camp to recover");
    }

    [Test]
    public void Summary_NamesWhatEachSizeDoes()
    {
        var x = Shaped();
        StringAssert.Contains("can go to ground", PartyShapes.Summary(PartyShapes.Of(x, 1)));
        StringAssert.Contains("no one to help", PartyShapes.Summary(PartyShapes.Of(x, 1)));
        StringAssert.Contains("can retreat", PartyShapes.Summary(PartyShapes.Of(x, 2)));
        StringAssert.Contains("quarrels from Clouded", PartyShapes.Summary(PartyShapes.Of(x, 4)));
    }
    // ===== WHAT SURVEYS TURN UP =====

    [Test]
    public void Surveys_ASentSurveyTurnsUpMoreThanAPartyPassingBy()
    {
        var x = Settings();
        Assert.Greater(x.surveyEventChance, x.passingEventChance, "events are likelier on a survey");
        Assert.Greater(x.surveyCacheChance, x.passingCacheChance, "and so are spare resources");
        // A draw between the two chances: found by the survey, not in passing.
        double between = (x.passingEventChance + x.surveyEventChance) / 2.0, cacheBetween = (x.passingCacheChance + x.surveyCacheChance) / 2.0;
        var surveyed = Expeditions.RollSurvey(x, true, between, 0.0, cacheBetween);
        var passing = Expeditions.RollSurvey(x, false, between, 0.0, cacheBetween);
        Assert.IsNotNull(surveyed.find);
        Assert.IsTrue(surveyed.cache);
        Assert.IsNull(passing.find);
        Assert.IsFalse(passing.cache);
        Assert.IsNull(Expeditions.RollSurvey(x, true, 0.99, 0.0, 0.99).find, "a high draw finds nothing");

        // The pick draw chooses by weight, across every find.
        var finds = Expeditions.SurveyFinds(x);
        Assert.AreSame(finds[0], Expeditions.RollSurvey(x, true, 0.0, 0.0, 1.0).find);
        Assert.AreSame(finds[finds.Count - 1], Expeditions.RollSurvey(x, true, 0.0, 0.9999, 1.0).find);
        var own = new ExpeditionSettings { surveyFinds = new List<SurveyFindSpec> { new SurveyFindSpec { kind = SurveyFindKind.Story, name = "Tale", story = "tale", weight = 1f } } };
        Assert.AreEqual("Tale", Expeditions.RollSurvey(own, true, 0.0, 0.5, 1.0).find.name, "the settings' own finds replace the defaults");
    }

    [Test]
    public void Surveys_SpareResourcesComeFromTheCellsForageOrItsYields()
    {
        var x = Settings();
        var gen = new WorldGenSettings();
        gen.terrains.Add(new TerrainSpec { id = "wood", name = "Wood", passable = true, forage = new List<ResourceAmount> { new ResourceAmount { resource = "Food", amount = 4f } } });
        gen.terrains.Add(new TerrainSpec { id = "rock", name = "Rock", passable = true, yields = new List<ResourceAmount> { new ResourceAmount { resource = "Stone", amount = 0.1f } } });
        var wood = Expeditions.Cache(x, gen, new WorldTile { terrain = "wood" }, 2f);
        Assert.AreEqual("Food", wood.Single().resource);
        Assert.AreEqual(4f * x.cacheForage * 2f, wood.Single().amount, 1e-4f, "forage times the cache share, times the party's multiplier");
        var rock = Expeditions.Cache(x, gen, new WorldTile { terrain = "rock" }, 1f);
        Assert.AreEqual(0.1f * x.cacheYieldSeconds, rock.Single().amount, 1e-4f, "nothing to forage: the ground's yields for a while");
    }
}
