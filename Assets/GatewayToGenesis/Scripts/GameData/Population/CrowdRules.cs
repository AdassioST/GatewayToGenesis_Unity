using System;

/// <summary>
/// How many of the people the settlement shows, with no scene state (tested in <c>CrowdRulesTests</c>). A small people is
/// shown exactly, one villager per person; past <c>nearCap</c> the rest are the crowd in the distance, smaller figures
/// whose number grows by the same step for every doubling of the people (so a city of 300,000 fills the valley with a
/// few hundred figures, never 300,000 instances).
/// </summary>
public static class CrowdRules
{
    /// <summary>Villagers walking close: one per person, up to the cap.</summary>
    public static int Near(int people, int nearCap) => Math.Max(0, Math.Min(people, nearCap));

    /// <summary>
    /// Figures in the distance for the people beyond the near cap: at least one as soon as anyone is left out, then
    /// <c>farCap × ln(1 + extra / unit) / ln(1 + (fullAt − nearCap) / unit)</c>, full at <paramref name="fullAt"/> people.
    /// </summary>
    public static int Far(int people, int nearCap, int farCap, float unit, int fullAt)
    {
        int extra = people - Math.Max(0, nearCap);
        if (extra <= 0 || farCap <= 0) return 0;
        double u = Math.Max(1f, unit);
        double full = Math.Log(1d + Math.Max(1, fullAt - nearCap) / u);
        double share = Math.Min(1d, Math.Log(1d + extra / u) / full);
        return Math.Min(extra, Math.Min(farCap, Math.Max(1, (int)Math.Round(farCap * share))));
    }

    /// <summary>The part of <paramref name="shown"/> figures that stands for <paramref name="part"/> of <paramref name="whole"/> (the vagrants among the people).</summary>
    public static int Share(int shown, int part, int whole) =>
        shown <= 0 || whole <= 0 || part <= 0 ? 0 : Math.Min(shown, (int)Math.Round(shown * (double)Math.Min(part, whole) / whole));
}
