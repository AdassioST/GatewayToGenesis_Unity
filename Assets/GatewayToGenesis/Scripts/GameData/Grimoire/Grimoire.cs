using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a Spell Maker card is composed to do. Saved by index: append only.</summary>
public enum SpellIntent { Strike, Ward, Mend, HastenWork, Reveal, Conceal, Calm }

/// <summary>The Grimoire's seats and cards, as the researched technologies grant them.</summary>
public sealed class GrimoireState
{
    public int symphonySeats, ceremonies, wildcards;
    public readonly List<SymphonyCardData> cards = new List<SymphonyCardData>();

    /// <summary>Voice seats: three for each Ceremony.</summary>
    public int CeremonySeats => ceremonies * Grimoire.VoicesPerCeremony;
    public int Seats => symphonySeats + CeremonySeats;
    /// <summary>Cards to seat: the scripted ones owned and the wildcards still to compose.</summary>
    public int Cards => cards.Count + wildcards;
    public bool IsOpen => Seats > 0 || Cards > 0;
    public IEnumerable<SpellBinding> Heard => cards.Where(c => c != null).Select(c => c.binding).Distinct();
}

/// <summary>
/// The Grimoire, with no scene state except <see cref="Current"/> (Docs/Planning/TECH_TREE_ACT_I.md section 4). Seats and
/// cards come only from researched technologies (<see cref="TechUnlockableType.GrimoireSeat"/>,
/// <see cref="TechUnlockableType.SymphonyCard"/>, <see cref="TechUnlockableType.SpellWildcard"/>), so it has nothing
/// of its own to save.
///
/// - Symphony seats are the deck, for battle (Symphony of War) and the field.
/// - Ceremonial Arts (the owner's rule, Sept 29, 2026): three Spellweavers doing different things, united. A Ceremony is
///   the Registers' triad, Resonance + Flux + Strand (vault: The Registers of Magic.md), but each performer plays only a
///   Unison: Resonance at the Root holds the shared frequency, Flux at the Third carries the feeling, Strand at the
///   Fifth repeats and binds. A voice with no card is the people's untrained hum (<see cref="HumStrength"/>).
/// - For now every seat takes a Unison only (Age 0: flickering Staccato, unreliable Unisons, mostly Minor Notes).
/// </summary>
public static class Grimoire
{
    public const int VoicesPerCeremony = 3;
    /// <summary>The largest chord a seat takes for now (the owner: Unisons only, later up to Tetrads).</summary>
    public const ChordTier SeatChordLimit = ChordTier.Unison;
    /// <summary>A voice without a card: the people's untrained hum, a weak Minor voice (proposal).</summary>
    public const float HumStrength = 0.5f;
    /// <summary>Default flicker chances in Age 0 (proposals).</summary>
    public const float MinorFlicker = 0.15f, MajorFlicker = 0.35f;

    // ===== CEREMONIAL ARTS =====

    /// <summary>The binding each voice of a Ceremony carries.</summary>
    public static SpellBinding BindingOf(CeremonyVoice voice) => voice switch
    {
        CeremonyVoice.Root => SpellBinding.Resonance,
        CeremonyVoice.Third => SpellBinding.Flux,
        CeremonyVoice.Fifth => SpellBinding.Strand,
        _ => SpellBinding.Unattuned,
    };

    /// <summary>The voice a binding sings in a Ceremony, or None when it has no place in the chord.</summary>
    public static CeremonyVoice VoiceOf(SpellBinding binding) => binding switch
    {
        SpellBinding.Resonance => CeremonyVoice.Root,
        SpellBinding.Flux => CeremonyVoice.Third,
        SpellBinding.Strand => CeremonyVoice.Fifth,
        _ => CeremonyVoice.None,
    };

    public static readonly IReadOnlyList<CeremonyVoice> Voices = new[] { CeremonyVoice.Root, CeremonyVoice.Third, CeremonyVoice.Fifth };

    /// <summary>
    /// A Ceremony's strength, 0-1: each voice counts 1 with a card (a Major Note counts 1.25) and <see cref="HumStrength"/>
    /// when the people hum it; an empty voice list is the people alone.
    /// </summary>
    public static float CeremonyStrength(IReadOnlyDictionary<CeremonyVoice, SymphonyCardData> voices)
    {
        float total = 0f;
        foreach (var voice in Voices)
        {
            var card = voices != null && voices.TryGetValue(voice, out var c) ? c : null;
            total += card == null ? HumStrength : card.weight == NoteWeight.Major ? 1.25f : 1f;
        }
        return total / (VoicesPerCeremony * 1.25f);
    }

    // ===== SEATING =====

    /// <summary>Why a card cannot take a seat, or null when it can.</summary>
    public static string WhyNotSeat(SymphonyCardData card, GrimoireSeat seat, CeremonyVoice voice, int ageNumber)
    {
        if (card == null) return "No card.";
        if (card.chord > SeatChordLimit) return $"Only Unisons fit a seat for now; {card.DisplayName} is a {card.chord}.";
        if (!AgeMagic.TempoAvailable(card.tempo, ageNumber)) return $"This Age does not know {card.tempo} yet.";
        if (card.weight == NoteWeight.Major && !AgeMagic.MajorNotes(ageNumber) && !AgeCapabilities.IsAvailable(AgeCapabilities.UnisonMajorAwakened, ageNumber))
            return "No one in this Age can sound a Major Note.";
        if (seat == GrimoireSeat.Symphony)
            return (card.uses & (CardUse.Battle | CardUse.Field)) != 0 ? null : $"{card.DisplayName} is not played in battle or the field.";
        if (voice == CeremonyVoice.None) return "Choose a voice of the Ceremony.";
        return VoiceOf(card.binding) == voice ? null : $"The {voice} of a Ceremony is sung in {BindingOf(voice)}; {card.DisplayName} is {card.binding}.";
    }

    // ===== THE SPELL MAKER =====

    // What each binding can be composed to do (its Principle and function: vault Glyphic Heptastave.md, the Auric Heptacode table).
    private static readonly Dictionary<SpellIntent, SpellBinding[]> IntentTable = new Dictionary<SpellIntent, SpellBinding[]>
    {
        [SpellIntent.Strike] = new[] { SpellBinding.Resonance, SpellBinding.Luminance, SpellBinding.Cindergale, SpellBinding.Crystal },
        [SpellIntent.Ward] = new[] { SpellBinding.Crystal, SpellBinding.Void },
        [SpellIntent.Mend] = new[] { SpellBinding.Flux, SpellBinding.Strand },
        [SpellIntent.HastenWork] = new[] { SpellBinding.Resonance, SpellBinding.Luminance, SpellBinding.Flux, SpellBinding.Cindergale },
        [SpellIntent.Reveal] = new[] { SpellBinding.Luminance, SpellBinding.Strand },
        [SpellIntent.Conceal] = new[] { SpellBinding.Void },
        [SpellIntent.Calm] = new[] { SpellBinding.Resonance, SpellBinding.Flux, SpellBinding.Void, SpellBinding.Strand },
    };

    public static bool Allows(SpellBinding binding, SpellIntent intent) => IntentTable.TryGetValue(intent, out var bindings) && bindings.Contains(binding);

    public static IEnumerable<SpellIntent> IntentsOf(SpellBinding binding) => IntentTable.Where(e => e.Value.Contains(binding)).Select(e => e.Key);

    /// <summary>
    /// Why a wildcard cannot be composed this way, or null when it can. The Root must be a binding the civilization has
    /// heard (<paramref name="heard"/>: its scripted cards' bindings and its seated legends' Soul Leitmotifs); a Major
    /// Note needs an Awakened Legend; the tempo and the intent must be ones the Age and the binding allow.
    /// </summary>
    public static string WhyNotCompose(SpellBinding binding, NoteWeight weight, SpellTempo tempo, SpellIntent intent,
        IEnumerable<SpellBinding> heard, int ageNumber, bool awakenedLegend)
    {
        if (binding == SpellBinding.Unattuned) return "Choose a binding for the Root.";
        if (heard == null || !heard.Contains(binding)) return $"Your people have never heard {binding} sung.";
        if (weight == NoteWeight.Major)
        {
            if (!awakenedLegend) return "Only an Awakened Legend can sound a Major Note.";
            if (!AgeMagic.MajorNotes(ageNumber) && !AgeCapabilities.IsAvailable(AgeCapabilities.UnisonMajorAwakened, ageNumber)) return "No one in this Age can sound a Major Note.";
        }
        if (!AgeMagic.TempoAvailable(tempo, ageNumber)) return $"This Age does not know {tempo} yet.";
        if (!Allows(binding, intent)) return $"{binding} cannot be composed to {Words(intent)}.";
        return null;
    }

    public static string Words(SpellIntent intent) => intent == SpellIntent.HastenWork ? "hasten work" : intent.ToString().ToLowerInvariant();

    // ===== WHAT THE TECHNOLOGIES GRANT =====

    /// <summary>The seats and cards a set of unlockables grants.</summary>
    public static GrimoireState Collect(IEnumerable<TechUnlockable> unlockables)
    {
        var state = new GrimoireState();
        if (unlockables == null) return state;
        foreach (var u in unlockables)
        {
            if (u == null) continue;
            int count = Math.Max(1, (int)Math.Round(u.resourceModifier));
            switch (u.unlockableType)
            {
                case TechUnlockableType.GrimoireSeat:
                    if (u.seat == GrimoireSeat.Ceremony) state.ceremonies += count; else state.symphonySeats += count;
                    break;
                case TechUnlockableType.SymphonyCard:
                    if (u.symphonyCard != null && !state.cards.Contains(u.symphonyCard)) state.cards.Add(u.symphonyCard);
                    break;
                case TechUnlockableType.SpellWildcard:
                    state.wildcards += count;
                    break;
            }
        }
        return state;
    }

    /// <summary>The Grimoire of the researched technologies (empty before the scene has its units).</summary>
    public static GrimoireState Current()
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return new GrimoireState();
        return Collect(GameCatalog.Technologies.All
            .Where(t => t != null && t.gameUnit != null && t.techUnlockables != null && units.IsTechnologyUnlocked(t.gameUnit.name))
            .SelectMany(t => t.techUnlockables));
    }

    /// <summary>
    /// The "grimoire" condition domain: "symphony_seats", "ceremonies", "ceremony_seats", "seats", "cards" (scripted),
    /// "wildcards", or "card:&lt;id&gt;" (1 once owned).
    /// </summary>
    public static float Value(GrimoireState state, string target)
    {
        if (state == null) return 0f;
        string t = (target ?? string.Empty).Trim().ToLowerInvariant();
        if (t.StartsWith("card:"))
        {
            string id = t.Substring(5).Trim();
            return state.cards.Any(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase)) ? 1f : 0f;
        }
        switch (t)
        {
            case "symphony_seats": return state.symphonySeats;
            case "ceremonies": return state.ceremonies;
            case "ceremony_seats": return state.CeremonySeats;
            case "wildcards": return state.wildcards;
            case "cards": return state.cards.Count;
            case "": case "seats": return state.Seats;
            default: return 0f;
        }
    }
}
