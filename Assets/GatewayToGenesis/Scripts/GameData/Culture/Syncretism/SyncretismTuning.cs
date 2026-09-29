using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The numbers of syncretism and ruin inheritance, and the authored hybrids (<see cref="SyncretismRules"/>). None of the
/// numbers is the vault's: they are proposals (Canon Gaps "Syncretism and inheritance"). The vault's own Syncretism
/// (Achievement.md) merges Constellations of the Stellar Legacy; the cultural hybrids here are not that system, and each
/// carries its own canon label. Digestive Rebirth (Achievement.md: "The ruins of the past fuel the roots of the present")
/// is the existing reform and ruin-civic adoption, extended here, not duplicated.
/// </summary>
[Serializable]
public class SyncretismTuning
{
    [Tooltip("Gatherings a settlement's custom needs before it counts as kept long enough to blend (a lived tradition always does).")]
    public int sustainedGatherings = 2;
    [Tooltip("Most times a tradition can be reworked (a hybrid of originals is 1, each renewal for a new Age adds 1).")]
    public int maxDepth = 2;
    [Tooltip("Sevenths before two traditions kept side by side are offered to blend again.")]
    public int reofferSevenths = 63;
    [Tooltip("Extra Unity to let a new form replace a tradition the nation recognised.")]
    public float recognizedPremium = 15f;
    [Tooltip("Share of a reform's shift a guess from a ruin nobody knows gives (the ground only suggests).")]
    public float uncertainShare = 0.5f;
    [Tooltip("Unity to renew an inherited or blended tradition for a new Age.")]
    public float renewUnity = 15f;
    [Tooltip("Exposure a custom taken up from a ruin starts with in the settlement that takes it up (T02: it still has to be gathered for).")]
    public float reviveExposure = 1f;
    [Tooltip("Decisions kept in the record (older ones stay summarised in the heritage).")]
    public int decisionsKept = 60;

    public List<HybridRule> hybrids = new List<HybridRule>
    {
        new HybridRule
        {
            id = "loaf-of-names", parentA = "tradition:ash-loaf-table", parentB = "tradition:naming-of-the-lost", result = "loaf-of-names",
            canon = CanonStatus.CanonSupported, canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            canonNote = "The Inescapable Hunger: mourning bakeries bake Ash-Loaves and Peachless Bread, recipes explicitly dedicated to those who starved. Scoring a name into each loaf when the Ash-Loaf Table and the Naming of the Lost are kept together is the game's adaptation.",
            fromA = "the loaf and the table set for whoever comes", fromB = "the names spoken one by one", adaptUnity = 20f, replaceUnity = 10f,
        },
        new HybridRule
        {
            id = "crossing-chorus", parentA = "tradition:evening-song", parentB = "local:crossing-songs", result = "crossing-chorus",
            canon = CanonStatus.NewGameRule, canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Waltz Pillar.md",
            canonNote = "A new game rule: the vault names no river chorus. The Waltz Pillar is song and shared performance; the Crossing Songs are a game custom (Canon Gaps.md). A communal performance, not a Soul Leitmotif.",
            fromA = "the evening's many voices", fromB = "the fords' call and answer", adaptUnity = 20f, replaceUnity = 10f,
        },
        new HybridRule
        {
            id = "first-fruit-songs", parentA = "local:golden-fruit", parentB = "tradition:evening-song", result = "first-fruit-songs",
            canon = CanonStatus.CanonSupported, canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md", minAge = 0, maxAge = 0,
            canonNote = "The Inescapable Hunger, early Age 0: the first golden fruit of each year becomes the focus of minor rites, named offerings and tiny festivals. Singing the fruit's naming with the evening's songs is the game's adaptation, and belongs to Age 0 only.",
            fromA = "the first fruit named and offered", fromB = "the evening's songs", adaptUnity = 15f, replaceUnity = 8f,
        },
    };

    public HybridRule Hybrid(string id) =>
        string.IsNullOrEmpty(id) ? null : hybrids?.FirstOrDefault(h => h != null && string.Equals(h.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The rule whose result is <paramref name="definition"/>, or null (it is not a hybrid).</summary>
    public HybridRule HybridMaking(string definition) =>
        string.IsNullOrEmpty(definition) ? null : hybrids?.FirstOrDefault(h => h != null && string.Equals(h.result, definition, StringComparison.OrdinalIgnoreCase));
}
