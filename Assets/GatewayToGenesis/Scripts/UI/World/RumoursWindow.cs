using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rumours (<see cref="WorldRumours"/>): what the people have heard and not yet found, each with its direction from the
/// capital, then what they followed to the truth. It never names what a rumour is about before it is found. Opened from
/// the world map's Rumours button; Esc or Close shuts it. Built in code (<see cref="CodeUI"/>), over the world view, like
/// the Chronicle.
/// </summary>
public class RumoursWindow : MonoBehaviour
{
    private static RumoursWindow _instance;
    private TooltipTheme _theme;
    private RectTransform _root;
    private TextMeshProUGUI _body;
    private ScrollRect _scroll;
    private float _refreshAt;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    /// <summary>The frame it last closed on (so the Esc that closed it closes nothing else).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>Open the Rumours, or close them when open.</summary>
    public static void Toggle()
    {
        if (IsOpen) { _instance.Close(); return; }
        if (_instance == null) _instance = new GameObject("Rumours Window").AddComponent<RumoursWindow>();
        _instance.Open();
    }

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(RumoursWindow));
        var canvas = CodeUI.Canvas(transform, "Rumours Canvas", 2, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(900f, 720f);
        CodeUI.Plate(book, _theme, scaler);
        var title = CodeUI.Label(book, "Title", "Rumours", _theme.titleSize + 6f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -84f), new Vector2(-180f, -26f));
        var close = CodeUI.TextButton(book, "Close", Close, _theme);
        close.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -80f), new Vector2(-36f, -30f));

        var area = CodeUI.Panel(book, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(36f, 36f);
        area.offsetMax = new Vector2(-30f, -96f);
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var fitter = content.gameObject.AddComponent<VerticalLayoutGroup>();
        fitter.padding = new RectOffset(12, 12, 10, 16);
        fitter.childControlHeight = fitter.childControlWidth = true;
        fitter.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _body = CodeUI.Label(content, "Body", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        _body.alignment = TextAlignmentOptions.TopLeft;
        _scroll.viewport = viewport;
        _scroll.content = content;
    }

    private void Open()
    {
        if (_root == null) Build();
        _root.gameObject.SetActive(true);
        Refresh();
        _scroll.verticalNormalizedPosition = 1f;
    }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

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

    private void Update()
    {
        if (!IsOpen || Time.unscaledTime < _refreshAt) return;
        bool telling = EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive;
        if (telling) { Close(); return; }
        Refresh();
    }

    private void Refresh()
    {
        _refreshAt = Time.unscaledTime + 1f;
        _body.text = KeywordMarkup.SafeGlyphs(Compose(RumourKeeper.Current));
    }

    /// <summary>The window's text: rumours still to follow, newest first, then those found true.</summary>
    public static string Compose(RumourState state)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Muted("What travellers, hunters and pilgrims speak of. A rumour gives a direction from the capital and a vague word of what is there, never the place: the map marks the area it points at. Go and see. Finding what a rumour spoke of earns Era Score, and names it."));
        var open = WorldRumours.Open(state);
        var found = state?.heard.Where(r => r != null && r.confirmed).ToList() ?? new System.Collections.Generic.List<Rumour>();
        text.AppendLine();
        text.AppendLine(TooltipText.Heading("Still to follow", open.Count > 0 ? open.Count.ToString() : null));
        if (open.Count == 0) text.AppendLine(TooltipText.Muted("Nothing new is spoken of. Travellers come with each Echo, and expeditions bring word home."));
        foreach (var r in Enumerable.Reverse(open)) text.AppendLine(TooltipText.Bullet(r.text));
        if (found.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading("Found true", found.Count.ToString()));
            foreach (var r in Enumerable.Reverse(found))
                text.AppendLine(TooltipText.Bullet($"{TooltipText.Good(r.truth ?? "found")} {TooltipText.Muted($"({r.direction})")}"));
        }
        return text.ToString().TrimEnd();
    }
}
