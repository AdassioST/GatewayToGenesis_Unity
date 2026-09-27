using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// The golden words of tooltips, and the one place tooltip text is linked to them. The same two layers as the
/// Sonata website, kept apart on purpose:
/// <list type="bullet">
/// <item><b>Cards</b> (Resources/Keywords/Keywords.md, <see cref="KeywordMasterfile"/>): what a hovered word shows.
/// Short and hand-written, staged by Age and by the civics in force, so a card never knows more than the world does
/// yet.</item>
/// <item><b>The White-Haven Library</b> (<see cref="Library"/>): the whole of every entry. A card links there; a word
/// with an entry but no card shows the entry's summary, as the website falls back to the article's opening.</item>
/// </list>
/// Game terms are keywords by themselves (pillars, aspects, resources, technologies, sections, the calendar), with
/// live numbers from <see cref="KeywordLiveValues"/>; their card and entry are found by name. A word means, in order:
/// a card, a game term, a Library entry.
/// </summary>
public static class Keywords
{
    public const string MasterfilePath = "Keywords/Keywords";
    public const string LibraryLinkText = "Open in the White-Haven Library";
    public const string DeeperText = "Its meaning deepens in a later Age.";

    private static KeywordMasterfile _cards;
    private static Dictionary<string, string> _gameWords;  // a game term's words → its id ("Waltz" → "pillar:waltz")
    private static Dictionary<string, string> _auto;       // exact words that light up by themselves → id
    private static Regex _autoRegex;
    private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> _markdown = new Dictionary<string, string>();

    // Statics survive between play sessions when domain reload is disabled; content may have changed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate()
    {
        _cards = null;
        _gameWords = null;
        _auto = null;
        _autoRegex = null;
        _cache.Clear();
        _markdown.Clear();
    }

    /// <summary>The keyword masterfile.</summary>
    public static KeywordMasterfile Cards
    {
        get
        {
            EnsureIndex();
            return _cards;
        }
    }

    // ===== LOOKUP =====

    /// <summary>The keyword id <paramref name="term"/> means (a word, a card or entry id, a game id), or null.</summary>
    public static string Resolve(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return null;
        EnsureIndex();
        term = term.Trim();
        var card = _cards.Find(term) ?? _cards.ById(term);
        if (card != null) return card.Id;
        if (_gameWords.TryGetValue(term, out var gameId)) return gameId;
        var entry = Library.Resolve(term);
        if (entry != null) return entry.id;
        // A game id written out, e.g. [[resource:food]].
        return KeywordLiveValues.Exists(term) ? term.ToLowerInvariant() : null;
    }

    // The card and Library entry behind a keyword id or word, and the game term it is bound to.
    private static void Lookup(string key, out KeywordCard card, out LibraryEntry entry, out string gameId)
    {
        key = key.Trim();
        gameId = null;
        card = _cards.ById(key) ?? _cards.ForWiki(key) ?? _cards.Find(key);
        if (card == null && _gameWords.TryGetValue(key, out var bound)) key = bound;
        if (card != null && card.gameId != null) gameId = card.gameId;
        else if (KeywordLiveValues.Exists(key)) gameId = key.ToLowerInvariant();

        // "resource:food" is also known by its name.
        string name = gameId != null ? NameOf(gameId) : null;
        if (card == null && name != null) card = _cards.Find(name);
        entry = null;
        if (card != null && card.wiki != null) entry = Library.Get(card.wiki);
        if (entry == null) entry = Library.Get(key) ?? Library.Resolve(key);
        if (entry == null && name != null) entry = Library.Resolve(name);
        if (entry == null && card != null) entry = Library.Resolve(card.title);
    }

    // "resource:food" → "food"; "time:ritual seventh" → "ritual seventh".
    private static string NameOf(string gameId)
    {
        int colon = gameId.IndexOf(':');
        return colon >= 0 ? gameId.Substring(colon + 1).Replace('-', ' ') : gameId;
    }

    /// <summary>Where the world stands, for a card's reading: the Age and the civics in force.</summary>
    public static KeywordContext Context()
    {
        var civics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manager = CivicManager.Instance;
        if (manager != null)
        {
            foreach (var civic in manager.GetAllActiveCivics()) if (civic != null) civics.Add(LibraryIndex.Slug(civic.civicName));
        }
        return new KeywordContext(GameAge.Id, GameAge.Number, civics);
    }

    /// <summary>What <paramref name="card"/> says now, or <paramref name="fallback"/> when it says nothing.</summary>
    public static string Reading(KeywordCard card, string fallback = "") => KeywordMasterfile.Reading(card, Context(), fallback);

    // ===== LINKING =====

    /// <summary>
    /// Highlight every keyword in <paramref name="text"/> and resolve its [[links]]; <paramref name="selfId"/> is
    /// not linked (a keyword's own tooltip). Tags and existing links are left alone, and characters the game font
    /// cannot draw are fixed (<see cref="KeywordMarkup.SafeGlyphs"/>). Results are cached.
    /// </summary>
    public static string Linkify(string text, string selfId = null)
    {
        if (string.IsNullOrEmpty(text)) return text;
        EnsureIndex();
        string cacheKey = selfId + "\u0001" + text;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        string linked = KeywordMarkup.Link(text, Resolve, _autoRegex, word => _auto.TryGetValue(word, out var id) ? id : null, selfId);
        linked = KeywordMarkup.SafeGlyphs(linked);
        if (_cache.Count > 1024) _cache.Clear();
        _cache[cacheKey] = linked;
        return linked;
    }

    // ===== TOOLTIPS =====

    /// <summary>
    /// Fill <paramref name="d"/> with the card of keyword <paramref name="id"/>: its title and kind, what it says in
    /// this Age (or its Library entry's summary), live numbers for a game term, and the way into the Library. False
    /// for an unknown keyword.
    /// </summary>
    public static bool TryBuild(string id, TooltipData d)
    {
        d.Clear();
        if (string.IsNullOrWhiteSpace(id)) return false;
        EnsureIndex();
        Lookup(id, out var card, out var entry, out var gameId);
        var live = new TooltipData();
        bool hasLive = gameId != null && KeywordLiveValues.TryFill(gameId, live);
        if (card == null && entry == null && !hasLive) return false;

        d.keywordId = id;
        d.title = card?.title ?? (hasLive ? live.title : null) ?? entry?.title;
        d.type = card?.category ?? (hasLive ? live.type : null) ?? entry?.category ?? "Keyword";
        if (hasLive)
        {
            d.description = live.description;
            d.effects = live.effects;
        }
        string reading = Reading(card, entry?.summary);
        if (!string.IsNullOrWhiteSpace(reading) && !SameText(reading, d.description)) d.summary = Markdown(reading);
        else if (hasLive) d.summary = live.summary;
        d.related = Footer(card, entry);
        return true;
    }

    /// <summary>
    /// Add a game object's lore to its tooltip: its card's reading (or its Glossary entry's summary) unless
    /// <paramref name="includeSummary"/> is false or the tooltip already has one, and the way into the Library. The
    /// object keeps its own title, type and flavour. <paramref name="name"/> is a keyword id or any of its words
    /// ("resource:aetherlight", "Great Sovereign"). False when it has no card and no Glossary entry: a Game Wiki
    /// entry only says what the object's tooltip already shows.
    /// </summary>
    public static bool AddLore(TooltipData d, string name, bool includeSummary = true)
    {
        if (d == null || string.IsNullOrWhiteSpace(name)) return false;
        EnsureIndex();
        Lookup(name, out var card, out var entry, out _);
        if (card == null && (entry == null || entry.game)) return false;
        // The object's own words are not linked to itself.
        d.keywordId = Resolve(name) ?? card?.Id ?? entry.id;
        string reading = Reading(card, entry != null && !entry.game ? entry.summary : null);
        if (includeSummary && string.IsNullOrEmpty(d.summary) && !string.IsNullOrWhiteSpace(reading) && !SameText(reading, d.description))
        {
            d.summary = Markdown(reading);
        }
        d.related = Footer(card, entry);
        return true;
    }

    // Under the card: that a later Age revises it, and the way into the Library.
    private static string Footer(KeywordCard card, LibraryEntry entry)
    {
        var lines = new List<string>();
        if (card != null && KeywordMasterfile.HasDeeper(card, Context(), Library.AgeNumberOf)) lines.Add(TooltipText.Muted($"<i>{DeeperText}</i>"));
        if (entry != null) lines.Add(TooltipText.LibraryLink(entry.id, LibraryLinkText));
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    private static bool SameText(string markdown, string shown) =>
        !string.IsNullOrEmpty(shown) && string.Equals(LibraryArticles.Plain(markdown), Regex.Replace(shown, "<[^>]+>", string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    // Authored text as rich text, converted once: open tooltips rebuild several times a second.
    private static string Markdown(string text)
    {
        text = text.Trim();
        if (_markdown.TryGetValue(text, out var converted)) return converted;
        converted = KeywordMarkup.FromMarkdown(text);
        if (_markdown.Count > 512) _markdown.Clear();
        _markdown[text] = converted;
        return converted;
    }

    // ===== INDEX =====

    private static void EnsureIndex()
    {
        if (_cards != null) return;
        string markdown = null;
        try { markdown = GameCatalog.LoadText(MasterfilePath); }
        catch (Exception e) { GameLog.Warning($"The keyword masterfile could not be loaded: {e.Message}", LogChannel.UI); }
        _cards = KeywordMasterfile.Parse(markdown);
        _gameWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _auto = new Dictionary<string, string>(StringComparer.Ordinal);

        // Cards first: their words win.
        foreach (var card in _cards.Cards)
        {
            foreach (var word in card.Names()) if (card.autoLink && !_auto.ContainsKey(word)) _auto[word] = card.Id;
        }

        // Game terms.
        foreach (var pillar in StatDefinitions.Pillars) Game(KeywordLiveValues.Title(pillar), "pillar:" + pillar, true);
        foreach (var substat in StatDefinitions.Substats) Game(KeywordLiveValues.Title(substat), "stat:" + substat, true);
        Game("Seventh", "time:seventh", true);
        Game("Sevenths", "time:seventh", true);
        Game("Ritual Seventh", "time:ritual seventh", true);
        // Ordinary words: reached by [[links]] only.
        Game("Phase", "time:phase", false);
        Game("Echo", "time:echo", false);
        Game("Cycle", "time:cycle", false);
        try
        {
            foreach (var resource in GameCatalog.Resources.Keys) Game(resource, "resource:" + resource.ToLowerInvariant(), true);
            foreach (var tech in GameCatalog.Technologies.Keys) Game(tech, "tech:" + tech.ToLowerInvariant(), true);
            foreach (var section in GameCatalog.Sections.Keys) Game(section, "section:" + section.ToLowerInvariant(), true);
        }
        catch (Exception e)
        {
            GameLog.Warning($"Keywords could not read the game catalogs: {e.Message}", LogChannel.UI);
        }

        // The Glossary's words (quiet ones, like Void or Echo, only through [[links]]).
        try
        {
            foreach (var (word, entry) in Library.Index.Words()) if (!_auto.ContainsKey(word)) _auto[word] = entry.id;
        }
        catch (Exception e)
        {
            GameLog.Warning($"Keywords could not read the White-Haven Library: {e.Message}", LogChannel.UI);
        }

        var words = _auto.Keys.Where(w => w.Length > 2).OrderByDescending(w => w.Length).Select(Regex.Escape).ToList();
        // Longest first, so "Critical Failure" wins over a shorter overlapping word.
        _autoRegex = words.Count > 0 ? new Regex($@"(?<![\w'])(?:{string.Join("|", words)})(?![\w'])", RegexOptions.Compiled) : null;
    }

    private static void Game(string word, string id, bool auto)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        if (!_gameWords.ContainsKey(word)) _gameWords[word] = id;
        if (auto && !_auto.ContainsKey(word)) _auto[word] = id;
    }
}
