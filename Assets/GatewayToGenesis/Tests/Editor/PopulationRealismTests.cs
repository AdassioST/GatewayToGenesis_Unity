using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class PopulationRealismTests
{
    [TestCase(1)] [TestCase(50)] [TestCase(700)] [TestCase(5000)]
    [TestCase(10000)] [TestCase(50000)] [TestCase(300000)] [TestCase(1000000)]
    public void RationsConserveAnnualEnergyAtEverySettlementScale(int people)
    {
        var t = new GrowthTuning { distributionLoss = 0f };
        double annualFood = GrowthRules.FoodDemand(people, 180f, t) * 180d * 252d;
        double annualEnergy = annualFood * t.kcalPerFood;
        Assert.AreEqual(people * (double)t.daysPerYear * t.kcalPerPersonDay, annualEnergy, annualEnergy * 1e-6);
    }

    [Test]
    public void PopulationAlwaysNeedsFoodIncludingWhenRationTechnologiesAreMaxed()
    {
        var t = new GrowthTuning();
        Assert.AreEqual(1f, GrowthRules.RationsPerDay(t, 0f));
        Assert.AreEqual(1.2f, GrowthRules.RationsPerDay(t, 1f), 1e-6);
        Assert.Greater(GrowthRules.FoodDemand(1, 180f, t, -100f), 0f);
        Assert.AreEqual(GrowthRules.FoodDemand(100, 180f, t) / 3,
            GrowthRules.FoodDemand(100, 540f, t), 1e-6, "calendar slow motion preserves annual intake");
    }

    [Test]
    public void OneCycleIsOneYearAndNot252YearsOfBirths()
    {
        var t = new GrowthTuning();
        double birthCarry = 0, deathCarry = 0;
        int born = 0, died = 0;
        for (int seventh = 0; seventh < 252; seventh++)
        {
            born += GrowthRules.Whole(GrowthRules.VitalEvents(10000, t.birthsPerThousand, 1d / 252), ref birthCarry);
            died += GrowthRules.Whole(GrowthRules.VitalEvents(10000, t.deathsPerThousand, 1d / 252), ref deathCarry);
        }
        Assert.AreEqual(350, born);
        Assert.AreEqual(300, died);
        Assert.AreEqual(0, GrowthRules.VitalEvents(0, t.birthsPerThousand, 100));
    }

    [Test]
    public void CenturySimulationMatchesConfiguredNetRateWithoutInventingAgeCaps()
    {
        int people = 10000;
        double births = 0, deaths = 0;
        for (int i = 0; i < 100 * 252; i++)
        {
            int born = GrowthRules.Whole(GrowthRules.VitalEvents(people, 35f, 1d / 252), ref births);
            int died = GrowthRules.Whole(GrowthRules.VitalEvents(people, 30f, 1d / 252), ref deaths);
            people += born - died;
        }
        Assert.AreEqual(10000 * Math.Exp(0.005 * 100), people, 10,
            "0.5% annual natural growth takes centuries, not minutes, to create a city");
    }

    [Test]
    public void ArrivalsCannotExceedTheirSourceFoodHousingOrPacing()
    {
        Assert.AreEqual(0, GrowthRules.Arrivals(0, 10000, true, 1000000, 12, int.MaxValue));
        Assert.AreEqual(0, GrowthRules.Arrivals(50, 0, false, 1000, 12, 100));
        Assert.AreEqual(2, GrowthRules.Arrivals(50, 100, false, 24, 12, 100));
        Assert.AreEqual(1, GrowthRules.Arrivals(50, 100, false, 2400, 12, 1));
        Assert.AreEqual(50, GrowthRules.Arrivals(50, 100, false, float.MaxValue, 12, int.MaxValue));
    }

    [Test]
    public void SupplyDeterminesCapacityAndAdditionalPeopleDoNotEatExponentially()
    {
        var t = new GrowthTuning();
        float supply = GrowthRules.FoodDemand(10001, 180f, t);
        // The provisioned founders live on top of whatever the supply feeds.
        Assert.That(GrowthRules.SupportedPeople(supply, 180f, t), Is.InRange(10000 + t.provisionedPeople, 10001 + t.provisionedPeople));
        Assert.AreEqual(t.provisionedPeople, GrowthRules.SupportedPeople(-1f, 180f, t));
        Assert.IsFalse(float.IsInfinity(GrowthRules.FoodDemand(int.MaxValue, 180f, t)));
    }

    [Test]
    public void TheFoundersAreProvisionedForGoodAndOnlyLaterMouthsEat()
    {
        var t = new GrowthTuning();
        Assert.AreEqual(21, t.provisionedPeople);
        Assert.AreEqual(0, GrowthRules.Eating(0, t));
        Assert.AreEqual(0, GrowthRules.Eating(21, t), "the 21 founders draw nothing from the daily food");
        Assert.AreEqual(1, GrowthRules.Eating(22, t), "counting starts at the 22nd");
        Assert.AreEqual(79, GrowthRules.Eating(100, t));
        Assert.AreEqual(0f, GrowthRules.FoodDemand(GrowthRules.Eating(15, t), 180f, t), "below 21 another person costs nothing to keep");
    }

    [Test]
    public void SurplusFoodIsBankedBeyondAHandsWorthAsFarAsTheRoomAllows()
    {
        Assert.AreEqual(8f, PantryRules.Banked(20f, 12f, 0f, 200f, 1f));
        Assert.AreEqual(0f, PantryRules.Banked(10f, 12f, 0f, 200f, 1f), "nothing beyond the hand");
        Assert.AreEqual(5f, PantryRules.Banked(40f, 12f, 195f, 200f, 1f), "only as much as there is room for");
        Assert.AreEqual(10f, PantryRules.Banked(40f, 12f, 195f, 200f, 2f), "a richer kind holds more Food in the same room");
        Assert.AreEqual(0f, PantryRules.Banked(40f, 12f, 200f, 200f, 1f));
        Assert.AreEqual(0f, PantryRules.Banked(40f, 12f, 0f, 200f, 0f), "a kind worth nothing stores nothing");
    }

    [Test]
    public void CrowdIsExactForABandBoundedForAMetropolisAndNeverOverrepresents()
    {
        int previous = 0;
        foreach (int people in new[] { 0, 1, 25, 40, 41, 50, 150, 700, 5000, 50000, 300000, 1000000, int.MaxValue })
        {
            int shown = CrowdRules.Near(people, 40) + CrowdRules.Far(people, 40, 160, 20f, 300000);
            Assert.That(shown, Is.InRange(previous, Math.Min(people, 200)));
            if (people <= 40) Assert.AreEqual(people, shown);
            previous = shown;
        }
        Assert.AreEqual(1, CrowdRules.Far(41, 40, 10000, 1f, 42));
    }

    [Test]
    public void RelativeConsequencesParseAndScaleWithResidents()
    {
        var problems = new List<string>();
        var c = EventScript.ParseConsequences("population_percent:population -15; housing_percent:housing -8", problems);
        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual(EventConsequence.ConsequenceType.PopulationPercentChange, c[0].type);
        Assert.AreEqual(EventConsequence.ConsequenceType.HousingPercentChange, c[1].type);
        Assert.AreEqual(7, GrowthRules.PercentOf(50, 15));
        Assert.AreEqual(1500, GrowthRules.PercentOf(10000, 15));
        Assert.AreEqual(10000, GrowthRules.PercentOf(10000, 500));
        StringAssert.Contains("15%", EventText.DescribeConsequence(c[0]));
    }

    [Test]
    public void ShippedPhysicalPressuresHaveNoSmallPopulationImmunityAndUseAnnualRates()
    {
        var settings = Resources.Load<HealthSettings>("Population/Health");
        Assert.IsNotNull(settings);
        foreach (var spec in settings.pressures.Where(p => p.pressure != HealthPressure.HarmonicStability))
        {
            Assert.AreEqual(1f, HealthRules.Emergence(spec, 5));
            Assert.LessOrEqual(spec.deathsPerSeventh * 252f, 0.031f);
            Assert.IsTrue(spec.treatments.All(t => t.capacity == 0f));
        }
        var growth = Resources.Load<GrowthSettings>("Population/Growth");
        Assert.IsNotNull(growth);
        Assert.AreEqual(2100f, growth.tuning.kcalPerFood);
        GameSnapshot.ValidateSchema();
    }
}

/// <summary>
/// The frontier: a founding band of 21 grows at a playable pace (births in minutes, caravans drawn by its stores,
/// survivors found by expeditions) and hands over to the historical rates once it is a village.
/// </summary>
public class FrontierGrowthTests
{
    private static GrowthTuning Shipped() => new GrowthTuning();

    [Test]
    public void FrontierPacingIsFullForTheFoundersAndGoneByAVillage()
    {
        var t = Shipped();
        Assert.AreEqual(21, t.foundingMigrants);
        Assert.AreEqual(1f, GrowthRules.Frontier(0, t));
        Assert.AreEqual(1f, GrowthRules.Frontier(21, t));
        Assert.AreEqual(0f, GrowthRules.Frontier(t.realismFrom, t));
        Assert.AreEqual(0f, GrowthRules.Frontier(10000, t));
        float previous = 1f;
        for (int people = 22; people < t.realismFrom; people++)
        {
            float f = GrowthRules.Frontier(people, t);
            Assert.That(f, Is.InRange(0f, previous), $"{people} people");
            previous = f;
        }
        Assert.AreEqual(t.frontierBirthPace, GrowthRules.BirthPace(21, t), 1e-4);
        Assert.AreEqual(1f, GrowthRules.BirthPace(t.realismFrom, t));
    }

    // At 180 seconds a Seventh: the founders' first child in about a quarter of an hour, not seven hours.
    [Test]
    public void TheFoundersFirstChildComesInMinutesNotHours()
    {
        var t = Shipped();
        int Sevenths(float pace)
        {
            double carry = 0;
            for (int s = 1; s < 100000; s++)
                if (GrowthRules.Whole(GrowthRules.VitalEvents(21, t.birthsPerThousand, 1d / GrowthRules.SeventhsPerYear) * pace, ref carry) > 0) return s;
            return int.MaxValue;
        }
        int frontier = Sevenths(GrowthRules.BirthPace(21, t));
        Assert.LessOrEqual(frontier, 5, "about 15 minutes of play");
        Assert.Greater(Sevenths(1f), 300, "history's pace alone takes well over a Cycle");
    }

    [Test]
    public void AFrontierBandGrowsToAVillageThenFollowsHistory()
    {
        var t = Shipped();
        int people = 21, sevenths = 0;
        double births = 0, deaths = 0;
        while (people < t.realismFrom && sevenths < 20 * GrowthRules.SeventhsPerYear)
        {
            people += GrowthRules.Whole(GrowthRules.VitalEvents(people, t.birthsPerThousand, 1d / GrowthRules.SeventhsPerYear) * GrowthRules.BirthPace(people, t), ref births)
                    - GrowthRules.Whole(GrowthRules.VitalEvents(people, t.deathsPerThousand, 1d / GrowthRules.SeventhsPerYear), ref deaths);
            sevenths++;
        }
        Assert.GreaterOrEqual(people, t.realismFrom, "births alone carry the band to a village");
        Assert.Greater(sevenths, GrowthRules.SeventhsPerYear, "but not in an afternoon: caravans and expeditions matter");
        Assert.AreEqual(1f, GrowthRules.BirthPace(people, t), "from there on, only the historical rates");
    }

    [Test]
    public void CaravansFollowTheStoresAndStopAtAVillage()
    {
        var t = Shipped();
        float rations = GrowthRules.RationsPerDay(t);
        Assert.AreEqual(0f, GrowthRules.CaravanPull(21, 0f, rations, t), "no one walks toward an empty granary");
        Assert.AreEqual(1f, GrowthRules.CaravanPull(21, 21 * rations * t.caravanReserveDays, rations, t), 1e-5);
        Assert.AreEqual(0.5f, GrowthRules.CaravanPull(21, 21 * rations * t.caravanReserveDays / 2, rations, t), 1e-5);
        Assert.AreEqual(0f, GrowthRules.CaravanPull(t.realismFrom, 1e9f, rations, t));
        Assert.AreEqual(t.caravansPerCycle / GrowthRules.SeventhsPerYear, GrowthRules.CaravanProgress(1f, t), 1e-9);
        var sizes = Enumerable.Range(0, 200).Select(i => GrowthRules.CaravanSize(i, t)).ToList();
        Assert.IsTrue(sizes.All(s => s >= t.caravanMin && s <= t.caravanMax));
        Assert.Greater(sizes.Distinct().Count(), 3);
        Assert.AreEqual(GrowthRules.CaravanSize(7, t), GrowthRules.CaravanSize(7, t));
    }

    [Test]
    public void ACellShelteringSurvivorsYieldsOneBandHoweverItIsExplored()
    {
        var x = new ExpeditionSettings();
        for (int i = 0; i < 1000; i++)
        {
            double draw = i / 1000d;
            int passing = Expeditions.SurvivorBand(x, false, false, "taiga", draw, 0.5);
            int surveyAfter = Expeditions.SurvivorBand(x, true, true, "taiga", draw, 0.5);
            int surveyFirst = Expeditions.SurvivorBand(x, true, false, "taiga", draw, 0.5);
            Assert.IsFalse(passing > 0 && surveyAfter > 0, "never twice from one cell");
            Assert.AreEqual(surveyFirst > 0, passing > 0 || surveyAfter > 0, "a survey after a passing finds exactly what a first survey would");
        }
        int Found(string biome) => Enumerable.Range(0, 1000).Count(i => Expeditions.SurvivorBand(x, true, false, biome, i / 1000d, 0.5) > 0);
        Assert.Greater(Found("survivor-architecture"), Found("taiga"));
        Assert.Less(Found("the-golden-ash"), Found("taiga"));
        Assert.AreEqual(x.survivorBandMin, Expeditions.SurvivorBand(x, true, false, "taiga", 0d, 0d));
        Assert.AreEqual(x.survivorBandMax, Expeditions.SurvivorBand(x, true, false, "taiga", 0d, 0.9999));
        Assert.AreEqual(1, Expeditions.SurvivorTravel(x, 0));
        Assert.AreEqual(6, Expeditions.SurvivorTravel(x, 6));
    }

    [Test]
    public void EraScoreIsAStoryConsequence()
    {
        var c = EventScript.ParseConsequence("era_score:The first child born +1");
        Assert.AreEqual(EventConsequence.ConsequenceType.EraScoreChange, c.type);
        Assert.AreEqual("The first child born", c.targetName);
        Assert.AreEqual(1, c.value);
        StringAssert.Contains("Era Score", EventText.DescribeConsequence(c));
    }

    [Test]
    public void TheFirstBirthIsAStoryWorthOneEraScoreAndMoraleOnEveryPath()
    {
        EventStoryIndex.Clear();
        try
        {
            var asset = Resources.Load<TextAsset>("Events/StarterVolume");
            Assert.IsNotNull(asset);
            var volume = InkDrivenEventSetup.CreateVolume(asset);
            var node = volume.storyNodes.Single(n => n.nodeName == "first_birth");
            Assert.IsTrue(node.storyConditions.Any(c => c.Domain == "births"), "it waits for a birth, not an arrival");
            var chorus = EventStoryIndex.Get(EventStoryIndex.Get(node.nodeName).ContinueTarget);
            Assert.AreEqual(2, chorus.chorusChoices.Count);
            foreach (var choice in chorus.chorusChoices)
            {
                var effects = EventStoryIndex.ConsequencesAlong(choice.destinationPath, out _);
                Assert.AreEqual(1, effects.Where(e => e.type == EventConsequence.ConsequenceType.EraScoreChange).Sum(e => e.value), choice.destinationPath);
                Assert.Greater(effects.Where(e => e.type == EventConsequence.ConsequenceType.StatChange && e.targetName == "morale").Sum(e => e.value), 0, choice.destinationPath);
            }
        }
        finally { EventStoryIndex.Clear(); }
    }
}
