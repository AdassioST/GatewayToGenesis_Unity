using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The vault import (<see cref="LoreNotes"/>, <see cref="LoreImport"/>): how notes are read, what counts as lore, and
/// what the import writes (the White-Haven Library's Library.json). Pure .NET with a temporary vault on disk, so they also
/// run outside Unity (except the JsonUtility round trip).
/// </summary>
public class LoreImportTests
{
    // ===== READING NOTES =====

    [Test]
    public void Parse_KeepsBlocksAsWrittenAndDropsTagsAndImages()
    {
        var blocks = LoreNotes.Parse("#chaos #society\n\n_\"An epigraph.\"_\n\n![[Art.png]]\n\nA paragraph\nwith two lines.\n\n### A Heading\n\n- one\n    \n- two\n    - nested\n\n|a|b|\n|---|---|\n|1|2|");
        Assert.AreEqual(new[] { LoreNotes.Kind.Paragraph, LoreNotes.Kind.Paragraph, LoreNotes.Kind.Heading, LoreNotes.Kind.List, LoreNotes.Kind.Table }, blocks.Select(b => b.kind).ToArray());
        Assert.AreEqual("A paragraph\nwith two lines.", blocks[1].text);
        StringAssert.Contains("- two", blocks[3].text, "blank lines between list items keep the list one block");
        Assert.AreEqual("A Heading", blocks[2].HeadingText);
        Assert.AreEqual(2, blocks[3].heading, "a block knows the heading it sits under");
    }

    [Test]
    public void Parse_TakesTheDescriptionLabelOff()
    {
        var blocks = LoreNotes.Parse("Description: *When destiny strikes a chord.*\n\nText.");
        Assert.AreEqual("*When destiny strikes a chord.*", blocks[0].text);
        Assert.IsTrue(blocks[0].described);
        LoreNotes.FindEpigraph(blocks, out string epigraph);
        Assert.AreEqual("*When destiny strikes a chord.*", epigraph);
    }

    [Test]
    public void Epigraph_IsTheShortItalicLineAtTheTopAndGetsItsItalicClosed()
    {
        var blocks = LoreNotes.MarkMeta(LoreNotes.Parse("#resource\n\n*\"Gleaming, velvet red crystal.\"\n\nBody."));
        var block = LoreNotes.FindEpigraph(blocks, out string text);
        Assert.IsNotNull(block);
        Assert.AreEqual("*\"Gleaming, velvet red crystal.\"*", text, "the italic the note leaves open is closed");
        Assert.IsNull(LoreNotes.FindEpigraph(LoreNotes.Parse("Plain first paragraph.\n\n_italic later_"), out _), "only a line at the top is an epigraph");
    }

    // ===== LORE OR NOT =====

    [Test]
    public void Meta_DesignNotesSectionsAndChatRepliesAreLeftOut()
    {
        var blocks = LoreNotes.MarkMeta(LoreNotes.Parse(
            "The Auric peach glows at dusk.\n\n" +
            "In game terms, golden ash is a soil modifier.\n\n" +
            "## Systemic Role (Gameplay Layer)\n\nIt always happens.\n\n### Key Beats\n\nStill part of it.\n\n" +
            "## Themes\n\nBack to the world.\n\n" +
            "Effects:\n\n- Increase the potency.\n\n" +
            "If desired, the next pass can focus on events.\n\n" +
            "Status:\n- [x] Time Tracking Implemented"));
        string Meta(string start) => blocks.First(b => b.text.StartsWith(start, StringComparison.Ordinal)).meta;
        Assert.IsNull(Meta("The Auric peach"));
        Assert.IsNotNull(Meta("In game terms"));
        Assert.IsNotNull(Meta("It always happens"), "everything under a heading about the game is left out");
        Assert.IsNotNull(Meta("Still part of it"), "down to its subsections");
        Assert.IsNull(Meta("Back to the world"), "the next heading of the same level ends it");
        Assert.IsNotNull(Meta("- Increase"), "a list introduced by \"Effects:\" is game data");
        Assert.IsNotNull(Meta("If desired"), "pasted chat replies are left out");
        Assert.IsNotNull(Meta("Status:"));
    }

    [Test]
    public void Meta_AListIntroducedByALeftOutParagraphGoesWithIt()
    {
        var blocks = LoreNotes.MarkMeta(LoreNotes.Parse("What changes between playthroughs is:\n\n- How deep the famine bites."));
        Assert.IsNotNull(blocks[1].meta);
    }

    [Test]
    public void Meta_InspiredByTheWorldIsLoreButInspiredByOtherWorksIsNot()
    {
        var terms = new[] { "Ages", "Sephira" };
        Assert.IsNull(LoreNotes.MetaReason("Inspired by the polyphonic practices of earlier Ages this ensemble is unique.", terms));
        Assert.IsNull(LoreNotes.MetaReason("A ceremony inspired by [[Sephira]], the fire.", terms));
        Assert.IsNull(LoreNotes.MetaReason("To hear outward is to be inspired by the impossible beauty of the beyond.", terms));
        Assert.IsNotNull(LoreNotes.MetaReason("His story is inspired by Galileo Galilei.", terms));
    }

    [Test]
    public void Meta_ATableIsJudgedByItsRowsNotItsHeader()
    {
        var blocks = LoreNotes.MarkMeta(LoreNotes.Parse("|State|Meaning in-world|\n|---|---|\n|**Golden**|Peak harmony.|"));
        Assert.IsNull(blocks[0].meta);
    }

    [Test]
    public void Meta_InWorldNamesThatLookLikeDesignWordsStay()
    {
        Assert.IsNull(LoreNotes.MetaReason("The Auric Codex was written by the Auric Aria."));
        Assert.IsNull(LoreNotes.MetaReason("First Auric Thread of Wave Mechanics."));
        Assert.IsNull(LoreNotes.MetaReason("Crucially, this is not famine in the cinematic sense."));
        Assert.IsNotNull(LoreNotes.MetaReason("The end of this group of Ages features the cinematic of breaching the hyperspace."));
    }

    // ===== SENTENCES =====

    [Test]
    public void Sentences_SplitOnlyWhereNothingIsLeftOpen()
    {
        var sentences = LoreNotes.Sentences("The first one. The [[Auric Aria]] sings. _An italic. Still italic._ Last, e.g. this.");
        Assert.AreEqual(new[] { "The first one.", "The [[Auric Aria]] sings.", "_An italic. Still italic._", "Last, e.g. this." }, sentences.ToArray());
    }

    [Test]
    public void Sentences_LineBreaksEndASentenceAndSpansKeepTheOriginalText()
    {
        string text = "- Magic in this age is mostly instinct:\n    - A spark coaxed from damp wood.";
        var spans = LoreNotes.SentenceSpans(text);
        Assert.AreEqual(2, spans.Count);
        Assert.AreEqual("- A spark coaxed from damp wood.", text.Substring(spans[1].start, spans[1].end - spans[1].start));
    }


    // ===== WRITING =====

    [Test]
    public void Json_EscapesWhatJsonMustAndKeepsTheRestAsWritten()
    {
        Assert.AreEqual("\"Say \\\"hi\\\"\\nnext \\\\ \u2014 \u00E9\"", LoreImport.Json("Say \"hi\"\r\nnext \\ \u2014 \u00E9"));
        Assert.AreEqual("\"\\u0001\"", LoreImport.Json("\u0001"));
        Assert.AreEqual("\"\"", LoreImport.Json(null));
    }

    // ===== THE WHOLE IMPORT =====

    private string _root;

    [SetUp]
    public void CreateVault()
    {
        _root = Path.Combine(Path.GetTempPath(), "g2g-lore-" + Guid.NewGuid().ToString("N"));
        Write("Vault/Worldbuilding/Origin of Magic/Magical Resources/Aetherlight.md",
            "#resource\n\n_The Force of Creation and Order._\n\nA golden god-ray caught in a [[Mirrorbox Trap]]. It pairs with [[Lunehymn]].\n\nIt is a core mechanic of the game.");
        Write("Vault/Worldbuilding/Origin of Magic/Magical Resources/Lunehymn.md", "_The Force of Repose and Vitality._\n\nA pale silvery liquid.");
        Write("Vault/Worldbuilding/Origin of Magic/Spellweaving/Empty.md", "");
        Write("Vault/Worldbuilding/Game Systems/Game Logic.md", "Design only.");
        Write("Vault/Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages of Magic/Ages 2/Age of Embers.md", "The embers of an age.");
        Write("Vault/Worldbuilding/Rise & Fall, Crisis/Crisis/Big Crisis.md", "_\"Epigraph.\"_\n\nThe crisis begins. It spreads.\n\n" +
            string.Concat(Enumerable.Range(1, 3).Select(i => $"## Part {i}\n\n" + string.Concat(Enumerable.Repeat($"Lore of part {i}, told at length. ", 90)) + "\n\n")));
        Write("Library/Vault~/Manifest.md",
            "exclude: Game Systems/\n\n## Aetherlight\naliases: Aether\nrelated: Lunehymn\n\n" +
            "## God-Ray\nnote: Aetherlight\ncategory: Light\nsummary: sentence \"A golden god-ray\"\n");
    }

    [TearDown]
    public void DeleteVault()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void Import_WritesTheLoreAndLeavesTheRestOut()
    {
        var result = Run();
        CollectionAssert.IsEmpty(result.problems);
        var aetherlight = result.keywords.Single(k => k.id == "aetherlight");
        Assert.AreEqual("_The Force of Creation and Order._", aetherlight.flavor);
        Assert.AreEqual("A golden god-ray caught in a [[Mirrorbox Trap]]. It pairs with [[Lunehymn]].", aetherlight.summary, "the summary is the lead of the first paragraph (all of a short one)");
        Assert.AreEqual(aetherlight.summary, aetherlight.body, "the article keeps the paragraph whole, once");
        Assert.IsTrue(aetherlight.cut.Any(c => c.Contains("core mechanic")), "the design sentence is left out, and the report says so");
        Assert.AreEqual("Magical Resource", aetherlight.category);
        Assert.AreEqual("Magic", aetherlight.group);

        Assert.IsFalse(result.keywords.Any(k => k.title == "Game Logic"), "excluded folders are not imported");
        Assert.IsFalse(result.keywords.Any(k => k.title == "Empty"), "empty notes are not imported");

        var godRay = result.keywords.Single(k => k.title == "God-Ray");
        Assert.AreEqual("god-ray", godRay.id);
        Assert.AreEqual("A golden god-ray caught in a [[Mirrorbox Trap]].", godRay.summary, "an entry from part of a note has only what it names");
        Assert.AreEqual(godRay.summary, godRay.body);
        Assert.IsTrue(string.IsNullOrEmpty(godRay.flavor));
    }

    [Test]
    public void Import_SplitsLongNotesIntoSectionsAndKeepsTheWholeNote()
    {
        var result = Run();
        var parent = result.keywords.Single(k => k.title == "Big Crisis");
        var sections = result.keywords.Where(k => k.parent == parent).ToList();
        Assert.AreEqual(3, sections.Count);
        CollectionAssert.AreEqual(sections.Select(s => s.id).OrderBy(s => s), parent.sections.Select(s => s.id).OrderBy(s => s));
        Assert.IsTrue(sections.All(s => s.isSection && s.id.StartsWith("big-crisis--part-", StringComparison.Ordinal)));
        Assert.AreEqual("Part 2", sections.Single(s => s.id == "big-crisis--part-2").section);
        StringAssert.StartsWith("Lore of part 2", sections.Single(s => s.id == "big-crisis--part-2").summary);
        Assert.AreEqual("The crisis begins. It spreads.", parent.summary);
        Assert.AreEqual("The crisis begins. It spreads.", parent.body, "the note keeps what comes before its sections");

        // The Library shows the whole note: its own text, then each section under its heading.
        var index = new LibraryIndex(result.library.entries);
        string article = index.Article("big-crisis");
        StringAssert.StartsWith("The crisis begins. It spreads.\n\n### Part 1\n\nLore of part 1", article);
        StringAssert.Contains("### Part 3\n\nLore of part 3", article);
    }

    [Test]
    public void Import_WritesTheLibraryWithResolvedLinksAndTheAges()
    {
        var result = Run();
        string json = Path.Combine(_root, "Library", "Library.json");
        Assert.IsTrue(File.Exists(json));
        Assert.IsTrue(result.written);
        StringAssert.Contains("\"id\": \"aetherlight\"", File.ReadAllText(json));

        var library = result.library;
        var aetherlight = library.entries.Single(e => e.id == "aetherlight");
        CollectionAssert.AreEqual(new[] { "lunehymn" }, aetherlight.links, "[[links]] and \"See also\" resolve to ids; a missing note is no link");
        CollectionAssert.Contains(aetherlight.aliases, "Aether");
        var crisis = library.entries.Single(e => e.id == "big-crisis");
        CollectionAssert.IsSubsetOf(new[] { "big-crisis--part-1", "big-crisis--part-2", "big-crisis--part-3" }, crisis.links);
        Assert.AreEqual("big-crisis", library.entries.Single(e => e.id == "big-crisis--part-1").parent);
        var age = library.ages.Single();
        Assert.AreEqual(("age-of-embers", "Age of Embers", 2), (age.id, age.title, age.number));

        // The game resolves names the same way.
        var index = new LibraryIndex(library.entries, library.ages);
        Assert.AreEqual("big-crisis--part-2", index.Resolve("Big Crisis#Part 2")?.id);
        Assert.AreEqual("aetherlight", index.Resolve("Aether")?.id);

        // A second run changes nothing; a note that disappears takes its entry and its links with it.
        Assert.IsFalse(Run().written);
        File.Delete(Path.Combine(_root, "Vault", "Worldbuilding", "Origin of Magic", "Magical Resources", "Lunehymn.md"));
        var again = Run();
        Assert.IsFalse(again.library.entries.Any(e => e.id == "lunehymn"));
        CollectionAssert.IsEmpty(again.library.entries.Single(e => e.id == "aetherlight").links);
        Assert.IsTrue(again.problems.Any(p => p.Contains("'Lunehymn', which is no entry")), "a \"See also\" that reaches nothing is reported");
        Assert.IsTrue(File.Exists(Path.Combine(_root, "Library", "Vault~", "ImportReport.md")));
    }

    // Needs Unity's JsonUtility, so it runs in the Test Runner only.
    [Test]
    public void Import_LibraryJsonReadsBackWithJsonUtility()
    {
        var result = Run();
        var file = UnityEngine.JsonUtility.FromJson<LibraryFile>(File.ReadAllText(Path.Combine(_root, "Library", "Library.json")));
        Assert.AreEqual(result.library.entries.Count, file.entries.Count);
        var aetherlight = file.entries.Single(e => e.id == "aetherlight");
        Assert.AreEqual("A golden god-ray caught in a [[Mirrorbox Trap]]. It pairs with [[Lunehymn]].", aetherlight.body);
        Assert.AreEqual(2, file.ages.Single().number);
    }

    private LoreImport.Result Run() => LoreImport.Run(new LoreImport.Settings
    {
        vaultRoot = Path.Combine(_root, "Vault"),
        libraryRoot = Path.Combine(_root, "Library"),
    });

    private void Write(string relative, string text)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }
}
