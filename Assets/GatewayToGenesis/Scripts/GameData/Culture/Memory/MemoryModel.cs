using System;
using System.Collections.Generic;
using System.Linq;

// Shared wounds, memorial places and inheritance (Docs/Planning/CULTURE_REDESIGN.md, T03): what the culture remembers
// of what it lost, kept apart from what it does to remember it. Saved inside CultureState.extensions (see
// MemoryState below). Every number is a proposal; the records and recovery rules are adaptations, not canon.

/// <summary>The kinds of historical cause the culture keeps as a shared wound. Append only: saves store the name.</summary>
public enum MemoryCause
{
    /// <summary>An Age Crisis the people lived through (The Inescapable Hunger, the Great Plague).</summary>
    Crisis,
    /// <summary>A settlement of theirs that Surrendered and fell into ruins.</summary>
    SettlementFall,
    /// <summary>The ruins of a fallen settlement of theirs, founded on again: what was lost reclaimed.</summary>
    RuinRecovered,
    /// <summary>A Legend whose Soul Leitmotif reached Surrender: lost to Dissonance, never met again.</summary>
    LegendLost,
}

/// <summary>Where a dedication stands. Append only.</summary>
public enum DedicationStatus
{
    /// <summary>Its dish, landmark or observance exists: keeping it keeps the memory.</summary>
    Active,
    /// <summary>What it was dedicated through is gone for now (a landmark's settlement fell): kept, not deleted; it wakes if the thing returns.</summary>
    Dormant,
    /// <summary>The people released it: the record stays in the history, it no longer counts as practice.</summary>
    Released,
}

/// <summary>
/// A historical cause the culture remembers: written once when it is learned and never changed afterwards (accounts,
/// dedications and practice are separate records that point at it by <see cref="id"/>). Unknown values stay unknown: a
/// cause read from an older record after the fact has no <see cref="occurred"/> stamp, only the Age when known.
/// </summary>
[Serializable]
public class MemoryEvidence
{
    /// <summary>Stable and unique: "crisis:age-of-desolation:1", "fall:7", "recovered:7", "legend-lost:Vaelia".</summary>
    public string id;
    public MemoryCause cause;
    public string title;
    public string text;
    /// <summary>What it is about, for the player (the crisis, the settlement's name, the Legend's name).</summary>
    public string subject;
    /// <summary>The thing in the world it rests on (Age:&lt;id&gt;, Ruin:&lt;id&gt;, Legend:&lt;name&gt;).</summary>
    public CultureEntityRef source = new CultureEntityRef();
    /// <summary>Another cause this one follows from (a recovery points at its fall), or null.</summary>
    public string related;
    /// <summary>The world cell it happened at (a ruin's), or -1 when it has no place.</summary>
    public int cell = -1;
    /// <summary>People lost in it when counted (a crisis), else 0.</summary>
    public int deaths;
    /// <summary>The Age it happened in, when known.</summary>
    public string occurredAge;
    /// <summary>When it happened, when it was witnessed as it happened; null when it was learned later.</summary>
    public CultureStamp occurred;
    /// <summary>When the culture recorded it.</summary>
    public CultureStamp recorded;
    /// <summary>Read from an older record after the fact (a save made before the culture kept memories): its moment is not known.</summary>
    public bool reconstructed;
    /// <summary>
    /// A cause the Stellar Legacy Score could one day canonize as a World Truth (a Legend's story). Only a link for a
    /// system not built yet: nothing canonizes it automatically.
    /// </summary>
    public bool worldTruthCandidate;

    public CultureEntityRef Ref => CultureEntityRef.Of(CultureEntityKind.Evidence, id, title);

    public MemoryEvidence Copy() => new MemoryEvidence
    {
        id = id, cause = cause, title = title, text = text, subject = subject, source = source?.Copy() ?? new CultureEntityRef(), related = related,
        cell = cell, deaths = deaths, occurredAge = occurredAge, occurred = occurred?.Copy(), recorded = recorded?.Copy(),
        reconstructed = reconstructed, worldTruthCandidate = worldTruthCandidate,
    };
}

/// <summary>
/// The people dedicated one of their own practices (a dish, a landmark, a rite or a holiday) to a cause: a lasting link,
/// not a new recipe. The target is kept by id, so a renamed dish is still the same dish.
/// </summary>
[Serializable]
public class MemorialDedication
{
    /// <summary>"ded-1", "ded-2"... never reused.</summary>
    public string id;
    /// <summary>The cause's <see cref="MemoryEvidence.id"/>.</summary>
    public string evidence;
    /// <summary>What was dedicated (Recipe, Landmark, Activity or Holiday); its label is the name it had then.</summary>
    public CultureEntityRef target = new CultureEntityRef();
    public CultureStamp dedicated;
    public DedicationStatus status;
    /// <summary>Why it sleeps, when <see cref="DedicationStatus.Dormant"/>.</summary>
    public string dormantReason;
    /// <summary>Sevenths it was kept in, and the last one (culture Seventh; -1: never).</summary>
    public int practiced;
    public int lastPracticed = -1;
    /// <summary>The Ages it has lived through, the one it was made in first (its lineage).</summary>
    public List<string> ages = new List<string>();
    /// <summary>It came over an Age passage from the Age before.</summary>
    public bool inherited;
    /// <summary>The Age that recognized it as its own inheritance (the player's choice), or null while it waits.</summary>
    public string recognizedAge;

    public bool Counts => status == DedicationStatus.Active;

    public MemorialDedication Copy() => new MemorialDedication
    {
        id = id, evidence = evidence, target = target?.Copy() ?? new CultureEntityRef(), dedicated = dedicated?.Copy(), status = status,
        dormantReason = dormantReason, practiced = practiced, lastPracticed = lastPracticed, ages = new List<string>(ages ?? new List<string>()),
        inherited = inherited, recognizedAge = recognizedAge,
    };
}

/// <summary>How a cause has been kept: the Sevenths the people remembered it, and how (quiet remembrance, a dedicated dish...).</summary>
[Serializable]
public class MemoryPractice
{
    public string evidence;
    public int kept;
    /// <summary>The culture Seventh it was last kept in (-1: never).</summary>
    public int last = -1;
    /// <summary>How it was kept last, in words.</summary>
    public string lastHow;
    /// <summary>The last quiet remembrance (culture Seventh; -1: never).</summary>
    public int lastQuiet = -1;

    public MemoryPractice Copy() => new MemoryPractice { evidence = evidence, kept = kept, last = last, lastHow = lastHow, lastQuiet = lastQuiet };
}

/// <summary>What the culture carried over one Age passage: the causes it remembered, its dedications as they stood, and its practices.</summary>
[Serializable]
public class MemoryLineage
{
    /// <summary>"lineage:&lt;age&gt;:&lt;pass&gt;": one per passage (the pass count of AgeProgression's history).</summary>
    public string id;
    public string ageId, ageTitle, nextAgeId, nextAgeTitle;
    public int pass;
    public CultureStamp stamp;
    public List<string> evidence = new List<string>();
    public List<MemorialDedication> dedications = new List<MemorialDedication>();
    /// <summary>The practices the culture lived when the Age passed (holidays, rites, national foods, landmarks, traditions).</summary>
    public List<CultureEntityRef> practices = new List<CultureEntityRef>();
}

/// <summary>Everything the culture remembers of its wounds (saved in <see cref="CultureExtensionState.memory"/>).</summary>
[Serializable]
public class MemoryState
{
    public List<MemoryEvidence> evidence = new List<MemoryEvidence>();
    public List<MemorialDedication> dedications = new List<MemorialDedication>();
    public List<MemoryPractice> practice = new List<MemoryPractice>();
    public List<MemoryLineage> lineage = new List<MemoryLineage>();
    public int nextDedication = 1;

    public MemoryEvidence Evidence(string id) => string.IsNullOrEmpty(id) ? null : evidence.FirstOrDefault(e => e != null && string.Equals(e.id, id, StringComparison.Ordinal));
    public MemorialDedication Dedication(string id) => string.IsNullOrEmpty(id) ? null : dedications.FirstOrDefault(d => d != null && string.Equals(d.id, id, StringComparison.Ordinal));
    public MemoryPractice PracticeOf(string evidenceId) => practice.FirstOrDefault(p => p != null && string.Equals(p.evidence, evidenceId, StringComparison.Ordinal));
    public IEnumerable<MemorialDedication> DedicationsOf(string evidenceId) => dedications.Where(d => d != null && d.evidence == evidenceId);
}

/// <summary>Where the culture keeps its memory: its part of the shared extension envelope (T01 owns the envelope).</summary>
public partial class CultureExtensionState
{
    [SaveOptionalField] public MemoryState memory = new MemoryState();
}

/// <summary>
/// The memory's numbers (code defaults; every one a proposal): how long a practice keeps a wound's recovery going, how
/// much it gives and the cap over all wounds, how often a quiet remembrance counts, how many practices a wound may hold.
/// </summary>
[Serializable]
public class MemoryTuning
{
    /// <summary>Sevenths a kept memory supports recovery after it was last kept (21: a Phase). Remembrance lapses; the wound stays.</summary>
    public int practiceWindowSevenths = 21;
    /// <summary>Morale each remembered wound supports while it is kept...</summary>
    public int moralePerWound = 1;
    /// <summary>...and the most all of them together ever give (many losses cannot stack recovery).</summary>
    public int moraleCap = 3;
    /// <summary>A quiet remembrance counts once per wound every this many Sevenths.</summary>
    public int quietCooldownSevenths = 7;
    /// <summary>Practices one wound can be dedicated through at once.</summary>
    public int maxDedicationsPerWound = 3;
    /// <summary>Cells around a memorial place searched for living blooms (its garden).</summary>
    public int gardenReach = 2;
    /// <summary>Vigor at which a bloom counts as living well.</summary>
    public float gardenVigor = 0.5f;
}

/// <summary>A fitting memorial the vault itself names, suggested (never required) when a matching cause is remembered.</summary>
public sealed class MemorialSuggestion
{
    public readonly MemoryCause cause;
    /// <summary>The crisis title it belongs to (Crisis causes only), or null for any.</summary>
    public readonly string crisis;
    public readonly CultureEntityKind kind;
    public readonly string target;
    /// <summary>Whether the vault says so, supports it, or it is the game's own rule.</summary>
    public readonly CanonStatus status;
    /// <summary>The vault note it rests on.</summary>
    public readonly string source;
    public readonly string text;

    public MemorialSuggestion(MemoryCause cause, string crisis, CultureEntityKind kind, string target, CanonStatus status, string source, string text)
    {
        this.cause = cause; this.crisis = crisis; this.kind = kind; this.target = target; this.status = status; this.source = source; this.text = text;
    }

    /// <summary>The label in words: "explicit canon", "canon-supported adaptation" or "new game rule".</summary>
    public string StatusText => status == CanonStatus.ExplicitCanon ? "explicit canon" : status == CanonStatus.CanonSupported ? "canon-supported adaptation" : "new game rule";

    public bool Fits(MemoryEvidence e) => e != null && e.cause == cause && (crisis == null || string.Equals(crisis, e.subject, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The authored memorials. The Ash-Loaf after the Hunger is explicit canon (The Inescapable Hunger.md: mourning bakeries
    /// bake Ash-Loaves "dedicated to those who starved"; they arise in the Age of Renewal, which is when this cause is
    /// first remembered). The rest are adaptations.
    /// </summary>
    public static readonly IReadOnlyList<MemorialSuggestion> All = new[]
    {
        new MemorialSuggestion(MemoryCause.Crisis, "The Inescapable Hunger", CultureEntityKind.Recipe, "ash-loaf", CanonStatus.ExplicitCanon,
            "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            "The mourning bakeries bake Ash-Loaves for those who starved: food as memorial and refusal."),
        new MemorialSuggestion(MemoryCause.Crisis, "The Inescapable Hunger", CultureEntityKind.Activity, "remembrance", CanonStatus.CanonSupported,
            "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            "The names of those the Hunger took, spoken one by one."),
        new MemorialSuggestion(MemoryCause.SettlementFall, null, CultureEntityKind.Landmark, "shrine", CanonStatus.CanonSupported,
            "Worldbuilding/World Environment/Bestiary/Eleos Bloom.md",
            "A place kept for the fallen, where Candlevein Blooms may take root around vigils and graves if their seeds are near."),
        new MemorialSuggestion(MemoryCause.LegendLost, null, CultureEntityKind.Activity, "song", CanonStatus.CanonSupported,
            "Worldbuilding/Society/Stellar Legacy/Stellar Legacy Score.md",
            "\"A life is not measured in years, but in the Ballads that are sung after.\""),
    };
}

/// <summary>A living bloom near a memorial place, and whether its own needs are met there (its species' constraints, not the memorial's wish).</summary>
public class MemorialBloom
{
    public string site, spec, name;
    public int distance;
    public float vigor, residue, need;
    /// <summary>Its needs are met where it stands and it lives well.</summary>
    public bool thriving;
    /// <summary>A bloom that feeds on or is shaped by sorrow (drawn by grief, or needing hurt): a comforting memorial does not feed it.</summary>
    public bool griefLinked;
    public string why;
}

/// <summary>The ecology of a memorial place as it is (read only: nothing here changes the world).</summary>
public class MemorialGround
{
    public int cell = -1;
    public string place;
    public readonly List<MemorialBloom> blooms = new List<MemorialBloom>();
    /// <summary>How Lumen Seeds reach the settlement there ("seeds", "coherence", "pollinators"), or null.</summary>
    public string seedSource;
    /// <summary>At least one bloom lives well here: a memorial garden in fact, not in name.</summary>
    public bool Suitable => blooms.Any(b => b.thriving);
    /// <summary>
    /// The garden's ecological bonus. The first release shows the garden and its suitability only: no numerical bonus, and
    /// never one without a suitable bloom (T03 acceptance).
    /// </summary>
    public float Bonus => 0f;
}
