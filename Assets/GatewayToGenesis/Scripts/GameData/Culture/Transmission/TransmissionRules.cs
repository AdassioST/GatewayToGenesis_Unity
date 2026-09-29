using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A teaching order as the player sets it up (<see cref="TransmissionRules.WhyNotStart"/>, <see cref="TransmissionRules.Start"/>):
/// the tradition, who teaches, who learns, where, and (for a food practice) which dish.
/// </summary>
public sealed class TeachingDraft
{
    public string tradition, traditionName, definition;
    /// <summary>The variant taught instead of its parent (empty: the tradition itself).</summary>
    public string variant;
    public bool food;
    public TeachingMode mode;
    public TeacherKind teacherKind;
    public string teacher, teacherLabel;
    public LearnerKind learnerKind;
    public string learner, learnerLabel;
    /// <summary>The institution's id, or empty for the hearth of <see cref="settlement"/>.</summary>
    public string institution;
    public int settlement = -1;
    public string settlementName;
    public CultureEntityRef recipe = new CultureEntityRef();
    /// <summary>No one living keeps it (taught from a record, or a practice fallen quiet).</summary>
    public bool recovery;

    public TeachingDraft Copy() => (TeachingDraft)MemberwiseClone();
}

/// <summary>What one Seventh did to an order.</summary>
public enum TeachingStep
{
    /// <summary>Nothing (finished, or this Seventh was counted already).</summary>
    None,
    Progressed,
    /// <summary>It stopped: something it needs is missing.</summary>
    Paused,
    /// <summary>Still waiting (the reason may have changed).</summary>
    Waiting,
    /// <summary>It advanced and has all the Sevenths it needs: complete it.</summary>
    Ready,
}

/// <summary>
/// The rules of teaching, pure (no scene): seats, complexity, who may teach and learn, the Sevenths an order needs, how it
/// pauses and resumes, what it makes when it completes (once), and founding (or growing) an institution. The system
/// (CultureSystem.Transmission.cs) supplies what the world says (who is away, which places stand).
/// </summary>
public static class TransmissionRules
{
    public static void Ensure(TransmissionState s)
    {
        if (s == null) return;
        if (s.orders == null) s.orders = new List<TeachingOrder>();
        if (s.institutions == null) s.institutions = new List<InstitutionRecord>();
        if (s.bearers == null) s.bearers = new List<TaughtBearer>();
        if (s.variants == null) s.variants = new List<PracticeVariant>();
        if (s.records == null) s.records = new List<WrittenRecord>();
        s.orders.RemoveAll(o => o == null);
        s.institutions.RemoveAll(i => i == null);
        s.bearers.RemoveAll(b => b == null);
        s.variants.RemoveAll(v => v == null);
        s.records.RemoveAll(r => r == null);
        foreach (var o in s.orders) if (o.recipe == null) o.recipe = new CultureEntityRef();
        foreach (var v in s.variants) if (v.recipe == null) v.recipe = new CultureEntityRef();
        foreach (var r in s.records) if (r.recipe == null) r.recipe = new CultureEntityRef();
        // Ids already given are never given again, whatever the saved counters say.
        s.nextOrder = Math.Max(s.nextOrder, Next(s.orders.Select(o => o.id), "teach-"));
        s.nextInstitution = Math.Max(s.nextInstitution, Next(s.institutions.Select(i => i.id), "inst-"));
        s.nextBearer = Math.Max(s.nextBearer, Next(s.bearers.Select(b => b.id), "taught-"));
        s.nextVariant = Math.Max(s.nextVariant, Next(s.variants.Select(v => v.id), "var-"));
        s.nextRecord = Math.Max(s.nextRecord, Next(s.records.Select(r => r.id), "rec-"));
    }

    private static int Next(IEnumerable<string> ids, string prefix)
    {
        int next = 1;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(prefix.Length), out int n) && n >= next) next = n + 1;
        return next;
    }

    // ===== PLACES AND SEATS =====

    /// <summary>A place's key: the institution's id, or "hearth:12" for a settlement's hearth.</summary>
    public static string PlaceKey(string institution, int settlement) =>
        !string.IsNullOrEmpty(institution) ? institution : $"{TransmissionTuning.Hearth}:{CultureIds.Settlement(settlement)}";

    public static string PlaceKey(TeachingOrder o) => PlaceKey(o.institution, o.settlement);

    /// <summary>Seats taken at a place: its open orders (a paused order keeps its seat until cancelled).</summary>
    public static int Occupied(TransmissionState s, string placeKey) =>
        s == null ? 0 : s.orders.Count(o => o != null && o.Open && PlaceKey(o) == placeKey);

    /// <summary>The institutions meeting at a landmark now (an institution that grew into another is not).</summary>
    public static IEnumerable<InstitutionRecord> At(TransmissionState s, string landmark) =>
        s == null ? Enumerable.Empty<InstitutionRecord>() : s.institutions.Where(i => i != null && string.IsNullOrEmpty(i.evolvedInto) && string.Equals(i.landmark, landmark, StringComparison.OrdinalIgnoreCase));

    // ===== ORDERS =====

    /// <summary>How hard it is to teach: the practice's own complexity, more when it is adapted (a record is as hard as the practice).</summary>
    public static int Complexity(TransmissionTuning t, string definition, bool food, TeachingMode mode) =>
        t.ComplexityOf(definition, food) + (mode == TeachingMode.Adapt ? Math.Max(0, t.adaptComplexity) : 0);

    /// <summary>Sevenths an order needs: per point of complexity, longer when no one living keeps the practice.</summary>
    public static int Required(TransmissionTuning t, int complexity, bool recovery)
    {
        float sevenths = Math.Max(1, t.seventhsPerComplexity) * Math.Max(1, complexity) * (recovery ? Math.Max(1f, t.recoveryFactor) : 1f);
        return Math.Max(1, (int)Math.Ceiling(sevenths - 1e-4f));
    }

    /// <summary>
    /// Why <paramref name="d"/> cannot begin at a place of kind <paramref name="place"/> with <paramref name="placeName"/>, or null.
    /// The caller has already checked what the world says (who is there, which places stand, what the kitchen knows).
    /// </summary>
    public static string WhyNotStart(TransmissionState s, TransmissionTuning t, TeachingDraft d, InstitutionSpec place, string placeName)
    {
        if (s == null || t == null || d == null) return "Nothing to teach.";
        if (string.IsNullOrEmpty(d.tradition)) return "Choose a tradition.";
        if (place == null) return "Choose where it is taught.";
        placeName = placeName ?? place.name;
        if (d.mode == TeachingMode.Record)
        {
            if (!place.keepsRecords) return $"{placeName} keeps no records: a Flavor Log or a guild writes practices down.";
            if (d.teacherKind == TeacherKind.Record) return "A record is written from someone who keeps the practice, not copied from another record.";
        }
        else if (d.learnerKind == LearnerKind.None || string.IsNullOrEmpty(d.learner)) return "Choose who learns it.";
        if (string.IsNullOrEmpty(d.teacher)) return "Choose who teaches it.";
        if (!place.Teaches(d.food)) return $"{placeName} does not teach {(d.food ? "food practices" : "songs, rites or tales")}.";
        int complexity = Complexity(t, d.definition, d.food, d.mode);
        if (complexity > place.maxComplexity)
            return $"Too complex for {placeName} (complexity {complexity}; it teaches up to {place.maxComplexity}): an institution is needed.";
        if (d.teacherKind == TeacherKind.Record && complexity >= t.livingTeacherFrom)
            return $"Too complex to learn from a record alone (complexity {complexity}): it needs someone living who keeps it.";
        int seats = Math.Max(0, place.seats);
        string key = PlaceKey(d.institution, d.settlement);
        if (Occupied(s, key) >= seats) return $"Every seat at {placeName} is taken ({seats} of {seats}): wait, or cancel an order there.";
        foreach (var o in s.orders.Where(o => o != null && o.Open))
        {
            if (d.teacherKind == TeacherKind.Legend && o.teacherKind == TeacherKind.Legend && Same(o.teacher, d.teacher))
                return $"{d.teacherLabel ?? d.teacher} is already teaching {o.traditionName}.";
            if (d.teacherKind == TeacherKind.Legend && o.learnerKind == LearnerKind.Legend && Same(o.learner, d.teacher))
                return $"{d.teacherLabel ?? d.teacher} is learning {o.traditionName}.";
            if (d.learnerKind == LearnerKind.Legend && (o.learnerKind == LearnerKind.Legend && Same(o.learner, d.learner) || o.teacherKind == TeacherKind.Legend && Same(o.teacher, d.learner)))
                return $"{d.learnerLabel ?? d.learner} is already busy with {o.traditionName}.";
            if (o.tradition == d.tradition && Same(o.variant, d.variant) && o.mode == d.mode && o.learnerKind == d.learnerKind && Same(o.learner, d.learner) && Same(o.institution, d.institution))
                return $"This is being taught already ({o.id}).";
        }
        // A Legend never teaches themselves; a community may make its own form of what it keeps (adapt), not learn it again.
        bool self = Same(d.teacher, d.learner) && (d.teacherKind == TeacherKind.Legend && d.learnerKind == LearnerKind.Legend
            || d.teacherKind == TeacherKind.Community && d.learnerKind == LearnerKind.Community && d.mode != TeachingMode.Adapt);
        if (self) return d.teacherKind == TeacherKind.Legend ? "No one teaches themselves." : $"{d.learnerLabel ?? d.learner} keeps it already.";
        switch (d.mode)
        {
            case TeachingMode.Preserve:
                if (Carries(s, d.tradition, d.variant, d.learnerKind, d.learner)) return $"{d.learnerLabel ?? d.learner} already carries it.";
                break;
            case TeachingMode.Adapt:
                int where = d.learnerKind == LearnerKind.Community && int.TryParse(d.learner, out int c) ? c : d.settlement;
                var variant = s.variants.FirstOrDefault(v => v.parent == d.tradition && v.settlement == where && string.Equals(v.settlementName, d.learnerKind == LearnerKind.Community ? d.learnerLabel : d.settlementName, StringComparison.Ordinal));
                if (variant != null) return $"{variant.settlementName} already keeps its own form of it ({variant.name}).";
                break;
            case TeachingMode.Record:
                if (s.records.Any(r => r.tradition == d.tradition && Same(r.variant, d.variant) && r.institution == d.institution))
                    return $"It is written down at {placeName} already.";
                break;
        }
        return null;
    }

    private static bool Same(string a, string b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="who"/> already learned the tradition (or that variant) by teaching.</summary>
    public static bool Carries(TransmissionState s, string tradition, string variant, LearnerKind kind, string who) =>
        s != null && s.bearers.Any(b => b.tradition == tradition && Same(b.variant, variant) && b.kind == kind && Same(b.who, who));

    /// <summary>Begin an order (the caller checked <see cref="WhyNotStart"/>).</summary>
    public static TeachingOrder Start(TransmissionState s, TransmissionTuning t, TeachingDraft d, int now, string ageId)
    {
        Ensure(s);
        int complexity = Complexity(t, d.definition, d.food, d.mode);
        bool recovery = d.recovery || d.teacherKind == TeacherKind.Record;
        var o = new TeachingOrder
        {
            id = "teach-" + s.nextOrder++, mode = d.mode, status = TeachingStatus.Active, tradition = d.tradition, traditionName = d.traditionName, definition = d.definition,
            variant = d.variant ?? string.Empty, teacherKind = d.teacherKind, teacher = d.teacher, teacherLabel = d.teacherLabel ?? d.teacher,
            learnerKind = d.mode == TeachingMode.Record ? LearnerKind.None : d.learnerKind, learner = d.mode == TeachingMode.Record ? string.Empty : d.learner,
            learnerLabel = d.mode == TeachingMode.Record ? string.Empty : d.learnerLabel ?? d.learner,
            institution = d.institution ?? string.Empty, settlement = d.settlement, settlementName = d.settlementName, recipe = d.recipe?.Copy() ?? new CultureEntityRef(),
            complexity = complexity, required = Required(t, complexity, recovery), recovery = recovery, startedSeventh = now, startedAge = ageId,
        };
        s.orders.Add(o);
        return o;
    }

    /// <summary>
    /// A Seventh of one order. <paramref name="whyPaused"/> null: everything it needs is there, it advances (once per
    /// Seventh); otherwise it pauses (keeping its progress and seat). Ready: it has its Sevenths; call <see cref="Complete"/>.
    /// </summary>
    public static TeachingStep Advance(TeachingOrder o, string whyPaused, int now)
    {
        if (o == null || !o.Open) return TeachingStep.None;
        if (whyPaused != null)
        {
            bool was = o.status == TeachingStatus.Active;
            o.status = TeachingStatus.Paused;
            o.pausedReason = whyPaused;
            if (was) o.pausedSince = now;
            return was ? TeachingStep.Paused : TeachingStep.Waiting;
        }
        if (o.lastProgress == now) return o.progress >= o.required ? TeachingStep.Ready : TeachingStep.None;
        o.status = TeachingStatus.Active;
        o.pausedReason = null;
        o.pausedSince = -1;
        o.progress = Math.Min(o.required, o.progress + 1);
        o.lastProgress = now;
        return o.progress >= o.required ? TeachingStep.Ready : TeachingStep.Progressed;
    }

    /// <summary>"The Ash-Loaf Table of Ashford" (", with Ember Bread" when its dish differs from its parent's).</summary>
    public static string VariantName(string parentName, string settlementName, string dish, string parentDish)
    {
        string name = $"{parentName ?? "A practice"} of {settlementName ?? "a settlement"}";
        return !string.IsNullOrEmpty(dish) && !string.Equals(dish, parentDish, StringComparison.OrdinalIgnoreCase) ? $"{name}, with {dish}" : name;
    }

    /// <summary>
    /// Finish an order that has its Sevenths: its bearer, variant or record is made (once: a completed order makes
    /// nothing again). Returns what it made (its id), or null when there was nothing to do.
    /// </summary>
    public static string Complete(TransmissionState s, TeachingOrder o, int now, string ageId, string variantName = null, string institutionName = null)
    {
        if (s == null || o == null || !o.Open || o.progress < o.required || !string.IsNullOrEmpty(o.result)) return null;
        switch (o.mode)
        {
            case TeachingMode.Preserve:
                o.result = AddBearer(s, o, o.variant, now, ageId).id;
                break;
            case TeachingMode.Adapt:
            {
                bool community = o.learnerKind == LearnerKind.Community && int.TryParse(o.learner, out _);
                var v = new PracticeVariant
                {
                    id = "var-" + s.nextVariant++, parent = o.tradition, parentDefinition = o.definition, parentName = o.traditionName,
                    settlement = community ? int.Parse(o.learner) : o.settlement, settlementName = community ? o.learnerLabel : o.settlementName,
                    recipe = o.recipe?.Copy() ?? new CultureEntityRef(), order = o.id, taughtBy = o.teacherLabel, seventh = now, ageId = ageId,
                };
                v.name = variantName ?? VariantName(o.traditionName, v.settlementName, v.recipe.IsKnown ? v.recipe.Display : null, null);
                s.variants.Add(v);
                AddBearer(s, o, v.id, now, ageId);
                o.result = v.id;
                break;
            }
            case TeachingMode.Record:
            {
                var r = new WrittenRecord
                {
                    id = "rec-" + s.nextRecord++, tradition = o.tradition, traditionName = o.traditionName, variant = o.variant, recipe = o.recipe?.Copy() ?? new CultureEntityRef(),
                    institution = o.institution, institutionName = institutionName, settlementName = o.settlementName, order = o.id, writtenFrom = o.teacherLabel, seventh = now, ageId = ageId,
                };
                s.records.Add(r);
                o.result = r.id;
                break;
            }
        }
        o.status = TeachingStatus.Completed;
        o.pausedReason = null;
        o.endedSeventh = now;
        return o.result;
    }

    private static TaughtBearer AddBearer(TransmissionState s, TeachingOrder o, string variant, int now, string ageId)
    {
        var existing = s.bearers.FirstOrDefault(b => b.order == o.id);
        if (existing != null) return existing;
        var b = new TaughtBearer
        {
            id = "taught-" + s.nextBearer++, tradition = o.tradition, variant = variant ?? string.Empty, kind = o.learnerKind, who = o.learner, label = o.learnerLabel,
            order = o.id, taughtBy = o.teacherLabel, seventh = now, ageId = ageId,
        };
        s.bearers.Add(b);
        return b;
    }

    /// <summary>Stop an open order: its seat frees, its progress is kept in its history, nothing is made.</summary>
    public static bool Cancel(TeachingOrder o, int now)
    {
        if (o == null || !o.Open) return false;
        o.status = TeachingStatus.Cancelled;
        o.endedSeventh = now;
        o.pausedReason = null;
        return true;
    }

    /// <summary>Another teacher takes over an open order (a Legend lost, a community fallen): its progress is kept.</summary>
    public static bool Reassign(TeachingOrder o, TeacherKind kind, string teacher, string label, TransmissionTuning t)
    {
        if (o == null || !o.Open || string.IsNullOrEmpty(teacher)) return false;
        if (o.mode == TeachingMode.Record && kind == TeacherKind.Record) return false;
        o.teacherKind = kind;
        o.teacher = teacher;
        o.teacherLabel = label ?? teacher;
        if (kind == TeacherKind.Record && !o.recovery)
        {
            o.recovery = true;
            o.required = Math.Max(o.required, Required(t, o.complexity, true));
        }
        return true;
    }

    /// <summary>Keep the finished orders bounded (what they made stays for good).</summary>
    public static void Trim(TransmissionState s, TransmissionTuning t)
    {
        if (s == null) return;
        var finished = s.orders.Where(o => o != null && !o.Open).OrderBy(o => o.endedSeventh).ToList();
        int extra = finished.Count - Math.Max(0, t.finishedOrdersKept);
        for (int i = 0; i < extra; i++) s.orders.Remove(finished[i]);
    }

    // ===== INSTITUTIONS =====

    /// <summary>
    /// Why <paramref name="spec"/> cannot be founded in the landmark <paramref name="landmark"/> (of kind
    /// <paramref name="landmarkSpec"/>) in Age <paramref name="age"/>, or null. Unity is the caller's to check.
    /// </summary>
    public static string WhyNotFound(TransmissionState s, InstitutionSpec spec, string landmarkSpec, string landmark, int age)
    {
        if (spec == null) return "No such institution.";
        if (spec.informal) return $"{spec.name} needs no founding: every settlement has one.";
        if (!spec.OpenIn(age)) return $"{spec.name} belongs to {spec.AgeText} (this is Age {AgeRules.Roman(age)}).";
        if (string.IsNullOrEmpty(landmark) || spec.venues == null || !spec.venues.Any(v => string.Equals(v, landmarkSpec, StringComparison.OrdinalIgnoreCase)))
            return $"{spec.name} meets in a {string.Join(" or ", (spec.venues ?? new List<string>()).Select(Pretty))}.";
        foreach (var here in At(s, landmark))
        {
            if (string.Equals(here.spec, spec.id, StringComparison.OrdinalIgnoreCase)) return $"{here.name} meets here already.";
            if (!string.Equals(here.spec, spec.evolvesFrom, StringComparison.OrdinalIgnoreCase)) return $"{here.name} meets here already: one institution to a landmark.";
        }
        return null;
    }

    private static string Pretty(string id) => string.IsNullOrEmpty(id) ? "landmark" : char.ToUpperInvariant(id[0]) + id.Substring(1).Replace('-', ' ');

    /// <summary>
    /// Found <paramref name="spec"/> at a landmark (the caller checked <see cref="WhyNotFound"/>). An institution it grows
    /// out of, meeting there, becomes it: its open orders move over with their progress; its records stay its own.
    /// </summary>
    public static InstitutionRecord Found(TransmissionState s, InstitutionSpec spec, string landmark, int settlement, string settlementName, string name, int now, string ageId)
    {
        Ensure(s);
        var record = new InstitutionRecord
        {
            id = "inst-" + s.nextInstitution++, spec = spec.id, name = name ?? $"{spec.name} of {settlementName}", landmark = landmark, settlement = settlement,
            settlementName = settlementName, foundedSeventh = now, foundedAge = ageId,
        };
        var from = !string.IsNullOrEmpty(spec.evolvesFrom) ? At(s, landmark).FirstOrDefault(i => string.Equals(i.spec, spec.evolvesFrom, StringComparison.OrdinalIgnoreCase)) : null;
        if (from != null)
        {
            from.evolvedInto = record.id;
            record.evolvedFrom = from.id;
            foreach (var o in s.orders.Where(o => o != null && o.Open && o.institution == from.id)) o.institution = record.id;
        }
        s.institutions.Add(record);
        return record;
    }

    // ===== WHAT CARRIES A TRADITION =====

    /// <summary>Who learned the tradition (or one of its variants when <paramref name="variant"/> is null) by teaching.</summary>
    public static IEnumerable<TaughtBearer> BearersOf(TransmissionState s, string tradition, string variant = null) =>
        s == null ? Enumerable.Empty<TaughtBearer>() : s.bearers.Where(b => b.tradition == tradition && (variant == null || Same(b.variant, variant)));

    public static IEnumerable<PracticeVariant> VariantsOf(TransmissionState s, string tradition) =>
        s == null ? Enumerable.Empty<PracticeVariant>() : s.variants.Where(v => v.parent == tradition);

    public static IEnumerable<WrittenRecord> RecordsOf(TransmissionState s, string tradition) =>
        s == null ? Enumerable.Empty<WrittenRecord>() : s.records.Where(r => r.tradition == tradition);

    public static string ModeWord(TeachingMode mode) => mode == TeachingMode.Adapt ? "adapt" : mode == TeachingMode.Record ? "write down" : "preserve";

    public static string StatusWord(TeachingOrder o)
    {
        if (o == null) return string.Empty;
        switch (o.status)
        {
            case TeachingStatus.Active: return $"{o.progress} of {o.required} Sevenths";
            case TeachingStatus.Paused: return $"paused at {o.progress} of {o.required}: {o.pausedReason}";
            case TeachingStatus.Completed: return "completed";
            default: return "cancelled";
        }
    }
}
