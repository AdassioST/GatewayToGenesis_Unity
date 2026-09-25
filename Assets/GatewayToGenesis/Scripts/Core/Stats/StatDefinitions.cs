using System;
using System.Collections.Generic;

/// <summary>
/// The civilization stat vocabulary, shared by <see cref="StatManager"/>, validation, effects and events.
/// Stat names are case-insensitive everywhere; these tables hold the canonical spelling.
/// </summary>
public static class StatDefinitions
{
    public enum StatKind
    {
        Unknown,
        Pillar,
        Substat,
        Derived,
        Global
    }

    public static readonly string[] Pillars = { "aureus", "regalia", "waltz", "chorus" };

    /// <summary>Each pillar feeds two substats.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> PillarSubstats = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "aureus", new[] { "innovation", "piety" } },
        { "regalia", new[] { "authority", "ambition" } },
        { "waltz", new[] { "symphony", "euphony" } },
        { "chorus", new[] { "arcane", "secrecy" } },
    };

    public static readonly string[] Substats = { "innovation", "piety", "authority", "ambition", "symphony", "euphony", "arcane", "secrecy" };

    /// <summary>Derived stat → the substat it grows from.</summary>
    public static readonly IReadOnlyDictionary<string, string> DerivedSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "discoveryEfficiency", "innovation" },
        { "savingRollChance", "piety" },
        { "legendEffectiveness", "authority" },
        { "expeditionCostMod", "ambition" },
        { "expeditionTimeMod", "ambition" },
        { "satisfactionEffectiveness", "symphony" },
        { "moraleLossMod", "euphony" },
        { "moraleRecoveryMod", "euphony" },
        { "clickPowerBonus", "arcane" },
        { "magicEffectiveness", "arcane" },
    };

    public const string CommunionStage = "communionStage";

    /// <summary>
    /// Civilization-wide values that accept modifiers. "morale" modifiers shift displayed morale;
    /// the others adjust thresholds.
    /// </summary>
    public const string MaxMorale = "maxmorale";
    public const string MoraleBalance = "moralebalance";
    public const string SatisfactionUpgradeThreshold = "satisfactionupgradethreshold";
    public const string Morale = "morale";
    public const string SatisfactionPoints = "satisfactionpoints";
    public const string SatisfactionLevel = "satisfactionlevel";

    public static readonly string[] Globals = { MaxMorale, MoraleBalance, SatisfactionUpgradeThreshold, Morale, SatisfactionPoints, SatisfactionLevel };

    private static readonly HashSet<string> PillarSet = new HashSet<string>(Pillars, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SubstatSet = new HashSet<string>(Substats, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> GlobalSet = new HashSet<string>(Globals, StringComparer.OrdinalIgnoreCase)
    {
        // Spellings used by older content and code.
        "satisfaction", "maxMorale", "moraleBalance", "satisfactionUpgradeThreshold", "satisfactionPoints", "satisfactionLevel"
    };

    public static StatKind KindOf(string statName)
    {
        if (string.IsNullOrEmpty(statName)) return StatKind.Unknown;
        if (PillarSet.Contains(statName)) return StatKind.Pillar;
        if (SubstatSet.Contains(statName)) return StatKind.Substat;
        if (DerivedSource.ContainsKey(statName) || string.Equals(statName, CommunionStage, StringComparison.OrdinalIgnoreCase)) return StatKind.Derived;
        if (GlobalSet.Contains(statName)) return StatKind.Global;
        return StatKind.Unknown;
    }

    public static bool IsKnown(string statName) => KindOf(statName) != StatKind.Unknown;

    /// <summary>Canonical lower-case key used for modifier storage ("maxMorale" → "maxmorale", "satisfaction" → "satisfactionlevel").</summary>
    public static string Key(string statName)
    {
        if (string.IsNullOrEmpty(statName)) return statName;
        string key = statName.Trim().ToLowerInvariant();
        return key == "satisfaction" ? SatisfactionLevel : key;
    }

    public static string ParentPillar(string substat)
    {
        foreach (var pair in PillarSubstats)
        {
            foreach (var child in pair.Value)
            {
                if (string.Equals(child, substat, StringComparison.OrdinalIgnoreCase)) return pair.Key;
            }
        }
        return null;
    }
}
