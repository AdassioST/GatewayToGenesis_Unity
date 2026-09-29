using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows tooltips and nested keyword tooltips.
///
/// Every tooltip starts <b>fluid</b>: it follows the pointer and lets clicks through. If it has something to
/// explore (golden keywords, or a list to expand) and the pointer holds still, the ring on its bottom edge
/// fills and it turns <b>solid</b>: it stays where it is, catches the pointer, and can be entered. In a solid
/// tooltip, hovering a keyword opens that keyword's tooltip, fluid again and following the pointer; it closes
/// as soon as the pointer leaves the keyword, unless the pointer held still long enough to make it solid too,
/// and so on down. Clicking "+N more" or "More..." in a solid tooltip expands it, and a card's "Open in the
/// White-Haven Library" closes the tooltips and opens the Library at that entry. The chain closes once the
/// pointer is away from all of it. Open tooltips are rebuilt a few times a second, so live numbers stay current.
/// Looks and timings come from <see cref="TooltipTheme"/>.
/// </summary>
public class TooltipSystemLogic : SingletonBehaviour<TooltipSystemLogic>
{
    [Tooltip("Look and timings. Empty: Resources/UI/TooltipTheme.")]
    [SerializeField] private TooltipTheme theme;
    [Tooltip("Seconds between content refreshes while a tooltip is open.")]
    [SerializeField] private float refreshInterval = 0.25f;

    // One open tooltip: the root belongs to a trigger, the ones above it to keywords.
    private sealed class Layer
    {
        public TooltipView view;
        public TooltipTrigger trigger;
        public string key;
        public bool solid;
        public Vector2 anchor;
        public float openedAt;
        public readonly HashSet<string> expanded = new HashSet<string>();
    }

    private readonly List<Layer> _layers = new List<Layer>();
    private readonly Stack<TooltipView> _pool = new Stack<TooltipView>();
    private readonly TooltipData _data = new TooltipData();
    private float _nextRefresh;
    private bool _triggerHovered;
    private Vector2 _lastPointer;
    private float _stillSince;
    private float _outsideSince = -1f;
    private string _pendingLink;
    private float _pendingSince;
    private float _childAwaySince = -1f;
    private int _lastLevel = -1;

    public bool isTooltipActive => _layers.Count > 0;

    /// <summary>The trigger whose tooltip is open, if any.</summary>
    public TooltipTrigger Current => _layers.Count > 0 ? _layers[0].trigger : null;

    /// <summary>The pointer is inside a solid tooltip (what lies under it should not react to the pointer).</summary>
    public bool PointerOverSolidTooltip => _layers.Count > 0 && DeepestSolidUnder(InputUtils.MousePosition) >= 0;

    protected override void OnSingletonAwake()
    {
        // A solid tooltip must catch the pointer, or what lies beneath it would take the hover away.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private TooltipTheme Theme
    {
        get
        {
            if (theme == null) theme = GameCatalog.UiThemes.Get("TooltipTheme", nameof(TooltipSystemLogic));
            return theme;
        }
    }

    // ===== TRIGGERS =====

    /// <summary>Show <paramref name="trigger"/>'s tooltip at the pointer, replacing any other.</summary>
    public void Show(TooltipTrigger trigger)
    {
        if (trigger == null) return;
        if (_layers.Count > 0 && _layers[0].trigger == trigger)
        {
            _triggerHovered = true;
            return;
        }
        if (!trigger.TryBuild(_data))
        {
            if (trigger == Current) HideTooltip();
            return;
        }
        CloseAbove(0, instant: true);
        Open(0, trigger, null, _data, InputUtils.MousePosition);
        _triggerHovered = true;
    }

    /// <summary>The pointer left <paramref name="trigger"/>: hand over to the next trigger, or close unless the tooltip is solid.</summary>
    public void Exit(TooltipTrigger trigger, GameObject nowHovered)
    {
        if (_layers.Count == 0 || trigger != _layers[0].trigger) return;
        _triggerHovered = false;
        if (nowHovered != null && nowHovered.GetComponentInParent<TooltipView>() != null) return;

        var next = nowHovered != null ? nowHovered.GetComponentInParent<TooltipTrigger>() : null;
        if (next != null && next != trigger)
        {
            Show(next);
            return;
        }
        // A fluid tooltip goes with its trigger; a solid one waits a moment for the pointer to reach it.
        if (!_layers[0].solid) HideTooltip();
    }

    /// <summary>A trigger is going away; take its tooltip down.</summary>
    public void Release(TooltipTrigger trigger)
    {
        if (trigger != null && trigger == Current) HideTooltip();
    }

    public void HideTooltip()
    {
        CloseAbove(-1, instant: false);
        _triggerHovered = false;
        _outsideSince = -1f;
        _pendingLink = null;
    }

    /// <summary>Game state changed: rebuild the open tooltips now.</summary>
    public void RefreshAllTooltips()
    {
        if (_layers.Count > 0) Rebuild();
    }

    /// <summary>Rebuild the open tooltip if it belongs to <paramref name="trigger"/>.</summary>
    public void RefreshIfShowing(TooltipTrigger trigger)
    {
        if (trigger != null && trigger == Current) Rebuild();
    }

    // ===== FRAME =====

    private void Update()
    {
        if (_layers.Count == 0) return;
        var root = _layers[0];
        if (root.trigger == null || !root.trigger.isActiveAndEnabled)
        {
            HideTooltip();
            return;
        }

        var theme = Theme;
        float now = Time.unscaledTime;
        Vector2 pointer = InputUtils.MousePosition;
        // Still = within a few pixels of where the pointer came to rest (hand jitter and slow drift both reset it).
        if ((pointer - _lastPointer).sqrMagnitude > theme.stillTolerance * theme.stillTolerance)
        {
            _lastPointer = pointer;
            _stillSince = now;
        }

        // Fluid tooltips follow the pointer.
        foreach (var layer in _layers) if (!layer.solid) layer.view.PlaceAt(pointer);

        // Only the newest tooltip can be locking; it turns solid once the pointer has held still long enough.
        var top = _layers[_layers.Count - 1];
        if (!top.solid && top.view.Interactive)
        {
            // Options, General: the player's hold time scales the theme's.
            float lockDelay = theme.lockDelay * GameSettings.TooltipHold;
            float progress = lockDelay <= 0f ? 1f : (now - Mathf.Max(_stillSince, top.openedAt)) / lockDelay;
            top.view.SetLockProgress(progress);
            if (progress >= 1f) MakeSolid(top, pointer);
        }

        int inside = DeepestSolidUnder(pointer);
        if (inside < 0 && !_triggerHovered)
        {
            // Away from the trigger and every solid tooltip: close after a short grace for crossing gaps.
            if (_outsideSince < 0f) _outsideSince = now;
            if (!root.solid || now - _outsideSince >= theme.closeGrace) HideTooltip();
            return;
        }
        _outsideSince = -1f;

        UpdateNested(inside >= 0 ? inside : 0, pointer, now, theme);

        if (now >= _nextRefresh) Rebuild();
    }

    private void MakeSolid(Layer layer, Vector2 pointer)
    {
        layer.solid = true;
        layer.anchor = pointer;
        layer.view.SetSolid(true);
    }

    // The deepest solid tooltip under the pointer; -1 when none is (fluid ones never are: they follow it).
    private int DeepestSolidUnder(Vector2 pointer)
    {
        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i].solid && _layers[i].view.Contains(pointer)) return i;
        }
        return -1;
    }

    // The pointer is on tooltip <paramref name="level"/> (or on the trigger, for level 0).
    private void UpdateNested(int level, Vector2 pointer, float now, TooltipTheme theme)
    {
        if (level != _lastLevel)
        {
            _lastLevel = level;
            _childAwaySince = -1f;
            _pendingLink = null;
        }

        var layer = _layers[level];
        string link = layer.solid ? layer.view.LinkAt(pointer) : null;
        for (int i = 0; i < _layers.Count; i++) if (_layers[i].solid) _layers[i].view.SetHoveredLink(i == level ? link : null);

        bool isUi = link != null && link.StartsWith(TooltipText.UiLinkPrefix, System.StringComparison.Ordinal);
        if (isUi && InputUtils.LeftClickDown && link.StartsWith(TooltipText.LibraryLinkPrefix, System.StringComparison.Ordinal))
        {
            // "Open in the White-Haven Library": the card has done its job.
            HideTooltip();
            Library.Open(link.Substring(TooltipText.LibraryLinkPrefix.Length));
            return;
        }
        if (isUi && InputUtils.LeftClickDown)
        {
            string id = link.Substring(TooltipText.UiLinkPrefix.Length);
            if (!layer.expanded.Remove(id)) layer.expanded.Add(id);
            RebuildLayer(level);
            return;
        }
        string keyword = isUi ? null : link;

        var child = _layers.Count > level + 1 ? _layers[level + 1] : null;
        if (keyword != null)
        {
            if (child != null && child.key == keyword)
            {
                _childAwaySince = -1f;
                CloseAbove(level + 1, instant: false);
                return;
            }
            if (_pendingLink != keyword)
            {
                _pendingLink = keyword;
                _pendingSince = now;
            }
            else if (now - _pendingSince >= theme.linkHoverDelay && Keywords.TryBuild(keyword, _data))
            {
                CloseAbove(level, instant: true);
                Open(level + 1, null, keyword, _data, pointer);
                _pendingLink = null;
            }
            return;
        }

        _pendingLink = null;
        if (child == null) return;
        // A fluid keyword tooltip lives only while its keyword is hovered; a solid one waits for the pointer.
        if (!child.solid)
        {
            CloseAbove(level, instant: false);
            return;
        }
        if (_childAwaySince < 0f) _childAwaySince = now;
        if (now - _childAwaySince >= theme.closeGrace)
        {
            CloseAbove(level, instant: false);
            _childAwaySince = -1f;
        }
    }

    // ===== LAYERS =====

    private void Open(int level, TooltipTrigger trigger, string key, TooltipData data, Vector2 pointer)
    {
        var theme = Theme;
        if (theme == null) return;
        bool reuse = level < _layers.Count;
        var layer = reuse ? _layers[level] : new Layer { view = Take(theme) };
        if (!reuse) _layers.Add(layer);

        layer.trigger = trigger;
        layer.key = key;
        layer.solid = false;
        layer.anchor = pointer;
        layer.openedAt = Time.unscaledTime;
        layer.expanded.Clear();
        _stillSince = Time.unscaledTime;
        _lastPointer = pointer;
        _childAwaySince = -1f;

        if (!layer.view.gameObject.activeSelf || !reuse) layer.view.FadeIn();
        layer.view.SetSolid(false);
        layer.view.Show(data, layer.expanded);
        layer.view.PlaceAt(pointer);
        _nextRefresh = Time.unscaledTime + refreshInterval;
    }

    // Close every layer above <paramref name="level"/> (-1 closes all).
    private void CloseAbove(int level, bool instant)
    {
        for (int i = _layers.Count - 1; i > level; i--)
        {
            var view = _layers[i].view;
            _layers.RemoveAt(i);
            if (instant)
            {
                view.HideNow();
                _pool.Push(view);
            }
            else view.FadeOut(() => _pool.Push(view));
        }
        if (_lastLevel > level) _lastLevel = -1;
    }

    private TooltipView Take(TooltipTheme theme)
    {
        while (_pool.Count > 0)
        {
            var pooled = _pool.Pop();
            if (pooled != null && !_layers.Exists(l => l.view == pooled)) return pooled;
        }
        return TooltipView.Create(transform, theme);
    }

    private void Rebuild()
    {
        _nextRefresh = Time.unscaledTime + refreshInterval;
        for (int i = 0; i < _layers.Count; i++)
        {
            if (!RebuildLayer(i)) return;
        }
    }

    // False when the layer could not be rebuilt (and was closed).
    private bool RebuildLayer(int i)
    {
        var layer = _layers[i];
        bool built = i == 0 ? layer.trigger != null && layer.trigger.TryBuild(_data) : Keywords.TryBuild(layer.key, _data);
        if (!built)
        {
            if (i == 0) HideTooltip();
            else CloseAbove(i - 1, instant: false);
            return false;
        }
        layer.view.Show(_data, layer.expanded);
        layer.view.PlaceAt(layer.solid ? layer.anchor : InputUtils.MousePosition);
        return true;
    }
}
