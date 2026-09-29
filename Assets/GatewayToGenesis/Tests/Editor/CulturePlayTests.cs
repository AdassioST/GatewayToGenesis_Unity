using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The culture in the real scene (<see cref="CultureSystem"/>): Horology unlocks the founding myth; its answer founds
/// the culture and asks the name (the naming dialog, typed into); the capital's "Your Nation" slot names the nation and
/// opens its window; Sevenths spread the culture over held land and make a food familiar until the people are asked
/// whether it is theirs; embracing it makes it national; a reform turns the culture; story text names the nation.
/// </summary>
public class CulturePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    [UnityTest]
    public IEnumerator HorologyFoundsANamedCultureThatSpreadsAndMakesAFoodItsOwn()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || EventVolumeManager.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var culture = CultureSystem.Instance;
        Assert.IsFalse(CultureSystem.IsFounded, "no culture before the founding myth");
        Assert.AreEqual("Your Nation", CultureSystem.NationName);

        // Horology tells the founding myth.
        var horology = GameUnitsLogic.Instance.GetTechnologySlot("Horology");
        Assert.IsNotNull(horology, "Horology is in the tree");
        horology.UnlockTechnology();
        yield return null;
        var story = EventVolumeManager.Instance.FindStoryNode("culture_founding_myth");
        Assert.IsNotNull(story, "Culture.ink is loaded");
        Assert.IsTrue(story.isUnlocked, "researching Horology unlocks the founding myth");

        // Its answer founds the culture (as the chorus choice's consequence does).
        int aureus = StatManager.Instance.GetPillarValue("aureus");
        Assert.IsTrue(culture.ApplyConsequence("myth hearth", 1, "Why Were We Founded?"));
        Assert.IsTrue(CultureSystem.IsFounded);
        Assert.AreEqual(FoundingMyths.Hearth, culture.Myth);
        Assert.AreEqual(EnclaveFamily.Agromagical, culture.Dominant, "the hearth's baseline leads");
        Assert.AreEqual(aureus + 1, StatManager.Instance.GetPillarValue("aureus"), "the myth's effect applies");
        Assert.IsTrue(culture.NamingPending);
        for (Wait(1f, out float until); Time.realtimeSinceStartup < until && !CultureNamingDialog.IsOpen;) yield return null;
        Assert.IsTrue(CultureNamingDialog.IsOpen, "the people are asked their name");

        // Typed into the dialog: the citizens and culture follow the name.
        var dialog = Object.FindAnyObjectByType<CultureNamingDialog>();
        var nameField = (TMP_InputField)typeof(CultureNamingDialog).GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
        var demonymField = (TMP_InputField)typeof(CultureNamingDialog).GetField("_demonym", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
        nameField.text = "iridia";
        Assert.AreEqual("Iridian", demonymField.text, "the demonym is suggested as the name is typed");
        typeof(CultureNamingDialog).GetMethod("Confirm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dialog, null);
        Assert.IsFalse(CultureNamingDialog.IsOpen);
        Assert.AreEqual("Iridia", CultureSystem.NationName);
        Assert.AreEqual("Iridian", CultureSystem.Demonym);
        Assert.AreEqual("Iridian", CultureSystem.Adjective);
        Assert.AreEqual("Welcome to Iridia, Iridians.", CultureSystem.ExpandText("Welcome to {nation}, {people}."));

        // The capital's Your Nation slot names the nation and opens its window.
        yield return null;
        var slot = Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(t => t.name == "NationSlot" && t.gameObject.scene.IsValid());
        Assert.IsNotNull(slot, "the HUD has its NationSlot");
        Assert.AreEqual("Iridia", slot.GetComponent<TooltipTrigger>().customTitle);
        slot.GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(CultureWindow.IsOpen, "clicking Your Nation opens the culture");
        CultureWindow.Hide();

        // Sevenths: the culture spreads over held land and the cellar's peaches grow familiar.
        for (int i = 0; i < 3; i++) culture.Tick();
        var capital = WorldSystem.Instance.Map.Get(WorldSystem.Instance.Map.Capital);
        Assert.Greater(culture.PresenceAt(capital.index), 0.5f, "the capital's land has taken the culture");
        Assert.Greater(WorldLenses.Value(WorldLens.Culture, WorldSystem.Instance.Map, capital, null), 0.5f, "and the Culture lens shows it");
        Assert.Greater(culture.Cohesion, 0f);
        var peaches = culture.FoodwayOf("Dried Auric Peaches");
        Assert.IsNotNull(peaches, "what the cellars hold is at the table");
        Assert.Greater(peaches.familiarity, 0f);

        // Grown used to it: the people are asked, and embrace it.
        peaches.familiarity = 0.95f;
        peaches.firstSevenths = 100;
        culture.Tick();
        Assert.AreEqual("Dried Auric Peaches", culture.PendingFood, "the peaches wait to become the national food");
        Assert.IsTrue(EventVolumeManager.Instance.FindStoryNode("culture_national_food").isUnlocked, "the story that asks is unlocked");
        Assert.AreEqual(1f, GameValues.Get("culture", "pending_food"));
        int monocrop = EventSystemLogic.Instance.GetEventScore("monocrop");
        Assert.IsTrue(culture.ApplyConsequence("embrace", 1, "A Taste of Home"));
        Assert.IsTrue(culture.IsNationalFood("Dried Auric Peaches"));
        Assert.AreEqual(1f, GameValues.Get("national_food", "Dried Auric Peaches"));
        Assert.AreEqual(monocrop + 1, EventSystemLogic.Instance.GetEventScore("monocrop"), "a peach nation drifts deeper into the monocrop");

        // A reform turns the culture.
        float esoteric = culture.Leaning(EnclaveFamily.Esoteric);
        Assert.IsTrue(culture.Reform(EnclaveFamily.Esoteric));
        Assert.Greater(culture.Leaning(EnclaveFamily.Esoteric), esoteric + 0.1f);
        Assert.IsNotNull(culture.WhyNotReform(), "a reform needs time to settle");
        Assert.GreaterOrEqual(culture.Moments.Count, 4, "founded, named, a national food, a reform");
        CultureWindow.Open();
        yield return null;
        Assert.IsTrue(CultureWindow.IsOpen, "the window shows a founded culture");
        CultureWindow.Hide();

        // The whole culture survives a save (GameSnapshot.Schema: _state), written out and read back.
        float presence = culture.PresenceAt(capital.index);
        int moments = culture.Moments.Count;
        var saved = UnityEngine.JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        Assert.IsFalse(CultureSystem.IsFounded);
        SaveStateCodec.Restore(culture, UnityEngine.JsonUtility.FromJson<StateNode>(saved), "_state");
        Assert.IsTrue(CultureSystem.IsFounded);
        Assert.AreEqual("Iridia", CultureSystem.NationName);
        Assert.AreEqual(FoundingMyths.Hearth, culture.Myth);
        Assert.IsTrue(culture.IsNationalFood("Dried Auric Peaches"));
        Assert.AreEqual(presence, culture.PresenceAt(capital.index), 1e-5f);
        Assert.AreEqual(moments, culture.Moments.Count);
        Assert.AreEqual(1, culture.Reforms);
        yield return new ExitPlayMode();
    }
}
