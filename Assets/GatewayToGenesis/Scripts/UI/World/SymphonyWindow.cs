using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A unit's Symphony laid out as cards (<see cref="SymphonyCardView"/>, the Sonata website's Spell Builder cards): every
/// card it would play in battle, grouped by who brings it, with its mix by Purpose. "The face" and "The back" turn every
/// card, a Purpose filter shows one of the five, and touching a card turns it alone. Opened from a unit's card on the
/// map ("See its Symphony"); Esc or Close shuts it. Built in code (<see cref="CodeUI"/>).
/// </summary>
public class SymphonyWindow : MonoBehaviour
{
    private static SymphonyWindow _instance;
    private TooltipTheme _theme;
    private RectTransform _root, _grid, _book;
    private TextMeshProUGUI _title, _subtitle, _faceButton, _backButton;
    private readonly List<TextMeshProUGUI> _filters = new List<TextMeshProUGUI>();
    private List<CardEntry> _cards = new List<CardEntry>();
    private readonly HashSet<int> _turned = new HashSet<int>();
    private bool _back;
    private SpellPurpose? _only;
    private readonly Dictionary<int, RectTransform> _cells = new Dictionary<int, RectTransform>();
    private ScrollRect _scroll;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    /// <summary>The frame it last closed on (so the Esc that closed it closes nothing else).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>Shows <paramref name="cards"/> under a title.</summary>
    public static void Show(string title, IEnumerable<CardEntry> cards)
    {
        if (_instance == null) _instance = new GameObject("Symphony Window").AddComponent<SymphonyWindow>();
        if (_instance._root == null) _instance.Build();
        _instance._cards = (cards ?? Enumerable.Empty<CardEntry>()).Where(c => c.card != null).ToList();
        _instance._turned.Clear();
        _instance._back = false;
        _instance._only = null;
        _instance.ClearCards();
        _instance._title.text = KeywordMarkup.SafeGlyphs(title ?? "Symphony");
        _instance._root.gameObject.SetActive(true);
        _instance.Refresh();
        _instance._scroll.StopMovement();
        _instance._scroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>A side's deck as the window's entries (each card's voice decides the Root a caster's card takes).</summary>
    public static IEnumerable<CardEntry> Entries(BattleSide side)
    {
        if (side?.deck == null) yield break;
        foreach (var dc in side.deck.Where(d => d?.card != null))
        {
            var sec = dc.voice >= 0 && dc.voice < side.sections.Count ? side.sections[dc.voice] : null;
            var voiceRoot = dc.legend != null ? dc.legend.leitmotif : sec?.primary ?? SpellBinding.Unattuned;
            yield return new CardEntry { card = dc.card, major = dc.major, voiceRoot = voiceRoot, source = dc.source };
        }
    }

    /// <summary>A map unit's Symphony (yours, or a band's instincts).</summary>
    public static void ShowUnit(WorldSystem world, WorldUnit unit)
    {
        var side = world?.SideOf(unit);
        if (side == null) return;
        Show($"{unit.name}: its Symphony", Entries(side));
    }

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(SymphonyWindow));
        var canvas = CodeUI.Canvas(transform, "Symphony Canvas", 2, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.6f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(1700f, 960f);
        _book = book;
        CodeUI.Plate(book, _theme, scaler);

        _title = CodeUI.Label(book, "Title", string.Empty, _theme.titleSize + 6f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -80f), new Vector2(-200f, -26f));
        _subtitle = CodeUI.Label(book, "Subtitle", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        CodeUI.Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -112f), new Vector2(-40f, -80f));
        var close = CodeUI.TextButton(book, "Close", Close, _theme);
        close.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -76f), new Vector2(-40f, -30f));

        // The face or the back, then the five Purposes.
        float x = 40f;
        _faceButton = Toggle(book, "Cards", () => { _back = false; _turned.Clear(); ClearCards(); Refresh(); }, ref x);
        _backButton = Toggle(book, "Details", () => { _back = true; _turned.Clear(); ClearCards(); Refresh(); }, ref x);
        x += 40f;
        _filters.Add(Toggle(book, "All", () => { _only = null; Refresh(); }, ref x));
        foreach (SpellPurpose p in System.Enum.GetValues(typeof(SpellPurpose)))
        {
            var purpose = p;
            _filters.Add(Toggle(book, p.ToString(), () => { _only = purpose; Refresh(); }, ref x));
        }

        var area = CodeUI.Panel(book, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(36f, 30f);
        area.offsetMax = new Vector2(-30f, -168f);
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
        var scroll = _scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _grid = CodeUI.Panel(viewport, "Grid", new Vector2(0f, 1f), new Vector2(1f, 1f));
        _grid.pivot = new Vector2(0.5f, 1f);
        var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(SymphonyCardView.Width, SymphonyCardView.Height);
        layout.spacing = new Vector2(18f, 22f);
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.childAlignment = TextAnchor.UpperCenter;
        _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = _grid;
    }

    private TextMeshProUGUI Toggle(RectTransform book, string text, System.Action onClick, ref float x)
    {
        var label = CodeUI.TextButton(book, text, onClick, _theme, _theme.subtitleSize + 3f);
        float width = 30f + text.Length * 13f;
        var r = label.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -120f);
        r.sizeDelta = new Vector2(width, 36f);
        x += width + 8f;
        return label;
    }

    private void Refresh()
    {
        var shown = _cards.Select((c, i) => (c, i)).Where(x => _only == null || x.c.card.purpose == _only.Value).ToList();
        foreach (var cell in _cells.Values) cell.gameObject.SetActive(false);
        foreach (var (entry, index) in shown)
        {
            int at = index;
            if (!_cells.TryGetValue(at, out var cell))
            {
                cell = CodeUI.Panel(_grid, entry.card.name, Vector2.zero, Vector2.one);
                _cells.Add(at, cell);
                DrawCard(at);
            }
            cell.gameObject.SetActive(true);
            cell.SetAsLastSibling();
        }
        _subtitle.text = KeywordMarkup.SafeGlyphs(shown.Count == 0 ? "No cards in this category. Choose All to see the deck." : $"{shown.Count} / {_cards.Count} cards   |   Click a card for details");
        _faceButton.color = !_back ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        _backButton.color = _back ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        for (int i = 0; i < _filters.Count; i++)
            _filters[i].color = (i == 0 && _only == null) || (i > 0 && _only == (SpellPurpose)(i - 1)) ? Color.white : new Color(1f, 1f, 1f, 0.5f);
    }

    private void DrawCard(int index)
    {
        var cell = _cells[index];
        for (int i = cell.childCount - 1; i >= 0; i--)
        {
            var old = cell.GetChild(i).gameObject;
            old.SetActive(false);
            if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
        SymphonyCardView.Build(cell, _cards[index], _theme, _back != _turned.Contains(index), () =>
        {
            if (!_turned.Add(index)) _turned.Remove(index);
            DrawCard(index);
        });
    }

    private void ClearCards()
    {
        foreach (var cell in _cells.Values)
        {
            cell.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(cell.gameObject); else DestroyImmediate(cell.gameObject);
        }
        _cells.Clear();
    }

    /// <summary>"26 cards: 10 Offensive, 10 Defensive, 5 Utility, 1 Setup · from Grave Warden, Wasteland Archer ...".</summary>
    public static string Subtitle(IReadOnlyCollection<CardEntry> cards)
    {
        if (cards == null || cards.Count == 0) return TooltipText.Muted("No cards.");
        var sources = cards.Select(c => c.source).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        return $"{cards.Count} cards: {CardFace.Mix(cards.Select(c => c.card))}" +
               (sources.Count > 0 ? TooltipText.Muted($"  ·  from {string.Join(", ", sources.Take(4))}{(sources.Count > 4 ? $" and {sources.Count - 4} more" : string.Empty)}") : string.Empty) +
               TooltipText.Muted("  ·  touch a card to turn it");
    }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void LateUpdate() { if (IsOpen) CodeUI.FitModal(_book); }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;

    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Close();
    }
}
