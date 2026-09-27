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
/// stay over it. The lower-left dock opens small panels (Lens, Layers, Units, the scale, the way home); a card under
/// the cursor tells everything about the cell it rests on (at the micro reading, about the hex too); a card in the
/// lower right shows the selected unit (how it fares: rations, fatigue, attrition), settlement, enclave or cell and
/// what can be done with it (train units at a settlement, claim explored wilderness).
/// Left click selects (the unit under the cursor, else what stands on the cell; again: the next one), right click
/// sends the selected unit: at the micro reading to the hex itself, beyond it to the heart of the cell (either way to
/// the nearest ground it can reach), with Shift to survey on arrival; any drag pans. Units walk the micro hexes
/// (<see cref="MicroGrid"/>). Built in code on its own canvas, under the event overlay so stories still show; time
/// keeps running.
/// </summary>
public class WorldView : MonoBehaviour
{
    public enum Mode { Capital, Opening, World, Closing }
    private enum Popup { None, Lens, Layers, Units, Realm }

    private const float OpenSeconds = 0.75f, CloseSeconds = 0.6f, RefreshSeconds = 0.5f, DragPixels = 6f;
    private const float CardWidth = 440f, HoverWidth = 390f, Pad = 16f, Inner = 18f;
    private const int ActionSlots = 10, UnitRows = 10;
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
    private TextMeshProUGUI _hoverText, _cardText, _lensLegend, _layersInfo, _unitsText, _realmText;
    private readonly Dictionary<Popup, RectTransform> _popups = new Dictionary<Popup, RectTransform>();
    private TextMeshProUGUI _lensDock, _layersDock, _unitsDock, _scaleDock, _realmDock;
    private readonly Dictionary<BorderPolicy, TextMeshProUGUI> _policyButtons = new Dictionary<BorderPolicy, TextMeshProUGUI>();
    private readonly Dictionary<WorldLens, TextMeshProUGUI> _lensButtons = new Dictionary<WorldLens, TextMeshProUGUI>();
    private TextMeshProUGUI _riversButton, _leylinesButton, _forecastButton, _previousButton;
    private readonly TextMeshProUGUI[] _unitButtons = new TextMeshProUGUI[UnitRows];
    private readonly TextMeshProUGUI[] _actionLabels = new TextMeshProUGUI[ActionSlots];
    private readonly Button[] _actionButtons = new Button[ActionSlots];
    private readonly Action[] _actionCalls = new Action[ActionSlots];
    private Popup _popup;

    // Interaction
    private int _selectedUnit = -1;
    private HexCoord? _selected, _selectedMacro;
    private bool _rivers = true, _leylines, _forecast, _previous;
    private Vector2 _pressAt;
    private int _pressButton = -1;
    private bool _dragging;
    private float _refreshAt, _hoverAt, _lockedNoticeAt, _knowledgeAt;
    private int _knowledgeSeen = -1, _magicSeen = -1, _civilizationSeen = -1;
    private int _weatherSeen = -1;
    private HexCoord? _hovered, _hoveredMicro;
    private Preview _preview;
    // The Settle lens's field and the cells where a town could be founded now (recomputed when the world changes).
    private float[] _potential;
    private HashSet<int> _foundable;
    private int _potentialStamp = -1;
    // The cells society will adopt next (the Territory lens draws them bright).
    private HashSet<int> _nextAdoptions = new HashSet<int>();
    private readonly List<WorldRenderer.UnitMark> _marks = new List<WorldRenderer.UnitMark>();
    private readonly List<Vector2> _path = new List<Vector2>();

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
        if (LegendProgress.Instance != null) LegendProgress.Instance.Changed -= MarkDirty;
        RestoreCapital();
        _renderer?.Destroy();
        if (_instance == this) _instance = null;
    }

    // Esc steps back: a popup, then the selection, then the capital.
    private void OnCancel()
    {
        if (_mode != Mode.World && _mode != Mode.Opening) return;
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
        if (_selectedUnit < 0 && world.Map.Units.Count > 0) _selectedUnit = world.Map.Units[0].id;
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
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        _renderer.SetLevel(WorldZoom.Level(_size), perPixel, pulse);
        DrawUnits(pulse);
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
        _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / (opening ? OpenSeconds : CloseSeconds));
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

        if (InputUtils.KeyDown(Key.Period)) NextIdleUnit();

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
        if (WorldZoom.ScaleOf(_size) == WorldScale.Macro)
        {
            Deselect();
            _selectedMacro = WorldMap.MacroOf(tile.coord);
            Refresh(force: true);
            return;
        }
        // Clicking again walks through what is there: the units drawn under the cursor first (nearest first), then
        // the cell's settlement or enclave (a settlement trains units); with no unit under the cursor, the cell's
        // place first and then each unit standing in the cell. Bare land is selected too (it can be claimed).
        float pick = Mathf.Max(0.9f, 16f * PerPixel);
        var under = world.Map.Units.Where(u => !u.Missing)
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
        else
        {
            if (place) order.Add(-1);
            order.AddRange(world.UnitsAt(tile.coord).Select(u => u.id));
        }
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
        Refresh(force: true);
    }

    // Right click sends the selected unit: at the micro reading to the hex itself, beyond it to the heart of the cell
    // (either way to the nearest ground it can reach). With Shift held it surveys where it arrives: around the hex, or
    // the whole meso hex.
    private void Order(Vector2 point)
    {
        var world = WorldSystem.Instance;
        var unit = world?.UnitById(_selectedUnit);
        var tile = world?.Map.At(point.x, point.y);
        if (unit == null || tile == null) return;
        bool survey = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        if (WorldZoom.ScaleOf(_size) == WorldScale.Micro)
        {
            var hex = HexHierarchy.MicroAt(point.x, point.y);
            if (survey) world.GoAndSurveyMicro(unit, hex);
            else world.GoMicro(unit, hex);
        }
        else if (survey) world.GoAndSurvey(unit, tile.coord);
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

    private void CycleScale()
    {
        if (_mode != Mode.World) return;
        var next = WorldZoom.ScaleOf(_size) == WorldScale.Micro ? WorldScale.Meso : WorldZoom.ScaleOf(_size) == WorldScale.Meso ? WorldScale.Macro : WorldScale.Micro;
        _size = WorldZoom.SizeFor(next, _farthest);
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
        if (LegendProgress.Instance != null) LegendProgress.Instance.Changed += MarkDirty;
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
        _cardText = CodeUI.Label(_card, "Text", string.Empty, _theme.bodySize - 2f, _theme.bodyColor, FontStyles.Normal, _theme);
        _cardText.lineSpacing = TooltipText.LineSpacing;
        _cardText.alignment = TextAlignmentOptions.TopLeft;
        _cardText.overflowMode = TextOverflowModes.Ellipsis;
        for (int i = 0; i < ActionSlots; i++)
        {
            int slot = i;
            var label = CodeUI.TextButton(_card, string.Empty, () => _actionCalls[slot]?.Invoke(), _theme, _theme.subtitleSize + 1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            _actionLabels[i] = label;
            _actionButtons[i] = label.GetComponent<Button>();
        }
        _card.gameObject.SetActive(false);
    }

    private TextMeshProUGUI DockButton(RectTransform dock, string name, Action onClick, string title, string tip, ref float x, float width)
    {
        var button = CodeUI.TextButton(dock, name, onClick, _theme, _theme.subtitleSize + 3f);
        button.alignment = TextAlignmentOptions.Center;
        button.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(button.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x, 6f), new Vector2(x + width, -6f));
        TooltipTrigger.Ensure(button.gameObject).SetCustom(title, tip);
        x += width + 6f;
        return button;
    }

    private void BuildDock()
    {
        var dock = CodeUI.Panel(_canvas.transform, "Dock", Vector2.zero, Vector2.zero);
        dock.pivot = Vector2.zero;
        dock.sizeDelta = new Vector2(946f, 54f);
        dock.anchoredPosition = new Vector2(Pad, Pad);
        CodeUI.Plate(dock, _theme, _scaler, 0.9f);
        float x = Inner;
        _lensDock = DockButton(dock, "Lens", () => ShowPopup(Popup.Lens), "Lens", "One reading of the map at a time, each tied to City Development: Settle, fertility, Coherence and leylines, magic, authority, trade, grandfields, danger.", ref x, 210f);
        _layersDock = DockButton(dock, "Layers", () => ShowPopup(Popup.Layers), "Layers", "Rivers, leylines, the next Age's forecast, the previous Age's paths, and what the land yields.", ref x, 120f);
        _unitsDock = DockButton(dock, "Units", () => ShowPopup(Popup.Units), "Units", "Every unit on the map: click one to select it and look at it. Click the Capital (or another settlement) to train units.", ref x, 130f);
        _realmDock = DockButton(dock, "Realm", () => ShowPopup(Popup.Realm), "Realm", "Administrative Capacity, territorial pull and the border policy: how much land your administration can hold, and whether growing wider or taller pays now.", ref x, 140f);
        _scaleDock = DockButton(dock, "Meso", CycleScale, "Scale", "Micro, meso or macro reading (the mouse wheel zooms too).", ref x, 130f);
        DockButton(dock, "Capital", Close, "Return to the capital", "Back to the capital (Esc, or scroll in over it at the closest zoom).", ref x, 160f);
    }

    private RectTransform PopupPanel(string name, Vector2 size)
    {
        var panel = CodeUI.Panel(_canvas.transform, name, Vector2.zero, Vector2.zero);
        panel.pivot = Vector2.zero;
        panel.sizeDelta = size;
        panel.anchoredPosition = new Vector2(Pad, Pad + 62f);
        CodeUI.Plate(panel, _theme, _scaler, 0.94f);
        panel.gameObject.SetActive(false);
        return panel;
    }

    private void BuildPopups()
    {
        // Lens: fifteen readings in two columns, and what the current one shows.
        var lens = PopupPanel("Lens Panel", new Vector2(360f, 360f));
        float y = -Inner;
        int column = 0;
        foreach (var l in WorldLenses.All)
        {
            var mode = l;
            var button = CodeUI.TextButton(lens, WorldLenses.ShortName(l), () => SetLens(mode), _theme, _theme.subtitleSize + 1f);
            float bx = Inner + column * 160f;
            CodeUI.Place(button.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(bx, y - 28f), new Vector2(bx + 160f, y));
            TooltipTrigger.Ensure(button.gameObject).SetCustom(WorldLenses.Name(l), WorldLenses.Legend(l));
            _lensButtons[mode] = button;
            if (++column == 2) { column = 0; y -= 30f; }
        }
        _lensLegend = CodeUI.Label(lens, "Legend", string.Empty, _theme.bodySize - 4f, _theme.bodyColor, FontStyles.Normal, _theme);
        _lensLegend.alignment = TextAlignmentOptions.TopLeft;
        CodeUI.Place(_lensLegend.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(Inner, 12f), new Vector2(-Inner, 12f + 110f));
        _popups[Popup.Lens] = lens;

        // Layers: four toggles and the world's ledger.
        var layers = PopupPanel("Layers Panel", new Vector2(420f, 360f));
        y = -Inner;
        _riversButton = Toggle(layers, () => { _rivers = !_rivers; ApplyLayers(); }, ref y);
        _leylinesButton = Toggle(layers, () => { _leylines = !_leylines; ApplyLayers(); }, ref y);
        _forecastButton = Toggle(layers, () => { _forecast = !_forecast; if (_forecast) _leylines = true; ApplyLayers(); }, ref y);
        _previousButton = Toggle(layers, () => { _previous = !_previous; ApplyLayers(); }, ref y);
        _layersInfo = CodeUI.Label(layers, "Info", string.Empty, _theme.bodySize - 4f, _theme.bodyColor, FontStyles.Normal, _theme);
        _layersInfo.alignment = TextAlignmentOptions.TopLeft;
        _layersInfo.lineSpacing = TooltipText.LineSpacing;
        CodeUI.Place(_layersInfo.rectTransform, Vector2.zero, Vector2.one, new Vector2(Inner, 12f), new Vector2(-Inner, y - 6f));
        _popups[Popup.Layers] = layers;

        // Units: one row each, click to select and look.
        var units = PopupPanel("Units Panel", new Vector2(460f, 60f + UnitRows * 30f));
        y = -Inner;
        for (int i = 0; i < UnitRows; i++)
        {
            int row = i;
            var button = CodeUI.TextButton(units, string.Empty, () => PickUnitRow(row), _theme, _theme.subtitleSize);
            button.textWrappingMode = TextWrappingModes.NoWrap;
            button.overflowMode = TextOverflowModes.Ellipsis;
            CodeUI.Place(button.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Inner, y - 28f), new Vector2(-Inner, y));
            _unitButtons[i] = button;
            y -= 30f;
        }
        _unitsText = CodeUI.Label(units, "Hint", string.Empty, _theme.bodySize - 4f, _theme.bodyColor, FontStyles.Normal, _theme);
        _unitsText.alignment = TextAlignmentOptions.BottomLeft;
        CodeUI.Place(_unitsText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(Inner, 10f), new Vector2(-Inner, 40f));
        _popups[Popup.Units] = units;

        // Realm: the administration's ledger, and the border policy (three buttons along the bottom).
        var realm = PopupPanel("Realm Panel", new Vector2(560f, 520f));
        float px = Inner;
        foreach (BorderPolicy policy in Enum.GetValues(typeof(BorderPolicy)))
        {
            var choice = policy;
            var button = CodeUI.TextButton(realm, RealmReport.Name(policy), () => WorldSystem.Instance?.SetBorderPolicy(choice), _theme, _theme.subtitleSize);
            button.alignment = TextAlignmentOptions.Center;
            button.textWrappingMode = TextWrappingModes.NoWrap;
            CodeUI.Place(button.rectTransform, Vector2.zero, Vector2.zero, new Vector2(px, 12f), new Vector2(px + 168f, 42f));
            TooltipTrigger.Ensure(button.gameObject).SetCustom(RealmReport.Name(policy), RealmReport.Describe(policy));
            _policyButtons[policy] = button;
            px += 174f;
        }
        _realmText = CodeUI.Label(realm, "Ledger", string.Empty, _theme.bodySize - 4f, _theme.bodyColor, FontStyles.Normal, _theme);
        _realmText.alignment = TextAlignmentOptions.TopLeft;
        _realmText.lineSpacing = TooltipText.LineSpacing;
        _realmText.overflowMode = TextOverflowModes.Ellipsis;
        CodeUI.Place(_realmText.rectTransform, Vector2.zero, Vector2.one, new Vector2(Inner, 50f), new Vector2(-Inner, -Inner));
        _popups[Popup.Realm] = realm;
    }

    private TextMeshProUGUI Toggle(RectTransform parent, Action onClick, ref float y)
    {
        var button = CodeUI.TextButton(parent, string.Empty, onClick, _theme, _theme.subtitleSize + 1f);
        CodeUI.Place(button.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Inner, y - 28f), new Vector2(-Inner, y));
        y -= 30f;
        return button;
    }

    private void ShowPopup(Popup popup)
    {
        _popup = _popup == popup ? Popup.None : popup;
        foreach (var pair in _popups) pair.Value.gameObject.SetActive(pair.Key == _popup);
        Refresh(force: true);
    }

    private void PickUnitRow(int row)
    {
        var world = WorldSystem.Instance;
        if (world == null || row >= world.Map.Units.Count) return;
        SelectUnit(world.Map.Units[row].id);
        ShowPopup(Popup.None);
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

    // The cells a lens draws bright: where a town could be founded (Settle), what society adopts next (Territory).
    private ICollection<int> Highlights(WorldLens lens)
    {
        if (lens != WorldLens.Territory) return _foundable;
        var world = WorldSystem.Instance;
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

    private static float ShapeOf(UnitSpec spec) => spec == null ? 1f : spec.role == UnitRole.Scout ? 3f : spec.role == UnitRole.Builder ? 4f : spec.role == UnitRole.Expedition ? 2f : 1f;

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
            if (unit.Missing) continue;
            var spec = world.SpecOf(unit);
            var (x, y) = WorldUnits.Position(map, gen, unit);
            bool selected = unit.id == _selectedUnit;
            var color = spec != null ? spec.color : Color.white;
            if (unit.Working) color = Color.Lerp(color, Color.white, 0.4f * pulse);
            bool distress = unit.hungry || unit.attrition >= 65f || Spiraling(unit).Count > 0;
            _marks.Add(new WorldRenderer.UnitMark { position = new Vector2(x, y), color = color, shape = ShapeOf(spec), selected = selected, camping = unit.Camping, distress = distress });
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
        if (_path.Count == 0 && chosen != null && _mode == Mode.World && !PointerOverUI())
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
        _renderer.SetUnits(_marks, _path, pulse, pathColor);
        bool hexes = WorldZoom.ScaleOf(_size) == WorldScale.Micro && _mode == Mode.World && !PointerOverUI();
        _renderer.SetMicroHighlight(hexes ? _hoveredMicro : null, destination);
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
        _hoverText.text = KeywordMarkup.SafeGlyphs(HoverText(world, tile));
        float height = _hoverText.GetPreferredValues(_hoverText.text, HoverWidth - 2f * Inner, 0f).y + 26f;
        _hoverCard.sizeDelta = new Vector2(HoverWidth, height);
        _hoverCard.gameObject.SetActive(true);
        FollowCursor();
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
        _hoverCard.anchoredPosition = pos;
    }

    /// <summary>Everything a cell means: its ground and place, travel fatigue, fields, water and magic, what stands there, what it yields and could become, and where the selected unit could go.</summary>
    private string HoverText(WorldSystem world, WorldTile tile)
    {
        var gen = world.Settings.generation;
        var rules = world.Rules;
        var text = new StringBuilder();
        var feature = tile.HasFeature ? gen.Feature(tile.feature) : null;
        bool landmark = feature != null && feature.visibleFromAfar && tile.revealed;
        if (!tile.known)
        {
            // The fog, or unknown wilderness: seen from afar, but no scout has walked it.
            text.AppendLine(TooltipText.Heading(landmark ? feature.name : tile.revealed ? "Unknown wilderness" : "The Fog", tile.revealed ? "Seen, not known" : "Unknown"));
            text.AppendLine(TooltipText.Muted(landmark ? "Seen from afar; send a scout to learn more." : tile.revealed ? "Out of the fog, but no scout has passed over it: its ground and what stands there are unknown." : "Nothing is known of it. Send a scout."));
            if (tile.revealed && tile.water) text.AppendLine(TooltipText.Row("Water", "open water"));
            AppendTravel(world, tile, text);
            return text.ToString().TrimEnd();
        }
        var terrain = gen.Terrain(tile.terrain);
        var biome = gen.Biome(tile.biome);
        text.AppendLine(TooltipText.Heading(world.Place(tile), tile.coord == world.Map.Capital ? TooltipText.Good("Your capital") : tile.explored ? TooltipText.Good("Explored") : "Known, not surveyed"));
        var where = new List<string>();
        if (terrain != null && world.Place(tile) != terrain.name) where.Add(terrain.name);
        if (biome != null) where.Add(biome.name);
        if (tile.region == WorldRegion.Connective) where.Add("connective ground");
        if (where.Count > 0) text.AppendLine(TooltipText.Muted(string.Join(", ", where)));
        if (WorldZoom.ScaleOf(_size) == WorldScale.Micro && _hoveredMicro.HasValue && HexHierarchy.Parent(_hoveredMicro.Value) == tile.coord)
            AppendHex(world, tile, _hoveredMicro.Value, text);
        if (feature != null && !string.IsNullOrEmpty(feature.description)) text.AppendLine(TooltipText.Quote($"<i>{feature.description}</i>"));
        if (feature != null && !tile.explored && tile.coord != world.Map.Capital) text.AppendLine(TooltipText.Warn("Not investigated yet: survey it (an expedition brings back more)."));
        var forage = WorldUnits.ForageOf(gen, tile, 1f);
        if (forage.Count > 0) text.AppendLine(TooltipText.Row("Forage", tile.foragedAge == world.AgeNumber + 1 ? TooltipText.Muted("gathered this Age") : string.Join(", ", forage.Select(a => $"{a.amount:0.#} {a.resource}"))));

        float fatigue = WorldPaths.StepCost(tile, gen);
        if (float.IsPositiveInfinity(fatigue)) text.AppendLine(TooltipText.Row("Travel fatigue", tile.water ? "open water" : "impassable"));
        else
        {
            var why = new List<string>();
            if (tile.road) why.Add("road");
            if (tile.leylines != 0) why.Add("leyline");
            if (tile.danger > 0.01f || tile.dissonance > 0.01f) why.Add("dissonance and danger");
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
        if (tile.danger > 0.01f) text.AppendLine(TooltipText.Row("Danger", TooltipText.Warn($"{tile.danger:P0}")));
        string owner = tile.authorityId == WorldAuthority.Player ? TooltipText.Good("Your authority") : tile.authorityId == WorldAuthority.Outpost ? "Your Outpost"
            : tile.authorityId == WorldAuthority.Wilderness ? TooltipText.Muted(tile.water ? "Unclaimed waters" : "Wilderness")
            : tile.enclave >= 0 || tile.authorityId.StartsWith("enclave:", StringComparison.Ordinal) ? EnclaveOwner(world, tile) : tile.authorityId;
        text.AppendLine(TooltipText.Row("Held by", owner));
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
        string lens = _renderer.Lens != WorldLens.Normal ? WorldLenses.Hover(_renderer.Lens, world.Map, tile, rules, _potential, gen) : null;
        if (!string.IsNullOrEmpty(lens)) text.AppendLine(TooltipText.Muted(lens));
        AppendTravel(world, tile, text);
        return text.ToString().TrimEnd();
    }

    private static string EnclaveOwner(WorldSystem world, WorldTile tile)
    {
        int index = tile.enclave;
        if (index < 0 && tile.authorityId.StartsWith("enclave:", StringComparison.Ordinal)) int.TryParse(tile.authorityId.Substring(8), out index);
        return index >= 0 && index < world.Map.Enclaves.Count ? $"{world.Map.Enclaves[index].name} ({world.Map.Enclaves[index].family})" : "an enclave";
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
            text.AppendLine(TooltipText.Muted(p.micro ? "Shift + right click: survey around the hex on arrival." : "Shift + right click: survey the meso hex on arrival."));
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
            if (_renderer.Lens == WorldLens.Settle) _renderer.RefreshLens(LensField(WorldLens.Settle), Highlights(WorldLens.Settle));
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
        if (moved) _renderer.RefreshLens(LensField(_renderer.Lens), Highlights(_renderer.Lens));
        if (world.UnitById(_selectedUnit) == null) _selectedUnit = -1;
        _renderer.SetState(_selected, _selectedMacro, Enumerable.Empty<HexCoord>(), Enumerable.Empty<HexCoord>());
        DrawHud(world);
    }

    private void DrawHud(WorldSystem world)
    {
        _lensDock.text = $"Lens: {WorldLenses.ShortName(_renderer.Lens)}";
        _unitsDock.text = $"Units {world.Map.Units.Count}";
        _scaleDock.text = WorldZoom.Name(WorldZoom.ScaleOf(_size));
        if (_popup == Popup.Lens)
        {
            foreach (var pair in _lensButtons)
            {
                string name = WorldLenses.ShortName(pair.Key);
                pair.Value.text = pair.Key == _renderer.Lens ? $"<u>{name}</u>" : TooltipText.Muted(name);
            }
            _lensLegend.text = KeywordMarkup.SafeGlyphs(TooltipText.Muted(WorldLenses.Legend(_renderer.Lens)));
        }
        if (_popup == Popup.Layers)
        {
            _riversButton.text = ToggleText("Rivers", _rivers);
            _leylinesButton.text = ToggleText("Leylines", _leylines);
            _forecastButton.text = ToggleText("Next Age forecast", _forecast);
            _previousButton.text = ToggleText("Previous Age leylines", _previous);
            _layersInfo.text = KeywordMarkup.SafeGlyphs(LedgerText(world));
        }
        if (_popup == Popup.Units) DrawUnitList(world);
        var realm = world.Realm;
        _realmDock.text = realm.favoured == Expansion.Overextended ? $"Realm {TooltipText.Warn($"{realm.strain:P0}")}" : $"Realm {realm.strain:P0}";
        if (_popup == Popup.Realm)
        {
            foreach (var pair in _policyButtons)
                pair.Value.text = pair.Key == world.BorderPolicy ? $"<u>{RealmReport.Name(pair.Key)}</u>" : TooltipText.Muted(RealmReport.Name(pair.Key));
            _realmText.text = KeywordMarkup.SafeGlyphs(RealmText(world, realm));
        }
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
        text.AppendLine(TooltipText.Row("Held", $"{realm.cells} cells ({realm.adopted} adopted, {realm.claimed} claimed), {realm.averageLoad:0.00} load each"));
        text.AppendLine(TooltipText.Row("Wide or tall", $"horizontal pays to ~{realm.comfortCells:0} cells, breaks even at ~{realm.breakEvenCells:0}, society stops at ~{realm.stopCells:0}"));
        text.AppendLine(TooltipText.Row("Held land", $"Coherence {realm.averageCoherence:P0}, beauty {WorldBeauty.Word(realm.averageBeauty)}, City Development {realm.averageDevelopment:0} on average"));
        text.AppendLine(TooltipText.Muted(realm.Advice));
        var sources = realm.capacitySources.Select(s => $"{s.source} {s.amount:+0.#;-0.#}").ToList();
        text.AppendLine(TooltipText.Row("Capacity from", string.Join(", ", sources)));
        var map = world.Map;
        var seats = map.territory?.Seats.Where(s => s.IsPlayers).ToList() ?? new List<TerritorySeat>();
        if (seats.Count > 0)
            text.AppendLine(TooltipText.Row("Seats", string.Join(", ", seats.Take(8).Select(s => $"{s.name} {s.held}/{s.maxCells}")) + (seats.Count > 8 ? $" and {seats.Count - 8} more" : string.Empty)));
        string rate = world.BorderPolicy == BorderPolicy.Hold ? "held by policy" : realm.adoptionRate > 0f ? $"{realm.adoptionRate:0.##} cells per Seventh" : "none now";
        text.AppendLine(TooltipText.Row("Adoption", !world.MapUnlocked || !world.IsOpen ? TooltipText.Muted("begins once the world map is open") : rate));
        var next = world.NextAdoptions(3);
        if (next.Count > 0)
            text.AppendLine(TooltipText.Row("Next", string.Join(", ", next.Select(c => $"{world.Place(map[c.cell])} ({c.seat.name}, priority {c.priority:0.00})"))));
        return text.ToString().TrimEnd();
    }

    private static string ToggleText(string name, bool on) => on ? $"{name}: on" : TooltipText.Muted($"{name}: off");

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
        var units = world.Map.Units;
        for (int i = 0; i < UnitRows; i++)
        {
            bool used = i < units.Count;
            _unitButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            var u = units[i];
            var spec = world.SpecOf(u);
            string status = u.Missing ? "missing in action" : u.retreating && u.Moving ? "retreating" : u.Working ? TaskWord(u.task) : u.Camping ? (u.resting ? "resting in camp" : "camped")
                : u.returning ? "walking back for rations" : u.autoExplore ? "exploring by itself"
                : u.Moving ? $"walking, {Sevenths(WorldUnits.SeventhsLeft(world.Map, world.Settings.generation, u, spec))} left" : "waiting";
            if (u.hungry) status += ", starving";
            else if (u.attrition >= 50f) status += $", worn ({u.attrition:0})";
            int party = Expeditions.PartySize(u);
            if (party > 1) status += $", {party} legends";
            if (Spiraling(u).Count > 0) status += ", a legend Spiraling";
            string name = u.id == _selectedUnit ? $"<u>{u.name}</u>" : u.name;
            _unitButtons[i].text = $"{name} {TooltipText.Muted($"· {status}")}";
        }
        _unitsText.text = KeywordMarkup.SafeGlyphs(TooltipText.Muted(units.Count == 0 ? "No expedition walks the world. Click the Capital to form one around a legend." : units.Count > UnitRows ? $"And {units.Count - UnitRows} more." : "Left click selects, right click sends the selected unit."));
    }

    // ===== THE SELECTION CARD =====

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
            else DescribeLand(world, tile, text, actions);
        }
        if (text.Length == 0)
        {
            _card.gameObject.SetActive(false);
            return;
        }
        SetActions(actions, text);
        _cardText.text = KeywordMarkup.SafeGlyphs(text.ToString().TrimEnd());
        int rows = (Math.Min(actions.Count, ActionSlots) + 1) / 2;
        float textHeight = _cardText.GetPreferredValues(_cardText.text, CardWidth - 2f * Inner, 0f).y;
        float maxHeight = _canvasRect.rect.height * 0.62f;
        float height = Mathf.Min(maxHeight, textHeight + rows * 34f + 40f);
        _card.sizeDelta = new Vector2(CardWidth, height);
        CodeUI.Place(_cardText.rectTransform, Vector2.zero, Vector2.one, new Vector2(Inner, 14f + rows * 34f), new Vector2(-Inner, -14f));
        _card.pivot = new Vector2(1f, 0f);
        _card.anchorMin = _card.anchorMax = new Vector2(1f, 0f);
        _card.anchoredPosition = new Vector2(-Pad, Pad);
        for (int i = 0; i < ActionSlots; i++)
        {
            int row = i / 2, col = i % 2;
            float x0 = Inner + col * (CardWidth - 2f * Inner) / 2f, x1 = x0 + (CardWidth - 2f * Inner) / 2f - 6f;
            float yb = 12f + (rows - 1 - row) * 34f;
            CodeUI.Place(_actionLabels[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(x0, yb), new Vector2(x1, yb + 30f));
        }
        _card.gameObject.SetActive(true);
    }

    // A label "Name (detail)" splits into the button's name and the detail listed in the card.
    private static (string name, string detail) Split(string label)
    {
        int cut = label.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 && label.EndsWith(")", StringComparison.Ordinal) ? (label.Substring(0, cut), label.Substring(cut + 2, label.Length - cut - 3)) : (label, null);
    }

    // Short names on the buttons; each action's cost and why it cannot be taken go in the card (a shared reason once).
    private void SetActions(List<(string label, string why, Action call)> actions, StringBuilder text)
    {
        for (int i = 0; i < ActionSlots; i++)
        {
            bool used = i < actions.Count;
            _actionLabels[i].gameObject.SetActive(used);
            _actionCalls[i] = null;
            if (!used) continue;
            var (label, why, call) = actions[i];
            string name = Split(label).name;
            _actionLabels[i].text = why == null ? name : TooltipText.Muted(name);
            _actionButtons[i].interactable = why == null;
            _actionCalls[i] = () =>
            {
                call();
                Refresh(force: true);
            };
        }
        if (actions.Count == 0) return;
        var shown = actions.Take(ActionSlots).ToList();
        var reasons = shown.Where(a => a.why != null).Select(a => a.why).Distinct().ToList();
        bool shared = reasons.Count == 1 && shown.All(a => a.why == reasons[0]);
        text.AppendLine();
        if (shared) text.AppendLine(TooltipText.Warn(reasons[0]));
        foreach (var (label, why, _) in shown)
        {
            var (name, detail) = Split(label);
            if (detail == null && (why == null || shared)) continue;
            string line = detail != null ? $"{name}: {detail}" : name;
            text.AppendLine(TooltipText.Bullet(why == null || shared ? line : $"{line}. {TooltipText.Warn(why)}"));
        }
    }

    private static void DescribeUnit(WorldSystem world, WorldUnit unit, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
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
        if (spec == null) return;
        // An expedition in one of your settlements is outfitted there; in the field it works the land.
        var cell = map.Get(unit.coord);
        bool home = expedition && cell != null && cell.settlement >= 0;
        // What it can do on the land: its abilities, then how it looks after itself.
        foreach (var ability in UnitAbilities.All)
        {
            if (home || !WorldUnits.Can(spec, ability.ability) || ability.task == UnitTask.Improve) continue;
            string detail = ability.task == UnitTask.Forage ? ForageDetail(world, unit)
                : $"{Sevenths(world.WorkSevenths(unit, ability))}, {Hexes(UnitAbilities.Targets(map, gen, unit, spec, ability).Count)} to survey";
            actions.Add(($"{ability.name} ({detail})", world.WhyNotWork(unit, ability), () => world.Work(unit, ability)));
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
                if (world.CanImprove && here != null && WorldUnits.IsHotspot(map, world.Settings.generation, here))
                    actions.Add(($"Improve hotspot (level {here.improvement + 1}, {WorldSystem.CostText(world.ExpeditionRules.improveCost)}, {Sevenths(world.WorkSevenths(unit, UnitAbilities.Improve))})", world.WhyNotImprove(unit), () => world.Improve(unit)));
                PartyActions(world, unit, home, actions);
                break;
        }
        if (unit.Moving) actions.Add(("Halt", null, () => world.Halt(unit)));
        else text.AppendLine(TooltipText.Muted(WorldUnits.Can(spec, UnitAbility.Survey)
            ? "Right click the map to send it (zoomed in: to that hex; zoomed out: to the heart of the cell); Shift + right click to survey where it arrives. '.' picks the next idle unit."
            : "Right click the map to send it (zoomed in: to that hex; zoomed out: to the heart of the cell)."));
    }

    // ===== EXPEDITIONS ON THE CARDS =====

    // The legends the cards' choosers point at: a Director for a new expedition, a companion to add. Each keeps its
    // legend while it stays free to go.
    private static string _formDirector, _companionPick;

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

    // Who walks, who leads and what the road is doing to them.
    private static void DescribeParty(WorldSystem world, WorldUnit unit, StringBuilder text)
    {
        var legends = LegendProgress.Instance;
        var x = world.ExpeditionRules;
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
        if (unit.settlers > 0) text.AppendLine(TooltipText.Row("Settlers", $"{unit.settlers} citizens, to found a settlement"));
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
        if (companions.Count > 0) actions.Add(($"Make {companions[0]} Director", null, () => world.SetDirector(unit, companions[0])));
        if (home)
        {
            string pick = Chosen(world, ref _companionPick);
            int free = world.Candidates().Count;
            actions.Add(($"Companion: {pick ?? "none free"}", free == 0 ? "No legend is free to go." : free == 1 ? "No other legend is free." : null, () => _companionPick = world.NextCandidate(_companionPick)));
            actions.Add(($"Add {pick ?? "a companion"} ({WorldSystem.CostText(x.outfitCost)}{SeatNote(world, pick)})", world.WhyNotAddCompanion(unit, pick), () => world.AddCompanion(unit, _companionPick)));
            if (companions.Count > 0)
            {
                string last = companions[companions.Count - 1];
                actions.Add(($"Send {last} home", world.WhyNotSendHome(unit, last), () => world.SendHome(unit, last)));
            }
            if (unit.settlers == 0)
                actions.Add(($"Take on settlers ({world.SettlerCostText})", world.WhyNotTakeSettlers(unit), () => world.TakeSettlers(unit)));
            actions.Add(("Disband (its legends go home)", world.WhyNotDisband(unit), () => world.Disband(unit)));
            return;
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
        if (!world.MapUnlocked || s.kind == SettlementKind.Outpost) return;
        var x = world.ExpeditionRules;
        text.AppendLine(TooltipText.Row("Expedition slots", $"{world.ExpeditionSlotsUsed} of {world.ExpeditionSlots} in use"));
        text.AppendLine(TooltipText.Muted($"One per legend in the field; Government Capacity adds {x.slotsPerCapacity} each{(string.IsNullOrEmpty(x.slotBuilding) ? string.Empty : $", each {x.slotBuilding} {x.slotsPerBuilding}")}."));
        string director = Chosen(world, ref _formDirector);
        int free = world.Candidates().Count;
        actions.Add(($"Director: {director ?? "none free"}", free == 0 ? "No legend is free to go." : free == 1 ? "No other legend is free." : null, () => _formDirector = world.NextCandidate(_formDirector)));
        actions.Add(($"Form expedition ({WorldSystem.CostText(x.outfitCost)}{SeatNote(world, director)})", world.WhyNotForm(s, director), () =>
        {
            var unit = world.FormExpedition(s, _formDirector);
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
        string place = world.Place(map.Get(unit.coord));
        if (unit.retreating && unit.Moving) return $"retreating from {place}, {Sevenths(WorldUnits.SeventhsLeft(map, gen, unit, spec))} to safe ground";
        if (unit.Camping)
            return !unit.resting ? $"camped at {place}" : unit.Moving ? $"resting in camp at {place}; it walks on once rested" : $"resting in camp at {place}, taking on rations";
        if (unit.Working) return $"{TaskWord(unit.task)} at {place}, {Sevenths(unit.workLeft)} left";
        if (unit.Moving)
        {
            var end = map.Get(HexHierarchy.Parent(unit.path[unit.path.Count - 1]));
            string to = end != null && end.revealed ? $"to {world.Place(end)}" : "into the fog";
            string how = $"{Hexes(unit.path.Count)}, fatigue {WorldUnits.FatigueLeft(map, gen, unit):0.#}, about {Sevenths(WorldUnits.SeventhsLeft(map, gen, unit, spec))}";
            return unit.returning ? $"walking back {to} for rations: {how}" : unit.autoExplore ? $"exploring by itself, {to}" : $"walking {to}: {how}";
        }
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
        if (WorldUnits.Can(spec, UnitAbility.Survey)) words.Add("survey");
        if (WorldUnits.Can(spec, UnitAbility.SurveyMeso)) words.Add("sweep a meso hex");
        if (WorldUnits.Can(spec, UnitAbility.Forage)) words.Add("forage");
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
            text.AppendLine(TooltipText.Row("Territorial pull", $"holds {own.held}/{own.maxCells} cells, strength {own.strength:0.##}, reach {own.reach:0.#}, adopts {own.adoptPerSeventh:0.##}/Seventh, +{own.capacity:0.#} capacity"));
        if (s.kind != SettlementKind.Capital) text.AppendLine(TooltipText.Row("Roads", networked ? "joined to the Capital" : "isolated"));
        if (s.anchor) text.AppendLine(TooltipText.Row("Resonance Anchor", $"+{rules.anchorCoherence:P0} Coherence within {rules.anchorRadius}"));
        var extraction = WorldCivilization.Extractions(map).FirstOrDefault(x => x.by == s);
        if (extraction.field != null) text.AppendLine(TooltipText.Row("Extracting", $"{world.Settings.generation.Grandfield(extraction.field.spec)?.name} at {extraction.density:P0}"));

        // Expeditions of legends form here around a Director (not at Outposts): everything on the map is legend-run.
        ExpeditionActions(world, s, text, actions);
        if (s.kind == SettlementKind.Town) actions.Add(($"Promote ({WorldSystem.CostText(rules.promoteCost)})", world.WhyNotPromote(s), () => world.Promote(s)));
        if (s.kind == SettlementKind.Major) actions.Add(($"Attune (now {s.binding})", null, () => world.CycleBinding(s)));
        if (s.detached) actions.Add(($"Incorporate ({WorldSystem.CostText(rules.incorporateCost)})", world.WhyNotIncorporate(s), () => world.Incorporate(s)));
        if (s.kind != SettlementKind.Capital)
        {
            string why = world.WhyNotRoad(s, out _, out _, out int cells);
            if (why != "Already joined to the Capital by road.")
                actions.Add(($"Build road ({WorldSystem.CostText(rules.roadCostPerCell, Math.Max(1, cells))})", why, () => world.BuildRoad(s)));
            if (!s.anchor) actions.Add(($"Resonance Anchor ({WorldSystem.CostText(rules.anchorCost)})", world.WhyNotAnchor(s), () => world.BuildAnchor(s)));
        }
    }

    /// <summary>A cell of bare land: who holds it and, for wilderness, claiming it into your authority.</summary>
    private static void DescribeLand(WorldSystem world, WorldTile tile, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        text.AppendLine(TooltipText.Heading(world.Place(tile), tile.explored ? TooltipText.Good("Explored") : "Seen, not explored"));
        if (tile.authorityId == WorldAuthority.Player)
        {
            text.AppendLine(TooltipText.Row("Held by", TooltipText.Good("Your authority")));
            return;
        }
        if (tile.authorityId != WorldAuthority.Wilderness)
        {
            text.AppendLine(TooltipText.Row("Held by", tile.authorityId == WorldAuthority.Outpost ? "Your Outpost" : EnclaveOwner(world, tile)));
            return;
        }
        text.AppendLine(TooltipText.Row("Held by", TooltipText.Muted("Wilderness")));
        var yields = world.Settings.generation.Terrain(tile.terrain)?.yields?.Where(y => y != null && y.amount != 0f).ToList();
        if (yields != null && yields.Count > 0)
            text.AppendLine(TooltipText.Row("Its ground would yield", string.Join(", ", yields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
        string pull = WorldLenses.TerritoryHover(world.Map, tile, world.Settings.generation);
        if (!string.IsNullOrEmpty(pull)) text.AppendLine(TooltipText.Row("Territorial pull", pull));
        text.AppendLine(TooltipText.Muted("Society adopts known wilderness by itself where your seats pull it; claiming brings a bordering cell in now, paid in stored food of any kind (it still weighs on Administrative Capacity)."));
        actions.Add(($"Claim ({world.ClaimCostText})", world.WhyNotClaim(tile), () => world.Claim(tile)));
    }

    private static void DescribeEnclave(WorldSystem world, Enclave e, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var spec = world.Settings.generation.Enclave(e.spec);
        text.AppendLine(TooltipText.Heading(e.name, $"{e.family} Enclave"));
        if (spec != null && !string.IsNullOrEmpty(spec.description)) text.AppendLine(TooltipText.Quote($"<i>{spec.description}</i>"));
        text.AppendLine(TooltipText.Row("Binding", e.binding));
        text.AppendLine(TooltipText.Row("Wound Resonance", $"{e.woundResonance:0} ({e.WoundState})"));
        text.AppendLine(TooltipText.Row("Standing", e.suzerain ? "you hold its Suzerainty" : $"{e.influence:0}/100"));
        if (spec != null && spec.suzeraintyYields.Count > 0)
            text.AppendLine(TooltipText.Row(e.suzerain ? "Yields" : "As suzerain", string.Join(", ", spec.suzeraintyYields.Select(y => $"{y.amount:+0.###} {y.resource}/s"))));
        actions.Add(($"Send an envoy ({WorldSystem.CostText(world.Rules.envoyCost)})", world.WhyNotEnvoy(e), () => world.SendEnvoy(e)));
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
        foreach (var g in land.Where(t => t.biome != null).GroupBy(t => t.biome).OrderByDescending(g => g.Count()).Take(4))
            text.AppendLine(TooltipText.Bullet($"{gen.Biome(g.Key)?.name ?? g.Key}: {g.Count() / (float)Math.Max(1, land.Count):P0}"));
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
