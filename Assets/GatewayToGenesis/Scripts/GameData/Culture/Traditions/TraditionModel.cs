using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Where a tradition is in its life. The record (its origin, history, bearers) is kept whatever the stage: a dormant
/// practice is remembered but not lived.
/// </summary>
public enum TraditionStage
{
    /// <summary>Done more than once, not yet a custom.</summary>
    Emerging,
    /// <summary>A custom the people keep (it gives its practice benefits).</summary>
    Practiced,
    /// <summary>Not practised for long: its benefits stop, its record stays.</summary>
    Dormant,
    /// <summary>Taken up again after lying dormant (lived, like a practised custom).</summary>
    Revived,
}

/// <summary>What the player decided about a practised tradition. Crossing a threshold never decides for them.</summary>
public enum TraditionRecognition
{
    /// <summary>Not yet a custom: nothing to decide.</summary>
    None,
    /// <summary>A custom now: the people wait to hear whether it is recognised.</summary>
    Offered,
    /// <summary>Left for later (offered again after a while).</summary>
    Deferred,
    /// <summary>Recognised by the nation (paid for when chosen; its recognised benefits apply while it is lived).</summary>
    Recognized,
    /// <summary>Kept as a local custom: it lapses more slowly, and the nation takes nothing from it.</summary>
    Preserved,
}

/// <summary>An occurrence that feeds a tradition: its kind, what was practised (any when empty) and how much it counts.</summary>
[Serializable]
public class TraditionTrigger
{
    public CulturalOccurrenceKind kind;
    [Tooltip("The kind of thing practised (None: any).")]
    public CultureEntityKind subjectKind;
    [Tooltip("Its id (empty: any of that kind).")]
    public string subjectId;
    [Tooltip("Credit one occurrence gives (per batch for Production).")]
    public float weight = 1f;

    public TraditionTrigger() { }
    public TraditionTrigger(CulturalOccurrenceKind kind, CultureEntityKind subjectKind, string subjectId, float weight = 1f)
    {
        this.kind = kind; this.subjectKind = subjectKind; this.subjectId = subjectId; this.weight = weight;
    }
}

/// <summary>A tradition's own benefit (a game effect with its target), applied under the tradition's source.</summary>
[Serializable]
public class TraditionEffect
{
    public GameEffectType type;
    public float value;
    public ModifierType modifierType;
    public string target;
    public ScopeType scope;

    public TraditionEffect() { }
    public TraditionEffect(GameEffectType type, float value, ModifierType modifierType, string target = null, ScopeType scope = ScopeType.Individual)
    {
        this.type = type; this.value = value; this.modifierType = modifierType; this.target = target; this.scope = scope;
    }

    public GameEffect ToEffect() => new GameEffect(type, value, modifierType, target, scope);
}

/// <summary>
/// A tradition the people can come to keep (authored; <see cref="TraditionTuning.definitions"/>): what feeds it, where
/// it lives (each settlement apart, or the nation), what it gives while it is lived and once recognised, and what it
/// rests on in the vault. Every number is a proposal.
/// </summary>
[Serializable]
public class TraditionDefinition
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("The way of living it belongs to (an EnclaveFamily name): lived, it leans the culture toward it.")]
    public string family;
    [Tooltip("A food practice (cooked, served or eaten) rather than a song, rite or gathering.")]
    public bool food;
    public CanonStatus canon = CanonStatus.NewGameRule;
    [Tooltip("The vault note it rests on (a path under Arcanoria/).")]
    public string canonSource;
    [Tooltip("The first Age (its number) it can arise in.")]
    public int minAge;
    [Tooltip("Researched before it can arise (empty: none).")]
    public string technology;
    [Tooltip("Each settlement keeps its own (an occurrence with no known place feeds the nation's).")]
    public bool local = true;
    [Tooltip("One tradition per thing practised (a national food's own table), rather than one for all.")]
    public bool perSubject;
    [Tooltip("Only made by a link (a national food embraced), never by occurrences alone.")]
    public bool linkedOnly;
    public List<TraditionTrigger> triggers = new List<TraditionTrigger>();
    [Tooltip("What it gives while it is lived anywhere (practised or revived).")]
    public List<TraditionEffect> practiced = new List<TraditionEffect>();
    [Tooltip("What it adds once the nation has recognised it (while it is lived).")]
    public List<TraditionEffect> recognized = new List<TraditionEffect>();
    [Tooltip("Unity the nation spends to recognise it (asked, never taken when the threshold is crossed).")]
    public float recognitionUnity = 30f;

    /// <summary>"Rites", "the table": what kind of practice it is, for the player.</summary>
    public string Kind => food ? "food practice" : "practice";
}

/// <summary>Where a tradition came from: the occurrence that first made it (or, for an older save, what was recorded).</summary>
[Serializable]
public class TraditionOrigin
{
    public string key;
    public CulturalOccurrenceKind kind;
    public CultureEntityRef subject = new CultureEntityRef();
    public int settlement = -1;
    public List<string> actors = new List<string>();
    public CultureStamp stamp = new CultureStamp();
    public string text;
    /// <summary>Recorded before the tradition ledger existed: its occasions and bearers were never kept.</summary>
    public bool legacy;
}

/// <summary>One occasion a tradition was practised (bounded: the oldest are summarised in the tallies).</summary>
[Serializable]
public class TraditionParticipation
{
    public string key;
    public CulturalOccurrenceKind kind;
    public CultureEntityRef subject = new CultureEntityRef();
    public int seventh;
    public int settlement = -1;
    public float credit;
    public List<string> actors = new List<string>();
    public string text;
}

/// <summary>How many occasions of one kind fed a tradition, all told (kept when the history is trimmed).</summary>
[Serializable]
public class TraditionTally
{
    public CulturalOccurrenceKind kind;
    public int count;
    public float credit;
}

/// <summary>
/// One living (or remembered) tradition: a definition kept in one place (a settlement, or the nation), with its origin,
/// the people who carried it when known, its bounded history, its stage and what the player decided about it.
/// </summary>
[Serializable]
public class TraditionInstance
{
    /// <summary>Stable ("trad-3"); never reused.</summary>
    public string id;
    public string definition;
    /// <summary>The settlement it lives in, or -1 for the nation (or a place unknown).</summary>
    public int settlement = -1;
    /// <summary>What it is the tradition of, for a per-subject definition (a national food); else none.</summary>
    public CultureEntityRef subject = new CultureEntityRef();
    public TraditionOrigin origin = new TraditionOrigin();
    public TraditionStage stage;
    /// <summary>The Seventh (since the founding) it entered its stage.</summary>
    public int stageSince;
    public TraditionRecognition recognition;
    public int recognitionSeventh;
    /// <summary>A deferred decision is offered again from this Seventh.</summary>
    public int deferredUntil;
    /// <summary>How strongly it is being practised: credit that fades each Seventh.</summary>
    public float momentum;
    /// <summary>Occasions all told, and the distinct Sevenths they fell in.</summary>
    public int participations;
    public int distinctSevenths;
    public int lastPracticed;
    /// <summary>Distinct Sevenths it was practised since it lay dormant (it revives at the tuning's count).</summary>
    public int revivalProgress;
    public int revivals;
    public List<TraditionParticipation> history = new List<TraditionParticipation>();
    public List<TraditionTally> tallies = new List<TraditionTally>();
    /// <summary>Legends who took part, when known (bounded).</summary>
    public List<string> bearers = new List<string>();
    /// <summary>Places raised where it can be held (landmarks), and things it is bound to (a national food, a rite).</summary>
    public List<CultureEntityRef> venues = new List<CultureEntityRef>();
    public List<CultureEntityRef> links = new List<CultureEntityRef>();
    /// <summary>What has already been told and remembered once ("emerged", "practiced", "recognized", "revived"...): never twice.</summary>
    public List<string> milestones = new List<string>();
    /// <summary>
    /// Its settlement fell: it lives nowhere now (<see cref="settlement"/> is <see cref="TraditionRules.FallenPlace"/>, so a
    /// later settlement given the same id never inherits it); the place's name and the ruin it left are kept.
    /// </summary>
    [SaveOptionalField] public bool fallen;
    [SaveOptionalField] public string formerPlace;
    [SaveOptionalField] public int formerSettlement;
    [SaveOptionalField] public int ruin;
    /// <summary>
    /// Blended into a new form that took its place here (T07, a hybrid's Replace): it lives on in that tradition (its id)
    /// and takes no credit of its own here any more; its record stays.
    /// </summary>
    [SaveOptionalField] public string mergedInto;

    public bool Lived => stage == TraditionStage.Practiced || stage == TraditionStage.Revived;
    public bool Established => milestones.Contains(TraditionRules.PracticedMilestone);
    public bool HasMilestone(string m) => milestones.Contains(m);
}

/// <summary>Every tradition the culture has recorded (saved in <see cref="CultureExtensionState.traditions"/>).</summary>
[Serializable]
public class TraditionState
{
    public List<TraditionInstance> instances = new List<TraditionInstance>();
    public int nextId = 1;
}

/// <summary>What happened to a tradition this Seventh (for the player, the heritage and the effects).</summary>
public enum TraditionChangeKind
{
    Emerged,
    Established,
    Offered,
    Lapsed,
    Revived,
    Reemerging,
}

public readonly struct TraditionChange
{
    public readonly TraditionChangeKind kind;
    public readonly TraditionInstance instance;
    /// <summary>The first time this has happened to it (the heritage remembers it; later times are only told).</summary>
    public readonly bool first;

    public TraditionChange(TraditionChangeKind kind, TraditionInstance instance, bool first)
    {
        this.kind = kind; this.instance = instance; this.first = first;
    }
}
