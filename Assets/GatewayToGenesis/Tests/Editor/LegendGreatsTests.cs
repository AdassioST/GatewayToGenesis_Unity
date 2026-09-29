using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Greats as earned standings (every legend starts an Unattuned Legend; fragments of a Great's kind become 1-3
/// stars), what a council seat asks for, and the open conditions API (Traumatized, Haunted...). No scene.
/// </summary>
public class LegendGreatsTests
{
    private static readonly FragmentTuning Tuning = new FragmentTuning();

    private static Dictionary<string, int> Purse(params (FragmentKind kind, int amount)[] fragments)
    {
        var purse = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (kind, amount) in fragments) LyricalFragments.Add(purse, kind, amount);
        return purse;
    }

    [TestCase(LegendClass.Vanguard, FragmentKind.Defiance)]
    [TestCase(LegendClass.Sovereign, FragmentKind.Meaning)]
    [TestCase(LegendClass.Architect, FragmentKind.Vision)]
    [TestCase(LegendClass.Concertist, FragmentKind.Catharsis)]
    [TestCase(LegendClass.Seer, FragmentKind.Lucidity)]
    [TestCase(LegendClass.Justiciar, FragmentKind.Acceptance)]
    [TestCase(LegendClass.Chronicler, FragmentKind.Rebirth)]
    public void EachGreat_GrowsFromItsBindingsFragments(LegendClass great, FragmentKind kind)
    {
        Assert.AreEqual(kind, LegendGreats.KindOf(great));
        Assert.AreEqual(great, LegendGreats.GreatOf(kind));
    }

    [Test]
    public void EveryLegend_StartsUnattuned()
    {
        var empty = Purse();
        Assert.AreEqual(LegendGreats.UnattunedTitle, LegendGreats.Title(empty, Tuning));
        Assert.IsEmpty(LegendGreats.Standing(empty, Tuning));
        Assert.AreEqual("Unattuned Legend", LegendGreats.UnattunedTitle);
    }

    [Test]
    public void Stars_FollowTheThresholds()
    {
        var t = Tuning.greatStars;
        Assert.AreEqual(0, LegendGreats.Stars(t[0] - 1, Tuning));
        Assert.AreEqual(1, LegendGreats.Stars(t[0], Tuning));
        Assert.AreEqual(2, LegendGreats.Stars(t[1], Tuning));
        Assert.AreEqual(3, LegendGreats.Stars(t[2], Tuning));
        Assert.AreEqual(3, LegendGreats.Stars(t[2] * 10, Tuning), "three stars is the most");
        Assert.AreEqual(t[1], LegendGreats.NextStarAt(1, Tuning));
        Assert.AreEqual(-1, LegendGreats.NextStarAt(3, Tuning));
    }

    [Test]
    public void ALegend_CanStandInSeveralGreatsAtOnce()
    {
        var t = Tuning.greatStars;
        var purse = Purse((FragmentKind.Meaning, t[2]), (FragmentKind.Defiance, t[1]), (FragmentKind.Vision, t[0]), (FragmentKind.Rebirth, t[0] - 1));
        var standing = LegendGreats.Standing(purse, Tuning);
        Assert.AreEqual(3, standing[LegendClass.Sovereign]);
        Assert.AreEqual(2, standing[LegendClass.Vanguard]);
        Assert.AreEqual(1, standing[LegendClass.Architect]);
        Assert.IsFalse(standing.ContainsKey(LegendClass.Chronicler));
        Assert.AreEqual("3★ Great Sovereign · 2★ Great Vanguard · 1★ Great Architect", LegendGreats.Title(standing));
        Assert.AreEqual(LegendClass.Sovereign, LegendGreats.Highest(standing));
    }

    [Test]
    public void Seats_AskForStarsInOneOfTheirGreats()
    {
        var t = Tuning.greatStars;
        var vanguard2 = Purse((FragmentKind.Defiance, t[1]));
        var greats = new[] { LegendClass.Vanguard, LegendClass.Justiciar };
        Assert.IsTrue(LegendGreats.Meets(vanguard2, greats, 2, Tuning));
        Assert.IsFalse(LegendGreats.Meets(vanguard2, greats, 3, Tuning));
        Assert.IsFalse(LegendGreats.Meets(vanguard2, new[] { LegendClass.Sovereign }, 1, Tuning));
        Assert.IsTrue(LegendGreats.Meets(Purse(), greats, 0, Tuning), "0 stars: any legend, an Unattuned Legend too");
        Assert.AreEqual("2★ Great Vanguard or Great Justiciar", LegendGreats.Requirement(greats, 2));
        StringAssert.StartsWith("Any legend", LegendGreats.Requirement(greats, 0));
    }

    [Test]
    public void CouncilSeat_ZeroStarsTakesAnyLegend()
    {
        var seat = new CouncilSeat("Test", 0) { requiredStars = 0 };
        seat.allowedLegendClasses.Add(LegendClass.Vanguard);
        var legend = UnityEngine.ScriptableObject.CreateInstance<LegendData>();
        legend.legendName = "Nobody";
        legend.legendClass = LegendClass.Seer;
        Assert.IsTrue(seat.CanAssignLegend(legend));
        UnityEngine.Object.DestroyImmediate(legend);
    }

    // ---- Conditions ------------------------------------------------------------------------------------------------

    [Test]
    public void Conditions_CountDownAndKeepTheLonger()
    {
        var list = new List<LegendCondition>();
        LegendConditions.Add(list, LegendConditions.Traumatized, 3, "battle");
        LegendConditions.Add(list, LegendConditions.Traumatized, 2, "a second battle");
        Assert.AreEqual(1, list.Count, "one condition of a kind");
        Assert.AreEqual(3, list[0].sevenths, "a second trauma does not shorten the first");
        LegendConditions.Add(list, LegendConditions.Haunted, 0, "missing in action");
        Assert.IsEmpty(LegendConditions.Tick(list));
        Assert.IsEmpty(LegendConditions.Tick(list));
        var ended = LegendConditions.Tick(list);
        Assert.AreEqual(LegendConditions.Traumatized, ended.Single().id);
        Assert.IsTrue(LegendConditions.Has(list, LegendConditions.Haunted), "0 Sevenths: until lifted");
        Assert.IsTrue(LegendConditions.Remove(list, LegendConditions.Haunted));
        Assert.IsEmpty(list);
        StringAssert.Contains("Traumatized", LegendConditions.Describe(new LegendCondition(LegendConditions.Traumatized, 5, "x")));
    }

    [Test]
    public void Conditions_OtherSystemsCanRegisterTheirOwn()
    {
        string id = "test-shellshock-" + System.Guid.NewGuid().ToString("N");
        Assert.IsTrue(LegendConditions.Register(new LegendConditionSpec { id = id, name = "Shellshocked", description = "test" }));
        Assert.IsFalse(LegendConditions.Register(new LegendConditionSpec { id = id, name = "again" }), "ids are unique");
        Assert.AreEqual("Shellshocked", LegendConditions.Name(id));
        Assert.IsFalse(LegendConditions.Register(new LegendConditionSpec { id = LegendConditions.Traumatized }));
    }
}
