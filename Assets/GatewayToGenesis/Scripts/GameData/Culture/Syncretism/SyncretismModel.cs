using System;
using System.Collections.Generic;
using System.Linq;

// Earned syncretism and specific ruin inheritance (T07, Docs/Planning/CultureRedesign/T07.md). A lost people's ways are
// read from what their ruins actually recorded (civic, district, binding, the customs and traditions that fell with
// them); two traditions that truly met and were kept may become one authored hybrid. Saved types hold fields only; enums
// are saved by name, so they are append-only.

/// <summary>What the people do when two traditions meet, or with what a ruin left. Append only.</summary>
public enum SyncretismMode
{
    /// <summary>Keep both as they are, side by side (nothing is paid, nothing is lost).</summary>
    SideBySide,
    /// <summary>Make a new form beside the old ones (the old ones go on).</summary>
    Adapt,
    /// <summary>Let the new form take the old ones' place where it is made (they fall quiet there).</summary>
    Replace,
}

/// <summary>A choice about a ruin's inheritance. Append only.</summary>
public enum InheritanceChoice
{
    /// <summary>Adopt the civic its people lived by, beside your own (WorldSystem.AdoptRuinCivic).</summary>
    PreserveCivic,
    /// <summary>Adopt its civic in place of one of yours: yours and its effects are removed.</summary>
    ReplaceCivic,
    /// <summary>Digest its ways: the culture reforms toward a way of living its records support (Digestive Rebirth).</summary>
    AdaptWays,
    /// <summary>Take up a tradition or custom its people kept, in one of your settlements.</summary>
    Revive,
    /// <summary>Nothing is known of who lived there: a smaller reform toward what its ground suggests, a guess.</summary>
    ReadTheStones,
}

/// <summary>What kind of record a ruin's clue comes from. Append only.</summary>
public enum RuinClueKind
{
    Civic,
    District,
    Binding,
    Practice,
    Tradition,
    Settlement,
    Fall,
    Ground,
}

/// <summary>
/// An authored hybrid (a deterministic compatibility rule, never generated): two parents (<c>tradition:&lt;id&gt;</c> for a
/// living tradition, T01; <c>local:&lt;id&gt;</c> for a settlement's custom, T02) and the tradition they may become.
/// </summary>
[Serializable]
public class HybridRule
{
    public string id;
    /// <summary>The two parents' keys.</summary>
    public string parentA, parentB;
    /// <summary>The tradition definition (TraditionTuning, linked only) the hybrid is.</summary>
    public string result;
    public CanonStatus canon = CanonStatus.NewGameRule;
    public string canonSource;
    public string canonNote;
    public int minAge;
    public int maxAge = -1;
    public string technology;
    /// <summary>Unity to make the new form beside the old ones, and to let it replace them.</summary>
    public float adaptUnity = 20f;
    public float replaceUnity = 10f;
    /// <summary>What each parent gives the new form (said when it is offered).</summary>
    public string fromA, fromB;

    public bool OpenIn(int age) => age >= minAge && (maxAge < 0 || age <= maxAge);
}

/// <summary>One choice made about a meeting of traditions or a ruin, with its evidence, what it kept and what it let go.</summary>
[Serializable]
public class SyncretismDecision
{
    /// <summary>"syn-1", "syn-2"... never reused.</summary>
    public string id;
    /// <summary>"hybrid:&lt;rule&gt;:&lt;place&gt;" or "ruin:&lt;id&gt;:&lt;choice&gt;[:&lt;evidence&gt;]": a decision is never made twice.</summary>
    public string key;
    public bool hybrid;
    public string rule;
    public SyncretismMode mode;
    public InheritanceChoice choice;
    /// <summary>Where it was made (-1: the nation).</summary>
    public int settlement = -1;
    public int ruin = -1;
    public string ruinName;
    /// <summary>The two parents (hybrid), or the ruin's record taken up.</summary>
    public string parentA, parentB;
    /// <summary>The tradition made or taken up (a TraditionInstance id), when one was.</summary>
    public string tradition;
    /// <summary>The family a reform turned toward, or the civics adopted and replaced.</summary>
    public string family, civic, replaced;
    /// <summary>What justified it: occurrence keys, provenance, the ruin's records.</summary>
    public List<string> evidence = new List<string>();
    public List<string> retained = new List<string>();
    public List<string> lost = new List<string>();
    public List<ResourceAmount> paid = new List<ResourceAmount>();
    public CultureStamp stamp = new CultureStamp();
    public string text;
}

/// <summary>A tradition that is a variant (a hybrid, or taken up from a ruin): its parents, depth and the Age passages it has seen.</summary>
[Serializable]
public class VariantRecord
{
    /// <summary>The TraditionInstance id.</summary>
    public string tradition;
    /// <summary>"hybrid:&lt;rule&gt;" or "ruin:&lt;id&gt;".</summary>
    public string source;
    public string parentA, parentB;
    public int ruin = -1;
    /// <summary>How many times it has been reworked (a hybrid of originals is 1; each renewal adds one; capped).</summary>
    public int depth;
    /// <summary>Completed Age passages (AgeProgression.History) when it was made or last renewed.</summary>
    public int agePasses;
    public string ageId;
    public List<string> evidence = new List<string>();
    public List<string> renewals = new List<string>();
}

/// <summary>The syncretism's saved state, in <see cref="CultureExtensionState.syncretism"/>.</summary>
[Serializable]
public class SyncretismState
{
    public const int CurrentVersion = 1;
    public int version;
    public int nextId = 1;
    public List<SyncretismDecision> decisions = new List<SyncretismDecision>();
    public List<VariantRecord> variants = new List<VariantRecord>();
    /// <summary>Offers already announced ("rule:place"): each meeting is told once.</summary>
    public List<string> announced = new List<string>();

    public VariantRecord Variant(string tradition) => string.IsNullOrEmpty(tradition) ? null : variants.FirstOrDefault(v => v != null && v.tradition == tradition);

    public SyncretismDecision Decision(string key) => string.IsNullOrEmpty(key) ? null : decisions.LastOrDefault(d => d != null && d.key == key);
}

public partial class CultureExtensionState
{
    /// <summary>Syncretism and ruin inheritance (T07).</summary>
    [SaveOptionalField] public SyncretismState syncretism = new SyncretismState();
}

// ===== WHAT THE RULES READ (snapshots built by CultureSystem; pure tests build their own) =====

/// <summary>One parent kept somewhere: a tradition instance or a settlement's custom.</summary>
public sealed class ParentPresence
{
    /// <summary>"tradition:&lt;definition&gt;" or "local:&lt;practice&gt;".</summary>
    public string key;
    /// <summary>Where it is kept (-1: the nation's own, which every settlement shares).</summary>
    public int settlement = -1;
    /// <summary>Kept there at all (false: a custom only met, whose record still shows what crossed a road).</summary>
    public bool kept = true;
    /// <summary>Kept long enough to be a custom (a lived tradition; a custom gathered for often enough).</summary>
    public bool sustained;
    /// <summary>The tradition instance, when a tradition.</summary>
    public string instance;
    /// <summary>The community keeps it as its own (preserved locally): it will not be replaced.</summary>
    public bool preserved;
    /// <summary>The nation recognised it: replacing it costs more.</summary>
    public bool recognized;
    /// <summary>For a custom: the settlement it came from and how (an exchange: road, visit, founders, arrival, table), when recorded.</summary>
    public int fromSettlement = -1;
    public bool exchanged;
    public string provenance;
    public float momentum;
}

/// <summary>A road between two settlements, open or not (T02's contact map).</summary>
public sealed class ContactLink
{
    public int a, b;
    public bool open;
}

/// <summary>A hybrid the people could make, where, and why they can (the evidence of contact and use).</summary>
public sealed class HybridOffer
{
    public HybridRule rule;
    public int settlement;
    public string place;
    public ParentPresence a, b;
    /// <summary>"together:3", "exchange:5->3:crossing-songs".</summary>
    public List<string> evidence = new List<string>();
    public string contact;
    public string Key => SyncretismRules.OfferKey(rule?.id, settlement);
}

/// <summary>A ruin as the rules read it: what its records keep (null values: not recorded).</summary>
public sealed class RuinRecord
{
    public int id;
    public string name;
    /// <summary>The ruin still stands on the map (false: only what the culture recorded remains).</summary>
    public bool onMap = true;
    public bool investigated, ancient, reclaimed;
    public SettlementKind kind;
    public string district, districtFamily;
    public string binding;
    public string civic, civicFamily;
    public bool civicAdopted, digested;
    public int foundedAge = -1, fallenAge = -1, fullAges;
    public string cause;
    /// <summary>The family its ground suggests (for a ruin nobody knows), or null.</summary>
    public string groundFamily;
    /// <summary>Customs its settlement kept (T02's former profile): id, name, family, kept (not only met).</summary>
    public List<(string id, string name, string family, bool kept)> practices = new List<(string, string, string, bool)>();
    /// <summary>Traditions that fell with it (T01, remembered in its ruins): instance, definition, name, family, established.</summary>
    public List<(string instance, string definition, string name, string family, bool established)> traditions = new List<(string, string, string, string, bool)>();
}

/// <summary>One thing a ruin's records tell, what way of living it supports (if any) and its evidence id.</summary>
public sealed class RuinClue
{
    public RuinClueKind kind;
    public string text;
    /// <summary>The way of living it supports, or null (a binding, a fall, a kind of settlement).</summary>
    public string family;
    /// <summary>"ruin:4:civic", "ruin:4:tradition:trad-7"...</summary>
    public string evidence;
    /// <summary>It can be taken up (a tradition or custom to revive).</summary>
    public bool revivable;
    /// <summary>What reviving it would take up: a tradition definition, or a custom's id.</summary>
    public string definition, practice;
}

/// <summary>What a ruin offers, read from its records.</summary>
public sealed class RuinHeritage
{
    public RuinRecord record;
    public List<RuinClue> clues = new List<RuinClue>();
    /// <summary>The ways of living its records support (each with the clues that support it).</summary>
    public List<string> families = new List<string>();
    /// <summary>What the fall took that nothing can bring back.</summary>
    public List<string> lost = new List<string>();
    /// <summary>Nothing of who lived there is known: only a guess from the ground is offered.</summary>
    public bool unknown;
    public string summary;
}

/// <summary>A choice about a ruin, as the player asks for it.</summary>
public sealed class InheritanceRequest
{
    public int ruin = -1;
    public InheritanceChoice choice;
    /// <summary>AdaptWays: the way of living to turn toward.</summary>
    public string family;
    /// <summary>Revive: the clue's evidence id.</summary>
    public string evidence;
    /// <summary>Revive: where it is taken up (-1: the nation).</summary>
    public int settlement = -1;
    /// <summary>ReplaceCivic: the civic of yours it replaces.</summary>
    public string replacing;
}
