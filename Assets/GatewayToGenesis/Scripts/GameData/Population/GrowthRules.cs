using System;

/// <summary>Dimensioned population rules. A citizen is always one person; rendering never changes the count.</summary>
public static class GrowthRules
{
    public const int SeventhsPerYear = 21 * 3 * 4;

    public static float RationsPerDay(GrowthTuning t, float lossModifier = 1f) =>
        Math.Max(0f, t.kcalPerPersonDay) / Math.Max(1f, t.kcalPerFood)
        * (1f + Math.Max(0f, t.distributionLoss) * Clamp01(lossModifier));

    public static float FoodDemand(int people, float secondsPerSeventh, GrowthTuning t, float lossModifier = 1f) =>
        (float)(Math.Max(0, people) * (double)RationsPerDay(t, lossModifier) * Math.Max(0f, t.daysPerYear)
        / (SeventhsPerYear * Math.Max(0.01f, secondsPerSeventh)));

    /// <summary>
    /// The people who eat from the daily food: everyone beyond <see cref="GrowthTuning.provisionedPeople"/> (the
    /// founders were provisioned once, for good, at the gates). A gameplay rule for the founding band.
    /// </summary>
    public static int Eating(int people, GrowthTuning t) => Math.Max(0, people - Math.Max(0, t.provisionedPeople));

    /// <summary>
    /// People sustained by a net food supply excluding their own consumption and reserve withdrawals (the provisioned
    /// founders need none of it).
    /// </summary>
    public static int SupportedPeople(float supplyPerSecond, float secondsPerSeventh, GrowthTuning t, float lossModifier = 1f)
    {
        double ration = FoodDemand(1, secondsPerSeventh, t, lossModifier);
        long fed = ration <= 0d ? int.MaxValue : (long)Math.Max(0d, Math.Floor(supplyPerSecond / ration));
        return (int)Math.Min(int.MaxValue, fed + Math.Max(0, t.provisionedPeople));
    }

    /// <summary>Expected events for a fraction of a year. Rates are crude births or deaths per 1,000 per year.</summary>
    public static double VitalEvents(int people, float perThousand, double years) =>
        Math.Max(0, people) * Math.Max(0f, perThousand) / 1000d * Math.Max(0d, years);

    public static int Arrivals(int waiting, int freeHomes, bool allowVagrants, float food, float rations, int limit)
    {
        if (waiting <= 0 || limit <= 0 || rations <= 0f || float.IsNaN(food)) return 0;
        int fed = (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Floor((double)food / rations)));
        return Math.Min(Math.Min(waiting, limit), Math.Min(fed, allowVagrants ? int.MaxValue : Math.Max(0, freeHomes)));
    }

    /// <summary>Fractional people persist across saves; no forced minimum birth or death per tick.</summary>
    public static int Whole(double amount, ref double carry)
    {
        double total = Math.Max(0d, amount) + Math.Max(0d, carry);
        if (double.IsNaN(total) || double.IsInfinity(total)) { carry = 0d; return 0; }
        int whole = (int)Math.Min(int.MaxValue, Math.Floor(total + 1e-10d));
        carry = Math.Max(0d, total - whole);
        return whole;
    }

    /// <summary>
    /// How much frontier pacing is left: 1 at or below <see cref="GrowthTuning.frontierFullBelow"/> people, 0 from
    /// <see cref="GrowthTuning.realismFrom"/>, fading on a log scale between (each doubling takes off the same share).
    /// A gameplay rule: a founding band grows at a playable pace, a village follows the historical rates.
    /// </summary>
    public static float Frontier(int people, GrowthTuning t)
    {
        int full = Math.Max(1, t.frontierFullBelow), real = Math.Max(full + 1, t.realismFrom);
        if (people <= full) return 1f;
        if (people >= real) return 0f;
        return (float)(1d - Math.Log(people / (double)full) / Math.Log(real / (double)full));
    }

    /// <summary>Birth-rate multiplier: <see cref="GrowthTuning.frontierBirthPace"/> for a founding band, 1 once it is a village.</summary>
    public static float BirthPace(int people, GrowthTuning t) => 1f + (Math.Max(1f, t.frontierBirthPace) - 1f) * Frontier(people, t);

    /// <summary>
    /// How strongly the settlement draws caravans of survivors, 0-1: frontier pacing times its stores (days of rations
    /// for everyone over <see cref="GrowthTuning.caravanReserveDays"/>). No one walks toward an empty granary.
    /// </summary>
    public static float CaravanPull(int people, float storedFood, float rationsPerDay, GrowthTuning t)
    {
        if (rationsPerDay <= 0f || float.IsNaN(storedFood)) return 0f;
        double days = Math.Max(0f, storedFood) / (Math.Max(1, people) * (double)rationsPerDay);
        return Frontier(people, t) * (float)Math.Min(1d, days / Math.Max(1f, t.caravanReserveDays));
    }

    /// <summary>Caravans due this Seventh at this pull, carried as a fraction across Sevenths and saves.</summary>
    public static double CaravanProgress(float pull, GrowthTuning t) => Math.Max(0f, t.caravansPerCycle) * Clamp01(pull) / SeventhsPerYear;

    /// <summary>The people in the <paramref name="index"/>th caravan: within [min, max], the same for the same caravan.</summary>
    public static int CaravanSize(int index, GrowthTuning t)
    {
        int min = Math.Max(1, Math.Min(t.caravanMin, t.caravanMax)), max = Math.Max(min, Math.Max(t.caravanMin, t.caravanMax));
        uint h = (uint)index * 2654435761u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return min + (int)(h % (uint)(max - min + 1));
    }

    public static int PercentOf(int people, float percent) => (int)Math.Min(Math.Max(0, people),
        Math.Floor(Math.Max(0, people) * Math.Min(100d, Math.Max(0d, percent)) / 100d));

    private static float Clamp01(float x) => Math.Max(0f, Math.Min(1f, x));
}
