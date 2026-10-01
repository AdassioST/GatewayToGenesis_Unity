using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// A card's face and back as the Sonata website's Spell Builder draws a spell (src/features/spells/SpellCard.tsx), with
/// no scene state, so the game's cards and the website's grimoire read as one system:
/// - **the suit is the Root**: the whole card takes the Major Note's colours (<see cref="Palette"/>, the website's);
/// - **the rank is the chord** (Unison, Dyad Chord, Triad Chord, Tetrad Chord), and **the corner is the note** the Root
///   carries (<see cref="Note"/>: Resonance C, Luminance D, Flux E, Void A, Cindergale F, Crystal G, Strand B);
/// - **the art is the chord** drawn on the circle of fifths (<see cref="Station"/>: C at twelve o'clock, then G, D, A, E,
///   B, G♭, D♭, A♭, E♭, B♭, F clockwise; the Root lit, each Minor Note a smaller node with a line back to it, the five
///   stations no thread stands on drawn as rests, and B back to F, the tritone, left open);
/// - under the name, **the suit line** (the Root, then each Minor Note) and **the practice** (The Registers of Magic);
/// - the **catchline**, and the **purpose** stamp (<see cref="SpellPurpose"/>, with its role and what it does).
/// The back is <see cref="Reading"/>: what it does, what it costs, the image to hold, where it is at home.
/// Steel, orders and instincts have no Root: their suit is Steel, their rank their kind.
/// </summary>
public static class CardFace
{
    /// <summary>The note a thread carries (vault: each binding binds a natural).</summary>
    public static string Note(SpellBinding b)
    {
        switch (b)
        {
            case SpellBinding.Resonance: return "C";
            case SpellBinding.Luminance: return "D";
            case SpellBinding.Flux: return "E";
            case SpellBinding.Void: return "A";
            case SpellBinding.Cindergale: return "F";
            case SpellBinding.Crystal: return "G";
            case SpellBinding.Strand: return "B";
            default: return "·";
        }
    }

    /// <summary>The twelve stations of the circle of fifths, clockwise from C at twelve o'clock.</summary>
    public static readonly IReadOnlyList<string> Fifths = new[] { "C", "G", "D", "A", "E", "B", "Gb", "Db", "Ab", "Eb", "Bb", "F" };

    /// <summary>A thread's station on the circle of fifths (0 C at twelve o'clock ... 11 F), or -1.</summary>
    public static int Station(SpellBinding b)
    {
        string note = Note(b);
        for (int i = 0; i < Fifths.Count; i++) if (Fifths[i] == note) return i;
        return -1;
    }

    /// <summary>The seven threads in fifths order (F C G D A E B): Cindergale, Resonance, Crystal, Luminance, Void, Flux, Strand.</summary>
    public static readonly IReadOnlyList<SpellBinding> FifthsOrder = new[]
    {
        SpellBinding.Cindergale, SpellBinding.Resonance, SpellBinding.Crystal, SpellBinding.Luminance, SpellBinding.Void, SpellBinding.Flux, SpellBinding.Strand,
    };

    /// <summary>The five stations no thread stands on (the rests): G♭, D♭, A♭, E♭, B♭.</summary>
    public static IEnumerable<(int station, string note)> Rests() =>
        Enumerable.Range(0, Fifths.Count).Where(i => HarmonicCircle.Seven.All(b => Station(b) != i)).Select(i => (i, Fifths[i]));

    /// <summary>The website's colours for a thread: bright (the gem), deep (the card), ink (the shadow). Steel is bone and iron.</summary>
    public static (Color bright, Color deep, Color ink) Palette(SpellBinding b)
    {
        switch (b)
        {
            case SpellBinding.Resonance: return (Hex("4fd8cb"), Hex("0b3b38"), Hex("02110f"));
            case SpellBinding.Luminance: return (Hex("f4e9b8"), Hex("3b3416"), Hex("151004"));
            case SpellBinding.Flux: return (Hex("5aa9f0"), Hex("0b233b"), Hex("020a14"));
            case SpellBinding.Void: return (Hex("a78bfa"), Hex("1a1030"), Hex("08040f"));
            case SpellBinding.Cindergale: return (Hex("f0803c"), Hex("3b1a0b"), Hex("150601"));
            case SpellBinding.Crystal: return (Hex("f0a9dd"), Hex("3b0b30"), Hex("150111"));
            case SpellBinding.Strand: return (Hex("e3b657"), Hex("3b2c0b"), Hex("140e01"));
            default: return (Hex("d6cbb8"), Hex("2a2622"), Hex("0d0b0a"));
        }
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.gray;

    // ===== WHAT A CARD IS =====

    /// <summary>The Root a card sounds as played by <paramref name="voiceRoot"/> (a caster's card with no Root of its own takes its voice's).</summary>
    public static SpellBinding Root(CombatCard card, SpellBinding voiceRoot = SpellBinding.Unattuned) =>
        card == null ? SpellBinding.Unattuned : card.binding != SpellBinding.Unattuned ? card.binding : Magic(card) ? voiceRoot : SpellBinding.Unattuned;

    public static bool Magic(CombatCard card) => card != null && (card.kind == CardKind.Spell || card.kind == CardKind.Instinct && card.effects.Any(e => e != null && e.op == CardOp.Spell));

    public static ChordTier Tier(CombatCard card) => (ChordTier)Math.Min(3, card?.minors?.Count ?? 0);

    /// <summary>The website's tier name: "Unison", "Dyad Chord", "Triad Chord", "Tetrad Chord".</summary>
    public static string TierName(ChordTier tier) => tier == ChordTier.Unison ? "Unison" : $"{tier} Chord";

    /// <summary>The rank in the corner: "Minor Unison", "Major Dyad Chord" for spells; "Steel", "Order", "Instinct" for the rest.</summary>
    public static string Rank(CombatCard card, bool major)
    {
        if (card == null) return string.Empty;
        switch (card.kind)
        {
            case CardKind.Spell: return $"{(major || card.weight == NoteWeight.Major ? "Major" : "Minor")} {TierName(Tier(card))}";
            case CardKind.Command: return "Order";
            case CardKind.Instinct: return Magic(card) ? $"Instinct · {TierName(Tier(card))}" : "Instinct";
            default: return "Steel";
        }
    }

    /// <summary>The suit line: "Cindergale • Strand" (the Root, then each Minor Note), or "Steel".</summary>
    public static string SuitLine(CombatCard card, SpellBinding voiceRoot = SpellBinding.Unattuned)
    {
        var root = Root(card, voiceRoot);
        if (root == SpellBinding.Unattuned) return card != null && Magic(card) ? "Its voice's own binding" : "Steel";
        return string.Join(" • ", new[] { root }.Concat(card.minors ?? new List<SpellBinding>()).Distinct().Select(HarmonicCircle.Name));
    }

    /// <summary>The practice line (the Registers' name), or the website's "Unclassified" for a spell without one; steel says what it is.</summary>
    public static string Practice(CombatCard card)
    {
        if (card == null) return string.Empty;
        if (!string.IsNullOrEmpty(card.practice)) return card.practice;
        switch (card.kind)
        {
            case CardKind.Spell: return "Unclassified";
            case CardKind.Command: return "The Battle Conductor's order";
            case CardKind.Instinct: return Magic(card) ? "Coherence-Binding Tissue" : "Instinct";
            default: return card.voice == CardVoice.Back ? "Missile drill" : "Drill and steel";
        }
    }

    // ===== THE FIVE PURPOSES (the Spell Builder's words) =====

    public static string Name(SpellPurpose p) => p.ToString();

    /// <summary>"The Lead", "The Counterpoint", "The Measure", "The Texture", "The Ornamentation".</summary>
    public static string Role(SpellPurpose p)
    {
        switch (p)
        {
            case SpellPurpose.Offensive: return "The Lead";
            case SpellPurpose.Defensive: return "The Counterpoint";
            case SpellPurpose.Setup: return "The Measure";
            case SpellPurpose.Utility: return "The Texture";
            default: return "The Ornamentation";
        }
    }

    public static string What(SpellPurpose p)
    {
        switch (p)
        {
            case SpellPurpose.Offensive: return "Destabilise the target's frequency";
            case SpellPurpose.Defensive: return "Preserve your own frequency";
            case SpellPurpose.Setup: return "Prepare latent harmonics";
            case SpellPurpose.Utility: return "Manipulate the environment or the state of matter";
            default: return "Alter an effect already active";
        }
    }

    public static string Gist(SpellPurpose p)
    {
        switch (p)
        {
            case SpellPurpose.Offensive: return "Force sent outward to break something. A blast, a piercing bolt, a cutting jet, or a discordant note that shatters an enemy's composure from the inside.";
            case SpellPurpose.Defensive: return "A shield, a ward, a barrier. It answers an incoming force with its exact opposite, so nothing gets through and nothing corrupts what stands behind it.";
            case SpellPurpose.Setup: return "Groundwork laid before anything burns. Traps, glyphs carved into the ground, anchors and open channels that hold power without releasing any of it yet.";
            case SpellPurpose.Utility: return "Everything a drawn sword cannot answer: illusions, spatial traversal, matter repair and mending, stealth, communication, and craft.";
            default: return "It carries no power of its own. It takes hold of a spell already in the air to speed it up, slow it down, redirect it, silence it, or lay another layer over it.";
        }
    }

    /// <summary>"Creating an effect" (Offensive, Defensive, Utility), "Preparing the environment" (Setup), "Acting on an effect already sounding" (Modulation).</summary>
    public static string Group(SpellPurpose p) =>
        p == SpellPurpose.Setup ? "Preparing the environment" : p == SpellPurpose.Modulation ? "Acting on an effect already sounding" : "Creating an effect";

    /// <summary>The thread each Purpose is traditionally attuned to (its seat on the Pentatonic circle).</summary>
    public static SpellBinding PurposeBinding(SpellPurpose p)
    {
        switch (p)
        {
            case SpellPurpose.Offensive: return SpellBinding.Cindergale;
            case SpellPurpose.Defensive: return SpellBinding.Crystal;
            case SpellPurpose.Setup: return SpellBinding.Resonance;
            case SpellPurpose.Utility: return SpellBinding.Strand;
            default: return SpellBinding.Flux;
        }
    }

    /// <summary>What a Symphony holds, by purpose: "10 Offensive, 10 Defensive, 5 Utility, 1 Setup".</summary>
    public static string Mix(IEnumerable<CombatCard> cards) =>
        string.Join(", ", (cards ?? Enumerable.Empty<CombatCard>()).Where(c => c != null).GroupBy(c => c.purpose).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));

    // ===== THE BACK =====

    /// <summary>
    /// The card's back (the website's "The reading"): what it does in numbers, what it costs and who can sound it, where it
    /// is at home, the image the weaver holds, and the whole of its description. Plain text with TMP colour tags only.
    /// </summary>
    public static string Reading(CombatCard card, SymphonyTuning tuning = null)
    {
        if (card == null) return string.Empty;
        tuning = tuning ?? SymphonyTuning.Default;
        var text = new StringBuilder();
        string effects = card.noteRole == BattleNoteRole.Minor ? SymphonyText.Modifier(card.chord) : string.Join("; ", card.effects.Where(e => e != null).Select(SymphonyText.Effect));
        text.AppendLine($"<b>What it does</b>: {effects}.");
        if (card.reaction != null) text.AppendLine($"<b>Reaction</b>: {(card.reaction.enemyTrigger ? "enemy " : "allied ")}{card.reaction.trigger}; {card.reaction.uses} prepared response(s), lasting {card.reaction.durationBeats} Beats.");
        if (card.noteRole == BattleNoteRole.Minor) text.AppendLine("<b>Minor Note</b>: commit with one Core; this card cannot resolve alone.");
        if (card.kind == CardKind.Spell || card.weaving) text.AppendLine("<b>Holding</b>: base 4 Beats, raised by support and Tuning, reduced by Interference; exceeding Hold collapses the entire working.");
        if (card.abjuredOnlyBy.Count > 0) text.AppendLine($"<b>Required counter</b>: {string.Join(", ", card.abjuredOnlyBy)}.");
        if (card.minimumWardBeats > 0) text.AppendLine($"<b>Early preparation</b>: the matching Ward must stand for {card.minimumWardBeats} Beats before impact.");
        var cost = new List<string> { $"{card.TemporalBeats} Beat{(card.TemporalBeats == 1 ? "" : "s")}" };
        if (card.windUp > card.TemporalBeats) cost.Add($"{card.windUp} Beat Wind-Up");
        if (card.channeling) cost.Add("channeling");
        if (card.kind == CardKind.Spell)
        {
            cost.Add("paid in Composure (Essence Sacrifice)");
            float flicker = card.flicker >= 0f ? card.flicker : card.weight == NoteWeight.Major ? tuning.majorFlicker : tuning.minorFlicker;
            cost.Add($"flickers {flicker:P0} of the time in Age 0");
        }
        if (card.exhaust) cost.Add("once a battle");
        text.AppendLine($"<b>What it costs</b>: {string.Join(", ", cost)}.");
        text.AppendLine($"<b>Who sounds it</b>: {Voice(card.voice)}.");
        string home = Home(card);
        if (home != null) text.AppendLine($"<b>At home</b>: {home}.");
        if (card.purpose == SpellPurpose.Setup) text.AppendLine($"<b>Groundwork</b>: every Offensive card after it this measure lands {tuning.groundwork:P0} harder (up to {tuning.groundworkCap}).");
        if (!string.IsNullOrEmpty(card.vision)) text.AppendLine($"<b>The image</b>: <i>{card.vision}</i>");
        if (!string.IsNullOrEmpty(card.description) && card.description != card.catchline) text.AppendLine(card.description);
        if (!string.IsNullOrEmpty(card.canonAnchor)) text.AppendLine($"<size=85%>{card.canonAnchor}</size>");
        return text.ToString().TrimEnd();
    }

    private static string Voice(CardVoice v)
    {
        switch (v)
        {
            case CardVoice.Front: return "a section in the front lane";
            case CardVoice.Back: return "a section in the back lane";
            case CardVoice.Caster: return "a caster, in its own binding";
            case CardVoice.Commander: return "the Battle Conductor";
            default: return "anyone in the formation";
        }
    }

    /// <summary>Where it lands harder ("x1.25 on forest or hills; x1.4 from cover, only there"), or null.</summary>
    public static string Home(CombatCard card)
    {
        var parts = new List<string>();
        if (card.grounds.Count > 0) parts.Add($"x{card.groundBonus:0.##} on {string.Join(" or ", card.grounds.Select(g => g.ToString().ToLowerInvariant()))} ground");
        if (card.condition != CardCondition.None)
        {
            var flags = Enum.GetValues(typeof(CardCondition)).Cast<CardCondition>().Where(c => c != CardCondition.None && (card.condition & c) != 0).Select(Condition);
            parts.Add($"x{card.conditionBonus:0.##} {string.Join(" and ", flags)}");
        }
        if (parts.Count == 0) return null;
        return string.Join("; ", parts) + (card.requires ? ", and only there" : string.Empty);
    }

    public static string Condition(CardCondition c)
    {
        switch (c)
        {
            case CardCondition.Concealed: return "from cover";
            case CardCondition.HighGround: return "from higher ground";
            case CardCondition.RiverCrossing: return "while the enemy wades a river";
            case CardCondition.Settlement: return "holding a settlement";
            case CardCondition.Defending: return "defending";
            case CardCondition.Attacking: return "attacking";
            case CardCondition.Opening: return "in the first measure";
            case CardCondition.Hunting: return "against creatures";
            case CardCondition.LandElement: return "where the land sings its binding";
            case CardCondition.Sacred: return "on Sacred ground";
            case CardCondition.Leyline: return "over a leyline";
            default: return c.ToString();
        }
    }
}
