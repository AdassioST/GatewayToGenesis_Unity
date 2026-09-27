using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// An area of the civilization's affairs a council seat answers for (defense, justice, diplomacy...). Seats list the
/// areas they cover (<see cref="CouncilSeatData.areas"/>, <see cref="CivicCouncilPosition.areas"/>) and stories ask for
/// an area, never a seat title (<c># cast: area:defense</c>), so any seat can be added, renamed or granted by a civic
/// and still be called on. Related areas make the search forgiving: with no seat for an area, the closest one answers.
/// </summary>
[Serializable]
public class CouncilArea
{
    [Tooltip("How seats and stories name the area (lowercase, unique): \"defense\".")]
    public string id;
    [Tooltip("Shown on seat tooltips: \"Defense\".")]
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("Other words that mean this area in a seat or a story (\"military\", \"war\").")]
    public List<string> aliases = new List<string>();
    [Tooltip("Areas next to this one: asked for and not covered, this area's seat answers first through them. Links work both ways.")]
    public List<string> related = new List<string>();

    public CouncilArea() { }

    public CouncilArea(string id, string name, string description, string[] aliases, string[] related)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        this.aliases = aliases.ToList();
        this.related = related.ToList();
    }
}

/// <summary>
/// Finding the legend who answers for an area, with no scene state (tested in <c>CouncilAreaTests</c>):
/// <list type="number">
/// <item>A seat covering the area, its legend free: the seat that lists the area first wins, then the seat order.</item>
/// <item>The closest seat through related areas (up to the catalog's maxDistance links).</item>
/// <item>No one: the caller falls back to the Head of State, then asks the player.</item>
/// </list>
/// </summary>
public static class CouncilAreaRules
{
    public const int DefaultMaxDistance = 2;

    /// <summary>A seat as the search sees it.</summary>
    public struct SeatView
    {
        public string title;
        public IList<string> areas;
        /// <summary>The seated legend (null when empty).</summary>
        public string holder;
        /// <summary>Position on the council (lower answers first on a tie).</summary>
        public int order;
    }

    /// <summary>Who answers for an area, and how.</summary>
    public struct Answer
    {
        public string legend;
        public string seat;
        /// <summary>The area the seat covers that answered (the one asked, or a related one).</summary>
        public string area;
        /// <summary>0 for the area itself, 1+ through related areas.</summary>
        public int distance;
        public bool Found => !string.IsNullOrEmpty(legend);
    }

    /// <summary>The canonical id for an area name or alias ("Military" → "defense"); null when unknown.</summary>
    public static string Resolve(string area, IEnumerable<CouncilArea> catalog)
    {
        string key = Key(area);
        if (key.Length == 0 || catalog == null) return null;
        foreach (var a in catalog.Where(a => a != null))
        {
            if (Key(a.id) == key || Key(a.name) == key) return a.id;
            if (a.aliases != null && a.aliases.Any(alias => Key(alias) == key)) return a.id;
        }
        return null;
    }

    /// <summary>The area's display name ("Defense"), or the text itself when unknown.</summary>
    public static string NameOf(string area, IEnumerable<CouncilArea> catalog)
    {
        string id = Resolve(area, catalog);
        var found = id != null ? catalog.First(a => a != null && a.id == id) : null;
        return found != null && !string.IsNullOrEmpty(found.name) ? found.name : area;
    }

    /// <summary>Links between two areas through <see cref="CouncilArea.related"/> (both directions); -1 when unconnected or unknown.</summary>
    public static int Distance(string from, string to, IEnumerable<CouncilArea> catalog)
    {
        var areas = catalog?.Where(a => a != null).ToList() ?? new List<CouncilArea>();
        string start = Resolve(from, areas), goal = Resolve(to, areas);
        if (start == null || goal == null) return -1;
        return Distances(start, areas).TryGetValue(goal, out int d) ? d : -1;
    }

    // Breadth-first from one area over the undirected graph of related areas.
    private static Dictionary<string, int> Distances(string start, List<CouncilArea> areas)
    {
        var neighbours = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in areas)
        {
            if (string.IsNullOrEmpty(a.id)) continue;
            if (!neighbours.ContainsKey(a.id)) neighbours[a.id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in a.related ?? new List<string>())
            {
                string other = Resolve(r, areas);
                if (other == null || other == a.id) continue;
                neighbours[a.id].Add(other);
                if (!neighbours.ContainsKey(other)) neighbours[other] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                neighbours[other].Add(a.id);
            }
        }
        var distance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string at = queue.Dequeue();
            if (!neighbours.TryGetValue(at, out var next)) continue;
            foreach (var n in next)
            {
                if (distance.ContainsKey(n)) continue;
                distance[n] = distance[at] + 1;
                queue.Enqueue(n);
            }
        }
        return distance;
    }

    /// <summary>
    /// The seated legend who answers for <paramref name="area"/> (see the class summary). <paramref name="free"/> says
    /// whether a legend can take the role now (null: everyone can). Not <see cref="Answer.Found"/> when no seat within
    /// <paramref name="maxDistance"/> links has a free legend.
    /// </summary>
    public static Answer Find(string area, IEnumerable<SeatView> seats, IEnumerable<CouncilArea> catalog, int maxDistance, Func<string, bool> free = null)
    {
        var areas = catalog?.Where(a => a != null).ToList() ?? new List<CouncilArea>();
        string wanted = Resolve(area, areas);
        if (wanted == null || seats == null) return default;
        var reach = Distances(wanted, areas);
        Answer best = default;
        int bestIndex = int.MaxValue, bestOrder = int.MaxValue;
        foreach (var seat in seats)
        {
            if (string.IsNullOrEmpty(seat.holder) || seat.areas == null || (free != null && !free(seat.holder))) continue;
            for (int i = 0; i < seat.areas.Count; i++)
            {
                string covered = Resolve(seat.areas[i], areas);
                if (covered == null || !reach.TryGetValue(covered, out int d) || d > Math.Max(0, maxDistance)) continue;
                // Closer first; then the seat for which this area comes first in its list (its main charge); then order.
                bool better = !best.Found || d < best.distance || (d == best.distance && (i < bestIndex || (i == bestIndex && seat.order < bestOrder)));
                if (!better) continue;
                best = new Answer { legend = seat.holder, seat = seat.title, area = covered, distance = d };
                bestIndex = i;
                bestOrder = seat.order;
            }
        }
        return best;
    }

    /// <summary>Area names on seats or stories that the catalog does not know (for the content validator).</summary>
    public static IEnumerable<string> Unknown(IEnumerable<string> names, IEnumerable<CouncilArea> catalog) =>
        (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n) && Resolve(n, catalog) == null);

    private static string Key(string text) => new string((text ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>
    /// The default areas (proposals). The relations sketch how affairs touch: a caravan at the gates is defense; with no
    /// one on defense, security or logistics answers; justice and diplomacy sit next to each other.
    /// </summary>
    public static readonly IReadOnlyList<CouncilArea> Defaults = new List<CouncilArea>
    {
        new CouncilArea("defense", "Defense", "The walls, the army, threats at the gates.", new[] { "military", "war", "army" }, new[] { "security", "logistics" }),
        new CouncilArea("security", "Security", "Order within: the watch, crime, unrest.", new[] { "order", "watch", "policing" }, new[] { "defense", "justice", "governance" }),
        new CouncilArea("justice", "Justice", "Law, judgment and disputes.", new[] { "judicial", "law", "judgment" }, new[] { "security", "diplomacy", "governance" }),
        new CouncilArea("diplomacy", "Diplomacy", "Strangers, envoys, caravans and other peoples.", new[] { "diplomatic", "foreign", "envoys" }, new[] { "justice", "economy", "culture" }),
        new CouncilArea("governance", "Governance", "Administration of the settlements and their people.", new[] { "administration", "civil", "civic" }, new[] { "justice", "welfare", "logistics" }),
        new CouncilArea("welfare", "Welfare", "Food, health, shelter and the people's spirits.", new[] { "health", "people", "morale" }, new[] { "governance", "faith" }),
        new CouncilArea("economy", "Economy", "The treasury, trade and markets.", new[] { "treasury", "trade", "finance", "commerce" }, new[] { "diplomacy", "industry", "logistics" }),
        new CouncilArea("industry", "Industry", "Craft, building and production.", new[] { "craft", "construction", "production" }, new[] { "economy", "innovation", "logistics" }),
        new CouncilArea("logistics", "Logistics", "Stores, supplies and roads.", new[] { "supply", "supplies", "storage" }, new[] { "defense", "governance", "exploration" }),
        new CouncilArea("exploration", "Exploration", "The wilds beyond the settlements and those who walk them.", new[] { "expeditions", "wilds", "discovery" }, new[] { "logistics", "lore" }),
        new CouncilArea("lore", "Lore", "Records, history and the relics of the old world.", new[] { "history", "records", "knowledge" }, new[] { "innovation", "mysticism", "exploration" }),
        new CouncilArea("innovation", "Innovation", "Research and invention.", new[] { "research", "science", "invention" }, new[] { "lore", "industry" }),
        new CouncilArea("mysticism", "Mysticism", "Magic, omens and the heavens.", new[] { "magic", "omens", "arcane", "celestial" }, new[] { "faith", "lore" }),
        new CouncilArea("faith", "Faith", "Rites, the dead and the spirit.", new[] { "rites", "religion", "spiritual" }, new[] { "mysticism", "welfare", "culture" }),
        new CouncilArea("culture", "Culture", "Song, art and festivals.", new[] { "arts", "music", "festivals" }, new[] { "faith", "diplomacy", "welfare" }),
    };
}
