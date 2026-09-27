using System.Collections.Generic;
using System.Text;

/// <summary>
/// The tooltip palette and the rich-text building blocks every tooltip is written with, so all of them share
/// one high-contrast look on the near-black panel: ivory text and headings, golden keywords (nested tooltips),
/// soft purple for minor details, bright green and red for good and bad news. Keywords are linked by
/// <see cref="Keywords.Linkify"/> automatically; <see cref="Link"/> and <see cref="Expander"/> exist for code that
/// needs them explicitly. Free of Unity so wording stays testable.
/// </summary>
public static class TooltipText
{
    public const string GoodHex = "#93DB7F";
    public const string BadHex = "#FF7A68";
    /// <summary>Attention without judgement: totals, multipliers, pending states.</summary>
    public const string WarnHex = "#FFB070";
    /// <summary>Minor details: running totals, scopes, asides ("soft purple").</summary>
    public const string MutedHex = "#B9A5E0";
    /// <summary>Section headings: bright ivory small caps.</summary>
    public const string HeadingHex = "#F5E9D4";
    /// <summary>Numbers that matter in a sentence.</summary>
    public const string ValueHex = "#FFFFFF";
    /// <summary>A keyword: hover it in a solid tooltip to open its own tooltip.</summary>
    public const string LinkHex = "#F4C95D";

    /// <summary>Prefix of links that act on the tooltip itself ("show more") rather than open a keyword.</summary>
    public const string UiLinkPrefix = "ui:";

    /// <summary>Prefix of a link that opens the White-Haven Library at an entry ("ui:library:auric-peach").</summary>
    public const string LibraryLinkPrefix = UiLinkPrefix + "library:";

    // The game font (Chantelli Antiqua) draws ASCII, Latin-1 letters and curly quotes, but its bullet, dashes and
    // comparison signs are blank. Such symbols are drawn with TextMesh Pro's bundled font, found by name under
    // Resources/Fonts & Materials (KeywordMarkup.SafeGlyphs does this for any text).
    private const string SymbolFont = "LiberationSans SDF";

    /// <summary>Opens a run of text in the symbol font; <see cref="SymbolClose"/> ends it.</summary>
    public const string SymbolOpen = "<font=\"" + SymbolFont + "\">";
    public const string SymbolClose = "</font>";

    /// <summary><paramref name="glyphs"/> in a font that has them (bullets, dots, ≥, ×...).</summary>
    public static string Symbol(string glyphs) => SymbolOpen + glyphs + SymbolClose;

    /// <summary>A middle dot between two parts of a subtitle: "Idealism · Waltz Challenge".</summary>
    public static string Separator => " " + Symbol("·") + " ";

    public static string Good(string text) => Paint(text, GoodHex);
    public static string Bad(string text) => Paint(text, BadHex);
    public static string Warn(string text) => Paint(text, WarnHex);
    public static string Muted(string text) => Paint(text, MutedHex);
    public static string Value(string text) => Paint(text, ValueHex);

    /// <summary>Good or bad by <paramref name="good"/>.</summary>
    public static string Judge(string text, bool good) => good ? Good(text) : Bad(text);

    public static string Paint(string text, string hex) => string.IsNullOrEmpty(text) ? string.Empty : $"<color={hex}>{text}</color>";

    /// <summary>A small-caps section heading, optionally with a value on the right of the same line.</summary>
    public static string Heading(string text, string right = null)
    {
        string heading = $"<smallcaps><color={HeadingHex}>{text}</color></smallcaps>";
        return string.IsNullOrEmpty(right) ? heading : Row(heading, right);
    }

    /// <summary>Extra space between the wrapped lines of one row (TMP line spacing, hundredths of an em).</summary>
    public const float LineSpacing = 6f;
    /// <summary>Extra space between rows, i.e. after each line break (TMP paragraph spacing, hundredths of an em).</summary>
    public const float RowSpacing = 38f;

    /// <summary>One line with <paramref name="left"/> on the left and <paramref name="right"/> flush right.</summary>
    // TMP idiom: a line break that does not advance puts the right-aligned half on the same baseline. TMP still adds
    // line and paragraph spacing after an explicit line-height, so the tag subtracts exactly that much.
    public static string Row(string left, string right) =>
        $"<align=left>{left}{RowBreak}</line-height><align=right>{right}</align></align>";

    private static readonly string RowBreakTag =
        $"<line-height=-{((LineSpacing + RowSpacing) / 100f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}em>";

    /// <summary>Marks the inside of a <see cref="Row"/>, which is one logical line despite its line break.</summary>
    public static readonly string RowBreak = RowBreakTag + "\n";

    private const string BulletMark = "•</font></color><indent=";

    /// <summary>
    /// A bullet with a hanging indent, so wrapped lines stay aligned under the text. <paramref name="level"/> nests it
    /// (1 = under a bullet of level 0).
    /// </summary>
    public static string Bullet(string text, string bulletHex = MutedHex, int level = 0)
    {
        if (level <= 0) return $"<color={bulletHex}>{Symbol("•")}</color><indent=1em>{text}</indent>";
        return $"<color={bulletHex}><indent={level}em>{Symbol("•")}</color><indent={level + 1}em>{text}</indent></indent>";
    }

    /// <summary>True for a line made by <see cref="Bullet"/>, at any level.</summary>
    public static bool IsBullet(string line) => line != null && line.StartsWith("<color=", System.StringComparison.Ordinal) && line.Contains(BulletMark);

    /// <summary>A numbered list item ("1." as <paramref name="marker"/>) with a hanging indent.</summary>
    public static string Numbered(string marker, string text, int level = 0)
    {
        string open = level > 0 ? $"<indent={Em(level * 1.2f)}>" : string.Empty;
        string close = level > 0 ? "</indent>" : string.Empty;
        return $"{open}<color={MutedHex}>{marker}</color><indent={Em(level * 1.2f + 1.4f)}>{text}</indent>{close}";
    }

    // TMP sizes are read with a dot, whatever the player's culture.
    private static string Em(float value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "em";

    /// <summary>A quoted line, set in from the left.</summary>
    public static string Quote(string text) => $"<indent=1em>{text}</indent>";

    /// <summary>A signed number: "+12", "-3.5%". Coloured by whether the change is good for the player.</summary>
    public static string Signed(float value, string suffix = "", bool higherIsBetter = true)
    {
        string text = $"{(value > 0 ? "+" : "")}{value:0.##}{suffix}";
        if (value == 0f) return Value(text);
        return Judge(text, value > 0 == higherIsBetter);
    }

    /// <summary>A keyword that opens its own tooltip; <paramref name="id"/> is a keyword id (<see cref="Keywords.Resolve"/>).</summary>
    public static string Link(string id, string text) => $"<link=\"{id}\"><color={LinkHex}>{text}</color></link>";

    /// <summary>A clickable line that changes the tooltip itself ("+3 more", "More...").</summary>
    public static string Expander(string id, string text) => $"<link=\"{UiLinkPrefix}{id}\"><color={MutedHex}><u>{text}</u></color></link>";

    /// <summary>A clickable line that opens the White-Haven Library at entry <paramref name="entryId"/>.</summary>
    public static string LibraryLink(string entryId, string text) => $"<link=\"{LibraryLinkPrefix}{entryId}\"><color={LinkHex}><u>{text}</u></color></link>";

    /// <summary>Lines joined by newlines, skipping empty ones.</summary>
    public static string Lines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Event consequences as coloured bullets (good, bad or neutral), their running "New Total" in soft purple.
    /// <paramref name="appliedFirst"/> moves the totals without being listed (a choice's costs).
    /// </summary>
    public static string Consequences(IEnumerable<EventConsequence> consequences, IEnumerable<EventConsequence> appliedFirst = null)
    {
        var lines = new List<string>();
        foreach (var (text, tone) in EventText.ConsequenceLines(consequences, true, appliedFirst))
        {
            string body = text, total = string.Empty;
            int at = text.IndexOf(" (New Total", System.StringComparison.Ordinal);
            if (at >= 0)
            {
                body = text.Substring(0, at);
                total = " " + Muted(text.Substring(at + 1));
            }
            string painted = tone == ConsequenceTone.Good ? Good(body) : tone == ConsequenceTone.Bad ? Bad(body) : body;
            lines.Add(Bullet(painted + total, tone == ConsequenceTone.Good ? GoodHex : tone == ConsequenceTone.Bad ? BadHex : MutedHex));
        }
        return Lines(lines);
    }

    // ===== COLLAPSING =====

    /// <summary>
    /// The text as logical lines: a <see cref="Row"/> is one line even though it contains a line break.
    /// </summary>
    public static List<string> LogicalLines(string text)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        var parts = text.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            string line = parts[i];
            while (line.EndsWith(RowBreakTag, System.StringComparison.Ordinal) && i + 1 < parts.Length) line += "\n" + parts[++i];
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Shorten long lists so a tooltip is never a wall of text: any run of more than <paramref name="maxRun"/>
    /// bullets shows its first <paramref name="maxRun"/> - 1 and a clickable "+N more" line. Headings and other
    /// lines always stay, so no section disappears. A run whose id is in <paramref name="expanded"/> is shown in
    /// full, with a "Show less" line.
    /// </summary>
    /// <param name="blockId">Unique per text block in the tooltip; part of each expander's id.</param>
    public static string Collapse(string text, string blockId, ICollection<string> expanded, int maxRun, out bool hasExpanders)
    {
        hasExpanders = false;
        if (string.IsNullOrEmpty(text) || maxRun < 2) return text;
        var lines = LogicalLines(text);
        var output = new List<string>(lines.Count);
        int run = 0;
        for (int i = 0; i < lines.Count;)
        {
            if (!IsBullet(lines[i]))
            {
                output.Add(lines[i++]);
                continue;
            }
            int end = i;
            while (end < lines.Count && IsBullet(lines[end])) end++;
            int count = end - i;
            string id = $"more:{blockId}:{run++}";
            if (count <= maxRun)
            {
                for (int j = i; j < end; j++) output.Add(lines[j]);
            }
            else if (expanded != null && expanded.Contains(id))
            {
                for (int j = i; j < end; j++) output.Add(lines[j]);
                output.Add(Expander(id, "Show less"));
                hasExpanders = true;
            }
            else
            {
                for (int j = i; j < i + maxRun - 1; j++) output.Add(lines[j]);
                output.Add(Expander(id, $"+{count - (maxRun - 1)} more..."));
                hasExpanders = true;
            }
            i = end;
        }
        return string.Join("\n", output);
    }

    // ===== PAGES =====

    private static readonly System.Text.RegularExpressions.Regex Tags = new System.Text.RegularExpressions.Regex(@"<[^<>\n]*>");

    /// <summary>Length of <paramref name="text"/> as read, without its tags.</summary>
    public static int VisibleLength(string text) => string.IsNullOrEmpty(text) ? 0 : Tags.Replace(text, string.Empty).Length;

    /// <summary>
    /// Split a long text into pages of about <paramref name="pageChars"/> visible characters, breaking only between
    /// paragraphs (a paragraph longer than a page is split between its lines). A heading never ends a page: it moves
    /// to the next one, with the text it introduces. A text barely over one page stays whole.
    /// </summary>
    public static List<string> Pages(string text, int pageChars)
    {
        var pages = new List<string>();
        if (string.IsNullOrEmpty(text)) return pages;
        if (pageChars <= 0 || VisibleLength(text) <= pageChars * 5 / 4)
        {
            pages.Add(text);
            return pages;
        }

        // Paragraphs, and the lines of any paragraph too long for one page.
        var pieces = new List<(string text, bool paragraph)>();
        foreach (var paragraph in text.Split(new[] { "\n\n" }, System.StringSplitOptions.None))
        {
            if (VisibleLength(paragraph) <= pageChars)
            {
                pieces.Add((paragraph, true));
                continue;
            }
            bool first = true;
            foreach (var line in LogicalLines(paragraph))
            {
                pieces.Add((line, first));
                first = false;
            }
        }

        var current = new List<(string text, bool paragraph)>();
        int length = 0;
        foreach (var piece in pieces)
        {
            int size = VisibleLength(piece.text);
            if (current.Count > 0 && length + size > pageChars)
            {
                var carry = current[current.Count - 1];
                bool keepHeading = current.Count > 1 && IsHeading(carry.text);
                if (keepHeading) current.RemoveAt(current.Count - 1);
                pages.Add(JoinPieces(current));
                current.Clear();
                length = 0;
                if (keepHeading)
                {
                    current.Add((carry.text, true));
                    length = VisibleLength(carry.text);
                }
            }
            current.Add(piece);
            length += size;
        }
        if (current.Count > 0) pages.Add(JoinPieces(current));
        return pages;
    }

    private static bool IsHeading(string line) => line.StartsWith("<smallcaps>", System.StringComparison.Ordinal) && line.IndexOf('\n') < 0;

    private static string JoinPieces(List<(string text, bool paragraph)> pieces)
    {
        var sb = new StringBuilder();
        foreach (var (text, paragraph) in pieces)
        {
            if (sb.Length > 0) sb.Append(paragraph ? "\n\n" : "\n");
            sb.Append(text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// The page of <paramref name="text"/> to show, with a line to turn pages and to close it again ("Less"). The
    /// page lives in <paramref name="expanded"/> as "<paramref name="id"/>@2", "@3"...: "Next" adds the next page's
    /// id and "Previous" removes the current one, so the tooltip's ordinary expand clicks turn the pages.
    /// </summary>
    public static string Paged(string text, string id, ICollection<string> expanded, int pageChars)
    {
        var pages = Pages(text, pageChars);
        if (pages.Count <= 1) return text + "\n" + Expander(id, "Less");
        int page = 1;
        if (expanded != null)
        {
            while (page < pages.Count && expanded.Contains($"{id}@{page + 1}")) page++;
        }
        var navigation = new List<string>();
        if (page > 1) navigation.Add(Expander($"{id}@{page}", "Previous"));
        navigation.Add(Muted($"Page {page} of {pages.Count}"));
        if (page < pages.Count) navigation.Add(Expander($"{id}@{page + 1}", "Next"));
        navigation.Add(Expander(id, "Less"));
        return pages[page - 1] + "\n\n" + string.Join(Separator, navigation);
    }
}
