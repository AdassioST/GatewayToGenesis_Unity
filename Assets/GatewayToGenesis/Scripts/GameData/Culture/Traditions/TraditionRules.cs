using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A tradition whose benefits apply now, and whether through recognition.</summary>
public sealed class TraditionBenefit
{
    public TraditionDefinition definition;
    public bool recognized;
    /// <summary>Its strongest living instance's momentum (the cap keeps the strongest).</summary>
    public float strength;
    /// <summary>Lived, but past the cap: its benefits wait until a stronger tradition fades.</summary>
    public bool capped;
    public List<TraditionInstance> through = new List<TraditionInstance>();

    public IEnumerable<TraditionEffect> Effects =>
        capped || definition == null ? Enumerable.Empty<TraditionEffect>()
            : (definition.practiced ?? new List<TraditionEffect>()).Concat(recognized ? definition.recognized ?? new List<TraditionEffect>() : new List<TraditionEffect>()).Where(e => e != null);
}

/// <summary>
/// The traditions' rules, pure (no scene): which occurrences feed which tradition, how a practice emerges, becomes a
/// custom, lies dormant and is revived, what the player may decide about it, which benefits apply under the cap, and
/// how a national food is linked to its record. Deterministic: the same occurrences in the same order give the same
/// state, before or after a save.
/// </summary>
public static class TraditionRules
{
    public const string NationalTable = "national-table";
    /// <summary>The settlement of a tradition whose place fell (never a real id, never the nation's -1).</summary>
    public const int FallenPlace = -2;
    public const string EmergedMilestone = "emerged", PracticedMilestone = "practiced", RecognizedMilestone = "recognized",
        PreservedMilestone = "preserved", RevivedMilestone = "revived", DormantMilestone = "dormant";

    public static void Ensure(TraditionState state)
    {
        if (state == null) return;
        if (state.instances == null) state.instances = new List<TraditionInstance>();
        state.instances.RemoveAll(i => i == null);
        if (state.nextId < 1) state.nextId = 1;
        foreach (var i in state.instances)
        {
            if (i.subject == null) i.subject = new CultureEntityRef();
            if (i.origin == null) i.origin = new TraditionOrigin();
            if (i.origin.subject == null) i.origin.subject = new CultureEntityRef();
            if (i.origin.actors == null) i.origin.actors = new List<string>();
            if (i.origin.stamp == null) i.origin.stamp = new CultureStamp();
            if (i.history == null) i.history = new List<TraditionParticipation>();
            if (i.tallies == null) i.tallies = new List<TraditionTally>();
            if (i.bearers == null) i.bearers = new List<string>();
            if (i.venues == null) i.venues = new List<CultureEntityRef>();
            if (i.links == null) i.links = new List<CultureEntityRef>();
            if (i.milestones == null) i.milestones = new List<string>();
            // Ids already given are never given again, whatever the saved counter says.
            if (TryNumber(i.id, out int n) && n >= state.nextId) state.nextId = n + 1;
        }
    }

    private static bool TryNumber(string id, out int n)
    {
        n = 0;
        return !string.IsNullOrEmpty(id) && id.StartsWith("trad-", StringComparison.Ordinal) && int.TryParse(id.Substring(5), out n);
    }

    // ===== WHAT FEEDS A TRADITION =====

    public static bool Matches(TraditionTrigger t, CulturalOccurrence o)
    {
        if (t == null || o == null || t.kind != o.kind) return false;
        if (t.subjectKind == CultureEntityKind.None) return true;
        bool Fits(CultureEntityRef r) => r != null && r.kind == t.subjectKind && (string.IsNullOrEmpty(t.subjectId) || string.Equals(r.id, t.subjectId, StringComparison.OrdinalIgnoreCase));
        return Fits(o.subject) || Fits(o.recipe);
    }

    /// <summary>
    /// How much <paramref name="o"/> counts toward <paramref name="d"/> (0: not at all). A kitchen batch counts per batch;
    /// anything else is one occasion (a portion count is not an attendance count).
    /// </summary>
    public static float Credit(TraditionDefinition d, CulturalOccurrence o)
    {
        if (d == null || o == null || d.triggers == null) return 0f;
        float best = 0f;
        foreach (var t in d.triggers) if (Matches(t, o)) best = Math.Max(best, Math.Max(0f, t.weight));
        if (best <= 0f) return 0f;
        float times = o.kind == CulturalOccurrenceKind.Production && o.unit == CultureQuantityUnit.Batches ? Math.Max(1f, o.quantity)
            : o.unit == CultureQuantityUnit.Occasions && o.quantity > 0f ? Math.Min(1f, o.quantity) : 1f;
        return best * times;
    }

    /// <summary>Where an occurrence's tradition lives: its settlement for a local tradition when the place is known, else the nation (-1).</summary>
    public static int Locality(TraditionDefinition d, CulturalOccurrence o) => d != null && d.local && o != null && o.settlement >= 0 ? o.settlement : -1;

    public static TraditionInstance Find(TraditionState state, string definition, int settlement, CultureEntityRef subject = null) =>
        state?.instances.FirstOrDefault(i => i != null && string.Equals(i.definition, definition, StringComparison.OrdinalIgnoreCase) && i.settlement == settlement
            && (subject == null || !subject.IsKnown ? !i.subject.IsKnown : subject.Same(i.subject)));

    public static TraditionInstance Get(TraditionState state, string id) =>
        string.IsNullOrEmpty(id) ? null : state?.instances.FirstOrDefault(i => i != null && i.id == id);

    // ===== THE LIFECYCLE =====

    /// <summary>
    /// A Seventh of the traditions (<paramref name="now"/>: Sevenths since the founding): every occurrence feeds the
    /// traditions it matches (a tradition that does not exist yet emerges from it, where <paramref name="available"/>
    /// allows), each tradition takes at most <see cref="TraditionTuning.maxCreditPerSeventh"/>, then each one's stage
    /// moves on. Returns what changed, in order.
    /// </summary>
    public static List<TraditionChange> Advance(TraditionState state, IList<TraditionDefinition> definitions, IEnumerable<CulturalOccurrence> occurrences,
        int now, TraditionTuning tuning, Func<TraditionDefinition, bool> available = null)
    {
        Ensure(state);
        var changes = new List<TraditionChange>();
        var credit = new Dictionary<TraditionInstance, float>();
        var defs = (definitions ?? new List<TraditionDefinition>()).Where(d => d != null && !string.IsNullOrEmpty(d.id)).ToList();
        foreach (var o in occurrences ?? Enumerable.Empty<CulturalOccurrence>())
        {
            if (o == null) continue;
            foreach (var d in defs)
            {
                float c = Credit(d, o);
                if (c <= 0f) continue;
                int place = Locality(d, o);
                var subject = d.perSubject ? o.subject : null;
                if (d.perSubject && (subject == null || !subject.IsKnown)) continue;
                var instance = Find(state, d.id, place, subject);
                // Blended into a new form here (T07): its practice here now feeds that form, never itself again.
                if (instance != null && !string.IsNullOrEmpty(instance.mergedInto)) continue;
                if (instance == null)
                {
                    if (d.linkedOnly || (available != null && !available(d))) continue;
                    instance = Create(state, d, place, subject, OriginOf(o), now);
                    changes.Add(new TraditionChange(TraditionChangeKind.Emerged, instance, Mark(instance, EmergedMilestone)));
                }
                Participate(instance, o, c, now, tuning);
                credit.TryGetValue(instance, out float was);
                credit[instance] = was + c;
            }
        }
        foreach (var instance in state.instances)
        {
            credit.TryGetValue(instance, out float c);
            Step(instance, Math.Min(Math.Max(0f, tuning.maxCreditPerSeventh), c), now, tuning, changes);
        }
        return changes;
    }

    private static TraditionOrigin OriginOf(CulturalOccurrence o) => new TraditionOrigin
    {
        key = o.key, kind = o.kind, subject = o.subject?.Copy() ?? new CultureEntityRef(), settlement = o.settlement,
        actors = o.actors != null ? new List<string>(o.actors) : new List<string>(), stamp = o.stamp?.Copy() ?? new CultureStamp(), text = o.text,
    };

    public static TraditionInstance Create(TraditionState state, TraditionDefinition d, int settlement, CultureEntityRef subject, TraditionOrigin origin, int now)
    {
        var instance = new TraditionInstance
        {
            id = "trad-" + state.nextId++, definition = d.id, settlement = settlement, subject = subject?.Copy() ?? new CultureEntityRef(),
            origin = origin ?? new TraditionOrigin(), stage = TraditionStage.Emerging, stageSince = now, lastPracticed = now,
        };
        if (instance.subject.IsKnown) instance.links.Add(instance.subject.Copy());
        state.instances.Add(instance);
        return instance;
    }

    // One occasion: its history (bounded), its tally, the legends who carried it.
    private static void Participate(TraditionInstance i, CulturalOccurrence o, float credit, int now, TraditionTuning tuning)
    {
        i.participations++;
        i.history.Add(new TraditionParticipation
        {
            key = o.key, kind = o.kind, subject = o.subject?.Copy() ?? new CultureEntityRef(), seventh = o.stamp != null ? o.stamp.cultureSeventh : now,
            settlement = o.settlement, credit = credit, actors = o.actors != null ? new List<string>(o.actors) : new List<string>(), text = o.text,
        });
        int keep = Math.Max(1, tuning.historyKept);
        if (i.history.Count > keep) i.history.RemoveRange(0, i.history.Count - keep);
        var tally = i.tallies.FirstOrDefault(t => t.kind == o.kind);
        if (tally == null) i.tallies.Add(tally = new TraditionTally { kind = o.kind });
        tally.count++;
        tally.credit += credit;
        foreach (var actor in o.actors ?? new List<string>())
        {
            if (string.IsNullOrEmpty(actor)) continue;
            i.bearers.Remove(actor);
            i.bearers.Add(actor);
        }
        int bearers = Math.Max(0, tuning.bearersKept);
        if (i.bearers.Count > bearers) i.bearers.RemoveRange(0, i.bearers.Count - bearers);
        if (o.kind == CulturalOccurrenceKind.Venue && o.subject != null && o.subject.IsKnown && !i.venues.Any(v => v.Same(o.subject))) i.venues.Add(o.subject.Copy());
    }

    // The stage moves on with this Seventh's credit.
    private static void Step(TraditionInstance i, float credit, int now, TraditionTuning tuning, List<TraditionChange> changes)
    {
        i.momentum = i.momentum * (1f - Clamp01(tuning.momentumDecay)) + credit;
        bool practised = credit > 0f;
        if (practised)
        {
            if (i.lastPracticed != now || i.distinctSevenths == 0) i.distinctSevenths++;
            i.lastPracticed = now;
        }
        switch (i.stage)
        {
            case TraditionStage.Emerging:
                if (i.distinctSevenths >= Math.Max(1, tuning.establishSevenths) && i.participations >= Math.Max(1, tuning.establishOccasions))
                {
                    Enter(i, TraditionStage.Practiced, now);
                    bool first = Mark(i, PracticedMilestone);
                    changes.Add(new TraditionChange(TraditionChangeKind.Established, i, first));
                    if (i.recognition == TraditionRecognition.None)
                    {
                        i.recognition = TraditionRecognition.Offered;
                        changes.Add(new TraditionChange(TraditionChangeKind.Offered, i, true));
                    }
                }
                else Lapse(i, now, tuning, changes);
                break;
            case TraditionStage.Practiced:
            case TraditionStage.Revived:
                if (i.recognition == TraditionRecognition.Deferred && now >= i.deferredUntil)
                {
                    i.recognition = TraditionRecognition.Offered;
                    changes.Add(new TraditionChange(TraditionChangeKind.Offered, i, false));
                }
                Lapse(i, now, tuning, changes);
                break;
            case TraditionStage.Dormant:
                if (practised) i.revivalProgress++;
                if (i.revivalProgress >= Math.Max(1, tuning.reviveSevenths))
                {
                    i.revivalProgress = 0;
                    if (i.Established)
                    {
                        Enter(i, TraditionStage.Revived, now);
                        i.revivals++;
                        changes.Add(new TraditionChange(TraditionChangeKind.Revived, i, Mark(i, RevivedMilestone)));
                    }
                    else
                    {
                        Enter(i, TraditionStage.Emerging, now);
                        changes.Add(new TraditionChange(TraditionChangeKind.Reemerging, i, false));
                    }
                }
                break;
        }
    }

    private static void Lapse(TraditionInstance i, int now, TraditionTuning tuning, List<TraditionChange> changes)
    {
        if (now - i.lastPracticed <= LapseAfter(i, tuning)) return;
        Enter(i, TraditionStage.Dormant, now);
        i.revivalProgress = 0;
        changes.Add(new TraditionChange(TraditionChangeKind.Lapsed, i, Mark(i, DormantMilestone)));
    }

    /// <summary>Sevenths without practice before it lies dormant (longer when preserved locally).</summary>
    public static int LapseAfter(TraditionInstance i, TraditionTuning tuning)
    {
        float factor = i != null && i.recognition == TraditionRecognition.Preserved ? Math.Max(1f, tuning.preservedLapseFactor) : 1f;
        return (int)Math.Round(Math.Max(1, tuning.lapseSevenths) * factor);
    }

    private static void Enter(TraditionInstance i, TraditionStage stage, int now)
    {
        i.stage = stage;
        i.stageSince = now;
    }

    /// <summary>Record a milestone; true the first time (the heritage remembers it once).</summary>
    public static bool Mark(TraditionInstance i, string milestone)
    {
        if (i == null || string.IsNullOrEmpty(milestone) || i.milestones.Contains(milestone)) return false;
        i.milestones.Add(milestone);
        return true;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>
    /// A settlement fell: its traditions leave its id (settlement ids are reused by later settlements) and keep its name
    /// and ruin. They are no longer practised there, so they lapse in time; their history stays. Returns how many.
    /// </summary>
    public static int SettlementFell(TraditionState state, int settlement, string name, int ruin)
    {
        if (state == null || settlement < 0) return 0;
        int moved = 0;
        foreach (var i in state.instances.Where(i => i != null && i.settlement == settlement))
        {
            i.fallen = true;
            i.formerSettlement = settlement;
            i.formerPlace = name;
            i.ruin = ruin;
            i.settlement = FallenPlace;
            moved++;
        }
        return moved;
    }

    // ===== BENEFITS =====

    /// <summary>
    /// The traditions whose benefits apply: one entry per definition lived anywhere (never one per settlement, so a
    /// custom kept in five towns does not give five times), recognised when a living instance is; the strongest
    /// <see cref="TraditionTuning.maxActiveBenefits"/> apply, the rest are marked capped. Dormant ones give nothing.
    /// </summary>
    public static List<TraditionBenefit> Benefits(TraditionState state, TraditionTuning tuning)
    {
        var list = new List<TraditionBenefit>();
        if (state == null || tuning == null) return list;
        foreach (var group in state.instances.Where(i => i != null && i.Lived).GroupBy(i => i.definition, StringComparer.OrdinalIgnoreCase))
        {
            var d = tuning.Definition(group.Key);
            if (d == null || ((d.practiced == null || d.practiced.Count == 0) && (d.recognized == null || d.recognized.Count == 0))) continue;
            list.Add(new TraditionBenefit
            {
                definition = d, recognized = group.Any(i => i.recognition == TraditionRecognition.Recognized),
                strength = group.Max(i => i.momentum), through = group.ToList(),
            });
        }
        list = list.OrderByDescending(b => b.recognized).ThenByDescending(b => b.strength).ThenBy(b => b.definition.id, StringComparer.Ordinal).ToList();
        for (int k = Math.Max(0, tuning.maxActiveBenefits); k < list.Count; k++) list[k].capped = true;
        return list;
    }

    /// <summary>The ways of living the lived traditions lean the culture toward: one share per definition lived (the aggregate stays a description).</summary>
    public static Dictionary<EnclaveFamily, float> LivedFamilies(TraditionState state, TraditionTuning tuning, Func<TraditionInstance, string> familyOf = null)
    {
        var map = new Dictionary<EnclaveFamily, float>();
        if (state == null || tuning == null) return map;
        foreach (var group in state.instances.Where(i => i != null && i.Lived).GroupBy(i => i.definition, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            string family = familyOf != null ? familyOf(first) : tuning.Definition(group.Key)?.family;
            if (!CultureRules.TryFamily(family, out var f)) continue;
            map.TryGetValue(f, out float was);
            map[f] = was + Math.Max(0f, tuning.livedShare);
        }
        return map;
    }

    // ===== THE PLAYER'S CHOICES =====

    /// <summary>Traditions waiting for the player's decision (lived and offered).</summary>
    public static IEnumerable<TraditionInstance> Waiting(TraditionState state) =>
        state?.instances.Where(i => i != null && i.Lived && i.recognition == TraditionRecognition.Offered) ?? Enumerable.Empty<TraditionInstance>();

    /// <summary>"practised in 2 of the 3 Sevenths needed, 2 of 3 occasions".</summary>
    public static string Progress(TraditionInstance i, TraditionTuning tuning) =>
        $"practised in {Math.Min(i.distinctSevenths, tuning.establishSevenths)} of the {tuning.establishSevenths} Sevenths needed, {Math.Min(i.participations, tuning.establishOccasions)} of {tuning.establishOccasions} occasions";

    /// <summary>Why the nation cannot recognise this tradition now (Unity aside), or null.</summary>
    public static string WhyNotRecognize(TraditionState state, TraditionInstance i, TraditionDefinition d, TraditionTuning tuning)
    {
        if (i == null || d == null) return "No such tradition.";
        if (d.linkedOnly) return $"{d.name} is recognised by the choice that made it (a national food embraced).";
        if (i.recognition == TraditionRecognition.Recognized) return $"{d.name} is already recognised.";
        if (i.stage == TraditionStage.Emerging) return $"Not yet a custom: {Progress(i, tuning)}.";
        if (i.stage == TraditionStage.Dormant) return $"{d.name} lies dormant: a custom no one keeps cannot be recognised until it is revived.";
        var other = state?.instances.FirstOrDefault(o => o != i && o != null && string.Equals(o.definition, i.definition, StringComparison.OrdinalIgnoreCase) && o.recognition == TraditionRecognition.Recognized);
        if (other != null) return $"The nation already recognises {d.name} (through {other.id}); preserve this one as a local custom instead.";
        return null;
    }

    /// <summary>Why this tradition cannot be preserved as a local custom now, or null.</summary>
    public static string WhyNotPreserve(TraditionInstance i, TraditionDefinition d, TraditionTuning tuning)
    {
        if (i == null || d == null) return "No such tradition.";
        if (d.linkedOnly) return $"{d.name} belongs to the nation's table.";
        if (i.recognition == TraditionRecognition.Preserved) return $"{d.name} is already preserved here.";
        if (i.recognition == TraditionRecognition.Recognized) return $"{d.name} is recognised by the nation.";
        if (!i.Established) return $"Not yet a custom: {Progress(i, tuning)}.";
        if (!i.Lived) return $"{d.name} lies dormant: revive it first.";
        return null;
    }

    /// <summary>Why the decision cannot be put off now, or null.</summary>
    public static string WhyNotDefer(TraditionInstance i) =>
        i == null ? "No such tradition." : i.recognition != TraditionRecognition.Offered ? "There is nothing waiting to be decided." : null;

    /// <summary>The nation recognises it (the caller has paid). True the first time it is recognised.</summary>
    public static bool Recognize(TraditionInstance i, int now)
    {
        i.recognition = TraditionRecognition.Recognized;
        i.recognitionSeventh = now;
        return Mark(i, RecognizedMilestone);
    }

    /// <summary>It is kept as a local custom: it lapses more slowly. True the first time.</summary>
    public static bool Preserve(TraditionInstance i, int now)
    {
        i.recognition = TraditionRecognition.Preserved;
        i.recognitionSeventh = now;
        return Mark(i, PreservedMilestone);
    }

    public static void Defer(TraditionInstance i, int now, TraditionTuning tuning)
    {
        i.recognition = TraditionRecognition.Deferred;
        i.deferredUntil = now + Math.Max(1, tuning.deferSevenths);
    }

    // ===== NATIONAL FOODS =====

    /// <summary>
    /// A national food's tradition record (made when it is embraced, or for one embraced in an older save): lived and
    /// recognised by the choice that made it national. Returns the record, already there or new; null for a food that is
    /// not national. It gives no benefit of its own: the national food's output bonus stays the national food's.
    /// </summary>
    public static TraditionInstance LinkNationalFood(TraditionState state, Foodway food, TraditionOrigin origin, int now)
    {
        if (state == null || food == null || !food.national || string.IsNullOrEmpty(food.resource)) return null;
        Ensure(state);
        var subject = CultureEntityRef.Of(CultureEntityKind.Resource, food.resource, food.resource);
        var existing = Get(state, food.tradition) ?? Find(state, NationalTable, -1, subject);
        if (existing != null)
        {
            food.tradition = existing.id;
            return existing;
        }
        origin = origin ?? new TraditionOrigin();
        if (origin.subject == null || !origin.subject.IsKnown) origin.subject = subject.Copy();
        int since = origin.legacy ? Math.Min(now, food.nationalSeventh) : now;
        var i = new TraditionInstance
        {
            id = "trad-" + state.nextId++, definition = NationalTable, settlement = -1, subject = subject, origin = origin,
            stage = TraditionStage.Practiced, stageSince = since, recognition = TraditionRecognition.Recognized, recognitionSeventh = since, lastPracticed = now,
        };
        i.links.Add(subject.Copy());
        i.milestones.AddRange(new[] { EmergedMilestone, PracticedMilestone, RecognizedMilestone });
        state.instances.Add(i);
        food.tradition = i.id;
        return i;
    }

    /// <summary>Link every national food of an older save to its record, honestly marked as recorded before the ledger. Returns how many were made.</summary>
    public static int LinkNationalFoods(CultureState culture, TraditionState state, TraditionTuning tuning)
    {
        if (culture?.foodways == null || state == null) return 0;
        int made = 0;
        foreach (var food in culture.foodways.Where(f => f != null && f.national))
        {
            if (Get(state, food.tradition) != null) continue;
            int before = state.instances.Count;
            LinkNationalFood(state, food, new TraditionOrigin
            {
                key = "legacy:national:" + food.resource, kind = CulturalOccurrenceKind.Recognition, legacy = true,
                stamp = new CultureStamp { cultureSeventh = food.nationalSeventh, ageId = food.nationalAge },
                text = $"embraced as the {CultureRules.NationalLabel(food.cuisine)} at Seventh {food.nationalSeventh} (recorded before traditions were kept: its occasions and bearers are unknown)",
            }, culture.sevenths);
            if (state.instances.Count > before) made++;
        }
        return made;
    }

    // ===== WORDS =====

    public static string StageWord(TraditionStage stage)
    {
        switch (stage)
        {
            case TraditionStage.Emerging: return "emerging";
            case TraditionStage.Practiced: return "practised";
            case TraditionStage.Dormant: return "dormant";
            default: return "revived";
        }
    }

    public static string RecognitionWord(TraditionRecognition r)
    {
        switch (r)
        {
            case TraditionRecognition.Offered: return "awaiting your decision";
            case TraditionRecognition.Deferred: return "decision deferred";
            case TraditionRecognition.Recognized: return "recognised by the nation";
            case TraditionRecognition.Preserved: return "preserved as a local custom";
            default: return "not yet a custom";
        }
    }

    public static string KindWord(CulturalOccurrenceKind kind)
    {
        switch (kind)
        {
            case CulturalOccurrenceKind.Production: return "made";
            case CulturalOccurrenceKind.Consumption: return "at the table";
            case CulturalOccurrenceKind.Gathering: return "gatherings";
            case CulturalOccurrenceKind.Festival: return "festivals";
            case CulturalOccurrenceKind.Hospitality: return "shared tables";
            case CulturalOccurrenceKind.Memorial: return "remembrances";
            case CulturalOccurrenceKind.Observance: return "observances";
            case CulturalOccurrenceKind.Performance: return "performances";
            case CulturalOccurrenceKind.Teaching: return "teachings";
            default: return kind.ToString().ToLowerInvariant();
        }
    }

    /// <summary>"Began with a festival in Ashford, led by Vaelia (Seventh 12). 5 occasions over 4 Sevenths: 3 festivals, 2 gatherings."</summary>
    public static string Explain(TraditionInstance i, TraditionDefinition d)
    {
        if (i == null) return string.Empty;
        string origin = string.IsNullOrEmpty(i.origin?.text) ? "an occasion no one recorded" : i.origin.text;
        string when = i.origin != null && i.origin.stamp != null ? $" (Seventh {i.origin.stamp.cultureSeventh})" : string.Empty;
        string tallies = i.tallies.Count == 0 ? string.Empty : ": " + string.Join(", ", i.tallies.OrderByDescending(t => t.count).Select(t => $"{t.count} {KindWord(t.kind)}"));
        string occasions = i.participations == 0 ? (i.origin != null && i.origin.legacy ? " No occasion recorded since." : string.Empty)
            : $" {i.participations} occasion{(i.participations == 1 ? "" : "s")} over {i.distinctSevenths} Seventh{(i.distinctSevenths == 1 ? "" : "s")}{tallies}.";
        return $"Began with {origin}{when}.{occasions}";
    }
}
