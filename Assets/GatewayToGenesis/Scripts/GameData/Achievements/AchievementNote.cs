using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>One achievement as the vault writes it (Worldbuilding/Events/Achievement.md). Text is verbatim, [[links]] included.</summary>
public class AchievementDefinition
{
    /// <summary>Stable id: the slug of the title without its link brackets ("a-league-of-legends").</summary>
    public string id;
    /// <summary>The section heading it sits under ("[[Stellar Legacy Score]]").</summary>
    public string category;
    /// <summary>What earns it ("Fill all the slots of a [[Civilization]]'s Government Council...").</summary>
    public string requirement;
    /// <summary>The part of the quote before the first ": " ("A League of [[Legend]]s").</summary>
    public string title;
    /// <summary>The rest of the quote ("And together, we are the League of [[Legend]]s!").</summary>
    public string flavor;
    /// <summary>Position in the note (0 = first).</summary>
    public int order;
}

/// <summary>
/// Reads the vault's achievement note. Its shape: <c>### Category</c> headings; a plain line saying what earns the
/// achievement; the next line an italic quote, <c>_"Title: flavor"_</c>. The text is kept exactly as written (the
/// canon is copied, never paraphrased). Lines that break the shape are reported as <see cref="Result.problems"/>, not
/// repaired: fix them in the vault and re-import. Pure, so it is tested without Unity.
/// </summary>
public static class AchievementNote
{
    public class Result
    {
        public readonly List<AchievementDefinition> achievements = new List<AchievementDefinition>();
        public readonly List<string> problems = new List<string>();
    }

    private static readonly Regex Heading = new Regex(@"^#{1,6}\s+(.+)$");
    private static readonly Regex Link = new Regex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]");

    public static Result Parse(string markdown)
    {
        var result = new Result();
        if (string.IsNullOrEmpty(markdown)) return result;

        string category = string.Empty, requirement = null;
        int requirementLine = 0, lineNumber = 0;
        var ids = new HashSet<string>();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var heading = Heading.Match(line);
            if (heading.Success)
            {
                if (requirement != null) result.problems.Add($"Line {requirementLine}: \"{requirement}\" has no title line under it.");
                category = heading.Groups[1].Value.Trim();
                requirement = null;
                continue;
            }
            if (line[0] == '#') continue; // tags (#mechanic #event)

            if (line[0] != '_' && line[0] != '*')
            {
                if (requirement != null) result.problems.Add($"Line {requirementLine}: \"{requirement}\" has no title line under it.");
                requirement = line;
                requirementLine = lineNumber;
                continue;
            }

            string quote = Unquote(line, out bool closed);
            if (!closed) result.problems.Add($"Line {lineNumber}: the italic quote is not closed (missing the final {line[0]}).");
            if (requirement == null)
            {
                result.problems.Add($"Line {lineNumber}: the quote \"{quote}\" has no requirement line above it, so it is left out.");
                continue;
            }

            int split = quote.IndexOf(": ", System.StringComparison.Ordinal);
            string title = (split >= 0 ? quote.Substring(0, split) : quote).Trim();
            string flavor = split >= 0 ? quote.Substring(split + 2).Trim() : string.Empty;
            if (split < 0) result.problems.Add($"Line {lineNumber}: \"{quote}\" has no \": \" between its title and its flavor.");

            string id = LibraryIndex.Slug(Plain(title));
            if (string.IsNullOrEmpty(id)) id = "achievement-" + result.achievements.Count;
            if (!ids.Add(id))
            {
                result.problems.Add($"Line {lineNumber}: a second achievement is titled \"{Plain(title)}\"; it gets the id '{id}-{result.achievements.Count}'.");
                id += "-" + result.achievements.Count;
                ids.Add(id);
            }
            result.achievements.Add(new AchievementDefinition
            {
                id = id,
                category = category,
                requirement = requirement,
                title = title,
                flavor = flavor,
                order = result.achievements.Count
            });
            requirement = null;
        }
        if (requirement != null) result.problems.Add($"Line {requirementLine}: \"{requirement}\" has no title line under it.");
        return result;
    }

    /// <summary>Text without link brackets: "[[Legend]]s" → "Legends", "[[A|B]]" → "B".</summary>
    public static string Plain(string text) => string.IsNullOrEmpty(text) ? string.Empty : Link.Replace(text, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value);

    // _"Title: flavor"_  →  Title: flavor   (also *...*, and quotes without the inner "...")
    private static string Unquote(string line, out bool closed)
    {
        char marker = line[0];
        closed = line.Length > 1 && line[line.Length - 1] == marker;
        string text = line.Substring(1, line.Length - (closed ? 2 : 1)).Trim();
        if (text.StartsWith("\"")) text = text.Substring(1);
        if (text.EndsWith("\"")) text = text.Substring(0, text.Length - 1);
        return text.Trim();
    }
}
