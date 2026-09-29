using System;
using UnityEngine;

/// <summary>Units and scenario assumptions, not universal historical constants. See Docs/Planning/POPULATION_GROWTH.md.</summary>
[Serializable]
public class GrowthTuning
{
    [Header("Calendar and food")]
    [Tooltip("One Cycle represents one year. Food is a daily ration equivalent, not a person or a kilogram.")]
    public float daysPerYear = 365.2425f;
    [Tooltip("Energy represented by one Food. Planning baseline; needs differ by age, activity and climate.")]
    public float kcalPerFood = 2100f;
    public float kcalPerPersonDay = 2100f;
    [Tooltip("Additional distribution losses. Ration technologies reduce this loss, never basic nutritional needs.")]
    [Range(0f, 1f)] public float distributionLoss = 0.2f;

    [Header("Annual vital rates, per 1,000 people")]
    [Tooltip("Illustrative preindustrial scenario within historical ranges, not an estimate for every era.")]
    public float birthsPerThousand = 35f;
    public float deathsPerThousand = 30f;
    [Tooltip("Additional deaths per 10,000 per day at complete food deprivation, after the grace period. Scenario assumption.")]
    public float famineDeathsPerTenThousandDay = 2f;
    public float famineGraceDays = 7f;

    [Header("Founding survivors")]
    [Tooltip("The founding members waiting at the gates when a world begins. Never refilled by an Age change.")]
    public int foundingMigrants = 21;
    [Tooltip("Rations issued to a survivor let in at the gates. Arrival provisions, not a biological cost of birth.")]
    public float arrivalRations = 12f;
    [Tooltip("Least real seconds between two survivors let in. Presentation pacing, independent of fertility. The founding members are let in at once, each as soon as their rations are gathered.")]
    public float arrivalIntervalSeconds = 1.5f;
    [Tooltip("People whose rations were paid once, for good, at the gates (the founders): daily food upkeep counts only the people beyond them, and while fewer live here, another mouth costs nothing to keep.")]
    public int provisionedPeople = 21;

    [Header("Frontier pacing (gameplay, not history)")]
    [Tooltip("People at or below which frontier pacing is full.")]
    public int frontierFullBelow = 21;
    [Tooltip("People at which frontier pacing has faded out and only the historical rates remain.")]
    public int realismFrom = 150;
    [Tooltip("Birth-rate multiplier at full frontier pacing: a founding band has its first child in minutes, not hours.")]
    public float frontierBirthPace = 100f;

    [Header("Caravans (frontier only)")]
    [Tooltip("Caravans of survivors per Cycle at full pull: frontier pacing and well-stocked stores.")]
    public float caravansPerCycle = 36f;
    public int caravanMin = 3, caravanMax = 8;
    [Tooltip("Days of rations in store for everyone at which the stores draw caravans fully.")]
    public float caravanReserveDays = 30f;
}

[CreateAssetMenu(fileName = "Growth", menuName = "Game Object/Growth Settings", order = 15)]
public class GrowthSettings : ScriptableObject
{
    public GrowthTuning tuning = new GrowthTuning();
}
