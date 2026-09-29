using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The strategic geography placed over the terrain (WORLD_GENERATION.md §6 C-E and G; Arcanoria.md, Map Features),
/// with no scene state (tested in <c>WorldCivilizationTests</c>). The same map, settings and Age give the same sites.
///
/// - Trade Nexus sites: rare gifted geography (sheltered harbors, estuaries, mountain passes, river confluences),
///   spaced apart; a site is potential, not an operating hub.
/// - Resource Grandfields: sparse multi-cell footprints of one resource on compatible substrate, densest at the heart.
/// - Threats: spawn potential (Vibrational Fallout at the Dissonance Seeds; later Ages add their own), a danger field
///   that falls off with distance and never touches Sacred ground.
/// - Enclaves: Age-appropriate candidates placed where their function fits (a Trading enclave at a nexus, a Militant
///   one guarding a river approach, an Agromagical one on dual-fertile or silver-watered land).
/// </summary>
public static class WorldSites
{
    /// <summary>Cells around a Sacred Site kept free of danger, and the farther ring where it is halved.</summary>
    public const int SacredCalm = 2, SacredShade = 5;

    /// <summary>Place what arrives in Age <paramref name="age"/> (nexus sites once, at Age 0), then rebuild authority.</summary>
    public static void PlaceAge(WorldMap map, WorldGenSettings settings, int age)
    {
        if (age == 0 && map.NexusSites.Count == 0) FindNexusSites(map, settings);
        // Cover first: some resource sites stand only inside it.
        if (age == 0) WorldCover.Place(map, settings);
        PlaceGrandfields(map, settings, age);
        PlaceThreats(map, settings, age);
        PlaceEnclaves(map, settings, age);
        WorldResources.PlaceAge(map, settings, age);
        WorldCivilization.Rebuild(map, settings);
    }

    // ===== TRADE NEXUS =====

    public static void FindNexusSites(WorldMap map, WorldGenSettings settings)
    {
        var rules = settings.nexus ?? new NexusRules();
        foreach (var t in map.Tiles) t.nexus = null;
        map.NexusSites.Clear();
        var candidates = new List<(WorldTile tile, string kind, float score)>();
        float major = Math.Max(1f, settings.majorRiverThreshold);
        foreach (var t in map.Tiles)
        {
            if (t.water || t.impassable || t.sacred) continue;
            if (map.StepsFromCapital(t.coord) < rules.minDistance) continue;
            int sea = 0, lake = 0, blocked = 0, open = 0;
            var blockedDirs = new List<int>();
            for (int d = 0; d < 6; d++)
            {
                int nb = map.Neighbour(t.index, d);
                if (nb < 0) continue;
                var n = map[nb];
                if (n.water && !n.lake) sea++;
                else if (n.lake) lake++;
                else if (n.impassable) { blocked++; blockedDirs.Add(d); }
                else open++;
            }
            string kind = null;
            float score = 0f;
            if (rules.estuaries && t.river && t.downstream >= 0 && map[t.downstream].water && !map[t.downstream].lake)
            {
                kind = "estuary";
                score = 1.6f + Math.Min(1.5f, t.flow / major);
            }
            else if (rules.harbors && sea >= 1 && sea <= 3 && open >= 2)
            {
                // Sheltered: the sea around is a bay, not open coast (a straight shore puts about 7-9 of 19 cells at sea).
                int seaNear = HexCoord.Spiral(t.coord, 2).Count(c => { var x = map.Get(c); return x != null && x.water && !x.lake; });
                if (seaNear >= 2 && seaNear <= 7)
                {
                    kind = "harbor";
                    score = 1.2f + 0.08f * (8 - seaNear) + 0.3f * t.coherence;
                }
            }
            if (kind == null && rules.passes && blocked >= 2 && open >= 2 && blockedDirs.Any(a => blockedDirs.Any(b => Math.Abs(a - b) == 3 || Math.Abs(a - b) == 2 || Math.Abs(a - b) == 4)))
            {
                kind = "pass";
                score = 1.1f + 0.1f * blocked;
            }
            if (kind == null && rules.confluences && t.river)
            {
                int inflows = 0;
                for (int d = 0; d < 6; d++)
                {
                    int nb = map.Neighbour(t.index, d);
                    if (nb >= 0 && map[nb].river && map[nb].downstream == t.index) inflows++;
                }
                if (inflows >= 2)
                {
                    kind = "confluence";
                    score = 1f + Math.Min(1.5f, t.flow / major);
                }
            }
            if (kind != null) candidates.Add((t, kind, score + 0.2f * t.coherence));
        }
        // Each kind takes turns by its own rank, so a world keeps harbors, passes and crossings, not only its best estuaries.
        var ranked = candidates.GroupBy(c => c.kind)
            .SelectMany(g => g.OrderByDescending(c => c.score).ThenBy(c => c.tile.index).Select((c, rank) => (c, rank)))
            .OrderBy(x => x.rank).ThenByDescending(x => x.c.score).ThenBy(x => x.c.tile.index).Select(x => x.c);
        foreach (var c in ranked)
        {
            if (map.NexusSites.Count >= rules.count) break;
            if (map.NexusSites.Any(s => HexCoord.Distance(map[s].coord, c.tile.coord) < rules.spacing)) continue;
            c.tile.nexus = c.kind;
            map.NexusSites.Add(c.tile.index);
        }
        map.Report.notes.Add($"Trade Nexus sites: {string.Join(", ", map.NexusSites.GroupBy(s => map[s].nexus).Select(g => $"{g.Count()} {g.Key}"))}.");
    }

    public static string NexusName(string kind)
    {
        switch (kind)
        {
            case "harbor": return "a sheltered harbor";
            case "estuary": return "an estuary";
            case "pass": return "a mountain pass";
            case "confluence": return "a river confluence";
            default: return kind;
        }
    }

    // ===== GRANDFIELDS =====

    private static bool Substrate(WorldMap map, WorldTile t, GrandfieldSpec spec, WorldGenSettings settings)
    {
        if (t.water || t.impassable || t.sacred || t.grandfield >= 0) return false;
        if (settings.Terrain(t.terrain)?.passable == false) return false;
        if (spec.terrains.Count > 0 && !spec.terrains.Any(x => string.Equals(x, t.terrain, StringComparison.OrdinalIgnoreCase))) return false;
        if (spec.macroBiomes.Count > 0 && !spec.macroBiomes.Any(x => string.Equals(x, t.macroBiome, StringComparison.OrdinalIgnoreCase))) return false;
        if (t.coherence < spec.minimumCoherence || t.magicFertility < spec.minimumMagicalFertility) return false;
        return true;
    }

    public static void PlaceGrandfields(WorldMap map, WorldGenSettings settings, int age)
    {
        foreach (var spec in settings.grandfields.Where(g => g != null && g.minAge == age && g.count > 0))
        {
            if (map.Grandfields.Any(g => g.spec == spec.id)) continue;
            var rng = new Random(WorldNoise.Stream(map.seed, "grandfield:" + spec.id));
            int size = Math.Max(1, spec.size), placed = 0;
            var centres = map.Tiles.Where(t => Substrate(map, t, spec, settings) && map.StepsFromCapital(t.coord) >= spec.minDistance && !t.HasFeature
                    && (!spec.requiresFreshwater || t.river || map.NeighboursOf(t).Any(n => n.river || n.lake)))
                .OrderBy(t => WorldNoise.Hash01(WorldNoise.Stream(map.seed, "grandfield-centre:" + spec.id), t.index, 0)).ToList();
            int apart = 2 * (int)Math.Ceiling(Math.Sqrt(size)) + 6;
            foreach (var centre in centres)
            {
                if (placed >= spec.count) break;
                if (centre.grandfield >= 0 || map.Grandfields.Any(g => HexCoord.Distance(map[g.center].coord, centre.coord) < apart)) continue;
                // Grow over compatible ground, nearest first (ties broken by the seed).
                var field = new List<(int cell, int steps)> { (centre.index, 0) };
                var seen = new HashSet<int> { centre.index };
                var frontier = new List<(int cell, int steps)> { (centre.index, 0) };
                while (field.Count < size && frontier.Count > 0)
                {
                    var next = new List<(int, int)>();
                    foreach (var (cell, steps) in frontier)
                        foreach (var n in map.NeighboursOf(map[cell]))
                            if (seen.Add(n.index) && Substrate(map, n, spec, settings)) next.Add((n.index, steps + 1));
                    foreach (var item in next.OrderBy(_ => rng.NextDouble()))
                    {
                        if (field.Count >= size) break;
                        field.Add(item);
                    }
                    frontier = next;
                }
                if (field.Count < Math.Max(1, size / 2)) continue;
                var grandfield = new Grandfield { index = map.Grandfields.Count, spec = spec.id, resource = spec.resource, center = centre.index };
                int reach = field.Max(f => f.steps);
                foreach (var (cell, steps) in field)
                {
                    grandfield.cells.Add(cell);
                    map[cell].grandfield = grandfield.index;
                    map[cell].grandfieldDensity = 1f - 0.65f * steps / Math.Max(1f, reach);
                }
                map.Grandfields.Add(grandfield);
                placed++;
            }
            if (placed < spec.count) map.Report.notes.Add($"Grandfield '{spec.id}': {placed} of {spec.count} placed (not enough compatible ground).");
        }
    }

    // ===== THREATS =====

    public static void PlaceThreats(WorldMap map, WorldGenSettings settings, int age)
    {
        var sacred = map.Magic?.SacredSites ?? new List<int>();
        bool NearSacred(WorldTile t, int reach) => sacred.Any(s => HexCoord.Distance(map[s].coord, t.coord) <= reach);
        foreach (var spec in settings.threats.Where(s => s != null && s.minAge == age))
        {
            if (map.Threats.Any(x => x.spec == spec.id)) continue;
            var cells = new List<int>();
            if (spec.onDissonanceSeeds && map.Magic != null)
                cells.AddRange(map.Magic.Seeds.Where(s => s.dissonance).Select(s => s.cell).Where(c => !NearSacred(map[c], SacredShade)));
            else
            {
                var candidates = map.Tiles.Where(t => !t.water && !t.impassable && map.StepsFromCapital(t.coord) >= spec.minDistance && !NearSacred(t, SacredShade) && t.settlement < 0)
                    .OrderBy(t => map.Magic != null ? map.Magic.BaseCoherence(t.index) : t.coherence).ThenBy(t => t.index).Take(Math.Max(spec.count * 30, 60))
                    .OrderBy(t => WorldNoise.Hash01(WorldNoise.Stream(map.seed, "threat:" + spec.id), t.index, age)).ToList();
                foreach (var t in candidates)
                {
                    if (cells.Count >= spec.count) break;
                    if (cells.Any(c => HexCoord.Distance(map[c].coord, t.coord) < spec.radius * 2)) continue;
                    cells.Add(t.index);
                }
            }
            foreach (int c in cells) map.Threats.Add(new ThreatSite { spec = spec.id, cell = c, age = age, strength = spec.strength, radius = spec.radius });
        }
        RecomputeDanger(map);
    }

    /// <summary>The threats' danger at one cell before Sacred calm and wards (what a district reads for adjacency).</summary>
    public static float ThreatDanger(WorldMap map, HexCoord coord)
    {
        float danger = 0f;
        foreach (var threat in map.Threats)
        {
            float f = 1f - HexCoord.Distance(coord, map[threat.cell].coord) / (threat.radius + 1f);
            if (f > 0f) danger = Math.Max(danger, threat.strength * f * f);
        }
        return danger;
    }

    /// <summary>
    /// Danger: the standing hazards of each cell (a predatory bloom's); Sacred ground is calm, and districts that keep
    /// watch ward it off around them (<see cref="WorldTributaries.Ward"/>). Threats cast no aura: their bands roam, and
    /// what hunts is known only by the signs it leaves (<see cref="WorldTile.signs"/>, WorldSystem.Encounters).
    /// </summary>
    public static void RecomputeDanger(WorldMap map)
    {
        foreach (var t in map.Tiles) t.danger = t.siteDanger;
        var sacred = map.Magic?.SacredSites ?? new List<int>();
        foreach (int s in sacred)
        {
            foreach (var coord in HexCoord.Spiral(map[s].coord, SacredShade))
            {
                var t = map.Get(coord);
                if (t == null) continue;
                t.danger = HexCoord.Distance(coord, map[s].coord) <= SacredCalm ? 0f : t.danger * 0.5f;
            }
        }
        WorldTributaries.Ward(map);
    }

    // ===== ENCLAVES =====

    private static bool Fits(WorldMap map, WorldTile t, EnclaveSite site)
    {
        switch (site)
        {
            case EnclaveSite.Coast: return map.NeighboursOf(t).Any(n => n.water && !n.lake);
            case EnclaveSite.Freshwater: return t.river || map.NeighboursOf(t).Any(n => n.river || n.lake);
            case EnclaveSite.Pass: return t.nexus == "pass" || map.NeighboursOf(t).Count(n => n.impassable) >= 2;
            case EnclaveSite.Nexus: return t.nexus != null || map.NeighboursOf(t).Any(n => n.nexus != null);
            case EnclaveSite.DualFertile: return t.landFertility >= 0.4f && t.magicFertility >= 0.2f;
            case EnclaveSite.Extraction: return HexCoord.Spiral(t.coord, 3).Any(c => map.Get(c)?.grandfield >= 0);
            case EnclaveSite.Anomaly: return t.dissonance >= 0.08f || t.junction >= 2 || (t.danger >= 0.15f && t.danger <= 0.5f);
            case EnclaveSite.HighCoherence: return t.coherence >= 0.6f;
            case EnclaveSite.SilverWater: return t.silver || map.NeighboursOf(t).Any(n => n.silver);
            default: return true;
        }
    }

    public static void PlaceEnclaves(WorldMap map, WorldGenSettings settings, int age)
    {
        foreach (var spec in settings.enclaves.Where(e => e != null && e.minAge == age && e.count > 0))
        {
            if (map.Enclaves.Any(e => e.spec == spec.id)) continue;
            var rng = new Random(WorldNoise.Stream(map.seed, "enclave:" + spec.id));
            var candidates = map.Tiles.Where(t => !t.water && !t.impassable && !t.sacred && t.settlement < 0 && t.enclave < 0 && !t.HasFeature && t.grandfield < 0
                    && t.authorityId == WorldAuthority.Wilderness && t.danger < 0.5f && map.StepsFromCapital(t.coord) >= spec.minDistance && Fits(map, t, spec.site))
                .OrderByDescending(t => t.Desirability + 0.25f * WorldNoise.Hash01(WorldNoise.Stream(map.seed, "enclave-site:" + spec.id), t.index, 0)).ThenBy(t => t.index).ToList();
            int placed = 0;
            foreach (var t in candidates)
            {
                if (placed >= spec.count) break;
                if (map.Enclaves.Any(e => HexCoord.Distance(e.coord, t.coord) < spec.spacing)) continue;
                if (map.Settlements.Any(s => HexCoord.Distance(s.coord, t.coord) < spec.minDistance)) continue;
                if (t.authorityId != WorldAuthority.Wilderness) continue;
                var bindings = spec.bindings.Count > 0 ? spec.bindings : CityDevelopment.Bindings.ToList();
                var enclave = new Enclave
                {
                    index = map.Enclaves.Count,
                    spec = spec.id,
                    name = spec.count > 1 ? $"{spec.name} {ToRoman(placed + 1)}" : spec.name,
                    family = spec.family,
                    binding = bindings[rng.Next(bindings.Count)],
                    coord = t.coord,
                    age = age,
                    woundResonance = rng.Next(Math.Min(spec.woundMin, spec.woundMax), Math.Max(spec.woundMin, spec.woundMax) + 1),
                    authorityRadius = spec.authorityRadius,
                };
                map.Enclaves.Add(enclave);
                t.enclave = enclave.index;
                WorldAuthority.Project(map, t.coord, enclave.AuthorityId, enclave.authorityRadius);
                placed++;
            }
            if (placed < spec.count) map.Report.notes.Add($"Enclave '{spec.id}': {placed} of {spec.count} placed (no fitting {spec.site} ground left).");
        }
    }

    private static string ToRoman(int n)
    {
        string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return n >= 1 && n <= numerals.Length ? numerals[n - 1] : n.ToString();
    }
}
