using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One species living in one Macro Biome (saved): how many of its groups (herds, packs, colonies) live there, what the
/// land could hold at the last Echo, and how many there were an Echo before (its trend).
/// </summary>
[Serializable]
public class Population
{
    /// <summary>The Macro Biome's slot (<see cref="WorldTile.habitatSlot"/>).</summary>
    public int slot;
    public string species;
    public float groups;
    /// <summary>Groups the land could hold at the last Echo.</summary>
    public float capacity;
    /// <summary>Groups an Echo before, for the trend.</summary>
    public float last;

    public float Abundance => capacity > 0.0001f ? groups / capacity : 0f;
}

public enum EcologyChangeKind { Arrived, Depleted, Recovered, Vanished }

/// <summary>What the living land did at an Echo (<see cref="WorldEcology.Tick"/>), for the notices.</summary>
public class EcologyChange
{
    public EcologyChangeKind kind;
    public Population population;
    /// <summary>The den an arrival founded, or the den that thinned, recovered or emptied.</summary>
    public ResourceSite site;
}

/// <summary>
/// How the Macro Biomes' creatures live (vault: Arcanorian Ecology.md, "Where Creatures Live"). Every number is a proposal (Canon Gaps).
/// </summary>
[Serializable]
public class EcologySettings
{
    [UnityEngine.Header("Capacity: groups one fitting cell holds at full quality")]
    public float smallPerCell = 1f;
    public float mediumPerCell = 0.5f;
    public float largePerCell = 0.25f;
    public float gargantuanPerCell = 0.05f;

    [UnityEngine.Header("Growth per Echo (logistic), by reproduction pace")]
    public float verySlowGrowth = 0.05f;
    public float slowGrowth = 0.12f;
    public float mediumGrowth = 0.25f;
    public float fastGrowth = 0.45f;
    [UnityEngine.Tooltip("Share of what the land holds that a species starts with where its first den stands.")]
    [UnityEngine.Range(0f, 1f)] public float startFill = 0.8f;

    [UnityEngine.Header("Pressure on a cell's quality (1 - pressure)")]
    [UnityEngine.Tooltip("At a settlement's own cell, fading to nothing past settlementReach.")]
    [UnityEngine.Range(0f, 1f)] public float settlementPressure = 0.6f;
    public int settlementReach = 3;
    [UnityEngine.Tooltip("On ground any authority holds (land in use).")]
    [UnityEngine.Range(0f, 1f)] public float heldPressure = 0.1f;
    [UnityEngine.Tooltip("Pressure is multiplied by this for species that flee or hide from people, and by unmovedPressure for the unmoved.")]
    public float fleeingPressure = 1.5f, unmovedPressure = 0.5f;
    [UnityEngine.Tooltip("Pure Light fragility: a species needs Coherence of at least its Pure Light share times this (a slime at 75% Pure Light needs 0.45 at 0.6).")]
    [UnityEngine.Range(0f, 1f)] public float pureLightCoherence = 0.6f;
    [UnityEngine.Tooltip("Below that need, quality falls to nothing over this much Coherence.")]
    public float coherenceMargin = 0.2f;
    [UnityEngine.Tooltip("Vibrational Fallout's harm (vault: Soliton.md, Aetherlight vulnerability): a cell's quality falls by fallout x Pure Light share x this. A 95% Structure insect walks through it; a 70% Pure Light lineage at fallout 0.5 loses 70% at 2. Discordant lineages are spared.")]
    public float falloutHarm = 2f;
    [UnityEngine.Tooltip("A Leyline lineage away from leylines and silver water keeps this share of a cell's quality.")]
    [UnityEngine.Range(0f, 1f)] public float offLeyline = 0.3f;
    [UnityEngine.Tooltip("Cells from a silver river that still count as beside the silver water, for Leyline lineages.")]
    public int leylineReach = 1;
    [UnityEngine.Tooltip("A commensal species (rats in granaries) finds a settlement's own cell worth up to this many wild cells.")]
    public float commensalCap = 1.5f;

    [UnityEngine.Header("Disease vectors (E10: fed to Disease Burden)")]
    [UnityEngine.Tooltip("Cells from one of your settlements within which a vector species' groups carry sickness in.")]
    public int vectorReach = 2;
    [UnityEngine.Tooltip("Groups of a full-strength vector (vector 1) near each of your settlements, on average, that make the pressure 1 (a small species fills about 25 near a settlement: 19 cells at up to 1.5 each).")]
    public float vectorFull = 25f;

    [UnityEngine.Header("Predators and prey")]
    [UnityEngine.Tooltip("A predator's land holds at least this share of its capacity however scarce its prey (it hunts other things too).")]
    [UnityEngine.Range(0f, 1f)] public float preyFloor = 0.2f;
    [UnityEngine.Tooltip("Share of a prey population a predator population at full strength takes each Echo.")]
    [UnityEngine.Range(0f, 1f)] public float predation = 0.08f;

    [UnityEngine.Header("Blooms in the food web (SpeciesSpec.blooms)")]
    [UnityEngine.Tooltip("Most a species drawn to Eleos Blooms gains on what its Macro Biome holds where they thrive (0.5: +50%).")]
    public float bloomDraw = 0.5f;
    [UnityEngine.Tooltip("Cells of thriving bloom ground (bloom vigor x falloff, summed over its range) that give the full gain.")]
    public float bloomCellsFull = 10f;

    [UnityEngine.Header("Hunting (an expedition's harvest at a den)")]
    [UnityEngine.Tooltip("Share of what the land holds one hunt takes (times the party's reward multiplier). A den can be hunted once a Phase, three times an Echo: 0.06 a hunt is 0.18 an Echo, more than any pace can bear for long, while every other Phase suits fast breeders and once an Echo medium ones.")]
    [UnityEngine.Range(0f, 1f)] public float huntTake = 0.06f;
    [UnityEngine.Tooltip("Share of a den's listed harvest one hunt brings back (three hunts an Echo bring about what one harvest did).")]
    [UnityEngine.Range(0f, 1f)] public float huntYield = 0.35f;
    [UnityEngine.Tooltip("Below this abundance a den is depleted: it yields nothing and cannot be hunted until it recovers.")]
    [UnityEngine.Range(0f, 1f)] public float depletedBelow = 0.2f;

    [UnityEngine.Header("Migration between neighbouring Macro Biomes")]
    [UnityEngine.Tooltip("Share of a population above half what its land holds that disperses each Echo, by ranging (settled ones never leave).")]
    public float territorialDispersal = 0.05f, roamingDispersal = 0.1f, nomadicDispersal = 0.2f, expandingDispersal = 0.2f;
    [UnityEngine.Tooltip("A population this far toward what its land holds founds a den where it has none.")]
    [UnityEngine.Range(0f, 1f)] public float denAt = 0.35f;
    [UnityEngine.Tooltip("Fewer groups than this and the species is gone from that Macro Biome.")]
    public float vanishBelow = 0.05f;
}

/// <summary>
/// The Macro Biomes' living creatures (vault: Arcanorian Ecology.md, "Where Creatures Live"), with no scene state (tested in <c>EcologyTests</c>).
/// Populations live per Macro Biome, keyed by slot (ARCHITECTURE.md, "World scales"), and change once an Echo:
///
/// - Habitat: the cells of a Macro Biome's range (its own and the intersection ground nearest it) where one of the
///   species' dens could stand (<see cref="WorldResources.Fits"/> in habitat mode). Recomputed each Age.
/// - Capacity: habitat cells times groups per cell (by size) times each cell's quality: settlements and held land
///   press on it (harder on species that flee whoever presses it, by their behavior toward them: <see cref="WorldBehavior"/>), and Pure Light species need Coherence (vault: Pure Light.md,
///   the more Pure Light, the more a lineage depends on precise Coherence). A predator's land holds less when its prey
///   is scarce. E10: each lineage's harmonic niche says where it finds what its Pure Light needs (Coherence, Dissonance,
///   or the leylines), Vibrational Fallout wears Pure Light down, and commensal species live off people's land instead.
/// - Growth: logistic at the species' pace (births crowd into a species' breeding Echo); predators take a share of their prey.
/// - Vectors (E10): species that carry sickness near your settlements feed Disease Burden (<see cref="VectorPressure"/>).
/// - Migration: above half its capacity a population disperses into neighbouring Macro Biomes with room, faster for
///   nomadic and expanding subgroups, never for settled ones; overflow beyond capacity moves or dies.
/// - Dens: Fauna resource sites are where a population shows. Their yields and harvests follow its abundance; below
///   <see cref="EcologySettings.depletedBelow"/> they are depleted; a population that has spread founds new dens.
/// Eleos Blooms live by their vigor (<see cref="WorldResources"/>), not here. None of this concerns the Atonalis.
/// </summary>
public static class WorldEcology
{
    /// <summary>From Renewal, a small share of sprite offspring settles into slime bodies on enclave-tended land.
    /// Parents retain their ecotype. Existing populations are the saved progress; nothing runs on load.</summary>
    public static void Entrain(WorldMap map, WorldGenSettings settings, int age, float[] pressure)
    {
        if (age < 1 || map == null || settings?.ecology == null) return;
        var growers = map.Enclaves.Where(e => e.family == EnclaveFamily.Agromagical).ToList();
        if (growers.Count == 0) return;
        foreach (var child in settings.species.Where(s => !string.IsNullOrEmpty(s.entrainedFrom)))
        {
            var parents = map.Populations.Where(p => p.species == child.entrainedFrom && p.groups > 0f).ToList();
            if (parents.Count == 0) continue;
            var habitat = Habitat(map, settings, child.id, age);
            foreach (var parent in parents)
            {
                if (!habitat.TryGetValue(parent.slot, out var cells) ||
                    !cells.Any(c => growers.Any(e => HexCoord.Distance(e.coord, map[c].coord) <= 6))) continue;
                float capacity = Capacity(map, settings.ecology, child, cells, pressure);
                var offspring = Find(map, parent.slot, child.id);
                float born = Math.Min(parent.groups * 0.05f, Math.Max(0f, capacity - (offspring?.groups ?? 0f)));
                if (born <= 0f) continue;
                if (offspring == null)
                {
                    offspring = new Population { slot = parent.slot, species = child.id, capacity = capacity };
                    map.Populations.Add(offspring);
                }
                offspring.groups += born;
                parent.groups -= born;
            }
        }
    }

    // ===== HABITAT =====

    /// <summary>The Fauna site specs that are dens of <paramref name="species"/>.</summary>
    public static IEnumerable<ResourceSiteSpec> DensOf(WorldGenSettings settings, string species) =>
        (settings?.resourceSites ?? new List<ResourceSiteSpec>()).Where(s => s != null && s.kind == ResourceKind.Fauna &&
            string.Equals(s.species, species, StringComparison.OrdinalIgnoreCase));

    /// <summary>Each species' habitat cells by Macro Biome slot, cached on the map for an Age.</summary>
    public static Dictionary<int, List<int>> Habitat(WorldMap map, WorldGenSettings settings, string species, int age)
    {
        if (map.habitat == null || map.habitatAge != age)
        {
            map.habitat = new Dictionary<string, Dictionary<int, List<int>>>(StringComparer.OrdinalIgnoreCase);
            map.habitatAge = age;
        }
        if (map.habitat.TryGetValue(species, out var cached)) return cached;
        var specs = DensOf(settings, species).Where(s => s.minAge <= age).ToList();
        var bySlot = new Dictionary<int, List<int>>();
        foreach (var t in map.Tiles)
        {
            if (t.habitatSlot < 0) continue;
            // A den's own cells are always habitat; other cells where a den of the species could stand.
            bool den = t.resourceSite >= 0 && t.resourceSite < map.ResourceSites.Count &&
                specs.Any(s => string.Equals(s.id, map.ResourceSites[t.resourceSite].spec, StringComparison.OrdinalIgnoreCase));
            if (!den && !specs.Any(s => WorldResources.Fits(map, settings, t, s, false, habitat: true))) continue;
            if (!bySlot.TryGetValue(t.habitatSlot, out var list)) bySlot[t.habitatSlot] = list = new List<int>();
            list.Add(t.index);
        }
        map.habitat[species] = bySlot;
        return bySlot;
    }

    public static float PerCell(EcologySettings e, CreatureSize size)
    {
        switch (size)
        {
            case CreatureSize.Small: return e.smallPerCell;
            case CreatureSize.Large: return e.largePerCell;
            case CreatureSize.Gargantuan: return e.gargantuanPerCell;
            default: return e.mediumPerCell;
        }
    }

    public static float Growth(EcologySettings e, ReproductionPace pace)
    {
        switch (pace)
        {
            case ReproductionPace.VerySlow: return e.verySlowGrowth;
            case ReproductionPace.Slow: return e.slowGrowth;
            case ReproductionPace.Fast: return e.fastGrowth;
            default: return e.mediumGrowth;
        }
    }

    public static float Dispersal(EcologySettings e, CreatureRanging ranging)
    {
        switch (ranging)
        {
            case CreatureRanging.Territorial: return e.territorialDispersal;
            case CreatureRanging.Roaming: return e.roamingDispersal;
            case CreatureRanging.Nomadic: return e.nomadicDispersal;
            case CreatureRanging.Expanding: return e.expandingDispersal;
            default: return 0f;
        }
    }

    /// <summary>How much people press on each cell (0 none): settlements, fading with distance, and held ground.</summary>
    public static float[] Pressure(WorldMap map, EcologySettings e) => Pressure(map, e, out _);

    /// <summary>The same, with who presses each cell (<paramref name="by"/>: the holder, or you near your settlements; null for no one).</summary>
    public static float[] Pressure(WorldMap map, EcologySettings e, out string[] by)
    {
        var pressure = new float[map.Count];
        by = new string[map.Count];
        int reach = Math.Max(0, e.settlementReach);
        foreach (var s in map.Settlements)
            foreach (var c in HexCoord.Spiral(s.coord, reach))
            {
                var t = map.Get(c);
                if (t == null) continue;
                float p = e.settlementPressure * (1f - HexCoord.Distance(s.coord, c) / (float)(reach + 1));
                pressure[t.index] = Math.Max(pressure[t.index], p);
                if (p > 0f) by[t.index] = WorldAuthority.Player;
            }
        foreach (var t in map.Tiles)
            if (Held(t))
            {
                pressure[t.index] += e.heldPressure;
                by[t.index] = WorldBehavior.Key(t.authorityId);
            }
        return pressure;
    }

    /// <summary>A cell's quality for a species (0-1): people's pressure (by how the species meets people) and Pure Light fragility.</summary>
    public static float Quality(EcologySettings e, SpeciesSpec species, WorldTile t, float pressure) =>
        Quality(e, species, t, pressure, WorldBehavior.Nature(species));

    /// <summary>
    /// The same, with the response the species shows whoever presses the cell (<see cref="WorldBehavior"/>): one that lets
    /// them near minds them as little as the unmoved. A commensal species (E10) lives off the pressure instead, the more
    /// so the more it depends on people. Its harmonic niche says where it finds what its Pure Light needs
    /// (<see cref="Attunement"/>), and Vibrational Fallout wears Pure Light down (Discordant lineages aside).
    /// </summary>
    public static float Quality(EcologySettings e, SpeciesSpec species, WorldTile t, float pressure, ThreatResponse response)
    {
        if (species.unmerged && !WorldResources.CoherentRefuge(t)) return 0f;
        float weight = response == ThreatResponse.FleesOnSight || response == ThreatResponse.Hides || response == ThreatResponse.FleesWhenThreatened
            ? e.fleeingPressure : response == ThreatResponse.Unmoved || response == ThreatResponse.Tolerates ? e.unmovedPressure : 1f;
        float quality = Clamp01(1f - pressure * weight);
        float commensal = Clamp01(species.commensal);
        if (commensal > 0f)
            quality = (1f - commensal) * quality + commensal * Math.Max(1f, e.commensalCap) * Math.Min(1f, pressure / Math.Max(0.01f, e.settlementPressure));
        float light = WorldRhythm.PureLight(species), need = light * e.pureLightCoherence, have = Attunement(e, species, t);
        if (have < need) quality *= Clamp01(1f - (need - have) / Math.Max(0.01f, e.coherenceMargin));
        if (species.niche == HarmonicNiche.Leyline && !OnLeyline(e, t)) quality *= Clamp01(e.offLeyline);
        if (species.niche != HarmonicNiche.Discordant && t.fallout > 0f) quality *= Clamp01(1f - t.fallout * light * Math.Max(0f, e.falloutHarm));
        return quality;
    }

    /// <summary>
    /// What a cell offers a species' Pure Light (E10): Coherence for most lineages; for Discordant ones the torn Loom,
    /// Dissonance plus Vibrational Fallout (Dead-zone lineages live where it broke). Ritual Sevenths add Lunehymn on silver
    /// water to it (<see cref="WorldRhythm.Attune"/>).
    /// </summary>
    public static float Attunement(EcologySettings e, SpeciesSpec species, WorldTile t) =>
        species != null && species.niche == HarmonicNiche.Discordant ? Clamp01(t.dissonance + t.fallout) : t.coherence;

    /// <summary>
    /// What the thriving blooms a species is drawn to (<see cref="WorldResources.BloomGround"/>) add to what its range
    /// holds: 1 with none, up to 1 + <see cref="EcologySettings.bloomDraw"/> once <see cref="EcologySettings.bloomCellsFull"/>
    /// cells' worth of bloom ground lie within <paramref name="cells"/>.
    /// </summary>
    public static float BloomGain(EcologySettings e, Dictionary<int, float> ground, List<int> cells)
    {
        if (e == null || ground == null || ground.Count == 0 || cells == null || e.bloomDraw <= 0f) return 1f;
        float sum = 0f;
        foreach (int c in cells)
            if (ground.TryGetValue(c, out float f)) sum += f;
        return 1f + e.bloomDraw * Math.Min(1f, sum / Math.Max(0.01f, e.bloomCellsFull));
    }

    /// <summary>On a leyline or silver water, or within <see cref="EcologySettings.leylineReach"/> of a silver river.</summary>
    public static bool OnLeyline(EcologySettings e, WorldTile t) =>
        t != null && (t.leylines != 0 || t.silver || t.silverRiverSteps <= Math.Max(0, e?.leylineReach ?? 1));

    /// <summary>
    /// What a Macro Biome's land holds of a species (groups), before its prey is counted. With <paramref name="by"/> and
    /// <paramref name="toward"/>, each cell's pressure is weighed by the species' behavior toward whoever presses it.
    /// </summary>
    public static float Capacity(WorldMap map, EcologySettings e, SpeciesSpec species, List<int> cells, float[] pressure,
        string[] by = null, Func<string, ThreatResponse> toward = null)
    {
        if (cells == null) return 0f;
        float perCell = PerCell(e, species.size), total = 0f;
        var nature = WorldBehavior.Nature(species);
        foreach (int i in cells)
        {
            var response = by != null && toward != null && by[i] != null ? toward(by[i]) : nature;
            total += perCell * Quality(e, species, map[i], pressure[i], response);
        }
        return total;
    }

    // ===== THE ECHO =====

    /// <summary>
    /// The creatures' Echo (after the blooms', <see cref="WorldResources.GrowEcho"/>): populations are founded where their
    /// first dens stand, capacities follow the land, populations grow, prey is taken, populations spread, and new dens
    /// appear where they have spread. <paramref name="season"/> is the Echo just lived (1-4; 0 none): its rhythm weighs
    /// births, decay, spreading and predation (<see cref="WorldRhythm"/>). Deterministic: the same map and Echo count give the same result.
    /// </summary>
    public static List<EcologyChange> Tick(WorldMap map, WorldGenSettings settings, int age, int echo, int season = 0)
    {
        var changes = new List<EcologyChange>();
        var e = settings?.ecology;
        if (map == null || e == null || settings.species == null) return changes;
        var pops = map.Populations;
        var pressure = Pressure(map, e, out var by);
        // The Echo just lived (WorldRhythm): births, decay, spreading and predation follow its theme.
        var lived = WorldRhythm.Of(settings, season);
        float predationPace = Math.Max(0f, lived?.predation ?? 1f), dispersalPace = Math.Max(0f, lived?.dispersal ?? 1f);
        // Each species' behavior toward whoever presses its land (WorldBehavior), read once this Echo.
        var toward = new Dictionary<string, Func<string, ThreatResponse>>(StringComparer.OrdinalIgnoreCase);
        Func<SpeciesSpec, Func<string, ThreatResponse>> responses = s =>
            toward.TryGetValue(s.id, out var f) ? f : toward[s.id] = WorldBehavior.Responses(map, settings, s);
        var before =pops.ToDictionary(p => Key(p.slot, p.species), p => Abundance(p, e));
        // What the land holds of a species, and more where the Eleos Blooms it is drawn to thrive (the food web).
        var bloomGround = new Dictionary<string, Dictionary<int, float>>(StringComparer.OrdinalIgnoreCase);
        List<(ResourceSiteSpec, List<(int, float)>)> thriving = null;
        var gains = new Dictionary<(string, int), float>();
        float Holds(SpeciesSpec species, int slot, List<int> cells)
        {
            float land = Capacity(map, e, species, cells, pressure, by, responses(species));
            if (land <= 0f || species.blooms == null || species.blooms.Count == 0) return land;
            if (!gains.TryGetValue((species.id, slot), out float gain))
            {
                if (!bloomGround.TryGetValue(species.id, out var ground))
                    bloomGround[species.id] = ground = WorldResources.BloomGround(thriving ??= WorldResources.ThrivingBlooms(map, settings), species);
                gains[(species.id, slot)] = gain = BloomGain(e, ground, cells);
            }
            return land * gain;
        }

        // Founding: every den of a species starts its Macro Biome's population.
        foreach (var site in map.ResourceSites)
        {
            var species = SpeciesAt(settings, site);
            if (species == null) continue;
            int slot = map[site.center].habitatSlot;
            if (slot < 0 || Find(map, slot, species.id) != null) continue;
            var cells = Habitat(map, settings, species.id, age);
            cells.TryGetValue(slot, out var list);
            float capacity = Holds(species, slot, list);
            pops.Add(new Population { slot = slot, species = species.id, groups = capacity * e.startFill, capacity = capacity, last = capacity * e.startFill });
        }
        Entrain(map, settings, age, pressure);
        pops.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : string.CompareOrdinal(a.species, b.species));

        // Capacity: the land, then the prey.
        foreach (var p in pops)
        {
            var species = settings.Species(p.species);
            if (species == null) continue;
            Habitat(map, settings, species.id, age).TryGetValue(p.slot, out var list);
            p.capacity = Holds(species, p.slot, list);
        }
        var landCapacity = pops.ToDictionary(p => p, p => p.capacity);
        foreach (var p in pops)
        {
            var species = settings.Species(p.species);
            if (species?.prey == null || species.prey.Count == 0) continue;
            float preyGroups = 0f, preyCapacity = 0f;
            foreach (var q in pops.Where(q => q.slot == p.slot && species.prey.Contains(q.species, StringComparer.OrdinalIgnoreCase)))
            {
                preyGroups += q.groups;
                preyCapacity += landCapacity[q];
            }
            float prey = preyCapacity > 0.0001f ? preyGroups / preyCapacity : 0f;
            p.capacity *= Math.Max(e.preyFloor, Math.Min(1f, prey));
        }

        // Growth and predation.
        var predation = new Dictionary<Population, float>();
        foreach (var p in pops)
        {
            var species = settings.Species(p.species);
            if (species?.prey == null || p.capacity <= 0.0001f) continue;
            float strength = Math.Min(1f, p.groups / p.capacity);
            foreach (var q in pops.Where(q => q.slot == p.slot && species.prey.Contains(q.species, StringComparer.OrdinalIgnoreCase)))
                predation[q] = (predation.TryGetValue(q, out float had) ? had : 0f) + e.predation * predationPace * strength;
        }
        foreach (var p in pops)
        {
            var species = settings.Species(p.species);
            p.last = p.groups;
            if (species == null) continue;
            float r = Growth(e, CreatureTaxonomy.PaceOf(species)) * WorldRhythm.Births(settings, species, season);
            float grown = p.capacity > 0.0001f ? r * p.groups * (1f - p.groups / p.capacity) : -p.groups;
            float eaten = predation.TryGetValue(p, out float share) ? Math.Min(1f, share) * p.groups : 0f;
            p.groups = Math.Max(0f, p.groups + grown - eaten);
        }

        // Migration into neighbouring Macro Biomes with room; what finds no room is lost.
        var moving = new List<(Population from, float groups)>();
        foreach (var p in pops)
        {
            var species = settings.Species(p.species);
            if (species == null) continue;
            var ranging = CreatureTaxonomy.Profile(species.diet, species.subgroup)?.ranging ?? CreatureRanging.Roaming;
            float overflow = Math.Max(0f, p.groups - p.capacity);
            float disperse = p.groups > 0.5f * p.capacity ? Math.Min(1f, Dispersal(e, ranging) * dispersalPace) * (p.groups - overflow) : 0f;
            if (ranging == CreatureRanging.Settled) { p.groups -= overflow; continue; }
            if (overflow + disperse > 0.0001f) moving.Add((p, overflow + disperse));
        }
        foreach (var (from, groups) in moving)
        {
            var species = settings.Species(from.species);
            var habitat = Habitat(map, settings, species.id, age);
            var rooms = new List<(int slot, float room)>();
            foreach (int n in Neighbours(map, from.slot))
            {
                if (!habitat.TryGetValue(n, out var cells)) continue;
                var there = Find(map, n, species.id);
                float capacity = there?.capacity ?? Holds(species, n, cells);
                float room = capacity - (there?.groups ?? 0f);
                if (room > 0.0001f) rooms.Add((n, room));
            }
            float totalRoom = rooms.Sum(r => r.room), moved = Math.Min(groups, totalRoom);
            foreach (var (slot, room) in rooms)
            {
                float share = moved * room / totalRoom;
                var there = Find(map, slot, species.id);
                if (there == null)
                {
                    habitat.TryGetValue(slot, out var cells);
                    there = new Population { slot = slot, species = species.id, capacity = Holds(species, slot, cells) };
                    pops.Add(there);
                }
                there.groups += share;
            }
            // Overflow that found no room dies; dispersers that found none stay.
            float overflow = Math.Max(0f, from.groups - from.capacity);
            from.groups -= moved + Math.Max(0f, overflow - moved);
            from.groups = Math.Max(0f, from.groups);
        }

        // Vanishing, dens and the notices (each Macro Biome's den of each species found once, not per population).
        var dens = Dens(map, settings);
        // A den is sought among its Macro Biome's own tiles, gathered once this Echo (a failed search is tried again the
        // next Echo, so it must stay cheap), with the bloom ground already gathered.
        Dictionary<int, List<WorldTile>> slotTiles = null;
        List<WorldTile> TilesOf(int slot)
        {
            if (slotTiles == null)
            {
                slotTiles = new Dictionary<int, List<WorldTile>>();
                foreach (var t in map.Tiles)
                {
                    if (!slotTiles.TryGetValue(t.habitatSlot, out var list)) slotTiles[t.habitatSlot] = list = new List<WorldTile>();
                    list.Add(t);
                }
            }
            return slotTiles.TryGetValue(slot, out var tiles) ? tiles : new List<WorldTile>();
        }
        Dictionary<int, float> DrawnTo(SpeciesSpec species) => bloomGround.TryGetValue(species.id, out var ground) ? ground
            : bloomGround[species.id] = WorldResources.BloomGround(thriving ??= WorldResources.ThrivingBlooms(map, settings), species);
        foreach (var p in pops.OrderBy(p => p.slot).ThenBy(p => p.species, StringComparer.Ordinal).ToList())
        {
            var species = settings.Species(p.species);
            if (species == null) continue;
            if (p.groups < e.vanishBelow && p.groups > 0f)
            {
                p.groups = 0f;
                dens.TryGetValue(Key(p.slot, p.species), out var den);
                if (den != null) changes.Add(new EcologyChange { kind = EcologyChangeKind.Vanished, population = p, site = den });
                continue;
            }
            float was = before.TryGetValue(Key(p.slot, p.species), out float a) ? a : 1f, now = Abundance(p, e);
            dens.TryGetValue(Key(p.slot, p.species), out var home);
            if (home == null && p.capacity > 0.0001f && p.groups >= e.denAt * p.capacity)
            {
                var spec = DensOf(settings, species.id).FirstOrDefault(s => s.minAge <= age);
                var placed = spec != null ? WorldResources.PlaceDen(map, settings, spec, age, p.slot, $"den:{species.id}:{p.slot}:{echo}",
                    TilesOf(p.slot), DrawnTo(species)) : null;
                if (placed != null)
                {
                    dens[Key(p.slot, p.species)] = placed;
                    changes.Add(new EcologyChange { kind = EcologyChangeKind.Arrived, population = p, site = placed });
                }
            }
            else if (home != null && was >= e.depletedBelow && now < e.depletedBelow)
                changes.Add(new EcologyChange { kind = EcologyChangeKind.Depleted, population = p, site = home });
            else if (home != null && was < e.depletedBelow && now >= e.depletedBelow)
                changes.Add(new EcologyChange { kind = EcologyChangeKind.Recovered, population = p, site = home });
        }
        pops.RemoveAll(p => p.groups <= 0f && !dens.ContainsKey(Key(p.slot, p.species)));
        return changes;
    }

    // ===== HUNTING AND READING =====

    /// <summary>
    /// A hunt at a den: the harvest takes <see cref="EcologySettings.huntTake"/> of what the land holds (times the party's
    /// multiplier) from its Macro Biome's population. <paramref name="authority"/> is who hunted: the species remembers it (<see cref="WorldBehavior.Hunted"/>).
    /// Returns the groups taken.
    /// </summary>
    public static float Hunt(WorldMap map, WorldGenSettings settings, ResourceSite site, string authority, float multiplier)
    {
        var e = settings?.ecology;
        var species = SpeciesAt(settings, site);
        if (e == null || species == null) return 0f;
        var p = Find(map, map[site.center].habitatSlot, species.id);
        if (p == null) return 0f;
        float take = Math.Min(p.groups, e.huntTake * Math.Max(0f, multiplier) * p.capacity);
        p.groups -= take;
        WorldBehavior.Hunted(map, settings, species.id, authority, multiplier);
        return take;
    }

    /// <summary>The species living at a den (a Fauna site that names one), or null.</summary>
    public static SpeciesSpec SpeciesAt(WorldGenSettings settings, ResourceSite site)
    {
        if (site == null || site.kind != ResourceKind.Fauna || site.planted) return null;
        return settings?.Species(settings.ResourceSite(site.spec)?.species);
    }

    /// <summary>The population a den shows, or null (before its first Echo, or not a den).</summary>
    public static Population PopulationAt(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        var species = SpeciesAt(settings, site);
        return species == null ? null : Find(map, map[site.center].habitatSlot, species.id);
    }

    /// <summary>A den's abundance: its population over what its land holds (1 before the first Echo, and for every other site).</summary>
    public static float AbundanceAt(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        var p = PopulationAt(map, settings, site);
        return p == null ? 1f : Abundance(p, settings.ecology);
    }

    /// <summary>
    /// How much sickness the creatures carry into your settlements (E10), 0-1, for Disease Burden (PopulationHealth's
    /// vectorPressure): every vector species' groups living within <see cref="EcologySettings.vectorReach"/> of one of your
    /// settlements (its population spread over its habitat by each cell's quality), times how strongly it carries sickness, over
    /// <see cref="EcologySettings.vectorFull"/> per settlement (how infested your settlements are, on average). Rats in the
    /// granaries count; a hunted-down colony counts less.
    /// </summary>
    public static float VectorPressure(WorldMap map, WorldGenSettings settings, int age) =>
        Clamp01(VectorSources(map, settings, age).Sum(v => v.pressure));

    /// <summary>Each vector species' share of <see cref="VectorPressure"/> (before the clamp), largest first.</summary>
    public static List<(SpeciesSpec species, float pressure)> VectorSources(WorldMap map, WorldGenSettings settings, int age)
    {
        var result = new List<(SpeciesSpec species, float pressure)>();
        var e = settings?.ecology;
        if (map == null || e == null || settings.species == null || map.Settlements.Count == 0) return result;
        int reach = Math.Max(0, e.vectorReach);
        var near = new HashSet<int>();
        foreach (var s in map.Settlements)
            foreach (var c in HexCoord.Spiral(s.coord, reach))
                if (map.Get(c) is WorldTile t) near.Add(t.index);
        var pressure = Pressure(map, e);
        foreach (var species in settings.species.Where(s => s != null && s.vector > 0f))
        {
            float groups = 0f;
            var habitat = Habitat(map, settings, species.id, age);
            foreach (var p in map.Populations.Where(p => p.groups > 0f && string.Equals(p.species, species.id, StringComparison.OrdinalIgnoreCase)))
            {
                if (!habitat.TryGetValue(p.slot, out var cells) || cells.Count == 0) continue;
                // The groups live where the land holds them best: rats crowd into the granaries.
                float all = 0f, close = 0f;
                foreach (int i in cells)
                {
                    float q = Quality(e, species, map[i], pressure[i]);
                    all += q;
                    if (near.Contains(i)) close += q;
                }
                if (all > 0.0001f) groups += p.groups * close / all;
            }
            if (groups > 0f) result.Add((species, Clamp01(species.vector) * groups / (Math.Max(0.01f, e.vectorFull) * map.Settlements.Count)));
        }
        return result.OrderByDescending(v => v.pressure).ToList();
    }

    public static bool Depleted(WorldMap map, WorldGenSettings settings, ResourceSite site) =>
        settings?.ecology != null && PopulationAt(map, settings, site) != null && AbundanceAt(map, settings, site) < settings.ecology.depletedBelow;

    /// <summary>"thriving", "steady", "scarce", "depleted", or "gone".</summary>
    public static string AbundanceWord(Population p, EcologySettings e)
    {
        float a = Abundance(p, e);
        if (p.groups <= 0f) return "gone";
        if (a < e.depletedBelow) return "depleted";
        return a >= 0.8f ? "thriving" : a >= 0.5f ? "steady" : "scarce";
    }

    /// <summary>"growing", "declining", or null when steady.</summary>
    public static string TrendWord(Population p)
    {
        float change = p.groups - p.last, scale = Math.Max(0.01f, Math.Max(p.groups, p.last));
        return change / scale > 0.02f ? "growing" : change / scale < -0.02f ? "declining" : null;
    }

    /// <summary>The Macro Biome in a slot, or null.</summary>
    public static MacroBiomeSpec MacroBiomeOf(WorldMap map, int slot) =>
        map.Placements.FirstOrDefault(p => p.slot != null && p.slot.index == slot)?.macroBiome;

    public static Population Find(WorldMap map, int slot, string species) =>
        map.Populations.FirstOrDefault(p => p.slot == slot && string.Equals(p.species, species, StringComparison.OrdinalIgnoreCase));

    // Every den by its Macro Biome and species (the first standing in the list).
    private static Dictionary<string, ResourceSite> Dens(WorldMap map, WorldGenSettings settings)
    {
        var dens = new Dictionary<string, ResourceSite>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in map.ResourceSites)
        {
            var species = SpeciesAt(settings, site);
            if (species == null) continue;
            string key = Key(map[site.center].habitatSlot, species.id);
            if (!dens.ContainsKey(key)) dens[key] = site;
        }
        return dens;
    }

    private static IEnumerable<int> Neighbours(WorldMap map, int slot) =>
        map.Stencil != null && slot >= 0 && slot < map.Stencil.Slots.Count ? map.Stencil.Slots[slot].neighbours : Enumerable.Empty<int>();

    private static float Abundance(Population p, EcologySettings e) => p.Abundance;

    private static string Key(int slot, string species) => slot + "|" + species;

    /// <summary>Land an authority holds (not the wilderness).</summary>
    public static bool Held(WorldTile t) => !string.IsNullOrEmpty(t.authorityId) && t.authorityId != WorldAuthority.Wilderness;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
