using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A golden border that breathes in and out around a control (<see cref="Tutorials"/>): gold copies of the control's own
/// art, nudged a little in eight directions and drawn just behind it in its own canvas, so the border follows the shape
/// that is drawn (a round swirl glows round; a plain button glows square). It follows the control every frame, never
/// catches clicks and is ignored by layout groups. <see cref="Follow"/> to move it, null to put it out.
/// </summary>
public class TutorialGlow : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.8f, 0.3f, 1f);

    private const float PulseSeconds = 1.6f;
    // Two rings in art pixels: a bright tight border and a softer halo beyond it.
    private static readonly (float reach, float strength)[] Rings = { (1.2f, 1f), (2.6f, 0.45f) };
    private static readonly Vector2[] Directions =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f),
    };

    private Graphic _target;
    private float _strength = 1f;
    private RectTransform _root;
    private readonly List<(Image image, Vector2 offset, float strength)> _copies = new List<(Image, Vector2, float)>();

    /// <summary>Ring <paramref name="target"/> (null: no glow), at <paramref name="intensity"/> (a quiet hint glows softer). Rebuilt only when the control changes.</summary>
    public void Follow(Graphic target, float intensity = 1f)
    {
        _strength = Mathf.Clamp01(intensity);
        if (target == _target && (_target == null || _root != null)) return;
        Clear();
        _target = target;
        if (_target == null) return;
        var parent = _target.transform.parent;
        if (parent == null) return;
        _root = new GameObject("Tutorial Glow", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(parent, false);
        _root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var group = _root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        var source = _target as Image;
        float artPixel = ArtPixel(source);
        foreach (var (reach, strength) in Rings)
            foreach (var direction in Directions)
            {
                var go = new GameObject("Ring", typeof(RectTransform));
                go.transform.SetParent(_root, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                if (source != null)
                {
                    image.sprite = source.sprite;
                    image.type = source.type;
                    image.preserveAspect = source.preserveAspect;
                    image.fillCenter = source.fillCenter;
                    image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                }
                _copies.Add((image, direction * reach * artPixel, strength));
            }
        Update();
    }

    private void Clear()
    {
        if (_root != null) Destroy(_root.gameObject);
        _root = null;
        _copies.Clear();
    }

    private void OnDestroy() => Clear();

    // One pixel of the control's art in its own local units (a sprite drawn larger shows larger pixels); 4 without art.
    private static float ArtPixel(Image image)
    {
        if (image == null || image.sprite == null || image.sprite.rect.width <= 0f) return 4f;
        var rect = ((RectTransform)image.transform).rect;
        return Mathf.Max(1f, rect.width / image.sprite.rect.width);
    }

    private void Update()
    {
        if (_root == null) return;
        if (_target == null || !_target.isActiveAndEnabled)
        {
            _root.gameObject.SetActive(false);
            if (_target == null) Clear();
            return;
        }
        if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
        // Sit exactly where the control is, just behind it.
        var t = (RectTransform)_target.transform;
        _root.anchorMin = t.anchorMin;
        _root.anchorMax = t.anchorMax;
        _root.pivot = t.pivot;
        _root.anchoredPosition = t.anchoredPosition;
        _root.sizeDelta = t.sizeDelta;
        _root.localRotation = t.localRotation;
        _root.localScale = t.localScale;
        int index = t.GetSiblingIndex();
        if (_root.GetSiblingIndex() != index - 1) _root.SetSiblingIndex(Mathf.Max(0, _root.GetSiblingIndex() < index ? index - 1 : index));
        float breath = GameSettings.ReduceMotion ? 0.7f : 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / PulseSeconds);
        foreach (var (image, offset, strength) in _copies)
        {
            var r = image.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = offset;
            r.offsetMax = offset;
            image.color = new Color(Gold.r, Gold.g, Gold.b, _strength * strength * Mathf.Lerp(0.15f, 0.95f, breath));
        }
    }
}
