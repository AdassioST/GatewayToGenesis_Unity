using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>How a technology shows in its tree.</summary>
public enum TechVisibility
{
    /// <summary>Not uncovered yet: nothing of it shows, not even its lines.</summary>
    Hidden,
    /// <summary>Uncovered but locked: a prerequisite can be researched now, it was enlightened, or the Age asks for it.</summary>
    Preview,
    /// <summary>Every prerequisite is researched: it can be researched now.</summary>
    Available,
    Researched
}

/// <summary>How a connector reads, from dim to bright. Brighter tones are drawn over dimmer ones where lines overlap.</summary>
public enum LineTone { Pending, Complete, Open, Ready, Planned, Focus }

/// <summary>Where a routed connector turns, in grid terms (<see cref="TechTreeRules.Route"/>).</summary>
public enum GridX
{
    /// <summary>The right edge of the slot in the column.</summary>
    SlotRight,
    /// <summary>The left edge of the slot in the column.</summary>
    SlotLeft,
    /// <summary>The middle of the gap after the column (column -1: before the first column).</summary>
    GapAfter
}

public enum GridY
{
    /// <summary>The line height of the row's slots.</summary>
    Row,
    /// <summary>The middle of the channel under the row (row -1: above the first row).</summary>
    ChannelBelow
}

/// <summary>One turn of a routed connector: an x and a y reference into the grid.</summary>
public readonly struct GridPoint : IEquatable<GridPoint>
{
    public readonly GridX x;
    public readonly int column;
    public readonly GridY y;
    public readonly int row;

    public GridPoint(GridX x, int column, GridY y, int row)
    {
        this.x = x;
        this.column = column;
        this.y = y;
        this.row = row;
    }

    public bool Equals(GridPoint other) => x == other.x && column == other.column && y == other.y && row == other.row;
    public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
    public override int GetHashCode() => ((int)x * 397 ^ column) * 397 ^ ((int)y * 31 + row);
    public override string ToString() => $"{x}({column})@{y}({row})";
}

/// <summary>
/// The technology tree's rules, free of Unity so they are tested directly: what is uncovered
/// (<see cref="Visibility"/>), how the lines between technologies read (<see cref="BranchTone"/>,
/// <see cref="TrunkTone"/>), how a line is routed between grid cells without crossing a slot (<see cref="Route"/>),
/// the order a research plan takes (<see cref="PlanTo"/>, <see cref="Merge"/>, <see cref="Without"/>,
/// <see cref="NextInPlan"/>) and how an Enlightenment goal's progress is written (<see cref="ProgressText"/>).
/// Technologies are named as their assets are; prerequisites come from a lookup so the tree, the tests and other trees
/// agree on one graph. The lookup answers null for a technology that exists nowhere (an empty list is a technology with
/// no prerequisite), and such a technology is never researchable.
/// </summary>
public static class TechTreeRules
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    // ===== WHAT IS UNCOVERED =====

    /// <summary>
    /// Researched; Available when every prerequisite is researched; Preview when a prerequisite is available now (the
    /// tree shows one step past what can be researched), when it was enlightened, or when <paramref name="revealed"/>
    /// says the game asks for it (the Age waiting on it); Hidden otherwise. Prerequisites that exist nowhere are never
    /// researched, so a technology naming one stays locked (<see cref="ContentValidator"/> reports it).
    /// </summary>
    public static TechVisibility Visibility(string technology, Func<string, IEnumerable<string>> prerequisites, Func<string, bool> researched,
        Func<string, bool> enlightened = null, Func<string, bool> revealed = null)
    {
        if (string.IsNullOrWhiteSpace(technology)) return TechVisibility.Hidden;
        if (researched(technology)) return TechVisibility.Researched;
        if (!Exists(technology, prerequisites)) return TechVisibility.Hidden;
        bool all = true, anyAvailable = false;
        foreach (var before in Needs(technology, prerequisites))
        {
            if (researched(before)) continue;
            all = false;
            if (!anyAvailable && IsAvailable(before, prerequisites, researched)) anyAvailable = true;
        }
        if (all) return TechVisibility.Available;
        if (anyAvailable || (enlightened != null && enlightened(technology)) || (revealed != null && revealed(technology))) return TechVisibility.Preview;
        return TechVisibility.Hidden;
    }

    /// <summary>Not researched, and every prerequisite is (a technology that exists nowhere never is).</summary>
    public static bool IsAvailable(string technology, Func<string, IEnumerable<string>> prerequisites, Func<string, bool> researched) =>
        !string.IsNullOrWhiteSpace(technology) && !researched(technology) && Exists(technology, prerequisites) && Needs(technology, prerequisites).All(researched);

    // The lookup answers null for a technology that exists nowhere.
    private static bool Exists(string technology, Func<string, IEnumerable<string>> prerequisites) => prerequisites == null || prerequisites(technology) != null;

    public static bool IsUncovered(TechVisibility visibility) => visibility != TechVisibility.Hidden;

    /// <summary>A technology's prerequisites, trimmed, without blanks.</summary>
    public static IEnumerable<string> Needs(string technology, Func<string, IEnumerable<string>> prerequisites)
    {
        var before = prerequisites != null ? prerequisites(technology) : null;
        if (before == null) yield break;
        foreach (var name in before)
            if (!string.IsNullOrWhiteSpace(name)) yield return name.Trim();
    }

    // ===== HOW THE LINES READ =====

    /// <summary>
    /// A line from a prerequisite into a technology is drawn only once both are uncovered; a technology whose
    /// prerequisite is still hidden gets a stub instead (<see cref="NeedsStub"/>), which never says where it leads from.
    /// </summary>
    public static bool ShowsLine(TechVisibility prerequisite, TechVisibility technology) => IsUncovered(prerequisite) && IsUncovered(technology);

    /// <summary>The technology shows, but this prerequisite does not: a short line from nowhere says something is missing.</summary>
    public static bool NeedsStub(TechVisibility prerequisite, TechVisibility technology) => IsUncovered(technology) && !IsUncovered(prerequisite);

    /// <summary>
    /// The branch of a line, the stretch that belongs to one prerequisite: lit once that prerequisite is researched
    /// (settled once the technology is too), planned while both are in the research plan, pending otherwise.
    /// </summary>
    public static LineTone BranchTone(TechVisibility prerequisite, TechVisibility technology, bool planned, bool focused)
    {
        if (focused) return LineTone.Focus;
        if (prerequisite == TechVisibility.Researched) return technology == TechVisibility.Researched ? LineTone.Complete : LineTone.Open;
        return planned ? LineTone.Planned : LineTone.Pending;
    }

    /// <summary>
    /// The trunk, the last stretch into a technology that all its lines share: settled once researched, planned while in
    /// the research plan, ready once every prerequisite is researched, pending otherwise.
    /// </summary>
    public static LineTone TrunkTone(TechVisibility technology, bool planned, bool focused)
    {
        if (focused) return LineTone.Focus;
        if (technology == TechVisibility.Researched) return LineTone.Complete;
        if (planned) return LineTone.Planned;
        return technology == TechVisibility.Available ? LineTone.Ready : LineTone.Pending;
    }

    // ===== ROUTING =====

    /// <summary>
    /// The turns of a line from the slot in (<paramref name="fromColumn"/>, <paramref name="fromRow"/>) to the slot in
    /// (<paramref name="toColumn"/>, <paramref name="toRow"/>), orthogonal and never through an occupied cell: out of the
    /// prerequisite's right edge, vertical runs only in the gaps between columns, horizontal runs along a row whose cells
    /// in between are free (the prerequisite's row first, then the technology's, then the nearest free row), else along
    /// the channel between two rows, and into the technology's left edge. The route always passes the gap after the
    /// prerequisite's column and ends with the stretch from the gap before the technology's column (the trunk shared by
    /// every line into it), so the last two points are the trunk.
    /// </summary>
    public static List<GridPoint> Route(int fromColumn, int fromRow, int toColumn, int toRow, Func<int, int, bool> occupied, int rows)
    {
        var points = new List<GridPoint>();
        void Add(GridX x, int column, GridY y, int row)
        {
            var point = new GridPoint(x, column, y, row);
            if (points.Count == 0 || !points[points.Count - 1].Equals(point)) points.Add(point);
        }

        int cA = fromColumn, rA = fromRow, cB = toColumn, rB = toRow;
        Add(GridX.SlotRight, cA, GridY.Row, rA);
        Add(GridX.GapAfter, cA, GridY.Row, rA);
        if (cB == cA + 1)
        {
            Add(GridX.GapAfter, cA, GridY.Row, rB);
        }
        else if (cB > cA + 1)
        {
            bool Free(int r)
            {
                if (occupied == null) return true;
                for (int c = cA + 1; c < cB; c++) if (occupied(c, r)) return false;
                return true;
            }

            if (Free(rA))
            {
                Add(GridX.GapAfter, cB - 1, GridY.Row, rA);
                Add(GridX.GapAfter, cB - 1, GridY.Row, rB);
            }
            else if (Free(rB))
            {
                Add(GridX.GapAfter, cA, GridY.Row, rB);
            }
            else
            {
                int best = -1;
                for (int r = 0; r < rows; r++)
                    if (Free(r) && (best < 0 || Detour(r, rA, rB) < Detour(best, rA, rB))) best = r;
                if (best >= 0)
                {
                    Add(GridX.GapAfter, cA, GridY.Row, best);
                    Add(GridX.GapAfter, cB - 1, GridY.Row, best);
                }
                else
                {
                    int channel = Channel(rA, rB, rows);
                    Add(GridX.GapAfter, cA, GridY.ChannelBelow, channel);
                    Add(GridX.GapAfter, cB - 1, GridY.ChannelBelow, channel);
                }
                Add(GridX.GapAfter, cB - 1, GridY.Row, rB);
            }
        }
        else
        {
            // Backwards or in the same column (an authoring slip, or a prerequisite in another Age's tree placed later):
            // out through the gap after the prerequisite, along a channel, into the gap before the technology.
            int channel = Channel(rA, rB, rows);
            Add(GridX.GapAfter, cA, GridY.ChannelBelow, channel);
            Add(GridX.GapAfter, cB - 1, GridY.ChannelBelow, channel);
        }
        Add(GridX.GapAfter, cB - 1, GridY.Row, rB);
        Add(GridX.SlotLeft, cB, GridY.Row, rB);
        return points;
    }

    private static int Detour(int row, int rA, int rB) => Math.Abs(row - rA) + Math.Abs(row - rB);

    // The channel between the two rows (under the upper one), or under a shared row (above it when it is the last row).
    private static int Channel(int rA, int rB, int rows)
    {
        if (rA != rB) return Math.Min(rA, rB);
        return rA < rows - 1 ? rA : rA - 1;
    }

    // ===== THE RESEARCH PLAN =====

    /// <summary>What a technology still needs, each once, prerequisites first and the technology last; empty once researched.</summary>
    public static List<string> PlanTo(string technology, Func<string, IEnumerable<string>> prerequisites, Func<string, bool> researched) =>
        AgeRules.PathTo(technology, t => Needs(t, prerequisites)).Where(t => !researched(t)).ToList();

    /// <summary>
    /// A new research plan from the one being followed (<paramref name="plan"/>) and the way to a new target
    /// (<paramref name="path"/>, from <see cref="PlanTo"/>). Appended, the way follows the plan (what it already holds is
    /// not repeated); otherwise the way replaces the plan, but research under way (<paramref name="active"/>) that is on
    /// the way and can go on now stays first, so its progress keeps paying off. Both orders keep every prerequisite
    /// before what needs it.
    /// </summary>
    public static List<string> Merge(IReadOnlyList<string> plan, IEnumerable<string> path, bool append, string active = null, Func<string, bool> available = null)
    {
        var result = new List<string>();
        if (append && plan != null) foreach (var name in plan) AddOnce(result, name);
        foreach (var name in path ?? Enumerable.Empty<string>()) AddOnce(result, name);
        if (!append && !string.IsNullOrEmpty(active) && available != null && available(active))
        {
            int at = result.FindIndex(n => Names.Equals(n, active));
            if (at > 0)
            {
                result.RemoveAt(at);
                result.Insert(0, active);
            }
        }
        return result;
    }

    private static void AddOnce(List<string> list, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!list.Contains(name.Trim(), Names)) list.Add(name.Trim());
    }

    /// <summary>The plan without <paramref name="technology"/> and every planned technology that needs it, directly or through another.</summary>
    public static List<string> Without(IReadOnlyList<string> plan, string technology, Func<string, IEnumerable<string>> prerequisites)
    {
        var removed = new HashSet<string>(Names) { technology };
        var result = new List<string>();
        if (plan == null) return result;
        // A plan lists prerequisites before what needs them, so one pass settles every dependant.
        foreach (var name in plan)
        {
            if (removed.Contains(name) || Needs(name, prerequisites).Any(removed.Contains)) removed.Add(name);
            else result.Add(name);
        }
        return result;
    }

    /// <summary>The plan's next research: the first planned technology not researched whose prerequisites all are, or null.</summary>
    public static string NextInPlan(IReadOnlyList<string> plan, Func<string, IEnumerable<string>> prerequisites, Func<string, bool> researched)
    {
        if (plan == null) return null;
        foreach (var name in plan)
            if (IsAvailable(name, prerequisites, researched)) return name;
        return null;
    }

    /// <summary>Position in the plan, 1-based; 0 when not planned.</summary>
    public static int PlanPosition(IReadOnlyList<string> plan, string technology)
    {
        if (plan == null || string.IsNullOrEmpty(technology)) return 0;
        for (int i = 0; i < plan.Count; i++) if (Names.Equals(plan[i], technology)) return i + 1;
        return 0;
    }

    // ===== ENLIGHTENMENT =====

    /// <summary>
    /// The Era Score an Enlightenment earns: enlightening an Event or Crisis Technology is a great deed of the Age and
    /// earns <paramref name="eraScore"/> (the owner's rule: 1, once, even for a technology that is both); any other
    /// technology earns nothing.
    /// </summary>
    public static int EnlightenmentEraScore(bool eventTechnology, bool crisisTechnology, int eraScore) =>
        eventTechnology || crisisTechnology ? Math.Max(0, eraScore) : 0;

    /// <summary>
    /// The progress an Enlightenment goal shows after its wording: "(12/80)", "(3.5/55 /s)" for rates, nothing for a goal
    /// that is simply met or not (needing 1 or less). Large numbers are shortened (1.2K).
    /// </summary>
    public static string ProgressText(float current, float required, bool rate = false)
    {
        if (required <= 1f) return string.Empty;
        return $"({Short(Math.Min(Math.Max(0f, current), required))}/{Short(required)}{(rate ? " /s" : string.Empty)})";
    }

    /// <summary>1234 → "1.2K", 12.345 → "12.3", 5 → "5".</summary>
    public static string Short(float value)
    {
        float magnitude = Math.Abs(value);
        if (magnitude >= 1000000f) return (value / 1000000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
        if (magnitude >= 1000f) return (value / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K";
        return value.ToString(magnitude >= 100f ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ===== CHECKS =====

    /// <summary>Technologies that need themselves through their prerequisites (such a loop can never be researched).</summary>
    public static List<string> Cycles(IEnumerable<string> technologies, Func<string, IEnumerable<string>> prerequisites)
    {
        var looping = new List<string>();
        foreach (var start in technologies ?? Enumerable.Empty<string>())
        {
            var seen = new HashSet<string>(Names);
            var stack = new Stack<string>(Needs(start, prerequisites));
            while (stack.Count > 0)
            {
                var name = stack.Pop();
                if (Names.Equals(name, start)) { looping.Add(start); break; }
                if (!seen.Add(name)) continue;
                foreach (var before in Needs(name, prerequisites)) stack.Push(before);
            }
        }
        return looping;
    }
}
