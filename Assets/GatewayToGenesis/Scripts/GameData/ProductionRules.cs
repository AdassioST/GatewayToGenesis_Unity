using UnityEngine;

/// <summary>
/// The production formulas of ARCHITECTURE.md §4, with no scene state: what buildings produce and consume, a
/// resource's net rate, click power, build costs and discounted research costs. <see cref="GlobalProductionManager"/>
/// and <see cref="GameUnitsLogic"/> gather the inputs (ledgers, slots, stats) and ask these rules for results.
/// Tested in <c>EconomyRulesTests</c>.
/// </summary>
public static class ProductionRules
{
    /// <summary>A building's output of one resource: rate × units × (1 + efficiency % / 100).</summary>
    public static float UnitOutput(float ratePerUnit, float units, float efficiencyPercent) => ratePerUnit * units * (1f + efficiencyPercent / 100f);

    /// <summary>A building's consumption of one resource: rate × units (efficiency does not change what it eats).</summary>
    public static float UnitConsumption(float ratePerUnit, float units) => ratePerUnit * units;

    /// <summary>
    /// A resource's rate, built up from its parts: output (buildings, scaling, positive flat modifiers), consumption
    /// (buildings, negative flat modifiers) and the % applied to output (modifiers + morale).
    /// </summary>
    public struct Rate
    {
        public float output;
        public float consumption;
        public float percent;

        /// <summary>A flat modifier: positive adds output, negative adds consumption.</summary>
        public void AddFlat(float flat)
        {
            if (flat >= 0f) output += flat;
            else consumption -= flat;
        }

        /// <summary>Net = output × max(0, 1 + % / 100) − consumption. The % never touches consumption.</summary>
        public float Net => output * Mathf.Max(0f, 1f + percent / 100f) - consumption;
    }

    /// <summary>
    /// Click power = (base + permanent upgrades + flat) × (1 + (% + Click Power Bonus stat) / 100), never below the
    /// base (nor below 0.001).
    /// </summary>
    public static float ClickPower(float baseClickPower, float permanentBonus, ModifierValue modifiers, float clickPowerBonusPercent)
    {
        float value = (baseClickPower + permanentBonus + modifiers.Flat) * (1f + (modifiers.Percent + clickPowerBonusPercent) / 100f);
        return Mathf.Max(Mathf.Max(0.001f, baseClickPower), value);
    }

    /// <summary>
    /// Cost of the next unit for one build requirement: base × (1 + construction cost % / 100), floored at
    /// <paramref name="minimumShare"/> × base; buildings (not units) then grow × e^(costBalance / techTier × owned).
    /// </summary>
    public static float BuildCost(float baseCost, float costPercent, float minimumShare, bool growsWithOwned, float costBalance, float techTier, float owned)
    {
        float cost = Mathf.Max(baseCost * (1f + costPercent / 100f), baseCost * minimumShare);
        if (growsWithOwned) cost *= Mathf.Exp(costBalance / (techTier > 0f ? techTier : 1f) * owned);
        return cost;
    }

    /// <summary>A research cost after Discovery Efficiency (0-1): base − round(base × efficiency), never below 0.</summary>
    public static float ResearchCost(float baseCost, float discoveryEfficiency) => Mathf.Max(0f, baseCost - Mathf.RoundToInt(baseCost * discoveryEfficiency));
}
