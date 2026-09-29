using System;
using System.Collections.Generic;
using System.Linq;

// Observances (T04, Docs/Planning/CultureRedesign/T04.md): the culture's calendar. A holiday becomes one kind of
// observance among several: each has a cause, a venue, a repertoire, a food or tea, a recurrence on the game's own
// clock (Cycle / Echo / Phase / Seventh, never the wall clock) and a scale. Saved types hold fields only; enums are saved
// by name, so they are append-only.

/// <summary>How an observance comes round. Append only.</summary>
public enum ObservanceRecurrence
{
    /// <summary>On one Seventh of one Phase of every Echo (an anniversary; every holiday before observances).</summary>
    EveryEcho,
    /// <summary>On one Seventh of one Phase of one Echo, once every Cycle (a harvest offering).</summary>
    OncePerCycle,
    /// <summary>On the Ritual Seventh (the 21st, under the full Moon) of every Phase, or of one Phase of the Echo.</summary>
    RitualSeventh,
}

/// <summary>What the people gather for: it decides what the day gives and what it asks. Append only.</summary>
public enum ObservanceObjective
{
    /// <summary>A holiday's joy: a day set apart to celebrate (every holiday before observances).</summary>
    Celebration,
    /// <summary>To remember a loss or a cause: quiet, never joyous; it keeps the memory (T03).</summary>
    Remembrance,
    /// <summary>To set a table and share it: a named dish, tea or drink served.</summary>
    Hospitality,
    /// <summary>To sing, tell or dance a repertoire together.</summary>
    Performance,
    /// <summary>To meet: nothing served, nothing performed (a vigil).</summary>
    Gathering,
}

/// <summary>How much the day asks of the stores. Append only.</summary>
public enum ObservanceScale
{
    /// <summary>Nothing served: always possible, even in hardship.</summary>
    Quiet,
    /// <summary>A smaller table.</summary>
    Modest,
    /// <summary>The full table.</summary>
    Full,
}

/// <summary>What became of one occasion of an observance. Append only.</summary>
public enum ObservanceOutcome
{
    Kept,
    /// <summary>Kept, but smaller than planned (the stores could not set the planned table and nothing was prepared).</summary>
    KeptSmaller,
    /// <summary>Its day passed unseen (time moved on without the culture looking): nothing spent, nothing given.</summary>
    Missed,
    /// <summary>Moved to a later Seventh by the player.</summary>
    Postponed,
    /// <summary>Its preparation was cancelled and the day passed without it.</summary>
    Cancelled,
}

/// <summary>
/// An observance the game knows how to hold (authored in <see cref="ObservanceTuning.definitions"/>): its canon label
/// and vault note, the Ages and technology it needs, what it is for, the recurrences, days and grounds it allows, and
/// what it may be given (a cause, a repertoire, a food).
/// </summary>
[Serializable]
public class ObservanceDefinition
{
    public string id;
    public string name;
    public string description;
    /// <summary>The way of living keeping it leans the culture toward (an EnclaveFamily name).</summary>
    public string family;
    public CanonStatus canon = CanonStatus.NewGameRule;
    /// <summary>The vault note it rests on (Worldbuilding/...).</summary>
    public string canonSource;
    /// <summary>What the vault says and what the game adds, for the player and review.</summary>
    public string canonNote;
    public int minAge;
    /// <summary>The last Age it can be set apart in (-1: every Age after minAge). One already kept stays on the calendar.</summary>
    public int maxAge = -1;
    public string technology;
    public ObservanceObjective objective;
    /// <summary>The recurrences it may take, the first being the one offered first.</summary>
    public List<ObservanceRecurrence> recurrences = new List<ObservanceRecurrence>();
    /// <summary>Words the venue settlement's ground must hold (any of them; empty: anywhere).</summary>
    public List<string> grounds = new List<string>();
    public string groundText;
    /// <summary>It needs a cause to remember (a loss the culture remembers, T03).</summary>
    public bool needsCause;
    /// <summary>Rites it may be kept with (activity ids; empty: no repertoire).</summary>
    public List<string> repertoire = new List<string>();
    /// <summary>It may set a table with a chosen dish, tea or drink.</summary>
    public bool food;
    /// <summary>Foods offered first (resources or recipe ids), e.g. the peaches of the first golden fruit.</summary>
    public List<string> suggestedFoods = new List<string>();
    /// <summary>The scales it may be kept at (empty: every scale).</summary>
    public List<ObservanceScale> scales = new List<ObservanceScale>();
    /// <summary>Paid once, when it is set apart (Unity...).</summary>
    public List<ResourceAmount> establishCost = new List<ResourceAmount>();
    /// <summary>Paid each time it is kept above Quiet (Faith for the peaks...); unpaid, the day is kept quietly.</summary>
    public List<ResourceAmount> keepCost = new List<ResourceAmount>();
    /// <summary>Only one of it on the calendar at a time.</summary>
    public bool unique;

    public bool Allows(ObservanceScale scale) => scales == null || scales.Count == 0 || scales.Contains(scale);

    public bool FitsGround(string ground) =>
        grounds == null || grounds.Count == 0 || (!string.IsNullOrEmpty(ground) && grounds.Any(g => !string.IsNullOrEmpty(g) && ground.Contains(g.ToLowerInvariant())));

    public bool OpenIn(int age) => age >= minAge && (maxAge < 0 || age <= maxAge);
}

/// <summary>
/// One observance on the calendar (the saved schedule). Its day: <see cref="seventh"/> (1-21) of <see cref="phase"/>
/// (1-3; 0 for a Ritual Seventh of every Phase) of <see cref="echo"/> (1-4, once-per-Cycle only). Changing any choice
/// raises <see cref="revision"/>. <see cref="lastResolved"/> is the last date (<see cref="CultureStamp.Index"/>) whose
/// occasion was decided (kept, missed, postponed): an occasion is never decided twice.
/// </summary>
[Serializable]
public class Observance
{
    /// <summary>"obs-1", "obs-2"... never reused.</summary>
    public string id;
    public string definition;
    public string name;
    public ObservanceObjective objective;
    public ObservanceRecurrence recurrence;
    public int seventh = 1, phase = 1, echo = 1;
    /// <summary>The settlement it is kept in (-1: the nation's, no one place).</summary>
    public int settlement = -1;
    public CultureEntityRef venue = new CultureEntityRef();
    /// <summary>What it remembers: a remembered loss (Evidence), a heritage moment, or nothing.</summary>
    public CultureEntityRef cause = new CultureEntityRef();
    /// <summary>The cause in words (a holiday's occasion, a loss's title) as it was when chosen.</summary>
    public string causeText;
    /// <summary>The rite performed (an activity id), or empty.</summary>
    public string repertoire;
    /// <summary>The dish, tea or drink served (a Recipe or a Resource), or none.</summary>
    public CultureEntityRef food = new CultureEntityRef();
    public ObservanceScale scale;
    /// <summary>A holiday from before observances (its name is <see cref="holiday"/>): kept exactly as a holiday was until its plan is changed.</summary>
    public bool legacy;
    public string holiday;
    /// <summary>The player changed its plan (a legacy holiday is then kept by the observance rules too).</summary>
    public bool customized;
    public CultureStamp established;
    public int revision;
    public long lastResolved = -1;
    /// <summary>An occasion moved by the player: its own date and the date it is kept on instead (-1: none).</summary>
    public long postponedFrom = -1, postponedTo = -1;
    public int kept, keptSmaller, missed;
    /// <summary>Taken off the calendar (its history stays).</summary>
    public bool ended;
}

/// <summary>A table prepared ahead for one occasion: the food already taken from the stores, returned whole if cancelled.</summary>
[Serializable]
public class ObservancePreparation
{
    public string observance;
    /// <summary>The date it is for (after a postponement, the date it is kept on).</summary>
    public long date;
    public ObservanceScale scale;
    public CultureEntityRef food = new CultureEntityRef();
    public List<ResourceAmount> reserved = new List<ResourceAmount>();
    public int preparedSeventh;
    /// <summary>The observance's plan revision it was prepared for.</summary>
    public int revision;
}

/// <summary>One occasion decided: which, when, what became of it, what it spent and what was served.</summary>
[Serializable]
public class ObservanceOccasion
{
    /// <summary>"obs:obs-3:1234": unique per observance and date, the occasion's dedupe key.</summary>
    public string key;
    public string observance;
    public long date;
    public int cultureSeventh;
    public ObservanceOutcome outcome;
    public ObservanceScale scale;
    public List<ResourceAmount> paid = new List<ResourceAmount>();
    public string text;
}

/// <summary>The observances' saved state, in <see cref="CultureExtensionState.observances"/>.</summary>
[Serializable]
public class ObservanceState
{
    public const int CurrentVersion = 1;
    public int version;
    public int nextId = 1;
    public List<Observance> observances = new List<Observance>();
    public List<ObservancePreparation> preparations = new List<ObservancePreparation>();
    /// <summary>The latest occasions decided (bounded; tallies on each observance keep the rest).</summary>
    public List<ObservanceOccasion> history = new List<ObservanceOccasion>();

    public Observance Get(string id) => string.IsNullOrEmpty(id) ? null : observances.FirstOrDefault(o => o != null && o.id == id);

    public ObservancePreparation PreparationOf(string id) => preparations.FirstOrDefault(p => p != null && p.observance == id);

    public IEnumerable<Observance> Active => observances.Where(o => o != null && !o.ended);
}

public partial class CultureExtensionState
{
    [SaveOptionalField] public ObservanceState observances = new ObservanceState();
}

/// <summary>What establishing an observance chose (the player's plan): day, venue, cause, repertoire, food and scale.</summary>
public class ObservancePlan
{
    public string definition;
    public ObservanceRecurrence recurrence;
    public int seventh = 1, phase = 1, echo = 1;
    public int settlement = -1;
    public CultureEntityRef venue = new CultureEntityRef();
    public CultureEntityRef cause = new CultureEntityRef();
    public string causeText;
    public string repertoire;
    public CultureEntityRef food = new CultureEntityRef();
    public ObservanceScale scale;
}

/// <summary>An observance as a snapshot for views and other features.</summary>
public sealed class ObservanceView
{
    public CultureEntityRef foodRef, causeRef, venueRef;
    public string id, definition, name, description, family, canonSource, canonNote;
    public CanonStatus canon;
    public ObservanceObjective objective;
    public ObservanceRecurrence recurrence;
    public string day, next, place, cause, repertoire, food;
    public int settlement;
    public ObservanceScale scale;
    public bool legacy, customized, ended, prepared, postponed;
    public int kept, keptSmaller, missed;
    /// <summary>Sevenths until its next occasion (0: today), or -1.</summary>
    public int seventhsUntil;
    public long nextDate;
    /// <summary>What the next occasion will ask and give at its scale, and what happens if the stores cannot.</summary>
    public string plan;
    public List<ResourceAmount> reserved = new List<ResourceAmount>();
    public ObservanceOccasion[] history;
}
