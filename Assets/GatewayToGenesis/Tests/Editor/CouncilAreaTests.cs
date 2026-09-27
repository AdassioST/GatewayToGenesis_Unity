using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Council areas (<see cref="CouncilAreaRules"/>): stories call on seats by area, never by title, so any seat can be
/// added or renamed. The seat covering the area answers (its main charge first), else the closest related seat, else no
/// one (the caller turns to the Head of State, then the player). Pure: runs outside Unity on the code defaults.
/// </summary>
public class CouncilAreaTests
{
    private static readonly IReadOnlyList<CouncilArea> Areas = CouncilAreaRules.Defaults;

    private static CouncilAreaRules.SeatView Seat(string title, string holder, int order, params string[] areas) =>
        new CouncilAreaRules.SeatView { title = title, holder = holder, order = order, areas = areas };

    // The default council, as its assets tag it.
    private static List<CouncilAreaRules.SeatView> Council() => new List<CouncilAreaRules.SeatView>
    {
        Seat("High Arbiter", "Ada", 0, "justice", "defense", "diplomacy"),
        Seat("Oracle", "Bel", 1, "mysticism", "faith", "culture"),
        Seat("Grand Archivist", "Cyr", 2, "lore", "innovation", "governance"),
        Seat("Master of Craft", "Dov", 3, "industry", "innovation", "culture"),
        Seat("Treasurer", "Eli", 4, "economy", "logistics", "welfare"),
        Seat("Supreme Commander", "Fen", 5, "defense", "security", "exploration"),
    };

    [TestCase("defense", "defense")]
    [TestCase("Military", "defense")]
    [TestCase("judicial", "justice")]
    [TestCase("Diplomatic", "diplomacy")]
    [TestCase("Security", "security")]
    [TestCase("nonsense", null)]
    [TestCase("", null)]
    public void Resolve_ReadsIdsNamesAndAliases(string text, string id)
    {
        Assert.AreEqual(id, CouncilAreaRules.Resolve(text, Areas));
    }

    [Test]
    public void Distance_FollowsRelatedAreasBothWays()
    {
        Assert.AreEqual(0, CouncilAreaRules.Distance("defense", "military", Areas));
        Assert.AreEqual(1, CouncilAreaRules.Distance("defense", "security", Areas));
        Assert.AreEqual(1, CouncilAreaRules.Distance("security", "defense", Areas), "links work both ways");
        Assert.AreEqual(2, CouncilAreaRules.Distance("defense", "justice", Areas), "defense -> security -> justice");
        Assert.AreEqual(-1, CouncilAreaRules.Distance("defense", "nonsense", Areas));
    }

    [Test]
    public void Find_TheSeatWhoseMainChargeItIsWins()
    {
        // Both the High Arbiter and the Supreme Commander cover defense; it is the Commander's first charge.
        var answer = CouncilAreaRules.Find("defense", Council(), Areas, 2);
        Assert.AreEqual("Fen", answer.legend);
        Assert.AreEqual("Supreme Commander", answer.seat);
        Assert.AreEqual(0, answer.distance);
    }

    [Test]
    public void Find_AnotherSeatCoveringTheAreaAnswersWhenTheFirstIsNotFree()
    {
        var answer = CouncilAreaRules.Find("defense", Council(), Areas, 2, free: n => n != "Fen");
        Assert.AreEqual("High Arbiter", answer.seat, "the Commander is away: the Arbiter also covers defense");
        Assert.AreEqual(0, answer.distance);
    }

    [Test]
    public void Find_TheClosestRelatedSeatAnswersWhenNoSeatCoversTheArea()
    {
        var seats = Council().Where(s => !s.areas.Contains("defense")).ToList();
        var answer = CouncilAreaRules.Find("defense", seats, Areas, 2);
        Assert.AreEqual(1, answer.distance, "security and logistics sit next to defense");
        Assert.AreEqual("Treasurer", answer.seat, "logistics is the Treasurer's second charge");
        Assert.AreEqual("logistics", answer.area);
    }

    [Test]
    public void Find_TitlesDoNotMatterOnlyAreas()
    {
        var seats = new List<CouncilAreaRules.SeatView> { Seat("Warden of the Ninth Gate", "Gil", 7, "army") };
        var answer = CouncilAreaRules.Find("defense", seats, Areas, 2);
        Assert.AreEqual("Gil", answer.legend, "a new seat tagged with an alias answers under any title");
    }

    [Test]
    public void Find_NoOneBeyondReachEmptySeatsOrUnknownAreas()
    {
        var farAway = new List<CouncilAreaRules.SeatView> { Seat("Oracle", "Bel", 1, "mysticism") };
        Assert.IsFalse(CouncilAreaRules.Find("defense", farAway, Areas, 2).Found, "mysticism is more than two links from defense");
        Assert.IsFalse(CouncilAreaRules.Find("defense", Council(), Areas, 0, free: n => n != "Fen" && n != "Ada").Found, "0: exact areas only");
        var empty = new List<CouncilAreaRules.SeatView> { Seat("Supreme Commander", null, 5, "defense") };
        Assert.IsFalse(CouncilAreaRules.Find("defense", empty, Areas, 2).Found, "an empty seat answers for nothing");
        Assert.IsFalse(CouncilAreaRules.Find("nonsense", Council(), Areas, 2).Found);
    }

    [Test]
    public void TheDefaultAreasAreConsistent()
    {
        CollectionAssert.AllItemsAreUnique(Areas.Select(a => a.id));
        foreach (var area in Areas) CollectionAssert.IsEmpty(CouncilAreaRules.Unknown(area.related, Areas), $"{area.id}'s related areas");
        var aliases = Areas.SelectMany(a => a.aliases.Append(a.id)).ToList();
        CollectionAssert.AllItemsAreUnique(aliases, "an alias means one area");
    }
}
