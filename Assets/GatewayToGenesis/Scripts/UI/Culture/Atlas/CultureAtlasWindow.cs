using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scrollable atlas with stable-ID selection. Rebuilds snapshots after changes/load; owns no domain state.</summary>
public sealed class CultureAtlasWindow : MonoBehaviour
{
    private static CultureAtlasWindow _instance;
    private RectTransform _root, _content, _viewport;
    private TooltipTheme _theme;
    private ScrollRect _scroll;
    private CultureSystem _culture;
    private CultureAtlasReadModel _model;
    private string _selection, _category, _message;
    private readonly Stack<string> _back = new Stack<string>();
    private readonly List<Button> _buttons = new List<Button>();
    private int _page, _compareA, _compareB = 1;
    private bool _compare, _dirty;
    private GameObject _returnFocus;
    public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    public static void Open(string key = null)
    {
        if (_instance == null) _instance = new GameObject("Cultural Atlas").AddComponent<CultureAtlasWindow>();
        _instance._returnFocus = EventSystem.current?.currentSelectedGameObject;
        CultureWindow.Hide();
        _instance.gameObject.SetActive(true);
        _instance.Bind();
        _instance._selection = key;
        _instance._back.Clear();
        _instance._category = null; _instance._page = 0; _instance._compare = false;
        _instance.Draw();
    }

    private void Bind()
    {
        if (_culture != CultureSystem.Instance)
        {
            if (_culture != null) _culture.Changed -= Changed;
            _culture = CultureSystem.Instance;
            if (_culture != null) _culture.Changed += Changed;
        }
        if (_root != null) return;
        _theme = CodeUI.Theme(nameof(CultureAtlasWindow));
        var canvas = CodeUI.Canvas(transform, "Atlas Canvas", 5, out var scaler);
        // Match height so a narrow window wraps the prose instead of shrinking it to tiny type.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        _root = CodeUI.Panel(canvas.transform, "Atlas", new Vector2(.04f, .04f), new Vector2(.96f, .96f));
        _root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .85f);
        CodeUI.Plate(_root, _theme, scaler);
        var title = CodeUI.Label(_root, "Title", "Cultural Atlas", _theme.titleSize, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -55), new Vector2(-130, -12));
        var close = CodeUI.TextButton(_root, "Close", Close, _theme);
        CodeUI.Place(close.rectTransform, new Vector2(1, 1), Vector2.one, new Vector2(-115, -55), new Vector2(-20, -12));
        _viewport = CodeUI.Panel(_root, "Viewport", Vector2.zero, Vector2.one);
        _viewport.offsetMin = new Vector2(22, 20); _viewport.offsetMax = new Vector2(-22, -65);
        _viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .12f);
        _viewport.gameObject.AddComponent<RectMask2D>();
        _content = CodeUI.Panel(_viewport, "Content", new Vector2(0, 1), Vector2.one);
        _content.pivot = new Vector2(.5f, 1);
        var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 18); layout.spacing = 7;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll = _root.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = _viewport; _scroll.content = _content; _scroll.horizontal = false;
        _scroll.scrollSensitivity = 45; _scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private void OnEnable() => GameInput.CancelPressed += Back;
    private void OnDisable() => GameInput.CancelPressed -= Back;
    private void OnDestroy() { if (_culture != null) _culture.Changed -= Changed; if (_instance == this) _instance = null; }
    private void Changed() => _dirty = true;
    private void LateUpdate()
    {
        if (_dirty) { _dirty = false; Draw(false); }
        // Keep keyboard focus visible inside the scroll viewport.
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(_content)) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, selected.transform);
        if (bounds.min.y < _viewport.rect.yMin) _content.anchoredPosition += Vector2.up * (_viewport.rect.yMin - bounds.min.y);
        else if (bounds.max.y > _viewport.rect.yMax) _content.anchoredPosition -= Vector2.up * (bounds.max.y - _viewport.rect.yMax);
    }

    private void Close()
    {
        gameObject.SetActive(false); ClosedFrame = Time.frameCount;
        if (_returnFocus != null && _returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(_returnFocus);
    }
    private void Back()
    {
        if (_back.Count > 0) { _selection = _back.Pop(); Draw(); }
        else if (_selection != null || _compare) { _selection = null; _compare = false; Draw(); }
        else Close();
    }
    private void Select(string key) { if (_selection != null) _back.Push(_selection); _selection = key; _message = null; Draw(); }
    private void Text(string value, bool heading = false)
    {
        var t = CodeUI.Label(_content, "Text", value ?? "", heading ? _theme.subtitleSize + 3 : _theme.bodySize, _theme.bodyColor, heading ? FontStyles.Bold : FontStyles.Normal, _theme);
        t.richText = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 32;
    }
    private void Button(string label, Action action, string why = null)
    {
        var t = CodeUI.TextButton(_content, label + (why == null ? "" : " — " + why), action, _theme, _theme.bodySize);
        t.richText = false; t.textWrappingMode = TextWrappingModes.Normal;
        var le = t.gameObject.AddComponent<LayoutElement>(); le.minHeight = 40;
        var b = t.GetComponent<Button>(); b.interactable = why == null;
        if (b.interactable) _buttons.Add(b);
    }
    private void Draw(bool resetScroll = true)
    {
        Bind();
        int focus = _buttons.FindIndex(b => b != null && b.gameObject == EventSystem.current?.currentSelectedGameObject);
        foreach (Transform child in _content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        _buttons.Clear();
        _model = CultureAtlasReadModel.Capture(_culture);
        Button("Back", Back);
        Button("Culture actions", () => { Close(); CultureWindow.Open(); });
        if (_message != null) Text(_message);
        if (_compare) DrawComparison();
        else if (_selection != null) DrawDetail();
        else DrawIndex();
        for (int i = 0; i < _buttons.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _buttons[(i + _buttons.Count - 1) % _buttons.Count], selectOnDown = _buttons[(i + 1) % _buttons.Count] };
            _buttons[i].navigation = nav;
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        if (resetScroll) _scroll.verticalNormalizedPosition = 1;
        if (_buttons.Count > 0) EventSystem.current?.SetSelectedGameObject(_buttons[Mathf.Clamp(resetScroll ? 0 : focus, 0, _buttons.Count - 1)].gameObject);
    }
    private void DrawIndex()
    {
        Text(_model.opportunity, true);
        if (_model.opportunityKey != null) Button("Inspect this opportunity", () => Select(_model.opportunityKey));
        Button("Compare two local cultures", () => { _compare = true; Draw(); }, _model.places.Count < 2 ? "Two recorded settlements are needed" : null);
        var categories = new[] { "All" }.Concat(_model.nodes.Values.Select(n => n.category).Distinct().OrderBy(x => x)).ToArray();
        Button("Browse: " + (_category ?? "All") + " (change)", () => { int i = Array.IndexOf(categories, _category ?? "All"); _category = categories[(i + 1) % categories.Length]; if (_category == "All") _category = null; _page = 0; Draw(); });
        var entries = _model.Browse(null, _category).ToList();
        int pages = Math.Max(1, (entries.Count + 19) / 20); _page = Math.Min(_page, pages - 1);
        Text($"Page {_page + 1} of {pages} · {entries.Count} records. Evidence, accounts and possibilities stay separate.");
        if (pages > 1) { Button("Previous page", () => { _page = (_page + pages - 1) % pages; Draw(); }); Button("Next page", () => { _page = (_page + 1) % pages; Draw(); }); }
        foreach (var n in entries.Skip(_page * 20).Take(20)) { string key = n.key; Button(n.category + " · " + n.title, () => Select(key)); }
        if (entries.Count == 0) Text("No culture history has been recorded yet.");
    }
    private void DrawComparison()
    {
        var places = _model.places;
        if (places.Count < 2) { Text("Fewer than two settlements remain in the records."); return; }
        _compareA %= places.Count; _compareB %= places.Count;
        Button("First: " + places[_compareA].name, () => { _compareA = (_compareA + 1) % places.Count; Draw(); });
        Button("Second: " + places[_compareB].name, () => { _compareB = (_compareB + 1) % places.Count; Draw(); });
        Text(CultureAtlasReadModel.Compare(places[_compareA], places[_compareB]));
    }
    private void DrawDetail()
    {
        var n = _model.Resolve(_selection);
        Text(n.title, true); Text(n.category + "\n" + n.detail);
        if (n.command != null) Button("Open " + n.command + " decisions", () => { Close(); CultureWindow.OpenSection(n.command, n.argument); });
        if (n.category == "Legend" || n.category == "Performance")
        {
            var journal = UnityEngine.Object.FindAnyObjectByType<BalladJournalView>();
            Button("Open Ballads & Bonds", () => { Close(); journal.OpenJournal(n.category == "Legend"); }, journal == null ? "Journal unavailable" : null);
        }
        if (n.category == "Tradition" && _culture != null)
        {
            string id = n.key.Substring("Tradition:".Length);
            Preview("Recognize nationally", () => _culture.PreviewRecognizeTradition(id), () => _culture.RecognizeTradition(id));
            Preview("Preserve locally", () => _culture.PreviewPreserveTradition(id), () => _culture.PreserveTradition(id));
            Preview("Decide later", () => _culture.PreviewDeferTradition(id), () => _culture.DeferTradition(id));
        }
        var entry = Library.Resolve(n.title);
        if (entry != null) Button("Read in the Library", () => { Close(); Library.Open(entry.id); });
        Text("Connected records", true);
        if (n.links.Count == 0) Text("No further connection was recorded.");
        foreach (string key in n.links.OrderBy(k => k)) { string target = key; var next = _model.Resolve(key); Button(next.category + " · " + next.title, () => Select(target)); }
    }
    private void Preview(string title, Func<CultureCommandResult> preview, Func<CultureCommandResult> execute)
    {
        var p = preview(); Text(title + ": " + p.reason);
        Button(title, () => { var latest = preview(); _message = latest.succeeded ? execute().reason : latest.reason; Draw(false); }, p.succeeded ? null : p.reason);
    }
}
