using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Reads the Game Wiki's hand-written articles (Resources/Library/GameWiki.md), free of Unity so it is testable.
/// The same shape as the keyword masterfile, with an article where the masterfile has states:
/// <code>
/// ## Housing
/// shelf: Settlement
/// category: Game Rule
/// also: Homes
///
/// Room for citizens. The first paragraph is the entry's summary...
///
/// ### Any Markdown the vault uses
/// </code>
/// <c>id:</c> sets the entry's id (default "game-" and the title's slug). Anything inside <c>&lt;!-- --&gt;</c> is
/// ignored. Every entry is a Game Wiki entry (<see cref="LibraryEntry.game"/>).
/// </summary>
public static class LibraryArticles
{
    public const string GameIdPrefix = "game-";

    private static readonly Regex Comment = new Regex(@"<!--[\s\S]*?-->", RegexOptions.Compiled);
    private static readonly Regex Heading = new Regex(@"^##[ \t]+", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MetaLine = new Regex(@"^\s*(shelf|category|also|id)\s*:\s*(.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WikiLink = new Regex(@"\[\[([^\]|\r\n]+)(?:\|([^\]\r\n]+))?\]\]", RegexOptions.Compiled);
    private static readonly Regex Emphasis = new Regex(@"(\*\*|__|\*|_|==)(?=\S)(.+?)(?<=\S)\1", RegexOptions.Compiled);

    /// <summary>The id of the Game Wiki entry titled <paramref name="title"/>: "game-housing".</summary>
    public static string GameId(string title) => GameIdPrefix + LibraryIndex.Slug(title);

    public static List<LibraryEntry> Parse(string markdown, string source = "GameWiki.md")
    {
        var entries = new List<LibraryEntry>();
        if (string.IsNullOrEmpty(markdown)) return entries;
        string text = Comment.Replace(markdown.Replace("\r\n", "\n"), string.Empty);
        var blocks = Heading.Split(text);
        for (int b = 1; b < blocks.Length; b++)
        {
            string block = blocks[b];
            int newline = block.IndexOf('\n');
            string title = (newline < 0 ? block : block.Substring(0, newline)).Trim();
            if (title.Length == 0) continue;
            var entry = new LibraryEntry { title = title, game = true, source = source };
            var lines = (newline < 0 ? string.Empty : block.Substring(newline + 1)).Split('\n');
            int at = 0;
            // Metadata lines open the entry; the article starts at the first other line.
            for (; at < lines.Length; at++)
            {
                if (lines[at].Trim().Length == 0) continue;
                var meta = MetaLine.Match(lines[at]);
                if (!meta.Success) break;
                string value = meta.Groups[2].Value.Trim();
                switch (meta.Groups[1].Value.ToLowerInvariant())
                {
                    case "shelf": entry.shelf = value; break;
                    case "category": entry.category = value; break;
                    case "id": entry.id = value; break;
                    default:
                        foreach (var alias in value.Split(',')) if (alias.Trim().Length > 0) entry.aliases.Add(alias.Trim());
                        break;
                }
            }
            entry.body = Unwrap(string.Join("\n", lines, at, lines.Length - at)).Trim();
            if (string.IsNullOrEmpty(entry.id)) entry.id = GameId(title);
            entry.summary = FirstParagraph(entry.body);
            entries.Add(entry);
        }
        return entries;
    }

    // A line that starts a block of its own: a list item, a quote, a table row or a heading.
    private static readonly Regex BlockLine = new Regex(@"^\s*([-*+]\s|[>|#]|\d{1,3}[.)]\s)", RegexOptions.Compiled);

    /// <summary>
    /// Hand-written files wrap their prose to keep lines short, as the website's masterfile does: a line break inside a
    /// paragraph is a space. List items, quotes, table rows and headings keep their own lines, and a line continuing
    /// a list item (indented) joins it. Blank lines still separate paragraphs. (Vault notes are not unwrapped: there a
    /// line break is meant.)
    /// </summary>
    public static string Unwrap(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new System.Text.StringBuilder(markdown.Length);
        bool previousJoins = false;
        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();
            bool blank = line.Trim().Length == 0;
            bool block = !blank && BlockLine.IsMatch(line);
            bool continuation = !blank && !block && previousJoins;
            if (sb.Length > 0) sb.Append(continuation ? " " : "\n");
            sb.Append(continuation ? line.Trim() : line);
            // Prose, and a list item's own text, can run on to the next line; a heading, a quote or a table row cannot.
            previousJoins = !blank && !Regex.IsMatch(line, @"^\s*([>|#])");
        }
        return sb.ToString();
    }

    /// <summary>The first paragraph of <paramref name="markdown"/> that is prose (not a heading, list or quote).</summary>
    public static string FirstParagraph(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        foreach (var paragraph in markdown.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = paragraph.Trim();
            if (trimmed.Length == 0) continue;
            char first = trimmed[0];
            if (first == '#' || first == '-' || first == '*' && !trimmed.StartsWith("**", StringComparison.Ordinal) || first == '>' || first == '|') continue;
            return trimmed;
        }
        return string.Empty;
    }

    /// <summary>Markdown as plain words: "[[Food|meals]] and **bold**" → "meals and bold". For search and lists.</summary>
    public static string Plain(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        string text = WikiLink.Replace(markdown, m => m.Groups[2].Success ? m.Groups[2].Value : TargetName(m.Groups[1].Value));
        for (int i = 0; i < 3; i++) text = Emphasis.Replace(text, "$2");
        return text.Replace("\n", " ").Trim();
    }

    // "Folder/Note#Section" → "Note".
    private static string TargetName(string target)
    {
        int hash = target.IndexOf('#');
        if (hash >= 0) target = hash > 0 ? target.Substring(0, hash) : target.Substring(1);
        int slash = target.LastIndexOf('/');
        return (slash >= 0 ? target.Substring(slash + 1) : target).Trim();
    }
}
