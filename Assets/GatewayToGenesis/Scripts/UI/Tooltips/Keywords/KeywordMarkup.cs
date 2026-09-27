using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Turns authored text into tooltip rich text, free of Unity so it is testable:
/// <list type="bullet">
/// <item><c>[[Term]]</c>, <c>[[Term|shown words]]</c> and <c>[[Note#Section]]</c> become keyword links (the vault's
/// wikilink syntax, so notes can be pasted as they are). A term with no keyword yet shows as its plain words.</item>
/// <item>Known keyword words are linked automatically, outside markup and outside existing links.</item>
/// <item>The Markdown the vault's notes use: <c>_italic_</c>, <c>*italic*</c>, <c>**bold**</c>, headings, bullets
/// (nested by indentation), numbered lists, <c>&gt; quotes</c> and table rows. Image embeds, tag lines and
/// <c>%%comments%%</c> are dropped.</item>
/// <item>Characters the game font cannot draw are replaced or drawn with the symbol font (<see cref="SafeGlyphs"/>).</item>
/// </list>
/// </summary>
public static class KeywordMarkup
{
    // Tags (on one line), wikilinks, the text between them, and a lone bracket that starts neither.
    private static readonly Regex Tokens = new Regex(@"<[^<>\n]*>|\[\[[^\]\r\n]+\]\]|[^<\[]+|[<\[]", RegexOptions.Compiled);

    /// <summary>
    /// Link keywords in <paramref name="text"/>.
    /// </summary>
    /// <param name="resolve">Keyword id for a term written in <c>[[ ]]</c> (case-insensitive), or null when unknown.</param>
    /// <param name="autoTerms">Matches the words that link automatically; null for none.</param>
    /// <param name="autoId">Keyword id for a word <paramref name="autoTerms"/> matched.</param>
    /// <param name="selfId">The keyword being explained, which its own tooltip does not link to.</param>
    public static string Link(string text, Func<string, string> resolve, Regex autoTerms, Func<string, string> autoId, string selfId)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length + 64);
        int linkDepth = 0;
        foreach (Match token in Tokens.Matches(text))
        {
            string piece = token.Value;
            if (piece.Length > 1 && piece[0] == '<')
            {
                if (piece.StartsWith("<link", StringComparison.OrdinalIgnoreCase)) linkDepth++;
                else if (piece.StartsWith("</link", StringComparison.OrdinalIgnoreCase)) linkDepth = Math.Max(0, linkDepth - 1);
                sb.Append(piece);
            }
            else if (piece.StartsWith("[[", StringComparison.Ordinal))
            {
                ParseWikiLink(piece, out string target, out string section, out string shown);
                string id = null;
                if (linkDepth == 0 && resolve != null)
                {
                    // A section with a keyword of its own ("Note#Section") wins over the note.
                    if (section != null) id = resolve(target + "#" + section);
                    if (id == null) id = resolve(target);
                }
                sb.Append(id != null && !Same(id, selfId) ? TooltipText.Link(id, shown) : shown);
            }
            else if (linkDepth > 0 || autoTerms == null || piece.Length == 1 && (piece[0] == '<' || piece[0] == '['))
            {
                sb.Append(piece);
            }
            else
            {
                sb.Append(autoTerms.Replace(piece, m =>
                {
                    string id = autoId(m.Value);
                    return id == null || Same(id, selfId) ? m.Value : TooltipText.Link(id, m.Value);
                }));
            }
        }
        return sb.ToString();
    }

    /// <summary>"[[Waltz Pillar|Waltz]]" → target "Waltz Pillar", shown "Waltz".</summary>
    public static void ParseWikiLink(string link, out string target, out string shown) => ParseWikiLink(link, out target, out _, out shown);

    /// <summary>"[[Pillars#Axis 1|axis]]" → target "Pillars", section "Axis 1" (null when none), shown "axis".</summary>
    public static void ParseWikiLink(string link, out string target, out string section, out string shown)
    {
        string inner = link.Substring(2, link.Length - 4).Replace("\\|", "|");
        int bar = inner.IndexOf('|');
        target = (bar >= 0 ? inner.Substring(0, bar) : inner).Trim();
        shown = bar >= 0 ? inner.Substring(bar + 1).Trim() : target;
        section = null;
        // "[[Note#Section]]" shows as the note's name.
        int hash = target.IndexOf('#');
        if (hash >= 0)
        {
            section = target.Substring(hash + 1).Trim();
            if (section.Length == 0) section = null;
            target = target.Substring(0, hash).Trim();
            if (bar < 0) shown = target.Length > 0 ? target : section ?? string.Empty;
        }
        // A path ("Folder/Note") names the note.
        int slash = target.LastIndexOf('/');
        if (slash >= 0)
        {
            if (bar < 0 && shown == target) shown = target.Substring(slash + 1);
            target = target.Substring(slash + 1);
        }
    }

    /// <summary>Every <c>[[target]]</c> in <paramref name="text"/> (for validation); sections are dropped.</summary>
    public static IEnumerable<string> WikiTargets(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (Match match in WikiLink.Matches(text))
        {
            if (match.Index > 0 && text[match.Index - 1] == '!') continue; // an embed, not a link
            ParseWikiLink(match.Value, out string target, out _);
            if (target.Length > 0) yield return target;
        }
    }

    private static readonly Regex WikiLink = new Regex(@"\[\[[^\]\r\n]+\]\]", RegexOptions.Compiled);

    private static bool Same(string a, string b) => b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ===== MARKDOWN =====

    private static readonly Regex Bold = new Regex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*", RegexOptions.Compiled);
    private static readonly Regex ItalicUnderscore = new Regex(@"(?<![\w\[])_(?=\S)(.+?)(?<=\S)_(?![\w\]])", RegexOptions.Compiled);
    private static readonly Regex ItalicStar = new Regex(@"(?<![\w*])\*(?=\S)(.+?)(?<=\S)\*(?![\w*])", RegexOptions.Compiled);
    private static readonly Regex Highlight = new Regex(@"==(?=\S)(.+?)(?<=\S)==", RegexOptions.Compiled);
    private static readonly Regex Embed = new Regex(@"!\[\[[^\]\r\n]*\]\]", RegexOptions.Compiled);
    private static readonly Regex Comment = new Regex(@"%%.*?%%", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex HeadingLine = new Regex(@"^\s*(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex TagLine = new Regex(@"^\s*#[\w/-]+(\s+#[\w/-]+)*\s*$", RegexOptions.Compiled);
    private static readonly Regex BulletLine = new Regex(@"^([ \t]*)[-*+]\s+(?:\[[ xX]\]\s+)?(.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberLine = new Regex(@"^([ \t]*)(\d{1,3}[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex QuoteLine = new Regex(@"^\s*>\s?(?:\[![\w-]+\][+-]?\s*)?(.*)$", RegexOptions.Compiled);
    private static readonly Regex RuleLine = new Regex(@"^\s*([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TableRule = new Regex(@"^\s*\|?(\s*:?-{2,}:?\s*\|)+\s*:?-*:?\s*\|?\s*$", RegexOptions.Compiled);
    private static readonly Regex TableRow = new Regex(@"^\s*\|.*\|\s*$", RegexOptions.Compiled);
    // A "<" that cannot start a tag ("<10 trees"), which TMP must not read as one.
    private static readonly Regex StrayAngle = new Regex(@"<(?=[\d\s=<>]|$)", RegexOptions.Compiled);

    /// <summary>
    /// The Markdown authors paste from vault notes, as TMP rich text: headings, bullets (nested by indentation),
    /// numbered lists, quotes, table rows, bold and italic. Blank runs become one paragraph break, except between
    /// list items, which stay together. Other text (wikilinks included) passes through for <see cref="Link"/>.
    /// </summary>
    public static string FromMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;
        markdown = Comment.Replace(Embed.Replace(markdown, string.Empty), string.Empty);
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var output = new List<(string text, bool item)>(lines.Length);
        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.Trim().Length == 0 || RuleLine.IsMatch(line) || TableRule.IsMatch(line) || TagLine.IsMatch(line))
            {
                output.Add((string.Empty, false));
                continue;
            }
            Match m;
            if ((m = HeadingLine.Match(line)).Success)
            {
                output.Add((TooltipText.Heading(Inline(m.Groups[2].Value.Trim().TrimEnd('#').Trim())), false));
            }
            else if ((m = BulletLine.Match(line)).Success)
            {
                string item = m.Groups[2].Value.Trim();
                output.Add(item.Length == 0 ? (string.Empty, false) : (TooltipText.Bullet(Inline(item), TooltipText.MutedHex, Level(m.Groups[1].Value)), true));
            }
            else if ((m = NumberLine.Match(line)).Success)
            {
                output.Add((TooltipText.Numbered(m.Groups[2].Value, Inline(m.Groups[3].Value.Trim()), Level(m.Groups[1].Value)), true));
            }
            else if ((m = QuoteLine.Match(line)).Success)
            {
                string quote = m.Groups[1].Value.Trim();
                output.Add(quote.Length == 0 ? (string.Empty, false) : (TooltipText.Quote(Inline(quote)), true));
            }
            else if (TableRow.IsMatch(line))
            {
                output.Add((Inline(TableCells(line)), true));
            }
            else output.Add((Inline(line.Trim()), false));
        }

        // Paragraphs: a run of blank lines is one paragraph break; list items (and quote lines) stay together.
        var sb = new StringBuilder();
        bool blank = false, lastItem = false;
        foreach (var (text, item) in output)
        {
            if (text.Length == 0)
            {
                if (sb.Length > 0) blank = true;
                continue;
            }
            if (sb.Length > 0) sb.Append(blank && !(item && lastItem) ? "\n\n" : "\n");
            sb.Append(text);
            blank = false;
            lastItem = item;
        }
        return sb.ToString();
    }

    // Nesting depth from indentation: a tab or four spaces per level.
    private static int Level(string indent)
    {
        int tabs = 0, spaces = 0;
        foreach (char c in indent)
        {
            if (c == '\t') tabs++;
            else spaces++;
        }
        return tabs + (spaces >= 2 ? Math.Max(1, spaces / 4) : 0);
    }

    // "| **Golden** | Flesh gleams | Peak harmony |" → "**Golden** · Flesh gleams · Peak harmony".
    private static string TableCells(string row)
    {
        string inner = row.Trim();
        if (inner.StartsWith("|", StringComparison.Ordinal)) inner = inner.Substring(1);
        if (inner.EndsWith("|", StringComparison.Ordinal) && !inner.EndsWith("\\|", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1);
        // Split on bars that are neither escaped nor inside a wikilink.
        var cells = new List<string>();
        var cell = new StringBuilder();
        int depth = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '[' && i + 1 < inner.Length && inner[i + 1] == '[') { depth++; cell.Append("[["); i++; continue; }
            if (c == ']' && i + 1 < inner.Length && inner[i + 1] == ']') { depth = Math.Max(0, depth - 1); cell.Append("]]"); i++; continue; }
            if (c == '\\' && i + 1 < inner.Length && inner[i + 1] == '|') { cell.Append('|'); i++; continue; }
            if (c == '|' && depth == 0)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }
            cell.Append(c);
        }
        cells.Add(cell.ToString().Trim());
        cells.RemoveAll(string.IsNullOrEmpty);
        return string.Join(TooltipText.Separator, cells);
    }

    private static string Inline(string text)
    {
        text = StrayAngle.Replace(text, "<noparse><</noparse>");
        text = Bold.Replace(text, "<b>$1</b>");
        text = ItalicUnderscore.Replace(text, "<i>$1</i>");
        text = ItalicStar.Replace(text, "<i>$1</i>");
        return Highlight.Replace(text, "<b>$1</b>");
    }

    // ===== GLYPHS =====

    /// <summary>
    /// Make <paramref name="text"/> drawable. The game font (Chantelli Antiqua) has ASCII, the Latin-1 letters and
    /// curly quotes, but its dashes, ellipsis, dots and signs are blank and many characters are missing: the ellipsis
    /// becomes "...", the non-breaking hyphen "-", emoji are dropped, and every other character the font lacks is drawn
    /// with the symbol font (<see cref="TooltipText.Symbol"/>). Tags, and text already in another font, are left alone.
    /// </summary>
    public static string SafeGlyphs(string text)
    {
        if (string.IsNullOrEmpty(text) || !NeedsGlyphFix(text)) return text;
        var sb = new StringBuilder(text.Length + 32);
        int fontDepth = 0;
        bool symbolRun = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                int close = text.IndexOf('>', i + 1);
                int newline = text.IndexOf('\n', i + 1);
                if (close > i && (newline < 0 || close < newline))
                {
                    string tag = text.Substring(i, close - i + 1);
                    if (tag.StartsWith("<font", StringComparison.OrdinalIgnoreCase)) fontDepth++;
                    else if (tag.StartsWith("</font", StringComparison.OrdinalIgnoreCase)) fontDepth = Math.Max(0, fontDepth - 1);
                    EndRun(sb, ref symbolRun);
                    sb.Append(tag);
                    i = close;
                    continue;
                }
            }
            if (fontDepth > 0)
            {
                sb.Append(c);
                continue;
            }
            if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c) || c == '️' || c == '​' || c == '‍')
            {
                continue; // emoji and invisible joiners: decoration the fonts cannot draw
            }
            string replaced = Replacement(c);
            if (replaced != null)
            {
                EndRun(sb, ref symbolRun);
                sb.Append(replaced);
                continue;
            }
            if (GameFontDraws(c))
            {
                EndRun(sb, ref symbolRun);
                sb.Append(c);
                continue;
            }
            if (!symbolRun)
            {
                sb.Append(TooltipText.SymbolOpen);
                symbolRun = true;
            }
            sb.Append(c);
        }
        EndRun(sb, ref symbolRun);
        return sb.ToString();
    }

    private static void EndRun(StringBuilder sb, ref bool symbolRun)
    {
        if (!symbolRun) return;
        sb.Append(TooltipText.SymbolClose);
        symbolRun = false;
    }

    private static bool NeedsGlyphFix(string text)
    {
        foreach (char c in text) if (!GameFontDraws(c)) return true;
        return false;
    }

    private static string Replacement(char c)
    {
        switch (c)
        {
            case '…': return "...";
            case '‑': return "-";
            case ' ': return " ";
            case ' ':
            case ' ':
            case ' ': return " ";
            default: return null;
        }
    }

    /// <summary>True for characters Chantelli Antiqua draws (checked against its outlines).</summary>
    public static bool GameFontDraws(char c) =>
        (c >= ' ' && c <= '~') || c == '\n' || c == '\r' || c == '\t'
        || c == '‘' || c == '’' || c == '“' || c == '”' || c == '‐'
        || (c >= 'À' && c <= 'ÿ' && c != '×' && c != '÷')
        || c == 'Œ' || c == 'œ' || c == 'Š' || c == 'š' || c == 'Ÿ' || c == 'Ž' || c == 'ž';
}
