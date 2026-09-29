using System;
using System.Collections.Generic;
using System.Linq;

// Apprenticeships and cultural institutions (Docs/Planning/CULTURE_REDESIGN.md, T06): who learned a tradition from
// whom, where, and what was written down. "Our traditions survive because someone learned to carry them." Saved inside
// CultureState.extensions (see TransmissionState below). Fields only; enums are saved by name, so they are append-only.
// Every number is a proposal; the teaching rules are new game rules, the named institutions rest on Civic.md.

public partial class CultureExtensionState
{
    /// <summary>Teaching orders, institutions, taught bearers, documented variants and written records (T06).</summary>
    [SaveOptionalField] public TransmissionState transmission = new TransmissionState();
}

/// <summary>What a teaching order is for. Append only.</summary>
public enum TeachingMode
{
    /// <summary>Teach the practice as it is kept: the learner becomes one more of its bearers.</summary>
    Preserve,
    /// <summary>Teach it to be made anew: the learner keeps a documented local variant (its parent is kept).</summary>
    Adapt,
    /// <summary>Write it down at an institution that keeps records (a Flavor Log, a play's script): no one learns it.</summary>
    Record,
}

/// <summary>Where a teaching order stands. Append only.</summary>
public enum TeachingStatus
{
    /// <summary>Teacher, learner and place are there: each Seventh brings it closer.</summary>
    Active,
    /// <summary>Something it needs is missing (a teacher away or lost, a place fallen): it keeps its progress and its seat.</summary>
    Paused,
    /// <summary>Done: what it made (a bearer, a variant, a record) exists, once.</summary>
    Completed,
    /// <summary>Stopped by the player: its seat is free; its history stays.</summary>
    Cancelled,
}

/// <summary>Who teaches. Append only.</summary>
public enum TeacherKind
{
    /// <summary>A Legend who carries the practice.</summary>
    Legend,
    /// <summary>The community that keeps it (a village custom needs no Legend): its people teach.</summary>
    Community,
    /// <summary>A written record, when no one living can teach it (slower; never for the most complex practices).</summary>
    Record,
}

/// <summary>Who learns. Append only.</summary>
public enum LearnerKind
{
    /// <summary>No one: a record is being written.</summary>
    None,
    Legend,
    /// <summary>A settlement's people.</summary>
    Community,
}

/// <summary>
/// A teaching order: a tradition (T01's instance id) taught by a teacher to a learner at a place, as it is or adapted,
/// or written down. It takes Sevenths and a seat at its place; it pauses, never vanishes, when something is missing.
/// </summary>
[Serializable]
public class TeachingOrder
{
    /// <summary>Stable ("teach-3"); never reused.</summary>
    public string id;
    public TeachingMode mode;
    public TeachingStatus status;
    /// <summary>The tradition taught (T01's instance id) and its name then (display only).</summary>
    public string tradition;
    public string traditionName;
    public string definition;
    /// <summary>The variant taught, when a variant rather than its parent is carried on (empty: the tradition itself).</summary>
    public string variant;
    public TeacherKind teacherKind;
    /// <summary>A Legend's name, a settlement's id, or a record's id.</summary>
    public string teacher;
    public string teacherLabel;
    public LearnerKind learnerKind;
    /// <summary>A Legend's name or a settlement's id (empty for a record).</summary>
    public string learner;
    public string learnerLabel;
    /// <summary>The institution teaching it (its id), or empty for a settlement's hearth.</summary>
    public string institution;
    /// <summary>The settlement it is taught in, and its name when the order began (a later settlement with the same id is not it).</summary>
    public int settlement = -1;
    public string settlementName;
    /// <summary>The dish taught, for a food practice (an authored or invented recipe id); an adapted variant's own dish.</summary>
    public CultureEntityRef recipe = new CultureEntityRef();
    public int complexity;
    /// <summary>Sevenths of teaching it needs, and how many it has had.</summary>
    public int required;
    public int progress;
    /// <summary>Taught from a written record, or to a practice no one keeps any more (a recovery).</summary>
    public bool recovery;
    public int startedSeventh;
    public string startedAge;
    /// <summary>The Seventh it last advanced (-1: never), completed or was cancelled.</summary>
    public int lastProgress = -1;
    public int endedSeventh = -1;
    /// <summary>Why it waits, and since when (empty while it advances).</summary>
    public string pausedReason;
    public int pausedSince = -1;
    /// <summary>What it made: a bearer's, a variant's or a record's id.</summary>
    public string result;

    public bool Open => status == TeachingStatus.Active || status == TeachingStatus.Paused;

    public TeachingOrder Copy()
    {
        var c = (TeachingOrder)MemberwiseClone();
        c.recipe = recipe?.Copy() ?? new CultureEntityRef();
        return c;
    }
}

/// <summary>An institution the people founded at a landmark (a Flavor Log, a guild of plays): more seats, records kept.</summary>
[Serializable]
public class InstitutionRecord
{
    /// <summary>Stable ("inst-2"); never reused.</summary>
    public string id;
    /// <summary>Its kind (<see cref="InstitutionSpec.id"/>).</summary>
    public string spec;
    public string name;
    /// <summary>The landmark it meets in (<see cref="CultureIds.Landmark"/>), its settlement and that settlement's name then.</summary>
    public string landmark;
    public int settlement = -1;
    public string settlementName;
    public int foundedSeventh;
    public string foundedAge;
    /// <summary>The institution it grew out of, and the one it grew into (empty: none). An evolved institution hands on its seats' work.</summary>
    public string evolvedFrom;
    public string evolvedInto;

    public InstitutionRecord Copy() => (InstitutionRecord)MemberwiseClone();
}

/// <summary>Someone who learned a tradition by teaching (a Legend or a community). Living or not is read from the world, never stored as fact.</summary>
[Serializable]
public class TaughtBearer
{
    public string id;
    public string tradition;
    /// <summary>The variant they learned (empty: the tradition as it is kept).</summary>
    public string variant;
    public LearnerKind kind;
    /// <summary>A Legend's name, or a settlement's id.</summary>
    public string who;
    public string label;
    /// <summary>The order that taught them, who taught them, when.</summary>
    public string order;
    public string taughtBy;
    public int seventh;
    public string ageId;

    public TaughtBearer Copy() => (TaughtBearer)MemberwiseClone();
}

/// <summary>A documented local form of a tradition. It keeps its parent (T01's instance id and definition) for good.</summary>
[Serializable]
public class PracticeVariant
{
    /// <summary>Stable ("var-1"); never reused.</summary>
    public string id;
    public string parent;
    public string parentDefinition;
    public string parentName;
    public string name;
    /// <summary>Where it is kept (its community), and that settlement's name then.</summary>
    public int settlement = -1;
    public string settlementName;
    /// <summary>Its own dish, for a food practice (may differ from its parent's).</summary>
    public CultureEntityRef recipe = new CultureEntityRef();
    public string order;
    public string taughtBy;
    public int seventh;
    public string ageId;

    public PracticeVariant Copy()
    {
        var c = (PracticeVariant)MemberwiseClone();
        c.recipe = recipe?.Copy() ?? new CultureEntityRef();
        return c;
    }
}

/// <summary>A tradition written down at an institution: it helps a lost practice be taught again, but no one keeps it by being written.</summary>
[Serializable]
public class WrittenRecord
{
    /// <summary>Stable ("rec-1"); never reused.</summary>
    public string id;
    public string tradition;
    public string traditionName;
    /// <summary>The variant written down (empty: the tradition itself).</summary>
    public string variant;
    public CultureEntityRef recipe = new CultureEntityRef();
    public string institution;
    public string institutionName;
    public string settlementName;
    public string order;
    public string writtenFrom;
    public int seventh;
    public string ageId;

    public WrittenRecord Copy()
    {
        var c = (WrittenRecord)MemberwiseClone();
        c.recipe = recipe?.Copy() ?? new CultureEntityRef();
        return c;
    }
}

/// <summary>Everything the culture's teaching remembers.</summary>
[Serializable]
public class TransmissionState
{
    public List<TeachingOrder> orders = new List<TeachingOrder>();
    public List<InstitutionRecord> institutions = new List<InstitutionRecord>();
    public List<TaughtBearer> bearers = new List<TaughtBearer>();
    public List<PracticeVariant> variants = new List<PracticeVariant>();
    public List<WrittenRecord> records = new List<WrittenRecord>();
    public int nextOrder = 1, nextInstitution = 1, nextBearer = 1, nextVariant = 1, nextRecord = 1;

    public TeachingOrder Order(string id) => string.IsNullOrEmpty(id) ? null : orders.FirstOrDefault(o => o != null && o.id == id);
    public InstitutionRecord Institution(string id) => string.IsNullOrEmpty(id) ? null : institutions.FirstOrDefault(i => i != null && i.id == id);
    public PracticeVariant Variant(string id) => string.IsNullOrEmpty(id) ? null : variants.FirstOrDefault(v => v != null && v.id == id);
    public WrittenRecord Record(string id) => string.IsNullOrEmpty(id) ? null : records.FirstOrDefault(r => r != null && r.id == id);
}

/// <summary>
/// A kind of place where practices are taught: a settlement's hearth (informal, everywhere, from the first Age) or an
/// institution founded at a landmark within its Ages (the vault's Culinary Alchemists and their Flavor Log, the guilds
/// of Ballad &amp; Fantasy Plays, the later Cooking Guilds). Authored in <see cref="TransmissionTuning.institutions"/>.
/// </summary>
[Serializable]
public class InstitutionSpec
{
    public string id;
    public string name;
    public string description;
    /// <summary>Informal: every settlement has one, nothing is founded (the hearth).</summary>
    public bool informal;
    /// <summary>It teaches food practices (dishes, the table) and/or the others (songs, rites, tales).</summary>
    public bool food, other;
    /// <summary>The landmarks (<see cref="LandmarkSpec.id"/>) it can be founded in (empty for the hearth).</summary>
    public List<string> venues = new List<string>();
    /// <summary>The Ages (numbers, 0 = the Age of Desolation) it can be founded in; -1: no end.</summary>
    public int minAge, maxAge = -1;
    /// <summary>Orders it can hold at once.</summary>
    public int seats = 1;
    /// <summary>The most complex practice it can teach.</summary>
    public int maxComplexity = 2;
    /// <summary>It writes practices down (records).</summary>
    public bool keepsRecords;
    /// <summary>Unity paid to found it (asked, never taken).</summary>
    public float foundingUnity;
    /// <summary>The institution it grows out of, where one stands at the same landmark (empty: none).</summary>
    public string evolvesFrom;
    public CanonStatus canon;
    /// <summary>The vault note it rests on (a path under Worldbuilding/).</summary>
    public string vault;
    public string canonNote;

    public bool OpenIn(int age) => age >= minAge && (maxAge < 0 || age <= maxAge);
    public bool Teaches(bool foodPractice) => foodPractice ? food : other;
    public string AgeText => maxAge < 0 ? (minAge <= 0 ? "every Age" : $"from Age {AgeRules.Roman(minAge)}") : $"Ages {AgeRules.Roman(minAge)}-{AgeRules.Roman(maxAge)}";
    public string CanonText => canon == CanonStatus.ExplicitCanon ? "explicit canon" : canon == CanonStatus.CanonSupported ? "canon-supported adaptation" : "new game rule (proposal)";
}

/// <summary>How hard a tradition is to carry on (1 simple: a tale; 2: a dish; 3+: needs an institution or a living teacher).</summary>
[Serializable]
public class PracticeComplexity
{
    public string definition;
    public int complexity = 1;
}

/// <summary>
/// The numbers and places of teaching. The vault gives the institutions and their Ages (Civic.md); none of these
/// numbers: every one is a proposal (Canon Gaps "Apprenticeships and institutions").
/// </summary>
[Serializable]
public class TransmissionTuning
{
    /// <summary>Sevenths of teaching per point of complexity.</summary>
    public int seventhsPerComplexity = 3;
    /// <summary>Adapting a practice is harder than passing it on as it is.</summary>
    public int adaptComplexity = 1;
    /// <summary>Taught from a record (no one living keeps it): this many times longer.</summary>
    public float recoveryFactor = 1.5f;
    /// <summary>From this complexity a practice cannot be learned from a record alone: it needs someone living who keeps it.</summary>
    public int livingTeacherFrom = 3;
    /// <summary>Complexity of a practice with no entry below: a food practice, and any other.</summary>
    public int foodComplexity = 2, otherComplexity = 1;
    /// <summary>Finished (completed or cancelled) orders kept one by one (the bearers, variants and records they made are kept for good).</summary>
    public int finishedOrdersKept = 40;

    public List<PracticeComplexity> complexity = new List<PracticeComplexity>
    {
        new PracticeComplexity { definition = "hearth-tales", complexity = 1 },
        new PracticeComplexity { definition = "evening-song", complexity = 1 },
        new PracticeComplexity { definition = "naming-of-the-lost", complexity = 1 },
        new PracticeComplexity { definition = "ash-loaf-table", complexity = 2 },
        new PracticeComplexity { definition = "national-table", complexity = 2 },
    };

    public List<InstitutionSpec> institutions = new List<InstitutionSpec>
    {
        new InstitutionSpec
        {
            id = Hearth, name = "The Hearth", informal = true, food = true, other = true, minAge = 0, seats = 1, maxComplexity = 2,
            description = "Around a settlement's fire, the ones who know show the ones who ask: a recipe at the pot, a song after the work. No one founds it.",
            canon = CanonStatus.NewGameRule, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "A new game rule: informal teaching before any institution. Civic.md's Culinary Alchemists share every recipe communally; the hearth is the game's earlier, unnamed form of that sharing.",
        },
        new InstitutionSpec
        {
            id = "flavor-log", name = "Flavor Log", food = true, venues = { "feast-hall" }, minAge = 1, maxAge = 3, seats = 2, maxComplexity = 3, keepsRecords = true, foundingUnity = 15f,
            description = "The village's cooks meet where the feasts are laid and write every combination into one shared log: which hold together, which fall apart.",
            canon = CanonStatus.ExplicitCanon, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Culinary Alchemists (Agromagical, Ages I-III), 'All the recipes are shared communally, and each village maintains a Flavor Log.' Meeting in a Feast Hall and its seats are adaptations.",
        },
        new InstitutionSpec
        {
            id = "ballad-plays", name = "Guild of Ballads and Plays", other = true, venues = { "song-hall", "amphitheatre" }, minAge = 2, maxAge = 4, seats = 2, maxComplexity = 3, keepsRecords = true, foundingUnity = 20f,
            description = "Writers, singers, poets and composers organised to make new stories and keep the Ballads of Legends: what is sung is also written.",
            canon = CanonStatus.ExplicitCanon, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Ballad & Fantasy Plays (Weaver, Ages II-IV), guilds 'designed to create culture, new stories, and preserve the Ballads of Legends'. Meeting in a Hall of Song or Amphitheatre is an adaptation.",
        },
        new InstitutionSpec
        {
            id = "cooking-guild", name = "Cooking Guild", food = true, venues = { "feast-hall", "pleasure-garden" }, minAge = 4, maxAge = 6, seats = 3, maxComplexity = 4, keepsRecords = true, foundingUnity = 40f,
            evolvesFrom = "flavor-log",
            description = "The Flavor Logs grown into guilds with masters and apprentices: the hardest dishes are taught here, and a Flavor Log standing in the same hall becomes one.",
            canon = CanonStatus.ExplicitCanon, vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Cooking Guilds (Agromagical, Ages IV-VI), the 'evolution of Culinary Alchemists'. Its seats, venues and the Flavor Log growing into it are adaptations.",
        },
    };

    public const string Hearth = "hearth";

    public InstitutionSpec Institution(string id) =>
        string.IsNullOrEmpty(id) ? null : institutions?.FirstOrDefault(i => i != null && string.Equals(i.id, id, StringComparison.OrdinalIgnoreCase));

    public InstitutionSpec HearthSpec => Institution(Hearth) ?? new InstitutionSpec { id = Hearth, name = "The Hearth", informal = true, food = true, other = true, seats = 1, maxComplexity = 2 };

    /// <summary>How hard the tradition <paramref name="definition"/> is to carry on (its entry, else the food or other default).</summary>
    public int ComplexityOf(string definition, bool food)
    {
        var entry = complexity?.FirstOrDefault(c => c != null && string.Equals(c.definition, definition, StringComparison.OrdinalIgnoreCase));
        return Math.Max(1, entry != null ? entry.complexity : food ? foodComplexity : otherComplexity);
    }
}
