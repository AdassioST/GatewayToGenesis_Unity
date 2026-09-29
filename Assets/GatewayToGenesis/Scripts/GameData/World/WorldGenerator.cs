using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Builds the world from a seed, <see cref="WorldGenSettings"/>, the composition stencil and the handmade tiles, with
/// no scene state (tested in <c>WorldGenerationTests</c>): the same seed, version and catalog always give the same
/// world. Docs/Planning/WORLD_GENERATION.md is the design; the stages follow roadmap WG01-WG10:
///
/// 1. Domain: meso cells filling the stencil plus an ocean apron (<see cref="HexHierarchy"/> positions).
/// 2. Composition: each cell reads the stencil through a low-frequency warp, so the stencil's straight construction
///    lines never show; quadrants become slots, I intersections, W ocean (WG03).
/// 3. Slots: <see cref="SlotSolver"/> shuffles each quadrant's biomes into its slots and turns them (WG01, WG04).
/// 4. Land and sea: slots are always land; the coastline wanders through the intersections and W; the apron is a reserved ocean
///    ring around the whole mainland (WG03).
/// 5. Fields: height, moisture and temperature are blended across seams from both neighbouring slots over shared
///    world-coordinate noise, so a seam has one height and no cliff wall; a mountainous quadrant raises a range along
///    its own intersections (WG05).
/// 6. Handmade tiles are stamped into slot interiors at seeded places and rotations (the hybrid).
/// 7. Topology: the mainland stays connected (bridges are carved and reported) and the ocean ring keeps its width.
/// 8. Water: priority-flood drainage to the sea, lakes in deep hollows, flow accumulation and rivers (WG06).
/// 9. Ground: each cell takes its biome's terrain for its climate; intersections and slot buffer bands mix the neighbouring
///    macro biomes with the intersections' own ground, then one smoothing pass softens the seams.
/// 10. Land fertility, the capital (Q1, on freshwater), magic (<see cref="WorldMagic"/>, WG07-WG09) and the Age 0
///     features (<see cref="PlaceFeatures"/>, WG10).
/// 11. Crags: steep escarpments break into micro hexes no one climbs, for the micro travel grid (<see cref="MicroGrid"/>);
///     every two walkable neighbouring cells keep a crossing.
/// 12. Sectors: each Macro Biome is divided into its nine compass Sectors (<see cref="WorldSectors"/>).
/// </summary>
public static class WorldGenerator
{
    public const int Version = 9;

    private static readonly string[] DirectionNames = { "east", "north-east", "north-west", "west", "south-west", "south-east" };

    public static string DirectionName(int orientation) => DirectionNames[((orientation % 6) + 6) % 6];

    public static WorldMap Generate(int seed, WorldGenSettings settings, WorldStencil stencil, IList<TileTemplate> tiles)
    {
        if (stencil == null) throw new ArgumentNullException(nameof(stencil));
        var watch = Stopwatch.StartNew();
        var build = new Build(seed, settings ?? new WorldGenSettings(), stencil, tiles ?? new List<TileTemplate>());
        build.Run();
        var map = build.map;
        WorldSectors.Assign(map);
        map.Magic = WorldMagic.Build(map, build.settings, seed);
        map.Magic.Apply(map, 0);
        WorldCivilization.EnsureCapital(map, build.settings);
        PlaceFeatures(map, build.settings, 0);
        WorldSites.PlaceAge(map, build.settings, 0);
        map.Report.milliseconds = watch.Elapsed.TotalMilliseconds;
        return map;
    }

    /// <summary>A short, stable fingerprint of the catalog, recorded with each world (a seed alone is not enough after content changes).</summary>
    public static string CatalogHash(WorldGenSettings settings, IList<TileTemplate> tiles)
    {
        unchecked
        {
            uint hash = 2166136261u;
            void Add(string s)
            {
                foreach (char c in s ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                hash ^= 0xff;
                hash *= 16777619u;
            }
            Add(settings.stencil);
            Add($"{settings.naturalBasins}|{settings.capitalAuthorityRadius}|{settings.leylineDriftRadius}|{settings.leylineDriftStrength}|{settings.seedStrength}|{settings.seedRadius}|{settings.dissonanceSeeds}|{settings.sacredSites}|{settings.silverRetention}|{settings.silverThreshold}");
            foreach (var feature in settings.features.Where(f => f != null))
                Add($"{feature.id}|{feature.count}|{feature.minAge}|{feature.authorityId}|{feature.authorityRadius}|{feature.minimumCoherence}|{feature.requiresCoast}|{feature.requiresFreshwater}|{string.Join(",", feature.terrains)}|{string.Join(",", feature.macroBiomes)}");
            foreach (var tile in tiles ?? new List<TileTemplate>())
                foreach (var cell in tile.cells.OrderBy(c => c.Key.q).ThenBy(c => c.Key.r))
                {
                    Add($"{cell.Key.q},{cell.Key.r}:{cell.Value}");
                    if (tile.heights.TryGetValue(cell.Key, out var height)) Add(height.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }
            Add($"{settings.cellsPerStencilCell}|{settings.apron}|{settings.warp}|{settings.seaLevel}|{settings.coastBand}|{settings.seamReach}|{settings.riverThreshold}|{settings.lakeDepth}|{settings.minLakeCells}|{settings.maxLakeCells}");
            foreach (var t in settings.terrains.Where(t => t != null)) Add($"{t.id}|{t.passable}|{t.water}|{t.fertility}|{t.moveCost}");
            foreach (var g in settings.grandfields.Where(g => g != null))
                Add($"{g.id}|{g.resource}|{g.minAge}|{g.count}|{g.size}|{g.minDistance}|{g.minimumCoherence}|{g.minimumMagicalFertility}|{g.requiresFreshwater}|{string.Join(",", g.terrains)}|{string.Join(",", g.macroBiomes)}");
            foreach (var r in (settings.resourceSites ?? new List<ResourceSiteSpec>()).Where(r => r != null))
                Add($"{r.id}|{r.minAge}|{r.count}|{r.size}|{r.spacing}|{r.minDistance}|{r.minElevation}|{r.maxElevation}|{r.minMoisture}|{r.minimumFertility}|{r.minimumCoherence}|{r.maximumCoherence}|{r.minimumDissonance}|{r.requiresFreshwater}|{r.requiresSilverOrLeyline}|{r.maxTemperature}|{r.coastal}|{r.requiresCoherentRefuge}|{r.minimumDesirability}|{r.requiresSacred}|{r.nearReach}|{string.Join(",", r.nearTerrains)}|{string.Join(",", r.terrains)}|{string.Join(",", r.macroBiomes)}|{r.coherenceAura}|{r.fertilityAura}|{r.auraRadius}");
            foreach (var c in (settings.covers ?? new List<CoverSpec>()).Where(c => c != null))
                Add($"{c.id}|{c.count}|{c.minSize}|{c.maxSize}|{c.spacing}|{c.minDistance}|{c.minMoisture}|{c.blocksSight}|{string.Join(",", c.terrains)}");
            foreach (var r in (settings.resourceSites ?? new List<ResourceSiteSpec>()).Where(r => r != null && r.covers != null && r.covers.Count > 0))
                Add($"{r.id}|covers:{string.Join(",", r.covers)}");
            foreach (var e in settings.enclaves.Where(e => e != null))
                Add($"{e.id}|{e.family}|{e.minAge}|{e.count}|{e.site}|{e.authorityRadius}|{e.woundMin}|{e.woundMax}|{e.minDistance}|{e.spacing}|{string.Join(",", e.bindings)}");
            foreach (var th in settings.threats.Where(th => th != null))
                Add($"{th.id}|{th.minAge}|{th.onDissonanceSeeds}|{th.count}|{th.strength}|{th.radius}|{th.minDistance}");
            if (settings.nexus != null) Add($"{settings.nexus.count}|{settings.nexus.spacing}|{settings.nexus.minDistance}|{settings.nexus.harbors}|{settings.nexus.estuaries}|{settings.nexus.passes}|{settings.nexus.confluences}");
            foreach (var b in settings.macroBiomes.Where(b => b != null))
                Add(b.terraces.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var b in settings.macroBiomes.Where(b => b != null))
                Add($"{b.id}|{b.elevation}|{b.relief}|{b.tilt}|{b.moisture}|{string.Join(",", b.orientations)}|{string.Join(",", b.requires)}|{string.Join(",", b.terrains.Where(r => r != null).Select(r => r.terrain + r.weight))}");
            foreach (var s in settings.quadrants.Where(s => s != null)) Add($"{s.id}|{string.Join(",", s.macroBiomes)}|{s.allowRepeats}|{s.backbone}|{s.island}");
            foreach (var r in settings.intersectionTerrains.Where(r => r != null)) Add(r.terrain + r.weight);
            foreach (var t in tiles ?? new List<TileTemplate>()) Add($"{t.id}|{t.macroBiome}|{t.cells.Count}|{string.Join(",", t.rotations)}");
            return hash.ToString("x8");
        }
    }

    // ===== THE BUILD =====

    private class Build
    {
        public readonly int seed;
        public readonly WorldGenSettings settings;
        public readonly WorldStencil stencil;
        public readonly IList<TileTemplate> tiles;
        public readonly WorldMap map;

        private float _side, _halfSW, _halfSH;
        private int _n;
        private bool[] _reserved;
        private int[] _depth;           // slot cells: steps to the nearest non-slot cell
        private float[][] _slotDist;    // per slot: signed steps to it (its interior negative)
        private int[] _s1;              // nearest slot
        private float[] _d1;            // signed steps to it
        private int[][] _mixSlot;       // slots lending their recipe to a cell...
        private float[][] _mixWeight;   // ...and how much (summing to 1)
        private float[] _blend;         // share of a cell's recipe not from its nearest slot
        private List<(float height, int[] slots)> _backboneSlots;
        private int[] _toSea, _toLand;  // steps to the nearest water / land
        private SlotSolution _solution;
        private readonly WorldNoise _warp, _coast, _terrain, _moisture, _patch;
        private readonly int _pickSeed;

        public Build(int seed, WorldGenSettings settings, WorldStencil stencil, IList<TileTemplate> tiles)
        {
            this.seed = seed;
            this.settings = settings;
            this.stencil = stencil;
            this.tiles = tiles;
            map = new WorldMap(seed) { Stencil = stencil };
            _warp = new WorldNoise(WorldNoise.Stream(seed, "warp"));
            _coast = new WorldNoise(WorldNoise.Stream(seed, "coast"));
            _terrain = new WorldNoise(WorldNoise.Stream(seed, "terrain"));
            _moisture = new WorldNoise(WorldNoise.Stream(seed, "moisture"));
            _pickSeed = WorldNoise.Stream(seed, "ground");
            _patch = new WorldNoise(WorldNoise.Stream(seed, "patches"));
        }

        private WorldTile T(int i) => map[i];

        private MacroBiomeSpec MacroBiomeOf(int slot) => slot >= 0 ? _solution.For(slot)?.macroBiome : null;

        public void Run()
        {
            map.Report.seed = seed;
            map.Report.version = Version;
            map.Report.catalogHash = CatalogHash(settings, tiles);

            BuildDomain();
            ReadStencil();
            _solution = SlotSolver.Solve(stencil, settings, seed);
            map.Placements.AddRange(_solution.placements);
            map.Report.errors.AddRange(_solution.errors);
            map.Report.repairs.AddRange(_solution.repairs);
            MeasureSlots();
            ShapeLand();
            RaiseFields();
            StampTiles();
            SculptBasins();
            CheckTopology();
            Drain();
            DescribeRelief();
            Lay();
            Fertilize();
            PlaceCapital();
            PlaceCrags();
            foreach (var placement in _solution.placements)
            {
                string tilesHere = string.Join(", ", map.Tiles.Where(t => t.slot == placement.slot.index && t.handmadeTile != null).Select(t => t.handmadeTile).Distinct());
                map.Report.assignments.Add($"{placement}{(tilesHere.Length > 0 ? $" [{tilesHere}]" : string.Empty)}");
            }
        }

        // ===== 1. DOMAIN =====

        private void BuildDomain()
        {
            int per = Math.Max(2, settings.cellsPerStencilCell);
            _side = (float)(per * Math.Sqrt(HexHierarchy.Area(HexHierarchy.Meso)));
            _halfSW = stencil.Width * _side / 2f;
            _halfSH = stencil.Height * _side / 2f;
            float apron = Math.Max(0f, settings.apron) * _side;
            map.minX = -_halfSW - apron;
            map.maxX = _halfSW + apron;
            map.minY = -_halfSH - apron;
            map.maxY = _halfSH + apron;

            int bound = (int)Math.Ceiling(2.0 * Math.Max(map.maxX, map.maxY) / HexHierarchy.Spacing(HexHierarchy.Meso)) + 4;
            for (int r = -bound; r <= bound; r++)
            {
                for (int q = -bound; q <= bound; q++)
                {
                    var coord = new HexCoord(q, r);
                    HexHierarchy.ToWorld(coord, HexHierarchy.Meso, out float x, out float y);
                    if (x < map.minX || x > map.maxX || y < map.minY || y > map.maxY) continue;
                    map.Add(new WorldTile { coord = coord, x = x, y = y });
                }
            }
            _n = map.Count;
        }

        // ===== 2. COMPOSITION =====

        private void ReadStencil()
        {
            _reserved = new bool[_n];
            double scale = 1.0 / (_side * 1.1);
            float amplitude = Math.Max(0f, settings.warp) * _side * 1.8f;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                float col0 = (t.x + _halfSW) / _side, row0 = (_halfSH - t.y) / _side;
                _reserved[i] = col0 < 0f || row0 < 0f || col0 >= stencil.Width || row0 >= stencil.Height;
                if (_reserved[i])
                {
                    t.composition = WorldComposition.Ocean;
                    continue;
                }
                double wx = t.x + amplitude * _warp.Fbm(t.x * scale, t.y * scale, 4);
                double wy = t.y + amplitude * _warp.Fbm(t.x * scale + 31.7, t.y * scale - 12.9, 4);
                int col = (int)Math.Floor((wx + _halfSW) / _side), row = (int)Math.Floor((_halfSH - wy) / _side);
                string token = stencil.At(col, row);
                if (token == WorldStencil.Ocean) t.composition = WorldComposition.Ocean;
                else if (token == WorldStencil.Intersection) t.composition = WorldComposition.Intersection;
                else
                {
                    t.composition = WorldComposition.MacroBiome;
                    t.slot = stencil.SlotAt(col, row);
                    t.quadrant = token;
                }
            }
        }

        // Breadth-first steps from every source cell, through cells passing the filter.
        private int[] Steps(Func<int, bool> isSource, Func<int, bool> canEnter = null)
        {
            var dist = new int[_n];
            var open = new Queue<int>();
            for (int i = 0; i < _n; i++)
            {
                if (isSource(i))
                {
                    dist[i] = 0;
                    open.Enqueue(i);
                }
                else dist[i] = int.MaxValue;
            }
            while (open.Count > 0)
            {
                int c = open.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int nb = map.Neighbour(c, d);
                    if (nb < 0 || dist[nb] != int.MaxValue || (canEnter != null && !canEnter(nb))) continue;
                    dist[nb] = dist[c] + 1;
                    open.Enqueue(nb);
                }
            }
            return dist;
        }

        // ===== 3. SLOT DISTANCES =====

        private void MeasureSlots()
        {
            _depth = Steps(i => T(i).composition != WorldComposition.MacroBiome);
            int slots = stencil.Slots.Count;
            _slotDist = new float[slots][];
            _s1 = new int[_n];
            _d1 = new float[_n];
            for (int i = 0; i < _n; i++)
            {
                _s1[i] = -1;
                _d1[i] = float.MaxValue;
            }
            foreach (var slot in stencil.Slots)
            {
                int index = slot.index;
                var steps = Steps(i => T(i).slot == index);
                var dist = new float[_n];
                for (int i = 0; i < _n; i++)
                {
                    dist[i] = steps[i] == int.MaxValue ? float.MaxValue : T(i).slot == index ? 1 - _depth[i] : steps[i];
                    if (dist[i] < _d1[i])
                    {
                        _d1[i] = dist[i];
                        _s1[i] = index;
                    }
                }
                _slotDist[index] = dist;
            }

            // Every slot within reach of the nearest one lends its recipe, weighted by how much nearer it is: the blend
            // is continuous everywhere, even where the second-nearest slot changes.
            float reach = Math.Max(1, settings.seamReach);
            _mixSlot = new int[_n][];
            _mixWeight = new float[_n][];
            _blend = new float[_n];
            var slotsHere = new List<int>();
            var weightsHere = new List<float>();
            for (int i = 0; i < _n; i++)
            {
                slotsHere.Clear();
                weightsHere.Clear();
                float total = 0f;
                for (int s = 0; s < slots; s++)
                {
                    float w = reach - (_slotDist[s][i] - _d1[i]);
                    if (w <= 0f) continue;
                    slotsHere.Add(s);
                    weightsHere.Add(w);
                    total += w;
                }
                _mixSlot[i] = slotsHere.ToArray();
                _mixWeight[i] = weightsHere.Select(w => w / total).ToArray();
                _blend[i] = _mixWeight[i].Length == 0 ? 0f : 1f - _mixWeight[i].Max();
                var t = T(i);
                t.seam = t.composition != WorldComposition.MacroBiome || _blend[i] > 0.01f;
                if (t.composition != WorldComposition.MacroBiome && _s1[i] >= 0) t.quadrant = stencil.Slots[_s1[i]].quadrant;
                t.habitatSlot = _s1[i];
            }

            _backboneSlots = settings.quadrants.Where(s => s != null && s.backbone > 0f)
                .Select(s => (s.backbone, stencil.SlotsOf(s.id).Select(slot => slot.index).ToArray()))
                .Where(p => p.Item2.Length >= 2).ToList();
        }

        /// <summary>
        /// A mountainous quadrant's range: highest along the intersection midway between two of its own slots, fading into the
        /// slots and away from the quadrant (continuous, since it reads only that quadrant's slot distances).
        /// </summary>
        private float Backbone(int i)
        {
            float total = 0f, per = Math.Max(2, settings.cellsPerStencilCell);
            foreach (var (height, slots) in _backboneSlots)
            {
                float a = float.MaxValue, b = float.MaxValue;
                foreach (int s in slots)
                {
                    float d = _slotDist[s][i];
                    if (d < a)
                    {
                        b = a;
                        a = d;
                    }
                    else if (d < b) b = d;
                }
                if (b == float.MaxValue) continue;
                float middle = Clamp(1f - (b - a) / (b + Math.Max(a, 0f) + 1f), 0f, 1f);
                float fade = 1f - (float)WorldNoise.Smooth(per * 0.6, per * 1.5, a);
                total += height * middle * middle * fade;
            }
            return total;
        }

        // ===== 4. LAND AND SEA =====

        private void ShapeLand()
        {
            var oceanish = new bool[_n];
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                oceanish[i] = t.composition == WorldComposition.Ocean;
                // An island quadrant's intersections become strait and sea.
                if (t.composition == WorldComposition.Intersection && _s1[i] >= 0 && _d1[i] >= 2 && settings.Quadrant(stencil.Slots[_s1[i]].quadrant)?.island == true) oceanish[i] = true;
            }
            var toOcean = Steps(i => oceanish[i]);
            var toLand = Steps(i => !oceanish[i]);
            double scale = 1.0 / (_side * 0.8);
            float amplitude = Math.Max(0f, settings.coastBand) * Math.Max(2, settings.cellsPerStencilCell) * 1.4f;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (_reserved[i])
                {
                    t.water = true;
                    continue;
                }
                if (t.composition == WorldComposition.MacroBiome)
                {
                    t.water = false;
                    continue;
                }
                float signed = oceanish[i] ? -toLand[i] : toOcean[i];
                double value = signed - 0.5 + amplitude * _coast.Fbm(t.x * scale, t.y * scale, 4);
                bool nearSlot = _s1[i] >= 0 && _d1[i] <= 2;
                t.water = value <= 0 && !nearSlot;
                // The coast wandered out to sea: that ground is an intersection, not ocean.
                if (!t.water && t.composition == WorldComposition.Ocean) t.composition = WorldComposition.Intersection;
            }
            MeasureCoast();
        }

        private void MeasureCoast()
        {
            _toSea = Steps(i => T(i).water && !T(i).lake);
            _toLand = Steps(i => !T(i).water);
        }

        // ===== 5. FIELDS =====

        private void SlotShape(int slot, float x, float y, out float height, out float relief, out float moisture)
        {
            var biome = MacroBiomeOf(slot);
            var placement = slot >= 0 ? _solution.For(slot) : null;
            if (biome == null)
            {
                height = 0.45f;
                relief = 0.1f;
                moisture = 0.5f;
                return;
            }
            var s = stencil.Slots[slot];
            float cx = (s.centerCol + 0.5f) * _side - _halfSW, cy = _halfSH - (s.centerRow + 0.5f) * _side;
            float radius = (float)Math.Sqrt(s.cells.Count) * _side * 0.5f;
            double uphill = placement.orientation * Math.PI / 3.0;
            float along = (float)(((x - cx) * Math.Cos(uphill) + (y - cy) * Math.Sin(uphill)) / Math.Max(1f, radius));
            height = biome.elevation + biome.tilt * 0.5f * Clamp(along, -1.2f, 1.2f);
            var quadrant = settings.Quadrant(s.quadrant);
            if (quadrant != null) height += quadrant.backbone * 0.3f;
            relief = biome.relief;
            moisture = biome.moisture;
        }

        private void RaiseFields()
        {
            double hScale = 1.0 / (_side * 0.45), rScale = 1.0 / (_side * 0.7), mScale = 1.0 / (_side * 0.6);
            float reach = Math.Max(1, settings.seamReach);
            float sea = settings.seaLevel;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (t.water)
                {
                    t.elevation = Clamp(sea - 0.03f - 0.25f * (float)WorldNoise.Smooth(0, 10, _toLand[i]), 0f, sea - 0.01f);
                    t.moisture = 1f;
                    t.temperature = Clamp(0.55f - 0.3f * t.y / _halfSH, 0f, 1f);
                    continue;
                }
                float h = 0f, rel = 0f, m = 0f;
                if (_mixSlot[i].Length == 0) SlotShape(-1, t.x, t.y, out h, out rel, out m);
                for (int k = 0; k < _mixSlot[i].Length; k++)
                {
                    SlotShape(_mixSlot[i][k], t.x, t.y, out float hk, out float rk, out float mk);
                    float w = _mixWeight[i][k];
                    h += w * hk;
                    rel += w * rk;
                    m += w * mk;
                }
                // Away from every slot (deep in an intersection) the ground drifts to a neutral level with its own relief.
                float own = Clamp((_d1[i] - 1f) / reach, 0f, 1f) * 0.5f;
                h = Lerp(h, 0.42f, own);
                rel = Lerp(rel, Math.Max(rel, 0.1f), Clamp(_d1[i], 0f, 1f));
                m = Lerp(m, 0.5f, own);
                // A mountainous quadrant's range runs along the intersections between its own slots.
                float range = Backbone(i);
                h += range;
                rel += range * 0.25f;
                double n = _terrain.Fbm(t.x * hScale, t.y * hScale, 5);
                double ridge = _terrain.Ridge(t.x * rScale + 101.3, t.y * rScale - 77.1, 4);
                h += rel * (float)(n * 1.2 + (ridge - 0.5) * 0.8);
                // Fine grain everywhere, so water finds winding ways even down gentle, even slopes.
                h += 0.02f * (float)_terrain.Fbm(t.x * hScale * 3.1 + 7.7, t.y * hScale * 3.1 - 3.3, 2);
                // The continent rises gently inland, so its water runs out to the sea rather than pooling in the middle.
                h += 0.1f * (float)WorldNoise.Smooth(0, 60, _toSea[i]);
                float terraces = 0f;
                for (int k = 0; k < _mixSlot[i].Length; k++)
                    terraces += (MacroBiomeOf(_mixSlot[i][k])?.terraces ?? 0f) * _mixWeight[i][k];
                if (terraces > 0f)
                {
                    // Long quiet treads separated by short steep risers, cut by warped valleys.
                    double warp = _warp.Fbm(t.x * hScale, t.y * hScale, 3);
                    double valley = Math.Abs(_terrain.Noise(t.x * hScale * 0.7 + warp * 0.6, t.y * hScale * 0.7 + 37));
                    float incision = 0.075f * (1f - (float)WorldNoise.Smooth(0.025, 0.16, valley));
                    float level = h / 0.055f;
                    float step = (float)Math.Floor(level);
                    float shelf = (step + (float)WorldNoise.Smooth(0.72, 0.98, level - step)) * 0.055f;
                    h = Lerp(h, shelf - incision, Clamp(terraces, 0f, 1f));
                }
                // Coasts are low-lying.
                h = Lerp(sea + 0.02f, h, (float)WorldNoise.Smooth(0, 4, _toSea[i]));
                t.elevation = Clamp(h, sea + 0.01f, 1f);

                m += 0.18f * (float)_moisture.Fbm(t.x * mScale, t.y * mScale, 4);
                m += 0.12f * (1f - (float)WorldNoise.Smooth(0, 8, _toSea[i]));
                m -= 0.25f * Math.Max(0f, t.elevation - 0.65f);
                t.moisture = Clamp(m, 0f, 1f);
                float latitude = t.y / _halfSH;
                t.temperature = Clamp(0.55f - 0.3f * latitude - 0.6f * Math.Max(0f, t.elevation - sea) + 0.05f * (float)_moisture.Noise(t.x * mScale * 0.5, t.y * mScale * 0.5), 0f, 1f);
            }
        }

        // ===== 6. HANDMADE TILES =====

        private void StampTiles()
        {
            var stampOf = new int[_n];
            for (int i = 0; i < _n; i++) stampOf[i] = -1;
            int stampCount = 0;
            foreach (var placement in _solution.placements)
            {
                var biome = placement.macroBiome;
                if (biome == null) continue;
                var options = tiles.Where(t => t != null && string.Equals(t.macroBiome, biome.id, StringComparison.OrdinalIgnoreCase) && t.weight > 0f).ToList();
                if (options.Count == 0) continue;
                int slot = placement.slot.index;
                var interior = Enumerable.Range(0, _n).Where(i => T(i).slot == slot && _depth[i] >= 3).ToList();
                if (interior.Count == 0) continue;
                var rng = new Random(WorldNoise.Stream(seed, "tiles:" + placement.slot.id));
                int target = (int)(biome.tileCoverage * interior.Count), covered = 0;
                for (int attempt = 0; attempt < 80 && covered < target; attempt++)
                {
                    var center = T(interior[rng.Next(interior.Count)]);
                    var tile = PickWeighted(options, rng);
                    int rotation = tile.rotations.Count > 0 ? tile.rotations[rng.Next(tile.rotations.Count)] : 0;
                    var cells = new List<(WorldTile cell, string terrain, HexCoord local)>();
                    bool fits = true;
                    foreach (var pair in tile.Rotated(rotation))
                    {
                        var cell = map.Get(center.coord + pair.Key);
                        if (cell == null || cell.slot != slot || _depth[cell.index] < 2 || stampOf[cell.index] >= 0 || map.NeighboursOf(cell).Any(nb => stampOf[nb.index] >= 0))
                        {
                            fits = false;
                            break;
                        }
                        cells.Add((cell, pair.Value, pair.Key));
                    }
                    if (!fits) continue;
                    foreach (var (cell, terrain, local) in cells)
                    {
                        stampOf[cell.index] = stampCount;
                        cell.handmadeTile = tile.id;
                        cell.terrain = terrain;
                        cell.seam = false;
                        float h = tile.HeightAt(local, rotation, out bool has);
                        if (has) cell.elevation = Lerp(cell.elevation, h, 0.85f);
                        if (settings.Terrain(terrain)?.water == true)
                        {
                            cell.water = true;
                            cell.lake = true;
                            cell.elevation = Math.Min(cell.elevation, settings.seaLevel + 0.03f);
                        }
                    }
                    covered += cells.Count;
                    stampCount++;
                    map.Report.tiles.Add($"{placement.slot.id}: {tile.id} turned {rotation * 60} degrees at {center.coord}");
                }
            }
            foreach (var tile in tiles.Where(t => t != null && settings.MacroBiome(t.macroBiome) == null)) map.Report.errors.Add($"Handmade tile '{tile.id}' names unknown Macro Biome '{tile.macroBiome}'.");
            foreach (var tile in tiles.Where(t => t != null))
            {
                foreach (var terrain in tile.cells.Values.Distinct())
                    if (settings.Terrain(terrain) == null) map.Report.errors.Add($"Handmade tile '{tile.id}' draws unknown terrain '{terrain}'.");
            }
        }

        private static TileTemplate PickWeighted(List<TileTemplate> options, Random rng)
        {
            double total = options.Sum(o => (double)o.weight), roll = rng.NextDouble() * total;
            foreach (var option in options)
            {
                roll -= option.weight;
                if (roll < 0) return option;
            }
            return options[options.Count - 1];
        }

        // ===== 7. TOPOLOGY =====

        // The mainland's anchor: in the land mass holding the most Q1 ground (then the most cells), the Q1 cell
        // nearest the centre. A pocket that lakes or coast cut off never anchors it.
        private int CoreCell()
        {
            var s1 = stencil.Slots.FirstOrDefault(s => string.Equals(s.quadrant, "Q1", StringComparison.OrdinalIgnoreCase));
            var label = new int[_n];
            for (int i = 0; i < _n; i++) label[i] = -1;
            var starts = new List<int>();
            var sizes = new List<int>();
            var s1Sizes = new List<int>();
            var open = new Queue<int>();
            for (int i = 0; i < _n; i++)
            {
                if (T(i).water || label[i] >= 0) continue;
                int id = starts.Count, size = 0, s1Size = 0;
                starts.Add(i);
                label[i] = id;
                open.Enqueue(i);
                while (open.Count > 0)
                {
                    int c = open.Dequeue();
                    size++;
                    if (s1 != null && T(c).slot == s1.index) s1Size++;
                    for (int d = 0; d < 6; d++)
                    {
                        int nb = map.Neighbour(c, d);
                        if (nb < 0 || label[nb] >= 0 || T(nb).water) continue;
                        label[nb] = id;
                        open.Enqueue(nb);
                    }
                }
                sizes.Add(size);
                s1Sizes.Add(s1Size);
            }
            if (starts.Count == 0) return -1;
            int main = Enumerable.Range(0, starts.Count).OrderByDescending(k => s1Sizes[k]).ThenByDescending(k => sizes[k]).First();
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (label[i] != main || (s1 != null && s1Sizes[main] > 0 && t.slot != s1.index)) continue;
                float d = t.x * t.x + t.y * t.y;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }


        private bool[] Mainland(int core)
        {
            var reached = new bool[_n];
            if (core < 0) return reached;
            var open = new Queue<int>();
            open.Enqueue(core);
            reached[core] = true;
            while (open.Count > 0)
            {
                int c = open.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int nb = map.Neighbour(c, d);
                    if (nb < 0 || reached[nb] || T(nb).water) continue;
                    reached[nb] = true;
                    open.Enqueue(nb);
                }
            }
            return reached;
        }

        private void CheckTopology()
        {
            int core = CoreCell();
            if (core < 0)
            {
                map.Report.errors.Add("The stencil has no land to start on.");
                return;
            }
            var mainland = Mainland(core);
            foreach (var slot in stencil.Slots)
            {
                if (settings.Quadrant(slot.quadrant)?.island == true) continue;
                int index = slot.index;
                if (Enumerable.Range(0, _n).Any(i => mainland[i] && T(i).slot == index)) continue;
                // Carve the shortest bridge from the slot to the mainland.
                var from = new int[_n];
                for (int i = 0; i < _n; i++) from[i] = -2;
                var open = new Queue<int>();
                for (int i = 0; i < _n; i++)
                {
                    if (T(i).slot == index)
                    {
                        from[i] = -1;
                        open.Enqueue(i);
                    }
                }
                int reachedAt = -1;
                while (open.Count > 0 && reachedAt < 0)
                {
                    int c = open.Dequeue();
                    for (int d = 0; d < 6 && reachedAt < 0; d++)
                    {
                        int nb = map.Neighbour(c, d);
                        if (nb < 0 || from[nb] != -2 || _reserved[nb]) continue;
                        from[nb] = c;
                        if (mainland[nb]) reachedAt = nb;
                        else open.Enqueue(nb);
                    }
                }
                if (reachedAt < 0)
                {
                    map.Report.errors.Add($"{slot.id} cannot be joined to the mainland.");
                    continue;
                }
                int carved = 0;
                for (int c = from[reachedAt]; c >= 0; c = from[c])
                {
                    var t = T(c);
                    if (!t.water) continue;
                    t.water = t.lake = false;
                    t.composition = WorldComposition.Intersection;
                    t.seam = true;
                    t.elevation = settings.seaLevel + 0.03f;
                    carved++;
                }
                map.Report.repairs.Add($"{slot.id} was cut off from the mainland: a land bridge of {carved} cell(s) was raised.");
                mainland = Mainland(core);
            }

            // The ocean ring: every reserved cell is water and one body of water; the mainland keeps its distance from the edge.
            var border = Steps(i => IsBorder(i));
            int nearest = int.MaxValue;
            for (int i = 0; i < _n; i++) if (mainland[i]) nearest = Math.Min(nearest, border[i]);
            if (nearest < settings.oceanRingWidth) map.Report.errors.Add($"The mainland comes within {nearest} cell(s) of the world's edge (the ocean ring needs {settings.oceanRingWidth}).");
            var sea = Steps(i => IsBorder(i) && T(i).water, i => T(i).water && !T(i).lake);
            int cut = Enumerable.Range(0, _n).Count(i => _reserved[i] && sea[i] == int.MaxValue);
            if (cut > 0) map.Report.errors.Add($"{cut} cell(s) of the ocean ring are cut off from the open sea.");
            map.Report.notes.Add($"Ocean ring: the mainland stays {nearest} cells from the edge; {map.Tiles.Count(t => mainland[t.index])} mainland cells.");
            MeasureCoast();
        }

        /// <summary>The first slot (of a quadrant that is not an island) with no cell on the mainland, or null.</summary>
        private string CutOffSlot()
        {
            var mainland = Mainland(CoreCell());
            var joined = new bool[stencil.Slots.Count];
            for (int i = 0; i < _n; i++) if (mainland[i] && T(i).slot >= 0) joined[T(i).slot] = true;
            foreach (var slot in stencil.Slots)
            {
                if (!joined[slot.index] && settings.Quadrant(slot.quadrant)?.island != true) return slot.id;
            }
            return null;
        }

        private bool IsBorder(int i)
        {
            for (int d = 0; d < 6; d++) if (map.Neighbour(i, d) < 0) return true;
            return false;
        }

        // ===== 8. WATER =====

        private void SculptBasins()
        {
            var candidates = map.Tiles.Where(t => !t.water && t.handmadeTile == null && t.moisture >= 0.45f && !_reserved[t.index])
                .OrderBy(t => WorldNoise.Hash01(_pickSeed + 71, t.index, 0)).ToList();
            var centers = new List<WorldTile>();
            foreach (var center in candidates)
            {
                if (centers.Count >= Math.Max(0, settings.naturalBasins)) break;
                if (centers.Any(t => HexCoord.Distance(t.coord, center.coord) < 10)) continue;
                int radius = 3 + (int)(WorldNoise.Hash01(_pickSeed + 72, center.index, 0) * 4);
                var bowl = HexCoord.Spiral(center.coord, radius).Select(map.Get).Where(t => t != null).ToList();
                if (bowl.Any(t => t.water || t.handmadeTile != null || _reserved[t.index])) continue;
                foreach (var t in bowl)
                {
                    double angle = WorldNoise.Hash01(_pickSeed + 73, center.index, 0) * Math.PI;
                    double dx = (t.x - center.x) / HexHierarchy.Spacing(HexHierarchy.Meso);
                    double dy = (t.y - center.y) / HexHierarchy.Spacing(HexHierarchy.Meso);
                    double along = dx * Math.Cos(angle) + dy * Math.Sin(angle);
                    double across = -dx * Math.Sin(angle) + dy * Math.Cos(angle);
                    double shoreline = 1 + 0.22 * _coast.Noise(dx * 0.7 + center.x, dy * 0.7 + center.y);
                    float distance = (float)(Math.Sqrt(along * along + across * across * 2.3) / (radius * shoreline));
                    float depth = 0.16f * (1f - (float)WorldNoise.Smooth(0, 1, distance));
                    t.elevation = Math.Max(settings.seaLevel + 0.01f, t.elevation - depth);
                }
                centers.Add(center);
            }
            map.Report.notes.Add($"Sculpted {centers.Count} wet basin candidates; drainage decides which retain water.");
        }

        private void Drain()
        {
            var filled = new float[_n];
            var done = new bool[_n];
            var heap = new MinHeap(_n);
            var order = new List<int>(_n);
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                filled[i] = t.elevation;
                t.downstream = -1;
                if (t.water && !t.lake)
                {
                    done[i] = true;
                    heap.Push(i, filled[i], WorldNoise.Hash01(_pickSeed + 3, i, 0));
                }
            }
            while (heap.Count > 0)
            {
                int c = heap.Pop();
                order.Add(c);
                for (int d = 0; d < 6; d++)
                {
                    int nb = map.Neighbour(c, d);
                    if (nb < 0 || done[nb]) continue;
                    done[nb] = true;
                    filled[nb] = Math.Max(T(nb).elevation, filled[c] + 1e-5f);
                    T(nb).downstream = c;
                    heap.Push(nb, filled[nb], WorldNoise.Hash01(_pickSeed + 3, nb, 0));
                }
            }
            // Land no outlet reaches (enclosed by the map's edge) drains nowhere: report it.
            int stranded = Enumerable.Range(0, _n).Count(i => !done[i]);
            if (stranded > 0) map.Report.errors.Add($"{stranded} cell(s) have no way to the sea.");

            // Deep hollows hold lakes.
            var candidate = new bool[_n];
            for (int i = 0; i < _n; i++) candidate[i] = !T(i).water && T(i).handmadeTile == null && filled[i] - T(i).elevation > settings.lakeDepth;
            var seen = new bool[_n];
            int lakes = 0;
            for (int i = 0; i < _n; i++)
            {
                if (!candidate[i] || seen[i]) continue;
                var group = new List<int>();
                var open = new Stack<int>();
                open.Push(i);
                seen[i] = true;
                while (open.Count > 0)
                {
                    int c = open.Pop();
                    group.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        int nb = map.Neighbour(c, d);
                        if (nb >= 0 && candidate[nb] && !seen[nb])
                        {
                            seen[nb] = true;
                            open.Push(nb);
                        }
                    }
                }
                if (group.Count < Math.Max(1, settings.minLakeCells) || group.Count > Math.Max(settings.minLakeCells, settings.maxLakeCells)) continue; // a vast hollow stays land and drains across
                foreach (int c in group)
                {
                    T(c).water = true;
                    T(c).lake = true;
                }
                // A lake never cuts a slot off the mainland: that hollow stays dry ground (its water still drains).
                string cut = CutOffSlot();
                if (cut != null)
                {
                    foreach (int c in group)
                    {
                        T(c).water = false;
                        T(c).lake = false;
                    }
                    map.Report.notes.Add($"A lake of {group.Count} cells at {T(group[0]).coord} was left dry: it would cut {cut} off the mainland.");
                    continue;
                }
                lakes++;
            }
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (t.lake) t.waterDepth = Math.Max(0f, filled[i] - t.elevation);
                if (!t.water || t.lake) t.elevation = filled[i];
            }

            // Rain runs downhill and gathers.
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                t.flow = t.water && !t.lake ? 0f : 0.3f + 0.7f * t.moisture;
            }
            for (int k = order.Count - 1; k >= 0; k--)
            {
                var t = T(order[k]);
                if (t.downstream >= 0) T(t.downstream).flow += t.flow;
            }
            for (int i = 0; i < _n; i++) T(i).river = !T(i).water && T(i).flow >= settings.riverThreshold;
            // Cut channels from the outlet upstream. Every new bed remains above its downstream bed;
            // fixed handmade cells and lake spill levels constrain the cut rather than being overwritten.
            foreach (int i in order)
            {
                var t = T(i);
                if (!t.river || t.handmadeTile != null || t.downstream < 0) continue;
                float cut = Math.Min(0.055f, 0.008f * (float)Math.Log(1f + t.flow));
                t.elevation = Math.Max(T(t.downstream).elevation + 1e-5f, t.elevation - cut);
            }
            TraceRivers();
            map.Report.notes.Add($"Water: {lakes} lake(s), {map.Rivers.Count} river(s) ({map.Rivers.Count(r => r.major)} major).");
        }

        private void DescribeRelief()
        {
            foreach (var t in map.Tiles)
            {
                float rise = 0f, fall = 0f;
                foreach (var n in map.NeighboursOf(t))
                {
                    rise = Math.Max(rise, n.elevation - t.elevation);
                    fall = Math.Max(fall, t.elevation - n.elevation);
                }
                t.escarpment = Math.Max(rise, fall);
                t.landform = t.lake ? "Drowned basin" : t.water ? "Sea" :
                    t.river && rise > 0.035f ? "Incised river gorge" : t.river ? "Alluvial reach" :
                    fall > 0.045f ? "Escarpment" : t.elevation > 0.72f ? "High ridge" :
                    t.escarpment < 0.018f ? "Open plain" : "Terraced upland";
            }
        }

        private void TraceRivers()
        {
            var upstreamRiver = new bool[_n];
            var upstreamLake = new int[_n];
            for (int i = 0; i < _n; i++) upstreamLake[i] = -1;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (t.downstream < 0) continue;
                if (t.river) upstreamRiver[t.downstream] = true;
                if (t.lake && upstreamLake[t.downstream] < 0) upstreamLake[t.downstream] = i;
            }
            var traced = new bool[_n];
            for (int i = 0; i < _n; i++)
            {
                if (!T(i).river || upstreamRiver[i]) continue;
                var path = new RiverPath();
                if (upstreamLake[i] >= 0) path.cells.Add(upstreamLake[i]);
                int c = i;
                while (c >= 0)
                {
                    var t = T(c);
                    path.cells.Add(c);
                    path.flow = Math.Max(path.flow, t.flow);
                    if (t.water || traced[c]) break;
                    traced[c] = true;
                    c = t.downstream;
                }
                if (path.cells.Count < 2) continue;
                path.major = path.flow >= settings.majorRiverThreshold;
                map.Rivers.Add(path);
            }
        }

        // ===== 9. GROUND =====

        private string Pick(List<TerrainRule> rules, float elevation, float moisture, double roll)
        {
            TerrainRule chosen = null;
            double total = 0;
            foreach (var rule in rules)
            {
                if (rule == null || rule.weight <= 0f || !rule.Holds(elevation, moisture) || settings.Terrain(rule.terrain) == null) continue;
                total += rule.weight;
            }
            if (total > 0)
            {
                double at = roll * total;
                foreach (var rule in rules)
                {
                    if (rule == null || rule.weight <= 0f || !rule.Holds(elevation, moisture) || settings.Terrain(rule.terrain) == null) continue;
                    at -= rule.weight;
                    chosen = rule;
                    if (at < 0) break;
                }
                return chosen?.terrain;
            }
            // No rule holds: the nearest one by climate.
            float best = float.MaxValue;
            foreach (var rule in rules)
            {
                if (rule == null || settings.Terrain(rule.terrain) == null) continue;
                float miss = Outside(elevation, rule.minElevation, rule.maxElevation) + Outside(moisture, rule.minMoisture, rule.maxMoisture);
                if (miss < best)
                {
                    best = miss;
                    chosen = rule;
                }
            }
            return chosen?.terrain;
        }

        private static float Outside(float v, float min, float max) => v < min ? min - v : v > max ? v - max : 0f;

        // A roughly uniform 0-1 value that changes smoothly across the land (patches a few cells wide), with a
        // little per-cell grain so patch edges are ragged.
        private double Patch(WorldTile t, double cells, double offset, int grainSeed)
        {
            double scale = 1.0 / (HexHierarchy.Spacing(HexHierarchy.Meso) * cells);
            double v = _patch.Fbm(t.x * scale + offset, t.y * scale - offset * 0.7, 3);
            double u = 1.0 / (1.0 + Math.Exp(-v * 7.0));
            return Math.Max(0.0, Math.Min(0.9999, 0.94 * u + 0.06 * WorldNoise.Hash01(grainSeed, t.coord.q, t.coord.r)));
        }

        private void Lay()
        {
            float reach = Math.Max(1, settings.seamReach);
            string fallback = settings.terrains.FirstOrDefault(t => t != null && t.passable && !t.water)?.id;
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                var b1 = MacroBiomeOf(_s1[i]);
                t.macroBiome = t.water && !t.lake ? null : b1?.id;
                if (t.handmadeTile != null && !(t.water && !t.lake)) continue;
                if (t.water)
                {
                    t.terrain = t.lake ? settings.lakeTerrain : _toLand[i] <= 2 ? settings.shallowsTerrain : settings.oceanTerrain;
                    continue;
                }
                // Coherent noise, not a per-cell draw: ground comes in patches and seams interlock in fingers.
                double roll = Patch(t, 7.0, 0.0, _pickSeed);
                double mix = Patch(t, 5.0, 57.3, _pickSeed + 1);
                // Each lending slot's biome is drawn by its weight: seams dither between their neighbours' ground.
                var biome = b1;
                double at = mix;
                for (int k = 0; k < _mixSlot[i].Length; k++)
                {
                    at -= _mixWeight[i][k];
                    if (at >= 0) continue;
                    biome = MacroBiomeOf(_mixSlot[i][k]) ?? b1;
                    break;
                }
                bool intersection = biome == null;
                if (t.composition == WorldComposition.Intersection)
                {
                    float own = Clamp((_d1[i] - 1f) / (reach * 0.6f), 0f, 0.85f);
                    if (Patch(t, 6.0, -41.9, _pickSeed + 2) < own) intersection = true;
                }
                string terrain = null;
                if (intersection && settings.intersectionTerrains.Count > 0) terrain = Pick(settings.intersectionTerrains, t.elevation, t.moisture, roll);
                if (terrain == null && biome != null) terrain = Pick(biome.terrains, t.elevation, t.moisture, roll);
                t.terrain = terrain ?? fallback;
                if (string.Equals(t.macroBiome, "auric-grasslands", StringComparison.OrdinalIgnoreCase))
                {
                    // The starting region reads as landforms, not a random mix of vegetation.
                    string ground = t.escarpment > 0.055f ? "foothills" :
                        t.escarpment < 0.018f && !t.river ? "auric-meadow" :
                        t.moisture > 0.53f && roll > 0.60 ? "auric-copse" : null;
                    if (ground != null && settings.Terrain(ground) != null) t.terrain = ground;
                }
            }

            // One smoothing pass softens the seams (never a protected interior or a handmade cell).
            var next = new string[_n];
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                next[i] = t.terrain;
                if (!t.seam || t.water || t.handmadeTile != null) continue;
                var counts = new Dictionary<string, int>();
                foreach (var nb in map.NeighboursOf(t))
                {
                    if (nb.water || nb.terrain == null) continue;
                    counts.TryGetValue(nb.terrain, out int c);
                    counts[nb.terrain] = c + 1;
                }
                var top = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).FirstOrDefault();
                if (top.Key != null && top.Value >= 4 && top.Key != t.terrain) next[i] = top.Key;
            }
            for (int i = 0; i < _n; i++) T(i).terrain = next[i];
        }

        // ===== 10. FERTILITY AND THE CAPITAL =====

        private void Fertilize()
        {
            var toFresh = Steps(i => T(i).river || T(i).lake, i => !T(i).water || T(i).lake);
            for (int i = 0; i < _n; i++)
            {
                var t = T(i);
                if (t.water)
                {
                    t.landFertility = 0f;
                    continue;
                }
                float soil = settings.Terrain(t.terrain)?.fertility ?? 0.3f;
                float water = toFresh[i] == int.MaxValue ? 0.15f : Math.Max(0.15f, 1f - toFresh[i] / 7f);
                water *= 0.5f + 0.5f * t.moisture;
                float climate = Clamp(1f - Math.Abs(t.temperature - 0.55f) * 1.3f, 0.1f, 1f);
                float slope = 0f;
                foreach (var nb in map.NeighboursOf(t)) if (!nb.water) slope = Math.Max(slope, Math.Abs(nb.elevation - t.elevation));
                t.landFertility = Clamp(soil * (0.35f + 0.65f * water) * climate - Math.Min(0.5f, slope * 3f), 0f, 1f);
            }
        }

        private void PlaceCapital()
        {
            var s1 = stencil.Slots.FirstOrDefault(s => string.Equals(s.quadrant, "Q1", StringComparison.OrdinalIgnoreCase));
            var toFresh = Steps(i => T(i).river || T(i).lake);
            var mainland = Mainland(CoreCell()); // never a pocket that lakes cut off
            WorldTile best = null;
            double bestScore = double.MinValue;
            foreach (var t in map.Tiles)
            {
                if (t.water || !mainland[t.index] || (s1 != null && t.slot != s1.index) || (s1 != null && _depth[t.index] < 2)) continue;
                var terrain = settings.Terrain(t.terrain);
                if (terrain == null || !terrain.passable) continue;
                double score = t.landFertility - Math.Sqrt(t.x * t.x + t.y * t.y) * 0.01 + (toFresh[t.index] <= 2 ? 1.0 : 0.0) + (t.river ? 0.2 : 0.0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            if (best == null)
            {
                best = map.Tiles.Where(t => !t.water).OrderBy(t => t.x * t.x + t.y * t.y).FirstOrDefault();
                map.Report.errors.Add("No Q1 cell can hold the capital: it stands on the land nearest the centre.");
            }
            if (best == null) return;
            if (toFresh[best.index] > 3)
            {
                var pond = map.NeighboursOf(best).Where(nb => !nb.water).OrderBy(nb => nb.index).FirstOrDefault();
                if (pond != null)
                {
                    pond.water = pond.lake = true;
                    pond.landform = "Spring-fed basin";
                    pond.waterDepth = 0.02f;
                    pond.terrain = settings.lakeTerrain;
                    pond.landFertility = 0f;
                    map.Report.repairs.Add($"The capital at {best.coord} had no freshwater within 3 cells: a spring-fed pond was placed at {pond.coord}.");
                }
            }
            map.Capital = best.coord;
            foreach (var t in map.Tiles)
            {
                float dx = t.x - best.x, dy = t.y - best.y;
                t.quarter = dy >= 0 ? (dx >= 0 ? 0 : 1) : (dx < 0 ? 2 : 3);
            }
            best.feature = settings.capitalFeature;
            map.ExploreAround(best.coord, Math.Max(0, settings.startExploreRadius), Math.Max(0, settings.startRevealRadius), id => settings.Terrain(id)?.passable != false);
        }

        // ===== 11. CRAGS (the micro travel grid) =====

        /// <summary>Escarpment steeper than this breaks into crags, more of them the steeper it is.</summary>
        private const float CragEscarpment = 0.04f;

        // Steep ground breaks into crags: micro hexes no one climbs (never a cell's centre, where its sites and
        // settlements stand), so a unit's way winds through an escarpment instead of over it. Every two walkable
        // neighbouring cells keep a crossing between them, so crags reroute travel but never cut the land apart.
        private void PlaceCrags()
        {
            bool Walkable(WorldTile t) => !t.water && settings.Terrain(t.terrain)?.passable != false;
            int cragSeed = WorldNoise.Stream(seed, "crags");
            foreach (var t in map.Tiles)
            {
                t.microBlockedMask = 0;
                if (!Walkable(t) || t.escarpment <= CragEscarpment) continue;
                double chance = Math.Min(0.3, 0.06 + (t.escarpment - CragEscarpment) * 2.5);
                for (int child = 1; child < MicroNavigation.PerCell; child++)
                    if (WorldNoise.Hash01(cragSeed, t.index, child) < chance) t.microBlockedMask |= 1 << child;
            }
            int repaired = 0;
            foreach (var t in map.Tiles)
            {
                if (!Walkable(t)) continue;
                for (int d = 0; d < 6; d++)
                {
                    int n = map.Neighbour(t.index, d);
                    if (n < 0 || !Walkable(T(n))) continue;
                    var other = T(n);
                    var contacts = MicroNavigation.Contacts(d);
                    if (contacts.Any(c => (t.microBlockedMask & (1 << c.mine)) == 0 && (other.microBlockedMask & (1 << c.theirs)) == 0)) continue;
                    foreach (var (mine, theirs) in contacts)
                    {
                        t.microBlockedMask &= ~(1 << mine);
                        other.microBlockedMask &= ~(1 << theirs);
                    }
                    repaired++;
                }
            }
            int crags = map.Tiles.Sum(t => MicroNavigation.Crags(t.microBlockedMask));
            if (crags > 0) map.Report.notes.Add($"{crags} crags on escarpments ({repaired} crossings kept open between neighbouring cells)");
        }
    }

    // ===== FEATURES =====

    /// <summary>
    /// Place the features that arrive in Age <paramref name="age"/> (roadmap WG10): on dry passable ground they allow,
    /// in the biomes they allow, within their distance of the capital, apart from their own kind, preferring the best
    /// cells of their field (fertile land, Coherence...), spread over the quarters around the capital, and (unless they
    /// may) off explored ground. Same map, settings and Age give the same places. Returns the cells that received one.
    /// </summary>
    public static List<HexCoord> PlaceFeatures(WorldMap map, WorldGenSettings settings, int age)
    {
        var placed = new List<HexCoord>();
        if (map == null || settings == null) return placed;
        var rng = new Random(WorldNoise.Stream(map.seed, "features:" + age));

        foreach (var feature in settings.features.Where(f => f != null && f.minAge == age && f.count > 0 && f.id != settings.capitalFeature))
        {
            var candidates = map.Tiles.Where(t => Allows(feature, t, map, settings)).ToList();
            if (feature.prefers != FeatureSpec.Preference.None && candidates.Count > 0)
            {
                int keep = Math.Max(feature.count * 4, (int)(candidates.Count * 0.35f));
                candidates = candidates.OrderByDescending(t => FieldOf(t, feature.prefers)).ThenBy(t => t.index).Take(keep).ToList();
            }
            candidates = candidates.OrderBy(t => t.index).ToList(); // a stable order before the seeded draws
            for (int n = 0; n < feature.count && candidates.Count > 0; n++)
            {
                // Prefer the quarter holding the fewest of this feature, so each part of the world gets its share.
                int quarter = Enumerable.Range(0, 4)
                    .Where(q => candidates.Any(t => t.quarter == q))
                    .OrderBy(q => map.WithFeature(feature.id).Count(t => t.quarter == q))
                    .ThenBy(q => q)
                    .First();
                var pool = candidates.Where(t => t.quarter == quarter).ToList();
                var tile = pool[rng.Next(pool.Count)];
                tile.feature = feature.id;
                tile.featureAge = age;
                placed.Add(tile.coord);
                int spacing = Math.Max(1, feature.spacing);
                candidates.RemoveAll(t => HexCoord.Distance(t.coord, tile.coord) < spacing);
            }
        }
        WorldAuthority.Establish(map, settings);
        return placed;
    }

    private static float FieldOf(WorldTile tile, FeatureSpec.Preference preference)
    {
        switch (preference)
        {
            case FeatureSpec.Preference.LandFertility: return tile.landFertility;
            case FeatureSpec.Preference.MagicalFertility: return tile.magicFertility;
            case FeatureSpec.Preference.Coherence: return tile.coherence;
            case FeatureSpec.Preference.River: return tile.river ? 1f + tile.flow * 0.001f : 0f;
            default: return 0f;
        }
    }

    private static bool Allows(FeatureSpec feature, WorldTile tile, WorldMap map, WorldGenSettings settings)
    {
        if (tile.coherence < feature.minimumCoherence) return false;
        if (feature.requiresFreshwater && !tile.river && !map.NeighboursOf(tile).Any(t => t.river || t.lake)) return false;
        if (feature.requiresCoast && !map.NeighboursOf(tile).Any(t => t.water && !t.lake)) return false;
        if (!string.IsNullOrEmpty(feature.authorityId) && tile.authorityId != WorldAuthority.Wilderness) return false;
        if (tile.HasFeature || tile.water || tile.coord == map.Capital || tile.sacred || tile.settlement >= 0 || tile.enclave >= 0) return false;
        if (tile.explored && !feature.mayAppearOnExplored) return false;
        int distance = map.StepsFromCapital(tile.coord);
        if (distance < feature.minDistance || distance > feature.maxDistance) return false;
        var terrain = settings.Terrain(tile.terrain);
        if (terrain == null || !terrain.passable) return false;
        if (feature.terrains.Count > 0 && !feature.terrains.Any(t => string.Equals(t, tile.terrain, StringComparison.OrdinalIgnoreCase))) return false;
        if (feature.macroBiomes.Count > 0 && !feature.macroBiomes.Any(b => string.Equals(b, tile.macroBiome, StringComparison.OrdinalIgnoreCase))) return false;
        return true;
    }

    // ===== HELPERS =====

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// A binary min-heap of cell indices by priority. Ties (the flats left by filling hollows) are broken by a seeded
    /// hash rather than by index, so water finds its way across a flat in a natural fan, not a ruled line.
    /// </summary>
    private class MinHeap
    {
        private readonly List<(float key, double tie, int index)> _items;

        public MinHeap(int capacity) => _items = new List<(float, double, int)>(capacity);

        public int Count => _items.Count;

        private static bool Less((float key, double tie, int index) a, (float key, double tie, int index) b) =>
            a.key < b.key || (a.key == b.key && (a.tie < b.tie || (a.tie == b.tie && a.index < b.index)));

        public void Push(int index, float key, double tie)
        {
            _items.Add((key, tie, index));
            int c = _items.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (!Less(_items[c], _items[p])) break;
                (_items[c], _items[p]) = (_items[p], _items[c]);
                c = p;
            }
        }

        public int Pop()
        {
            var top = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1, r = l + 1, m = c;
                if (l < _items.Count && Less(_items[l], _items[m])) m = l;
                if (r < _items.Count && Less(_items[r], _items[m])) m = r;
                if (m == c) break;
                (_items[c], _items[m]) = (_items[m], _items[c]);
                c = m;
            }
            return top.index;
        }
    }
}
