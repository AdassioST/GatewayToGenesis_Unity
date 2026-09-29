using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class LegendRelationshipTests
{
    private int _moment;
    // Stage-depth tests begin with an eligible meter. Meter progression is tested separately below.
    private void Test(LegendRelationship bond, int direction, bool wound = true, bool room = true, string thread = "Void")
    {
        bond.affection = direction > 0 ? 85 : direction < 0 ? 14 : 50;
        LegendRelationshipRules.Experience(bond, (++_moment).ToString(), "Shared hardship", direction, thread, wound, room);
    }

    [TestCase(84, 0)]
    [TestCase(85, 1)]
    [TestCase(15, 0)]
    [TestCase(14, -1)]
    public void ThresholdBoundariesAreExact(int meter, int readiness)
    {
        Assert.AreEqual(readiness, LegendRelationshipRules.Readiness(0, meter));
    }

    [Test]
    public void EnteringThresholdOnlyArmsALaterMatchingExperience()
    {
        var bond = new LegendRelationship { thread = "Void", affection = 84 };
        LegendRelationshipRules.Experience(bond, "approach", "Support", 1, "Void", true, true, 1);
        Assert.AreEqual(85, bond.affection);
        Assert.AreEqual(0, bond.stage);
        Assert.AreEqual(0, bond.growth);
        LegendRelationshipRules.Experience(bond, "meet", "Encounter", 0, "Void", false, true, 3);
        Assert.AreEqual(0, bond.stage, "A neutral encounter does not resolve readiness");
        LegendRelationshipRules.Experience(bond, "resolve", "Support at a cost", 1, "Void", true, true, 12);
        Assert.AreEqual(1, bond.stage);
        Assert.AreEqual(50, bond.affection);
        Assert.AreEqual(0, bond.Readiness);
        bond.affection = 15;
        LegendRelationshipRules.Experience(bond, "fray", "Quarrel", -1, "Void", true, true, -1);
        Assert.AreEqual(1, bond.stage);
        Assert.AreEqual(-1, bond.Readiness);
        LegendRelationshipRules.Experience(bond, "break", "Betrayal", -1, "Void", true, true, -20);
        Assert.AreEqual(0, bond.stage);
        Assert.AreEqual(50, bond.affection);
    }

    [Test]
    public void MeterClampsAndCannotAdvancePastEitherExtreme()
    {
        var high = new LegendRelationship { thread = "Void", stage = 3, affection = 95 };
        var low = new LegendRelationship { thread = "Void", stage = -3, affection = 5 };
        LegendRelationshipRules.Experience(high, "high", "Support", 1, "Void", true, true, int.MaxValue);
        LegendRelationshipRules.Experience(low, "low", "Fracture", -1, "Void", true, true, int.MinValue);
        Assert.AreEqual(100, high.affection); Assert.AreEqual(0, low.affection);
        Assert.AreEqual(0, high.Readiness); Assert.AreEqual(0, low.Readiness);
        Assert.AreEqual(3, high.stage); Assert.AreEqual(-3, low.stage);
    }

    [Test]
    public void LeavingThresholdClearsPendingTestsAndCannotCrossTheOppositeStage()
    {
        var bond = new LegendRelationship { thread = "Void", stage = 1, affection = 90, growth = 1 };
        LegendRelationshipRules.Experience(bond, "reversal", "Conflict", -1, "Void", true, true, -80);
        Assert.AreEqual(1, bond.stage);
        Assert.AreEqual(0, bond.growth); Assert.AreEqual(0, bond.fractures);
        Assert.AreEqual(-1, bond.Readiness);
    }

    [Test]
    public void OldRelationshipSaveStartsAtMidpointWithoutLosingItsStage()
    {
        var bond = new LegendRelationship { thread = "Void", stage = 2, affection = 91 };
        var saved = SaveStateCodec.Write(bond, typeof(LegendRelationship));
        var current = (LegendRelationship)SaveStateCodec.Read(saved, typeof(LegendRelationship));
        Assert.AreEqual(91, current.affection);
        saved.children.RemoveAll(n => n.name == "affectionProgress");
        var old = (LegendRelationship)SaveStateCodec.Read(saved, typeof(LegendRelationship));
        Assert.AreEqual(50, old.affection); Assert.AreEqual(2, old.stage);
    }

    [Test]
    public void MeetingsDoNotManufactureAffectionOrThreads()
    {
        var bond = new LegendRelationship();
        for (int i = 0; i < 20; i++) Test(bond, 0, false);
        Test(bond, 1, false);
        Assert.AreEqual(0, bond.stage);
        Assert.IsFalse(bond.Significant);
        Assert.AreEqual(21, bond.encounters);
    }

    [Test]
    public void DepthRequiresOneThenTwoThenThreeTestsAndEquallyCostlyRetreat()
    {
        var bond = new LegendRelationship();
        Test(bond, 1);
        Assert.AreEqual(1, bond.stage);
        Test(bond, 1); Assert.AreEqual(1, bond.stage);
        Test(bond, 1); Assert.AreEqual(2, bond.stage);
        Test(bond, 1); Test(bond, 1); Assert.AreEqual(2, bond.stage);
        Test(bond, 1); Assert.AreEqual(3, bond.stage);
        Test(bond, -1); Test(bond, -1); Assert.AreEqual(3, bond.stage);
        Test(bond, -1); Assert.AreEqual(2, bond.stage);
        Test(bond, -1); Assert.AreEqual(2, bond.stage);
        Test(bond, -1); Assert.AreEqual(1, bond.stage);
    }

    [Test]
    public void DirectionIsIndependentAndMirroringRemembersBothExtremes()
    {
        var outward = new LegendRelationship();
        var inward = new LegendRelationship();
        for (int i = 0; i < 3; i++) Test(outward, 1);
        Test(inward, -1);
        Assert.AreEqual(2, outward.stage);
        Assert.AreEqual(-1, inward.stage);
        for (int i = 0; i < 6; i++) Test(outward, -1);
        Assert.AreEqual(-2, outward.stage);
        Assert.IsTrue(outward.mirrored);
        Assert.IsFalse(inward.mirrored);
    }

    [Test]
    public void AFullWebRetainsMemoriesWithoutReplacingABond()
    {
        var bond = new LegendRelationship();
        Test(bond, 1, room: false);
        Assert.AreEqual(0, bond.stage);
        Assert.IsFalse(bond.Significant);
        Test(bond, 1);
        Test(bond, 1, room: false, thread: "Flux");
        Assert.AreEqual("Void", bond.thread, "The formative thread never changes");
        Assert.AreEqual(1, bond.growth, "Existing ties can grow at capacity");
    }

    [Test]
    public void ReplayedMomentAndSeveredTieCannotFarmGrowth()
    {
        var bond = new LegendRelationship();
        Assert.IsTrue(LegendRelationshipRules.Experience(bond, "one", "Saved on the road", 1, "Void", true, true));
        Assert.IsFalse(LegendRelationshipRules.Experience(bond, "one", "Saved on the road", 1, "Void", true, true));
        bond.severed = true;
        Test(bond, 1);
        Assert.AreEqual(1, bond.encounters);
        Assert.IsFalse(bond.Significant);
    }

    [Test]
    public void SaveRoundTripPreservesBothHistoryAndPendingTests()
    {
        var bond = new LegendRelationship { other = "Companion" };
        Test(bond, 1); Test(bond, 1);
        var saved = SaveStateCodec.Write(bond, typeof(LegendRelationship));
        var restored = (LegendRelationship)SaveStateCodec.Read(saved, typeof(LegendRelationship));
        Assert.AreEqual(1, restored.stage);
        Assert.AreEqual(1, restored.growth);
        CollectionAssert.AreEqual(bond.moments, restored.moments);
        CollectionAssert.AreEqual(bond.memories, restored.memories);
    }

    [Test]
    public void OldBalladSaveDefaultsNewJournalFields()
    {
        var record = new BalladRecord { id = "song", versesTold = new List<int> { 1 } };
        var saved = SaveStateCodec.Write(record, typeof(BalladRecord));
        saved.children.RemoveAll(n => n.name == "lastDevelopment" || n.name == "lastChoice" || n.name == "lastDate");
        var restored = (BalladRecord)SaveStateCodec.Read(saved, typeof(BalladRecord));
        Assert.IsNull(restored.lastDevelopment);
        CollectionAssert.AreEqual(new[] { 1 }, restored.versesTold);
    }

    [Test]
    public void JournalOffersOnlyEarliestUntoldVerseAndNothingAfterFinale()
    {
        var record = new BalladRecord { id = "song", versesTold = new List<int> { 1 } };
        var stories = new[] { new StoryNode { ballad = "song", verse = 3 }, new StoryNode { ballad = "song", verse = 2 }, new StoryNode { ballad = "other", verse = 1 } };
        Assert.AreEqual(2, BalladJournal.NextVerses(record, stories).Single().verse);
        record.complete = true;
        Assert.IsEmpty(BalladJournal.NextVerses(record, stories));
    }

    [TestCase("affection:protagonist > co | Void +1", true)]
    [TestCase("affection:co > protagonist | Cindergale -1", true)]
    [TestCase("affection:co > protagonist | Cindergale +5", false)]
    [TestCase("affection:co > protagonist | MadeUp +1", false)]
    public void AuthoredTestsValidateDirectionBindingAndOneTest(string input, bool valid)
    {
        var problems = new List<string>();
        var parsed = EventScript.ParseConsequences(input, problems);
        Assert.AreEqual(valid ? 1 : 0, parsed.Count);
        Assert.AreEqual(valid, problems.Count == 0);
    }
}
