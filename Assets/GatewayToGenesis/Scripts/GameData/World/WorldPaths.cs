using System;
using System.Collections.Generic;

/// <summary>
/// Travel over the meso cells (Arcanoria.md, Magical Pathways): units move faster along leylines and roads, low
/// Coherence and danger are attrition zones that slow them, water and barriers stop them. Roads are planned over it
/// with a preference for high Coherence ("the pathways of Trade Routes prefer to travel in high Coherence zones along
/// Leylines"). Units themselves walk the finer micro grid (<see cref="MicroGrid"/>), whose steps share these factors.
/// No scene state (tested in <c>WorldCivilizationTests</c>). Costs are proposals.
/// </summary>
public static class WorldPaths
{
    public const float LeylineFactor = 0.6f, RoadFactor = 0.5f;

    /// <summary>Cost of entering a cell on foot, or +infinity for water and impassable ground.</summary>
    public static float StepCost(WorldTile t, WorldGenSettings settings)
    {
        if (t == null || t.water || t.impassable) return float.PositiveInfinity;
        var terrain = settings?.Terrain(t.terrain);
        if (terrain != null && (!terrain.passable || terrain.water)) return float.PositiveInfinity;
        float cost = Math.Max(0.2f, terrain?.moveCost ?? 1f);
        if (t.road) cost *= RoadFactor;
        return cost * MicroGrid.CellFactor(t);
    }

    /// <summary>Cost of laying road through a cell: existing road is nearly free; high Coherence is preferred.</summary>
    public static float RoadCost(WorldTile t, WorldGenSettings settings)
    {
        float step = StepCost(t, settings);
        if (float.IsPositiveInfinity(step)) return step;
        if (t.road) return 0.05f;
        return step * (1.6f - 0.9f * t.coherence);
    }

    /// <summary>
    /// The cheapest path from <paramref name="from"/> to the first cell satisfying <paramref name="isGoal"/> (the goal
    /// itself need not be passable when <paramref name="enterGoal"/> is true: a road may end at a settlement standing
    /// on odd ground). Cells from start to goal inclusive; false when nothing is reachable within <paramref name="maxCost"/>.
    /// </summary>
    public static bool Find(WorldMap map, int from, Func<int, bool> isGoal, Func<WorldTile, float> cost, out List<int> path, out float total, float maxCost = float.PositiveInfinity)
    {
        path = new List<int>();
        total = 0f;
        if (map == null || from < 0 || from >= map.Count) return false;
        var best = new Dictionary<int, float> { [from] = 0f };
        var back = new Dictionary<int, int>();
        var heap = new Heap();
        heap.Push(from, 0f);
        int goal = -1;
        while (heap.Count > 0)
        {
            heap.Pop(out int c, out float g);
            if (g > best[c]) continue;
            if (isGoal(c))
            {
                goal = c;
                total = g;
                break;
            }
            for (int d = 0; d < 6; d++)
            {
                int nb = map.Neighbour(c, d);
                if (nb < 0) continue;
                // A goal on ground that cannot be walked (a settlement on odd ground a road must reach) costs one step.
                float step = cost(map[nb]);
                if (float.IsPositiveInfinity(step) && isGoal(nb)) step = 1f;
                if (float.IsPositiveInfinity(step) || float.IsNaN(step)) continue;
                float next = g + step;
                if (next > maxCost) continue;
                if (best.TryGetValue(nb, out float known) && known <= next) continue;
                best[nb] = next;
                back[nb] = c;
                heap.Push(nb, next);
            }
        }
        if (goal < 0) return false;
        for (int c = goal; ; c = back[c])
        {
            path.Add(c);
            if (c == from) break;
        }
        path.Reverse();
        return true;
    }

    /// <summary>
    /// The cheapest cost from <paramref name="from"/> to every cell within <paramref name="maxCost"/> (the start at 0;
    /// cells whose <paramref name="cost"/> is +infinity are never entered). Used by territorial pull.
    /// </summary>
    public static Dictionary<int, float> Flood(WorldMap map, int from, Func<WorldTile, float> cost, float maxCost)
    {
        var best = new Dictionary<int, float>();
        if (map == null || from < 0 || from >= map.Count) return best;
        best[from] = 0f;
        var heap = new Heap();
        heap.Push(from, 0f);
        while (heap.Count > 0)
        {
            heap.Pop(out int c, out float g);
            if (g > best[c]) continue;
            for (int d = 0; d < 6; d++)
            {
                int nb = map.Neighbour(c, d);
                if (nb < 0) continue;
                float step = cost(map[nb]);
                if (float.IsPositiveInfinity(step) || float.IsNaN(step)) continue;
                float next = g + step;
                if (next > maxCost) continue;
                if (best.TryGetValue(nb, out float known) && known <= next) continue;
                best[nb] = next;
                heap.Push(nb, next);
            }
        }
        return best;
    }

    /// <summary>Travel cost on foot between two cells, or +infinity when no way over land joins them.</summary>
    public static float TravelCost(WorldMap map, WorldGenSettings settings, int from, int to, out List<int> path)
    {
        if (Find(map, from, c => c == to, t => StepCost(t, settings), out path, out float total, 4000f)) return total;
        return float.PositiveInfinity;
    }

    private class Heap
    {
        private readonly List<(float key, int cell)> _items = new List<(float, int)>();
        public int Count => _items.Count;

        public void Push(int cell, float key)
        {
            _items.Add((key, cell));
            int c = _items.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (!Less(_items[c], _items[p])) break;
                (_items[c], _items[p]) = (_items[p], _items[c]);
                c = p;
            }
        }

        public void Pop(out int cell, out float key)
        {
            var top = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1, r = l + 1, m = c;
                if (l < _items.Count && Less(_items[l], _items[m])) m = l;
                if (r < _items.Count && Less(_items[r], _items[m])) m = r;
                if (m == c) break;
                (_items[c], _items[m]) = (_items[m], _items[c]);
                c = m;
            }
            cell = top.cell;
            key = top.key;
        }

        private static bool Less((float key, int cell) a, (float key, int cell) b) => a.key < b.key || (a.key == b.key && a.cell < b.cell);
    }
}
