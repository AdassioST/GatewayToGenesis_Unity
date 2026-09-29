using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The observances' calendar, with no scene state. A date is a Seventh's index since the world's first
/// (<see cref="CultureStamp.Index"/>): the game's own clock, never the wall clock. An Echo is 63 Sevenths, a Cycle 252;
/// the 21st Seventh of every Phase is the Ritual Seventh, the full Moon of the folk calendar (Cycle.md).
/// </summary>
public static class ObservanceCalendar
{
    public const int PerPhase = TimeSystemLogic.SeventhsPerPhase;
    public const int PerEcho = PerPhase * TimeSystemLogic.PhasesPerEcho;
    public const int PerCycle = PerEcho * TimeSystemLogic.EchoesPerCycle;

    public static long Date(int cycle, int echo, int phase, int seventh) => CultureStamp.Index(cycle, echo, phase, seventh);

    public static long Date(CultureStamp s) => s == null ? 0 : s.Absolute;

    public static (int cycle, int echo, int phase, int seventh) Parts(long date)
    {
        if (date < 0) date = 0;
        return ((int)(date / PerCycle) + 1, (int)(date / PerEcho % TimeSystemLogic.EchoesPerCycle) + 1, (int)(date / PerPhase % TimeSystemLogic.PhasesPerEcho) + 1, (int)(date % PerPhase) + 1);
    }

    /// <summary>Whether <paramref name="o"/> falls on <paramref name="date"/> by its recurrence (a postponement aside).</summary>
    public static bool Falls(Observance o, long date)
    {
        if (o == null || date < 0) return false;
        var p = Parts(date);
        switch (o.recurrence)
        {
            case ObservanceRecurrence.EveryEcho: return p.seventh == o.seventh && p.phase == o.phase;
            case ObservanceRecurrence.OncePerCycle: return p.seventh == o.seventh && p.phase == o.phase && p.echo == o.echo;
            case ObservanceRecurrence.RitualSeventh: return p.seventh == PerPhase && (o.phase <= 0 || p.phase == o.phase);
            default: return false;
        }
    }

    /// <summary>The first date from <paramref name="from"/> (inclusive) it falls on, or -1 (a malformed day).</summary>
    public static long NextOn(Observance o, long from)
    {
        if (o == null) return -1;
        if (from < 0) from = 0;
        // Every recurrence comes round within a Cycle; step to the right Seventh of the Phase first.
        var p = Parts(from);
        int want = o.recurrence == ObservanceRecurrence.RitualSeventh ? PerPhase : o.seventh;
        if (want < 1 || want > PerPhase) return -1;
        long d = from + ((want - p.seventh) % PerPhase + PerPhase) % PerPhase;
        for (int i = 0; i <= PerCycle / PerPhase; i++, d += PerPhase)
            if (Falls(o, d)) return d;
        return -1;
    }

    /// <summary>
    /// The next occasion still to be decided after today (<paramref name="today"/>): the date it is kept on and its own
    /// date (different when postponed), or (-1, -1).
    /// </summary>
    public static (long keepOn, long original) NextOccasion(Observance o, long today)
    {
        if (o == null || o.ended) return (-1, -1);
        long d = NextOn(o, Math.Max(o.lastResolved + 1, today + 1));
        if (o.postponedFrom >= 0 && o.postponedFrom > o.lastResolved && o.postponedFrom <= today && o.postponedTo > today) return (o.postponedTo, o.postponedFrom);
        if (d < 0) return (-1, -1);
        return d == o.postponedFrom ? (o.postponedTo, d) : (d, d);
    }

    /// <summary>"the 20th Seventh of the second Phase of every Echo", "the 5th Seventh of the first Phase of the second Echo, once every Cycle", "every Ritual Seventh (the full Moon)".</summary>
    public static string DayText(ObservanceRecurrence recurrence, int seventh, int phase, int echo)
    {
        switch (recurrence)
        {
            case ObservanceRecurrence.OncePerCycle:
                return $"the {CultureCalendar.Ordinal(seventh)} Seventh of the {CultureCalendar.PhaseWord(phase)} Phase of the {EchoWord(echo)} Echo, once every Cycle";
            case ObservanceRecurrence.RitualSeventh:
                return phase <= 0 ? "every Ritual Seventh (the full Moon)" : $"the Ritual Seventh (the full Moon) of the {CultureCalendar.PhaseWord(phase)} Phase of every Echo";
            default:
                return CultureCalendar.Day(seventh, phase);
        }
    }

    public static string DayText(Observance o) => o == null ? string.Empty : DayText(o.recurrence, o.seventh, o.phase, o.echo);

    public static string EchoWord(int echo) => echo == 1 ? "first" : echo == 2 ? "second" : echo == 3 ? "third" : echo == 4 ? "fourth" : CultureCalendar.Ordinal(echo);

    /// <summary>"the 5th Seventh of Phase 2, Echo 3, Cycle 4": a date in words.</summary>
    public static string DateText(long date)
    {
        var p = Parts(date);
        return $"the {CultureCalendar.Ordinal(p.seventh)} Seventh of Phase {p.phase}, Echo {p.echo}, Cycle {p.cycle}";
    }
}

/// <summary>What a culture system answers for the establishment checks (pure tests pass their own).</summary>
public class ObservanceChecks
{
    public bool founded = true;
    public int age;
    public Func<string, bool> researched = _ => true;
    /// <summary>The ground words of a settlement (WorldSystem.SettlementGround), or null when it does not exist.</summary>
    public Func<int, string> groundOf = _ => string.Empty;
    public Func<CultureEntityRef, bool> causeKnown = _ => false;
    /// <summary>Why a rite cannot be performed (unknown, its technology unknown), or null.</summary>
    public Func<string, string> whyNotRite = _ => null;
    /// <summary>Why a food cannot be served at all (not a dish, tea or drink; recipe unknown), or null.</summary>
    public Func<CultureEntityRef, string> whyNotFood = _ => null;
    public Func<string, float> held = _ => 0f;
}

/// <summary>
/// The observances' rules, with no scene state (numbers in <see cref="ObservanceTuning"/>):
/// - Establishing checks the Age, technology, day, venue ground, cause, repertoire and food the definition asks for, and
///   the calendar's room; a remembrance needs no high morale and costs nothing to keep quietly.
/// - Each occasion is decided once (<see cref="Observance.lastResolved"/> only moves forward): kept on its day, missed
///   when its day passed unseen (a time jump: nothing spent, nothing given), postponed by the player.
/// - A table is prepared ahead by taking its food from the stores (a reservation); cancelling or missing the day returns
///   it whole; keeping the day serves it. Without a preparation, the table is revalidated on the day and, if the stores
///   cannot set it, the day is kept quietly (disclosed in every preview).
/// </summary>
public static class ObservanceRules
{
    /// <summary>The definition every holiday from before observances belongs to.</summary>
    public const string Anniversary = "anniversary";

    public static void Ensure(ObservanceState s)
    {
        if (s == null) return;
        if (s.observances == null) s.observances = new List<Observance>();
        if (s.preparations == null) s.preparations = new List<ObservancePreparation>();
        if (s.history == null) s.history = new List<ObservanceOccasion>();
        s.observances.RemoveAll(o => o == null);
        s.preparations.RemoveAll(p => p == null || s.Get(p.observance) == null);
        foreach (var o in s.observances)
        {
            if (o.venue == null) o.venue = new CultureEntityRef();
            if (o.cause == null) o.cause = new CultureEntityRef();
            if (o.food == null) o.food = new CultureEntityRef();
        }
        foreach (var p in s.preparations)
        {
            if (p.reserved == null) p.reserved = new List<ResourceAmount>();
            if (p.food == null) p.food = new CultureEntityRef();
        }
        if (s.nextId < 1) s.nextId = 1;
        foreach (var o in s.observances)
            if (o.id != null && o.id.StartsWith("obs-") && int.TryParse(o.id.Substring(4), out int n) && n >= s.nextId) s.nextId = n + 1;
        if (s.version < ObservanceState.CurrentVersion) s.version = ObservanceState.CurrentVersion;
    }

    // ===== ESTABLISHING =====

    /// <summary>Why <paramref name="p"/>'s day cannot be kept by <paramref name="d"/> (its recurrence and the calendar's shape), or null.</summary>
    public static string WhyNotDay(ObservanceDefinition d, ObservancePlan p)
    {
        if (d == null || p == null) return "No such observance.";
        if (d.recurrences != null && d.recurrences.Count > 0 && !d.recurrences.Contains(p.recurrence))
            return $"{d.name} is not kept {RecurrenceWord(p.recurrence)}.";
        if (p.recurrence == ObservanceRecurrence.RitualSeventh)
        {
            if (p.phase < 0 || p.phase > TimeSystemLogic.PhasesPerEcho) return "No such Phase.";
            return null;
        }
        if (p.seventh < 1 || p.seventh > ObservanceCalendar.PerPhase) return "A Phase has 21 Sevenths.";
        if (p.phase < 1 || p.phase > TimeSystemLogic.PhasesPerEcho) return "An Echo has three Phases.";
        if (p.recurrence == ObservanceRecurrence.OncePerCycle && (p.echo < 1 || p.echo > TimeSystemLogic.EchoesPerCycle)) return "A Cycle has four Echoes.";
        return null;
    }

    /// <summary>
    /// Why <paramref name="d"/> cannot be set apart with <paramref name="p"/> now, or null. Checked in the order a player
    /// meets them: the founding, the Age and technology, the calendar's room, the day, the place, the cause, the
    /// repertoire, the food, the scale, the cost. (A holiday's own gates, morale and a festival first, are the
    /// holiday's: <see cref="CultureLifeRules.WhyNotHoliday"/>.)
    /// </summary>
    public static string WhyNotEstablish(ObservanceDefinition d, ObservancePlan p, ObservanceChecks c, ObservanceState s, ObservanceTuning t)
    {
        t = t ?? new ObservanceTuning();
        c = c ?? new ObservanceChecks();
        if (d == null || p == null) return "No such observance.";
        if (!c.founded) return "The culture has not been founded yet.";
        if (!d.OpenIn(c.age)) return d.maxAge >= 0 && c.age > d.maxAge ? $"{d.name} belongs to Ages {AgeRules.Roman(Math.Max(0, d.minAge))}-{AgeRules.Roman(d.maxAge)}: it can no longer be newly set apart (one already kept stays)." : $"{d.name} begins in Age {AgeRules.Roman(d.minAge)}.";
        if (!string.IsNullOrEmpty(d.technology) && !(c.researched?.Invoke(d.technology) ?? false)) return $"Needs {d.technology}.";
        if (d.id != Anniversary && (s?.Active.Count(o => !o.legacy && o.definition != Anniversary) ?? 0) >= Math.Max(0, t.maxObservances))
            return $"The calendar already keeps {t.maxObservances} observances besides its holidays.";
        if (d.unique && s != null && s.Active.Any(o => string.Equals(o.definition, d.id, StringComparison.OrdinalIgnoreCase) && o.settlement == p.settlement))
            return $"{d.name} is already kept {(p.settlement >= 0 ? "there" : "by the nation")}.";
        string why = WhyNotDay(d, p);
        if (why != null) return why;
        if (s != null && s.Active.Any(o => string.Equals(o.definition, d.id, StringComparison.OrdinalIgnoreCase) && SameDay(o, p)))
            return $"{d.name} is already kept on that day.";
        if (d.grounds != null && d.grounds.Count > 0)
        {
            if (p.settlement < 0) return $"{d.name} is kept in a place: choose {d.groundText ?? "a settlement whose ground fits it"}.";
            string ground = c.groundOf?.Invoke(p.settlement);
            if (ground == null) return "That settlement no longer stands.";
            if (!d.FitsGround(ground)) return $"{d.name} needs {d.groundText ?? "another ground"}.";
        }
        if (d.needsCause && (p.cause == null || !p.cause.IsKnown || !(c.causeKnown?.Invoke(p.cause) ?? false)))
            return $"{d.name} remembers a loss: choose one your people remember (Memorials).";
        if (!string.IsNullOrEmpty(p.repertoire))
        {
            if (d.repertoire == null || !d.repertoire.Any(r => string.Equals(r, p.repertoire, StringComparison.OrdinalIgnoreCase))) return $"{d.name} is not kept with that rite.";
            why = c.whyNotRite?.Invoke(p.repertoire);
            if (why != null) return why;
        }
        if (p.food != null && p.food.IsKnown)
        {
            if (!d.food) return $"{d.name} sets no table.";
            why = c.whyNotFood?.Invoke(p.food);
            if (why != null) return why;
        }
        if (!d.Allows(p.scale)) return $"{d.name} is not kept {ScaleWord(p.scale)}.";
        foreach (var cost in d.establishCost ?? new List<ResourceAmount>())
            if (cost != null && cost.amount > 0f && (c.held?.Invoke(cost.resource) ?? 0f) + 1e-4f < cost.amount)
                return $"Needs {cost.amount:0.#} {cost.resource} ({c.held?.Invoke(cost.resource) ?? 0f:0.#} held).";
        return null;
    }

    // The same observance, for the same place and cause, on the same day (another grove or another loss keeps its own).
    private static bool SameDay(Observance o, ObservancePlan p) =>
        o.settlement == p.settlement && (o.cause?.Key ?? string.Empty) == (p.cause?.Key ?? string.Empty)
        && o.recurrence == p.recurrence && o.seventh == (p.recurrence == ObservanceRecurrence.RitualSeventh ? ObservanceCalendar.PerPhase : p.seventh) && o.phase == p.phase
        && (p.recurrence != ObservanceRecurrence.OncePerCycle || o.echo == p.echo);

    /// <summary>Put a new observance on the calendar; its first occasion is the next one after today.</summary>
    public static Observance Establish(ObservanceState s, ObservanceDefinition d, ObservancePlan p, string name, CultureStamp now)
    {
        Ensure(s);
        var o = new Observance
        {
            id = "obs-" + s.nextId++, definition = d.id, name = string.IsNullOrEmpty(name) ? d.name : name, objective = d.objective, recurrence = p.recurrence,
            seventh = p.recurrence == ObservanceRecurrence.RitualSeventh ? ObservanceCalendar.PerPhase : p.seventh, phase = p.phase,
            echo = p.recurrence == ObservanceRecurrence.OncePerCycle ? p.echo : 1, settlement = p.settlement, venue = p.venue?.Copy() ?? new CultureEntityRef(),
            cause = p.cause?.Copy() ?? new CultureEntityRef(), causeText = p.causeText, repertoire = p.repertoire, food = p.food?.Copy() ?? new CultureEntityRef(),
            scale = p.scale, established = now?.Copy(), lastResolved = ObservanceCalendar.Date(now),
        };
        s.observances.Add(o);
        return o;
    }

    /// <summary>
    /// Link the holidays from before observances (idempotent): each gets a legacy observance on its own day of every Echo,
    /// with its tallies; its occasions from today on are decided by the observance rules. Nothing about its past is
    /// invented. Returns how many were linked. <paramref name="resolvedThrough"/>: the last date already decided (default: today).
    /// </summary>
    public static int LinkHolidays(ObservanceState s, IEnumerable<Holiday> holidays, CultureStamp now, long resolvedThrough = long.MinValue)
    {
        Ensure(s);
        int linked = 0;
        foreach (var h in holidays ?? Enumerable.Empty<Holiday>())
        {
            if (h == null || string.IsNullOrEmpty(h.name)) continue;
            var o = s.observances.FirstOrDefault(x => x.legacy && string.Equals(x.holiday, h.name, StringComparison.OrdinalIgnoreCase));
            if (o != null)
            {
                o.kept = Math.Max(o.kept, h.kept);
                continue;
            }
            s.observances.Add(new Observance
            {
                id = "obs-" + s.nextId++, definition = Anniversary, name = h.name, objective = ObservanceObjective.Celebration, recurrence = ObservanceRecurrence.EveryEcho,
                seventh = h.seventh, phase = h.phase, echo = 1, legacy = true, holiday = h.name, causeText = h.occasion, scale = ObservanceScale.Full, kept = h.kept,
                established = new CultureStamp { cultureSeventh = h.established, cycle = Math.Max(1, h.cycle), echo = Math.Max(1, h.echo), phase = h.phase, seventh = h.seventh },
                lastResolved = resolvedThrough != long.MinValue ? resolvedThrough : ObservanceCalendar.Date(now),
            });
            linked++;
        }
        return linked;
    }

    // ===== THE DAYS =====

    /// <summary>
    /// The occasions of <paramref name="o"/> to decide by <paramref name="today"/>, oldest first: each one's own date,
    /// the date it is kept on, and whether it is kept now (true) or its day passed unseen (false). A postponed occasion
    /// waits for its new date. Nothing is changed.
    /// </summary>
    public static List<(long original, long keepOn, bool keep)> Due(Observance o, long today)
    {
        var list = new List<(long, long, bool)>();
        if (o == null || o.ended) return list;
        long cursor = o.lastResolved + 1;
        for (int guard = 0; guard < 64; guard++)
        {
            long d = ObservanceCalendar.NextOn(o, cursor);
            if (d < 0 || d > today) break;
            if (d == o.postponedFrom && o.postponedTo >= 0)
            {
                if (o.postponedTo > today) break;
                list.Add((d, o.postponedTo, o.postponedTo == today));
            }
            else list.Add((d, d, d == today));
            cursor = d + 1;
        }
        return list;
    }

    /// <summary>An occasion decided: the observance moves past it (never back) and tallies it.</summary>
    public static void Resolve(ObservanceState s, Observance o, long original, ObservanceOccasion occasion, ObservanceTuning t)
    {
        if (o == null) return;
        if (original > o.lastResolved) o.lastResolved = original;
        if (original == o.postponedFrom) o.postponedFrom = o.postponedTo = -1;
        if (occasion == null) return;
        switch (occasion.outcome)
        {
            case ObservanceOutcome.Kept: o.kept++; break;
            case ObservanceOutcome.KeptSmaller: o.kept++; o.keptSmaller++; break;
            case ObservanceOutcome.Missed:
            case ObservanceOutcome.Cancelled: o.missed++; break;
        }
        if (s == null) return;
        s.history.Add(occasion);
        int keep = Math.Max(1, t?.historyKept ?? 40);
        if (s.history.Count > keep) s.history.RemoveRange(0, s.history.Count - keep);
    }

    public static string OccasionKey(Observance o, long original) => $"obs:{o?.id}:{original}";

    // ===== THE TABLE =====

    /// <summary>Food value a full table asks for <paramref name="people"/>, times the scale's share.</summary>
    public static float FeastValue(ObservanceTuning t, int people, ObservanceScale scale)
    {
        t = t ?? new ObservanceTuning();
        float full = Math.Max(t.feastMinimum, t.feastPerHundred * Math.Max(0, people) / 100f);
        return full * Math.Max(0f, t.Scale(scale).food);
    }

    /// <summary>What keeping it gives at <paramref name="scale"/>: the objective's full rewards times the scale's share.</summary>
    public static (float unity, int morale, int sevenths, float joy, float lived) Rewards(ObservanceTuning t, ObservanceObjective objective, ObservanceScale scale)
    {
        t = t ?? new ObservanceTuning();
        var o = t.Objective(objective);
        float share = Math.Max(0f, t.Scale(scale).rewards);
        return (o.unity * share, (int)Math.Round(o.morale * share, MidpointRounding.AwayFromZero), Math.Max(1, o.moraleSevenths), o.joy * share, o.lived * share);
    }

    /// <summary>Why a table cannot be prepared for <paramref name="o"/>'s next occasion now, or null (then it is due on <paramref name="keepOn"/>).</summary>
    public static string WhyNotPrepare(ObservanceState s, Observance o, ObservanceDefinition d, long today, ObservanceTuning t, out long keepOn)
    {
        t = t ?? new ObservanceTuning();
        keepOn = -1;
        if (o == null || o.ended) return "No such observance.";
        if (s?.PreparationOf(o.id) != null) return "Its table is already prepared.";
        if (o.scale == ObservanceScale.Quiet) return "It is kept quietly: there is no table to prepare.";
        if (o.food == null || !o.food.IsKnown) return o.legacy && !o.customized ? "A holiday's table is drawn from the stores on its day; choose a dish to prepare one ahead." : "Choose what is served first.";
        var next = ObservanceCalendar.NextOccasion(o, today);
        if (next.keepOn < 0) return "It has no day ahead.";
        if (next.keepOn - today > Math.Max(0, t.prepareWindow)) return $"Its day is {next.keepOn - today} Sevenths away: a table is prepared at most {t.prepareWindow} Sevenths ahead.";
        keepOn = next.keepOn;
        return null;
    }

    /// <summary>Why the next occasion cannot be moved <paramref name="by"/> Sevenths later, or null.</summary>
    public static string WhyNotPostpone(Observance o, ObservanceDefinition d, long today, int by, ObservanceTuning t)
    {
        t = t ?? new ObservanceTuning();
        if (o == null || o.ended) return "No such observance.";
        if (o.recurrence == ObservanceRecurrence.RitualSeventh) return $"{o.name} is kept only under the full Moon: it cannot be moved.";
        if (o.postponedFrom >= 0 && o.postponedFrom > o.lastResolved) return "Its next occasion was already moved once.";
        if (by < 1 || by > Math.Max(1, t.postponeMax)) return $"It can be moved 1 to {t.postponeMax} Sevenths.";
        var next = ObservanceCalendar.NextOccasion(o, today);
        if (next.original < 0) return "It has no day ahead.";
        long following = ObservanceCalendar.NextOn(o, next.original + 1);
        if (following >= 0 && next.original + by >= following) return "That would run into its next occasion.";
        return null;
    }

    public static void Postpone(Observance o, long today, int by)
    {
        var next = ObservanceCalendar.NextOccasion(o, today);
        if (next.original < 0) return;
        o.postponedFrom = next.original;
        o.postponedTo = next.original + by;
        o.revision++;
    }

    // ===== WORDS =====

    public static string RecurrenceWord(ObservanceRecurrence r) =>
        r == ObservanceRecurrence.OncePerCycle ? "once every Cycle" : r == ObservanceRecurrence.RitualSeventh ? "on the Ritual Seventh" : "every Echo";

    public static string ScaleWord(ObservanceScale s) => s == ObservanceScale.Quiet ? "quietly" : s == ObservanceScale.Modest ? "at a modest table" : "at a full table";

    public static string ObjectiveWord(ObservanceObjective o)
    {
        switch (o)
        {
            case ObservanceObjective.Remembrance: return "remembrance";
            case ObservanceObjective.Hospitality: return "a shared table";
            case ObservanceObjective.Performance: return "performance";
            case ObservanceObjective.Gathering: return "a gathering";
            default: return "celebration";
        }
    }

    public static string OutcomeWord(ObservanceOutcome o)
    {
        switch (o)
        {
            case ObservanceOutcome.KeptSmaller: return "kept quietly (the stores could not set the table)";
            case ObservanceOutcome.Missed: return "passed unkept";
            case ObservanceOutcome.Postponed: return "postponed";
            case ObservanceOutcome.Cancelled: return "passed without its table (cancelled)";
            default: return "kept";
        }
    }
}
