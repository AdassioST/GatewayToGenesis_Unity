using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One condition on a legend: which, for how many more Sevenths, and what gave it.</summary>
[Serializable]
public class LegendCondition
{
    public string id;
    /// <summary>Sevenths left; 0 or less: until something lifts it.</summary>
    public int sevenths;
    public string source;

    public LegendCondition() { }
    public LegendCondition(string id, int sevenths, string source) { this.id = id; this.sevenths = sevenths; this.source = source; }
}

/// <summary>A kind of condition: its name and what it is.</summary>
public sealed class LegendConditionSpec
{
    public string id;
    public string name;
    public string description;
    /// <summary>A hardship to carry (traumas) rather than a blessing.</summary>
    public bool harmful = true;
}

/// <summary>
/// Lasting conditions on a legend, bad traits for a while (owner, Sept 28 2026: "being traumatized for x amount of
/// sevenths"). This is the open end of another system: conditions are recorded, counted down each Seventh and shown,
/// but nothing reads their effects yet. A system that wants one registers its kind (<see cref="Register"/>) and gives it
/// through <see cref="LegendProgress.AddCondition"/>; one that wants to act on them asks <see cref="Has"/>. No scene
/// state; tested in <c>LegendGreatsTests</c>. The durations callers pick are proposals.
/// </summary>
public static class LegendConditions
{
    /// <summary>Came back from a doomed battle, or broke in one: nightmares, flinching, a Soul Leitmotif that will not settle.</summary>
    public const string Traumatized = "traumatized";
    /// <summary>Came back from missing in action: hollow, slow to trust the council again.</summary>
    public const string Haunted = "haunted";

    private static readonly Dictionary<string, LegendConditionSpec> Specs = new Dictionary<string, LegendConditionSpec>(StringComparer.OrdinalIgnoreCase)
    {
        [Traumatized] = new LegendConditionSpec { id = Traumatized, name = "Traumatized", description = "Broken in battle or left the field as it fell. The memory returns unbidden; the Soul Leitmotif does not settle." },
        [Haunted] = new LegendConditionSpec { id = Haunted, name = "Haunted", description = "Came home alone from a doomed battle, leaving the rest behind. The names of those left follow them." },
    };

    /// <summary>Adds a kind of condition (another system's). Returns false when the id is taken.</summary>
    public static bool Register(LegendConditionSpec spec)
    {
        if (spec == null || string.IsNullOrEmpty(spec.id) || Specs.ContainsKey(spec.id)) return false;
        Specs[spec.id] = spec;
        return true;
    }

    public static LegendConditionSpec Spec(string id) => id != null && Specs.TryGetValue(id, out var s) ? s : null;

    public static IEnumerable<LegendConditionSpec> Kinds => Specs.Values;

    public static string Name(string id) => Spec(id)?.name ?? id;

    public static bool Has(IEnumerable<LegendCondition> conditions, string id) =>
        conditions != null && conditions.Any(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Gives a condition; one already carried runs for the longer of the two (a second trauma does not shorten the first).</summary>
    public static LegendCondition Add(List<LegendCondition> conditions, string id, int sevenths, string source)
    {
        if (conditions == null || string.IsNullOrEmpty(id)) return null;
        var held = conditions.FirstOrDefault(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));
        if (held == null)
        {
            held = new LegendCondition(id, sevenths, source);
            conditions.Add(held);
        }
        else if (held.sevenths > 0 && (sevenths <= 0 || sevenths > held.sevenths))
        {
            held.sevenths = sevenths;
            held.source = source;
        }
        return held;
    }

    public static bool Remove(List<LegendCondition> conditions, string id) =>
        conditions != null && conditions.RemoveAll(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>One Seventh passes: timed conditions count down; those that run out are removed and returned.</summary>
    public static List<LegendCondition> Tick(List<LegendCondition> conditions)
    {
        var ended = new List<LegendCondition>();
        if (conditions == null) return ended;
        foreach (var c in conditions.Where(c => c != null && c.sevenths > 0).ToList())
        {
            c.sevenths--;
            if (c.sevenths <= 0) { ended.Add(c); conditions.Remove(c); }
        }
        conditions.RemoveAll(c => c == null);
        return ended;
    }

    /// <summary>"Traumatized (5 Sevenths)".</summary>
    public static string Describe(LegendCondition c) =>
        c == null ? string.Empty : c.sevenths > 0 ? $"{Name(c.id)} ({c.sevenths} Seventh{(c.sevenths == 1 ? "" : "s")})" : Name(c.id);
}
