using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The arithmetic of the food stores (<see cref="PantryRules"/>), with no scene: food value by kind (earth-beans 0.5,
/// Behemoth Meat 5), what is drawn first, spoilage, how much of a Food shortfall the stores cover, and the variety and
/// peach share the famine reads.
/// </summary>
public class PantryTests
{
    private static readonly FoodKind Peaches = new FoodKind { resource = "Dried Auric Peaches", foodValue = 1f, spoilPerSeventh = 0.02f, peach = true };
    private static readonly FoodKind Beans = new FoodKind { resource = "Earth-Beans", foodValue = 0.5f, spoilPerSeventh = 0.01f };
    private static readonly FoodKind Meat = new FoodKind { resource = "Behemoth Meat", foodValue = 5f, spoilPerSeventh = 0.1f };
    private static readonly FoodKind AshBread = new FoodKind { resource = "Ash-Bread", foodValue = 0.3f, spoilPerSeventh = 0f };

    [Test]
    public void FoodValueWeighsEachKind()
    {
        var stock = new List<FoodStock> { new FoodStock(Beans, 10f), new FoodStock(Meat, 2f), new FoodStock(Peaches, 3f) };
        Assert.AreEqual(5f + 10f + 3f, PantryRules.Value(stock), 1e-4f, "10 earth-beans feed like 5 Food, 2 Behemoth Meat like 10");
    }

    [Test]
    public void DrawingTakesTheMostPerishableFirstAndOnlyWhatIsNeeded()
    {
        var stock = new List<FoodStock> { new FoodStock(Beans, 20f), new FoodStock(Meat, 1f), new FoodStock(AshBread, 50f) };
        var taken = PantryRules.Draw(stock, 7f);
        Assert.AreEqual("Behemoth Meat", taken[0].resource, "meat spoils fastest: eaten first");
        Assert.AreEqual(1f, taken[0].amount, 1e-4f);
        Assert.AreEqual("Earth-Beans", taken[1].resource);
        Assert.AreEqual(4f, taken[1].amount, 1e-4f, "the last 2 food value is 4 earth-beans");
        Assert.AreEqual(2, taken.Count, "ash-bread, which never spoils, is kept");
        Assert.AreEqual(7f, taken.Sum(t => t.amount * (t.resource == "Behemoth Meat" ? 5f : 0.5f)), 1e-4f);
    }

    [Test]
    public void DrawingMoreThanTheStoresHoldTakesEverything()
    {
        var stock = new List<FoodStock> { new FoodStock(Peaches, 3f) };
        var taken = PantryRules.Draw(stock, 10f);
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual(3f, taken[0].amount, 1e-4f);
        Assert.IsEmpty(PantryRules.Draw(stock, 0f));
    }

    [Test]
    public void SpoilageIsAShareOfTheStockPerSeventhSoftenedByPreservation()
    {
        Assert.AreEqual(100f * 0.1f / 180f, PantryRules.SpoilPerSecond(100f, 0.1f, 1f, 180f), 1e-6f);
        Assert.AreEqual(100f * 0.1f * 0.5f / 180f, PantryRules.SpoilPerSecond(100f, 0.1f, 0.5f, 180f), 1e-6f, "preservation halves it");
        Assert.AreEqual(0f, PantryRules.SpoilPerSecond(0f, 0.1f, 1f, 180f));
        Assert.AreEqual(0f, PantryRules.SpoilPerSecond(50f, 0f, 1f, 180f), "ash-bread never spoils");
    }

    [Test]
    public void TheStoresCoverAFallingFoodAsFarAsTheyReach()
    {
        Assert.AreEqual(0f, PantryRules.Cover(0.5f, 100f, 5f, 0.5f), "Food rising: nothing drawn");
        Assert.AreEqual(1.5f, PantryRules.Cover(-1.5f, 100f, 5f, 0.5f), 1e-5f, "the whole shortfall");
        Assert.AreEqual(5f, PantryRules.Cover(-9f, 100f, 5f, 0.5f), 1e-5f, "at most the cap");
        Assert.AreEqual(2f, PantryRules.Cover(-9f, 1f, 5f, 0.5f), 1e-5f, "no more than the stores can give over the step");
        Assert.AreEqual(0f, PantryRules.Cover(-9f, 0f, 5f, 0.5f), "empty stores cover nothing");
    }

    [Test]
    public void VarietyAndPeachShareReadTheStoresValue()
    {
        var stock = new List<FoodStock> { new FoodStock(Peaches, 30f), new FoodStock(Beans, 20f), new FoodStock(Meat, 0.5f) };
        Assert.AreEqual(3, PantryRules.Variety(stock, 2.5f), "meat's 2.5 value counts at the 2.5 minimum");
        Assert.AreEqual(2, PantryRules.Variety(stock, 5f));
        Assert.AreEqual(30f / 42.5f, PantryRules.PeachShare(stock), 1e-4f);
        Assert.AreEqual(0f, PantryRules.PeachShare(new List<FoodStock>()));
    }

    [Test]
    public void VariedStoresEaseTheFamineAndPeachOnlyStoresAggravateIt()
    {
        var tuning = new CrisisTuning();
        float varied = CrisisRules.Factors(new CrisisInputs { population = 20, storeVariety = 4 }, tuning, CrisisRules.Labels.Default).First(f => f.label == "Varied stores").value;
        Assert.AreEqual(-System.Math.Min(tuning.storeVarietyCap, 3 * tuning.perStoreKind), varied, 1e-5f, "every kind beyond the first counts");
        var peachy = CrisisRules.Factors(new CrisisInputs { population = 20, peachShare = 1f }, tuning, CrisisRules.Labels.Default);
        Assert.AreEqual(tuning.monocropStoreWeight, peachy.First(f => f.label == "Stores of peaches alone").value, 1e-5f);
        Assert.IsFalse(CrisisRules.Factors(new CrisisInputs { population = 20, peachShare = tuning.monocropStoreThreshold }, tuning, CrisisRules.Labels.Default).Any(f => f.label == "Stores of peaches alone"));
    }
}
