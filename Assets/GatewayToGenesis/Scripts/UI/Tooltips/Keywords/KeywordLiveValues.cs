using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Live numbers for keywords bound to the game ("kind:name" ids): current pillar strength, stock and rates,
/// unlock state, population... Only values and state live here; what a keyword means is its card in the keyword
/// masterfile and its White-Haven Library entry (<see cref="Keywords"/>). Each kind fills a default title and type
/// so a game term works without a card.
/// </summary>
public static class KeywordLiveValues
{
    // Each pillar's opposite on its axis (Aureus vs Chorus, Regalia vs Waltz).
    private static readonly Dictionary<string, string> PillarAxis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "aureus", "chorus" }, { "chorus", "aureus" }, { "regalia", "waltz" }, { "waltz", "regalia" },
    };

    /// <summary>True when <paramref name="id"/> names something in the game (a pillar, stat, resource...).</summary>
    public static bool Exists(string id) => TryFill(id, new TooltipData());

    /// <summary>Fill <paramref name="d"/> with the defaults and live values of <paramref name="id"/>; false when it is not a game id.</summary>
    public static bool TryFill(string id, TooltipData d)
    {
        if (string.IsNullOrEmpty(id)) return false;
        int colon = id.IndexOf(':');
        if (colon <= 0) return false;
        string kind = id.Substring(0, colon).ToLowerInvariant(), name = id.Substring(colon + 1);
        switch (kind)
        {
            case "pillar": return Pillar(name, d);
            case "stat": return Stat(name, d);
            case "resource": return Resource(name, d);
            case "tech": return Technology(name, d);
            case "section": return Section(name, d);
            case "term": return Term(name, d);
            case "time": return Time(name, d);
            default: return false;
        }
    }

    private static bool Pillar(string pillar, TooltipData d)
    {
        if (!StatDefinitions.PillarSubstats.TryGetValue(pillar, out var substats)) return false;
        var stats = StatManager.Instance;
        d.title = Title(pillar);
        d.type = "Pillar of Civilization";
        var lines = new List<string>();
        if (stats != null) lines.Add(TooltipText.Row("Current Strength", TooltipText.Value(stats.GetPillarValue(pillar).ToString())));
        lines.Add($"Shapes {Title(substats[0])} and {Title(substats[1])}");
        if (PillarAxis.TryGetValue(pillar, out var opposite)) lines.Add(TooltipText.Muted("Opposes ") + Title(opposite));
        d.effects = TooltipText.Lines(lines);
        return true;
    }

    private static bool Stat(string stat, TooltipData d)
    {
        var stats = StatManager.Instance;
        string key = stat.ToLowerInvariant();
        if (key == "morale") return Morale(stats, d);
        if (key == "satisfaction") return Satisfaction(stats, d);
        if (StatDefinitions.KindOf(stat) == StatDefinitions.StatKind.Unknown) return false;

        d.title = Title(stat);
        string parent = StatDefinitions.PillarSubstats.FirstOrDefault(p => p.Value.Any(s => string.Equals(s, stat, StringComparison.OrdinalIgnoreCase))).Key;
        d.type = parent != null ? $"Aspect of {Title(parent)}" : "Civilization Stat";
        var lines = new List<string>();
        if (stats != null) lines.Add(TooltipText.Row("Current", TooltipText.Value(stats.GetStatValue(stat).ToString())));
        foreach (var derived in StatDefinitions.DerivedSource.Where(p => string.Equals(p.Value, stat, StringComparison.OrdinalIgnoreCase)))
        {
            float value = stats != null ? stats.GetDerivedValue(derived.Key) : 0f;
            lines.Add(TooltipText.Bullet(TooltipText.Row(SplitCamel(derived.Key), TooltipText.Value($"{value:0.#}"))));
        }
        if (key == "piety" && stats != null)
        {
            lines.Add(TooltipText.Row("Saving Roll", TooltipText.Good($"+{stats.GetSavingRollChancePercentCapped():0.#}")));
        }
        d.effects = TooltipText.Lines(lines);
        return true;
    }

    private static bool Morale(StatManager stats, TooltipData d)
    {
        d.title = "Morale";
        d.type = "Civilization Stat";
        if (stats == null) return true;
        var lines = new List<string>
        {
            TooltipText.Row("Current", TooltipText.Value($"{stats.GetMorale()} / {stats.GetMaxMorale()}")),
            TooltipText.Row("Balance", TooltipText.Value(stats.GetMoraleBalance().ToString())),
        };
        float delta = stats.GetMoraleDeltaPercent();
        if (delta != 0f) lines.Add(TooltipText.Row("Growth and output", TooltipText.Signed(delta, "%")));
        d.effects = TooltipText.Lines(lines);
        return true;
    }

    private static bool Satisfaction(StatManager stats, TooltipData d)
    {
        d.title = "Satisfaction";
        d.type = "Civilization Stat";
        if (stats == null) return true;
        d.description = $"<i>{stats.GetSatisfactionLevelDescription()}</i>";
        d.effects = TooltipText.Lines(new[]
        {
            TooltipText.Row("Level", TooltipText.Value(stats.GetSatisfactionLevelName())),
            TooltipText.Row("Points", TooltipText.Value(stats.GetSatisfactionPoints().ToString())),
        });
        return true;
    }

    private static bool Resource(string name, TooltipData d)
    {
        if (!GameCatalog.Resources.TryGet(name, out var unit)) return false;
        d.title = unit.name;
        d.type = !string.IsNullOrEmpty(unit.section) ? unit.section : "Resource";
        if (!string.IsNullOrEmpty(unit.description)) d.description = $"<i>{unit.description}</i>";
        var lines = new List<string>();
        var units = GameUnitsLogic.Instance;
        var slot = units != null ? units.GetResourceSlotFromName(unit.name) : null;
        if (slot != null)
        {
            lines.Add(TooltipText.Row("Stored", TooltipText.Value($"{TooltipContent.Amount(slot.amount)} / {slot.maxAmount:0.##}")));
            var production = GlobalProductionManager.Instance;
            if (production != null) lines.Add(TooltipText.Row("Per Second", TooltipText.Signed(production.GetNetProductionRate(unit.name), "/s")));
        }
        else lines.Add(TooltipText.Muted("Not yet discovered"));
        if (unit.role == ResourceRole.Food) lines.Add(TooltipText.Muted("Feeds the Population; stored Food brings newcomers"));
        if (unit.role == ResourceRole.Research) lines.Add(TooltipText.Muted("Every citizen produces it"));
        d.effects = TooltipText.Lines(lines);
        return true;
    }

    private static bool Technology(string name, TooltipData d)
    {
        if (!GameCatalog.Technologies.TryGet(name, out var tech)) return false;
        var unit = tech.gameUnit;
        d.title = unit != null ? unit.name : name;
        d.type = unit != null && !string.IsNullOrEmpty(unit.type) ? $"{unit.type} Technology" : "Technology";
        if (unit != null && !string.IsNullOrEmpty(unit.description)) d.description = $"<i>{unit.description}</i>";
        var units = GameUnitsLogic.Instance;
        bool unlocked = units != null && units.IsTechnologyUnlocked(d.title);
        d.effects = unlocked ? TooltipText.Good("Unlocked") : TooltipText.Muted("Not yet unlocked");
        return true;
    }

    private static bool Section(string name, TooltipData d)
    {
        var section = SectionData.GetSectionData(name);
        if (section == null) return false;
        // A section's title field is its banner line; its name is what players call it.
        d.title = !string.IsNullOrEmpty(section.name) ? section.name : name;
        d.type = "Section";
        if (!string.IsNullOrEmpty(section.description)) d.description = $"<i>{section.description}</i>";
        if (!string.IsNullOrEmpty(section.title)) d.summary = section.title;
        return true;
    }

    // The calendar: seventh, ritual seventh, phase, echo and cycle, with where the world stands in it now.
    private static bool Time(string unit, TooltipData d)
    {
        var time = TimeSystemLogic.Instance;
        string row = null;
        switch (unit.ToLowerInvariant())
        {
            case "seventh":
                d.title = "Seventh";
                if (time != null) row = TooltipText.Row("Now", TooltipText.Value($"{time.CurrentSeventh} of {TimeSystemLogic.SeventhsPerPhase}"));
                break;
            case "ritual seventh":
                d.title = "Ritual Seventh";
                if (time != null)
                {
                    int until = TimeSystemLogic.SeventhsPerPhase - time.CurrentSeventh;
                    row = TooltipText.Row("Next", until == 0 ? TooltipText.Good("Now") : TooltipText.Value($"in {until} Sevenths"));
                }
                break;
            case "phase":
                d.title = "Phase";
                if (time != null && time.CurrentPhaseUnit != null) row = TooltipText.Row("Now", TooltipText.Value(time.CurrentPhaseUnit.unitName));
                break;
            case "echo":
                d.title = "Echo";
                if (time != null && time.CurrentEchoUnit != null) row = TooltipText.Row("Now", TooltipText.Value(time.CurrentEchoUnit.unitName));
                break;
            case "cycle":
                d.title = "Cycle";
                if (time != null) row = TooltipText.Row("Now", TooltipText.Value($"Cycle {time.CurrentCycle}"));
                break;
            default:
                return false;
        }
        d.type = "Passage of Time";
        if (row != null) d.effects = row;
        return true;
    }

    private static bool Term(string name, TooltipData d)
    {
        var people = PopGrowthLogic.Instance;
        var stats = StatManager.Instance;
        switch (name.ToLowerInvariant())
        {
            case "population":
                d.title = "Population";
                d.type = "Settlement";
                if (people != null) d.effects = TooltipText.Row("Citizens", TooltipText.Value(people.population.ToString()));
                return true;
            case "housing":
                d.title = "Housing";
                d.type = "Settlement";
                if (people != null)
                {
                    d.effects = TooltipText.Lines(new[]
                    {
                        TooltipText.Row("Total", TooltipText.Value(people.housing.ToString())),
                        TooltipText.Row("Free", TooltipText.Value(Mathf.Max(0, people.housing - people.population).ToString())),
                    });
                }
                return true;
            case "vagrants":
                d.title = "Vagrants";
                d.type = "Settlement";
                if (people != null) d.effects = TooltipText.Row("Wandering", TooltipText.Value(people.vagrants.ToString()));
                return true;
            case "critical-failure":
                d.title = "Critical Failure";
                d.type = "Decision Outcome";
                if (stats != null)
                {
                    bool impossible = stats.GetSavingRollChancePercentCapped() >= ChorusRules.CriticalBand;
                    d.effects = TooltipText.Row("With your Piety", impossible ? TooltipText.Good("Impossible") : TooltipText.Bad("Possible"));
                }
                return true;
            case "luck":
                d.title = "Luck";
                d.type = "Decision Odds";
                d.effects = TooltipText.Lines(new[]
                {
                    TooltipText.Row("Fated", TooltipText.Value("above 90%")),
                    TooltipText.Row("Blessed", TooltipText.Value("60 - 90%")),
                    TooltipText.Row("Gamble", TooltipText.Value("40 - 60%")),
                    TooltipText.Row("Cursed", TooltipText.Value("10 - 40%")),
                    TooltipText.Row("Forsaken", TooltipText.Value("below 10%")),
                });
                return true;
            case "enlightenment":
            {
                d.title = "Enlightenment";
                d.type = "Research";
                var units = GameUnitsLogic.Instance;
                if (units != null) d.effects = TooltipText.Row("Technologies enlightened", TooltipText.Value(units.CountEnlightenedTechnologies().ToString()));
                return true;
            }
            default:
                return false;
        }
    }

    // ===== WORDS =====

    /// <summary>"waltz" → "Waltz".</summary>
    public static string Title(string word) => EventText.Humanize(word);

    /// <summary>"discoveryEfficiency" → "Discovery Efficiency".</summary>
    public static string SplitCamel(string word)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;
        var sb = new StringBuilder();
        for (int i = 0; i < word.Length; i++)
        {
            char c = word[i];
            if (i == 0) sb.Append(char.ToUpperInvariant(c));
            else
            {
                if (char.IsUpper(c)) sb.Append(' ');
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
