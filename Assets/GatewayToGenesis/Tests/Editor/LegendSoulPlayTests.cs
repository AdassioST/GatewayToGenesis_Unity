using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Legends' souls in the real scene (ClickerScreen): the legends met at the start have a Soul Leitmotif and three
/// traits from the vault's note; a seated legend cracks and spirals as the people die (its council bonuses dim and the
/// capital raises an issue); rest heals it into a Motif Awakening (news, an Ornament, renown); its soul survives the
/// save codec's round trip; and a legend whose Soul Leitmotif reaches Surrender leaves the council and the roster.
/// Seventh ticks are driven directly so the test does not wait on the clock. Helpers are static: locals captured
/// before Enter Play Mode are lost to the domain reload.
/// </summary>
public class LegendSoulPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator ALegendCracksHealsIntoAMotifAwakeningAndIsLostAtSurrender()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        DismissSaveMenu();

        string name = CheckStartingSouls();
        int seat = Seat(name);
        yield return null;

        // The people die while the legend sits on the council: grief takes it to Spiraling.
        Spiral(name);
        yield return null;
        CheckSpiraling(name);

        // It stays on the council long enough for the wound to be real, then rests and heals.
        for (int i = 0; i < LegendLore.ComposureTuning.woundSevenths; i++) Seventh();
        Assert.IsTrue(GovernmentLogic.Instance.RemoveLegendFromSeat(seat, bypassCooldown: true), "the legend could not step down");
        for (int i = 0; i < 200 && LegendProgress.Instance.Composure(name) > ComposureState.Clouded; i++) Seventh();
        yield return null;
        CheckAwakening(name);
        CheckSaveRoundTrip(name);

        // Back on the council at the edge of Surrender, one more loss breaks it.
        Seat(name);
        yield return null;
        Break(name);
        yield return null;
        CheckLost(name);
    }

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && GovernmentLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        AgeProgression.Instance != null && AgeProgression.Instance.Current != null && Object.FindAnyObjectByType<NotificationFeed>() != null;

    private static void DismissSaveMenu()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
        Assert.IsFalse(SaveMenu.BlocksGameplay, "the save menu still holds the game");
    }

    private static void Seventh() => typeof(LegendProgress).GetMethod("OnSeventh", Private).Invoke(LegendProgress.Instance, new object[] { 0 });

    private static string CheckStartingSouls()
    {
        var legends = LegendProgress.Instance;
        Assert.IsFalse(LegendLore.Traits.IsEmpty, "Resources/Legends/Legend Trait.md did not load");
        foreach (var met in legends.RecruitedNames)
        {
            var soul = legends.Soul(met);
            Assert.IsNotNull(soul, $"{met} has no soul");
            Assert.IsNotNull(MagicBindings.Canonical(soul.leitmotif), $"{met}: no primary binding");
            Assert.AreEqual(LegendSoulRules.ExpressionCount, soul.expression.Count, $"{met}: not three personality traits");
            Assert.AreEqual(ComposureState.Clouded, legends.Composure(met), $"{met} does not start Clouded");
        }
        return legends.RecruitedNames.First();
    }

    private static int Seat(string name)
    {
        var government = GovernmentLogic.Instance;
        var legend = GameCatalog.Legends.Get(name, nameof(LegendSoulPlayTests));
        var current = government.GetSeatWithLegend(name);
        if (current != null) return current.seatIndex;
        var seats = Enumerable.Range(0, GovernmentLogic.RegularSeatCount).Append(GovernmentLogic.HeadOfStateIndex).ToList();
        int index = seats.FirstOrDefault(i => government.CanAssignLegendToSeat(legend, i));
        if (!government.CanAssignLegendToSeat(legend, index)) government.UnlockNextCouncilSeat();
        index = seats.First(i => government.CanAssignLegendToSeat(legend, i));
        Assert.IsTrue(government.AssignLegendToSeat(legend, index, bypassCooldown: true), $"{name} could not be seated");
        return index;
    }

    // Everyone mourns what they have already seen; then a fifth of the people die in one Seventh, twice: the first
    // loss cracks a strained legend, the second sends it Spiraling.
    private static void Spiral(string name)
    {
        var tuning = LegendLore.ComposureTuning;
        Mourn(name, tuning.fracturedAt - 4f);
        Assert.AreEqual(ComposureState.Fractured, LegendProgress.Instance.Composure(name), "the first loss did not crack the legend");
        Mourn(name, tuning.spiralingAt - 4f);
    }

    private static void Mourn(string name, float strain)
    {
        var pop = PopGrowthLogic.Instance;
        pop.ModifyVagrants(60);
        Seventh();
        LegendProgress.Instance.Soul(name).strain = strain;
        pop.ModifyVagrants(-Mathf.Max(1, (pop.population + pop.vagrants) / 5));
        Seventh();
    }

    private static void CheckSpiraling(string name)
    {
        var legends = LegendProgress.Instance;
        var tuning = LegendLore.ComposureTuning;
        Assert.AreEqual(ComposureState.Spiraling, legends.Composure(name), $"grief did not take {name} to Spiraling (strain {legends.Soul(name).strain})");
        Assert.AreEqual(legends.Growth(name) * tuning.spiralingCouncil, legends.CouncilMultiplier(name), 1e-4, "a Spiraling legend's council bonuses dim");
        var issues = (IEnumerable)typeof(NotificationFeed).GetMethod("Issues", Private).Invoke(Object.FindAnyObjectByType<NotificationFeed>(), null);
        Assert.IsTrue(Titles(issues).Any(t => t == $"{name} is Spiraling"), "the capital raised no issue for a Spiraling legend at work");

        var legend = GameCatalog.Legends.Get(name, nameof(LegendSoulPlayTests));
        var data = new TooltipData();
        Assert.IsTrue(TooltipContent.Legend(legend, data));
        StringAssert.Contains("[[Soul Leitmotif]]", data.summary);
        StringAssert.Contains("Spiraling", data.summary);
        StringAssert.Contains("The Expression", data.details);
        if (legend.bonuses != null && legend.bonuses.Count > 0)
            StringAssert.Contains($"x{tuning.spiralingCouncil:0.##} while Spiraling", data.effects, "the council bonuses say they are dimmed");
        Debug.Log($"[LegendSoulPlayTests] {name}'s tooltip while Spiraling:\nSUMMARY\n{data.summary}\nEFFECTS\n{data.effects}\nDETAILS\n{data.details}");
    }

    private static void CheckAwakening(string name)
    {
        var legends = LegendProgress.Instance;
        var soul = legends.Soul(name);
        Assert.AreEqual(ComposureState.Clouded, legends.Composure(name), "rest did not heal the legend");
        Assert.AreEqual(1, soul.ornaments.Count, "healing a real wound brings a Motif Awakening");
        Assert.AreEqual(1, soul.awakenings.Count);
        Assert.IsTrue(legends.Deeds(name).Any(d => d.StartsWith("Motif Awakening to ")), "the awakening is a deed");
        var news = (IEnumerable)typeof(NotificationFeed).GetField("_news", Private).GetValue(Object.FindAnyObjectByType<NotificationFeed>());
        Assert.IsTrue(Titles(news).Any(t => t == $"Motif Awakening: {name}"), "no news of the awakening");
        Assert.IsTrue(Titles(news).Any(t => t == $"{name}'s Soul Leitmotif is Fractured"), "no news of the crack");
    }

    // The soul is saved inside LegendProgress's records: capture, JSON, spoil it, restore.
    private static void CheckSaveRoundTrip(string name)
    {
        var legends = LegendProgress.Instance;
        var soul = legends.Soul(name);
        string ornament = soul.ornaments[0];
        float strain = soul.strain;
        var expression = soul.expression.ToList();
        var json = JsonUtility.ToJson(SaveStateCodec.Capture(legends, "_recruited", "_started"));
        soul.ornaments.Clear();
        soul.strain = 0f;
        SaveStateCodec.Restore(legends, JsonUtility.FromJson<StateNode>(json), "_recruited", "_started");
        var restored = legends.Soul(name);
        CollectionAssert.AreEqual(new[] { ornament }, restored.ornaments);
        Assert.AreEqual(strain, restored.strain, 1e-4);
        CollectionAssert.AreEqual(expression, restored.expression);
    }

    // At the edge of Surrender, one more loss breaks the Soul Leitmotif.
    private static void Break(string name) => Mourn(name, LegendLore.ComposureTuning.surrenderAt - 1f);

    private static void CheckLost(string name)
    {
        var legends = LegendProgress.Instance;
        Assert.IsTrue(legends.IsLost(name), $"{name} did not reach Surrender (strain {legends.Soul(name).strain})");
        Assert.IsFalse(legends.IsRecruited(name));
        Assert.IsNull(GovernmentLogic.Instance.GetSeatWithLegend(name), "a lost legend still sits on the council");
        CollectionAssert.DoesNotContain(LegendLeaderLogic.Instance.GetAvailableLegends().Select(l => l.legendName).ToList(), name, "a lost legend is still offered");
        Assert.IsFalse(legends.Recruit(GameCatalog.Legends.Get(name, nameof(LegendSoulPlayTests)), "met again"), "a lost legend is never met again");
        var news = (IEnumerable)typeof(NotificationFeed).GetField("_news", Private).GetValue(Object.FindAnyObjectByType<NotificationFeed>());
        Assert.IsTrue(Titles(news).Any(t => t == $"{name} is lost to Dissonance"), "no news of the loss");
    }

    private static List<string> Titles(IEnumerable notices)
    {
        var titles = new List<string>();
        foreach (var notice in notices) titles.Add((string)notice.GetType().GetField("title").GetValue(notice));
        return titles;
    }
}
