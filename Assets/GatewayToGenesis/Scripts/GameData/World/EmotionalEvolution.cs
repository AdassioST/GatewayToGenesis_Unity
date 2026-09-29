using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What an eater of feeling eats: a cocktail (<see cref="recipe"/>, notes in proportion) and how exactly it must be made
/// (<see cref="specificity"/>, 0 a generalist that takes any of its notes, 1 a purist that takes only the exact cocktail).
/// </summary>
[Serializable]
public class Palate
{
    public List<FeelingWeight> recipe = new List<FeelingWeight>();
    [UnityEngine.Range(0f, 1f)] public float specificity;

    public Palate Copy() => new Palate { recipe = recipe.Where(l => l != null).Select(l => new FeelingWeight { feeling = l.feeling, weight = l.weight }).ToList(), specificity = specificity };
}

/// <summary>What one moment's feeding gives an eater of <see cref="Palate"/> at a place.</summary>
public struct Feeding
{
    /// <summary>Exact servings of its cocktail the place pours, and every note of its recipe it finds, whatever the proportion.</summary>
    public float servings, loose;
    /// <summary>What it eats: servings for a purist, loose food for a generalist, a blend between.</summary>
    public float food;
    /// <summary>How exactly the place's mix matches its cocktail (0-1).</summary>
    public float fit;
    /// <summary>How well it grows (food over need, less for a niche palate: specialists grow less).</summary>
    public float growth;
    /// <summary>How potent what it gives is (1 ordinary; a niche palate fed its exact cocktail is superloaded, up to 1 + superload).</summary>
    public float potency;
    /// <summary>Growth and potency together: what evolution weighs.</summary>
    public float fitness;
}

/// <summary>How a lineage's palate changed at an Echo.</summary>
public enum LineageChange { Specialized, Generalized, Diverged, Reverted }

/// <summary>An evolutionary step of a lineage (saved with it, for its card and for other systems).</summary>
[Serializable]
public class LineageEvent
{
    public LineageChange change;
    public int generation;
    /// <summary>The palate before and after, in words.</summary>
    public string from, to;
    /// <summary>A variety it became (Diverged) or left (Reverted).</summary>
    public string variety;
    public float fitness;
}

/// <summary>Anything whose palate evolves generation by generation (Eleos Bloom lineages now; the open end for other systems).</summary>
public interface IEvolvingLineage
{
    string Origin { get; }
    Palate Palate { get; }
    Palate Ancestral { get; }
    int Generation { get; }
    string Variety { get; }
    IReadOnlyList<LineageEvent> History { get; }
}

/// <summary>
/// A lineage (saved with its site): the palate it started with, the one it has now, how many generations it has lived,
/// the variety it has become, and its history.
/// </summary>
[Serializable]
public class Lineage : IEvolvingLineage
{
    /// <summary>What it descends from (a resource site spec id).</summary>
    public string origin;
    public Palate palate = new Palate();
    public Palate ancestral = new Palate();
    public int generation;
    /// <summary>The variety it has become ("Memory Marigolds of Anemoia"), or null while it is still its kind.</summary>
    public string variety;
    /// <summary>Its last feeding, measured at the last Echo.</summary>
    public float fitness, growth, potency;
    public List<LineageEvent> history = new List<LineageEvent>();

    public string Origin => origin;
    public Palate Palate => palate;
    public Palate Ancestral => ancestral;
    public int Generation => generation;
    public string Variety => variety;
    public IReadOnlyList<LineageEvent> History => history;
}

/// <summary>How eaters of feeling live and evolve (Eleos settings; every number a proposal).</summary>
[Serializable]
public class EvolutionSettings
{
    [UnityEngine.Tooltip("Share of growth a fully niche palate gives up (specialists grow less).")]
    [UnityEngine.Range(0f, 1f)] public float nicheGrowthCost = 0.4f;
    [UnityEngine.Tooltip("How much more potent a fully niche palate is when fed its exact cocktail (superloaded).")]
    public float superload = 1.5f;
    [UnityEngine.Tooltip("Chance each Echo that a lineage tries a step (evolution is slow and uneven).")]
    [UnityEngine.Range(0f, 1f)] public float stepChance = 0.35f;
    [UnityEngine.Tooltip("How far one step moves its recipe toward what it tries (0-1).")]
    [UnityEngine.Range(0f, 1f)] public float drift = 0.2f;
    [UnityEngine.Tooltip("How far one step moves its specificity.")]
    [UnityEngine.Range(0f, 1f)] public float specificityStep = 0.1f;
    [UnityEngine.Tooltip("A step is kept only when it is fitter by this share.")]
    public float margin = 0.03f;
    [UnityEngine.Tooltip("Its recipe resembling its ancestors' less than this (cosine), it is a new variety.")]
    [UnityEngine.Range(0f, 1f)] public float varietyAt = 0.8f;
    [UnityEngine.Tooltip("A variety resembling its ancestors this much again has reverted to its kind.")]
    [UnityEngine.Range(0f, 1f)] public float revertAt = 0.92f;
    [UnityEngine.Tooltip("Recipe lines thinner than this share are dropped.")]
    public float thinLine = 0.03f;
    [UnityEngine.Tooltip("Entries of history a lineage keeps.")]
    public int maxHistory = 12;
}

/// <summary>
/// Emotional evolution, with no scene state (tested in <c>EmotionalAlchemyTests</c>): the API every evolving eater of
/// feeling goes through. Today it drives Eleos Bloom lineages (<see cref="WorldSystem.EvolveBlooms"/>); the Atonalis eat
/// cocktails too but do not evolve (a Path's hunger is fixed), and other systems can hold their own
/// <see cref="Lineage"/> and call <see cref="Evolve"/>.
///
/// - <see cref="Feed"/>: at a place (its register), a palate eats <c>specificity x servings + (1 - specificity) x
///   loose food</c>; it grows by that over its need, less the more niche it is (<see cref="EvolutionSettings.nicheGrowthCost"/>),
///   and what it gives is more potent the more niche it is and the more exactly it is fed (<see cref="EvolutionSettings.superload"/>).
///   A niche palate on its exact cocktail is superloaded but sparse; a general one spreads and gives ordinary gifts.
/// - <see cref="Evolve"/>: now and then (<see cref="EvolutionSettings.stepChance"/>) a lineage tries to specialize (sharpen
///   its recipe to the proportions its place gives, and grow pickier) or to generalize (take in the notes its place is
///   rich in, and grow looser), and keeps whichever is fitter. Drifted far from its ancestors it becomes a variety named
///   for the compound feeling it now resembles; drifted back, it reverts.
/// </summary>
public static class EmotionalEvolution
{
    public static readonly EvolutionSettings Defaults = new EvolutionSettings();

    /// <summary>A fresh lineage of <paramref name="origin"/>, its palate its ancestors'.</summary>
    public static Lineage Seed(string origin, IEnumerable<FeelingWeight> recipe, float specificity)
    {
        var palate = new Palate { recipe = EmotionalAlchemy.Shares(recipe), specificity = Math.Max(0f, Math.Min(1f, specificity)) };
        return new Lineage { origin = origin, palate = palate, ancestral = palate.Copy() };
    }

    /// <summary>
    /// What <paramref name="palate"/> gets at a place holding <paramref name="r"/> (plus <paramref name="ambient"/>, the
    /// wild's own faint feeling, which a generalist takes too), against <paramref name="need"/> (0: it needs nothing).
    /// </summary>
    public static Feeding Feed(Palate palate, EmotionalRegister r, float need, float ambient = 0f, EvolutionSettings rules = null)
    {
        rules = rules ?? Defaults;
        var f = new Feeding();
        if (palate == null || palate.recipe == null || palate.recipe.Count == 0) return f;
        float s = Math.Max(0f, Math.Min(1f, palate.specificity));
        f.servings = EmotionalAlchemy.Servings(r, palate.recipe);
        f.loose = EmotionalAlchemy.Loose(r, palate.recipe) + Math.Max(0f, ambient) * (1f - s);
        f.food = s * f.servings + (1f - s) * f.loose;
        f.fit = EmotionalAlchemy.Fit(r, palate.recipe);
        f.growth = (need <= 0f ? 1f : f.food / need) * (1f - rules.nicheGrowthCost * s);
        f.potency = (0.6f + 0.4f * f.fit) * (1f + rules.superload * s * f.fit * f.fit);
        f.fitness = Math.Min(f.growth, 1.5f) * (float)Math.Sqrt(Math.Max(0f, f.potency));
        return f;
    }

    /// <summary>A step toward the specialist: its recipe sharpened to the proportions the place gives of its own notes, and pickier.</summary>
    public static Palate Specialize(Palate palate, EmotionalRegister r, EvolutionSettings rules = null)
    {
        rules = rules ?? Defaults;
        var own = new EmotionalRegister();
        foreach (var l in palate.recipe) own[l.feeling] = r?[l.feeling] ?? 0f;
        var next = Toward(palate, own, rules);
        next.specificity = Math.Min(1f, palate.specificity + rules.specificityStep);
        return next;
    }

    /// <summary>A step toward the generalist: the notes the place is rich in taken into its recipe, and looser.</summary>
    public static Palate Generalize(Palate palate, EmotionalRegister r, EvolutionSettings rules = null)
    {
        rules = rules ?? Defaults;
        var next = Toward(palate, r, rules);
        next.specificity = Math.Max(0f, palate.specificity - rules.specificityStep);
        return next;
    }

    // Its recipe moved `drift` of the way toward a register's shape; thin lines dropped.
    private static Palate Toward(Palate palate, EmotionalRegister target, EvolutionSettings rules)
    {
        var mix = new EmotionalRegister();
        foreach (var l in EmotionalAlchemy.Shares(palate.recipe)) mix[l.feeling] = l.weight * (1f - rules.drift);
        float total = target?.Total ?? 0f;
        if (total > 1e-6f) foreach (var f in EmotionalRegister.All) mix.Add(f, target[f] / total * rules.drift);
        var lines = EmotionalAlchemy.Shares(EmotionalRegister.All.Select(f => new FeelingWeight { feeling = f, weight = mix[f] }))
            .Where(l => l.weight >= rules.thinLine).ToList();
        return new Palate { recipe = EmotionalAlchemy.Shares(lines), specificity = palate.specificity };
    }

    /// <summary>
    /// A generation passes for <paramref name="lineage"/> at a place holding <paramref name="r"/>: its feeding is measured,
    /// and with <paramref name="roll"/> (0-1) below <see cref="EvolutionSettings.stepChance"/> it tries to specialize and to
    /// generalize and keeps the fitter if it beats its present self. Returns what changed (a step, a new variety, a
    /// reversion), or null. <paramref name="kindName"/> names a variety ("Memory Marigolds of Anemoia").
    /// </summary>
    public static LineageEvent Evolve(Lineage lineage, EmotionalRegister r, float need, float ambient, double roll, string kindName, EvolutionSettings rules = null)
    {
        rules = rules ?? Defaults;
        if (lineage == null || lineage.palate == null || lineage.palate.recipe.Count == 0) return null;
        lineage.generation++;
        var now = Feed(lineage.palate, r, need, ambient, rules);
        Record(lineage, now);
        if (roll >= rules.stepChance) return null;
        var special = Specialize(lineage.palate, r, rules);
        var general = Generalize(lineage.palate, r, rules);
        var fs = Feed(special, r, need, ambient, rules);
        var fg = Feed(general, r, need, ambient, rules);
        bool specializes = fs.fitness >= fg.fitness;
        var best = specializes ? special : general;
        var fb = specializes ? fs : fg;
        if (fb.fitness <= now.fitness * (1f + rules.margin)) return null;
        string before = Describe(lineage.palate);
        lineage.palate = best;
        Record(lineage, fb);
        var step = new LineageEvent { change = specializes ? LineageChange.Specialized : LineageChange.Generalized, generation = lineage.generation, from = before, to = Describe(best), fitness = fb.fitness };
        // Far from its ancestors now: a variety of its own, named for the compound feeling it most resembles.
        float kin = EmotionalAlchemy.Cosine(EmotionalRegister.Of(lineage.palate.recipe), EmotionalRegister.Of(lineage.ancestral.recipe));
        if (lineage.variety == null && kin < rules.varietyAt)
        {
            var (compound, _) = EmotionalAlchemy.Nearest(lineage.palate.recipe);
            lineage.variety = $"{kindName} of {compound.name}";
            step = new LineageEvent { change = LineageChange.Diverged, generation = lineage.generation, from = before, to = step.to, variety = lineage.variety, fitness = fb.fitness };
        }
        else if (lineage.variety != null && kin >= rules.revertAt)
        {
            step = new LineageEvent { change = LineageChange.Reverted, generation = lineage.generation, from = before, to = step.to, variety = lineage.variety, fitness = fb.fitness };
            lineage.variety = null;
        }
        lineage.history.Add(step);
        if (lineage.history.Count > rules.maxHistory) lineage.history.RemoveRange(0, lineage.history.Count - rules.maxHistory);
        return step;
    }

    private static void Record(Lineage lineage, Feeding f)
    {
        lineage.fitness = f.fitness;
        lineage.growth = f.growth;
        lineage.potency = f.potency;
    }

    /// <summary>How niche a palate is, in a word: a Purist, a Connoisseur, an Epicure, a Generalist.</summary>
    public static string NicheWord(float specificity) =>
        specificity >= 0.8f ? "Purist" : specificity >= 0.55f ? "Connoisseur" : specificity >= 0.3f ? "Epicure" : "Generalist";

    /// <summary>"Connoisseur: Longing 40%, Wonder 30%, Estrangement 20%, Love 10%".</summary>
    public static string Describe(Palate palate) => palate == null || palate.recipe.Count == 0 ? "no palate"
        : $"{NicheWord(palate.specificity)}: {EmotionalAlchemy.Words(palate.recipe)}";
}
