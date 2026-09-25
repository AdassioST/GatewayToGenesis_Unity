using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Pure-logic tests for the game rules that live outside the scene: weather selection (<see cref="WeatherRules"/>),
/// the council (<see cref="CouncilRules"/>), timers (<see cref="Countdowns{TKey}"/>), civic requirements and log
/// channels. Run from Window > General > Test Runner > EditMode.
/// </summary>
public class GameRulesTests
{
    // ===== COUNTDOWNS =====

    [Test]
    public void Countdowns_TickDownAndReportWhatExpired()
    {
        var timers = new Countdowns<string>();
        timers.Start("Storm", 2);
        timers.Start("Mist", 1);

        CollectionAssert.AreEquivalent(new[] { "Mist" }, timers.Tick());
        Assert.AreEqual(1, timers.Remaining("Storm"));
        Assert.IsFalse(timers.IsRunning("Mist"));

        CollectionAssert.AreEquivalent(new[] { "Storm" }, timers.Tick());
        Assert.AreEqual(0, timers.Count);
    }

    [Test]
    public void Countdowns_RestartReplacesAndZeroClears()
    {
        var timers = new Countdowns<int>();
        timers.Start(3, 1);
        timers.Start(3, 5);
        Assert.AreEqual(5, timers.Remaining(3));
        timers.Start(3, 0);
        Assert.IsFalse(timers.IsRunning(3));
        Assert.AreEqual(0, timers.Remaining(99));
    }

    // ===== WEATHER =====

    [Test]
    public void Weather_RetentionChanceDecaysPerSeventhAndStopsAtZero()
    {
        Assert.AreEqual(90f, WeatherRules.RetentionChance(90f, 0, 2f), 1e-4);
        Assert.AreEqual(80f, WeatherRules.RetentionChance(90f, 5, 2f), 1e-4);
        Assert.AreEqual(0f, WeatherRules.RetentionChance(90f, 100, 2f), 1e-4);
    }

    [TestCase(80f, 3, 20, 80f, WeatherRules.Retention.Kept)]    // roll at the chance keeps it
    [TestCase(80f, 3, 20, 80.1f, WeatherRules.Retention.Lost)]
    [TestCase(80f, 20, 20, 0f, WeatherRules.Retention.Forced)]  // retention limit reached
    [TestCase(0f, 3, 20, 0f, WeatherRules.Retention.Forced)]    // chance decayed away
    [TestCase(80f, 500, 0, 10f, WeatherRules.Retention.Kept)]   // 0 = no retention limit
    public void Weather_RetentionOutcome(float chance, int retentions, int limit, float roll, WeatherRules.Retention expected)
    {
        Assert.AreEqual(expected, WeatherRules.CheckRetention(chance, retentions, limit, roll));
    }

    [TestCase(14, 50f)]   // below the threshold: base weight
    [TestCase(15, 51.2f)] // exactly at the threshold: first step
    [TestCase(24, 62f)]   // ten steps
    [TestCase(200, 100f)] // capped at 2 × base
    public void Weather_VariationBoostGrowsLinearlyUpToTheCap(int seventhsSinceSeen, float expected)
    {
        Assert.AreEqual(expected, WeatherRules.VariationWeight(50f, seventhsSinceSeen, 15, 1.2f, 2f), 1e-3);
    }

    [Test]
    public void Weather_ZeroWeightIsNeverBoosted()
    {
        Assert.AreEqual(0f, WeatherRules.VariationWeight(0f, 500, 15, 1.2f, 2f));
    }

    [Test]
    public void Weather_CandidacyChecksWeightThenActiveThenCooldownThenConditions()
    {
        Assert.AreEqual(WeatherRules.Candidacy.NoWeight, WeatherRules.CheckCandidate(0f, true, true, false));
        Assert.AreEqual(WeatherRules.Candidacy.Active, WeatherRules.CheckCandidate(10f, true, true, false));
        Assert.AreEqual(WeatherRules.Candidacy.OnCooldown, WeatherRules.CheckCandidate(10f, false, true, false));
        Assert.AreEqual(WeatherRules.Candidacy.ConditionsNotMet, WeatherRules.CheckCandidate(10f, false, false, false));
        Assert.AreEqual(WeatherRules.Candidacy.Candidate, WeatherRules.CheckCandidate(10f, false, false, true));
    }

    [TestCase(0f, 0)]
    [TestCase(0.249f, 0)]
    [TestCase(0.25f, 1)]
    [TestCase(0.999f, 1)]
    [TestCase(1f, 1)] // clamped into range
    public void Weather_PickWeightedFollowsCumulativeWeights(float roll, int expected)
    {
        Assert.AreEqual(expected, WeatherRules.PickWeighted(new[] { 10f, 30f }, roll));
    }

    [Test]
    public void Weather_PickWeightedHandlesEmptyAndWeightlessPools()
    {
        Assert.AreEqual(-1, WeatherRules.PickWeighted(new float[0], 0.5f));
        Assert.AreEqual(2, WeatherRules.PickWeighted(new[] { 0f, 0f, 0f }, 0.9f));
        Assert.AreEqual(1, WeatherRules.PickWeighted(new[] { 0f, 5f, 0f }, 0.9f));
    }

    [Test]
    public void Weather_ChancesMatchThePickAndAddUpToOneHundred()
    {
        var weights = new[] { 10f, 30f, 60f };
        Assert.AreEqual(100f, Enumerable.Range(0, weights.Length).Sum(i => WeatherRules.Chance(weights, i)), 1e-3);

        // The share of a fine grid of rolls each index wins equals its displayed chance.
        const int steps = 10000;
        var wins = new int[weights.Length];
        for (int s = 0; s < steps; s++) wins[WeatherRules.PickWeighted(weights, (s + 0.5f) / steps)]++;
        for (int i = 0; i < weights.Length; i++) Assert.AreEqual(WeatherRules.Chance(weights, i), wins[i] * 100f / steps, 0.05, $"index {i}");
    }

    // ===== COUNCIL =====

    private static SeatBonus Seat(float waltz) => new SeatBonus { bonusType = SeatBonusType.PillarBonus, targetStat = "waltz", modifierValue = waltz };

    private static LegendBonus Legend(GameEffectType type, string target, float value) => new LegendBonus { bonusType = type, targetStat = target, modifierValue = value };

    private static CouncilRules.SeatState State(bool hasLegend, int seventhsUntilActive, bool headOfState = false) => new CouncilRules.SeatState
    {
        seatSource = "Council Seat: Test",
        seatBonuses = new[] { Seat(2f) },
        hasLegend = hasLegend,
        legendSource = hasLegend ? "Legend: Test" : null,
        legendBonuses = hasLegend ? new[] { Legend(GameEffectType.PillarBonus, "chorus", 3f) } : null,
        seventhsUntilActive = seventhsUntilActive,
        isHeadOfState = headOfState
    };

    [Test]
    public void Council_EmptySeatGivesNothing()
    {
        CollectionAssert.IsEmpty(CouncilRules.Collect(new[] { State(false, 0) }, 2f));
    }

    [Test]
    public void Council_SeatBonusesApplyTheMomentALegendIsSeated()
    {
        var effects = CouncilRules.Collect(new[] { State(true, 3) }, 2f);
        Assert.AreEqual(1, effects.Count, "only the seat's own bonus while the legend is still activating");
        Assert.IsFalse(effects[0].IsLegendBonus);
        Assert.AreEqual("waltz", effects[0].effect.target);
    }

    [Test]
    public void Council_LegendBonusesApplyOnceActivationFinishes()
    {
        var effects = CouncilRules.Collect(new[] { State(true, 0) }, 2f);
        Assert.AreEqual(2, effects.Count);
        Assert.AreEqual(1, effects.Count(e => e.IsLegendBonus && e.effect.target == "chorus"));
    }

    [Test]
    public void Council_HeadOfStateMultipliesLegendBonusesButNeverSeatBonuses()
    {
        var effects = CouncilRules.Collect(new[] { State(true, 0, headOfState: true) }, 2f);
        var legend = effects.Single(e => e.IsLegendBonus);
        var seat = effects.Single(e => !e.IsLegendBonus);
        Assert.AreEqual(2f * 1.5f, CouncilRules.Multiplier(legend, 1.5f), 1e-4);
        Assert.AreEqual(1f, CouncilRules.Multiplier(seat, 1.5f), 1e-4);
    }

    [Test]
    public void Council_AuthorityAndLegendEffectivenessAreNotScaledByLegendEffectiveness()
    {
        var authority = new CouncilRules.CouncilEffect("Legend: A", new GameEffect(GameEffectType.SubstatBonus, 1f, ModifierType.Add, "authority"), 1f);
        var effectiveness = new CouncilRules.CouncilEffect("Legend: A", new GameEffect(GameEffectType.DerivedStatBonus, 5f, ModifierType.Add, "legendEffectiveness"), 1f);
        var other = new CouncilRules.CouncilEffect("Legend: A", new GameEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "waltz"), 1f);

        Assert.AreEqual(CouncilRules.Phase.Authority, authority.Phase);
        Assert.AreEqual(CouncilRules.Phase.LegendEffectiveness, effectiveness.Phase);
        Assert.AreEqual(CouncilRules.Phase.Scaled, other.Phase);
        Assert.AreEqual(1f, CouncilRules.Multiplier(authority, 1.3f), 1e-4);
        Assert.AreEqual(1f, CouncilRules.Multiplier(effectiveness, 1.3f), 1e-4);
        Assert.AreEqual(1.3f, CouncilRules.Multiplier(other, 1.3f), 1e-4);
    }

    [TestCase(0f, 1f)]
    [TestCase(29.6f, 1.3f)]
    [TestCase(-40f, 1f)] // negative effectiveness never shrinks bonuses
    public void Council_EffectivenessMultiplier(float legendEffectiveness, float expected)
    {
        Assert.AreEqual(expected, CouncilRules.EffectivenessMultiplier(legendEffectiveness), 1e-4);
    }

    [Test]
    public void Council_CivicBonusMarkerIsNotAnEffect()
    {
        var state = State(true, 0);
        state.seatBonuses = new[] { new SeatBonus { bonusType = SeatBonusType.CivicBonus } };
        Assert.IsFalse(CouncilRules.Collect(new[] { state }, 1f).Any(e => !e.IsLegendBonus));
    }

    [Test]
    public void Council_HeadOfStateHasItsOwnCooldown()
    {
        Assert.AreEqual(3, CouncilRules.CooldownFor(GovernmentLogic.HeadOfStateIndex, 1, 3));
        Assert.AreEqual(1, CouncilRules.CooldownFor(2, 1, 3));
    }

    //                targetCd eligible already hasPrev prevCd occupied fits  → expected
    [TestCase(true,  true,  false, false, false, false, false, CouncilRules.Assignment.TargetOnCooldown)]
    [TestCase(false, false, false, false, false, false, false, CouncilRules.Assignment.NotEligible)]
    [TestCase(false, true,  true,  true,  true,  true,  true,  CouncilRules.Assignment.AlreadySeated)]
    [TestCase(false, true,  false, true,  true,  true,  true,  CouncilRules.Assignment.PreviousSeatOnCooldown)]
    [TestCase(false, true,  false, true,  false, true,  true,  CouncilRules.Assignment.Swap)]
    [TestCase(false, true,  false, true,  false, true,  false, CouncilRules.Assignment.Move)]  // occupant does not fit: it is unseated
    [TestCase(false, true,  false, false, false, true,  false, CouncilRules.Assignment.Move)]  // new legend replaces the occupant
    [TestCase(false, true,  false, true,  false, false, false, CouncilRules.Assignment.Move)]  // moves into an empty seat
    public void Council_AssignmentPlan(bool targetOnCooldown, bool eligible, bool alreadyInTarget, bool hasPreviousSeat,
        bool previousOnCooldown, bool targetOccupied, bool occupantFits, CouncilRules.Assignment expected)
    {
        Assert.AreEqual(expected, CouncilRules.PlanAssignment(targetOnCooldown, eligible, alreadyInTarget, hasPreviousSeat, previousOnCooldown, targetOccupied, occupantFits));
    }

    // ===== CIVIC REQUIREMENTS =====

    [Test]
    public void EveryCivicRequirementBecomesAnEventConditionOnARegisteredValue()
    {
        foreach (RequirementType type in Enum.GetValues(typeof(RequirementType)))
        {
            var condition = new CivicRequirement { requirementType = type, requirementTarget = "x", requiredValue = 1 }.ToCondition();
            if (type == RequirementType.EraUnlock)
            {
                Assert.IsNull(condition, "eras are not implemented; EraUnlock has no condition");
                continue;
            }
            Assert.IsNotNull(condition, $"{type} has no condition: map it in CivicRequirement.ToCondition");
            Assert.IsTrue(GameValues.IsKnownDomain(condition.Domain), $"{type} reads unregistered domain '{condition.Domain}'");
        }
    }

    [Test]
    public void CivicRequirementsAreWordedLikeEventRequirements()
    {
        Assert.AreEqual("Needs At Least 10 Regalia", Requirement(RequirementType.PillarStat, "regalia", 10, ComparisonType.GreaterEqual));
        Assert.AreEqual("Needs More Than 5 Waltz", Requirement(RequirementType.PillarStat, "waltz", 5, ComparisonType.GreaterThan));
        Assert.AreEqual("Needs Guild Masters", Requirement(RequirementType.CivicPresent, "Guild Masters"));
        Assert.AreEqual("Needs No Guild Masters", Requirement(RequirementType.CivicAbsent, "Guild Masters"));
        Assert.AreEqual("Needs At Least 5 Morale", Requirement(RequirementType.MoraleLevel, null, 5, ComparisonType.GreaterEqual));
        Assert.AreEqual("Needs At Most 2 Satisfaction", Requirement(RequirementType.SatisfactionLevel, null, 2, ComparisonType.LessEqual));
        Assert.AreEqual($"Needs A {GovernmentCompass.Name(GovernmentType.TrueCentrist)} Government", Requirement(RequirementType.GovernmentType, "TrueCentrist"));
    }

    private static string Requirement(RequirementType type, string target, float value = 0, ComparisonType comparison = ComparisonType.GreaterEqual)
    {
        return new CivicRequirement { requirementType = type, requirementTarget = target, requiredValue = value, comparison = comparison }.GetAutoDescription();
    }

    // ===== LOGGING =====

    [Test]
    public void EveryLogChannelIsItsOwnFlag()
    {
        var channels = Enum.GetValues(typeof(LogChannel)).Cast<LogChannel>().Where(c => c != LogChannel.None).ToList();
        foreach (var channel in channels)
        {
            int bits = (int)channel;
            Assert.IsTrue(bits > 0 && (bits & (bits - 1)) == 0, $"{channel} must be a single bit");
        }
        Assert.AreEqual(channels.Count, channels.Distinct().Count());
    }

    [Test]
    public void GameLog_OnlyEnabledChannelsAreOn()
    {
        var previous = GameLog.Enabled;
        try
        {
            GameLog.Enabled = LogChannel.Weather | LogChannel.Council;
            Assert.IsTrue(GameLog.IsEnabled(LogChannel.Weather));
            Assert.IsTrue(GameLog.IsEnabled(LogChannel.Council));
            Assert.IsFalse(GameLog.IsEnabled(LogChannel.Events));
            GameLog.Enabled = LogChannel.None;
            Assert.IsFalse(GameLog.IsEnabled(LogChannel.Weather));
        }
        finally
        {
            GameLog.Enabled = previous;
        }
    }
}
