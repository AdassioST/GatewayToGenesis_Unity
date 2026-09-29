using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The faces of the story being told: its ballad actors (<see cref="EventSystemLogic.CurrentCast"/>), each with its
/// portrait, role and how it came to the story, beside the event screens like the cast list of a play. The player may
/// hand the lead to a co-protagonist, take a legend out, fill an open co-protagonist role, and, when no one plays the
/// story (no seat answers for its area and no Head of State is seated), choose who does: the council first.
/// Built in code by <see cref="GenesisLoop"/>; shown only while a story is told.
/// </summary>
public class BalladActorsView : MonoBehaviour
{
    private const float Width = 330f, Face = 56f;
    private const int MaxPicks = 8;

    private TooltipTheme _theme;
    private CanvasScaler _scaler;
    private RectTransform _root, _body;
    private ScrollRect _scroll;
    private bool _shown, _dirty, _subscribed;
    // Which list the picker shows: none, the lead (no one plays the story) or a co-protagonist role.
    private enum Picking { None, Lead, Co }
    private Picking _picking;
    private int _page;

    private void Start()
    {
        _theme = CodeUI.Theme(nameof(BalladActorsView));
        var canvas = CodeUI.Canvas(transform, "Ballad Actors", 6, out _scaler);
        _root = CodeUI.Panel(canvas.transform, "Cast", new Vector2(0f, 1f), new Vector2(0f, 1f));
        _root.pivot = new Vector2(0f, 1f);
        _root.anchoredPosition = new Vector2(24f, -246f);
        _root.sizeDelta = new Vector2(Width, 100f);
        CodeUI.Plate(_root, _theme, _scaler);
        var viewport = CodeUI.Panel(_root, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(18f, 16f);
        viewport.offsetMax = new Vector2(-18f, -16f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _scroll = viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 35f;
        _body = CodeUI.Panel(viewport, "Body", new Vector2(0f, 1f), Vector2.one);
        _body.pivot = new Vector2(0.5f, 1f);
        _scroll.viewport = viewport;
        _scroll.content = _body;
        var layout = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_subscribed && EventSystemLogic.Instance != null) EventSystemLogic.Instance.CastChanged -= MarkDirty;
    }

    private void MarkDirty() => _dirty = true;

    private void Update()
    {
        var events = EventSystemLogic.Instance;
        if (events == null || _root == null) return;
        if (!_subscribed)
        {
            events.CastChanged += MarkDirty;
            _subscribed = true;
        }
        bool show = events.isEventActive && events.CurrentCast != null;
        if (show != _shown)
        {
            _shown = show;
            _picking = Picking.None;
            _root.gameObject.SetActive(show);
            _dirty = show;
        }
        if (show && _dirty)
        {
            _dirty = false;
            Rebuild(events);
        }
    }

    private void Rebuild(EventSystemLogic events)
    {
        // Hidden at once so the layout below measures only the new rows (Destroy waits for the end of the frame).
        foreach (Transform child in _body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var cast = events.CurrentCast;
        var story = events.GetCurrentStoryNode();

        string heading = story != null && !string.IsNullOrEmpty(story.ballad)
            ? $"{(string.IsNullOrEmpty(story.balladTitle) ? "A Ballad" : story.balladTitle)}, verse {story.verse}"
            : "Legends in this event";
        Line(TooltipText.Heading(heading), _theme.subtitleSize + 4f, _theme.titleColor);

        if (cast.Empty)
        {
            Line("No one plays this story. Choose who does:", _theme.subtitleSize + 2f, _theme.bodyColor);
            _picking = Picking.Lead;
        }
        else
        {
            // Someone took the lead (here or elsewhere): the lead picker is done.
            if (_picking == Picking.Lead) _picking = Picking.None;
            Actor(events, cast, cast.protagonist);
            foreach (var co in cast.coProtagonists.Where(c => !string.IsNullOrEmpty(c)).ToList()) Actor(events, cast, co);
            if (cast.FreeCoSlots > 0 && _picking == Picking.None)
                Button($"+ Add a co-protagonist ({cast.FreeCoSlots} open)", () => { _picking = Picking.Co; _dirty = true; });
        }

        if (_picking != Picking.None) Picker(events);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_body);
        float height = ((RectTransform)_root.parent).rect.height;
        _root.sizeDelta = new Vector2(Width, Mathf.Min(LayoutUtility.GetPreferredHeight(_body) + 34f, Mathf.Max(180f, height - 276f)));
        _scroll.verticalNormalizedPosition = 1f;
    }

    // One actor: face, name, role and how it came to the story; the lead and remove buttons.
    private void Actor(EventSystemLogic events, BalladCast cast, string legend)
    {
        var row = CodeUI.Panel(_body, legend, Vector2.zero, Vector2.one);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        row.gameObject.AddComponent<LayoutElement>().minHeight = Face;

        GameCatalog.Legends.TryGet(legend, out var data);
        var face = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
        face.transform.SetParent(row, false);
        face.sprite = data != null ? data.portrait : null;
        face.color = face.sprite != null ? Color.white : new Color(0.3f, 0.25f, 0.22f);
        face.preserveAspect = true;
        var faceSize = face.gameObject.AddComponent<LayoutElement>();
        faceSize.preferredWidth = faceSize.preferredHeight = Face;
        faceSize.minWidth = Face;
        face.gameObject.AddComponent<LegendFace>().legend = data;
        TooltipTrigger.Ensure(face.gameObject);

        var text = new GameObject("Text", typeof(RectTransform));
        text.transform.SetParent(row, false);
        var v = text.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        text.AddComponent<LayoutElement>().flexibleWidth = 1f;
        string role = BalladActors.RoleOf(cast, legend) ?? string.Empty;
        string how = legend == cast.protagonist && !string.IsNullOrEmpty(cast.how) ? $", {(cast.how == "chosen" ? "your choice" : cast.how)}" : string.Empty;
        Label(text.transform, legend, _theme.bodySize, _theme.titleColor);
        Label(text.transform, TooltipText.Muted(role + how), _theme.subtitleSize, _theme.bodyColor);
        if (legend != cast.protagonist && LegendProgress.Instance != null)
        {
            var tie = LegendProgress.Instance.Relationships(legend).FirstOrDefault(b => string.Equals(b.other, cast.protagonist, StringComparison.OrdinalIgnoreCase));
            Label(text.transform, "Toward the protagonist: " + (tie == null ? "Open Rest" : tie.severed ? "Severed" : tie.StageName + $" · {tie.affection}/100" + (tie.Readiness > 0 ? " · Upgrade ready" : tie.Readiness < 0 ? " · Downgrade ready" : "")), _theme.subtitleSize, _theme.bodyColor);
        }
        var buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(text.transform, false);
        var hb = buttons.AddComponent<HorizontalLayoutGroup>();
        hb.spacing = 12f;
        hb.childControlWidth = hb.childControlHeight = true;
        hb.childForceExpandWidth = hb.childForceExpandHeight = false;
        if (legend != cast.protagonist) Small(buttons.transform, "Lead", () => events.ChooseProtagonist(legend));
        Small(buttons.transform, legend == cast.protagonist ? "Replace" : "Remove", () =>
        {
            if (legend == cast.protagonist && cast.coProtagonists.Count == 0) _picking = Picking.Lead;
            events.RemoveActor(legend);
            _dirty = true;
        });
    }

    // The legends free to play, the council first, as buttons.
    private void Picker(EventSystemLogic events)
    {
        var government = GovernmentLogic.Instance;
        var candidates = events.CastCandidates();
        if (candidates.Count == 0)
        {
            Line(TooltipText.Muted("No legend is free to play it (all are away on the road, or none has been met)."), _theme.subtitleSize, _theme.bodyColor);
            return;
        }
        _page = Math.Min(_page, (candidates.Count - 1) / MaxPicks);
        foreach (var name in candidates.Skip(_page * MaxPicks).Take(MaxPicks))
        {
            var seat = government != null ? government.GetSeatWithLegend(name) : null;
            string label = seat != null ? $"{name}  {TooltipText.Muted(seat.isHeadOfState ? "Head of State" : seat.GetEffectiveTitle())}" : name;
            Button(label, () =>
            {
                bool done = _picking == Picking.Co ? events.AddCoProtagonist(name) : events.ChooseProtagonist(name);
                if (done) _picking = Picking.None;
                _dirty = true;
            });
        }
        if (_page > 0) Button("Previous legends", () => { _page--; _dirty = true; });
        if ((_page + 1) * MaxPicks < candidates.Count) Button("More legends", () => { _page++; _dirty = true; });
        if (_picking == Picking.Co) Button(TooltipText.Muted("Cancel"), () => { _picking = Picking.None; _dirty = true; });
    }

    private void Line(string text, float size, Color color) => Label(_body, text, size, color);

    private TextMeshProUGUI Label(Transform parent, string text, float size, Color color) =>
        CodeUI.Label(parent, "Label", text, size, color, FontStyles.Normal, _theme);

    private void Button(string text, Action onClick) => CodeUI.TextButton(_body, text, onClick, _theme, _theme.subtitleSize + 4f);

    private void Small(Transform parent, string text, Action onClick) => CodeUI.TextButton(parent, text, onClick, _theme, _theme.subtitleSize + 1f);
}
