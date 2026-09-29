using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a table's serving plan reads: the stores, as the pantry holds them.</summary>
public interface IServingStores
{
    /// <summary>How much of a resource is held.</summary>
    float Held(string resource);
    /// <summary>The pantry's kind for a resource (its class and food value), or null when it is not stored food.</summary>
    FoodKind Kind(string resource);
    /// <summary>The stores' whole food value now.</summary>
    float StoredValue { get; }
}

/// <summary>One food of a serving plan: exactly how much of which resource, and what it is worth.</summary>
public sealed class ServingItem
{
    public string resource;
    public FoodKind kind;
    public float amount;
    public float foodValue;
}

/// <summary>What a serving would take (exact resources, not generic food value), or why it cannot be planned.</summary>
public sealed class ServingPlan
{
    public readonly List<ServingItem> items = new List<ServingItem>();
    /// <summary>Why the menu itself is not servable (null: it is).</summary>
    public string invalid;
    public float FoodValue => items.Sum(i => i.foodValue);

    /// <summary>The plan as the serving boundary takes it (<see cref="CultureSystem.Serve(IEnumerable{ResourceAmount}, CulturalOccurrence)"/>).</summary>
    public List<ResourceAmount> Items() => items.Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList();
}

/// <summary>
/// A communal table's menu and its policy checks (T05), with no scene. A plan names each finished food and its exact
/// amount; the checks keep the people's survival first (no table while they go hungry, none that draws the stores below
/// the reserve) and serve only finished foods: edibles, teas and beverages the pantry knows. Ingredients and spices go
/// through the kitchen and its recipe checks first. The spend itself is never here: it goes through the culture's one
/// serving boundary (<see cref="CultureServing"/>, <c>CultureSystem.Serve</c>), all or nothing, which also tells the
/// foodways of the use once and never reports it as eaten.
/// </summary>
public static class TableServing
{
    private const float Epsilon = 1e-4f;

    /// <summary>Why a resource cannot be served at a table, or null: it must be a finished food with a food value.</summary>
    public static string WhyNotServable(string resource, FoodKind kind)
    {
        if (string.IsNullOrEmpty(resource)) return "No food chosen.";
        if (kind == null) return $"{resource} is not a food the stores keep.";
        switch (kind.cuisine)
        {
            case FoodClass.Ingredient: return $"{resource} is an ingredient: cook it into a dish first (the Kitchen), where its recipe is checked.";
            case FoodClass.Spice: return $"{resource} is a spice: it flavours dishes, it is not served on its own.";
        }
        if (!CultureServing.Servable(kind.cuisine)) return $"{resource} is not a dish, a tea or a drink.";
        if (kind.foodValue <= 0f) return $"{resource} feeds no one.";
        return null;
    }

    /// <summary>
    /// Plan a serving of <paramref name="foodValue"/> food value split evenly across <paramref name="menu"/> (each
    /// food brings its share; portions follow from its own value). Nothing is taken.
    /// </summary>
    public static ServingPlan Plan(IList<string> menu, float foodValue, IServingStores stores, int maxMenu = 3)
    {
        var plan = new ServingPlan();
        var list = (menu ?? new List<string>()).Where(m => !string.IsNullOrEmpty(m)).ToList();
        if (list.Count == 0) { plan.invalid = "Choose at least one food to serve."; return plan; }
        if (list.Count > Math.Max(1, maxMenu)) { plan.invalid = $"A table serves {Math.Max(1, maxMenu)} foods at most."; return plan; }
        if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count) { plan.invalid = "Each food is chosen once."; return plan; }
        float share = Math.Max(0f, foodValue) / list.Count;
        foreach (string resource in list)
        {
            var kind = stores?.Kind(resource);
            string why = WhyNotServable(resource, kind);
            if (why != null) { plan.invalid = why; plan.items.Clear(); return plan; }
            plan.items.Add(new ServingItem { resource = kind.resource ?? resource, kind = kind, amount = share / kind.foodValue, foodValue = share });
        }
        return plan;
    }

    /// <summary>
    /// Why <paramref name="plan"/> cannot be served now, or null: the menu, the people's hunger, each food held in full
    /// (<see cref="CultureServing.WhyNot"/>, the boundary's own check), and the survival reserve (<paramref name="reserve"/>
    /// food value) left untouched.
    /// </summary>
    public static string WhyNot(ServingPlan plan, IServingStores stores, float reserve, bool hungry)
    {
        if (plan == null) return "Nothing planned.";
        if (plan.invalid != null) return plan.invalid;
        if (plan.items.Count == 0) return "Choose at least one food to serve.";
        if (stores == null) return "There are no stores.";
        if (hungry) return "The people are going hungry: the stores feed them first, and no table is set until Food stops falling.";
        string lacking = CultureServing.WhyNot(plan.Items(), stores.Held);
        if (lacking != null) return lacking;
        float after = stores.StoredValue - plan.FoodValue;
        if (reserve > 0f && after + Epsilon < reserve)
            return $"The stores must keep {reserve:0.#} food value for survival: this table would leave {Math.Max(0f, after):0.#}.";
        return null;
    }

    /// <summary>
    /// Settle <paramref name="plan"/> on what the serving boundary actually took (<paramref name="paid"/>): each item's
    /// amount and food value become the spent ones, so the record never claims more than left the stores.
    /// </summary>
    public static void Settle(ServingPlan plan, IEnumerable<ResourceAmount> paid)
    {
        if (plan == null) return;
        var spent = CultureServing.Merge(paid);
        foreach (var item in plan.items)
        {
            var line = spent.FirstOrDefault(p => string.Equals(p.resource, item.resource, StringComparison.OrdinalIgnoreCase));
            item.amount = line != null ? line.amount : 0f;
            item.foodValue = item.amount * Math.Max(0f, item.kind?.foodValue ?? 0f);
        }
    }
}

/// <summary>The scene's stores for table plans: the resources the pantry keeps.</summary>
public sealed class PantryServingStores : IServingStores
{
    public static readonly PantryServingStores Instance = new PantryServingStores();

    public float Held(string resource) => GameUnitsLogic.Instance != null && !string.IsNullOrEmpty(resource) ? GameUnitsLogic.Instance.GetResourceAmountExact(resource) : 0f;

    public FoodKind Kind(string resource) => Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(resource) : null;

    public float StoredValue => Pantry.StoredValue;
}
