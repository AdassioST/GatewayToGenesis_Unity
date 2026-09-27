using UnityEngine;

/// <summary>
/// The stat formulas of ARCHITECTURE.md §4, with no scene state: pillars, substats, thresholds, morale and
/// satisfaction. <see cref="StatManager"/> owns the values and the modifier ledger and asks these rules for results;
/// derived-stat curves are <see cref="StatGrowth"/>. Tested in <c>EconomyRulesTests</c>.
/// </summary>
public static class StatRules
{
    /// <summary>Pillar = (base + flat) × (1 + % / 100), rounded, at least 1.</summary>
    public static int Pillar(int basePillar, ModifierValue modifiers) => Mathf.Max(1, Mathf.RoundToInt(modifiers.ApplyTo(basePillar)));

    /// <summary>A substat before modifiers: its pillar × the pillar's multiplier (rounded, at least 1) + event adjustments, at least 1.</summary>
    public static int SubstatBase(int pillar, float pillarMultiplier, int adjustment)
    {
        int fromPillar = Mathf.Max(1, Mathf.RoundToInt(pillar * pillarMultiplier));
        return Mathf.Max(1, fromPillar + adjustment);
    }

    /// <summary>Substat = (base + flat) × (1 + % / 100), rounded, at least 1.</summary>
    public static int Substat(int substatBase, ModifierValue modifiers) => Mathf.Max(1, Mathf.RoundToInt(modifiers.ApplyTo(substatBase)));

    /// <summary>A threshold (max morale, morale balance, satisfaction upgrade) = (base + flat) × (1 + %), rounded; optionally at least 1.</summary>
    public static int Threshold(int baseValue, ModifierValue modifiers, bool atLeastOne)
    {
        int value = Mathf.RoundToInt(modifiers.ApplyTo(baseValue));
        return atLeastOne ? Mathf.Max(1, value) : value;
    }

    /// <summary>One Communion stage per 5 points of Secrecy.</summary>
    public static int CommunionStage(int secrecy) => Mathf.FloorToInt(secrecy / 5f);

    /// <summary>
    /// A morale shift after Morale Loss Mitigation (0-100%): losses shrink by the mitigation (never turning into a
    /// gain), gains grow by mitigation × <paramref name="gainBalanceFactor"/>.
    /// </summary>
    public static int MoraleShift(int amount, float lossMitigationPercent, float gainBalanceFactor)
    {
        if (amount == 0) return 0;
        float mitigation = Mathf.Clamp(lossMitigationPercent, 0f, 100f) / 100f;
        if (amount < 0) return Mathf.Min(Mathf.RoundToInt(amount * (1f - mitigation)), 0);
        return Mathf.RoundToInt(amount * (1f + mitigation * gainBalanceFactor));
    }

    /// <summary>
    /// Morale after one seventh of drifting toward its resting point <paramref name="target"/>: down by
    /// ⌈Waltz × aboveFactor⌉ from above, up by round(Waltz × Morale Recovery) (at least 1) from below, never past it.
    /// </summary>
    public static int MoraleDrift(int morale, int target, int waltz, float aboveBalanceFactor, float recoveryMod)
    {
        if (morale > target) return Mathf.Max(target, morale - Mathf.CeilToInt(waltz * Mathf.Max(0f, aboveBalanceFactor)));
        if (morale < target) return Mathf.Min(target, morale + Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, waltz) * recoveryMod)));
        return morale;
    }

    /// <summary>
    /// Change of the "dark_morale" event score for one seventh: grows by ⌈deficit × increase⌉ while morale is below its
    /// balance, shrinks by ⌈surplus × decrease⌉ (never below 0) while above.
    /// </summary>
    public static int DarkMoraleChange(int balance, int morale, float increaseFactor, float decreaseFactor, int currentScore)
    {
        int deficit = balance - morale;
        if (deficit > 0) return Mathf.CeilToInt(deficit * Mathf.Max(0f, increaseFactor));
        if (deficit < 0) return -Mathf.Min(Mathf.CeilToInt(-deficit * Mathf.Max(0f, decreaseFactor)), Mathf.Max(0, currentScore));
        return 0;
    }

    /// <summary>Satisfaction gains are boosted by Satisfaction Effectiveness (× multiplier, rounded); losses are not.</summary>
    public static int SatisfactionGain(int change, float effectivenessMultiplier) => change > 0 ? Mathf.RoundToInt(change * effectivenessMultiplier) : change;
}
