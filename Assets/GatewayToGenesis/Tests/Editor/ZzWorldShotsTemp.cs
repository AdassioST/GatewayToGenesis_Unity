using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// TEMPORARY (world UI restyle session): batchmode screenshots of the world view. Runs only when G2G_SHOTS names an
// output folder. Delete when the restyle is done.
public class ZzWorldShotsTemp
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator WorldShots()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G2G_SHOTS"))) { Assert.Ignore("no G2G_SHOTS"); yield break; }
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        Setup();
        for (float until = Time.realtimeSinceStartup + 5f; WorldView.Current != WorldView.Mode.World && Time.realtimeSinceStartup < until;) yield return null;
        Prepare();
        for (int shot = 0; shot < Shots; shot++)
        {
            Stage(shot);
            for (float until = Time.realtimeSinceStartup + 0.6f; Time.realtimeSinceStartup < until;) yield return null;
            Capture(shot);
        }
        yield return new ExitPlayMode();
    }

    private const int Shots = 7;

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        WorldSystem.Instance != null && WorldSystem.Instance.Map != null && GameUnitsLogic.Instance != null && AgeProgression.Instance != null && AgeProgression.Instance.Current != null
        && UnityEngine.Object.FindAnyObjectByType<WorldView>() != null;

    private static WorldView View => UnityEngine.Object.FindAnyObjectByType<WorldView>();
    private static WorldRenderer Renderer => (WorldRenderer)typeof(WorldView).GetField("_renderer", Private).GetValue(View);

    private static void Setup()
    {
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
        var world = WorldSystem.Instance;
        var slot = GameUnitsLogic.Instance.GetTechnologySlot(world.Settings.mapTechnology);
        if (!GameUnitsLogic.Instance.IsTechnologyUnlocked(world.Settings.mapTechnology)) slot.UnlockTechnology();
        WorldView.ShowWorld();
    }

    // Reveal a ring of land around the capital so the shots show every state of knowledge.
    private static void Prepare()
    {
        var map = WorldSystem.Instance.Map;
        foreach (var t in map.Tiles)
        {
            int d = HexCoord.Distance(t.coord, map.Capital);
            if (d <= 4) { t.revealed = t.known = t.explored = true; t.microKnownMask = 127; }
            else if (d <= 7) { t.revealed = t.known = true; t.microKnownMask = 127; }
            else if (d <= 11) t.revealed = true;
        }
        Renderer.RefreshKnowledge();
        var rt = new RenderTexture(1920, 1080, 24) { name = "Shots" };
        Renderer.Camera.targetTexture = rt;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Renderer.Camera;
            canvas.planeDistance = 2f;
            canvas.sortingOrder += 200;
        }
    }

    private static void Set(string field, object value) => typeof(WorldView).GetField(field, Private).SetValue(View, value);
    private static object Call(string method, params object[] args) => typeof(WorldView).GetMethod(method, Private).Invoke(View, args);
    private static object Popup(string name) => Enum.Parse(typeof(WorldView).GetNestedType("Popup", BindingFlags.NonPublic), name);

    private static readonly string[] Names = { "1-micro-capital", "2-meso", "3-macro", "4-meso-settle", "5-lens-popup", "6-meso-hover", "7-micro-far" };

    private static void Stage(int shot)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var capital = map.Get(map.Capital);
        float farthest = (float)typeof(WorldView).GetField("_farthest", Private).GetValue(View);
        Set("_cx", capital.x);
        Set("_cy", capital.y);
        switch (shot)
        {
            case 0: Set("_size", WorldZoom.MicroSize); break;
            case 1: Set("_size", WorldZoom.SizeFor(WorldScale.Meso, farthest)); break;
            case 2: Set("_size", WorldZoom.SizeFor(WorldScale.Macro, farthest)); break;
            case 3:
                Set("_size", WorldZoom.SizeFor(WorldScale.Meso, farthest));
                Call("SetLens", WorldLens.Settle);
                break;
            case 4:
                Call("SetLens", WorldLens.Normal);
                Call("ShowPopup", Popup("Map"));
                break;
            case 5:
                Call("ShowPopup", Popup("None"));
                var near = map.Tiles.Where(t => !t.water && HexCoord.Distance(t.coord, map.Capital) == 3).OrderBy(t => t.index).First();
                Call("ShowHover", world, near);
                break;
            case 6:
                Set("_size", WorldZoom.MicroSize * 1.6f);
                var edge = map.Tiles.Where(t => !t.water && HexCoord.Distance(t.coord, map.Capital) == 7).OrderBy(t => t.index).First();
                Set("_cx", edge.x);
                Set("_cy", edge.y);
                Set("_selectedUnit", -1);
                Set("_selected", (HexCoord?)edge.coord);
                Call("Refresh", true);
                break;
        }
    }

    private static void Capture(int shot)
    {
        var cam = Renderer.Camera;
        var rt = cam.targetTexture;
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        string dir = Environment.GetEnvironmentVariable("G2G_SHOTS");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, Names[shot] + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.Destroy(tex);
    }
}
