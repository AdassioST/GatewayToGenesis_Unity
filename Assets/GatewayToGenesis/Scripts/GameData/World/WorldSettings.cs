using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A resource and an amount (a one-time reward, or a cost).</summary>
[Serializable]
public class ResourceAmount
{
    public string resource;
    public float amount;
}

/// <summary>A kind of ground: its look, whether it can be crossed and what an explored cell of it yields every second.</summary>
[Serializable]
public class TerrainSpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    public Color color = Color.gray;
    [Tooltip("Expeditions cannot cross or explore it.")]
    public bool passable = true;
    [Tooltip("Sea or lake: water on the map, the drainage's outlets.")]
    public bool water;
    [Tooltip("The soil's share of land fertility (0 barren rock, 1 the richest loam).")]
    [Range(0f, 1f)] public float fertility = 0.4f;
    [Tooltip("Travel cost of one cell of it (1 open ground; roads and leylines make it cheaper).")]
    public float moveCost = 1f;
    [Tooltip("How hard it is to govern and integrate (1 open plains; highlands and bogs more). Territorial pull fades faster over it, society adopts it later and it weighs more on Administrative Capacity. 0: read from the travel cost.")]
    public float governance;
    [Tooltip("How beautiful (+1) or hideous (-1) this ground is to live on: people adopt and work beautiful land first (WorldBeauty).")]
    [Range(-1f, 1f)] public float beauty;
    [Tooltip("Flat production per second once a cell of this ground is held and surveyed (stored food, materials).")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();
    [Tooltip("What a unit gathers here with Forage, once per Age per cell (stored food: roots, beans, game).")]
    public List<ResourceAmount> forage = new List<ResourceAmount>();
}

/// <summary>
/// One ground a biome (or P) may lay down, and the climate it needs. A cell takes one of the rules whose elevation
/// and moisture ranges hold it, drawn by weight.
/// </summary>
[Serializable]
public class TerrainRule
{
    public string terrain;
    public float weight = 1f;
    [Range(0f, 1f)] public float minElevation = 0f;
    [Range(0f, 1f)] public float maxElevation = 1f;
    [Range(0f, 1f)] public float minMoisture = 0f;
    [Range(0f, 1f)] public float maxMoisture = 1f;

    public bool Holds(float elevation, float moisture) =>
        elevation >= minElevation && elevation <= maxElevation && moisture >= minMoisture && moisture <= maxMoisture;
}

/// <summary>
/// A macrobiome: a constrained recipe, not a finished square (WORLD_GENERATION.md §4). It supplies the climate and
/// height envelope of its slot, the ground it lays down, the orientations its uphill side may face and the slot
/// tags it needs; its handmade tiles (Resources/World/Tiles) are stamped into the slot's protected interior, turned
/// by the seed, and the rest is filled procedurally and blended into P.
/// </summary>
[Serializable]
public class BiomeSpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("The biome's colour on the atlas (macro) view.")]
    public Color color = Color.gray;
    public List<TerrainRule> terrains = new List<TerrainRule>();
    [Tooltip("Mean height of the slot, 0-1 (the sea stands at the settings' sea level).")]
    [Range(0f, 1f)] public float elevation = 0.45f;
    [Tooltip("Height of its hills and hollows (noise amplitude).")]
    [Range(0f, 0.5f)] public float relief = 0.12f;
    [Tooltip("Strength of broad stepped shelves, plains and incised valleys (0 disables).")]
    [Range(0f, 1f)] public float terraces;
    [Tooltip("Rise from its low side to its uphill side across the slot; the uphill side faces the chosen orientation.")]
    [Range(0f, 0.5f)] public float tilt = 0.1f;
    [Range(0f, 1f)] public float moisture = 0.5f;
    [Tooltip("Orientations the uphill side may face: sixths of a turn from east, counter-clockwise (0 E, 1 NE, 2 NW, 3 W, 4 SW, 5 SE). Empty: any.")]
    public List<int> orientations = new List<int>();
    [Tooltip("Slot tags it needs: north, south, east, west, coast, inland, core.")]
    public List<string> requires = new List<string>();
    [Tooltip("Slot tags it prefers (a better score, never a requirement).")]
    public List<string> prefers = new List<string>();
    [Tooltip("Share of the slot's protected interior covered by handmade tiles.")]
    [Range(0f, 1f)] public float tileCoverage = 0.3f;
    [Tooltip("Baseline Coherence of its land, 0-1.")]
    [Range(0f, 1f)] public float coherence = 0.5f;
}

/// <summary>
/// A sector of the composition stencil (S1-S7): its biome catalog, shared among all its slots (each same-sector
/// block of the stencil is one slot), and the regional character every slot keeps.
/// </summary>
[Serializable]
public class SectorSpec
{
    [Tooltip("The stencil's token (S1-S7).")]
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("Biomes shuffled into the sector's slots, each at most once (unless repeats are allowed). With more biomes than slots, the seed draws which ones appear.")]
    public List<string> biomes = new List<string>();
    [Tooltip("A placeholder catalog smaller than its slot count may reuse biomes.")]
    public bool allowRepeats;
    [Tooltip("Height of the range raised along the sector's own P (0: none). A mountainous sector reads as one range.")]
    [Range(0f, 0.6f)] public float backbone;
    [Tooltip("Coherence Seeds placed in each of its slots.")]
    public int coherenceSeeds = 1;
    [Tooltip("An offshore sector: the P around it becomes strait and sea instead of land.")]
    public bool island;
}

/// <summary>
/// Something to find on the map: a ruin, an orchard, a landmark, a legend waiting to be met. Features arrive with
/// their Age (<see cref="minAge"/>): generation places Age 0's, and each new Age places its own, so every Age brings
/// new things to the map.
/// </summary>
[Serializable]
public class FeatureSpec
{
    public enum Preference { None, LandFertility, MagicalFertility, Coherence, River }

    public string id;
    public string name;
    [Tooltip("Grouping read by rules: 'fertile' eases the famine, 'legend' recruits a legend, 'landmark' starts ballads.")]
    public string tag;
    [TextArea(1, 4)] public string description;
    public Color color = Color.white;
    [Tooltip("The Age (number) in which this feature appears on the map.")]
    public int minAge;
    [Tooltip("How many are placed when their Age begins.")]
    public int count = 1;
    [Tooltip("Independent authority established by this place; empty means no territorial claim.")]
    public string authorityId;
    public int authorityRadius = 2;
    public bool requiresFreshwater, requiresCoast;
    [Range(0f, 1f)] public float minimumCoherence;
    [Tooltip("Cell steps from the capital.")]
    public int minDistance = 2, maxDistance = 999;
    [Tooltip("Ground it may sit on (empty: any passable ground).")]
    public List<string> terrains = new List<string>();
    [Tooltip("Biomes it may sit in (empty: any).")]
    public List<string> biomes = new List<string>();
    [Tooltip("Where it would rather be: among the best cells of this field.")]
    public Preference prefers;
    [Tooltip("Cells kept between two of a kind.")]
    public int spacing = 3;
    [Tooltip("Seen through the fog before it is reached (a landmark on the horizon).")]
    public bool visibleFromAfar;
    [Tooltip("May appear on ground already explored (things that arise where people live).")]
    public bool mayAppearOnExplored;
    [Tooltip("One-time reward when explored.")]
    public List<ResourceAmount> rewards = new List<ResourceAmount>();
    [Tooltip("Flat production per second from then on.")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();
    [Tooltip("An unrecruited legend joins the roster when this is explored.")]
    public bool recruitsLegend;
    [Tooltip("Lyrical Fragments (of LegendSettings' discovery kind: Lucidity) for each legend of the expedition that finds it.")]
    public int renown;
    [Tooltip("Ink knot offered once it is surveyed (a ballad's verse, a discovery); empty for none.")]
    public string story;
    [Tooltip("An event line for expeditions: the first expedition to survey one of these offers the first knot, the next the second... (instead of the story above).")]
    public List<string> expeditionStories = new List<string>();
    [Tooltip("How much it adds to the beauty of its cell (-1 to 1): a choir of ruins or a blooming orchard draws people; a scar repels them.")]
    [Range(-1f, 1f)] public float beauty;
}

/// <summary>
/// A Resource Grandfield (Arcanoria.md, Map Features): a sparse, multi-cell patch of one resource, densest at its
/// heart, that needs a dedicated extraction Outpost. Its bonus counts once per field, never once per cell.
/// </summary>
[Serializable]
public class GrandfieldSpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("The resource it concentrates.")]
    public string resource;
    [Tooltip("Building Material or Vital Resource (the vault's two kinds).")]
    public string category = "Building Material";
    public Color color = new Color(0.85f, 0.7f, 0.35f);
    [Tooltip("The Age (number) in which fields of this kind appear.")]
    public int minAge;
    public int count = 3;
    [Tooltip("Cells in one footprint (it grows over compatible ground up to this size).")]
    public int size = 9;
    [Tooltip("Ground it grows on (empty: any passable ground).")]
    public List<string> terrains = new List<string>();
    [Tooltip("Biomes it grows in (empty: any).")]
    public List<string> biomes = new List<string>();
    [Range(0f, 1f)] public float minimumCoherence;
    [Range(0f, 1f)] public float minimumMagicalFertility;
    public bool requiresFreshwater;
    [Tooltip("Cell steps from the capital.")]
    public int minDistance = 6;
    [Tooltip("Flat production per second at full density once an Outpost extracts it.")]
    public float baseYield = 0.3f;
}

/// <summary>The ten functional families of Enclave (Arcanoria.md, The Horizontal Worldbuilding of Enclaves).</summary>
public enum EnclaveFamily { Agromagical, Militant, Auric, Weaver, Domestication, Trading, Industrious, Regal, Indulgent, Esoteric }

/// <summary>What ground an Enclave's function looks for (WORLD_GENERATION.md §6D).</summary>
public enum EnclaveSite { Any, Coast, Freshwater, Pass, Nexus, DualFertile, Extraction, Anomaly, HighCoherence, SilverWater }

/// <summary>
/// An Enclave: an autonomous city-state with a function, a binding and an origin wound (Enclave.md). Candidates
/// arrive with their Age: Age 0 knows only the first Militant and Trading enclaves among survivor tribes; the famine's
/// survivors found Agromagical ones in the Age of Renewal.
/// </summary>
[Serializable]
public class EnclaveSpec
{
    public string id;
    public string name;
    public EnclaveFamily family;
    [TextArea(1, 4)] public string description;
    [TextArea(1, 3)] public string originWound;
    public Color color = new Color(0.9f, 0.62f, 0.3f);
    public int minAge;
    public int count = 1;
    public EnclaveSite site;
    [Tooltip("Bindings it may be tuned to (empty: any of the seven).")]
    public List<string> bindings = new List<string>();
    public int authorityRadius = 3;
    [Tooltip("Wound Resonance range at founding (0-100: above 75 raw, 40-75 integrated, below 40 forgotten).")]
    public int woundMin = 55, woundMax = 85;
    public int minDistance = 8;
    public int spacing = 12;
    [Tooltip("Production per second while it is your suzerain.")]
    public List<ResourceAmount> suzeraintyYields = new List<ResourceAmount>();
}

/// <summary>
/// A source of danger on the map (WORLD_GENERATION.md §6G): its spawn potential, not its occupants. Sacred Sites stay
/// clear (Atonalis avoid them entirely); danger slows travel and weighs on City Development.
/// </summary>
[Serializable]
public class ThreatSpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    public Color color = new Color(0.9f, 0.25f, 0.25f);
    public int minAge;
    [Tooltip("Placed on the world's Dissonance Seeds (each one) instead of drawn from low-Coherence ground.")]
    public bool onDissonanceSeeds;
    public int count = 3;
    [Range(0f, 1f)] public float strength = 0.6f;
    [Tooltip("Reach of its danger, in cells.")]
    public float radius = 6f;
    public int minDistance = 12;
}

/// <summary>Which gifted geography can hold a Trade Nexus, and how many a world keeps (Arcanoria.md, Trade Nexus).</summary>
[Serializable]
public class NexusRules
{
    [Tooltip("Nexus sites kept per world (the best-scoring, spaced apart).")]
    public int count = 14;
    public int spacing = 14;
    public int minDistance = 5;
    public bool harbors = true, estuaries = true, passes = true, confluences = true;
}

/// <summary>A binding a Major Settlement is attuned to (one of the seven of The Principles of Magic) and what it adds.</summary>
[Serializable]
public class BindingSpec
{
    public string id;
    [TextArea(1, 2)] public string focus;
    [Tooltip("Production per second of a Major Settlement attuned to it.")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();
}

/// <summary>
/// How settlements grow on the map (Arcanoria.md, Settlements): Developing Towns inside Administrative Authority and
/// apart from other settlements, detached Outposts, Religious Havens, promotion to Major Settlement within Government
/// Capacity, roads, Resonance Anchors and the City Development weights. Every number is a proposal.
/// </summary>
[Serializable]
public class SettlementRules
{
    [Header("Founding")]
    [Tooltip("Fewest cell steps between two settlements (the vault's distance limit against sprawl).")]
    public int spacing = 4;
    [Tooltip("Cells around a settlement it works and draws its City Development from.")]
    public int workRadius = 2;
    public List<ResourceAmount> townCost = new List<ResourceAmount>();
    public List<ResourceAmount> outpostCost = new List<ResourceAmount>();
    public List<ResourceAmount> havenCost = new List<ResourceAmount>();
    public List<ResourceAmount> incorporateCost = new List<ResourceAmount>();
    [Tooltip("A Religious Haven needs a Sacred Site within this many cells, or this much Coherence.")]
    public int havenSacredReach = 2;
    [Range(0f, 1f)] public float havenCoherence = 0.75f;

    [Header("Authority")]
    [Tooltip("Core ground a Developing Town holds the moment it is founded; beyond it its territorial pull adopts land over time (territory).")]
    public int townAuthorityRadius = 1;
    [Tooltip("Core ground of a Major Settlement; beyond it its pull adopts land over time (territory).")]
    public int majorAuthorityRadius = 2;
    [Tooltip("Territorial pull, passive adoption of land and Administrative Capacity (WorldTerritory).")]
    public TerritoryRules territory = new TerritoryRules();

    [Header("Growth and promotion")]
    [Tooltip("City Development gained per seventh while below its potential (lost at half the rate above it).")]
    public float growthPerSeventh = 0.12f;
    [Tooltip("City Development a Developing Town needs to become a Major Settlement.")]
    public float majorThreshold = 55f;
    [Tooltip("Major Settlements allowed (the Capital's Government Capacity) before Capital improvements.")]
    public int governmentCapacity = 1;
    [Tooltip("Each of these Capital buildings raises Government Capacity by one (empty: none).")]
    public string capacityBuilding = "";
    public List<ResourceAmount> promoteCost = new List<ResourceAmount>();
    [Tooltip("Ceiling of City Development at zero Coherence; the rest scales with the settlement's Coherence (high Coherence is needed to fully develop).")]
    public float ceilingBase = 35f;

    [Header("City Development weights (points)")]
    public float landWeight = 30f;
    public float freshwaterWeight = 12f;
    public float coherenceWeight = 20f;
    public float magicWeight = 10f;
    public float leylineWeight = 6f;
    public float convergenceWeight = 4f, basinWeight = 8f;
    public float roadWeight = 6f, networkWeight = 4f, nexusWeight = 12f;
    public float grandfieldWeight = 5f;
    public float sacredWeight = 10f;
    public float dangerWeight = 20f;
    public float dissonanceWeight = 10f;
    [Tooltip("Points for worked ground at full beauty (lost at full hideousness): people build and stay where the land is fair.")]
    public float beautyWeight = 6f;

    [Header("Yields")]
    [Tooltip("Production per second of a Developing Town per 10 City Development.")]
    public List<ResourceAmount> townYields = new List<ResourceAmount>();
    public List<ResourceAmount> majorYields = new List<ResourceAmount>();
    [Tooltip("A Religious Haven's production per 10 City Development, scaled by its Vibrational Density (Coherence).")]
    public List<ResourceAmount> havenYields = new List<ResourceAmount>();
    public List<BindingSpec> bindings = new List<BindingSpec>();

    [Header("Grandfields")]
    [Tooltip("Production bonus (percent) to a grandfield's resource once an Outpost extracts it, scaled by the density under the Outpost.")]
    public float grandfieldBonusPercent = 150f;
    [Tooltip("Largest total bonus per resource from grandfields (the vault's tenfold output is a balance target).")]
    public float grandfieldCapPercent = 900f;

    [Header("Roads")]
    public List<ResourceAmount> roadCostPerCell = new List<ResourceAmount>();
    [Tooltip("Cells between two Trade Nodes along a road.")]
    public int nodeSpacing = 8;

    [Header("Resonance Anchors")]
    public List<ResourceAmount> anchorCost = new List<ResourceAmount>();
    [Range(0f, 1f)] public float anchorCoherence = 0.2f;
    public int anchorRadius = 4;
    [Tooltip("Cells within which an Anchor draws the leylines toward itself.")]
    public int anchorPull = 10;

    [Header("Enclaves")]
    public List<ResourceAmount> envoyCost = new List<ResourceAmount>();
    public float envoyInfluence = 25f;

    [Header("Claiming land")]
    [Tooltip("Food value paid from the stored food (any kind, the most perishable first) to bring one known wilderness cell bordering your authority inside it.")]
    public float claimFoodValue = 12f;
    [Tooltip("Other resources a claim also costs (none by default).")]
    public List<ResourceAmount> claimCost = new List<ResourceAmount>();
    [Tooltip("Each cell already claimed raises the next claim's cost by this share (0.1 = +10%).")]
    public float claimCostGrowth = 0.1f;

    [Header("Travel")]
    [Tooltip("Extra micro hexes a unit sees while it stands on a leyline (seers see farther).")]
    public int leylineRevealBonus = 4;

    [Header("Hotspots and builders")]
    [Tooltip("Each improvement level adds this share of a hotspot's yields (0.5: +50% per level).")]
    public float improvementBonus = 0.5f;
    public int maxImprovement = 3;
    [Tooltip("An improved grandfield cell adds this share of the field's base yield per level, scaled by its density.")]
    public float grandfieldImprovementShare = 0.5f;

    [Header("Era Score (proposals)")]
    public int eraTown = 2, eraHaven = 2, eraOutpost = 1, eraNexus = 3, eraMajor = 3, eraSuzerainty = 2;
    public int eraLandmark = 1, eraUniqueLandmark = 3, eraSacredSite = 2, eraEnclaveMet = 1, eraMasterwork = 1;

    public BindingSpec Binding(string id) => bindings.Find(b => b != null && string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What exerts territorial pull, strongest first (Arcanoria.md, Settlements: the Capital "anchors all Administrative
/// Authority", Major Settlements are "major anchors", Developing Towns are "expansion engines", Outposts stand
/// detached). Trade, grandfields and Religious Havens pull too. Enclaves pull for themselves (a rival pull).
/// </summary>
public enum SeatKind { Capital, Major, TradeNexus, TradeNode, Town, Haven, Grandfield, Outpost, Enclave }

/// <summary>How strongly one kind of seat pulls land toward its authority, how far, and how much it can hold.</summary>
[Serializable]
public class SeatSpec
{
    public SeatKind kind;
    [Tooltip("Pull at the seat itself (the Capital 1). A wilderness cell's pull is the strongest seat's, reduced with the governance travel to it.")]
    public float strength = 0.5f;
    [Tooltip("Governance travel (each cell's governance difficulty, summed) at which its pull fades to nothing.")]
    public float reach = 4f;
    [Tooltip("Most cells it holds by its own pull (those where it pulls strongest, its core included). The rest waits for another seat.")]
    public int maxCells = 6;
    [Tooltip("Cells per Seventh it adopts while it has room and the administration has capacity to spare.")]
    public float adoptPerSeventh = 0.25f;
    [Tooltip("Administrative Capacity it adds. Settlements scale it by City Development (x0.5 at 0, x1.5 at 100).")]
    public float capacity = 1f;
    [Tooltip("Scale its capacity with its settlement's City Development.")]
    public bool scalesWithDevelopment;

    public SeatSpec() { }

    public SeatSpec(SeatKind kind, float strength, float reach, int maxCells, float adoptPerSeventh, float capacity, bool scales = false)
    {
        this.kind = kind;
        this.strength = strength;
        this.reach = reach;
        this.maxCells = maxCells;
        this.adoptPerSeventh = adoptPerSeventh;
        this.capacity = capacity;
        scalesWithDevelopment = scales;
    }
}

/// <summary>Something outside the map that raises Administrative Capacity: a technology, a Capital building (per count).</summary>
[Serializable]
public class CapacitySource
{
    public string id;
    public float capacity = 2f;
}

/// <summary>
/// Territorial pull, the passive adoption of land and Administrative Capacity (<see cref="WorldTerritory"/>). Every
/// number is a proposal (roadmap D07): they set where horizontal expansion stops paying and vertical growth takes over.
/// </summary>
[Serializable]
public class TerritoryRules
{
    [Header("Seats of authority")]
    public List<SeatSpec> seats = DefaultSeats();
    [Tooltip("Least pull a wilderness cell needs before society adopts it.")]
    public float adoptThreshold = 0.2f;
    [Tooltip("Share of the other seats' pull added to the strongest (seats close together reinforce each other).")]
    [Range(0f, 1f)] public float overlapShare = 0.25f;
    [Tooltip("A Resonance Anchor raises its settlement's pull by this share (and its reach by one).")]
    public float anchorPullBonus = 0.25f;
    [Tooltip("Adopting needs the cell known (an expedition passed over it), except within this many cells of a settlement (the locals know it).")]
    public int localKnowledge = 1;

    [Header("Governance travel (what pull fades over)")]
    [Tooltip("Multiplier on a road cell (roads carry the administration).")]
    public float roadFactor = 0.5f;
    [Tooltip("Multiplier on a river cell without a road (valleys are integrated along the water).")]
    public float riverFactor = 0.85f;
    [Tooltip("Added per unit of escarpment (the height step to the neighbours, about 0.02-0.1), of dissonance and of danger.")]
    public float escarpmentCost = 10f, dissonanceCost = 1f, dangerCost = 1f;

    [Header("What society integrates first (adoption priority)")]
    public float fertilityPriority = 1.2f;
    public float freshwaterPriority = 0.5f;
    public float coherencePriority = 0.5f;
    public float beautyPriority = 0.8f;
    public float grandfieldPriority = 0.6f;
    public float featurePriority = 0.3f;
    [Tooltip("Ground an expedition surveyed is trusted a little more.")]
    public float exploredPriority = 0.15f;
    [Tooltip("Subtracted per unit of danger, of dissonance and of governance difficulty above open plains.")]
    public float dangerPriority = 0.8f, dissonancePriority = 0.6f, difficultyPriority = 0.5f;

    [Header("Administrative Capacity")]
    [Tooltip("What the Capital's bureaucracy holds by itself.")]
    public float baseCapacity = 12f;
    [Tooltip("Added for each Major Settlement the Government Capacity allows (Capital improvements).")]
    public float perGovernmentCapacity = 3f;
    [Tooltip("Added for each settlement joined to the Capital by road.")]
    public float networkCapacity = 1f;
    [Tooltip("Added (or taken) per unit of the held land's average Coherence above (below) 0.4: coherent land governs itself.")]
    public float coherenceCapacity = 10f;
    [Tooltip("Researched technologies that raise it.")]
    public List<CapacitySource> technologies = new List<CapacitySource>();
    [Tooltip("Capital buildings that raise it, per building.")]
    public List<CapacitySource> buildings = new List<CapacitySource>();
    [Tooltip("The council area whose seated legend raises it (half through a related area).")]
    public string councilArea = "governance";
    public float councilCapacity = 4f;

    [Header("Administrative load (what each held cell costs)")]
    [Tooltip("A settlement's own cell costs this share of a cell.")]
    public float seatCellLoad = 0.25f;
    [Tooltip("Added per unit of governance travel from the nearest seat of yours.")]
    public float distanceLoad = 0.08f;
    [Tooltip("Load eased (added) per unit of Coherence above (below) 0.5.")]
    public float coherenceRelief = 0.5f;
    [Tooltip("Load eased per unit of beauty (content people keep themselves).")]
    public float beautyRelief = 0.15f;

    [Header("Strain (load over capacity)")]
    [Tooltip("Below it everything runs at full efficiency: horizontal expansion is favoured.")]
    public float comfortStrain = 0.8f;
    [Tooltip("At it society stops adopting land by itself (claims still work).")]
    public float overStrain = 1.1f;
    [Tooltip("Above it the farthest, weakest-held land slips back into the wilderness.")]
    public float collapseStrain = 1.4f;
    [Tooltip("Efficiency lost per unit of strain above comfort, and the floor it never goes below.")]
    public float efficiencyDrop = 0.9f;
    [Range(0f, 1f)] public float efficiencyFloor = 0.4f;
    [Tooltip("Cells lost per Seventh above the collapse strain.")]
    public float driftPerSeventh = 0.5f;

    [Header("Beauty")]
    [Tooltip("Held land yields this much more at full beauty (less when hideous): people want to work fair land.")]
    public float beautyWork = 0.2f;

    public SeatSpec Seat(SeatKind kind)
    {
        var spec = seats?.Find(s => s != null && s.kind == kind);
        return spec ?? Defaults.Find(s => s.kind == kind);
    }

    private static readonly List<SeatSpec> Defaults = DefaultSeats();

    public static List<SeatSpec> DefaultSeats() => new List<SeatSpec>
    {
        new SeatSpec(SeatKind.Capital, 1f, 9f, 30, 1f, 0f),
        new SeatSpec(SeatKind.Major, 0.8f, 8f, 36, 0.6f, 10f, true),
        new SeatSpec(SeatKind.TradeNexus, 0.6f, 5f, 10, 0.3f, 3f),
        new SeatSpec(SeatKind.TradeNode, 0.4f, 3f, 4, 0.15f, 1f),
        new SeatSpec(SeatKind.Town, 0.55f, 5f, 16, 0.35f, 4f, true),
        new SeatSpec(SeatKind.Haven, 0.4f, 3f, 6, 0.15f, 1f),
        new SeatSpec(SeatKind.Grandfield, 0.45f, 4f, 8, 0.25f, 0f),
        new SeatSpec(SeatKind.Outpost, 0.4f, 3f, 4, 0.1f, 0.5f),
        new SeatSpec(SeatKind.Enclave, 0.5f, 4f, 0, 0f, 0f),
    };
}

/// <summary>What a unit on the map is for. Scout and Settler are retired (expeditions of legends do both); kept so the values keep their numbers.</summary>
public enum UnitRole { Scout, Settler, Builder, Expedition }

/// <summary>What a unit can do on the land (<see cref="UnitSpec.abilities"/>; the working ones are described in <see cref="UnitAbilities"/>).</summary>
[Flags]
public enum UnitAbility
{
    None = 0,
    /// <summary>Survey the micro hexes around it (its survey radius): each becomes surveyed, and a meso cell whose every hex is surveyed is explored and its sites investigated.</summary>
    Survey = 1,
    /// <summary>Gather the ground's forage (stored food), once per Age per cell.</summary>
    Forage = 2,
    /// <summary>Found a settlement where it stands (consumes the unit).</summary>
    Settle = 4,
    /// <summary>Improve a resource hotspot.</summary>
    Improve = 8,
    /// <summary>Walk to unknown ground by itself while it has nothing else to do.</summary>
    AutoExplore = 16,
    /// <summary>Survey all seven micro hexes of the meso hex it stands in, in one longer sweep, wherever in it it stands.</summary>
    SurveyMeso = 32,
}

/// <summary>
/// A kind of unit that walks the map. It walks the micro hexes (<see cref="MicroGrid"/>): its <see cref="stamina"/> is
/// the travel fatigue it can spend in one Seventh (Cycle.md: a Seventh is four Days), and each micro hex costs its
/// travel fatigue (rough ground more, roads and leylines less, climbing more than descending, fords more), so an
/// expedition crosses about six micro hexes of open plain per Seventh, some two meso cells. It carries rations,
/// tires and wears down (<see cref="WorldUnits.Needs"/>): hungry, exhausted or worn it walks and works slower, and a
/// unit worn out entirely is lost. Units move smoothly within the Seventh and stop when time is paused. Numbers are
/// proposals.
/// </summary>
[Serializable]
public class UnitSpec
{
    public string id;
    public string name;
    public UnitRole role;
    [TextArea(1, 3)] public string description;
    public Color color = Color.white;
    [Tooltip("Travel fatigue it can spend per Seventh (one micro hex of open ground costs 1).")]
    public float stamina = 6f;
    [Tooltip("Micro hexes it sees around itself (a leyline adds the settings' leyline reveal bonus, a ridge 2; weariness takes some away).")]
    public int sight = 5;

    [Header("Provisions and endurance")]
    [Tooltip("Rations it carries when full (one ration feeds it for a Seventh at the usual pace).")]
    public float supplyCapacity = 12f;
    [Tooltip("Rations eaten per Seventh on the march or at work (less in camp, more in harsh weather).")]
    public float supplyUsePerSeventh = 1f;
    [Tooltip("Fatigue (0-100) gained per point of travel fatigue walked (more in harsh weather).")]
    public float fatiguePerTravelCost = 1.2f;
    [Tooltip("Fatigue gained per Seventh at work (surveying, foraging, building).")]
    public float workFatiguePerSeventh = 4f;
    [Tooltip("Fatigue shed per Seventh in camp (a share of it while merely standing; more in a settlement).")]
    public float campRecoveryPerSeventh = 25f;
    [Tooltip("Attrition (0-100) per Seventh without rations.")]
    public float starvationAttritionPerSeventh = 8f;
    [Tooltip("Attrition healed per Seventh while fed and resting in camp (three times as much in a settlement).")]
    public float recoveryPerSeventh = 3f;
    [Tooltip("Rations a camp gathers per Seventh on the most fertile ground (hunting, roots, water): scaled by the land's fertility, less in danger and harsh weather.")]
    public float forageRationsPerSeventh = 1.2f;
    [Tooltip("Sevenths one survey of a whole meso hex takes (all seven of its micro hexes; less when some are already surveyed).")]
    public float mesoSurveySevenths = 2.5f;
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    [Tooltip("Citizens who leave with it (settlers carry people to the new town).")]
    public int populationCost;
    [Tooltip("Researched before it can be trained (empty: always).")]
    public string unlockTechnology;
    [Tooltip("What it can do on the land.")]
    public UnitAbility abilities;
    [Tooltip("Micro hexes around it that become known as it walks (scouts 1: known land is land a scout passed over; 0: only the hex it walks on).")]
    public int knowRadius;
    [Tooltip("Sevenths one survey takes, and the micro hexes around it surveyed at once (0: its own hex; 1: the seven around it).")]
    public float surveySevenths = 1f;
    public int surveyRadius = 1;
    [Tooltip("Sevenths one forage takes.")]
    public float forageSevenths = 1f;
    [Tooltip("Rewards from sites, forage and surveys are multiplied by this (expeditions bring back more).")]
    public float rewardMultiplier = 1f;
    [Tooltip("Attrition it takes from hunger, strain, weather, danger and dissonance is multiplied by this.")]
    public float wearMultiplier = 1f;
    [Tooltip("Most of this kind at once (0: no limit); each limit building adds one. Expeditions are limited by their slots instead (WorldSettings.expeditions).")]
    public int limit = 1;
    public string limitBuilding;
    [Tooltip("Builders: improvements it can make before it is spent.")]
    public int charges;
    [Tooltip("Sevenths one task takes (a builder's improvement).")]
    public float workSevenths = 2f;

    /// <summary>A copy to adjust for one unit (an expedition's party); lists are shared, not copied.</summary>
    public UnitSpec Clone() => (UnitSpec)MemberwiseClone();
}

/// <summary>What befalls an expedition when things go bad (<see cref="Expeditions.RollMishap"/>); each kind has its own target and condition.</summary>
public enum MishapKind
{
    /// <summary>A member is hurt (anywhere; the most strained are likeliest).</summary>
    Injury,
    /// <summary>A member falls sick (hungry, worn, or in harsh weather).</summary>
    Fever,
    /// <summary>Part of the rations carried is lost (the Director answers for it).</summary>
    SpoiledRations,
    /// <summary>The Director loses the way (on the march, or in harsh weather): the party tires.</summary>
    LostBearings,
    /// <summary>Two members quarrel (when one is Fractured or deeper).</summary>
    Quarrel,
    /// <summary>Dissonance gnaws at a member (on dissonant ground).</summary>
    Whispers,
    /// <summary>The party is attacked (within a threat's reach).</summary>
    Ambush,
    /// <summary>A Spiraling companion abandons the expedition and makes for home.</summary>
    Desertion,
}

/// <summary>One kind of mishap and what it does. Every number is a proposal.</summary>
[Serializable]
public class MishapSpec
{
    public MishapKind kind;
    [Tooltip("Its name in the notices.")]
    public string name;
    [Tooltip("Relative chance among the mishaps that can happen where the expedition stands.")]
    public float weight = 1f;
    [Tooltip("Strain added to the legend it strikes (Composure).")]
    public float strain;
    [Tooltip("Strain added to every other member.")]
    public float partyStrain;
    [Tooltip("Attrition added to the expedition.")]
    public float attrition;
    [Tooltip("Fatigue added to the expedition.")]
    public float fatigue;
    [Tooltip("Share of the rations carried that is lost.")]
    [Range(0f, 1f)] public float rationsLost;
}

/// <summary>
/// Expeditions: parties of legends that walk the world in place of generic scouts and settlers (the vault has no
/// Expedition note; everything here is game design and every number a proposal). Each legend in the field takes one of
/// the civilization's expedition slots, which grow with the Capital's Government Capacity and the Hollow Watchposts.
/// A party is led by its Director, whose Soul Leitmotif gives it one strength; hardship on the road strains every
/// member's Composure, and when things go bad mishaps strike its members (<see cref="Expeditions"/>).
/// </summary>
[Serializable]
public class ExpeditionSettings
{
    [Tooltip("The unit (in WorldSettings.units) an expedition walks as; its numbers are per legend.")]
    public string unit = "expedition";

    [Header("Slots (per civilization)")]
    public int baseSlots = 1;
    [Tooltip("Slots per point of Government Capacity (the Capital's administration).")]
    public int slotsPerCapacity = 2;
    [Tooltip("Each of these buildings adds slotsPerBuilding.")]
    public string slotBuilding = "Hollow Watchpost";
    public int slotsPerBuilding = 1;
    [Tooltip("Most legends in one expedition, its Director included.")]
    public int maxParty = 4;

    [Header("Forming")]
    [Tooltip("Paid per legend when an expedition forms or a companion joins (empty: free; slots are the limit, and rations come from your stores as the party draws them).")]
    public List<ResourceAmount> outfitCost = new List<ResourceAmount>();
    [Tooltip("The first expedition sets out free, directed by a free legend, when the map's technology is researched.")]
    public bool firstFree = true;

    [Header("Settlers")]
    [Tooltip("Citizens an expedition escorts to found a settlement.")]
    public int settlers = 5;
    [Tooltip("What taking them on costs (besides the citizens). Not Food itself: Food past the growth threshold becomes people, so it never banks enough.")]
    public List<ResourceAmount> settlerCost = new List<ResourceAmount>();
    [Tooltip("Food value their provisions take from your stores (the Pantry, like claiming land); 0: none.")]
    public float settlerFoodValue;
    [Tooltip("For rations, each settler counts as this share of a legend.")]
    public float settlerWeight = 0.3f;
    [Tooltip("The party's pace with settlers aboard.")]
    public float settlerPace = 0.5f;
    [Header("Improving the land (the legends' own work: builders are folded into expeditions)")]
    [Tooltip("The technology that teaches expeditions to improve hotspots.")]
    public string improveTechnology = "Woodcraft Mastery";
    [Tooltip("Paid when an improvement starts (materials; the legends' time is the rest).")]
    public List<ResourceAmount> improveCost = new List<ResourceAmount>();
    [Tooltip("The improvement's work time is multiplied by this for each legend beyond the first (more hands, quicker work).")]
    public float improveHands = 0.8f;

    [Header("Hardship: Composure strain per Seventh on each member")]
    [Tooltip("Attrition above which the road starts to weigh on the party.")]
    public float attritionFrom = 20f;
    [Tooltip("At 100 attrition (scaled from attritionFrom).")]
    public float attritionStrain = 3f;
    public float hungerStrain = 2f;
    [Tooltip("Marching or working at 90 fatigue or more.")]
    public float exhaustionStrain = 1f;
    [Tooltip("At full Dissonance.")]
    public float dissonanceStrain = 2f;
    [Tooltip("Per point of harsh weather above the ordinary.")]
    public float exposureStrain = 1f;
    [Tooltip("The Director carries this much more.")]
    public float directorShare = 1.25f;
    [Tooltip("Strain on the companions of a legend lost to Dissonance on the road.")]
    public float lossStrain = 8f;
    [Tooltip("Strain on every member when the expedition breaks (attrition 100).")]
    public float breakStrain = 20f;

    [Header("Mishaps: chance per Seventh")]
    [Tooltip("At 100 attrition (from 25).")]
    public float attritionRisk = 0.25f;
    public float hungerRisk = 0.15f;
    public float exhaustionRisk = 0.05f;
    [Tooltip("At full danger.")]
    public float dangerRisk = 0.2f;
    [Tooltip("At full Dissonance.")]
    public float dissonanceRisk = 0.15f;
    [Tooltip("Per point of harsh weather.")]
    public float exposureRisk = 0.1f;
    [Tooltip("Per member Fractured, and per member Spiraling or deeper.")]
    public float fracturedRisk = 0.05f, spiralingRisk = 0.1f;
    public float maxRisk = 0.6f;
    [Tooltip("Empty: the defaults (Expeditions.DefaultMishaps).")]
    public List<MishapSpec> mishaps = new List<MishapSpec>();

    [Header("The Director's strength, by Soul Leitmotif (the class affinities of Stellar Legacy Score.md)")]
    [Tooltip("Luminance (Truth): micro hexes of sight added.")]
    public int luminanceSight = 2;
    [Tooltip("Cindergale (Momentum): pace multiplier.")]
    public float cindergalePace = 1.15f;
    [Tooltip("Crystal (Endurance): attrition multiplier.")]
    public float crystalWear = 0.8f;
    [Tooltip("Void (Sacrifice): rations multiplier.")]
    public float voidRations = 0.8f;
    [Tooltip("Strand (History, Reliquaries): reward multiplier.")]
    public float strandRewards = 1.25f;
    [Tooltip("Flux (Emotion, healing): camp recovery multiplier.")]
    public float fluxRest = 1.3f;
    [Tooltip("Resonance (Unity): the party's hardship multiplier.")]
    public float resonanceHardship = 0.75f;
    [Tooltip("Each companion adds this share to what the party brings back.")]
    public float companionRewards = 0.1f;

    [Header("Party size: Solo, Duo, Trio, Company (PartyShapes)")]
    [Tooltip("One entry per size, from the legends it applies to (empty: PartyShapes.Defaults).")]
    public List<PartyShape> shapes = new List<PartyShape>();
    [Tooltip("Share of a mishap's harm (wear and strain) that lands when the others handle it; a slipped ambush lands this share of its strain and no wear.")]
    [Range(0f, 1f)] public float easedShare = 0.35f;

    [Header("Retreat (small parties slip away to safe ground)")]
    [Tooltip("Ground is safe to retreat to below this danger (or held by you)...")]
    public float safeDanger = 0.1f;
    [Tooltip("...and below this Dissonance.")]
    public float safeDissonance = 0.2f;

    [Header("Missing in action (a legend alone goes to ground)")]
    [Tooltip("A lone legend can choose to go to ground once its Composure is this deep (a lone expedition worn to 100 attrition goes to ground instead of breaking).")]
    public ComposureState vanishFrom = ComposureState.Fractured;
    [Tooltip("Sevenths it stays missing at the least...")]
    public float missingSevenths = 2f;
    [Tooltip("...plus the Sevenths the walk back to your territory would take, times this (slow, unseen, living rough).")]
    public float missingSlowness = 1.5f;
    [Tooltip("Attrition and fatigue it turns up with (no rations).")]
    public float reappearAttrition = 40f, reappearFatigue = 60f;
}

/// <summary>
/// How units live off the land and the stores (<see cref="WorldUnits.Needs"/>): where they draw rations, what camp
/// does, and what wears them down. Each unit's own rates are in its <see cref="UnitSpec"/>. Every number is a proposal.
/// </summary>
[Serializable]
public class ProvisionRules
{
    [Header("Rations")]
    [Tooltip("Rations a unit draws per Seventh in one of your settlements (half as many elsewhere inside your authority).")]
    public float resupplyPerSeventh = 12f;
    [Tooltip("Food value taken from your stores for each ration drawn (stored food first, then Food).")]
    public float foodValuePerRation = 1f;
    [Tooltip("Share of its usual rations a unit eats in camp.")]
    [Range(0f, 1f)] public float campRationShare = 0.7f;

    [Header("Fatigue")]
    [Tooltip("Share of camp's fatigue recovery a unit gets standing idle outside camp.")]
    [Range(0f, 1f)] public float idleRecoveryShare = 0.35f;
    [Tooltip("Fatigue at which a walking unit can go no further and makes camp by itself (it walks on once rested).")]
    public float exhaustion = 100f;
    [Tooltip("Fatigue at or below which a unit that made camp by itself walks on.")]
    public float rested = 30f;

    [Header("Attrition (per Seventh)")]
    [Tooltip("Marching or working at 90 fatigue or more.")]
    public float exhaustionAttrition = 6f;
    [Tooltip("Per point of harsh weather above the ordinary (a travel multiplier of 1.25 is a quarter point); halved in camp, none in a settlement.")]
    public float exposureAttrition = 12f;
    [Tooltip("At full danger (the reach of threats); none in a settlement.")]
    public float dangerAttrition = 10f;
    [Tooltip("At full Dissonance (low-Coherence ground wears on the living); none in a settlement.")]
    public float dissonanceAttrition = 5f;
    [Tooltip("A unit whose attrition reaches 100 is lost (its people scatter or perish).")]
    public bool lostAtFullAttrition = true;
}

/// <summary>
/// Everything the world map is made of (Resources/World). <see cref="WorldGenerator"/> reads <see cref="generation"/>
/// (with the stencil Composition.txt and the handmade tiles in Tiles/); <see cref="WorldSystem"/> the settlement,
/// unit and provision rules. All numbers are prototype proposals (roadmap D07).
/// </summary>
[CreateAssetMenu(fileName = "World", menuName = "Game Object/World Settings", order = 11)]
public class WorldSettings : ScriptableObject
{
    public WorldGenSettings generation = new WorldGenSettings();
    public SettlementRules settlements = new SettlementRules();
    public List<UnitSpec> units = new List<UnitSpec>();
    public ProvisionRules provisions = new ProvisionRules();
    public ExpeditionSettings expeditions = new ExpeditionSettings();

    public UnitSpec Unit(string id) => units.Find(u => u != null && string.Equals(u.id, id, StringComparison.OrdinalIgnoreCase));

    [Header("Beyond the walls")]
    [Tooltip("The world map opens when this technology is researched; before it only the Capital is known. Empty: open from the start.")]
    public string mapTechnology = "Pathfinder Training";
    [Tooltip("Roads, Resonance Anchors, promotions, envoys and claims need this technology (lookouts need something to report to).")]
    public string unlockTechnology = "Reconstruction";
    [Tooltip("Units that set out with the survivors when a world begins (unit ids).")]
    public List<string> startingUnits = new List<string>();
}

/// <summary>The generator's input: plain data, so generation is tested without a scene.</summary>
[Serializable]
public class WorldGenSettings
{
    [Header("Composition")]
    [Tooltip("The composition stencil, a TextAsset under Resources (S1-S7 sectors, P seams, W ocean).")]
    public string stencil = "World/Composition";
    [Tooltip("Folder under Resources holding the handmade tiles (.txt).")]
    public string tilesFolder = "World/Tiles";
    [Tooltip("Meso cells across one stencil cell.")]
    public int cellsPerStencilCell = 8;
    [Tooltip("Width of the ocean apron around the stencil, in stencil cells.")]
    public float apron = 1.5f;
    [Tooltip("How far region borders wander from the stencil's straight lines, in stencil cells.")]
    public float warp = 0.3f;
    [Tooltip("0: a new world every run. Anything else: the same world every run.")]
    public int fixedSeed;

    [Header("Land and sea")]
    [Range(0f, 1f)] public float seaLevel = 0.3f;
    [Tooltip("Width of the band, in stencil cells, in which the coastline wanders between P and the ocean.")]
    public float coastBand = 0.7f;
    [Tooltip("Narrowest the ocean ring around the mainland may be, in cells.")]
    public int oceanRingWidth = 3;
    [Tooltip("Ground of the open ocean, the shallows by the coast and lakes.")]
    public string oceanTerrain = "deep-ocean", shallowsTerrain = "shallows", lakeTerrain = "lake";
    [Tooltip("P's own ground by climate, blended with the neighbouring biomes near them.")]
    public List<TerrainRule> connective = new List<TerrainRule>();
    [Tooltip("How far into P, in cells, the neighbouring biomes' ground reaches.")]
    public int seamReach = 5;

    [Header("Authority")]
    public int capitalAuthorityRadius = 4;

    [Header("Water")]
    [Tooltip("Wet, procedural depressions per world; drainage determines their actual lake footprint.")]
    public int naturalBasins = 12;
    [Tooltip("Rain gathered (in cells of full moisture) before a watercourse shows as a river.")]
    public float riverThreshold = 28f;
    [Tooltip("Rivers this large still show on the atlas (macro) view.")]
    public float majorRiverThreshold = 110f;
    [Tooltip("Depressions deeper than this (in height) hold a lake.")]
    public float lakeDepth = 0.035f;
    [Tooltip("Smallest lake kept, in cells.")]
    public int minLakeCells = 3;
    [Tooltip("Largest lake kept, in cells: a bigger hollow stays land and its water crosses it.")]
    public int maxLakeCells = 80;

    [Header("Magic")]
    [Tooltip("Strength and reach (cells) of a Coherence Seed.")]
    public float seedStrength = 0.35f;
    public float seedRadius = 14f;
    [Tooltip("Dissonance Seeds in the world, their strength and reach (cells).")]
    public int dissonanceSeeds = 6;
    public float dissonanceStrength = 0.4f;
    public float dissonanceRadius = 10f;
    [Tooltip("Sacred Sites: fixed Coherence maxima that no Age moves.")]
    public int sacredSites = 4;
    [Tooltip("Legacy serialized setting; downhill leylines now continue until their first water outlet.")]
    public int leylineLength = 60;
    [Tooltip("Coherence corridor radius in meso cells; fades smoothly away from a leyline.")]
    public int leylineDriftRadius = 5;
    [Range(0f, 1f)] public float leylineDriftStrength = 0.22f;
    [Tooltip("Lunehymn above which a river reach shows silver, and the share kept per cell downstream.")]
    public float silverThreshold = 0.25f;
    [Range(0f, 1f)] public float silverRetention = 0.93f;
    [Tooltip("Share of a reach's Lunehymn kept when its leyline moves away at an Age change.")]
    [Range(0f, 1f)] public float silverResidue = 0.4f;
    [Tooltip("How much of the magic flow each Age allows to be used (by Age number; the last value holds beyond).")]
    public List<float> magicAccess = new List<float> { 0.35f, 0.6f, 0.8f, 1f };

    [Header("Start")]
    [Tooltip("Cells around the capital explored and known from the start.")]
    public int startExploreRadius = 1;
    public int startRevealRadius = 4;
    [Tooltip("Feature placed on the capital's cell.")]
    public string capitalFeature = "capital";

    [Header("Content")]
    public List<TerrainSpec> terrains = new List<TerrainSpec>();
    public List<BiomeSpec> biomes = new List<BiomeSpec>();
    public List<SectorSpec> sectors = new List<SectorSpec>();
    public List<FeatureSpec> features = new List<FeatureSpec>();
    public List<GrandfieldSpec> grandfields = new List<GrandfieldSpec>();
    public List<EnclaveSpec> enclaves = new List<EnclaveSpec>();
    public List<ThreatSpec> threats = new List<ThreatSpec>();
    public NexusRules nexus = new NexusRules();

    public GrandfieldSpec Grandfield(string id) => grandfields.Find(g => g != null && string.Equals(g.id, id, StringComparison.OrdinalIgnoreCase));

    public EnclaveSpec Enclave(string id) => enclaves.Find(e => e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));

    public ThreatSpec Threat(string id) => threats.Find(t => t != null && string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase));

    public TerrainSpec Terrain(string id) => terrains.Find(t => t != null && string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase));

    public BiomeSpec Biome(string id) => biomes.Find(b => b != null && string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase));

    public SectorSpec Sector(string id) => sectors.Find(s => s != null && string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase));

    public FeatureSpec Feature(string id) => features.Find(f => f != null && string.Equals(f.id, id, StringComparison.OrdinalIgnoreCase));

    public float MagicAccess(int age) => magicAccess.Count == 0 ? 1f : magicAccess[Math.Max(0, Math.Min(age, magicAccess.Count - 1))];
}
