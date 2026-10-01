using System;
using TMPro;
using UnityEngine;

/// <summary>Icon first, with the same meaning on hover, keyboard focus and optional captions.</summary>
public sealed class IconActionButton : MonoBehaviour
{
    public UnityEngine.UI.Button Button { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    private UnityEngine.UI.Image art, marker;
    private TextMeshProUGUI caption, badge;
    private string title;
    private bool active, attention;
    public static IconActionButton Create(Transform parent, string title, PixelIcon icon, Action click, string tip, TooltipTheme theme)
    {
        var rect = CodeUI.Panel(parent, title, Vector2.zero, Vector2.zero);
        rect.sizeDelta = new Vector2(56, 56);
        var action = rect.gameObject.AddComponent<IconActionButton>();
        action.title = title;
        var face = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        action.Button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        face.color = Color.white; action.Button.targetGraphic = face;
        var colors = action.Button.colors;
        colors.normalColor = new Color(.10f, .09f, .08f, .98f);
        colors.highlightedColor = new Color(.28f, .22f, .14f, 1);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.38f, .29f, .15f, 1);
        colors.disabledColor = new Color(.12f, .12f, .12f, .85f);
        colors.fadeDuration = GameSettings.ReduceMotion ? 0 : .08f;
        action.Button.colors = colors;
        action.Button.onClick.AddListener(() => { TooltipSystemLogic.Instance?.HideTooltip(); click?.Invoke(); });
        action.art = CodeUI.Solid(rect, "Pixel icon", theme.titleColor);
        action.art.sprite = PixelIcons.Get(icon);
        action.art.preserveAspect = true;
        action.marker = CodeUI.Solid(rect, "Active underline", theme.titleColor);
        CodeUI.Place(action.marker.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(6, 2), new Vector2(-6, 5));
        action.caption = CodeUI.Label(rect, "Action name", title, 16, theme.titleColor, FontStyles.Normal, theme);
        action.caption.alignment = TextAlignmentOptions.Center;
        action.caption.textWrappingMode = TextWrappingModes.NoWrap;
        action.caption.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(action.caption.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(4, 7), new Vector2(-4, 28));
        action.badge = CodeUI.Label(rect, "Status", "", 16, theme.titleColor, FontStyles.Bold, theme);
        action.badge.alignment = TextAlignmentOptions.TopRight;
        CodeUI.Place(action.badge.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(4, -25), new Vector2(-4, -3));
        TooltipTrigger.Ensure(rect.gameObject).SetCustom(title, tip);
        UiFocus.Ensure(action.Button);
        action.RefreshPresentation();
        return action;
    }
    public void SetIcon(PixelIcon icon) => art.sprite = PixelIcons.Get(icon);
    public void SetState(bool selected, bool needsAttention = false, string status = null)
    {
        active = selected; attention = needsAttention;
        marker.gameObject.SetActive(active || attention);
        badge.text = status ?? (attention ? "!" : string.Empty);
    }
    public void RefreshPresentation()
    {
        bool labels = GameSettings.ActionLabels;
        caption.gameObject.SetActive(labels);
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(.5f, .5f);
        art.rectTransform.sizeDelta = new Vector2(32, 32);
        art.rectTransform.anchoredPosition = new Vector2(0, labels ? 12 : 0);
        marker.gameObject.SetActive(active || attention);
    }
}
