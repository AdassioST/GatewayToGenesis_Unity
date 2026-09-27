using System;
using System.Collections.Generic;

/// <summary>How a chorus choice turned out.</summary>
public enum ChorusOutcome
{
    TimePasses,
    RareEvent,
    CriticalSuccess,
    Success,
    Failure,
    CriticalFailure
}

/// <summary>The result of resolving one chorus choice: where the story goes and what it costs.</summary>
public struct ChorusResolution
{
    public ChorusOutcome outcome;
    /// <summary>Good outcome (rare event, success, critical success, time passes).</summary>
    public bool isPositive;
    /// <summary>The saving-roll bonus (Piety) turned a miss into this outcome.</summary>
    public bool savedByRoll;
    public string targetKnot;
    public int naturalRoll;
    public int enhancedRoll;
    public int successPercent;
    /// <summary>Outcome consequences plus the choice's unconditional ones (costs are separate).</summary>
    public List<EventConsequence> consequences;
}

/// <summary>
/// What the player is shown before choosing: the chance to succeed or fail and whether a failure can be
/// critical. Rare events and critical successes stay hidden, so they come as a surprise.
/// </summary>
public struct ChorusPreview
{
    /// <summary>False for a choice that simply happens (no pillar challenge): it has one outcome, no odds.</summary>
    public bool hasChallenge;
    /// <summary>Chance (0-100) of a good outcome, as the challenge slot shows it (Piety included).</summary>
    public int successPercent;
    public int failurePercent;
    /// <summary>Some failing rolls end in a critical failure.</summary>
    public bool canFailCritically;
}

/// <summary>
/// Chorus dice rules, free of Unity so they can be tested. One d100 roll decides everything; the saving-roll
/// bonus (Piety) is added to it and capped at 100.
///   Rare event (when authored): the top rare_event_percent rolls, checked first.
///   Challenge: success chance = pillar / strength (capped 0-100%); the roll succeeds when it is above
///   100 - chance. Success on 91+ is critical, failure on 10 or less is critical, when those knots exist.
///   No challenge: the choice simply happens ("Time passes...").
/// </summary>
public static class ChorusRules
{
    public const int CriticalBand = 10;

    public static int SuccessPercent(int pillarValue, int requiredStrength)
    {
        if (requiredStrength <= 0) return 100;
        return Clamp((int)Math.Round(pillarValue / (double)requiredStrength * 100.0, MidpointRounding.AwayFromZero), 0, 100);
    }

    public static int EnhancedRoll(int naturalRoll, float savingRollBonus) =>
        Math.Min(100, naturalRoll + (int)Math.Round(Math.Max(0f, savingRollBonus), MidpointRounding.AwayFromZero));

    /// <summary>
    /// Chance (0-100) that a roll of 1-100 plus the bonus (capped at 100) lands above <paramref name="threshold"/>.
    /// Nothing lands above 100, so a 0% challenge stays 0% whatever the bonus.
    /// </summary>
    public static int ChanceAbove(int threshold, float savingRollBonus)
    {
        if (threshold >= 100) return 0;
        int bonus = (int)Math.Round(Math.Max(0f, savingRollBonus), MidpointRounding.AwayFromZero);
        return Clamp(100 - Clamp(threshold - bonus, 0, 100), 0, 100);
    }

    /// <summary>Requirements and costs all hold right now.</summary>
    public static bool IsAvailable(ChorusChoiceData choice)
    {
        if (choice == null) return false;
        if (choice.requirements != null) foreach (var r in choice.requirements) if (r != null && !r.Evaluate()) return false;
        if (choice.requirementsCost != null) foreach (var r in choice.requirementsCost) if (r != null && !r.Evaluate()) return false;
        return true;
    }

    /// <summary>What paying a choice's costs takes away. Only resources, population and housing can be paid.</summary>
    public static List<EventConsequence> CostConsequences(ChorusChoiceData choice)
    {
        var list = new List<EventConsequence>();
        if (choice?.requirementsCost == null) return list;
        foreach (var cost in choice.requirementsCost)
        {
            if (cost == null) continue;
            int amount = -Math.Abs(cost.requiredValue);
            switch (cost.type)
            {
                case EventCondition.ConditionType.ResourceCheck:
                    list.Add(new EventConsequence { type = EventConsequence.ConsequenceType.ResourceChange, targetName = cost.targetName, value = amount });
                    break;
                case EventCondition.ConditionType.PopulationCheck:
                    list.Add(new EventConsequence { type = EventConsequence.ConsequenceType.PopulationChange, targetName = "population", value = amount });
                    break;
                case EventCondition.ConditionType.HousingCheck:
                    list.Add(new EventConsequence { type = EventConsequence.ConsequenceType.HousingChange, targetName = "housing", value = amount });
                    break;
            }
        }
        return list;
    }

    /// <summary>Which outcome a natural roll (1-100) gives. Allocation-free; <see cref="Resolve"/> and the odds use it.</summary>
    public static ChorusOutcome OutcomeFor(ChorusChoiceData choice, int naturalRoll, float savingRollBonus, int pillarValue)
    {
        int enhanced = EnhancedRoll(naturalRoll, savingRollBonus);
        if (HasRareEvent(choice) && enhanced > 100 - Clamp(choice.rareEventPercent, 0, 100)) return ChorusOutcome.RareEvent;
        if (!choice.hasChallenge) return ChorusOutcome.TimePasses;

        bool success = enhanced > 100 - SuccessPercent(pillarValue, choice.challengeStrength);
        if (success && enhanced > 100 - CriticalBand && !string.IsNullOrEmpty(choice.critSuccessPath)) return ChorusOutcome.CriticalSuccess;
        if (!success && enhanced <= CriticalBand && !string.IsNullOrEmpty(choice.critFailurePath)) return ChorusOutcome.CriticalFailure;
        return success ? ChorusOutcome.Success : ChorusOutcome.Failure;
    }

    /// <summary>Resolve a choice for a given natural roll (1-100): outcome, destination and consequences.</summary>
    public static ChorusResolution Resolve(ChorusChoiceData choice, int naturalRoll, float savingRollBonus, int pillarValue)
    {
        var outcome = OutcomeFor(choice, naturalRoll, savingRollBonus, pillarValue);
        var result = new ChorusResolution
        {
            outcome = outcome,
            naturalRoll = naturalRoll,
            enhancedRoll = EnhancedRoll(naturalRoll, savingRollBonus),
            successPercent = choice.hasChallenge ? SuccessPercent(pillarValue, choice.challengeStrength) : 100,
            consequences = new List<EventConsequence>(),
        };
        List<EventConsequence> outcomeConsequences;
        switch (outcome)
        {
            case ChorusOutcome.RareEvent:
                result.targetKnot = choice.rareEventPath;
                outcomeConsequences = choice.rareEventConsequences;
                break;
            case ChorusOutcome.CriticalSuccess:
                result.targetKnot = choice.critSuccessPath;
                outcomeConsequences = choice.critSuccessConsequences;
                break;
            case ChorusOutcome.CriticalFailure:
                result.targetKnot = choice.critFailurePath;
                outcomeConsequences = choice.critFailureConsequences;
                break;
            case ChorusOutcome.Failure:
                result.targetKnot = First(choice.failurePath, choice.destinationPath);
                outcomeConsequences = choice.failureConsequences;
                break;
            default: // Success, TimePasses
                result.targetKnot = First(choice.successPath, choice.destinationPath);
                outcomeConsequences = choice.successConsequences;
                break;
        }
        result.isPositive = outcome != ChorusOutcome.Failure && outcome != ChorusOutcome.CriticalFailure;
        // Piety only ever helps: it "saved" the roll when the natural roll alone would have ended elsewhere.
        result.savedByRoll = result.isPositive && result.enhancedRoll != naturalRoll && OutcomeFor(choice, naturalRoll, 0f, pillarValue) != outcome;
        if (outcomeConsequences != null) result.consequences.AddRange(outcomeConsequences);
        if (choice.consequences != null) result.consequences.AddRange(choice.consequences);
        return result;
    }

    /// <summary>Chance of each outcome (percent of 1-100 rolls), for tooltips.</summary>
    public static Dictionary<ChorusOutcome, int> OutcomeOdds(ChorusChoiceData choice, float savingRollBonus, int pillarValue)
    {
        var odds = new Dictionary<ChorusOutcome, int>();
        for (int roll = 1; roll <= 100; roll++)
        {
            var outcome = OutcomeFor(choice, roll, savingRollBonus, pillarValue);
            odds.TryGetValue(outcome, out int count);
            odds[outcome] = count + 1;
        }
        return odds;
    }

    /// <summary>
    /// The odds as the player sees them. Success is the challenge chance with Piety (what <see cref="ChanceAbove"/>
    /// gives the challenge slot); rare events and critical successes are not revealed.
    /// </summary>
    public static ChorusPreview Preview(ChorusChoiceData choice, float savingRollBonus, int pillarValue)
    {
        if (choice == null || !choice.hasChallenge) return new ChorusPreview { hasChallenge = false, successPercent = 100 };
        int success = ChanceAbove(100 - SuccessPercent(pillarValue, choice.challengeStrength), savingRollBonus);
        bool critical = false;
        if (!string.IsNullOrEmpty(choice.critFailurePath))
        {
            for (int roll = 1; roll <= CriticalBand && !critical; roll++)
            {
                critical = OutcomeFor(choice, roll, savingRollBonus, pillarValue) == ChorusOutcome.CriticalFailure;
            }
        }
        return new ChorusPreview { hasChallenge = true, successPercent = success, failurePercent = 100 - success, canFailCritically = critical };
    }

    private static bool HasRareEvent(ChorusChoiceData choice) => choice.rareEventPercent > 0 && !string.IsNullOrEmpty(choice.rareEventPath);

    private static string First(string a, string b) => !string.IsNullOrEmpty(a) ? a : b;

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
