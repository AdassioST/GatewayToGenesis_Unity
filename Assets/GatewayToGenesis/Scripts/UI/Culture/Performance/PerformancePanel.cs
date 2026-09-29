using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Performances panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body (T08):
/// choose a cultural party, a piece and its purpose, see how it would land and every reason why (each voice's
/// readiness, the ties between them, the place), plan it for the party's festival or call it off; and read the echoes
/// still heard and the performances given. Everything is text: no rhythm input or audio is needed. It only reads
/// snapshots and calls <see cref="CultureSystem.PlanPerformance"/> / <see cref="CultureSystem.CancelPerformance"/>.
/// </summary>
public static class PerformancePanel
{
    // What is being chosen, by ids only (a stale selection after a load shows the list again).
    private static int _unit = -1;
    private static string _repertoire, _last;
    private static PerformanceIntent _intent;

    /// <summary>Forget the choice (the window was opened afresh).</summary>
    public static void Reset()
    {
        _unit = -1;
        _repertoire = null;
        _last = null;
    }

    /// <summary>Open the panel on one party (its card's "Plan a performance").</summary>
    public static void Select(int unit)
    {
        Reset();
        _unit = unit;
    }

    private static List<WorldUnit> Parties()
    {
        var world = WorldSystem.Instance;
        return world == null || world.Map == null ? new List<WorldUnit>()
            : world.Map.Units.Where(u => u != null && WorldUnits.Can(world.SpecOf(u), UnitAbility.Celebrate)).OrderBy(u => u.name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row (label, why not or null, tooltip, action).</summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var world = WorldSystem.Instance;
        var parties = Parties();
        var unit = parties.FirstOrDefault(u => u.id == _unit);
        if (unit == null)
        {
            _unit = -1;
            var bookings = query.PerformanceBookings();
            header("A cultural party performs at its festival or a named calendar observance: the people performing together matter, and their history can be heard in the result. "
                + TooltipText.Muted($"Planned: {bookings.Count}. Echoes still heard: {query.PerformanceEchoes().Count}. Given: {culture.PerformancesCompleted}."));
            if (parties.Count == 0) choice(TooltipText.Muted("No cultural party is abroad: form one (an expedition chartered as a Cultural Party) to perform."), null, null, () => { });
            foreach (var p in parties)
            {
                var party = p;
                var b = culture.PerformanceOf(party.id);
                var s = world.SettlementAt(party);
                string where = s != null ? $"in {s.name}" : "on the road";
                choice($"{party.name} {TooltipText.Muted($"({where}; {(b != null ? $"plans {b.name} for {PerformanceRules.IntentName(b.intent)}" : "nothing planned")})")}", null,
                    "Choose it to see what it could perform where it stands.", () => Select(party.id));
            }
            foreach (var e in query.PerformanceEchoes())
                choice(TooltipText.Muted($"Echo: {e.name} ({PerformanceRules.BandName(e.band)}): {e.text}{(e.morale > 0f && !e.moraleInForce ? "; its morale waits (only the strongest apply)" : string.Empty)}"), null, null, () => { });
            foreach (var r in query.PerformanceHistory(5))
                choice(TooltipText.Muted($"Seventh {r.seventh}: {r.text}"), null, RecordTip(r), () => { });
            return;
        }

        var place = world.SettlementAt(unit);
        var planned = culture.PerformanceOf(unit.id);
        var choices = culture.RepertoireChoices(unit);
        var selected = choices.FirstOrDefault(c => c.spec.id == _repertoire && c.intent == _intent);
        string at = place != null ? $" in {place.name}" : " (on the road: performances are given in one of your settlements)";
        if (planned != null)
            header($"{unit.name}{at} plans {planned.name} for {PerformanceRules.IntentName(planned.intent)} ({(planned.observance != null ? "given when " + (culture.ObservanceOf(planned.observance)?.name ?? "the observance") + " is kept" : planned.status == PerformanceStatus.Performing ? "its festival is under way" : "given at the end of its festival")}), by {string.Join(", ", planned.cast)}.");
        else if (selected != null)
            header($"{unit.name}{at}: {selected.spec.name} for {PerformanceRules.IntentName(selected.intent)}. "
                + (selected.why == null ? selected.preview.reason : TooltipText.Bad(selected.why)) + "\n" + Explain(selected.preview));
        else
            header($"{unit.name}{at}: choose a piece and what it is for. Each shows how it would land with these voices here; choose one for every reason.");
        choice("< All parties", null, "Back to the parties, the echoes and the performances given.", () => Reset());
        if (planned != null)
        {
            string id = planned.id;
            if (planned.observance != null) choice("Calendar host: " + (culture.ObservanceOf(planned.observance)?.name ?? "missing observance"), null, "The party stays here and free until this occasion. Leaving or changing its date cancels the booking.", () => { });
            choice($"Call off {planned.name}", null, "Nothing was spent and nothing is given; the festival itself goes on.", () => _last = culture.CancelPerformance(id).reason);
        }
        foreach (var c in choices)
        {
            var pick = c;
            bool on = pick.spec.id == _repertoire && pick.intent == _intent;
            var e = pick.preview.evaluation;
            string band = e != null ? $"{PerformanceRules.BandName(e.band)} ({e.score:0.00})" : "-";
            string locked = pick.why != null ? TooltipText.Muted(" (not now)") : string.Empty;
            choice($"{(on ? "(o)" : "( )")} {pick.spec.name} for {PerformanceRules.IntentName(pick.intent)}: {band}{locked}", null, ChoiceTip(pick), () => { _repertoire = pick.spec.id; _intent = pick.intent; });
        }
        if (planned == null && selected != null)
        {
            var s = selected;
            choice($"Plan {s.spec.name} for {PerformanceRules.IntentName(s.intent)}", s.why, s.why == null ? s.preview.reason : null, () =>
            {
                var result = culture.PlanPerformance(unit, s.spec.id, s.intent);
                _last = result.reason;
            });
            foreach (var o in culture.Observances().Where(o => !o.ended && o.settlement == place?.id))
            {
                string host = o.id;
                var preview = culture.PreviewPerformance(unit, s.spec.id, s.intent, host);
                choice("Plan for " + o.name + " (" + o.next + ")", preview.ok ? null : preview.reason, preview.reason,
                    () => _last = culture.PlanPerformance(unit, s.spec.id, s.intent, host).reason);
            }
        }
        if (_last != null) choice(TooltipText.Muted(_last), null, _last, () => { });
    }

    /// <summary>Every reason, one per line: what the score is made of, and what the echo would leave.</summary>
    public static string Explain(PerformancePreview p)
    {
        if (p?.evaluation == null) return string.Empty;
        var lines = p.evaluation.factors.Select(f => TooltipText.Bullet(f.ToString())).ToList();
        lines.Add(TooltipText.Muted($"Score {p.evaluation.score:0.00}: {PerformanceRules.BandName(p.evaluation.band)}; it would leave {PerformanceRules.EchoText(p.echo)}."));
        return string.Join("\n", lines);
    }

    private static string ChoiceTip(RepertoireChoice c) => string.Join("\n", new[]
    {
        c.spec.description,
        TooltipText.Row("Canon", c.spec.canon == CanonStatus.ExplicitCanon ? "explicit canon" : c.spec.canon == CanonStatus.CanonSupported ? "canon-supported adaptation" : "new game rule"),
        TooltipText.Row("Vault", c.spec.canonSource),
        TooltipText.Row("Carried by", $"{c.spec.binding}; {c.spec.minCast}+ voices"),
        c.why != null ? TooltipText.Bad(c.why) : null,
        Explain(c.preview),
    }.Where(l => !string.IsNullOrEmpty(l)));

    private static string RecordTip(PerformanceRecordView r) => string.Join("\n", new[]
    {
        TooltipText.Row("Voices", string.Join(", ", r.cast)),
        r.factors.Length > 0 ? string.Join("\n", r.factors.Select(f => TooltipText.Bullet(f))) : null,
    }.Where(l => l != null));

    /// <summary>The Performances section of the window's body (nothing before the first performance is planned).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        ICultureQuery query = culture;
        var bookings = query.PerformanceBookings();
        var echoes = query.PerformanceEchoes();
        var history = query.PerformanceHistory(3);
        if (bookings.Count == 0 && echoes.Count == 0 && history.Count == 0) return;
        text.AppendLine(TooltipText.Heading("Performances", $"{culture.PerformancesCompleted} given"));
        foreach (var b in bookings) text.AppendLine(TooltipText.Bullet($"{b.unitName} plans {b.name} for {PerformanceRules.IntentName(b.intent)} in {b.place}."));
        foreach (var e in echoes) text.AppendLine(TooltipText.Bullet($"Still heard: {e.name} ({PerformanceRules.BandName(e.band)}): {e.text}."));
        foreach (var r in history) text.AppendLine(TooltipText.Muted($"Seventh {r.seventh}: {r.text}"));
        text.AppendLine(TooltipText.Muted("Readiness, the ties between the voices and the place decide how a performance lands. A shared performance adds a memory and a little affection to a bond, never a new tie or a changed stage."));
        text.AppendLine();
    }
}
