using System;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Pure-logic tests for the shared core (no scene needed). Run from Window > General > Test Runner > EditMode.
/// The "Every..." tests are guards: they fail when an enum value is added without wiring it up.
/// </summary>
public class CoreSystemsTests
{
    // ===== MODIFIER LEDGER =====

    [Test]
    public void Ledger_RemovingASourceRestoresThePreviousTotal()
    {
        var ledger = new ModifierLedger();
        ledger.Set("Food", "Legend: A", new ModifierValue(2f, 10f));
        var before = ledger.Total("Food");

        ledger.Set("Food", "Weather: Storm", new ModifierValue(5f, -20f));
        ledger.RemoveSource("Weather: Storm");

        Assert.AreEqual(before.Flat, ledger.Total("Food").Flat, 1e-4);
        Assert.AreEqual(before.Percent, ledger.Total("Food").Percent, 1e-4);
    }

    [Test]
    public void Ledger_SetReplacesInsteadOfStacking()
    {
        var ledger = new ModifierLedger();
        for (int i = 0; i < 5; i++) ledger.Set("waltz", "Civic: X", new ModifierValue(3f, 0f));
        Assert.AreEqual(3f, ledger.Total("waltz").Flat, 1e-4);
    }

    [Test]
    public void Ledger_AddAccumulatesWithinOneSource()
    {
        var ledger = new ModifierLedger();
        ledger.Add("waltz", "Legend: A", new ModifierValue(2f, 0f));
        ledger.Add("waltz", "Legend: A", new ModifierValue(3f, 5f));
        Assert.AreEqual(5f, ledger.Total("waltz").Flat, 1e-4);
        Assert.AreEqual(5f, ledger.Total("waltz").Percent, 1e-4);
    }

    [Test]
    public void Ledger_ZeroContributionIsRemovedAndRaisesChanged()
    {
        var ledger = new ModifierLedger();
        int changes = 0;
        ledger.Changed += _ => changes++;
        ledger.Set("Food", "Event: A", new ModifierValue(1f, 0f));
        ledger.Set("Food", "Event: A", default);
        Assert.IsFalse(ledger.HasSource("Event: A"));
        Assert.AreEqual(2, changes);
    }

    [Test]
    public void Ledger_TargetsAndSourcesAreCaseInsensitive()
    {
        var ledger = new ModifierLedger();
        ledger.Set("Food", "Legend: A", new ModifierValue(1f, 0f));
        ledger.RemoveSource("legend: a");
        Assert.IsFalse(ledger.HasTarget("food"));
    }

    [Test]
    public void ModifierValue_AppliesFlatThenPercent()
    {
        Assert.AreEqual(15f, new ModifierValue(5f, 50f).ApplyTo(5f), 1e-4);
    }

    // ===== SCOPED TARGETS =====

    [Test]
    public void Targets_ResolveSumsItemSectionTypeAndGlobal()
    {
        var ledger = new ModifierLedger();
        ledger.Set("Food", "a", new ModifierValue(0f, 1f));
        ledger.Set(ModifierTargets.Section("Vital Resource"), "b", new ModifierValue(0f, 10f));
        ledger.Set(ModifierTargets.Type("Vital Resource"), "c", new ModifierValue(0f, 100f));
        ledger.Set(ModifierTargets.All, "d", new ModifierValue(0f, 1000f));
        ledger.Set("Elderwood", "e", new ModifierValue(0f, 10000f));

        Assert.AreEqual(1111f, ModifierTargets.Resolve(ledger, "Food", "Vital Resource", "Vital Resource").Percent, 1e-3);
    }

    [Test]
    public void Targets_CoversMatchesByScope()
    {
        Assert.IsTrue(ModifierTargets.Covers(ModifierTargets.All, "Hut", "Heartlands", "Residence"));
        Assert.IsTrue(ModifierTargets.Covers(ModifierTargets.Section("heartlands"), "Hut", "Heartlands", "Residence"));
        Assert.IsTrue(ModifierTargets.Covers(ModifierTargets.Type("Residence"), "Hut", "Heartlands", "Residence"));
        Assert.IsTrue(ModifierTargets.Covers("hut", "Hut", "Heartlands", "Residence"));
        Assert.IsFalse(ModifierTargets.Covers(ModifierTargets.Section("Academia"), "Hut", "Heartlands", "Residence"));
    }

    // ===== EFFECTS =====

    [Test]
    public void EveryEffectTypeHasAHandler()
    {
        CollectionAssert.IsEmpty(EffectRouter.MissingHandlers().ToList());
    }

    [Test]
    public void EverySeatBonusTypeMapsToAnEffectType()
    {
        foreach (SeatBonusType type in Enum.GetValues(typeof(SeatBonusType)))
        {
            Assert.DoesNotThrow(() => GameEffectAdapters.ToEffectType(type), type.ToString());
        }
    }

    [Test]
    public void EveryEffectTypeHasItsOwnSentence()
    {
        foreach (GameEffectType type in Enum.GetValues(typeof(GameEffectType)))
        {
            Assert.IsTrue(GameEffect.HasSentence(type), $"{type} falls through to the generic wording: add a case to GameEffect.Sentence");
        }
    }

    [Test]
    public void AuthoredEffectsAreWordedByGameEffect()
    {
        var expected = new GameEffect(GameEffectType.ResourceModifier, 10f, ModifierType.Percentage, "Food").Describe();
        Assert.AreEqual("+10% Food production", expected);

        Assert.AreEqual(expected, new LegendBonus { bonusType = GameEffectType.ResourceModifier, modifierValue = 10f, modifierType = ModifierType.Percentage, targetStat = "Food" }.GetAutoDescription());
        Assert.AreEqual(expected, new CivicEffect { effectType = GameEffectType.ResourceModifier, modifierValue = 10f, modifierType = ModifierType.Percentage, targetStat = "Food" }.GetAutoDescription());
        Assert.AreEqual(expected, new WeatherProfileSO.WeatherEffect { effectType = GameEffectType.ResourceModifier, modifierValue = 10f, modifierType = ModifierType.Percentage, targetStat = "Food" }.GetAutoDescription());
        Assert.AreEqual(expected, new SeatBonus { bonusType = SeatBonusType.ResourceModifier, modifierValue = 10f, modifierType = ModifierType.Percentage, targetStat = "Food" }.GetAutoDescription());
        Assert.AreEqual(expected, new CivicSeatBonus { bonusType = SeatBonusType.ResourceModifier, modifierValue = 10f, modifierType = ModifierType.Percentage, targetStat = "Food" }.GetAutoDescription());
    }

    [Test]
    public void SeatBonusWording_AuthoredTextWinsAndTheCivicMarkerIsNamed()
    {
        Assert.AreEqual("Keeps the peace", new SeatBonus { bonusType = SeatBonusType.PillarBonus, targetStat = "waltz", modifierValue = 2f, description = "Keeps the peace" }.GetAutoDescription());
        Assert.AreEqual("Civic Bonus", new SeatBonus { bonusType = SeatBonusType.CivicBonus }.GetAutoDescription());
        Assert.AreEqual("+2 waltz", new SeatBonus { bonusType = SeatBonusType.PillarBonus, targetStat = "waltz", modifierValue = 2f }.GetAutoDescription());
    }

    [Test]
    public void EveryWeatherConditionReadsARegisteredGameValue()
    {
        foreach (WeatherCondition.ConditionType type in Enum.GetValues(typeof(WeatherCondition.ConditionType)))
        {
            string domain = WeatherCondition.DomainOf(type);
            Assert.IsNotNull(domain, $"{type} has no GameValues domain: add it to WeatherCondition.DomainOf");
            Assert.IsTrue(GameValues.IsKnownDomain(domain), $"{type} reads unregistered domain '{domain}'");
        }
    }

    [Test]
    public void Effects_ScopeResolvesToScopedKeys()
    {
        Assert.AreEqual(ModifierTargets.All, EffectRouter.ResolveResourceTarget("Food", ScopeType.Global));
        Assert.AreEqual(ModifierTargets.All, EffectRouter.ResolveResourceTarget("", ScopeType.Individual));
        Assert.AreEqual(ModifierTargets.Section("Vital Resource"), EffectRouter.ResolveResourceTarget("Vital Resource", ScopeType.Section));
        Assert.AreEqual(ModifierTargets.Type("Unit"), EffectRouter.ResolveUnitTarget("units", ScopeType.Individual, out _));
    }

    [Test]
    public void Effects_ShapeValidationCatchesMissingTargets()
    {
        Assert.IsFalse(new GameEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add).ValidateShape().isValid);
        Assert.IsFalse(new GameEffect(GameEffectType.ResourceModifier, 1f, ModifierType.Add, null, ScopeType.Section).ValidateShape().isValid);
        Assert.IsTrue(new GameEffect(GameEffectType.ResourceModifier, 1f, ModifierType.Add, null, ScopeType.Global).ValidateShape().isValid);
    }

    [Test]
    public void EveryModifierConsequenceBecomesAnEffect()
    {
        var modifierTypes = new[]
        {
            EventConsequence.ConsequenceType.ProductionPercentChange, EventConsequence.ConsequenceType.ProductionPercentChangeSection,
            EventConsequence.ConsequenceType.ClickPowerChange, EventConsequence.ConsequenceType.ClickPowerPercentChange,
            EventConsequence.ConsequenceType.ClickPowerChangeSection, EventConsequence.ConsequenceType.ClickPowerPercentChangeSection
        };
        foreach (EventConsequence.ConsequenceType type in Enum.GetValues(typeof(EventConsequence.ConsequenceType)))
        {
            bool isEffect = EventSystemLogic.TryGetEffect(new EventConsequence { type = type, targetName = "Food", value = 10 }, out var effect);
            Assert.AreEqual(modifierTypes.Contains(type), isEffect, type.ToString());
            if (isEffect) Assert.IsTrue(effect.ValidateShape().isValid, type.ToString());
        }
    }

    // ===== CONDITIONS =====

    [Test]
    public void EveryEventConditionReadsARegisteredGameValue()
    {
        foreach (EventCondition.ConditionType type in Enum.GetValues(typeof(EventCondition.ConditionType)))
        {
            // ValueCheck names its domain per condition; EventScript only builds one for a registered domain.
            if (type == EventCondition.ConditionType.ValueCheck) continue;
            string domain = EventCondition.DomainOf(type);
            Assert.IsNotNull(domain, $"{type} has no GameValues domain");
            Assert.IsTrue(GameValues.IsKnownDomain(domain), $"{type} reads unregistered domain '{domain}'");
        }
    }

    [Test]
    public void GameValues_UnknownDomainReportsUnavailable()
    {
        Assert.IsFalse(GameValues.TryGet("no_such_domain", "x", out float value));
        Assert.AreEqual(0f, value);
    }

    [TestCase(5f, ComparisonOperator.GreaterThan, 4f, true)]
    [TestCase(5f, ComparisonOperator.LessThanOrEqual, 5f, true)]
    [TestCase(5f, ComparisonOperator.NotEquals, 5f, false)]
    public void GameValues_Compare(float actual, ComparisonOperator op, float expected, bool result)
    {
        Assert.AreEqual(result, GameValues.Compare(actual, op, expected));
    }

    // ===== STATS =====

    [Test]
    public void EveryDerivedStatHasAGrowthCurveAndASubstatSource()
    {
        foreach (var pair in StatDefinitions.DerivedSource)
        {
            Assert.IsTrue(StatGrowth.Curves.ContainsKey(pair.Key), $"{pair.Key} has no growth curve");
            Assert.AreEqual(StatDefinitions.StatKind.Substat, StatDefinitions.KindOf(pair.Value), $"{pair.Key} grows from '{pair.Value}', which is not a substat");
        }
    }

    [Test]
    public void EverySubstatHasAParentPillar()
    {
        foreach (var substat in StatDefinitions.Substats) Assert.IsNotNull(StatDefinitions.ParentPillar(substat), substat);
    }

    [Test]
    public void StatKeysNormalizeLegacySpellings()
    {
        Assert.AreEqual(StatDefinitions.MaxMorale, StatDefinitions.Key("maxMorale"));
        Assert.AreEqual(StatDefinitions.SatisfactionLevel, StatDefinitions.Key("satisfaction"));
        Assert.AreEqual(StatDefinitions.StatKind.Pillar, StatDefinitions.KindOf("Waltz"));
    }

    [Test]
    public void Growth_IsZeroAtLevelZeroAndNeverDecreases()
    {
        foreach (var name in StatGrowth.Curves.Keys)
        {
            var curve = StatGrowth.Curves[name];
            float previous = 0f;
            for (int level = 0; level <= 260; level += 5)
            {
                float total = StatGrowth.TieredGrowth(level, curve.tiers, StatGrowth.Boundaries.Default);
                if (level == 0) Assert.AreEqual(0f, total, 1e-4, name);
                Assert.GreaterOrEqual(total + 1e-3f, previous, $"{name} decreased at level {level}");
                previous = total;
            }
        }
    }

    [Test]
    public void Growth_MultiplierStatsStartAtOne()
    {
        Assert.AreEqual(1f, StatGrowth.Evaluate("expeditionCostMod", 0, StatGrowth.Boundaries.Default), 1e-4);
        Assert.AreEqual(1f, StatGrowth.Evaluate("moraleRecoveryMod", 0, StatGrowth.Boundaries.Default), 1e-4);
        Assert.Less(StatGrowth.Evaluate("expeditionCostMod", 50, StatGrowth.Boundaries.Default), 1f);
    }

    // ===== GOVERNMENT =====

    [TestCase(0, 0)]
    [TestCase(24, 0)]
    [TestCase(25, 1)]
    [TestCase(-49, -1)]
    [TestCase(50, 2)]
    [TestCase(-74, -2)]
    [TestCase(75, 3)]
    [TestCase(-300, -3)]
    public void Compass_AxisThresholds(int difference, int coordinate)
    {
        Assert.AreEqual(coordinate, GovernmentCompass.AxisCoordinate(difference, 25, 50, 75));
    }

    [TestCase(0, 0, GovernmentType.TrueCentrist)]
    [TestCase(-1, 0, GovernmentType.WaltzCentrist)]
    [TestCase(2, 0, GovernmentType.RegaliaLeaning)]
    [TestCase(0, 3, GovernmentType.TrueChorus)]
    [TestCase(0, -2, GovernmentType.AureusLeaning)]
    [TestCase(-1, 1, GovernmentType.WaltzChorusCentrist)]
    [TestCase(1, -1, GovernmentType.RegaliaAureusCentrist)]
    [TestCase(-2, 1, GovernmentType.WaltzChorus)]
    [TestCase(3, -1, GovernmentType.RegaliaAureus)]
    [TestCase(2, 3, GovernmentType.Radical)]
    [TestCase(-3, -2, GovernmentType.Radical)]
    [TestCase(3, 3, GovernmentType.FanaticRadical)]
    [TestCase(-3, 3, GovernmentType.FanaticRadical)]
    public void Compass_Classification(int waltzRegalia, int chorusAureus, GovernmentType expected)
    {
        Assert.AreEqual(expected, GovernmentCompass.Classify(waltzRegalia, chorusAureus));
    }

    [Test]
    public void EveryGovernmentTypeHasANameAndDescription()
    {
        foreach (GovernmentType type in Enum.GetValues(typeof(GovernmentType)))
        {
            Assert.AreNotEqual("Unknown", GovernmentCompass.Name(type), type.ToString());
            Assert.AreNotEqual("Unknown government type", GovernmentCompass.Description(type), type.ToString());
        }
    }

    [Test]
    public void EveryCompassCellHasAGovernment()
    {
        for (int x = -3; x <= 3; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                Assert.DoesNotThrow(() => GovernmentCompass.Classify(x, y));
            }
        }
    }
}
