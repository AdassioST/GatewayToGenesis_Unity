using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The life of the culture in the real scene (<see cref="CultureSystem"/>, CultureSystem.Life.cs): a founded culture
/// makes Unity; the kitchen cooks a dish (once and by standing order); a rite gives Unity and waits its cooldown; a
/// festival in the Capital; a holiday set apart on today's date and kept again the next Echo; the culture names a
/// district; cultural parties wait for their technology; and all of it survives a save.
/// </summary>
public class CultureLifePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static float Held(string resource) => GameUnitsLogic.Instance.GetResourceAmountExact(resource);

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    [UnityTest]
    public IEnumerator AFoundedCultureCooksCelebratesAndKeepsAHoliday()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Pantry.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var culture = CultureSystem.Instance;
        Assert.IsNotNull(culture.WhyNotCook(culture.Life.Recipe("peach-soup")), "no Flavor Log before the founding");
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        yield return null;

        // A people of the Song makes Unity, shown as a resource.
        var unity = culture.UnityPerSeventh();
        Assert.Greater(unity.song, culture.Life.songMythUnity - 1e-4f, "the Song myth sings");
        Assert.Greater(unity.Total, 0f);
        Assert.IsNotNull(GameUnitsLogic.Instance.GetResourceSlotFromName("Unity"), "Unity has its place in storage");

        // The kitchen: a batch of Peach Soup feeds more than what went in; a standing order cooks every Seventh.
        Give("Dried Auric Peaches", 20f);
        Give("Earth-Beans", 10f);
        float soup = Held("Peach Soup");
        Assert.IsNull(culture.WhyNotCook(culture.Life.Recipe("peach-soup")));
        Assert.AreEqual(1, culture.Cook("peach-soup"));
        Assert.AreEqual(soup + 2f, Held("Peach Soup"), 0.01f);
        Assert.IsTrue(Pantry.IsStoredFood("Peach Soup"), "a dish is stored food");
        culture.SetStandingOrder("peach-soup", 2);
        Assert.AreEqual(2, culture.StandingOrder("peach-soup"));
        culture.Tick();
        Assert.AreEqual(soup + 6f, Held("Peach Soup"), 0.3f, "two more batches on the Seventh (a little may spoil)");
        Assert.Greater(culture.DishesCooked, 5.9f);
        culture.SetStandingOrder("peach-soup", 0);

        // Cultural goods use the same stock and Seventh as the kitchen; lasting glass is not spent.
        var people = PopGrowthLogic.Instance;
        int oldPopulation = people.population;
        bool oldScarcity = people.isFoodScarce;
        people.population = 400;
        people.isFoodScarce = false;
        Give("Sky Glass", 10f);
        Give("Eleos Tea", 20f);
        float glassBefore = Held("Sky Glass"), teaBefore = Held("Eleos Tea"), faithBefore = Held("Faith");
        typeof(CultureSystem).GetMethod("ConsumeLuxuries", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(culture, null);
        Assert.AreEqual(glassBefore, Held("Sky Glass"), 0.001f);
        Assert.Greater(culture.State.culturalFaith, 0f);
        Assert.AreEqual(faithBefore + culture.State.culturalFaith, Held("Faith"), 0.001f);
        Assert.Less(Held("Eleos Tea"), teaBefore);
        people.population = oldPopulation;
        people.isFoodScarce = oldScarcity;

        // A rite: Unity now, then it waits its cooldown.
        Give("Deep-Rooted Grain", 60f);
        float before = culture.UnityHeld;
        var song = culture.Life.Activity("song");
        Assert.IsNull(culture.WhyNotActivity(song));
        Assert.IsTrue(culture.HoldActivity("song"));
        Assert.AreEqual(before + song.unity, culture.UnityHeld, 0.5f);
        Assert.IsNotNull(culture.WhyNotActivity(song), "held not long ago");
        Assert.Greater(culture.Joy, 0f);

        // A festival in the Capital: its table from the stores, Unity, one festival held.
        var capital = WorldCivilization.Capital(WorldSystem.Instance.Map);
        Assert.IsNotNull(culture.WhyNotHoliday(), "no holiday before a festival");
        Assert.IsNull(culture.WhyNotFestival(capital), culture.WhyNotFestival(capital));
        Assert.IsTrue(culture.PrepareFestival(capital));
        before = culture.UnityHeld;
        Assert.IsNotNull(culture.CelebrateFestival(capital, new[] { "Vaelia" }));
        Assert.AreEqual(1, culture.FestivalsHeld);
        Assert.Greater(culture.UnityHeld, before);
        Assert.IsNotNull(culture.WhyNotFestival(capital), "one festival a Phase");

        // A holiday: high morale and Unity; it records today and is kept again the next Echo.
        StatManager.Instance.ApplyMoraleShift(60, "Culture life test", true, 5);
        Give("Unity", 100f);
        Assert.IsNull(culture.WhyNotHoliday(), culture.WhyNotHoliday());
        var time = TimeSystemLogic.Instance;
        var holiday = culture.EstablishHoliday();
        Assert.IsNotNull(holiday);
        Assert.AreEqual(time.CurrentSeventh, holiday.seventh);
        Assert.AreEqual(time.CurrentPhase, holiday.phase);
        Assert.AreEqual(1, holiday.kept, "kept the day it was set apart");
        Assert.AreEqual(0, culture.NextHoliday().sevenths, "today");
        Assert.IsNotNull(culture.WhyNotHoliday(), "one holiday an Echo");
        culture.Tick();
        Assert.AreEqual(1, holiday.kept, "the same day is never kept twice (T04: each occasion is decided once)");
        // An Echo passes on the world's own clock and its day comes round again.
        int echo = time.CurrentEcho;
        typeof(TimeSystemLogic).GetProperty("CurrentEcho").SetValue(time, echo % TimeSystemLogic.EchoesPerCycle + 1);
        if (echo == TimeSystemLogic.EchoesPerCycle) typeof(TimeSystemLogic).GetProperty("CurrentCycle").SetValue(time, time.CurrentCycle + 1);
        culture.Tick();
        Assert.AreEqual(2, holiday.kept, "kept again on its day");
        Assert.AreEqual(1f, GameValues.Get("culture", "holidays"));
        Assert.Greater(culture.Happiness, 0f);

        // The culture names a district in its own voice.
        var district = new Settlement { id = 9999, name = "Capital Outskirts IX", kind = SettlementKind.Tributary };
        string called = culture.GiveName(district, "trading", EnclaveFamily.Trading);
        Assert.IsNotNull(called);
        Assert.AreEqual(called, district.name);
        Assert.AreEqual("Capital Outskirts IX", culture.NameOf(9999).former);

        // Cultural parties wait for The Rekindling.
        StringAssert.Contains(culture.Life.festivalTechnology, WorldSystem.Instance.WhyNotCharter(ExpeditionCharter.CulturalParty));

        // All of it survives a save.
        int holidays = culture.Holidays.Count, names = culture.Names.Count;
        float joy = culture.Joy;
        culture.State.culturalFaith = 1.25f;
        float storedFaith = Held("Faith");
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        Assert.AreEqual(holidays, culture.Holidays.Count);
        Assert.AreEqual(holiday.name, culture.Holidays[0].name);
        Assert.AreEqual(names, culture.Names.Count);
        Assert.AreEqual(1, culture.FestivalsHeld);
        Assert.AreEqual(joy, culture.Joy, 1e-5f);
        Assert.AreEqual(1.25f, culture.State.culturalFaith, 1e-5f);
        Assert.AreEqual(storedFaith, Held("Faith"), 1e-5f, "restoring the last award never pays it again");
        yield return new ExitPlayMode();
    }

    private static List<ResourceAmount> In(params (string resource, float amount)[] inputs) =>
        inputs.Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList();

    [UnityTest]
    public IEnumerator ThePeopleInventADishAndADrinkOfTheirOwn_AndKeepThemThroughASave()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Pantry.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var culture = CultureSystem.Instance;
        var cakeIn = In(("Wild Honey", 2f), ("Deep-Rooted Grain", 2f));
        Assert.IsNotNull(culture.WhyNotInvent("Hearth Cake", KitchenMethod.Cook, cakeIn), "no Flavor Log before the founding");
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        Give("Wild Honey", 20f);
        Give("Deep-Rooted Grain", 20f);
        Give("Earth-Beans", 10f);
        yield return null;

        // The dialog opens over the Kitchen for a dish and for a drink (any exception in building it fails the test).
        CultureWindow.Open();
        RecipeInventionDialog.Open(false);
        yield return null;
        Assert.IsTrue(RecipeInventionDialog.IsOpen);
        RecipeInventionDialog.Open(true);
        yield return null;
        Assert.IsTrue(RecipeInventionDialog.IsOpen);
        ((System.Action)typeof(GameInput).GetField("CancelPressed", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))?.Invoke();
        yield return null;
        Assert.IsFalse(RecipeInventionDialog.IsOpen, "Esc closes the dialog");
        Assert.IsTrue(CultureWindow.IsOpen, "and only the dialog");
        CultureWindow.Hide();

        // A dish: chosen from what the stores know, named, and made the way of the closest dish the people can cook.
        CollectionAssert.Contains(culture.IngredientChoices(KitchenMethod.Cook).Select(k => k.resource).ToList(), "Wild Honey");
        Assert.IsNull(culture.WhyNotInvent("hearth cake", KitchenMethod.Cook, cakeIn), culture.WhyNotInvent("hearth cake", KitchenMethod.Cook, cakeIn));
        var cake = culture.Invent("hearth cake", KitchenMethod.Cook, cakeIn);
        Assert.IsNotNull(cake);
        Assert.AreEqual("Hearth Cake", cake.name, "the name is cleaned");
        Assert.IsNotNull(cake.template, "made the way of a known dish");
        Assert.IsTrue(GameCatalog.IsResource("Hearth Cake"), "a resource of the stores");
        Assert.AreEqual(FoodClass.Edible, Pantry.ClassOf("Hearth Cake"));
        Assert.IsTrue(culture.Life.IsDish("Hearth Cake"));
        StringAssert.Contains("name", culture.WhyNotInvent("Hearth Cake", KitchenMethod.Cook, In(("Earth-Beans", 3f), ("Wild Honey", 1f))), "the name is taken");
        Assert.AreEqual(1, culture.Cook(cake.id));
        Assert.AreEqual(cake.output, Held("Hearth Cake"), 0.01f);
        Assert.IsNotNull(GameUnitsLogic.Instance.GetResourceSlotFromName("Hearth Cake"), "it has its place in storage");

        // A drink: brewed the way of the cellar's ale, a Beverage; never from an Eleos Bloom.
        var beer = culture.Invent("Field Beer", KitchenMethod.Ferment, In(("Deep-Rooted Grain", 2f), ("Earth-Beans", 1f)));
        Assert.IsNotNull(beer);
        Assert.AreEqual(FoodClass.Beverage, Pantry.ClassOf("Field Beer"));
        Assert.IsTrue(culture.Life.IsDrink("Field Beer"));
        Assert.IsNotNull(culture.WhyNotInvent("Fern Wine", KitchenMethod.Ferment, In(("Glimmerfern", 1f), ("Deep-Rooted Grain", 2f))));
        Assert.AreEqual(2, culture.Invented.Count);

        // At the table it is theirs: its foodway is marked as the people's own.
        culture.Tick();
        Assert.IsTrue(culture.FoodwayOf("Hearth Cake").invented);

        // A save keeps the made resource so the slot can be made again before the state loads.
        var doc = new SaveDocument();
        GameSnapshot.Capture(doc);
        Assert.IsTrue(doc.runtimeUnits.Any(r => r.name == "Hearth Cake"), "the cooked dish's unit is saved");
        Assert.IsFalse(doc.runtimeUnits.Any(r => r.name == "Field Beer"), "a drink never made has no slot to save");
        Assert.IsTrue(doc.systems.Any(s => s.type == nameof(GameResourceSlot) && s.key == "Hearth Cake"));
        RuntimeUnits.Clear();
        Assert.IsFalse(GameCatalog.IsResource("Hearth Cake"), "a fresh session knows nothing of it");
        GameSnapshot.PrepareSlots(doc); // throws "Missing saved unit" when the made unit is not rebuilt first
        Assert.IsTrue(GameCatalog.IsResource("Hearth Cake"), "made again from the save");

        // The culture's own state brings the recipes, their pantry kinds and units back.
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        Assert.IsNull(culture.Life.Recipe(cake.id));
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        yield return null;
        Assert.AreEqual(2, culture.Invented.Count);
        Assert.IsNotNull(culture.Life.Recipe(cake.id));
        Assert.AreEqual(cake.foodValue, culture.InventedFood("Hearth Cake").foodValue, 1e-5f);
        Assert.IsTrue(GameCatalog.IsResource("Field Beer"));
        Assert.IsTrue(Pantry.IsStoredFood("Field Beer"));
        yield return new ExitPlayMode();
    }
}
