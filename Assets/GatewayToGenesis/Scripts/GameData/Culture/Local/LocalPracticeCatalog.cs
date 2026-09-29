using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A custom a settlement can keep (T02, local cultures): where it can arise on its own, which Ages know it, the way of
/// living it belongs to, and where the vault says so. A settlement whose ground fits one of its <see cref="grounds"/>
/// words can begin it with a gathering; anywhere else it must be learned from a community that keeps it (a road, a
/// cultural party's visit, settlers or founders who carried it).
/// </summary>
[Serializable]
public class LocalPracticeSpec
{
    public string id;
    public string name;
    public string description;
    /// <summary>The way of living it leans toward (an EnclaveFamily name).</summary>
    public string family;
    /// <summary>
    /// Words in a settlement's ground (terrain, biome, landform, "river", "sacred", nearby resource sites) that let it
    /// begin there. Empty: it can begin anywhere.
    /// </summary>
    public List<string> grounds = new List<string>();
    /// <summary>What the ground words mean to the player ("orchards or peach groves nearby").</summary>
    public string groundText;
    /// <summary>The Ages (numbers, 0 = the Age of Desolation) in which it can begin or be learned; -1: no end.</summary>
    public int minAge, maxAge = -1;
    public CanonStatus canon;
    /// <summary>The vault note it comes from (a path under Worldbuilding/), or empty for a new game rule.</summary>
    public string vault;
    /// <summary>What the vault says, in a few words, or what the proposal is.</summary>
    public string canonNote;
    /// <summary>The tradition definition (T01) it is a local form of, when one exists (empty: none).</summary>
    public string tradition;

    public bool OpenIn(int age) => age >= minAge && (maxAge < 0 || age <= maxAge);

    /// <summary>It can begin on ground described by <paramref name="ground"/> (lower-case words).</summary>
    public bool FitsGround(string ground) =>
        grounds == null || grounds.Count == 0 || (!string.IsNullOrEmpty(ground) && grounds.Any(g => !string.IsNullOrEmpty(g) && ground.Contains(g.ToLowerInvariant())));

    public string AgeText => maxAge < 0 ? (minAge <= 0 ? "every Age" : $"from Age {AgeRules.Roman(minAge)}") : $"Ages {(minAge <= 0 ? "0" : AgeRules.Roman(minAge))}-{AgeRules.Roman(maxAge)}";

    public string CanonText => canon == CanonStatus.ExplicitCanon ? "explicit canon" : canon == CanonStatus.CanonSupported ? "canon-supported adaptation" : "new game rule (proposal)";
}

/// <summary>
/// The numbers of local cultures and exchange. None is from the vault: every number is a proposal (Canon Gaps.md,
/// "Local cultures and routes of exchange").
/// </summary>
[Serializable]
public class LocalCultureTuning
{
    // ----- Exposure: how well a community knows a custom it does not keep -----
    /// <summary>Exposure a completed cultural visit gives the host for each custom the party carried.</summary>
    public float visitExposure = 0.6f;
    /// <summary>Exposure a new settlement's founders (settlers, or a hub's people raising a hamlet) give it for each custom they kept at home.</summary>
    public float founderExposure = 0.6f;
    /// <summary>Exposure people arriving from one of your settlements (returning settlers) give the place they join.</summary>
    public float arrivalExposure = 0.3f;
    /// <summary>Exposure an open road gives each Seventh, per custom kept at its other end.</summary>
    public float roadExposurePerSeventh = 0.04f;
    /// <summary>Share of its exposure a custom not kept loses each Seventh without contact.</summary>
    public float exposureFade = 0.02f;
    /// <summary>Exposure needed before a community will try a custom it did not begin (hold a gathering of it).</summary>
    public float tryExposure = 0.3f;

    // ----- Participation: how much the community has been taking part lately -----
    /// <summary>Participation a local gathering gives the custom it held.</summary>
    public float gatheringParticipation = 0.5f;
    /// <summary>Participation a festival gives each custom the host already keeps.</summary>
    public float festivalParticipation = 0.5f;
    /// <summary>Participation a festival gives each custom the visiting party performed (the host watched and joined in).</summary>
    public float performedParticipation = 0.25f;
    /// <summary>Share of its participation a custom loses each Seventh.</summary>
    public float participationFade = 0.1f;

    // ----- Adoption and keeping -----
    /// <summary>A community takes up a custom once its exposure and participation both reach these.</summary>
    public float adoptExposure = 0.6f;
    public float adoptParticipation = 0.5f;
    /// <summary>A kept custom no one has gathered for in this many Sevenths grows quiet (still known, never forgotten).</summary>
    public int quietAfterSevenths = 42;

    // ----- Gatherings -----
    /// <summary>Food value a local gathering draws from the stores.</summary>
    public float gatheringFoodValue = 3f;
    /// <summary>Sevenths before the same settlement gathers again.</summary>
    public int gatheringCooldownSevenths = 7;
    /// <summary>Unity a local gathering gives, and the strain it eases in the settlement.</summary>
    public float gatheringUnity = 2f;
    public float gatheringRelief = 4f;

    // ----- Parties, roads and records -----
    /// <summary>Customs a cultural party can carry at once.</summary>
    public int repertoireSize = 2;
    /// <summary>
    /// A road is interrupted where a threat's source stands on it, or where another authority holds a cell of it; also
    /// where the danger on one of its cells reaches this (1: never by danger alone).
    /// </summary>
    public float roadDangerLimit = 1f;
    /// <summary>Arrival records kept one by one (older ones are summed per settlement).</summary>
    public int arrivalsKept = 40;
    /// <summary>Dedupe keys of applied visits and arrivals kept.</summary>
    public int keysKept = 256;
}

/// <summary>The local customs this build knows (authored in code; each cites its vault note and its Ages).</summary>
public static class LocalPracticeCatalog
{
    public static readonly IReadOnlyList<LocalPracticeSpec> All = new List<LocalPracticeSpec>
    {
        new LocalPracticeSpec
        {
            id = "evening-song", name = "Evening of Song", family = "Weaver", tradition = "evening-song",
            description = "After the day's work the people gather to sing what they remember: a shared pot, a few voices, then everyone.",
            groundText = "anywhere people gather", minAge = 0, canon = CanonStatus.NewGameRule, vault = "Society/Societal Resources/Foundation/Waltz Pillar.md",
            canonNote = "The game's Evening of Song (a proposal already in Canon Gaps.md); the Waltz Pillar is song and shared performance. Its local form is a new game rule.",
        },
        new LocalPracticeSpec
        {
            id = "golden-fruit", name = "Rite of the First Golden Fruit", family = "Agromagical",
            description = "The first golden fruit of the year is named, offered and shared: a tiny festival among the orchards.",
            grounds = { "orchard", "peach" }, groundText = "orchards or peach groves nearby", minAge = 0, maxAge = 0, canon = CanonStatus.CanonSupported,
            vault = "Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            canonNote = "The Inescapable Hunger: the first golden fruit of each year becomes the focus of minor rites, named offerings and tiny festivals. A rite kept by one orchard town rather than all is an adaptation.",
        },
        new LocalPracticeSpec
        {
            id = "moonlit-vigil", name = "Moonlit Vigil", family = "Weaver",
            description = "In a Glimmerfern grove under a full moon, people meet in silence, listening for a matching frequency.",
            grounds = { "glimmerfern", "grove", "glade" }, groundText = "Glimmerfern, groves or glades nearby", minAge = 0, maxAge = 3, canon = CanonStatus.ExplicitCanon,
            vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Moonlit Vigil, a Weaver civic of Ages 0-III, courtship in Glimmerfern groves at the full moon, the folk tradition of the broken world kept by peasants and refugees. Keeping it in one settlement before any civic is an adaptation.",
        },
        new LocalPracticeSpec
        {
            id = "sky-glass-burial", name = "Sky Glass Burial", family = "Esoteric",
            description = "The dead are carried up to the peaks where Sky Glass forms and given to the sky, into the Auroral Ribbons.",
            grounds = { "skyglass", "sky glass", "peak" }, groundText = "peaks or Sky Glass nearby", minAge = 0, maxAge = 3, canon = CanonStatus.ExplicitCanon,
            vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Sky Glass Burials, an Esoteric civic of Ages 0-III, funerary 'sky burying' at high peaks where Sky Glass forms. A mountain settlement's own rite, before the civic, is an adaptation.",
        },
        new LocalPracticeSpec
        {
            id = "crossing-songs", name = "Crossing Songs", family = "Trading",
            description = "Those who work the river keep time with call-and-answer songs at the fords, taught to anyone who crosses with them.",
            grounds = { "river" }, groundText = "on a river", minAge = 0, canon = CanonStatus.NewGameRule, vault = "",
            canonNote = "A new game rule: the vault names no river custom. It is written so a river town has a voice of its own (Canon Gaps.md).",
        },
        new LocalPracticeSpec
        {
            id = "flavor-log", name = "Village Flavor Log", family = "Agromagical",
            description = "Every cook's combinations are written into one shared log: which ingredients hold together, which fall apart.",
            groundText = "anywhere, once the Age allows", minAge = 1, maxAge = 3, canon = CanonStatus.ExplicitCanon,
            vault = "Society/Societal Resources/Foundation/Civic.md",
            canonNote = "Civic.md: Culinary Alchemists (Agromagical, Ages I-III), 'each village maintains a Flavor Log'. A village keeping one before the civic is an adaptation.",
        },
    };

    public static LocalPracticeSpec Find(string id) =>
        string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(p => string.Equals(p.id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string NameOf(string id) => Find(id)?.name ?? id ?? "an unknown custom";
}
