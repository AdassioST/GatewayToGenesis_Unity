using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The Symphony of War's card layer (vault: Combat System.md, "Micro Level | Combat as Performance": "The attacks are
// dictated by Symphony Cards which consist of actions such as combat attacks with traditional swords, Magic Arts for
// imbuing offensive, defensive, and utility magic"). A side performs a deck (its Symphony) every measure of a battle;
// the auto-resolve plays it with a performer of its own and a manual battle lets the player play it
// (BattleResolver.Begin). Actions consume time on the shared Beat timeline, with preparation persisting across Measures.
// Card magnitudes are authored balance values; the three-Beat unmodified channeling minimum comes from the design.

/// <summary>What a card is. Saved by index: append only.</summary>
public enum CardKind
{
    /// <summary>Steel: a blow, a volley, a charge.</summary>
    Attack,
    /// <summary>A drilled action with no magic: a guard, a feint, reading the ground, binding a wound.</summary>
    Skill,
    /// <summary>A Symphony Card proper, a song of Spellweaving (Root, Harmony, Tempo): paid from Composure, bound by the Age.</summary>
    Spell,
    /// <summary>An order from the Battle Conductor (a legend's Greats), heard by the whole formation.</summary>
    Command,
    /// <summary>A creature's instinct: teeth, hide, a roar, magic cast through Coherence-Binding Tissue.</summary>
    Instinct,
}

/// <summary>
/// What a card is for (the Spell Builder's five Purposes, Sonata Website src/content/chords.ts): classified by what the
/// weave does on the Loom rather than by school or element. Offensive, Defensive and Utility make an effect; Setup
/// prepares the ground for one; Modulation acts on one already sounding. Each is traditionally attuned to one thread of
/// the Pentatonic circle (<see cref="CardFace.PurposeBinding"/>). Saved by index: append only.
/// </summary>
public enum SpellPurpose { Offensive, Defensive, Setup, Utility, Modulation }

/// <summary>What one effect of a card does. Saved by index: append only.</summary>
public enum CardOp
{
    /// <summary>Extra steel: <c>amount</c> times the voice's attack, this measure.</summary>
    Strike,
    /// <summary>A spell of the card's binding: <c>amount</c> times the voice's potency, through the Elemental Harmonic Circle.</summary>
    Spell,
    /// <summary>Composure harm, flat (a roar, a sentence, the Void's cost felt by the enemy).</summary>
    Dread,
    /// <summary>Timed extra parries: amount times defense, or fixed protection when flat is set.</summary>
    Guard,
    /// <summary>Timed share of spell harm turned aside.</summary>
    Ward,
    /// <summary>Integrity restored: a share of the target's maximum (never more than it lost).</summary>
    Mend,
    /// <summary>Composure restored, flat.</summary>
    Rally,
    /// <summary>Cards drawn when this action resolves.</summary>
    Draw,
    /// <summary>Legacy saved operation: advances eligible allied countdowns, without granting energy.</summary>
    Beat,
    /// <summary>The target takes this share more Integrity harm this measure and the next.</summary>
    Expose,
    /// <summary>The target's steel lands this share less this measure and the next.</summary>
    Blind,
    /// <summary>Integrity lost each measure for the next two (Cindergale's flame; no armor stops it).</summary>
    Burn,
    /// <summary>Puts out every burn on the target side and cools it (Composure, flat).</summary>
    Douse,
    /// <summary>Displaces a reachable enemy one hex toward home; fallback can stack, rout or become trapped.</summary>
    Push,
    /// <summary>Entrenchment levels dug in at once (defenders only).</summary>
    Entrench,
    /// <summary>This measure the side's missiles are not parried ("an arrow guided a hair's breadth truer").</summary>
    Sure,
    /// <summary>Every allied section's steel lands this share harder this measure.</summary>
    Surge,
    /// <summary>Every allied caster's spells land this share harder this measure.</summary>
    Crescendo,
    /// <summary>The voice pays for it in its own body: this share of its maximum Integrity (Struggle).</summary>
    Recoil,
    /// <summary>Fixed physical Integrity output; eligible Guard subtracts from it before exposure.</summary>
    IntegrityDamage,
    /// <summary>Ritardando / Disruption: the target's unsounded Notes fall this many Beats later (past its Hold, it collapses).</summary>
    Delay,
    /// <summary>Accelerando: an allied working's Major is pulled this many Beats earlier; Minors not yet sounded are left unresolved.</summary>
    Accelerate,
    /// <summary>Resonance: aligns allied pending Majors on the latest of their Beats.</summary>
    Synchronize,
    /// <summary>Fermata modulation on an allied working: +amount Hold, at most +2 a card.</summary>
    ExtendHold,
    /// <summary>Legacy saved operation (finite complexity is now the Hold Limit): +1 Hold, like a Fermata.</summary>
    WeaveCapacity,
    /// <summary>Restores coherent prerequisites of an allied working; each Stabilization adds one Interference.</summary>
    Stabilize,
    Reposition,
    /// <summary>Break Focus: amount Interference on the target's sounding workings.</summary>
    Interrupt,
    /// <summary>Exceptional movement (a Major): breaks engagement and falls back up to two hexes without a forced retreat's Composure loss.</summary>
    Withdraw,
    /// <summary>Exceptional movement (a Major): Pace doubled, up to two Steps a Beat, and no other Notes on the Track.</summary>
    Rush,
    /// <summary>Exceptional movement (a Major): the formation takes the Stance numbered by amount (0 Line, 1 Spearhead, 2 Crescent).</summary>
    Reform,
    /// <summary>Hostile Dissonance: the target's Tuning falls one step for the rest of the Measure.</summary>
    Detune,
    /// <summary>A harmonic doctrine or card of the Conductor: the target's Tuning rises one step for the rest of the Measure.</summary>
    Attune,
    /// <summary>Setup: a Resonance Anchor laid in the target's hex (+1 Hold to workings there, and a harmonic source).</summary>
    Anchor,
    /// <summary>Meets an ally's heartbeat over two or three Beats on the helper's Track; amount Battle Composure per Beat.</summary>
    CoRegulate,
    SpatialChaos,
    CatharticBlast,
    ParasiticResonance,
    Pollute,
    Purge,
    StatusFuel,
    Disarm,
    Veil,
    PhantomIntent,
    /// <summary>A compulsion of possession: the nearest allies are pulled toward the performer, stacking where they land; amount Battle Composure each.</summary>
    Gather,
}

/// <summary>Whom an effect falls on. Saved by index: append only.</summary>
public enum CardAim
{
    /// <summary>One enemy section, chosen by the performer (a finishing blow, the weakest nerve, the element it fears).</summary>
    Enemy,
    /// <summary>Every enemy eligible under the effect's hex reach and Rank Proximity; steel spreads by frontage.</summary>
    EnemyLine,
    /// <summary>The section that plays it.</summary>
    Self,
    /// <summary>One allied section, chosen by the performer (the most wounded, the shakiest).</summary>
    Ally,
    /// <summary>Every allied section within support access (Command orders can broadcast across the formation).</summary>
    Allies,
}

/// <summary>Who can voice a card. Saved by index: append only.</summary>
public enum CardVoice { Any, Front, Back, Caster, Commander }

/// <summary>Where and when a card is at home: the field it favours or needs. Flags; saved by value.</summary>
[Flags]
public enum CardCondition
{
    None = 0,
    /// <summary>The side fights from cover (the defender's hex is concealed).</summary>
    Concealed = 1,
    /// <summary>The side stands higher: the defender up a slope, or the attacker coming downhill.</summary>
    HighGround = 2,
    /// <summary>The attacker crosses a river to reach the defender (the side is the defender).</summary>
    RiverCrossing = 4,
    /// <summary>The side holds a settlement.</summary>
    Settlement = 8,
    Defending = 16,
    Attacking = 32,
    /// <summary>The first measure (a charge, an ambush's first volley).</summary>
    Opening = 64,
    /// <summary>Against wild creatures (a hunt).</summary>
    Hunting = 128,
    /// <summary>The land sings the card's own binding.</summary>
    LandElement = 256,
    /// <summary>Sacred ground.</summary>
    Sacred = 512,
    /// <summary>A leyline runs beneath the field.</summary>
    Leyline = 1024,
}

/// <summary>One effect of a card.</summary>
[Serializable]
public class CardEffect
{
    public BattlePollution pollution;
    public BattleProximity proximity;
    [Tooltip("Hex reach; 0 uses the spatial defaults for this action.")]
    public int range;
    public bool ignoreGuard;
    public bool ignoreArmor;
    [Tooltip("Guard, Ward and Dread use the authored amount without melody normalization.")]
    public bool flat;
    [Tooltip("Guard/ward lifetime in Beats; 0 uses the default Measure cadence.")]
    public int durationBeats;
    public int destinationHex = -1;
    public CardOp op;
    public CardAim aim;
    [Tooltip("How much (see CardOp: a multiple of the voice's attack, potency or defense; a share; or flat points).")]
    public float amount = 1f;

    public CardEffect() { }
    public CardEffect(CardOp op, CardAim aim, float amount) { this.op = op; this.aim = aim; this.amount = amount; }
    public CardEffect Clone() => (CardEffect)MemberwiseClone();
}

/// <summary>
/// One card of a Symphony. Its effects scale with the section that voices it (a Grave Warden's blow is a Grave Warden's
/// attack), so the same card is stronger in a stronger hand; its cost is paid in Beats. A card voiced by a section that
/// has fallen, fled or been taken is dead in the hand; a spell voiced by a Mind Broken section cannot be cast.
/// </summary>
[Serializable]
public class CombatCard
{
    public BattlePollution pollution, pollutionAfter;
    public float alteredBelowComposure, alteredPower;
    public bool corrupted, cathartic;
    [Tooltip("Stable id (kebab case). Symphony Cards keep their SymphonyCardData id.")]
    public string id;
    public string name;
    public CardKind kind;
    [Tooltip("What it is for: Offensive, Defensive, Setup, Utility or Modulation (the Spell Builder's five). A unit's basic deck is 2 Offensive, 2 Defensive, 1 Utility.")]
    public SpellPurpose purpose;
    [Tooltip("The line its face prints (the Spell Builder's catchline), bounded by what the face can hold.")]
    [TextArea(1, 2)] public string catchline;
    [Tooltip("The practice inside the Root's tessitura, as The Registers of Magic names it (\"Combustion Arts\"); empty for steel, orders and instincts.")]
    public string practice;
    [Tooltip("Spells: what the weaver has to hold in the mind's eye for this to exist (Absolute Certainty, in words).")]
    [TextArea(1, 2)] public string vision;
    [Tooltip("Spells: Minor Notes layered over the Root (0-3: Unison, Dyad, Triad, Tetrad). The Age decides whether the chord can be played.")]
    public List<SpellBinding> minors = new List<SpellBinding>();
    [Tooltip("Legacy cost: the Beats a Major channels when channel is 0 (1-3).")]
    [Range(0, 3)] public int cost = 1;
    [Tooltip("Legacy temporal cost; read like cost when channel is 0.")]
    public int actionBeats;
    [Tooltip("Legacy preparation Countdown from the action-clock prototype; the four-Beat Measure ignores it.")]
    public int windUp;
    [Tooltip("Channeled: a Legato working that must sound at least three Beats to anchor.")]
    public bool channeling;
    public bool weaving;
    [Tooltip("Rhythm: Inherit reads Legato from channeling, the formation's tempo for spells, Staccato for steel. Only a Staccato Major may be syncopated onto Beats 1-3.")]
    public CardTempo tempo;
    [Tooltip("How its performer may move while it sounds (the strictest Note of a Chord rules; every Tetrad is at least Braced).")]
    public BattleFooting footing;
    [Tooltip("Beats a Major sounds before it resolves; 0 reads the legacy cost. Legato and channeled workings sound at least three.")]
    [Range(0, 4)] public int channel;
    [Tooltip("A Hyper Chord: the Measures its repeated Tetrad spans before it releases (0 or 1: an ordinary Chord).")]
    public int hyperMeasures;
    [Tooltip("Workings too absolute to answer with anything else: the only Ward card ids that can cover them (empty: any Ward of the right frequency).")]
    public List<string> abjuredOnlyBy = new List<string>();
    [Tooltip("Minimum Beats a matching Ward must already have stood before this release can be answered.")]
    public int minimumWardBeats;
    public BattleNoteRole noteRole;
    public BattleChordModifier chord;
    public BattleReactionSpec reaction;
    [Tooltip("Legacy holding window from the action-clock prototype; the Hold Limit (base 4) replaces it.")]
    public int holdBeats, weaveCapacity;
    public int requiredVoices;
    public float requiredBond;
    /// <summary>Beats the card's Major sounds on its Track (1-4), before Legato anchoring is applied.</summary>
    public int ChannelBeats => effects.Any(e => e?.op == CardOp.CoRegulate)
        ? Math.Max(2, Math.Min(3, effects.First(e => e?.op == CardOp.CoRegulate).durationBeats > 0 ? effects.First(e => e?.op == CardOp.CoRegulate).durationBeats : 3))
        : Math.Max(1, Math.Min(BattleMeasureMath.Beats, channel > 0 ? channel : channeling ? 3 : actionBeats > 0 ? actionBeats : cost));
    /// <summary>Kept for the card face: the Beats it occupies.</summary>
    public int TemporalBeats => ChannelBeats;
    public CardVoice voice;
    [Tooltip("Spells and instincts: the Root, the binding struck as the spell's primary note.")]
    public SpellBinding binding;
    public NoteWeight weight;
    [Tooltip("Chance a cast flickers into Discordant Interference, before the Age's own (negative: the Age decides). Proposal: Minor 0.15, Major 0.35.")]
    public float flicker = -1f;
    public List<CardEffect> effects = new List<CardEffect>();
    [Tooltip("Removed from the Symphony for the rest of the battle once played.")]
    public bool exhaust;
    [Tooltip("Preparation fails if its voice leaves the hex where it was committed.")]
    public bool requiresPosition;
    [Tooltip("A live harmonic channel must remain at the committed voice's hex.")]
    public bool requiresChannel;

    [Header("The field")]
    [Tooltip("Grounds it is at home on: its effects are multiplied by groundBonus there (the side's own footing).")]
    public List<BattleGround> grounds = new List<BattleGround>();
    public float groundBonus = 1.25f;
    [Tooltip("When it is at home: its effects are multiplied by conditionBonus when every flag holds.")]
    public CardCondition condition;
    public float conditionBonus = 1.3f;
    [Tooltip("Only playable when the condition holds (and on one of its grounds, when it lists any).")]
    public bool requires;

    [Tooltip("The whole of it, which only the card's back has room for (the face prints the catchline).")]
    [TextArea(1, 3)] public string description;
    [Tooltip("The vault note or line it is drawn from (empty: a proposal with no anchor).")]
    [TextArea(1, 2)] public string canonAnchor;

    public bool IsSpell => effects.Any(e => e != null && e.op == CardOp.Spell);

    public CombatCard Clone()
    {
        var c = (CombatCard)MemberwiseClone();
        c.effects = effects.Select(e => e?.Clone()).ToList();
        c.chord = chord?.Clone(); c.reaction = reaction?.Clone();
        c.grounds = new List<BattleGround>(grounds);
        c.minors = new List<SpellBinding>(minors ?? new List<SpellBinding>());
        c.abjuredOnlyBy = new List<string>(abjuredOnlyBy ?? new List<string>());
        return c;
    }

    /// <summary>"1 · Attack · Strike an enemy (x1.2)".</summary>
    public string Summary => $"{TemporalBeats} · {CardFace.Rank(this, false)} · {purpose} · {(noteRole == BattleNoteRole.Minor ? SymphonyText.Modifier(chord) : string.Join("; ", effects.Where(e => e != null).Select(SymphonyText.Effect)))}";
}

/// <summary>
/// One card in a side's Symphony: the card, the section that voices it (its index in <see cref="BattleSide.sections"/>;
/// -1 the Battle Conductor), and the legend it belongs to when it comes from a personal grimoire.
/// </summary>
public sealed class DeckCard
{
    public CombatCard card;
    public int voice;
    public BattleLegend legend;
    /// <summary>Its effects are multiplied by this (a Great's stars, a legend's Major Note).</summary>
    public float scale = 1f;
    /// <summary>A spell sounded as a Major Note (an Awakened Legend's own leitmotif), whatever the card says.</summary>
    public bool major;
    /// <summary>Where it came from ("Grave Warden", "Wayfarer's Kit", "Aurelian's grimoire").</summary>
    public string source;

    public NoteWeight Weight => major ? NoteWeight.Major : card?.weight ?? NoteWeight.Minor;
    public string Name => card?.name ?? card?.id ?? "?";
}

/// <summary>A kit an expedition carries into the field: the cards a party of legends and porters can fight with.</summary>
[Serializable]
public class ExpeditionKit
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("A technology it needs (empty: none).")]
    public string technology;
    [Tooltip("Card ids it gives the party (each voiced by one of its sections in turn).")]
    public List<string> cards = new List<string>();
    [Tooltip("Carried weight it adds to the party (Expeditions' load).")]
    public float weight;
}

/// <summary>The cards one kind of section brings to its side's Symphony (its basic deck).</summary>
[Serializable]
public class SectionCards
{
    public string section;
    public List<string> cards = new List<string>();
}

/// <summary>How a Symphony is performed. Every number is a proposal (roadmap D10).</summary>
[Serializable]
public class SymphonyTuning
{
    public static readonly SymphonyTuning Default = new SymphonyTuning();

    [Header("The ostinato and the melody")]
    [Tooltip("With a Symphony, the share of a formation's drilled fighting (its sections' own attack and spells every measure) that goes on without cards; the cards carry the rest. A side with no deck fights at 1 (the plain auto-resolve).")]
    [Range(0.3f, 1f)] public float ostinato = 0.7f;
    [Tooltip("Legacy melody normalization for proportional card effects. Fixed output bypasses it.")]
    public float melody = 0.35f;
    [Tooltip("Legacy saved names: determine hand capacity and proportional effect normalization; these never limit actions or give energy.")]
    public float beatsBase = 1f, beatsPerVoice = 0.5f;
    public int minBeats = 1, maxBeats = 7;
    [Tooltip("Cards drawn each Measure beyond the calculated hand capacity.")]
    public int handExtra = 2;
    [Tooltip("Extra hand capacity for Sovereign stars (2 or more), and an extra drawn card for Seer stars (1 or more). Saved field names are retained.")]
    public int sovereignBeat = 1, seerDraw = 1;

    [Header("Voices")]
    [Tooltip("Potency of a Minor Unison voiced by someone who is not a caster (anyone can sound one in Age 0); a Major Note lands majorPotency times harder.")]
    public float humPotency = 6f, majorPotency = 1.5f;
    [Tooltip("Flicker of a Unison card when the card sets none: Minor and Major (Docs/Planning/TECH_TREE_ACT_I.md 4.2).")]
    public float minorFlicker = 0.15f, majorFlicker = 0.35f;
    [Tooltip("Spell cards of different voices in one measure layer into one chord, the earlier ones' Roots becoming the later one's Minor Notes (Chord Layering, as the Ceremony's three voices share one Triad), up to the largest chord the Age plays.")]
    public bool ensembleLayering = true;
    [Tooltip("Each star a Great holds beyond the first adds this to its Command card.")]
    public float perStar = 0.25f;

    [Header("Purposes")]
    [Tooltip("Groundwork: each Setup card played earlier in the measure makes the side's Offensive cards land this much harder, up to groundworkCap (the Spell Builder: \"the duel is four spells where three are groundwork, and the attack is cheap because of them\").")]
    public float groundwork = 0.15f;
    public int groundworkCap = 3;
    [Tooltip("The attack an Offensive steel card strikes with when its voice has less (musicians, menders, a settler with a stick).")]
    public float handAttack = 4f;

    [Header("Rendition (the rhythm layer)")]
    [Tooltip("How well the auto-resolve plays each card: 1 is clean. A manual battle passes the player's rhythm instead: repeating the melody back grows the Chord Layering stack (up to perfect), and the opposite melody cancels noise on defense (vault: Combat System.md).")]
    public float autoRendition = 1f;
    public float perfect = 1.25f, missed = 0.6f;

    [Header("How the performer values the field")]
    [Tooltip("Measures of steel and spells an Expose or a Burn is counted for.")]
    public int statusMeasures = 2;
}

/// <summary>The card layer's content in World.asset (empty lists: <see cref="SymphonyCards"/>' defaults).</summary>
[Serializable]
public class SymphonySettings
{
    public SymphonyTuning tuning = new SymphonyTuning();
    [Tooltip("Every card (empty: SymphonyCards.Cards).")]
    public List<CombatCard> cards = new List<CombatCard>();
    [Tooltip("The basic deck of each section kind (empty: SymphonyCards.Sections).")]
    public List<SectionCards> sections = new List<SectionCards>();
    [Tooltip("Expedition kits (empty: SymphonyCards.Kits).")]
    public List<ExpeditionKit> kits = new List<ExpeditionKit>();

    public SymphonyTuning Tuning => tuning ?? SymphonyTuning.Default;
    public IReadOnlyList<CombatCard> Cards => cards != null && cards.Count > 0 ? cards : SymphonyCards.Cards;
    public IReadOnlyList<SectionCards> Sections => sections != null && sections.Count > 0 ? sections : SymphonyCards.Sections;
    public IReadOnlyList<ExpeditionKit> Kits => kits != null && kits.Count > 0 ? kits : SymphonyCards.Kits;

    public CombatCard Card(string id) =>
        string.IsNullOrEmpty(id) ? null : Cards.FirstOrDefault(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));

    public ExpeditionKit Kit(string id) =>
        string.IsNullOrEmpty(id) ? null : Kits.FirstOrDefault(k => k != null && string.Equals(k.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The basic deck a section kind brings (empty for kinds that list none).</summary>
    public IEnumerable<CombatCard> SectionDeck(string specId) =>
        (Sections.FirstOrDefault(s => s != null && string.Equals(s.section, specId, StringComparison.OrdinalIgnoreCase))?.cards ?? new List<string>())
        .Select(Card).Where(c => c != null);
}
