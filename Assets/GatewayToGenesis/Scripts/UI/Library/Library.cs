using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The White-Haven Library: the game's wiki, in two halves, as the Sonata website keeps its archive.
/// <list type="bullet">
/// <item>The <b>Glossary</b>: the lore, every note of the vault as written (Resources/Library/Library.json, which
/// the vault import writes: Tools > Gateway to Genesis > Import Lore From Vault).</item>
/// <item>The <b>Game Wiki</b>: how the game works. The hand-written articles of Resources/Library/GameWiki.md
/// (<see cref="LibraryArticles"/>), then an entry per resource, building, technology, section, pillar, council seat,
/// legend class and civic, written from the game's own data (<see cref="GameWikiEntries"/>).</item>
/// </list>
/// Tooltips never show an entry whole: they show the keyword's card (<see cref="Keywords"/>), which links here.
/// The Library is opened with L, or from a card's "Open in the White-Haven Library" (<see cref="LibraryWindow"/>).
/// </summary>
public static class Library
{
    public const string GlossaryPath = "Library/Library";
    public const string GameWikiPath = "Library/GameWiki";

    private static LibraryIndex _index;
    private static readonly List<string> _problems = new List<string>();

    /// <summary>Every entry of both halves.</summary>
    public static LibraryIndex Index
    {
        get
        {
            Ensure();
            return _index;
        }
    }

    /// <summary>What could not be loaded, for <see cref="ContentValidator"/>.</summary>
    public static IReadOnlyList<string> Problems
    {
        get
        {
            Ensure();
            return _problems;
        }
    }

    public static LibraryEntry Get(string id) => Index.Get(id);

    /// <summary>The entry a name means, Glossary first (see <see cref="LibraryIndex.Resolve"/>).</summary>
    public static LibraryEntry Resolve(string name, bool preferGame = false) => Index.Resolve(name, preferGame);

    /// <summary>The number of an Age by id ("age-of-embers" → 2, from the vault's "Ages 2"), or -1 when the Library does not know it.</summary>
    public static int AgeNumberOf(string ageId)
    {
        var age = Index.Age(ageId);
        return age != null ? age.number : -1;
    }

    /// <summary>Open the Library at entry <paramref name="id"/> (null: its front page).</summary>
    public static void Open(string id = null) => LibraryWindow.Open(id);

    // Statics survive between play sessions when domain reload is disabled; the files may have changed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate()
    {
        _index = null;
        _problems.Clear();
    }

    private static void Ensure()
    {
        if (_index != null) return;
        _problems.Clear();
        var entries = new List<LibraryEntry>();
        List<LibraryAge> ages = null;

        string json = GameCatalog.LoadText(GlossaryPath);
        if (json == null) _problems.Add($"The Library has no Glossary: Resources/{GlossaryPath}.json is missing (run Tools > Gateway to Genesis > Import Lore From Vault)");
        else
        {
            try
            {
                var file = JsonUtility.FromJson<LibraryFile>(json);
                if (file?.entries != null) entries.AddRange(file.entries);
                ages = file?.ages;
            }
            catch (Exception e)
            {
                _problems.Add($"Resources/{GlossaryPath}.json could not be read: {e.Message}");
            }
        }
        foreach (var entry in entries) entry.game = false;

        // The Game Wiki: a hand-written article takes the place of the entry written from the same data.
        var game = LibraryArticles.Parse(GameCatalog.LoadText(GameWikiPath));
        var written = new HashSet<string>(game.Select(e => e.id), StringComparer.OrdinalIgnoreCase);
        try { game.AddRange(GameWikiEntries.FromGame().Where(e => !written.Contains(e.id))); }
        catch (Exception e)
        {
            _problems.Add($"The Game Wiki could not be written from the game's data: {e.Message}");
        }
        entries.AddRange(game);

        // Game Wiki articles link by name; their links resolve once every entry is known, their own half first.
        var names = new LibraryIndex(entries, ages);
        foreach (var entry in game)
        {
            entry.links.Clear();
            foreach (var target in KeywordMarkup.WikiTargets(entry.body))
            {
                var linked = names.Resolve(target, preferGame: true);
                if (linked != null && linked != entry && !entry.links.Contains(linked.id)) entry.links.Add(linked.id);
            }
        }
        _index = new LibraryIndex(entries, ages);
        foreach (var duplicate in _index.Duplicates) _problems.Add("Library: " + duplicate + ": only the first is kept");
        GameLog.Event($"The White-Haven Library holds {entries.Count - game.Count} Glossary entries and {game.Count} Game Wiki entries", LogChannel.UI);
    }
}
