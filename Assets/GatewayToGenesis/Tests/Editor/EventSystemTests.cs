using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Pure-logic tests for the event layer (no scene needed): the Ink grammar (<see cref="EventScript"/>), the
/// chorus dice (<see cref="ChorusRules"/>), the player-facing wording (<see cref="EventText"/>), sentence
/// splitting and tooltip formatting. Live game values are stubbed so totals are deterministic.
/// </summary>
public class EventSystemTests
{
    private Func<EventConsequence, int?> _liveValues;

    [SetUp]
    public void StubLiveValues()
    {
        _liveValues = EventText.CurrentValue;
        EventText.CurrentValue = c => null;
    }

    [TearDown]
    public void RestoreLiveValues() => EventText.CurrentValue = _liveValues;

    // ===== GRAMMAR: CONSEQUENCES =====

    [Test]
    public void Consequences_EachDurationTimesTheConsequenceBeforeIt()
    {
        var problems = new List<string>();
        var list = EventScript.ParseConsequences("production_percent:Duskstone +12; duration:sevenths:11; click_power:Elderwood +3; duration:sevenths:5; resource:Food +10", problems);

        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual(11, list[0].durationSevenths);
        Assert.AreEqual(5, list[1].durationSevenths);
        Assert.AreEqual(0, list[2].durationSevenths, "a duration must not leak onto later consequences");
    }

    [Test]
    public void Consequences_DurationWithNothingBeforeItIsReported()
    {
        var problems = new List<string>();
        EventScript.ParseConsequences("duration:sevenths:4; resource:Food +1", problems);
        Assert.AreEqual(1, problems.Count);
    }

    [Test]
    public void Consequences_ReadTargetsAmountsAndSpecialForms()
    {
        var problems = new List<string>();
        var list = EventScript.ParseConsequences(
            "click_power_percent_section:Old World Remnants +15; population:population -50; population:-10; " +
            "technology:Harvest Hymns enlightened; weather:Weeping Sky, permanent; weather:clear; unlock_event:weeping_princess.0.c-1", problems);

        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual(EventConsequence.ConsequenceType.ClickPowerPercentChangeSection, list[0].type);
        Assert.AreEqual(("Old World Remnants", 15), (list[0].targetName, list[0].value));
        Assert.AreEqual(("population", -50), (list[1].targetName, list[1].value));
        Assert.AreEqual(("population", -10), (list[2].targetName, list[2].value), "population's target is implied");
        Assert.AreEqual((EventConsequence.ConsequenceType.TechnologyEnlightened, "Harvest Hymns"), (list[3].type, list[3].targetName));
        Assert.AreEqual(("Weeping Sky", 1), (list[4].targetName, list[4].value), "permanent weather has value 1");
        Assert.AreEqual(("clear", 0), (list[5].targetName, list[5].value));
        Assert.AreEqual(("weeping_princess", EventConsequence.ConsequenceType.UnlockEvent), (list[6].targetName, list[6].type));
    }

    [Test]
    public void Consequences_UnknownKeyIsReportedAndSkipped()
    {
        var problems = new List<string>();
        var list = EventScript.ParseConsequences("resorce:Food +5; resource:Food +5", problems);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(1, problems.Count);
    }

    // ===== GRAMMAR: CONDITIONS =====

    [Test]
    public void Conditions_SupportOperatorsBareNumbersAndYesNo()
    {
        var problems = new List<string>();
        var list = EventScript.ParseConditions("population:population >= 5; resource:Elderwood 5; technology:Shared Embers; stat:morale <= 115; no_event_in_sevenths:2", problems);

        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual((EventCondition.ConditionType.PopulationCheck, ComparisonOperator.GreaterThanOrEqual, 5), (list[0].type, list[0].comparison, list[0].requiredValue));
        Assert.AreEqual(("Elderwood", ComparisonOperator.GreaterThanOrEqual, 5), (list[1].targetName, list[1].comparison, list[1].requiredValue), "a bare number means at least");
        Assert.AreEqual(("Shared Embers", ComparisonOperator.Equals, 1), (list[2].targetName, list[2].comparison, list[2].requiredValue), "no number means yes");
        Assert.AreEqual((ComparisonOperator.LessThanOrEqual, 115), (list[3].comparison, list[3].requiredValue));
        Assert.AreEqual((EventCondition.ConditionType.NoEventInSeventhsCheck, 2), (list[4].type, list[4].requiredValue));
    }

    [Test]
    public void Conditions_AnyValueDomainBecomesAValueCheck()
    {
        var condition = EventScript.ParseCondition("building:Timber Camp >= 3");
        Assert.AreEqual(EventCondition.ConditionType.ValueCheck, condition.type);
        Assert.AreEqual(("building", "Timber Camp", 3), (condition.Domain, condition.targetName, condition.requiredValue));
    }

    [Test]
    public void Conditions_WithoutATargetReadTheWholeValue()
    {
        // "age: <= 1" is parsed with the domain as its target; resolvers must see no target (the Age number), not an Age named "age".
        var condition = EventScript.ParseCondition("age: <= 1");
        Assert.AreEqual(("age", "age", 1), (condition.Domain, condition.targetName, condition.requiredValue));
        Assert.AreEqual(string.Empty, GameValues.TargetOf(condition.Domain, condition.targetName));
        Assert.AreEqual("Age-Of-Renewal", GameValues.TargetOf("age", "Age-Of-Renewal"));
        Assert.IsNull(EventContentCheck.UnknownKind(condition), "no Age id to look up");
    }

    [Test]
    public void Conditions_UnknownDomainIsReported()
    {
        var problems = new List<string>();
        CollectionAssert.IsEmpty(EventScript.ParseConditions("resorce:Food 5", problems));
        Assert.AreEqual(1, problems.Count);
    }

    // ===== GRAMMAR: CHORUS CHOICES =====

    private const string CaravanIdealism =
        "* Idealism. It's Hope. Welcome the Caravan!&D We all deserve a second chance.&C pillar:waltz;strength:15;" +
        "requirements:population:population >= 5;requirements: stat:morale >= 95;requirements:cost:resource:Elderwood 5;" +
        "success:hollow_caravan_verse_2;failure:hollow_caravan_verse_3;crit_success:hollow_caravan_verse_12;crit_failure:hollow_caravan_verse_13;" +
        "rare_event:hollow_caravan_verse_14;rare_event_percent:3 -> hollow_caravan_verse_2";

    [Test]
    public void ChorusChoice_ReadsEveryMetadataKey()
    {
        var problems = new List<string>();
        var choice = EventScript.ParseChorusChoice(CaravanIdealism, null, problems);

        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual("idealism", choice.choiceId);
        Assert.AreEqual("It's Hope. Welcome the Caravan!", choice.title);
        Assert.AreEqual("We all deserve a second chance.", choice.description);
        Assert.AreEqual("hollow_caravan_verse_2", choice.destinationPath);
        Assert.IsTrue(choice.hasChallenge);
        Assert.AreEqual(("waltz", 15), (choice.challengePillar, choice.challengeStrength));
        Assert.AreEqual(2, choice.requirements.Count);
        Assert.AreEqual(1, choice.requirementsCost.Count);
        Assert.AreEqual(("Elderwood", 5), (choice.requirementsCost[0].targetName, choice.requirementsCost[0].requiredValue));
        Assert.AreEqual(("hollow_caravan_verse_2", "hollow_caravan_verse_3"), (choice.successPath, choice.failurePath));
        Assert.AreEqual(("hollow_caravan_verse_12", "hollow_caravan_verse_13"), (choice.critSuccessPath, choice.critFailurePath));
        Assert.AreEqual(("hollow_caravan_verse_14", 3), (choice.rareEventPath, choice.rareEventPercent));
    }

    [Test]
    public void ChorusChoice_ConsequenceGroupsKeepTheirDurations()
    {
        var choice = EventScript.ParseChorusChoice("Realism. Hold.&C consequences: production_percent_section:Academia -7; duration:sevenths:7; click_power_percent_section:Old World Remnants +15; duration:sevenths:4 -> next");
        Assert.AreEqual(2, choice.consequences.Count);
        Assert.AreEqual((7, 4), (choice.consequences[0].durationSevenths, choice.consequences[1].durationSevenths));
        Assert.IsFalse(choice.hasChallenge);
    }

    [Test]
    public void ChoiceIds_AreRecognisedFromTheTypeWord()
    {
        Assert.AreEqual("idealism", EventScript.ChoiceIdFor("Idealism"));
        Assert.AreEqual("realism", EventScript.ChoiceIdFor(" Realism "));
        Assert.AreEqual("pragmatism", EventScript.ChoiceIdFor("PRAGMATISM"));
    }

    [Test]
    public void Knots_TopLevelNameAndScreenTypeComeFromTheName()
    {
        Assert.AreEqual("hollow_caravan_verse_1", EventScript.TopLevelKnot("hollow_caravan_verse_1.0.c-0"));
        Assert.AreEqual(ScreenType.Chorus, EventScript.ScreenTypeOf("hollow_caravan_chorus"));
        Assert.AreEqual(ScreenType.Outro, EventScript.ScreenTypeOf("hollow_caravan_outro"));
        Assert.AreEqual(ScreenType.Verse, EventScript.ScreenTypeOf("hollow_caravan_verse_3"));
    }

    // ===== CHORUS RULES =====

    private static ChorusChoiceData Challenge(int strength = 20, bool crits = true, int rarePercent = 0) => new ChorusChoiceData
    {
        choiceId = "idealism",
        title = "Test",
        hasChallenge = true,
        challengePillar = "waltz",
        challengeStrength = strength,
        successPath = "win",
        failurePath = "lose",
        critSuccessPath = crits ? "crit_win" : null,
        critFailurePath = crits ? "crit_lose" : null,
        rareEventPath = rarePercent > 0 ? "rare" : null,
        rareEventPercent = rarePercent,
    };

    [Test]
    public void SuccessPercent_IsPillarOverStrengthCapped()
    {
        Assert.AreEqual(50, ChorusRules.SuccessPercent(10, 20));
        Assert.AreEqual(100, ChorusRules.SuccessPercent(30, 20));
        Assert.AreEqual(0, ChorusRules.SuccessPercent(0, 20));
        Assert.AreEqual(100, ChorusRules.SuccessPercent(5, 0), "no strength means no challenge");
    }

    [Test]
    public void Odds_MatchTheDisplayedChanceAndCoverEveryRoll()
    {
        foreach (int pillar in new[] { 0, 1, 5, 10, 19, 20, 40 })
        foreach (float bonus in new[] { 0f, 7.4f, 25f, 150f })
        {
            var choice = Challenge(crits: false);
            var odds = ChorusRules.OutcomeOdds(choice, bonus, pillar);
            Assert.AreEqual(100, odds.Values.Sum());
            odds.TryGetValue(ChorusOutcome.Success, out int success);
            int shown = ChorusRules.ChanceAbove(100 - ChorusRules.SuccessPercent(pillar, choice.challengeStrength), bonus);
            Assert.AreEqual(shown, success, $"pillar {pillar}, bonus {bonus}: the challenge slot must show the real chance");
        }
    }

    [Test]
    public void Resolve_RareEventTakesTheTopRollsFirst()
    {
        var choice = Challenge(rarePercent: 3);
        Assert.AreEqual(ChorusOutcome.RareEvent, ChorusRules.Resolve(choice, 98, 0f, 20).outcome);
        Assert.AreNotEqual(ChorusOutcome.RareEvent, ChorusRules.Resolve(choice, 97, 0f, 20).outcome);
        Assert.AreEqual("rare", ChorusRules.Resolve(choice, 100, 0f, 0).targetKnot, "a rare event ignores the challenge");
    }

    [Test]
    public void Resolve_CriticalBandsNeedTheirKnots()
    {
        Assert.AreEqual(ChorusOutcome.CriticalSuccess, ChorusRules.Resolve(Challenge(), 95, 0f, 20).outcome);
        Assert.AreEqual(ChorusOutcome.CriticalFailure, ChorusRules.Resolve(Challenge(), 5, 0f, 10).outcome);
        Assert.AreEqual(ChorusOutcome.Success, ChorusRules.Resolve(Challenge(crits: false), 95, 0f, 20).outcome);
        Assert.AreEqual(ChorusOutcome.Failure, ChorusRules.Resolve(Challenge(crits: false), 5, 0f, 10).outcome);
    }

    [Test]
    public void Resolve_PietyOnlyEverSavesPositiveOutcomes()
    {
        var choice = Challenge(); // pillar 10 of 20: succeed above 50
        var saved = ChorusRules.Resolve(choice, 45, 10f, 10);
        Assert.AreEqual(ChorusOutcome.Success, saved.outcome);
        Assert.IsTrue(saved.savedByRoll);

        var lifted = ChorusRules.Resolve(choice, 5, 10f, 10); // 15: no longer critical, still a failure
        Assert.AreEqual(ChorusOutcome.Failure, lifted.outcome);
        Assert.IsFalse(lifted.savedByRoll);

        Assert.IsFalse(ChorusRules.Resolve(choice, 60, 10f, 10).savedByRoll, "it would have succeeded anyway");
    }

    [Test]
    public void Resolve_OutcomeConsequencesThenTheChoicesOwn()
    {
        var choice = Challenge();
        choice.successConsequences.Add(new EventConsequence { type = EventConsequence.ConsequenceType.ResourceChange, targetName = "Food", value = 5 });
        choice.consequences.Add(new EventConsequence { type = EventConsequence.ConsequenceType.StatChange, targetName = "morale", value = 1 });

        var result = ChorusRules.Resolve(choice, 60, 0f, 10);
        Assert.AreEqual("win", result.targetKnot);
        CollectionAssert.AreEqual(new[] { "Food", "morale" }, result.consequences.Select(c => c.targetName).ToArray());
        Assert.AreEqual(1, choice.successConsequences.Count, "resolving must not change the shared choice data");
    }

    [Test]
    public void Resolve_WithoutAChallengeTimePassesToTheDestination()
    {
        var choice = new ChorusChoiceData { choiceId = "pragmatism", destinationPath = "next" };
        var result = ChorusRules.Resolve(choice, 50, 0f, 0);
        Assert.AreEqual((ChorusOutcome.TimePasses, "next", true), (result.outcome, result.targetKnot, result.isPositive));
    }

    [Test]
    public void Costs_BecomeNegativeConsequences()
    {
        var choice = EventScript.ParseChorusChoice("Idealism. Pay.&C requirements:cost:resource:Elderwood 5;housing:housing 2 -> next");
        var costs = ChorusRules.CostConsequences(choice);
        Assert.AreEqual(2, costs.Count);
        Assert.AreEqual((EventConsequence.ConsequenceType.ResourceChange, "Elderwood", -5), (costs[0].type, costs[0].targetName, costs[0].value));
        Assert.AreEqual((EventConsequence.ConsequenceType.HousingChange, -2), (costs[1].type, costs[1].value));
    }

    // ===== WORDING =====

    private static EventConsequence Food(int value) => new EventConsequence { type = EventConsequence.ConsequenceType.ResourceChange, targetName = "Food", value = value };

    [Test]
    public void Wording_RunningTotalsCombineRepeatedTargets()
    {
        EventText.CurrentValue = c => c.targetName == "Food" ? 100 : (int?)null;
        string text = EventText.DescribeConsequences(new[] { Food(20), Food(-50) });
        StringAssert.Contains("(New Total 120)", text);
        StringAssert.Contains("(New Total 70)", text);
    }

    [Test]
    public void Wording_CostsPaidFirstMoveTheTotalsWithoutBeingListed()
    {
        EventText.CurrentValue = c => 100;
        string text = EventText.DescribeConsequences(new[] { Food(20) }, true, new[] { Food(-30) });
        StringAssert.Contains("(New Total 90)", text);
        StringAssert.DoesNotContain("Lost", text);
    }

    [Test]
    public void Wording_TimedEffectsShowTheirDuration()
    {
        var list = EventScript.ParseConsequences("production_percent:Food +10; duration:sevenths:6");
        StringAssert.Contains("For 6 Sevenths", EventText.DescribeConsequences(list));
    }

    [Test]
    public void Wording_EveryConsequenceTypeHasItsOwnSentence()
    {
        foreach (EventConsequence.ConsequenceType type in Enum.GetValues(typeof(EventConsequence.ConsequenceType)))
        {
            string text = EventText.DescribeConsequence(new EventConsequence { type = type, targetName = "X", value = 1 });
            Assert.IsFalse(text.StartsWith(type.ToString(), StringComparison.Ordinal), $"{type} falls through to the placeholder wording in EventText");
        }
    }

    [Test]
    public void Wording_CostsReadAsPrices()
    {
        var cost = EventScript.ParseCondition("resource:Elderwood 5");
        Assert.AreEqual("Costs 5 Elderwood", EventText.DescribeRequirement(cost, isCost: true));
        Assert.AreEqual("Needs At Least 5 Elderwood", EventText.DescribeRequirement(cost));
    }

    // ===== SENTENCES AND TOOLTIPS =====

    [Test]
    public void Sentences_SplitWithoutLosingAnyText()
    {
        const string text = "It rains. The <b>river</b> rises 3.5 feet!\nWho knows... Maybe. \"Run!\" she said.";
        var parts = ProgressiveSentenceRevealLogic.SplitSentences(text);
        Assert.AreEqual(text, string.Concat(parts));
        Assert.Greater(parts.Count, 3);
        Assert.IsFalse(parts.Any(p => p.TrimEnd().EndsWith("3.", StringComparison.Ordinal)), "decimals are not sentence ends");
    }

    [Test]
    public void TooltipData_ClearLeavesNothingToShow()
    {
        var data = new TooltipData { title = "A", effects = "B", style = TooltipStyle.Banner };
        Assert.IsTrue(data.HasContent);
        data.Clear();
        Assert.IsFalse(data.HasContent);
        Assert.AreEqual(TooltipStyle.Standard, data.style);
    }

    [Test]
    public void TooltipAmounts_TruncateToThreeDecimalsAndNeverShowZeroForAPositive()
    {
        Assert.AreEqual("12.345", TooltipContent.Amount(12.34567f));
        Assert.AreEqual("0.001", TooltipContent.Amount(0.0004f));
        Assert.AreEqual("0", TooltipContent.Amount(-3f));
        Assert.AreEqual("40", TooltipContent.Amount(40f));
    }

    // ===== CHORUS PREVIEW =====

    [Test]
    public void Preview_ShowsTheChallengeChanceAndHidesRareEvents()
    {
        foreach (int pillar in new[] { 0, 5, 10, 20 })
        foreach (float bonus in new[] { 0f, 7.4f, 25f })
        {
            var preview = ChorusRules.Preview(Challenge(rarePercent: 3), bonus, pillar);
            int shown = ChorusRules.ChanceAbove(100 - ChorusRules.SuccessPercent(pillar, 20), bonus);
            Assert.AreEqual(shown, preview.successPercent, $"pillar {pillar}, bonus {bonus}: same chance as the challenge slot");
            Assert.AreEqual(100, preview.successPercent + preview.failurePercent);
        }
        Assert.IsFalse(ChorusRules.Preview(new ChorusChoiceData { successPath = "on" }, 0f, 0).hasChallenge, "a choice without a pillar simply happens");
    }

    [Test]
    public void Preview_FlagsCriticalFailureOnlyWhenARollCanReachIt()
    {
        Assert.IsTrue(ChorusRules.Preview(Challenge(), 0f, 10).canFailCritically);
        Assert.IsFalse(ChorusRules.Preview(Challenge(crits: false), 0f, 10).canFailCritically, "no critical failure knot, no flag");
        Assert.IsFalse(ChorusRules.Preview(Challenge(), 10f, 10).canFailCritically, "Piety lifts every roll above the critical band");
        Assert.IsFalse(ChorusRules.Preview(Challenge(), 0f, 20).canFailCritically, "a certain success cannot fail at all");
    }

    [Test]
    public void Outcomes_FollowTheStoryAndRevealOnlySuccessAndFailure()
    {
        var choice = Challenge(rarePercent: 3);
        choice.rareEventConsequences.Add(Food(999));
        choice.critSuccessConsequences.Add(Food(777));
        var along = new Dictionary<string, List<EventConsequence>>
        {
            { "win", new List<EventConsequence> { Food(10) } },
            { "lose", new List<EventConsequence> { Food(-5) } },
            { "rare", new List<EventConsequence> { Food(999) } },
        };
        List<EventConsequence> Along(string knot, out bool decision)
        {
            decision = knot == "lose";
            return along.TryGetValue(knot, out var list) ? list : new List<EventConsequence>();
        }

        string text = ChorusChoice.DescribeOutcomes(choice, ChorusRules.Preview(choice, 0f, 10), Along);
        StringAssert.Contains("On Success", text);
        StringAssert.Contains("On Failure", text);
        StringAssert.Contains("50%", text);
        StringAssert.Contains("Gained 10 x Food", text, "the outcome verse's button effects are shown");
        StringAssert.Contains("Lost 5 x Food", text);
        StringAssert.Contains("another decision", text, "a path that reaches a chorus says so");
        StringAssert.Contains("Critical Failure", text, "a possible critical failure is flagged");
        StringAssert.DoesNotContain("999", text, "rare events stay secret");
        StringAssert.DoesNotContain("777", text, "critical successes stay secret");
        StringAssert.DoesNotContain("Rare", text);
    }

    [Test]
    public void Outcomes_WithoutAChallengeHaveOneOutcomeAndNoOdds()
    {
        var choice = new ChorusChoiceData { choiceId = "pragmatism", successPath = "on" };
        List<EventConsequence> Along(string knot, out bool decision)
        {
            decision = false;
            return new List<EventConsequence> { Food(3) };
        }
        string text = ChorusChoice.DescribeOutcomes(choice, ChorusRules.Preview(choice, 0f, 0), Along);
        StringAssert.Contains("Outcome", text);
        StringAssert.DoesNotContain("%", text);
        StringAssert.DoesNotContain("Failure", text);
    }

    [Test]
    public void Tone_ColoursGainsLossesAndStoryFlags()
    {
        Assert.AreEqual(ConsequenceTone.Good, EventText.ToneOf(Food(5)));
        Assert.AreEqual(ConsequenceTone.Bad, EventText.ToneOf(Food(-5)));
        Assert.AreEqual(ConsequenceTone.Bad, EventText.ToneOf(new EventConsequence { type = EventConsequence.ConsequenceType.PopulationChange, value = -3 }));
        Assert.AreEqual(ConsequenceTone.Neutral, EventText.ToneOf(new EventConsequence { type = EventConsequence.ConsequenceType.ScoreChange, value = 1 }));
        Assert.AreEqual(ConsequenceTone.Good, EventText.ToneOf(new EventConsequence { type = EventConsequence.ConsequenceType.TechnologyEnlightened }));
    }

    [Test]
    public void Keywords_LinkTheCatalogsTermsButNotMarkupItselfOrExistingLinks()
    {
        string text = Keywords.Linkify("<color=#fff>Waltz</color> and Piety, <link=\"x\">Waltz</link>", "stat:piety");
        StringAssert.Contains("<link=\"pillar:waltz\">", text);
        StringAssert.DoesNotContain("stat:piety", text, "a term's own tooltip does not link to itself");
        StringAssert.Contains("<color=#fff>", text, "tags are left alone");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(text, "pillar:waltz").Count, "an existing link is not linked again");
    }
}
