using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One voice of a cast, as the evaluation reads it.</summary>
public sealed class CastMember
{
    public string name;
    public ComposureState composure;
    /// <summary>The repertoire's binding score (Resonance, Strand...).</summary>
    public int binding;
}

/// <summary>One Legend's reading of another in the cast (directional: the reverse is its own tie).</summary>
public sealed class CastTie
{
    public string from, to;
    public int stage, encounters;
    public bool significant, sharedWound;
}

/// <summary>Everything a performance's outcome depends on (no scene state).</summary>
public sealed class PerformanceContext
{
    public RepertoireSpec repertoire;
    public PerformanceIntent intent;
    public List<CastMember> cast = new List<CastMember>();
    public List<CastTie> ties = new List<CastTie>();
    public string place;
    public float strain;
    /// <summary>The settlement keeps the repertoire's custom (T02).</summary>
    public bool keepsCustom;
    /// <summary>How rooted the people are in the settlement's cell, 0-1.</summary>
    public float rootedness;
    /// <summary>The loss this remembrance sings of happened near here (its title), or null.</summary>
    public string lossHere;
}

/// <summary>One line of a performance's explanation and what it adds to the score.</summary>
public sealed class PerformanceFactor
{
    public string label;
    public float delta;

    public override string ToString() => $"{label} ({(delta >= 0f ? "+" : "")}{delta:0.00})";
}

/// <summary>How a performance would land, and why (deterministic: the same context always gives the same result).</summary>
public sealed class PerformanceEvaluation
{
    public float score;
    public PerformanceBand band;
    public readonly List<PerformanceFactor> factors = new List<PerformanceFactor>();
    public List<string> Lines => factors.Select(f => f.ToString()).ToList();
}

/// <summary>What a world observation says about a booking this Seventh.</summary>
public struct BookingObservation
{
    public bool unitExists, inSettlement, festivalRunning, settlementStanding;
    public int voices;
}

/// <summary>
/// Performances with no scene (tested in <c>PerformanceTests</c>). Research inference (R4: who takes part and their
/// ties matter alongside what is spent) and the Waltz Pillar (an orchestra carries a faltering voice) shape the rules;
/// every number is a proposal (<see cref="PerformanceTuning"/>).
/// - Evaluation is deterministic: each voice's readiness (Composure, the repertoire's binding), the ensemble (the weakest
///   voice counts less as the cast grows, a few voices more are better), the Legends' own readings of each other
///   (consonant ties help, dissonant ones hurt, a shared wound is heard in a remembrance) and the place.
/// - A booking is a reservation of the party's festival for the piece; it lapses, is called off or fails with nothing
///   given, and completes once.
/// - A completed performance leaves an echo: strain eased in its settlement each Seventh for a while, a small morale
///   source for the strongest few, and, for the later Ages' choruses only, Coherence lent around it (an overlay the
///   world reads; the ground itself never changes).
/// </summary>
public static class PerformanceRules
{
    public static PerformanceState Ensure(PerformanceState state)
    {
        if (state == null) state = new PerformanceState();
        if (state.bookings == null) state.bookings = new List<PerformanceBooking>();
        if (state.echoes == null) state.echoes = new List<PerformanceEcho>();
        if (state.history == null) state.history = new List<PerformanceRecord>();
        state.bookings.RemoveAll(b => b == null);
        state.echoes.RemoveAll(e => e == null);
        state.history.RemoveAll(h => h == null);
        foreach (var b in state.bookings) if (b.cast == null) b.cast = new List<string>();
        foreach (var h in state.history)
        {
            if (h.cast == null) h.cast = new List<string>();
            if (h.factors == null) h.factors = new List<string>();
        }
        // A serial below a saved id would hand out an id twice.
        foreach (var id in state.bookings.Select(b => b.id).Concat(state.history.Select(h => h.id)))
            if (id != null && id.StartsWith("perf-") && int.TryParse(id.Substring(5), out int n)) state.serial = Math.Max(state.serial, n);
        return state;
    }

    public static string IntentName(PerformanceIntent i) =>
        i == PerformanceIntent.Reassurance ? "reassurance" : i == PerformanceIntent.Remembrance ? "remembrance" : i == PerformanceIntent.Celebration ? "celebration" : "restoration";

    public static string BandName(PerformanceBand b) =>
        b == PerformanceBand.Faltering ? "Faltering" : b == PerformanceBand.Steady ? "Steady" : b == PerformanceBand.Moving ? "Moving" : "Resonant";

    public static string ComposureName(ComposureState s) => s.ToString();

    // ===== ELIGIBILITY =====

    /// <summary>
    /// Why <paramref name="spec"/> cannot be performed for <paramref name="intent"/> now, or null. Restoration needs its
    /// later Age and its civic in force: an early song never becomes a map-healing spell.
    /// </summary>
    public static string WhyNotRepertoire(RepertoireSpec spec, PerformanceIntent intent, int age, Func<string, bool> researched, Func<string, bool> civicActive,
        bool lossKnown, int voices)
    {
        if (spec == null) return "No such piece.";
        if (age < spec.minAge) return $"{spec.name} belongs to Age {Roman(spec.minAge)}{(spec.maxAge >= 0 ? $"-{Roman(spec.maxAge)}" : "+")}: not yet.";
        if (spec.maxAge >= 0 && age > spec.maxAge) return $"{spec.name} was of Ages {Roman(spec.minAge)}-{Roman(spec.maxAge)}: it is not performed any more.";
        if (!string.IsNullOrEmpty(spec.technology) && !(researched?.Invoke(spec.technology) ?? false)) return $"{spec.name} needs {spec.technology}.";
        if (!string.IsNullOrEmpty(spec.civic) && !(civicActive?.Invoke(spec.civic) ?? false)) return $"{spec.name} is performed by the {spec.civic}: that civic must be in force.";
        if (spec.intents == null || !spec.intents.Contains(intent)) return $"{spec.name} is not performed for {IntentName(intent)}.";
        if (spec.needsLoss && !lossKnown) return $"{spec.name} sings of a loss the people remember: there is none yet.";
        if (voices < Math.Max(1, spec.minCast)) return $"{spec.name} needs {spec.minCast} voices ({voices} in the party).";
        return null;
    }

    private static string Roman(int n) => n <= 0 ? "0" : new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[Math.Min(9, n - 1)];

    // ===== EVALUATION =====

    /// <summary>A voice's readiness: its Composure, and its repertoire's binding.</summary>
    public static float Readiness(CastMember m, PerformanceTuning t)
    {
        if (m == null) return 0f;
        var table = t.composureReadiness ?? new float[0];
        float composure = (int)m.composure < table.Length ? table[(int)m.composure] : 0f;
        if (composure <= 0f) return 0f;
        float binding = Math.Max(t.bindingMin, Math.Min(t.bindingMax, (m.binding - t.bindingBase) * t.perBindingPoint));
        return Math.Max(0f, composure + binding);
    }

    public static PerformanceBand BandOf(float score, PerformanceTuning t) =>
        score >= t.resonantFrom ? PerformanceBand.Resonant : score >= t.movingFrom ? PerformanceBand.Moving : score >= t.steadyFrom ? PerformanceBand.Steady : PerformanceBand.Faltering;

    /// <summary>How the performance would land and why. Changes nothing.</summary>
    public static PerformanceEvaluation Evaluate(PerformanceContext c, PerformanceTuning t)
    {
        t = t ?? new PerformanceTuning();
        var e = new PerformanceEvaluation();
        var cast = (c?.cast ?? new List<CastMember>()).Where(m => m != null && !string.IsNullOrEmpty(m.name)).ToList();
        if (cast.Count == 0)
        {
            e.factors.Add(new PerformanceFactor { label = "No one to perform", delta = 0f });
            e.band = PerformanceBand.Faltering;
            return e;
        }
        string binding = c.repertoire?.binding ?? "binding";

        // The ensemble: every voice's readiness, the weakest counting less as more voices carry it.
        var ready = cast.Select(m => (m, r: Readiness(m, t))).ToList();
        float mean = ready.Average(x => x.r), min = ready.Min(x => x.r);
        float core = mean - (mean - min) / cast.Count;
        string voices = string.Join("; ", ready.Select(x => $"{x.m.name} {ComposureName(x.m.composure)}, {binding} {x.m.binding}: {x.r:0.00}"));
        string carry = cast.Count > 1 && mean - min > 1e-3f ? $"; with {cast.Count} voices the weakest counts for 1/{cast.Count}" : string.Empty;
        e.factors.Add(new PerformanceFactor { label = $"Readiness {core:0.00} ({voices}{carry})", delta = core * t.readinessWeight });
        if (cast.Count > 1)
            e.factors.Add(new PerformanceFactor { label = $"{cast.Count} voices together (an orchestra, not a soloist)", delta = Math.Min(t.ensembleMax, t.perExtraVoice * (cast.Count - 1)) });

        // The history between them, each Legend's own reading.
        float ties = 0f;
        var names = cast.Select(m => m.name).ToList();
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                var ab = Tie(c.ties, names[i], names[j]);
                var ba = Tie(c.ties, names[j], names[i]);
                float pair = Direction(ab, c.intent, t) + Direction(ba, c.intent, t);
                pair = Math.Max(-t.pairMax, Math.Min(t.pairMax, pair));
                if (Math.Abs(pair) < 1e-4f) continue;
                ties += pair;
                e.factors.Add(new PerformanceFactor { label = $"{names[i]} and {names[j]}: {Reading(ab)} / {Reading(ba)}{(c.intent == PerformanceIntent.Remembrance && ((ab?.sharedWound ?? false) || (ba?.sharedWound ?? false)) ? ", a wound they share" : string.Empty)}", delta = pair });
            }
        float boundedTies = Math.Max(t.tiesMin, Math.Min(t.tiesMax, ties));
        if (Math.Abs(boundedTies - ties) > 1e-4f) e.factors.Add(new PerformanceFactor { label = "Ties counted within their bound", delta = boundedTies - ties });

        // The place.
        if (c.keepsCustom) e.factors.Add(new PerformanceFactor { label = $"{c.place} keeps it as its own custom", delta = t.keepsCustom });
        if (c.rootedness > 0.01f) e.factors.Add(new PerformanceFactor { label = $"Rooted in {c.place} ({c.rootedness:P0})", delta = t.rootedness * Math.Min(1f, c.rootedness) });
        if (c.intent == PerformanceIntent.Remembrance && !string.IsNullOrEmpty(c.lossHere)) e.factors.Add(new PerformanceFactor { label = $"{c.lossHere} is remembered here", delta = t.lossHere });
        if (c.strain >= t.fracturedStrain) e.factors.Add(new PerformanceFactor { label = $"{c.place} is too troubled to hear easily (strain {c.strain:0})", delta = t.fracturedTown });

        e.score = Math.Max(0f, Math.Min(1f, e.factors.Sum(f => f.delta)));
        e.band = BandOf(e.score, t);
        return e;
    }

    private static CastTie Tie(List<CastTie> ties, string from, string to) =>
        ties?.FirstOrDefault(x => x != null && string.Equals(x.from, from, StringComparison.OrdinalIgnoreCase) && string.Equals(x.to, to, StringComparison.OrdinalIgnoreCase));

    private static float Direction(CastTie tie, PerformanceIntent intent, PerformanceTuning t)
    {
        if (tie == null) return 0f;
        if (!tie.significant) return tie.encounters >= t.familiarEncounters ? t.familiar : 0f;
        float d = tie.stage > 0 ? t.perConsonantStage * tie.stage : tie.stage < 0 ? -t.perDissonantStage * -tie.stage : 0f;
        if (intent == PerformanceIntent.Remembrance && tie.sharedWound) d += t.sharedWoundRemembrance;
        return d;
    }

    private static string Reading(CastTie tie) =>
        tie == null ? "strangers" : !tie.significant ? (tie.encounters > 0 ? $"{tie.encounters} shared moments" : "strangers") : LegendRelationshipRules.StageName(tie.stage);

    // ===== WHAT IT LEAVES =====

    /// <summary>The echo a completed performance leaves (null when Faltering: nothing lingers).</summary>
    public static PerformanceEcho EchoOf(PerformanceBooking b, RepertoireSpec spec, PerformanceBand band, int cell, int now, bool restorationUnlocked, PerformanceTuning t)
    {
        float share = Share(band, t);
        if (b == null || share <= 0f) return null;
        var fx = t.Effect(b.intent);
        bool restore = b.intent == PerformanceIntent.Restoration && restorationUnlocked;
        return new PerformanceEcho
        {
            id = b.id, settlement = b.settlement, place = b.place, repertoire = b.repertoire, name = $"{spec?.name ?? b.repertoire} in {b.place}",
            intent = b.intent, band = band, relief = Math.Max(0f, fx.reliefPerSeventh * share), morale = MoraleOf(fx.morale, share),
            coherence = restore ? Math.Max(0f, fx.coherence * share) : 0f, radius = restore ? Math.Max(0, fx.coherenceRadius) : 0, cell = cell,
            started = now, until = now + Math.Max(1, fx.echoSevenths),
        };
    }

    public static float Share(PerformanceBand band, PerformanceTuning t) => t.bandShare != null && (int)band < t.bandShare.Length ? Math.Max(0f, t.bandShare[(int)band]) : 0f;

    /// <summary>Morale from an echo: only a performance that moved people gives any (Moving: the intent's; Resonant: half again, rounded).</summary>
    public static float MoraleOf(float morale, float share) => share < 1f || morale <= 0f ? 0f : (float)Math.Round(morale * share, MidpointRounding.AwayFromZero);

    /// <summary>Affection a performance adds to an existing significant bond (direction 0: never a new tie, never a stage).</summary>
    public static int AffectionOf(PerformanceBand band, PerformanceTuning t) => t.bandAffection != null && (int)band < t.bandAffection.Length ? Math.Max(0, t.bandAffection[(int)band]) : 0;

    /// <summary>"Composure strain eased by 3 a Seventh in Ashford for 7 Sevenths; +1 morale while it lasts".</summary>
    public static string EchoText(PerformanceEcho e)
    {
        if (e == null) return "nothing lingers";
        var parts = new List<string>();
        if (e.relief > 0f) parts.Add($"{e.place}'s strain eased by {e.relief:0.#} a Seventh");
        if (e.morale > 0f) parts.Add($"+{e.morale:0} morale");
        if (e.coherence > 0f) parts.Add($"+{e.coherence:0.00} Coherence within {e.radius} of it");
        return (parts.Count == 0 ? "nothing lingers" : string.Join("; ", parts)) + $" for {Math.Max(0, e.until - e.started)} Sevenths";
    }

    // ===== BOOKINGS =====

    public static bool Active(PerformanceBooking b) => b != null && (b.status == PerformanceStatus.Planned || b.status == PerformanceStatus.Performing);

    public static PerformanceBooking ForUnit(PerformanceState s, int unit) => s?.bookings.FirstOrDefault(b => Active(b) && b.unit == unit);

    public static PerformanceBooking AtSettlement(PerformanceState s, int settlement) => s?.bookings.FirstOrDefault(b => Active(b) && b.settlement == settlement);

    public static PerformanceBooking Book(PerformanceState s, int unit, string unitName, int settlement, string place, string repertoire, PerformanceIntent intent,
        IEnumerable<string> cast, string cause, int now)
    {
        var b = new PerformanceBooking
        {
            id = "perf-" + (++s.serial), unit = unit, unitName = unitName, settlement = settlement, place = place, repertoire = repertoire, intent = intent,
            cast = (cast ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).ToList(), cause = cause, status = PerformanceStatus.Planned, planned = now,
        };
        s.bookings.Add(b);
        return b;
    }

    /// <summary>
    /// A Seventh's look at a booking: it starts when its festival is seen under way, and ends (the reason) when its party is
    /// gone, walked away, its festival stopped, its place fell, or it waited too long. Null: it goes on.
    /// </summary>
    public static string Check(PerformanceBooking b, BookingObservation o, int now, PerformanceTuning t)
    {
        if (!Active(b)) return null;
        if (!o.settlementStanding) return $"{b.place} is no longer yours.";
        if (!o.unitExists) return $"{b.unitName} is gone.";
        if (!o.inSettlement) return $"{b.unitName} left {b.place}.";
        if (o.voices <= 0) return $"No one of {b.unitName} is left to perform.";
        if (o.festivalRunning)
        {
            if (b.status == PerformanceStatus.Planned) { b.status = PerformanceStatus.Performing; b.started = now; }
            return null;
        }
        if (b.status == PerformanceStatus.Performing) return $"The festival in {b.place} stopped before its end.";
        if (now - b.planned >= Math.Max(1, t.planSevenths)) return $"No festival was held in {b.place} within {t.planSevenths} Sevenths.";
        return null;
    }

    /// <summary>End a booking with nothing given (cancelled or failed), kept in the history with why.</summary>
    public static PerformanceRecord End(PerformanceState s, PerformanceBooking b, PerformanceStatus status, string reason, string name, int now, string ageId, PerformanceTuning t)
    {
        if (!Active(b)) return null;
        b.status = status;
        s.bookings.Remove(b);
        var r = new PerformanceRecord
        {
            id = b.id, settlement = b.settlement, place = b.place, repertoire = b.repertoire, name = name ?? b.repertoire, intent = b.intent, status = status,
            cast = new List<string>(b.cast), seventh = now, ageId = ageId, text = $"{name ?? b.repertoire} in {b.place}: {(status == PerformanceStatus.Failed ? "failed" : "called off")}. {reason}",
        };
        Remember(s, r, t);
        return r;
    }

    /// <summary>
    /// Complete an active booking, once: the record is kept and its echo replaces any older echo in the same settlement.
    /// Null (nothing done) when the booking is not active: a replayed completion changes nothing.
    /// </summary>
    public static PerformanceRecord Complete(PerformanceState s, PerformanceBooking b, string name, IList<string> cast, PerformanceEvaluation e, PerformanceEcho echo,
        int now, string ageId, PerformanceTuning t)
    {
        if (s == null || !Active(b) || e == null) return null;
        b.status = PerformanceStatus.Completed;
        s.bookings.Remove(b);
        s.completed++;
        if (echo != null)
        {
            s.echoes.RemoveAll(x => x.settlement == echo.settlement);
            s.echoes.Add(echo);
        }
        var r = new PerformanceRecord
        {
            id = b.id, settlement = b.settlement, place = b.place, repertoire = b.repertoire, name = name ?? b.repertoire, intent = b.intent, status = PerformanceStatus.Completed,
            band = e.band, score = e.score, cast = (cast ?? b.cast).ToList(), factors = e.Lines, seventh = now, ageId = ageId,
            text = $"{name ?? b.repertoire} in {b.place} for {IntentName(b.intent)}, by {string.Join(", ", cast ?? b.cast)}: {BandName(e.band)} ({e.score:0.00}); {EchoText(echo)}.",
        };
        Remember(s, r, t);
        return r;
    }

    private static void Remember(PerformanceState s, PerformanceRecord r, PerformanceTuning t)
    {
        s.history.Add(r);
        int keep = Math.Max(1, t.historyKept);
        if (s.history.Count > keep) s.history.RemoveRange(0, s.history.Count - keep);
    }

    // ===== ECHOES =====

    /// <summary>Echoes whose time is up or whose settlement is gone are dropped. Returns those dropped.</summary>
    public static List<PerformanceEcho> Expire(PerformanceState s, int now, Func<int, bool> standing)
    {
        var gone = s.echoes.Where(e => now >= e.until || !(standing?.Invoke(e.settlement) ?? true)).ToList();
        foreach (var e in gone) s.echoes.Remove(e);
        return gone;
    }

    /// <summary>The morale sources in force: the strongest few echoes (by morale, then the newest), one named source each.</summary>
    public static List<PerformanceEcho> MoraleEchoes(PerformanceState s, PerformanceTuning t) =>
        (s?.echoes ?? new List<PerformanceEcho>()).Where(e => e.morale > 0f).OrderByDescending(e => e.morale).ThenByDescending(e => e.started).ThenBy(e => e.id, StringComparer.Ordinal)
            .Take(Math.Max(0, t.maxMoraleEchoes)).ToList();

    /// <summary>Coherence the echoes lend a cell (<paramref name="distanceTo"/>: from an echo's cell to it, or -1), capped.</summary>
    public static float CoherenceAt(PerformanceState s, Func<int, int> distanceTo, PerformanceTuning t)
    {
        if (s == null || s.echoes.Count == 0 || distanceTo == null) return 0f;
        float sum = 0f;
        foreach (var e in s.echoes)
        {
            if (e.coherence <= 0f || e.cell < 0) continue;
            int d = distanceTo(e.cell);
            if (d >= 0 && d <= e.radius) sum += e.coherence;
        }
        return Math.Min(Math.Max(0f, t.coherenceCap), sum);
    }
}
