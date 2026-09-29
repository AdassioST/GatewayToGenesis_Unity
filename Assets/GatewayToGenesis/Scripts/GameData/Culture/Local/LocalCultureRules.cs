using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What changed for one settlement's custom in a Seventh or a command (for notices and tests).</summary>
public struct LocalChange
{
    public int settlement;
    public string practice;
    public LocalPracticeStage stage;
    public PracticeChannel channel;
    public string text;
}

/// <summary>
/// Local cultures and exchange, with no scene (tested in <c>LocalCultureTests</c>). Research inferences (R2 partial
/// connectivity, R7 arrival / contact / adoption / retention as separate steps) shape the rules; every number is a
/// proposal (<see cref="LocalCultureTuning"/>).
/// - A settlement keeps its own customs (<see cref="SettlementProfile"/>). One begins where its ground fits
///   (<see cref="LocalPracticeSpec.grounds"/>) when the community gathers for it.
/// - Exposure comes only from actual contact: an open road to a community that keeps the custom (a little each
///   Seventh), a completed cultural visit (once per visit), founders, and people admitted from a known settlement.
///   Disconnected communities never exchange. Anonymous arrivals bring no custom: nothing is assumed about them.
/// - Adoption needs both exposure and participation. A road enables contact; it moves no land and no loyalty.
/// - A kept custom is never erased: a cut road stops future contact, a long silence makes it quiet, and a gathering
///   revives it. Its first provenance is never rewritten, and its local form survives national recognition.
/// </summary>
public static class LocalCultureRules
{
    // ===== PROFILES =====

    public static SettlementProfile Profile(LocalCultureState state, int settlement) =>
        state?.profiles.FirstOrDefault(p => p != null && p.settlement == settlement);

    public static SettlementProfile EnsureProfile(LocalCultureState state, int settlement, string name)
    {
        var p = Profile(state, settlement);
        if (p == null) state.profiles.Add(p = new SettlementProfile { settlement = settlement, name = name });
        else if (!string.IsNullOrEmpty(name)) p.name = name;
        if (p.practices == null) p.practices = new List<LocalPractice>();
        return p;
    }

    public static LocalPractice Practice(SettlementProfile profile, string practice) =>
        profile?.practices.FirstOrDefault(x => x != null && string.Equals(x.practice, practice, StringComparison.OrdinalIgnoreCase));

    /// <summary>The customs a settlement keeps now (practised, not quiet): what a road or a party can carry from it.</summary>
    public static IEnumerable<LocalPractice> Practised(SettlementProfile profile) =>
        profile == null ? Enumerable.Empty<LocalPractice>() : profile.practices.Where(p => p != null && p.stage == LocalPracticeStage.Practiced);

    /// <summary>Make sure every list exists (an older save, or a new world).</summary>
    public static LocalCultureState Ensure(LocalCultureState state)
    {
        if (state == null) state = new LocalCultureState();
        if (state.profiles == null) state.profiles = new List<SettlementProfile>();
        if (state.repertoires == null) state.repertoires = new List<PartyRepertoire>();
        if (state.settlers == null) state.settlers = new List<SettlersOrigin>();
        if (state.arrivals == null) state.arrivals = new List<ArrivalRecord>();
        if (state.olderArrivals == null) state.olderArrivals = new List<ArrivalSummary>();
        if (state.applied == null) state.applied = new List<string>();
        if (state.former == null) state.former = new List<SettlementProfile>();
        foreach (var p in state.profiles.Concat(state.former).Where(p => p != null))
        {
            if (p.practices == null) p.practices = new List<LocalPractice>();
            foreach (var x in p.practices.Where(x => x != null))
            {
                if (x.origin == null) x.origin = new PracticeProvenance();
                if (x.origin.legends == null) x.origin.legends = new List<string>();
            }
        }
        foreach (var r in state.repertoires.Where(r => r != null)) if (r.carried == null) r.carried = new List<CarriedPractice>();
        return state;
    }

    /// <summary>
    /// The settlement with this id no longer stands (it fell, or a newly founded one reuses its id): its profile becomes
    /// history (<see cref="LocalCultureState.former"/>), never erased and never inherited. True when one was retired.
    /// </summary>
    public static bool Retire(LocalCultureState state, int settlement)
    {
        var p = Profile(state, settlement);
        if (p == null) return false;
        p.gone = true;
        state.profiles.Remove(p);
        if (state.former == null) state.former = new List<SettlementProfile>();
        state.former.Add(p);
        return true;
    }

    /// <summary>This settlement's own form of a custom ("Evening of Song of Ashford").</summary>
    public static string Variant(LocalPracticeSpec spec, string settlementName) =>
        string.IsNullOrEmpty(settlementName) ? spec?.name : $"{spec?.name ?? "A custom"} of {settlementName}";

    // ===== EXPOSURE =====

    /// <summary>
    /// <paramref name="amount"/> exposure to a custom for a community that does not keep it. The first contact's
    /// provenance is recorded and never rewritten. False when nothing changed (it keeps it already, the custom is not
    /// of this Age, or no amount).
    /// </summary>
    public static bool Expose(LocalCultureState state, int settlement, string name, string practice, float amount, PracticeProvenance from, int age)
    {
        var spec = LocalPracticeCatalog.Find(practice);
        if (state == null || spec == null || amount <= 0f || !spec.OpenIn(age)) return false;
        var profile = EnsureProfile(state, settlement, name);
        var p = Practice(profile, spec.id);
        if (p != null && p.Kept) return false;
        if (p == null) profile.practices.Add(p = new LocalPractice { practice = spec.id, stage = LocalPracticeStage.Exposed, origin = from?.Copy() ?? new PracticeProvenance() });
        else if (p.origin == null || p.origin.channel == PracticeChannel.Unknown) p.origin = from?.Copy() ?? new PracticeProvenance();
        p.exposure = Math.Min(1f, p.exposure + amount);
        return true;
    }

    /// <summary>Participation in a custom (a gathering, a festival): the community took part.</summary>
    public static void Participate(LocalPractice p, float amount)
    {
        if (p != null && amount > 0f) p.participation = Math.Min(1f, p.participation + amount);
    }

    /// <summary>Take a custom up when exposure and participation both reach the thresholds (and its Age allows). True when it was.</summary>
    public static bool TryAdopt(SettlementProfile profile, LocalPractice p, LocalCultureTuning tuning, int cultureSeventh, int age)
    {
        var spec = LocalPracticeCatalog.Find(p?.practice);
        if (p == null || spec == null || p.Kept || !spec.OpenIn(age)) return false;
        if (p.exposure + 1e-4f < tuning.adoptExposure || p.participation + 1e-4f < tuning.adoptParticipation) return false;
        p.stage = LocalPracticeStage.Practiced;
        p.adoptedSeventh = cultureSeventh;
        if (string.IsNullOrEmpty(p.variant)) p.variant = Variant(spec, profile?.name);
        return true;
    }

    // ===== GATHERINGS =====

    /// <summary>
    /// Why <paramref name="profile"/>'s community cannot gather for <paramref name="practice"/> now, or null. A custom
    /// is held where it is kept, where its ground lets it begin, or where they know it well enough to try it.
    /// </summary>
    public static string WhyNotGather(SettlementProfile profile, string practice, string ground, int age, int cultureSeventh, LocalCultureTuning tuning)
    {
        var spec = LocalPracticeCatalog.Find(practice);
        if (spec == null) return "No such custom.";
        if (profile != null && profile.gone) return "No one lives there any more.";
        if (profile != null && profile.lastGathering >= 0)
        {
            int wait = profile.lastGathering + tuning.gatheringCooldownSevenths - cultureSeventh;
            if (wait > 0) return $"They gathered not long ago: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        }
        var p = Practice(profile, spec.id);
        if (p != null && p.Kept) return null;
        if (!spec.OpenIn(age)) return $"{spec.name} belongs to {spec.AgeText}.";
        if (p != null && p.exposure + 1e-4f >= tuning.tryExposure) return null;
        if (spec.FitsGround(ground)) return null;
        return p != null && p.exposure > 0f
            ? $"They know {spec.name} only a little ({p.exposure:P0} of the {tuning.tryExposure:P0} needed to try it): more contact first."
            : $"No one here knows {spec.name}: it begins only {spec.groundText}, or must come by road, a cultural party or those who keep it.";
    }

    /// <summary>
    /// A gathering held for a custom (its cost already paid): it begins here (origin), is revived, or is taken up once
    /// exposure and participation both suffice. Returns what changed.
    /// </summary>
    public static LocalChange Gather(LocalCultureState state, int settlement, string name, string practice, string ground, int cultureSeventh, string ageId, int age, LocalCultureTuning tuning)
    {
        var spec = LocalPracticeCatalog.Find(practice);
        var profile = EnsureProfile(state, settlement, name);
        profile.lastGathering = cultureSeventh;
        var p = Practice(profile, spec.id);
        var change = new LocalChange { settlement = settlement, practice = spec.id };
        if (p == null && spec.FitsGround(ground) && spec.OpenIn(age))
        {
            profile.practices.Add(p = new LocalPractice
            {
                practice = spec.id, stage = LocalPracticeStage.Practiced, exposure = 1f, adoptedSeventh = cultureSeventh, variant = Variant(spec, name),
                origin = new PracticeProvenance { channel = PracticeChannel.Originated, fromSettlement = settlement, fromName = name, carrier = "its own people", seventh = cultureSeventh, ageId = ageId },
            });
            change.channel = PracticeChannel.Originated;
            change.text = $"{spec.name} begins in {name}: {spec.groundText}.";
        }
        else if (p == null)
        {
            // Only reached when the rules were bypassed: the gathering happened, but nothing is invented about it.
            profile.practices.Add(p = new LocalPractice { practice = spec.id, stage = LocalPracticeStage.Exposed });
        }
        // Heard of first by road or visit, but the ground here teaches it too: they know it fully (its first provenance stays).
        else if (!p.Kept && spec.FitsGround(ground) && spec.OpenIn(age)) p.exposure = 1f;
        p.gatherings++;
        p.lastGathered = cultureSeventh;
        Participate(p, tuning.gatheringParticipation);
        change.stage = p.stage;
        if (change.channel == PracticeChannel.Originated) return change;
        change.channel = p.origin?.channel ?? PracticeChannel.Unknown;
        if (p.stage == LocalPracticeStage.Quiet)
        {
            p.stage = LocalPracticeStage.Practiced;
            change.stage = p.stage;
            change.text = $"{name} gathers for {spec.name} again: the custom is kept once more.";
        }
        else if (TryAdopt(profile, p, tuning, cultureSeventh, age))
        {
            change.stage = p.stage;
            change.text = $"{name} takes up {spec.name}{ProvenanceWords(p.origin)}.";
        }
        else if (!p.Kept)
            change.text = $"{name} tries {spec.name}: {Progress(p, tuning)}.";
        else change.text = $"{name} gathers for {spec.name}.";
        return change;
    }

    /// <summary>"exposure 45% of 60%, taking part 50% of 50%".</summary>
    public static string Progress(LocalPractice p, LocalCultureTuning tuning) =>
        p == null ? "unknown here" : $"known {p.exposure:P0} (needs {tuning.adoptExposure:P0}), taking part {p.participation:P0} (needs {tuning.adoptParticipation:P0})";

    /// <summary>" (brought by the Cultural Party of Vaelia from Ashford)", or empty when the origin is unknown.</summary>
    public static string ProvenanceWords(PracticeProvenance o)
    {
        if (o == null) return string.Empty;
        string from = !string.IsNullOrEmpty(o.fromName) ? $" from {o.fromName}" : string.Empty;
        string who = o.legends != null && o.legends.Count > 0 ? $" ({string.Join(", ", o.legends)})" : string.Empty;
        switch (o.channel)
        {
            case PracticeChannel.Originated: return " (it began here)";
            case PracticeChannel.Road: return $" (it came along the road{from})";
            case PracticeChannel.Visit: return $" (brought by {o.carrier ?? "a cultural party"}{who}{from})";
            case PracticeChannel.Founders: return $" (carried by its founders{from})";
            case PracticeChannel.Arrival: return $" (brought by {o.carrier ?? "people who came"}{from})";
            case PracticeChannel.Table: return $" (met at {o.carrier ?? "a shared table"}{from})";
            case PracticeChannel.Inherited: return $" (taken up from {o.carrier ?? "the ruins of a fallen people"})";
            default: return " (its origin was not recorded)";
        }
    }

    // ===== VISITS, FOUNDERS, ARRIVALS =====

    /// <summary>A key applied once only (bounded memory). False when it was applied already.</summary>
    public static bool MarkApplied(LocalCultureState state, string key, LocalCultureTuning tuning)
    {
        if (state == null || string.IsNullOrEmpty(key) || state.applied.Contains(key)) return false;
        state.applied.Add(key);
        int keep = Math.Max(16, tuning.keysKept);
        if (state.applied.Count > keep) state.applied.RemoveRange(0, state.applied.Count - keep);
        return true;
    }

    /// <summary>
    /// A cultural party's festival in <paramref name="host"/> is over (<paramref name="key"/> names that one festival).
    /// Once per key: each carried custom the host does not keep gains visit exposure, attributed to the party, its
    /// legends and where it took the custom up; the host takes part in its own customs and, a little, in those
    /// performed. Returns what changed (empty on a repeat).
    /// </summary>
    public static List<LocalChange> Visit(LocalCultureState state, string key, int host, string hostName, PartyRepertoire party, IList<string> legends,
        int cultureSeventh, string ageId, int age, LocalCultureTuning tuning)
    {
        var changes = new List<LocalChange>();
        if (!MarkApplied(state, key, tuning)) return changes;
        var profile = EnsureProfile(state, host, hostName);
        foreach (var own in Practised(profile).ToList()) Participate(own, tuning.festivalParticipation);
        foreach (var carried in party?.carried ?? new List<CarriedPractice>())
        {
            if (carried == null) continue;
            var spec = LocalPracticeCatalog.Find(carried.practice);
            if (spec == null) continue;
            var from = new PracticeProvenance
            {
                channel = PracticeChannel.Visit, fromSettlement = carried.fromSettlement, fromName = carried.fromName,
                carrier = string.IsNullOrEmpty(party.unitName) ? "a cultural party" : party.unitName,
                legends = legends != null ? legends.Where(l => !string.IsNullOrEmpty(l)).ToList() : new List<string>(), seventh = cultureSeventh, ageId = ageId,
            };
            var existing = Practice(profile, spec.id);
            if (existing != null && existing.Kept)
            {
                changes.Add(new LocalChange { settlement = host, practice = spec.id, stage = existing.stage, channel = PracticeChannel.Visit, text = $"{hostName} already keeps {spec.name}." });
                continue;
            }
            if (!Expose(state, host, hostName, spec.id, tuning.visitExposure, from, age)) continue;
            var p = Practice(profile, spec.id);
            Participate(p, tuning.performedParticipation);
            changes.Add(new LocalChange { settlement = host, practice = spec.id, stage = p.stage, channel = PracticeChannel.Visit, text = $"{hostName} saw {spec.name} performed: {Progress(p, tuning)}." });
        }
        return changes;
    }

    /// <summary>
    /// A new settlement's founders bring the customs of <paramref name="from"/> (exposure, attributed to them): once per
    /// key. Nothing is brought when the home settlement kept nothing, or is unknown.
    /// </summary>
    public static List<LocalChange> Found(LocalCultureState state, string key, int settlement, string name, int from, string fromName, string carrier,
        int cultureSeventh, string ageId, int age, LocalCultureTuning tuning)
    {
        var changes = new List<LocalChange>();
        if (from < 0 || from == settlement || !MarkApplied(state, key, tuning)) return changes;
        EnsureProfile(state, settlement, name);
        foreach (var kept in Practised(Profile(state, from)).ToList())
        {
            var prov = new PracticeProvenance { channel = PracticeChannel.Founders, fromSettlement = from, fromName = fromName, carrier = carrier, seventh = cultureSeventh, ageId = ageId };
            if (Expose(state, settlement, name, kept.practice, tuning.founderExposure, prov, age))
                changes.Add(new LocalChange { settlement = settlement, practice = kept.practice, stage = LocalPracticeStage.Exposed, channel = PracticeChannel.Founders, text = $"{name}'s founders remember {LocalPracticeCatalog.NameOf(kept.practice)} from {fromName}." });
        }
        return changes;
    }

    /// <summary>
    /// People admitted (the population's admission, as reported): recorded as they were, never with an invented origin.
    /// Only people from one of your settlements bring its customs. Once per key. Returns what changed.
    /// </summary>
    public static List<LocalChange> Arrive(LocalCultureState state, ArrivalRecord arrival, LocalCultureTuning tuning, int cultureSeventh, int age, string destinationName)
    {
        var changes = new List<LocalChange>();
        if (arrival == null || arrival.people <= 0 || !MarkApplied(state, arrival.key, tuning)) return changes;
        state.arrivals.Add(arrival);
        int keep = Math.Max(1, tuning.arrivalsKept);
        while (state.arrivals.Count > keep)
        {
            var old = state.arrivals[0];
            state.arrivals.RemoveAt(0);
            var sum = state.olderArrivals.FirstOrDefault(s => s.settlement == old.settlement && s.originKind == old.originKind);
            if (sum == null) state.olderArrivals.Add(sum = new ArrivalSummary { settlement = old.settlement, originKind = old.originKind });
            sum.people += old.people;
            sum.arrivals++;
        }
        if (arrival.originKind != ArrivalOriginKind.Settlement || arrival.settlement < 0 || !int.TryParse(arrival.originId, out int from) || from == arrival.settlement) return changes;
        foreach (var kept in Practised(Profile(state, from)).ToList())
        {
            var prov = new PracticeProvenance
            {
                channel = PracticeChannel.Arrival, fromSettlement = from, fromName = arrival.originLabel, carrier = $"{arrival.people} {(arrival.people == 1 ? "person" : "people")} arriving ({arrival.channel ?? "arrivals"})",
                seventh = cultureSeventh, ageId = arrival.ageId,
            };
            if (Expose(state, arrival.settlement, destinationName, kept.practice, tuning.arrivalExposure, prov, age))
                changes.Add(new LocalChange { settlement = arrival.settlement, practice = kept.practice, stage = LocalPracticeStage.Exposed, channel = PracticeChannel.Arrival });
        }
        return changes;
    }

    /// <summary>
    /// An open communal table in <paramref name="host"/> (T05) drew guests from <paramref name="guests"/> (neighbours by
    /// open road): each side meets the customs the other keeps now (<paramref name="amount"/> exposure, attributed to the
    /// table), once per <paramref name="key"/>. Nothing is taken up without gathering. Returns what changed.
    /// </summary>
    public static List<LocalChange> Table(LocalCultureState state, string key, int host, string hostName, IEnumerable<(int id, string name)> guests, float amount,
        int cultureSeventh, string ageId, int age, LocalCultureTuning tuning)
    {
        var changes = new List<LocalChange>();
        var list = (guests ?? Enumerable.Empty<(int, string)>()).Where(g => g.Item1 != host).ToList();
        if (state == null || list.Count == 0 || amount <= 0f || !MarkApplied(state, key, tuning)) return changes;
        string carrier = $"the shared table in {hostName}";
        var hostKept = Practised(Profile(state, host)).Select(p => p.practice).ToList();
        foreach (var (id, name) in list)
        {
            var guestKept = Practised(Profile(state, id)).Select(p => p.practice).ToList();
            foreach (string practice in guestKept)
                if (Expose(state, host, hostName, practice, amount, new PracticeProvenance { channel = PracticeChannel.Table, fromSettlement = id, fromName = name, carrier = carrier, seventh = cultureSeventh, ageId = ageId }, age))
                    changes.Add(new LocalChange { settlement = host, practice = practice, stage = LocalPracticeStage.Exposed, channel = PracticeChannel.Table, text = $"Guests from {name} brought {LocalPracticeCatalog.NameOf(practice)} to the table in {hostName}." });
            foreach (string practice in hostKept)
                if (Expose(state, id, name, practice, amount, new PracticeProvenance { channel = PracticeChannel.Table, fromSettlement = host, fromName = hostName, carrier = carrier, seventh = cultureSeventh, ageId = ageId }, age))
                    changes.Add(new LocalChange { settlement = id, practice = practice, stage = LocalPracticeStage.Exposed, channel = PracticeChannel.Table, text = $"Guests from {name} carried {LocalPracticeCatalog.NameOf(practice)} home from {hostName}." });
        }
        return changes;
    }

    /// <summary>People admitted to a settlement, all told, by what is known of their origin (records and older summaries).</summary>
    public static (int known, int place, int unknown) ArrivalsAt(LocalCultureState state, int settlement)
    {
        int k = 0, pl = 0, u = 0;
        void Add(ArrivalOriginKind kind, int n) { if (kind == ArrivalOriginKind.Settlement) k += n; else if (kind == ArrivalOriginKind.Place) pl += n; else u += n; }
        foreach (var a in state?.arrivals ?? new List<ArrivalRecord>()) if (a != null && a.settlement == settlement) Add(a.originKind, a.people);
        foreach (var s in state?.olderArrivals ?? new List<ArrivalSummary>()) if (s != null && s.settlement == settlement) Add(s.originKind, s.people);
        return (k, pl, u);
    }

    // ===== PARTIES =====

    public static PartyRepertoire Repertoire(LocalCultureState state, int unit) => state?.repertoires.FirstOrDefault(r => r != null && r.unit == unit);

    /// <summary>Why the party cannot take up <paramref name="practice"/> in <paramref name="where"/> now, or null.</summary>
    public static string WhyNotCarry(LocalCultureState state, int unit, SettlementProfile where, string practice, LocalCultureTuning tuning)
    {
        var spec = LocalPracticeCatalog.Find(practice);
        if (spec == null) return "No such custom.";
        if (where == null) return "A party takes up customs in a settlement that keeps them.";
        var p = Practice(where, spec.id);
        if (p == null || p.stage != LocalPracticeStage.Practiced) return $"{where.name} does not keep {spec.name} now.";
        var r = Repertoire(state, unit);
        if (r != null && r.carried.Any(c => string.Equals(c.practice, spec.id, StringComparison.OrdinalIgnoreCase))) return $"The party already carries {spec.name}.";
        int size = Math.Max(1, tuning.repertoireSize);
        if (r != null && r.carried.Count >= size) return $"A party carries {size} customs at most: set one down first.";
        return null;
    }

    /// <summary>The party takes up a custom where it stands (after <see cref="WhyNotCarry"/>).</summary>
    public static void Carry(LocalCultureState state, int unit, string unitName, SettlementProfile where, string practice, int cultureSeventh)
    {
        var r = Repertoire(state, unit);
        if (r == null) state.repertoires.Add(r = new PartyRepertoire { unit = unit, unitName = unitName });
        r.unitName = unitName ?? r.unitName;
        r.carried.Add(new CarriedPractice { practice = LocalPracticeCatalog.Find(practice)?.id ?? practice, fromSettlement = where.settlement, fromName = where.name, seventh = cultureSeventh });
    }

    public static bool Drop(LocalCultureState state, int unit, string practice)
    {
        var r = Repertoire(state, unit);
        return r != null && r.carried.RemoveAll(c => c == null || string.Equals(c.practice, practice, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>Forget the repertoires and settlers of parties no longer on the map.</summary>
    public static void PruneParties(LocalCultureState state, ICollection<int> units)
    {
        if (state == null || units == null) return;
        state.repertoires.RemoveAll(r => r == null || !units.Contains(r.unit));
        state.settlers.RemoveAll(s => s == null || !units.Contains(s.unit));
    }

    // ===== THE SEVENTH =====

    /// <summary>
    /// A Seventh of local life: open roads carry the customs kept at either end (a little exposure); exposure not
    /// renewed fades; participation fades; customs with enough of both are taken up; long-silent ones grow quiet.
    /// Settlements no longer standing keep their profile as history. Nothing is ever deleted. Returns what changed.
    /// </summary>
    public static List<LocalChange> Seventh(LocalCultureState state, ContactSnapshot contact, LocalCultureTuning tuning, int cultureSeventh, string ageId, int age)
    {
        var changes = new List<LocalChange>();
        if (state == null) return changes;
        contact = contact ?? ContactSnapshot.Empty;
        // Settlements no longer standing keep their customs as history (a world with no settlements at all is not read).
        if (contact.Standing.Count > 0)
            foreach (var gone in state.profiles.Where(p => p != null && !contact.Stands(p.settlement)).Select(p => p.settlement).ToList()) Retire(state, gone);
        foreach (var p in state.profiles.Where(p => p != null)) p.name = contact.NameOf(p.settlement) ?? p.name;
        // Contact: what each end kept at the start of the Seventh reaches the other (never through a closed road).
        var touched = new HashSet<(int, string)>();
        var keptNow = contact.Standing.Keys.ToDictionary(id => id, id => Practised(Profile(state, id)).Select(x => x.practice).ToList());
        foreach (var edge in contact.Edges.Where(e => e.open))
        {
            foreach (var (from, to) in new[] { (edge.a, edge.b), (edge.b, edge.a) })
            {
                foreach (string practice in keptNow[from])
                {
                    string fromName = contact.NameOf(from);
                    var prov = new PracticeProvenance { channel = PracticeChannel.Road, fromSettlement = from, fromName = fromName, carrier = $"the road from {fromName}", seventh = cultureSeventh, ageId = ageId };
                    if (Expose(state, to, contact.NameOf(to), practice, tuning.roadExposurePerSeventh, prov, age)) touched.Add((to, practice));
                }
            }
        }
        foreach (var profile in state.profiles.Where(p => p != null))
        {
            foreach (var p in profile.practices.Where(x => x != null))
            {
                if (!profile.gone && TryAdopt(profile, p, tuning, cultureSeventh, age))
                    changes.Add(new LocalChange { settlement = profile.settlement, practice = p.practice, stage = p.stage, channel = p.origin?.channel ?? PracticeChannel.Unknown,
                        text = $"{profile.name} takes up {LocalPracticeCatalog.NameOf(p.practice)}{ProvenanceWords(p.origin)}." });
                if (!p.Kept && !touched.Contains((profile.settlement, p.practice))) p.exposure = Math.Max(0f, p.exposure * (1f - Clamp01(tuning.exposureFade)));
                p.participation = Math.Max(0f, p.participation * (1f - Clamp01(tuning.participationFade)));
                if (p.stage == LocalPracticeStage.Practiced && !profile.gone)
                {
                    int last = Math.Max(p.lastGathered, p.adoptedSeventh);
                    if (cultureSeventh - last >= Math.Max(1, tuning.quietAfterSevenths))
                    {
                        p.stage = LocalPracticeStage.Quiet;
                        changes.Add(new LocalChange { settlement = profile.settlement, practice = p.practice, stage = p.stage, channel = p.origin?.channel ?? PracticeChannel.Unknown,
                            text = $"No one in {profile.name} has gathered for {LocalPracticeCatalog.NameOf(p.practice)} in a long while: it grows quiet (remembered, not lost)." });
                    }
                }
            }
        }
        return changes;
    }

    /// <summary>
    /// The nation recognised a custom (by its id or the tradition it is a local form of): every settlement that keeps it
    /// keeps its own variant, marked recognised; none is merged into another. Returns how many local forms were marked.
    /// </summary>
    public static int Recognize(LocalCultureState state, string practiceOrTradition)
    {
        if (state == null || string.IsNullOrEmpty(practiceOrTradition)) return 0;
        var specs = LocalPracticeCatalog.All.Where(s => string.Equals(s.id, practiceOrTradition, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.tradition, practiceOrTradition, StringComparison.OrdinalIgnoreCase)).Select(s => s.id).ToList();
        int n = 0;
        foreach (var profile in state.profiles.Where(p => p != null))
            foreach (var p in profile.practices.Where(p => p != null && p.Kept && specs.Contains(p.practice) && !p.recognized))
            {
                p.recognized = true;
                if (string.IsNullOrEmpty(p.variant)) p.variant = Variant(LocalPracticeCatalog.Find(p.practice), profile.name);
                n++;
            }
        return n;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
