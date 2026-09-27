using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// The world view in the real scene (roadmap WG11): scrolling out of the capital opens the world at the capital's
/// own hex and settles on the micro reading, with the capital's canvases faded and the world camera on; scrolling
/// out moves through the meso and macro readings; at the closest zoom over the capital, scrolling in returns to the
/// capital and gives its canvases back. Drives a virtual mouse, so it needs no hands on the keyboard.
/// </summary>
public class WorldViewPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static Mouse _mouse;

    private static void Scroll(float notches)
    {
        InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), scroll = new Vector2(0f, notches * 120f) }); // a raw wheel notch (the Input System may normalise it)
    }

    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    private static void SetField(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);

    [UnityTest]
    public IEnumerator ScrollingOutOfTheCapitalOpensTheWorldAndScrollingInReturns()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (WorldSystem.Instance == null || WorldSystem.Instance.Map == null || TabHotkeys.Instance == null || Object.FindAnyObjectByType<WorldView>() == null); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        // This test exercises the atlas after the front door; persistence has its own tests.
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) SetField(menu, "visible", false);
        Time.timeScale = 1;
        var view = Object.FindAnyObjectByType<WorldView>();
        Assert.IsNotNull(view, "The Genesis Loop did not create the world view");
        Assert.AreEqual(WorldView.Mode.Capital, WorldView.Current);
        var capitalCanvas = TabHotkeys.Instance.CapitalCanvases().FirstOrDefault();
        Assert.IsNotNull(capitalCanvas, "The capital's canvas was not found");

        // Batch runs have no focused window: let the virtual mouse through regardless.
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorBehaviour = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        _mouse = InputSystem.AddDevice<Mouse>();
        _mouse.MakeCurrent();
        try
        {
            // Before Pathfinder Training only the capital is known.
            var capitalCamera = CameraMovement.Instance;
            if (capitalCamera != null) capitalCamera.ZoomToWidest();
            Scroll(-1f);
            yield return null;
            yield return null;
            Assert.AreEqual(WorldView.Mode.Capital, WorldView.Current, "the world stays unknown before its technology");
            Assert.IsFalse(WorldSystem.Instance.MapUnlocked);
            Assert.AreEqual(0, WorldSystem.Instance.Map.Units.Count, "no unit walks the world yet");

            // Researching it opens the map and sends the first expedition out free, directed by a legend no seat holds.
            var pathfinder = GameUnitsLogic.Instance.GetTechnologySlot(WorldSystem.Instance.Settings.mapTechnology);
            Assert.IsNotNull(pathfinder, "the map's technology is not in the tree");
            pathfinder.UnlockTechnology();
            yield return null;
            Assert.IsTrue(WorldSystem.Instance.MapUnlocked);
            var world = WorldSystem.Instance;
            var scout = world.Map.Units.SingleOrDefault();
            Assert.IsNotNull(scout, $"the first expedition is given (units {world.Map.Units.Count}, legends free {string.Join(",", world.Candidates())}, last notice '{world.LastNotice}', researched '{pathfinder.gameUnit?.name}')");
            Assert.IsTrue(world.IsExpedition(scout), "the first unit is an expedition");
            Assert.IsNotNull(scout.leader, "a legend directs it");
            Assert.IsNull(GovernmentLogic.Instance.GetSeatWithLegend(scout.leader), "its Director holds no seat");
            Assert.AreEqual(WorldSystem.Instance.Map.Capital, scout.coord, "it waits at the capital");

            // At the capital camera's widest view, a notch out (not over a list) carries on into the world.
            if (capitalCamera != null)
            {
                capitalCamera.SetZoom(0f);
                Scroll(-1f);
                yield return null;
                yield return null;
                Assert.AreEqual(WorldView.Mode.Capital, WorldView.Current, "below its widest view the wheel zooms the capital");
                capitalCamera.ZoomToWidest();
            }
            Scroll(-1f);
            yield return null;
            yield return null;
            Assert.AreNotEqual(WorldView.Mode.Capital, WorldView.Current, "scrolling out of the capital opens the world");
            for (float until = Time.realtimeSinceStartup + 5f; WorldView.Current != WorldView.Mode.World && Time.realtimeSinceStartup < until;) yield return null;
            Assert.AreEqual(WorldView.Mode.World, WorldView.Current);
            var renderer = Field<WorldRenderer>(view, "_renderer");
            Assert.IsTrue(renderer.Camera.enabled, "the world camera draws the world");
            Assert.AreEqual(WorldScale.Micro, WorldZoom.ScaleOf(renderer.Camera.orthographicSize), "the world opens on the micro reading");
            var group = capitalCanvas.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group);
            Assert.AreEqual(0f, group.alpha, 1e-3, "the capital's canvas fades out");
            Assert.IsFalse(group.blocksRaycasts);

            // Scrolling out reads the world at the meso, then the macro scale.
            for (int i = 0; i < 40 && WorldZoom.ScaleOf(Field<float>(view, "_size")) == WorldScale.Micro; i++)
            {
                Scroll(-1f);
                yield return null;
            }
            Assert.AreEqual(WorldScale.Meso, WorldZoom.ScaleOf(Field<float>(view, "_size")));
            for (int i = 0; i < 40 && WorldZoom.ScaleOf(Field<float>(view, "_size")) != WorldScale.Macro; i++)
            {
                Scroll(-1f);
                yield return null;
            }
            Assert.AreEqual(WorldScale.Macro, WorldZoom.ScaleOf(Field<float>(view, "_size")));

            // At the closest zoom over the capital, scrolling in returns to it.
            var map = WorldSystem.Instance.Map;
            var capital = map.Get(map.Capital);
            SetField(view, "_cx", capital.x);
            SetField(view, "_cy", capital.y);
            SetField(view, "_size", WorldZoom.Closest);
            yield return null;
            Scroll(1f);
            yield return null;
            yield return null;
            Assert.AreEqual(WorldView.Mode.Closing, WorldView.Current, "scrolling in at the closest zoom returns to the capital");
            for (float until = Time.realtimeSinceStartup + 5f; WorldView.Current != WorldView.Mode.Capital && Time.realtimeSinceStartup < until;) yield return null;
            Assert.AreEqual(WorldView.Mode.Capital, WorldView.Current);
            Assert.IsFalse(renderer.Camera.enabled, "the world camera rests in the capital");
            Assert.AreEqual(1f, group.alpha, 1e-3, "the capital's canvas is back");
            Assert.IsTrue(group.blocksRaycasts);
            if (capitalCamera != null) Assert.IsTrue(capitalCamera.AtWidest, "the capital comes back at its widest view, where the world left it");
        }
        finally
        {
            InputSystem.RemoveDevice(_mouse);
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorBehaviour;
        }
        yield return new ExitPlayMode();
    }
}
