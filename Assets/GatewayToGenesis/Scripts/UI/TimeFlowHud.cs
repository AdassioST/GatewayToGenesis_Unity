using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// How time flows, on the HUD: a small plate under the calendar's right side (the Cycle and Echo box; the Ballads & Bonds
/// launcher shares its row on the left) with an icon
/// (play, slowed, held, paused) and a word for it, the pause key beside it. Clicking it pauses or resumes, as Space does
/// (<see cref="TimeSystemLogic.PlayerPaused"/>). While the player has paused, a thin gold frame runs round the screen.
/// Hidden while a story is told, the Library is open or the menu holds the screen. Built in code; created by
/// <see cref="GenesisLoop"/>.
/// </summary>
public class TimeFlowHud : MonoBehaviour
{
    private const float Width = 232f, Height = 48f, Gap = 11f, Inset = 4f, Margin = 24f, Frame = 4f, PlaceSeconds = 0.25f;

    private static readonly Color PausedColor = new Color32(0xE6, 0xBC, 0x93, 0xFF);
    private static readonly Color PlayingColor = new Color32(0xB8, 0xE0, 0xB0, 0xFF);
    private static readonly Color SlowColor = new Color32(0xA8, 0xC8, 0xE8, 0xFF);
    private static readonly Color HeldColor = new Color32(0xB0, 0xA8, 0xA0, 0xFF);

    private enum Flow { Hidden, Playing, Slowed, Held, Paused }

    private TooltipTheme _theme;
    private Canvas _canvas;
    private CanvasGroup _group, _frameGroup;
    private RectTransform _plate;
    private float _placeAt;
    private readonly Vector3[] _corners = new Vector3[4];
    private TextMeshProUGUI _label, _hint;
    private Image _play, _barLeft, _barRight, _stop;
    private Flow _shown = (Flow)(-1);
    private string _key;
    private static Sprite _triangle;

    private void Start()
    {
        _theme = CodeUI.Theme(nameof(TimeFlowHud));
        if (_theme == null) { enabled = false; return; }
        Build();
    }

    private void Build()
    {
        var canvas = _canvas = CodeUI.Canvas(transform, "Time Flow Canvas", 3, out var scaler);
        _group = canvas.gameObject.AddComponent<CanvasGroup>();

        // While the player has paused: a thin frame round the whole screen, under everything else and never in the way.
        var frame = CodeUI.Panel(canvas.transform, "Paused Frame", Vector2.zero, Vector2.one);
        _frameGroup = frame.gameObject.AddComponent<CanvasGroup>();
        _frameGroup.blocksRaycasts = false;
        _frameGroup.alpha = 0f;
        var gold = new Color(PausedColor.r, PausedColor.g, PausedColor.b, 0.55f);
        CodeUI.Place(CodeUI.Solid(frame, "Top", gold).rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -Frame), Vector2.zero);
        CodeUI.Place(CodeUI.Solid(frame, "Bottom", gold).rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Frame));
        CodeUI.Place(CodeUI.Solid(frame, "Left", gold).rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(Frame, 0f));
        CodeUI.Place(CodeUI.Solid(frame, "Right", gold).rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-Frame, 0f), Vector2.zero);

        // The plate hangs under the calendar (Place).
        _plate = CodeUI.Panel(canvas.transform, "Time Flow", new Vector2(0f, 1f), new Vector2(0f, 1f));
        _plate.pivot = new Vector2(1f, 1f);
        _plate.sizeDelta = new Vector2(Width, Height);
        _plate.anchoredPosition = new Vector2(Margin + Width, -Margin);
        CodeUI.Plate(_plate, _theme, scaler, 0.92f);
        var button = _plate.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TimeSystemLogic.Instance?.TogglePlayerPause());
        TooltipTrigger.Ensure(_plate.gameObject);

        var icon = CodeUI.Panel(_plate, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        icon.sizeDelta = new Vector2(18f, 18f);
        icon.anchoredPosition = new Vector2(28f, 0f);
        _play = CodeUI.Solid(icon, "Play", PlayingColor);
        _play.sprite = Triangle();
        _barLeft = CodeUI.Solid(icon, "Bar", PausedColor);
        CodeUI.Place(_barLeft.rectTransform, Vector2.zero, new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero);
        _barRight = CodeUI.Solid(icon, "Bar", PausedColor);
        CodeUI.Place(_barRight.rectTransform, new Vector2(0.64f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        _stop = CodeUI.Solid(icon, "Stop", HeldColor);
        CodeUI.Place(_stop.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        _label = CodeUI.Label(_plate, "Flow", string.Empty, _theme.bodySize, _theme.titleColor, FontStyles.SmallCaps, _theme);
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(_label.rectTransform, Vector2.zero, Vector2.one, new Vector2(46f, 4f), new Vector2(-62f, -4f));
        _hint = CodeUI.Label(_plate, "Key", string.Empty, _theme.subtitleSize, _theme.subtitleColor, FontStyles.Normal, _theme);
        _hint.alignment = TextAlignmentOptions.MidlineRight;
        _hint.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(_hint.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-60f, 4f), new Vector2(-16f, -4f));
    }

    private void Update()
    {
        if (_group == null) return;
        var flow = Current(out var time);
        bool hidden = flow == Flow.Hidden;
        _group.alpha = Mathf.MoveTowards(_group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        _group.blocksRaycasts = !hidden;

        if (Time.unscaledTime >= _placeAt)
        {
            _placeAt = Time.unscaledTime + PlaceSeconds;
            Place();
            string key = KeyBindings.Keys("pause");
            if (key != _key) Show(flow, key, time);
        }
        if (flow != _shown) Show(flow, _key, time);

        // Paused: the bars breathe and the frame shows (still, if motion is reduced).
        float breathe = flow == Flow.Paused && !GameSettings.ReduceMotion ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;
        _barLeft.color = _barRight.color = new Color(PausedColor.r, PausedColor.g, PausedColor.b, breathe);
        _frameGroup.alpha = Mathf.MoveTowards(_frameGroup.alpha, flow == Flow.Paused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
    }

    // Under the calendar's box, right-aligned with it, while it shows (it follows the scene's HUD, whatever the screen);
    // before Horology, in the top-left corner where the calendar will be.
    private void Place()
    {
        var calendar = TimeSystemLogic.Instance != null ? TimeSystemLogic.Instance.CalendarRect : null;
        var root = calendar != null ? calendar.GetComponentInParent<Canvas>()?.rootCanvas : null;
        if (calendar == null || root == null)
        {
            _plate.anchorMin = _plate.anchorMax = new Vector2(0f, 1f);
            _plate.anchoredPosition = new Vector2(Margin + Width, -Margin);
            return;
        }
        calendar.GetWorldCorners(_corners);
        var screen = RectTransformUtility.WorldToScreenPoint(root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera, _corners[3]);
        var own = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, own, out var local)) return;
        _plate.anchorMin = _plate.anchorMax = new Vector2(0.5f, 0.5f);
        _plate.anchoredPosition = local + new Vector2(-Inset, -Gap);
    }

    private static Flow Current(out TimeSystemLogic time)
    {
        time = TimeSystemLogic.Instance;
        var events = EventSystemLogic.Instance;
        if (time == null || SaveMenu.BlocksGameplay || LibraryWindow.IsOpen ||
            (events != null && events.IsEventActive())) return Flow.Hidden;
        if (time.PlayerPaused) return Flow.Paused;
        // A tab or a card holds the calendar (before Horology there is no calendar to hold: time simply flows).
        if (time.canTrackTime && time.isTimePaused) return Flow.Held;
        if (time.IsSlowMotionActive) return Flow.Slowed;
        return Flow.Playing;
    }

    private void Show(Flow flow, string key, TimeSystemLogic time)
    {
        _shown = flow;
        _key = key;
        _play.gameObject.SetActive(flow == Flow.Playing || flow == Flow.Slowed);
        _play.color = flow == Flow.Slowed ? SlowColor : PlayingColor;
        _barLeft.gameObject.SetActive(flow == Flow.Paused);
        _barRight.gameObject.SetActive(flow == Flow.Paused);
        _stop.gameObject.SetActive(flow == Flow.Held);
        if (flow == Flow.Hidden || time == null) return;

        float slow = time.BaseSecondsPerSeventh > 0f ? time.GetEffectiveSecondsPerSeventh() / time.BaseSecondsPerSeventh : 1f;
        // The game font has no multiplication sign: the pace is written as a fraction.
        _label.text = flow == Flow.Paused ? "Paused" : flow == Flow.Held ? "Time waits" : flow == Flow.Slowed ? "Slowed " + TooltipText.Muted($"1/{slow:0.#}") : "Time flows";
        _label.color = flow == Flow.Paused ? PausedColor : _theme.titleColor;
        _hint.text = key;

        string title = flow == Flow.Paused ? "Paused" : flow == Flow.Held ? "Time waits" : flow == Flow.Slowed ? "Time slowed" : "Time flows";
        string body = flow == Flow.Paused
            ? "You have stopped time. The calendar, production, the stores, your people and every party on the map wait for you."
            : flow == Flow.Held
                ? "The calendar waits while this is open; it moves on when you close it."
                : flow == Flow.Slowed
                    ? $"A story is waiting for you: time passes {slow:0.#} times slower until you answer it or let it go."
                    : "Time passes at its own pace.";
        body += "\n\n" + TooltipText.Muted($"{key} or a click here: {(flow == Flow.Paused ? "resume" : "pause")}. The key can be changed in Options, Controls.");
        TooltipTrigger.Ensure(_plate.gameObject).SetCustom(title, body);
    }

    // A play triangle pointing right, drawn once.
    private static Sprite Triangle()
    {
        if (_triangle != null) return _triangle;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                bool inside = u >= 0.12f && u <= 0.92f && Mathf.Abs(v - 0.5f) <= 0.42f * (0.92f - u) / 0.8f;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        _triangle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _triangle;
    }
}
