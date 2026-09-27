using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Ballad actors (<see cref="BalladActors"/>): who plays a story (a ballad's carried actors, the expedition that found
/// it, a named legend, the seat answering for its area, the Head of State, or no one yet), the roles fragment
/// consequences name, and what a ballad's finale pays. Pure: runs outside Unity.
/// </summary>
public class BalladActorTests
{
    // Everyone is free unless listed as away; "Fen" answers for defense.
    private static BalladActors.Stage Stage(string headOfState = "Hal", params string[] away) => new BalladActors.Stage
    {
        available = n => !away.Contains(n),
        answerFor = (area, free) => CouncilAreaRules.Find(area, new[]
        {
            new CouncilAreaRules.SeatView { title = "Supreme Commander", holder = "Fen", order = 5, areas = new[] { "defense", "security" } },
            new CouncilAreaRules.SeatView { title = "Treasurer", holder = "Eli", order = 4, areas = new[] { "economy", "logistics" } },
        }, CouncilAreaRules.Defaults, 2, free),
        holderOf = title => title == "Treasurer" ? "Eli" : null,
        headOfState = headOfState,
        balladCoProtagonists = 2,
    };

    private static BalladCast Party(string director, params string[] companions) =>
        new BalladCast { protagonist = director, coProtagonists = companions.ToList(), coSlots = companions.Length, how = "their expedition" };

    // ===== THE CAST TAG =====

    [Test]
    public void ParseCast_ReadsEveryEntry()
    {
        var problems = new List<string>();
        var spec = BalladActors.ParseCast("area:defense, co:1; legend:Vittoria Frauter, expedition, open", problems, "knot");
        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual("defense", spec.area);
        Assert.AreEqual("Vittoria Frauter", spec.legend);
        Assert.AreEqual(1, spec.co);
        Assert.IsTrue(spec.expedition);
        Assert.IsTrue(spec.open);
    }

    [Test]
    public void ParseCast_ReportsMistakes()
    {
        var problems = new List<string>();
        BalladActors.ParseCast("area:, co:many, heroes", problems, "knot");
        Assert.AreEqual(3, problems.Count, string.Join("\n", problems));
    }

    // ===== CASTING =====

    [Test]
    public void Resolve_TheAreaSeatPlaysTheStory()
    {
        var cast = BalladActors.Resolve(BalladActors.ParseCast("area:defense"), false, null, null, Stage());
        Assert.AreEqual("Fen", cast.protagonist, "a caravan at the gates is the defense's business");
        StringAssert.Contains("Supreme Commander", cast.how);
        Assert.AreEqual(0, cast.coSlots, "a lone event offers no co-protagonist role unless it asks");
    }

    [Test]
    public void Resolve_TheClosestSeatAnswersWhenTheAreaSeatIsAway()
    {
        var cast = BalladActors.Resolve(BalladActors.ParseCast("area:defense"), false, null, null, Stage("Hal", "Fen"));
        Assert.AreEqual("Eli", cast.protagonist, "logistics sits next to defense");
        StringAssert.Contains("closest to", cast.how);
    }

    [Test]
    public void Resolve_TheHeadOfStateWhenNoSeatAnswers()
    {
        var cast = BalladActors.Resolve(BalladActors.ParseCast("area:mysticism"), false, null, null, Stage());
        Assert.AreEqual("Hal", cast.protagonist);
        StringAssert.Contains("Head of State", cast.how);
        Assert.AreEqual("Hal", BalladActors.Resolve(default, false, null, null, Stage()).protagonist, "a story with no cast tag");
    }

    [Test]
    public void Resolve_NoOneWhenNoHeadOfStateOrTheStoryIsOpen()
    {
        Assert.IsTrue(BalladActors.Resolve(BalladActors.ParseCast("area:mysticism"), false, null, null, Stage(null)).Empty, "the player chooses");
        Assert.IsTrue(BalladActors.Resolve(BalladActors.ParseCast("open"), false, null, null, Stage()).Empty, "open: the Head of State is not called");
        Assert.IsTrue(BalladActors.Resolve(default, false, null, null, Stage("Hal", "Hal")).Empty, "the Head of State is away on the road");
    }

    [Test]
    public void Resolve_TheExpeditionThatFoundItPlaysIt()
    {
        var cast = BalladActors.Resolve(BalladActors.ParseCast("area:defense"), false, null, Party("Ivo", "Jun", "Kai"), Stage());
        Assert.AreEqual("Ivo", cast.protagonist, "the Director leads");
        CollectionAssert.AreEqual(new[] { "Jun", "Kai" }, cast.coProtagonists, "everyone who walked there is on stage");
    }

    [Test]
    public void Resolve_ABalladsActorsCarryFromVerseToVerse()
    {
        var carried = new BalladCast { protagonist = "Ivo", coProtagonists = new List<string> { "Jun" }, coSlots = 2, how = "their expedition" };
        var cast = BalladActors.Resolve(BalladActors.ParseCast("area:defense"), true, carried, Party("Zed"), Stage());
        Assert.AreEqual("Ivo", cast.protagonist, "the ballad's own actors come first");
        CollectionAssert.AreEqual(new[] { "Jun" }, cast.coProtagonists);
        Assert.AreEqual(2, cast.coSlots);
        var recast = BalladActors.Resolve(BalladActors.ParseCast("area:defense"), true, carried, null, Stage("Hal", "Ivo"));
        Assert.AreEqual("Fen", recast.protagonist, "a protagonist away on the road cannot carry the ballad");
        Assert.AreEqual(2, recast.coSlots, "a ballad offers its co-protagonist roles");
    }

    [Test]
    public void TheCastCanBeChangedByThePlayer()
    {
        var cast = Party("Ivo", "Jun");
        cast.coSlots = 2;
        BalladActors.SetProtagonist(cast, "Jun", "chosen");
        Assert.AreEqual("Jun", cast.protagonist);
        CollectionAssert.AreEqual(new[] { "Ivo" }, cast.coProtagonists, "the old lead takes the co-protagonist's place");
        Assert.IsTrue(BalladActors.AddCo(cast, "Kai"));
        Assert.IsFalse(BalladActors.AddCo(cast, "Lia"), "no role left");
        Assert.IsFalse(BalladActors.AddCo(new BalladCast { protagonist = "A", coSlots = 1 }, "A"), "never twice");
        Assert.IsTrue(BalladActors.Remove(cast, "Jun"));
        Assert.AreEqual("Ivo", cast.protagonist, "the first co-protagonist steps up");
        var candidates = BalladActors.Candidates(new[] { "Hal", "Fen" }, new[] { "Zed", "Ivo", "Abe" }, cast, n => n != "Fen");
        CollectionAssert.AreEqual(new[] { "Hal", "Abe", "Zed" }, candidates, "the council first, then the others by name; actors and the away left out");
    }

    // ===== ROLES IN CONSEQUENCES =====

    [Test]
    public void Targets_NameTheRolesLegends()
    {
        var cast = Party("Ivo", "Jun", "Kai");
        CollectionAssert.AreEqual(new[] { "Ivo" }, BalladActors.Targets("protagonist", cast, null));
        CollectionAssert.AreEqual(new[] { "Jun", "Kai" }, BalladActors.Targets("co", cast, null));
        CollectionAssert.AreEqual(new[] { "Ivo", "Jun", "Kai" }, BalladActors.Targets("cast", cast, null));
        CollectionAssert.AreEqual(new[] { "Hal", "Fen" }, BalladActors.Targets("council", cast, () => new List<string> { "Hal", "Fen" }));
        CollectionAssert.AreEqual(new[] { "Old" }, BalladActors.Targets("leader", null, null, "Old"), "the older form falls back to the last Director");
        CollectionAssert.AreEqual(new[] { "Vittoria Frauter" }, BalladActors.Targets("Vittoria Frauter", cast, null));
    }

    [TestCase("protagonist Lucidity", "protagonist", FragmentKind.Lucidity)]
    [TestCase("co Vision", "co", FragmentKind.Vision)]
    [TestCase("Vittoria Frauter Fragment of Rebirth", "Vittoria Frauter", FragmentKind.Rebirth)]
    [TestCase("council Cindergale", "council", FragmentKind.Defiance)]
    public void SplitTarget_ReadsWhoAndTheKind(string target, string who, FragmentKind kind)
    {
        Assert.IsTrue(BalladActors.SplitTarget(target, out var w, out var k));
        Assert.AreEqual(who, w);
        Assert.AreEqual(kind, k);
    }

    [Test]
    public void SplitTarget_NeedsAKind()
    {
        Assert.IsFalse(BalladActors.SplitTarget("protagonist", out _, out _));
        Assert.IsFalse(BalladActors.SplitTarget("Lucidity", out _, out _), "no one named");
    }

    // ===== BALLADS =====

    [Test]
    public void TheFinalePaysTheThemeToTheActors()
    {
        var tuning = new FragmentTuning();
        var rewards = BalladActors.BalladRewards("Scholar", Party("Ivo", "Jun"), tuning);
        Assert.AreEqual(2, rewards.Count);
        var lead = rewards[0].reward;
        Assert.AreEqual("Ivo", rewards[0].legend);
        Assert.AreEqual(5, lead.Single(a => a.kind == FragmentKind.Lucidity).amount, "Scholar: Lucidity x5");
        Assert.AreEqual(3, lead.Single(a => a.kind == FragmentKind.Rebirth).amount, "and Rebirth x3");
        var co = rewards[1].reward;
        Assert.AreEqual(3, co.Single(a => a.kind == FragmentKind.Lucidity).amount, "a co-protagonist's share (0.6)");
        Assert.AreEqual(2, co.Single(a => a.kind == FragmentKind.Rebirth).amount);
        CollectionAssert.IsEmpty(BalladActors.BalladRewards(null, Party("Ivo"), tuning), "no theme, no reward");
        CollectionAssert.IsEmpty(BalladActors.BalladRewards("Scholar", new BalladCast(), tuning), "no actors, no reward");
    }

    [Test]
    public void AThemedStoryPaysEveryActorALittleOfIt()
    {
        var reward = BalladActors.ThemedEventReward("Resistor", new FragmentTuning());
        Assert.AreEqual(FragmentKind.Defiance, reward.Single().kind);
        Assert.AreEqual(1, reward.Single().amount);
        Assert.AreEqual(FragmentKind.Vision, BalladActors.ThemedEventReward("Crystal", new FragmentTuning()).Single().kind, "a binding names its kind");
        CollectionAssert.IsEmpty(BalladActors.ThemedEventReward("", new FragmentTuning()));
    }

    [Test]
    public void TheFinaleIsTheHighestVerse()
    {
        var stories = new List<StoryNode>
        {
            new StoryNode { nodeName = "a", ballad = "ruin_song", verse = 1, theme = "Scholar", balladTitle = "The Ballad of the Ruin-Song" },
            new StoryNode { nodeName = "c", ballad = "ruin_song", verse = 3 },
            new StoryNode { nodeName = "b", ballad = "ruin_song", verse = 2 },
            new StoryNode { nodeName = "x", ballad = "other", verse = 9 },
        };
        Assert.AreEqual(3, BalladActors.FinaleVerse(stories, "ruin_song"));
        var (theme, title) = BalladActors.BalladInfo(stories, "ruin_song");
        Assert.AreEqual("Scholar", theme);
        Assert.AreEqual("The Ballad of the Ruin-Song", title);
    }
}
