using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Announces each achievement the moment it unlocks: a card at the top of the screen, in the tooltips' look
/// (<see cref="TooltipTheme"/>), with the achievement's title and the vault's flavor line. Several unlocks queue.
/// Created by itself after the scene loads; no scene setup is needed. Does not block clicks.
/// </summary>
public class AchievementToast : MonoBehaviour
{
    private const float FadeSeconds = 0.35f, HoldSeconds = 5f, Width = 620f, Top = 24f;

    private static AchievementToast _instance;

    private readonly Queue<AchievementDefinition> _queue = new Queue<AchievementDefinition>();
    private TooltipTheme _theme;
    private CanvasGroup _group;
    private RectTransform _card;
    private TextMeshProUGUI _title, _flavor;
    private float _shownAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("Achievement Toast");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AchievementToast>();
    }

    private void OnEnable() => Achievements.Unlocked += Enqueue;

    private void OnDisable() => Achievements.Unlocked -= Enqueue;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Enqueue(AchievementDefinition achievement)
    {
        if (achievement != null) _queue.Enqueue(achievement);
    }

    private void Update()
    {
        if (_shownAt < 0f)
        {
            if (_queue.Count == 0 || !EnsureBuilt()) return;
            var next = _queue.Dequeue();
            _title.text = KeywordMarkup.SafeGlyphs(AchievementNote.Plain(next.title));
            _flavor.text = KeywordMarkup.SafeGlyphs(AchievementNote.Plain(next.flavor));
            // Below the Age banner, which holds the top of the screen.
            _card.anchoredPosition = new Vector2(0f, -Top - AgeBanner.ReservedHeight);
            _shownAt = Time.unscaledTime;
        }

        float age = Time.unscaledTime - _shownAt;
        float total = FadeSeconds * 2f + HoldSeconds;
        _group.alpha = age < FadeSeconds ? age / FadeSeconds : age > total - FadeSeconds ? Mathf.Clamp01((total - age) / FadeSeconds) : 1f;
        if (age >= total)
        {
            _group.alpha = 0f;
            _shownAt = -1f;
        }
    }

    private bool EnsureBuilt()
    {
        if (_group != null) return true;
        _theme = GameCatalog.UiThemes.Get("TooltipTheme", nameof(AchievementToast));
        if (_theme == null) return false;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // over the HUD, tabs and the Library
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var card = new GameObject("Card", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        card.transform.SetParent(transform, false);
        var rect = (RectTransform)card.transform;
        _card = rect;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -Top);
        rect.sizeDelta = new Vector2(Width, 0f);
        var layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(_theme.padding.left, _theme.padding.right, _theme.padding.top, _theme.padding.bottom);
        layout.spacing = _theme.spacing * 0.5f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        float px = Mathf.Max(0.5f, _theme.pixelScale);
        var plate = Art(card.transform, "Plate", _theme.background, tiled: true);
        plate.color = new Color(1f, 1f, 1f, Mathf.Max(0.94f, _theme.backgroundAlpha));
        if (_theme.background != null) plate.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / (_theme.background.pixelsPerUnit * Mathf.Max(0.5f, _theme.backgroundScale));
        var frame = Art(card.transform, "Frame", _theme.frame, tiled: false);
        frame.fillCenter = false;
        frame.color = _theme.frameSolid;
        plate.rectTransform.offsetMin = new Vector2(2f * px, 2f * px);
        plate.rectTransform.offsetMax = new Vector2(-2f * px, -2f * px);

        Label(card.transform, "Heading", "Achievement Unlocked", _theme.subtitleSize + 2f, _theme.subtitleColor, FontStyles.SmallCaps);
        _title = Label(card.transform, "Title", string.Empty, _theme.titleSize, _theme.titleColor, FontStyles.Normal);
        _flavor = Label(card.transform, "Flavor", string.Empty, _theme.flavorSize, _theme.flavorColor, FontStyles.Italic);
        return true;
    }

    // Background art stretches behind the laid-out labels without taking part in the layout.
    private Image Art(Transform parent, string name, Sprite sprite, bool tiled)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = tiled ? Image.Type.Tiled : sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        if (sprite != null) image.pixelsPerUnitMultiplier = 100f / (sprite.pixelsPerUnit * Mathf.Max(0.5f, _theme.pixelScale));
        return image;
    }

    private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        if (_theme.font != null) label.font = _theme.font;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
