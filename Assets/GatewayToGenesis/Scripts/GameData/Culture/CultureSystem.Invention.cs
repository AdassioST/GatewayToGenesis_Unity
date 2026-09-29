using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Dishes and drinks the people invent and name themselves (arithmetic in <see cref="CultureInvention"/>, saved in
/// <see cref="CultureState.invented"/>): the player chooses what goes in and how it is made (cooked in the kitchen,
/// brewed or distilled in the cellar). It is treated as the known recipe closest to it and does what its ingredients
/// do, each scaled by its share. Each becomes a resource of the stores (<see cref="RuntimeUnits"/>), a pantry kind
/// (<see cref="Pantry.AddKind"/>) and a recipe the kitchen makes like any other, and the people's own are the most
/// eligible to become national.
/// </summary>
public partial class CultureSystem
{
    private CultureLifeTuning _withInventions, _inventedFrom;
    private List<InventedRecipe> _inventedList, _ensuredList;
    private int _inventedCount = -1, _ensuredCount = -1;
    private HashSet<string> _blooms;

    public IReadOnlyList<InventedRecipe> Invented => (IReadOnlyList<InventedRecipe>)_state.invented ?? Array.Empty<InventedRecipe>();

    /// <summary>The invented dish or drink by its name (its resource), or null.</summary>
    public InventedRecipe InventedFood(string resource) =>
        string.IsNullOrEmpty(resource) ? null : _state.invented?.FirstOrDefault(r => r != null && string.Equals(r.name, resource, StringComparison.OrdinalIgnoreCase));

    public bool IsInvented(string resource) => InventedFood(resource) != null;

    // The authored numbers, or a copy with the inventions in it (made again only when the inventions change).
    private CultureLifeTuning LifeWithInventions(CultureLifeTuning authored)
    {
        var invented = _state.invented;
        if (invented == null || invented.Count == 0) return authored;
        if (_withInventions == null || _inventedFrom != authored || !ReferenceEquals(_inventedList, invented) || _inventedCount != invented.Count)
        {
            _withInventions = authored.WithInventions(invented);
            _inventedFrom = authored;
            _inventedList = invented;
            _inventedCount = invented.Count;
        }
        return _withInventions;
    }

    // Every invention has its resource and its pantry kind in this world (after inventing, and after a load).
    private void EnsureInventions()
    {
        if (_state.invented == null) _state.invented = new List<InventedRecipe>();
        if (Pantry.Instance == null || Pantry.Instance.Settings == null) return;
        foreach (var r in _state.invented.Where(r => r != null && !string.IsNullOrEmpty(r.name)))
        {
            Pantry.Instance.AddKind(r.ToKind());
            RuntimeUnits.Ensure(new RuntimeUnitRecord { name = r.name, type = "Stored Food", section = "Stored Food", description = r.description, iconFrom = r.templateDish });
        }
        _ensuredList = _state.invented;
        _ensuredCount = _state.invented.Count;
    }

    private void CheckInventions()
    {
        if (!ReferenceEquals(_ensuredList, _state.invented) || _ensuredCount != (_state.invented?.Count ?? 0)) EnsureInventions();
    }

    // ===== WHAT GOES IN =====

    /// <summary>Resources of the Eleos Blooms (a site's resource, its yields and its harvest): never brewed or distilled.</summary>
    public static HashSet<string> BloomResources() =>
        new HashSet<string>(GameCatalog.World.All.Where(w => w != null && w.generation != null)
            .SelectMany(w => w.generation.resourceSites).Where(s => s != null && s.kind == ResourceKind.Bloom)
            .SelectMany(s => new[] { s.resource }.Concat((s.yields ?? new List<ResourceAmount>()).Select(y => y?.resource)).Concat((s.harvest ?? new List<ResourceAmount>()).Select(h => h?.resource)))
            .Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);

    private bool IsBloom(string resource) => (_blooms ?? (_blooms = BloomResources())).Contains(resource ?? string.Empty);

    /// <summary>What an ingredient brings to an invention (null when the stores do not keep it).</summary>
    public IngredientFacts FactsOf(string resource)
    {
        var kind = Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(resource) : null;
        if (kind == null) return null;
        var life = Life;
        var invented = InventedFood(resource);
        var family = Tuning.FamilyOfResource(resource);
        return new IngredientFacts
        {
            kind = kind,
            leanings = invented != null ? invented.leanings.Where(l => l != null).Select(l => new CultureLeaning { family = l.family, share = l.share }).ToList()
                : family.HasValue ? new List<CultureLeaning> { new CultureLeaning { family = family.Value, share = 1f } } : new List<CultureLeaning>(),
            faithPerUnit = life.Good(resource)?.faithPerUnit ?? 0f,
            luxuries = LuxuriesOf(resource).ToList(),
            bloom = IsBloom(resource),
        };
    }

    private IEnumerable<string> LuxuriesOf(string resource) =>
        string.IsNullOrEmpty(resource) ? Enumerable.Empty<string>()
            : Life.luxuries.Where(l => l != null && l.resources != null && l.resources.Contains(resource, StringComparer.OrdinalIgnoreCase)).Select(l => l.category);

    /// <summary>
    /// What the kitchen (or the cellar) may put into an invention: the stored foods the people have come to know (each
    /// has its place in storage), the cellar never an Eleos Bloom or a tea. Kitchen order, then by name.
    /// </summary>
    public List<FoodKind> IngredientChoices(KitchenMethod method)
    {
        var pantry = Pantry.Instance;
        var units = GameUnitsLogic.Instance;
        if (pantry == null || pantry.Settings == null || units == null) return new List<FoodKind>();
        return pantry.Settings.kinds
            .Where(k => k != null && !string.IsNullOrEmpty(k.resource) && units.GetResourceSlotFromName(k.resource) != null)
            .Where(k => method == KitchenMethod.Cook || (k.cuisine != FoodClass.EleosTea && !IsBloom(k.resource)))
            .OrderBy(k => CultureRules.TableOrder(k.cuisine)).ThenBy(k => k.resource, StringComparer.Ordinal).ToList();
    }

    /// <summary>The recipes the people can make now (their technology known): an invention is made the way of one of them.</summary>
    public List<RecipeSpec> KnownRecipes() => Life.recipes.Where(r => r != null && (string.IsNullOrEmpty(r.technology) || Researched(r.technology))).ToList();

    /// <summary>The known recipe made the same way that these ingredients are closest to, or null.</summary>
    public RecipeSpec ClosestRecipe(KitchenMethod method, IList<ResourceAmount> inputs) => CultureInvention.Closest(method, inputs, KnownRecipes(), FactsOf);

    // ===== INVENTING =====

    /// <summary>Why this dish or drink cannot be invented now, or null.</summary>
    public string WhyNotInvent(string name, KitchenMethod method, IList<ResourceAmount> inputs)
    {
        if (!_state.founded) return "The culture has not been founded yet: no one keeps a Flavor Log.";
        var life = Life;
        if ((_state.invented?.Count ?? 0) >= Mathf.Max(0, life.maxInvented)) return $"Your people already keep {life.maxInvented} recipes of their own.";
        string why = CultureInvention.WhyNot(method, inputs, FactsOf, KnownRecipes(), life);
        if (why != null) return why;
        string noun = CultureInvention.Noun(method);
        why = CultureRules.WhyNotName(name, $"The {noun}'s name");
        if (why != null) return why;
        string n = CultureRules.CleanName(name);
        if (GameCatalog.IsResource(n) || GameCatalog.Units.Contains(n) || life.recipes.Any(r => r != null && string.Equals(r.dish, n, StringComparison.OrdinalIgnoreCase))
            || (GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.GetGameUnitByName(n) != null))
            return $"{n} is already the name of something else.";
        return null;
    }

    /// <summary>What these ingredients would make (its template, yield, food value, keeping, luxuries and leanings), or null with nothing to go on.</summary>
    public InventedRecipe PreviewInvention(string name, KitchenMethod method, IList<ResourceAmount> inputs)
    {
        var list = (inputs ?? new List<ResourceAmount>()).Where(i => i != null && i.amount > 0f && FactsOf(i.resource) != null).ToList();
        if (list.Count == 0) return null;
        var template = ClosestRecipe(method, list);
        string n = CultureRules.CleanName(name);
        var preview = CultureInvention.Derive("preview", string.IsNullOrEmpty(n) ? $"Your {CultureInvention.Noun(method)}" : n, method, list, template, FactsOf, LuxuriesOf, Life);
        KitchenTrials.Apply(FoundHiddenRecipe(method, list), preview);
        return preview;
    }

    /// <summary>
    /// The people make a dish or drink of their own and name it: it joins the kitchen (or the cellar) as a recipe, the
    /// stores as a resource, and their history as a moment. Null (nothing changed) when <see cref="WhyNotInvent"/> refuses.
    /// </summary>
    public InventedRecipe Invent(string name, KitchenMethod method, IList<ResourceAmount> inputs)
    {
        string why = WhyNotInvent(name, method, inputs);
        if (why != null) { GameLog.Event($"Invention refused: {why}", Log); return null; }
        var life = Life;
        var template = ClosestRecipe(method, inputs);
        var recipe = CultureInvention.Derive(CultureInvention.NextId(_state.invented), CultureRules.CleanName(name), method, inputs, template, FactsOf, LuxuriesOf, life);
        // Kept from a hidden recipe the kitchen found (KitchenTrials): finer than its ingredients alone would make.
        KitchenTrials.Apply(FoundHiddenRecipe(method, inputs), recipe);
        recipe.seventh = _state.sevenths;
        recipe.ageId = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null;
        _state.invented.Add(recipe);
        EnsureInventions();
        string noun = CultureInvention.Noun(method);
        AddJoy(life.inventionJoy);
        Remember("invented", recipe.name, $"{Capital(PeopleWord)} made a {noun} of their own and called it {recipe.name}: {recipe.description}");
        GameLog.Event($"{recipe.name} invented ({noun}, the way of {recipe.templateDish ?? "nothing known"}): {recipe.output:0.#} a batch, food value {recipe.foodValue:0.##}.", Log);
        NotificationFeed.Push($"{recipe.name}, a {noun} of your own", $"{recipe.description} {Capital(PeopleWord)} will take to it sooner than to anything else.",
            NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:invented:" + recipe.name);
        RaiseChanged();
        return recipe;
    }
}
