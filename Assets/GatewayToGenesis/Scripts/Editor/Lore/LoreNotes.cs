using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Reads a vault note (Obsidian Markdown) as blocks and tells lore from everything else, free of Unity so it is
/// testable. A block is a heading, a paragraph, a list, a table or a quote, kept exactly as written (the canon rule:
/// prose is copied, never paraphrased). Blocks that are not in-world text (design notes, status checklists, formulas,
/// pasted chat replies) are marked with the reason, so the import leaves them out and the report can show the cut.
/// </summary>
public static class LoreNotes
{
    public enum Kind { Heading, Paragraph, List, Table, Quote, Math, Status }

    public sealed class Block
    {
        public int index;
        public Kind kind;
        /// <summary>Heading level (1-6) for headings; 0 otherwise.</summary>
        public int level;
        /// <summary>The block as written in the note (images, tags and trailing spaces removed).</summary>
        public string text;
        /// <summary>Index of the heading this block sits under; -1 before the first heading.</summary>
        public int heading = -1;
        /// <summary>Why the block is not lore (it is left out), or null.</summary>
        public string meta;
        /// <summary>The note labelled it "Description:" (the label is not part of <see cref="text"/>).</summary>
        public bool described;

        /// <summary>The heading's words without Markdown ("## The [[Moon]]" → "The Moon").</summary>
        public string HeadingText => kind == Kind.Heading ? Plain(Regex.Replace(text, @"^\s*#+\s*", string.Empty)).Trim() : null;

        public override string ToString() => $"{index}:{kind}{(meta != null ? " (" + meta + ")" : "")} {Preview(text, 60)}";
    }

    // ===== PARSING =====

    private static readonly Regex HeadingLine = new Regex(@"^\s{0,3}(#{1,6})\s+\S");
    private static readonly Regex TagLine = new Regex(@"^\s*#[\w/-]+(\s+#[\w/-]+)*\s*$");
    private static readonly Regex ImageOnly = new Regex(@"^\s*(!\[\[[^\]]*\]\]\s*)+$");
    private static readonly Regex Image = new Regex(@"!\[\[[^\]\r\n]*\]\]");
    private static readonly Regex Rule = new Regex(@"^\s*([-*_])(\s*\1){2,}\s*$");
    private static readonly Regex ListLine = new Regex(@"^\s*([-*+]|\d{1,3}[.)])\s+");
    private static readonly Regex Checkbox = new Regex(@"^\s*[-*+]\s+\[[ xX]\]");
    private static readonly Regex StatusLabel = new Regex(@"^\s*\**\s*Status\s*:?\s*\**\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex Emoji = new Regex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]|[☀-➿]️?|️");

    /// <summary>The blocks of <paramref name="markdown"/>, in order.</summary>
    public static List<Block> Parse(string markdown)
    {
        var blocks = new List<Block>();
        if (string.IsNullOrEmpty(markdown)) return blocks;
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = 0;

        // YAML front matter.
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            int end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
            if (end > 0) i = end + 1;
        }

        var current = new List<string>();
        Kind currentKind = Kind.Paragraph;
        int heading = -1;

        void Flush()
        {
            if (current.Count == 0) return;
            string text = string.Join("\n", current).TrimEnd();
            current.Clear();
            bool described = currentKind == Kind.Paragraph && DescriptionLabel.IsMatch(text);
            if (described) text = DescriptionLabel.Replace(text, string.Empty, 1).Trim();
            if (text.Trim().Length == 0) return;
            blocks.Add(new Block { index = blocks.Count, kind = currentKind, text = text, heading = heading, described = described });
        }

        for (; i < lines.Length; i++)
        {
            string line = Emoji.Replace(lines[i], string.Empty).TrimEnd();

            // $$ formulas, kept whole so they can be left out whole.
            if (line.TrimStart().StartsWith("$$", StringComparison.Ordinal))
            {
                Flush();
                currentKind = Kind.Math;
                current.Add(line);
                bool closed = line.Trim().Length > 2 && line.Trim().EndsWith("$$", StringComparison.Ordinal);
                while (!closed && i + 1 < lines.Length)
                {
                    current.Add(lines[++i].TrimEnd());
                    closed = lines[i].TrimEnd().EndsWith("$$", StringComparison.Ordinal);
                }
                Flush();
                continue;
            }

            if (line.Trim().Length == 0)
            {
                // Lists often have blank (or whitespace-only) lines between their items: keep them one block.
                if (current.Count > 0 && (currentKind == Kind.List || currentKind == Kind.Status))
                {
                    int next = NextNonBlank(lines, i + 1);
                    if (next >= 0 && (ListLine.IsMatch(lines[next]) || IsIndented(lines[next]))) continue;
                }
                Flush();
                continue;
            }
            if (TagLine.IsMatch(line) || ImageOnly.IsMatch(line)) continue;
            if (Rule.IsMatch(line))
            {
                Flush();
                continue;
            }
            line = Image.Replace(line, string.Empty).TrimEnd();

            if (HeadingLine.IsMatch(line))
            {
                Flush();
                int level = line.TrimStart().TakeWhile(c => c == '#').Count();
                blocks.Add(new Block { index = blocks.Count, kind = Kind.Heading, level = level, text = line.Trim(), heading = heading });
                heading = blocks.Count - 1;
                blocks[heading].heading = heading;
                currentKind = Kind.Paragraph;
                continue;
            }

            if (StatusLabel.IsMatch(line))
            {
                Flush();
                currentKind = Kind.Status;
                current.Add(line);
                continue;
            }

            Kind kind = ListLine.IsMatch(line) ? (currentKind == Kind.Status && Checkbox.IsMatch(line) ? Kind.Status : Kind.List)
                : line.TrimStart().StartsWith(">", StringComparison.Ordinal) ? Kind.Quote
                : line.TrimStart().StartsWith("|", StringComparison.Ordinal) ? Kind.Table
                : Kind.Paragraph;

            // An indented line under a list item continues it.
            if (kind == Kind.Paragraph && current.Count > 0 && (currentKind == Kind.List || currentKind == Kind.Status) && IsIndented(line)) kind = currentKind;

            if (current.Count > 0 && kind != currentKind) Flush();
            currentKind = kind;
            current.Add(line);
        }
        Flush();
        return blocks;
    }

    private static int NextNonBlank(string[] lines, int from)
    {
        for (int j = from; j < lines.Length; j++) if (lines[j].Trim().Length > 0) return j;
        return -1;
    }

    private static bool IsIndented(string line) => line.StartsWith("\t", StringComparison.Ordinal) || line.StartsWith("  ", StringComparison.Ordinal);

    // ===== LORE OR NOT =====

    // Sections about the game rather than the world.
    private static readonly Regex MetaHeading = new Regex(
        @"\b(gameplay|game ?play|game|systemic|mechanic|mechanics|mechanical|implementation|how to use|specific changes|real[- ]world|realism|design|designer|balanc\w*|status|player|players|codex|tooltips?|UI|HUD|progression|backlog|to ?do|notes? for)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Words that only appear when a note talks about the game, its design, or other works.
    private static readonly (Regex pattern, string reason)[] MetaText =
    {
        (new Regex(@"\b(the player|players?|playthroughs?|gameplay|game ?loop|in[- ]game|in[-‑ ]world|(early|mid|late)[- ]game|game'?s|the game|this game|video ?games?|(?<!wave |quantum )mechanics?|mechanically|HUD|tooltips?|4X|clicker|incremental|snowball|DLC|story patch|hard mode|NPCs?|XP|save (file|state|data)|cutscenes?|cinematics?(?! sense)|fourth wall|UI)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "talks about the game"),
        (new Regex(@"\b(Gateway To Genesis|Third Actor)\b", RegexOptions.Compiled), "talks about the game"),
        (new Regex(@"\b(Terraria|Frieren|Elden Ring|Hollywood|Umamusume|Uma Musume|Ninja Scroll|Secrets of the Silent Witch|Pasteur|Aztec|Magical Girls?|Dark Souls|Bloodborne|Civilization VI|Crusader Kings|Stellaris)\b", RegexOptions.Compiled), "names a real-world work"),
        (new Regex(@"\b(inspired by|real[- ]life|real[- ]world|Earth (years?|months?|days?|hours?|minutes?|seconds?)|in real[- ]time|real minutes)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "real-world reference"),
        (new Regex(@"^\s*(>\s*)?(If you (want|like|imagine|prefer)|If desired|Below is|Here (are|is) (the|a|an) |Let me know|Would you like|As a final|Previously, the|We now|Now it is|\**Change:|\**Why it helps|Realism mirror)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline), "pasted chat reply"),
        (new Regex(@"\b(does this feel aligned|next pass|your canon|your Arcanoria|in your in-game)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "pasted chat reply"),
        (new Regex(@"\b(Implemented|Not Implemented)\b", RegexOptions.Compiled), "implementation status"),
    };

    // A label that introduces game data ("Effects:", "Responsibilities"), which the list after it inherits.
    private static readonly Regex MetaLabel = new Regex(@"^\s*\**\s*(Effects|Status|Responsibilities|Mechanics|Mechanically|In game terms|Game Effects|Unlocks?|Rewards?|Requirements?)\s*:?\s*\**\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Mark every block that is not lore: status checklists, formulas, sections about the game, and blocks whose words
    /// only appear in design notes or pasted chat replies. Returns <paramref name="blocks"/>. <paramref name="terms"/> are the
    /// vault's words (note names, link targets): "inspired by" one of them is lore, by anything else a design reference.
    /// </summary>
    public static List<Block> MarkMeta(List<Block> blocks, IReadOnlyCollection<string> terms = null)
    {
        // Sections: a meta heading takes everything under it, up to the next heading of its level or higher.
        int metaLevel = 0;
        string metaReason = null;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.kind == Kind.Heading)
            {
                if (metaLevel > 0 && block.level <= metaLevel)
                {
                    metaLevel = 0;
                    metaReason = null;
                }
                if (metaLevel == 0 && MetaHeading.IsMatch(block.HeadingText ?? string.Empty))
                {
                    metaLevel = block.level;
                    metaReason = $"section \"{block.HeadingText}\" is about the game";
                }
            }
            if (metaReason != null)
            {
                block.meta = metaReason;
                continue;
            }
            if (block.kind == Kind.Status) block.meta = "status checklist";
            else if (block.kind == Kind.Math) block.meta = "formula (a tooltip cannot show it)";
            else if (block.kind == Kind.List && Checkbox.IsMatch(block.text)) block.meta = "checklist";
            // A table's header row only names its columns ("Meaning in-world"): judge the rows.
            else if (block.kind == Kind.Table) block.meta = MetaReason(string.Join("\n", block.text.Split('\n').Skip(1)), terms);
            else if (block.kind != Kind.Heading) block.meta = MetaReason(block.text, terms);
        }

        // "Effects:" and the like pass their reason on to the list they introduce, and so does a left-out
        // paragraph that ends by introducing one ("What changes between playthroughs is:").
        for (int i = 0; i + 1 < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.kind != Kind.Paragraph) continue;
            bool label = MetaLabel.IsMatch(block.text);
            if (label) block.meta ??= "introduces game data";
            var next = blocks[i + 1];
            if (next.kind != Kind.List && next.kind != Kind.Table || next.meta != null) continue;
            if (label) next.meta = "game data (after \"" + block.text.Trim().Trim('*').Trim() + "\")";
            else if (block.meta != null && block.text.TrimEnd().EndsWith(":", StringComparison.Ordinal)) next.meta = "introduced by a passage left out";
        }
        // ...and the other way round: a lead-in ("The resets are the following:") whose list was left out.
        for (int i = 0; i + 1 < blocks.Count; i++)
        {
            var block = blocks[i];
            var next = blocks[i + 1];
            if (block.kind != Kind.Paragraph || block.meta != null || next.meta == null) continue;
            if ((next.kind == Kind.List || next.kind == Kind.Table) && block.text.TrimEnd().EndsWith(":", StringComparison.Ordinal)) block.meta = "introduces a passage left out";
        }
        return blocks;
    }

    /// <summary>Why <paramref name="text"/> reads as design or chat rather than lore, or null.</summary>
    public static string MetaReason(string text, IReadOnlyCollection<string> terms = null)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var (pattern, reason) in MetaText)
        {
            foreach (Match match in pattern.Matches(text))
            {
                if (match.Value.StartsWith("inspired by", StringComparison.OrdinalIgnoreCase) && InspiredByLore(text, match, terms)) continue;
                return $"{reason} (\"{match.Value.Trim()}\")";
            }
        }
        return null;
    }

    // "inspired by the polyphonic practices of earlier Ages", "to be inspired by the impossible beauty": someone in the
    // world is inspired, by something of the world. "His story is inspired by Galileo Galilei" is a design note.
    private static bool InspiredByLore(string text, Match match, IReadOnlyCollection<string> terms)
    {
        string before = text.Substring(Math.Max(0, match.Index - 8), Math.Min(8, match.Index));
        if (Regex.IsMatch(before, @"\b(be|being|feel|felt)\s+$", RegexOptions.IgnoreCase)) return true;
        int start = match.Index + match.Length;
        string after = text.Substring(start, Math.Min(90, text.Length - start));
        int stop = after.IndexOfAny(new[] { '.', ';', '\n' });
        if (stop > 0) after = after.Substring(0, stop);
        if (after.Contains("[[") || Regex.IsMatch(after, @"^\s*(this|these|that|those|her|his|their|its)\b", RegexOptions.IgnoreCase)) return true;
        return terms != null && terms.Any(term => term.Length >= 4 && Regex.IsMatch(after, @"\b" + Regex.Escape(term) + @"\b"));
    }

    // ===== EPIGRAPHS AND SENTENCES =====

    // "Description:" (or "**Description:**") at the start of a paragraph: a label, not part of the text.
    private static readonly Regex DescriptionLabel = new Regex(@"^\s*(?:\*\*)?Description(?:\*\*)?\s*:\s*(?:\*\*(?=\s))?\s*", RegexOptions.IgnoreCase);

    /// <summary>
    /// The note's epigraph: a short italic line at the top ("_The Force of Creation and Order._", a quote with its
    /// attribution, or what the note labels "Description:"), before the first heading and the first plain paragraph.
    /// Null when none. The text comes back with an italic the note leaves open closed.
    /// </summary>
    public static Block FindEpigraph(List<Block> blocks, out string text)
    {
        text = null;
        foreach (var block in blocks)
        {
            if (block.kind == Kind.Heading || block.meta != null) continue;
            if (block.kind != Kind.Paragraph && block.kind != Kind.Quote) return null;
            string candidate = block.text.Trim();
            if (candidate.StartsWith(">", StringComparison.Ordinal)) candidate = string.Join("\n", candidate.Split('\n').Select(l => l.TrimStart('>', ' ')));
            if (candidate.Length > 420 || candidate.Split('\n').Length > 3) return null;
            bool italic = candidate.StartsWith("_", StringComparison.Ordinal) || candidate.StartsWith("*", StringComparison.Ordinal) && !candidate.StartsWith("**", StringComparison.Ordinal);
            if (!block.described && !italic) return null;
            text = CloseItalic(candidate);
            return block;
        }
        return null;
    }

    /// <summary>"*text" (the note forgot the closing marker) → "*text*".</summary>
    public static string CloseItalic(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        char marker = text[0];
        if (marker != '_' && marker != '*') return text;
        int count = text.Count(c => c == marker);
        if (count % 2 == 0) return text;
        // Close before a trailing attribution (" — [[Someone]]") when there is one.
        int dash = text.LastIndexOf(" — ", StringComparison.Ordinal);
        if (dash < 0) dash = text.LastIndexOf(" - [[", StringComparison.Ordinal);
        return dash > 0 ? text.Substring(0, dash).TrimEnd() + marker + text.Substring(dash) : text.TrimEnd() + marker;
    }

    private static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?][""”’)*_]*)\s+(?=[\p{Lu}\[“""_*(])");
    private static readonly string[] Abbreviations = { "e.g.", "i.e.", "etc.", "vs.", "Mr.", "Mrs.", "Ms.", "Dr.", "St.", "No." };

    private static readonly Regex LineBreak = new Regex(@"[ \t]*\n\s*");

    /// <summary>
    /// <paramref name="paragraph"/> split into sentences (and at line breaks), only where the split cannot break a
    /// [[link]] or an italic or bold span.
    /// </summary>
    public static List<string> Sentences(string paragraph) => SentenceSpans(paragraph).Select(s => paragraph.Substring(s.start, s.end - s.start)).ToList();

    /// <summary>
    /// Where each sentence of <paramref name="paragraph"/> starts and ends (trimmed), so a selection can be cut from the
    /// paragraph itself, keeping its own spacing and line breaks.
    /// </summary>
    public static List<(int start, int end)> SentenceSpans(string paragraph)
    {
        var spans = new List<(int start, int end)>();
        if (string.IsNullOrEmpty(paragraph)) return spans;
        var cuts = SentenceEnd.Matches(paragraph).Cast<Match>().Select(m => (m.Index, m.Length, sentence: true))
            .Concat(LineBreak.Matches(paragraph).Cast<Match>().Select(m => (m.Index, m.Length, sentence: false)))
            .OrderBy(c => c.Index).ToList();
        int start = 0;
        foreach (var (index, length, sentence) in cuts)
        {
            if (index < start) continue;
            string before = paragraph.Substring(start, index - start);
            if (sentence && Abbreviations.Any(a => before.EndsWith(a, StringComparison.Ordinal))) continue;
            if (!Balanced(paragraph.Substring(0, index))) continue;
            Add(spans, paragraph, start, index);
            start = index + length;
        }
        Add(spans, paragraph, start, paragraph.Length);
        return spans;
    }

    private static void Add(List<(int start, int end)> spans, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        if (end > start) spans.Add((start, end));
    }

    // True when no link, bold or italic span is open at the end of <paramref name="text"/>.
    private static bool Balanced(string text)
    {
        int links = 0;
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] == '[' && text[i + 1] == '[') links++;
            if (text[i] == ']' && text[i + 1] == ']') links--;
        }
        if (links != 0) return false;
        int bold = Regex.Matches(text, @"\*\*").Count;
        if (bold % 2 != 0) return false;
        string noBold = text.Replace("**", string.Empty);
        int stars = noBold.Count(c => c == '*');
        int underscores = Regex.Matches(noBold, @"(?<![\w\[])_|_(?![\w\]])").Count;
        return stars % 2 == 0 && underscores % 2 == 0;
    }

    // ===== TEXT =====

    private static readonly Regex WikiLink = new Regex(@"\[\[([^\]|\r\n]+)(?:\|([^\]\r\n]+))?\]\]");

    /// <summary>What a reader sees: links as their shown words, no emphasis markers or heading marks.</summary>
    public static string Plain(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown ?? string.Empty;
        string text = WikiLink.Replace(markdown, m => m.Groups[2].Success ? m.Groups[2].Value : StripSection(m.Groups[1].Value));
        text = Regex.Replace(text, @"(\*\*|__|==)", string.Empty);
        text = Regex.Replace(text, @"(?<![\w])[_*](?=\S)|(?<=\S)[_*](?![\w])", string.Empty);
        return text;
    }

    private static string StripSection(string target)
    {
        int hash = target.IndexOf('#');
        string note = hash >= 0 ? target.Substring(0, hash) : target;
        int slash = note.LastIndexOf('/');
        return (slash >= 0 ? note.Substring(slash + 1) : note).Trim();
    }

    /// <summary>The first <paramref name="length"/> characters of the block's plain text, for reports.</summary>
    public static string Preview(string text, int length)
    {
        string plain = Plain(text ?? string.Empty).Replace('\n', ' ').Trim();
        plain = Regex.Replace(plain, @"\s+", " ");
        return plain.Length <= length ? plain : plain.Substring(0, length).TrimEnd() + "...";
    }

    /// <summary>Every [[link]] target in <paramref name="text"/>, without sections (embeds excluded).</summary>
    public static IEnumerable<string> LinkTargets(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (Match match in WikiLink.Matches(text))
        {
            if (match.Index > 0 && text[match.Index - 1] == '!') continue;
            string target = StripSection(match.Groups[1].Value.Replace("\\", string.Empty));
            if (target.Length > 0) yield return target;
        }
    }

    /// <summary>Text for a keyword field: blocks joined as paragraphs, every heading shown as a level-3 heading.</summary>
    public static string Compose(IEnumerable<string> pieces)
    {
        var sb = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (string.IsNullOrWhiteSpace(piece)) continue;
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(piece.Trim('\n'));
        }
        return sb.ToString();
    }

    /// <summary>A heading block as "### Heading".</summary>
    public static string AsSubheading(Block heading) => "### " + Regex.Replace(heading.text, @"^\s*#+\s*", string.Empty).Trim();
}
