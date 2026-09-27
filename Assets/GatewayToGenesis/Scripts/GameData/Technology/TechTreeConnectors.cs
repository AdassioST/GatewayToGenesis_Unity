using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The connective tissue of one technology tree: a line from every uncovered prerequisite into each uncovered
/// technology, routed through the gaps between columns so it never crosses a slot (<see cref="TechTreeRules.Route"/>),
/// ending in an arrowhead. Each line is toned by how far research has come (<see cref="TechTreeRules.BranchTone"/>,
/// <see cref="TechTreeRules.TrunkTone"/>): dim stone while pending, gold once its prerequisite is researched, bright and
/// softly glowing into a technology that can be researched now, violet along the research plan, ivory along the way to
/// the technology under the pointer (or, over a researched one, to where it leads). A technology whose prerequisite is
/// not uncovered yet gets a short dashed stub from nowhere, so nothing hidden is given away.
///
/// Lives under the tree's Slots (ignored by its grid, drawn behind the slots) and rebuilds only when the tree, the plan,
/// the hovered technology or the layout change. Created by <see cref="TechnologyTreeLogic"/>.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TechTreeConnectors : MonoBehaviour
{
    [Header("Size (in the tree's own units)")]
    [SerializeField] private float lineWidth = 5f;
    [SerializeField] private float shadowWidth = 5f;
    [SerializeField] private float feather = 1.5f;
    [SerializeField] private float glowWidth = 16f;
    [SerializeField] private float arrowLength = 20f;
    [SerializeField] private float arrowWidth = 22f;
    [SerializeField] private float stubLength = 64f;
    [Tooltip("Sideways shift of a long line's turns (one that turns in more than one gap), so it runs beside the ordinary turns sharing its gaps instead of on top of them.")]
    [SerializeField] private float detourLane = 12f;
    [Tooltip("Height of the lines on a slot, from its bottom (0) to its top (1): the middle of its panels, above the Enlightenment bar.")]
    [SerializeField, Range(0f, 1f)] private float lineHeight = 0.59f;

    [Header("Tones")]
    [SerializeField] private Color pending = new Color(0.62f, 0.56f, 0.48f, 0.6f);
    [SerializeField] private Color complete = new Color(0.74f, 0.6f, 0.32f, 0.9f);
    [SerializeField] private Color open = new Color(0.93f, 0.77f, 0.4f, 1f);
    [SerializeField] private Color ready = new Color(1f, 0.89f, 0.58f, 1f);
    [SerializeField] private Color planned = new Color(0.78f, 0.64f, 1f, 1f);
    [SerializeField] private Color focus = new Color(1f, 0.97f, 0.88f, 1f);
    [SerializeField] private Color shadow = new Color(0.05f, 0.03f, 0.02f, 0.55f);

    [Header("Glow")]
    [SerializeField] private float glowSpeed = 2.4f;
    [SerializeField, Range(0f, 1f)] private float glowMin = 0.25f, glowMax = 0.75f;

    private TechnologyTreeLogic _tree;
    private RectTransform _rect, _slotsRect;
    private GridLayoutGroup _grid;
    private TechTreeLines _lines, _glow;
    private CanvasGroup[] _groups;
    private bool _dirty = true;
    private Vector4 _layoutSignature;

    // The grid, measured from the laid-out slots: the pitch between cells and the slots' edges in cell 0.
    private Vector2 _pitch;
    private float _left0, _right0, _top0, _bottom0;
    private int _rows;
    private readonly Dictionary<GameTechnologySlot, Vector2Int> _cells = new Dictionary<GameTechnologySlot, Vector2Int>();
    private readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();

    private struct Stroke
    {
        public Vector2 a, b;
        public LineTone tone;
        public bool dashed;
    }

    private readonly List<Stroke> _strokes = new List<Stroke>();
    private readonly List<(Vector2 tip, LineTone tone)> _arrows = new List<(Vector2, LineTone)>();
    private readonly List<(Vector2 center, LineTone tone)> _diamonds = new List<(Vector2, LineTone)>();
    private readonly List<(Vector2 a, Vector2 b, LineTone tone)> _glowing = new List<(Vector2, Vector2, LineTone)>();

    /// <summary>The layer for <paramref name="tree"/>, made under its Slots (first child, so the slots draw over it).</summary>
    public static TechTreeConnectors Create(TechnologyTreeLogic tree, RectTransform slots)
    {
        var go = new GameObject("Connectors", typeof(RectTransform));
        go.transform.SetParent(slots, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();

        var connectors = go.AddComponent<TechTreeConnectors>();
        connectors._tree = tree;
        connectors._rect = rect;
        connectors._slotsRect = slots;
        connectors._grid = slots.GetComponent<GridLayoutGroup>();
        connectors._glow = Layer(rect, "Glow");
        connectors._lines = Layer(rect, "Lines");
        connectors._groups = slots.GetComponentsInParent<CanvasGroup>(true);
        return connectors;
    }

    private static TechTreeLines Layer(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var lines = go.AddComponent<TechTreeLines>();
        lines.raycastTarget = false;
        return lines;
    }

    /// <summary>Redraw on the next frame (the tree, the plan or the hovered technology changed).</summary>
    public void MarkDirty() => _dirty = true;

    private void LateUpdate()
    {
        if (_tree == null || _slotsRect == null) return;
        var signature = new Vector4(_slotsRect.rect.width, _slotsRect.rect.height, _slotsRect.childCount, _tree.TreeSlots.Count);
        if (signature != _layoutSignature) _dirty = true;
        if (_dirty)
        {
            _dirty = false;
            // Place the cells now, so the lines are right on the frame the tree changes, not one frame later.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotsRect);
            _layoutSignature = new Vector4(_slotsRect.rect.width, _slotsRect.rect.height, _slotsRect.childCount, _tree.TreeSlots.Count);
            Rebuild();
        }
        Pulse();
    }

    // Lines into technologies that can be researched now breathe; nothing animates while the research tab is hidden.
    private void Pulse()
    {
        if (_glow == null || _glowing.Count == 0) return;
        float shown = 1f;
        if (_groups != null) foreach (var group in _groups) if (group != null && group.enabled) shown = Mathf.Min(shown, group.alpha);
        if (shown <= 0.01f) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowSpeed);
        _glow.canvasRenderer.SetAlpha(Mathf.Lerp(glowMin, glowMax, t));
    }

    // ===== MEASURING THE GRID =====

    private bool Measure()
    {
        _cells.Clear();
        _occupied.Clear();
        if (_grid == null) return false;
        _pitch = _grid.cellSize + _grid.spacing;
        if (_pitch.x <= 1f || _pitch.y <= 1f) return false;

        // Every laid-out child is a cell (technologies and empty cells alike): its column from x, its row from y.
        var centers = new List<(Transform child, Vector2 center)>();
        foreach (Transform child in _slotsRect)
        {
            if (!child.gameObject.activeSelf || child == transform) continue;
            if (child.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
            var rect = (RectTransform)child;
            Vector2 size = rect.rect.size;
            Vector2 min = (Vector2)rect.localPosition - Vector2.Scale(size, rect.pivot);
            centers.Add((child, ToLocal(min + size * 0.5f)));
        }
        if (centers.Count == 0) return false;
        float minX = centers.Min(c => c.center.x), maxY = centers.Max(c => c.center.y);

        float left = 0f, right = 0f, top = 0f, bottom = 0f;
        int measured = 0;
        _rows = 1;
        foreach (var (child, center) in centers)
        {
            var cell = new Vector2Int(Mathf.RoundToInt((center.x - minX) / _pitch.x), Mathf.RoundToInt((maxY - center.y) / _pitch.y));
            _rows = Mathf.Max(_rows, cell.y + 1);
            if (!child.TryGetComponent(out GameTechnologySlot slot) || slot.gameUnit == null) continue;
            _cells[slot] = cell;
            _occupied.Add(cell);
            // The slot's visible card, in cell 0's terms (the grid is uniform, so one offset serves every cell).
            var display = slot.displayComponent != null ? (RectTransform)slot.displayComponent.transform : (RectTransform)child;
            var corners = new Vector3[4];
            display.GetWorldCorners(corners);
            Vector2 lo = _rect.InverseTransformPoint(corners[0]), hi = _rect.InverseTransformPoint(corners[2]);
            left += lo.x - cell.x * _pitch.x;
            right += hi.x - cell.x * _pitch.x;
            top += hi.y + cell.y * _pitch.y;
            bottom += lo.y + cell.y * _pitch.y;
            measured++;
        }
        if (measured == 0) return false;
        _left0 = left / measured;
        _right0 = right / measured;
        _top0 = top / measured;
        _bottom0 = bottom / measured;
        return true;
    }

    private Vector2 ToLocal(Vector2 slotsLocal) => _rect.InverseTransformPoint(_slotsRect.TransformPoint(slotsLocal));

    private float Left(int column) => _left0 + column * _pitch.x;
    private float Right(int column) => _right0 + column * _pitch.x;
    private float Top(int row) => _top0 - row * _pitch.y;
    private float Bottom(int row) => _bottom0 - row * _pitch.y;
    private float LineY(int row) => Mathf.Lerp(Bottom(row), Top(row), lineHeight);
    private float GapX(int afterColumn) => (Right(afterColumn) + Left(afterColumn + 1)) * 0.5f;
    private float ChannelY(int belowRow) => (Bottom(belowRow) + Top(belowRow + 1)) * 0.5f;

    // Where a route point sits. Lines start just inside a slot's right edge (hidden under it) and end at the arrow's base;
    // turns in a gap shift by the route's lane.
    private Vector2 At(GridPoint p, float lane = 0f)
    {
        float x = p.x == GridX.SlotRight ? Right(p.column) - lineWidth
            : p.x == GridX.SlotLeft ? Left(p.column) - arrowLength * 0.6f
            : GapX(p.column) + lane;
        float y = p.y == GridY.Row ? LineY(p.row) : ChannelY(p.row);
        return new Vector2(x, y);
    }

    // ===== BUILDING THE LINES =====

    private void Rebuild()
    {
        // The tabs add their fading CanvasGroups at start-up, possibly after this layer was made.
        _groups = _slotsRect.GetComponentsInParent<CanvasGroup>(true);
        _strokes.Clear();
        _arrows.Clear();
        _diamonds.Clear();
        _glowing.Clear();
        if (Measure()) Collect();
        Draw();
    }

    private void Collect()
    {
        var slots = _tree.TreeSlots;
        var visibility = new Dictionary<string, TechVisibility>(System.StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, GameTechnologySlot>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var slot in slots)
        {
            if (slot == null || slot.gameUnit == null) continue;
            byName[slot.gameUnit.name] = slot;
            visibility[slot.gameUnit.name] = _tree.VisibilityOf(slot);
        }
        TechVisibility Seen(string name) => visibility.TryGetValue(name, out var v) ? v : TechnologyTreeLogic.GlobalVisibility(name);

        var plan = _tree.Plan;
        bool Planned(string name) => TechTreeRules.PlanPosition(plan, name) > 0;
        FocusSets(byName, out var focusTargets, out var focusEdges);

        foreach (var slot in slots)
        {
            if (slot == null || slot.gameUnit == null || !_cells.TryGetValue(slot, out var to)) continue;
            string name = slot.gameUnit.name;
            var state = visibility[name];
            if (!TechTreeRules.IsUncovered(state)) continue;

            bool anyLine = false;
            foreach (var before in TechTreeRules.Needs(name, TechnologyTreeLogic.Prerequisites))
            {
                var beforeState = Seen(before);
                bool inTree = byName.TryGetValue(before, out var beforeSlot) && _cells.ContainsKey(beforeSlot);
                if (inTree && TechTreeRules.ShowsLine(beforeState, state))
                {
                    var from = _cells[beforeSlot];
                    var route = TechTreeRules.Route(from.x, from.y, to.x, to.y, (c, r) => _occupied.Contains(new Vector2Int(c, r)), _rows);
                    bool focused = focusEdges.Contains((before, name));
                    var tone = TechTreeRules.BranchTone(beforeState, state, Planned(before) && Planned(name), focused);
                    // A long line turns in more than one gap: its turns run in their own lane, beside the ordinary ones.
                    float lane = route.Where(p => p.x == GridX.GapAfter).Select(p => p.column).Distinct().Count() > 1 ? detourLane : 0f;
                    for (int i = 1; i < route.Count - 1; i++) AddStroke(At(route[i - 1], lane), At(route[i], lane), tone);
                    anyLine = true;
                }
                else if (!inTree && beforeState == TechVisibility.Researched)
                {
                    // Researched in another Age's tree: nothing is missing, so no stub.
                }
                else
                {
                    // Not uncovered (or in another tree, not researched): a dashed stub from nowhere, never saying where.
                    var tone = focusTargets.Contains(name) ? LineTone.Focus : Planned(before) ? LineTone.Planned : LineTone.Pending;
                    Vector2 start = At(new GridPoint(GridX.GapAfter, to.x - 1, GridY.Row, to.y));
                    Vector2 tail = start - new Vector2(stubLength, 0f);
                    _strokes.Add(new Stroke { a = tail, b = start, tone = tone, dashed = true });
                    _diamonds.Add((tail, tone));
                    anyLine = true;
                }
            }
            if (!anyLine) continue;

            // The trunk: the last stretch every line into this technology shares, and its arrowhead.
            var trunkTone = TechTreeRules.TrunkTone(state, Planned(name), focusTargets.Contains(name));
            Vector2 trunkStart = At(new GridPoint(GridX.GapAfter, to.x - 1, GridY.Row, to.y));
            Vector2 trunkEnd = At(new GridPoint(GridX.SlotLeft, to.x, GridY.Row, to.y));
            AddStroke(trunkStart, trunkEnd, trunkTone);
            _arrows.Add((new Vector2(Left(to.x) - 1f, LineY(to.y)), trunkTone));
            if (state == TechVisibility.Available) _glowing.Add((trunkStart, trunkEnd, trunkTone));
        }
    }

    private void AddStroke(Vector2 a, Vector2 b, LineTone tone)
    {
        if ((b - a).sqrMagnitude > 0.01f) _strokes.Add(new Stroke { a = a, b = b, tone = tone });
    }

    // Over an unresearched technology: the way to it (what a click would plan). Over a researched one: where it leads.
    private void FocusSets(Dictionary<string, GameTechnologySlot> byName, out HashSet<string> targets, out HashSet<(string, string)> edges)
    {
        targets = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        edges = new HashSet<(string, string)>();
        var hovered = _tree.Hovered;
        if (hovered == null || hovered.gameUnit == null) return;
        string name = hovered.gameUnit.name;
        if (!hovered.isUnlocked)
        {
            foreach (var step in TechTreeRules.PlanTo(name, TechnologyTreeLogic.Prerequisites, TechnologyTreeLogic.IsResearched)) targets.Add(step);
            foreach (var target in targets)
                foreach (var before in TechTreeRules.Needs(target, TechnologyTreeLogic.Prerequisites))
                    if (targets.Contains(before) || TechnologyTreeLogic.IsResearched(before)) edges.Add((before, target));
            return;
        }
        foreach (var slot in byName.Values)
        {
            if (slot == hovered || !TechTreeRules.Needs(slot.gameUnit.name, TechnologyTreeLogic.Prerequisites).Contains(name, System.StringComparer.OrdinalIgnoreCase)) continue;
            edges.Add((name, slot.gameUnit.name));
            targets.Add(slot.gameUnit.name);
        }
    }

    private Color ColorOf(LineTone tone)
    {
        switch (tone)
        {
            case LineTone.Complete: return complete;
            case LineTone.Open: return open;
            case LineTone.Ready: return ready;
            case LineTone.Planned: return planned;
            case LineTone.Focus: return focus;
            default: return pending;
        }
    }

    // Shadows first, then the lines from dim to bright, so brighter tones win where lines share a stretch.
    private void Draw()
    {
        if (_lines == null) return;
        _lines.Clear();
        _glow.Clear();
        var ordered = _strokes.OrderBy(s => (int)s.tone).ToList();
        foreach (var s in ordered)
        {
            if (s.dashed) _lines.AddDashes(s.a, s.b, lineWidth + shadowWidth, shadow, 10f, 7f);
            else _lines.AddStroke(s.a, s.b, lineWidth + shadowWidth, shadow, feather);
        }
        foreach (var (tip, _) in _arrows) _lines.AddArrow(tip + new Vector2(1.5f, 0f), Vector2.right, arrowLength + 4f, arrowWidth + 6f, shadow);
        foreach (var (center, _) in _diamonds) _lines.AddDiamond(center, 16f, shadow);

        foreach (var s in ordered)
        {
            if (s.dashed) _lines.AddDashes(s.a, s.b, lineWidth * 0.8f, ColorOf(s.tone), 10f, 7f);
            else _lines.AddStroke(s.a, s.b, lineWidth, ColorOf(s.tone), feather);
        }
        foreach (var (tip, tone) in _arrows.OrderBy(a => (int)a.tone)) _lines.AddArrow(tip, Vector2.right, arrowLength, arrowWidth, ColorOf(tone));
        foreach (var (center, tone) in _diamonds.OrderBy(d => (int)d.tone)) _lines.AddDiamond(center, 11f, ColorOf(tone));
        _lines.Commit();

        foreach (var (a, b, tone) in _glowing)
        {
            var glow = ColorOf(tone);
            glow.a = 0.6f;
            _glow.AddStroke(a, b, glowWidth * 0.35f, glow, glowWidth * 0.4f);
        }
        _glow.Commit();
        if (_glowing.Count == 0) _glow.canvasRenderer.SetAlpha(0f);
    }
}
