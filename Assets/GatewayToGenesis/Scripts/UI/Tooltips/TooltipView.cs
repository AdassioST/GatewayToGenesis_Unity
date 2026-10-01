using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tooltip box, built in code from a <see cref="TooltipTheme"/>: a soft shadow, a rust-mottled plate (tiled) and a
/// weathered stone frame with an inset copper line, the title with its type underneath, then one block per
/// <see cref="TooltipData"/> section. A stone-and-copper rule appears only where the tooltip changes purpose (under the
/// title, between story and numbers); rows and sections are otherwise set apart by spacing. Keywords are
/// highlighted in golden (<see cref="Keywords.Linkify"/>), long lists are shortened to a clickable "+N more", and
/// <see cref="TooltipData.details"/> waits behind "More...". A ring on the bottom edge shows how close the box is to
/// turning solid; <see cref="TooltipSystemLogic"/> decides when it does.
/// </summary>
public class TooltipView : MonoBehaviour
{
    private sealed class Block
    {
        public string id;
        public GameObject divider;
        public TextMeshProUGUI text;
    }

    private const string DetailsId = "details";

    private TooltipTheme _theme;
    private Canvas _canvas;
    private RectTransform _rect;
    private CanvasGroup _group;
    private LayoutElement _size;
    private Image _frame;
    private TextMeshProUGUI _title, _subtitle;
    private readonly List<Block> _blocks = new List<Block>();
    private RectTransform _ringRoot;
    private Image _ring;
    private float _artPixel;
    private string _hoverId;
    private TextMeshProUGUI _hoverText;

    public RectTransform Rect => _rect;
    /// <summary>There is something to do inside: keywords to hover or text to expand. Only such a box turns solid.</summary>
    public bool Interactive { get; private set; }

    public static TooltipView Create(Transform parent, TooltipTheme theme)
    {
        var go = new GameObject("Tooltip", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<TooltipView>();
        view.Build(theme);
        return view;
    }

    // ===== BUILD =====

    private void Build(TooltipTheme theme)
    {
        _theme = theme;
        _canvas = GetComponentInParent<Canvas>();
        _artPixel = Mathf.Max(0.5f, theme.pixelScale);
        _rect = (RectTransform)transform;
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0f, 1f);

        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(theme.padding.left, theme.padding.right, theme.padding.top, theme.padding.bottom);
        layout.spacing = theme.spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _size = gameObject.AddComponent<LayoutElement>();
        _size.minWidth = theme.minWidth;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // Behind the content: shadow, the tiled plate, then the frame (which catches the pointer once the box is solid).
        float px = _artPixel;
        var shadow = Layer("Shadow", theme.shadow, false, tiled: false);
        shadow.rectTransform.offsetMin = new Vector2(-3f * px, -5f * px);
        shadow.rectTransform.offsetMax = new Vector2(3f * px, 1f * px);
        var plate = Layer("Plate", theme.background, false, tiled: true);
        plate.rectTransform.offsetMin = new Vector2(3f * px, 3f * px);
        plate.rectTransform.offsetMax = new Vector2(-3f * px, -3f * px);
        // The Storage Tab's texture at the Storage Tab's own pixel size, slightly see-through.
        if (theme.background != null)
        {
            float reference = _canvas != null ? _canvas.referencePixelsPerUnit : 100f;
            plate.pixelsPerUnitMultiplier = reference / (theme.background.pixelsPerUnit * Mathf.Max(0.5f, theme.backgroundScale));
        }
        plate.color = new Color(1f, 1f, 1f, Mathf.Clamp01(theme.backgroundAlpha));
        _frame = Layer("Frame", theme.frame, true, tiled: true);
        _frame.fillCenter = false;

        _title = Text("Title", theme.titleSize, theme.titleColor, FontStyles.Normal);
        _subtitle = Text("Type", theme.subtitleSize, theme.subtitleColor, FontStyles.SmallCaps);
        _subtitle.characterSpacing = 3f;

        foreach (var id in new[] { "flavor", "summary", "requirements", "modifiers", "breakdown", "effects", "prerequisites", "notes", DetailsId, "related" })
        {
            bool flavor = id == "flavor";
            _blocks.Add(new Block
            {
                id = id,
                divider = Divider(),
                text = Text(id, flavor ? theme.flavorSize : theme.bodySize, flavor ? theme.flavorColor : theme.bodyColor, FontStyles.Normal),
            });
        }

        BuildRing(theme);
    }

    private Image Layer(string name, Sprite sprite, bool raycast, bool tiled)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        Slice(image, sprite, tiled);
        image.raycastTarget = raycast;
        return image;
    }

    private void BuildRing(TooltipTheme theme)
    {
        float size = (theme.ring != null ? theme.ring.rect.width : 12f) * _artPixel;
        _ringRoot = new GameObject("Lock", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        _ringRoot.SetParent(transform, false);
        _ringRoot.GetComponent<LayoutElement>().ignoreLayout = true;
        // Centred on the bottom edge: half the ring sits on the frame, like a seal.
        _ringRoot.anchorMin = _ringRoot.anchorMax = new Vector2(0.5f, 0f);
        _ringRoot.pivot = new Vector2(0.5f, 0.5f);
        _ringRoot.sizeDelta = new Vector2(size, size);
        _ringRoot.anchoredPosition = new Vector2(0f, 2f * _artPixel);

        Image Piece(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_ringRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
        Piece("Socket", theme.ringSocket);
        var track = Piece("Track", theme.ring);
        track.color = theme.ringTrack;
        _ring = Piece("Fill", theme.ring);
        _ring.type = Image.Type.Filled;
        _ring.fillMethod = Image.FillMethod.Radial360;
        _ring.fillOrigin = (int)Image.Origin360.Top;
        _ring.fillClockwise = true;
        _ring.color = theme.ringFill;
        _ring.fillAmount = 0f;
    }

    private TextMeshProUGUI Text(string name, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (_theme.font != null) text.font = _theme.font;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        // Shared with TooltipText.Row, which cancels them inside a label/value row.
        text.lineSpacing = TooltipText.LineSpacing;
        text.paragraphSpacing = TooltipText.RowSpacing;
        text.raycastTarget = false;
        return text;
    }

    private GameObject Divider()
    {
        var go = new GameObject("Rule", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var image = go.AddComponent<Image>();
        Slice(image, _theme.divider, tiled: true);
        image.raycastTarget = false;
        var element = go.AddComponent<LayoutElement>();
        float height = (_theme.divider != null ? _theme.divider.rect.height : 5f) * _artPixel;
        element.minHeight = element.preferredHeight = height;
        return go;
    }

    // Pixel art keeps its size: one art pixel is theme.pixelScale canvas units, however large the image is.
    // Tiled images repeat their texture (or, with a border, their edges) instead of stretching it.
    private void Slice(Image image, Sprite sprite, bool tiled = false)
    {
        image.sprite = sprite;
        image.type = tiled ? Image.Type.Tiled : sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        if (sprite == null) return;
        float reference = _canvas != null ? _canvas.referencePixelsPerUnit : 100f;
        image.pixelsPerUnitMultiplier = reference / (sprite.pixelsPerUnit * _artPixel);
    }

    // ===== CONTENT =====

    /// <summary>
    /// Fill the box with <paramref name="data"/>: keywords highlighted, long lists shortened unless their id is in
    /// <paramref name="expanded"/>, details behind "More..." unless expanded.
    /// </summary>
    public void Show(TooltipData data, ICollection<string> expanded)
    {
        bool banner = data.style == TooltipStyle.Banner;
        _title.alignment = banner ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        Set(_title, KeywordMarkup.SafeGlyphs(data.title));
        Set(_subtitle, banner ? null : KeywordMarkup.SafeGlyphs(data.type));

        string self = data.keywordId;
        bool header = _title.gameObject.activeSelf || _subtitle.gameObject.activeSelf;
        int lastGroup = header ? HeaderGroup : -1;
        bool links = false, expanders = false;
        foreach (var block in _blocks)
        {
            string text = Section(data, block.id);
            if (!string.IsNullOrEmpty(text))
            {
                text = Keywords.Linkify(text.Trim(), self);
                if (block.id == DetailsId)
                {
                    // Long reads (whole vault notes) are shown a page at a time.
                    bool open = expanded != null && expanded.Contains(DetailsId);
                    text = open ? TooltipText.Paged(text, DetailsId, expanded, _theme.detailsPageChars) : TooltipText.Expander(DetailsId, "More...");
                    expanders = true;
                }
                else
                {
                    text = TooltipText.Collapse(text, block.id, expanded, _theme.collapseAfter, out bool collapsed);
                    expanders |= collapsed;
                }
            }
            bool visible = !string.IsNullOrEmpty(text);
            Set(block.text, text);
            // Rules only where the tooltip changes purpose: under the title, and between the story (flavour,
            // summary) and the numbers. Sections of one kind are set apart by spacing and their own headings.
            int group = GroupOf(block.id);
            block.divider.SetActive(visible && lastGroup >= 0 && group != lastGroup && group != RelatedGroup);
            if (!visible) continue;
            lastGroup = group;
            if (text.IndexOf("<link=\"", StringComparison.Ordinal) >= 0 && ContainsKeyword(text)) links = true;
            // "Open in the White-Haven Library" is clicked like an expander.
            if (text.IndexOf(TooltipText.LibraryLinkPrefix, StringComparison.Ordinal) >= 0) expanders = true;
        }
        Interactive = links || expanders;
        _ringRoot.gameObject.SetActive(Interactive);
        FitWidth(banner);

        // Re-apply the hover highlight the rebuild just cleared.
        string hovered = _hoverId;
        _hoverId = null;
        _hoverText = null;
        SetHoveredLink(hovered);
    }

    private const int HeaderGroup = 0, StoryGroup = 1, NumbersGroup = 2, RelatedGroup = 3;

    private static int GroupOf(string id)
    {
        switch (id)
        {
            case "flavor":
            case "summary": return StoryGroup;
            case "related": return RelatedGroup;
            default: return NumbersGroup;
        }
    }

    private static string Section(TooltipData d, string id)
    {
        switch (id)
        {
            case "flavor": return d.description;
            case "summary": return d.summary;
            case "requirements": return d.requirements;
            case "modifiers": return d.modifiers;
            case "breakdown": return d.breakdown;
            case "effects": return d.effects;
            case "prerequisites": return d.prerequisites;
            case "notes": return d.notes;
            case DetailsId: return d.details;
            case "related": return d.related;
            default: return null;
        }
    }

    private static bool ContainsKeyword(string text)
    {
        int at = 0;
        while ((at = text.IndexOf("<link=\"", at, StringComparison.Ordinal)) >= 0)
        {
            at += 7;
            if (string.CompareOrdinal(text, at, TooltipText.UiLinkPrefix, 0, TooltipText.UiLinkPrefix.Length) != 0) return true;
        }
        return false;
    }

    private static void Set(TextMeshProUGUI label, string text)
    {
        bool visible = !string.IsNullOrEmpty(text);
        label.text = visible ? text : string.Empty;
        label.gameObject.SetActive(visible);
    }

    // As narrow as the content allows, wrapping at the theme's maximum width.
    private void FitWidth(bool banner)
    {
        float horizontal = _theme.padding.left + _theme.padding.right;
        float widest = 0f;
        foreach (var text in Texts())
        {
            if (!text.gameObject.activeSelf) continue;
            widest = Mathf.Max(widest, text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x);
        }
        // Slack for right-aligned values sharing a line with a label (TooltipText.Row).
        float wanted = widest + horizontal + 28f;
        _size.preferredWidth = banner ? _theme.bannerWidth : Mathf.Clamp(wanted, _theme.minWidth, _theme.maxWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
    }

    private IEnumerable<TextMeshProUGUI> Texts()
    {
        yield return _title;
        yield return _subtitle;
        foreach (var block in _blocks) yield return block.text;
    }

    // ===== POINTER =====

    private Camera EventCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

    public bool Contains(Vector2 screenPoint) => gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, EventCamera);

    /// <summary>The link under <paramref name="screenPoint"/> (a keyword id, or a "ui:" expander), or null.</summary>
    public string LinkAt(Vector2 screenPoint)
    {
        if (!Interactive) return null;
        var camera = EventCamera;
        foreach (var block in _blocks)
        {
            var text = block.text;
            if (!text.gameObject.activeSelf) continue;
            int index = TMP_TextUtilities.FindIntersectingLink(text, screenPoint, camera);
            if (index >= 0 && index < text.textInfo.linkCount) return text.textInfo.linkInfo[index].GetLinkID();
        }
        return null;
    }

    /// <summary>Brighten the keyword or expander with <paramref name="id"/> (null clears it).</summary>
    public void SetHoveredLink(string id)
    {
        if (id == _hoverId) return;
        if (_hoverText != null) _hoverText.ForceMeshUpdate();
        _hoverId = id;
        _hoverText = null;
        if (string.IsNullOrEmpty(id)) return;
        foreach (var block in _blocks)
        {
            var text = block.text;
            if (!text.gameObject.activeSelf) continue;
            var info = text.textInfo;
            bool touched = false;
            for (int l = 0; l < info.linkCount; l++)
            {
                if (info.linkInfo[l].GetLinkID() != id) continue;
                var link = info.linkInfo[l];
                for (int c = link.linkTextfirstCharacterIndex; c < link.linkTextfirstCharacterIndex + link.linkTextLength && c < info.characterCount; c++)
                {
                    var character = info.characterInfo[c];
                    if (!character.isVisible) continue;
                    var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                    for (int v = 0; v < 4; v++) colors[character.vertexIndex + v] = _theme.linkHover;
                    touched = true;
                }
            }
            if (!touched) continue;
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _hoverText = text;
        }
    }

    /// <summary>
    /// Put the box's corner next to <paramref name="screenPoint"/>, on whichever side keeps it on screen.
    /// </summary>
    public void PlaceAt(Vector2 screenPoint)
    {
        float scale = _rect.lossyScale.x;
        Vector2 size = _rect.rect.size * scale;
        Vector2 offset = _theme.pointerOffset * scale;
        var pivot = new Vector2(0f, 1f);
        if (screenPoint.x + offset.x + size.x > Screen.width) { pivot.x = 1f; offset.x = -offset.x; }
        if (screenPoint.y + offset.y - size.y < 0f) { pivot.y = 0f; offset.y = -offset.y; }
        _rect.pivot = pivot;

        Vector2 position = screenPoint + offset;
        // Clamp: a box larger than the gap on both sides still stays fully visible.
        position.x = Mathf.Clamp(position.x, size.x * pivot.x, Mathf.Max(size.x * pivot.x, Screen.width - size.x * (1f - pivot.x)));
        position.y = Mathf.Clamp(position.y, size.y * pivot.y, Mathf.Max(size.y * pivot.y, Screen.height - size.y * (1f - pivot.y)));
        _rect.position = position;
    }

    // ===== STATE =====

    /// <summary>How far the ring has filled while the pointer holds still (0-1).</summary>
    public void SetLockProgress(float progress)
    {
        _ring.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>Solid: stops following, catches the pointer, frame and ring brighten. Fluid: the opposite.</summary>
    public void SetSolid(bool solid)
    {
        _group.blocksRaycasts = solid;
        _frame.color = solid ? _theme.frameSolid : _theme.frameFluid;
        _ring.color = solid ? _theme.ringSolid : _theme.ringFill;
        _ring.fillAmount = solid ? 1f : 0f;
        DOTween.Kill(_ringRoot);
        _ringRoot.localScale = Vector3.one;
        if (solid && !GameSettings.ReduceMotion) _ringRoot.DOPunchScale(Vector3.one * 0.35f, 0.3f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        if (!solid) SetHoveredLink(null);
    }

    public void FadeIn()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        DOTween.Kill(_group);
        DOTween.Kill(transform);
        if (GameSettings.ReduceMotion) { _group.alpha = 1f; transform.localScale = Vector3.one; return; }
        _group.alpha = 0f;
        transform.localScale = Vector3.one * 0.97f;
        _group.DOFade(1f, _theme.fadeIn).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        transform.DOScale(1f, _theme.fadeIn * 1.5f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
    }

    public void FadeOut(Action done)
    {
        DOTween.Kill(_group);
        _group.blocksRaycasts = false;
        if (GameSettings.ReduceMotion) { HideNow(); done?.Invoke(); return; }
        _group.DOFade(0f, _theme.fadeOut).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                done?.Invoke();
            });
    }

    public void HideNow()
    {
        DOTween.Kill(_group);
        DOTween.Kill(transform);
        transform.localScale = Vector3.one;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
