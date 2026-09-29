using System;

/// <summary>
/// What the civilization's eight aspects do, read in one place by the systems they drive. <see cref="StatManager"/>
/// owns the values (pillar → aspect → derived stat, ARCHITECTURE.md §4); this is the read-only side, safe to call with no
/// StatManager in the scene (every property is then neutral: no bonus, multiplier 1).
/// <list type="bullet">
/// <item>Innovation: <see cref="DiscoveryEfficiency"/> makes research cheaper (GameUnitsLogic).</item>
/// <item>Piety: <see cref="SavingRollPercent"/> is added to every decision roll (choruses).</item>
/// <item>Authority: <see cref="LegendEffectiveness"/> strengthens seated legends (GovernmentLogic).</item>
/// <item>Ambition: <see cref="Expedition"/> lightens the road's toll and quickens the march (WorldSystem, Expeditions).</item>
/// <item>Symphony: <see cref="SatisfactionEffectiveness"/> boosts satisfaction gains (StatManager).</item>
/// <item>Euphony: morale losses shrink and morale recovers faster (StatManager).</item>
/// <item>Arcane: click power (GameUnitsLogic) and <see cref="MagicEffectiveness"/>, open for the Magic Arts: scale a
/// spell's or rite's magnitude with <see cref="Magic"/>.</item>
/// <item>Secrecy: <see cref="CommunionStage"/> and <see cref="CommunionEffectiveness"/>, open for Communion: scale a
/// Communion bonus with <see cref="Communion"/>.</item>
/// </list>
/// Listen to <see cref="StatManager.OnStatsChanged"/> to hear when any of these changes.
/// </summary>
public static class CivilizationProperties
{
    private static StatManager Stats => StatManager.Instance;

    /// <summary>Innovation: the share research costs less, 0-1 (capped by the StatManager).</summary>
    public static float DiscoveryEfficiency => Stats != null ? Stats.GetDiscoveryEfficiencyCapped() : 0f;

    /// <summary>Piety: points added to every decision roll (capped by the StatManager).</summary>
    public static float SavingRollPercent => Stats != null ? Stats.GetSavingRollChancePercentCapped() : 0f;

    /// <summary>Authority: the multiplier on what seated legends give (1.12 = 12% more).</summary>
    public static float LegendEffectiveness => CouncilRules.EffectivenessMultiplier(Stats != null ? Stats.GetDerivedValue("legendEffectiveness") : 0f);

    /// <summary>Ambition: what the road costs an expedition and how long it takes it to advance.</summary>
    public static ExpeditionAmbition Expedition => Stats != null
        ? new ExpeditionAmbition { cost = Stats.GetExpeditionCostMultiplier(), time = Stats.GetExpeditionTimeMultiplier() }
        : ExpeditionAmbition.None;

    /// <summary>Symphony: the multiplier on satisfaction gains.</summary>
    public static float SatisfactionEffectiveness => Stats != null ? Stats.GetSatisfactionEffectivenessMultiplier() : 1f;

    /// <summary>Arcane: the multiplier on magic (1.12 = 12% stronger).</summary>
    public static float MagicEffectiveness => Stats != null ? Stats.GetMagicEffectivenessMultiplier() : 1f;

    /// <summary>A magical effect's magnitude as the civilization's Arcane makes it.</summary>
    public static float Magic(float baseMagnitude) => baseMagnitude * MagicEffectiveness;

    /// <summary>Secrecy: the Communion stage reached (one per 5 Secrecy).</summary>
    public static int CommunionStage => Stats != null ? Stats.GetCommunionStage() : 0;

    /// <summary>Secrecy: the multiplier on every Communion bonus.</summary>
    public static float CommunionEffectiveness => Stats != null ? Stats.GetCommunionEffectivenessMultiplier() : 1f;

    /// <summary>A Communion bonus as the civilization's Secrecy makes it.</summary>
    public static float Communion(float baseBonus) => baseBonus * CommunionEffectiveness;

    // ===== WORDS =====

    /// <summary>A derived stat's player-facing name (the civilization stats table's Properties).</summary>
    public static string Name(string derived)
    {
        switch (StatDefinitions.Key(derived))
        {
            case "discoveryefficiency": return "Discovery Rate";
            case "savingrollchance": return "Saving Roll Chance";
            case "legendeffectiveness": return "Legend Effectiveness";
            case "expeditioncostmod": return "Expedition Cost";
            case "expeditiontimemod": return "Completion Time";
            case "satisfactioneffectiveness": return "Satisfaction Effectiveness";
            case "moralelossmod": return "Morale Loss";
            case "moralerecoverymod": return "Morale Recovery";
            case "clickpowerbonus": return "Click Power";
            case "magiceffectiveness": return "Magic Effectiveness";
            case "communioneffectiveness": return "Communion Bonus Effectiveness";
            case "communionstage": return "Communion Stage";
            default: return KeywordLiveValues.SplitCamel(derived);
        }
    }

    /// <summary>
    /// A derived stat's value as it acts in play (the capped, floored or multiplied form the systems above read), for
    /// <see cref="Describe"/>.
    /// </summary>
    public static float EffectiveValue(string derived)
    {
        var stats = Stats;
        if (stats == null || string.IsNullOrEmpty(derived)) return 0f;
        switch (StatDefinitions.Key(derived))
        {
            case "discoveryefficiency": return stats.GetDiscoveryEfficiencyPercentCapped();
            case "savingrollchance": return stats.GetSavingRollChancePercentCapped();
            case "expeditioncostmod": return stats.GetExpeditionCostMultiplier();
            case "expeditiontimemod": return stats.GetExpeditionTimeMultiplier();
            default: return stats.GetDerivedValue(derived);
        }
    }

    /// <summary>
    /// What a derived stat does at <paramref name="value"/>, in a player's words ("+4.5% research discount"). Percent
    /// stats take their percentage; Expedition Cost and Time and Morale Recovery take their multiplier.
    /// </summary>
    public static string Describe(string derived, float value)
    {
        switch (StatDefinitions.Key(derived))
        {
            case "discoveryefficiency": return $"Research costs {value:0.#}% less";
            case "savingrollchance": return $"{Signed(value)} to every decision roll";
            case "legendeffectiveness": return $"Seated legends give {Signed(value)}% more";
            case "expeditioncostmod": return $"Expeditions cost {Percent(1f - value)}% less to outfit, suffer that much less attrition and eat that many fewer rations";
            case "expeditiontimemod": return $"Expeditions take {Percent(1f - value)}% less time: they advance {Percent(value > 0f ? 1f / value - 1f : 0f)}% faster";
            case "satisfactioneffectiveness": return $"Satisfaction gains {Signed(value)}%";
            case "moralelossmod": return $"Morale losses {Math.Min(100f, Math.Max(0f, value)):0.#}% smaller";
            case "moralerecoverymod": return $"Morale recovers {(value - 1f) * 100f:0.#}% faster";
            case "clickpowerbonus": return $"Click power {Signed(value)}%";
            case "magiceffectiveness": return $"Magic {Signed(value)}% stronger";
            case "communioneffectiveness": return $"Communion bonuses {Signed(value)}% stronger";
            case "communionstage": return $"Communion stage {Math.Max(0, (int)Math.Round(value))}";
            default: return $"{Name(derived)} {value:0.#}";
        }
    }

    private static string Signed(float value) => value.ToString("+0.#;-0.#;0");

    private static string Percent(float share) => (share * 100f).ToString("0.#");
}
