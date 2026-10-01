using System;
using System.Collections.Generic;
using System.Linq;

// Tactical roles are independent of a magical working's Major/Minor binding weight.
public enum BattleNoteRole { Either, Core, Minor }
public enum BattleChordCause
{
    Committed, NoteFailed, Stabilized, StabilizationFailed, Collapsed, Resolved, Reaction, CascadeLimit, AttachmentShock,
    Interrupted, Suspended, Grounded, Cancelled, Fallback, Missed, PerfectEcho, HoldDrain, StabilizationCadence, StaticCriticality, Retracted, Delayed, Accelerated,
    CoRegulation, ClimaxComposed, ClimaxFused, GrandResolution, ResolutionDeclared, SympatheticCollapse,
}
public enum BattleReactionTrigger { Incoming, Guarded, Damaged, Moved, AllyFell, WeaveCollapsed, EnemyEntersAdjacent, EnemyEntersLane, AllyMindBroken, ConductorFaltered }

/// <summary>
/// Echoing Bond and repetition magnitudes (proposals). The Hold Limit, Interference and Collapse rules are canon and
/// live in <see cref="BattleMeasureTuning"/>.
/// </summary>
[Serializable]
public sealed class BattleChordTuning
{
    public int maxRepeats = 16;
    [UnityEngine.Tooltip("A bond at least this strong is an Echoing Bond in battle: +1 Hold on a shared Ensemble, duets, emergency protection.")]
    public float bondThreshold = .6f;
    [UnityEngine.Tooltip("Consecutive bonded voices: Minor effects stronger by this share of the bond, and casting flicker divided by 1 + the bond.")]
    public float bondPower = .1f;
    public float attachmentShock = .2f, emergencyGuard = 8f;
}

[Serializable]
public sealed class BattleChordModifier
{
    public float power = 1f, stability;
    public int windUp, holdBeats, capacity, durationBeats, range, repeats = 1;
    public SpellBinding binding;
    public BattleProximity targeting;
    public bool changeTargeting;
    public bool joinBinding, replaceRoot, area, penetrate, advance, synchronize;
    public bool requiresAdjacent = true;
    public List<CardEffect> effects = new List<CardEffect>();
    public BattleChordModifier Clone()
    {
        var copy = (BattleChordModifier)MemberwiseClone();
        copy.effects = effects.Select(e => e?.Clone()).ToList(); return copy;
    }
}

[Serializable]
public sealed class BattleReactionSpec
{
    public BattleReactionTrigger trigger;
    public int range = 1, uses = 1, durationBeats = 8;
    public bool bondOnly, enemyTrigger;
    [UnityEngine.Tooltip("Intercept: the reactor steps into the triggering enemy's hex and forces the engagement early.")]
    public bool intercept;
    public BattleReactionSpec Clone() => (BattleReactionSpec)MemberwiseClone();
}

[Serializable]
public sealed class BattleEchoingBond
{
    public string first, second;
    public float strength;
    public BattleEchoingBond Clone() => (BattleEchoingBond)MemberwiseClone();
}

public sealed class BattleChordEvent
{
    public int measure, beat, note = -1;
    public long action;
    public bool attacker;
    public BattleChordCause cause;
    public float interference, integrity, composure;
    public string detail;
}

/// <summary>A composed working as Visualization shows it (with Clarity 1: its Hold and margin).</summary>
public sealed class BattleWeaveView
{
    public long action;
    public bool attacker, coreLost, awaitingTrigger, suspended, hyper;
    public string core;
    public BattleChordState state;
    public BattleFooting footing;
    /// <summary>First Beat it sounds, the Beat its Major resolves (dueBeat), and the last Beat its Hold allows (deadline).</summary>
    public int firstBeat, dueBeat, deadline, holdLimit, sounding, margin, attempts, criticality;
    public int load, capacity, failedAttempts;
    public float interference, stabilizationRisk, compatibility;
    public List<string> notes;
}

public sealed class BattleReactionView
{
    public long id;
    public bool attacker;
    public int voice, remainingUses, expires;
    public string card;
    public BattleReactionSpec rule;
}

/// <summary>Pure composition: one Core's identity survives every ordered structural modifier.</summary>
public static class BattleChordLogic
{
    public static CombatCard Compose(CombatCard core, IEnumerable<CombatCard> minors, BattleChordTuning tuning, int defaultDurationBeats = 4)
    {
        var result = core.Clone();
        int repetitions = 1;
        foreach (var minor in minors)
        {
            var mod = minor.chord;
            if (mod == null) throw new ArgumentException("A Minor needs an explicit structural modifier.");
            if (mod.replaceRoot) { result.binding = mod.binding; result.minors.Remove(mod.binding); }
            else if (mod.joinBinding && mod.binding != SpellBinding.Unattuned && mod.binding != result.binding && !result.minors.Contains(mod.binding)) result.minors.Add(mod.binding);
            result.weaving |= minor.kind == CardKind.Spell || minor.weaving || mod.binding != SpellBinding.Unattuned;
            int nextRepetitions = (int)Math.Min(Math.Max(1, tuning.maxRepeats), (long)repetitions * Math.Max(1, mod.repeats));
            foreach (var effect in result.effects)
            {
                if (effect.op == CardOp.Strike || effect.op == CardOp.Spell || effect.op == CardOp.IntegrityDamage || effect.op == CardOp.Dread || effect.op == CardOp.Guard || effect.op == CardOp.Ward)
                    effect.amount *= Math.Max(0f, mod.power) * nextRepetitions / repetitions;
                if (mod.range > 0) effect.range = Math.Max(effect.range, mod.range);
                if (mod.durationBeats > 0) effect.durationBeats = (effect.durationBeats > 0 ? effect.durationBeats : Math.Max(1, defaultDurationBeats)) + mod.durationBeats;
                if (mod.changeTargeting) effect.proximity = mod.targeting;
                effect.ignoreGuard |= mod.penetrate;
                effect.ignoreArmor |= mod.penetrate;
                if (mod.area && effect.aim == CardAim.Enemy) effect.aim = CardAim.EnemyLine;
                if (mod.area && (effect.aim == CardAim.Ally || effect.aim == CardAim.Self &&
                    (effect.op == CardOp.Guard || effect.op == CardOp.Ward || effect.op == CardOp.Mend || effect.op == CardOp.Rally))) effect.aim = CardAim.Allies;
            }
            repetitions = nextRepetitions;
            // The Minor's own effects sound on its own Beat (the resolver plays them there), not at the Core's release.
        }
        return result;
    }
}
