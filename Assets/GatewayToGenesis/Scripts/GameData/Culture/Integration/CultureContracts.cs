using System;
using System.Collections.Generic;
using System.Linq;

// The culture's shared contract (Docs/Planning/CULTURE_REDESIGN.md, "Freeze this minimum contract"): stable references,
// calendar stamps, committed occurrences and command results used by every culture feature (traditions, local
// cultures, memory, observances, hospitality, teaching...). Saved types hold fields only (no auto-properties, no
// Nullable<T>); enums are saved by name, so they are append-only.

/// <summary>What a <see cref="CultureEntityRef"/> points at. Append only: saves store the name.</summary>
public enum CultureEntityKind
{
    None,
    Nation,
    Tradition,
    Settlement,
    Legend,
    Recipe,
    Resource,
    Landmark,
    Activity,
    Holiday,
    Observance,
    Ruin,
    Moment,
    Occurrence,
    Civic,
    Age,
    Dedication,
    Evidence,
    Account,
}

/// <summary>
/// A stable reference to something the culture remembers: a kind and an id (never a display name used as a key, except
/// where the thing has no id yet: a holiday is its name until observances give it one). The label is only for display
/// and may be stale.
/// </summary>
[Serializable]
public class CultureEntityRef
{
    public CultureEntityKind kind;
    public string id;
    public string label;

    public static CultureEntityRef None => new CultureEntityRef();

    public static CultureEntityRef Of(CultureEntityKind kind, string id, string label = null) =>
        new CultureEntityRef { kind = kind, id = id, label = label };

    /// <summary>It names something (a kind and an id).</summary>
    public bool IsKnown => kind != CultureEntityKind.None && !string.IsNullOrEmpty(id);

    /// <summary>"Recipe:ash-loaf": the reference as one comparable string.</summary>
    public string Key => IsKnown ? $"{kind}:{id}" : string.Empty;

    /// <summary>The same thing (kind and id; ids compare without case).</summary>
    public bool Same(CultureEntityRef other) =>
        other != null && IsKnown && other.IsKnown && kind == other.kind && string.Equals(id, other.id, StringComparison.OrdinalIgnoreCase);

    public string Display => !string.IsNullOrEmpty(label) ? label : IsKnown ? id : "unknown";

    public CultureEntityRef Copy() => new CultureEntityRef { kind = kind, id = id, label = label };

    public override string ToString() => IsKnown ? Key : "(unknown)";
}

/// <summary>How the culture's features spell the ids of things that have none of their own.</summary>
public static class CultureIds
{
    public static string Settlement(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A landmark: one of each kind per settlement ("12:shrine").</summary>
    public static string Landmark(int settlement, string spec) => $"{Settlement(settlement)}:{spec}";
}

/// <summary>
/// When something happened, on the game's own calendar (never the wall clock): the Seventh counted from the culture's
/// founding, the Cycle / Echo / Phase / Seventh of the world, and the Age. <see cref="Absolute"/> orders stamps.
/// </summary>
[Serializable]
public class CultureStamp
{
    /// <summary>Sevenths since the culture's founding (<see cref="CultureState.sevenths"/>).</summary>
    public int cultureSeventh;
    public int cycle = 1, echo = 1, phase = 1, seventh = 1;
    public string ageId;

    /// <summary>The Seventh's index since the world's first (Cycle 1, Echo 1, Phase 1, Seventh 1 is 0).</summary>
    public long Absolute => Index(cycle, echo, phase, seventh);

    public static long Index(int cycle, int echo, int phase, int seventh) =>
        (((long)Math.Max(0, cycle - 1) * TimeSystemLogic.EchoesPerCycle + Math.Max(0, echo - 1)) * TimeSystemLogic.PhasesPerEcho + Math.Max(0, phase - 1))
        * TimeSystemLogic.SeventhsPerPhase + Math.Max(0, seventh - 1);

    public CultureStamp Copy() => new CultureStamp { cultureSeventh = cultureSeventh, cycle = cycle, echo = echo, phase = phase, seventh = seventh, ageId = ageId };

    public override string ToString() => $"Seventh {cultureSeventh} (the {CultureCalendar.Ordinal(seventh)} Seventh of Phase {phase}, Echo {echo}, Cycle {cycle})";
}

/// <summary>
/// Why a committed action counts for the culture. Each purpose is its own kind so the same material is never counted
/// twice as different participation (ingredients cooked are Production; a dish eaten is Consumption; the same dish set
/// on a public table is Hospitality). Append only.
/// </summary>
public enum CulturalOccurrenceKind
{
    /// <summary>Something was made: a kitchen or cellar batch (ingredients are production inputs, not meals).</summary>
    Production,
    /// <summary>Ordinary eating: the table's everyday habit (no participants are known).</summary>
    Consumption,
    /// <summary>A rite or gathering held.</summary>
    Gathering,
    /// <summary>A festival completed in a settlement by a cultural party.</summary>
    Festival,
    Hospitality,
    Luxury,
    Teaching,
    Performance,
    Memorial,
    /// <summary>A holiday or other observance kept on its day.</summary>
    Observance,
    /// <summary>A place was raised where practices can be held (a landmark).</summary>
    Venue,
    /// <summary>The people recognised something as theirs (a national food embraced, a tradition recognised).</summary>
    Recognition,
    /// <summary>People admitted to a settlement (origin only when known).</summary>
    Admission,
    /// <summary>Two communities met (a completed cultural visit).</summary>
    Contact,
}

/// <summary>What <see cref="CulturalOccurrence.quantity"/> counts. Append only.</summary>
public enum CultureQuantityUnit
{
    None,
    Occasions,
    Batches,
    Portions,
    IngredientAmount,
    FoodValue,
    Participants,
}

/// <summary>
/// One committed action the culture observed (never a preview): its dedupe key, purpose, when, what was practised,
/// where and by whom when known, and how much in explicit units. Unknown values stay unknown: settlement -1 and an
/// empty actor list mean "not recorded", never "nobody".
/// </summary>
[Serializable]
public class CulturalOccurrence
{
    /// <summary>Stable and unique per committed action: a second report with the same key is ignored.</summary>
    public string key;
    public CulturalOccurrenceKind kind;
    public CultureStamp stamp;
    /// <summary>What was practised: the rite, the recipe, the holiday, the landmark, the food.</summary>
    public CultureEntityRef subject = new CultureEntityRef();
    /// <summary>What committed it (the kitchen, a command, a world task), when useful.</summary>
    public CultureEntityRef source = new CultureEntityRef();
    public CultureEntityRef recipe = new CultureEntityRef();
    public CultureEntityRef evidence = new CultureEntityRef();
    /// <summary>The settlement it happened in, or -1 when unknown.</summary>
    public int settlement = -1;
    /// <summary>Legends taking part, when known (empty: not recorded).</summary>
    public List<string> actors = new List<string>();
    public float quantity;
    public CultureQuantityUnit unit;
    /// <summary>A short account of the cause for the player ("A festival in Ashford, led by Vaelia").</summary>
    public string text;

    public bool SettlementKnown => settlement >= 0;

    public CulturalOccurrence Copy() => new CulturalOccurrence
    {
        key = key, kind = kind, stamp = stamp?.Copy(), subject = subject?.Copy() ?? new CultureEntityRef(), source = source?.Copy() ?? new CultureEntityRef(),
        recipe = recipe?.Copy() ?? new CultureEntityRef(), evidence = evidence?.Copy() ?? new CultureEntityRef(), settlement = settlement,
        actors = actors != null ? new List<string>(actors) : new List<string>(), quantity = quantity, unit = unit, text = text,
    };
}

/// <summary>What a culture command did: whether it succeeded and why not, what it actually paid, and the occurrences it committed.</summary>
public class CultureCommandResult
{
    public bool succeeded;
    public string reason;
    public List<ResourceAmount> paid = new List<ResourceAmount>();
    public List<string> occurrences = new List<string>();

    public static CultureCommandResult Fail(string reason) => new CultureCommandResult { succeeded = false, reason = reason };

    public static CultureCommandResult Ok(string reason = null) => new CultureCommandResult { succeeded = true, reason = reason };

    public string PaidText => paid.Count == 0 ? "nothing" : string.Join(", ", paid.Where(p => p != null).Select(p => $"{p.amount:0.#} {p.resource}"));
}

/// <summary>
/// How far authored culture content rests on the vault (CULTURE_REDESIGN.md, "Canon boundaries"): every tradition,
/// local practice, memorial or observance carries one of these three labels. Shared by all culture features.
/// </summary>
public enum CanonStatus
{
    /// <summary>The vault says so.</summary>
    ExplicitCanon,
    /// <summary>The vault describes it; the game's form of it is an adaptation.</summary>
    CanonSupported,
    /// <summary>The game's own rule: a proposal recorded in Canon Gaps.md until the vault settles it.</summary>
    NewGameRule,
}
