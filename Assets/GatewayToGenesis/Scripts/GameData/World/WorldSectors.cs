using System;
using System.Collections.Generic;

/// <summary>
/// The nine Sectors of each Macro Biome (AECOR's finest named scale; ARCHITECTURE.md, "World scales"): its centre
/// and the eight compass directions around it, measured from the centre of the Macro Biome's own cells, north up. The
/// Central Sector holds about a ninth of the Macro Biome (as the middle square of AECOR's three-by-three), and the
/// rest are 45-degree wedges. With no scene state (tested in <c>WorldGenerationTests</c>).
/// </summary>
public static class WorldSectors
{
    /// <summary>Radius of the Central Sector as a share of the Macro Biome's equivalent radius (a ninth of its area).</summary>
    public const float CentralShare = 1f / 3f;

    private static readonly CompassSector[] Wedges =
    {
        CompassSector.East, CompassSector.NorthEast, CompassSector.North, CompassSector.NorthWest,
        CompassSector.West, CompassSector.SouthWest, CompassSector.South, CompassSector.SouthEast,
    };

    /// <summary>
    /// The Sector of a point <paramref name="dx"/>, <paramref name="dy"/> away from its Macro Biome's centre (y grows
    /// north), in a Macro Biome of equivalent radius <paramref name="radius"/>.
    /// </summary>
    public static CompassSector Of(float dx, float dy, float radius)
    {
        if (dx * dx + dy * dy <= radius * CentralShare * radius * CentralShare) return CompassSector.Central;
        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI; // 0 east, counter-clockwise
        int wedge = (int)Math.Floor((angle + 360.0 + 22.5) / 45.0) % 8;
        return Wedges[wedge];
    }

    /// <summary>Gives every cell of every Macro Biome its Sector; intersections and the ocean get None.</summary>
    public static void Assign(WorldMap map)
    {
        var members = new Dictionary<int, List<WorldTile>>();
        foreach (var t in map.Tiles)
        {
            t.sector = CompassSector.None;
            if (t.composition != WorldComposition.MacroBiome || t.slot < 0) continue;
            if (!members.TryGetValue(t.slot, out var list)) members[t.slot] = list = new List<WorldTile>();
            list.Add(t);
        }
        foreach (var cells in members.Values)
        {
            double cx = 0, cy = 0;
            foreach (var t in cells) { cx += t.x; cy += t.y; }
            cx /= cells.Count;
            cy /= cells.Count;
            // A uniform disc's root-mean-square distance from its centre is its radius over the square root of two.
            double squares = 0;
            foreach (var t in cells) squares += (t.x - cx) * (t.x - cx) + (t.y - cy) * (t.y - cy);
            float radius = (float)Math.Sqrt(2.0 * squares / cells.Count);
            foreach (var t in cells) t.sector = Of((float)(t.x - cx), (float)(t.y - cy), radius);
        }
    }

    /// <summary>"North-East Sector", or null for None.</summary>
    public static string Name(CompassSector sector)
    {
        switch (sector)
        {
            case CompassSector.Central: return "Central Sector";
            case CompassSector.North: return "North Sector";
            case CompassSector.NorthEast: return "North-East Sector";
            case CompassSector.East: return "East Sector";
            case CompassSector.SouthEast: return "South-East Sector";
            case CompassSector.South: return "South Sector";
            case CompassSector.SouthWest: return "South-West Sector";
            case CompassSector.West: return "West Sector";
            case CompassSector.NorthWest: return "North-West Sector";
            default: return null;
        }
    }
}
