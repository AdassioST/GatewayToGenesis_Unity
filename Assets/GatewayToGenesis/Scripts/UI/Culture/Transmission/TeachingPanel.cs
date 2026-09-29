using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Teaching panel of the Culture window (<see cref="CultureWindow"/>) and its section in the window's body:
/// apprenticeships and institutions (<see cref="CultureSystem"/>, CultureSystem.Transmission.cs). A teaching is set up
/// step by step (tradition, preserve / adapt / write down, teacher, learner, dish, place), previewed, then begun; open
/// orders can be cancelled or given another teacher; institutions are founded at landmarks. It only reads the culture's
/// snapshots and calls its commands.
/// </summary>
public static class TeachingPanel
{
    private enum Stage { Mode, Teacher, Learner, Dish, Place, Confirm }

    // What the panel shows: an order's detail, the institutions, a teaching being set up, else the overview. Stale ids
    // after a load fall back to the overview.
    private static string _order;
    private static bool _institutions;
    private static TeachingDraft _draft;
    private static Stage _stage;

    /// <summary>Back to the overview (the window was opened afresh).</summary>
    public static void Reset()
    {
        _order = null;
        _institutions = false;
        _draft = null;
        _stage = Stage.Mode;
    }

    /// <summary>
    /// Fill the panel: <paramref name="header"/> sets its explanation, <paramref name="choice"/> adds a row
    /// (label, why not or null, tooltip, action).
    /// </summary>
    public static void Fill(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        if (culture == null) return;
        if (_order != null) { Order(culture, header, choice); return; }
        if (_institutions) { Institutions(culture, header, choice); return; }
        if (_draft != null && culture.Tradition(_draft.tradition) == null) _draft = null;
        if (_draft != null) { Draft(culture, header, choice); return; }
        Overview(culture, header, choice);
    }

    // ===== THE OVERVIEW =====

    private static void Overview(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var orders = culture.TeachingOrders();
        var open = orders.Where(o => o.Open).ToList();
        header($"Traditions survive because someone learned to carry them. A Legend who carries a custom, or the community that keeps it, can teach it at a hearth or an institution: as it is (a new bearer), adapted (a local form that keeps its parent), or written down (a record, which keeps no one practising it). Teaching takes Sevenths and a seat; nothing needs schooling to be practised.");
        choice($"Institutions {TooltipText.Muted($"({culture.TeachingInstitutions().Count(i => string.IsNullOrEmpty(i.evolvedInto))} founded; the hearth seats one teaching in every settlement)")}", null,
            "Found a Flavor Log, a Guild of Ballads and Plays or a Cooking Guild at a landmark, within its Ages: more seats, harder practices, written records.", () => _institutions = true);
        foreach (var o in open)
        {
            var order = o;
            choice($"{order.traditionName}: {Who(order)} {TooltipText.Muted($"({TransmissionRules.ModeWord(order.mode)}; {TransmissionRules.StatusWord(order)})")}", null, OrderTip(culture, order), () => _order = order.id);
        }
        foreach (var o in orders.Where(o => o.status == TeachingStatus.Completed).OrderByDescending(o => o.endedSeventh).Take(3))
            choice(TooltipText.Muted($"{o.traditionName}: {Who(o)} (completed at Seventh {o.endedSeventh})"), null, OrderTip(culture, o), () => { });
        var teachable = culture.TeachableTraditions();
        if (teachable.Count == 0)
        {
            choice(TooltipText.Muted("No custom to teach yet."), null, "A practice becomes a custom when it is kept over several Sevenths (Traditions); then it can be taught.", () => { });
            return;
        }
        foreach (var t in teachable.OrderBy(t => t.Lived ? 0 : 1).ThenBy(t => t.name, StringComparer.Ordinal))
        {
            var view = t;
            var carriers = culture.CarriersOf(view.id);
            choice($"Teach {view.name} {TooltipText.Muted($"({view.place}; {CarriedText(carriers)})")}", null, CarriersTip(culture, view, carriers), () =>
            {
                _draft = culture.DraftTeaching(view.id, TeachingMode.Preserve);
                _stage = Stage.Mode;
            });
        }
    }

    // ===== SETTING UP A TEACHING =====

    private static void Draft(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var view = culture.Tradition(_draft.tradition);
        var carriers = culture.CarriersOf(_draft.tradition);
        switch (_stage)
        {
            case Stage.Mode:
                header($"{view.name}, {view.place}: {CarriedText(carriers)}. {(carriers.Living ? string.Empty : TooltipText.Warn("No one living keeps it: only a record can teach it again (slower)."))}");
                choice("< All teaching", null, "Back to the teachings and traditions.", () => _draft = null);
                choice("Preserve: teach it as it is kept (a new bearer)", null, "The learner becomes one of its bearers: a Legend who can later teach it, or a settlement's people who keep it too.", () => Pick(culture, TeachingMode.Preserve, null));
                choice("Adapt: teach a local form of it (a documented variant)", null, "The learner keeps their own form of it, which remembers its parent for good. Adapting is harder than passing it on.", () => Pick(culture, TeachingMode.Adapt, null));
                choice("Write it down (an institution that keeps records)", null, "A Flavor Log or a guild writes it down from someone who keeps it. A record lets it be taught again when no one living remembers it; it keeps no one practising it.", () => Pick(culture, TeachingMode.Record, null));
                foreach (var v in carriers.variants)
                {
                    var variant = v;
                    choice($"Teach {variant.name} {TooltipText.Muted($"(a local form, {variant.settlementName})")}", null, $"Pass on {variant.settlementName}'s own form. Its parent is {variant.parentName}.", () => Pick(culture, TeachingMode.Preserve, variant.id));
                    choice($"Write down {variant.name}", null, "Record this local form at an institution that keeps records.", () => Pick(culture, TeachingMode.Record, variant.id));
                }
                return;
            case Stage.Teacher:
                header($"Who teaches {_draft.traditionName}? A Legend who carries it, the people who keep it (they teach where they live){(_draft.mode == TeachingMode.Record ? string.Empty : ", or a written record when no one living can")}.");
                choice("< Back", null, "Choose preserve, adapt or write down again.", () => _stage = Stage.Mode);
                var teachers = culture.TeacherOptions(_draft);
                if (teachers.Count == 0) choice(TooltipText.Muted("No one carries it, and nothing is written."), null, "A practice no one keeps and no one wrote down cannot be taught; it can still be taken up again by practising it.", () => { });
                foreach (var t in teachers)
                {
                    var teacher = t;
                    choice($"{teacher.label} {TooltipText.Muted($"({KindWord(teacher.kind)})")}", teacher.why, TeacherTip(teacher.kind), () =>
                    {
                        _draft.teacherKind = teacher.kind; _draft.teacher = teacher.id; _draft.teacherLabel = teacher.label;
                        _stage = _draft.mode == TeachingMode.Record ? Stage.Place : Stage.Learner;
                    });
                }
                return;
            case Stage.Learner:
                header($"Who learns {_draft.traditionName} from {_draft.teacherLabel}? A Legend, or the people of a settlement (they learn at their own hearth, or at an institution).");
                choice("< Back", null, "Choose the teacher again.", () => _stage = Stage.Teacher);
                foreach (var l in culture.LearnerOptions(_draft))
                {
                    var learner = l;
                    choice($"{learner.label} {TooltipText.Muted(learner.kind == LearnerKind.Legend ? "(a Legend)" : "(a community)")}", learner.why, learner.kind == LearnerKind.Legend ? "A Legend who learns it can teach it on, wherever the Legend goes." : "Its people keep it among themselves; no Legend is needed.", () =>
                    {
                        _draft.learnerKind = learner.kind; _draft.learner = learner.id; _draft.learnerLabel = learner.kind == LearnerKind.Community ? null : learner.label;
                        _stage = _draft.mode == TeachingMode.Adapt && _draft.food ? Stage.Dish : Stage.Place;
                    });
                }
                return;
            case Stage.Dish:
                header($"The adapted form's dish: keep {(_draft.recipe.IsKnown ? culture.MemoryTargetLabel(_draft.recipe) : "its dish")}, or make it with another the kitchen already knows (your own recipes too). Teaching never unlocks a dish or an ingredient.");
                choice("< Back", null, "Choose the learner again.", () => _stage = Stage.Learner);
                foreach (var r in culture.RecipeOptions())
                {
                    var recipe = r;
                    choice($"{recipe.recipe.Display}{(recipe.recipe.Same(_draft.recipe) ? TooltipText.Muted(" (its own dish)") : string.Empty)}", recipe.why, "The variant is made with this dish; its parent keeps its own.", () =>
                    {
                        _draft.recipe = recipe.recipe.Copy();
                        _stage = Stage.Place;
                    });
                }
                return;
            case Stage.Place:
                header($"Where is {_draft.traditionName} taught? Each hearth seats {culture.TransmissionTuning.HearthSpec.seats}; institutions seat more and teach harder practices.");
                choice("< Back", null, "Go back a step.", () => _stage = _draft.mode == TeachingMode.Record ? Stage.Teacher : _draft.mode == TeachingMode.Adapt && _draft.food ? Stage.Dish : Stage.Learner);
                foreach (var p in culture.PlaceOptions(_draft).OrderBy(p => p.why == null ? 0 : 1))
                {
                    var place = p;
                    var (taken, seats) = culture.Seats(place.institution, place.settlement);
                    choice($"{place.label} {TooltipText.Muted($"({taken} of {seats} seats taken)")}", place.why, "Teaching takes a seat here until it is completed or cancelled (a paused teaching keeps its seat).", () =>
                    {
                        _draft.institution = place.institution; _draft.settlement = place.settlement; _draft.settlementName = place.settlementName;
                        _stage = Stage.Confirm;
                    });
                }
                return;
            case Stage.Confirm:
                var preview = culture.PreviewTeaching(_draft);
                header(preview.succeeded ? preview.reason : TooltipText.Bad(preview.reason));
                choice("< Back", null, "Choose the place again.", () => _stage = Stage.Place);
                choice("Begin the teaching", preview.succeeded ? null : preview.reason, "It takes its seat now and advances each Seventh while teacher, learner and place are there.", () =>
                {
                    if (culture.StartTeaching(_draft).succeeded) _draft = null;
                });
                return;
        }
    }

    private static void Pick(CultureSystem culture, TeachingMode mode, string variant)
    {
        _draft = culture.DraftTeaching(_draft.tradition, mode, variant);
        _stage = Stage.Teacher;
    }

    // ===== AN ORDER =====

    private static void Order(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        var o = culture.TeachingOrder(_order);
        if (o == null) { _order = null; Overview(culture, header, choice); return; }
        header($"{o.traditionName}: {Who(o)}. {Capital(TransmissionRules.StatusWord(o))}.");
        choice("< All teaching", null, "Back to the teachings and traditions.", () => _order = null);
        if (!o.Open) return;
        choice("Cancel this teaching", null, "Its seat frees at once; what it would have made is not made. Its history stays.", () => { culture.CancelTeaching(o.id); _order = null; });
        foreach (var t in culture.ReassignOptions(o.id))
        {
            var teacher = t;
            choice($"{teacher.label} takes over {TooltipText.Muted($"({KindWord(teacher.kind)}; progress kept)")}", teacher.why, TeacherTip(teacher.kind), () => culture.ReassignTeaching(o.id, teacher.kind, teacher.id));
        }
    }

    // ===== INSTITUTIONS =====

    private static void Institutions(CultureSystem culture, Action<string> header, Action<string, string, string, Action> choice)
    {
        header($"Institutions meet at landmarks within their Ages (Civic.md): more seats, harder practices, and written records. Every settlement's hearth teaches without one. Unity held: {culture.UnityHeld:0}.");
        choice("< All teaching", null, "Back to the teachings and traditions.", () => _institutions = false);
        foreach (var i in culture.TeachingInstitutions())
        {
            var spec = culture.TransmissionTuning.Institution(i.spec);
            if (!string.IsNullOrEmpty(i.evolvedInto)) { choice(TooltipText.Muted($"{i.name} (grew into another institution)"), null, "Its records are kept.", () => { }); continue; }
            var (taken, seats) = culture.Seats(i.id, i.settlement);
            int records = culture.WrittenRecords().Count(r => r.institution == i.id);
            choice($"{i.name} {TooltipText.Muted($"({taken} of {seats} seats; {records} record{(records == 1 ? "" : "s")})")}", null, spec != null ? SpecTip(spec) : null, () => { });
        }
        foreach (var o in culture.InstitutionOptions())
        {
            var option = o;
            choice($"Found the {option.spec.name} in {option.landmark.name}, {option.settlementName} {TooltipText.Muted($"({(option.spec.foundingUnity > 0f ? $"{option.spec.foundingUnity:0} Unity" : "free")})")}", option.why, SpecTip(option.spec),
                () => culture.FoundInstitution(option.spec.id, CultureIds.Landmark(option.landmark.settlement, option.landmark.spec)));
        }
        foreach (var spec in culture.TransmissionTuning.institutions.Where(s => s != null && !s.informal && !s.OpenIn(GameAge.Number)))
            choice(TooltipText.Muted($"{spec.name}: {spec.AgeText}"), null, SpecTip(spec), () => { });
    }

    // ===== THE WINDOW'S BODY =====

    /// <summary>The Teaching section of the window's body (nothing before any teaching or institution).</summary>
    public static void Describe(CultureSystem culture, StringBuilder text)
    {
        if (culture == null) return;
        var orders = culture.TeachingOrders();
        var institutions = culture.TeachingInstitutions().Where(i => string.IsNullOrEmpty(i.evolvedInto)).ToList();
        var bearers = culture.TaughtBearers();
        var variants = culture.PracticeVariants();
        var records = culture.WrittenRecords();
        if (orders.Count == 0 && institutions.Count == 0) return;
        text.AppendLine(TooltipText.Heading("Teaching", $"{orders.Count(o => o.Open)} under way"));
        foreach (var o in orders.Where(o => o.Open)) text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(o.traditionName)}: {Who(o)} ({TransmissionRules.StatusWord(o)})"));
        if (institutions.Count > 0) text.AppendLine(TooltipText.Row("Institutions", string.Join(", ", institutions.Select(i => i.name))));
        if (bearers.Count > 0) text.AppendLine(TooltipText.Row("Taught", string.Join("; ", bearers.Select(b => $"{b.label} ({culture.Tradition(b.tradition)?.name ?? b.tradition}{(string.IsNullOrEmpty(b.variant) ? string.Empty : ", a local form")})"))));
        if (variants.Count > 0) text.AppendLine(TooltipText.Row("Local forms", string.Join("; ", variants.Select(v => $"{v.name} (from {v.parentName})"))));
        if (records.Count > 0) text.AppendLine(TooltipText.Row("Written", string.Join("; ", records.Select(r => $"{r.traditionName} at {r.institutionName}"))));
        text.AppendLine(TooltipText.Muted("A record lets a practice be taught again when no one living keeps it; it is not a practitioner. A teacher who leaves on an expedition, or is lost, pauses the teaching: it keeps its progress."));
        text.AppendLine();
    }

    // ===== WORDS =====

    private static string Who(TeachingOrder o) =>
        o.mode == TeachingMode.Record ? $"written from {o.teacherLabel} at {Place(o)}" : $"{o.teacherLabel} to {(o.learnerKind == LearnerKind.Community ? "the people of " + o.learnerLabel : o.learnerLabel)} at {Place(o)}";

    private static string Place(TeachingOrder o) => string.IsNullOrEmpty(o.institution) ? $"the hearth of {o.settlementName}" : CultureSystem.Instance?.TeachingInstitutions().FirstOrDefault(i => i.id == o.institution)?.name ?? "an institution";

    private static string CarriedText(TraditionCarriers c)
    {
        var parts = new List<string>();
        if (c.lived) parts.Add("practised");
        int legends = c.livingLegends.Count + c.awayLegends.Count;
        if (legends > 0) parts.Add($"{legends} Legend{(legends == 1 ? "" : "s")}");
        if (c.communities.Count > 0) parts.Add($"{c.communities.Count} communit{(c.communities.Count == 1 ? "y" : "ies")}");
        if (c.variants.Count > 0) parts.Add($"{c.variants.Count} local form{(c.variants.Count == 1 ? "" : "s")}");
        if (c.records.Count > 0) parts.Add($"{c.records.Count} record{(c.records.Count == 1 ? "" : "s")}");
        return parts.Count == 0 ? "no one keeps it" : string.Join(", ", parts);
    }

    private static string CarriersTip(CultureSystem culture, TraditionView view, TraditionCarriers c)
    {
        var s = new StringBuilder(view.description ?? string.Empty);
        s.Append('\n').Append(TooltipText.Row("Kept by", c.livingLegends.Count + c.communities.Count == 0 ? "no one here now" : string.Join(", ", c.livingLegends.Concat(c.communities.Select(x => "the people of " + x.name)))));
        if (c.awayLegends.Count > 0) s.Append('\n').Append(TooltipText.Row("Away", string.Join(", ", c.awayLegends.Select(a => a.why))));
        if (c.lostLegends.Count > 0) s.Append('\n').Append(TooltipText.Row("Lost", string.Join(", ", c.lostLegends)));
        if (c.records.Count > 0) s.Append('\n').Append(TooltipText.Row("Written (not practised)", string.Join(", ", c.records.Select(r => r.institutionName))));
        if (c.variants.Count > 0) s.Append('\n').Append(TooltipText.Row("Local forms", string.Join(", ", c.variants.Select(v => v.name))));
        s.Append('\n').Append(TooltipText.Row("Complexity", culture.TransmissionTuning.ComplexityOf(view.definition, view.food).ToString()));
        return s.ToString();
    }

    private static string OrderTip(CultureSystem culture, TeachingOrder o)
    {
        var s = new StringBuilder($"{Capital(TransmissionRules.ModeWord(o.mode))}: {Who(o)}.");
        s.Append('\n').Append(TooltipText.Row("Progress", TransmissionRules.StatusWord(o)));
        s.Append('\n').Append(TooltipText.Row("Complexity", $"{o.complexity}{(o.recovery ? " (no one living keeps it: slower)" : string.Empty)}"));
        if (o.recipe != null && o.recipe.IsKnown) s.Append('\n').Append(TooltipText.Row("Dish", culture.MemoryTargetLabel(o.recipe)));
        s.Append('\n').Append(TooltipText.Row("Begun", $"Seventh {o.startedSeventh}"));
        return s.ToString();
    }

    private static string SpecTip(InstitutionSpec spec)
    {
        var s = new StringBuilder(spec.description ?? string.Empty);
        s.Append('\n').Append(TooltipText.Row("Teaches", spec.food && spec.other ? "every practice" : spec.food ? "food practices" : "songs, rites and tales"));
        s.Append('\n').Append(TooltipText.Row("Seats", $"{spec.seats}, up to complexity {spec.maxComplexity}{(spec.keepsRecords ? "; keeps written records" : string.Empty)}"));
        s.Append('\n').Append(TooltipText.Row("Ages", spec.AgeText));
        s.Append('\n').Append(TooltipText.Muted($"{spec.CanonText}: {spec.canonNote}"));
        return s.ToString();
    }

    private static string KindWord(TeacherKind kind) => kind == TeacherKind.Legend ? "a Legend" : kind == TeacherKind.Community ? "the community" : "a written record";

    private static string TeacherTip(TeacherKind kind) =>
        kind == TeacherKind.Legend ? "A Legend who carries it. Away on an expedition or lost, the teaching pauses." :
        kind == TeacherKind.Community ? "Its people teach it where they live; no Legend is needed for a village custom." :
        "Taught from what was written: slower, and never the most complex practices. It lets a practice no one keeps be taught again.";

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
