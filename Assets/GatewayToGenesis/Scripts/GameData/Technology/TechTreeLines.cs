using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws flat line work for <see cref="TechTreeConnectors"/> in one mesh: strokes with soft edges, arrowheads and small
/// diamonds, in the order they were added (later marks cover earlier ones). Clipped by the research tab's scroll mask
/// like any UI graphic and never a raycast target. Fill it with <see cref="Clear"/> and the Add methods, then
/// <see cref="Commit"/>.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class TechTreeLines : MaskableGraphic
{
    private enum Kind { Stroke, Arrow, Diamond }

    private struct Mark
    {
        public Kind kind;
        public Vector2 a, b;
        public float width, feather;
        public Color32 color;
    }

    private readonly List<Mark> _marks = new List<Mark>();

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Clear() => _marks.Clear();

    /// <summary>How many marks were drawn (tests and debugging).</summary>
    public int MarkCount => _marks.Count;

    /// <summary>A straight stroke from <paramref name="a"/> to <paramref name="b"/>, extended by half its width at both ends so turns join square.</summary>
    public void AddStroke(Vector2 a, Vector2 b, float width, Color color, float feather)
    {
        if ((b - a).sqrMagnitude < 0.0001f) return;
        _marks.Add(new Mark { kind = Kind.Stroke, a = a, b = b, width = width, feather = feather, color = color });
    }

    /// <summary>A stroke through every point in turn.</summary>
    public void AddPolyline(IReadOnlyList<Vector2> points, float width, Color color, float feather)
    {
        for (int i = 1; i < points.Count; i++) AddStroke(points[i - 1], points[i], width, color, feather);
    }

    /// <summary>A dashed stroke: <paramref name="dash"/> drawn, <paramref name="gap"/> skipped.</summary>
    public void AddDashes(Vector2 a, Vector2 b, float width, Color color, float dash, float gap)
    {
        float length = Vector2.Distance(a, b);
        if (length < 0.01f) return;
        Vector2 direction = (b - a) / length;
        for (float at = 0f; at < length; at += dash + gap)
            _marks.Add(new Mark { kind = Kind.Stroke, a = a + direction * at, b = a + direction * Mathf.Min(length, at + dash), width = width, feather = 0.75f, color = color });
    }

    /// <summary>An arrowhead whose tip is at <paramref name="tip"/>, pointing along <paramref name="direction"/>.</summary>
    public void AddArrow(Vector2 tip, Vector2 direction, float length, float width, Color color)
    {
        _marks.Add(new Mark { kind = Kind.Arrow, a = tip, b = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right, width = width, feather = length, color = color });
    }

    public void AddDiamond(Vector2 center, float size, Color color)
    {
        _marks.Add(new Mark { kind = Kind.Diamond, a = center, width = size, color = color });
    }

    /// <summary>Show what was added.</summary>
    public void Commit() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var mark in _marks)
        {
            switch (mark.kind)
            {
                case Kind.Stroke: Stroke(vh, mark); break;
                case Kind.Arrow: Arrow(vh, mark); break;
                case Kind.Diamond: Diamond(vh, mark); break;
            }
        }
    }

    // A core quad, and a feather quad on each side fading to clear, so edges stay soft at any zoom.
    private static void Stroke(VertexHelper vh, Mark m)
    {
        Vector2 along = (m.b - m.a).normalized;
        Vector2 across = new Vector2(-along.y, along.x);
        float half = m.width * 0.5f;
        Vector2 a = m.a - along * half, b = m.b + along * half;
        Color32 clear = m.color;
        clear.a = 0;
        Quad(vh, a - across * half, a + across * half, b + across * half, b - across * half, m.color, m.color, m.color, m.color);
        if (m.feather <= 0f) return;
        Vector2 outer = across * (half + m.feather);
        Quad(vh, a + across * half, a + outer, b + outer, b + across * half, m.color, clear, clear, m.color);
        Quad(vh, a - outer, a - across * half, b - across * half, b - outer, clear, m.color, m.color, clear);
    }

    private static void Arrow(VertexHelper vh, Mark m)
    {
        Vector2 direction = m.b, across = new Vector2(-direction.y, direction.x);
        Vector2 tip = m.a, back = tip - direction * m.feather;
        int start = vh.currentVertCount;
        vh.AddVert(tip, m.color, Vector4.zero);
        vh.AddVert(back + across * (m.width * 0.5f), m.color, Vector4.zero);
        vh.AddVert(back - across * (m.width * 0.5f), m.color, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private static void Diamond(VertexHelper vh, Mark m)
    {
        float r = m.width * 0.5f;
        Vector2 c = m.a;
        Quad(vh, c + new Vector2(0f, r), c + new Vector2(r, 0f), c + new Vector2(0f, -r), c + new Vector2(-r, 0f), m.color, m.color, m.color, m.color);
    }

    private static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 c0, Color32 c1, Color32 c2, Color32 c3)
    {
        int start = vh.currentVertCount;
        vh.AddVert(p0, c0, Vector4.zero);
        vh.AddVert(p1, c1, Vector4.zero);
        vh.AddVert(p2, c2, Vector4.zero);
        vh.AddVert(p3, c3, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
