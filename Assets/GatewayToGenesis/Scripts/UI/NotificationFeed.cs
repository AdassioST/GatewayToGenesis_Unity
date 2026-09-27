using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The capital's notices: rhombuses popping into the HUD's NotificationGrid (the scene's layout; its placeholder
/// rhombuses are hidden), stacking up from the bottom. Hover one for what it is about; left click goes there.
/// - Issues stay until they are solved, checked every second: units without orders, no research under way, research
///   stalled for a resource, empty council seats while legends wait, the Age waiting for its technology, land ready
///   to be claimed. They sit at the bottom of the stack and cannot be dismissed.
/// - News waits above them until it is dismissed with a right click (or a left click, which also goes to it):
///   discoveries and world events, research completed, technologies enlightened, Era Score, legends joining or ranking
///   up, a new Age, the crisis.
/// While the world view has the screen the capital's HUD is hidden, so a small plate there says how many notices wait
/// in the capital (clicking it returns). Look: <see cref="NotificationTheme"/> (Resources/UI). Created by
/// <see cref="GenesisLoop"/>.
/// </summary>
public class NotificationFeed : MonoBehaviour
{
    public enum Topic { Research, Council, Units, Land, Discovery, EraScore, Age, Crisis, World }

    private const LogChannel Log = LogChannel.UI;
    private const string GridName = "NotificationGrid";
    private const float CheckSeconds = 1f;

    private class Notice
    {
        public string key, title, body;
        public Topic topic;
        public Action onClick;
        public bool issue;
        public int count = 1;
        public Sprite icon;
    }

    private class View
    {
        public GameObject go;
        public Image plate, icon;
        public TextMeshProUGUI badge;
        public TooltipTrigger tooltip;
        public Notice notice;
        public float age;
        public bool leaving;
    }

    private static NotificationFeed _instance;

    private NotificationTheme _theme;
    private TooltipTheme _textTheme;
    private RectTransform _grid;
    private float _findAt, _checkAt;
    private readonly List<Notice> _news = new List<Notice>();
    private List<Notice> _issues = new List<Notice>();
    private readonly Dictionary<string, View> _views = new Dictionary<string, View>();
    private readonly List<View> _leaving = new List<View>();
    private string _shown;
    private bool _subscribed, _wasDeclared;
    // The world view's reminder that notices wait in the capital.
    private CanvasGroup _away;
    private TextMeshProUGUI _awayText;

    /// <summary>Add a piece of news; it waits until dismissed. The same key replaces the older one.</summary>
    public static void Push(string title, string body, Topic topic, Action onClick = null, string key = null)
    {
        if (_instance == null || string.IsNullOrEmpty(title)) return;
        _instance.AddNews(new Notice { key = "news:" + (key ?? title + "|" + body), title = title, body = body, topic = topic, onClick = onClick });
    }

    /// <summary>Notices waiting in the capital (issues and news).</summary>
    public static int Count => _instance != null ? _instance._issues.Count + _instance._news.Count : 0;

    private void Awake() => _instance = this;

    private void Start()
    {
        _theme = Resources.Load<NotificationTheme>("UI/NotificationTheme");
        if (_theme == null || _theme.prefab == null)
        {
            GameLog.Warning("No Resources/UI/NotificationTheme (with its prefab): the capital shows no notices.", Log);
            enabled = false;
            return;
        }
        _textTheme = CodeUI.Theme(nameof(NotificationFeed));
        BuildAway();
        Subscribe();
    }

    private void Subscribe()
    {
        var world = WorldSystem.Instance;
        if (world != null)
        {
            world.Notice += OnWorldNotice;
            world.UnitNotice += OnUnitNotice;
        }
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.EraScoreAwarded += OnEraScore;
            ages.AgeBegan += OnAgeBegan;
            ages.Changed += OnAgesChanged;
        }
        if (LegendProgress.Instance != null)
        {
            LegendProgress.Instance.Recruited += OnRecruited;
            LegendProgress.Instance.RankedUp += OnRankedUp;
            LegendProgress.Instance.ComposureChanged += OnComposureChanged;
            LegendProgress.Instance.Awakened += OnAwakened;
            LegendProgress.Instance.Lost += OnLegendLost;
        }
        if (EventSystemLogic.Instance != null) EventSystemLogic.Instance.BalladCompleted += OnBalladCompleted;
        GameTechnologySlot.Researched += OnResearched;
        _government = GovernmentLogic.Instance;
        if (_government != null) _government.OnCouncilSeatUnlocked += OnSeatOpened;
        GameTechnologySlot.Enlightened += OnEnlightened;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (!_subscribed) return;
        var world = WorldSystem.Instance;
        if (world != null)
        {
            world.Notice -= OnWorldNotice;
            world.UnitNotice -= OnUnitNotice;
        }
        var ages = AgeProgression.Instance;
        if (ages != null)
        {
            ages.EraScoreAwarded -= OnEraScore;
            ages.AgeBegan -= OnAgeBegan;
            ages.Changed -= OnAgesChanged;
        }
        if (LegendProgress.Instance != null)
        {
            LegendProgress.Instance.Recruited -= OnRecruited;
            LegendProgress.Instance.RankedUp -= OnRankedUp;
            LegendProgress.Instance.ComposureChanged -= OnComposureChanged;
            LegendProgress.Instance.Awakened -= OnAwakened;
            LegendProgress.Instance.Lost -= OnLegendLost;
        }
        if (EventSystemLogic.Instance != null) EventSystemLogic.Instance.BalladCompleted -= OnBalladCompleted;
        GameTechnologySlot.Researched -= OnResearched;
        if (_government != null) _government.OnCouncilSeatUnlocked -= OnSeatOpened;
        GameTechnologySlot.Enlightened -= OnEnlightened;
    }

    // ===== NEWS =====

    private void OnWorldNotice(string message)
    {
        bool found = message.Contains("Found:");
        Push(found ? "A discovery" : "The World", message, found ? Topic.Discovery : Topic.World, WorldView.ShowWorld);
    }

    // How a unit fares (hunger, exhaustion, attrition, its loss): a click opens the world on it.
    private void OnUnitNotice(WorldUnit unit, string title, string message)
    {
        int id = unit != null ? unit.id : -1;
        Push(title, message, Topic.Units, id >= 0 ? () => WorldView.ShowUnit(id) : (Action)WorldView.ShowWorld, "unit:" + id + ":" + title);
    }

    private void OnEraScore(int points, string reason) => Push($"{points:+0;-0} Era Score", reason, Topic.EraScore, key: "era:" + reason);

    private void OnAgeBegan(AgeDefinition age)
    {
        if (age != null) Push(age.title, age.epigraph, Topic.Age, key: "age:" + age.id);
    }

    private void OnAgesChanged()
    {
        var ages = AgeProgression.Instance;
        if (ages == null) return;
        if (ages.CrisisDeclared && !_wasDeclared) Push(ages.Current.crisisTitle, "The Age Crisis is named. Food output falls while it lasts.", Topic.Crisis, key: "crisis:" + ages.Current.id);
        _wasDeclared = ages.CrisisDeclared;
    }

    private void OnRecruited(LegendData legend)
    {
        if (legend == null) return;
        AddNews(new Notice { key = "news:legend:" + legend.legendName, title = $"{legend.legendName} joins you", body = "A legend waits for a seat on the council.", topic = Topic.Council, onClick = OpenGovernment, icon = legend.portrait });
    }

    private void OnRankedUp(string legend, int rank) => Push($"{legend} is now rank {rank}", "Their council bonuses grow.", Topic.Council, OpenGovernment, "rank:" + legend);

    // A Soul Leitmotif cracking is news; Spiraling is an issue (below) until the legend rests or recovers.
    private void OnComposureChanged(string legend, ComposureState from, ComposureState to)
    {
        if (to == ComposureState.Fractured && from < to)
            Push($"{legend}'s Soul Leitmotif is Fractured", "Their council bonuses dim. Rest mends it, and healing back to Clouded brings a Motif Awakening.", Topic.Council, OpenGovernment, "composure:" + legend);
    }

    private void OnAwakened(string legend, AwakeningResult result)
    {
        var tuning = LegendLore.ComposureTuning;
        if (result.abyss)
        {
            Push($"{legend}: the Catalytic Abyss of Emotion", $"Their Soul Leitmotif erupts into its Awakened State: their council bonuses are multiplied by {tuning.abyssSurge:0.##} for {tuning.abyssSevenths} Sevenths, then it collapses.",
                Topic.Council, OpenGovernment, "awakening:" + legend);
            return;
        }
        string evolved = result.Evolved ? $" {result.evolvedFrom} became {result.evolvedTo}." : string.Empty;
        Push($"Motif Awakening: {legend}", $"Their Soul Leitmotif gains its {(result.ornament == 1 ? "primary" : "secondary")} Ornament, {result.element}.{evolved}", Topic.Council, OpenGovernment, "awakening:" + legend);
    }

    // A ballad sung to its end: its actors are rewarded in its theme.
    private void OnBalladCompleted(BalladRecord ballad)
    {
        var cast = ballad.cast;
        string who = cast == null || cast.Empty ? "No one played it to the end." : $"{string.Join(", ", cast.Members)} grow{(cast.Members.Count() == 1 ? "s" : string.Empty)} in its theme.";
        Push($"{ballad.title} is sung to its end", who, Topic.Council, OpenGovernment, "ballad:" + ballad.id);
    }

    private void OnLegendLost(string legend)
    {
        string epitaph = LegendLore.Composure.Emphasis(ComposureState.Surrender);
        Push($"{legend} is lost to Dissonance", (epitaph != null ? $"\"{epitaph}\" " : string.Empty) + "Their Soul Leitmotif reached Surrender; they have left the council.", Topic.Council, OpenGovernment, "lost:" + legend);
    }

    private GovernmentLogic _government;

    private void OnSeatOpened(CouncilSeat seat)
    {
        if (seat != null) Push("A council seat opens", $"{seat.GetEffectiveTitle()} waits for a legend.", Topic.Council, OpenGovernment, "seat:" + seat.seatIndex);
    }

    private void OnResearched(GameTechnologySlot slot)
    {
        if (slot == null || slot.gameUnit == null) return;
        Push("Research complete", slot.gameUnit.name, Topic.Research, OpenResearch, "research:" + slot.gameUnit.name);
    }

    // An Enlightenment uncovers the technology in its tree (even before its way) and pays part of its research.
    private void OnEnlightened(GameTechnologySlot slot, string reason)
    {
        if (slot == null || slot.gameUnit == null) return;
        int percent = Mathf.RoundToInt(Mathf.Clamp01(slot.enlightenedBonusPercent) * 100f);
        string why = string.IsNullOrEmpty(reason) ? string.Empty : $"{reason.TrimEnd('.')}. ";
        Push($"{slot.gameUnit.name} enlightened", $"{why}{percent}% of its research is done, and it now shows in the technology tree.", Topic.Research, OpenResearch, "enlightened:" + slot.gameUnit.name);
    }

    private void AddNews(Notice notice)
    {
        _news.RemoveAll(n => n.key == notice.key);
        _news.Add(notice);
        int max = _theme != null ? Mathf.Max(1, _theme.maxNews) : 6;
        while (_news.Count > max) _news.RemoveAt(0);
        _shown = null;
    }

    private void Dismiss(Notice notice)
    {
        if (notice == null || notice.issue) return;
        _news.Remove(notice);
        _shown = null;
    }

    // ===== ISSUES =====

    public static void OpenResearch()
    {
        if (WorldView.IsOpen) WorldView.Toggle();
        else TabHotkeys.Instance?.OpenResearchTab();
    }

    public static void OpenGovernment()
    {
        if (WorldView.IsOpen) WorldView.Toggle();
        else TabHotkeys.Instance?.OpenGovernmentTab();
    }

    private List<Notice> Issues()
    {
        var issues = new List<Notice>();
        void Add(string key, string title, string body, Topic topic, Action onClick, int count = 1, Sprite icon = null) =>
            issues.Add(new Notice { key = "issue:" + key, title = title, body = body, topic = topic, onClick = onClick, issue = true, count = count, icon = icon });

        var events = EventSystemLogic.Instance;
        var volumes = EventVolumeManager.Instance;
        if (events != null && volumes != null && !events.IsEventActive())
            foreach (var story in volumes.AvailableIssues())
                Add("story:" + story.nodeName, story.storyTitle, story.storyDescription,
                    Topic.World, () => events.TryStartIssue(story));

        var ages = AgeProgression.Instance;
        string gate = ages != null ? ages.WaitingGate : null;
        if (gate != null) Add("gate", "The Age waits", $"Research {gate} to turn the Act of Fate.", Topic.Age, OpenResearch);

        var units = GameUnitsLogic.Instance;
        if (units != null)
        {
            string stalled = units.ResearchStalledOn();
            if (units.activeTechnologySlot == null)
            {
                int ready = units.ResearchableTechnologies().Count;
                if (ready > 0) Add("research", "No research under way", $"{ready} technolog{(ready == 1 ? "y" : "ies")} can be researched.", Topic.Research, OpenResearch);
            }
            else if (stalled != null)
                Add("stalled", "Research stalled", $"{units.activeTechnologySlot.gameUnit?.name} needs more {stalled} to continue.", Topic.Research, OpenResearch);
        }

        // Food falling: the stores feed the shortfall while they last; with nothing left, people starve.
        var pantry = Pantry.Instance;
        if (pantry != null && pantry.CoverRate > 0f)
        {
            float left = pantry.SecondsLeft;
            string lasts = float.IsInfinity(left) ? string.Empty : left >= 90f ? $", about {Mathf.RoundToInt(left / 60f)} min left" : $", about {Mathf.RoundToInt(left)} s left";
            Add("stores", "The stores feed the people", $"Food is falling: the stores give {pantry.CoverRate:0.##} food value a second ({pantry.Value:0} stored{lasts}). Grow more Food or gather more stores.", Topic.Crisis, null);
        }
        else if (PopGrowthLogic.Instance != null && PopGrowthLogic.Instance.isFoodScarce && PopGrowthLogic.Instance.population > 0)
            Add("hunger", "Hunger", "Food is falling and the stores are empty: people are starving.", Topic.Crisis, null);

        var government = GovernmentLogic.Instance;
        var legends = LegendProgress.Instance;
        if (government != null && legends != null)
        {
            var seats = government.GetActiveRegularSeats().Where(s => s != null && s.isUnlocked).ToList();
            var head = government.GetCouncilSeat(GovernmentLogic.HeadOfStateIndex);
            if (head != null) seats.Add(head);
            var seated = new HashSet<string>(seats.Where(s => s.assignedLegend != null).Select(s => s.assignedLegend.legendName));
            // Legends away with an expedition cannot take a seat.
            var road = WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance : null;
            var free = legends.RecruitedNames.Where(n => !seated.Contains(n) && (road == null || road.ExpeditionOf(n) == null)).ToList();
            int empty = seats.Count(s => s.assignedLegend == null);
            if (empty > 0 && free.Count > 0)
            {
                var portrait = GameCatalog.Legends.All.FirstOrDefault(l => l != null && l.legendName == free[0])?.portrait;
                Add("council", "Council seats stand empty", $"{empty} seat{(empty == 1 ? "" : "s")} open, {free.Count} legend{(free.Count == 1 ? "" : "s")} without a seat: {string.Join(", ", free)}.",
                    Topic.Council, OpenGovernment, empty, portrait);
            }

            // Spiraling legends still at work (on the council or the road): resting ones are already mending.
            var spiraling = legends.RecruitedNames.Where(n => legends.Composure(n) == ComposureState.Spiraling &&
                (seated.Contains(n) || road != null && road.ExpeditionOf(n) != null)).ToList();
            if (spiraling.Count > 0)
            {
                string warning = LegendLore.Composure.Emphasis(ComposureState.Spiraling);
                var portrait = GameCatalog.Legends.All.FirstOrDefault(l => l != null && l.legendName == spiraling[0])?.portrait;
                Add("spiraling", spiraling.Count == 1 ? $"{spiraling[0]} is Spiraling" : $"{spiraling.Count} legends are Spiraling",
                    (warning != null ? warning + " " : string.Empty) + $"Unseat {string.Join(", ", spiraling)} to rest before their Soul Leitmotif reaches Surrender.",
                    Topic.Council, OpenGovernment, spiraling.Count, portrait);
            }
        }

        var world = WorldSystem.Instance;
        if (world != null && world.Map != null && world.MapUnlocked)
        {
            // No expedition in the field while slots and a legend are free: the world waits to be walked.
            if (!world.ExpeditionUnits.Any() && world.FreeExpeditionSlots > 0 && world.Candidates().Count > 0)
                Add("expedition", "No expedition in the field", $"{world.FreeExpeditionSlots} expedition slot{(world.FreeExpeditionSlots == 1 ? "" : "s")} free. Click the Capital on the world map and form one around a legend.",
                    Topic.Units, WorldView.ShowWorld);
            var idle = world.Map.Units.Where(WorldUnits.AwaitsOrders).ToList();
            if (idle.Count > 0)
            {
                int first = idle[0].id;
                Add("units", idle.Count == 1 ? $"{idle[0].name} needs orders" : $"{idle.Count} units need orders",
                    $"{string.Join(", ", idle.Select(u => u.name))}. Select one on the world map and right click where it should go.", Topic.Units, () => WorldView.ShowUnit(first), idle.Count);
            }
            var starving = world.Map.Units.Where(u => u.hungry).ToList();
            if (starving.Count > 0)
            {
                int first = starving[0].id;
                Add("starving", starving.Count == 1 ? $"{starving[0].name} is starving" : $"{starving.Count} units are starving",
                    $"{string.Join(", ", starving.Select(u => u.name))}: out of rations, they falter and wear down. Bring them into your authority, or make camp on fertile ground.",
                    Topic.Units, () => WorldView.ShowUnit(first), starving.Count);
            }
            int claimable = world.ClaimableCount();
            if (claimable > 0) Add("claim", "Land can be claimed", $"{claimable} known cell{(claimable == 1 ? "" : "s")} border your authority ({world.ClaimCostText} each).", Topic.Land, WorldView.ShowWorld, claimable);
            // An administration past its capacity: yields fall, society stops adopting land, past collapse the fringe slips away.
            var realm = world.Realm;
            if (realm.favoured == Expansion.Overextended)
            {
                bool slipping = realm.strain > world.Rules.territory.collapseStrain;
                Add("overextended", slipping ? "Your realm is slipping away" : "Administration overextended",
                    $"Load {realm.load:0} of {realm.capacity:0} capacity (strain {realm.strain:P0}): yields at {realm.efficiency:P0}, society adopts no land{(slipping ? " and the weakest-held land slips back into the wilderness" : string.Empty)}. Develop settlements, build roads, raise Government Capacity, or see the Realm panel on the world map.",
                    Topic.Land, WorldView.ShowWorld);
            }
        }
        return issues;
    }

    // ===== THE STACK =====

    private void Update()
    {
        if (_theme == null) return;
        if (Time.unscaledTime >= _checkAt)
        {
            _checkAt = Time.unscaledTime + CheckSeconds;
            _issues = Issues();
        }
        if (_grid == null && Time.unscaledTime >= _findAt)
        {
            _findAt = Time.unscaledTime + 2f;
            FindGrid();
        }
        if (_grid != null)
        {
            // News above, issues at the bottom (the grid stacks from its lower end); the newest news nearest the issues.
            int max = Mathf.Max(1, _theme.maxShown);
            var shown = _issues.Take(max).ToList();
            shown.InsertRange(0, _news.Skip(Math.Max(0, _news.Count - (max - shown.Count))).Take(max - shown.Count));
            string signature = string.Join("\n", shown.Select(n => $"{n.key}|{n.title}|{n.body}|{n.count}"));
            if (signature != _shown)
            {
                _shown = signature;
                Sync(shown);
            }
            Animate();
        }
        UpdateAway();
    }

    private void FindGrid()
    {
        var hud = TabHotkeys.Instance != null ? TabHotkeys.Instance.Hud : null;
        var grid = hud != null ? hud.transform.Find(GridName) : null;
        if (grid == null)
        {
            if (TabHotkeys.Instance != null) GameLog.Warning($"The HUD has no '{GridName}': the capital shows no notices.", Log);
            return;
        }
        _grid = (RectTransform)grid;
        // The scene's rhombuses are a layout preview.
        foreach (Transform child in _grid) child.gameObject.SetActive(false);
        _shown = null;
    }

    private void Sync(List<Notice> shown)
    {
        var keys = new HashSet<string>(shown.Select(n => n.key));
        foreach (var key in _views.Keys.Where(k => !keys.Contains(k)).ToList())
        {
            var leaving = _views[key];
            leaving.leaving = true;
            leaving.age = 0f;
            _leaving.Add(leaving);
            _views.Remove(key);
        }
        foreach (var notice in shown)
        {
            if (!_views.TryGetValue(notice.key, out var view)) _views[notice.key] = view = Create();
            Bind(view, notice);
        }
        // Sibling order is stack order: the first shown on top, the last at the bottom.
        foreach (var notice in shown) _views[notice.key].go.transform.SetAsLastSibling();
    }

    private View Create()
    {
        var go = Instantiate(_theme.prefab, _grid);
        go.name = "Notice";
        go.SetActive(true);
        go.transform.localScale = Vector3.zero;
        var view = new View { go = go, plate = go.GetComponent<Image>(), tooltip = TooltipTrigger.Ensure(go) };
        var iconTransform = go.transform.Find("Icon");
        view.icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (view.icon != null)
        {
            view.icon.raycastTarget = false;
            view.icon.preserveAspect = true;
        }
        var clicks = go.AddComponent<NoticeClicks>();
        clicks.left = () => Activate(view);
        clicks.right = () => Dismiss(view.notice);
        var button = go.GetComponent<Button>();
        if (button != null) button.onClick.RemoveAllListeners(); // NoticeClicks takes both buttons

        if (_textTheme != null)
        {
            // The count sits on the rhombus's lower-right face (the grid is scaled down, so the text is large).
            view.badge = CodeUI.Label(go.transform, "Count", string.Empty, _textTheme.titleSize * 1.9f, Color.white, FontStyles.Bold, _textTheme);
            view.badge.alignment = TextAlignmentOptions.Center;
            view.badge.textWrappingMode = TextWrappingModes.NoWrap;
            view.badge.outlineWidth = 0.35f;
            view.badge.outlineColor = new Color32(0, 0, 0, 240);
            view.badge.raycastTarget = false;
            CodeUI.Place(view.badge.rectTransform, new Vector2(0.55f, 0.02f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        }
        return view;
    }

    private void Bind(View view, Notice notice)
    {
        view.notice = notice;
        if (view.plate != null)
            view.plate.sprite = notice.issue ? _theme.issue : notice.topic == Topic.Discovery || notice.topic == Topic.World || notice.topic == Topic.Land || notice.topic == Topic.Units ? _theme.world : _theme.news;
        if (view.icon != null) view.icon.sprite = notice.icon != null ? notice.icon : _theme.Icon(notice.topic);
        if (view.badge != null) view.badge.text = notice.count > 1 ? notice.count.ToString() : string.Empty;
        string hint = notice.issue ? "Stays until it is solved. Left click to see to it." : "Left click to see it, right click to dismiss it.";
        view.tooltip.SetCustom(notice.title, KeywordMarkup.SafeGlyphs($"{notice.body}\n\n{TooltipText.Muted(hint)}".Trim()), notice.issue ? "Issue" : "News");
    }

    // Left click: go to what it is about (news is then done with).
    private void Activate(View view)
    {
        var notice = view.notice;
        if (notice == null) return;
        notice.onClick?.Invoke();
        Dismiss(notice);
    }

    // Pop in with a little overshoot; shrink away, then go.
    private void Animate()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, _theme.popSeconds);
        foreach (var view in _views.Values)
        {
            if (view.age >= 1f) continue;
            view.age = Mathf.Min(1f, view.age + step);
            float t = view.age, overshoot = 1.7f;
            float s = 1f + (overshoot + 1f) * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
            view.go.transform.localScale = Vector3.one * Mathf.Max(0f, s);
        }
        for (int i = _leaving.Count - 1; i >= 0; i--)
        {
            var view = _leaving[i];
            view.age = Mathf.Min(1f, view.age + step);
            if (view.go != null) view.go.transform.localScale = Vector3.one * (1f - view.age);
            if (view.age < 1f && view.go != null) continue;
            if (view.go != null) Destroy(view.go);
            _leaving.RemoveAt(i);
        }
    }

    // ===== IN THE WORLD VIEW =====

    private void BuildAway()
    {
        if (_textTheme == null) return;
        var canvas = CodeUI.Canvas(transform, "Notices Away Canvas", 3, out var scaler);
        _away = canvas.gameObject.AddComponent<CanvasGroup>();
        var plate = CodeUI.Panel(canvas.transform, "Notices Wait", new Vector2(1f, 1f), new Vector2(1f, 1f));
        plate.pivot = new Vector2(1f, 1f);
        plate.sizeDelta = new Vector2(360f, 56f);
        plate.anchoredPosition = new Vector2(-16f, -16f);
        CodeUI.Plate(plate, _textTheme, scaler, 0.92f);
        _awayText = CodeUI.TextButton(plate, string.Empty, () => { if (WorldView.IsOpen) WorldView.Toggle(); }, _textTheme, _textTheme.subtitleSize + 2f);
        _awayText.alignment = TextAlignmentOptions.Center;
        _awayText.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(_awayText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 6f), new Vector2(-16f, -6f));
        TooltipTrigger.Ensure(_awayText.gameObject).SetCustom("Notices", "Notices are shown in the capital. Click to return to it.");
        SetAway(false);
    }

    private void UpdateAway()
    {
        if (_away == null) return;
        int count = Count;
        bool show = WorldView.Current == WorldView.Mode.World && count > 0 && !SaveMenu.BlocksGameplay;
        SetAway(show);
        if (!show) return;
        int issues = _issues.Count;
        string text = $"{count} notice{(count == 1 ? "" : "s")} in the capital";
        _awayText.text = issues > 0 ? $"{text} {TooltipText.Warn($"({issues} to solve)")}" : text;
    }

    private void SetAway(bool visible)
    {
        _away.alpha = visible ? 1f : 0f;
        _away.blocksRaycasts = _away.interactable = visible;
    }
}

/// <summary>Left and right clicks on a notice (the Button alone only hears the left one).</summary>
public class NoticeClicks : MonoBehaviour, IPointerClickHandler
{
    public Action left, right;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) left?.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Right) right?.Invoke();
    }
}
