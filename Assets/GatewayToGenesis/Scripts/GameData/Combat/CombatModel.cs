using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One section on the field: a raised <see cref="CombatSectionSpec"/> or a creature group. Holds its numbers after
/// the template's choices and its state through a battle. <see cref="Clone"/> gives a fresh copy for forecasts.
///
/// Two bars: Integrity (the body: at 0 the section is cut down) and Composure (the mind and the magical reserve: at 0
/// the section is Mind Broken, fighting on at a heavy penalty and unable to cast, until it steadies).
/// </summary>
public sealed class CombatSection
{
    public string name;
    public string specId;
    /// <summary>The species id when a creature group.</summary>
    public string speciesId;
    public FormationRow row;
    public SectionKind kind;
    /// <summary>Primary combat hex (BattleHexLayout), -1 until deployed. Independent of the section's role.</summary>
    public int battleHex = -1;
    public BattleDeploymentSlot deployment;
    public BattleEliteRole eliteRole;
    public BattleAbstraction abstraction;
    public string trainingTechnology, productionUnit, equipment;
    public int productionTier = 1;
    public List<string> trainingCards = new List<string>(), equipmentCards = new List<string>();

    public float maxIntegrity, maxComposure, structure = 0.75f;
    public float attack, defense, breakthrough, armor, piercing, width = 1f, speed = 3f, charge = 1f;
    public float potency, ward;
    /// <summary>Composure harm dealt is multiplied by this (roars, songs, screeching; a Justiciar's dread is the conductor's).</summary>
    public float dread = 1f;
    public bool needsAccompaniment;
    /// <summary>Casts through a Soul Leitmotif (a civilization's Spellweavers): bound by the Age's magic (<see cref="AgeMagic"/>). Creatures cast by instinct.</summary>
    public bool soulWeaver;

    /// <summary>The primary binding: its weakness, and the root it casts in first.</summary>
    public SpellBinding primary;
    /// <summary>Other bindings it can root a spell in (a creature's secondary attunements), at <see cref="CombatTuning.secondaryPotency"/>.</summary>
    public List<SpellBinding> secondary = new List<SpellBinding>();
    /// <summary>Minor Notes layered over its root (casters).</summary>
    public List<SpellBinding> harmony = new List<SpellBinding>();

    public BindingOrgan organ;
    public HarmonicNiche niche;

    public float recon, catalyst, rally, mending, woundedShare, entrench;
    public List<GroundModifier> grounds = new List<GroundModifier>();

    /// <summary>Individuals in it (creatures in the group, soldiers in a conscripted company); 0 when not counted.</summary>
    public int count;
    /// <summary>A legend leading this section (null: nameless). It lends its Greats to this section alone.</summary>
    public BattleLegend leader;
    /// <summary>The conscripted company it was mustered from (<see cref="ArmyRoster"/>), or null.</summary>
    public string unitId;
    /// <summary>Legends attached to this company, with their stars (1-3; <see cref="CompanyBond"/>): with one of them on the field it fights harder and is hard to lose.</summary>
    public Dictionary<string, int> bonds;

    // State through a battle.
    public float integrity, composure;
    public float parasiticStrain;
    public bool deathKnell, permanentDeath, evacuated, eliteCheck, eliteCheckmate, savingRelic, equipmentLost;
    public int deathblowChecks;
    public float severeWounds, knellCapability = .15f;
    /// <summary>Mind Break: Composure broke. Attacks, parries, armor and wards fall (<see cref="CombatTuning.mindBreakAttack"/>), no spells, until it steadies.</summary>
    public bool mindBroken;
    /// <summary>Integrity gone, or run down after its side lost: out of the battle.</summary>
    public bool destroyed;
    /// <summary>It fled when its side was beaten (still alive, out of the battle).</summary>
    public bool fled;
    /// <summary>Mind Broken and below <see cref="CombatTuning.captureBelow"/> Integrity: subdued and taken by the enemy, alive.</summary>
    public bool captured;
    public bool committed;
    public float lost, dead, wounded;
    public int casts, misfires, timesMindBroken;

    public bool Standing => !destroyed && !fled && !captured;
    /// <summary>Individuals left, by its Integrity.</summary>
    public int Alive => count <= 0 || permanentDeath ? 0 : deathKnell ? Math.Max(1, count) : Math.Max(integrity > 0f ? 1 : 0, (int)Math.Round(count * IntegrityShare));
    public bool Fighting => Standing && battleHex >= 0;
    public bool Casts => potency > 0f;
    public bool PureLight => structure < CreatureTaxonomy.PureLightBeingBelow || organ != BindingOrgan.None;
    public float IntegrityShare => maxIntegrity <= 0f ? 0f : deathKnell ? knellCapability : Math.Max(0f, integrity) / maxIntegrity;
    public float ComposureShare => maxComposure <= 0f ? 0f : Math.Max(0f, composure) / maxComposure;
    public ChordTier Tier => (ChordTier)Math.Min(3, harmony?.Count ?? 0);

    /// <summary>The bindings it can root a spell in, primary first.</summary>
    public IEnumerable<SpellBinding> Roots
    {
        get
        {
            if (primary != SpellBinding.Unattuned) yield return primary;
            foreach (var b in secondary ?? new List<SpellBinding>()) if (b != SpellBinding.Unattuned && b != primary) yield return b;
        }
    }

    public GroundModifier On(BattleGround ground) => grounds?.FirstOrDefault(g => g != null && g.ground == ground);

    public void Reset()
    {
        integrity = maxIntegrity;
        composure = maxComposure;
        battleHex = -1;
        committed = mindBroken = destroyed = fled = captured = false;
        deathKnell = permanentDeath = evacuated = eliteCheck = eliteCheckmate = false; deathblowChecks = 0;
        lost = dead = wounded = 0f;
        casts = misfires = timesMindBroken = 0;
    }

    public CombatSection Clone()
    {
        var c = (CombatSection)MemberwiseClone();
        c.secondary = new List<SpellBinding>(secondary ?? new List<SpellBinding>());
        c.harmony = new List<SpellBinding>(harmony ?? new List<SpellBinding>());
        c.grounds = (grounds ?? new List<GroundModifier>()).Select(g => g == null ? null : new GroundModifier { ground = g.ground, attack = g.attack, defense = g.defense }).ToList();
        c.bonds = bonds == null ? null : new Dictionary<string, int>(bonds, bonds.Comparer);
        c.leader = leader?.Clone();
        c.trainingCards = new List<string>(trainingCards ?? new List<string>());
        c.equipmentCards = new List<string>(equipmentCards ?? new List<string>());
        c.deployment = deployment == null ? null : new BattleDeploymentSlot { role = deployment.role, rank = deployment.rank, lane = deployment.lane };
        return c;
    }

    /// <summary>Raises one section of <paramref name="spec"/>, a caster rooted in <paramref name="binding"/> with <paramref name="harmony"/>.</summary>
    public static CombatSection Raise(CombatSectionSpec spec, SpellBinding binding = SpellBinding.Unattuned, IEnumerable<SpellBinding> harmony = null)
    {
        bool caster = spec.Casts;
        var s = new CombatSection
        {
            name = caster && binding != SpellBinding.Unattuned ? $"{spec.name} ({binding})" : spec.name,
            specId = spec.id, row = spec.row, kind = spec.kind,
            trainingTechnology = spec.technology, productionUnit = spec.productionUnit, productionTier = Math.Max(1, spec.productionTier),
            trainingCards = new List<string>(spec.trainingCards ?? new List<string>()),
            maxIntegrity = spec.integrity, maxComposure = spec.composure, structure = spec.structure,
            attack = spec.attack, defense = spec.defense, breakthrough = spec.breakthrough, armor = spec.armor, piercing = spec.piercing,
            width = spec.width, speed = spec.speed, charge = spec.charge,
            potency = caster && binding != SpellBinding.Unattuned ? spec.potency : 0f, ward = spec.ward,
            needsAccompaniment = spec.needsAccompaniment, soulWeaver = caster,
            // A person who never had a Motif Awakening is unattuned: no element, no weakness.
            primary = caster ? binding : SpellBinding.Unattuned,
            harmony = caster ? (harmony ?? Enumerable.Empty<SpellBinding>()).Where(h => h != SpellBinding.Unattuned && h != binding).Distinct().Take(3).ToList() : new List<SpellBinding>(),
            recon = spec.recon, catalyst = spec.catalyst, rally = spec.rally, mending = spec.mending, woundedShare = spec.wounded, entrench = spec.entrench,
            grounds = new List<GroundModifier>(spec.grounds ?? new List<GroundModifier>()),
            count = Math.Max(0, spec.people),
        };
        s.Reset();
        return s;
    }
}

/// <summary>
/// A Legend in battle: the commander of a stack (the vault's Battle Conductor, Combat System.md) or the leader of one
/// section. It lends what it has become, its stars in the Greats (<see cref="LegendGreats"/>): a Great Vanguard's
/// blades strike harder, a Great Architect's line holds, a Great Sovereign's steadies, and so on; a commander lends them
/// to the whole stack and casts its own chord each measure from its Soul Leitmotif (and, once the Age allows Ornamental
/// Magic, its Ornaments). An Unattuned Legend lends nothing but its presence.
///
/// In battle a legend has a Composure bar of its own (<see cref="BattleComposure"/>), separate from its real Composure:
/// its real state only sets where the bar starts. What happens in the battle reaches the real soul afterwards, through
/// <see cref="LegendBattleFate"/>: the caller applies it (<see cref="LegendProgress.ApplyBattleFate"/>).
/// </summary>
public sealed class BattleLegend
{
    public BattleDeckEvolution deckEvolution = new BattleDeckEvolution();
    /// <summary>Negative: derive tactical intelligence from Seer/Vanguard stars and reconnaissance.</summary>
    public float tacticalIntelligence = -1f;
    public string name;
    public SpellBinding leitmotif;
    public List<SpellBinding> ornaments = new List<SpellBinding>();
    /// <summary>The seven binding scores (<see cref="LegendSoulRules.Bindings"/>); empty: 21 (Skilled) each.</summary>
    public Dictionary<string, int> scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Its stars in each Great (none: an Unattuned Legend).</summary>
    public Dictionary<LegendClass, int> greats = new Dictionary<LegendClass, int>();
    /// <summary>Real Composure's strain when the battle begins (0 Pristine ... Surrender); Clouded's baseline by default.</summary>
    public float strain = 20f;
    /// <summary>Where each Composure state begins (null: the defaults).</summary>
    public ComposureTuning composureTuning;
    /// <summary>Symphony Cards it has learned into its personal grimoire (ids; <see cref="LegendGrimoires"/>). Empty: none.</summary>
    public List<string> grimoire = new List<string>();
    /// <summary>Awakened (an Ornament, or the Awakened State): it can sound Major Notes, its own leitmotif among them.</summary>
    public bool awakened;
    public List<string> traits = new List<string>(), conditions = new List<string>(), memories = new List<string>();
    public int piety;

    /// <summary>A forecast owns its legend data; persistent souls are changed only by applying the final fate.</summary>
    public BattleLegend Clone()
    {
        var c = (BattleLegend)MemberwiseClone();
        c.deckEvolution = deckEvolution?.Clone() ?? new BattleDeckEvolution();
        c.ornaments = new List<SpellBinding>(ornaments ?? new List<SpellBinding>());
        c.scores = scores == null ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(scores, scores.Comparer);
        c.greats = new Dictionary<LegendClass, int>(greats ?? new Dictionary<LegendClass, int>());
        c.grimoire = new List<string>(grimoire ?? new List<string>());
        c.traits = new List<string>(traits ?? new List<string>());
        c.conditions = new List<string>(conditions ?? new List<string>());
        c.memories = new List<string>(memories ?? new List<string>());
        c.composureTuning = composureTuning?.Clone();
        return c;
    }

    public ComposureTuning ComposureTuning => composureTuning ?? DefaultComposure;
    private static readonly ComposureTuning DefaultComposure = new ComposureTuning();

    /// <summary>The real Composure state at the battle's start.</summary>
    public ComposureState State => ComposureRules.StateOf(strain, ComposureTuning);

    public int Stars(LegendClass great) => greats != null && greats.TryGetValue(great, out int s) ? Math.Max(0, Math.Min(LegendGreats.MaxStars, s)) : 0;

    /// <summary>"2★ Great Vanguard · 1★ Great Architect", or "Unattuned Legend".</summary>
    public string Title => LegendGreats.Title(greats);

    /// <summary>Its battle Composure at the start: the tuning's bar, scaled by its real state (a Spiraling legend starts low; one in Surrender cannot fight).</summary>
    public float BattleComposure(CombatTuning t)
    {
        t = t ?? CombatTuning.Default;
        var by = t.legendComposureByState;
        return t.legendComposure * (by == null || by.Length == 0 ? 1f : by[Math.Max(0, Math.Min((int)State, by.Length - 1))]);
    }

    public int Score(SpellBinding b) => scores != null && scores.TryGetValue(HarmonicCircle.Name(b), out int v) ? v : 21;

    /// <summary>A battle legend from a legend's soul, its binding scores and its standing in the Greats.</summary>
    public static BattleLegend Of(string name, LegendSoul soul, Dictionary<string, int> scores, IReadOnlyDictionary<LegendClass, int> greats, ComposureTuning tuning = null) => new BattleLegend
    {
        name = name,
        leitmotif = HarmonicCircle.Of(soul?.leitmotif),
        ornaments = (soul?.ornaments ?? new List<string>()).Select(HarmonicCircle.Of).Where(b => b != SpellBinding.Unattuned).ToList(),
        scores = scores ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        greats = greats == null ? new Dictionary<LegendClass, int>() : greats.ToDictionary(p => p.Key, p => p.Value),
        strain = soul?.strain ?? (tuning ?? DefaultComposure).baseline,
        composureTuning = tuning,
        awakened = LegendGrimoires.Awakened(soul),
        traits = (soul?.wish ?? new List<string>()).Concat(soul?.expression ?? new List<string>()).Distinct().ToList(),
    };
}

/// <summary>One side of a battle.</summary>
public sealed class BattleSide
{
    /// <summary>Physical Seer selected at deployment; -1 selects the most capable eligible elite.</summary>
    public int seerVoice = -1;
    public bool objectiveLost;
    public string name;
    public List<CombatSection> sections = new List<CombatSection>();
    /// <summary>The stack's commander (the Battle Conductor), or null.</summary>
    public BattleLegend conductor;
    /// <summary>Whether it subdues and takes Mind Broken enemies below <see cref="CombatTuning.captureBelow"/> Integrity instead of cutting them down. Wild creatures do not.</summary>
    public bool takesCaptives = true;
    public SpellTempo tempo = SpellTempo.Staccato;
    /// <summary>Entrenchment levels already dug in (a defender who waited; HOI4 digs in by the day).</summary>
    public float entrenchment;
    /// <summary>Wild creatures: they fight to the last only when cornered or hunting, and never pursue far.</summary>
    public bool wild;
    /// <summary>Withdraws once its standing sections' Composure falls below this share (negative: the tuning's <see cref="CombatTuning.withdrawAt"/>). Shy creatures leave early.</summary>
    public float withdrawAt = -1f;
    /// <summary>
    /// Its Symphony: the cards it performs each measure (<see cref="SymphonyDecks"/>), each voiced by one of its sections
    /// (by index) or its conductor. Empty: no cards, and its sections fight wholly by their drilled rhythm (the plain
    /// auto-resolve).
    /// </summary>
    public List<DeckCard> deck = new List<DeckCard>();
    public List<BattleEchoingBond> echoingBonds = new List<BattleEchoingBond>();
    /// <summary>The player performs its Symphony (a manual battle, <see cref="BattleResolver.Begin"/>): the resolver does not play its cards for it.</summary>
    public bool manual;

    /// <summary>Chosen doctrine and its independent cohesion condition. 100 is a normalized full bar, not a bonus.</summary>
    public BattleStance stance = BattleStance.Line;
    public string doctrine;
    public BattleScoreInputs scoreInputs;
    public float maxStanceStability = 100f, stanceStability = 100f;
    public bool StanceBroken => stanceStability <= 0f;
    /// <summary>Known population, with one element for an uncounted section; a separate conductor counts once.</summary>
    public int CombatantCount
    {
        get
        {
            long total = sections.Sum(s => (long)Math.Max(1, s.count));
            if (conductor != null && !sections.Any(s => s.leader == conductor ||
                (s.leader != null && !string.IsNullOrEmpty(conductor.name) && s.leader.name == conductor.name))) total++;
            return (int)Math.Max(1L, Math.Min(int.MaxValue, total));
        }
    }

    public IEnumerable<CombatSection> Standing => sections.Where(s => s.Standing);
    public bool HasSymphony => deck != null && deck.Count > 0;

    /// <summary>Copies the current condition without healing it. Shared legend identities stay shared within the copy.</summary>
    public BattleSide Clone()
    {
        var legends = new Dictionary<BattleLegend, BattleLegend>();
        BattleLegend CopyLegend(BattleLegend legend)
        {
            if (legend == null) return null;
            if (!legends.TryGetValue(legend, out var copy)) legends[legend] = copy = legend.Clone();
            return copy;
        }
        var copySide = new BattleSide
        {
            name = name, conductor = CopyLegend(conductor), tempo = tempo, entrenchment = entrenchment, seerVoice = seerVoice,
              wild = wild, withdrawAt = withdrawAt, takesCaptives = takesCaptives, manual = manual, objectiveLost = objectiveLost,
            stance = stance, maxStanceStability = maxStanceStability, stanceStability = stanceStability,
            doctrine = doctrine, scoreInputs = scoreInputs?.Clone(),
            echoingBonds = echoingBonds.Select(b => b.Clone()).ToList(),
        };
        foreach (var section in sections)
        {
            var copy = section.Clone();
            copy.leader = CopyLegend(section.leader);
            copySide.sections.Add(copy);
        }
        foreach (var dc in deck ?? new List<DeckCard>())
            copySide.deck.Add(dc == null ? null : new DeckCard
            {
                card = dc.card?.Clone(), voice = dc.voice, legend = CopyLegend(dc.legend),
                scale = dc.scale, major = dc.major, source = dc.source,
            });
        return copySide;
    }

    public float MaxIntegrity => sections.Sum(s => s.maxIntegrity);
    /// <summary>Integrity still with the side (captives are the enemy's now).</summary>
    public float Integrity => sections.Where(s => !s.captured).Sum(s => Math.Max(0f, s.integrity));
    /// <summary>Every legend on this side: the commander, then the section leaders.</summary>
    public IEnumerable<BattleLegend> Legends => new[] { conductor }.Concat(sections.Select(s => s.leader)).Where(l => l != null).Distinct();
    /// <summary>The line's Integrity bar (front and back lanes; support stands behind it): what decides the battle.</summary>
    public float LineIntegrity => sections.Where(s => s.row != FormationRow.Support && s.Standing).Sum(s => Math.Max(0f, s.integrity));
    public float LineMaxIntegrity => sections.Where(s => s.row != FormationRow.Support).Sum(s => s.maxIntegrity);
    /// <summary>The line's Composure bar.</summary>
    public float LineComposure => sections.Where(s => s.row != FormationRow.Support && s.Standing).Sum(s => Math.Max(0f, s.composure));
    public float LineMaxComposure => sections.Where(s => s.row != FormationRow.Support).Sum(s => s.maxComposure);
}

/// <summary>
/// Where a battle is fought: the ground, the land's element, the Loom's state there and the season. Built from a world
/// cell with <see cref="From"/>, or set by hand (tests, the sandbox).
/// </summary>
public sealed class Battlefield
{
    /// <summary>Sparse terrain overrides for the sixteen primary combat hexes.</summary>
    public List<BattleHexTerrain> hexes = new List<BattleHexTerrain>();

    public Battlefield Clone()
    {
        var copy = (Battlefield)MemberwiseClone();
        copy.hexes = hexes == null ? new List<BattleHexTerrain>() : hexes.Select(h => h?.Clone()).ToList();
        return copy;
    }
    public string terrain = "plains";
    public string place = "open ground";
    public BattleGround ground = BattleGround.Open;
    /// <summary>The element the land sings (spells rooted in it land harder).</summary>
    public SpellBinding element;
    public string cover;
    public bool concealed;
    /// <summary>The attacker crosses a river to reach the defender.</summary>
    public bool riverCrossing;
    /// <summary>The defender's ground stands this much higher (world elevation units, 0-1).</summary>
    public float height;
    /// <summary>The attacker's ground stands this much higher: it comes downhill (world elevation units, 0-1).</summary>
    public float downhill;
    /// <summary>The ground the attacker stands on when it is not the defender's (a clash across two hexes): its sections fight by their own footing.</summary>
    public BattleGround? attackerGround;
    public bool settlement;
    public float coherence = 0.5f, dissonance;
    public bool leyline, sacred;
    public float fallout;
    /// <summary>Hard weather (the cell's travel multiplier from weather).</summary>
    public float weather = 1f;
    /// <summary>The Echo (1 Resonance, 2 Crescendo, 3 Dissonance, 4 Silence), 0 unknown.</summary>
    public int echo;
    /// <summary>The Age number.</summary>
    public int age;
    /// <summary>Share of the magic flow the Age allows a civilization to use (<see cref="WorldGenSettings.MagicAccess"/>).</summary>
    public float magicAccess = 1f;

    /// <summary>
    /// The field at <paramref name="tile"/>, the attacker coming from <paramref name="from"/> (null: no river or height
    /// read). Reads terrain, cover, rivers, elevation, settlement, Coherence, Dissonance, leylines, Sacred ground,
    /// Vibrational Fallout and weather.
    /// </summary>
    public static Battlefield From(WorldTile tile, WorldTile from, WorldGenSettings gen, CombatSettings combat, int age, int echo)
    {
        combat = combat ?? new CombatSettings();
        var f = new Battlefield { age = age, echo = echo, magicAccess = gen?.MagicAccess(age) ?? 1f };
        if (tile == null) return f;
        var ground = combat.GroundOf(tile.terrain);
        f.terrain = tile.terrain;
        f.ground = ground.ground;
        f.place = gen?.Terrain(tile.terrain)?.name ?? ground.name;
        f.element = combat.ElementOf(tile.terrain);
        f.cover = tile.cover;
        f.concealed = tile.concealed;
        // Defenders standing on a river are reached across it, unless the attacker comes along the same river.
        f.riverCrossing = from != null && tile.river && tile.downstream != from.index && from.downstream != tile.index;
        f.height = from == null ? 0f : Math.Max(0f, tile.elevation - from.elevation);
        f.settlement = tile.settlement >= 0;
        f.coherence = tile.coherence;
        f.dissonance = tile.dissonance;
        f.leyline = tile.leylines != 0;
        f.sacred = tile.sacred;
        f.fallout = tile.fallout;
        f.weather = tile.weatherTravelMultiplier;
        return f;
    }
}

/// <summary>A battle to resolve.</summary>
public sealed class BattleSetup
{
    public bool majorEncounter, boss, decisive, originalEight;
    public string objective;
    public bool RequiresManual => majorEncounter || boss || decisive || originalEight;
    public BattleSide attacker, defender;
    public Battlefield field = new Battlefield();
    public int seed = 1;

    public BattleSetup Clone(int newSeed) => new BattleSetup { attacker = attacker.Clone(), defender = defender.Clone(), field = field?.Clone(), seed = newSeed,
        majorEncounter = majorEncounter, boss = boss, decisive = decisive, originalEight = originalEight, objective = objective };
}

/// <summary>
/// How a battle ended for one side (<see cref="BattleVerdicts"/>): three victories, three defeats, and the Legendary
/// Victory. Each battle's two verdicts mirror each other (a Decisive Victory is the other side's Crushing Defeat, a Close
/// Victory its Close Defeat, a Pyrrhic Victory its Valiant Defeat). The Legendary Victory is never given by the
/// auto-resolve: only to a battle won by hand (the micro layer) that the forecast said would be lost. A side that holds
/// the field when the measures run out has won it (<see cref="BattleReport.Held"/>). Append only.
/// </summary>
public enum BattleOutcome { DecisiveVictory, CloseVictory, PyrrhicVictory, CloseDefeat, ValiantDefeat, CrushingDefeat, LegendaryVictory }

/// <summary>What one side came out with.</summary>
public sealed class SideResult
{
    public float lossBurden;
    public bool mythical;
    public string name;
    /// <summary>Integrity (bodies) of every section at the start and the end; the loss is split into dead and wounded.</summary>
    public float integrityBefore, integrityAfter, dead, wounded;
    /// <summary>The line's Composure bar at the start and the end.</summary>
    public float composureBefore, composureAfter;
    /// <summary>Sections Mind Broken at the end, sections that suffered a Mind Break at any point, sections cut down, sections taken captive.</summary>
    public int mindBroken, everMindBroken, destroyed, captured, misfires, casts;
    /// <summary>Individuals the enemy took captive from this side.</summary>
    public int capturedIndividuals;
    /// <summary>Integrity that went alive to the enemy with its captives (not dead, not wounded).</summary>
    public float capturedIntegrity;
    /// <summary>Its line's Integrity gave out: it lost the field.</summary>
    public bool beaten;
    /// <summary>Wild creatures that left the fight on their own.</summary>
    public bool withdrew;
    /// <summary>How the battle ended for this side (<see cref="BattleVerdicts"/>).</summary>
    public BattleOutcome outcome;
    /// <summary>Its commander's battle Composure broke (a Mind Break: the stack fought on leaderless).</summary>
    public bool conductorBroke;
    /// <summary>The commander's battle Composure at the start and the end (0 with no commander).</summary>
    public float conductorBefore, conductorAfter;
    /// <summary>Its Symphony's power when the battle began (<see cref="SymphonyPower"/>, on this field), the cards it played, and how many flickered.</summary>
    public float symphonyPower;
    public int cardsPlayed, cardFlickers;
    /// <summary>The card it played most, or null.</summary>
    public string signatureCard;
    public float LossShare => integrityBefore <= 0f ? 0f : (integrityBefore - integrityAfter) / integrityBefore;
}

/// <summary>The bars of one side at the end of a measure (Stellaris's and Total War's battle bars).</summary>
public struct SideBars
{
    /// <summary>Independent formation cohesion; spatial consequences are resolved by the Stance rules.</summary>
    public BattleStance stance;
    public float stanceStability, stanceStabilityMax;
    /// <summary>The line's Integrity (the bar that decides) and Composure (the one that shakes it).</summary>
    public float integrity, integrityMax, composure, composureMax;
    /// <summary>The commander's battle Composure, and where it began (0 and 0 with no commander).</summary>
    public float conductor, conductorMax;
    public int mindBroken;

    public float IntegrityShare => integrityMax <= 0f ? 0f : integrity / integrityMax;
    public float ComposureShare => composureMax <= 0f ? 0f : composure / composureMax;
    public float ConductorShare => conductorMax <= 0f ? 1f : conductor / conductorMax;
    public float StanceShare => stanceStabilityMax <= 0f ? 0f : stanceStability / stanceStabilityMax;
}

/// <summary>One measure of the timeline (measure 0: the line-up, after the Overture).</summary>
public struct BattleMeasure
{
    public int measure;
    public int beat;
    public SideBars attacker, defender;
    public List<BattlePosition> positions;
}

/// <summary>The resolved battle: the outcome, each side's losses and a readable account, measure by measure.</summary>
public sealed class BattleReport
{
    public List<BattleSurvivalEvent> survival = new List<BattleSurvivalEvent>();
    public List<BattlePopulationFate> population = new List<BattlePopulationFate>();
    public List<BattleCrisisEvent> crises = new List<BattleCrisisEvent>();
    public List<BattleRhythmRecord> rhythm = new List<BattleRhythmRecord>();
    /// <summary>Every Resolution performed (vault: "Performances Remembered"): who contributed, what they built, how it ended.</summary>
    public List<BattleResolutionRecord> resolutions = new List<BattleResolutionRecord>();
    public List<BattlePhaseEvent> phases = new List<BattlePhaseEvent>();
    public List<BattleActionIntent> commitments = new List<BattleActionIntent>();
    public List<BattleChordEvent> chordEvents = new List<BattleChordEvent>();
    public List<BattleSpatialEvent> spatialEvents = new List<BattleSpatialEvent>();
    /// <summary>How the battle ended for the attacker (the defender's is <see cref="SideResult.outcome"/> on <see cref="defender"/>).</summary>
    public BattleOutcome outcome;
    /// <summary>1 attacker, -1 defender, 0 neither (a stalemate: the defender holds).</summary>
    public int winner;
    public int measures;
    public SideResult attacker = new SideResult(), defender = new SideResult();
    /// <summary>Both sides' bars after the Overture and after each measure, for a battle screen to play back.</summary>
    public List<BattleMeasure> timeline = new List<BattleMeasure>();
    public List<string> log = new List<string>();
    /// <summary>How often each element met each primary ("Flux on Cindergale" → spells cast).</summary>
    public Dictionary<string, int> matchups = new Dictionary<string, int>();
    /// <summary>Sections subdued and taken alive (Mind Broken below <see cref="CombatTuning.captureBelow"/> Integrity): tamed animals, prisoners.</summary>
    public List<BattleCaptive> captives = new List<BattleCaptive>();
    /// <summary>What the battle did to every legend in it, for the caller to apply to their souls.</summary>
    public List<LegendBattleFate> legends = new List<LegendBattleFate>();
    /// <summary>Every card played, in order, for a battle screen to play back.</summary>
    public List<CardPlay> plays = new List<CardPlay>();
    public int seed;

    public bool AttackerWon => winner > 0;

    /// <summary>The captives one side took (true: the attacker's).</summary>
    public IEnumerable<BattleCaptive> TakenBy(bool attacker) => captives.Where(c => c.takenByAttacker == attacker);

    public static string Words(BattleOutcome outcome) => BattleVerdicts.Words(outcome);

    /// <summary>Neither line gave way before the measures ran out: the defender held the field (and so won it).</summary>
    public bool Held => winner == 0;

    /// <summary>How the battle ended for one side (true: the attacker).</summary>
    public BattleOutcome OutcomeFor(bool attacker) => attacker ? this.attacker.outcome : defender.outcome;

    /// <summary>Won by hand against the forecast (<see cref="BattleOutcome.LegendaryVictory"/>): the side that did, or null.</summary>
    public bool? legendaryFor;
}

/// <summary>A section taken alive: a creature group to bring home (tamed, penned, returned to an Enclave), or prisoners.</summary>
public sealed class BattleCaptive
{
    public bool takenByAttacker;
    /// <summary>The side it was taken from.</summary>
    public string from;
    public string name;
    /// <summary>Its species (a creature group), or null.</summary>
    public string speciesId;
    /// <summary>Its section kind, or null.</summary>
    public string specId;
    /// <summary>The conscripted company it was, or null.</summary>
    public string unitId;
    /// <summary>Individuals taken (0 when the section was not counted).</summary>
    public int individuals;
    public float integrity;
    public int measure;
}

/// <summary>One card played in a battle.</summary>
public sealed class CardPlay
{
    public int beat;
    public long action;
    public int countdown;
    public bool minor, reaction;
    public bool failed;
    public string failure;
    public int measure;
    public bool attacker;
    public string card, cardName;
    /// <summary>The section that voiced it (or the commander), and the section it was aimed at (null: none, or a whole line).</summary>
    public string voice, target;
    /// <summary>A spell that flickered into Discordant Interference (it landed weakly).</summary>
    public bool flicker;
    /// <summary>Its effects' multiplier from the field (its ground, its condition) and from the rendition.</summary>
    public float field = 1f, rendition = 1f;
    /// <summary>Its Purpose, and the Setups performed before it this measure (groundwork: an Offensive card after them lands harder).</summary>
    public SpellPurpose purpose;
    public int groundwork;
}

/// <summary>How a legend fought: commanding the stack, or leading one section.</summary>
public enum BattleRole { Commander, SectionLeader }

/// <summary>
/// What a battle did to one legend, for the caller to apply (<see cref="LegendProgress.ApplyBattleFate"/>): the strain
/// its real Composure takes, the fragments it earned, conditions it carries home, and whether it went missing in action.
/// Named aftermath distinguishes permanent death, capture and missing-in-action recovery. Ordinary strain cannot
/// cross persistent Surrender; explicit parasitic strain is recorded separately. Deck changes commit with the fate.
/// </summary>
public sealed class LegendBattleFate
{
    public BattleDeckEvolution deckEvolution;
    public bool dead, captured, deathKnell;
    public string name;
    public BattleRole role;
    /// <summary>The section it led (section leaders), or the side's name.</summary>
    public string section;
    public bool attacker, won;
    /// <summary>Its battle Composure broke (commanders), or its section suffered a Mind Break (leaders).</summary>
    public bool mindBroken;
    /// <summary>It left the field when its side or section was doomed: missing in action until it walks home.</summary>
    public bool missing;
    public int missingSevenths;
    /// <summary>Strain for its real Composure (the caller adds it to the soul).</summary>
    public float strain;
    /// <summary>The explicitly extraordinary part of strain; ordinary aftermath cannot cause Surrender.</summary>
    public float parasiticStrain;
    /// <summary>The part of <see cref="strain"/> owed to the companies it is attached to (cut down, taken, or left behind).</summary>
    public float grief;
    public List<FragmentAward> fragments = new List<FragmentAward>();
    /// <summary>Conditions it carries home (ids from <see cref="LegendConditions"/>), with their Sevenths.</summary>
    public List<LegendCondition> conditions = new List<LegendCondition>();
    public string deed;
}
