using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The technology tree's rules (<see cref="TechTreeRules"/>) on the Age of Desolation's Act I tree as authored (its
/// prerequisites from Resources/Technology, its cells from AgeTechnology.prefab): what is uncovered and what stays
/// hidden, how the lines read, that no line crosses a slot, the order a research plan takes, and how an
/// Enlightenment goal is worded.
/// </summary>
public class TechTreeTests
{
    private static readonly Dictionary<string, string[]> Needs = new Dictionary<string, string[]>
    {
        { "Reconstruction", new string[0] },
        { "Woodcraft Mastery", new[] { "Reconstruction" } },
        { "Efficient Rations", new[] { "Reconstruction" } },
        { "Rites of Harvest", new[] { "Woodcraft Mastery", "Efficient Rations" } },
        { "Agricultural Renewal", new[] { "Efficient Rations" } },
        { "Horology", new[] { "Rites of Harvest" } },
        { "Pathfinder Training", new[] { "Woodcraft Mastery", "Rites of Harvest" } },
        { "Resource Storage", new[] { "Agricultural Renewal" } },
        { "The Rekindling", new[] { "Horology", "Pathfinder Training" } },
        { "Advanced Woodworking", new[] { "The Rekindling" } },
        { "Knowledge Sanctums", new[] { "The Rekindling" } },
        { "Resource Preservation", new[] { "Advanced Woodworking", "Resource Storage" } },
        { "Edicts of Stone and Bone", new[] { "Advanced Woodworking" } },
        { "Celestial Astrology", new[] { "Edicts of Stone and Bone" } },
        { "Vital Winds Mastery", new[] { "Knowledge Sanctums" } },
        { "Chants of Ash", new[] { "Celestial Astrology" } },
        { "Songs of the Moon", new[] { "Celestial Astrology" } },
        { "Fortified Living", new[] { "Vital Winds Mastery", "Celestial Astrology" } },
        { "Unsustainable Growth", new[] { "Songs of the Moon", "Chants of Ash", "Fortified Living" } },
        { "Echoes of Hunger", new[] { "Unsustainable Growth" } },
    };

    // (column, row) of each technology in the research tab's grid.
    private static readonly Dictionary<string, (int column, int row)> Cells = new Dictionary<string, (int, int)>
    {
        { "Reconstruction", (0, 1) },
        { "Woodcraft Mastery", (1, 1) }, { "Efficient Rations", (1, 2) },
        { "Rites of Harvest", (2, 0) }, { "Agricultural Renewal", (2, 2) },
        { "Horology", (3, 0) }, { "Pathfinder Training", (3, 1) }, { "Resource Storage", (3, 2) },
        { "The Rekindling", (4, 1) },
        { "Advanced Woodworking", (5, 0) }, { "Knowledge Sanctums", (5, 2) },
        { "Resource Preservation", (6, 0) }, { "Edicts of Stone and Bone", (6, 1) },
        { "Celestial Astrology", (7, 1) }, { "Vital Winds Mastery", (7, 2) },
        { "Chants of Ash", (8, 0) }, { "Songs of the Moon", (8, 1) }, { "Fortified Living", (8, 2) },
        { "Unsustainable Growth", (9, 1) },
        { "Echoes of Hunger", (10, 1) },
    };

    // Like TechnologyTreeLogic.Prerequisites: null for a technology that exists nowhere.
    private static IEnumerable<string> Prerequisites(string technology) => Needs.TryGetValue(technology, out var before) ? before : null;

    private static bool Occupied(int column, int row) => Cells.Values.Contains((column, row));

    private static TechVisibility See(string technology, ICollection<string> researched, ICollection<string> enlightened = null, string revealed = null) =>
        TechTreeRules.Visibility(technology, Prerequisites, researched.Contains, t => enlightened != null && enlightened.Contains(t), t => t == revealed);

    // ===== WHAT IS UNCOVERED =====

    [Test]
    public void AtTheStartOnlyReconstructionAndItsNextStepShow()
    {
        var researched = new HashSet<string>();
        Assert.AreEqual(TechVisibility.Available, See("Reconstruction", researched));
        Assert.AreEqual(TechVisibility.Preview, See("Woodcraft Mastery", researched), "one step past what can be researched shows, locked");
        Assert.AreEqual(TechVisibility.Preview, See("Efficient Rations", researched));
        Assert.AreEqual(TechVisibility.Hidden, See("Rites of Harvest", researched), "two steps away stays hidden");
        Assert.AreEqual(TechVisibility.Hidden, See("Echoes of Hunger", researched));
    }

    [Test]
    public void ResearchUncoversTheTreeOneStepAtATime()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual(TechVisibility.Researched, See("Reconstruction", researched));
        Assert.AreEqual(TechVisibility.Available, See("Woodcraft Mastery", researched));
        Assert.AreEqual(TechVisibility.Preview, See("Rites of Harvest", researched));
        Assert.AreEqual(TechVisibility.Preview, See("Pathfinder Training", researched), "one of its prerequisites can be researched now");
        Assert.AreEqual(TechVisibility.Hidden, See("Horology", researched), "its only prerequisite is itself locked");

        researched.UnionWith(new[] { "Woodcraft Mastery", "Efficient Rations" });
        Assert.AreEqual(TechVisibility.Available, See("Rites of Harvest", researched), "every prerequisite researched");
        Assert.AreEqual(TechVisibility.Preview, See("Horology", researched));
    }

    [Test]
    public void AnEnlightenedTechnologyAndTheAgesGateShowBeforeTheirWay()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual(TechVisibility.Preview, See("Echoes of Hunger", researched, new[] { "Echoes of Hunger" }));
        Assert.AreEqual(TechVisibility.Preview, See("Echoes of Hunger", researched, revealed: "Echoes of Hunger"), "the Age waits for it, so it shows");
        Assert.AreEqual(TechVisibility.Hidden, See("Unsustainable Growth", researched, new[] { "Echoes of Hunger" }), "its way stays hidden");
        Assert.AreEqual(TechVisibility.Available, See("Woodcraft Mastery", researched, new[] { "Woodcraft Mastery" }), "enlightened and researchable reads as researchable");
    }

    [Test]
    public void AMissingPrerequisiteKeepsATechnologyLocked()
    {
        IEnumerable<string> Broken(string t) => t == "Orphan" ? new[] { "Nowhere" } : null;
        Assert.AreEqual(TechVisibility.Hidden, TechTreeRules.Visibility("Orphan", Broken, _ => false), "a prerequisite that exists nowhere is never researchable, so nothing previews its dependant");
        Assert.IsFalse(TechTreeRules.IsAvailable("Orphan", Broken, _ => false));
        Assert.IsFalse(TechTreeRules.IsAvailable("Nowhere", Broken, _ => false));
    }

    // ===== HOW THE LINES READ =====

    [Test]
    public void LinesJoinOnlyUncoveredTechnologiesAndAStubStandsForAHiddenOne()
    {
        Assert.IsTrue(TechTreeRules.ShowsLine(TechVisibility.Available, TechVisibility.Preview));
        Assert.IsFalse(TechTreeRules.ShowsLine(TechVisibility.Available, TechVisibility.Hidden), "no line gives a hidden technology away");
        Assert.IsFalse(TechTreeRules.ShowsLine(TechVisibility.Hidden, TechVisibility.Preview));
        Assert.IsTrue(TechTreeRules.NeedsStub(TechVisibility.Hidden, TechVisibility.Preview), "an enlightened technology's hidden way is a stub");
        Assert.IsFalse(TechTreeRules.NeedsStub(TechVisibility.Preview, TechVisibility.Hidden));
    }

    [Test]
    public void LineTonesFollowTheResearch()
    {
        Assert.AreEqual(LineTone.Complete, TechTreeRules.BranchTone(TechVisibility.Researched, TechVisibility.Researched, false, false));
        Assert.AreEqual(LineTone.Open, TechTreeRules.BranchTone(TechVisibility.Researched, TechVisibility.Preview, true, false), "a researched prerequisite lights its branch");
        Assert.AreEqual(LineTone.Planned, TechTreeRules.BranchTone(TechVisibility.Available, TechVisibility.Preview, true, false));
        Assert.AreEqual(LineTone.Pending, TechTreeRules.BranchTone(TechVisibility.Available, TechVisibility.Preview, false, false));
        Assert.AreEqual(LineTone.Focus, TechTreeRules.BranchTone(TechVisibility.Available, TechVisibility.Preview, true, true));

        Assert.AreEqual(LineTone.Ready, TechTreeRules.TrunkTone(TechVisibility.Available, false, false));
        Assert.AreEqual(LineTone.Pending, TechTreeRules.TrunkTone(TechVisibility.Preview, false, false), "a locked technology's trunk stays dim");
        Assert.AreEqual(LineTone.Planned, TechTreeRules.TrunkTone(TechVisibility.Preview, true, false));
        Assert.AreEqual(LineTone.Complete, TechTreeRules.TrunkTone(TechVisibility.Researched, true, false));
        Assert.Less((int)LineTone.Pending, (int)LineTone.Focus, "brighter tones are drawn last");
    }

    // ===== ROUTING =====

    private static GridPoint P(GridX x, int column, GridY y, int row) => new GridPoint(x, column, y, row);

    private static List<GridPoint> Route(string from, string to)
    {
        var a = Cells[from];
        var b = Cells[to];
        return TechTreeRules.Route(a.column, a.row, b.column, b.row, Occupied, 3);
    }

    [Test]
    public void NeighboursJoinStraightOrWithOneTurnInTheirGap()
    {
        CollectionAssert.AreEqual(new[] { P(GridX.SlotRight, 0, GridY.Row, 1), P(GridX.GapAfter, 0, GridY.Row, 1), P(GridX.SlotLeft, 1, GridY.Row, 1) },
            Route("Reconstruction", "Woodcraft Mastery"));
        CollectionAssert.AreEqual(new[] { P(GridX.SlotRight, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 0), P(GridX.SlotLeft, 2, GridY.Row, 0) },
            Route("Woodcraft Mastery", "Rites of Harvest"));
    }

    [Test]
    public void ALongLineTakesAFreeRowOrTheChannelBetweenRows()
    {
        // Woodcraft Mastery to Pathfinder Training: the cell between them on their row is empty.
        CollectionAssert.AreEqual(new[] { P(GridX.SlotRight, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 1), P(GridX.GapAfter, 2, GridY.Row, 1), P(GridX.SlotLeft, 3, GridY.Row, 1) },
            Route("Woodcraft Mastery", "Pathfinder Training"));
        // Resource Storage to Resource Preservation: every row between them holds a technology, so the channel carries it.
        CollectionAssert.AreEqual(new[]
        {
            P(GridX.SlotRight, 3, GridY.Row, 2), P(GridX.GapAfter, 3, GridY.Row, 2), P(GridX.GapAfter, 3, GridY.ChannelBelow, 0),
            P(GridX.GapAfter, 5, GridY.ChannelBelow, 0), P(GridX.GapAfter, 5, GridY.Row, 0), P(GridX.SlotLeft, 6, GridY.Row, 0)
        }, Route("Resource Storage", "Resource Preservation"));
    }

    [Test]
    public void NoLineOfTheActITreeCrossesASlotAndEachEndsWithItsTrunk()
    {
        foreach (var entry in Needs)
        {
            foreach (var before in entry.Value)
            {
                var route = Route(before, entry.Key);
                var to = Cells[entry.Key];
                Assert.AreEqual(P(GridX.SlotLeft, to.column, GridY.Row, to.row), route[route.Count - 1], $"{before} -> {entry.Key} ends at its left edge");
                Assert.AreEqual(P(GridX.GapAfter, to.column - 1, GridY.Row, to.row), route[route.Count - 2], $"{before} -> {entry.Key} ends with the shared trunk");
                for (int i = 1; i < route.Count; i++)
                {
                    var (a, b) = (route[i - 1], route[i]);
                    if (a.y != GridY.Row || b.y != GridY.Row || a.row != b.row) continue;
                    // A horizontal run along a row passes over the cells strictly between its two ends.
                    float x1 = Across(a), x2 = Across(b);
                    for (int column = 0; column <= 10; column++)
                    {
                        if (column <= System.Math.Min(x1, x2) || column >= System.Math.Max(x1, x2)) continue;
                        Assert.IsFalse(Occupied(column, a.row), $"{before} -> {entry.Key} crosses the slot in column {column}, row {a.row}");
                    }
                }
            }
        }
    }

    // A route point's x in column units: a slot spans column ± 0.4, the gap after it sits at column + 0.5.
    private static float Across(GridPoint p) => p.x == GridX.SlotRight ? p.column + 0.4f : p.x == GridX.SlotLeft ? p.column - 0.4f : p.column + 0.5f;

    [Test]
    public void ABackwardLineStillRunsThroughGapsAndAChannel()
    {
        var route = TechTreeRules.Route(4, 0, 2, 0, (c, r) => false, 3);
        Assert.AreEqual(P(GridX.SlotLeft, 2, GridY.Row, 0), route[route.Count - 1]);
        Assert.IsTrue(route.Any(p => p.y == GridY.ChannelBelow), "it turns along a channel instead of through the slots");
    }

    // ===== THE RESEARCH PLAN =====

    private static void AssertPrerequisitesFirst(IReadOnlyList<string> plan, string label)
    {
        for (int i = 0; i < plan.Count; i++)
            foreach (var before in Prerequisites(plan[i]))
            {
                int at = plan.ToList().IndexOf(before);
                if (at >= 0) Assert.Less(at, i, $"{label}: {before} must come before {plan[i]}");
            }
    }

    [Test]
    public void ALockedTechnologyPlansItsMissingPrerequisitesFirst()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        var plan = TechTreeRules.PlanTo("Pathfinder Training", Prerequisites, researched.Contains);
        CollectionAssert.AreEqual(new[] { "Woodcraft Mastery", "Efficient Rations", "Rites of Harvest", "Pathfinder Training" }, plan);
        Assert.IsEmpty(TechTreeRules.PlanTo("Reconstruction", Prerequisites, researched.Contains), "nothing to plan once researched");
    }

    [Test]
    public void EveryPlanInTheTreeKeepsPrerequisitesFirst()
    {
        foreach (var technology in Needs.Keys)
        {
            var plan = TechTreeRules.PlanTo(technology, Prerequisites, _ => false);
            Assert.AreEqual(technology, plan.Last(), "the target comes last");
            Assert.AreEqual(plan.Count, plan.Distinct().Count(), "each technology once");
            AssertPrerequisitesFirst(plan, technology);
        }
    }

    [Test]
    public void ANewTargetReplacesThePlanButResearchUnderWayOnItsWayStaysFirst()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        var plan = TechTreeRules.PlanTo("Pathfinder Training", Prerequisites, researched.Contains);
        var path = TechTreeRules.PlanTo("Horology", Prerequisites, researched.Contains);
        bool Available(string t) => TechTreeRules.IsAvailable(t, Prerequisites, researched.Contains);

        var replaced = TechTreeRules.Merge(plan, path, false, "Efficient Rations", Available);
        CollectionAssert.AreEqual(new[] { "Efficient Rations", "Woodcraft Mastery", "Rites of Harvest", "Horology" }, replaced);
        AssertPrerequisitesFirst(replaced, "replaced");

        var appended = TechTreeRules.Merge(plan, TechTreeRules.PlanTo("Resource Storage", Prerequisites, researched.Contains), true);
        CollectionAssert.AreEqual(new[] { "Woodcraft Mastery", "Efficient Rations", "Rites of Harvest", "Pathfinder Training", "Agricultural Renewal", "Resource Storage" }, appended);
        AssertPrerequisitesFirst(appended, "appended");
    }

    [Test]
    public void TakingATechnologyOutOfThePlanTakesWhatNeedsItToo()
    {
        var plan = new List<string> { "Woodcraft Mastery", "Efficient Rations", "Rites of Harvest", "Pathfinder Training", "Horology", "The Rekindling", "Agricultural Renewal" };
        CollectionAssert.AreEqual(new[] { "Woodcraft Mastery", "Efficient Rations", "Agricultural Renewal" }, TechTreeRules.Without(plan, "Rites of Harvest", Prerequisites));
        CollectionAssert.AreEqual(plan, TechTreeRules.Without(plan, "Knowledge Sanctums", Prerequisites), "a technology not planned changes nothing");
    }

    [Test]
    public void ThePlansNextResearchIsItsFirstResearchableTechnology()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual("Woodcraft Mastery", TechTreeRules.NextInPlan(new[] { "Rites of Harvest", "Woodcraft Mastery" }, Prerequisites, researched.Contains));
        Assert.IsNull(TechTreeRules.NextInPlan(new[] { "Horology" }, Prerequisites, researched.Contains));
        Assert.AreEqual(2, TechTreeRules.PlanPosition(new[] { "Woodcraft Mastery", "Horology" }, "horology"), "names match as the catalogs do, ignoring case");
        Assert.AreEqual(0, TechTreeRules.PlanPosition(new[] { "Woodcraft Mastery" }, "Horology"));
    }

    [Test]
    public void TheActITreeHasNoLoopAndALoopIsFound()
    {
        Assert.IsEmpty(TechTreeRules.Cycles(Needs.Keys, Prerequisites));
        IEnumerable<string> Loop(string t) => t == "A" ? new[] { "B" } : t == "B" ? new[] { "A" } : new string[0];
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, TechTreeRules.Cycles(new[] { "A", "B", "C" }, Loop));
    }

    // ===== ENLIGHTENMENT =====

    [Test]
    public void EnlightenmentProgressIsShortAndOnlyForCounts()
    {
        Assert.AreEqual(string.Empty, TechTreeRules.ProgressText(0f, 1f), "a goal simply met or not shows no count");
        Assert.AreEqual("(120/250)", TechTreeRules.ProgressText(120f, 250f));
        Assert.AreEqual("(3.5/55 /s)", TechTreeRules.ProgressText(3.46f, 55f, rate: true));
        Assert.AreEqual("(4K/4K)", TechTreeRules.ProgressText(5000f, 4000f), "never past the goal");
        Assert.AreEqual("1.2K", TechTreeRules.Short(1234f));
        Assert.AreEqual("150", TechTreeRules.Short(150f));
        Assert.AreEqual("12.3", TechTreeRules.Short(12.345f));
    }

    [Test]
    public void OnlyEventAndCrisisTechnologiesEarnEraScoreWhenEnlightened()
    {
        Assert.AreEqual(1, TechTreeRules.EnlightenmentEraScore(true, false, 1), "an Event Technology earns it");
        Assert.AreEqual(1, TechTreeRules.EnlightenmentEraScore(false, true, 1), "a Crisis Technology earns it");
        Assert.AreEqual(1, TechTreeRules.EnlightenmentEraScore(true, true, 1), "once, even for one that is both");
        Assert.AreEqual(0, TechTreeRules.EnlightenmentEraScore(false, false, 1), "any other technology earns nothing");
        Assert.AreEqual(0, TechTreeRules.EnlightenmentEraScore(true, false, -2), "never takes Era Score away");
    }

    [Test]
    public void AnEnlightenmentGoalWithoutWordsIsWordedFromItself()
    {
        Assert.AreEqual("Build 3 Timber Camp", new EnlightenedCondition { type = EnlightenedType.ConstructUnit, trigger = "Timber Camp", requiredAmount = 3f }.Describe());
        Assert.AreEqual("Produce 55 Food per second", new EnlightenedCondition { type = EnlightenedType.ReachProductionRate, trigger = "Food", requiredAmount = 55f }.Describe());
        Assert.AreEqual("Witness the Aurean Winds", new EnlightenedCondition { type = EnlightenedType.GameValue, trigger = "weather:Aurean Winds", requiredAmount = 1f, description = " Witness the Aurean Winds " }.Describe(), "written words win");

        var goal = new EnlightenedCondition { type = EnlightenedType.GameValue, trigger = "weather: Aurean Winds" };
        goal.SplitTrigger(out string domain, out string target);
        Assert.AreEqual(("weather", "Aurean Winds"), (domain, target));
    }
}
