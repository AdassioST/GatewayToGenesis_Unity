using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// The White-Haven Library's data as the vault import writes it (Resources/Library/Library.json), read with
/// JsonUtility. The same shape as the Sonata website's wiki index: one entry per note, its shelf, a short summary,
/// the article, and the entries it links to.
/// </summary>
[Serializable]
public class LibraryFile
{
    public int version;
    /// <summary>Every Age of the vault, with its number (its "Ages N" folder: the Age of Desolation is 0), for keyword states.</summary>
    public List<LibraryAge> ages = new List<LibraryAge>();
    public List<LibraryEntry> entries = new List<LibraryEntry>();
}

[Serializable]
public class LibraryAge
{
    /// <summary>Slug of the Age's name: "age-of-desolation".</summary>
    public string id;
    public string title;
    public int number;
}

/// <summary>One article of the Library.</summary>
[Serializable]
public class LibraryEntry
{
    /// <summary>Slug of the title ("auric-aria"); a section's is its note's id, "--" and its heading's slug.</summary>
    public string id;
    public string title;
    /// <summary>Fine label under the title: "Deity", "Age 0", "Magical Resource"...</summary>
    public string category;
    /// <summary>The shelf it is filed on: "Mythology", "Magic", "Age of Desolation"... (Game Wiki entries: their kind.)</summary>
    public string shelf;
    /// <summary>The note's epigraph, shown first.</summary>
    public string epigraph;
    /// <summary>The note's opening, used where a keyword has no card of its own.</summary>
    public string summary;
    /// <summary>The article, in the vault's Markdown.</summary>
    public string body;
    /// <summary>Other names that mean this entry.</summary>
    public List<string> aliases = new List<string>();
    /// <summary>Ids of the entries this one links to (resolved by the import).</summary>
    public List<string> links = new List<string>();
    /// <summary>For a section of a longer note: the note's entry id, and the section's heading.</summary>
    public string parent;
    public string section;
    /// <summary>An ordinary word (Void, Echo...): only [[links]] reach it, it does not light up by itself.</summary>
    public bool quiet;
    /// <summary>Where it came from: the vault note, or the game system for a Game Wiki entry.</summary>
    public string source;
    /// <summary>True for the Game Wiki half of the Library (how the game works), false for the Glossary (the lore).</summary>
    public bool game;

    public override string ToString() => $"{title} ({id})";
}

/// <summary>
/// Lookups over the Library's entries, free of Unity so they are testable: names (titles, aliases, "Note#Section",
/// and the forms prose writes them in), backlinks, shelves, a ranked search and related reading. The same rules as
/// the website's White-Haven Library.
/// </summary>
public sealed class LibraryIndex
{
    private readonly Dictionary<string, LibraryEntry> _byId = new Dictionary<string, LibraryEntry>(StringComparer.OrdinalIgnoreCase);
    // Names per half of the Library: [0] the Glossary, [1] the Game Wiki. A name resolves in the half asked for first.
    private readonly Dictionary<string, string>[] _byName =
    {
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };
    private readonly Dictionary<string, List<string>> _backlinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly List<LibraryEntry> _entries;
    private readonly Dictionary<string, LibraryAge> _ages = new Dictionary<string, LibraryAge>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LibraryEntry> Entries => _entries;

    /// <summary>Entries left out because another entry already had their id.</summary>
    public readonly List<string> Duplicates = new List<string>();

    public LibraryIndex(IEnumerable<LibraryEntry> entries, IEnumerable<LibraryAge> ages = null)
    {
        _entries = new List<LibraryEntry>();
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
            if (_byId.ContainsKey(entry.id))
            {
                Duplicates.Add($"'{entry.title}' ({entry.source}) has the id '{entry.id}' of '{_byId[entry.id].title}'");
                continue;
            }
            _entries.Add(entry);
            _byId[entry.id] = entry;
        }
        _entries.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));

        // Names: exact titles and aliases first, so a tolerant variant never takes another entry's own name.
        foreach (var entry in _entries)
        {
            Name(entry.title, entry);
            Name(entry.id, entry);
            foreach (var alias in entry.aliases ?? new List<string>()) Name(alias, entry);
            if (!string.IsNullOrEmpty(entry.parent) && !string.IsNullOrEmpty(entry.section) && _byId.TryGetValue(entry.parent, out var parent))
            {
                Name(parent.title + "#" + entry.section, entry);
            }
        }
        foreach (var entry in _entries)
        {
            if (!string.IsNullOrEmpty(entry.parent)) continue; // a section is named through its note only
            foreach (var form in Forms(entry.title)) Name(form, entry);
            foreach (var alias in entry.aliases ?? new List<string>()) foreach (var form in Forms(alias)) Name(form, entry);
        }

        foreach (var entry in _entries)
        {
            foreach (var target in entry.links ?? new List<string>())
            {
                if (!_byId.ContainsKey(target) || string.Equals(target, entry.id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!_backlinks.TryGetValue(target, out var list)) _backlinks[target] = list = new List<string>();
                if (!list.Contains(entry.id)) list.Add(entry.id);
            }
        }

        foreach (var age in ages ?? Array.Empty<LibraryAge>()) if (age != null && !string.IsNullOrEmpty(age.id)) _ages[age.id] = age;
    }

    private void Name(string name, LibraryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        var names = _byName[entry.game ? 1 : 0];
        // A section's heading is no name of its own ("Origins" is in many notes); "Note#Section" is.
        if (!string.IsNullOrEmpty(entry.parent) && name.IndexOf('#') < 0 && !string.Equals(name, entry.id, StringComparison.OrdinalIgnoreCase)) return;
        if (!names.ContainsKey(name)) names[name] = entry.id;
    }

    /// <summary>
    /// What prose writes for a name: plurals (Peaches, Treasuries), possessives, and a leading "The" (or its
    /// absence), as the website's lookups allow. Shared with the keyword masterfile.
    /// </summary>
    public static IEnumerable<string> Forms(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) yield break;
        yield return name + "s";
        if (Regex.IsMatch(name, "(s|sh|ch|x|z)$")) yield return name + "es";
        if (Regex.IsMatch(name, "[^aeiou]y$")) yield return name.Substring(0, name.Length - 1) + "ies";
        yield return name + "'s";
        yield return name + "’s";
        if (name.EndsWith("s", StringComparison.Ordinal)) yield return name.Substring(0, name.Length - 1);
        if (name.StartsWith("The ", StringComparison.Ordinal)) yield return name.Substring(4);
        else yield return "The " + name;
    }

    // ===== LOOKUP =====

    public LibraryEntry Get(string id) => id != null && _byId.TryGetValue(id, out var entry) ? entry : null;

    /// <summary>
    /// The entry a name means: a title, an alias, an id, "Note#Section" (falling back to the note), or a form prose
    /// writes them in. The Glossary is asked first, unless <paramref name="preferGame"/> (a Game Wiki article's own
    /// links), then the other half.
    /// </summary>
    public LibraryEntry Resolve(string name, bool preferGame = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        int first = preferGame ? 1 : 0;
        if (_byName[first].TryGetValue(name, out var id) || _byName[1 - first].TryGetValue(name, out id)) return Get(id);
        int hash = name.IndexOf('#');
        return hash > 0 ? Resolve(name.Substring(0, hash), preferGame) : null;
    }

    /// <summary>The entry of the same title in the other half of the Library (the lore of a game term, or its rules), or null.</summary>
    public LibraryEntry OtherHalf(LibraryEntry entry)
    {
        if (entry == null || !string.IsNullOrEmpty(entry.parent)) return null;
        var names = _byName[entry.game ? 0 : 1];
        // By title, then by its other names ("Waltz" is the Glossary's "Waltz Pillar").
        var candidates = new List<string> { entry.title };
        if (entry.aliases != null) candidates.AddRange(entry.aliases);
        foreach (var name in candidates)
        {
            if (!string.IsNullOrWhiteSpace(name) && names.TryGetValue(name.Trim(), out var id) && Get(id) is LibraryEntry other && string.IsNullOrEmpty(other.parent)) return other;
        }
        return null;
    }

    /// <summary>
    /// The words that light up by themselves in tooltip text: every Glossary title and alias, except sections (reached
    /// through their note) and quiet entries (ordinary words, reached by [[links]] only).
    /// </summary>
    public IEnumerable<(string word, LibraryEntry entry)> Words()
    {
        foreach (var entry in _entries)
        {
            if (entry.game || entry.quiet || !string.IsNullOrEmpty(entry.parent)) continue;
            yield return (entry.title, entry);
            foreach (var alias in entry.aliases ?? new List<string>()) yield return (alias, entry);
        }
    }

    public LibraryAge Age(string id) => id != null && _ages.TryGetValue(id, out var age) ? age : null;

    public IEnumerable<LibraryAge> Ages => _ages.Values;

    // ===== THE GRAPH =====

    /// <summary>Entries that link here.</summary>
    public List<LibraryEntry> Backlinks(string id)
    {
        var list = new List<LibraryEntry>();
        if (id == null || !_backlinks.TryGetValue(id, out var ids)) return list;
        foreach (var other in ids) if (_byId.TryGetValue(other, out var entry)) list.Add(entry);
        list.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    /// <summary>The sections of a longer note, in the note's order.</summary>
    public List<LibraryEntry> Sections(string id)
    {
        var list = new List<LibraryEntry>();
        var parent = Get(id);
        if (parent == null) return list;
        foreach (var target in parent.links ?? new List<string>())
        {
            var entry = Get(target);
            if (entry != null && string.Equals(entry.parent, parent.id, StringComparison.OrdinalIgnoreCase)) list.Add(entry);
        }
        return list;
    }

    /// <summary>
    /// The whole article of an entry: its own text, then each of its sections under its heading. (A long note keeps
    /// only its introduction as its own text; its sections are entries of their own.)
    /// </summary>
    public string Article(string id)
    {
        var entry = Get(id);
        if (entry == null) return string.Empty;
        var sb = new System.Text.StringBuilder(entry.body ?? string.Empty);
        foreach (var section in Sections(id))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("### ").Append(section.section ?? section.title).Append("\n\n").Append(section.body);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Related reading: entries linked both ways first (a real relationship), then this entry's links, then what
    /// links here, then shelf-mates to fill it out. The note's own sections and its parent are left out.
    /// </summary>
    public List<LibraryEntry> Related(string id, int limit = 8)
    {
        var result = new List<LibraryEntry>();
        var entry = Get(id);
        if (entry == null) return result;
        var outgoing = new HashSet<string>(entry.links ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var incoming = _backlinks.TryGetValue(id, out var list) ? list : new List<string>();

        void Add(string other)
        {
            if (result.Count >= limit || string.Equals(other, id, StringComparison.OrdinalIgnoreCase)) return;
            var candidate = Get(other);
            if (candidate == null || result.Contains(candidate)) return;
            if (string.Equals(candidate.parent, id, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.id, entry.parent, StringComparison.OrdinalIgnoreCase)) return;
            result.Add(candidate);
        }

        foreach (var other in incoming) if (outgoing.Contains(other)) Add(other);
        foreach (var other in entry.links ?? new List<string>()) Add(other);
        foreach (var other in incoming) Add(other);
        foreach (var sibling in _entries)
        {
            if (result.Count >= limit) break;
            if (sibling.shelf == entry.shelf && sibling.game == entry.game && string.IsNullOrEmpty(sibling.parent)) Add(sibling.id);
        }
        return result;
    }

    /// <summary>Every shelf of one half of the Library, with how many entries it holds, largest first.</summary>
    public List<(string shelf, int count)> Shelves(bool game)
    {
        var counts = new Dictionary<string, int>();
        foreach (var entry in _entries)
        {
            if (entry.game != game || !string.IsNullOrEmpty(entry.parent)) continue;
            string shelf = string.IsNullOrEmpty(entry.shelf) ? "Unfiled" : entry.shelf;
            counts[shelf] = counts.TryGetValue(shelf, out int n) ? n + 1 : 1;
        }
        var list = new List<(string shelf, int count)>();
        foreach (var pair in counts) list.Add((pair.Key, pair.Value));
        list.Sort((a, b) => a.count != b.count ? b.count.CompareTo(a.count) : string.Compare(a.shelf, b.shelf, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    /// <summary>The entries on a shelf, by title (sections are reached through their note).</summary>
    public List<LibraryEntry> OnShelf(string shelf, bool game)
    {
        var list = new List<LibraryEntry>();
        foreach (var entry in _entries)
        {
            if (entry.game != game || !string.IsNullOrEmpty(entry.parent)) continue;
            if (string.Equals(string.IsNullOrEmpty(entry.shelf) ? "Unfiled" : entry.shelf, shelf, StringComparison.OrdinalIgnoreCase)) list.Add(entry);
        }
        return list;
    }

    /// <summary>
    /// Ranked search over titles, aliases and summaries. A title match dominates: someone typing "atonalis" wants
    /// the article, not every note that mentions it. Needs two letters.
    /// </summary>
    public List<LibraryEntry> Search(string query, bool? game = null, int limit = 40)
    {
        var result = new List<LibraryEntry>();
        string needle = (query ?? string.Empty).Trim();
        if (needle.Length < 2) return result;
        var scored = new List<(LibraryEntry entry, int score)>();
        foreach (var entry in _entries)
        {
            if (game.HasValue && entry.game != game.Value) continue;
            string title = entry.title ?? string.Empty;
            int score = 0;
            if (string.Equals(title, needle, StringComparison.OrdinalIgnoreCase)) score = 1000;
            else if (title.StartsWith(needle, StringComparison.OrdinalIgnoreCase)) score = 500 - title.Length;
            else if (title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) score = 250 - title.Length;
            else if ((entry.aliases ?? new List<string>()).Exists(a => a.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)) score = 150;
            else if ((entry.summary ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) score = 60;
            if (!string.IsNullOrEmpty(entry.parent)) score -= 20; // a note before its sections
            if (score > 0) scored.Add((entry, score));
        }
        scored.Sort((a, b) => a.score != b.score ? b.score.CompareTo(a.score) : string.Compare(a.entry.title, b.entry.title, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < scored.Count && i < limit; i++) result.Add(scored[i].entry);
        return result;
    }

    /// <summary>"Auric Aria" → "auric-aria": lower case, accents dropped, anything else a single dash.</summary>
    public static string Slug(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        var sb = new System.Text.StringBuilder(title.Length);
        foreach (char c in title.Normalize(System.Text.NormalizationForm.FormD))
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            char lower = char.ToLowerInvariant(c);
            if (lower >= 'a' && lower <= 'z' || lower >= '0' && lower <= '9') sb.Append(lower);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
