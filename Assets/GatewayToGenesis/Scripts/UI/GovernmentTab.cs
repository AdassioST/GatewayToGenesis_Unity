using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main UI controller for the Government Tab
/// Displays Head of State, Council Seats, equipped Civics, and manages leader/civic assignment
/// </summary>
public class GovernmentTab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup displayCanvasGroup;
    [SerializeField] private CanvasGroup civicPoolCanvasGroup;
    [SerializeField] private CanvasGroup leaderPoolCanvasGroup;
    
    [Header("Content Containers")]
    [SerializeField] private Transform leaderPoolContent;
    [SerializeField] private Transform civicPoolContent;
    [SerializeField] private Transform councilSeatsContainer;
    [SerializeField] private Transform headOfStateContainer;
    
    [Header("Civic Tier Containers")]
    [SerializeField] private Transform aeonicCivicsContainer;
    [SerializeField] private Transform majorCivicsContainer;
    [SerializeField] private Transform minorCivicsContainer;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject seatPositionPrefab;
    [SerializeField] private GameObject leaderSlotPrefab;
    [SerializeField] private GameObject civicDisplayPrefab;
    [SerializeField] private GameObject civicDetailedPrefab;
    
    [Header("Default Assets")]
    [SerializeField] private Sprite defaultIcon; // Default icon for unassigned Head of State
    
    [Header("UI State")]
    [SerializeField] private bool isDisplayVisible = false;
    
    // Internal state
    private int selectedSeatIndex = -1; // -1 = Head of State, 0-5 = Regular seats
    
    // Spawned UI elements
    private List<GameObject> spawnedCivics = new List<GameObject>();
    private List<GameObject> spawnedSeats = new List<GameObject>();
    private List<GameObject> spawnedLeaders = new List<GameObject>();
    private List<GameObject> spawnedCivicDetails = new List<GameObject>();

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private System.Collections.IEnumerator InitializeWhenReady()
    {
        // Wait for GovernmentLogic to be ready
        while (GovernmentLogic.Instance == null)
        {
            yield return null;
        }
        
        // Wait a frame to ensure GovernmentLogic is fully initialized
        yield return null;
        
        InitializeUI();
        SubscribeToEvents();
        
        // Set default icon for Head of State if no legend is assigned
        SetDefaultHeadOfStateIcon();
    }
    
    private void SetDefaultHeadOfStateIcon()
    {
        if (headOfStateContainer == null || defaultIcon == null) return;
        
        // Find the Sprite child GameObject (HeadOfState/Sprite) for the actual portrait
        Transform spriteChild = headOfStateContainer.Find("Sprite");
        var spriteImage = spriteChild != null ? spriteChild.GetComponent<Image>() : null;
        
        if (spriteImage != null)
        {
            spriteImage.sprite = defaultIcon;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void InitializeUI()
    {
        // Set initial state
        if (displayCanvasGroup != null)
        {
            displayCanvasGroup.alpha = 0f;
            displayCanvasGroup.blocksRaycasts = false;
            displayCanvasGroup.interactable = false;
        }
        
        if (civicPoolCanvasGroup != null)
        {
            civicPoolCanvasGroup.alpha = 0f;
            civicPoolCanvasGroup.blocksRaycasts = false;
            civicPoolCanvasGroup.interactable = false;
        }
        
        if (leaderPoolCanvasGroup != null)
        {
            leaderPoolCanvasGroup.alpha = 0f;
            leaderPoolCanvasGroup.blocksRaycasts = false;
            leaderPoolCanvasGroup.interactable = false;
        }
        
        // Refresh displays
        RefreshAllDisplays();
    }

    private void SubscribeToEvents()
    {
        if (GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.OnCouncilCompositionChanged += RefreshAllDisplays;
            GovernmentLogic.Instance.OnCouncilSeatChanged += OnCouncilSeatChangedHandler;
            GovernmentLogic.Instance.OnCivicPoolChanged += RefreshCivicPoolAndDisplays;
            GovernmentLogic.Instance.OnLeaderAssigned += OnLeaderAssignedToSeat;
            GovernmentLogic.Instance.OnLeaderRemoved += OnLeaderRemovedFromSeat;
            GovernmentLogic.Instance.OnLeaderPoolChanged += OnLeaderPoolChanged;
        }
        
        if (CivicManager.Instance != null)
        {
            CivicManager.Instance.OnCivicPoolChanged += RefreshCivicPoolAndDisplays;
            CivicManager.Instance.OnActiveCivicsChanged += RefreshCivicDisplays;
        }
        
        // Add click handler for Head of State container
        if (headOfStateContainer != null)
        {
            var headOfStateButton = headOfStateContainer.GetComponent<Button>();
            if (headOfStateButton != null)
            {
                headOfStateButton.onClick.AddListener(OnHeadOfStateContainerClicked);
            }
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.OnCouncilCompositionChanged -= RefreshAllDisplays;
            GovernmentLogic.Instance.OnCouncilSeatChanged -= OnCouncilSeatChangedHandler;
            GovernmentLogic.Instance.OnCivicPoolChanged -= RefreshCivicPoolAndDisplays;
            GovernmentLogic.Instance.OnLeaderAssigned -= OnLeaderAssignedToSeat;
            GovernmentLogic.Instance.OnLeaderRemoved -= OnLeaderRemovedFromSeat;
            GovernmentLogic.Instance.OnLeaderPoolChanged -= OnLeaderPoolChanged;
        }
        
        if (CivicManager.Instance != null)
        {
            CivicManager.Instance.OnCivicPoolChanged -= RefreshCivicPoolAndDisplays;
            CivicManager.Instance.OnActiveCivicsChanged -= RefreshCivicDisplays;
        }
        
        // Remove click handler for Head of State container
        if (headOfStateContainer != null)
        {
            var headOfStateButton = headOfStateContainer.GetComponent<Button>();
            if (headOfStateButton != null)
            {
                headOfStateButton.onClick.RemoveListener(OnHeadOfStateContainerClicked);
            }
        }
    }

    public void RefreshAllDisplays()
    {
        RefreshCivicDisplays();
        RefreshCouncilSeats();
        // Head of State is now persistent and gets updated directly, no need to refresh
    }

    private void RefreshCivicDisplays()
    {
        ClearSpawnedCivics();
        
        if (CivicManager.Instance != null)
        {
            var activeCivics = CivicManager.Instance.GetAllActiveCivics();
            
            foreach (var civic in activeCivics)
            {
                SpawnCivicDisplay(civic);
            }
        }
    }

    /// <summary>
    /// Spawn a civic display for ACTIVE civics (goes in tier-specific containers)
    /// These show currently equipped/active civics in the government
    /// </summary>
    private void SpawnCivicDisplay(CivicData civic)
    {
        if (civicDisplayPrefab == null) return;
        
        // Determine the appropriate container based on civic tier
        Transform targetContainer = GetCivicContainer(civic.tier);
        if (targetContainer == null) return;
        
        var civicObj = Instantiate(civicDisplayPrefab, targetContainer);
        var civicDisplay = civicObj.GetComponent<CivicDisplay>();
        
        if (civicDisplay != null)
        {
            civicDisplay.Initialize(civic, civic.tier);
        }
        
        spawnedCivics.Add(civicObj);
        
        GameLoggingSystem.Instance.LogEvent($"Spawned Civic '{civic.civicName}' (Tier: {civic.tier}) in {targetContainer.name}", "GovernmentTab");
    }

    private void RefreshCouncilSeats()
    {
        if (councilSeatsContainer == null) return;
        
        ClearSpawnedSeats();
        
        if (GovernmentLogic.Instance != null)
        {
            var activeSeats = GovernmentLogic.Instance.GetActiveRegularSeats();
            
            for (int i = 0; i < activeSeats.Length; i++)
            {
                var seat = activeSeats[i];
                if (seat != null)
                {
                    // Use the actual seat index from the seat object, not the loop index
                    SpawnSeatDisplay(seat, seat.seatIndex);
                }
            }
        }
    }

    private void SpawnSeatDisplay(CouncilSeat seat, int seatIndex)
    {
        if (seatPositionPrefab == null || councilSeatsContainer == null) return;
        
        var seatObj = Instantiate(seatPositionPrefab, councilSeatsContainer);
        var seatDisplay = seatObj.GetComponent<SeatPositionDisplay>();
        
        if (seatDisplay != null)
        {
            seatDisplay.Initialize(seat, seatIndex);
            seatDisplay.OnSeatClicked += OnSeatClicked;
        }
        
        spawnedSeats.Add(seatObj);
    }

    private void RefreshHeadOfState()
    {
        if (headOfStateContainer == null) return;
        
        // Clear existing head of state display
        foreach (Transform child in headOfStateContainer)
        {
            Destroy(child.gameObject);
        }
        
        if (GovernmentLogic.Instance != null)
        {
            var headOfState = GovernmentLogic.Instance.GetCouncilSeat(-1);
            if (headOfState != null)
            {
                UpdateHeadOfStateDisplay(headOfState);
            }
        }
    }

    private void UpdateHeadOfStateDisplay(CouncilSeat headOfState)
    {
        if (headOfStateContainer == null) return;
        
        // Find existing UI elements in the Head of State container (don't spawn new ones)
        var titleText = headOfStateContainer.GetComponentInChildren<TextMeshProUGUI>();
        
        // Find the Sprite child GameObject (HeadOfState/Sprite) for the actual portrait
        Transform spriteChild = headOfStateContainer.Find("Sprite");
        var spriteImage = spriteChild != null ? spriteChild.GetComponent<Image>() : null;
        
        if (headOfState.assignedLegend != null)
        {
            var legend = headOfState.assignedLegend;
            
            // Update title text - just show the legend name
            if (titleText != null)
            {
                titleText.text = legend.legendName;
            }
            
            // Update sprite image (HeadOfState/Sprite)
            if (spriteImage != null && legend.portrait != null)
            {
                spriteImage.sprite = legend.portrait;
            }
        }
        else
        {
            // No legend assigned - show default state
            if (titleText != null)
            {
                titleText.text = "Unassigned";
            }
            
            if (spriteImage != null && defaultIcon != null)
            {
                spriteImage.sprite = defaultIcon;
            }
        }
    }

    private void OnSeatClicked(int seatIndex)
    {
        selectedSeatIndex = seatIndex;
        
        // Open both pools simultaneously for intuitive selection
        OpenLeaderPool();
        OpenCivicPool();
    }

    private void OnHeadOfStateContainerClicked()
    {
        selectedSeatIndex = -1; // Head of State
        
        // Head of State can only have legends assigned, not be replaced by civics
        OpenLeaderPool();
    }

    private void OpenLeaderPool()
    {
        if (leaderPoolCanvasGroup == null) return;
        
        leaderPoolCanvasGroup.alpha = 1f;
        leaderPoolCanvasGroup.blocksRaycasts = true;
        leaderPoolCanvasGroup.interactable = true;
        
        RefreshLeaderPool();
    }

    private void CloseLeaderPool()
    {
        if (leaderPoolCanvasGroup == null) return;
        
        leaderPoolCanvasGroup.alpha = 0f;
        leaderPoolCanvasGroup.blocksRaycasts = false;
        leaderPoolCanvasGroup.interactable = false;
    }

    private void RefreshLeaderPool()
    {
        if (leaderPoolContent == null) return;
        
        ClearSpawnedLeaders();
        
        if (GovernmentLogic.Instance != null)
        {
            List<LegendData> availableLegends;
            
            if (selectedSeatIndex >= 0)
            {
                // For regular seats, get legends available for this specific seat
                availableLegends = GovernmentLogic.Instance.GetAvailableLegendsForSeat(selectedSeatIndex);
            }
            else if (selectedSeatIndex == -1)
            {
                // For Head of State, get legends available for Head of State
                availableLegends = GovernmentLogic.Instance.GetAvailableLegendsForSeat(-1);
            }
            else
            {
                // No seat selected, show all available legends
                availableLegends = GovernmentLogic.Instance.GetAllAvailableLegends();
            }
            
            // Separate legends into unequipped (priority) and equipped (for swapping)
            var unequippedLegends = new List<LegendData>();
            var equippedLegends = new List<LegendData>();
            
            foreach (var legend in availableLegends)
            {
                var currentSeat = GovernmentLogic.Instance.GetSeatWithLegend(legend.legendName);
                if (currentSeat == null)
                {
                    // Legend is not equipped anywhere - highest priority
                    unequippedLegends.Add(legend);
                }
                else
                {
                    // Legend is equipped somewhere - available for swapping
                    equippedLegends.Add(legend);
                }
            }
            
            // Log the organization for debugging
            GameLoggingSystem.Instance.LogEvent($"Organizing {availableLegends.Count} available legends for seat {selectedSeatIndex}: {unequippedLegends.Count} unequipped, {equippedLegends.Count} equipped", "GovernmentTab");
            
            // Spawn unequipped legends first (priority)
            foreach (var legend in unequippedLegends)
            {
                SpawnLeaderSlot(legend);
            }
            
            // Spawn equipped legends second (for swapping)
            foreach (var legend in equippedLegends)
            {
                SpawnLeaderSlot(legend);
            }
        }
    }

    private void SpawnLeaderSlot(LegendData legend)
    {
        if (leaderSlotPrefab == null || leaderPoolContent == null) return;
        
        var leaderObj = Instantiate(leaderSlotPrefab, leaderPoolContent);
        var leaderSlot = leaderObj.GetComponent<LeaderSlotDisplay>();
        
        if (leaderSlot != null)
        {
            leaderSlot.Initialize(legend, selectedSeatIndex);
            leaderSlot.OnLeaderSelected += OnLeaderSelected;
        }
        
        spawnedLeaders.Add(leaderObj);
    }

    private void OnLeaderSelected(LegendData legend)
    {
        if (GovernmentLogic.Instance != null)
        {
            bool success = GovernmentLogic.Instance.AssignLegendToSeat(legend, selectedSeatIndex);
            
            if (success)
            {
                // Close both pools after successful assignment
                CloseBothPools();
                RefreshAllDisplays();
            }
        }
    }

    private void OpenCivicPool()
    {
        if (civicPoolCanvasGroup == null) return;
        
        civicPoolCanvasGroup.alpha = 1f;
        civicPoolCanvasGroup.blocksRaycasts = true;
        civicPoolCanvasGroup.interactable = true;
        
        RefreshCivicPool();
    }

    private void CloseCivicPool()
    {
        if (civicPoolCanvasGroup == null) return;
        
        civicPoolCanvasGroup.alpha = 0f;
        civicPoolCanvasGroup.blocksRaycasts = false;
        civicPoolCanvasGroup.interactable = false;
    }

    private void RefreshCivicPool()
    {
        if (civicPoolContent == null) return;
        
        ClearSpawnedCivicDetails();
        
        if (GovernmentLogic.Instance != null)
        {
            var availableSeats = GovernmentLogic.Instance.GetAvailableSeatTitles();
            
            foreach (var seatTitle in availableSeats)
            {
                SpawnCivicDetailed(seatTitle);
            }
        }
    }
    
    /// <summary>
    /// Refresh both civic pool (CivicDetailed objects) and civic displays (active civics)
    /// Called when civic pool changes to ensure both are updated
    /// </summary>
    private void RefreshCivicPoolAndDisplays()
    {
        RefreshCivicPool();
        RefreshCivicDisplays();
    }
    
    /// <summary>
    /// Get the appropriate container for a civic based on its tier
    /// </summary>
    private Transform GetCivicContainer(CivicTier tier)
    {
        Transform targetContainer = null;
        
        switch (tier)
        {
            case CivicTier.Aeonic:
                targetContainer = aeonicCivicsContainer;
                break;
            case CivicTier.Major:
                targetContainer = majorCivicsContainer;
                break;
            case CivicTier.Minor:
                targetContainer = minorCivicsContainer;
                break;
            default:
                targetContainer = civicPoolContent;
                break;
        }
        
        // Debug logging to help troubleshoot
        if (targetContainer == null)
        {
            Debug.LogWarning($"[GovernmentTab] No container found for {tier} tier civics. Falling back to civicPoolContent.");
            targetContainer = civicPoolContent;
        }
        else
        {
            GameLoggingSystem.Instance.LogEvent($"Using {tier} container: {targetContainer.name}", "GovernmentTab");
        }
        
        return targetContainer;
    }

    /// <summary>
    /// Spawn a CivicDetailed display for the CIVIC POOL (always goes in civicPoolContent)
    /// These are for seat replacement options, not active civic displays
    /// </summary>
    private void SpawnCivicDetailed(string seatTitle)
    {
        if (civicDetailedPrefab == null) return;
        
        // CivicDetailed objects ALWAYS go in the civicPoolContent (not tier-specific containers)
        // These are for the civic pool display, not active civic displays
        Transform targetContainer = civicPoolContent;
        
        if (targetContainer == null) return;
        
        var civicObj = Instantiate(civicDetailedPrefab, targetContainer);
        var civicDetailed = civicObj.GetComponent<CivicDetailedDisplay>();
        
        if (civicDetailed != null)
        {
            civicDetailed.Initialize(seatTitle);
            civicDetailed.OnCivicClicked += OnCivicClicked;
        }
        
        spawnedCivicDetails.Add(civicObj);
    }

    private void OnCivicClicked(string seatTitle)
    {
        // Head of State cannot be replaced with civics
        if (selectedSeatIndex == -1)
        {
            Debug.LogWarning("[GovernmentTab] Cannot replace Head of State with civics");
            return;
        }
        
        if (GovernmentLogic.Instance != null)
        {
            // Safety check: Ensure the seat is still available before attempting replacement
            var availableSeats = GovernmentLogic.Instance.GetAvailableSeatTitles();
            if (!availableSeats.Contains(seatTitle))
            {
                Debug.LogWarning($"[GovernmentTab] Seat '{seatTitle}' is no longer available. Refreshing civic pool to remove orphaned UI objects.");
                
                // Refresh the civic pool to clean up orphaned CivicDetailed objects
                RefreshCivicPool();
                return;
            }
            
            bool success = GovernmentLogic.Instance.ReplaceSeatWithAvailable(selectedSeatIndex, seatTitle);
            
            if (success)
            {
                // Close both pools after successful replacement
                CloseBothPools();
                RefreshAllDisplays();
            }
        }
    }

    private void CloseBothPools()
    {
        CloseLeaderPool();
        CloseCivicPool();
    }

    private void ClearSpawnedCivics()
    {
        foreach (var civic in spawnedCivics)
        {
            if (civic != null)
            {
                Destroy(civic);
            }
        }
        spawnedCivics.Clear();
    }

    private void ClearSpawnedSeats()
    {
        foreach (var seat in spawnedSeats)
        {
            if (seat != null)
            {
                Destroy(seat);
            }
        }
        spawnedSeats.Clear();
    }

    private void ClearSpawnedLeaders()
    {
        foreach (var leader in spawnedLeaders)
        {
            if (leader != null)
            {
                Destroy(leader);
            }
        }
        spawnedLeaders.Clear();
    }

    private void ClearSpawnedCivicDetails()
    {
        foreach (var civicDetail in spawnedCivicDetails)
        {
            if (civicDetail != null)
            {
                Destroy(civicDetail);
            }
        }
        spawnedCivicDetails.Clear();
    }

    public void ToggleDisplay()
    {
        isDisplayVisible = !isDisplayVisible;
        
        if (displayCanvasGroup != null)
        {
            displayCanvasGroup.alpha = isDisplayVisible ? 1f : 0f;
            displayCanvasGroup.blocksRaycasts = isDisplayVisible;
            displayCanvasGroup.interactable = isDisplayVisible;
        }
        
        if (!isDisplayVisible)
        {
            // Close pools when hiding the tab
            CloseBothPools();
        }
    }

    private void OnLeaderAssignedToSeat(CouncilSeat seat, LegendData legend)
    {
        // Update Head of State display if this is the Head of State
        if (seat.seatIndex == -1)
        {
            UpdateHeadOfStateDisplay(seat);
        }
    }

    private void OnLeaderRemovedFromSeat(CouncilSeat seat)
    {
        // Update Head of State display if this is the Head of State
        if (seat.seatIndex == -1)
        {
            UpdateHeadOfStateDisplay(seat);
        }
    }
    
    private void OnLeaderPoolChanged()
    {
        // Refresh seat visuals to reflect cooldown/activation changes at each seventh or change
        RefreshCouncilSeats();
        
        // Refresh leader pool if it's currently open to reflect cooldown changes
        if (leaderPoolCanvasGroup != null && leaderPoolCanvasGroup.alpha > 0f)
        {
            RefreshLeaderPool();
        }
    }

    private void OnCouncilSeatChangedHandler(CouncilSeat seat)
    {
        // Seat composition changed at a position – refresh seat visuals
        RefreshCouncilSeats();
        
        // If Head of State title/sprite needs updating, handle it here too
        if (seat != null && seat.seatIndex == -1)
        {
            UpdateHeadOfStateDisplay(seat);
        }
    }
} 