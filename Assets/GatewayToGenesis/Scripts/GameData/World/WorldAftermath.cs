using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A lineage that may settle a Macro Biome the aftermath emptied (vault: Pure Light.md, slime ecotypes and generational adaptation).</summary>
[Serializable]
public class AftermathLineage
{
    [UnityEngine.Tooltip("The species (an id from generation.species). It needs a den spec its habitat is read from; give that spec count 0 so it only arrives this way.")]
    public string species;
    [UnityEngine.Tooltip("It settles only where at least this share of its range in the Macro Biome carries Vibrational Fallout (Dead-zone lineages; 0: wherever its dens could stand).")]
    [UnityEngine.Range(0f, 1f)] public float minFalloutShare;
}

/// <summary>
/// How the creatures come through an Age's end (vault: Arcanorian Ecology.md, "The Aftermath of Creatures"). Every number is a proposal (Canon Gaps.md,
/// "Aftermath ecology").
/// </summary>
[Serializable]
public class AftermathSettings
{
    [UnityEngine.Header("The crash (times the ended crisis' severity)")]
    [UnityEngine.Tooltip("Share every lineage loses at severity 1, whatever its makeup.")]
    [UnityEngine.Range(0f, 1f)] public float baseLoss = 0.2f;
    [UnityEngine.Tooltip("Share lost per unit of Pure Light at severity 1 (vault: high Pure Light is fragility; high Auric Structure persists). A 90% Pure Light lineage at severity 0.5 loses 0.5 x (0.2 + 0.9 x this).")]
    [UnityEngine.Min(0f)] public float pureLightLoss = 1.2f;
    [UnityEngine.Tooltip("A population left with less than this share of itself is gone from its Macro Biome.")]
    [UnityEngine.Range(0f, 1f)] public float vanishBelow = 0.1f;

    [UnityEngine.Header("Scar spectra")]
    [UnityEngine.Tooltip("A lineage that lost at least this share of its numbers is scarred.")]
    [UnityEngine.Range(0f, 1f)] public float scarLoss = 0.5f;
    [UnityEngine.Tooltip("Share of its tempers a scarred lineage keeps across the Age (the others keep behavior.memoryKept): severe stress passes its history into offspring.")]
    [UnityEngine.Range(0f, 1f)] public float scarKept = 0.6f;

    [UnityEngine.Header("Resettling the emptied Macro Biomes")]
    [UnityEngine.Tooltip("A Macro Biome is emptied when its creatures fall below this share of what they were.")]
    [UnityEngine.Range(0f, 1f)] public float emptiedBelow = 0.3f;
    [UnityEngine.Tooltip("A new lineage starts at this share of what its land holds there (it grows from few; its den appears once it has).")]
    [UnityEngine.Range(0f, 1f)] public float resettleShare = 0.15f;
    [UnityEngine.Tooltip("New lineages that settle each emptied Macro Biome.")]
    [UnityEngine.Min(0)] public int lineagesPerBiome = 1;
    [UnityEngine.Tooltip("A cell carries Vibrational Fallout for a lineage's minFalloutShare at this much fallout.")]
    [UnityEngine.Range(0f, 1f)] public float falloutAt = 0.2f;
    public List<AftermathLineage> lineages = new List<AftermathLineage>();
}

/// <summary>What the aftermath did to the creatures (<see cref="WorldAftermath.Pass"/>), for the notices.</summary>
public class AftermathReport
{
    public float severity;
    /// <summary>Species by id: their groups before and after, over every Macro Biome.</summary>
    public Dictionary<string, (float before, float after)> totals = new Dictionary<string, (float before, float after)>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Populations gone from their Macro Biome (their records, groups now 0).</summary>
    public List<Population> vanished = new List<Population>();
    /// <summary>Populations whose land no longer holds them and that moved to neighbouring Macro Biomes (species, from, to).</summary>
    public List<(string species, int from, int to)> shifted = new List<(string species, int from, int to)>();
    /// <summary>New lineages settling emptied Macro Biomes.</summary>
    public List<Population> resettled = new List<Population>();
    /// <summary>Lineages scarred by what they lost (they keep more of their memory).</summary>
    public List<string> scarred = new List<string>();
}

/// <summary>
/// The Macro Biomes' creatures through an Age's end (vault: Arcanorian Ecology.md, "The Aftermath of Creatures"), with no scene state (tested in
/// <c>AftermathTests</c>). Once per Age, after the new Age's sites are placed (its habitat is read for the new Age):
///
/// - The crash: each population loses a share by the ended crisis' severity and by its Pure Light (vault: Pure
///   Light.md, high Pure Light is magic and fragility, high Auric Structure persistence). Below
///   <see cref="AftermathSettings.vanishBelow"/> it is gone from its Macro Biome.
/// - Ranges shift: a population whose Macro Biome no longer holds any of its habitat moves to the neighbouring Macro
///   Biomes that do (by their habitat), or dies.
/// - Resettling: a Macro Biome whose creatures fell below <see cref="AftermathSettings.emptiedBelow"/> of what they were
///   is settled by new lineages (<see cref="AftermathSettings.lineages"/>: ecotypes; Dead-zone lineages only where
///   Vibrational Fallout lies), from few. The parents never adapt; their descendants do (the ecology grows them).
/// - Scar spectra: every lineage's memory eases back toward its nature (<see cref="WorldBehavior.AgePassed"/>), but a
///   lineage that lost <see cref="AftermathSettings.scarLoss"/> of its numbers keeps more of it.
/// Rivers shifting and forests expanding by the crisis' outcome come later. None of this concerns the Atonalis.
/// </summary>
public static class WorldAftermath
{
    /// <summary>
    /// The creatures come through the end of an Age into Age <paramref name="age"/>; <paramref name="severity"/> is the
    /// ended crisis' (0-1; 0 without one: only the ranges shift and memories ease).
    /// </summary>
    public static AftermathReport Pass(WorldMap map, WorldGenSettings settings, int age, float severity)
    {
        var report = new AftermathReport { severity = Clamp01(severity) };
        var a = settings?.aftermath;
        var e = settings?.ecology;
        if (map == null || settings == null || a == null || e == null) return report;
        var slotBefore = new Dictionary<int, float>();
        foreach (var p in map.Populations.Where(p => p != null && p.groups > 0f))
        {
            slotBefore[p.slot] = (slotBefore.TryGetValue(p.slot, out float had) ? had : 0f) + p.groups;
            float before = report.totals.TryGetValue(p.species, out var was) ? was.before : 0f;
            report.totals[p.species] = (before + p.groups, 0f);
        }

        // The crash, by severity and Pure Light.
        foreach (var p in map.Populations.Where(p => p != null && p.groups > 0f).OrderBy(p => p.slot).ThenBy(p => p.species, StringComparer.Ordinal))
        {
            var species = settings.Species(p.species);
            if (species == null) continue;
            float survival = Survival(a, species, report.severity);
            if (survival < a.vanishBelow)
            {
                p.groups = 0f;
                report.vanished.Add(p);
            }
            else p.groups *= survival;
            p.last = p.groups;
        }

        // Ranges shift toward the habitat that is left.
        foreach (var p in map.Populations.Where(p => p != null && p.groups > 0f).OrderBy(p => p.slot).ThenBy(p => p.species, StringComparer.Ordinal).ToList())
        {
            var habitat = WorldEcology.Habitat(map, settings, p.species, age);
            if (habitat.TryGetValue(p.slot, out var cells) && cells.Count > 0) continue;
            var rooms = Neighbours(map, p.slot).Where(n => habitat.TryGetValue(n, out var c) && c.Count > 0).ToList();
            float groups = p.groups;
            p.groups = 0f;
            p.last = 0f;
            if (rooms.Count == 0) { report.vanished.Add(p); continue; }
            float total = rooms.Sum(n => (float)habitat[n].Count);
            foreach (int n in rooms)
            {
                var there = WorldEcology.Find(map, n, p.species);
                if (there == null) map.Populations.Add(there = new Population { slot = n, species = p.species });
                there.groups += groups * habitat[n].Count / total;
                there.last = there.groups;
                report.shifted.Add((p.species, p.slot, n));
            }
        }

        // The emptied Macro Biomes are settled by new lineages.
        if (a.lineages != null && a.lineages.Count > 0 && a.lineagesPerBiome > 0)
        {
            var pressure = WorldEcology.Pressure(map, e);
            foreach (var pair in slotBefore.OrderBy(x => x.Key))
            {
                float now = map.Populations.Where(p => p.slot == pair.Key && p.groups > 0f).Sum(p => p.groups);
                if (now >= a.emptiedBelow * pair.Value) continue;
                foreach (var lineage in Candidates(map, settings, a, pair.Key, age).Take(a.lineagesPerBiome))
                {
                    var species = settings.Species(lineage.species);
                    var cells = WorldEcology.Habitat(map, settings, species.id, age)[pair.Key];
                    float capacity = WorldEcology.Capacity(map, e, species, cells, pressure);
                    if (capacity <= 0.0001f) continue;
                    var p = WorldEcology.Find(map, pair.Key, species.id);
                    if (p == null) map.Populations.Add(p = new Population { slot = pair.Key, species = species.id });
                    p.capacity = capacity;
                    p.groups = p.last = capacity * a.resettleShare;
                    report.resettled.Add(p);
                }
            }
        }

        // What each lineage came through with, and its scars.
        foreach (var id in report.totals.Keys.ToList())
        {
            float after = map.Populations.Where(p => string.Equals(p.species, id, StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Max(0f, p.groups));
            var t = report.totals[id];
            report.totals[id] = (t.before, after);
            if (t.before > 0f && after <= t.before * (1f - a.scarLoss)) report.scarred.Add(id);
        }
        var scarred = new HashSet<string>(report.scarred, StringComparer.OrdinalIgnoreCase);
        WorldBehavior.AgePassed(map, settings, id => scarred.Contains(id) ? a.scarKept : (float?)null);
        map.Populations.Sort((x, y) => x.slot != y.slot ? x.slot.CompareTo(y.slot) : string.CompareOrdinal(x.species, y.species));
        return report;
    }

    /// <summary>The share of a lineage that comes through a crisis of <paramref name="severity"/>.</summary>
    public static float Survival(AftermathSettings a, SpeciesSpec species, float severity)
    {
        float pureLight = 1f - Clamp01(species.structure);
        return Clamp01(1f - Clamp01(severity) * (a.baseLoss + a.pureLightLoss * pureLight));
    }

    /// <summary>
    /// The lineages that could settle an emptied Macro Biome, in the order they would: the ones fallout calls for first,
    /// then the rest in an order drawn from the world's seed, the Macro Biome and the Age. Each needs a den spec of its
    /// Age, habitat there, and not to live there already.
    /// </summary>
    public static List<AftermathLineage> Candidates(WorldMap map, WorldGenSettings settings, AftermathSettings a, int slot, int age)
    {
        var fit = new List<(AftermathLineage lineage, float order)>();
        foreach (var lineage in a.lineages.Where(l => l != null && !string.IsNullOrEmpty(l.species)))
        {
            var species = settings.Species(lineage.species);
            if (species == null || !WorldEcology.DensOf(settings, species.id).Any(d => d.minAge <= age)) continue;
            if (WorldEcology.Find(map, slot, species.id) is Population here && here.groups > 0f) continue;
            if (!WorldEcology.Habitat(map, settings, species.id, age).TryGetValue(slot, out var cells) || cells.Count == 0) continue;
            float fallout = cells.Count(i => map[i].fallout >= a.falloutAt) / (float)cells.Count;
            if (fallout < lineage.minFalloutShare) continue;
            float order = (lineage.minFalloutShare > 0f ? 0f : 1f) + (float)WorldNoise.Hash01(map.seed * 31 + Key(lineage.species), slot, age);
            fit.Add((lineage, order));
        }
        return fit.OrderBy(f => f.order).Select(f => f.lineage).ToList();
    }

    private static int Key(string id)
    {
        int key = 17;
        foreach (char c in id) key = key * 31 + c;
        return key;
    }

    private static IEnumerable<int> Neighbours(WorldMap map, int slot) =>
        map.Stencil != null && slot >= 0 && slot < map.Stencil.Slots.Count ? map.Stencil.Slots[slot].neighbours : Enumerable.Empty<int>();

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
