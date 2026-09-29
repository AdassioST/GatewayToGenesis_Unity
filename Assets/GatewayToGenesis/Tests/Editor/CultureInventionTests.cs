using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Dishes and drinks the people invent (<see cref="CultureInvention"/>, the national-food rules in
/// <see cref="CultureRules"/>, <see cref="CultureLifeTuning.WithInventions"/>): the closest known recipe decides how
/// one is treated, its ingredients (each by its share) decide what it does, the cellar's rules hold, and the people's
/// own are the most eligible to become national. Pure, so they also run outside Unity.
/// </summary>
public class CultureInventionTests
{
    // The kinds of Resources/Food/Pantry these tests use (proposals; copied, so no asset is loaded).
    private static readonly Dictionary<string, FoodKind> Kinds = new[]
    {
        Kind("Dried Auric Peaches", 1f, 0.02f, FoodClass.Edible, peach: true), Kind("Earth-Beans", 0.5f, 0.01f, FoodClass.Edible),
        Kind("Bitter Roots", 0.75f, 0.015f, FoodClass.Edible), Kind("Deep-Rooted Grain", 1.5f, 0.005f, FoodClass.Edible),
        Kind("Wild Honey", 1.5f, 0f, FoodClass.Ingredient), Kind("Glimmerfern", 0f, 0f, FoodClass.Spice),
        Kind("Eleos Tea", 0.5f, 0.005f, FoodClass.EleosTea), Kind("Candlevein Grief Tea", 0.5f, 0.005f, FoodClass.EleosTea),
        Kind("Honeyed Grain Porridge", 1.8f, 0.01f, FoodClass.Edible), Kind("Honeyed Peach Tart", 2.2f, 0.01f, FoodClass.Edible),
        Kind("Peach Soup", 2f, 0.01f, FoodClass.Edible), Kind("Peach Pits", 0.2f, 0f, FoodClass.Ingredient),
        Kind("Rootgrain Ale", 0.6f, 0.05f, FoodClass.Beverage), Kind("Bitterroot Spirit", 0.4f, 0f, FoodClass.Beverage),
        Kind("Wild Honey Mead", 0.6f, 0.004f, FoodClass.Beverage), Kind("Auric Peach Wine", 0.5f, 0.004f, FoodClass.Beverage, peach: true),
        Kind("Auric Peach Brandy", 0.4f, 0f, FoodClass.Beverage, peach: true),
    }.ToDictionary(k => k.resource);

    private static FoodKind Kind(string resource, float value, float spoil, FoodClass cuisine, bool peach = false) =>
        new FoodKind { resource = resource, foodValue = value, spoilPerSeventh = spoil, cuisine = cuisine, peach = peach };

    private static readonly CultureLifeTuning Life = new CultureLifeTuning();
    private static readonly CultureTuning Tuning = new CultureTuning();

    // What CultureSystem.FactsOf reads from the scene, read here from the tables alone (Glimmerfern is a bloom's).
    private static IngredientFacts Facts(string resource)
    {
        if (!Kinds.TryGetValue(resource, out var kind)) return null;
        var family = Tuning.FamilyOfResource(resource);
        return new IngredientFacts
        {
            kind = kind,
            leanings = family.HasValue ? new List<CultureLeaning> { new CultureLeaning { family = family.Value, share = 1f } } : new List<CultureLeaning>(),
            faithPerUnit = Life.Good(resource)?.faithPerUnit ?? 0f,
            luxuries = Life.luxuries.Where(l => l.resources.Contains(resource)).Select(l => l.category).ToList(),
            bloom = resource == "Glimmerfern",
        };
    }

    private static IEnumerable<string> LuxuriesOf(string dish) => Life.luxuries.Where(l => dish != null && l.resources.Contains(dish)).Select(l => l.category);

    private static List<ResourceAmount> In(params (string resource, float amount)[] inputs) => inputs.Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList();

    private static InventedRecipe Invent(string name, KitchenMethod method, List<ResourceAmount> inputs, IEnumerable<RecipeSpec> known = null)
    {
        var template = CultureInvention.Closest(method, inputs, known ?? Life.recipes, Facts);
        return CultureInvention.Derive("invented-1", name, method, inputs, template, Facts, LuxuriesOf, Life);
    }

    [Test]
    public void AnInventionIsMadeTheWayOfTheClosestKnownRecipe_AndDoesWhatItsIngredientsDo()
    {
        var inputs = In(("Wild Honey", 2f), ("Deep-Rooted Grain", 2f));
        Assert.IsNull(CultureInvention.WhyNot(KitchenMethod.Cook, inputs, Facts, Life.recipes, Life));
        var dish = Invent("Hearth Cake", KitchenMethod.Cook, inputs);
        Assert.AreEqual("honeyed-porridge", dish.template, "honey and grain: the porridge's way");
        Assert.AreEqual(FoodClass.Edible, dish.Cuisine);
        // The porridge makes 3 from 3: so does this, from 4.
        Assert.AreEqual(4f, dish.output, 1e-4f);
        // It feeds what went in (6 food value) times the porridge's gain (5.4 out of 4.5 in = 1.2), over 4.
        Assert.AreEqual(1.8f, dish.foodValue, 1e-3f);
        // It keeps as its ingredients do (half honey, half grain: 0.0025), as much better as the porridge keeps than its own (0.01 against 0.00333).
        Assert.AreEqual(0.0075f, dish.spoilPerSeventh, 1e-4f);
        // Each ingredient leans the culture by its share: honey Indulgent, grain Agromagical.
        Assert.AreEqual(0.5f, dish.leanings.Single(l => l.family == EnclaveFamily.Indulgent).share, 1e-3f);
        Assert.AreEqual(0.5f, dish.leanings.Single(l => l.family == EnclaveFamily.Agromagical).share, 1e-3f);
        CollectionAssert.AreEqual(new[] { "Sweets" }, dish.luxuries, "the porridge's luxury, and honey's (half of it)");
        Assert.IsFalse(dish.peach);
        Assert.AreEqual(Life.Recipe("honeyed-porridge").technology, dish.technology);
        StringAssert.Contains("Honeyed Grain Porridge", dish.description);
    }

    [Test]
    public void EveryEffectIsScaledDownByItsShareOfTheBatch()
    {
        // A sacred tea gives Faith and leans Esoteric; beans dilute both.
        var some = Invent("Grief Broth", KitchenMethod.Cook, In(("Candlevein Grief Tea", 1f), ("Earth-Beans", 1f)));
        var more = Invent("Thin Grief Broth", KitchenMethod.Cook, In(("Candlevein Grief Tea", 1f), ("Earth-Beans", 3f)));
        Assert.AreEqual(0.2f, some.faithPerUnit, 1e-3f, "half of the tea's 0.4");
        Assert.AreEqual(0.1f, more.faithPerUnit, 1e-3f, "a quarter of it");
        Assert.AreEqual(0.5f, some.leanings.Single(l => l.family == EnclaveFamily.Esoteric).share, 1e-3f);
        Assert.AreEqual(0.25f, more.leanings.Single(l => l.family == EnclaveFamily.Esoteric).share, 1e-3f);
        // An ingredient lends its luxury only when it makes up enough of the batch.
        var sweet = Invent("Honey Beans", KitchenMethod.Cook, In(("Wild Honey", 1f), ("Earth-Beans", 1f)));
        var plain = Invent("Bean Pot", KitchenMethod.Cook, In(("Wild Honey", 1f), ("Earth-Beans", 9f)));
        CollectionAssert.Contains(sweet.luxuries, "Sweets");
        CollectionAssert.DoesNotContain(plain.luxuries, "Sweets", "a spoon of honey in a pot of beans is no sweet");
        // Mostly peach is still the peach (the monocrop); a little is not.
        Assert.IsTrue(Invent("Peach Mash", KitchenMethod.Cook, In(("Dried Auric Peaches", 3f), ("Earth-Beans", 1f))).peach);
        Assert.IsFalse(Invent("Bean Mash", KitchenMethod.Cook, In(("Dried Auric Peaches", 1f), ("Earth-Beans", 3f))).peach);
    }

    [Test]
    public void DrinksAreTreatedAsTheCellarsOwn()
    {
        var mead = Invent("Grain Mead", KitchenMethod.Ferment, In(("Wild Honey", 3f), ("Deep-Rooted Grain", 1f)));
        Assert.AreEqual("honey-mead", mead.template);
        Assert.AreEqual(FoodClass.Beverage, mead.Cuisine);
        CollectionAssert.Contains(mead.luxuries, "Wines & Spirits");
        // A spirit keeps for ever because the still's spirits do, whatever went in.
        var spirit = Invent("Bean Fire", KitchenMethod.Distill, In(("Rootgrain Ale", 2f), ("Earth-Beans", 1f)));
        Assert.AreEqual("bitterroot-spirit", spirit.template);
        Assert.AreEqual(0f, spirit.spoilPerSeventh);
        Assert.AreEqual(FoodClass.Beverage, spirit.Cuisine);
        var brandy = Invent("Pit Brandy", KitchenMethod.Distill, In(("Auric Peach Wine", 2f), ("Peach Pits", 2f)));
        Assert.AreEqual("peach-brandy", brandy.template);
        Assert.IsTrue(brandy.peach, "still the peach");
    }

    [Test]
    public void WhatCannotBeInvented()
    {
        string Why(KitchenMethod m, params (string, float)[] inputs) => CultureInvention.WhyNot(m, In(inputs), Facts, Life.recipes, Life);
        Assert.IsNotNull(Why(KitchenMethod.Cook), "nothing in it");
        Assert.IsNotNull(Why(KitchenMethod.Cook, ("Glimmerfern", 1f)), "spices alone");
        StringAssert.Contains("Honeyed Grain Porridge", Why(KitchenMethod.Cook, ("Deep-Rooted Grain", 4f), ("Wild Honey", 2f)), "the same mix is a dish already");
        Assert.IsNotNull(Why(KitchenMethod.Cook, ("Unknown Stone", 1f)), "not something the stores keep");
        Assert.IsNotNull(Why(KitchenMethod.Cook, ("Earth-Beans", 1f), ("Bitter Roots", 1f), ("Wild Honey", 1f), ("Deep-Rooted Grain", 1f), ("Peach Pits", 1f)), "too many ingredients");
        Assert.IsNull(Why(KitchenMethod.Cook, ("Eleos Tea", 1f), ("Earth-Beans", 1f)), "the kitchen may cook with a tea");
        StringAssert.Contains("Eleos", Why(KitchenMethod.Ferment, ("Eleos Tea", 1f), ("Deep-Rooted Grain", 1f)), "never brewed from a tea");
        StringAssert.Contains("Eleos", Why(KitchenMethod.Ferment, ("Glimmerfern", 1f), ("Deep-Rooted Grain", 1f)), "never brewed from a bloom");
        StringAssert.Contains("fermented", Why(KitchenMethod.Distill, ("Deep-Rooted Grain", 2f)), "a spirit is distilled from a drink");
        // A people that knows no still cannot invent a spirit.
        var noStill = Life.recipes.Where(r => r.method != KitchenMethod.Distill).ToList();
        Assert.IsNotNull(CultureInvention.WhyNot(KitchenMethod.Distill, In(("Rootgrain Ale", 2f)), Facts, noStill, Life));
        Assert.IsNull(CultureInvention.WhyNot(KitchenMethod.Distill, In(("Rootgrain Ale", 2f)), Facts, Life.recipes, Life));
    }

    [Test]
    public void AnInventionIsARecipeAPantryKindAndALuxury_WithoutChangingTheAuthoredNumbers()
    {
        var dish = Invent("Hearth Cake", KitchenMethod.Cook, In(("Wild Honey", 2f), ("Deep-Rooted Grain", 2f)));
        var recipe = dish.ToRecipe();
        Assert.AreEqual(dish.id, recipe.id);
        Assert.AreEqual("Hearth Cake", recipe.dish);
        Assert.AreEqual(dish.output, recipe.output);
        Assert.AreEqual(2, recipe.inputs.Count);
        var kind = dish.ToKind();
        Assert.AreEqual(FoodClass.Edible, kind.cuisine);
        Assert.AreEqual(dish.foodValue, kind.foodValue);

        int recipes = Life.recipes.Count, sweets = Life.luxuries.Single(l => l.category == "Sweets").resources.Count;
        var with = Life.WithInventions(new[] { dish });
        Assert.IsTrue(with.IsDish("Hearth Cake"));
        Assert.AreEqual(recipe.dish, with.Recipe(dish.id).dish);
        CollectionAssert.Contains(with.luxuries.Single(l => l.category == "Sweets").resources, "Hearth Cake");
        Assert.AreEqual(recipes, Life.recipes.Count, "the authored recipes are untouched");
        Assert.AreEqual(sweets, Life.luxuries.Single(l => l.category == "Sweets").resources.Count, "and the authored luxuries");
        Assert.AreEqual("invented-2", CultureInvention.NextId(new[] { dish }));
    }

    [Test]
    public void ThePeoplesOwnAreTheMostEligibleToBecomeNational()
    {
        var t = new CultureTuning();
        // Familiarity: their own grows faster at the same share of the table.
        var own = new List<Foodway>();
        var other = new List<Foodway>();
        CultureRules.Taste(own, new[] { new DietEntry("Hearth Cake", FoodClass.Edible, 10f, 0f, invented: true) }, t);
        CultureRules.Taste(other, new[] { new DietEntry("Peach Soup", FoodClass.Edible, 10f, 0f) }, t);
        Assert.IsTrue(own[0].invented);
        Assert.Greater(own[0].familiarity, other[0].familiarity);
        // At the table: their own comes first even with a little less of it held.
        var table = new List<Foodway>();
        CultureRules.Taste(table, new[] { new DietEntry("Peach Soup", FoodClass.Edible, 10f, 0f), new DietEntry("Hearth Cake", FoodClass.Edible, 8f, 0f, invented: true) }, t);
        Assert.AreEqual(1, table.Single(f => f.resource == "Hearth Cake").firstSevenths);
        Assert.AreEqual(0, table.Single(f => f.resource == "Peach Soup").firstSevenths);
        // Offered sooner, and first: at 70% of the familiarity and Sevenths another food needs.
        var soup = new Foodway { resource = "Peach Soup", cuisine = FoodClass.Edible, familiarity = t.nationalFamiliarity, firstSevenths = t.nationalSevenths };
        var cake = new Foodway { resource = "Hearth Cake", cuisine = FoodClass.Edible, invented = true, familiarity = t.nationalFamiliarity * t.inventedNationalEase, firstSevenths = (int)System.Math.Ceiling(t.nationalSevenths * t.inventedNationalEase) };
        Assert.AreSame(cake, CultureRules.NextNational(new[] { soup, cake }, t), "their own before anything else");
        var early = new Foodway { resource = "Hearth Cake", cuisine = FoodClass.Edible, invented = true, familiarity = t.nationalFamiliarity * t.inventedNationalEase, firstSevenths = 3 };
        Assert.AreSame(soup, CultureRules.NextNational(new[] { soup, early }, t), "not before its Sevenths");
        var tea = new Foodway { resource = "Eleos Tea", cuisine = FoodClass.EleosTea, familiarity = 1f, firstSevenths = 99 };
        var brew = new Foodway { resource = "Grain Mead", cuisine = FoodClass.Beverage, invented = true, familiarity = 0.4f, firstSevenths = 15 };
        Assert.AreSame(brew, CultureRules.NextNational(new[] { tea, brew }, t), "an invented drink before a tea, whatever the table's order");
    }
}
