using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The reading room of the White-Haven Library (<see cref="Library"/>): the game's wiki, laid out like the Sonata
/// website's archive. Two halves, the <b>Glossary</b> (the lore) and the <b>Game Wiki</b> (how the game works), each
/// with its shelves and a search; the article shows the whole entry, the notes it is split into, "Threads from
/// here" (related reading) and "Mentioned in" (backlinks). Links in an article open their entry, and hovering one
/// shows its keyword card, as anywhere else. Back and Forward walk the pages read.
///
/// L opens and closes it (unless a text field has the keyboard), Esc closes it, and a card's "Open in the
/// White-Haven Library" opens it at that entry. Built in code on a canvas of its own, just under the tooltips, in
/// the tooltips' look (<see cref="TooltipTheme"/>); no scene setup is needed. While it is open the HUD tabs ignore
/// their hotkeys (<see cref="TabHotkeys"/>).
/// </summary>
public class LibraryWindow : MonoBehaviour, ITooltipSource
{
    private const string Title = "The White-Haven Library";
    private const string ShelfLink = "shelf:", EntryLink = "entry:", GlossaryTab = "Glossary", GameTab = "Game Wiki";
    private const int ChunkChars = 2400;     // visible characters per text block of an article (a TMP mesh holds ~16k)
    private const int BacklinkLimit = 24;
    private const float FadeTime = 0.12f;

    // Where the front page starts reading: the threads everything else hangs off, as on the website.
    private static readonly string[] FeaturedGlossary = { "Age of Desolation", "The Inescapable Hunger", "Old World Remnants", "Auric Peach", "Pillars", "Cycle" };

    private static LibraryWindow _instance;

    /// <summary>True while the Library is on screen.</summary>
    public static bool IsOpen => _instance != null && _instance._open;

    private TooltipTheme _theme;
    private bool _built, _open;
    private float _fade;
    private Canvas _canvas;
    private CanvasGroup _group;
    private TextMeshProUGUI _glossaryTab, _gameTab, _backButton, _forwardButton, _indexText;
    private TMP_InputField _search;
    private ScrollRect _indexScroll, _articleScroll;
    private RectTransform _articleContent;
    private readonly List<TextMeshProUGUI> _blocks = new List<TextMeshProUGUI>();
    private TooltipTrigger _articleTrigger;

    // What is shown.
    private bool _game;
    private LibraryEntry _entry;
    private string _openShelf, _query = string.Empty;
    private readonly List<string> _history = new List<string>();
    private int _historyAt = -1;

    // Pointer.
    private string _hoverLink, _cardLink;
    private TMP_Text _hoverText;
    private float _awayFromCardSince = -1f;

    // ===== OPENING =====

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("White-Haven Library");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LibraryWindow>();
    }

    /// <summary>Open the Library at entry <paramref name="entryId"/> (null: where it was left, or its front page).</summary>
    public static void Open(string entryId = null)
    {
        Bootstrap();
        _instance.Show(entryId);
    }

    public static void Close()
    {
        if (_instance != null) _instance.Hide();
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // Keys come through GameInput (L toggles, Escape closes), like every other hotkey.
    private void Awake()
    {
        GameInput.LibraryPressed += Toggle;
        GameInput.CancelPressed += Hide;
    }

    private void OnDestroy()
    {
        GameInput.LibraryPressed -= Toggle;
        GameInput.CancelPressed -= Hide;
        if (_instance == this) _instance = null;
    }

    private void Show(string entryId)
    {
        if (!_built) Build();
        if (!_built) return;
        if (!_open)
        {
            _open = true;
            _canvas.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _group.interactable = true;
            var tooltips = TooltipSystemLogic.Instance;
            if (tooltips != null) tooltips.HideTooltip();
            Achievements.Report(AchievementEvent.Of(AchievementSignal.LibraryOpened).From("library:opened"));
        }
        if (entryId != null) Navigate(Library.Get(entryId) ?? Library.Resolve(entryId), push: true);
        else if (_historyAt < 0) Navigate(null, push: true);
        else Render();
    }

    private void Hide()
    {
        if (!_open) return;
        _open = false;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        ReleaseCard();
        if (_search != null && _search.isFocused) _search.DeactivateInputField();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ===== FRAME =====

    private void Update()
    {
        if (_built)
        {
            float target = _open ? 1f : 0f;
            if (!Mathf.Approximately(_fade, target))
            {
                _fade = Mathf.MoveTowards(_fade, target, Time.unscaledDeltaTime / FadeTime);
                _group.alpha = _fade;
                if (!_open && _fade <= 0f) _canvas.gameObject.SetActive(false);
            }
        }
        if (!_open) return;

        Vector2 pointer = InputUtils.MousePosition;
        var tooltips = TooltipSystemLogic.Instance;
        bool overCard = tooltips != null && tooltips.PointerOverSolidTooltip;

        string indexLink = overCard || !Visible(_indexScroll, pointer) ? null : LinkAt(_indexText, pointer);
        TMP_Text articleText = null;
        string articleLink = overCard || indexLink != null ? null : ArticleLinkAt(pointer, out articleText);
        SetHover(indexLink ?? articleLink, indexLink != null ? _indexText : articleLink != null ? articleText : null);
        UpdateCard(articleLink, overCard, tooltips);

        if (!InputUtils.LeftClickDown || overCard) return;
        if (indexLink != null) OnIndexLink(indexLink);
        else if (articleLink != null) Navigate(Library.Get(articleLink), push: true);
    }

    // A hovered link in the article shows its keyword card, as in any tooltip. The card stays while the pointer is on
    // the link or inside the card (once solid), and closes a moment after it leaves both.
    private void UpdateCard(string link, bool overCard, TooltipSystemLogic tooltips)
    {
        if (tooltips == null || _articleTrigger == null) return;
        if (link != null)
        {
            _awayFromCardSince = -1f;
            // The same link keeps its card, unless the tooltips closed it meanwhile.
            if (link == _cardLink && tooltips.Current == _articleTrigger) return;
            // Another link: its card opens at the pointer, fluid, as a fresh tooltip does.
            _cardLink = link;
            tooltips.Release(_articleTrigger);
            tooltips.Show(_articleTrigger);
            return;
        }
        if (_cardLink == null || overCard)
        {
            _awayFromCardSince = -1f;
            return;
        }
        if (_awayFromCardSince < 0f) _awayFromCardSince = Time.unscaledTime;
        if (Time.unscaledTime - _awayFromCardSince >= (_theme != null ? _theme.closeGrace : 0.35f)) ReleaseCard();
    }

    private void ReleaseCard()
    {
        _cardLink = null;
        _awayFromCardSince = -1f;
        var tooltips = TooltipSystemLogic.Instance;
        if (tooltips != null && _articleTrigger != null) tooltips.Release(_articleTrigger);
    }

    /// <summary>The card of the article link under the pointer (the article is the trigger).</summary>
    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data)
    {
        if (trigger != _articleTrigger || _cardLink == null || !_open) return false;
        return Keywords.TryBuild(_cardLink, data);
    }

    // ===== NAVIGATION =====

    private void Navigate(LibraryEntry entry, bool push)
    {
        if (push)
        {
            string id = entry?.id;
            if (_historyAt >= 0 && _historyAt < _history.Count && _history[_historyAt] == id) push = false;
            else
            {
                if (_historyAt < _history.Count - 1) _history.RemoveRange(_historyAt + 1, _history.Count - _historyAt - 1);
                _history.Add(id);
                _historyAt = _history.Count - 1;
            }
        }
        _entry = entry;
        if (entry != null)
        {
            _game = entry.game;
            var shelved = !string.IsNullOrEmpty(entry.parent) ? Library.Get(entry.parent) ?? entry : entry;
            _openShelf = ShelfOf(shelved);
        }
        ReleaseCard();
        Render();
        if (_articleScroll != null) _articleScroll.verticalNormalizedPosition = 1f;
    }

    private void Walk(int step)
    {
        int to = _historyAt + step;
        if (to < 0 || to >= _history.Count) return;
        _historyAt = to;
        Navigate(_history[to] != null ? Library.Get(_history[to]) : null, push: false);
    }

    private void OnIndexLink(string link)
    {
        if (link.StartsWith(ShelfLink, StringComparison.Ordinal))
        {
            string shelf = link.Substring(ShelfLink.Length);
            _openShelf = _openShelf == shelf ? null : shelf;
            RenderIndex();
        }
        else if (link.StartsWith(EntryLink, StringComparison.Ordinal))
        {
            Navigate(Library.Get(link.Substring(EntryLink.Length)), push: true);
        }
    }

    private void SwitchHalf(bool game)
    {
        if (_game == game) return;
        _game = game;
        _openShelf = null;
        // The front page belongs to one half; an article stays open until another is chosen.
        if (_entry == null) Render();
        else RenderIndex();
        RenderTabs();
    }

    private void OnSearch(string query)
    {
        _query = query ?? string.Empty;
        RenderIndex();
    }

    private static string ShelfOf(LibraryEntry entry) => string.IsNullOrEmpty(entry.shelf) ? "Unfiled" : entry.shelf;

    // ===== RENDERING =====

    private void Render()
    {
        RenderTabs();
        RenderIndex();
        RenderArticle();
        _backButton.alpha = _historyAt > 0 ? 1f : 0.35f;
        _forwardButton.alpha = _historyAt < _history.Count - 1 ? 1f : 0.35f;
    }

    private void RenderTabs()
    {
        Paint(_glossaryTab, !_game);
        Paint(_gameTab, _game);
    }

    private void Paint(TextMeshProUGUI tab, bool selected)
    {
        tab.color = selected ? _theme.titleColor : _theme.subtitleColor;
        tab.fontStyle = selected ? FontStyles.SmallCaps | FontStyles.Underline : FontStyles.SmallCaps;
    }

    private void RenderIndex()
    {
        var index = Library.Index;
        var sb = new StringBuilder();
        string query = _query.Trim();
        if (query.Length == 1) sb.Append(TooltipText.Muted("Keep typing: search needs two letters."));
        else if (query.Length >= 2)
        {
            var results = index.Search(query, _game);
            sb.Append(TooltipText.Heading($"Search: {Escape(query)}", TooltipText.Muted(results.Count.ToString())));
            if (results.Count == 0) sb.Append('\n').Append(TooltipText.Muted("Nothing on these shelves answers to that."));
            foreach (var entry in results)
            {
                string category = !string.IsNullOrEmpty(entry.parent) ? Library.Get(entry.parent)?.title : entry.category;
                sb.Append('\n').Append(IndexLine(entry, category));
            }
        }
        else
        {
            foreach (var (shelf, count) in index.Shelves(_game))
            {
                bool open = shelf == _openShelf;
                if (sb.Length > 0) sb.Append('\n');
                string marker = open ? "- " : "+ ";
                sb.Append($"<link=\"{ShelfLink}{shelf}\"><color={TooltipText.HeadingHex}><noparse>{marker}</noparse><smallcaps>{Escape(shelf)}</smallcaps></color></link>  ")
                  .Append(TooltipText.Muted(count.ToString()));
                if (!open) continue;
                foreach (var entry in index.OnShelf(shelf, _game)) sb.Append('\n').Append("<indent=1.2em>").Append(IndexLine(entry, null)).Append("</indent>");
            }
            if (sb.Length == 0) sb.Append(TooltipText.Muted(_game ? "The Game Wiki is empty." : "The Glossary is empty: import the vault (Tools > Gateway to Genesis > Import Lore From Vault)."));
        }
        _indexText.text = KeywordMarkup.SafeGlyphs(sb.ToString());
        _hoverText = null;
        _hoverLink = null;
    }

    private string IndexLine(LibraryEntry entry, string note)
    {
        bool current = _entry != null && (_entry.id == entry.id);
        string color = current ? TooltipText.ValueHex : TooltipText.LinkHex;
        string line = $"<link=\"{EntryLink}{entry.id}\"><color={color}>{Escape(entry.title)}</color></link>";
        return string.IsNullOrEmpty(note) ? line : line + "  " + TooltipText.Muted($"<size=85%>{Escape(note)}</size>");
    }

    private void RenderArticle()
    {
        var parts = new List<(string text, TextKind kind)>();
        if (_entry == null) FrontPage(parts);
        else Article(_entry, parts);

        // Long articles are cut at paragraph breaks into several text blocks (one TMP mesh holds ~16k characters).
        var blocks = new List<(string text, TextKind kind)>();
        foreach (var (text, kind) in parts)
        {
            if (string.IsNullOrEmpty(text)) continue;
            if (kind != TextKind.Body) blocks.Add((KeywordMarkup.SafeGlyphs(text), kind));
            else foreach (var chunk in Chunks(text)) blocks.Add((KeywordMarkup.SafeGlyphs(chunk), kind));
        }
        while (_blocks.Count < blocks.Count) _blocks.Add(ArticleText());
        for (int i = 0; i < _blocks.Count; i++)
        {
            var label = _blocks[i];
            bool used = i < blocks.Count;
            label.gameObject.SetActive(used);
            if (!used) continue;
            Style(label, blocks[i].kind);
            label.text = blocks[i].text;
        }
        _hoverText = null;
        _hoverLink = null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_articleContent);
    }

    private enum TextKind { Title, Meta, Epigraph, Body }

    private void Style(TextMeshProUGUI label, TextKind kind)
    {
        switch (kind)
        {
            case TextKind.Title:
                label.fontSize = _theme.titleSize * 1.5f;
                label.color = _theme.titleColor;
                label.fontStyle = FontStyles.Normal;
                break;
            case TextKind.Meta:
                label.fontSize = _theme.subtitleSize + 2f;
                label.color = _theme.subtitleColor;
                label.fontStyle = FontStyles.SmallCaps;
                break;
            case TextKind.Epigraph:
                label.fontSize = _theme.flavorSize + 2f;
                label.color = _theme.flavorColor;
                label.fontStyle = FontStyles.Italic;
                break;
            default:
                label.fontSize = _theme.bodySize + 2f;
                label.color = _theme.bodyColor;
                label.fontStyle = FontStyles.Normal;
                break;
        }
    }

    private void FrontPage(List<(string, TextKind)> parts)
    {
        var index = Library.Index;
        parts.Add((Title, TextKind.Title));
        parts.Add((_game ? "The Game Wiki: how the world is kept" : "The open shelves", TextKind.Meta));
        var library = index.Resolve(Title);
        if (!_game && library != null && !string.IsNullOrEmpty(library.epigraph)) parts.Add((Markdown(library.epigraph, library), TextKind.Epigraph));

        int glossary = index.Entries.Count(e => !e.game), game = index.Entries.Count(e => e.game);
        var sb = new StringBuilder();
        sb.Append("Books rest on threads of solidified coherence. Choose a memory, and a chamber unfolds.");
        sb.Append("\n\n").Append(TooltipText.Muted($"{glossary} entries in the Glossary, {game} in the Game Wiki. Search above, pick a shelf, or start from one of the threads below."));
        IEnumerable<LibraryEntry> featured = _game
            ? index.Entries.Where(e => e.game && e.source == "GameWiki.md")
            : FeaturedGlossary.Select(name => index.Resolve(name)).Where(e => e != null && !e.game);
        foreach (var entry in featured.Distinct())
        {
            sb.Append("\n\n").Append(TooltipText.Link(entry.id, $"<b>{Escape(entry.title)}</b>"));
            string summary = LibraryArticles.Plain(entry.summary);
            if (summary.Length > 0) sb.Append('\n').Append(Escape(Shorten(summary, 180)));
        }
        parts.Add((sb.ToString(), TextKind.Body));
    }

    private void Article(LibraryEntry entry, List<(string, TextKind)> parts)
    {
        var index = Library.Index;
        var parent = !string.IsNullOrEmpty(entry.parent) ? index.Get(entry.parent) : null;
        parts.Add((Escape(entry.title), TextKind.Title));

        var meta = new List<string>();
        if (parent != null) meta.Add(TooltipText.Link(parent.id, Escape(parent.title)));
        else if (!string.IsNullOrEmpty(entry.category)) meta.Add(Escape(entry.category));
        if (parent == null && !string.IsNullOrEmpty(entry.shelf) && entry.shelf != entry.category) meta.Add(Escape(entry.shelf));
        meta.Add(entry.game ? GameTab : GlossaryTab);
        parts.Add((string.Join(TooltipText.Separator, meta), TextKind.Meta));

        if (!string.IsNullOrWhiteSpace(entry.epigraph)) parts.Add((Markdown(entry.epigraph, entry), TextKind.Epigraph));

        // A long note: its sections first, as a table of contents (each opens on its own), then the whole note.
        var sections = index.Sections(entry.id);
        if (sections.Count > 0)
        {
            var contents = new StringBuilder(TooltipText.Heading("In this note"));
            foreach (var section in sections) contents.Append('\n').Append(TooltipText.Bullet(TooltipText.Link(section.id, Escape(section.section ?? section.title))));
            parts.Add((contents.ToString(), TextKind.Body));
        }
        string body = index.Article(entry.id);
        if (string.IsNullOrWhiteSpace(body)) body = entry.summary;
        parts.Add((!string.IsNullOrWhiteSpace(body) ? Markdown(body, entry) : TooltipText.Muted("This thread is catalogued but not yet transcribed."), TextKind.Body));

        var sb = new StringBuilder();
        var other = index.OtherHalf(entry);
        if (other != null)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(TooltipText.Muted(other.game ? "How it works, in the Game Wiki:  " : "Its lore, in the Glossary:  ")).Append(TooltipText.Link(other.id, Escape(other.title)));
        }
        var related = index.Related(entry.id);
        if (related.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(TooltipText.Heading("Threads from here")).Append('\n');
            sb.Append(string.Join(TooltipText.Separator, related.Select(r => TooltipText.Link(r.id, Escape(r.title)))));
        }
        var backlinks = index.Backlinks(entry.id);
        if (backlinks.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(TooltipText.Heading("Mentioned in", TooltipText.Muted(backlinks.Count.ToString()))).Append('\n');
            sb.Append(string.Join(TooltipText.Separator, backlinks.Take(BacklinkLimit).Select(r => TooltipText.Link(r.id, Escape(r.title)))));
            if (backlinks.Count > BacklinkLimit) sb.Append(TooltipText.Muted($"  and {backlinks.Count - BacklinkLimit} more"));
        }
        if (sb.Length > 0) parts.Add((sb.ToString(), TextKind.Body));
    }

    // An entry's Markdown as rich text, its [[links]] opening entries (its own half first; the entry is not linked to itself).
    private static string Markdown(string markdown, LibraryEntry entry)
    {
        string text = KeywordMarkup.FromMarkdown(markdown.Trim());
        return KeywordMarkup.Link(text, name => Library.Resolve(name, entry != null && entry.game)?.id, null, null, entry?.id);
    }

    // Split at paragraph breaks into pieces of about ChunkChars visible characters.
    private static IEnumerable<string> Chunks(string text)
    {
        if (TooltipText.VisibleLength(text) <= ChunkChars)
        {
            yield return text;
            yield break;
        }
        var chunk = new StringBuilder();
        int visible = 0;
        foreach (var paragraph in text.Split(new[] { "\n\n" }, StringSplitOptions.None))
        {
            int length = TooltipText.VisibleLength(paragraph);
            if (chunk.Length > 0 && visible + length > ChunkChars)
            {
                yield return chunk.ToString();
                chunk.Clear();
                visible = 0;
            }
            if (chunk.Length > 0) chunk.Append("\n\n");
            chunk.Append(paragraph);
            visible += length;
        }
        if (chunk.Length > 0) yield return chunk.ToString();
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, text.LastIndexOf(' ', max) > 0 ? text.LastIndexOf(' ', max) : max) + "...";

    // Text shown as written: a "<" must not open a tag.
    private static string Escape(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("<", "<noparse><</noparse>");

    // ===== POINTER =====

    private static string LinkAt(TMP_Text text, Vector2 pointer)
    {
        if (text == null || !text.gameObject.activeInHierarchy) return null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, pointer, null)) return null;
        int index = TMP_TextUtilities.FindIntersectingLink(text, pointer, null);
        return index >= 0 && index < text.textInfo.linkCount ? text.textInfo.linkInfo[index].GetLinkID() : null;
    }

    // Scrolled text reaches past its viewport, where it is hidden: only what shows can be pointed at.
    private static bool Visible(ScrollRect scroll, Vector2 pointer) =>
        scroll != null && RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, null);

    private string ArticleLinkAt(Vector2 pointer, out TMP_Text found)
    {
        found = null;
        if (!Visible(_articleScroll, pointer)) return null;
        foreach (var block in _blocks)
        {
            string link = LinkAt(block, pointer);
            if (link == null) continue;
            found = block;
            return link;
        }
        return null;
    }

    // Brighten the hovered link, as tooltips do.
    private void SetHover(string link, TMP_Text text)
    {
        if (link == _hoverLink && text == _hoverText) return;
        if (_hoverText != null) _hoverText.ForceMeshUpdate();
        _hoverLink = link;
        _hoverText = text;
        if (link == null || text == null) return;
        var info = text.textInfo;
        bool touched = false;
        for (int l = 0; l < info.linkCount; l++)
        {
            if (info.linkInfo[l].GetLinkID() != link) continue;
            var span = info.linkInfo[l];
            for (int c = span.linkTextfirstCharacterIndex; c < span.linkTextfirstCharacterIndex + span.linkTextLength && c < info.characterCount; c++)
            {
                var character = info.characterInfo[c];
                if (!character.isVisible) continue;
                var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                for (int v = 0; v < 4; v++) colors[character.vertexIndex + v] = _theme.linkHover;
                touched = true;
            }
        }
        if (touched) text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    // ===== BUILD =====

    private void Build()
    {
        _theme = GameCatalog.UiThemes.Get("TooltipTheme", nameof(LibraryWindow));
        if (_theme == null) return;

        // A canvas of its own, just under the tooltips' (so cards open over the Library), scaled like theirs.
        var host = new GameObject("Library Canvas", typeof(RectTransform));
        host.SetActive(false);
        host.transform.SetParent(transform, false);
        _canvas = host.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var tooltips = TooltipSystemLogic.Instance;
        var tooltipCanvas = tooltips != null ? tooltips.GetComponentInParent<Canvas>() : null;
        _canvas.sortingOrder = tooltipCanvas != null ? Mathf.Max(0, tooltipCanvas.sortingOrder - 1) : 30000;
        var scaler = host.AddComponent<CanvasScaler>();
        var reference = tooltipCanvas != null ? tooltipCanvas.GetComponent<CanvasScaler>() : null;
        if (reference != null)
        {
            scaler.uiScaleMode = reference.uiScaleMode;
            scaler.referenceResolution = reference.referenceResolution;
            scaler.screenMatchMode = reference.screenMatchMode;
            scaler.matchWidthOrHeight = reference.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = reference.referencePixelsPerUnit;
            scaler.scaleFactor = reference.scaleFactor;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        host.AddComponent<GraphicRaycaster>();
        _group = host.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        float px = Mathf.Max(0.5f, _theme.pixelScale);

        // A dimmed room; clicking outside the book closes it.
        var backdrop = Panel(host.transform, "Backdrop", Vector2.zero, Vector2.one);
        var dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.01f, 0.03f, 0.72f);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        // The book: plate, shadow and frame in the tooltips' look.
        var book = Panel(host.transform, "Book", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
        var shadow = Art(book, "Shadow", _theme.shadow, false);
        shadow.rectTransform.offsetMin = new Vector2(-3f * px, -5f * px);
        shadow.rectTransform.offsetMax = new Vector2(3f * px, 1f * px);
        var plate = Art(book, "Plate", _theme.background, true);
        plate.rectTransform.offsetMin = new Vector2(3f * px, 3f * px);
        plate.rectTransform.offsetMax = new Vector2(-3f * px, -3f * px);
        plate.color = new Color(1f, 1f, 1f, Mathf.Max(0.96f, _theme.backgroundAlpha));
        if (_theme.background != null) plate.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / (_theme.background.pixelsPerUnit * Mathf.Max(0.5f, _theme.backgroundScale));
        plate.raycastTarget = true; // clicks inside the book do not reach the backdrop
        var frame = Art(book, "Frame", _theme.frame, true);
        frame.fillCenter = false;
        frame.color = _theme.frameSolid;

        const float pad = 36f, header = 64f, toolbar = 52f, indexWidth = 360f, gap = 28f;

        // Header: the Library's name, the two halves, and Close.
        var title = Label(book, "Title", Title, _theme.titleSize * 1.35f, _theme.titleColor, FontStyles.Normal);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(pad, -pad - header), new Vector2(0f, -pad));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        _glossaryTab = TextButton(book, GlossaryTab, () => SwitchHalf(false));
        Place(_glossaryTab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -pad - header), new Vector2(160f, -pad));
        _gameTab = TextButton(book, GameTab, () => SwitchHalf(true));
        Place(_gameTab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(170f, -pad - header), new Vector2(340f, -pad));
        var close = TextButton(book, "Close", Hide);
        close.alignment = TextAlignmentOptions.MidlineRight;
        Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-pad - 160f, -pad - header), new Vector2(-pad, -pad));

        var rule = Art(book, "Rule", _theme.divider, true);
        rule.raycastTarget = false;
        float ruleHeight = (_theme.divider != null ? _theme.divider.rect.height : 5f) * px;
        Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(pad, -pad - header - ruleHeight), new Vector2(-pad, -pad - header));

        // Toolbar: Back, Forward, and the search.
        float top = -pad - header - ruleHeight - 12f;
        _backButton = TextButton(book, "Back", () => Walk(-1));
        _backButton.richText = false;
        Place(_backButton.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, top - toolbar), new Vector2(pad + 90f, top));
        _forwardButton = TextButton(book, "Forward", () => Walk(1));
        _forwardButton.richText = false;
        Place(_forwardButton.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + 100f, top - toolbar), new Vector2(pad + 220f, top));
        _search = SearchField(book);
        Place((RectTransform)_search.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad + 240f, top - toolbar + 6f), new Vector2(pad + indexWidth + 240f, top - 6f));

        // The shelves on the left, the article on the right.
        float bodyTop = top - toolbar - 14f;
        _indexScroll = Scroll(book, "Shelves", out var indexContent);
        Place((RectTransform)_indexScroll.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(pad, pad), new Vector2(pad + indexWidth, bodyTop));
        _indexText = Label(indexContent, "Index", string.Empty, _theme.bodySize + 1f, _theme.bodyColor, FontStyles.Normal);
        _indexText.lineSpacing = 4f;
        _indexText.paragraphSpacing = 10f;

        _articleScroll = Scroll(book, "Article", out _articleContent);
        Place((RectTransform)_articleScroll.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(pad + indexWidth + gap, pad), new Vector2(-pad, bodyTop));
        var layout = _articleContent.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 28, 4, 24);
        layout.spacing = 14f;
        // The article is one trigger: the card shown is the hovered link's (BuildTooltip).
        _articleTrigger = _articleScroll.gameObject.AddComponent<TooltipTrigger>();

        _built = true;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    // Pixel art keeps its size, as in TooltipView: one art pixel is theme.pixelScale canvas units.
    private Image Art(Transform parent, string name, Sprite sprite, bool tiled)
    {
        var rect = Panel(parent, name, Vector2.zero, Vector2.one);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = tiled ? Image.Type.Tiled : sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        if (sprite != null) image.pixelsPerUnitMultiplier = 100f / (sprite.pixelsPerUnit * Mathf.Max(0.5f, _theme.pixelScale));
        return image;
    }

    private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        if (_theme.font != null) label.font = _theme.font;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private TextMeshProUGUI TextButton(Transform parent, string text, Action onClick)
    {
        var label = Label(parent, text, text, _theme.subtitleSize + 6f, _theme.subtitleColor, FontStyles.SmallCaps);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = true;
        var button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f);
        colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        return label;
    }

    private TextMeshProUGUI ArticleText()
    {
        var label = Label(_articleContent, "Text", string.Empty, _theme.bodySize + 2f, _theme.bodyColor, FontStyles.Normal);
        // Shared with TooltipText.Row, which cancels them inside a label/value row.
        label.lineSpacing = TooltipText.LineSpacing;
        label.paragraphSpacing = TooltipText.RowSpacing * 0.6f;
        return label;
    }

    private ScrollRect Scroll(Transform parent, string name, out RectTransform content)
    {
        var root = Panel(parent, name, Vector2.zero, Vector2.one);
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0.18f); // a faint well, and what the wheel scrolls
        var scroll = root.gameObject.AddComponent<ScrollRect>();

        var viewport = Panel(root, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 16);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // A thin bar on the right.
        var bar = Panel(root, "Scrollbar", new Vector2(1f, 0f), new Vector2(1f, 1f));
        bar.offsetMin = new Vector2(-8f, 4f);
        bar.offsetMax = new Vector2(-2f, -4f);
        var track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.3f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var area = Panel(bar, "Sliding Area", Vector2.zero, Vector2.one);
        var handle = Panel(area, "Handle", Vector2.zero, Vector2.one);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color32(0xE6, 0xBC, 0x93, 0xB0);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private TMP_InputField SearchField(Transform parent)
    {
        var root = Panel(parent, "Search", Vector2.zero, Vector2.one);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.4f);
        var field = root.gameObject.AddComponent<TMP_InputField>();

        var area = Panel(root, "Text Area", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(12f, 4f);
        area.offsetMax = new Vector2(-12f, -4f);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = Label(area, "Placeholder", "Search the archive...", _theme.bodySize, _theme.subtitleColor, FontStyles.Italic);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(placeholder.rectTransform);
        var text = Label(area, "Text", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
        Stretch(text.rectTransform);

        field.targetGraphic = background;
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.fontAsset = _theme.font;
        field.pointSize = _theme.bodySize;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false;
        field.customCaretColor = true;
        field.caretColor = _theme.titleColor;
        field.selectionColor = new Color(0.9f, 0.74f, 0.58f, 0.35f);
        field.onValueChanged.AddListener(OnSearch);
        return field;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
