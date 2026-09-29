using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a table does, by its policy (numbers from <see cref="HospitalityTuning"/>).</summary>
public struct TableEffects
{
    public float unity, relief, joy, coverage, exposure;
    /// <summary>Neighbours by open road come (public welcome): customs are exchanged with them.</summary>
    public bool reachesNeighbours;
    /// <summary>A luxury on this table counts as shared openly (not a patron's private table).</summary>
    public bool sharesLuxury;
    /// <summary>The patron earns standing (when their rest allows).</summary>
    public bool rewardsPatron;
}

/// <summary>One settlement's access to shared tables and luxuries (a row of <see cref="AccessReport"/>).</summary>
public sealed class AccessRow
{
    public int settlement;
    public string name;
    public int lastTable, lastShared, lastLuxuryShared, lastPatron, tables;
    /// <summary>How far shared tables reach its people now, 0-1 (open table in the window: 1; only a patron's: less; none: 0).</summary>
    public float reach;
    /// <summary>Luxury categories shared openly there within the window, and those served there only at a patron's table.</summary>
    public string[] shared, privateOnly;
}

/// <summary>Who has a place at the table, settlement by settlement, and where luxuries stop (a visible access problem).</summary>
public sealed class AccessReport
{
    public List<AccessRow> rows = new List<AccessRow>();
    /// <summary>Share of standing settlements reached by shared tables within the window, 0-1.</summary>
    public float coverage;
    /// <summary>Luxury foods held in the stores while no open table has shared a luxury within the window ("12 Honeyed Peach Tart").</summary>
    public List<string> hoarded = new List<string>();
    /// <summary>Plain sentences naming each access problem (empty: none).</summary>
    public List<string> problems = new List<string>();
}

/// <summary>
/// Communal tables with no scene (tested in <c>HospitalityTests</c>). Research inferences (R5 eating together has its
/// own social value, R6 local arrangements differ) shape the rules; every number is a proposal
/// (<see cref="HospitalityTuning"/>).
/// - A table is sized in food value by its policy and the settlement's development (the festival's own measure, not a
///   head count), and served from exact finished foods (<see cref="TableServing"/>).
/// - Policies differ in consequence: an open welcome reaches everyone and neighbours by road (customs exchanged), a
///   recovery table eases a strained community most, a patron's table gives the patron standing and Unity but reaches
///   few, and a luxury served there stays private.
/// - Coverage is per settlement, never per head (the simulation keeps no local population): a settlement is reached
///   for a while after its table. Repeating tables in one place never raises it past reached.
/// - Access: luxuries held but not shared, served only privately, or shared in some places and not others are named.
/// </summary>
public static class HospitalityRules
{
    public static HospitalityState Ensure(HospitalityState state)
    {
        if (state == null) state = new HospitalityState();
        if (state.tables == null) state.tables = new List<TableRecord>();
        if (state.settlements == null) state.settlements = new List<SettlementTables>();
        if (state.patrons == null) state.patrons = new List<PatronRecord>();
        foreach (var t in state.tables.Where(t => t != null))
        {
            if (t.served == null) t.served = new List<ServedLine>();
            foreach (var l in t.served.Where(l => l != null)) if (l.luxuries == null) l.luxuries = new List<string>();
            if (t.groups == null) t.groups = new List<string>();
            if (t.guestsFrom == null) t.guestsFrom = new List<int>();
            if (t.legends == null) t.legends = new List<string>();
        }
        foreach (var s in state.settlements.Where(s => s != null))
        {
            if (s.sharedLuxuries == null) s.sharedLuxuries = new List<string>();
            if (s.privateLuxuries == null) s.privateLuxuries = new List<string>();
        }
        return state;
    }

    public static string PolicyName(TablePolicy p) =>
        p == TablePolicy.PublicWelcome ? "Public welcome" : p == TablePolicy.RecoverySupport ? "Recovery support" : "Patron-hosted";

    public static string PolicyText(TablePolicy p) =>
        p == TablePolicy.PublicWelcome ? "an open table: anyone may come, neighbours by road too, and customs pass between them"
        : p == TablePolicy.RecoverySupport ? "a table for a community under strain: it eases its Composure most"
        : "a Legend's table for their own guests: standing for the patron, but few of the town have a place at it";

    /// <summary>Food value a table sets out: its policy's base and a little more for a developed settlement.</summary>
    public static float TableSize(TablePolicy policy, float development, HospitalityTuning t)
    {
        t = t ?? new HospitalityTuning();
        float baseValue = policy == TablePolicy.PublicWelcome ? t.publicFoodValue : policy == TablePolicy.RecoverySupport ? t.recoveryFoodValue : t.patronFoodValue;
        return Math.Max(0f, baseValue + Math.Max(0f, development) * Math.Max(0f, t.foodValuePerDevelopment));
    }

    /// <summary>What a table of this policy does (a luxury among its foods, the Feast of Abundance in force).</summary>
    public static TableEffects Effects(TablePolicy policy, bool luxury, bool abundance, HospitalityTuning t)
    {
        t = t ?? new HospitalityTuning();
        switch (policy)
        {
            case TablePolicy.PublicWelcome:
                return new TableEffects
                {
                    unity = t.publicUnity + (abundance ? t.abundanceUnity : 0f), relief = t.publicRelief, joy = t.publicJoy + (luxury ? t.luxuryJoy : 0f),
                    coverage = t.publicCoverage, exposure = t.tableExposure, reachesNeighbours = true, sharesLuxury = luxury,
                };
            case TablePolicy.RecoverySupport:
                return new TableEffects
                {
                    unity = t.recoveryUnity, relief = t.recoveryRelief, joy = t.recoveryJoy + (luxury ? t.luxuryJoy : 0f), coverage = t.recoveryCoverage, sharesLuxury = luxury,
                };
            default:
                return new TableEffects { unity = t.patronUnity, relief = t.patronRelief, joy = t.patronJoy, coverage = t.patronCoverage, rewardsPatron = true };
        }
    }

    public static SettlementTables Summary(HospitalityState state, int settlement) => state?.settlements.FirstOrDefault(s => s != null && s.settlement == settlement);

    public static PatronRecord Patron(HospitalityState state, string legend) =>
        string.IsNullOrEmpty(legend) ? null : state?.patrons.FirstOrDefault(p => p != null && string.Equals(p.legend, legend, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sevenths before <paramref name="settlement"/> may set another table (0: now).</summary>
    public static int Wait(HospitalityState state, int settlement, int now, HospitalityTuning t)
    {
        var s = Summary(state, settlement);
        if (s == null || s.lastTable < 0) return 0;
        return Math.Max(0, s.lastTable + Math.Max(0, t.cooldownSevenths) - now);
    }

    /// <summary>The patron is owed standing for a table now (their last reward was long enough ago).</summary>
    public static bool PatronRewardDue(HospitalityState state, string legend, int now, HospitalityTuning t)
    {
        var p = Patron(state, legend);
        return p == null || p.lastRewarded < 0 || now - p.lastRewarded >= Math.Max(1, t.patronRestSevenths);
    }

    /// <summary>A table was set: kept in the history (bounded), in its settlement's summary and its patron's record.</summary>
    public static void Record(HospitalityState state, TableRecord table, bool patronRewarded, HospitalityTuning t)
    {
        if (state == null || table == null) return;
        state.tables.Add(table);
        int keep = Math.Max(1, t.tablesKept);
        if (state.tables.Count > keep) state.tables.RemoveRange(0, state.tables.Count - keep);
        var s = Summary(state, table.settlement);
        if (s == null) state.settlements.Add(s = new SettlementTables { settlement = table.settlement });
        s.name = table.place ?? s.name;
        s.tables++;
        s.lastTable = table.seventh;
        s.foodValue += table.foodValue;
        var categories = table.served.Where(l => l != null).SelectMany(l => l.luxuries ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (table.policy == TablePolicy.PatronHosted)
        {
            s.patronTables++;
            s.lastPatron = table.seventh;
            if (categories.Count > 0) s.privateLuxuries = categories;
            var p = Patron(state, table.patron);
            if (p == null && !string.IsNullOrEmpty(table.patron)) state.patrons.Add(p = new PatronRecord { legend = table.patron });
            if (p != null)
            {
                p.tables++;
                p.lastSeventh = table.seventh;
                p.foodValue += table.foodValue;
                if (patronRewarded) p.lastRewarded = table.seventh;
            }
        }
        else
        {
            if (table.policy == TablePolicy.PublicWelcome) s.publicTables++;
            else s.recoveryTables++;
            s.lastShared = table.seventh;
            if (categories.Count > 0)
            {
                s.lastLuxuryShared = table.seventh;
                s.sharedLuxuries = categories;
            }
        }
    }

    private static bool Within(int then, int now, HospitalityTuning t) => then >= 0 && now - then < Math.Max(1, t.coverageWindowSevenths);

    /// <summary>How far shared tables reach a settlement's people now, 0-1.</summary>
    public static float Reach(SettlementTables s, int now, HospitalityTuning t)
    {
        if (s == null) return 0f;
        if (Within(s.lastShared, now, t)) return Math.Min(1f, Math.Max(0f, Math.Max(t.publicCoverage, t.recoveryCoverage)));
        if (Within(s.lastPatron, now, t)) return Math.Min(1f, Math.Max(0f, t.patronCoverage));
        return 0f;
    }

    /// <summary>Share of the standing settlements reached by shared tables now, 0-1 (0 with none standing).</summary>
    public static float Coverage(HospitalityState state, ICollection<int> standing, int now, HospitalityTuning t)
    {
        if (standing == null || standing.Count == 0) return 0f;
        return standing.Sum(id => Reach(Summary(state, id), now, t)) / standing.Count;
    }

    /// <summary>Living (0-1) coverage adds to happiness: bounded by <see cref="HospitalityTuning.coverageLiving"/>.</summary>
    public static float LivingFrom(float coverage, HospitalityTuning t) => Math.Max(0f, t.coverageLiving) * Math.Max(0f, Math.Min(1f, coverage));

    /// <summary>
    /// Who has a place at the table: each standing settlement's reach and the luxuries shared or kept private there,
    /// the luxuries held but not shared, and the problems in plain words.
    /// </summary>
    public static AccessReport Access(HospitalityState state, IEnumerable<(int id, string name)> standing, int now,
        IEnumerable<(string resource, float amount)> heldLuxuries, HospitalityTuning t)
    {
        t = t ?? new HospitalityTuning();
        var report = new AccessReport();
        var places = (standing ?? Enumerable.Empty<(int, string)>()).ToList();
        foreach (var (id, name) in places)
        {
            var s = Summary(state, id);
            bool sharedLux = s != null && Within(s.lastLuxuryShared, now, t);
            bool privateLux = s != null && Within(s.lastPatron, now, t) && s.privateLuxuries.Count > 0;
            report.rows.Add(new AccessRow
            {
                settlement = id, name = name, tables = s?.tables ?? 0, lastTable = s?.lastTable ?? -1, lastShared = s?.lastShared ?? -1,
                lastLuxuryShared = s?.lastLuxuryShared ?? -1, lastPatron = s?.lastPatron ?? -1, reach = Reach(s, now, t),
                shared = sharedLux ? s.sharedLuxuries.ToArray() : new string[0],
                privateOnly = privateLux ? s.privateLuxuries.Where(c => !sharedLux || !s.sharedLuxuries.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray() : new string[0],
            });
        }
        report.coverage = places.Count == 0 ? 0f : report.rows.Sum(r => r.reach) / places.Count;
        int window = Math.Max(1, t.coverageWindowSevenths);
        bool anyOpenLuxury = report.rows.Any(r => r.shared.Length > 0);
        if (!anyOpenLuxury)
            report.hoarded = (heldLuxuries ?? Enumerable.Empty<(string, float)>()).Where(h => h.amount >= 1f).OrderByDescending(h => h.amount)
                .Select(h => $"{h.amount:0} {h.resource}").ToList();
        if (report.hoarded.Count > 0)
            report.problems.Add($"Held but not shared: {string.Join(", ", report.hoarded.Take(4))}. No open table has served a luxury in {window} Sevenths.");
        foreach (var r in report.rows.Where(r => r.privateOnly.Length > 0))
            report.problems.Add($"In {r.name}, {string.Join(" and ", r.privateOnly)} reached only a patron's guests.");
        if (anyOpenLuxury)
        {
            var without = report.rows.Where(r => r.shared.Length == 0).Select(r => r.name).ToList();
            if (without.Count > 0)
                report.problems.Add($"Luxuries were shared openly in {string.Join(", ", report.rows.Where(r => r.shared.Length > 0).Select(r => r.name))}, not in {string.Join(", ", without)}.");
        }
        var unreached = report.rows.Where(r => r.reach <= 0f).Select(r => r.name).ToList();
        if (unreached.Count > 0 && unreached.Count < report.rows.Count)
            report.problems.Add($"No shared table in {window} Sevenths: {string.Join(", ", unreached)}.");
        return report;
    }

    /// <summary>
    /// A served food's name now: the current dish of its recipe when it has one (<paramref name="dishOf"/>: recipe id to
    /// its dish, null when unknown), so a renamed recipe keeps its tables; else the name it was served under.
    /// </summary>
    public static string ServedName(ServedLine l, Func<string, string> dishOf)
    {
        if (l == null) return "an unknown food";
        string dish = !string.IsNullOrEmpty(l.recipe) ? dishOf?.Invoke(l.recipe) : null;
        return !string.IsNullOrEmpty(dish) ? dish : l.resource ?? "an unknown food";
    }

    /// <summary>"4 Peach Soup and 2 Rootgrain Ale".</summary>
    public static string MenuText(IEnumerable<(string name, float amount)> lines) =>
        string.Join(" and ", (lines ?? Enumerable.Empty<(string, float)>()).Select(l => $"{l.amount:0.#} {l.name}"));
}
