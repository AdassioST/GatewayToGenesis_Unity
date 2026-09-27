using System;
using System.Collections.Generic;

/// <summary>
/// The rules of procedural weather, with no scene state: whether the active weather holds another seventh,
/// how the weight of weather that has not been seen for a while grows, which weather can be picked and which
/// one a roll picks. <see cref="CelestialWeatherSystemLogic"/> owns the state (active weather, counters,
/// cooldowns) and supplies the random rolls, so every rule here is deterministic and tested in
/// <c>CoreSystemsTests</c>.
/// </summary>
public static class WeatherRules
{
    public enum Retention
    {
        /// <summary>The roll kept the active weather for another seventh.</summary>
        Kept,
        /// <summary>The roll failed: pick new weather.</summary>
        Lost,
        /// <summary>Kept too long (retention limit reached or chance decayed to 0): pick new weather and put the old one on cooldown.</summary>
        Forced
    }

    /// <summary>Why a weather in the pool is not a candidate for the next pick.</summary>
    public enum Candidacy { Candidate, NoWeight, Active, OnCooldown, ConditionsNotMet }

    /// <summary>Chance (0-100) the active weather stays: its retention minus <paramref name="decayPerRetention"/> per seventh it already stayed.</summary>
    public static float RetentionChance(float baseRetention, int consecutiveRetentions, float decayPerRetention)
    {
        return Math.Max(0f, baseRetention - consecutiveRetentions * decayPerRetention);
    }

    /// <summary>
    /// Forced when <paramref name="maxConsecutiveRetentions"/> (0 = no limit) is reached or the chance is 0;
    /// otherwise a roll (0-100) at or below the chance keeps the weather.
    /// </summary>
    public static Retention CheckRetention(float chance, int consecutiveRetentions, int maxConsecutiveRetentions, float roll)
    {
        bool limitReached = maxConsecutiveRetentions > 0 && consecutiveRetentions >= maxConsecutiveRetentions;
        if (limitReached || chance <= 0f) return Retention.Forced;
        return roll <= chance ? Retention.Kept : Retention.Lost;
    }

    /// <summary>
    /// Guaranteed variation: once a weather has been absent for <paramref name="threshold"/> sevenths its weight grows by
    /// <paramref name="increasePerSeventh"/> each seventh (the first step at exactly the threshold), capped at
    /// <paramref name="maxMultiplier"/> × its base weight.
    /// </summary>
    public static float VariationWeight(float baseWeight, int seventhsSinceSeen, int threshold, float increasePerSeventh, float maxMultiplier)
    {
        if (baseWeight <= 0f) return 0f;
        int steps = seventhsSinceSeen - threshold + 1;
        if (steps <= 0) return baseWeight;
        return Math.Min(baseWeight + steps * increasePerSeventh, baseWeight * Math.Max(1f, maxMultiplier));
    }

    /// <summary>Whether a pool entry can be picked next. The active weather never replaces itself.</summary>
    public static Candidacy CheckCandidate(float triggerWeight, bool isActive, bool onCooldown, bool conditionsMet)
    {
        if (triggerWeight <= 0f) return Candidacy.NoWeight;
        if (isActive) return Candidacy.Active;
        if (onCooldown) return Candidacy.OnCooldown;
        return conditionsMet ? Candidacy.Candidate : Candidacy.ConditionsNotMet;
    }

    /// <summary>
    /// The index a roll in [0, 1) picks, each entry with probability weight / total. With no positive weight the
    /// roll picks uniformly. -1 for an empty list.
    /// </summary>
    public static int PickWeighted(IReadOnlyList<float> weights, float roll)
    {
        if (weights == null || weights.Count == 0) return -1;
        roll = Math.Min(Math.Max(roll, 0f), 0.999999f);
        float total = Total(weights);
        if (total <= 0f) return (int)(roll * weights.Count);

        float target = roll * total, cumulative = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += Math.Max(0f, weights[i]);
            if (target < cumulative) return i;
        }
        return weights.Count - 1;
    }

    /// <summary>Probability (0-100) that <see cref="PickWeighted"/> picks <paramref name="index"/>.</summary>
    public static float Chance(IReadOnlyList<float> weights, int index)
    {
        if (weights == null || index < 0 || index >= weights.Count) return 0f;
        float total = Total(weights);
        return total > 0f ? Math.Max(0f, weights[index]) / total * 100f : 100f / weights.Count;
    }

    /// <summary>
    /// A seventh's phase durations normalized to 100%: a shortfall is added to the shortest phase, an excess is
    /// trimmed from the longest (never below 0). Ties go to the earliest phase, so the sky never flickers between
    /// two answers. Returns a new array; <paramref name="durations"/> is not changed.
    /// </summary>
    public static float[] NormalizePhaseDurations(IReadOnlyList<float> durations)
    {
        var result = new float[durations.Count];
        float total = 0f;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = durations[i];
            total += durations[i];
        }
        if (result.Length == 0 || Math.Abs(total - 100f) < 0.01f) return result;

        int target = 0;
        for (int i = 1; i < result.Length; i++)
        {
            bool better = total < 100f ? result[i] < result[target] : result[i] > result[target];
            if (better) target = i;
        }
        result[target] = Math.Max(0f, result[target] + (100f - total));
        return result;
    }

    private static float Total(IReadOnlyList<float> weights)
    {
        float total = 0f;
        foreach (float weight in weights) total += Math.Max(0f, weight);
        return total;
    }
}
