using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The calendar in the real scene (T04, CultureSystem.Observance.cs): a holiday from before observances keeps its date
/// and is kept once on it; a quiet Day of Remembrance is set apart without the holiday's morale gate and asks nothing;
/// the Offering of the First Golden Fruit prepares its table ahead (the soup leaves the stores once, survives two
/// reloads, returns whole when cancelled), is kept once on its day, is kept smaller when the stores are bare, passes
/// unkept across a time jump (a prepared table returned), and waits for a postponed day; the vigil keeps to the full Moon.
/// </summary>
public class ObservancePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    private const string Soup = "Dried Auric Peaches";

    private static long D(int cycle, int echo, int phase, int seventh) => ObservanceCalendar.Date(cycle, echo, phase, seventh);
    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);
    private static float Held(string resource) => GameUnitsLogic.Instance.GetResourceAmountExact(resource);

    // The world's clock set to a date (the game's own Cycle / Echo / Phase / Seventh, never the wall clock).
    private static void SetDate(long date)
    {
        var p = ObservanceCalendar.Parts(date);
        var time = TimeSystemLogic.Instance;
        typeof(TimeSystemLogic).GetProperty("CurrentCycle").SetValue(time, p.cycle);
        typeof(TimeSystemLogic).GetProperty("CurrentEcho").SetValue(time, p.echo);
        typeof(TimeSystemLogic).GetProperty("CurrentPhase").SetValue(time, p.phase);
        typeof(TimeSystemLogic).GetProperty("CurrentSeventh").SetValue(time, p.seventh);
    }

    // A Seventh of culture on that date (a jump when the date is further than tomorrow).
    private static void Day(CultureSystem culture, long date)
    {
        SetDate(date);
        culture.Tick();
        Assert.AreEqual(date, culture.Today);
    }

    // A save and a load of the culture (the codec and the post-restore hook a real load runs).
    private static void Reload(CultureSystem culture)
    {
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", Any).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
    }

    // An Age passes as AgeProgression reports it (its crisis becomes a loss the culture remembers, T03).
    private static void PassAge(string ageId, string ageTitle, string crisis, string nextId, string nextTitle, int deaths)
    {
        var ages = AgeProgression.Instance;
        var history = (List<AgeRecord>)typeof(AgeProgression).GetField("_history", Any).GetValue(ages);
        var record = new AgeRecord { ageId = ageId, ageTitle = ageTitle, crisisTitle = crisis, nextAgeId = nextId, nextAgeTitle = nextTitle, populationBefore = 100, deaths = deaths };
        history.Add(record);
        ((Action<AgeRecord>)typeof(AgeProgression).GetField("AgePassed", Any).GetValue(ages))?.Invoke(record);
    }

    [UnityTest]
    public IEnumerator TheCalendarKeepsEachOccasionOnce_ThroughReloadsCancellationsBareStoresAndTimeJumps()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null
                                    || Pantry.Instance == null || PopGrowthLogic.Instance == null || AgeProgression.Instance == null || TimeSystemLogic.Instance == null); i++) yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Any).SetValue(menu, false);
        Time.timeScale = 1;
        for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;) yield return null;
        TimeSystemLogic.Instance.isTimePaused = true;
        Scenario();

        // The calendar panel builds over it (any exception fails the test).
        CultureWindow.Open();
        yield return null;
        var window = UnityEngine.Object.FindAnyObjectByType<CultureWindow>();
        var mode = typeof(CultureWindow).GetNestedType("LifeMode", BindingFlags.NonPublic);
        typeof(CultureWindow).GetMethod("SetLife", Any).Invoke(window, new[] { Enum.Parse(mode, "Holidays") });
        yield return null;
        CultureWindow.Hide();
        yield return new ExitPlayMode();
    }

    // The whole scenario in a plain method: its lambdas capture locals, which EnterPlayMode's domain reload would lose.
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        var state = (CultureState)typeof(CultureSystem).GetField("_state", Any).GetValue(culture);
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        PopGrowthLogic.Instance.isFoodScarce = false;
        Day(culture, D(1, 1, 1, 2));

        // A holiday from before observances: linked on its own day by the load, kept there once.
        var old = new Holiday { name = "The Day of Sparks", occasion = "the people's joy", seventh = 5, phase = 1, echo = 1, cycle = 1, kept = 2 };
        state.holidays.Add(old);
        culture.AfterRestore();
        culture.AfterRestore();
        var sparks = culture.Observances().Single(v => v.legacy);
        Assert.AreEqual("The Day of Sparks", sparks.name);
        Assert.AreEqual(D(1, 1, 1, 5), sparks.nextDate, "the holiday keeps its date");
        Assert.AreEqual(2, sparks.kept);
        Day(culture, D(1, 1, 1, 5));
        Assert.AreEqual(3, old.kept, "kept on its day");
        Assert.AreEqual(CultureCalendar.EchoKey(1, 1), old.lastKept);
        culture.Tick();
        Assert.AreEqual(3, old.kept, "a second Seventh on the same day keeps nothing again");
        Assert.AreEqual(D(1, 2, 1, 5), culture.ObservanceOf(sparks.id).nextDate);

        // A quiet Day of Remembrance: no festival, no high spirits, no Unity, nothing from the stores.
        PassAge("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", "age-of-renewal", "Age of Renewal", 30);
        var hunger = culture.MemoryCauses().Single(e => e.cause == MemoryCause.Crisis);
        Assert.IsNotNull(culture.WhyNotHoliday(), "a celebratory holiday is still out of reach");
        var grief = culture.DefaultObservancePlan("day-of-remembrance");
        Assert.AreEqual(hunger.id, grief.cause.id, "the loss offered as its cause");
        Assert.AreEqual(ObservanceScale.Quiet, grief.scale);
        grief.seventh = 10;
        grief.phase = 1;
        var preview = culture.PreviewEstablishObservance(grief);
        Assert.IsTrue(preview.succeeded, preview.reason);
        Assert.AreEqual(0, preview.paid.Count, "set apart for nothing");
        StringAssert.Contains("nothing from the stores", preview.reason, "what it asks is said before it is committed");
        float unity = culture.UnityHeld;
        Assert.IsTrue(culture.EstablishObservance(grief).succeeded);
        Assert.AreEqual(unity, culture.UnityHeld, 1e-4f);
        var remembrance = culture.Observances().Single(v => v.definition == "day-of-remembrance");
        StringAssert.Contains("The Inescapable Hunger", remembrance.name);
        Give(Soup, 30f);
        float soup = Held(Soup);
        Day(culture, D(1, 1, 1, 10));
        remembrance = culture.ObservanceOf(remembrance.id);
        Assert.AreEqual(1, remembrance.kept);
        Assert.AreEqual(ObservanceOutcome.Kept, remembrance.history.Single().outcome);
        Assert.AreEqual(0, remembrance.history.Single().paid.Count, "kept quietly: nothing spent");
        Assert.AreEqual(soup, Held(Soup), 1e-4f);
        Assert.IsTrue(CultureOccurrences.Seen(culture.Extensions.occurrences, remembrance.history.Single().key), "one observance occurrence, by its occasion's key");

        // The Offering of the First Golden Fruit (Age 0): a table of soup on the 14th of the first Phase of every Echo.
        Give(culture.UnityResource, 20f);
        var fruit = culture.DefaultObservancePlan("first-golden-fruit");
        fruit.recurrence = ObservanceRecurrence.EveryEcho;
        fruit.seventh = 14;
        fruit.phase = 1;
        fruit.scale = ObservanceScale.Full;
        fruit.food = CultureEntityRef.Of(CultureEntityKind.Resource, Soup, Soup);
        preview = culture.PreviewEstablishObservance(fruit);
        Assert.IsTrue(preview.succeeded, preview.reason);
        StringAssert.Contains("kept quietly", preview.reason, "the fallback when the stores cannot set the table is disclosed");
        unity = culture.UnityHeld;
        Assert.IsTrue(culture.EstablishObservance(fruit).succeeded);
        Assert.AreEqual(unity - 10f, culture.UnityHeld, 1e-3f, "its cost paid once");
        string id = culture.Observances().Single(v => v.definition == "first-golden-fruit").id;

        // Prepared ahead: the soup leaves the stores once; two reloads neither refund nor charge it again; a cancel returns it whole.
        var prepare = culture.PreviewPrepareObservance(id);
        Assert.IsTrue(prepare.succeeded, prepare.reason);
        float portions = prepare.paid.Single().amount;
        Assert.Greater(portions, 0f);
        soup = Held(Soup);
        Assert.IsTrue(culture.PrepareObservance(id).succeeded);
        Assert.AreEqual(soup - portions, Held(Soup), 1e-3f);
        StringAssert.Contains("prepared", culture.WhyNotConfigureObservance(id, ObservanceScale.Modest, fruit.food, null), "a prepared plan is not changed under it");
        Reload(culture);
        Reload(culture);
        Assert.IsTrue(culture.ObservanceOf(id).prepared, "the preparation is saved");
        Assert.AreEqual(soup - portions, Held(Soup), 1e-3f, "and nothing moved on load");
        Assert.IsTrue(culture.CancelObservancePreparation(id).succeeded);
        Assert.AreEqual(soup, Held(Soup), 1e-3f, "returned whole");
        Assert.IsFalse(culture.ObservanceOf(id).prepared);
        Assert.IsTrue(culture.PrepareObservance(id).succeeded);
        Reload(culture);
        soup = Held(Soup);

        // Its day: the prepared table is served (not spent again), once.
        Day(culture, D(1, 1, 1, 14));
        var kept = culture.ObservanceOf(id);
        Assert.AreEqual(1, kept.kept);
        Assert.IsFalse(kept.prepared);
        Assert.AreEqual(soup, Held(Soup), 1e-3f, "the table was already taken from the stores");
        var first = kept.history.Last();
        Assert.AreEqual(ObservanceOutcome.Kept, first.outcome);
        Assert.AreEqual(portions, first.paid.Single(p => p.resource == Soup).amount, 1e-3f);
        Assert.IsTrue(CultureOccurrences.Seen(culture.Extensions.occurrences, first.key + ":table"), "one table occurrence, by the occasion's key");
        culture.Tick();
        Reload(culture);
        culture.Tick();
        Assert.AreEqual(1, culture.ObservanceOf(id).kept, "the same day after a reload keeps nothing again");
        Assert.AreEqual(soup, Held(Soup), 1e-3f);

        // Bare stores and nothing prepared: kept quietly, nothing spent.
        Give(Soup, -Held(Soup));
        Day(culture, D(1, 2, 1, 14));
        kept = culture.ObservanceOf(id);
        Assert.AreEqual(ObservanceOutcome.KeptSmaller, kept.history.Last().outcome);
        Assert.AreEqual(1, kept.keptSmaller);
        Assert.IsFalse(kept.history.Last().paid.Any(p => p.resource == Soup));

        // A time jump over two of its days: both pass unkept, nothing spent.
        Give(Soup, 30f);
        soup = Held(Soup);
        Day(culture, D(1, 4, 1, 20));
        kept = culture.ObservanceOf(id);
        Assert.AreEqual(2, kept.missed);
        Assert.AreEqual(2, kept.kept, "one kept full, one kept smaller");
        Assert.AreEqual(soup, Held(Soup), 1e-3f);

        // A table prepared, then a jump past its day: the table comes back whole.
        Day(culture, D(2, 1, 1, 8));
        Assert.IsTrue(culture.PrepareObservance(id).succeeded);
        Day(culture, D(2, 1, 1, 18));
        kept = culture.ObservanceOf(id);
        Assert.AreEqual(3, kept.missed);
        Assert.IsFalse(kept.prepared);
        Assert.AreEqual(soup, Held(Soup), 1e-3f, "a missed day spends nothing");

        // Postponed three Sevenths: its own day passes quietly, it is kept on the new one.
        Day(culture, D(2, 2, 1, 2));
        Assert.IsTrue(culture.PostponeObservance(id, 3).succeeded);
        Assert.AreEqual(D(2, 2, 1, 17), culture.ObservanceOf(id).nextDate);
        Day(culture, D(2, 2, 1, 14));
        Assert.AreEqual(2, culture.ObservanceOf(id).kept, "not on its own day");
        Day(culture, D(2, 2, 1, 17));
        kept = culture.ObservanceOf(id);
        Assert.AreEqual(3, kept.kept);
        Assert.AreEqual(3, kept.missed);
        Assert.AreEqual(D(2, 3, 1, 14), kept.nextDate, "the next Echo keeps its own day");

        // Scaled down explicitly: a modest table asks half.
        Assert.IsTrue(culture.ConfigureObservance(id, ObservanceScale.Modest, fruit.food, null).succeeded);
        StringAssert.Contains("modest", culture.ObservanceOf(id).plan);

        // The vigil keeps to the full Moon and a grove.
        var vigil = culture.DefaultObservancePlan("moonlit-vigil");
        Assert.AreEqual(ObservanceRecurrence.RitualSeventh, vigil.recurrence);
        var wrong = culture.DefaultObservancePlan("moonlit-vigil");
        wrong.recurrence = ObservanceRecurrence.EveryEcho;
        wrong.seventh = 5;
        wrong.phase = 1;
        Assert.IsFalse(culture.PreviewEstablishObservance(wrong).succeeded, "not on an ordinary day");
        if (culture.ObservanceVenues("moonlit-vigil").Count == 0) StringAssert.Contains("kept in a place", culture.PreviewEstablishObservance(vigil).reason);
        else
        {
            Assert.IsTrue(culture.EstablishObservance(vigil).succeeded);
            var v = culture.Observances().Single(x => x.definition == "moonlit-vigil");
            Assert.AreEqual(ObservanceCalendar.PerPhase, ObservanceCalendar.Parts(v.nextDate).seventh, "under the full Moon");
        }

        // Retired: off the calendar, its history kept.
        Assert.IsTrue(culture.RetireObservance(id).succeeded);
        Assert.IsTrue(culture.ObservanceOf(id).ended);
        Assert.AreEqual(3, culture.ObservanceOf(id).kept);
        Assert.IsFalse(culture.RetireObservance(sparks.id).succeeded, "a holiday stays on the calendar");
    }
}
