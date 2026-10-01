using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CombatUiPolishTests
{
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator WorldPartyMetersAndDetachedSaveCapture()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new UnityEngine.TestTools.EnterPlayMode();
        for (int i = 0; i < 600 && (WorldSystem.Instance?.Map == null || Object.FindAnyObjectByType<WorldView>() == null); i++) yield return null;
        var world = WorldSystem.Instance;
        Assert.IsNotNull(world?.Map);
        typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(SaveMenu.Instance, false);
        Time.timeScale = 1f;
        foreach (string technology in new[] { world.Settings.mapTechnology, world.Settings.unlockTechnology })
            if (!GameUnitsLogic.Instance.IsTechnologyUnlocked(technology)) GameUnitsLogic.Instance.GetTechnologySlot(technology)?.UnlockTechnology();
        WorldView.ShowWorld();
        for (float until = Time.realtimeSinceStartup + 5f; WorldView.Current != WorldView.Mode.World && Time.realtimeSinceStartup < until;) yield return null;
        var unit = world.Map.Units.FirstOrDefault(world.IsExpedition);
        if (unit == null)
        {
            string leader = world.Candidates().FirstOrDefault();
            Assert.IsNotNull(leader, "a captain is available for the test expedition");
            unit = world.FormExpedition(world.Map.Settlements.First(s => s.kind == SettlementKind.Capital), leader, free: true);
        }
        Assert.IsNotNull(unit);
        unit.fatigue = 80f;
        WorldView.ShowUnit(unit.id);
        yield return null;
        var view = Object.FindAnyObjectByType<WorldView>();
        Assert.IsTrue(view.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Meter track"), "party vitals render as meters");
        Capture(view.transform, "world-party");
        MeasureSave();
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("G2G_COMBAT_SHOTS")))
        {
            SymphonyWindow.ShowUnit(world, unit);
            Capture(Object.FindAnyObjectByType<SymphonyWindow>().transform, "expedition-deck");
        }
        yield return new UnityEngine.TestTools.ExitPlayMode();
    }

    private static void MeasureSave()
    {
        var save = new SaveDocument();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var formatTiles = GameSnapshot.Begin(save);
        double mainMs = watch.Elapsed.TotalMilliseconds;
        string json = System.Threading.Tasks.Task.Run(() => { save.tileColumns = formatTiles(); return JsonUtility.ToJson(save); }).GetAwaiter().GetResult();
        int count = WorldSystem.Instance.Map.Tiles.Count();
        Debug.Log($"Autosave capture: {mainMs:0.0} ms on main thread; {count} tiles; JSON {json.Length / 1048576f:0.00} MiB");
        Assert.AreEqual(count, save.tileColumns[0].values.Count);
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var window in Object.FindObjectsByType<SymphonyWindow>(FindObjectsInactive.Include)) Object.DestroyImmediate(window.gameObject);
        foreach (var window in Object.FindObjectsByType<BattleWindow>(FindObjectsInactive.Include)) Object.DestroyImmediate(window.gameObject);
    }

    [Test]
    public void FlippingAndFilteringKeepUnchangedCardsAndTheirArtwork()
    {
        var cards = Enumerable.Range(0, 8).Select(i => new CardEntry
        {
            card = new CombatCard { name = "Guard " + i, purpose = i % 2 == 0 ? SpellPurpose.Defensive : SpellPurpose.Offensive,
                effects = { new CardEffect(CardOp.Guard, CardAim.Self, 1f) } }, source = "Vanguard"
        }).ToArray();
        SymphonyWindow.Show("Vanguard / Symphony", cards);
        var window = Object.FindAnyObjectByType<SymphonyWindow>();
        var grid = window.GetComponentsInChildren<GridLayoutGroup>().Single().transform;
        var untouched = grid.GetChild(1).GetChild(0);
        grid.GetChild(0).GetChild(0).GetComponent<Button>().onClick.Invoke();
        Assert.AreSame(untouched, grid.GetChild(1).GetChild(0), "flipping one card must not rebuild the deck");
        window.GetComponentsInChildren<Button>().Single(b => b.name == "Defensive").onClick.Invoke();
        Assert.AreEqual(4, Enumerable.Range(0, grid.childCount).Count(i => grid.GetChild(i).gameObject.activeSelf));
        window.GetComponentsInChildren<Button>().Single(b => b.name == "All").onClick.Invoke();
        Assert.IsTrue(untouched != null, "filtering retains card art");
        Assert.AreEqual(8, grid.childCount);
        Capture(window.transform, "cards");
    }

    [Test]
    public void BattleSummaryHasAnAccessibleScrollableBreakdown()
    {
        var preview = new BattlePreview { title = "The Eastern Crossing", balance = 0.65f };
        preview.attacker.name = "The Dawn Vanguard"; preview.attacker.strength = 124;
        preview.attacker.sections = 6; preview.attacker.individuals = 72;
        preview.attacker.cards = 24; preview.attacker.beats = 3; preview.attacker.predictedLoss = 0.18f;
        preview.attacker.commander = "Aurelian"; preview.attacker.predicted = BattleOutcome.DecisiveVictory;
        preview.defender.name = "Ash Wolves"; preview.defender.strength = 82;
        preview.defender.sections = 4; preview.defender.individuals = 16;
        preview.defender.cards = 12; preview.defender.beats = 2; preview.defender.predictedLoss = 0.6f;
        preview.defender.predicted = BattleOutcome.CrushingDefeat;
        BattleWindow.ShowPreview(preview, true);
        var window = Object.FindAnyObjectByType<BattleWindow>();
        var label = window.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == "Left");
        StringAssert.Contains("STRENGTH", label.text);
        var toggle = window.GetComponentsInChildren<Button>().Single(b => b.name == "Show breakdown");
        Capture(window.transform, "battle-summary");
        toggle.onClick.Invoke();
        StringAssert.Contains("Deployed", label.text);
        Assert.IsNotNull(label.GetComponentInParent<ScrollRect>().content);
        Capture(window.transform, "battle-details");
    }

    private static void Capture(Transform window, string name)
    {
        string directory = Environment.GetEnvironmentVariable("G2G_COMBAT_SHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        var view = window.GetComponent<WorldView>();
        var canvases = (view == null ? window.GetComponentsInChildren<Canvas>() : Object.FindObjectsByType<Canvas>())
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var host = view == null ? new GameObject("UI capture camera") : null;
        var renderer = view == null ? null : (WorldRenderer)typeof(WorldView).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        var camera = renderer != null ? renderer.Camera : host.AddComponent<Camera>();
        if (host != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
        }
        var oldTarget = camera.targetTexture;
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2f; canvas.sortingOrder += 200; }
        Canvas.ForceUpdateCanvases();
        window.GetComponent<MonoBehaviour>().GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(window.GetComponent<MonoBehaviour>(), null);
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; canvas.sortingOrder -= 200; }
        camera.targetTexture = oldTarget;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
        if (host != null) Object.DestroyImmediate(host);
    }
}
