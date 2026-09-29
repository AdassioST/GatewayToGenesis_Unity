using System;
using System.Collections.Generic;
using System.Linq;

// The bestiary's shared shape (vault: Arcanorian Ecology.md, "The Nature of a Species"): every creature of Arcanoria, Pure Light or
// not, is sorted by what it eats (diet), how it meets others (stance) and its subgroup, after the owner's earlier
// design, AECOR (2021). None of this applies to the Atonalis: they feed on Consciousness, not in food webs, and keep
// their own scale (Nascent to Primal Discordia) and Paths as threats (ThreatSpec).
//
// A species' *nature* (diet and subgroup) is fixed. How it acts toward a civilization, which changes with how it is
// treated, is its *behavior* (vault: Arcanorian Ecology.md, "Behavior: The Memory of a Lineage"; WorldBehavior).

/// <summary>What a creature eats (AECOR: herbivores 44% of species, carnivores 26%, omnivores 18%, detritivores 12%).</summary>
public enum CreatureDiet { Herbivore, Carnivore, Omnivore, Detritivore }

/// <summary>How a creature meets others: the division above its subgroup (which ones a diet has is fixed).</summary>
public enum CreatureStance { Passive, Neutral, Aggressive, Territorial, Apex }

/// <summary>AECOR's subgroups. A name can serve several diets (a Territorial herbivore, omnivore or detritivore).</summary>
public enum CreatureSubgroup
{
    Frightful, Docile, Venerable, Territorial, Benign, Wrathful, Smart, Erratic,
    Unobtrusive, Jingoistic, Solitary, Social, Isolationist, Marauder, Trapper, Hiding,
}

/// <summary>What a creature does when someone comes near or threatens it.</summary>
public enum ThreatResponse
{
    /// <summary>Flees as soon as it perceives anyone.</summary>
    FleesOnSight,
    /// <summary>Carries on, and flees once threatened.</summary>
    FleesWhenThreatened,
    /// <summary>Hides until the threat is gone.</summary>
    Hides,
    /// <summary>Unaffected by almost anything anyone does.</summary>
    Unmoved,
    /// <summary>Avoids a fight when it can, and outwits what it cannot avoid.</summary>
    AvoidsConflict,
    /// <summary>Indifferent, but defends itself when threatened.</summary>
    DefendsWhenThreatened,
    /// <summary>Attacks whoever enters its territory, and gives up once they leave it.</summary>
    DefendsTerritory,
    /// <summary>A colony that keeps expanding its territory, strong in numbers.</summary>
    Expands,
    /// <summary>Bursts of sudden violence or feigned weakness, with no warning.</summary>
    Unpredictable,
    /// <summary>Attacks anything that crosses its path.</summary>
    AttacksOnSight,
    /// <summary>Hunts, alone or in a group.</summary>
    Hunts,
    /// <summary>Hunts on sight, but announces itself (a constant screeching).</summary>
    HuntsLoudly,
    /// <summary>Lures prey with something beautiful or harmless-looking; you learn you are prey too late.</summary>
    Lures,
    /// <summary>Lets someone come near: it has learned they mean no harm. No nature has it; only behavior reaches it (<see cref="WorldBehavior"/>).</summary>
    Tolerates,
}

public enum CreatureSize { Small, Medium, Large, Gargantuan }

public enum CreatureMating { Monogamous, Polygamous }

/// <summary>How fast a lineage renews itself. Typical: the subgroup's own pace.</summary>
public enum ReproductionPace { Typical, VerySlow, Slow, Medium, Fast }

/// <summary>How a creature holds ground: bound to a den or one place, to a territory, roaming its range, nomadic, or a colony that spreads.</summary>
public enum CreatureRanging { Settled, Territorial, Roaming, Nomadic, Expanding }

/// <summary>
/// Where a creature's Coherence-Binding Tissue lies, the organ that turns its emotion into magic (vault: Pure Light.md,
/// "Examples of Coherence-Binding Tissue"). None: an ordinary animal. Append only (saved in World.asset by index).
/// </summary>
public enum BindingOrgan
{
    None,
    /// <summary>A glowing back, crystal scales: armored CBT fields (dragons' enchanted scales).</summary>
    Hide,
    /// <summary>Wing membranes that metabolize ambient magic and radiate light and glamour (moths).</summary>
    Wings,
    /// <summary>A living resonant chamber that encodes Frequency Harmonics into song (a Resonance Box, a chorus).</summary>
    Voice,
    /// <summary>Glandular CBT releasing resonance-charged pheromones or roars.</summary>
    Gland,
    /// <summary>Aquatic CBT in fin rays or swim bladders, using pressure instead of airborne sound.</summary>
    Fins,
    /// <summary>CBT threaded through the whole body (slimes, predatory blooms): no structural buffer.</summary>
    Matrix,
}

/// <summary>
/// The harmonic climate a lineage lives in (vault: Arcanorian Ecology.md, "The Nature of a Species"). Pure Light species need Coherence by their Pure
/// Light share (<see cref="WorldEcology.Quality"/>); where they find it is their niche. Append only.
/// </summary>
public enum HarmonicNiche
{
    /// <summary>Needs Coherence where it lives, like most Pure Light lineages.</summary>
    Coherent,
    /// <summary>Feeds where the Loom is torn: Dissonance meets its need, and Vibrational Fallout does not harm it (Dead-zone lineages).</summary>
    Discordant,
    /// <summary>Bound to the leylines and silver water: it thrives only on or beside them, and drinks the Lunehymn there.</summary>
    Leyline,
}

/// <summary>One of AECOR's subgroups under one diet, with its typical traits (a species may differ from them).</summary>
public sealed class SubgroupProfile
{
    public CreatureDiet diet;
    public CreatureStance stance;
    public CreatureSubgroup subgroup;
    public ThreatResponse response;
    public CreatureSize[] sizes;
    public int groupMin, groupMax;
    public CreatureMating mating;
    public ReproductionPace pace;
    public CreatureRanging ranging;
    /// <summary>One sentence for the card.</summary>
    public string summary;

    public string Name => CreatureTaxonomy.Name(diet, subgroup);
}

/// <summary>
/// AECOR's creature taxonomy, all 21 diet-subgroup pairs, with no scene state (tested in <c>BestiaryTests</c>). Traits
/// AECOR states are kept as written; the ones it leaves open are proposals and listed in Canon Gaps.md.
/// </summary>
public static class CreatureTaxonomy
{
    private static SubgroupProfile P(CreatureDiet diet, CreatureStance stance, CreatureSubgroup subgroup, ThreatResponse response,
        CreatureSize[] sizes, int groupMin, int groupMax, CreatureMating mating, ReproductionPace pace, CreatureRanging ranging, string summary) =>
        new SubgroupProfile { diet = diet, stance = stance, subgroup = subgroup, response = response, sizes = sizes, groupMin = groupMin,
            groupMax = groupMax, mating = mating, pace = pace, ranging = ranging, summary = summary };

    private static readonly CreatureSize[] Small = { CreatureSize.Small }, SmallMedium = { CreatureSize.Small, CreatureSize.Medium },
        Medium = { CreatureSize.Medium }, MediumLarge = { CreatureSize.Medium, CreatureSize.Large }, Large = { CreatureSize.Large },
        LargeGargantuan = { CreatureSize.Large, CreatureSize.Gargantuan }, Gargantuan = { CreatureSize.Gargantuan };

    public static readonly IReadOnlyList<SubgroupProfile> Profiles = new List<SubgroupProfile>
    {
        // Herbivores: passive ones flee, aggressive ones answer force with force.
        P(CreatureDiet.Herbivore, CreatureStance.Passive, CreatureSubgroup.Frightful, ThreatResponse.FleesOnSight, SmallMedium, 15, 20,
            CreatureMating.Polygamous, ReproductionPace.Medium, CreatureRanging.Roaming, "Flees at first sight: the hardest herbivores to come near."),
        P(CreatureDiet.Herbivore, CreatureStance.Passive, CreatureSubgroup.Docile, ThreatResponse.FleesWhenThreatened, MediumLarge, 8, 12,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Roaming, "Goes about its day until threatened, then flees."),
        P(CreatureDiet.Herbivore, CreatureStance.Passive, CreatureSubgroup.Venerable, ThreatResponse.Unmoved, Gargantuan, 1, 2,
            CreatureMating.Monogamous, ReproductionPace.VerySlow, CreatureRanging.Roaming, "A solitary giant almost nothing can disturb; it breeds very slowly."),
        P(CreatureDiet.Herbivore, CreatureStance.Aggressive, CreatureSubgroup.Territorial, ThreatResponse.DefendsTerritory, MediumLarge, 6, 15,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Territorial, "Peaceful until someone enters its territory, which it defends for the weaker creatures sheltering with it."),
        P(CreatureDiet.Herbivore, CreatureStance.Aggressive, CreatureSubgroup.Benign, ThreatResponse.DefendsWhenThreatened, Medium, 5, 15,
            CreatureMating.Polygamous, ReproductionPace.Medium, CreatureRanging.Roaming, "Calm, with a defence it turns on anyone who threatens it; it can live where the land is fouled."),
        P(CreatureDiet.Herbivore, CreatureStance.Aggressive, CreatureSubgroup.Wrathful, ThreatResponse.AttacksOnSight, MediumLarge, 10, 30,
            CreatureMating.Polygamous, ReproductionPace.Fast, CreatureRanging.Nomadic, "Charges anything in its path; nomadic, quick to breed, and it raids others' food."),

        // Omnivores: clever aggressive ones, and neutral ones that only defend.
        P(CreatureDiet.Omnivore, CreatureStance.Aggressive, CreatureSubgroup.Smart, ThreatResponse.AvoidsConflict, SmallMedium, 1, 1,
            CreatureMating.Polygamous, ReproductionPace.Slow, CreatureRanging.Roaming, "A solitary survivor that avoids fights and outwits what it cannot avoid."),
        P(CreatureDiet.Omnivore, CreatureStance.Aggressive, CreatureSubgroup.Erratic, ThreatResponse.Unpredictable, SmallMedium, 3, 9,
            CreatureMating.Polygamous, ReproductionPace.Medium, CreatureRanging.Nomadic, "A ticking threat: sudden attacks, feigned injuries, and the nerve to face apex predators."),
        P(CreatureDiet.Omnivore, CreatureStance.Neutral, CreatureSubgroup.Territorial, ThreatResponse.DefendsTerritory, MediumLarge, 5, 15,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Territorial, "A close-knit herd that ignores others until they cross its borders or harm one of its own."),
        P(CreatureDiet.Omnivore, CreatureStance.Neutral, CreatureSubgroup.Benign, ThreatResponse.DefendsWhenThreatened, SmallMedium, 2, 4,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Nomadic, "Carries on regardless of others, and defends itself when it must."),

        // Carnivores: aggressive, territorial, and the apex predators.
        P(CreatureDiet.Carnivore, CreatureStance.Aggressive, CreatureSubgroup.Unobtrusive, ThreatResponse.AttacksOnSight, MediumLarge, 1, 1,
            CreatureMating.Monogamous, ReproductionPace.Slow, CreatureRanging.Settled, "Hidden and fearful, it strikes the moment it senses a threat."),
        P(CreatureDiet.Carnivore, CreatureStance.Aggressive, CreatureSubgroup.Jingoistic, ThreatResponse.Expands, Small, 50, 500,
            CreatureMating.Polygamous, ReproductionPace.Fast, CreatureRanging.Expanding, "A hive that conquers: weak alone, and its colony keeps pushing its borders out."),
        P(CreatureDiet.Carnivore, CreatureStance.Territorial, CreatureSubgroup.Solitary, ThreatResponse.DefendsTerritory, MediumLarge, 1, 1,
            CreatureMating.Polygamous, ReproductionPace.Slow, CreatureRanging.Settled, "Waits by its den, often behind simple traps, for whatever comes close."),
        P(CreatureDiet.Carnivore, CreatureStance.Territorial, CreatureSubgroup.Social, ThreatResponse.Hunts, Medium, 4, 12,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Nomadic, "A pack that follows its leader and hunts together."),
        P(CreatureDiet.Carnivore, CreatureStance.Territorial, CreatureSubgroup.Isolationist, ThreatResponse.Hunts, SmallMedium, 4, 10,
            CreatureMating.Polygamous, ReproductionPace.Medium, CreatureRanging.Territorial, "A closed group that admits no one, and goes for the injured."),
        P(CreatureDiet.Carnivore, CreatureStance.Apex, CreatureSubgroup.Marauder, ThreatResponse.HuntsLoudly, Large, 1, 1,
            CreatureMating.Polygamous, ReproductionPace.Slow, CreatureRanging.Roaming, "Hunts anyone it notices and plays with its prey, but its screeching gives it away."),
        P(CreatureDiet.Carnivore, CreatureStance.Apex, CreatureSubgroup.Trapper, ThreatResponse.Lures, LargeGargantuan, 1, 1,
            CreatureMating.Polygamous, ReproductionPace.VerySlow, CreatureRanging.Territorial, "Looks beautiful or harmless, and you learn you are the prey too late."),

        // Detritivores: the fewest species, the most individuals.
        P(CreatureDiet.Detritivore, CreatureStance.Passive, CreatureSubgroup.Frightful, ThreatResponse.FleesOnSight, SmallMedium, 1, 5,
            CreatureMating.Polygamous, ReproductionPace.Slow, CreatureRanging.Roaming, "Shy decomposers that flee from anyone at all."),
        P(CreatureDiet.Detritivore, CreatureStance.Passive, CreatureSubgroup.Hiding, ThreatResponse.Hides, SmallMedium, 1, 5,
            CreatureMating.Monogamous, ReproductionPace.Slow, CreatureRanging.Settled, "Hides when threatened and comes back once the danger has passed."),
        P(CreatureDiet.Detritivore, CreatureStance.Neutral, CreatureSubgroup.Territorial, ThreatResponse.DefendsTerritory, Medium, 10, 30,
            CreatureMating.Polygamous, ReproductionPace.Medium, CreatureRanging.Territorial, "Lives its whole life in one colony and defends its ground."),
        P(CreatureDiet.Detritivore, CreatureStance.Neutral, CreatureSubgroup.Docile, ThreatResponse.DefendsWhenThreatened, MediumLarge, 2, 6,
            CreatureMating.Monogamous, ReproductionPace.Medium, CreatureRanging.Roaming, "Indifferent until attacked, then defends itself."),
    };

    /// <summary>The profile of a diet-subgroup pair, or null when AECOR has no such subgroup for that diet.</summary>
    public static SubgroupProfile Profile(CreatureDiet diet, CreatureSubgroup subgroup) =>
        Profiles.FirstOrDefault(p => p.diet == diet && p.subgroup == subgroup);

    public static bool IsValid(CreatureDiet diet, CreatureSubgroup subgroup) => Profile(diet, subgroup) != null;

    /// <summary>The subgroups a diet has, in AECOR's order.</summary>
    public static IEnumerable<CreatureSubgroup> SubgroupsOf(CreatureDiet diet) => Profiles.Where(p => p.diet == diet).Select(p => p.subgroup);

    /// <summary>"Frightful Herbivore", "Marauder Apex Predator".</summary>
    public static string Name(CreatureDiet diet, CreatureSubgroup subgroup) =>
        subgroup == CreatureSubgroup.Marauder || subgroup == CreatureSubgroup.Trapper ? $"{subgroup} Apex Predator" : $"{subgroup} {diet}";

    public static string SizeWord(CreatureSize size) => size.ToString().ToLowerInvariant();

    public static string PaceWord(ReproductionPace pace) => pace == ReproductionPace.VerySlow ? "very slow" : pace.ToString().ToLowerInvariant();

    /// <summary>The group a species lives in: its own numbers, or its subgroup's when it leaves them at 0.</summary>
    public static (int min, int max) GroupOf(SpeciesSpec species)
    {
        var profile = species == null ? null : Profile(species.diet, species.subgroup);
        if (profile == null) return (1, 1);
        return species.groupMax > 0 ? (Math.Max(1, species.groupMin), species.groupMax) : (profile.groupMin, profile.groupMax);
    }

    /// <summary>How fast a species breeds: its own pace, or its subgroup's when Typical.</summary>
    public static ReproductionPace PaceOf(SpeciesSpec species)
    {
        if (species == null) return ReproductionPace.Medium;
        if (species.reproduction != ReproductionPace.Typical) return species.reproduction;
        return Profile(species.diet, species.subgroup)?.pace ?? ReproductionPace.Medium;
    }

    /// <summary>"alone", "in pairs", "in groups of 8-12".</summary>
    public static string GroupWords(int min, int max) =>
        max <= 1 ? "alone" : min == max ? $"in groups of {max}" : max == 2 ? "alone or in pairs" : $"in groups of {min}-{max}";

    /// <summary>Below this Auric Structure share a species is a Pure Light being (vault: Pure Light.md, "less than 65%").</summary>
    public const float PureLightBeingBelow = 0.65f;

    /// <summary>
    /// A Pure Light being: less than 65% Auric Structure, or a dedicated Coherence-Binding Tissue organ (vault: Pure
    /// Light.md). These attune at the Ritual Seventh (<see cref="WorldRhythm.Attune"/>) and can catch resonance plagues.
    /// </summary>
    public static bool IsPureLightBeing(SpeciesSpec species) =>
        species != null && (species.structure < PureLightBeingBelow || species.binding != BindingOrgan.None);

    /// <summary>"its hide glows with Coherence-Binding Tissue", or null for an ordinary animal.</summary>
    public static string BindingWords(BindingOrgan organ)
    {
        switch (organ)
        {
            case BindingOrgan.Hide: return "its hide or scales carry Coherence-Binding Tissue, an armored field of light";
            case BindingOrgan.Wings: return "its wings carry Coherence-Binding Tissue: they drink ambient magic and radiate light and glamour";
            case BindingOrgan.Voice: return "its song carries Coherence-Binding Tissue: a living resonant chamber that sings magic";
            case BindingOrgan.Gland: return "glands of Coherence-Binding Tissue charge its scent and roars with resonance";
            case BindingOrgan.Fins: return "Coherence-Binding Tissue in its fins turns the water's pressure into magic";
            case BindingOrgan.Matrix: return "Coherence-Binding Tissue threads its whole body, with no structure to buffer it";
            default: return null;
        }
    }

    /// <summary>"it feeds where the Loom is torn", or null for the common (Coherent) niche.</summary>
    public static string NicheWords(HarmonicNiche niche)
    {
        switch (niche)
        {
            case HarmonicNiche.Discordant: return "it feeds where the Loom is torn: Dissonance sustains it and Vibrational Fallout does not harm it";
            case HarmonicNiche.Leyline: return "it is bound to the leylines and silver water, and fails away from them";
            default: return null;
        }
    }

    /// <summary>The Echo a species breeds in (1-4), or 0 when it follows its diet's rhythm.</summary>
    public static int BreedingEchoOf(SpeciesSpec species) =>
        species == null || species.breedingEcho < 1 || species.breedingEcho > TimeSystemLogic.EchoesPerCycle ? 0 : species.breedingEcho;
}

/// <summary>
/// What coming to know a species gives the civilization once, at one level of knowledge (vault: Arcanorian Ecology.md, "The Nature of a Species"):
/// Era Score for the Age and resources (Research for what was learned). The later technology templates will read this
/// too (biomimicry, deferred). Paid by <see cref="SpeciesLoreKeeper"/>.
/// </summary>
[Serializable]
public class DiscoveryReward
{
    [UnityEngine.Tooltip("The level of knowledge that pays it: Identified (2), Observed (3) or Understood (4).")]
    public SpeciesLevel level = SpeciesLevel.Identified;
    public int eraScore;
    [UnityEngine.Tooltip("A resource given once (empty: none).")]
    public string resource;
    public float amount;
}

/// <summary>
/// A species of Arcanoria's bestiary (<see cref="CreatureTaxonomy"/>). A resource site that is a creature's den or
/// herd names its species (<see cref="ResourceSiteSpec.species"/>). Every value is a proposal (Canon Gaps).
/// </summary>
[Serializable]
public class SpeciesSpec
{
    public string id;
    public string name;
    [UnityEngine.TextArea(1, 3)] public string description;
    public CreatureDiet diet;
    [UnityEngine.Tooltip("Its AECOR subgroup; the diet decides which ones exist (CreatureTaxonomy).")]
    public CreatureSubgroup subgroup;
    public CreatureSize size = CreatureSize.Medium;
    [UnityEngine.Tooltip("Group size, from min to max (0: its subgroup's typical group).")]
    public int groupMin, groupMax;
    [UnityEngine.Tooltip("How fast it breeds (Typical: its subgroup's pace).")]
    public ReproductionPace reproduction;
    [UnityEngine.Tooltip("Its Auric Structure share (0-1); the rest is Pure Light (vault: Pure Light.md). Ordinary animals are high: rats 0.9, insects 0.95; dragons 0.55, slimes 0.25, sprites 0.1.")]
    [UnityEngine.Range(0f, 1f)] public float structure = 0.9f;
    [UnityEngine.Tooltip("Species it preys on (ids). A predator's land holds less where they are scarce, and it takes a share of them each Echo (WorldEcology).")]
    public List<string> prey = new List<string>();
    [UnityEngine.Tooltip("Eleos Blooms it is drawn to (site ids, or a niche: listener, healer, predator; \"bloom\" for any): pollinators, grazers and the beetles of grief. Its Macro Biome holds more of it where they thrive, its dens take root beside them, and its presence carries Lumen Seeds to the settlements nearby (WorldEcology, WorldResources.Volunteer).")]
    public List<string> blooms = new List<string>();

    [UnityEngine.Header("Magic (E10)")]
    [UnityEngine.Tooltip("Where its Coherence-Binding Tissue lies (None: an ordinary animal). An organ makes it a Pure Light being whatever its Structure (vault: Pure Light.md).")]
    public BindingOrgan binding;
    [UnityEngine.Tooltip("The element its magic is rooted in, and so its weakness (the Elemental Harmonic Circle, HarmonicCircle). Every Pure Light being carries one; an ordinary animal is Unattuned.")]
    public SpellBinding primaryBinding;
    [UnityEngine.Tooltip("Up to two further elements it can cast in (weaker than its primary). They widen its attacks; its weakness stays its primary's.")]
    public List<SpellBinding> secondaryBindings = new List<SpellBinding>();
    [UnityEngine.Tooltip("The harmonic climate it lives in: Coherence (most), Dissonance (Dead-zone lineages), or the leylines and silver water.")]
    public HarmonicNiche niche;
    [UnityEngine.Tooltip("The Echo it breeds in (1 Resonance ... 4 Silence): births crowd into it (WorldRhythm). 0: its diet's rhythm.")]
    [UnityEngine.Range(0, 4)] public int breedingEcho;
    [UnityEngine.Tooltip("The sprite ecotype it descends from (an id). WorldEcology.Entrain introduces offspring from Renewal near Agromagical Enclaves; parents keep their form. Its den spec keeps count 0.")]
    public string entrainedFrom;
    [UnityEngine.Tooltip("An unmerged sprite: survives only at sacred sites or leyline convergences with at least 80% Coherence.")]
    public bool unmerged;
    [UnityEngine.Tooltip("A sprite/slime ecotype family; variants share one lineage for the AECOR diet budget.")]
    public string ecotypeFamily;

    [UnityEngine.Header("People (E10)")]
    [UnityEngine.Tooltip("How much it lives off people's works (0 wild: settlements press it out; 1: it lives in granaries and middens and hardly anywhere else).")]
    [UnityEngine.Range(0f, 1f)] public float commensal;
    [UnityEngine.Tooltip("How much sickness it carries into settlements near its range (0 none). Feeds Disease Burden (PopulationHealth).")]
    [UnityEngine.Range(0f, 1f)] public float vector;

    [UnityEngine.Header("Discovery (E10)")]
    [UnityEngine.Tooltip("What coming to know it gives, once per level, on top of the bestiary's usual rewards (SpeciesLoreTuning).")]
    public List<DiscoveryReward> discovery = new List<DiscoveryReward>();

    [UnityEngine.Header("Map encounters (WorldPursuit)")]
    [UnityEngine.Tooltip("What its bands are on the map (each has its own mark): a creature, or one of the peoples. A threat's own beings are always Atonalis.")]
    public BandIdentity identity = BandIdentity.Creature;
    [UnityEngine.Tooltip("Where its bands go: Land, Water (lakes, the sea and rivers; they never leave it) or Amphibious. A Pure Light being with fins is at least aquatic.")]
    public CreatureHabitat habitat;
    [UnityEngine.Tooltip("At least this clever (its nature can raise it: the Smart subgroup is Smart; omnivores and pack hunters Regular).")]
    public BandIntelligence intelligence = BandIntelligence.Instinctive;
    [UnityEngine.Tooltip("Hexes it chases before giving up (0: by how it meets others; pack hunters run far, ambushers hardly at all).")]
    [UnityEngine.Min(0)] public int pursuitTiles;
    [UnityEngine.Tooltip("Hexes around home it defends and roams (0: by how it ranges).")]
    [UnityEngine.Min(0)] public int territoryTiles;
    [UnityEngine.Tooltip("Sevenths a band stays out when it leaves its den, once you know the den (0: its bands never leave the den, e.g. fish).")]
    [UnityEngine.Min(0)] public float denVisitSevenths = 18f;
}
