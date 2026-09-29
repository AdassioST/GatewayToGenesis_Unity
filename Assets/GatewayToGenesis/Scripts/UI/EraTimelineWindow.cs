using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Chronicle: every award of Era Score since the world began, in chronological order (<see cref="EraTimeline"/>),
/// grouped by Age, then by Cycle and Echo, each line dated to its Phase and Seventh and its Act of Fate. Opened by
/// clicking the Era Score sun (<see cref="EraScoreHud"/>) or the world map's Chronicle button; Esc or Close shuts it.
/// Built in code (<see cref="CodeUI"/>), over the world view and the Age banner.
/// </summary>
public class EraTimelineWindow : MonoBehaviour
{
    private static EraTimelineWindow _instance;
    private TooltipTheme _theme;
    private RectTransform _root;
    private TextMeshProUGUI _title, _body;
    private ScrollRect _scroll;
    private AgeProgression _ages;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    /// <summary>The frame it last closed on (so the Esc that closed it closes nothing else).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>Open the Chronicle, or close it when it is open.</summary>
    public static void Toggle()
    {
        if (IsOpen) { _instance.Close(); return; }
        if (_instance == null) _instance = new GameObject("Era Timeline Window").AddComponent<EraTimelineWindow>();
        _instance.Open();
    }

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(EraTimelineWindow));
        var canvas = CodeUI.Canvas(transform, "Era Timeline Canvas", 2, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        // A dim backdrop that closes the Chronicle when clicked.
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(980f, 820f);
        CodeUI.Plate(book, _theme, scaler);
        _title = CodeUI.Label(book, "Title", "Chronicle of Era Score", _theme.titleSize + 6f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -84f), new Vector2(-180f, -26f));
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
        var bar = CodeUI.Panel(area, "Scrollbar", new Vector2(1f, 0f), Vector2.one);
        bar.offsetMin = new Vector2(-7f, 0f);
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        var handle = CodeUI.Solid(bar, "Handle", new Color(0.72f, 0.62f, 0.4f, 0.85f), true);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.viewport = viewport;
        _scroll.content = content;
        _scroll.verticalScrollbar = scrollbar;
        _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void Open()
    {
        if (_root == null) Build();
        if (_ages == null)
        {
            _ages = AgeProgression.Instance;
            if (_ages != null) _ages.EraScoreAwarded += OnAwarded;
        }
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
        if (_ages != null) _ages.EraScoreAwarded -= OnAwarded;
        if (_instance == this) _instance = null;
    }

    private void OnAwarded(int points, string reason)
    {
        if (IsOpen) Refresh();
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;

    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Close();
    }

    private void Refresh()
    {
        var awards = _ages != null ? _ages.EraTimelineAwards : null;
        var text = new StringBuilder();
        if (_ages != null && _ages.Current != null)
            text.AppendLine(TooltipText.Row("This Age", $"{_ages.Current.title}: {_ages.EraScore} Era Score"));
        text.AppendLine(TooltipText.Muted("Every deed that earned Era Score, oldest first, dated to its Cycle, Echo, Phase and Seventh (21 Sevenths a Phase, 3 Phases an Echo, 4 Echoes a Cycle) and to its Act of Fate."));
        if (awards == null || awards.Count == 0)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Muted("Nothing has earned Era Score yet."));
        }
        foreach (var (heading, echoes) in EraTimeline.Sections(awards, ActLabel))
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading(heading));
            foreach (var (echo, lines) in echoes)
            {
                text.AppendLine(TooltipText.Value(echo));
                foreach (var line in lines) text.AppendLine(TooltipText.Bullet(line));
            }
        }
        _body.text = KeywordMarkup.SafeGlyphs(text.ToString().TrimEnd());
    }

    // "Act II" as the Age that held the award names it.
    private static string ActLabel(EraAward a)
    {
        var age = GameCatalog.Ages.All.FirstOrDefault(x => x != null && x.id == a.ageId);
        return age != null ? age.ActLabel(a.act) : $"Act {AgeRules.Roman(a.act + 1)}";
    }
}
