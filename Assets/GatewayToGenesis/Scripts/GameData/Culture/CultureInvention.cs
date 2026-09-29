using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A dish or drink the people invented and named themselves (the Flavor Log's own pages), saved in
/// <see cref="CultureState.invented"/>. What it is follows the recipe closest to it (<see cref="template"/>): where it is
/// made, how much a batch makes, how much more it feeds, how it keeps, its luxury and its icon. What it does comes from
/// its ingredients, each scaled down by its share of the batch: food value, spoilage, the peach, the ways of living it
/// leans the culture toward, its Faith. Worked out once, when it is invented (<see cref="CultureInvention.Derive"/>).
/// </summary>
[Serializable]
public class InventedRecipe
{
    /// <summary>Its recipe id ("invented-1"): what standing orders and the kitchen call it.</summary>
    public string id;
    /// <summary>The name the people gave it, which is also its resource in the stores.</summary>
    public string name;
    public KitchenMethod method;
    public List<ResourceAmount> inputs = new List<ResourceAmount>();
    /// <summary>The recipe it was closest to when it was invented (its id and its dish), and how close (0-1 of the ingredients shared).</summary>
    public string template, templateDish;
    public float closeness;
    /// <summary>Dishes (or drinks) one batch makes.</summary>
    public float output;
    public float foodValue, spoilPerSeventh;
    public bool peach;
    /// <summary>The template's technology (known when it was invented).</summary>
    public string technology;
    /// <summary>Unity one batch gives (a memorial template's, by how much of its ingredients it shares).</summary>
    public float unity;
    /// <summary>Faith each unit gives when it is enjoyed as a luxury (its ingredients', by share).</summary>
    public float faithPerUnit;
    /// <summary>The luxury categories it satisfies: the template's, and any an ingredient lends by making up enough of it.</summary>
    public List<string> luxuries = new List<string>();
    /// <summary>The ways of living it leans the culture toward when eaten, by its ingredients' shares (they need not sum to 1).</summary>
    public List<CultureLeaning> leanings = new List<CultureLeaning>();
    public string description;
    /// <summary>When it was invented: the Seventh since the founding, and the Age.</summary>
    public int seventh;
    public string ageId;
    /// <summary>The hidden recipe it was kept from (<see cref="HiddenRecipeSpec"/>), or null: a dish of the people's own finding.</summary>
    [SaveOptionalField] public string hidden;

    public FoodClass Cuisine => CultureInvention.ClassOf(method);

    /// <summary>The recipe the kitchen (or cellar) makes it by.</summary>
    public RecipeSpec ToRecipe() => new RecipeSpec
    {
        id = id, dish = name, method = method, output = output, technology = technology, unity = unity, description = description,
        inputs = (inputs ?? new List<ResourceAmount>()).Where(i => i != null).Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList(),
    };

    /// <summary>What it is to the pantry.</summary>
    public FoodKind ToKind() => new FoodKind { resource = name, foodValue = foodValue, spoilPerSeventh = spoilPerSeventh, peach = peach, cuisine = Cuisine };
}

/// <summary>What one ingredient brings to an invention (read from the pantry, the culture's tables and its luxuries).</summary>
public class IngredientFacts
{
    public FoodKind kind;
    /// <summary>The ways of living it leans toward and how much (one family whole for an authored food; an invented one passes on its own).</summary>
    public List<CultureLeaning> leanings = new List<CultureLeaning>();
    public float faithPerUnit;
    public List<string> luxuries = new List<string>();
    /// <summary>An Eleos Bloom's resource, yield or harvest: never brewed or distilled.</summary>
    public bool bloom;
}

/// <summary>
/// The arithmetic of invented dishes and drinks (pure; <see cref="CultureSystem"/> reads the world into it). Every number
/// is a proposal (Canon Gaps "Invented dishes and drinks").
/// </summary>
public static class CultureInvention
{
    public const string IdPrefix = "invented-";

    public static FoodClass ClassOf(KitchenMethod method) => method == KitchenMethod.Cook ? FoodClass.Edible : FoodClass.Beverage;

    /// <summary>"dish" for the kitchen, "drink" for the cellar.</summary>
    public static string Noun(KitchenMethod method) => method == KitchenMethod.Cook ? "dish" : "drink";

    /// <summary>Each resource's share of the batch (its amount over all of them), summed per resource.</summary>
    public static Dictionary<string, float> Shares(IEnumerable<ResourceAmount> inputs)
    {
        var list = (inputs ?? Enumerable.Empty<ResourceAmount>()).Where(i => i != null && !string.IsNullOrEmpty(i.resource) && i.amount > 0f).ToList();
        float total = list.Sum(i => i.amount);
        var shares = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (total <= 0f) return shares;
        foreach (var i in list)
        {
            shares.TryGetValue(i.resource, out float was);
            shares[i.resource] = was + i.amount / total;
        }
        return shares;
    }

    // Σ min of two share maps: how much of one is also the other (1: the same mix).
    private static float Overlap<T>(IDictionary<T, float> a, IDictionary<T, float> b) =>
        a.Sum(p => b.TryGetValue(p.Key, out float other) ? Math.Min(p.Value, other) : 0f);

    private static Dictionary<TKey, float> Group<TKey>(Dictionary<string, float> shares, Func<string, TKey?> keyOf) where TKey : struct
    {
        var grouped = new Dictionary<TKey, float>();
        foreach (var p in shares)
        {
            var key = keyOf(p.Key);
            if (!key.HasValue) continue;
            grouped.TryGetValue(key.Value, out float was);
            grouped[key.Value] = was + p.Value;
        }
        return grouped;
    }

    /// <summary>
    /// How close an invention is to a recipe, 0-1.75: the ingredients they share (by share of the batch), then how alike
    /// their mix of kinds is (edibles, ingredients, spices...: half), then the ways of living they lean toward (a quarter).
    /// </summary>
    public static float Similarity(IEnumerable<ResourceAmount> inputs, RecipeSpec recipe, Func<string, IngredientFacts> facts)
    {
        if (recipe == null) return 0f;
        var mine = Shares(inputs);
        var theirs = Shares(recipe.inputs);
        FoodClass? Class(string r) => facts?.Invoke(r)?.kind?.cuisine;
        return Overlap(mine, theirs) + 0.5f * Overlap(Group(mine, Class), Group(theirs, Class)) + 0.25f * Overlap(Leanings(mine, facts), Leanings(theirs, facts));
    }

    /// <summary>The ways of living a mix leans toward: each ingredient's leanings, scaled by its share of the batch.</summary>
    public static Dictionary<EnclaveFamily, float> Leanings(Dictionary<string, float> shares, Func<string, IngredientFacts> facts)
    {
        var result = new Dictionary<EnclaveFamily, float>();
        foreach (var p in shares ?? new Dictionary<string, float>())
            foreach (var l in facts?.Invoke(p.Key)?.leanings ?? new List<CultureLeaning>())
            {
                if (l == null || l.share <= 0f) continue;
                result.TryGetValue(l.family, out float was);
                result[l.family] = was + p.Value * l.share;
            }
        return result;
    }

    /// <summary>The known recipe made the same way that the invention is closest to (the first listed on a tie), or null when none is known.</summary>
    public static RecipeSpec Closest(KitchenMethod method, IEnumerable<ResourceAmount> inputs, IEnumerable<RecipeSpec> known, Func<string, IngredientFacts> facts)
    {
        RecipeSpec best = null;
        float bestScore = -1f;
        foreach (var r in known ?? Enumerable.Empty<RecipeSpec>())
        {
            if (r == null || r.method != method) continue;
            float score = Similarity(inputs, r, facts);
            if (score > bestScore + 1e-5f) { best = r; bestScore = score; }
        }
        return best;
    }

    /// <summary>
    /// Why this cannot be invented, or null. A dish needs something that feeds; a drink is never made from an Eleos Bloom
    /// or a tea; a spirit is distilled from something fermented; the people must know a way of making it already
    /// (<paramref name="known"/>: the recipes they can make, one of the same method at least).
    /// </summary>
    public static string WhyNot(KitchenMethod method, IList<ResourceAmount> inputs, Func<string, IngredientFacts> facts, IEnumerable<RecipeSpec> known, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        var list = (inputs ?? new List<ResourceAmount>()).Where(i => i != null && !string.IsNullOrEmpty(i.resource) && i.amount > 0f).ToList();
        string noun = Noun(method);
        if (list.Count == 0) return $"Choose what goes into the {noun}.";
        int distinct = list.Select(i => i.resource).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (distinct > Math.Max(1, t.maxInventedIngredients)) return $"A {noun} of more than {t.maxInventedIngredients} ingredients is too much to keep in one's head.";
        var knownOfMethod = (known ?? Enumerable.Empty<RecipeSpec>()).Where(r => r != null && r.method == method).ToList();
        if (knownOfMethod.Count == 0)
            return method == KitchenMethod.Distill ? "Your people know no still yet: learn to distil a spirit first."
                : method == KitchenMethod.Ferment ? "Your people have never brewed anything yet." : "Your people have no kitchen yet.";
        var kinds = list.Select(i => (i, f: facts?.Invoke(i.resource))).ToList();
        var unknown = kinds.FirstOrDefault(p => p.f?.kind == null);
        if (unknown.i != null) return $"{unknown.i.resource} is not something the stores keep.";
        if (!kinds.Any(p => CultureRules.Feeds(p.f.kind.cuisine))) return $"A {noun} of spices alone is no {noun}: add something to it.";
        if (method != KitchenMethod.Cook)
        {
            var eleos = kinds.FirstOrDefault(p => p.f.bloom || p.f.kind.cuisine == FoodClass.EleosTea);
            if (eleos.i != null) return $"{eleos.i.resource} is of the Eleos Blooms or a tea: steeped or distilled it is a tea, never a drink of the cellar.";
            if (method == KitchenMethod.Distill && !kinds.Any(p => p.f.kind.cuisine == FoodClass.Beverage)) return "A spirit is distilled from something fermented: add an ale, mead or wine.";
        }
        var mine = Shares(list);
        var same = knownOfMethod.FirstOrDefault(r => Math.Abs(Overlap(mine, Shares(r.inputs)) - 1f) < 1e-3f);
        if (same != null) return $"That is {same.dish} already.";
        return null;
    }

    /// <summary>
    /// The invention worked out: a batch makes as much, for what went in, as its template's does; it feeds what its
    /// ingredients fed, times the template's gain (a dish feeds more than went into it; a drink need not); it keeps as its
    /// ingredients do, as much better or worse as the template keeps than its own (a spirit never spoils). Its peach,
    /// Faith and leanings are its ingredients', each scaled by its share of the batch; its luxuries are the template's,
    /// and those of any ingredient that makes up <see cref="CultureLifeTuning.inventedLuxuryShare"/> of it.
    /// </summary>
    public static InventedRecipe Derive(string id, string name, KitchenMethod method, IList<ResourceAmount> inputs, RecipeSpec template,
        Func<string, IngredientFacts> facts, Func<string, IEnumerable<string>> templateLuxuries, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        var list = (inputs ?? new List<ResourceAmount>()).Where(i => i != null && !string.IsNullOrEmpty(i.resource) && i.amount > 0f)
            .GroupBy(i => i.resource, StringComparer.OrdinalIgnoreCase).Select(g => new ResourceAmount { resource = g.First().resource, amount = g.Sum(i => i.amount) }).ToList();
        var shares = Shares(list);
        float total = list.Sum(i => i.amount);
        IngredientFacts F(string r) => facts?.Invoke(r) ?? new IngredientFacts();
        float Feeds(string r) { var k = F(r).kind; return k == null || !CultureRules.Feeds(k.cuisine) ? 0f : Math.Max(0f, k.foodValue); }
        float Spoil(string r) => Math.Max(0f, F(r).kind?.spoilPerSeventh ?? 0f);

        var recipe = new InventedRecipe
        {
            id = id, name = name, method = method, inputs = list,
            template = template?.id, templateDish = template?.dish, technology = template?.technology,
        };
        // How much a batch makes: as much for what went in as the template makes for its own.
        float templateIn = (template?.inputs ?? new List<ResourceAmount>()).Where(i => i != null).Sum(i => Math.Max(0f, i.amount));
        float ratio = template != null && templateIn > 0f ? Math.Max(0f, template.output) / templateIn : 1f;
        recipe.output = Math.Max(0.1f, (float)Math.Round(total * ratio, 1));
        // What it feeds: its ingredients' food value, times the template's gain.
        float fedIn = list.Sum(i => i.amount * Feeds(i.resource));
        float gain = 1f;
        if (template != null)
        {
            var (tin, tout) = CultureLifeRules.FoodValue(template, Feeds, template.output);
            if (tin > 0f) gain = tout / tin;
        }
        recipe.foodValue = (float)Math.Round(fedIn * gain / recipe.output, 3);
        // How it keeps: as its ingredients do, bettered (or worsened) as the template keeps against its own ingredients.
        float spoilIn = shares.Sum(p => p.Value * Spoil(p.Key));
        float templateDish = template != null ? Spoil(template.dish) : spoilIn;
        float templateSpoilIn = template != null ? Shares(template.inputs).Sum(p => p.Value * Spoil(p.Key)) : spoilIn;
        float spoil = templateSpoilIn > 1e-6f ? spoilIn * templateDish / templateSpoilIn : templateDish;
        recipe.spoilPerSeventh = (float)Math.Round(Math.Min(1f, Math.Max(0f, spoil)), 4);
        // Its ingredients' character, each by its share.
        recipe.peach = shares.Where(p => F(p.Key).kind?.peach == true).Sum(p => p.Value) >= 0.5f - 1e-4f;
        recipe.faithPerUnit = (float)Math.Round(shares.Sum(p => p.Value * Math.Max(0f, F(p.Key).faithPerUnit)), 3);
        recipe.leanings = Leanings(shares, facts).OrderByDescending(p => p.Value).ThenBy(p => p.Key)
            .Select(p => new CultureLeaning { family = p.Key, share = (float)Math.Round(p.Value, 3) }).ToList();
        recipe.closeness = template != null ? (float)Math.Round(Overlap(shares, Shares(template.inputs)), 3) : 0f;
        recipe.unity = template != null ? (float)Math.Round(Math.Max(0f, template.unity) * recipe.closeness, 2) : 0f;
        var luxuries = new List<string>(templateLuxuries?.Invoke(template?.dish) ?? Enumerable.Empty<string>());
        foreach (var p in shares.Where(p => p.Value >= t.inventedLuxuryShare - 1e-4f))
            luxuries.AddRange(F(p.Key).luxuries ?? new List<string>());
        recipe.luxuries = luxuries.Where(l => !string.IsNullOrEmpty(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        recipe.description = Describe(recipe);
        return recipe;
    }

    /// <summary>"Wild Honey and Deep-Rooted Grain, cooked the way of Honeyed Grain Porridge."</summary>
    public static string Describe(InventedRecipe r)
    {
        var names = (r.inputs ?? new List<ResourceAmount>()).Where(i => i != null).OrderByDescending(i => i.amount).Select(i => i.resource).ToList();
        string what = names.Count <= 1 ? names.FirstOrDefault() ?? "nothing" : string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        string how = r.method == KitchenMethod.Ferment ? "brewed" : r.method == KitchenMethod.Distill ? "distilled" : "cooked";
        return string.IsNullOrEmpty(r.templateDish) ? $"{what}, {how} as the people first thought to." : $"{what}, {how} the way of {r.templateDish}.";
    }

    /// <summary>The next free id ("invented-3").</summary>
    public static string NextId(IEnumerable<InventedRecipe> invented)
    {
        int n = 1;
        var taken = new HashSet<string>((invented ?? Enumerable.Empty<InventedRecipe>()).Where(r => r != null).Select(r => r.id), StringComparer.OrdinalIgnoreCase);
        while (taken.Contains(IdPrefix + n)) n++;
        return IdPrefix + n;
    }
}
