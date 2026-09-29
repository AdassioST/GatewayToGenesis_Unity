using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Living traditions in the real scene (<see cref="CultureSystem"/>, CultureSystem.Traditions.cs): Tales at the Hearth
/// held over three Sevenths becomes a custom and waits for the player; nothing is paid until it is recognised; its
/// benefit applies under its own source, survives a save and the post-load reconciliation without paying again, stops
/// when it lies dormant and returns when it is revived (its firsts never repeated). A festival makes the Capital's own
/// Evening Song with its party as bearers; a national food embraced gets its record; an older save gets honest records.
/// </summary>
public class TraditionPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static float Held(string resource) => GameUnitsLogic.Instance.GetResourceAmountExact(resource);

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    // Hold Tales at the Hearth, then let its cooldown pass one Seventh at a time.
    private static void TalesThenWait(CultureSystem culture)
    {
        Assert.IsTrue(culture.HoldActivity("tales"), culture.WhyNotActivity(culture.Life.Activity("tales")));
        for (int i = 0; i < culture.Life.Activity("tales").cooldownSevenths; i++) culture.Tick();
    }

    [UnityTest]
    public IEnumerator APracticeBecomesACustom_IsRecognised_LapsesAndRevives_ThroughASave()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Pantry.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var culture = CultureSystem.Instance;
        ICultureQuery query = culture;
        Assert.IsFalse(culture.Record(new CulturalOccurrence { key = "before-founding" }), "nothing is recorded before the founding");
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        yield return null;

        // Tales at the Hearth, three Sevenths running: emerging, then a custom waiting for the player.
        TalesThenWait(culture);
        var tales = query.Traditions().Single(t => t.definition == "hearth-tales");
        Assert.AreEqual(TraditionStage.Emerging, tales.stage);
        Assert.AreEqual(-1, tales.settlement, "a rite from the window has no known place: the nation's");
        CollectionAssert.IsEmpty(tales.bearers);
        TalesThenWait(culture);
        float unity = culture.UnityHeld;
        Assert.IsTrue(culture.HoldActivity("tales"));
        culture.Tick();
        tales = query.Tradition(tales.id);
        Assert.AreEqual(TraditionStage.Practiced, tales.stage);
        Assert.AreEqual(TraditionRecognition.Offered, tales.recognition);
        Assert.AreEqual(1, query.PendingTraditionChoices().Count);
        Assert.AreEqual(1f, GameValues.Get("culture", "tradition_choices"));
        Assert.GreaterOrEqual(culture.UnityHeld, unity, "crossing the threshold spent nothing");
        string source = CultureEffectPolicy.Source(culture.TraditionTuning.Definition("hearth-tales").name);
        Assert.IsTrue(EffectRouter.HasSource(source), "a lived custom gives its practice benefit");
        Assert.AreEqual(1, culture.Moments.Count(m => m.kind == "tradition"));

        // The Traditions panel lists it and, opened, offers the three choices (building it throws nothing).
        CheckPanel(culture);
        CultureWindow.Open();
        var window = Object.FindAnyObjectByType<CultureWindow>();
        var modes = typeof(CultureWindow).GetNestedType("LifeMode", BindingFlags.NonPublic);
        typeof(CultureWindow).GetMethod("SetLife", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new[] { System.Enum.Parse(modes, "Traditions") });
        yield return null;
        Assert.IsTrue(CultureWindow.IsOpen);
        CultureWindow.Hide();

        // The preview changes nothing; recognising pays once.
        Give("Unity", 100f);
        var preview = query.PreviewRecognizeTradition(tales.id);
        Assert.IsTrue(preview.succeeded, preview.reason);
        Assert.AreEqual(TraditionRecognition.Offered, query.Tradition(tales.id).recognition, "a preview is read-only");
        float held = culture.UnityHeld;
        var result = culture.RecognizeTradition(tales.id);
        Assert.IsTrue(result.succeeded, result.reason);
        Assert.AreEqual(held - culture.TraditionTuning.Definition("hearth-tales").recognitionUnity, culture.UnityHeld, 0.01f);
        Assert.IsFalse(culture.RecognizeTradition(tales.id).succeeded, "recognised once");
        Assert.AreEqual(1, culture.Moments.Count(m => m.kind == "tradition-recognized"));

        // A festival in the Capital: its own Evening Song, carried by the party.
        var capital = WorldCivilization.Capital(WorldSystem.Instance.Map);
        Give("Dried Auric Peaches", 40f);
        Give("Earth-Beans", 40f);
        Assert.IsTrue(culture.PrepareFestival(capital), culture.WhyNotFestival(capital));
        Assert.IsNotNull(culture.CelebrateFestival(capital, new[] { "Vaelia" }));
        Assert.AreEqual(1, query.PendingOccurrences().Count(o => o.kind == CulturalOccurrenceKind.Festival));
        culture.Tick();
        var song = query.TraditionsAt(capital.id).Single(t => t.definition == "evening-song");
        CollectionAssert.AreEqual(new[] { "Vaelia" }, song.bearers);
        StringAssert.Contains(capital.name, song.origin);

        // A national food embraced gets its record at once.
        var soup = new Foodway { resource = "Peach Soup", cuisine = FoodClass.Edible, familiarity = 0.9f };
        culture.State.foodways.Add(soup);
        culture.State.pendingFood = "Peach Soup";
        Assert.IsTrue(culture.EmbraceNationalFood("test"));
        Assert.IsNotNull(soup.tradition);
        Assert.AreEqual(TraditionRules.NationalTable, query.Tradition(soup.tradition).definition);

        // A save: the same traditions after it, the benefit re-applied from state, nothing paid or remembered again.
        int moments = culture.Moments.Count;
        float unityBefore = culture.UnityHeld;
        string describe = Describe(query);
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        EffectRouter.RemoveSource(source);
        culture.AfterRestore();
        culture.AfterRestore();
        Assert.AreEqual(describe, Describe(query), "the same practice history after a load");
        Assert.IsTrue(EffectRouter.HasSource(source), "reconciled from saved state");
        Assert.AreEqual(moments, culture.Moments.Count);
        Assert.AreEqual(unityBefore, culture.UnityHeld, 0.01f);

        // No one keeps the tales: dormant, the benefit gone, the history kept.
        int participations = query.Tradition(tales.id).participations;
        for (int i = 0; i <= culture.TraditionTuning.lapseSevenths + 1; i++) culture.Tick();
        tales = query.Tradition(tales.id);
        Assert.AreEqual(TraditionStage.Dormant, tales.stage);
        Assert.IsFalse(EffectRouter.HasSource(source), "dormancy removes its effects");
        Assert.AreEqual(participations, tales.participations);
        Assert.AreEqual(TraditionRecognition.Recognized, tales.recognition);

        // Taken up again: revived, its benefit back, remembered once.
        TalesThenWait(culture);
        TalesThenWait(culture);
        tales = query.Tradition(tales.id);
        Assert.AreEqual(TraditionStage.Revived, tales.stage);
        Assert.IsTrue(EffectRouter.HasSource(source));
        Assert.AreEqual(1, culture.Moments.Count(m => m.kind == "tradition-revived"));
        Assert.AreEqual(1, culture.Moments.Count(m => m.kind == "tradition-recognized"), "revival repeats no first");

        // An older save (no envelope): honest national-food records, no invented history.
        var node = SaveStateCodec.Capture(culture, "_state");
        var stateNode = node.children.Single(c => c.name == "_state");
        stateNode.children.RemoveAll(c => c.name == "extensions");
        foreach (var way in stateNode.children.Single(c => c.name == "foodways").children) way.children.RemoveAll(c => c.name == "tradition");
        SaveStateCodec.Restore(culture, node, "_state");
        culture.AfterRestore();
        var records = query.Traditions();
        Assert.AreEqual(1, records.Count, "only the national food's embrace is linked");
        Assert.IsTrue(records[0].legacy);
        Assert.AreEqual(0, records[0].participations);
        Assert.IsFalse(EffectRouter.HasSource(source), "no benefit survives from a record the save did not have");
        yield return new ExitPlayMode();
    }

    // A plain method, not the iterator: lambdas capturing the iterator's locals lose their closure at EnterPlayMode.
    private static void CheckPanel(CultureSystem culture)
    {
        var rows = new System.Collections.Generic.List<(string label, string why, System.Action act)>();
        string header = null;
        TraditionPanel.Reset();
        TraditionPanel.Fill(culture, h => header = h, (label, why, tip, act) => rows.Add((label, why, act)));
        StringAssert.Contains("await", header);
        rows.Single(r => r.label.Contains("Tales at the Hearth")).act();
        rows.Clear();
        TraditionPanel.Fill(culture, h => header = h, (label, why, tip, act) => rows.Add((label, why, act)));
        Assert.AreEqual(3, rows.Count(r => r.label.StartsWith("Recognise") || r.label.StartsWith("Preserve") || r.label.StartsWith("Decide later")));
        TraditionPanel.Reset();
    }

    private static string Describe(ICultureQuery query) => string.Join("\n", query.Traditions().Select(t =>
        $"{t.id} {t.definition}@{t.settlement} {t.stage} {t.recognition} {t.momentum:0.###} {t.participations}/{t.distinctSevenths} {t.origin} [{string.Join(",", t.bearers)}]"));
}
