using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// The founding in the real scene: the 21 founders wait at the gates; every 12 Food gathered lets one in at once; the
/// first lesson (<see cref="FoundingLesson"/>) is spoken by the Auric Aria at a pause, as layered floating words in the
/// middle of the screen (whisper, large title, what to do; no card) with the Food button ringed in gold
/// (<see cref="TutorialGlow"/>, just behind the button); she falls silent when the player acts and returns at the next
/// pause; the last founder home is announced at once, even while the player acts; the first death gets a line of hers,
/// low on the screen as a subtitle; once all are in, the founders eat nothing from the daily food,
/// and with Ash-Cellars known the Food beyond a hand's worth goes into the stores.
/// Each step is a static helper that reads the scene afresh (nothing is held across frames in play mode).
/// </summary>
public class FoundingPlayTests
{
    private static bool Ready() => WorldSystem.Instance?.Map != null && PopGrowthLogic.Instance != null && GameUnitsLogic.Instance != null && Pantry.Instance != null;
    private static string Food => GameCatalog.ResourceNameFor(ResourceRole.Food);
    private static GameResourceSlot FoodSlot => GameUnitsLogic.Instance.GetResourceSlotFromName(Food);
    private static RectTransform Glow() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name == "Tutorial Glow");

    [UnityTest]
    public IEnumerator FoundersComeInForEveryTwelveFoodAndTheLessonRingsTheButton()
    {
        EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        DismissSaveMenu();
        for (int i = 0; i < 300 && PopGrowthLogic.Instance.FoundersWaiting == 0; i++) yield return null;
        yield return new WaitForSecondsRealtime(2f); // she fades in slowly: the glow comes once she is half there
        CheckTheVoiceAndTheGlow();
        Tutorials.Stir();
        yield return null;
        yield return null;
        CheckTheVoiceFellSilent();
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual("founding", Tutorials.ShowingId, "she returns at the next pause while founders still wait");
        GatherForTheFounders();
        BringEveryoneHome();
        yield return new WaitForSecondsRealtime(0.5f);
        Tutorials.Stir();
        yield return null;
        yield return null;
        CheckTheHomecoming();
        GriefForTheFirstDeath();
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.AreEqual("moment.first-death", AuricAria.Waiting.FirstOrDefault().id, "her line waits for the announcement to be read");
        for (float t = 0f; t < 14f && Tutorials.ShowingId != "moment.first-death"; t += 0.1f) yield return new WaitForSecondsRealtime(0.1f);
        yield return new WaitForSecondsRealtime(0.5f);
        CheckTheSubtitle();
        if (UnlockStorage())
        {
            yield return new WaitForSecondsRealtime(1.3f);
            CheckTheSurplusIsStored();
        }
        Tutorials.IdleSeconds = 5f;
        yield return new ExitPlayMode();
    }

    private static void DismissSaveMenu()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1f;
        Tutorials.IdleSeconds = 0.4f;
    }

    private static void GatherForTheFounders()
    {
        var pop = PopGrowthLogic.Instance;
        var units = GameUnitsLogic.Instance;
        Assert.AreEqual(21, pop.FoundersWaiting, "a new world begins with its founders at the gates");
        var slot = FoodSlot;
        Assert.IsNotNull(slot);
        slot.amount = 0f;
        int people = pop.TotalPeople;
        units.ChangeResourceFromName(Food, 11f, false);
        Assert.AreEqual(21, pop.FoundersWaiting, "eleven is not yet a ration");
        units.ChangeResourceFromName(Food, 1f, false);
        Assert.AreEqual(20, pop.FoundersWaiting, "the twelfth lets a founder in at once");
        Assert.AreEqual(people + 1, pop.TotalPeople);
        Assert.AreEqual(0f, slot.amount, 1e-3f, "their rations are spent");
        units.ChangeResourceFromName(Food, 30f, false);
        Assert.AreEqual(18, pop.FoundersWaiting, "thirty lets two in");
        Assert.AreEqual(6f, slot.amount, 1e-3f);
        Assert.AreEqual(6f / 12f, slot.fill.fillAmount, 1e-3f, "the bar fills toward the next founder's twelve");
    }

    private static void CheckTheVoiceAndTheGlow()
    {
        var host = Object.FindAnyObjectByType<Tutorials>();
        Assert.AreEqual("founding", Tutorials.ShowingId, $"host {(host != null ? (host.enabled ? "on" : "off") : "missing")}, story {EventSystemLogic.Instance?.isEventActive}, map {WorldView.IsOpen}, save menu {SaveMenu.BlocksGameplay}, speaks {Tutorials.Lessons[0].Speak(out _)}");
        var lesson = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).FirstOrDefault(r => r.name == "Announcement" && r.parent != null && r.parent.name == "Tutorials");
        Assert.IsNotNull(lesson);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), lesson.anchorMin, "in the middle of the screen");
        Assert.IsNotNull(lesson.GetComponent<AuricVoice>(), "spoken by the Auric Aria");
        Assert.IsNull(lesson.Find("Plate"), "floating words, no card");
        Assert.AreEqual("A broken world awaits your voice", Line(lesson, "Whisper"), "a whisper on top");
        Assert.AreEqual("Feed Your 21 Founding Members", Line(lesson, "Title"), "the founders, large");
        Assert.AreEqual("Gather food from what glows in gold...", Line(lesson, "Body"), "what to do, below");
        Assert.Greater(lesson.Find("Title").GetComponent<TMPro.TMP_Text>().fontSize, lesson.Find("Body").GetComponent<TMPro.TMP_Text>().fontSize + 10f, "the title is the big line");
        Assert.IsFalse(lesson.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g => g.raycastTarget), "the words never catch clicks");
        var glow = Glow();
        Assert.IsNotNull(glow, "the button is ringed");
        var button = Object.FindObjectsByType<ClickLogic>(FindObjectsInactive.Exclude).First(c => c.currentMode == ClickLogic.ClickMode.AddResource && c.activeResource == Food).transform;
        Assert.AreEqual(button.parent, glow.parent);
        Assert.AreEqual(button.GetSiblingIndex() - 1, glow.GetSiblingIndex(), "drawn just behind the button");
        Assert.IsFalse(glow.GetComponentsInChildren<UnityEngine.UI.Graphic>().Any(g => g.raycastTarget), "the glow never catches clicks");
    }

    private static string Line(RectTransform voice, string name) => voice.Find(name).GetComponent<TMPro.TMP_Text>().text;

    private static void CheckTheVoiceFellSilent()
    {
        Assert.IsNull(Tutorials.ShowingId, "the player acted: she falls silent while founders still wait");
        Assert.Greater(PopGrowthLogic.Instance.FoundersWaiting, 0);
        Assert.IsNull(Glow(), "the glow goes with her words");
    }

    private static void BringEveryoneHome()
    {
        for (int guard = 0; guard < 40 && PopGrowthLogic.Instance.FoundersWaiting > 0; guard++) GameUnitsLogic.Instance.ChangeResourceFromName(Food, 12f, false);
        var pop = PopGrowthLogic.Instance;
        Assert.AreEqual(0, pop.FoundersWaiting);
        Assert.IsTrue(pop.FoundingDone);
        Assert.AreEqual(0, GrowthRules.Eating(21, pop.Growth), "the founders eat nothing from the daily food");
    }

    private static void CheckTheHomecoming()
    {
        Assert.AreEqual("founding.home", Tutorials.ShowingId, "announced the moment the last founder is in, not hushed by the player's acts");
        var announcement = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).First(r => r.name == "Announcement" && r.parent != null && r.parent.name == "Tutorials");
        Assert.IsTrue(announcement.gameObject.activeSelf);
        Assert.AreEqual("Your 21 Founders Are Home", Line(announcement, "Title"));
        Assert.IsNull(Glow(), "nothing left to click for");
    }

    private static void GriefForTheFirstDeath()
    {
        Assert.AreEqual(0, PopGrowthLogic.Instance.trueDeaths, "no one has died yet");
        PopGrowthLogic.Instance.ProcessEventDeaths(1, "a test");
    }

    private static void CheckTheSubtitle()
    {
        Assert.AreEqual("moment.first-death", Tutorials.ShowingId);
        var line = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).First(r => r.name == "Line" && r.parent != null && r.parent.name == "Tutorials");
        Assert.IsTrue(line.gameObject.activeSelf);
        Assert.AreEqual(0f, line.anchorMin.y, "low on the screen, like a subtitle");
        Assert.IsNotNull(line.GetComponent<AuricVoice>(), "spoken by the Auric Aria");
        string text = Line(line, "Subtitle");
        StringAssert.StartsWith("<i>", text, "the stage direction is set apart");
        StringAssert.Contains("It's okay, sometimes things die", text);
        Assert.IsFalse(line.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g => g.raycastTarget), "the words never catch clicks");
    }

    private static bool UnlockStorage()
    {
        var storage = GameUnitsLogic.Instance.GetTechnologySlot("Ash-Cellars");
        if (storage == null) return false;
        storage.isUnlocked = true;
        Assert.IsTrue(Pantry.Instance.Banking);
        FoodSlot.amount = 0f;
        GameUnitsLogic.Instance.ChangeResourceFromName(Food, 40f, false);
        return true;
    }

    private static void CheckTheSurplusIsStored()
    {
        Assert.AreEqual(12f, FoodSlot.amount, 0.5f, "a hand's worth is kept");
        Assert.Greater(GameUnitsLogic.Instance.GetResourceAmountExact(Pantry.Instance.Settings.bankedKind), 60f, "the rest went into the stores (40 peaches to start)");
    }
}
