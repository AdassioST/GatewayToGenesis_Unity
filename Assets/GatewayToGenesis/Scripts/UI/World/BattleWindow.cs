using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The battle screen, before and after (<see cref="BattlePreview"/>, <see cref="BattleRecord"/>): your side on the left,
/// the enemy on the right, each with its strength and every modifier that made it (Civilization VI), the verdict or the
/// advisors' prediction in the middle with the balance of power and the predicted casualties (Total War), and after a
/// battle the losses as bars, the advantage as one number, and the moments that decided it (Crusader Kings III). Built in
/// code (<see cref="CodeUI"/>) over the world view. Opens by itself after a battle one of your units fought
/// (<see cref="AutoOpen"/>); Esc or Close shuts it.
/// </summary>
public class BattleWindow : MonoBehaviour
{
    private static BattleWindow _instance;
    private TooltipTheme _theme;
    private RectTransform _root, _bar, _book;
    private Image _barLeft, _barRight;
    private TextMeshProUGUI _title, _center, _left, _right, _bottom;
    private TextMeshProUGUI _detailsButton, _leftMeterLabel, _rightMeterLabel;
    private UnityEngine.UI.Image _leftMeter, _rightMeter;
    private string _leftFull, _rightFull, _bottomFull, _leftSummary, _rightSummary, _bottomSummary;
    private bool _details;

    /// <summary>Open the result screen by itself after each battle one of your units fought.</summary>
    public static bool AutoOpen = true;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    /// <summary>The frame it last closed on (so the Esc that closed it closes nothing else).</summary>
    public static int ClosedFrame { get; private set; } = -1;

    /// <summary>The pre-battle screen, your side on the left (<paramref name="yoursAttacking"/>: yours is the attacker).</summary>
    public static void ShowPreview(BattlePreview preview, bool yoursAttacking)
    {
        if (preview == null) return;
        Instance().Show(preview.title, ComposeCenter(preview, null, yoursAttacking), ComposeSide(preview, null, yoursAttacking),
            ComposeSide(preview, null, !yoursAttacking), PreviewFooter(), yoursAttacking ? preview.balance : 1f - preview.balance, null);
        Instance().Summarize(preview, null, yoursAttacking);
    }

    /// <summary>The result screen of a recorded battle, your side on the left.</summary>
    public static void ShowRecord(BattleRecord record)
    {
        if (record?.report == null || record.preview == null) return;
        bool left = record.yours ?? true;
        float share = left ? record.preview.balance : 1f - record.preview.balance;
        Instance().Show(record.preview.title, ComposeCenter(record.preview, record, left), ComposeSide(record.preview, record, left),
            ComposeSide(record.preview, record, !left), ComposeMoments(record), share, record.Outcome);
        Instance().Summarize(record.preview, record, left);
    }

    /// <summary>Hooks the screen to a world's battles (called by the world view once it is built).</summary>
    public static void Watch(WorldSystem world)
    {
        if (world == null) return;
        world.BattleRecorded -= OnBattle;
        world.BattleRecorded += OnBattle;
    }

    public static void Unwatch(WorldSystem world)
    {
        if (world != null) world.BattleRecorded -= OnBattle;
    }

    private static void OnBattle(BattleRecord record)
    {
        if (!AutoOpen || record?.yours == null || !WorldView.IsOpen) return;
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive) return;
        ShowRecord(record);
    }

    private static BattleWindow Instance()
    {
        if (_instance == null) _instance = new GameObject("Battle Window").AddComponent<BattleWindow>();
        if (_instance._root == null) _instance.Build();
        return _instance;
    }

    // ===== BUILDING =====

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(BattleWindow));
        var canvas = CodeUI.Canvas(transform, "Battle Canvas", 2, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        var backdrop = CodeUI.Solid(_root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), true);
        backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var book = CodeUI.Panel(_root, "Book", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        book.sizeDelta = new Vector2(1240f, 720f);
        _book = book;
        CodeUI.Plate(book, _theme, scaler);

        _title = CodeUI.Label(book, "Title", string.Empty, _theme.titleSize + 6f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        _title.alignment = TextAlignmentOptions.Center;
        CodeUI.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(180f, -78f), new Vector2(-180f, -24f));
        var close = CodeUI.TextButton(book, "Close", Close, _theme);
        close.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -74f), new Vector2(-36f, -28f));

        // The balance of power: your share of the two strengths, left, against theirs.
        _bar = CodeUI.Panel(book, "Balance", new Vector2(0f, 1f), new Vector2(1f, 1f));
        _bar.offsetMin = new Vector2(40f, -104f);
        _bar.offsetMax = new Vector2(-40f, -88f);
        _barLeft = CodeUI.Solid(_bar, "Yours", new Color(0.33f, 0.55f, 0.9f));
        _barRight = CodeUI.Solid(_bar, "Theirs", new Color(0.85f, 0.28f, 0.25f));

        _left = Column(book, "Left", new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(40f, 250f), new Vector2(-8f, -118f));
        _center = Column(book, "Center", new Vector2(0.34f, 0f), new Vector2(0.66f, 1f), new Vector2(8f, 170f), new Vector2(-8f, -118f));
        _center.alignment = TextAlignmentOptions.Top;
        _right = Column(book, "Right", new Vector2(0.66f, 0f), new Vector2(1f, 1f), new Vector2(8f, 250f), new Vector2(-40f, -118f));
        _bottom = Column(book, "Moments", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 74f), new Vector2(-40f, 160f));
        _leftMeter = BuildMeter(book, "Your integrity", 0f, 0.34f, out _leftMeterLabel);
        _rightMeter = BuildMeter(book, "Enemy integrity", 0.66f, 1f, out _rightMeterLabel);
        _detailsButton = CodeUI.TextButton(book, "Show breakdown", () => { _details = !_details; RenderDetails(); }, _theme);
        CodeUI.Place(_detailsButton.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 24f), new Vector2(-40f, 64f));
    }

    private TextMeshProUGUI Column(RectTransform book, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var area = CodeUI.Panel(book, name + " Scroll", min, max);
        CodeUI.Place(area, min, max, offsetMin, offsetMax);
        CodeUI.Solid(area, "Surface", new Color(0f, 0f, 0f, 0.12f), true);
        var scroll = area.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 35f;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var label = CodeUI.Label(viewport, name, string.Empty, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        CodeUI.Place(label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = label.rectTransform;
        return label;
    }

    private void Show(string title, string center, string left, string right, string bottom, float share, BattleOutcome? verdict)
    {
        _title.text = KeywordMarkup.SafeGlyphs(title ?? "Battle");
        _center.text = KeywordMarkup.SafeGlyphs(center);
        _left.text = KeywordMarkup.SafeGlyphs(left);
        _right.text = KeywordMarkup.SafeGlyphs(right);
        _bottom.text = KeywordMarkup.SafeGlyphs(bottom);
        _leftFull = _left.text; _rightFull = _right.text; _bottomFull = _bottom.text;
        share = Mathf.Clamp01(share);
        CodeUI.Place(_barLeft.rectTransform, Vector2.zero, new Vector2(share, 1f), Vector2.zero, Vector2.zero);
        CodeUI.Place(_barRight.rectTransform, new Vector2(share, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        if (verdict.HasValue) _title.color = Color.Lerp(_theme.titleColor, BattleVerdicts.Color(verdict.Value), 0.35f);
        else _title.color = _theme.titleColor;
        _root.gameObject.SetActive(true);
    }

    private UnityEngine.UI.Image BuildMeter(RectTransform book, string name, float min, float max, out TextMeshProUGUI label)
    {
        var area = CodeUI.Panel(book, name, new Vector2(min, 0f), new Vector2(max, 0f));
        area.offsetMin = new Vector2(40f, 190f); area.offsetMax = new Vector2(-40f, 238f);
        label = CodeUI.Label(area, "Label", name, _theme.bodySize - 2f, _theme.bodyColor, FontStyles.Normal, _theme);
        CodeUI.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 16f), Vector2.zero);
        var track = CodeUI.Solid(area, "Track", new Color(1f, 1f, 1f, 0.12f));
        CodeUI.Place(track.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 10f));
        return CodeUI.Solid(track.transform, "Fill", new Color(0.46f, 0.72f, 0.65f));
    }

    private void Summarize(BattlePreview preview, BattleRecord record, bool left)
    {
        _leftSummary = ComposeOverview(preview, record, left);
        _rightSummary = ComposeOverview(preview, record, !left);
        _bottomSummary = record == null ? "Hostile neighbours engage automatically. Review the balance before committing your party."
            : string.Join("\n", record.aftermath.Take(2));
        void Meter(UnityEngine.UI.Image fill, TextMeshProUGUI label, bool side)
        {
            float loss = record == null ? preview.Side(side).predictedLoss : (side ? record.report.attacker : record.report.defender).LossShare;
            float kept = Mathf.Clamp01(1f - loss);
            CodeUI.Place(fill.rectTransform, Vector2.zero, new Vector2(kept, 1f), Vector2.zero, Vector2.zero);
            fill.color = Color.Lerp(new Color(0.85f, 0.35f, 0.28f), new Color(0.46f, 0.72f, 0.65f), kept);
            label.text = $"{(record == null ? "Expected integrity" : "Integrity remaining")}  {kept:P0}";
        }
        Meter(_leftMeter, _leftMeterLabel, left); Meter(_rightMeter, _rightMeterLabel, !left);
        _details = false; RenderDetails();
    }

    private void RenderDetails()
    {
        _left.text = KeywordMarkup.SafeGlyphs(_details ? _leftFull : _leftSummary);
        _right.text = KeywordMarkup.SafeGlyphs(_details ? _rightFull : _rightSummary);
        _bottom.text = KeywordMarkup.SafeGlyphs(_details ? _bottomFull : _bottomSummary);
        _detailsButton.text = _details ? "Hide breakdown" : "Show breakdown";
        foreach (var scroll in _book.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
        { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
    }

    public static string ComposeOverview(BattlePreview preview, BattleRecord record, bool side)
    {
        var s = preview.Side(side);
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading(s.name, side ? "Attacking" : "Defending"));
        text.AppendLine($"\n<size=220%><b>{s.Strength}</b></size>\n{TooltipText.Muted("STRENGTH")}\n");
        text.AppendLine($"{s.sections} sections" + (s.individuals > 0 ? $"  |  {s.individuals} strong" : string.Empty));
        text.AppendLine($"{s.cards} cards  |  {s.beats} Beats");
        if (!string.IsNullOrEmpty(s.commander)) text.AppendLine($"\n{TooltipText.Muted("Commander")}\n{s.commander}");
        if (record?.report != null)
        {
            var result = side ? record.report.attacker : record.report.defender;
            text.AppendLine($"\n{result.dead:0} dead  |  {result.wounded:0} wounded");
            if (result.captured > 0) text.AppendLine(TooltipText.Warn($"{result.captured} sections captured"));
        }
        return text.ToString();
    }

    private void LateUpdate() { if (IsOpen) CodeUI.FitModal(_book); }

    private void Close()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _root.gameObject.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnEnable() => GameInput.CancelPressed += OnCancel;

    private void OnDisable() => GameInput.CancelPressed -= OnCancel;

    private void OnCancel()
    {
        if (IsOpen) Close();
    }

    // ===== THE TEXT (pure: tested without a scene) =====

    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // A label and its value on one line (the tooltip's right-aligned rows crowd a narrow column).
    private static string Pair(string label, string value) => $"{TooltipText.Muted(label + ":")} {value}";

    // The game font has no star: "2★ Great Vanguard" reads "2-star Great Vanguard".
    private static string Stars(string text) => string.IsNullOrEmpty(text) ? text : text.Replace("★", "-star");

    /// <summary>The middle: the verdict (or the prediction), what it means, the strengths face to face and the advantage.</summary>
    public static string ComposeCenter(BattlePreview p, BattleRecord record, bool left)
    {
        var text = new StringBuilder();
        var own = p.Side(left);
        var other = p.Side(!left);
        if (record?.report != null)
        {
            var verdict = record.report.OutcomeFor(left);
            bool mythical = (left ? record.report.attacker : record.report.defender).mythical;
            text.AppendLine($"<size=150%><b>{TooltipText.Paint(((mythical ? "Mythical " : "") + BattleVerdicts.Words(verdict)).ToUpperInvariant(), Hex(BattleVerdicts.Color(verdict)))}</b></size>");
            text.AppendLine(TooltipText.Muted(BattleVerdicts.Meaning(verdict)));
            if (record.report.Held) text.AppendLine(TooltipText.Muted($"No line gave way in {record.report.measures} measures: {record.report.defender.name} held the field."));
        }
        else
        {
            text.AppendLine(TooltipText.Muted("PREDICTED"));
            text.AppendLine($"<size=140%><b>{TooltipText.Paint(BattleVerdicts.Words(own.predicted).ToUpperInvariant(), Hex(BattleVerdicts.Color(own.predicted)))}</b></size>");
            text.AppendLine(p.Advice(left));
        }
        text.AppendLine();
        text.AppendLine($"<size=170%><b>{own.Strength}</b></size>  {TooltipText.Muted("vs")}  <size=170%><b>{other.Strength}</b></size>");
        int advantage = own.Strength - other.Strength;
        text.AppendLine(TooltipText.Judge($"Advantage {(advantage >= 0 ? "+" : "")}{advantage}", advantage >= 0) + TooltipText.Muted($"  ({SymphonyPower.Words(left ? p.balance : 1f - p.balance)})"));
        if (record?.report == null && p.forecast != null)
        {
            text.AppendLine();
            text.AppendLine(Pair("Chance to win", $"{own.winChance:P0}"));
            text.AppendLine(Pair("Forecast", BattleLossBurden.Forecast(own.winChance).ToString()));
            text.AppendLine(Pair("Predicted casualties", Casualty(own)));
            text.AppendLine(Pair("Theirs", Casualty(other)));
        }
        return text.ToString().TrimEnd();
    }

    private static string Casualty(PreviewSide s)
    {
        string word = s.Casualties;
        return s.predictedLoss >= 0.45f ? TooltipText.Bad(word) : s.predictedLoss >= 0.25f ? TooltipText.Warn(word) : TooltipText.Good(word);
    }

    /// <summary>One side's column: who, how many, its strength and every modifier; after a battle, what it lost.</summary>
    public static string ComposeSide(BattlePreview p, BattleRecord record, bool attacker)
    {
        var s = p.Side(attacker);
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading(s.name, attacker ? "Attacking" : "Defending"));
        if (!string.IsNullOrEmpty(s.commander)) text.AppendLine(TooltipText.Muted($"Commanded by {s.commander}, {Stars(s.commanderTitle)}"));
        text.AppendLine(Pair("Deployed", $"{s.sections} section{(s.sections == 1 ? "" : "s")}{(s.individuals > 0 ? $", {s.individuals} strong" : string.Empty)}"));
        if (s.cards > 0) text.AppendLine(Pair("Symphony", $"{s.cards} cards, {s.beats} Beats a measure"));
        text.AppendLine(Pair("Initial advance", s.stance.ToString()));
        foreach (var group in s.deployment.GroupBy(d => d.hex))
        {
            var hex = BattleHexLayout.At(group.Key);
            text.AppendLine(Pair($"{hex.Lane}, Rank {hex.Rank}" + (group.Count() > 1 ? " (stacked)" : ""), string.Join(", ", group.Select(d => d.name))));
        }
        foreach (var group in (s.scoreInputs?.facts ?? new List<BattleScoreFact>()).GroupBy(f => f.input))
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading(group.Key.ToString(), null));
            foreach (var fact in group) text.AppendLine(Pair(fact.label, fact.value));
        }
        text.AppendLine();
        text.AppendLine($"<size=130%><b>{s.Strength}</b></size> {TooltipText.Muted("strength")}");
        foreach (var f in s.factors)
        {
            string amount = $"{(f.amount >= 0f ? "+" : "")}{f.amount:0}";
            string line = $"{(f == s.factors[0] ? TooltipText.Value(amount) : TooltipText.Judge(amount, f.amount >= 0f))} {Stars(f.label)}{(string.IsNullOrEmpty(f.detail) ? string.Empty : TooltipText.Muted($" ({Stars(f.detail)})"))}";
            text.AppendLine(line);
        }
        var r = record?.report == null ? null : attacker ? record.report.attacker : record.report.defender;
        if (r != null)
        {
            text.AppendLine();
            text.AppendLine(TooltipText.Heading("Losses", $"{r.LossShare:P0}"));
            text.AppendLine(Meter(1f - r.LossShare, r.LossShare >= BattleVerdicts.PyrrhicLoss));
            text.AppendLine(Pair("Dead · wounded", $"{r.dead:0} · {r.wounded:0}"));
            var population = record.report.population.Where(f => f.attacker == attacker).ToList();
            if (population.Count > 0) text.AppendLine(Pair("Population", $"{population.Sum(f => f.dead)} dead · {population.Sum(f => f.wounded)} wounded · {population.Sum(f => f.recoverable)} recoverable · {population.Sum(f => f.missing)} missing · {population.Sum(f => f.captured)} captured"));
            text.AppendLine(Pair("Loss Burden", $"{r.lossBurden:P0}"));
            if (r.captured > 0) text.AppendLine(Pair("Taken captive", TooltipText.Warn($"{r.captured} section{(r.captured == 1 ? "" : "s")}{(r.capturedIndividuals > 0 ? $" ({r.capturedIndividuals})" : string.Empty)}")));
            if (r.everMindBroken > 0) text.AppendLine(Pair("Mind Broken", TooltipText.Warn($"{r.everMindBroken} section{(r.everMindBroken == 1 ? "" : "s")}")));
            if (r.conductorBroke) text.AppendLine(TooltipText.Bad("Its commander suffered a Mind Break."));
            if (r.cardsPlayed > 0) text.AppendLine(Pair("Cards played", $"{r.cardsPlayed}{(r.cardFlickers > 0 ? TooltipText.Muted($", {r.cardFlickers} flickered") : string.Empty)}{(r.signatureCard != null ? TooltipText.Muted($"; most: {r.signatureCard}") : string.Empty)}"));
        }
        else if (p.forecast != null)
        {
            text.AppendLine();
            text.AppendLine(Pair("Predicted casualties", Casualty(s)));
        }
        return text.ToString().TrimEnd();
    }

    private static string Meter(float kept, bool bad, int width = 20)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(kept * width), 0, width);
        return TooltipText.Symbol(TooltipText.Judge(new string('█', n), !bad) + TooltipText.Muted(new string('░', width - n)));
    }

    /// <summary>The moments that decided it (Mind Breaks, the fallen, the taken, the cards at home on the field), and the aftermath.</summary>
    public static string ComposeMoments(BattleRecord record)
    {
        var text = new StringBuilder();
        var log = record.report.log;
        string[] marks = { "Mind Break", "cut down", "taken alive", "ambush", "layered", "thrown out", "at home here", "Legendary", "Overture", "gives way", "scatter" };
        var moments = log.Where(l => marks.Any(k => l.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)).Take(6).ToList();
        if (moments.Count > 0)
        {
            text.AppendLine(TooltipText.Heading("How it went", $"{record.report.measures} measure{(record.report.measures == 1 ? "" : "s")}"));
            foreach (var m in moments) text.AppendLine(TooltipText.Bullet(m));
        }
        foreach (var line in record.aftermath.Take(4)) text.AppendLine(TooltipText.Bullet(TooltipText.Warn(line)));
        return text.ToString().TrimEnd();
    }

    private static string PreviewFooter() => TooltipText.Muted(
        "Strength weighs each side's sections and its Symphony (its cards: basic decks, kits, the legends' grimoires, a commander's orders) on this field. " +
        "The forecast reads the written score. Choose Humanize the Score to bring it to life, or eligible automatic resolution. Bosses and decisive encounters require Humanize the Score. Winning a Doomed forecast through your performance adds Mythical to the casualty verdict.");
}
