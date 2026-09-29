using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The traditions' numbers and authored definitions (<see cref="TraditionRules"/>). The vault names none of these
/// numbers: the lifecycle, thresholds and benefits are new game rules (Canon Gaps "Living traditions"); each
/// definition carries its own canon label and the note it rests on.
/// </summary>
[Serializable]
public class TraditionTuning
{
    [Tooltip("Distinct Sevenths a tradition must be practised in before it is a custom (repetition, not one burst).")]
    public int establishSevenths = 3;
    [Tooltip("Occasions all told before it is a custom.")]
    public int establishOccasions = 3;
    [Tooltip("Sevenths without practice before a tradition lies dormant (two Phases).")]
    public int lapseSevenths = 42;
    [Tooltip("A tradition preserved locally lapses this many times more slowly.")]
    public float preservedLapseFactor = 2f;
    [Tooltip("Distinct Sevenths of practice that revive a dormant tradition.")]
    public int reviveSevenths = 2;
    [Tooltip("Share of a tradition's momentum that fades each Seventh.")]
    public float momentumDecay = 0.15f;
    [Tooltip("Most credit one tradition takes in one Seventh (holding a rite ten times is not ten customs).")]
    public float maxCreditPerSeventh = 2f;
    [Tooltip("Occasions kept in a tradition's history (older ones remain in its tallies).")]
    public int historyKept = 24;
    [Tooltip("Legends remembered as a tradition's bearers.")]
    public int bearersKept = 7;
    [Tooltip("Most traditions whose benefits apply at once (the strongest by momentum): several customs never multiply every bonus.")]
    public int maxActiveBenefits = 4;
    [Tooltip("Sevenths a deferred decision waits before it is offered again.")]
    public int deferSevenths = 21;
    [Tooltip("Share of a rite's weight (CultureLifeTuning.livedPerRite) each lived tradition leans the culture toward its family every Seventh.")]
    public float livedShare = 0.25f;

    public List<TraditionDefinition> definitions = new List<TraditionDefinition>
    {
        new TraditionDefinition
        {
            id = "hearth-tales", name = "Tales at the Hearth", family = "Auric", canon = CanonStatus.NewGameRule,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Civic.md",
            description = "When the lamps are lit the old ones tell what the world was before the golden dust, and the children ask for the same tale again. Nothing is spent but the evening.",
            triggers = { new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "tales") },
            practiced = { new TraditionEffect(GameEffectType.ResourceModifier, 3f, ModifierType.Percentage, "Research") },
            recognized = { new TraditionEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "aureus") },
            recognitionUnity = 25f,
        },
        new TraditionDefinition
        {
            id = "evening-song", name = "The Evening Song", family = "Weaver", canon = CanonStatus.CanonSupported,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Waltz Pillar.md",
            description = "After the day's work the people sing what they remember: a few voices, then everyone. \"A Civilization is the song it chooses to sing\" (Civic).",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "song"),
                // A settlement's own evening song (a local gathering, T02).
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "local:evening-song"),
                new TraditionTrigger(CulturalOccurrenceKind.Festival, CultureEntityKind.None, null, 0.5f),
                new TraditionTrigger(CulturalOccurrenceKind.Performance, CultureEntityKind.None, null),
            },
            practiced = { new TraditionEffect(GameEffectType.MaxMoraleModifier, 1f, ModifierType.Add) },
            recognized = { new TraditionEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "waltz") },
            recognitionUnity = 30f,
        },
        new TraditionDefinition
        {
            id = "naming-of-the-lost", name = "The Naming of the Lost", family = "Esoteric", canon = CanonStatus.CanonSupported,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md", technology = "Rites of Harvest",
            description = "The names of those the Hunger took are spoken aloud, one by one, so that no one is ground into the dust unremembered. It asks for no feast and no high spirits.",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "remembrance"),
                new TraditionTrigger(CulturalOccurrenceKind.Memorial, CultureEntityKind.None, null),
            },
            practiced = { new TraditionEffect(GameEffectType.MoraleModifier, 1f, ModifierType.Add) },
            recognized = { new TraditionEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "chorus") },
            recognitionUnity = 30f,
        },
        new TraditionDefinition
        {
            id = "ash-loaf-table", name = "The Ash-Loaf Table", family = "Weaver", food = true, canon = CanonStatus.CanonSupported,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md", technology = "Echoes of Hunger",
            description = "Peachless bread baked for those who starved and set out for whoever comes: food as memorial and refusal (the mourning bakeries after the Hunger; Enclave's bakery born of a wound).",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Production, CultureEntityKind.Recipe, "ash-loaf", 0.5f),
                new TraditionTrigger(CulturalOccurrenceKind.Hospitality, CultureEntityKind.Recipe, "ash-loaf"),
                new TraditionTrigger(CulturalOccurrenceKind.Memorial, CultureEntityKind.Recipe, "ash-loaf"),
            },
            practiced = { new TraditionEffect(GameEffectType.ResourceModifier, 5f, ModifierType.Percentage, "Ash-Loaf") },
            recognized = { new TraditionEffect(GameEffectType.MaxMoraleModifier, 2f, ModifierType.Add) },
            recognitionUnity = 30f,
        },
        new TraditionDefinition
        {
            id = TraditionRules.NationalTable, name = "The National Table", food = true, canon = CanonStatus.NewGameRule, local = false, perSubject = true, linkedOnly = true,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Civic.md",
            description = "A food the people embraced as theirs, kept at the table. Its output bonus is the national food's own; this record is where it came from and whether it is still eaten.",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Consumption, CultureEntityKind.Resource, null),
                new TraditionTrigger(CulturalOccurrenceKind.Hospitality, CultureEntityKind.Resource, null),
            },
            recognitionUnity = 0f,
        },
        // ----- Hybrids (T07): made only when the people choose to blend two traditions that met (SyncretismTuning.hybrids) -----
        new TraditionDefinition
        {
            id = "loaf-of-names", name = "The Loaf of Names", family = "Esoteric", food = true, canon = CanonStatus.CanonSupported, linkedOnly = true,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md", technology = "Echoes of Hunger",
            description = "Each Ash-Loaf is scored with a name before it goes into the oven; broken at the table, the name is spoken as it is shared (the Ash-Loaf Table and the Naming of the Lost, kept as one).",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Production, CultureEntityKind.Recipe, "ash-loaf", 0.5f),
                new TraditionTrigger(CulturalOccurrenceKind.Hospitality, CultureEntityKind.Recipe, "ash-loaf"),
                new TraditionTrigger(CulturalOccurrenceKind.Memorial, CultureEntityKind.None, null),
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "remembrance"),
            },
            practiced = { new TraditionEffect(GameEffectType.MoraleModifier, 1f, ModifierType.Add) },
            recognized = { new TraditionEffect(GameEffectType.MaxMoraleModifier, 2f, ModifierType.Add) },
            recognitionUnity = 30f,
        },
        new TraditionDefinition
        {
            id = "crossing-chorus", name = "The Crossing Chorus", family = "Weaver", canon = CanonStatus.NewGameRule, linkedOnly = true,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Waltz Pillar.md",
            description = "The fords' call-and-answer taken up by the whole evening: one bank calls, the other answers, and everyone between sings the crossing (the Evening Song and the Crossing Songs, blended).",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "local:crossing-songs"),
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "local:evening-song"),
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "song"),
                new TraditionTrigger(CulturalOccurrenceKind.Performance, CultureEntityKind.None, null),
                new TraditionTrigger(CulturalOccurrenceKind.Festival, CultureEntityKind.None, null, 0.5f),
            },
            practiced = { new TraditionEffect(GameEffectType.ResourceModifier, 3f, ModifierType.Percentage, "Unity") },
            recognized = { new TraditionEffect(GameEffectType.MaxMoraleModifier, 1f, ModifierType.Add) },
            recognitionUnity = 30f,
        },
        new TraditionDefinition
        {
            id = "first-fruit-songs", name = "Songs of the First Fruit", family = "Agromagical", canon = CanonStatus.CanonSupported, linkedOnly = true,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            description = "The first golden fruit is named in a song, and the song is sung each evening until the next is picked (the Rite of the First Golden Fruit and the Evening Song, blended).",
            triggers =
            {
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "local:golden-fruit"),
                new TraditionTrigger(CulturalOccurrenceKind.Gathering, CultureEntityKind.Activity, "song"),
                new TraditionTrigger(CulturalOccurrenceKind.Festival, CultureEntityKind.None, null, 0.5f),
            },
            practiced = { new TraditionEffect(GameEffectType.ResourceModifier, 4f, ModifierType.Percentage, "Dried Auric Peaches") },
            recognized = { new TraditionEffect(GameEffectType.MaxMoraleModifier, 1f, ModifierType.Add) },
            recognitionUnity = 25f,
        },
    };

    public TraditionDefinition Definition(string id) =>
        string.IsNullOrEmpty(id) ? null : definitions?.FirstOrDefault(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
}
