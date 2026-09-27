using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The two layers of lore, as on the Sonata website: the keyword masterfile (<see cref="KeywordMasterfile"/>: cards
/// staged by Age and civic) and the White-Haven Library (<see cref="LibraryIndex"/>, <see cref="LibraryArticles"/>).
/// Pure logic, so they also run outside Unity.
/// </summary>
public class LibraryTests
{
    // ===== THE KEYWORD MASTERFILE =====

    private const string Masterfile =
        "# Keyword Masterfile\n\nNotes for the author, above the first card.\n\n" +
        "## Auric Peach\nwiki: auric-peach\nalso: Golden Peach\n\n" +
        "@default\nThe fruit everyone eats.\n\n" +
        "@age-1\nThe fruit that once fed the world.\n\n" +
        "@age-of-embers\nA fruit remembered in the embers.\n\n" +
        "@civic-moonlit-vigil\nThe fruit the vigil keeps.\n\n" +
        "<!-- a note to the author -->\n" +
        "## Waltz\nid: pillar:waltz\nwiki: waltz-pillar\ncategory: Pillar of Civilization\nautolink: off\n\n@default\nThe pillar of the dance.\n\n" +
        "## Stub\nwiki: stub\n\n@default\nTODO — write the reader-facing description.\n\n" +
        "## Nothing\n\n@default\nTODO — nothing yet.\n";

    [Test]
    public void Masterfile_ReadsTheWebsiteFormatWithGameExtensions()
    {
        var file = KeywordMasterfile.Parse(Masterfile);
        CollectionAssert.IsEmpty(file.Problems);
        Assert.AreEqual(3, file.Cards.Count, "a stub keeps its binding; a card with nothing at all is dropped");

        var peach = file.Find("Auric Peach");
        Assert.AreEqual("auric-peach", peach.Id, "a card's id is its Library entry");
        Assert.AreSame(peach, file.Find("golden peach"), "also: spellings, any case");
        Assert.AreSame(peach, file.Find("Auric Peaches"), "plurals, as prose writes them");
        Assert.AreSame(peach, file.Find("The Auric Peach"));
        Assert.AreSame(peach, file.ForWiki("auric-peach"));
        Assert.AreEqual(4, peach.states.Count);
        Assert.AreEqual(KeywordStateKind.Default, peach.states[0].kind);

        var waltz = file.ById("pillar:waltz");
        Assert.AreEqual("Waltz", waltz.title);
        Assert.AreEqual("waltz-pillar", waltz.wiki);
        Assert.AreEqual("Pillar of Civilization", waltz.category);
        Assert.IsFalse(waltz.autoLink);

        var stub = file.Find("Stub");
        CollectionAssert.IsEmpty(stub.states, "a TODO state is never shown");
        Assert.AreEqual("from the Library", KeywordMasterfile.Reading(stub, new KeywordContext("age-of-desolation", 0), "from the Library"), "it falls back to the entry");
    }

    [Test]
    public void Masterfile_ReadingFollowsTheAgeAndTheCivicsInForce()
    {
        var peach = KeywordMasterfile.Parse(Masterfile).Find("Auric Peach");
        string Read(string age, int number, params string[] civics) => KeywordMasterfile.Reading(peach, new KeywordContext(age, number, new HashSet<string>(civics)));

        Assert.AreEqual("The fruit everyone eats.", Read("age-of-desolation", 0), "the default until a later state applies");
        Assert.AreEqual("The fruit that once fed the world.", Read("age-of-hymns", 1), "an @age-N state from that Age onward");
        Assert.AreEqual("The fruit that once fed the world.", Read("age-of-glyphs", 2));
        Assert.AreEqual("A fruit remembered in the embers.", Read("age-of-embers", 2), "an Age's own state only during that Age");
        Assert.AreEqual("The fruit the vigil keeps.", Read("age-of-desolation", 0, "moonlit-vigil"), "a civic in force wins over the Age");

        Assert.IsTrue(KeywordMasterfile.HasDeeper(peach, new KeywordContext("age-of-desolation", 0)), "a later Age revises it");
        Assert.IsFalse(KeywordMasterfile.HasDeeper(peach, new KeywordContext("age-of-hymns", 1), id => id == "age-of-embers" ? 1 : -1));
        Assert.IsTrue(KeywordMasterfile.HasDeeper(peach, new KeywordContext("age-of-hymns", 1), id => id == "age-of-embers" ? 2 : -1));
    }

    [Test]
    public void Masterfile_WrappedProseIsOneParagraphButListsAndQuotesKeepTheirLines()
    {
        var card = KeywordMasterfile.Parse("## Peach\n\n@default\nA fruit that\ncan take hold.\n\n- One crop,\n- One relic that runs\n  on and on,\n\n> “Have you forgotten\n> the peaches?”\n").Find("Peach");
        Assert.AreEqual("A fruit that can take hold.\n\n- One crop,\n- One relic that runs on and on,\n\n> “Have you forgotten\n> the peaches?”", card.states[0].text);
        Assert.AreEqual("**Fated**: above 90%\n- next", LibraryArticles.Unwrap("**Fated**: above 90%\n- next"), "bold at the start of a line is no list item");
    }

    [Test]
    public void Masterfile_TwoCardsClaimingOneWordIsAProblem()
    {
        var file = KeywordMasterfile.Parse("## Echo\n\n@default\nOne.\n\n## Resonant Echo\nalso: Echo\n\n@default\nTwo.\n");
        Assert.AreEqual(1, file.Problems.Count);
        Assert.AreEqual("Echo", file.Find("Echo").title, "the first keeps it");
        Assert.AreEqual(-1, KeywordMasterfile.AgeNumberOf("age-of-embers"));
        Assert.AreEqual(12, KeywordMasterfile.AgeNumberOf("age-12"));
    }

    // ===== THE WHITE-HAVEN LIBRARY =====

    private static LibraryIndex Index()
    {
        var entries = new List<LibraryEntry>
        {
            Entry("cycle", "Cycle", "Time", "The main unit to track time.", links: new[] { "cycle--cultural-facts", "seventh" }),
            new LibraryEntry { id = "cycle--cultural-facts", title = "Cultural Facts", shelf = "Time", parent = "cycle", section = "Cultural Facts", summary = "Every culture keeps it." },
            Entry("seventh", "Seventh", "Time", "The minimum scholarly unit.", links: new[] { "cycle" }),
            Entry("auric-peach", "Auric Peach", "Age of Desolation", "The primary food source of this era.", aliases: new[] { "Golden Peach" }),
            Entry("void", "Void", "Magic", "An element.", quiet: true),
            Entry("food", "Food", "Age of Desolation", "Lore of food."),
        };
        var game = LibraryArticles.Parse("# Game Wiki\n\n## Food\nshelf: Resources\ncategory: Vital Resource\nalso: Meals\n\nFeeds the [[Population]]; stored Food brings newcomers.\n\n### More\n\n- a list\n\n## Housing\nid: game-housing\n\nRoom for citizens.");
        return new LibraryIndex(entries.Concat(game), new[] { new LibraryAge { id = "age-of-desolation", title = "Age of Desolation", number = 0 } });
    }

    private static LibraryEntry Entry(string id, string title, string shelf, string summary, string[] links = null, string[] aliases = null, bool quiet = false)
    {
        var entry = new LibraryEntry { id = id, title = title, shelf = shelf, summary = summary, quiet = quiet };
        if (links != null) entry.links.AddRange(links);
        if (aliases != null) entry.aliases.AddRange(aliases);
        return entry;
    }

    [Test]
    public void Library_ResolvesNamesAsProseWritesThem()
    {
        var index = Index();
        Assert.AreEqual("auric-peach", index.Resolve("Auric Peaches")?.id);
        Assert.AreEqual("auric-peach", index.Resolve("golden peach")?.id);
        Assert.AreEqual("cycle", index.Resolve("Cycle's")?.id);
        Assert.AreEqual("cycle--cultural-facts", index.Resolve("Cycle#Cultural Facts")?.id, "a note's section");
        Assert.AreEqual("cycle", index.Resolve("Cycle#No Such Section")?.id, "an unknown section falls back to its note");
        Assert.IsNull(index.Resolve("Cultural Facts"), "a section's heading is no name of its own");
        Assert.AreEqual("seventh", index.Resolve("seventh")?.id, "an id is a name too");
    }

    [Test]
    public void Library_EachHalfResolvesItsOwnNamesFirst()
    {
        var index = Index();
        Assert.AreEqual("food", index.Resolve("Food")?.id, "the Glossary first");
        Assert.AreEqual("game-food", index.Resolve("Food", preferGame: true)?.id, "a Game Wiki article's links, its own half first");
        Assert.AreEqual("game-food", index.Resolve("Meals")?.id, "a name only one half has");
        Assert.AreEqual("game-food", index.OtherHalf(index.Get("food"))?.id);
        Assert.AreEqual("food", index.OtherHalf(index.Get("game-food"))?.id);
        Assert.IsNull(index.OtherHalf(index.Get("seventh")));
    }

    [Test]
    public void Library_WordsThatLightUpAreTheGlossarysOwnNamesOnly()
    {
        var words = Index().Words().Select(w => w.word).ToList();
        CollectionAssert.Contains(words, "Auric Peach");
        CollectionAssert.Contains(words, "Golden Peach");
        CollectionAssert.DoesNotContain(words, "Void", "a quiet entry is reached by [[links]] only");
        CollectionAssert.DoesNotContain(words, "Cultural Facts", "sections are reached through their note");
        CollectionAssert.DoesNotContain(words, "Meals", "Game Wiki entries are reached through cards and game terms");
    }

    [Test]
    public void Library_GraphShelvesAndSearch()
    {
        var index = Index();
        CollectionAssert.AreEqual(new[] { "cycle" }, index.Backlinks("seventh").Select(e => e.id));
        CollectionAssert.AreEqual(new[] { "cycle--cultural-facts" }, index.Sections("cycle").Select(e => e.id));
        var related = index.Related("cycle").Select(e => e.id).ToList();
        Assert.AreEqual("seventh", related.First(), "linked both ways comes first");
        CollectionAssert.DoesNotContain(related, "cycle--cultural-facts", "the note's own sections are not related reading");

        var shelves = index.Shelves(game: false);
        Assert.AreEqual(("Age of Desolation", 2), shelves.First(), "largest shelf first; sections are not counted");
        CollectionAssert.AreEqual(new[] { "Cycle", "Seventh" }, index.OnShelf("Time", game: false).Select(e => e.title));
        CollectionAssert.AreEqual(new[] { "Resources", "Unfiled" }, index.Shelves(game: true).Select(s => s.shelf).OrderBy(s => s));

        Assert.AreEqual("seventh", index.Search("seventh").First().id, "an exact title wins");
        Assert.AreEqual("auric-peach", index.Search("peach").First().id);
        Assert.AreEqual("seventh", index.Search("scholarly").Single().id, "summaries are searched too");
        CollectionAssert.IsEmpty(index.Search("s"), "search needs two letters");
        Assert.IsTrue(index.Search("food", game: true).All(e => e.game));
        Assert.AreEqual(0, index.Age("age-of-desolation").number);
    }

    [Test]
    public void GameWiki_ArticlesReadTheirMetadataAndSummary()
    {
        var articles = LibraryArticles.Parse("<!-- notes -->\n## Housing\nshelf: Settlement\ncategory: Game Rule\nalso: Homes, Room\n\nRoom for **citizens**, as [[Population|people]] need.\n\n- one\n");
        var housing = articles.Single();
        Assert.AreEqual("game-housing", housing.id);
        Assert.IsTrue(housing.game);
        Assert.AreEqual(("Settlement", "Game Rule"), (housing.shelf, housing.category));
        CollectionAssert.AreEqual(new[] { "Homes", "Room" }, housing.aliases);
        Assert.AreEqual("Room for **citizens**, as [[Population|people]] need.", housing.summary);
        StringAssert.EndsWith("- one", housing.body);
        Assert.AreEqual("Room for citizens, as people need.", LibraryArticles.Plain(housing.summary));
        Assert.AreEqual("auric-peach", LibraryIndex.Slug("Auric Peach"));
        Assert.AreEqual("the-ash-bread-winter", LibraryIndex.Slug("The Ash‑Bread Winter"));
        Assert.AreEqual("tathagata", LibraryIndex.Slug("Tathāgata"));
    }
}
