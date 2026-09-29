using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The culture's serving boundary (<see cref="CultureServing"/>) and recipe read contract (<see cref="IRecipeCultureRead"/>).
/// A serving spends finished stock by its own resource, all or nothing, tells the foodways it was used once (the
/// economic leaning, never a second social credit) and records the caller's one occurrence only when it succeeded. A
/// reservation spends the same way now and can be returned whole later (a cancelled preparation); what is reserved is
/// out of the stores, so it neither spoils nor feeds anyone meanwhile.
/// </summary>
public partial class CultureSystem : IRecipeCultureRead
{
    // ===== RECIPES, READ ONLY =====

    private RecipeCultureInfo Info(RecipeSpec r) => r == null ? null : new RecipeCultureInfo
    {
        id = r.id, dish = r.dish, method = r.method, custom = InventedFood(r.dish) != null, technology = r.technology,
        available = string.IsNullOrEmpty(r.technology) || Researched(r.technology), description = r.description,
    };

    public RecipeCultureInfo RecipeInfo(string id) => Info(Life.Recipe(id));

    public RecipeCultureInfo RecipeOfDish(string resource) =>
        string.IsNullOrEmpty(resource) ? null : Info(Life.recipes.FirstOrDefault(r => r != null && string.Equals(r.dish, resource, StringComparison.OrdinalIgnoreCase)));

    public IReadOnlyList<RecipeCultureInfo> RecipeInfos() => Life.recipes.Where(r => r != null).Select(Info).ToList();

    // ===== FOOD REFERENCES =====

    /// <summary>The stored resource a food reference names (a recipe's dish, or the resource itself), or null.</summary>
    public string FoodResource(CultureEntityRef food)
    {
        if (food == null || !food.IsKnown) return null;
        if (food.kind == CultureEntityKind.Recipe) return Life.Recipe(food.id)?.dish;
        return food.kind == CultureEntityKind.Resource ? food.id : null;
    }

    /// <summary>What <paramref name="food"/> is called now (a recipe by its dish), else its saved label.</summary>
    public string FoodLabel(CultureEntityRef food) => FoodResource(food) ?? food?.Display ?? "no food";

    /// <summary>Why <paramref name="food"/> cannot be set on a table (unknown, not yet known how to make, not a dish, tea or drink), or null.</summary>
    public string WhyNotServable(CultureEntityRef food)
    {
        if (food == null || !food.IsKnown) return "No food chosen.";
        if (food.kind == CultureEntityKind.Recipe)
        {
            var r = RecipeInfo(food.id);
            if (r == null) return $"No recipe '{food.Display}' is known.";
            if (!r.available) return $"{r.dish} needs {r.technology}.";
        }
        else if (food.kind != CultureEntityKind.Resource) return "Only a dish, a tea or a drink can be served.";
        string resource = FoodResource(food);
        if (!CultureServing.Servable(Pantry.ClassOf(resource))) return $"{resource} is not a dish, a tea or a drink the stores keep.";
        return null;
    }

    /// <summary>Portions of <paramref name="food"/> worth <paramref name="foodValue"/> (0 when it cannot be served).</summary>
    public float PortionsOf(CultureEntityRef food, float foodValue)
    {
        string resource = FoodResource(food);
        var kind = Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(resource) : null;
        return kind == null ? 0f : CultureServing.PortionsFor(foodValue, kind.foodValue);
    }

    // ===== SERVING =====

    private static float HeldExact(string resource) => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceAmountExact(resource) : 0f;

    /// <summary>What serving <paramref name="items"/> would spend, or why it cannot (nothing is changed).</summary>
    public CultureCommandResult PreviewServe(IEnumerable<ResourceAmount> items)
    {
        var merged = CultureServing.Merge(items);
        string why = CultureServing.WhyNot(merged, HeldExact);
        if (why != null) return CultureCommandResult.Fail(why);
        var r = CultureCommandResult.Ok($"Serves {CultureServing.Text(merged)} from the stores.");
        r.paid.AddRange(merged);
        return r;
    }

    /// <summary>
    /// Serve finished stock, all or nothing: every line is spent or none is. The foodways hear of the use once. When it
    /// succeeds, <paramref name="occurrence"/> (the caller's purpose: hospitality, an observance's table...) is recorded;
    /// when it fails, nothing is spent and nothing is recorded.
    /// </summary>
    public CultureCommandResult Serve(IEnumerable<ResourceAmount> items, CulturalOccurrence occurrence = null)
    {
        var result = Reserve(items);
        if (!result.succeeded) return result;
        foreach (var i in result.paid) RecordUse(i.resource, i.amount);
        if (occurrence != null && Record(occurrence)) result.occurrences.Add(occurrence.key);
        return result;
    }

    /// <summary>Take finished stock out of the stores now, all or nothing, to be served later (<see cref="Unreserve"/> returns it).</summary>
    public CultureCommandResult Reserve(IEnumerable<ResourceAmount> items)
    {
        var preview = PreviewServe(items);
        if (!preview.succeeded || GameUnitsLogic.Instance == null) return preview.succeeded ? CultureCommandResult.Fail("There are no stores.") : preview;
        foreach (var i in preview.paid) GameUnitsLogic.Instance.ChangeResourceFromName(i.resource, -i.amount, false);
        return preview;
    }

    /// <summary>Return what was reserved to the stores (what they have room for); the amounts actually returned.</summary>
    public List<ResourceAmount> Unreserve(IEnumerable<ResourceAmount> items)
    {
        var back = new List<ResourceAmount>();
        var units = GameUnitsLogic.Instance;
        if (units == null) return back;
        foreach (var i in CultureServing.Merge(items))
        {
            float before = units.GetResourceAmountExact(i.resource);
            units.ChangeResourceFromName(i.resource, i.amount, false);
            float returned = Math.Max(0f, units.GetResourceAmountExact(i.resource) - before);
            if (returned > 0f) back.Add(new ResourceAmount { resource = i.resource, amount = returned });
        }
        return back;
    }

    /// <summary>The foodways hear that reserved food was eaten at last (a kept observance's table): once, when it is served.</summary>
    public void ServeReserved(IEnumerable<ResourceAmount> items)
    {
        foreach (var i in CultureServing.Merge(items)) RecordUse(i.resource, i.amount);
    }
}
