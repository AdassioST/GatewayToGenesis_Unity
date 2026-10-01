using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One seat of authority on the map, derived at each rebuild: what pulls land, how far, and how much it holds.</summary>
public class TerritorySeat
{
    public int index;
    /// <summary>Stable while the map stands ("settlement:3", "nexus:812", "node:1034", "field:2", "enclave:1").</summary>
    public string key;
    public SeatKind kind;
    public string name;
    public int cell;
    /// <summary>The settlement it belongs to (-1 for Trade Nodes and enclaves).</summary>
    public int settlement = -1;
    /// <summary>The grandfield it extracts (-1 when not a grandfield seat).</summary>
    public int grandfield = -1;
    /// <summary>The authority it grows: yours (<see cref="WorldAuthority.Player"/>), a detached pocket (<see cref="WorldAuthority.Outpost"/>) or a rival's.</summary>
    public string authorityId;
    public float strength, reach, adoptPerSeventh, capacity;
    public int maxCells;
    /// <summary>Held cells where this seat pulls strongest (its core included), a cell held in part counting its share of hexes.</summary>
    public float held;
    /// <summary>Governance travel from the seat to every cell within its reach.</summary>
    public Dictionary<int, float> travel = new Dictionary<int, float>();

    public bool IsPlayers => WorldAuthority.IsPlayers(authorityId);
    public bool HasRoom => held < maxCells;

    /// <summary>Its pull at a cell: full at the seat, fading with governance travel to nothing at its reach.</summary>
    public float PullAt(int c) => travel.TryGetValue(c, out float cost) && reach > 0f ? strength * Math.Max(0f, 1f - cost / reach) : 0f;
}

/// <summary>The seats and fields of the last rebuild (derived; <see cref="WorldMap.territory"/>).</summary>
public class TerritoryState
{
    public readonly List<TerritorySeat> Seats = new List<TerritorySeat>();
    /// <summary>Governance travel from the nearest seat of yours, per cell (+infinity beyond every reach).</summary>
    public float[] nearest;

    public TerritorySeat Seat(int index) => index >= 0 && index < Seats.Count ? Seats[index] : null;

    public TerritorySeat OfSettlement(int settlementId) => Seats.FirstOrDefault(s => s.settlement == settlementId && (s.kind != SeatKind.TradeNexus && s.kind != SeatKind.Grandfield));
}

/// <summary>Which way of growing pays now (<see cref="WorldTerritory.Realm"/>).</summary>
public enum Expansion
{
    /// <summary>Capacity to spare: every cell adopted is pure gain.</summary>
    Horizontal,
    /// <summary>Past comfort: a new cell still pays, but it costs efficiency on every other one.</summary>
    Balanced,
    /// <summary>Past the break-even: more land lowers the realm's total output; develop what you hold.</summary>
    Vertical,
    /// <summary>Over capacity: society stops adopting land, and past collapse the fringe slips away.</summary>
    Overextended,
}

/// <summary>How far the player lets society adopt land by itself.</summary>
public enum BorderPolicy
{
    /// <summary>Adopt until the administration is full (over strain): the widest realm, at an efficiency cost.</summary>
    Expand,
    /// <summary>Adopt while one more cell still raises the realm's total output (the break-even).</summary>
    Measured,
    /// <summary>Adopt nothing: grow tall (claims still work).</summary>
    Hold,
}

/// <summary>What raises Administrative Capacity beyond the map itself (researched technologies, buildings, the council).</summary>
public class RealmContext
{
    /// <summary>Major Settlements the Government Capacity allows.</summary>
    public int governmentCapacity = 1;
    /// <summary>How far society may adopt land by itself.</summary>
    public BorderPolicy policy = BorderPolicy.Measured;
    /// <summary>Citizens of the Capital: the people who go out to settle the land (-1: not counted, full pace).</summary>
    public int population = -1;
    public readonly List<(string source, float capacity)> extra = new List<(string, float)>();
}

/// <summary>The state of the realm's administration: capacity, load, strain and where horizontal gives way to vertical.</summary>
public class RealmReport
{
    public float capacity, load, strain, efficiency = 1f;
    /// <summary>Core cells you hold (your authority and Outpost pockets), and how many of them were adopted and claimed.</summary>
    public int cells, adopted, claimed;
    /// <summary>Hexes you hold of cells not yet core, of which cells you rule de facto; all your land in cells' worth
    /// (core cells plus those hexes' shares).</summary>
    public int hexes, deFacto;
    public float land;
    /// <summary>Average load of one held cell (what one more cell like them would cost).</summary>
    public float averageLoad = 1f;
    /// <summary>Cells at which strain reaches comfort: horizontal is favoured below.</summary>
    public float comfortCells;
    /// <summary>Cells at which one more cell adds nothing to the realm's total output: vertical is favoured beyond.</summary>
    public float breakEvenCells;
    /// <summary>Cells at which strain reaches the point where society stops adopting land.</summary>
    public float stopCells;
    public Expansion favoured;
    public float averageCoherence, averageDevelopment, averageBeauty;
    /// <summary>Capacity every settlement would add if each gained 10 City Development (the vertical lever).</summary>
    public float developmentLever;
    /// <summary>Micro hexes per Seventh society settles now (on average: below 1 it is a chance each Seventh).</summary>
    public float adoptionRate;
    /// <summary>The Capital's citizens (-1 not counted) and the share of the seats' pace they allow (0 below the minimum).</summary>
    public int population = -1;
    public float populationShare = 1f;
    /// <summary>Wilderness cells partly settled by society, and claims still being settled.</summary>
    public int settling, claiming;
    public readonly List<(string source, float amount)> capacitySources = new List<(string, float)>();
    public readonly List<(string source, float amount)> loadSources = new List<(string, float)>();

    public static string Name(BorderPolicy p) => p == BorderPolicy.Expand ? "Expand" : p == BorderPolicy.Measured ? "Measured" : "Hold the borders";

    public static string Describe(BorderPolicy p) =>
        p == BorderPolicy.Expand ? "Society adopts land until the administration is full, even when more land lowers efficiency everywhere."
        : p == BorderPolicy.Measured ? "Society adopts land while each new cell still raises the realm's total output, then stops."
        : "Society adopts no land by itself: develop what you hold (claims still work).";

    public static string Name(Expansion e)
    {
        switch (e)
        {
            case Expansion.Horizontal: return "Horizontal favoured";
            case Expansion.Balanced: return "Balanced";
            case Expansion.Vertical: return "Vertical favoured";
            default: return "Overextended";
        }
    }

    /// <summary>What the report means for the player, in one or two sentences.</summary>
    public string Advice
    {
        get
        {
            switch (favoured)
            {
                case Expansion.Horizontal:
                    return $"Capacity to spare: every cell your society adopts is pure gain until about {comfortCells:0} cells.";
                case Expansion.Balanced:
                    return $"Past comfort: new land still pays until about {breakEvenCells:0} cells, but each costs efficiency everywhere. Raise City Development, roads and Coherence to push the limit.";
                case Expansion.Vertical:
                    return $"Past the break-even ({breakEvenCells:0} cells): more land lowers your total output. Develop your settlements (+10 City Development everywhere: +{developmentLever:0.#} capacity), build roads, anchor Coherence or raise Government Capacity.";
                default:
                    return $"Over capacity: society stops adopting land and yields run at {efficiency:P0}. Develop and connect what you hold, or the weakest-held fringe will slip away.";
            }
        }
    }
}

/// <summary>
/// Administrative Authority as a living territory, with no scene state (tested in <c>WorldTerritoryTests</c>).
///
/// Seats (<see cref="SeatKind"/>): every settlement of yours, a settled and connected Trade Nexus, the Trade Nodes of
/// connected roads and the grandfields being extracted exert a territorial pull; the Capital pulls strongest, then
/// Major Settlements, trade, Developing Towns, outskirt tributaries (their districts may pull harder or add capacity:
/// <see cref="WorldTributaries"/>), havens, grandfields and Outposts (Arcanoria.md: the Capital "anchors all
/// Administrative Authority", Major Settlements are "major anchors"). Pull fades with governance travel: each cell's
/// governance difficulty (<see cref="TerrainSpec.governance"/>: plains easy, highlands and bogs hard, peaks and water
/// never), cheaper along roads and river valleys, dearer over cliffs, dissonance and danger. Enclaves pull for
/// themselves, a rival pull your society will not adopt against. Pull only crosses wilderness and its own authority.
///
/// Land is held one micro hex at a time (<see cref="WorldTile.microHeldMask"/>, <see cref="WorldHoldings"/>): a hex
/// your people hold is yours at once (inside your border, with its share of the cell's yields and load,
/// <see cref="HeldShare"/>); holding <see cref="TerritoryRules.deFactoHexes"/> of a cell's hexes makes the cell yours
/// de facto (your authority), holding all of them makes it core territory.
///
/// Passive adoption (<see cref="Tick"/>): while the administration has capacity to spare, society brings in the
/// wilderness bordering what it holds hex by hex, each new hex touching one already held, so the border creeps
/// outward; a seat finishes the cell it began before starting another. It chooses cells by pull x priority
/// (<see cref="Priority"/>): fertile, watered, coherent, beautiful land, grandfields and sites first; dangerous,
/// dissonant and hard ground last. People do the settling: nothing moves until the Capital has
/// <see cref="TerritoryRules.adoptionMinPopulation"/> citizens, and each seat's chance of settling a hex in a Seventh
/// rises with them (<see cref="PopulationShare"/>). Each seat holds at most <see cref="SeatSpec.maxCells"/> cells'
/// worth of hexes by its own pull: to hold more you need more seats. A paid claim takes one hex bordering your land
/// (<see cref="ClaimHex"/>).
///
/// Administrative Capacity against load (<see cref="Realm"/>): every held cell weighs on the Capital's bureaucracy by
/// its difficulty and its distance from the nearest seat, eased by Coherence and beauty; capacity comes from the
/// Capital, Government Capacity, each settlement (scaled by City Development), the road network, the held land's
/// Coherence, technologies, buildings and the council. Strain (load / capacity) sets efficiency: full below comfort,
/// falling above it. So a wide realm has more land and resources but is harder to run, and a tall one converts
/// development into capacity: <see cref="RealmReport.comfortCells"/> and <see cref="RealmReport.breakEvenCells"/> say
/// where horizontal growth stops paying and vertical growth takes over. Over capacity society stops adopting; past
/// collapse the weakest-held fringe slips back into the wilderness.
/// </summary>
public static class WorldTerritory
{
    public static TerritoryRules RulesOf(WorldMap map) => map?.territoryRules ?? (map != null ? map.territoryRules = new TerritoryRules() : new TerritoryRules());

    // ===== DIFFICULTY AND PRIORITY =====

    /// <summary>How hard the ground is to govern (1 open plains); +infinity on water and impassable ground.</summary>
    public static float Difficulty(WorldGenSettings settings, WorldTile t)
    {
        if (t == null || t.water || t.impassable) return float.PositiveInfinity;
        var terrain = settings?.Terrain(t.terrain);
        if (terrain == null) return 1f;
        if (!terrain.passable || terrain.water) return float.PositiveInfinity;
        float ground = terrain.governance > 0f ? terrain.governance : (float)Math.Pow(Math.Max(0.5f, terrain.moveCost), 1.3);
        // Dense cover is harder to reach and to rule (WorldCover).
        var cover = WorldCover.SpecAt(settings, t);
        return cover != null && cover.governance > 0f ? ground * cover.governance : ground;
    }

    /// <summary>Governance travel to enter a cell (what pull fades over), or +infinity where it cannot pass.</summary>
    public static float Step(TerritoryRules rules, WorldGenSettings settings, WorldTile t)
    {
        float cost = Difficulty(settings, t);
        if (float.IsPositiveInfinity(cost)) return cost;
        cost += rules.escarpmentCost * t.escarpment + rules.dissonanceCost * t.dissonance + rules.dangerCost * t.danger;
        // A restored Old World stretch carries the administration only halfway as well as a rebuilt road.
        if (t.road) cost *= t.restoredRoad ? (1f + rules.roadFactor) * 0.5f : rules.roadFactor;
        else if (t.river) cost *= rules.riverFactor;
        return Math.Max(0.1f, cost);
    }

    /// <summary>
    /// How much society wants a cell (at least 0.05): fertile, watered, coherent and beautiful land, grandfields and
    /// sites first; dangerous, dissonant and hard ground last. <paramref name="seat"/>: an extraction seat wants its own field.
    /// </summary>
    public static float Priority(WorldMap map, WorldGenSettings settings, TerritoryRules rules, WorldTile t, TerritorySeat seat = null)
    {
        if (t == null || t.water || t.impassable) return 0f;
        float p = 1f;
        p += rules.fertilityPriority * (t.landFertility - 0.4f);
        p += rules.freshwaterPriority * Freshwater(map, t);
        p += rules.coherencePriority * (t.coherence - 0.4f);
        p += rules.beautyPriority * t.beauty;
        if (t.grandfield >= 0) p += rules.grandfieldPriority * (0.5f + 0.5f * t.grandfieldDensity) * (seat != null && seat.grandfield == t.grandfield ? 2f : 1f);
        if (t.HasFeature) p += rules.featurePriority;
        p += rules.resourcePriority * WorldResources.LandValue(map, t);
        if (t.explored) p += rules.exploredPriority;
        p -= rules.dangerPriority * t.danger + rules.dissonancePriority * t.dissonance;
        float difficulty = Difficulty(settings, t);
        if (!float.IsPositiveInfinity(difficulty)) p -= rules.difficultyPriority * Math.Max(0f, difficulty - 1f);
        return Math.Max(0.05f, p);
    }

    /// <summary>Freshwater at hand: 1 on or beside a river or lake, else 0.</summary>
    public static float Freshwater(WorldMap map, WorldTile t)
    {
        if (t.river || t.lake) return 1f;
        for (int d = 0; d < 6; d++)
        {
            int nb = map.Neighbour(t.index, d);
            if (nb >= 0 && (map[nb].river || map[nb].lake)) return 1f;
        }
        return 0f;
    }

    // ===== SEATS AND PULL =====

    /// <summary>
    /// Derive the seats and their pull (called by <see cref="WorldCivilization.Rebuild"/> after authority is established):
    /// each cell's pull and strongest seat, the rival pull, Outpost pockets, each seat's held cells.
    /// </summary>
    public static TerritoryState Compute(WorldMap map, WorldGenSettings settings)
    {
        var rules = RulesOf(map);
        var state = new TerritoryState();
        int n = map.Count;
        state.nearest = new float[n];
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            t.pull = 0f;
            t.rivalPull = 0f;
            t.pullSeat = -1;
            state.nearest[i] = float.PositiveInfinity;
        }
        BuildSeats(map, rules, state);

        var sum = new float[n];
        foreach (var seat in state.Seats)
        {
            string own = seat.authorityId;
            bool players = seat.IsPlayers;
            seat.travel = WorldPaths.Flood(map, seat.cell, t =>
            {
                if (players ? !WorldAuthority.IsPlayers(t.authorityId) && t.authorityId != WorldAuthority.Wilderness : t.authorityId != own && t.authorityId != WorldAuthority.Wilderness)
                    return float.PositiveInfinity;
                return Step(rules, settings, t);
            }, seat.reach);
            foreach (var pair in seat.travel)
            {
                var t = map[pair.Key];
                float pull = seat.PullAt(pair.Key);
                if (!players)
                {
                    t.rivalPull = Math.Max(t.rivalPull, pull);
                    continue;
                }
                sum[pair.Key] += pull;
                if (pair.Value < state.nearest[pair.Key]) state.nearest[pair.Key] = pair.Value;
                if (t.pullSeat < 0 || pull > state.Seats[t.pullSeat].PullAt(pair.Key)) t.pullSeat = seat.index;
            }
        }
        foreach (var t in map.Tiles)
        {
            if (t.pullSeat < 0) continue;
            float best = state.Seats[t.pullSeat].PullAt(t.index);
            t.pull = best + rules.overlapShare * (sum[t.index] - best);
        }
        // An Outpost's pocket: land settled hex by hex (not claimed) under a detached seat's pull stays outside Administrative Authority.
        foreach (var t in map.Tiles)
        {
            if (t.whole || t.authorityId != WorldAuthority.Player || map.Claims.Contains(t.index)) continue;
            var seat = state.Seat(t.pullSeat);
            if (seat != null && seat.authorityId == WorldAuthority.Outpost) t.authorityId = WorldAuthority.Outpost;
        }
        foreach (var t in map.Tiles)
        {
            float share = HeldShare(map, t);
            if (share <= 0f) continue;
            var seat = state.Seat(t.pullSeat);
            if (seat != null) seat.held += share;
        }
        map.territory = state;
        return state;
    }

    private static void BuildSeats(WorldMap map, TerritoryRules rules, TerritoryState state)
    {
        var network = WorldCivilization.Networked(map);
        TerritorySeat Add(string key, SeatKind kind, string name, int cell, string authority, float scale = 1f)
        {
            var spec = rules.Seat(kind);
            if (spec == null || cell < 0) return null;
            var seat = new TerritorySeat
            {
                index = state.Seats.Count, key = key, kind = kind, name = name, cell = cell, authorityId = authority,
                strength = spec.strength * scale, reach = spec.reach, maxCells = spec.maxCells, adoptPerSeventh = spec.adoptPerSeventh, capacity = spec.capacity,
            };
            state.Seats.Add(seat);
            return seat;
        }
        foreach (var s in map.Settlements)
        {
            var t = map.Get(s.coord);
            if (t == null) continue;
            var kind = s.kind == SettlementKind.Capital ? SeatKind.Capital : s.detached || s.kind == SettlementKind.Outpost ? SeatKind.Outpost
                : s.kind == SettlementKind.Major ? SeatKind.Major : s.kind == SettlementKind.Haven ? SeatKind.Haven
                : s.kind == SettlementKind.Tributary ? SeatKind.Tributary : SeatKind.Town;
            string authority = s.detached ? WorldAuthority.Outpost : WorldAuthority.Player;
            // A tributary's district may pull harder (a Militant District's walls) and add capacity of its own.
            float districtPull = kind == SeatKind.Tributary ? WorldTributaries.PullScale(map, WorldTributaries.RulesOf(map), s) : 1f;
            var seat = Add("settlement:" + s.id, kind, s.name, t.index, authority, (s.anchor ? 1f + rules.anchorPullBonus : 1f) * districtPull);
            if (seat == null) continue;
            seat.settlement = s.id;
            if (s.anchor) seat.reach += 1f;
            if (districtPull >= 1.5f) seat.reach += 1f;
            var spec = rules.Seat(kind);
            if (spec.scalesWithDevelopment) seat.capacity = spec.capacity * (0.5f + Math.Max(0f, Math.Min(100f, s.development)) / 100f);
            if (kind == SeatKind.Tributary) seat.capacity += WorldTributaries.Capacity(map, WorldTributaries.RulesOf(map), s);
            // A Trade Nexus settled: one connected is worth three isolated cities (half its pull while cut off).
            if (t.nexus != null && !s.detached)
            {
                var nexus = Add("nexus:" + t.index, SeatKind.TradeNexus, $"Trade Nexus of {s.name}", t.index, authority, network.Contains(s.id) ? 1f : 0.5f);
                if (nexus != null)
                {
                    nexus.settlement = s.id;
                    if (!network.Contains(s.id)) nexus.capacity *= 0.5f;
                }
            }
        }
        // Trade Nodes of roads that reach the Capital: the checkpoints carry the administration along the road.
        var nodes = new HashSet<int>();
        foreach (var route in map.Routes)
        {
            if (!network.Contains(route.from) || !network.Contains(route.to)) continue;
            foreach (var coord in route.nodes)
            {
                var t = map.Get(coord);
                if (t == null || t.settlement >= 0 || !nodes.Add(t.index)) continue;
                if (t.authorityId != WorldAuthority.Wilderness && !WorldAuthority.IsPlayers(t.authorityId)) continue;
                Add("node:" + t.index, SeatKind.TradeNode, $"Trade Node ({coord.q}, {coord.r})", t.index, WorldAuthority.Player);
            }
        }
        // Grandfields being extracted draw their own field in.
        foreach (var (field, by, _) in WorldCivilization.Extractions(map))
        {
            var t = map.Get(by.coord);
            if (t == null) continue;
            var seat = Add("field:" + field.index, SeatKind.Grandfield, $"Grandfield by {by.name}", t.index, by.detached ? WorldAuthority.Outpost : WorldAuthority.Player);
            if (seat == null) continue;
            seat.settlement = by.id;
            seat.grandfield = field.index;
        }
        // Enclaves pull for themselves (a Suzerainty keeps its autonomy but no longer contests your land).
        foreach (var e in map.Enclaves)
        {
            if (e.suzerain) continue;
            var t = map.Get(e.coord);
            if (t != null) Add("enclave:" + e.index, SeatKind.Enclave, e.name, t.index, e.AuthorityId);
        }
    }

    // ===== ADOPTION =====

    /// <summary>A cell society could adopt next, with the seat pulling it in and its score (pull x priority).</summary>
    public struct Candidate
    {
        public int cell;
        public TerritorySeat seat;
        public float pull, priority, score;
    }

    /// <summary>
    /// Why your society will not adopt <paramref name="t"/> for <paramref name="seat"/>, or null. It must be passable
    /// wilderness, known (or next to a settlement), with a hex not yet held touching held land, pulled strongly enough
    /// and not pulled harder by a rival; the seat must have room.
    /// </summary>
    public static string WhyNotAdopt(WorldMap map, WorldGenSettings settings, WorldTile t, TerritorySeat seat)
    {
        var rules = RulesOf(map);
        if (t == null) return "Beyond the edge of the world.";
        if (t.water || t.impassable) return "No one can live here.";
        if (t.authorityId != WorldAuthority.Wilderness)
        {
            if (!WorldAuthority.IsPlayers(t.authorityId)) return "Another authority holds it.";
            // Yours de facto: society goes on settling its free hexes until it is core.
            if (!WorldHoldings.Fillable(t, t.authorityId)) return "Already yours.";
        }
        if (map.Claiming.Contains(t.index)) return "It is being claimed.";
        if (seat == null || !seat.IsPlayers) return "None of your seats pulls it.";
        if (!seat.HasRoom) return $"{seat.name} holds all it can ({seat.maxCells} cells): found or grow another seat nearby.";
        if (!t.known && !map.Settlements.Any(s => HexCoord.Distance(s.coord, t.coord) <= rules.localKnowledge)) return "No one knows this land yet: an expedition must pass over it.";
        float pull = seat.PullAt(t.index);
        if (pull < rules.adoptThreshold) return $"The pull is too weak here ({pull:0.00} of {rules.adoptThreshold:0.00}).";
        if (t.rivalPull > t.pull) return "A rival pulls harder here.";
        if (NextHex(map, t, seat.authorityId) < 0) return WorldHoldings.FreeMask(map, t) == 0 ? "Every hex of it is already held." : "It does not border the land this seat holds.";
        return null;
    }

    /// <summary>
    /// The cells society could adopt now, next first (<paramref name="limit"/> at most): cells it has begun settling
    /// (the most settled first), then the best by score; only <paramref name="only"/>'s when given (each seat adopts at
    /// its own pace).
    /// </summary>
    public static List<Candidate> Candidates(WorldMap map, WorldGenSettings settings, int limit = int.MaxValue, TerritorySeat only = null)
    {
        var state = map.territory ?? Compute(map, settings);
        var rules = RulesOf(map);
        var best = new Dictionary<int, Candidate>();
        foreach (var seat in state.Seats)
        {
            if (only != null && seat != only) continue;
            if (!seat.IsPlayers || !seat.HasRoom || seat.adoptPerSeventh <= 0f) continue;
            foreach (int c in seat.travel.Keys)
            {
                var t = map[c];
                if (!WorldHoldings.Fillable(t, seat.authorityId) || WhyNotAdopt(map, settings, t, seat) != null) continue;
                float pull = seat.PullAt(c) + rules.overlapShare * Math.Max(0f, t.pull - seat.PullAt(c));
                float priority = Priority(map, settings, rules, t, seat);
                float score = pull * priority;
                if (!best.TryGetValue(c, out var known) || score > known.score || (score == known.score && seat.index < known.seat.index))
                    best[c] = new Candidate { cell = c, seat = seat, pull = pull, priority = priority, score = score };
            }
        }
        return best.Values.OrderByDescending(x => WorldMap.SettledHexes(map[x.cell])).ThenByDescending(x => x.score).ThenBy(x => x.cell).Take(limit).ToList();
    }

    // ===== SETTLING HEX BY HEX =====

    /// <summary>
    /// Share of the seats' pace the Capital's citizens allow: none below <see cref="TerritoryRules.adoptionMinPopulation"/>,
    /// <see cref="TerritoryRules.adoptionLeastShare"/> at it, rising to all of it at <see cref="TerritoryRules.adoptionFullPopulation"/>.
    /// A population of -1 is not counted (full pace).
    /// </summary>
    public static float PopulationShare(TerritoryRules rules, int population)
    {
        if (population < 0) return 1f;
        if (population < rules.adoptionMinPopulation) return 0f;
        float span = Math.Max(1f, rules.adoptionFullPopulation - rules.adoptionMinPopulation);
        float t = Math.Min(1f, (population - rules.adoptionMinPopulation) / span);
        float least = Math.Max(0f, Math.Min(1f, rules.adoptionLeastShare));
        return least + (1f - least) * t;
    }

    // ===== HOLDING HEX BY HEX =====

    /// <summary>Every hex of the cell a unit can stand on is held (crags come with them).</summary>
    public static bool FullySettled(WorldTile t) => t != null && ((t.microHeldMask | t.microBlockedMask) & 127) == 127;

    /// <summary>
    /// Whether micro hex <paramref name="id"/> is held by <paramref name="authority"/> (yours and your Outposts' count as
    /// one; <see cref="WorldHoldings.HexHolder"/>).
    /// </summary>
    public static bool HexHeld(WorldMap map, int id, string authority) => WorldHoldings.Same(WorldHoldings.HexHolder(map, id), authority);

    /// <summary>How many of the six hexes around <paramref name="id"/> are held for <paramref name="authority"/> (<see cref="HexHeld"/>).</summary>
    public static int Touching(WorldMap map, int id, string authority)
    {
        int touch = 0;
        for (int d = 0; d < 6; d++)
            if (HexHeld(map, MicroNavigation.Neighbour(map, id, d), authority)) touch++;
        return touch;
    }

    /// <summary>
    /// The share of a cell that is yours, by its hexes (<see cref="WorldHoldings.MaskOf"/>): all of a core cell, the
    /// hexes you hold of one you rule de facto or hold in part (out of its open hexes), none otherwise. Yields and load
    /// follow it.
    /// </summary>
    public static float HeldShare(WorldMap map, WorldTile t)
    {
        if (t == null || t.water) return 0f;
        // Core (or given by hand, with no hold worked out): all of it.
        if (WorldAuthority.IsPlayers(t.authorityId) && t.hold != HoldStatus.DeFacto) return 1f;
        if (t.microHeldMask == 0 && !t.whole) return 0f;
        int open = MicroNavigation.Crags(WorldHoldings.OpenMask(t));
        return open <= 0 ? 0f : Math.Min(1f, WorldHoldings.Hexes(map, t, WorldAuthority.Player) / (float)open);
    }

    /// <summary>Hexes you have paid claims for (an older save's whole-cell claims count every open hex).</summary>
    public static int ClaimedHexes(WorldMap map)
    {
        if (map == null) return 0;
        int n = 0;
        foreach (var t in map.Tiles) n += MicroNavigation.Crags(t.microClaimMask);
        foreach (int c in map.Claims)
            if (c >= 0 && c < map.Count && map[c].microClaimMask == 0) n += WorldMap.OpenHexes(map[c]);
        return n;
    }

    /// <summary>
    /// The micro hex of <paramref name="t"/> to settle next for <paramref name="authority"/>, or -1 when none can be:
    /// of the hexes no one holds (<see cref="WorldHoldings.FreeMask"/>) that touch ground it holds (<see cref="HexHeld"/>,
    /// this cell's included), the one touching the most, so land fills in from the border inward; crags are never
    /// settled by hand. Only wilderness and cells it rules de facto have hexes to settle.
    /// </summary>
    public static int NextHex(WorldMap map, WorldTile t, string authority)
    {
        if (map == null || !WorldHoldings.Fillable(t, authority)) return -1;
        int open = WorldHoldings.FreeMask(map, t);
        int best = -1, bestTouch = 0;
        for (int k = 0; k < MicroNavigation.PerCell; k++)
        {
            if ((open & (1 << k)) == 0) continue;
            int id = t.index * MicroNavigation.PerCell + k, touch = Touching(map, id, authority);
            if (touch > bestTouch) { best = id; bestTouch = touch; }
        }
        return best;
    }

    /// <summary>Settle the next hex of <paramref name="t"/> (<see cref="NextHex"/>). Returns its micro id, or -1 when none was left.</summary>
    public static int SettleHex(WorldMap map, WorldTile t, string authority)
    {
        int id = NextHex(map, t, authority);
        if (id >= 0) t.microHeldMask |= 1 << (id % MicroNavigation.PerCell);
        return id;
    }

    /// <summary>
    /// A cell whose every hex you hold becomes core territory: into <see cref="WorldMap.Claims"/> when any hex of it was
    /// claimed (claimed land never slips away), else <see cref="WorldMap.Adopted"/>, and the civilization is rebuilt.
    /// False (nothing changes) while a hex is still open or held by another, or when it is core already.
    /// </summary>
    public static bool Complete(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        if (map == null || t == null || t.whole || !FullySettled(t)) return false;
        if (t.authorityId != WorldAuthority.Wilderness && !WorldAuthority.IsPlayers(t.authorityId)) return false;
        if (map.Claims.Contains(t.index) || map.Adopted.Contains(t.index)) return false;
        (t.microClaimMask != 0 ? map.Claims : map.Adopted).Add(t.index);
        WorldCivilization.Rebuild(map, settings);
        return true;
    }

    /// <summary>
    /// After a hex of <paramref name="t"/> became yours: the last one makes the cell core (<see cref="Complete"/>); the
    /// one that gives you <see cref="TerritoryRules.deFactoHexes"/> of them (more than anyone else) makes it yours de
    /// facto (the civilization is rebuilt). Returns your hold on the cell now.
    /// </summary>
    public static HoldStatus AfterHex(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        if (Complete(map, settings, t)) return HoldStatus.Core;
        var status = WorldHoldings.Status(map, t, WorldAuthority.Player);
        if (status == HoldStatus.DeFacto && t.authorityId == WorldAuthority.Wilderness) WorldCivilization.Rebuild(map, settings);
        return status;
    }

    /// <summary>
    /// A paid claim takes one micro hex (<paramref name="id"/>) into your sphere at once: it must be claimable
    /// (<see cref="WorldAuthority.WhyNotClaimHex"/>, cost aside). It is held and marked claimed; the fourth makes the
    /// cell yours de facto, the last core (<see cref="AfterHex"/>). Returns whether the hex was taken;
    /// <paramref name="status"/> is your hold on the cell after it.
    /// </summary>
    public static bool ClaimHex(WorldMap map, WorldGenSettings settings, int id, out HoldStatus status)
    {
        status = HoldStatus.None;
        if (WorldAuthority.WhyNotClaimHex(map, id) != null) return false;
        var t = map[id / MicroNavigation.PerCell];
        var before = t.authorityId;
        int bit = 1 << (id % MicroNavigation.PerCell);
        t.microHeldMask |= bit;
        t.microClaimMask |= bit;
        status = AfterHex(map, settings, t);
        // Counted toward the seat pulling it hardest, as Compute counts it (a rebuild counted it already).
        if (t.authorityId == before && status != HoldStatus.Core && map.territory?.Seat(t.pullSeat) is TerritorySeat seat)
            seat.held += 1f / Math.Max(1, WorldMap.OpenHexes(t));
        return true;
    }

    /// <summary>
    /// Takes a whole cell at once (claims made before they went hex by hex, in older saves): every hex of it is held
    /// and claimed, it joins <see cref="WorldMap.Claims"/> and the civilization is rebuilt. False (and nothing
    /// changes) when the cell is no longer wilderness land. The micro hexes it settled are added to
    /// <paramref name="settled"/> when given.
    /// </summary>
    public static bool ClaimNow(WorldMap map, WorldGenSettings settings, int cell, List<int> settled = null)
    {
        var t = map != null && cell >= 0 && cell < map.Count ? map[cell] : null;
        map?.Claiming.Remove(cell);
        if (t == null || t.water || t.impassable || t.authorityId != WorldAuthority.Wilderness) return false;
        int open = 127 & ~t.microBlockedMask & ~t.microHeldMask;
        for (int k = 0; k < MicroNavigation.PerCell; k++)
            if ((open & (1 << k)) != 0) settled?.Add(cell * MicroNavigation.PerCell + k);
        t.microHeldMask |= open;
        t.microClaimMask |= open;
        if (!map.Claims.Contains(cell)) map.Claims.Add(cell);
        WorldCivilization.Rebuild(map, settings);
        return true;
    }

    /// <summary>
    /// Whole-cell claims an older save left being settled are finished now (<see cref="ClaimNow"/>); a claimed cell
    /// another authority or settlement took first is dropped.
    /// </summary>
    public static TickResult AdvanceClaims(WorldMap map, WorldGenSettings settings)
    {
        var result = new TickResult();
        if (map == null || map.Claiming.Count == 0) return result;
        foreach (int cell in map.Claiming.ToList())
            if (ClaimNow(map, settings, cell, result.settled)) result.claimed.Add(cell);
        return result;
    }

    /// <summary>The strain at which one more cell adds nothing to the realm's total output (N x efficiency peaks).</summary>
    public static float BreakEvenStrain(TerritoryRules rules)
    {
        float k = Math.Max(0.0001f, rules.efficiencyDrop);
        return Math.Max(rules.comfortStrain, (1f + k * rules.comfortStrain) / (2f * k));
    }

    /// <summary>The strain at which society stops adopting under a policy.</summary>
    public static float StopStrain(TerritoryRules rules, BorderPolicy policy) =>
        policy == BorderPolicy.Hold ? 0f : policy == BorderPolicy.Measured ? Math.Min(rules.overStrain, BreakEvenStrain(rules)) : rules.overStrain;

    /// <summary>Share of its pace each seat adopts at a strain: full up to comfort, slowing toward over strain, none from the policy's stop.</summary>
    public static float Pace(TerritoryRules rules, float strain, BorderPolicy policy)
    {
        if (strain >= StopStrain(rules, policy)) return 0f;
        if (strain > rules.comfortStrain && rules.overStrain > rules.comfortStrain)
            return Math.Max(0f, (rules.overStrain - strain) / (rules.overStrain - rules.comfortStrain));
        return 1f;
    }

    /// <summary>
    /// Micro hexes per Seventh society settles at a strain: every seat with room at its own pace (<see cref="Pace"/>),
    /// times the share the Capital's citizens allow (<see cref="PopulationShare"/>).
    /// </summary>
    public static float AdoptionRate(WorldMap map, float strain, BorderPolicy policy = BorderPolicy.Measured, float populationShare = 1f)
    {
        if (map.territory == null) return 0f;
        float rate = map.territory.Seats.Where(s => s.IsPlayers && s.HasRoom).Sum(s => Math.Max(0f, s.adoptPerSeventh));
        return rate * Pace(RulesOf(map), strain, policy) * Math.Max(0f, populationShare);
    }

    /// <summary>A cell society is bringing in next (<see cref="Forecast"/>): its hexes in the order they will be settled, and about when.</summary>
    public class AdoptionPlan
    {
        public int cell;
        /// <summary>The authority it will join (yours, or a detached Outpost pocket).</summary>
        public string authorityId;
        /// <summary>The seats settling it (usually one).</summary>
        public readonly List<string> seats = new List<string>();
        /// <summary>Hexes per Seventh settled here on average (the seats' pace, the strain and the Capital's citizens).</summary>
        public float rate;
        /// <summary>The micro hexes still to settle (ids), next first.</summary>
        public readonly List<int> hexes = new List<int>();
        /// <summary>About how many Sevenths from now each of <see cref="hexes"/> is settled (an average: each Seventh is a roll).</summary>
        public readonly List<float> sevenths = new List<float>();
        /// <summary>About how many Sevenths until the whole cell is yours.</summary>
        public float WholeCell => sevenths.Count > 0 ? sevenths[sevenths.Count - 1] : 0f;
    }

    /// <summary>
    /// What society will settle next, and about when: for each seat with room, the cell it settles next (the one it
    /// began first, <see cref="Candidates"/>), its hexes in the order <see cref="NextHex"/> will take them, each timed
    /// at the seat's average pace (<see cref="SeatSpec.adoptPerSeventh"/> x <see cref="Pace"/> x
    /// <see cref="PopulationShare"/>); seats settling the same cell add up. <paramref name="progress"/> is the time each
    /// seat already has toward its next roll (by seat key, as <see cref="Tick"/> keeps it) and <paramref name="elapsed"/>
    /// the share of the current Seventh gone by. Empty when society settles nothing now (too few citizens, over
    /// strain, a Hold policy, or past collapse). Nothing on the map changes.
    /// </summary>
    public static List<AdoptionPlan> Forecast(WorldMap map, WorldGenSettings settings, RealmContext context, IReadOnlyDictionary<string, float> progress = null, float elapsed = 0f)
    {
        var plans = new List<AdoptionPlan>();
        if (map == null) return plans;
        var rules = RulesOf(map);
        if (map.territory == null) Compute(map, settings);
        var realm = Realm(map, settings, context);
        if (realm.strain > rules.collapseStrain) return plans;
        float pace = Pace(rules, realm.strain, context?.policy ?? BorderPolicy.Measured) * PopulationShare(rules, context?.population ?? -1);
        if (pace <= 0f) return plans;
        var byCell = new Dictionary<int, AdoptionPlan>();
        var waited = new Dictionary<int, float>();
        foreach (var seat in map.territory.Seats)
        {
            if (!seat.IsPlayers || !seat.HasRoom || seat.adoptPerSeventh <= 0f) continue;
            var next = Candidates(map, settings, 1, seat);
            if (next.Count == 0) continue;
            int cell = next[0].cell;
            if (!byCell.TryGetValue(cell, out var plan))
            {
                plan = new AdoptionPlan { cell = cell, authorityId = seat.authorityId };
                byCell[cell] = plan;
                plans.Add(plan);
            }
            plan.rate += seat.adoptPerSeventh * pace;
            plan.seats.Add(seat.name);
            float have = progress != null && progress.TryGetValue(seat.key, out float p) ? p : 0f;
            waited[cell] = Math.Max(waited.TryGetValue(cell, out float w) ? w : 0f, Math.Min(1f, have + Math.Max(0f, elapsed)));
        }
        foreach (var plan in plans)
        {
            // Settle the cell in thought, hex by hex, then put it back as it was.
            var t = map[plan.cell];
            int keep = t.microHeldMask;
            for (int k = 1; ; k++)
            {
                int id = NextHex(map, t, plan.authorityId);
                if (id < 0) break;
                t.microHeldMask |= 1 << (id % MicroNavigation.PerCell);
                plan.hexes.Add(id);
                // Each Seventh is one roll: the k-th hex comes after about k / rate rolls, the first of them at the next Seventh.
                plan.sevenths.Add(Math.Max(0f, Math.Max(1f, k / Math.Max(1e-4f, plan.rate)) - waited[plan.cell]));
            }
            t.microHeldMask = keep;
        }
        return plans;
    }

    /// <summary>What one Seventh (or several) of territorial life did.</summary>
    public class TickResult
    {
        /// <summary>Cells adopted as core (every hex settled).</summary>
        public readonly List<int> adopted = new List<int>();
        /// <summary>Cells that became yours de facto (<see cref="TerritoryRules.deFactoHexes"/> hexes held), not yet core.</summary>
        public readonly List<int> deFacto = new List<int>();
        /// <summary>Cells whose every hex is now held with a hex of them claimed (completed by society, or an older save's claims, <see cref="AdvanceClaims"/>).</summary>
        public readonly List<int> claimed = new List<int>();
        /// <summary>Micro hexes settled (ids), whether or not they completed a cell: each is yours at once.</summary>
        public readonly List<int> settled = new List<int>();
        /// <summary>Cells slipped back into the wilderness, whole or their adopted hexes (claimed hexes stay).</summary>
        public readonly List<int> lost = new List<int>();
        /// <summary>Authority changed (a cell adopted, claimed or lost).</summary>
        public bool Changed => adopted.Count > 0 || claimed.Count > 0 || deFacto.Count > 0 || lost.Count > 0;
        /// <summary>Anything changed on the map, a single hex included (each hex held is land, yields and load).</summary>
        public bool Any => Changed || settled.Count > 0;
    }

    /// <summary>
    /// Advance the territory by <paramref name="sevenths"/>. Each seat with room settles hexes of its own next
    /// candidate (the cell it began first) at its own pace: <see cref="SeatSpec.adoptPerSeventh"/> x <see cref="Pace"/>
    /// x <see cref="PopulationShare"/> hexes per Seventh. With <paramref name="roll"/> (draws in [0, 1)) that pace is a
    /// chance rolled once per whole Seventh (<paramref name="progress"/> keeps each seat's time toward its next roll,
    /// by seat key); without it the expected hexes accumulate (<paramref name="progress"/> keeps the fraction). So an
    /// Outpost or a town grows beside the Capital rather than behind it. Past collapse strain the weakest-held adopted
    /// land drifts away instead (<paramref name="drift"/>). Rebuilds the civilization after each cell adopted or lost.
    /// <paramref name="context"/> sets capacity, the border policy and the Capital's citizens.
    /// </summary>
    public static TickResult Tick(WorldMap map, WorldGenSettings settings, RealmContext context, float sevenths, Dictionary<string, float> progress, ref float drift, Func<double> roll = null)
    {
        var result = new TickResult();
        if (map == null || sevenths <= 0f || progress == null) return result;
        var rules = RulesOf(map);
        var policy = context?.policy ?? BorderPolicy.Measured;
        float people = PopulationShare(rules, context?.population ?? -1);
        if (map.territory == null) Compute(map, settings);
        var realm = Realm(map, settings, context);

        if (realm.strain > rules.collapseStrain)
        {
            progress.Clear();
            drift += Math.Max(0f, rules.driftPerSeventh) * sevenths;
            while (drift >= 1f)
            {
                drift -= 1f;
                int lose = Weakest(map, settings);
                if (lose < 0) { drift = 0f; break; }
                // Its adopted hexes go (a core adopted cell, or the fringe of one held de facto or in part); claimed hexes stay.
                map.Adopted.Remove(lose);
                map[lose].microHeldMask &= map[lose].microClaimMask;
                result.lost.Add(lose);
                WorldCivilization.Rebuild(map, settings);
                realm = Realm(map, settings, context);
                if (realm.strain <= rules.collapseStrain) { drift = 0f; break; }
            }
            return result;
        }
        drift = 0f;
        float pace = Pace(rules, realm.strain, policy) * people;
        if (pace <= 0f) { progress.Clear(); return result; }
        // Seats gone since the last Seventh lose their progress.
        foreach (var key in progress.Keys.Where(k => map.territory.Seats.All(s => s.key != k)).ToList()) progress.Remove(key);
        foreach (string key in map.territory.Seats.Where(s => s.IsPlayers && s.adoptPerSeventh > 0f).Select(s => s.key).ToList())
        {
            var seat = map.territory.Seats.FirstOrDefault(s => s.key == key);
            if (seat == null || !seat.HasRoom) { progress.Remove(key); continue; }
            progress.TryGetValue(key, out float have);
            float rate = seat.adoptPerSeventh * pace;
            int hexes = 0;
            if (roll == null)
            {
                have += rate * sevenths;
                hexes = (int)have;
                have -= hexes;
            }
            else
            {
                // One roll per whole Seventh: a pace of 0.3 is a 30% chance of settling a hex, 1.4 one hex and a 40% chance of another.
                have += sevenths;
                while (have >= 1f)
                {
                    have -= 1f;
                    hexes += (int)rate;
                    if (roll() < rate - Math.Floor(rate)) hexes++;
                }
            }
            for (int i = 0; i < hexes; i++)
            {
                var next = Candidates(map, settings, 1, seat);
                if (next.Count == 0) break;
                var t = map[next[0].cell];
                int id = SettleHex(map, t, seat.authorityId);
                if (id < 0) break;
                result.settled.Add(id);
                var was = t.authorityId;
                var status = AfterHex(map, settings, t);
                bool rebuilt = status == HoldStatus.Core || t.authorityId != was;
                if (status == HoldStatus.Core) (map.Claims.Contains(t.index) ? result.claimed : result.adopted).Add(t.index);
                else if (t.authorityId != was) result.deFacto.Add(t.index);
                // The hex is yours at once, and weighs on the administration like the rest.
                realm = Realm(map, settings, context);
                pace = Pace(rules, realm.strain, policy) * people;
                if (pace <= 0f) { progress.Clear(); return result; }
                // It counts toward the room of the seat pulling it hardest (as Compute counts it); a rebuild (the cell de
                // facto or core now) made new seat objects: follow this one by its key.
                if (rebuilt) seat = map.territory.Seats.FirstOrDefault(s => s.key == key);
                else (map.territory.Seat(t.pullSeat) ?? seat).held += 1f / Math.Max(1, WorldMap.OpenHexes(t));
                if (seat == null || !seat.HasRoom) { have = 0f; break; }
            }
            if (have > 0f) progress[key] = have;
            else progress.Remove(key);
        }
        return result;
    }

    // The adopted land held most weakly (a core adopted cell, or a cell not yet core with adopted hexes): least pull x
    // priority, farthest from any seat first on a tie. Land a district keeps watch over (a Militant District) never slips away.
    private static int Weakest(WorldMap map, WorldGenSettings settings)
    {
        var rules = RulesOf(map);
        var heldFast = WorldTributaries.HeldFast(map);
        int worst = -1;
        float score = float.MaxValue, far = -1f;
        var fringe = map.Tiles.Where(t => !t.whole && (t.microHeldMask & ~t.microClaimMask) != 0 && !map.Adopted.Contains(t.index) && !map.Claims.Contains(t.index)
            && (t.authorityId == WorldAuthority.Wilderness || WorldAuthority.IsPlayers(t.authorityId))).Select(t => t.index);
        foreach (int c in map.Adopted.Concat(fringe))
        {
            if (c < 0 || c >= map.Count || heldFast.Contains(c)) continue;
            var t = map[c];
            float s = t.pull * Priority(map, settings, rules, t);
            float d = map.territory != null ? map.territory.nearest[c] : 0f;
            if (s < score || (s == score && d > far)) { worst = c; score = s; far = d; }
        }
        return worst;
    }

    // ===== ADMINISTRATION =====

    /// <summary>The load of one held cell: its difficulty and distance from the nearest seat, eased by Coherence and beauty.</summary>
    public static float CellLoad(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        var rules = RulesOf(map);
        float difficulty = Difficulty(settings, t);
        if (float.IsPositiveInfinity(difficulty)) difficulty = 1f;
        if (t.settlement >= 0) return rules.seatCellLoad;
        float distance = map.territory != null && t.index < map.territory.nearest.Length ? map.territory.nearest[t.index] : float.PositiveInfinity;
        // Beyond every seat's reach: as far as the farthest reach, and then some.
        if (float.IsPositiveInfinity(distance)) distance = HexCoord.Distance(t.coord, map.Capital) * 1.5f;
        float load = difficulty * (1f + rules.distanceLoad * distance);
        load *= Math.Max(0.3f, 1f + rules.coherenceRelief * (0.5f - t.coherence) - rules.beautyRelief * t.beauty);
        return Math.Max(0.05f, load);
    }

    /// <summary>Efficiency at a strain: 1 up to comfort, falling by <see cref="TerritoryRules.efficiencyDrop"/> per unit above it, never below the floor.</summary>
    public static float Efficiency(TerritoryRules rules, float strain) =>
        strain <= rules.comfortStrain ? 1f : Math.Max(rules.efficiencyFloor, 1f - rules.efficiencyDrop * (strain - rules.comfortStrain));

    /// <summary>The realm's administration now (see the class summary).</summary>
    public static RealmReport Realm(WorldMap map, WorldGenSettings settings, RealmContext context = null)
    {
        var report = new RealmReport();
        if (map == null) return report;
        var rules = RulesOf(map);
        context = context ?? new RealmContext();
        var state = map.territory ?? Compute(map, settings);

        // Capacity.
        void Capacity(string source, float amount)
        {
            if (Math.Abs(amount) < 0.01f) return;
            report.capacitySources.Add((source, amount));
            report.capacity += amount;
        }
        Capacity("The Capital's bureaucracy", rules.baseCapacity);
        Capacity($"Government Capacity ({context.governmentCapacity})", rules.perGovernmentCapacity * Math.Max(0, context.governmentCapacity));
        float developed = 0f, lever = 0f;
        int settlements = 0;
        foreach (var seat in state.Seats.Where(s => s.IsPlayers))
        {
            Capacity(seat.name, seat.capacity);
            var spec = rules.Seat(seat.kind);
            if (spec != null && spec.scalesWithDevelopment) lever += spec.capacity * 0.1f;
        }
        var network = WorldCivilization.Networked(map);
        // Tributaries are joined by their own road and counted by their seats: only independent settlements count here.
        int joined = map.Settlements.Count(s => s.kind != SettlementKind.Capital && s.kind != SettlementKind.Tributary && network.Contains(s.id));
        Capacity($"Roads to the Capital ({joined})", rules.networkCapacity * joined);
        foreach (var s in map.Settlements.Where(s => s.kind != SettlementKind.Outpost && s.kind != SettlementKind.Tributary))
        {
            developed += s.development;
            settlements++;
        }

        // Load, and the held land's character.
        float coherence = 0f, beauty = 0f, hardLoad = 0f, farLoad = 0f;
        foreach (var t in map.Tiles)
        {
            // A cell held de facto or in part weighs by its share of hexes.
            float share = HeldShare(map, t);
            if (share <= 0f) continue;
            if (WorldAuthority.IsPlayers(t.authorityId) && t.hold != HoldStatus.DeFacto) report.cells++;
            else
            {
                report.hexes += WorldHoldings.Hexes(map, t, WorldAuthority.Player);
                if (WorldAuthority.IsPlayers(t.authorityId)) report.deFacto++;
            }
            report.land += share;
            float load = CellLoad(map, settings, t) * share;
            report.load += load;
            coherence += t.coherence * share;
            beauty += t.beauty * share;
            float difficulty = Difficulty(settings, t);
            if (!float.IsPositiveInfinity(difficulty) && difficulty > 1.4f && t.settlement < 0) hardLoad += load;
            if (t.settlement < 0 && state.nearest[t.index] > 4f) farLoad += load;
        }
        foreach (int c in map.Adopted) if (c >= 0 && c < map.Count && WorldAuthority.IsPlayers(map[c].authorityId)) report.adopted++;
        foreach (int c in map.Claims) if (c >= 0 && c < map.Count && WorldAuthority.IsPlayers(map[c].authorityId)) report.claimed++;
        if (report.land > 0f)
        {
            report.averageCoherence = coherence / report.land;
            report.averageBeauty = beauty / report.land;
            Capacity("Coherence of the held land", rules.coherenceCapacity * (report.averageCoherence - 0.4f));
        }
        foreach (var (source, amount) in context.extra) Capacity(source, amount);
        report.capacity = Math.Max(1f, report.capacity);
        report.averageDevelopment = settlements > 0 ? developed / settlements : 0f;
        report.developmentLever = lever;

        report.loadSources.Add(($"{report.cells} held cells{(report.hexes > 0 ? $" and {report.hexes} hexes of others" : string.Empty)}", report.load));
        if (hardLoad > 0f) report.loadSources.Add(("of which hard ground", hardLoad));
        if (farLoad > 0f) report.loadSources.Add(("of which far from any seat", farLoad));

        report.strain = report.load / report.capacity;
        report.efficiency = Efficiency(rules, report.strain);
        report.averageLoad = report.land > 0f ? Math.Max(0.05f, report.load / report.land) : 1f;
        // Where the strain reaches comfort, where one more cell adds nothing (d/dN of N x efficiency = 0), and where adoption stops.
        report.comfortCells = rules.comfortStrain * report.capacity / report.averageLoad;
        report.breakEvenCells = BreakEvenStrain(rules) * report.capacity / report.averageLoad;
        report.stopCells = rules.overStrain * report.capacity / report.averageLoad;
        report.favoured = report.strain >= rules.overStrain ? Expansion.Overextended
            : report.land < report.comfortCells ? Expansion.Horizontal
            : report.land < report.breakEvenCells ? Expansion.Balanced : Expansion.Vertical;
        report.population = context.population;
        report.populationShare = PopulationShare(rules, context.population);
        report.adoptionRate = AdoptionRate(map, report.strain, context.policy, report.populationShare);
        report.settling = map.Tiles.Count(t => t.microHeldMask != 0 && t.authorityId == WorldAuthority.Wilderness && !map.Claiming.Contains(t.index));
        report.claiming = map.Claiming.Count;
        return report;
    }
}
