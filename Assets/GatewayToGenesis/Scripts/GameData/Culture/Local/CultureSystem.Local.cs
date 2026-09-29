using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// People admitted to one of your settlements, as the population's admission reports them (PopGrowthLogic): how many,
/// and what is known of where they came from ("survivors:&lt;cell&gt;", "settlement:&lt;id&gt;"; null: unknown).
/// </summary>
public sealed class ArrivalAdmission
{
    /// <summary>Unique per admission ("admission:12").</summary>
    public string key;
    public int people;
    /// <summary>Where they came from, when known ("survivors:431", "settlement:3"); null: unknown, never guessed.</summary>
    public string origin;
    /// <summary>How they came: "founding", "survivors", "caravan", "settlers", "story".</summary>
    public string channel;
    /// <summary>They were let in at the Capital's gates (where waiting survivors are admitted).</summary>
    public bool atGates;
    /// <summary>The settlement they joined when not the gates (-1: unknown).</summary>
    public int settlement = -1;
}

/// <summary>
/// Local cultures and routes of exchange (T02; rules in <see cref="LocalCultureRules"/>, saved in
/// <see cref="CultureExtensionState.local"/>). "Our river town celebrates differently from our orchard town, and
/// travelers carry both traditions."
///
/// - Each settlement keeps its own customs (a profile). A local gathering (world map, settlement card) begins a custom
///   where the ground fits it, revives a quiet one, or lets the community take up one it has come to know.
/// - Contact is only real contact: open roads (<see cref="WorldSystem.CulturalContact"/>), a cultural party's completed
///   festival (it carries a chosen repertoire), founders, and people admitted from a known settlement. Anonymous
///   arrivals stay anonymous: they bring no assumed custom. Adoption needs exposure and participation; it never
///   moves land or loyalty. A cut road stops future contact; nothing learned is erased.
/// - Presence on the map is the culture's rootedness on held land, not agreement: the customs live here instead.
/// </summary>
public partial class CultureSystem : ICultureQuery
{
    public const string LocalGatheringSource = "local-gathering";

    /// <summary>Local cultures' numbers (code defaults: Culture.asset has no block for them; every number is a proposal).</summary>
    public static readonly LocalCultureTuning LocalDefaults = new LocalCultureTuning();
    public LocalCultureTuning LocalTuning => LocalDefaults;

    private LocalCultureState Local
    {
        get
        {
            if (_state.extensions == null) _state.extensions = new CultureExtensionState();
            return _state.extensions.local ?? (_state.extensions.local = new LocalCultureState());
        }
    }

    private static int AgeNow => GameAge.Number;
    private static string AgeIdNow => GameAge.Id;

    // ===== READS (snapshots) =====

    public IReadOnlyList<SettlementProfileView> LocalProfiles() => Local.profiles.Where(p => p != null).Select(SettlementProfileView.Of).ToList();

    /// <summary>The customs of settlements that no longer stand (history, never inherited by a newer settlement).</summary>
    public IReadOnlyList<SettlementProfileView> FormerProfiles() => (Local.former ?? new List<SettlementProfile>()).Where(p => p != null).Select(SettlementProfileView.Of).ToList();

    /// <summary>A settlement's customs (an empty profile when it has none yet).</summary>
    public SettlementProfileView LocalProfile(int settlement)
    {
        var p = LocalCultureRules.Profile(Local, settlement);
        return p != null ? SettlementProfileView.Of(p) : new SettlementProfileView { settlement = settlement, name = SettlementName(settlement), practices = new List<LocalPracticeView>() };
    }

    /// <summary>Roads between your settlements as contact sees them (open or interrupted).</summary>
    public IReadOnlyList<ContactEdgeView> ContactEdges()
    {
        var c = WorldSystem.Instance != null ? WorldSystem.Instance.CulturalContact() : ContactSnapshot.Empty;
        return c.Edges.Select(e => new ContactEdgeView { a = e.a, b = e.b, aName = c.NameOf(e.a), bName = c.NameOf(e.b), open = e.open, blockedBy = e.blockedBy }).ToList();
    }

    /// <summary>The latest arrivals recorded at a settlement (newest first).</summary>
    public IReadOnlyList<ArrivalView> ArrivalsAt(int settlement) =>
        Local.arrivals.Where(a => a != null && a.settlement == settlement).Reverse()
            .Select(a => new ArrivalView { settlement = a.settlement, people = a.people, seventh = a.seventh, originKind = a.originKind, originLabel = a.originLabel, channel = a.channel }).ToList();

    /// <summary>People admitted to a settlement all told, by what is known of their origin.</summary>
    public (int fromSettlements, int fromPlaces, int unknown) ArrivalTotals(int settlement) => LocalCultureRules.ArrivalsAt(Local, settlement);

    /// <summary>Every settlement's own form of a custom (its variants, recognised or not).</summary>
    public IReadOnlyList<(int settlement, string name, LocalPracticeView practice)> PracticeVariants(string practice) =>
        Local.profiles.Where(p => p != null).SelectMany(p => p.practices.Where(x => x != null && x.Kept && string.Equals(x.practice, practice, StringComparison.OrdinalIgnoreCase))
            .Select(x => (p.settlement, p.name, LocalPracticeView.Of(x)))).ToList();

    /// <summary>The customs a cultural party carries.</summary>
    public IReadOnlyList<CarriedPracticeView> CarriedBy(int unit) =>
        (LocalCultureRules.Repertoire(Local, unit)?.carried ?? new List<CarriedPractice>()).Where(c => c != null)
            .Select(c => new CarriedPracticeView { practice = c.practice, name = LocalPracticeCatalog.NameOf(c.practice), fromName = c.fromName, fromSettlement = c.fromSettlement }).ToList();

    // ===== LOCAL GATHERINGS =====

    private SettlementProfile ProfileFor(Settlement s) => LocalCultureRules.EnsureProfile(Local, s.id, s.name);

    /// <summary>
    /// The customs a settlement could gather for, with why not (null: it can) and what it would do: those it keeps,
    /// those it has met, and those its ground could begin. Nothing it has never met and could not begin is listed.
    /// </summary>
    public List<(LocalPracticeSpec spec, string why, string preview)> LocalGatheringChoices(Settlement s)
    {
        var list = new List<(LocalPracticeSpec, string, string)>();
        if (s == null || !_state.founded) return list;
        string ground = WorldSystem.Instance != null ? WorldSystem.Instance.SettlementGround(s) : string.Empty;
        var profile = LocalCultureRules.Profile(Local, s.id);
        foreach (var spec in LocalPracticeCatalog.All)
        {
            var p = LocalCultureRules.Practice(profile, spec.id);
            bool fits = spec.FitsGround(ground) && spec.OpenIn(AgeNow);
            if (p == null && !fits) continue;
            list.Add((spec, WhyNotLocalGathering(s, spec.id), GatheringPreview(p, spec, fits)));
        }
        return list;
    }

    private string GatheringPreview(LocalPractice p, LocalPracticeSpec spec, bool fits)
    {
        var t = LocalTuning;
        if (p == null) return $"begins {spec.name} here ({spec.groundText})";
        if (p.stage == LocalPracticeStage.Quiet) return $"revives {spec.name}";
        if (p.Kept) return $"keeps {spec.name}";
        if (fits) return $"takes up {spec.name}: the ground here knows it too";
        float part = Mathf.Min(1f, p.participation + t.gatheringParticipation);
        return p.exposure + 1e-4f >= t.adoptExposure && part + 1e-4f >= t.adoptParticipation
            ? $"takes up {spec.name}{LocalCultureRules.ProvenanceWords(p.origin)}"
            : $"tries {spec.name} (known {p.exposure:P0} of {t.adoptExposure:P0}; taking part would reach {part:P0} of {t.adoptParticipation:P0})";
    }

    /// <summary>What a local gathering costs, in words.</summary>
    public string LocalGatheringCostText => CostText(null, LocalTuning.gatheringFoodValue);

    /// <summary>Why <paramref name="s"/> cannot gather for <paramref name="practice"/> now, or null.</summary>
    public string WhyNotLocalGathering(Settlement s, string practice)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        if (s == null) return "A gathering is held in one of your settlements.";
        string ground = WorldSystem.Instance != null ? WorldSystem.Instance.SettlementGround(s) : string.Empty;
        return LocalCultureRules.WhyNotGather(LocalCultureRules.Profile(Local, s.id), practice, ground, AgeNow, _state.sevenths, LocalTuning)
            ?? Pantry.WhyNotAfford(LocalTuning.gatheringFoodValue);
    }

    /// <summary>
    /// The community of <paramref name="s"/> gathers for a custom: its shared pot is paid from the stores, a little
    /// Unity, its strain eased, and the custom begins, is revived or taken up (<see cref="LocalCultureRules.Gather"/>).
    /// Nothing is paid when it cannot be held.
    /// </summary>
    public CultureCommandResult HoldLocalGathering(Settlement s, string practice)
    {
        string why = WhyNotLocalGathering(s, practice);
        if (why != null) return CultureCommandResult.Fail(why);
        var t = LocalTuning;
        if (t.gatheringFoodValue > 0f && !Pantry.TrySpend(t.gatheringFoodValue)) return CultureCommandResult.Fail(Pantry.WhyNotAfford(t.gatheringFoodValue) ?? "The stores cannot set the pot.");
        var result = CultureCommandResult.Ok();
        if (t.gatheringFoodValue > 0f) result.paid.Add(new ResourceAmount { resource = "food value", amount = t.gatheringFoodValue });
        var spec = LocalPracticeCatalog.Find(practice);
        string ground = WorldSystem.Instance != null ? WorldSystem.Instance.SettlementGround(s) : string.Empty;
        var change = LocalCultureRules.Gather(Local, s.id, s.name, spec.id, ground, _state.sevenths, AgeIdNow, AgeNow, t);
        GainUnity(t.gatheringUnity);
        s.strain = Mathf.Max(0f, s.strain - Mathf.Max(0f, t.gatheringRelief));
        Lived(spec.family, 0.25f);
        var o = new CulturalOccurrence
        {
            key = $"local-gathering:{s.id}:{_state.sevenths}", kind = CulturalOccurrenceKind.Gathering, stamp = Stamp(),
            subject = CultureEntityRef.Of(CultureEntityKind.Activity, "local:" + spec.id, spec.name),
            source = CultureEntityRef.Of(CultureEntityKind.Activity, LocalGatheringSource, "a local gathering"),
            settlement = s.id, quantity = 1f, unit = CultureQuantityUnit.Occasions, text = change.text,
        };
        if (Record(o)) result.occurrences.Add(o.key);
        if (change.channel == PracticeChannel.Originated) Remember("local-custom", $"{spec.name} in {s.name}", change.text);
        else if (change.stage == LocalPracticeStage.Practiced && change.text != null && change.text.Contains("takes up"))
            Remember("local-custom", $"{s.name} takes up {spec.name}", change.text);
        result.reason = change.text;
        GameLog.Event($"Local gathering in {s.name}: {change.text}", Log);
        NotificationFeed.Push($"{spec.name} in {s.name}", $"{change.text} +{t.gatheringUnity:0.#} Unity, its Composure eased.", NotificationFeed.Topic.Culture, CultureWindow.Open, o.key);
        RaiseChanged();
        return result;
    }

    // ===== ARRIVALS (the population's admission boundary) =====

    /// <summary>People were admitted (PopGrowthLogic): recorded with exactly the origin reported; no-op with no culture in the scene.</summary>
    public static void Admitted(ArrivalAdmission admission) => Instance?.RecordAdmission(admission);

    public void RecordAdmission(ArrivalAdmission a)
    {
        if (a == null || a.people <= 0 || string.IsNullOrEmpty(a.key)) return;
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        int destination = a.atGates ? (map != null ? WorldCivilization.Capital(map)?.id ?? -1 : -1) : a.settlement;
        var record = new ArrivalRecord { key = a.key, settlement = destination, people = a.people, channel = a.channel, seventh = _state.sevenths, ageId = AgeIdNow };
        ReadOrigin(a.origin, record, map);
        var changes = LocalCultureRules.Arrive(Local, record, LocalTuning, _state.sevenths, AgeNow, SettlementName(destination));
        var o = new CulturalOccurrence
        {
            key = a.key, kind = CulturalOccurrenceKind.Admission, stamp = Stamp(), settlement = destination, quantity = a.people, unit = CultureQuantityUnit.Participants,
            subject = record.originKind == ArrivalOriginKind.Settlement ? CultureEntityRef.Of(CultureEntityKind.Settlement, record.originId, record.originLabel) : CultureEntityRef.None,
            text = new ArrivalView { people = a.people, originKind = record.originKind, originLabel = record.originLabel, channel = a.channel }.Text,
        };
        Record(o);
        if (changes.Count > 0) RaiseChanged();
    }

    // What an admission's origin says: one of your settlements, a place in the world, or nothing (unknown stays unknown).
    private static void ReadOrigin(string origin, ArrivalRecord record, WorldMap map)
    {
        record.originKind = ArrivalOriginKind.Unknown;
        if (string.IsNullOrEmpty(origin)) return;
        int colon = origin.IndexOf(':');
        string kind = colon > 0 ? origin.Substring(0, colon) : origin;
        string id = colon > 0 ? origin.Substring(colon + 1) : null;
        if (kind == "settlement" && int.TryParse(id, out int sid))
        {
            record.originKind = ArrivalOriginKind.Settlement;
            record.originId = CultureIds.Settlement(sid);
            record.originLabel = map != null ? WorldCivilization.Get(map, sid)?.name : null;
        }
        else if (kind == "survivors" && int.TryParse(id, out int cell))
        {
            record.originKind = ArrivalOriginKind.Place;
            record.originId = id;
            var t = map != null && cell >= 0 && cell < map.Count ? map[cell] : null;
            record.originLabel = t != null ? WorldSystem.Instance?.Place(t) : null;
        }
    }

    // ===== FOUNDERS AND VISITS (called by WorldSystem.CulturalContact) =====

    /// <summary>A new settlement's founders came from <paramref name="from"/>: they remember its customs (once per <paramref name="key"/>).</summary>
    public void LocalFounding(Settlement s, Settlement from, string carrier, string key)
    {
        if (s == null) return;
        // A new settlement: any profile under its id belonged to one that fell before (ids are reused), and is history.
        LocalCultureRules.Retire(Local, s.id);
        LocalCultureRules.EnsureProfile(Local, s.id, s.name);
        if (from == null) return;
        var changes = LocalCultureRules.Found(Local, key, s.id, s.name, from.id, from.name, carrier, _state.sevenths, AgeIdNow, AgeNow, LocalTuning);
        if (changes.Count > 0) RaiseChanged();
    }

    /// <summary>
    /// A cultural party's festival in <paramref name="s"/> is over: the host takes part in its own customs, and each
    /// custom the party carried is shown to it (exposure attributed to the party and its legends), once per festival.
    /// Returns a sentence for the notice, or null.
    /// </summary>
    public string CompleteLocalVisit(Settlement s, WorldUnit unit, IList<string> legends)
    {
        if (s == null || unit == null || !_state.founded) return null;
        var record = FestivalRecordOf(s.id);
        string key = $"visit:{unit.id}:{s.id}:{record?.count ?? 0}";
        var party = LocalCultureRules.Repertoire(Local, unit.id);
        if (party != null) party.unitName = unit.name;
        var changes = LocalCultureRules.Visit(Local, key, s.id, s.name, party ?? new PartyRepertoire { unit = unit.id, unitName = unit.name }, legends,
            _state.sevenths, AgeIdNow, AgeNow, LocalTuning);
        var shown = changes.Where(c => c.stage == LocalPracticeStage.Exposed).ToList();
        foreach (var c in shown)
        {
            var carried = party?.carried.FirstOrDefault(x => x.practice == c.practice);
            Record(new CulturalOccurrence
            {
                key = $"{key}:{c.practice}", kind = CulturalOccurrenceKind.Contact, stamp = Stamp(), settlement = s.id,
                subject = CultureEntityRef.Of(CultureEntityKind.Activity, "local:" + c.practice, LocalPracticeCatalog.NameOf(c.practice)),
                source = carried != null && carried.fromSettlement >= 0 ? CultureEntityRef.Of(CultureEntityKind.Settlement, CultureIds.Settlement(carried.fromSettlement), carried.fromName) : CultureEntityRef.None,
                actors = legends != null ? legends.Where(l => !string.IsNullOrEmpty(l)).ToList() : new List<string>(),
                quantity = 1f, unit = CultureQuantityUnit.Occasions, text = c.text,
            });
        }
        if (changes.Count > 0) RaiseChanged();
        if (shown.Count == 0) return null;
        return $"They performed {string.Join(" and ", shown.Select(c => LocalPracticeCatalog.NameOf(c.practice)))}: {s.name} will take it up if its people gather for it.";
    }

    /// <summary>Why the party cannot take up the custom where it stands, or null.</summary>
    public string WhyNotCarry(WorldUnit unit, Settlement where, string practice) =>
        unit == null ? "No party selected." : LocalCultureRules.WhyNotCarry(Local, unit.id, where != null ? LocalCultureRules.Profile(Local, where.id) : null, practice, LocalTuning);

    public void Carry(WorldUnit unit, Settlement where, string practice)
    {
        LocalCultureRules.Carry(Local, unit.id, unit.name, ProfileFor(where), practice, _state.sevenths);
        RaiseChanged();
    }

    public bool Drop(WorldUnit unit, string practice)
    {
        bool dropped = unit != null && LocalCultureRules.Drop(Local, unit.id, practice);
        if (dropped) RaiseChanged();
        return dropped;
    }

    /// <summary>Remember where an expedition took its settlers on (their customs go with them).</summary>
    public void NoteSettlers(WorldUnit unit, Settlement from)
    {
        if (unit == null || from == null) return;
        Local.settlers.RemoveAll(x => x == null || x.unit == unit.id);
        Local.settlers.Add(new SettlersOrigin { unit = unit.id, settlement = from.id, name = from.name });
    }

    /// <summary>Where an expedition's settlers were taken on, and forget it (they founded or rejoined somewhere).</summary>
    public SettlersOrigin TakeSettlersOrigin(WorldUnit unit)
    {
        var o = unit != null ? Local.settlers.FirstOrDefault(x => x != null && x.unit == unit.id) : null;
        if (o != null) Local.settlers.Remove(o);
        return o;
    }

    // ===== THE SEVENTH =====

    internal void LocalSeventh(CultureSeventh context)
    {
        var local = Local;
        var world = WorldSystem.Instance;
        if (world != null && world.Map != null) LocalCultureRules.PruneParties(local, new HashSet<int>(world.Map.Units.Select(u => u.id)));
        foreach (var o in context.Occurrences)
        {
            if (o == null) continue;
            // The nation recognised a custom: each settlement keeps its own form.
            if (o.kind == CulturalOccurrenceKind.Recognition)
            {
                // A tradition instance (T01) is recognised by its definition; anything else by its own id.
                string id = o.subject?.kind == CultureEntityKind.Tradition ? Tradition(o.subject.id)?.definition ?? o.subject.id : o.subject?.id;
                if (id != null && id.StartsWith("local:")) id = id.Substring(6);
                LocalCultureRules.Recognize(local, id);
            }
            // Another feature's gathering held in a known settlement for one of its customs (ours apply at once).
            else if (o.kind == CulturalOccurrenceKind.Gathering && o.SettlementKnown && o.source?.id != LocalGatheringSource)
            {
                string id = o.subject?.id ?? string.Empty;
                if (id.StartsWith("local:")) id = id.Substring(6);
                var spec = LocalPracticeCatalog.All.FirstOrDefault(sp => string.Equals(sp.id, id, StringComparison.OrdinalIgnoreCase) || string.Equals(sp.tradition, id, StringComparison.OrdinalIgnoreCase));
                var p = spec != null ? LocalCultureRules.Practice(LocalCultureRules.Profile(local, o.settlement), spec.id) : null;
                if (p != null) LocalCultureRules.Participate(p, LocalTuning.gatheringParticipation);
            }
        }
        var contact = world != null ? world.CulturalContact() : ContactSnapshot.Empty;
        foreach (var change in LocalCultureRules.Seventh(local, contact, LocalTuning, _state.sevenths, AgeIdNow, AgeNow))
        {
            if (string.IsNullOrEmpty(change.text)) continue;
            string name = LocalPracticeCatalog.NameOf(change.practice);
            context.Notices.Add((change.stage == LocalPracticeStage.Quiet ? $"{name} grows quiet" : $"A custom taken up", change.text, $"culture:local:{change.settlement}:{change.practice}:{change.stage}:{_state.sevenths}"));
            if (change.stage == LocalPracticeStage.Practiced) Remember("local-custom", change.text.TrimEnd('.'), change.text);
        }
    }

    internal void ReconcileLocal(CultureReconcileReason reason)
    {
        if (_state.extensions == null) _state.extensions = new CultureExtensionState();
        _state.extensions.local = LocalCultureRules.Ensure(_state.extensions.local);
        WorldSystem.Instance?.ResetCulturalContact();
    }
}

/// <summary>Local cultures' reads for the culture's queries (T01's <see cref="ICultureQuery"/>): snapshots only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<SettlementProfileView> LocalProfiles();
    SettlementProfileView LocalProfile(int settlement);
    IReadOnlyList<ContactEdgeView> ContactEdges();
    IReadOnlyList<ArrivalView> ArrivalsAt(int settlement);
    IReadOnlyList<CarriedPracticeView> CarriedBy(int unit);
}

/// <summary>Local participation and contact, run after the Seventh's occurrences are deduplicated and before the traditions' lifecycle.</summary>
public sealed class LocalCultureFeature : ICultureFeature
{
    public string Id => "local";
    public CulturePhase Phase => CulturePhase.LocalParticipation;
    public int Order => 0;

    public void Seventh(CultureSeventh context) => context?.Culture?.LocalSeventh(context);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileLocal(reason);
}
