using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The seven Greats (Great Sovereign, Great Vanguard, Great Architect...) as standings a legend earns, never a class
/// it is born with (owner's rule, Sept 28 2026). Every legend begins as an Unattuned Legend; what it does grows its
/// affinity toward each Great, and affinity becomes stars: 1, 2 or 3 (the best). A legend can be a 3★ Great Sovereign,
/// a 2★ Great Vanguard and a 1★ Great Architect at once.
///
/// Affinity is read from the legend's Lyrical Fragments (they are never spent): each Great is tied to a binding
/// (<see cref="MagicBindings.AffinityOf"/>, Stellar Legacy Score.md) and each binding to a kind of fragment
/// (<see cref="LyricalFragments.Binding"/>), so Defiance makes a Vanguard, Meaning a Sovereign, Vision an Architect,
/// Catharsis a Concertist, Lucidity a Seer, Acceptance a Justiciar and Rebirth a Chronicler. Whatever pays fragments
/// (the council, expeditions, ballads, battles) pays affinity. <see cref="LegendData.legendClass"/> is kept as the
/// legend's calling: the Great its Soul Leitmotif leans toward, not a title. No scene state; tested in
/// <c>LegendGreatsTests</c>. The star thresholds are proposals (<see cref="FragmentTuning.greatStars"/>).
/// </summary>
public static class LegendGreats
{
    public const int MaxStars = 3;
    public const string UnattunedTitle = "Unattuned Legend";

    public static readonly LegendClass[] All = (LegendClass[])Enum.GetValues(typeof(LegendClass));

    private static readonly int[] DefaultThresholds = { 15, 40, 90 };

    /// <summary>The kind of fragment that grows a Great (Defiance for the Great Vanguard).</summary>
    public static FragmentKind KindOf(LegendClass great) => LyricalFragments.OfBinding(MagicBindings.AffinityOf(great)) ?? FragmentKind.Meaning;

    /// <summary>The Great a kind of fragment grows.</summary>
    public static LegendClass GreatOf(FragmentKind kind) => All.First(g => KindOf(g) == kind);

    /// <summary>"Great Vanguard".</summary>
    public static string Name(LegendClass great) => LegendClasses.Title(great);

    /// <summary>A legend's affinity toward a Great: its fragments of that Great's kind.</summary>
    public static int Affinity(IReadOnlyDictionary<string, int> purse, LegendClass great) => LyricalFragments.Count(purse, KindOf(great));

    private static int[] Thresholds(FragmentTuning tuning) =>
        tuning?.greatStars != null && tuning.greatStars.Length >= MaxStars ? tuning.greatStars : DefaultThresholds;

    /// <summary>Stars (0-3) for an affinity.</summary>
    public static int Stars(int affinity, FragmentTuning tuning)
    {
        var t = Thresholds(tuning);
        int stars = 0;
        for (int i = 0; i < MaxStars; i++) if (affinity >= t[i]) stars = i + 1;
        return stars;
    }

    public static int Stars(IReadOnlyDictionary<string, int> purse, LegendClass great, FragmentTuning tuning) => Stars(Affinity(purse, great), tuning);

    /// <summary>Affinity the next star needs, or -1 at three stars.</summary>
    public static int NextStarAt(int stars, FragmentTuning tuning) => stars >= 0 && stars < MaxStars ? Thresholds(tuning)[stars] : -1;

    /// <summary>A legend's stars in every Great it has at least one star in, the most first.</summary>
    public static Dictionary<LegendClass, int> Standing(IReadOnlyDictionary<string, int> purse, FragmentTuning tuning)
    {
        var standing = new Dictionary<LegendClass, int>();
        foreach (var g in All)
        {
            int stars = Stars(purse, g, tuning);
            if (stars > 0) standing[g] = stars;
        }
        return standing;
    }

    /// <summary>Whether a legend holds at least <paramref name="stars"/> in one of <paramref name="greats"/> (0: anyone does, an Unattuned Legend too).</summary>
    public static bool Meets(IReadOnlyDictionary<string, int> purse, IEnumerable<LegendClass> greats, int stars, FragmentTuning tuning) =>
        stars <= 0 || (greats ?? Enumerable.Empty<LegendClass>()).Any(g => Stars(purse, g, tuning) >= stars);

    public static bool Meets(IReadOnlyDictionary<LegendClass, int> standing, IEnumerable<LegendClass> greats, int stars) =>
        stars <= 0 || (greats ?? Enumerable.Empty<LegendClass>()).Any(g => standing != null && standing.TryGetValue(g, out int s) && s >= stars);

    /// <summary>"★★☆" for 2 of 3.</summary>
    public static string StarText(int stars) => new string('★', Math.Max(0, Math.Min(MaxStars, stars))) + new string('☆', MaxStars - Math.Max(0, Math.Min(MaxStars, stars)));

    /// <summary>"3★ Great Sovereign".</summary>
    public static string Title(LegendClass great, int stars) => $"{stars}★ {Name(great)}";

    /// <summary>"3★ Great Sovereign · 2★ Great Vanguard · 1★ Great Architect", or "Unattuned Legend".</summary>
    public static string Title(IReadOnlyDictionary<LegendClass, int> standing)
    {
        if (standing == null || standing.Count == 0 || standing.Values.All(s => s <= 0)) return UnattunedTitle;
        return string.Join(" · ", standing.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ThenBy(p => (int)p.Key).Select(p => Title(p.Key, p.Value)));
    }

    public static string Title(IReadOnlyDictionary<string, int> purse, FragmentTuning tuning) => Title(Standing(purse, tuning));

    /// <summary>The Great it stands highest in (ties: the order of the enum), or null for an Unattuned Legend.</summary>
    public static LegendClass? Highest(IReadOnlyDictionary<LegendClass, int> standing) =>
        standing == null || standing.Count == 0 ? (LegendClass?)null : standing.OrderByDescending(p => p.Value).ThenBy(p => (int)p.Key).First().Key;

    /// <summary>What a seat asks: "Any legend", "1★ Great Vanguard", "2★ Great Sovereign or Great Justiciar".</summary>
    public static string Requirement(IEnumerable<LegendClass> greats, int stars)
    {
        var list = (greats ?? Enumerable.Empty<LegendClass>()).Distinct().ToList();
        if (stars <= 0) return "Any legend, Unattuned Legends too";
        if (list.Count == 0 || list.Count >= All.Length) return $"{stars}★ in any Great";
        return $"{stars}★ {string.Join(" or ", list.Select(Name))}";
    }
}
