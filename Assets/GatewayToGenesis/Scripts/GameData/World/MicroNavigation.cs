using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>How far an ability reaches from the micro hex its unit stands on (<see cref="UnitAbilities"/>).</summary>
public enum AbilityReach
{
    /// <summary>The micro hex it stands on.</summary>
    Hex,
    /// <summary>The micro hexes within the ability's radius.</summary>
    Around,
    /// <summary>The whole meso hex it stands in: all seven of its micro hexes.</summary>
    Meso,
}

/// <summary>
/// The micro hexes as the ground units walk (WORLD_GENERATION.md, "Micro travel"): every meso cell owns seven, so the
/// travel grid is seven times finer than the strategy grid. A micro hex is addressed by its cell's index times seven
/// plus its place among the cell's children (<see cref="HexHierarchy.ChildOffsets"/>); its neighbours follow one fixed
/// pattern (a parent's centre always has residue 0, so the lattice looks the same around every cell), so the world's
/// graph is never stored twice. With no scene state (tested in <c>WorldUnitTests</c>).
///
/// Orders: a micro target is walked to exactly, a meso target at its centre hex; either falls back to the nearest
/// hex the unit can reach when the one asked for cannot be entered. What a step costs lives in <see cref="MicroGrid"/>.
/// </summary>
public static class MicroNavigation
{
    /// <summary>Micro hexes per meso cell.</summary>
    public const int PerCell = 7;
    /// <summary>Largest travel fatigue a single order plans for (a whole continent is far less).</summary>
    public const float MaxPlan = 4000f;
    /// <summary>How far (in micro hexes) from a target that cannot be entered the nearest reachable ground is sought.</summary>
    public const int NearestReach = 6;

    // For child k and direction d (HexCoord.Directions): the neighbour's cell (-1 the same cell, else the meso
    // direction it lies in) and which of that cell's children it is.
    private static readonly sbyte[] PatternCell = new sbyte[PerCell * 6];
    private static readonly byte[] PatternChild = new byte[PerCell * 6];
    // Children of a cell touching a neighbouring cell in meso direction d, paired with the neighbour's children they touch.
    private static readonly List<(int mine, int theirs)>[] ContactPairs = new List<(int, int)>[6];

    static MicroNavigation()
    {
        for (int d = 0; d < 6; d++) ContactPairs[d] = new List<(int, int)>();
        var origin = HexHierarchy.ChildCenter(HexCoord.Zero);
        for (int k = 0; k < PerCell; k++)
        {
            for (int d = 0; d < 6; d++)
            {
                var micro = origin + HexHierarchy.ChildOffsets[k] + HexCoord.Directions[d];
                var parent = HexHierarchy.Parent(micro);
                int cell = parent == HexCoord.Zero ? -1 : Array.IndexOf(HexCoord.Directions, parent);
                if (parent != HexCoord.Zero && cell < 0) throw new InvalidOperationException($"Micro hex {micro} lies beyond the neighbouring cells.");
                int child = ChildIndex(HexHierarchy.OffsetInParent(micro));
                PatternCell[k * 6 + d] = (sbyte)cell;
                PatternChild[k * 6 + d] = (byte)child;
                if (cell >= 0 && !ContactPairs[cell].Contains((k, child))) ContactPairs[cell].Add((k, child));
            }
        }
    }

    // ===== ADDRESSES =====

    /// <summary>Which of its cell's children an offset from the cell's centre is (-1 for none).</summary>
    public static int ChildIndex(HexCoord offset)
    {
        for (int i = 0; i < PerCell; i++) if (HexHierarchy.ChildOffsets[i] == offset) return i;
        return -1;
    }

    /// <summary>Id of a micro hex (its cell's index x 7 + child), or -1 off the map.</summary>
    public static int Index(WorldMap map, HexCoord micro)
    {
        var parent = map?.Get(HexHierarchy.Parent(micro));
        return parent == null ? -1 : parent.index * PerCell + ChildIndex(HexHierarchy.OffsetInParent(micro));
    }

    public static HexCoord Coord(WorldMap map, int id) => HexHierarchy.ChildCenter(map[id / PerCell].coord) + HexHierarchy.ChildOffsets[id % PerCell];

    /// <summary>The meso cell owning a micro hex, or null off the map.</summary>
    public static WorldTile Parent(WorldMap map, HexCoord micro) => map?.Get(HexHierarchy.Parent(micro));

    /// <summary>The micro hex at the centre of a meso cell (where its sites and settlements stand).</summary>
    public static HexCoord Center(HexCoord meso) => HexHierarchy.ChildCenter(meso);

    /// <summary>The neighbour of micro hex <paramref name="id"/> in <paramref name="direction"/>, or -1 off the map.</summary>
    public static int Neighbour(WorldMap map, int id, int direction)
    {
        int cell = id / PerCell, p = id % PerCell * 6 + direction;
        int d = PatternCell[p];
        int next = d < 0 ? cell : map.Neighbour(cell, d);
        return next < 0 ? -1 : next * PerCell + PatternChild[p];
    }

    /// <summary>Children of a cell that touch its neighbour in meso direction <paramref name="direction"/>, each with the neighbour's child it touches.</summary>
    public static IReadOnlyList<(int mine, int theirs)> Contacts(int direction) => ContactPairs[((direction % 6) + 6) % 6];

    // ===== GROUND =====

    /// <summary>Chance that a rim hex on a seam between two kinds of land carries the neighbouring cell's ground.</summary>
    public const double BorrowChance = 0.34;

    /// <summary>The seed of the micro ground's ragged seams (the same one <see cref="WorldRenderer"/> paints with).</summary>
    public static int GroundSeed(WorldMap map) => WorldNoise.Stream(map.seed, "micro");

    /// <summary>A cell that can be walked at all: dry, and its ground passable.</summary>
    public static bool Walkable(WorldTile t, WorldGenSettings settings)
    {
        if (t == null || t.water || t.impassable) return false;
        var terrain = settings?.Terrain(t.terrain);
        return terrain == null || (terrain.passable && !terrain.water);
    }

    /// <summary>
    /// The cell whose ground a micro hex carries: its own, except that a rim hex on a seam between two kinds of
    /// walkable land may carry the neighbouring cell's (seams are ragged at the micro scale, never straight). Water and
    /// impassable ground never spill over, so every shore and mountain wall is where its cell says.
    /// </summary>
    public static WorldTile GroundCell(WorldMap map, WorldGenSettings settings, HexCoord micro, int seed)
    {
        var parent = Parent(map, micro);
        if (parent == null) return null;
        var offset = HexHierarchy.OffsetInParent(micro);
        if (offset == HexCoord.Zero || !Walkable(parent, settings)) return parent;
        var beyond = map.Get(HexHierarchy.Parent(micro + offset));
        if (beyond == null || beyond == parent || beyond.terrain == parent.terrain || !Walkable(beyond, settings)) return parent;
        return WorldNoise.Hash01(seed, micro.q, micro.r) < BorrowChance ? beyond : parent;
    }

    public static WorldTile GroundCell(WorldMap map, WorldGenSettings settings, HexCoord micro) => map == null ? null : GroundCell(map, settings, micro, GroundSeed(map));

    /// <summary>The travel grid of a map (built on first use and kept with the map).</summary>
    public static MicroGrid Grid(WorldMap map, WorldGenSettings settings)
    {
        var grid = map.microGrid;
        if (grid == null || grid.Settings != settings || grid.Count != map.Count * PerCell)
            map.microGrid = grid = new MicroGrid(map, settings);
        return grid;
    }

    /// <summary>Forget a map's travel grid (after its ground was changed by hand, as tests do).</summary>
    public static void Invalidate(WorldMap map)
    {
        if (map != null) map.microGrid = null;
    }

    /// <summary>Travel fatigue of entering a micro hex (without the slope from where it is entered), or +infinity.</summary>
    public static float Cost(WorldMap map, WorldGenSettings settings, HexCoord micro)
    {
        int id = Index(map, micro);
        return id < 0 ? float.PositiveInfinity : Grid(map, settings).Enter(id);
    }

    public static bool Enterable(WorldMap map, WorldGenSettings settings, HexCoord micro) => !float.IsPositiveInfinity(Cost(map, settings, micro));

    // ===== ROUTES =====

    /// <summary>The cheapest way from <paramref name="from"/> to <paramref name="to"/> exactly (micro ids after the start) and its travel fatigue.</summary>
    public static bool Find(WorldMap map, WorldGenSettings settings, HexCoord from, HexCoord to, out List<int> path, out float cost, float maxCost = MaxPlan, int maxVisits = int.MaxValue)
    {
        path = new List<int>();
        cost = 0f;
        var grid = Grid(map, settings);
        int start = Index(map, from), goal = Index(map, to);
        if (start < 0 || goal < 0 || !grid.SameGround(start, goal)) return false;
        return grid.Search(start, id => id == goal, to, 0, maxCost, maxVisits, out path, out cost);
    }

    /// <summary>
    /// The way to <paramref name="target"/> if it can be entered and reached; otherwise to the nearest micro hex that
    /// can (ring by ring around it, up to <see cref="NearestReach"/>), the cheapest of the nearest ring. False when
    /// nothing near it can be reached. <paramref name="reached"/> is where the way ends.
    /// </summary>
    public static bool ToNearest(WorldMap map, WorldGenSettings settings, HexCoord from, HexCoord target, out List<int> path, out float cost, out HexCoord reached,
        float maxCost = MaxPlan, int maxVisits = int.MaxValue)
    {
        path = new List<int>();
        cost = 0f;
        reached = target;
        var grid = Grid(map, settings);
        int start = Index(map, from);
        if (start < 0) return false;
        for (int ring = 0; ring <= NearestReach; ring++)
        {
            var goals = new HashSet<int>();
            foreach (var hex in HexCoord.Ring(target, ring))
            {
                int id = Index(map, hex);
                if (id >= 0 && grid.SameGround(start, id)) goals.Add(id);
            }
            if (goals.Count == 0) continue;
            if (!grid.Search(start, goals.Contains, target, ring, maxCost, maxVisits, out path, out cost)) return false;
            reached = path.Count > 0 ? Coord(map, path[path.Count - 1]) : from;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether ground joins <paramref name="from"/> to <paramref name="target"/> or to some hex near it (what
    /// <see cref="ToNearest"/> would walk to), without searching the way.
    /// </summary>
    public static bool Reachable(WorldMap map, WorldGenSettings settings, HexCoord from, HexCoord target)
    {
        var grid = Grid(map, settings);
        int start = Index(map, from);
        if (start < 0) return false;
        foreach (var hex in HexCoord.Spiral(target, NearestReach))
        {
            int id = Index(map, hex);
            if (id >= 0 && grid.SameGround(start, id)) return true;
        }
        return false;
    }

    /// <summary>An order given at the meso scale: to the cell's centre hex, or the nearest one the unit can reach.</summary>
    public static bool ToMeso(WorldMap map, WorldGenSettings settings, HexCoord from, HexCoord meso, out List<int> path, out float cost, out HexCoord reached,
        float maxCost = MaxPlan, int maxVisits = int.MaxValue)
    {
        path = new List<int>();
        cost = 0f;
        reached = Center(meso);
        return map.InBounds(meso) && ToNearest(map, settings, from, Center(meso), out path, out cost, out reached, maxCost, maxVisits);
    }

    /// <summary>
    /// The cheapest way to the first micro hex satisfying <paramref name="isGoal"/> (the nearest unknown ground, the
    /// nearest settlement...), within <paramref name="maxCost"/>.
    /// </summary>
    public static bool FindNearest(WorldMap map, WorldGenSettings settings, HexCoord from, Func<int, bool> isGoal, out List<int> path, out float cost,
        float maxCost = MaxPlan, int maxVisits = int.MaxValue)
    {
        path = new List<int>();
        cost = 0f;
        int start = Index(map, from);
        return start >= 0 && isGoal != null && Grid(map, settings).Search(start, isGoal, from, -1, maxCost, maxVisits, out path, out cost);
    }

    // ===== ABILITIES AND KNOWLEDGE =====

    /// <summary>The micro hexes an ability reaches from <paramref name="micro"/> (only those on the map).</summary>
    public static IEnumerable<HexCoord> Area(WorldMap map, HexCoord micro, AbilityReach reach, int radius)
    {
        IEnumerable<HexCoord> area = reach == AbilityReach.Meso ? HexHierarchy.Children(HexHierarchy.Parent(micro))
            : reach == AbilityReach.Around ? HexCoord.Spiral(micro, Math.Max(0, radius)) : new[] { micro };
        return area.Where(c => Index(map, c) >= 0);
    }

    /// <summary>How many of a cell's seven micro hexes a mask marks.</summary>
    public static int Crags(int mask)
    {
        int n = 0;
        for (int m = mask & 127; m != 0; m &= m - 1) n++;
        return n;
    }

    /// <summary>A crag: a micro hex of an escarpment no one climbs.</summary>
    public static bool Crag(WorldMap map, HexCoord micro)
    {
        int id = Index(map, micro);
        return id >= 0 && (map[id / PerCell].microBlockedMask & (1 << (id % PerCell))) != 0;
    }

    /// <summary>A unit surveyed it (or its whole cell is explored).</summary>
    public static bool Surveyed(WorldMap map, HexCoord micro) => Surveyed(map, Index(map, micro));

    public static bool Surveyed(WorldMap map, int id)
    {
        if (id < 0) return true;
        var t = map[id / PerCell];
        return t.explored || (t.microSurveyMask & (1 << (id % PerCell))) != 0;
    }

    /// <summary>A unit walked on or beside it (or surveyed it).</summary>
    public static bool Known(WorldMap map, int id)
    {
        if (id < 0) return false;
        var t = map[id / PerCell];
        int bit = 1 << (id % PerCell);
        return t.explored || ((t.microKnownMask | t.microSurveyMask) & bit) != 0;
    }

    public static bool Known(WorldMap map, HexCoord micro) => Known(map, Index(map, micro));
}

/// <summary>
/// A map's travel grid: what entering each micro hex costs, and which ground joins which. Built once per map and
/// settings (<see cref="MicroNavigation.Grid"/>); roads follow the map's routes whenever the civilization changes.
///
/// A step's travel fatigue: the ground of the hex entered (its terrain's move cost), roughened off-road on steep ground,
/// a ford where it crosses a river (a road bridges it), halved on a road; then its cell's leylines, dissonance,
/// danger and weather (<see cref="CellFactor"/>, the same as <see cref="WorldPaths.StepCost"/>); and between cells the
/// slope: climbing tires three times more than going down. Crags on escarpments cannot be entered (a road cuts
/// through). Every step costs at least <see cref="MinStep"/>, so <see cref="Search"/> is an exact A*.
/// </summary>
public sealed class MicroGrid
{
    [Flags]
    public enum Mark : byte
    {
        None = 0,
        /// <summary>A crag on an escarpment.</summary>
        Crag = 1,
        /// <summary>A river runs through it (fording it slows the crossing).</summary>
        Ford = 2,
        /// <summary>A great river runs through it (a wide, deep ford).</summary>
        GreatFord = 4,
        /// <summary>A road runs through it.</summary>
        Road = 8,
        /// <summary>It carries a neighbouring cell's ground (a ragged seam).</summary>
        Borrowed = 16,
    }

    /// <summary>Extra fatigue of a step up per unit of height climbed, and of a step down (proposals).</summary>
    public const float ClimbCost = 6f, DescentCost = 2f;
    /// <summary>How much a ford multiplies a step (a great river's more).</summary>
    public const float FordFactor = 1.5f, GreatFordFactor = 2.2f;
    /// <summary>Most a hex of steep ground is roughened off-road.</summary>
    public const float MaxRoughness = 0.6f;

    public WorldMap Map { get; }
    public WorldGenSettings Settings { get; }
    public int Count { get; }

    private readonly float[] _ground;
    private readonly float[] _rough;
    private readonly byte[] _marks;
    private readonly short[] _terrain;
    private readonly int[] _component;
    private readonly List<TerrainSpec> _terrains = new List<TerrainSpec>();
    private float _minGround = float.PositiveInfinity;
    private int _roadVersion = int.MinValue, _roads;

    // Search state, reused (a stamp marks what the current search has touched).
    private readonly float[] _g;
    private readonly int[] _prev, _open, _closed;
    private int _stamp;
    private readonly Heap _heap = new Heap();

    public MicroGrid(WorldMap map, WorldGenSettings settings)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Settings = settings;
        Count = map.Count * MicroNavigation.PerCell;
        _ground = new float[Count];
        _rough = new float[Count];
        _marks = new byte[Count];
        _terrain = new short[Count];
        _component = new int[Count];
        _g = new float[Count];
        _prev = new int[Count];
        _open = new int[Count];
        _closed = new int[Count];
        BuildGround();
        BuildRivers();
        BuildComponents();
    }

    // ===== BUILD =====

    private void BuildGround()
    {
        var bySpec = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);
        short TerrainIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            if (bySpec.TryGetValue(id, out short i)) return i;
            var spec = Settings?.Terrain(id);
            i = spec == null ? (short)-1 : (short)_terrains.Count;
            if (spec != null) _terrains.Add(spec);
            bySpec[id] = i;
            return i;
        }
        int seed = MicroNavigation.GroundSeed(Map), roughSeed = WorldNoise.Stream(Map.seed, "micro:rough");
        var walkable = new bool[Map.Count];
        for (int c = 0; c < Map.Count; c++) walkable[c] = MicroNavigation.Walkable(Map[c], Settings);
        for (int id = 0; id < Count; id++)
        {
            var cell = Map[id / MicroNavigation.PerCell];
            int child = id % MicroNavigation.PerCell;
            var micro = HexHierarchy.ChildCenter(cell.coord) + HexHierarchy.ChildOffsets[child];
            var ground = walkable[cell.index] ? MicroNavigation.GroundCell(Map, Settings, micro, seed) : cell;
            _terrain[id] = TerrainIndex(ground.terrain);
            if ((cell.microBlockedMask & (1 << child)) != 0) _marks[id] |= (byte)Mark.Crag;
            if (ground != cell) _marks[id] |= (byte)Mark.Borrowed;
            if (!walkable[cell.index])
            {
                _ground[id] = float.PositiveInfinity;
                _rough[id] = 1f;
                continue;
            }
            var terrain = _terrain[id] >= 0 ? _terrains[_terrain[id]] : null;
            _ground[id] = Math.Max(0.2f, terrain?.moveCost ?? 1f);
            _rough[id] = 1f + Math.Min(MaxRoughness, cell.escarpment * 4f) * (float)WorldNoise.Hash01(roughSeed, micro.q, micro.r);
            _minGround = Math.Min(_minGround, _ground[id]);
        }
        if (float.IsPositiveInfinity(_minGround)) _minGround = 1f;
    }

    private void BuildRivers()
    {
        float major = Math.Max(1f, Settings?.majorRiverThreshold ?? 110f);
        foreach (var river in Map.Rivers)
        {
            foreach (var (hex, flow) in WorldGeometry.Hexes(WorldGeometry.Smooth(WorldGeometry.RiverPoints(Map, river))))
            {
                int id = MicroNavigation.Index(Map, hex);
                if (id < 0 || float.IsPositiveInfinity(_ground[id])) continue;
                _marks[id] |= (byte)(flow >= major ? Mark.GreatFord : Mark.Ford);
            }
        }
    }

    // Ground joined by steps that can be taken (crags and water split it; roads never join separate ground: the
    // generator keeps every pair of walkable neighbouring cells joined).
    private void BuildComponents()
    {
        for (int i = 0; i < Count; i++) _component[i] = -1;
        var queue = new int[Count];
        int next = 0;
        for (int s = 0; s < Count; s++)
        {
            if (_component[s] >= 0 || !Open(s)) continue;
            int head = 0, tail = 0;
            queue[tail++] = s;
            _component[s] = next;
            while (head < tail)
            {
                int at = queue[head++];
                for (int d = 0; d < 6; d++)
                {
                    int n = MicroNavigation.Neighbour(Map, at, d);
                    if (n < 0 || _component[n] >= 0 || !Open(n)) continue;
                    _component[n] = next;
                    queue[tail++] = n;
                }
            }
            next++;
        }
    }

    private bool Open(int id) => !float.IsPositiveInfinity(_ground[id]) && (_marks[id] & (byte)Mark.Crag) == 0;

    // Roads are laid hex by hex along the line the map draws; rebuilt whenever the civilization changed.
    private void RefreshRoads()
    {
        if (_roadVersion == Map.CivilizationVersion) return;
        _roadVersion = Map.CivilizationVersion;
        _roads = 0;
        for (int i = 0; i < Count; i++) _marks[i] &= unchecked((byte)~Mark.Road);
        foreach (var route in Map.Routes)
        {
            foreach (var (hex, _) in WorldGeometry.Hexes(WorldGeometry.Smooth(WorldGeometry.RoadPoints(Map, route))))
            {
                int id = MicroNavigation.Index(Map, hex);
                if (id < 0 || float.IsPositiveInfinity(_ground[id])) continue;
                _marks[id] |= (byte)Mark.Road;
                _roads++;
                // A road cut through a crag joins it to the ground around it.
                if (_component[id] < 0)
                    for (int d = 0; d < 6 && _component[id] < 0; d++)
                    {
                        int n = MicroNavigation.Neighbour(Map, id, d);
                        if (n >= 0 && _component[n] >= 0) _component[id] = _component[n];
                    }
            }
        }
    }

    // ===== COSTS =====

    /// <summary>A cell's own share of every step into it: leylines speed, dissonance and danger slow, weather multiplies.</summary>
    public static float CellFactor(WorldTile t) =>
        (t.leylines != 0 ? WorldPaths.LeylineFactor : 1f) * (1f + 0.8f * t.dissonance + t.danger) * Math.Max(0.25f, Math.Min(4f, t.weatherTravelMultiplier));

    public Mark Marks(int id)
    {
        RefreshRoads();
        return (Mark)_marks[id];
    }

    /// <summary>The ground a micro hex carries (its terrain), or null.</summary>
    public TerrainSpec Ground(int id) => id >= 0 && id < Count && _terrain[id] >= 0 ? _terrains[_terrain[id]] : null;

    /// <summary>Travel fatigue of entering micro hex <paramref name="id"/> (no slope), or +infinity when it cannot be entered.</summary>
    public float Enter(int id)
    {
        if (id < 0 || id >= Count) return float.PositiveInfinity;
        RefreshRoads();
        float ground = _ground[id];
        if (float.IsPositiveInfinity(ground)) return ground;
        byte marks = _marks[id];
        bool road = (marks & (byte)Mark.Road) != 0;
        if (!road && (marks & (byte)Mark.Crag) != 0) return float.PositiveInfinity;
        float way = road ? WorldPaths.RoadFactor
            : _rough[id] * ((marks & (byte)Mark.GreatFord) != 0 ? GreatFordFactor : (marks & (byte)Mark.Ford) != 0 ? FordFactor : 1f);
        return ground * way * CellFactor(Map[id / MicroNavigation.PerCell]);
    }

    /// <summary>Travel fatigue of the step from <paramref name="from"/> into its neighbour <paramref name="to"/> (with the slope between their cells).</summary>
    public float Step(int from, int to)
    {
        float cost = Enter(to);
        if (float.IsPositiveInfinity(cost) || from < 0 || from / MicroNavigation.PerCell == to / MicroNavigation.PerCell) return cost;
        float rise = Map[to / MicroNavigation.PerCell].elevation - Map[from / MicroNavigation.PerCell].elevation;
        return cost * (1f + ClimbCost * Math.Max(0f, rise) + DescentCost * Math.Max(0f, -rise));
    }

    /// <summary>The cheapest step anywhere now (a lower bound of every step, for the search's estimate).</summary>
    public float MinStep()
    {
        RefreshRoads();
        float weather = 4f;
        bool leylines = false;
        foreach (var t in Map.List)
        {
            if (t.weatherTravelMultiplier < weather) weather = t.weatherTravelMultiplier;
            if (t.leylines != 0) leylines = true;
        }
        return _minGround * (_roads > 0 ? WorldPaths.RoadFactor : 1f) * (leylines ? WorldPaths.LeylineFactor : 1f) * Math.Max(0.25f, Math.Min(4f, weather));
    }

    /// <summary>Two micro hexes a unit could walk between (both can be entered and ground joins them).</summary>
    public bool SameGround(int a, int b)
    {
        if (a < 0 || b < 0 || a >= Count || b >= Count) return false;
        RefreshRoads();
        if (float.IsPositiveInfinity(Enter(b))) return false;
        // A unit may stand where it cannot enter (a crag it was set down on): it joins the ground around it.
        int ca = _component[a];
        if (ca < 0)
            for (int d = 0; d < 6 && ca < 0; d++)
            {
                int n = MicroNavigation.Neighbour(Map, a, d);
                if (n >= 0) ca = _component[n];
            }
        return ca >= 0 && ca == _component[b];
    }

    // ===== SEARCH =====

    /// <summary>
    /// A* from <paramref name="start"/> to the first micro hex satisfying <paramref name="isGoal"/>. Every goal lies
    /// within <paramref name="goalRadius"/> of <paramref name="goalCenter"/> (a negative radius: goals anywhere, a
    /// plain Dijkstra). The path lists the micro ids after the start; empty when the start is itself a goal.
    /// Gives up past <paramref name="maxCost"/> or <paramref name="maxVisits"/> hexes expanded.
    /// </summary>
    public bool Search(int start, Func<int, bool> isGoal, HexCoord goalCenter, int goalRadius, float maxCost, int maxVisits, out List<int> path, out float cost)
    {
        path = new List<int>();
        cost = 0f;
        if (start < 0 || start >= Count || isGoal == null) return false;
        RefreshRoads();
        float hMin = goalRadius >= 0 ? MinStep() : 0f;
        float Estimate(int id)
        {
            if (hMin <= 0f) return 0f;
            int d = HexCoord.Distance(MicroNavigation.Coord(Map, id), goalCenter) - goalRadius;
            return d > 0 ? d * hMin : 0f;
        }
        if (++_stamp == int.MaxValue)
        {
            Array.Clear(_open, 0, Count);
            Array.Clear(_closed, 0, Count);
            _stamp = 1;
        }
        _heap.Clear();
        _g[start] = 0f;
        _prev[start] = -1;
        _open[start] = _stamp;
        _heap.Push(Estimate(start), start);
        int goal = -1, visits = 0;
        while (_heap.Count > 0)
        {
            int at = _heap.Pop();
            if (_closed[at] == _stamp) continue;
            _closed[at] = _stamp;
            float g = _g[at];
            if (isGoal(at))
            {
                goal = at;
                cost = g;
                break;
            }
            if (++visits > maxVisits) break;
            for (int d = 0; d < 6; d++)
            {
                int next = MicroNavigation.Neighbour(Map, at, d);
                if (next < 0 || _closed[next] == _stamp) continue;
                float step = Step(at, next);
                if (float.IsPositiveInfinity(step) || float.IsNaN(step)) continue;
                float ng = g + step;
                if (ng > maxCost || (_open[next] == _stamp && _g[next] <= ng)) continue;
                _g[next] = ng;
                _prev[next] = at;
                _open[next] = _stamp;
                _heap.Push(ng + Estimate(next), next);
            }
        }
        if (goal < 0) return false;
        for (int at = goal; at != start; at = _prev[at]) path.Add(at);
        path.Reverse();
        return true;
    }

    // A binary min-heap of (key, id); ties go to the lower id, so searches are deterministic.
    private sealed class Heap
    {
        private float[] _keys = new float[256];
        private int[] _ids = new int[256];
        public int Count { get; private set; }

        public void Clear() => Count = 0;

        public void Push(float key, int id)
        {
            if (Count == _keys.Length)
            {
                Array.Resize(ref _keys, Count * 2);
                Array.Resize(ref _ids, Count * 2);
            }
            int c = Count++;
            while (c > 0)
            {
                int p = (c - 1) >> 1;
                if (!Less(key, id, _keys[p], _ids[p])) break;
                _keys[c] = _keys[p];
                _ids[c] = _ids[p];
                c = p;
            }
            _keys[c] = key;
            _ids[c] = id;
        }

        public int Pop()
        {
            int top = _ids[0];
            Count--;
            if (Count == 0) return top;
            float key = _keys[Count];
            int id = _ids[Count];
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1;
                if (l >= Count) break;
                int r = l + 1, m = r < Count && Less(_keys[r], _ids[r], _keys[l], _ids[l]) ? r : l;
                if (!Less(_keys[m], _ids[m], key, id)) break;
                _keys[c] = _keys[m];
                _ids[c] = _ids[m];
                c = m;
            }
            _keys[c] = key;
            _ids[c] = id;
            return top;
        }

        private static bool Less(float ka, int ia, float kb, int ib) => ka < kb || (ka == kb && ia < ib);
    }
}
