using UnityEngine;

/// <summary>Housing and food-shortage rules. Calendar-based demography and ration units live in GrowthRules.</summary>
public static class PopulationRules
{
    /// <summary>Housing = (base + flat) × (1 + % / 100), rounded, never below 0.</summary>
    public static int Housing(int baseHousing, ModifierValue modifiers) => Mathf.Max(0, Mathf.RoundToInt(modifiers.ApplyTo(baseHousing)));

    /// <summary>Vagrants who move into free homes this second (at most <paramref name="perSecond"/>).</summary>
    public static int VagrantsMovingIn(int vagrants, int freeHousing, int perSecond) => Mathf.Max(0, Mathf.Min(perSecond, Mathf.Min(vagrants, freeHousing)));

    /// <summary>A citizen starves while food is both running out (net rate below 0) and gone (none stored).</summary>
    public static bool IsStarving(float netFoodRate, float storedFood) => netFoodRate < 0f && storedFood <= 0f;
}
