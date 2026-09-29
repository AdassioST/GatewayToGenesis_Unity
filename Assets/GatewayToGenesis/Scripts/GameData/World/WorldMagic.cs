using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A Coherence Seed or a Dissonance Seed: a generation source of the magical field.</summary>
public class MagicSource
{
    public int id;
    public int cell;
    public float strength, radius;
    public bool dissonance;
}

/// <summary>A leyline family (the lineage of one Coherence Seed) and the path it takes in one Age.</summary>
public class Leyline
{
    /// <summary>Stable family id: the seed's lineage, whatever path it takes.</summary>
    public int family;
    /// <summary>Cells it crosses downhill from its seed to the first lake or ocean outlet (its footprint: junctions, silver water).</summary>
    public readonly List<int> cells = new List<int>();
    /// <summary>The curve itself, in world units (what the map draws, the same at every zoom).</summary>
    public readonly List<(float x, float y)> points = new List<(float x, float y)>();
}

/// <summary>Where distinct leyline families meet: two make a Convergence, three or more a Basin.</summary>
public class LeylineJunction
{
    public readonly List<int> cells = new List<int>();
    public readonly List<int> families = new List<int>();
    /// <summary>The cell where most families meet (where the marker stands).</summary>
    public int center;
    public bool IsBasin => families.Count >= 3;
}

/// <summary>
/// The magical geography (Docs/Planning/WORLD_GENERATION.md §5, roadmap WG07-WG09, WG12), with no scene state
/// (tested in <c>WorldGenerationTests</c>).
///
/// Fixed for the world: Coherence Seeds (one lineage each), Dissonance Seeds and the Sacred Sites, protected maxima
/// of the Age-free Coherence field. Each Age (<see cref="Apply"/>): the Grand Thread Rings turn, every seed's leyline
/// family is re-routed downhill through the rings' field toward a water outlet, preferring falling Coherence and rivers; families
/// meeting at one local footprint make a Convergence (two) or a Basin (three or more), counted by family, never by
/// segment; rivers a leyline crosses carry Lunehymn downstream, diluted by tributaries, and show silver above a
/// threshold, while a reach whose line moved away keeps a fading residue; Coherence and magical fertility follow.
/// Geography, ordinary rivers and Sacred Sites never move. <see cref="Forecast"/> routes an Age without changing
/// anything (the Age preview).
/// </summary>
public class WorldMagic
{
    public const int MaxFamilies = 64;
    private const int RingCount = 3;

    public List<MagicSource> Seeds { get; } = new List<MagicSource>();
    public List<int> SacredSites { get; } = new List<int>();
    public List<Leyline> Leylines { get; private set; } = new List<Leyline>();
    /// <summary>The paths of the Age before (the "previous Age" layer: why an old trading town declined).</summary>
    public List<Leyline> PreviousLeylines { get; private set; } = new List<Leyline>();
    public List<LeylineJunction> Junctions { get; private set; } = new List<LeylineJunction>();
    /// <summary>Cells holding a Resonance Anchor: each raises Coherence around it and draws nearby leylines toward it.</summary>
    public List<int> Anchors { get; } = new List<int>();
    private float _anchorCoherence = 0.2f;
    private int _anchorRadius = 4, _anchorPull = 10;
    /// <summary>The Age whose magic is in the map, or -1 before the first.</summary>
    public int Age { get; private set; } = -1;
    /// <summary>Bumped each time an Age's magic is committed (views redraw).</summary>
    public int Version { get; private set; }

    private WorldGenSettings _settings;
    private int _seed;
    private float[] _baseCoherence, _dissonance;
    private bool[] _protected;
    private (double x, double y, double radius, double phase, double speed, double orbit, double sense)[] _rings;

    public float BaseCoherence(int cell) => _baseCoherence[cell];

    public static WorldMagic Build(WorldMap map, WorldGenSettings settings, int seed)
    {
        var magic = new WorldMagic { _settings = settings, _seed = seed };
        int n = map.Count;
        var rng = new Random(WorldNoise.Stream(seed, "magic:seeds"));
        var capital = map.Get(map.Capital);

        // Coherence Seeds: in each slot's dry interior, as many as its quadrant asks.
        foreach (var placement in map.Placements)
        {
            int count = settings.Quadrant(placement.slot.quadrant)?.coherenceSeeds ?? 1;
            var cells = map.Tiles.Where(t => t.slot == placement.slot.index && !t.water && !t.seam).ToList();
            if (cells.Count == 0) cells = map.Tiles.Where(t => t.slot == placement.slot.index && !t.water).ToList();
            for (int k = 0; k < count && cells.Count > 0 && magic.Seeds.Count < MaxFamilies; k++)
            {
                // Headwaters favour high terrain; the seed supplies the local Coherence maximum.
                var headwaters = cells.OrderByDescending(t => t.elevation).ThenBy(t => t.index).Take(Math.Max(1, cells.Count / 4)).ToList();
                var cell = headwaters[rng.Next(headwaters.Count)];
                cells.Remove(cell);
                magic.Seeds.Add(new MagicSource { id = magic.Seeds.Count, cell = cell.index, strength = settings.seedStrength, radius = settings.seedRadius });
            }
        }
        // Dissonance Seeds: away from the capital.
        var far = map.Tiles.Where(t => !t.water && (capital == null || HexCoord.Distance(t.coord, capital.coord) >= 20)).ToList();
        for (int k = 0; k < settings.dissonanceSeeds && far.Count > 0; k++)
        {
            var cell = far[rng.Next(far.Count)];
            magic.Seeds.Add(new MagicSource { id = magic.Seeds.Count, cell = cell.index, strength = settings.dissonanceStrength, radius = settings.dissonanceRadius, dissonance = true });
        }

        // The Age-free field: biome baseline, plus seeds, minus dissonance (tracked apart).
        magic._baseCoherence = new float[n];
        magic._dissonance = new float[n];
        float spacing = (float)HexHierarchy.Spacing(HexHierarchy.Meso);
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            float baseline = t.water ? 0.4f : settings.MacroBiome(t.macroBiome)?.coherence ?? 0.45f;
            float plus = 0f, minus = 0f;
            foreach (var source in magic.Seeds)
            {
                var s = map[source.cell];
                float dx = (t.x - s.x) / spacing, dy = (t.y - s.y) / spacing;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d >= source.radius) continue;
                float f = 1f - d / source.radius;
                if (source.dissonance) minus += source.strength * f * f;
                else plus += source.strength * f * f;
            }
            magic._dissonance[i] = minus;
            magic._baseCoherence[i] = Clamp(baseline + plus - minus, 0f, 1f);
        }

        // Sacred Sites: the highest Coherence on dry, passable ground, far apart and away from the capital.
        var ranked = Enumerable.Range(0, n).Where(i => !map[i].water && settings.Terrain(map[i].terrain)?.passable != false && (capital == null || HexCoord.Distance(map[i].coord, capital.coord) >= 8))
            .OrderByDescending(i => magic._baseCoherence[i]).ThenBy(i => i).ToList();
        // One sacred destination belongs to the opening exploration, before the distant pilgrimage sites.
        // Prefer the most naturally coherent ground in that band; it receives the same protection as every site.
        if (settings.sacredSites > 0 && capital != null && settings.resourceSites != null && settings.resourceSites.Any(s => s != null && s.requiresCoherentRefuge))
        {
            int nearby = ranked.Where(i => !map[i].impassable && HexCoord.Distance(map[i].coord, capital.coord) <= 16).DefaultIfEmpty(-1).First();
            if (nearby >= 0) magic.SacredSites.Add(nearby);
        }
        foreach (int i in ranked)
        {
            if (magic.SacredSites.Count >= settings.sacredSites) break;
            if (magic.SacredSites.Any(s => HexCoord.Distance(map[s].coord, map[i].coord) < 25)) continue;
            magic.SacredSites.Add(i);
        }
        magic._protected = new bool[n];
        foreach (int site in magic.SacredSites)
        {
            map[site].sacred = true;
            foreach (var coord in HexCoord.Spiral(map[site].coord, 2))
            {
                var t = map.Get(coord);
                if (t != null) magic._protected[t.index] = true;
            }
        }

        // The Grand Thread Rings: each turns one way about a centre that orbits a little further every Age.
        var ringRng = new Random(WorldNoise.Stream(seed, "magic:rings"));
        double size = Math.Max(map.maxX - map.minX, map.maxY - map.minY);
        magic._rings = new (double, double, double, double, double, double, double)[RingCount];
        for (int k = 0; k < RingCount; k++)
        {
            double x = map.minX + (0.25 + 0.5 * ringRng.NextDouble()) * (map.maxX - map.minX);
            double y = map.minY + (0.25 + 0.5 * ringRng.NextDouble()) * (map.maxY - map.minY);
            magic._rings[k] = (x, y, size * (0.14 + 0.14 * ringRng.NextDouble()), ringRng.NextDouble() * Math.PI * 2, 0.55 + 0.3 * k, size * 0.08, ringRng.NextDouble() < 0.5 ? -1.0 : 1.0);
        }
        return magic;
    }

    // ===== AGES =====

    /// <summary>
    /// Commit Age <paramref name="age"/>'s magic to the map: leylines, junctions, silver reaches, Coherence and magical
    /// fertility. A repeated call for the Age already committed changes nothing and returns false.
    /// </summary>
    public bool Apply(WorldMap map, int age)
    {
        if (Age == age) return false;
        if (Age >= 0) PreviousLeylines = Leylines;
        Commit(map, age);
        return true;
    }

    /// <summary>
    /// The Resonance Anchors standing now (Arcanoria.md: Outposts "redirect the flow of Leylines through Resonance
    /// Anchors to increase the Coherence of a region"). Takes effect at the next <see cref="Apply"/> or <see cref="Reapply"/>.
    /// </summary>
    public void SetAnchors(IEnumerable<int> cells, float coherence, int radius, int pull)
    {
        Anchors.Clear();
        if (cells != null) Anchors.AddRange(cells.Distinct().OrderBy(c => c));
        _anchorCoherence = coherence;
        _anchorRadius = Math.Max(0, radius);
        _anchorPull = Math.Max(0, pull);
    }

    // Coherence lent or taken by what stands on the land (resource sites: a Lunehymn well steadies, an Emberwhisper
    // spire unsettles), added to each cell at every commit.
    private float[] _shifts;

    /// <summary>
    /// Coherence each cell gains or loses from what stands on it (<see cref="WorldResources"/>). False when nothing
    /// changed; otherwise call <see cref="Refield"/> (or the next <see cref="Apply"/>) for it to take effect.
    /// </summary>
    public bool SetShifts(float[] shifts) => Replace(ref _shifts, shifts);

    // Dissonance drunk or lent by what grows on the land (Eleos Blooms that listen keep it from stagnating in the soil),
    // added to each cell's Dissonance at every commit.
    private float[] _dissonanceShifts;

    /// <summary>Dissonance each cell gains or loses from what grows on it (<see cref="WorldResources"/>); as <see cref="SetShifts"/>.</summary>
    public bool SetDissonanceShifts(float[] shifts) => Replace(ref _dissonanceShifts, shifts);

    /// <summary>A cell's Dissonance before what grows on it (0 on Sacred ground).</summary>
    public float BaseDissonance(int cell) => _dissonance == null || cell < 0 || cell >= _dissonance.Length || _protected[cell] ? 0f : _dissonance[cell];

    private static bool Replace(ref float[] held, float[] shifts)
    {
        bool none = shifts == null || Array.TrueForAll(shifts, s => s == 0f);
        if (none && held == null) return false;
        if (!none && held != null && held.Length == shifts.Length)
        {
            bool same = true;
            for (int i = 0; i < shifts.Length && same; i++) same = held[i] == shifts[i];
            if (same) return false;
        }
        held = none ? null : (float[])shifts.Clone();
        return true;
    }

    /// <summary>Recompute Coherence and magical fertility for the current Age without re-routing the leylines.</summary>
    public void Refield(WorldMap map)
    {
        if (Age < 0) return;
        Fields(map, Age);
        Version++;
    }

    /// <summary>Re-route the current Age after an Anchor changed (the geography, rivers and Sacred Sites stay).</summary>
    public void Reapply(WorldMap map)
    {
        if (Age < 0) return;
        Commit(map, Age);
    }

    private void Commit(WorldMap map, int age)
    {
        int n = map.Count;
        Leylines = Route(map, age);
        for (int i = 0; i < n; i++)
        {
            map[i].leylines = 0L;
            map[i].junction = 0;
        }
        foreach (var line in Leylines)
            foreach (int c in line.cells)
                map[c].leylines |= 1L << (line.family % MaxFamilies);

        Junctions = FindJunctions(map);
        foreach (var junction in Junctions)
            foreach (int c in junction.cells)
                map[c].junction = Math.Max(map[c].junction, junction.families.Count);

        Silver(map);
        Fields(map, age);
        Age = age;
        Version++;
    }

    /// <summary>The leylines Age <paramref name="age"/> would bring, without changing the map (the Age preview).</summary>
    public List<Leyline> Forecast(WorldMap map, int age) => Route(map, age);

    private List<Leyline> Route(WorldMap map, int age)
    {
        var lines = new List<Leyline>();
        var fieldNoise = new WorldNoise(WorldNoise.Stream(_seed, "magic:age" + age));
        foreach (var seed in Seeds.Where(s => !s.dissonance))
        {
            var line = new Leyline { family = seed.id };
            line.cells.Add(seed.cell);
            line.points.Add((map[seed.cell].x, map[seed.cell].y));
            Walk(map, seed.cell, 1, age, fieldNoise, line.cells, line.points);
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Where ring <paramref name="k"/>'s centre stands in Age <paramref name="age"/> (it orbits its home a little each Age).</summary>
    private void RingCentre(int k, int age, out double x, out double y)
    {
        var ring = _rings[k];
        double angle = ring.phase + age * ring.speed;
        x = ring.x + ring.orbit * Math.Cos(angle);
        y = ring.y + ring.orbit * Math.Sin(angle);
    }

    // The rings' direction at a point in Age age: the tangent of the rings whose circle passes near, each turning
    // its own way, with a gentle wobble of its own for each Age. Lines drawn along it trace arcs, not rulers.
    private void Field(double x, double y, int age, WorldNoise noise, out double fx, out double fy)
    {
        fx = fy = 0;
        for (int k = 0; k < _rings.Length; k++)
        {
            var ring = _rings[k];
            RingCentre(k, age, out double cx, out double cy);
            double dx = x - cx, dy = y - cy;
            double d = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
            double near = (d - ring.radius) / (0.45 * ring.radius);
            double w = Math.Exp(-near * near) + 0.12 / (1.0 + d / ring.radius);
            // Perpendicular to the radius, turning with the ring's sense.
            fx += w * ring.sense * -dy / d;
            fy += w * ring.sense * dx / d;
        }
        double angle = Math.Atan2(fy, fx) + 0.7 * noise.Fbm(x / 140.0, y / 140.0, 2);
        fx = Math.Cos(angle);
        fy = Math.Sin(angle);
    }

    // A leyline follows the rings' field as a continuous curve from its seed, steered a little toward Coherence and
    // rivers; the cells it crosses are its footprint. Stops at the world's edge or after its length.
    private void Walk(WorldMap map, int start, int sign, int age, WorldNoise noise, List<int> cells, List<(float x, float y)> points)
    {
        int current = start;
        // Drainage already guarantees a descending outlet. Ring motion chooses between downhill
        // branches, never lifts a river-like leyline over a ridge or loops it uphill.
        for (int step = 0; step < map.Count && !map[current].water; step++)
        {
            var here = map[current];
            Field(here.x, here.y, age, noise, out double fx, out double fy);
            int best = -1;
            double bestScore = double.NegativeInfinity;
            foreach (var next in map.NeighboursOf(here))
            {
                if (next.elevation >= here.elevation) continue;
                double dx = next.x - here.x, dy = next.y - here.y;
                double alignment = (dx * fx + dy * fy) / Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
                double score = 4.0 * (here.elevation - next.elevation)
                    + 1.5 * (_baseCoherence[current] - _baseCoherence[next.index])
                    + 0.65 * alignment + (next.river ? 0.15 : 0) + (next.water ? 1.0 : 0)
                    + AnchorPull(map, here, dx, dy);
                if (score > bestScore) { bestScore = score; best = next.index; }
            }
            if (best < 0) break; // malformed/external map: never invent a disconnected segment
            current = best;
            cells.Add(current);
            points.Add((map[current].x, map[current].y));
        }
    }

    // A near Anchor steers the choice between downhill steps toward itself (it never lifts a line uphill).
    private double AnchorPull(WorldMap map, WorldTile here, double dx, double dy)
    {
        if (Anchors.Count == 0 || _anchorPull <= 0) return 0.0;
        double spacing = HexHierarchy.Spacing(HexHierarchy.Meso), step = Math.Sqrt(dx * dx + dy * dy);
        double pull = 0.0;
        foreach (int a in Anchors)
        {
            if (a < 0 || a >= map.Count) continue;
            var anchor = map[a];
            double ax = anchor.x - here.x, ay = anchor.y - here.y, d = Math.Sqrt(ax * ax + ay * ay);
            double cells = d / spacing;
            if (cells > _anchorPull || d < 1e-6) continue;
            pull += 1.4 * (1.0 - cells / (_anchorPull + 1.0)) * (ax * dx + ay * dy) / (d * Math.Max(1e-9, step));
        }
        return pull;
    }

    /// <summary>
    /// Junctions: connected footprints of cells where at least two distinct families run. The families present across
    /// one footprint are counted (a bend, a fork or a doubled segment of one family adds nothing).
    /// </summary>
    public static List<LeylineJunction> FindJunctions(WorldMap map)
    {
        var junctions = new List<LeylineJunction>();
        int n = map.Count;
        var seen = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (seen[i] || map[i].LeylineCount < 2) continue;
            var junction = new LeylineJunction();
            long families = 0L;
            var open = new Stack<int>();
            open.Push(i);
            seen[i] = true;
            int bestCount = -1;
            while (open.Count > 0)
            {
                int c = open.Pop();
                junction.cells.Add(c);
                families |= map[c].leylines;
                int count = map[c].LeylineCount;
                if (count > bestCount || (count == bestCount && c < junction.center))
                {
                    bestCount = count;
                    junction.center = c;
                }
                for (int d = 0; d < 6; d++)
                {
                    int nb = map.Neighbour(c, d);
                    if (nb >= 0 && !seen[nb] && map[nb].LeylineCount >= 2)
                    {
                        seen[nb] = true;
                        open.Push(nb);
                    }
                }
            }
            for (int f = 0; f < MaxFamilies; f++) if ((families & (1L << f)) != 0) junction.families.Add(f);
            junction.cells.Sort();
            junctions.Add(junction);
        }
        return junctions;
    }

    // Lunehymn: infused where a leyline crosses a river or lake, carried downstream, diluted by tributaries.
    private void Silver(WorldMap map)
    {
        int n = map.Count;
        var previous = new float[n];
        for (int i = 0; i < n; i++) previous[i] = map[i].lunehymn;
        var carries = Enumerable.Range(0, n).Where(i => map[i].river || map[i].lake).OrderBy(i => map[i].flow).ThenBy(i => i).ToList();
        var incoming = new float[n];
        var conc = new float[n];
        foreach (int c in carries)
        {
            var t = map[c];
            float infusion = t.leylines != 0 ? 1f : 0f;
            float carried = t.flow > 0f ? incoming[c] / t.flow : 0f;
            float value = Math.Max(infusion, carried);
            value = Math.Max(value, previous[c] * _settings.silverResidue);
            conc[c] = Clamp(value, 0f, 1f);
            if (t.downstream >= 0) incoming[t.downstream] += conc[c] * t.flow * _settings.silverRetention;
        }
        for (int i = 0; i < n; i++)
        {
            map[i].lunehymn = conc[i];
            map[i].silver = conc[i] >= _settings.silverThreshold;
        }
    }

    private void Fields(WorldMap map, int age)
    {
        // A multi-source distance field spreads each corridor across nearby slots and seams.
        var distance = Enumerable.Repeat(-1, map.Count).ToArray();
        var queue = new Queue<int>();
        for (int i = 0; i < map.Count; i++) if (map[i].leylines != 0) { distance[i] = 0; queue.Enqueue(i); }
        int radius = Math.Max(1, _settings.leylineDriftRadius);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            if (distance[c] >= radius) continue;
            foreach (var next in map.NeighboursOf(map[c]))
                if (distance[next.index] < 0) { distance[next.index] = distance[c] + 1; queue.Enqueue(next.index); }
        }
        // Resonance Anchors: constructed Coherence that fades over their radius.
        var anchored = new float[map.Count];
        foreach (int a in Anchors)
        {
            if (a < 0 || a >= map.Count) continue;
            foreach (var coord in HexCoord.Spiral(map[a].coord, _anchorRadius))
            {
                var t = map.Get(coord);
                if (t == null) continue;
                float f = _anchorCoherence * (1f - HexCoord.Distance(coord, map[a].coord) / (float)(_anchorRadius + 1));
                anchored[t.index] = Math.Max(anchored[t.index], f);
            }
        }
        float access = _settings.MagicAccess(age);
        var vibration = _settings.vibration;
        for (int i = 0; i < map.Count; i++)
        {
            var t = map[i];

            float influence = distance[i] < 0 ? 0f : 1f - distance[i] / (float)(radius + 1);
            influence *= influence;
            t.leylineInfluence = influence;
            // Vibrational Fallout, where the Dissonance broke the Loom: nothing built or grown heals it (WorldVibration).
            float fallout = WorldVibration.Fallout(vibration, _dissonance[i], _protected[i]);
            float heal = vibration != null && vibration.unhealable ? 1f - fallout : 1f;
            float support = (_settings.leylineDriftStrength * influence) + (t.junction >= 3 ? 0.2f : t.junction == 2 ? 0.1f : 0f) + (t.silver ? 0.05f : 0f) + anchored[i] * heal;
            float density = WorldVibration.Density(vibration, t, _settings.seaLevel, fallout);
            float coherence = _baseCoherence[i] + support + (_shifts != null && i < _shifts.Length ? _shifts[i] : 0f) + WorldVibration.CoherenceShift(vibration, density);
            if (_protected[i])
            {
                coherence = Math.Max(coherence + _dissonance[i], 0.9f); // Sacred ground shrugs off common dissonance
                t.dissonance = 0f;
            }
            else
            {
                float drunk = _dissonanceShifts != null && i < _dissonanceShifts.Length ? _dissonanceShifts[i] : 0f;
                t.dissonance = Math.Max(0f, _dissonance[i] + (drunk < 0f ? drunk * heal : drunk));
            }
            t.fallout = fallout;
            t.vibrationalDensity = density;
            t.falloutTravel = WorldVibration.TravelFactor(vibration, fallout);
            t.coherence = Clamp(coherence, 0f, 1f);
            float flow = Math.Min(1f, (0.5f * influence) + 0.4f * t.lunehymn + (t.junction >= 3 ? 0.4f : t.junction == 2 ? 0.25f : 0f) + (t.sacred ? 0.5f : 0f));
            t.magicFertility = t.water && !t.lake ? 0f : Clamp(t.coherence * (0.25f + 0.75f * flow) * access, 0f, 1f);
        }
        WorldVibration.Cascades(map, vibration);
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
