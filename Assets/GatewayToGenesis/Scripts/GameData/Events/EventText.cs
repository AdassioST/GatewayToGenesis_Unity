using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Player-facing wording for event consequences and requirements. The outro, verse tooltips, chorus
/// choice tooltips and requirement slots all use it, so a new consequence type is worded once.
/// "New Total" values run through the list: two gains of the same resource show the combined total.
/// </summary>
public static class EventText
{
    /// <summary>Reads the current value behind a consequence; null when it has none or its system is missing.</summary>
    public static Func<EventConsequence, int?> CurrentValue = LiveValue;

    /// <summary>
    /// A "- ..." line per consequence, with running totals. Empty for an empty list. <paramref name="appliedFirst"/>
    /// are consequences that land before these (a chorus choice's costs): they move the totals but are not listed.
    /// </summary>
    public static string DescribeConsequences(IEnumerable<EventConsequence> consequences, bool withTotals = true, IEnumerable<EventConsequence> appliedFirst = null)
    {
        if (consequences == null) return string.Empty;
        var sb = new StringBuilder();
        var running = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (withTotals && appliedFirst != null)
        {
            foreach (var c in appliedFirst) if (c != null) Advance(running, c);
        }
        foreach (var c in consequences)
        {
            if (c == null) continue;
            int? total = withTotals ? Advance(running, c) : null;
            sb.Append("- ").AppendLine(DescribeConsequence(c, total));
        }
        return sb.ToString().TrimEnd();
    }

    // Moves the running total for c's value; null when it has none.
    private static int? Advance(Dictionary<string, int> running, EventConsequence c)
    {
        string key = TotalKey(c);
        if (key == null) return null;
        int? current = running.TryGetValue(key, out int seen) ? seen : CurrentValue?.Invoke(c);
        if (!current.HasValue) return null;
        running[key] = current.Value + c.value;
        return running[key];
    }

    /// <summary>One consequence in words; <paramref name="newTotal"/> adds "(New Total N)" where it applies.</summary>
    public static string DescribeConsequence(EventConsequence c, int? newTotal = null)
    {
        int amount = Math.Abs(c.value);
        bool gain = c.value >= 0;
        string total = newTotal.HasValue ? $" (New Total {Math.Max(0, newTotal.Value)})" : string.Empty;
        string duration = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
        string sign = gain ? "+" : "-";

        switch (c.type)
        {
            case EventConsequence.ConsequenceType.ResourceChange:
            case EventConsequence.ConsequenceType.ProductionUnitChange:
                return $"You've {(gain ? "Gained" : "Lost")} {amount} x {c.targetName}{total}";
            case EventConsequence.ConsequenceType.ScoreChange:
                string scoreTotal = newTotal.HasValue ? $" (New Total {newTotal.Value})" : string.Empty;
                return $"You've {(gain ? "Gained" : "Lost")} {amount} x {Humanize(c.targetName)}{scoreTotal}";
            case EventConsequence.ConsequenceType.StatChange:
                string statTotal = newTotal.HasValue ? $" (New Total {newTotal.Value})" : string.Empty;
                return $"You've {(gain ? "Gained" : "Lost")} {amount} {Humanize(c.targetName)}{statTotal}";
            case EventConsequence.ConsequenceType.TechnologyEnlightened:
                return $"You've Gained Enlightenment on {c.targetName}";
            case EventConsequence.ConsequenceType.UnlockEvent:
                return $"A New Story Awaits: {Humanize(c.targetName)}";
            case EventConsequence.ConsequenceType.PopulationChange:
                // Events can only remove population; newcomers arrive as vagrants.
                return $"{amount} Population Have Been Killed{total}";
            case EventConsequence.ConsequenceType.HousingChange:
                return gain ? $"{amount} New Housing Units Have Been Gained{total}" : $"{amount} Housing Units Have Been Lost{total}";
            case EventConsequence.ConsequenceType.VagrantsChange:
                return gain ? $"{amount} New Vagrants Have Arrived{total}" : $"{amount} Vagrants Have Been Killed{total}";
            case EventConsequence.ConsequenceType.DeathsChange:
                return gain ? $"{amount} Additional Deaths Have Been Recorded{total}" : $"{amount} Deaths Have Been Wiped From The Historical Records{total}";
            case EventConsequence.ConsequenceType.DeathRecordsRevision:
                return gain ? $"{amount} Additional Deaths Have Been Added To The Public Records{total}" : $"{amount} Deaths Have Been Wiped From The Public Records{total}";
            case EventConsequence.ConsequenceType.ProductionPercentChange:
                return $"Production {sign}{amount}% For {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
                return $"Production {sign}{amount}% For Section {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.ClickPowerChange:
                return $"Click Power {sign}{amount} For {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                return $"Click Power {sign}{amount}% For {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                return $"Click Power {sign}{amount} For Section {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                return $"Click Power {sign}{amount}% For Section {c.targetName}{duration}";
            case EventConsequence.ConsequenceType.WeatherChange:
                if (string.Equals(c.targetName, "clear", StringComparison.OrdinalIgnoreCase)) return "The Winds Have Fallen Silent";
                return c.value == 1 ? $"The Weather Has Permanently Changed To {c.targetName}" : $"The Weather Has Changed To {c.targetName}";
            default:
                return $"{c.type} {c.targetName} {c.value:+#;-#;0}";
        }
    }

    // ===== REQUIREMENTS =====

    /// <summary>Headline for a requirement: "Needs At Least 5 Population", or "Costs 5 Elderwood" for a cost.</summary>
    public static string DescribeRequirement(EventCondition c, bool isCost = false)
    {
        if (c == null) return "Requirement";
        string name = TargetLabel(c);
        if (isCost) return $"Costs {Math.Abs(c.requiredValue)} {name}";
        switch (c.type)
        {
            case EventCondition.ConditionType.TechnologyCheck:
                return $"Needs {c.targetName} Unlocked";
            case EventCondition.ConditionType.RitualSeventhCheck:
                return "Needs A Ritual Seventh";
            case EventCondition.ConditionType.NoEventInSeventhsCheck:
                return $"Needs {c.requiredValue} Quiet Sevenths";
            case EventCondition.ConditionType.SeventhCheck:
            case EventCondition.ConditionType.PhaseCheck:
            case EventCondition.ConditionType.EchoCheck:
            case EventCondition.ConditionType.CycleCheck:
                return $"Needs {name} {Symbol(c.comparison)} {c.requiredValue}";
        }
        if (c.IsYesNo) return c.requiredValue > 0 && c.comparison != ComparisonOperator.NotEquals ? $"Needs {name}" : $"Needs No {name}";
        return $"Needs {Phrase(c.comparison)} {c.requiredValue} {name}";
    }

    /// <summary>Required vs current value and a Met / Not Met line, coloured for the tooltip.</summary>
    public static string DescribeRequirementStatus(EventCondition c, bool isCost = false)
    {
        if (c == null) return string.Empty;
        bool met = c.Evaluate();
        string color = met ? "green" : "red";
        var sb = new StringBuilder();
        if (c.IsYesNo)
        {
            if (c.type == EventCondition.ConditionType.TechnologyCheck) sb.Append($"Status: <color={color}>{(met ? "Unlocked" : "Locked")}</color>");
            else sb.Append($"Status: <color={color}>{(met ? "Yes" : "No")}</color>");
        }
        else
        {
            bool known = c.TryGetCurrentValue(out float current);
            sb.AppendLine(isCost ? $"Cost: {Math.Abs(c.requiredValue)}" : $"Required: {Symbol(c.comparison)} {c.requiredValue}");
            sb.Append($"Current: <color={color}>{(known ? Math.Round(current).ToString(CultureInfo.InvariantCulture) : "?")}</color>");
        }
        sb.AppendLine();
        sb.Append($"Status: <b><color={color}>{(met ? (isCost ? "Affordable" : "Met") : (isCost ? "Cannot Afford" : "Not Met"))}</color></b>");
        return sb.ToString();
    }

    public static string Symbol(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.NotEquals: return "≠";
            case ComparisonOperator.GreaterThan: return ">";
            case ComparisonOperator.LessThan: return "<";
            case ComparisonOperator.GreaterThanOrEqual: return "≥";
            case ComparisonOperator.LessThanOrEqual: return "≤";
            default: return "=";
        }
    }

    public static string Phrase(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return "Exactly";
            case ComparisonOperator.NotEquals: return "Anything But";
            case ComparisonOperator.GreaterThan: return "More Than";
            case ComparisonOperator.LessThan: return "Less Than";
            case ComparisonOperator.LessThanOrEqual: return "At Most";
            default: return "At Least";
        }
    }

    /// <summary>"caravan_encountered" → "Caravan Encountered".</summary>
    public static string Humanize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var words = raw.Replace('_', ' ').Trim().Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length == 0) continue;
            words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
        }
        return string.Join(" ", words);
    }

    private static string TargetLabel(EventCondition c)
    {
        switch (c.type)
        {
            case EventCondition.ConditionType.PopulationCheck: return "Population";
            case EventCondition.ConditionType.HousingCheck: return "Housing";
            case EventCondition.ConditionType.VagrantsCheck: return "Vagrants";
            case EventCondition.ConditionType.DeathsCheck: return "Deaths";
            case EventCondition.ConditionType.VagrantDeathsCheck: return "Vagrant Deaths";
            case EventCondition.ConditionType.TrueDeathsCheck: return "True Deaths";
            case EventCondition.ConditionType.SeventhCheck: return "Seventh";
            case EventCondition.ConditionType.PhaseCheck: return "Phase";
            case EventCondition.ConditionType.EchoCheck: return "Echo";
            case EventCondition.ConditionType.CycleCheck: return "Cycle";
            case EventCondition.ConditionType.ResourceCheck: return c.targetName;
            case EventCondition.ConditionType.ValueCheck when string.IsNullOrEmpty(c.targetName):
                return Humanize(c.domain); // "morale", "satisfaction": the domain is the value
            case EventCondition.ConditionType.ValueCheck when c.domain == "government" && Enum.TryParse(c.targetName, true, out GovernmentType government):
                return $"A {GovernmentCompass.Name(government)} Government";
            default: return Humanize(c.targetName);
        }
    }

    // Consequences that change one countable value get a running total.
    private static string TotalKey(EventConsequence c)
    {
        string domain = DomainOf(c.type);
        if (domain == null) return null;
        return domain == "population" || domain == "housing" || domain == "vagrants" || domain == "deaths" ? domain : domain + ":" + c.targetName;
    }

    private static string DomainOf(EventConsequence.ConsequenceType type)
    {
        switch (type)
        {
            case EventConsequence.ConsequenceType.ResourceChange: return "resource";
            case EventConsequence.ConsequenceType.ProductionUnitChange: return "building";
            case EventConsequence.ConsequenceType.ScoreChange: return "score";
            case EventConsequence.ConsequenceType.StatChange: return "stat";
            case EventConsequence.ConsequenceType.PopulationChange: return "population";
            case EventConsequence.ConsequenceType.HousingChange: return "housing";
            case EventConsequence.ConsequenceType.VagrantsChange: return "vagrants";
            case EventConsequence.ConsequenceType.DeathsChange:
            case EventConsequence.ConsequenceType.DeathRecordsRevision: return "deaths";
            default: return null;
        }
    }

    private static int? LiveValue(EventConsequence c)
    {
        string domain = DomainOf(c.type);
        if (domain == null || !GameValues.TryGet(domain, c.targetName, out float value)) return null;
        return (int)Math.Round(value);
    }
}
