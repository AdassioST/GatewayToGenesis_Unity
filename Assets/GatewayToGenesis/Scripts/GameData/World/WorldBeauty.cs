using System;
using System.Collections.Generic;

/// <summary>
/// Beauty: how fair (+1) or hideous (-1) a place is to live on, with no scene state (tested in
/// <c>WorldTerritoryTests</c>). Society adopts beautiful land first and works it more willingly
/// (<see cref="TerritoryRules.beautyPriority"/>, <see cref="TerritoryRules.beautyWork"/>), and it adds a little City
/// Development (<see cref="SettlementRules.beautyWeight"/>). A proposal: the vault has no beauty rule (Canon Gaps.md).
///
/// Read from the ground's own beauty (<see cref="TerrainSpec.beauty"/>) and what stands on it
/// (<see cref="FeatureSpec.beauty"/>), then the place: water in view, Sacred Sites, silver rivers and leyline lights,
/// cliffs and vistas, Coherence (a place in tune) against dissonance and danger (a place that feels wrong).
/// Recomputed with the civilization, since the magic moves with the Ages.
/// </summary>
public static class WorldBeauty
{
    public const float RiverView = 0.2f, LakeView = 0.25f, CoastView = 0.15f, SacredGround = 0.4f, SilverWater = 0.15f,
        LeylineLights = 0.1f, Vista = 0.25f, CoherenceShare = 0.5f, DissonanceShare = 0.8f, DangerShare = 0.5f;

    /// <summary>A word for a beauty value, from hideous to sublime.</summary>
    public static string Word(float beauty) =>
        beauty >= 0.6f ? "sublime" : beauty >= 0.3f ? "beautiful" : beauty >= 0.1f ? "pleasant" : beauty > -0.1f ? "plain" : beauty > -0.3f ? "bleak" : beauty > -0.6f ? "ugly" : "hideous";

    /// <summary>Every land cell's beauty (water keeps 0).</summary>
    public static void Refresh(WorldMap map, WorldGenSettings settings)
    {
        if (map == null || settings == null) return;
        foreach (var t in map.Tiles) t.beauty = t.water ? 0f : Evaluate(map, settings, t, null);
    }

    /// <summary>The reasons behind a cell's beauty (named points, strongest first), for the hover card and lens.</summary>
    public static List<(string reason, float points)> Explain(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        var reasons = new List<(string, float)>();
        if (map != null && settings != null && t != null && !t.water) Evaluate(map, settings, t, reasons);
        reasons.Sort((a, b) => Math.Abs(b.Item2).CompareTo(Math.Abs(a.Item2)));
        return reasons;
    }

    private static float Evaluate(WorldMap map, WorldGenSettings settings, WorldTile t, List<(string, float)> reasons)
    {
        float sum = 0f;
        void Add(string reason, float points)
        {
            if (Math.Abs(points) < 0.005f) return;
            reasons?.Add((reason, points));
            sum += points;
        }
        var terrain = settings.Terrain(t.terrain);
        if (terrain != null) Add(terrain.name, terrain.beauty);
        var feature = t.HasFeature ? settings.Feature(t.feature) : null;
        if (feature != null) Add(feature.name, feature.beauty);

        bool lakeNear = t.lake, riverNear = t.river, coast = false;
        for (int d = 0; d < 6; d++)
        {
            int nb = map.Neighbour(t.index, d);
            if (nb < 0) continue;
            var n = map[nb];
            lakeNear |= n.lake;
            riverNear |= n.river;
            coast |= n.water && !n.lake;
        }
        if (lakeNear) Add("A lake in view", LakeView);
        else if (riverNear) Add(t.river ? "Running water" : "A river in view", RiverView);
        if (coast) Add("The sea in view", CoastView);
        if (t.silver) Add("Silver water", SilverWater);
        if (t.sacred) Add("Sacred ground", SacredGround);
        if (t.junction >= 2 || t.leylines != 0) Add("Leyline lights", t.junction >= 2 ? LeylineLights * 1.5f : LeylineLights);
        // Escarpment is the height step to the neighbours (0.045 and up reads as an escarpment).
        if (t.escarpment > 0.03f) Add("Cliffs and vistas", Vista * Math.Min(1f, (t.escarpment - 0.03f) / 0.07f));
        Add(t.coherence >= 0.4f ? "A place in tune (Coherence)" : "A place out of tune (low Coherence)", CoherenceShare * (t.coherence - 0.4f));
        if (t.dissonance > 0.01f) Add("Dissonance", -DissonanceShare * t.dissonance);
        if (t.danger > 0.01f) Add("Danger", -DangerShare * t.danger);
        return Math.Max(-1f, Math.Min(1f, sum));
    }
}
