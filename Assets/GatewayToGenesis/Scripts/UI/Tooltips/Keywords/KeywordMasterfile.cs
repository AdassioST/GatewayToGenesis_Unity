using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// The keyword masterfile (Resources/Keywords/Keywords.md): the short, hand-written cards tooltips show when a
/// golden word is hovered, staged so a card never knows more than the world does yet. The same format as the Sonata
/// website's masterfile, with Ages and civics where the website has chapters:
/// <code>
/// ## Auric Peach
/// wiki: auric-peach
/// also: Auric Peaches
///
/// @default
/// Shown until a state below applies.
///
/// @age-1
/// Shown from Age 1 (the Ages after the Age of Desolation) onward, until a later Age's state.
///
/// @age-of-embers
/// Shown only during the Age of Embers.
///
/// @civic-moonlit-vigil
/// Shown while the civic Moonlit Vigil is in force, whatever the Age.
/// </code>
/// <c>wiki:</c> is the White-Haven Library entry the card opens; <c>also:</c> lists other spellings (plurals,
/// possessives and a leading "The" are handled already). Game extensions: <c>id:</c> binds the card to a game term
/// ("pillar:waltz") so it also shows live values, <c>category:</c> sets the small-caps line, and
/// <c>autolink: off</c> leaves the word plain unless a [[link]] names it. Anything inside <c>&lt;!-- --&gt;</c> is
/// ignored, and a state still reading "TODO..." is skipped, as on the website. Free of Unity, so it is testable.
/// </summary>
public sealed class KeywordMasterfile
{
    /// <summary>A stub's prose is a note to the author, never shown.</summary>
    private static readonly Regex Placeholder = new Regex(@"^TODO\b", RegexOptions.Compiled);
    private static readonly Regex Comment = new Regex(@"<!--[\s\S]*?-->", RegexOptions.Compiled);
    private static readonly Regex Heading = new Regex(@"^##[ \t]+", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex StateLine = new Regex(@"^@([\w-]+)[ \t]*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MetaLine = new Regex(@"^\s*(wiki|also|id|category|autolink)\s*:\s*(.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AgeNumber = new Regex(@"^age-(\d{1,2})$", RegexOptions.Compiled);

    private readonly List<KeywordCard> _cards = new List<KeywordCard>();
    private readonly Dictionary<string, KeywordCard> _byName = new Dictionary<string, KeywordCard>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KeywordCard> _byId = new Dictionary<string, KeywordCard>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KeywordCard> _byWiki = new Dictionary<string, KeywordCard>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<KeywordCard> Cards => _cards;
    /// <summary>What could not be read as written (a title used twice), for <see cref="ContentValidator"/>.</summary>
    public readonly List<string> Problems = new List<string>();

    public static KeywordMasterfile Parse(string markdown)
    {
        var file = new KeywordMasterfile();
        if (string.IsNullOrEmpty(markdown)) return file;
        string text = Comment.Replace(markdown.Replace("\r\n", "\n"), string.Empty);

        // Everything from one "## Heading" to the next; what comes before the first is the file's own notes.
        var blocks = Heading.Split(text);
        for (int b = 1; b < blocks.Length; b++)
        {
            string block = blocks[b];
            int newline = block.IndexOf('\n');
            string title = (newline < 0 ? block : block.Substring(0, newline)).Trim();
            if (title.Length == 0) continue;
            string body = newline < 0 ? string.Empty : block.Substring(newline + 1);
            var card = new KeywordCard { title = title };

            // Metadata lines sit above the first "@state" marker.
            var first = StateLine.Match(body);
            string head = first.Success ? body.Substring(0, first.Index) : body;
            foreach (var line in head.Split('\n'))
            {
                var meta = MetaLine.Match(line);
                if (!meta.Success) continue;
                string value = meta.Groups[2].Value.Trim();
                switch (meta.Groups[1].Value.ToLowerInvariant())
                {
                    case "wiki": card.wiki = value.Length > 0 ? value : null; break;
                    case "id": card.gameId = value.Length > 0 ? value : null; break;
                    case "category": card.category = value.Length > 0 ? value : null; break;
                    case "autolink": card.autoLink = !Regex.IsMatch(value, "^(off|no|false)$", RegexOptions.IgnoreCase); break;
                    default:
                        foreach (var alias in value.Split(','))
                        {
                            if (alias.Trim().Length > 0) card.also.Add(alias.Trim());
                        }
                        break;
                }
            }

            if (first.Success)
            {
                // Split on the state markers, keeping each label with its paragraph.
                var parts = StateLine.Split(body.Substring(first.Index));
                for (int i = 1; i + 1 < parts.Length; i += 2)
                {
                    string label = parts[i].Trim().ToLowerInvariant();
                    string value = LibraryArticles.Unwrap(parts[i + 1]).Trim();
                    if (value.Length == 0 || Placeholder.IsMatch(value)) continue;
                    card.states.Add(KeywordState.For(label, value));
                }
                // The default first; the others keep the order they were written in.
                card.states.Sort((x, y) => (x.kind == KeywordStateKind.Default ? 0 : 1).CompareTo(y.kind == KeywordStateKind.Default ? 0 : 1));
            }

            // A card with no reading still binds its words to a Library entry or a game term.
            if (card.states.Count == 0 && card.wiki == null && card.gameId == null) continue;
            if (file._byId.ContainsKey(card.Id))
            {
                file.Problems.Add($"Keyword card '{card.title}' has the id '{card.Id}' of an earlier card: only the first is used");
                continue;
            }
            file._cards.Add(card);
            file._byId[card.Id] = card;
            if (card.wiki != null && !file._byWiki.ContainsKey(card.wiki)) file._byWiki[card.wiki] = card;
        }

        // Exact names (titles and "also:") of every card first, so a plural never takes another card's own title.
        foreach (var card in file._cards)
        {
            foreach (var name in card.Names())
            {
                if (!file._byName.TryGetValue(name, out var other)) file._byName[name] = card;
                else if (other != card) file.Problems.Add($"Keyword cards '{other.title}' and '{card.title}' both claim the word '{name}': the first keeps it");
            }
        }
        foreach (var card in file._cards)
        {
            foreach (var name in card.Forms()) if (!file._byName.ContainsKey(name)) file._byName[name] = card;
        }
        return file;
    }

    // ===== LOOKUP =====

    /// <summary>The card a word means (its title, an "also:" spelling, or a form prose writes them in), or null.</summary>
    public KeywordCard Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _byName.TryGetValue(name.Trim(), out var card) ? card : null;
    }

    /// <summary>The card with this <see cref="KeywordCard.Id"/>, or null.</summary>
    public KeywordCard ById(string id) => id != null && _byId.TryGetValue(id.Trim(), out var card) ? card : null;

    /// <summary>The card that opens Library entry <paramref name="entryId"/>, or null.</summary>
    public KeywordCard ForWiki(string entryId) => entryId != null && _byWiki.TryGetValue(entryId.Trim(), out var card) ? card : null;

    // ===== READING =====

    /// <summary>
    /// What the card says in <paramref name="context"/>: a state for a civic in force first (the first written wins),
    /// then a state for the current Age by name, then the state of the latest Age number reached, then the default,
    /// then <paramref name="fallback"/> (the Library entry's summary).
    /// </summary>
    public static string Reading(KeywordCard card, KeywordContext context, string fallback = "")
    {
        if (card == null || card.states.Count == 0) return fallback;
        foreach (var state in card.states)
        {
            if (state.kind == KeywordStateKind.Civic && context.HasCivic(state.target)) return state.text;
        }
        foreach (var state in card.states)
        {
            if (state.kind == KeywordStateKind.DuringAge && string.Equals(state.target, context.ageId, StringComparison.OrdinalIgnoreCase)) return state.text;
        }
        KeywordState best = null;
        foreach (var state in card.states)
        {
            if (state.kind == KeywordStateKind.FromAge && state.number <= context.ageNumber && (best == null || state.number >= best.number)) best = state;
        }
        if (best != null) return best.text;
        foreach (var state in card.states) if (state.kind == KeywordStateKind.Default) return state.text;
        return fallback;
    }

    /// <summary>
    /// True when a later Age revises the card (the card says so, as the website's does for later chapters).
    /// <paramref name="ageNumberOf"/> gives an Age's number by id (-1 when unknown); civic states never count.
    /// </summary>
    public static bool HasDeeper(KeywordCard card, KeywordContext context, Func<string, int> ageNumberOf = null)
    {
        if (card == null) return false;
        foreach (var state in card.states)
        {
            if (state.kind == KeywordStateKind.FromAge && state.number > context.ageNumber) return true;
            if (state.kind == KeywordStateKind.DuringAge && ageNumberOf != null && ageNumberOf(state.target) > context.ageNumber) return true;
        }
        return false;
    }

    /// <summary>"age-2" → 2; -1 for any other label.</summary>
    public static int AgeNumberOf(string label)
    {
        var match = AgeNumber.Match(label ?? string.Empty);
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    }
}

public enum KeywordStateKind
{
    Default,
    /// <summary>"@age-2": from Age 2 (the vault's "Ages 2") onward.</summary>
    FromAge,
    /// <summary>"@age-of-embers": only during that Age.</summary>
    DuringAge,
    /// <summary>"@civic-moonlit-vigil": while that civic is in force.</summary>
    Civic,
}

/// <summary>One reading of a card and when it applies.</summary>
public sealed class KeywordState
{
    public string label;
    public KeywordStateKind kind;
    /// <summary>The Age number (FromAge), or -1.</summary>
    public int number = -1;
    /// <summary>The Age id (Age) or civic slug (Civic).</summary>
    public string target;
    public string text;

    public static KeywordState For(string label, string text)
    {
        var state = new KeywordState { label = label, text = text };
        int number = KeywordMasterfile.AgeNumberOf(label);
        if (label == "default") state.kind = KeywordStateKind.Default;
        else if (number >= 0)
        {
            state.kind = KeywordStateKind.FromAge;
            state.number = number;
        }
        else if (label.StartsWith("civic-", StringComparison.Ordinal))
        {
            state.kind = KeywordStateKind.Civic;
            state.target = label.Substring("civic-".Length);
        }
        else
        {
            state.kind = KeywordStateKind.DuringAge;
            state.target = label;
        }
        return state;
    }

    public override string ToString() => "@" + label;
}

/// <summary>One "## Term" of the masterfile.</summary>
public sealed class KeywordCard
{
    public string title;
    /// <summary>The Library entry the card opens ("auric-peach"), or null.</summary>
    public string wiki;
    /// <summary>A game term the card is bound to ("pillar:waltz"), whose live values it shows, or null.</summary>
    public string gameId;
    /// <summary>The small-caps line under the title, or null for the Library entry's category.</summary>
    public string category;
    public bool autoLink = true;
    public readonly List<string> also = new List<string>();
    /// <summary>The default first, then the others in the order written.</summary>
    public readonly List<KeywordState> states = new List<KeywordState>();

    /// <summary>The keyword id links use: the game term, else the Library entry, else the title's slug.</summary>
    public string Id => gameId ?? wiki ?? LibraryIndex.Slug(title);

    /// <summary>The title and every "also:" spelling.</summary>
    public IEnumerable<string> Names()
    {
        yield return title;
        foreach (var alias in also) yield return alias;
    }

    // What prose writes: plurals, possessives, and a leading "The" (or its absence), as on the website.
    internal IEnumerable<string> Forms() => LibraryIndex.Forms(title);

    public override string ToString() => title;
}

/// <summary>Where the world stands, for choosing a card's reading: the Age (by id and number) and the civics in force.</summary>
public struct KeywordContext
{
    public string ageId;
    public int ageNumber;
    /// <summary>Slugs of the civics in force ("moonlit-vigil").</summary>
    public ICollection<string> civics;

    public KeywordContext(string ageId, int ageNumber, ICollection<string> civics = null)
    {
        this.ageId = ageId;
        this.ageNumber = ageNumber;
        this.civics = civics;
    }

    public bool HasCivic(string slug) => civics != null && slug != null && civics.Contains(slug);
}
