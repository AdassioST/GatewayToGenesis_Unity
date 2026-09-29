using System;
using System.Collections.Generic;
using System.Linq;

// Public memory and civic legitimacy (Docs/Planning/CULTURE_REDESIGN.md, T09): "What we commemorate can support our laws,
// or expose the gap between our ideals and our actions." The council links a practised tradition to a promise in force
// (a stance of the Edicts, or a civic); what the culture recorded is compared with it; a gap opens a dispute the
// council answers. Accounts (what is said) are versioned records apart from evidence (what was recorded): an account
// never changes a table, an admission, a dedication, a death count or a memory. Saved inside CultureState.extensions.
// Enums are saved by name: append only. Every number is a proposal (Canon Gaps "Public memory and civic legitimacy").

public partial class CultureExtensionState
{
    /// <summary>The council's links, their accounts and the disputes over them (T09).</summary>
    [SaveOptionalField] public PublicMemoryState publicMemory = new PublicMemoryState();
}

/// <summary>What a promise is about, and so which recorded actions are compared with it. Append only.</summary>
public enum PromiseKind
{
    /// <summary>Who is received and fed: compared with the tables set and the people admitted.</summary>
    Hospitality,
    /// <summary>What is remembered: compared with the dedications to the causes remembered, and how they are kept.</summary>
    Remembrance,
}

/// <summary>Where a promise stands in the realm's law: a stance's option, or a civic. Append only.</summary>
public enum PromiseSource
{
    Stance,
    Civic,
}

/// <summary>Where a link stands. Append only.</summary>
public enum LinkStatus
{
    /// <summary>The tradition is said to show the promise: it is compared each Seventh.</summary>
    Active,
    /// <summary>The council revised it: the tradition no longer claims the promise (history kept).</summary>
    Withdrawn,
    /// <summary>The promise is no longer in force (a stance changed, a civic ended): its effect ends, its history stays.</summary>
    Lapsed,
}

/// <summary>What one version of an account is. Append only.</summary>
public enum AccountKind
{
    /// <summary>The council's first word when it linked the tradition to the promise.</summary>
    Official,
    /// <summary>The council admits the records and the promise disagree.</summary>
    Acknowledged,
    /// <summary>The council takes back the claim: the tradition no longer stands for the promise.</summary>
    Revised,
    /// <summary>A Legend sponsors a different telling. The records stay as they were.</summary>
    Sponsored,
}

/// <summary>How the council answered a dispute. Append only.</summary>
public enum DisputeResolution
{
    None,
    Acknowledge,
    Revise,
    Sponsor,
}

/// <summary>Where a dispute stands. Append only.</summary>
public enum DisputeStatus
{
    Open,
    Resolved,
    /// <summary>Its promise or link ended before an answer: kept unanswered in the history.</summary>
    Lapsed,
}

/// <summary>Whether a recorded action bears the promise out. Append only.</summary>
public enum RecordBearing
{
    Supports,
    Contradicts,
    /// <summary>Recorded, but it says nothing either way (an origin not recorded, a dormant practice).</summary>
    Silent,
}

/// <summary>How a place stands toward the account, by what was recorded there (never by who lives there). Append only.</summary>
public enum LocalStance
{
    /// <summary>Nothing relevant is recorded there: no opinion is assumed.</summary>
    NoRecord,
    Supports,
    /// <summary>Its records point both ways, or its practice lapsed.</summary>
    Divided,
    /// <summary>Its own records disagree with the account the council let stand.</summary>
    Doubts,
}

/// <summary>
/// One thing the culture recorded that a comparison saw: its source (a table, an admission, a dedication...), where and
/// when, and whether it bears the promise out. A snapshot: the source record is never changed by being cited.
/// </summary>
[Serializable]
public class RecordedAction
{
    /// <summary>The source's own id ("table:table-3", "admission:adm-4", "dedication:ded-2", "cause:crisis:age-of-desolation:1", "held:Honeyed Peach Tart").</summary>
    public string source;
    public RecordBearing bearing;
    public string text;
    /// <summary>The culture Seventh it was recorded in (-1: not known).</summary>
    public int seventh = -1;
    /// <summary>Where (a settlement's id and its name then), or -1 when no place is recorded.</summary>
    public int settlement = -1;
    public string place;

    public RecordedAction Copy() => (RecordedAction)MemberwiseClone();
}

/// <summary>A place's standing toward the account, and the recorded reason.</summary>
[Serializable]
public class LocalSupport
{
    public int settlement = -1;
    public string place;
    public LocalStance stance;
    public string reason;

    public LocalSupport Copy() => (LocalSupport)MemberwiseClone();
}

/// <summary>What the records say about one link now (derived each Seventh; the last one is kept on the link).</summary>
[Serializable]
public class PromiseComparison
{
    public int seventh = -1;
    public int supports, contradicts, silent;
    /// <summary>The practice itself is not lived now (dormant): it shows nothing either way.</summary>
    public bool practiceQuiet;
    public List<RecordedAction> actions = new List<RecordedAction>();

    /// <summary>The records contradict the promise more than they bear it out (and at least <see cref="PublicMemoryTuning.gapAt"/> do).</summary>
    public bool Gap(PublicMemoryTuning t) => !practiceQuiet && contradicts >= Math.Max(1, t.gapAt) && contradicts > supports;
    /// <summary>The records bear the promise out.</summary>
    public bool Kept => !practiceQuiet && supports > 0 && supports >= contradicts;

    public PromiseComparison Copy()
    {
        var c = (PromiseComparison)MemberwiseClone();
        c.actions = (actions ?? new List<RecordedAction>()).Select(a => a.Copy()).ToList();
        return c;
    }
}

/// <summary>
/// The council's decision: a practised tradition (T01's instance id) is said to show a promise in force. It keeps its
/// accounts' versions and its last comparison; ended, it stays in the history.
/// </summary>
[Serializable]
public class PublicLink
{
    /// <summary>Stable ("link-2"); never reused.</summary>
    public string id;
    public string tradition, traditionName;
    public string promise, promiseName;
    public LinkStatus status;
    public int linkedSeventh;
    public string linkedAge;
    public int endedSeventh = -1;
    public string endedReason;
    /// <summary>The Seventh its last dispute was answered (-1: none): only records after it can open another.</summary>
    public int lastResolvedSeventh = -1;
    public PromiseComparison last = new PromiseComparison();

    public bool Active => status == LinkStatus.Active;

    public PublicLink Copy()
    {
        var c = (PublicLink)MemberwiseClone();
        c.last = last?.Copy() ?? new PromiseComparison();
        return c;
    }
}

/// <summary>One version of what is said about a link. Versions are only ever added; the evidence is never touched.</summary>
[Serializable]
public class PublicAccount
{
    /// <summary>Stable ("account-5"); never reused.</summary>
    public string id;
    public string link;
    public int version;
    public AccountKind kind;
    /// <summary>Who gives it: "the council", or a sponsoring Legend's name.</summary>
    public string author;
    public string text;
    /// <summary>The dispute it answered (empty for the first account).</summary>
    public string dispute;
    public int seventh;
    public string ageId;

    public PublicAccount Copy() => (PublicAccount)MemberwiseClone();
}

/// <summary>
/// A disagreement between a promise and the records, put to the council. It keeps a snapshot of the records it saw and
/// how each place stood, so its outcome can be inspected after the sources are trimmed.
/// </summary>
[Serializable]
public class PublicDispute
{
    /// <summary>Stable ("dispute-3"); never reused.</summary>
    public string id;
    /// <summary>Its authored kind (<see cref="DisputeSpec.id"/>) and its story.</summary>
    public string spec;
    public string link;
    public string traditionName, promiseName;
    public DisputeStatus status;
    public int openedSeventh;
    public string openedAge;
    /// <summary>The account in force when it opened (its version).</summary>
    public int accountVersion;
    public List<RecordedAction> records = new List<RecordedAction>();
    public List<LocalSupport> places = new List<LocalSupport>();
    public DisputeResolution resolution;
    public int resolvedSeventh = -1;
    /// <summary>The sponsoring Legend (Sponsor), what was paid, and the outcome in words.</summary>
    public string sponsor;
    public List<ResourceAmount> paid = new List<ResourceAmount>();
    public string outcome;

    public bool Open => status == DisputeStatus.Open;

    public PublicDispute Copy()
    {
        var c = (PublicDispute)MemberwiseClone();
        c.records = (records ?? new List<RecordedAction>()).Select(r => r.Copy()).ToList();
        c.places = (places ?? new List<LocalSupport>()).Select(p => p.Copy()).ToList();
        c.paid = (paid ?? new List<ResourceAmount>()).Select(p => new ResourceAmount { resource = p.resource, amount = p.amount }).ToList();
        return c;
    }
}

/// <summary>Everything public memory keeps.</summary>
[Serializable]
public class PublicMemoryState
{
    public List<PublicLink> links = new List<PublicLink>();
    public List<PublicAccount> accounts = new List<PublicAccount>();
    public List<PublicDispute> disputes = new List<PublicDispute>();
    public int nextLink = 1, nextAccount = 1, nextDispute = 1;

    public PublicLink Link(string id) => string.IsNullOrEmpty(id) ? null : links.FirstOrDefault(l => l != null && l.id == id);
    public PublicDispute Dispute(string id) => string.IsNullOrEmpty(id) ? null : disputes.FirstOrDefault(d => d != null && d.id == id);
    public IEnumerable<PublicAccount> AccountsOf(string link) => accounts.Where(a => a != null && a.link == link).OrderBy(a => a.version);
    public PublicAccount CurrentAccount(string link) => AccountsOf(link).LastOrDefault();
}

/// <summary>
/// A promise the council can link a tradition to: a stance's option of the Edicts, or a civic, while it is in force.
/// Authored in <see cref="PublicMemoryTuning.promises"/>.
/// </summary>
[Serializable]
public class PromiseSpec
{
    public string id;
    public string name;
    /// <summary>What it promises, in the people's words.</summary>
    public string pledge;
    public PromiseKind kind;
    public PromiseSource source;
    /// <summary>The stance and option (<see cref="EdictCatalog"/>), or the civic's name.</summary>
    public string stance, option, civic;
    /// <summary>Tradition definitions that can be said to show it.</summary>
    public List<string> traditions = new List<string>();
    /// <summary>A remembrance promise: the kind of cause (T03) it promises to keep.</summary>
    public MemoryCause cause = MemoryCause.Crisis;
    /// <summary>Optional recorded subject within that kind; a Hunger promise must not count a different crisis.</summary>
    public string causeSubject;
    /// <summary>Ages it can be linked in (numbers); -1: no end.</summary>
    public int minAge, maxAge = -1;
    public CanonStatus canon;
    public string vault, canonNote;

    public bool OpenIn(int age) => age >= minAge && (maxAge < 0 || age <= maxAge);
    public string SourceText => source == PromiseSource.Civic ? $"the civic {civic}" : $"the stance {EdictCatalog.Stance(stance)?.title ?? stance}: {EdictCatalog.Stance(stance)?.Option(option)?.title ?? option}";
}

/// <summary>An authored dispute: the story it is told in and the three answers' words and costs.</summary>
[Serializable]
public class DisputeSpec
{
    public string id;
    public string title;
    public PromiseKind kind;
    /// <summary>The Ink knot that tells it (locked; unlocked when it opens).</summary>
    public string story;
    public string summary;
    /// <summary>What each answer says (the account's new text; {tradition} and {promise} are filled in).</summary>
    public string acknowledgeText, reviseText, sponsorText;
    public float acknowledgeUnity = 10f, sponsorUnity = 20f;
    public CanonStatus canon;
    public string vault, canonNote;
}

/// <summary>The numbers and content of public memory. Every number is a proposal.</summary>
[Serializable]
public class PublicMemoryTuning
{
    /// <summary>Sevenths of records a comparison looks back over.</summary>
    public int window = 42;
    /// <summary>Contradicting records (and more than those that bear it out) that make a gap.</summary>
    public int gapAt = 2;
    /// <summary>Links active at once; Unity to make one.</summary>
    public int maxLinks = 3;
    public float linkUnity = 10f;
    /// <summary>Sevenths after an answer before the same link can be disputed again (and only by newer records).</summary>
    public int disputeRest = 42;
    /// <summary>Sevenths before a revised tradition and promise can be linked again.</summary>
    public int relinkRest = 63;
    /// <summary>Records of each bearing kept in a dispute's snapshot.</summary>
    public int recordsKept = 6;
    /// <summary>Disputes and ended links kept one by one (accounts are kept for good).</summary>
    public int historyKept = 30;

    // The culture's part of the council's accord (EdictSystem), each explained; capped both ways.
    public float keptAccord = 3f, openDisputeAccord = -5f, acknowledgedAccord = 2f, contestedAccord = -3f;
    /// <summary>Sevenths an acknowledgement is remembered in the accord, and a contested account weighs on it.</summary>
    public int acknowledgedSevenths = 21, contestedSevenths = 42;
    public float accordCap = 10f;

    /// <summary>The civic (Civic.md, Harmonic Quorum) under which a council in discord cannot pass a revision or a sponsored account.</summary>
    public string quorumCivic = "Harmonic Quorum";

    public List<PromiseSpec> promises = new List<PromiseSpec>
    {
        new PromiseSpec
        {
            id = "open-gate", name = "The Open Gate", kind = PromiseKind.Hospitality, source = PromiseSource.Stance, stance = "strangers", option = "welcome",
            pledge = "Whoever comes to the gate is received and fed.", traditions = { "ash-loaf-table", TraditionRules.NationalTable },
            canon = CanonStatus.NewGameRule, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "A new game rule over the Edicts' Welcome the Wanderers (itself a proposal). The Ash-Loaf set out 'for whoever comes' is The Inescapable Hunger's mourning bread.",
        },
        new PromiseSpec
        {
            id = "almshouse", name = "The Almshouse Charter", kind = PromiseKind.Hospitality, source = PromiseSource.Stance, stance = "roofless", option = "almshouses",
            pledge = "The granaries feed those without a roof.", traditions = { "ash-loaf-table", TraditionRules.NationalTable },
            canon = CanonStatus.NewGameRule, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "A new game rule over the Edicts' Almshouse Charter (a proposal).",
        },
        new PromiseSpec
        {
            id = "feast-of-abundance", name = "The Feast of Abundance", kind = PromiseKind.Hospitality, source = PromiseSource.Civic, civic = "Feast of Abundance", minAge = 1, maxAge = 3,
            pledge = "Surplus is given to the squares; wealth held back is hoarding.", traditions = { "ash-loaf-table", TraditionRules.NationalTable },
            canon = CanonStatus.ExplicitCanon, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Feast of Abundance (Indulgent, Ages I-III): a mandatory redistribution; 'Wealth held past the festival date is taxed as hoarding'; those who give most 'earn higher social legitimacy'; 'some communities maintain records of each feast's yields and distribution'. Comparing it with the tables recorded is an adaptation.",
        },
        new PromiseSpec
        {
            id = "lesson-of-hunger", name = "The Lesson of the Hunger", kind = PromiseKind.Remembrance, source = PromiseSource.Stance, stance = "cradle", option = "measured_cradle",
            causeSubject = "The Inescapable Hunger",
            pledge = "We do not forget the Hunger, and we live by its lesson.", traditions = { "ash-loaf-table", "naming-of-the-lost" },
            canon = CanonStatus.CanonSupported, vault = "Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            canonNote = "The Measured Cradle stance names 'the lesson of The Inescapable Hunger'; the vault's mourning bakeries and names spoken for the starved are the memory it claims. Comparing it with the dedications is a new game rule.",
        },
    };

    public List<DisputeSpec> disputes = new List<DisputeSpec>
    {
        new DisputeSpec
        {
            id = "hollow-table", title = "The Hollow Table", kind = PromiseKind.Hospitality, story = "public_memory_hollow_table",
            summary = "The council said {tradition} shows {promise}. The records of the tables say otherwise.",
            acknowledgeText = "The council admits it: {tradition} has not been the open table {promise} promised. The records stand as they are.",
            reviseText = "{tradition} no longer stands for {promise}. The council will not claim what the tables do not show.",
            sponsorText = "{sponsor} tells it another way: the private tables were thanks to those who gave, and {tradition} still shows {promise}. The records are unchanged.",
            acknowledgeUnity = 10f, sponsorUnity = 20f,
            canon = CanonStatus.CanonSupported, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "The Ballad of Hollow Banquet (Feast of Abundance): hoarded wealth and a distribution that buys loyalty. The dispute and its answers are adaptations.",
        },
        new DisputeSpec
        {
            id = "unbaked-loaf", title = "The Loaf No One Bakes", kind = PromiseKind.Remembrance, story = "public_memory_unbaked_loaf",
            summary = "The council said {tradition} shows {promise}. The records of the memorials say it is not kept.",
            acknowledgeText = "The council admits it: {promise} was said more than it was kept. What was lost is still recorded as it was.",
            reviseText = "{tradition} no longer stands for {promise}. The council will say only what the memorials show.",
            sponsorText = "{sponsor} tells it another way: the memory is kept in the heart, not the oven, and {tradition} still shows {promise}. The records are unchanged.",
            acknowledgeUnity = 10f, sponsorUnity = 20f,
            canon = CanonStatus.CanonSupported, vault = "Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            canonNote = "The Inescapable Hunger: mourning bakeries and recipes dedicated to those who starved. A promise of remembrance tested against the dedications is an adaptation.",
        },
    };

    public PromiseSpec Promise(string id) => string.IsNullOrEmpty(id) ? null : promises?.FirstOrDefault(p => p != null && string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));
    public DisputeSpec DisputeFor(PromiseKind kind) => disputes?.FirstOrDefault(d => d != null && d.kind == kind);
    public DisputeSpec Dispute(string id) => string.IsNullOrEmpty(id) ? null : disputes?.FirstOrDefault(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
}
