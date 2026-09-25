using System;
using System.Collections.Generic;
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
        return resolver(target ?? string.Empty, out value);
    }

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

    private static bool Weather(Func<CelestialWeatherSystemLogic, float> read, out float value)
    {
        var weather = CelestialWeatherSystemLogic.Instance;
        value = weather != null ? read(weather) : 0f;
        return weather != null;
    }
}
