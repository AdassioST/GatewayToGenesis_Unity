using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asks the people their name once the founding myth is told (<see cref="CultureSystem"/>), and again whenever the nation
/// renames itself: the nation (Iridia), what its citizens are called (Iridian) and what its culture is called (Iridian).
/// The last two follow the name as it is typed (<see cref="CultureRules.SuggestDemonym"/>) until the player changes them.
/// "Later" leaves the name for the Culture window (and a notice). Built in code (<see cref="CodeUI"/>), over the Culture window.
/// </summary>
public class CultureNamingDialog : MonoBehaviour
{
    private static CultureNamingDialog _instance;

    private TooltipTheme _theme;
    private RectTransform _root;
    private TextMeshProUGUI _intro, _error;
    private TMP_InputField _name, _demonym, _adjective;
    private bool _demonymEdited, _adjectiveEdited, _filling;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    public static void Open()
    {
        if (CultureSystem.Instance == null || !CultureSystem.IsFounded) return;
        if (_instance == null) _instance = new GameObject("Culture Naming Dialog").AddComponent<CultureNamingDialog>();
        _instance.Show();
    }

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(CultureNamingDialog));
        var canvas = CodeUI.Canvas(transform, "Culture Naming Canvas", 1, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.6f), true);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(820f, 620f);
        CodeUI.Plate(book, _theme, scaler);

        var title = CodeUI.Label(book, "Title", "What are we called?", _theme.titleSize + 8f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -86f), new Vector2(-40f, -28f));
        _intro = CodeUI.Label(book, "Intro", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        _intro.alignment = TextAlignmentOptions.TopLeft;
        CodeUI.Place(_intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -206f), new Vector2(-40f, -92f));

        _name = Field(book, "The nation", "Iridia", -228f);
        _demonym = Field(book, "Its citizens are called", "Iridian", -318f);
        _adjective = Field(book, "Its culture is called", "Iridian", -408f);
        _name.onValueChanged.AddListener(OnNameTyped);
        _demonym.onValueChanged.AddListener(_ => { if (!_filling) _demonymEdited = true; OnDemonymTyped(); });
        _adjective.onValueChanged.AddListener(_ => { if (!_filling) _adjectiveEdited = true; });
        foreach (var field in new[] { _name, _demonym, _adjective }) field.onSubmit.AddListener(_ => Confirm());

        _error = CodeUI.Label(book, "Error", string.Empty, _theme.bodySize - 1f, new Color(1f, 0.55f, 0.45f), FontStyles.Italic, _theme);
        CodeUI.Place(_error.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 86f), new Vector2(-40f, 122f));
        var confirm = CodeUI.TextButton(book, "Proclaim", Confirm, _theme, _theme.subtitleSize + 8f);
        confirm.color = _theme.titleColor;
        confirm.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(confirm.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260f, 30f), new Vector2(-40f, 76f));
        var later = CodeUI.TextButton(book, "Later", Later, _theme, _theme.subtitleSize + 4f);
        CodeUI.Place(later.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(240f, 76f));
    }

    private TMP_InputField Field(Transform book, string label, string placeholderText, float top)
    {
        var caption = CodeUI.Label(book, label, label, _theme.subtitleSize + 3f, _theme.subtitleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, top - 26f), new Vector2(-40f, top));
        var root = CodeUI.Panel(book, label + " Field", new Vector2(0f, 1f), new Vector2(1f, 1f));
        CodeUI.Place(root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, top - 74f), new Vector2(-40f, top - 30f));
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.4f);
        var field = root.gameObject.AddComponent<TMP_InputField>();
        var area = CodeUI.Panel(root, "Text Area", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(14f, 4f);
        area.offsetMax = new Vector2(-14f, -4f);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = CodeUI.Label(area, "Placeholder", placeholderText, _theme.bodySize + 3f, _theme.subtitleColor, FontStyles.Italic, _theme);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        CodeUI.Stretch(placeholder.rectTransform);
        var text = CodeUI.Label(area, "Text", string.Empty, _theme.bodySize + 3f, _theme.titleColor, FontStyles.Normal, _theme);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
        CodeUI.Stretch(text.rectTransform);
        field.targetGraphic = background;
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.fontAsset = _theme.font;
        field.pointSize = _theme.bodySize + 3f;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = CultureRules.MaxNameLength;
        field.richText = false;
        field.customCaretColor = true;
        field.caretColor = _theme.titleColor;
        field.selectionColor = new Color(0.9f, 0.74f, 0.58f, 0.35f);
        return field;
    }

    private void Show()
    {
        if (_root == null) Build();
        var culture = CultureSystem.Instance;
        var myth = culture.Myth;
        var identity = culture.Identity;
        _intro.text = KeywordMarkup.SafeGlyphs((myth != null ? TooltipText.Value(myth.answer) + " " : string.Empty)
            + (identity.IsNamed ? "A nation may take a new name; its history remembers the old one." : "Now the people ask what to call the nation they have become, what to call one another, and what to call their ways."));
        _filling = true;
        _name.text = identity.name ?? string.Empty;
        _demonym.text = identity.demonym ?? string.Empty;
        _adjective.text = identity.adjective ?? string.Empty;
        _filling = false;
        _demonymEdited = _adjectiveEdited = identity.IsNamed;
        _error.text = string.Empty;
        _root.gameObject.SetActive(true);
        _name.Select();
        _name.ActivateInputField();
    }

    private void OnNameTyped(string name)
    {
        if (_filling || _demonymEdited) return;
        _filling = true;
        _demonym.text = CultureRules.SuggestDemonym(name);
        _filling = false;
        OnDemonymTyped();
    }

    private void OnDemonymTyped()
    {
        if (_adjectiveEdited) return;
        bool was = _filling;
        _filling = true;
        _adjective.text = _demonym.text;
        _filling = was;
    }

    private void Confirm()
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !IsOpen) return;
        if (culture.Name(_name.text, _demonym.text, _adjective.text, out string error)) Close();
        else _error.text = KeywordMarkup.SafeGlyphs(error);
    }

    private void Later()
    {
        Close();
        if (CultureSystem.Instance != null && CultureSystem.Instance.NamingPending)
            NotificationFeed.Push("Your people wait for a name", "Open Your Nation to name the nation, its citizens and its culture.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:naming");
    }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;
    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Later();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
