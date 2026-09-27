using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The settlement categories of Arcanoria.md (Map Features, Settlements), plus the Religious Haven of Places.</summary>
public enum SettlementKind { Capital, Town, Major, Outpost, Haven }

/// <summary>A settlement on the map (saved with the world).</summary>
[Serializable]
public class Settlement
{
    public int id;
    public string name;
    public SettlementKind kind;
    public HexCoord coord;
    public int foundedAge;
    /// <summary>Age transitions it has stood through (a settlement founded mid-Age counts that Age too).</summary>
    public int agesPresent;
    /// <summary>Whole Ages it stood through, from an Age's start to its end (Arcanoria.md: settlement Ornaments need
    /// three entire Ages, with The Truth of Arcanoria). What loss and reclamation do to it is D07.</summary>
    public int fullAges;
    /// <summary>It has stood since the current Age began (the Capital from the world's birth; others from the next Age).</summary>
    public bool standsSinceAgeStart;
    /// <summary>City Development, 0-100.</summary>
    public float development;
    /// <summary>A Major Settlement's binding (one of the seven of The Principles of Magic), or null.</summary>
    public string binding;
    public bool anchor;
    /// <summary>Outside Administrative Authority: it holds only its own cell (every Outpost; a Haven founded in the wilds).</summary>
    public bool detached;
    public int authorityRadius;
    /// <summary>Id of the Major Settlement or Capital it answers to (-1 for the Capital and detached settlements).</summary>
    public int governedBy = -1;
}

/// <summary>A road between two settlements: the Trade Route's path and its Trade Nodes (saved with the world).</summary>
[Serializable]
public class TradeRoute
{
    public int id;
    public int from, to;
    public List<HexCoord> cells = new List<HexCoord>();
    public List<HexCoord> nodes = new List<HexCoord>();
    /// <summary>Share of Coherence and leylines along it (Trade Routes prefer high Coherence; efficiency changes with the Ages).</summary>
    public float efficiency;
}

/// <summary>An Enclave standing on the map (saved: its influence and suzerainty change through play).</summary>
[Serializable]
public class Enclave
{
    public int index;
    public string spec;
    public string name;
    public EnclaveFamily family;
    public string binding;
    public HexCoord coord;
    public int age;
    /// <summary>Wound Resonance, 0-100 (Enclave.md).</summary>
    public float woundResonance;
    /// <summary>Your standing with it, 0-100; at 100 you hold its Suzerainty.</summary>
    public float influence;
    public bool suzerain;
    public int authorityRadius;

    public string AuthorityId => "enclave:" + index;

    /// <summary>The vault's three states of Wound Resonance.</summary>
    public string WoundState => woundResonance >= 75f ? "raw wound" : woundResonance >= 40f ? "integrated wound" : "forgotten wound";
}

/// <summary>A Resource Grandfield's footprint (generated).</summary>
public class Grandfield
{
    public int index;
    public string spec;
    public string resource;
    public int center;
    public readonly List<int> cells = new List<int>();
}

/// <summary>A threat's source on the map (generated each Age).</summary>
public class ThreatSite
{
    public string spec;
    public int cell;
    public int age;
    public float strength, radius;
}

/// <summary>One line of what the civilization's map produces: a flat amount per second, or a percent bonus.</summary>
public struct WorldYield
{
    public string source, resource;
    public float amount;
    public bool percent;
}

/// <summary>
/// The civilization's life on the map, with no scene state (tested in <c>WorldCivilizationTests</c>). Everything
/// lives in the <see cref="WorldMap"/>'s lists; <see cref="WorldSystem"/> pays the costs and applies the yields.
///
/// - Developing Towns: inside Administrative Authority, at least <see cref="SettlementRules.spacing"/> cells from any
///   other settlement; they extend the reach a little and grow City Development toward their ground's potential.
/// - Major Settlements: a town promoted at <see cref="SettlementRules.majorThreshold"/> City Development, within the
///   Capital's Government Capacity; attuned to a binding; major anchors of authority; towns in their zone answer to them.
/// - Outposts: detached stations for extraction (a grandfield needs one), trade or Resonance Anchors; incorporated
///   into a town once authority reaches them. Religious Havens: at Sacred Sites or high Vibrational Density.
/// - Roads join a settlement to the Capital's network along high Coherence; Trade Nodes stand along them.
/// </summary>
public static class WorldCivilization
{
    public static string KindName(SettlementKind kind)
    {
        switch (kind)
        {
            case SettlementKind.Capital: return "Capital";
            case SettlementKind.Town: return "Developing Town";
            case SettlementKind.Major: return "Major Settlement";
            case SettlementKind.Outpost: return "Outpost";
            default: return "Religious Haven";
        }
    }

    public static Settlement At(WorldMap map, HexCoord coord) => map.Settlements.FirstOrDefault(s => s.coord == coord);

    public static Settlement Get(WorldMap map, int id) => map.Settlements.FirstOrDefault(s => s.id == id);

    public static Settlement Capital(WorldMap map) => map.Settlements.FirstOrDefault(s => s.kind == SettlementKind.Capital);

    /// <summary>Record the Capital as the first settlement (once, at generation).</summary>
    public static void EnsureCapital(WorldMap map, WorldGenSettings settings)
    {
        if (Capital(map) != null || map.Get(map.Capital) == null) return;
        map.Settlements.Insert(0, new Settlement { id = 0, name = "The Capital", kind = SettlementKind.Capital, coord = map.Capital, authorityRadius = settings.capitalAuthorityRadius, development = 20f, standsSinceAgeStart = true });
    }

    // ===== DERIVED STATE =====

    /// <summary>Re-derive each cell's settlement, enclave, road and node marks, then authority and who governs whom.</summary>
    public static void Rebuild(WorldMap map, WorldGenSettings settings)
    {
        foreach (var t in map.Tiles)
        {
            t.settlement = -1;
            t.enclave = -1;
            t.road = false;
            t.tradeNode = false;
        }
        for (int i = 0; i < map.Settlements.Count; i++)
        {
            var t = map.Get(map.Settlements[i].coord);
            if (t != null) t.settlement = i;
        }
        for (int i = 0; i < map.Enclaves.Count; i++)
        {
            var t = map.Get(map.Enclaves[i].coord);
            if (t != null) t.enclave = i;
        }
        foreach (var route in map.Routes)
        {
            foreach (var c in route.cells) { var t = map.Get(c); if (t != null) t.road = true; }
            foreach (var c in route.nodes) { var t = map.Get(c); if (t != null) t.tradeNode = true; }
        }
        WorldBeauty.Refresh(map, settings);
        WorldAuthority.Establish(map, settings);
        var territory = WorldTerritory.Compute(map, settings);
        // A town answers to the Major Settlement whose pull reaches it strongest (its secondary zone of control), else the Capital.
        var capital = Capital(map);
        foreach (var s in map.Settlements)
        {
            s.governedBy = -1;
            if (s.kind == SettlementKind.Capital || s.kind == SettlementKind.Major || s.detached) continue;
            var t = map.Get(s.coord);
            TerritorySeat major = null;
            float strongest = 0f;
            foreach (var seat in territory.Seats)
            {
                if (seat.kind != SeatKind.Major || t == null) continue;
                float pull = seat.PullAt(t.index);
                if (pull > strongest) { strongest = pull; major = seat; }
            }
            s.governedBy = major?.settlement ?? capital?.id ?? -1;
        }
        map.CivilizationVersion++;
    }

    /// <summary>Ids of the settlements joined to the Capital by roads.</summary>
    public static HashSet<int> Networked(WorldMap map)
    {
        var capital = Capital(map);
        var reached = new HashSet<int>();
        if (capital == null) return reached;
        reached.Add(capital.id);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var route in map.Routes)
            {
                if (reached.Contains(route.from) && reached.Add(route.to)) grew = true;
                if (reached.Contains(route.to) && reached.Add(route.from)) grew = true;
            }
        }
        return reached;
    }

    /// <summary>The breakdown of a settlement's City Development on its own cell.</summary>
    public static DevelopmentBreakdown Breakdown(WorldMap map, SettlementRules rules, Settlement s)
    {
        var t = map.Get(s.coord);
        return t == null ? new DevelopmentBreakdown() : CityDevelopment.Evaluate(map, rules, t.index, Networked(map).Contains(s.id));
    }

    public static int GovernmentCapacity(SettlementRules rules, int capacityBuildings) => Math.Max(0, rules.governmentCapacity) + Math.Max(0, capacityBuildings);

    public static int MajorCount(WorldMap map) => map.Settlements.Count(s => s.kind == SettlementKind.Major);

    // ===== FOUNDING =====

    /// <summary>Why a <paramref name="kind"/> cannot be founded at <paramref name="coord"/>, or null when it can.</summary>
    public static string WhyNotFound(WorldMap map, WorldGenSettings settings, SettlementRules rules, HexCoord coord, SettlementKind kind)
    {
        var t = map.Get(coord);
        if (t == null) return "Beyond the edge of the world.";
        if (kind == SettlementKind.Capital || kind == SettlementKind.Major) return "Major Settlements grow from Developing Towns.";
        if (t.water) return "Settlements stand on dry land.";
        if (float.IsPositiveInfinity(WorldPaths.StepCost(t, settings))) return "No settlement can stand on this ground.";
        if (kind == SettlementKind.Outpost ? !t.known : !t.explored) return kind == SettlementKind.Outpost ? "A scout must pass over it first." : "Survey it first.";
        if (t.settlement >= 0 || t.enclave >= 0) return "Something already stands here.";
        int spacing = kind == SettlementKind.Outpost ? Math.Max(1, rules.spacing / 2) : Math.Max(1, rules.spacing);
        var near = map.Settlements.Where(s => HexCoord.Distance(s.coord, coord) < spacing).OrderBy(s => HexCoord.Distance(s.coord, coord)).FirstOrDefault();
        if (near != null) return $"Too close to {near.name} ({spacing} cells apart at least).";
        if (map.Enclaves.Any(e => HexCoord.Distance(e.coord, coord) < spacing)) return "Too close to an enclave.";
        bool yours = t.authorityId == WorldAuthority.Player;
        bool free = t.authorityId == WorldAuthority.Wilderness;
        switch (kind)
        {
            case SettlementKind.Town:
                if (!yours) return "Developing Towns stand inside your Administrative Authority.";
                break;
            case SettlementKind.Outpost:
                if (yours) return "Inside your authority: found a Developing Town instead.";
                if (!free) return "Another authority holds this ground.";
                break;
            case SettlementKind.Haven:
                if (!yours && !free) return "Another authority holds this ground.";
                bool sacredNear = HexCoord.Spiral(coord, Math.Max(0, rules.havenSacredReach)).Any(c => map.Get(c)?.sacred == true);
                if (!sacredNear && t.coherence < rules.havenCoherence) return $"A Religious Haven needs a Sacred Site within {rules.havenSacredReach} cells or Coherence {rules.havenCoherence:P0}.";
                break;
        }
        return null;
    }

    /// <summary>An Age has ended: every settlement stood through it, wholly if it stood from its start, and stands from
    /// the start of the next.</summary>
    public static void AgePassed(WorldMap map)
    {
        foreach (var s in map.Settlements)
        {
            s.agesPresent++;
            if (s.standsSinceAgeStart) s.fullAges++;
            s.standsSinceAgeStart = true;
        }
    }

    /// <summary>Found a settlement (check <see cref="WhyNotFound"/> first); its worked ground is explored.</summary>
    public static Settlement Found(WorldMap map, WorldGenSettings settings, SettlementRules rules, HexCoord coord, SettlementKind kind, int age)
    {
        var t = map.Get(coord);
        bool detached = kind == SettlementKind.Outpost || t.authorityId != WorldAuthority.Player;
        int number = map.Settlements.Count(s => s.kind == kind) + 1;
        var settlement = new Settlement
        {
            id = map.Settlements.Count == 0 ? 0 : map.Settlements.Max(s => s.id) + 1,
            name = $"{KindName(kind)} {number}",
            kind = kind,
            coord = coord,
            foundedAge = age,
            detached = detached,
            authorityRadius = detached ? 0 : rules.townAuthorityRadius,
        };
        map.Settlements.Add(settlement);
        map.ExploreAround(coord, detached ? 0 : Math.Max(0, rules.workRadius), Math.Max(1, rules.workRadius + 1), id => settings.Terrain(id)?.passable != false);
        Rebuild(map, settings);
        return settlement;
    }

    public static string WhyNotPromote(WorldMap map, SettlementRules rules, Settlement s, int capacity)
    {
        if (s == null) return "Nothing to promote.";
        if (s.kind != SettlementKind.Town) return "Only Developing Towns become Major Settlements.";
        if (s.detached) return "Bring it inside your Administrative Authority first.";
        if (s.development < rules.majorThreshold) return $"Needs {rules.majorThreshold:0} City Development ({s.development:0} now).";
        if (MajorCount(map) >= capacity) return $"Government Capacity is full ({MajorCount(map)}/{capacity} Major Settlements): improve the Capital.";
        return null;
    }

    /// <summary>A Developing Town becomes a Major Settlement, attuned to the binding its ground suggests.</summary>
    public static void Promote(WorldMap map, WorldGenSettings settings, SettlementRules rules, Settlement s)
    {
        s.kind = SettlementKind.Major;
        s.authorityRadius = rules.majorAuthorityRadius;
        s.name = s.name.Replace(KindName(SettlementKind.Town), KindName(SettlementKind.Major));
        var t = map.Get(s.coord);
        if (string.IsNullOrEmpty(s.binding) && t != null) s.binding = CityDevelopment.SuggestBinding(map, t.index);
        Rebuild(map, settings);
    }

    /// <summary>The next of the seven bindings for a Major Settlement.</summary>
    public static void CycleBinding(Settlement s)
    {
        if (s == null || s.kind != SettlementKind.Major) return;
        int at = Array.IndexOf(CityDevelopment.Bindings, s.binding);
        s.binding = CityDevelopment.Bindings[(at + 1) % CityDevelopment.Bindings.Length];
    }

    public static string WhyNotIncorporate(WorldMap map, Settlement s)
    {
        if (s == null || !s.detached) return "It already answers to your Administrative Authority.";
        var t = map.Get(s.coord);
        if (t == null || !map.NeighboursOf(t).Any(n => n.authorityId == WorldAuthority.Player)) return "Your Administrative Authority does not reach it yet.";
        return null;
    }

    /// <summary>A detached settlement joins Administrative Authority: an Outpost becomes a Developing Town.</summary>
    public static void Incorporate(WorldMap map, WorldGenSettings settings, SettlementRules rules, Settlement s)
    {
        s.detached = false;
        s.authorityRadius = rules.townAuthorityRadius;
        if (s.kind == SettlementKind.Outpost)
        {
            s.kind = SettlementKind.Town;
            s.name = $"{KindName(SettlementKind.Town)} {map.Settlements.Count(x => x.kind == SettlementKind.Town)}";
        }
        Rebuild(map, settings);
    }

    // ===== ROADS =====

    /// <summary>
    /// The road that would join <paramref name="s"/> to the Capital's network: the cheapest way to any networked
    /// settlement, preferring high Coherence and reusing roads. False when it is joined already or no way exists.
    /// </summary>
    public static bool PlanRoad(WorldMap map, WorldGenSettings settings, Settlement s, out List<int> path, out int target)
    {
        path = new List<int>();
        target = -1;
        var network = Networked(map);
        if (s == null || network.Contains(s.id)) return false;
        var start = map.Get(s.coord);
        if (start == null) return false;
        var goals = new Dictionary<int, int>();
        foreach (var other in map.Settlements.Where(o => o.id != s.id && network.Contains(o.id)))
        {
            var t = map.Get(other.coord);
            if (t != null) goals[t.index] = other.id;
        }
        if (goals.Count == 0) return false;
        if (!WorldPaths.Find(map, start.index, c => goals.ContainsKey(c), t => WorldPaths.RoadCost(t, settings), out path, out _, 2000f)) return false;
        target = goals[path[path.Count - 1]];
        return true;
    }

    /// <summary>Cells of a planned road that are not road yet (what it costs).</summary>
    public static int NewRoadCells(WorldMap map, List<int> path) => path.Count(c => !map[c].road && map[c].settlement < 0);

    public static TradeRoute BuildRoad(WorldMap map, WorldGenSettings settings, SettlementRules rules, Settlement s, List<int> path, int target)
    {
        var route = new TradeRoute { id = map.Routes.Count == 0 ? 0 : map.Routes.Max(r => r.id) + 1, from = s.id, to = target };
        int since = 0;
        foreach (int c in path)
        {
            var t = map[c];
            route.cells.Add(t.coord);
            since++;
            bool gifted = t.nexus != null && t.settlement < 0;
            if (t.settlement < 0 && (gifted || since >= Math.Max(2, rules.nodeSpacing)))
            {
                route.nodes.Add(t.coord);
                since = 0;
            }
        }
        route.efficiency = Efficiency(map, route);
        map.Routes.Add(route);
        Rebuild(map, settings);
        return route;
    }

    /// <summary>A route's efficiency, 0-1: Coherence along it, plus a share for the stretches that run on leylines.</summary>
    public static float Efficiency(WorldMap map, TradeRoute route)
    {
        var cells = route.cells.Select(map.Get).Where(t => t != null).ToList();
        if (cells.Count == 0) return 0f;
        float coherence = cells.Average(t => t.coherence), onLeyline = cells.Count(t => t.leylines != 0) / (float)cells.Count;
        return Math.Max(0f, Math.Min(1f, 0.75f * coherence + 0.25f * onLeyline));
    }

    /// <summary>Recompute every route's efficiency (after an Age moved the leylines; the roads themselves stay).</summary>
    public static void RefreshEfficiency(WorldMap map)
    {
        foreach (var route in map.Routes) route.efficiency = Efficiency(map, route);
    }

    // ===== RESONANCE ANCHORS =====

    public static string WhyNotAnchor(Settlement s)
    {
        if (s == null) return "No settlement here.";
        if (s.anchor) return "A Resonance Anchor already stands here.";
        if (s.kind == SettlementKind.Capital) return "Anchors are raised at Outposts and settlements beyond the Capital.";
        return null;
    }

    /// <summary>Hand the standing Anchors to the magic and re-route the current Age (the leylines bend toward them).</summary>
    public static void SyncAnchors(WorldMap map, SettlementRules rules, bool reroute)
    {
        if (map.Magic == null) return;
        var cells = map.Settlements.Where(s => s.anchor).Select(s => map.Get(s.coord)).Where(t => t != null).Select(t => t.index);
        map.Magic.SetAnchors(cells, rules.anchorCoherence, rules.anchorRadius, rules.anchorPull);
        if (reroute) map.Magic.Reapply(map);
        RefreshEfficiency(map);
    }

    // ===== ENCLAVES =====

    public static string WhyNotEnvoy(WorldMap map, Enclave e)
    {
        if (e == null) return "No enclave here.";
        if (e.suzerain) return "You already hold its Suzerainty.";
        var t = map.Get(e.coord);
        if (t == null || !t.revealed) return "Find it first.";
        return null;
    }

    /// <summary>An envoy raises your standing; at 100 you hold the enclave's Suzerainty (it keeps its autonomy).</summary>
    public static void SendEnvoy(Enclave e, SettlementRules rules)
    {
        e.influence = Math.Min(100f, e.influence + Math.Max(0f, rules.envoyInfluence));
        if (e.influence >= 100f) e.suzerain = true;
    }

    // ===== GROWTH AND YIELDS =====

    /// <summary>
    /// City Development moves toward each settlement's potential (down at half the rate when above it). Outposts do
    /// not develop; a settlement cut off from your authority stops growing; an overstretched administration
    /// (<paramref name="efficiency"/> below 1, <see cref="WorldTerritory.Efficiency"/>) slows the growth. True when any
    /// whole point changed.
    /// </summary>
    public static bool Tick(WorldMap map, SettlementRules rules, int sevenths, float efficiency = 1f)
    {
        bool changed = false;
        var network = Networked(map);
        foreach (var s in map.Settlements)
        {
            if (s.kind == SettlementKind.Outpost) continue;
            var t = map.Get(s.coord);
            if (t == null) continue;
            if (!s.detached && t.authorityId != WorldAuthority.Player) continue;
            float target = CityDevelopment.Evaluate(map, rules, t.index, network.Contains(s.id)).Potential;
            float before = s.development, step = Math.Max(0f, rules.growthPerSeventh) * Math.Max(0, sevenths) * Math.Max(0f, Math.Min(1f, efficiency));
            s.development = s.development < target ? Math.Min(target, s.development + step) : Math.Max(target, s.development - step * 0.5f);
            if ((int)before != (int)s.development) changed = true;
        }
        return changed;
    }

    /// <summary>The grandfields being extracted: the Outpost (or settlement) with the richest cell on each field.</summary>
    public static List<(Grandfield field, Settlement by, float density)> Extractions(WorldMap map)
    {
        var result = new List<(Grandfield, Settlement, float)>();
        foreach (var field in map.Grandfields)
        {
            Settlement best = null;
            float density = 0f;
            foreach (var s in map.Settlements)
            {
                var t = map.Get(s.coord);
                if (t == null || t.grandfield != field.index || t.grandfieldDensity <= density) continue;
                best = s;
                density = t.grandfieldDensity;
            }
            if (best != null) result.Add((field, best, density));
        }
        return result;
    }

    /// <summary>What the civilization's map produces: settlements by City Development, bindings, extracted grandfields and suzerain enclaves.</summary>
    public static List<WorldYield> Yields(WorldMap map, WorldGenSettings settings, SettlementRules rules)
    {
        var yields = new List<WorldYield>();
        void Add(string source, IEnumerable<ResourceAmount> amounts, float scale, bool percent = false)
        {
            if (amounts == null) return;
            foreach (var a in amounts)
                if (a != null && !string.IsNullOrEmpty(a.resource) && a.amount != 0f && scale != 0f)
                    yields.Add(new WorldYield { source = source, resource = a.resource, amount = a.amount * scale, percent = percent });
        }
        foreach (var s in map.Settlements)
        {
            float tens = s.development / 10f;
            switch (s.kind)
            {
                case SettlementKind.Town: Add($"Settlement: {s.name}", rules.townYields, tens); break;
                case SettlementKind.Major:
                    Add($"Settlement: {s.name}", rules.majorYields, tens);
                    Add($"Binding: {s.name} ({s.binding})", rules.Binding(s.binding)?.yields, 1f);
                    break;
                case SettlementKind.Haven: Add($"Settlement: {s.name}", rules.havenYields, tens * (map.Get(s.coord)?.coherence ?? 0f)); break;
            }
        }
        var bonus = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, by, density) in Extractions(map))
        {
            var spec = settings.Grandfield(field.spec);
            if (spec == null || string.IsNullOrEmpty(field.resource)) continue;
            yields.Add(new WorldYield { source = $"Grandfield: {spec.name} ({by.name})", resource = field.resource, amount = spec.baseYield * density });
            bonus.TryGetValue(field.resource, out float sum);
            float add = Math.Min(rules.grandfieldBonusPercent * (0.5f + 0.5f * density), Math.Max(0f, rules.grandfieldCapPercent - sum));
            if (add <= 0f) continue;
            bonus[field.resource] = sum + add;
            yields.Add(new WorldYield { source = $"Grandfield extraction: {spec.name} ({by.name})", resource = field.resource, amount = add, percent = true });
        }
        foreach (var e in map.Enclaves.Where(e => e.suzerain))
            Add($"Suzerainty: {e.name}", settings.Enclave(e.spec)?.suzeraintyYields, 1f);
        return yields;
    }
}
