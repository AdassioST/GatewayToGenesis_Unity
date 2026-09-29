using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The nation's own window (<see cref="CultureSystem"/>): its name and what its people and ways are called, the founding
/// myth, its character and leanings (and where they drift), its foodways by class (Edibles, Eleos Teas, Ingredients, Spices; the
/// national ones and the one waiting), the land it has taken root in, its reforms and its own history. Opened from the
/// capital HUD's "Your Nation" slot (<see cref="CultureHud"/>) or the world view's nation banner (<see cref="NationBanner"/>);
/// Esc or Close shuts it. Built in code (<see cref="CodeUI"/>).
/// </summary>
public class CultureWindow : MonoBehaviour
{
    private static CultureWindow _instance;
    private const float BarHeight = 150f, ReformHeight = 150f, LifeHeight = 360f;

    // The lower panel of the culture's life: its rites, its kitchen or its holidays.
    private enum LifeMode { None, Rites, Kitchen, Holidays, Memorials, Traditions, Tables, Teaching, Performances, Accounts, Heritage }

    private TooltipTheme _theme;
    private RectTransform _root, _book, _scrollArea, _reformPanel, _lifePanel, _lifeGrid;
    private TextMeshProUGUI _title, _subtitle, _body, _nameButton, _reformButton, _reformText, _confirm, _ritesButton, _kitchenButton, _holidayButton, _memorialButton, _traditionButton, _tableButton, _teachingButton, _performanceButton, _accountsButton, _heritageButton, _lifeText;
    private GridLayoutGroup _lifeLayout;
    private LifeMode _mode;
    private ScrollRect _scroll, _lifeScroll;
    private CultureSystem _culture;
    private EnclaveFamily? _chosen;
    private readonly List<TextMeshProUGUI> _familyButtons = new List<TextMeshProUGUI>();

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    public static void Toggle()
    {
        if (IsOpen) { _instance.Close(); return; }
        Open();
    }

    public static void Open()
    {
        if (_instance == null) _instance = new GameObject("Culture Window").AddComponent<CultureWindow>();
        _instance.Show();
    }

    /// <summary>Open the window on its Tables panel, setting up a communal table in <paramref name="settlement"/> (T05).</summary>
    public static void OpenTables(int settlement)
    {
        Open();
        HospitalityPanel.Select(settlement);
        _instance.SetLife(LifeMode.Tables);
    }

    /// <summary>Open the window on its Performances panel, for one cultural party (T08).</summary>
    public static void OpenPerformances(int unit)
    {
        Open();
        PerformancePanel.Select(unit);
        _instance.SetLife(LifeMode.Performances);
    }

    /// <summary>Open the window on its Accounts panel, on one dispute when given (T09).</summary>
    public static void OpenAccounts(string dispute = null)
    {
        Open();
        if (dispute != null) AccountsPanel.Select(dispute);
        _instance.SetLife(LifeMode.Accounts);
    }

    public static void Hide()
    {
        if (IsOpen) _instance.Close();
    }

    public static void OpenSection(string section, string argument = null)
    {
        if (section == "Accounts") { OpenAccounts(argument); return; }
        if (section == "Tables" && int.TryParse(argument, out int settlement)) { OpenTables(settlement); return; }
        Open();
        if (section == "Heritage" && int.TryParse(argument, out int ruin)) HeritagePanel.Select(ruin);
        if (Enum.TryParse(section, out LifeMode mode)) _instance.SetLife(mode);
    }

    // ===== BUILD =====

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(CultureWindow));
        var canvas = CodeUI.Canvas(transform, "Culture Canvas", 3, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        _book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        _book.sizeDelta = new Vector2(1100f, 860f);
        CodeUI.Plate(_book, _theme, scaler);

        _title = CodeUI.Label(_book, "Title", string.Empty, _theme.titleSize + 10f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        _title.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -86f), new Vector2(-180f, -26f));
        _subtitle = CodeUI.Label(_book, "Subtitle", string.Empty, _theme.subtitleSize + 4f, _theme.subtitleColor, FontStyles.Normal, _theme);
        CodeUI.Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(38f, -120f), new Vector2(-36f, -86f));
        var close = CodeUI.TextButton(_book, "Close", Close, _theme);
        close.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -80f), new Vector2(-36f, -30f));

        BuildScroll();
        BuildBar();
        BuildReform();
        BuildLife();
    }

    private void BuildScroll()
    {
        _scrollArea = CodeUI.Panel(_book, "Scroll", Vector2.zero, Vector2.one);
        _scrollArea.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        _scroll = _scrollArea.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        var viewport = CodeUI.Panel(_scrollArea, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 16);
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _body = CodeUI.Label(content, "Body", string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        _body.alignment = TextAlignmentOptions.TopLeft;
        _body.lineSpacing = TooltipText.LineSpacing;
        var bar = CodeUI.Panel(_scrollArea, "Scrollbar", new Vector2(1f, 0f), Vector2.one);
        bar.offsetMin = new Vector2(-7f, 0f);
        bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        var handle = CodeUI.Solid(bar, "Handle", new Color(0.72f, 0.62f, 0.4f, 0.85f), true);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.viewport = viewport;
        _scroll.content = content;
        _scroll.verticalScrollbar = scrollbar;
        _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void BuildBar()
    {
        var bar = CodeUI.Panel(_book, "Actions", new Vector2(0f, 0f), new Vector2(1f, 0f));
        bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = new Vector2(36f, 30f);
        bar.offsetMax = new Vector2(-36f, 30f + BarHeight);
        var row = bar.gameObject.AddComponent<GridLayoutGroup>();
        row.spacing = new Vector2(6f, 3f);
        row.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        row.constraintCount = 4;
        row.cellSize = new Vector2(250f, 34f);
        _nameButton = ActionButton(bar, "Name the nation", () => CultureNamingDialog.Open(), "Name the nation, its citizens and its culture (Iridia, Citizens: Iridian, Culture: Iridian). A nation may rename itself; its history remembers.");
        ActionButton(bar, "Atlas", () => CultureAtlasWindow.Open(), "Follow practices through their evidence, places, carriers and public accounts; compare local cultures.");
        ActionButton(bar, "Map lens", () => { Close(); WorldView.ShowLens(WorldLens.Culture); }, "Open the world through the Culture lens: where your people's ways have taken root.");
        _reformButton = ActionButton(bar, "Reform", ToggleReform, "Turn the culture toward another way of living by the people's own will (the ways a fallen people's ruins record are taken in from Heritage).");
        _heritageButton = ActionButton(bar, "Heritage", () => ToggleLife(LifeMode.Heritage), "What a fallen people's ruins record and what can be taken from them (their civic, their ways, what they kept), and new forms where two traditions truly met: side by side, blended beside them, or in their place.");
        _ritesButton = ActionButton(bar, "Rites", () => ToggleLife(LifeMode.Rites), "Gatherings and rites: an evening of song, a rite of remembrance... Unity, morale and joy.");
        _kitchenButton = ActionButton(bar, "Kitchen", () => ToggleLife(LifeMode.Kitchen), "The Flavor Log: cook what the stores hold into dishes that feed more, and brew or distil drinks in the cellar, once or every Seventh.");
        _holidayButton = ActionButton(bar, "Holidays", () => ToggleLife(LifeMode.Holidays), "The calendar: holidays and observances (vigils, offerings, remembrances), when and how each is kept, and setting a new one apart.");
        _memorialButton = ActionButton(bar, "Memorials", () => ToggleLife(LifeMode.Memorials), "What your people remember of what they lost: keep a quiet remembrance, or dedicate a dish, landmark, rite or holiday to it.");
        _traditionButton = ActionButton(bar, "Traditions", () => ToggleLife(LifeMode.Traditions), "The practices your people keep: where each came from, who carried it, and the customs waiting for your decision (recognise, preserve locally, decide later).");
        _teachingButton = ActionButton(bar, "Teaching", () => ToggleLife(LifeMode.Teaching), "Apprenticeships and institutions: teach a custom as it is, adapt it into a local form, or write it down; found a Flavor Log or a guild at a landmark.");
        _tableButton = ActionButton(bar, "Tables", () => ToggleLife(LifeMode.Tables), "Set a communal table in a settlement from what the stores hold (open to all, for a community under strain, or hosted by a Legend), and see who has had a place at the table.");
        _performanceButton = ActionButton(bar, "Performances", () => ToggleLife(LifeMode.Performances), "What your cultural parties perform at their festivals and why it lands as it does: each voice's readiness, the history between them, the place.");
        _accountsButton = ActionButton(bar, "Accounts", () => ToggleLife(LifeMode.Accounts), "Public memory: say a practised tradition shows a promise in force, see what the records say for and against it, and answer the disputes when they disagree (acknowledge, revise, or a Legend's other account).");
    }

    private void BuildLife()
    {
        _lifePanel = CodeUI.Panel(_book, "Life", new Vector2(0f, 0f), new Vector2(1f, 0f));
        _lifePanel.pivot = new Vector2(0.5f, 0f);
        _lifePanel.offsetMin = new Vector2(36f, 30f + BarHeight + 6f);
        _lifePanel.offsetMax = new Vector2(-36f, 30f + BarHeight + 6f + LifeHeight);
        _lifePanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        _lifeText = CodeUI.Label(_lifePanel, "Why", string.Empty, _theme.bodySize - 2f, _theme.bodyColor, FontStyles.Normal, _theme);
        CodeUI.Place(_lifeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -58f), new Vector2(-14f, -6f));
        // The choices scroll when there are more than fit (a kitchen with the people's own recipes).
        var viewport = CodeUI.Panel(_lifePanel, "Choices Viewport", new Vector2(0f, 0f), new Vector2(1f, 1f));
        viewport.offsetMin = new Vector2(14f, 8f);
        viewport.offsetMax = new Vector2(-14f, -62f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _lifeGrid = CodeUI.Panel(viewport, "Choices", new Vector2(0f, 1f), new Vector2(1f, 1f));
        _lifeGrid.pivot = new Vector2(0.5f, 1f);
        _lifeLayout = _lifeGrid.gameObject.AddComponent<GridLayoutGroup>();
        _lifeLayout.spacing = new Vector2(12f, 4f);
        _lifeLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _lifeGrid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _lifeScroll = viewport.gameObject.AddComponent<ScrollRect>();
        _lifeScroll.horizontal = false;
        _lifeScroll.scrollSensitivity = 30f;
        _lifeScroll.movementType = ScrollRect.MovementType.Clamped;
        _lifeScroll.viewport = viewport;
        _lifeScroll.content = _lifeGrid;
        _lifePanel.gameObject.SetActive(false);
    }

    private TextMeshProUGUI ActionButton(Transform parent, string text, Action onClick, string tip)
    {
        var button = CodeUI.TextButton(parent, text, onClick, _theme, _theme.subtitleSize + 2f);
        button.textWrappingMode = TextWrappingModes.NoWrap;
        button.overflowMode = TextOverflowModes.Ellipsis;
        button.color = _theme.titleColor;
        TooltipTrigger.Ensure(button.gameObject).SetCustom(text, tip);
        return button;
    }

    private void BuildReform()
    {
        _reformPanel = CodeUI.Panel(_book, "Reform", new Vector2(0f, 0f), new Vector2(1f, 0f));
        _reformPanel.pivot = new Vector2(0.5f, 0f);
        _reformPanel.offsetMin = new Vector2(36f, 30f + BarHeight + 6f);
        _reformPanel.offsetMax = new Vector2(-36f, 30f + BarHeight + 6f + ReformHeight);
        _reformPanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        _reformText = CodeUI.Label(_reformPanel, "Why", string.Empty, _theme.bodySize - 2f, _theme.bodyColor, FontStyles.Normal, _theme);
        CodeUI.Place(_reformText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -46f), new Vector2(-14f, -6f));
        var grid = CodeUI.Panel(_reformPanel, "Families", new Vector2(0f, 0f), new Vector2(1f, 1f));
        grid.offsetMin = new Vector2(14f, 44f);
        grid.offsetMax = new Vector2(-14f, -48f);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(196f, 26f);
        layout.spacing = new Vector2(10f, 4f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        foreach (var family in CultureRules.Families)
        {
            var f = family;
            var button = CodeUI.TextButton(grid, family.ToString(), () => Choose(f), _theme, _theme.bodySize);
            button.textWrappingMode = TextWrappingModes.NoWrap;
            _familyButtons.Add(button);
        }
        _confirm = CodeUI.TextButton(_reformPanel, "Confirm", ConfirmReform, _theme, _theme.subtitleSize + 5f);
        _confirm.color = _theme.titleColor;
        CodeUI.Place(_confirm.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 8f), new Vector2(-14f, 40f));
        _reformPanel.gameObject.SetActive(false);
    }

    // ===== OPEN / CLOSE =====

    private void Show()
    {
        if (_root == null) Build();
        if (_culture == null)
        {
            _culture = CultureSystem.Instance;
            if (_culture != null) _culture.Changed += OnChanged;
        }
        _root.gameObject.SetActive(true);
        _chosen = null;
        SetReformOpen(false);
        MemorialPanel.Reset();
        TraditionPanel.Reset();
        ObservancePanel.Reset();
        TeachingPanel.Reset();
        HospitalityPanel.Reset();
        PerformancePanel.Reset();
        AccountsPanel.Reset();
        HeritagePanel.Reset();
        SetLife(LifeMode.None);
        Refresh();
        _scroll.verticalNormalizedPosition = 1f;
    }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void OnDestroy()
    {
        if (_culture != null) _culture.Changed -= OnChanged;
        if (_instance == this) _instance = null;
    }

    private void OnChanged()
    {
        if (IsOpen) Refresh();
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;
    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (CultureAtlasWindow.IsOpen || CultureAtlasWindow.ClosedFrame == Time.frameCount) return;
        if (CultureNamingDialog.IsOpen || CultureNamingDialog.ClosedFrame == Time.frameCount) return;
        if (RecipeInventionDialog.IsOpen || RecipeInventionDialog.ClosedFrame == Time.frameCount) return;
        if (IsOpen) Close();
    }

    // ===== REFORM =====

    private void ToggleReform() => SetReformOpen(!_reformPanel.gameObject.activeSelf);

    private void SetReformOpen(bool open)
    {
        if (_reformPanel == null) return;
        bool can = _culture != null && CultureSystem.IsFounded;
        if (open && can && _lifePanel != null) { _mode = LifeMode.None; _lifePanel.gameObject.SetActive(false); }
        _reformPanel.gameObject.SetActive(open && can);
        LayoutPanels();
        if (_reformPanel.gameObject.activeSelf) RefreshReform();
    }

    // The scroll ends above whichever lower panel is open.
    private void LayoutPanels()
    {
        float panel = _reformPanel != null && _reformPanel.gameObject.activeSelf ? ReformHeight + 6f : _lifePanel != null && _lifePanel.gameObject.activeSelf ? LifeHeight + 6f : 0f;
        _scrollArea.offsetMin = new Vector2(36f, 30f + BarHeight + 10f + panel);
        _scrollArea.offsetMax = new Vector2(-30f, -134f);
    }

    // ===== RITES, KITCHEN, HOLIDAYS =====

    private void ToggleLife(LifeMode mode) => SetLife(_mode == mode ? LifeMode.None : mode);

    private void SetLife(LifeMode mode)
    {
        if (_lifePanel == null) return;
        bool can = _culture != null && CultureSystem.IsFounded;
        _mode = can ? mode : LifeMode.None;
        if (_mode != LifeMode.None && _reformPanel != null) _reformPanel.gameObject.SetActive(false);
        _lifePanel.gameObject.SetActive(_mode != LifeMode.None);
        LayoutPanels();
        RefreshLife();
        if (_lifeScroll != null) _lifeScroll.verticalNormalizedPosition = 1f;
    }

    private void RefreshLife()
    {
        if (_lifePanel == null || !_lifePanel.gameObject.activeSelf || _culture == null) return;
        for (int i = _lifeGrid.childCount - 1; i >= 0; i--) Destroy(_lifeGrid.GetChild(i).gameObject);
        float width = 1100f - 72f - 28f;
        switch (_mode)
        {
            case LifeMode.Rites: Rites(width); break;
            case LifeMode.Kitchen: Kitchen(width); break;
            case LifeMode.Holidays: Columns(1, width); ObservancePanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Memorials: Columns(1, width); MemorialPanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Traditions: Columns(1, width); TraditionPanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Teaching: Columns(1, width); TeachingPanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Tables: Columns(1, width); HospitalityPanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Performances: Columns(1, width); PerformancePanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Accounts: Columns(1, width); AccountsPanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
            case LifeMode.Heritage: Columns(1, width); HeritagePanel.Fill(_culture, t => _lifeText.text = KeywordMarkup.SafeGlyphs(t), Choice); break;
        }
    }

    private void Columns(int count, float width)
    {
        _lifeLayout.constraintCount = count;
        _lifeLayout.cellSize = new Vector2((width - _lifeLayout.spacing.x * (count - 1)) / count, 24f);
    }

    private void Choice(string label, string why, string tip, Action act)
    {
        var button = CodeUI.TextButton(_lifeGrid, label, () => { act(); Refresh(); }, _theme, _theme.bodySize - 1f);
        button.textWrappingMode = TextWrappingModes.NoWrap;
        button.overflowMode = TextOverflowModes.Ellipsis;
        button.alignment = TextAlignmentOptions.MidlineLeft;
        button.color = why == null ? _theme.titleColor : _theme.subtitleColor;
        var b = button.GetComponent<Button>();
        if (b != null) b.interactable = why == null;
        TooltipTrigger.Ensure(button.gameObject).SetCustom(StripTags(label), why != null ? TooltipText.Bad(why) + (tip != null ? "\n" + tip : string.Empty) : tip);
    }

    private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? string.Empty, "<.*?>", string.Empty);

    private void Rites(float width)
    {
        var life = _culture.Life;
        _lifeText.text = KeywordMarkup.SafeGlyphs($"Gatherings and rites bring {CultureSystem.PeopleWord} together: Unity ({_culture.UnityHeld:0} held), morale for a while, and joy that fades over the Sevenths. Each leans the culture toward its way of living.");
        Columns(1, width);
        foreach (var a in life.activities.Where(a => a != null))
        {
            var activity = a;
            string why = _culture.WhyNotActivity(activity);
            string gives = $"+{activity.unity:0.#} Unity{(activity.morale != 0 ? $", {activity.morale:+0;-0} morale for {activity.moraleSevenths} Sevenths" : string.Empty)}";
            string again = activity.cooldownSevenths > 0 ? $"; again after {activity.cooldownSevenths} Sevenths" : string.Empty;
            Choice($"{activity.name} ({CultureSystem.CostText(activity.cost, activity.foodValue)}; {gives}{again})", why, activity.description, () => _culture.HoldActivity(activity.id));
        }
    }

    // The Kitchen panel shows the kitchen's dishes or the cellar's drinks; its first row switches between them.
    private bool _cellar;

    private void Kitchen(float width)
    {
        var life = _culture.Life;
        string civic = _culture.KitchenCivic ? $" A civic of the kitchen is in force: +{life.kitchenCivicBonus:P0} per batch." : string.Empty;
        _lifeText.text = KeywordMarkup.SafeGlyphs(_cellar
            ? $"The cellar: ales, meads and wines fermented from the stores, and spirits distilled from them (never from an Eleos Bloom: that is a tea). Drinks are kept for joy rather than hunger, and the stores open them last. A standing order brews every Seventh.{civic}"
            : $"The Flavor Log: dishes feed more than what went into them, and the fine ones are luxuries. A standing order cooks every Seventh as far as the stores allow.{civic}");
        Columns(2, width);
        Choice(_cellar ? "The kitchen: dishes" : "<u>The kitchen: dishes</u>", null, "Dishes cooked from the stores: they feed more than their ingredients did.", () => _cellar = false);
        Choice(_cellar ? "<u>The cellar: drinks</u>" : "The cellar: drinks", null, "Beverages brewed and distilled from the stores: ales, meads, wines and spirits.", () => _cellar = true);
        // The people's own: choose what goes in, and name it.
        string noun = _cellar ? "drink" : "dish";
        int own = _culture.Invented.Count;
        string whyNotOwn = own >= life.maxInvented ? $"Your people already keep {life.maxInvented} recipes of their own." : null;
        bool cellar = _cellar;
        Choice($"Invent a {noun} of your own...", whyNotOwn,
            $"Choose what goes in and try a batch: no one knows what a mix makes until it is tasted, and a batch uses up what goes in. Keep what is worth a name. Your people take to their own sooner than to anything else: it is the first offered as national.",
            () => RecipeInventionDialog.Open(cellar));
        Choice(TooltipText.Muted($"Your own recipes: {own} of {life.maxInvented}"), null, own > 0 ? string.Join(", ", _culture.Invented.Select(i => i.name)) : "None yet.", () => { });
        // The recipes no one wrote down: their hints so far, and the ones found (KitchenTrials).
        var whispers = _culture.Whispers().Where(w => w.spec.Method == KitchenMethod.Cook ? !cellar : cellar).ToList();
        if (whispers.Count > 0)
        {
            int found = whispers.Count(w => w.Found);
            string lines = string.Join("\n", whispers.Select(w => w.Found ? TooltipText.Good($"{w.spec.formula.name}: found") : $"<i>{string.Join(" ", w.hints)}</i>"));
            Choice(TooltipText.Muted($"Whispered recipes: {found} of {whispers.Count} found ({_culture.Kitchen.total} batches tried)"), null,
                $"{lines}\n\n{TooltipText.Muted("Never written down: found only by trying batches. Each batch says how near it came, and coming nearer brings the next hint to light.")}",
                () => RecipeInventionDialog.Open(cellar));
        }
        foreach (var r in life.recipes.Where(r => r != null && r.InCellar == _cellar))
        {
            var recipe = r;
            var invented = _culture.InventedFood(recipe.dish);
            float made = _culture.YieldOf(recipe);
            var (fin, fout) = CultureLifeRules.FoodValue(recipe, CultureSystem.FoodValueOf, made);
            string inputs = string.Join(" + ", recipe.inputs.Where(i => i != null).Select(i => $"{i.amount:0.#} {i.resource}"));
            string why = _culture.WhyNotCook(recipe);
            string use = CultureLifeRules.ResourceUse(life, recipe.dish);
            string tip = $"{recipe.description}\n{TooltipText.Row("Makes", $"{made:0.#} {recipe.dish} from {inputs}")}\n{TooltipText.Row("Food value", $"{fin:0.#} in, {fout:0.#} out")}"
                + (recipe.InCellar ? "\n" + TooltipText.Row("Keeps", Keeps(recipe.dish)) : string.Empty)
                + (recipe.unity > 0f ? "\n" + TooltipText.Row("Memorial", $"+{recipe.unity:0.#} Unity a batch") : string.Empty)
                + (recipe.InCellar && use != null ? "\n" + TooltipText.Muted(use) : string.Empty)
                + (invented != null ? "\n" + TooltipText.Row("Your people's own", invented.templateDish != null ? $"made the way of {invented.templateDish}" : "invented") + "\n"
                    + TooltipText.Muted("It grows familiar sooner than anything else and is the first offered as national.") : string.Empty);
            int can = _culture.BatchesPossible(recipe);
            string possible = why == null ? $", {can} possible" : string.Empty;
            Choice(recipe.InCellar ? $"{recipe.Verb} {recipe.dish} (+{made:0.#}{possible})" : $"{recipe.Verb} {recipe.dish} ({fin:0.#} to {fout:0.#} food value{possible})", why, tip, () => _culture.Cook(recipe.id));
            int order = _culture.StandingOrder(recipe.id);
            bool known = string.IsNullOrEmpty(recipe.technology) || why == null || !why.StartsWith("Research", StringComparison.Ordinal);
            Choice(order > 0 ? $"Every Seventh: {order} batch{(order == 1 ? "" : "es")} (change)" : "Every Seventh: none (change)", known && CultureSystem.IsFounded ? null : why,
                $"Standing order: up to {life.maxStandingBatches} batches of {recipe.dish} each Seventh, as far as the stores allow.",
                () => _culture.SetStandingOrder(recipe.id, (order + 1) % (life.maxStandingBatches + 1)));
        }
    }

    // How long a drink keeps in the stores (spirits never spoil).
    private static string Keeps(string resource)
    {
        var kind = Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(resource) : null;
        if (kind == null) return "unknown";
        float spoil = kind.spoilPerSeventh * Pantry.Instance.PreservationMultiplier;
        return spoil <= 0f ? "never spoils" : $"{spoil:P1} of the stock spoils each Seventh";
    }

    private void Holidays(float width)
    {
        var now = _culture.Now;
        string why = _culture.WhyNotHoliday();
        var preview = _culture.HolidayPreview();
        _lifeText.text = KeywordMarkup.SafeGlyphs($"Today is the {CultureCalendar.Ordinal(now.seventh)} Seventh of the {now.phaseName ?? CultureCalendar.PhaseWord(now.phase) + " Phase"}{(now.echoName != null ? ", " + now.echoName : string.Empty)}, Cycle {now.cycle}. A holiday set apart today returns on {CultureCalendar.Day(now.seventh, now.phase)}.");
        Columns(1, width);
        Choice($"Establish {preview.name}, remembering {preview.occasion} ({_culture.NextHolidayCost:0} Unity; morale {_culture.Life.holidayMoraleMargin}+ above balance)", why,
            $"Kept on its day every Echo: +{_culture.Life.holidayUnity:0.#} Unity, {_culture.Life.holidayMorale:+0} morale, joy, and a table set from the stores. Each holiday on the calendar adds {_culture.Life.holidayMaxMorale:0.#} max morale for good.",
            () => _culture.EstablishHoliday());
        foreach (var h in _culture.Holidays)
        {
            int until = CultureCalendar.SeventhsUntil(h, now.seventh, now.phase);
            Choice($"{h.name}: {CultureCalendar.Day(h)} ({(until == 0 ? "today" : $"in {until} Seventh{(until == 1 ? "" : "s")}")})", null,
                $"Set apart on {CultureCalendar.Established(h)} to remember {h.occasion}; kept {h.kept} time{(h.kept == 1 ? "" : "s")}.", () => { });
        }
    }

    private void Choose(EnclaveFamily family)
    {
        _chosen = family;
        RefreshReform();
    }

    private void ConfirmReform()
    {
        if (_culture == null || !_chosen.HasValue) return;
        if (_culture.Reform(_chosen.Value))
        {
            _chosen = null;
            SetReformOpen(false);
        }
        else RefreshReform();
    }

    private void RefreshReform()
    {
        if (_culture == null) return;
        string why = _culture.WhyNotReform();
        _reformText.text = KeywordMarkup.SafeGlyphs(why != null ? TooltipText.Bad(why) : $"Choose the way to turn toward: {CultureRules.Percent(_culture.Tuning.reformShift)} of the culture moves to it. {_culture.ReformCostText()}");
        for (int i = 0; i < _familyButtons.Count; i++)
        {
            var family = CultureRules.Families[i];
            float share = _culture.Leaning(family);
            bool chosen = _chosen.HasValue && _chosen.Value == family;
            _familyButtons[i].text = $"{(chosen ? "> " : string.Empty)}{family} {TooltipText.Muted(CultureRules.Percent(share))}";
            _familyButtons[i].color = chosen ? _theme.titleColor : _theme.subtitleColor;
        }
        bool ready = why == null && _chosen.HasValue;
        _confirm.text = ready ? $"Confirm: reform toward the {_chosen.Value} ways" : why == null ? "Choose a way above" : "Reform unavailable";
        var button = _confirm.GetComponent<Button>();
        if (button != null) button.interactable = ready;
    }

    // ===== TEXT =====

    private void Refresh()
    {
        var culture = _culture != null ? _culture : CultureSystem.Instance;
        bool founded = CultureSystem.IsFounded;
        _title.text = KeywordMarkup.SafeGlyphs(CultureSystem.NationName);
        _subtitle.text = KeywordMarkup.SafeGlyphs(CultureSystem.IsNamed
            ? $"Citizens: {CultureSystem.Demonym}     Culture: {CultureSystem.Adjective}     Character: {culture.Character}"
            : founded ? "Your people have not given themselves a name yet." : "A people without a founding myth.");
        _nameButton.text = CultureSystem.IsNamed ? "Rename" : "Name the nation";
        _nameButton.GetComponent<Button>().interactable = founded;
        _reformButton.GetComponent<Button>().interactable = founded;
        foreach (var b in new[] { _ritesButton, _kitchenButton, _holidayButton, _memorialButton, _traditionButton, _tableButton, _teachingButton, _performanceButton, _accountsButton, _heritageButton }) b.GetComponent<Button>().interactable = founded;
        _body.text = KeywordMarkup.SafeGlyphs(culture == null ? "There is no culture in this world." : founded ? Describe(culture) : Unfounded());
        if (_reformPanel.gameObject.activeSelf) RefreshReform();
        RefreshLife();
    }

    private static string Unfounded()
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading("Before the founding"));
        text.AppendLine("Your people are survivors, gathered but not yet a people: no one has said why they came together.");
        text.AppendLine();
        text.AppendLine(TooltipText.Muted("Research Horology: once the Sevenths can be counted, the founding myth is told (\"Why were we founded?\"), and your people name their nation, themselves and their ways. From then on the culture grows over the land you hold, takes in what your people eat and work, your pillars, civics and districts, and in time makes a food its own."));
        return text.ToString().TrimEnd();
    }

    private static string Describe(CultureSystem culture)
    {
        var text = new StringBuilder();
        var tuning = culture.Tuning;

        // The founding myth.
        var myth = culture.Myth;
        if (myth != null)
        {
            text.AppendLine(TooltipText.Heading("The founding myth", myth.stance));
            text.AppendLine(TooltipText.Value(myth.title) + ": " + myth.answer);
            text.AppendLine(TooltipText.Quote(TooltipText.Muted(myth.summary)));
            text.AppendLine(TooltipText.Row("For as long as it is told", string.Join(", ", myth.effects.Select(e => e.Describe()))));
            text.AppendLine();
        }

        // Character and leanings.
        var target = culture.TargetLeanings();
        text.AppendLine(TooltipText.Heading("Character", culture.Character));
        foreach (var family in culture.Ranked)
        {
            float now = culture.Leaning(family), toward = target[(int)family];
            if (now < 0.005f && toward < 0.005f) continue;
            string drift = Mathf.Abs(toward - now) < 0.01f ? TooltipText.Muted("steady") : toward > now ? TooltipText.Good($"rising toward {CultureRules.Percent(toward)}") : TooltipText.Bad($"falling toward {CultureRules.Percent(toward)}");
            text.AppendLine(TooltipText.Row($"{family} {TooltipText.Muted(new string('|', Mathf.Clamp(Mathf.RoundToInt(now * 60f), 0, 40)))}", $"{CultureRules.Percent(now)} ({drift})"));
        }
        var dominant = culture.Dominant;
        if (dominant.HasValue)
        {
            var effects = tuning.character.Where(c => c != null && c.family == dominant.Value).Select(c => c.ToEffect().Describe()).ToList();
            if (effects.Count > 0) text.AppendLine(TooltipText.Row($"As a {dominant.Value} people", string.Join(", ", effects)));
        }
        text.AppendLine(TooltipText.Muted("Each Seventh the culture drifts a little toward what it lives: its founding myth, its pillars (Aureus: Auric, Regalia: Regal, Waltz: Weaver, Chorus: Esoteric), its civics, what its people eat and work, its land and its districts. Its leading leaning is its character."));
        text.AppendLine();

        DescribeLife(culture, text);
        TraditionPanel.Describe(culture, text);
        TeachingPanel.Describe(culture, text);
        HospitalityPanel.Describe(culture, text);
        PerformancePanel.Describe(culture, text);
        AccountsPanel.Describe(culture, text);

        // Foodways.
        text.AppendLine(TooltipText.Heading("Foodways"));
        if (!string.IsNullOrEmpty(culture.PendingFood))
        {
            var pending = culture.FoodwayOf(culture.PendingFood);
            text.AppendLine(TooltipText.Warn($"{culture.PendingFood} waits to be called the {CultureRules.NationalLabel(pending != null ? pending.cuisine : FoodClass.Edible)}."));
        }
        foreach (var cuisine in CultureRules.TableClasses)
        {
            var ways = culture.Foodways.Where(f => f.cuisine == cuisine && (f.national || f.familiarity >= 0.01f)).OrderByDescending(f => f.national).ThenByDescending(f => f.familiarity).ToList();
            text.AppendLine(TooltipText.Value(CultureRules.ClassName(cuisine, true)));
            if (ways.Count == 0) text.AppendLine(TooltipText.Bullet(TooltipText.Muted("None known yet.")));
            foreach (var way in ways)
            {
                string status = way.national ? TooltipText.Good($"the {CultureRules.NationalLabel(cuisine)}") + TooltipText.Muted($" since Seventh {way.nationalSeventh}{AgeText(way.nationalAge)}")
                    : way.declined ? TooltipText.Muted("kept off the national table")
                    : way.firstSevenths > 0 ? TooltipText.Muted($"first at the table for {way.firstSevenths} Seventh{(way.firstSevenths == 1 ? "" : "s")}") : string.Empty;
                text.AppendLine(TooltipText.Bullet($"{way.resource}{(way.invented ? TooltipText.Muted(" (your own)") : string.Empty)}: familiarity {CultureRules.Percent(way.familiarity)}{(status.Length > 0 ? ", " + status : string.Empty)}"));
            }
        }
        text.AppendLine(TooltipText.Muted($"What your people eat and keep grows familiar, each class apart. The first of a class, {CultureRules.Percent(tuning.nationalFamiliarity)} familiar for {tuning.nationalSevenths} Sevenths in a row, is offered as national: embraced, its output grows {tuning.nationalFoodBonusPercent:0}%. Your people's own dishes and drinks need only {CultureRules.Percent(tuning.nationalFamiliarity * tuning.inventedNationalEase)} for {Mathf.CeilToInt(tuning.nationalSevenths * tuning.inventedNationalEase)} Sevenths, and are offered first. Edibles and Eleos Teas feed; ingredients are eaten raw only when they run out; spices never."));
        text.AppendLine();

        // The land.
        int held = culture.HeldCells().Count();
        text.AppendLine(TooltipText.Heading("The land"));
        text.AppendLine(TooltipText.Row("Rootedness", $"{CultureRules.Percent(culture.Cohesion)} of the land you hold"));
        text.AppendLine(TooltipText.Row("Cells reached", $"{culture.CellsReached} ({held} held)"));
        text.AppendLine(TooltipText.Muted("Held land takes on the culture, fastest near settlements; land no longer held lets it fade. See it with the Culture lens. Rootedness is how long your people have lived there, not whether they agree: each settlement keeps its own customs (its card on the world map)."));
        text.AppendLine();

        DescribePlaces(culture, text);
        MemorialPanel.Describe(culture, text);
        HeritagePanel.Describe(culture, text);

        // Reforms.
        text.AppendLine(TooltipText.Heading("Reforms", culture.Reforms > 0 ? $"{culture.Reforms} so far" : null));
        string why = culture.WhyNotReform();
        text.AppendLine(why ?? culture.ReformCostText());
        if (culture.FoundingCivics.Count > 0)
            text.AppendLine(TooltipText.Row("Civics at the founding", string.Join(", ", culture.FoundingCivics) + (culture.State.foundingCivicsGone ? TooltipText.Muted(" (none remains)") : string.Empty)));
        text.AppendLine();

        // History.
        text.AppendLine(TooltipText.Heading("Heritage"));
        foreach (var moment in culture.Moments.Reverse().Take(24))
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(moment.title)} {TooltipText.Muted($"(Seventh {moment.seventh}{AgeText(moment.ageId)})")}: {moment.text}"));
        return text.ToString().TrimEnd();
    }

    // How the people live: happiness (surviving and living), luxuries, Unity, the calendar and the kitchen.
    private static void DescribeLife(CultureSystem culture, StringBuilder text)
    {
        var life = culture.Life;
        var w = culture.CurrentWellbeing();
        string mood = CultureLifeRules.MoodWord(culture.Happiness);
        int morale = CultureLifeRules.Morale(culture.Happiness, life);
        text.AppendLine(TooltipText.Heading("How the people live", $"{culture.Happiness:0} happiness, {mood}"));
        text.AppendLine(TooltipText.Row("Surviving", $"{CultureRules.Percent(culture.State.survival)} (fed, housed, stores that feel secure, a varied cellar)"));
        text.AppendLine(TooltipText.Row("Living", $"{CultureRules.Percent(culture.State.living)} (luxuries, fine dishes, joy {CultureRules.Percent(culture.Joy)}, landmarks)"));
        text.AppendLine(TooltipText.Row("Living weighs", w.weight <= 0f ? TooltipText.Muted($"nothing yet: below {life.livingFrom} citizens the people only ask to survive")
            : $"{CultureRules.Percent(w.weight / 2f)} of happiness (half, as much as surviving, from {life.livingFull} citizens)"));
        if (morale != 0) text.AppendLine(TooltipText.Row("Morale", morale > 0 ? TooltipText.Good($"{morale:+0} while they are {mood}") : TooltipText.Bad($"{morale:+0;-0} while they are {mood}")));
        int wanted = CultureLifeRules.WantedLuxuries(PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.population : 0, life);
        if (wanted > 0)
        {
            text.AppendLine(TooltipText.Row("Luxuries & amenities", $"{wanted} kind{(wanted == 1 ? "" : "s")} wanted, {CultureRules.Percent(culture.LuxuryMet)} met last Seventh"));
            foreach (var met in culture.LuxuriesMet.OrderByDescending(m => m.met))
            {
                var spec = life.luxuries.FirstOrDefault(l => l != null && l.category == met.category);
                var sources = (spec?.resources ?? new List<string>()).Select(r => r + (life.Good(r)?.amenity == true ? " (kept)" : "")).ToList();
                if (life.gardens.Any(g => g.category == met.category)) sources.Add("surveyed living blooms on your land");
                text.AppendLine(TooltipText.Bullet($"{met.category}: {CultureRules.Percent(met.met)} {TooltipText.Muted($"({string.Join(", ", sources)})")}"));
            }
            text.AppendLine(TooltipText.Row("Faith from cultural goods", $"+{culture.State.culturalFaith:0.##} last Seventh"));
            text.AppendLine(TooltipText.Muted("Only the best available categories are used. Sky Glass is kept; living gardens need surveyed land you hold and healthy blooms. Sacred teas, glass and gardens also give Faith. Food is reserved for survival during shortages."));
        }
        text.AppendLine(TooltipText.Muted($"On the frontier the people ask only to survive. Past {life.livingFrom} citizens they want to live as well: luxuries (one more kind every {life.peoplePerLuxury} people), fine dishes, festivals and holidays, landmarks."));
        text.AppendLine();

        // Unity.
        var u = culture.UnityPerSeventh();
        text.AppendLine(TooltipText.Heading("Unity", $"{culture.UnityHeld:0} held, {u.Total:+0.#;-0.#;0} a Seventh"));
        text.AppendLine(TooltipText.Row("Song", $"{u.song:0.#} (the Weaver share{(string.Equals(culture.Myth?.id, "song", StringComparison.OrdinalIgnoreCase) ? " and the Song myth" : string.Empty)})"));
        if (u.civics > 0f) text.AppendLine(TooltipText.Row("Civics of music and rite", $"{u.civics:0.#}"));
        if (u.districts > 0f) text.AppendLine(TooltipText.Row("Districts of song, faith and pleasure", $"{u.districts:0.#}"));
        if (u.landmarks > 0f) text.AppendLine(TooltipText.Row("Landmarks", $"{u.landmarks:0.#}"));
        text.AppendLine(TooltipText.Row("Happiness", $"x{u.multiplier:0.##}"));
        text.AppendLine(TooltipText.Muted("Unity is the people's shared life: made by song, rites, festivals and holidays, spent to raise landmarks and set holidays apart. Cultural parties (an expedition charter) hold festivals in your settlements."));
        text.AppendLine();

        // The calendar.
        text.AppendLine(TooltipText.Heading("The calendar", culture.Holidays.Count > 0 ? $"{culture.Holidays.Count} holiday{(culture.Holidays.Count == 1 ? "" : "s")}" : null));
        var now = culture.Now;
        if (culture.Holidays.Count == 0)
            text.AppendLine(TooltipText.Muted(culture.FestivalsHeld > 0 ? "No holiday yet. With morale high and Unity to spend, set today apart (Holidays below)." : "No holiday yet: a people sets a day apart once it has learned to celebrate (hold a festival first)."));
        foreach (var h in culture.Holidays.OrderBy(h => CultureCalendar.SeventhsUntil(h, now.seventh, now.phase)))
        {
            int until = CultureCalendar.SeventhsUntil(h, now.seventh, now.phase);
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(h.name)}: {CultureCalendar.Day(h)}, {(until == 0 ? TooltipText.Good("today") : $"in {until} Seventh{(until == 1 ? "" : "s")}")} {TooltipText.Muted($"(remembers {h.occasion}; kept {h.kept} time{(h.kept == 1 ? "" : "s")})")}"));
        }
        ObservancePanel.Describe(culture, text);
        if (culture.FestivalsHeld > 0) text.AppendLine(TooltipText.Row("Festivals held", culture.FestivalsHeld.ToString()));
        text.AppendLine();

        // The kitchen.
        var made = new List<string>();
        if (culture.DishesCooked > 0f) made.Add($"{culture.DishesCooked:0} dishes cooked");
        if (culture.DrinksMade > 0f) made.Add($"{culture.DrinksMade:0} drinks brewed");
        text.AppendLine(TooltipText.Heading("The kitchen and cellar", made.Count > 0 ? string.Join(", ", made) : null));
        var orders = life.recipes.Where(r => r != null && culture.StandingOrder(r.id) > 0).Select(r => $"{culture.StandingOrder(r.id)} {r.dish}").ToList();
        text.AppendLine(orders.Count > 0 ? TooltipText.Row("Every Seventh", string.Join(", ", orders)) : TooltipText.Muted("No standing orders: open the Kitchen below to cook dishes that feed more than their ingredients, or to brew drinks in the cellar."));
        text.AppendLine();
    }

    // The names the culture gave its places and the landmarks it raised.
    private static void DescribePlaces(CultureSystem culture, StringBuilder text)
    {
        if (culture.Names.Count == 0 && culture.Landmarks.Count == 0) return;
        text.AppendLine(TooltipText.Heading("Names and landmarks"));
        foreach (var n in culture.Names.Reverse().Take(10))
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(n.name)} {TooltipText.Muted($"(once {n.former}; named in a {n.voice} voice, Seventh {n.seventh})")}"));
        foreach (var l in culture.Landmarks.Reverse().Take(10))
        {
            var spec = culture.Life.Landmark(l.spec);
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(l.name)}, a {spec?.name.ToLowerInvariant() ?? l.spec} {TooltipText.Muted($"(Seventh {l.seventh}{AgeText(l.ageId)})")}"));
        }
        text.AppendLine(TooltipText.Muted("The culture names its hamlets and districts in its own voice (its character and founding myth), and each district of faith, song or pleasure can raise landmarks (from the district's card on the map)."));
        text.AppendLine();
    }

    private static string AgeText(string ageId)
    {
        if (string.IsNullOrEmpty(ageId)) return string.Empty;
        var age = GameCatalog.Ages.All.FirstOrDefault(a => a != null && a.id == ageId);
        return age != null ? ", " + age.title : string.Empty;
    }
}
