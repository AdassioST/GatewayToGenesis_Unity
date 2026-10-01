using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// The player's pause in the real scene (<see cref="TimeSystemLogic.PlayerPaused"/>, Space): it stops the calendar and
/// every party on the map as a story does, a story ending does not lift it, and the time indicator
/// (<see cref="TimeFlowHud"/>) says so with the gold frame round the screen. Each step reads the scene afresh.
/// </summary>
public class TimeFlowPlayTests
{
    private static bool Ready() => WorldSystem.Instance?.Map != null && TimeSystemLogic.Instance != null && GameUnitsLogic.Instance != null && Label() != null;
    private static TextMeshProUGUI Label() => Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Flow" && t.transform.parent.name == "Time Flow");
    private static CanvasGroup PausedFrame() => Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include).First(g => g.name == "Paused Frame");
    private static float _progress;

    [UnityTest]
    public IEnumerator ThePlayersPauseStopsTheWorldAndTheIndicatorSaysSo()
    {
        EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        EnterTheWorld();
        yield return new WaitForSecondsRealtime(0.5f);
        CheckTimeFlows();
        Pause(true);
        yield return new WaitForSecondsRealtime(0.6f);
        CheckEverythingWaits();
        yield return new WaitForSecondsRealtime(0.4f);
        CheckStillWaiting();
        Pause(false);
        yield return new WaitForSecondsRealtime(0.4f);
        CheckTimeFlowsAgain();
        LeaveTheWorld();
        yield return new ExitPlayMode();
    }

    [Test]
    public void SpaceIsThePauseKey()
    {
        Assert.AreEqual("<Keyboard>/space", KeyBindings.Action("pause").bindings[0].path);
        Assert.IsTrue(KeyBindings.Entries.Any(e => e.id == "pause" && !e.locked), "the player can rebind it in Options, Controls");
    }

    // The menu dismissed and the calendar counting (Horology known).
    private static void EnterTheWorld()
    {
        var menu = SaveMenu.Instance;
        typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1f;
        var time = TimeSystemLogic.Instance;
        time.canTrackTime = true;
        time.PauseTime(false);
    }

    private static void LeaveTheWorld()
    {
        TimeSystemLogic.Instance.SetPlayerPaused(false);
    }

    private static void Pause(bool paused) => TimeSystemLogic.Instance.SetPlayerPaused(paused);

    private static void CheckTimeFlows()
    {
        Assert.IsFalse(TimeSystemLogic.SimulationHeld);
        Assert.Greater(WorldSystem.Instance.SeventhsPassing(1f), 0f, "parties walk while time flows");
        StringAssert.Contains("Time flows", Label().text);
        Assert.Less(PausedFrame().alpha, 0.01f, "no frame while time flows");
    }

    private static void CheckEverythingWaits()
    {
        var time = TimeSystemLogic.Instance;
        Assert.IsTrue(time.PlayerPaused);
        Assert.IsTrue(TimeSystemLogic.SimulationHeld, "production, the pantry and the people hold");
        Assert.IsTrue(time.IsStopped);
        Assert.AreEqual(0f, WorldSystem.Instance.SeventhsPassing(1f), "parties stand still");
        Assert.AreEqual("Paused", Label().text);
        Assert.Greater(PausedFrame().alpha, 0.5f, "the gold frame shows the pause");
        // A story ending (or a card closing) releases its own hold on the calendar, never the player's pause.
        time.PauseTime(true);
        time.PauseTime(false);
        Assert.IsTrue(time.PlayerPaused);
        Assert.IsTrue(time.IsStopped);
        _progress = time.GetSeventhProgress();
    }

    private static void CheckStillWaiting() =>
        Assert.AreEqual(_progress, TimeSystemLogic.Instance.GetSeventhProgress(), "the calendar did not move while paused");

    private static void CheckTimeFlowsAgain()
    {
        var time = TimeSystemLogic.Instance;
        Assert.IsFalse(time.PlayerPaused);
        Assert.Greater(time.GetSeventhProgress(), _progress, "the calendar moves on");
        StringAssert.Contains("Time flows", Label().text);
    }
}
