using System;
using System.Collections.Generic;
using System.Linq;

// The culture's serving boundary and recipe read contract (CULTURE_REDESIGN.md, "Freeze this minimum contract"):
// - IRecipeCultureRead: what the culture may know of a recipe (authored or invented) by its id: the dish it makes,
//   how, whether it is the people's own, whether it can be made now. Read only: no derivation, no mutation.
// - Serving: finished stock (a dish, a tea, a drink already in the stores) spent all-or-nothing for a named purpose.
//   Ingredients are production inputs, never feast portions; a generic food-value draw (Pantry.TrySpend) cannot
//   prove a named dish was served, so a chosen dish is always spent by its own resource here.
// Hospitality (T05), observances (T04) and teaching (T06) serve through CultureSystem.Serve / Reserve / Unreserve.

/// <summary>A recipe as the culture may read it (a snapshot; changing it changes nothing).</summary>
public sealed class RecipeCultureInfo
{
    /// <summary>The recipe's stable id ("ash-loaf", "invented-3").</summary>
    public string id;
    /// <summary>The resource it makes (also the dish's display name).</summary>
    public string dish;
    public KitchenMethod method;
    /// <summary>Invented by the people (a custom recipe) rather than authored.</summary>
    public bool custom;
    public string technology;
    /// <summary>Its technology is known: it can be made now.</summary>
    public bool available;
    public string description;

    public CultureEntityRef Ref => CultureEntityRef.Of(CultureEntityKind.Recipe, id, dish);
}

/// <summary>What the culture reads of recipes (<see cref="CultureSystem"/> answers). No derivation or mutation API.</summary>
public interface IRecipeCultureRead
{
    /// <summary>A recipe by id (authored or invented), or null.</summary>
    RecipeCultureInfo RecipeInfo(string id);
    /// <summary>The recipe that makes <paramref name="resource"/>, or null (a food with no recipe stays a resource).</summary>
    RecipeCultureInfo RecipeOfDish(string resource);
    /// <summary>Every recipe the culture knows of, available or not.</summary>
    IReadOnlyList<RecipeCultureInfo> RecipeInfos();
}

/// <summary>The serving rules, with no scene state: merge a serving, and say why it cannot be served from what is held.</summary>
public static class CultureServing
{
    /// <summary>What a serving needs, one line per resource (duplicates summed, empty and non-positive lines dropped).</summary>
    public static List<ResourceAmount> Merge(IEnumerable<ResourceAmount> items) =>
        (items ?? Enumerable.Empty<ResourceAmount>())
            .Where(i => i != null && !string.IsNullOrEmpty(i.resource) && i.amount > 0f)
            .GroupBy(i => i.resource, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ResourceAmount { resource = g.First().resource, amount = g.Sum(i => i.amount) })
            .ToList();

    /// <summary>Why <paramref name="items"/> cannot all be served from what is held, or null (all of it is there).</summary>
    public static string WhyNot(IEnumerable<ResourceAmount> items, Func<string, float> held)
    {
        var merged = Merge(items);
        if (merged.Count == 0) return "Nothing to serve.";
        var missing = merged.Where(i => (held?.Invoke(i.resource) ?? 0f) + 1e-4f < i.amount)
            .Select(i => $"{i.amount:0.#} {i.resource} ({held?.Invoke(i.resource) ?? 0f:0.#} held)").ToList();
        return missing.Count == 0 ? null : "The stores lack " + string.Join(", ", missing) + ".";
    }

    /// <summary>
    /// Portions of <paramref name="resource"/> worth <paramref name="foodValue"/> (its pantry food value per unit), rounded
    /// up to a tenth so a feast is never short. Zero when the resource feeds nothing (it cannot set a table).
    /// </summary>
    public static float PortionsFor(float foodValue, float valuePerUnit) =>
        foodValue <= 0f || valuePerUnit <= 0f ? 0f : (float)Math.Ceiling(foodValue / valuePerUnit * 10f - 1e-4f) / 10f;

    /// <summary>Whether a stored food of this kind can be set on a table (a dish, a tea, a drink; never raw ingredients or spices).</summary>
    public static bool Servable(FoodClass? cuisine) =>
        cuisine == FoodClass.Edible || cuisine == FoodClass.EleosTea || cuisine == FoodClass.Beverage;

    public static string Text(IEnumerable<ResourceAmount> items)
    {
        var merged = Merge(items);
        return merged.Count == 0 ? "nothing" : string.Join(", ", merged.Select(i => $"{i.amount:0.#} {i.resource}"));
    }
}
