using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The people invent a dish (the kitchen) or a drink (the cellar: brewed or distilled) and name it
/// (<see cref="CultureSystem.Invent"/>). The player picks what goes in (each click adds one more of it, past the most
/// it wraps to none) and learns only what it will be something like. What it makes is known once a batch is tried
/// (<see cref="CultureSystem.TryBatch"/>, which uses up what goes in, and says how near it came to one of the Flavor Log's
/// whispered recipes); only a mix the people have tasted can be kept and named. Opened from the Culture window's Kitchen
/// panel; built in code (<see cref="CodeUI"/>), over the Culture window.
/// </summary>
public class RecipeInventionDialog : MonoBehaviour
{
    private const int MaxOfOne = 6;
    private static RecipeInventionDialog _instance;

    private TooltipTheme _theme;
    private RectTransform _root, _grid;
    private TextMeshProUGUI _title, _intro, _preview, _error, _brew, _distill, _confirm, _try;
    private TMP_InputField _name;
    private KitchenMethod _method;
    private readonly List<ResourceAmount> _inputs = new List<ResourceAmount>();

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>Open for the kitchen (a dish) or the cellar (a drink, brewed first).</summary>
    public static void Open(bool cellar)
    {
        if (CultureSystem.Instance == null || !CultureSystem.IsFounded) return;
        if (_instance == null) _instance = new GameObject("Recipe Invention Dialog").AddComponent<RecipeInventionDialog>();
        _instance.Show(cellar ? KitchenMethod.Ferment : KitchenMethod.Cook);
    }

    // ===== BUILD =====

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(RecipeInventionDialog));
        var canvas = CodeUI.Canvas(transform, "Recipe Invention Canvas", 1, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.6f), true);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(1200f, 800f);
        CodeUI.Plate(book, _theme, scaler);

        _title = CodeUI.Label(book, "Title", string.Empty, _theme.titleSize + 8f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -86f), new Vector2(-40f, -28f));
        _intro = CodeUI.Label(book, "Intro", string.Empty, _theme.bodySize - 1f, _theme.bodyColor, FontStyles.Normal, _theme);
        _intro.alignment = TextAlignmentOptions.TopLeft;
        CodeUI.Place(_intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -150f), new Vector2(-40f, -90f));

        // How it is made (the cellar chooses between brewing and distilling).
        _brew = CodeUI.TextButton(book, "Brew", () => SetMethod(KitchenMethod.Ferment), _theme, _theme.subtitleSize + 4f);
        CodeUI.Place(_brew.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -192f), new Vector2(200f, -156f));
        TooltipTrigger.Ensure(_brew.gameObject).SetCustom("Brew", "Ferment it: an ale, a mead, a wine.");
        _distill = CodeUI.TextButton(book, "Distill", () => SetMethod(KitchenMethod.Distill), _theme, _theme.subtitleSize + 4f);
        CodeUI.Place(_distill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -192f), new Vector2(400f, -156f));
        TooltipTrigger.Ensure(_distill.gameObject).SetCustom("Distill", "Run something fermented through the still: a spirit.");

        _name = Field(book, "Its name", "Hearthside Stew", -200f);
        _name.onValueChanged.AddListener(_ => RefreshPreview());
        _name.onSubmit.AddListener(_ => Confirm());

        var caption = CodeUI.Label(book, "What goes in", "What goes in (click to add one more)", _theme.subtitleSize + 3f, _theme.subtitleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -330f), new Vector2(600f, -300f));
        var clear = CodeUI.TextButton(book, "Clear", () => { _inputs.Clear(); Refresh(); }, _theme, _theme.subtitleSize + 2f);
        clear.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(clear.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, -330f), new Vector2(700f, -300f));
        _grid = CodeUI.Panel(book, "Ingredients", new Vector2(0f, 0f), new Vector2(0f, 1f));
        CodeUI.Place(_grid, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 130f), new Vector2(700f, -336f));
        _grid.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = new Vector2(10f, 3f);
        layout.cellSize = new Vector2(206f, 23f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        _preview = CodeUI.Label(book, "Preview", string.Empty, _theme.bodySize - 2f, _theme.bodyColor, FontStyles.Normal, _theme);
        _preview.alignment = TextAlignmentOptions.TopLeft;
        CodeUI.Place(_preview.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(730f, 130f), new Vector2(-40f, -200f));

        _error = CodeUI.Label(book, "Error", string.Empty, _theme.bodySize - 1f, new Color(1f, 0.55f, 0.45f), FontStyles.Italic, _theme);
        CodeUI.Place(_error.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 80f), new Vector2(-40f, 124f));
        _confirm = CodeUI.TextButton(book, "Keep it", Confirm, _theme, _theme.subtitleSize + 8f);
        _confirm.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(_confirm.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 28f), new Vector2(-40f, 74f));
        TooltipTrigger.Ensure(_confirm.gameObject).SetCustom("Keep it", "Name what the batch made and keep it: it joins the kitchen as a recipe of your people's own. Only a mix they have tasted can be kept.");
        _try = CodeUI.TextButton(book, "Try a batch", TryBatch, _theme, _theme.subtitleSize + 8f);
        _try.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(_try.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-640f, 28f), new Vector2(-330f, 74f));
        TooltipTrigger.Ensure(_try.gameObject).SetCustom("Try a batch", "Cook (or brew) it once, using up what goes in. Only then do your people know what it makes, and how near it comes to the recipes the Flavor Log only whispers of.");
        var cancel = CodeUI.TextButton(book, "Cancel", Close, _theme, _theme.subtitleSize + 4f);
        CodeUI.Place(cancel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 28f), new Vector2(240f, 74f));
    }

    private TMP_InputField Field(Transform book, string label, string placeholderText, float top)
    {
        var caption = CodeUI.Label(book, label, label, _theme.subtitleSize + 3f, _theme.subtitleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, top - 26f), new Vector2(-40f, top));
        var root = CodeUI.Panel(book, label + " Field", new Vector2(0f, 1f), new Vector2(1f, 1f));
        CodeUI.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, top - 74f), new Vector2(700f, top - 30f));
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

    // ===== SHOW =====

    private void Show(KitchenMethod method)
    {
        if (_root == null) Build();
        _method = method;
        _inputs.Clear();
        _name.text = string.Empty;
        _error.text = string.Empty;
        _root.gameObject.SetActive(true);
        Refresh();
        _name.Select();
        _name.ActivateInputField();
    }

    private bool Cellar => _method != KitchenMethod.Cook;

    private void SetMethod(KitchenMethod method)
    {
        _method = method;
        // What the cellar cannot use (a bloom, a tea) is dropped when switching to it.
        var allowed = new HashSet<string>(CultureSystem.Instance.IngredientChoices(method).Select(k => k.resource), StringComparer.OrdinalIgnoreCase);
        _inputs.RemoveAll(i => !allowed.Contains(i.resource));
        Refresh();
    }

    private void Refresh()
    {
        var culture = CultureSystem.Instance;
        if (culture == null) { Close(); return; }
        string noun = CultureInvention.Noun(_method);
        _title.text = $"A {noun} of our own";
        _intro.text = KeywordMarkup.SafeGlyphs(Cellar
            ? "Choose what goes into the crock or the still, try a batch, and keep it if it is worth a name. No one knows what a mix makes until it has been tried. Never an Eleos Bloom or a tea: those are steeped, not brewed."
            : "Choose what goes into the pot, try a batch, and keep it if it is worth a name. No one knows what a mix makes until it has been tried, and some recipes were never written down at all.");
        _brew.gameObject.SetActive(Cellar);
        _distill.gameObject.SetActive(Cellar);
        _brew.text = _method == KitchenMethod.Ferment ? "<u>Brew</u>" : "Brew";
        _distill.text = _method == KitchenMethod.Distill ? "<u>Distill</u>" : "Distill";
        ((TextMeshProUGUI)_name.placeholder).text = Cellar ? "Hearthfire Mead" : "Hearthside Stew";

        for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
        var units = GameUnitsLogic.Instance;
        foreach (var kind in culture.IngredientChoices(_method))
        {
            var k = kind;
            float amount = _inputs.FirstOrDefault(x => string.Equals(x.resource, k.resource, StringComparison.OrdinalIgnoreCase))?.amount ?? 0f;
            string label = amount > 0f ? $"<u>{k.resource} x{amount:0}</u>" : k.resource;
            var button = CodeUI.TextButton(_grid, label, () => Add(k.resource), _theme, _theme.bodySize - 1f);
            button.textWrappingMode = TextWrappingModes.NoWrap;
            button.overflowMode = TextOverflowModes.Ellipsis;
            button.color = amount > 0f ? _theme.titleColor : _theme.subtitleColor;
            float held = units != null ? units.GetResourceAmountExact(k.resource) : 0f;
            string tip = TooltipText.Row("Kitchen", CultureRules.ClassName(k.cuisine)) + "\n"
                + TooltipText.Row("Food value", CultureRules.Feeds(k.cuisine) ? $"{k.foodValue:0.##}" : "none") + "\n"
                + TooltipText.Row("Held", $"{held:0.#}")
                + (culture.IsInvented(k.resource) ? "\n" + TooltipText.Muted("One of your people's own.") : string.Empty);
            TooltipTrigger.Ensure(button.gameObject).SetCustom(k.resource, tip);
        }
        RefreshPreview();
    }

    private void Add(string resource)
    {
        var entry = _inputs.FirstOrDefault(x => string.Equals(x.resource, resource, StringComparison.OrdinalIgnoreCase));
        if (entry == null) _inputs.Add(new ResourceAmount { resource = resource, amount = 1f });
        else if (entry.amount >= MaxOfOne) _inputs.Remove(entry);
        else entry.amount += 1f;
        Refresh();
    }

    private void RefreshPreview()
    {
        var culture = CultureSystem.Instance;
        if (culture == null) return;
        var trial = _inputs.Count > 0 ? culture.TrialOf(_method, _inputs) : null;
        // Untried: the batch is what can be done. Tasted: it can be named and kept.
        string whyTry = culture.WhyNotTry(_method, _inputs);
        string whyKeep = trial == null ? "Try a batch first: no one names a dish they have never tasted." : culture.WhyNotInvent(_name.text, _method, _inputs);
        string why = trial == null ? whyTry : whyKeep;
        _error.text = why != null ? KeywordMarkup.SafeGlyphs(why) : string.Empty;
        Enable(_confirm, whyKeep == null);
        Enable(_try, whyTry == null);
        _try.text = trial == null ? "Try a batch" : "Try it again";
        _preview.text = KeywordMarkup.SafeGlyphs(trial != null ? Tasted(culture, trial) : Untried(culture) + Whispers(culture));
    }

    private void Enable(TextMeshProUGUI label, bool on)
    {
        label.color = on ? _theme.titleColor : _theme.subtitleColor;
        var b = label.GetComponent<Button>();
        if (b != null) b.interactable = on;
    }

    // What the cooks can say of a mix no one has tried: what it is like, what a batch costs, and nothing more.
    private string Untried(CultureSystem culture)
    {
        string noun = CultureInvention.Noun(_method);
        if (_inputs.Count == 0) return TooltipText.Muted($"Nothing in it yet. What goes in decides what the {noun} is like and what it does.") + "\n";
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading("Untried", TooltipText.Muted("no one knows yet what it makes")));
        string like = culture.ExpectedLike(_method, _inputs);
        text.AppendLine(TooltipText.Row("Something like", like ?? TooltipText.Warn("nothing your people know")));
        text.AppendLine(TooltipText.Row("A batch uses up", string.Join(" + ", _inputs.Select(i => $"{i.amount:0.#} {i.resource}"))));
        text.AppendLine(TooltipText.Row("Batches left", $"{culture.TriesLeft} this Seventh"));
        text.AppendLine(TooltipText.Muted($"How much it feeds, how it keeps, what it leans your people toward: all of it is learned by tasting. Every batch tried is remembered."));
        return text.ToString();
    }

    // What a tasted mix turned out to be, how near it came to a whispered recipe, and the whispers themselves.
    private string Tasted(CultureSystem culture, KitchenTrial trial)
    {
        var text = new StringBuilder();
        var found = trial.found != null ? culture.HiddenRecipes.FirstOrDefault(h => h?.formula?.id == trial.found) : null;
        var shown = culture.PreviewInvention(_name.text, _method, _inputs) ?? trial.result;
        text.Append(Preview(shown, trial.seventh));
        text.AppendLine(found != null ? TooltipText.Good($"A lost recipe: {found.formula.name}. Keep it to make it your people's own.")
            : TooltipText.Value(KitchenTrials.WarmthWords((Warmth)trial.warmth)));
        text.Append(Whispers(culture));
        return text.ToString();
    }

    // The Flavor Log's whispered recipes: the hints each has brought to light, and the ones found.
    private string Whispers(CultureSystem culture)
    {
        var whispers = culture.Whispers().Where(w => w.spec.Method == KitchenMethod.Cook ? !Cellar : Cellar).ToList();
        if (whispers.Count == 0) return string.Empty;
        var text = new StringBuilder("\n");
        text.AppendLine(TooltipText.Heading("Whispered recipes", TooltipText.Muted("never written down")));
        foreach (var w in whispers)
        {
            if (w.Found) { text.AppendLine(TooltipText.Bullet(TooltipText.Good($"{w.spec.formula.name}: found"))); continue; }
            text.AppendLine(TooltipText.Bullet($"<i>{string.Join(" ", w.hints)}</i>"));
        }
        return text.ToString();
    }

    private string Preview(InventedRecipe r, int seventh)
    {
        string noun = CultureInvention.Noun(_method);
        if (r == null) return TooltipText.Muted($"Nothing in it yet. What goes in decides what the {noun} is like and what it does.");
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading(r.name, TooltipText.Muted($"tasted in Seventh {seventh + 1}")));
        text.AppendLine(r.templateDish != null
            ? TooltipText.Row("Made the way of", $"{r.templateDish} ({r.closeness:P0} the same ingredients)")
            : TooltipText.Row("Made the way of", TooltipText.Warn("nothing your people know")));
        string inputs = string.Join(" + ", r.inputs.Select(i => $"{i.amount:0.#} {i.resource}"));
        text.AppendLine(TooltipText.Row("A batch", $"{inputs} makes {r.output:0.#}"));
        text.AppendLine(TooltipText.Row("Is", CultureRules.ClassName(r.Cuisine) + (r.Cuisine == FoodClass.Beverage ? " (drunk for joy; opened for hunger last)" : string.Empty)));
        float fedIn = r.inputs.Sum(i => i.amount * CultureSystem.FoodValueOf(i.resource));
        text.AppendLine(TooltipText.Row("Food value", $"{r.foodValue:0.##} each ({fedIn:0.#} in, {r.foodValue * r.output:0.#} out a batch)"));
        text.AppendLine(TooltipText.Row("Keeps", r.spoilPerSeventh <= 0f ? "never spoils" : $"{r.spoilPerSeventh:P1} spoils each Seventh"));
        if (r.leanings.Count > 0) text.AppendLine(TooltipText.Row("Leans the culture", string.Join(", ", r.leanings.Select(l => $"{l.family} {l.share:P0}"))));
        if (r.luxuries.Count > 0) text.AppendLine(TooltipText.Row("Luxury", string.Join(", ", r.luxuries)));
        if (r.faithPerUnit > 0f) text.AppendLine(TooltipText.Row("Faith", $"+{r.faithPerUnit:0.##} a unit enjoyed"));
        if (r.unity > 0f) text.AppendLine(TooltipText.Row("Memorial", $"+{r.unity:0.##} Unity a batch"));
        if (r.peach) text.AppendLine(TooltipText.Row("Peach", TooltipText.Warn("mostly peach: it counts toward the monocrop")));
        text.AppendLine(TooltipText.Muted($"Kept and named, it becomes your people's own {noun}: it grows familiar sooner and is the first offered as national."));
        return text.ToString();
    }

    private void TryBatch()
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !IsOpen) return;
        var trial = culture.TryBatch(_method, _inputs);
        if (trial == null) { _error.text = KeywordMarkup.SafeGlyphs(culture.WhyNotTry(_method, _inputs) ?? "It could not be tried."); return; }
        // A lost recipe found offers its old name (the player may still give it their own).
        var found = trial.found != null ? culture.HiddenRecipes.FirstOrDefault(h => h?.formula?.id == trial.found) : null;
        if (found != null && string.IsNullOrWhiteSpace(_name.text)) _name.text = found.formula.name;
        Refresh();
    }

    private void Confirm()
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !IsOpen) return;
        if (!culture.HasTasted(_method, _inputs)) { _error.text = "Try a batch first: no one names a dish they have never tasted."; return; }
        var made = culture.Invent(_name.text, _method, _inputs);
        if (made != null) Close();
        else _error.text = KeywordMarkup.SafeGlyphs(culture.WhyNotInvent(_name.text, _method, _inputs) ?? "It could not be made.");
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
        if (IsOpen) Close();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
