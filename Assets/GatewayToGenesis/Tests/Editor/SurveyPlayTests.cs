using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Surveys in the real scene (ClickerScreen): an expedition passing by explores a cell once it has seen all seven of
/// its hexes; one sent to survey a cell walks to each of its hexes in turn and surveys it there, and the cell is
/// surveyed hex by hex; a move within the cell keeps the survey going, any other order sets it aside with its progress kept. The simulation is stepped directly (WorldSystem.StepUnits), so
/// the test does not wait on the clock. Helpers are static: locals captured before Enter Play Mode are lost to the
/// domain reload.
/// </summary>
public class SurveyPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator PassingPartiesExploreWhatTheySeeAndASurveyWalksEveryHex()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;

        OpenTheMap();
        yield return null;
        CheckPassing();
        CheckSurvey();
        yield return null;
        CheckAnotherOrderEndsIt();
        yield return new ExitPlayMode();
    }

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        WorldSystem.Instance != null && WorldSystem.Instance.Map != null && GameUnitsLogic.Instance != null && AgeProgression.Instance != null && AgeProgression.Instance.Current != null;

    private static void OpenTheMap()
    {
        var world = WorldSystem.Instance;
        var slot = GameUnitsLogic.Instance.GetTechnologySlot(world.Settings.mapTechnology);
        Assert.IsNotNull(slot, "the map's technology is not in the tree");
        if (!GameUnitsLogic.Instance.IsTechnologyUnlocked(world.Settings.mapTechnology)) slot.UnlockTechnology();
        Assert.IsNotNull(world.ExpeditionUnits.FirstOrDefault(), "the first expedition sets out free");
    }

    private static WorldUnit Party() => WorldSystem.Instance.ExpeditionUnits.First();

    // Land near the Capital, walkable, not yet explored and with every hex open to walk.
    private static WorldTile Unexplored(int distance, WorldTile except = null)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var grid = MicroNavigation.Grid(map, world.Settings.generation);
        return map.Tiles.Where(t => t != except && !t.water && !t.impassable && !t.explored && t.settlement < 0 && t.microBlockedMask == 0
                && HexCoord.Distance(t.coord, map.Capital) == distance
                && Enumerable.Range(0, MicroNavigation.PerCell).All(k => !float.IsPositiveInfinity(grid.Enter(t.index * MicroNavigation.PerCell + k)))
                && world.CanReach(Party(), t.coord, false))
            .OrderBy(t => t.index).FirstOrDefault();
    }

    // A party that sees all seven hexes of a cell explores it in passing.
    private static void CheckPassing()
    {
        var world = WorldSystem.Instance;
        var unit = Party();
        var cell = Unexplored(2);
        Assert.IsNotNull(cell, "some unexplored land near the Capital");
        var look = typeof(WorldSystem).GetMethod("Look", Private);
        look.Invoke(world, new object[] { unit, world.SpecOf(unit), new List<string>(), (HexCoord?)MicroNavigation.Center(cell.coord) });
        if (world.SpecOf(unit).knowRadius >= 1)
        {
            Assert.IsTrue(WorldMap.FullySeen(cell), "from the heart of a cell a party sees all of it");
            Assert.IsTrue(cell.explored, "a cell seen hex by hex is explored in passing");
            Assert.AreEqual(0, WorldMap.SurveyedHexes(cell), "but not surveyed");
            Assert.Greater(world.HexesToSurvey(cell), 0, "a survey still has its hexes to walk");
        }
    }

    // Sent to survey a cell, the party walks each hex and surveys it there.
    private static void CheckSurvey()
    {
        var world = WorldSystem.Instance;
        var unit = Party();
        var cell = Unexplored(1);
        Assert.IsNotNull(cell, "some unexplored land beside the Capital");
        Assert.IsNull(world.WhyNotSurveyCell(unit, cell.coord), world.WhyNotSurveyCell(unit, cell.coord));
        Assert.IsTrue(world.SurveyCell(unit, cell.coord));
        Assert.IsTrue(unit.surveying);
        Assert.AreSame(unit, world.SurveyorOf(cell));
        Assert.Greater(world.SurveySevenths(unit, cell.coord), 0f);

        var step = typeof(WorldSystem).GetMethod("StepUnits", Private);
        var visited = new HashSet<int>();
        for (int i = 0; i < 60 * 60 && unit.surveying; i++)
        {
            unit.supplies = world.SpecOf(unit).supplyCapacity;
            unit.fatigue = 0f;
            step.Invoke(world, new object[] { 1f / 60f });
            var at = WorldUnits.MicroPosition(unit);
            if (HexHierarchy.Parent(at) == cell.coord && unit.Working) visited.Add(MicroNavigation.Index(world.Map, at));
        }
        Assert.IsFalse(unit.surveying, "the survey ends once every hex is surveyed");
        Assert.AreEqual(0, world.HexesToSurvey(cell), "every hex surveyed");
        Assert.AreEqual(WorldMap.OpenHexes(cell), WorldMap.SurveyedHexes(cell));
        Assert.AreEqual(WorldMap.OpenHexes(cell), visited.Count, "it stood on each hex to survey it");
        Assert.IsTrue(cell.explored);
        Assert.AreEqual("Already surveyed, hex by hex.", world.WhyNotSurveyCell(unit, cell.coord));
    }

    // A move within the cell keeps a survey going; another order sets it aside with its progress kept (work on the hex
    // under way included); it is taken up again where it stopped; stopping it ends it.
    private static void CheckAnotherOrderEndsIt()
    {
        var world = WorldSystem.Instance;
        var unit = Party();
        var cell = Unexplored(2);
        Assert.IsNotNull(cell);
        Assert.IsTrue(world.SurveyCell(unit, cell.coord));
        var step = typeof(WorldSystem).GetMethod("StepUnits", Private);
        // Walk it into the cell and let it work part of a hex.
        for (int i = 0; i < 60 * 60 && !(unit.Working && unit.coord == cell.coord && WorldUnits.WorkProgress(unit) > 0.3f); i++)
        {
            unit.supplies = world.SpecOf(unit).supplyCapacity;
            unit.fatigue = 0f;
            step.Invoke(world, new object[] { 1f / 60f });
        }
        Assert.IsTrue(unit.Working && unit.surveying, "it is at work on a hex of the cell");
        float before = world.SurveyProgress(unit);
        Assert.Greater(before, 0f);

        // A move to another hex of the same cell: still surveying.
        var other = HexHierarchy.Children(cell.coord).First(c => c != WorldUnits.MicroPosition(unit) && MicroNavigation.Enterable(world.Map, world.Settings.generation, c));
        Assert.IsTrue(world.GoMicro(unit, other));
        Assert.IsTrue(unit.surveying, "a move within the cell keeps the survey going");

        // Halting sets it aside; nothing done is lost.
        world.Halt(unit);
        Assert.IsFalse(unit.surveying, "halting sets the survey aside");
        Assert.IsTrue(unit.surveyPaused);
        Assert.IsNull(world.SurveyorOf(cell), "a survey set aside does not hold the cell");
        Assert.AreEqual(before, world.SurveyProgress(unit), 0.02f, "its progress is kept");
        Assert.IsNull(world.WhyNotResumeSurvey(unit), world.WhyNotResumeSurvey(unit));
        Assert.IsTrue(world.ResumeSurvey(unit));
        Assert.IsTrue(unit.surveying && !unit.surveyPaused, "taken up again");

        world.StopSurvey(unit);
        Assert.IsFalse(unit.surveying || unit.surveyPaused, "stopping ends the survey");
        Assert.IsNull(world.SurveyorOf(cell));
    }
}
