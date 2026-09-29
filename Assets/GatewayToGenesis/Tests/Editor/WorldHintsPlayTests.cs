using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// The hints in the real scene (<see cref="Tutorials"/> and the hints in WorldHints.cs): after the founding, a world
/// newly open says how to get there, then (on the map) how to look about, then how to send the selected party; each is
/// spoken by the Auric Aria at a pause, under the Age banner, one at a time, remembered once heard out or done, and a
/// test never writes the player's PlayerPrefs. Static helpers read the scene afresh after every wait.
/// </summary>
public class WorldHintsPlayTests
{
    private static bool Ready() => WorldSystem.Instance?.Map != null && PopGrowthLogic.Instance != null && GameUnitsLogic.Instance != null && Object.FindAnyObjectByType<Tutorials>() != null;
    private static string Food => GameCatalog.ResourceNameFor(ResourceRole.Food);

    [UnityTest]
    public IEnumerator HintsComeOneAtATimeOnceEachAndLearntByDoing()
    {
        EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        Setup();
        for (int i = 0; i < 300 && PopGrowthLogic.Instance.FoundersWaiting == 0; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f); // the lesson sees them waiting
        BringTheFoundersHome();
        // The founding's farewell has the screen first; no hint shows under a lesson.
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual("founding", Tutorials.ShowingId);
        Assert.IsFalse(Tutorials.Seen("world-opens"), "the map is still locked");
        yield return new WaitForSecondsRealtime(8.5f);

        OpenTheMapTechnology();
        yield return new WaitForSecondsRealtime(1.2f);
        CheckAHintAtTheTop("world-opens");

        WorldView.ShowWorld();
        for (int i = 0; i < 600 && WorldView.Current != WorldView.Mode.World; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.IsTrue(Tutorials.Seen("world-opens"), "learnt by doing: the world was opened");
        CheckAHintAtTheTop("world-look");

        // Skip ahead: the selected party waits for orders.
        Tutorials.MarkSeen("world-look");
        yield return new WaitForSecondsRealtime(1.2f);
        CheckAHintAtTheTop("send-party");
        SendTheParty();
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsTrue(Tutorials.Seen("send-party"), "learnt by doing: the party was sent");
        Assert.AreNotEqual("send-party", Tutorials.ShowingId);
        CheckNothingWasWritten();
        yield return new ExitPlayMode();
    }

    private static string _prefsBefore;

    private static void Setup()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1f;
        _prefsBefore = PlayerPrefs.GetString("g2g.tutorials.seen", "<none>");
        Tutorials.Persist = false;
        Tutorials.ResetSeen();
        Tutorials.HintGap = 0.3f;
        Tutorials.IdleSeconds = 0.3f;
    }

    private static void BringTheFoundersHome()
    {
        var units = GameUnitsLogic.Instance;
        for (int guard = 0; guard < 40 && PopGrowthLogic.Instance.FoundersWaiting > 0; guard++) units.ChangeResourceFromName(Food, 12f, false);
        Assert.AreEqual(0, PopGrowthLogic.Instance.FoundersWaiting);
    }

    private static void OpenTheMapTechnology()
    {
        var world = WorldSystem.Instance;
        var slot = GameUnitsLogic.Instance.GetTechnologySlot(world.Settings.mapTechnology);
        Assert.IsNotNull(slot, world.Settings.mapTechnology + " is in the tree");
        slot.UnlockTechnology();
        Assert.IsTrue(world.MapUnlocked);
    }

    private static void CheckAHintAtTheTop(string id)
    {
        var host = Object.FindAnyObjectByType<Tutorials>();
        Assert.AreEqual(id, Tutorials.ShowingId, $"story {EventSystemLogic.Instance?.isEventActive}, map {WorldView.Current}, save menu {SaveMenu.BlocksGameplay}, host {(host != null && host.enabled)}");
        var plate = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name == "Hint" && r.parent != null && r.parent.name == "Tutorials");
        Assert.IsNotNull(plate, "the hint's words are up");
        Assert.AreEqual(1f, plate.anchorMin.y, "at the top of the screen, out of the way");
        Assert.LessOrEqual(plate.sizeDelta.x, 521f, "one short line");
        Assert.IsFalse(plate.GetComponentsInChildren<UnityEngine.UI.Graphic>().Any(g => g.raycastTarget), "it never catches clicks");
    }

    private static void SendTheParty()
    {
        var world = WorldSystem.Instance;
        var party = WorldView.SelectedUnit;
        Assert.IsNotNull(party, "opening the map selects the first party");
        var target = world.Map.NeighboursOf(world.Map.Get(party.coord)).First(t => !t.water && !t.impassable).coord;
        Assert.IsTrue(world.Go(party, target));
    }

    private static void CheckNothingWasWritten()
    {
        Assert.AreEqual(_prefsBefore, PlayerPrefs.GetString("g2g.tutorials.seen", "<none>"), "a test never marks the player's hints as seen");
        Tutorials.Persist = true;
        Tutorials.HintGap = 3f;
        Tutorials.IdleSeconds = 5f;
        Tutorials.ResetSeenMemory();
    }
}
