using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Shared wounds in the real scene (CultureSystem.Memory.cs): a town falls and is remembered once; a quiet remembrance
/// needs nothing; the Hunger's passage is remembered and the Ash-Loaf dedicated to it, kept when a batch is baked; the
/// memory survives a save and a second reading of the world's records; the next Age passage carries its lineage.
/// </summary>
public class CultureMemoryPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    // An Age passes as AgeProgression reports it (the record kept in its history, then the event).
    private static void PassAge(string ageId, string ageTitle, string crisis, string nextId, string nextTitle, int deaths)
    {
        var ages = AgeProgression.Instance;
        var history = (System.Collections.Generic.List<AgeRecord>)typeof(AgeProgression).GetField("_history", Any).GetValue(ages);
        var record = new AgeRecord { ageId = ageId, ageTitle = ageTitle, crisisTitle = crisis, nextAgeId = nextId, nextAgeTitle = nextTitle, populationBefore = 100, deaths = deaths };
        history.Add(record);
        ((Action<AgeRecord>)typeof(AgeProgression).GetField("AgePassed", Any).GetValue(ages))?.Invoke(record);
    }

    // What a load does after the systems are restored (T01's AfterRestore when it is there).
    private static void AfterRestore(CultureSystem culture)
    {
        var after = typeof(CultureSystem).GetMethod("AfterRestore", Any, null, Type.EmptyTypes, null);
        if (after != null) after.Invoke(culture, null);
        else typeof(CultureSystem).GetMethod("ReconcileMemory", Any).Invoke(culture, new object[] { CultureReconcileReason.Restored });
    }

    // A town a few cells from the Capital (a static helper: lambdas in the test body lose their closure after EnterPlayMode).
    private static Settlement FoundTown()
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        // A new game has surveyed little: the fixture surveys the cell first, as an expedition would.
        // A Developing Town inside the authority, else an Outpost just beyond it.
        foreach (var kind in new[] { SettlementKind.Town, SettlementKind.Outpost })
            foreach (var t in map.Tiles.Where(t => !t.water && HexCoord.Distance(t.coord, map.Capital) >= 2 && HexCoord.Distance(t.coord, map.Capital) <= 8).OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).ThenBy(t => t.index))
            {
                bool explored = t.explored, known = t.known;
                t.explored = t.known = true;
                if (WorldCivilization.WhyNotFound(map, world.Settings.generation, world.Rules, t.coord, kind) == null)
                    return WorldCivilization.Found(map, world.Settings.generation, world.Rules, t.coord, kind, GameAge.Number);
                t.explored = explored;
                t.known = known;
            }
        return null;
    }

    [UnityTest]
    public IEnumerator AFallAndAFamineAreRememberedOnce_KeptThroughASave_AndCarriedIntoTheNextAge()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || AgeProgression.Instance == null); i++) yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Any).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var culture = CultureSystem.Instance;
        Assert.IsTrue(culture.ApplyConsequence("myth hearth", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        yield return null;
        culture.Tick();

        // A town near the Capital falls: remembered once, as it happened.
        var world = WorldSystem.Instance;
        var town = FoundTown();
        Assert.IsNotNull(town, "somewhere to found a town");
        string townName = town.name;
        Assert.IsTrue(world.Pillage(town, 1000f, "in a test"), "the town falls");
        var fall = culture.MemoryCauses().Single(e => e.cause == MemoryCause.SettlementFall);
        Assert.AreEqual($"The fall of {townName}", fall.title);
        Assert.IsFalse(fall.reconstructed, "witnessed as it happened");
        Assert.GreaterOrEqual(fall.cell, 0, "its ruins are its place");
        culture.Tick();
        Assert.AreEqual(1, culture.MemoryCauses().Count, "a Seventh reading the world's records adds nothing");

        // Ordinary mourning: free, whatever the stores and morale.
        Assert.IsNull(culture.WhyNotRemembrance(fall.id), culture.WhyNotRemembrance(fall.id));
        var quiet = culture.KeepRemembrance(fall.id);
        Assert.IsTrue(quiet.succeeded, quiet.reason);
        Assert.AreEqual(0, quiet.paid.Count, "nothing spent");
        Assert.AreEqual(1, culture.RemembranceMorale);
        Assert.IsFalse(culture.KeepRemembrance(fall.id).succeeded, "once a Seventh at most");
        Assert.AreEqual(0f, culture.MemorialGroundOf(fall.id)?.Bonus ?? 0f, "a memorial place gives no ecological bonus");

        // The Hunger passes: its crisis is remembered, and the Ash-Loaf (baked once Echoes of Hunger is known) carries it.
        PassAge("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", "age-of-renewal", "Age of Renewal", 30);
        var hunger = culture.MemoryCauses().Single(e => e.cause == MemoryCause.Crisis);
        Assert.AreEqual("The Inescapable Hunger", hunger.title);
        Assert.AreEqual(30, hunger.deaths);
        var loaf = CultureEntityRef.Of(CultureEntityKind.Recipe, "ash-loaf");
        StringAssert.Contains("does not exist", culture.WhyNotDedicate(hunger.id, loaf), "no Ash-Loaf before Echoes of Hunger");
        GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger").UnlockTechnology();
        Assert.IsTrue(culture.SuggestionsFor(hunger.id).Any(s => s.target == "ash-loaf" && s.status == CanonStatus.ExplicitCanon));
        var dedicated = culture.Dedicate(hunger.id, loaf);
        Assert.IsTrue(dedicated.succeeded, dedicated.reason);
        Assert.IsFalse(culture.Dedicate(fall.id, loaf).succeeded, "one memory per dish");
        Give("Ash-Bread", 20f);
        Give("Earth-Beans", 10f);
        Assert.AreEqual(1, culture.Cook("ash-loaf"));
        culture.Tick();
        var d = culture.DedicationThrough(loaf);
        Assert.IsNotNull(d);
        Assert.AreEqual(1, d.practiced, "the batch baked kept the memory");
        Assert.AreEqual(2, culture.RemembranceMorale, "two memories kept, two shares (under the cap)");

        // A save and a load: nothing is duplicated, nothing given again.
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", Any).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        AfterRestore(culture);
        AfterRestore(culture);
        Assert.AreEqual(2, culture.MemoryCauses().Count);
        Assert.AreEqual(1, culture.MemorialDedications().Count);
        Assert.AreEqual(1, culture.DedicationThrough(loaf).practiced);
        culture.Tick();
        Assert.AreEqual(2, culture.MemoryCauses().Count, "loss, reload and a Seventh: still one memorial each");

        // The next Age passes: the lineage is kept and the Ash-Loaf goes on, awaiting recognition.
        PassAge("age-of-renewal", "Age of Renewal", "The Great Plague", "age-of-embers", "Age of Embers", 5);
        Assert.AreEqual(3, culture.MemoryCauses().Count);
        var lineage = culture.MemoryLineages();
        Assert.AreEqual(2, lineage.Count, "one lineage per passage");
        Assert.AreEqual(1, lineage[1].dedications.Count);
        d = culture.DedicationThrough(loaf);
        CollectionAssert.Contains(d.ages, "age-of-embers");
        Assert.IsTrue(d.inherited);
        Assert.IsNull(d.recognizedAge, "nothing is recognized or canonized by itself");

        string atlasBefore = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        var atlas = CultureAtlasReadModel.Capture(culture);
        Assert.LessOrEqual(CultureAtlasTests.Distance(atlas, "Recipe:ash-loaf", "Evidence:" + hunger.id), 3);
        Assert.IsTrue(atlas.nodes.Values.Any(n => n.category == "Lineage"));
        Assert.AreEqual(atlasBefore, JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state")), "atlas does not change the memorial recipe or its history");
        CultureAtlasWindow.Open("Recipe:ash-loaf");
        yield return null;
        Assert.IsTrue(CultureAtlasWindow.IsOpen);
        var atlasWindow = UnityEngine.Object.FindAnyObjectByType<CultureAtlasWindow>();
        CaptureAtlas(atlasWindow, "atlas-wide", 1920, 1080);
        var root = (RectTransform)typeof(CultureAtlasWindow).GetField("_root", Any).GetValue(atlasWindow);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(520, 700);
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(atlasBefore), "_state");
        culture.AfterRestore();
        yield return null;
        typeof(CultureAtlasWindow).GetMethod("Text", Any).Invoke(atlasWindow, new object[] { new string('W', 240), true });
        Canvas.ForceUpdateCanvases();
        var scroll = atlasWindow.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        Assert.Greater(scroll.viewport.rect.width, 400);
        Assert.Greater(scroll.content.rect.height, 0);
        foreach (var button in scroll.content.GetComponentsInChildren<UnityEngine.UI.Button>())
        {
            var rect = (RectTransform)button.transform;
            Assert.Greater(rect.rect.height, 0, button.name);
            Assert.LessOrEqual(rect.rect.width, scroll.viewport.rect.width, button.name);
            if (button.interactable) Assert.IsNotNull(button.navigation.selectOnDown, "keyboard navigation must survive a reload");
        }
        root.anchorMin = new Vector2(.04f, .04f); root.anchorMax = new Vector2(.96f, .96f);
        root.sizeDelta = Vector2.zero;
        CaptureAtlas(atlasWindow, "atlas-narrow", 720, 900);
        CultureAtlasWindow.Open("Recipe:missing-after-load");
        yield return null;
        Assert.IsTrue(atlasWindow.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("cannot be inferred")));
        UnityEngine.Object.Destroy(atlasWindow.gameObject);

        // The panel builds over it (any exception fails the test).
        CultureWindow.Open();
        yield return null;
        var window = UnityEngine.Object.FindAnyObjectByType<CultureWindow>();
        var mode = typeof(CultureWindow).GetNestedType("LifeMode", BindingFlags.NonPublic);
        typeof(CultureWindow).GetMethod("SetLife", Any).Invoke(window, new[] { Enum.Parse(mode, "Memorials") });
        yield return null;
        CultureWindow.Hide();
        yield return new ExitPlayMode();
    }

    private static void CaptureAtlas(CultureAtlasWindow window, string name, int width, int height)
    {
        string directory = Environment.GetEnvironmentVariable("G2G_CULTURE_SHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        var canvas = window.GetComponentInChildren<Canvas>();
        var go = new GameObject("Atlas capture");
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.07f, .06f, .09f);
        var target = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(go);
        }
    }
}
