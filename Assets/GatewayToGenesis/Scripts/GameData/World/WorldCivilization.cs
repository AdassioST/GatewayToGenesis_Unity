using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The settlement categories of Arcanoria.md (Map Features, Settlements), plus the Religious Haven of Places and the
/// outskirt Tributary (a minor hub serving one of the others: <see cref="WorldTributaries"/>).
/// </summary>
public enum SettlementKind { Capital, Town, Major, Outpost, Haven, Tributary }

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
    /// <summary>A tributary's hub: the id of the settlement it serves (unused by other kinds).</summary>
    [SaveOptionalField] public int parent;
    /// <summary>A tributary's district (<see cref="DistrictSpec.id"/>; null or empty: the generalist).</summary>
    [SaveOptionalField] public string district;
    /// <summary>
    /// Its Composure's strain, 0-100, read through the legends' five states (<see cref="WorldRuins.StateOf"/>): at
    /// Surrender it falls into a ruin (the Capital never does).
    /// </summary>
    [SaveOptionalField] public float strain;
    /// <summary>What strained it last ("danger", "withering", "pillage"), for its card and its ruin.</summary>
    [SaveOptionalField] public string harmedBy;
    /// <summary>What its people feel this moment (<see cref="WorldSuffering.SettlementFeelings"/>): given off into its cell every Seventh, what its blooms catalogue.</summary>
    [SaveOptionalField] public EmotionalRegister feelings = new EmotionalRegister();
    /// <summary>Its cell's imprint was told heavy (<see cref="WorldSuffering.WarnImprint"/>): told again only after it eases.</summary>
    [SaveOptionalField] public bool imprintWarned;
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
    /// <summary>Its cells that were Old World road restored cheaply (<see cref="OldWorldRules"/>): a lesser road until rebuilt.</summary>
    [SaveOptionalField] public List<HexCoord> restored = new List<HexCoord>();
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
    /// <summary>A Domestication Enclave's kept species at the last Echo (<see cref="WorldEnclaveEcology.Echo"/>: what began or ceased to be kept is told).</summary>
    [SaveOptionalField] public List<string> kept = new List<string>();
    /// <summary>The Phase it last served you (<see cref="WorldEnclaveEcology.Commission"/>; -1 never): once a Phase.</summary>
    [SaveOptionalField] public int commissionedPhase = -1;
    /// <summary>The Echo count (<see cref="WorldSystem.EchoesGrown"/>) until which its moth farms stay restricted (<see cref="WorldGreatPlague.Restrict"/>).</summary>
    [SaveOptionalField] public int restrictedUntil;

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
/// - Tributaries: minor hubs raised inside your authority to serve one of the settlements above (their districts,
///   adjacency and effects live in <see cref="WorldTributaries"/>); every settlement grows faster on desirable ground
///   (<see cref="WorldDesirability"/>).
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
            case SettlementKind.Tributary: return "Tributary";
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
        // A tributary whose hub was lost rejoins the nearest hub first (and may lay a road to it).
        if (map.rehomed == null) map.rehomed = new List<(int, int)>();
        foreach (var (tributary, hub) in WorldTributaries.Rehome(map, settings, map.settlementRules))
            map.rehomed.Add((tributary.id, hub.id));
        foreach (var t in map.Tiles)
        {
            t.settlement = -1;
            t.enclave = -1;
            t.road = false;
            t.restoredRoad = false;
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
        // A cell is only a restored Old World stretch while no route has it rebuilt.
        var rebuilt = new HashSet<HexCoord>();
        foreach (var route in map.Routes)
        {
            var restored = new HashSet<HexCoord>(route.restored ?? new List<HexCoord>());
            foreach (var c in route.cells)
            {
                var t = map.Get(c);
                if (t == null) continue;
                t.road = true;
                if (restored.Contains(c)) t.restoredRoad = true;
                else rebuilt.Add(c);
            }
            foreach (var c in route.nodes) { var t = map.Get(c); if (t != null) t.tradeNode = true; }
        }
        foreach (var c in rebuilt) { var t = map.Get(c); if (t != null) t.restoredRoad = false; }
        // A tributary is a node of its hub's roads.
        foreach (var s in map.Settlements.Where(WorldTributaries.IsTributary))
        {
            var t = map.Get(s.coord);
            if (t != null) t.tradeNode = true;
        }
        // Resource sites first: they lend or take beauty, Coherence and fertility around them.
        WorldResources.Refresh(map, settings);
        WorldBeauty.Refresh(map, settings);
        WorldAuthority.Establish(map, settings);
        var territory = WorldTerritory.Compute(map, settings);
        // A town answers to the Major Settlement whose pull reaches it strongest (its secondary zone of control), else the Capital.
        var capital = Capital(map);
        foreach (var s in map.Settlements)
        {
            s.governedBy = -1;
            if (s.kind == SettlementKind.Capital || s.kind == SettlementKind.Major || s.detached) continue;
            // A tributary answers to the hub it serves.
            if (s.kind == SettlementKind.Tributary)
            {
                s.governedBy = WorldTributaries.HubOf(map, s)?.id ?? capital?.id ?? -1;
                continue;
            }
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
    public static DevelopmentBreakdown Breakdown(WorldMap map, SettlementRules rules, Settlement s) => Evaluate(map, rules, s, Networked(map).Contains(s.id));

    // A settlement's City Development: its ground, plus what its outskirt tributaries lend a hub.
    private static DevelopmentBreakdown Evaluate(WorldMap map, SettlementRules rules, Settlement s, bool networked)
    {
        var t = map.Get(s.coord);
        if (t == null) return new DevelopmentBreakdown();
        var b = CityDevelopment.Evaluate(map, rules, t.index, networked);
        float outskirts = WorldTributaries.HubDevelopment(map, rules.tributaries ?? WorldTributaries.RulesOf(map), s);
        if (outskirts >= 0.005f)
        {
            b.terms.Add((DevelopmentTerm.Outskirts, outskirts));
            b.raw += outskirts;
        }
        return b;
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
        if (kind == SettlementKind.Tributary) return "Tributaries are raised from home, inside your authority, for a hub nearby.";
        if (t.water) return "Settlements stand on dry land.";
        if (float.IsPositiveInfinity(WorldPaths.StepCost(t, settings))) return "No settlement can stand on this ground.";
        if (kind == SettlementKind.Outpost ? !t.known : !t.explored) return kind == SettlementKind.Outpost ? "A scout must pass over it first." : "Survey it first.";
        if (t.settlement >= 0 || t.enclave >= 0) return "Something already stands here.";
        int spacing = kind == SettlementKind.Outpost ? Math.Max(1, rules.spacing / 2) : Math.Max(1, rules.spacing);
        // Tributaries are minor hubs: an independent settlement keeps only their own spacing from them.
        int tributarySpacing = Math.Min(spacing, Math.Max(1, rules.tributaries?.spacing ?? 2));
        var near = map.Settlements.Where(s => HexCoord.Distance(s.coord, coord) < (s.kind == SettlementKind.Tributary ? tributarySpacing : spacing)).OrderBy(s => HexCoord.Distance(s.coord, coord)).FirstOrDefault();
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

    /// <summary>Lay a road along <paramref name="path"/> from <paramref name="s"/> to <paramref name="target"/>; <paramref name="rebuild"/> false
    /// leaves the derived marks to a rebuild already under way (<see cref="WorldTributaries.Rehome"/>).</summary>
    public static TradeRoute BuildRoad(WorldMap map, WorldGenSettings settings, SettlementRules rules, Settlement s, List<int> path, int target, bool rebuild = true)
    {
        var route = new TradeRoute { id = map.Routes.Count == 0 ? 0 : map.Routes.Max(r => r.id) + 1, from = s.id, to = target };
        int since = 0;
        foreach (int c in path)
        {
            var t = map[c];
            route.cells.Add(t.coord);
            // Old World road not yet under a road of yours is restored, cheaply: a lesser road until rebuilt, with no Trade Node.
            bool restored = t.oldRoad && !t.road && t.settlement < 0;
            if (restored) route.restored.Add(t.coord);
            since++;
            bool gifted = t.nexus != null && t.settlement < 0;
            if (t.settlement < 0 && !restored && (gifted || since >= Math.Max(2, rules.nodeSpacing)))
            {
                route.nodes.Add(t.coord);
                since = 0;
            }
        }
        route.efficiency = Efficiency(map, route, rules?.loss?.oldWorld);
        map.Routes.Add(route);
        if (rebuild) Rebuild(map, settings);
        return route;
    }

    /// <summary>
    /// A route's efficiency, 0-1: Coherence along it, plus a share for the stretches that run on leylines; its restored
    /// Old World stretches count only <see cref="OldWorldRules.restoredWorth"/> until rebuilt.
    /// </summary>
    public static float Efficiency(WorldMap map, TradeRoute route, OldWorldRules oldWorld = null)
    {
        var cells = route.cells.Select(map.Get).Where(t => t != null).ToList();
        if (cells.Count == 0) return 0f;
        float coherence = cells.Average(t => t.coherence), onLeyline = cells.Count(t => t.leylines != 0) / (float)cells.Count;
        float restored = Math.Min(1f, WorldRuins.RestoredCount(route) / (float)cells.Count);
        float worth = 1f - restored * (1f - Math.Max(0f, Math.Min(1f, (oldWorld ?? map.settlementRules?.loss?.oldWorld ?? new OldWorldRules()).restoredWorth)));
        return Math.Max(0f, Math.Min(1f, (0.75f * coherence + 0.25f * onLeyline) * worth));
    }

    /// <summary>Recompute every route's efficiency (after an Age moved the leylines; the roads themselves stay).</summary>
    public static void RefreshEfficiency(WorldMap map)
    {
        foreach (var route in map.Routes) route.efficiency = Efficiency(map, route);
    }

    /// <summary>Rebuild a route's restored Old World stretches into full road (pay <see cref="WorldRuins.RebuildScale"/> first).</summary>
    public static void RebuildRestored(WorldMap map, WorldGenSettings settings, TradeRoute route)
    {
        if (route == null || WorldRuins.RestoredCount(route) == 0) return;
        route.restored.Clear();
        route.efficiency = Efficiency(map, route);
        Rebuild(map, settings);
    }

    // ===== RESONANCE ANCHORS =====

    public static string WhyNotAnchor(Settlement s)
    {
        if (s == null) return "No settlement here.";
        if (s.anchor) return "A Resonance Anchor already stands here.";
        if (s.kind == SettlementKind.Capital) return "Anchors are raised at Outposts and settlements beyond the Capital.";
        if (s.kind == SettlementKind.Tributary) return "A tributary is too small to hold an Anchor: raise it at its hub or an independent settlement.";
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
    /// City Development moves toward each settlement's potential (down at half the rate when above it), faster on
    /// desirable ground (<see cref="DesirabilityRules.GrowthFactor"/>); a tributary moves toward its own target
    /// (<see cref="WorldTributaries.Target"/>) at its own rate. Outposts do not develop; a settlement cut off from your
    /// authority stops growing; an overstretched administration (<paramref name="efficiency"/> below 1,
    /// <see cref="WorldTerritory.Efficiency"/>) slows the growth. True when any whole point changed.
    /// </summary>
    public static bool Tick(WorldMap map, SettlementRules rules, int sevenths, float efficiency = 1f)
    {
        bool changed = false;
        var network = Networked(map);
        var desirability = rules.desirability ?? WorldDesirability.RulesOf(map);
        var tributaries = rules.tributaries ?? WorldTributaries.RulesOf(map);
        // Tributaries after their hubs, so they read this Seventh's hub.
        foreach (var s in map.Settlements.OrderBy(x => x.kind == SettlementKind.Tributary ? 1 : 0).ToList())
        {
            if (s.kind == SettlementKind.Outpost) continue;
            var t = map.Get(s.coord);
            if (t == null) continue;
            if (!s.detached && t.authorityId != WorldAuthority.Player) continue;
            bool tributary = s.kind == SettlementKind.Tributary;
            float target = tributary ? WorldTributaries.Target(map, tributaries, s) : Evaluate(map, rules, s, network.Contains(s.id)).Potential;
            float rate = tributary ? tributaries.growthPerSeventh : rules.growthPerSeventh;
            float pace = desirability.GrowthFactor(WorldDesirability.Of(map, desirability, t));
            // A shaken settlement (Fractured, Spiraling) grows slower, as it yields less (WorldRuins.Output).
            float whole = WorldRuins.Output(rules.loss ?? WorldRuins.RulesOf(map), s);
            float before = s.development, step = Math.Max(0f, rate) * pace * whole * Math.Max(0, sevenths) * Math.Max(0f, Math.Min(1f, efficiency));
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
            // A shaken settlement yields less (WorldRuins.Output).
            float tens = s.development / 10f * WorldRuins.Output(rules.loss ?? WorldRuins.RulesOf(map), s);
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
        yields.AddRange(WorldTributaries.Yields(map, rules.tributaries ?? WorldTributaries.RulesOf(map)));
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
        // The herds your suzerain keepers keep (WorldEnclaveEcology).
        yields.AddRange(WorldEnclaveEcology.Yields(map, settings));
        return yields;
    }
}
