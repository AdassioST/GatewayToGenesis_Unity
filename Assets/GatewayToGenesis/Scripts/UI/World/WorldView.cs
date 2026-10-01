using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The world screen (roadmap WG11, S07): a primary view like a strategy game's map, with the capital as its most
/// zoomed-in reading, and it opens once its technology is researched (<see cref="WorldSettings.mapTechnology"/>). Scrolling
/// out past the capital camera's widest view (<see cref="CameraMovement"/>) pulls back from the capital's own hex into the world;
/// scrolling keeps zooming through the three readings (micro, meso, macro) about the cursor; at the closest zoom,
/// scrolling in over the capital returns to it. M toggles, Esc steps back (a popup, the selection, then the capital).
///
/// Most of the screen is the map; the Age banner (<see cref="AgeBanner"/>) and the notices (<see cref="NotificationFeed"/>)
/// stay over it. The HUD wears the capital's look (stone plates, dark slots rimmed in brass): a map key in the upper
/// left names the reading and lens and what the lens's colours mean; the lower-left dock has four doors (Map: lens,
/// layers and reading; Parties; Realm; Capital); a card under the cursor tells about the cell it rests on (at the micro
/// reading, about the hex too); a card in the lower right shows the selected unit, settlement, enclave or cell: who or
/// what it is and how it fares, its orders first, and the long reports folded away.
/// Left click selects (the unit under the cursor, else what stands on the cell; again: the next one), right click
/// sends the selected unit: at the micro reading to the hex itself, beyond it to the heart of the cell (either way to
/// the nearest ground it can reach), with Shift to survey on arrival; any drag pans. Units walk the micro hexes
/// (<see cref="MicroGrid"/>). Built in code on its own canvas, under the event overlay so stories still show; time
/// keeps running.
/// </summary>
public class WorldView : MonoBehaviour
{
    public enum Mode { Capital, Opening, World, Closing }
    private enum Popup { None, Map, Units, Realm }
    private static readonly WorldLens[] CommonLenses =
        { WorldLens.Normal, WorldLens.Settle, WorldLens.Resources, WorldLens.Authority, WorldLens.Danger, WorldLens.Terrain };

    private const float OpenSeconds = 0.75f, CloseSeconds = 0.6f, RefreshSeconds = 0.5f, DragPixels = 6f;
    private const float CardWidth = 440f, HoverWidth = 390f, Pad = 16f, Inner = 18f, KeyWidth = 430f, DockWidth = 920f;
    /// <summary>Most micro hexes a hover's route preview searches (a farther way is planned when ordered).</summary>
    private const int PreviewVisits = 60000;
    private const string FarAway = "Too far to trace from here: right click and it plans the way.";

    // The way a right click would send the selected unit, kept until the unit, the target or the land changes.
    private struct Preview
    {
        public bool valid, micro;
        public int unit, stamp;
        public HexCoord from, target, reached;
        public string why;
        public List<int> path;
        public float fatigue;
    }

    private static WorldView _instance;

    /// <summary>The world has (or is taking or giving back) the screen.</summary>
    public static bool IsOpen => _instance != null && _instance._mode != Mode.Capital;

    public static Mode Current => _instance != null ? _instance._mode : Mode.Capital;

    // ===== FOR THE MINI TUTORIALS (read only) =====

    /// <summary>The unit selected on the open map, or null.</summary>
    public static WorldUnit SelectedUnit => _instance != null && IsOpen ? WorldSystem.Instance?.UnitById(_instance._selectedUnit) : null;

    /// <summary>The cell selected on the open map (bare land, a settlement or an enclave), or null.</summary>
    public static WorldTile SelectedTile => _instance != null && IsOpen && _instance._selected.HasValue ? WorldSystem.Instance?.Map?.Get(_instance._selected.Value) : null;

    /// <summary>The panel open over the map: "Map", "Units", "Realm", or null.</summary>
    public static string OpenPanel => _instance != null && IsOpen && _instance._popup != Popup.None ? _instance._popup.ToString() : null;

    /// <summary>The selection card's order whose name starts with <paramref name="name"/> (its button plate), or null.</summary>
    public static Graphic OrderButton(string name) => _instance != null && _instance._mode == Mode.World && _instance._card != null && _instance._card.gameObject.activeInHierarchy
        ? _instance._selectionList?.Find(name) : null;

    /// <summary>The dock's button ("Map", "Parties", "Realm"), or null.</summary>
    public static Graphic DockButton(string name)
    {
        if (_instance == null || _instance._mode != Mode.World) return null;
        var label = _instance._dockButtons.FirstOrDefault(b => b != null && b.gameObject.name == name);
        return label != null ? label.GetComponent<Graphic>() : null;
    }

    /// <summary>Where a point of the map is on screen (pixels), while the map has the screen.</summary>
    public static bool ScreenPoint(float x, float y, out Vector2 screen)
    {
        screen = default;
        return _instance != null && _instance._mode == Mode.World && _instance._renderer != null && _instance._renderer.WorldToScreen(new Vector2(x, y), out screen);
    }

    private Mode _mode = Mode.Capital;
    private WorldRenderer _renderer;
    private bool _built, _buildFailed;
    private float _t;
    private float _cx, _cy, _size = WorldZoom.MicroSize, _farthest = 400f;
    private float _fromX, _fromY, _fromSize, _toX, _toY, _toSize;
    private readonly List<(CanvasGroup group, float alpha, bool raycasts)> _capital = new List<(CanvasGroup, float, bool)>();
    private CanvasGroup _eventGroup;
    private bool _addedEventGroup, _eventIgnored;

    // HUD
    private TooltipTheme _theme;
    private CanvasScaler _scaler;
    private Canvas _canvas;
    private CanvasGroup _hud;
    private RectTransform _canvasRect, _hoverCard, _card;
    private TextMeshProUGUI _hoverText;
    private readonly Dictionary<Popup, RectTransform> _popups = new Dictionary<Popup, RectTransform>();
    private IconActionButton _mapDock, _unitsDock, _realmDock, _guide;
    private QuickActionBar _actionBar;
    private RectTransform _key;
    private readonly Dictionary<Popup, HudList> _menuLists = new Dictionary<Popup, HudList>();
    private HudList _selectionList, _rosterList;
    private RectTransform _dock;
    private readonly List<IconActionButton> _dockButtons = new List<IconActionButton>();
    private readonly HashSet<string> _expandedSections = new HashSet<string>();
    private string _cardIdentity;
    private Popup _popup;

    // Interaction
    private int _selectedUnit = -1;
    private HexCoord? _selected, _selectedMacro;
    // The micro hex clicked with the cell at the micro reading (what a claim takes), or null.
    private HexCoord? _selectedHex;
    private bool _rivers = true, _leylines, _forecast, _previous;
    private Vector2 _pressAt;
    private int _pressButton = -1;
    private bool _dragging;
    private float _refreshAt, _hoverAt, _lockedNoticeAt, _knowledgeAt;
    private int _knowledgeSeen = -1, _magicSeen = -1, _civilizationSeen = -1;
    private int _weatherSeen = -1, _cultureSeen = -1;
    private HexCoord? _hovered, _hoveredMicro;
    private Preview _preview;
    // Where the pointer is on the map (world units), for the band under it.
    private Vector2 _pointer;
    private int _feelingsSeen = -1;
    // The Settle lens's field and the cells where a town could be founded now (recomputed when the world changes).
    private float[] _potential;
    private HashSet<int> _foundable;
    private int _potentialStamp = -1;
    // The cells society will adopt next (the Territory lens draws them bright).
    private HashSet<int> _nextAdoptions = new HashSet<int>();
    private readonly List<WorldRenderer.UnitMark> _marks = new List<WorldRenderer.UnitMark>();
    // The tags over your parties' tokens ("Surveying 43%"), pooled.
    private RectTransform _tagLayer;
    private readonly List<UnitTag> _tags = new List<UnitTag>();

    private sealed class UnitTag
    {
        public RectTransform rect, fill;
        public TextMeshProUGUI label;
        public string text;
        public float progress = -2f;
    }
    private readonly List<Vector2> _path = new List<Vector2>();

    // The plans drawn on the map under the units (survey tours, the hexes society settles next), and the badges over
    // them: a survey's hexes numbered in the order it walks them like the research plan's, each hex society will
    // settle with about when.
    private RectTransform _badgeLayer;
    private readonly List<WorldRenderer.PlanMark> _plans = new List<WorldRenderer.PlanMark>();
    private readonly List<WorldRenderer.PlanLine> _planLines = new List<WorldRenderer.PlanLine>();
    private readonly List<BadgeWant> _badgeWants = new List<BadgeWant>();
    private readonly List<PlanBadge> _badges = new List<PlanBadge>();
    // Each surveying party's tour, planned again only when it moved, its cell's survey changed or the land did.
    private readonly Dictionary<int, (PlanStamp stamp, List<HexCoord> plan)> _surveyPlans = new Dictionary<int, (PlanStamp, List<HexCoord>)>();
    private (PlanStamp stamp, List<HexCoord> plan) _pickPlan;

    private struct BadgeWant
    {
        public Vector2 at;
        public string text;
        public Color rim;
        public float alpha;
        // A rhombus with a number (a survey's order), or a plate with words (when society settles a hex).
        public bool rhombus;
    }

    private sealed class PlanBadge
    {
        public RectTransform rect, rimRect, faceRect;
        public Image rim, face;
        public CanvasGroup group;
        public TextMeshProUGUI label;
        public string text;
        public bool rhombus;
    }

    private readonly struct PlanStamp : IEquatable<PlanStamp>
    {
        private readonly int _unit, _mask, _version, _knowledge;
        private readonly HexCoord _at, _cell;

        public PlanStamp(int unit, HexCoord at, HexCoord cell, int mask, int version, int knowledge)
        {
            _unit = unit;
            _at = at;
            _cell = cell;
            _mask = mask;
            _version = version;
            _knowledge = knowledge;
        }

        public bool Equals(PlanStamp o) => _unit == o._unit && _at == o._at && _cell == o._cell && _mask == o._mask && _version == o._version && _knowledge == o._knowledge;
        public override bool Equals(object obj) => obj is PlanStamp o && Equals(o);
        public override int GetHashCode() => _unit * 31 + _mask;
    }

    // ===== OPENING AND CLOSING =====

    public static void Toggle()
    {
        if (_instance == null) return;
        if (_instance._mode == Mode.Capital || _instance._mode == Mode.Closing) _instance.Open();
        else _instance.Close();
    }

    /// <summary>Open the world (if it is not already), e.g. from a notice.</summary>
    public static void ShowWorld()
    {
        if (_instance != null && (_instance._mode == Mode.Capital || _instance._mode == Mode.Closing)) _instance.Open();
    }

    /// <summary>Open the world reading it through one lens (the Culture window's "Show on the map").</summary>
    public static void ShowLens(WorldLens lens)
    {
        ShowWorld();
        if (_instance != null && _instance._renderer != null) _instance.SetLens(lens);
    }

    /// <summary>Open the world on a unit and select it.</summary>
    public static void ShowUnit(int id)
    {
        ShowWorld();
        if (_instance != null && IsOpen) _instance.SelectUnit(id);
    }

    /// <summary>
    /// The capital camera was scrolled out past its widest view (<see cref="CameraMovement"/>): the zoom carries on
    /// into the world, so the capital's closest view and the world's are one continuous zoom.
    /// </summary>
    public static void ZoomOutOfCapital()
    {
        if (_instance == null || _instance._mode != Mode.Capital || !CanLeaveCapital()) return;
        _instance.Open();
    }

    private static bool CanLeaveCapital()
    {
        var world = WorldSystem.Instance;
        if (world == null || world.Map == null || LibraryWindow.IsOpen || GameInput.IsTyping) return false;
        return TabHotkeys.Instance == null || !TabHotkeys.Instance.IsEventActive;
    }

    private void Awake()
    {
        _instance = this;
        GameInput.MapPressed += Toggle;
        GameInput.CancelPressed += OnCancel;
    }

    private void OnDestroy()
    {
        GameInput.MapPressed -= Toggle;
        GameInput.CancelPressed -= OnCancel;
        var world = WorldSystem.Instance;
        if (world != null) world.Changed -= MarkDirty;
        BattleWindow.Unwatch(world);
        if (LegendProgress.Instance != null) LegendProgress.Instance.Changed -= MarkDirty;
        RumourKeeper.Changed -= MarkDirty;
        RestoreCapital();
        _renderer?.Destroy();
        if (_instance == this) _instance = null;
    }

    // Esc steps back: choosing where to survey, a popup, then the selection, then the capital.
    private void OnCancel()
    {
        if (_mode != Mode.World && _mode != Mode.Opening) return;
        // The Chronicle takes the Esc that closes it.
        if (EraTimelineWindow.IsOpen || EraTimelineWindow.ClosedFrame == Time.frameCount) return;
        if (RumoursWindow.IsOpen || RumoursWindow.ClosedFrame == Time.frameCount) return;
        if (BattleWindow.IsOpen || BattleWindow.ClosedFrame == Time.frameCount) return;
        if (BattleEncounterWindow.IsOpen || BattleRecoveryWindow.IsOpen || BalladJournalView.IsOpen || BestiaryWindow.IsOpen || BestiaryWindow.ClosedFrame == Time.frameCount) return;
        if (SymphonyWindow.IsOpen || SymphonyWindow.ClosedFrame == Time.frameCount) return;
        // So do the nation's windows.
        if (CultureAtlasWindow.IsOpen || CultureAtlasWindow.ClosedFrame == Time.frameCount || CultureWindow.IsOpen || CultureWindow.ClosedFrame == Time.frameCount || CultureNamingDialog.IsOpen || CultureNamingDialog.ClosedFrame == Time.frameCount) return;
        if (_surveyPick) { _surveyPick = false; Refresh(force: true); return; }
        if (_popup != Popup.None) { ShowPopup(Popup.None); return; }
        if (_selectedUnit >= 0 || _selected.HasValue || _selectedMacro.HasValue) { Deselect(); return; }
        Close();
    }

    private void Open()
    {
        var world = WorldSystem.Instance;
        if (world == null || world.Map == null) return;
        // Before its technology only the Capital is known.
        if (!world.MapUnlocked)
        {
            if (Time.unscaledTime >= _lockedNoticeAt)
            {
                _lockedNoticeAt = Time.unscaledTime + 4f;
                NotificationFeed.Push("The world is unknown", $"Research {world.Settings.mapTechnology} to look beyond the walls.", NotificationFeed.Topic.Research, NotificationFeed.OpenResearch, "map-locked");
            }
            return;
        }
        if (!_built) Build();
        if (!_built) return;
        if (LibraryWindow.IsOpen) LibraryWindow.Close();
        var tooltips = TooltipSystemLogic.Instance;
        if (tooltips != null) tooltips.HideTooltip();

        var capital = world.Map.Get(world.Map.Capital);
        if (_mode == Mode.Capital)
        {
            _cx = _fromX = _toX = capital != null ? capital.x : 0f;
            _cy = _fromY = _toY = capital != null ? capital.y : 0f;
            _fromSize = WorldZoom.CapitalSize;
            _toSize = WorldZoom.MicroSize;
            CollectCapital();
        }
        else
        {
            _fromX = _cx;
            _fromY = _cy;
            _fromSize = _size;
            _toX = _cx;
            _toY = _cy;
            _toSize = WorldZoom.MicroSize;
        }
        _mode = Mode.Opening;
        _t = 0f;
        _hud.blocksRaycasts = _hud.interactable = true;
        var first = world.Map.Units.FirstOrDefault(u => WorldBattles.IsPlayers(u));
        if (_selectedUnit < 0 && first != null) _selectedUnit = first.id;
        Refresh(force: true);
    }

    private void Close()
    {
        if (_mode == Mode.Capital || _mode == Mode.Closing || _renderer == null) return;
        var world = WorldSystem.Instance;
        var capital = world != null && world.Map != null ? world.Map.Get(world.Map.Capital) : null;
        _fromX = _cx;
        _fromY = _cy;
        _fromSize = _size;
        _toX = capital != null ? capital.x : _cx;
        _toY = capital != null ? capital.y : _cy;
        _toSize = WorldZoom.CapitalSize;
        _mode = Mode.Closing;
        _t = 0f;
        // The capital comes back at its widest view, where the world left it: scrolling in keeps zooming into it.
        if (CameraMovement.Instance != null) CameraMovement.Instance.ZoomToWidest();
        _hud.blocksRaycasts = _hud.interactable = false;
        ShowPopup(Popup.None);
        var tooltips = TooltipSystemLogic.Instance;
        if (tooltips != null) tooltips.HideTooltip();
    }

    // The capital's canvases fade while the world has the screen. The event overlay lives inside the capital's
    // canvas, so it gets a group of its own that ignores the fade: stories still show over the world.
    private void CollectCapital()
    {
        _capital.Clear();
        var tabs = TabHotkeys.Instance;
        if (tabs == null) return;
        foreach (var canvas in tabs.CapitalCanvases())
        {
            var group = canvas.GetComponent<CanvasGroup>();
            if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
            _capital.Add((group, group.alpha, group.blocksRaycasts));
        }
        var overlay = tabs.EventOverlay;
        if (overlay != null && _eventGroup == null)
        {
            _eventGroup = overlay.GetComponent<CanvasGroup>();
            _addedEventGroup = _eventGroup == null;
            if (_addedEventGroup) _eventGroup = overlay.AddComponent<CanvasGroup>();
            _eventIgnored = _eventGroup.ignoreParentGroups;
            _eventGroup.ignoreParentGroups = true;
        }
    }

    private void FadeCapital(float visible)
    {
        foreach (var (group, alpha, raycasts) in _capital)
        {
            if (group == null) continue;
            group.alpha = alpha * visible;
            group.blocksRaycasts = raycasts && visible > 0.99f;
        }
    }

    private void RestoreCapital()
    {
        foreach (var (group, alpha, raycasts) in _capital)
        {
            if (group == null) continue;
            group.alpha = alpha;
            group.blocksRaycasts = raycasts;
        }
        _capital.Clear();
        if (_eventGroup != null)
        {
            if (_addedEventGroup) Destroy(_eventGroup);
            else _eventGroup.ignoreParentGroups = _eventIgnored;
            _eventGroup = null;
        }
    }

    // ===== FRAME =====

    private void Update()
    {
        switch (_mode)
        {
            case Mode.Capital:
                WatchCapitalScroll();
                return;
            case Mode.Opening:
            case Mode.Closing:
                Animate();
                break;
            case Mode.World:
                HandleInput();
                break;
        }
        if (_renderer == null || _mode == Mode.Capital) return;
        _renderer.SetCamera(_cx, _cy, _size, true);
        float perPixel = 2f * _size / Mathf.Max(1f, Screen.height);
        float pulse = GameSettings.ReduceMotion ? 0.75f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        _renderer.SetLevel(WorldZoom.Level(_size), perPixel, pulse);
        DrawUnits(pulse);
        DrawBadges();
        DrawTags();
        FollowCursor();
        if (Time.unscaledTime >= _refreshAt) Refresh(force: false);
    }

    // The capital camera hands the zoom over at its widest view (ZoomOutOfCapital). Over the HUD, where that camera
    // ignores the wheel, a notch out at its widest view leaves too (not over a list: that scrolls the list). Without a
    // capital camera, scrolling out anywhere opens the world.
    private void WatchCapitalScroll()
    {
        var camera = CameraMovement.Instance;
        if (camera != null && (!camera.AtWidest || !camera.PointerBlocked)) return;
        float scroll = InputUtils.MouseScrollDelta.y;
        if (scroll > -0.01f || !CanLeaveCapital() || PointerOverScrollable()) return;
        Open();
    }

    // A scroll over a list scrolls the list, not the camera.
    private static bool PointerOverScrollable()
    {
        var events = EventSystem.current;
        if (events == null) return false;
        var data = new PointerEventData(events) { position = InputUtils.MousePosition };
        var hits = new List<RaycastResult>();
        events.RaycastAll(data, hits);
        return hits.Any(h => h.gameObject != null && ExecuteEvents.GetEventHandler<IScrollHandler>(h.gameObject) != null);
    }

    private static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void Animate()
    {
        bool opening = _mode == Mode.Opening;
        _t = GameSettings.ReduceMotion ? 1f : Mathf.Min(1f, _t + Time.unscaledDeltaTime / (opening ? OpenSeconds : CloseSeconds));
        float e = _t * _t * (3f - 2f * _t);
        _cx = Mathf.Lerp(_fromX, _toX, e);
        _cy = Mathf.Lerp(_fromY, _toY, e);
        _size = Mathf.Exp(Mathf.Lerp(Mathf.Log(_fromSize), Mathf.Log(_toSize), e));
        if (opening)
        {
            FadeCapital(1f - Mathf.Clamp01(_t / 0.45f));
            _hud.alpha = Mathf.Clamp01((_t - 0.35f) / 0.65f);
            if (_t >= 1f) _mode = Mode.World;
        }
        else
        {
            _hud.alpha = 1f - Mathf.Clamp01(_t / 0.4f);
            FadeCapital(Mathf.Clamp01((_t - 0.45f) / 0.55f));
            if (_t >= 1f)
            {
                _mode = Mode.Capital;
                RestoreCapital();
                _renderer.SetCamera(_cx, _cy, _size, false);
            }
        }
    }

    // ===== INPUT =====

    private void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 screen = mouse.position.ReadValue();
        bool overUI = PointerOverUI();
        Vector2 point = _renderer.ScreenToWorld(screen);
        _pointer = point;

        float scroll = mouse.scroll.ReadValue().y;
        if (!overUI && Mathf.Abs(scroll) > 0.01f)
        {
            float notches = Mathf.Abs(scroll) > 3f ? Mathf.Sign(scroll) : scroll;
            if (notches > 0f && WorldZoom.AtClosest(_size) && CapitalInView())
            {
                Close();
                return;
            }
            float to = WorldZoom.Step(_size, notches, _farthest);
            WorldZoom.ZoomAbout(_cx, _cy, point.x, point.y, _size, to, out _cx, out _cy);
            _size = to;
            ClampCenter();
        }

        if (KeyBindings.NextIdlePartyPressed) NextIdleUnit();

        // Any button drags; a click without a drag selects (left) or sends the selected unit (right).
        if (!overUI && _pressButton < 0)
        {
            if (mouse.leftButton.wasPressedThisFrame) Press(0, screen);
            else if (mouse.rightButton.wasPressedThisFrame) Press(1, screen);
            else if (mouse.middleButton.wasPressedThisFrame) Press(2, screen);
        }
        if (_pressButton >= 0)
        {
            var button = _pressButton == 0 ? mouse.leftButton : _pressButton == 1 ? mouse.rightButton : mouse.middleButton;
            if (button.isPressed)
            {
                if (!_dragging && (screen - _pressAt).magnitude > DragPixels) _dragging = true;
                if (_dragging)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float perPixel = 2f * _size / Mathf.Max(1f, Screen.height);
                    _cx -= delta.x * perPixel;
                    _cy -= delta.y * perPixel;
                    ClampCenter();
                }
            }
            else
            {
                if (!_dragging)
                {
                    if (_pressButton == 0) Click(point);
                    else if (_pressButton == 1) Order(point);
                }
                _pressButton = -1;
                _dragging = false;
            }
        }

        var world = WorldSystem.Instance;
        var hovered = !overUI && world != null ? world.Map.At(point.x, point.y) : null;
        var coord = hovered?.coord;
        HexCoord? micro = hovered != null ? HexHierarchy.MicroAt(point.x, point.y) : (HexCoord?)null;
        bool hexChanged = micro != _hoveredMicro && WorldZoom.ScaleOf(_size) == WorldScale.Micro;
        _hoveredMicro = micro;
        if (coord != _hovered || hexChanged || Time.unscaledTime >= _hoverAt)
        {
            _hovered = coord;
            _hoverAt = Time.unscaledTime + RefreshSeconds;
            ShowHover(world, hovered);
        }
    }

    private float PerPixel => 2f * _size / Mathf.Max(1f, Screen.height);

    private void Press(int button, Vector2 screen)
    {
        _pressButton = button;
        _pressAt = screen;
        _dragging = false;
        if (_popup != Popup.None) ShowPopup(Popup.None);
    }

    private bool CapitalInView()
    {
        var world = WorldSystem.Instance;
        var capital = world != null ? world.Map.Get(world.Map.Capital) : null;
        return capital != null && _renderer.View.Contains(new Vector2(capital.x, capital.y));
    }

    private void ClampCenter()
    {
        var map = WorldSystem.Instance?.Map;
        if (map == null) return;
        _cx = Mathf.Clamp(_cx, map.minX, map.maxX);
        _cy = Mathf.Clamp(_cy, map.minY, map.maxY);
    }

    private void Click(Vector2 point)
    {
        var world = WorldSystem.Instance;
        var tile = world?.Map.At(point.x, point.y);
        if (tile == null) return;
        // Choosing where to survey: the click picks the meso hex.
        if (_surveyPick && world.UnitById(_selectedUnit) != null)
        {
            Order(point);
            return;
        }
        _surveyPick = false;
        if (WorldZoom.ScaleOf(_size) == WorldScale.Macro)
        {
            Deselect();
            _selectedMacro = WorldMap.MacroOf(tile.coord);
            Refresh(force: true);
            return;
        }
        // A party is selected only by clicking its token; anywhere else in the cell selects the ground (or the cell's
        // settlement or enclave). Clicking a token again walks through the tokens under the cursor (nearest first),
        // then the cell's place. Bare land is selected too (it can be surveyed and claimed).
        float pick = Mathf.Max(0.95f, 10f * PerPixel); // the token: 1.8 across, never under 17 pixels
        var under = world.Map.Units.Where(world.Sees)
            .Select(u => (unit: u, pos: WorldUnits.Position(world.Map, world.Settings.generation, u)))
            .Select(p => (p.unit, d: Vector2.Distance(point, new Vector2(p.pos.x, p.pos.y))))
            .Where(p => p.d <= pick).OrderBy(p => p.d).ThenBy(p => p.unit.id).Select(p => p.unit.id).ToList();
        bool place = tile.settlement >= 0 || tile.enclave >= 0;
        var order = new List<int>(); // unit ids; -1 is the cell's place
        if (under.Count > 0)
        {
            order.AddRange(under);
            if (place) order.Add(-1);
        }
        else if (place) order.Add(-1);
        _selectedMacro = null;
        if (order.Count > 0)
        {
            int current = _selectedUnit >= 0 ? order.IndexOf(_selectedUnit) : _selected == tile.coord ? order.IndexOf(-1) : -1;
            int next = order[(current + 1) % order.Count];
            if (next < 0)
            {
                _selectedUnit = -1;
                _selected = tile.coord;
            }
            else
            {
                _selectedUnit = next;
                _selected = null;
            }
        }
        else if (!tile.water && (tile.revealed || tile.explored))
        {
            _selectedUnit = -1;
            _selected = tile.coord;
        }
        else
        {
            Deselect();
            return;
        }
        // At the micro reading the click also picks the hex (land is claimed hex by hex).
        _selectedHex = WorldZoom.ScaleOf(_size) == WorldScale.Micro ? HexHierarchy.MicroAt(point.x, point.y) : (HexCoord?)null;
        Refresh(force: true);
    }

    // Right click sends the selected unit: at the micro reading to the hex itself, beyond it to the heart of the cell
    // (either way to the nearest ground it can reach). With Shift held, or while choosing where to survey, it surveys
    // the meso hex under the cursor: it walks each of its seven hexes in turn.
    private void Order(Vector2 point)
    {
        var world = WorldSystem.Instance;
        var unit = world?.UnitById(_selectedUnit);
        var tile = world?.Map.At(point.x, point.y);
        // Only your own parties take orders (a band you inspect does not).
        if (unit == null || tile == null || !WorldBattles.IsPlayers(unit)) return;
        bool survey = _surveyPick || (Keyboard.current != null && Keyboard.current.shiftKey.isPressed);
        _surveyPick = false;
        // Right click on a band: give chase (hunt it, or attack it).
        var band = survey ? null : BandAt(world, point);
        if (band != null)
        {
            world.EngageBand(unit, band);
            _preview = default;
            Refresh(force: true);
            return;
        }
        if (survey) world.SurveyCell(unit, tile.coord);
        else if (WorldZoom.ScaleOf(_size) == WorldScale.Micro) world.GoMicro(unit, HexHierarchy.MicroAt(point.x, point.y));
        else world.Go(unit, tile.coord);
        _preview = default;
        Refresh(force: true);
    }

    /// <summary>Select the next unit waiting for orders (the '.' key), and look at it.</summary>
    private void NextIdleUnit()
    {
        var world = WorldSystem.Instance;
        if (world == null || world.Map == null) return;
        var idle = world.Map.Units.Where(WorldUnits.AwaitsOrders).ToList();
        if (idle.Count == 0) return;
        int at = idle.FindIndex(u => u.id == _selectedUnit);
        SelectUnit(idle[(at + 1) % idle.Count].id);
    }

    private void Deselect()
    {
        _surveyPick = false;
        _selectedUnit = -1;
        _selected = null;
        _selectedMacro = null;
        Refresh(force: true);
    }

    private void Focus(float x, float y)
    {
        _cx = x;
        _cy = y;
        ClampCenter();
    }

    private void SetScale(WorldScale scale)
    {
        if (_mode != Mode.World) return;
        _size = WorldZoom.SizeFor(scale, _farthest);
        ClampCenter();
        Refresh(force: true);
    }

    // ===== BUILD =====

    private void Build()
    {
        if (_buildFailed) return;
        var world = WorldSystem.Instance;
        _theme = CodeUI.Theme(nameof(WorldView));
        if (_theme == null || world == null || world.Map == null)
        {
            _buildFailed = true;
            return;
        }
        _renderer = new WorldRenderer();
        if (!_renderer.Build(world.Map, world.Settings.generation, null, world.Rules))
        {
            _renderer = null;
            _buildFailed = true;
            return;
        }
        var map = world.Map;
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        _farthest = WorldZoom.Farthest((map.maxY - map.minY) / 2f, (map.maxX - map.minX) / 2f, aspect);
        BuildHud();
        world.Changed += MarkDirty;
        BattleWindow.Watch(world);
        if (LegendProgress.Instance != null) LegendProgress.Instance.Changed += MarkDirty;
        RumourKeeper.Changed += MarkDirty;
        _built = true;
    }

    private void MarkDirty() => _refreshAt = 0f;

    private void BuildHud()
    {
        _canvas = CodeUI.Canvas(transform, "World View Canvas", 4, out _scaler);
        // Under the event overlay (stories still show over the world), above the capital's hidden canvases.
        _canvas.sortingOrder = -5;
        _canvasRect = (RectTransform)_canvas.transform;
        _hud = _canvas.gameObject.AddComponent<CanvasGroup>();
        _hud.alpha = 0f;
        _hud.blocksRaycasts = _hud.interactable = false;

        // The plans' badges (a survey's order, when society settles a hex), under the parties' tags.
        _badgeLayer = CodeUI.Panel(_canvas.transform, "Plan Badges", Vector2.zero, Vector2.one);
        _badgeLayer.pivot = new Vector2(0.5f, 0.5f);

        // What your parties are busy with, over their tokens (under every other part of the HUD).
        _tagLayer = CodeUI.Panel(_canvas.transform, "Unit Tags", Vector2.zero, Vector2.one);
        _tagLayer.pivot = new Vector2(0.5f, 0.5f);

        BuildKey();
        BuildDock();
        BuildPopups();

        // The card under the cursor (never catches clicks).
        _hoverCard = CodeUI.Panel(_canvas.transform, "Hover", Vector2.zero, Vector2.zero);
        _hoverCard.pivot = new Vector2(0f, 1f);
        _hoverCard.sizeDelta = new Vector2(HoverWidth, 120f);
        CodeUI.Plate(_hoverCard, _theme, _scaler, 0.93f);
        foreach (var g in _hoverCard.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        _hoverText = CodeUI.Label(_hoverCard, "Text", string.Empty, _theme.bodySize - 3f, _theme.bodyColor, FontStyles.Normal, _theme);
        _hoverText.lineSpacing = TooltipText.LineSpacing;
        _hoverText.alignment = TextAlignmentOptions.TopLeft;
        CodeUI.Place(_hoverText.rectTransform, Vector2.zero, Vector2.one, new Vector2(Inner, 12f), new Vector2(-Inner, -12f));
        _hoverCard.gameObject.SetActive(false);

        // The selection card, lower right.
        _card = CodeUI.Panel(_canvas.transform, "Selection", new Vector2(1f, 0f), new Vector2(1f, 0f));
        _card.pivot = new Vector2(1f, 0f);
        _card.sizeDelta = new Vector2(CardWidth, 300f);
        _card.anchoredPosition = new Vector2(-Pad, Pad);
        CodeUI.Plate(_card, _theme, _scaler, 0.92f);
        _selectionList = new HudList(_card, _theme);
        _card.gameObject.SetActive(false);
    }

    // The map key, upper left: the reading and the lens in force, what the lens's colours mean, and how to drive the map.
    private void BuildKey()
    {
        // The map guide has one button; the nation's report is available from the action bar.
        _key = CodeUI.Panel(_canvas.transform, "Map Key", new Vector2(0f, 1f), new Vector2(0f, 1f));
        _key.pivot = new Vector2(0f, 1f);
        _key.anchoredPosition = new Vector2(Pad, -Pad);
        _key.sizeDelta = new Vector2(56f, 56f);
        _guide = IconActionButton.Create(_key, "Map guide", PixelIcon.Help, () => ShowPopup(Popup.Map), "Map controls and the active lens.", _theme);
        _guide.Rect.anchorMin = _guide.Rect.anchorMax = new Vector2(.5f, .5f);
        _guide.Rect.anchoredPosition = Vector2.zero;
    }

    private IconActionButton DockButton(RectTransform dock, string name, Action onClick, string title, string tip, HudIcon icon)
    {
        var glyph = name == "Rumours" ? PixelIcon.Rumour : name == "Chronicle" ? PixelIcon.Chronicle : PixelFor(icon);
        var button = _actionBar.Add(name, glyph, onClick, tip, _theme, name == "Map" || name == "Parties");
        _dockButtons.Add(button);
        TooltipTrigger.Ensure(button.gameObject).SetCustom(title, tip);
        return button;
    }

    // Five doors, the same on every screen size: the map (lens, layers, reading), the parties, the realm, the Chronicle, home.
    private void BuildDock()
    {
        _actionBar = QuickActionBar.Create(_canvas.transform, _theme, _scaler);
        var dock = _actionBar.Rect;
        _dock = dock;
        dock.pivot = Vector2.zero;
        dock.sizeDelta = new Vector2(DockWidth, 62f);
        dock.anchoredPosition = new Vector2(Pad, Pad);
        _mapDock = DockButton(dock, "Map", () => ShowPopup(Popup.Map), "Map",
            "How the map reads: one lens at a time (Settle, fertility, Coherence and leylines, magic, authority, trade, danger...), the layers drawn over it, and the reading (micro, meso, macro).", HudIcon.Compass);
        _unitsDock = DockButton(dock, "Parties", () => ShowPopup(Popup.Units), "Parties",
            "Every party in the field: its captain, where it is and how it fares. Select one to find it on the map. Form expeditions at a settlement.", HudIcon.Crew);
        _actionBar.Add("Next idle party", PixelIcon.Idle, NextIdleUnit, "Select the next party waiting for orders. Its actions appear beside the map.", _theme, true);
        _realmDock = DockButton(dock, "Realm", () => ShowPopup(Popup.Realm), "Realm",
            "Administrative Capacity, territorial pull and the border policy: how much land your administration can hold, and whether growing wider or taller pays now.", HudIcon.Crown);
        DockButton(dock, "Rumours", RumoursWindow.Toggle, "Rumours",
            "What travellers, hunters and pilgrims speak of: a direction from the capital and a vague word of what is there. The map marks the area each points at; go there to find out. Finding what a rumour spoke of earns Era Score.", HudIcon.Compass);
        DockButton(dock, "Chronicle", EraTimelineWindow.Toggle, "Chronicle",
            "Every award of Era Score since the world began, oldest first, dated to its Cycle, Echo, Phase and Seventh and its Act of Fate.", HudIcon.Pin);
        DockButton(dock, "Capital", Close, "Return to the capital", "Back to the capital (Esc, or scroll in over it at the closest zoom).", HudIcon.Home);
        _actionBar.Add("Culture", PixelIcon.Culture, () => CultureAtlasWindow.Open(), "Traditions, cultural choices and history.", _theme);
        _actionBar.Add("Nation", PixelIcon.Crown, CultureWindow.Toggle, "Your nation, its people, culture and character.", _theme);
        _actionBar.Add("Ballads & Bonds", PixelIcon.Music, BalladJournalView.ToggleJournal, "Ballads and relationships between legends.", _theme);
        _actionBar.Add("Bestiary", PixelIcon.Beast, BestiaryWindow.Toggle, "Identified creatures and their known habitats.", _theme, visible: () => BestiaryHud.Unlocked);
        _actionBar.Add("Library", PixelIcon.Book, LibraryWindow.Toggle, "Browse the White-Haven Library (L).", _theme);
        _actionBar.Reflow();
    }

    private RectTransform PopupPanel(string name, Vector2 size)
    {
        var panel = CodeUI.Panel(_canvas.transform, name, Vector2.zero, Vector2.zero);
        panel.pivot = Vector2.zero;
        panel.sizeDelta = size;
        panel.anchoredPosition = new Vector2(Pad, Pad + 70f);
        CodeUI.Plate(panel, _theme, _scaler, 0.94f);
        panel.gameObject.SetActive(false);
        return panel;
    }

    private void BuildPopups()
    {
        foreach (var kind in new[] { Popup.Map, Popup.Units, Popup.Realm })
        {
            var panel = PopupPanel(kind + " Panel", new Vector2(460f, 520f));
            _popups[kind] = panel;
            _menuLists[kind] = new HudList(panel, _theme);
        }
        _rosterList = _menuLists[Popup.Units];
    }
    private void ShowPopup(Popup popup)
    {
        _popup = _popup == popup ? Popup.None : popup;
        foreach (var pair in _popups) pair.Value.gameObject.SetActive(pair.Key == _popup);
        Refresh(force: true);
    }

    /// <summary>Select a unit and bring it into view (while the world is still opening, the camera heads for it).</summary>
    private void SelectUnit(int id)
    {
        var world = WorldSystem.Instance;
        var unit = world?.UnitById(id);
        if (unit == null) return;
        _selectedUnit = unit.id;
        _selected = null;
        _selectedMacro = null;
        if (unit.Missing) { Refresh(force: true); return; }
        var (x, y) = WorldUnits.Position(world.Map, world.Settings.generation, unit);
        if (_mode == Mode.Opening)
        {
            _toX = x;
            _toY = y;
        }
        else
        {
            Focus(x, y);
            if (WorldZoom.ScaleOf(_size) == WorldScale.Macro) _size = WorldZoom.SizeFor(WorldScale.Meso, _farthest);
        }
        Refresh(force: true);
    }

    // ===== LENSES AND LAYERS =====

    private void SetLens(WorldLens lens)
    {
        if (WorldLenses.ShowsLeylines(lens) && !_leylines)
        {
            _leylines = true;
            ApplyLayers();
        }
        _renderer.SetLens(lens, LensField(lens), Highlights(lens));
        _renderer.RefreshCivilization();
        Refresh(force: true);
    }

    // The Settle field and the foundable cells, recomputed only when the world changed since the last time.
    private float[] LensField(WorldLens lens)
    {
        var world = WorldSystem.Instance;
        if (lens != WorldLens.Settle || world == null || world.Map == null) return _potential;
        var map = world.Map;
        int stamp = map.CivilizationVersion * 7919 + (map.Magic?.Version ?? 0) * 31 + map.KnowledgeVersion;
        if (stamp == _potentialStamp && _potential != null) return _potential;
        _potentialStamp = stamp;
        _potential = CityDevelopment.PotentialField(map, world.Rules);
        _foundable = new HashSet<int>(map.Tiles.Where(t => t.authorityId == WorldAuthority.Player && t.explored
            && WorldCivilization.WhyNotFound(map, world.Settings.generation, world.Rules, t.coord, SettlementKind.Town) == null).Select(t => t.index));
        return _potential;
    }

    // The cells a lens draws bright: where a town could be founded (Settle), what society adopts next (Territory),
    // where a tributary could be raised (Desirability).
    private ICollection<int> Highlights(WorldLens lens)
    {
        var world = WorldSystem.Instance;
        if (lens == WorldLens.Desirability) return world != null && world.Map != null ? world.TributarySites() : new HashSet<int>();
        if (lens != WorldLens.Territory) return _foundable;
        _nextAdoptions = world != null ? new HashSet<int>(world.NextAdoptions(5).Select(c => c.cell)) : new HashSet<int>();
        return _nextAdoptions;
    }

    private void ApplyLayers()
    {
        var world = WorldSystem.Instance;
        _renderer.SetLayers(_rivers, _leylines, _forecast && world != null ? world.ForecastNextAge() : null, _previous);
        Refresh(force: true);
    }

    // ===== UNITS ON THE MAP =====

    // A party's mark by its role; an enemy band's by what it is (WorldMarks.shader: 8 paw, 9 Atonalis burst, 10 shield,
    // 11 horned head, 12 hooded figure).
    private static float ShapeOf(WorldUnit unit, UnitSpec spec) => !WorldBattles.IsPlayers(unit) ? BandShape(unit)
        : spec == null ? 1f : spec.role == UnitRole.Scout ? 3f : spec.role == UnitRole.Builder ? 4f : spec.role == UnitRole.Expedition ? 2f : 1f;

    // 8 paw, 9 Atonalis burst, 10 shield, 11 horned head, 12 hooded figure; 13 a Formless Mass, 14 a creature of the water, 15 a cocoon.
    private static float BandShape(WorldUnit band) => band.bandActivity == BandActivity.Cocooned ? 15f : band.identity == BandIdentity.FormlessMass ? 13f
        : band.identity == BandIdentity.Creature && band.habitat == CreatureHabitat.Water ? 14f : band.identity <= BandIdentity.Humanoid ? 8f + (int)band.identity : 8f;

    // The band you can see whose token lies under a map point (nearest first), or null.
    private WorldUnit BandAt(WorldSystem world, Vector2 point)
    {
        if (world?.Map == null) return null;
        float pick = Mathf.Max(0.95f, 10f * PerPixel);
        var gen = world.Settings.generation;
        return world.Map.Units.Where(u => !WorldBattles.IsPlayers(u) && world.Sees(u))
            .Select(u => (unit: u, pos: WorldUnits.Position(world.Map, gen, u)))
            .Select(p => (p.unit, d: Vector2.Distance(point, new Vector2(p.pos.x, p.pos.y))))
            .Where(p => p.d <= pick).OrderBy(p => p.d).ThenBy(p => p.unit.id).Select(p => p.unit).FirstOrDefault();
    }

    // The legends of an expedition whose Composure is Spiraling or worse (a party in distress).
    private static List<string> Spiraling(WorldUnit unit)
    {
        var legends = LegendProgress.Instance;
        return legends == null ? new List<string>() : Expeditions.Members(unit).Where(n => legends.Composure(n) >= ComposureState.Spiraling).ToList();
    }

    private void DrawUnits(float pulse)
    {
        var world = WorldSystem.Instance;
        if (world == null || world.Map == null) return;
        var map = world.Map;
        var gen = world.Settings.generation;
        _marks.Clear();
        _path.Clear();
        Color? pathColor = null;
        HexCoord? destination = null;
        foreach (var unit in map.Units)
        {
            // Bands show only while your people can see them.
            if (!world.Sees(unit)) continue;
            var spec = world.SpecOf(unit);
            var (x, y) = WorldUnits.Position(map, gen, unit);
            bool selected = unit.id == _selectedUnit;
            bool band = !WorldBattles.IsPlayers(unit);
            var color = spec != null ? spec.color : Color.white;
            if (unit.Working) color = Color.Lerp(color, Color.white, 0.4f * pulse);
            bool distress = !band && (unit.hungry || unit.attrition >= 65f || Spiraling(unit).Count > 0);
            // Whom it chases (a band after its quarry, or one of your parties giving chase), drawn as a line to it.
            var quarry = unit.quarryId >= 0 ? world.UnitById(unit.quarryId) : null;
            bool chasing = quarry != null && world.Sees(quarry) && (!band || unit.bandActivity == BandActivity.Pursuing || unit.bandActivity == BandActivity.Stalking);
            Vector2 chaseTo = default;
            if (chasing) { var (qx, qy) = WorldUnits.Position(map, gen, quarry); chaseTo = new Vector2(qx, qy); }
            var chaseColor = !band ? new Color(1f, 0.9f, 0.6f, 0.75f) : WorldBattles.IsPlayers(quarry) ? new Color(0.95f, 0.2f, 0.15f, 0.8f) : BandTone(unit, 0.5f);
            bool showEndurance = unit.winded || unit.endurance < 99.5f || WorldPursuit.Running(unit);
            _marks.Add(new WorldRenderer.UnitMark
            {
                position = new Vector2(x, y), color = color, shape = ShapeOf(unit, spec), selected = selected, camping = unit.Camping, distress = distress,
                band = band, alert = band && chasing && WorldBattles.IsPlayers(quarry), chasing = chasing, chaseTo = chaseTo, chaseColor = chaseColor,
                endurance = showEndurance ? Mathf.Clamp01(unit.endurance / 100f) : -1f, winded = unit.winded,
            });
            if (selected && unit.Moving)
            {
                // The road ahead, hex by hex; amber when its rations will not last the way.
                _path.Add(new Vector2(x, y));
                foreach (var c in unit.path) _path.Add(Pixel(c));
                destination = unit.path[unit.path.Count - 1];
                if (spec != null && spec.supplyUsePerSeventh > 0f && WorldUnits.RationsAhead(map, gen, unit, spec) > unit.supplies + 1e-3f) pathColor = WorldRenderer.HungryPathColor;
            }
        }
        // A preview of where a right click would send the selected unit.
        var chosen = world.UnitById(_selectedUnit);
        // Over a band the right click gives chase instead (the hover card says so).
        if (_path.Count == 0 && chosen != null && WorldBattles.IsPlayers(chosen) && _mode == Mode.World && !PointerOverUI() && BandAt(world, _pointer) == null)
        {
            var preview = PreviewFor(world, chosen);
            if (preview.valid && preview.why == null)
            {
                var spec = world.SpecOf(chosen);
                var (x, y) = WorldUnits.Position(map, gen, chosen);
                _path.Add(new Vector2(x, y));
                foreach (int id in preview.path) _path.Add(Pixel(MicroNavigation.Coord(map, id)));
                destination = preview.reached;
                if (spec != null && spec.supplyUsePerSeventh > 0f && WorldUnits.RationsAway(map, gen, chosen, spec, preview.path) > chosen.supplies + 1e-3f) pathColor = WorldRenderer.HungryPathColor;
            }
        }
        CollectPlans(world, pulse);
        _renderer.SetUnits(_marks, _path, pulse, pathColor, _plans, _planLines);
        bool hexes =WorldZoom.ScaleOf(_size) == WorldScale.Micro && _mode == Mode.World && !PointerOverUI();
        _renderer.SetMicroHighlight(hexes ? _hoveredMicro : null, destination);
    }

    // ===== PLANS ON THE MAP =====

    // Screen pixels between two neighbouring micro hexes below which their badges would crowd: one badge per cell instead.
    private const float BadgeSpacing = 34f;
    // The same for the wider plates saying when society settles each hex (closer, only the next hex has one).
    private const float PlateSpacing = 52f;
    // A survey's number sits over the top of its hex, clear of a party standing on it.
    private static readonly Vector2 BadgeLift = new Vector2(0f, 0.7f);

    // What your people mean to do, drawn under the units at the local and region readings, like the research plan in
    // the tech tree: each survey's hexes still to walk, numbered in its order (gold on the one under way, violet after)
    // with its tour drawn from the party; while picking a cell to survey, the tour it would walk there; and the hexes
    // society settles next by itself, glowing in your border's colour with about when each joins.
    private void CollectPlans(WorldSystem world, float pulse)
    {
        _plans.Clear();
        _planLines.Clear();
        _badgeWants.Clear();
        if (_mode != Mode.World || WorldZoom.ScaleOf(_size) == WorldScale.Macro) return;
        var map = world.Map;
        float spacing = 1.73f / Mathf.Max(1e-4f, PerPixel);
        bool each = spacing >= BadgeSpacing;
        foreach (var unit in map.Units)
        {
            if (!WorldBattles.IsPlayers(unit) || unit.Missing || (!unit.surveying && !unit.surveyPaused)) continue;
            bool selected = unit.id == _selectedUnit;
            AddSurveyPlan(world, unit, SurveyPlanOf(world, unit), selected ? 1f : 0.7f, unit.surveying, selected || unit.surveying, pulse, each);
        }
        var picking = _surveyPick ? world.UnitById(_selectedUnit) : null;
        if (picking != null && _hovered.HasValue && world.WhyNotSurveyCell(picking, _hovered.Value) == null)
            AddSurveyPlan(world, picking, PickPlan(world, picking, _hovered.Value), 0.55f, false, true, pulse, each);
        AddAdoptionPlans(world, pulse, spacing);
    }

    // A survey's tour: a ring on every hex still to survey and its number in the order walked, the tour's way from the party.
    private void AddSurveyPlan(WorldSystem world, WorldUnit unit, List<HexCoord> plan, float alpha, bool active, bool line, float pulse, bool each)
    {
        if (plan == null || plan.Count == 0) return;
        var (x, y) = WorldUnits.Position(world.Map, world.Settings.generation, unit);
        var points = line ? new List<Vector2> { new Vector2(x, y) } : null;
        for (int i = 0; i < plan.Count; i++)
        {
            var at = Pixel(plan[i]);
            points?.Add(at);
            bool now = i == 0 && active;
            var tone = now ? WorldRenderer.PlanActiveColor : WorldRenderer.PlanColor;
            tone.a = alpha * (now ? 0.55f + 0.45f * pulse : 0.85f);
            _plans.Add(new WorldRenderer.PlanMark { position = at, shape = 2f, color = tone, worldSize = 1.3f, minPixels = 9f });
            if (each) _badgeWants.Add(new BadgeWant { at = at + BadgeLift, text = (i + 1).ToString(), rim = now ? WorldRenderer.PlanActiveColor : WorldRenderer.PlanColor, alpha = alpha, rhombus = true });
        }
        if (points != null && points.Count > 1)
        {
            var tone = WorldRenderer.PlanColor;
            tone.a = 0.7f * alpha;
            _planLines.Add(new WorldRenderer.PlanLine { points = points, color = tone });
        }
    }

    // The hexes society settles next: a glow on each (the next one pulsing) and, up close, about when each joins; at
    // the region reading one plate per cell says when all of it will be yours.
    private void AddAdoptionPlans(WorldSystem world, float pulse, float spacing)
    {
        var border = world.Rules.borderColor;
        foreach (var p in world.AdoptionForecast())
        {
            if (p.hexes.Count == 0) continue;
            for (int i = 0; i < p.hexes.Count; i++)
            {
                var at = Pixel(MicroNavigation.Coord(world.Map, p.hexes[i]));
                var glow = border;
                glow.a = i == 0 ? 0.3f + 0.35f * pulse : 0.22f;
                _plans.Add(new WorldRenderer.PlanMark { position = at, shape = 7f, color = glow, worldSize = 1.7f, minPixels = 12f });
                var ring = border;
                ring.a = i == 0 ? 0.95f : 0.6f;
                _plans.Add(new WorldRenderer.PlanMark { position = at, shape = 2f, color = ring, worldSize = 1.05f, minPixels = 7f });
                if (spacing >= PlateSpacing || (i == 0 && spacing >= BadgeSpacing)) _badgeWants.Add(new BadgeWant { at = at, text = $"~{p.sevenths[i]:0.#}", rim = border, alpha = i == 0 ? 1f : 0.8f });
            }
            if (spacing < BadgeSpacing)
            {
                var t = world.Map[p.cell];
                _badgeWants.Add(new BadgeWant { at = new Vector2(t.x, t.y), text = $"Settling · all ~{p.WholeCell:0.#} Sevenths", rim = border, alpha = 0.9f });
            }
        }
    }

    // The survey tour a party is on, planned again only when something it depends on changed.
    private List<HexCoord> SurveyPlanOf(WorldSystem world, WorldUnit unit)
    {
        var stamp = StampFor(world, unit, unit.surveyCell);
        if (_surveyPlans.TryGetValue(unit.id, out var known) && known.stamp.Equals(stamp)) return known.plan;
        var plan = world.SurveyPlan(unit);
        _surveyPlans[unit.id] = (stamp, plan);
        if (_surveyPlans.Count > 64)
            foreach (int gone in _surveyPlans.Keys.Where(id => world.UnitById(id) == null).ToList()) _surveyPlans.Remove(gone);
        return plan;
    }

    // The tour the picking party would walk in the hovered cell.
    private List<HexCoord> PickPlan(WorldSystem world, WorldUnit unit, HexCoord cell)
    {
        var stamp = StampFor(world, unit, cell);
        if (_pickPlan.plan != null && _pickPlan.stamp.Equals(stamp)) return _pickPlan.plan;
        _pickPlan = (stamp, world.SurveyPlan(unit, cell));
        return _pickPlan.plan;
    }

    private static PlanStamp StampFor(WorldSystem world, WorldUnit unit, HexCoord cell)
    {
        var map = world.Map;
        var tile = map.Get(cell);
        return new PlanStamp(unit.id, WorldUnits.MicroPosition(unit), cell, tile != null ? tile.microSurveyMask : 0, map.CivilizationVersion, map.KnowledgeVersion);
    }

    // The badges wanted this frame, placed over their hexes (pooled; hidden past the screen's edge).
    private void DrawBadges()
    {
        if (_badgeLayer == null) return;
        int used = 0;
        foreach (var want in _badgeWants)
        {
            if (!_renderer.WorldToScreen(want.at, out var screen)) continue;
            if (screen.x < -60f || screen.x > Screen.width + 60f || screen.y < -30f || screen.y > Screen.height + 30f) continue;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_badgeLayer, screen, null, out var local)) continue;
            var badge = used < _badges.Count ? _badges[used] : NewBadge();
            used++;
            if (!badge.rect.gameObject.activeSelf) badge.rect.gameObject.SetActive(true);
            if (badge.text != want.text || badge.rhombus != want.rhombus) ShapeBadge(badge, want.text, want.rhombus);
            badge.rim.color = want.rim;
            badge.group.alpha = want.alpha;
            badge.rect.anchoredPosition = local;
        }
        for (int i = used; i < _badges.Count; i++)
            if (_badges[i].rect.gameObject.activeSelf) _badges[i].rect.gameObject.SetActive(false);
    }

    private PlanBadge NewBadge()
    {
        var rect = CodeUI.Panel(_badgeLayer, "Badge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.pivot = new Vector2(0.5f, 0.5f);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        var rim = CodeUI.Solid(rect, "Rim", WorldRenderer.PlanColor);
        var face = CodeUI.Solid(rect, "Face", new Color(0.11f, 0.08f, 0.06f, 0.94f));
        var label = CodeUI.Label(rect, "Text", string.Empty, _theme.bodySize - 5f, new Color(1f, 0.95f, 0.84f, 1f), FontStyles.Bold, _theme);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        CodeUI.Stretch(label.rectTransform);
        var badge = new PlanBadge { rect = rect, rimRect = rim.rectTransform, faceRect = face.rectTransform, rim = rim, face = face, group = group, label = label };
        _badges.Add(badge);
        return badge;
    }

    // A rhombus around a number, like the research plan's badge; a plate around words.
    private void ShapeBadge(PlanBadge badge, string text, bool rhombus)
    {
        badge.text = text;
        badge.rhombus = rhombus;
        badge.label.text = text;
        badge.label.fontSize = rhombus ? _theme.bodySize - 5f : _theme.bodySize - 7f;
        foreach (var part in new[] { badge.rimRect, badge.faceRect })
        {
            part.anchorMin = part.anchorMax = new Vector2(0.5f, 0.5f);
            part.pivot = new Vector2(0.5f, 0.5f);
            part.anchoredPosition = Vector2.zero;
            part.localRotation = rhombus ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
        }
        if (rhombus)
        {
            badge.rect.sizeDelta = new Vector2(26f, 26f);
            badge.rimRect.sizeDelta = new Vector2(19f, 19f);
            badge.faceRect.sizeDelta = new Vector2(15f, 15f);
        }
        else
        {
            var size = badge.label.GetPreferredValues(text);
            var plate = new Vector2(Mathf.Ceil(size.x) + 10f, Mathf.Ceil(size.y) + 4f);
            badge.rect.sizeDelta = plate;
            badge.rimRect.sizeDelta = plate;
            badge.faceRect.sizeDelta = plate - new Vector2(3f, 3f);
        }
    }

    // Over each of your parties at work, what it is doing and how far along it is ("Surveying 43%"), so a party busy
    // with a long survey never looks idle. Shown at the local and region readings, not on the atlas.
    private void DrawTags()
    {
        if (_tagLayer == null) return;
        var world = WorldSystem.Instance;
        int used = 0;
        if (world != null && world.Map != null && _mode == Mode.World && WorldZoom.ScaleOf(_size) != WorldScale.Macro)
        {
            var map = world.Map;
            var gen = world.Settings.generation;
            float lift = Mathf.Max(20f, 1.15f / Mathf.Max(1e-4f, PerPixel));
            foreach (var unit in map.Units)
            {
                if (!WorldBattles.IsPlayers(unit) || unit.Missing) continue;
                string text = TagFor(world, unit, out float progress);
                if (text == null) continue;
                var (x, y) = WorldUnits.Position(map, gen, unit);
                if (!_renderer.WorldToScreen(new Vector2(x, y), out var screen)) continue;
                screen.y += lift;
                if (screen.x < -80f || screen.x > Screen.width + 80f || screen.y < -40f || screen.y > Screen.height + 40f) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_tagLayer, screen, null, out var local)) continue;
                var tag = used < _tags.Count ? _tags[used] : NewTag();
                used++;
                tag.rect.gameObject.SetActive(true);
                if (tag.text != text)
                {
                    tag.text = text;
                    tag.label.text = text;
                    var size = tag.label.GetPreferredValues(text);
                    tag.rect.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 14f, Mathf.Ceil(size.y) + 7f);
                }
                if (!Mathf.Approximately(tag.progress, progress))
                {
                    tag.progress = progress;
                    tag.fill.gameObject.SetActive(progress >= 0f);
                    tag.fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 0f);
                }
                tag.rect.anchoredPosition = local;
            }
        }
        for (int i = used; i < _tags.Count; i++)
            if (_tags[i].rect.gameObject.activeSelf) _tags[i].rect.gameObject.SetActive(false);
    }

    private UnitTag NewTag()
    {
        var rect = CodeUI.Panel(_tagLayer, "Tag", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.pivot = new Vector2(0.5f, 0f);
        var plate = rect.gameObject.AddComponent<Image>();
        plate.color = Hud.Slot;
        plate.raycastTarget = false;
        var fill = CodeUI.Solid(rect, "Progress", Hud.Brass);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 0f);
        fill.rectTransform.offsetMin = new Vector2(0f, 0f);
        fill.rectTransform.offsetMax = new Vector2(0f, 2f);
        var label = CodeUI.Label(rect, "Text", string.Empty, _theme.bodySize - 6f, Hud.Brass, FontStyles.Normal, _theme);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 3f), new Vector2(-4f, -2f));
        var tag = new UnitTag { rect = rect, fill = fill.rectTransform, label = label };
        _tags.Add(tag);
        return tag;
    }

    // What the tag over a party says, or null for none; progress 0-1 fills its bar (-1: no bar).
    private static string TagFor(WorldSystem world, WorldUnit unit, out float progress)
    {
        progress = -1f;
        if (unit.surveying)
        {
            progress = world.SurveyProgress(unit);
            string done = $"{Mathf.FloorToInt(progress * 100f)}%";
            if (unit.retreating && unit.Moving) return $"Retreating · survey {done}";
            if (unit.Camping) return $"Resting · survey {done}";
            if (unit.returning && unit.Moving) return $"Resupplying · survey {done}";
            if (unit.surveyWait > 0f) return $"Surveying {done} · waiting";
            return $"Surveying {done}";
        }
        if (unit.Working)
        {
            var ability = UnitAbilities.For(unit.task);
            if (ability == null) return null;
            progress = WorldUnits.WorkProgress(unit);
            string doing = string.IsNullOrEmpty(ability.doing) ? ability.name : ability.doing;
            return $"{char.ToUpperInvariant(doing[0])}{doing.Substring(1)} {Mathf.FloorToInt(progress * 100f)}%";
        }
        if (unit.autoSurvey && !unit.Moving) return "Surveying by itself";
        if (unit.surveyPaused && !unit.Moving && !unit.Camping)
        {
            progress = world.SurveyProgress(unit);
            return $"Survey paused {Mathf.FloorToInt(progress * 100f)}%";
        }
        return null;
    }

    private static Vector2 Pixel(HexCoord micro)
    {
        micro.ToPixel(1f, out float x, out float y);
        return new Vector2(x, y);
    }

    // The way a right click would send the selected unit now: to the hovered hex at the micro reading, to the
    // hovered cell's heart beyond it. Searched again only when the unit, the target or the land changed, and bounded,
    // so hovering never stalls a frame.
    private Preview PreviewFor(WorldSystem world, WorldUnit unit)
    {
        bool micro = WorldZoom.ScaleOf(_size) == WorldScale.Micro;
        var target = micro ? _hoveredMicro : _hovered;
        if (!target.HasValue || unit == null) return default;
        var from = WorldUnits.MicroPosition(unit);
        int stamp = (world.Map.CivilizationVersion * 31 + (world.Map.Magic?.Version ?? 0)) * 31 + (CelestialWeatherSystemLogic.Instance?.RegionalWeatherVersion ?? 0);
        if (_preview.valid && _preview.unit == unit.id && _preview.micro == micro && _preview.target == target.Value && _preview.from == from && _preview.stamp == stamp)
            return _preview;
        var p = new Preview { valid = true, unit = unit.id, micro = micro, target = target.Value, from = from, stamp = stamp };
        p.why = micro ? world.WhyNotGoMicro(unit, target.Value, out p.path, out p.fatigue, out p.reached, PreviewVisits)
            : world.WhyNotGo(unit, target.Value, out p.path, out p.fatigue, out p.reached, PreviewVisits);
        if (p.why == WorldSystem.NoWay && world.CanReach(unit, target.Value, micro)) p.why = FarAway;
        _preview = p;
        return p;
    }

    // ===== THE CARD UNDER THE CURSOR =====

    private void ShowHover(WorldSystem world, WorldTile tile)
    {
        if (tile == null || world == null || _mode != Mode.World)
        {
            _hoverCard.gameObject.SetActive(false);
            return;
        }
        var preview = new StringBuilder();
        // A band under the pointer comes first: what it is, what it means to you, and what a right click would do.
        var band = BandAt(world, _pointer);
        if (band != null)
        {
            preview.AppendLine(BandTitle(world, band));
            preview.AppendLine($"{BandStanceWord(band)}  {TooltipText.Muted("·")}  {WorldPursuit.Status(band)}");
            preview.AppendLine($"Endurance {WorldPursuit.Bar(band)}");
            var hunter = world.UnitById(_selectedUnit);
            if (hunter != null && WorldBattles.IsPlayers(hunter))
            {
                string why = world.WhyNotEngage(hunter, band);
                preview.AppendLine(why == null
                    ? TooltipText.Good($"Right click: {hunter.name} {(WorldBattles.Angry(band) ? "attacks" : "gives chase to")} it")
                    : TooltipText.Warn($"{hunter.name}: {why}"));
            }
            else preview.AppendLine(TooltipText.Muted("Click it for its full report; select a party and right click it to give chase."));
            preview.AppendLine();
        }
        preview.AppendLine(TooltipText.Heading(tile.known ? world.Place(tile) : tile.revealed ? "Unknown wilderness" : "The Fog"));
        string lensLines = LensLines(world, tile);
        if (lensLines != null) preview.AppendLine(lensLines);
        preview.AppendLine(SurveyState(world, tile));
        string settling = SettlingState(world, tile);
        if (settling != null) preview.AppendLine(settling);
        if (tile.known) preview.AppendLine($"Fertility {tile.landFertility:P0}{(tile.danger > 0.01f ? $" | Hazard {tile.danger:P0}" : string.Empty)}");
        if (tile.known && tile.signs > 0.05f) preview.AppendLine(TooltipText.Warn("Fresh signs of a hunter: something hunts nearby"));
        if (tile.known)
        {
            var air = WorldSuffering.Feelings(tile);
            var held = air.Total > 0.05f ? EmotionalAlchemy.Detect(air, 0.7f, 1) : null;
            if (held != null && held.Count > 0) preview.AppendLine(TooltipText.Muted($"The air holds {held[0].compound.name}: {held[0].compound.gloss}"));
        }
        if (tile.known && tile.suffering > 0.05f) preview.AppendLine($"The land here suffers ({tile.suffering:0.##}){(WorldSuffering.Pressure(tile, tile.suffering) >= WorldSuffering.SpawnPressure ? TooltipText.Warn(": a Formless Mass may pool") : string.Empty)}");
        var picking = _surveyPick ? world.UnitById(_selectedUnit) : null;
        if (picking != null)
        {
            string why = world.WhyNotSurveyCell(picking, tile.coord);
            preview.AppendLine(why == null
                ? TooltipText.Good($"Click: {picking.name} surveys it (walks its {Hexes(world.HexesToSurvey(tile))}, about {Sevenths(world.SurveySevenths(picking, tile.coord))} once there)")
                : TooltipText.Warn($"{picking.name}: {why}"));
        }
        else AppendTravel(world, tile, preview);
        preview.AppendLine(TooltipText.Muted("Select this ground to open its full report."));
        _hoverText.text = FlowText(preview.ToString());
        float width = Mathf.Min(HoverWidth, _canvasRect.rect.width - 2f * Pad);
        float height = _hoverText.GetPreferredValues(_hoverText.text, width - 2f * Inner, 0f).y + 26f;
        _hoverCard.sizeDelta = new Vector2(width, height);
        _hoverCard.gameObject.SetActive(true);
        FollowCursor();
    }

    // The lens in force on a cell, first in its hover: the colour under the cursor as a swatch, what that colour means,
    // and what the cell gives in that lens (WorldLenses.Hover). Nothing under the Landscape; the Fog hides every lens.
    private string LensLines(WorldSystem world, WorldTile tile)
    {
        var lens = _renderer.Lens;
        if (lens == WorldLens.Normal || tile == null) return null;
        if (!tile.known && !tile.revealed) return TooltipText.Muted($"{WorldLenses.Name(lens)}: hidden in the Fog");
        string band = _renderer.LensAt(tile, out var color);
        string swatch = color.a > 0.01f ? WorldRenderer.Swatch(color) + " " : string.Empty;
        string head = swatch + TooltipText.Heading(WorldLenses.Name(lens)) + (band != null ? ": " + TooltipText.Value(band) : string.Empty);
        string reading = WorldLenses.Hover(lens, world.Map, tile, world.Rules, _potential, world.Settings.generation);
        return string.IsNullOrEmpty(reading) || reading == band ? head : head + "\n" + TooltipText.Muted(reading);
    }

    // How well a cell is known: seen hex by hex until explored in passing, then surveyed hex by hex.
    private static string SurveyState(WorldSystem world, WorldTile tile)
    {
        if (!tile.known) return tile.revealed ? "Seen from afar: send a party to walk it." : "Send a party to reveal this ground.";
        if (tile.water) return "Known waters";
        int open = WorldMap.OpenHexes(tile), surveyed = WorldMap.SurveyedHexes(tile);
        var surveyor = world.SurveyorOf(tile);
        string by = surveyor != null ? $"; {surveyor.name} is surveying it ({surveyed}/{open})" : string.Empty;
        if (world.HexesToSurvey(tile) == 0) return TooltipText.Good("Surveyed, hex by hex");
        if (tile.explored) return $"Explored in passing{(surveyed > 0 ? $", {surveyed}/{open} hexes surveyed" : string.Empty)}{by}";
        return $"Known: {WorldMap.SeenHexes(tile)}/{open} hexes seen (all of them explores it){by}";
    }

    // Wilderness your people are settling hex by hex (or will settle next, and when), or null.
    private static string SettlingState(WorldSystem world, WorldTile tile)
    {
        if (!WorldHoldings.Fillable(tile, WorldAuthority.Player)) return null;
        var plan = world.AdoptionForecast().FirstOrDefault(p => p.cell == tile.index);
        if (plan == null && tile.microHeldMask == 0) return null;
        string hexes = $"{WorldMap.SettledHexes(tile)}/{WorldMap.OpenHexes(tile)} hexes yours";
        if (plan == null || plan.sevenths.Count == 0) return WorldAuthority.IsPlayers(tile.authorityId) ? $"Yours de facto: {hexes}" : $"Held in part: {hexes}";
        string by = plan.seats.Count > 0 ? $" (drawn by {string.Join(", ", plan.seats)})" : string.Empty;
        string doing = tile.microHeldMask == 0 ? "Your society settles it next" : $"Your society is settling it ({hexes})";
        return $"{doing}{by}: the next hex in about {Sevenths(plan.sevenths[0])}, core in about {Sevenths(plan.WholeCell)}";
    }

    private void FollowCursor()
    {
        if (_hoverCard == null || !_hoverCard.gameObject.activeSelf) return;
        if (_mode != Mode.World || PointerOverUI())
        {
            _hoverCard.gameObject.SetActive(false);
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, InputUtils.MousePosition, null, out var local);
        var size = _canvasRect.rect.size;
        var pos = local + size * 0.5f + new Vector2(26f, -22f);
        if (pos.x + _hoverCard.sizeDelta.x > size.x - 8f) pos.x -= _hoverCard.sizeDelta.x + 52f;
        if (pos.y - _hoverCard.sizeDelta.y < 8f) pos.y = _hoverCard.sizeDelta.y + 8f;
        pos.x = Mathf.Clamp(pos.x, 8f, Mathf.Max(8f, size.x - _hoverCard.sizeDelta.x - 8f));
        pos.y = Mathf.Min(pos.y, size.y - 8f);
        _hoverCard.anchoredPosition = pos;
    }

    /// <summary>Everything a cell means: its ground and place, travel fatigue, fields, water and magic, what stands there, what it yields and could become, and where the selected unit could go.</summary>
    private string HoverText(WorldSystem world, WorldTile tile, bool travel = true)
    {
        var gen = world.Settings.generation;
        var rules = world.Rules;
        var text = new StringBuilder();
        // What stands inside concealing cover stays unseen until the cell is explored.
        var feature = tile.HasFeature && !WorldCover.Hides(tile) ? gen.Feature(tile.feature) : null;
        bool landmark = feature != null && feature.visibleFromAfar && tile.revealed;
        if (!tile.known)
        {
            // The fog, or unknown wilderness: seen from afar, but no scout has walked it.
            string seen = tile.revealed ? WorldCover.SpecAt(gen, tile)?.name ?? "Unknown wilderness" : "The Fog";
            text.AppendLine(TooltipText.Heading(landmark ? feature.name : seen, tile.revealed ? "Seen, not known" : "Unknown"));
            string fogLens = tile.revealed ? LensLines(world, tile) : null;
            if (fogLens != null) text.AppendLine(fogLens);
            text.AppendLine(TooltipText.Muted(landmark ? "Seen from afar; send a scout to learn more." : tile.revealed ? "Out of the fog, but no scout has passed over it: its ground and what stands there are unknown." : "Nothing is known of it. Send a scout."));
            if (tile.revealed && tile.water) text.AppendLine(TooltipText.Row("Water", "open water"));
            AppendRumours(world, tile, text);
            if (travel) AppendTravel(world, tile, text);
            return text.ToString().TrimEnd();
        }
        var terrain = gen.Terrain(tile.terrain);
        var biome = gen.MacroBiome(tile.macroBiome);
        text.AppendLine(TooltipText.Heading(world.Place(tile), tile.coord == world.Map.Capital ? TooltipText.Good("Your capital") : tile.explored ? TooltipText.Good("Explored") : "Known, not explored"));
        string lens = LensLines(world, tile);
        if (lens != null) text.AppendLine(lens);
        if (tile.coord != world.Map.Capital && !tile.water) text.AppendLine(TooltipText.Row("Survey", SurveyState(world, tile)));
        var where = new List<string>();
        if (terrain != null && world.Place(tile) != terrain.name) where.Add(terrain.name);
        if (biome != null) where.Add(WorldSectors.Name(tile.sector) is string sector ? $"{biome.name}, {sector}" : biome.name);
        if (tile.composition == WorldComposition.Intersection) where.Add("an intersection");
        if (where.Count > 0) text.AppendLine(TooltipText.Muted(string.Join(", ", where)));
        AppendCover(world, tile, text);
        AppendRumours(world, tile, text);
        if (travel && WorldZoom.ScaleOf(_size) == WorldScale.Micro && _hoveredMicro.HasValue && HexHierarchy.Parent(_hoveredMicro.Value) == tile.coord)
            AppendHex(world, tile, _hoveredMicro.Value, text);
        if (feature != null && !string.IsNullOrEmpty(feature.description)) text.AppendLine(TooltipText.Quote($"<i>{feature.description}</i>"));
        if (feature != null && !tile.explored && tile.coord != world.Map.Capital) text.AppendLine(TooltipText.Warn("Not investigated yet: walk all its hexes, or survey it (a survey turns up more)."));
        float richness = WorldUnits.ForageRichness(world.Provisions, tile);
        var forage = WorldUnits.ForageOf(gen, tile, richness);
        text.AppendLine(TooltipText.Row("Foraging richness", $"x{richness:0.##} (fertility and nearby resources)"));
        text.AppendLine(TooltipText.Row("Vibrational density", WorldVibration.Describe(tile)));
        if (forage.Count > 0) text.AppendLine(TooltipText.Row("Forage", tile.foragedAge == world.AgeNumber + 1 ? TooltipText.Muted("gathered this Age") : string.Join(", ", forage.Select(a => $"{a.amount:0.#} {a.resource}"))));

        float fatigue = WorldPaths.StepCost(tile, gen);
        if (float.IsPositiveInfinity(fatigue)) text.AppendLine(TooltipText.Row("Travel fatigue", tile.water ? "open water" : "impassable"));
        else
        {
            var why = new List<string>();
            if (tile.road) why.Add("road");
            if (tile.leylines != 0) why.Add("leyline");
            if (tile.danger > 0.01f || tile.dissonance > 0.01f) why.Add("dissonance and danger");
            if (tile.coverTravel > 1.01f) why.Add(gen.Cover(tile.cover)?.name.ToLowerInvariant() ?? "cover");
            text.AppendLine(TooltipText.Row("Travel fatigue", $"{fatigue:0.##}{(why.Count > 0 ? TooltipText.Muted($" ({string.Join(", ", why)})") : string.Empty)}"));
        }
        if (!tile.water)
            text.AppendLine(TooltipText.Row("Fertility · Coherence · Magic", $"{tile.landFertility:P0} · {tile.coherence:P0} · {tile.magicFertility:P0}"));
        if (tile.lake) text.AppendLine(TooltipText.Row("Water", tile.silver ? "a silver lake" : "a lake"));
        else if (tile.river) text.AppendLine(TooltipText.Row("Water", tile.silver ? $"a silver reach (Lunehymn {tile.lunehymn:P0})" : "a river"));
        if (!string.IsNullOrEmpty(tile.landform)) text.AppendLine(TooltipText.Row("Landform", tile.landform));
        var localWeather = CelestialWeatherSystemLogic.Instance?.WeatherAt(tile.coord);
        if (localWeather != null) text.AppendLine(TooltipText.Row("Weather", localWeather.weatherDisplayName));
        if (tile.junction >= 3) text.AppendLine(TooltipText.Row("Leylines", $"a Basin of {tile.junction}"));
        else if (tile.junction == 2) text.AppendLine(TooltipText.Row("Leylines", "a Convergence"));
        else if (tile.leylines != 0) text.AppendLine(TooltipText.Row("Leylines", "a leyline runs here"));
        if (tile.sacred) text.AppendLine(TooltipText.Row("Sacred Site", "fixed through every Age"));
        if (tile.nexus != null) text.AppendLine(TooltipText.Row("Trade Nexus site", WorldSites.NexusName(tile.nexus)));
        if (tile.road) text.AppendLine(TooltipText.Row("Road", tile.tradeNode ? "a Trade Node" : "part of a Trade Route"));
        if (tile.grandfield >= 0 && tile.grandfield < world.Map.Grandfields.Count)
        {
            var field = gen.Grandfield(world.Map.Grandfields[tile.grandfield].spec);
            text.AppendLine(TooltipText.Row("Grandfield", $"{field?.name} ({field?.resource}), density {tile.grandfieldDensity:P0}"));
        }
        AppendResource(world, tile, text);
        if (tile.danger > 0.01f) text.AppendLine(TooltipText.Row("Hazard", TooltipText.Warn($"{tile.danger:P0}")));
        if (tile.signs > 0.05f) text.AppendLine(TooltipText.Row("Signs", TooltipText.Warn($"fresh signs of a hunter ({tile.signs:P0}): something hunts nearby")));
        if (tile.suffering > 0.02f) text.AppendLine(TooltipText.Row("Suffering", $"{tile.suffering:0.##}: battles, hunts and loss linger here{(WorldSuffering.Pressure(tile, tile.suffering) >= WorldSuffering.SpawnPressure ? TooltipText.Warn("; enough for a Formless Mass to pool") : string.Empty)}"));
        var feel = WorldSuffering.Feelings(tile);
        if (tile.known && feel.Total > 0.02f)
            text.AppendLine(TooltipText.Row("Feelings", $"{feel.Words(3)}\n" + WorldSuffering.Meter(feel, Math.Max(WorldSuffering.SpawnPressure, feel.Total))));
        string compounds = tile.known ? WorldSuffering.CompoundWords(feel) : null;
        if (compounds != null) text.AppendLine(TooltipText.Row("The air holds", compounds));
        if (tile.known && feel.Total > 0.02f)
            text.AppendLine(TooltipText.Row("Harmony", $"{feel.Harmony:P0} consonant" + TooltipText.Muted(feel.Harmony >= 0.5f ? ": its feelings are mostly whole" : ": its feelings are mostly wounds")));
        string owner = tile.authorityId == WorldAuthority.Player ? TooltipText.Good("Your authority") : tile.authorityId == WorldAuthority.Outpost ? "Your Outpost"
            : tile.authorityId == WorldAuthority.Wilderness ? TooltipText.Muted(tile.water ? "Unclaimed waters" : "Wilderness")
            : tile.enclave >= 0 || tile.authorityId.StartsWith("enclave:", StringComparison.Ordinal) ? EnclaveOwner(world, tile) : HolderName(world, tile.authorityId);
        text.AppendLine(TooltipText.Row("Held by", owner));
        // Held hex by hex: de facto or core, who else holds hexes, and any dispute.
        string hold = HoldText(world, tile);
        if (hold != null) text.AppendLine(TooltipText.Row("Hold", hold));
        string settling = SettlingState(world, tile);
        if (settling != null) text.AppendLine(TooltipText.Row("Settling", settling));
        // Land is held hex by hex: at the micro reading, the hex under the cursor.
        if (!tile.water && tile.known && WorldZoom.ScaleOf(_size) == WorldScale.Micro
            && _hoveredMicro.HasValue && HexHierarchy.Parent(_hoveredMicro.Value) == tile.coord)
        {
            int id = MicroNavigation.Index(world.Map, _hoveredMicro.Value), bit = 1 << (id % MicroNavigation.PerCell);
            string holder = WorldHoldings.HexHolder(world.Map, id);
            string why = holder == null ? world.WhyNotClaim(tile, _hoveredMicro.Value) : null;
            text.AppendLine(TooltipText.Row("This hex", WorldAuthority.IsPlayers(holder) ? TooltipText.Good((tile.microClaimMask & bit) != 0 ? "yours (claimed)" : "yours")
                : holder != null ? $"held by {HolderName(world, holder)}"
                : why == null ? $"can be claimed ({world.ClaimCostText}): click it, then Claim this hex" : TooltipText.Muted(why)));
        }
        if (!tile.water)
        {
            var beautyWhy = WorldBeauty.Explain(world.Map, gen, tile).Take(2).Select(r => r.reason).ToList();
            text.AppendLine(TooltipText.Row("Beauty", beautyWhy.Count > 0 ? $"{WorldBeauty.Word(tile.beauty)} {TooltipText.Muted($"({string.Join(", ", beautyWhy)})")}" : WorldBeauty.Word(tile.beauty)));
            var puller = world.Map.territory?.Seat(tile.pullSeat);
            if (puller != null && !tile.impassable) text.AppendLine(TooltipText.Row("Pull", $"{tile.pull:0.00} from {puller.name}{(tile.rivalPull > tile.pull ? TooltipText.Warn($", a rival pulls {tile.rivalPull:0.00}") : string.Empty)}"));
        }

        var yields = world.CellYields(tile);
        if (yields.Count > 0) text.AppendLine(TooltipText.Row("Yields", string.Join(", ", yields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
        if (WorldUnits.IsHotspot(world.Map, gen, tile))
            text.AppendLine(TooltipText.Row("Hotspot", tile.improvement > 0 ? $"improved to level {tile.improvement} of {rules.maxImprovement}" : world.CanImprove ? "an expedition can improve it" : $"expeditions can improve it after {world.ExpeditionRules.improveTechnology}"));
        if (!tile.water && !tile.impassable && tile.settlement < 0)
        {
            var b = CityDevelopment.Evaluate(world.Map, rules, tile.index);
            text.AppendLine(TooltipText.Row("A town here", $"City Development {b.Potential:0} {TooltipText.Muted($"(ceiling {b.ceiling:0})")}"));
        }
        if (tile.settlement >= 0 && tile.settlement < world.Map.Settlements.Count)
        {
            var s = world.Map.Settlements[tile.settlement];
            text.AppendLine(TooltipText.Row(WorldCivilization.KindName(s.kind), s.kind == SettlementKind.Outpost ? s.name : $"{s.name}, City Development {s.development:0}"));
        }
        var here = world.UnitsAt(tile.coord).ToList();
        if (here.Count > 0) text.AppendLine(TooltipText.Row("Units", string.Join(", ", here.Select(u => u.name))));
        if (travel) AppendTravel(world, tile, text);
        return text.ToString().TrimEnd();
    }

    // Cover over the cell (a Deep Forest, a Mistfen): what it does to exploring, what it gives once held, and whether
    // what stands inside is still hidden.
    // The rumours whose area covers this cell: what was heard (the place is somewhere near, never marked).
    private static void AppendRumours(WorldSystem world, WorldTile tile, StringBuilder text)
    {
        foreach (var r in WorldRumours.Covering(RumourKeeper.Current, world.Map, tile.index, RumourKeeper.Tuning))
            text.AppendLine(TooltipText.Row("Rumour", TooltipText.Muted(r.text)));
    }

    private static void AppendCover(WorldSystem world, WorldTile tile, StringBuilder text)
    {
        var gen = world.Settings.generation;
        var cover = WorldCover.SpecAt(gen, tile);
        if (cover == null) return;
        var patch = WorldCover.PatchAt(world.Map, tile);
        text.AppendLine(TooltipText.Row("Cover", $"{cover.name}{(patch != null && patch.cells.Count > 1 ? TooltipText.Muted($" ({patch.cells.Count} cells)") : string.Empty)}"));
        if (!string.IsNullOrEmpty(cover.description)) text.AppendLine(TooltipText.Quote($"<i>{cover.description}</i>"));
        string effects = WorldCover.Effects(cover);
        if (effects.Length > 0) text.AppendLine(TooltipText.Row("Exploring", effects));
        var yields = WorldCover.YieldsOf(gen, tile);
        if (yields.Count > 0) text.AppendLine(TooltipText.Row(WorldAuthority.IsPlayers(tile.authorityId) ? "It yields" : "Held, it yields", string.Join(", ", yields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
        if (WorldCover.Hides(tile)) text.AppendLine(TooltipText.Warn("What stands inside is hidden: explore it to find out."));
    }

    // A resource site on the cell: unidentified until surveyed (its kind and a hint only); identified, its land value,
    // what it yields and gives to a harvest, and what it does to the land around it. Then what nearby sites do here.
    private static void AppendResource(WorldSystem world, WorldTile tile, StringBuilder text)
    {
        var map = world.Map;
        var gen = world.Settings.generation;
        var site = WorldResources.SiteAt(map, tile);
        var spec = site != null ? gen.ResourceSite(site.spec) : null;
        if (spec != null && WorldResources.Sighted(tile, site, spec))
        {
            if (!WorldResources.Identified(map, site))
            {
                text.AppendLine(TooltipText.Row("Resource", $"{WorldResources.Label(map, gen, tile)} {TooltipText.Muted($"({WorldResources.Hint(site.kind)})")}"));
                text.AppendLine(TooltipText.Warn("Explore it to learn what it is: walk all its hexes, or survey it."));
            }
            else
            {
                string value = site.landValue >= 0f ? TooltipText.Good($"land value +{site.landValue:0.#}") : TooltipText.Bad($"land value {site.landValue:0.#}");
                text.AppendLine(TooltipText.Row("Resource", $"{site.name} ({value}{(site.cells.Count > 1 ? $", a patch of {site.cells.Count} cells" : string.Empty)})"));
                if (!string.IsNullOrEmpty(spec.description)) text.AppendLine(TooltipText.Quote($"<i>{spec.description}</i>"));
                var cultureLife = CultureSystem.Instance != null ? CultureSystem.Instance.Life : new CultureLifeTuning();
                string culturalUse = CultureLifeRules.ResourceUse(cultureLife, spec.resource);
                if (culturalUse != null) text.AppendLine(TooltipText.Muted(culturalUse));
                var garden = cultureLife.gardens.FirstOrDefault(g => g.site == spec.id);
                if (garden != null) text.AppendLine(TooltipText.Muted($"Living amenity: up to {garden.amenity:0.#} {garden.category} when surveyed and held, scaled by bloom vigor. +{garden.faithPerUnit:0.##} Faith per unit enjoyed. The blooms remain intact."));
                if (spec.minimumDesirability > 0f) text.AppendLine(TooltipText.Row("Specialty habitat", $"natural desirability {spec.minimumDesirability:P0} or more"));
                AppendSpecies(gen.Species(spec.species), text);
                // How its numbers fare and how it treats you are read once it has been watched long enough (E5: Observed).
                bool observed = WorldEcology.SpeciesAt(gen, site) != null && world.Knows(spec.species, SpeciesLevel.Observed);
                if (observed)
                {
                    AppendPopulation(world, site, text);
                    AppendBehavior(world, gen.Species(spec.species), text);
                }
                else if (WorldEcology.SpeciesAt(gen, site) != null) text.AppendLine(TooltipText.Muted("Watch it and hunt it longer to learn how its numbers fare and how it treats your people."));
                if (WorldEcology.SpeciesAt(gen, site) != null) AppendMagicAndIlls(world, site, gen.Species(spec.species), text);
                AppendSeason(world, site, text);
                var effects = new List<string>();
                if (Mathf.Abs(spec.beautyAura) > 0.001f) effects.Add(spec.beautyAura > 0f ? "fairer" : "uglier");
                if (Mathf.Abs(spec.coherenceAura) > 0.001f) effects.Add(spec.coherenceAura > 0f ? $"+{spec.coherenceAura:P0} Coherence" : $"{spec.coherenceAura:P0} Coherence");
                if (Mathf.Abs(spec.fertilityAura) > 0.001f) effects.Add($"{spec.fertilityAura:+0%;-0%} fertility");
                if (spec.kind == ResourceKind.Bloom) AppendBloom(site, spec, tile, gen, effects, text);
                if (spec.kind == ResourceKind.Bloom) AppendBloomLife(world, site, spec, text);
                if (effects.Count > 0) text.AppendLine(TooltipText.Row("Around it", $"{string.Join(", ", effects)} within {spec.auraRadius}"));
                var yields = WorldResources.YieldsAt(map, gen, tile);
                if (yields.Count > 0) text.AppendLine(TooltipText.Row(WorldAuthority.IsPlayers(tile.authorityId) ? "It yields" : "Held, it yields", string.Join(", ", yields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
                var harvest = WorldResources.HarvestAt(map, gen, tile, WorldBehavior.HuntShare(map, gen, site, WorldAuthority.Player));
                if (harvest.Count > 0)
                {
                    bool den = WorldEcology.SpeciesAt(gen, site) != null;
                    string note = den ? HuntNote(world, site)
                        : !den && tile.harvestedAge == world.AgeNumber + 1 ? TooltipText.Muted(" (harvested this Age)") : spec.seeds && !site.planted ? TooltipText.Muted(" and seeds") : string.Empty;
                    text.AppendLine(TooltipText.Row(den ? "Hunt" : "Harvest", $"{string.Join(", ", harvest.Select(a => $"{a.amount:0.#} {a.resource}"))}{note}"));
                    if (observed && WorldBehavior.HuntShare(map, gen, site, WorldAuthority.Player) < 0.999f) text.AppendLine(TooltipText.Warn("It has learned to flee your hunters: a hunt brings back less."));
                    if (observed && WorldBehavior.DenDanger(map, gen, site) > 0f) text.AppendLine(TooltipText.Warn("It has turned against you: its den is dangerous to come near."));
                    string holder = WorldResources.Owner(tile);
                    if (holder != null && spec.grievance > 0f) text.AppendLine(TooltipText.Warn($"Held by {WorldResources.OwnerName(map, gen, holder)}: a harvest raises their grievances (+{spec.grievance:0})."));
                }
                else if (!string.IsNullOrEmpty(spec.untouchable)) text.AppendLine(TooltipText.Row("Harvest", TooltipText.Muted(spec.untouchable)));
            }
        }
        bool nearby = Mathf.Abs(tile.siteCoherence) > 0.005f || Mathf.Abs(tile.siteFertility) > 0.005f || Mathf.Abs(tile.siteDissonance) > 0.005f || tile.lure > 0.01f || tile.sanctuary > 0.01f;
        if (tile.known && !tile.water && nearby && site == null)
        {
            var near = new List<string>();
            if (Mathf.Abs(tile.siteCoherence) > 0.005f) near.Add($"{tile.siteCoherence:+0%;-0%} Coherence");
            if (Mathf.Abs(tile.siteFertility) > 0.005f) near.Add($"{tile.siteFertility:+0%;-0%} fertility");
            if (Mathf.Abs(tile.siteDissonance) > 0.005f) near.Add($"{tile.siteDissonance:+0%;-0%} Dissonance");
            if (tile.sanctuary > 0.01f) near.Add(TooltipText.Good($"a bloom's light eases {tile.sanctuary:0.#} strain"));
            if (tile.lure > 0.01f) near.Add(TooltipText.Bad("a bloom's lure"));
            text.AppendLine(TooltipText.Row("From sites nearby", string.Join(", ", near)));
        }
    }

    // An Eleos Bloom: its niche, the Emotional Residue it finds and its vigor (withering when starved), and what it
    // does around it (drinks Dissonance, shelters or lures expeditions).
    /// <summary>A den's Macro Biome population (WorldEcology), in words: exact numbers wait for the bestiary's knowledge (E5).</summary>
    private static void AppendPopulation(WorldSystem world, ResourceSite site, StringBuilder text)
    {
        var gen = world.Settings.generation;
        var p = WorldEcology.PopulationAt(world.Map, gen, site);
        if (p == null) return;
        string word = WorldEcology.AbundanceWord(p, gen.ecology), trend = WorldEcology.TrendWord(p);
        string where = WorldEcology.MacroBiomeOf(world.Map, p.slot)?.name ?? "this land";
        string line = $"{word} in the {where}{(trend != null ? $", {trend}" : string.Empty)}";
        text.AppendLine(TooltipText.Row("Population", word == "depleted" || word == "gone" ? TooltipText.Bad(line) : word == "thriving" ? TooltipText.Good(line) : line));
    }

    /// <summary>
    /// A den in its season (WorldRhythm): what the Echo does to the creatures, how strongly this Phase carries it, what it
    /// does to the den's yields and hunts now, and a hungry predator straying toward your land.
    /// </summary>
    private static void AppendSeason(WorldSystem world, ResourceSite site, StringBuilder text)
    {
        var map = world.Map;
        var gen = world.Settings.generation;
        var rhythm = WorldRhythm.Of(gen, map.echo);
        if (rhythm == null || WorldEcology.SpeciesAt(gen, site) == null) return;
        float intensity = WorldRhythm.Intensity(map, gen), yields = WorldRhythm.Yields(map, gen);
        string strength = intensity >= 0.999f ? "at its height" : map.echoPhase <= 1 ? "opening" : "waning";
        string effect = Mathf.Abs(yields - 1f) > 0.005f ? $"; yields and hunts {yields - 1f:+0%;-0%} now" : string.Empty;
        text.AppendLine(TooltipText.Row("Season", $"{rhythm.name}, {strength}: {rhythm.summary}{effect}"));
        if (WorldRhythm.HungerDanger(map, gen, site) > 0f) text.AppendLine(TooltipText.Warn("Hungry: its hunters stray onto your land nearby."));
        if (map.ritualSeventh) text.AppendLine(TooltipText.Good("Ritual Seventh: the world attunes; a hunt now wounds their trust twice over."));
    }

    /// <summary>
    /// How the creature acts toward you (WorldBehavior): its nature until your treatment moves it, and what newcomers
    /// meet when that differs. Words only, like the population.
    /// </summary>
    private static void AppendBehavior(WorldSystem world, SpeciesSpec species, StringBuilder text)
    {
        if (species == null || CreatureTaxonomy.Profile(species.diet, species.subgroup) == null) return;
        var map = world.Map;
        var gen = world.Settings.generation;
        var nature = WorldBehavior.Nature(species);
        var toward = WorldBehavior.Toward(map, gen, species, WorldAuthority.Player);
        var overall = WorldBehavior.Overall(map, gen, species);
        if (toward == nature) text.AppendLine(TooltipText.Row("Behavior", $"toward you it {WorldBehavior.Words(nature)}, as is its nature"));
        else
        {
            string line = $"toward you it {WorldBehavior.Words(toward)} (by nature it {WorldBehavior.Words(nature)})";
            text.AppendLine(TooltipText.Row("Behavior", WorldBehavior.Harsher(nature, toward) ? TooltipText.Bad(line) : TooltipText.Good(line)));
            text.AppendLine(TooltipText.Muted(WorldBehavior.Harsher(nature, toward)
                ? "Hunting it and taking its land made it so; living beside it unhunted calms it, and each Age it forgets a little."
                : "Living beside it unhunted made it so; each Age it forgets a little."));
        }
        if (overall != toward && WorldBehavior.Met(map, species.id, WorldAuthority.Player) && world.Knows(species.id, SpeciesLevel.Understood))
            text.AppendLine(TooltipText.Row("Newcomers", TooltipText.Muted($"it {WorldBehavior.Words(overall)}: the word has spread")));
    }

    /// <summary>The creature living at an identified site: its AECOR subgroup, how it lives, and its nature.</summary>
    private static void AppendSpecies(SpeciesSpec species, StringBuilder text)
    {
        var profile = species == null ? null : CreatureTaxonomy.Profile(species.diet, species.subgroup);
        if (profile == null) return;
        var (min, max) = CreatureTaxonomy.GroupOf(species);
        text.AppendLine(TooltipText.Row("Creature", $"{species.name}, {profile.Name}"));
        text.AppendLine(TooltipText.Muted(profile.summary));
        text.AppendLine(TooltipText.Row("Lives", $"{CreatureTaxonomy.SizeWord(species.size)}, {CreatureTaxonomy.GroupWords(min, max)}, breeds {CreatureTaxonomy.PaceWord(CreatureTaxonomy.PaceOf(species))}"));
        text.AppendLine(TooltipText.Row("Nature", $"Auric Structure {species.structure:P0}, Pure Light {1f - species.structure:P0}{(CreatureTaxonomy.IsPureLightBeing(species) ? ": a Pure Light being" : string.Empty)}"));
        string organ = CreatureTaxonomy.BindingWords(species.binding);
        if (organ != null) text.AppendLine(TooltipText.Row("Magic", organ));
        if (species.commensal >= 0.5f) text.AppendLine(TooltipText.Row("Near people", "it lives off people's works, in granaries and middens, and thrives beside settlements"));
    }

    /// <summary>
    /// What knowing a creature better reveals of its magic and its ills (E10): its harmonic niche and breeding Echo once
    /// Observed; the sickness it carries and the plagues tuned to it once Understood; and a plague running among it now,
    /// by its folklore until then.
    /// </summary>
    private static void AppendMagicAndIlls(WorldSystem world, ResourceSite site, SpeciesSpec species, StringBuilder text)
    {
        if (species == null) return;
        var gen = world.Settings.generation;
        bool observed = world.Knows(species.id, SpeciesLevel.Observed), understood = world.Knows(species.id, SpeciesLevel.Understood);
        // Niche and breeding once the people worked them out (SpeciesHypotheses) or understand the creature.
        var record = SpeciesLoreKeeper.View?.Of(species.id);
        var level = understood ? SpeciesLevel.Understood : observed ? SpeciesLevel.Observed : SpeciesLevel.Identified;
        bool knowsNiche = SpeciesHypotheses.Knows(record, HypothesisQuestion.Niche, level), knowsBreeding = SpeciesHypotheses.Knows(record, HypothesisQuestion.Breeding, level);
        if (knowsNiche || knowsBreeding)
        {
            string niche = knowsNiche ? CreatureTaxonomy.NicheWords(species.niche) : null;
            if (niche != null) text.AppendLine(TooltipText.Row("Niche", niche));
            int breeding = knowsBreeding ? CreatureTaxonomy.BreedingEchoOf(species) : 0;
            string echo = breeding > 0 ? WorldRhythm.EchoName(gen, breeding) : null;
            if (echo != null) text.AppendLine(TooltipText.Row("Breeds", $"in the {echo}"));
        }
        if ((understood || world.KnownVector(species.id)) && species.vector > 0f) text.AppendLine(TooltipText.Warn($"It carries sickness: near your settlements it feeds Disease Burden{(species.vector >= 0.5f ? ", strongly" : string.Empty)}."));
        if (understood)
        {
            var tuned = WorldPlagues.PlaguesOf(gen, species.id).Select(p => p.name).ToList();
            if (tuned.Count > 0) text.AppendLine(TooltipText.Row("Vulnerable to", TooltipText.Muted($"{string.Join(", ", tuned)} (near-immune to other sickness)")));
        }
        foreach (var (outbreak, plague) in WorldPlagues.At(world.Map, gen, site))
        {
            bool known = plague.hosts.Any(h => world.Knows(h, SpeciesLevel.Understood));
            string left = outbreak.echoesLeft == 1 ? "its last Echo" : $"{outbreak.echoesLeft} more Echoes";
            text.AppendLine(TooltipText.Row("Sickness", TooltipText.Bad($"{WorldPlagues.Name(plague, known)} runs through them ({left})")));
            if (known && !string.IsNullOrEmpty(plague.description)) text.AppendLine(TooltipText.Muted(plague.description));
        }
    }

    private static void AppendBloom(ResourceSite site, ResourceSiteSpec spec, WorldTile tile, WorldGenSettings gen, List<string> effects, StringBuilder text)
    {
        string vigor = WorldResources.Withering(site, gen)
            ? TooltipText.Bad($"withering: it finds {tile.residue:P0} Emotional Residue and needs {spec.residueNeed:P0}")
            : spec.residueNeed <= 0f ? TooltipText.Good("needs no feeling to thrive") : $"vigor {site.vigor:P0} ({tile.residue:P0} Emotional Residue here, thrives at {spec.residueNeed:P0})";
        text.AppendLine(TooltipText.Row("Eleos Bloom", $"{WorldResources.NicheWord(spec.niche)}, {vigor}"));
        var palate = WorldResources.PalateOf(site, spec);
        var map = WorldSystem.Instance?.Map;
        if (palate != null && map != null)
        {
            // Its cocktail: what it eats of the Emotional Register, how picky it is, and what its lineage has become.
            string recipe = string.Join(", ", EmotionalAlchemy.Shares(palate.recipe)
                .Select(l => $"<color=#{WorldSuffering.Colour(l.feeling)}>{l.feeling}</color> {Math.Round(l.weight * 100f):0}%"));
            text.AppendLine(TooltipText.Row("Cocktail", $"{EmotionalEvolution.NicheWord(palate.specificity)}: {recipe}"));
            var lineage = site.lineage;
            if (lineage != null)
                text.AppendLine(TooltipText.Row("Lineage", $"generation {lineage.generation}{(lineage.variety != null ? $", now {TooltipText.Good(lineage.variety)}" : string.Empty)}"
                    + (lineage.history.Count > 0 ? TooltipText.Muted($"; last {lineage.history[lineage.history.Count - 1].change.ToString().ToLowerInvariant()} at generation {lineage.history[lineage.history.Count - 1].generation}") : string.Empty)));
            var feeding = WorldResources.FeedingAt(map, site, spec, gen.eleos);
            string how = feeding.potency >= 1.5f ? TooltipText.Good($"superloaded: gifts x{feeding.potency:0.0}")
                : feeding.growth < 0.5f ? TooltipText.Warn($"starving on this land's feelings: its lineage will likely loosen its palate")
                : $"gifts x{feeding.potency:0.0}";
            text.AppendLine(TooltipText.Row("Feeding", $"fit {feeding.fit:P0}, {feeding.food:0.00} of its cocktail here, growth {Math.Min(feeding.growth, 9.99f):0.00}; {how}"));
            text.AppendLine(TooltipText.Muted(WorldResources.Withering(site, gen) ? "Too starved to drink." : spec.niche == BloomNiche.Healer
                ? "It drinks its cocktail from the ground and breathes the wounds it drinks back out as their healthy pair."
                : "It drinks its cocktail from the ground, the region's emotional filter."));
        }
        if (spec.sprouts) text.AppendLine(TooltipText.Row("Sterile", TooltipText.Muted($"it gives no seeds and grows by itself where history and sorrow gather (history {tile.history:P0}, sorrow {tile.hurt:P0} here)")));
        var into = !string.IsNullOrEmpty(spec.turnsInto) ? gen.ResourceSite(spec.turnsInto) : null;
        if (into != null)
        {
            // Echo by Echo: how far its turn has run, and what drives it here.
            bool decays = spec.turnHurt > 0f;
            string verb = decays ? "decays" : "heals";
            string drive = decays ? $"sorrow {tile.hurt:P0} here, turns from {spec.turnHurt:P0}{(spec.holdCoherence > 0f ? $" unless Coherence holds at {spec.holdCoherence:P0}" : string.Empty)}; Dissonance hastens it"
                                  : $"Coherence {tile.coherence:P0} here, turns from {spec.turnCoherence:P0}";
            string progress = site.turning > 0.005f ? $"{site.turning:P0} of the way: it {verb} into {into.name} Echo by Echo ({drive})" : $"it {verb} into {into.name} when its ground turns ({drive})";
            text.AppendLine(TooltipText.Row(decays ? "Sorrow" : "Healing", site.turning > 0.005f && decays ? TooltipText.Bad(progress) : site.turning > 0.005f ? TooltipText.Good(progress) : progress));
        }
        if (site.fading > 0.005f) text.AppendLine(TooltipText.Warn($"Fading ({site.fading:P0}): what let it grow here is gone."));
        else if (spec.silverRiverReach > 0 || spec.silverLakeReach > 0) text.AppendLine(TooltipText.Row("Moonlit", TooltipText.Muted("it lives on the silver: when the leylines leave this river it fades")));
        float gift = WorldResources.Gift(site, gen);
        if (spec.dissonanceAura < -0.001f) effects.Add($"drinks {-spec.dissonanceAura * gift:0%} Dissonance");
        else if (spec.dissonanceAura > 0.001f) effects.Add($"+{spec.dissonanceAura * gift:0%} Dissonance");
        if (spec.soothe > 0f && gift > 0f) effects.Add(TooltipText.Good($"eases {spec.soothe * gift:0.#} strain a Seventh for expeditions (a camp here rests)"));
        if (spec.niche == BloomNiche.Predator && gift > 0f) effects.Add(TooltipText.Bad($"danger {spec.dangerAura * gift:P0}, lures travellers"));
    }

    // A bloom among the living (WorldResources.Volunteer, Drift, BloomGround): the settlement whose life drew it up,
    // whether it wanders, and the creatures you know that it draws.
    private static void AppendBloomLife(WorldSystem world, ResourceSite site, ResourceSiteSpec spec, StringBuilder text)
    {
        var map = world.Map;
        var gen = world.Settings.generation;
        if (site.tendedBy >= 0)
        {
            var s = map.Settlements.Find(x => x.id == site.tendedBy);
            string reason = s != null ? WorldResources.DrawReason(gen, s, spec) : null;
            string why = reason == "grief" ? "its people's grief" : reason == "ease" ? "its people's ease" : reason != null ? $"its {world.Rules.tributaries.District(reason)?.name ?? reason}" : null;
            text.AppendLine(TooltipText.Row("Drawn up", s == null ? TooltipText.Muted("by a settlement that is gone")
                : why != null ? $"by {s.name}: {why}" : TooltipText.Warn($"by {s.name}, which no longer draws it")));
        }
        else if (spec.driftPerEcho > 0f)
            text.AppendLine(TooltipText.Row("Wandering", TooltipText.Muted(spec.niche == BloomNiche.Predator ? "it creeps toward feeling, Echo by Echo" : "the patch creeps toward better ground, Echo by Echo")));
        var drawn = gen.species.Where(sp => sp != null && WorldResources.DrawsCreature(sp, spec) && world.Knows(sp.id, SpeciesLevel.Identified)).Select(sp => sp.name).ToList();
        if (drawn.Count > 0) text.AppendLine(TooltipText.Row("Draws", string.Join(", ", drawn)));
    }

    private static string EnclaveOwner(WorldSystem world, WorldTile tile)
    {
        int index = tile.enclave;
        if (index < 0 && tile.authorityId.StartsWith("enclave:", StringComparison.Ordinal)) int.TryParse(tile.authorityId.Substring(8), out index);
        return index >= 0 && index < world.Map.Enclaves.Count ? $"{world.Map.Enclaves[index].name} ({world.Map.Enclaves[index].family})" : "an enclave";
    }

    // A holder's name in words: you, your Outpost, an enclave, or an independent claim.
    private static string HolderName(WorldSystem world, string holder) =>
        holder == WorldAuthority.Player ? "you" : holder == WorldAuthority.Outpost ? "your Outpost"
        : WorldResources.EnclaveOf(world.Map, holder) is Enclave e ? e.name : "an independent claim";

    // How the cell is held, hex by hex (WorldHoldings): your share and status, anyone else's, and any dispute over it
    // (a grievance, a casus belli). Null for plain wilderness no one holds any of.
    private static string HoldText(WorldSystem world, WorldTile tile)
    {
        var map = world.Map;
        if (tile.water) return null;
        var shares = WorldHoldings.Shares(map, tile);
        if (shares.Count == 0) return null;
        int open = WorldMap.OpenHexes(tile);
        var parts = new List<string>();
        int mine = WorldHoldings.Hexes(map, tile, WorldAuthority.Player);
        switch (WorldHoldings.Status(map, tile, WorldAuthority.Player))
        {
            case HoldStatus.Core: parts.Add(TooltipText.Good("core territory of yours")); break;
            case HoldStatus.DeFacto: parts.Add($"{TooltipText.Good($"yours de facto ({mine}/{open} hexes)")}: hold every hex to make it core"); break;
            case HoldStatus.Partial: parts.Add($"{mine}/{open} hexes yours"); break;
        }
        foreach (var s in shares.Where(s => !WorldAuthority.IsPlayers(s.holder)))
        {
            var status = WorldHoldings.Status(map, tile, s.holder);
            parts.Add($"{HolderName(world, s.holder)} {(status == HoldStatus.Core ? "holds all of it" : status == HoldStatus.DeFacto ? $"rules it de facto ({s.hexes}/{open} hexes)" : $"holds {s.hexes}/{open} hexes")}");
        }
        var dispute = WorldHoldings.Dispute(map, tile);
        if (dispute != null)
        {
            var why = WorldHoldings.CasusBelliOf(dispute, WorldAuthority.Player);
            int grievance = dispute.Grievance(WorldAuthority.Player);
            if (why == CasusBelli.Recover) parts.Add(TooltipText.Warn("disputed: a core of yours others hold: a casus belli to recover it"));
            else if (why == CasusBelli.Integrate) parts.Add(TooltipText.Warn("disputed: a casus belli to integrate the rest as core"));
            else if (grievance > 0) parts.Add(TooltipText.Warn($"disputed: a grievance over your {grievance} hexes under another's rule"));
            else parts.Add(TooltipText.Warn("disputed"));
        }
        return string.Join("; ", parts);
    }

    // The hex under the cursor at the micro reading: its own ground, what lies on it, what entering it costs and
    // whether a unit walked or surveyed it.
    private static void AppendHex(WorldSystem world, WorldTile tile, HexCoord hex, StringBuilder text)
    {
        var map = world.Map;
        int id = MicroNavigation.Index(map, hex);
        if (id < 0 || tile.water) return;
        var grid = MicroNavigation.Grid(map, world.Settings.generation);
        var marks = grid.Marks(id);
        bool road = (marks & MicroGrid.Mark.Road) != 0, ford = (marks & (MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord)) != 0;
        var what = new List<string>();
        var ground = grid.Ground(id);
        if (ground != null) what.Add((marks & MicroGrid.Mark.Borrowed) != 0 ? $"{ground.name}, the neighbouring ground" : ground.name);
        if ((marks & MicroGrid.Mark.Crag) != 0) what.Add(road ? "crags the road cuts through" : "crags no one climbs");
        if (ford) what.Add(road ? "a bridge over the river" : (marks & MicroGrid.Mark.GreatFord) != 0 ? "a deep ford of a great river" : "a river ford");
        else if (road) what.Add("on the road");
        if (id % MicroNavigation.PerCell == 0) what.Add("the heart of the cell");
        text.AppendLine(TooltipText.Row("This hex", string.Join(", ", what)));
        float cost = grid.Enter(id);
        string walked = MicroNavigation.Surveyed(map, id) ? TooltipText.Good("surveyed") : MicroNavigation.Known(map, id) ? "walked, not surveyed" : "no unit has walked it";
        text.AppendLine(TooltipText.Row("To enter it", float.IsPositiveInfinity(cost) ? TooltipText.Warn("impassable") : $"fatigue {cost:0.##} {TooltipText.Muted($"({walked})")}"));
    }

    // Where the selected unit could go: the hexes, fatigue and Sevenths the journey takes and the rations it eats
    // beyond your authority, or why not.
    private void AppendTravel(WorldSystem world, WorldTile tile, StringBuilder text)
    {
        var unit = world.UnitById(_selectedUnit);
        if (unit == null || unit.Moving) return;
        var p = PreviewFor(world, unit);
        if (!p.valid) return;
        var spec = world.SpecOf(unit);
        if (p.why != null)
        {
            if (!p.why.StartsWith("It is already", StringComparison.Ordinal))
            {
                text.AppendLine();
                text.AppendLine(TooltipText.Warn($"{unit.name}: {p.why}"));
            }
            return;
        }
        text.AppendLine();
        bool exact = p.micro ? p.reached == p.target : p.reached == MicroNavigation.Center(p.target);
        string where = exact ? (p.micro ? "there" : "to the heart of the cell") : "to the nearest hex it can reach";
        text.AppendLine(TooltipText.Good($"Right click: {unit.name} goes {where} ({Hexes(p.path.Count)}, fatigue {p.fatigue:0.#}, about {Sevenths(WorldUnits.SeventhsFor(unit, spec, p.fatigue))})"));
        if (spec == null) return;
        if (spec.supplyUsePerSeventh > 0f)
        {
            float rations = WorldUnits.RationsAway(world.Map, world.Settings.generation, unit, spec, p.path);
            if (rations > unit.supplies + 1e-3f) text.AppendLine(TooltipText.Warn($"It eats about {rations:0.#} rations beyond your authority and carries {unit.supplies:0.#}: it will go hungry."));
            else if (rations > 0.05f) text.AppendLine(TooltipText.Muted($"About {rations:0.#} of its {unit.supplies:0.#} rations beyond your authority."));
        }
        if (unit.fatigue + p.fatigue * spec.fatiguePerTravelCost >= world.Provisions.exhaustion)
            text.AppendLine(TooltipText.Warn("It will have to make camp and rest on the way."));
        if (WorldUnits.Can(spec, UnitAbility.Survey) || WorldUnits.Can(spec, UnitAbility.SurveyMeso))
        {
            var cell = world.Map.Get(p.micro ? HexHierarchy.Parent(p.target) : p.target);
            int left = world.HexesToSurvey(cell);
            if (left > 0) text.AppendLine(TooltipText.Muted($"Shift + right click: survey this meso hex (walks its {Hexes(left)}, about {Sevenths(world.SurveySevenths(unit, cell.coord))} once there)."));
        }
    }

    // ===== REFRESH =====

    private void Refresh(bool force)
    {
        _refreshAt = Time.unscaledTime + RefreshSeconds;
        var world = WorldSystem.Instance;
        if (world == null || world.Map == null || _renderer == null) return;
        var map = world.Map;
        // Walking units change knowledge hex by hex: the fog is rewritten at most a few times a second.
        if (map.KnowledgeVersion != _knowledgeSeen && (force || Time.unscaledTime >= _knowledgeAt))
        {
            _knowledgeSeen = map.KnowledgeVersion;
            _knowledgeAt = Time.unscaledTime + 0.25f;
            _renderer.RefreshKnowledge();
            if (_renderer.Lens == WorldLens.Settle || _renderer.Lens == WorldLens.Resources) _renderer.RefreshLens(LensField(_renderer.Lens), Highlights(_renderer.Lens));
        }
        bool moved = map.Magic != null && map.Magic.Version != _magicSeen;
        if (moved)
        {
            _magicSeen = map.Magic.Version;
            _renderer.SetLayers(_rivers, _leylines, _forecast ? world.ForecastNextAge() : null, _previous);
        }
        if (map.CivilizationVersion != _civilizationSeen)
        {
            _civilizationSeen = map.CivilizationVersion;
            _renderer.RefreshCivilization();
            moved = true;
        }
        int weatherVersion = CelestialWeatherSystemLogic.Instance?.RegionalWeatherVersion ?? 0;
        if (_weatherSeen != weatherVersion)
        {
            _weatherSeen = weatherVersion;
            if (_renderer.Lens == WorldLens.Weather) moved = true;
        }
        if (_feelingsSeen != world.FeelingsVersion && (_renderer.Lens == WorldLens.Feelings || _renderer.Lens == WorldLens.Danger))
        {
            _feelingsSeen = world.FeelingsVersion;
            moved = true;
        }
        if (_cultureSeen != CultureField.Version)
        {
            _cultureSeen = CultureField.Version;
            if (_renderer.Lens == WorldLens.Culture) moved = true;
        }
        if (moved) _renderer.RefreshLens(LensField(_renderer.Lens), Highlights(_renderer.Lens));
        if (world.UnitById(_selectedUnit) == null) _selectedUnit = -1;
        _renderer.SetState(_selected, _selectedMacro, Enumerable.Empty<HexCoord>(), Enumerable.Empty<HexCoord>());
        DrawHud(world);
    }

    private void DrawHud(WorldSystem world)
    {
        LayoutHud();
        _mapDock.SetState(_popup == Popup.Map);
        _unitsDock.SetState(_popup == Popup.Units, false, world.Map.Units.Count(WorldBattles.IsPlayers).ToString());
        var realm = world.Realm;
        _realmDock.SetState(_popup == Popup.Realm, realm.favoured == Expansion.Overextended);
        TooltipTrigger.Ensure(_realmDock.gameObject).SetCustom("Realm", $"Administrative strain: {realm.strain:P0}. Open capacity and border policy.");
        DrawKey(world);
        if (_popup == Popup.Units) DrawUnitList(world);
        else if (_popup != Popup.None) DrawMapMenu(world);
        DrawCard(world);
    }

    // The administration at a glance: capacity against load, strain and efficiency, which way of growing pays and
    // where it turns, what capacity is made of, the seats and what society will adopt next.
    private static string RealmText(WorldSystem world, RealmReport realm)
    {
        var text = new StringBuilder();
        string strain = realm.favoured == Expansion.Overextended ? TooltipText.Warn($"{realm.strain:P0}") : realm.strain > 0.8f ? $"{realm.strain:P0}" : TooltipText.Good($"{realm.strain:P0}");
        text.AppendLine(TooltipText.Heading("Administrative Capacity", RealmReport.Name(realm.favoured)));
        text.AppendLine(TooltipText.Row("Load / capacity", $"{realm.load:0.#} / {realm.capacity:0.#} (strain {strain}, efficiency {realm.efficiency:P0})"));
        string partly = realm.hexes > 0 ? $", and {realm.hexes} hexes of cells not yet core ({realm.deFacto} of them yours de facto; {realm.land:0.#} cells' worth in all)" : string.Empty;
        text.AppendLine(TooltipText.Row("Held", $"{realm.cells} core cells ({realm.adopted} adopted, {realm.claimed} claimed){partly}, {realm.averageLoad:0.00} load a cell"));
        var disputes = WorldHoldings.Disputes(world.Map);
        if (disputes.Count > 0)
        {
            int integrate = disputes.Count(d => WorldHoldings.CasusBelliOf(d, WorldAuthority.Player) == CasusBelli.Integrate);
            int recover = disputes.Count(d => WorldHoldings.CasusBelliOf(d, WorldAuthority.Player) == CasusBelli.Recover);
            int grievances = disputes.Count(d => d.Grievance(WorldAuthority.Player) > 0 && WorldHoldings.CasusBelliOf(d, WorldAuthority.Player) == CasusBelli.None);
            text.AppendLine(TooltipText.Row("Disputed", TooltipText.Warn($"{disputes.Count} cells: {recover} cores of yours to recover, {integrate} to integrate, {grievances} grievances")));
        }
        text.AppendLine(TooltipText.Row("Wide or tall", $"horizontal pays to ~{realm.comfortCells:0} cells, breaks even at ~{realm.breakEvenCells:0}, society stops at ~{realm.stopCells:0}"));
        text.AppendLine(TooltipText.Row("Held land", $"Coherence {realm.averageCoherence:P0}, beauty {WorldBeauty.Word(realm.averageBeauty)}, City Development {realm.averageDevelopment:0} on average"));
        text.AppendLine(TooltipText.Muted(realm.Advice));
        var sources = realm.capacitySources.Select(s => $"{s.source} {s.amount:+0.#;-0.#}").ToList();
        text.AppendLine(TooltipText.Row("Capacity from", string.Join(", ", sources)));
        var map = world.Map;
        var seats = map.territory?.Seats.Where(s => s.IsPlayers).ToList() ?? new List<TerritorySeat>();
        if (seats.Count > 0)
            text.AppendLine(TooltipText.Row("Seats", string.Join(", ", seats.Take(8).Select(s => $"{s.name} {s.held:0.#}/{s.maxCells}")) + (seats.Count > 8 ? $" and {seats.Count - 8} more" : string.Empty)));
        var rules = world.Rules.territory;
        string rate = world.BorderPolicy == BorderPolicy.Hold ? "held by policy"
            : realm.population >= 0 && realm.populationShare <= 0f ? TooltipText.Warn($"waits for {rules.adoptionMinPopulation} citizens in the Capital (now {realm.population})")
            : realm.adoptionRate > 0f ? $"{realm.adoptionRate:0.##} hexes per Seventh on average (7 make a cell){(realm.populationShare < 0.999f ? TooltipText.Muted($"; {realm.populationShare:P0} of the pace: more citizens, more settlers, full at {rules.adoptionFullPopulation}") : string.Empty)}"
            : "none now";
        text.AppendLine(TooltipText.Row("Adoption", !world.MapUnlocked || !world.IsOpen ? TooltipText.Muted("begins once the world map is open") : rate));
        if (realm.settling > 0 || realm.claiming > 0)
            text.AppendLine(TooltipText.Row("Held in part", $"{realm.settling} cells, hex by hex{(realm.claiming > 0 ? $"; {realm.claiming} older whole-cell claims" : string.Empty)}"));
        // What each seat settles next and when (the glowing hexes on the map).
        var next = world.AdoptionForecast().Where(p => p.sevenths.Count > 0).OrderBy(p => p.sevenths[0]).Take(4).ToList();
        if (next.Count > 0)
            text.AppendLine(TooltipText.Row("Next", string.Join(", ", next.Select(p => $"{world.Place(map[p.cell])} ({string.Join(", ", p.seats)}: next hex ~{p.sevenths[0]:0.#}, all ~{p.WholeCell:0.#} Sevenths)"))));
        return text.ToString().TrimEnd();
    }

    private static string LedgerText(WorldSystem world)
    {
        var map = world.Map;
        var text = new StringBuilder();
        var yields = world.TotalYields().Where(y => Mathf.Abs(y.Value) > 0.0001f).Select(y => $"{y.Value:+0.##} {y.Key}/s").ToList();
        text.AppendLine(TooltipText.Row("Land and towns yield", yields.Count > 0 ? string.Join(", ", yields) : "nothing yet"));
        text.AppendLine(TooltipText.Row("Explored", $"{map.ExploredCount} of {map.Count} cells"));
        text.AppendLine(TooltipText.Row("Settlements", $"{map.Settlements.Count(s => s.kind != SettlementKind.Capital)} (Major {WorldCivilization.MajorCount(map)}/{world.GovernmentCapacity})"));
        text.AppendLine(TooltipText.Row("Journeys completed", world.JourneysCompleted.ToString()));
        text.AppendLine(TooltipText.Muted($"Seed {map.seed}, generator v{map.Report.version}, catalog {map.Report.catalogHash}"));
        return text.ToString().TrimEnd();
    }

    private void DrawUnitList(WorldSystem world)
    {
        _rosterList.Begin(_popups[Popup.Units].sizeDelta.x - 2f * Inner - 14f);
        _rosterList.Text("Expeditions & units", HudIcon.Crew);
        var parties = world.Map.Units.Where(WorldBattles.IsPlayers).ToList();
        if (parties.Count == 0)
            _rosterList.Text("No parties in the field. Select a settlement to form an expedition.");
        foreach (var unit in parties)
        {
            int id = unit.id;
            string title = (id == _selectedUnit ? "Selected: " : string.Empty) + unit.name;
            string caption = $"{Status(world, unit, world.SpecOf(unit))}\n{Location(world, unit)}";
            if (world.IsExpedition(unit)) caption += $"  |  {Expeditions.PartySize(unit)} crew";
            _rosterList.Action(title, caption, HudIcon.Pin, () => { SelectUnit(id); ShowPopup(Popup.None); });
        }
        _rosterList.End();
    }

    private void DrawMapMenu(WorldSystem world)
    {
        var list = _menuLists[_popup];
        list.Begin(_popups[_popup].sizeDelta.x - 2f * Inner - 14f);
        if (_popup == Popup.Map)
        {
            list.Text(TooltipText.Heading("Map view"), HudIcon.Compass);
            list.Grid(true);
            foreach (var lens in CommonLenses)
            {
                var choice = lens;
                list.Choice(lens == WorldLens.Normal ? "Landscape" : WorldLenses.ShortName(lens),
                    lens == _renderer.Lens, () => SetLens(choice));
            }
            list.Grid(false);
            if (Section("More lenses", HudIcon.None,
                CommonLenses.Contains(_renderer.Lens) ? "Fertility, magic, trade, weather and more" : "Active: " + WorldLenses.ShortName(_renderer.Lens), list))
            {
                list.Grid(true);
                foreach (var lens in WorldLenses.All.Where(l => !CommonLenses.Contains(l)))
                {
                    var choice = lens;
                    list.Choice(WorldLenses.ShortName(lens), lens == _renderer.Lens, () => SetLens(choice));
                }
                list.Grid(false);
            }
            list.Text(TooltipText.Heading("Zoom"));
            list.Grid(true);
            foreach (var scale in new[] { WorldScale.Micro, WorldScale.Meso, WorldScale.Macro })
            {
                var choice = scale;
                list.Choice(ScaleLabel(scale), scale == WorldZoom.ScaleOf(_size), () => SetScale(choice));
            }
            list.Grid(false);
            if (Section("Map layers", HudIcon.None, "Rivers, leylines and Age forecasts", list))
            {
                list.Grid(true);
                list.Choice("Rivers", _rivers, () => { _rivers = !_rivers; ApplyLayers(); });
                list.Choice("Leylines", _leylines, () => { _leylines = !_leylines; ApplyLayers(); });
                list.Choice("Next Age forecast", _forecast, () => { _forecast = !_forecast; if (_forecast) _leylines = true; ApplyLayers(); });
                list.Choice("Previous Age", _previous, () => { _previous = !_previous; ApplyLayers(); });
                list.Grid(false);
            }
            if (Section("World ledger", HudIcon.Supply, null, list)) list.Text(LedgerText(world));
        }
        else if (_popup == Popup.Realm)
        {
            list.Text("Border policy", HudIcon.Home);
            // Once the Edicts are established the policy is The Borders stance: it changes by decree and then stands a while.
            var edicts = EdictSystem.IsEstablished ? EdictSystem.Instance : null;
            foreach (BorderPolicy policy in Enum.GetValues(typeof(BorderPolicy)))
            {
                var choice = policy;
                var option = EdictCatalog.BorderOption(policy);
                string why = edicts != null && policy != world.BorderPolicy && option != null ? edicts.WhyNotChange(EdictCatalog.Borders.id, option.id) : null;
                string decree = edicts != null ? $" Decreed by the Edicts ({EdictCatalog.Borders.title}{(option != null ? ": " + option.title : string.Empty)}).{(why != null ? " " + why : string.Empty)}" : string.Empty;
                list.Action(RealmReport.Name(policy), (policy == world.BorderPolicy ? "Selected. " : string.Empty) + RealmReport.Describe(policy) + decree,
                    HudIcon.Home, () => { EdictSystem.RequestBorderPolicy(choice, out _); Refresh(force: true); });
            }
            list.Text(RealmText(world, world.Realm));
        }
        list.End();
    }

    // ===== THE SELECTION CARD =====

    private static string Location(WorldSystem world, WorldUnit unit) => unit.Missing
        ? "Location unknown (missing in action)"
        : $"{world.Place(world.Map.Get(unit.coord))} | Hex {WorldUnits.MicroPosition(unit)}";

    private static string CaptainState(WorldUnit unit)
    {
        if (unit.leader == null || LegendProgress.Instance == null) return "Condition unknown";
        var state = LegendProgress.Instance.Composure(unit.leader);
        return state >= ComposureState.Spiraling ? TooltipText.Bad(state.ToString())
            : state == ComposureState.Fractured ? TooltipText.Warn(state.ToString()) : state.ToString();
    }

    private bool Section(string name, HudIcon icon, string summary = null, HudList list = null)
    {
        bool open = _expandedSections.Contains(name);
        (list ?? _selectionList).Action($"{(open ? "-" : "+")} {name}", summary, icon, () =>
        {
            if (!_expandedSections.Remove(name)) _expandedSections.Add(name);
            Refresh(force: true);
        });
        return open;
    }

    private void DrawCard(WorldSystem world)
    {
        var text = new StringBuilder();
        var actions = new List<(string label, string why, Action call)>();
        var unit = world.UnitById(_selectedUnit);
        if (unit != null) DescribeUnit(world, unit, text, actions);
        else if (_selectedMacro.HasValue) DescribeMacro(world, _selectedMacro.Value, text);
        else if (_selected.HasValue && world.Map.Get(_selected.Value) is WorldTile tile)
        {
            if (tile.settlement >= 0 && tile.settlement < world.Map.Settlements.Count) DescribeSettlement(world, world.Map.Settlements[tile.settlement], text, actions);
            else if (tile.enclave >= 0 && tile.enclave < world.Map.Enclaves.Count) DescribeEnclave(world, world.Map.Enclaves[tile.enclave], text, actions);
            else DescribeLand(world, tile, _selectedHex, text, actions);
        }
        if (text.Length == 0) { _card.gameObject.SetActive(false); return; }
        string identity = $"{_selectedUnit}/{_selected}/{_selectedMacro}/{_selectedHex}";
        bool changed = identity != _cardIdentity;
        _cardIdentity = identity;
        float width = Mathf.Min(500f, _canvasRect.rect.width - Pad * 2f);
        // Beside the dock when the screen is wide enough, above it when not; clear of the notices at the top.
        float dockHeight = _canvasRect.rect.width >= _dock.sizeDelta.x + width + 3f * Pad ? 0f : _dock.sizeDelta.y + 8f;
        _card.sizeDelta = new Vector2(width, Mathf.Max(160f, _canvasRect.rect.height - dockHeight - 110f));
        _card.anchoredPosition = new Vector2(-Pad, Pad + dockHeight);
        _selectionList.Begin(width - 2f * Inner - 14f);
        // Who or what it is, where, what it is doing and how it fares; then its orders; the long reports fold away.
        if (unit != null)
        {
            var spec = world.SpecOf(unit);
            if (!WorldBattles.IsPlayers(unit))
            {
                // An enemy band: what it is, its temper in its colour, what it is doing, and how spent it is.
                _selectionList.Text(BandTitle(world, unit), HudIcon.Compass);
                _selectionList.Text($"{BandStanceWord(unit)}  {TooltipText.Muted("·")}  {WorldPursuit.Status(unit)}\n{TooltipText.Muted(Location(world, unit))}", HudIcon.Pin);
                _selectionList.Text($"{unit.creatures} strong  {TooltipText.Muted("·")}  Endurance {WorldPursuit.Bar(unit)}", HudIcon.Supply);
            }
            else
            {
                _selectionList.Text(TooltipText.Heading(unit.name) + (spec != null ? "  " + TooltipText.Muted(spec.role.ToString()) : string.Empty), HudIcon.Compass);
                _selectionList.Text($"{Status(world, unit, spec)}\n{TooltipText.Muted(Location(world, unit))}", HudIcon.Pin);
                if (!unit.Missing && spec != null)
                {
                    if (spec.supplyCapacity > 0f && spec.supplyUsePerSeventh > 0f)
                        _selectionList.Meter("Rations", unit.supplies / spec.supplyCapacity, $"{unit.supplies:0.#} / {spec.supplyCapacity:0.#}", unit.supplies < spec.supplyCapacity * 0.25f);
                    _selectionList.Meter("Fatigue", unit.fatigue / 100f, $"{unit.fatigue:0}%", unit.fatigue >= 75f);
                    if (unit.attrition > 0f) _selectionList.Meter("Attrition", unit.attrition / 100f, $"{unit.attrition:0}%", unit.attrition >= 50f);
                    if (unit.winded || unit.sprinting || unit.endurance < 99.5f)
                        _selectionList.Meter("Endurance", unit.endurance / 100f, unit.winded ? "Winded" : $"{unit.endurance:0}%", unit.winded);
                }
            }
        }
        else
        {
            string report = text.ToString();
            int close = report.IndexOf("</align>", StringComparison.Ordinal);
            int firstLine = report.IndexOf('\n', close >= 0 ? close : 0);
            _selectionList.Text(firstLine >= 0 ? report.Substring(0, firstLine) : report, HudIcon.Pin);
        }
        if (actions.Count > 0)
        {
            var forming = actions.Where(a => a.label.StartsWith("Director:") || a.label.StartsWith("Charter:") || a.label.StartsWith("Form ")).ToList();
            if (forming.Count > 0)
            {
                _selectionList.Text(TooltipText.Heading("Form a party"), HudIcon.Crew);
                _selectionList.Meter("Party capacity", world.ExpeditionSlotsUsed / (float)Mathf.Max(1, world.ExpeditionSlots),
                    $"{world.ExpeditionSlotsUsed} / {world.ExpeditionSlots}", world.ExpeditionSlotsUsed >= world.ExpeditionSlots);
                foreach (var action in forming) DrawAction(action);
                _selectionList.Grid(false);
            }
            actions = actions.Except(forming).ToList();
            _selectionList.Text(TooltipText.Heading("Orders"));
            _selectionList.Grid(true);
            var available = actions.Where(a => a.why == null).ToList();
            bool expedition = unit != null && world.IsExpedition(unit);
            foreach (var action in available.Where(a => !expedition || ExpeditionActionGroup(a.label) == null)) DrawAction(action);
            _selectionList.Grid(false);
            if (expedition)
                foreach (string group in new[] { "Outfit party", "Battle preparation", "Cargo & retirement" })
                {
                    var grouped = available.Where(a => ExpeditionActionGroup(a.label) == group).ToList();
                    if (grouped.Count == 0 || !Section(group, group == "Outfit party" ? HudIcon.Crew : HudIcon.Supply, $"{grouped.Count} options")) continue;
                    _selectionList.Grid(true);
                    foreach (var action in grouped) DrawAction(action);
                    _selectionList.Grid(false);
                }
            int unavailable = actions.Count(a => a.why != null);
            if (unavailable > 0 && Section("Unavailable orders", HudIcon.Compass, $"{unavailable} - view requirements"))
            {
                _selectionList.Grid(true);
                foreach (var action in actions.Where(a => a.why != null)) DrawAction(action);
                _selectionList.Grid(false);
            }
        }
        if (unit != null)
        {
            if (world.IsExpedition(unit))
            {
                _selectionList.Text($"Captain: {unit.leader ?? "Unassigned"}  {TooltipText.Muted(CaptainState(unit))}", HudIcon.Crown);
                if (Section("Crew", HudIcon.Crew, $"{Expeditions.PartySize(unit)} travelling together"))
                    DrawCrew(world, unit);
            }
            if (Section("Details", HudIcon.Compass, WorldBattles.IsPlayers(unit) ? "Rations, fatigue, sight and what it can do" : "Its temper, its mind and how far it chases"))
                _selectionList.Text(text.ToString());
        }
        else
        {
            if (Section("Details", HudIcon.Compass)) _selectionList.Text(text.ToString());
            if (_selected.HasValue && world.Map.Get(_selected.Value) is WorldTile ground && Section("Terrain & resources", HudIcon.Pin))
                _selectionList.Text(HoverText(world, ground, false));
        }
        _selectionList.End();
        _card.sizeDelta = new Vector2(width, Mathf.Min(_card.sizeDelta.y, _selectionList.Height + 2f * Inner));
        _card.gameObject.SetActive(true);
        if (changed) _selectionList.ResetScroll();
    }

    // How a party fares, in one line: rations, fatigue and attrition, amber when they run low or high.
    private static string Vitals(WorldUnit unit, UnitSpec spec)
    {
        var parts = new List<string>();
        if (spec.supplyCapacity > 0f && spec.supplyUsePerSeventh > 0f)
        {
            string rations = unit.supplies <= 0f ? "Starving" : $"Rations {unit.supplies:0.#}";
            parts.Add(unit.supplies < spec.supplyCapacity * 0.25f ? TooltipText.Warn(rations) : rations);
        }
        string fatigue = $"Fatigue {unit.fatigue:0}";
        parts.Add(unit.fatigue >= 75f ? TooltipText.Warn(fatigue) : fatigue);
        string attrition = $"Attrition {unit.attrition:0}";
        parts.Add(unit.attrition >= 50f ? TooltipText.Warn(attrition) : attrition);
        if (unit.winded || unit.sprinting || unit.endurance < 99.5f) parts.Add(unit.winded ? TooltipText.Warn("Winded") : $"Endurance {unit.endurance:0}{(unit.sprinting ? " (running)" : string.Empty)}");
        return string.Join(TooltipText.Muted("  ·  "), parts);
    }

    // The map key: the reading and lens in force, then what the lens's colours mean (or how to drive the map).
    private static string ScaleLabel(WorldScale scale) => scale == WorldScale.Micro ? "Local" : scale == WorldScale.Meso ? "Region" : "Atlas";

    private void DrawKey(WorldSystem world)
    {
        var lens = _renderer.Lens;
        string head = TooltipText.Heading(ScaleLabel(WorldZoom.ScaleOf(_size)) + " / " + (lens == WorldLens.Normal ? "Landscape" : WorldLenses.ShortName(lens)));
        string body;
        if (_surveyPick) body = TooltipText.Warn("Click a meso hex to survey it (Esc to cancel).");
        else if (lens != WorldLens.Normal) body = TooltipText.Muted(WorldLenses.Legend(lens)) + "\n" + _renderer.LensKeyText();
        else body = $"<color=#{Hud.Hex(Hud.Muted)}>" + (world.UnitById(_selectedUnit) != null
            ? "Right click: move  /  Shift + right click: survey\nDrag: pan  /  Wheel: zoom  /  Esc: back"
            : $"Click: inspect  /  Drag: pan  /  Wheel: zoom\n{KeyBindings.Keys("next-idle")}: next idle party  /  Esc: back") + "</color>";
        string expandedKey = body + "\n" + WorldExplorationAppearance.Legend;
        // The plans on the map, once there are any.
        var plans = new List<string>();
        if (_surveyPick || world.Map.Units.Any(u => WorldBattles.IsPlayers(u) && (u.surveying || u.surveyPaused)))
            plans.Add($"<color=#{Hud.Hex(WorldRenderer.PlanColor)}>Violet numbers</color> the order a survey walks its hexes (<color=#{Hud.Hex(WorldRenderer.PlanActiveColor)}>gold</color> under way)");
        if (world.AdoptionForecast().Count > 0)
            plans.Add($"<color=#{Hud.Hex(world.Rules.borderColor)}>Glowing hexes</color> your society settles next, ~Sevenths until each");
        if (plans.Count > 0) expandedKey += $"\n<color=#{Hud.Hex(Hud.Muted)}>{string.Join("  /  ", plans)}</color>";
        // Once bands are in sight: what their colours and marks mean.
        if (lens == WorldLens.Normal && !_surveyPick && world.Map.Units.Any(u => !WorldBattles.IsPlayers(u) && world.Sees(u)))
            expandedKey += $"\n<color=#{Hud.Hex(WorldBattles.Red)}>Red</color> hunts you  /  <color=#{Hud.Hex(WorldBattles.Orange)}>Orange</color> wary  /  <color=#{Hud.Hex(WorldBattles.Timid)}>Pale</color> flees\n" +
                    $"<color=#{Hud.Hex(Hud.Muted)}>Paw creature  /  Fish water creature  /  Burst Atonalis  /  Blob Formless Mass  /  Cocoon  /  Shield humans  /  Horns demihumans  /  Hood humanoids. Right click a band to give chase.</color>";
        TooltipTrigger.Ensure(_guide.gameObject).SetCustom("Map guide", FlowText(head + "\n" + expandedKey));
        _guide.SetState(lens != WorldLens.Normal, _surveyPick);
        _guide.RefreshPresentation();
    }

    private void DrawCrew(WorldSystem world, WorldUnit unit)
    {
        foreach (string member in Expeditions.Members(unit))
        {
            bool captain = member == unit.leader;
            string state = LegendProgress.Instance != null ? LegendProgress.Instance.Composure(member).ToString() : "Condition unknown";
            _selectionList.Text($"{member} - {(captain ? "Captain (Director)" : "Companion")}\n{state}", captain ? HudIcon.Crown : HudIcon.Crew);
            if (unit.Missing) continue;
            if (!captain)
                _selectionList.Action("Appoint captain", member, HudIcon.Crown, () => { world.SetDirector(unit, member); Refresh(force: true); });
            if ((world.Map.Get(unit.coord)?.settlement ?? -1) >= 0)
            {
                string why = world.WhyNotSendHome(unit, member);
                _selectionList.Action("Send home", why ?? member, HudIcon.Home,
                    () => { world.SendHome(unit, member); Refresh(force: true); }, why == null);
            }
        }
        _selectionList.Text(unit.Missing ? "The party's whereabouts are unknown." : "All crew members are at the location shown above.");
    }

    private void DrawAction((string label, string why, Action call) action)
    {
        var (name, detail) = Split(action.label);
        bool companion = name.StartsWith("Companion: ", StringComparison.Ordinal);
        if (name.StartsWith("Charter: ", StringComparison.Ordinal))
        {
            _selectionList.Grid(false);
            if (Section("Purpose: " + WorldSystem.CharterName(_formCharter, plural: false), HudIcon.Compass, "Choose what this party will do"))
                foreach (ExpeditionCharter charter in Enum.GetValues(typeof(ExpeditionCharter)))
                {
                    var choice = charter;
                    var why = WorldSystem.Instance.WhyNotCharter(choice);
                    _selectionList.Action(WorldSystem.CharterName(choice, plural: false), why ?? WorldSystem.CharterDescription(choice), HudIcon.Compass,
                        () => { _expandedSections.Remove("Purpose: " + WorldSystem.CharterName(_formCharter, plural: false)); _formCharter = choice; Refresh(force: true); }, why == null);
                }
            _selectionList.Grid(true);
            return;
        }
        if (companion || name.StartsWith("Director: ", StringComparison.Ordinal))
        {
            string key = companion ? "Choose companion" : "Choose captain";
            string selected = companion ? _companionPick : _formDirector;
            _selectionList.Grid(false);
            if (Section(key, companion ? HudIcon.Crew : HudIcon.Crown, selected ?? "No free legends"))
            {
                var candidates = WorldSystem.Instance.Candidates();
                if (candidates.Count == 0) _selectionList.Text("No legend is free to go.");
                foreach (string candidate in candidates)
                    _selectionList.Action(candidate, candidate == selected ? "Selected" : "Select this legend", HudIcon.Crew, () =>
                    {
                        if (companion) _companionPick = candidate; else _formDirector = candidate;
                        _expandedSections.Remove(key);
                        Refresh(force: true);
                    });
            }
            _selectionList.Grid(true);
            return;
        }
        string caption = detail;
        if (action.why != null) caption = string.IsNullOrEmpty(detail) ? $"Unavailable: {action.why}" : $"{detail}\nUnavailable: {action.why}";
        _selectionList.Action(name, caption, ActionIcon(name), () => { action.call(); Refresh(force: true); }, action.why == null);
    }

    private static string ExpeditionActionGroup(string label)
    {
        if (label.StartsWith("Prepare ") || label.StartsWith("Rest and integrate") || label.StartsWith("Initial advance:")) return "Battle preparation";
        if (label.StartsWith("Companion:") || label.StartsWith("Add ") || label.StartsWith("Take on settlers") || label.StartsWith("Kit:")) return "Outfit party";
        if (label.StartsWith("Discard ") || label.StartsWith("Disband")) return "Cargo & retirement";
        return null;
    }

    private static HudIcon ActionIcon(string name)
    {
        if (name.Contains("Captain") || name.Contains("captain") || name.Contains("Director")) return HudIcon.Crown;
        if (name.Contains("Companion") || name.Contains("companion") || name.Contains("expedition") || name.StartsWith("Add ")) return HudIcon.Crew;
        if (name.Contains("camp") || name.Contains("rations") || name.Contains("Forage")) return HudIcon.Supply;
        if (name.Contains("Return") || name.Contains("home") || name.Contains("Found")) return HudIcon.Home;
        return HudIcon.Compass;
    }

    private static (string name, string detail) Split(string label)
    {
        int cut = label.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 && label.EndsWith(")", StringComparison.Ordinal) ? (label.Substring(0, cut), label.Substring(cut + 2, label.Length - cut - 3)) : (label, null);
    }
    // ===== ENEMY BANDS =====

    private static Color BandTone(WorldUnit band, float alpha)
    {
        var c = WorldBattles.ColorOf(band);
        return new Color(c.r, c.g, c.b, alpha);
    }

    // "Grey Wolf (4)  Creatures · Regular", its name in its stance's colour.
    private static string BandTitle(WorldSystem world, WorldUnit band) =>
        $"<b><color=#{Hud.Hex(WorldBattles.ColorOf(band))}>{band.name}</color></b>  " +
        TooltipText.Muted($"{WorldPursuit.IdentityName(band.identity)} · {band.intelligence}");

    // Its temper in one coloured word.
    private static string BandStanceWord(WorldUnit band) =>
        $"<color=#{Hud.Hex(WorldBattles.ColorOf(band))}>{(band.stance == EnemyStance.Timid ? "Timid" : WorldBattles.Angry(band) ? (band.stance == EnemyStance.Hostile ? "Hostile" : "Provoked") : "Wary")}</color>";

    // A band's report: what it is, what it means to you and why, how it thinks, how far it will chase, how spent it is.
    private static void DescribeBand(WorldSystem world, WorldUnit band, StringBuilder text)
    {
        var gen = world.Settings.generation;
        var species = WorldPursuit.Species(gen, band);
        text.AppendLine(TooltipText.Heading(band.name, WorldPursuit.IdentityName(band.identity)));
        if (!string.IsNullOrEmpty(species?.description)) text.AppendLine(TooltipText.Quote($"<i>{species.description}</i>"));
        text.AppendLine(TooltipText.Row("Now", WorldPursuit.Status(band)));
        text.AppendLine(TooltipText.Row("Temper", $"<color=#{Hud.Hex(WorldBattles.ColorOf(band))}>{WorldPursuit.StanceText(band)}</color>"));
        if (band.identity == BandIdentity.Atonalis && band.atonalPath != AtonalPath.None)
            text.AppendLine(TooltipText.Row("Path", band.hybridPath != AtonalPath.None
                ? $"{band.atonalPath}, a hybrid with {band.hybridPath}: {AtonalPaths.Of(band.atonalPath).summary}"
                : $"{band.atonalPath}: {AtonalPaths.Of(band.atonalPath).summary}"));
        if (band.identity == BandIdentity.Atonalis && band.fed != null && band.fed.Total > 0.01f)
            text.AppendLine(TooltipText.Row("Born of", "\n" + WorldSuffering.Meter(band.fed, band.fed.Total)));
        text.AppendLine(TooltipText.Row("Mind", WorldPursuit.IntelligenceText(band.intelligence)));
        if (band.habitat != CreatureHabitat.Land)
            text.AppendLine(TooltipText.Row("Lives", band.habitat == CreatureHabitat.Water ? "in the water: it never leaves it, so the shore is as far as it follows" : "on land and in the water alike"));
        text.AppendLine(TooltipText.Row("Numbers", band.creatures.ToString()));
        // Its Symphony (its instincts as cards) and how it weighs against your nearest party.
        var bandSide = world.SideOf(band);
        if (bandSide != null)
        {
            var yours = world.NearestOfYours(band);
            var quick = yours != null ? world.QuickPreview(yours, band) : null;
            bool yoursAttack = yours != null && world.WouldAttack(yours, band);
            string against = quick == null ? string.Empty
                : $"; against {yours.name}: {quick.Side(!yoursAttack).Strength} vs {quick.Side(yoursAttack).Strength} ({SymphonyPower.Words(yoursAttack ? 1f - quick.balance : quick.balance)} for it)";
            text.AppendLine(TooltipText.Row("Symphony", $"strength {SymphonyPower.Rate(bandSide, world.Combat).power:0}, {SymphonyDecks.Describe(bandSide.deck)}{against}"));
        }
        if (band.captives != null && band.captives.Count > 0)
            text.AppendLine(TooltipText.Row("Holds captive", TooltipText.Warn($"{string.Join(", ", band.captives)}: destroy it to free them")));
        if (band.identity == BandIdentity.FormlessMass)
        {
            // Its emotional meter: what it has drunk decides the Path it hatches into (the profile it most resembles, or a hybrid).
            var (becoming, hybrid) = EmotionalProfile.Match(band.fed);
            string into = WorldSuffering.PathName(becoming, hybrid);
            text.AppendLine(TooltipText.Row("Fed", band.bandActivity == BandActivity.Cocooned
                ? TooltipText.Warn($"cocooned: a Nascent {into} hatches in about {Sevenths(band.cocoon)} unless you destroy it")
                : $"{band.fed?.Total ?? 0f:0.##} of {WorldSuffering.CocoonAt:0.#} (it cocoons then; as it is it would hatch into a {into})"));
            if (band.fed != null && band.fed.Total > 0.01f) text.AppendLine(WorldSuffering.Meter(band.fed, WorldSuffering.CocoonAt));
        }
        if (band.identity == BandIdentity.Atonalis && band.binding != SpellBinding.Unattuned)
            text.AppendLine(TooltipText.Row("Binding", $"{band.binding} (its weakness)"));
        text.AppendLine(TooltipText.Row("Endurance", WorldPursuit.Bar(band)));
        text.AppendLine(TooltipText.Row("Chases", band.response == ThreatResponse.DefendsTerritory
            ? $"only inside its territory ({Hexes(band.territoryRadius)} around home), at most {Hexes(band.pursuitLimit)}"
            : $"up to {Hexes(band.pursuitLimit)}{(band.chaseTiles > 0 ? $" ({Hexes(band.chaseTiles)} run so far)" : string.Empty)}; roams {Hexes(band.territoryRadius)} around home"));
        var quarry = band.quarryId >= 0 ? world.UnitById(band.quarryId) : null;
        if (quarry != null && world.Sees(quarry)) text.AppendLine(TooltipText.Row("After", quarry.name));
        if (band.denLife > 0f) text.AppendLine(TooltipText.Row("Out from its den", $"heads home in about {Sevenths(band.denLife)}"));
        else if (band.denLife < 0f) text.AppendLine(TooltipText.Row("Out from its den", "heading home"));
        if (!string.IsNullOrEmpty(band.threat)) text.AppendLine(TooltipText.Row("Sent by", gen.Threat(band.threat)?.name ?? band.threat));
        text.AppendLine(TooltipText.Muted(band.identity == BandIdentity.FormlessMass
            ? "Lingering suffering pooled into a slime. It creeps to where the land suffers and drinks it; a party whose nerve is shaken draws it. Kill it before it cocoons, or an Atonalis hatches."
            : band.stance == EnemyStance.Timid
            ? "Select one of your parties and right click it to give chase. It runs and tires faster than your people: keep after it until it is winded, and it fights half-beaten. What falls is carried home as a hunt."
            : band.identity == BandIdentity.Atonalis && band.atonalPath != AtonalPath.Carnalix
            ? "Select one of your parties and right click it to attack. Lose to it, and legends that break or are left behind are dragged away captive until it is destroyed."
            : "Select one of your parties and right click it to attack. Chased, run: a pursuer that runs itself out gives up or fights spent, and a thinking one will not follow you across a river."));
    }

    private static void DescribeUnit(WorldSystem world, WorldUnit unit, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        if (!WorldBattles.IsPlayers(unit))
        {
            DescribeBand(world, unit, text);
            return;
        }
        var spec = world.SpecOf(unit);
        var map = world.Map;
        var gen = world.Settings.generation;
        text.AppendLine(TooltipText.Heading(unit.name, spec != null ? spec.role.ToString() : string.Empty));
        if (spec != null && !string.IsNullOrEmpty(spec.description)) text.AppendLine(TooltipText.Quote($"<i>{spec.description}</i>"));
        text.AppendLine(TooltipText.Row("Now", Status(world, unit, spec)));
        if (unit.Missing)
        {
            // Gone to ground: nothing to order until it turns up.
            text.AppendLine(TooltipText.Muted($"No one knows where {unit.leader ?? unit.name} is. Word should come in about {Sevenths(unit.missingSevenths)}, from the nearest ground of your territory."));
            return;
        }
        if (spec != null)
        {
            // How it fares: rations, weariness, wear, and what they cost its pace.
            if (spec.supplyCapacity > 0f && spec.supplyUsePerSeventh > 0f)
            {
                string rations = $"{unit.supplies:0.#} of {spec.supplyCapacity:0.#}, about {Sevenths(WorldUnits.RationSevenths(unit, spec))}";
                text.AppendLine(TooltipText.Row("Rations", unit.supplies <= 0f ? TooltipText.Warn("none: it is starving") : unit.supplies < spec.supplyCapacity * 0.25f ? TooltipText.Warn(rations) : rations));
            }
            string fatigue = $"{unit.fatigue:0} of 100, {WorldUnits.FatigueWord(unit.fatigue)}";
            text.AppendLine(TooltipText.Row("Fatigue", unit.fatigue >= 75f ? TooltipText.Warn(fatigue) : fatigue));
            string attrition = $"{unit.attrition:0} of 100, {WorldUnits.AttritionWord(unit.attrition)}";
            text.AppendLine(TooltipText.Row("Attrition", unit.attrition >= 50f ? TooltipText.Warn(attrition) : attrition));
            float pace = WorldUnits.Efficiency(unit);
            if (pace < 0.995f) text.AppendLine(TooltipText.Row("Pace", TooltipText.Warn($"{pace:P0} of its best ({Hindrances(unit)})")));
            text.AppendLine(TooltipText.Row("Endurance", WorldPursuit.Bar(unit) + (unit.winded ? TooltipText.Warn("  winded") : unit.sprinting ? $"  running, x{WorldPursuit.RunPace(unit):0.##} pace" : string.Empty)));
            var at = UnitSurroundings.Of(map, unit);
            if (at.held) text.AppendLine(TooltipText.Muted(at.settlement ? "In a settlement: it takes on rations from your stores and rests well." : "Inside your authority: your stores keep it in rations."));
            else if (unit.Camping) text.AppendLine(TooltipText.Muted(at.fertility > 0.05f ? $"The camp gathers about {spec.forageRationsPerSeventh * at.fertility:0.#} rations a Seventh from the land." : "The land here gives the camp nothing to eat."));
            text.AppendLine(TooltipText.Row("Stamina", $"{spec.stamina:0.#} travel fatigue per Seventh"));
            text.AppendLine(TooltipText.Row("Sight", Hexes(WorldUnits.Sight(map, unit, spec, world.Rules))));
            string can = AbilityWords(spec);
            if (can.Length > 0) text.AppendLine(TooltipText.Row("Can", can));
            if (spec.rewardMultiplier > 1f) text.AppendLine(TooltipText.Row("Brings back", $"x{spec.rewardMultiplier:0.#} from sites and forage"));
        }
        bool expedition = world.IsExpedition(unit);
        if (expedition) DescribeParty(world, unit, text);
        if (WorldBattles.IsPlayers(unit)) DescribeSymphony(world, unit, text);
        if (spec == null) return;
        // An expedition in one of your settlements is outfitted there; in the field it works the land.
        var cell = map.Get(unit.coord);
        bool home = expedition && cell != null && cell.settlement >= 0;
        // What it can do on the land: survey a meso hex (this one, or one picked on the map), its other abilities, then
        // how it looks after itself.
        if ((WorldUnits.Can(spec, UnitAbility.SurveyMeso) || WorldUnits.Can(spec, UnitAbility.Survey))) SurveyActions(world, unit, text, actions);
        foreach (var ability in UnitAbilities.All)
        {
            if (home || !WorldUnits.Can(spec, ability.ability) || ability.task == UnitTask.Improve || ability.task == UnitTask.Festival) continue;
            if (ability.task == UnitTask.Survey || ability.task == UnitTask.SurveyMeso) continue;
            // Harvesting and planting show only where they could be done (a site here; seeds in the packs).
            if (ability.task == UnitTask.Harvest && WorldResources.SiteAt(map, map.Get(unit.coord)) == null) continue;
            if (ability.task == UnitTask.Plant && (unit.seeds == null || unit.seeds.Count == 0)) continue;
            // Investigating shows only on ruins still to be read.
            var ruinHere = ability.task == UnitTask.Investigate ? WorldRuins.At(map, unit.coord) : null;
            if (ability.task == UnitTask.Investigate && (ruinHere == null || ruinHere.investigated)) continue;
            string detail = ability.task == UnitTask.Forage ? ForageDetail(world, unit)
                : ability.task == UnitTask.Harvest ? HarvestDetail(world, unit)
                : ability.task == UnitTask.Investigate ? $"of {ruinHere.name}, {Sevenths(world.WorkSevenths(unit, ability))}"
                : $"{world.NextSeeds(unit)}, {Sevenths(world.WorkSevenths(unit, ability))}";
            // At a den the harvest is a hunt.
            string label = ability.task == UnitTask.Harvest && WorldEcology.SpeciesAt(world.Settings.generation, WorldResources.SiteAt(map, cell)) != null ? "Hunt" : ability.name;
            actions.Add(($"{label} ({detail})", world.WhyNotWork(unit, ability), () => world.Work(unit, ability)));
        }
        if (WorldUnits.Can(spec, UnitAbility.AutoExplore))
            actions.Add((unit.autoExplore ? "Stop exploring" : "Explore by itself", null, () => world.SetAutoExplore(unit, !unit.autoExplore)));
        if (unit.Camping) actions.Add(("Break camp", null, () => world.BreakCamp(unit)));
        else actions.Add(("Make camp (rest, eat less, live off the land)", unit.Working ? "Already at work." : null, () => world.MakeCamp(unit)));
        if (spec.supplyUsePerSeventh > 0f && cell != null && cell.settlement < 0)
        {
            var (back, hexes, wayFatigue) = ReturnWay(world, unit);
            actions.Add(($"Return for rations{(back == null ? $" ({Hexes(hexes)}, about {Sevenths(WorldUnits.SeventhsFor(unit, spec, wayFatigue))})" : string.Empty)}", back, () => world.ReturnToResupply(unit)));
        }
        switch (spec.role)
        {
            case UnitRole.Expedition:
                // The legends improve the land themselves (there are no builders) once the civilization knows how.
                var here = map.Get(unit.coord);
                if (world.CanImprove && WorldUnits.Can(spec, UnitAbility.Improve) && here != null && WorldUnits.IsHotspot(map, world.Settings.generation, here))
                    actions.Add(($"Improve hotspot (level {here.improvement + 1}, {WorldSystem.CostText(world.ExpeditionRules.improveCost)}, {Sevenths(world.WorkSevenths(unit, UnitAbilities.Improve))})", world.WhyNotImprove(unit), () => world.Improve(unit)));
                // A cultural party celebrates where it stands, in one of your settlements.
                if (WorldUnits.Can(spec, UnitAbility.Celebrate))
                {
                    string table = world.FestivalCostText(unit);
                    actions.Add(($"Hold a festival ({(table != null ? table + ", " : string.Empty)}{Sevenths(world.WorkSevenths(unit, UnitAbilities.Festival))})", world.WhyNotFestival(unit), () => world.HoldFestival(unit)));
                    LocalCultureCard.Party(world, unit, text, actions);
                }
                PartyActions(world, unit, home, actions);
                break;
        }
        // Its cards, as the Spell Builder draws them.
        var ownSide = world.SideOf(unit);
        if (ownSide != null && WorldBattles.Fights(unit))
            foreach (var doctrine in world.BattleDoctrines.Where(d => d.id != ownSide.doctrine && (ownSide.doctrine != null || d.stance != ownSide.stance)))
            {
                string id = doctrine.id;
                actions.Add(($"Initial advance: {ownSide.stance}; deploy {doctrine.name}", null, () => world.ChooseBattleDoctrine(unit, id)));
            }
        if (ownSide != null && ownSide.deck.Count > 0)
            actions.Add(($"See its Symphony ({ownSide.deck.Count} cards)", null, () => SymphonyWindow.ShowUnit(world, unit)));
        // Running, and the bands in sight it could give chase to (nearest first).
        if (unit.quarryId >= 0) actions.Add(("Stop the chase", null, () => { world.Halt(unit); world.SetSprint(unit, false); }));
        else actions.Add((unit.sprinting ? "Walk (stop running)" : "Run (faster, spends endurance)", unit.winded ? "Winded: catching its breath." : null, () => world.SetSprint(unit, !unit.sprinting)));
        var from = WorldUnits.MicroPosition(unit);
        int sight = WorldUnits.Sight(map, unit, spec, world.Rules);
        foreach (var band in map.Units.Where(b => !WorldBattles.IsPlayers(b) && b.id != unit.quarryId && world.Sees(b) && HexCoord.Distance(from, WorldUnits.MicroPosition(b)) <= sight + 1)
                     .OrderBy(b => HexCoord.Distance(from, WorldUnits.MicroPosition(b))).ThenBy(b => b.id).Take(3))
        {
            var target = band;
            // The odds on the order itself (Civ VI's strengths, Stellaris's word), and the full pre-battle screen.
            var quick = world.QuickPreview(unit, band);
            string odds = quick == null ? string.Empty : $", {quick.Side(world.WouldAttack(unit, band)).Strength} vs {quick.Side(!world.WouldAttack(unit, band)).Strength}, {SymphonyPower.Words(world.WouldAttack(unit, band) ? quick.balance : 1f - quick.balance)}";
            actions.Add(($"{(WorldBattles.Angry(band) ? "Attack" : "Hunt")} {band.name} ({Hexes(HexCoord.Distance(from, WorldUnits.MicroPosition(band)))} away){odds}",
                world.WhyNotEngage(unit, band), () => world.EngageBand(unit, target)));
            if (quick != null)
            {
                actions.Add(($"Battle preview: {band.name}", null, () => BattleWindow.ShowPreview(world.PreviewBattle(unit, target, 20), world.WouldAttack(unit, target))));
                actions.Add(($"Its instincts: {band.name}", null, () => SymphonyWindow.ShowUnit(world, target)));
            }
        }
        if (unit.Moving) actions.Add(("Halt", null, () => world.Halt(unit)));
        else text.AppendLine(TooltipText.Muted(WorldUnits.Can(spec, UnitAbility.Survey) || WorldUnits.Can(spec, UnitAbility.SurveyMeso)
            ? "Right click the map to send it (zoomed in: to that hex; zoomed out: to the heart of the cell); Shift + right click to survey that meso hex. '.' picks the next idle unit."
            : "Right click the map to send it (zoomed in: to that hex; zoomed out: to the heart of the cell)."));
    }

    // Surveying: a cell is explored once the party has seen all seven of its hexes in passing; sent to survey one, it
    // walks each hex in turn, slower, and far likelier to turn up an event or spare resources.
    private static void SurveyActions(WorldSystem world, WorldUnit unit, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var map = world.Map;
        var x = world.ExpeditionRules;
        bool auto = WorldUnits.Can(world.SpecOf(unit), UnitAbility.SurveyMeso);
        if (unit.surveying || unit.surveyPaused)
        {
            var target = map.Get(unit.surveyCell);
            string done = $"{world.Place(target)}: {WorldMap.SurveyedHexes(target)} of {WorldMap.OpenHexes(target)} hexes surveyed, {world.SurveyProgress(unit):P0} done";
            text.AppendLine(TooltipText.Row("Survey", unit.surveying ? done : TooltipText.Muted($"set aside, {done}")));
            if (unit.surveyPaused)
                actions.Add(($"Resume survey (about {Sevenths(world.SurveySevenths(unit, unit.surveyCell))} of work left)", world.WhyNotResumeSurvey(unit), () => world.ResumeSurvey(unit)));
            actions.Add((unit.surveying ? "Stop surveying" : "Forget the survey", null, () => world.StopSurvey(unit)));
        }
        if (!unit.surveying)
        {
            // Survey the cell it stands in (while any of it is left), or pick another on the map.
            var here = map.Get(unit.coord);
            int left = world.HexesToSurvey(here);
            bool resumesHere = unit.surveyPaused && unit.surveyCell == unit.coord;
            if (left > 0 && !resumesHere && here.settlement < 0) actions.Add(($"Survey here ({Hexes(left)}, about {Sevenths(world.SurveySevenths(unit, unit.coord))})", world.WhyNotSurveyCell(unit, unit.coord), () => world.SurveyCell(unit, unit.coord)));
            actions.Add((_surveyPick ? "Pick a cell to survey (click it; Esc cancels)" : "Survey elsewhere (then click a cell)", null, () =>
            {
                _surveyPick = !_surveyPick;
            }));
        }
        if (auto)
            actions.Add((unit.autoSurvey ? "Stop surveying by itself" : "Survey by itself (cell after cell, resting and resupplying as needed)", unit.autoSurvey ? null : world.WhyNotAutoSurvey(unit),
                () => world.SetAutoSurvey(unit, !unit.autoSurvey)));
        if (unit.surveying) return;
        if (x != null)
            text.AppendLine(TooltipText.Muted($"A cell is explored once the party has seen all seven of its hexes in passing. Surveying walks every hex: slower, but {x.surveyEventChance:P0} likely to turn up an event and {x.surveyCacheChance:P0} spare resources (passing by: {x.passingEventChance:P0} and {x.passingCacheChance:P0}), and it identifies what grows and dwells there. A move within the cell keeps the survey going; any other order sets it aside, its progress kept."));
    }

    // ===== ADAPTIVE HUD BUILDING BLOCKS =====

    private enum HudIcon { None, Compass, Pin, Crown, Crew, Supply, Home }
    private static PixelIcon PixelFor(HudIcon icon) => icon == HudIcon.Crown ? PixelIcon.Crown : icon == HudIcon.Crew ? PixelIcon.Crew
        : icon == HudIcon.Supply ? PixelIcon.Supply : icon == HudIcon.Home ? PixelIcon.Home : icon == HudIcon.Pin ? PixelIcon.Chronicle : PixelIcon.Compass;

    // The world HUD's palette, the capital's own: dark stone slots rimmed in worn brass, cream titles, parchment captions.
    private static class Hud
    {
        public static readonly Color Slot = new Color(0.105f, 0.082f, 0.066f, 0.94f);
        public static readonly Color SlotHover = new Color(0.25f, 0.18f, 0.1f, 0.97f);
        public static readonly Color SlotPressed = new Color(0.34f, 0.24f, 0.12f, 1f);
        public static readonly Color SlotDisabled = new Color(0.075f, 0.066f, 0.06f, 0.7f);
        public static readonly Color Edge = new Color(0.42f, 0.31f, 0.18f, 0.85f);
        public static readonly Color Brass = new Color32(0xD9, 0xAE, 0x6A, 0xFF);
        public static readonly Color BrassDim = new Color32(0x6B, 0x57, 0x3E, 0xFF);
        public static readonly Color Muted = new Color32(0xB3, 0xA3, 0x8E, 0xFF);
        public static readonly Color Disabled = new Color32(0x86, 0x7B, 0x70, 0xFF);

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static void Active(Button button, bool active)
        {
            var colors = button.colors;
            colors.normalColor = active ? SlotPressed : Slot;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            var rim = button.targetGraphic.GetComponent<Outline>();
            if (rim != null) rim.effectColor = active ? Brass : Edge;
        }

        // A button's plate: a dark slot that warms toward brass under the pointer, with a thin brass rim.
        public static void Surface(Image plate, Button button)
        {
            plate.color = Color.white;
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Slot;
            colors.highlightedColor = SlotHover;
            colors.pressedColor = SlotPressed;
            colors.selectedColor = Slot;
            colors.disabledColor = SlotDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var rim = plate.GetComponent<Outline>();
            if (rim == null) rim = plate.gameObject.AddComponent<Outline>();
            rim.effectColor = Edge;
            rim.effectDistance = new Vector2(1f, -1f);
        }
    }

    // A single point-filtered graphic replaces many individual line graphics per icon.
    private static RectTransform Icon(Transform parent, HudIcon kind, Color color)
    {
        var root = CodeUI.Panel(parent, kind.ToString(), Vector2.zero, Vector2.one);
        var image = CodeUI.Solid(root, "Pixel icon", color);
        image.sprite = PixelIcons.Get(PixelFor(kind));
        image.preserveAspect = true;
        return root;
    }
    private static void ButtonSurface(TextMeshProUGUI label)
    {
        // A parent plate renders behind the label. The button keeps its label and tooltip hit target.
        var rect = label.rectTransform;
        var wrapper = CodeUI.Panel(rect.parent, label.gameObject.name + " button", rect.anchorMin, rect.anchorMax);
        wrapper.pivot = rect.pivot; wrapper.sizeDelta = rect.sizeDelta; wrapper.anchoredPosition = rect.anchoredPosition;
        var background = wrapper.gameObject.AddComponent<Image>();
        rect.SetParent(wrapper, false);
        CodeUI.Stretch(rect);
        label.margin = new Vector4(8f, 2f, 8f, 2f);
        Hud.Surface(background, label.GetComponent<Button>());
    }

    // Tooltip rows share a baseline; in narrow HUD columns use ordinary wrapping label/value text instead.
    private static string FlowText(string text) => KeywordMarkup.SafeGlyphs((text ?? string.Empty)
        .Replace(TooltipText.RowBreak + "</line-height><align=right>", ": ")
        .Replace("<align=left>", string.Empty).Replace("<align=right>", string.Empty).Replace("</align>", string.Empty).Trim());

    private static ScrollRect ScrollArea(RectTransform parent, float bottom = Inner, float top = Inner)
    {
        var root = CodeUI.Panel(parent, "Scroll", Vector2.zero, Vector2.one);
        root.offsetMin = new Vector2(Inner, bottom); root.offsetMax = new Vector2(-Inner, -top);
        var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .08f);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        var viewport = CodeUI.Panel(root, "Viewport", Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0, 1), new Vector2(1, 1));
        content.pivot = new Vector2(.5f, 1f);
        var bar = CodeUI.Panel(root, "Scrollbar", new Vector2(1, 0), Vector2.one);
        bar.offsetMin = new Vector2(-7, 0); bar.offsetMax = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .25f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        var handle = CodeUI.Solid(bar, "Handle", new Color(.72f, .62f, .4f, .85f), true);
        scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f; scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private void LayoutHud()
    {
        _actionBar.Reflow();
        float height = _dock.sizeDelta.y;
        foreach (var entry in _popups)
        {
            float preferred = entry.Key == Popup.Units ? 460f : 540f;
            entry.Value.sizeDelta = new Vector2(Mathf.Min(preferred, _canvasRect.rect.width - Pad * 2f), Mathf.Max(100f, Mathf.Min(640f, _canvasRect.rect.height - height - _key.sizeDelta.y - 8f - 3f * Pad)));
            entry.Value.anchoredPosition = new Vector2(Pad, Pad + height + 8f);
        }
        // Expanded menus should remain usable when they overlap the selection on narrow windows.
        if (_popup != Popup.None) _popups[_popup].SetAsLastSibling();
    }

    // Pooled rows: refresh updates labels and callbacks without recreating the hierarchy or losing scroll position.
    private sealed class HudList
    {
        private sealed class Row
        {
            public RectTransform rect;
            public TextMeshProUGUI label;
            public string title;
            public Button button;
            public Image plate, accent, meterTrack, meterFill;
            public readonly Dictionary<HudIcon, RectTransform> icons = new Dictionary<HudIcon, RectTransform>();
        }
        private readonly ScrollRect _scroll;
        private readonly TooltipTheme _theme;
        private readonly List<Row> _rows = new List<Row>();
        private float _width, _y, _rowHeight;
        private int _used, _column;
        private bool _grid;
        public float Height => _y;
        public HudList(RectTransform parent, TooltipTheme theme) { _theme = theme; _scroll = ScrollArea(parent); }
        public void Begin(float width) { _width = width; _y = 0; _used = 0; _column = 0; _grid = false; _rowHeight = 0; }
        public void Grid(bool enabled)
        {
            if (_column > 0) { _y += _rowHeight + 6f; _column = 0; _rowHeight = 0; }
            _grid = enabled && _width >= 330f;
        }
        public void Text(string text, HudIcon icon = HudIcon.None) => Add(text, null, icon, null, true);
        public void Meter(string title, float fraction, string value, bool warning)
        {
            Add($"{title}  <b>{value}</b>", null, HudIcon.None, null, true);
            var row = _rows[_used - 1];
            if (row.meterTrack == null)
            {
                row.meterTrack = CodeUI.Solid(row.rect, "Meter track", new Color(1f, 1f, 1f, 0.12f));
                CodeUI.Place(row.meterTrack.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(10f, 1f), new Vector2(-10f, 6f));
                row.meterFill = CodeUI.Solid(row.meterTrack.transform, "Meter fill", Hud.Brass);
            }
            row.meterTrack.gameObject.SetActive(true);
            row.meterFill.color = warning ? new Color(0.94f, 0.48f, 0.30f) : new Color(0.48f, 0.73f, 0.66f);
            CodeUI.Place(row.meterFill.rectTransform, Vector2.zero, new Vector2(Mathf.Clamp01(fraction), 1f), Vector2.zero, Vector2.zero);
        }
        /// <summary>The plate of the shown order whose title starts with <paramref name="name"/> and can be given now, or null.</summary>
        public Graphic Find(string name)
        {
            for (int i = 0; i < _used && i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.button.enabled && row.button.interactable && row.title != null && row.title.StartsWith(name, StringComparison.Ordinal)) return row.plate;
            }
            return null;
        }
        public void Action(string title, string detail, HudIcon icon, Action call, bool enabled = true) => Add(title, detail, icon, call, enabled);
        public void Choice(string title, bool active, Action call)
        {
            Add(title, active ? "Active" : null, HudIcon.None, call, true);
            var row = _rows[_used - 1];
            Hud.Active(row.button, active);
            row.accent.color = active ? Hud.Brass : Hud.BrassDim;
        }
        private void Add(string title, string detail, HudIcon icon, Action call, bool enabled)
        {
            if (_used == _rows.Count)
            {
                var rect = CodeUI.Panel(_scroll.content, "Row", new Vector2(0, 1), new Vector2(0, 1));
                var plate = rect.gameObject.AddComponent<Image>();
                var button = rect.gameObject.AddComponent<Button>();
                Hud.Surface(plate, button);
                // A brass mark down the left edge of every order (dim when it cannot be given now).
                var accent = CodeUI.Solid(rect, "Accent", Hud.Brass);
                CodeUI.Place(accent.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 3f), new Vector2(3f, -3f));
                var label = CodeUI.Label(rect, "Label", string.Empty, Mathf.Max(16, _theme.bodySize - 3f), _theme.bodyColor, FontStyles.Normal, _theme);
                label.alignment = TextAlignmentOptions.TopLeft;
                _rows.Add(new Row { rect = rect, plate = plate, accent = accent, button = button, label = label });
            }
            var row = _rows[_used++];
            if (row.meterTrack != null) row.meterTrack.gameObject.SetActive(false);
            row.title = title;
            row.rect.gameObject.SetActive(true);
            Hud.Active(row.button, false);
            row.plate.color = call == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
            row.accent.gameObject.SetActive(call != null);
            row.accent.color = enabled ? Hud.Brass : Hud.BrassDim;
            row.plate.raycastTarget = call != null;
            row.button.enabled = call != null; row.button.interactable = enabled;
            if (call != null) UiFocus.Ensure(row.button);
            row.button.onClick.RemoveAllListeners();
            if (call != null) row.button.onClick.AddListener(() => call());
            row.label.color = !enabled ? Hud.Disabled : call != null ? _theme.titleColor : _theme.bodyColor;
            string value = call == null ? FlowText(title) : $"<b>{FlowText(title)}</b>";
            // Costs, consequences and requirements remain available without making every order a paragraph.
            var tip = row.rect.GetComponent<TooltipTrigger>();
            if (call != null)
            {
                tip = TooltipTrigger.Ensure(row.rect.gameObject); tip.enabled = true;
                tip.SetCustom(FlowText(title), FlowText(detail ?? "Select to perform this action."));
            }
            else if (tip != null) tip.enabled = false;
            row.label.text = value;
            float width = _grid ? (_width - 6f) / 2f : _width;
            float inset = icon == HudIcon.None ? 10f : 40f;
            float height = Mathf.Max(call == null ? 28f : 48f, row.label.GetPreferredValues(value, width - inset - 10f, 0f).y + 18f);
            CodeUI.Place(row.rect, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(_column * (width + 6f), -_y - height), new Vector2(_column * (width + 6f) + width, -_y));
            CodeUI.Place(row.label.rectTransform, Vector2.zero, Vector2.one, new Vector2(inset, 9f), new Vector2(-10f, -9f));
            if (icon != HudIcon.None && !row.icons.ContainsKey(icon))
            {
                var art = Icon(row.rect, icon, Hud.Brass);
                CodeUI.Place(art, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -34), new Vector2(32, -10));
                row.icons[icon] = art;
            }
            foreach (var entry in row.icons) entry.Value.gameObject.SetActive(entry.Key == icon);
            if (_grid)
            {
                _rowHeight = Mathf.Max(_rowHeight, height);
                if (++_column == 2)
                {
                    // Both slots share a baseline even if only one caption wraps.
                    for (int i = _used - 2; i < _used; i++)
                        _rows[i].rect.offsetMin = new Vector2(_rows[i].rect.offsetMin.x, -_y - _rowHeight);
                    _y += _rowHeight + 6f; _column = 0; _rowHeight = 0;
                }
            }
            else _y += height + 6f;
        }
        public void End()
        {
            Grid(false);
            for (int i = _used; i < _rows.Count; i++) _rows[i].rect.gameObject.SetActive(false);
            _scroll.content.sizeDelta = new Vector2(0, _y);
        }
        public void ResetScroll() { _scroll.StopMovement(); _scroll.verticalNormalizedPosition = 1f; }
    }

    // ===== EXPEDITIONS ON THE CARDS =====

    // The legends the cards' choosers point at: a Director for a new expedition, a companion to add. Each keeps its
    // legend while it stays free to go.
    private static string _formDirector, _companionPick;
    private static ExpeditionCharter _formCharter;
    // The selected unit's next click on the map picks the meso hex it surveys (the card's "Survey another hex").
    private static bool _surveyPick;

    private static string Chosen(WorldSystem world, ref string pick)
    {
        if (pick == null || world.WhyNotJoin(pick) != null) pick = world.Candidates().FirstOrDefault();
        return pick;
    }

    // " (leaves the High Arbiter seat)" for a seated legend, else nothing.
    private static string SeatNote(WorldSystem world, string legend)
    {
        var seat = world.SeatLeftBy(legend);
        return seat != null ? $", leaves the {seat.GetEffectiveTitle()} seat" : string.Empty;
    }

    // Its Symphony: the cards it fights with (its kit or companies, its legends' grimoires, its commander's orders) and
    // the strength they make it on the battle screen; then each legend's personal grimoire.
    private static void DescribeSymphony(WorldSystem world, WorldUnit unit, StringBuilder text)
    {
        var side = world.SideOf(unit);
        if (side == null) return;
        var rating = SymphonyPower.Rate(side, world.Combat);
        var kit = world.KitOf(unit);
        text.AppendLine(TooltipText.Row("Symphony", $"strength {rating.power:0}: {SymphonyDecks.Describe(side.deck)}, {rating.beats} Beats a measure{(kit != null ? $" ({kit.name})" : string.Empty)}"));
        var legends = LegendProgress.Instance;
        if (legends == null) return;
        var owned = Grimoire.Current();
        foreach (string name in Expeditions.Members(unit))
        {
            var soul = legends.Soul(name);
            var own = world.Symphony.Card(SymphonyCards.LeitmotifId(HarmonicCircle.Of(soul?.leitmotif)));
            var learned = legends.PersonalGrimoire(name).Select(id => owned.cards.FirstOrDefault(c => c != null && c.id == id)?.DisplayName ?? id).ToList();
            string pages = $"{learned.Count}/{legends.GrimoirePages(name)} pages";
            text.AppendLine(TooltipText.Row($"{name}'s grimoire", $"{(own != null ? own.name : "no leitmotif")}{(learned.Count > 0 ? ", " + string.Join(", ", learned) : string.Empty)} {TooltipText.Muted($"({pages})")}"));
        }
    }

    // Who walks, who leads and what the road is doing to them.
    private static void DescribeParty(WorldSystem world, WorldUnit unit, StringBuilder text)
    {
        var legends = LegendProgress.Instance;
        var x = world.ExpeditionRules;
        if (unit.charter != ExpeditionCharter.Expedition)
            text.AppendLine(TooltipText.Row("Charter", $"{WorldSystem.CharterName(unit.charter, plural: false)}: {WorldSystem.CharterDescription(unit.charter)}"));
        if (unit.leader != null)
        {
            string binding = WorldSystem.DirectorBinding(unit);
            string strength = Expeditions.Strength(binding, x);
            text.AppendLine(TooltipText.Row("Director", binding != null ? $"{unit.leader} ([[{binding}]]{(strength != null ? $": {strength}" : string.Empty)})" : unit.leader));
        }
        var companions = unit.companions.Where(n => !string.IsNullOrEmpty(n)).ToList();
        text.AppendLine(TooltipText.Row("Companions", companions.Count > 0 ? string.Join(", ", companions) : TooltipText.Muted("none")));
        // Its size is a choice: alone is fast and fragile, a company slow, loud and hard to undo.
        var shape = world.PartyShapeOf(unit);
        text.AppendLine(TooltipText.Row("Party", $"{shape.name}: {PartyShapes.Summary(shape)}"));
        string ambition = Expeditions.AmbitionSummary(CivilizationProperties.Expedition);
        if (ambition != null) text.AppendLine(TooltipText.Row("[[Ambition]]", ambition));
        if (unit.settlers > 0) text.AppendLine(TooltipText.Row("Settlers", $"{unit.settlers} citizens, to found a settlement"));
        string cargo = WorldSystem.CargoText(unit);
        float burden = Expeditions.BurdenStep(unit, x);
        text.AppendLine(TooltipText.Row("Carried weight", $"{Expeditions.Load(unit, x):0.##} / {Expeditions.LoadAtEase(unit, x):0.##} comfortable load; pace x{Expeditions.LoadPace(x, burden):0.##}{(Expeditions.LoadSummary(x, burden) is string load ? $" ({load})" : string.Empty)}"));
        if (cargo != null) text.AppendLine(TooltipText.Row("Cargo", $"{cargo} {TooltipText.Muted($"({world.CargoLoad(unit):0}/{world.CargoCapacity(unit):0}; unloaded in any of your settlements)")}"));
        if (legends != null)
        {
            var states = Expeditions.Members(unit).Select(n =>
            {
                var state = legends.Composure(n);
                string word = state >= ComposureState.Spiraling ? TooltipText.Bad(state.ToString()) : state == ComposureState.Fractured ? TooltipText.Warn(state.ToString()) : state.ToString();
                return $"{n} {word}";
            });
            text.AppendLine(TooltipText.Row("[[Composure]]", string.Join(", ", states)));
        }
        var at = UnitSurroundings.Of(world.Map, unit);
        float solace = Expeditions.Solace(at, x);
        if (solace > 0.01f) text.AppendLine(TooltipText.Row("Restorative surroundings", $"+{solace:0.##} Composure recovery per Seventh"));
        if (at.fallout > 0.01f) text.AppendLine(TooltipText.Row("Vibrational Fallout", TooltipText.Warn($"{at.fallout:P0}: rapid Composure loss and difficult travel")));
        float hardship = Expeditions.Hardship(unit, at, false, WorldSystem.DirectorBinding(unit), x);
        if (hardship > 0.01f)
            text.AppendLine(TooltipText.Row("Hardship", TooltipText.Warn($"+{hardship:0.#} strain a Seventh on each legend, +{hardship * x.directorShare:0.#} on the Director")));
        float risk = world.MishapRisk(unit);
        if (risk > 0.001f) text.AppendLine(TooltipText.Row("Mishaps", TooltipText.Warn($"{risk:P0} chance a Seventh{(unit.mishaps > 0 ? $", {unit.mishaps} suffered" : string.Empty)}")));
        else if (unit.mishaps > 0) text.AppendLine(TooltipText.Row("Mishaps", $"{unit.mishaps} suffered"));
    }

    // The party's own actions: in one of your settlements, outfitting (companions, settlers, disbanding); in the field,
    // who directs and what its settlers found.
    private static void PartyActions(WorldSystem world, WorldUnit unit, bool home, List<(string label, string why, Action call)> actions)
    {
        var x = world.ExpeditionRules;
        var companions = unit.companions.Where(n => !string.IsNullOrEmpty(n)).ToList();
        if (home)
        {
            foreach (BattlePremonitionKind kind in Enum.GetValues(typeof(BattlePremonitionKind)))
            {
                var preparation = kind;
                var cost = world.Combat.Preparation.Cost(kind) ?? Enumerable.Empty<ResourceAmount>();
                string label = "Prepare " + kind + " premonition (" + string.Join(", ", cost.Select(c => $"{c.amount:0.#} {c.resource}")) + ")";
                actions.Add((label, null, () =>
                {
                    string why = world.PreparePremonition(preparation);
                    NotificationFeed.Push("Premonition preparation", why ?? $"{world.PreparedPremonitions.Remaining} prepared attempts remain.", NotificationFeed.Topic.World);
                }));
            }
            if (LegendProgress.Instance != null)
                foreach (string member in Expeditions.Members(unit))
                { string name = member; actions.Add(("Rest and integrate Legend Opus: " + name, null, () => BattleRecoveryWindow.Show(name))); }
            string pick = Chosen(world, ref _companionPick);
            int free = world.Candidates().Count;
            actions.Add(($"Companion: {pick ?? "none free"}", free == 0 ? "No legend is free to go." : free == 1 ? "No other legend is free." : null, () => _companionPick = world.NextCandidate(_companionPick)));
            actions.Add(($"Add {pick ?? "a companion"} ({world.OutfitCostText}{SeatNote(world, pick)})", world.WhyNotAddCompanion(unit, pick), () => world.AddCompanion(unit, _companionPick)));
            if (unit.settlers == 0 && unit.charter == ExpeditionCharter.Expedition)
                actions.Add(($"Take on settlers ({world.SettlerCostText})", world.WhyNotTakeSettlers(unit), () => world.TakeSettlers(unit)));
            // Its kit: the cards it fights with in the field (changed only here, in a settlement).
            var nextKit = world.NextKit(unit, null);
            if (nextKit != null)
                actions.Add(($"Kit: {world.KitOf(unit)?.name ?? "none"}; take up the {nextKit.name}{(nextKit.weight > 0f ? $" (+{nextKit.weight:0.#} weight)" : string.Empty)}", world.WhyNotEquip(unit, nextKit.id), () => world.EquipKit(unit, nextKit.id)));
            actions.Add(("Disband (its legends go home)", world.WhyNotDisband(unit), () => world.Disband(unit)));
            return;
        }
        foreach (var item in (unit.cargo ?? new List<ResourceAmount>()).Where(a => a != null && a.amount > 0f).ToList())
        {
            string resource = item.resource;
            float amount = item.amount, weight = amount * Expeditions.WeightOf(x, resource);
            actions.Add(($"Discard half: {amount / 2f:0.##} {resource} (-{weight / 2f:0.#} weight, permanently lost)", unit.Missing ? "The expedition is missing." : null, () => world.DropCargo(unit, resource, amount / 2f)));
            actions.Add(($"Discard all: {amount:0.##} {resource} (-{weight:0.#} weight, permanently lost)", unit.Missing ? "The expedition is missing." : null, () => world.DropCargo(unit, resource, amount)));
        }
        // The moves of a small party: slip away from danger; alone, drop out of sight altogether.
        var shape = world.PartyShapeOf(unit);
        if (shape.retreatPace > 0f && !unit.retreating)
            actions.Add(($"Retreat to safe ground ({shape.retreatPace - 1f:P0} faster)", world.WhyNotRetreat(unit), () => world.Retreat(unit)));
        if (shape.canVanish)
            actions.Add(("Go to ground (missing for a while, turns up in your territory)", world.WhyNotGoToGround(unit), () => world.GoToGround(unit)));
        if (unit.settlers <= 0 || unit.Moving) return;
        foreach (var kind in new[] { SettlementKind.Town, SettlementKind.Outpost, SettlementKind.Haven })
        {
            string why = world.WhyNotSettle(unit, kind);
            if (kind == SettlementKind.Haven && why != null && why.StartsWith("A Religious Haven needs", StringComparison.Ordinal)) continue;
            actions.Add(($"Found {WorldCivilization.KindName(kind)}", why, () => world.Settle(unit, kind)));
        }
    }

    // A settlement's expedition slots and the forming of a new expedition around a Director.
    private static void ExpeditionActions(WorldSystem world, Settlement s, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        if (!world.MapUnlocked || !world.CanOutfit(s)) return;
        var x = world.ExpeditionRules;
        text.AppendLine(TooltipText.Row("Expedition slots", $"{world.ExpeditionSlotsUsed} of {world.ExpeditionSlots} in use"));
        text.AppendLine(TooltipText.Muted($"One per legend in the field; Government Capacity adds {x.slotsPerCapacity} each{(string.IsNullOrEmpty(x.slotBuilding) ? string.Empty : $", each {x.slotBuilding} {x.slotsPerBuilding}")}{(world.TributariesUnlocked ? ", each Militant District more" : string.Empty)}."));
        string director = Chosen(world, ref _formDirector);
        int free = world.Candidates().Count;
        actions.Add(($"Director: {director ?? "none free"}", free == 0 ? "No legend is free to go." : free == 1 ? "No other legend is free." : null, () => _formDirector = world.NextCandidate(_formDirector)));
        // The charter: what the party sets out to do (explore, build, celebrate).
        var charter = _formCharter;
        text.AppendLine(TooltipText.Row("Charter", $"{WorldSystem.CharterName(charter, plural: false)}: {WorldSystem.CharterDescription(charter)}"));
        actions.Add(($"Charter: {WorldSystem.CharterName(charter, plural: false)} (change)", null, () => _formCharter = WorldSystem.NextCharter(_formCharter)));
        actions.Add(($"Form {WorldSystem.CharterName(charter, plural: false).ToLowerInvariant()} ({world.OutfitCostText}{SeatNote(world, director)})", world.WhyNotForm(s, director) ?? world.WhyNotCharter(charter), () =>
        {
            var unit = world.FormExpedition(s, _formDirector, _formCharter);
            if (unit != null) ShowUnit(unit.id);
        }));
    }

    // The way back to a settlement for the selected unit's card, searched again only when it moved or the land changed.
    private static (int unit, HexCoord from, int stamp, string why, int hexes, float fatigue) _returnWay = (-1, default, 0, null, 0, 0f);

    private static (string why, int hexes, float fatigue) ReturnWay(WorldSystem world, WorldUnit unit)
    {
        var from = WorldUnits.MicroPosition(unit);
        int stamp = world.Map.CivilizationVersion * 31 + (world.Map.Magic?.Version ?? 0);
        if (_returnWay.unit != unit.id || _returnWay.from != from || _returnWay.stamp != stamp)
        {
            string why = world.WhyNotReturn(unit, out var way, out float fatigue);
            _returnWay = (unit.id, from, stamp, why, way?.Count ?? 0, fatigue);
        }
        return (_returnWay.why, _returnWay.hexes, _returnWay.fatigue);
    }

    // What the unit is doing, where, and for how long.
    private static string Status(WorldSystem world, WorldUnit unit, UnitSpec spec)
    {
        var map = world.Map;
        var gen = world.Settings.generation;
        if (unit.Missing) return TooltipText.Warn("missing in action");
        if (!WorldBattles.IsPlayers(unit)) return WorldPursuit.Status(unit);
        string place = world.Place(map.Get(unit.coord));
        if (unit.winded) return TooltipText.Warn($"winded at {place}, catching its breath");
        var quarry = unit.quarryId >= 0 ? world.UnitById(unit.quarryId) : null;
        if (quarry != null)
            return $"giving chase to {quarry.name}, {Hexes(HexCoord.Distance(WorldUnits.MicroPosition(unit), WorldUnits.MicroPosition(quarry)))} ahead";
        if (unit.retreating && unit.Moving) return $"retreating from {place}, {Sevenths(WorldUnits.SeventhsLeft(map, gen, unit, spec))} to safe ground";
        if (unit.Camping)
        {
            if (unit.resting && unit.surveying) return $"resting in camp at {place}; it takes up its survey ({world.SurveyProgress(unit):P0} done) once rested";
            return !unit.resting ? $"camped at {place}" : unit.Moving ? $"resting in camp at {place}; it walks on once rested" : $"resting in camp at {place}, taking on rations";
        }
        if (unit.surveying)
        {
            var target = map.Get(unit.surveyCell);
            int left = world.HexesToSurvey(target);
            string of = $"{world.Place(target)} ({world.SurveyProgress(unit):P0} done, {Hexes(left)} of {WorldMap.OpenHexes(target)} left)";
            if (unit.Working) return $"surveying a hex of {of}, {Sevenths(unit.workLeft)} on this one";
            if (unit.returning && unit.Moving) return $"walking back for rations; it takes up its survey of {of} after";
            if (unit.Moving) return unit.coord == unit.surveyCell ? $"walking to the next hex to survey in {of}" : $"on its way to survey {of}";
            if (unit.surveyWait > 0f) return $"surveying {of}: others stand in the way, it waits for them to move";
        }
        if (unit.Working) return $"{TaskWord(unit.task)} at {place}, {Sevenths(unit.workLeft)} left";
        if (unit.Moving)
        {
            var end = map.Get(HexHierarchy.Parent(unit.path[unit.path.Count - 1]));
            string to = end != null && end.revealed ? $"to {world.Place(end)}" : "into the fog";
            string how = $"{Hexes(unit.path.Count)}, fatigue {WorldUnits.FatigueLeft(map, gen, unit):0.#}, about {Sevenths(WorldUnits.SeventhsLeft(map, gen, unit, spec))}";
            return unit.returning ? $"walking back {to} for rations: {how}" : unit.autoExplore ? $"exploring by itself, {to}" : $"walking {to}: {how}";
        }
        if (unit.autoSurvey) return $"surveying by itself from {place}";
        if (unit.surveyPaused) return $"waiting at {place}; its survey of {world.Place(map.Get(unit.surveyCell))} is set aside ({world.SurveyProgress(unit):P0} done)";
        return unit.autoExplore ? $"exploring by itself from {place}" : $"waiting at {place}";
    }

    // What slows it: weariness, wear and hunger.
    private static string Hindrances(WorldUnit unit)
    {
        var why = new List<string>();
        if (unit.fatigue > WorldUnits.FreshFatigue) why.Add("weary");
        if (unit.attrition > 0.5f) why.Add("worn");
        if (unit.hungry) why.Add("hungry");
        return string.Join(", ", why);
    }

    // What a harvest here brings back, how long it takes, and whose grievances it raises. At a den on its cooldown,
    // when it can be hunted again.
    private static string HarvestDetail(WorldSystem world, WorldUnit unit)
    {
        var site = WorldResources.SiteAt(world.Map, world.Map.Get(unit.coord));
        if (WorldEcology.SpeciesAt(world.Settings.generation, site) != null && world.HuntedThisPhase(site))
            return $"hunted this Phase, {WorldResources.HuntCooldownText(world.SeventhsToNextPhase)}";
        var gain = world.HarvestPreview(unit);
        string what = gain.Count > 0 ? string.Join(", ", gain.Select(a => $"{a.amount:0.#} {a.resource}")) : "nothing to take";
        var grievance = world.HarvestGrievance(unit);
        return $"{what}, {Sevenths(world.WorkSevenths(unit, UnitAbilities.Harvest))}{(grievance.HasValue ? $"; {grievance.Value.owner}'s grievances +{grievance.Value.grievance:0}" : string.Empty)}";
    }

    // A den's hunting rhythm on its card: hunted this Phase (and when it is ready again), or open to a hunt.
    private static string HuntNote(WorldSystem world, ResourceSite site) =>
        world.HuntedThisPhase(site) ? " " + TooltipText.Warn($"(hunted this Phase: {WorldResources.HuntCooldownText(world.SeventhsToNextPhase)})")
            : TooltipText.Muted(" (a hunt, once a Phase)");

    private static string ForageDetail(WorldSystem world, WorldUnit unit)
    {
        var gain = world.ForagePreview(unit);
        return gain.Count > 0 ? string.Join(", ", gain.Select(a => $"{a.amount:0.#} {a.resource}")) : "nothing here";
    }

    private static string TaskWord(UnitTask task) => task == UnitTask.Camp ? "camped" : UnitAbilities.For(task)?.doing ?? "working";

    private static string Sevenths(float sevenths) => Mathf.Abs(sevenths - 1f) < 0.05f ? "1 Seventh" : $"{sevenths:0.#} Sevenths";

    private static string Hexes(int count) => count == 1 ? "1 hex" : $"{count} hexes";

    // Short, so the row never wraps over its label; the actions below say the rest.
    private static string AbilityWords(UnitSpec spec)
    {
        var words = new List<string>();
        if (spec.knowRadius > 0) words.Add("scout");
        if (WorldUnits.Can(spec, UnitAbility.Survey) || WorldUnits.Can(spec, UnitAbility.SurveyMeso)) words.Add("survey");
        if (WorldUnits.Can(spec, UnitAbility.Forage)) words.Add("forage");
        if (WorldUnits.Can(spec, UnitAbility.Harvest)) words.Add("harvest");
        if (WorldUnits.Can(spec, UnitAbility.Settle)) words.Add("found settlements");
        if (WorldUnits.Can(spec, UnitAbility.Improve)) words.Add("improve hotspots");
        return string.Join(", ", words);
    }

    private static void DescribeSettlement(WorldSystem world, Settlement s, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var map = world.Map;
        var rules = world.Rules;
        var b = WorldCivilization.Breakdown(map, rules, s);
        bool networked = WorldCivilization.Networked(map).Contains(s.id);
        text.AppendLine(TooltipText.Heading(s.name, WorldCivilization.KindName(s.kind)));
        if (s.kind != SettlementKind.Outpost)
        {
            text.AppendLine(TooltipText.Row("City Development", $"{s.development:0} of {b.Potential:0} (ceiling {b.ceiling:0})"));
            text.AppendLine(TooltipText.Muted(string.Join(", ", b.terms.OrderByDescending(t => Math.Abs(t.points)).Take(5).Select(t => $"{CityDevelopment.Name(t.term).ToLowerInvariant()} {t.points:+0;-0}"))));
        }
        if (s.kind == SettlementKind.Town)
            text.AppendLine(TooltipText.Row("Major Settlement", $"at {rules.majorThreshold:0} (capacity {WorldCivilization.MajorCount(map)}/{world.GovernmentCapacity})"));
        if (s.kind == SettlementKind.Major && !string.IsNullOrEmpty(s.binding))
        {
            var binding = rules.Binding(s.binding);
            text.AppendLine(TooltipText.Row("Binding", $"{s.binding}{(binding != null && !string.IsNullOrEmpty(binding.focus) ? $": {binding.focus}" : string.Empty)}"));
        }
        var seat = s.governedBy >= 0 ? WorldCivilization.Get(map, s.governedBy) : null;
        if (seat != null && seat.id != s.id) text.AppendLine(TooltipText.Row("Answers to", seat.name));
        if (s.detached) text.AppendLine(TooltipText.Row("Authority", "detached"));
        // Its territorial pull: the cells it holds by its own pull, how far it reaches and how fast it brings land in.
        var own = map.territory?.OfSettlement(s.id);
        if (own != null)
            text.AppendLine(TooltipText.Row("Territorial pull", $"holds {own.held:0.#}/{own.maxCells} cells, strength {own.strength:0.##}, reach {own.reach:0.#}, settles up to {own.adoptPerSeventh:0.##} hexes/Seventh, +{own.capacity:0.#} capacity"));
        if (s.kind != SettlementKind.Capital) text.AppendLine(TooltipText.Row("Roads", networked ? "joined to the Capital" : "isolated"));
        if (s.anchor) text.AppendLine(TooltipText.Row("Resonance Anchor", $"+{rules.anchorCoherence:P0} Coherence within {rules.anchorRadius}"));
        // Its Composure (a legend's five states), what strains it now and what mending it costs (WorldRuins).
        var loss = rules.loss;
        var harm = WorldRuins.Harm(map, loss, s);
        var composure = world.ComposureOf(s);
        string composureText = $"{composure} (strain {s.strain:0})";
        text.AppendLine(TooltipText.Row("[[Composure]]", composure >= ComposureState.Spiraling ? TooltipText.Bad(composureText) : composure == ComposureState.Fractured ? TooltipText.Warn(composureText) : composureText));
        if (harm.Count > 0)
            text.AppendLine(TooltipText.Row("Strained by", TooltipText.Warn(string.Join(", ", harm.Select(h => $"{(h.cause == WorldRuins.Withering ? "withering (no road joins it to a hub)" : h.cause)} +{h.perSeventh:0.#}/Seventh"))) + TooltipText.Muted($"; it eases {WorldRuins.Recovery(loss, s, networked):0.#}")));
        if (composure >= ComposureState.Fractured)
            text.AppendLine(TooltipText.Muted(s.kind == SettlementKind.Capital
                ? $"The Capital never Surrenders, but while shaken the realm yields {1f - world.CapitalOutput:P0} less."
                : $"It yields and grows {1f - WorldRuins.Output(loss, s):P0} less; at Surrender it falls into ruins, which an expedition can investigate for what its failure left behind."));
        // The Emotional Register: what its people feel (given off into its ground every Seventh) and what the ground keeps.
        if (s.feelings != null && s.feelings.Total > 0.01f)
            text.AppendLine(TooltipText.Row("Its people feel", "\n" + WorldSuffering.Meter(s.feelings, 1f)));
        var ground = map.Get(s.coord);
        if (ground?.imprint != null && ground.imprint.Total > 0.01f)
        {
            float pressure = WorldSuffering.Pressure(ground, ground.suffering);
            text.AppendLine(TooltipText.Row("Its ground holds", "\n" + WorldSuffering.Meter(ground.imprint, WorldSuffering.SpawnPressure)));
            text.AppendLine(pressure >= WorldSuffering.SpawnPressure
                ? TooltipText.Bad($"Heavy enough for Formless Masses to pool ({pressure:0.##} of {WorldSuffering.SpawnPressure:0.#}). Blooms that catalogue its {ground.imprint.Dominant} would drink it; so would easing what hurts its people.")
                : TooltipText.Muted($"Formless Masses pool at {WorldSuffering.SpawnPressure:0.#} ({pressure:0.##} now). Eleos Blooms nearby drink the feelings they catalogue."));
        }
        if (WorldRuins.MendScale(loss, s) > 0f) actions.Add(($"Mend its Composure ({world.MendCostText(s)})", world.WhyNotMend(s), () => world.Mend(s)));
        // Its roads' restored Old World stretches, a lesser road until rebuilt.
        foreach (var route in world.RestoredRoutes(s))
            actions.Add(($"Rebuild {WorldRuins.RestoredCount(route)} cells of restored Old World road ({world.RebuildCostText(route)})", world.WhyNotRebuild(route), () => world.RebuildRoad(route)));
        var ruinBeneath = WorldRuins.At(map, s.coord);
        if (ruinBeneath != null) DescribeRuin(world, ruinBeneath, text, actions);
        var extraction = WorldCivilization.Extractions(map).FirstOrDefault(x => x.by == s);
        if (extraction.field != null) text.AppendLine(TooltipText.Row("Extracting", $"{world.Settings.generation.Grandfield(extraction.field.spec)?.name} at {extraction.density:P0}"));
        // Minor hubs: a tributary's district and its upgrades; a major hub's outskirts.
        var tributaries = rules.tributaries;
        if (WorldTributaries.IsTributary(s)) DescribeTributary(world, s, text, actions);
        else if (WorldTributaries.IsHub(s) && world.TributariesUnlocked)
            text.AppendLine(TooltipText.Row("Tributaries", $"{WorldTributaries.Of(map, s).Count()} of {WorldTributaries.Slots(tributaries, s)} (one more every {tributaries.developmentPerSlot:0} City Development; raised on held land within {tributaries.hubReach} cells)"));
        CultureOfSettlement(s, text, actions);
        LocalCultureCard.Settlement(s, text, actions);

        // Expeditions of legends form here around a Director (not at Outposts or plain tributaries): everything on the map is legend-run.
        ExpeditionActions(world, s, text, actions);
        if (s.kind == SettlementKind.Town) actions.Add(($"Promote ({WorldSystem.CostText(rules.promoteCost)})", world.WhyNotPromote(s), () => world.Promote(s)));
        if (s.kind == SettlementKind.Major) actions.Add(($"Attune (now {s.binding})", null, () => world.CycleBinding(s)));
        if (s.detached) actions.Add(($"Incorporate ({WorldSystem.CostText(rules.incorporateCost)})", world.WhyNotIncorporate(s), () => world.Incorporate(s)));
        if (s.kind != SettlementKind.Capital)
        {
            string why = world.WhyNotRoad(s, out var roadPath, out _, out _);
            if (why != "Already joined to the Capital by road.")
            {
                // Old World road on the way is restored cheaply (a lesser road until rebuilt).
                var (restored, fresh) = roadPath != null ? WorldRuins.RoadCells(map, roadPath) : (0, 0);
                float scale = roadPath != null ? WorldRuins.RoadCostScale(map, rules.loss.oldWorld, roadPath) : 1f;
                actions.Add(($"Build road ({WorldSystem.CostText(rules.roadCostPerCell, Math.Max(0.35f, scale))}{(restored > 0 ? $"; restores {restored} of {restored + fresh} cells from an Old World road" : string.Empty)})", why, () => world.BuildRoad(s)));
            }
            if (!s.anchor && !WorldTributaries.IsTributary(s)) actions.Add(($"Resonance Anchor ({WorldSystem.CostText(rules.anchorCost)})", world.WhyNotAnchor(s), () => world.BuildAnchor(s)));
        }
    }

    // What the culture made of a settlement: the name it gave it, its landmarks (shrines, temples, halls of song) and the
    // next one each of its lines can raise, and its festivals.
    private static void CultureOfSettlement(Settlement s, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !CultureSystem.IsFounded || s == null) return;
        var named = culture.NameOf(s.id);
        if (named != null) text.AppendLine(TooltipText.Row("Named by", $"{CultureSystem.PeopleWord} (once {named.former})"));
        var standing = culture.LandmarksAt(s.id).ToList();
        if (standing.Count > 0) text.AppendLine(TooltipText.Row("Landmarks", string.Join(", ", standing.Select(l => $"{l.name} ({culture.Life.Landmark(l.spec)?.name ?? l.spec})"))));
        var festival = culture.FestivalRecordOf(s.id);
        if (festival != null && festival.count > 0) text.AppendLine(TooltipText.Row("Festivals", $"{festival.count} held, the last {culture.Age - festival.lastSeventh} Sevenths ago"));
        foreach (var spec in culture.NextLandmarks(s))
        {
            var l = spec;
            string gives = string.Join(", ", new[] { l.unityPerSeventh > 0f ? $"+{l.unityPerSeventh:0.#} Unity" : null, l.faithPerSeventh > 0f ? $"+{l.faithPerSeventh:0.#} Faith" : null }.Where(g => g != null));
            actions.Add(($"Raise a {l.name} ({CultureSystem.CostText(l.cost)}; {gives} a Seventh, named by {CultureSystem.PeopleWord})", culture.WhyNotLandmark(s, l), () => culture.RaiseLandmark(s, l.id)));
        }
    }

    // An outskirt tributary: its district, what it grows toward, its adjacency and what it gives; every other district
    // as an upgrade, best fit here first.
    private static void DescribeTributary(WorldSystem world, Settlement s, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var map = world.Map;
        var tr = world.Rules.tributaries;
        var spec = WorldTributaries.DistrictOf(tr, s);
        var tile = map.Get(s.coord);
        float desire = WorldDesirability.Of(map, tile);
        text.AppendLine(TooltipText.Row("District", spec != null ? $"{spec.name} ({spec.role})" : "none"));
        if (!string.IsNullOrEmpty(spec?.description)) text.AppendLine(TooltipText.Muted(spec.description));
        text.AppendLine(TooltipText.Row("Grows toward", $"{WorldTributaries.Target(map, tr, s):0}: {WorldDesirability.Word(desire)} ground ({desire:P0}), never more than {tr.aboveHub:0} above its hub"));
        var adjacency = WorldTributaries.Adjacency(map, tr, s);
        text.AppendLine(TooltipText.Row("Adjacency", adjacency.Count == 0 ? TooltipText.Muted("nothing around it counts")
            : $"{WorldTributaries.AdjacencyTotal(map, tr, s):+0%;-0%;0%} ({string.Join(", ", adjacency.Select(a => $"{a.what} {a.bonus:+0%;-0%}"))})"));
        var linked = WorldTributaries.Linked(map, tr, s).ToList();
        if (linked.Count > 0) text.AppendLine(TooltipText.Row("Linked districts", string.Join(", ", linked.Select(o => $"{o.name} ({WorldTributaries.DistrictOf(tr, o)?.name})"))));
        var effects = WorldTributaries.EffectLines(map, tr, s);
        if (effects.Count > 0) text.AppendLine(TooltipText.Row("Gives", string.Join(", ", effects)));
        bool generalist = WorldTributaries.IsGeneralist(tr, s);
        var choices = tr.AllDistricts.Where(d => !string.Equals(d.id, spec?.id, StringComparison.OrdinalIgnoreCase))
            .Select(d => (spec: d, back: string.Equals(d.id, tr.generalist, StringComparison.OrdinalIgnoreCase), adjacency: WorldTributaries.AdjacencyTotal(map, tr, s, d)))
            .OrderBy(c => c.back ? 1 : 0).ThenByDescending(c => c.adjacency).ToList();
        foreach (var c in choices)
        {
            var district = c.spec;
            string rebuild = generalist ? string.Empty : $"; rebuilt, keeps {tr.respecializeKeeps:P0} of its development";
            string label = c.back ? $"Back to {district.name} (free{rebuild})"
                : $"Upgrade to {WorldTributaries.Article(district.name)}: {district.role}, adjacency here {c.adjacency:+0%;-0%;0%} ({world.SpecializeCostText(s, district.id)}{rebuild})";
            actions.Add((label, world.WhyNotSpecialize(s, district.id), () => world.Specialize(s, district.id)));
        }
    }

    // The ruins of a fallen settlement: what it was, why it fell, and investigating it (once) for what it left behind.
    private static void DescribeRuin(WorldSystem world, Ruin ruin, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        actions.Add(("Inspect cultural inheritance", null, () => CultureWindow.OpenSection("Heritage", ruin.id.ToString())));
        var tr = world.Rules.tributaries;
        string was = ruin.kind == SettlementKind.Tributary ? WorldTributaries.Article(tr.District(ruin.district ?? tr.generalist)?.name ?? "tributary") : WorldTributaries.Article(WorldCivilization.KindName(ruin.kind));
        text.AppendLine(TooltipText.Row("Ruins", $"of {ruin.name}, once {was} of development {ruin.development:0}"));
        text.AppendLine(TooltipText.Muted($"It fell in Age {AgeRules.Roman(ruin.fallenAge)}, {WorldRuins.CauseWords(ruin.cause)}."));
        if (!ruin.investigated)
        {
            text.AppendLine(TooltipText.Muted("Loss is transformation: an expedition can read what its failure left behind (salvage, Research, perhaps an Enlightenment or a civic its people lived by)."));
            var unit = world.InvestigatorFor(ruin);
            if (unit != null)
                actions.Add((unit.coord == ruin.coord ? $"Investigate the ruins with {unit.name} ({Sevenths(world.WorkSevenths(unit, UnitAbilities.Investigate))})"
                    : $"Send {unit.name} to investigate the ruins ({HexCoord.Distance(unit.coord, ruin.coord)} cells away)", null, () => world.InvestigateRuin(unit, ruin)));
            return;
        }
        if (string.IsNullOrEmpty(ruin.civic)) { text.AppendLine(TooltipText.Muted("Investigated: the ruins have given up what they held.")); return; }
        if (ruin.civicAdopted) { text.AppendLine(TooltipText.Row("Inherited", $"{ruin.civic}, adopted from these ruins")); return; }
        text.AppendLine(TooltipText.Row("Left behind", $"{ruin.civic}: its people lived by it, so it can be adopted from the ruins without its requirements"));
        actions.Add(($"Adopt {ruin.civic} from the ruins", world.WhyNotAdoptRuinCivic(ruin), () => world.AdoptRuinCivic(ruin)));
    }

    // Raising an outskirt tributary on held land, for each hub within reach.
    private static void TributaryActions(WorldSystem world, WorldTile tile, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        if (tile.water || tile.impassable || tile.settlement >= 0 || !world.TributariesUnlocked) return;
        var tr = world.Rules.tributaries;
        var hubs = world.TributaryHubs(tile);
        if (hubs.Count == 0)
        {
            text.AppendLine(TooltipText.Muted($"No hub of yours within {tr.hubReach} cells to raise an outskirt tributary for."));
            return;
        }
        float desire = WorldDesirability.Of(world.Map, tile);
        text.AppendLine(TooltipText.Muted($"An outskirt tributary here serves a hub nearby: joined to it by road, it pulls the land around into your authority and adds Administrative Capacity. It begins as {WorldTributaries.Article(tr.District(tr.generalist)?.name ?? "generalist")} and can be upgraded to one district."));
        foreach (var hub in hubs.Take(3))
        {
            var h = hub;
            float target = Math.Max(0f, Math.Min(100f * desire, h.development + tr.aboveHub));
            actions.Add(($"Raise a tributary for {h.name} ({world.TributaryCostText}; develops toward {target:0})", world.WhyNotRaiseTributary(tile, h), () => world.RaiseTributary(tile, h)));
        }
    }

    /// <summary>A cell of bare land: who holds it and, for wilderness, claiming it into your authority hex by hex (<paramref name="hex"/>: the hex clicked, if any).</summary>
    private static void DescribeLand(WorldSystem world, WorldTile tile, HexCoord? hex, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        text.AppendLine(TooltipText.Heading(world.Place(tile), tile.explored ? TooltipText.Good("Explored") : "Seen, not explored"));
        if (!tile.water) text.AppendLine(TooltipText.Row("Survey", SurveyState(world, tile)));
        var ruin = WorldRuins.At(world.Map, tile.coord);
        if (ruin != null && tile.known) DescribeRuin(world, ruin, text, actions);
        if (tile.known) AppendResource(world, tile, text);
        SurveyWith(world, tile, actions);
        if (tile.known) CommissionActions(world, tile, actions);
        if (tile.known && !tile.water && !tile.impassable)
        {
            float desire = WorldDesirability.Of(world.Map, tile);
            text.AppendLine(TooltipText.Row("Desirability", $"{WorldDesirability.Word(desire)} ({desire:P0}): settlements grow x{WorldDesirability.RulesOf(world.Map).GrowthFactor(desire):0.0#} here"));
        }
        string hold = HoldText(world, tile);
        bool fillable = WorldHoldings.Fillable(tile, WorldAuthority.Player);
        if (WorldAuthority.IsPlayers(tile.authorityId) && !fillable)
        {
            text.AppendLine(TooltipText.Row("Held by", TooltipText.Good(tile.authorityId == WorldAuthority.Outpost ? "Your Outpost" : "Your authority")));
            if (hold != null) text.AppendLine(TooltipText.Row("Hold", hold));
            if (tile.authorityId == WorldAuthority.Player) TributaryActions(world, tile, text, actions);
            return;
        }
        if (!fillable)
        {
            text.AppendLine(TooltipText.Row("Held by", WorldResources.EnclaveOf(world.Map, tile.authorityId) != null ? EnclaveOwner(world, tile) : HolderName(world, tile.authorityId)));
            if (hold != null) text.AppendLine(TooltipText.Row("Hold", hold));
            return;
        }
        string settling = SettlingState(world, tile);
        if (settling != null) text.AppendLine(TooltipText.Row("Settling", settling));
        int held = WorldMap.SettledHexes(tile), open = WorldMap.OpenHexes(tile), claimed = MicroNavigation.Crags(tile.microClaimMask);
        text.AppendLine(TooltipText.Row("Held by", held == 0 ? TooltipText.Muted("Wilderness")
            : $"{TooltipText.Good(WorldAuthority.IsPlayers(tile.authorityId) ? $"You de facto, {held}/{open} hexes" : $"You, {held}/{open} hexes")}{(claimed > 0 ? $" ({claimed} claimed)" : string.Empty)}; the rest {(WorldHoldings.FreeMask(world.Map, tile) != 0 ? "wilderness" : "held by others")}"));
        if (hold != null && WorldHoldings.Shares(world.Map, tile).Count > 1) text.AppendLine(TooltipText.Row("Hold", hold));
        var yields = world.Settings.generation.Terrain(tile.terrain)?.yields?.Where(y => y != null && y.amount != 0f).ToList();
        if (yields != null && yields.Count > 0)
            text.AppendLine(TooltipText.Row("Its ground would yield", string.Join(", ", yields.Select(y => $"{y.amount:+0.###} {y.resource}/s")) + $" (all {open} hexes; each hex its share)"));
        string pull = WorldLenses.TerritoryHover(world.Map, tile, world.Settings.generation);
        if (!string.IsNullOrEmpty(pull)) text.AppendLine(TooltipText.Row("Territorial pull", pull));
        var rules = world.Rules.territory;
        text.AppendLine(TooltipText.Muted($"Land is held one hex at a time: each hex settled or claimed is yours at once (inside your border, with its share of the cell's yields and Administrative load). With {WorldHoldings.DeFactoHexes(world.Map)} of a cell's hexes, more than anyone else, the cell answers to you de facto; with all of them it is core territory. A cell shared with another holder is disputed: a grievance for the one under the other's rule, a casus belli for the ruler to integrate it. Once the Capital has {rules.adoptionMinPopulation} citizens, society settles known wilderness by itself where your seats pull it (more people, more often; the glowing hexes on the map show where next, and when). A claim takes one hex bordering land you hold, paid in stored food of any kind: zoom in to the local reading and click a hex to claim that one."));
        if (world.Map.Claiming.Contains(tile.index)) return;
        int best = world.ClaimTarget(tile);
        bool picked = hex.HasValue && HexHierarchy.Parent(hex.Value) == tile.coord;
        if (picked)
        {
            var h = hex.Value;
            actions.Add(($"Claim this hex ({world.ClaimCostText}; yours at once)", world.WhyNotClaim(tile, h), () => world.Claim(tile, h)));
        }
        if (!picked || world.ClaimTarget(tile, hex) != best)
            actions.Add(($"Claim {(picked ? "the hex bordering your land most" : held > 0 ? "the next hex" : "a bordering hex")} ({world.ClaimCostText}; yours at once)", world.WhyNotClaim(tile), () => world.Claim(tile)));
    }

    // Send the nearest expedition free for it to survey the selected cell.
    private static void SurveyWith(WorldSystem world, WorldTile tile, List<(string label, string why, Action call)> actions)
    {
        if (tile.water || world.HexesToSurvey(tile) == 0 || world.SurveyorOf(tile) != null) return;
        var unit = world.Map.Units.Where(u => !u.Missing && !u.surveying && world.IsExpedition(u) && world.WhyNotSurveyCell(u, tile.coord) == null)
            .OrderBy(u => u.Moving || u.Working ? 1 : 0).ThenBy(u => HexCoord.Distance(u.coord, tile.coord)).ThenBy(u => u.id).FirstOrDefault();
        if (unit == null) return;
        int away = HexCoord.Distance(unit.coord, tile.coord);
        actions.Add(($"Survey with {unit.name} ({(away == 0 ? "here" : $"{away} cell{(away == 1 ? "" : "s")} away")}, {Hexes(world.HexesToSurvey(tile))})", null, () => world.SurveyCell(unit, tile.coord)));
    }

    private static void DescribeEnclave(WorldSystem world, Enclave e, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var spec = world.Settings.generation.Enclave(e.spec);
        text.AppendLine(TooltipText.Heading(e.name, $"{e.family} Enclave"));
        if (spec != null && !string.IsNullOrEmpty(spec.description)) text.AppendLine(TooltipText.Quote($"<i>{spec.description}</i>"));
        text.AppendLine(TooltipText.Row("Binding", e.binding));
        text.AppendLine(TooltipText.Row("Wound Resonance", $"{e.woundResonance:0} ({e.WoundState})"));
        text.AppendLine(TooltipText.Row("Standing", e.suzerain ? "you hold its Suzerainty" : $"{e.influence:0}/100"));
        float grievance = world.GrievanceOf(e.AuthorityId);
        if (grievance > 0.05f) text.AppendLine(TooltipText.Row("Grievances", TooltipText.Warn($"{grievance:0} against you (taking from its ground); an envoy answers them first")));
        if (spec != null && spec.suzeraintyYields.Count > 0)
            text.AppendLine(TooltipText.Row(e.suzerain ? "Yields" : "As suzerain", string.Join(", ", spec.suzeraintyYields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
        AppendEnclaveEcology(world, e, text);
        actions.Add(($"Send an envoy ({WorldSystem.CostText(world.Rules.envoyCost)})", world.WhyNotEnvoy(e), () => world.SendEnvoy(e)));
        // A moth-farming Enclave (WorldGreatPlague): what it trades, and asking it to stop once the moths' part is known.
        string traded = world.TradedBy(e);
        if (traded == null) return;
        var g = world.Settings.generation.greatPlague;
        int echo = world.EchoesGrown;
        text.AppendLine(TooltipText.Row("Farms and trades", e.restrictedUntil > echo ? $"{traded} {TooltipText.Muted($"(farms closed for {e.restrictedUntil - echo} more Echo(es))")}" : $"{traded}, to your settlements within {g.tradeReach} cells"));
        if (world.PlagueRevealed)
            actions.Add(($"Ask it to close its moth farms for {g.restrictEchoes} Echoes ({world.RestrictCostText}{(e.suzerain ? string.Empty : $", {g.restrictStandingCost:0} standing")})", world.WhyNotRestrict(e), () => world.Restrict(e)));
    }

    // What the Enclave does with the creatures (WorldEnclaveEcology): what it keeps (only what you know by name), what it
    // tends, what its Suzerainty lends you, and what it can be asked at a den.
    private static void AppendEnclaveEcology(WorldSystem world, Enclave e, StringBuilder text)
    {
        var s = world.EnclaveEcology;
        if (s == null) return;
        bool suzerain = e.suzerain;
        switch (e.family)
        {
            case EnclaveFamily.Domestication:
                var (known, unknown) = world.KeptBy(e);
                var parts = new List<string>(known);
                if (unknown > 0) parts.Add(unknown == 1 ? "a creature you have not identified" : $"{unknown} creatures you have not identified");
                text.AppendLine(TooltipText.Row("Keeps", parts.Count > 0 ? string.Join(", ", parts) : TooltipText.Muted($"nothing yet (the creatures within {s.keepReach} cells that suit it)")));
                string keeping = "they grow gentle with you, your hunts harm them less, their herds feed you, and every one you know is Mastered";
                text.AppendLine(suzerain ? TooltipText.Row("As your suzerain", TooltipText.Good(keeping)) : TooltipText.Muted($"As its suzerain: {keeping}."));
                break;
            case EnclaveFamily.Agromagical:
                text.AppendLine(TooltipText.Row("Tends", $"the land within {s.restoreReach} cells: its creatures recover {s.restorePerEcho:P0} of what they lack each Echo"));
                break;
            case EnclaveFamily.Auric:
                string scholars = "your people understand every creature they have observed";
                text.AppendLine(suzerain ? TooltipText.Row("As your suzerain", TooltipText.Good(scholars)) : TooltipText.Muted($"As its suzerain: {scholars}."));
                break;
        }
        var service = WorldEnclaveEcology.ServiceOf(e.family);
        if (service == EnclaveService.None) return;
        string when = suzerain ? "once a Phase" : $"once a Phase, from standing {s.commissionStanding:0} (spends {s.standingCost:0})";
        text.AppendLine(TooltipText.Row("Can be asked", $"to {WorldEnclaveEcology.Verb(service)} the creatures of an identified den within {s.serviceReach} cells, {when}; {world.CommissionCostText}"));
    }

    // At an identified den: ask a nearby Enclave to cull, tame, restore or study its creatures.
    private static void CommissionActions(WorldSystem world, WorldTile tile, List<(string label, string why, Action call)> actions)
    {
        var site = WorldResources.SiteAt(world.Map, tile);
        if (site == null || !WorldResources.Identified(world.Map, site)) return;
        foreach (var (enclave, service, why) in world.Commissions(site))
        {
            var e = enclave;
            actions.Add(($"Ask {e.name} to {WorldEnclaveEcology.Verb(service)} them ({world.CommissionCostText}{(e.suzerain ? string.Empty : $", {world.EnclaveEcology.standingCost:0} standing")})", why, () => world.Commission(e, site)));
        }
    }

    /// <summary>An atlas aggregate: its real cells, land and biome mixture, rivers, fields (area-weighted) and what was found.</summary>
    private static void DescribeMacro(WorldSystem world, HexCoord macro, StringBuilder text)
    {
        var gen = world.Settings.generation;
        var cells = world.Map.CellsOfMacro(macro);
        var known = cells.Where(t => t.revealed || t.explored).ToList();
        text.AppendLine(TooltipText.Heading("Atlas region", $"{cells.Count} cells"));
        if (known.Count == 0)
        {
            text.AppendLine(TooltipText.Muted("Nothing of it is known yet."));
            return;
        }
        var land = known.Where(t => !t.water || t.lake).ToList();
        text.AppendLine(TooltipText.Row("Known", $"{known.Count} of {cells.Count} cells, {cells.Count(t => t.explored)} explored"));
        foreach (var g in land.Where(t => t.macroBiome != null).GroupBy(t => t.macroBiome).OrderByDescending(g => g.Count()).Take(4))
            text.AppendLine(TooltipText.Bullet($"{gen.MacroBiome(g.Key)?.name ?? g.Key}: {g.Count() / (float)Math.Max(1, land.Count):P0}"));
        if (land.Count > 0)
        {
            text.AppendLine(TooltipText.Row("Land fertility", $"{land.Average(t => t.landFertility):P0} on average"));
            text.AppendLine(TooltipText.Row("Coherence", $"{land.Average(t => t.coherence):P0} on average"));
        }
        var found = known.Where(t => t.HasFeature && t.explored).GroupBy(t => t.feature).Select(g => $"{gen.Feature(g.Key)?.name ?? g.Key}{(g.Count() > 1 ? $" x{g.Count()}" : string.Empty)}").ToList();
        if (found.Count > 0) text.AppendLine(TooltipText.Row("Found", string.Join(", ", found)));
        text.AppendLine(TooltipText.Muted("Zoom in to command units."));
    }
}
