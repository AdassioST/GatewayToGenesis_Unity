using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[Serializable]
public sealed class LegendAffectionMeter
{
    public int value = 50;
}

/// <summary>A directional reading. The reverse direction is a separate saved record.</summary>
[Serializable]
public sealed class LegendRelationship
{
    public string other;
    public int stage, growth, fractures, encounters;
    // Nested optional data preserves a nonzero default with the existing save codec.
    [SaveOptionalField] public LegendAffectionMeter affectionProgress = new LegendAffectionMeter();
    public int affection
    {
        get => Math.Max(0, Math.Min(100, affectionProgress?.value ?? 50));
        set { if (affectionProgress == null) affectionProgress = new LegendAffectionMeter(); affectionProgress.value = Math.Max(0, Math.Min(100, value)); }
    }
    public string thread;
    public bool severed, mirrored, reachedConsonance, reachedDissonance, sharedWound;
    public List<string> memories = new List<string>();
    public List<string> moments = new List<string>();
    public bool Significant => !severed && !string.IsNullOrEmpty(thread);
    public string StageName => LegendRelationshipRules.StageName(stage);
    public int Readiness => Significant ? LegendRelationshipRules.Readiness(stage, affection) : 0;
}

/// <summary>Canon: seven stages, directional readings, 1/2/3 tests, seven significant ties.
/// Encounters move a capped 0-100 meter but never count as a Relation Growth.
/// A wound establishes a thread; tests change stages only after the meter has entered its threshold.</summary>
public static class LegendRelationshipRules
{
    public const int Capacity = 7;
    public const int Midpoint = 50, UpgradeAt = 85, DowngradeBelow = 15;
    public static int Readiness(int stage, int affection) => affection >= UpgradeAt && stage < 3 ? 1 : affection < DowngradeBelow && stage > -3 ? -1 : 0;
    public static readonly string[] Threads = { "Resonance", "Luminance", "Flux", "Void", "Cindergale", "Crystal", "Strand" };
    public static bool SplitTarget(string target, out string from, out string to, out string thread)
    {
        from = to = thread = null;
        if (string.IsNullOrWhiteSpace(target)) return false;
        int arrow = target.IndexOf('>'), separator = target.LastIndexOf('|');
        if (arrow <= 0 || separator <= arrow + 1) return false;
        from = target.Substring(0, arrow).Trim();
        to = target.Substring(arrow + 1, separator - arrow - 1).Trim();
        string binding = target.Substring(separator + 1).Trim();
        thread = Threads.FirstOrDefault(t => string.Equals(t, binding, StringComparison.OrdinalIgnoreCase));
        return from.Length > 0 && to.Length > 0 && thread != null;
    }
    private static readonly string[] Stages = { "Vowed Nemesis", "Forged Rivalry", "Frayed Tension", "Open Rest", "Dim Tuning", "Sworn Duet", "Kindred Soulmate" };
    public static string StageName(int stage) => Stages[Math.Max(-3, Math.Min(3, stage)) + 3];
    public static int TestsNeeded(int stage, int direction) => Math.Max(1, Math.Abs(stage + (Math.Sign(stage) == Math.Sign(direction) || stage == 0 ? Math.Sign(direction) : 0)));

    public static bool Experience(LegendRelationship bond, string key, string memory, int direction, string thread, bool wound, bool hasRoom, int? affectionChange = null)
    {
        if (bond == null || bond.severed || string.IsNullOrWhiteSpace(key) || bond.moments.Contains(key)) return false;
        bond.moments.Add(key);
        bond.encounters++;
        bond.memories.Insert(0, memory);
        if (bond.memories.Count > 12) bond.memories.RemoveAt(12);
        bond.sharedWound |= wound;
        direction = Math.Sign(direction);
        if (!bond.Significant)
        {
            if (direction == 0 || !wound || !hasRoom || string.IsNullOrEmpty(thread)) return true;
            bond.thread = thread;
        }
        int readyBefore = bond.Readiness;
        int amount = affectionChange ?? (direction == 0 ? 3 : direction * 12);
        bond.affection = (int)Math.Max(0L, Math.Min(100L, (long)bond.affection + amount));
        // Entering a threshold arms a later meaningful experience; it never also changes the stage.
        // Neutral encounters can move the meter but cannot satisfy a Relation Growth/Fracture test.
        if (bond.Readiness != 1) bond.growth = 0;
        if (bond.Readiness != -1) bond.fractures = 0;
        if (direction == 0 || readyBefore != direction || bond.Readiness != direction) return true;
        if (direction > 0) { bond.growth++; bond.fractures = 0; }
        else { bond.fractures++; bond.growth = 0; }
        if ((direction > 0 ? bond.growth : bond.fractures) < TestsNeeded(bond.stage, direction)) return true;
        bond.stage = Math.Max(-3, Math.Min(3, bond.stage + direction));
        bond.affection = Midpoint;
        bond.growth = bond.fractures = 0;
        bond.reachedConsonance |= bond.stage >= 2;
        bond.reachedDissonance |= bond.stage <= -2;
        bond.mirrored = bond.reachedConsonance && bond.reachedDissonance;
        return true;
    }
}

public partial class LegendProgress
{
    public event Action<string, string, int> RelationshipThresholdReached;
    public event Action<string, string, int> RelationshipStageChanged;
    public IReadOnlyList<LegendRelationship> Relationships(string legend) =>
        legend != null && _recruited.TryGetValue(legend, out var record) ? record.relationships : Array.Empty<LegendRelationship>();

    public bool Relate(string from, string to, string key, string memory, int direction = 0, string thread = null, bool wound = false, int? affectionChange = null)
    {
        if (!IsRecruited(from) || !IsRecruited(to) || string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return false;
        var ties = _recruited[from].relationships;
        var bond = ties.FirstOrDefault(b => string.Equals(b.other, to, StringComparison.OrdinalIgnoreCase));
        if (bond == null) { bond = new LegendRelationship { other = to }; ties.Add(bond); }
        int previous = bond.stage;
        int readyBefore = bond.Readiness;
        bool changed = LegendRelationshipRules.Experience(bond, key, memory, direction, thread, wound, ties.Count(b => b.Significant) < LegendRelationshipRules.Capacity, affectionChange);
        if (bond.stage != previous)
        {
            int directionChanged = Math.Sign(bond.stage - previous);
            NotificationFeed.Push($"{from} toward {to}: relationship {(directionChanged > 0 ? "upgraded" : "downgraded")}",
                $"{LegendRelationshipRules.StageName(previous)} → {bond.StageName}\n{memory}\nAffection settles at 50/100 in the new stage. {bond.thread} Thread.", NotificationFeed.Topic.Council, key: $"bond-stage:{from}:{to}");
            RelationshipStageChanged?.Invoke(from, to, directionChanged);
        }
        else if (changed && bond.Readiness != 0 && bond.Readiness != readyBefore)
        {
            NotificationFeed.Push($"{from} toward {to}: {(bond.Readiness > 0 ? "upgrade" : "downgrade")} threshold reached",
                $"{bond.StageName} · {bond.affection}/100\n{memory}\nA further shared experience must {(bond.Readiness > 0 ? "strengthen" : "fray")} this bond to change its stage.", NotificationFeed.Topic.Council, key: $"bond-threshold:{from}:{to}");
            RelationshipThresholdReached?.Invoke(from, to, bond.Readiness);
        }
        if (changed) Changed?.Invoke();
        return changed;
    }

    public void ShareExperience(IEnumerable<string> members, string key, string memory, int direction = 0, string thread = "Strand", bool wound = false, int? affectionChange = null)
    {
        var cast = members.Where(IsRecruited).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var from in cast)
            foreach (var to in cast.Where(n => !string.Equals(n, from, StringComparison.OrdinalIgnoreCase)))
                Relate(from, to, key, memory, direction, thread, wound, affectionChange);
    }

    /// <summary>Explicit, directional severance. Prototype cost: five strain; history and reverse reading survive.</summary>
    public bool SeverRelationship(string from, string to)
    {
        var bond = Relationships(from).FirstOrDefault(b => string.Equals(b.other, to, StringComparison.OrdinalIgnoreCase));
        if (bond == null || !bond.Significant || !IsRecruited(from)) return false;
        bond.severed = true;
        bond.memories.Insert(0, "Chose Relational Severance; the old thread remains in memory.");
        Strain(from, 5f, $"Severed the bond with {to}");
        Changed?.Invoke();
        return true;
    }

    public string RelationshipText(string legend)
    {
        var ties = Relationships(legend);
        var text = new StringBuilder();
        text.AppendLine($"Significant ties: {ties.Count(b => b.Significant)} / {LegendRelationshipRules.Capacity}");
        foreach (var bond in ties.OrderByDescending(b => b.Significant).ThenBy(b => b.other))
        {
            var reverse = Relationships(bond.other).FirstOrDefault(b => string.Equals(b.other, legend, StringComparison.OrdinalIgnoreCase));
            text.AppendLine($"{bond.other}: {(bond.severed ? "Severed" : bond.StageName)}{(bond.thread == null ? "" : " · " + bond.thread + " Thread")}");
            text.AppendLine($"  Their reading: {(reverse == null ? "Open Rest" : reverse.severed ? "Severed" : reverse.StageName + $" · {reverse.affection}/100")} · {bond.encounters} shared moments");
            if (bond.mirrored) text.AppendLine("  Deep Mirrored Bond");
            if (bond.Significant) text.AppendLine($"  Affection: {bond.affection}/100 (50: halfway) · " +
                (bond.Readiness > 0 ? "Upgrade ready: awaiting shared experience" : bond.Readiness < 0 ? "Downgrade ready: awaiting a fraying experience" : "Upgrade at 85+; downgrade below 15"));
            if (bond.growth > 0) text.AppendLine($"  Relation Growth: {bond.growth}/{LegendRelationshipRules.TestsNeeded(bond.stage, 1)}");
            if (bond.fractures > 0) text.AppendLine($"  Relation Fracture: {bond.fractures}/{LegendRelationshipRules.TestsNeeded(bond.stage, -1)}");
            if (bond.memories.Count > 0) text.AppendLine("  Last: " + bond.memories[0]);
        }
        if (ties.Count == 0) text.AppendLine("No shared moments yet. Bring legends together in stories and expeditions.");
        return text.ToString().TrimEnd();
    }
}

/// <summary>Story qualities address a directional pair as Name | Name. Unknown legends fail closed.</summary>
public static class LegendRelationshipValues
{
    private static bool Read(string target, out LegendRelationship bond)
    {
        bond = null;
        var legends = LegendProgress.Instance;
        var pair = (target ?? "").Split('|');
        if (legends == null || pair.Length != 2) return false;
        string from = pair[0].Trim(), to = pair[1].Trim();
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) || !legends.IsRecruited(from) || !legends.IsRecruited(to)) return false;
        bond = legends.Relationships(from).FirstOrDefault(b => string.Equals(b.other, to, StringComparison.OrdinalIgnoreCase));
        return true;
    }
    public static bool Affection(string target, out float value)
    {
        bool known = Read(target, out var bond);
        value = bond != null && !bond.severed ? bond.stage : 0;
        return known;
    }
    public static bool Significant(string target, out float value)
    {
        bool known = Read(target, out var bond);
        value = bond != null && bond.Significant ? 1 : 0;
        return known;
    }
    public static bool Progress(string target, out float value)
    {
        bool known = Read(target, out var bond);
        value = bond != null && !bond.severed ? bond.affection : LegendRelationshipRules.Midpoint;
        return known;
    }
}
