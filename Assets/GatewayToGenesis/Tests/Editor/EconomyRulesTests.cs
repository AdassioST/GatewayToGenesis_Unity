using NUnit.Framework;

/// <summary>
/// The economy formulas of ARCHITECTURE.md §4, pinned to worked numbers: stats (<see cref="StatRules"/>),
/// production, clicks and costs (<see cref="ProductionRules"/>) and the population (<see cref="PopulationRules"/>).
/// A failing test here means a formula changed: update §4 with it, or fix the change.
/// </summary>
public class EconomyRulesTests
{
    private static ModifierValue Mod(float flat, float percent) => new ModifierValue(flat, percent);

    // ===== STATS =====

    [TestCase(5, 0f, 0f, 5)]
    [TestCase(5, 2f, 0f, 7)]
    [TestCase(5, 2f, 50f, 10)]   // (5 + 2) × 1.5 = 10.5 → 10 (Mathf.RoundToInt rounds halves to even)
    [TestCase(5, -10f, 0f, 1)]   // never below 1
    public void Stats_PillarIsBasePlusFlatTimesPercent(int basePillar, float flat, float percent, int expected)
    {
        Assert.AreEqual(expected, StatRules.Pillar(basePillar, Mod(flat, percent)));
    }

    [Test]
    public void Stats_SubstatComesFromItsPillarThenModifiers()
    {
        int baseValue = StatRules.SubstatBase(pillar: 10, pillarMultiplier: 0.5f, adjustment: 2); // 5 + 2
        Assert.AreEqual(7, baseValue);
        Assert.AreEqual(14, StatRules.Substat(baseValue, Mod(0f, 100f)));
        Assert.AreEqual(1, StatRules.SubstatBase(pillar: 1, pillarMultiplier: 0.1f, adjustment: -5), "never below 1");
        Assert.AreEqual(1, StatRules.Substat(3, Mod(-10f, 0f)), "never below 1");
    }

    [Test]
    public void Stats_ThresholdsOnlyFloorWhenAsked()
    {
        Assert.AreEqual(15, StatRules.Threshold(10, Mod(0f, 50f), atLeastOne: false));
        Assert.AreEqual(-5, StatRules.Threshold(5, Mod(-10f, 0f), atLeastOne: false));
        Assert.AreEqual(1, StatRules.Threshold(5, Mod(-10f, 0f), atLeastOne: true));
    }

    [TestCase(0, 0)]
    [TestCase(4, 0)]
    [TestCase(5, 1)]
    [TestCase(14, 2)]
    public void Stats_CommunionStageEveryFiveSecrecy(int secrecy, int stage)
    {
        Assert.AreEqual(stage, StatRules.CommunionStage(secrecy));
    }

    [TestCase(0.9f, 0.25f, 0.1f)]    // 10% cheaper
    [TestCase(0.5f, 0.25f, 0.25f)]   // never past the cap, like the Saving Roll
    [TestCase(1.2f, 0.25f, 0f)]      // never a penalty
    [TestCase(0.5f, 1.5f, 0.5f)]     // the cap is a 0-1 fraction
    [TestCase(0.5f, -1f, 0f)]
    public void Stats_AmbitionReductionsAreCappedLikeTheSavingRoll(float multiplier, float cap, float expected)
    {
        Assert.AreEqual(expected, StatRules.CappedReduction(multiplier, cap), 1e-5);
    }

    [TestCase(0f, 1f)]
    [TestCase(12f, 1.12f)]
    [TestCase(-150f, 0f)]            // never negative
    public void Stats_MagicAndCommunionEffectivenessAreMultipliers(float percent, float expected)
    {
        Assert.AreEqual(expected, StatRules.EffectivenessMultiplier(percent), 1e-5);
    }

    [Test]
    public void Stats_SecrecyGrowsCommunionEffectiveness()
    {
        var bounds = StatGrowth.Boundaries.Default;
        Assert.AreEqual(0f, StatGrowth.Evaluate("communionEffectiveness", 0, bounds), 1e-5);
        Assert.Greater(StatGrowth.Evaluate("communionEffectiveness", 20, bounds), StatGrowth.Evaluate("communionEffectiveness", 5, bounds));
        Assert.AreEqual("secrecy", StatDefinitions.DerivedSource["communionEffectiveness"]);
        Assert.AreEqual(StatDefinitions.StatKind.Derived, StatDefinitions.KindOf("communionEffectiveness"));
    }

    [Test]
    public void Properties_AreNeutralWithoutAStatManager()
    {
        Assume.That(StatManager.Instance == null, "a StatManager is alive in this domain");
        Assert.AreEqual(10f, CivilizationProperties.Magic(10f), "magic at its base");
        Assert.AreEqual(10f, CivilizationProperties.Communion(10f), "communion at its base");
        Assert.AreEqual(0, CivilizationProperties.CommunionStage);
        Assert.AreEqual(ExpeditionAmbition.None.cost, CivilizationProperties.Expedition.cost);
        Assert.AreEqual(ExpeditionAmbition.None.time, CivilizationProperties.Expedition.time);
        Assert.AreEqual(1f, CivilizationProperties.LegendEffectiveness, 1e-5);
    }

    [Test]
    public void Properties_EveryDerivedStatHasANameAndWords()
    {
        foreach (var derived in StatDefinitions.DerivedSource.Keys)
        {
            StringAssert.DoesNotContain("Mod", CivilizationProperties.Name(derived), $"{derived} has a player-facing name");
            Assert.IsFalse(CivilizationProperties.Describe(derived, 1f).StartsWith(CivilizationProperties.Name(derived) + " 1"), $"{derived} has its own sentence");
        }
        Assert.AreEqual("Expeditions cost 20% less to outfit, suffer that much less attrition and eat that many fewer rations", CivilizationProperties.Describe("expeditionCostMod", 0.8f));
        Assert.AreEqual("Expeditions take 20% less time: they advance 25% faster", CivilizationProperties.Describe("expeditionTimeMod", 0.8f));
        Assert.AreEqual("Magic +12% stronger", CivilizationProperties.Describe("magicEffectiveness", 12f));
        Assert.AreEqual("Communion stage 3", CivilizationProperties.Describe("communionStage", 3f));
    }

    [TestCase(-10, 0f, 0.5f, -10)]
    [TestCase(-10, 30f, 0.5f, -7)]    // losses shrink by the mitigation
    [TestCase(-10, 100f, 0.5f, 0)]    // full mitigation cancels a loss, never turns it into a gain
    [TestCase(10, 30f, 0.5f, 12)]     // gains grow by mitigation × balance factor: 10 × 1.15 = 11.5 → 12
    [TestCase(0, 50f, 0.5f, 0)]
    public void Stats_MoraleShiftAfterMitigation(int amount, float mitigation, float gainFactor, int expected)
    {
        Assert.AreEqual(expected, StatRules.MoraleShift(amount, mitigation, gainFactor));
    }

    [TestCase(20, 10, 4, 0.5f, 1f, 18)]   // from above: down by ⌈4 × 0.5⌉ = 2
    [TestCase(11, 10, 40, 0.5f, 1f, 10)]  // never past the resting point
    [TestCase(0, 10, 3, 0.5f, 2f, 6)]     // from below: up by round(3 × 2) = 6
    [TestCase(0, 10, 0, 0.5f, 2f, 1)]     // at least 1 per seventh
    [TestCase(10, 10, 5, 0.5f, 2f, 10)]
    public void Stats_MoraleDriftsToItsRestingPoint(int morale, int target, int waltz, float above, float recovery, int expected)
    {
        Assert.AreEqual(expected, StatRules.MoraleDrift(morale, target, waltz, above, recovery));
    }

    [Test]
    public void Stats_DarkMoraleGrowsBelowBalanceAndFadesAbove()
    {
        Assert.AreEqual(3, StatRules.DarkMoraleChange(balance: 10, morale: 5, increaseFactor: 0.5f, decreaseFactor: 0.5f, currentScore: 0)); // ⌈5 × 0.5⌉
        Assert.AreEqual(-2, StatRules.DarkMoraleChange(balance: 5, morale: 9, increaseFactor: 0.5f, decreaseFactor: 0.5f, currentScore: 10));
        Assert.AreEqual(-1, StatRules.DarkMoraleChange(balance: 5, morale: 25, increaseFactor: 0.5f, decreaseFactor: 0.5f, currentScore: 1), "never below 0");
        Assert.AreEqual(0, StatRules.DarkMoraleChange(balance: 5, morale: 5, increaseFactor: 0.5f, decreaseFactor: 0.5f, currentScore: 4));
    }

    [Test]
    public void Stats_SatisfactionGainsAreBoostedLossesAreNot()
    {
        Assert.AreEqual(15, StatRules.SatisfactionGain(10, 1.5f));
        Assert.AreEqual(-10, StatRules.SatisfactionGain(-10, 1.5f));
    }

    // ===== PRODUCTION =====

    [Test]
    public void Production_UnitOutputScalesWithEfficiencyConsumptionDoesNot()
    {
        Assert.AreEqual(3f * 4f * 1.25f, ProductionRules.UnitOutput(3f, 4f, 25f), 1e-4);
        Assert.AreEqual(3f * 4f, ProductionRules.UnitConsumption(3f, 4f), 1e-4);
    }

    [Test]
    public void Production_NetRateAppliesPercentToOutputOnly()
    {
        var rate = new ProductionRules.Rate { output = 10f, consumption = 4f, percent = 50f };
        rate.AddFlat(2f);   // output
        rate.AddFlat(-3f);  // consumption
        Assert.AreEqual(12f * 1.5f - 7f, rate.Net, 1e-4);
    }

    [Test]
    public void Production_OutputPercentNeverGoesNegative()
    {
        var rate = new ProductionRules.Rate { output = 10f, consumption = 2f, percent = -150f };
        Assert.AreEqual(-2f, rate.Net, 1e-4);
    }

    [TestCase(1f, 0f, 0f, 0f, 0f, 1f)]
    [TestCase(1f, 2f, 1f, 50f, 0f, 6f)]     // (1 + 2 + 1) × 1.5
    [TestCase(1f, 0f, 0f, 50f, 50f, 2f)]    // the Click Power Bonus stat adds to the %
    [TestCase(2f, 0f, -5f, 0f, 0f, 2f)]     // never below base
    public void Production_ClickPower(float baseClick, float permanent, float flat, float percent, float statBonus, float expected)
    {
        Assert.AreEqual(expected, ProductionRules.ClickPower(baseClick, permanent, Mod(flat, percent), statBonus), 1e-4);
    }

    [Test]
    public void Production_BuildCostGrowsWithBuildingsOwned()
    {
        Assert.AreEqual(100f, ProductionRules.BuildCost(100f, 0f, 0.1f, growsWithOwned: true, costBalance: 0.05f, techTier: 1f, owned: 0f), 1e-3);
        Assert.AreEqual(100f * System.MathF.Exp(0.05f * 10f), ProductionRules.BuildCost(100f, 0f, 0.1f, true, 0.05f, 1f, 10f), 1e-2);
        Assert.AreEqual(100f * System.MathF.Exp(0.05f / 2f * 10f), ProductionRules.BuildCost(100f, 0f, 0.1f, true, 0.05f, 2f, 10f), 1e-2, "a higher tech tier slows the growth");
        Assert.AreEqual(100f, ProductionRules.BuildCost(100f, 0f, 0.1f, growsWithOwned: false, 0.05f, 1f, 10f), 1e-3, "units never grow");
    }

    [Test]
    public void Production_BuildCostModifiersAreFlooredAtAShareOfBase()
    {
        Assert.AreEqual(75f, ProductionRules.BuildCost(100f, -25f, 0.1f, false, 0f, 1f, 0f), 1e-3);
        Assert.AreEqual(10f, ProductionRules.BuildCost(100f, -200f, 0.1f, false, 0f, 1f, 0f), 1e-3);
    }

    [Test]
    public void Production_ResearchCostDiscountedByDiscoveryEfficiency()
    {
        Assert.AreEqual(80f, ProductionRules.ResearchCost(100f, 0.2f), 1e-4);
        Assert.AreEqual(0f, ProductionRules.ResearchCost(100f, 1.5f), 1e-4);
    }

    // ===== POPULATION =====

    [Test]
    public void Population_FoodDemandIsLinearAndNeverOverflowsAtCityScale()
    {
        var tuning = new GrowthTuning();
        Assert.AreEqual(GrowthRules.FoodDemand(1, 180f, tuning) * 1000000,
            GrowthRules.FoodDemand(1000000, 180f, tuning), 0.01f);
        Assert.AreEqual(0f, GrowthRules.FoodDemand(0, 180f, tuning));
    }

    [Test]
    public void Population_HousingVagrantsAndStarvation()
    {
        Assert.AreEqual(15, PopulationRules.Housing(10, Mod(0f, 50f)));
        Assert.AreEqual(0, PopulationRules.Housing(2, Mod(-10f, 0f)));
        Assert.AreEqual(2, PopulationRules.VagrantsMovingIn(vagrants: 5, freeHousing: 2, perSecond: 3));
        Assert.AreEqual(0, PopulationRules.VagrantsMovingIn(vagrants: 5, freeHousing: -1, perSecond: 3));
        Assert.IsTrue(PopulationRules.IsStarving(-1f, 0f));
        Assert.IsFalse(PopulationRules.IsStarving(-1f, 5f), "stored food feeds people while the rate is negative");
        Assert.IsFalse(PopulationRules.IsStarving(0f, 0f));
    }
}
