using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One kind of stored food and how much of it is held.</summary>
public struct FoodStock
{
    public FoodKind kind;
    public float amount;

    public FoodStock(FoodKind kind, float amount)
    {
        this.kind = kind;
        this.amount = amount;
    }

    public float Value => kind != null ? Math.Max(0f, amount) * Math.Max(0f, kind.foodValue) : 0f;
}

/// <summary>
/// The arithmetic of the stores, with no scene state (tested in <c>PantryTests</c>): food value, what to take first
/// when the stores are drawn on (the most perishable, so little is wasted), spoilage, how much of a Food shortfall
/// the stores cover, and the variety the famine reads.
/// </summary>
public static class PantryRules
{
    /// <summary>Food value of everything held (spices feed no one, so they count for nothing).</summary>
    public static float Value(IEnumerable<FoodStock> stock) => stock?.Where(s => s.kind == null || CultureRules.Feeds(s.kind.cuisine)).Sum(s => s.Value) ?? 0f;

    /// <summary>What eating <paramref name="amount"/> of a kind leaves for the kitchen (Peach Pits from dried peaches): (resource, amount), or null.</summary>
    public static (string resource, float amount)? Leftover(FoodKind kind, float amount)
    {
        if (kind == null || string.IsNullOrEmpty(kind.leaves) || kind.leavesPerUnit <= 0f || amount <= 0f) return null;
        return (kind.leaves, amount * kind.leavesPerUnit);
    }

    /// <summary>
    /// What to take to raise <paramref name="value"/> of food value: edibles and teas before ingredients, and the
    /// cellar's beverages last (spices are never eaten to fill a belly; <see cref="CultureRules.EatingTier"/>), and within them the most perishable kinds first (then the least valuable, so rich food is
    /// kept). Takes everything edible when the stores hold less. Returns (resource, amount) pairs.
    /// </summary>
    public static List<(string resource, float amount)> Draw(IEnumerable<FoodStock> stock, float value)
    {
        var taken = new List<(string, float)>();
        float need = Math.Max(0f, value);
        if (stock == null || need <= 0f) return taken;
        foreach (var s in stock.Where(s => s.kind != null && s.kind.foodValue > 0f && s.amount > 0f && CultureRules.Feeds(s.kind.cuisine))
                     .OrderBy(s => CultureRules.EatingTier(s.kind.cuisine)).ThenByDescending(s => s.kind.spoilPerSeventh).ThenBy(s => s.kind.foodValue).ThenBy(s => s.kind.resource, StringComparer.Ordinal))
        {
            if (need <= 1e-6f) break;
            float amount = Math.Min(s.amount, need / s.kind.foodValue);
            taken.Add((s.kind.resource, amount));
            need -= amount * s.kind.foodValue;
        }
        return taken;
    }

    /// <summary>Stock lost to spoilage per second: a share per Seventh, softened by preservation.</summary>
    public static float SpoilPerSecond(float amount, float spoilPerSeventh, float preservationMultiplier, float secondsPerSeventh)
    {
        if (amount <= 0f || secondsPerSeventh <= 0f) return 0f;
        return amount * Math.Max(0f, spoilPerSeventh) * Math.Max(0f, preservationMultiplier) / secondsPerSeventh;
    }

    /// <summary>
    /// Food value per second the stores feed in while Food is falling: the whole shortfall, as far as the stores and
    /// the cap reach (<paramref name="storesValue"/> must last at least the step, <paramref name="seconds"/>). 0 while
    /// Food is not falling.
    /// </summary>
    public static float Cover(float netWithoutStores, float storesValue, float maxPerSecond, float seconds)
    {
        if (netWithoutStores >= 0f || storesValue <= 0f) return 0f;
        float lasting = storesValue / Math.Max(0.01f, seconds);
        return Math.Min(-netWithoutStores, Math.Min(Math.Max(0f, maxPerSecond), lasting));
    }

    /// <summary>
    /// Food to move into the stores now: what is held beyond <paramref name="keep"/>, as far as the kind's room allows
    /// (<paramref name="held"/> of <paramref name="room"/>, one Food making 1 / <paramref name="foodValue"/> of it). 0 when
    /// there is no surplus or no room.
    /// </summary>
    public static float Banked(float food, float keep, float held, float room, float foodValue)
    {
        if (foodValue <= 0f || float.IsNaN(food)) return 0f;
        float surplus = food - Math.Max(0f, keep);
        float space = Math.Max(0f, room - Math.Max(0f, held)) * foodValue;
        return Math.Max(0f, Math.Min(surplus, space));
    }

    /// <summary>Kinds held with at least <paramref name="minimumValue"/> of food value.</summary>
    public static int Variety(IEnumerable<FoodStock> stock, float minimumValue) => stock?.Count(s => s.Value >= Math.Max(0.01f, minimumValue)) ?? 0;

    /// <summary>Share of the stores' food value that is Auric peaches (0 with empty stores).</summary>
    public static float PeachShare(IEnumerable<FoodStock> stock)
    {
        var list = stock?.ToList() ?? new List<FoodStock>();
        float total = Value(list);
        return total <= 0f ? 0f : list.Where(s => s.kind != null && s.kind.peach).Sum(s => s.Value) / total;
    }
}
