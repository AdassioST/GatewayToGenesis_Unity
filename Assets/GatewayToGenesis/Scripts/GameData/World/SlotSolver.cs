using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One slot's Macro Biome and the direction its uphill side faces.</summary>
public class SlotPlacement
{
    public StencilSlot slot;
    /// <summary>Null when the quadrant's catalog could not fill it (an authoring error): the intersections' ground fills it.</summary>
    public MacroBiomeSpec macroBiome;
    /// <summary>Sixths of a turn from east, counter-clockwise.</summary>
    public int orientation;

    public override string ToString() => $"{slot.id}: {(macroBiome != null ? macroBiome.id : "(none)")} facing {WorldGenerator.DirectionName(orientation)}";
}

/// <summary>What the slot solver decided and why.</summary>
public class SlotSolution
{
    public readonly List<SlotPlacement> placements = new List<SlotPlacement>();
    /// <summary>Catalog problems an author must fix (missing, unknown or too small catalogs; impossible requirements).</summary>
    public readonly List<string> errors = new List<string>();
    /// <summary>Constraints relaxed to reach a layout (logged, never silent).</summary>
    public readonly List<string> repairs = new List<string>();
    /// <summary>Search nodes visited (bounded).</summary>
    public int nodes;

    public SlotPlacement For(int slotIndex) => placements.Find(p => p.slot.index == slotIndex);
}

/// <summary>
/// Roadmap WG04: assigns each quadrant's biome catalog to its slots and picks each biome's orientation, with a seeded
/// constraint search (no scene state; tested in <c>WorldGenerationTests</c>).
///
/// Hard constraints: a biome appears at most once in its quadrant (unless the catalog allows repeats); its slot has
/// every tag it requires (north, coast, core...); its orientation is one it allows; and the heights of two slots
/// that only an intersection separates meet within <see cref="MaxSeamStep"/> where they face each other. The most constrained slot
/// is placed first, candidates are shuffled by the seed, and the search backtracks within a node budget. When no
/// layout exists the solver relaxes the seam heights, then the tag requirements, and reports each relaxation; a
/// catalog that cannot fill its slots is an authoring error, never a silently dropped biome.
/// </summary>
public static class SlotSolver
{
    public const float MaxSeamStep = 0.3f;
    public const int NodeBudget = 40000;

    private class Candidate
    {
        public MacroBiomeSpec macroBiome;
        public int orientation;
        public double score;
    }

    private class Problem
    {
        public WorldStencil stencil;
        public List<StencilSlot> slots;
        public Dictionary<int, List<Candidate>> candidates = new Dictionary<int, List<Candidate>>();
        public Dictionary<string, QuadrantSpec> quadrants = new Dictionary<string, QuadrantSpec>(StringComparer.OrdinalIgnoreCase);
        public bool checkSeams = true, checkTags = true;
        public int nodes;
    }

    public static SlotSolution Solve(WorldStencil stencil, WorldGenSettings settings, int seed)
    {
        var solution = new SlotSolution();
        var rng = new Random(WorldNoise.Stream(seed, "slots"));
        var problem = new Problem { stencil = stencil, slots = stencil.Slots };

        // The biomes each quadrant may use this world: its catalog, drawn down to its slot count when larger.
        var pools = new Dictionary<string, List<MacroBiomeSpec>>(StringComparer.OrdinalIgnoreCase);
        foreach (string quadrantId in stencil.QuadrantIds)
        {
            var quadrant = settings.Quadrant(quadrantId);
            int slotCount = stencil.SlotsOf(quadrantId).Count();
            if (quadrant == null)
            {
                solution.errors.Add($"Quadrant {quadrantId} is in the stencil but has no QuadrantSpec: its {slotCount} slot(s) get the intersections' ground.");
                pools[quadrantId] = new List<MacroBiomeSpec>();
                continue;
            }
            problem.quadrants[quadrantId] = quadrant;
            var biomes = new List<MacroBiomeSpec>();
            foreach (string id in quadrant.macroBiomes)
            {
                var biome = settings.MacroBiome(id);
                if (biome == null) solution.errors.Add($"Quadrant {quadrantId} names unknown Macro Biome '{id}'.");
                else if (!biomes.Contains(biome)) biomes.Add(biome);
            }
            if (biomes.Count == 0) solution.errors.Add($"Quadrant {quadrantId} has an empty Macro Biome catalog: its {slotCount} slot(s) get the intersections' ground.");
            else if (biomes.Count < slotCount && !quadrant.allowRepeats)
            {
                solution.errors.Add($"Quadrant {quadrantId} has {biomes.Count} Macro Biome(s) for {slotCount} slots and does not allow repeats: biomes are repeated.");
                solution.repairs.Add($"{quadrantId}: repeats allowed to fill {slotCount} slots with {biomes.Count} Macro Biome(s).");
            }
            else if (biomes.Count > slotCount)
            {
                Shuffle(biomes, rng);
                biomes = biomes.Take(slotCount).ToList();
            }
            pools[quadrantId] = biomes;
        }

        foreach (var slot in problem.slots)
        {
            var pool = pools[slot.quadrant];
            var list = new List<Candidate>();
            foreach (var biome in pool)
            {
                var orientations = biome.orientations != null && biome.orientations.Count > 0 ? biome.orientations.Where(o => o >= 0 && o < 6).Distinct().ToList() : Enumerable.Range(0, 6).ToList();
                double preference = biome.prefers.Count(t => slot.tags.Contains(t));
                foreach (int o in orientations) list.Add(new Candidate { macroBiome = biome, orientation = o, score = preference + rng.NextDouble() * 0.9 });
            }
            // Seeded order, best preference first.
            problem.candidates[slot.index] = list.OrderByDescending(c => c.score).ToList();
        }

        var assigned = new Dictionary<int, Candidate>();
        bool solved = Search(problem, assigned);
        if (!solved)
        {
            problem.checkSeams = false;
            assigned.Clear();
            solved = Search(problem, assigned);
            if (solved) solution.repairs.Add($"No layout met every seam height (step {MaxSeamStep}); seam heights were relaxed and the intersections will carry the slopes.");
        }
        if (!solved)
        {
            problem.checkTags = false;
            assigned.Clear();
            solved = Search(problem, assigned);
            if (solved) solution.repairs.Add("No layout met every slot tag requirement; tag requirements were relaxed.");
        }
        if (!solved)
        {
            solution.errors.Add("No biome layout exists even with relaxed constraints: each slot took its first candidate.");
            assigned.Clear();
            foreach (var slot in problem.slots)
            {
                var first = problem.candidates[slot.index].FirstOrDefault();
                if (first != null) assigned[slot.index] = first;
            }
        }
        solution.nodes = problem.nodes;

        foreach (var slot in problem.slots)
        {
            assigned.TryGetValue(slot.index, out var pick);
            solution.placements.Add(new SlotPlacement { slot = slot, macroBiome = pick?.macroBiome, orientation = pick?.orientation ?? 0 });
        }
        // Requirements no biome of the catalog can meet are authoring errors too.
        foreach (var slot in problem.slots)
        {
            var biome = solution.For(slot.index).macroBiome;
            if (biome == null) continue;
            var missing = biome.requires.Where(t => !slot.tags.Contains(t)).ToList();
            if (missing.Count > 0) solution.errors.Add($"{slot.id} holds {biome.id}, which requires {string.Join(", ", missing)} (the slot is {string.Join(", ", slot.tags)}).");
        }
        return solution;
    }

    private static bool Search(Problem problem, Dictionary<int, Candidate> assigned)
    {
        if (assigned.Count == problem.slots.Count) return true;
        if (problem.nodes > NodeBudget) return false;

        // Most constrained open slot first.
        StencilSlot next = null;
        List<Candidate> nextOptions = null;
        foreach (var slot in problem.slots)
        {
            if (assigned.ContainsKey(slot.index)) continue;
            var options = problem.candidates[slot.index].Where(c => Allowed(problem, slot, c, assigned)).ToList();
            if (problem.candidates[slot.index].Count == 0) options = new List<Candidate> { null }; // an empty catalog: nothing to place
            if (next == null || options.Count < nextOptions.Count)
            {
                next = slot;
                nextOptions = options;
                if (options.Count == 0) return false;
            }
        }
        foreach (var option in nextOptions)
        {
            problem.nodes++;
            assigned[next.index] = option;
            if (Search(problem, assigned)) return true;
            assigned.Remove(next.index);
            if (problem.nodes > NodeBudget) return false;
        }
        return false;
    }

    private static bool Allowed(Problem problem, StencilSlot slot, Candidate candidate, Dictionary<int, Candidate> assigned)
    {
        if (problem.checkTags && candidate.macroBiome.requires.Any(t => !slot.tags.Contains(t))) return false;
        problem.quadrants.TryGetValue(slot.quadrant, out var quadrant);
        bool repeats = quadrant == null || quadrant.allowRepeats || problem.candidates[slot.index].Select(c => c.macroBiome).Distinct().Count() < problem.stencil.SlotsOf(slot.quadrant).Count();
        if (!repeats)
        {
            foreach (var other in problem.stencil.SlotsOf(slot.quadrant))
            {
                if (other.index != slot.index && assigned.TryGetValue(other.index, out var taken) && taken != null && taken.macroBiome == candidate.macroBiome) return false;
            }
        }
        if (problem.checkSeams)
        {
            foreach (int n in slot.neighbours)
            {
                if (!assigned.TryGetValue(n, out var neighbour) || neighbour == null) continue;
                var other = problem.stencil.Slots[n];
                float here = EdgeHeight(candidate.macroBiome, candidate.orientation, slot, other);
                float there = EdgeHeight(neighbour.macroBiome, neighbour.orientation, other, slot);
                if (Math.Abs(here - there) > MaxSeamStep) return false;
            }
        }
        return true;
    }

    /// <summary>Height of <paramref name="biome"/> at the side of <paramref name="slot"/> that faces <paramref name="toward"/>.</summary>
    public static float EdgeHeight(MacroBiomeSpec biome, int orientation, StencilSlot slot, StencilSlot toward)
    {
        double dx = toward.centerCol - slot.centerCol, dy = slot.centerRow - toward.centerRow; // y grows north
        double facing = Math.Atan2(dy, dx), uphill = orientation * Math.PI / 3.0;
        return (float)(biome.elevation + 0.5 * biome.tilt * Math.Cos(facing - uphill));
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
