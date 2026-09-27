using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The Government tab: the Head of State, the open council positions, the active civics, and the two pools that open
/// when a position is clicked (legends to seat there, and other seats to put there instead).
///
/// A view, never a rule: every change goes through <see cref="GovernmentLogic"/> / <see cref="CivicManager"/>, whose
/// events only mark parts of the tab dirty. The tab redraws each dirty part once, at the end of the frame, re-binding
/// its existing views in place (<see cref="ViewList{TView}"/>), so a burst of events (seating a legend raises three
/// or four) or the per-seventh cooldown tick costs one cheap redraw and no new objects. Showing and hiding the tab
/// itself is <see cref="TabHotkeys"/>'s job.
/// </summary>
public class GovernmentTab : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The tab's Display (shown and hidden by TabHotkeys); the pools close while it is hidden")]
    [SerializeField] private CanvasGroup displayCanvasGroup;
    [SerializeField] private CanvasGroup civicPoolCanvasGroup;
    [SerializeField] private CanvasGroup leaderPoolCanvasGroup;

    [Header("Council")]
    [Tooltip("The Head of State's seat view (HeadOfState in the scene)")]
    [SerializeField] private SeatPositionDisplay headOfStateSeat;
    [SerializeField] private Transform councilSeatsContainer;

    [Header("Pools")]
    [SerializeField] private Transform leaderPoolContent;
    [SerializeField] private Transform civicPoolContent;

    [Header("Civic Tier Containers")]
    [SerializeField] private Transform aeonicCivicsContainer;
    [SerializeField] private Transform majorCivicsContainer;
    [SerializeField] private Transform minorCivicsContainer;

    [Header("Prefabs")]
    [SerializeField] private GameObject seatPositionPrefab;
    [SerializeField] private GameObject leaderSlotPrefab;
    [SerializeField] private GameObject civicDisplayPrefab;
    [Tooltip("A seat in the seat pool")]
    [SerializeField] private GameObject civicDetailedPrefab;

    private const LogChannel Log = LogChannel.GovernmentUI;

    // Position the pools are open for (Head of State = -1).
    private int _selectedSeatIndex = GovernmentLogic.HeadOfStateIndex;

    private ViewList<SeatPositionDisplay> _seats;
    private ViewList<LeaderSlotDisplay> _leaders;
    private ViewList<CivicDetailedDisplay> _seatOptions;
    private readonly Dictionary<CivicTier, ViewList<CivicDisplay>> _civicsByTier = new Dictionary<CivicTier, ViewList<CivicDisplay>>();

    private bool _councilDirty, _civicsDirty, _poolsDirty;
    private GovernmentLogic _government;
    private CivicManager _civics;

    // ===== LIFECYCLE =====

    private void Awake()
    {
        _seats = new ViewList<SeatPositionDisplay>(seatPositionPrefab, councilSeatsContainer, view => view.OnSeatClicked += OnSeatClicked);
        _leaders = new ViewList<LeaderSlotDisplay>(leaderSlotPrefab, leaderPoolContent, view => view.OnLeaderSelected += OnLeaderSelected);
        _seatOptions = new ViewList<CivicDetailedDisplay>(civicDetailedPrefab, civicPoolContent, view => view.OnCivicClicked += OnSeatOptionClicked);
        _civicsByTier[CivicTier.Aeonic] = new ViewList<CivicDisplay>(civicDisplayPrefab, aeonicCivicsContainer);
        _civicsByTier[CivicTier.Major] = new ViewList<CivicDisplay>(civicDisplayPrefab, majorCivicsContainer);
        _civicsByTier[CivicTier.Minor] = new ViewList<CivicDisplay>(civicDisplayPrefab, minorCivicsContainer);

        if (headOfStateSeat != null) headOfStateSeat.OnSeatClicked += OnSeatClicked;
        else GameLog.Warning("GovernmentTab has no Head of State seat view assigned; the Head of State cannot be changed.", Log);
    }

    // Singletons register in Awake, so the systems exist here; the council itself is built in GovernmentLogic.Start,
    // which has run by the first LateUpdate, where the tab draws for the first time.
    private void Start()
    {
        _government = GovernmentLogic.Instance;
        _civics = CivicManager.Instance;
        if (_government != null)
        {
            _government.OnCouncilCompositionChanged += MarkCouncilDirty;
            _government.OnCouncilSeatChanged += OnCouncilSeatChanged;
            _government.OnLeaderPoolChanged += MarkCouncilDirty; // every seventh: cooldowns and activation
            _government.OnCivicPoolChanged += MarkPoolsDirty;
        }
        if (_civics != null)
        {
            _civics.OnActiveCivicsChanged += MarkCivicsDirty;
            _civics.OnCivicPoolChanged += MarkPoolsDirty;
        }
        SetPool(leaderPoolCanvasGroup, false);
        SetPool(civicPoolCanvasGroup, false);
        _councilDirty = _civicsDirty = true;
    }

    private void OnDestroy()
    {
        if (_government != null)
        {
            _government.OnCouncilCompositionChanged -= MarkCouncilDirty;
            _government.OnCouncilSeatChanged -= OnCouncilSeatChanged;
            _government.OnLeaderPoolChanged -= MarkCouncilDirty;
            _government.OnCivicPoolChanged -= MarkPoolsDirty;
        }
        if (_civics != null)
        {
            _civics.OnActiveCivicsChanged -= MarkCivicsDirty;
            _civics.OnCivicPoolChanged -= MarkPoolsDirty;
        }
        if (headOfStateSeat != null) headOfStateSeat.OnSeatClicked -= OnSeatClicked;
    }

    private void MarkCouncilDirty() { _councilDirty = true; _poolsDirty = true; }

    private void OnCouncilSeatChanged(CouncilSeat seat) => MarkCouncilDirty();

    private void MarkCivicsDirty() => _civicsDirty = true;

    private void MarkPoolsDirty() => _poolsDirty = true;

    private void LateUpdate()
    {
        if (PoolsOpen && displayCanvasGroup != null && displayCanvasGroup.alpha <= 0f) ClosePools(); // tab hidden
        if (_councilDirty) DrawCouncil();
        if (_civicsDirty) DrawCivics();
        if (_poolsDirty) DrawPools();
    }

    // ===== DRAWING =====

    private void DrawCouncil()
    {
        _councilDirty = false;
        if (_government == null) return;
        var head = _government.GetCouncilSeat(GovernmentLogic.HeadOfStateIndex);
        if (headOfStateSeat != null && head != null) headOfStateSeat.Bind(head, GovernmentLogic.HeadOfStateIndex);
        var seats = _government.GetActiveRegularSeats().Where(seat => seat != null).ToList();
        _seats.Show(seats, (view, seat) => view.Bind(seat, seat.seatIndex));
    }

    private void DrawCivics()
    {
        _civicsDirty = false;
        var active = _civics != null ? _civics.GetAllActiveCivics() : new List<CivicData>();
        foreach (var pair in _civicsByTier)
        {
            pair.Value.Show(active.Where(civic => civic.tier == pair.Key).ToList(), (view, civic) => view.Bind(civic));
        }
    }

    private void DrawPools()
    {
        _poolsDirty = false;
        if (_government == null) return;
        if (IsOpen(leaderPoolCanvasGroup)) _leaders.Show(LegendsForSelectedSeat(), (view, legend) => view.Bind(legend, _selectedSeatIndex));
        if (IsOpen(civicPoolCanvasGroup)) _seatOptions.Show(SeatOptions(), (view, title) => view.Bind(title));
    }

    /// <summary>Legends that qualify for the selected seat and can move now: free legends first, then seated ones (a swap).</summary>
    private List<LegendData> LegendsForSelectedSeat()
    {
        return _government.GetAvailableLegendsForSeat(_selectedSeatIndex)
            .OrderBy(legend => _government.GetSeatWithLegend(legend.legendName) != null)
            .ToList();
    }

    /// <summary>Seats that can take the selected position: every default or civic seat not already on the council.</summary>
    private List<string> SeatOptions() => _government.GetAvailableSeatTitles().Where(title => !_government.IsSeatTitleActive(title)).ToList();

    // ===== INTERACTION =====

    /// <summary>A position was clicked: offer legends for it, and (regular positions only) other seats to put there.</summary>
    private void OnSeatClicked(int seatIndex)
    {
        _selectedSeatIndex = seatIndex;
        SetPool(leaderPoolCanvasGroup, true);
        SetPool(civicPoolCanvasGroup, seatIndex != GovernmentLogic.HeadOfStateIndex);
        _poolsDirty = true;
    }

    private void OnLeaderSelected(LegendData legend)
    {
        if (_government != null && _government.AssignLegendToSeat(legend, _selectedSeatIndex)) ClosePools();
    }

    private void OnSeatOptionClicked(string seatTitle)
    {
        if (_government == null || _selectedSeatIndex == GovernmentLogic.HeadOfStateIndex) return;
        if (_government.ReplaceSeatWithAvailable(_selectedSeatIndex, seatTitle)) ClosePools();
        else MarkPoolsDirty(); // the pool was stale: show what is available now
    }

    private bool PoolsOpen => IsOpen(leaderPoolCanvasGroup) || IsOpen(civicPoolCanvasGroup);

    private void ClosePools()
    {
        SetPool(leaderPoolCanvasGroup, false);
        SetPool(civicPoolCanvasGroup, false);
    }

    private static bool IsOpen(CanvasGroup group) => group != null && group.alpha > 0f;

    private static void SetPool(CanvasGroup group, bool open)
    {
        if (group == null) return;
        group.alpha = open ? 1f : 0f;
        group.blocksRaycasts = open;
        group.interactable = open;
    }
}
