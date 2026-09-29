using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What the culture recorded, as public memory reads it (built by CultureSystem.PublicMemory.cs from the other features'
/// snapshots). Only records: nothing here is read from the world's own truth, so what was never recorded stays unknown.
/// </summary>
public sealed class PublicFacts
{
    public int now;
    /// <summary>The linked tradition is lived now.</summary>
    public bool practiceLived = true;
    public string practiceName;
    public readonly List<TableFact> tables = new List<TableFact>();
    public readonly List<AdmissionFact> admissions = new List<AdmissionFact>();
    /// <summary>Luxuries held while no open table shared one lately ("12 Honeyed Peach Tart", T05's access report).</summary>
    public readonly List<string> hoarded = new List<string>();
    public readonly List<CauseFact> causes = new List<CauseFact>();
    public readonly List<DedicationFact> dedications = new List<DedicationFact>();
    public readonly List<MemorialFact> memorials = new List<MemorialFact>();
    /// <summary>The standing settlements (id, name).</summary>
    public readonly List<(int id, string name)> settlements = new List<(int, string)>();
}

public sealed class TableFact
{
    public string key, place, patron;
    public TablePolicy policy;
    public int settlement = -1, seventh;
    public bool luxuryShared;
    /// <summary>Luxuries served at a patron's table (private).</summary>
    public string[] privateLuxuries = Array.Empty<string>();
}

public sealed class AdmissionFact
{
    public string key, place, text;
    public int settlement = -1, seventh, people;
}

public sealed class CauseFact
{
    public string id, title, subject;
    public MemoryCause cause;
    /// <summary>The last quiet remembrance (culture Seventh; -1: never).</summary>
    public int lastQuiet = -1;
}

public sealed class DedicationFact
{
    public string id, evidence, label, dormantReason;
    public DedicationStatus status;
    public int lastPracticed = -1;
}

public sealed class MemorialFact
{
    public string key, evidence, place;
    public int settlement = -1, seventh;
}

/// <summary>
/// Public memory's rules, pure (tested in CulturePublicMemoryTests): a promise compared with a bounded window of
/// records; how each place stands by what was recorded there; links, disputes and versioned accounts; and the capped,
/// explained part of the council's accord. Nothing here writes to a record it reads.
/// </summary>
public static class PublicMemoryRules
{
    public static void Ensure(PublicMemoryState s)
    {
        if (s == null) return;
        if (s.links == null) s.links = new List<PublicLink>();
        if (s.accounts == null) s.accounts = new List<PublicAccount>();
        if (s.disputes == null) s.disputes = new List<PublicDispute>();
        s.links.RemoveAll(l => l == null);
        s.accounts.RemoveAll(a => a == null);
        s.disputes.RemoveAll(d => d == null);
        foreach (var l in s.links) if (l.last == null) l.last = new PromiseComparison();
        foreach (var d in s.disputes)
        {
            if (d.records == null) d.records = new List<RecordedAction>();
            if (d.places == null) d.places = new List<LocalSupport>();
            if (d.paid == null) d.paid = new List<ResourceAmount>();
        }
        s.nextLink = Math.Max(s.nextLink, Next(s.links.Select(l => l.id), "link-"));
        s.nextAccount = Math.Max(s.nextAccount, Next(s.accounts.Select(a => a.id), "account-"));
        s.nextDispute = Math.Max(s.nextDispute, Next(s.disputes.Select(d => d.id), "dispute-"));
    }

    private static int Next(IEnumerable<string> ids, string prefix)
    {
        int next = 1;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(prefix.Length), out int n) && n >= next) next = n + 1;
        return next;
    }

    private static bool Within(int seventh, int now, int window) => seventh >= 0 && seventh <= now && now - seventh <= Math.Max(0, window);

    /// <summary>Bind a story to its own newest open dispute, never another story's evidence.</summary>
    public static PublicDispute StoryDispute(PublicMemoryState s, PublicMemoryTuning t, string story) =>
        s?.disputes.Where(d => d != null && d.Open && t.Dispute(d.spec)?.story == story)
            .OrderByDescending(d => d.openedSeventh).FirstOrDefault();

    // ===== COMPARING A PROMISE WITH THE RECORDS =====

    /// <summary>What the records of the last <see cref="PublicMemoryTuning.window"/> Sevenths say about <paramref name="promise"/>.</summary>
    public static PromiseComparison Compare(PromiseSpec promise, PublicFacts f, PublicMemoryTuning t)
    {
        var c = new PromiseComparison { seventh = f?.now ?? -1 };
        if (promise == null || f == null) return c;
        if (!f.practiceLived)
        {
            c.practiceQuiet = true;
            c.actions.Add(new RecordedAction { source = "practice", bearing = RecordBearing.Silent, text = $"{f.practiceName ?? "The practice"} lies dormant: it shows nothing either way now." });
        }
        if (promise.kind == PromiseKind.Hospitality) Hospitality(f, t, c.actions);
        else Remembrance(promise, f, t, c.actions);
        c.supports = c.actions.Count(a => a.bearing == RecordBearing.Supports);
        c.contradicts = c.actions.Count(a => a.bearing == RecordBearing.Contradicts);
        c.silent = c.actions.Count(a => a.bearing == RecordBearing.Silent);
        return c;
    }

    private static void Hospitality(PublicFacts f, PublicMemoryTuning t, List<RecordedAction> list)
    {
        foreach (var table in f.tables.Where(x => Within(x.seventh, f.now, t.window)).OrderBy(x => x.seventh))
        {
            var a = new RecordedAction { source = "table:" + table.key, seventh = table.seventh, settlement = table.settlement, place = table.place };
            if (table.policy != TablePolicy.PatronHosted)
            {
                a.bearing = RecordBearing.Supports;
                a.text = $"An open table at {table.place} (Seventh {table.seventh}){(table.luxuryShared ? ", a luxury shared openly" : string.Empty)}.";
            }
            else if (table.privateLuxuries.Length > 0)
            {
                a.bearing = RecordBearing.Contradicts;
                a.text = $"At {table.place}, {string.Join(", ", table.privateLuxuries)} reached only {table.patron ?? "a patron"}'s guests (Seventh {table.seventh}).";
            }
            else
            {
                a.bearing = RecordBearing.Silent;
                a.text = $"{table.patron ?? "A patron"} hosted a table at {table.place} (Seventh {table.seventh}).";
            }
            list.Add(a);
        }
        foreach (var admission in f.admissions.Where(x => Within(x.seventh, f.now, t.window)).OrderBy(x => x.seventh))
            list.Add(new RecordedAction
            {
                source = "admission:" + admission.key, bearing = RecordBearing.Supports, seventh = admission.seventh, settlement = admission.settlement, place = admission.place,
                text = $"Admitted at {admission.place ?? "a settlement"}: {admission.text ?? $"{admission.people} people"} (Seventh {admission.seventh}).",
            });
        foreach (var held in f.hoarded)
            list.Add(new RecordedAction { source = "held:" + held, bearing = RecordBearing.Contradicts, seventh = f.now, text = $"Held in the stores but not shared at any open table lately: {held}." });
    }

    private static void Remembrance(PromiseSpec promise, PublicFacts f, PublicMemoryTuning t, List<RecordedAction> list)
    {
        var causes = f.causes.Where(x => PromisedCause(promise, x)).ToList();
        if (causes.Count == 0)
        {
            list.Add(new RecordedAction { source = "causes", bearing = RecordBearing.Silent, text = "No such loss is recorded yet: the promise has nothing to answer to." });
            return;
        }
        foreach (var cause in causes)
        {
            bool kept = false;
            foreach (var d in f.dedications.Where(x => x.evidence == cause.id))
            {
                var a = new RecordedAction { source = "dedication:" + d.id, seventh = d.lastPracticed };
                switch (d.status)
                {
                    case DedicationStatus.Active when Within(d.lastPracticed, f.now, t.window):
                        a.bearing = RecordBearing.Supports;
                        a.text = $"{d.label}, dedicated to {cause.title}, was kept at Seventh {d.lastPracticed}.";
                        kept = true;
                        break;
                    case DedicationStatus.Active:
                        a.bearing = RecordBearing.Contradicts;
                        a.text = $"{d.label} is dedicated to {cause.title}, but it was not kept in the last {t.window} Sevenths{(d.lastPracticed >= 0 ? $" (last at Seventh {d.lastPracticed})" : " (never yet)")}.";
                        break;
                    case DedicationStatus.Dormant:
                        a.bearing = RecordBearing.Contradicts;
                        a.text = $"{d.label}, dedicated to {cause.title}, sleeps: {d.dormantReason ?? "what it was dedicated through is gone"}.";
                        break;
                    default:
                        a.bearing = RecordBearing.Silent;
                        a.text = $"{d.label} was released from {cause.title}'s memory.";
                        break;
                }
                list.Add(a);
            }
            if (Within(cause.lastQuiet, f.now, t.window))
            {
                list.Add(new RecordedAction { source = "cause:" + cause.id, bearing = RecordBearing.Supports, seventh = cause.lastQuiet, text = $"{cause.title} was remembered quietly at Seventh {cause.lastQuiet}." });
                kept = true;
            }
            if (!kept && !f.dedications.Any(x => x.evidence == cause.id && x.status != DedicationStatus.Released))
                list.Add(new RecordedAction { source = "cause:" + cause.id, bearing = RecordBearing.Contradicts, text = $"No memorial is dedicated to {cause.title}, and it was not remembered in the last {t.window} Sevenths." });
        }
        foreach (var m in f.memorials.Where(x => Within(x.seventh, f.now, t.window) && causes.Any(c => c.id == x.evidence)))
            list.Add(new RecordedAction { source = "memorial:" + m.key, bearing = RecordBearing.Silent, seventh = m.seventh, settlement = m.settlement, place = m.place, text = $"Remembered at {m.place ?? "a place not recorded"} (Seventh {m.seventh})." });
    }

    /// <summary>
    /// How each standing place stands, by what was recorded there within the window (its customs, family or origin are
    /// never read). A place with no relevant record has no assumed opinion. <paramref name="contested"/>: a Legend's
    /// different account stands, so a place whose records contradict it doubts it.
    /// </summary>
    public static List<LocalSupport> Local(PromiseSpec promise, PublicFacts f, PublicMemoryTuning t, bool contested)
    {
        var list = new List<LocalSupport>();
        if (promise == null || f == null) return list;
        foreach (var (id, name) in f.settlements)
        {
            int good = 0, bad = 0;
            string reason = null;
            if (promise.kind == PromiseKind.Hospitality)
            {
                var tables = f.tables.Where(x => x.settlement == id && Within(x.seventh, f.now, t.window)).ToList();
                good = tables.Count(x => x.policy != TablePolicy.PatronHosted) + f.admissions.Count(x => x.settlement == id && Within(x.seventh, f.now, t.window));
                bad = tables.Count(x => x.policy == TablePolicy.PatronHosted && x.privateLuxuries.Length > 0);
                reason = good > 0 && bad > 0 ? "It held open tables, and luxuries that reached only a patron's guests."
                    : good > 0 ? "Its tables were open and its gates received people."
                    : bad > 0 ? "Its luxuries reached only a patron's guests." : null;
            }
            else
            {
                good = f.memorials.Count(x => x.settlement == id && Within(x.seventh, f.now, t.window) &&
                    f.causes.Any(c => c.id == x.evidence && PromisedCause(promise, c)));
                reason = good > 0 ? "The memory was kept here." : null;
            }
            var stance = good == 0 && bad == 0 ? LocalStance.NoRecord : bad == 0 ? LocalStance.Supports : good == 0 && contested ? LocalStance.Doubts : LocalStance.Divided;
            if (contested && bad > 0 && good > 0) stance = LocalStance.Doubts;
            if (stance == LocalStance.Doubts) reason = $"{reason} Its own records disagree with the account the council let stand.";
            list.Add(new LocalSupport { settlement = id, place = name, stance = stance, reason = reason ?? "Nothing relevant is recorded here: no opinion is assumed." });
        }
        return list;
    }

    // ===== LINKS =====

    /// <summary>
    /// Why <paramref name="tradition"/> (its definition <paramref name="definition"/>, lived or not) cannot be linked to
    /// <paramref name="promise"/> now, or null. <paramref name="inForce"/>: the promise's stance or civic is the law now.
    /// </summary>
    public static string WhyNotLink(PublicMemoryState s, PublicMemoryTuning t, PromiseSpec promise, string tradition, string definition, bool lived, bool inForce, int age, int now)
    {
        if (s == null || t == null) return "Nothing to link.";
        if (promise == null) return "Choose a promise.";
        if (string.IsNullOrEmpty(tradition)) return "Choose a tradition.";
        if (!promise.OpenIn(age)) return $"{promise.name} belongs to Ages {AgeRules.Roman(promise.minAge)}{(promise.maxAge >= 0 ? "-" + AgeRules.Roman(promise.maxAge) : " on")}.";
        if (!inForce) return $"{promise.name} is not in force: it needs {promise.SourceText}.";
        if (promise.traditions != null && promise.traditions.Count > 0 && !promise.traditions.Any(d => string.Equals(d, definition, StringComparison.OrdinalIgnoreCase)))
            return $"This tradition cannot be said to show {promise.name}.";
        if (!lived) return "Only a tradition practised now can be said to show a promise.";
        if (s.links.Any(l => l.Active && l.tradition == tradition)) return "It is linked to a promise already.";
        if (s.links.Count(l => l.Active) >= Math.Max(1, t.maxLinks)) return $"The council keeps {t.maxLinks} such links at most: revise one first.";
        var revised = s.links.Where(l => l.status == LinkStatus.Withdrawn && l.tradition == tradition && l.promise == promise.id).OrderByDescending(l => l.endedSeventh).FirstOrDefault();
        if (revised != null && now - revised.endedSeventh < t.relinkRest) return $"The council revised this claim at Seventh {revised.endedSeventh}: {t.relinkRest - (now - revised.endedSeventh)} more Sevenths before it can be made again.";
        return null;
    }

    /// <summary>Make the link and its first, official account (the caller checked <see cref="WhyNotLink"/>).</summary>
    public static PublicLink Link(PublicMemoryState s, PromiseSpec promise, string tradition, string traditionName, int now, string ageId)
    {
        Ensure(s);
        var link = new PublicLink
        {
            id = "link-" + s.nextLink++, tradition = tradition, traditionName = traditionName, promise = promise.id, promiseName = promise.name,
            status = LinkStatus.Active, linkedSeventh = now, linkedAge = ageId,
        };
        s.links.Add(link);
        AddAccount(s, link, AccountKind.Official, "the council", $"{traditionName} shows {promise.name}: {promise.pledge}", null, now, ageId);
        return link;
    }

    public static PublicAccount AddAccount(PublicMemoryState s, PublicLink link, AccountKind kind, string author, string text, string dispute, int now, string ageId)
    {
        int version = s.AccountsOf(link.id).Select(a => a.version).DefaultIfEmpty(0).Max() + 1;
        var a = new PublicAccount { id = "account-" + s.nextAccount++, link = link.id, version = version, kind = kind, author = author, text = text, dispute = dispute ?? string.Empty, seventh = now, ageId = ageId };
        s.accounts.Add(a);
        return a;
    }

    /// <summary>The promise is no longer in force (or the tradition is gone): the link lapses, an open dispute with it. History is kept.</summary>
    public static bool Lapse(PublicMemoryState s, PublicLink link, string reason, int now)
    {
        if (s == null || link == null || !link.Active) return false;
        link.status = LinkStatus.Lapsed;
        link.endedSeventh = now;
        link.endedReason = reason;
        foreach (var d in s.disputes.Where(d => d.link == link.id && d.Open))
        {
            d.status = DisputeStatus.Lapsed;
            d.resolvedSeventh = now;
            d.outcome = $"Left unanswered: {reason}";
        }
        return true;
    }

    // ===== DISPUTES =====

    public static PublicDispute OpenDisputeOf(PublicMemoryState s, string link) => s?.disputes.FirstOrDefault(d => d.link == link && d.Open);

    /// <summary>
    /// Whether this Seventh's comparison opens a dispute on <paramref name="link"/>: a gap, none open, the rest after the
    /// last answer passed, and a contradicting record newer than that answer (old records never reopen it).
    /// </summary>
    public static bool ShouldOpen(PublicMemoryState s, PublicLink link, PromiseComparison c, PublicMemoryTuning t, int now)
    {
        if (s == null || link == null || !link.Active || c == null || !c.Gap(t) || OpenDisputeOf(s, link.id) != null) return false;
        if (link.lastResolvedSeventh < 0) return true;
        if (now - link.lastResolvedSeventh < t.disputeRest) return false;
        return c.actions.Any(a => a.bearing == RecordBearing.Contradicts && a.seventh > link.lastResolvedSeventh);
    }

    public static PublicDispute Open(PublicMemoryState s, DisputeSpec spec, PublicLink link, PromiseComparison c, List<LocalSupport> places, PublicMemoryTuning t, int now, string ageId)
    {
        Ensure(s);
        int keep = Math.Max(1, t.recordsKept);
        var records = c.actions.Where(a => a.bearing == RecordBearing.Contradicts).Take(keep)
            .Concat(c.actions.Where(a => a.bearing == RecordBearing.Supports).Take(keep))
            .Concat(c.actions.Where(a => a.bearing == RecordBearing.Silent).Take(2)).Select(a => a.Copy()).ToList();
        var d = new PublicDispute
        {
            id = "dispute-" + s.nextDispute++, spec = spec.id, link = link.id, traditionName = link.traditionName, promiseName = link.promiseName, status = DisputeStatus.Open,
            openedSeventh = now, openedAge = ageId, accountVersion = s.CurrentAccount(link.id)?.version ?? 0, records = records,
            places = (places ?? new List<LocalSupport>()).Select(p => p.Copy()).ToList(),
        };
        s.disputes.Add(d);
        return d;
    }

    /// <summary>"{tradition}", "{promise}", "{sponsor}" filled in.</summary>
    public static string Words(string text, PublicDispute d, string sponsor = null) =>
        (text ?? string.Empty).Replace("{tradition}", d?.traditionName ?? "the tradition").Replace("{promise}", d?.promiseName ?? "the promise").Replace("{sponsor}", sponsor ?? "A Legend");

    /// <summary>
    /// Answer an open dispute: a new version of the account is added (the evidence it cited is untouched); a revision
    /// withdraws the link. Returns the new account, or null when there was nothing to answer.
    /// </summary>
    public static PublicAccount Resolve(PublicMemoryState s, PublicDispute d, DisputeSpec spec, DisputeResolution resolution, string sponsor, List<ResourceAmount> paid, int now, string ageId)
    {
        if (s == null || d == null || !d.Open || spec == null || spec.id != d.spec ||
            resolution != DisputeResolution.Acknowledge && resolution != DisputeResolution.Revise && resolution != DisputeResolution.Sponsor) return null;
        var link = s.Link(d.link);
        if (link == null || !link.Active) return null;
        AccountKind kind;
        string text;
        switch (resolution)
        {
            case DisputeResolution.Acknowledge: kind = AccountKind.Acknowledged; text = Words(spec?.acknowledgeText, d); break;
            case DisputeResolution.Revise: kind = AccountKind.Revised; text = Words(spec?.reviseText, d); break;
            default: kind = AccountKind.Sponsored; text = Words(spec?.sponsorText, d, sponsor); break;
        }
        var account = AddAccount(s, link, kind, resolution == DisputeResolution.Sponsor ? sponsor : "the council", text, d.id, now, ageId);
        d.status = DisputeStatus.Resolved;
        d.resolution = resolution;
        d.resolvedSeventh = now;
        d.sponsor = resolution == DisputeResolution.Sponsor ? sponsor : null;
        d.paid = (paid ?? new List<ResourceAmount>()).Select(p => new ResourceAmount { resource = p.resource, amount = p.amount }).ToList();
        d.outcome = text;
        link.lastResolvedSeventh = now;
        if (resolution == DisputeResolution.Revise)
        {
            link.status = LinkStatus.Withdrawn;
            link.endedSeventh = now;
            link.endedReason = "The council revised it.";
        }
        return account;
    }

    // ===== THE COUNCIL'S ACCORD =====

    /// <summary>
    /// The culture's part of the council's accord, capped at ±<see cref="PublicMemoryTuning.accordCap"/>, with each
    /// term's reason: a link the records bear out, an open dispute, a recent acknowledgement, a contested account the
    /// records still contradict. Links that ended add nothing.
    /// </summary>
    public static (float points, List<(string reason, float value)> terms) Accord(PublicMemoryState s, PublicMemoryTuning t, int now, Func<PublicLink, bool> inForce = null)
    {
        var terms = new List<(string, float)>();
        if (s == null || t == null) return (0f, terms);
        var active = s.links.Where(l => l.Active && (inForce == null || inForce(l))).ToList();
        foreach (var link in active)
        {
            var open = OpenDisputeOf(s, link.id);
            if (open != null) { terms.Add(($"Disputed: {link.traditionName} and {link.promiseName} (the records disagree)", t.openDisputeAccord)); continue; }
            var last = s.disputes.Where(d => d.link == link.id && d.status == DisputeStatus.Resolved).OrderByDescending(d => d.resolvedSeventh).FirstOrDefault();
            if (last != null && last.resolution == DisputeResolution.Sponsor && Within(last.resolvedSeventh, now, t.contestedSevenths) && link.last != null && link.last.contradicts > link.last.supports)
                terms.Add(($"Contested: {last.sponsor}'s account of {link.traditionName}, which the records still contradict", t.contestedAccord));
            else if (link.last != null && link.last.Kept)
                terms.Add(($"{link.traditionName} shows {link.promiseName}", t.keptAccord));
        }
        foreach (var d in s.disputes.Where(d => active.Any(l => l.id == d.link) && d.status == DisputeStatus.Resolved && d.resolution == DisputeResolution.Acknowledge && Within(d.resolvedSeventh, now, t.acknowledgedSevenths)))
            terms.Add(($"The council owned the gap in {d.traditionName} and {d.promiseName}", t.acknowledgedAccord));
        float sum = terms.Sum(x => x.Item2), cap = Math.Abs(t.accordCap);
        return (Math.Max(-cap, Math.Min(cap, sum)), terms);
    }

    /// <summary>Keep ended links and answered disputes bounded (accounts are kept for good).</summary>
    public static void Trim(PublicMemoryState s, PublicMemoryTuning t)
    {
        if (s == null) return;
        int keep = Math.Max(0, t.historyKept);
        var disputes = s.disputes.Where(d => !d.Open).OrderBy(d => d.resolvedSeventh).ToList();
        for (int i = 0; i < disputes.Count - keep; i++) s.disputes.Remove(disputes[i]);
        var links = s.links.Where(l => !l.Active).OrderBy(l => l.endedSeventh).ToList();
        for (int i = 0; i < links.Count - keep; i++)
            if (!s.disputes.Any(d => d.link == links[i].id)) s.links.Remove(links[i]);
    }

    public static string StanceWord(LocalStance stance) =>
        stance == LocalStance.Supports ? "supports" : stance == LocalStance.Divided ? "divided" : stance == LocalStance.Doubts ? "doubts the account" : "no record";

    private static bool PromisedCause(PromiseSpec promise, CauseFact cause) => cause.cause == promise.cause &&
        (string.IsNullOrEmpty(promise.causeSubject) || string.Equals(cause.subject, promise.causeSubject, StringComparison.OrdinalIgnoreCase));

    public static string ResolutionWord(DisputeResolution r) =>
        r == DisputeResolution.Acknowledge ? "acknowledged" : r == DisputeResolution.Revise ? "promise revised" : r == DisputeResolution.Sponsor ? "a different account sponsored" : "unanswered";
}
