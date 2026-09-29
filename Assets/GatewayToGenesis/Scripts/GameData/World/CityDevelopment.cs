using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One of the things a place's City Development is made of (each lens of the world view shows one).</summary>
public enum DevelopmentTerm { LandFertility, Freshwater, Coherence, MagicalFertility, Leylines, Trade, Grandfields, Sacred, Danger, Dissonance, Beauty, Resources, Outskirts }

/// <summary>What City Development a place can reach and why: named terms in points, and the Coherence ceiling.</summary>
public class DevelopmentBreakdown
{
    public readonly List<(DevelopmentTerm term, float points)> terms = new List<(DevelopmentTerm, float)>();
    /// <summary>Sum of the terms before the ceiling.</summary>
    public float raw;
    /// <summary>The most this place can develop: high Coherence is needed to develop fully.</summary>
    public float ceiling;

    /// <summary>The City Development a settlement here grows toward (0 to the ceiling).</summary>
    public float Potential => Math.Max(0f, Math.Min(raw, ceiling));

    public float Points(DevelopmentTerm term)
    {
        float sum = 0f;
        foreach (var t in terms) if (t.term == term) sum += t.points;
        return sum;
    }
}

/// <summary>
/// City Development (Arcanoria.md, Settlements and Magical Pathways): how far a settlement can grow on its ground.
/// The vault ties it to the map: high Coherence allows "easier City Development" and is "necessary for Major
/// Settlements and Capitals to fully develop"; roads "tend to increase the adjacent City Development"; a Trade Nexus
/// "creates value and City Development"; Spellweaving.md: "the geology of a region determines stability and City
/// Development". The weights (<see cref="SettlementRules"/>) and the land/water/danger terms are proposals.
///
/// A place reads the cells it would work (within <see cref="SettlementRules.workRadius"/>): their land fertility,
/// Coherence and magical fertility on average, freshwater at hand, leylines and their junctions, roads, the Trade
/// Nexus and whether the road network reaches the Capital, grandfields, Sacred Sites, and the danger and dissonance
/// that weigh it down. The ceiling is <c>ceilingBase + (100 - ceilingBase) * Coherence</c> of the settlement's own cell.
/// </summary>
public static class CityDevelopment
{
    public static readonly DevelopmentTerm[] Terms = (DevelopmentTerm[])Enum.GetValues(typeof(DevelopmentTerm));

    /// <summary>
    /// Coherence lent to a cell on top of its ground (an owned overlay: the culture's later choruses, T08), or null. The
    /// ground's own <see cref="WorldTile.coherence"/> never changes; every development read goes through <see cref="CoherenceOf"/>.
    /// </summary>
    public static Func<WorldTile, float> CoherenceOverlay;

    /// <summary>A cell's Coherence as development reads it: its ground's, plus any overlay, 0-1.</summary>
    public static float CoherenceOf(WorldTile t) => t == null ? 0f : Math.Max(0f, Math.Min(1f, t.coherence + (CoherenceOverlay?.Invoke(t) ?? 0f)));

    public static string Name(DevelopmentTerm term)
    {
        switch (term)
        {
            case DevelopmentTerm.LandFertility: return "Land fertility";
            case DevelopmentTerm.Freshwater: return "Freshwater";
            case DevelopmentTerm.Coherence: return "Coherence";
            case DevelopmentTerm.MagicalFertility: return "Magical fertility";
            case DevelopmentTerm.Leylines: return "Leylines";
            case DevelopmentTerm.Trade: return "Trade";
            case DevelopmentTerm.Grandfields: return "Grandfields";
            case DevelopmentTerm.Sacred: return "Sacred ground";
            case DevelopmentTerm.Danger: return "Danger";
            case DevelopmentTerm.Beauty: return "Beauty";
            case DevelopmentTerm.Resources: return "Resources";
            case DevelopmentTerm.Outskirts: return "Outskirts";
            default: return "Dissonance";
        }
    }

    /// <summary>
    /// The breakdown for a settlement on <paramref name="cell"/>. <paramref name="networked"/>: its roads reach the
    /// Capital (a connected Trade Nexus is worth three isolated cities).
    /// </summary>
    public static DevelopmentBreakdown Evaluate(WorldMap map, SettlementRules rules, int cell, bool networked = false)
    {
        var result = new DevelopmentBreakdown();
        if (map == null || rules == null || cell < 0 || cell >= map.Count) return result;
        Gather(map, rules, HexCoord.Spiral(HexCoord.Zero, Math.Max(0, rules.workRadius)), cell, networked, result, new HashSet<int>());
        result.ceiling = Ceiling(rules, map[cell]);
        return result;
    }

    // The one sum behind Evaluate and PotentialField: terms go into result (when given) and the raw total is returned.
    private static float Gather(WorldMap map, SettlementRules rules, List<HexCoord> offsets, int cell, bool networked, DevelopmentBreakdown result, HashSet<int> fields)
    {
        var t = map[cell];
        int landCount = 0;
        float sumLand = 0f, sumCoherence = 0f, sumMagic = 0f, sumDissonance = 0f, sumBeauty = 0f, maxDanger = 0f, maxInfluence = 0f;
        bool anyWater = false, anyLeyline = false, sacred = false;
        int junction = 0;
        fields.Clear();
        // Identified resource sites worked (each once, however many of its cells are in reach).
        float resources = 0f;
        int lastSite = -1;
        foreach (var offset in offsets)
        {
            var c = map.Get(t.coord + offset);
            if (c == null) continue;
            if (c.resourceSite >= 0 && c.resourceSite != lastSite && WorldResources.Identified(map, WorldResources.SiteAt(map, c)) && fields.Add(-2 - c.resourceSite))
            {
                resources += map.ResourceSites[c.resourceSite].landValue;
                lastSite = c.resourceSite;
            }
            maxDanger = Math.Max(maxDanger, c.danger);
            anyWater |= c.river || c.lake;
            anyLeyline |= c.leylines != 0;
            junction = Math.Max(junction, c.junction);
            sacred |= c.sacred;
            if (c.grandfield >= 0) fields.Add(c.grandfield);
            if (c.water || c.impassable) continue;
            landCount++;
            sumLand += c.landFertility;
            sumCoherence += CoherenceOf(c);
            sumMagic += c.magicFertility;
            sumDissonance += c.dissonance;
            sumBeauty += c.beauty;
            maxInfluence = Math.Max(maxInfluence, c.leylineInfluence);
        }
        if (landCount == 0)
        {
            landCount = 1;
            sumLand = t.landFertility;
            sumCoherence = CoherenceOf(t);
            sumMagic = t.magicFertility;
            sumDissonance = t.dissonance;
            sumBeauty = t.beauty;
            maxInfluence = t.leylineInfluence;
        }
        bool freshAdjacent = t.river, nexus = t.nexus != null;
        // A rebuilt road alongside counts in full, a restored Old World stretch only in part (OldWorldRules.restoredWorth).
        float restoredWorth = Math.Max(0f, Math.Min(1f, rules.loss?.oldWorld?.restoredWorth ?? 0.5f));
        float RoadWorth(WorldTile x) => !x.road ? 0f : x.restoredRoad ? restoredWorth : 1f;
        float road = RoadWorth(t);
        for (int d = 0; d < 6; d++)
        {
            int nb = map.Neighbour(cell, d);
            if (nb < 0) continue;
            var n = map[nb];
            freshAdjacent |= n.river || n.lake;
            road = Math.Max(road, RoadWorth(n));
            nexus |= n.nexus != null;
        }
        float fresh = freshAdjacent ? 1f : anyWater ? 0.5f : 0f;
        float leyline = anyLeyline ? 1f : maxInfluence;

        float raw = 0f;
        void Add(DevelopmentTerm term, float points)
        {
            if (Math.Abs(points) < 0.005f) return;
            result?.terms.Add((term, points));
            raw += points;
        }
        Add(DevelopmentTerm.LandFertility, rules.landWeight * sumLand / landCount);
        Add(DevelopmentTerm.Freshwater, rules.freshwaterWeight * fresh);
        Add(DevelopmentTerm.Coherence, rules.coherenceWeight * sumCoherence / landCount);
        Add(DevelopmentTerm.MagicalFertility, rules.magicWeight * sumMagic / landCount);
        Add(DevelopmentTerm.Leylines, rules.leylineWeight * leyline + (junction >= 3 ? rules.basinWeight : junction == 2 ? rules.convergenceWeight : 0f));
        Add(DevelopmentTerm.Trade, road * rules.roadWeight + (networked ? rules.networkWeight : 0f) + (nexus ? rules.nexusWeight * (networked ? 1f : 0.5f) : 0f));
        Add(DevelopmentTerm.Grandfields, rules.grandfieldWeight * Math.Min(2, fields.Count(f => f >= 0)));
        Add(DevelopmentTerm.Sacred, sacred ? rules.sacredWeight : 0f);
        Add(DevelopmentTerm.Danger, -rules.dangerWeight * maxDanger);
        Add(DevelopmentTerm.Dissonance, -rules.dissonanceWeight * sumDissonance / landCount);
        Add(DevelopmentTerm.Beauty, rules.beautyWeight * sumBeauty / landCount);
        float cap = Math.Max(0f, rules.resourceCap);
        Add(DevelopmentTerm.Resources, Math.Max(-cap, Math.Min(cap, rules.resourceWeight * resources)));
        if (result != null) result.raw = raw;
        return raw;
    }

    /// <summary>The most a settlement on this cell can develop (Coherence decides how far).</summary>
    public static float Ceiling(SettlementRules rules, WorldTile t) =>
        rules.ceilingBase + (100f - rules.ceilingBase) * CoherenceOf(t);

    /// <summary>
    /// A cell's own share of one term, 0-1 (what a lens paints: the ground under the cursor, not its neighbourhood).
    /// Danger and dissonance read as how much they weigh down.
    /// </summary>
    public static float TileScore(WorldMap map, WorldTile t, DevelopmentTerm term)
    {
        if (t == null) return 0f;
        switch (term)
        {
            case DevelopmentTerm.LandFertility: return t.landFertility;
            case DevelopmentTerm.Freshwater: return t.river || t.lake ? 1f : map != null && map.NeighboursOf(t).Any(n => n.river || n.lake) ? 0.6f : 0f;
            case DevelopmentTerm.Coherence: return CoherenceOf(t);
            case DevelopmentTerm.MagicalFertility: return t.magicFertility;
            case DevelopmentTerm.Leylines: return t.junction >= 3 ? 1f : t.junction == 2 ? 0.85f : t.leylines != 0 ? 0.7f : 0.6f * t.leylineInfluence;
            case DevelopmentTerm.Trade: return t.nexus != null ? 1f : t.tradeNode ? 0.8f : t.road ? (t.restoredRoad ? 0.3f : 0.6f) : 0f;
            case DevelopmentTerm.Grandfields: return t.grandfield >= 0 ? 0.35f + 0.65f * t.grandfieldDensity : 0f;
            case DevelopmentTerm.Sacred: return t.sacred ? 1f : 0f;
            case DevelopmentTerm.Danger: return t.danger;
            case DevelopmentTerm.Beauty: return (t.beauty + 1f) * 0.5f;
            case DevelopmentTerm.Resources: return Math.Max(0f, Math.Min(1f, 0.5f + WorldResources.LandValue(map, t) / 10f));
            case DevelopmentTerm.Outskirts: return t.settlement >= 0 && t.settlement < map?.Settlements.Count && map.Settlements[t.settlement].kind == SettlementKind.Tributary ? 1f : 0f;
            default: return t.dissonance;
        }
    }

    /// <summary>
    /// The potential of every cell at once (the Settle lens), 0 on water and impassable ground. The same sums as
    /// <see cref="Evaluate"/>, gathered from per-cell arrays so the whole map costs one pass over its neighbourhoods.
    /// </summary>
    public static float[] PotentialField(WorldMap map, SettlementRules rules)
    {
        int n = map.Count;
        var field = new float[n];
        var offsets = HexCoord.Spiral(HexCoord.Zero, Math.Max(0, rules.workRadius));
        var fields = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            if (t.water || t.impassable) continue;
            float raw = Gather(map, rules, offsets, i, false, null, fields);
            field[i] = Math.Max(0f, Math.Min(raw, Ceiling(rules, t)));
        }
        return field;
    }

    /// <summary>
    /// The binding a Major Settlement here would most naturally attune to (one of the seven of The Principles of Magic),
    /// read from its ground: Flux where water runs, Crystal on high stone, Resonance on open wind-swept or coherent
    /// ground, Luminance where magic gathers, Strand among remnants of the past, Cindergale on dry hot land, Void where
    /// dissonance lingers. A proposal: the vault ties Major Settlements to a binding but not a binding to terrain.
    /// </summary>
    public static string SuggestBinding(WorldMap map, int cell, int radius = 2)
    {
        if (map == null || cell < 0 || cell >= map.Count) return "Resonance";
        var scores = new Dictionary<string, float>
        {
            { "Flux", 0f }, { "Crystal", 0f }, { "Resonance", 0f }, { "Luminance", 0f }, { "Strand", 0f }, { "Cindergale", 0f }, { "Void", 0f },
        };
        foreach (var coord in HexCoord.Spiral(map[cell].coord, radius))
        {
            var t = map.Get(coord);
            if (t == null) continue;
            if (t.water || t.river) scores["Flux"] += t.silver ? 1.5f : 1f;
            if (!t.water) scores["Crystal"] += Math.Max(0f, t.elevation - 0.55f) * 4f + (t.impassable ? 0.5f : 0f);
            scores["Resonance"] += t.coherence * 0.6f + (t.leylines != 0 ? 0.4f : 0f);
            scores["Luminance"] += t.magicFertility + (t.junction >= 2 ? 0.8f : 0f) + (t.sacred ? 1.5f : 0f);
            if (t.HasFeature || t.handmadeTile != null) scores["Strand"] += 0.8f;
            if (!t.water) scores["Cindergale"] += Math.Max(0f, 0.4f - t.moisture) * 2f + Math.Max(0f, t.temperature - 0.6f) * 2f;
            scores["Void"] += t.dissonance * 3f + t.danger;
        }
        return scores.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
    }

    /// <summary>The seven bindings in a fixed order (cycled by the player on a Major Settlement).</summary>
    public static readonly string[] Bindings = { "Cindergale", "Crystal", "Flux", "Luminance", "Resonance", "Strand", "Void" };
}
