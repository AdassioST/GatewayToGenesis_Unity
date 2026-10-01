using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The technology tree in the real scene (ClickerScreen in Play Mode), what the pure <see cref="TechTreeTests"/> cannot
/// see: hidden technologies show nothing and take no pointer, the lines are drawn, a click on a locked technology plans
/// its way through its prerequisites and research starts on the first step, research moves along the plan, a right
/// click takes a technology and its dependants out of it, and a met Enlightenment goal enlightens its technology
/// (uncovered before its way, part of its research paid, Era Score for an Event Technology and none for an ordinary one).
/// </summary>
public class TechTreePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    [UnityTest]
    public IEnumerator TheTreeUncoversPlansAndEnlightens()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (GameUnitsLogic.Instance == null || GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger") == null
            || AgeProgression.Instance == null || AgeProgression.Instance.Current == null); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        // The save menu opens at the front door and holds the game while it is up; this test lives past it.
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        yield return null;

        var units = GameUnitsLogic.Instance;
        Assert.IsNotNull(units, "no GameUnitsLogic in the scene");
        var reconstruction = units.GetTechnologySlot("Reconstruction");
        var felling = units.GetTechnologySlot("Elderwood Felling");
        var hymns = units.GetTechnologySlot("Harvest Hymns");
        var horology = units.GetTechnologySlot("Horology");
        Assert.IsNotNull(reconstruction, "the tree was not built");
        Assert.IsNotNull(horology);

        // At the start: Reconstruction can be researched, the next step shows locked, the rest is hidden, pointer included.
        Assert.AreEqual(GameTechnologySlot.TechnologyState.CurrentResearchOption, reconstruction.techState);
        Assert.IsTrue(felling.isVisible, "one step past Reconstruction shows");
        Assert.AreEqual(GameTechnologySlot.TechnologyState.NextResearchOption, felling.techState);
        Assert.IsFalse(hymns.isVisible, "two steps away stays hidden");
        Assert.IsFalse(hymns.displayComponent.activeSelf);
        Assert.IsFalse(hymns.GetComponent<Image>().raycastTarget, "a hidden technology takes no pointer");
        Assert.IsFalse(hymns.GetComponent<Button>().interactable, "a hidden technology cannot be clicked");
        Assert.AreEqual(40, reconstruction.technologyTreeLogic.TreeSlots.Count, "Act I has 40 technologies");

        var tree = reconstruction.technologyTreeLogic;
        Assert.IsNotNull(tree, "the slot knows its tree");
        var lines = tree.GetComponentsInChildren<TechTreeLines>(true).FirstOrDefault(l => l.name == "Lines");
        Assert.IsNotNull(lines, "the tree has its connector layer");
        for (int i = 0; i < 3; i++) yield return null;
        Assert.Greater(lines.MarkCount, 0, "lines join Reconstruction to what it leads to");

        // Researched, Reconstruction uncovers the next step; a click on a locked technology plans its way.
        reconstruction.UnlockTechnology();
        yield return null;
        Assert.IsTrue(hymns.isVisible, "Harvest Hymns shows once one of its prerequisites can be researched");
        Assert.AreEqual(GameTechnologySlot.TechnologyState.NextResearchOption, hymns.techState);
        hymns.GetComponent<ClickLogic>().OnButtonClick();
        CollectionAssert.AreEqual(new[] { "Shared Embers", "Elderwood Felling", "Harvest Hymns" }, units.ResearchPlan.ToArray(),
            "the click planned every missing prerequisite first");
        Assert.AreEqual("Shared Embers", units.activeTechnologySlot != null ? units.activeTechnologySlot.gameUnit.name : null, "research starts on the first step");

        // Research moves along the plan.
        units.GetTechnologySlot("Shared Embers").UnlockTechnology();
        yield return null;
        Assert.AreEqual(felling, units.activeTechnologySlot, "researching the first step moves to the next");
        CollectionAssert.AreEqual(new[] { "Elderwood Felling", "Harvest Hymns" }, units.ResearchPlan.ToArray());

        // A right click on Harvest Hymns takes it out of the plan.
        units.RemoveFromPlan(hymns);
        CollectionAssert.AreEqual(new[] { "Elderwood Felling" }, units.ResearchPlan.ToArray());
        Assert.AreEqual(felling, units.activeTechnologySlot, "research under way carries on");

        // An old name still finds a renamed technology (saves and stories written before the rework).
        Assert.AreEqual(felling, units.GetTechnologySlot("Woodcraft Mastery"));

        // Enlightenment: Horology's goal is the first birth. Once a child is born, Horology is enlightened within a check or two.
        Assert.IsFalse(horology.isVisible, "Horology is hidden before its goal is met");
        Assert.IsTrue(horology.IsEventTechnology, "Horology is an Event Technology");
        var ages = AgeProgression.Instance;
        int eraBefore = ages.EraScore;
        typeof(PopGrowthLogic).GetField("_births", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(PopGrowthLogic.Instance, 1);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.IsTrue(horology.enlightenedCompleted, "a met Enlightenment goal enlightens its technology");
        Assert.IsTrue(horology.isVisible, "an enlightened technology shows before its way");
        Assert.IsTrue(units.technologyProgress.TryGetValue(horology, out var paid) && paid.Values.Sum() > 0f, "part of its research is paid at once");
        Assert.AreEqual(1f, GameValues.Get("enlightened", "Horology"), "the condition domain sees it");
        Assert.IsFalse(horology.isUnlocked, "its way is not researched, so it waits");
        Assert.AreEqual(eraBefore + ages.Current.enlightenedTechnologyEraScore, ages.EraScore, "an Event Technology's Enlightenment earns Era Score");
        Assert.AreEqual(1, ages.Current.enlightenedTechnologyEraScore, "the owner's rule: 1");

        // An ordinary technology's Enlightenment earns no Era Score.
        var renewal = units.GetTechnologySlot("Earth-Bean Rows");
        Assert.IsFalse(renewal.IsEventTechnology || renewal.IsCrisisTechnology);
        Assert.IsTrue(units.EnlightenTechnology(renewal, "a test"));
        Assert.AreEqual(eraBefore + 1, ages.EraScore, "only Event and Crisis Technologies earn Era Score when enlightened");

        yield return new ExitPlayMode();
    }
}
