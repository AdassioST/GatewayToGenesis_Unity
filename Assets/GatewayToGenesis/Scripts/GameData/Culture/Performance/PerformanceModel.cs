using System;
using System.Collections.Generic;
using System.Linq;

// Ensembles whose relationships affect their performance (T08): a cultural party performs a repertoire for an intent at
// its festival; who performs, their readiness and the history between them decide how it lands. Saved inside
// CultureExtensionState (fields only; enums appended, never reordered).

public partial class CultureExtensionState
{
    /// <summary>Performances: those planned, the echoes still heard, and the history (T08).</summary>
    [SaveOptionalField] public PerformanceState performance = new PerformanceState();
}

/// <summary>What a performance is for. Append only: saves store the name.</summary>
public enum PerformanceIntent
{
    /// <summary>To steady a troubled town: it eases its Composure most.</summary>
    Reassurance,
    /// <summary>To keep a loss the people remember: the names sung, a little heart for everyone.</summary>
    Remembrance,
    /// <summary>For joy: a little Composure eased, a little heart.</summary>
    Celebration,
    /// <summary>To lend a region Coherence (the Polyphonic Choral Singers of the later Ages only).</summary>
    Restoration,
}

/// <summary>How a performance landed. Append only.</summary>
public enum PerformanceBand { Faltering, Steady, Moving, Resonant }

/// <summary>Where a planned performance stands. Append only.</summary>
public enum PerformanceStatus
{
    /// <summary>Chosen for a party standing in a settlement: it waits for the festival.</summary>
    Planned,
    /// <summary>The party's festival is under way.</summary>
    Performing,
    Completed,
    /// <summary>Called off, or its festival never happened or stopped (nothing given).</summary>
    Cancelled,
    /// <summary>The festival ended but too few voices were left to perform it (nothing given).</summary>
    Failed,
}

/// <summary>
/// A piece a cultural party can perform (authored; <see cref="PerformanceTuning.repertoires"/>): what it is for, what
/// it asks of the performers, the Ages, technology and civic it needs, and what it rests on in the vault.
/// </summary>
[Serializable]
public class RepertoireSpec
{
    public string id;
    public string name;
    public string description;
    public CanonStatus canon;
    /// <summary>The vault note it rests on.</summary>
    public string canonSource;
    public string family;
    /// <summary>What it can be performed for.</summary>
    public List<PerformanceIntent> intents = new List<PerformanceIntent>();
    /// <summary>The Legends' binding that carries it (a Resonance song, a Strand of memory).</summary>
    public string binding;
    /// <summary>Voices it needs at least.</summary>
    public int minCast = 1;
    /// <summary>Ages it is known in (by number; -1: no end).</summary>
    public int minAge, maxAge = -1;
    public string technology;
    /// <summary>A civic that must be in force (the institution that performs it).</summary>
    public string civic;
    /// <summary>Needs a loss the people remember (T03).</summary>
    public bool needsLoss;
    /// <summary>A settlement custom (T02) that knows it: a town keeping it hears it best.</summary>
    public string localCustom;
}

/// <summary>What each intent does, before the band's share (see <see cref="PerformanceTuning.intentEffects"/>).</summary>
[Serializable]
public class IntentEffect
{
    public PerformanceIntent intent;
    /// <summary>Composure strain eased in the settlement each Seventh the echo lasts.</summary>
    public float reliefPerSeventh;
    /// <summary>Morale while the echo lasts (named source), at a Moving band; more when Resonant.</summary>
    public float morale;
    /// <summary>Coherence lent to the cells around the settlement while the echo lasts (Restoration only).</summary>
    public float coherence;
    public int coherenceRadius;
    public int echoSevenths = 7;
}

/// <summary>A performance planned or under way: the party, the place, the piece and its purpose, and who sings.</summary>
[Serializable]
public class PerformanceBooking
{
    /// <summary>"perf-3": unique (its occurrence, relationship moment and echo use it).</summary>
    public string id;
    public int unit = -1;
    public string unitName;
    public int settlement = -1;
    public string place;
    public string repertoire;
    public PerformanceIntent intent;
    /// <summary>The voices when it was planned (the festival's end reads who is still there).</summary>
    public List<string> cast = new List<string>();
    /// <summary>The loss it remembers (T03 evidence id), for a remembrance.</summary>
    public string cause;
    public PerformanceStatus status;
    public int planned = -1, started = -1;
    /// <summary>Optional calendar host. Empty retains legacy festival completion ownership.</summary>
    [SaveOptionalField] public string observance;
    [SaveOptionalField] public long occasion = -1;
}

/// <summary>What a completed performance leaves behind for a while: an echo in its settlement (a named source).</summary>
[Serializable]
public class PerformanceEcho
{
    /// <summary>The performance's id.</summary>
    public string id;
    public int settlement = -1;
    public string place;
    public string repertoire;
    public string name;
    public PerformanceIntent intent;
    public PerformanceBand band;
    public float relief, morale, coherence;
    public int radius;
    /// <summary>The settlement's cell (for Coherence around it).</summary>
    public int cell = -1;
    public int started, until;
}

/// <summary>A performance as history: what, where, who, how it landed and why.</summary>
[Serializable]
public class PerformanceRecord
{
    public string id;
    public int settlement = -1;
    public string place;
    public string repertoire;
    public string name;
    public PerformanceIntent intent;
    public PerformanceStatus status;
    public PerformanceBand band;
    public float score;
    public List<string> cast = new List<string>();
    /// <summary>The explanation, one line per factor ("Vaelia is Clouded: 0.85").</summary>
    public List<string> factors = new List<string>();
    public int seventh;
    public string ageId;
    public string text;
}

/// <summary>Everything performance remembers.</summary>
[Serializable]
public class PerformanceState
{
    public List<PerformanceBooking> bookings = new List<PerformanceBooking>();
    public List<PerformanceEcho> echoes = new List<PerformanceEcho>();
    /// <summary>The latest performances, oldest first (bounded).</summary>
    public List<PerformanceRecord> history = new List<PerformanceRecord>();
    /// <summary>Performances completed all told.</summary>
    public int completed;
    public int serial;
}

/// <summary>
/// The numbers and repertoire of performance. Every number is a proposal (Canon Gaps.md, "Ensembles and performance").
/// </summary>
[Serializable]
public class PerformanceTuning
{
    public List<RepertoireSpec> repertoires = new List<RepertoireSpec>
    {
        new RepertoireSpec
        {
            id = "evening-song", name = "The Evening Song", canon = CanonStatus.CanonSupported, family = "Weaver",
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Waltz Pillar.md",
            description = "Many voices after the day's work: a resonant orchestra rather than a soloist who can miss a note (the Waltz Pillar).",
            intents = { PerformanceIntent.Reassurance, PerformanceIntent.Celebration }, binding = "Resonance", minCast = 1, localCustom = "evening-song",
        },
        new RepertoireSpec
        {
            id = "song-of-the-named", name = "Song of the Named", canon = CanonStatus.CanonSupported, family = "Esoteric",
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            description = "The names of the lost, sung one by one so no one is ground into the dust unremembered. It needs a loss the people remember.",
            intents = { PerformanceIntent.Remembrance }, binding = "Strand", minCast = 1, needsLoss = true,
        },
        new RepertoireSpec
        {
            id = "polyphonic-chorus", name = "Polyphonic Chorus", canon = CanonStatus.ExplicitCanon, family = "Weaver",
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Civic.md",
            description = "State choruses of overlaid voices, bells and Magical Relics that enhance the Coherence of regions, for recovery and rebuilding (Polyphonic Choral Singers, Ages IV-VI).",
            intents = { PerformanceIntent.Restoration, PerformanceIntent.Reassurance }, binding = "Resonance", minCast = 3,
            minAge = 4, maxAge = 6, civic = "Polyphonic Choral Singers",
        },
    };

    public List<IntentEffect> intentEffects = new List<IntentEffect>
    {
        new IntentEffect { intent = PerformanceIntent.Reassurance, reliefPerSeventh = 3f, echoSevenths = 7 },
        new IntentEffect { intent = PerformanceIntent.Remembrance, reliefPerSeventh = 1.5f, morale = 1f, echoSevenths = 7 },
        new IntentEffect { intent = PerformanceIntent.Celebration, reliefPerSeventh = 1f, morale = 1f, echoSevenths = 7 },
        new IntentEffect { intent = PerformanceIntent.Restoration, reliefPerSeventh = 2f, coherence = 0.1f, coherenceRadius = 1, echoSevenths = 21 },
    };

    // ----- Readiness -----
    /// <summary>What each Composure state lends a voice (Pristine, Clouded, Fractured, Spiraling, Surrender).</summary>
    public float[] composureReadiness = { 1f, 0.85f, 0.5f, 0.2f, 0f };
    /// <summary>A voice's readiness per binding point above <see cref="bindingBase"/>, within the bounds below.</summary>
    public float perBindingPoint = 0.01f, bindingMin = -0.04f, bindingMax = 0.1f;
    public int bindingBase = 5;
    /// <summary>The ensemble's readiness counts this much toward the score.</summary>
    public float readinessWeight = 0.6f;
    /// <summary>Each voice past the first (Waltz: an orchestra over a soloist), at most <see cref="ensembleMax"/>.</summary>
    public float perExtraVoice = 0.05f, ensembleMax = 0.15f;

    // ----- Ties (per direction: each Legend's own reading) -----
    public float perConsonantStage = 0.02f, perDissonantStage = 0.03f, sharedWoundRemembrance = 0.02f, familiar = 0.005f;
    public int familiarEncounters = 3;
    public float pairMax = 0.15f, tiesMin = -0.25f, tiesMax = 0.2f;

    // ----- The place -----
    public float keepsCustom = 0.08f, rootedness = 0.05f, lossHere = 0.08f, fracturedTown = -0.06f;
    /// <summary>Strain from which a town is too troubled to hear easily (Composure: Fractured).</summary>
    public float fracturedStrain = 40f;
    /// <summary>A loss happened within this many cells of the settlement counts as remembered here.</summary>
    public int lossRadius = 2;

    // ----- Bands -----
    public float steadyFrom = 0.3f, movingFrom = 0.55f, resonantFrom = 0.8f;
    /// <summary>Share of the intent's effects by band (Faltering, Steady, Moving, Resonant).</summary>
    public float[] bandShare = { 0f, 0.5f, 1f, 1.5f };
    /// <summary>Affection a performance adds to an existing significant bond, by band (never a new tie or stage).</summary>
    public int[] bandAffection = { 0, 1, 2, 3 };

    // ----- Bounds -----
    /// <summary>Echoes whose morale applies at once (the strongest).</summary>
    public int maxMoraleEchoes = 2;
    /// <summary>Most Coherence all echoes together lend a cell.</summary>
    public float coherenceCap = 0.2f;
    /// <summary>A planned performance with no festival begun lapses after this many Sevenths.</summary>
    public int planSevenths = 7;
    public int historyKept = 30;

    public RepertoireSpec Repertoire(string id) =>
        string.IsNullOrEmpty(id) ? null : repertoires?.FirstOrDefault(r => r != null && string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase));

    public IntentEffect Effect(PerformanceIntent intent) => intentEffects?.FirstOrDefault(e => e != null && e.intent == intent) ?? new IntentEffect { intent = intent };
}
