using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Apprenticeships and cultural institutions (T06; rules in <see cref="TransmissionRules"/>, saved in
/// <see cref="CultureExtensionState.transmission"/>). "Our traditions survive because someone learned to carry them."
///
/// - A teaching order: a tradition (T01) is taught by a Legend who carries it, by the community that keeps it (a village
///   custom needs no Legend), or from a written record when no one living keeps it; to a Legend or a settlement's people;
///   at a settlement's hearth or at an institution. Preserve makes a new bearer; adapt makes a documented local variant
///   that keeps its parent; an institution that keeps records can write it down instead.
/// - It takes Sevenths (more for complex practices, more again from a record) and a seat at its place: the hearth seats
///   one, institutions more. A teacher or learner who goes on an expedition, is lost, or whose settlement falls pauses the
///   order (its progress and seat kept); it resumes by itself, or another teacher takes over.
/// - Institutions are founded at standing landmarks within their Ages (Civic.md): a Flavor Log (Ages I-III), a Guild of
///   Ballads and Plays (II-IV), a Cooking Guild (IV-VI; a Flavor Log in the same hall grows into it).
/// - Nothing here grants a technology, an ingredient or a recipe: a food practice is only taught with a dish the kitchen
///   can already make. Ordinary practice never needs schooling.
/// </summary>
public partial class CultureSystem
{
    private readonly TransmissionTuning _transmissionDefaults = new TransmissionTuning();

    public TransmissionTuning TransmissionTuning => _transmissionDefaults;

    private TransmissionState Transmission
    {
        get
        {
            var x = Extensions;
            if (x.transmission == null) x.transmission = new TransmissionState();
            return x.transmission;
        }
    }

    // ===== READS (snapshots) =====

    public IReadOnlyList<TeachingOrder> TeachingOrders() => Transmission.orders.Where(o => o != null).Select(o => o.Copy()).ToList();
    public TeachingOrder TeachingOrder(string id) => Transmission.Order(id)?.Copy();
    public IReadOnlyList<InstitutionRecord> TeachingInstitutions() => Transmission.institutions.Where(i => i != null).Select(i => i.Copy()).ToList();
    public IReadOnlyList<PracticeVariant> PracticeVariants() => Transmission.variants.Where(v => v != null).Select(v => v.Copy()).ToList();
    public IReadOnlyList<WrittenRecord> WrittenRecords() => Transmission.records.Where(r => r != null).Select(r => r.Copy()).ToList();
    public IReadOnlyList<TaughtBearer> TaughtBearers() => Transmission.bearers.Where(b => b != null).Select(b => b.Copy()).ToList();

    /// <summary>
    /// Who carries a tradition now, told apart from what is only written: its living Legends (T01's bearers and those
    /// taught), the communities that keep it, its variants and its records.
    /// </summary>
    public TraditionCarriers CarriersOf(string tradition)
    {
        var i = TraditionRules.Get(Extensions.traditions, tradition);
        var c = new TraditionCarriers { tradition = tradition, lived = i != null && i.Lived };
        if (i == null) return c;
        foreach (string legend in LegendBearers(i, null))
        {
            string why = WhyLegendUnavailable(legend);
            if (why == null) c.livingLegends.Add(legend);
            else if (LegendProgress.Instance != null && LegendProgress.Instance.IsLost(legend)) c.lostLegends.Add(legend);
            else c.awayLegends.Add((legend, why));
        }
        foreach (var (settlement, name) in CommunityBearers(i, null))
        {
            if (Stands(settlement, name) != null) c.communities.Add((settlement, name));
            else c.formerCommunities.Add((settlement, name));
        }
        c.variants.AddRange(TransmissionRules.VariantsOf(Transmission, tradition).Select(v => v.Copy()));
        c.records.AddRange(TransmissionRules.RecordsOf(Transmission, tradition).Select(r => r.Copy()));
        return c;
    }

    /// <summary>The seats of a place (a hearth or an institution) and how many are taken.</summary>
    public (int taken, int seats) Seats(string institution, int settlement)
    {
        var spec = PlaceSpec(institution);
        return (TransmissionRules.Occupied(Transmission, TransmissionRules.PlaceKey(institution, settlement)), spec != null ? Math.Max(0, spec.seats) : 0);
    }

    // ===== THE WORLD, AS TEACHING SEES IT =====

    // A settlement standing now, with the name it had (a later settlement given the same id is not it).
    private Settlement Stands(int id, string name)
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map == null || id < 0) return null;
        var s = map.Settlements.FirstOrDefault(x => x != null && x.id == id);
        return s != null && (string.IsNullOrEmpty(name) || string.Equals(s.name, name, StringComparison.Ordinal)) ? s : null;
    }

    private Settlement CapitalSettlement()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        return map != null ? WorldCivilization.Capital(map) : null;
    }

    /// <summary>Why <paramref name="legend"/> cannot teach or learn now (lost, not met, away on an expedition), or null.</summary>
    public string WhyLegendUnavailable(string legend)
    {
        var legends = LegendProgress.Instance;
        if (string.IsNullOrEmpty(legend)) return "No one.";
        if (legends == null) return $"{legend} is not here.";
        if (legends.IsLost(legend)) return $"{legend} is lost to Dissonance.";
        if (!legends.IsRecruited(legend)) return $"{legend} has not been met.";
        var away = WorldSystem.Instance != null ? WorldSystem.Instance.ExpeditionOf(legend) : null;
        if (away != null) return $"{legend} is away with {away.name}.";
        return null;
    }

    // The tradition's home community: its settlement (the Capital for the nation's own); none when its place fell.
    private (int id, string name) HomeOf(TraditionInstance i)
    {
        if (i == null || i.fallen || i.settlement == TraditionRules.FallenPlace) return (-1, null);
        if (i.settlement >= 0) { var s = Stands(i.settlement, null); return s != null ? (s.id, s.name) : (-1, null); }
        var capital = CapitalSettlement();
        return capital != null ? (capital.id, capital.name) : (-1, null);
    }

    // Legends who carry it: T01's bearers (who took part) and those taught it (for the tradition itself, or a variant).
    private IEnumerable<string> LegendBearers(TraditionInstance i, string variant)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(variant)) names.AddRange(i.bearers.Where(b => !string.IsNullOrEmpty(b)));
        names.AddRange(Transmission.bearers.Where(b => b.tradition == i.id && b.kind == LearnerKind.Legend && string.Equals(b.variant ?? string.Empty, variant ?? string.Empty, StringComparison.Ordinal)).Select(b => b.who));
        return names.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // Communities that carry it: its home while it is lived there, and those taught it (a variant: its own settlement).
    private IEnumerable<(int id, string name)> CommunityBearers(TraditionInstance i, string variant)
    {
        var list = new List<(int, string)>();
        if (string.IsNullOrEmpty(variant))
        {
            var home = HomeOf(i);
            if (home.id >= 0 && i.Lived) list.Add(home);
        }
        else
        {
            var v = Transmission.Variant(variant);
            if (v != null && v.settlement >= 0) list.Add((v.settlement, v.settlementName));
        }
        foreach (var b in Transmission.bearers.Where(b => b.tradition == i.id && b.kind == LearnerKind.Community && string.Equals(b.variant ?? string.Empty, variant ?? string.Empty, StringComparison.Ordinal)))
            if (int.TryParse(b.who, out int id)) list.Add((id, b.label));
        return list.Distinct();
    }

    private InstitutionSpec PlaceSpec(string institution)
    {
        if (string.IsNullOrEmpty(institution)) return TransmissionTuning.HearthSpec;
        var record = Transmission.Institution(institution);
        return record != null ? TransmissionTuning.Institution(record.spec) : null;
    }

    private string PlaceName(string institution, int settlement, string settlementName)
    {
        if (!string.IsNullOrEmpty(institution)) return Transmission.Institution(institution)?.name ?? "an institution now gone";
        return $"the hearth of {settlementName ?? SettlementName(settlement) ?? "a settlement"}";
    }

    // Why the place cannot hold teaching now (fallen, landmark gone, grown into another), or null.
    private string WhyPlaceClosed(string institution, int settlement, string settlementName)
    {
        if (string.IsNullOrEmpty(institution))
            return Stands(settlement, settlementName) != null ? null : $"{settlementName ?? "Its settlement"} no longer stands.";
        var record = Transmission.Institution(institution);
        if (record == null) return "Its institution is gone.";
        if (!string.IsNullOrEmpty(record.evolvedInto)) return $"{record.name} grew into {Transmission.Institution(record.evolvedInto)?.name ?? "another institution"}.";
        if (Stands(record.settlement, record.settlementName) == null) return $"{record.name} stood in {record.settlementName}, which fell.";
        if (LandmarkOf(record.landmark) == null) return $"The landmark {record.name} met in is gone.";
        return null;
    }

    /// <summary>Why the dish <paramref name="recipe"/> cannot be taught now (unknown, not researched, not yet registered), or null. Teaching never unlocks it.</summary>
    public string WhyNotRecipe(CultureEntityRef recipe)
    {
        if (recipe == null || !recipe.IsKnown) return null;
        var spec = Life.Recipe(recipe.id);
        if (spec == null) return $"{recipe.Display} is not a dish the kitchen knows.";
        if (!string.IsNullOrEmpty(spec.technology) && !Researched(spec.technology)) return $"{spec.dish} needs {spec.technology}: teaching does not unlock it.";
        // Authored or registered at runtime (an invented dish has no store slot until its first batch).
        if (!GameCatalog.IsResource(spec.dish)) return $"{spec.dish} is not known to the stores yet.";
        return null;
    }

    /// <summary>The dish a food tradition is made with (its trigger's recipe, or a national food's recipe), or none.</summary>
    public CultureEntityRef RecipeOf(TraditionInstance i)
    {
        var d = i != null ? TraditionTuning.Definition(i.definition) : null;
        if (d == null || !d.food) return new CultureEntityRef();
        if (i.subject != null && i.subject.kind == CultureEntityKind.Recipe && i.subject.IsKnown) return i.subject.Copy();
        if (i.subject != null && i.subject.kind == CultureEntityKind.Resource && i.subject.IsKnown)
        {
            var invented = InventedFood(i.subject.id);
            if (invented != null) return CultureEntityRef.Of(CultureEntityKind.Recipe, invented.id, invented.name);
            var authored = Life.recipes.FirstOrDefault(r => r != null && string.Equals(r.dish, i.subject.id, StringComparison.OrdinalIgnoreCase));
            return authored != null ? CultureEntityRef.Of(CultureEntityKind.Recipe, authored.id, authored.dish) : new CultureEntityRef();
        }
        var trigger = d.triggers?.FirstOrDefault(t => t != null && t.subjectKind == CultureEntityKind.Recipe && !string.IsNullOrEmpty(t.subjectId));
        var r0 = trigger != null ? Life.Recipe(trigger.subjectId) : null;
        return r0 != null ? CultureEntityRef.Of(CultureEntityKind.Recipe, r0.id, r0.dish) : new CultureEntityRef();
    }

    // ===== WHAT CAN BE TAUGHT, BY WHOM, TO WHOM, WHERE =====

    /// <summary>Traditions that can be taught: every custom the people made (lived, dormant or fallen with its place). An emerging practice is not a custom yet.</summary>
    public IReadOnlyList<TraditionView> TeachableTraditions() => Traditions().Where(t => t.established).ToList();

    /// <summary>A draft for teaching <paramref name="tradition"/> (or its <paramref name="variant"/>) in <paramref name="mode"/>, with its dish and whether it is a recovery.</summary>
    public TeachingDraft DraftTeaching(string tradition, TeachingMode mode, string variant = null)
    {
        var i = TraditionRules.Get(Extensions.traditions, tradition);
        var d = i != null ? TraditionTuning.Definition(i.definition) : null;
        var v = !string.IsNullOrEmpty(variant) ? Transmission.Variant(variant) : null;
        var draft = new TeachingDraft
        {
            tradition = tradition, definition = i?.definition, food = d != null && d.food, mode = mode, variant = v?.id ?? string.Empty,
            traditionName = v != null ? v.name : Tradition(tradition)?.name ?? d?.name ?? tradition,
            recipe = v != null && v.recipe.IsKnown ? v.recipe.Copy() : RecipeOf(i),
        };
        draft.recovery = i != null && (v == null ? !i.Lived && !LegendBearers(i, null).Any(l => WhyLegendUnavailable(l) == null) : false);
        return draft;
    }

    /// <summary>Who could teach the draft's tradition: its Legends, its communities, and (unless writing a record) its records. Each with why not, or null.</summary>
    public List<(TeacherKind kind, string id, string label, string why)> TeacherOptions(TeachingDraft draft)
    {
        var list = new List<(TeacherKind, string, string, string)>();
        var i = draft != null ? TraditionRules.Get(Extensions.traditions, draft.tradition) : null;
        if (i == null) return list;
        foreach (string legend in LegendBearers(i, draft.variant)) list.Add((TeacherKind.Legend, legend, legend, WhyLegendUnavailable(legend)));
        foreach (var (id, name) in CommunityBearers(i, draft.variant))
            list.Add((TeacherKind.Community, CultureIds.Settlement(id), $"the people of {name}", Stands(id, name) != null ? null : $"{name} no longer stands."));
        if (draft.mode != TeachingMode.Record)
            foreach (var r in TransmissionRules.RecordsOf(Transmission, i.id).Where(r => string.Equals(r.variant ?? string.Empty, draft.variant ?? string.Empty, StringComparison.Ordinal)))
                list.Add((TeacherKind.Record, r.id, $"the record written at {r.institutionName ?? "an institution"}", null));
        return list;
    }

    /// <summary>Who could learn it: Legends met, and the people of each standing settlement. Each with why not, or null.</summary>
    public List<(LearnerKind kind, string id, string label, string why)> LearnerOptions(TeachingDraft draft)
    {
        var list = new List<(LearnerKind, string, string, string)>();
        if (draft == null || draft.mode == TeachingMode.Record) return list;
        var i = TraditionRules.Get(Extensions.traditions, draft.tradition);
        var legends = LegendProgress.Instance;
        if (legends != null)
            foreach (string name in legends.RecruitedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                string why = WhyLegendUnavailable(name);
                if (why == null && draft.mode == TeachingMode.Preserve && i != null && LegendBearers(i, draft.variant).Any(b => string.Equals(b, name, StringComparison.OrdinalIgnoreCase)))
                    why = $"{name} already carries it.";
                list.Add((LearnerKind.Legend, name, name, why));
            }
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null)
            foreach (var s in map.Settlements.Where(s => s != null).OrderBy(s => s.id))
            {
                string why = null;
                if (draft.mode == TeachingMode.Preserve && i != null && CommunityBearers(i, draft.variant).Any(c => c.id == s.id && c.name == s.name)) why = $"{s.name} keeps it already.";
                list.Add((LearnerKind.Community, CultureIds.Settlement(s.id), $"the people of {s.name}", why));
            }
        return list;
    }

    /// <summary>Where it could be taught: the hearths of standing settlements and the open institutions, each with its seats and why not.</summary>
    public List<(string institution, int settlement, string settlementName, string label, string why)> PlaceOptions(TeachingDraft draft)
    {
        var list = new List<(string, int, string, string, string)>();
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (draft == null || map == null) return list;
        foreach (var s in map.Settlements.Where(s => s != null).OrderBy(s => s.id))
        {
            var d = draft.Copy();
            d.institution = string.Empty; d.settlement = s.id; d.settlementName = s.name;
            list.Add((string.Empty, s.id, s.name, PlaceName(null, s.id, s.name), WhyNotStartTeaching(d)));
        }
        foreach (var inst in Transmission.institutions.Where(x => x != null && string.IsNullOrEmpty(x.evolvedInto)))
        {
            var d = draft.Copy();
            d.institution = inst.id; d.settlement = inst.settlement; d.settlementName = inst.settlementName;
            list.Add((inst.id, inst.settlement, inst.settlementName, inst.name, WhyNotStartTeaching(d)));
        }
        return list;
    }

    /// <summary>Dishes an adapted food practice could be made with: those the kitchen can make now (authored or invented).</summary>
    public List<(CultureEntityRef recipe, string why)> RecipeOptions() =>
        Life.recipes.Where(r => r != null && !r.InCellar).Select(r => CultureEntityRef.Of(CultureEntityKind.Recipe, r.id, r.dish)).Select(r => (r, WhyNotRecipe(r))).ToList();

    // ===== COMMANDS =====

    /// <summary>Why <paramref name="draft"/> cannot begin now (the world's reasons first, then the rules'), or null.</summary>
    public string WhyNotStartTeaching(TeachingDraft draft)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        if (draft == null) return "Nothing to teach.";
        var i = TraditionRules.Get(Extensions.traditions, draft.tradition);
        if (i == null) return "No such tradition.";
        if (!i.Established) return "It is not a custom yet: what is only emerging cannot be taught.";
        if (!string.IsNullOrEmpty(draft.variant) && Transmission.Variant(draft.variant) == null) return "No such variant.";
        var teacher = TeacherOptions(draft).FirstOrDefault(t => t.kind == draft.teacherKind && string.Equals(t.id, draft.teacher, StringComparison.OrdinalIgnoreCase));
        if (teacher.id == null) return string.IsNullOrEmpty(draft.teacher) ? "Choose who teaches it." : $"{draft.teacherLabel ?? draft.teacher} does not carry it.";
        if (teacher.why != null) return teacher.why;
        if (draft.mode != TeachingMode.Record)
        {
            if (draft.learnerKind == LearnerKind.Legend && WhyLegendUnavailable(draft.learner) is string away) return away;
            if (draft.learnerKind == LearnerKind.Community && (!int.TryParse(draft.learner, out int c) || Stands(c, null) == null)) return "That settlement no longer stands.";
        }
        string closed = WhyPlaceClosed(draft.institution, draft.settlement, draft.settlementName);
        if (closed != null) return closed;
        // Its people teach at home; a community learns at home unless someone brings the teaching to an institution.
        if (draft.teacherKind == TeacherKind.Community && int.TryParse(draft.teacher, out int from) && from != draft.settlement)
            return $"{teacher.label} teach where they live ({SettlementName(from) ?? "their settlement"}).";
        if (draft.teacherKind != TeacherKind.Community && draft.learnerKind == LearnerKind.Community && string.IsNullOrEmpty(draft.institution) && int.TryParse(draft.learner, out int at) && at != draft.settlement)
            return $"The people of {SettlementName(at)} learn at their own hearth, or at an institution.";
        string recipe = WhyNotRecipe(draft.recipe);
        if (recipe != null) return recipe;
        var place = PlaceSpec(draft.institution);
        return TransmissionRules.WhyNotStart(Transmission, TransmissionTuning, Resolve(draft), place, PlaceName(draft.institution, draft.settlement, draft.settlementName));
    }

    // The draft with its labels filled in and whether it is a recovery.
    private TeachingDraft Resolve(TeachingDraft draft)
    {
        var d = draft.Copy();
        if (string.IsNullOrEmpty(d.teacherLabel)) d.teacherLabel = d.teacherKind == TeacherKind.Community && int.TryParse(d.teacher, out int t) ? $"the people of {SettlementName(t)}" : d.teacher;
        // A community is saved by its settlement's name (so a later settlement with the same id is never taken for it).
        if (d.learnerKind == LearnerKind.Community && int.TryParse(d.learner, out int l)) d.learnerLabel = SettlementName(l) ?? d.learnerLabel;
        if (string.IsNullOrEmpty(d.learnerLabel)) d.learnerLabel = d.learner;
        if (string.IsNullOrEmpty(d.settlementName)) d.settlementName = SettlementName(d.settlement);
        if (d.recipe == null) d.recipe = new CultureEntityRef();
        return d;
    }

    /// <summary>What beginning <paramref name="draft"/> would do (nothing is changed): its Sevenths, its seat, what it makes.</summary>
    public CultureCommandResult PreviewTeaching(TeachingDraft draft)
    {
        string why = WhyNotStartTeaching(draft);
        if (why != null) return CultureCommandResult.Fail(why);
        var d = Resolve(draft);
        int complexity = TransmissionRules.Complexity(TransmissionTuning, d.definition, d.food, d.mode);
        bool recovery = d.recovery || d.teacherKind == TeacherKind.Record;
        int sevenths = TransmissionRules.Required(TransmissionTuning, complexity, recovery);
        var (taken, seats) = Seats(d.institution, d.settlement);
        string place = PlaceName(d.institution, d.settlement, d.settlementName);
        string makes = d.mode == TeachingMode.Preserve ? $"{Learner(d.learnerKind, d.learnerLabel)} will carry {d.traditionName}"
            : d.mode == TeachingMode.Adapt ? $"a documented local form, {VariantNameFor(d)}, kept by {Learner(d.learnerKind, d.learnerLabel)} (its parent stays {d.traditionName})"
            : $"{d.traditionName} written down at {place} (a record helps it be taught again; it keeps no one practising it)";
        return CultureCommandResult.Ok($"{d.teacherLabel} {(d.mode == TeachingMode.Record ? "tells" : "teaches")} {d.traditionName}{(d.mode == TeachingMode.Record ? string.Empty : " to " + Learner(d.learnerKind, d.learnerLabel))} at {place}: complexity {complexity}, {sevenths} Sevenths{(recovery ? " (no one living keeps it: slower)" : string.Empty)}, one of {seats} seat{(seats == 1 ? "" : "s")} ({taken} taken). Then {makes}. Nothing is spent.");
    }

    private static string Learner(LearnerKind kind, string label) => kind == LearnerKind.Community ? $"the people of {label}" : label;

    private string VariantNameFor(TeachingDraft d)
    {
        string where = d.learnerKind == LearnerKind.Community && int.TryParse(d.learner, out int s) ? SettlementName(s) : d.settlementName;
        var parentRecipe = RecipeOf(TraditionRules.Get(Extensions.traditions, d.tradition));
        return TransmissionRules.VariantName(d.traditionName, where, d.recipe != null && d.recipe.IsKnown ? MemoryTargetLabel(d.recipe) : null, parentRecipe.IsKnown ? MemoryTargetLabel(parentRecipe) : null);
    }

    /// <summary>Begin a teaching order. It takes a seat at once and advances each Seventh while all it needs is there.</summary>
    public CultureCommandResult StartTeaching(TeachingDraft draft)
    {
        var preview = PreviewTeaching(draft);
        if (!preview.succeeded) { GameLog.Event("Teaching refused: " + preview.reason, Log); return preview; }
        var o = TransmissionRules.Start(Transmission, TransmissionTuning, Resolve(draft), _state.sevenths, CurrentAgeId);
        GameLog.Event($"Teaching begins ({o.id}): {preview.reason}", Log);
        RaiseChanged();
        return CultureCommandResult.Ok(preview.reason);
    }

    /// <summary>Stop an order: its seat frees at once; nothing it would have made is made.</summary>
    public CultureCommandResult CancelTeaching(string order)
    {
        var o = Transmission.Order(order);
        if (o == null) return CultureCommandResult.Fail("No such teaching.");
        if (!TransmissionRules.Cancel(o, _state.sevenths)) return CultureCommandResult.Fail("It is finished already.");
        GameLog.Event($"Teaching cancelled ({o.id}): {o.traditionName}.", Log);
        RaiseChanged();
        return CultureCommandResult.Ok($"The teaching of {o.traditionName} stops; its seat is free.");
    }

    /// <summary>Why another teacher cannot take over <paramref name="order"/>, or null.</summary>
    public string WhyNotReassign(string order, TeacherKind kind, string teacher)
    {
        var o = Transmission.Order(order);
        if (o == null || !o.Open) return "No open teaching.";
        if (kind == o.teacherKind && string.Equals(teacher, o.teacher, StringComparison.OrdinalIgnoreCase)) return "They teach it already.";
        var draft = DraftOf(o);
        var option = TeacherOptions(draft).FirstOrDefault(t => t.kind == kind && string.Equals(t.id, teacher, StringComparison.OrdinalIgnoreCase));
        if (option.id == null) return "They do not carry it.";
        if (option.why != null) return option.why;
        if (kind == TeacherKind.Record && o.mode == TeachingMode.Record) return "A record is written from someone who keeps the practice.";
        if (kind == TeacherKind.Record && o.complexity >= TransmissionTuning.livingTeacherFrom) return "Too complex to learn from a record alone.";
        if (kind == TeacherKind.Community && int.TryParse(teacher, out int from) && from != o.settlement) return "Its people teach where they live.";
        if (kind == TeacherKind.Legend && Transmission.orders.Any(x => x != o && x.Open && (x.teacherKind == TeacherKind.Legend && string.Equals(x.teacher, teacher, StringComparison.OrdinalIgnoreCase) || x.learnerKind == LearnerKind.Legend && string.Equals(x.learner, teacher, StringComparison.OrdinalIgnoreCase))))
            return $"{teacher} is busy with another teaching.";
        return null;
    }

    /// <summary>Who could take over <paramref name="order"/> (each with why not, or null).</summary>
    public List<(TeacherKind kind, string id, string label, string why)> ReassignOptions(string order)
    {
        var o = Transmission.Order(order);
        if (o == null || !o.Open) return new List<(TeacherKind, string, string, string)>();
        return TeacherOptions(DraftOf(o)).Where(t => !(t.kind == o.teacherKind && string.Equals(t.id, o.teacher, StringComparison.OrdinalIgnoreCase)))
            .Select(t => (t.kind, t.id, t.label, WhyNotReassign(order, t.kind, t.id))).ToList();
    }

    /// <summary>Another teacher takes over an open order (its progress is kept).</summary>
    public CultureCommandResult ReassignTeaching(string order, TeacherKind kind, string teacher)
    {
        string why = WhyNotReassign(order, kind, teacher);
        if (why != null) return CultureCommandResult.Fail(why);
        var o = Transmission.Order(order);
        string label = TeacherOptions(DraftOf(o)).First(t => t.kind == kind && string.Equals(t.id, teacher, StringComparison.OrdinalIgnoreCase)).label;
        TransmissionRules.Reassign(o, kind, teacher, label, TransmissionTuning);
        GameLog.Event($"Teaching {o.id}: {label} takes over.", Log);
        RaiseChanged();
        return CultureCommandResult.Ok($"{label} takes over teaching {o.traditionName} ({o.progress} of {o.required} Sevenths kept).");
    }

    private TeachingDraft DraftOf(TeachingOrder o) => new TeachingDraft
    {
        tradition = o.tradition, traditionName = o.traditionName, definition = o.definition, variant = o.variant, mode = o.mode, teacherKind = o.teacherKind, teacher = o.teacher,
        teacherLabel = o.teacherLabel, learnerKind = o.learnerKind, learner = o.learner, learnerLabel = o.learnerLabel, institution = o.institution, settlement = o.settlement,
        settlementName = o.settlementName, recipe = o.recipe?.Copy() ?? new CultureEntityRef(), recovery = o.recovery,
        food = TraditionTuning.Definition(o.definition)?.food ?? false,
    };

    /// <summary>Institutions that could be founded now at a standing landmark, each with why not (Age, landmark, Unity) and its cost.</summary>
    public List<(InstitutionSpec spec, Landmark landmark, string settlementName, string why)> InstitutionOptions()
    {
        var list = new List<(InstitutionSpec, Landmark, string, string)>();
        foreach (var spec in TransmissionTuning.institutions.Where(x => x != null && !x.informal))
            foreach (var l in _state.landmarks.Where(l => l != null && spec.venues.Any(v => string.Equals(v, l.spec, StringComparison.OrdinalIgnoreCase))))
            {
                var s = Stands(l.settlement, null);
                if (s == null) continue;
                list.Add((spec, l, s.name, WhyNotFoundInstitution(spec.id, CultureIds.Landmark(l.settlement, l.spec))));
            }
        return list;
    }

    /// <summary>Why <paramref name="spec"/> cannot be founded at <paramref name="landmark"/> now, or null.</summary>
    public string WhyNotFoundInstitution(string spec, string landmark)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        var s = TransmissionTuning.Institution(spec);
        var l = LandmarkOf(landmark);
        if (l == null) return "No such landmark.";
        if (Stands(l.settlement, null) == null) return "Its settlement no longer stands.";
        string why = TransmissionRules.WhyNotFound(Transmission, s, l.spec, landmark, GameAge.Number);
        if (why != null) return why;
        return s.foundingUnity > 0f && !Has(UnityResource, s.foundingUnity) ? $"Needs {s.foundingUnity:0} Unity ({UnityHeld:0} held)." : null;
    }

    /// <summary>Found an institution at a landmark (its Unity paid now); an institution it grows out of there becomes it.</summary>
    public CultureCommandResult FoundInstitution(string spec, string landmark)
    {
        string why = WhyNotFoundInstitution(spec, landmark);
        if (why != null) return CultureCommandResult.Fail(why);
        var s = TransmissionTuning.Institution(spec);
        var l = LandmarkOf(landmark);
        var settlement = Stands(l.settlement, null);
        var result = CultureCommandResult.Ok();
        if (s.foundingUnity > 0f)
        {
            var cost = new List<ResourceAmount> { new ResourceAmount { resource = UnityResource, amount = s.foundingUnity } };
            Pay(cost);
            result.paid.AddRange(cost);
        }
        var record = TransmissionRules.Found(Transmission, s, landmark, settlement.id, settlement.name, $"{s.name} of {settlement.name}", _state.sevenths, CurrentAgeId);
        var from = Transmission.Institution(record.evolvedFrom);
        result.reason = from != null ? $"{from.name} grows into the {record.name}." : $"The {record.name} meets in {l.name}.";
        string key = $"institution:{record.id}";
        var o = Occurrence(key, CulturalOccurrenceKind.Venue, CultureEntityRef.Of(CultureEntityKind.Landmark, landmark, l.name), settlement.id, 1f, CultureQuantityUnit.Occasions, $"{record.name} founded in {l.name}");
        if (Record(o)) result.occurrences.Add(key);
        Remember("institution", record.name, from != null ? $"{from.name} grew into the {record.name}: what its cooks wrote down, the guild keeps." : $"{Capital(PeopleWord)} founded the {record.name} in {l.name}. {s.description}");
        GameLog.Event($"Institution founded: {record.name} ({record.id}, {result.PaidText}).", Log);
        NotificationFeed.Push(record.name, result.reason + $" It seats {s.seats} teaching{(s.seats == 1 ? "" : "s")}{(s.keepsRecords ? " and keeps written records" : string.Empty)}.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:institution:" + record.id);
        RaiseChanged();
        return result;
    }

    // ===== THE SEVENTH =====

    // Why an open order cannot advance this Seventh (teacher first, then learner, place, dish), or null.
    private string WhyPaused(TeachingOrder o)
    {
        var i = TraditionRules.Get(Extensions.traditions, o.tradition);
        if (i == null) return "Its tradition's record is gone.";
        switch (o.teacherKind)
        {
            case TeacherKind.Legend:
                if (WhyLegendUnavailable(o.teacher) is string legend) return legend;
                break;
            case TeacherKind.Community:
                if (!int.TryParse(o.teacher, out int c) || !CommunityBearers(i, o.variant).Any(b => b.id == c && Stands(b.id, b.name) != null))
                    return $"{o.teacherLabel} no longer keep it.";
                break;
            case TeacherKind.Record:
                if (Transmission.Record(o.teacher) == null) return "The record it was taught from is gone.";
                break;
        }
        if (o.learnerKind == LearnerKind.Legend && WhyLegendUnavailable(o.learner) is string learner) return learner;
        if (o.learnerKind == LearnerKind.Community && (!int.TryParse(o.learner, out int at) || Stands(at, o.learnerLabel) == null)) return $"{o.learnerLabel} no longer stands.";
        string closed = WhyPlaceClosed(o.institution, o.settlement, o.settlementName);
        if (closed != null) return closed;
        return WhyNotRecipe(o.recipe);
    }

    /// <summary>
    /// The teaching's Seventh (<see cref="TransmissionFeature"/>): each open order advances once when all it needs is
    /// there, or pauses and says why; an order with its Sevenths completes (once) and records a Teaching occurrence.
    /// </summary>
    internal void TransmissionSeventh(CultureSeventh context)
    {
        var state = Transmission;
        TransmissionRules.Ensure(state);
        int now = context?.Now != null ? context.Now.cultureSeventh : _state.sevenths;
        foreach (var o in state.orders.Where(o => o != null && o.Open).ToList())
        {
            var step = TransmissionRules.Advance(o, WhyPaused(o), now);
            if (step == TeachingStep.Paused)
            {
                GameLog.Event($"Teaching paused ({o.id}): {o.pausedReason}", Log);
                context?.Notices.Add(($"{o.traditionName}: teaching paused", $"{o.pausedReason} The teaching keeps its {o.progress} of {o.required} Sevenths and its seat; it goes on when that changes, or another teacher can take over.", $"culture:teaching:paused:{o.id}:{o.pausedSince}"));
            }
            if (step == TeachingStep.Ready) CompleteTeaching(o, context, now);
        }
        TransmissionRules.Trim(state, TransmissionTuning);
    }

    private void CompleteTeaching(TeachingOrder o, CultureSeventh context, int now)
    {
        var inst = Transmission.Institution(o.institution);
        string variantName = null;
        if (o.mode == TeachingMode.Adapt) variantName = VariantNameFor(DraftOf(o));
        string made = TransmissionRules.Complete(Transmission, o, now, CurrentAgeId, variantName, inst?.name);
        if (made == null) return;
        string place = PlaceName(o.institution, o.settlement, o.settlementName);
        string text = o.mode == TeachingMode.Preserve ? $"{Learner(o.learnerKind, o.learnerLabel)} learned {o.traditionName} from {o.teacherLabel} at {place}"
            : o.mode == TeachingMode.Adapt ? $"{Learner(o.learnerKind, o.learnerLabel)} made {o.traditionName} their own as {Transmission.Variant(made)?.name}, taught by {o.teacherLabel}"
            : $"{o.traditionName} was written down at {place} from {o.teacherLabel}";
        var occurrence = Occurrence($"teaching:{o.id}", CulturalOccurrenceKind.Teaching, CultureEntityRef.Of(CultureEntityKind.Tradition, o.tradition, o.traditionName), o.settlement, 1f, CultureQuantityUnit.Occasions, text,
            new[] { o.teacherKind == TeacherKind.Legend ? o.teacher : null, o.learnerKind == LearnerKind.Legend ? o.learner : null });
        if (o.recipe != null && o.recipe.IsKnown) occurrence.recipe = o.recipe.Copy();
        if (inst != null) occurrence.source = CultureEntityRef.Of(CultureEntityKind.Landmark, inst.landmark, inst.name);
        Record(occurrence);
        Remember(o.mode == TeachingMode.Record ? "record" : "teaching", o.mode == TeachingMode.Adapt ? Transmission.Variant(made)?.name ?? o.traditionName : o.traditionName, text + ".");
        GameLog.Event($"Teaching completed ({o.id} -> {made}): {text}.", Log);
        context?.Notices.Add((o.mode == TeachingMode.Record ? $"{o.traditionName}, written down" : $"{o.traditionName}, carried on", text + ".", $"culture:teaching:done:{o.id}"));
    }

    /// <summary>After a load, the founding or a start: the records are made whole. Nothing is replayed or granted.</summary>
    internal void ReconcileTransmission(CultureReconcileReason reason) => TransmissionRules.Ensure(Transmission);

    /// <summary>
    /// A value for conditions (for the owner of the condition registry to route): teaching_orders (open), teaching_bearers,
    /// teaching_variants, teaching_records, institutions (open), institution:&lt;spec&gt; (1 when one is open). NaN otherwise.
    /// </summary>
    public float TeachingValue(string key)
    {
        var s = Transmission;
        switch (key)
        {
            case "teaching_orders": return s.orders.Count(o => o != null && o.Open);
            case "teaching_bearers": return s.bearers.Count;
            case "teaching_variants": return s.variants.Count;
            case "teaching_records": return s.records.Count;
            case "institutions": return s.institutions.Count(i => i != null && string.IsNullOrEmpty(i.evolvedInto) && WhyPlaceClosed(i.id, i.settlement, i.settlementName) == null);
        }
        if (key != null && key.StartsWith("institution:"))
        {
            string spec = key.Substring(12).Trim();
            return s.institutions.Any(i => i != null && string.Equals(i.spec, spec, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(i.evolvedInto) && WhyPlaceClosed(i.id, i.settlement, i.settlementName) == null) ? 1f : 0f;
        }
        return float.NaN;
    }
}

/// <summary>Who carries a tradition now, told apart from what is only written (<see cref="CultureSystem.CarriersOf"/>).</summary>
public sealed class TraditionCarriers
{
    public string tradition;
    /// <summary>It is practised now where it lives (T01's stage).</summary>
    public bool lived;
    public readonly List<string> livingLegends = new List<string>();
    public readonly List<(string legend, string why)> awayLegends = new List<(string, string)>();
    public readonly List<string> lostLegends = new List<string>();
    public readonly List<(int settlement, string name)> communities = new List<(int, string)>();
    public readonly List<(int settlement, string name)> formerCommunities = new List<(int, string)>();
    public readonly List<PracticeVariant> variants = new List<PracticeVariant>();
    public readonly List<WrittenRecord> records = new List<WrittenRecord>();

    /// <summary>Someone living keeps it (a Legend here or away, or a community): not only a record.</summary>
    public bool Living => livingLegends.Count > 0 || awayLegends.Count > 0 || communities.Count > 0;
}

/// <summary>What teaching offers the culture's queries (T01's <see cref="ICultureQuery"/>): snapshots only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<TeachingOrder> TeachingOrders();
    IReadOnlyList<InstitutionRecord> TeachingInstitutions();
    IReadOnlyList<PracticeVariant> PracticeVariants();
    IReadOnlyList<WrittenRecord> WrittenRecords();
    TraditionCarriers CarriersOf(string tradition);
    CultureCommandResult PreviewTeaching(TeachingDraft draft);
}

/// <summary>Teaching's part of the culture's Seventh (found by <see cref="CultureFeatures"/>): before the traditions' lifecycle, so a completed lesson joins the Seventh.</summary>
public sealed class TransmissionFeature : ICultureFeature
{
    public string Id => "transmission";
    public CulturePhase Phase => CulturePhase.SourceActions;
    public int Order => 60;

    public void Seventh(CultureSeventh context) => context?.Culture?.TransmissionSeventh(context);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileTransmission(reason);
}
