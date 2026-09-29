using System;
using System.Collections.Generic;

/// <summary>
/// The lines laid over the world (rivers with their fixed meander, roads) as points in world units, with no scene
/// state. <see cref="WorldRenderer"/> draws these points and <see cref="MicroGrid"/> reads the micro hexes under them,
/// so the river a unit fords and the road it follows are exactly the ones on screen.
/// </summary>
public static class WorldGeometry
{
    /// <summary>A point of a line and a value carried along it (a river's flow).</summary>
    public struct Point
    {
        public float x, y, value;

        public Point(float x, float y, float value = 0f)
        {
            this.x = x;
            this.y = y;
            this.value = value;
        }
    }

    /// <summary>Share of a meso cell's spacing a river swings from the straight line between cell centres.</summary>
    public const float MeanderShare = 0.28f;

    /// <summary>
    /// A river through its cells' centres with a gentle, fixed meander (the ends stay put, so rivers still meet their
    /// water). Each point carries its cell's flow.
    /// </summary>
    public static List<Point> RiverPoints(WorldMap map, RiverPath river)
    {
        var points = new List<Point>();
        if (map == null || river == null) return points;
        float bend = (float)HexHierarchy.Spacing(HexHierarchy.Meso) * MeanderShare;
        int seed = WorldNoise.Stream(map.seed, "meander");
        for (int k = 0; k < river.cells.Count; k++)
        {
            var cell = map[river.cells[k]];
            float x = cell.x, y = cell.y;
            if (k > 0 && k < river.cells.Count - 1)
            {
                var prev = map[river.cells[k - 1]];
                var next = map[river.cells[k + 1]];
                float ax = next.x - prev.x, ay = next.y - prev.y;
                float length = (float)Math.Sqrt(ax * ax + ay * ay);
                if (length > 1e-6f)
                {
                    ax /= length;
                    ay /= length;
                }
                float swing = (float)(WorldNoise.Hash01(seed, cell.coord.q, cell.coord.r) * 2.0 - 1.0);
                x += -ay * swing * bend;
                y += ax * swing * bend;
            }
            points.Add(new Point(x, y, cell.flow));
        }
        return points;
    }

    /// <summary>A road through its cells' centres.</summary>
    public static List<Point> RoadPoints(WorldMap map, TradeRoute route)
    {
        var points = new List<Point>();
        if (map == null || route == null) return points;
        foreach (var c in route.cells)
        {
            var t = map.Get(c);
            if (t != null) points.Add(new Point(t.x, t.y));
        }
        return points;
    }

    /// <summary>The points of an Old World road (cell indices, <see cref="WorldMap.OldRoads"/>), for drawing and the travel grid.</summary>
    public static List<Point> OldRoadPoints(WorldMap map, List<int> cells)
    {
        var points = new List<Point>();
        if (map == null || cells == null) return points;
        foreach (int c in cells)
            if (c >= 0 && c < map.Count) points.Add(new Point(map[c].x, map[c].y));
        return points;
    }

    /// <summary>Chaikin corner cutting (the renderer's smoothing): each pass keeps the ends and replaces every corner by two points.</summary>
    public static List<Point> Smooth(List<Point> points, int passes = 2)
    {
        for (int pass = 0; pass < passes && points != null && points.Count >= 3; pass++)
        {
            var smooth = new List<Point>(points.Count * 2) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                smooth.Add(new Point(a.x + (b.x - a.x) * 0.25f, a.y + (b.y - a.y) * 0.25f, a.value + (b.value - a.value) * 0.25f));
                smooth.Add(new Point(a.x + (b.x - a.x) * 0.75f, a.y + (b.y - a.y) * 0.75f, a.value + (b.value - a.value) * 0.75f));
            }
            smooth.Add(points[points.Count - 1]);
            points = smooth;
        }
        return points;
    }

    /// <summary>
    /// Every micro hex a line passes through, in order, each once, with the value carried there (sampled finely enough
    /// that consecutive hexes always share an edge, so a line is a wall no step slips past).
    /// </summary>
    public static List<(HexCoord hex, float value)> Hexes(List<Point> points, float step = 0.2f)
    {
        var hexes = new List<(HexCoord, float)>();
        if (points == null || points.Count == 0) return hexes;
        var seen = new HashSet<HexCoord>();
        void Add(float x, float y, float value)
        {
            var hex = HexHierarchy.MicroAt(x, y);
            if (seen.Add(hex)) hexes.Add((hex, value));
        }
        Add(points[0].x, points[0].y, points[0].value);
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            float dx = b.x - a.x, dy = b.y - a.y;
            int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / Math.Max(0.01f, step)));
            for (int s = 1; s <= n; s++)
            {
                float t = s / (float)n;
                Add(a.x + dx * t, a.y + dy * t, a.value + (b.value - a.value) * t);
            }
        }
        return hexes;
    }
}
