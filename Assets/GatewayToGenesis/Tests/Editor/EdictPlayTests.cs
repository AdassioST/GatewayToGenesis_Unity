using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The Edicts in the real scene (<see cref="EdictSystem"/>): nothing is decreed until the council holds three seats;
/// then the realm's customs become its stances, the Government tab shows the section, a stance changes the pillars,
/// the levers (caravans) and the Realm's border policy and then stands, the Head of State seals an edict into its one
/// slot, the edict runs out and rests, and the whole state survives a save.
/// </summary>
public class EdictPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static void Wait(float seconds, out float until) => until = Time.realtimeSinceStartup + seconds;

    private static void Seventh(EdictSystem edicts) =>
        typeof(EdictSystem).GetMethod("OnSeventh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(edicts, new object[] { 1 });

    [UnityTest]
    public IEnumerator TheCouncilOfThreeEstablishesTheEdicts()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (EdictSystem.Instance == null || GovernmentLogic.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (Wait(1.5f, out float until); Time.realtimeSinceStartup < until;) yield return null;

        var edicts = EdictSystem.Instance;
        var government = GovernmentLogic.Instance;
        var world = WorldSystem.Instance;
        Assert.IsFalse(EdictSystem.IsEstablished, "only the Head of State governs at first");
        Assert.AreEqual(0f, GameValues.Get("edicts", "established"));
        Assert.AreEqual(1f, EdictSystem.Levers.births, "nothing is decreed yet");
        Assert.IsNotNull(Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(t => t.name == "Edicts Button" && t.gameObject.scene.IsValid()), "the Government tab has its Edicts section");

        // Two more seats: three with the Head of State.
        Assert.IsTrue(government.UnlockNextCouncilSeat());
        for (Wait(0.7f, out float until); Time.realtimeSinceStartup < until;) yield return null;
        Assert.IsFalse(EdictSystem.IsEstablished, "two seats are not enough");
        Assert.IsTrue(government.UnlockNextCouncilSeat());
        for (Wait(1f, out float until); Time.realtimeSinceStartup < until && !EdictSystem.IsEstablished;) yield return null;
        Assert.IsTrue(EdictSystem.IsEstablished, "the third seat establishes the edicts");
        Assert.AreEqual(1, edicts.Capacity);
        Assert.AreEqual(world.BorderPolicy, EdictSystem.Levers.borders, "the realm's border policy became The Borders stance");

        // A stance: the pillars, the levers and the story values follow; it then stands a Phase.
        int regalia = StatManager.Instance.GetPillarValue("regalia");
        Assert.IsTrue(edicts.SetStance("strangers", "sealed", out string why), why);
        yield return null;
        Assert.AreEqual(regalia + (int)EdictCatalog.PillarLean, StatManager.Instance.GetPillarValue("regalia"), "Sealed Gates pulls Regalia");
        Assert.AreEqual(0f, EdictSystem.Levers.caravans, "no caravans are drawn");
        Assert.AreEqual(1f, GameValues.Get("stance", "strangers:sealed"));
        Assert.IsFalse(edicts.SetStance("strangers", "welcome", out why));
        StringAssert.Contains("changed recently", why);

        // The Realm panel's border policy is now The Borders stance.
        Assert.IsTrue(EdictSystem.RequestBorderPolicy(BorderPolicy.Hold, out why), why);
        Assert.AreEqual(BorderPolicy.Hold, world.BorderPolicy);
        Assert.AreEqual(1f, GameValues.Get("stance", "borders:hold"));

        // An edict needs a Head of State, then fills the one slot.
        GameUnitsLogic.Instance.GetResourceSlotFromName("Food").ChangeAmount(200f);
        if (government.HeadOfStateLegend == null)
        {
            Assert.IsFalse(edicts.Seal("scholars_vigil", out why), "no Head of State, no seal");
            StringAssert.Contains("Head of State", why);
            var legend = government.GetAllAvailableLegends().FirstOrDefault();
            Assert.IsNotNull(legend, "a legend waits for a seat");
            Assert.IsTrue(government.AssignLegendToSeat(legend, GovernmentLogic.HeadOfStateIndex, bypassCooldown: true));
        }
        Assert.IsTrue(edicts.Seal("scholars_vigil", out why), why);
        Assert.AreEqual(1f, GameValues.Get("edict", "scholars_vigil"));
        Assert.IsFalse(edicts.Seal("open_granaries", out why), "one slot at three seats");
        StringAssert.Contains("slot", why);

        // It runs out, then rests.
        int duration = EdictCatalog.Edict("scholars_vigil").duration;
        for (int i = 0; i < duration; i++) Seventh(edicts);
        Assert.AreEqual(0f, GameValues.Get("edict", "scholars_vigil"), "it ran its course");
        Assert.Greater(edicts.EdictCooldown("scholars_vigil"), 0, "and rests");
        Assert.IsNull(edicts.WhyNotChange("strangers", "welcome"), "the stance has stood its Phase");

        // The Edicts section draws either way.
        EdictsSection.Open();
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(EdictsSection.IsOpen);

        // The whole state survives a save (GameSnapshot.Schema: _state).
        int decrees = edicts.State.decrees;
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(edicts, "_state"));
        typeof(EdictSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(edicts, new EdictState());
        Assert.IsFalse(EdictSystem.IsEstablished);
        SaveStateCodec.Restore(edicts, JsonUtility.FromJson<StateNode>(saved), "_state");
        Assert.IsTrue(EdictSystem.IsEstablished);
        Assert.AreEqual(decrees, edicts.State.decrees);
        Assert.AreEqual("sealed", edicts.InForce(EdictCatalog.Strangers).id);
        Assert.Greater(edicts.EdictCooldown("scholars_vigil"), 0);

        yield return new ExitPlayMode();
    }
}
