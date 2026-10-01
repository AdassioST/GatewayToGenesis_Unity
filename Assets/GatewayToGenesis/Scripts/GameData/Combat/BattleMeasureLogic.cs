using System;
using System.Collections.Generic;
using System.Linq;

// The Temporal Economy (vault: Combat System.md, "The Four-Beat Measure"). Every round of manual combat is one Measure
// of four simultaneous Beats: Visualization, Composition and Commitment happen in held time, then both armies perform
// Beats 1-4 together, the Cadence releases, the Abjuration Window answers, Toll and Assessment close it. Time is the
// limiting resource: every Track owns the same four Beats, one Major Note, one Note per Beat.

public enum BattlePhase { Visualization, Composition, Commitment, Execution, Reaction, Abjuration, Toll, Assessment }
public enum BattleActionKind { Card, Movement, DrilledAction, Conductor, Wait, Chord }
public enum BattleCommandKind { Card, Movement, Redraw, Wait, Chord, Order, Retract, Path, Stabilize, Ground, Seer, OverbeatCard, OverbeatStep, ClimaxMajor, GrandResolution }

/// <summary>How a performer may move while its Chord sounds. A Chord's Footing is the strictest of its Notes. Saved by index: append only.</summary>
public enum BattleFooting { Free, Braced, Anchored }
/// <summary>Steps per Measure: Heavy 1, Standard 2, Swift 3.</summary>
public enum BattlePace { Heavy = 1, Standard = 2, Swift = 3 }
/// <summary>The doctrine a Track performs when it is not composed this Measure (exactly as Auto-Resolve performs it). Append only.</summary>
public enum BattleStandingOrder { Advance, Hold, Guard, Screen, Pursue, Volley }
public enum BattleExecution { Missed, Clean, Perfect }
/// <summary>The tuning needle, lowest first. Sympathetic exists only for synchronized Spotlit Tracks in Resolution Climax.</summary>
public enum BattleTuningState { OutOfTune, Detuned, InTune, Resonant, Sympathetic }
/// <summary>A card's rhythm; Inherit reads the card (Legato when channeling, the formation's tempo for spells, Staccato for steel). Append only.</summary>
public enum CardTempo { Inherit, Staccato, Legato, Accelerando, Ritardando, Polyrhythm }
/// <summary>SupportingMajor: a Major fused into a Climax finale or a Grand Resolution, structurally subordinate to its Core.</summary>
public enum BattleNoteKind { Major, Minor, Ward, Contribution, Sustain, Ground, SupportingMajor }
public enum BattleChordState { Composed, Sounding, Suspended, Resolved, Collapsed, Grounded, Cancelled }

/// <summary>
/// The Measure's numbers. The first block is the document's Balance Reference (canon: keep unless the vault changes);
/// the second is where the document names a rule but no magnitude (proposals, tune freely).
/// </summary>
[Serializable]
public sealed class BattleMeasureTuning
{
    // Canon (Combat System.md, "Balance Reference — Baseline Values").
    public int baseHold = 4, legatoAnchor = 3, maxRings = 2, reactionsPerTrack = 1, cascadeLinks = 4, fluxCascadeLinks = 1;
    public int bandwidthBase = 2, spotlit = 3, hyperStabilization = 4;
    public float holdDrain = 5f, perfectPower = .25f, wardGrowth = .25f, wardAsMinor = .5f;
    public float cleanAbjuration = .2f, perfectAbjuration = .45f, wardCounters = 1.5f, wardCountered = .5f;
    public int channelHold = 1, fermataCap = 2, sustainHold = 1, bondedEnsembleHold = 1, crystalMinorHold = 1, crystalExpertHold = 1, crystalMasterHold = 2;
    public int resonantHold = 1, detunedHold = -1, outOfTuneHold = -2;
    public int riverSteps = 2, swiftSnowPenalty = 1, spearheadNeutralStep = 1, chargeStep = 1, withdrawHexes = 2;

    // Proposals: the document fixes the rule, not these magnitudes.
    [UnityEngine.Tooltip("Share of a drilled strike exchanged as Skirmish Attrition on each Beat after contact.")]
    public float attritionShare = .25f;
    [UnityEngine.Tooltip("Skirmish Pressure difference below which the ground holds and the Skirmish continues.")]
    public float pressureMargin = .05f;
    [UnityEngine.Tooltip("Integrity lost in one impact, as a share of maximum, that interrupts a Braced or Anchored performer; Cindergale mastery raises it per tier.")]
    public float interruptShare = .1f, cindergaleInterrupt = .05f;
    [UnityEngine.Tooltip("Battle Composure shares read against the five states for Tuning (Mind Break itself belongs to SOW-07).")]
    public float pristineAbove = .8f, fracturedBelow = .5f, spiralingBelow = .25f;
    public float dissonantGround = .5f;
    [UnityEngine.Tooltip("Collapse: share taken by the performer and by the contributing Tracks; the rest spills on the nearest allies.")]
    public float collapsePerformer = .5f, collapseContributors = .3f, collapseFloor = 8f, collapseIntegrity = 1f, collapseComposure = .5f, collapseWitness = 4f;
    [UnityEngine.Tooltip("Ward Strength per point of a Ward card's legacy spell share.")]
    public float wardPerShare = 100f;
    [UnityEngine.Tooltip("Impetus on the Beat a Charge collides (multiplies the section's own charge).")]
    public float impetus = 1.2f;
    [UnityEngine.Tooltip("Ranged and channeling units caught in melee strike at this share of their Attrition.")]
    public float caughtInMelee = .5f;
    [UnityEngine.Tooltip("Default Beat a composed Ward Major is raised on when none is chosen.")]
    public int wardRaise = 3;
}

/// <summary>Pure Measure arithmetic shared by the resolver, the views and the tests.</summary>
public static class BattleMeasureMath
{
    public const int Beats = 4;
    public static int Rel(int absoluteBeat) => absoluteBeat <= 0 ? 0 : (absoluteBeat - 1) % Beats + 1;
    public static int MeasureOf(int absoluteBeat) => absoluteBeat <= 0 ? 0 : (absoluteBeat - 1) / Beats + 1;
    public static int Absolute(int measure, int beat) => (measure - 1) * Beats + beat;
    public static int Cadence(int measure) => measure * Beats;
    /// <summary>Beats remaining until <paramref name="due"/>: 4 at Visualization is this Measure's Cadence.</summary>
    public static int Countdown(int due, int beat) => Math.Max(0, due - beat);

    /// <summary>A rendition (1 Clean, 1.25 a full Perfect Echo, below 1 Missed) as an Execution State.</summary>
    public static BattleExecution Grade(float rendition) =>
        rendition < .999f ? BattleExecution.Missed : rendition > 1.001f ? BattleExecution.Perfect : BattleExecution.Clean;

    public static int HoldOf(BattleTuningState state, BattleMeasureTuning t)
    {
        switch (state)
        {
            case BattleTuningState.Sympathetic:
            case BattleTuningState.Resonant: return t.resonantHold;
            case BattleTuningState.Detuned: return t.detunedHold;
            case BattleTuningState.OutOfTune: return t.outOfTuneHold;
            default: return 0;
        }
    }

    /// <summary>Binding mastery on the chord ladder: 0 none, 1 Basic (Skilled 21+), 2 Advanced (28+), 3 Expert (33+), 4 Master (42+).</summary>
    public static int Mastery(int score) => score >= 42 ? 4 : score >= 33 ? 3 : score >= 28 ? 2 : score >= 21 ? 1 : 0;

    /// <summary>Steps a section takes in one Measure from its speed: 2 or less Heavy, up to 4 Standard, faster Swift.</summary>
    public static BattlePace Pace(float speed) => speed <= 2f ? BattlePace.Heavy : speed <= 4f ? BattlePace.Standard : BattlePace.Swift;
}

/// <summary>One committed Note or Step as Visualization shows it: Beat, Countdown, target rule and expected output.</summary>
public sealed class BattleActionIntent
{
    public long id;
    public int committedMeasure, committedBeat, dueBeat, actionBeats, anchorBeat, voice, target = -1, from = -1, to = -1;
    public bool attacker, channeling, targetAttacker;
    public BattleActionKind kind;
    public BattleProximity targeting;
    public CardAim aim;
    public string card, name;
    public float expectedIntegrity, expectedComposure, expectedGuard, expectedWard;
    public SpellBinding binding;
    public bool damageCertain;
    public bool phantom, unverified;
    /// <summary>A Minor Note (Clarity 1 shows these), a syncopated Major (Beats 1-3), a Ward Major, a Hyper Chord.</summary>
    public bool minor, syncopated, ward, hyper;
    /// <summary>Committed into the current Measure: truthful and unchangeable (framed in gold).</summary>
    public bool confirmed;
    public BattleFooting footing;
    /// <summary>The working's Hold Limit and the Beats it will have sounded at release (margin = limit - sounding).</summary>
    public int holdLimit, sounding;
    public int minimumWardBeats;
    public List<string> abjuredOnlyBy = new List<string>();
    public int Remaining(int beat) => Math.Max(0, dueBeat - beat);
    public BattleActionIntent Clone() { var copy = (BattleActionIntent)MemberwiseClone(); copy.abjuredOnlyBy = new List<string>(abjuredOnlyBy); return copy; }
}

public sealed class BattlePhaseEvent
{
    public int measure, beat;
    public BattlePhase phase;
    public long action;
}

public sealed class BattleCommand
{
    public BattleCommandKind kind;
    public bool attacker = true;
    public int handIndex, section, target = -1, hex, beats = 1, beat;
    public long chord;
    /// <summary>ClimaxMajor: fuse the Climax Major with its Track's own Chord (Fused Finale, 80%+).</summary>
    public bool fuse;
    public float rendition = 1f;
    public BattleStandingOrder order;
    public List<int> minors = new List<int>(), path = new List<int>(), stepBeats = new List<int>(), minorBeats = new List<int>();
}

public sealed class BattleUnitView
{
    public int section, hex, countdown, pace;
    public bool attacker, standing, mindBroken, stacked, engaged, pinned;
    public float integrity, composure, guard, ward;
    public float burn, blind, exposed;
    public BattleTuningState tuning;
}

/// <summary>One row of the sequencer: a Track's four Beats, its Steps and its standing doctrine.</summary>
public sealed class BattleTrackView
{
    public bool attacker, composed, spotlit, tempoFever;
    public int voice;
    public string name;
    public BattleStandingOrder order;
    /// <summary>Index 1-4: what sounds on each Beat of the open Measure ("Rest", "Minor: Crystal Aegis", "Major: Shield Bash", "Sustained").</summary>
    public string[] beats = new string[BattleMeasureMath.Beats + 1];
    /// <summary>Index 1-4: the hex the Track steps into on that Beat, or -1.</summary>
    public int[] steps = Enumerable.Repeat(-1, BattleMeasureMath.Beats + 1).ToArray();
    public int primed, pace;
    public BattleTuningState tuning;
}

public sealed class BattleVisualization
{
    public int measure, beat;
    public BattlePhase phase;
    public SideBars attacker, defender;
    public List<BattleUnitView> units;
    public List<BattleHexTerrain> terrain;
    public List<BattleActionIntent> intents;
    public List<BattleWeaveView> weaves;
    public List<BattleReactionView> reactions;
    public List<BattleTrackView> tracks;
}

public sealed class BattleSequencePreview
{
    public BattleVisualization before, after;
    public List<CardPlay> plays;
    public List<BattleSpatialEvent> spatialEvents;
    public Dictionary<string, int> harmonicInteractions;
    public List<string> failures;
    public List<BattleChordEvent> chordEvents;
    // A redraw's unknown replacement hand is deliberately absent from the preview.
}
