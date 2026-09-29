using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>A piece a party could perform, for one purpose, with why not (null: it can) and how it would land.</summary>
public sealed class RepertoireChoice
{
    public RepertoireSpec spec;
    public PerformanceIntent intent;
    public string why;
    public PerformancePreview preview;
}

/// <summary>What planning a performance would do, computed without changing anything.</summary>
public sealed class PerformancePreview
{
    public bool ok;
    /// <summary>Why it cannot be planned now, or (when it can) how it would land and what it would leave.</summary>
    public string reason;
    public string repertoire, name, place, cause, causeTitle;
    public PerformanceIntent intent;
    public int settlement = -1;
    public List<string> cast = new List<string>();
    public PerformanceEvaluation evaluation;
    /// <summary>The echo it would leave at the band previewed (null: Faltering leaves nothing).</summary>
    public PerformanceEcho echo;
}

/// <summary>A performance planned or under way, as a snapshot.</summary>
public sealed class PerformanceBookingView
{
    public string id, unitName, place, repertoire, name, observance;
    public int unit, settlement, planned, started;
    public PerformanceIntent intent;
    public PerformanceStatus status;
    public string[] cast;
}

/// <summary>An echo still heard, as a snapshot.</summary>
public sealed class PerformanceEchoView
{
    public string id, name, place, text;
    public int settlement, until;
    public PerformanceIntent intent;
    public PerformanceBand band;
    public float relief, morale, coherence;
    /// <summary>Its morale applies now (one of the strongest few).</summary>
    public bool moraleInForce;
}

/// <summary>A performance as history, as a snapshot.</summary>
public sealed class PerformanceRecordView
{
    public string id, name, place, text;
    public int settlement, seventh;
    public PerformanceIntent intent;
    public PerformanceStatus status;
    public PerformanceBand band;
    public float score;
    public string[] cast, factors;
}

/// <summary>
/// Ensembles whose relationships affect their performance (T08; rules in <see cref="PerformanceRules"/>, saved in
/// <see cref="CultureExtensionState.performance"/>). "The people performing together matter, and their history can be
/// heard in the result."
///
/// - A cultural party standing in one of your settlements plans a performance for its festival: a piece of its Age and
///   a purpose (reassurance, remembrance, celebration; restoration only for the later Ages' choruses). The preview
///   reads the Legends' readiness (Composure, the piece's binding), their readings of each other and the place, and
///   shows the band and every reason; nothing is kept until it is planned, and planning is free.
/// - The festival stays the single completion owner (<see cref="WorldSystem"/> FinishFestival): at its end the
///   performance completes once, with the voices still there. It leaves an echo (strain eased in the settlement for a
///   while, a small named morale source for the strongest few, Coherence for a later chorus), one Performance
///   occurrence, and a shared memory through <see cref="LegendProgress.ShareExperience"/> with no direction and no wound:
///   ordinary co-performance never makes a significant tie, never passes the seven-bond cap, never changes a stage. It
///   grants no Meaning (the festival's own fragments are the reward).
/// - A planned performance whose party leaves, disbands, stops its festival or waits too long is called off with
///   nothing given, and the reason kept.
/// </summary>
public partial class CultureSystem
{
    /// <summary>Performance's numbers and repertoire (code defaults: every number is a proposal).</summary>
    public static readonly PerformanceTuning PerformanceDefaults = new PerformanceTuning();
    public PerformanceTuning PerformanceTuning => PerformanceDefaults;

    // Morale sources applied now (transient: re-derived from the saved echoes after a load).
    private readonly HashSet<string> _performanceSources = new HashSet<string>(StringComparer.Ordinal);

    private PerformanceState PerformanceData
    {
        get
        {
            if (_state.extensions == null) _state.extensions = new CultureExtensionState();
            return _state.extensions.performance ?? (_state.extensions.performance = PerformanceRules.Ensure(null));
        }
    }

    private static WorldMap PerformanceMap => WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;

    // ===== WHAT A PARTY BRINGS =====

    /// <summary>The Legends of a party who can perform (met and not lost).</summary>
    public List<string> Voices(WorldUnit unit)
    {
        var legends = LegendProgress.Instance;
        return Expeditions.Members(unit).Where(m => !string.IsNullOrEmpty(m) && (legends == null || legends.IsRecruited(m))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<CastMember> CastOf(IEnumerable<string> names, string binding)
    {
        var legends = LegendProgress.Instance;
        return names.Select(n =>
        {
            int score = 0;
            if (legends != null && !string.IsNullOrEmpty(binding)) legends.Bindings(n).TryGetValue(binding, out score);
            return new CastMember { name = n, composure = legends != null ? legends.Composure(n) : ComposureState.Clouded, binding = score };
        }).ToList();
    }

    private static List<CastTie> TiesOf(IList<string> names)
    {
        var ties = new List<CastTie>();
        var legends = LegendProgress.Instance;
        if (legends == null) return ties;
        foreach (string from in names)
            foreach (var bond in legends.Relationships(from))
                if (bond != null && names.Contains(bond.other, StringComparer.OrdinalIgnoreCase))
                    ties.Add(new CastTie { from = from, to = bond.other, stage = bond.severed ? 0 : bond.stage, significant = bond.Significant, sharedWound = bond.sharedWound, encounters = bond.encounters });
        return ties;
    }

    // The loss a remembrance sings of in this settlement: the nearest remembered with a place within reach, else the latest.
    private MemoryEvidence CauseFor(Settlement s, out bool here)
    {
        here = false;
        var causes = MemoryCauses();
        if (causes.Count == 0) return null;
        var map = PerformanceMap;
        MemoryEvidence best = null;
        int bestDistance = int.MaxValue;
        if (map != null && s != null)
            foreach (var c in causes.Where(c => c.cell >= 0 && c.cell < map.Count))
            {
                int d = HexCoord.Distance(map[c.cell].coord, s.coord);
                if (d <= PerformanceTuning.lossRadius && d < bestDistance) { best = c; bestDistance = d; }
            }
        here = best != null;
        return best ?? causes.Last();
    }

    private bool CivicInForce(string civic) => CivicManager.Instance != null && !string.IsNullOrEmpty(civic) && CivicManager.Instance.IsCivicActive(civic);

    private string WhyNotPiece(RepertoireSpec spec, PerformanceIntent intent, int voices) =>
        PerformanceRules.WhyNotRepertoire(spec, intent, AgeNow, Researched, CivicInForce, MemoryCauses().Count > 0, voices);

    private bool RestorationUnlocked(RepertoireSpec spec) => spec != null && WhyNotPiece(spec, PerformanceIntent.Restoration, int.MaxValue) == null;

    // ===== PREVIEW =====

    /// <summary>How <paramref name="repertoire"/> for <paramref name="intent"/> would land at the party's festival, or why it cannot be planned. Nothing is changed.</summary>
    public PerformancePreview PreviewPerformance(WorldUnit unit, string repertoire, PerformanceIntent intent) => PreviewPerformance(unit, repertoire, intent, null);

    public PerformancePreview PreviewPerformance(WorldUnit unit, string repertoire, PerformanceIntent intent, string observance)
    {
        var p = new PerformancePreview { repertoire = repertoire, intent = intent };
        string Fail(string why) { p.ok = false; p.reason = why; return why; }
        var world = WorldSystem.Instance;
        var t = PerformanceTuning;
        var spec = t.Repertoire(repertoire);
        p.name = spec?.name ?? repertoire;
        if (!_state.founded) { Fail("The culture has not been founded yet."); return p; }
        if (unit == null || world == null) { Fail("No party selected."); return p; }
        if (!WorldUnits.Can(world.SpecOf(unit), UnitAbility.Celebrate)) { Fail("Only a cultural party performs."); return p; }
        var s = world.SettlementAt(unit);
        if (s == null) { Fail("A performance is given at a festival in one of your settlements: walk the party into one."); return p; }
        p.settlement = s.id;
        p.place = s.name;
        p.cast = Voices(unit);

        // How it would land (shown even when it cannot be planned yet, so the player sees why a cast matters).
        bool lossHere = false;
        var cause = spec != null && spec.needsLoss ? CauseFor(s, out lossHere) : null;
        p.cause = cause?.id;
        p.causeTitle = cause?.title;
        var map = world.Map;
        var cell = map.Get(s.coord);
        var context = new PerformanceContext
        {
            repertoire = spec, intent = intent, cast = CastOf(p.cast, spec?.binding), ties = TiesOf(p.cast), place = s.name, strain = s.strain,
            keepsCustom = spec != null && !string.IsNullOrEmpty(spec.localCustom) && LocalProfile(s.id).Kept.Any(k => string.Equals(k.practice, spec.localCustom, StringComparison.OrdinalIgnoreCase)),
            rootedness = cell != null ? PresenceAt(cell.index) : 0f, lossHere = lossHere ? cause?.title : null,
        };
        p.evaluation = PerformanceRules.Evaluate(context, t);
        var draft = new PerformanceBooking { id = "preview", settlement = s.id, place = s.name, repertoire = repertoire, intent = intent };
        p.echo = PerformanceRules.EchoOf(draft, spec, p.evaluation.band, cell?.index ?? -1, _state.sevenths, RestorationUnlocked(spec), t);

        // Why not, in the order the player meets it.
        string why = WhyNotPiece(spec, intent, p.cast.Count);
        if (why != null) { Fail(why); return p; }
        var data = PerformanceData;
        var mine = PerformanceRules.ForUnit(data, unit.id);
        if (mine != null) { Fail($"{unit.name} already plans {t.Repertoire(mine.repertoire)?.name ?? mine.repertoire} for {PerformanceRules.IntentName(mine.intent)}: call it off first."); return p; }
        var other = PerformanceRules.AtSettlement(data, s.id);
        if (other != null) { Fail($"{other.unitName} already plans a performance in {s.name}."); return p; }
        string festival = observance != null ? WhyNotPerformanceHost(unit, observance, intent) : unit.task == UnitTask.Festival && unit.Working ? null : world.WhyNotFestival(unit);
        if (festival != null) { Fail(observance != null ? festival : $"It is given at the party's festival, which cannot be held now: {festival}"); return p; }
        p.ok = true;
        p.reason = $"{p.name} for {PerformanceRules.IntentName(intent)} in {s.name} by {string.Join(", ", p.cast)}: {PerformanceRules.BandName(p.evaluation.band)} ({p.evaluation.score:0.00}), "
            + $"leaving {PerformanceRules.EchoText(p.echo)}{(p.causeTitle != null ? $"; it sings of {p.causeTitle}" : string.Empty)}. It is given at the end of the party's festival; planning costs nothing.";
        if (observance != null) p.reason = p.reason.Replace("at the end of the party's festival", "when " + ObservanceOf(observance).name + " is kept; the party must remain here and free");
        return p;
    }

    /// <summary>Every piece and purpose the party could plan where it stands, with why not and how each would land.</summary>
    public IReadOnlyList<RepertoireChoice> RepertoireChoices(WorldUnit unit)
    {
        var list = new List<RepertoireChoice>();
        foreach (var spec in PerformanceTuning.repertoires.Where(r => r != null))
            foreach (var intent in spec.intents)
            {
                var preview = PreviewPerformance(unit, spec.id, intent);
                list.Add(new RepertoireChoice { spec = spec, intent = intent, why = preview.ok ? null : preview.reason, preview = preview });
            }
        return list;
    }

    // ===== COMMANDS =====

    /// <summary>Plan the party's performance (free): it is given at the end of its festival in the settlement it stands in.</summary>
    public CultureCommandResult PlanPerformance(WorldUnit unit, string repertoire, PerformanceIntent intent, string observance = null)
    {
        var p = PreviewPerformance(unit, repertoire, intent, observance);
        if (!p.ok) return CultureCommandResult.Fail(p.reason);
        var b = PerformanceRules.Book(PerformanceData, unit.id, unit.name, p.settlement, p.place, repertoire, intent, p.cast, p.cause, _state.sevenths);
        b.observance = observance;
        if (observance != null) b.occasion = ObservanceOf(observance).nextDate;
        GameLog.Event($"{unit.name} plans {p.name} for {PerformanceRules.IntentName(intent)} in {p.place} ({b.id}).", Log);
        RaiseChanged();
        return CultureCommandResult.Ok(p.reason);
    }

    /// <summary>Call a planned performance off: nothing was spent and nothing is given; the festival itself goes on.</summary>
    public CultureCommandResult CancelPerformance(string id)
    {
        var data = PerformanceData;
        var b = data.bookings.FirstOrDefault(x => PerformanceRules.Active(x) && x.id == id);
        if (b == null) return CultureCommandResult.Fail("No such performance is planned.");
        var r = PerformanceRules.End(data, b, PerformanceStatus.Cancelled, "Called off.", PerformanceTuning.Repertoire(b.repertoire)?.name, _state.sevenths, AgeIdNow, PerformanceTuning);
        RaiseChanged();
        return CultureCommandResult.Ok(r.text);
    }

    /// <summary>
    /// The party's festival in <paramref name="s"/> ended (called once by the festival's completion, its single owner):
    /// its planned performance is given with the voices still there, or fails. Returns what the festival's notice adds,
    /// or null when nothing was planned or it was already given (a replay changes nothing).
    /// </summary>
    public string CompletePerformance(WorldUnit unit, Settlement s, IList<string> members, string observance = null)
    {
        if (!_state.founded || unit == null || s == null) return null;
        var data = PerformanceData;
        var b = PerformanceRules.ForUnit(data, unit.id);
        if (b == null || b.settlement != s.id || b.observance != observance) return null;
        if (observance != null && (b.occasion != Today || WhyNotPerformanceHost(unit, observance, b.intent, b) != null)) return null;
        var t = PerformanceTuning;
        var spec = t.Repertoire(b.repertoire);
        var legends = LegendProgress.Instance;
        var cast = (members ?? new List<string>()).Where(m => !string.IsNullOrEmpty(m) && (legends == null || legends.IsRecruited(m))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        string why = WhyNotPiece(spec, b.intent, cast.Count);
        if (why != null)
        {
            var failed = PerformanceRules.End(data, b, PerformanceStatus.Failed, why, spec?.name, _state.sevenths, AgeIdNow, t);
            RaiseChanged();
            return failed.text;
        }

        var map = PerformanceMap;
        var cell = map?.Get(s.coord);
        bool lossHere = false;
        var cause = spec.needsLoss ? (RememberedCause(b.cause) ?? CauseFor(s, out lossHere)) : null;
        if (cause != null && map != null && cause.cell >= 0 && cause.cell < map.Count) lossHere = HexCoord.Distance(map[cause.cell].coord, s.coord) <= t.lossRadius;
        var context = new PerformanceContext
        {
            repertoire = spec, intent = b.intent, cast = CastOf(cast, spec.binding), ties = TiesOf(cast), place = s.name, strain = s.strain,
            keepsCustom = !string.IsNullOrEmpty(spec.localCustom) && LocalProfile(s.id).Kept.Any(k => string.Equals(k.practice, spec.localCustom, StringComparison.OrdinalIgnoreCase)),
            rootedness = cell != null ? PresenceAt(cell.index) : 0f, lossHere = lossHere ? cause?.title : null,
        };
        var e = PerformanceRules.Evaluate(context, t);
        var echo = PerformanceRules.EchoOf(b, spec, e.band, cell?.index ?? -1, _state.sevenths, RestorationUnlocked(spec), t);
        string id = b.id;
        var intent = b.intent;
        var record = PerformanceRules.Complete(data, b, spec.name, cast, e, echo, _state.sevenths, AgeIdNow, t);
        if (record == null) return null;

        // One occurrence (the traditions hear a performance; T01's Evening Song listens for it).
        Record(new CulturalOccurrence
        {
            key = "performance:" + id, kind = CulturalOccurrenceKind.Performance, stamp = Stamp(), settlement = s.id,
            subject = CultureEntityRef.Of(CultureEntityKind.Activity, "perform:" + spec.id, spec.name),
            source = observance == null ? CultureEntityRef.Of(CultureEntityKind.Activity, "festival", "a festival") : CultureEntityRef.Of(CultureEntityKind.Observance, observance, ObservanceOf(observance)?.name),
            evidence = cause != null ? cause.Ref : new CultureEntityRef(),
            actors = new List<string>(cast), quantity = cast.Count, unit = CultureQuantityUnit.Participants, text = record.text,
        });
        // A shared memory, no direction, no wound: never a new significant tie, never a stage. Keyed once per performance.
        if (cast.Count > 1)
            legends?.ShareExperience(cast, "performance:" + id, $"Performed {spec.name} together in {s.name} ({PerformanceRules.BandName(e.band)}).", 0, "Strand", false, PerformanceRules.AffectionOf(e.band, t));
        if (echo != null && echo.coherence > 0f && map != null) map.CivilizationVersion++;
        ApplyPerformanceEffects();
        if (data.completed == 1) Remember("performance", $"{spec.name} in {s.name}", $"{string.Join(", ", cast)} performed {spec.name} in {s.name} for {PerformanceRules.IntentName(intent)}.");
        GameLog.Event($"Performance {id}: {record.text}", Log);
        RaiseChanged();
        return $"{spec.name} for {PerformanceRules.IntentName(intent)}: {PerformanceRules.BandName(e.band)}; {PerformanceRules.EchoText(echo)}.";
    }

    // ===== THE SEVENTH =====

    /// <summary>
    /// A Seventh of performance: planned ones whose party left, disbanded, stopped its festival or waited too long are
    /// called off (nothing given); echoes ease their settlement's strain, then those whose time is up fade; the morale
    /// sources follow the echoes still heard.
    /// </summary>
    internal void PerformanceSeventh()
    {
        var data = PerformanceData;
        var t = PerformanceTuning;
        var map = PerformanceMap;
        int now = _state.sevenths;
        foreach (var b in data.bookings.Where(PerformanceRules.Active).ToList())
        {
            var o = new BookingObservation();
            var unit = map?.Units.FirstOrDefault(u => u != null && u.id == b.unit);
            o.settlementStanding = map != null && WorldCivilization.Get(map, b.settlement) != null;
            o.unitExists = unit != null;
            o.inSettlement = unit != null && WorldSystem.Instance.SettlementAt(unit)?.id == b.settlement;
            o.festivalRunning = unit != null && unit.task == UnitTask.Festival && unit.Working;
            o.voices = unit != null ? Voices(unit).Count : 0;
            string why = b.observance != null ? WhyNotPerformanceHost(unit, b.observance, b.intent, b) : PerformanceRules.Check(b, o, now, t);
            if (why == null) continue;
            var r = PerformanceRules.End(data, b, PerformanceStatus.Cancelled, why, t.Repertoire(b.repertoire)?.name, now, AgeIdNow, t);
            if (r != null) GameLog.Event($"Performance {r.id} called off: {why}", Log);
        }
        if (map != null)
            foreach (var e in data.echoes.Where(e => e.relief > 0f && now > e.started && now <= e.until))
            {
                var s = WorldCivilization.Get(map, e.settlement);
                if (s != null) s.strain = Mathf.Max(0f, s.strain - e.relief);
            }
        var faded = PerformanceRules.Expire(data, now, id => map == null || WorldCivilization.Get(map, id) != null);
        if (faded.Any(e => e.coherence > 0f) && map != null) map.CivilizationVersion++;
        ApplyPerformanceEffects();
    }

    /// <summary>One named morale source per echo among the strongest few; every other source this culture applied is removed.</summary>
    internal void ApplyPerformanceEffects()
    {
        var wanted = PerformanceRules.MoraleEchoes(PerformanceData, PerformanceTuning)
            .Select(e => (source: CultureEffectPolicy.Source(e.name), morale: e.morale)).GroupBy(x => x.source).Select(g => g.First()).ToList();
        foreach (string stale in _performanceSources.Where(s => wanted.All(w => w.source != s)).ToList())
        {
            EffectRouter.ApplySet(stale, new List<GameEffect>());
            _performanceSources.Remove(stale);
        }
        foreach (var (source, morale) in wanted)
        {
            EffectRouter.ApplySet(source, new List<GameEffect> { new GameEffect(GameEffectType.MoraleModifier, morale, ModifierType.Add) });
            _performanceSources.Add(source);
        }
    }

    internal void ReconcilePerformance()
    {
        if (_state.extensions == null) _state.extensions = new CultureExtensionState();
        _state.extensions.performance = PerformanceRules.Ensure(_state.extensions.performance);
        CityDevelopment.CoherenceOverlay = PerformanceCoherence;
        ApplyPerformanceEffects();
    }

    // The overlay the world's development reads: Coherence lent by later choruses, never the ground's own.
    private static float PerformanceCoherence(WorldTile t) => Instance != null && t != null ? Instance.CoherenceOverlay(t) : 0f;

    /// <summary>Coherence performances lend <paramref name="t"/> now (0 without a later chorus's echo nearby).</summary>
    public float CoherenceOverlay(WorldTile t)
    {
        var data = _state.extensions?.performance;
        var map = PerformanceMap;
        if (t == null || data == null || data.echoes.Count == 0 || map == null) return 0f;
        return PerformanceRules.CoherenceAt(data, cell => cell >= 0 && cell < map.Count ? HexCoord.Distance(map[cell].coord, t.coord) : -1, PerformanceTuning);
    }

    // ===== READS (snapshots) =====

    private string PieceName(string id) => PerformanceTuning.Repertoire(id)?.name ?? id;

    public IReadOnlyList<PerformanceBookingView> PerformanceBookings() => PerformanceData.bookings.Where(PerformanceRules.Active).Select(b => new PerformanceBookingView
    {
        id = b.id, unit = b.unit, unitName = b.unitName, settlement = b.settlement, place = b.place, repertoire = b.repertoire, name = PieceName(b.repertoire),
        intent = b.intent, status = b.status, planned = b.planned, started = b.started, cast = b.cast.ToArray(), observance = b.observance,
    }).ToList();

    /// <summary>The party's planned performance, or null.</summary>
    public PerformanceBookingView PerformanceOf(int unit) => PerformanceBookings().FirstOrDefault(b => b.unit == unit);

    public IReadOnlyList<PerformanceEchoView> PerformanceEchoes()
    {
        var data = PerformanceData;
        var inForce = PerformanceRules.MoraleEchoes(data, PerformanceTuning);
        return data.echoes.Select(e => new PerformanceEchoView
        {
            id = e.id, name = e.name, place = e.place, settlement = e.settlement, until = e.until, intent = e.intent, band = e.band,
            relief = e.relief, morale = e.morale, coherence = e.coherence, text = PerformanceRules.EchoText(e), moraleInForce = inForce.Contains(e),
        }).ToList();
    }

    public IReadOnlyList<PerformanceRecordView> PerformanceHistory(int max = 20) => PerformanceData.history.AsEnumerable().Reverse().Take(Mathf.Max(0, max)).Select(r => new PerformanceRecordView
    {
        id = r.id, name = r.name, place = r.place, settlement = r.settlement, seventh = r.seventh, intent = r.intent, status = r.status, band = r.band, score = r.score,
        text = r.text, cast = r.cast.ToArray(), factors = r.factors.ToArray(),
    }).ToList();

    public int PerformancesCompleted => PerformanceData.completed;
}

/// <summary>Performance's reads for the culture's queries: snapshots only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<PerformanceBookingView> PerformanceBookings();
    IReadOnlyList<PerformanceEchoView> PerformanceEchoes();
    IReadOnlyList<PerformanceRecordView> PerformanceHistory(int max = 20);
    PerformancePreview PreviewPerformance(WorldUnit unit, string repertoire, PerformanceIntent intent);
}

/// <summary>Performance in the Seventh's social effects: bookings checked, echoes heard and faded, morale sources kept in step.</summary>
public sealed class PerformanceFeature : ICultureFeature
{
    public string Id => "performance";
    public CulturePhase Phase => CulturePhase.SocialEffects;
    public int Order => 30;

    public void Seventh(CultureSeventh context) => context?.Culture?.PerformanceSeventh();

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcilePerformance();
}
