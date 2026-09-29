using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A road as contact sees it: its two ends (settlement ids) and what interrupts it now (null: open).</summary>
public struct ContactRoute
{
    public int id, from, to;
    public string blockedBy;

    public ContactRoute(int id, int from, int to, string blockedBy = null)
    {
        this.id = id; this.from = from; this.to = to; this.blockedBy = blockedBy;
    }
}

/// <summary>
/// Two standing settlements joined by road, directly or through the ruins of a fallen one (roads outlive the places
/// they joined). Open when some way between them is uninterrupted; otherwise <see cref="blockedBy"/> says why.
/// </summary>
public sealed class ContactEdge
{
    public readonly int a, b;
    public readonly bool open;
    public readonly string blockedBy;
    public readonly IReadOnlyList<int> routes;

    public ContactEdge(int a, int b, bool open, string blockedBy, IReadOnlyList<int> routes)
    {
        this.a = Math.Min(a, b); this.b = Math.Max(a, b); this.open = open; this.blockedBy = blockedBy; this.routes = routes;
    }

    public int Other(int settlement) => settlement == a ? b : a;
    public bool Touches(int settlement) => settlement == a || settlement == b;
}

/// <summary>
/// Which of your settlements are in contact, derived from the roads actually built (never from distance, never from
/// territory). Built once per change of the map's roads and settlements (<see cref="Build"/>), not per custom or frame.
/// A road enables contact only: nothing here moves land, ownership or loyalty.
/// </summary>
public sealed class ContactSnapshot
{
    private readonly Dictionary<int, string> _standing;
    private readonly List<ContactEdge> _edges;

    public static readonly ContactSnapshot Empty = new ContactSnapshot(new Dictionary<int, string>(), new List<ContactEdge>());

    private ContactSnapshot(Dictionary<int, string> standing, List<ContactEdge> edges)
    {
        _standing = standing;
        _edges = edges;
    }

    /// <summary>Settlements standing now, by id, with their names.</summary>
    public IReadOnlyDictionary<int, string> Standing => _standing;
    public IReadOnlyList<ContactEdge> Edges => _edges;
    public bool Stands(int settlement) => _standing.ContainsKey(settlement);
    public string NameOf(int settlement) => _standing.TryGetValue(settlement, out var n) ? n : null;

    public IEnumerable<ContactEdge> EdgesOf(int settlement) => _edges.Where(e => e.Touches(settlement));
    public IEnumerable<int> OpenNeighbours(int settlement) => _edges.Where(e => e.open && e.Touches(settlement)).Select(e => e.Other(settlement));
    public ContactEdge Edge(int x, int y) => _edges.FirstOrDefault(e => e.a == Math.Min(x, y) && e.b == Math.Max(x, y));

    /// <summary>
    /// The contact between <paramref name="standing"/> settlements along <paramref name="routes"/>. A route's end that no
    /// longer stands (a fallen settlement) is a junction: the road runs on through its ruins. An edge is open when some
    /// way between its two settlements uses only open routes.
    /// </summary>
    public static ContactSnapshot Build(IEnumerable<(int id, string name)> standing, IEnumerable<ContactRoute> routes)
    {
        var names = new Dictionary<int, string>();
        foreach (var (id, name) in standing ?? Enumerable.Empty<(int, string)>()) names[id] = name;
        var list = (routes ?? Enumerable.Empty<ContactRoute>()).Where(r => r.from != r.to).ToList();
        var byNode = new Dictionary<int, List<ContactRoute>>();
        foreach (var r in list)
        {
            if (!byNode.TryGetValue(r.from, out var f)) byNode[r.from] = f = new List<ContactRoute>();
            if (!byNode.TryGetValue(r.to, out var t)) byNode[r.to] = t = new List<ContactRoute>();
            f.Add(r);
            t.Add(r);
        }
        var found = new Dictionary<(int, int), (bool open, string blocked, SortedSet<int> routes)>();
        foreach (int start in names.Keys.OrderBy(i => i))
        {
            // Walk out from each standing settlement, through junctions only, twice: along open routes, then along any.
            foreach (bool openOnly in new[] { true, false })
            {
                var seen = new HashSet<int> { start };
                var queue = new Queue<(int node, string blocked, List<int> via)>();
                queue.Enqueue((start, null, new List<int>()));
                while (queue.Count > 0)
                {
                    var (node, blocked, via) = queue.Dequeue();
                    if (!byNode.TryGetValue(node, out var edges)) continue;
                    foreach (var r in edges.OrderBy(e => e.id))
                    {
                        if (openOnly && r.blockedBy != null) continue;
                        int next = r.from == node ? r.to : r.from;
                        if (!seen.Add(next)) continue;
                        var path = new List<int>(via) { r.id };
                        string why = blocked ?? r.blockedBy;
                        if (names.ContainsKey(next))
                        {
                            if (next == start) continue;
                            var key = (Math.Min(start, next), Math.Max(start, next));
                            if (!found.TryGetValue(key, out var have)) found[key] = (openOnly, openOnly ? null : why, new SortedSet<int>(path));
                            else if (openOnly && have.open) have.routes.UnionWith(path);
                            continue;
                        }
                        queue.Enqueue((next, why, path));
                    }
                }
            }
        }
        var result = found.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2)
            .Select(p => new ContactEdge(p.Key.Item1, p.Key.Item2, p.Value.open, p.Value.open ? null : p.Value.blocked ?? "interrupted", p.Value.routes.ToList())).ToList();
        return new ContactSnapshot(names, result);
    }
}
