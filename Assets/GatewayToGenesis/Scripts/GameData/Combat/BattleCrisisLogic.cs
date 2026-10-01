using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleCrisisCause { MindBreak, CorruptedResolution, CorrodedGambit, CoRegulation, ParasiticResonance, ConductorMaladaptation, Steadied, OrdinaryBreak }

/// <summary>
/// The maladaptive face of a Legend's traits (vault: Combat System.md, "Maladaptive Legend Traits"): Compassion becomes
/// self-erasure, Confidence reckless certainty, Devotion obsession, Caution paralysis, Ambition grandiosity,
/// Protectiveness possessive control, and a manipulator paranoid control of everyone's position. Memories and wounds
/// decide when the traits are silent. Saved by index: append only.
/// </summary>
public enum BattleMaladaptation { RawInstinct, RecklessCertainty, SelfErasure, Obsession, Paralysis, Grandiosity, PossessiveControl, Paranoia, AbandonmentPanic, DesperateDefiance }

/// <summary>How a Mind Broken Conductor's army inherits its maladaptive behavior (vault: "Conductor Mind Break"). Append only.</summary>
public enum BattleConductorMaladaptation { None, Aggressive, Fearful, Controlling }

/// <summary>What an ordinary section's Mind Break looks like at its scale (vault: "Mind Break"). Append only.</summary>
public enum BattleOrdinaryBreak { None, Freeze, Panic, Rout, Surrender }

/// <summary>Magnitudes the reference does not fix (proposals, tune freely). The rules they serve are canon.</summary>
[Serializable]
public sealed class BattleCrisisTuning
{
    [UnityEngine.Tooltip("Corrupted Instinct output: the larger of this and the performer's attack times instinctAttack.")]
    public float instinctBase = 35f, instinctAttack = 8f;
    [UnityEngine.Tooltip("Emotional Authenticity: extra output at zero Battle Composure, scaled down to none at the Spiraling boundary.")]
    public float authenticity = .5f;
    [UnityEngine.Tooltip("Output multiplier while below half Integrity (current wounds feed the compulsion).")]
    public float wounded = 1.15f;
    [UnityEngine.Tooltip("Battle Composure dealt to each ally displaced or gathered by a compulsion (the reference's sample: 15).")]
    public float collateral = 15f;
    [UnityEngine.Tooltip("Cathartic Cadenza output: the larger of this and the performer's attack times cadenzaAttack.")]
    public float cadenzaBase = 70f, cadenzaAttack = 12f;
    [UnityEngine.Tooltip("Battle Composure share a Corroded Gambit restores (at least).")]
    public float gambitRestore = .35f;
    [UnityEngine.Tooltip("Co-Regulation steadies the patient for the Measure: further Battle Composure loss is multiplied by this.")]
    public float steadiedLoss = .5f;
    [UnityEngine.Tooltip("A Conductor's Corroded Gambit: every standing ally regains this share of its maximum Composure, the formation this share of maximum Stance Stability.")]
    public float conductorGambitComposure = .15f, conductorGambitStance = .1f;
    [UnityEngine.Tooltip("Aggressive Conductor Mind Break: allied steel lands this share harder and every ordinary section is exposed by this share.")]
    public float aggressiveSurge = .15f, aggressiveExposure = .15f;
    [UnityEngine.Tooltip("Fearful Conductor Mind Break: the army abandons the fight once its line's Composure falls to this share.")]
    public float fearfulWithdraw = .2f;
    [UnityEngine.Tooltip("Controlling Conductor Mind Break: Command Bandwidth lost, and Composure spilled each Toll onto allies beside the Conductor.")]
    public int controllingBandwidth = 1;
    public float controllingSpill = 6f;
    [UnityEngine.Tooltip("Ordinary Mind Break by size: at most this many combatants freeze; from this many a broken section routs instead of panicking.")]
    public int freezeUpTo = 10, routFrom = 1000;
}

[Serializable]
public sealed class BattleCrisisEvent
{
    public int measure, beat, voice, resolved;
    public bool attacker;
    public BattleCrisisCause cause;
    public string character, detail;
}

public sealed class BattleCrisisView
{
    public int voice, resolvedThisMeasure;
    public string character, expression;
    public bool mindBroken, cadenzaReady, steadied;
    public float battleComposure, maximum, persistentStrain;
}

/// <summary>Authored crisis cards. Trait inversions shape the compulsion and its Cadenza; persistent psychology is never healed here.</summary>
public static class BattleCrisisLogic
{
    private static readonly BattleCrisisTuning Defaults = new BattleCrisisTuning();

    // Personality Legend Traits (vault: Legend Trait.md) grouped by the maladaptive face the reference names.
    private static readonly (BattleMaladaptation face, string[] traits)[] Faces =
    {
        (BattleMaladaptation.SelfErasure, new[] { "compassion", "empath", "selfless", "trusting", "submissive", "healer", "grief" }),
        (BattleMaladaptation.RecklessCertainty, new[] { "confiden", "reckless", "fierce", "hot-headed", "hotheaded", "arrogan", "volatile", "impatien", "fearless", "presumptu" }),
        (BattleMaladaptation.Obsession, new[] { "devot", "zealo", "obsess", "dogmat", "vengeful", "jealous", "reveren", "stubborn" }),
        (BattleMaladaptation.Paralysis, new[] { "cautio", "coward", "anxio", "hesitan", "shy", "fearful", "melanchol", "numb", "apathe", "lazy" }),
        (BattleMaladaptation.Grandiosity, new[] { "ambitio", "opportun", "charisma", "delusion", "self-deceiv", "grandios" }),
        (BattleMaladaptation.PossessiveControl, new[] { "possessive", "protect", "control", "pragmati", "principled", "diligent" }),
        (BattleMaladaptation.Paranoia, new[] { "manipulat", "paranoi", "distrust", "cynic", "skeptic", "enigma" }),
    };

    /// <summary>The strongest maladaptive face among the Legend's traits; memories, then wounds, when the traits are silent.</summary>
    public static BattleMaladaptation Maladaptation(BattleLegend legend, CombatSection unit)
    {
        var traits = (legend?.traits ?? new List<string>()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x.ToLowerInvariant()).ToList();
        int best = 0; var face = BattleMaladaptation.RawInstinct;
        foreach (var (candidate, fragments) in Faces)
        {
            int score = traits.Count(trait => fragments.Any(trait.Contains));
            if (score > best) { best = score; face = candidate; }
        }
        if (best > 0) return face;
        string memories = string.Join(" ", legend?.memories ?? new List<string>()).ToLowerInvariant();
        if (memories.Contains("lost") || memories.Contains("missing") || memories.Contains("died") || memories.Contains("abandon")) return BattleMaladaptation.AbandonmentPanic;
        if (memories.Contains("won") || memories.Contains("defiance") || unit?.IntegrityShare < .5f || (legend?.conditions?.Count ?? 0) > 0) return BattleMaladaptation.DesperateDefiance;
        return BattleMaladaptation.RawInstinct;
    }

    public static string Name(BattleMaladaptation face)
    {
        switch (face)
        {
            case BattleMaladaptation.RecklessCertainty: return "Reckless certainty";
            case BattleMaladaptation.SelfErasure: return "Self-erasure";
            case BattleMaladaptation.Obsession: return "Obsession";
            case BattleMaladaptation.Paralysis: return "Paralysis";
            case BattleMaladaptation.Grandiosity: return "Grandiosity";
            case BattleMaladaptation.PossessiveControl: return "Possessive control";
            case BattleMaladaptation.Paranoia: return "Paranoia";
            case BattleMaladaptation.AbandonmentPanic: return "Abandonment panic";
            case BattleMaladaptation.DesperateDefiance: return "Desperate defiance";
            default: return "Raw instinct";
        }
    }

    public static string Expression(BattleLegend legend, CombatSection unit) => Name(Maladaptation(legend, unit));

    /// <summary>The opposing force hidden beneath the maladaptive trait, which the Cathartic Cadenza draws on.</summary>
    public static string Opposite(BattleMaladaptation face)
    {
        switch (face)
        {
            case BattleMaladaptation.RecklessCertainty: return "Humility";
            case BattleMaladaptation.SelfErasure: return "Self-worth";
            case BattleMaladaptation.Obsession: return "Release";
            case BattleMaladaptation.Paralysis: return "Courage";
            case BattleMaladaptation.Grandiosity: return "Service";
            case BattleMaladaptation.PossessiveControl: return "Trust";
            case BattleMaladaptation.Paranoia: return "Improvisation";
            case BattleMaladaptation.AbandonmentPanic: return "Belonging";
            case BattleMaladaptation.DesperateDefiance: return "Acceptance";
            default: return "Direction";
        }
    }

    /// <summary>How the army inherits a Mind Broken Conductor's personality: aggressive, fearful or controlling.</summary>
    public static BattleConductorMaladaptation Conductor(BattleLegend legend, CombatSection unit)
    {
        switch (Maladaptation(legend, unit))
        {
            case BattleMaladaptation.RecklessCertainty: case BattleMaladaptation.Grandiosity: case BattleMaladaptation.Obsession: case BattleMaladaptation.DesperateDefiance:
                return BattleConductorMaladaptation.Aggressive;
            case BattleMaladaptation.PossessiveControl: case BattleMaladaptation.Paranoia:
                return BattleConductorMaladaptation.Controlling;
            case BattleMaladaptation.Paralysis: case BattleMaladaptation.SelfErasure: case BattleMaladaptation.AbandonmentPanic:
                return BattleConductorMaladaptation.Fearful;
            default: return BattleConductorMaladaptation.None; // Nothing known of the character: the army loses its tempo, nothing more.
        }
    }

    /// <summary>Emotional Authenticity: raw output rises as regulation fails, and current wounds feed it.</summary>
    public static float Authenticity(CombatSection unit, float spiralingBelow, BattleCrisisTuning tuning = null)
    {
        tuning = tuning ?? Defaults;
        float share = unit == null ? spiralingBelow : Math.Max(0f, unit.ComposureShare);
        float depth = spiralingBelow <= 0f ? 0f : Math.Max(0f, Math.Min(1f, (spiralingBelow - share) / spiralingBelow));
        return (1f + tuning.authenticity * depth) * (unit?.IntegrityShare < .5f ? tuning.wounded : 1f);
    }

    private static CardEffect Enemy(CardOp op, float amount, bool line = false) =>
        new CardEffect(op, line ? CardAim.EnemyLine : CardAim.Enemy, amount) { range = 16, ignoreGuard = true, proximity = BattleProximity.ForwardMost, flat = op == CardOp.Dread };
    private static CardEffect Allies(CardOp op, float amount, int range = 1) => new CardEffect(op, CardAim.Allies, amount) { range = range, flat = op == CardOp.Guard || op == CardOp.Ward };
    private static CardEffect Self(CardOp op, float amount) => new CardEffect(op, CardAim.Self, amount) { flat = op == CardOp.Guard || op == CardOp.Ward };

    /// <summary>
    /// A Corrupted Instinct Card: a compulsion, not a composition. It ignores the one-Major rule, sounds on any Beat,
    /// resolves through Rank Proximity, and is frequently stronger than the Legend's normal cards with severe collateral.
    /// Variation 0 is the face's signature compulsion; variation 1 its second.
    /// </summary>
    public static CombatCard Instinct(BattleLegend legend, CombatSection unit, int variation, float spiralingBelow = .25f, BattleCrisisTuning tuning = null)
    {
        tuning = tuning ?? Defaults;
        var face = Maladaptation(legend, unit);
        float s = Math.Max(tuning.instinctBase, (unit?.attack ?? 0f) * tuning.instinctAttack) * Authenticity(unit, spiralingBelow, tuning);
        float c = tuning.collateral;
        string name; var effects = new List<CardEffect>(); string line;
        bool first = variation % 2 == 0;
        switch (face)
        {
            case BattleMaladaptation.SelfErasure:
                if (first) { name = "Take It All Onto Me"; line = "Every blow meant for them, on me."; effects.Add(Allies(CardOp.Guard, s * 1.5f)); effects.Add(Self(CardOp.Recoil, .2f)); effects.Add(Enemy(CardOp.Dread, 20)); }
                else { name = "Let Me Be the One"; line = "Someone has to pay; let it be me."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.2f)); effects.Add(Allies(CardOp.Mend, .1f)); effects.Add(Self(CardOp.Recoil, .15f)); }
                break;
            case BattleMaladaptation.Obsession:
                if (first) { name = "Only That One"; line = "Nothing else on the field exists."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.4f)); effects.Add(Enemy(CardOp.Expose, .25f)); effects.Add(Self(CardOp.SpatialChaos, c)); }
                else { name = "Again, and Again"; line = "Until it stops moving."; effects.Add(Enemy(CardOp.IntegrityDamage, s * .7f)); effects.Add(Enemy(CardOp.IntegrityDamage, s * .7f)); effects.Add(Allies(CardOp.Detune, 1)); }
                break;
            case BattleMaladaptation.Paralysis:
                if (first) { name = "Hold Everything Still"; line = "If nothing moves, nothing else can go wrong."; effects.Add(Self(CardOp.Guard, s * 1.5f)); effects.Add(Enemy(CardOp.Delay, 1)); effects.Add(new CardEffect(CardOp.Pollute, CardAim.Allies, 1) { range = 1, pollution = BattlePollution.Restrained }); }
                else { name = "Don't Move"; line = "Stop. All of you. Stop."; effects.Add(Enemy(CardOp.Delay, 1)); effects.Add(Enemy(CardOp.Dread, 30)); effects.Add(Allies(CardOp.Delay, 1)); }
                break;
            case BattleMaladaptation.Grandiosity:
                if (first) { name = "Witness Me"; line = "Every eye on the field belongs on me."; effects.Add(Enemy(CardOp.IntegrityDamage, s * .9f, line: true)); effects.Add(Allies(CardOp.Dread, c * .7f)); effects.Add(Self(CardOp.Expose, .25f)); }
                else { name = "All of It Is Mine"; line = "The whole line, by my hand alone."; effects.Add(Enemy(CardOp.CatharticBlast, s * .8f)); effects.Add(Enemy(CardOp.Dread, 20)); }
                break;
            case BattleMaladaptation.PossessiveControl:
                if (first) { name = "Stay Where I Can See You"; line = "No one leaves my side again."; effects.Add(Self(CardOp.Gather, c)); effects.Add(Allies(CardOp.Guard, s)); effects.Add(Enemy(CardOp.Interrupt, 1)); }
                else { name = "Mine to Protect"; line = "I decide who is safe."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.1f)); effects.Add(Allies(CardOp.Dread, c * .7f)); effects.Add(Allies(CardOp.Guard, s * .5f)); }
                break;
            case BattleMaladaptation.Paranoia:
                if (first) { name = "Everyone Into Place"; line = "Every piece where I put it; trust nothing else."; effects.Add(Self(CardOp.SpatialChaos, c)); effects.Add(Enemy(CardOp.Interrupt, 2)); effects.Add(Self(CardOp.Veil, 1)); }
                else { name = "Trust No Voice"; line = "Any of them could be the one."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.2f)); effects.Add(Allies(CardOp.Detune, 1)); }
                break;
            case BattleMaladaptation.AbandonmentPanic:
                if (first) { name = "Don't Leave Me"; line = "Not again. Not you too."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.1f)); effects.Add(Self(CardOp.Gather, c)); }
                else { name = "I Won't Lose Another"; line = "Whatever it costs."; effects.Add(Allies(CardOp.Guard, s * 1.2f)); effects.Add(Self(CardOp.Recoil, .12f)); effects.Add(Enemy(CardOp.Dread, 25)); }
                break;
            case BattleMaladaptation.DesperateDefiance:
                if (first) { name = "Not While I Breathe"; line = "Bleeding is not stopping."; effects.Add(Enemy(CardOp.IntegrityDamage, s * 1.35f)); effects.Add(Enemy(CardOp.Dread, 25)); effects.Add(Self(CardOp.Recoil, .1f)); }
                else { name = "Through Them"; line = "The way out is through."; effects.Add(Enemy(CardOp.IntegrityDamage, s)); effects.Add(Self(CardOp.SpatialChaos, c)); }
                break;
            default: // Reckless certainty and the raw instinct of a Legend whose traits are unknown: the reference's sample card.
                if (first) { name = "Break Them Before They Leave"; line = "Power without restraint."; effects.Add(Enemy(CardOp.IntegrityDamage, s * (face == BattleMaladaptation.RecklessCertainty ? 1.3f : 1f))); effects.Add(Enemy(CardOp.Dread, 25)); effects.Add(Self(CardOp.SpatialChaos, c)); }
                else { name = face == BattleMaladaptation.RecklessCertainty ? "Nothing Can Touch Me" : "Lash Out"; line = "Guard down, all in."; effects.Add(Enemy(CardOp.IntegrityDamage, s * (face == BattleMaladaptation.RecklessCertainty ? 1.3f : 1.1f))); effects.Add(Self(CardOp.Expose, .3f)); }
                break;
        }
        return new CombatCard { id = "corrupted-instinct-" + variation, name = name, kind = CardKind.Instinct, corrupted = true, exhaust = true, tempo = CardTempo.Staccato,
            channel = 1, purpose = effects.Any(e => e.aim == CardAim.Enemy || e.aim == CardAim.EnemyLine) ? SpellPurpose.Offensive : SpellPurpose.Defensive,
            catchline = Name(face) + ": " + line, effects = effects };
    }

    /// <summary>
    /// The Cathartic Cadenza: regained direction drawn from the force opposing the maladaptive trait. Its core is a
    /// Rank Proximity blast that still reaches nearby allies when the enemy frontline it expected is gone.
    /// </summary>
    public static CombatCard Cadenza(BattleLegend legend, CombatSection unit, BattleCrisisTuning tuning = null)
    {
        tuning = tuning ?? Defaults;
        var face = Maladaptation(legend, unit);
        float s = Math.Max(tuning.cadenzaBase, (unit?.attack ?? 0f) * tuning.cadenzaAttack);
        var effects = new List<CardEffect> { Enemy(CardOp.CatharticBlast, s) };
        string name;
        switch (face)
        {
            case BattleMaladaptation.Paralysis: name = "Step Into the Fire"; effects.Add(Allies(CardOp.Rally, 20, 2)); break;
            case BattleMaladaptation.PossessiveControl: name = "Carry It Together"; effects[0].amount = s * .8f; effects.Add(Allies(CardOp.Rally, 25, 2)); effects.Add(Allies(CardOp.Attune, 1, 2)); break;
            case BattleMaladaptation.Paranoia: name = "Unwritten Cadenza"; effects.Add(Allies(CardOp.Accelerate, 1, 2)); effects.Add(Self(CardOp.Draw, 2)); break;
            case BattleMaladaptation.RecklessCertainty: name = "A Single True Note"; effects[0].amount = s * .6f; effects.Add(Enemy(CardOp.IntegrityDamage, s * .8f)); break;
            case BattleMaladaptation.SelfErasure: name = "I Am Worth Saving Too"; effects.Add(Self(CardOp.Mend, .3f)); effects.Add(Allies(CardOp.Rally, 15, 2)); break;
            case BattleMaladaptation.Obsession: name = "Let It Go"; effects.Add(new CardEffect(CardOp.Purge, CardAim.Self, 1) { pollution = BattlePollution.Panic }); effects.Add(Allies(CardOp.Rally, 15, 2)); break;
            case BattleMaladaptation.Grandiosity: name = "For Them, Not Me"; effects[0].amount = s * .8f; effects.Add(Self(CardOp.Surge, .2f)); effects.Add(Self(CardOp.Crescendo, .2f)); break;
            case BattleMaladaptation.AbandonmentPanic: name = "Still Here"; effects.Add(Allies(CardOp.Rally, 25, 2)); break;
            case BattleMaladaptation.DesperateDefiance: name = "Even Now"; effects.Add(Self(CardOp.Mend, .2f)); break;
            default: name = "Cathartic Cadenza"; break;
        }
        return new CombatCard { id = "cathartic-cadenza", name = name, kind = CardKind.Instinct, cathartic = true, exhaust = true, tempo = CardTempo.Staccato, channel = 1,
            purpose = SpellPurpose.Offensive, catchline = $"{Opposite(face)} beneath {Name(face).ToLowerInvariant()}: regained direction, not serenity; the blast still reaches nearby allies.",
            effects = effects };
    }

    /// <summary>Co-Regulation stabilizes corrupted cards: the compulsion keeps its power but loses its collateral on allies and self.</summary>
    public static CombatCard Steady(CombatCard card)
    {
        if (card == null || !card.corrupted || card.name.EndsWith(" (steadied)", StringComparison.Ordinal)) return card;
        var steady = card.Clone();
        steady.effects = card.effects.Where(e => e != null && !(e.op == CardOp.SpatialChaos || e.op == CardOp.Gather || e.op == CardOp.Recoil || e.op == CardOp.Expose && e.aim == CardAim.Self ||
            (e.aim == CardAim.Allies || e.aim == CardAim.Ally) && (e.op == CardOp.Dread || e.op == CardOp.Delay || e.op == CardOp.Detune || e.op == CardOp.Pollute)))
            .Select(e => e.Clone()).ToList();
        steady.name = card.name + " (steadied)";
        return steady;
    }

    /// <summary>An ordinary section's Mind Break by its size: a handful freezes, a company panics, an army routs; engaged and broken, it surrenders.</summary>
    public static BattleOrdinaryBreak OrdinaryBreak(CombatSection unit, bool engaged, bool captors, BattleCrisisTuning tuning = null)
    {
        tuning = tuning ?? Defaults;
        if (unit == null || !unit.mindBroken || unit.eliteRole != BattleEliteRole.None) return BattleOrdinaryBreak.None;
        if (engaged && captors) return BattleOrdinaryBreak.Surrender;
        int size = Math.Max(1, unit.Alive);
        return size <= tuning.freezeUpTo ? BattleOrdinaryBreak.Freeze : size >= tuning.routFrom ? BattleOrdinaryBreak.Rout : BattleOrdinaryBreak.Panic;
    }
}
