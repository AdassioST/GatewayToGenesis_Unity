using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A recipe no one has written down (a <see cref="SecretFormula"/> made in the kitchen or the cellar) and what finding it
/// gives: the dish the people then keep is better than anything its ingredients alone would make. Every one is a proposal
/// (Canon Gaps "Kitchen trials").
/// </summary>
[Serializable]
public class HiddenRecipeSpec
{
    public SecretFormula formula = new SecretFormula();
    [UnityEngine.Tooltip("Food value of what is kept, times this (a dish that feeds more than its ingredients should).")]
    public float foodValueMultiplier = 1.3f;
    [UnityEngine.Tooltip("What is kept never spoils.")]
    public bool neverSpoils;
    [UnityEngine.Tooltip("A luxury category it satisfies besides its own (\"Sweets\", \"Fine Dishes\"...).")]
    public string luxury;
    [UnityEngine.Tooltip("Unity each batch gives (food as remembrance), on top of what it would give.")]
    public float unity;
    [UnityEngine.Tooltip("Faith each unit gives when enjoyed as a luxury, on top of what it would give.")]
    public float faithPerUnit;
    [UnityEngine.Tooltip("Era Score, once, when a trial finds it.")]
    public int eraScore = 2;

    public KitchenMethod Method => KitchenTrials.MethodOf(formula?.method);
}

/// <summary>One batch the kitchen tried: what went in, what came of it, and how near it came to a hidden recipe.</summary>
[Serializable]
public class KitchenTrial
{
    /// <summary><see cref="Experiments.Signature"/> of the mix: the same mix tried again replaces it.</summary>
    public string key;
    public KitchenMethod method;
    public List<ResourceAmount> inputs = new List<ResourceAmount>();
    /// <summary>The Seventh since the founding it was tried in.</summary>
    public int seventh;
    /// <summary><see cref="Warmth"/> as an int.</summary>
    public int warmth;
    /// <summary>The hidden recipe it found, or null.</summary>
    public string found;
    /// <summary>What it turned out to be (the numbers the cooks now know); named "A trial batch" until kept.</summary>
    public InventedRecipe result;
}

/// <summary>The kitchen's trials (saved in <see cref="CultureExtensionState.kitchen"/>).</summary>
[Serializable]
public class KitchenLog
{
    public List<KitchenTrial> trials = new List<KitchenTrial>();
    public List<FormulaProgress> formulas = new List<FormulaProgress>();
    /// <summary>Batches tried in <see cref="lastSeventh"/> (the kitchen tries only so many in one Seventh).</summary>
    public int triedThisSeventh;
    public int lastSeventh = -1;
    /// <summary>Every batch ever tried.</summary>
    public int total;
}

/// <summary>
/// The kitchen's experiments, with no scene state (tested in <c>ExperimentTests</c>). The rules (the discovery loop:
/// Docs/Planning/DISCOVERY_LOOP.md): no one knows what a mix will be until a batch is tried, and trying one uses up what
/// goes in; every trial is remembered with its numbers, so a mix is never a mystery twice; each trial says how near it
/// came to one of the recipes no one has written down (<see cref="Warmth"/>), and coming nearer brings that recipe's next
/// hint to light; a trial that makes one finds it for good.
/// </summary>
public static class KitchenTrials
{
    /// <summary>Trials kept in the log (the oldest are forgotten first; found ones are kept).</summary>
    public const int MaxRemembered = 40;

    public static string MethodName(KitchenMethod method) => method.ToString();

    public static KitchenMethod MethodOf(string name) =>
        Enum.TryParse(name ?? string.Empty, true, out KitchenMethod m) ? m : KitchenMethod.Cook;

    public static Dictionary<string, float> Shares(IEnumerable<ResourceAmount> inputs) =>
        Experiments.Shares((inputs ?? Enumerable.Empty<ResourceAmount>()).Where(i => i != null).Select(i => (i.resource, i.amount)));

    public static string Key(KitchenMethod method, IEnumerable<ResourceAmount> inputs) => Experiments.Signature(MethodName(method), Shares(inputs));

    /// <summary>The trial of this very mix (by its shares, whatever the batch size), or null when it was never tried.</summary>
    public static KitchenTrial Tried(KitchenLog log, KitchenMethod method, IEnumerable<ResourceAmount> inputs)
    {
        if (log?.trials == null) return null;
        string key = Key(method, inputs);
        return log.trials.LastOrDefault(t => t != null && t.key == key);
    }

    /// <summary>The hidden recipes made this way.</summary>
    public static IEnumerable<SecretFormula> Formulas(IEnumerable<HiddenRecipeSpec> specs) =>
        (specs ?? Enumerable.Empty<HiddenRecipeSpec>()).Where(s => s?.formula != null).Select(s => s.formula);

    /// <summary>The hidden recipe this mix is, found or not (null when it is none).</summary>
    public static HiddenRecipeSpec Matching(IEnumerable<HiddenRecipeSpec> specs, KitchenMethod method, IEnumerable<ResourceAmount> inputs)
    {
        var shares = Shares(inputs);
        return (specs ?? Enumerable.Empty<HiddenRecipeSpec>()).FirstOrDefault(s => s?.formula != null && Experiments.Matches(s.formula, MethodName(method), shares));
    }

    /// <summary>
    /// Record a trial: its warmth against the hidden recipes not yet found, what it found, and its place in the log (the
    /// same mix replaces its older trial; past <see cref="MaxRemembered"/> the oldest unfound ones are forgotten).
    /// Returns the trial, the hidden recipe the warmth refers to (null for Nothing) and whether a new hint came to light.
    /// </summary>
    public static (KitchenTrial trial, HiddenRecipeSpec near, bool newHint) Record(KitchenLog log, IEnumerable<HiddenRecipeSpec> specs, KitchenMethod method,
        IList<ResourceAmount> inputs, InventedRecipe result, int seventh)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        var list = (specs ?? Enumerable.Empty<HiddenRecipeSpec>()).Where(s => s?.formula != null).ToList();
        var shares = Shares(inputs);
        var found = new HashSet<string>(log.formulas.Where(f => f != null && f.found).Select(f => f.id), StringComparer.OrdinalIgnoreCase);
        var (warmth, formula) = Experiments.WarmthOf(list.Select(s => s.formula), found, MethodName(method), shares);
        var near = formula == null ? null : list.FirstOrDefault(s => s.formula == formula);
        bool newHint = formula != null && Experiments.Record(log.formulas, formula, warmth);
        var trial = new KitchenTrial
        {
            key = Experiments.Signature(MethodName(method), shares), method = method, seventh = seventh, warmth = (int)warmth,
            found = warmth == Warmth.Found ? formula.id : null, result = result,
            inputs = (inputs ?? new List<ResourceAmount>()).Where(i => i != null && i.amount > 0f).Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList(),
        };
        // A mix that was already found keeps its finding when tried again.
        var older = log.trials.FindLast(t => t != null && t.key == trial.key);
        if (older != null && trial.found == null) trial.found = older.found;
        log.trials.RemoveAll(t => t != null && t.key == trial.key);
        log.trials.Add(trial);
        while (log.trials.Count > MaxRemembered)
        {
            int oldest = log.trials.FindIndex(t => t == null || string.IsNullOrEmpty(t.found));
            log.trials.RemoveAt(oldest < 0 ? 0 : oldest);
        }
        if (log.lastSeventh != seventh) { log.lastSeventh = seventh; log.triedThisSeventh = 0; }
        log.triedThisSeventh++;
        log.total++;
        return (trial, near, newHint);
    }

    /// <summary>Batches the kitchen may still try this Seventh.</summary>
    public static int TriesLeft(KitchenLog log, int seventh, int perSeventh) =>
        Math.Max(0, perSeventh - (log != null && log.lastSeventh == seventh ? log.triedThisSeventh : 0));

    /// <summary>What finding a hidden recipe makes of a dish kept from it: it feeds more, keeps, and is finer than its ingredients alone.</summary>
    public static void Apply(HiddenRecipeSpec spec, InventedRecipe recipe)
    {
        if (spec == null || recipe == null) return;
        recipe.hidden = spec.formula?.id;
        recipe.foodValue = (float)Math.Round(recipe.foodValue * Math.Max(1f, spec.foodValueMultiplier), 3);
        if (spec.neverSpoils) recipe.spoilPerSeventh = 0f;
        if (!string.IsNullOrEmpty(spec.luxury) && !recipe.luxuries.Contains(spec.luxury, StringComparer.OrdinalIgnoreCase)) recipe.luxuries.Add(spec.luxury);
        recipe.unity = (float)Math.Round(recipe.unity + Math.Max(0f, spec.unity), 2);
        recipe.faithPerUnit = (float)Math.Round(recipe.faithPerUnit + Math.Max(0f, spec.faithPerUnit), 3);
        if (!string.IsNullOrEmpty(spec.formula?.description)) recipe.description = spec.formula.description.Trim();
    }

    /// <summary>What the cooks say of a trial's warmth.</summary>
    public static string WarmthWords(Warmth warmth)
    {
        switch (warmth)
        {
            case Warmth.Found: return "This is it: a recipe no one had written down.";
            case Warmth.VeryClose: return "The old cooks go quiet over it: it is very nearly something they remember.";
            case Warmth.Close: return "Something in it stirs an old memory: it is close to one of the whispered recipes.";
            case Warmth.Faint: return "A hint of something familiar, faint and far off.";
            default: return "Nothing in it reminds anyone of anything.";
        }
    }

    /// <summary>Why a hidden recipe could never be found, or null.</summary>
    public static string Problem(HiddenRecipeSpec spec, Func<string, bool> isFood, Func<string, bool> isBloom)
    {
        if (spec?.formula == null) return "is empty";
        string why = Experiments.Problem(spec.formula);
        if (why != null) return why;
        if (!Enum.TryParse(spec.formula.method, true, out KitchenMethod method)) return $"is made by \"{spec.formula.method}\", which is no way of the kitchen";
        foreach (var part in spec.formula.parts)
        {
            if (isFood != null && !isFood(part.component)) return $"needs {part.component}, which the stores do not keep";
            if (method != KitchenMethod.Cook && isBloom != null && isBloom(part.component)) return $"brews {part.component}, an Eleos Bloom (never a drink of the cellar)";
        }
        if (spec.foodValueMultiplier < 1f) return "feeds less than its ingredients would once found";
        return null;
    }

    // ===== THE WHISPERED RECIPES (proposals) =====

    private static FormulaPart P(string component, float min, float max) => new FormulaPart { component = component, minShare = min, maxShare = max };

    /// <summary>The hidden recipes the culture starts with (code defaults: Culture.asset has no tuning block). Every name, part and reward is a proposal.</summary>
    public static List<HiddenRecipeSpec> Defaults() => new List<HiddenRecipeSpec>
    {
        new HiddenRecipeSpec
        {
            formula = new SecretFormula
            {
                id = "everkeep-honeycake", name = "Everkeep Honeycake", method = "Cook",
                parts = { P("Wild Honey", 0.35f, 0.6f), P("Deep-Rooted Grain", 0.25f, 0.5f), P("Peach Pits", 0.05f, 0.2f) },
                hints =
                {
                    "The first keepers baked a cake for the long road that no Seventh could spoil.",
                    "Its sweetness came from the wild, and its body from the grain.",
                    "A bitter stone of the peach, crushed small, is what kept it.",
                },
                description = "Honey, grain and crushed peach stone, baked hard for the road: it never spoils.",
            },
            foodValueMultiplier = 1.25f, neverSpoils = true, eraScore = 2,
        },
        new HiddenRecipeSpec
        {
            formula = new SecretFormula
            {
                id = "moonwater-broth", name = "Moonwater Broth", method = "Cook",
                parts = { P("River Fish", 0.35f, 0.7f), P("Glimmerfern", 0.1f, 0.3f), P("Silver Salt", 0.05f, 0.2f) },
                hints =
                {
                    "The fishers of the silver rivers speak of a broth that tastes of moonlight.",
                    "Silver in the water, silver in the pot.",
                    "And a sprig of the fern that grows beside that water, no more than a fifth of it.",
                },
                description = "Fish from the silver water, salt and the fern that grows beside it: a broth the people drink as a prayer.",
            },
            foodValueMultiplier = 1.3f, luxury = "Fine Dishes", faithPerUnit = 0.15f, eraScore = 2,
        },
        new HiddenRecipeSpec
        {
            formula = new SecretFormula
            {
                id = "ashen-hearthbread", name = "Ashen Hearthbread", method = "Cook",
                parts = { P("Ash-Bread", 0.4f, 0.7f), P("Bitter Roots", 0.15f, 0.35f), P("Wild Honey", 0.1f, 0.25f) },
                hints =
                {
                    "What was saved from the fire can be sweetened by those who survived it.",
                    "The bread of ash, and the roots of the hungry years.",
                    "A little honey, for what came after: never more than a quarter.",
                },
                description = "Ash-bread, the roots of the hungry years and a little honey: the people break it to remember.",
            },
            foodValueMultiplier = 1.2f, unity = 1.5f, eraScore = 2,
        },
        new HiddenRecipeSpec
        {
            formula = new SecretFormula
            {
                id = "first-harvest-sunmead", name = "Sunmead of the First Harvest", method = "Ferment", exclusive = true,
                parts = { P("Wild Honey", 0.4f, 0.7f), P("Dried Auric Peaches", 0.2f, 0.5f), P("Auric Saffron", 0.03f, 0.15f) },
                hints =
                {
                    "A gold drink for a gold day: the elders say it was brewed once, for the first harvest.",
                    "Honey and the peach, and something the colour of the sun.",
                    "Nothing else goes into the crock: gold alone.",
                },
                description = "Honey, peach and saffron and nothing else: the gold mead of the first harvest.",
            },
            foodValueMultiplier = 1.2f, luxury = "Sweets", faithPerUnit = 0.1f, eraScore = 2,
        },
    };
}

/// <summary>The kitchen's trials ride in the culture's extension envelope (T01's contract for later culture features).</summary>
public partial class CultureExtensionState
{
    [SaveOptionalField] public KitchenLog kitchen = new KitchenLog();
}
