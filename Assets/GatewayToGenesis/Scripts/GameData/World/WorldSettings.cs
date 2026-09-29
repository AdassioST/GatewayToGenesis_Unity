using System;
using System.Collections.Generic;
using System.Linq;
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
    [Tooltip("Composure strain eased per Seventh for an expedition walking or camped on this ground (a moonlit grove's light), on top of its recovery (Expeditions.Solace).")]
    public float solace;
}

/// <summary>
/// One ground a Macro Biome (or an intersection) may lay down, and the climate it needs. A cell takes one of the rules whose elevation
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
/// by the seed, and the rest is filled procedurally and blended into the intersections.
/// </summary>
[Serializable]
public class MacroBiomeSpec
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
/// A quadrant of the composition stencil (Q1-Q7): its biome catalog, shared among all its slots (each same-quadrant
/// block of the stencil is one slot), and the regional character every slot keeps.
/// </summary>
[Serializable]
public class QuadrantSpec
{
    [Tooltip("The stencil's token (Q1-Q7).")]
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("Biomes shuffled into the quadrant's slots, each at most once (unless repeats are allowed). With more biomes than slots, the seed draws which ones appear.")]
    public List<string> macroBiomes = new List<string>();
    [Tooltip("A placeholder catalog smaller than its slot count may reuse biomes.")]
    public bool allowRepeats;
    [Tooltip("Height of the range raised along the quadrant's own intersections (0: none). A mountainous quadrant reads as one range.")]
    [Range(0f, 0.6f)] public float backbone;
    [Tooltip("Coherence Seeds placed in each of its slots.")]
    public int coherenceSeeds = 1;
    [Tooltip("An offshore quadrant: the intersections around it become strait and sea instead of land.")]
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
    public List<string> macroBiomes = new List<string>();
    [Tooltip("Where it would rather be: among the best cells of this field.")]
    public Preference prefers;
    [Tooltip("Cells kept between two of a kind.")]
    public int spacing = 3;
    [Tooltip("Seen from afar once your people's sight reaches it (a landmark on the horizon); never through the fog: before that, only rumours tell of it (WorldRumours).")]
    public bool visibleFromAfar;
    [Tooltip("What travellers say of it before anyone has found it, never its name (\"a white tower that sings in the wind\"). Empty: a line made from its kind. A feature with one makes it worth a rumour.")]
    public string rumour;
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
    public List<string> macroBiomes = new List<string>();
    [Range(0f, 1f)] public float minimumCoherence;
    [Range(0f, 1f)] public float minimumMagicalFertility;
    public bool requiresFreshwater;
    [Tooltip("Cell steps from the capital.")]
    public int minDistance = 6;
    [Tooltip("Flat production per second at full density once an Outpost extracts it.")]
    public float baseYield = 0.3f;
}

/// <summary>What a resource site looks like before a survey decodes it (the hint an expedition brings back from afar).</summary>
public enum ResourceKind { Crop, Flora, Fauna, Mineral, Light, Water, Bloom }

/// <summary>
/// How an Eleos Bloom (vault: Eleos Bloom.md) lives off the Emotional Residue around it. Listeners (Tier 1) drink
/// ambient feeling and keep Dissonance from stagnating in the soil; Healers (Bioluminescence-Dominant) spend it all on
/// light and shelter the weary; Predators (Movement-Dominant) spend it on movement and lure travellers in.
/// </summary>
public enum BloomNiche { Listener, Healer, Predator }

/// <summary>
/// A resource site (<see cref="WorldResources"/>): a lesser patch of one thing that makes a tile worth having, the small
/// cousin of a Resource Grandfield (one to a few cells, no Outpost needed). It is sighted when an expedition passes
/// (only its kind is known: "unidentified flora") and identified when its cell is surveyed. Identified, it raises the
/// land's value (City Development, what society adopts first), yields while held, can be harvested by an expedition as
/// cargo without claiming the tile (on ground another authority holds, that raises its grievances), may give seeds to
/// plant on fertile land you hold, and makes its neighbours fairer or uglier and more or less coherent. Every number is
/// a proposal (Canon Gaps.md).
/// </summary>
[Serializable]
public class ResourceSiteSpec
{
    public string id;
    public string name;
    public ResourceKind kind;
    [TextArea(1, 4)] public string description;
    public Color color = new Color(0.8f, 0.8f, 0.6f);
    [Tooltip("The usable resource it is known for (the one it yields and gives when harvested); empty for sites that give nothing to take.")]
    public string resource;
    [Tooltip("The species living here (kind Fauna: every one names its species; a predatory bloom may name its own). Empty for everything else.")]
    public string species;

    [Header("Placement")]
    [Tooltip("The Age (number) in which sites of this kind appear.")]
    public int minAge;
    public int count = 4;
    [Tooltip("Cells in one patch (1 a single spire or well; a few for terraces and groves).")]
    public int size = 1;
    [Tooltip("Cells kept between two of a kind.")]
    public int spacing = 6;
    [Tooltip("Cell steps from the capital.")]
    public int minDistance = 3;
    [Tooltip("Ground it may sit on (empty: any passable land).")]
    public List<string> terrains = new List<string>();
    [Tooltip("Biomes it may sit in (empty: any).")]
    public List<string> macroBiomes = new List<string>();
    [Tooltip("Cover it must stand in (Heartwood Hollow only in a Deep Forest); empty: in cover or out of it.")]
    public List<string> covers = new List<string>();
    [Tooltip("Ground that must lie within nearReach cells (peaks for highland rice); empty: none needed.")]
    public List<string> nearTerrains = new List<string>();
    public int nearReach = 2;
    [Range(0f, 1f)] public float minElevation, maxElevation = 1f;
    [Range(0f, 1f)] public float minMoisture;
    [Range(0f, 1f)] public float minimumFertility;
    [Range(0f, 1f)] public float minimumCoherence;
    [Tooltip("At most this much Coherence (sites of wounded ground).")]
    [Range(0f, 1f)] public float maximumCoherence = 1f;
    [Tooltip("At least this much Dissonance.")]
    [Range(0f, 1f)] public float minimumDissonance;
    public bool requiresFreshwater;
    [Tooltip("On or beside a silver reach, or a leyline (either will do).")]
    public bool requiresSilverOrLeyline;
    [Tooltip("At most this warm (0-1; the Taiga and high ground are cold): ice and snow lineages. 1: any.")]
    [Range(0f, 1f)] public float maxTemperature = 1f;
    [Tooltip("On the sea's shore: beside deep ocean or shallows (salt water, not a lake).")]
    public bool coastal;
    [Tooltip("Only at a high-Coherence sacred site or leyline convergence. Reserved for unmerged Elemental Sprites.")]
    public bool requiresCoherentRefuge;
    [Tooltip("Minimum natural land desirability: the generator's fertility, Coherence and magical fertility score (before this resource's own auras).")]
    [Range(0f, 1f)] public float minimumDesirability;
    [Tooltip("Sacred ground or its immediate neighbouring cells.")]
    public bool requiresSacred;
    [Tooltip("Seen through the fog before it is reached (a god-ray, a red spire on the skyline).")]
    public bool visibleFromAfar;

    [Header("Land value")]
    [Tooltip("What it adds to the land's value once identified: City Development points to a settlement working it, and how eagerly society adopts it. Negative: a blight that lowers it.")]
    public float landValue = 2f;
    [Tooltip("Flat production per second while its cell is held (yours or your Outpost's).")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();

    [Header("Neighbours")]
    [Tooltip("Beauty of its own cell (-1 to 1).")]
    [Range(-1f, 1f)] public float beauty;
    [Tooltip("Beauty it lends (or takes from) the cells around it, fading with distance.")]
    [Range(-1f, 1f)] public float beautyAura;
    [Tooltip("Coherence it lends (or takes from) its own cell and the cells around it, fading with distance.")]
    [Range(-0.5f, 0.5f)] public float coherenceAura;
    [Tooltip("Land fertility it lends its own cell and the cells around it (slimes singing the soil), fading with distance.")]
    [Range(-0.5f, 0.5f)] public float fertilityAura;
    [Tooltip("Cells its aura reaches.")]
    public int auraRadius = 1;

    [Header("Harvest (expeditions carry it off as cargo)")]
    [Tooltip("What an expedition gathers here once an Age (empty: nothing can be taken).")]
    public List<ResourceAmount> harvest = new List<ResourceAmount>();
    [Tooltip("The harvest also yields seeds an expedition carries to plant on fertile land you hold.")]
    public bool seeds;
    [Tooltip("Grievances a harvest raises with the authority that holds the ground (none in the wilderness or on your own land).")]
    public float grievance = 10f;
    [Tooltip("Why nothing can be taken (for the card), when the harvest is empty.")]
    public string untouchable;

    [Header("Planting (seeds carried to your land)")]
    [Tooltip("Land fertility the cell needs.")]
    [Range(0f, 1f)] public float plantFertility = 0.4f;
    [Tooltip("Coherence the cell needs.")]
    [Range(0f, 1f)] public float plantCoherence;
    [Tooltip("A planted patch yields this share of a wild one's yields, times the cell's fertility over 0.5.")]
    [Range(0f, 1.5f)] public float plantedShare = 0.6f;
    [Tooltip("Land fertility the planting adds to its cell (earth-beans fix nitrogen; peaches drain the soil).")]
    [Range(-0.3f, 0.3f)] public float plantedFertility;

    [Header("Eleos Bloom (kind Bloom only; WorldResources, the Eleos settings)")]
    public BloomNiche niche;
    [Tooltip("Its cocktail (EmotionalAlchemy): the notes of the Emotional Register it eats, in proportion (weights become shares). It thrives where they run, drinks them out of the land's imprint (a healer turns the wounds it drinks into their healthy pair), and its lineage evolves from here (EmotionalEvolution). Empty: it feeds on residue of any kind.")]
    public List<FeelingWeight> flavors = new List<FeelingWeight>();
    [Tooltip("How exactly its cocktail must be made (0 a generalist that eats any of its notes and spreads well; 1 a purist that eats only the exact cocktail, grows sparsely, and is superloaded where it finds it).")]
    [Range(0f, 1f)] public float specificity = 0.3f;
    [Tooltip("Emotional Residue (0-1) at which it thrives. Below it the bloom dims, its gifts and its menace with it; with none it withers (it can neither move nor shine). 0: it needs none.")]
    [Range(0f, 1f)] public float residueNeed = 0.3f;
    [Tooltip("Dissonance it lends (or, negative, drinks) in its own cell and the cells around it, fading with distance.")]
    [Range(-0.5f, 0.5f)] public float dissonanceAura;
    [Tooltip("Predators: danger it casts over its cell and the cells around it (travel slows, ambushes and lures).")]
    [Range(0f, 1f)] public float dangerAura;
    [Tooltip("Healers: Composure strain eased per Seventh for an expedition on or beside it; a party camped there rests as in a settlement.")]
    public float soothe;
    [Tooltip("Any site: Composure strain eased per Seventh for an expedition on it (fading over its aura radius around it), on top of its recovery. Glimmerfern's light along silver water (Expeditions.Solace).")]
    public float solace;

    [Header("Growing by itself, Echo by Echo (sterile blooms: Fated and Forsaken Flowers, Glimmerfern)")]
    [Tooltip("Grows by itself: never placed with its Age like other sites; at the world's start (up to count) and at every Echo of the game's clock, new patches sprout on ground that fits until count stand. Sterile blooms give no seeds.")]
    public bool sprouts;
    [Tooltip("New patches at most per Echo (0: up to count at once).")]
    public int sproutPerEcho = 1;
    [Tooltip("History the ground must hold (0-1: ruins, Atonalis nests, Old World roads, landmarks, Sacred Sites, old settlements).")]
    [Range(0f, 1f)] public float minimumHistory;
    [Tooltip("At most this much hurtful residue (Fated Flowers sprout golden only where sorrow has not reached them).")]
    [Range(0f, 1f)] public float maximumHurt = 1f;
    [Tooltip("Ways to grow (any one will do; none set: none needed). Hurtful residue the ground holds (0-1: grief of ruins, strained people, Dissonance)...")]
    [Range(0f, 1f)] public float minimumHurt;
    [Tooltip("...sites it grows beside, within nearReach cells (Glimmerfern around a field of Forsaken Flowers)...")]
    public List<string> nearSites = new List<string>();
    [Tooltip("...within this many cells of a silver river (a leyline running down a real river; 0: not a way to grow)...")]
    public int silverRiverReach;
    [Tooltip("...or within this many cells of a lake a silver river runs into (Glimmerfern all along its shores; 0: not a way to grow).")]
    public int silverLakeReach;
    [Tooltip("A sprouted patch whose ways to grow are gone fades by this much each Echo, and is gone at 1 (0: it never fades).")]
    [Range(0f, 1f)] public float fadePerEcho;

    [Header("Turning, Echo by Echo (Fated Flowers decay into Forsaken ones; Forsaken ones heal)")]
    [Tooltip("The site it turns into once its turn has run its course (empty: it never turns).")]
    public string turnsInto;
    [Tooltip("Its turn runs while the patch's hurtful residue is at least this (0: hurt does not turn it)...")]
    [Range(0f, 1f)] public float turnHurt;
    [Tooltip("...or while its Coherence is at least this (0: Coherence does not turn it).")]
    [Range(0f, 1f)] public float turnCoherence;
    [Tooltip("Coherence at which it holds against a turn by hurt (coherent ground keeps Fated Flowers golden; 0: never).")]
    [Range(0f, 1f)] public float holdCoherence;
    [Tooltip("How far its turn runs each Echo it is due (1: it turns at once); it recedes as fast when it is not.")]
    public float turnPerEcho = 0.35f;
    [Tooltip("Added per point of the patch's Dissonance each Echo (positive: Dissonance hastens decay; negative: it holds back healing).")]
    public float turnPerDissonance;

    [Header("Drifting, Echo by Echo (blooms that move: WorldResources.Drift)")]
    [Tooltip("Chance each Echo that the patch creeps a cell: it lets go of its poorest cell and takes the best ground beside it (more residue; for Fated Flowers more history and less sorrow). A withering bloom cannot move. Movement-dominant blooms (predators) drift most, the healers that spend their feeling on light least. 0: it never moves.")]
    [Range(0f, 1f)] public float driftPerEcho;

    [Header("Growing near settlements (volunteer blooms: WorldResources.Volunteer)")]
    [Tooltip("The mood of a settlement that draws it up once Lumen Seeds reach the ground there (seeds transplanted nearby, pollinators, or high Coherence): Content settlements grow the gentle blooms, Suffering ones the blooms of grief and shame. None: no mood draws it.")]
    public BloomDraw drawnBy;
    [Tooltip("Districts (ids) whose life draws it up beside them, whatever the mood (the Indulgent District's revels and Lust Berries; the Weaver District's songs and Xochi-Singers).")]
    public List<string> districts = new List<string>();
    [Tooltip("How likely it is picked among the blooms a settlement draws (relative).")]
    public float volunteerWeight = 1f;

    [Header("Events")]
    [Tooltip("An event line for expeditions: the first to identify one of these offers the first knot, the next the second...")]
    public List<string> expeditionStories = new List<string>();
}

/// <summary>What in a settlement's life draws a bloom up beside it (<see cref="ResourceSiteSpec.drawnBy"/>).</summary>
public enum BloomDraw { None, Content, Suffering, Either }

/// <summary>
/// Emotional Residue (vault: Eleos Bloom.md), the minute harmonic traces feeling leaves in a place, which Eleos Blooms
/// feed on (<see cref="WorldResources.Residue"/>): people living nearby (more where they are strained), the ruins of the
/// fallen, and the Dissonance no one has metabolized. Every number is a proposal (Canon Gaps.md).
/// </summary>
[Serializable]
public class EleosSettings
{
    [Header("Emotional Residue: where it comes from")]
    [Tooltip("Residue everywhere on land: the wild's own feeling (beasts, sprites, weather), enough to keep a bloom dimly alive.")]
    public float ambientResidue = 0.1f;
    [Tooltip("How Eleos Bloom lineages live and evolve on their cocktails (EmotionalEvolution): niche cost, superload, how often and how far they step.")]
    public EvolutionSettings evolution = new EvolutionSettings();
    [Tooltip("Residue at a settlement's own cell: the Capital, a Major Settlement, any other.")]
    public float capitalResidue = 0.5f, majorResidue = 0.4f, settlementResidue = 0.25f;
    [Tooltip("Added at a settlement's cell at full strain (its people's Composure; a place in grief feeds more).")]
    public float strainResidue = 0.3f;
    [Tooltip("Cells a settlement's residue reaches, fading with distance.")]
    public int settlementReach = 3;
    [Tooltip("Residue at a ruin (the grief of the fallen), and the cells it reaches.")]
    public float ruinResidue = 0.45f;
    public int ruinReach = 2;
    [Tooltip("Residue per point of Dissonance (feeling left unmetabolized in the soil; the blooms' own touch not counted).")]
    public float dissonanceResidue = 0.6f;
    // Hurtful residue (WorldTile.hurt) is the part of it that is sorrow: the ruins, the strain, the Dissonance.

    [Header("History: where the past weighs on the land (where Rose Seeds and Fated Flowers take)")]
    [Tooltip("History at a ruin, and the cells it reaches.")]
    public float ruinHistory = 0.8f;
    public int ruinHistoryReach = 3;
    [Tooltip("History at an Atonalis threat (where Atonalis live and fall, and their Rose Seeds with them), and the cells it reaches.")]
    public float atonalisHistory = 0.7f;
    public int atonalisReach = 2;
    [Tooltip("Threats (ids) that are Atonalis.")]
    public List<string> atonalisThreats = new List<string> { "atonalis-nest" };
    [Tooltip("History on a broken Old World road, a landmark (feature) and its neighbours, and a Sacred Site (over 2 cells).")]
    public float oldRoadHistory = 0.3f, featureHistory = 0.6f, sacredHistory = 0.5f;
    [Tooltip("History a settlement gathers per Age it has stood (at most settlementHistoryMax), over 2 cells.")]
    public float settlementHistoryPerAge = 0.1f, settlementHistoryMax = 0.5f;
    [Tooltip("Ground that is history itself (ruin fields, skeletal orchards, golden ash, the rift scar), and how much.")]
    public List<string> historicTerrains = new List<string> { "ruin-field", "skeletal-orchard", "ash-plains", "rift-scar" };
    public float historicTerrainHistory = 0.4f;

    [Header("Vigor")]
    [Tooltip("A bloom below this vigor withers: no yields, no gifts, no menace.")]
    [Range(0f, 1f)] public float witherBelow = 0.2f;
    [Tooltip("Share of a harvest a withering bloom still gives (its fallen leaves).")]
    [Range(0f, 1f)] public float witheredHarvest = 0.25f;

    [Header("Volunteer blooms: what a settlement's life draws up around it (WorldResources.Volunteer)")]
    [Tooltip("A settlement whose Composure strain is at most this is Content (its people's ease draws up the gentle blooms)...")]
    public float contentStrain = 10f;
    [Tooltip("...and one at this strain or more is Suffering (grief and shame draw up the dark ones; the legends' Fractured state). Between the two, only districts draw blooms.")]
    public float sufferingStrain = 40f;
    [Tooltip("Lumen Seeds must reach the ground first (Eleos Bloom.md: physical inheritance). Seeds planted within this many cells of the settlement carry them in (rice or any other: lumen grains ride in the soil and on the tools)...")]
    public int seedReach = 3;
    [Tooltip("...or the ground around it holds this much Coherence on average (sprite pollinators live where the Loom is whole)...")]
    [Range(0f, 1f)] public float seedCoherence = 0.65f;
    [Tooltip("...or a pollinator (a species drawn to blooms, SpeciesSpec.blooms) lives in its Macro Biome at this abundance or more.")]
    [Range(0f, 1f)] public float pollinatorAbundance = 0.3f;
    [Tooltip("Cells from the settlement a volunteer bloom takes root (never on the settlement's own cell).")]
    public int volunteerReach = 2;
    [Tooltip("Chance each Echo that a seeded settlement draws up a new bloom.")]
    [Range(0f, 1f)] public float volunteerChance = 0.5f;
    [Tooltip("Volunteer blooms one settlement keeps around it at most (a district adds districtVolunteers).")]
    public int volunteersPerSettlement = 2, districtVolunteers = 1;
    [Tooltip("Cells in a volunteer patch at most (its spec's size if smaller).")]
    public int volunteerSize = 2;
    [Tooltip("A volunteer whose settlement no longer draws it (the mood turned, the district changed, the settlement fell) fades by this each Echo, and is gone at 1.")]
    [Range(0f, 1f)] public float volunteerFadePerEcho = 0.34f;

    [Header("The food web: creatures drawn to blooms (SpeciesSpec.blooms, WorldEcology)")]
    [Tooltip("Cells around a thriving bloom within which the creatures it draws count it.")]
    public int drawReach = 2;
}

/// <summary>
/// Cover (<see cref="WorldCover"/>): a multi-cell patch laid over the ground, a Deep Forest or a Mistfen, that makes the
/// land harder to explore and worth holding. It can block sight (nothing beyond it is seen, and whatever stands inside
/// stays hidden until its cell is explored), narrow sight from within, slow travel and surveys, wear parties down,
/// raise the odds of survey finds, add forage, and yield while held. Placed once with the world. Every number is a
/// proposal (Canon Gaps.md).
/// </summary>
[Serializable]
public class CoverSpec
{
    public string id;
    public string name;
    [TextArea(1, 4)] public string description;
    [Tooltip("Tint laid over the ground (alpha: how strongly).")]
    public Color color = new Color(0.1f, 0.3f, 0.12f, 0.6f);

    [Header("Placement")]
    public int count = 5;
    [Tooltip("Cells in one patch, from min to max.")]
    public int minSize = 4, maxSize = 10;
    [Tooltip("Cells kept between two patches of a kind.")]
    public int spacing = 8;
    [Tooltip("Cell steps from the capital.")]
    public int minDistance = 4;
    [Tooltip("Ground it grows over (empty: any passable land).")]
    public List<string> terrains = new List<string>();
    [Range(0f, 1f)] public float minMoisture;

    [Header("Exploring it")]
    [Tooltip("Nothing beyond it can be seen, and what stands inside is hidden until its cell is explored.")]
    public bool blocksSight = true;
    [Tooltip("Micro hexes a party sees from inside (-1: its own sight).")]
    public int sightInside = 1;
    [Tooltip("Travel through it is multiplied by this.")]
    public float travel = 1.5f;
    [Tooltip("Surveys inside take this many times longer.")]
    public float survey = 1.4f;
    [Tooltip("Attrition per Seventh a party takes inside (thorns, bog, choking ash).")]
    public float hardship;
    [Tooltip("Survey events and spare finds are this many times likelier inside.")]
    public float finds = 1.5f;

    [Header("Holding it")]
    [Tooltip("Beauty it adds to its cells (-1 to 1).")]
    [Range(-1f, 1f)] public float beauty;
    [Tooltip("Governance difficulty is multiplied by this (dense ground is harder to administer).")]
    public float governance = 1.2f;
    [Tooltip("Flat production per second of each held cell.")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();
    [Tooltip("Added to the ground's forage.")]
    public List<ResourceAmount> forage = new List<ResourceAmount>();
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
    [Tooltip("Domestication only: the Pure Light share (1 - Auric Structure) a species needs for it to keep it (0: it keeps any creature; the Sprite-Light Conclave keeps Pure Light ones). WorldEnclaveEcology.")]
    [Range(0f, 1f)] public float keepsPureLight;
    [Tooltip("Domestication only: a species it farms and trades to your settlements (the Great Plague's moths: WorldGreatPlague). Empty: none.")]
    public string trades;
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
    [Tooltip("How strongly a watch district beside it counts it (WorldTributaries).")]
    [Range(0f, 1f)] public float strength = 0.6f;
    [Tooltip("How far a watch district still counts it as near (cells). Threats cast no danger aura: their bands roam, and explorers learn of them by their signs.")]
    public float radius = 6f;
    public int minDistance = 12;

    [Header("What it sends out (WorldBattles)")]
    [Tooltip("Red (Hostile): its bands hunt your units. Orange (Wary): they turn hostile when one of your units comes adjacent or passes close by.")]
    public EnemyStance stance = EnemyStance.Hostile;
    [Tooltip("A bestiary species it sends out (empty: its own beings, described below).")]
    public string species;
    [Tooltip("Its own beings (when no species): their name, size, Auric Structure and behaviour.")]
    public string beingName;
    public CreatureSize beingSize = CreatureSize.Medium;
    [Range(0f, 1f)] public float beingStructure = 0.4f;
    public CreatureDiet beingDiet = CreatureDiet.Carnivore;
    public CreatureSubgroup beingSubgroup = CreatureSubgroup.Marauder;
    [Tooltip("Bindings its beings may carry; each band draws one as its primary (empty: any of the seven). Every Atonalis carries one.")]
    public List<SpellBinding> beingBindings = new List<SpellBinding>();
    [Tooltip("Eight-Born Paths its beings may walk; each band draws one (empty: by the vault's shares). The Path sets how it hunts (AtonalPaths).")]
    public List<AtonalPath> beingPaths = new List<AtonalPath>();
    [Tooltip("Creatures in a band, from min to max.")]
    public int bandMin = 2, bandMax = 5;
    [Tooltip("Bands out at once from each of its sites (0: it sends none).")]
    public int bands = 1;
    [Tooltip("Sevenths before a lost band is replaced.")]
    public float respawnSevenths = 14f;
    [Tooltip("Cells a band roams from its site.")]
    public int roam = 3;
}

/// <summary>How an enemy on the map meets your units: red hostile, orange wary until provoked. Saved by index: append only.</summary>
public enum EnemyStance { Hostile, Wary, Timid }

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
    [Tooltip("A Religious Haven needs a Sacred Site within this many cells, or this much Coherence, or this much Vibrational Density (Arcanoria.md: havens yield by the place's density).")]
    public int havenSacredReach = 2;
    [Range(0f, 1f)] public float havenCoherence = 0.75f;
    [Range(0f, 1f)] public float havenDensity = 0.7f;

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
    [Tooltip("City Development per point of land value from the identified resource sites a settlement works.")]
    public float resourceWeight = 1.5f;
    [Tooltip("Most City Development resource sites add to one settlement (a blight can take as much).")]
    public float resourceCap = 18f;

    [Header("Borders")]
    [Tooltip("Your civilization's colour: the outline of its borders in the default view (enclaves use their own colour).")]
    public Color borderColor = new Color(1f, 0.86f, 0.52f, 0.8f);

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

    [Header("Desirability and tributaries")]
    [Tooltip("Where people want to live: settlements grow faster on desirable ground and tributaries develop toward it (WorldDesirability).")]
    public DesirabilityRules desirability = new DesirabilityRules();
    [Tooltip("Outskirt tributaries: minor hubs raised inside your authority that serve a settlement, and their districts (WorldTributaries).")]
    public TributaryRules tributaries = new TributaryRules();
    [Tooltip("Settlement Composure (a legend's five states), loss, ruins and the Old World: danger, withering and pillage strain settlements, the Surrendered fall into ruins (never the Capital), ruins are investigated for what the failure left behind, and the world begins with Old World ruins and broken roads (WorldRuins).")]
    public LossRules loss = new LossRules();

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
public enum SeatKind { Capital, Major, TradeNexus, TradeNode, Town, Haven, Grandfield, Outpost, Enclave, Tributary }

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
    [Tooltip("Micro hexes per Seventh it settles at full Capital population while it has room and the administration has capacity to spare (below 1: the chance of settling one each Seventh). A cell is adopted once all seven of its hexes are settled.")]
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

    [Header("Settling hex by hex (who goes out to live on the land)")]
    [Tooltip("Citizens the Capital needs before society settles any land by itself.")]
    public int adoptionMinPopulation = 25;
    [Tooltip("Citizens at which society settles at the seats' full pace; between the minimum and this the chance rises from adoptionLeastShare.")]
    public int adoptionFullPopulation = 100;
    [Tooltip("Share of the seats' pace at the minimum population.")]
    [Range(0f, 1f)] public float adoptionLeastShare = 0.2f;

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
    [Tooltip("Added per point of land value of an identified resource site (a blight subtracts).")]
    public float resourcePriority = 0.12f;
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
        new SeatSpec(SeatKind.Tributary, 0.45f, 3.5f, 5, 0.2f, 1.5f, true),
    };
}

/// <summary>
/// Where people want to live (<see cref="WorldDesirability"/>): a cell's beauty, Coherence, magical and land
/// fertility and leylines, blended with the hexes around it, less its danger and dissonance. Settlements grow faster
/// on desirable ground; tributaries develop toward it. Every number is a proposal.
/// </summary>
[Serializable]
public class DesirabilityRules
{
    [Header("What people look for (weights, shared out of their sum)")]
    public float beautyWeight = 0.2f;
    public float coherenceWeight = 0.25f;
    public float magicWeight = 0.15f;
    public float landWeight = 0.25f;
    public float leylineWeight = 0.15f;
    [Tooltip("Share of a cell's desirability that comes from the six hexes around it (the neighbourhood people see from their door).")]
    [Range(0f, 1f)] public float surroundings = 0.4f;

    [Header("What drives them away (subtracted per unit)")]
    public float dangerPenalty = 0.4f;
    public float dissonancePenalty = 0.5f;

    [Header("Growth")]
    [Tooltip("City Development grows this many times its rate on the least desirable ground...")]
    public float slowestGrowth = 0.5f;
    [Tooltip("...and this many times on the most desirable.")]
    public float fastestGrowth = 1.6f;

    /// <summary>How much faster than its rate City Development grows at a desirability (0-1).</summary>
    public float GrowthFactor(float desirability) => slowestGrowth + (fastestGrowth - slowestGrowth) * Math.Max(0f, Math.Min(1f, desirability));
}

/// <summary>
/// Settlement Composure, loss and ruins (<see cref="WorldRuins"/>): loss is transformation, not only punishment. A
/// settlement's Composure is a legend's, unified: the same strain (0-100), the same five states (Pristine, Clouded,
/// Fractured, Spiraling, Surrender) and the same thresholds (<see cref="composure"/>). Danger at its cell, withering (a
/// tributary no road joins to a hub) and pillage strain it; it eases toward the Clouded baseline when nothing presses.
/// Fractured and Spiraling settlements yield less (the tuning's council factors); at Surrender a settlement falls into a
/// ruin that stays on the map. The Capital can be Spiraling but never Surrenders. Every number is a proposal.
/// </summary>
[Serializable]
public class LossRules
{
    [Tooltip("The five states and where each begins, the Clouded baseline strain eases toward and how fast (restRecovery), and what Fractured and Spiraling settlements yield (fracturedCouncil, spiralingCouncil): the same numbers as a legend's Composure.")]
    public ComposureTuning composure = new ComposureTuning();

    [Header("What strains a settlement (per Seventh)")]
    [Tooltip("Danger at a settlement's cell below this does not strain it.")]
    [Range(0f, 1f)] public float dangerFloor = 0.15f;
    [Tooltip("Strain per Seventh at danger 1 (scaled from the floor up; a Militant District's watch lowers the danger first).")]
    public float dangerStrain = 6f;
    [Tooltip("Strain per Seventh on a tributary no road joins to a hub (orphaned, or its road home cut off): it withers away.")]
    public float witherStrain = 5f;
    [Tooltip("Recovery toward the baseline is this many times faster when it is joined to the Capital by road.")]
    public float networkRecovery = 1.5f;
    [Tooltip("City Development lost per point of strain added.")]
    public float developmentPerStrain = 0.25f;

    [Header("Mending")]
    [Tooltip("Paid per 10 points of strain above the baseline (rounded up) to mend a settlement's Composure at once.")]
    public List<ResourceAmount> mendCostPer10 = new List<ResourceAmount> { new ResourceAmount { resource = "Elderwood", amount = 8f }, new ResourceAmount { resource = "Food", amount = 6f } };

    [Header("Ruins: what investigating one turns up")]
    [Tooltip("Seconds of the fallen settlement's former production salvaged (its district's or its kind's yields at the development it had).")]
    public float salvageSeconds = 300f;
    public List<ResourceAmount> salvageBase = new List<ResourceAmount> { new ResourceAmount { resource = "Elderwood", amount = 10f }, new ResourceAmount { resource = "Duskstone", amount = 4f } };
    [Tooltip("Research recovered from its records: a base, plus per point of the development it had.")]
    public float researchBase = 8f, researchPerDevelopment = 0.4f;
    [Tooltip("Chance its records enlighten a technology: a base plus per point of development...")]
    [Range(0f, 1f)] public float enlightenChance = 0.3f;
    public float enlightenPerDevelopment = 0.004f;
    [Tooltip("...and this much more in a ruin of the Old World (what the world knew before it ended).")]
    public float oldWorldEnlighten = 0.15f;
    [Tooltip("Chance its people's ways survive as a civic you may adopt from the ruins; more for a town, Major Settlement or haven, and for a Weaver, Regal or Auric district.")]
    [Range(0f, 1f)] public float civicChance = 0.2f;
    public float civicSettlementBonus = 0.2f, civicCultureBonus = 0.1f;
    [Tooltip("Lyrical Fragments of discovery each legend of the investigating party earns.")]
    public int fragments = 1;
    [Tooltip("Era Score for the first investigation of each ruin.")]
    public int eraRuin = 1;

    [Header("Reclaiming what was lost")]
    [Tooltip("Era Score for reclaiming lost land of your own: a settlement founded on the ruins of one of yours, or cells that slipped from your authority brought back (once per Seventh).")]
    public int eraReclaim = 3;

    [Header("The Old World (the world ended once already)")]
    public OldWorldRules oldWorld = new OldWorldRules();
}

/// <summary>
/// What the world before the Cataclysmic Aftermath left at the start (<see cref="WorldRuins.PlaceOldWorld"/>): ruins of
/// its cities and the broken roads between them. Walking an old road is better than open ground; restoring one while
/// building a road costs little, but a restored stretch is a lesser road until rebuilt. Every number is a proposal.
/// </summary>
[Serializable]
public class OldWorldRules
{
    [Tooltip("Ruins of Old World cities, towns and outposts placed with the world.")]
    public int ruins = 9;
    [Tooltip("Fewest cell steps between two Old World ruins, and from the Capital.")]
    public int spacing = 7, capitalDistance = 4;
    [Tooltip("Development the fallen places had (a range): what their ruins salvage.")]
    public float minDevelopment = 20f, maxDevelopment = 60f;
    [Tooltip("Longest broken road (cost of the way over land) between two ruins; farther ones stay unjoined.")]
    public float maxRoadCost = 60f;

    [Header("Old roads (walked at WorldPaths.OldRoadFactor: between open ground and a road)")]
    [Tooltip("Share of a road's cost paid for a cell of old road restored while building a road.")]
    [Range(0f, 1f)] public float restoreShare = 0.35f;
    [Tooltip("A restored stretch counts this much of a rebuilt road toward City Development and a route's efficiency (and holds no Trade Node) until it is rebuilt.")]
    [Range(0f, 1f)] public float restoredWorth = 0.5f;
}

/// <summary>What a district's adjacency reads around it (<see cref="WorldTributaries.Adjacency"/>).</summary>
public enum AdjacencySource
{
    /// <summary>Each other tributary linked to it (radii touching) with the named district (any district when empty).</summary>
    District,
    /// <summary>A major hub (the Capital, a Major Settlement, a town or a haven) within linking distance.</summary>
    Hub,
    Freshwater,
    Coast,
    Leyline,
    /// <summary>A Leyline Convergence or Basin in its area.</summary>
    Junction,
    /// <summary>Average of its area (0-1).</summary>
    Coherence,
    MagicalFertility,
    LandFertility,
    /// <summary>Average beauty of its area (-1 to 1).</summary>
    Beauty,
    /// <summary>Each high, steep or impassable cell of its area.</summary>
    HighGround,
    /// <summary>Each cell of its area under cover (forest, reeds...).</summary>
    Cover,
    Grandfield,
    /// <summary>Each identified resource site in its area.</summary>
    ResourceSite,
    Road,
    TradeNexus,
    Sacred,
    /// <summary>The worst danger in its area (0-1).</summary>
    Danger,
    /// <summary>Average dissonance of its area.</summary>
    Dissonance,
    /// <summary>Each enclave within <see cref="TributaryRules.enclaveReach"/> of the rule's category (empty: the district's
    /// own, "any": every category); one whose Suzerainty you hold counts twice.</summary>
    Enclave,
}

/// <summary>A pillar, substat or morale a district raises: whole points per 50 development, x (1 + adjacency), rounded down.</summary>
[Serializable]
public class DistrictStat
{
    [Tooltip("A pillar (Aureus, Regalia, Waltz, Chorus), a substat (Innovation, Piety, Authority, Ambition, Symphony, Euphony, Arcane, Secrecy) or morale.")]
    public string stat;
    public float perFifty = 1f;

    public DistrictStat() { }

    public DistrictStat(string stat, float perFifty = 1f)
    {
        this.stat = stat;
        this.perFifty = perFifty;
    }
}

/// <summary>One adjacency of a district: what it reads around it and the bonus (a share of its effect) per unit.</summary>
[Serializable]
public class AdjacencyRule
{
    public AdjacencySource source;
    [Tooltip("For District: the district id counted (empty: any other tributary). For Enclave: the enclave category counted (empty: the district's own, \"any\": all).")]
    public string district = "";
    [Tooltip("Share of the district's effect added per unit of the source (negative: a penalty).")]
    public float bonus = 0.1f;

    public AdjacencyRule() { }

    public AdjacencyRule(AdjacencySource source, float bonus, string district = "")
    {
        this.source = source;
        this.bonus = bonus;
        this.district = district;
    }
}

/// <summary>
/// What an outskirt tributary is turned into (<see cref="WorldTributaries"/>): every tributary begins as the generalist
/// (<see cref="TributaryRules.generalist"/>) and can be upgraded to one district, one for each of the ten Enclave
/// categories (Enclave.md: Agromagical, Militant, Auric, Weaver, Domestication, Trading, Industrious, Regal, Indulgent,
/// Esoteric). Its effects scale with the tributary's development (per 10) and with its adjacency (x (1 + adjacency)).
/// </summary>
[Serializable]
public class DistrictSpec
{
    public string id;
    public string name;
    [Tooltip("The Enclave category it follows (an EnclaveFamily name); empty for the generalist.")]
    public string enclave;
    [Tooltip("A few words for its role, from its Enclave category's activities.")]
    public string role;
    [TextArea(1, 3)] public string description;
    public Color color = new Color(0.85f, 0.75f, 0.55f);
    [Tooltip("Development the tributary needs before it can be upgraded to this district.")]
    public float minDevelopment = 10f;
    public List<ResourceAmount> cost = new List<ResourceAmount>();

    [Header("Effects (scaled by adjacency)")]
    [Tooltip("Production per second per 10 development.")]
    public List<ResourceAmount> yields = new List<ResourceAmount>();
    [Tooltip("Housing for the Capital's people per 10 development.")]
    public float housing;
    [Tooltip("City Development it lends its hub at 40 development or more (less below).")]
    public float hubDevelopment;
    [Tooltip("Administrative Capacity it adds.")]
    public float capacity;
    [Tooltip("Share added to its territorial pull (and its reach grows by a cell at 0.5 or more).")]
    public float pull;
    [Tooltip("Pillars, substats or morale it raises (whole points per 50 development, x (1 + adjacency)).")]
    public List<DistrictStat> stats = new List<DistrictStat>();
    [Tooltip("Share of danger it wards off within wardRadius (a Militant District's watch).")]
    [Range(0f, 1f)] public float ward;
    public int wardRadius = 2;
    [Tooltip("Held land within wardRadius never slips back into the wilderness.")]
    public bool holdsLand;
    [Tooltip("Expedition slots it adds (x (1 + adjacency), rounded down).")]
    public int expeditionSlots;
    [Tooltip("Expeditions may form and take on companions here.")]
    public bool outfits;

    [Header("Adjacency")]
    public List<AdjacencyRule> adjacency = new List<AdjacencyRule>();
}

/// <summary>
/// Outskirt tributaries (<see cref="WorldTributaries"/>), minor civilization hubs: raised from home inside your
/// Administrative Authority, each serving a major hub (the Capital, a Major Settlement, a Developing Town or a Religious
/// Haven) within reach. They are joined to it by road, stand as Trade Nodes and seats of territorial pull, and never
/// next to another settlement. Every number is a proposal.
/// </summary>
[Serializable]
public class TributaryRules
{
    [Tooltip("The technology that lets a civilization raise tributaries.")]
    public string technology = "The Rekindling";
    public List<ResourceAmount> cost = new List<ResourceAmount> { new ResourceAmount { resource = "Food", amount = 40f }, new ResourceAmount { resource = "Elderwood", amount = 20f } };
    [Tooltip("Each tributary already standing raises the next one's cost by this share.")]
    public float costGrowth = 0.15f;

    [Header("Placement")]
    [Tooltip("Fewest cell steps to any other settlement or enclave: tributaries never stand next to one another (their districts may touch).")]
    public int spacing = 2;
    [Tooltip("A tributary stands within this many cells of the hub it serves.")]
    public int hubReach = 3;
    [Tooltip("Ground it holds around itself (brings the edge of your authority a cell further).")]
    public int authorityRadius = 1;
    [Tooltip("Tributaries each hub keeps at 0 City Development...")]
    public int capitalSlots = 3, majorSlots = 3, townSlots = 1, havenSlots = 1;
    [Tooltip("...and one more for every this much City Development of the hub.")]
    public float developmentPerSlot = 25f;

    [Header("Growth")]
    public float startDevelopment = 5f;
    [Tooltip("Development gained per Seventh toward its target (scaled by desirability, like every settlement).")]
    public float growthPerSeventh = 0.2f;
    [Tooltip("A tributary develops toward 100 x its desirability, never more than this far above its hub's City Development.")]
    public float aboveHub = 15f;
    [Tooltip("Most City Development one hub gains from all its tributaries.")]
    public float hubDevelopmentCap = 15f;

    [Header("Districts")]
    [Tooltip("The generalist every tributary begins as.")]
    public string generalist = "hamlet";
    [Tooltip("Two tributaries this many cells apart are linked (their radii touch): each counts toward the other's adjacency.")]
    public int linkDistance = 2;
    [Tooltip("Cells around a tributary it reads for adjacency (its district's radius).")]
    public int adjacencyRadius = 1;
    [Tooltip("Adjacency never goes beyond these bounds (shares of the effect).")]
    public float maxAdjacency = 1.5f, minAdjacency = -0.5f;
    [Tooltip("Changing an upgraded district again costs this many times its price...")]
    public float respecializeCost = 1.5f;
    [Tooltip("...and keeps this share of the development (the quarter is rebuilt).")]
    [Range(0f, 1f)] public float respecializeKeeps = 0.5f;
    [Tooltip("Cells within which an enclave counts toward a district's Enclave adjacency (its kindred enclave, or any for a Regal District's envoys).")]
    public int enclaveReach = 5;
    public List<DistrictSpec> districts = DefaultDistricts();

    [Header("Era Score")]
    public int eraTributary = 1, eraDistrict = 1;

    public DistrictSpec District(string id)
    {
        var spec = districts?.Find(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
        return spec ?? Defaults.Find(d => string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every district that can be chosen (the configured list, or the defaults when it is empty).</summary>
    public IEnumerable<DistrictSpec> AllDistricts => districts != null && districts.Count > 0 ? districts.Where(d => d != null && !string.IsNullOrEmpty(d.id)) : Defaults;

    /// <summary>Tributaries a hub of <paramref name="kind"/> keeps at <paramref name="development"/>.</summary>
    public int Slots(SettlementKind kind, float development)
    {
        int base_ = kind == SettlementKind.Capital ? capitalSlots : kind == SettlementKind.Major ? majorSlots : kind == SettlementKind.Town ? townSlots : kind == SettlementKind.Haven ? havenSlots : 0;
        if (base_ <= 0) return 0;
        return base_ + (developmentPerSlot > 0f ? (int)(Math.Max(0f, development) / developmentPerSlot) : 0);
    }

    private static readonly List<DistrictSpec> Defaults = DefaultDistricts();

    private static List<ResourceAmount> Costs(params (string resource, float amount)[] costs) => costs.Select(c => new ResourceAmount { resource = c.resource, amount = c.amount }).ToList();

    /// <summary>
    /// The proposed districts: the generalist Outskirt Hamlet and one district for each of the ten Enclave categories
    /// (Enclave.md, The Horizontal Worldbuilding of Enclaves). The four whose categories name a pillar raise it.
    /// </summary>
    public static List<DistrictSpec> DefaultDistricts() => new List<DistrictSpec>
    {
        new DistrictSpec
        {
            id = "hamlet", name = "Outskirt Hamlet", role = "housing", color = new Color(0.9f, 0.78f, 0.55f), minDevelopment = 0f,
            description = "Homes spilling out past the walls. Houses the Capital's people and lends its hub a little City Development; fair, fertile, watered land fills it fastest.",
            yields = Costs(("Food", 0.03f)), housing = 2f, hubDevelopment = 2f, capacity = 0.5f,
            adjacency = { new AdjacencyRule(AdjacencySource.LandFertility, 0.4f), new AdjacencyRule(AdjacencySource.Freshwater, 0.2f), new AdjacencyRule(AdjacencySource.Beauty, 0.3f),
                new AdjacencyRule(AdjacencySource.Hub, 0.2f), new AdjacencyRule(AdjacencySource.District, 0.1f, "hamlet"), new AdjacencyRule(AdjacencySource.District, 0.1f, "indulgent"),
                new AdjacencyRule(AdjacencySource.District, -0.1f, "industrious") },
        },
        new DistrictSpec
        {
            id = "agromagical", name = "Agromagical District", enclave = "Agromagical", role = "growth, health, sanitation, sustenance", color = new Color(0.55f, 0.82f, 0.36f),
            minDevelopment = 5f, cost = Costs(("Elderwood", 20f)),
            description = "Terraced fields, healers' gardens and clean water, raised so the famine years never come again. Produces Food and a little Glimmerfern and houses people in health; fertile, watered, magic-rich land feeds it, danger and dissonance waste it.",
            yields = Costs(("Food", 0.07f), ("Glimmerfern", 0.004f)), housing = 1.5f, hubDevelopment = 1f,
            adjacency = { new AdjacencyRule(AdjacencySource.LandFertility, 0.5f), new AdjacencyRule(AdjacencySource.Freshwater, 0.3f), new AdjacencyRule(AdjacencySource.MagicalFertility, 0.3f),
                new AdjacencyRule(AdjacencySource.Grandfield, 0.2f), new AdjacencyRule(AdjacencySource.District, 0.15f, "domestication"), new AdjacencyRule(AdjacencySource.District, 0.1f, "agromagical"),
                new AdjacencyRule(AdjacencySource.Danger, -0.4f), new AdjacencyRule(AdjacencySource.Dissonance, -0.3f), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "militant", name = "Militant District", enclave = "Militant", role = "tactics, weapons, mercenaries, hunters", color = new Color(0.78f, 0.36f, 0.3f),
            minDevelopment = 15f, cost = Costs(("Elderwood", 30f), ("Food", 30f)),
            description = "Walls, drill yards, outfitters and hunters' lodges. Wards off danger around it, holds the land it watches, pulls hard against rivals, adds an expedition slot and outfits expeditions; raises Ambition. Strongest on heights, where the wilds threaten and beside the forges; its noise troubles the Auric scholars.",
            yields = Costs(("Game Meat", 0.02f), ("Hides", 0.01f)), stats = { new DistrictStat("Ambition") }, housing = 0.5f, capacity = 1f, pull = 0.35f, ward = 0.45f, wardRadius = 2, holdsLand = true,
            expeditionSlots = 1, outfits = true,
            adjacency = { new AdjacencyRule(AdjacencySource.HighGround, 0.1f), new AdjacencyRule(AdjacencySource.Freshwater, 0.1f), new AdjacencyRule(AdjacencySource.Danger, 0.6f),
                new AdjacencyRule(AdjacencySource.Road, 0.15f), new AdjacencyRule(AdjacencySource.Cover, 0.05f), new AdjacencyRule(AdjacencySource.District, 0.25f, "industrious"),
                new AdjacencyRule(AdjacencySource.District, 0.15f, "militant"), new AdjacencyRule(AdjacencySource.District, -0.2f, "auric"), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "auric", name = "Auric District", enclave = "Auric", role = "Aureus, Piety, Faith, knowledge, science", color = new Color(1f, 0.84f, 0.4f),
            minDevelopment = 15f, cost = Costs(("Food", 25f), ("Research", 30f)),
            description = "Observatories, archives and shrines, where science explains and faith interprets. Produces Research and Faith and raises Aureus; thrives on leylines and their junctions, magical fertility, Coherence, Sacred ground and heights, and beside the Domestication keepers it studies. Keep the Militant drill yards away.",
            yields = Costs(("Research", 0.03f), ("Faith", 0.02f)), stats = { new DistrictStat("Aureus") }, housing = 0.5f, hubDevelopment = 1f,
            adjacency = { new AdjacencyRule(AdjacencySource.Leyline, 0.3f), new AdjacencyRule(AdjacencySource.Junction, 0.3f), new AdjacencyRule(AdjacencySource.MagicalFertility, 0.3f),
                new AdjacencyRule(AdjacencySource.Coherence, 0.3f), new AdjacencyRule(AdjacencySource.Sacred, 0.4f), new AdjacencyRule(AdjacencySource.HighGround, 0.05f),
                new AdjacencyRule(AdjacencySource.District, 0.15f, "auric"), new AdjacencyRule(AdjacencySource.District, 0.1f, "domestication"), new AdjacencyRule(AdjacencySource.District, -0.25f, "militant"),
                new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "weaver", name = "Weaver District", enclave = "Weaver", role = "Waltz, culture, poetry, songs, communal arts", color = new Color(0.86f, 0.5f, 0.78f),
            minDevelopment = 10f, cost = Costs(("Food", 20f), ("Elderwood", 15f)),
            description = "Singing circles, bard halls and the bakeries that keep lost recipes alive. Raises Waltz, lends its hub City Development and spins a little Silverreed; fair, coherent ground, a hub nearby and Indulgent and Agromagical neighbours deepen it, dissonance sours it.",
            yields = Costs(("Silverreed", 0.004f)), stats = { new DistrictStat("Waltz") }, housing = 0.5f, hubDevelopment = 3f,
            adjacency = { new AdjacencyRule(AdjacencySource.Beauty, 0.4f), new AdjacencyRule(AdjacencySource.Coherence, 0.2f), new AdjacencyRule(AdjacencySource.Hub, 0.2f),
                new AdjacencyRule(AdjacencySource.Sacred, 0.1f), new AdjacencyRule(AdjacencySource.District, 0.2f, "indulgent"), new AdjacencyRule(AdjacencySource.District, 0.1f, "agromagical"),
                new AdjacencyRule(AdjacencySource.District, 0.1f, "weaver"), new AdjacencyRule(AdjacencySource.Dissonance, -0.3f), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "domestication", name = "Domestication District", enclave = "Domestication", role = "creatures, Pure Light, taming, druids", color = new Color(0.55f, 0.9f, 0.85f),
            minDevelopment = 10f, cost = Costs(("Food", 30f)),
            description = "Pens, sanctuaries and druid groves where creatures and Pure Light herds are tamed. Produces Game Meat, Hides and a little Lumenwool; animal sites, cover, leylines and fertile land feed it, Agromagical fields beside it help, the forges' din and danger scatter the herds.",
            yields = Costs(("Game Meat", 0.03f), ("Hides", 0.01f), ("Lumenwool", 0.005f)), housing = 0.5f,
            adjacency = { new AdjacencyRule(AdjacencySource.ResourceSite, 0.25f), new AdjacencyRule(AdjacencySource.Cover, 0.1f), new AdjacencyRule(AdjacencySource.Leyline, 0.2f),
                new AdjacencyRule(AdjacencySource.LandFertility, 0.2f), new AdjacencyRule(AdjacencySource.District, 0.2f, "agromagical"), new AdjacencyRule(AdjacencySource.District, 0.1f, "domestication"),
                new AdjacencyRule(AdjacencySource.District, -0.15f, "industrious"), new AdjacencyRule(AdjacencySource.Danger, -0.2f), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "trading", name = "Trading District", enclave = "Trading", role = "economics, wealth, resources, efficiency", color = new Color(1f, 0.72f, 0.2f),
            minDevelopment = 10f, cost = Costs(("Food", 30f), ("Elderwood", 20f)),
            description = "Stalls, warehouses and the toll-house. Lends its hub much City Development, raises Administrative Capacity and pulls land; roads, a Trade Nexus, rivers, the coast and a hub feed it, and Industrious and Indulgent neighbours give it goods to sell.",
            yields = Costs(("Food", 0.02f)), housing = 1f, hubDevelopment = 6f, capacity = 1.5f, pull = 0.15f,
            adjacency = { new AdjacencyRule(AdjacencySource.Road, 0.3f), new AdjacencyRule(AdjacencySource.TradeNexus, 0.5f), new AdjacencyRule(AdjacencySource.Freshwater, 0.2f),
                new AdjacencyRule(AdjacencySource.Coast, 0.2f), new AdjacencyRule(AdjacencySource.Hub, 0.3f), new AdjacencyRule(AdjacencySource.District, 0.15f, "trading"),
                new AdjacencyRule(AdjacencySource.District, 0.15f, "industrious"), new AdjacencyRule(AdjacencySource.District, 0.1f, "indulgent"), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "industrious", name = "Industrious District", enclave = "Industrious", role = "equipment, forges, artisans, metalworks", color = new Color(0.8f, 0.5f, 0.28f),
            minDevelopment = 10f, cost = Costs(("Food", 30f)),
            description = "Sawpits, kilns, forges and workshops. Produces Elderwood and Duskstone and adds Administrative Capacity; forests, resource sites, grandfields and roads feed it, and a Militant District beside it buys its equipment. Its soot troubles the hamlets beside it.",
            yields = Costs(("Elderwood", 0.04f), ("Duskstone", 0.02f)), housing = 0.5f, capacity = 0.5f,
            adjacency = { new AdjacencyRule(AdjacencySource.Cover, 0.1f), new AdjacencyRule(AdjacencySource.ResourceSite, 0.2f), new AdjacencyRule(AdjacencySource.Grandfield, 0.3f),
                new AdjacencyRule(AdjacencySource.HighGround, 0.05f), new AdjacencyRule(AdjacencySource.Road, 0.15f), new AdjacencyRule(AdjacencySource.District, 0.15f, "industrious"),
                new AdjacencyRule(AdjacencySource.District, 0.1f, "militant"), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "regal", name = "Regal District", enclave = "Regal", role = "Regalia, politics, diplomacy, prestige, espionage", color = new Color(0.62f, 0.45f, 0.9f),
            minDevelopment = 20f, cost = Costs(("Food", 30f), ("Elderwood", 30f)),
            description = "Courts, embassies and quiet listening-houses. Raises Regalia, adds much Administrative Capacity, pulls land and lends its hub City Development; a hub and roads nearby, fair ground and every enclave within its envoys' reach feed it. Two courts side by side scheme against each other.",
            stats = { new DistrictStat("Regalia") }, housing = 0.5f, hubDevelopment = 2f, capacity = 2f, pull = 0.25f,
            adjacency = { new AdjacencyRule(AdjacencySource.Hub, 0.4f), new AdjacencyRule(AdjacencySource.Road, 0.2f), new AdjacencyRule(AdjacencySource.Beauty, 0.2f),
                new AdjacencyRule(AdjacencySource.Coherence, 0.1f), new AdjacencyRule(AdjacencySource.District, 0.15f, "trading"), new AdjacencyRule(AdjacencySource.District, 0.1f, "weaver"),
                new AdjacencyRule(AdjacencySource.District, -0.1f, "regal"), new AdjacencyRule(AdjacencySource.Enclave, 0.15f, "any") },
        },
        new DistrictSpec
        {
            id = "indulgent", name = "Indulgent District", enclave = "Indulgent", role = "entertainment, pleasure, joy, luxuries", color = new Color(1f, 0.55f, 0.6f),
            minDevelopment = 10f, cost = Costs(("Food", 30f)),
            description = "Inns, bathhouses, festival grounds and sweet-shops, a refuge for travellers. Raises morale, houses people, lends its hub City Development and gathers Wild Honey; fair ground, roads, water and Weaver, Agromagical and Trading neighbours feed it, a Militant District and danger spoil the mood.",
            yields = Costs(("Wild Honey", 0.01f)), stats = { new DistrictStat("morale") }, housing = 1f, hubDevelopment = 2f,
            adjacency = { new AdjacencyRule(AdjacencySource.Beauty, 0.4f), new AdjacencyRule(AdjacencySource.Road, 0.2f), new AdjacencyRule(AdjacencySource.Freshwater, 0.1f),
                new AdjacencyRule(AdjacencySource.District, 0.2f, "weaver"), new AdjacencyRule(AdjacencySource.District, 0.15f, "agromagical"), new AdjacencyRule(AdjacencySource.District, 0.1f, "trading"),
                new AdjacencyRule(AdjacencySource.District, -0.15f, "militant"), new AdjacencyRule(AdjacencySource.Danger, -0.3f), new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
        new DistrictSpec
        {
            id = "esoteric", name = "Esoteric District", enclave = "Esoteric", role = "Chorus, Outer Gods, dark Magic Arts, witchcraft, spirits", color = new Color(0.45f, 0.3f, 0.6f),
            minDevelopment = 15f, cost = Costs(("Food", 20f), ("Faith", 20f)),
            description = "Covens, spirit-lanterns and shuttered libraries of what should not be read. Raises Chorus and turns up a little Research and Sky Glass; thrives where dissonance runs, on leyline junctions and magic-rich, hidden ground, and away from the crowds of a hub.",
            yields = Costs(("Research", 0.01f), ("Sky Glass", 0.004f)), stats = { new DistrictStat("Chorus") }, housing = 0.5f,
            adjacency = { new AdjacencyRule(AdjacencySource.Dissonance, 0.4f), new AdjacencyRule(AdjacencySource.Junction, 0.3f), new AdjacencyRule(AdjacencySource.MagicalFertility, 0.2f),
                new AdjacencyRule(AdjacencySource.Cover, 0.1f), new AdjacencyRule(AdjacencySource.District, 0.1f, "esoteric"), new AdjacencyRule(AdjacencySource.Hub, -0.1f),
                new AdjacencyRule(AdjacencySource.Enclave, 0.3f) },
        },
    };
}

/// <summary>What a unit on the map is for. Scout and Settler are retired (expeditions of legends do both); kept so the values keep their numbers.</summary>
public enum UnitRole { Scout, Settler, Builder, Expedition, Army, Band }

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
    /// <summary>Gather an identified resource site's harvest as cargo (and its seeds), once per Age per site, without claiming the ground.</summary>
    Harvest = 64,
    /// <summary>Plant carried seeds on fertile ground you hold (a new, smaller patch of the resource).</summary>
    Plant = 128,
    /// <summary>Hold a festival in one of your settlements (a cultural party; <see cref="CultureSystem"/>).</summary>
    Celebrate = 256,
}

/// <summary>
/// What an expedition sets out to do, chosen when it forms: explore (the default: survey, forage, harvest, settle,
/// improve once known), build (Builders: improve, plant, forage; faster at it, no surveys) or celebrate (a Cultural
/// Party: festivals in your settlements). Appended values only: saved by name.
/// </summary>
public enum ExpeditionCharter
{
    Expedition,
    Builders,
    CulturalParty,
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
    [Tooltip("Sevenths a survey of a whole meso hex spends at work, shared between its seven micro hexes (the walk from hex to hex comes on top).")]
    public float mesoSurveySevenths = 1.25f;
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
    [Tooltip("Sevenths one harvest of a resource site takes, and one planting of seeds.")]
    public float harvestSevenths = 1f, plantSevenths = 1.5f;
    [Tooltip("Sevenths an expedition takes to investigate the ruins of a fallen settlement.")]
    public float investigateSevenths = 3f;
    [Tooltip("Sevenths a cultural party's festival lasts.")]
    public float festivalSevenths = 2f;
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

    [Header("In battle (WorldBattles.PartySide; 0: its role's default)")]
    [Tooltip("Integrity and Composure of the party (each legend in it adds its own share), and its attack and defense. Military units (armies) fight with their companies instead and carry far more.")]
    public float battleIntegrity, battleComposure, battleAttack, battleDefense;

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
    /// <summary>A predatory Eleos Bloom lures a member in (within its reach: a mourner's silhouette, a cry for help).</summary>
    Lure,
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

/// <summary>What a survey event does (<see cref="SurveyFindSpec"/>).</summary>
public enum SurveyFindKind
{
    /// <summary>Old waymarks or a high vantage: the land around comes out of the fog.</summary>
    Vantage,
    /// <summary>A hidden spring or a sheltered hollow: the party refills its rations and rests.</summary>
    Spring,
    /// <summary>Something left by those before: every legend of the party is remembered for it (Lyrical Fragments).</summary>
    Relic,
    /// <summary>An ink story (its <see cref="SurveyFindSpec.story"/>), played by the party.</summary>
    Story,
}

/// <summary>One event a survey can turn up: its kind, name, how likely it is among the others and its numbers.</summary>
[Serializable]
public class SurveyFindSpec
{
    public SurveyFindKind kind;
    [Tooltip("Its name in the notices.")]
    public string name;
    [Tooltip("The notice, after the party's name ('{0}' is the place).")]
    public string text;
    [Tooltip("Relative chance among the finds.")]
    public float weight = 1f;
    [Tooltip("Vantage: cells around brought out of the fog.")]
    public int reveal = 3;
    [Tooltip("Spring: fatigue taken away (rations are refilled).")]
    public float rest = 25f;
    [Tooltip("Relic: Lyrical Fragments of discovery for every member.")]
    public int fragments = 2;
    [Tooltip("Story: the ink knot the party plays.")]
    public string story;
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

    [Header("Charters: what an expedition sets out to do (ExpeditionCharter)")]
    [Tooltip("Builders (the improveTechnology unlocks them) work the land only: they improve, plant and forage, work faster by this and wear by builderWear, but survey nothing and escort no settlers.")]
    public float builderWork = 0.6f;
    public float builderWear = 0.8f;
    [Tooltip("Cultural parties hold festivals in your settlements (CultureLifeTuning.festivalTechnology unlocks them, once the culture is founded); they wear by partyWear and do nothing else on the land.")]
    public float partyWear = 0.75f;

    [Header("Cargo and valuables (harvests carried home)")]
    [Tooltip("Weight of cargo each legend can carry (settlers carry none); seeds weigh nothing.")]
    public float cargoPerLegend = 40f;

    [Header("Weight: what the party carries sets its pace (Expeditions.Burden)")]
    [Tooltip("Weight each legend carries at ease (settlers carry their own and nothing more). Rations and cargo count.")]
    public float loadPerLegend = 30f;
    [Tooltip("Weight of one ration.")]
    public float rationWeight = 1f;
    [Tooltip("Weight of one unit of cargo, by resource (1 when not listed): Sky Glass comes in sheets, Lumenwool is light.")]
    public List<ResourceAmount> cargoWeights = new List<ResourceAmount>();
    [Tooltip("Travelling light: below this share of the load at ease the party walks faster...")]
    [Range(0f, 1f)] public float lightBelow = 0.15f;
    [Tooltip("...up to this pace with nothing at all.")]
    public float lightPace = 1.1f;
    [Tooltip("Burdened: pace lost per load at ease carried beyond it (1.5x the load at ease walks 0.5 x this slower)...")]
    public float overloadSlowdown = 0.6f;
    [Tooltip("...never slower than this pace.")]
    [Range(0.05f, 1f)] public float minLoadPace = 0.35f;
    [Tooltip("Travel fatigue added per load at ease carried beyond it.")]
    public float overloadFatigue = 0.5f;

    [Header("Solace: places that ease Composure on the road (Expeditions.Solace), on top of expeditionRecovery")]
    [Tooltip("Strain eased per Seventh on a silver river (a leyline running down real water), fading over silverRiverReach cells.")]
    public float silverRiverSolace = 0.6f;
    public int silverRiverReach = 2;
    [Tooltip("Strain eased per Seventh on the shore of a lake a silver river runs into (fading over silverLakeReach cells).")]
    public float silverLakeSolace = 0.8f;
    public int silverLakeReach = 1;
    [Tooltip("Strain eased per Seventh per point of beauty (WorldBeauty; fair land only).")]
    public float beautySolace = 1f;
    [Tooltip("The most solace one place gives.")]
    public float maxSolace = 4f;
    [Tooltip("Grievances fade by this much per Seventh with every authority.")]
    public float grievanceDecayPerSeventh = 0.5f;
    [Tooltip("An enclave's standing with you drops by this share of each grievance you raise with it.")]
    public float grievanceStanding = 1f;

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
    [Tooltip("At full Vibrational Fallout, on top of its Dissonance: the corrupted field broadcasts back into a Spellweaver's Soul Leitmotif (Soliton.md).")]
    public float falloutStrain = 8f;
    [Tooltip("A dead zone: Vibrational Density below this weighs on the attuned, who feel being away from density (Pure Light.md)...")]
    [Range(0f, 1f)] public float thinDensityBelow = 0.15f;
    [Tooltip("...up to this much at none.")]
    public float thinStrain = 1f;
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
    [Tooltip("At full Vibrational Fallout, and at the height of a Chaotic Resonant Cascade along its edge.")]
    public float falloutRisk = 0.25f, cascadeRisk = 0.15f;
    [Tooltip("At a predatory Eleos Bloom's full lure.")]
    public float lureRisk = 0.25f;
    [Tooltip("Per point of harsh weather.")]
    public float exposureRisk = 0.1f;
    [Tooltip("Per member Fractured, and per member Spiraling or deeper.")]
    public float fracturedRisk = 0.05f, spiralingRisk = 0.1f;
    public float maxRisk = 0.6f;
    [Tooltip("Empty: the defaults (Expeditions.DefaultMishaps).")]
    public List<MishapSpec> mishaps = new List<MishapSpec>();

    [Header("Surveys: what exploring a cell turns up")]
    [Tooltip("A cell whose seven hexes an expedition has seen in passing is explored; the chance it also turns up an event, and spare resources.")]
    [Range(0f, 1f)] public float passingEventChance = 0.06f, passingCacheChance = 0.08f;
    [Tooltip("A cell an expedition was sent to survey, walking all seven of its hexes: the chance of an event, and of spare resources.")]
    [Range(0f, 1f)] public float surveyEventChance = 0.3f, surveyCacheChance = 0.4f;
    [Tooltip("Spare resources: the cell's forage times this (its ground's yields for this many seconds where nothing can be foraged), times the party's reward multiplier.")]
    public float cacheForage = 2.5f, cacheYieldSeconds = 90f;
    [Tooltip("Empty: the defaults (Expeditions.DefaultSurveyFinds).")]
    public List<SurveyFindSpec> surveyFinds = new List<SurveyFindSpec>();

    [Header("Survivors: bands of people found by exploring a cell (proposals)")]
    [Tooltip("The chance a cell explored in passing, and one surveyed, turns up a band of survivors. One draw per cell, so a band is never found twice.")]
    [Range(0f, 1f)] public float passingSurvivorChance = 0.1f, surveySurvivorChance = 0.35f;
    [Tooltip("People in a band.")]
    public int survivorBandMin = 2, survivorBandMax = 7;
    [Tooltip("Sevenths a band takes to walk one cell toward the Capital.")]
    public float survivorSeventhsPerCell = 1f;
    [Tooltip("Macro biomes where survivors shelter (ruined towns, old infrastructure): their chance times havenOdds.")]
    public List<string> survivorHavens = new List<string> { "survivor-architecture", "crumbling-infrastructure" };
    public float havenOdds = 2f;
    [Tooltip("Macro biomes few survive in: their chance times barrenOdds.")]
    public List<string> survivorBarrens = new List<string> { "the-golden-ash", "magical-rift" };
    public float barrenOdds = 0.35f;

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
    [Tooltip("At full Vibrational Fallout (Primal White Noise in every thread: bodies fray, Soliton.md), on top of its Dissonance; none in a settlement.")]
    public float falloutAttrition = 12f;
    [Tooltip("A unit whose attrition reaches 100 is lost (its people scatter or perish).")]
    public bool lostAtFullAttrition = true;

    [Header("Rich ground (WorldUnits.ForageRichness): Forage, and what a camp gathers")]
    [Tooltip("Land fertility from which foraging yields more...")]
    [Range(0f, 1f)] public float fertileFrom = 0.4f;
    [Tooltip("...up to this share more at full fertility.")]
    public float fertilityForage = 0.8f;
    [Tooltip("Share more at the heart of living resource sites (crops, flora, fauna, water, blooms; fading over two cells around them; WorldTile.siteBounty). Also raises what a camp gathers.")]
    public float siteForage = 0.6f;
    [Tooltip("Share more at the heart of a Resource Grandfield (by its density). Also raises what a camp gathers.")]
    public float grandfieldForage = 0.5f;
    [Tooltip("The most all of it adds together.")]
    public float maxForageBonus = 1.5f;
}

/// <summary>
/// Vibrational Density and Vibrational Fallout (<see cref="WorldVibration"/>), read by <see cref="WorldMagic"/> each
/// Age. Vault: the atmosphere "stratifies by Vibrational Density", a measure of "how tightly reality's threads maintain
/// harmonic integrity"; it peaks along the Leylines and at Sacred Sites; Coherence increases through it. Fallout is
/// where the Dissonance broke the Loom: permanent, unhealable, harsh on the attuned. Every number is a proposal.
/// </summary>
[Serializable]
public class VibrationSettings
{
    [Header("Density (0-1)")]
    [Tooltip("What every cell holds.")]
    [Range(0f, 1f)] public float baseDensity = 0.15f;
    [Tooltip("The strata: added at the highest ground, scaled by height above the sea (Sky Glass sheets on the peaks).")]
    [Range(0f, 1f)] public float strata = 0.35f;
    [Tooltip("Added at a leyline's heart (its influence, fading over the drift radius): the Symphonic Veins are where density peaks.")]
    [Range(0f, 1f)] public float leylines = 0.35f;
    [Tooltip("Added where two families meet, and where three or more do.")]
    [Range(0f, 1f)] public float convergence = 0.1f, basin = 0.2f;
    [Tooltip("Added on silver water (Lunehymn).")]
    [Range(0f, 1f)] public float silver = 0.05f;
    [Tooltip("What water holds (the sea carries no strata).")]
    [Range(0f, 1f)] public float waterDensity = 0.2f;
    [Tooltip("Coherence gained per point of density above neutralDensity (lost below it): Arcanoria.md, Coherence increases through the Leylines and Vibrational Density.")]
    public float coherence = 0.1f;
    [Range(0f, 1f)] public float neutralDensity = 0.35f;

    [Header("Vibrational Fallout (0-1): where the Dissonance broke the Loom")]
    [Tooltip("The Age-free Dissonance (its Seeds) at which fallout begins...")]
    [Range(0f, 1f)] public float falloutFrom = 0.2f;
    [Tooltip("...and where it is total.")]
    [Range(0f, 1f)] public float falloutFull = 0.38f;
    [Tooltip("Travel through it: each step costs 1 + this x fallout times as much (Cushion Arts' ferry planks are the canon way across).")]
    public float travel = 2f;
    [Tooltip("Fallout cannot be healed: a Resonance Anchor's Coherence and the Dissonance a listening bloom drinks count for (1 - fallout) of themselves in it.")]
    public bool unhealable = true;
    [Tooltip("Chaotic Resonant Cascades along its boundary: cells within this many steps of fallout, where their own is lower.")]
    public int cascadeReach = 2;
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
    [Tooltip("The Symphony of War's macro layer: auto-resolved battles, section kinds, Orchestral Formation templates, ground (BattleResolver). Empty lists use CombatDefaults.")]
    public CombatSettings combat = new CombatSettings();

    public UnitSpec Unit(string id) => units.Find(u => u != null && string.Equals(u.id, id, StringComparison.OrdinalIgnoreCase)) ?? WorldBattles.DefaultUnit(id);

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
    [Tooltip("The composition stencil, a TextAsset under Resources (Q1-Q7 quadrants, I intersections, W ocean).")]
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
    [Tooltip("Width of the band, in stencil cells, in which the coastline wanders between the intersections and the ocean.")]
    public float coastBand = 0.7f;
    [Tooltip("Narrowest the ocean ring around the mainland may be, in cells.")]
    public int oceanRingWidth = 3;
    [Tooltip("Ground of the open ocean, the shallows by the coast and lakes.")]
    public string oceanTerrain = "deep-ocean", shallowsTerrain = "shallows", lakeTerrain = "lake";
    [Tooltip("The intersections' own ground by climate, blended with the neighbouring Macro Biomes near them.")]
    public List<TerrainRule> intersectionTerrains = new List<TerrainRule>();
    [Tooltip("How far into the intersections, in cells, the neighbouring biomes' ground reaches.")]
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
    public List<MacroBiomeSpec> macroBiomes = new List<MacroBiomeSpec>();
    public List<QuadrantSpec> quadrants = new List<QuadrantSpec>();
    public List<FeatureSpec> features = new List<FeatureSpec>();
    public List<GrandfieldSpec> grandfields = new List<GrandfieldSpec>();
    [Tooltip("Lesser resource sites that make tiles worth having (WorldResources).")]
    public List<ResourceSiteSpec> resourceSites = new List<ResourceSiteSpec>();
    [Tooltip("The bestiary: every creature species (CreatureTaxonomy). A Fauna resource site is one species' den or herd; not the Atonalis, which are threats.")]
    public List<SpeciesSpec> species = new List<SpeciesSpec>();
    [Tooltip("How the species live in each Macro Biome: capacity, growth, pressure, hunting, migration (WorldEcology).")]
    public EcologySettings ecology = new EcologySettings();
    [Tooltip("How the species learn from the way each authority treats them (WorldBehavior): hunting, habitat taken, living beside them.")]
    public BehaviorSettings behavior = new BehaviorSettings();
    [Tooltip("The creatures' calendar (WorldRhythm): what each Echo does to them, the shape of its Phases, and the Ritual Seventh's attunement.")]
    public RhythmSettings rhythm = new RhythmSettings();
    [Tooltip("Resonance plagues, each tuned to a few Pure Light species (WorldPlagues; vault: Pure Light.md, Pathogenic Weaknesses).")]
    public List<ResonancePlagueSpec> plagues = new List<ResonancePlagueSpec>();
    [Tooltip("Emotional Residue and how Eleos Blooms (resource sites of kind Bloom) live off it.")]
    public EleosSettings eleos = new EleosSettings();
    [Tooltip("Vibrational Density and Vibrational Fallout (WorldVibration), read by the magic each Age.")]
    public VibrationSettings vibration = new VibrationSettings();
    [Tooltip("Cover laid over the ground: deep forests, fens, canyons (WorldCover).")]
    public List<CoverSpec> covers = new List<CoverSpec>();
    public List<EnclaveSpec> enclaves = new List<EnclaveSpec>();
    public List<ThreatSpec> threats = new List<ThreatSpec>();
    [Tooltip("How the Enclaves act on the creatures: keeping, tending, herds and commissions (WorldEnclaveEcology, AECOR E7).")]
    public EnclaveEcologySettings enclaveEcology = new EnclaveEcologySettings();
    [Tooltip("How the creatures come through an Age's end: the crash by Pure Light, shifting ranges, new lineages, scar spectra (WorldAftermath, AECOR E8).")]
    public AftermathSettings aftermath = new AftermathSettings();
    [Tooltip("The Great Plague's vector in the living world: the moths' trade, their plague-era breeding, when their part is known (WorldGreatPlague, AECOR E9).")]
    public GreatPlagueSettings greatPlague = new GreatPlagueSettings();
    public NexusRules nexus = new NexusRules();

    public GrandfieldSpec Grandfield(string id) => grandfields.Find(g => g != null && string.Equals(g.id, id, StringComparison.OrdinalIgnoreCase));

    public ResourceSiteSpec ResourceSite(string id) => resourceSites.Find(r => r != null && string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase));

    public SpeciesSpec Species(string id) => string.IsNullOrEmpty(id) || species == null ? null : species.Find(s => s != null && string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase));

    public ResonancePlagueSpec Plague(string id) => string.IsNullOrEmpty(id) || plagues == null ? null : plagues.Find(p => p != null && string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));

    public CoverSpec Cover(string id) => string.IsNullOrEmpty(id) || covers == null ? null : covers.Find(c => c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));

    public EnclaveSpec Enclave(string id) => enclaves.Find(e => e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));

    public ThreatSpec Threat(string id) => threats.Find(t => t != null && string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase));

    public TerrainSpec Terrain(string id) => terrains.Find(t => t != null && string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase));

    public MacroBiomeSpec MacroBiome(string id) => macroBiomes.Find(b => b != null && string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase));

    public QuadrantSpec Quadrant(string id) => quadrants.Find(s => s != null && string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase));

    public FeatureSpec Feature(string id) => features.Find(f => f != null && string.Equals(f.id, id, StringComparison.OrdinalIgnoreCase));

    public float MagicAccess(int age) => magicAccess.Count == 0 ? 1f : magicAccess[Math.Max(0, Math.Min(age, magicAccess.Count - 1))];
}
