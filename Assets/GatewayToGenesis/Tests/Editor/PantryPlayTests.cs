using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The food stores and the council's growth in the real scene: the survivors start with a cellar of dried peaches;
/// when Food falls the stores feed the shortfall (so no one starves while they last); the council opens only the Head of
/// State, and The Rekindling and Call and Response each open one more position.
/// </summary>
public class PantryPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const string Shortfall = "Pantry test shortfall";

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    [UnityTest]
    public IEnumerator StoresStartStockedCoverAFallingFoodAndTechnologiesOpenCouncilSeats()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (Pantry.Instance == null || GameUnitsLogic.Instance == null || GovernmentLogic.Instance == null || GlobalProductionManager.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var pantry = Pantry.Instance;
        Assert.IsNotNull(pantry.Settings, "Resources/Food/Pantry is loaded");
        Assert.AreEqual(40f, GameUnitsLogic.Instance.GetResourceAmountExact("Dried Auric Peaches"), 1f, "the survivors' cellars are full of dried peaches");
        Assert.Greater(pantry.Value, 30f);

        // Food falls: the stores feed in the shortfall.
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        GlobalProductionManager.Instance.SetFlatRate(food, Shortfall, -2f);
        float before = pantry.Value;
        for (Wait(2.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;
        Assert.Greater(pantry.CoverRate, 1.5f, "the stores cover the shortfall");
        Assert.GreaterOrEqual(GlobalProductionManager.Instance.GetNetProductionRate(food), -0.05f, "so Food no longer falls");
        Assert.Less(pantry.Value, before - 1f, "and the stores are drawn on");
        GlobalProductionManager.Instance.SetFlatRate(food, Shortfall, 0f);
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;
        Assert.AreEqual(0f, pantry.CoverRate, 1e-4f, "Food steady: nothing is drawn");

        // The council: the Head of State alone, then one position per chokepoint technology.
        var government = GovernmentLogic.Instance;
        Assert.AreEqual(0, government.GetUnlockedSeatCount(), "only the Head of State at the start");
        foreach (var technology in new[] { "The Rekindling", "Call and Response" })
        {
            var slot = GameUnitsLogic.Instance.GetTechnologySlot(technology);
            Assert.IsNotNull(slot, technology + " is in the tree");
            slot.UnlockTechnology();
            yield return null;
        }
        Assert.AreEqual(2, government.GetUnlockedSeatCount(), "two positions open by the end of Act I: three on the council");
        yield return new ExitPlayMode();
    }
}
