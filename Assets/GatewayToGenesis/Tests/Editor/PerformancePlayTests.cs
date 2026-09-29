using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Performances in the real scene (T08, CultureSystem.Performance.cs): the later Ages' chorus is locked in the early
/// Age; two casts give different, explained outcomes and previewing changes nothing; a plan called off or walked away
/// from gives nothing and frees the party; a planned performance is given once at the end of its festival (the
/// festival's own Meaning only, one occurrence, one shared memory, no new tie), a replay and a reload give nothing more,
/// and its echo eases the Capital's strain.
/// </summary>
public class PerformancePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);
    private static string Snapshot(CultureSystem culture) => JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));

    [UnityTest]
    public IEnumerator APartyPerformsOnce_AndWhoSingsDecidesHowItLands()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Pantry.Instance == null || LegendProgress.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
        for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;) yield return null;
        Scenario();
        yield return new ExitPlayMode();
    }

    // The whole scenario in a plain method: an iterator's locals and closures made before EnterPlayMode are lost.
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        var world = WorldSystem.Instance;
        var map = world.Map;
        var capital = WorldCivilization.Capital(map);
        var legends = LegendProgress.Instance;
        ICultureQuery query = culture;
        TimeSystemLogic.Instance.PauseTime(true);
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        var festival = GameUnitsLogic.Instance.GetTechnologySlot(culture.Life.festivalTechnology);
        if (festival != null && !GameUnitsLogic.Instance.IsTechnologyUnlocked(culture.Life.festivalTechnology)) festival.UnlockTechnology();
        while (legends.RecruitedCount < 3) Assert.IsNotNull(legends.RecruitNext("a test"));
        var names = legends.RecruitedNames.Take(3).ToArray();
        string a = names[0], b = names[1], c = names[2];
        legends.Soul(a).strain = 0f;
        legends.Soul(b).strain = 0f;
        legends.Soul(c).strain = 45f;
        Assert.AreEqual(ComposureState.Fractured, legends.Composure(c));
        PopGrowthLogic.Instance.isFoodScarce = false;
        Give("Deep-Rooted Grain", 300f);

        var party = new WorldUnit { id = 90808, name = "Test Cultural Party", spec = world.ExpeditionRules.unit, charter = ExpeditionCharter.CulturalParty, leader = a, coord = capital.coord };
        party.companions.Add(b);
        map.Units.Add(party);
        try
        {
            // The later Ages' chorus is out of reach in an early Age: its Age comes first.
            var choices = culture.RepertoireChoices(party);
            StringAssert.Contains("Age IV", choices.First(x => x.spec.id == "polyphonic-chorus").why, "advanced magic is inaccessible early");

            // Two casts, two outcomes, each explained; previewing changes nothing.
            string before = Snapshot(culture);
            var calm = culture.PreviewPerformance(party, "evening-song", PerformanceIntent.Reassurance);
            Assert.IsTrue(calm.ok, calm.reason);
            Assert.AreEqual(before, Snapshot(culture), "a preview never changes state");
            party.companions[0] = c;
            var strained = culture.PreviewPerformance(party, "evening-song", PerformanceIntent.Reassurance);
            party.companions[0] = b;
            Assert.Less(strained.evaluation.score, calm.evaluation.score, string.Join("\n", strained.evaluation.Lines));
            Assert.IsTrue(strained.evaluation.Lines.Any(l => l.Contains(c + " Fractured")), "the reason is shown");

            // Called off, and walked away from: nothing given, the party freed.
            Assert.IsTrue(culture.PlanPerformance(party, "evening-song", PerformanceIntent.Reassurance).succeeded);
            StringAssert.Contains("already plans", culture.PlanPerformance(party, "evening-song", PerformanceIntent.Celebration).reason);
            Assert.IsTrue(culture.CancelPerformance(culture.PerformanceOf(party.id).id).succeeded);
            Assert.IsNull(culture.PerformanceOf(party.id));
            Assert.AreEqual(PerformanceStatus.Cancelled, query.PerformanceHistory(1).Single().status);
            Assert.IsTrue(culture.PlanPerformance(party, "evening-song", PerformanceIntent.Reassurance).succeeded);
            var away = map.NeighboursOf(map.Get(capital.coord)).First(t => t.settlement < 0 && !t.water && !t.impassable);
            party.coord = away.coord;
            culture.Tick();
            party.coord = capital.coord;
            Assert.IsNull(culture.PerformanceOf(party.id), "a party that left gives nothing");
            StringAssert.Contains("left", query.PerformanceHistory(1).Single().text);
            Assert.IsEmpty(query.PerformanceEchoes());
            Assert.AreEqual(0, culture.PerformancesCompleted);

            // Planned, then given at the end of its festival: once.
            Assert.IsTrue(culture.PlanPerformance(party, "evening-song", PerformanceIntent.Reassurance).succeeded);
            Assert.IsTrue(world.HoldFestival(party), world.WhyNotFestival(party));
            culture.Tick();
            Assert.AreEqual(PerformanceStatus.Performing, culture.PerformanceOf(party.id).status);
            string id = culture.PerformanceOf(party.id).id;
            int meaning = legends.Fragments(a, FragmentKind.Meaning);
            bool significant = legends.Relationships(a).FirstOrDefault(x => x.other == b)?.Significant ?? false;
            typeof(WorldSystem).GetMethod("FinishWork", Private).Invoke(world, new object[] { party, world.SpecOf(party), new List<string>() });
            var record = query.PerformanceHistory(1).Single();
            Assert.AreEqual(PerformanceStatus.Completed, record.status, record.text);
            Assert.AreEqual(id, record.id);
            Assert.IsNotEmpty(record.factors);
            Assert.AreEqual(culture.Life.festivalFragments, legends.Fragments(a, FragmentKind.Meaning) - meaning, "only the festival's own Meaning: the performance adds none");
            var bond = legends.Relationships(a).Single(x => x.other == b);
            CollectionAssert.Contains(bond.moments, "performance:" + id, "a shared memory");
            Assert.AreEqual(significant, bond.Significant, "never a new significant tie");
            var o = query.PendingOccurrences().Single(x => x.key == "performance:" + id);
            Assert.AreEqual(CulturalOccurrenceKind.Performance, o.kind);
            CollectionAssert.AreEquivalent(new[] { a, b }, o.actors);
            var echo = query.PerformanceEchoes().Single();
            Assert.AreEqual(capital.id, echo.settlement);

            // A replay gives nothing more.
            int moments = bond.moments.Count;
            Assert.IsNull(culture.CompletePerformance(party, capital, new[] { a, b }));
            Assert.AreEqual(1, culture.PerformancesCompleted);
            Assert.AreEqual(moments, legends.Relationships(a).Single(x => x.other == b).moments.Count);
            Assert.AreEqual(1, query.PerformanceEchoes().Count);

            // The echo eases the Capital a little each Seventh; no chorus, no Coherence lent.
            capital.strain = 30f;
            culture.Tick();
            Assert.LessOrEqual(capital.strain, 30f - echo.relief + 1e-3f);
            Assert.IsNotNull(CityDevelopment.CoherenceOverlay);
            Assert.AreEqual(0f, culture.CoherenceOverlay(map.Get(capital.coord)));

            // Everything reads as text: the panel, the window's body and the party's card.
            CheckText(culture, world, party);

            // A reload keeps it all and gives nothing twice.
            var saved = Snapshot(culture);
            typeof(CultureSystem).GetField("_state", Private).SetValue(culture, new CultureState());
            SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
            culture.AfterRestore();
            Assert.AreEqual(1, culture.PerformancesCompleted);
            Assert.AreEqual(id, query.PerformanceHistory(1).Single().id);
            Assert.AreEqual(1, query.PerformanceEchoes().Count);
            Assert.IsNull(culture.CompletePerformance(party, capital, new[] { a, b }), "a reload does not replay a performance");
            CheckCalendarHost(culture, party, capital, a, b);
        }
        finally
        {
            map.Units.Remove(party);
            TimeSystemLogic.Instance.PauseTime(false);
        }
    }

    private static void CheckCalendarHost(CultureSystem culture, WorldUnit party, Settlement capital, string a, string b)
    {
        party.workLeft = 0; party.task = UnitTask.None; party.path.Clear();
        var now = ObservanceCalendar.Parts(culture.Today + 1);
        var host = new Observance { id = "obs-calendar-test", definition = ObservanceRules.Anniversary, name = "The Named Evening", objective = ObservanceObjective.Celebration,
            settlement = capital.id, recurrence = ObservanceRecurrence.EveryEcho, seventh = now.seventh, phase = now.phase, scale = ObservanceScale.Quiet };
        culture.State.extensions.observances.observances.Add(host);
        string before = Snapshot(culture);
        var preview = culture.PreviewPerformance(party, "evening-song", PerformanceIntent.Celebration, host.id);
        Assert.IsTrue(preview.ok, preview.reason);
        Assert.AreEqual(before, Snapshot(culture));
        Assert.IsTrue(culture.PlanPerformance(party, "evening-song", PerformanceIntent.Celebration, host.id).succeeded);
        int count = culture.PerformancesCompleted;
        Assert.IsNull(culture.CompletePerformance(party, capital, new[] { a, b }), "a festival cannot complete an observance booking");
        var saved = Snapshot(culture);
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore(); culture.AfterRestore();
        Assert.AreEqual(host.id, culture.PerformanceOf(party.id).observance);
        host = culture.State.extensions.observances.Get(host.id);
        long date = culture.ObservanceOf(host.id).nextDate;
        Assert.IsNull(culture.CompletePerformance(party, capital, new[] { a, b }, host.id), "a calendar booking cannot complete early");
        var onDay = ObservanceCalendar.Parts(date);
        var clock = TimeSystemLogic.Instance;
        typeof(TimeSystemLogic).GetProperty("CurrentCycle").SetValue(clock, onDay.cycle);
        typeof(TimeSystemLogic).GetProperty("CurrentEcho").SetValue(clock, onDay.echo);
        typeof(TimeSystemLogic).GetProperty("CurrentPhase").SetValue(clock, onDay.phase);
        typeof(TimeSystemLogic).GetProperty("CurrentSeventh").SetValue(clock, onDay.seventh);
        var decide = typeof(CultureSystem).GetMethod("DecideOccasion", Private);
        decide.Invoke(culture, new object[] { host, date, date, true });
        Assert.AreEqual(count + 1, culture.PerformancesCompleted);
        var occurrence = culture.PendingOccurrences().Last(o => o.kind == CulturalOccurrenceKind.Performance && o.source?.id == host.id);
        CollectionAssert.AreEquivalent(new[] { a, b }, occurrence.actors);
        Assert.IsNull(culture.CompletePerformance(party, capital, new[] { a, b }, host.id));
        string stateBefore = Snapshot(culture);
        var atlas = CultureAtlasReadModel.Capture(culture);
        Assert.AreEqual(stateBefore, Snapshot(culture), "atlas reads never alter rewards or history");
        Assert.IsTrue(atlas.nodes.ContainsKey("Observance:" + host.id));
        Assert.IsTrue(atlas.nodes.Values.Any(n => n.category == "Performance"));
        // A new near-term occasion can be cancelled by departure, with no additional performance reward.
        var tomorrow = ObservanceCalendar.Parts(date + 1);
        host.seventh = tomorrow.seventh; host.phase = tomorrow.phase;
        Assert.IsTrue(culture.PlanPerformance(party, "evening-song", PerformanceIntent.Celebration, host.id).succeeded);
        party.path.Add(capital.coord);
        culture.Tick();
        party.path.Clear();
        Assert.IsNull(culture.PerformanceOf(party.id));
        Assert.AreEqual(count + 1, culture.PerformancesCompleted);
    }

    private static void CheckText(CultureSystem culture, WorldSystem world, WorldUnit party)
    {
        string header = null;
        var rows = new List<string>();
        PerformancePanel.Select(party.id);
        PerformancePanel.Fill(culture, h => header = h, (label, why, tip, act) => rows.Add(label + " | " + tip));
        StringAssert.Contains(party.name, header);
        Assert.IsTrue(rows.Any(r => r.Contains("Polyphonic Chorus") && r.Contains("not now")), "a locked piece says so");
        Assert.IsTrue(rows.Any(r => r.Contains("The Evening Song for reassurance") && r.Contains("Readiness")), "each choice explains itself");
        PerformancePanel.Reset();
        rows.Clear();
        PerformancePanel.Fill(culture, h => header = h, (label, why, tip, act) => rows.Add(label));
        Assert.IsTrue(rows.Any(r => r.Contains("Echo: The Evening Song")));
        var body = new System.Text.StringBuilder();
        PerformancePanel.Describe(culture, body);
        StringAssert.Contains("Performances", body.ToString());
        var card = new System.Text.StringBuilder();
        var actions = new List<(string label, string why, System.Action call)>();
        LocalCultureCard.Party(world, party, card, actions);
        StringAssert.Contains("Performance", card.ToString());
        Assert.IsTrue(actions.Any(x => x.label.StartsWith("Plan a performance")));
    }
}
