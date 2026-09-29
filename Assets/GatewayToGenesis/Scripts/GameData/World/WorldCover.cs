using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A patch of cover on the map (a Deep Forest, a Mistfen): placed with the world, never saved.</summary>
public class CoverPatch
{
    public int index;
    public string spec;
    public string name;
    public int center;
    public readonly List<int> cells = new List<int>();
}

/// <summary>
/// Cover (<see cref="CoverSpec"/>), with no scene state (tested in <c>WorldCoverTests</c>): multi-cell patches laid over
/// the ground once with the world (the same seed and catalog give the same patches, so nothing is saved).
///
/// - Exploring: a concealed cell blocks the view (<see cref="Visible"/>: a party sees the edge of a forest, not what lies
///   beyond it, unless it looks from high ground) and hides what stands inside it until the cell is explored (resource
///   sites, features). From inside, sight is narrowed (<see cref="WorldTile.coverSight"/>); travel
///   (<see cref="MicroGrid.CellFactor"/>) and surveys (<see cref="UnitAbilities.Duration"/>) are slower; some cover wears
///   parties down (<see cref="UnitSurroundings.hardship"/>), and survey finds are likelier (<see cref="WorldTile.coverFinds"/>).
/// - Holding it: its cells yield (<see cref="YieldsOf"/>), add forage (<see cref="ForageOf"/>) and beauty, and are harder to
///   administer (<see cref="CoverSpec.governance"/>).
/// </summary>
public static class WorldCover
{
    /// <summary>Cells kept clear of cover around the capital (the survivors' own ground).</summary>
    public const int CapitalClearance = 3;

    // ===== PLACEMENT =====

    /// <summary>Lay every kind of cover over the world (once; a map that has cover keeps it).</summary>
    public static void Place(WorldMap map, WorldGenSettings settings)
    {
        if (map == null || settings?.covers == null || map.Covers.Count > 0) return;
        foreach (var spec in settings.covers.Where(s => s != null && !string.IsNullOrEmpty(s.id) && s.count > 0))
        {
            var rng = new Random(WorldNoise.Stream(map.seed, "cover:" + spec.id));
            int stream = WorldNoise.Stream(map.seed, "cover-centre:" + spec.id);
            var centres = map.Tiles.Where(t => map.StepsFromCapital(t.coord) >= spec.minDistance && Fits(map, settings, t, spec))
                .OrderBy(t => WorldNoise.Hash01(stream, t.index, 0)).ToList();
            int placed = 0, least = Math.Max(1, spec.minSize), most = Math.Max(least, spec.maxSize);
            foreach (var centre in centres)
            {
                if (placed >= spec.count) break;
                if (centre.cover != null) continue;
                if (map.Covers.Any(p => p.spec == spec.id && HexCoord.Distance(map[p.center].coord, centre.coord) < Math.Max(1, spec.spacing))) continue;
                int size = rng.Next(least, most + 1);
                var patch = new CoverPatch { index = map.Covers.Count, spec = spec.id, name = spec.name, center = centre.index };
                patch.cells.Add(centre.index);
                var taken = new HashSet<int> { centre.index };
                // It grows in a blob: each new cell is any fitting neighbour of the patch so far.
                var edge = new List<int>();
                void Around(int cell)
                {
                    foreach (var n in map.NeighboursOf(map[cell]))
                        if (!taken.Contains(n.index) && !edge.Contains(n.index) && Fits(map, settings, n, spec)) edge.Add(n.index);
                }
                Around(centre.index);
                while (patch.cells.Count < size && edge.Count > 0)
                {
                    int pick = edge[rng.Next(edge.Count)];
                    edge.Remove(pick);
                    taken.Add(pick);
                    patch.cells.Add(pick);
                    Around(pick);
                }
                foreach (int cell in patch.cells) Apply(map[cell], spec, patch.index);
                map.Covers.Add(patch);
                placed++;
            }
            if (placed < spec.count) map.Report.notes.Add($"Cover '{spec.id}': {placed} of {spec.count} placed (not enough fitting ground).");
        }
    }

    /// <summary>Whether cover of <paramref name="spec"/> may lie over <paramref name="t"/>: open, passable land on its ground, clear of the capital, settlements and enclaves.</summary>
    public static bool Fits(WorldMap map, WorldGenSettings settings, WorldTile t, CoverSpec spec)
    {
        if (t == null || spec == null || t.water || t.impassable || t.cover != null) return false;
        if (t.settlement >= 0 || t.enclave >= 0 || map.StepsFromCapital(t.coord) < CapitalClearance) return false;
        var terrain = settings.Terrain(t.terrain);
        if (terrain == null || !terrain.passable || terrain.water) return false;
        if (spec.terrains.Count > 0 && !spec.terrains.Any(x => string.Equals(x, t.terrain, StringComparison.OrdinalIgnoreCase))) return false;
        return t.moisture >= spec.minMoisture;
    }

    /// <summary>Copy what the cover does onto its cell (read by travel, sight, needs and surveys without the catalog).</summary>
    public static void Apply(WorldTile t, CoverSpec spec, int patch)
    {
        t.cover = spec.id;
        t.coverPatch = patch;
        t.concealed = spec.blocksSight;
        t.coverSight = spec.sightInside;
        t.coverTravel = Math.Max(0.5f, spec.travel);
        t.coverSurvey = Math.Max(0.2f, spec.survey);
        t.coverFinds = Math.Max(0f, spec.finds);
        t.coverHardship = Math.Max(0f, spec.hardship);
    }

    // ===== SIGHT =====

    /// <summary>
    /// The micro hexes a watcher at <paramref name="from"/> sees within <paramref name="sight"/>: every hex whose line of
    /// sight crosses no concealed cell but the watcher's own (it sees the edge of a forest, not through it). From high
    /// ground (<paramref name="overCover"/>) the view carries over the canopy.
    /// </summary>
    public static List<HexCoord> Visible(WorldMap map, HexCoord from, int sight, bool overCover = false)
    {
        var seen = new List<HexCoord>();
        var home = HexHierarchy.Parent(from);
        foreach (var c in HexCoord.Spiral(from, Math.Max(0, sight)))
            if (overCover || Clear(map, from, c, home)) seen.Add(c);
        return seen;
    }

    /// <summary>Nothing hides <paramref name="to"/> from <paramref name="from"/>: no concealed cell but <paramref name="home"/> lies between them.</summary>
    public static bool Clear(WorldMap map, HexCoord from, HexCoord to, HexCoord home)
    {
        int n = HexCoord.Distance(from, to);
        for (int i = 1; i < n; i++)
        {
            double f = i / (double)n;
            // A nudge keeps the line off the seams between hexes (the same side every time).
            var h = HexCoord.Round(from.q + (to.q - from.q) * f + 1e-6, from.r + (to.r - from.r) * f + 2e-6);
            var cell = HexHierarchy.Parent(h);
            if (cell == home) continue;
            var t = map.Get(cell);
            if (t != null && t.concealed) return false;
        }
        return true;
    }

    /// <summary>Sight narrowed by the cover the watcher stands in (at least 1).</summary>
    public static int SightFrom(WorldTile here, int sight) => here != null && here.coverSight >= 0 ? Math.Max(1, Math.Min(sight, here.coverSight)) : sight;

    /// <summary>What stands on <paramref name="t"/> is hidden from view (concealed and not yet explored).</summary>
    public static bool Hides(WorldTile t) => t != null && t.concealed && !t.explored;

    // ===== WHAT IT GIVES =====

    public static CoverSpec SpecAt(WorldGenSettings settings, WorldTile t) => t?.cover != null ? settings?.Cover(t.cover) : null;

    public static CoverPatch PatchAt(WorldMap map, WorldTile t) =>
        map != null && t != null && t.coverPatch >= 0 && t.coverPatch < map.Covers.Count ? map.Covers[t.coverPatch] : null;

    /// <summary>What a held cell of cover yields per second (before improvement, beauty and administration).</summary>
    public static List<ResourceAmount> YieldsOf(WorldGenSettings settings, WorldTile t) => Amounts(SpecAt(settings, t)?.yields, 1f);

    /// <summary>What the cover adds to the ground's forage (times <paramref name="multiplier"/>).</summary>
    public static List<ResourceAmount> ForageOf(WorldGenSettings settings, WorldTile t, float multiplier) => Amounts(SpecAt(settings, t)?.forage, multiplier);

    private static List<ResourceAmount> Amounts(List<ResourceAmount> list, float scale) =>
        (list ?? new List<ResourceAmount>()).Where(a => a != null && !string.IsNullOrEmpty(a.resource) && a.amount > 0f)
            .Select(a => new ResourceAmount { resource = a.resource, amount = a.amount * Math.Max(0f, scale) }).ToList();

    /// <summary>"blocks sight, travel x1.6, surveys x1.5, finds x1.5, wears 3/Seventh" for cards.</summary>
    public static string Effects(CoverSpec spec)
    {
        if (spec == null) return string.Empty;
        var parts = new List<string>();
        if (spec.blocksSight) parts.Add("hides what lies inside and beyond");
        if (spec.sightInside >= 0) parts.Add($"sight {spec.sightInside} inside");
        if (Math.Abs(spec.travel - 1f) > 0.01f) parts.Add($"travel x{spec.travel:0.#}");
        if (Math.Abs(spec.survey - 1f) > 0.01f) parts.Add($"surveys x{spec.survey:0.#}");
        if (spec.hardship > 0.01f) parts.Add($"wears {spec.hardship:0.#} a Seventh");
        if (spec.finds > 1.01f) parts.Add($"finds x{spec.finds:0.#}");
        return string.Join(", ", parts);
    }
}
