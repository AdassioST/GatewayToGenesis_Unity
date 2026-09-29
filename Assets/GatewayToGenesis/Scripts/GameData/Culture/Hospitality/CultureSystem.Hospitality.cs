using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>A finished food the stores could set on a table now.</summary>
public sealed class ServableFood
{
    public string resource;
    /// <summary>The recipe that makes it, by id (null: none).</summary>
    public string recipe;
    public float held, foodValue;
    public FoodClass cuisine;
    public bool invented, national;
    /// <summary>The luxury categories it belongs to (empty: a plain food).</summary>
    public string[] luxuries;

    public bool Luxury => luxuries != null && luxuries.Length > 0;
}

/// <summary>What setting a table would do, computed without changing anything (<see cref="CultureSystem.PreviewTable"/>).</summary>
public sealed class TablePreview
{
    public bool ok;
    /// <summary>Why it cannot be set now, or (when it can) what it will do.</summary>
    public string reason;
    public TablePolicy policy;
    public int settlement = -1;
    public string place, patron;
    /// <summary>The portions planned: resource, amount and food value of each.</summary>
    public List<(string resource, float amount, float foodValue)> portions = new List<(string, float, float)>();
    public float foodValue, storesBefore, storesAfter, reserve;
    /// <summary>The people and groups represented, as far as they are known.</summary>
    public List<string> groups = new List<string>();
    public List<(int id, string name)> guests = new List<(int, string)>();
    public List<string> legends = new List<string>();
    public TableEffects effects;
    public bool luxury, patronReward;

    internal ServingPlan plan;
}

/// <summary>A table that was set, as a snapshot.</summary>
public sealed class TableView
{
    public string key, place, patron, text;
    public TablePolicy policy;
    public int settlement, seventh;
    public float foodValue;
    public bool luxuryShared;
    /// <summary>What was served: its name now (a renamed recipe shows its new name), portions and food value.</summary>
    public (string name, float amount, float foodValue, bool luxury)[] served;
    public string[] groups, legends;
}

/// <summary>
/// Hospitality, redistribution and access to culture (T05; rules in <see cref="HospitalityRules"/>, serving through
/// <see cref="TableServing"/>, saved in <see cref="CultureExtensionState.hospitality"/>). "A feast changes who belongs
/// at the table."
///
/// - A communal table is set in one settlement under a policy (public welcome, recovery support, patron-hosted) from one
///   to three finished foods, authored or the people's own. Its preview shows the portions, the groups represented
///   where known, what the stores will lose and the reserve kept for survival; nothing is paid until it is set.
/// - Serving takes those exact foods, all or nothing, and records their use once (foodways, national familiarity). The
///   same portions can no longer be a luxury draw or feed a shortfall, and a table is its own occurrence (Hospitality),
///   never also a feast or ordinary consumption.
/// - Consequences differ by policy, and a table's history, coverage and access report make who was left out visible.
/// </summary>
public partial class CultureSystem
{
    /// <summary>Hospitality's numbers (code defaults: every number is a proposal).</summary>
    public static readonly HospitalityTuning HospitalityDefaults = new HospitalityTuning();
    public HospitalityTuning HospitalityTuning => HospitalityDefaults;

    private HospitalityState HospitalityData
    {
        get
        {
            if (_state.extensions == null) _state.extensions = new CultureExtensionState();
            return _state.extensions.hospitality ?? (_state.extensions.hospitality = HospitalityRules.Ensure(null));
        }
    }

    private static IServingStores Stores => PantryServingStores.Instance;

    // ===== WHAT CAN BE SERVED =====

    /// <summary>The recipe that makes a resource, by id (null: none makes it).</summary>
    public string RecipeIdOf(string resource) => RecipeOfDish(resource)?.id;

    /// <summary>The luxury categories a resource belongs to.</summary>
    public string[] LuxuryCategoriesOf(string resource) =>
        string.IsNullOrEmpty(resource) ? new string[0]
            : Life.luxuries.Where(l => l != null && l.resources != null && l.resources.Contains(resource, StringComparer.OrdinalIgnoreCase)).Select(l => l.category).ToArray();

    /// <summary>The finished foods held now that a table could serve (edibles, teas, beverages; never ingredients or spices).</summary>
    public IReadOnlyList<ServableFood> ServableFoods()
    {
        var list = new List<ServableFood>();
        var pantry = Pantry.Instance;
        if (pantry == null) return list;
        foreach (var s in pantry.Stock())
        {
            if (s.amount < 0.1f || TableServing.WhyNotServable(s.kind.resource, s.kind) != null) continue;
            list.Add(new ServableFood
            {
                resource = s.kind.resource, recipe = RecipeIdOf(s.kind.resource), held = s.amount, foodValue = s.kind.foodValue, cuisine = s.kind.cuisine,
                invented = IsInvented(s.kind.resource), national = IsNationalFood(s.kind.resource), luxuries = LuxuryCategoriesOf(s.kind.resource),
            });
        }
        return list.OrderByDescending(f => f.national).ThenByDescending(f => f.invented).ThenByDescending(f => f.Luxury).ThenBy(f => f.resource, StringComparer.Ordinal).ToList();
    }

    /// <summary>The Legends who could host a table now: met, not lost, and at home (not away with an expedition).</summary>
    public IReadOnlyList<string> PatronChoices() =>
        LegendLeaderLogic.Instance != null ? LegendLeaderLogic.Instance.GetAvailableLegends().Where(l => l != null).Select(l => l.legendName).ToList() : new List<string>();

    /// <summary>Food value the stores keep for survival now (no table draws below it).</summary>
    public float SurvivalReserve => Mathf.Max(0f, (PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.TotalPeople : 0) * HospitalityTuning.reservePerCitizen);

    private static bool Hungry => PopGrowthLogic.Instance != null && PopGrowthLogic.Instance.isFoodScarce;

    // ===== A TABLE'S SERVING =====

    /// <summary>Plan serving <paramref name="foodValue"/> of <paramref name="menu"/> (exact finished foods): nothing is taken.</summary>
    public ServingPlan PlanServing(IList<string> menu, float foodValue) => TableServing.Plan(menu, foodValue, Stores, HospitalityTuning.maxMenu);

    /// <summary>Why <paramref name="plan"/> cannot be served now (hunger, stock, the survival reserve), or null.</summary>
    public string WhyNotServe(ServingPlan plan) => TableServing.WhyNot(plan, Stores, SurvivalReserve, Hungry);

    /// <summary>
    /// Serve <paramref name="plan"/>: the table's checks again, then the culture's serving boundary takes every food or
    /// none (<see cref="Serve(IEnumerable{ResourceAmount}, CulturalOccurrence)"/>), telling the foodways and national
    /// familiarity of the use once; nothing is reported as eaten. The plan is settled on what was actually taken. The
    /// caller records its own occurrences.
    /// </summary>
    private bool ServeTable(ServingPlan plan, out string why)
    {
        why = WhyNotServe(plan);
        if (why != null) return false;
        var served = Serve(plan.Items());
        if (!served.succeeded) { why = served.reason; return false; }
        TableServing.Settle(plan, served.paid);
        return true;
    }

    // ===== PREVIEW =====

    private static string PolicyId(TablePolicy p) => p == TablePolicy.PublicWelcome ? "public-welcome" : p == TablePolicy.RecoverySupport ? "recovery-support" : "patron-hosted";

    /// <summary>What setting this table would do, or why it cannot be set now. Nothing is changed.</summary>
    public TablePreview PreviewTable(TableRequest request)
    {
        var p = new TablePreview { policy = request?.policy ?? TablePolicy.PublicWelcome, settlement = request?.settlement ?? -1, patron = request?.patron };
        var t = HospitalityTuning;
        string Fail(string why) { p.ok = false; p.reason = why; return why; }
        if (request == null) { Fail("Nothing asked."); return p; }
        if (!_state.founded) { Fail("The culture has not been founded yet."); return p; }
        var world = WorldSystem.Instance;
        var map = world != null ? world.Map : null;
        var s = map != null ? WorldCivilization.Get(map, request.settlement) : null;
        if (s == null) { Fail("Choose one of your settlements."); return p; }
        p.place = s.name;
        var hospitality = HospitalityData;
        int now = _state.sevenths;

        // Who is represented, as far as it is known.
        var contact = world.CulturalContact();
        if (request.policy == TablePolicy.PatronHosted)
        {
            if (!string.IsNullOrEmpty(request.patron))
            {
                p.groups.Add($"{request.patron} and their guests (who they were is not recorded)");
                p.legends.Add(request.patron);
            }
        }
        else
        {
            p.groups.Add($"the people of {s.name}");
            p.groups.AddRange(ArrivalGroups(s.id, now - Mathf.Max(1, t.coverageWindowSevenths)));
            foreach (var unit in map.Units.Where(u => u != null && u.coord == s.coord))
                foreach (string member in Expeditions.Members(unit).Where(m => !string.IsNullOrEmpty(m) && !p.legends.Contains(m))) p.legends.Add(member);
            if (p.legends.Count > 0) p.groups.Add($"{string.Join(", ", p.legends)}, whose parties stand in {s.name}");
            if (request.policy == TablePolicy.PublicWelcome)
            {
                p.guests = contact.OpenNeighbours(s.id).Select(id => (id, contact.NameOf(id))).ToList();
                if (p.guests.Count > 0) p.groups.Add($"guests by open road from {string.Join(", ", p.guests.Select(g => g.name))}");
            }
        }

        // What it takes, and what it does.
        float size = HospitalityRules.TableSize(request.policy, s.development, t);
        p.plan = PlanServing(request.menu, size);
        foreach (var item in p.plan.items) p.portions.Add((item.resource, item.amount, item.foodValue));
        p.foodValue = p.plan.FoodValue;
        p.storesBefore = Pantry.StoredValue;
        p.storesAfter = p.storesBefore - p.foodValue;
        p.reserve = SurvivalReserve;
        p.luxury = p.plan.items.Any(i => LuxuryCategoriesOf(i.resource).Length > 0);
        bool abundance = CivicManager.Instance != null && !string.IsNullOrEmpty(t.abundanceCivic) && CivicManager.Instance.IsCivicActive(t.abundanceCivic);
        p.effects = HospitalityRules.Effects(request.policy, p.luxury, abundance, t);
        p.patronReward = p.effects.rewardsPatron && !string.IsNullOrEmpty(request.patron) && t.patronFragments > 0 && HospitalityRules.PatronRewardDue(hospitality, request.patron, now, t);

        // Why not, in the order the player meets it.
        int wait = HospitalityRules.Wait(hospitality, s.id, now, t);
        if (wait > 0) { Fail($"{s.name} set a table not long ago: {wait} more Seventh{(wait == 1 ? "" : "s")}."); return p; }
        if (request.policy == TablePolicy.RecoverySupport && s.strain + 1e-4f < t.recoveryStrain)
        {
            Fail($"{s.name} is not under strain (Composure strain {s.strain:0}; recovery support needs {t.recoveryStrain:0}): set a public welcome instead.");
            return p;
        }
        if (request.policy == TablePolicy.PatronHosted)
        {
            if (string.IsNullOrEmpty(request.patron)) { Fail("Choose the Legend who hosts."); return p; }
            if (!PatronChoices().Contains(request.patron, StringComparer.OrdinalIgnoreCase)) { Fail($"{request.patron} cannot host now: lost, away with an expedition, or not yet met."); return p; }
        }
        string why = WhyNotServe(p.plan);
        if (why != null) { Fail(why); return p; }
        p.ok = true;
        p.reason = PreviewText(p, s);
        return p;
    }

    // Recent arrivals at a settlement, grouped by what is known of their origin (nothing is guessed).
    private List<string> ArrivalGroups(int settlement, int since)
    {
        var recent = ArrivalsAt(settlement).Where(a => a.seventh > since).ToList();
        var groups = new List<string>();
        foreach (var g in recent.Where(a => a.originKind == ArrivalOriginKind.Settlement).GroupBy(a => a.originLabel ?? "one of your settlements"))
            groups.Add($"{g.Sum(a => a.people)} newly arrived from {g.Key}");
        int place = recent.Where(a => a.originKind == ArrivalOriginKind.Place).Sum(a => a.people);
        if (place > 0) groups.Add($"{place} survivor{(place == 1 ? "" : "s")} found in the wilds (their customs not recorded)");
        int unknown = recent.Where(a => a.originKind == ArrivalOriginKind.Unknown).Sum(a => a.people);
        if (unknown > 0) groups.Add($"{unknown} recent arrival{(unknown == 1 ? "" : "s")} of unknown origin");
        return groups;
    }

    private string PreviewText(TablePreview p, Settlement s)
    {
        var e = p.effects;
        var parts = new List<string>
        {
            $"Serves {HospitalityRules.MenuText(p.portions.Select(x => (x.resource, x.amount)))}: the stores lose {p.foodValue:0.#} food value ({p.storesBefore:0.#} to {p.storesAfter:0.#}; {p.reserve:0.#} kept for survival).",
        };
        var gains = new List<string>();
        if (e.unity > 0f) gains.Add($"+{e.unity:0.#} Unity");
        if (e.relief > 0f) gains.Add($"{s.name}'s strain eased by {e.relief:0.#}");
        if (e.joy > 0f) gains.Add($"joy +{e.joy * 100f:0}");
        gains.Add(e.coverage >= 1f ? $"all of {s.name} has a place at it" : $"it reaches few of {s.name} ({e.coverage:P0})");
        if (e.reachesNeighbours && p.guests.Count > 0) gains.Add($"customs pass between {s.name} and {string.Join(", ", p.guests.Select(g => g.name))}");
        if (p.patronReward) gains.Add($"{p.patron} earns {HospitalityTuning.patronFragments} Meaning fragment{(HospitalityTuning.patronFragments == 1 ? "" : "s")} of standing");
        else if (e.rewardsPatron && !string.IsNullOrEmpty(p.patron)) gains.Add($"{p.patron}'s standing was earned lately (not again for now)");
        if (p.luxury) gains.Add(e.sharesLuxury ? "a luxury shared openly" : "the luxury stays with the patron's guests");
        parts.Add(string.Join("; ", gains) + ".");
        return string.Join(" ", parts);
    }

    // ===== SETTING THE TABLE =====

    /// <summary>
    /// Set a communal table: checked again, the exact foods taken (all or nothing), the policy's consequences applied,
    /// its history kept and one Hospitality occurrence recorded per food served. Nothing is paid or granted when it fails.
    /// </summary>
    public CultureCommandResult SetTable(TableRequest request)
    {
        var p = PreviewTable(request);
        if (!p.ok) return CultureCommandResult.Fail(p.reason);
        var t = HospitalityTuning;
        var map = WorldSystem.Instance.Map;
        var s = WorldCivilization.Get(map, request.settlement);
        if (!ServeTable(p.plan, out string why)) return CultureCommandResult.Fail(why);

        var hospitality = HospitalityData;
        string key = "table-" + (++hospitality.serial);
        var lines = p.plan.items.Select(i => new ServedLine
        {
            resource = i.resource, recipe = RecipeIdOf(i.resource), amount = i.amount, foodValue = i.foodValue,
            luxuries = LuxuryCategoriesOf(i.resource).ToList(), invented = IsInvented(i.resource), national = IsNationalFood(i.resource),
        }).ToList();
        var e = p.effects;
        var record = new TableRecord
        {
            key = key, policy = request.policy, settlement = s.id, place = s.name, patron = request.policy == TablePolicy.PatronHosted ? request.patron : null,
            served = lines, groups = new List<string>(p.groups), guestsFrom = p.guests.Select(g => g.id).ToList(), legends = new List<string>(p.legends),
            seventh = _state.sevenths, ageId = AgeIdNow, foodValue = lines.Sum(l => l.foodValue), luxuryShared = e.sharesLuxury && lines.Any(l => l.Luxury),
        };
        record.text = $"{HospitalityRules.PolicyName(request.policy)} in {s.name}{(record.patron != null ? $", hosted by {record.patron}" : string.Empty)}: {HospitalityRules.MenuText(lines.Select(l => (l.resource, l.amount)))}";

        // The policy's consequences.
        GainUnity(e.unity);
        s.strain = Mathf.Max(0f, s.strain - Mathf.Max(0f, e.relief));
        AddJoy(e.joy);
        Lived("Indulgent", 0.3f);
        bool rewarded = p.patronReward && LegendProgress.Instance != null;
        if (rewarded) LegendProgress.Instance.Award(record.patron, FragmentKind.Meaning, t.patronFragments, $"A table for their guests in {s.name}");
        HospitalityRules.Record(hospitality, record, rewarded, t);
        var contact = e.reachesNeighbours ? LocalCultureRules.Table(Local, key, s.id, s.name, p.guests, e.exposure, _state.sevenths, AgeIdNow, AgeNow, LocalTuning) : new List<LocalChange>();

        var result = CultureCommandResult.Ok();
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var o = new CulturalOccurrence
            {
                key = $"hospitality:{key}:{i}", kind = CulturalOccurrenceKind.Hospitality, stamp = Stamp(), settlement = s.id,
                subject = CultureEntityRef.Of(CultureEntityKind.Resource, l.resource, l.resource),
                recipe = string.IsNullOrEmpty(l.recipe) ? new CultureEntityRef() : CultureEntityRef.Of(CultureEntityKind.Recipe, l.recipe, l.resource),
                source = CultureEntityRef.Of(CultureEntityKind.Activity, "communal-table:" + PolicyId(request.policy), HospitalityRules.PolicyName(request.policy)),
                actors = new List<string>(record.legends), quantity = l.amount, unit = CultureQuantityUnit.Portions, text = record.text,
            };
            if (Record(o)) result.occurrences.Add(o.key);
            result.paid.Add(new ResourceAmount { resource = l.resource, amount = l.amount });
        }
        string contactText = contact.Count > 0 ? $" Guests and hosts met {contact.Count} custom{(contact.Count == 1 ? "" : "s")} of each other's." : string.Empty;
        result.reason = $"{record.text}. {p.reason}{contactText}";
        if (hospitality.serial == 1) Remember("hospitality", "The first shared table", $"{Capital(PeopleWord)} set a table in {s.name} and shared {HospitalityRules.MenuText(lines.Select(l => (l.resource, l.amount)))}.");
        GameLog.Event($"Communal table ({PolicyId(request.policy)}) in {s.name}: {record.foodValue:0.#} food value.", Log);
        NotificationFeed.Push($"A table in {s.name}", result.reason, NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:" + key);
        RaiseChanged();
        return result;
    }

    // ===== READS (snapshots) =====

    /// <summary>A served food's name now: a recipe's current dish when it has one (a renamed recipe keeps its tables), else the name it was served under.</summary>
    public string ServedName(ServedLine l) => HospitalityRules.ServedName(l, id => Life.Recipe(id)?.dish);

    private TableView View(TableRecord r)
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        var standing = map != null ? WorldCivilization.Get(map, r.settlement) : null;
        return new TableView
        {
            key = r.key, policy = r.policy, settlement = r.settlement, place = standing != null ? standing.name : r.place, patron = r.patron, text = r.text,
            seventh = r.seventh, foodValue = r.foodValue, luxuryShared = r.luxuryShared,
            served = r.served.Where(l => l != null).Select(l => (ServedName(l), l.amount, l.foodValue, l.Luxury)).ToArray(),
            groups = r.groups.ToArray(), legends = r.legends.ToArray(),
        };
    }

    /// <summary>The latest tables, newest first.</summary>
    public IReadOnlyList<TableView> RecentTables(int max = 20) =>
        HospitalityData.tables.Where(r => r != null).Reverse().Take(Mathf.Max(0, max)).Select(View).ToList();

    /// <summary>The tables set in one settlement (those still recorded one by one), newest first.</summary>
    public IReadOnlyList<TableView> TablesAt(int settlement) =>
        HospitalityData.tables.Where(r => r != null && r.settlement == settlement).Reverse().Select(View).ToList();

    private List<(int id, string name)> StandingSettlements()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        return map == null ? new List<(int, string)>() : map.Settlements.Where(s => s != null).Select(s => (s.id, s.name)).ToList();
    }

    /// <summary>Share of your settlements reached by shared tables lately, 0-1.</summary>
    public float SharedTableCoverage() =>
        HospitalityRules.Coverage(HospitalityData, StandingSettlements().Select(s => s.id).ToList(), _state.sevenths, HospitalityTuning);

    /// <summary>Who has a place at the table, settlement by settlement, and where luxuries stop.</summary>
    public AccessReport TableAccess()
    {
        var held = new List<(string, float)>();
        var pantry = Pantry.Instance;
        if (pantry != null)
            foreach (var s in pantry.Stock())
                if (s.amount > 0f && TableServing.WhyNotServable(s.kind.resource, s.kind) == null && LuxuryCategoriesOf(s.kind.resource).Length > 0) held.Add((s.kind.resource, s.amount));
        return HospitalityRules.Access(HospitalityData, StandingSettlements(), _state.sevenths, held, HospitalityTuning);
    }

    // ===== THE SEVENTH =====

    internal void ReconcileHospitality()
    {
        if (_state.extensions == null) _state.extensions = new CultureExtensionState();
        _state.extensions.hospitality = HospitalityRules.Ensure(_state.extensions.hospitality);
    }
}

/// <summary>Hospitality's reads for the culture's queries: snapshots only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<ServableFood> ServableFoods();
    TablePreview PreviewTable(TableRequest request);
    IReadOnlyList<TableView> RecentTables(int max = 20);
    IReadOnlyList<TableView> TablesAt(int settlement);
    AccessReport TableAccess();
    float SharedTableCoverage();
}

/// <summary>Hospitality in the Seventh's social effects: its saved state kept whole (tables act when set, not per Seventh).</summary>
public sealed class HospitalityFeature : ICultureFeature
{
    public string Id => "hospitality";
    public CulturePhase Phase => CulturePhase.SocialEffects;
    public int Order => 20;

    public void Seventh(CultureSeventh context) => context?.Culture?.ReconcileHospitality();

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileHospitality();
}
