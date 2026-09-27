using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One place that answers "what is the current value of X?" for every gameplay condition:
/// event conditions, chorus requirements, weather conditions, civic requirements and Ink functions.
///
/// A value is addressed by a domain and a target, e.g. ("stat", "waltz"), ("resource", "Food"),
/// ("technology", "Horology") or ("population", ""). Adding a new kind of condition anywhere in the
/// game means registering one resolver here; every consumer understands it immediately.
/// Booleans resolve to 1 (true) or 0 (false).
/// </summary>
public static class GameValues
{
    public delegate bool Resolver(string target, out float value);

    private static readonly Dictionary<string, Resolver> Resolvers = new Dictionary<string, Resolver>(StringComparer.OrdinalIgnoreCase);

    static GameValues()
    {
        Register("stat", (string t, out float v) => Stat(t, out v));
        Register("resource", (string t, out float v) => Unit(u => u.GetResourceAmountExact(t), out v));
        Register("resource_capacity", (string t, out float v) => Unit(u => u.GetResourceSlotFromName(t)?.maxAmount ?? 0f, out v));
        Register("production_rate", (string t, out float v) => Production(p => p.GetNetProductionRate(t), out v));
        Register("building", (string t, out float v) => Unit(u => u.GetProductionUnitCount(t), out v));
        Register("technology", (string t, out float v) => Unit(u => u.IsTechnologyUnlocked(t) ? 1f : 0f, out v));
        Register("clicked", (string t, out float v) => Unit(u => u.GetClickedTotal(t), out v));
        Register("score", (string t, out float v) => Events(e => e.GetEventScore(t), out v));
        Register("event_completed", (string t, out float v) => Events(e => e.GetCompletionCount(t), out v));
        Register("no_event_in_sevenths", (string t, out float v) => Events(e => e.GetSeventhsSinceLastEvent(), out v));
        Register("seventh", (string t, out float v) => Time(time => time.CurrentSeventh, out v));
        Register("phase", (string t, out float v) => Time(time => time.CurrentPhase, out v));
        Register("echo", (string t, out float v) => Time(time => time.CurrentEcho, out v));
        Register("cycle", (string t, out float v) => Time(time => time.CurrentCycle, out v));
        Register("ritual_seventh", (string t, out float v) => Time(time => time.IsRitualSeventh ? 1f : 0f, out v));
        Register("population", (string t, out float v) => Pop(p => p.population, out v));
        Register("housing", (string t, out float v) => Pop(p => p.housing, out v));
        Register("free_housing", (string t, out float v) => Pop(p => p.housing - p.population, out v));
        Register("vagrants", (string t, out float v) => Pop(p => p.vagrants, out v));
        Register("deaths", (string t, out float v) => Pop(p => p.deaths, out v));
        Register("vagrant_deaths", (string t, out float v) => Pop(p => p.vagrantDeaths, out v));
        Register("true_deaths", (string t, out float v) => Pop(p => p.trueDeaths, out v));
        Register("morale", (string t, out float v) => Stat("morale", out v));
        Register("satisfaction", (string t, out float v) => Stat("satisfactionlevel", out v));
        Register("civic", (string t, out float v) => Civics(c => c.IsCivicActive(t) ? 1f : 0f, out v));
        Register("government", (string t, out float v) => Government(g => string.Equals(g.GetCurrentGovernmentTypeString(), t, StringComparison.OrdinalIgnoreCase) ? 1f : 0f, out v));
        Register("weather", (string t, out float v) => Weather(w => w.IsWeatherActive(t) ? 1f : 0f, out v));
        // The Ages: "age" is the current Age's number (with a target: 1 while that Age id is current), "act" the Act
        // (1-based), "age_progress" the % of the Age passed, "crisis_stage" the crisis stage reached (0 before it).
        Register("age", (string t, out float v) => Ages(a => string.IsNullOrEmpty(t) ? a.Current.number : string.Equals(a.Current.id, t, StringComparison.OrdinalIgnoreCase) ? 1f : 0f, out v));
        Register("act", (string t, out float v) => Ages(a => a.Act + 1, out v));
        Register("age_progress", (string t, out float v) => Ages(a => Mathf.Round(a.Progress * 100f), out v));
        Register("crisis_stage", (string t, out float v) => Ages(a => a.StageReached + 1, out v));
        Register("ages_survived", (string t, out float v) => Ages(a => a.History.Count, out v));
        // "age_reached": 1 once the world is in that Age id or has passed through it (Age requirements, X05).
        Register("age_reached", (string t, out float v) => { v = AgeReached(t) ? 1f : 0f; return !string.IsNullOrEmpty(t); });
        // "capability": 1 while the current Age allows it (AgeCapabilities; e.g. "ornamental-magic").
        Register("capability", (string t, out float v) => { v = AgeCapabilities.IsAvailable(t) ? 1f : 0f; return AgeCapabilities.IsKnown(t); });
        // The world: explored hexes (all, or those whose feature carries the tag).
        Register("map_explored", (string t, out float v) => World(w => w.ExploredWithTag(t), out v));
        // Exact discovered feature IDs: seeing a silhouette is not enough to tell its story.
        Register("map_feature", (string t, out float v) => World(w => w.Map == null ? 0 :
            w.Map.Tiles.Count(tile => tile.explored && string.Equals(tile.feature, t, StringComparison.OrdinalIgnoreCase)), out v));
        Register("expedition_party", (string t, out float v) => World(w => w.ExpeditionUnits.Count(u => u.companions != null && u.companions.Count > 0), out v));
        Register("expedition_worn", (string t, out float v) => World(w => w.ExpeditionUnits.Count(u => u.fatigue >= 40f), out v));
        Register("expedition_mishaps", (string t, out float v) => World(w => w.ExpeditionUnits.Sum(u => u.mishaps), out v));
        Register("expedition_dispute", (string t, out float v) => World(w => w.ExpeditionUnits.Count(u => u.mishaps > 0 && u.companions != null && u.companions.Count > 0), out v));
        // Units on the map: "units" deployed now (optionally of one kind), "journeys" completed by any unit.
        Register("units", (string t, out float v) => World(w => w.UnitCount(t), out v));
        Register("journeys", (string t, out float v) => World(w => w.JourneysCompleted, out v));
        Register("settlements", (string t, out float v) => World(w => w.SettlementCount(t), out v));
        // Territory and administration (WorldTerritory): cells held (all, or "adopted"/"claimed"), Administrative
        // Capacity, strain and efficiency in percent, and 1 while that way of growing is favoured ("horizontal"...).
        Register("territory", (string t, out float v) => World(w => string.Equals(t, "adopted", StringComparison.OrdinalIgnoreCase) ? w.Realm.adopted
            : string.Equals(t, "claimed", StringComparison.OrdinalIgnoreCase) ? w.Realm.claimed : w.Realm.cells, out v));
        Register("admin_capacity", (string t, out float v) => World(w => w.Realm.capacity, out v));
        Register("admin_strain", (string t, out float v) => World(w => w.Realm.strain * 100f, out v));
        Register("admin_efficiency", (string t, out float v) => World(w => w.Realm.efficiency * 100f, out v));
        Register("expansion", (string t, out float v) => World(w => string.Equals(w.Realm.favoured.ToString(), t, StringComparison.OrdinalIgnoreCase) ? 1f : 0f, out v));
        // Era Score: this Age's total (with a target "act": the current Act's).
        Register("era_score", (string t, out float v) => Ages(a => string.Equals(t, "act", StringComparison.OrdinalIgnoreCase) ? a.EraScoreThisAct : a.EraScore, out v));
        // Counts across the capital: technologies researched, every building, every stored resource, any store full.
        Register("technologies", (string t, out float v) => Unit(u => u.CountUnlockedTechnologies(), out v));
        // Enlightened technologies (their goals met, or a story): how many, or with a target 1 once that one is.
        Register("enlightened", (string t, out float v) => Unit(u => string.IsNullOrEmpty(t) ? u.CountEnlightenedTechnologies() : u.GetTechnologySlot(t) is GameTechnologySlot s && s.enlightenedCompleted ? 1f : 0f, out v));
        Register("buildings_total", (string t, out float v) => Unit(u => u.CountAllBuildings(), out v));
        Register("total_resources", (string t, out float v) => Unit(u => u.TotalStoredResources(), out v));
        Register("resource_full", (string t, out float v) => Unit(u => u.AnyResourceFull() ? 1f : 0f, out v));
        Register("stories_completed", (string t, out float v) => Events(e => e.TotalCompletions(), out v));
        // The food stores: their food value, and the kinds held in quantity.
        Register("stored_food", (string t, out float v) => { v = Pantry.StoredValue; return Pantry.Instance != null; });
        Register("stored_food_kinds", (string t, out float v) => { v = Pantry.Instance != null ? Pantry.Instance.Variety : 0f; return Pantry.Instance != null; });
        // Placeholder: Keynote Relics (Celestial Astrology) have no system yet, so none is ever held.
        Register("keynote_relics", (string t, out float v) => { v = 0f; return true; });
        // Legends: "legend" 1 once met, "legend_rank" 1-5, "fragments" (a legend's Lyrical Fragments, or one kind:
        // "fragments:Name Vision"); "renown" is the older name for the total.
        Register("legend", (string t, out float v) => Legends(l => l.IsRecruited(t) ? 1f : 0f, out v));
        Register("legend_rank", (string t, out float v) => Legends(l => l.IsRecruited(t) ? l.Rank(t) : 0f, out v));
        Register("fragments", (string t, out float v) => Legends(l => BalladActors.SplitTarget(t, out string who, out var kind) && !l.IsRecruited(t) ? l.Fragments(who, kind) : l.Fragments(t), out v));
        Register("renown", (string t, out float v) => Legends(l => l.Fragments(t), out v));
        // Ballads: 1 once a ballad's finale is sung; "ballad_verses" the verses told so far.
        Register("ballad", (string t, out float v) => Events(e => e.IsBalladComplete(t) ? 1f : 0f, out v));
        Register("ballad_verses", (string t, out float v) => Events(e => e.BalladVersesTold(t), out v));
        Register("legends_met", (string t, out float v) => Legends(l => l.RecruitedCount, out v));
    }

    /// <summary>Add or replace the resolver for a domain.</summary>
    public static void Register(string domain, Resolver resolver)
    {
        if (string.IsNullOrEmpty(domain) || resolver == null) return;
        Resolvers[domain] = resolver;
    }

    public static bool IsKnownDomain(string domain) => domain != null && Resolvers.ContainsKey(domain);

    public static IEnumerable<string> Domains => Resolvers.Keys;

    /// <summary>
    /// Current value of (domain, target). False when the domain is unknown or the owning system is not
    /// in the scene yet; <paramref name="value"/> is then 0.
    /// </summary>
    public static bool TryGet(string domain, string target, out float value)
    {
        value = 0f;
        if (domain == null || !Resolvers.TryGetValue(domain, out var resolver)) return false;
        return resolver(TargetOf(domain, target), out value);
    }

    /// <summary>
    /// The target a resolver sees. Story conditions without a target ("age: &lt;= 1") are parsed with the domain as
    /// their target, which means "no target": without this, "age" would look for an Age whose id is "age".
    /// </summary>
    public static string TargetOf(string domain, string target) =>
        target == null || string.Equals(target.Trim(), domain, StringComparison.OrdinalIgnoreCase) ? string.Empty : target;

    public static float Get(string domain, string target) => TryGet(domain, target, out float value) ? value : 0f;

    public static bool Evaluate(string domain, string target, ComparisonOperator op, float required)
    {
        return TryGet(domain, target, out float actual) && Compare(actual, op, required);
    }

    public static bool Compare(float actual, ComparisonOperator op, float expected)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return Mathf.Approximately(actual, expected);
            case ComparisonOperator.NotEquals: return !Mathf.Approximately(actual, expected);
            case ComparisonOperator.GreaterThan: return actual > expected;
            case ComparisonOperator.LessThan: return actual < expected;
            case ComparisonOperator.GreaterThanOrEqual: return actual >= expected;
            case ComparisonOperator.LessThanOrEqual: return actual <= expected;
            default: return false;
        }
    }

    /// <summary>Civic requirements use their own serialized enum; this maps it onto the shared comparison.</summary>
    public static ComparisonOperator ToOperator(ComparisonType comparison)
    {
        switch (comparison)
        {
            case ComparisonType.GreaterThan: return ComparisonOperator.GreaterThan;
            case ComparisonType.GreaterEqual: return ComparisonOperator.GreaterThanOrEqual;
            case ComparisonType.Equal: return ComparisonOperator.Equals;
            case ComparisonType.LessEqual: return ComparisonOperator.LessThanOrEqual;
            case ComparisonType.LessThan: return ComparisonOperator.LessThan;
            default: return ComparisonOperator.NotEquals;
        }
    }

    public static string Symbol(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return "==";
            case ComparisonOperator.NotEquals: return "!=";
            case ComparisonOperator.GreaterThan: return ">";
            case ComparisonOperator.LessThan: return "<";
            case ComparisonOperator.GreaterThanOrEqual: return ">=";
            default: return "<=";
        }
    }

    // Each helper reports "not available" instead of a misleading 0 when its system is missing.

    private static bool Stat(string name, out float value)
    {
        var stats = StatManager.Instance;
        value = stats != null ? stats.GetStatValue(name) : 0f;
        return stats != null && StatDefinitions.IsKnown(name);
    }

    private static bool Unit(Func<GameUnitsLogic, float> read, out float value)
    {
        var units = GameUnitsLogic.Instance;
        value = units != null ? read(units) : 0f;
        return units != null;
    }

    private static bool Production(Func<GlobalProductionManager, float> read, out float value)
    {
        var production = GlobalProductionManager.Instance;
        value = production != null ? read(production) : 0f;
        return production != null;
    }

    private static bool Events(Func<EventSystemLogic, float> read, out float value)
    {
        var events = EventSystemLogic.Instance;
        value = events != null ? read(events) : 0f;
        return events != null;
    }

    private static bool Time(Func<TimeSystemLogic, float> read, out float value)
    {
        var time = TimeSystemLogic.Instance;
        value = time != null ? read(time) : 0f;
        return time != null;
    }

    private static bool Pop(Func<PopGrowthLogic, float> read, out float value)
    {
        var pop = PopGrowthLogic.Instance;
        value = pop != null ? read(pop) : 0f;
        return pop != null;
    }

    private static bool Civics(Func<CivicManager, float> read, out float value)
    {
        var civics = CivicManager.Instance;
        value = civics != null ? read(civics) : 0f;
        return civics != null;
    }

    private static bool Government(Func<GovernmentLogic, float> read, out float value)
    {
        var government = GovernmentLogic.Instance;
        value = government != null ? read(government) : 0f;
        return government != null;
    }

    private static bool AgeReached(string ageId)
    {
        if (string.IsNullOrEmpty(ageId)) return false;
        if (string.Equals(GameAge.Id, ageId, StringComparison.OrdinalIgnoreCase)) return true;
        var history = AgeProgression.Instance != null ? AgeProgression.Instance.History : null;
        if (history == null) return false;
        foreach (var record in history)
            if (string.Equals(record.ageId, ageId, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool Ages(Func<AgeProgression, float> read, out float value)
    {
        var ages = AgeProgression.Instance;
        bool ready = ages != null && ages.Current != null;
        value = ready ? read(ages) : 0f;
        return ready;
    }

    private static bool World(Func<WorldSystem, float> read, out float value)
    {
        var world = WorldSystem.Instance;
        bool ready = world != null && world.Map != null;
        value = ready ? read(world) : 0f;
        return ready;
    }

    private static bool Legends(Func<LegendProgress, float> read, out float value)
    {
        var legends = LegendProgress.Instance;
        value = legends != null ? read(legends) : 0f;
        return legends != null;
    }

    private static bool Weather(Func<CelestialWeatherSystemLogic, float> read, out float value)
    {
        var weather = CelestialWeatherSystemLogic.Instance;
        value = weather != null ? read(weather) : 0f;
        return weather != null;
    }
}
