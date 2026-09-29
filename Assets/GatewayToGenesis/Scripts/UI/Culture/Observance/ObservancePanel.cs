using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Calendar panel of the Culture window (<see cref="CultureWindow"/>, its Holidays button) and its section in the
/// window's body: the observances kept (holidays among them), what each asks and gives, and setting a new one apart
/// with its cause, place, rite, food, recurrence and scale. It only reads <see cref="ObservanceView"/> snapshots and
/// previews and calls the culture's commands; every cost and condition is shown before anything is committed.
/// </summary>
public static class ObservancePanel
{
    // The observance open (null: the list), or the plan being made for a new one (null: none).
    private static string _selected;
    private static ObservancePlan _plan;

    /// <summary>Back to the list (the window was opened afresh).</summary>
    public static void Reset()
    {
        _selected = null;
        _plan = null;
    }

    /// <summary>
    /// Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row (label, why not
    /// or null, tooltip, action).
    /// </summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        if (_plan != null && culture.ObservanceDefinitionOf(_plan.definition) != null) { FillPlan(culture, header, choice); return; }
        _plan = null;
        var open = _selected != null ? culture.ObservanceOf(_selected) : null;
        if (open != null) { FillObservance(culture, open, header, choice); return; }
        _selected = null;

        var now = culture.Now;
        header($"Today is the {CultureCalendar.Ordinal(now.seventh)} Seventh of the {now.phaseName ?? CultureCalendar.PhaseWord(now.phase) + " Phase"}{(now.echoName != null ? ", " + now.echoName : string.Empty)}, Cycle {now.cycle}"
            + $"{(now.seventh == ObservanceCalendar.PerPhase ? " (the Ritual Seventh, the full Moon)" : string.Empty)}. What the people keep, when, and how: a quiet day costs nothing; a feast can be prepared ahead, scaled down or postponed.");
        foreach (var d in culture.ObservanceTuning.definitions.Where(d => d != null))
        {
            var def = d;
            var plan = culture.DefaultObservancePlan(def.id);
            var preview = culture.PreviewEstablishObservance(plan);
            string label = def.id == ObservanceRules.Anniversary ? $"Set today apart as a holiday ({culture.NextHolidayCost:0} {culture.UnityResource})" : $"Set apart: {def.name} {TooltipText.Muted($"({ObservanceRules.ObjectiveWord(def.objective)})")}";
            // A definition whose Age or technology is not reached stays listed with its reason (unimplemented later content stays visibly unavailable).
            choice(label, OpenWhy(preview, def, culture), Tip(def, preview), () => { _plan = plan; _selected = null; });
        }
        foreach (var o in culture.Observances().Where(o => !o.ended))
        {
            var view = o;
            string when = view.seventhsUntil == 0 ? TooltipText.Good("today") : view.seventhsUntil < 0 ? "no day ahead" : $"in {view.seventhsUntil} Seventh{(view.seventhsUntil == 1 ? "" : "s")}";
            string flags = (view.prepared ? ", table prepared" : string.Empty) + (view.postponed ? ", postponed" : string.Empty);
            choice($"{view.name}: {when} {TooltipText.Muted($"({ObservanceRules.ScaleWord(view.scale)}{flags})")}", null, Tip(view), () => _selected = view.id);
        }
    }

    // Only the reasons that stop a plan from being made at all (Age, technology, the founding, a holiday's own gates)
    // keep a definition closed; the rest are chosen in the plan.
    private static string OpenWhy(CultureCommandResult preview, ObservanceDefinition d, CultureSystem culture)
    {
        if (preview.succeeded) return null;
        if (d.id == ObservanceRules.Anniversary) return preview.reason;
        if (!CultureSystem.IsFounded) return preview.reason;
        if (!d.OpenIn(GameAge.Number)) return preview.reason;
        if (!string.IsNullOrEmpty(d.technology) && preview.reason.StartsWith("Needs " + d.technology)) return preview.reason;
        if (preview.reason.StartsWith("The calendar already")) return preview.reason;
        return null;
    }

    // ===== A NEW OBSERVANCE =====

    private static void FillPlan(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var d = culture.ObservanceDefinitionOf(_plan.definition);
        var p = _plan;
        var preview = culture.PreviewEstablishObservance(p);
        header($"{d.name}: {d.description} {TooltipText.Muted(d.canonNote)}");
        choice("< All observances", null, "Back to the calendar.", () => _plan = null);
        bool holiday = d.id == ObservanceRules.Anniversary;

        if (!holiday)
        {
            if (d.recurrences.Count > 1)
                choice($"Kept {ObservanceRules.RecurrenceWord(p.recurrence)} {TooltipText.Muted("(change)")}", null, "How often it comes round.", () =>
                {
                    p.recurrence = Next(d.recurrences, p.recurrence);
                    if (p.recurrence == ObservanceRecurrence.RitualSeventh) { p.seventh = ObservanceCalendar.PerPhase; p.phase = 0; }
                    else if (p.phase <= 0) p.phase = 1;
                });
            if (p.recurrence == ObservanceRecurrence.RitualSeventh)
                choice($"On {ObservanceCalendar.DayText(p.recurrence, p.seventh, p.phase, p.echo)} {TooltipText.Muted("(change)")}", null,
                    "The Ritual Seventh is the 21st of every Phase, when the folk calendar's Moon is full.", () => p.phase = (p.phase + 1) % (TimeSystemLogic.PhasesPerEcho + 1));
            else
            {
                choice($"On {ObservanceCalendar.DayText(p.recurrence, p.seventh, p.phase, p.echo)}", null, "Its day. Move it a Seventh later or earlier below.", () => { });
                choice("  a Seventh later", null, null, () => Shift(p, 1));
                choice("  a Seventh earlier", null, null, () => Shift(p, -1));
            }
        }
        if (d.grounds != null && d.grounds.Count > 0)
        {
            var venues = culture.ObservanceVenues(d.id).Select(s => s.id).ToList();
            string place = p.settlement >= 0 ? (culture.ObservanceVenues(d.id).FirstOrDefault(s => s.id == p.settlement)?.name ?? "a settlement") : "no place";
            choice($"Kept in {place}{(venues.Count > 1 ? TooltipText.Muted(" (change)") : string.Empty)}", venues.Count == 0 ? $"None of your settlements lies by {d.groundText}." : null,
                $"It needs {d.groundText}.", () => p.settlement = Next(venues, p.settlement));
        }
        if (d.needsCause)
        {
            var causes = culture.ObservanceCauses().ToList();
            choice($"Remembering {p.causeText ?? "no loss"}{(causes.Count > 1 ? TooltipText.Muted(" (change)") : string.Empty)}", causes.Count == 0 ? "Your people remember no loss yet (Memorials)." : null,
                "What it keeps: a loss your people remember.", () =>
                {
                    int i = causes.FindIndex(c => c.Same(p.cause));
                    var next = causes[(i + 1) % causes.Count];
                    p.cause = next.Copy();
                    p.causeText = culture.RememberedCause(next.id)?.title ?? next.Display;
                });
        }
        if (d.repertoire != null && d.repertoire.Count > 0)
        {
            var options = new List<string> { null };
            options.AddRange(d.repertoire);
            string rite = p.repertoire != null ? culture.Life.Activity(p.repertoire)?.name ?? p.repertoire : "no rite";
            choice($"With {rite} {TooltipText.Muted("(change)")}", null, "The rite performed on the day (a performance or a gathering the traditions remember).", () => p.repertoire = Next(options, p.repertoire));
        }
        if (d.food)
        {
            var foods = new List<CultureEntityRef> { new CultureEntityRef() };
            foods.AddRange(culture.ObservanceFoods(d.id));
            string food = p.food != null && p.food.IsKnown ? culture.FoodLabel(p.food) : holiday ? "a table from the stores" : "no chosen dish";
            choice($"Serving {food} {TooltipText.Muted("(change)")}", null, "A named dish, tea or drink is served by its own stock (prepared ahead, or taken on the day); none: the table is drawn from the stores' food value.", () =>
            {
                int i = foods.FindIndex(f => f.IsKnown ? f.Same(p.food) : !(p.food != null && p.food.IsKnown));
                p.food = foods[(i + 1) % foods.Count].Copy();
            });
        }
        var scales = d.scales != null && d.scales.Count > 0 ? d.scales : new List<ObservanceScale> { ObservanceScale.Quiet, ObservanceScale.Modest, ObservanceScale.Full };
        if (scales.Count > 1) choice($"Kept {ObservanceRules.ScaleWord(p.scale)} {TooltipText.Muted("(change)")}", null, "Quiet asks nothing of the stores and is always possible; a table asks for food.", () => p.scale = Next(scales, p.scale));

        string label = holiday ? "Set today apart" : $"Set apart {d.name}{(preview.paid.Count > 0 ? $" ({preview.PaidText})" : " (free)")}";
        choice(label, preview.succeeded ? null : preview.reason, preview.succeeded ? preview.reason : Tip(d, preview), () =>
        {
            var result = culture.EstablishObservance(p);
            if (result.succeeded) _plan = null;
        });
    }

    private static void Shift(ObservancePlan p, int by)
    {
        int index = (Math.Max(1, p.phase) - 1) * ObservanceCalendar.PerPhase + (p.seventh - 1) + by;
        int span = ObservanceCalendar.PerEcho;
        index = ((index % span) + span) % span;
        p.phase = index / ObservanceCalendar.PerPhase + 1;
        p.seventh = index % ObservanceCalendar.PerPhase + 1;
    }

    private static T Next<T>(IList<T> options, T current)
    {
        if (options == null || options.Count == 0) return current;
        int i = options.IndexOf(current);
        return options[(i + 1) % options.Count];
    }

    // ===== ONE OBSERVANCE =====

    private static void FillObservance(CultureSystem culture, ObservanceView o, Action<string> header, Action<string, string, string, Action> choice)
    {
        header($"{o.name}: {o.day}{(o.place != "the nation" ? $", in {o.place}" : string.Empty)}{(o.cause != null ? $", remembering {o.cause}" : string.Empty)}. Next: {o.next}{(o.seventhsUntil >= 0 ? $" (in {o.seventhsUntil})" : string.Empty)}. {TooltipText.Muted(o.plan)}");
        choice("< All observances", null, "Back to the calendar.", () => _selected = null);
        if (o.prepared) choice($"Put its table away {TooltipText.Muted($"({string.Join(", ", o.reserved.Select(r => $"{r.amount:0.#} {r.resource}"))} back to the stores)")}", null, "Cancel the preparation: everything reserved returns to the stores.", () => culture.CancelObservancePreparation(o.id));
        else
        {
            var prepare = culture.PreviewPrepareObservance(o.id);
            choice("Prepare its table now", prepare.succeeded ? null : prepare.reason, prepare.succeeded ? prepare.reason : null, () => culture.PrepareObservance(o.id));
        }
        foreach (int by in new[] { 3, 7 })
        {
            string why = culture.WhyNotPostponeObservance(o.id, by);
            choice($"Postpone its next day by {by} Sevenths", why, "A feast the stores cannot bear yet can wait; the day itself stays for the Echoes after.", () => culture.PostponeObservance(o.id, by));
        }
        var d = culture.ObservanceDefinitionOf(o.definition);
        if (d != null)
        {
            var scales = d.scales != null && d.scales.Count > 0 ? d.scales : new List<ObservanceScale> { ObservanceScale.Quiet, ObservanceScale.Modest, ObservanceScale.Full };
            var current = CurrentPlan(culture, o.id);
            foreach (var s in scales.Where(s => s != o.scale))
            {
                var scale = s;
                string why = culture.WhyNotConfigureObservance(o.id, scale, current.food, current.repertoire);
                choice($"Keep it {ObservanceRules.ScaleWord(scale)} from now on", why, (o.legacy && !o.customized ? "A holiday changed here is kept by the calendar's own rules from then on. " : string.Empty) + culture.PlanText(d, o.objective, scale, current.food, current.repertoire, false),
                    () => culture.ConfigureObservance(o.id, scale, current.food, current.repertoire));
            }
            if (d.food)
            {
                var foods = new List<CultureEntityRef> { new CultureEntityRef() };
                foods.AddRange(culture.ObservanceFoods(d.id));
                int i = foods.FindIndex(f => f.IsKnown ? f.Same(current.food) : !current.food.IsKnown);
                var next = foods[(i + 1) % foods.Count];
                string why = culture.WhyNotConfigureObservance(o.id, o.scale, next, current.repertoire);
                choice($"Serve {(next.IsKnown ? culture.FoodLabel(next) : "a table from the stores")} instead", why, null, () => culture.ConfigureObservance(o.id, o.scale, next, current.repertoire));
            }
        }
        if (!o.legacy) choice("Take it off the calendar", null, "Its history stays; a prepared table goes back to the stores.", () => { culture.RetireObservance(o.id); _selected = null; });
        foreach (var h in o.history.Reverse().Take(8))
            choice(TooltipText.Muted($"{ObservanceCalendar.DateText(h.date)}: {h.text}"), null, h.paid.Count > 0 ? "Spent: " + string.Join(", ", h.paid.Select(p => $"{p.amount:0.#} {p.resource}")) : "Nothing spent.", () => { });
    }

    // The saved choices of an observance, read through its snapshot (the view holds labels, the culture the references).
    private static (CultureEntityRef food, string repertoire) CurrentPlan(CultureSystem culture, string id)
    {
        var state = culture.Extensions.observances?.Get(id);
        return (state?.food?.Copy() ?? new CultureEntityRef(), state?.repertoire);
    }

    // ===== TEXT =====

    private static string Tip(ObservanceDefinition d, CultureCommandResult preview)
    {
        var lines = new List<string> { d.description };
        if (preview != null && preview.succeeded && !string.IsNullOrEmpty(preview.reason)) lines.Add(preview.reason);
        lines.Add(TooltipText.Row("For", ObservanceRules.ObjectiveWord(d.objective)));
        lines.Add(TooltipText.Row("Ages", d.maxAge < 0 ? (d.minAge <= 0 ? "every Age" : $"from Age {AgeRules.Roman(d.minAge)}") : $"{AgeRules.Roman(Math.Max(0, d.minAge))}-{AgeRules.Roman(d.maxAge)}"));
        if (d.grounds != null && d.grounds.Count > 0) lines.Add(TooltipText.Row("Where", d.groundText));
        lines.Add(TooltipText.Muted($"{CanonWord(d.canon)}{(string.IsNullOrEmpty(d.canonSource) ? string.Empty : ": " + d.canonSource)}"));
        return string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));
    }

    private static string Tip(ObservanceView o)
    {
        var lines = new List<string>
        {
            o.description,
            TooltipText.Row("Kept on", o.day),
            TooltipText.Row("Next", o.next),
            TooltipText.Row("Plan", o.plan),
            TooltipText.Row("Kept", $"{o.kept} time{(o.kept == 1 ? "" : "s")}{(o.keptSmaller > 0 ? $" ({o.keptSmaller} quietly for want of food)" : string.Empty)}{(o.missed > 0 ? $", {o.missed} passed unkept" : string.Empty)}"),
        };
        if (o.legacy) lines.Add(TooltipText.Muted(o.customized ? "A holiday, now kept by the calendar's rules." : "A holiday: kept as holidays always were until its plan is changed."));
        lines.Add(TooltipText.Muted($"{CanonWord(o.canon)}{(string.IsNullOrEmpty(o.canonSource) ? string.Empty : ": " + o.canonSource)}"));
        return string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));
    }

    private static string CanonWord(CanonStatus c) => c == CanonStatus.ExplicitCanon ? "Explicit canon" : c == CanonStatus.CanonSupported ? "Canon-supported adaptation" : "New game rule (proposal)";

    /// <summary>The observances besides holidays, for the window's calendar section (holidays are listed by the window itself).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        var list = culture.Observances().Where(o => !o.ended && !o.legacy).ToList();
        if (list.Count == 0) return;
        foreach (var o in list)
        {
            string when = o.seventhsUntil == 0 ? TooltipText.Good("today") : o.seventhsUntil < 0 ? "no day ahead" : $"in {o.seventhsUntil} Seventh{(o.seventhsUntil == 1 ? "" : "s")}";
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(o.name)}: {o.day}, {when} {TooltipText.Muted($"({ObservanceRules.ObjectiveWord(o.objective)}, {ObservanceRules.ScaleWord(o.scale)}; kept {o.kept} time{(o.kept == 1 ? "" : "s")})")}"));
        }
    }
}
