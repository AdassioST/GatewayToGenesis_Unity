using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Contextual actions in a compact, wrapping ribbon. Expansion stays until the player collapses it.</summary>
public sealed class QuickActionBar : MonoBehaviour
{
    private readonly List<(IconActionButton button, bool pinned, Func<bool> visible)> entries = new List<(IconActionButton, bool, Func<bool>)>();
    private IconActionButton toggle;
    private bool expanded;
    private RectTransform content, viewport;
    private UnityEngine.UI.ScrollRect scroll;
    public RectTransform Rect => (RectTransform)transform;
    public static QuickActionBar Create(Transform parent, TooltipTheme theme, UnityEngine.UI.CanvasScaler scaler)
    {
        var rect = CodeUI.Panel(parent, "Quick Actions", Vector2.zero, Vector2.zero);
        rect.pivot = Vector2.zero;
        var bar = rect.gameObject.AddComponent<QuickActionBar>();
        CodeUI.Plate(rect, theme, scaler);
        bar.viewport = CodeUI.Panel(rect, "Viewport", Vector2.zero, Vector2.one);
        bar.viewport.offsetMin = new Vector2(12, 12); bar.viewport.offsetMax = new Vector2(-12, -12);
        bar.viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        CodeUI.Solid(bar.viewport, "Scroll surface", new Color(0, 0, 0, .01f), true);
        bar.content = CodeUI.Panel(bar.viewport, "Actions", new Vector2(0, 1), new Vector2(0, 1));
        bar.content.pivot = new Vector2(0, 1);
        bar.scroll = rect.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        bar.scroll.viewport = bar.viewport; bar.scroll.content = bar.content;
        bar.scroll.horizontal = false; bar.scroll.scrollSensitivity = 48;
        bar.scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var track = CodeUI.Solid(rect, "Scroll track", new Color(0, 0, 0, .35f), true);
        CodeUI.Place(track.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-8, 12), new Vector2(-3, -12));
        var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        var handle = CodeUI.Solid(track.transform, "Scroll handle", theme.titleColor, true);
        scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        bar.scroll.verticalScrollbar = scrollbar; bar.scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        bar.toggle = IconActionButton.Create(bar.content, "More actions", PixelIcon.Expand, bar.Toggle,
            "Expand or collapse quick actions. Arrow keys move between buttons; Enter activates. Enable action labels in Options for names beside the icons.", theme);
        return bar;
    }
    public IconActionButton Add(string title, PixelIcon icon, Action click, string tip, TooltipTheme theme, bool pinned = false, Func<bool> visible = null)
    {
        var button = IconActionButton.Create(content, title, icon, click, tip, theme);
        entries.Add((button, pinned, visible));
        return button;
    }
    public void Toggle()
    {
        expanded = !expanded;
        toggle.SetIcon(expanded ? PixelIcon.Collapse : PixelIcon.Expand);
        toggle.SetState(expanded);
        TooltipTrigger.Ensure(toggle.gameObject).SetCustom(expanded ? "Fewer actions" : "More actions", expanded ? "Collapse the action bar." : "Expand quick actions.");
        Reflow();
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
    }
    private void OnEnable() => GameSettings.Changed += SettingsChanged;
    private void OnDisable() => GameSettings.Changed -= SettingsChanged;
    private void SettingsChanged(string section) { if (section == "general") Reflow(); }
    public void Reflow()
    {
        if (toggle == null || !(transform.parent is RectTransform parent)) return;
        var shown = new List<IconActionButton> { toggle };
        foreach (var entry in entries)
        {
            bool show = (entry.pinned || expanded) && (entry.visible == null || entry.visible());
            if (entry.button.gameObject.activeSelf != show) entry.button.gameObject.SetActive(show);
            if (show) shown.Add(entry.button);
        }
        var layout = new ActionBarLayout(shown.Count, parent.rect.width - 48, Mathf.Max(100, parent.rect.height - 180), GameSettings.ActionLabels, GameSettings.ActionSize);
        Rect.sizeDelta = new Vector2(layout.Width, layout.Height);
        content.sizeDelta = new Vector2(layout.Width - 24, layout.ContentHeight);
        scroll.inertia = !GameSettings.ReduceMotion;
        for (int i = 0; i < shown.Count; i++)
        {
            var rect = shown[i].Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(layout.CellWidth, layout.CellHeight);
            rect.anchoredPosition = new Vector2(i % layout.Columns * (layout.CellWidth + 8), -i / layout.Columns * (layout.CellHeight + 8));
            shown[i].RefreshPresentation();
        }
    }
}

/// <summary>Pure geometry, also verified without a native Editor.</summary>
public readonly struct ActionBarLayout
{
    public readonly int Columns, Rows;
    public readonly float Width, Height, ContentHeight, CellWidth, CellHeight;
    public ActionBarLayout(int count, float availableWidth, float availableHeight, bool labels, float scale)
    {
        count = Math.Max(1, count); scale = Math.Max(1, Math.Min(1.5f, scale));
        availableWidth = Math.Max(80, availableWidth);
        CellWidth = Math.Min((labels ? 126 : 56) * scale, availableWidth - 24);
        CellHeight = (labels ? 84 : 56) * scale;
        Columns = Math.Max(1, Math.Min(count, (int)Math.Floor((availableWidth - 24 + 8) / (CellWidth + 8))));
        Rows = (count + Columns - 1) / Columns;
        Width = Columns * (CellWidth + 8) - 8 + 24;
        ContentHeight = Rows * (CellHeight + 8) - 8;
        Height = Math.Min(ContentHeight + 24, Math.Max(CellHeight + 24, availableHeight));
    }
}
