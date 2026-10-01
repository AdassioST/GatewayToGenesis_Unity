using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What part of the composition a cell grew from. The world's scales, largest first (AECOR's names; ARCHITECTURE.md,
/// "World scales"): the Overall Map, its Quadrants (Q1-Q7), the Macro Biomes drawn into each Quadrant's slots, the
/// Inter-Biome Definitions inside them (covers, landmarks, grandfields, resource sites), and each Macro Biome's nine
/// compass Sectors (<see cref="CompassSector"/>). Intersections are the procedural ground between Macro Biomes.
/// </summary>
public enum WorldComposition
{
    /// <summary>The ocean (stencil W and the apron around the stencil).</summary>
    Ocean,
    /// <summary>An intersection (stencil I): procedural ground joining Macro Biomes (seams, passes, coasts).</summary>
    Intersection,
    /// <summary>A Macro Biome in one of its Quadrant's slots.</summary>
    MacroBiome,
}

/// <summary>
/// One of the nine Sectors of a Macro Biome: its centre and the eight compass directions around it, measured from
/// the Macro Biome's own centre (north is up). <see cref="None"/> in the intersections and the ocean.
/// </summary>
public enum CompassSector { None, Central, North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

/// <summary>
/// One meso cell of the world, the playable strategy hex (it owns seven micro hexes; 49 of it make a macro
/// aggregate, <see cref="HexHierarchy"/>). Terrain, fields, water, magic and knowledge are separate layers: a cell can
/// be a forested floodplain on a silver reach inside a slot's seam at once (WORLD_GENERATION.md §6).
/// </summary>
[Serializable]
public class WorldTile
{
    public HexCoord coord;
    /// <summary>Position in the map's cell list (stable for a seed and generator version).</summary>
    public int index;
    /// <summary>World position of its centre (micro units).</summary>
    public float x, y;

    // Composition
    public WorldComposition composition;
    /// <summary>Its Quadrant (Q1-Q7; the nearest slot's in an intersection), or null at sea.</summary>
    public string quadrant;
    /// <summary>Stencil slot index, or -1 in the intersections and the ocean.</summary>
    public int slot = -1;
    /// <summary>The slot whose Macro Biome's living range it belongs to (<see cref="WorldEcology"/>): its own slot, or in an
    /// intersection the nearest one's; -1 off the stencil.</summary>
    public int habitatSlot = -1;
    /// <summary>The Macro Biome whose ground it carries (the nearest slot's in an intersection), or null at sea.</summary>
    public string macroBiome;
    /// <summary>Which of its Macro Biome's nine Sectors it lies in (None in the intersections and the ocean).</summary>
    public CompassSector sector;
    /// <summary>In an intersection or a slot's blended buffer band (not a protected interior).</summary>
    public bool seam;
    /// <summary>Stamped from a handmade tile (its id), or null.</summary>
    public string handmadeTile;
    public string terrain;
    /// <summary>Quarter of the world around the capital (0 north-east, 1 north-west, 2 south-west, 3 south-east).</summary>
    public int quarter;

    // Physical fields (0-1)
    public float elevation, moisture, temperature, landFertility;
    public bool water, lake;
    /// <summary>Rain gathered from upstream (cells of full moisture).</summary>
    public float flow;
    /// <summary>Index of the cell its water runs to, or -1 at an outlet.</summary>
    public int downstream = -1;
    public bool river;
    public float waterDepth;
    public float escarpment;
    public string landform;
    /// <summary>Derived from the local weather front, rebuilt on load and whenever fronts change.</summary>
    public float weatherTravelMultiplier = 1f;
    // Its seven micro hexes, one bit each (bit k: HexHierarchy.ChildOffsets[k]; MicroNavigation).
    /// <summary>Crags: micro hexes of an escarpment no one climbs (generated; never the centre).</summary>
    public int microBlockedMask;
    /// <summary>Micro hexes a unit surveyed (saved). All of them surveyed (crags aside) explores the cell.</summary>
    public int microSurveyMask;
    /// <summary>Micro hexes a unit walked on or beside (saved). Any of them known makes the cell known.</summary>
    public int microKnownMask;
    /// <summary>Micro hexes of a wilderness cell your people hold (saved; <see cref="WorldTerritory"/>), adopted or claimed
    /// hex by hex: each is yours at once (your border, its share of the cell's yields and load). All of them held (crags
    /// aside), the whole cell joins your authority.</summary>
    [SaveOptionalField] public int microHeldMask;
    /// <summary>Of <see cref="microHeldMask"/>, the hexes you paid a claim for (saved): they never slip away, and they set
    /// the next claim's cost.</summary>
    [SaveOptionalField] public int microClaimMask;

    // Magic (recomputed each Age by WorldMagic)
    public float coherence, dissonance, magicFertility, lunehymn;
    public bool silver, sacred;
    /// <summary>Leyline families through this cell (one bit per family, <see cref="WorldMagic.MaxFamilies"/> at most).</summary>
    public long leylines;
    /// <summary>Families meeting at a junction here: 2 a Convergence, 3 or more a Basin, 0 none.</summary>
    public int junction;

    // Ownership is independent of discovery and terrain accessibility.
    public string authorityId = "wilderness";
    public float administrativeAuthority;
    /// <summary>The authority was given the whole cell (a settlement's or the Capital's reach, an enclave, an independent
    /// site): its hexes no one else holds are the authority's (derived by <see cref="WorldAuthority.Establish"/>).</summary>
    public bool whole;
    /// <summary>How firmly <see cref="authorityId"/> holds the cell: de facto or core (derived; <see cref="WorldHoldings"/>).</summary>
    public HoldStatus hold;
    public bool impassable;
    public float leylineInfluence;
    // Territory (WorldTerritory, WorldBeauty; derived, rebuilt with the civilization)
    /// <summary>How beautiful (+1) or hideous (-1) the place is to live on: society adopts and works fair land first.</summary>
    public float beauty;
    /// <summary>Your territorial pull here (0 none; the strongest seat plus a share of the others).</summary>
    public float pull;
    /// <summary>The strongest rival pull here (enclaves), which keeps your society from adopting it.</summary>
    public float rivalPull;
    /// <summary>Index into <see cref="TerritoryState.Seats"/> of your seat pulling strongest here, or -1.</summary>
    public int pullSeat = -1;
    /// <summary>A shallow copy that keeps this moment's values (a background save reads its fields while play goes on).</summary>
    public WorldTile CopyForSave() => (WorldTile)MemberwiseClone();

    public float Desirability => water || impassable ? 0f :
        0.45f * landFertility + 0.35f * coherence + 0.2f * magicFertility;

    // Sites and civilization (WorldSites, WorldCivilization; derived from their lists, rebuilt on change)
    /// <summary>Standing hazards here, 0-1 (predatory blooms; none on Sacred ground). Threats cast no aura: what hunts is
    /// known only by its <see cref="signs"/>.</summary>
    public float danger;
    /// <summary>The land's lasting emotional imprint here (battles, hunts, loss, a people's suffering: <see cref="WorldSuffering"/>;
    /// derived from the saved imprints, never saved), and its total.</summary>
    public EmotionalRegister imprint = new EmotionalRegister();
    public float suffering;
    /// <summary>The feelings living here now (settlements' people, ruins: <see cref="WorldResources.Residue"/>; derived, never saved).</summary>
    public EmotionalRegister living = new EmotionalRegister();
    /// <summary>Fresh signs of something hunting here (tracks, kills, static), 0-1, fading (derived, never saved): the only warning an explorer gets.</summary>
    public float signs;
    /// <summary>Index into <see cref="WorldMap.Grandfields"/>, or -1.</summary>
    public int grandfield = -1;
    /// <summary>How rich the grandfield is here: 1 at its heart, less toward its edge.</summary>
    public float grandfieldDensity;
    /// <summary>The gifted geography that can hold a Trade Nexus (harbor, estuary, pass, confluence), or null.</summary>
    public string nexus;
    public bool road;
    /// <summary>An Old World road runs here, broken (placed with the world, <see cref="WorldRuins.PlaceOldWorld"/>; never saved).</summary>
    public bool oldRoad;
    /// <summary>A road here is only a restored Old World stretch, not rebuilt yet (derived at each rebuild).</summary>
    public bool restoredRoad;
    /// <summary>A Trade Node stands here (a checkpoint of a road).</summary>
    public bool tradeNode;
    /// <summary>Index into <see cref="WorldMap.Settlements"/> of the settlement on this cell, or -1.</summary>
    public int settlement = -1;
    /// <summary>Index into <see cref="WorldMap.Enclaves"/> of the enclave on this cell, or -1.</summary>
    public int enclave = -1;
    /// <summary>A builder's improvement level on this resource hotspot (0 none).</summary>
    public int improvement;
    /// <summary>Index into <see cref="WorldMap.ResourceSites"/> of the resource site on this cell, or -1.</summary>
    public int resourceSite = -1;
    /// <summary>The Age (number + 1) in which its resource site was last harvested; 0 never (saved).</summary>
    public int harvestedAge;
    /// <summary>What nearby resource sites do here (derived by <see cref="WorldResources.Refresh"/>, never saved): beauty
    /// lent or taken, Coherence lent or taken (handed to the magic), land fertility added to <see cref="landFertility"/>.</summary>
    public float siteBeauty, siteCoherence, siteFertility;
    /// <summary>Eleos Blooms here (derived by <see cref="WorldResources.Refresh"/>, never saved): the Emotional Residue in the
    /// cell (0-1, what blooms feed on), Dissonance drunk or lent (handed to the magic), a predator's danger (folded into
    /// <see cref="danger"/>) and lure, and the strain a healer eases per Seventh.</summary>
    public float residue, siteDissonance, siteDanger, lure, sanctuary;
    /// <summary>The hurtful part of the residue (ruins' grief, strain, Dissonance) and the history the land holds, 0-1
    /// (derived with the residue, never saved): where Fated and Forsaken Flowers and Glimmerfern grow by themselves.</summary>
    public float hurt, history;
    /// <summary>Cell steps to the nearest silver river (a leyline on a real river) and to the nearest lake a silver river
    /// runs into (derived with the residue, never saved; <see cref="WorldResources.FarFromSilver"/> when far).</summary>
    public int silverRiverSteps = WorldResources.FarFromSilver, silverLakeSteps = WorldResources.FarFromSilver;
    /// <summary>The cover over this cell (<see cref="CoverSpec"/> id, placed with the world, never saved), or null; its patch in <see cref="WorldMap.Covers"/>.</summary>
    public string cover;
    public int coverPatch = -1;
    /// <summary>What the cover does here (copied from its spec by <see cref="WorldCover"/>): it hides what stands inside and blocks
    /// the view beyond, caps sight from inside (-1 no cap), multiplies travel and survey time and survey finds, wears parties down.</summary>
    public bool concealed;
    public int coverSight = -1;
    public float coverTravel = 1f, coverSurvey = 1f, coverFinds = 1f, coverHardship;
    /// <summary>Vibrational Density, 0-1: how tightly reality's threads hold here (recomputed with the magic by
    /// <see cref="WorldVibration"/>, never saved). Vibrational Fallout, 0-1: where the Dissonance broke the Loom (permanent);
    /// a Chaotic Resonant Cascade, 0-1, along its edge; and what the fallout does to each step into the cell.</summary>
    public float vibrationalDensity, fallout, cascade, falloutTravel = 1f;
    /// <summary>Composure strain eased per Seventh by what grows and lies here (a moonlit grove's ground, Glimmerfern's light;
    /// derived by <see cref="WorldResources.Refresh"/>, never saved), and how richly living resource sites around feed a
    /// forager (0-1).</summary>
    public float solace, siteBounty;

    // Places and knowledge
    /// <summary>Feature id, or null.</summary>
    public string feature;
    /// <summary>The Age (number) the feature appeared in.</summary>
    public int featureAge;
    // Knowledge, from least to most (each implies the ones before it):
    /// <summary>Out of the fog: seen from afar, but still unknown wilderness.</summary>
    public bool revealed;
    /// <summary>Known: a scout has passed over it. Its ground and what stands there are known; it can be claimed.</summary>
    public bool known;
    /// <summary>Explored: a unit surveyed it. Its yields flow (if held) and its feature has been investigated.</summary>
    public bool explored;
    /// <summary>The Age (number + 1) in which it was last foraged; 0 never. A cell gives forage once per Age.</summary>
    public int foragedAge;

    public bool HasFeature => !string.IsNullOrEmpty(feature);

    public int LeylineCount
    {
        get
        {
            int n = 0;
            for (long v = leylines; v != 0; v &= v - 1) n++;
            return n;
        }
    }
}

/// <summary>A watercourse from its source to where it meets the sea, a lake or a larger river.</summary>
public class RiverPath
{
    /// <summary>Cell indices from the source downstream; the last may be the water it runs into.</summary>
    public readonly List<int> cells = new List<int>();
    /// <summary>Largest flow along it.</summary>
    public float flow;
    /// <summary>Drawn on the atlas (macro) view too.</summary>
    public bool major;
}

/// <summary>What generation decided, repaired and could not do (WORLD_GENERATION.md §4 step 8).</summary>
public class WorldGenReport
{
    public int seed;
    public int version;
    public string catalogHash;
    public readonly List<string> assignments = new List<string>();
    public readonly List<string> tiles = new List<string>();
    public readonly List<string> repairs = new List<string>();
    public readonly List<string> errors = new List<string>();
    public readonly List<string> notes = new List<string>();
    public double milliseconds;

    public override string ToString()
    {
        var lines = new List<string> { $"World seed {seed}, generator v{version}, catalog {catalogHash}, {milliseconds:0} ms" };
        lines.AddRange(assignments.Select(a => "  " + a));
        if (tiles.Count > 0) lines.Add($"  handmade tiles: {tiles.Count} stamped");
        lines.AddRange(notes.Select(n => "  note: " + n));
        lines.AddRange(repairs.Select(r => "  repair: " + r));
        lines.AddRange(errors.Select(e => "  ERROR: " + e));
        return string.Join("\n", lines);
    }
}

/// <summary>
/// The world's meso cells and what is known of them, with no scene state (tested in <c>WorldGenerationTests</c>).
/// Knowledge spreads from explored ground: exploring a cell reveals it and its neighbours, and an expedition can only
/// set out for revealed, passable ground next to ground already explored. The frontier is kept incrementally, so a
/// map of tens of thousands of cells answers it at once.
/// </summary>
[Serializable]
public class WorldMap
{
    public int seed;
    public HexCoord Capital { get; set; }
    /// <summary>World rectangle the cells fill (micro units).</summary>
    public float minX, minY, maxX, maxY;
    public WorldGenReport Report { get; } = new WorldGenReport();
    public List<RiverPath> Rivers { get; } = new List<RiverPath>();
    public List<SlotPlacement> Placements { get; } = new List<SlotPlacement>();
    public WorldStencil Stencil { get; set; }
    public WorldMagic Magic { get; set; }
    /// <summary>Generated each world (and each Age for threats): multi-cell grandfields, Trade Nexus sites, threats.</summary>
    public List<Grandfield> Grandfields { get; } = new List<Grandfield>();
    public List<int> NexusSites { get; } = new List<int>();
    /// <summary>Resource sites (saved): generated each Age (<see cref="WorldResources.PlaceAge"/>), then the planted ones (re-derived from <see cref="Plantings"/>).</summary>
    public List<ResourceSite> ResourceSites { get; set; } = new List<ResourceSite>();
    /// <summary>Cover patches (<see cref="WorldCover.Place"/>, once with the world; the same seed gives the same patches).</summary>
    public List<CoverPatch> Covers { get; } = new List<CoverPatch>();
    /// <summary>Seeds planted on your land (saved): each becomes a planted resource site.</summary>
    public List<Planting> Plantings { get; set; } = new List<Planting>();
    /// <summary>The creatures of each Macro Biome (saved; <see cref="WorldEcology"/>).</summary>
    public List<Population> Populations { get; set; } = new List<Population>();
    /// <summary>What each species has learned from the authorities (saved; <see cref="WorldBehavior"/>).</summary>
    public List<SpeciesBehavior> Behaviors { get; set; } = new List<SpeciesBehavior>();
    /// <summary>Resonance plagues running through (or lately past) the Macro Biomes' creatures (saved; <see cref="WorldPlagues"/>).</summary>
    public List<Outbreak> Outbreaks { get; set; } = new List<Outbreak>();
    /// <summary>Each species' habitat cells by slot for <see cref="habitatAge"/> (derived, never saved; <see cref="WorldEcology.Habitat"/>).</summary>
    public Dictionary<string, Dictionary<int, List<int>>> habitat;
    public int habitatAge = -1;
    /// <summary>
    /// The calendar as the world's rules read it (derived, never saved; <c>WorldSystem</c> sets it from the time system):
    /// the Echo (1-4, 0 with no calendar), the Phase within it (1-3), Phases since the game began, and whether this is
    /// a Ritual Seventh (<see cref="WorldRhythm"/>).
    /// </summary>
    public int echo, echoPhase, phaseCount;
    public bool ritualSeventh;
    public List<ThreatSite> Threats { get; } = new List<ThreatSite>();
    /// <summary>What play builds and changes (saved): settlements, roads and the enclaves the Ages bring.</summary>
    public List<Settlement> Settlements { get; set; } = new List<Settlement>();
    public List<TradeRoute> Routes { get; set; } = new List<TradeRoute>();
    public List<Enclave> Enclaves { get; set; } = new List<Enclave>();
    public List<WorldUnit> Units { get; set; } = new List<WorldUnit>();
    /// <summary>The ruins of fallen settlements (<see cref="WorldRuins"/>), saved by <see cref="WorldSystem"/>.</summary>
    public List<Ruin> Ruins { get; set; } = new List<Ruin>();
    /// <summary>The Old World's broken roads, cell by cell (placed with the world; never saved: the same seed lays them again).</summary>
    public List<List<int>> OldRoads { get; set; } = new List<List<int>>();
    /// <summary>Wilderness cells that joined your authority whole with a hex of them claimed (cell indices, in order;
    /// older saves: cells claimed whole): held like the Capital's own ground.</summary>
    public List<int> Claims { get; set; } = new List<int>();
    /// <summary>Hexes held by holders other than you (rivals, occupiers), cell by cell, and their core claims (saved by
    /// <see cref="WorldSystem"/>; <see cref="WorldHoldings"/>). Yours live in <see cref="WorldTile.microHeldMask"/>.</summary>
    public List<HexHolding> HexHoldings { get; set; } = new List<HexHolding>();
    /// <summary><see cref="HexHoldings"/> by cell (derived; <see cref="WorldHoldings.Invalidate"/> after changing the list).</summary>
    [NonSerialized] public Dictionary<int, List<HexHolding>> holdingIndex;
    /// <summary>Wilderness cells your society adopted by territorial pull, every hex of them (cell indices, in order; <see cref="WorldTerritory"/>).</summary>
    public List<int> Adopted { get; set; } = new List<int>();
    /// <summary>Claims an older save left half settled (cell indices, in order): a claim takes its land at once now (<see cref="WorldTerritory.ClaimNow"/>), and these are finished on the next Seventh.</summary>
    public List<int> Claiming { get; set; } = new List<int>();
    /// <summary>The territory rules the civilization layer reads when it rebuilds (set by <see cref="WorldSystem"/>; defaults otherwise).</summary>
    [NonSerialized] public TerritoryRules territoryRules;
    /// <summary>The settlement rules (desirability, tributaries and their districts) the civilization layer reads when it rebuilds (set by <see cref="WorldSystem"/>; defaults otherwise).</summary>
    [NonSerialized] public SettlementRules settlementRules;
    /// <summary>Tributaries that rejoined another hub at a rebuild, as (tributary id, hub id), until <see cref="WorldSystem"/> announces them; never saved.</summary>
    [NonSerialized] public List<(int tributary, int hub)> rehomed = new List<(int, int)>();
    /// <summary>The seats and their pull, derived at each rebuild (<see cref="WorldTerritory.Compute"/>); never saved.</summary>
    [NonSerialized] public TerritoryState territory;
    /// <summary>Bumped whenever settlements, roads, enclaves or authority change (views redraw).</summary>
    public int CivilizationVersion { get; set; }
    /// <summary>Bumped whenever knowledge changes (views refresh their fog).</summary>
    public int KnowledgeVersion { get; private set; }
    /// <summary>The micro travel grid, built on first use (<see cref="MicroNavigation.Grid"/>); never saved.</summary>
    [NonSerialized] public MicroGrid microGrid;

    private readonly Dictionary<HexCoord, WorldTile> _tiles = new Dictionary<HexCoord, WorldTile>();
    private readonly List<WorldTile> _list = new List<WorldTile>();
    private readonly HashSet<HexCoord> _frontier = new HashSet<HexCoord>();
    private int[] _neighbours;
    private int _explored, _known;

    public WorldMap(int seed)
    {
        this.seed = seed;
    }

    public IEnumerable<WorldTile> Tiles => _list;
    public IReadOnlyList<WorldTile> List => _list;
    public int Count => _list.Count;

    public bool InBounds(HexCoord coord) => _tiles.ContainsKey(coord);

    public WorldTile Get(HexCoord coord) => _tiles.TryGetValue(coord, out var tile) ? tile : null;

    public WorldTile this[int index] => _list[index];

    /// <summary>Add a cell (generation only); its index is its position in the list.</summary>
    public void Add(WorldTile tile)
    {
        if (tile == null || _tiles.ContainsKey(tile.coord)) return;
        tile.index = _list.Count;
        _list.Add(tile);
        _tiles[tile.coord] = tile;
        _neighbours = null;
    }

    /// <summary>Index of the neighbour of cell <paramref name="index"/> in <paramref name="direction"/>, or -1 off the map.</summary>
    public int Neighbour(int index, int direction)
    {
        if (_neighbours == null) BuildNeighbours();
        return _neighbours[index * 6 + direction];
    }

    public IEnumerable<WorldTile> NeighboursOf(WorldTile tile)
    {
        for (int d = 0; d < 6; d++)
        {
            int n = Neighbour(tile.index, d);
            if (n >= 0) yield return _list[n];
        }
    }

    private void BuildNeighbours()
    {
        _neighbours = new int[_list.Count * 6];
        for (int i = 0; i < _list.Count; i++)
        {
            for (int d = 0; d < 6; d++) _neighbours[i * 6 + d] = _tiles.TryGetValue(_list[i].coord.Neighbor(d), out var n) ? n.index : -1;
        }
    }

    /// <summary>The meso cell under a world point, or null off the map.</summary>
    public WorldTile At(float x, float y) => Get(HexHierarchy.CellAt(x, y, HexHierarchy.Meso));

    // ===== KNOWLEDGE =====

    /// <summary>Reveal every cell within <paramref name="reach"/> of <paramref name="center"/>.</summary>
    public void Reveal(HexCoord center, int reach)
    {
        foreach (var coord in HexCoord.Spiral(center, Math.Max(0, reach)))
        {
            var tile = Get(coord);
            if (tile != null && !tile.revealed)
            {
                tile.revealed = true;
                KnowledgeVersion++;
            }
        }
    }

    /// <summary>Mark a cell known (a scout passed over it). False when it was already known or is off the map.</summary>
    public bool Know(HexCoord coord)
    {
        var tile = Get(coord);
        if (tile == null || tile.known) return false;
        tile.known = true;
        tile.revealed = true;
        _known++;
        KnowledgeVersion++;
        return true;
    }

    /// <summary>A unit walked on or beside a micro hex: it becomes known, and so does its cell. True when the cell became known.</summary>
    public bool KnowMicro(HexCoord micro)
    {
        int id = MicroNavigation.Index(this, micro);
        if (id < 0) return false;
        var tile = this[id / MicroNavigation.PerCell];
        int bit = 1 << (id % MicroNavigation.PerCell);
        if ((tile.microKnownMask & bit) == 0)
        {
            tile.microKnownMask |= bit;
            KnowledgeVersion++;
        }
        return Know(tile.coord);
    }

    /// <summary>
    /// A unit surveyed a micro hex (it becomes known too). Its cell is explored once every hex of it that can be
    /// entered was surveyed. True when the cell became explored.
    /// </summary>
    public bool SurveyMicro(HexCoord micro)
    {
        int id = MicroNavigation.Index(this, micro);
        if (id < 0) return false;
        var tile = this[id / MicroNavigation.PerCell];
        int bit = 1 << (id % MicroNavigation.PerCell);
        if ((tile.microSurveyMask & bit) == 0)
        {
            tile.microSurveyMask |= bit;
            KnowledgeVersion++;
        }
        KnowMicro(micro);
        return ((tile.microSurveyMask | tile.microBlockedMask) & 127) == 127 && Explore(tile.coord);
    }

    /// <summary>Micro hexes of a cell still to survey before it is explored (crags aside).</summary>
    public static int UnsurveyedHexes(WorldTile tile)
    {
        if (tile == null || tile.explored) return 0;
        int left = ~(tile.microSurveyMask | tile.microBlockedMask) & 127, n = 0;
        for (; left != 0; left &= left - 1) n++;
        return n;
    }

    /// <summary>Micro hexes of a cell a unit can stand on (crags aside).</summary>
    public static int OpenHexes(WorldTile tile) => tile == null ? 0 : MicroNavigation.PerCell - MicroNavigation.Crags(tile.microBlockedMask);

    /// <summary>Every hex of the cell a unit can stand on has been seen up close (walked on or beside, or surveyed): passing expeditions explore it.</summary>
    public static bool FullySeen(WorldTile tile) =>
        tile != null && ((tile.microKnownMask | tile.microSurveyMask | tile.microBlockedMask) & 127) == 127;

    /// <summary>Hexes of the cell seen up close (crags aside), out of <see cref="OpenHexes"/>.</summary>
    public static int SeenHexes(WorldTile tile) =>
        tile == null ? 0 : MicroNavigation.Crags((tile.microKnownMask | tile.microSurveyMask) & ~tile.microBlockedMask);

    /// <summary>Hexes of the cell surveyed one by one (crags aside), out of <see cref="OpenHexes"/>.</summary>
    public static int SurveyedHexes(WorldTile tile) =>
        tile == null ? 0 : MicroNavigation.Crags(tile.microSurveyMask & ~tile.microBlockedMask);

    /// <summary>Hexes of a wilderness cell your people hold (crags aside), out of <see cref="OpenHexes"/>.</summary>
    public static int SettledHexes(WorldTile tile) =>
        tile == null ? 0 : MicroNavigation.Crags(tile.microHeldMask & ~tile.microBlockedMask);

    /// <summary>Mark a cell explored (surveyed; also known) and reveal its neighbours. False when it was already explored or is off the map.</summary>
    public bool Explore(HexCoord coord)
    {
        var tile = Get(coord);
        if (tile == null || tile.explored) return false;
        Know(coord);
        tile.explored = true;
        tile.revealed = true;
        _explored++;
        _frontier.Remove(coord);
        foreach (var n in coord.Neighbors())
        {
            var neighbour = Get(n);
            if (neighbour != null && !neighbour.explored) _frontier.Add(n);
        }
        Reveal(coord, 1);
        KnowledgeVersion++;
        return true;
    }

    /// <summary>
    /// Explore the passable ground within <paramref name="exploreRadius"/> of <paramref name="center"/> that the
    /// expedition can walk to from it, and reveal everything within <paramref name="revealRadius"/>. Returns the
    /// cells newly explored.
    /// </summary>
    public List<WorldTile> ExploreAround(HexCoord center, int exploreRadius, int revealRadius, Func<string, bool> isPassable) =>
        Spread(center, exploreRadius, revealRadius, isPassable, Explore);

    /// <summary>
    /// What a scout learns passing through <paramref name="center"/>: the passable ground within <paramref name="knowRadius"/>
    /// it could walk to becomes known, and everything within <paramref name="revealRadius"/> comes out of the fog.
    /// Returns the cells newly known.
    /// </summary>
    public List<WorldTile> KnowAround(HexCoord center, int knowRadius, int revealRadius, Func<string, bool> isPassable) =>
        Spread(center, knowRadius, revealRadius, isPassable, Know);

    private List<WorldTile> Spread(HexCoord center, int exploreRadius, int revealRadius, Func<string, bool> isPassable, Func<HexCoord, bool> mark)
    {
        var found = new List<WorldTile>();
        var start = Get(center);
        if (start == null) return found;
        var seen = new HashSet<HexCoord> { center };
        var open = new Queue<HexCoord>();
        open.Enqueue(center);
        while (open.Count > 0)
        {
            var coord = open.Dequeue();
            var tile = Get(coord);
            if (mark(coord)) found.Add(tile);
            foreach (var n in coord.Neighbors())
            {
                var next = Get(n);
                if (next == null || !seen.Add(n) || HexCoord.Distance(n, center) > exploreRadius) continue;
                if (next.water || next.impassable || (isPassable != null && !isPassable(next.terrain))) continue;
                open.Enqueue(n);
            }
        }
        Reveal(center, revealRadius);
        return found;
    }

    public bool TouchesExplored(HexCoord coord) => coord.Neighbors().Any(n => Get(n)?.explored == true);

    /// <summary>
    /// Whether an expedition may set out for <paramref name="coord"/>: revealed, not yet explored, dry and passable,
    /// and next to explored ground.
    /// </summary>
    public bool CanExplore(HexCoord coord, Func<string, bool> isPassable)
    {
        var tile = Get(coord);
        if (tile == null || tile.explored || !tile.revealed || tile.water || tile.impassable) return false;
        if (isPassable != null && !isPassable(tile.terrain)) return false;
        return _frontier.Contains(coord) || TouchesExplored(coord);
    }

    /// <summary>Every cell an expedition could set out for now.</summary>
    public List<WorldTile> Frontier(Func<string, bool> isPassable) =>
        _frontier.Where(c => CanExplore(c, isPassable)).Select(Get).OrderBy(t => t.index).ToList();

    /// <summary>Rebuild cached exploration after restoring tile knowledge without discoveries/rewards.</summary>
    public void RebuildKnowledge()
    {
        _frontier.Clear(); _explored = _list.Count(t => t.explored); _known = _list.Count(t => t.known);
        foreach (var tile in _list.Where(t => t.explored))
            foreach (var next in NeighboursOf(tile)) if (!next.explored) _frontier.Add(next.coord);
        KnowledgeVersion++;
    }
    public bool IsFrontier(HexCoord coord) => _frontier.Contains(coord);

    public int ExploredCount => _explored;

    public int KnownCount => _known;

    public IEnumerable<WorldTile> WithFeature(string featureId) =>
        _list.Where(t => string.Equals(t.feature, featureId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Cell steps from the capital.</summary>
    public int StepsFromCapital(HexCoord coord) => HexCoord.Distance(coord, Capital);

    // ===== AGGREGATES (macro) =====

    /// <summary>The macro aggregate (two more index-7 levels up) that owns a meso cell.</summary>
    public static HexCoord MacroOf(HexCoord meso) => HexHierarchy.Ancestor(meso, HexHierarchy.Macro - HexHierarchy.Meso);

    /// <summary>The map's cells inside one macro aggregate (only real cells: edge aggregates hold fewer than 49).</summary>
    public List<WorldTile> CellsOfMacro(HexCoord macro)
    {
        var cells = new List<WorldTile>();
        foreach (var intermediate in HexHierarchy.Children(macro))
            foreach (var meso in HexHierarchy.Children(intermediate))
            {
                var tile = Get(meso);
                if (tile != null) cells.Add(tile);
            }
        return cells;
    }
}

/// <summary>
/// Administrative Authority (Arcanoria.md, Settlements): territory follows passable land from its anchors and stops
/// at water, barriers and other claims; discovering a tile never annexes it. Rebuilt from scratch in a fixed order:
/// the Capital, then the independent claims already standing (enclaves, independent sites), then the Major
/// Settlements and Developing Towns that extend the Capital's reach. Outposts are detached: they hold their own cell
/// outside Administrative Authority. Beyond each settlement's core, land joins one micro hex at a time, by claims
/// (paid) and by territorial pull (<see cref="WorldTerritory"/>; <see cref="WorldTile.microHeldMask"/>): a cell whose
/// every hex is held joins the authority whole (saved in <see cref="WorldMap.Claims"/> or <see cref="WorldMap.Adopted"/>).
/// </summary>
public static class WorldAuthority
{
    public const string Wilderness = "wilderness", Player = "player", Outpost = "outpost";

    /// <summary>Held by the player, inside Administrative Authority or as a detached Outpost.</summary>
    public static bool IsPlayers(string authorityId) => authorityId == Player || authorityId == Outpost;

    /// <summary>Administrative reach of a claimed cell (the Capital's own cell has 1).</summary>
    public const float ClaimedReach = 0.5f;

    /// <summary>
    /// Why no hex of <paramref name="t"/> can be claimed, or null: it must be passable land (known: a scout has passed
    /// over it, not only seen it), wilderness or yours de facto (not yet core), with a hex no one holds that borders
    /// ground you already hold. Claims go one micro hex at a time (<see cref="WhyNotClaimHex"/>); a hex another holder
    /// holds is never claimed peacefully (<see cref="WorldHoldings.CasusBelliOf"/>).
    /// </summary>
    public static string WhyNotClaim(WorldMap map, WorldTile t)
    {
        if (map == null || t == null) return "Beyond the edge of the world.";
        if (t.water) return "Open water cannot be claimed.";
        if (t.impassable) return "No one can hold this ground.";
        if (IsPlayers(t.authorityId) && !WorldHoldings.Fillable(t, t.authorityId)) return "It is already yours.";
        if (t.authorityId != Wilderness && !IsPlayers(t.authorityId)) return "Another authority holds this ground.";
        if (map.Claiming.Contains(t.index)) return "Your people are already settling it, hex by hex.";
        if (!t.known) return "A scout must pass over it first.";
        if (WorldTerritory.NextHex(map, t, Player) < 0)
            return WorldHoldings.FreeMask(map, t) == 0 ? "Every hex of it is held: what others hold is only taken by force." : "No hex of it borders land you hold.";
        return null;
    }

    /// <summary>
    /// Why the micro hex <paramref name="id"/> (<see cref="MicroNavigation"/>) cannot be claimed, or null: its cell
    /// must allow claims (<see cref="WhyNotClaim"/>), and the hex must be open ground no one holds, touching a hex you hold.
    /// </summary>
    public static string WhyNotClaimHex(WorldMap map, int id)
    {
        if (map == null || id < 0 || id >= map.Count * MicroNavigation.PerCell) return "Beyond the edge of the world.";
        var t = map[id / MicroNavigation.PerCell];
        int bit = 1 << (id % MicroNavigation.PerCell);
        if ((t.microBlockedMask & bit) != 0) return "Crags no one can hold.";
        string holder = WorldHoldings.HexHolder(map, id);
        if (holder != null) return IsPlayers(holder) ? "This hex is already yours." : "Another holds this hex: it is only taken by force.";
        string why = WhyNotClaim(map, t);
        if (why != null) return why;
        if (WorldTerritory.Touching(map, id, Player) == 0) return "It must border a hex you hold.";
        return null;
    }

    /// <summary>Cells with a hex that could be claimed now (cost aside).</summary>
    public static IEnumerable<WorldTile> Claimable(WorldMap map) =>
        map == null ? Enumerable.Empty<WorldTile>() : map.Tiles.Where(t => t.known && WorldHoldings.Fillable(t, Player) && WhyNotClaim(map, t) == null);

    /// <summary>What the next claim costs: the base cost raised by <paramref name="growth"/> for each cell's worth already claimed.</summary>
    public static float ClaimScale(float claimedCells, float growth) => 1f + Math.Max(0f, growth) * Math.Max(0f, claimedCells);

    public static void Establish(WorldMap map, WorldGenSettings settings)
    {
        foreach (var t in map.Tiles)
        {
            t.impassable = !t.water && settings.Terrain(t.terrain)?.passable == false;
            t.authorityId = Wilderness;
            t.administrativeAuthority = 0f;
            t.whole = false;
            t.hold = HoldStatus.None;
        }
        // Land held hex by hex (claimed, adopted, taken) answers to whoever holds most of it, once they hold enough
        // (WorldHoldings.Ruler: de facto from 4 of 7 hexes, core with all). It comes first: no one's reach overrides it.
        // A whole-cell claim or adoption of an older save held every hex.
        foreach (int cell in map.Claims.Concat(map.Adopted))
        {
            if (cell < 0 || cell >= map.Count) continue;
            var t = map[cell];
            if (t.microHeldMask == 0 && WorldHoldings.At(map, cell).Count == 0) t.microHeldMask = WorldHoldings.OpenMask(t);
        }
        foreach (var t in map.Tiles)
        {
            if (t.water || t.impassable || (t.microHeldMask == 0 && WorldHoldings.At(map, t.index).Count == 0)) continue;
            string ruler = WorldHoldings.Ruler(map, t);
            if (ruler == null) continue;
            t.authorityId = ruler;
            t.administrativeAuthority = Math.Max(t.administrativeAuthority, ClaimedReach);
        }
        Project(map, map.Capital, Player, settings.capitalAuthorityRadius);
        foreach (var enclave in map.Enclaves.OrderBy(e => e.index))
        {
            var t = map.Get(enclave.coord);
            if (t != null) Project(map, t.coord, enclave.AuthorityId, enclave.authorityRadius);
        }
        foreach (var t in map.Tiles.Where(t => t.HasFeature).OrderBy(t => t.index))
        {
            var feature = settings.Feature(t.feature);
            if (!string.IsNullOrEmpty(feature?.authorityId))
                Project(map, t.coord, feature.authorityId + ":" + t.index, feature.authorityRadius);
        }
        // Major Settlements anchor the most reach; each town then extends it a little.
        foreach (var s in map.Settlements.Where(s => !s.detached).OrderByDescending(s => s.kind == SettlementKind.Major).ThenBy(s => s.id))
            Project(map, s.coord, Player, s.authorityRadius);
        foreach (var s in map.Settlements.Where(s => s.detached).OrderBy(s => s.id))
        {
            var t = map.Get(s.coord);
            if (t == null || t.authorityId != Wilderness) continue;
            t.authorityId = Outpost;
            t.whole = true;
        }
        // How firmly each holder holds its cells: de facto or core.
        foreach (var t in map.Tiles)
            if (!t.water && t.authorityId != Wilderness) t.hold = WorldHoldings.Status(map, t, t.authorityId);
    }

    public static void Project(WorldMap map, HexCoord center, string owner, int radius)
    {
        var start = map.Get(center);
        if (start == null || start.water || start.impassable || string.IsNullOrEmpty(owner)) return;
        radius = Math.Max(0, radius);
        var open = new Queue<(int cell, int distance)>();
        var seen = new HashSet<int> { start.index };
        open.Enqueue((start.index, 0));
        while (open.Count > 0)
        {
            var item = open.Dequeue();
            var t = map[item.cell];
            if (t.authorityId != Wilderness && t.authorityId != owner) continue;
            t.authorityId = owner;
            // Given whole: every hex no one else holds is the owner's.
            t.whole = true;
            t.administrativeAuthority = Math.Max(t.administrativeAuthority, 1f - item.distance / (float)(radius + 1));
            if (item.distance == radius) continue;
            foreach (var next in map.NeighboursOf(t))
                if (!next.water && !next.impassable && seen.Add(next.index)) open.Enqueue((next.index, item.distance + 1));
        }
    }
}
