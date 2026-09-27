using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What happens at one moment of an Age (<see cref="AgeRules.Schedule"/>). Ordered by kind at equal times.</summary>
public enum AgeBeatKind
{
    /// <summary>The boundary between two Acts: the turning point is an Act of Fate (a milder crisis, a paradigm shift).</summary>
    ActOfFate = 0,
    /// <summary>The Age Crisis reaches its next stage.</summary>
    CrisisStage = 1,
    /// <summary>The Age's time is spent: the crisis resolves and the world passes to the next Age.</summary>
    AgeEnds = 2,
}

public readonly struct AgeBeat
{
    public readonly AgeBeatKind kind;
    /// <summary>Sevenths into the Age at which the beat happens.</summary>
    public readonly int at;
    /// <summary>ActOfFate: the Act that begins (1 = the second Act). CrisisStage: the stage (0-based). AgeEnds: 0.</summary>
    public readonly int index;

    public AgeBeat(AgeBeatKind kind, int at, int index)
    {
        this.kind = kind;
        this.at = at;
        this.index = index;
    }

    public override string ToString() => $"{kind} {index} at {at}";
}

/// <summary>
/// The shape of an Age in time, with no scene state (tested in <c>AgeRulesTests</c>). An Age is its Acts of Fate
/// (the vault: "one third of the Ages duration", four in the long Ages), each lasting an authored number of
/// sevenths; its Age Crisis begins partway through (halfway in the first prototype) and climbs through authored
/// stages until the Age's time is spent. <see cref="AgeProgression"/> keeps the clock and asks these rules what is due.
///
/// Every beat is scheduled once, up front, and consumed in order, so a beat can neither repeat nor be skipped:
/// the clock only moves forward one seventh at a time and pausing simply stops it.
/// </summary>
public static class AgeRules
{
    /// <summary>Sevenths in an Age: the sum of its Acts (an Act is at least one seventh).</summary>
    public static int Length(IReadOnlyList<int> actSevenths)
    {
        if (actSevenths == null) return 0;
        int total = 0;
        foreach (int act in actSevenths) total += Math.Max(1, act);
        return total;
    }

    /// <summary>Sevenths into the Age at which Act <paramref name="act"/> (0-based) begins.</summary>
    public static int ActStart(int act, IReadOnlyList<int> actSevenths)
    {
        int start = 0;
        for (int i = 0; i < act && i < actSevenths.Count; i++) start += Math.Max(1, actSevenths[i]);
        return start;
    }

    /// <summary>The Act (0-based) running <paramref name="sevenths"/> into the Age; the last Act once the Age is spent.</summary>
    public static int ActAt(int sevenths, IReadOnlyList<int> actSevenths)
    {
        if (actSevenths == null || actSevenths.Count == 0) return 0;
        int end = 0;
        for (int i = 0; i < actSevenths.Count; i++)
        {
            end += Math.Max(1, actSevenths[i]);
            if (sevenths < end) return i;
        }
        return actSevenths.Count - 1;
    }

    /// <summary>How far through the Age the clock is, 0 to 1.</summary>
    public static float Progress(int sevenths, IReadOnlyList<int> actSevenths)
    {
        int length = Length(actSevenths);
        return length <= 0 ? 0f : Math.Min(1f, Math.Max(0f, sevenths / (float)length));
    }

    /// <summary>
    /// The seventh a crisis stage starting at <paramref name="fraction"/> of the Age falls on. A stage never falls on
    /// the Age's first seventh or after its end.
    /// </summary>
    public static int StageStart(float fraction, int length)
    {
        if (length <= 0) return 0;
        int at = (int)Math.Round(Math.Max(0f, fraction) * length, MidpointRounding.AwayFromZero);
        return Math.Min(length, Math.Max(1, at));
    }

    /// <summary>
    /// Every beat of an Age in the order it happens: an Act of Fate at each boundary between Acts, each crisis stage
    /// at its fraction of the Age (stages keep their authored order even if their fractions do not), and the end.
    /// Beats at the same seventh come Act of Fate first, then crisis stages, then the end.
    /// </summary>
    public static List<AgeBeat> Schedule(IReadOnlyList<int> actSevenths, IReadOnlyList<float> crisisStageStarts)
    {
        var beats = new List<AgeBeat>();
        int length = Length(actSevenths);
        if (length <= 0) return beats;

        for (int act = 1; act < actSevenths.Count; act++) beats.Add(new AgeBeat(AgeBeatKind.ActOfFate, ActStart(act, actSevenths), act));

        int previous = 1;
        if (crisisStageStarts != null)
        {
            for (int stage = 0; stage < crisisStageStarts.Count; stage++)
            {
                int at = Math.Max(previous, StageStart(crisisStageStarts[stage], length));
                beats.Add(new AgeBeat(AgeBeatKind.CrisisStage, at, stage));
                previous = at;
            }
        }
        beats.Add(new AgeBeat(AgeBeatKind.AgeEnds, length, 0));

        // Equal (time, kind) fall back to the index, which is the authored order.
        beats.Sort((a, b) =>
        {
            int byTime = a.at.CompareTo(b.at);
            if (byTime != 0) return byTime;
            int byKind = a.kind.CompareTo(b.kind);
            return byKind != 0 ? byKind : a.index.CompareTo(b.index);
        });
        return beats;
    }

    /// <summary>
    /// The beats due once the clock reads <paramref name="sevenths"/>, starting at <paramref name="next"/>; advances
    /// <paramref name="next"/> past them, so each beat is returned exactly once.
    /// </summary>
    public static List<AgeBeat> Due(IReadOnlyList<AgeBeat> schedule, ref int next, int sevenths)
    {
        var due = new List<AgeBeat>();
        if (schedule == null) return due;
        while (next < schedule.Count && schedule[next].at <= sevenths)
        {
            due.Add(schedule[next]);
            next++;
        }
        return due;
    }

    /// <summary>
    /// The technology tree drives the Age: moves the clock and returns the beats due, where a beat with a gate (a
    /// technology name in <paramref name="gates"/>, parallel to the schedule) fires only once that technology is
    /// researched. The clock never passes a closed gate (it waits at the gate's seventh), and a gate researched early
    /// opens at once: the clock jumps to it. Beats without a gate keep their authored spacing in time.
    /// <paramref name="steps"/> is the sevenths of time passing (0 to only check the gates).
    /// </summary>
    public static List<AgeBeat> Advance(IReadOnlyList<AgeBeat> schedule, IReadOnlyList<string> gates, ref int next, ref int sevenths, int steps, Func<string, bool> researched)
    {
        var due = new List<AgeBeat>();
        if (schedule == null) return due;
        string Gate(int i) => gates != null && i < gates.Count && !string.IsNullOrEmpty(gates[i]) ? gates[i] : null;
        bool Closed(int i) => Gate(i) != null && (researched == null || !researched(Gate(i)));
        while (true)
        {
            while (next < schedule.Count && schedule[next].at <= sevenths && !Closed(next)) due.Add(schedule[next++]);
            if (next >= schedule.Count) break;
            // A gate researched before its time opens now.
            if (Gate(next) != null && !Closed(next) && schedule[next].at > sevenths)
            {
                sevenths = schedule[next].at;
                continue;
            }
            // Time passes, but never past a closed gate.
            if (steps <= 0 || (Closed(next) && sevenths >= schedule[next].at)) break;
            sevenths++;
            steps--;
        }
        return due;
    }

    /// <summary>The technology the Age is waiting for now (a closed gate the clock has reached), or null.</summary>
    public static string WaitingGate(IReadOnlyList<AgeBeat> schedule, IReadOnlyList<string> gates, int next, int sevenths, Func<string, bool> researched)
    {
        if (schedule == null || gates == null || next >= schedule.Count || next >= gates.Count) return null;
        string gate = gates[next];
        if (string.IsNullOrEmpty(gate) || sevenths < schedule[next].at) return null;
        return researched != null && researched(gate) ? null : gate;
    }

    // ===== THE AGE MEASURED BY ITS TECHNOLOGY TREE =====

    /// <summary>A technology and everything it needs, each once, prerequisites first (names as written).</summary>
    public static List<string> PathTo(string technology, Func<string, IEnumerable<string>> prerequisites)
    {
        var path = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name.Trim())) return;
            var before = prerequisites != null ? prerequisites(name.Trim()) : null;
            if (before != null) foreach (var p in before) Visit(p);
            path.Add(name.Trim());
        }
        Visit(technology);
        return path;
    }

    /// <summary>
    /// The technologies that close Act <paramref name="act"/> (0-based): the technology opening the next Act
    /// (<paramref name="actGates"/>[act]) and every prerequisite not already on an earlier Act's path. Null when time,
    /// not a technology, ends the Act (no gate, or the last Act).
    /// </summary>
    public static List<string> ActPath(int act, IReadOnlyList<string> actGates, Func<string, IEnumerable<string>> prerequisites)
    {
        if (actGates == null || act < 0 || act >= actGates.Count || string.IsNullOrWhiteSpace(actGates[act])) return null;
        var earlier = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < act; i++)
            if (!string.IsNullOrWhiteSpace(actGates[i])) earlier.UnionWith(PathTo(actGates[i], prerequisites));
        return PathTo(actGates[act], prerequisites).Where(t => !earlier.Contains(t)).ToList();
    }

    /// <summary>
    /// How far through the Age, 0-1, as the banner's bar shows it: each Act is an equal share of the bar and fills
    /// with <paramref name="actShare"/> (0-1) of itself.
    /// </summary>
    public static float BarProgress(int act, int acts, float actShare)
    {
        if (acts <= 0) return 0f;
        act = Math.Max(0, Math.Min(acts - 1, act));
        return Math.Min(1f, (act + Math.Max(0f, Math.Min(1f, actShare))) / acts);
    }

    /// <summary>Roman numerals for Acts and Ages ("Act II", "Ages IV"); 0 is "0", as the vault writes Age 0.</summary>
    public static string Roman(int number)
    {
        if (number <= 0) return "0";
        var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            while (number >= values[i])
            {
                text.Append(symbols[i]);
                number -= values[i];
            }
        }
        return text.ToString();
    }
}
