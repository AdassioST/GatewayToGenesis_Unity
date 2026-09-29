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
/// Apprenticeships and institutions in the real scene (CultureSystem.Transmission.cs): the Capital's people teach Tales
/// at the Hearth to a Legend (no Legend needed to teach a village custom) at the hearth's one seat; the completed lesson
/// makes one bearer and one Teaching occurrence, never again after a save and two reloads; a teacher away on an
/// expedition pauses the next lesson, which resumes with its progress; a Flavor Log cannot be founded in the Age of
/// Desolation, a Cooking Guild not before Age IV; at a Flavor Log the Ash-Loaf Table is adapted with the people's own
/// invented bread (its recipe saved by id and found again after a load) and written down as a record that keeps no one
/// practising it.
/// </summary>
public class CultureTransmissionPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    private static List<ResourceAmount> In(params (string resource, float amount)[] inputs) =>
        inputs.Select(i => new ResourceAmount { resource = i.resource, amount = i.amount }).ToList();

    private static void TalesThenWait(CultureSystem culture)
    {
        Assert.IsTrue(culture.HoldActivity("tales"), culture.WhyNotActivity(culture.Life.Activity("tales")));
        for (int i = 0; i < culture.Life.Activity("tales").cooldownSevenths; i++) culture.Tick();
    }

    private static void Ticks(CultureSystem culture, int n)
    {
        for (int i = 0; i < n; i++) culture.Tick();
    }

    // A draft taught by the Capital's people (or a Legend) to a learner at the Capital's hearth (or an institution).
    private static TeachingDraft Draft(CultureSystem culture, string tradition, TeachingMode mode, TeacherKind teacherKind, string teacher, LearnerKind learnerKind, string learner, string institution = "")
    {
        var capital = WorldCivilization.Capital(WorldSystem.Instance.Map);
        var d = culture.DraftTeaching(tradition, mode);
        d.teacherKind = teacherKind;
        d.teacher = teacher;
        d.teacherLabel = culture.TeacherOptions(d).FirstOrDefault(t => t.kind == teacherKind && t.id == teacher).label;
        d.learnerKind = learnerKind;
        d.learner = learner;
        d.institution = institution;
        d.settlement = capital.id;
        d.settlementName = capital.name;
        return d;
    }

    private static void SaveAndReload(CultureSystem culture)
    {
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", Any).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
        culture.AfterRestore();
    }

    // A Legend away on an expedition (a fixture: the party stands at the Capital; nothing is paid).
    private static WorldUnit SendAway(string legend)
    {
        var world = WorldSystem.Instance;
        var capital = WorldCivilization.Capital(world.Map);
        var unit = new WorldUnit { id = 90000, spec = world.ExpeditionRules.unit, name = "The Test Expedition", coord = capital.coord, microCoord = capital.coord, leader = legend };
        world.Map.Units.Add(unit);
        return unit;
    }

    private static int Count(IEnumerable<CulturalOccurrence> list, string key) => list.Count(o => o.key == key);

    [UnityTest]
    public IEnumerator ACustomIsTaughtOnce_PausesWhileItsTeacherIsAway_AndAFlavorLogAdaptsTheAshLoaf()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || LegendProgress.Instance == null); i++) yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Any).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        Assert.IsTrue(CultureSystem.Instance.ApplyConsequence("myth hearth", 1, "Why Were We Founded?"));
        Assert.IsTrue(CultureSystem.Instance.Name("Iridia", null, null, out _));
        yield return null;
        Scenario();

        // The window opens on the Teaching panel.
        CultureWindow.Open();
        yield return null;
        var window = UnityEngine.Object.FindAnyObjectByType<CultureWindow>();
        var mode = typeof(CultureWindow).GetNestedType("LifeMode", BindingFlags.NonPublic);
        typeof(CultureWindow).GetMethod("SetLife", Any).Invoke(window, new[] { Enum.Parse(mode, "Teaching") });
        yield return null;
        CultureWindow.Hide();
        yield return new ExitPlayMode();
    }

    // The whole scenario, in a plain method: an iterator's lambdas would capture its locals in a closure made before
    // EnterPlayMode, which the domain reload loses.
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        ICultureQuery query = culture;
        var legends = LegendProgress.Instance;
        while (legends.RecruitedCount < 2) Assert.IsNotNull(legends.RecruitNext("a test"));
        var names = legends.RecruitedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        string first = names[0], second = names[1];
        var capital = WorldCivilization.Capital(WorldSystem.Instance.Map);
        string people = CultureIds.Settlement(capital.id);

        // Tales at the Hearth becomes a custom (the nation's: its home is the Capital).
        for (int i = 0; i < 3; i++) TalesThenWait(culture);
        var tales = query.Traditions().Single(t => t.definition == "hearth-tales");
        Assert.IsTrue(tales.established);
        Assert.IsTrue(culture.TeachableTraditions().Any(t => t.id == tales.id));

        // The Capital's people teach it to a Legend at the Capital's hearth: no Legend is needed to teach a village custom.
        var lesson = Draft(culture, tales.id, TeachingMode.Preserve, TeacherKind.Community, people, LearnerKind.Legend, first);
        var preview = query.PreviewTeaching(lesson);
        Assert.IsTrue(preview.succeeded, preview.reason);
        StringAssert.Contains("3 Sevenths", preview.reason);
        Assert.AreEqual(0, culture.TeachingOrders().Count, "a preview changes nothing");
        Assert.IsTrue(culture.StartTeaching(lesson).succeeded);
        var busy = Draft(culture, tales.id, TeachingMode.Preserve, TeacherKind.Community, people, LearnerKind.Legend, second);
        StringAssert.Contains("Every seat", culture.WhyNotStartTeaching(busy), "the hearth's one seat is taken");
        Ticks(culture, 3);
        var order = culture.TeachingOrders().Single();
        Assert.AreEqual(TeachingStatus.Completed, order.status, TransmissionRules.StatusWord(order));
        CollectionAssert.Contains(culture.CarriersOf(tales.id).livingLegends, first);
        Assert.AreEqual(1, Count(query.RecentOccurrences(60), "teaching:" + order.id), "one Teaching occurrence");
        int moments = culture.Moments.Count(m => m.kind == "teaching");
        Assert.AreEqual(1, moments);

        // A save and two reloads, then a Seventh: nothing is taught or remembered twice.
        SaveAndReload(culture);
        Ticks(culture, 1);
        Assert.AreEqual(1, culture.TaughtBearers().Count);
        Assert.AreEqual(TeachingStatus.Completed, culture.TeachingOrder(order.id).status);
        Assert.AreEqual(moments, culture.Moments.Count(m => m.kind == "teaching"));

        // The Legend now teaches the other; the teacher leaves on an expedition: the lesson pauses and keeps its progress.
        var onward = Draft(culture, tales.id, TeachingMode.Preserve, TeacherKind.Legend, first, LearnerKind.Legend, second);
        Assert.IsNull(culture.WhyNotStartTeaching(onward), culture.WhyNotStartTeaching(onward));
        Assert.IsTrue(culture.StartTeaching(onward).succeeded);
        Ticks(culture, 1);
        var next = culture.TeachingOrders().Single(o => o.Open);
        Assert.AreEqual(1, next.progress);
        var away = SendAway(first);
        Ticks(culture, 2);
        next = culture.TeachingOrder(next.id);
        Assert.AreEqual(TeachingStatus.Paused, next.status);
        StringAssert.Contains("away", next.pausedReason);
        Assert.AreEqual(1, next.progress, "paused, it keeps its progress");
        WorldSystem.Instance.Map.Units.Remove(away);
        Ticks(culture, 1);
        next = culture.TeachingOrder(next.id);
        Assert.AreEqual(TeachingStatus.Active, next.status, next.pausedReason);
        Assert.AreEqual(2, next.progress, "back home, it goes on");
        Assert.IsTrue(culture.CancelTeaching(next.id).succeeded);

        // A Feast Hall at the Capital (a fixture). No Flavor Log in the Age of Desolation; no Cooking Guild before Age IV.
        var state = (CultureState)typeof(CultureSystem).GetField("_state", Any).GetValue(culture);
        state.landmarks.Add(new Landmark { settlement = capital.id, spec = "feast-hall", name = "The Long Table", seventh = state.sevenths });
        string hall = CultureIds.Landmark(capital.id, "feast-hall");
        Give(culture.UnityResource, 200f);
        StringAssert.Contains("Ages I-III", culture.WhyNotFoundInstitution("flavor-log", hall));
        GameAge.Set("age-of-renewal", 1);
        try
        {
            StringAssert.Contains("Ages IV-VI", culture.WhyNotFoundInstitution("cooking-guild", hall));
            Assert.IsNull(culture.WhyNotFoundInstitution("flavor-log", hall), culture.WhyNotFoundInstitution("flavor-log", hall));
            var founded = culture.FoundInstitution("flavor-log", hall);
            Assert.IsTrue(founded.succeeded, founded.reason);
            Assert.AreEqual(15f, founded.paid.Single().amount);
            var log = culture.TeachingInstitutions().Single();

            // The Ash-Loaf Table becomes a custom (three Sevenths of baking), and the people invent a bread of their own.
            GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger").UnlockTechnology();
            for (int i = 0; i < 3; i++)
            {
                Give("Ash-Bread", 8f);
                Give("Earth-Beans", 4f);
                Assert.AreEqual(1, culture.Cook("ash-loaf"));
                culture.Tick();
            }
            var table = query.Traditions().Single(t => t.definition == "ash-loaf-table");
            Assert.IsTrue(table.established, table.progress);
            Give("Ash-Bread", 10f);
            Give("Earth-Beans", 10f);
            var ember = culture.Invent("Ember Bread", KitchenMethod.Cook, In(("Ash-Bread", 3f), ("Earth-Beans", 1f)));
            Assert.IsNotNull(ember, culture.WhyNotInvent("Ember Bread", KitchenMethod.Cook, In(("Ash-Bread", 3f), ("Earth-Beans", 1f))));
            var emberRef = CultureEntityRef.Of(CultureEntityKind.Recipe, ember.id, ember.name);

            // Adapting the loaf is too complex for the hearth; at the Flavor Log the Capital makes its own form with Ember Bread.
            var adapt = Draft(culture, table.id, TeachingMode.Adapt, TeacherKind.Community, people, LearnerKind.Community, people);
            adapt.recipe = emberRef;
            StringAssert.Contains("Too complex", culture.WhyNotStartTeaching(adapt));
            adapt.institution = log.id;
            Assert.IsTrue(culture.StartTeaching(adapt).succeeded, culture.WhyNotStartTeaching(adapt));
            var write = Draft(culture, table.id, TeachingMode.Record, TeacherKind.Community, people, LearnerKind.None, null, log.id);
            Assert.IsTrue(culture.StartTeaching(write).succeeded, culture.WhyNotStartTeaching(write));
            var third = Draft(culture, tales.id, TeachingMode.Preserve, TeacherKind.Legend, first, LearnerKind.Legend, second, log.id);
            Assert.IsNotNull(culture.WhyNotStartTeaching(third), "the Flavor Log teaches food, and its two seats are taken");

            // Saved mid-lesson: the invented bread is found again after the load (its resource registered anew).
            SaveAndReload(culture);
            Assert.IsNull(culture.WhyNotRecipe(emberRef), "the custom recipe resolves after a load");
            Ticks(culture, 12);
            var variant = culture.PracticeVariants().Single();
            Assert.AreEqual(table.id, variant.parent, "a variant keeps its parent");
            Assert.AreEqual(ember.id, variant.recipe.id);
            StringAssert.Contains("Ember Bread", variant.name);
            var carriers = culture.CarriersOf(table.id);
            Assert.AreEqual(1, carriers.records.Count, "written down once");
            Assert.AreEqual(1, carriers.variants.Count);
            Assert.IsTrue(carriers.Living, "the Capital still keeps it: living practice");
            Assert.AreEqual(0, culture.TaughtBearers().Count(b => b.tradition == table.id && string.IsNullOrEmpty(b.variant)), "writing it down made no bearer: a record is not a practitioner");
            Assert.AreEqual(1, culture.TaughtBearers().Count(b => b.variant == variant.id), "the Capital keeps its own form");
            Assert.AreEqual(1f, culture.TeachingValue("institutions"));
            Assert.AreEqual(1f, culture.TeachingValue("institution:flavor-log"));
            string beforeAtlas = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
            var atlas = CultureAtlasReadModel.Capture(culture);
            Assert.LessOrEqual(CultureAtlasTests.Distance(atlas, "Recipe:" + ember.id, "Tradition:" + table.id), 3);
            Assert.LessOrEqual(CultureAtlasTests.Distance(atlas, "Recipe:" + ember.id, "Settlement:" + capital.id), 3);
            Assert.AreEqual(beforeAtlas, JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state")));
        }
        finally
        {
            GameAge.Set("age-of-desolation", 0);
        }

        // The panel builds over all of it (any exception fails the test).
        TeachingPanel.Reset();
        var rows = new List<string>();
        TeachingPanel.Fill(culture, h => { }, (label, why, tip, act) => rows.Add(label));
        Assert.IsTrue(rows.Any(r => r.StartsWith("Institutions")));
        Assert.IsTrue(rows.Any(r => r.StartsWith("Teach Tales at the Hearth")));
    }
}
