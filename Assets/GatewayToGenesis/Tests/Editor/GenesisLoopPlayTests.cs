using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The first playable loop in the real scene: open ClickerScreen, enter Play Mode, and live through the Age of
/// Desolation (its Acts of Fate and The Inescapable Hunger) into the Age of Renewal, telling every story the Age
/// offers the moment it waits. Checks what the pure tests cannot: the systems find each other in the scene, the
/// stories exist and complete, the famine kills once and never below the survivor floor, the Age changes once, the
/// world gains the new Age's features and the achievement is awarded.
/// </summary>
public class GenesisLoopPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    [UnityTest]
    public IEnumerator TheAgeOfDesolationPassesIntoTheAgeOfRenewal()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        // The scene's systems wake over the first frames (longer after a domain reload).
        for (int i = 0; i < 600 && (PopGrowthLogic.Instance == null || AgeProgression.Instance == null || AgeProgression.Instance.Current == null || WorldSystem.Instance == null || EventVolumeManager.Instance == null); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        // The save menu opens at the front door and holds the Ages while it is up; this test lives past it.
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        Assert.IsFalse(SaveMenu.BlocksGameplay, "the save menu still holds the game");

        var ages = AgeProgression.Instance;
        var world = WorldSystem.Instance;
        var legends = LegendProgress.Instance;
        var events = EventSystemLogic.Instance;
        var volumes = EventVolumeManager.Instance;
        var pop = PopGrowthLogic.Instance;
        Assert.IsNotNull(ages, "The Genesis Loop did not create the Ages");
        Assert.IsNotNull(world, "The Genesis Loop did not create the world");
        Assert.IsNotNull(world.Map, "No world was generated (Resources/World)");
        Assert.IsNotNull(legends);
        Assert.IsNotNull(events);
        Assert.IsNotNull(volumes);
        Assert.IsNotNull(pop);
        Assert.AreEqual("age-of-desolation", ages.Current.id);
        Assert.AreEqual(GameAge.FirstAge, GameAge.Id);
        Assert.AreEqual(2, legends.RecruitedCount, "two legends are known at the start");
        Assert.AreEqual(0, world.Map.Tiles.Count(t => t.featureAge == 1), "no Age I features before Age I");

        // Give the famine people to take.
        pop.ModifyVagrants(60);
        for (int i = 0; i < 5; i++) yield return null;

        var told = new System.Collections.Generic.List<string>();
        int guard = 0;
        while (ages.History.Count == 0 && guard++ < 2000)
        {
            if (ages.WaitingStory != null && !events.IsEventActive())
            {
                string knot = ages.WaitingStory;
                var node = volumes.FindStoryNode(knot);
                Assert.IsNotNull(node, $"The Age waits for '{knot}', which is not a story");
                events.TriggerSpecificEvent(node, "GenesisLoopPlayTests");
                Assert.IsTrue(events.IsEventActive(), $"'{knot}' did not start");
                volumes.CompleteStory();
                told.Add(knot);
                yield return null;
                continue;
            }
            // The technology tree drives the Age: research the technology it waits for.
            string gate = ages.WaitingGate;
            if (gate != null)
            {
                var slot = GameUnitsLogic.Instance.GetTechnologySlot(gate);
                Assert.IsNotNull(slot, $"The Age waits for '{gate}', which is not in the technology tree");
                slot.UnlockTechnology();
                yield return null;
                continue;
            }
            ages.Advance(1);
            yield return null;
        }

        CollectionAssert.AreEqual(new[]
        {
            "desolation_golden_orchard", "desolation_brown_peach", "desolation_peach_sickness",
            "desolation_regional_failure", "desolation_ash_bread", "desolation_choosing_of_seeds",
        }, told, "every story of the Age, in order, once");

        Assert.AreEqual(1, ages.History.Count, "the Age passed once");
        var record = ages.History[0];
        Assert.AreEqual("age-of-desolation", record.ageId);
        Assert.AreEqual("age-of-renewal", record.nextAgeId, "the famine always leads to the Age of Renewal");
        Assert.Greater(record.populationBefore, 0, "the 60 vagrants count as people the famine can reach");
        Assert.Greater(record.deaths, 0, "the famine takes people");
        Assert.GreaterOrEqual(record.Survivors, System.Math.Min(record.populationBefore, 5), "the survivor floor lives");
        Assert.AreEqual("age-of-renewal", GameAge.Id);
        Assert.AreEqual(1, GameAge.Number);
        Assert.AreEqual("age-of-renewal", ages.Current.id);
        Assert.AreEqual(0, ages.Sevenths, "the new Age starts from its first seventh");
        Assert.Greater(world.Map.Tiles.Count(t => t.featureAge == 1), 0, "the new Age brings new things to the map");
        // Achievements are kept per world (the save session). This test plays without one, so the player's lifetime
        // profile is never written; the passage still sends the signal that earns it.
        if (SaveSession.Current != null) Assert.IsTrue(Achievements.Tracker.IsUnlocked("the-brown-auric-peach"), "Survive through Ages 0");
        else CollectionAssert.Contains(AchievementTriggers.Earned(AchievementEvent.Of(AchievementSignal.AgeSurvived, record.ageNumber)).ToList(), "the-brown-auric-peach", "Survive through Ages 0");
        Assert.AreEqual("renewal_dawn", ages.WaitingStory, "the new Age opens with its own story");

        yield return new ExitPlayMode();
    }
}
