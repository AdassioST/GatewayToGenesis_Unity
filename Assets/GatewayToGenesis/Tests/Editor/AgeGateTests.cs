using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The technology tree drives the Age (<see cref="AgeRules.Advance"/>): a gated beat waits for its technology, opens at
/// once when researched early, and the ungated beats after it keep their spacing; plus the crisis factors Era Score and
/// Crisis Advantages add (<see cref="CrisisRules.Factors"/>).
/// </summary>
public class AgeGateTests
{
    // Three Acts of 63; the first boundary and the first crisis stage are gated by one technology.
    private static (List<AgeBeat> schedule, List<string> gates) Desolation()
    {
        var schedule = AgeRules.Schedule(new[] { 63, 63, 63 }, new[] { 0.334f, 0.72f, 0.82f, 0.93f });
        var gates = schedule.Select(b => (b.kind == AgeBeatKind.ActOfFate && b.index == 1) || (b.kind == AgeBeatKind.CrisisStage && b.index == 0) ? "Echoes of Hunger" : null).ToList();
        return (schedule, gates);
    }

    [Test]
    public void AClosedGateHoldsTheClockAndTimeAloneNeverOpensIt()
    {
        var (schedule, gates) = Desolation();
        int next = 0, clock = 0;
        var due = AgeRules.Advance(schedule, gates, ref next, ref clock, 1000, _ => false);
        Assert.IsEmpty(due, "nothing happens before the gate");
        Assert.AreEqual(63, clock, "the clock waits at the gate");
        Assert.AreEqual("Echoes of Hunger", AgeRules.WaitingGate(schedule, gates, next, clock, _ => false));
    }

    [Test]
    public void ResearchOpensTheGateAndTheFamineRunsOnTime()
    {
        var (schedule, gates) = Desolation();
        int next = 0, clock = 10;
        bool researched = false;
        Assert.IsEmpty(AgeRules.Advance(schedule, gates, ref next, ref clock, 0, _ => researched));
        researched = true;
        var due = AgeRules.Advance(schedule, gates, ref next, ref clock, 0, _ => researched);
        Assert.AreEqual(63, clock, "researched early, the gate opens at once");
        CollectionAssert.AreEqual(new[] { AgeBeatKind.ActOfFate, AgeBeatKind.CrisisStage }, due.Select(b => b.kind), "Act II and the famine's first stage together");
        Assert.IsNull(AgeRules.WaitingGate(schedule, gates, next, clock, _ => researched));
        var rest = AgeRules.Advance(schedule, gates, ref next, ref clock, 1000, _ => researched);
        CollectionAssert.AreEqual(new[] { AgeBeatKind.ActOfFate, AgeBeatKind.CrisisStage, AgeBeatKind.CrisisStage, AgeBeatKind.CrisisStage, AgeBeatKind.AgeEnds }, rest.Select(b => b.kind));
        Assert.AreEqual(189, clock, "after the gate, time alone carries the Age to its end");
        Assert.IsEmpty(AgeRules.Advance(schedule, gates, ref next, ref clock, 10, _ => true), "every beat fires once");
    }

    [Test]
    public void EraScoreAndAdvantagesEaseTheCrisisWithinTheirCaps()
    {
        var tuning = new CrisisTuning();
        var calm = CrisisRules.Severity(CrisisRules.Factors(new CrisisInputs { population = 50 }, tuning, CrisisRules.Labels.Default));
        var helped = CrisisRules.Factors(new CrisisInputs { population = 50, advantages = 3, eraScore = tuning.eraScoreThreshold + 5 }, tuning, CrisisRules.Labels.Default);
        Assert.Less(CrisisRules.Severity(helped), calm);
        Assert.AreEqual(-3 * tuning.perAdvantage, helped.First(f => f.label == "Crisis Advantages").value, 1e-5f);
        Assert.AreEqual(-5 * tuning.perExcessEraScore, helped.First(f => f.label == "Era Score beyond the need").value, 1e-5f);
        var capped = CrisisRules.Factors(new CrisisInputs { advantages = 1000, eraScore = 1000 }, tuning, CrisisRules.Labels.Default);
        Assert.AreEqual(-tuning.advantageCap, capped.First(f => f.label == "Crisis Advantages").value, 1e-5f);
        Assert.AreEqual(-tuning.eraScoreCap, capped.First(f => f.label == "Era Score beyond the need").value, 1e-5f);
        Assert.IsFalse(CrisisRules.Factors(new CrisisInputs { eraScore = tuning.eraScoreThreshold }, tuning, CrisisRules.Labels.Default).Any(f => f.label == "Era Score beyond the need"), "only the surplus counts");
    }

    // ===== THE BAR MEASURED BY THE TREE =====

    // A small tree: Hunger needs Rites and Storage; Rites needs Reconstruction; Storage needs Reconstruction too.
    private static readonly Dictionary<string, string[]> Tree = new Dictionary<string, string[]>
    {
        { "Reconstruction", new string[0] },
        { "Rites", new[] { "Reconstruction" } },
        { "Storage", new[] { "Reconstruction" } },
        { "Hunger", new[] { "Rites", "Storage" } },
        { "Songs", new[] { "Hunger" } },
        { "Sanctums", new[] { "Songs", "Rites" } },
    };

    private static IEnumerable<string> Needs(string technology) => Tree.TryGetValue(technology, out var before) ? before : new string[0];

    [Test]
    public void AnActsPathIsItsGateAndThePrerequisitesNoEarlierActCovers()
    {
        CollectionAssert.AreEqual(new[] { "Reconstruction", "Rites", "Storage", "Hunger" }, AgeRules.PathTo("Hunger", Needs), "prerequisites first, each once");
        var gates = new List<string> { "Hunger", "Sanctums" };
        CollectionAssert.AreEquivalent(new[] { "Reconstruction", "Rites", "Storage", "Hunger" }, AgeRules.ActPath(0, gates, Needs));
        CollectionAssert.AreEquivalent(new[] { "Songs", "Sanctums" }, AgeRules.ActPath(1, gates, Needs), "Act II only counts what Act I did not");
        Assert.IsNull(AgeRules.ActPath(2, gates, Needs), "the last Act ends with the Age, not a technology");
        Assert.IsNull(AgeRules.ActPath(1, new List<string> { "Hunger", "" }, Needs), "an Act with no technology is timed");
    }

    [Test]
    public void TheBarGivesEachActAnEqualShare()
    {
        Assert.AreEqual(0f, AgeRules.BarProgress(0, 3, 0f), 1e-5f);
        Assert.AreEqual(0.5f / 3f, AgeRules.BarProgress(0, 3, 0.5f), 1e-5f, "half of Act I's path researched");
        Assert.AreEqual(1f / 3f, AgeRules.BarProgress(1, 3, 0f), 1e-5f, "Act II begins at the first tick");
        Assert.AreEqual(1f, AgeRules.BarProgress(2, 3, 1f), 1e-5f);
        Assert.AreEqual(1f, AgeRules.BarProgress(5, 3, 2f), 1e-5f, "clamped");
    }
}
