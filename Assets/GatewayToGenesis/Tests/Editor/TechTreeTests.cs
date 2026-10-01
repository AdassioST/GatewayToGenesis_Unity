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
    // The Act I tree (Docs/Planning/TECH_TREE_ACT_I.md): 40 technologies in three lanes, Hearth (row 0), Stone (row 1)
    // and Song (row 2). TheAuthoredTreeIsTheOneTested checks this copy against Resources/Technology.
    private static readonly Dictionary<string, string[]> Needs = new Dictionary<string, string[]>
    {
        { "Reconstruction", new string[0] },
        { "Shared Embers", new[] { "Reconstruction" } },
        { "Elderwood Felling", new[] { "Reconstruction" } },
        { "The Ruin-Song", new[] { "Reconstruction" } },
        { "Harvest Hymns", new[] { "Shared Embers", "Elderwood Felling" } },
        { "Colonnade Salvage", new[] { "Elderwood Felling", "The Ruin-Song" } },
        { "Staccato", new[] { "The Ruin-Song", "Shared Embers" } },
        { "Root-Digging", new[] { "Harvest Hymns" } },
        { "Ash-Cellars", new[] { "Colonnade Salvage" } },
        { "Horology", new[] { "Harvest Hymns", "Staccato" } },
        { "Earth-Bean Rows", new[] { "Root-Digging" } },
        { "Lookout Towers", new[] { "Ash-Cellars" } },
        { "The Minor Note", new[] { "Staccato", "Horology" } },
        { "Trapper's Patience", new[] { "Earth-Bean Rows", "Lookout Towers" } },
        { "Peat and Kiln", new[] { "Lookout Towers" } },
        { "Ostinato", new[] { "The Minor Note", "Harvest Hymns" } },
        { "Midwives' Lullaby", new[] { "Earth-Bean Rows", "The Minor Note" } },
        { "The Rekindling", new[] { "Lookout Towers", "Horology" } },
        { "Unison", new[] { "Ostinato", "The Minor Note" } },
        { "Wild Honey Keeping", new[] { "Trapper's Patience" } },
        { "Heartwood Joinery", new[] { "Peat and Kiln", "The Rekindling" } },
        { "Knowledge Sanctums", new[] { "The Rekindling", "Ostinato" } },
        { "Salt and Smoke", new[] { "Wild Honey Keeping", "Heartwood Joinery" } },
        { "Stonebound Walls", new[] { "Heartwood Joinery" } },
        { "Call and Response", new[] { "Knowledge Sanctums", "The Rekindling" } },
        { "Reading the Vital Winds", new[] { "Knowledge Sanctums", "Wild Honey Keeping" } },
        { "Old World Roads", new[] { "Stonebound Walls" } },
        { "The Seventh Degree", new[] { "Call and Response", "Unison" } },
        { "Songs of the Moon", new[] { "Reading the Vital Winds" } },
        { "Chants of Ash", new[] { "Old World Roads" } },
        { "The Stave of Stars", new[] { "The Seventh Degree", "Call and Response" } },
        { "Moonlit Vigil", new[] { "Songs of the Moon", "Midwives' Lullaby" } },
        { "Sky Glass Burials", new[] { "Chants of Ash", "The Stave of Stars" } },
        { "The Dual Confluence", new[] { "Songs of the Moon", "Chants of Ash" } },
        { "Golden Orchard Belts", new[] { "Salt and Smoke" } },
        { "Golden Ash", new[] { "Chants of Ash" } },
        { "Fermata", new[] { "The Dual Confluence", "The Stave of Stars" } },
        { "Unsustainable Growth", new[] { "Moonlit Vigil", "Sky Glass Burials", "Salt and Smoke" } },
        { "Da Capo", new[] { "Fermata" } },
        { "Echoes of Hunger", new[] { "Unsustainable Growth", "Da Capo" } },
    };

    // (column, row) of each technology in AgeTechnology.prefab: column = tier, row = lane; Reconstruction and Echoes of
    // Hunger stand alone in the middle row.
    private static readonly Dictionary<string, (int column, int row)> Cells = Needs.Keys.ToDictionary(t => t, t => Tier(t));

    private static (int column, int row) Tier(string technology)
    {
        string[][] columns =
        {
            new[] { null, "Reconstruction", null },
            new[] { "Shared Embers", "Elderwood Felling", "The Ruin-Song" },
            new[] { "Harvest Hymns", "Colonnade Salvage", "Staccato" },
            new[] { "Root-Digging", "Ash-Cellars", "Horology" },
            new[] { "Earth-Bean Rows", "Lookout Towers", "The Minor Note" },
            new[] { "Trapper's Patience", "Peat and Kiln", "Ostinato" },
            new[] { "Midwives' Lullaby", "The Rekindling", "Unison" },
            new[] { "Wild Honey Keeping", "Heartwood Joinery", "Knowledge Sanctums" },
            new[] { "Salt and Smoke", "Stonebound Walls", "Call and Response" },
            new[] { "Reading the Vital Winds", "Old World Roads", "The Seventh Degree" },
            new[] { "Songs of the Moon", "Chants of Ash", "The Stave of Stars" },
            new[] { "Moonlit Vigil", "Sky Glass Burials", "The Dual Confluence" },
            new[] { "Golden Orchard Belts", "Golden Ash", "Fermata" },
            new[] { "Unsustainable Growth", null, "Da Capo" },
            new[] { null, "Echoes of Hunger", null },
        };
        for (int c = 0; c < columns.Length; c++)
            for (int r = 0; r < 3; r++)
                if (columns[c][r] == technology) return (c, r);
        throw new KeyNotFoundException(technology);
    }

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
        Assert.AreEqual(TechVisibility.Preview, See("Elderwood Felling", researched), "one step past what can be researched shows, locked");
        Assert.AreEqual(TechVisibility.Preview, See("Shared Embers", researched));
        Assert.AreEqual(TechVisibility.Hidden, See("Harvest Hymns", researched), "two steps away stays hidden");
        Assert.AreEqual(TechVisibility.Hidden, See("Echoes of Hunger", researched));
    }

    [Test]
    public void ResearchUncoversTheTreeOneStepAtATime()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual(TechVisibility.Researched, See("Reconstruction", researched));
        Assert.AreEqual(TechVisibility.Available, See("Elderwood Felling", researched));
        Assert.AreEqual(TechVisibility.Preview, See("Harvest Hymns", researched));
        Assert.AreEqual(TechVisibility.Preview, See("Colonnade Salvage", researched), "one of its prerequisites can be researched now");
        Assert.AreEqual(TechVisibility.Hidden, See("Horology", researched), "its only prerequisite is itself locked");

        researched.UnionWith(new[] { "Elderwood Felling", "Shared Embers" });
        Assert.AreEqual(TechVisibility.Available, See("Harvest Hymns", researched), "every prerequisite researched");
        Assert.AreEqual(TechVisibility.Preview, See("Horology", researched));
    }

    [Test]
    public void AnEnlightenedTechnologyAndTheAgesGateShowBeforeTheirWay()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual(TechVisibility.Preview, See("Echoes of Hunger", researched, new[] { "Echoes of Hunger" }));
        Assert.AreEqual(TechVisibility.Preview, See("Echoes of Hunger", researched, revealed: "Echoes of Hunger"), "the Age waits for it, so it shows");
        Assert.AreEqual(TechVisibility.Hidden, See("Unsustainable Growth", researched, new[] { "Echoes of Hunger" }), "its way stays hidden");
        Assert.AreEqual(TechVisibility.Available, See("Shared Embers", researched, new[] { "Shared Embers" }), "enlightened and researchable reads as researchable");
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
            Route("Reconstruction", "Elderwood Felling"));
        CollectionAssert.AreEqual(new[] { P(GridX.SlotRight, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 0), P(GridX.SlotLeft, 2, GridY.Row, 0) },
            Route("Elderwood Felling", "Harvest Hymns"));
    }

    [Test]
    public void ALongLineTakesAFreeRowOrTheChannelBetweenRows()
    {
        // Two cells apart on a row whose middle cell is empty: the line runs straight along it.
        CollectionAssert.AreEqual(new[] { P(GridX.SlotRight, 1, GridY.Row, 1), P(GridX.GapAfter, 1, GridY.Row, 1), P(GridX.GapAfter, 2, GridY.Row, 1), P(GridX.SlotLeft, 3, GridY.Row, 1) },
            TechTreeRules.Route(1, 1, 3, 1, (column, row) => !(column == 2 && row == 1), 3));
        // Salt and Smoke to Unsustainable Growth: every cell between them on the Hearth row holds a technology, so the
        // channel below the row carries the line.
        CollectionAssert.AreEqual(new[]
        {
            P(GridX.SlotRight, 8, GridY.Row, 0), P(GridX.GapAfter, 8, GridY.Row, 0), P(GridX.GapAfter, 8, GridY.ChannelBelow, 0),
            P(GridX.GapAfter, 12, GridY.ChannelBelow, 0), P(GridX.GapAfter, 12, GridY.Row, 0), P(GridX.SlotLeft, 13, GridY.Row, 0)
        }, Route("Salt and Smoke", "Unsustainable Growth"));
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
                    for (int column = 0; column <= 14; column++)
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
        var plan = TechTreeRules.PlanTo("Lookout Towers", Prerequisites, researched.Contains);
        CollectionAssert.AreEqual(new[] { "Elderwood Felling", "The Ruin-Song", "Colonnade Salvage", "Ash-Cellars", "Lookout Towers" }, plan);
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
        var plan = TechTreeRules.PlanTo("Lookout Towers", Prerequisites, researched.Contains);
        var path = TechTreeRules.PlanTo("Horology", Prerequisites, researched.Contains);
        bool Available(string t) => TechTreeRules.IsAvailable(t, Prerequisites, researched.Contains);

        var replaced = TechTreeRules.Merge(plan, path, false, "Elderwood Felling", Available);
        CollectionAssert.AreEqual(new[] { "Elderwood Felling", "Shared Embers", "Harvest Hymns", "The Ruin-Song", "Staccato", "Horology" }, replaced);
        AssertPrerequisitesFirst(replaced, "replaced");

        var appended = TechTreeRules.Merge(plan, TechTreeRules.PlanTo("Root-Digging", Prerequisites, researched.Contains), true);
        CollectionAssert.AreEqual(new[] { "Elderwood Felling", "The Ruin-Song", "Colonnade Salvage", "Ash-Cellars", "Lookout Towers", "Shared Embers", "Harvest Hymns", "Root-Digging" }, appended);
        AssertPrerequisitesFirst(appended, "appended");
    }

    [Test]
    public void TakingATechnologyOutOfThePlanTakesWhatNeedsItToo()
    {
        var plan = new List<string> { "Elderwood Felling", "Shared Embers", "Harvest Hymns", "The Ruin-Song", "Staccato", "Horology", "Root-Digging" };
        CollectionAssert.AreEqual(new[] { "Elderwood Felling", "Shared Embers", "The Ruin-Song", "Staccato" }, TechTreeRules.Without(plan, "Harvest Hymns", Prerequisites));
        CollectionAssert.AreEqual(plan, TechTreeRules.Without(plan, "Knowledge Sanctums", Prerequisites), "a technology not planned changes nothing");
    }

    [Test]
    public void ThePlansNextResearchIsItsFirstResearchableTechnology()
    {
        var researched = new HashSet<string> { "Reconstruction" };
        Assert.AreEqual("Elderwood Felling", TechTreeRules.NextInPlan(new[] { "Harvest Hymns", "Elderwood Felling" }, Prerequisites, researched.Contains));
        Assert.IsNull(TechTreeRules.NextInPlan(new[] { "Horology" }, Prerequisites, researched.Contains));
        Assert.AreEqual(2, TechTreeRules.PlanPosition(new[] { "Elderwood Felling", "Horology" }, "horology"), "names match as the catalogs do, ignoring case");
        Assert.AreEqual(0, TechTreeRules.PlanPosition(new[] { "Elderwood Felling" }, "Horology"));
    }

    [Test]
    public void ActIHasFortyTechnologiesEndingAtEchoesOfHunger()
    {
        Assert.AreEqual(40, Needs.Count, "Age 0 has 120 technologies, 40 per Act");
        var needed = new HashSet<string>(Needs.Values.SelectMany(v => v));
        var sinks = Needs.Keys.Where(t => !needed.Contains(t)).OrderBy(t => t).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Echoes of Hunger", "Golden Ash", "Golden Orchard Belts" }, sinks,
            "every technology leads to Echoes of Hunger except the two optional temptations");
        var path = AgeRules.PathTo("Echoes of Hunger", Prerequisites);
        Assert.AreEqual(38, path.Count, "the Act's gate needs everything but the temptations");
        CollectionAssert.DoesNotContain(path, "Golden Orchard Belts");
        CollectionAssert.DoesNotContain(path, "Golden Ash");
    }

    [Test]
    public void EachLaneHoldsOneTechnologyPerTierAndPrerequisitesComeFromEarlierTiers()
    {
        Assert.AreEqual(Cells.Count, Cells.Values.Distinct().Count(), "one technology per cell");
        foreach (var entry in Needs)
            foreach (var before in entry.Value)
                Assert.Less(Cells[before].column, Cells[entry.Key].column, $"{before} comes before {entry.Key}");
    }

    [Test]
    public void AnOldNameFindsTheRenamedTechnology()
    {
        Assert.AreEqual("Lookout Towers", TechnologyAliases.Resolve("Pathfinder Training"));
        Assert.AreEqual("Ash-Cellars", TechnologyAliases.Resolve(" resource storage "), "names match as the catalogs do, ignoring case");
        Assert.AreEqual("Horology", TechnologyAliases.Resolve("Horology"), "a kept name is itself");
        foreach (var current in TechnologyAliases.Renamed.Values) Assert.IsTrue(Needs.ContainsKey(current), $"{current} is in the tree");
        foreach (var old in TechnologyAliases.Renamed.Keys) Assert.IsFalse(Needs.ContainsKey(old), $"{old} is no longer a name in the tree");
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
