using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Ballad actors in the real scene (ClickerScreen): the caravan at the gates asks for defense, so the High Arbiter
/// (defense is its second charge; the Supreme Commander's seat is not open yet) plays it; the faces show beside the
/// screens; the player hands the lead to the Head of State, who earns the story's fragment consequence and a little of
/// its theme. Then the Ruin-Song: its first verse is played by the party that found it, the next verses carry the same
/// actors, and the finale pays the ballad's theme (Scholar: Lucidity x5, Rebirth x3; a share to the co-protagonist).
/// Stories are completed directly (their screens are not clicked through), so only their own tag consequences apply.
/// Helpers are static: locals captured before Enter Play Mode are lost to the domain reload.
/// </summary>
public class BalladActorPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static string _arbiter, _head;

    [UnityTest]
    public IEnumerator EventsHaveFacesAndBalladsPayTheirTheme()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        DismissSaveMenu();

        SeatTheCouncil();
        yield return null;
        StartTheCaravan();
        for (int i = 0; i < 5; i++) yield return null;
        CheckTheFaces();
        HandTheLeadToTheHeadOfState();
        yield return null;
        CheckTheFaces();
        CompleteTheCaravan();

        SingTheRuinSong();
        yield return null;
    }

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && GovernmentLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        EventSystemLogic.Instance != null && EventVolumeManager.Instance != null && EventVolumeManager.Instance.FindStoryNode("hollow_caravan") != null;

    private static void DismissSaveMenu()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
    }

    // A High Arbiter and a Head of State, meeting legends until someone fits each.
    private static void SeatTheCouncil()
    {
        var government = GovernmentLogic.Instance;
        var legends = LegendProgress.Instance;
        // Positions open as the game goes on; the High Arbiter's is the first.
        for (int i = 0; i < 3 && government.GetSeatPosition("High Arbiter") < 0; i++) government.UnlockNextCouncilSeat();
        int arbiter = government.GetSeatPosition("High Arbiter");
        Assert.GreaterOrEqual(arbiter, 0, "the High Arbiter's seat opens first");
        CollectionAssert.Contains(government.GetCouncilSeat(arbiter).areas, "defense", "the High Arbiter answers for defense");
        Assert.Less(government.GetSeatPosition("Supreme Commander"), 0, "the Supreme Commander's seat waits in the pool");

        LegendData forSeat = null, forHead = null;
        for (int i = 0; i < 20 && (forSeat == null || forHead == null); i++)
        {
            forSeat = government.GetAvailableLegendsForSeat(arbiter).FirstOrDefault();
            forHead = government.GetAllAvailableLegends().FirstOrDefault(l => l.canBeHeadOfState && l != forSeat);
            if (forSeat == null || forHead == null) Assert.IsNotNull(legends.RecruitNext("Met for the test"), "not enough legends for a High Arbiter and a Head of State");
        }
        Assert.IsTrue(government.AssignLegendToSeat(forSeat, arbiter, bypassCooldown: true));
        Assert.IsTrue(government.AssignLegendToSeat(forHead, GovernmentLogic.HeadOfStateIndex, bypassCooldown: true));
        _arbiter = forSeat.legendName;
        _head = forHead.legendName;
        Assert.AreEqual(_head, government.HeadOfStateLegend);
        var answer = government.AnswerFor("defense");
        Assert.AreEqual(_arbiter, answer.legend);
        Assert.AreEqual(0, answer.distance);
    }

    private static void StartTheCaravan()
    {
        var events = EventSystemLogic.Instance;
        events.TriggerSpecificEvent(EventVolumeManager.Instance.FindStoryNode("hollow_caravan"), "test");
        Assert.IsTrue(events.isEventActive);
        Assert.IsNotNull(events.CurrentCast);
        Assert.AreEqual(_arbiter, events.CurrentCast.protagonist, "a caravan at the gates is the defense's business");
        StringAssert.Contains("High Arbiter", events.CurrentCast.how);
        StringAssert.Contains(_arbiter, EventText.DescribeConsequence(new EventConsequence { type = EventConsequence.ConsequenceType.FragmentChange, targetName = "protagonist Vision", value = 2 }),
            "consequences name the legend behind the role");
    }

    private static void CheckTheFaces()
    {
        var view = Object.FindAnyObjectByType<BalladActorsView>();
        Assert.IsNotNull(view, "GenesisLoop adds the ballad actors view");
        var faces = Object.FindObjectsByType<LegendFace>().Where(f => f.isActiveAndEnabled).ToList();
        Assert.AreEqual(1, faces.Count, "one face: the protagonist");
        Assert.AreEqual(EventSystemLogic.Instance.CurrentCast.protagonist, faces[0].legend.legendName);
    }

    private static void HandTheLeadToTheHeadOfState()
    {
        var events = EventSystemLogic.Instance;
        CollectionAssert.Contains(events.CastCandidates(), _head, "the council is offered");
        Assert.IsTrue(events.ChooseProtagonist(_head));
        Assert.AreEqual(_head, events.CurrentCast.protagonist);
    }

    // Nothing here is against the odds, so an Underdog earns the same.
    private static void CompleteTheCaravan()
    {
        var events = EventSystemLogic.Instance;
        var legends = LegendProgress.Instance;
        int vision = legends.Fragments(_head, FragmentKind.Vision), defiance = legends.Fragments(_head, FragmentKind.Defiance);
        int arbiterVision = legends.Fragments(_arbiter, FragmentKind.Vision);
        events.AddConsequence(new EventConsequence { type = EventConsequence.ConsequenceType.FragmentChange, targetName = "protagonist Vision", value = 4 });
        events.OnStoryCompleted();
        Assert.IsNull(events.CurrentCast, "the cast leaves the stage with the story");
        Assert.AreEqual(vision + 4, legends.Fragments(_head, FragmentKind.Vision), "fragment:protagonist pays whoever plays the lead");
        Assert.AreEqual(defiance + 1, legends.Fragments(_head, FragmentKind.Defiance), "the caravan's theme (Defiance) pays its actors a little");
        Assert.AreEqual(arbiterVision, legends.Fragments(_arbiter, FragmentKind.Vision), "the replaced legend earns nothing");
    }

    private static void SingTheRuinSong()
    {
        var events = EventSystemLogic.Instance;
        var legends = LegendProgress.Instance;
        int lucidity = legends.Fragments(_arbiter, FragmentKind.Lucidity), rebirth = legends.Fragments(_arbiter, FragmentKind.Rebirth);
        int coLucidity = legends.Fragments(_head, FragmentKind.Lucidity), coRebirth = legends.Fragments(_head, FragmentKind.Rebirth);

        // An expedition directed by the Arbiter, the Head of State walking with it, found the first verse.
        events.Summon("ballad_ruin_song_1", _arbiter, new[] { _head });
        for (int verse = 1; verse <= 3; verse++)
        {
            events.TriggerSpecificEvent(EventVolumeManager.Instance.FindStoryNode("ballad_ruin_song_" + verse), "test");
            Assert.AreEqual(_arbiter, events.CurrentCast.protagonist, $"verse {verse}: the Director leads, and the ballad keeps its actors");
            CollectionAssert.AreEqual(new[] { _head }, events.CurrentCast.coProtagonists, $"verse {verse}");
            events.OnStoryCompleted();
            Assert.AreEqual(verse, events.BalladVersesTold("ruin_song"));
        }
        Assert.IsTrue(events.IsBalladComplete("ruin_song"));
        Assert.IsTrue(GameValues.TryGet("ballad", "ruin_song", out float sung) && sung == 1f, "the ballad condition reads it");
        // Three themed verses (Scholar: 1 Lucidity each) and the finale (Lucidity x5, Rebirth x3; the co-protagonist 3 and 2).
        Assert.AreEqual(lucidity + 1 * 3 + 5, legends.Fragments(_arbiter, FragmentKind.Lucidity));
        Assert.AreEqual(rebirth + 3, legends.Fragments(_arbiter, FragmentKind.Rebirth));
        Assert.AreEqual(coLucidity + 1 * 3 + 3, legends.Fragments(_head, FragmentKind.Lucidity));
        Assert.AreEqual(coRebirth + 2, legends.Fragments(_head, FragmentKind.Rebirth));
    }
}
