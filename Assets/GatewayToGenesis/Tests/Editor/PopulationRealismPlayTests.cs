using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PopulationRealismPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

    [UnityTest]
    public IEnumerator RealSceneConservesPeopleAndBoundsCrowdAcrossSavesAndCitySizes()
    {
        EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (WorldSystem.Instance?.Map == null || PopGrowthLogic.Instance == null || GlobalCharacterManager.Instance == null); i++) yield return null;
        Assert.IsNotNull(PopGrowthLogic.Instance);
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1f;
        var calendar = TimeSystemLogic.Instance;
        calendar.isTimePaused = true;
        var pop = PopGrowthLogic.Instance;
        pop.enabled = false;
        pop.CancelInvoke();
        var crowd = GlobalCharacterManager.Instance;
        var food = GameUnitsLogic.Instance.GetResourceSlotFromName(GameCatalog.ResourceNameFor(ResourceRole.Food));
        Assert.IsNotNull(food);
        food.amount = 0;

        // Food cannot create births, even via repeated external deliveries.
        pop.population = 25; pop.vagrants = 0; pop.housing = 100;
        for (int i = 0; i < 100; i++) pop.HandleExternalFoodChange(0, 200);
        Assert.AreEqual(25, pop.TotalPeople);
        Assert.AreEqual(float.MaxValue, pop.GetMaxUsefulFoodGain(200));

        // Save/load carries fractional vital events and the finite survivor pool exactly.
        Call(pop, "InitializeDemography");
        typeof(PopGrowthLogic).GetField("_birthCarry", Private).SetValue(pop, 0.75d);
        var fields = GameSnapshot.Schema[typeof(PopGrowthLogic)];
        var state = SaveStateCodec.Capture(pop, fields);
        typeof(PopGrowthLogic).GetField("_birthCarry", Private).SetValue(pop, 0d);
        SaveStateCodec.Restore(pop, state, fields);
        Assert.AreEqual(0.75d, (double)typeof(PopGrowthLogic).GetField("_birthCarry", Private).GetValue(pop));
        pop.RestoreDemography(true);
        Assert.AreEqual(0, pop.WaitingMigrants, "an old save never gets a second founding group");

        // Everyone beyond the provisioned founders eats, and no citizen dies just because a real-time second passed with empty stores.
        calendar.canTrackTime = true; calendar.isTimePaused = false;
        pop.population = 20; pop.vagrants = 5;
        Call(pop, "UpdateFoodDemand");
        float demand = (float)typeof(PopGrowthLogic).GetField("_foodDemand", Private).GetValue(pop);
        Assert.AreEqual(GrowthRules.FoodDemand(GrowthRules.Eating(25, pop.Growth), calendar.GetEffectiveSecondsPerSeventh(), pop.Growth, pop.demandModifier), demand, 1e-5, "the provisioned founders eat nothing from the daily food");
        int before = pop.TotalPeople;
        Call(pop, "ProcessPopulationChanges");
        Assert.AreEqual(before, pop.TotalPeople);
        calendar.isTimePaused = true;

        // Proportional losses include people without a home and preserve the death ledger.
        pop.population = 80; pop.vagrants = 20;
        int trueDeaths = pop.trueDeaths;
        pop.LosePopulationPercent(10, "test event");
        Assert.AreEqual(90, pop.TotalPeople);
        Assert.AreEqual(trueDeaths + 10, pop.trueDeaths);

        // A band found by an expedition walks home, then waits at the gates; the road and the gates survive a save.
        int gates = pop.WaitingMigrants;
        pop.SurvivorsFound(4, 2, "test");
        Assert.AreEqual(4, pop.PeopleOnTheRoad);
        var roadState = SaveStateCodec.Capture(pop, fields);
        SaveStateCodec.Restore(pop, roadState, fields);
        Assert.AreEqual(4, pop.PeopleOnTheRoad, "bands on the road are saved");
        Call(pop, "AdvanceBands");
        Assert.AreEqual(gates, pop.WaitingMigrants, "still a Seventh away");
        Call(pop, "AdvanceBands");
        Assert.AreEqual(gates + 4, pop.WaitingMigrants);
        Assert.AreEqual(0, pop.PeopleOnTheRoad);

        // A birth comes from the calendar, is counted apart from arrivals, and is witnessed: "There is Beauty in That".
        pop.population = 1000; pop.vagrants = 0; pop.housing = 1000; pop.isFoodScarce = false;
        typeof(PopGrowthLogic).GetField("_birthCarry", Private).SetValue(pop, 0.9999d);
        int births = pop.Births;
        Call(pop, "OnDemographicSeventh", 1);
        Assert.Greater(pop.Births, births, "a Seventh with a whole birth due adds a child");
        if (SaveSession.Current != null) Assert.IsTrue(Achievements.Tracker.IsUnlocked("there-is-beauty-in-that"), "Witness the first birth of your Civilization");

        // Housing does not acquire an exponential material bill as the town grows.
        var hutUnit = GameCatalog.Units.Get("Decaying Hut", "Population test");
        GameUnitsLogic.Instance.productionTab.AddNewUnit(hutUnit);
        yield return null;
        var hut = GameUnitsLogic.Instance.GetProductionSlot("Decaying Hut");
        Assert.IsNotNull(hut);
        hut.maxAmount = 0;
        float hutCost = hut.GetEffectiveConstructionCost(0);
        hut.maxAmount = 100000;
        Assert.AreEqual(hutCost, hut.GetEffectiveConstructionCost(0));
        hut.maxAmount = 0;

        foreach (int people in new[] { 25, 5000, 300000, 0, 300000, 25 })
        {
            pop.population = people; pop.vagrants = 0; pop.housing = Math.Max(35, people);
            crowd.Resync();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.LessOrEqual(crowd.AllocatedCount, crowd.FigureLimit);
            Assert.AreEqual(CrowdRules.Near(people, 40) + CrowdRules.Far(people, 40, 160, 20f, 300000), crowd.Count);
            if (people == 25 || people == 5000 || people == 300000) Capture(people);
        }
        yield return new ExitPlayMode();
    }

    private static void Capture(int people)
    {
        string directory = Environment.GetEnvironmentVariable("G2G_POPULATION_SHOTS");
        if (string.IsNullOrEmpty(directory) || Camera.main == null) return;
        Directory.CreateDirectory(directory);
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(directory, "population-" + people + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(texture); Object.Destroy(target);
        }
    }
}
