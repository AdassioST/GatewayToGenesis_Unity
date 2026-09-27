using UnityEngine;

/// <summary>
/// The population formulas of ARCHITECTURE.md §4, with no scene state: what a new citizen costs in food, how much
/// food is worth gathering, how much the population eats, how stored food turns into people, housing, vagrants and
/// starvation. <see cref="PopGrowthLogic"/> owns the counts and applies the results. Tested in <c>EconomyRulesTests</c>.
/// </summary>
public static class PopulationRules
{
    /// <summary>
    /// Food for the next citizen: base × clamp(1 − morale delta % / 100, min, max), at least 1. Morale above its
    /// balance makes growth cheaper, below it dearer.
    /// </summary>
    public static float FoodThreshold(float baseThreshold, float moraleDeltaPercent, float minFactor, float maxFactor)
    {
        float factor = Mathf.Clamp(1f - Mathf.Round(moraleDeltaPercent) / 100f, minFactor, maxFactor);
        return Mathf.Max(1f, baseThreshold * factor);
    }

    /// <summary>
    /// The most food that can still become people: unlimited with vagrants; otherwise enough for every free home,
    /// or, with none free, only up to one citizen's worth in store (so food cannot pile up uneaten).
    /// </summary>
    public static float MaxUsefulFoodGain(bool allowVagrants, float threshold, int freeHousing, float currentFood)
    {
        if (allowVagrants) return float.MaxValue;
        if (freeHousing <= 0) return Mathf.Max(0f, threshold - currentFood);
        return Mathf.Max(0f, freeHousing * threshold);
    }

    /// <summary>Food eaten per second: (buffer + e^(rate / sustainability tier × population)) × demand modifier.</summary>
    public static float FoodDemand(float buffer, float rateConstant, float sustainabilityTier, int population, float demandModifier)
    {
        return (buffer + Mathf.Exp(rateConstant / Mathf.Max(0.0001f, sustainabilityTier) * population)) * demandModifier;
    }

    /// <summary>What stored food turns into: citizens who found a home, vagrants who did not, and the food spent.</summary>
    public readonly struct Growth
    {
        public readonly int housed;
        public readonly int vagrants;
        public readonly float foodSpent;

        public Growth(int housed, int vagrants, float foodSpent)
        {
            this.housed = housed;
            this.vagrants = vagrants;
            this.foodSpent = foodSpent;
        }

        public int Arrivals => housed + vagrants;
    }

    /// <summary>
    /// Every full <paramref name="threshold"/> of food becomes one arrival (at most <paramref name="maxArrivals"/>):
    /// housed while homes are free, a vagrant after that if vagrants are allowed, otherwise growth stops.
    /// </summary>
    public static Growth Grow(float food, float threshold, int freeHousing, bool allowVagrants, int maxArrivals)
    {
        int housed = 0, vagrants = 0;
        float spent = 0f;
        while (housed + vagrants < maxArrivals && food - spent + 1e-3f >= threshold)
        {
            if (housed < freeHousing) housed++;
            else if (allowVagrants) vagrants++;
            else break;
            spent += threshold;
        }
        return new Growth(housed, vagrants, spent);
    }

    /// <summary>Housing = (base + flat) × (1 + % / 100), rounded, never below 0.</summary>
    public static int Housing(int baseHousing, ModifierValue modifiers) => Mathf.Max(0, Mathf.RoundToInt(modifiers.ApplyTo(baseHousing)));

    /// <summary>Vagrants who move into free homes this second (at most <paramref name="perSecond"/>).</summary>
    public static int VagrantsMovingIn(int vagrants, int freeHousing, int perSecond) => Mathf.Max(0, Mathf.Min(perSecond, Mathf.Min(vagrants, freeHousing)));

    /// <summary>A citizen starves while food is both running out (net rate below 0) and gone (none stored).</summary>
    public static bool IsStarving(float netFoodRate, float storedFood) => netFoodRate < 0f && storedFood <= 0f;
}
