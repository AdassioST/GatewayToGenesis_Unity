using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Outskirt tributaries, the civilization's minor hubs, with no scene state (tested in <c>WorldTributaryTests</c>).
/// There are two ways to settle: expeditions escorting settlers found independent settlements, the major hubs
/// (Developing Towns inside your authority, Outposts beyond it, Religious Havens); a tributary is raised from home,
/// inside land you already hold, to serve a major hub within <see cref="TributaryRules.hubReach"/> cells (the
/// Capital's outskirt towns and fields). Opened by <see cref="TributaryRules.technology"/> (The Rekindling).
///
/// - It leads back to its hub: founded with a road to it (so it joins the Capital's roads when its hub does, and other
///   settlements may build their roads to it), it stands as a Trade Node, answers to its hub and develops no further
///   than <see cref="TributaryRules.aboveHub"/> past it. It forms no expeditions (a Militant District does), is never promoted,
///   raises no Anchor.
/// - It is a seat of territorial pull (<see cref="SeatKind.Tributary"/>): it brings the land around it into your
///   authority and adds Administrative Capacity, without being a full settlement.
/// - It develops toward 100 x its desirability (<see cref="WorldDesirability"/>), faster on desirable ground.
/// - Tributaries never stand next to another settlement (<see cref="TributaryRules.spacing"/>), but two within
///   <see cref="TributaryRules.linkDistance"/> are linked: their radii touch, so one meso hex stands for each whole
///   outskirt district, and linked districts count toward each other's adjacency.
/// - Every tributary begins as the generalist (<see cref="TributaryRules.generalist"/>, a housing hamlet) and can be
///   upgraded to one district (<see cref="DistrictSpec"/>), one for each of the ten Enclave categories of Enclave.md
///   (Agromagical, Militant, Auric, Weaver, Domestication, Trading, Industrious, Regal, Indulgent, Esoteric). A district's
///   effects scale with the tributary's development and x (1 + adjacency): what its linked districts are, what lies in
///   its area and the kindred enclaves nearby (<see cref="Adjacency"/>); the pillar districts raise their pillar
///   (<see cref="StatEffects"/>).
/// - When its hub is lost it rejoins the nearest hub (<see cref="Rehome"/>).
/// </summary>
public static class WorldTributaries
{
    public static TributaryRules RulesOf(WorldMap map) => map?.settlementRules?.tributaries ?? Default;

    private static readonly TributaryRules Default = new TributaryRules();

    // ===== WHO IS WHO =====

    /// <summary>A major hub that can keep tributaries: the Capital, a Major Settlement, a Developing Town or a Religious Haven inside your authority.</summary>
    public static bool IsHub(Settlement s) =>
        s != null && !s.detached && (s.kind == SettlementKind.Capital || s.kind == SettlementKind.Major || s.kind == SettlementKind.Town || s.kind == SettlementKind.Haven);

    public static bool IsTributary(Settlement s) => s != null && s.kind == SettlementKind.Tributary;

    /// <summary>The tributaries of <paramref name="hub"/>.</summary>
    public static IEnumerable<Settlement> Of(WorldMap map, Settlement hub) =>
        hub == null ? Enumerable.Empty<Settlement>() : map.Settlements.Where(s => IsTributary(s) && s.parent == hub.id);

    /// <summary>The hub a tributary serves (null for anything else, or when its hub is lost: gone, detached or no longer a hub).</summary>
    public static Settlement HubOf(WorldMap map, Settlement s)
    {
        if (!IsTributary(s)) return null;
        var hub = WorldCivilization.Get(map, s.parent);
        return IsHub(hub) ? hub : null;
    }

    /// <summary>Tributaries <paramref name="hub"/> keeps at its City Development now.</summary>
    public static int Slots(TributaryRules rules, Settlement hub) => IsHub(hub) ? rules.Slots(hub.kind, hub.development) : 0;

    public static int Count(WorldMap map) => map.Settlements.Count(IsTributary);

    /// <summary>What the next tributary costs, as a multiple of <see cref="TributaryRules.cost"/>.</summary>
    public static float CostScale(WorldMap map, TributaryRules rules) => 1f + Math.Max(0f, rules.costGrowth) * Count(map);

    // ===== PLACEMENT =====

    /// <summary>The hubs within reach of <paramref name="coord"/>, nearest first (with room or not).</summary>
    public static List<Settlement> Hubs(WorldMap map, TributaryRules rules, HexCoord coord) =>
        map.Settlements.Where(h => IsHub(h) && HexCoord.Distance(h.coord, coord) <= Math.Max(1, rules.hubReach))
            .OrderBy(h => HexCoord.Distance(h.coord, coord)).ThenBy(h => h.kind == SettlementKind.Capital ? 0 : h.kind == SettlementKind.Major ? 1 : 2).ThenBy(h => h.id).ToList();

    /// <summary>
    /// Why a tributary of <paramref name="hub"/> cannot be raised at <paramref name="coord"/>, or null: known, dry,
    /// passable land inside your Administrative Authority, nothing standing on it, no settlement or enclave within
    /// <see cref="TributaryRules.spacing"/> cells, its hub within reach and with a free place.
    /// </summary>
    public static string WhyNotFound(WorldMap map, WorldGenSettings settings, TributaryRules rules, HexCoord coord, Settlement hub)
    {
        var t = map.Get(coord);
        if (t == null) return "Beyond the edge of the world.";
        if (t.water) return "Tributaries stand on dry land.";
        if (t.impassable || float.IsPositiveInfinity(WorldPaths.StepCost(t, settings))) return "No one can live on this ground.";
        if (t.authorityId != WorldAuthority.Player) return "Tributaries stand inside your Administrative Authority: beyond it, send settlers to found an Outpost or a town.";
        if (!t.known) return "No one knows this ground yet.";
        if (t.settlement >= 0 || t.enclave >= 0) return "Something already stands here.";
        int spacing = Math.Max(1, rules.spacing);
        var near = map.Settlements.Where(s => HexCoord.Distance(s.coord, coord) < spacing).OrderBy(s => HexCoord.Distance(s.coord, coord)).FirstOrDefault();
        if (near != null) return $"Right beside {near.name}: tributaries never stand next to another settlement ({spacing} cells apart; their districts may touch).";
        if (map.Enclaves.Any(e => HexCoord.Distance(e.coord, coord) < spacing)) return "Right beside an enclave.";
        if (hub == null) return Hubs(map, rules, coord).Count == 0 ? $"No hub of yours within {rules.hubReach} cells: tributaries serve the Capital, a Major Settlement, a town or a haven nearby." : "Choose the hub it will serve.";
        if (!IsHub(hub)) return "Only the Capital, Major Settlements, Developing Towns and Religious Havens keep tributaries.";
        if (HexCoord.Distance(hub.coord, coord) > rules.hubReach) return $"Too far from {hub.name} (within {rules.hubReach} cells).";
        int slots = Slots(rules, hub), used = Of(map, hub).Count();
        if (used >= slots) return $"{hub.name} keeps all the tributaries it can ({used}/{slots}): raise its City Development (one more every {rules.developmentPerSlot:0}).";
        return null;
    }

    /// <summary>
    /// Raise a tributary of <paramref name="hub"/> at <paramref name="coord"/> (check <see cref="WhyNotFound"/> first):
    /// a generalist, its area explored, joined to its hub by a road.
    /// </summary>
    public static Settlement Found(WorldMap map, WorldGenSettings settings, SettlementRules rules, HexCoord coord, Settlement hub, int age)
    {
        var tr = rules.tributaries ?? Default;
        var t = map.Get(coord);
        var s = new Settlement
        {
            id = map.Settlements.Count == 0 ? 0 : map.Settlements.Max(x => x.id) + 1,
            name = NameFor(map, hub),
            kind = SettlementKind.Tributary,
            coord = coord,
            foundedAge = age,
            parent = hub.id,
            district = tr.generalist,
            development = Math.Max(0f, tr.startDevelopment),
            authorityRadius = Math.Max(0, tr.authorityRadius),
        };
        map.Settlements.Add(s);
        map.ExploreAround(coord, Math.Max(0, tr.adjacencyRadius), Math.Max(1, tr.adjacencyRadius + 1), id => settings.Terrain(id)?.passable != false);
        WorldCivilization.Rebuild(map, settings);
        // The road home: the way to its hub, over the cheapest ground (reusing roads where they run).
        var hubTile = map.Get(hub.coord);
        if (hubTile != null && WorldPaths.Find(map, t.index, c => c == hubTile.index, x => WorldPaths.RoadCost(x, settings), out var path, out _, 500f))
            WorldCivilization.BuildRoad(map, settings, rules, s, path, hub.id);
        WorldSites.RecomputeDanger(map);
        return s;
    }

    // ===== ORPHANS =====

    /// <summary>
    /// Tributaries whose hub is lost rejoin the nearest hub (<see cref="NearestHub"/>), keeping their district and
    /// development; a road is laid to the new hub, free, when none joins them yet. Returns what moved. Called at the start
    /// of <see cref="WorldCivilization.Rebuild"/>, so the map never holds an orphan while a hub stands.
    /// </summary>
    public static List<(Settlement tributary, Settlement hub)> Rehome(WorldMap map, WorldGenSettings settings, SettlementRules settlementRules)
    {
        var moved = new List<(Settlement, Settlement)>();
        var rules = settlementRules?.tributaries ?? RulesOf(map);
        foreach (var s in map.Settlements.Where(IsTributary).ToList())
        {
            if (HubOf(map, s) != null) continue;
            var hub = NearestHub(map, rules, s);
            if (hub == null) continue;
            s.parent = hub.id;
            moved.Add((s, hub));
            if (Joined(map, s.id, hub.id)) continue;
            var from = map.Get(s.coord);
            var to = map.Get(hub.coord);
            if (from != null && to != null && WorldPaths.Find(map, from.index, c => c == to.index, x => WorldPaths.RoadCost(x, settings), out var path, out _, 2000f))
                WorldCivilization.BuildRoad(map, settings, settlementRules ?? new SettlementRules(), s, path, hub.id, rebuild: false);
        }
        return moved;
    }

    /// <summary>
    /// The hub an orphaned tributary rejoins: the nearest within <see cref="TributaryRules.hubReach"/> with a free place,
    /// else the nearest within reach, else the nearest anywhere (the Capital first on a tie). Null when no hub stands.
    /// </summary>
    public static Settlement NearestHub(WorldMap map, TributaryRules rules, Settlement s)
    {
        var hubs = map.Settlements.Where(h => h != s && IsHub(h)).OrderBy(h => HexCoord.Distance(h.coord, s.coord))
            .ThenBy(h => h.kind == SettlementKind.Capital ? 0 : h.kind == SettlementKind.Major ? 1 : 2).ThenBy(h => h.id).ToList();
        int reach = Math.Max(1, rules.hubReach);
        bool InReach(Settlement h) => HexCoord.Distance(h.coord, s.coord) <= reach;
        return hubs.FirstOrDefault(h => InReach(h) && Of(map, h).Count(o => o != s) < Slots(rules, h)) ?? hubs.FirstOrDefault(InReach) ?? hubs.FirstOrDefault();
    }

    /// <summary>Two settlements joined by roads (directly or through others).</summary>
    public static bool Joined(WorldMap map, int a, int b)
    {
        var reached = new HashSet<int> { a };
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (var route in map.Routes)
            {
                if (reached.Contains(route.from) && reached.Add(route.to)) grew = true;
                if (reached.Contains(route.to) && reached.Add(route.from)) grew = true;
            }
        }
        return reached.Contains(b);
    }

    private static string NameFor(WorldMap map, Settlement hub)
    {
        string stem = hub.kind == SettlementKind.Capital ? "Capital" : hub.name;
        for (int n = Of(map, hub).Count() + 1; ; n++)
        {
            string name = $"{stem} Outskirts {AgeRules.Roman(n)}";
            if (map.Settlements.All(s => s.name != name)) return name;
        }
    }

    // ===== DISTRICTS =====

    /// <summary>A tributary's district (the generalist when unset or unknown), or null for anything else.</summary>
    public static DistrictSpec DistrictOf(TributaryRules rules, Settlement s) =>
        !IsTributary(s) ? null : rules.District(string.IsNullOrEmpty(s.district) ? rules.generalist : s.district) ?? rules.District(rules.generalist);

    /// <summary>A district's name with its article ("an Auric District", "a Militant District").</summary>
    public static string Article(string name) =>
        string.IsNullOrEmpty(name) ? name : ("AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    public static bool IsGeneralist(TributaryRules rules, Settlement s) =>
        IsTributary(s) && (string.IsNullOrEmpty(s.district) || string.Equals(s.district, rules.generalist, StringComparison.OrdinalIgnoreCase));

    /// <summary>What upgrading costs, as a multiple of the district's cost: once from the generalist, dearer after that (a return to the generalist is free).</summary>
    public static float SpecializeCostScale(TributaryRules rules, Settlement s, string district) =>
        string.Equals(district, rules.generalist, StringComparison.OrdinalIgnoreCase) ? 0f : IsGeneralist(rules, s) ? 1f : Math.Max(0f, rules.respecializeCost);

    /// <summary>Why <paramref name="s"/> cannot become <paramref name="district"/> now, or null (costs aside).</summary>
    public static string WhyNotSpecialize(TributaryRules rules, Settlement s, string district)
    {
        if (!IsTributary(s)) return "Only tributaries are turned into districts.";
        var spec = rules.District(district);
        if (spec == null) return "No such district.";
        if (string.Equals(DistrictOf(rules, s)?.id, spec.id, StringComparison.OrdinalIgnoreCase)) return $"It is {Article(spec.name)} already.";
        if (s.development < spec.minDevelopment) return $"{Capitalize(Article(spec.name))} needs development {spec.minDevelopment:0} ({s.development:0} now).";
        return null;
    }

    /// <summary>
    /// Turn <paramref name="s"/> into <paramref name="district"/> (check <see cref="WhyNotSpecialize"/> first). Leaving an
    /// upgraded district rebuilds the quarter: it keeps <see cref="TributaryRules.respecializeKeeps"/> of its development.
    /// </summary>
    public static void Specialize(WorldMap map, WorldGenSettings settings, TributaryRules rules, Settlement s, string district)
    {
        if (!IsGeneralist(rules, s)) s.development *= Math.Max(0f, Math.Min(1f, rules.respecializeKeeps));
        s.district = rules.District(district)?.id ?? district;
        WorldCivilization.Rebuild(map, settings);
        WorldSites.RecomputeDanger(map);
    }

    // ===== ADJACENCY =====

    /// <summary>The other tributaries linked to <paramref name="s"/> (within <see cref="TributaryRules.linkDistance"/>: their radii touch).</summary>
    public static IEnumerable<Settlement> Linked(WorldMap map, TributaryRules rules, Settlement s) =>
        map.Settlements.Where(o => o != s && IsTributary(o) && HexCoord.Distance(o.coord, s.coord) <= Math.Max(1, rules.linkDistance));

    /// <summary>What adds to (or takes from) a tributary's district effect, as shares: each adjacency that reads something.</summary>
    public static List<(string what, float bonus)> Adjacency(WorldMap map, TributaryRules rules, Settlement s) => Adjacency(map, rules, s, DistrictOf(rules, s));

    /// <summary>The adjacency <paramref name="s"/> would have as <paramref name="spec"/> (a preview before upgrading).</summary>
    public static List<(string what, float bonus)> Adjacency(WorldMap map, TributaryRules rules, Settlement s, DistrictSpec spec)
    {
        var result = new List<(string, float)>();
        if (s == null || spec?.adjacency == null || map.Get(s.coord) == null) return result;
        var area = HexCoord.Spiral(s.coord, Math.Max(0, rules.adjacencyRadius)).Select(map.Get).Where(c => c != null).ToList();
        var land = area.Where(c => !c.water && !c.impassable).ToList();
        foreach (var rule in spec.adjacency)
        {
            if (rule == null || rule.bonus == 0f) continue;
            var (what, m) = Measure(map, rules, s, spec, rule, area, land);
            if (Math.Abs(m) < 0.005f) continue;
            result.Add((what, rule.bonus * m));
        }
        return result;
    }

    // How much of an adjacency's source a tributary has, and how to name it.
    private static (string what, float m) Measure(WorldMap map, TributaryRules rules, Settlement s, DistrictSpec spec, AdjacencyRule rule, List<WorldTile> area, List<WorldTile> land)
    {
        float Avg(Func<WorldTile, float> f) => land.Count > 0 ? land.Average(f) : 0f;
        float Any(Func<WorldTile, bool> f) => area.Any(f) ? 1f : 0f;
        switch (rule.source)
        {
            case AdjacencySource.District:
                var linked = Linked(map, rules, s).Where(o => string.IsNullOrEmpty(rule.district) || string.Equals(DistrictOf(rules, o)?.id, rule.district, StringComparison.OrdinalIgnoreCase)).ToList();
                string name = string.IsNullOrEmpty(rule.district) ? "linked tributaries" : $"linked {rules.District(rule.district)?.name ?? rule.district}";
                return ($"{name} x{linked.Count}", linked.Count);
            case AdjacencySource.Hub:
                var hub = map.Settlements.Where(h => IsHub(h) && HexCoord.Distance(h.coord, s.coord) <= Math.Max(1, rules.linkDistance)).OrderBy(h => HexCoord.Distance(h.coord, s.coord)).FirstOrDefault();
                return (hub != null ? $"beside {hub.name}" : "a hub", hub != null ? 1f : 0f);
            case AdjacencySource.Freshwater: return ("freshwater", Any(c => c.river || c.lake));
            case AdjacencySource.Coast: return ("the coast", Any(c => c.water && !c.lake));
            case AdjacencySource.Leyline: return ("a leyline", Any(c => c.leylines != 0));
            case AdjacencySource.Junction: return ("a leyline junction", Any(c => c.junction >= 2));
            case AdjacencySource.Coherence: { float v = Avg(c => c.coherence); return ($"Coherence {v:P0}", v); }
            case AdjacencySource.MagicalFertility: { float v = Avg(c => c.magicFertility); return ($"magical fertility {v:P0}", v); }
            case AdjacencySource.LandFertility: { float v = Avg(c => c.landFertility); return ($"land fertility {v:P0}", v); }
            case AdjacencySource.Beauty: { float v = Avg(c => c.beauty); return ($"beauty {v:+0.00;-0.00;0}", v); }
            case AdjacencySource.HighGround: { int n = area.Count(c => c.impassable || (!c.water && (c.elevation >= 0.6f || c.escarpment >= 0.06f))); return ($"high ground x{n}", n); }
            case AdjacencySource.Cover: { int n = area.Count(c => !string.IsNullOrEmpty(c.cover)); return ($"cover x{n}", n); }
            case AdjacencySource.Grandfield: return ("a grandfield", Any(c => c.grandfield >= 0));
            case AdjacencySource.ResourceSite:
                {
                    int n = area.Select(c => WorldResources.SiteAt(map, c)).Where(site => site != null && WorldResources.Identified(map, site)).Distinct().Count();
                    return ($"resource sites x{n}", n);
                }
            case AdjacencySource.Road:
                // Another road than its own way home.
                var own = new HashSet<HexCoord>(map.Routes.Where(r => r.from == s.id).SelectMany(r => r.cells));
                var others = new HashSet<HexCoord>(map.Routes.Where(r => r.from != s.id).SelectMany(r => r.cells));
                return ("a road", area.Any(c => others.Contains(c.coord) || (c.road && !own.Contains(c.coord))) ? 1f : 0f);
            case AdjacencySource.TradeNexus: return ("a Trade Nexus", Any(c => c.nexus != null));
            case AdjacencySource.Sacred: return ("Sacred ground", Any(c => c.sacred));
            case AdjacencySource.Danger:
                {
                    // A watch thrives on the threat itself; what suffers from danger reads what is left after the watch (a
                    // Militant District beside the fields shields them).
                    float v = area.Count == 0 ? 0f : rule.bonus > 0f ? area.Max(c => WorldSites.ThreatDanger(map, c.coord)) : area.Max(c => c.danger);
                    return ($"danger {v:P0}", v);
                }
            case AdjacencySource.Dissonance: { float v = Avg(c => c.dissonance); return ($"dissonance {v:P0}", v); }
            case AdjacencySource.Enclave:
                {
                    // Its kindred enclave (or the named category, or any), within reach; held in Suzerainty it counts twice.
                    string family = string.IsNullOrEmpty(rule.district) ? spec?.enclave : rule.district;
                    bool any = string.Equals(family, "any", StringComparison.OrdinalIgnoreCase);
                    if (!any && string.IsNullOrEmpty(family)) return ("an enclave", 0f);
                    int reach = Math.Max(0, rules.enclaveReach), n = 0;
                    foreach (var e in map.Enclaves)
                        if (HexCoord.Distance(e.coord, s.coord) <= reach && (any || string.Equals(e.family.ToString(), family, StringComparison.OrdinalIgnoreCase)))
                            n += e.suzerain ? 2 : 1;
                    return (any ? $"enclaves within reach x{n}" : $"kindred {family} enclave x{n}", n);
                }
            default: return (rule.source.ToString(), 0f);
        }
    }

    /// <summary>A tributary's adjacency, within <see cref="TributaryRules.minAdjacency"/> and <see cref="TributaryRules.maxAdjacency"/>.</summary>
    public static float AdjacencyTotal(WorldMap map, TributaryRules rules, Settlement s) => AdjacencyTotal(map, rules, s, DistrictOf(rules, s));

    public static float AdjacencyTotal(WorldMap map, TributaryRules rules, Settlement s, DistrictSpec spec) =>
        Math.Max(rules.minAdjacency, Math.Min(rules.maxAdjacency, Adjacency(map, rules, s, spec).Sum(a => a.bonus)));

    /// <summary>What its district's effects are multiplied by: 1 + adjacency, less while its Composure is shaken (<see cref="WorldRuins.Output"/>).</summary>
    public static float Scale(WorldMap map, TributaryRules rules, Settlement s) => Math.Max(0f, 1f + AdjacencyTotal(map, rules, s)) * WorldRuins.Output(WorldRuins.RulesOf(map), s);

    // ===== GROWTH =====

    /// <summary>The development a tributary grows toward: 100 x its desirability, never more than <see cref="TributaryRules.aboveHub"/> past its hub.</summary>
    public static float Target(WorldMap map, TributaryRules rules, Settlement s)
    {
        var t = map.Get(s.coord);
        if (t == null) return 0f;
        float target = 100f * WorldDesirability.Of(map, t);
        var hub = HubOf(map, s);
        if (hub != null) target = Math.Min(target, hub.development + Math.Max(0f, rules.aboveHub));
        return Math.Max(0f, Math.Min(100f, target));
    }

    // ===== EFFECTS =====

    /// <summary>City Development a hub gains from its tributaries (each lends its district's share, fully from 40 development), capped.</summary>
    public static float HubDevelopment(WorldMap map, TributaryRules rules, Settlement hub)
    {
        if (!IsHub(hub)) return 0f;
        float sum = 0f;
        foreach (var s in Of(map, hub))
        {
            var spec = DistrictOf(rules, s);
            if (spec == null || spec.hubDevelopment == 0f) continue;
            sum += spec.hubDevelopment * Scale(map, rules, s) * Math.Min(1f, Math.Max(0f, s.development) / 40f);
        }
        return Math.Min(Math.Max(0f, rules.hubDevelopmentCap), sum);
    }

    /// <summary>Housing the tributaries give the Capital's people (each district's per 10 development, x its adjacency).</summary>
    public static int Housing(WorldMap map, TributaryRules rules)
    {
        float sum = 0f;
        foreach (var s in map.Settlements.Where(IsTributary))
        {
            var spec = DistrictOf(rules, s);
            if (spec != null && spec.housing > 0f) sum += spec.housing * Math.Max(0f, s.development) / 10f * Scale(map, rules, s);
        }
        return (int)Math.Floor(sum + 1e-4f);
    }

    /// <summary>Expedition slots the districts add.</summary>
    public static int ExpeditionSlots(WorldMap map, TributaryRules rules)
    {
        int slots = 0;
        foreach (var s in map.Settlements.Where(IsTributary))
        {
            var spec = DistrictOf(rules, s);
            if (spec != null && spec.expeditionSlots > 0) slots += (int)Math.Floor(spec.expeditionSlots * Scale(map, rules, s) + 1e-4f);
        }
        return slots;
    }

    /// <summary>Expeditions may form and take on companions at <paramref name="s"/> (a district that outfits them).</summary>
    public static bool Outfits(TributaryRules rules, Settlement s) => DistrictOf(rules, s)?.outfits == true;

    /// <summary>What the tributaries produce per second: each district's yields per 10 development, x its adjacency.</summary>
    public static IEnumerable<WorldYield> Yields(WorldMap map, TributaryRules rules)
    {
        foreach (var s in map.Settlements.Where(IsTributary))
        {
            var spec = DistrictOf(rules, s);
            if (spec?.yields == null || spec.yields.Count == 0) continue;
            float scale = Math.Max(0f, s.development) / 10f * Scale(map, rules, s);
            foreach (var a in spec.yields)
                if (a != null && !string.IsNullOrEmpty(a.resource) && a.amount != 0f && scale > 0f)
                    yield return new WorldYield { source = $"Tributary: {s.name} ({spec.name})", resource = a.resource, amount = a.amount * scale };
        }
    }

    /// <summary>Its seat's pull, as a multiple of the tributary seat's (a Militant District's walls pull harder).</summary>
    public static float PullScale(WorldMap map, TributaryRules rules, Settlement s)
    {
        var spec = DistrictOf(rules, s);
        return spec == null ? 1f : 1f + Math.Max(0f, spec.pull) * Scale(map, rules, s);
    }

    /// <summary>Administrative Capacity its district adds on top of its seat's.</summary>
    public static float Capacity(WorldMap map, TributaryRules rules, Settlement s)
    {
        var spec = DistrictOf(rules, s);
        return spec == null ? 0f : spec.capacity * Scale(map, rules, s);
    }

    /// <summary>Share of danger a tributary wards off in its radius (0 unless its district keeps watch), at most 0.9.</summary>
    public static float WardOf(WorldMap map, TributaryRules rules, Settlement s)
    {
        var spec = DistrictOf(rules, s);
        return spec == null || spec.ward <= 0f ? 0f : Math.Min(0.9f, spec.ward * Scale(map, rules, s));
    }

    /// <summary>The pillars, substats and morale <paramref name="s"/> raises now: whole points per 50 development x (1 + adjacency), rounded down.</summary>
    public static List<(string stat, int amount)> Stats(WorldMap map, TributaryRules rules, Settlement s)
    {
        var result = new List<(string, int)>();
        var spec = DistrictOf(rules, s);
        if (spec?.stats == null || spec.stats.Count == 0) return result;
        float fifties = Math.Max(0f, s.development) / 50f * Scale(map, rules, s);
        foreach (var st in spec.stats)
        {
            if (st == null || string.IsNullOrEmpty(st.stat)) continue;
            int n = (int)Math.Floor(st.perFifty * fifties + 1e-4f);
            if (n != 0) result.Add((st.stat, n));
        }
        return result;
    }

    /// <summary>The effect type that raises <paramref name="stat"/>: a pillar, a substat or morale (null for anything else).</summary>
    public static GameEffectType? StatEffectType(string stat)
    {
        if (string.Equals(stat, StatDefinitions.Morale, StringComparison.OrdinalIgnoreCase)) return GameEffectType.MoraleModifier;
        switch (StatDefinitions.KindOf(stat))
        {
            case StatDefinitions.StatKind.Pillar: return GameEffectType.PillarBonus;
            case StatDefinitions.StatKind.Substat: return GameEffectType.SubstatBonus;
            default: return null;
        }
    }

    /// <summary>Every district's stat effects, by source ("District: name (district)"), for the effect router.</summary>
    public static IEnumerable<(string source, GameEffect effect)> StatEffects(WorldMap map, TributaryRules rules)
    {
        foreach (var s in map.Settlements.Where(IsTributary))
        {
            var spec = DistrictOf(rules, s);
            foreach (var (stat, amount) in Stats(map, rules, s))
            {
                var type = StatEffectType(stat);
                if (type.HasValue) yield return ($"District: {s.name} ({spec.name})", new GameEffect(type.Value, amount, ModifierType.Add, stat.ToLowerInvariant()));
            }
        }
    }

    /// <summary>What a tributary gives now, in words (its card): each effect of its district at its development and adjacency.</summary>
    public static List<string> EffectLines(WorldMap map, TributaryRules rules, Settlement s)
    {
        var lines = new List<string>();
        var spec = DistrictOf(rules, s);
        if (spec == null) return lines;
        float scale = Scale(map, rules, s), tens = Math.Max(0f, s.development) / 10f;
        foreach (var a in spec.yields ?? new List<ResourceAmount>())
            if (a != null && !string.IsNullOrEmpty(a.resource) && a.amount != 0f) lines.Add($"{a.amount * tens * scale:+0.###;-0.###} {a.resource}/s");
        foreach (var st in spec.stats ?? new List<DistrictStat>())
        {
            if (st == null || string.IsNullOrEmpty(st.stat)) continue;
            int n = Stats(map, rules, s).Where(x => x.stat == st.stat).Sum(x => x.amount);
            lines.Add(n != 0 ? $"+{n} {st.stat}" : $"{st.stat} at {Math.Ceiling(50f / Math.Max(0.01f, st.perFifty * scale)):0} development");
        }
        if (spec.housing > 0f) lines.Add($"+{spec.housing * tens * scale:0.#} housing");
        if (spec.hubDevelopment != 0f) lines.Add($"+{spec.hubDevelopment * scale * Math.Min(1f, Math.Max(0f, s.development) / 40f):0.#} City Development to its hub");
        if (spec.capacity != 0f) lines.Add($"+{Capacity(map, rules, s):0.#} Administrative Capacity");
        if (spec.pull > 0f) lines.Add($"pulls land x{PullScale(map, rules, s):0.00}");
        if (spec.ward > 0f) lines.Add($"wards off {WardOf(map, rules, s):P0} of the danger within {spec.wardRadius} cells{(spec.holdsLand ? ", and that land never slips away" : string.Empty)}");
        if (spec.expeditionSlots > 0)
        {
            int slots = (int)Math.Floor(spec.expeditionSlots * scale + 1e-4f);
            lines.Add(slots == 1 ? "+1 expedition slot" : $"+{slots} expedition slots");
        }
        if (spec.outfits) lines.Add("outfits expeditions");
        return lines;
    }

    /// <summary>Lower the danger around every district that keeps watch (called by <see cref="WorldSites.RecomputeDanger"/>).</summary>
    public static void Ward(WorldMap map)
    {
        var rules = RulesOf(map);
        foreach (var s in map.Settlements.Where(IsTributary).ToList())
        {
            float ward = WardOf(map, rules, s);
            if (ward <= 0f) continue;
            foreach (var coord in HexCoord.Spiral(s.coord, Math.Max(0, DistrictOf(rules, s).wardRadius)))
            {
                var t = map.Get(coord);
                if (t != null) t.danger *= 1f - ward;
            }
        }
    }

    /// <summary>Cells within the watch of a district that holds its land: they never slip back into the wilderness.</summary>
    public static HashSet<int> HeldFast(WorldMap map)
    {
        var rules = RulesOf(map);
        var cells = new HashSet<int>();
        foreach (var s in map.Settlements.Where(IsTributary))
        {
            var spec = DistrictOf(rules, s);
            if (spec == null || !spec.holdsLand) continue;
            foreach (var coord in HexCoord.Spiral(s.coord, Math.Max(0, spec.wardRadius)))
            {
                var t = map.Get(coord);
                if (t != null) cells.Add(t.index);
            }
        }
        return cells;
    }
}
