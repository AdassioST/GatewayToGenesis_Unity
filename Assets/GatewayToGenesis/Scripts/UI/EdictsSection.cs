using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Government tab's Edicts section (<see cref="EdictSystem"/>): the realm's stances (one option in force each, a
/// click to change it), its edicts (seal or revoke, slots used of the council's), what each seated legend thinks of
/// the laws in force (the council's accord) and the record of decrees. Before the council holds enough seats it says
/// how many more it needs.
///
/// Built in code (<see cref="CodeUI"/>) by <see cref="GovernmentTab"/> inside its Display, so it shows and hides with
/// the tab: an "Edicts" button on the tab opens it over the council, "Council" (or Esc) returns. A view, never a rule:
/// every change goes through <see cref="EdictSystem"/>, whose Changed event marks it for one redraw at the end of the frame.
/// </summary>
public class EdictsSection : MonoBehaviour
{
    private static EdictsSection _instance;

    private TooltipTheme _theme;
    private RectTransform _panel;
    private TextMeshProUGUI _toggle, _title, _subtitle, _intro, _accord, _record;
    private RectTransform _stancesHeader, _edictsHeader;
    private ScrollRect _scroll;
    private EdictSystem _edicts;
    private bool _dirty = true;

    private class OptionView { public StanceOption option; public TextMeshProUGUI label; public Button button; }
    private class StanceView { public StanceDefinition stance; public TextMeshProUGUI heading; public List<OptionView> options = new List<OptionView>(); public GameObject root; }
    private class EdictView { public EdictDefinition edict; public TextMeshProUGUI label, action; public Button button; public GameObject root; }
    private readonly List<StanceView> _stances = new List<StanceView>();
    private readonly List<EdictView> _edictRows = new List<EdictView>();

    public static bool IsOpen => _instance != null && _instance._panel != null && _instance._panel.gameObject.activeSelf;

    /// <summary>Open the Government tab at its Edicts section (from a notice or a story).</summary>
    public static void Open()
    {
        if (WorldView.IsOpen) WorldView.Toggle();
        TabHotkeys.Instance?.OpenGovernmentTab();
        if (_instance != null) _instance.Show(true);
    }

    /// <summary>Put the section inside the tab's Display (<paramref name="display"/>).</summary>
    public static EdictsSection Attach(RectTransform display)
    {
        if (display == null) return null;
        var section = display.gameObject.GetComponent<EdictsSection>();
        if (section == null) section = display.gameObject.AddComponent<EdictsSection>();
        section.Build(display);
        return section;
    }

    // ===== BUILD =====

    private void Build(RectTransform display)
    {
        if (_panel != null) return;
        _instance = this;
        _theme = CodeUI.Theme(nameof(EdictsSection));
        var scaler = display.GetComponentInParent<CanvasScaler>();

        _toggle = CodeUI.TextButton(display, "Edicts", () => Show(!IsOpen), _theme, _theme.subtitleSize + 8f);
        _toggle.name = "Edicts Button";
        _toggle.alignment = TextAlignmentOptions.MidlineRight;
        _toggle.textWrappingMode = TextWrappingModes.NoWrap;
        _toggle.color = _theme.titleColor;
        CodeUI.Place(_toggle.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-330f, -64f), new Vector2(-24f, -14f));
        TooltipTrigger.Ensure(_toggle.gameObject).SetCustom("Edicts", "The realm's laws and decrees: its stances (the roofless, strangers, growth, the borders and the Pillars) and the edicts its Head of State seals. Established once the council holds three seats, counting the Head of State.");

        _panel = CodeUI.Panel(display, "Edicts", Vector2.zero, Vector2.one);
        _panel.offsetMin = new Vector2(16f, 16f);
        _panel.offsetMax = new Vector2(-16f, -72f);
        // An opaque backing: the council under the section must not show through the plate.
        CodeUI.Solid(_panel, "Backing", new Color(0.09f, 0.08f, 0.07f, 1f), true);
        if (scaler != null) CodeUI.Plate(_panel, _theme, scaler);
        else _panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.97f);

        _title = CodeUI.Label(_panel, "Title", "Edicts", _theme.titleSize + 8f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        _title.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -76f), new Vector2(-200f, -20f));
        _subtitle = CodeUI.Label(_panel, "Subtitle", string.Empty, _theme.subtitleSize + 3f, _theme.subtitleColor, FontStyles.Normal, _theme);
        CodeUI.Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -110f), new Vector2(-32f, -76f));
        var back = CodeUI.TextButton(_panel, "Council", () => Show(false), _theme);
        back.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -70f), new Vector2(-32f, -24f));
        TooltipTrigger.Ensure(back.gameObject).SetCustom("Council", "Back to the council and the civics.");

        var content = BuildScroll(_panel);
        _intro = Text(content, "Intro", _theme.bodySize);
        _stancesHeader = Text(content, "Stances Header", _theme.subtitleSize + 6f).rectTransform;
        _stancesHeader.GetComponent<TextMeshProUGUI>().text = TooltipText.Heading("Stances");
        foreach (var stance in EdictCatalog.Stances) _stances.Add(BuildStance(content, stance));
        _edictsHeader = Text(content, "Edicts Header", _theme.subtitleSize + 6f).rectTransform;
        foreach (var edict in EdictCatalog.Edicts) _edictRows.Add(BuildEdict(content, edict));
        _accord = Text(content, "Accord", _theme.bodySize);
        _record = Text(content, "Record", _theme.bodySize - 1f);

        _panel.gameObject.SetActive(false);
    }

    private RectTransform BuildScroll(RectTransform parent)
    {
        var area = CodeUI.Panel(parent, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(28f, 24f);
        area.offsetMax = new Vector2(-24f, -118f);
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 16);
        layout.spacing = 4f;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bar = CodeUI.Panel(area, "Scrollbar", new Vector2(1f, 0f), Vector2.one);
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
        return content;
    }

    private TextMeshProUGUI Text(Transform parent, string name, float size)
    {
        var label = CodeUI.Label(parent, name, string.Empty, size, _theme.bodyColor, FontStyles.Normal, _theme);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.lineSpacing = TooltipText.LineSpacing;
        return label;
    }

    private StanceView BuildStance(Transform parent, StanceDefinition stance)
    {
        var view = new StanceView { stance = stance };
        var root = CodeUI.Panel(parent, stance.title, Vector2.zero, Vector2.one);
        view.root = root.gameObject;
        var column = root.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childControlHeight = column.childControlWidth = true;
        column.childForceExpandHeight = false;
        column.padding = new RectOffset(0, 0, 6, 4);
        view.heading = Text(root, "Heading", _theme.bodySize);
        var row = CodeUI.Panel(root, "Options", Vector2.zero, Vector2.one);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 28f;
        layout.padding = new RectOffset(18, 0, 0, 0);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        foreach (var option in stance.options)
        {
            var o = option;
            var label = CodeUI.TextButton(row, option.title, () => Choose(stance, o), _theme, _theme.bodySize + 1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            view.options.Add(new OptionView { option = option, label = label, button = label.GetComponent<Button>() });
        }
        return view;
    }

    private EdictView BuildEdict(Transform parent, EdictDefinition edict)
    {
        var view = new EdictView { edict = edict };
        var row = CodeUI.Panel(parent, edict.title, Vector2.zero, Vector2.one);
        view.root = row.gameObject;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.padding = new RectOffset(0, 8, 4, 4);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        view.label = Text(row, "Label", _theme.bodySize);
        view.label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        view.label.raycastTarget = true;
        view.action = CodeUI.TextButton(row, "Seal", () => Act(edict), _theme, _theme.bodySize + 2f);
        view.action.textWrappingMode = TextWrappingModes.NoWrap;
        view.action.alignment = TextAlignmentOptions.MidlineRight;
        view.action.gameObject.AddComponent<LayoutElement>().minWidth = 150f;
        view.button = view.action.GetComponent<Button>();
        return view;
    }

    // ===== OPEN / CLOSE =====

    private void Show(bool open)
    {
        if (_panel == null) return;
        _panel.gameObject.SetActive(open);
        if (open)
        {
            _dirty = true;
            _scroll.verticalNormalizedPosition = 1f;
        }
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;
    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Show(false);
    }

    private void OnDestroy()
    {
        if (_edicts != null) _edicts.Changed -= MarkDirty;
        if (_instance == this) _instance = null;
    }

    private void MarkDirty() => _dirty = true;

    private void LateUpdate()
    {
        if (_edicts == null && EdictSystem.Instance != null)
        {
            _edicts = EdictSystem.Instance;
            _edicts.Changed += MarkDirty;
            _dirty = true;
        }
        // The toggle always says where the edicts stand; the panel redraws only while it is open.
        if (_dirty || Time.frameCount % 30 == 0) DrawToggle();
        if (_dirty && IsOpen) Draw();
    }

    // ===== ACTIONS =====

    private void Choose(StanceDefinition stance, StanceOption option)
    {
        if (_edicts == null || _edicts.InForce(stance) == option) return;
        _edicts.SetStance(stance.id, option.id, out _);
        _dirty = true;
    }

    private void Act(EdictDefinition edict)
    {
        if (_edicts == null) return;
        if (_edicts.Active(edict.id) != null) _edicts.Revoke(edict.id);
        else _edicts.Seal(edict.id, out _);
        _dirty = true;
    }

    // ===== DRAW =====

    private void DrawToggle()
    {
        if (_toggle == null) return;
        var edicts = _edicts;
        _toggle.text = edicts == null ? "Edicts" : EdictSystem.IsEstablished ? $"Edicts {edicts.State.active.Count}/{edicts.Capacity}" : $"Edicts {TooltipText.Muted($"({edicts.Seats}/{edicts.Tuning.minCouncilSeats} seats)")}";
    }

    private void Draw()
    {
        _dirty = false;
        var edicts = _edicts;
        bool established = EdictSystem.IsEstablished;
        foreach (var view in _stances) view.root.SetActive(established);
        foreach (var view in _edictRows) view.root.SetActive(established);
        _stancesHeader.gameObject.SetActive(established);
        _edictsHeader.gameObject.SetActive(established);
        _accord.gameObject.SetActive(established);
        _record.gameObject.SetActive(established && edicts != null && edicts.Record.Count > 0);

        if (edicts == null)
        {
            _subtitle.text = string.Empty;
            _intro.text = "There is no government in this world to issue edicts.";
            return;
        }
        if (!established)
        {
            _subtitle.text = KeywordMarkup.SafeGlyphs($"Council seats: {edicts.Seats} of {edicts.Tuning.minCouncilSeats}");
            _intro.text = KeywordMarkup.SafeGlyphs(Unestablished(edicts));
            return;
        }

        float accord = edicts.Accord();
        var mood = EdictRules.MoodOf(accord, edicts.Tuning);
        _subtitle.text = KeywordMarkup.SafeGlyphs($"Edict slots {edicts.State.active.Count}/{edicts.Capacity}     Council accord {accord:+0;-0;0} ({EdictRules.MoodName(mood)})     Sealed by {edicts.HeadOfState ?? "no one: the Head of State's seat is empty"}");
        _intro.text = KeywordMarkup.SafeGlyphs(TooltipText.Muted("Stances are the realm's standing laws: one option is always in force, and a changed stance stands a Phase before it can change again. Each lean pulls the political compass toward its pillar, so the laws you keep shape your government. Edicts are decrees your Head of State seals for a cost; each fills one slot while it lasts."));
        foreach (var view in _stances) DrawStance(edicts, view);
        _edictsHeader.GetComponent<TextMeshProUGUI>().text = KeywordMarkup.SafeGlyphs(TooltipText.Heading("Edicts", $"{edicts.State.active.Count} of {edicts.Capacity} slots"));
        foreach (var view in _edictRows) DrawEdict(edicts, view);
        _accord.text = KeywordMarkup.SafeGlyphs(DescribeAccord(edicts, accord, mood));
        _record.text = KeywordMarkup.SafeGlyphs(DescribeRecord(edicts));
    }

    private void DrawStance(EdictSystem edicts, StanceView view)
    {
        var inForce = edicts.InForce(view.stance);
        int wait = edicts.StanceCooldown(view.stance.id);
        view.heading.text = KeywordMarkup.SafeGlyphs($"{TooltipText.Value(view.stance.title)}  {TooltipText.Muted(view.stance.question)}{(wait > 0 ? "  " + TooltipText.Warn($"stands {wait} more Seventh{(wait == 1 ? "" : "s")}") : string.Empty)}");
        foreach (var o in view.options)
        {
            bool chosen = o.option == inForce;
            string why = chosen ? null : edicts.WhyNotChange(view.stance.id, o.option.id);
            // The law in force stays clickable (a click on it does nothing) so the disabled tint never dims it.
            o.label.text = KeywordMarkup.SafeGlyphs(chosen ? TooltipText.Good($"> {o.option.title}") : o.option.title);
            o.label.color = why == null || chosen ? _theme.subtitleColor : _theme.subtitleColor * new Color(1f, 1f, 1f, 0.55f);
            o.button.interactable = chosen || why == null;
            TooltipTrigger.Ensure(o.label.gameObject).SetCustom(o.option.title, KeywordMarkup.SafeGlyphs(DescribeOption(edicts, o.option, chosen, why)));
        }
    }

    private void DrawEdict(EdictSystem edicts, EdictView view)
    {
        var edict = view.edict;
        var active = edicts.Active(edict.id);
        int rest = edicts.EdictCooldown(edict.id);
        string why = active == null ? edicts.WhyNotSeal(edict.id) : null;
        string status = active != null ? TooltipText.Good($"in force, {active.remaining} Seventh{(active.remaining == 1 ? "" : "s")} left")
            : rest > 0 ? TooltipText.Muted($"rests {rest} more Seventh{(rest == 1 ? "" : "s")}")
            : TooltipText.Muted(Cost(edict) + $", {edict.duration} Sevenths");
        view.label.text = KeywordMarkup.SafeGlyphs($"{TooltipText.Value(edict.title)}{(edict.dissonant ? TooltipText.Bad(" (Dissonant)") : string.Empty)}  {status}\n{TooltipText.Muted(string.Join(", ", EffectsOf(edict, active)))}");
        view.action.text = active != null ? "Revoke" : "Seal";
        view.button.interactable = active != null || why == null;
        view.action.color = active != null || why == null ? _theme.titleColor : _theme.subtitleColor * new Color(1f, 1f, 1f, 0.55f);
        TooltipTrigger.Ensure(view.label.gameObject).SetCustom(edict.title, KeywordMarkup.SafeGlyphs(DescribeEdict(edicts, edict, active, why)));
        TooltipTrigger.Ensure(view.action.gameObject).SetCustom(active != null ? "Revoke" : "Seal", KeywordMarkup.SafeGlyphs(active != null ? $"End {edict.title} now. It rests {edict.cooldown} Sevenths as if it had run its course." : why != null ? TooltipText.Bad(why) : $"Seal {edict.title}: {Cost(edict)}, in force for {edict.duration} Sevenths."));
    }

    // ===== TEXT =====

    private static string Cost(EdictDefinition edict) => edict.cost > 0f && !string.IsNullOrEmpty(edict.costResource) ? $"{edict.cost:0} {edict.costResource}" : "no cost";

    private static IEnumerable<string> EffectsOf(EdictDefinition edict, ActiveEdict active) =>
        (active != null ? EdictRules.EdictEffects(active) : edict.effects).Select(Describe);

    // An effect in the player's words: derived stats by their names in the civilization stats table ("Saving Roll Chance").
    private static string Describe(GameEffect effect) =>
        effect.type == GameEffectType.DerivedStatBonus && !string.IsNullOrEmpty(effect.target) ? effect.Describe().Replace(effect.target, CivilizationProperties.Name(effect.target)) : effect.Describe();

    private static string Unestablished(EdictSystem edicts)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading("Not yet established"));
        int need = edicts.SeatsNeeded;
        text.AppendLine($"The government issues edicts once the council holds {edicts.Tuning.minCouncilSeats} seats, counting the Head of State: {need} more to open. The Rekindling and Edicts of Stone and Bone each open a seat.");
        text.AppendLine();
        text.AppendLine(TooltipText.Muted("Until then the realm keeps its customs: the roofless are let in once Efficient Rations allows it, strangers are received like any survivor, the people grow at nature's pace, and the borders follow the Realm's policy."));
        text.AppendLine();
        text.AppendLine(TooltipText.Heading("What the Edicts will decide"));
        foreach (var stance in EdictCatalog.Stances) text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(stance.title)}: {stance.question}"));
        return text.ToString().TrimEnd();
    }

    private static string DescribeOption(EdictSystem edicts, StanceOption option, bool chosen, string why)
    {
        var text = new StringBuilder();
        text.AppendLine(option.summary);
        var effects = option.effects.Select(Describe).ToList();
        var levers = Levers(option.levers);
        if (effects.Count + levers.Count > 0) text.AppendLine(TooltipText.Row("While in force", string.Join(", ", levers.Concat(effects))));
        if (!string.IsNullOrEmpty(option.lean))
        {
            int resonance = edicts.Resonance(option);
            text.AppendLine(TooltipText.Row("Leans", $"{Capitalize(option.lean)}{(resonance > 0 ? TooltipText.Good(" (resonant with your government)") : resonance < 0 ? TooltipText.Bad(" (against your government's pillar)") : string.Empty)}"));
        }
        if (option.favoredBy.Count > 0) text.AppendLine(TooltipText.Row("Favored by seats of", string.Join(", ", option.favoredBy.Select(Capitalize))));
        if (option.opposedBy.Count > 0) text.AppendLine(TooltipText.Row("Opposed by seats of", string.Join(", ", option.opposedBy.Select(Capitalize))));
        if (option.dissonant) text.AppendLine(TooltipText.Bad("Dissonant: strains the council's accord."));
        if (chosen) text.AppendLine(TooltipText.Good("The law in force."));
        else if (why != null) text.AppendLine(TooltipText.Bad(why));
        else text.AppendLine(TooltipText.Muted($"Click to decree it: it then stands {EdictRules.StanceCooldown(edicts.Tuning, edicts.Mood)} Sevenths."));
        return text.ToString().TrimEnd();
    }

    private static List<string> Levers(EdictLevers levers)
    {
        var list = new List<string>();
        if (levers.turnAwayRoofless) list.Add("only the housed are let in");
        if (Mathf.Abs(levers.births - 1f) > 0.001f) list.Add($"{Percent(levers.births)} births");
        if (Mathf.Abs(levers.caravans - 1f) > 0.001f) list.Add(levers.caravans <= 0f ? "no caravans are drawn" : $"{Percent(levers.caravans)} caravans");
        if (Mathf.Abs(levers.arrivalRations - 1f) > 0.001f) list.Add($"{Percent(levers.arrivalRations)} rations to let a survivor in");
        if (Mathf.Abs(levers.vagrantsHoused - 1f) > 0.001f) list.Add($"vagrants housed {levers.vagrantsHoused:0.##}x as fast");
        if (levers.borders.HasValue) list.Add($"border policy: {RealmReport.Name(levers.borders.Value)}");
        return list;
    }

    private static string Percent(float multiplier) => $"{(multiplier >= 1f ? "+" : "")}{(multiplier - 1f) * 100f:0}%";

    private static string DescribeEdict(EdictSystem edicts, EdictDefinition edict, ActiveEdict active, string why)
    {
        var text = new StringBuilder();
        text.AppendLine(edict.summary);
        text.AppendLine(TooltipText.Row("Effects", string.Join(", ", EffectsOf(edict, active))));
        text.AppendLine(TooltipText.Row("Cost", $"{Cost(edict)}, in force {edict.duration} Sevenths, then rests {edict.cooldown}"));
        var (legend, seat, strength) = edicts.Answering(edict);
        text.AppendLine(TooltipText.Row("Council area", Capitalize(edict.area)));
        text.AppendLine(legend != null ? TooltipText.Good($"{legend} ({seat}) would carry it out: {strength:0.##}x strength.") : TooltipText.Muted("No seat answers for this area: normal strength."));
        if (active != null) text.AppendLine(TooltipText.Row("Sealed by", $"{active.sealedBy ?? "the council"} at {active.strength:0.##}x"));
        if (edict.dissonant) text.AppendLine(TooltipText.Bad("Dissonant."));
        if (why != null) text.AppendLine(TooltipText.Bad(why));
        return text.ToString().TrimEnd();
    }

    private static string DescribeAccord(EdictSystem edicts, float accord, EdictRules.Mood mood)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading("The council's accord", $"{accord:+0;-0;0} {EdictRules.MoodName(mood)}"));
        var opinions = edicts.Opinions();
        if (opinions.Count == 0) text.AppendLine(TooltipText.Muted("No legend sits on the council besides the Head of State: only the government's compass weighs the laws."));
        foreach (var o in opinions)
        {
            string verdict = o.Net > 0 ? TooltipText.Good("approves") : o.Net < 0 ? TooltipText.Bad("objects") : TooltipText.Muted("is content");
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(o.legend)} ({o.seat}) {verdict}: {o.approves} law{(o.approves == 1 ? "" : "s")} favored, {o.opposes} opposed"));
        }
        // The culture's public memory (T09): each term with its reason, capped by the culture.
        (float points, IReadOnlyList<(string reason, float value)> terms) culture = CultureSystem.Instance != null ? CultureSystem.Instance.PublicAccord() : (0f, new List<(string, float)>());
        foreach (var term in culture.terms) text.AppendLine(TooltipText.Bullet($"{(term.value >= 0f ? TooltipText.Good($"{term.value:+0}") : TooltipText.Bad($"{term.value:0}"))} {term.reason} {TooltipText.Muted("(public memory)")}"));
        if (culture.terms.Count > 0) text.AppendLine(TooltipText.Muted($"The culture's part: {culture.points:+0;-0;0} (at most {CultureSystem.Instance.PublicMemoryTuning.accordCap:0} either way; Culture window, Accounts)."));
        var effects = EdictRules.MoodEffects(mood, edicts.Tuning).Select(Describe).ToList();
        if (effects.Count > 0) text.AppendLine(TooltipText.Row(EdictRules.MoodName(mood), string.Join(", ", effects) + (mood == EdictRules.Mood.Discord ? ", stances stand twice as long" : string.Empty)));
        text.AppendLine(TooltipText.Muted($"Each seated legend weighs the laws in force by their seat's areas, and the government weighs each leaning law against its compass (Dissonant laws strain it). At {edicts.Tuning.harmonyAt:+0} or more the council is in harmony; at {edicts.Tuning.discordAt:0} or less, in discord."));
        return text.ToString().TrimEnd();
    }

    private static string DescribeRecord(EdictSystem edicts)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading("Record of decrees"));
        foreach (var moment in edicts.Record.Reverse().Take(12))
            text.AppendLine(TooltipText.Bullet($"{TooltipText.Value(moment.title)} {TooltipText.Muted($"(Seventh {moment.seventh})")}: {moment.text}"));
        return text.ToString().TrimEnd();
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
