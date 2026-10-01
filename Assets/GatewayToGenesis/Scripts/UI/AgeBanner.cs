using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Age on the HUD, over the capital and the world alike: which Age and Act, how far through the Age the world is,
/// and the soft signs of what is coming. The bar is measured by the technology tree, as the Age itself is: each Act is
/// an equal part of it and fills with the technologies researched on the path to the one that opens the next Act (an
/// Act no technology ends yet fills with time).
/// Before a crisis is declared the banner only shows the harvest's colour (the vault: an Age Crisis "never announces
/// itself"); once declared it names the crisis, its stage and its severity, and its tooltip breaks the severity down.
/// When an Age passes, a card tells how many survived and which Age begins (time waits until it is closed).
/// News goes to the notices on the right (<see cref="NotificationFeed"/>). Built in code; no scene setup.
/// </summary>
public class AgeBanner : MonoBehaviour
{
    private const float Width = 780f, Height = 92f, Margin = 6f, RefreshSeconds = 0.25f;

    /// <summary>The banner's width, centred at the top of the screen (the time indicator sits beside it).</summary>
    public const float BannerWidth = Width;

    private static AgeBanner _instance;

    /// <summary>Screen height the banner holds at the top (0 before it is built), so toasts can sit below it.</summary>
    public static float ReservedHeight => _instance != null && _instance._built ? Height + Margin : 0f;

    private static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color BarFill = new Color32(0xE6, 0xBC, 0x93, 0xFF);
    private static readonly Color BarCrisis = new Color32(0xD9, 0x6A, 0x5A, 0xFF);

    private TooltipTheme _theme;
    private CanvasScaler _scaler;
    private CanvasGroup _group, _cardGroup;
    private TextMeshProUGUI _title, _act, _status, _cardText, _mapButton;
    private Image _fill;
    private RectTransform _ticks;
    private bool _built;
    private float _refreshAt;
    private readonly Queue<AgeRecord> _cards = new Queue<AgeRecord>();
    private bool _cardOpen, _pausedForCard;

    private void Start()
    {
        _theme = CodeUI.Theme(nameof(AgeBanner));
        if (_theme == null) return;
        _instance = this;
        Build();
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.AgePassed += OnAgePassed;
            ages.AgeBegan += OnAgeBegan;
        }
        DrawTicks();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.AgePassed -= OnAgePassed;
            ages.AgeBegan -= OnAgeBegan;
        }
    }

    private void OnAgeBegan(AgeDefinition _) => DrawTicks();

    // ===== BUILD =====

    private void Build()
    {
        var canvas = CodeUI.Canvas(transform, "Age Banner Canvas", 3, out _scaler);
        _group = canvas.gameObject.AddComponent<CanvasGroup>();

        var banner = CodeUI.Panel(canvas.transform, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        banner.pivot = new Vector2(0.5f, 1f);
        banner.sizeDelta = new Vector2(Width, Height);
        banner.anchoredPosition = new Vector2(0f, -Margin);
        CodeUI.Plate(banner, _theme, _scaler, 0.92f);
        TooltipTrigger.Ensure(banner.gameObject);
        banner.gameObject.AddComponent<AgeBannerTooltip>().banner = this;

        const float pad = 22f;
        _title = CodeUI.Label(banner, "Title", string.Empty, _theme.titleSize - 3f, _theme.titleColor, FontStyles.Normal, _theme);
        _title.alignment = TextAlignmentOptions.MidlineLeft;
        _title.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(pad, -40f), new Vector2(-170f, -10f));
        _act = CodeUI.Label(banner, "Act", string.Empty, _theme.subtitleSize + 3f, _theme.subtitleColor, FontStyles.SmallCaps, _theme);
        _act.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(_act.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -40f), new Vector2(-pad, -10f));

        // The Age as a bar: Act boundaries are ticks; it turns red once the crisis is named.
        var bar = CodeUI.Panel(banner, "Bar", new Vector2(0f, 1f), new Vector2(1f, 1f));
        CodeUI.Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(pad, -52f), new Vector2(-pad, -45f));
        CodeUI.Solid(bar, "Track", BarTrack);
        _fill = CodeUI.Solid(bar, "Fill", BarFill);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.sprite = WhitePixel();
        _ticks = CodeUI.Panel(bar, "Ticks", Vector2.zero, Vector2.one);

        _status = CodeUI.Label(banner, "Status", string.Empty, _theme.bodySize - 1f, _theme.bodyColor, FontStyles.Normal, _theme);
        _status.alignment = TextAlignmentOptions.MidlineLeft;
        _status.textWrappingMode = TextWrappingModes.NoWrap;
        _status.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(pad, 10f), new Vector2(-200f, 38f));
        var map = _mapButton = CodeUI.TextButton(banner, "The World (M)", WorldView.Toggle, _theme, _theme.subtitleSize + 3f);
        map.alignment = TextAlignmentOptions.MidlineRight;
        map.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(map.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 10f), new Vector2(-pad, 38f));

        BuildCard(canvas.transform);
        _built = true;
    }

    private void BuildCard(Transform canvas)
    {
        var host = CodeUI.Panel(canvas, "Age Passes", Vector2.zero, Vector2.one);
        _cardGroup = host.gameObject.AddComponent<CanvasGroup>();
        var dim = CodeUI.Solid(host, "Backdrop", new Color(0.02f, 0.01f, 0.03f, 0.78f), true);
        var card = CodeUI.Panel(host, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.sizeDelta = new Vector2(760f, 620f);
        CodeUI.Plate(card, _theme, _scaler);
        _cardText = CodeUI.Label(card, "Text", string.Empty, _theme.bodySize + 1f, _theme.bodyColor, FontStyles.Normal, _theme);
        _cardText.lineSpacing = TooltipText.LineSpacing;
        _cardText.alignment = TextAlignmentOptions.TopLeft;
        _cardText.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(_cardText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 84f), new Vector2(-44f, -40f));
        var go = CodeUI.TextButton(card, "Continue", CloseCard, _theme, _theme.subtitleSize + 8f);
        go.alignment = TextAlignmentOptions.Center;
        CodeUI.Place(go.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-140f, 26f), new Vector2(140f, 70f));
        SetCardVisible(false);
        dim.raycastTarget = true;
    }

    private static Sprite _white;

    private static Sprite WhitePixel()
    {
        if (_white != null) return _white;
        var texture = new Texture2D(2, 2) { filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        _white = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        return _white;
    }

    private void DrawTicks()
    {
        var ages = AgeProgression.Instance;
        if (_ticks == null || ages == null || ages.Current == null) return;
        for (int i = _ticks.childCount - 1; i >= 0; i--) Destroy(_ticks.GetChild(i).gameObject);
        // Each Act is an equal share of the bar (it is measured by technologies, not time).
        int acts = Mathf.Max(1, ages.Current.actSevenths.Count);
        for (int act = 1; act < acts; act++)
        {
            float x = act / (float)acts;
            var tick = CodeUI.Solid(_ticks, $"Act {act + 1}", _theme.titleColor);
            CodeUI.Place(tick.rectTransform, new Vector2(x, 0f), new Vector2(x, 1f), new Vector2(-1.5f, -3f), new Vector2(1.5f, 3f));
        }
    }

    // ===== FRAME =====

    private void Update()
    {
        if (!_built) return;
        var events = EventSystemLogic.Instance;
        bool hidden = (events != null && events.IsEventActive()) || LibraryWindow.IsOpen;
        _group.alpha = Mathf.MoveTowards(_group.alpha, hidden && !_cardOpen ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        _group.blocksRaycasts = !hidden || _cardOpen;

        if (!_cardOpen && _cards.Count > 0 && (events == null || !events.IsEventActive())) OpenCard(_cards.Dequeue());
        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + RefreshSeconds;
        Refresh();
    }

    private void Refresh()
    {
        var ages = AgeProgression.Instance;
        var age = ages != null ? ages.Current : null;
        if (age == null)
        {
            _title.text = "The Ages";
            _act.text = string.Empty;
            _status.text = TooltipText.Muted("No Age is authored (Resources/Ages).");
            return;
        }
        _title.text = KeywordMarkup.SafeGlyphs($"Ages {AgeRules.Roman(age.number)}{TooltipText.Separator}{age.title}");
        _act.text = age.ActLabel(ages.Act);
        _fill.fillAmount = ages.TreeProgress;
        _fill.color = ages.CrisisDeclared ? BarCrisis : BarFill;

        string status;
        if (ages.ChronicleRests) status = TooltipText.Muted("The chronicle rests here: the next Age is not written yet.");
        else if (ages.CrisisDeclared)
        {
            var stage = ages.CurrentStage;
            // Severity first: the stage name is what gets cut when the line runs long.
            status = TooltipText.Bad($"{age.crisisTitle} {Mathf.RoundToInt(ages.CurrentSeverity * 100f)}%") + (stage != null ? TooltipText.Separator + stage.name : string.Empty);
        }
        else status = $"Harvest: {HarvestWord(ages.Harvest)}";
        status += TooltipText.Separator + TooltipText.Paint($"Era Score {ages.EraScore}", TooltipText.LinkHex);
        if (!ages.ChronicleRests) status += TooltipText.Separator + NextTurning(ages);
        _status.text = KeywordMarkup.SafeGlyphs(status);
        var world = WorldSystem.Instance;
        _mapButton.text = world == null || world.MapUnlocked ? "The World (M)" : TooltipText.Muted("The World (unknown)");
    }

    // What turns the Age next: the technology that opens the next Act and how far along its path the tree is.
    private static string NextTurning(AgeProgression ages)
    {
        var path = ages.ActPath;
        ages.ActShare(out int done, out int total);
        if (path != null && total > 0)
        {
            string gate = path[path.Count - 1];
            return ages.WaitingGate != null ? TooltipText.Warn($"The Age waits for {gate} ({done}/{total})") : $"Next Act: {gate} {TooltipText.Muted($"({done}/{total})")}";
        }
        return TooltipText.Muted(ages.Act + 1 < ages.Current.actSevenths.Count ? "Next Act: with time" : "The Age draws on");
    }

    private static bool IsResearched(string technology) => GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.IsTechnologyUnlocked(technology);

    private static string HarvestWord(HarvestQuality harvest)
    {
        switch (harvest)
        {
            case HarvestQuality.Golden: return TooltipText.Paint("Golden", "#E6C36A");
            case HarvestQuality.Pink: return TooltipText.Paint("Pink", "#E59AB0");
            default: return TooltipText.Paint("Brown", "#A9744F");
        }
    }

    // ===== TOOLTIP =====

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data)
    {
        var ages = AgeProgression.Instance;
        var age = ages != null ? ages.Current : null;
        if (age == null) return false;
        data.title = age.title;
        data.type = $"Ages {AgeRules.Roman(age.number)}";
        data.description = age.epigraph;

        var summary = new StringBuilder();
        summary.AppendLine(TooltipText.Row(age.ActLabel(ages.Act), $"{Mathf.RoundToInt(ages.TreeProgress * 100f)}% of the Age"));
        var path = ages.ActPath;
        ages.ActShare(out int done, out int total);
        int nextAct = AgeRules.ActStart(ages.Act + 1, age.actSevenths);
        if (path != null && total > 0)
        {
            summary.AppendLine(TooltipText.Row("Next Act of Fate", $"when {path[path.Count - 1]} is researched"));
            summary.AppendLine(TooltipText.Row("On its path", $"{done} of {total} technologies researched"));
            var left = path.Where(t => !IsResearched(t)).ToList();
            if (left.Count > 0) summary.AppendLine(TooltipText.Row("Still to research", left.Count > 4 ? $"{string.Join(", ", left.Take(4))} and {left.Count - 4} more" : string.Join(", ", left)));
        }
        else if (ages.Act + 1 < age.actSevenths.Count) summary.AppendLine(TooltipText.Row("Next Act of Fate", $"{Until(nextAct - ages.Sevenths)} {TooltipText.Muted("(no technology opens it yet)")}"));
        else if (!ages.Ending) summary.AppendLine(TooltipText.Row("The Age ends", Until(age.Length - ages.Sevenths)));
        summary.Append(TooltipText.Row("Harvest", HarvestWord(ages.Harvest)));
        data.summary = summary.ToString();

        if (ages.CrisisDeclared)
        {
            var rows = new StringBuilder();
            rows.AppendLine(TooltipText.Heading(age.crisisTitle, $"{Mathf.RoundToInt(ages.CurrentSeverity * 100f)}%"));
            foreach (var factor in ages.CurrentFactors())
            {
                rows.AppendLine(TooltipText.Row(factor.label, TooltipText.Signed(Mathf.Round(factor.value * 100f), "%", higherIsBetter: false)));
            }
            var tuning = age.tuning;
            int population = AgeProgression.People;
            rows.Append(TooltipText.Row("Would be lost now", $"{CrisisRules.Deaths(population, ages.CurrentSeverity, tuning)} of {population}"));
            data.breakdown = rows.ToString();
            data.notes = "Research its technologies, store food, explore fertile land and choose carefully in its stories to lower the severity.";
        }
        else if (ages.CrisisBegun)
        {
            data.notes = "The peaches ripen pink.";
        }

        if (ages.History.Count > 0)
        {
            data.related = string.Join("\n", ages.History.Select(r => $"{r.ageTitle}: {r.Survivors} of {r.populationBefore} survived {r.crisisTitle}"));
        }
        return true;
    }

    private static string Until(int sevenths)
    {
        sevenths = Mathf.Max(0, sevenths);
        var time = TimeSystemLogic.Instance;
        if (time == null) return $"{sevenths} sevenths";
        float seconds = sevenths * time.GetEffectiveSecondsPerSeventh();
        return seconds >= 90f ? $"{sevenths} sevenths (~{Mathf.RoundToInt(seconds / 60f)} min)" : $"{sevenths} sevenths (~{Mathf.RoundToInt(seconds)} s)";
    }

    // ===== THE PASSAGE CARD =====

    private void OnAgePassed(AgeRecord record) => _cards.Enqueue(record);

    private void OpenCard(AgeRecord record)
    {
        var text = new StringBuilder();
        text.AppendLine($"<size=150%><color=#{ColorUtility.ToHtmlStringRGB(_theme.titleColor)}>{record.crisisTitle} has passed</color></size>");
        text.AppendLine(TooltipText.Muted($"The end of the {record.ageTitle}"));
        text.AppendLine();
        text.AppendLine(TooltipText.Row("Survivors", $"{record.Survivors} of {record.populationBefore}"));
        text.AppendLine(TooltipText.Row("Lost", TooltipText.Bad(record.deaths.ToString())));
        text.AppendLine(TooltipText.Row("Severity", $"{Mathf.RoundToInt(record.severity * 100f)}%"));
        foreach (var factor in record.factors) text.AppendLine(TooltipText.Bullet($"{factor.label}: {TooltipText.Signed(Mathf.Round(factor.value * 100f), "%", higherIsBetter: false)}"));
        text.AppendLine();

        var next = AgeProgression.Instance != null ? AgeProgression.Instance.Current : null;
        if (record.nextAgeId != null && next != null)
        {
            text.AppendLine($"<size=125%><color=#{ColorUtility.ToHtmlStringRGB(_theme.titleColor)}>The world enters the {next.title}</color></size>");
            if (!string.IsNullOrEmpty(next.epigraph)) text.AppendLine(TooltipText.Quote($"<i>{next.epigraph}</i>"));
            text.AppendLine();
            if (WorldSystem.Instance != null && !string.IsNullOrEmpty(WorldSystem.Instance.LastNotice)) text.AppendLine(WorldSystem.Instance.LastNotice);
        }
        else text.AppendLine(TooltipText.Muted("No further Age is written yet: the chronicle rests here."));

        _cardText.text = KeywordMarkup.SafeGlyphs(text.ToString());
        SetCardVisible(true);
        var time = TimeSystemLogic.Instance;
        _pausedForCard = time != null && !time.isTimePaused;
        if (_pausedForCard) time.PauseTime(true);
    }

    private void CloseCard()
    {
        SetCardVisible(false);
        var time = TimeSystemLogic.Instance;
        if (_pausedForCard && time != null) time.PauseTime(false);
        _pausedForCard = false;
    }

    private void SetCardVisible(bool visible)
    {
        _cardOpen = visible;
        _cardGroup.alpha = visible ? 1f : 0f;
        _cardGroup.blocksRaycasts = visible;
        _cardGroup.interactable = visible;
    }
}

/// <summary>
/// The banner's tooltip source, on the banner itself: the Genesis Loop object above it carries other views, so the
/// banner answers here rather than as a source higher up.
/// </summary>
public class AgeBannerTooltip : MonoBehaviour, ITooltipSource
{
    public AgeBanner banner;

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => banner != null && banner.BuildTooltip(trigger, data);
}
