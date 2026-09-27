using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What part of the composition a cell grew from.</summary>
public enum WorldRegion
{
    /// <summary>The ocean (stencil W and the apron around the stencil).</summary>
    Ocean,
    /// <summary>Procedural connective terrain (stencil P): seams, passes, coasts.</summary>
    Connective,
    /// <summary>A sector's biome slot.</summary>
    Slot,
}

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
    public WorldRegion region;
    public string sector;
    /// <summary>Stencil slot index, or -1 in P and the ocean.</summary>
    public int slot = -1;
    /// <summary>The biome whose ground it carries (the nearest slot's in P), or null at sea.</summary>
    public string biome;
    /// <summary>In P or a slot's blended buffer band (not a protected interior).</summary>
    public bool seam;
    /// <summary>Stamped from a handmade tile (its id), or null.</summary>
    public string handmadeTile;
    public string terrain;
    /// <summary>Quarter of the world around the capital (0 north-east, 1 north-west, 2 south-west, 3 south-east).</summary>
    public int quadrant;

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
    public float Desirability => water || impassable ? 0f :
        0.45f * landFertility + 0.35f * coherence + 0.2f * magicFertility;

    // Sites and civilization (WorldSites, WorldCivilization; derived from their lists, rebuilt on change)
    /// <summary>Spawn potential of threats here, 0-1 (none on Sacred ground).</summary>
    public float danger;
    /// <summary>Index into <see cref="WorldMap.Grandfields"/>, or -1.</summary>
    public int grandfield = -1;
    /// <summary>How rich the grandfield is here: 1 at its heart, less toward its edge.</summary>
    public float grandfieldDensity;
    /// <summary>The gifted geography that can hold a Trade Nexus (harbor, estuary, pass, confluence), or null.</summary>
    public string nexus;
    public bool road;
    /// <summary>A Trade Node stands here (a checkpoint of a road).</summary>
    public bool tradeNode;
    /// <summary>Index into <see cref="WorldMap.Settlements"/> of the settlement on this cell, or -1.</summary>
    public int settlement = -1;
    /// <summary>Index into <see cref="WorldMap.Enclaves"/> of the enclave on this cell, or -1.</summary>
    public int enclave = -1;
    /// <summary>A builder's improvement level on this resource hotspot (0 none).</summary>
    public int improvement;

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
    public List<ThreatSite> Threats { get; } = new List<ThreatSite>();
    /// <summary>What play builds and changes (saved): settlements, roads and the enclaves the Ages bring.</summary>
    public List<Settlement> Settlements { get; set; } = new List<Settlement>();
    public List<TradeRoute> Routes { get; set; } = new List<TradeRoute>();
    public List<Enclave> Enclaves { get; set; } = new List<Enclave>();
    public List<WorldUnit> Units { get; set; } = new List<WorldUnit>();
    /// <summary>Wilderness cells the player claimed (cell indices, in the order claimed): held like the Capital's own ground.</summary>
    public List<int> Claims { get; set; } = new List<int>();
    /// <summary>Wilderness cells your society adopted by territorial pull (cell indices, in order; <see cref="WorldTerritory"/>).</summary>
    public List<int> Adopted { get; set; } = new List<int>();
    /// <summary>The territory rules the civilization layer reads when it rebuilds (set by <see cref="WorldSystem"/>; defaults otherwise).</summary>
    [NonSerialized] public TerritoryRules territoryRules;
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
/// outside Administrative Authority. Beyond each settlement's core, land joins by claims (paid) and by territorial
/// pull (<see cref="WorldTerritory"/>: adopted a cell at a time, saved in <see cref="WorldMap.Adopted"/>).
/// </summary>
public static class WorldAuthority
{
    public const string Wilderness = "wilderness", Player = "player", Outpost = "outpost";

    /// <summary>Held by the player, inside Administrative Authority or as a detached Outpost.</summary>
    public static bool IsPlayers(string authorityId) => authorityId == Player || authorityId == Outpost;

    /// <summary>Administrative reach of a claimed cell (the Capital's own cell has 1).</summary>
    public const float ClaimedReach = 0.5f;

    /// <summary>
    /// Why <paramref name="t"/> cannot be claimed, or null: it must be passable land, still wilderness,
    /// (known: a scout has passed over it, not only seen it) and bordering ground you already hold.
    /// </summary>
    public static string WhyNotClaim(WorldMap map, WorldTile t)
    {
        if (map == null || t == null) return "Beyond the edge of the world.";
        if (t.water) return "Open water cannot be claimed.";
        if (t.impassable) return "No one can hold this ground.";
        if (t.authorityId == Player) return "It is already yours.";
        if (t.authorityId != Wilderness) return "Another authority holds this ground.";
        if (!t.known) return "A scout must pass over it first.";
        if (!map.NeighboursOf(t).Any(n => n.authorityId == Player)) return "It must border your authority.";
        return null;
    }

    /// <summary>Cells that could be claimed now (cost aside).</summary>
    public static IEnumerable<WorldTile> Claimable(WorldMap map) =>
        map == null ? Enumerable.Empty<WorldTile>() : map.Tiles.Where(t => t.authorityId == Wilderness && t.known && WhyNotClaim(map, t) == null);

    /// <summary>What the next claim costs: the base cost raised by <paramref name="growth"/> for each cell already claimed.</summary>
    public static float ClaimScale(int claimed, float growth) => 1f + Math.Max(0f, growth) * Math.Max(0, claimed);

    public static void Establish(WorldMap map, WorldGenSettings settings)
    {
        foreach (var t in map.Tiles)
        {
            t.impassable = !t.water && settings.Terrain(t.terrain)?.passable == false;
            t.authorityId = Wilderness;
            t.administrativeAuthority = 0f;
        }
        Project(map, map.Capital, Player, settings.capitalAuthorityRadius);
        // Claimed wilderness is held like the Capital's own ground (claims are paid for, so they come before other claimants).
        foreach (int cell in map.Claims)
        {
            if (cell < 0 || cell >= map.Count) continue;
            var t = map[cell];
            if (t.water || t.impassable || t.authorityId != Wilderness) continue;
            t.authorityId = Player;
            t.administrativeAuthority = Math.Max(t.administrativeAuthority, ClaimedReach);
        }
        // Land adopted by territorial pull, the same way (an Outpost's pocket is told apart once the pull is known).
        foreach (int cell in map.Adopted)
        {
            if (cell < 0 || cell >= map.Count) continue;
            var t = map[cell];
            if (t.water || t.impassable || t.authorityId != Wilderness) continue;
            t.authorityId = Player;
            t.administrativeAuthority = Math.Max(t.administrativeAuthority, ClaimedReach);
        }
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
            if (t != null && t.authorityId == Wilderness) t.authorityId = Outpost;
        }
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
            t.administrativeAuthority = Math.Max(t.administrativeAuthority, 1f - item.distance / (float)(radius + 1));
            if (item.distance == radius) continue;
            foreach (var next in map.NeighboursOf(t))
                if (!next.water && !next.impassable && seen.Add(next.index)) open.Enqueue((next.index, item.distance + 1));
        }
    }
}
