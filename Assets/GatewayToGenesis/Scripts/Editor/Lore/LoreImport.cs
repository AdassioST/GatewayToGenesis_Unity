using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Imports the Obsidian vault, the canon, as the Glossary of the White-Haven Library: every lore note becomes an
/// entry of Resources/Library/Library.json, its text copied from the note as written, never paraphrased. What goes in
/// is decided by Resources/Library/Vault~/Manifest.md (which passages, other names, "See also", notes to split), and
/// by <see cref="LoreNotes"/>, which leaves out whatever is not in-world text. Every run writes
/// Vault~/ImportReport.md, listing each entry and every passage left out, and why.
///
/// The Library is the whole of the lore; tooltips never show it whole. They show the keyword's card, written by
/// hand in Resources/Keywords/Keywords.md, and link here (<see cref="Keywords"/>), as the Sonata website's cards link
/// to its archive. Free of Unity, so it runs from the Editor menu (Tools > Gateway to Genesis > Import Lore From
/// Vault) and outside Unity alike.
/// </summary>
public static class LoreImport
{
    public const string OutputFile = "Library.json";
    /// <summary>Bumped when the shape of Library.json changes.</summary>
    public const int FormatVersion = 1;

    public sealed class Settings
    {
        /// <summary>The vault's root folder (the one holding Worldbuilding/).</summary>
        public string vaultRoot;
        /// <summary>Assets/Resources/Library.</summary>
        public string libraryRoot;
        /// <summary>Defaults to libraryRoot/Vault~/Manifest.md.</summary>
        public string manifestPath;
        /// <summary>Defaults to libraryRoot/Vault~/ImportReport.md.</summary>
        public string reportPath;
        /// <summary>False: build and report only, write nothing.</summary>
        public bool write = true;
        /// <summary>Words the game already makes keywords (resources, technologies, sections...), for the report.</summary>
        public IEnumerable<string> gameTerms;
    }

    /// <summary>One manifest entry: a Library entry and where its text comes from.</summary>
    public sealed class Entry
    {
        public string title, display, note, category, group;
        public readonly List<string> aliases = new List<string>();
        public bool? autoLink;
        /// <summary>Selectors (see the manifest's header); null means automatic.</summary>
        public string flavor, summary, details, skip;
        public readonly List<string> related = new List<string>();
        /// <summary>"sections", "none", "entries", "at ..." or null (automatic).</summary>
        public string split;
        public bool import = true;
        public int line;
    }

    /// <summary>An entry about to be written.</summary>
    public sealed class Keyword
    {
        public string id, title, category, group, flavor, summary, body, source;
        public readonly List<string> aliases = new List<string>();
        public bool autoLink = true, isSection;
        /// <summary>"See also": names of other entries.</summary>
        public readonly List<string> related = new List<string>();
        /// <summary>Report lines: passages left out and passages kept despite a warning.</summary>
        public readonly List<string> cut = new List<string>();
        public Keyword parent;
        /// <summary>A section's heading.</summary>
        public string section;
        /// <summary>A split note's sections, in the note's order.</summary>
        public readonly List<Keyword> sections = new List<Keyword>();
        public int Length => (flavor?.Length ?? 0) + (body?.Length ?? 0);
    }

    public sealed class Result
    {
        public readonly List<Keyword> keywords = new List<Keyword>();
        public readonly List<string> problems = new List<string>();
        public readonly List<string> skipped = new List<string>();
        /// <summary>What was written: every entry, and the Ages of the vault.</summary>
        public LibraryFile library;
        public bool written;
        public string report;
    }

    // ===== RUN =====

    public static Result Run(Settings settings)
    {
        var result = new Result();
        if (string.IsNullOrEmpty(settings.vaultRoot) || !Directory.Exists(settings.vaultRoot))
        {
            result.problems.Add($"Vault not found at '{settings.vaultRoot}'");
            return result;
        }
        string manifestPath = settings.manifestPath ?? Path.Combine(settings.libraryRoot, "Vault~", "Manifest.md");
        var manifest = File.Exists(manifestPath) ? ParseManifest(File.ReadAllText(manifestPath, Encoding.UTF8), result.problems) : new Manifest();
        if (!File.Exists(manifestPath)) result.problems.Add($"No manifest at {manifestPath}: every note is imported with the defaults");

        var vault = new Vault(settings.vaultRoot);
        var byNote = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.entries)
        {
            // An entry without "note:" is about the note of its title.
            string path = vault.Find(entry.note ?? entry.title);
            if (path == null)
            {
                result.problems.Add(entry.note != null
                    ? $"Manifest line {entry.line}: '{entry.title}' names note '{entry.note}', which is not in the vault"
                    : $"Manifest line {entry.line}: there is no note '{entry.title}' in the vault (add \"note:\")");
                continue;
            }
            entry.note = path;
            if (!byNote.TryGetValue(path, out var list)) byNote[path] = list = new List<Entry>();
            list.Add(entry);
        }

        // Every lore note, with its manifest entry when it has one, plus the manifest's entries taken from part of a note.
        foreach (var path in vault.Notes)
        {
            string title = Path.GetFileNameWithoutExtension(path);
            byNote.TryGetValue(path, out var entries);
            var own = entries?.FirstOrDefault(e => string.Equals(e.title, title, StringComparison.OrdinalIgnoreCase));
            if (own == null && manifest.Excludes(path))
            {
                result.skipped.Add($"{path}: excluded by the manifest");
                continue;
            }
            if (own != null && !own.import)
            {
                result.skipped.Add($"{path}: import: no");
                continue;
            }
            var note = vault.Load(path);
            if (note.blocks.Count == 0)
            {
                if (own != null || entries != null) result.problems.Add($"{path} is empty in the vault, so '{title}' has no text");
                else result.skipped.Add($"{path}: empty");
                if (own == null) continue;
            }
            Build(own ?? new Entry { title = title, note = path }, note, vault, result);
        }
        foreach (var entries in byNote.Values)
        {
            foreach (var entry in entries)
            {
                if (!entry.import) continue;
                string title = Path.GetFileNameWithoutExtension(entry.note);
                if (string.Equals(entry.title, title, StringComparison.OrdinalIgnoreCase)) continue; // built above
                Build(entry, vault.Load(entry.note), vault, result);
            }
        }

        CheckIds(result);
        result.library = ToLibrary(result, vault);
        string output = Path.Combine(settings.libraryRoot, OutputFile);
        if (settings.write)
        {
            Directory.CreateDirectory(settings.libraryRoot);
            string json = ToJson(result.library);
            result.written = !File.Exists(output) || File.ReadAllText(output, Encoding.UTF8) != json;
            if (result.written) File.WriteAllText(output, json, new UTF8Encoding(false));
        }
        result.report = Report(result, settings, manifestPath);
        if (settings.write)
        {
            string reportPath = settings.reportPath ?? Path.Combine(settings.libraryRoot, "Vault~", "ImportReport.md");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, result.report, new UTF8Encoding(false));
        }
        return result;
    }

    // ===== VAULT =====

    private sealed class Note
    {
        public string path, title;
        public List<LoreNotes.Block> blocks;
        public LoreNotes.Block epigraph;
        public string epigraphText;
    }

    private sealed class Vault
    {
        private readonly string _root;
        private readonly Dictionary<string, string> _byTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Note> _loaded = new Dictionary<string, Note>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Notes = new List<string>();

        public Vault(string root)
        {
            _root = root;
            string world = Path.Combine(root, "Worldbuilding");
            if (!Directory.Exists(world)) return;
            foreach (var file in Directory.GetFiles(world, "*.md", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string relative = Relative(file);
                Notes.Add(relative);
                string title = Path.GetFileNameWithoutExtension(file);
                if (!_byTitle.ContainsKey(title)) _byTitle[title] = relative;
            }
        }

        private HashSet<string> _terms;

        /// <summary>Every note name and link target in the vault: the words of the world.</summary>
        public IReadOnlyCollection<string> Terms
        {
            get
            {
                if (_terms != null) return _terms;
                _terms = new HashSet<string>(_byTitle.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (var note in Notes) foreach (var target in LoreNotes.LinkTargets(File.ReadAllText(Path.Combine(_root, note), Encoding.UTF8))) _terms.Add(target);
                return _terms;
            }
        }

        public string Relative(string file) => file.Substring(_root.Length).TrimStart('\\', '/').Replace('\\', '/');

        /// <summary>A note by path (from the vault root or from Worldbuilding/) or by title.</summary>
        public string Find(string note)
        {
            string clean = note.Trim().Replace('\\', '/');
            if (!clean.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) clean += ".md";
            foreach (var candidate in new[] { clean, "Worldbuilding/" + clean })
            {
                if (File.Exists(Path.Combine(_root, candidate))) return candidate;
            }
            return _byTitle.TryGetValue(Path.GetFileNameWithoutExtension(clean), out var path) ? path : null;
        }

        /// <summary>A note by path or title, or null.</summary>
        public Note LoadByName(string note)
        {
            string path = Find(note);
            return path == null ? null : Load(path);
        }

        /// <summary>The path of the loaded note <paramref name="block"/> belongs to.</summary>
        public string PathOf(LoreNotes.Block block) => _loaded.Values.FirstOrDefault(n => n.blocks.Contains(block))?.path;

        public Note Load(string relative)
        {
            if (_loaded.TryGetValue(relative, out var note)) return note;
            string text = File.ReadAllText(Path.Combine(_root, relative), Encoding.UTF8);
            var blocks = LoreNotes.MarkMeta(LoreNotes.Parse(text), Terms);
            note = new Note { path = relative, title = Path.GetFileNameWithoutExtension(relative), blocks = blocks };
            note.epigraph = LoreNotes.FindEpigraph(blocks, out note.epigraphText);
            _loaded[relative] = note;
            return note;
        }
    }

    // ===== MANIFEST =====

    private sealed class Manifest
    {
        public readonly List<Entry> entries = new List<Entry>();
        public readonly List<string> excludes = new List<string>();

        public bool Excludes(string path) => excludes.Any(e => path.StartsWith(e, StringComparison.OrdinalIgnoreCase) || path.StartsWith("Worldbuilding/" + e, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Regex Field = new Regex(@"^([a-z][a-z -]*):\s?(.*)$");

    private static Manifest ParseManifest(string text, List<string> problems)
    {
        var manifest = new Manifest();
        Entry entry = null;
        bool fenced = false;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                entry = new Entry { title = line.Substring(3).Trim(), line = i + 1 };
                manifest.entries.Add(entry);
                continue;
            }
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                entry = null; // a chapter heading: organisation only
                continue;
            }
            var match = Field.Match(line);
            if (!match.Success) continue; // prose: comments for the reader
            string key = match.Groups[1].Value.Trim(), value = match.Groups[2].Value.Trim();
            if (entry == null)
            {
                if (key == "exclude") manifest.excludes.AddRange(List(value).Select(v => v.Replace('\\', '/')));
                continue;
            }
            switch (key)
            {
                case "title": entry.display = value; break;
                case "note": entry.note = value; break;
                case "category": entry.category = value; break;
                case "group": entry.group = value; break;
                case "aliases": entry.aliases.AddRange(List(value)); break;
                case "autolink": entry.autoLink = !Regex.IsMatch(value, "^(off|no|false)$", RegexOptions.IgnoreCase); break;
                case "flavor": entry.flavor = value; break;
                case "summary": entry.summary = value; break;
                case "details": entry.details = value; break;
                case "skip": entry.skip = value; break;
                case "related": entry.related.AddRange(List(value)); break;
                case "split": entry.split = value.ToLowerInvariant(); break;
                case "import": entry.import = !Regex.IsMatch(value, "^(off|no|false)$", RegexOptions.IgnoreCase); break;
                default: problems.Add($"Manifest line {i + 1}: unknown field '{key}'"); break;
            }
        }
        return manifest;
    }

    private static IEnumerable<string> List(string value) => value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0);

    // ===== SELECTORS =====

    // A selected piece of a note: a whole block, or part of one (a line, a sentence, a paragraph's lead).
    private sealed class Piece
    {
        public LoreNotes.Block block;
        public string text;
        public bool whole;
        /// <summary>"auto": every passage of the note not otherwise used.</summary>
        public bool auto;
    }

    private static readonly Regex NoteToken = new Regex(@"\G\s*note\s+""(?<note>[^""]+)""\s*", RegexOptions.IgnoreCase);
    private static readonly Regex SelectorToken = new Regex(
        @"\G\s*(?:(?<word>auto|none|all|epigraph)\b|(?<kind>section|sentences|sentence|line)\s+""(?<a>[^""]*)""(?:\s*\.\.\s*""(?<b>[^""]*)"")?|""(?<a>[^""]*)""(?:\s*\.\.\s*(?:""(?<b>[^""]*)"")?(?<range>))?)\s*(?:\+|$)",
        RegexOptions.IgnoreCase);
    private static readonly Regex ListMarker = new Regex(@"^\s*([-*+]|\d{1,3}[.)])\s+");

    /// <summary>
    /// The pieces <paramref name="selectors"/> picks: <c>auto</c>, <c>none</c>, <c>all</c>, <c>epigraph</c>,
    /// <c>"text"</c> (the block containing it), <c>"a" .. "b"</c> (the blocks from one to the other),
    /// <c>"a" ..</c> (to the end of that block's section), <c>section "Heading"</c>, <c>line "text"</c>,
    /// <c>sentence "text"</c>, <c>sentences "text"</c> (the lead of that paragraph), joined with <c>+</c>.
    /// <c>note "Title"</c> before a selector takes it from another note. <c>text: words</c> is literal.
    /// Ranges and sections leave out what is not lore; a block named on its own is kept whatever it is.
    /// Null for <c>auto</c>.
    /// </summary>
    private static List<Piece> Select(string selectors, Note note, Vault vault, string owner, List<string> problems)
    {
        if (selectors == null) return null;
        string value = selectors.Trim();
        if (value.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Piece> { new Piece { text = value.Substring(5).Trim() } };
        }
        var pieces = new List<Piece>();
        var from = note;
        int at = 0;
        while (at < value.Length)
        {
            var other = NoteToken.Match(value, at);
            if (other.Success)
            {
                at += other.Length;
                from = vault.LoadByName(other.Groups["note"].Value);
                if (from == null)
                {
                    problems.Add($"{owner}: no note \"{other.Groups["note"].Value}\" in the vault");
                    return pieces;
                }
                continue;
            }
            var m = SelectorToken.Match(value, at);
            if (!m.Success || m.Length == 0)
            {
                problems.Add($"{owner}: cannot read selector '{value.Substring(at)}'");
                break;
            }
            at += m.Length;
            string word = m.Groups["word"].Value.ToLowerInvariant();
            string kind = m.Groups["kind"].Value.ToLowerInvariant();
            string a = m.Groups["a"].Value;
            if (word == "auto")
            {
                if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) return null;
                pieces.Add(new Piece { auto = true });
                continue;
            }
            if (word == "none") continue;
            if (word == "all")
            {
                pieces.AddRange(from.blocks.Where(b => b.meta == null && b != from.epigraph).Select(Whole));
                continue;
            }
            if (word == "epigraph")
            {
                if (from.epigraph != null) pieces.Add(new Piece { block = from.epigraph, text = from.epigraphText, whole = true });
                else problems.Add($"{owner}: '{from.title}' has no epigraph");
                continue;
            }
            if (kind == "section")
            {
                var heading = from.blocks.FirstOrDefault(b => b.kind == LoreNotes.Kind.Heading && Contains(b.HeadingText, a));
                if (heading == null) problems.Add($"{owner}: no heading \"{a}\" in '{from.title}'");
                else pieces.AddRange(SectionOf(from, heading).Where(b => b.meta == null || b == heading).Select(Whole));
                continue;
            }
            var start = FindBlock(from, a, 0);
            if (start == null)
            {
                problems.Add($"{owner}: no passage \"{a}\" in '{from.title}'");
                continue;
            }
            if (kind == "line")
            {
                // One line of a list, table or paragraph, without its list marker.
                string line = start.text.Split('\n').FirstOrDefault(l => Contains(l, a));
                string text = line != null ? ListMarker.Replace(line, string.Empty).Trim() : start.text;
                pieces.Add(new Piece { block = start, text = text, whole = line == null || line.Trim() == start.text.Trim() });
                continue;
            }
            if (kind == "sentence")
            {
                // One sentence, or with "..", the sentences from one to another, of the same block.
                string block = start.text;
                var spans = LoreNotes.SentenceSpans(block);
                string Span(int i) => block.Substring(spans[i].start, spans[i].end - spans[i].start);
                int first = spans.FindIndex(s => Contains(block.Substring(s.start, s.end - s.start), a));
                int last = first;
                if (first >= 0 && m.Groups["b"].Success && m.Groups["b"].Value.Length > 0)
                {
                    last = Enumerable.Range(first, spans.Count - first).Where(i => Contains(Span(i), m.Groups["b"].Value)).DefaultIfEmpty(-1).First();
                    if (last < 0) problems.Add($"{owner}: no sentence \"{m.Groups["b"].Value}\" after \"{a}\" in '{from.title}'");
                }
                if (first < 0 || last < 0)
                {
                    if (first < 0) problems.Add($"{owner}: \"{a}\" is not within one sentence in '{from.title}'");
                    continue;
                }
                // Cut from the block itself, so its own spacing stays; a list item loses its marker.
                string text = ListMarker.Replace(block.Substring(spans[first].start, spans[last].end - spans[first].start), string.Empty);
                pieces.Add(new Piece { block = start, text = text, whole = text == block.Trim() });
                continue;
            }
            if (kind == "sentences")
            {
                pieces.Add(Lead(start, out _));
                continue;
            }
            if (!m.Groups["range"].Success)
            {
                pieces.Add(Whole(start));
                continue;
            }
            LoreNotes.Block end;
            if (m.Groups["b"].Success && m.Groups["b"].Value.Length > 0)
            {
                end = FindBlock(from, m.Groups["b"].Value, start.index);
                if (end == null)
                {
                    problems.Add($"{owner}: no passage \"{m.Groups["b"].Value}\" after \"{a}\" in '{from.title}'");
                    continue;
                }
            }
            else end = EndOfSection(from, start);
            for (int i = start.index; i <= end.index; i++)
            {
                var block = from.blocks[i];
                if (block.meta == null || block == start || block == end) pieces.Add(Whole(block));
            }
        }
        return pieces;
    }

    private static Piece Whole(LoreNotes.Block block) => new Piece { block = block, text = block.text, whole = true };

    private static bool Contains(string haystack, string needle) =>
        haystack != null && (haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || LoreNotes.Plain(haystack).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);

    private static LoreNotes.Block FindBlock(Note note, string text, int from)
    {
        for (int i = from; i < note.blocks.Count; i++) if (Contains(note.blocks[i].text, text)) return note.blocks[i];
        return null;
    }

    // A heading and everything under it, up to the next heading of its level or higher.
    private static IEnumerable<LoreNotes.Block> SectionOf(Note note, LoreNotes.Block heading)
    {
        yield return heading;
        for (int i = heading.index + 1; i < note.blocks.Count; i++)
        {
            var block = note.blocks[i];
            if (block.kind == LoreNotes.Kind.Heading && block.level <= heading.level) yield break;
            yield return block;
        }
    }

    private static LoreNotes.Block EndOfSection(Note note, LoreNotes.Block start)
    {
        int level = start.heading >= 0 ? note.blocks[start.heading].level : 0;
        var last = start;
        for (int i = start.index + 1; i < note.blocks.Count; i++)
        {
            var block = note.blocks[i];
            if (block.kind == LoreNotes.Kind.Heading && (level == 0 || block.level <= level)) break;
            last = block;
        }
        return last;
    }

    /// <summary>A summary-sized lead of a paragraph (one to a few sentences); the rest comes back in <paramref name="rest"/>.</summary>
    private static Piece Lead(LoreNotes.Block block, out string rest)
    {
        rest = null;
        string text = block.text.Trim();
        if (block.kind != LoreNotes.Kind.Paragraph || text.Length <= 380) return new Piece { block = block, text = text, whole = true };
        // Whole sentences until the lead is long enough (140) or the next would make it too long (380).
        var spans = LoreNotes.SentenceSpans(text);
        int used = 0, end = 0;
        foreach (var span in spans)
        {
            if (used > 0 && (end >= 140 || span.end > 380)) break;
            end = span.end;
            used++;
        }
        if (used >= spans.Count) return new Piece { block = block, text = text, whole = true };
        rest = text.Substring(spans[used].start);
        return new Piece { block = block, text = text.Substring(0, end), whole = false };
    }

    // ===== BUILD =====

    private static readonly HashSet<string> Elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Void", "Crystal", "Luminance", "Flux", "Cindergale", "Strand", "Resonance" };

    // Single words too common to light up by themselves in every tooltip; [[links]] still reach them.
    private static readonly HashSet<string> QuietWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Void", "Crystal", "Flux", "Echo", "Phase", "Cycle", "Moon", "Dance", "Beat", "Legend", "Humanity", "Consciousness",
        "Coherence", "Composure", "Corruption", "Dissonance", "Resonance", "Luminance", "Strand", "Unison", "Ornament",
        "Religion", "Faith", "Events", "Resources", "Civilization", "Capital", "District", "Achievement", "Armament",
        "Ballad", "Constellation", "Stardust", "Consonance", "Institute", "Enclave", "Leylines", "Pillars", "Civic",
        "Ages", "Arcanoria", "Atrocity", "Scrap", "Kay", "Leaf", "Seraph", "Moon Phase", "Rite", "Bar", "Pulse", "Dyad",
    };

    private static readonly (string prefix, string group, string category)[] Folders =
    {
        ("Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages of Magic/", "Ages", "Age"),
        ("Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages.md", "Ages", "Passage of Time"),
        ("Worldbuilding/Rise & Fall, Crisis/Passage of Time/", "Time", "Passage of Time"),
        ("Worldbuilding/Rise & Fall, Crisis/Crisis/Crisis Objects/", "Crises", "Crisis Object"),
        ("Worldbuilding/Rise & Fall, Crisis/Crisis/", "Crises", "Age Crisis"),
        ("Worldbuilding/Rise & Fall, Crisis/", "World", "World"),
        ("Worldbuilding/Origin of Magic/Bindings & Elements/", "Magic", "Binding"),
        ("Worldbuilding/Origin of Magic/Celestial Objects/", "Cosmos", "Celestial Object"),
        ("Worldbuilding/Origin of Magic/Conduits of Magic/", "Magic", "Conduit of Magic"),
        ("Worldbuilding/Origin of Magic/Magical Resources/", "Magic", "Magical Resource"),
        ("Worldbuilding/Origin of Magic/Root of Evil/", "Chaos", "Root of Evil"),
        ("Worldbuilding/Origin of Magic/Spellweaving/", "Magic", "Spellweaving"),
        ("Worldbuilding/Origin of Magic/The One Symphony/", "Cosmos", "The One Symphony"),
        ("Worldbuilding/Origin of Magic/", "Magic", "Magic"),
        ("Worldbuilding/Mythology/Deities/", "Mythology", "Deity"),
        ("Worldbuilding/Mythology/", "Mythology", "Mythos"),
        ("Worldbuilding/Society/Auric Order/", "Society", "Auric Order"),
        ("Worldbuilding/Society/Characters/", "Legends", "Legend"),
        ("Worldbuilding/Society/Societal Resources/Faith/", "Society", "Faith"),
        ("Worldbuilding/Society/Societal Resources/Foundation/", "Society", "Foundation"),
        ("Worldbuilding/Society/Societal Resources/Places/", "Society", "Settlement"),
        ("Worldbuilding/Society/Societal Resources/Power/", "Society", "Power"),
        ("Worldbuilding/Society/Stellar Legacy/Personality Traits/", "Legends", "Legend Trait"),
        ("Worldbuilding/Society/Stellar Legacy/", "Legends", "Stellar Legacy"),
        ("Worldbuilding/Society/Tools & Inventions/", "Society", "Invention"),
        ("Worldbuilding/Society/Weapons, Instruments & Relics/Primary Instruments/", "Relics", "Primary Instrument"),
        ("Worldbuilding/Society/Weapons, Instruments & Relics/World Bending Objects/", "Relics", "World-Bending Relic"),
        ("Worldbuilding/Society/Weapons, Instruments & Relics/", "Relics", "Armament"),
        ("Worldbuilding/Society/", "Society", "Society"),
        ("Worldbuilding/Truths, Chaos, Rituals/Forbidden Objects/", "Truths", "Forbidden Object"),
        ("Worldbuilding/Truths, Chaos, Rituals/Inescapable Truths/", "Truths", "Inescapable Truth"),
        ("Worldbuilding/Truths, Chaos, Rituals/Old Testament/", "Mythology", "Old Testament"),
        ("Worldbuilding/Truths, Chaos, Rituals/Outer God Rituals/", "Chaos", "Outer God Ritual"),
        ("Worldbuilding/World Environment/Atonalis/Original Eight/", "Chaos", "Original Eight"),
        ("Worldbuilding/World Environment/Atonalis/Spellweaving Rituals/", "Magic", "Spellweaving Ritual"),
        ("Worldbuilding/World Environment/Atonalis/", "Chaos", "Atonalis"),
        ("Worldbuilding/World Environment/Bestiary/", "World", "Bestiary"),
        ("Worldbuilding/World Environment/Civilizations/", "World", "Civilization"),
        ("Worldbuilding/World Environment/Enclaves/", "World", "Enclave"),
        ("Worldbuilding/World Environment/Landmarks/", "World", "Landmark"),
        ("Worldbuilding/Events/World Events/", "Events", "World Event"),
        ("Worldbuilding/Events/", "Events", "Event"),
        ("Worldbuilding/", "World", "Lore"),
    };

    private static readonly Regex AgesFolder = new Regex(@"/Ages (\d+)/");

    private static (string group, string category) Defaults(string path, string title)
    {
        var folder = Folders.FirstOrDefault(f => path.StartsWith(f.prefix, StringComparison.OrdinalIgnoreCase));
        string group = folder.group ?? "World", category = folder.category ?? "Lore";
        var ages = AgesFolder.Match(path);
        if (category == "Age" && ages.Success) category = "Age " + Roman(int.Parse(ages.Groups[1].Value, CultureInfo.InvariantCulture));
        else if (category == "Legend" && ages.Success) category = "Legend" + " · Age " + Roman(int.Parse(ages.Groups[1].Value, CultureInfo.InvariantCulture));
        if (category == "Binding" && Elements.Contains(title)) category = "Element";
        if (category == "Inescapable Truth" && title.StartsWith("Weight of", StringComparison.OrdinalIgnoreCase)) category = "Weight";
        return (group, category);
    }

    private static string Roman(int number)
    {
        if (number <= 0) return "0";
        var numerals = new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
        var sb = new StringBuilder();
        foreach (var (value, symbol) in numerals)
        {
            while (number >= value)
            {
                sb.Append(symbol);
                number -= value;
            }
        }
        return sb.ToString();
    }

    private static void Build(Entry entry, Note note, Vault vault, Result result)
    {
        string owner = $"'{entry.title}' ({note.path})";
        // An entry taken from part of a note has only the passages the manifest names.
        bool ownNote = string.Equals(entry.title, note.title, StringComparison.OrdinalIgnoreCase);
        string Default(string selectors) => selectors ?? (ownNote ? null : "none");
        var (group, category) = Defaults(note.path, entry.title);
        string title = entry.display ?? entry.title;
        var keyword = new Keyword
        {
            title = title,
            id = LibraryIndex.Slug(title),
            category = entry.category ?? category,
            group = entry.group ?? group,
            source = note.path,
        };
        // A title shown instead of the note's name keeps that name as another name for the entry.
        if (!string.Equals(title, entry.title, StringComparison.OrdinalIgnoreCase)) keyword.aliases.Add(entry.title);
        keyword.aliases.AddRange(entry.aliases);
        keyword.related.AddRange(entry.related);
        keyword.autoLink = entry.autoLink ?? !(QuietWords.Contains(entry.title) || entry.title.Length <= 3);

        var used = new HashSet<LoreNotes.Block>();
        var remainders = new Dictionary<LoreNotes.Block, string>();

        // The epigraph: the note's own unless the manifest says otherwise. It stands above the article, not in it.
        var flavor = Select(Default(entry.flavor), note, vault, owner, result.problems);
        if (flavor == null)
        {
            if (note.epigraph != null)
            {
                keyword.flavor = note.epigraphText;
                used.Add(note.epigraph);
            }
        }
        else
        {
            keyword.flavor = LoreNotes.CloseItalic(Join(flavor, headings: false));
            Use(flavor, used, remainders);
        }

        // The summary: the lead of the first lore paragraph. It is what a word with no card of its own shows, and what
        // the Library's search reads; the paragraph stays whole in the article.
        var summary = Select(Default(entry.summary), note, vault, owner, result.problems);
        if (summary == null)
        {
            var first = note.blocks.FirstOrDefault(b => b.meta == null && !used.Contains(b) && b.kind == LoreNotes.Kind.Paragraph && !IsLabel(b.text));
            if (first != null) keyword.summary = Lead(first, out _).text;
        }
        else keyword.summary = Join(summary, headings: false);

        // The article: every other passage that is lore, or what the manifest picks. "auto" (anywhere in the list)
        // stands for every passage of the note that is lore and not otherwise named; named passages skip what the
        // epigraph already shows (or keep only the rest of its paragraph).
        var skip = Select(entry.skip, note, vault, owner, result.problems) ?? new List<Piece>();
        foreach (var piece in skip) if (piece.block != null) used.Add(piece.block);
        var details = Select(Default(entry.details), note, vault, owner, result.problems);
        List<(LoreNotes.Block block, string text)> body;
        bool withAuto = details == null || details.Any(p => p.auto);
        if (details == null) body = AutoDetails(note, used, remainders);
        else
        {
            var named = details.Where(p => !p.auto && p.block != null).Select(p => p.block).ToList();
            body = new List<(LoreNotes.Block block, string text)>();
            foreach (var piece in details)
            {
                if (piece.auto)
                {
                    var alsoUsed = new HashSet<LoreNotes.Block>(used.Concat(named));
                    body.AddRange(AutoDetails(note, alsoUsed, remainders));
                    continue;
                }
                if (piece.whole && piece.block != null && used.Contains(piece.block) && !remainders.ContainsKey(piece.block)) continue;
                body.Add((piece.block, piece.whole && piece.block != null && remainders.TryGetValue(piece.block, out var rest) ? rest : piece.text));
            }
        }
        if (withAuto) details = null; // for the report: the note's own text was chosen automatically

        // Sources beyond the note itself.
        foreach (var path in (flavor ?? new List<Piece>()).Concat(summary ?? new List<Piece>()).Select(p => p.block).Concat(body.Select(b => b.block)))
        {
            if (path == null) continue;
            string other = note.blocks.Contains(path) ? null : vault.PathOf(path);
            if (other != null && !keyword.source.Contains(other)) keyword.source += "; " + other;
        }

        // What the note's own entry leaves out (an entry taken from part of a note reports only what it kept).
        var kept = new HashSet<LoreNotes.Block>(body.Where(b => b.block != null).Select(b => b.block));
        foreach (var piece in (flavor ?? new List<Piece>()).Concat(summary ?? new List<Piece>())) if (piece.block != null) kept.Add(piece.block);
        foreach (var block in kept.Where(b => b.meta != null && !note.blocks.Contains(b))) keyword.cut.Add($"KEPT although {block.meta}: {LoreNotes.Preview(block.text, 110)}");
        foreach (var block in note.blocks)
        {
            if (block.meta == null) continue;
            if (kept.Contains(block)) keyword.cut.Add($"KEPT although {block.meta}: {LoreNotes.Preview(block.text, 110)}");
            else if (ownNote && (block.kind != LoreNotes.Kind.Heading || IsFirstOfSection(note, block))) keyword.cut.Add($"left out ({block.meta}): {LoreNotes.Preview(block.text, 110)}");
        }
        if (ownNote && details == null)
        {
            foreach (var piece in skip.Where(p => p.block != null && p.block.meta == null)) keyword.cut.Add($"left out (manifest skip): {LoreNotes.Preview(piece.block.text, 110)}");
        }
        if (ownNote && details != null)
        {
            // An explicit selection leaves out every lore block it does not name: list them too.
            var named = new HashSet<LoreNotes.Block>(kept.Concat(used));
            foreach (var block in note.blocks.Where(b => b.meta == null && !named.Contains(b) && b.kind != LoreNotes.Kind.Heading))
            {
                keyword.cut.Add($"left out (not selected by the manifest): {LoreNotes.Preview(block.text, 110)}");
            }
        }

        // The article is the note as written. A long note's sections are entries of their own, and its own article
        // keeps what comes before them (the Library shows the whole note: LibraryIndex.Article). A catalogue's
        // entries are entries of their own too, and the catalogue keeps its whole text.
        var splitAt = SplitPoints(entry.split);
        bool split = splitAt != null || entry.split == "sections" || entry.split == null && ShouldSplit(body);
        var own = body;
        if (entry.split == "entries") SplitEntries(keyword, body, result);
        else if (split) own = SplitSections(keyword, note, body, splitAt, result);
        keyword.body = JoinBody(own);
        // A summary taken from elsewhere opens the article.
        if (!string.IsNullOrWhiteSpace(keyword.summary) && !Contains(JoinBody(body), keyword.summary.Trim()))
        {
            keyword.body = LoreNotes.Compose(new[] { keyword.summary, keyword.body });
        }

        if (string.IsNullOrWhiteSpace(keyword.summary) && string.IsNullOrWhiteSpace(keyword.body) && string.IsNullOrWhiteSpace(keyword.flavor))
        {
            result.skipped.Add($"{note.path}: nothing in it is lore" + (ownNote ? string.Empty : $" (entry '{keyword.title}')"));
            return;
        }
        result.keywords.Add(keyword);
    }

    private static bool IsFirstOfSection(Note note, LoreNotes.Block heading) => heading.index == 0 || note.blocks[heading.index - 1].meta == null;

    private static bool IsLabel(string text) => Regex.IsMatch(text.Trim(), @"^[^\n]{1,60}:\**$");

    // Blocks a field took whole are not repeated in the details; a paragraph whose lead it took leaves its rest there.
    // Any other part (a line or sentence from the middle) leaves its block whole in the details: repeated, never lost.
    private static void Use(List<Piece> pieces, HashSet<LoreNotes.Block> used, Dictionary<LoreNotes.Block, string> remainders)
    {
        foreach (var piece in pieces)
        {
            if (piece.block == null) continue;
            if (piece.whole)
            {
                used.Add(piece.block);
                continue;
            }
            string text = piece.block.text.Trim(), part = piece.text.Trim();
            if (text.Length > part.Length && text.StartsWith(part, StringComparison.Ordinal))
            {
                used.Add(piece.block);
                remainders[piece.block] = text.Substring(part.Length).Trim();
            }
        }
    }

    private static string Join(List<Piece> pieces, bool headings) =>
        LoreNotes.Compose(pieces.Where(p => headings || p.block == null || p.block.kind != LoreNotes.Kind.Heading).Select(p => p.text));

    private static List<(LoreNotes.Block block, string text)> AutoDetails(Note note, HashSet<LoreNotes.Block> used, Dictionary<LoreNotes.Block, string> remainders)
    {
        var body = new List<(LoreNotes.Block, string)>();
        foreach (var block in note.blocks)
        {
            if (remainders.TryGetValue(block, out var rest))
            {
                // The rest of a paragraph whose lead a field took; not when the paragraph is not lore.
                if (block.meta == null) body.Add((block, rest));
                continue;
            }
            if (block.meta != null || used.Contains(block)) continue;
            if (block.kind == LoreNotes.Kind.Heading && !SectionHasLore(note, block, used)) continue;
            body.Add((block, block.text));
        }
        return body;
    }

    private static bool SectionHasLore(Note note, LoreNotes.Block heading, HashSet<LoreNotes.Block> used)
    {
        for (int i = heading.index + 1; i < note.blocks.Count; i++)
        {
            var block = note.blocks[i];
            if (block.kind == LoreNotes.Kind.Heading)
            {
                if (block.level <= heading.level) return false;
                continue;
            }
            if (block.meta == null && !used.Contains(block)) return true;
        }
        return false;
    }

    private static string JoinBody(List<(LoreNotes.Block block, string text)> body) =>
        LoreNotes.Compose(body.Select(b => b.block != null && b.block.kind == LoreNotes.Kind.Heading ? LoreNotes.AsSubheading(b.block) : b.text));

    // Long notes read better as a list of their sections, each a keyword of its own.
    private const int SplitAbove = 6000;

    private static bool ShouldSplit(List<(LoreNotes.Block block, string text)> body) =>
        body.Sum(b => b.text?.Length ?? 0) > SplitAbove && SectionLevel(body) > 0;

    // The heading level that cuts the text into sections: the highest level used at least three times (0: none).
    private static int SectionLevel(List<(LoreNotes.Block block, string text)> body)
    {
        var levels = body.Where(b => b.block != null && b.block.kind == LoreNotes.Kind.Heading).Select(b => b.block.level).ToList();
        for (int level = 1; level <= 6; level++) if (levels.Count(l => l == level) >= 3) return level;
        return 0;
    }

    // split: at "Phase I", "Themes" → the headings that open a section; null for any other value.
    private static List<string> SplitPoints(string split)
    {
        if (split == null || !split.StartsWith("at ", StringComparison.OrdinalIgnoreCase)) return null;
        return Regex.Matches(split, @"""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>
    /// Make each section of a long note an entry of its own; what comes before the first section (returned) stays
    /// the note's own article.
    /// </summary>
    private static List<(LoreNotes.Block block, string text)> SplitSections(Keyword parent, Note note, List<(LoreNotes.Block block, string text)> body, List<string> splitAt, Result result)
    {
        int top = SectionLevel(body);
        if (top == 0 && splitAt == null)
        {
            var any = body.Where(b => b.block != null && b.block.kind == LoreNotes.Kind.Heading).ToList();
            if (any.Count == 0) return body;
            top = any.Min(b => b.block.level);
        }
        // Headings at the section level (or above it) open a section; "split: at" names them instead.
        bool Opens(LoreNotes.Block block) => block != null && block.kind == LoreNotes.Kind.Heading &&
            (splitAt != null ? splitAt.Any(s => block.HeadingText.StartsWith(s, StringComparison.OrdinalIgnoreCase)) : block.level <= top);

        var intro = new List<(LoreNotes.Block block, string text)>();
        var sections = new List<(LoreNotes.Block heading, List<(LoreNotes.Block, string)> body)>();
        foreach (var item in body)
        {
            if (Opens(item.block)) sections.Add((item.block, new List<(LoreNotes.Block, string)>()));
            else if (sections.Count > 0) sections[sections.Count - 1].body.Add(item);
            else intro.Add(item);
        }

        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (heading, content) in sections)
        {
            if (content.Count == 0) continue;
            string title = heading.HeadingText;
            string unique = title;
            for (int n = 2; !titles.Add(unique); n++) unique = $"{title} ({n})";
            var section = new Keyword
            {
                title = unique,
                id = parent.id + "--" + LibraryIndex.Slug(unique),
                category = parent.title,
                group = parent.group,
                source = note.path + "#" + title,
                autoLink = false,
                isSection = true,
                parent = parent,
                section = unique,
            };
            var first = content.FirstOrDefault(c => c.Item1 != null && c.Item1.kind == LoreNotes.Kind.Paragraph && c.Item2 == c.Item1.text && !IsLabel(c.Item2));
            if (first.Item1 != null) section.summary = Lead(first.Item1, out _).text;
            section.body = JoinBody(content);
            parent.sections.Add(section);
            result.keywords.Add(section);
        }
        return intro;
    }

    // "**Moonlit Vigil | Weaver [[Civic]] | [[Ages]] 0-III | [[Consonance]]**": a named entry of a catalogue note.
    private static readonly Regex EntryHeader = new Regex(@"^\*\*(?<title>[^|\n]+?)\s*\|(?<rest>[^\n]*?)(\*\*)?\s*$");

    /// <summary>
    /// A catalogue note (split: entries): each "**Name | kind | ...**" line opens an entry of its own, named by its
    /// first part, its other parts as the category. The note's own entry keeps the whole catalogue as its article.
    /// </summary>
    private static void SplitEntries(Keyword parent, List<(LoreNotes.Block block, string text)> body, Result result)
    {
        Keyword current = null;
        var content = new List<(LoreNotes.Block, string)>();

        void Close()
        {
            if (current == null) return;
            var first = content.FirstOrDefault(c => c.Item1 != null && c.Item1.kind == LoreNotes.Kind.Paragraph && !IsLabel(c.Item2));
            if (first.Item1 != null) current.summary = first.Item2 == first.Item1.text ? Lead(first.Item1, out _).text : first.Item2;
            current.body = JoinBody(content);
            result.keywords.Add(current);
            current = null;
            content = new List<(LoreNotes.Block, string)>();
        }

        foreach (var item in body)
        {
            // An entry's header is the first line of its paragraph; any lines under it are its description.
            string firstLine = item.text.Trim().Split('\n')[0].Trim();
            var header = item.block != null && item.block.kind == LoreNotes.Kind.Paragraph ? EntryHeader.Match(firstLine) : Match.Empty;
            if (header.Success)
            {
                Close();
                string title = LoreNotes.Plain(header.Groups["title"].Value).Trim();
                string kind = string.Join(" · ", header.Groups["rest"].Value.Split('|').Select(p => LoreNotes.Plain(p).Trim().Trim('*').Trim()).Where(p => p.Length > 0));
                current = new Keyword { title = title, id = LibraryIndex.Slug(title), category = kind, group = parent.group, source = parent.source + " (" + title + ")" };
                current.related.Add(parent.title);
                parent.related.Add(title);
                string description = item.text.Trim().Substring(item.text.Trim().IndexOf('\n') + 1).Trim();
                if (item.text.Trim().IndexOf('\n') > 0 && description.Length > 0) content.Add((item.block, description));
                continue;
            }
            // A heading ends the entry above it; text outside every entry is the catalogue's own.
            bool heading = item.block != null && item.block.kind == LoreNotes.Kind.Heading;
            if (current != null && !heading) content.Add(item);
            else Close();
        }
        Close();
    }

    private static void CheckIds(Result result)
    {
        foreach (var group in result.keywords.GroupBy(k => k.id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            result.problems.Add($"Library id '{group.Key}' is used by {string.Join(", ", group.Select(k => $"'{k.title}' ({k.source})"))}: only the first is kept");
            foreach (var extra in group.Skip(1).ToList()) result.keywords.Remove(extra);
        }
    }

    // ===== THE LIBRARY =====

    // ".../Ages of Magic/Ages 2/Age of Embers.md": an Age and its number.
    private static readonly Regex AgeNote = new Regex(@"/Ages of Magic/Ages (\d+)/(Age [^/]+)\.md$", RegexOptions.IgnoreCase);

    /// <summary>
    /// The entries and the Ages as Library.json holds them. Every [[link]], "See also" and section is resolved to an
    /// entry id by the same rules the game uses (<see cref="LibraryIndex.Resolve"/>).
    /// </summary>
    private static LibraryFile ToLibrary(Result result, Vault vault)
    {
        var file = new LibraryFile { version = FormatVersion };
        foreach (var path in vault.Notes)
        {
            var match = AgeNote.Match("/" + path);
            if (!match.Success) continue;
            string title = match.Groups[2].Value;
            file.ages.Add(new LibraryAge { id = LibraryIndex.Slug(title), title = title, number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) });
        }
        file.ages.Sort((a, b) => a.number != b.number ? a.number.CompareTo(b.number) : string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));

        var byKeyword = new Dictionary<Keyword, LibraryEntry>();
        foreach (var keyword in result.keywords)
        {
            var entry = new LibraryEntry
            {
                id = keyword.id,
                title = keyword.title,
                category = keyword.category,
                shelf = keyword.group,
                epigraph = keyword.flavor,
                summary = keyword.summary,
                body = keyword.body,
                parent = keyword.parent?.id,
                section = keyword.section,
                quiet = !keyword.autoLink,
                source = keyword.source,
            };
            foreach (var alias in keyword.aliases)
            {
                if (!string.Equals(alias, keyword.title, StringComparison.OrdinalIgnoreCase) && !entry.aliases.Contains(alias, StringComparer.OrdinalIgnoreCase)) entry.aliases.Add(alias);
            }
            byKeyword[keyword] = entry;
            file.entries.Add(entry);
        }

        var index = new LibraryIndex(file.entries, file.ages);
        foreach (var keyword in result.keywords)
        {
            var entry = byKeyword[keyword];
            void Link(LibraryEntry other)
            {
                if (other != null && other.id != entry.id && !entry.links.Contains(other.id)) entry.links.Add(other.id);
            }
            foreach (var section in keyword.sections) Link(byKeyword.TryGetValue(section, out var linked) ? linked : null);
            foreach (var target in new[] { keyword.flavor, keyword.summary, keyword.body }.SelectMany(LoreNotes.LinkTargets)) Link(index.Resolve(target));
            foreach (var name in keyword.related)
            {
                var other = index.Resolve(name);
                if (other != null) Link(other);
                else result.problems.Add($"'{keyword.title}': \"See also\" names '{name}', which is no entry");
            }
        }
        file.entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return file;
    }

    /// <summary>
    /// Library.json as Unity's JsonUtility reads it: one field per line so a re-import reads well in a diff, and
    /// empty fields left out (they read back as their defaults).
    /// </summary>
    public static string ToJson(LibraryFile file)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"version\": ").Append(file.version.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"ages\": [\n");
        for (int i = 0; i < file.ages.Count; i++)
        {
            var age = file.ages[i];
            sb.Append("    { \"id\": ").Append(Json(age.id)).Append(", \"title\": ").Append(Json(age.title))
              .Append(", \"number\": ").Append(age.number.ToString(CultureInfo.InvariantCulture)).Append(" }").Append(i < file.ages.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ],\n  \"entries\": [\n");
        for (int i = 0; i < file.entries.Count; i++)
        {
            var entry = file.entries[i];
            var fields = new List<string>();
            void Text(string name, string value)
            {
                if (!string.IsNullOrEmpty(value)) fields.Add($"      \"{name}\": {Json(value)}");
            }
            void List(string name, List<string> values)
            {
                if (values != null && values.Count > 0) fields.Add($"      \"{name}\": [{string.Join(", ", values.Select(Json))}]");
            }
            Text("id", entry.id);
            Text("title", entry.title);
            Text("category", entry.category);
            Text("shelf", entry.shelf);
            Text("epigraph", entry.epigraph);
            Text("summary", entry.summary);
            Text("body", entry.body);
            List("aliases", entry.aliases);
            List("links", entry.links);
            Text("parent", entry.parent);
            Text("section", entry.section);
            if (entry.quiet) fields.Add("      \"quiet\": true");
            Text("source", entry.source);
            sb.Append("    {\n").Append(string.Join(",\n", fields)).Append("\n    }").Append(i < file.entries.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    /// <summary>A JSON string: quotes, backslashes and control characters escaped; everything else as written (UTF-8).</summary>
    public static string Json(string value)
    {
        if (value == null) return "\"\"";
        var sb = new StringBuilder(value.Length + 16);
        sb.Append('"');
        foreach (char c in value.Replace("\r\n", "\n"))
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': break;
                default:
                    if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ===== REPORT =====

    private static string Report(Result result, Settings settings, string manifestPath)
    {
        var gameTerms = new HashSet<string>(settings.gameTerms ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var library = result.library ?? new LibraryFile();
        var index = new LibraryIndex(library.entries, library.ages);
        var sb = new StringBuilder();
        sb.Append("# Vault import report\n\n");
        sb.Append("Written by the lore import (Tools > Gateway to Genesis > Import Lore From Vault). Each run replaces it.\n\n");
        sb.Append($"- Vault: `{settings.vaultRoot}`\n- Manifest: `{manifestPath}`\n");
        int sections = result.keywords.Count(k => k.isSection);
        sb.Append($"- Glossary: {result.keywords.Count - sections} entries from notes and passages, {sections} sections of long notes, {library.ages.Count} Ages\n");
        if (settings.write) sb.Append($"- {OutputFile}: {(result.written ? "written" : "unchanged")}\n");
        sb.Append('\n');

        // Two entries answering to the same name: links reach the first only.
        var conflicts = new List<string>();
        var claimed = new Dictionary<string, LibraryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in library.entries.Where(e => string.IsNullOrEmpty(e.parent)).OrderBy(e => e.title, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var name in new[] { entry.title }.Concat(entry.aliases))
            {
                if (!claimed.TryGetValue(name, out var other)) claimed[name] = entry;
                else if (other != entry) conflicts.Add($"'{name}' is claimed by '{other.title}' ({other.source}) and by '{entry.title}' ({entry.source})");
            }
        }
        Section(sb, "Problems", result.problems.Concat(conflicts.Select(c => "Name conflict: " + c)));

        // Links in the imported text that reach no entry: notes to write, or names to align.
        var missing = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in result.keywords)
        {
            foreach (var target in new[] { keyword.flavor, keyword.summary, keyword.body }.SelectMany(LoreNotes.LinkTargets))
            {
                if (index.Resolve(target) != null || gameTerms.Contains(target)) continue;
                if (!missing.TryGetValue(target, out var from)) missing[target] = from = new List<string>();
                if (!from.Contains(keyword.title)) from.Add(keyword.title);
            }
        }
        Section(sb, $"Linked terms with no entry ({missing.Count}): they show as plain words until a note exists",
            missing.OrderByDescending(m => m.Value.Count).ThenBy(m => m.Key).Select(m => $"[[{m.Key}]] ({m.Value.Count}: {string.Join(", ", m.Value.Take(6))}{(m.Value.Count > 6 ? ", ..." : "")})"));

        Section(sb, "Notes not imported", result.skipped);

        sb.Append("## Entries\n\nEvery entry by shelf, where its text comes from, and each passage of the note left out (or kept although it looked like a design note).\n\n");
        foreach (var group in result.keywords.Where(k => !k.isSection).GroupBy(k => k.group).OrderBy(g => g.Key))
        {
            sb.Append($"### {group.Key}\n\n");
            foreach (var keyword in group.OrderBy(k => k.title, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append($"- **{keyword.title}** (`{keyword.id}`, {keyword.category}) from `{keyword.source}`: {keyword.Length} characters");
                if (keyword.sections.Count > 0) sb.Append($", in {keyword.sections.Count} sections");
                if (!keyword.autoLink) sb.Append(", quiet");
                sb.Append('\n');
                foreach (var line in keyword.cut) sb.Append($"  - {line}\n");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return;
        sb.Append($"## {title}\n\n");
        foreach (var line in list) sb.Append($"- {line}\n");
        sb.Append('\n');
    }
}
