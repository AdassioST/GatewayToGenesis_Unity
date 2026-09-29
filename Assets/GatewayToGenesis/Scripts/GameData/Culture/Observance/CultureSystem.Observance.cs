using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Observances (T04; rules in <see cref="ObservanceRules"/>, numbers and authored observances in
/// <see cref="ObservanceTuning"/>, saved in <see cref="CultureExtensionState.observances"/>): the culture's calendar.
/// Every holiday is linked to an observance on its own day (its date kept); new observances choose a cause, a place, a
/// rite, a dish or tea, a recurrence and a scale. This is the one completion owner of every day on the calendar: each
/// occasion is decided once, in the Seventh's life (before the occurrences go through the culture's phases).
/// - A holiday nobody changed is kept exactly as a holiday always was (<see cref="KeepHoliday"/>).
/// - A table can be prepared ahead (its food taken from the stores and returned whole if cancelled or missed); without
///   one, the table is revalidated on the day and, if the stores cannot set it, the day is kept quietly.
/// - A quiet remembrance asks for no feast, Unity or high morale; a vigil keeps to the full Moon and its grove.
/// </summary>
public partial class CultureSystem
{
    private readonly ObservanceTuning _observanceDefaults = new ObservanceTuning();

    public ObservanceTuning ObservanceTuning => Settings != null && Settings.observances != null ? Settings.observances : _observanceDefaults;

    private ObservanceState ObservanceData
    {
        get
        {
            var x = Extensions;
            if (x.observances == null) x.observances = new ObservanceState();
            return x.observances;
        }
    }

    /// <summary>Today's date (<see cref="CultureStamp.Index"/>).</summary>
    public long Today => ObservanceCalendar.Date(Stamp());

    /// <summary>An occasion of an observance was decided (kept, kept smaller, missed).</summary>
    public event Action<ObservanceView, ObservanceOccasion> ObservanceDecided;

    private static int People => PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.population : 0;

    private Settlement SettlementById(int id) =>
        id < 0 || WorldSystem.Instance == null || WorldSystem.Instance.Map == null ? null : WorldSystem.Instance.Map.Settlements.FirstOrDefault(s => s != null && s.id == id);

    // ===== CHECKS =====

    private ObservanceChecks ObservanceChecksNow() => new ObservanceChecks
    {
        founded = _state.founded, age = GameAge.Number, researched = Researched,
        groundOf = id =>
        {
            var s = SettlementById(id);
            return s == null ? null : WorldSystem.Instance.SettlementGround(s) ?? string.Empty;
        },
        causeKnown = ObservanceCauseKnown, whyNotRite = WhyNotRite, whyNotFood = WhyNotServable, held = HeldExact,
    };

    private bool ObservanceCauseKnown(CultureEntityRef cause)
    {
        if (cause == null || !cause.IsKnown) return false;
        if (cause.kind == CultureEntityKind.Evidence) return RememberedCause(cause.id) != null;
        if (cause.kind == CultureEntityKind.Holiday) return _state.holidays.Any(h => h != null && string.Equals(h.name, cause.id, StringComparison.OrdinalIgnoreCase));
        return false;
    }

    private string WhyNotRite(string id)
    {
        var a = Life.Activity(id);
        if (a == null) return $"No rite '{id}' is known.";
        return !string.IsNullOrEmpty(a.technology) && !Researched(a.technology) ? $"{a.name} needs {a.technology}." : null;
    }

    // ===== CHOICES FOR A PLAN =====

    public ObservanceDefinition ObservanceDefinitionOf(string id) => ObservanceTuning.Definition(id);

    /// <summary>The settlements whose ground lets <paramref name="definition"/> be kept there (all held ones when it needs no ground).</summary>
    public IReadOnlyList<Settlement> ObservanceVenues(string definition)
    {
        var d = ObservanceDefinitionOf(definition);
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (d == null || map == null) return new List<Settlement>();
        return map.Settlements.Where(s => s != null && d.FitsGround(WorldSystem.Instance.SettlementGround(s))).ToList();
    }

    /// <summary>The losses the culture remembers (T03), as causes an observance may keep.</summary>
    public IReadOnlyList<CultureEntityRef> ObservanceCauses() => MemoryCauses().Select(e => e.Ref).ToList();

    /// <summary>What <paramref name="definition"/> may serve: its suggested foods first (those that can be served), then every dish, tea or drink held.</summary>
    public IReadOnlyList<CultureEntityRef> ObservanceFoods(string definition)
    {
        var d = ObservanceDefinitionOf(definition);
        var list = new List<CultureEntityRef>();
        if (d == null || !d.food) return list;
        void Add(CultureEntityRef r)
        {
            if (r != null && r.IsKnown && WhyNotServable(r) == null && !list.Any(x => string.Equals(FoodResource(x), FoodResource(r), StringComparison.OrdinalIgnoreCase))) list.Add(r);
        }
        foreach (var s in d.suggestedFoods ?? new List<string>())
            Add(Life.Recipe(s) != null ? CultureEntityRef.Of(CultureEntityKind.Recipe, Life.Recipe(s).id, Life.Recipe(s).dish) : CultureEntityRef.Of(CultureEntityKind.Resource, s, s));
        if (Pantry.Instance != null)
            foreach (var s in Pantry.Instance.Stock().Where(s => s.amount > 0f).OrderByDescending(s => s.amount))
            {
                var recipe = RecipeOfDish(s.kind.resource);
                Add(recipe != null ? recipe.Ref : CultureEntityRef.Of(CultureEntityKind.Resource, s.kind.resource, s.kind.resource));
            }
        return list;
    }

    /// <summary>A plan for <paramref name="definition"/> starting from today: its first recurrence, today's day, the first fitting place, the first cause, its first suggested food and the largest scale it allows.</summary>
    public ObservancePlan DefaultObservancePlan(string definition)
    {
        var d = ObservanceDefinitionOf(definition);
        var now = Stamp();
        var p = new ObservancePlan { definition = definition, seventh = now.seventh, phase = now.phase, echo = now.echo };
        if (d == null) return p;
        p.recurrence = d.recurrences != null && d.recurrences.Count > 0 ? d.recurrences[0] : ObservanceRecurrence.EveryEcho;
        if (p.recurrence == ObservanceRecurrence.RitualSeventh) { p.seventh = ObservanceCalendar.PerPhase; p.phase = 0; }
        if (d.grounds != null && d.grounds.Count > 0) p.settlement = ObservanceVenues(definition).Select(s => s.id).DefaultIfEmpty(-1).First();
        if (d.needsCause)
        {
            var cause = MemoryCauses().LastOrDefault();
            if (cause != null) { p.cause = cause.Ref; p.causeText = cause.title; }
        }
        if (d.food) p.food = ObservanceFoods(definition).FirstOrDefault() ?? new CultureEntityRef();
        var scales = d.scales != null && d.scales.Count > 0 ? d.scales : new List<ObservanceScale> { ObservanceScale.Quiet, ObservanceScale.Modest, ObservanceScale.Full };
        p.scale = scales.Max();
        if (d.objective == ObservanceObjective.Remembrance) p.scale = ObservanceScale.Quiet;
        if (d.repertoire != null && d.repertoire.Count > 0 && d.objective == ObservanceObjective.Remembrance) p.repertoire = d.repertoire.FirstOrDefault(r => WhyNotRite(r) == null);
        return p;
    }

    // ===== ESTABLISHING =====

    private string WhyNotEstablishObservance(ObservanceDefinition d, ObservancePlan p)
    {
        if (d == null || p == null) return "No such observance.";
        if (d.id == ObservanceRules.Anniversary)
        {
            // A holiday keeps its own gates (a festival first, high morale, Unity); its extras are checked like any observance's.
            string holiday = WhyNotHoliday();
            if (holiday != null) return holiday;
            var extras = new ObservancePlan
            {
                definition = d.id, recurrence = ObservanceRecurrence.EveryEcho, seventh = Now.seventh, phase = Now.phase, repertoire = p.repertoire, food = p.food, scale = p.scale,
            };
            return ObservanceRules.WhyNotEstablish(d, extras, ObservanceChecksNow(), null, ObservanceTuning);
        }
        return ObservanceRules.WhyNotEstablish(d, p, ObservanceChecksNow(), ObservanceData, ObservanceTuning);
    }

    /// <summary>What setting <paramref name="p"/> apart would do (its day, place, cost, what each occasion asks and gives), or why it cannot. Nothing is changed.</summary>
    public CultureCommandResult PreviewEstablishObservance(ObservancePlan p)
    {
        var d = ObservanceDefinitionOf(p?.definition);
        string why = WhyNotEstablishObservance(d, p);
        if (why != null) return CultureCommandResult.Fail(why);
        var r = CultureCommandResult.Ok();
        if (d.id == ObservanceRules.Anniversary)
        {
            var preview = HolidayPreview();
            r.reason = $"{preview.name}, remembering {preview.occasion}: kept on {CultureCalendar.Day(Now.seventh, Now.phase)}, and today for the first time. "
                + $"Set apart for {NextHolidayCost:0} {UnityResource}; each holiday adds {Life.holidayMaxMorale:0.#} max morale for good. {PlanText(d, ObservanceObjective.Celebration, p.scale, p.food, p.repertoire, !Customizes(p))}";
            r.paid.Add(new ResourceAmount { resource = UnityResource, amount = NextHolidayCost });
            return r;
        }
        string place = p.settlement >= 0 ? $" in {SettlementName(p.settlement) ?? "a settlement"}" : string.Empty;
        string cause = p.cause != null && p.cause.IsKnown ? $", remembering {p.causeText ?? p.cause.Display}" : string.Empty;
        r.reason = $"{d.name}{place}{cause}: kept on {ObservanceCalendar.DayText(p.recurrence, p.seventh, p.phase, p.echo)}, first on {FirstDateText(d, p)}. "
            + $"Set apart for {CostText(d.establishCost)}. {PlanText(d, d.objective, p.scale, p.food, p.repertoire, false)}";
        r.paid.AddRange(d.establishCost.Where(c => c != null && c.amount > 0f).Select(c => new ResourceAmount { resource = c.resource, amount = c.amount }));
        return r;
    }

    private string FirstDateText(ObservanceDefinition d, ObservancePlan p)
    {
        var probe = new Observance
        {
            recurrence = p.recurrence, seventh = p.recurrence == ObservanceRecurrence.RitualSeventh ? ObservanceCalendar.PerPhase : p.seventh, phase = p.phase, echo = p.echo, lastResolved = Today,
        };
        long first = ObservanceCalendar.NextOccasion(probe, Today).keepOn;
        return first < 0 ? "a day to come" : $"{ObservanceCalendar.DateText(first)} (in {first - Today} Seventh{(first - Today == 1 ? "" : "s")})";
    }

    private static bool Customizes(ObservancePlan p) =>
        p != null && ((p.food != null && p.food.IsKnown) || !string.IsNullOrEmpty(p.repertoire) || p.scale != ObservanceScale.Full);

    /// <summary>What each occasion asks and gives at <paramref name="scale"/>, and the disclosed fallback when the stores cannot set its table.</summary>
    public string PlanText(ObservanceDefinition d, ObservanceObjective objective, ObservanceScale scale, CultureEntityRef food, string repertoire, bool holiday)
    {
        var t = ObservanceTuning;
        if (holiday)
        {
            var life = Life;
            float feast = Mathf.Max(life.holidayFeastMinimum, life.holidayFeastPerHundred * People / 100f);
            return $"Each Echo: a table of {feast:0.#} food value from the stores, +{life.holidayUnity:0.#} {UnityResource}, {life.holidayMorale:+0} morale and joy (half when the stores are bare).";
        }
        var rw = ObservanceRules.Rewards(t, objective, scale);
        var gives = new List<string>();
        if (rw.unity > 0f) gives.Add($"+{rw.unity:0.#} {UnityResource}");
        if (rw.morale != 0) gives.Add($"{rw.morale:+0;-0} morale for {rw.sevenths} Sevenths");
        if (rw.joy > 0f) gives.Add("joy");
        if (objective == ObservanceObjective.Remembrance) gives.Add("the memory kept (Memorials)");
        string asks;
        if (scale == ObservanceScale.Quiet) asks = "nothing from the stores";
        else
        {
            float value = ObservanceRules.FeastValue(t, People, scale);
            string table = food != null && food.IsKnown ? $"{PortionsOf(food, value):0.#} {FoodLabel(food)}" : $"{value:0.#} food value";
            var keep = d?.keepCost?.Where(c => c != null && c.amount > 0f).ToList() ?? new List<ResourceAmount>();
            asks = table + (keep.Count > 0 ? ", " + CostText(keep) : string.Empty) + " (prepare it ahead to be sure; if the stores cannot set it on the day, it is kept quietly)";
        }
        string rite = !string.IsNullOrEmpty(repertoire) ? $", with {Life.Activity(repertoire)?.name ?? repertoire}" : string.Empty;
        return $"Each time, {ObservanceRules.ScaleWord(scale)}{rite}: asks {asks}; gives {(gives.Count == 0 ? "little" : string.Join(", ", gives))}.";
    }

    /// <summary>Set <paramref name="p"/> apart: its cost is paid now, and only now. A holiday is set apart today as holidays always were.</summary>
    public CultureCommandResult EstablishObservance(ObservancePlan p)
    {
        var preview = PreviewEstablishObservance(p);
        if (!preview.succeeded) { GameLog.Event("Observance refused: " + preview.reason, Log); return preview; }
        var d = ObservanceDefinitionOf(p.definition);
        var result = CultureCommandResult.Ok();
        result.paid.AddRange(preview.paid);
        if (d.id == ObservanceRules.Anniversary)
        {
            var h = EstablishHoliday();
            if (h == null) return CultureCommandResult.Fail("The holiday could not be set apart.");
            var o = ObservanceData.observances.FirstOrDefault(x => x.legacy && x.holiday == h.name);
            if (o != null && Customizes(p)) ApplyPlanChoices(o, p.scale, p.food, p.repertoire);
            result.reason = $"{h.name} is set apart.";
            return result;
        }
        Pay(preview.paid);
        string name = ObservanceName(d, p);
        var made = ObservanceRules.Establish(ObservanceData, d, p, name, Stamp());
        Remember("observance", made.name, $"{Capital(PeopleWord)} set apart {made.name}{Where(made)}: kept on {ObservanceCalendar.DayText(made)}{(string.IsNullOrEmpty(made.causeText) ? string.Empty : $", for {made.causeText}")}.");
        GameLog.Event($"Observance established: {made.name} ({ObservanceCalendar.DayText(made)}), {CostText(preview.paid)}.", Log);
        NotificationFeed.Push(made.name, $"{Capital(PeopleWord)} will keep {made.name} on {ObservanceCalendar.DayText(made)}.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:observance:" + made.id);
        result.reason = $"{made.name} is on the calendar.";
        RaiseChanged();
        return result;
    }

    private string ObservanceName(ObservanceDefinition d, ObservancePlan p)
    {
        if (d.needsCause && !string.IsNullOrEmpty(p.causeText)) return $"{d.name}: {p.causeText}";
        if (p.settlement >= 0 && d.grounds != null && d.grounds.Count > 0) return $"{d.name} of {SettlementName(p.settlement) ?? "the grove"}";
        return d.name;
    }

    private string Where(Observance o) => o != null && o.settlement >= 0 ? $" in {SettlementName(o.settlement) ?? "a settlement now gone"}" : string.Empty;

    // ===== CHANGING THE PLAN =====

    private (Observance o, ObservanceDefinition d) ObservanceById(string id)
    {
        var o = ObservanceData.Get(id);
        return (o, o != null ? ObservanceDefinitionOf(o.definition) : null);
    }

    /// <summary>Why <paramref name="id"/>'s plan cannot become this scale, food and rite, or null.</summary>
    public string WhyNotConfigureObservance(string id, ObservanceScale scale, CultureEntityRef food, string repertoire)
    {
        var (o, d) = ObservanceById(id);
        if (o == null || o.ended || d == null) return "No such observance.";
        if (ObservanceData.PreparationOf(o.id) != null) return "Its table is prepared: cancel the preparation first.";
        if (!d.Allows(scale)) return $"{o.name} is not kept {ObservanceRules.ScaleWord(scale)}.";
        if (food != null && food.IsKnown)
        {
            if (!d.food) return $"{o.name} sets no table.";
            string why = WhyNotServable(food);
            if (why != null) return why;
        }
        if (!string.IsNullOrEmpty(repertoire))
        {
            if (d.repertoire == null || !d.repertoire.Any(r => string.Equals(r, repertoire, StringComparison.OrdinalIgnoreCase))) return $"{o.name} is not kept with that rite.";
            return WhyNotRite(repertoire);
        }
        return null;
    }

    /// <summary>
    /// Change how an observance is kept from its next occasion on (scale down, another dish or tea, another rite). Its day
    /// stays. A holiday changed this way is kept by the observance rules from then on.
    /// </summary>
    public CultureCommandResult ConfigureObservance(string id, ObservanceScale scale, CultureEntityRef food, string repertoire)
    {
        string why = WhyNotConfigureObservance(id, scale, food, repertoire);
        if (why != null) return CultureCommandResult.Fail(why);
        var (o, d) = ObservanceById(id);
        ApplyPlanChoices(o, scale, food, repertoire);
        RaiseChanged();
        return CultureCommandResult.Ok($"{o.name} will be kept {ObservanceRules.ScaleWord(scale)}{(food != null && food.IsKnown ? $" with {FoodLabel(food)}" : string.Empty)}.");
    }

    private static void ApplyPlanChoices(Observance o, ObservanceScale scale, CultureEntityRef food, string repertoire)
    {
        o.scale = scale;
        o.food = food?.Copy() ?? new CultureEntityRef();
        o.repertoire = repertoire;
        o.revision++;
        if (o.legacy) o.customized = true;
    }

    /// <summary>What preparing <paramref name="id"/>'s next table would take from the stores now, or why it cannot be prepared.</summary>
    public CultureCommandResult PreviewPrepareObservance(string id)
    {
        var (o, d) = ObservanceById(id);
        string why = ObservanceRules.WhyNotPrepare(ObservanceData, o, d, Today, ObservanceTuning, out long keepOn);
        if (why != null) return CultureCommandResult.Fail(why);
        float portions = PortionsOf(o.food, ObservanceRules.FeastValue(ObservanceTuning, People, o.scale));
        if (portions <= 0f) return CultureCommandResult.Fail($"{FoodLabel(o.food)} cannot set a table.");
        var items = new List<ResourceAmount> { new ResourceAmount { resource = FoodResource(o.food), amount = portions } };
        var serve = PreviewServe(items);
        if (!serve.succeeded) return serve;
        serve.reason = $"Takes {CultureServing.Text(items)} from the stores now for {o.name} on {ObservanceCalendar.DateText(keepOn)}; cancelling returns what the stores have room for.";
        return serve;
    }

    /// <summary>Prepare the next table: its food leaves the stores now (all or nothing) and waits for the day.</summary>
    public CultureCommandResult PrepareObservance(string id)
    {
        var preview = PreviewPrepareObservance(id);
        if (!preview.succeeded) return preview;
        var (o, _) = ObservanceById(id);
        ObservanceRules.WhyNotPrepare(ObservanceData, o, null, Today, ObservanceTuning, out long keepOn);
        var reserved = Reserve(preview.paid);
        if (!reserved.succeeded) return reserved;
        ObservanceData.preparations.Add(new ObservancePreparation
        {
            observance = o.id, date = keepOn, scale = o.scale, food = o.food.Copy(), reserved = reserved.paid.Select(r => new ResourceAmount { resource = r.resource, amount = r.amount }).ToList(),
            preparedSeventh = _state.sevenths, revision = o.revision,
        });
        GameLog.Event($"{o.name}: its table is prepared ({CultureServing.Text(reserved.paid)}).", Log);
        RaiseChanged();
        reserved.reason = $"The table for {o.name} is prepared.";
        return reserved;
    }

    /// <summary>Cancel a prepared table: everything reserved goes back to the stores (what they have room for).</summary>
    public CultureCommandResult CancelObservancePreparation(string id)
    {
        var p = ObservanceData.PreparationOf(id);
        if (p == null) return CultureCommandResult.Fail("Nothing is prepared.");
        var back = Unreserve(p.reserved);
        ObservanceData.preparations.Remove(p);
        var r = CultureCommandResult.Ok($"The table is put away: {CultureServing.Text(back)} returned to the stores.");
        RaiseChanged();
        return r;
    }

    public string WhyNotPostponeObservance(string id, int by)
    {
        var (o, d) = ObservanceById(id);
        return ObservanceRules.WhyNotPostpone(o, d, Today, by, ObservanceTuning);
    }

    /// <summary>Move the next occasion <paramref name="by"/> Sevenths later (a feast the stores cannot bear yet). A prepared table waits for it.</summary>
    public CultureCommandResult PostponeObservance(string id, int by)
    {
        string why = WhyNotPostponeObservance(id, by);
        if (why != null) return CultureCommandResult.Fail(why);
        var (o, _) = ObservanceById(id);
        ObservanceRules.Postpone(o, Today, by);
        var prep = ObservanceData.PreparationOf(o.id);
        if (prep != null) prep.date = o.postponedTo;
        RaiseChanged();
        return CultureCommandResult.Ok($"{o.name} will be kept on {ObservanceCalendar.DateText(o.postponedTo)} instead.");
    }

    /// <summary>Take an observance off the calendar (its history stays; a prepared table goes back to the stores). Holidays stay: they are the calendar's own.</summary>
    public CultureCommandResult RetireObservance(string id)
    {
        var (o, _) = ObservanceById(id);
        if (o == null || o.ended) return CultureCommandResult.Fail("No such observance.");
        if (o.legacy) return CultureCommandResult.Fail("A holiday stays on the calendar.");
        if (ObservanceData.PreparationOf(o.id) != null) CancelObservancePreparation(o.id);
        o.ended = true;
        o.revision++;
        Remember("observance-ended", $"{o.name} ends", $"{Capital(PeopleWord)} no longer keep {o.name}; it was kept {o.kept} time{(o.kept == 1 ? "" : "s")}.");
        RaiseChanged();
        return CultureCommandResult.Ok($"{o.name} is no longer kept.");
    }

    // ===== THE DAYS =====

    /// <summary>
    /// The calendar's Seventh (called from the life of the culture, before the occurrences are processed): every
    /// observance's occasions due by today are decided once, and tomorrow's are announced.
    /// </summary>
    private void KeepObservances()
    {
        var data = ObservanceData;
        ObservanceRules.Ensure(data);
        long today = Today;
        // A holiday not linked yet is decided from today on (its own guard, lastKept, stops a second keeping today).
        ObservanceRules.LinkHolidays(data, _state.holidays, Stamp(), today - 1);
        foreach (var o in data.Active.ToList())
            foreach (var (original, keepOn, keep) in ObservanceRules.Due(o, today))
                DecideOccasion(o, original, keepOn, keep);
        // A table prepared for a day that is gone (its observance ended, a stale save) goes back to the stores.
        foreach (var p in data.preparations.Where(p => p.date < today || data.Get(p.observance) == null || data.Get(p.observance).ended).ToList())
        {
            Unreserve(p.reserved);
            data.preparations.Remove(p);
        }
        foreach (var o in data.Active)
        {
            var next = ObservanceCalendar.NextOccasion(o, today);
            if (next.keepOn != today + 1) continue;
            bool prepared = data.PreparationOf(o.id) != null;
            NotificationFeed.Push($"Tomorrow: {o.name}", $"{Capital(PeopleWord)} prepare for {o.name}{(o.scale != ObservanceScale.Quiet && o.food.IsKnown && !prepared ? " (its table is not prepared yet)" : string.Empty)}.",
                NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:eve:{o.id}:{next.keepOn}");
        }
    }

    private void DecideOccasion(Observance o, long original, long keepOn, bool keep)
    {
        var data = ObservanceData;
        var prep = data.PreparationOf(o.id);
        if (prep != null && prep.date != keepOn) prep = null;
        var occasion = new ObservanceOccasion { key = ObservanceRules.OccasionKey(o, original), observance = o.id, date = keepOn, cultureSeventh = _state.sevenths, scale = o.scale };
        string lost = keep ? WhyNotVenueNow(o) : null;
        if (!keep || lost != null)
        {
            // Its day passed unseen, or its place is gone: nothing spent, nothing given; a prepared table goes back.
            if (prep != null)
            {
                Unreserve(prep.reserved);
                data.preparations.Remove(prep);
            }
            occasion.outcome = ObservanceOutcome.Missed;
            occasion.text = lost != null ? $"{o.name} could not be kept on {ObservanceCalendar.DateText(keepOn)}: {lost}." : $"{o.name} passed unkept on {ObservanceCalendar.DateText(keepOn)}.";
            ResolveObservancePerformance(o, occasion, false);
            ObservanceRules.Resolve(data, o, original, occasion, ObservanceTuning);
            ObservanceDecided?.Invoke(View(o), occasion);
            return;
        }
        if (o.legacy && !o.customized)
        {
            var h = _state.holidays.FirstOrDefault(x => x != null && string.Equals(x.name, o.holiday, StringComparison.OrdinalIgnoreCase));
            if (h == null) { o.ended = true; return; }
            var now = Now;
            if (h.lastKept != CultureCalendar.EchoKey(now.cycle, now.echo)) KeepHoliday(h, now.cycle, now.echo);
            occasion.outcome = ObservanceOutcome.Kept;
            occasion.text = $"{h.name} kept.";
            ResolveObservancePerformance(o, occasion, true);
            ObservanceRules.Resolve(data, o, original, occasion, ObservanceTuning);
            ObservanceDecided?.Invoke(View(o), occasion);
            return;
        }
        KeepPlanned(o, prep, occasion);
        ResolveObservancePerformance(o, occasion, true);
        ObservanceRules.Resolve(data, o, original, occasion, ObservanceTuning);
        ObservanceDecided?.Invoke(View(o), occasion);
    }

    // A place-bound observance (a vigil in its grove) is kept only while its settlement stands on fitting ground.
    private string WhyNotVenueNow(Observance o)
    {
        var d = ObservanceDefinitionOf(o?.definition);
        if (d == null || d.grounds == null || d.grounds.Count == 0) return null;
        var s = SettlementById(o.settlement);
        if (s == null) return "its place no longer stands";
        return d.FitsGround(WorldSystem.Instance.SettlementGround(s)) ? null : $"it needs {d.groundText ?? "another ground"}";
    }

    // One occasion kept by its plan: the table (prepared, or revalidated now, or none), the rewards of its purpose, and
    // one occurrence per purpose (the day, its table, its rite; a remembrance keeps its memory through Memorials).
    private void KeepPlanned(Observance o, ObservancePreparation prep, ObservanceOccasion occasion)
    {
        var d = ObservanceDefinitionOf(o.definition);
        var t = ObservanceTuning;
        var scale = o.scale;
        List<ResourceAmount> served = null;
        bool genericTable = false;
        var keepCost = d?.keepCost?.Where(c => c != null && c.amount > 0f).ToList() ?? new List<ResourceAmount>();
        if (scale != ObservanceScale.Quiet && WhyNotPay(keepCost) != null && prep == null) scale = ObservanceScale.Quiet;
        if (prep != null)
        {
            served = prep.reserved;
            scale = prep.scale;
            ObservanceData.preparations.Remove(prep);
        }
        else if (scale != ObservanceScale.Quiet)
        {
            float value = ObservanceRules.FeastValue(t, People, scale);
            if (o.food != null && o.food.IsKnown && WhyNotServable(o.food) == null)
            {
                var reserved = Reserve(new[] { new ResourceAmount { resource = FoodResource(o.food), amount = PortionsOf(o.food, value) } });
                if (reserved.succeeded) served = reserved.paid;
                else scale = ObservanceScale.Quiet;
            }
            else if (Pantry.TrySpend(value)) { genericTable = true; occasion.paid.Add(new ResourceAmount { resource = "food value", amount = value }); }
            else scale = ObservanceScale.Quiet;
        }
        if (scale != ObservanceScale.Quiet && keepCost.Count > 0)
        {
            if (WhyNotPay(keepCost) == null) { Pay(keepCost); occasion.paid.AddRange(keepCost); }
            else scale = scale == ObservanceScale.Full ? ObservanceScale.Modest : scale;
        }
        if (served != null)
        {
            ServeReserved(served);
            occasion.paid.AddRange(served);
        }
        occasion.scale = scale;
        occasion.outcome = scale < o.scale ? ObservanceOutcome.KeptSmaller : ObservanceOutcome.Kept;

        var rw = ObservanceRules.Rewards(t, o.objective, scale);
        GainUnity(rw.unity);
        Cheer(rw.morale, rw.sevenths, o.name);
        AddJoy(rw.joy);
        Lived(!string.IsNullOrEmpty(d?.family) ? d.family : Voice.voice.ToString(), rw.lived);

        string key = occasion.key;
        var now = Now;
        var subject = o.legacy ? CultureEntityRef.Of(CultureEntityKind.Holiday, o.holiday, o.name) : CultureEntityRef.Of(CultureEntityKind.Observance, o.id, o.name);
        Record(Occurrence(key, CulturalOccurrenceKind.Observance, subject, o.settlement, scale == ObservanceScale.Quiet ? 0.5f : 1f, CultureQuantityUnit.Occasions,
            $"{o.name} kept {ObservanceRules.ScaleWord(scale)}{Where(o)}"));
        if (served != null && served.Count > 0)
        {
            var food = o.food.IsKnown ? o.food : prep?.food ?? new CultureEntityRef();
            string resource = FoodResource(food) ?? served[0].resource;
            var table = Occurrence(key + ":table", CulturalOccurrenceKind.Hospitality, CultureEntityRef.Of(CultureEntityKind.Resource, resource, resource), o.settlement,
                served.Sum(s => s.amount), CultureQuantityUnit.Portions, $"{CultureServing.Text(served)} shared at {o.name}");
            if (food.kind == CultureEntityKind.Recipe) table.recipe = food.Copy();
            else { var recipe = RecipeOfDish(resource); if (recipe != null) table.recipe = recipe.Ref; }
            Record(table);
        }
        if (!string.IsNullOrEmpty(o.repertoire) && Life.Activity(o.repertoire) != null)
        {
            var rite = Life.Activity(o.repertoire);
            Record(Occurrence(key + ":rite", o.objective == ObservanceObjective.Remembrance ? CulturalOccurrenceKind.Gathering : CulturalOccurrenceKind.Performance,
                CultureEntityRef.Of(CultureEntityKind.Activity, rite.id, rite.name), o.settlement, 1f, CultureQuantityUnit.Occasions, $"{rite.name} at {o.name}"));
        }
        if (o.objective == ObservanceObjective.Remembrance && o.cause != null && o.cause.kind == CultureEntityKind.Evidence && WhyNotRemembrance(o.cause.id) == null)
            KeepRemembrance(o.cause.id);
        if (o.legacy)
        {
            var h = _state.holidays.FirstOrDefault(x => x != null && string.Equals(x.name, o.holiday, StringComparison.OrdinalIgnoreCase));
            if (h != null)
            {
                h.kept++;
                h.lastKept = CultureCalendar.EchoKey(now.cycle, now.echo);
                HolidayKept?.Invoke(h);
            }
        }
        if (o.kept == 0 && !o.legacy) Remember("observance-kept", $"The first {o.name}", $"{Capital(PeopleWord)} kept {o.name}{Where(o)} for the first time.");
        occasion.text = $"{o.name} {(occasion.outcome == ObservanceOutcome.KeptSmaller ? ObservanceRules.OutcomeWord(ObservanceOutcome.KeptSmaller) : "kept " + ObservanceRules.ScaleWord(scale))}"
            + (genericTable || served != null ? $" ({(served != null ? CultureServing.Text(served) : "a table from the stores")})" : string.Empty) + ".";
        GameLog.Event(occasion.text, Log);
        NotificationFeed.Push(o.name, $"{Capital(PeopleWord)} keep {o.name}{Where(o)} {ObservanceRules.ScaleWord(scale)}: +{rw.unity:0.#} {UnityResource}{(rw.morale != 0 ? $", {rw.morale:+0} morale" : string.Empty)}.",
            NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:kept:{o.id}:{occasion.date}");
    }

    /// <summary>After a load or at the founding: link holidays (idempotent) and settle the saved state; nothing is kept, paid or given.</summary>
    public void ReconcileObservances()
    {
        var data = ObservanceData;
        ObservanceRules.Ensure(data);
        if (_state.founded) ObservanceRules.LinkHolidays(data, _state.holidays, Stamp());
    }

    // ===== QUESTIONS =====

    private ObservanceView View(Observance o)
    {
        var d = ObservanceDefinitionOf(o.definition);
        long today = Today;
        var next = ObservanceCalendar.NextOccasion(o, today);
        var prep = ObservanceData.PreparationOf(o.id);
        bool holiday = o.legacy && !o.customized;
        return new ObservanceView
        {
            foodRef = o.food?.Copy(), causeRef = o.cause?.Copy(), venueRef = o.venue?.Copy(),
            id = o.id, definition = o.definition, name = o.name, description = d?.description, family = d?.family, canonSource = d?.canonSource, canonNote = d?.canonNote,
            canon = d?.canon ?? CanonStatus.NewGameRule, objective = o.objective, recurrence = o.recurrence, day = ObservanceCalendar.DayText(o),
            next = next.keepOn < 0 ? "none" : ObservanceCalendar.DateText(next.keepOn), nextDate = next.keepOn, seventhsUntil = next.keepOn < 0 ? -1 : (int)(next.keepOn - today),
            settlement = o.settlement, place = o.settlement >= 0 ? SettlementName(o.settlement) ?? "a settlement now gone" : "the nation",
            cause = o.causeText ?? (o.cause != null && o.cause.IsKnown ? o.cause.Display : null), repertoire = !string.IsNullOrEmpty(o.repertoire) ? Life.Activity(o.repertoire)?.name ?? o.repertoire : null,
            food = o.food != null && o.food.IsKnown ? FoodLabel(o.food) : null, scale = o.scale, legacy = o.legacy, customized = o.customized, ended = o.ended,
            prepared = prep != null, postponed = o.postponedFrom >= 0 && o.postponedFrom > o.lastResolved, kept = o.kept, keptSmaller = o.keptSmaller, missed = o.missed,
            plan = PlanText(d, o.objective, o.scale, o.food, o.repertoire, holiday),
            reserved = prep != null ? prep.reserved.Select(r => new ResourceAmount { resource = r.resource, amount = r.amount }).ToList() : new List<ResourceAmount>(),
            history = ObservanceData.history.Where(h => h.observance == o.id).Select(h => new ObservanceOccasion
            {
                key = h.key, observance = h.observance, date = h.date, cultureSeventh = h.cultureSeventh, outcome = h.outcome, scale = h.scale, text = h.text,
                paid = h.paid.Select(p => new ResourceAmount { resource = p.resource, amount = p.amount }).ToList(),
            }).ToArray(),
        };
    }

    /// <summary>Every observance on the calendar (and those ended), soonest first.</summary>
    public IReadOnlyList<ObservanceView> Observances()
    {
        ObservanceRules.Ensure(ObservanceData);
        return ObservanceData.observances.Select(View).OrderBy(v => v.ended).ThenBy(v => v.seventhsUntil < 0 ? int.MaxValue : v.seventhsUntil).ToList();
    }

    public ObservanceView ObservanceOf(string id)
    {
        var o = ObservanceData.Get(id);
        return o == null ? null : View(o);
    }

    // A condition's value for the observances (CultureSystem.Value): observances (on the calendar), observances_kept,
    // observance:<definition> (1 while one is on the calendar).
    private float ObservanceValue(string t)
    {
        var data = ObservanceData;
        switch (t)
        {
            case "observances": return data.Active.Count();
            case "observances_kept": return data.observances.Sum(o => o.kept);
        }
        if (!t.StartsWith("observance:")) return float.NaN;
        string id = t.Substring(11).Trim();
        return data.Active.Any(o => string.Equals(o.definition, id, StringComparison.OrdinalIgnoreCase)) ? 1f : 0f;
    }
}

/// <summary>The observances in the culture's phases: after a load or the founding, the calendar is settled (holidays linked). Its days are kept in the Seventh's life.</summary>
public sealed class ObservanceFeature : ICultureFeature
{
    public string Id => "observances";
    public CulturePhase Phase => CulturePhase.SourceActions;
    public int Order => 20;

    public void Seventh(CultureSeventh context) { }

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileObservances();
}
