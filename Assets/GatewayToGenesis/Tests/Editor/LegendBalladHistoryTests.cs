using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class LegendBalladHistoryTests
{
    [Test]
    public void ResolutionRequiresActualParticipationAndCannotAwardTwice()
    {
        var history = new LegendBalladHistory();
        var opus = LesserOpusCatalog.Find("orchard-seeds");
        Assert.IsFalse(history.Resolve(opus, "sb_orchard_3", "sb_orchard", 3, true, 0));
        Assert.IsTrue(history.Record("sb_orchard_3", "sb_orchard", 3, "Witness", 0));
        Assert.IsFalse(history.Record("sb_orchard_3", "sb_orchard", 3, "Hero", 0));
        Assert.IsFalse(history.Resolve(opus, "sb_orchard_3", "sb_orchard", 3, false, 0));
        Assert.IsTrue(history.Resolve(opus, "sb_orchard_3", "sb_orchard", 3, true, 0));
        Assert.IsFalse(history.Resolve(LesserOpusCatalog.Find("orchard-harvest"), "sb_orchard_3", "sb_orchard", 3, true, 0));
        Assert.AreEqual(1, history.lesserOpus.Single().participation.Count, "Late cast members must not inherit earlier verse credit.");
        StringAssert.Contains("Locked", history.Describe());
        StringAssert.Contains(opus.futureTitle, history.Describe());
        var restored = (LegendBalladHistory)SaveStateCodec.Read(SaveStateCodec.Write(history, typeof(LegendBalladHistory)), typeof(LegendBalladHistory));
        Assert.AreEqual(history.Describe(), restored.Describe());
        Assert.IsFalse(restored.Resolve(opus, "sb_orchard_3", "sb_orchard", 3, true, 0));
    }

    [Test]
    public void OlderLegendRecordsDefaultHistoryButStillRejectMissingRequiredAndUnknownFields()
    {
        var type = typeof(LegendProgress).GetNestedType("Record", BindingFlags.NonPublic);
        var record = Activator.CreateInstance(type, true);
        var state = SaveStateCodec.Write(record, type);
        state.children.RemoveAll(c => c.name == "balladHistory");
        var restored = SaveStateCodec.Read(state, type);
        var history = (LegendBalladHistory)SaveStateCodec.Field(type, "balladHistory").GetValue(restored);
        Assert.IsNotNull(history);
        Assert.IsEmpty(history.lesserOpus);
        state.children.Add(new StateNode { name = "unknown", kind = "null" });
        Assert.Throws<InvalidOperationException>(() => SaveStateCodec.Read(state, type));
        state.children.RemoveAll(c => c.name == "unknown");
        state.children.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => SaveStateCodec.Read(state, type));
    }
}
