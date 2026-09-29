using System;
using System.Collections.Generic;

// Local cultures and routes of exchange (T02): what each settlement keeps, how it came to know it, the customs cultural
// parties carry, and who arrived where. Saved inside CultureExtensionState (fields only; enums appended, never reordered).

public partial class CultureExtensionState
{
    /// <summary>Local cultures: settlement profiles, carried repertoires, arrivals (T02).</summary>
    [SaveOptionalField] public LocalCultureState local = new LocalCultureState();
}

/// <summary>How far a community has taken a custom in. Append only.</summary>
public enum LocalPracticeStage
{
    /// <summary>They have seen or heard it (exposure), but do not keep it.</summary>
    Exposed,
    /// <summary>They keep it: gatherings of it are theirs.</summary>
    Practiced,
    /// <summary>They kept it, but no one has gathered for it in a long while. Known still; a gathering revives it.</summary>
    Quiet,
}

/// <summary>How a custom first reached a community. Append only.</summary>
public enum PracticeChannel
{
    /// <summary>Not recorded (never invented: an older save, or a source that left no trace).</summary>
    Unknown,
    /// <summary>It began here, from this ground.</summary>
    Originated,
    /// <summary>Along a road from a settlement that keeps it.</summary>
    Road,
    /// <summary>A cultural party carried it here and performed it at a festival.</summary>
    Visit,
    /// <summary>The founders brought it: settlers, or a hub's people raising a hamlet.</summary>
    Founders,
    /// <summary>People admitted here from a settlement that keeps it (returning settlers).</summary>
    Arrival,
    /// <summary>Guests at an open communal table (T05): neighbours by road who came, or the hosts they met there.</summary>
    Table,
    /// <summary>Taken up from what a fallen people's ruins record (T07): learned anew, not carried by anyone.</summary>
    Inherited,
}

/// <summary>Who introduced a custom to a community, and when: visible provenance, unknown values left unknown.</summary>
[Serializable]
public class PracticeProvenance
{
    public PracticeChannel channel;
    /// <summary>The settlement it came from (-1: none or unknown), and its name then.</summary>
    public int fromSettlement = -1;
    public string fromName;
    /// <summary>The party, road or arrival that carried it ("Cultural Party of Vaelia", "the road from Ashford").</summary>
    public string carrier;
    /// <summary>Legends who carried it, when known (empty: not recorded).</summary>
    public List<string> legends = new List<string>();
    /// <summary>When (Sevenths since the founding) and in which Age.</summary>
    public int seventh;
    public string ageId;

    public PracticeProvenance Copy() => new PracticeProvenance
    {
        channel = channel, fromSettlement = fromSettlement, fromName = fromName, carrier = carrier,
        legends = legends != null ? new List<string>(legends) : new List<string>(), seventh = seventh, ageId = ageId,
    };
}

/// <summary>One custom as a community knows it: how well, how much they take part, and where it came from.</summary>
[Serializable]
public class LocalPractice
{
    public string practice;
    public LocalPracticeStage stage;
    /// <summary>How well they know it, 0-1 (fades without contact while they do not keep it).</summary>
    public float exposure;
    /// <summary>How much they have taken part lately, 0-1 (fades each Seventh).</summary>
    public float participation;
    /// <summary>How it first reached them (never rewritten).</summary>
    public PracticeProvenance origin = new PracticeProvenance();
    /// <summary>Sevenths (since the founding) it was taken up (-1: not kept yet) and last gathered for (-1: never).</summary>
    public int adoptedSeventh = -1;
    public int lastGathered = -1;
    /// <summary>Gatherings held for it here, all told.</summary>
    public int gatherings;
    /// <summary>This settlement's own form of it ("the Riverside Evening of Song"): kept when the nation recognises the custom.</summary>
    public string variant;
    /// <summary>The nation recognised the custom as its own (its local form stays this settlement's).</summary>
    public bool recognized;

    public bool Kept => stage == LocalPracticeStage.Practiced || stage == LocalPracticeStage.Quiet;
}

/// <summary>A settlement's own customs (its profile). Kept after the settlement falls: its history is not erased.</summary>
[Serializable]
public class SettlementProfile
{
    public int settlement;
    /// <summary>Its name when last seen (a fallen settlement keeps it).</summary>
    public string name;
    public List<LocalPractice> practices = new List<LocalPractice>();
    /// <summary>The Seventh of its last local gathering (-1: never), for the gathering cooldown.</summary>
    public int lastGathering = -1;
    /// <summary>It no longer stands (fallen or gone): its customs are history.</summary>
    public bool gone;
}

/// <summary>A custom a cultural party carries, and where it took it up.</summary>
[Serializable]
public class CarriedPractice
{
    public string practice;
    public int fromSettlement = -1;
    public string fromName;
    public int seventh;
}

/// <summary>The customs a cultural party carries (keyed by its unit id; dropped when the party is gone).</summary>
[Serializable]
public class PartyRepertoire
{
    public int unit;
    public string unitName;
    public List<CarriedPractice> carried = new List<CarriedPractice>();
}

/// <summary>Where the settlers an expedition escorts were taken on (so their customs travel with them).</summary>
[Serializable]
public class SettlersOrigin
{
    public int unit;
    public int settlement;
    public string name;
}

/// <summary>What is known of where arriving people came from. Append only.</summary>
public enum ArrivalOriginKind
{
    /// <summary>Nothing is known: their customs are not recorded, and none is assumed.</summary>
    Unknown,
    /// <summary>One of your settlements (returning settlers): its customs are known.</summary>
    Settlement,
    /// <summary>A place in the world (survivors found there): where, but not what they keep.</summary>
    Place,
}

/// <summary>People admitted to a settlement, as the population's admission reported them.</summary>
[Serializable]
public class ArrivalRecord
{
    public string key;
    /// <summary>Where they were admitted (-1: unknown).</summary>
    public int settlement = -1;
    public int people;
    public ArrivalOriginKind originKind;
    /// <summary>The origin's id ("3" for a settlement, a cell index for a place) and its name, when known.</summary>
    public string originId;
    public string originLabel;
    /// <summary>How they came ("caravan", "survivors", "settlers", "founding", "story").</summary>
    public string channel;
    public int seventh;
    public string ageId;
}

/// <summary>Older arrivals folded into one line per settlement and origin kind (the record stays bounded).</summary>
[Serializable]
public class ArrivalSummary
{
    public int settlement = -1;
    public ArrivalOriginKind originKind;
    public int people;
    public int arrivals;
}

/// <summary>Everything local cultures remember.</summary>
[Serializable]
public class LocalCultureState
{
    public List<SettlementProfile> profiles = new List<SettlementProfile>();
    public List<PartyRepertoire> repertoires = new List<PartyRepertoire>();
    public List<SettlersOrigin> settlers = new List<SettlersOrigin>();
    public List<ArrivalRecord> arrivals = new List<ArrivalRecord>();
    public List<ArrivalSummary> olderArrivals = new List<ArrivalSummary>();
    /// <summary>Visits, foundings and arrivals already applied (bounded): each transfers its exposure once.</summary>
    public List<string> applied = new List<string>();
    /// <summary>Profiles of settlements that no longer stand: their customs are history (a later settlement may reuse the id).</summary>
    [SaveOptionalField] public List<SettlementProfile> former = new List<SettlementProfile>();
}
