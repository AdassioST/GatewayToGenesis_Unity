using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class LegendRelationshipPlayTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator StoriesExpeditionsAndJournalSharePersistentRelationships()
    {
        EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (LegendProgress.Instance == null || LegendProgress.Instance.RecruitedCount < 2 || WorldSystem.Instance?.Map == null || EventVolumeManager.Instance?.FindStoryNode("ballad_ruin_song_1") == null); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        Prepare();
        TestExpedition();
        StartStory();
        for (int i = 0; i < 8; i++) yield return null;
        AssertCastVisible();
        Capture("legend-cast");
        FinishStory();
        for (int i = 0; i < 3; i++) yield return null;
        OpenJournal();
        for (int i = 0; i < 3; i++) yield return null;
        AssertJournal();
        Capture("ballad-journal");
        var view = Object.FindAnyObjectByType<BalladJournalView>();
        typeof(BalladJournalView).GetField("_relationships", Private).SetValue(view, true);
        typeof(BalladJournalView).GetMethod("Refresh", Private).Invoke(view, new object[] { true });
        for (int i = 0; i < 3; i++) yield return null;
        Capture("legend-bonds");
        TestContinuation();
        TestCapacityAndSnapshot();
        yield return new ExitPlayMode();
    }

    private static string[] Pair => LegendProgress.Instance.RecruitedNames.Take(2).ToArray();
    private static LegendRelationship Tie(string from, string to) => LegendProgress.Instance.Relationships(from).Single(b => b.other == to);
    private static void Prepare()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
        TimeSystemLogic.Instance.PauseTime(true);
    }

    private static void TestExpedition()
    {
        var pair = Pair;
        var world = WorldSystem.Instance;
        var unit = new WorldUnit { id = 99999, name = "Relationship test expedition", leader = pair[0], coord = world.Map.Capital };
        unit.companions.Add(pair[1]);
        var strike = typeof(WorldSystem).GetMethod("Strike", Private);
        var legends = LegendProgress.Instance;
        var thresholds = new System.Collections.Generic.List<int>();
        var transitions = new System.Collections.Generic.List<int>();
        System.Action<string, string, int> onThreshold = (from, to, direction) => { if (from == pair[0]) thresholds.Add(direction); };
        System.Action<string, string, int> onStage = (from, to, direction) => { if (from == pair[0]) transitions.Add(direction); };
        legends.RelationshipThresholdReached += onThreshold;
        legends.RelationshipStageChanged += onStage;
        strike.Invoke(world, new object[] { unit, new Mishap { target = pair[0], helper = pair[1], outcome = MishapOutcome.Eased,
            spec = new MishapSpec { kind = MishapKind.Injury, name = "A wounded companion", strain = 5f } } });
        Assert.AreEqual(0, Tie(pair[0], pair[1]).stage);
        Assert.AreEqual(68, Tie(pair[0], pair[1]).affection);
        Assert.AreEqual(0, Tie(pair[1], pair[0]).stage);
        Assert.AreEqual(1, Tie(pair[0], pair[1]).encounters, "A helper and shared-party hook must count the same moment once");
        for (int i = 0; i < 2; i++)
        {
            strike.Invoke(world, new object[] { unit, new Mishap { target = pair[0], helper = pair[1], outcome = MishapOutcome.Eased,
                spec = new MishapSpec { kind = MishapKind.Injury, name = "A wounded companion", strain = 1f } } });
            if (i == 0) { Assert.AreEqual(0, Tie(pair[0], pair[1]).stage); CollectionAssert.AreEqual(new[] { 1 }, thresholds); Assert.IsEmpty(transitions); }
        }
        Assert.AreEqual(1, Tie(pair[0], pair[1]).stage);
        Assert.AreEqual(50, Tie(pair[0], pair[1]).affection);
        CollectionAssert.AreEqual(new[] { 1 }, transitions);
        for (int i = 0; i < 3; i++)
        strike.Invoke(world, new object[] { unit, new Mishap { target = pair[0], second = pair[1], outcome = MishapOutcome.Struck,
            spec = new MishapSpec { kind = MishapKind.Quarrel, name = "A bitter quarrel", strain = 2f } } });
        Assert.AreEqual(0, Tie(pair[0], pair[1]).stage, "An unresolved quarrel fractures the tie");
        Assert.AreEqual("Void", Tie(pair[0], pair[1]).thread, "The formative material survives a quarrel");
        CollectionAssert.AreEqual(new[] { 1, -1 }, thresholds);
        CollectionAssert.AreEqual(new[] { 1, -1 }, transitions);
        legends.RelationshipThresholdReached -= onThreshold;
        legends.RelationshipStageChanged -= onStage;
        // Prepare only this direction; the subsequent authored story supplies the actual stage-changing moment.
        legends.Relate(pair[0], pair[1], "prepared", "Time spent together", 0, affectionChange: 35);
        Assert.AreEqual(0, Tie(pair[0], pair[1]).stage);
    }

    private static void StartStory()
    {
        var pair = Pair;
        var events = EventSystemLogic.Instance;
        events.Summon("ballad_ruin_song_1", pair[0], new[] { pair[1] });
        events.TriggerStory(EventVolumeManager.Instance.FindStoryNode("ballad_ruin_song_1"));
        Assert.IsTrue(events.isEventActive);
        Assert.AreEqual(pair[0], events.CurrentCast.protagonist);
        CollectionAssert.Contains(events.CurrentCast.coProtagonists, pair[1]);
        events.RememberStoryChoice("Stayed beside the wounded companion");
        foreach (var consequence in EventScript.ParseConsequences("affection:protagonist > co | Void +1")) events.AddConsequence(consequence);
    }

    private static void AssertCastVisible()
    {
        var view = Object.FindAnyObjectByType<BalladActorsView>();
        var root = (RectTransform)typeof(BalladActorsView).GetField("_root", Private).GetValue(view);
        Assert.IsTrue(root.gameObject.activeInHierarchy);
        string text = string.Join("\n", root.GetComponentsInChildren<TextMeshProUGUI>().Select(t => t.text));
        StringAssert.Contains("Protagonist", text);
        StringAssert.Contains("Co-protagonist", text);
        foreach (var name in Pair) StringAssert.Contains(name, text);
    }

    private static void FinishStory()
    {
        var pair = Pair;
        EventVolumeManager.Instance.CompleteStory();
        TimeSystemLogic.Instance.PauseTime(true);
        Assert.AreEqual(1, Tie(pair[0], pair[1]).stage);
        Assert.AreEqual(0, Tie(pair[1], pair[0]).stage, "The authored directional test must not alter the reverse reading");
        Assert.IsTrue(GameValues.TryGet("affection", pair[0] + " | " + pair[1], out float stage));
        Assert.AreEqual(1f, stage);
        Assert.IsTrue(GameValues.TryGet("affection_progress", pair[0] + " | " + pair[1], out float progress));
        Assert.AreEqual(50f, progress);
        Assert.IsFalse(GameValues.TryGet("affection", "Unknown | " + pair[1], out _));
        var ballad = EventSystemLogic.Instance.Ballads.Single(b => b.id == "ruin_song");
        StringAssert.Contains("wounded companion", ballad.lastChoice);
        Assert.IsNotEmpty(ballad.lastDate);
        int memories = Tie(pair[0], pair[1]).encounters;
        EventSystemLogic.Instance.OnStoryCompleted();
        Assert.AreEqual(memories, Tie(pair[0], pair[1]).encounters);
        var next = EventVolumeManager.Instance.FindStoryNode("ballad_ruin_song_2");
        Assert.IsFalse(EventVolumeManager.Instance.ContinueBallad(next), "Unexplored spires still gate the next verse");
    }

    private static void OpenJournal()
    {
        var view = Object.FindAnyObjectByType<BalladJournalView>();
        Assert.IsNotNull(view);
        typeof(BalladJournalView).GetMethod("Toggle", Private).Invoke(view, null);
    }

    private static void AssertJournal()
    {
        var view = Object.FindAnyObjectByType<BalladJournalView>();
        var root = (RectTransform)typeof(BalladJournalView).GetField("_root", Private).GetValue(view);
        Assert.IsTrue(root.gameObject.activeInHierarchy);
        string text = string.Join("\n", root.GetComponentsInChildren<TextMeshProUGUI>().Select(t => t.text));
        StringAssert.Contains("Ongoing", text);
        StringAssert.Contains("Last developed:", text);
        StringAssert.Contains("wounded companion", text);
        StringAssert.Contains("Next verse 2", text);
        Assert.IsNotNull(root.GetComponent<Image>(), "Journal blocks click-through");
    }

    private static void TestCapacityAndSnapshot()
    {
        var legends = LegendProgress.Instance;
        foreach (var data in GameCatalog.Legends.All.Where(l => l != null).Take(10)) legends.Recruit(data, "Relationship test");
        for (int i = legends.RecruitedCount; i < 10; i++)
            typeof(LegendProgress).GetMethod("AddRecord", Private).Invoke(legends, new object[] { "Relationship fixture " + i });
        string from = Pair[0];
        foreach (var to in legends.RecruitedNames.Where(n => n != from).ToList()) legends.Relate(from, to, "capacity", "A shared wound", 1, "Strand", true);
        Assert.AreEqual(7, legends.Relationships(from).Count(b => b.Significant));
        var snapshot = SaveStateCodec.Capture(legends, "_recruited", "_started");
        string before = legends.RelationshipText(from);
        var old = legends.Relationships(from).First(b => b.Significant);
        Assert.IsTrue(legends.SeverRelationship(from, old.other));
        Assert.AreEqual(6, legends.Relationships(from).Count(b => b.Significant));
        SaveStateCodec.Restore(legends, snapshot, "_recruited", "_started");
        Assert.AreEqual(before, legends.RelationshipText(from));
        // A pre-relationships save gets an empty list for every nested legend record.
        foreach (var entry in snapshot.children.Single(n => n.name == "_recruited").children)
            entry.children[1].children.RemoveAll(n => n.name == "relationships");
        SaveStateCodec.Restore(legends, snapshot, "_recruited", "_started");
        Assert.IsEmpty(legends.Relationships(from));
    }

    private static void TestContinuation()
    {
        var volumes = EventVolumeManager.Instance;
        var next = volumes.FindStoryNode("ballad_ruin_song_2");
        var conditions = next.storyConditions.ToList();
        next.isUnlocked = true;
        next.storyConditions.Clear();
        Assert.IsTrue(volumes.ContinueBallad(next), "A ready earliest verse can be continued deliberately");
        Assert.AreEqual(Pair[0], EventSystemLogic.Instance.CurrentCast.protagonist, "Actors carry across verses");
        Assert.IsFalse(volumes.ContinueBallad(next), "Cannot start a second event while one is active");
        volumes.CompleteStory();
        TimeSystemLogic.Instance.PauseTime(true);
        Assert.IsFalse(volumes.ContinueBallad(next), "Told verses cannot be replayed from the journal");
        next.storyConditions.AddRange(conditions);
    }

    // Optional reproducible UI captures, requested through an environment variable; no scene assets are changed.
    private static void Capture(string name)
    {
        string directory = System.Environment.GetEnvironmentVariable("G2G_LEGEND_SHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        var cameraObject = new GameObject("Legend UI capture");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.07f, 0.10f);
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2f; }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        Object.Destroy(texture); Object.Destroy(target); Object.Destroy(cameraObject);
    }
}
