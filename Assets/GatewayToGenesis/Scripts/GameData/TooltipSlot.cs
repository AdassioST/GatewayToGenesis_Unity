using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tooltip box: one text section per <see cref="TooltipData"/> field, hidden when empty. It follows the
/// pointer, flipping sides near the screen edges, and caps its width (via the layout element) only when a
/// line would be wider than the preferred width. Content and size change only in <see cref="Show"/>.
/// </summary>
public class TooltipSlot : MonoBehaviour
{
    [SerializeField] private LayoutElement layoutElement;

    [SerializeField] private GameObject titleSection, descriptionSection, typeSection, productionModifiersSection, storageBreakdownSection, resourceRequirementsSection, effectsSection, techRequirementsSection;

    public TextMeshProUGUI title, description, productionModifiers, type, storageBreakdown, resourceRequirements, effects, techRequirements;

    public RectTransform rectTransform;

    private const float BannerWidth = 650f;
    private static readonly Vector2 PointerOffset = new Vector2(20f, -5f);

    private TextMeshProUGUI[] _texts;
    private float _defaultWidth;
    private TextAlignmentOptions _defaultTitleAlignment;
    private bool _initialized;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        _texts = new[] { title, description, productionModifiers, type, storageBreakdown, resourceRequirements, effects, techRequirements };
        _defaultWidth = layoutElement != null ? layoutElement.preferredWidth : 0f;
        _defaultTitleAlignment = title != null ? title.alignment : TextAlignmentOptions.Left;
    }

    private void LateUpdate() => FollowPointer();

    /// <summary>Show <paramref name="data"/>: fill every section, hide the empty ones, and resize.</summary>
    public void Show(TooltipData data)
    {
        Initialize();
        bool banner = data.style == TooltipStyle.Banner;
        if (title != null) title.alignment = banner ? TextAlignmentOptions.Center : _defaultTitleAlignment;
        if (layoutElement != null) layoutElement.preferredWidth = banner ? BannerWidth : _defaultWidth;

        Set(title, titleSection, data.title);
        Set(description, descriptionSection, data.description);
        Set(type, typeSection, banner ? null : data.type);
        Set(resourceRequirements, resourceRequirementsSection, data.requirements);
        Set(productionModifiers, productionModifiersSection, data.modifiers);
        Set(storageBreakdown, storageBreakdownSection, data.breakdown);
        Set(effects, effectsSection, data.effects);
        Set(techRequirements, techRequirementsSection, data.prerequisites);

        Resize();
        FollowPointer();
    }

    private static void Set(TextMeshProUGUI label, GameObject section, string text)
    {
        bool visible = !string.IsNullOrEmpty(text);
        if (label != null) label.text = visible ? text : string.Empty;
        if (section != null) section.SetActive(visible);
    }

    // Wrap at the preferred width only when some line is wider than it; short tooltips stay compact.
    private void Resize()
    {
        if (layoutElement == null) return;
        bool wrap = false;
        foreach (var text in _texts)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            if (text.GetPreferredValues(text.text).x >= layoutElement.preferredWidth)
            {
                wrap = true;
                break;
            }
        }
        layoutElement.enabled = wrap;
    }

    private void FollowPointer()
    {
        if (rectTransform == null) return;
        Vector2 pointer = InputUtils.MousePosition;
        Vector2 size = rectTransform.sizeDelta * rectTransform.lossyScale;

        Vector2 pivot = new Vector2(0f, 1f);
        Vector2 offset = PointerOffset;
        if (pointer.x + size.x > Screen.width)
        {
            pivot.x = 1f;
            offset.x = -10f;
        }
        if (pointer.y - size.y < 0f)
        {
            pivot.y = 0f;
            offset.y = 5f;
        }
        if (pointer.x - size.x < 0f)
        {
            pivot.x = 0f;
            offset.x = PointerOffset.x;
        }
        if (pointer.y + size.y > Screen.height)
        {
            pivot.y = 1f;
            offset.y = PointerOffset.y;
        }

        rectTransform.pivot = pivot;
        transform.position = pointer + offset;
    }
}
