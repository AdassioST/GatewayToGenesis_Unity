using System;
using UnityEngine;

/// <summary>How much of the self a note carries (vault: The Principles of Magic.md): a Minor Note draws on a fleeting
/// feeling and costs little; a Major Note is a core conviction, the Root of solid magic. Saved by index: append only.</summary>
public enum NoteWeight { Minor, Major }

/// <summary>Where a card can be played.</summary>
[Flags]
public enum CardUse { None = 0, Battle = 1, Field = 2, Ceremony = 4 }

/// <summary>A performer's place in a Ceremony's triad (the chord's Root, Third or Fifth).</summary>
public enum CeremonyVoice { None, Root, Third, Fifth }

/// <summary>The two kinds of Grimoire seat: the deck (Symphony of War and the field) and a Ceremony's voices.</summary>
public enum GrimoireSeat { Symphony, Ceremony }

/// <summary>
/// One Symphony Card: a song of Spellweaving written in the vault's spell-crafting form, Root / Harmony / Tempo
/// (vault: Symphony Card.md, The Principles of Magic.md). Act I only knows Unisons played Staccato
/// (Docs/Planning/TECH_TREE_ACT_I.md section 4); the chord and tempo fields are here so later Ages can widen them.
/// Effects are written for the player; the systems that play cards (Symphony of War, the rhythm ritual) are not built.
/// </summary>
[CreateAssetMenu(fileName = "New Symphony Card", menuName = "Game Object/Symphony Card", order = 5)]
public class SymphonyCardData : ScriptableObject
{
    [Tooltip("Stable id (kebab case): saves and conditions name the card by it (\"grimoire:card:igniting-cooking-pot\").")]
    public string id;
    public string cardName;
    [Tooltip("The Root: the binding struck as the spell's primary note.")]
    public SpellBinding binding = SpellBinding.Resonance;
    public NoteWeight weight = NoteWeight.Minor;
    public ChordTier chord = ChordTier.Unison;
    public SpellTempo tempo = SpellTempo.Staccato;
    public CardUse uses = CardUse.Battle | CardUse.Field;
    [Tooltip("Chance a cast flickers and fails (Age 0: unreliable Unisons). Proposal: Minor 0.15, Major 0.35.")]
    [Range(0f, 1f)] public float flickerChance = 0.15f;
    public Sprite icon;
    [TextArea(1, 3)] public string description;
    [TextArea(1, 3)] public string fieldEffect;
    [TextArea(1, 3)] public string battleEffect;
    [Tooltip("The vault note or line the card is drawn from.")]
    [TextArea(1, 2)] public string canonAnchor;

    public string DisplayName => string.IsNullOrWhiteSpace(cardName) ? name : cardName;

    /// <summary>"Minor Unison of Cindergale, Staccato".</summary>
    public string Form => $"{weight} {chord} of {binding}, {tempo}";
}
