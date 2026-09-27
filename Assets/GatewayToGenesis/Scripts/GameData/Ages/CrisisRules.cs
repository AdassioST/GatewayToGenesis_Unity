using System;
using System.Collections.Generic;

/// <summary>
/// How hard an Age Crisis falls, as numbers a designer tunes in the Age's asset. Every value here is a proposal for
/// the first playable prototype (roadmap decisions D01-D03), not canon: playtest before trusting them.
/// </summary>
[Serializable]
public class CrisisTuning
{
    [UnityEngine.Tooltip("Severity every civilization starts from (0 = no losses beyond the minimum, 1 = the maximum).")]
    public float baseSeverity = 0.55f;

    [UnityEngine.Header("Preparation (lowers severity)")]
    [UnityEngine.Tooltip("Per researched technology listed in the Age's preparation technologies.")]
    public float perPreparedTechnology = 0.08f;
    [UnityEngine.Tooltip("Per point of the Age's preparation event score, up to the cap.")]
    public float perPreparationPoint = 0.05f;
    public float preparationCap = 0.25f;
    [UnityEngine.Tooltip("Per point of the relief score (desperate short-term relief, like ash-bread), up to the cap.")]
    public float perReliefPoint = 0.04f;
    public float reliefCap = 0.2f;
    [UnityEngine.Tooltip("Stored food per citizen that counts as a full reserve; a full reserve lowers severity by the weight.")]
    public float reservePerCitizen = 4f;
    public float reserveWeight = 0.15f;
    [UnityEngine.Tooltip("Per kind of stored food beyond the first (earth-beans, roots, grains...), up to the cap: the land is a chord, not a note.")]
    public float perStoreKind = 0.02f;
    public float storeVarietyCap = 0.08f;
    [UnityEngine.Tooltip("Per explored world tile that eases this crisis (fertile ground for a famine), up to the cap.")]
    public float perEasingTile = 0.03f;
    public float easingTileCap = 0.15f;
    [UnityEngine.Tooltip("Per Crisis Advantage (granted by the Age's advantage technologies), up to the cap.")]
    public float perAdvantage = 0.03f;
    public float advantageCap = 0.12f;
    [UnityEngine.Tooltip("Era Score this Age above the threshold redeems severity (Ages.md: excess Era Score is a saving grace buffer), per point, up to the cap.")]
    public int eraScoreThreshold = 12;
    public float perExcessEraScore = 0.01f;
    public float eraScoreCap = 0.15f;

    [UnityEngine.Header("Aggravation (raises severity)")]
    [UnityEngine.Tooltip("Per point of the aggravation event score (monocrop drift for the famine).")]
    public float perAggravationPoint = 0.06f;
    [UnityEngine.Tooltip("Stores that are mostly Auric peaches: this weight when all of them are, proportionally above the threshold share.")]
    public float monocropStoreWeight = 0.08f;
    [UnityEngine.Range(0f, 1f)] public float monocropStoreThreshold = 0.6f;
    [UnityEngine.Tooltip("Crowding: this weight at crowdingPopulation citizens or more, proportionally below.")]
    public float crowdingWeight = 0.15f;
    public int crowdingPopulation = 150;

    [UnityEngine.Header("Losses")]
    [UnityEngine.Tooltip("Share of the population lost at severity 0 and at severity 1.")]
    public float minimumLoss = 0.1f;
    public float maximumLoss = 0.6f;
    [UnityEngine.Tooltip("Citizens who always survive, however badly it goes (both outcomes reach the next Age).")]
    public int survivorFloor = 5;
    [UnityEngine.Tooltip("While the crisis is declared, Food output falls by this % at severity 1 (proportionally below).")]
    public float foodOutputPenalty = 40f;
}

/// <summary>What the civilization brings into the crisis. <see cref="AgeProgression"/> gathers it from the systems.</summary>
public struct CrisisInputs
{
    public int population;
    /// <summary>Food and the stores' food value.</summary>
    public float foodStored;
    /// <summary>Kinds of stored food held in quantity.</summary>
    public int storeVariety;
    /// <summary>Share of the stores that is Auric peaches, 0-1.</summary>
    public float peachShare;
    public int preparedTechnologies;
    public int preparationPoints;
    public int reliefPoints;
    public int aggravationPoints;
    public int easingTiles;
    public int advantages;
    public int eraScore;
}

/// <summary>One line of the severity breakdown: what it is and how much it adds (negative eases the crisis).</summary>
public readonly struct CrisisFactor
{
    public readonly string label;
    public readonly float value;

    public CrisisFactor(string label, float value)
    {
        this.label = label;
        this.value = value;
    }
}

/// <summary>The soft warning the world gives before a famine is named: the Auric peach's colour (the vault's "Nutrient Thermometer").</summary>
public enum HarvestQuality { Golden, Pink, Brown }

/// <summary>
/// The Age Crisis in numbers, with no scene state (tested in <c>AgeRulesTests</c>). Severity is a sum of named
/// factors clamped to 0..1, so the player can read exactly why a crisis is as bad as it is; losses scale between
/// the tuning's minimum and maximum share and never take the survivor floor. The vault's rule for the first crisis
/// holds for every outcome: it "always leads to the Age of Renewal regardless of the success, the only difference
/// between handling properly or poorly the famine is the amount of people surviving through it".
/// </summary>
public static class CrisisRules
{
    /// <summary>Labels of the factors; the Age's asset names its own scores (preparation, relief, aggravation).</summary>
    public struct Labels
    {
        public string preparation, relief, aggravation, easingTiles;

        public static Labels Default => new Labels { preparation = "Preparation", relief = "Desperate relief", aggravation = "Aggravation", easingTiles = "Explored land" };
    }

    public static List<CrisisFactor> Factors(in CrisisInputs inputs, CrisisTuning tuning, Labels labels)
    {
        tuning = tuning ?? new CrisisTuning();
        var factors = new List<CrisisFactor> { new CrisisFactor("The crisis itself", tuning.baseSeverity) };
        Add(factors, "Crisis technologies researched", -inputs.preparedTechnologies * tuning.perPreparedTechnology);
        Add(factors, labels.preparation, -Math.Min(tuning.preparationCap, Math.Max(0, inputs.preparationPoints) * tuning.perPreparationPoint));
        Add(factors, labels.relief, -Math.Min(tuning.reliefCap, Math.Max(0, inputs.reliefPoints) * tuning.perReliefPoint));
        Add(factors, "Food in reserve", -tuning.reserveWeight * ReserveShare(inputs.population, inputs.foodStored, tuning));
        Add(factors, "Varied stores", -Math.Min(tuning.storeVarietyCap, Math.Max(0, inputs.storeVariety - 1) * tuning.perStoreKind));
        Add(factors, labels.easingTiles, -Math.Min(tuning.easingTileCap, Math.Max(0, inputs.easingTiles) * tuning.perEasingTile));
        Add(factors, "Crisis Advantages", -Math.Min(tuning.advantageCap, Math.Max(0, inputs.advantages) * tuning.perAdvantage));
        Add(factors, "Era Score beyond the need", -Math.Min(tuning.eraScoreCap, Math.Max(0, inputs.eraScore - tuning.eraScoreThreshold) * tuning.perExcessEraScore));
        Add(factors, labels.aggravation, Math.Max(0, inputs.aggravationPoints) * tuning.perAggravationPoint);
        float threshold = Math.Min(0.99f, Math.Max(0f, tuning.monocropStoreThreshold));
        Add(factors, "Stores of peaches alone", tuning.monocropStoreWeight * Clamp01((inputs.peachShare - threshold) / (1f - threshold)));
        Add(factors, "Crowding", tuning.crowdingWeight * Clamp01(inputs.population / (float)Math.Max(1, tuning.crowdingPopulation)));
        return factors;
    }

    /// <summary>How much of a full food reserve the stores hold, 0 to 1.</summary>
    public static float ReserveShare(int population, float foodStored, CrisisTuning tuning)
    {
        if (population <= 0) return 1f;
        float full = population * Math.Max(0.01f, tuning.reservePerCitizen);
        return Clamp01(foodStored / full);
    }

    public static float Severity(IEnumerable<CrisisFactor> factors)
    {
        float sum = 0f;
        foreach (var factor in factors) sum += factor.value;
        return Clamp01(sum);
    }

    /// <summary>Share of the population the crisis takes at <paramref name="severity"/>.</summary>
    public static float LossShare(float severity, CrisisTuning tuning)
    {
        tuning = tuning ?? new CrisisTuning();
        float min = Clamp01(tuning.minimumLoss), max = Math.Max(min, Clamp01(tuning.maximumLoss));
        return min + (max - min) * Clamp01(severity);
    }

    /// <summary>Citizens lost: never more than the population above the survivor floor, never negative.</summary>
    public static int Deaths(int population, float severity, CrisisTuning tuning)
    {
        tuning = tuning ?? new CrisisTuning();
        if (population <= 0) return 0;
        int deaths = (int)Math.Round(population * LossShare(severity, tuning), MidpointRounding.AwayFromZero);
        int canLose = Math.Max(0, population - Math.Max(0, tuning.survivorFloor));
        return Math.Min(canLose, Math.Max(0, deaths));
    }

    /// <summary>The Food output change (a negative %) while a declared crisis lasts.</summary>
    public static float FoodOutputPercent(float severity, CrisisTuning tuning) => -Math.Max(0f, (tuning ?? new CrisisTuning()).foodOutputPenalty) * Clamp01(severity);

    /// <summary>
    /// The peach's colour: golden until the crisis has begun, pink once it has (it looks like an ordinary bad harvest),
    /// brown once it is declared.
    /// </summary>
    public static HarvestQuality Harvest(bool begun, bool declared) => declared ? HarvestQuality.Brown : begun ? HarvestQuality.Pink : HarvestQuality.Golden;

    /// <summary>
    /// The Age that follows, when the outcome decides it: the first entry whose <c>maxSeverity</c> is at or above the
    /// severity (entries in the asset's order), else the last. Null when there are none.
    /// </summary>
    public static string NextAge(IReadOnlyList<(string ageId, float maxSeverity)> next, float severity)
    {
        if (next == null || next.Count == 0) return null;
        foreach (var (ageId, maxSeverity) in next)
        {
            if (severity <= maxSeverity) return ageId;
        }
        return next[next.Count - 1].ageId;
    }

    private static void Add(List<CrisisFactor> factors, string label, float value)
    {
        if (Math.Abs(value) > 0.0001f) factors.Add(new CrisisFactor(label, value));
    }

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
