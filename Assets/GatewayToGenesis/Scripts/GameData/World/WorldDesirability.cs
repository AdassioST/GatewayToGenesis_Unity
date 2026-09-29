using System;
using System.Collections.Generic;

/// <summary>
/// Where people want to live, 0-1, with no scene state (tested in <c>WorldTributaryTests</c>). A cell is desirable for
/// its beauty, Coherence, magical fertility, land fertility and leylines (<see cref="DesirabilityRules"/> weights),
/// blended with the same reading of the six hexes around it (<see cref="DesirabilityRules.surroundings"/>), and less
/// so for danger and dissonance. Settlements grow faster on desirable ground (<see cref="DesirabilityRules.GrowthFactor"/>)
/// and outskirt tributaries develop toward it (<see cref="WorldTributaries.Target"/>).
///
/// Not <see cref="WorldTile.Desirability"/>: that is the generator's raw reading of the ground (enclave placement).
/// </summary>
public static class WorldDesirability
{
    public static DesirabilityRules RulesOf(WorldMap map) => map?.settlementRules?.desirability ?? Default;

    private static readonly DesirabilityRules Default = new DesirabilityRules();

    /// <summary>A cell's own appeal, 0-1 (no neighbours, no penalties); 0 on water and impassable ground.</summary>
    public static float Own(DesirabilityRules rules, WorldTile t)
    {
        if (t == null || t.water || t.impassable) return 0f;
        float sum = rules.beautyWeight + rules.coherenceWeight + rules.magicWeight + rules.landWeight + rules.leylineWeight;
        if (sum <= 0f) return 0f;
        float v = rules.beautyWeight * (t.beauty + 1f) * 0.5f + rules.coherenceWeight * t.coherence + rules.magicWeight * t.magicFertility
            + rules.landWeight * t.landFertility + rules.leylineWeight * Leyline(t);
        return Clamp01(v / sum);
    }

    /// <summary>Leylines at a cell, 0-1: a Basin, a Convergence, a line, or its nearness to one.</summary>
    public static float Leyline(WorldTile t) =>
        t.junction >= 3 ? 1f : t.junction == 2 ? 0.85f : t.leylines != 0 ? 0.7f : 0.6f * t.leylineInfluence;

    /// <summary>Desirability of <paramref name="t"/>, 0-1 (0 on water and impassable ground).</summary>
    public static float Of(WorldMap map, WorldTile t) => Of(map, RulesOf(map), t);

    public static float Of(WorldMap map, DesirabilityRules rules, WorldTile t)
    {
        if (map == null || t == null || t.water || t.impassable) return 0f;
        float own = Own(rules, t), around = Around(map, rules, t, own);
        float s = Clamp01(rules.surroundings);
        return Clamp01((1f - s) * own + s * around - rules.dangerPenalty * t.danger - rules.dissonancePenalty * t.dissonance);
    }

    // The average appeal of the land around a cell (its own when none of it is land).
    private static float Around(WorldMap map, DesirabilityRules rules, WorldTile t, float own)
    {
        float sum = 0f;
        int count = 0;
        for (int d = 0; d < 6; d++)
        {
            int nb = map.Neighbour(t.index, d);
            if (nb < 0) continue;
            var n = map[nb];
            if (n.water || n.impassable) continue;
            sum += Own(rules, n);
            count++;
        }
        return count > 0 ? sum / count : own;
    }

    /// <summary>
    /// What makes up a cell's desirability, as signed shares of it (they sum to the value before it is clamped): each
    /// quality of the cell itself, the land around it, danger and dissonance.
    /// </summary>
    public static List<(string what, float share)> Breakdown(WorldMap map, WorldTile t)
    {
        var rules = RulesOf(map);
        var result = new List<(string, float)>();
        if (map == null || t == null || t.water || t.impassable) return result;
        float sum = rules.beautyWeight + rules.coherenceWeight + rules.magicWeight + rules.landWeight + rules.leylineWeight;
        float s = Clamp01(rules.surroundings), k = sum > 0f ? (1f - s) / sum : 0f;
        void Add(string what, float share)
        {
            if (Math.Abs(share) >= 0.005f) result.Add((what, share));
        }
        Add("beauty", k * rules.beautyWeight * (t.beauty + 1f) * 0.5f);
        Add("Coherence", k * rules.coherenceWeight * t.coherence);
        Add("magical fertility", k * rules.magicWeight * t.magicFertility);
        Add("land fertility", k * rules.landWeight * t.landFertility);
        Add("leylines", k * rules.leylineWeight * Leyline(t));
        Add("the land around", s * Around(map, rules, t, Own(rules, t)));
        Add("danger", -rules.dangerPenalty * t.danger);
        Add("dissonance", -rules.dissonancePenalty * t.dissonance);
        return result;
    }

    /// <summary>A word for a desirability, for cards and the lens.</summary>
    public static string Word(float v) => v >= 0.75f ? "coveted" : v >= 0.55f ? "desirable" : v >= 0.35f ? "fair" : v >= 0.2f ? "plain" : "shunned";

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
