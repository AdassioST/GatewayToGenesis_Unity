using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Living traditions (T01; rules in <see cref="TraditionRules"/>, numbers and authored traditions in
/// <see cref="TraditionTuning"/>, saved in <see cref="CultureExtensionState.traditions"/>), and the culture's shared
/// integration: the occurrence ledger (<see cref="Record"/>), the Seventh's phases (<see cref="CultureFeatures"/>), the
/// post-load reconciliation (<see cref="AfterRestore"/>) and the read-only questions (<see cref="ICultureQuery"/>).
///
/// What the people do (a rite, a festival, a batch of Ash-Loaf, a national food at the table) is recorded as a
/// committed occurrence. Repeated, it becomes a named practice where it happened, with its origin, bearers and history:
/// emerging, then practised (a custom), dormant when no one keeps it, revived when taken up again. Once it is a custom
/// the player decides: recognise it for the nation (Unity), preserve it as a local custom, or decide later. Nothing is
/// paid because a threshold was crossed.
/// </summary>
public partial class CultureSystem : ICultureQuery
{
    private readonly TraditionTuning _traditionDefaults = new TraditionTuning();
    private List<ICultureFeature> _features;
    private CultureSeventh _seventh;
    private string _traditionKey;

    public TraditionTuning TraditionTuning => Settings != null && Settings.traditions != null ? Settings.traditions : _traditionDefaults;

    /// <summary>The culture's extension envelope (made when missing: an older save).</summary>
    public CultureExtensionState Extensions => CultureMigration.Ensure(_state);

    private List<ICultureFeature> Features => _features ?? (_features = CultureFeatures.Create());

    /// <summary>A tradition happened to (emerged, became a custom, lapsed, revived), after the Seventh's lifecycle.</summary>
    public event Action<TraditionChange> TraditionChanged;

    // ===== THE OCCURRENCE LEDGER =====

    /// <summary>Now, on the game's calendar: the Seventh since the founding, the world's Cycle/Echo/Phase/Seventh and the Age.</summary>
    public CultureStamp Stamp()
    {
        var now = Now;
        return new CultureStamp
        {
            cultureSeventh = _state.sevenths, cycle = now.cycle, echo = now.echo, phase = now.phase, seventh = now.seventh,
            ageId = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null,
        };
    }

    /// <summary>A key made unique by the ledger's serial ("cook:ash-loaf:17"), for actions with nothing else unique about them.</summary>
    public string NextOccurrenceKey(string prefix) => $"{prefix}:{CultureOccurrences.NextSerial(Extensions.occurrences)}";

    /// <summary>
    /// Record a committed action (never a preview). It waits for the next Seventh, or joins the Seventh being processed
    /// when a feature records it during one. False (ignored) before the founding, without a key, or when the key was seen:
    /// one action never counts twice, whoever reports it.
    /// </summary>
    public bool Record(CulturalOccurrence o)
    {
        if (!_state.founded || o == null || string.IsNullOrEmpty(o.key)) return false;
        var ledger = Extensions.occurrences;
        if (o.stamp == null) o.stamp = Stamp();
        if (o.actors == null) o.actors = new List<string>();
        if (_seventh == null) return CultureOccurrences.Accept(ledger, o, _state.sevenths);
        if (CultureOccurrences.Seen(ledger, o.key)) return false;
        ledger.accepted.Add(new ProcessedOccurrence { key = o.key, seventh = _state.sevenths });
        ledger.recent.Add(o);
        ledger.total++;
        _seventh.Append(o);
        return true;
    }

    /// <summary><see cref="Record"/> from anywhere (false with no culture in the scene).</summary>
    public static bool Observe(CulturalOccurrence o) => Instance != null && Instance.Record(o);

    private CulturalOccurrence Occurrence(string key, CulturalOccurrenceKind kind, CultureEntityRef subject, int settlement, float quantity, CultureQuantityUnit unit, string text, IEnumerable<string> actors = null) =>
        new CulturalOccurrence
        {
            key = key, kind = kind, subject = subject ?? new CultureEntityRef(), settlement = settlement, quantity = quantity, unit = unit, text = text,
            actors = actors != null ? actors.Where(a => !string.IsNullOrEmpty(a)).ToList() : new List<string>(),
            source = CultureEntityRef.Of(CultureEntityKind.Nation, "culture"),
        };

    // ===== THE SEVENTH'S PHASES =====

    // The waiting occurrences, deduplicated, go through every feature in order: local participation, the traditions'
    // lifecycle, social effects; then the player is told.
    private void RunCulturePipeline()
    {
        var ledger = Extensions.occurrences;
        var occurrences = CultureOccurrences.Drain(ledger, _state.sevenths);
        _seventh = new CultureSeventh(this, _state, Stamp(), occurrences);
        try
        {
            CultureFeatures.Run(Features, _seventh, (feature, e) => GameLog.Error($"Culture feature '{feature.Id}' failed this Seventh: {e}", Log));
            foreach (var (title, text, key) in _seventh.Notices)
                NotificationFeed.Push(title, text, NotificationFeed.Topic.Culture, CultureWindow.Open, key);
        }
        finally
        {
            _seventh = null;
        }
    }

    /// <summary>
    /// After a save is restored (GameSnapshot.Restore, once the world, runtime resources and civics are back): the
    /// envelope is brought up to date, transient caches from before the load are dropped, and every derived effect is
    /// re-applied from saved state. No reward is granted and no story replays.
    /// </summary>
    public void AfterRestore()
    {
        _usedThisSeventh.Clear();
        _lived.Clear();
        _appliedKey = null;
        _lifeKey = null;
        _traditionKey = null;
        _seventh = null;
        CultureMigration.Upgrade(_state, TraditionTuning);
        CheckInventions();
        foreach (var feature in Features)
        {
            try { feature.Reconcile(this, CultureReconcileReason.Restored); }
            catch (Exception e) { GameLog.Error($"Culture feature '{feature.Id}' could not reconcile after the load: {e}", Log); }
        }
        if (_state.founded)
        {
            ApplyCharacter();
            ApplyLife();
        }
        RaiseChanged();
    }

    // At the founding: the envelope is made and every feature starts from it.
    private void ReconcileFeatures(CultureReconcileReason reason)
    {
        CultureMigration.Upgrade(_state, TraditionTuning);
        foreach (var feature in Features)
        {
            try { feature.Reconcile(this, reason); }
            catch (Exception e) { GameLog.Error($"Culture feature '{feature.Id}' could not reconcile ({reason}): {e}", Log); }
        }
    }

    // ===== TRADITIONS: THE LIFECYCLE'S CONSEQUENCES =====

    /// <summary>Whether a tradition can arise now (its Age reached and its technology known).</summary>
    public bool TraditionAvailable(TraditionDefinition d) =>
        d != null && GameAge.Number >= d.minAge && (string.IsNullOrEmpty(d.technology) || Researched(d.technology));

    /// <summary>Called by the lifecycle: the heritage remembers firsts, the player is told what matters, venues are found.</summary>
    public void OnTraditionChanges(CultureSeventh context, IList<TraditionChange> changes)
    {
        var tuning = TraditionTuning;
        foreach (var change in changes ?? new List<TraditionChange>())
        {
            var i = change.instance;
            var d = tuning.Definition(i?.definition);
            if (d == null) continue;
            string place = PlaceOf(i.settlement);
            string where = i.settlement >= 0 && place != null ? $" in {place}" : string.Empty;
            switch (change.kind)
            {
                case TraditionChangeKind.Emerged:
                    LinkVenues(i, d);
                    GameLog.Event($"A practice emerges{where}: {d.name} ({i.origin.text}).", Log);
                    break;
                case TraditionChangeKind.Established:
                    if (change.first) Remember("tradition", d.name, $"{Capital(PeopleWord)} made {d.name} a custom{where}. {TraditionRules.Explain(i, d)}");
                    break;
                case TraditionChangeKind.Lapsed:
                    if (change.first && i.Established) Remember("tradition-dormant", $"{d.name} falls quiet", $"No one kept {d.name}{where} for a long while; it is remembered.");
                    break;
                case TraditionChangeKind.Revived:
                    if (change.first) Remember("tradition-revived", $"{d.name} revived", $"{Capital(PeopleWord)} took up {d.name}{where} again.");
                    break;
            }
            var notice = TraditionLifecycle.Notice(change, d, i.settlement >= 0 ? place : null, PeopleWord);
            if (notice.HasValue) context?.Notices.Add((notice.Value.title, notice.Value.text, $"culture:tradition:{i.id}:{change.kind}:{_state.sevenths}"));
            TraditionChanged?.Invoke(change);
        }
    }

    // Landmarks of the tradition's way of living standing where it lives are its venues.
    private void LinkVenues(TraditionInstance i, TraditionDefinition d)
    {
        if (i == null || d == null || i.settlement < 0) return;
        foreach (var l in LandmarksAt(i.settlement))
        {
            var spec = Life.Landmark(l.spec);
            if (spec == null || !string.Equals(spec.family, d.family, StringComparison.OrdinalIgnoreCase)) continue;
            var venue = CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(l.settlement, l.spec), l.name);
            if (!i.venues.Any(v => v.Same(venue))) i.venues.Add(venue);
        }
    }

    private string PlaceOf(int settlement) => settlement < 0 ? null : SettlementName(settlement) ?? $"a settlement now gone ({settlement})";

    private string PlaceOf(TraditionInstance i) =>
        i.fallen ? $"{(string.IsNullOrEmpty(i.formerPlace) ? "a settlement" : i.formerPlace)}, now in ruins" : i.settlement >= 0 ? PlaceOf(i.settlement) : null;

    // ===== THE WORLD'S EVENTS =====

    private WorldSystem _world;

    // Settlement ids are reused by later settlements: a fallen settlement's traditions leave its id when it falls.
    private void HookWorld()
    {
        if (_world != null || WorldSystem.Instance == null) return;
        _world = WorldSystem.Instance;
        _world.SettlementFell += OnSettlementFell;
    }

    private void UnhookWorld()
    {
        if (_world != null) _world.SettlementFell -= OnSettlementFell;
        _world = null;
    }

    private void OnSettlementFell(Ruin ruin)
    {
        if (ruin == null) return;
        int moved = TraditionRules.SettlementFell(Extensions.traditions, ruin.settlement, ruin.name, ruin.id);
        if (moved == 0) return;
        GameLog.Event($"{moved} tradition{(moved == 1 ? "" : "s")} of {ruin.name} are remembered in its ruins.", Log);
        RaiseChanged();
    }

    /// <summary>Re-apply the traditions' benefits from saved state (after a load; nothing is replayed).</summary>
    public void ReconcileTraditions()
    {
        TraditionRules.Ensure(Extensions.traditions);
        _traditionKey = null;
        if (_state.founded) ApplyTraditionEffects(false);
    }

    /// <summary>
    /// The traditions' benefits: one source per definition (<see cref="CultureEffectPolicy"/>), applied while it is lived
    /// and within the cap, removed when it lies dormant. <paramref name="lean"/>: also lean this Seventh's drift toward the
    /// lived traditions' ways of living.
    /// </summary>
    public void ApplyTraditionEffects(bool lean)
    {
        var tuning = TraditionTuning;
        var state = Extensions.traditions;
        var benefits = TraditionRules.Benefits(state, tuning);
        var sources = tuning.definitions.Where(d => d != null && !string.IsNullOrEmpty(d.id)).Select(d =>
        {
            var b = benefits.FirstOrDefault(x => x.definition == d);
            return (CultureEffectPolicy.Source(d.name), b != null ? b.Effects.Select(e => e.ToEffect()).ToList() : new List<GameEffect>());
        });
        _traditionKey = CultureEffectPolicy.ApplyAll(_traditionKey, sources);
        if (!lean) return;
        foreach (var pair in TraditionRules.LivedFamilies(state, tuning, FamilyOfTradition)) Lived(pair.Key.ToString(), pair.Value);
    }

    // The way of living a tradition leans toward: its definition's, or for a national food, the food's.
    private string FamilyOfTradition(TraditionInstance i)
    {
        var d = TraditionTuning.Definition(i.definition);
        if (d != null && !string.IsNullOrEmpty(d.family)) return d.family;
        if (i.subject != null && i.subject.kind == CultureEntityKind.Resource)
        {
            var invented = InventedFood(i.subject.id);
            if (invented != null) return invented.leanings.Where(l => l != null).OrderByDescending(l => l.share).Select(l => l.family.ToString()).FirstOrDefault();
            return Tuning.FamilyOfResource(i.subject.id)?.ToString();
        }
        return null;
    }

    // ===== TRADITIONS: THE PLAYER'S CHOICES =====

    private (TraditionInstance instance, TraditionDefinition definition) TraditionById(string id)
    {
        var i = TraditionRules.Get(Extensions.traditions, id);
        return (i, i != null ? TraditionTuning.Definition(i.definition) : null);
    }

    private string WhyNotRecognizeTradition(TraditionInstance i, TraditionDefinition d)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        string why = TraditionRules.WhyNotRecognize(Extensions.traditions, i, d, TraditionTuning);
        if (why != null) return why;
        return d.recognitionUnity > 0f && !Has(UnityResource, d.recognitionUnity) ? $"Needs {d.recognitionUnity:0} Unity ({UnityHeld:0} held)." : null;
    }

    public CultureCommandResult PreviewRecognizeTradition(string id)
    {
        var (i, d) = TraditionById(id);
        string why = WhyNotRecognizeTradition(i, d);
        if (why != null) return CultureCommandResult.Fail(why);
        var r = CultureCommandResult.Ok($"{d.name} would be recognised by the nation: {BenefitText(d.recognized)} while it is kept, on top of {BenefitText(d.practiced)}.");
        if (d.recognitionUnity > 0f) r.paid.Add(new ResourceAmount { resource = UnityResource, amount = d.recognitionUnity });
        return r;
    }

    /// <summary>The nation recognises a practised tradition: its Unity is paid now (and only now), its recognised benefits apply while it is kept.</summary>
    public CultureCommandResult RecognizeTradition(string id)
    {
        var preview = PreviewRecognizeTradition(id);
        if (!preview.succeeded) { GameLog.Event("Recognition refused: " + preview.reason, Log); return preview; }
        var (i, d) = TraditionById(id);
        Pay(preview.paid);
        bool first = TraditionRules.Recognize(i, _state.sevenths);
        var result = CultureCommandResult.Ok($"{d.name} is recognised by the nation.");
        result.paid.AddRange(preview.paid);
        string key = $"tradition:recognized:{i.id}";
        if (Record(Occurrence(key, CulturalOccurrenceKind.Recognition, CultureEntityRef.Of(CultureEntityKind.Tradition, i.id, d.name), i.settlement, 1f, CultureQuantityUnit.Occasions, $"the nation recognised {d.name}")))
            result.occurrences.Add(key);
        if (first) Remember("tradition-recognized", $"{d.name}, recognised", $"The nation took {d.name}{Where(i)} as its own. {TraditionRules.Explain(i, d)}");
        GameLog.Event($"{d.name} recognised ({preview.PaidText}).", Log);
        NotificationFeed.Push($"{d.name}, recognised", $"The nation keeps {d.name} as its own: {BenefitText(d.recognized)}.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:tradition:recognized:" + i.id);
        ApplyTraditionEffects(false);
        RaiseChanged();
        return result;
    }

    public CultureCommandResult PreviewPreserveTradition(string id)
    {
        var (i, d) = TraditionById(id);
        string why = !_state.founded ? "The culture has not been founded yet." : TraditionRules.WhyNotPreserve(i, d, TraditionTuning);
        if (why != null) return CultureCommandResult.Fail(why);
        return CultureCommandResult.Ok($"{d.name} would be kept as a local custom{Where(i)}: it lapses {TraditionTuning.preservedLapseFactor:0.#}x more slowly ({TraditionRules.LapseAfter(i, TraditionTuning)} to {Mathf.RoundToInt(TraditionTuning.lapseSevenths * Mathf.Max(1f, TraditionTuning.preservedLapseFactor))} Sevenths), and the nation takes nothing from it. Free.");
    }

    /// <summary>Keep a practised tradition as a local custom (free): it lapses more slowly and stays its place's own.</summary>
    public CultureCommandResult PreserveTradition(string id)
    {
        var preview = PreviewPreserveTradition(id);
        if (!preview.succeeded) return preview;
        var (i, d) = TraditionById(id);
        bool first = TraditionRules.Preserve(i, _state.sevenths);
        if (first) Remember("tradition-preserved", $"{d.name}, kept{Where(i)}", $"{d.name} was kept as a local custom{Where(i)}.");
        GameLog.Event($"{d.name} preserved as a local custom{Where(i)}.", Log);
        RaiseChanged();
        return CultureCommandResult.Ok($"{d.name} is kept as a local custom{Where(i)}.");
    }

    public CultureCommandResult PreviewDeferTradition(string id)
    {
        var (i, d) = TraditionById(id);
        string why = TraditionRules.WhyNotDefer(i);
        if (why != null || d == null) return CultureCommandResult.Fail(why ?? "No such tradition.");
        return CultureCommandResult.Ok($"The decision about {d.name} waits {TraditionTuning.deferSevenths} Sevenths; the custom is kept meanwhile and nothing is spent.");
    }

    /// <summary>Put off the decision: it is offered again after <see cref="TraditionTuning.deferSevenths"/>.</summary>
    public CultureCommandResult DeferTradition(string id)
    {
        var preview = PreviewDeferTradition(id);
        if (!preview.succeeded) return preview;
        var (i, _) = TraditionById(id);
        TraditionRules.Defer(i, _state.sevenths, TraditionTuning);
        RaiseChanged();
        return preview;
    }

    private string Where(TraditionInstance i) => i != null && i.settlement >= 0 ? $" in {PlaceOf(i.settlement)}" : string.Empty;

    private static string BenefitText(IEnumerable<TraditionEffect> effects)
    {
        var list = (effects ?? Enumerable.Empty<TraditionEffect>()).Where(e => e != null).Select(e => e.ToEffect().Describe()).ToList();
        return list.Count == 0 ? "no benefit of its own" : string.Join(", ", list);
    }

    // ===== ADAPTERS: WHAT THE CULTURE ALREADY DOES, AS OCCURRENCES =====

    // A national food is kept at the table this Seventh (held or eaten): one Consumption for the national table, never more.
    private void RecordTable(List<DietEntry> diet)
    {
        foreach (var way in _state.foodways.Where(f => f != null && f.national))
        {
            var found = diet.Where(d => string.Equals(d.resource, way.resource, StringComparison.OrdinalIgnoreCase)).ToList();
            if (found.Count == 0 || (found[0].held <= 0f && found[0].used <= 0f)) continue;
            var entry = found[0];
            Record(Occurrence($"table:{way.resource}:{_state.sevenths}", CulturalOccurrenceKind.Consumption, CultureEntityRef.Of(CultureEntityKind.Resource, way.resource, way.resource),
                -1, entry.used, CultureQuantityUnit.FoodValue, $"{way.resource} at the table"));
        }
    }

    // A national food embraced: the choice is recorded and the food gets its tradition record.
    private void RecordNationalFood(Foodway way)
    {
        string key = "national:" + way.resource;
        var subject = CultureEntityRef.Of(CultureEntityKind.Resource, way.resource, way.resource);
        string text = $"{way.resource} embraced as the {CultureRules.NationalLabel(way.cuisine)}";
        Record(Occurrence(key, CulturalOccurrenceKind.Recognition, subject, -1, 1f, CultureQuantityUnit.Occasions, text));
        TraditionRules.LinkNationalFood(Extensions.traditions, way, new TraditionOrigin { key = key, kind = CulturalOccurrenceKind.Recognition, subject = subject, stamp = Stamp(), text = text }, _state.sevenths);
    }

    private void RecordRite(CultureActivitySpec a, ActivityRecord record)
    {
        var subject = CultureEntityRef.Of(CultureEntityKind.Activity, a.id, a.name);
        Record(Occurrence($"rite:{a.id}:{record.count}", CulturalOccurrenceKind.Gathering, subject, -1, 1f, CultureQuantityUnit.Occasions, $"{a.name}, held by {PeopleWord}"));
    }

    private void RecordFestival(Settlement s, FestivalRecord record, IList<string> legends, string festival)
    {
        var led = legends != null && legends.Count > 0 ? $", led by {string.Join(", ", legends)}" : string.Empty;
        Record(Occurrence($"festival:{s.id}:{record.count}", CulturalOccurrenceKind.Festival, CultureEntityRef.Of(CultureEntityKind.Settlement, CultureIds.Settlement(s.id), s.name),
            s.id, 1f, CultureQuantityUnit.Occasions, $"{festival} in {s.name}{led}", legends));
    }

    private void RecordCooking(RecipeSpec recipe, int batches)
    {
        var r = CultureEntityRef.Of(CultureEntityKind.Recipe, recipe.id, recipe.dish);
        var o = Occurrence(NextOccurrenceKey("cook:" + recipe.id), CulturalOccurrenceKind.Production, r, -1, batches, CultureQuantityUnit.Batches,
            $"{batches} batch{(batches == 1 ? "" : "es")} of {recipe.dish} {recipe.Done.ToLowerInvariant()}");
        o.recipe = r.Copy();
        o.source = CultureEntityRef.Of(CultureEntityKind.Nation, recipe.InCellar ? "cellar" : "kitchen");
        Record(o);
    }

    private void RecordHoliday(Holiday h, int cycle, int echo, bool table)
    {
        Record(Occurrence($"holiday:{h.name}:{CultureCalendar.EchoKey(cycle, echo)}", CulturalOccurrenceKind.Observance, CultureEntityRef.Of(CultureEntityKind.Holiday, h.name, h.name),
            -1, table ? 1f : 0.5f, CultureQuantityUnit.Occasions, $"{h.name} kept{(table ? string.Empty : " quietly")}"));
    }

    private void RecordLandmark(Settlement s, Landmark landmark, LandmarkSpec spec)
    {
        var venue = CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(s.id, spec.id), landmark.name);
        Record(Occurrence($"landmark:{venue.id}", CulturalOccurrenceKind.Venue, venue, s.id, 1f, CultureQuantityUnit.Occasions, $"{landmark.name} raised in {s.name}"));
        foreach (var i in Extensions.traditions.instances.Where(i => i.settlement == s.id)) LinkVenues(i, TraditionTuning.Definition(i.definition));
    }

    // ===== TRADITIONS: QUESTIONS =====

    private TraditionView View(TraditionInstance i, List<TraditionBenefit> benefits)
    {
        var d = TraditionTuning.Definition(i.definition);
        var b = benefits.FirstOrDefault(x => x.through.Contains(i));
        string name = d?.name ?? i.definition;
        if (d != null && d.perSubject && i.subject.IsKnown) name = $"{name}: {i.subject.Display}";
        var effects = d == null ? new List<string>() : (d.practiced ?? new List<TraditionEffect>()).Select(e => e.ToEffect().Describe() + " (while kept)")
            .Concat((d.recognized ?? new List<TraditionEffect>()).Select(e => e.ToEffect().Describe() + " (once recognised)")).ToList();
        return new TraditionView
        {
            id = i.id, definition = i.definition, name = name, description = d?.description, family = d?.family, canonSource = d?.canonSource,
            food = d != null && d.food, local = d != null && d.local, canon = d?.canon ?? CanonStatus.NewGameRule,
            settlement = i.settlement, place = i.settlement >= 0 ? PlaceOf(i.settlement) : "the nation", subject = i.subject.Copy(),
            stage = i.stage, recognition = i.recognition, stageSince = i.stageSince, lastPracticed = i.lastPracticed, participations = i.participations,
            distinctSevenths = i.distinctSevenths, revivals = i.revivals, momentum = i.momentum, established = i.Established,
            benefitActive = b != null && !b.capped, benefitCapped = b != null && b.capped, recognizedNationally = b != null && b.recognized,
            origin = i.origin.text, legacy = i.origin.legacy, explanation = TraditionRules.Explain(i, d), progress = TraditionRules.Progress(i, TraditionTuning),
            bearers = i.bearers.ToArray(), venues = i.venues.Select(v => v.Copy()).ToArray(), links = i.links.Select(v => v.Copy()).ToArray(),
            history = i.history.Select(h => new TraditionParticipation
            {
                key = h.key, kind = h.kind, subject = h.subject?.Copy(), seventh = h.seventh, settlement = h.settlement, credit = h.credit,
                actors = new List<string>(h.actors ?? new List<string>()), text = h.text,
            }).ToArray(),
            benefits = effects.ToArray(), recognitionCost = d?.recognitionUnity ?? 0f,
            whyNotRecognize = WhyNotRecognizeTradition(i, d), whyNotPreserve = TraditionRules.WhyNotPreserve(i, d, TraditionTuning), whyNotDefer = TraditionRules.WhyNotDefer(i),
        };
    }

    public IReadOnlyList<TraditionView> Traditions()
    {
        var benefits = TraditionRules.Benefits(Extensions.traditions, TraditionTuning);
        return Extensions.traditions.instances.Select(i => View(i, benefits)).ToList();
    }

    public TraditionView Tradition(string id)
    {
        var i = TraditionRules.Get(Extensions.traditions, id);
        return i == null ? null : View(i, TraditionRules.Benefits(Extensions.traditions, TraditionTuning));
    }

    public IReadOnlyList<TraditionView> TraditionsAt(int settlement) => Traditions().Where(t => t.settlement == settlement).ToList();

    public IReadOnlyList<TraditionView> PendingTraditionChoices()
    {
        var benefits = TraditionRules.Benefits(Extensions.traditions, TraditionTuning);
        return TraditionRules.Waiting(Extensions.traditions).Select(i => View(i, benefits)).ToList();
    }

    public IReadOnlyList<CulturalOccurrence> RecentOccurrences(int max = 20) =>
        Extensions.occurrences.recent.Skip(Math.Max(0, Extensions.occurrences.recent.Count - Math.Max(0, max))).Select(o => o.Copy()).ToList();

    public IReadOnlyList<CulturalOccurrence> PendingOccurrences() => Extensions.occurrences.pending.Select(o => o.Copy()).ToList();

    // A condition's value for the traditions (CultureSystem.Value): traditions (lived), traditions_recognized,
    // traditions_dormant, tradition_choices, tradition:<definition> (0 none, 1 emerging, 2 dormant, 3 lived).
    private float TraditionValue(string t)
    {
        var instances = Extensions.traditions.instances;
        switch (t)
        {
            case "traditions": return instances.Count(i => i.Lived);
            case "traditions_recognized": return instances.Count(i => i.recognition == TraditionRecognition.Recognized);
            case "traditions_dormant": return instances.Count(i => i.stage == TraditionStage.Dormant);
            case "tradition_choices": return TraditionRules.Waiting(Extensions.traditions).Count();
        }
        if (!t.StartsWith("tradition:")) return float.NaN;
        string id = t.Substring(10).Trim();
        var of = instances.Where(i => string.Equals(i.definition, id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (of.Count == 0) return 0f;
        if (of.Any(i => i.Lived)) return 3f;
        return of.Any(i => i.stage == TraditionStage.Dormant) ? 2f : 1f;
    }
}
