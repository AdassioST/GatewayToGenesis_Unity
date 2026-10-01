using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Symphony of War's cards when World.asset lists none (<see cref="SymphonySettings"/>), with no scene state, written
/// in the Sonata website's Spell Builder format (<see cref="CardFace"/>): a Root and Minor Notes, one of the five
/// Purposes, the practice The Registers of Magic names, a catchline for the face, and for spells the image the weaver
/// holds. Every card, cost and amount is a proposal; the vault's names are kept (the seven Principles of Magic for the
/// leitmotif cards, the owner's unit cards' quotes, the seven Symphony Cards of Act I by their asset ids, the Registers'
/// practices).
///
/// Where the cards come from:
/// - **every unit brings a basic deck of five: 2 Offensive, 2 Defensive, 1 Utility** (<see cref="Sections"/>): each
///   section kind, the sections of an expedition party (<see cref="PartyDeck"/>), and every creature (<see cref="Creature"/>);
/// - an expedition's kit adds its specialty (<see cref="Kits"/>: traps, orders of march, a mender's satchel), and is
///   where Setup and Modulation live;
/// - a legend carries its personal grimoire (<see cref="LegendGrimoires"/>), a commander its orders;
/// - **anything left with no card at all falls back on Struggle (Offensive) and Endure (Defensive)**.
/// </summary>
public static class SymphonyCards
{
    // ===== BUILDING BLOCKS =====

    private static CardEffect E(CardOp op, CardAim aim, float amount = 1f) => new CardEffect(op, aim, amount);

    private static CombatCard C(string id, string name, SpellPurpose purpose, CardKind kind, int cost, CardVoice voice, string catchline, params CardEffect[] effects) =>
        new CombatCard { id = id, name = name, purpose = purpose, kind = kind, cost = cost, voice = voice, catchline = catchline, effects = effects.ToList() };

    /// <summary>A spell: its Root (Unattuned: its caster's own), weight, practice, catchline and the image it asks for.</summary>
    private static CombatCard Spell(string id, string name, SpellPurpose purpose, SpellBinding binding, NoteWeight weight, int cost, string practice,
        string catchline, string vision, string anchor, params CardEffect[] effects)
    {
        var c = C(id, name, purpose, CardKind.Spell, cost, binding == SpellBinding.Unattuned ? CardVoice.Caster : CardVoice.Any, catchline, effects);
        c.binding = binding;
        c.weight = weight;
        c.practice = practice;
        c.vision = vision;
        c.canonAnchor = anchor;
        return c;
    }

    private static CombatCard At(this CombatCard c, float bonus, params BattleGround[] grounds)
    {
        c.grounds = grounds.ToList();
        c.groundBonus = bonus;
        return c;
    }

    private static CombatCard When(this CombatCard c, CardCondition condition, float bonus, bool requires = false)
    {
        c.condition = condition;
        c.conditionBonus = bonus;
        c.requires = requires;
        return c;
    }

    private static CombatCard Anchor(this CombatCard c, string anchor) { c.canonAnchor = anchor; return c; }
    private static CombatCard Lore(this CombatCard c, string description) { c.description = description; return c; }
    private static CombatCard Once(this CombatCard c) { c.exhaust = true; return c; }
    private static CombatCard Core(this CombatCard c, BattleChordModifier movement = null)
    { c.noteRole = BattleNoteRole.Core; c.chord = movement; return c; }
    private static CombatCard Reaction(this CombatCard c, BattleReactionSpec rule) { c.reaction = rule; return c; }
    private static CombatCard Ornament(string id, string name, SpellBinding binding, BattleChordModifier modifier, string description)
    {
        var card = Spell(id, name, SpellPurpose.Modulation, binding, NoteWeight.Minor, 1, "Chord Layering", description,
            "The Core, held coherently with this ornament.", "Combat System.md: Minor Notes — Ornaments Around the Core");
        card.noteRole = BattleNoteRole.Minor; card.chord = modifier; modifier.binding = binding; return card;
    }

    private const SpellPurpose O = SpellPurpose.Offensive, D = SpellPurpose.Defensive, U = SpellPurpose.Utility, S = SpellPurpose.Setup, M = SpellPurpose.Modulation;
    private const CardKind Steel = CardKind.Attack, Drill = CardKind.Skill;

    // ===== THE CARDS =====

    public static readonly IReadOnlyList<CombatCard> Cards = new List<CombatCard>
    {
        C("field-medicine", "Field Medicine", U, Drill, 1, CardVoice.Any, "Spend a Note cleaning the future hand.",
            E(CardOp.Purge, CardAim.Ally), E(CardOp.Mend, CardAim.Ally, .1f)).Anchor("Combat System.md: Status Pollution"),
        C("cinder-metabolism", "Cinder Metabolism", O, Steel, 1, CardVoice.Any, "Pain becomes fuel, while the burns still hurt.",
            new CardEffect(CardOp.StatusFuel, CardAim.Self, .25f) { pollution = BattlePollution.Burn }, E(CardOp.Strike, CardAim.Enemy, 1f)).Anchor("Combat System.md: Status Pollution and Builds"),
        C("embrace-agony", "Embrace Agony", O, Steel, 1, CardVoice.Any, "The damaged self gives its next blow weight.",
            new CardEffect(CardOp.StatusFuel, CardAim.Self, .25f) { pollution = BattlePollution.Agony }, E(CardOp.Strike, CardAim.Enemy, 1f)).Anchor("Combat System.md: Status Pollution and Builds"),
        // Structural examples are available to authored grimoires/gear/scores; basic five-card decks stay authored below.
        C("piercing-charge", "Piercing Charge", O, Steel, 1, CardVoice.Any, "Charge forward and strike.",
            new CardEffect(CardOp.Strike, CardAim.Enemy, 1.2f) { range = 2 }).Core(new BattleChordModifier { advance = true })
            .Anchor("Combat System.md: Example Chord: Piercing Charge"),
        Ornament("crystal-stability", "Crystal Stability", SpellBinding.Crystal, new BattleChordModifier { stability = 2f },
            "Stabilize the Core and make it harder to interrupt."),
        Ornament("resonance-synchronization", "Resonance Synchronization", SpellBinding.Resonance, new BattleChordModifier { synchronize = true },
            "Synchronize contributing allies' movement with the Core."),
        Ornament("strand-momentum", "Strand Momentum", SpellBinding.Strand, new BattleChordModifier { repeats = 2 },
            "Give the Core repeating damage momentum."),
        Spell("holding-pattern", "Holding Pattern", S, SpellBinding.Crystal, NoteWeight.Minor, 1, "Barrier Arts",
            "Hold the promised working longer, within its finite window.", "A coherent working held until its promised release.",
            "Combat System.md: commitment; finite holding extension from the combat design request",
            new CardEffect(CardOp.ExtendHold, CardAim.Ally, 4f) { durationBeats = 8 }),
        Spell("widen-the-weave", "Widen the Weave", S, SpellBinding.Resonance, NoteWeight.Minor, 1, "Phase Locking Arts",
            "Make room for more notes in an ally's working.", "Each note, distinct and coherent in the shared working.",
            "Finite spellweaving capacity from the combat design request", new CardEffect(CardOp.WeaveCapacity, CardAim.Ally, 4f) { durationBeats = 8 }),
        Spell("stabilize-the-core", "Stabilize the Core", M, SpellBinding.Crystal, NoteWeight.Minor, 1, "Barrier Arts",
            "Recover coherent prerequisites; failed attempts make the next harder.", "The original promise restored, without changing its target.",
            "Combat System.md: Commitment and Discordant Interference", E(CardOp.Stabilize, CardAim.Ally, 2f)),
        Spell("desynchronize", "Desynchronize", M, SpellBinding.Resonance, NoteWeight.Minor, 1, "Phase Locking Arts",
            "Delay an enemy's prepared release toward its holding limit.", "The enemy's clock falling out of step.",
            "Combat System.md: Tempo and Syncopation", E(CardOp.Delay, CardAim.Enemy, 2f)),
        C("emergency-guard", "Emergency Guard", D, Drill, 1, CardVoice.Any, "An adjacent ally protected as danger arrives.",
            new CardEffect(CardOp.Guard, CardAim.Ally, 12f) { flat = true }).Reaction(new BattleReactionSpec { trigger = BattleReactionTrigger.Incoming })
            .Anchor("Combat System.md: Reactions and Cascade Chains"),
        C("guarded-riposte", "Guarded Riposte", O, Steel, 1, CardVoice.Any, "Turn a successful guard into a counterattack.",
            E(CardOp.Strike, CardAim.Enemy, .8f)).Reaction(new BattleReactionSpec { trigger = BattleReactionTrigger.Guarded })
            .Anchor("Combat System.md: Reactions and Cascade Chains"),
        Spell("clock-snare", "Clock Snare", U, SpellBinding.Resonance, NoteWeight.Minor, 1, "Phase Locking Arts",
            "An enemy's movement trips a prepared delay.", "The approaching rhythm caught out of step.",
            "Combat System.md: Reactions and Cascade Chains", E(CardOp.Delay, CardAim.Enemy, 2f))
            .Reaction(new BattleReactionSpec { trigger = BattleReactionTrigger.Moved, enemyTrigger = true }),

        // --- Grave Warden (the owner's card: Melee Unit, 1 Attack, 2 Defense) ---------------------------------------
        C("grave-spade-strike", "Grave-Spade Strike", O, Steel, 1, CardVoice.Front, "The spade that digs the graves, swung like the vigil demands.",
            E(CardOp.Strike, CardAim.Enemy, 1f)),
        C("headstone-blow", "Headstone Blow", O, Steel, 1, CardVoice.Front, "A blow that carries the weight of every name it has buried.",
            E(CardOp.Strike, CardAim.Enemy, 0.8f), E(CardOp.Dread, CardAim.Enemy, 3f)),
        C("vigil-stance", "Vigil Stance", D, Drill, 1, CardVoice.Front, "Hold the line the way you hold vigil: still, and unmoved.",
            E(CardOp.Guard, CardAim.Self, 1.2f), E(CardOp.Rally, CardAim.Self, 3f)).When(CardCondition.Defending, 1.3f),
        C("wall-of-mourners", "Wall of Mourners", D, Drill, 1, CardVoice.Any, "Shoulder to shoulder, as at a graveside.",
            E(CardOp.Guard, CardAim.Allies, 0.4f)),
        C("in-the-shadow-of-the-fallen", "In the Shadow of the Fallen", U, Drill, 1, CardVoice.Any, "\"In the shadow of the fallen, we stand.\"",
            E(CardOp.Rally, CardAim.Allies, 3f)).Anchor("The owner's Grave Warden card"),

        // --- Wasteland Archer (the owner's card: Ranged Unit, 2 Attack, 1 Defense) ----------------------------------
        C("ash-volley", "Ash Volley", O, Steel, 1, CardVoice.Back, "\"From the ashes of ruin.\" Arrows over the line, into whatever stands behind it.",
            E(CardOp.Strike, CardAim.EnemyLine, 1f)).Anchor("The owner's Wasteland Archer card"),
        C("loose-from-cover", "Loose from Cover", O, Steel, 1, CardVoice.Back, "Hunters of the ash plains shoot best from where they cannot be seen.",
            E(CardOp.Strike, CardAim.Enemy, 1.2f)).At(1.2f, BattleGround.Forest, BattleGround.Rough, BattleGround.Hills).When(CardCondition.Concealed, 1.4f),
        C("fall-back-and-loose", "Fall Back and Loose", D, Drill, 1, CardVoice.Back, "Give ground a step at a time, and never stop shooting.",
            E(CardOp.Guard, CardAim.Self, 1f), E(CardOp.Rally, CardAim.Self, 2f)),
        C("stakes-in-the-ash", "Stakes in the Ash", D, Drill, 1, CardVoice.Any, "Sharpened wood where the rush will come.",
            E(CardOp.Guard, CardAim.Allies, 0.3f)).When(CardCondition.Defending, 1.3f),
        C("scavenge-arrows", "Scavenge Arrows", U, Drill, 1, CardVoice.Any, "Nothing on the ash plains is wasted, least of all an arrow.",
            E(CardOp.Draw, CardAim.Self, 1f), E(CardOp.Mend, CardAim.Self, 0.03f)),

        // --- Shieldwall ---------------------------------------------------------------------------------------------
        C("shield-bash", "Shield Bash", O, Steel, 1, CardVoice.Front, "A wall that steps forward throws a man out of the line.",
            E(CardOp.Strike, CardAim.Enemy, 0.6f), E(CardOp.Push, CardAim.Enemy, 1f)),
        C("spear-over-the-rim", "Spear over the Rim", O, Steel, 1, CardVoice.Front, "The shields hold; the spears behind them work.",
            E(CardOp.Strike, CardAim.Enemy, 1f)),
        C("lock-shields", "Lock Shields", D, Drill, 1, CardVoice.Front, "Rim over rim: a wall that holds best on open ground.",
            E(CardOp.Guard, CardAim.Self, 1.5f)).At(1.25f, BattleGround.Open),
        C("overlapping-rims", "Overlapping Rims", D, Drill, 1, CardVoice.Front, "Each shield covers the next man's heart.",
            E(CardOp.Guard, CardAim.Allies, 0.4f)),
        C("pass-the-wounded-back", "Pass the Wounded Back", U, Drill, 1, CardVoice.Any, "Hands over hands, the fallen go back through the ranks.",
            E(CardOp.Mend, CardAim.Ally, 0.08f)),

        // --- Ash Warband --------------------------------------------------------------------------------------------
        C("ash-rush", "Ash Rush", O, Steel, 1, CardVoice.Front, "Win in the first rush or not at all.",
            E(CardOp.Strike, CardAim.Enemy, 1.3f)).At(1.15f, BattleGround.Open, BattleGround.Rough).When(CardCondition.Opening, 1.4f),
        C("warband-howl", "Howl of the Warband", O, Drill, 1, CardVoice.Any, "A raiders' cry that makes a line wonder how many more are coming.",
            E(CardOp.Dread, CardAim.Enemy, 8f)),
        C("duck-and-weave", "Duck and Weave", D, Drill, 1, CardVoice.Front, "No armour to speak of, and no need of it.",
            E(CardOp.Guard, CardAim.Self, 1f)),
        C("scatter-and-regroup", "Scatter and Regroup", D, Drill, 1, CardVoice.Any, "Break before the blow lands, and come back together behind it.",
            E(CardOp.Guard, CardAim.Self, 0.7f), E(CardOp.Rally, CardAim.Self, 2f)),
        C("war-paint", "War Paint", U, Drill, 1, CardVoice.Any, "Ash and ochre: the warband stops being afraid of its own face.",
            E(CardOp.Rally, CardAim.Allies, 2f)),

        // --- Vanguard Blades ----------------------------------------------------------------------------------------
        C("drilled-thrust", "Drilled Thrust", O, Steel, 1, CardVoice.Front, "Bronze and hide, in step: the first true soldiers' blow.",
            E(CardOp.Strike, CardAim.Enemy, 1.1f)),
        C("bronze-advance", "Bronze Advance", O, Steel, 1, CardVoice.Front, "The whole rank takes one step, and strikes as one.",
            E(CardOp.Strike, CardAim.EnemyLine, 0.9f)),
        C("hold-the-gap", "Hold the Gap", D, Drill, 1, CardVoice.Front, "Close the ranks where the line thins.",
            E(CardOp.Guard, CardAim.Allies, 0.4f)),
        C("bronze-guard", "Bronze Guard", D, Drill, 1, CardVoice.Front, "Worked bronze, raised in time.",
            E(CardOp.Guard, CardAim.Self, 1.3f)),
        C("signal-horn", "Signal Horn", U, Drill, 1, CardVoice.Any, "One blast: every blade knows where the others are.",
            E(CardOp.Draw, CardAim.Self, 1f), E(CardOp.Rally, CardAim.Allies, 1f)),

        // --- Blade Dancers (Dance.md: the melee archetype of Spellweaving) ------------------------------------------
        Spell("dance-of-blades", "Dance of Blades", O, SpellBinding.Unattuned, NoteWeight.Minor, 2, "Dancing Blade Arts",
            "Battle movements woven into a song: every cut is also a note.", "The next step, already danced, before the body takes it.", "Dance.md: the melee archetype of Spellweaving",
            E(CardOp.Strike, CardAim.Enemy, 0.8f), E(CardOp.Spell, CardAim.Enemy, 0.8f)).At(1.15f, BattleGround.Forest),
        C("flourish", "Flourish", O, Steel, 1, CardVoice.Front, "A cut that is also a bow.",
            E(CardOp.Strike, CardAim.Enemy, 1f)),
        C("flowing-guard", "Flowing Guard", D, Drill, 1, CardVoice.Front, "The dance turns aside what it cannot outpace.",
            E(CardOp.Guard, CardAim.Self, 1f)).At(1.2f, BattleGround.Forest),
        Spell("veil-of-motion", "Veil of Motion", D, SpellBinding.Unattuned, NoteWeight.Minor, 1, null,
            "Too fast to be where the blow lands.", "Yourself, a half-step to the left of where they are looking.", null,
            E(CardOp.Ward, CardAim.Self, 0.15f), E(CardOp.Guard, CardAim.Self, 0.5f)),
        C("weaving-retreat", "Weaving Retreat", U, Drill, 1, CardVoice.Any, "Out of the fight for one bar, to breathe and bind a cut.",
            E(CardOp.Mend, CardAim.Self, 0.05f), E(CardOp.Rally, CardAim.Self, 2f)),

        // --- Longbows -----------------------------------------------------------------------------------------------
        C("plunging-shot", "Plunging Shot", O, Steel, 1, CardVoice.Back, "From above, an arrow falls where no shield is raised.",
            E(CardOp.Strike, CardAim.Enemy, 1.1f)).When(CardCondition.HighGround, 1.4f),
        C("volley-fire", "Volley Fire", O, Steel, 2, CardVoice.Back, "Every bow at once, on the drum.",
            E(CardOp.Strike, CardAim.EnemyLine, 1.6f)),
        C("retire-behind-the-line", "Retire Behind the Line", D, Drill, 1, CardVoice.Back, "Let the front take it; the bows are needed later.",
            E(CardOp.Guard, CardAim.Self, 1f)),
        C("stake-wall", "Stake Wall", D, Drill, 1, CardVoice.Any, "A hedge of stakes the archers stand behind.",
            E(CardOp.Guard, CardAim.Allies, 0.3f)).When(CardCondition.Defending, 1.3f),
        C("fletch-in-the-lull", "Fletch in the Lull", U, Drill, 1, CardVoice.Any, "Feathers and glue between volleys.",
            E(CardOp.Draw, CardAim.Self, 1f), E(CardOp.Mend, CardAim.Self, 0.03f)),

        // --- Spellweaver Circle (a caster's cards take the circle's own binding) ------------------------------------
        Spell("circle-chant", "Circle Chant", O, SpellBinding.Unattuned, NoteWeight.Minor, 1, null,
            "The circle sings its binding in one voice.", "Every voice in the circle landing on the same beat.", null,
            E(CardOp.Spell, CardAim.Enemy, 1f)),
        Spell("sustained-note", "Sustained Note", O, SpellBinding.Unattuned, NoteWeight.Minor, 1, null,
            "Held until the target's own frequency starts to give.", "The note not ending.", null,
            E(CardOp.Spell, CardAim.Enemy, 0.7f), E(CardOp.Expose, CardAim.Enemy, 0.1f)),
        Spell("woven-ward", "Woven Ward", D, SpellBinding.Unattuned, NoteWeight.Minor, 1, null,
            "A held note laid over the formation like a roof.", "A roof of sound over every head you can see.", null,
            E(CardOp.Ward, CardAim.Allies, 0.15f)),
        Spell("held-chord", "Held Chord", D, SpellBinding.Unattuned, NoteWeight.Minor, 1, null,
            "The circle answers the incoming spell with its opposite.", "The enemy's chord, and its mirror.", "Combat System.md: the opposite melody as noise cancellation",
            E(CardOp.Ward, CardAim.Self, 0.2f), E(CardOp.Guard, CardAim.Self, 0.6f)),
        C("attuning-hum", "Attuning Hum", U, Drill, 1, CardVoice.Any, "The circle finds the line's pitch and holds it steady.",
            E(CardOp.Rally, CardAim.Allies, 2f)),

        // --- War Musicians (Music as Catalyst) ----------------------------------------------------------------------
        C("drums-of-dread", "Drums of Dread", O, Drill, 1, CardVoice.Any, "A rhythm the enemy's heart starts to follow against its will.",
            E(CardOp.Dread, CardAim.Enemy, 6f)),
        C("discordant-blare", "Discordant Blare", O, Drill, 1, CardVoice.Any, "Horns pitched a hair off true, aimed at the enemy's ranks.",
            E(CardOp.Dread, CardAim.Enemy, 4f), E(CardOp.Blind, CardAim.Enemy, 0.1f)),
        C("steady-cadence", "Steady Cadence", D, Drill, 1, CardVoice.Any, "The drum slows, and the line's heartbeat slows with it.",
            E(CardOp.Guard, CardAim.Allies, 0.2f), E(CardOp.Rally, CardAim.Allies, 1f)),
        C("counter-melody", "Counter-Melody", D, Drill, 1, CardVoice.Any, "Play the opposite of what is coming, and it cancels.",
            E(CardOp.Ward, CardAim.Allies, 0.08f)).Anchor("Combat System.md: in defense, playing the opposite melody in rhythm as noise cancellation"),
        C("war-drums", "War Drums", U, Drill, 1, CardVoice.Any, "The line's heartbeat, kept by someone else's hands.",
            E(CardOp.Rally, CardAim.Allies, 3f)).Anchor("Combat System.md: Music as Catalyst"),

        // --- Strand Menders -----------------------------------------------------------------------------------------
        Spell("hurting-arts", "Hurting Arts", O, SpellBinding.Strand, NoteWeight.Minor, 1, "Hurting Arts",
            "The same knowledge that closes a wound, turned around.", "The body as it will be after, and the way there.", "The Registers of Magic: Hurting Arts (Strand)",
            E(CardOp.Spell, CardAim.Enemy, 0.6f), E(CardOp.Dread, CardAim.Enemy, 2f)),
        C("scalpel-cut", "Scalpel Cut", O, Steel, 1, CardVoice.Any, "Menders know exactly where to cut.",
            E(CardOp.Strike, CardAim.Enemy, 0.8f)),
        C("triage-screen", "Triage Screen", D, Drill, 1, CardVoice.Any, "Shields propped around the stretchers.",
            E(CardOp.Guard, CardAim.Allies, 0.25f)),
        C("shelter-the-stretchers", "Shelter the Stretchers", D, Drill, 1, CardVoice.Any, "The menders stand over the wounded.",
            E(CardOp.Guard, CardAim.Self, 0.8f), E(CardOp.Ward, CardAim.Self, 0.05f)),
        Spell("strand-mending", "Strand Mending", U, SpellBinding.Strand, NoteWeight.Minor, 1, "Field Medicine Arts",
            "A body reminded how it was before the wound.", "The body before the wound, exactly.", "Strand healing: Object Permanence",
            E(CardOp.Mend, CardAim.Ally, 0.12f)),

        // --- Crystalwrights -----------------------------------------------------------------------------------------
        Spell("crystal-spikes", "Crystal Spikes", O, SpellBinding.Crystal, NoteWeight.Minor, 1, "Conjuration Arts",
            "Points of certainty, grown up through the enemy's footing.", "The spike already there, under their feet.", null,
            E(CardOp.Spell, CardAim.Enemy, 0.7f)),
        Spell("shard-hail", "Shard Hail", O, SpellBinding.Crystal, NoteWeight.Minor, 1, "Fragmentation Arts",
            "A lattice that breaks on purpose, outward.", "Every fracture line, before the break.", null,
            E(CardOp.Spell, CardAim.Enemy, 0.5f), E(CardOp.Expose, CardAim.Enemy, 0.15f)),
        C("raise-a-palisade", "Raise a Palisade", D, Drill, 1, CardVoice.Any, "Crystal shelters raised on demand.",
            E(CardOp.Entrench, CardAim.Self, 1f), E(CardOp.Guard, CardAim.Allies, 0.3f)).When(CardCondition.Defending, 1.3f),
        Spell("crystal-shelter", "Crystal Shelter", D, SpellBinding.Crystal, NoteWeight.Minor, 1, "Barrier Arts",
            "A wall that exists because someone is certain of it.", "The wall, whole, before it is there.", null,
            E(CardOp.Ward, CardAim.Allies, 0.1f), E(CardOp.Guard, CardAim.Self, 0.5f)),
        C("reset-the-stones", "Reset the Stones", U, Drill, 1, CardVoice.Any, "Knocked-down footings put back where they were.",
            E(CardOp.Mend, CardAim.Allies, 0.03f)),

        // --- Pathfinders --------------------------------------------------------------------------------------------
        C("ambush-from-the-brush", "Ambush from the Brush", O, Steel, 1, CardVoice.Any, "They were never where the enemy looked.",
            E(CardOp.Strike, CardAim.Enemy, 1f)).At(1.2f, BattleGround.Forest, BattleGround.Rough).When(CardCondition.Concealed, 1.5f),
        C("harry-the-flanks", "Harry the Flanks", O, Drill, 1, CardVoice.Any, "Always at the edge of sight, always one more.",
            E(CardOp.Dread, CardAim.Enemy, 5f)),
        C("vanish-into-the-ground", "Vanish into the Ground", D, Drill, 1, CardVoice.Any, "A pathfinder not found is a pathfinder not hurt.",
            E(CardOp.Guard, CardAim.Self, 1.2f)),
        C("known-paths", "Known Paths", D, Drill, 1, CardVoice.Any, "The ways back through the ground, shown to everyone.",
            E(CardOp.Guard, CardAim.Allies, 0.3f)),
        C("read-the-ground", "Read the Ground", U, Drill, 1, CardVoice.Any, "Where the footing fails, where the line can be turned.",
            E(CardOp.Draw, CardAim.Self, 2f)),

        // --- An expedition's own (the basic deck of every party section) --------------------------------------------
        C("walking-staff", "Walking Staff", O, Steel, 1, CardVoice.Any, "Every traveller's first weapon.",
            E(CardOp.Strike, CardAim.Enemy, 0.9f)),
        C("sling-stone", "Sling Stone", O, Steel, 1, CardVoice.Any, "A stone picked up on the road, sent back down it.",
            E(CardOp.Strike, CardAim.Enemy, 0.8f)),
        C("circle-the-packs", "Circle the Packs", D, Drill, 1, CardVoice.Any, "Packs and bedrolls stacked into a low wall.",
            E(CardOp.Guard, CardAim.Allies, 0.5f)),
        C("stand-firm", "Stand Firm", D, Drill, 1, CardVoice.Any, "Feet set, staff across the chest.",
            E(CardOp.Guard, CardAim.Self, 1.1f)),
        C("stand-together", "Stand Together", U, Drill, 1, CardVoice.Any, "Shoulder to shoulder, where each can hear the others breathe.",
            E(CardOp.Rally, CardAim.Allies, 3f)),

        // --- Kits: an expedition's specialty (Setup and Modulation live here) --------------------------------------
        C("break-and-regroup", "Break and Regroup", U, Drill, 1, CardVoice.Any, "Fall back a step, bind what bleeds, look again.",
            E(CardOp.Mend, CardAim.Self, 0.06f), E(CardOp.Draw, CardAim.Self, 1f)),
        C("mark-the-weak-point", "Mark the Weak Point", S, Drill, 1, CardVoice.Any, "Show the others where to strike.",
            E(CardOp.Expose, CardAim.Enemy, 0.25f)),
        C("quicken-the-tempo", "Quicken the Tempo", M, Drill, 0, CardVoice.Any, "Once, everyone moves before they think.",
            E(CardOp.Beat, CardAim.Self, 1f)).Once(),
        C("snare-line", "Snare Line", S, Drill, 1, CardVoice.Any, "Cord and stakes where the quarry will run.",
            E(CardOp.Blind, CardAim.Enemy, 0.3f), E(CardOp.Expose, CardAim.Enemy, 0.15f)).At(1.2f, BattleGround.Forest, BattleGround.Rough).When(CardCondition.Hunting, 1.4f),
        C("thrown-spear", "Thrown Spear", O, Steel, 1, CardVoice.Any, "A hunter's spear, thrown before the charge arrives.",
            E(CardOp.Strike, CardAim.Enemy, 1f)).When(CardCondition.Hunting, 1.3f),
        C("drive-the-quarry", "Drive the Quarry", O, Drill, 1, CardVoice.Any, "Noise from three sides, and one way left open.",
            E(CardOp.Dread, CardAim.Enemy, 6f)).When(CardCondition.Hunting, 1.5f),
        C("brace-shields", "Brace Shields", D, Drill, 1, CardVoice.Any, "The warden's round shields, rim to rim.",
            E(CardOp.Guard, CardAim.Allies, 0.7f)),
        C("lantern-watch", "Lantern Watch", S, Drill, 1, CardVoice.Any, "A lantern kept lit through the night: no one is taken by surprise twice.",
            E(CardOp.Rally, CardAim.Allies, 2f), E(CardOp.Entrench, CardAim.Self, 1f)).When(CardCondition.Defending, 1.3f),
        C("take-the-high-ground", "Take the High Ground", O, Steel, 1, CardVoice.Any, "Whoever stands higher strikes down.",
            E(CardOp.Strike, CardAim.Enemy, 1f)).When(CardCondition.HighGround, 1.5f),
        C("hold-the-ford", "Hold the Ford", D, Drill, 1, CardVoice.Any, "Wait on the far bank while they wade.",
            E(CardOp.Guard, CardAim.Allies, 0.6f)).When(CardCondition.RiverCrossing, 1.6f),
        C("read-the-trail", "Read the Trail", S, Drill, 1, CardVoice.Any, "Old roads remember who walked them, and where they stumbled.",
            E(CardOp.Expose, CardAim.Enemy, 0.15f), E(CardOp.Draw, CardAim.Self, 1f)),
        C("bind-wounds", "Bind Wounds", U, Drill, 1, CardVoice.Any, "Clean cloth and a steady hand.",
            E(CardOp.Mend, CardAim.Ally, 0.1f)),
        C("soothing-hum", "Soothing Hum", U, Drill, 1, CardVoice.Any, "A heartbeat slowed to the song's.",
            new CardEffect(CardOp.CoRegulate, CardAim.Ally, 5f) { durationBeats = 3 }).Anchor("Combat System.md: Co-Regulation and the Heartbeat"),
        C("carry-the-fallen", "Carry the Fallen", U, Drill, 1, CardVoice.Any, "No one is left where they fell.",
            E(CardOp.Mend, CardAim.Allies, 0.04f), E(CardOp.Rally, CardAim.Allies, 1f)),

        // --- The fallback: anything with no card of its own -------------------------------------------------------
        C(Struggle, "Struggle", O, Steel, 1, CardVoice.Any, "Nothing left to fight with but the fight itself. It costs the body that throws it.",
            E(CardOp.Strike, CardAim.Enemy, 0.7f), E(CardOp.Recoil, CardAim.Self, 0.03f)),
        C(Endure, "Endure", D, Drill, 1, CardVoice.Any, "Hold on. Just hold on.",
            E(CardOp.Guard, CardAim.Self, 1f), E(CardOp.Rally, CardAim.Self, 2f)),

        // --- The Battle Conductor's orders (a legend's Greats) ----------------------------------------------------
        C("rally-to-me", "Rally to Me", U, CardKind.Command, 1, CardVoice.Commander, "A legend's voice, and the line remembers who it follows.",
            E(CardOp.Rally, CardAim.Allies, 2f)),
        C("great-vanguard", "Press the Attack", M, CardKind.Command, 1, CardVoice.Commander, "Perfect Focus: the blazing arc, carried by every blade already swinging.",
            E(CardOp.Surge, CardAim.Allies, 0.2f)).Anchor("Great Vanguard (Cindergale: Perfect Focus)"),
        C("great-architect", "Hold Fast", D, CardKind.Command, 1, CardVoice.Commander, "Absolute Certainty: the structure does not break.",
            E(CardOp.Guard, CardAim.Allies, 0.5f)).Anchor("Great Architect (Crystal: Absolute Certainty)"),
        C("great-sovereign", "One Key", S, CardKind.Command, 1, CardVoice.Commander, "Key of Attunement: one purpose, one carrier wave, laid under everything to come.",
            E(CardOp.Rally, CardAim.Allies, 3f)).Anchor("Great Sovereign (Resonance: Key of Attunement)"),
        C("great-concertist", "Crescendo", M, CardKind.Command, 1, CardVoice.Commander, "Emotional Authenticity: the song swells and every caster rides it.",
            E(CardOp.Crescendo, CardAim.Allies, 0.25f)).Anchor("Great Concertist (Flux: Emotional Authenticity)"),
        C("great-seer", "Read the Field", S, CardKind.Command, 1, CardVoice.Commander, "Sufficient Precision: see the whole field, and the flaw in it.",
            E(CardOp.Draw, CardAim.Self, 2f), E(CardOp.Expose, CardAim.Enemy, 0.15f)).Anchor("Great Seer (Luminance: Sufficient Precision)"),
        C("great-justiciar", "Sentence", O, CardKind.Command, 1, CardVoice.Commander, "Essence Sacrifice: the Void's cost, felt by the enemy.",
            E(CardOp.Dread, CardAim.Enemy, 10f)).Anchor("Great Justiciar (Void: Essence Sacrifice)"),
        C("great-chronicler", "No One Left Behind", U, CardKind.Command, 1, CardVoice.Commander, "Echoing Bonds: every name is carried home.",
            E(CardOp.Mend, CardAim.Allies, 0.04f), E(CardOp.Rally, CardAim.Allies, 2f)).Anchor("Great Chronicler (Strand: Echoing Bonds)"),

        // --- A legend's own leitmotif: its binding's Principle, and the Purpose that binding is attuned to ----------
        Spell("leitmotif-resonance", "Key of Attunement", S, SpellBinding.Resonance, NoteWeight.Minor, 1, "Phase Locking Arts",
            "The legend's own frequency, laid under the field for every chord to come.", "One pitch, held until everyone around you is singing it.", "The Principles of Magic: Resonance, Key of Attunement",
            E(CardOp.Spell, CardAim.Enemy, 0.5f), E(CardOp.Expose, CardAim.Enemy, 0.15f)),
        Spell("leitmotif-luminance", "Sufficient Precision", O, SpellBinding.Luminance, NoteWeight.Minor, 1, "Beam Arts",
            "Light placed exactly where the flaw is.", "The flaw, and nothing around it.", "The Principles of Magic: Luminance, Sufficient Precision",
            E(CardOp.Spell, CardAim.Enemy, 0.9f), E(CardOp.Expose, CardAim.Enemy, 0.1f)),
        Spell("leitmotif-flux", "Emotional Authenticity", M, SpellBinding.Flux, NoteWeight.Minor, 1, "Tide Singing Arts",
            "A feeling held honestly enough to carry every other spell on its current.", "What you actually feel, not what you should.", "The Principles of Magic: Flux, Emotional Authenticity",
            E(CardOp.Crescendo, CardAim.Allies, 0.2f), E(CardOp.Rally, CardAim.Ally, 3f)),
        Spell("leitmotif-void", "Essence Sacrifice", D, SpellBinding.Void, NoteWeight.Minor, 1, "Siphon Arts",
            "Something given up, and the dark where it was swallows what comes in.", "The thing you are giving, and its absence.", "The Principles of Magic: Void, Essence Sacrifice",
            E(CardOp.Ward, CardAim.Allies, 0.12f), E(CardOp.Blind, CardAim.Enemy, 0.2f)),
        Spell("leitmotif-cindergale", "Perfect Focus", O, SpellBinding.Cindergale, NoteWeight.Minor, 1, "Combustion Arts",
            "Momentum with nowhere else to go.", "One point, and everything moving toward it.", "The Principles of Magic: Cindergale, Perfect Focus",
            E(CardOp.Spell, CardAim.Enemy, 0.9f), E(CardOp.Burn, CardAim.Enemy, 2f)),
        Spell("leitmotif-crystal", "Absolute Certainty", D, SpellBinding.Crystal, NoteWeight.Minor, 1, "Barrier Arts",
            "A wall that exists because the legend is sure of it.", "The wall, finished, standing.", "The Principles of Magic: Crystal, Absolute Certainty",
            E(CardOp.Guard, CardAim.Allies, 0.4f), E(CardOp.Ward, CardAim.Self, 0.1f)),
        Spell("leitmotif-strand", "Echoing Bonds", U, SpellBinding.Strand, NoteWeight.Minor, 1, "Field Medicine Arts",
            "What was, insisted upon: the wound forgets it happened.", "Them, as they were this morning.", "The Principles of Magic: Strand, Echoing Bonds",
            E(CardOp.Mend, CardAim.Ally, 0.1f), E(CardOp.Rally, CardAim.Ally, 2f)),

        // --- The Symphony Cards of Act I (SymphonyCardData ids; battle effects from their assets) ----------------
        Spell("coaxed-spring", "Coaxed Spring", U, SpellBinding.Flux, NoteWeight.Minor, 1, "Stream Arts",
            "Water drawn by a feeling held long enough; it douses what burns.", "Cool water, already running.", "Water Magic (Flux), The Registers of Magic",
            E(CardOp.Douse, CardAim.Allies, 3f), E(CardOp.Mend, CardAim.Ally, 0.04f)),
        Spell("igniting-cooking-pot", "Igniting Cooking Pot", O, SpellBinding.Cindergale, NoteWeight.Minor, 1, "Hearth Arts",
            "An instant spark, turned on whatever stands closest.", "A spark coaxed from damp wood.", "The Principles of Magic, spell crafting example \"Igniting Cooking Pot (Minor Unison)\"",
            E(CardOp.Burn, CardAim.Enemy, 3f), E(CardOp.Spell, CardAim.Enemy, 0.3f)),
        Spell("desperate-humming", "Desperate Humming", U, SpellBinding.Strand, NoteWeight.Minor, 1, "Field Medicine Arts",
            "A wound reminded of how it was before, by a voice that will not stop.", "The skin whole again.", "The Inescapable Hunger: \"A wound closed by desperate humming\"",
            E(CardOp.Mend, CardAim.Ally, 0.1f)),
        Spell("ash-clearing-zephyr", "Ash-Clearing Zephyr", U, SpellBinding.Resonance, NoteWeight.Minor, 1, "Zephyr Arts",
            "A driven wind that goes exactly where it is sent, and takes someone with it.", "The air already moving, one way.", "Zephyr Arts (Resonance), Wind Magic, The Registers of Magic",
            E(CardOp.Push, CardAim.Enemy, 1f), E(CardOp.Spell, CardAim.Enemy, 0.3f)),
        Spell("swallowed-light", "Swallowed Light", U, SpellBinding.Void, NoteWeight.Minor, 1, "Silhouette Arts",
            "The light of a small place, drawn in and held: they strike at shadows.", "The dark, where the lantern was.", "Shadow Magic (Void), The Registers of Magic",
            E(CardOp.Blind, CardAim.Enemy, 0.3f)),
        Spell("conjured-shard", "Conjured Shard", O, SpellBinding.Crystal, NoteWeight.Major, 1, "Conjuration Arts",
            "An imagined edge pressed into the world by certainty.", "The shard, its weight, its edge, already flying.", "Conjuration Arts (Crystal): \"from simple projectiles upward\"",
            E(CardOp.Spell, CardAim.Enemy, 1.2f)),
        Spell("hairs-breadth-truer", "A Hair's Breadth Truer", M, SpellBinding.Luminance, NoteWeight.Minor, 1, "Optical Arts",
            "Precision given to a flight already begun: no arrow in the air misses.", "The arrow's line, and the target at the end of it.", "The Inescapable Hunger: \"An arrow guided a hair's breadth truer\"",
            E(CardOp.Sure, CardAim.Allies, 1f)),
    };

    /// <summary>Card ids of the two fallbacks.</summary>
    public const string Struggle = "struggle", Endure = "endure";

    /// <summary>The section id of an expedition party's basic deck (every one of its sections).</summary>
    public const string PartyDeck = "party";

    /// <summary>The basic deck of every kind of unit: five cards, 2 Offensive, 2 Defensive, 1 Utility (one copy each per section).</summary>
    public static readonly IReadOnlyList<SectionCards> Sections = new List<SectionCards>
    {
        Deck("grave-warden", "grave-spade-strike", "headstone-blow", "vigil-stance", "wall-of-mourners", "in-the-shadow-of-the-fallen"),
        Deck("wasteland-archer", "ash-volley", "loose-from-cover", "fall-back-and-loose", "stakes-in-the-ash", "scavenge-arrows"),
        Deck("shieldwall", "shield-bash", "spear-over-the-rim", "lock-shields", "overlapping-rims", "pass-the-wounded-back"),
        Deck("warband", "ash-rush", "warband-howl", "duck-and-weave", "scatter-and-regroup", "war-paint"),
        Deck("vanguard", "drilled-thrust", "bronze-advance", "hold-the-gap", "bronze-guard", "signal-horn"),
        Deck("blade-dancers", "dance-of-blades", "flourish", "flowing-guard", "veil-of-motion", "weaving-retreat"),
        Deck("longbows", "plunging-shot", "volley-fire", "retire-behind-the-line", "stake-wall", "fletch-in-the-lull"),
        Deck("weaver-circle", "circle-chant", "sustained-note", "woven-ward", "held-chord", "attuning-hum"),
        Deck("musicians", "drums-of-dread", "discordant-blare", "steady-cadence", "counter-melody", "war-drums"),
        Deck("menders", "hurting-arts", "scalpel-cut", "triage-screen", "shelter-the-stretchers", "strand-mending"),
        Deck("crystalwrights", "crystal-spikes", "shard-hail", "raise-a-palisade", "crystal-shelter", "reset-the-stones"),
        Deck("pathfinders", "ambush-from-the-brush", "harry-the-flanks", "vanish-into-the-ground", "known-paths", "read-the-ground"),
        Deck(PartyDeck, "walking-staff", "sling-stone", "circle-the-packs", "stand-firm", "stand-together"),
    };

    private static SectionCards Deck(string section, params string[] cards) => new SectionCards { section = section, cards = cards.ToList() };

    /// <summary>The shape of every basic deck: how many of each Purpose.</summary>
    public static readonly IReadOnlyDictionary<SpellPurpose, int> BasicShape = new Dictionary<SpellPurpose, int>
    {
        [SpellPurpose.Offensive] = 2, [SpellPurpose.Defensive] = 2, [SpellPurpose.Utility] = 1,
    };

    /// <summary>Expedition kits: an expedition's specialty, added to its sections' basic decks (the first needs only expeditions).</summary>
    public static readonly IReadOnlyList<ExpeditionKit> Kits = new List<ExpeditionKit>
    {
        new ExpeditionKit
        {
            id = "wayfarers-kit", name = "Wayfarer's Kit", technology = "Lookout Towers", weight = 0f,
            description = "What every expedition carries without being asked: a way to regroup, an eye for the weak point, and one burst of speed.",
            cards = { "break-and-regroup", "mark-the-weak-point", "quicken-the-tempo" },
        },
        new ExpeditionKit
        {
            id = "hunters-kit", name = "Hunter's Kit", technology = "Trapper's Patience", weight = 1.5f,
            description = "Snares, spears and patience: made for creatures, and best among trees and scrub.",
            cards = { "snare-line", "thrown-spear", "drive-the-quarry" },
        },
        new ExpeditionKit
        {
            id = "wardens-kit", name = "Warden's Kit", technology = "The Rekindling", weight = 2.5f,
            description = "Round shields, spades and a lantern: to hold a camp, a ford or a ruin until help comes.",
            cards = { "brace-shields", "lantern-watch", "grave-spade-strike" },
        },
        new ExpeditionKit
        {
            id = "pathfinders-kit", name = "Pathfinder's Kit", technology = "Old World Roads", weight = 1f,
            description = "Maps of the old roads, rope and a good eye: fight where the ground is on your side.",
            cards = { "read-the-trail", "take-the-high-ground", "hold-the-ford" },
        },
        new ExpeditionKit
        {
            id = "menders-satchel", name = "Mender's Satchel", technology = "Midwives' Lullaby", weight = 1f,
            description = "Cloth, salves and a steady voice: fewer blows, more of the party walks home.",
            cards = { "bind-wounds", "soothing-hum", "carry-the-fallen" },
        },
    };

    /// <summary>The kit a party carries when it has chosen none.</summary>
    public const string DefaultKit = "wayfarers-kit";

    /// <summary>The leitmotif card of a binding ("leitmotif-flux"), or null for Unattuned.</summary>
    public static string LeitmotifId(SpellBinding binding) => binding == SpellBinding.Unattuned ? null : "leitmotif-" + binding.ToString().ToLowerInvariant();

    /// <summary>The Command card of a Great ("great-vanguard").</summary>
    public static string GreatId(LegendClass great) => "great-" + great.ToString().ToLowerInvariant();

    public const string RallyToMe = "rally-to-me";

    // ===== SYMPHONY CARDS WITH NO BATTLE CARD =====

    /// <summary>The Purpose a Spell Maker intent serves.</summary>
    public static SpellPurpose PurposeOf(SpellIntent intent)
    {
        switch (intent)
        {
            case SpellIntent.Strike: return SpellPurpose.Offensive;
            case SpellIntent.Ward: return SpellPurpose.Defensive;
            case SpellIntent.Reveal: return SpellPurpose.Setup;
            default: return SpellPurpose.Utility;
        }
    }

    /// <summary>
    /// A Symphony Card as a battle card: its own when the library has one by its id, otherwise one composed from its
    /// Root the way the Spell Maker would (the first thing its binding can be composed to do: Grimoire.IntentsOf).
    /// </summary>
    public static CombatCard FromSymphonyCard(SymphonyCardData data, SymphonySettings settings = null)
    {
        if (data == null) return null;
        var known = (settings ?? new SymphonySettings()).Card(data.id);
        if (known != null) return known;
        var intent = Grimoire.IntentsOf(data.binding).DefaultIfEmpty(SpellIntent.Strike).First();
        CardEffect effect;
        switch (intent)
        {
            case SpellIntent.Ward: effect = E(CardOp.Ward, CardAim.Allies, 0.12f); break;
            case SpellIntent.Mend: effect = E(CardOp.Mend, CardAim.Ally, 0.1f); break;
            case SpellIntent.Calm: effect = E(CardOp.Rally, CardAim.Allies, 3f); break;
            case SpellIntent.Reveal: effect = E(CardOp.Expose, CardAim.Enemy, 0.2f); break;
            case SpellIntent.Conceal: effect = E(CardOp.Blind, CardAim.Enemy, 0.25f); break;
            default: effect = E(CardOp.Spell, CardAim.Enemy, data.weight == NoteWeight.Major ? 1.1f : 0.8f); break;
        }
        var card = Spell(data.id, data.DisplayName, PurposeOf(intent), data.binding, data.weight, 1, null, data.description, null, data.canonAnchor, effect);
        card.flicker = data.flickerChance;
        card.description = data.battleEffect;
        return card;
    }

    // ===== CREATURES =====

    private static readonly Dictionary<string, List<CombatCard>> CreatureCache = new Dictionary<string, List<CombatCard>>();

    /// <summary>
    /// A creature's basic deck, read from its species, in the same shape as every unit's (2 Offensive, 2 Defensive,
    /// 1 Utility): a blow by its size; a second weapon by what it is (an Atonalis's hunger, its binding cast by instinct,
    /// a hunter's flank, a keeper's challenge, else teeth); a guard by its organ or temper and one by its kind; and what it
    /// does between blows (drinking the wound, calling the pack, scenting the wind, licking its wounds).
    /// </summary>
    public static IReadOnlyList<CombatCard> Creature(SpeciesSpec species, BandIdentity identity = BandIdentity.Creature)
    {
        if (species == null) return Array.Empty<CombatCard>();
        string key = $"{species.id}|{identity}|{species.size}|{species.binding}|{species.primaryBinding}|{species.diet}|{species.subgroup}";
        if (CreatureCache.TryGetValue(key, out var cached)) return cached;
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        var stance = profile?.stance ?? CreatureStance.Neutral;
        var response = profile?.response ?? ThreatResponse.DefendsWhenThreatened;
        bool hunter = response == ThreatResponse.Hunts || response == ThreatResponse.HuntsLoudly || response == ThreatResponse.Lures;
        bool timid = stance == CreatureStance.Passive || response == ThreatResponse.FleesOnSight || response == ThreatResponse.FleesWhenThreatened || response == ThreatResponse.Hides;
        var primary = CreatureCombat.PrimaryOf(species);
        var list = new List<CombatCard>();
        CombatCard I(string id, string name, SpellPurpose purpose, int cost, string line, params CardEffect[] effects) =>
            C($"creature:{species.id}:{id}", name, purpose, CardKind.Instinct, cost, CardVoice.Any, line, effects);

        // Offensive: its blow, by its size.
        switch (species.size)
        {
            case CreatureSize.Small: list.Add(I("swarm", "Swarm", O, 1, "Too many to strike at, all biting at once.", E(CardOp.Strike, CardAim.EnemyLine, 0.9f))); break;
            case CreatureSize.Large: list.Add(I("maul", "Maul", O, 2, "One blow, with all its weight behind it.", E(CardOp.Strike, CardAim.Enemy, 1.5f))); break;
            case CreatureSize.Gargantuan: list.Add(I("trample", "Trample", O, 2, "It walks through the line as if it were not there.", E(CardOp.Strike, CardAim.EnemyLine, 1.3f))); break;
            default: list.Add(I("rend", "Rend", O, 1, "Claws and teeth.", E(CardOp.Strike, CardAim.Enemy, 1f))); break;
        }
        // Offensive: its second weapon, by what it is.
        if (identity == BandIdentity.Atonalis)
            list.Add(I("static-hunger", "Static Hunger", O, 1, "The wanting of something that should not be able to want: it takes the nerve first.", E(CardOp.Dread, CardAim.Enemy, 7f)));
        else if (primary != SpellBinding.Unattuned)
        {
            var spell = I("instinct", $"Instinct of {primary}", O, 1, $"{primary} cast through Coherence-Binding Tissue, without a thought.", E(CardOp.Spell, CardAim.Enemy, 1f));
            spell.binding = primary;
            list.Add(spell);
        }
        else if (hunter) list.Add(I("flank", "Flank the Weak", O, 1, "The pack goes for whoever falters.", E(CardOp.Strike, CardAim.Enemy, 0.8f), E(CardOp.Expose, CardAim.Enemy, 0.15f)));
        else if (stance == CreatureStance.Territorial || stance == CreatureStance.Apex || stance == CreatureStance.Aggressive)
            list.Add(I("challenge", species.binding == BindingOrgan.Voice ? "Keening Song" : "Challenge", O, 1, "It tells you whose ground this is.",
                E(CardOp.Dread, CardAim.Enemy, species.binding == BindingOrgan.Gland || species.binding == BindingOrgan.Voice ? 9f : 6f)));
        else list.Add(I("bite", species.size == CreatureSize.Small ? "Nip" : "Bite", O, 1, "Teeth, where the guard is thin.", E(CardOp.Strike, CardAim.Enemy, 0.8f)));

        // Defensive: its organ's guard, or its temper's.
        switch (species.binding)
        {
            case BindingOrgan.Hide: list.Add(I("harden", "Harden Hide", D, 1, "Armored Coherence-Binding fields turn blade and spell alike.", E(CardOp.Guard, CardAim.Self, 1f), E(CardOp.Ward, CardAim.Self, 0.1f))); break;
            case BindingOrgan.Wings: list.Add(I("take-wing", "Take Wing", D, 1, "Up, out of reach, and down somewhere else.", E(CardOp.Guard, CardAim.Self, 1.1f), E(CardOp.Blind, CardAim.Enemy, 0.1f))); break;
            case BindingOrgan.Fins: list.Add(I("dive", "Dive Deep", D, 1, "The water closes over it.", E(CardOp.Guard, CardAim.Self, 1f)).At(1.4f, BattleGround.Water, BattleGround.Marsh)); break;
            case BindingOrgan.Matrix: list.Add(I("phase", "Phase Shift", D, 1, "The whole body rings a note the blow passes through.", E(CardOp.Ward, CardAim.Self, 0.15f), E(CardOp.Guard, CardAim.Self, 0.5f))); break;
            default:
                list.Add(timid
                    ? I("scatter", "Scatter", D, 1, "It will not stand to be struck.", E(CardOp.Guard, CardAim.Self, 1.2f))
                    : I("hackles", "Hackles Up", D, 1, "Bigger, louder, harder to reach.", E(CardOp.Guard, CardAim.Self, 1f)));
                break;
        }
        // Defensive: how its kind holds together.
        list.Add(species.size == CreatureSize.Small || hunter
            ? I("close-ranks", "Close Ranks", D, 1, "The pack presses together, flank to flank.", E(CardOp.Guard, CardAim.Allies, 0.4f))
            : I("hold-ground", "Hold Ground", D, 1, "It plants itself and will not be moved.", E(CardOp.Guard, CardAim.Self, 0.8f), E(CardOp.Rally, CardAim.Self, 2f)));

        // Utility: what it does between blows.
        if (identity == BandIdentity.FormlessMass)
            list.Add(I("drink", "Drink the Wound", U, 1, "It swallows what the field suffered.", E(CardOp.Mend, CardAim.Self, 0.08f), E(CardOp.Dread, CardAim.Enemy, 3f)));
        else if (species.binding == BindingOrgan.Voice || species.binding == BindingOrgan.Gland)
            list.Add(I("call", "Call to the Pack", U, 1, "A sound the others answer.", E(CardOp.Rally, CardAim.Allies, 3f)));
        else if (hunter)
            list.Add(I("scent", "Scent the Wind", U, 1, "It knows where you are weakest before it sees you.", E(CardOp.Draw, CardAim.Self, 1f), E(CardOp.Expose, CardAim.Enemy, 0.05f)));
        else
            list.Add(I("lick", "Lick Wounds", U, 1, "A moment's retreat to tend a wound.", E(CardOp.Mend, CardAim.Self, 0.06f)));

        CreatureCache[key] = list;
        return list;
    }
}


/// <summary>Card words for the player.</summary>
public static class SymphonyText
{
    public static string Effect(CardEffect e)
    {
        string who = Aim(e.aim);
        switch (e.op)
        {
            case CardOp.Strike: return $"strike {who} (x{e.amount:0.##} attack)";
            case CardOp.Spell: return $"cast at {who} (x{e.amount:0.##} potency)";
            case CardOp.Dread: return $"{e.amount:0.#} Composure harm to {who}";
            case CardOp.Guard: return $"guard {who} (x{e.amount:0.##} defense)";
            case CardOp.Ward: return $"ward {who} ({e.amount:P0} of spells)";
            case CardOp.Mend: return $"mend {who} ({e.amount:P0} Integrity)";
            case CardOp.Rally: return $"+{e.amount:0.#} Composure to {who}";
            case CardOp.CoRegulate: return $"meet {who}'s heartbeat for {Math.Max(2, Math.Min(3, e.durationBeats > 0 ? e.durationBeats : 3))} Beats; +{e.amount:0.#} Battle Composure per Beat";
            case CardOp.Pollute: return $"shuffle {e.amount:0} {e.pollution} Status cards into {who}'s draw pile";
            case CardOp.Purge: return $"purge {(e.pollution == BattlePollution.None ? "all Status Pollution" : e.pollution.ToString())} from {who}'s piles";
            case CardOp.StatusFuel: return $"+{e.amount:P0} output per {e.pollution} held in hand (total fuel cap +200%)";
            case CardOp.Disarm: return $"disarm {who}; halve physical attack and count lost equipment";
            case CardOp.Veil: return $"Veil {e.amount:0} on {who}'s position for {Math.Max(1, e.durationBeats)} Beats";
            case CardOp.PhantomIntent: return $"project an unverified phantom due in {e.amount:0} Beats; Clarity 2 exposes it";
            case CardOp.Draw: return $"draw {e.amount:0}";
            case CardOp.Beat: return $"advance own clocks {e.amount:0} Beat";
            case CardOp.IntegrityDamage: return $"{e.amount:0} Integrity before Guard";
            case CardOp.SpatialChaos: return $"Knock the two nearest allies backward; {e.amount:0} Battle Composure harm to each";
            case CardOp.CatharticBlast: return $"{e.amount:0} Integrity blast across the nearest position and adjacent hexes, including allies";
            case CardOp.ParasiticResonance: return $"{e.amount:0} extraordinary persistent soul strain";
            case CardOp.Delay: return $"delay {e.amount:0} Beat";
            case CardOp.Accelerate: return $"accelerate {e.amount:0} Beat";
            case CardOp.Synchronize: return "synchronize clocks";
            case CardOp.ExtendHold: return $"extend {who}'s spell holding window by {e.amount:0} Beats";
            case CardOp.WeaveCapacity: return $"give {who} room for {e.amount:0} more notes";
            case CardOp.Stabilize: return $"stabilize {who}'s original working ({e.amount:0} Interference)";
            case CardOp.Reposition: return $"reposition {who}";
            case CardOp.Expose: return $"expose {who} (+{e.amount:P0} harm, 2 measures)";
            case CardOp.Blind: return $"blind {who} (-{e.amount:P0} steel, 2 measures)";
            case CardOp.Burn: return $"burn {who} ({e.amount:0.#} a measure, 2 measures)";
            case CardOp.Douse: return $"douse the flames on {who} (+{e.amount:0.#} Composure)";
            case CardOp.Push: return $"throw {who} out of the line this measure";
            case CardOp.Entrench: return $"dig in {e.amount:0} level";
            case CardOp.Sure: return "your missiles are not parried this measure";
            case CardOp.Surge: return $"{who}' steel +{e.amount:P0} this measure";
            case CardOp.Crescendo: return $"{who}' spells +{e.amount:P0} this measure";
            case CardOp.Recoil: return $"costs its voice {e.amount:P0} of its Integrity";
            default: return e.op.ToString();
        }
    }

    public static string Modifier(BattleChordModifier mod)
    {
        if (mod == null) return string.Empty;
        var words = new List<string>();
        if (mod.power != 1f) words.Add($"Core power x{mod.power:0.##}");
        if (mod.stability > 0f) words.Add("stabilize the Core");
        if (mod.repeats > 1) words.Add($"repeat momentum x{mod.repeats}");
        if (mod.holdBeats > 0) words.Add($"+{mod.holdBeats} Beats of holding");
        if (mod.capacity > 0) words.Add($"room for {mod.capacity} more notes");
        if (mod.windUp != 0) words.Add($"Wind-Up {mod.windUp:+0;-0} Beats");
        if (mod.durationBeats > 0) words.Add($"extend duration {mod.durationBeats} Beats");
        if (mod.range > 0) words.Add($"reach {mod.range} hexes");
        if (mod.changeTargeting) words.Add($"target {mod.targeting}");
        if (mod.area) words.Add("expand the Core's area");
        if (mod.penetrate) words.Add("penetrate Guard");
        if (mod.advance) words.Add("advance with the Core");
        if (mod.synchronize) words.Add("synchronize contributing movement");
        if (mod.replaceRoot) words.Add($"change the Core's Root to {mod.binding}");
        else if (mod.joinBinding) words.Add($"join {mod.binding} inside the working");
        words.AddRange(mod.effects.Where(e => e != null).Select(Effect));
        return words.Count == 0 ? "ornament the Core" : string.Join("; ", words);
    }

    public static string Aim(CardAim aim)
    {
        switch (aim)
        {
            case CardAim.Enemy: return "an enemy";
            case CardAim.EnemyLine: return "the enemy line";
            case CardAim.Self: return "itself";
            case CardAim.Ally: return "an ally";
            default: return "every ally";
        }
    }
}
