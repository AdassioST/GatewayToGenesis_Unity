using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The first playable loop's rules, with no scene: the shape of an Age in time (<see cref="AgeRules"/>), the Age
/// Crisis in numbers (<see cref="CrisisRules"/>), hexes (<see cref="HexCoord"/>; the world itself is in
/// <c>WorldGenerationTests</c>) and how legends grow (<see cref="LegendGrowthRules"/>, <see cref="CouncilRules"/>).
/// </summary>
public class GenesisLoopTests
{
    private static readonly List<int> ThreeActs = new List<int> { 63, 63, 63 };

    // ===== AGES =====

    [Test]
    public void Age_LengthActsAndProgress()
    {
        Assert.AreEqual(189, AgeRules.Length(ThreeActs));
        Assert.AreEqual(0, AgeRules.ActAt(0, ThreeActs));
        Assert.AreEqual(0, AgeRules.ActAt(62, ThreeActs));
        Assert.AreEqual(1, AgeRules.ActAt(63, ThreeActs));
        Assert.AreEqual(2, AgeRules.ActAt(188, ThreeActs));
        Assert.AreEqual(2, AgeRules.ActAt(500, ThreeActs), "the last Act once the Age is spent");
        Assert.AreEqual(126, AgeRules.ActStart(2, ThreeActs));
        Assert.AreEqual(0.5f, AgeRules.Progress(94, new List<int> { 94, 94 }), 1e-5);
        Assert.AreEqual(1f, AgeRules.Progress(999, ThreeActs), 1e-5);
    }

    [Test]
    public void Age_ScheduleHasActsOfFateAtBoundariesTheCrisisFromHalfwayAndTheEndLast()
    {
        var schedule = AgeRules.Schedule(ThreeActs, new List<float> { 0.5f, 0.67f, 0.85f });
        var kinds = schedule.Select(b => $"{b.kind}{b.index}@{b.at}").ToList();
        CollectionAssert.AreEqual(new[]
        {
            "ActOfFate1@63",
            "CrisisStage0@95",  // 0.5 × 189 = 94.5, rounded away from zero
            "ActOfFate2@126",
            "CrisisStage1@127", // 0.67 × 189 = 126.63
            "CrisisStage2@161", // 0.85 × 189 = 160.65
            "AgeEnds0@189",
        }, kinds);
    }

    [Test]
    public void Age_FourActsForTheLongAges()
    {
        var schedule = AgeRules.Schedule(new List<int> { 50, 50, 50, 50 }, null);
        Assert.AreEqual(3, schedule.Count(b => b.kind == AgeBeatKind.ActOfFate));
        Assert.AreEqual(AgeBeatKind.AgeEnds, schedule.Last().kind);
    }

    [Test]
    public void Age_StagesKeepTheirOrderAndNeverFallOnTheFirstSeventhOrAfterTheEnd()
    {
        var schedule = AgeRules.Schedule(ThreeActs, new List<float> { 0.8f, 0.5f, 0f, 2f });
        var stages = schedule.Where(b => b.kind == AgeBeatKind.CrisisStage).ToList();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, stages.Select(s => s.index), "authored order");
        Assert.IsTrue(stages.Zip(stages.Skip(1), (a, b) => a.at <= b.at).All(x => x), "never earlier than the stage before");
        Assert.IsTrue(stages.All(s => s.at >= 1 && s.at <= 189));
        Assert.AreEqual(AgeBeatKind.AgeEnds, schedule.Last().kind, "a stage on the last seventh still comes before the end");
    }

    [Test]
    public void Age_EveryBeatIsDueExactlyOnceHoweverTheClockMoves()
    {
        var schedule = AgeRules.Schedule(ThreeActs, new List<float> { 0.5f, 0.75f });
        int next = 0;
        var seen = new List<AgeBeat>();
        // One seventh at a time, then a big jump, then past the end twice.
        for (int s = 1; s <= 100; s++) seen.AddRange(AgeRules.Due(schedule, ref next, s));
        seen.AddRange(AgeRules.Due(schedule, ref next, 150));
        seen.AddRange(AgeRules.Due(schedule, ref next, 400));
        seen.AddRange(AgeRules.Due(schedule, ref next, 400));
        CollectionAssert.AreEqual(schedule, seen);
    }

    [TestCase(0, "0")]
    [TestCase(1, "I")]
    [TestCase(4, "IV")]
    [TestCase(9, "IX")]
    [TestCase(14, "XIV")]
    public void Age_RomanNumeralsAsTheVaultWritesThem(int number, string expected)
    {
        Assert.AreEqual(expected, AgeRules.Roman(number));
    }

    // ===== THE CRISIS =====

    private static CrisisTuning Tuning() => new CrisisTuning();

    [Test]
    public void Crisis_SeverityIsTheSumOfNamedFactorsClampedToOne()
    {
        var inputs = new CrisisInputs { population = 150, foodStored = 0f };
        var factors = CrisisRules.Factors(inputs, Tuning(), CrisisRules.Labels.Default);
        Assert.AreEqual("The crisis itself", factors[0].label);
        Assert.AreEqual(0.55f + 0.15f, CrisisRules.Severity(factors), 1e-4, "base + full crowding");

        inputs.aggravationPoints = 10;
        Assert.AreEqual(1f, CrisisRules.Severity(CrisisRules.Factors(inputs, Tuning(), CrisisRules.Labels.Default)), 1e-4, "never above 1");
    }

    [Test]
    public void Crisis_PreparationReliefReserveAndLandEaseItWithinTheirCaps()
    {
        var inputs = new CrisisInputs
        {
            population = 0,
            preparedTechnologies = 3,  // -0.24
            preparationPoints = 99,    // capped at -0.25
            reliefPoints = 2,          // -0.08
            easingTiles = 10,          // capped at -0.15
        };
        var factors = CrisisRules.Factors(inputs, Tuning(), CrisisRules.Labels.Default);
        Assert.AreEqual(0f, CrisisRules.Severity(factors), 1e-4, "0.55 - 0.24 - 0.25 - 0.08 - 0.15 (and a full reserve with no people) is below 0");
        Assert.AreEqual(-0.25f, factors.Single(f => f.label == "Preparation").value, 1e-4);
        Assert.AreEqual(-0.15f, factors.Single(f => f.label == "Explored land").value, 1e-4);
    }

    [TestCase(100, 0f, 10)]   // minimum loss 10%
    [TestCase(100, 1f, 60)]   // maximum loss 60%
    [TestCase(100, 0.5f, 35)]
    [TestCase(8, 1f, 3)]      // the survivor floor (5) always lives
    [TestCase(3, 1f, 0)]
    [TestCase(0, 1f, 0)]
    public void Crisis_DeathsScaleWithSeverityAndNeverTakeTheSurvivorFloor(int population, float severity, int expected)
    {
        Assert.AreEqual(expected, CrisisRules.Deaths(population, severity, Tuning()));
    }

    [Test]
    public void Crisis_BothPreparedAndPoorRoutesReachTheNextAgeWithDifferentSurvivors()
    {
        var prepared = new CrisisInputs { population = 120, foodStored = 480f, preparedTechnologies = 3, preparationPoints = 3, easingTiles = 2 };
        var poor = new CrisisInputs { population = 120, foodStored = 0f, aggravationPoints = 3 };
        float good = CrisisRules.Severity(CrisisRules.Factors(prepared, Tuning(), CrisisRules.Labels.Default));
        float bad = CrisisRules.Severity(CrisisRules.Factors(poor, Tuning(), CrisisRules.Labels.Default));
        var next = new List<(string, float)> { ("age-of-renewal", 1f) };
        Assert.AreEqual("age-of-renewal", CrisisRules.NextAge(next, good));
        Assert.AreEqual("age-of-renewal", CrisisRules.NextAge(next, bad), "the famine always leads to the Age of Renewal");
        Assert.Greater(CrisisRules.Deaths(120, bad, Tuning()), CrisisRules.Deaths(120, good, Tuning()));
    }

    [Test]
    public void Crisis_NextAgeByOutcomeBandsWithTheLastAsFallback()
    {
        var bands = new List<(string, float)> { ("golden", 0.35f), ("classical", 0.65f), ("dark", 1f) };
        Assert.AreEqual("golden", CrisisRules.NextAge(bands, 0.2f));
        Assert.AreEqual("classical", CrisisRules.NextAge(bands, 0.35001f));
        Assert.AreEqual("dark", CrisisRules.NextAge(bands, 0.9f));
        Assert.AreEqual("dark", CrisisRules.NextAge(new List<(string, float)> { ("golden", 0.1f), ("dark", 0.2f) }, 0.9f));
        Assert.IsNull(CrisisRules.NextAge(new List<(string, float)>(), 0.5f));
    }

    [Test]
    public void Crisis_TheHarvestTurnsPinkWhenItBeginsAndBrownWhenDeclared()
    {
        Assert.AreEqual(HarvestQuality.Golden, CrisisRules.Harvest(false, false));
        Assert.AreEqual(HarvestQuality.Pink, CrisisRules.Harvest(true, false));
        Assert.AreEqual(HarvestQuality.Brown, CrisisRules.Harvest(true, true));
        Assert.AreEqual(-20f, CrisisRules.FoodOutputPercent(0.5f, Tuning()), 1e-4);
    }

    // ===== HEXES =====

    [Test]
    public void Hex_NeighboursDistancesAndRings()
    {
        var origin = HexCoord.Zero;
        Assert.AreEqual(6, origin.Neighbors().Distinct().Count());
        Assert.IsTrue(origin.Neighbors().All(n => HexCoord.Distance(origin, n) == 1));
        Assert.AreEqual(3, HexCoord.Distance(new HexCoord(1, -2), new HexCoord(-1, 1)));
        Assert.AreEqual(1, HexCoord.Ring(origin, 0).Count);
        Assert.AreEqual(18, HexCoord.Ring(origin, 3).Count);
        Assert.IsTrue(HexCoord.Ring(origin, 3).All(h => HexCoord.Distance(origin, h) == 3));
        Assert.AreEqual(HexCoord.CountWithin(6), HexCoord.Spiral(origin, 6).Distinct().Count());
        Assert.AreEqual(127, HexCoord.CountWithin(6));
    }

    [Test]
    public void Hex_PixelsRoundTripForEveryHex()
    {
        foreach (var hex in HexCoord.Spiral(HexCoord.Zero, 8))
        {
            hex.ToPixel(37f, out float x, out float y);
            Assert.AreEqual(hex, HexCoord.FromPixel(x, y, 37f), $"centre of {hex}");
            Assert.AreEqual(hex, HexCoord.FromPixel(x + 10f, y - 12f, 37f), $"inside {hex}");
        }
    }

    [Test]
    public void Hex_QuadrantsSplitTheWorldIntoFourQuarters()
    {
        var counts = HexCoord.Spiral(HexCoord.Zero, 6).Where(h => h != HexCoord.Zero).GroupBy(h => h.Quadrant()).ToDictionary(g => g.Key, g => g.Count());
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, counts.Keys);
        Assert.IsTrue(counts.Values.All(c => c >= 25), "roughly a quarter each");
        Assert.AreEqual(3, new HexCoord(1, 1).Quadrant(), "south-east (y grows up, r grows down)");
    }

    // ===== LEGENDS =====

    [TestCase(0, 1)]
    [TestCase(9, 1)]
    [TestCase(10, 2)]
    [TestCase(49, 3)]
    [TestCase(100, 5)]
    [TestCase(10000, 5)]
    public void Legend_LyricalFragmentsSetTheRank(int fragments, int rank)
    {
        Assert.AreEqual(rank, LegendGrowthRules.RankFor(fragments));
    }

    [Test]
    public void Legend_RankGrowsTheLegendsOwnCouncilBonusesButNeverTheSeats()
    {
        Assert.AreEqual(1f, LegendGrowthRules.Multiplier(1), 1e-5);
        Assert.AreEqual(1.8f, LegendGrowthRules.Multiplier(5), 1e-5);
        Assert.AreEqual(25, LegendGrowthRules.NextThreshold(2));
        Assert.AreEqual(-1, LegendGrowthRules.NextThreshold(5));

        var state = new CouncilRules.SeatState
        {
            seatSource = "Council Seat: X",
            seatBonuses = new[] { new SeatBonus { bonusType = SeatBonusType.PillarBonus, modifierValue = 1, targetStat = "waltz" } },
            hasLegend = true,
            legendSource = "Legend: A",
            legendBonuses = new[] { new LegendBonus { bonusType = GameEffectType.PillarBonus, modifierValue = 2, targetStat = "chorus" } },
            isHeadOfState = true,
            legendGrowth = LegendGrowthRules.Multiplier(3),
        };
        var effects = CouncilRules.Collect(new[] { state }, 2f);
        Assert.AreEqual(2f * 1.4f, CouncilRules.Multiplier(effects.Single(e => e.IsLegendBonus), 1f), 1e-4, "Head of State × growth");
        Assert.AreEqual(1f, CouncilRules.Multiplier(effects.Single(e => !e.IsLegendBonus), 1f), 1e-4);

        state.legendGrowth = 0f; // not set: counts as 1
        Assert.AreEqual(2f, CouncilRules.Multiplier(CouncilRules.Collect(new[] { state }, 2f).Single(e => e.IsLegendBonus), 1f), 1e-4);
    }

    [Test]
    public void Legend_TheStartingRosterIsTheCommonestFirst()
    {
        var roster = LegendGrowthRules.StartingRoster(new[] { ("Zed", 0), ("Ann", 3), ("Bea", 0), ("Cid", 1) }, 3);
        CollectionAssert.AreEqual(new[] { "Bea", "Zed", "Cid" }, roster);
    }
}
