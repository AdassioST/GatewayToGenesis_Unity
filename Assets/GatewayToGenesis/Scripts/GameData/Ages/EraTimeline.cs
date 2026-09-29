using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One award of Era Score and when it came (saved in <see cref="AgeProgression"/>'s timeline): the Age and its Act of
/// Fate, and the calendar's Cycle, Echo, Phase and Seventh (<see cref="TimeSystemLogic"/>: 21 Sevenths a Phase, 3
/// Phases an Echo, 4 Echoes a Cycle).
/// </summary>
[Serializable]
public class EraAward
{
    public int points;
    public string reason;
    public string ageId, ageTitle;
    public int ageNumber;
    /// <summary>The Act of Fate (0-based) and the Seventh of the Age it came in.</summary>
    public int act, ageSeventh;
    public int cycle, echo, phase, seventh;
    public string cycleName;
    /// <summary>Its place among every award ever made (the timeline's order when two share a Seventh).</summary>
    public int order;
}

/// <summary>
/// The Era Score timeline, with no scene state (tested in <c>EraTimelineTests</c>): every award in chronological order,
/// grouped by Age, then by Cycle and Echo, each line dated to its Phase and Seventh.
/// </summary>
public static class EraTimeline
{
    /// <summary>A Seventh's place in the calendar, counted from the first (for ordering awards).</summary>
    public static int Stamp(int cycle, int echo, int phase, int seventh) =>
        (((Math.Max(1, cycle) - 1) * TimeSystemLogic.EchoesPerCycle + Math.Max(1, echo) - 1) * TimeSystemLogic.PhasesPerEcho + Math.Max(1, phase) - 1) * TimeSystemLogic.SeventhsPerPhase + Math.Max(1, seventh) - 1;

    public static int Stamp(EraAward a) => Stamp(a.cycle, a.echo, a.phase, a.seventh);

    /// <summary>The awards oldest first: by calendar, then by the order they were made.</summary>
    public static List<EraAward> Chronological(IEnumerable<EraAward> awards) =>
        (awards ?? Enumerable.Empty<EraAward>()).Where(a => a != null).OrderBy(Stamp).ThenBy(a => a.order).ToList();

    /// <summary>"Cycle 2 (Overture), Echo 3".</summary>
    public static string EchoLabel(EraAward a) => $"Cycle {a.cycle}{(string.IsNullOrEmpty(a.cycleName) ? string.Empty : $" ({a.cycleName})")}, Echo {a.echo}";

    /// <summary>"Phase 2, Seventh 14".</summary>
    public static string DayLabel(EraAward a) => $"Phase {a.phase}, Seventh {a.seventh}";

    /// <summary>
    /// The timeline as sections: one per Age (its title and total), each with its awards in order under Cycle/Echo
    /// headings, each line dated to its Phase and Seventh and its Act of Fate.
    /// </summary>
    public static List<(string heading, List<(string echo, List<string> lines)> echoes)> Sections(IEnumerable<EraAward> awards, Func<EraAward, string> actLabel = null)
    {
        var sections = new List<(string, List<(string, List<string>)>)>();
        var ordered = Chronological(awards);
        foreach (var age in ordered.GroupBy(a => (a.ageNumber, a.ageId)).OrderBy(g => g.Min(Stamp)))
        {
            var first = age.First();
            var echoes = new List<(string, List<string>)>();
            foreach (var echo in age.GroupBy(EchoLabel))
            {
                var lines = echo.Select(a => $"{DayLabel(a)}{(actLabel != null ? $" ({actLabel(a)})" : string.Empty)}: {a.points:+0;-0} {a.reason}").ToList();
                echoes.Add((echo.Key, lines));
            }
            sections.Add(($"{first.ageTitle ?? first.ageId}: {age.Sum(a => a.points):+0;-0;0} Era Score", echoes));
        }
        return sections;
    }
}
