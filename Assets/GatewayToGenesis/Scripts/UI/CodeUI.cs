using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Building blocks for views built in code (the Age banner, the world map) in the tooltips' look
/// (<see cref="TooltipTheme"/>): an overlay canvas scaled like the tooltips' own, plates with the stone frame,
/// labels in the game font and text buttons. The same pieces the White-Haven Library builds itself with.
/// </summary>
public static class CodeUI
{
    /// <summary>Keep a fixed-design modal inside its canvas, including narrower aspect ratios.</summary>
    public static void FitModal(RectTransform panel, float margin = 32f)
    {
        if (panel == null || !(panel.parent is RectTransform parent)) return;
        float scale = Mathf.Min(1f, (parent.rect.width - margin) / panel.sizeDelta.x,
            (parent.rect.height - margin) / panel.sizeDelta.y);
        panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
    }

    public static TooltipTheme Theme(string requester) => GameCatalog.UiThemes.Get("TooltipTheme", requester);

    /// <summary>An overlay canvas <paramref name="belowTooltips"/> orders under the tooltips' canvas, scaled like it.</summary>
    public static Canvas Canvas(Transform parent, string name, int belowTooltips, out CanvasScaler scaler)
    {
        var host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent, false);
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var tooltips = TooltipSystemLogic.Instance;
        var tooltipCanvas = tooltips != null ? tooltips.GetComponentInParent<Canvas>() : null;
        canvas.sortingOrder = tooltipCanvas != null ? Mathf.Max(0, tooltipCanvas.sortingOrder - belowTooltips) : 30000 - belowTooltips;
        scaler = host.AddComponent<CanvasScaler>();
        var reference = tooltipCanvas != null ? tooltipCanvas.GetComponent<CanvasScaler>() : null;
        if (reference != null)
        {
            scaler.uiScaleMode = reference.uiScaleMode;
            scaler.referenceResolution = reference.referenceResolution;
            scaler.screenMatchMode = reference.screenMatchMode;
            scaler.matchWidthOrHeight = reference.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = reference.referencePixelsPerUnit;
            scaler.scaleFactor = reference.scaleFactor;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static Image Solid(Transform parent, string name, Color color, bool raycast = false)
    {
        var image = Panel(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    /// <summary>Pixel art that keeps its size: one art pixel is theme.pixelScale canvas units.</summary>
    public static Image Art(Transform parent, string name, Sprite sprite, bool tiled, TooltipTheme theme)
    {
        var image = Panel(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = tiled ? Image.Type.Tiled : sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        if (sprite != null) image.pixelsPerUnitMultiplier = 100f / (sprite.pixelsPerUnit * Mathf.Max(0.5f, theme.pixelScale));
        return image;
    }

    /// <summary>Shadow, tiled plate and stone frame filling <paramref name="book"/>, as the tooltips and the Library have.</summary>
    public static void Plate(RectTransform book, TooltipTheme theme, CanvasScaler scaler, float opacity = 0.96f)
    {
        float px = Mathf.Max(0.5f, theme.pixelScale);
        var shadow = Art(book, "Shadow", theme.shadow, false, theme);
        shadow.rectTransform.offsetMin = new Vector2(-3f * px, -5f * px);
        shadow.rectTransform.offsetMax = new Vector2(3f * px, 1f * px);
        var plate = Art(book, "Plate", theme.background, true, theme);
        plate.rectTransform.offsetMin = new Vector2(3f * px, 3f * px);
        plate.rectTransform.offsetMax = new Vector2(-3f * px, -3f * px);
        plate.color = new Color(1f, 1f, 1f, Mathf.Max(opacity, theme.backgroundAlpha));
        if (theme.background != null) plate.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / (theme.background.pixelsPerUnit * Mathf.Max(0.5f, theme.backgroundScale));
        plate.raycastTarget = true; // clicks on the plate stay on it
        var frame = Art(book, "Frame", theme.frame, true, theme);
        frame.fillCenter = false;
        frame.color = theme.frameSolid;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, FontStyles style, TooltipTheme theme)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        if (theme.font != null) label.font = theme.font;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    public static TextMeshProUGUI TextButton(Transform parent, string text, Action onClick, TooltipTheme theme, float size = 0f)
    {
        var label = Label(parent, text, text, size > 0f ? size : theme.subtitleSize + 6f, theme.subtitleColor, FontStyles.SmallCaps, theme);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = true;
        var button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f);
        colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
        colors.selectedColor = new Color(1f, 0.92f, 0.64f);
        colors.disabledColor = new Color(0.55f, 0.52f, 0.5f, 0.8f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        UiFocus.Ensure(button);
        return label;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
