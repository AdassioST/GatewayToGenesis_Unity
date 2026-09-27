using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Pure-logic tests for keyword markup and tooltip collapsing (<see cref="KeywordMarkup"/>,
/// <see cref="TooltipText.Collapse"/>): no scene or catalogs needed, so they also run outside Unity.
/// </summary>
public class KeywordTests
{
    private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Waltz", "pillar:waltz" }, { "Waltz Pillar", "pillar:waltz" }, { "Piety", "stat:piety" }, { "Critical Failure", "term:critical-failure" },
    };

    private static readonly Regex Auto = new Regex(@"(?<![\w'])(?:Critical Failure|Waltz|Piety)(?![\w'])");

    private static string Link(string text, string self = null) =>
        KeywordMarkup.Link(text, t => Known.TryGetValue(t, out var id) ? id : null, Auto, w => Known.TryGetValue(w, out var id) ? id : null, self);

    [Test]
    public void WikiLinks_ResolveShowTheirTextAndFallBackToPlainWords()
    {
        string text = Link("See [[Waltz Pillar|the Waltz]] and [[Humanity]].");
        StringAssert.Contains("<link=\"pillar:waltz\"><color=" + TooltipText.LinkHex + ">the Waltz</color></link>", text);
        StringAssert.Contains("and Humanity.", text, "a term without a keyword is shown as its plain words");
        StringAssert.DoesNotContain("[[", text);
    }

    [Test]
    public void WikiLinks_KeepSuffixesAndDropSectionAnchors()
    {
        StringAssert.Contains("Spellweavers", Link("[[Spellweaver]]s gather"));
        KeywordMarkup.ParseWikiLink("[[Pillars#Axis 1]]", out string target, out string shown);
        Assert.AreEqual("Pillars", target);
        Assert.AreEqual("Pillars", shown);
    }

    [Test]
    public void AutoLinks_SkipMarkupExistingLinksAndTheKeywordItself()
    {
        string text = Link("<color=#fff>Waltz</color> and Piety, <link=\"x\">Waltz</link>", self: "stat:piety");
        Assert.AreEqual(1, Regex.Matches(text, "pillar:waltz").Count, "text inside a tag's content links, an existing link does not");
        StringAssert.DoesNotContain("stat:piety", text, "a keyword's own tooltip does not link to itself");
        StringAssert.Contains("<color=#fff>", text);
    }

    [Test]
    public void AutoLinks_PreferTheLongestTermAndWholeWords()
    {
        string text = Link("A Critical Failure, not Waltzing.");
        StringAssert.Contains("term:critical-failure", text);
        StringAssert.DoesNotContain("pillar:waltz", text, "part of a longer word is not a keyword");
    }

    [Test]
    public void Markdown_ItalicBoldBulletsAndHeadings()
    {
        string text = KeywordMarkup.FromMarkdown("### Debates\n\n- a _quoted_ **line**\n\n\n\nplain");
        StringAssert.Contains("<smallcaps>", text);
        StringAssert.Contains("<i>quoted</i>", text);
        StringAssert.Contains("<b>line</b>", text);
        Assert.IsTrue(TooltipText.LogicalLines(text).Any(TooltipText.IsBullet), "\"- \" becomes a bullet");
        StringAssert.DoesNotContain("\n\n\n", text, "blank runs collapse to one paragraph break");
        Assert.AreEqual("[[Humanity]]'s_fate", KeywordMarkup.FromMarkdown("[[Humanity]]'s_fate"), "underscores inside words are not italics");
    }

    [Test]
    public void Markdown_VaultNotesNestedAndNumberedListsQuotesTablesAndTags()
    {
        string text = KeywordMarkup.FromMarkdown("#chaos #crisis\n![[Art.png]]\n- one\n    \n- two\n    - nested\n1. first\n> quoted\n|**Golden**|Peak harmony.|\n|---|---|\n%%hidden%%after");
        StringAssert.DoesNotContain("#chaos", text, "tag lines are dropped");
        StringAssert.DoesNotContain("Art.png", text, "image embeds are dropped");
        StringAssert.DoesNotContain("hidden", text, "comments are dropped");
        StringAssert.Contains("<indent=2em>nested", text, "an indented bullet is nested");
        StringAssert.Contains("1.</color>", text);
        StringAssert.Contains("<indent=1em>quoted</indent>", text);
        StringAssert.Contains("<b>Golden</b>", text);
        StringAssert.DoesNotContain("|", text, "a table row reads as its cells");
        StringAssert.DoesNotContain("one\n\n", text, "list items separated by blank lines stay together");
        Assert.AreEqual(3, TooltipText.LogicalLines(text).Count(TooltipText.IsBullet), "a nested bullet is a bullet too");
    }

    [Test]
    public void Markdown_AStrayAngleBracketIsNotATag()
    {
        StringAssert.Contains("<noparse><</noparse>10 trees", KeywordMarkup.FromMarkdown("- <10 trees: one bad season away"));
    }

    [Test]
    public void WikiLinks_ASectionWithAKeywordOfItsOwnWins()
    {
        var known = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { { "Cycle", "time:cycle" }, { "Cycle#Cultural Facts", "cycle#cultural facts" } };
        string Resolve(string t) => known.TryGetValue(t, out var id) ? id : null;
        StringAssert.Contains("<link=\"cycle#cultural facts\">", KeywordMarkup.Link("[[Cycle#Cultural Facts|facts]]", Resolve, null, null, null));
        StringAssert.Contains("<link=\"time:cycle\">", KeywordMarkup.Link("[[Cycle#No Such Section]]", Resolve, null, null, null), "an unknown section falls back to its note");
        KeywordMarkup.ParseWikiLink("[[Worldbuilding/Notes/Cycle]]", out string target, out string shown);
        Assert.AreEqual(("Cycle", "Cycle"), (target, shown), "a path names its note");
    }

    [Test]
    public void Glyphs_WhatTheGameFontCannotDrawIsReplacedOrDrawnWithTheSymbolFont()
    {
        string text = KeywordMarkup.SafeGlyphs("Ash‑Bread — “quoted” café… Tathāgata \U0001F300<color=#fff>x—y</color>" + TooltipText.Symbol("•"));
        StringAssert.Contains("Ash-Bread", text, "the non-breaking hyphen becomes a hyphen");
        StringAssert.Contains("café...", text, "the ellipsis becomes three dots; Latin-1 letters stay");
        StringAssert.Contains("“quoted”", text, "curly quotes are in the game font");
        StringAssert.Contains(TooltipText.Symbol("—"), text, "a dash is drawn with the symbol font");
        StringAssert.Contains("Tath" + TooltipText.Symbol("ā") + "gata", text);
        StringAssert.DoesNotContain("\U0001F300", text, "emoji are dropped");
        StringAssert.Contains("<color=#fff>x" + TooltipText.Symbol("—") + "y</color>", text, "tags are left alone");
        StringAssert.EndsWith(TooltipText.Symbol("•"), text, "text already in the symbol font is not wrapped twice");
        Assert.AreEqual("plain", KeywordMarkup.SafeGlyphs("plain"));
    }

    [Test]
    public void Pages_LongTextTurnsPagesBetweenParagraphsAndNeverEndsOnAHeading()
    {
        var paragraphs = Enumerable.Range(1, 12).Select(i => i == 6 ? TooltipText.Heading("Part Two") : $"Paragraph {i} " + new string('x', 150));
        string text = string.Join("\n\n", paragraphs);
        var pages = TooltipText.Pages(text, 500);
        Assert.Greater(pages.Count, 2);
        Assert.IsTrue(pages.All(p => !p.TrimEnd().EndsWith("</smallcaps>", System.StringComparison.Ordinal)), "a heading moves to the next page");
        Assert.AreEqual(text.Replace("\n\n", ""), string.Concat(pages).Replace("\n\n", ""), "nothing is lost or repeated");
        Assert.AreEqual(1, TooltipText.Pages("short", 500).Count);

        var expanded = new HashSet<string> { "details" };
        string first = TooltipText.Paged(text, "details", expanded, 500);
        StringAssert.Contains("Page 1 of", first);
        StringAssert.Contains("ui:details@2", first, "Next adds the next page");
        StringAssert.DoesNotContain("Previous", first);
        expanded.Add("details@2");
        string second = TooltipText.Paged(text, "details", expanded, 500);
        StringAssert.Contains("Page 2 of", second);
        StringAssert.Contains("<link=\"ui:details@2\"", second, "Previous removes the current page");
        StringAssert.Contains("ui:details\"", second, "and Less closes the details");
    }

    [Test]
    public void Collapse_ShortensLongListsButKeepsEveryHeading()
    {
        var lines = new List<string> { TooltipText.Heading("On Success", "60%") };
        for (int i = 0; i < 8; i++) lines.Add(TooltipText.Bullet($"good {i}"));
        lines.Add(TooltipText.Heading("On Failure", "40%"));
        lines.Add(TooltipText.Bullet("bad"));
        string text = TooltipText.Lines(lines);

        string collapsed = TooltipText.Collapse(text, "effects", new HashSet<string>(), 5, out bool hasExpanders);
        Assert.IsTrue(hasExpanders);
        StringAssert.Contains("good 3", collapsed);
        StringAssert.DoesNotContain("good 4", collapsed);
        StringAssert.Contains("+4 more", collapsed);
        StringAssert.Contains("On Failure", collapsed, "a later section is never hidden");
        StringAssert.Contains("bad", collapsed);

        string expanded = TooltipText.Collapse(text, "effects", new HashSet<string> { "more:effects:0" }, 5, out _);
        StringAssert.Contains("good 7", expanded);
        StringAssert.Contains("Show less", expanded);
    }

    [Test]
    public void LogicalLines_TreatARowAsOneLine()
    {
        string text = TooltipText.Lines(new[] { TooltipText.Row("Left", "Right"), "next" });
        var lines = TooltipText.LogicalLines(text);
        Assert.AreEqual(2, lines.Count);
        StringAssert.Contains("Right", lines[0]);
    }
}
