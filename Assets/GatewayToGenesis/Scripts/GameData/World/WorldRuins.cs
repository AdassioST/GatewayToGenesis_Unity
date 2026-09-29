using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The ruins of a fallen settlement (saved with the world): what it was, when and why it fell, and what investigating it left.</summary>
[Serializable]
public class Ruin
{
    public int id;
    /// <summary>The settlement's name and id when it stood (-1 for the Old World's).</summary>
    public string name;
    public int settlement;
    public SettlementKind kind;
    /// <summary>A tributary's district, or null.</summary>
    public string district;
    public string binding;
    public HexCoord coord;
    /// <summary>Ages it was founded and fell in (-1: before the Ages, the Old World).</summary>
    public int foundedAge, fallenAge;
    /// <summary>Whole Ages it stood through.</summary>
    public int fullAges;
    /// <summary>City Development it had when it fell.</summary>
    public float development;
    /// <summary>It stood outside your Administrative Authority (an Outpost, a detached settlement).</summary>
    public bool detached;
    /// <summary>What brought it down: "danger", "withering", "pillage", "cataclysm".</summary>
    public string cause;
    public bool investigated;
    /// <summary>A civic its people's ways left behind, offered for adoption once investigated (null when none).</summary>
    public string civic;
    public bool civicAdopted;
    /// <summary>A ruin of the Old World, fallen before the Ages (not one of yours: rebuilding on it reclaims nothing).</summary>
    public bool ancient;
    /// <summary>A settlement of yours was founded on it again: the lost settlement reclaimed.</summary>
    public bool reclaimed;
    public string reclaimedBy;
}

/// <summary>What investigating a ruin turns up (<see cref="WorldRuins.Findings"/>); the technology and the civic are picked by <see cref="WorldSystem"/>.</summary>
public class RuinFindings
{
    public readonly List<ResourceAmount> resources = new List<ResourceAmount>();
    /// <summary>Its records enlighten a technology.</summary>
    public bool enlighten;
    /// <summary>Its people's ways survive as a civic (<see cref="Ruin.civic"/>).</summary>
    public bool civic;
    /// <summary>A roll (0-1) for picking the technology and the civic, so a save replays the same find.</summary>
    public double pick;
}

/// <summary>
/// Settlement Composure, loss and ruins, with no scene state (tested in <c>WorldRuinTests</c>). Loss is transformation
/// rather than only punishment: what falls leaves ruins that can be investigated for what the failure left behind, and
/// the world begins with the ruins and broken roads of the Old World, which ended once already.
///
/// - A settlement's Composure is a legend's (<see cref="ComposureRules"/>): strain 0-100 read as Pristine, Clouded,
///   Fractured, Spiraling or Surrender with the same thresholds, easing toward the Clouded baseline
///   (<see cref="ComposureRules.Ease"/>). Danger at its cell, withering (a tributary no road joins to a hub) and pillage
///   strain it, wearing City Development down; Fractured and Spiraling settlements yield less (<see cref="Output"/>).
/// - At Surrender a settlement falls (<see cref="Fall"/>) into a ruin that stays on the map; its roads stay, its
///   tributaries rejoin another hub. The Capital can be Spiraling (the whole realm yields less) but never Surrenders.
/// - A ruin is investigated once by an expedition (<see cref="UnitTask.Investigate"/>): salvage, Research, sometimes an
///   Enlightenment, sometimes a civic its people lived by (the Digestive Rebirth achievement). A settlement founded on
///   the ruins of one of yours reclaims it (Era Score, Welcome Back, Traitors).
/// - The Old World (<see cref="PlaceOldWorld"/>): ruins of its cities and the broken roads between them, walked faster
///   than open ground; a road built over them restores those stretches cheaply, as a lesser road until rebuilt.
/// </summary>
public static class WorldRuins
{
    public static LossRules RulesOf(WorldMap map) => map?.settlementRules?.loss ?? Default;

    private static readonly LossRules Default = new LossRules();

    public const string Danger = "danger", Withering = "withering", Pillage = "pillage", Cataclysm = "cataclysm";

    public static Ruin At(WorldMap map, HexCoord coord) => map?.Ruins.FirstOrDefault(r => r.coord == coord);

    // ===== COMPOSURE =====

    /// <summary>A settlement's Composure state, read from its strain as a legend's is.</summary>
    public static ComposureState StateOf(LossRules rules, Settlement s) => ComposureRules.StateOf(s?.strain ?? 0f, rules.composure);

    /// <summary>The most strain <paramref name="s"/> can bear: the Capital stops short of Surrender.</summary>
    public static float MaxStrain(LossRules rules, Settlement s) =>
        s != null && s.kind == SettlementKind.Capital ? Math.Max(0f, rules.composure.surrenderAt - 0.01f) : rules.composure.surrenderAt;

    /// <summary>What a settlement yields and grows at in its state: whole while Pristine or Clouded, less while Fractured or Spiraling (a legend's council factors).</summary>
    public static float Output(LossRules rules, Settlement s) => ComposureRules.CouncilFactor(StateOf(rules, s), rules.composure);

    /// <summary>A tributary withers when no road joins it to a hub: orphaned with no hub left, or cut off from the one it serves.</summary>
    public static bool IsWithering(WorldMap map, Settlement s)
    {
        if (!WorldTributaries.IsTributary(s)) return false;
        var hub = WorldTributaries.HubOf(map, s);
        return hub == null || !WorldTributaries.Joined(map, s.id, hub.id);
    }

    /// <summary>What strains <paramref name="s"/> now, as (cause, strain per Seventh).</summary>
    public static List<(string cause, float perSeventh)> Harm(WorldMap map, LossRules rules, Settlement s)
    {
        var result = new List<(string, float)>();
        var t = map.Get(s.coord);
        if (t == null) return result;
        float floor = Math.Max(0f, Math.Min(0.99f, rules.dangerFloor));
        // Standing hazards, and the fresh signs of hunters prowling at its gates (threats cast no aura: only what really hunts near strains it).
        float danger = Math.Max(t.danger, t.signs);
        if (danger > floor) result.Add((Danger, rules.dangerStrain * (danger - floor) / (1f - floor)));
        if (IsWithering(map, s) && rules.witherStrain > 0f) result.Add((Withering, rules.witherStrain));
        return result;
    }

    /// <summary>How fast its strain eases toward the baseline each Seventh (faster on the Capital's roads).</summary>
    public static float Recovery(LossRules rules, Settlement s, bool networked) =>
        Math.Max(0f, rules.composure.restRecovery) * (networked ? Math.Max(1f, rules.networkRecovery) : 1f);

    /// <summary>
    /// Strain <paramref name="s"/> by <paramref name="amount"/> at once (a pillage): it never goes past its maximum and
    /// its City Development wears down with it. True when it reached Surrender (a settlement other than the Capital then
    /// falls at the next <see cref="Tick"/>, or at once through <see cref="Fall"/>).
    /// </summary>
    public static bool Strain(LossRules rules, Settlement s, float amount, string cause)
    {
        if (s == null || amount <= 0f) return false;
        float before = s.strain;
        s.strain = Math.Min(MaxStrain(rules, s), s.strain + amount);
        s.development = Math.Max(0f, s.development - (s.strain - before) * Math.Max(0f, rules.developmentPerStrain));
        s.harmedBy = cause;
        return StateOf(rules, s) == ComposureState.Surrender;
    }

    /// <summary>What happened to the settlements over a tick: whose Composure deepened, who fell.</summary>
    public struct TickResult
    {
        public List<(Settlement settlement, ComposureState state)> deepened;
        public List<Ruin> fallen;
        public bool changed;
    }

    /// <summary>
    /// Sevenths of strain and easing for every settlement (<see cref="ComposureRules.Ease"/>: the strain of the Seventh
    /// is added, then it eases toward the baseline); those that Surrender fall into ruins. The Capital never falls.
    /// </summary>
    public static TickResult Tick(WorldMap map, WorldGenSettings settings, LossRules rules, int sevenths, int age)
    {
        var result = new TickResult { deepened = new List<(Settlement, ComposureState)>(), fallen = new List<Ruin>() };
        if (sevenths <= 0) return result;
        var network = WorldCivilization.Networked(map);
        var falling = new List<Settlement>();
        foreach (var s in map.Settlements.ToList())
        {
            float before = s.strain;
            var stateBefore = StateOf(rules, s);
            var harm = Harm(map, rules, s);
            float load = harm.Sum(h => h.perSeventh);
            float recovery = Recovery(rules, s, network.Contains(s.id));
            for (int i = 0; i < sevenths; i++)
            {
                float next = Math.Min(MaxStrain(rules, s), ComposureRules.Ease(s.strain, load, recovery, rules.composure));
                if (next > s.strain) s.development = Math.Max(0f, s.development - (next - s.strain) * Math.Max(0f, rules.developmentPerStrain));
                s.strain = next;
            }
            if (harm.Count > 0 && s.strain > before) s.harmedBy = harm.OrderByDescending(h => h.perSeventh).First().cause;
            if (Math.Abs(s.strain - before) > 1e-5f) result.changed = true;
            var state = StateOf(rules, s);
            if (state > stateBefore && state >= ComposureState.Fractured) result.deepened.Add((s, state));
            if (s.kind != SettlementKind.Capital && state == ComposureState.Surrender) falling.Add(s);
        }
        foreach (var s in falling) result.fallen.Add(Fall(map, settings, s, s.harmedBy ?? Danger, age));
        return result;
    }

    /// <summary>
    /// <paramref name="s"/> falls into ruins (never the Capital: null): it leaves the settlements, its ruin stays on the
    /// map, its roads stay, the map is rebuilt (its tributaries rejoin another hub) and the danger recomputed.
    /// </summary>
    public static Ruin Fall(WorldMap map, WorldGenSettings settings, Settlement s, string cause, int age)
    {
        if (s == null || s.kind == SettlementKind.Capital || !map.Settlements.Contains(s)) return null;
        var ruin = new Ruin
        {
            id = NextId(map),
            name = s.name,
            settlement = s.id,
            kind = s.kind,
            district = WorldTributaries.IsTributary(s) ? s.district : null,
            binding = s.binding,
            coord = s.coord,
            foundedAge = s.foundedAge,
            fallenAge = age,
            fullAges = s.fullAges,
            development = s.development,
            detached = s.detached,
            cause = cause,
        };
        // A ruin already there (it had been reclaimed) gives way to the newer one.
        map.Ruins.RemoveAll(r => r.coord == s.coord);
        map.Ruins.Add(ruin);
        map.Settlements.Remove(s);
        WorldCivilization.Rebuild(map, settings);
        WorldSites.RecomputeDanger(map);
        return ruin;
    }

    private static int NextId(WorldMap map) => map.Ruins.Count == 0 ? 0 : map.Ruins.Max(r => r.id) + 1;

    /// <summary>Share of the realm's land and settlement output left while the Capital's Composure is shaken (1 when Clouded or calmer).</summary>
    public static float CapitalOutput(WorldMap map, LossRules rules)
    {
        var capital = WorldCivilization.Capital(map);
        return capital == null ? 1f : Output(rules, capital);
    }

    /// <summary>What mending <paramref name="s"/> costs, as a multiple of <see cref="LossRules.mendCostPer10"/> (0 at or below the baseline).</summary>
    public static float MendScale(LossRules rules, Settlement s)
    {
        float above = s == null ? 0f : s.strain - rules.composure.baseline;
        return above <= 0.01f ? 0f : (float)Math.Ceiling(above / 10f - 1e-4f);
    }

    public static string CauseWords(string cause) =>
        cause == Withering ? "withered away, no road joining it to a hub" : cause == Pillage ? "pillaged"
        : cause == Cataclysm ? "in the Cataclysmic Aftermath that ended the world before" : "overrun by the dangers around it";

    // ===== RECLAIMING =====

    /// <summary>The ruin of one of your own settlements at <paramref name="coord"/> not yet reclaimed, or null (Old World ruins are no one's to reclaim).</summary>
    public static Ruin Reclaimable(WorldMap map, HexCoord coord)
    {
        var ruin = At(map, coord);
        return ruin != null && !ruin.ancient && !ruin.reclaimed ? ruin : null;
    }

    /// <summary>
    /// Track your land between two looks: cells held at <paramref name="before"/> but not <paramref name="now"/> join
    /// <paramref name="lost"/>; lost cells held again leave it. Returns how many were reclaimed.
    /// </summary>
    public static int TrackLand(ICollection<int> before, ICollection<int> now, List<int> lost)
    {
        var set = new HashSet<int>(lost);
        foreach (int c in before) if (!now.Contains(c)) set.Add(c);
        int back = set.RemoveWhere(now.Contains);
        lost.Clear();
        lost.AddRange(set.OrderBy(c => c));
        return back;
    }

    // ===== THE OLD WORLD =====

    /// <summary>
    /// Lay the Old World over a new map (once, with the world, deterministic from its seed; ruins are saved with the
    /// civilization, the roads laid again from the seed): ruins of its cities, towns and outposts spaced over dry land
    /// away from the Capital, and broken roads joining them (and the Capital's ground) as a spanning tree over land.
    /// </summary>
    public static void PlaceOldWorld(WorldMap map, WorldGenSettings settings, OldWorldRules rules)
    {
        if (map == null || rules == null) return;
        foreach (var t in map.Tiles) t.oldRoad = false;
        map.OldRoads.Clear();
        int stream = WorldNoise.Stream(map.seed, "old-world");
        var capital = map.Get(map.Capital);
        var sites = new List<WorldTile>();
        var candidates = map.Tiles.Where(t => !t.water && !t.impassable && !t.HasFeature && !t.sacred && t.enclave < 0 && t.settlement < 0
                && !float.IsPositiveInfinity(WorldPaths.StepCost(t, settings)) && map.StepsFromCapital(t.coord) >= Math.Max(1, rules.capitalDistance))
            .OrderBy(t => WorldNoise.Hash01(stream, t.index, 0)).ToList();
        foreach (var t in candidates)
        {
            if (sites.Count >= Math.Max(0, rules.ruins)) break;
            if (sites.Any(o => HexCoord.Distance(o.coord, t.coord) < Math.Max(1, rules.spacing))) continue;
            sites.Add(t);
        }
        var existing = new HashSet<HexCoord>(map.Ruins.Select(r => r.coord));
        for (int i = 0; i < sites.Count; i++)
        {
            var t = sites[i];
            if (existing.Contains(t.coord)) continue;
            double roll = WorldNoise.Hash01(stream, t.index, 1);
            var kind = i == 0 ? SettlementKind.Major : roll < 0.6 ? SettlementKind.Town : SettlementKind.Outpost;
            map.Ruins.Add(new Ruin
            {
                id = NextId(map),
                name = kind == SettlementKind.Major ? "an Old World city" : kind == SettlementKind.Town ? "an Old World town" : "an Old World outpost",
                settlement = -1,
                kind = kind,
                coord = t.coord,
                foundedAge = -1,
                fallenAge = -1,
                development = rules.minDevelopment + (float)WorldNoise.Hash01(stream, t.index, 2) * Math.Max(0f, rules.maxDevelopment - rules.minDevelopment),
                cause = Cataclysm,
                ancient = true,
            });
        }
        // The roads: a spanning tree over the ruins and the Capital's ground, each link the cheapest way over land.
        var nodes = new List<WorldTile>(sites);
        if (capital != null) nodes.Insert(0, capital);
        if (nodes.Count < 2) return;
        var joined = new List<WorldTile> { nodes[0] };
        var left = nodes.Skip(1).ToList();
        while (left.Count > 0)
        {
            var (from, to) = left.SelectMany(a => joined.Select(b => (a, b))).OrderBy(p => HexCoord.Distance(p.a.coord, p.b.coord)).ThenBy(p => p.a.index).First();
            left.Remove(from);
            joined.Add(from);
            if (!WorldPaths.Find(map, from.index, c => c == to.index, x => WorldPaths.StepCost(x, settings), out var path, out _, Math.Max(1f, rules.maxRoadCost))) continue;
            var cells = path.Where(c => c != capital?.index).ToList();
            foreach (int c in cells) map[c].oldRoad = true;
            map.OldRoads.Add(cells);
        }
        map.CivilizationVersion++;
    }

    /// <summary>Cells of a planned road that would restore Old World road (old road, no road yet) and those built anew.</summary>
    public static (int restored, int fresh) RoadCells(WorldMap map, List<int> path)
    {
        int restored = 0, fresh = 0;
        foreach (int c in path)
        {
            var t = map[c];
            if (t.road || t.settlement >= 0) continue;
            if (t.oldRoad) restored++;
            else fresh++;
        }
        return (restored, fresh);
    }

    /// <summary>What building a road along <paramref name="path"/> costs, as a multiple of the road cost per cell: fresh cells in full, old road restored at <see cref="OldWorldRules.restoreShare"/>.</summary>
    public static float RoadCostScale(WorldMap map, OldWorldRules rules, List<int> path)
    {
        var (restored, fresh) = RoadCells(map, path);
        return fresh + restored * Math.Max(0f, rules?.restoreShare ?? 1f);
    }

    /// <summary>Cells of <paramref name="route"/> only restored, not rebuilt yet.</summary>
    public static int RestoredCount(TradeRoute route) => route?.restored?.Count ?? 0;

    /// <summary>What rebuilding a route's restored stretches costs, as a multiple of the road cost per cell.</summary>
    public static float RebuildScale(OldWorldRules rules, TradeRoute route) => RestoredCount(route) * Math.Max(0f, 1f - (rules?.restoreShare ?? 1f));

    // ===== RUINS =====

    /// <summary>Why <paramref name="ruin"/> cannot be investigated, or null.</summary>
    public static string WhyNotInvestigate(Ruin ruin) => ruin == null ? "There are no ruins here." : ruin.investigated ? "These ruins have given up what they held." : null;

    /// <summary>
    /// What investigating <paramref name="ruin"/> turns up (deterministic per ruin): salvage of its former production at
    /// the development it had, plus a base; Research from its records; perhaps the Enlightenment of a technology (likelier
    /// in the Old World's ruins) and a civic its people's ways left behind (likelier the larger and more cultured it was).
    /// </summary>
    public static RuinFindings Findings(WorldMap map, SettlementRules rules, Ruin ruin)
    {
        var findings = new RuinFindings();
        if (ruin == null) return findings;
        var loss = rules?.loss ?? Default;
        var sums = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        void Add(string resource, float amount)
        {
            if (string.IsNullOrEmpty(resource) || amount <= 0f) return;
            sums[resource] = (sums.TryGetValue(resource, out var a) ? a : 0f) + amount;
        }
        foreach (var a in loss.salvageBase ?? new List<ResourceAmount>()) if (a != null) Add(a.resource, a.amount);
        float tens = Math.Max(0f, ruin.development) / 10f;
        foreach (var a in FormerYields(rules, ruin)) if (a != null) Add(a.resource, a.amount * tens * Math.Max(0f, loss.salvageSeconds));
        Add("Research", loss.researchBase + loss.researchPerDevelopment * Math.Max(0f, ruin.development));
        foreach (var pair in sums) findings.resources.Add(new ResourceAmount { resource = pair.Key, amount = (float)Math.Round(pair.Value, 1) });

        int stream = WorldNoise.Stream(map?.seed ?? 0, "ruin-findings");
        double enlighten = WorldNoise.Hash01(stream, ruin.id, 0), civic = WorldNoise.Hash01(stream, ruin.id, 1);
        findings.pick = WorldNoise.Hash01(stream, ruin.id, 2);
        findings.enlighten = enlighten < EnlightenChance(loss, ruin);
        findings.civic = civic < CivicChance(loss, rules, ruin);
        return findings;
    }

    public static float EnlightenChance(LossRules loss, Ruin ruin) =>
        Math.Max(0f, Math.Min(1f, loss.enlightenChance + loss.enlightenPerDevelopment * Math.Max(0f, ruin.development) + (ruin.ancient ? loss.oldWorldEnlighten : 0f)));

    /// <summary>The chance a ruin leaves a civic: more for a town, Major Settlement or haven, and for a Weaver, Regal or Auric district.</summary>
    public static float CivicChance(LossRules loss, SettlementRules rules, Ruin ruin)
    {
        float chance = loss.civicChance;
        if (ruin.kind == SettlementKind.Town || ruin.kind == SettlementKind.Major || ruin.kind == SettlementKind.Haven) chance += loss.civicSettlementBonus;
        var spec = ruin.district != null ? rules?.tributaries?.District(ruin.district) : null;
        if (spec != null && (spec.enclave == "Weaver" || spec.enclave == "Regal" || spec.enclave == "Auric")) chance += loss.civicCultureBonus;
        return Math.Max(0f, Math.Min(1f, chance));
    }

    // What the settlement produced per 10 development when it stood: its district's yields, or its kind's.
    private static IEnumerable<ResourceAmount> FormerYields(SettlementRules rules, Ruin ruin)
    {
        if (rules == null) return Enumerable.Empty<ResourceAmount>();
        switch (ruin.kind)
        {
            case SettlementKind.Tributary: return (rules.tributaries?.District(string.IsNullOrEmpty(ruin.district) ? rules.tributaries.generalist : ruin.district)?.yields) ?? new List<ResourceAmount>();
            case SettlementKind.Town: return rules.townYields ?? new List<ResourceAmount>();
            case SettlementKind.Major: return rules.majorYields ?? new List<ResourceAmount>();
            case SettlementKind.Haven: return rules.havenYields ?? new List<ResourceAmount>();
            default: return Enumerable.Empty<ResourceAmount>();
        }
    }

    /// <summary>Pick an item by a roll (0-1) from <paramref name="candidates"/> (sorted by the caller), or null when there are none.</summary>
    public static string Pick(IReadOnlyList<string> candidates, double roll) =>
        candidates == null || candidates.Count == 0 ? null : candidates[Math.Min(candidates.Count - 1, (int)(Math.Max(0.0, roll) * candidates.Count))];
}
