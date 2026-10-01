using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The Symphony of War's macro layer, "Strategy as Composition" (vault: Combat System.md): Orchestral Formations built
// from sections, a Legend as Battle Conductor, and battles that resolve on their own. Every number here is a proposal
// (roadmap D10: combat budgets are the owner's to approve); the names the vault gives are kept.

/// <summary>Where a section stands in an Orchestral Formation (the war deck's "two lanes", Amadea.md, plus support).</summary>
public enum FormationRow { Front, Back, Support }

/// <summary>What a section does in battle.</summary>
public enum SectionKind
{
    /// <summary>Holds the line with steel.</summary>
    Infantry,
    /// <summary>Hits hard on the first measure (a charge) and runs down the routed.</summary>
    Shock,
    /// <summary>Shoots from the back lane.</summary>
    Ranged,
    /// <summary>Casts chords from the back lane (Signal Loss applies: the vault's 3 m).</summary>
    Spellweaver,
    /// <summary>The melee archetype of Spellweaving (vault: Dance.md): casts in the front lane, needs accompaniment.</summary>
    Dancer,
    /// <summary>Musicians, menders, crystalwrights, pathfinders: never on the line.</summary>
    Support,
    /// <summary>A creature group (built from its species, <see cref="CreatureCombat"/>).</summary>
    Creature,
}

/// <summary>The kind of ground a battle is fought on (terrain ids map to one in <see cref="CombatSettings.grounds"/>). Append only.</summary>
public enum BattleGround { Open, Rough, Forest, Marsh, Hills, Mountains, Ruins, Waste, Water }

/// <summary>How a section fights on one kind of ground (HOI4's per-battalion terrain modifiers).</summary>
[Serializable]
public class GroundModifier
{
    public BattleGround ground;
    [Tooltip("Attack multiplier here.")]
    public float attack = 1f;
    [Tooltip("Defense and breakthrough multiplier here.")]
    public float defense = 1f;
}

/// <summary>
/// One kind of section an Orchestral Formation is built from (HOI4's battalion or support company; Total War's unit).
/// Numbers are per full-strength section and are proposals.
/// </summary>
[Serializable]
public class CombatSectionSpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    public FormationRow row;
    public SectionKind kind;
    [Tooltip("The first Age number it can be raised in.")]
    public int minAge;
    [Tooltip("A technology it needs (empty: none).")]
    public string technology;
    [Tooltip("Production unit that manufactures this company's standard equipment; empty: no factory requirement.")]
    public string productionUnit;
    public int productionTier = 1;
    public List<string> trainingCards = new List<string>();

    [Header("The two bars")]
    [Tooltip("Integrity: the physical bar, bodies, blood and armor. Steel and the body-harm of spells wear it down; at 0 the section is cut down.")]
    public float integrity = 100f;
    [Tooltip("Composure: the mental bar and the magical one (HOI4's organization, Total War's morale, a caster's reserve). Spells, dread and losses wear it down, and every spell is paid from it. At 0 the section is Mind Broken: it fights on, badly, and cannot cast.")]
    public float composure = 40f;
    [Tooltip("Auric Structure share (0-1): physical resilience; the rest is Pure Light, which spells land harder on (vault: Pure Light.md; Humanity is 75%).")]
    [Range(0f, 1f)] public float structure = 0.75f;

    [Header("Steel")]
    [Tooltip("Attacks it makes per measure (melee in the front lane, missiles in the back).")]
    public float attack;
    [Tooltip("Attacks it can parry per measure while its side defends.")]
    public float defense;
    [Tooltip("Attacks it can parry per measure while its side attacks.")]
    public float breakthrough;
    [Tooltip("Armor: attacks whose piercing is below it do half their harm.")]
    public float armor;
    public float piercing;
    [Tooltip("Frontage it takes on the line (combat width).")]
    public float width = 1f;
    [Tooltip("Pace: who runs down whom when a side routs.")]
    public float speed = 3f;
    [Tooltip("Attack multiplier on the first measure (a charge; open ground only).")]
    public float charge = 1f;

    [Header("Spellweaving")]
    [Tooltip("Spell potency per measure at full Integrity (0: casts nothing).")]
    public float potency;
    [Tooltip("Share of spell harm turned aside (0-1): wards, crystal shelters, armored Coherence-Binding fields.")]
    [Range(0f, 0.9f)] public float ward;
    [Tooltip("Dancers: spells fall to 60% without Musicians in the formation (vault: Dance.md needs a Primary Instrument or the dancer's Own Voice).")]
    public bool needsAccompaniment;

    [Header("Support (the side, while the section stands)")]
    [Tooltip("Scouting: initiative and ambushes.")]
    public float recon;
    [Tooltip("Spell potency added to every caster of the side (Music as Catalyst), e.g. 0.15.")]
    public float catalyst;
    [Tooltip("Composure every section of the side regains per measure.")]
    public float rally;
    [Tooltip("Share of the Integrity lost this measure that Strand mending restores.")]
    public float mending;
    [Tooltip("Share of the dead turned into wounded who recover after the battle.")]
    public float wounded;
    [Tooltip("Entrenchment levels given to the side while it defends.")]
    public float entrench;

    public List<GroundModifier> grounds = new List<GroundModifier>();

    [Header("Conscription (ArmyRoster)")]
    [Tooltip("Raised from the population as a nameless company (a basic unit), rather than only placed in templates.")]
    public bool conscripted;
    [Tooltip("People drawn from the population to raise one company; they return when it is disbanded, less the fallen.")]
    public int people;
    [Tooltip("What raising one company costs.")]
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    [Tooltip("The Section (UI group) the unit is listed under, e.g. Golden Hymn Citadel.")]
    public string category;
    [Tooltip("The unit's line on its card.")]
    public string quote;

    public GroundModifier On(BattleGround ground) => grounds?.FirstOrDefault(g => g != null && g.ground == ground);
    public bool Casts => potency > 0f;
}

/// <summary>One section placed in a template: which kind, and for casters the chord it plays.</summary>
[Serializable]
public class FormationSlot
{
    public string section;
    public BattleDeploymentSlot deployment;
    [Tooltip("Casters: the binding their chords are rooted in, their primary binding (and so their weakness).")]
    public SpellBinding binding;
    [Tooltip("Casters: Minor Notes layered over the root (0-3: Unison, Dyad, Triad, Tetrad). The Age decides which chords can be played.")]
    public List<SpellBinding> harmony = new List<SpellBinding>();
}

/// <summary>
/// An Orchestral Formation's template (vault: Combat System.md; HOI4's division template). Two lanes and a support
/// staff, played in one tempo. Front sections beyond the battlefield's width wait in reserve and step in as the line
/// thins.
/// </summary>
[Serializable]
public class FormationTemplate
{
    public BattleStance stance = BattleStance.Line;
    public string doctrine;
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    public SpellTempo tempo = SpellTempo.Staccato;
    public List<FormationSlot> front = new List<FormationSlot>();
    public List<FormationSlot> back = new List<FormationSlot>();
    public List<FormationSlot> support = new List<FormationSlot>();

    public IEnumerable<FormationSlot> Slots => front.Concat(back).Concat(support);
}

/// <summary>The kind of ground each terrain is, with its combat width and what it does to a battle.</summary>
[Serializable]
public class GroundSpec
{
    public BattleGround ground;
    public string name;
    public List<string> terrains = new List<string>();
    [Tooltip("Front sections each side can put on the line (combat width).")]
    public float width = 5f;
    [Tooltip("The defender's defense multiplier.")]
    public float defense = 1f;
    [Tooltip("The attacker's attack multiplier.")]
    public float assault = 1f;
    [Tooltip("Missile attacks multiplier (trees and walls stop arrows).")]
    public float missiles = 1f;
    [Tooltip("Whether a first-measure charge works here.")]
    public bool charges = true;
}

/// <summary>A terrain whose land sings one element: spells rooted in it land harder there.</summary>
[Serializable]
public class TerrainElement
{
    public string terrain;
    public SpellBinding binding;
}

/// <summary>
/// The numbers of the Symphony of War's auto-resolve. All proposals (roadmap D10). Rates are per measure (one round of
/// the battle).
/// </summary>
[Serializable]
public class CombatTuning
{
    public static readonly CombatTuning Default = new CombatTuning();

    [Header("The Elemental Harmonic Circle (Combat System.md multipliers)")]
    public float overcomes = 1.5f;
    public float resisted = 0.5f;
    [Tooltip("Luminance against Void and Void against Luminance: each effective against the other.")]
    public float dance = 1.5f;
    [Tooltip("A spell rooted in a secondary binding (an Ornament, a creature's other attunement) against one in the primary.")]
    public float secondaryPotency = 0.75f;
    [Tooltip("Spells rooted in the element the land sings (TerrainElement).")]
    public float landElement = 1.15f;

    [Header("Measures")]
    [Tooltip("A battle still undecided after this many measures is a stalemate: the defender holds (Total War's timer).")]
    public int maxMeasures = 16;
    [Tooltip("HOI4: attacks a target parries hit this often; the rest hit at undefendedHit.")]
    public float defendedHit = 0.12f;
    public float undefendedHit = 0.36f;
    [Tooltip("Steel: Integrity lost per hit, and the Composure the blow costs besides (fear, pain, the fallen beside you).")]
    public float integrityPerHit = 3f;
    public float composurePerHit = 1.5f;
    [Tooltip("Harm left when the attack's piercing is below the target's armor.")]
    public float armorBlock = 0.5f;
    [Tooltip("Randomness of each section's output per measure (0.2: 80%-120%).")]
    public float variance = 0.2f;
    [Tooltip("Share of a section's attacks that fall on one chosen enemy section; the rest spread along the line by frontage.")]
    [Range(0f, 1f)] public float focus = 0.5f;

    [Header("Spells")]
    [Tooltip("Hits per point of potency (spells cannot be parried, only warded).")]
    public float spellHit = 0.3f;
    [Tooltip("A spell hit's harm to Integrity and to Composure: magic strikes the mind harder than the body.")]
    public float integrityPerSpellHit = 1.6f, composurePerSpellHit = 3f;
    [Tooltip("Spell harm taken at 100% Auric Structure and at 100% Pure Light (glass cannons, Pure Light.md).")]
    public float structureSpellTaken = 0.8f, pureLightSpellTaken = 1.3f;
    [Tooltip("Signal Loss: potency lost by a caster in the back lane (vault: dampening past 3 m).")]
    public float signalLoss = 0.35f;
    [Tooltip("Signal Loss kept with each Minor Note: Resonance (the carrier wave), Flux (a trail of Stable Harmonic Channels), Void (bent space).")]
    public float resonanceChannel = 0.5f, fluxChannel = 0.6f, voidChannel = 0.6f;
    [Tooltip("Interference kept with a Luminance (Sufficient Precision) or Crystal (Absolute Certainty) Minor Note.")]
    public float precisionNote = 0.7f;
    [Tooltip("Ward a Crystal Minor Note raises over its own section.")]
    public float crystalWard = 0.05f;
    [Tooltip("Potency with a Cindergale Minor Note while the caster keeps flow (Composure above focusFrom).")]
    public float focusNote = 1.15f;
    [Range(0f, 1f)] public float focusFrom = 0.6f;
    [Tooltip("Share of the caster's lost Integrity a Strand Minor Note mends each cast.")]
    public float strandMending = 0.03f;
    [Tooltip("Potency multiplier per chord tier (Unison, Dyad, Triad, Tetrad).")]
    public float[] tierPotency = { 1f, 1.3f, 1.65f, 2.1f };
    [Tooltip("Composure a caster pays per cast by tier (Essence Sacrifice: fatigue for a Unison, far more for a Tetrad).")]
    public float[] essenceCost = { 0.4f, 0.9f, 1.8f, 3.6f };
    [Tooltip("Before Age I magic is \"mostly Minor Note\": spells land at this share.")]
    public float minorNoteMagic = 0.55f;
    [Tooltip("Discordant Interference chance by how the Age commands the chord (AgeMagic, words from Ages.md).")]
    public float unreliable = 0.35f, sparse = 0.25f, semiStable = 0.18f, stable = 0.08f, reliable = 0.07f, advanced = 0.05f, primacy = 0.04f, mastery = 0.02f;
    [Tooltip("Interference chance of a creature's instinctive casting (its tissue is phase-locked to the Loom).")]
    public float instinctInterference = 0.04f;
    [Tooltip("Interference added per point of the field's Dissonance.")]
    public float dissonanceInterference = 0.15f;
    [Tooltip("A misfire still lands this share, and costs the caster this much Composure per point of potency.")]
    public float misfireLands = 0.4f, backlash = 0.3f;

    [Header("Tempo (the formation's one tempo)")]
    public float staccatoOpen = 1.4f, staccatoStep = 0.15f, staccatoFloor = 0.85f;
    public float legato = 0.95f, legatoWard = 0.08f;
    public float accelerandoOpen = 0.7f, accelerandoStep = 0.1f, accelerandoCap = 1.6f;
    public float ritardandoOpen = 1.25f, ritardandoStep = 0.05f, ritardandoFloor = 0.8f, ritardandoInterference = 0.75f;
    public float polyrhythmSpread = 0.45f, polyrhythmDread = 1.2f, polyrhythmInterference = 1.25f;

    [Header("The land and the Loom")]
    [Tooltip("Spell potency at Coherence 0 and 1.")]
    public float incoherentPotency = 0.75f, coherentPotency = 1.2f;
    [Tooltip("Potency on a leyline, and the Signal Loss kept there (Stable Harmonic Channels already run).")]
    public float leylinePotency = 1.1f, leylineChannel = 0.8f;
    [Tooltip("Composure and Integrity a Pure Light being (not Discordant) loses per measure per point of Vibrational Fallout.")]
    public float falloutComposure = 3f, falloutIntegrity = 1f;
    [Tooltip("Composure each section regains per measure on Sacred ground.")]
    public float sacredRally = 1f;
    [Tooltip("The attacker's attack across a river, and the defender's defense from higher ground (per 0.1 elevation, capped).")]
    public float riverCrossing = 0.7f, heightDefense = 0.1f, heightCap = 0.3f;
    [Tooltip("Defense per entrenchment level (HOI4: +2% a day), at most maxEntrench levels.")]
    public float entrenchDefense = 0.05f;
    public float maxEntrench = 5f;
    [Tooltip("Defense of a side holding a settlement.")]
    public float settlementDefense = 1.25f;
    [Tooltip("Missile attacks under hard weather (travel multiplier above 1.2).")]
    public float stormMissiles = 0.8f;
    [Tooltip("Combat width lost inside a cover (deep forest, fen, canyon).")]
    public float coverWidth = 1f;
    [Tooltip("Missile attacks inside a cover.")]
    public float coverMissiles = 0.8f;

    [Header("Echoes (the seasons)")]
    [Tooltip("Echo of Crescendo: Cindergale burns hotter. Echo of Silence: Cindergale gutters, every section loses Composure each measure (the cold).")]
    public float crescendoFire = 1.1f, silenceFire = 0.9f, silenceChill = 0.5f;
    [Tooltip("Echo of Resonance: Resonance carries further. Echo of Dissonance: every chord is likelier to interfere.")]
    public float resonanceEcho = 1.1f, dissonanceEcho = 0.03f;

    [Header("The line")]
    [Tooltip("Attack gained per section overlapping the enemy's line (flanking), up to maxFlank sections.")]
    public float flankPerSection = 0.08f;
    public int maxFlank = 3;
    [Tooltip("The side with more scouting strikes first: attack on the first measure.")]
    public float initiative = 1.15f;
    [Tooltip("An ambush from cover: the ambushed side's Composure lost at the first measure (share of its maximum).")]
    public float ambushShock = 0.2f;
    [Tooltip("Composure lost by the back lane when the front lane is gone (the weavers are exposed), per measure.")]
    public float exposed = 4f;

    [Header("Mind Break: Composure broken")]
    [Tooltip("A Mind Broken section's attacks, parries, armor and wards are multiplied by these: it still fights, but it can be cut down.")]
    public float mindBreakAttack = 0.5f, mindBreakParry = 0.4f, mindBreakArmor = 0.5f, mindBreakWard = 0f;
    [Tooltip("A Mind Broken section steadies again once its Composure climbs back to this share (musicians, Sacred ground, a Chronicler).")]
    [Range(0f, 1f)] public float steadyAt = 0.3f;
    [Tooltip("Composure every other section of the side loses when one is Mind Broken, and when one is cut down (Total War: a rout nearby shakes the line).")]
    public float mindBreakShock = 2f, fallenShock = 5f;
    [Tooltip("Composure each section loses per measure from fatigueFrom on (the long fight wears the nerve).")]
    public float fatigue = 0.5f;
    public int fatigueFrom = 5;

    [Header("Ends: Integrity is the bar that decides")]
    [Tooltip("A formation is beaten when the Integrity of its line (front and back lanes) falls to this share of its maximum (0: every section cut down): the rest scatter and are run down.")]
    [Range(0f, 1f)] public float integrityBreak = 0.15f;
    [Tooltip("Wild creatures that are not cornered leave once their line's Composure falls to this share (BattleSide.withdrawAt overrides it).")]
    [Range(0f, 1f)] public float withdrawAt = 0f;
    [Tooltip("Integrity a fleeing section loses per point of the pursuers' pace (Total War's run-down).")]
    public float pursuit = 1.5f;
    [Tooltip("Share of lost Integrity that is wounded rather than dead, before menders.")]
    [Range(0f, 1f)] public float baseWounded = 0.35f;

    [Header("Capture")]
    [Tooltip("A Mind Broken section below this share of its Integrity is subdued and taken alive by a side that takes captives, instead of cut down: the way to bring animals home.")]
    [Range(0f, 1f)] public float captureBelow = 0.4f;
    [Tooltip("Mass surrender: a Mind Broken ordinary section held in contact (an enemy in its hex) gives up below this share of its Integrity (the reference's example: 60% Integrity and no effective Composure). Proposal.")]
    [Range(0f, 1f)] public float surrenderBelow = 0.6f;

    [Header("Legends in battle (commander and section leaders)")]
    [Tooltip("Per star in a Great, what a commander lends the whole stack: attack (Vanguard), parry (Architect), potency (Concertist), dread (Justiciar), Composure harm turned aside and Signal Loss (Sovereign), fewer misfires (Seer), wounded saved and rally (Chronicler).")]
    public float greatPerStar = 0.05f;
    [Tooltip("The same per star for a section leader, on its own section only.")]
    public float leaderPerStar = 0.08f;
    [Tooltip("The commander's own spell per measure at a binding score of 21 (Skilled).")]
    public float conductorPotency = 12f;
    [Tooltip("A legend's battle Composure (its own bar in battle, not its real Composure)...")]
    public float legendComposure = 100f;
    [Tooltip("...scaled by its real state when the battle begins: Pristine, Clouded, Fractured, Spiraling, Surrender (cannot fight).")]
    public float[] legendComposureByState = { 1.1f, 1f, 0.8f, 0.6f, 0f };
    [Tooltip("Whole-stack multiplier by the commander's battle Composure: steady (half or more left), strained (a quarter or more), faltering (above 0), and broken (Mind Break: the stack fights on leaderless).")]
    public float[] conductorNerve = { 1.05f, 0.97f, 0.9f, 0.82f };
    [Tooltip("Composure every section loses at once when its commander breaks.")]
    public float conductorFallShock = 10f;
    [Tooltip("Share of the Composure harm the enemy deals the stack that also reaches its commander (it feels the line waver; a Justiciar's dread reaches it too).")]
    [Range(0f, 1f)] public float conductorExposure = 0.1f;
    [Tooltip("The commander's battle Composure lost for each of its sections that suffers a Mind Break (half) or is cut down or taken.")]
    public float sectionLostStrain = 3f;
    [Tooltip("Share of its own chord's Essence Sacrifice the commander pays from its battle Composure.")]
    public float conductorEssence = 0.25f;

    [Header("Attachment: a legend and its company (CompanyBond)")]
    [Tooltip("Per star of attachment of a legend on the field, the company's attack, parry and Composure harm turned aside.")]
    public float attachmentPerStar = 0.06f;
    [Tooltip("Per star, the share of the run-down an attached company is spared when its side is beaten; an attached company is never cut down or taken in the rout (its legend covers its escape).")]
    [Range(0f, 0.33f)] public float attachmentCover = 0.3f;
    [Tooltip("Real strain per star for each attached company a legend leaves behind when it goes missing, and for each attached company cut down or taken.")]
    public float attachmentGrief = 5f, attachmentLoss = 8f;

    [Header("Aftermath: what reaches the legend's real Composure (LegendBattleFate)")]
    [Tooltip("Real strain for a lost battle.")]
    public float defeatStrain = 8f;
    [Tooltip("Real strain for a Mind Break: the commander's battle Composure broke, or the section a legend led was Mind Broken.")]
    public float mindBreakStrain = 10f;
    [Tooltip("Real strain per share of battle Composure the commander lost (the battle's weight even when won).")]
    public float battleWeight = 10f;
    [Tooltip("Real strain for retreating alone from a doomed battle and walking home missing in action.")]
    public float missingStrain = 20f;
    [Tooltip("Sevenths a legend is missing in action before it turns up in a settlement.")]
    public int missingSevenths = 7;
    [Tooltip("Sevenths of Traumatized for a Mind Break, and of Haunted for coming home from missing in action (LegendConditions).")]
    public int traumaSevenths = 14, hauntedSevenths = 7;
    [Tooltip("Fragments for a battle: Defiance for a victory (commander, leader), Acceptance for surviving a defeat.")]
    public int victoryDefiance = 4, leaderDefiance = 2, defeatAcceptance = 2;
}

/// <summary>The Symphony of War's settings in World.asset: its tuning, the section kinds, the templates, the ground.</summary>
[Serializable]
public class CombatSettings
{
    public BattlePreparationTuning preparation = new BattlePreparationTuning();
    public BattlePreparationTuning Preparation => preparation ?? (preparation = new BattlePreparationTuning());
    public BattleSurvivalTuning survival = new BattleSurvivalTuning();
    public BattleSurvivalTuning Survival => survival ?? (survival = new BattleSurvivalTuning());
    public BattleRhythmTuning rhythm = new BattleRhythmTuning();
    public BattleRhythmTuning Rhythm => rhythm ?? (rhythm = new BattleRhythmTuning());
    public BattleSpatialTuning spatial = new BattleSpatialTuning();
    public BattleSpatialTuning Spatial => spatial ?? (spatial = new BattleSpatialTuning());
    public BattleMeasureTuning measure = new BattleMeasureTuning();
    public BattleMeasureTuning Measure => measure ?? (measure = new BattleMeasureTuning());
    public BattleChordTuning chords = new BattleChordTuning();
    public BattleChordTuning Chords => chords ?? (chords = new BattleChordTuning());
    public BattleCrisisTuning crisis = new BattleCrisisTuning();
    public BattleCrisisTuning Crisis => crisis ?? (crisis = new BattleCrisisTuning());
    public List<BattleDoctrineSpec> doctrines = new List<BattleDoctrineSpec>();
    public List<BattleEquipmentSpec> equipment = new List<BattleEquipmentSpec>();
    public List<BattleCivicDeck> civicDecks = new List<BattleCivicDeck>();
    public BattleDoctrineSpec Doctrine(string id) => string.IsNullOrEmpty(id) ? null :
        (doctrines ?? new List<BattleDoctrineSpec>()).Concat(BattleDeploymentLogic.Defaults).FirstOrDefault(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
    public BattleEquipmentSpec Equipment(string id) => (equipment ?? new List<BattleEquipmentSpec>()).FirstOrDefault(e => e != null && e.id == id);
    public CombatTuning tuning = new CombatTuning();
    [Tooltip("Conscripted companies: merit toward promotion (ArmyRoster).")]
    public ConscriptionTuning conscription = new ConscriptionTuning();
    [Tooltip("Kinds of section (empty: CombatDefaults.Sections).")]
    public List<CombatSectionSpec> sections = new List<CombatSectionSpec>();
    [Tooltip("Orchestral Formation templates (empty: CombatDefaults.Templates).")]
    public List<FormationTemplate> templates = new List<FormationTemplate>();
    [Tooltip("The ground each terrain is (empty: CombatDefaults.Grounds).")]
    public List<GroundSpec> grounds = new List<GroundSpec>();
    [Tooltip("Terrains whose land sings an element (empty: CombatDefaults.Elements).")]
    public List<TerrainElement> elements = new List<TerrainElement>();
    [Tooltip("The card layer: each side's Symphony, the section decks, expedition kits (empty lists: SymphonyCards).")]
    public SymphonySettings symphony = new SymphonySettings();

    public SymphonySettings Symphony => symphony ?? (symphony = new SymphonySettings());

    public IReadOnlyList<CombatSectionSpec> Sections => sections != null && sections.Count > 0 ? sections : CombatDefaults.Sections;
    public IReadOnlyList<FormationTemplate> Templates => templates != null && templates.Count > 0 ? templates : CombatDefaults.Templates;
    public IReadOnlyList<GroundSpec> Grounds => grounds != null && grounds.Count > 0 ? grounds : CombatDefaults.Grounds;
    public IReadOnlyList<TerrainElement> Elements => elements != null && elements.Count > 0 ? elements : CombatDefaults.Elements;
    public CombatTuning Tuning => tuning ?? CombatTuning.Default;

    public CombatSectionSpec Section(string id) =>
        string.IsNullOrEmpty(id) ? null : Sections.FirstOrDefault(s => s != null && string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase));

    public FormationTemplate Template(string id) =>
        string.IsNullOrEmpty(id) ? null : Templates.FirstOrDefault(t => t != null && string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The ground a terrain is (Open when unlisted).</summary>
    public GroundSpec GroundOf(string terrain)
    {
        var found = Grounds.FirstOrDefault(g => g?.terrains != null && g.terrains.Any(t => string.Equals(t, terrain, StringComparison.OrdinalIgnoreCase)));
        return found ?? Grounds.FirstOrDefault(g => g != null && g.ground == BattleGround.Open) ?? new GroundSpec { name = "Open ground" };
    }

    public GroundSpec Ground(BattleGround ground) => Grounds.FirstOrDefault(g => g != null && g.ground == ground) ?? new GroundSpec { ground = ground, name = ground.ToString() };

    /// <summary>The element the land sings on a terrain, or Unattuned.</summary>
    public SpellBinding ElementOf(string terrain) =>
        Elements.FirstOrDefault(e => e != null && string.Equals(e.terrain, terrain, StringComparison.OrdinalIgnoreCase))?.binding ?? SpellBinding.Unattuned;
}
