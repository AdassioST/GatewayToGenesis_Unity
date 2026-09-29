using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Public memory in the real scene (CultureSystem.PublicMemory.cs): the council says the Ash-Loaf Table shows the Open
/// Gate (Welcome the Wanderers in force); luxuries held back and never shared are recorded against it; the Seventh opens
/// The Hollow Table (once, its story unlocked); the story's words, values and previews change nothing; two reloads open
/// nothing again; a Legend's sponsored account (the story's own consequence) adds a version and leaves every record as
/// it was; the culture's part of the council's accord follows, capped; the Open Gate repealed lapses the link, its effect
/// gone and its history kept.
/// </summary>
public class CulturePublicMemoryPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    private static float Held(string resource) => GameUnitsLogic.Instance.GetResourceAmountExact(resource);

    private static string Saved(CultureSystem culture) => JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));

    private static void SaveAndReload(CultureSystem culture)
    {
        var saved = Saved(culture);
        typeof(CultureSystem).GetField("_state", Any).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
        culture.AfterRestore();
    }

    [UnityTest]
    public IEnumerator APromiseMeetsTheRecords_ADisputeIsAnswered_AndTheRecordsStayAsTheyWere()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || LegendProgress.Instance == null || EdictSystem.Instance == null); i++) yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Any).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        Assert.IsTrue(CultureSystem.Instance.ApplyConsequence("myth hearth", 1, "Why Were We Founded?"));
        Assert.IsTrue(CultureSystem.Instance.Name("Iridia", null, null, out _));
        yield return null;
        Scenario();

        // The window opens on the Accounts panel.
        CultureWindow.OpenAccounts();
        yield return null;
        CultureWindow.Hide();
        yield return new ExitPlayMode();
    }

    // The scenario in a plain method: an iterator's lambdas would capture its locals in a closure the domain reload loses.
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        ICultureQuery query = culture;
        var legends = LegendProgress.Instance;
        if (legends.RecruitedCount < 1) Assert.IsNotNull(legends.RecruitNext("a test"));

        // The Ash-Loaf Table becomes a custom (three Sevenths of baking).
        GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger").UnlockTechnology();
        for (int i = 0; i < 3; i++)
        {
            Give("Ash-Bread", 8f);
            Give("Earth-Beans", 4f);
            Assert.AreEqual(1, culture.Cook("ash-loaf"));
            culture.Tick();
        }
        var table = query.Traditions().Single(t => t.definition == "ash-loaf-table");
        Assert.IsTrue(table.Lived, table.progress);

        // No promise without a law in force; the Edicts established, Welcome the Wanderers becomes the law.
        Give(culture.UnityResource, 200f);
        StringAssert.Contains("not in force", culture.WhyNotLinkPromise(table.id, "open-gate"));
        if (!EdictSystem.IsEstablished) EdictSystem.Instance.Establish("a test");
        Assert.IsTrue(EdictSystem.Instance.SetStance("strangers", "welcome", out string why), why);
        StringAssert.Contains("not in force", culture.WhyNotLinkPromise(table.id, "lesson-of-hunger"), "the Measured Cradle is not the law");
        var preview = query.PreviewLinkPromise(table.id, "open-gate");
        Assert.IsTrue(preview.succeeded, preview.reason);
        Assert.AreEqual(0, culture.PublicLinks().Count, "a preview changes nothing");
        float unity = Held(culture.UnityResource);
        var linked = culture.LinkPromise(table.id, "open-gate");
        Assert.IsTrue(linked.succeeded, linked.reason);
        Assert.AreEqual(unity - culture.PublicMemoryTuning.linkUnity, Held(culture.UnityResource), 1e-3f);
        var link = culture.PublicLinks().Single();
        Assert.AreEqual(AccountKind.Official, culture.CurrentAccount(link.id).kind);

        // Luxuries held back, never shared at an open table: recorded against the Open Gate.
        foreach (var luxury in new[] { "Honeyed Grain Porridge", "Peach Soup", "Honeyed Peach Tart", "Riverfish Rice" }) Give(luxury, 10f);
        var (now, _) = culture.CompareNow(link.id);
        Assert.IsTrue(now.Gap(culture.PublicMemoryTuning), string.Join(" | ", now.actions.Select(a => $"{a.bearing}: {a.text}")));

        // The Seventh opens The Hollow Table, once, and unlocks its story.
        culture.Tick();
        var dispute = culture.OpenDisputes().Single();
        Assert.AreEqual("hollow-table", dispute.spec);
        Assert.IsTrue(dispute.records.Any(r => r.bearing == RecordBearing.Contradicts && r.text.Contains("Held in the stores")));
        Assert.IsTrue(EventVolumeManager.Instance.FindStoryNode("public_memory_hollow_table").isUnlocked, "its story is unlocked");
        Assert.AreEqual(1f, culture.Value("dispute:hollow-table"));
        Assert.AreEqual(1f, culture.Value("answer:hollow-table:acknowledge"));
        Assert.AreEqual(culture.PublicMemoryTuning.openDisputeAccord, culture.PublicAccord().points, "an open dispute weighs on the accord");
        Assert.AreEqual(culture.PublicMemoryTuning.openDisputeAccord, EdictSystem.CultureAccord);

        // The story's words, values and previews are read only.
        string before = Saved(culture);
        float unityBefore = Held(culture.UnityResource);
        string words = CultureSystem.ExpandText("{dispute_tradition} / {dispute_promise} / {dispute_against} / {dispute_places} / {dispute_sponsor} / {acknowledge_cost}");
        StringAssert.Contains("The Ash-Loaf Table", words);
        StringAssert.Contains("The Open Gate", words);
        StringAssert.DoesNotContain("{dispute_", words);
        foreach (DisputeResolution r in new[] { DisputeResolution.Acknowledge, DisputeResolution.Revise, DisputeResolution.Sponsor }) query.PreviewResolve(dispute.id, r);
        culture.Value("answer:hollow-table:sponsor");
        CultureRules.Describe("account hollow-table sponsor", 1);
        Assert.AreEqual(before, Saved(culture), "reading the story changes nothing");
        Assert.AreEqual(unityBefore, Held(culture.UnityResource));

        // Two reloads and a Seventh: nothing opens again.
        SaveAndReload(culture);
        culture.Tick();
        Assert.AreEqual(1, culture.PublicDisputes().Count);
        Assert.AreEqual(dispute.id, culture.OpenDisputes().Single().id);
        Assert.AreEqual(1, culture.AccountsOf(link.id).Count);
        CheckStoryBindingAndMissingLinks(culture, dispute.id);

        // The story's answer: a Legend tells it another way. A new version; the records and the stores as they were.
        float unityAt = Held(culture.UnityResource);
        var held = new[] { "Honeyed Grain Porridge", "Peach Soup" }.ToDictionary(r => r, Held);
        Assert.IsTrue(culture.ApplyConsequence("account hollow-table sponsor", 1, "The Hollow Table"));
        var answered = culture.PublicDispute(dispute.id);
        Assert.AreEqual(DisputeResolution.Sponsor, answered.resolution);
        Assert.AreEqual(unityAt - culture.PublicMemoryTuning.Dispute("hollow-table").sponsorUnity, Held(culture.UnityResource), 1e-3f);
        var accounts = culture.AccountsOf(link.id);
        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual(AccountKind.Sponsored, accounts.Last().kind);
        Assert.AreEqual(answered.sponsor, accounts.Last().author);
        foreach (var kv in held) Assert.AreEqual(kv.Value, Held(kv.Key), 1e-3f, $"{kv.Key}: an account moves nothing in the stores");
        Assert.AreEqual(dispute.records.Count, answered.records.Count, "the records it answered are kept as they were");
        Assert.IsFalse(culture.ApplyConsequence("account hollow-table acknowledge", 1, "The Hollow Table"), "an answered dispute is not answered again");
        culture.Tick();
        Assert.AreEqual(culture.PublicMemoryTuning.contestedAccord, culture.PublicAccord().points, "a contested account the records still contradict");

        // The Open Gate repealed: the link lapses, its effect ends, its history stays.
        EdictSystem.Instance.State.stanceCooldowns.Clear();
        Assert.IsTrue(EdictSystem.Instance.SetStance("strangers", "measured_welcome", out why), why);
        Assert.AreEqual(0f, culture.PublicAccord().points, "repeal removes the effect before the next Seventh");
        culture.Tick();
        Assert.AreEqual(LinkStatus.Lapsed, culture.PublicLinks().Single().status);
        Assert.AreEqual(0f, culture.PublicAccord().points, "no effect once the law is gone");
        Assert.AreEqual(3, culture.AccountsOf(link.id).Count, "every version is kept");
        Assert.AreEqual(DisputeResolution.Sponsor, culture.PublicDispute(dispute.id).resolution);
        string atlasState = Saved(culture);
        var atlas = CultureAtlasReadModel.Capture(culture);
        Assert.IsTrue(atlas.nodes.ContainsKey("Dispute:" + dispute.id));
        Assert.AreEqual(3, atlas.nodes.Values.Count(n => n.category == "Account"));
        Assert.AreEqual(atlasState, Saved(culture), "accounts in the atlas cannot rewrite the records");

        // The panel builds over all of it (any exception fails the test).
        AccountsPanel.Reset();
        var rows = new List<string>();
        AccountsPanel.Fill(culture, h => { }, (label, reason, tip, act) => rows.Add(label));
        Assert.IsTrue(rows.Any(r => r.Contains("lapsed")), string.Join(" | ", rows));
        AccountsPanel.Select(dispute.id);
        AccountsPanel.Fill(culture, h => StringAssert.Contains("What was recorded", h), (label, reason, tip, act) => { });
        AccountsPanel.Reset();
    }

    private static void CheckStoryBindingAndMissingLinks(CultureSystem culture, string id)
    {
        var events = EventSystemLogic.Instance;
        var field = typeof(EventSystemLogic).GetField("currentStoryNode", Any);
        var previous = field.GetValue(events);
        var state = culture.State.extensions.publicMemory;
        var bound = state.Dispute(id);
        var other = bound.Copy();
        other.id = "test-other-dispute";
        other.spec = "unbaked-loaf";
        other.traditionName = "A different remembrance";
        other.openedSeventh++;
        state.disputes.Add(other);
        string link = bound.link;
        try
        {
            field.SetValue(events, new StoryNode { nodeName = "public_memory_hollow_table" });
            culture.BeginPublicMemoryStory("public_memory_hollow_table");
            Assert.AreEqual(bound.traditionName, culture.ExpandPublicMemory("{dispute_tradition}"));
            bound.status = DisputeStatus.Resolved;
            bound.sponsor = "The actual speaker";
            Assert.AreEqual("The actual speaker", culture.ExpandPublicMemory("{dispute_sponsor}"));
            Assert.IsFalse(culture.ApplyAccountConsequence("hollow-table acknowledge"), "a bound ending cannot answer another dispute");
            bound.status = DisputeStatus.Open;
            bound.link = "missing-link";
            float unity = Held(culture.UnityResource);
            int accounts = state.accounts.Count;
            Assert.IsFalse(culture.ResolveDispute(id, DisputeResolution.Acknowledge).succeeded);
            Assert.AreEqual(unity, Held(culture.UnityResource));
            Assert.AreEqual(accounts, state.accounts.Count);
            Assert.IsTrue(GameValues.TryGet("teaching", "teaching_orders", out float orders));
            Assert.AreEqual(culture.TeachingValue("teaching_orders"), orders);
        }
        finally
        {
            state.disputes.Remove(other);
            bound.status = DisputeStatus.Open;
            bound.sponsor = null;
            bound.link = link;
            field.SetValue(events, previous);
            culture.BeginPublicMemoryStory((previous as StoryNode)?.nodeName);
        }
    }
}
