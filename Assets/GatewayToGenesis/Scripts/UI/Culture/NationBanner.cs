using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The nation's banner in the world view's top left corner (above the map key): its name and its culture's word and
/// character. A click opens the <see cref="CultureWindow"/>. Built into the world view's canvas by
/// <see cref="WorldView"/> (<see cref="Attach"/>); it keeps itself current.
/// </summary>
public class NationBanner : MonoBehaviour
{
    /// <summary>The banner's height in canvas units (the map key sits under it).</summary>
    public const float Height = 64f;

    private TextMeshProUGUI _name, _line;
    private TooltipTrigger _tooltip;
    private CultureSystem _culture;
    private float _refreshAt;

    /// <summary>Build the banner in <paramref name="canvas"/>, its top left corner at <paramref name="topLeft"/>.</summary>
    public static NationBanner Attach(Transform canvas, TooltipTheme theme, CanvasScaler scaler, Vector2 topLeft, float width)
    {
        var rect = CodeUI.Panel(canvas, "Nation Banner", new Vector2(0f, 1f), new Vector2(0f, 1f));
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = new Vector2(width, Height);
        CodeUI.Plate(rect, theme, scaler, 0.92f);
        var banner = rect.gameObject.AddComponent<NationBanner>();
        banner._name = CodeUI.Label(rect, "Name", string.Empty, theme.titleSize + 2f, theme.titleColor, FontStyles.SmallCaps, theme);
        banner._name.textWrappingMode = TextWrappingModes.NoWrap;
        banner._name.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(banner._name.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -6f));
        banner._line = CodeUI.Label(rect, "Culture", string.Empty, theme.subtitleSize + 1f, theme.subtitleColor, FontStyles.Normal, theme);
        banner._line.textWrappingMode = TextWrappingModes.NoWrap;
        banner._line.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(banner._line.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(18f, 8f), new Vector2(-18f, 0f));
        // The plate catches the click and carries the tooltip.
        var plate = rect.Find("Plate");
        var target = plate != null ? plate.gameObject : rect.gameObject;
        var button = target.GetComponent<Button>() ?? target.AddComponent<Button>();
        button.onClick.AddListener(CultureWindow.Toggle);
        banner._tooltip = TooltipTrigger.Ensure(target);
        banner.Refresh();
        return banner;
    }

    private void OnDestroy()
    {
        if (_culture != null) _culture.Changed -= Refresh;
    }

    private void Update()
    {
        if (_culture == null && CultureSystem.Instance != null)
        {
            _culture = CultureSystem.Instance;
            _culture.Changed += Refresh;
            Refresh();
        }
        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 2f;
        Refresh();
    }

    private void Refresh()
    {
        if (_name == null) return;
        _name.text = KeywordMarkup.SafeGlyphs(CultureSystem.NationName);
        var culture = CultureSystem.Instance;
        _line.text = KeywordMarkup.SafeGlyphs(culture == null || !CultureSystem.IsFounded ? "Not yet founded: research Horology"
            : CultureSystem.IsNamed ? $"{CultureSystem.Adjective} culture, {culture.Character}   (rooted {CultureRules.Percent(culture.Cohesion)})"
            : "Your people wait for a name");
        if (_tooltip != null) _tooltip.SetCustom(CultureSystem.NationName, null, "Nation", CultureHud.Summary());
    }
}
