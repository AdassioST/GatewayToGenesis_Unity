using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Configuration template for default council seats
/// </summary>
[System.Serializable]
public class DefaultSeatTemplate
{
    public string title;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public LegendClass[] allowedClasses;
    public DefaultSeatBonus[] bonuses;
}

/// <summary>
/// Represents a legend bonus that's waiting to be applied to a resource slot
/// </summary>
[System.Serializable]
public struct PendingLegendBonus
{
    public string legendSource;
    public GameEffectType bonusType;
    public string targetStat;
    public float modifierValue;
    public ModifierType modifierType;
    public ScopeType scope;
    public bool isPositive;
    
    public PendingLegendBonus(string source, GameEffectType type, string target, float value, ModifierType modType, ScopeType scopeType, bool positive)
    {
        legendSource = source;
        bonusType = type;
        targetStat = target;
        modifierValue = value;
        modifierType = modType;
        scope = scopeType;
        isPositive = positive;
    }
}

/// <summary>
/// Configuration for default seat bonuses
/// </summary>
[System.Serializable]
public class DefaultSeatBonus
{
    public SeatBonusType bonusType;
    [Tooltip("Target stat for pillar/substat/derived bonuses (e.g., 'waltz', 'legendEffectiveness')")]
    public string targetStat;
    public float modifierValue;
    public ModifierType modifierType;
    public bool requiresLegend = true;
}

/// <summary>
/// Manages government types and civic systems based on political compass coordinates
/// Uses a 7x7 grid system based on net differences between pillar stats
/// 
/// Coordinate System:
/// - Horizontal: Waltz (left, negative) vs Regalia (right, positive)
/// - Vertical: Chorus (top, positive) vs Aureus (bottom, negative)
/// 
/// This makes the chart more intuitive: left = harmony, right = authority
/// 
/// Government Classification:
/// - Fanatic Radical: Both coordinates at ±3 (Fanatic)
/// - Radical: One coordinate at ±2 (Pillar) and other at ±3 (Fanatic)
/// - True Pillar: Single coordinate at ±3 (Fanatic), other at 0
/// - Pillar Leaning: Single coordinate at ±2 (Pillar), other at 0
/// - Pillar Centrist: Single coordinate at ±1 (Leaning), other at 0
/// - Regular: Combinations of Leaning (±1) and Pillar (±2) coordinates
/// - Centrist: Combinations of Leaning (±1) coordinates
/// </summary>
public class GovernmentLogic : MonoBehaviour
{
    public static GovernmentLogic Instance { get; private set; }

    [Header("Threshold Configuration")]
    [SerializeField] private int centristThreshold = 25; // Net difference 0-24 = Centrist
    [SerializeField] private int leaningThreshold = 50; // Net difference 25-49 = Leaning
    [SerializeField] private int pillarThreshold = 75; // Net difference 50-74 = Pillar
    [SerializeField] private int fanaticThreshold = 75; // Net difference 75+ = Fanatic
    
    [Header("Head of State Configuration")]
    [SerializeField] private float headOfStateMultiplier = 2.0f; // Multiplier for legend bonuses when assigned to Head of State
    
    [Header("Head of State UI References")]
    [SerializeField] private Transform headOfStateContainer; // Reference to the HeadOfState UI container
    [SerializeField] private UnityEngine.UI.Image headOfStateActiveIndicator; // Optional direct reference (fallback to auto-detection)
    [SerializeField] private UnityEngine.UI.Image headOfStateCooldownIndicator; // Optional direct reference (fallback to auto-detection)
    
    [Header("Head of State UI Colors")]
    [SerializeField] private Color activeColor = Color.green; // Green when active
    [SerializeField] private Color inactiveColor = Color.red; // Red when inactive
    [SerializeField] private Color readyColor = Color.cyan; // Cyan when ready
    [SerializeField] private Color cooldownColor = new Color(0f, 0f, 0.5f, 1f); // Deep blue when on cooldown
    
    [Header("Council Change Cooldowns")]
    [SerializeField] private int councilSeatCooldownSevenths = 1; // Regular seats can be changed every 1 seventh
    [SerializeField] private int headOfStateCooldownSevenths = 3; // Head of State can be changed every 3 sevenths
    
    // Pending legend bonuses for resources that aren't instantiated yet
    private Dictionary<string, List<PendingLegendBonus>> pendingResourceBonuses = new Dictionary<string, List<PendingLegendBonus>>();
    private Dictionary<string, List<PendingLegendBonus>> pendingSectionBonuses = new Dictionary<string, List<PendingLegendBonus>>();
    private List<PendingLegendBonus> pendingGlobalBonuses = new List<PendingLegendBonus>();
    

    
    // Flag to prevent infinite recursion during Head of State recalculation
    private bool isRecalculatingHeadOfState = false;
    
    // Track if Head of State recalculation has already happened this seventh
    private bool hasRecalculatedHeadOfStateThisSeventh = false;
    private int lastHeadOfStateRecalculationSeventh = -1;
    

    
    // Track last known Legend Effectiveness to detect actual changes (event-driven)
    private float lastKnownLegendEffectiveness = -1f;
    
    // Pipeline override to ensure deterministic Legend Effectiveness usage across phases
    private bool useLegendEffectivenessOverride = false;
    private float legendEffectivenessOverride = 0f;
    
    // Snapshot used during centralized processing to ensure deterministic LE-enhanced bonuses
    private float _currentLegendEffectivenessSnapshot = -1f;
    
    // Council seat change cooldown tracking - HARD RESET SYSTEM
    // Cooldowns can only be decremented and are always set to base values
    private Dictionary<int, int> seatCooldownRemaining = new Dictionary<int, int>(); // seatIndex -> sevenths remaining
    private int headOfStateCooldownRemaining = 0; // sevenths remaining for Head of State
    


    // Political compass coordinates (-3 to +3 for each axis)
    // Note: ±1 = Leaning, ±2 = Pillar, ±3 = Fanatic
    // True Pillar governments are at ±3 (Fanatic), not ±2
    // Waltz is negative (left side), Regalia is positive (right side) for intuitive chart reading
    private int waltzRegaliaCoordinate = 0; // Horizontal axis: Waltz (left, negative) vs Regalia (right, positive)
    private int chorusAureusCoordinate = 0; // Vertical axis: Chorus (top, positive) vs Aureus (bottom, negative)
    
    // Council system
    private List<CouncilSeat> councilSeats = new List<CouncilSeat>();

    // Council seat processing system
    private Dictionary<string, CouncilSeat> civicCouncilSeats = new Dictionary<string, CouncilSeat>(); // Civics that grant council positions
    private bool isProcessingSeatBonuses = false; // Prevent infinite loops during bonus processing

    // ===== FLEXIBLE SEAT REPLACEMENT SYSTEM =====
    
    // Pool of all available seats (default + civic) that can be used in the 6 regular positions
    private List<CouncilSeat> availableSeatPool = new List<CouncilSeat>();
    
    // Currently active seats in the 6 regular positions (0-5)
    private CouncilSeat[] activeRegularSeats = new CouncilSeat[6];
    
    // Default seats that are always available as fallback options
    private List<CouncilSeat> defaultSeatTemplates = new List<CouncilSeat>();
    
    // Track how many seats are currently unlocked (starts at 3, max 6)
    [SerializeField] private int unlockedSeatCount = 3;
    
    [Header("Default Seat Configuration")]
    [SerializeField] private DefaultSeatTemplate[] defaultSeatConfigs = new DefaultSeatTemplate[6];

    // Government type classification
    private GovernmentType currentGovernmentType = GovernmentType.TrueCentrist;
    private string currentGovernmentName = "True Centrist";
    private string currentGovernmentDescription = "A perfectly balanced government with no ideological leanings";

    // Events for UI updates
    public System.Action<GovernmentType, string, string> OnGovernmentChanged;
    public System.Action<int, int> OnCoordinatesChanged; // (waltzRegalia, chorusAureus)
    
    // Civic system integration
    public System.Action<GovernmentType> OnGovernmentTypeChangedForCivics;
    
    // Council system integration
    public System.Action<CouncilSeat> OnCouncilSeatUnlocked;
    public System.Action<CouncilSeat> OnCouncilSeatChanged;
    
    // UI system integration
    public System.Action<CouncilSeat, LegendData> OnLeaderAssigned; // (seat, legend)
    public System.Action<CouncilSeat> OnLeaderRemoved; // (seat)
    public System.Action OnCouncilCompositionChanged; // When any seat changes
    public System.Action OnCivicPoolChanged; // When civic availability changes
    public System.Action OnLeaderPoolChanged; // When leader pool availability changes (cooldown updates)

    /// <summary>
    /// Government type classification based on political compass coordinates
    /// </summary>


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GovernmentLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Validate all legends and civics at startup to catch configuration errors
        ValidateAllDataAtStartup();
        
        // Initialize council system
        InitializeCouncilSystem();
        
        // Initialize Head of State UI indicators with correct initial state
        UpdateHeadOfStateStatusIndicators();
        
        // Subscribe to StatManager changes to recalculate government type
        if (StatManager.Instance != null)
        {
            // Subscribe to derived stat changes to monitor Legend Effectiveness
            StatManager.Instance.OnDerivedChanged += OnDerivedStatChanged;
            
            // Initial calculation
            CalculateGovernmentCoordinates();
            DetermineGovernmentType();
        }
        else
        {
            // Attempt delayed subscription
            StartCoroutine(SubscribeToStatManagerWhenReady());
        }
        
        // Subscribe to TimeSystemLogic for council seat processing
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange += ProcessSeventhChange;
        }
        else
        {
            // Attempt delayed subscription
            StartCoroutine(SubscribeToTimeSystemWhenReady());
        }
        
        // Initialize Legend Effectiveness tracking baseline
        InitializeLegendEffectivenessTracking();
    }

    private System.Collections.IEnumerator SubscribeToStatManagerWhenReady()
    {
        while (StatManager.Instance == null)
        {
            yield return null;
        }
        
        // Subscribe to derived stat changes to monitor Legend Effectiveness
        StatManager.Instance.OnDerivedChanged += OnDerivedStatChanged;
        
        // Initial calculation
        CalculateGovernmentCoordinates();
        DetermineGovernmentType();
        
        // Initialize Legend Effectiveness tracking baseline
        InitializeLegendEffectivenessTracking();
        
        // Initialize Head of State UI indicators with correct initial state (delayed)
        UpdateHeadOfStateStatusIndicators();

    }
    
    private System.Collections.IEnumerator SubscribeToTimeSystemWhenReady()
    {
        while (TimeSystemLogic.Instance == null)
        {
            yield return null;
        }
        
        TimeSystemLogic.Instance.OnSeventhChange += ProcessSeventhChange;
        
        // Initialize baseline values for Authority and Legend Effectiveness tracking

    }

    private void Update()
    {
        // Debug input to recalculate government type
        if (Input.GetKeyDown(KeyCode.G))
        {
            CalculateGovernmentCoordinates();
            DetermineGovernmentType();
            LogGovernmentInfo();
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (StatManager.Instance != null)
        {
            StatManager.Instance.OnDerivedChanged -= OnDerivedStatChanged;
        }
        
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange -= ProcessSeventhChange;
        }
    }
    
    /// <summary>
    /// Handle derived stat changes to recalculate legend bonuses when Legend Effectiveness changes
    /// Uses efficient event-driven approach with precise change detection and seventh-based throttling
    /// </summary>
    private void OnDerivedStatChanged(string statName, float newValue)
    {
        // Only recalculate when Legend Effectiveness changes, as it affects bonus calculations
        if (statName.ToLower() == "legendeffectiveness")
        {
            // Check if this is actually a meaningful change (avoid spam from tiny fluctuations)
            bool isSignificantChange = Mathf.Abs(newValue - lastKnownLegendEffectiveness) > 0.001f;
            
            if (isSignificantChange)
            {
                GameLoggingSystem.Instance.LogEvent($"LE changed: {lastKnownLegendEffectiveness:F2} → {newValue:F2}", "GovernmentLogic");
                
                // Update stored value
                lastKnownLegendEffectiveness = newValue;
                
                // Use seventh-based throttling to prevent multiple recalculations per seventh
                if (ShouldTriggerHeadOfStateRecalculation())
                {
                    RecalculateFullCouncil("Legend Effectiveness changed via event");
                }
                else
                {
                    // skip log
                }
            }
            else
            {
                // too small; skip log
            }
        }
    }
    
    /// <summary>
    /// Initialize the baseline Legend Effectiveness value for change detection
    /// </summary>
    private void InitializeLegendEffectivenessTracking()
    {
        if (StatManager.Instance == null) return;
        
        lastKnownLegendEffectiveness = StatManager.Instance.GetDerivedValue("legendEffectiveness");
        GameLoggingSystem.Instance.LogEvent($"Initialized Legend Effectiveness tracking baseline: {lastKnownLegendEffectiveness:F3}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Check if a council seat change is allowed based on cooldown
    /// HARD RESET SYSTEM: Cooldowns can only be decremented and are always set to base values
    /// </summary>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    /// <returns>True if change is allowed, false if still on cooldown</returns>
    public bool CanChangeSeat(int seatIndex)
    {
        if (TimeSystemLogic.Instance == null) return true; // Allow if no time system
        
        // CRITICAL: If seat is unassigned, always allow changes (no cooldown on empty seats)
        var seat = GetCouncilSeat(seatIndex);
        if (seat == null || seat.assignedLegend == null)
        {
            string seatName = (seatIndex == -1) ? "Head of State" : $"Seat {seatIndex}";
            GameLoggingSystem.Instance.LogEvent($"{seatName} is unassigned - ignoring cooldown, change allowed", "GovernmentLogic");
            return true; // Unassigned seats have no cooldown
        }
        
        if (seatIndex == -1) // Head of State
        {
            return headOfStateCooldownRemaining <= 0;
        }
        else // Regular council seat
        {
            if (seatCooldownRemaining.TryGetValue(seatIndex, out int remaining))
            {
                return remaining <= 0;
            }
            return true; // Never changed before, allow
        }
    }
    
    /// <summary>
    /// Get remaining cooldown sevenths for a seat
    /// HARD RESET SYSTEM: Returns the actual remaining cooldown value
    /// </summary>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    /// <returns>Number of sevenths remaining on cooldown, 0 if ready</returns>
    public int GetSeatCooldownRemaining(int seatIndex)
    {
        if (TimeSystemLogic.Instance == null) return 0;
        
        // CRITICAL: If seat is unassigned, no cooldown applies
        var seat = GetCouncilSeat(seatIndex);
        if (seat == null || seat.assignedLegend == null)
        {
            return 0; // Unassigned seats have no cooldown
        }
        
        if (seatIndex == -1) // Head of State
        {
            return Mathf.Max(0, headOfStateCooldownRemaining);
        }
        else // Regular council seat
        {
            if (seatCooldownRemaining.TryGetValue(seatIndex, out int remaining))
            {
                return Mathf.Max(0, remaining);
            }
            return 0; // Never changed before, ready
        }
    }
    
    /// <summary>
    /// Record that a seat was changed and set its cooldown to base value
    /// HARD RESET SYSTEM: Always sets cooldown to base value, never increases it
    /// </summary>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    private void RecordSeatChange(int seatIndex)
    {
        if (TimeSystemLogic.Instance == null) return;
        
        if (seatIndex == -1) // Head of State
        {
            headOfStateCooldownRemaining = headOfStateCooldownSevenths; // HARD RESET to base value
            GameLoggingSystem.Instance.LogEvent($"HARD RESET: Head of State cooldown set to {headOfStateCooldownSevenths} sevenths", "GovernmentLogic");
        }
        else // Regular council seat
        {
            seatCooldownRemaining[seatIndex] = councilSeatCooldownSevenths; // HARD RESET to base value
            GameLoggingSystem.Instance.LogEvent($"HARD RESET: Seat {seatIndex} cooldown set to {councilSeatCooldownSevenths} sevenths", "GovernmentLogic");
        }
    }
    
    /// <summary>
    /// Record cooldowns for both seats involved in a perfect swap
    /// HARD RESET SYSTEM: Both seats get their cooldowns set to base values
    /// </summary>
    /// <param name="seatIndex1">First seat index involved in the swap</param>
    /// <param name="seatIndex2">Second seat index involved in the swap</param>
    private void RecordPerfectSwapCooldowns(int seatIndex1, int seatIndex2)
    {
        if (TimeSystemLogic.Instance == null) return;
        
        // Record cooldown for first seat - HARD RESET to base value
        if (seatIndex1 == -1) // Head of State
        {
            headOfStateCooldownRemaining = headOfStateCooldownSevenths;
            GameLoggingSystem.Instance.LogEvent($"Perfect swap: HARD RESET Head of State cooldown to {headOfStateCooldownSevenths} sevenths", "GovernmentLogic");
        }
        else // Regular council seat
        {
            seatCooldownRemaining[seatIndex1] = councilSeatCooldownSevenths;
            GameLoggingSystem.Instance.LogEvent($"Perfect swap: HARD RESET seat {seatIndex1} cooldown to {councilSeatCooldownSevenths} sevenths", "GovernmentLogic");
        }
        
        // Record cooldown for second seat - HARD RESET to base value
        if (seatIndex2 == -1) // Head of State
        {
            headOfStateCooldownRemaining = headOfStateCooldownSevenths;
            GameLoggingSystem.Instance.LogEvent($"Perfect swap: HARD RESET Head of State cooldown to {headOfStateCooldownSevenths} sevenths", "GovernmentLogic");
        }
        else // Regular council seat
        {
            seatCooldownRemaining[seatIndex2] = councilSeatCooldownSevenths;
            GameLoggingSystem.Instance.LogEvent($"Perfect swap: HARD RESET seat {seatIndex2} cooldown to {councilSeatCooldownSevenths} sevenths", "GovernmentLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent($"Perfect swap cooldowns HARD RESET for seats {seatIndex1} and {seatIndex2}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Request a seat change with cooldown validation
    /// This should be called by any system that wants to change council seats
    /// </summary>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    /// <param name="requestingSystem">Name of the system requesting the change (for logging)</param>
    /// <returns>True if change is allowed, false if on cooldown</returns>
    public bool RequestSeatChange(int seatIndex, string requestingSystem = "Unknown")
    {
        if (!CanChangeSeat(seatIndex))
        {
            int remainingCooldown = GetSeatCooldownRemaining(seatIndex);
            GameLoggingSystem.Instance.LogEvent($"Seat change denied by {requestingSystem} - Seat {seatIndex} on cooldown for {remainingCooldown} more sevenths", "GovernmentLogic");
            return false;
        }
        
        // Record the change request
        RecordSeatChange(seatIndex);
        
        GameLoggingSystem.Instance.LogEvent($"Seat change approved for {requestingSystem} - Seat {seatIndex} changed", "GovernmentLogic");
        
        // Trigger leader pool update to reflect cooldown changes
        OnLeaderPoolChanged?.Invoke();
        
        return true;
    }
    
    /// <summary>
    /// Get cooldown information for all seats (for UI display)
    /// </summary>
    /// <returns>Dictionary of seat index to remaining cooldown sevenths</returns>
    public Dictionary<int, int> GetAllSeatCooldowns()
    {
        var cooldowns = new Dictionary<int, int>();
        
        // Head of State
        cooldowns[-1] = GetSeatCooldownRemaining(-1);
        
        // Regular seats (0-5)
        for (int i = 0; i < 6; i++)
        {
            cooldowns[i] = GetSeatCooldownRemaining(i);
        }
        
        return cooldowns;
    }
    
    /// <summary>
    /// Force reset all seat cooldowns to their base values
    /// HARD RESET SYSTEM: Emergency function to clear all cooldowns
    /// </summary>
    public void ForceResetAllCooldowns()
    {
        headOfStateCooldownRemaining = 0;
        seatCooldownRemaining.Clear();
        
        GameLoggingSystem.Instance.LogEvent("FORCE RESET: All seat cooldowns cleared - all seats ready for changes", "GovernmentLogic");
        
        // Trigger leader pool update to reflect cooldown changes
        OnLeaderPoolChanged?.Invoke();
        
        // Update Head of State status indicators
        UpdateHeadOfStateStatusIndicators();
    }
    
    /// <summary>
    /// Update Head of State status indicators (Active and Cooldown)
    /// This method is called to synchronize the UI with the current Head of State state
    /// </summary>
    public void UpdateHeadOfStateStatusIndicators()
    {
        var headOfState = GetCouncilSeat(-1); // Head of State has index -1
        if (headOfState == null) return;
        
        bool isActive = headOfState.IsActive();
        bool onCooldown = !CanChangeSeat(-1);
        GameLoggingSystem.Instance.LogEvent($"Updating Head of State UI - Active: {isActive}, OnCooldown: {onCooldown}, HasLegend: {headOfState.assignedLegend != null}, SeventhsUntilActive: {headOfState.seventhsUntilActive}", "GovernmentLogic");
        
        // Get Active and Cooldown indicator components (with fallback logic)
        UnityEngine.UI.Image activeImage = GetHeadOfStateActiveIndicator();
        UnityEngine.UI.Image cooldownImage = GetHeadOfStateCooldownIndicator();
        
        // Update Active indicator (red = inactive, green = active)
        if (activeImage != null)
        {
            activeImage.color = isActive ? activeColor : inactiveColor;
        }
        
        // Update Cooldown indicator (deep blue = on cooldown, cyan = ready)
        if (cooldownImage != null)
        {
            cooldownImage.color = onCooldown ? cooldownColor : readyColor;
        }
    }
    
    /// <summary>
    /// Get the Head of State Active indicator Image component (with fallback logic)
    /// </summary>
    private UnityEngine.UI.Image GetHeadOfStateActiveIndicator()
    {
        // Try direct reference first
        if (headOfStateActiveIndicator != null)
        {
            return headOfStateActiveIndicator;
        }
        
        // Fallback to auto-detection via container
        if (headOfStateContainer != null)
        {
            Transform activeIndicator = headOfStateContainer.Find("Active");
            if (activeIndicator != null)
            {
                return activeIndicator.GetComponent<UnityEngine.UI.Image>();
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the Head of State Cooldown indicator Image component (with fallback logic)
    /// </summary>
    private UnityEngine.UI.Image GetHeadOfStateCooldownIndicator()
    {
        // Try direct reference first
        if (headOfStateCooldownIndicator != null)
        {
            return headOfStateCooldownIndicator;
        }
        
        // Fallback to auto-detection via container
        if (headOfStateContainer != null)
        {
            Transform cooldownIndicator = headOfStateContainer.Find("Cooldown");
            if (cooldownIndicator != null)
            {
                return cooldownIndicator.GetComponent<UnityEngine.UI.Image>();
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if we should trigger Head of State recalculation this seventh
    /// </summary>
    private bool ShouldTriggerHeadOfStateRecalculation(bool force = false)
    {
        if (force) return true;
        if (TimeSystemLogic.Instance == null) return true; // Allow if no time system
        
        int currentSeventh = TimeSystemLogic.Instance.CurrentSeventh;
        
        // If it's a new seventh, reset the flag
        if (currentSeventh != lastHeadOfStateRecalculationSeventh)
        {
            hasRecalculatedHeadOfStateThisSeventh = false;
            lastHeadOfStateRecalculationSeventh = currentSeventh;
        }
        
        return !hasRecalculatedHeadOfStateThisSeventh;
    }
    

    

    

    

    
    /// <summary>
    /// Recalculate the entire council when Head of State changes or Legend Effectiveness changes
    /// This ensures all legend bonuses are updated with correct Head of State multipliers and effectiveness values
    /// </summary>
    private void RecalculateFullCouncil(string reason, bool force = false)
    {
        if (isProcessingSeatBonuses || isRecalculatingHeadOfState)
        {
            GameLoggingSystem.Instance.LogEvent("Skipping full council recalculation - already processing seat bonuses or recalculating", "GovernmentLogic");
            return;
        }
        
        // Check if we've already recalculated this seventh (use unified throttling)
        if (!ShouldTriggerHeadOfStateRecalculation(force))
        {
            GameLoggingSystem.Instance.LogEvent($"Skipping full council recalculation - already done this seventh (reason: {reason})", "GovernmentLogic");
            return;
        }
        
        GameLoggingSystem.Instance.LogEvent($"Triggering full council recalculation due to: {reason}", "GovernmentLogic");
        
        // Set flags to prevent recursion and mark that we've recalculated this seventh
        isRecalculatingHeadOfState = true;
        hasRecalculatedHeadOfStateThisSeventh = true;
        
        try
        {
            // Use the centralized deterministic pipeline directly
            ProcessAllSeatBonuses();
        }
        finally
        {
            // Always clear the recursion flag, even if an exception occurs
            isRecalculatingHeadOfState = false;
        }
    }
    
    /// <summary>
    /// Recalculate all active legend bonuses with current stat values
    /// This is needed when Legend Effectiveness changes due to authority/head of state changes
    /// Uses the proven assignment/removal system for clean recalculation
    /// </summary>
    private void RecalculateAllLegendBonuses()
    {
        if (isProcessingSeatBonuses)
        {

            GameLoggingSystem.Instance.LogEvent("Skipping legend bonus recalculation - already processing seat bonuses", "GovernmentLogic");
            return;
        }
        
        // Deprecated path: directly use centralized pipeline now
        GameLoggingSystem.Instance.LogEvent("RecalculateAllLegendBonuses redirected to centralized pipeline", "GovernmentLogic");
        ProcessAllSeatBonuses();
    }
    
    /// <summary>
    /// Apply pending legend bonuses to a newly added resource slot
    /// This is called from GlobalProductionManager when a new resource slot is added
    /// </summary>
    public void ApplyPendingBonusesToResource(string resourceName, string sectionName)
    {
        if (string.IsNullOrEmpty(resourceName)) return;
        
        int appliedBonuses = 0;
        
        // Apply global pending bonuses
        foreach (var pendingBonus in pendingGlobalBonuses)
        {
            if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
            {
                if (pendingBonus.modifierType == ModifierType.Percentage)
                {
                    GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.legendSource);
                }
                else
                {
                    GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.legendSource);
                }
                appliedBonuses++;
            }
        }
        
        // Apply section-specific pending bonuses
        if (!string.IsNullOrEmpty(sectionName) && pendingSectionBonuses.ContainsKey(sectionName))
        {
            foreach (var pendingBonus in pendingSectionBonuses[sectionName])
            {
                if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
                {
                    if (pendingBonus.modifierType == ModifierType.Percentage)
                    {
                        GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.legendSource);
                    }
                    else
                    {
                        GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.legendSource);
                    }
                    appliedBonuses++;
                }
            }
        }
        
        // Apply resource-specific pending bonuses
        if (pendingResourceBonuses.ContainsKey(resourceName))
        {
            foreach (var pendingBonus in pendingResourceBonuses[resourceName])
            {
                if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
                {
                    if (pendingBonus.modifierType == ModifierType.Percentage)
                    {
                        GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.legendSource);
                    }
                    else
                    {
                        GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.legendSource);
                    }
                    appliedBonuses++;
                }
            }
            
            // Remove applied bonuses from pending list
            pendingResourceBonuses[resourceName].Clear();
        }
        
        if (appliedBonuses > 0)
        {
            GameLoggingSystem.Instance.LogEvent($"Applied {appliedBonuses} pending legend bonuses to new resource: {resourceName} (section: {sectionName})", "GovernmentLogic");
        }
    }
    


    public void CalculateGovernmentCoordinates()
    {
        if (StatManager.Instance == null) return;

        // Get current pillar values
        int waltz = StatManager.Instance.GetPillarValue("waltz");
        int regalia = StatManager.Instance.GetPillarValue("regalia");
        int chorus = StatManager.Instance.GetPillarValue("chorus");
        int aureus = StatManager.Instance.GetPillarValue("aureus");

        // Calculate net differences
        int waltzRegaliaDifference = regalia - waltz; // Swapped: Regalia positive, Waltz negative
        int chorusAureusDifference = chorus - aureus;

        // Determine coordinates based on thresholds
        int oldWaltzRegalia = waltzRegaliaCoordinate;
        int oldChorusAureus = chorusAureusCoordinate;

        waltzRegaliaCoordinate = CalculateAxisCoordinate(waltzRegaliaDifference);
        chorusAureusCoordinate = CalculateAxisCoordinate(chorusAureusDifference);

        GameLoggingSystem.Instance.LogEvent($"Coordinates calculated:", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Waltz: {waltz}, Regalia: {regalia} → Net difference: {waltzRegaliaDifference} → Coordinate: {waltzRegaliaCoordinate} (Waltz negative, Regalia positive)", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Chorus: {chorus}, Aureus: {aureus} → Net difference: {chorusAureusDifference} → Coordinate: {chorusAureusCoordinate}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Political compass position: ({waltzRegaliaCoordinate}, {chorusAureusCoordinate})", "GovernmentLogic");

        // Notify listeners if coordinates changed
        if (oldWaltzRegalia != waltzRegaliaCoordinate || oldChorusAureus != chorusAureusCoordinate)
        {
            OnCoordinatesChanged?.Invoke(waltzRegaliaCoordinate, chorusAureusCoordinate);
        }
    }


    private int CalculateAxisCoordinate(int netDifference)
    {
        int absoluteDifference = Mathf.Abs(netDifference);
        
        if (absoluteDifference < centristThreshold)
        {
            return 0; // Centrist
        }
        else if (absoluteDifference < leaningThreshold)
        {
            return netDifference > 0 ? 1 : -1; // Leaning
        }
        else if (absoluteDifference < pillarThreshold)
        {
            return netDifference > 0 ? 2 : -2; // Pillar
        }
        else
        {
            return netDifference > 0 ? 3 : -3; // Fanatic
        }
    }

    /// <summary>
    /// Determine government type based on current coordinates
    /// </summary>
    public void DetermineGovernmentType()
    {
        GovernmentType oldType = currentGovernmentType;
        
        // Determine government type based on coordinates
        currentGovernmentType = ClassifyGovernmentType(waltzRegaliaCoordinate, chorusAureusCoordinate);
        
        // Update government name and description
        UpdateGovernmentInfo();

        GameLoggingSystem.Instance.LogEvent($"Government type determined: {oldType} → {currentGovernmentType}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Name: {currentGovernmentName}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Description: {currentGovernmentDescription}", "GovernmentLogic");

        // Notify listeners if government type changed
        if (oldType != currentGovernmentType)
        {
            OnGovernmentChanged?.Invoke(currentGovernmentType, currentGovernmentName, currentGovernmentDescription);
            OnGovernmentTypeChangedForCivics?.Invoke(currentGovernmentType);
        }
    }

    private GovernmentType ClassifyGovernmentType(int waltzRegalia, int chorusAureus)
    {
        // Fanatic Radical governments (corner combinations: ±3,±3)
        if (Mathf.Abs(waltzRegalia) == 3 && Mathf.Abs(chorusAureus) == 3)
        {
            return GovernmentType.FanaticRadical;
        }

        // True Centrist (center: 0,0)
        if (waltzRegalia == 0 && chorusAureus == 0)
        {
            return GovernmentType.TrueCentrist;
        }

        // True Pillar governments (single axis at ±3 - Fanatic level)
        if (waltzRegalia == -3 && chorusAureus == 0) return GovernmentType.TrueWaltz;      // (-3,0)
        if (waltzRegalia == 3 && chorusAureus == 0) return GovernmentType.TrueRegalia;     // (3,0)
        if (waltzRegalia == 0 && chorusAureus == 3) return GovernmentType.TrueChorus;      // (0,3)
        if (waltzRegalia == 0 && chorusAureus == -3) return GovernmentType.TrueAureus;     // (0,-3)

        // Pillar Leaning governments (single axis at ±2 - Pillar level)
        if (waltzRegalia == -2 && chorusAureus == 0) return GovernmentType.WaltzLeaning;   // (-2,0)
        if (waltzRegalia == 2 && chorusAureus == 0) return GovernmentType.RegaliaLeaning;  // (2,0)
        if (waltzRegalia == 0 && chorusAureus == 2) return GovernmentType.ChorusLeaning;   // (0,2)
        if (waltzRegalia == 0 && chorusAureus == -2) return GovernmentType.AureusLeaning;  // (0,-2)

        // Pillar Centrist governments (single axis at ±1 - Leaning level)
        if (waltzRegalia == -1 && chorusAureus == 0) return GovernmentType.WaltzCentrist;   // (-1,0)
        if (waltzRegalia == 1 && chorusAureus == 0) return GovernmentType.RegaliaCentrist;  // (1,0)
        if (waltzRegalia == 0 && chorusAureus == 1) return GovernmentType.ChorusCentrist;   // (0,1)
        if (waltzRegalia == 0 && chorusAureus == -1) return GovernmentType.AureusCentrist;  // (0,-1)

        // Pillar + Pillar Centrist governments (both coordinates at ±1)
        if (waltzRegalia == -1 && chorusAureus == 1) return GovernmentType.WaltzChorusCentrist;    // (-1,1)
        if (waltzRegalia == 1 && chorusAureus == 1) return GovernmentType.RegaliaChorusCentrist;   // (1,1)
        if (waltzRegalia == -1 && chorusAureus == -1) return GovernmentType.WaltzAureusCentrist;   // (-1,-1)
        if (waltzRegalia == 1 && chorusAureus == -1) return GovernmentType.RegaliaAureusCentrist;  // (1,-1)

        // Radical governments (specific combinations based on grid)
        // Top-left quadrant: Waltz + Chorus Radicals
        if (waltzRegalia == -2 && chorusAureus == 3) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Waltz Pillar + Chorus Fanatic", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        if (waltzRegalia == -3 && chorusAureus == 2) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Waltz Fanatic + Chorus Pillar", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        
        // Top-right quadrant: Regalia + Chorus Radicals
        if (waltzRegalia == 2 && chorusAureus == 3) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Regalia Pillar + Chorus Fanatic", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        if (waltzRegalia == 3 && chorusAureus == 2) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Regalia Fanatic + Chorus Pillar", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        
        // Bottom-left quadrant: Waltz + Aureus Radicals
        if (waltzRegalia == -2 && chorusAureus == -3) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Waltz Pillar + Aureus Fanatic", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        if (waltzRegalia == -3 && chorusAureus == -2) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Waltz Fanatic + Aureus Pillar", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        
        // Bottom-right quadrant: Regalia + Aureus Radicals
        if (waltzRegalia == 2 && chorusAureus == -3) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Regalia Pillar + Aureus Fanatic", "GovernmentLogic");
            return GovernmentType.Radical;
        }
        if (waltzRegalia == 3 && chorusAureus == -2) 
        {
            GameLoggingSystem.Instance.LogEvent($"Radical classified: ({waltzRegalia},{chorusAureus}) - Regalia Fanatic + Aureus Pillar", "GovernmentLogic");
            return GovernmentType.Radical;
        }

        // Regular government types (remaining combinations)
        // Waltz + Chorus combinations (left side, top)
        if (waltzRegalia < 0 && chorusAureus > 0)
        {
            return GovernmentType.WaltzChorus;
        }
        
        // Regalia + Chorus combinations (right side, top)
        if (waltzRegalia > 0 && chorusAureus > 0)
        {
            return GovernmentType.RegaliaChorus;
        }
        
        // Waltz + Aureus combinations (left side, bottom)
        if (waltzRegalia < 0 && chorusAureus < 0)
        {
            return GovernmentType.WaltzAureus;
        }
        
        // Regalia + Aureus combinations (right side, bottom)
        if (waltzRegalia > 0 && chorusAureus < 0)
        {
            return GovernmentType.RegaliaAureus;
        }

        // Fallback
        return GovernmentType.TrueCentrist;
    }

    /// <summary>
    /// Update government name and description based on current type
    /// </summary>
    private void UpdateGovernmentInfo()
    {
        switch (currentGovernmentType)
        {
            case GovernmentType.TrueCentrist:
                currentGovernmentName = "True Centrist";
                currentGovernmentDescription = "A perfectly balanced government with no ideological leanings";
                break;

            case GovernmentType.WaltzChorusCentrist:
                currentGovernmentName = "Waltz + Chorus Centrist";
                currentGovernmentDescription = "Balanced between harmony and arcane knowledge";
                break;

            case GovernmentType.RegaliaChorusCentrist:
                currentGovernmentName = "Regalia + Chorus Centrist";
                currentGovernmentDescription = "Balanced between authority and arcane knowledge";
                break;

            case GovernmentType.WaltzAureusCentrist:
                currentGovernmentName = "Waltz + Aureus Centrist";
                currentGovernmentDescription = "Balanced between harmony and innovation";
                break;

            case GovernmentType.RegaliaAureusCentrist:
                currentGovernmentName = "Regalia + Aureus Centrist";
                currentGovernmentDescription = "Balanced between authority and innovation";
                break;

            case GovernmentType.WaltzLeaning:
                currentGovernmentName = "Waltz Leaning";
                currentGovernmentDescription = "Government favors harmony and endurance";
                break;

            case GovernmentType.RegaliaLeaning:
                currentGovernmentName = "Regalia Leaning";
                currentGovernmentDescription = "Government favors authority and ambition";
                break;

            case GovernmentType.ChorusLeaning:
                currentGovernmentName = "Chorus Leaning";
                currentGovernmentDescription = "Government favors arcane knowledge and secrecy";
                break;

            case GovernmentType.AureusLeaning:
                currentGovernmentName = "Aureus Leaning";
                currentGovernmentDescription = "Government favors innovation and piety";
                break;

            case GovernmentType.WaltzCentrist:
                currentGovernmentName = "Waltz Centrist";
                currentGovernmentDescription = "Government moderately favors harmony and endurance";
                break;

            case GovernmentType.RegaliaCentrist:
                currentGovernmentName = "Regalia Centrist";
                currentGovernmentDescription = "Government moderately favors authority and ambition";
                break;

            case GovernmentType.ChorusCentrist:
                currentGovernmentName = "Chorus Centrist";
                currentGovernmentDescription = "Government moderately favors arcane knowledge and secrecy";
                break;

            case GovernmentType.AureusCentrist:
                currentGovernmentName = "Aureus Centrist";
                currentGovernmentDescription = "Government moderately favors innovation and piety";
                break;

            case GovernmentType.TrueWaltz:
                currentGovernmentName = "True Waltz";
                currentGovernmentDescription = "Government strongly emphasizes harmony and endurance";
                break;

            case GovernmentType.TrueRegalia:
                currentGovernmentName = "True Regalia";
                currentGovernmentDescription = "Government strongly emphasizes authority and ambition";
                break;

            case GovernmentType.TrueChorus:
                currentGovernmentName = "True Chorus";
                currentGovernmentDescription = "Government strongly emphasizes arcane knowledge and secrecy";
                break;

            case GovernmentType.TrueAureus:
                currentGovernmentName = "True Aureus";
                currentGovernmentDescription = "Government strongly emphasizes innovation and piety";
                break;

            case GovernmentType.WaltzChorus:
                currentGovernmentName = "Waltz + Chorus";
                currentGovernmentDescription = "Government combines harmony with arcane knowledge";
                break;

            case GovernmentType.RegaliaChorus:
                currentGovernmentName = "Regalia + Chorus";
                currentGovernmentDescription = "Government combines authority with arcane knowledge";
                break;

            case GovernmentType.WaltzAureus:
                currentGovernmentName = "Waltz + Aureus";
                currentGovernmentDescription = "Government combines harmony with innovation";
                break;

            case GovernmentType.RegaliaAureus:
                currentGovernmentName = "Regalia + Aureus";
                currentGovernmentDescription = "Government combines authority with innovation";
                break;

            case GovernmentType.Radical:
                currentGovernmentName = "Radical";
                currentGovernmentDescription = "Government with extreme ideological positions";
                break;

            case GovernmentType.FanaticRadical:
                currentGovernmentName = "Fanatic Radical";
                currentGovernmentDescription = "Government with fanatical ideological positions";
                break;

            default:
                currentGovernmentName = "Unknown";
                currentGovernmentDescription = "Unknown government type";
                break;
        }
    }

    /// <summary>
    /// Get current government information
    /// </summary>
    public (GovernmentType type, string name, string description, int waltzRegalia, int chorusAureus) GetCurrentGovernment()
    {
        return (currentGovernmentType, currentGovernmentName, currentGovernmentDescription, waltzRegaliaCoordinate, chorusAureusCoordinate);
    }

    /// <summary>
    /// Get current political compass coordinates
    /// </summary>
    public (int waltzRegalia, int chorusAureus) GetCurrentCoordinates()
    {
        return (waltzRegaliaCoordinate, chorusAureusCoordinate);
    }

    /// <summary>
    /// Get threshold configuration
    /// </summary>
    public (int centrist, int leaning, int pillar, int fanatic) GetThresholds()
    {
        return (centristThreshold, leaningThreshold, pillarThreshold, fanaticThreshold);
    }

    public void LogGovernmentInfo()
    {
        if (!GameLoggingSystem.Instance.enableGovernmentLogicLogging) return;

        GameLoggingSystem.Instance.LogEvent("=== GOVERNMENT SYSTEM INFO ===", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Current Government: {currentGovernmentName}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Government Type: {currentGovernmentType}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Description: {currentGovernmentDescription}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Political Compass: ({waltzRegaliaCoordinate}, {chorusAureusCoordinate})", "GovernmentLogic");
        
        // Show pillar values and differences
        if (StatManager.Instance != null)
        {
            int waltz = StatManager.Instance.GetPillarValue("waltz");
            int regalia = StatManager.Instance.GetPillarValue("regalia");
            int chorus = StatManager.Instance.GetPillarValue("chorus");
            int aureus = StatManager.Instance.GetPillarValue("aureus");
            
            GameLoggingSystem.Instance.LogEvent($"Pillar Values: Waltz={waltz}, Regalia={regalia}, Chorus={chorus}, Aureus={aureus}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"Net Differences: Waltz-Regalia={waltz-regalia}, Chorus-Aureus={chorus-aureus}", "GovernmentLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent($"Thresholds: Centrist={centristThreshold}, Leaning={leaningThreshold}, Pillar={pillarThreshold}, Fanatic={fanaticThreshold}", "GovernmentLogic");
        
        // Show civic system integration
        if (CivicManager.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent("=== CIVIC INTEGRATION ===", "GovernmentLogic");
            var allCivics = CivicManager.Instance.GetAllActiveCivics();
            GameLoggingSystem.Instance.LogEvent($"Active Civics: {allCivics.Count}", "GovernmentLogic");
            foreach (var civic in allCivics)
            {
                GameLoggingSystem.Instance.LogEvent($"  {civic.civicName} ({civic.tier}) - {civic.description}", "GovernmentLogic");
            }
            
            // Show civic status information
            var civicStatus = GetCivicStatus();
            var totalLoaded = CivicManager.Instance.GetAllAvailableCivicNames().Count;
            GameLoggingSystem.Instance.LogEvent($"Civic Status:", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Total Loaded from Resources: {totalLoaded} civics", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Loaded but Locked: {civicStatus.locked.Count} civics", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Unlocked: {civicStatus.unlocked.Count} civics", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Available for Seat Replacement: {civicStatus.availableForSeats.Count} options", "GovernmentLogic");
            
            if (civicStatus.locked.Count > 0)
            {
                GameLoggingSystem.Instance.LogEvent($"  Locked Civics: {string.Join(", ", civicStatus.locked)}", "GovernmentLogic");
            }
            
            if (civicStatus.unlocked.Count > 0)
            {
                GameLoggingSystem.Instance.LogEvent($"  Unlocked Civics: {string.Join(", ", civicStatus.unlocked)}", "GovernmentLogic");
            }
        }
        
        // Show council system information
        GameLoggingSystem.Instance.LogEvent("=== COUNCIL SYSTEM ===", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Total Council Seats: {councilSeats.Count}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Unlocked Regular Seats: {unlockedSeatCount}/6", "GovernmentLogic");
        
        // Display Head of State (never changes)
        var headOfState = GetCouncilSeat(-1);
        if (headOfState != null)
        {
            string headStatus = headOfState.assignedLegend != null ? 
                $"Assigned: {headOfState.assignedLegend.legendName} (Active: {headOfState.IsActive()})" : 
                "Empty";
            GameLoggingSystem.Instance.LogEvent($"  Head of State (-1): {headStatus}", "GovernmentLogic");
        }
        
        // Display all 6 regular positions (0-5)
        GameLoggingSystem.Instance.LogEvent("=== REGULAR COUNCIL POSITIONS ===", "GovernmentLogic");
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null)
            {
                string status = seat.isUnlocked ? 
                    (seat.assignedLegend != null ? 
                        $"Assigned: {seat.assignedLegend.legendName} (Active: {seat.IsActive()})" : 
                        "Empty") : 
                    "Locked";
                string seatType = seat.sourceCivic != null ? $"Civic: {seat.sourceCivic.civicName}" : "Default";
                GameLoggingSystem.Instance.LogEvent($"  Position {i}: {seat.GetEffectiveTitle()} ({seatType}) - {status}", "GovernmentLogic");
            }
        }
        
        // Show available seat pool
        GameLoggingSystem.Instance.LogEvent("=== AVAILABLE SEAT POOL ===", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Total Available Seats: {availableSeatPool.Count}", "GovernmentLogic");
        foreach (var seat in availableSeatPool)
        {
            string seatType = seat.sourceCivic != null ? $"Civic: {seat.sourceCivic.civicName}" : "Default";
            GameLoggingSystem.Instance.LogEvent($"  {seat.seatTitle} ({seatType})", "GovernmentLogic");
        }
        
        // Show civic council seats
        if (civicCouncilSeats.Count > 0)
        {
            GameLoggingSystem.Instance.LogEvent("=== CIVIC COUNCIL SEATS ===", "GovernmentLogic");
            foreach (var kvp in civicCouncilSeats)
            {
                var civic = kvp.Key;
                var seat = kvp.Value;
                string status = seat.assignedLegend != null ? 
                    $"Assigned: {seat.assignedLegend.legendName} (Active: {seat.IsActive()})" : 
                    "Empty";
                GameLoggingSystem.Instance.LogEvent($"  {civic} ({seat.GetEffectiveTitle()}): {status}", "GovernmentLogic");
            }
        }
        
        GameLoggingSystem.Instance.LogEvent($"Total Council Seats (including civics): {GetTotalCouncilSeatCount()}", "GovernmentLogic");
    }

    public void ForceRecalculation()
    {
        CalculateGovernmentCoordinates();
        DetermineGovernmentType();
        LogGovernmentInfo();
    }
    
    /// <summary>
    /// Check if current government type matches a required government type for civics
    /// </summary>
    public bool CheckGovernmentTypeRequirement(string requiredGovernmentType)
    {
        if (string.IsNullOrEmpty(requiredGovernmentType))
            return true;
            
        return currentGovernmentType.ToString() == requiredGovernmentType;
    }
    
    /// <summary>
    /// Get current government type as string for civic requirements
    /// </summary>
    public string GetCurrentGovernmentTypeString()
    {
        return currentGovernmentType.ToString();
    }

    private string GetGovernmentTypeName(GovernmentType type)
    {
        switch (type)
        {
            case GovernmentType.TrueCentrist: return "True Centrist";
            case GovernmentType.WaltzChorusCentrist: return "Waltz + Chorus Centrist";
            case GovernmentType.RegaliaChorusCentrist: return "Regalia + Chorus Centrist";
            case GovernmentType.WaltzAureusCentrist: return "Waltz + Aureus Centrist";
            case GovernmentType.RegaliaAureusCentrist: return "Regalia + Aureus Centrist";
            case GovernmentType.WaltzLeaning: return "Waltz Leaning";
            case GovernmentType.RegaliaLeaning: return "Regalia Leaning";
            case GovernmentType.ChorusLeaning: return "Chorus Leaning";
            case GovernmentType.AureusLeaning: return "Aureus Leaning";
            case GovernmentType.WaltzCentrist: return "Waltz Centrist";
            case GovernmentType.RegaliaCentrist: return "Regalia Centrist";
            case GovernmentType.ChorusCentrist: return "Chorus Centrist";
            case GovernmentType.AureusCentrist: return "Aureus Centrist";
            case GovernmentType.TrueWaltz: return "True Waltz";
            case GovernmentType.TrueRegalia: return "True Regalia";
            case GovernmentType.TrueChorus: return "True Chorus";
            case GovernmentType.TrueAureus: return "True Aureus";
            case GovernmentType.WaltzChorus: return "Waltz + Chorus";
            case GovernmentType.RegaliaChorus: return "Regalia + Chorus";
            case GovernmentType.WaltzAureus: return "Waltz + Aureus";
            case GovernmentType.RegaliaAureus: return "Regalia + Aureus";
            case GovernmentType.Radical: return "Radical";
            case GovernmentType.FanaticRadical: return "Fanatic Radical";
            default: return "Unknown";
        }
    }
    
    /// <summary>
    /// Initialize the council system with default seats
    /// </summary>
    private void InitializeCouncilSystem()
    {
        councilSeats.Clear();
        availableSeatPool.Clear();
        defaultSeatTemplates.Clear();
        
        // Create Head of State position (-1 index) - NEVER CHANGES
        var headOfState = new CouncilSeat("Head of State", -1, true);
        headOfState.isUnlocked = true;
        headOfState.seventhsUntilActive = 0; // Ensure it starts with 0 (will be set to 3 when legend is assigned)
        headOfState.allowedLegendClasses.AddRange(new[] { 
            LegendClass.Sovereign, LegendClass.Vanguard, LegendClass.Steward, 
            LegendClass.Weaver, LegendClass.Seer, LegendClass.Justiciar 
        });
        headOfState.roleplayDescription = "The supreme leader of the civilization, representing the will of the people and the authority of the state.";
        councilSeats.Add(headOfState);
        
        // Create default seat templates that are always available as options
        CreateDefaultSeatTemplates();
        
        // Initialize the 6 regular positions with default seats
        InitializeRegularSeats();
        
        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Initialized flexible council system with {councilSeats.Count} total seats (1 Head of State + {unlockedSeatCount} unlocked regular seats)", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Available seat pool: {availableSeatPool.Count} seats, Default templates: {defaultSeatTemplates.Count}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Create default seat templates that are always available as fallback options
    /// </summary>
    private void CreateDefaultSeatTemplates()
    {
        // Create default seats from inspector-configured data
        foreach (var config in defaultSeatConfigs)
        {
            if (string.IsNullOrEmpty(config.title)) continue;
            
            var template = CreateSeatFromConfig(config);
            defaultSeatTemplates.Add(template);
            availableSeatPool.Add(template);
        }
        
        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Created {defaultSeatTemplates.Count} default seat templates from configuration", "GovernmentLogic");
    }
    
    /// <summary>
    /// Create a CouncilSeat from a DefaultSeatTemplate configuration
    /// </summary>
    /// <param name="config">The configuration template</param>
    /// <returns>A fully configured CouncilSeat</returns>
    private CouncilSeat CreateSeatFromConfig(DefaultSeatTemplate config)
    {
        var seat = new CouncilSeat(config.title, -999);
        seat.roleplayDescription = config.description;
        seat.seatIcon = config.icon;
        
        // Add allowed legend classes
        if (config.allowedClasses != null && config.allowedClasses.Length > 0)
        {
            seat.allowedLegendClasses.AddRange(config.allowedClasses);
        }
        
        // Add seat bonuses
        if (config.bonuses != null)
        {
            foreach (var bonusConfig in config.bonuses)
            {
                seat.seatBonuses.Add(new SeatBonus
                {
                    bonusType = bonusConfig.bonusType,
                    targetStat = bonusConfig.targetStat,
                    modifierValue = bonusConfig.modifierValue,
                    modifierType = bonusConfig.modifierType,
                    requiresLegend = bonusConfig.requiresLegend
                });
            }
        }
        
        return seat;
    }
    
    /// <summary>
    /// Initialize the 6 regular council positions with default seats
    /// </summary>
    private void InitializeRegularSeats()
    {
        // Start with first 3 default seats active
        for (int i = 0; i < 6; i++)
        {
            if (i < unlockedSeatCount)
            {
                // Use default seat templates for initial positions
                var defaultSeat = CreateSeatFromTemplate(defaultSeatTemplates[i], i);
                activeRegularSeats[i] = defaultSeat;
                councilSeats.Add(defaultSeat);
            }
            else
            {
                // For locked positions, don't create placeholders - just set to null
                activeRegularSeats[i] = null;
            }
        }
    }
    
    /// <summary>
    /// Create a seat instance from a template for a specific position
    /// </summary>
    private CouncilSeat CreateSeatFromTemplate(CouncilSeat template, int position)
    {
        var seat = new CouncilSeat(template.seatTitle, position);
        seat.isUnlocked = true;
        seat.allowedLegendClasses.AddRange(template.allowedLegendClasses);
        seat.roleplayDescription = template.roleplayDescription;
        
        // Copy seat bonuses
        foreach (var bonus in template.seatBonuses)
        {
            seat.seatBonuses.Add(new SeatBonus {
                bonusType = bonus.bonusType,
                targetStat = bonus.targetStat,
                modifierValue = bonus.modifierValue,
                modifierType = bonus.modifierType,
                description = bonus.GetAutoDescription(),
                requiresLegend = bonus.requiresLegend
            });
        }
        
        return seat;
    }
    
    /// <summary>
    /// Unlock an additional council seat
    /// </summary>
    public bool UnlockCouncilSeat(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= 6) // Only regular seats 0-5 can be unlocked
        {
            Debug.LogWarning($"[GovernmentLogic] Invalid seat index: {seatIndex}. Only seats 0-5 can be unlocked.");
            return false;
        }
        
        if (unlockedSeatCount >= 6)
        {
            Debug.LogWarning($"[GovernmentLogic] Cannot unlock more seats. Maximum of 6 regular seats already unlocked.");
            return false;
        }
        
        var seat = activeRegularSeats[seatIndex];
        if (seat == null)
        {
            Debug.LogError($"[GovernmentLogic] Seat {seatIndex} is null");
            return false;
        }
        
        if (seat.isUnlocked)
        {
            Debug.LogWarning($"[GovernmentLogic] Seat {seatIndex} is already unlocked");
            return false;
        }
        
        // Unlock the seat and assign a default template
        unlockedSeatCount++;
        seat.isUnlocked = true;
        
        // Find an available default template that's not currently in use
        var availableTemplate = FindAvailableDefaultTemplate();
        if (availableTemplate != null)
        {
            // Replace the placeholder with the default seat
            ReplaceSeatAtPosition(seatIndex, availableTemplate);
        }
        
        OnCouncilSeatUnlocked?.Invoke(seat);
        
        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Unlocked council seat {seatIndex}: {seat.GetEffectiveTitle()}. Total unlocked: {unlockedSeatCount}/6", "GovernmentLogic");
        
        return true;
    }
    
    /// <summary>
    /// Find an available default template that's not currently in use
    /// </summary>
    private CouncilSeat FindAvailableDefaultTemplate()
    {
        foreach (var template in defaultSeatTemplates)
        {
            bool isInUse = false;
            for (int i = 0; i < 6; i++)
            {
                if (activeRegularSeats[i] != null && 
                    activeRegularSeats[i].isUnlocked && 
                    activeRegularSeats[i].seatTitle == template.seatTitle)
                {
                    isInUse = true;
                    break;
                }
            }
            
            if (!isInUse)
            {
                return template;
            }
        }
        
        // If all templates are in use, return the first one (fallback)
        return defaultSeatTemplates.Count > 0 ? defaultSeatTemplates[0] : null;
    }
    
    /// <summary>
    /// Replace a seat at a specific position with a new seat
    /// </summary>
    private void ReplaceSeatAtPosition(int position, CouncilSeat newSeat)
    {
        if (position < 0 || position >= activeRegularSeats.Length)
        {
            Debug.LogWarning($"[GovernmentLogic] Cannot replace seat at invalid position: {position}");
            return;
        }

        var oldSeat = activeRegularSeats[position];
        if (oldSeat == null)
        {
            Debug.LogError($"[GovernmentLogic] Cannot replace null seat at position: {position}");
            return;
        }

        // Remove any assigned legend from the old seat BEFORE replacing the seat
        if (oldSeat.assignedLegend != null)
        {
            var legend = oldSeat.assignedLegend;
            
            // Clear any bonuses from the old legend
            RemoveSeatBonuses(legend);
            
            // Properly remove the legend and trigger UI events
            if (RemoveLegendFromSeat(position))
            {
                GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Removed legend {legend.legendName} from seat at position {position} during replacement", "GovernmentLogic");
            }
        }

        // Remove the old seat from the list of council seats
        councilSeats.Remove(oldSeat);

        // Create a new seat instance from the template
        var newSeatInstance = CreateSeatFromTemplate(newSeat, position);

        // Add the new seat to the list of council seats
        councilSeats.Add(newSeatInstance);

        // Update the activeRegularSeats array
        activeRegularSeats[position] = newSeatInstance;

        // Notify listeners about the seat change
        OnCouncilSeatChanged?.Invoke(newSeatInstance);
        
        // Also notify about council composition change since we replaced a seat
        OnCouncilCompositionChanged?.Invoke();

        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Replaced seat at position {position}: {oldSeat.GetEffectiveTitle()} -> {newSeatInstance.GetEffectiveTitle()}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Get a council seat by index
    /// </summary>
    public CouncilSeat GetCouncilSeat(int seatIndex)
    {
        // Special case for Head of State (seatIndex = -1)
        if (seatIndex == -1)
        {
            // Head of State is always stored at index 0 in the councilSeats list
            if (councilSeats.Count > 0 && councilSeats[0].seatIndex == -1)
            {
                return councilSeats[0];
            }
            return null;
        }
        
        // For regular seats (0-5), check bounds and return
        if (seatIndex < 0 || seatIndex >= 6)
            return null;
            
        // Find the seat with the matching seatIndex
        foreach (var seat in councilSeats)
        {
            if (seat.seatIndex == seatIndex)
            {
                return seat;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Get all council seats (regular + civic)
    /// </summary>
    public List<CouncilSeat> GetAllCouncilSeats()
    {
        var allSeats = new List<CouncilSeat>(councilSeats);
        allSeats.AddRange(civicCouncilSeats.Values);
        return allSeats;
    }
    
    /// <summary>
    /// Get unlocked council seats
    /// </summary>
    public List<CouncilSeat> GetUnlockedCouncilSeats()
    {
        return councilSeats.Where(s => s.isUnlocked).ToList();
    }
    
    /// <summary>
    /// Get the number of unlocked council seats
    /// </summary>
    public int GetUnlockedCouncilSeatCount()
    {
        return councilSeats.Count(s => s.isUnlocked);
    }
    
    /// <summary>
    /// Check if a council seat can be unlocked
    /// </summary>
    public bool CanUnlockCouncilSeat(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= 6) // Only regular seats 0-5 can be unlocked
        {
            Debug.LogWarning($"[GovernmentLogic] Invalid seat index for unlocking: {seatIndex}. Only seats 0-5 can be unlocked.");
            return false;
        }

        if (unlockedSeatCount >= 6)
        {
            Debug.LogWarning($"[GovernmentLogic] Cannot unlock more seats. Maximum of 6 regular seats already unlocked.");
            return false;
        }

        var seat = activeRegularSeats[seatIndex];
        if (seat == null)
        {
            Debug.LogError($"[GovernmentLogic] Seat {seatIndex} is null for unlocking check");
            return false;
        }

        return !seat.isUnlocked && unlockedSeatCount < 6;
    }
    
    // ===== COUNCIL SEAT PROCESSING SYSTEM =====
    
    /// <summary>
    /// Process all active seat bonuses for unlocked council seats
    /// This should be called when legends are assigned/removed or civics change
    /// </summary>
    public void ProcessAllSeatBonuses()
    {
        if (isProcessingSeatBonuses)
        {
            Debug.LogWarning("[GovernmentLogic] ProcessAllSeatBonuses: Already processing, skipping to prevent infinite loops");
            return; // Prevent infinite loops
        }
        
        isProcessingSeatBonuses = true;
        
        GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Seat pipeline: start ({GetUnlockedCouncilSeatCount()} unlocked)", "GovernmentLogic");
        
        try
        {
            int processedCount = 0;
            
            // HARD RESET: clear all government-origin modifiers from every assigned seat/legend
            // This guarantees no residual bonuses from inactive seats (e.g., during perfect swaps)
            var seatsWithLegends = councilSeats.Where(seat => seat.assignedLegend != null).ToList();
            foreach (var seat in seatsWithLegends)
            {
                RemoveSeatBonuses(seat.assignedLegend);
            }
            if (StatManager.Instance != null)
            {
                StatManager.Instance.ForceCompleteRecalculation();
            }
            
            // Get all active seats that need processing (centralized deterministic pipeline)
            // Always include all active seats and process deterministically by seatIndex
            var activeSeats = councilSeats
                .Where(seat => seat.isUnlocked && seat.IsActive() && seat.assignedLegend != null)
                .OrderBy(seat => seat.seatIndex)
                .ToList();
            
            if (activeSeats.Count == 0)
            {
                GameLoggingSystem.Instance.LogEvent("[GovernmentLogic] No active seats to process", "GovernmentLogic");
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Processing {activeSeats.Count} active seats", "GovernmentLogic");
                
                // Reset override/snapshot for this processing cycle
                useLegendEffectivenessOverride = false;
                _currentLegendEffectivenessSnapshot = -1f;

                // (All seat/legend sources were cleared globally before determining active seats)
                
                // PHASE A: Apply Authority-only bonuses first to stabilize Legend Effectiveness base
            foreach (var seat in activeSeats)
            {
                    // Legend authority bonuses (do NOT LE-enhance authority)
                    foreach (var legendBonus in seat.assignedLegend.bonuses)
                    {
                        if (legendBonus != null && legendBonus.bonusType == GameEffectType.SubstatBonus && legendBonus.targetStat.ToLower() == "authority")
                        {
                            ApplyLegendBonus(legendBonus, seat.assignedLegend);
                        }
                    }
                    // Seat authority bonuses
                    foreach (var seatBonus in seat.seatBonuses)
                    {
                        if (seatBonus != null && seatBonus.bonusType == SeatBonusType.SubstatBonus && seatBonus.targetStat.ToLower() == "authority")
                        {
                            ApplySeatBonus(seatBonus, seat.assignedLegend);
                        }
                    }
                }
                
                // Recalculate derived stats so Legend Effectiveness reflects Authority phase
            if (StatManager.Instance != null)
            {
                    StatManager.Instance.ForceCompleteRecalculation();
                    lastKnownLegendEffectiveness = StatManager.Instance.GetDerivedValue("legendEffectiveness");
                    GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] LE after authority: {lastKnownLegendEffectiveness:F2}", "GovernmentLogic");
                    useLegendEffectivenessOverride = true;
                    legendEffectivenessOverride = lastKnownLegendEffectiveness;
                }
                
                // PHASE B: Apply direct Legend Effectiveness bonuses (no LE enhancement) across all seats
                float leBeforeDirect = lastKnownLegendEffectiveness;
                foreach (var seat in activeSeats)
                {
                    // Legend LE direct bonuses (only direct LE; not LE-enhanced)
                    foreach (var legendBonus in seat.assignedLegend.bonuses)
                    {
                        if (legendBonus != null && legendBonus.bonusType == GameEffectType.DerivedStatBonus && legendBonus.targetStat.ToLower() == "legendeffectiveness")
                        {
                            ApplyLegendBonus(legendBonus, seat.assignedLegend);
                        }
                    }
                    // Seat LE direct bonuses (seat bonuses are never LE-enhanced in our system)
                    foreach (var seatBonus in seat.seatBonuses)
                    {
                        if (seatBonus != null && seatBonus.bonusType == SeatBonusType.DerivedStatBonus && seatBonus.targetStat.ToLower() == "legendeffectiveness")
                        {
                            ApplySeatBonus(seatBonus, seat.assignedLegend);
                        }
                    }
                }
                
                // Recalculate again so final Legend Effectiveness is ready for LE-enhanced effects
                if (StatManager.Instance != null)
                {
                StatManager.Instance.ForceCompleteRecalculation();
                lastKnownLegendEffectiveness = StatManager.Instance.GetDerivedValue("legendEffectiveness");
                    GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] LE direct: {leBeforeDirect:F2} → {lastKnownLegendEffectiveness:F2}", "GovernmentLogic");
                    legendEffectivenessOverride = lastKnownLegendEffectiveness;
                    _currentLegendEffectivenessSnapshot = lastKnownLegendEffectiveness;
                    GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] LE snapshot: {_currentLegendEffectivenessSnapshot:F2}", "GovernmentLogic");
            }
            
                // PHASE C: Apply all Legend bonuses that are enhanced by Legend Effectiveness (resources/production/click/etc.)
            foreach (var seat in activeSeats)
            {
                    var legend = seat.assignedLegend;
                    if (legend == null) continue;
                    foreach (var legendBonus in legend.bonuses)
                    {
                        if (legendBonus == null) continue;
                        
                        // Skip Authority and direct LE which were applied earlier
                        if (legendBonus.bonusType == GameEffectType.SubstatBonus && legendBonus.targetStat.ToLower() == "authority") continue;
                        if (legendBonus.bonusType == GameEffectType.DerivedStatBonus && legendBonus.targetStat.ToLower() == "legendeffectiveness") continue;
                        
                        // Apply remaining legend bonuses now (these use LE-enhanced calculations where applicable)
                        switch (legendBonus.bonusType)
                        {
                            case GameEffectType.ResourceModifier:
                            case GameEffectType.ProductionModifier:
                            case GameEffectType.ClickPowerBonus:
                            case GameEffectType.ProductionScalingBonus:
                            case GameEffectType.ConstructionCostModifier:
                            case GameEffectType.DerivedStatBonus: // non-LE derived stats
                                ApplyLegendBonus(legendBonus, legend);
                                break;
                            default:
                                // Defer remaining types (e.g., morale, housing, pillar) to Phase D
                                break;
                        }
                    }
                }
                
                // PHASE D: Apply remaining Seat bonuses and Legend bonuses (including Pillar and other non-resource effects)
                foreach (var seat in activeSeats)
                {
                    var legend = seat.assignedLegend;
                    if (legend == null) continue;
                    
                    // Seat bonuses excluding Authority and LE already applied
                    foreach (var seatBonus in seat.seatBonuses)
                    {
                        if (seatBonus == null) continue;
                        if (seatBonus.bonusType == SeatBonusType.SubstatBonus && seatBonus.targetStat.ToLower() == "authority") continue;
                        if (seatBonus.bonusType == SeatBonusType.DerivedStatBonus && seatBonus.targetStat.ToLower() == "legendeffectiveness") continue;
                        ApplySeatBonus(seatBonus, legend);
                    }
                    
                    // Remaining legend bonuses not handled in Phase C that aren't authority or direct LE
                    foreach (var legendBonus in legend.bonuses)
                    {
                        if (legendBonus == null) continue;
                        if (legendBonus.bonusType == GameEffectType.SubstatBonus && legendBonus.targetStat.ToLower() == "authority") continue;
                        if (legendBonus.bonusType == GameEffectType.DerivedStatBonus && legendBonus.targetStat.ToLower() == "legendeffectiveness") continue;
                        
                        switch (legendBonus.bonusType)
                        {
                            case GameEffectType.PillarBonus:
                            case GameEffectType.MaxMoraleModifier:
                            case GameEffectType.MoraleBalanceModifier:
                            case GameEffectType.SatisfactionThresholdModifier:
                            case GameEffectType.HousingBonus:
                            case GameEffectType.SpecialAbility:
                                ApplyLegendBonus(legendBonus, legend);
                                break;
                        }
                    }
                }
                
                // Mark seats as processed post-pipeline
                foreach (var seat in activeSeats)
                {
                processedCount++;
                    seat.bonusesProcessed = true;
                }
            }
            
            // Reset flags for inactive or empty seats
            foreach (var seat in councilSeats)
            {
                if (!seat.IsActive() || seat.assignedLegend == null)
                {
                    if (seat.bonusesProcessed)
                    {
                        GameLoggingSystem.Instance.LogEvent($"Resetting bonusesProcessed flag for inactive/empty seat '{seat.seatTitle}'", "GovernmentLogic");
                        seat.bonusesProcessed = false;
                    }
                }
            }
            
            GameLoggingSystem.Instance.LogEvent($"Seat pipeline: done ({processedCount} seats)", "GovernmentLogic");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error processing seat bonuses: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        finally
        {
            isProcessingSeatBonuses = false;
            // Release LE override and snapshot at end of the centralized processing
            useLegendEffectivenessOverride = false;
            _currentLegendEffectivenessSnapshot = -1f;
        }
    }
    
    /// <summary>
    /// Process only Legend Effectiveness bonuses for a specific council seat (Phase 1)
    /// This ensures Authority-based bonuses are applied before other stats
    /// </summary>
    // Removed: Obsolete per-seat effectiveness phase. The centralized pipeline handles all phases.
    
    /// <summary>
    /// Process bonuses for a specific council seat
    /// </summary>
    private void ProcessSeatBonuses(CouncilSeat seat)
    {
        if (seat == null)
        {
            Debug.LogWarning("[GovernmentLogic] ProcessSeatBonuses: Seat is null");
            return;
        }
        
        if (!seat.IsActive())
        {
            GameLoggingSystem.Instance.LogEvent($"ProcessSeatBonuses: Seat '{seat.seatTitle}' is not active (seventhsUntilActive: {seat.seventhsUntilActive})", "GovernmentLogic");
            return;
        }
        
        if (seat.assignedLegend == null)
        {
            GameLoggingSystem.Instance.LogEvent($"ProcessSeatBonuses: Seat '{seat.seatTitle}' has no assigned legend", "GovernmentLogic");
            return;
        }
        
        GameLoggingSystem.Instance.LogEvent($"Processing {seat.seatBonuses.Count} bonuses for seat '{seat.seatTitle}' with legend '{seat.assignedLegend.legendName}'", "GovernmentLogic");
        
        // Process seat bonuses (skip Authority bonuses - already processed in Phase 1)
        foreach (var seatBonus in seat.seatBonuses)
        {
            // Skip Authority bonuses as they're processed in Phase 1
            if (seatBonus.bonusType == SeatBonusType.SubstatBonus && 
                seatBonus.targetStat.ToLower() == "authority")
            {
                continue; // Skip Authority bonuses
            }
            
            if (seatBonus.requiresLegend && seat.assignedLegend != null)
            {
                GameLoggingSystem.Instance.LogEvent($"Applying seat bonus: {seatBonus.bonusType} | Target: '{seatBonus.targetStat}' | Value: {seatBonus.modifierValue} | Type: {seatBonus.modifierType}", "GovernmentLogic");
                ApplySeatBonus(seatBonus, seat.assignedLegend);
            }
            else if (!seatBonus.requiresLegend)
            {
                GameLoggingSystem.Instance.LogEvent($"Applying passive seat bonus: {seatBonus.bonusType} | Target: '{seatBonus.targetStat}' | Value: {seatBonus.modifierValue} | Type: {seatBonus.modifierType}", "GovernmentLogic");
                ApplySeatBonus(seatBonus, seat.assignedLegend);
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"Skipping seat bonus (requires legend but none assigned): {seatBonus.bonusType}", "GovernmentLogic");
            }
        }
        
        // Process legend bonuses (from the assigned legend's own bonuses)
        if (seat.assignedLegend != null)
        {
            GameLoggingSystem.Instance.LogEvent($"Processing {seat.assignedLegend.bonuses.Count} legend bonuses for legend '{seat.assignedLegend.legendName}'", "GovernmentLogic");
            
            foreach (var legendBonus in seat.assignedLegend.bonuses)
            {
                // Skip Authority bonuses as they're processed in Phase 1
                if (legendBonus.bonusType == GameEffectType.SubstatBonus && 
                    legendBonus.targetStat.ToLower() == "authority")
                {
                    continue; // Skip Authority bonuses
                }
                
                GameLoggingSystem.Instance.LogEvent($"Applying legend bonus: {legendBonus.bonusType} | Target: '{legendBonus.targetStat}' | Value: {legendBonus.modifierValue} | Type: {legendBonus.modifierType}", "GovernmentLogic");
                ApplyLegendBonus(legendBonus, seat.assignedLegend);
            }
        }
    }
    
    /// <summary>
    /// Check if a legend is currently assigned to the Head of State position
    /// </summary>
    private bool IsLegendHeadOfState(LegendData legend)
    {
        if (legend == null) return false;
        
        var headOfState = GetCouncilSeat(-1); // Head of State has index -1
        return headOfState?.assignedLegend == legend && headOfState.IsActive();
    }
    
    /// <summary>
    /// Apply a single legend bonus to the game systems with Head of State multiplier
    /// </summary>
    private void ApplyLegendBonus(LegendBonus legendBonus, LegendData legend)
    {
        if (legendBonus == null)
        {
            Debug.LogWarning("[GovernmentLogic] ApplyLegendBonus: LegendBonus is null");
            return;
        }
        
        if (legend == null)
        {
            Debug.LogWarning("[GovernmentLogic] ApplyLegendBonus: Legend is null");
            return;
        }
        
        // Check if this legend is assigned to Head of State and apply multiplier
        bool isHeadOfState = IsLegendHeadOfState(legend);
        float effectiveValue = isHeadOfState ? legendBonus.modifierValue * headOfStateMultiplier : legendBonus.modifierValue;
        string sourceName = $"Legend: {legend.legendName}";
        
        // Keep logs minimal; only critical application summaries elsewhere
        
        try
        {
            switch (legendBonus.bonusType)
            {
                case GameEffectType.PillarBonus:
                    if (string.IsNullOrEmpty(legendBonus.targetStat))
                    {
                        Debug.LogWarning($"[GovernmentLogic] PillarBonus has empty targetStat for {sourceName}");
                        break;
                    }
                    
                    // Validate target stat
                    ValidateTargetStat(legendBonus.targetStat, "PillarBonus", sourceName);
                    
                    if (StatManager.Instance == null)
                    {
                        Debug.LogError($"[GovernmentLogic] StatManager.Instance is null for PillarBonus {sourceName}");
                        break;
                    }
                    
                    // Get base value for proper percentage calculation
                    float basePillarValue = StatManager.Instance.GetBasePillarValue(legendBonus.targetStat);
                    float pillarBonusValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, basePillarValue);
                    GameLoggingSystem.Instance.LogEvent($"Legend Pillar: {legendBonus.targetStat} +{pillarBonusValue} by {sourceName}", "GovernmentLogic");
                    StatManager.Instance.AddPillarBonus(legendBonus.targetStat, pillarBonusValue, sourceName);
                    
                    break;
                    
                case GameEffectType.SubstatBonus:
                    if (!string.IsNullOrEmpty(legendBonus.targetStat) && StatManager.Instance != null)
                    {
                        // Validate target stat
                        ValidateTargetStat(legendBonus.targetStat, "SubstatBonus", sourceName);
                        
                        // Get base value for proper percentage calculation
                        float baseSubstatValue = StatManager.Instance.GetBaseSubstatValue(legendBonus.targetStat);
                        float bonusValue;
                        if (string.Equals(legendBonus.targetStat, "authority", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // Do NOT LE-enhance authority to avoid circular amplification
                            bonusValue = CalculateDirectModifierValue(effectiveValue, legendBonus.modifierType, baseSubstatValue);
                        }
                        else
                        {
                            bonusValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, baseSubstatValue);
                        }
                        GameLoggingSystem.Instance.LogEvent($"Legend Substat: {legendBonus.targetStat} +{bonusValue} by {sourceName}", "GovernmentLogic");
                        StatManager.Instance.AddSubstatBonus(legendBonus.targetStat, bonusValue, sourceName);
                    }
                    break;
                    
                case GameEffectType.DerivedStatBonus:
                    if (!string.IsNullOrEmpty(legendBonus.targetStat) && StatManager.Instance != null)
                    {
                        // Validate target stat
                        ValidateTargetStat(legendBonus.targetStat, "DerivedStatBonus", sourceName);
                        
                        // Get base value for proper percentage calculation
                        float baseDerivedValue = StatManager.Instance.GetBaseDerivedValue(legendBonus.targetStat);
                        // Prevent recursive Legend Effectiveness enhancement
                        bool isLegendEffectivenessTarget = legendBonus.targetStat.ToLower() == "legendeffectiveness";
                        float bonusValue;
                        if (isLegendEffectivenessTarget)
                        {
                            // Differentiate add vs percentage for LE direct bonuses
                            if (legendBonus.modifierType == ModifierType.Add)
                            {
                                // Additive points directly to LE (e.g., +5 means +5 p.p.)
                                bonusValue = effectiveValue;
                            }
                            else if (legendBonus.modifierType == ModifierType.Percentage)
                            {
                                // Percentage of base LE (e.g., +10% of base 40 → +4 p.p.)
                                bonusValue = (effectiveValue / 100f) * baseDerivedValue;
                            }
                            else
                            {
                                // SetValue: set absolute LE; add delta
                                bonusValue = effectiveValue - baseDerivedValue;
                            }
                            GameLoggingSystem.Instance.LogEvent($"LE direct: base {baseDerivedValue:F2} +{bonusValue:F2} via {legendBonus.modifierType} from {sourceName}", "GovernmentLogic");
                        }
                        else
                        {
                            bonusValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, baseDerivedValue);
                        }
                        StatManager.Instance.AddDerivedStatBonus(legendBonus.targetStat, bonusValue, sourceName);
                    }
                    break;
                    
                case GameEffectType.ResourceModifier:
                    if (GlobalProductionManager.Instance != null)
                    {
                        // Validate target stat if specified
                        if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            ValidateTargetStat(legendBonus.targetStat, "ResourceModifier", sourceName);
                        }
                        
                        float modifierValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        bool isPositive = modifierValue > 0;
                        
                        if (legendBonus.scope == ScopeType.Global || string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to all resources (global bonus)
                            int leSnap = Mathf.RoundToInt(_currentLegendEffectivenessSnapshot >= 0 ? _currentLegendEffectivenessSnapshot : (useLegendEffectivenessOverride ? legendEffectivenessOverride : (StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f)));
                            float mult = 1f + (leSnap / 100f);
                            GameLoggingSystem.Instance.LogEvent($"Resource(global): {effectiveValue}→{modifierValue} (LE {leSnap}%, ×{mult:F2}) by {sourceName}", "GovernmentLogic");
                            var allResources = GameUnitsLogic.Instance?.GetAvailableResources() ?? new List<GameResourceSlot>();
                            
                            // Apply to existing resources
                            foreach (var resource in allResources)
                            {
                                if (resource != null && resource.gameUnit != null)
                                {
                                    if (legendBonus.modifierType == ModifierType.Percentage)
                                    {
                                        GlobalProductionManager.Instance.AdjustPercentageModifier(resource.gameUnit.name, Mathf.Abs(modifierValue), isPositive, true, sourceName);
                                    }
                                    else // Add modifier - use regular resource modifier method
                                    {
                                        GlobalProductionManager.Instance.AdjustResourceModifier(resource.gameUnit.name, modifierValue, isPositive, true, sourceName);
                                    }
                                }
                            }
                            
                            // Store as pending global bonus for future resources
                            var pendingBonus = new PendingLegendBonus(sourceName, legendBonus.bonusType, legendBonus.targetStat, modifierValue, legendBonus.modifierType, legendBonus.scope, isPositive);
                            pendingGlobalBonuses.Add(pendingBonus);
                            
                            GameLoggingSystem.Instance.LogEvent($"Stored global ResourceModifier as pending bonus for future resources", "GovernmentLogic");
                        }
                        else if (legendBonus.scope == ScopeType.Section && !string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific section
                            int leSnap = Mathf.RoundToInt(_currentLegendEffectivenessSnapshot >= 0 ? _currentLegendEffectivenessSnapshot : (useLegendEffectivenessOverride ? legendEffectivenessOverride : (StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f)));
                            float mult = 1f + (leSnap / 100f);
                            GameLoggingSystem.Instance.LogEvent($"Resource(section {legendBonus.targetStat}): {effectiveValue}→{modifierValue} (LE {leSnap}%, ×{mult:F2}) by {sourceName}", "GovernmentLogic");
                            if (legendBonus.modifierType == ModifierType.Percentage)
                            {
                                GlobalProductionManager.Instance.AdjustPercentageModifierForSection(legendBonus.targetStat, Mathf.Abs(modifierValue), isPositive, true, sourceName);
                            }
                            else // Add modifier for section - need to implement section-wide add modifier
                            {
                                var allResources = GameUnitsLogic.Instance?.GetAvailableResources() ?? new List<GameResourceSlot>();
                                foreach (var resource in allResources)
                                {
                                    if (resource != null && resource.gameUnit != null && string.Equals(resource.gameUnit.section, legendBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        GlobalProductionManager.Instance.AdjustResourceModifier(resource.gameUnit.name, modifierValue, isPositive, true, sourceName);
                                    }
                                }
                            }
                            
                            // Store as pending section bonus for future resources
                            if (!pendingSectionBonuses.ContainsKey(legendBonus.targetStat))
                            {
                                pendingSectionBonuses[legendBonus.targetStat] = new List<PendingLegendBonus>();
                            }
                            var pendingBonus = new PendingLegendBonus(sourceName, legendBonus.bonusType, legendBonus.targetStat, modifierValue, legendBonus.modifierType, legendBonus.scope, isPositive);
                            pendingSectionBonuses[legendBonus.targetStat].Add(pendingBonus);
                            
                            GameLoggingSystem.Instance.LogEvent($"Stored section ResourceModifier as pending bonus for future {legendBonus.targetStat} resources", "GovernmentLogic");
                        }
                        else if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific resource
                            int leSnap = Mathf.RoundToInt(_currentLegendEffectivenessSnapshot >= 0 ? _currentLegendEffectivenessSnapshot : (useLegendEffectivenessOverride ? legendEffectivenessOverride : (StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f)));
                            float mult = 1f + (leSnap / 100f);
                            GameLoggingSystem.Instance.LogEvent($"Resource({legendBonus.targetStat}): {effectiveValue}→{modifierValue} (LE {leSnap}%, ×{mult:F2}) by {sourceName}", "GovernmentLogic");
                            bool resourceExists = false;
                            
                            if (legendBonus.modifierType == ModifierType.Percentage)
                            {
                                GlobalProductionManager.Instance.AdjustPercentageModifier(legendBonus.targetStat, Mathf.Abs(modifierValue), isPositive, true, sourceName);
                                resourceExists = true; // We can apply percentage modifiers even if resource doesn't exist yet
                            }
                            else // Add modifier
                            {
                                // Check if resource exists before applying flat modifier
                                var allResources = GameUnitsLogic.Instance?.GetAvailableResources() ?? new List<GameResourceSlot>();
                                var targetResource = allResources.FirstOrDefault(r => r != null && r.gameUnit != null && string.Equals(r.gameUnit.name, legendBonus.targetStat, System.StringComparison.OrdinalIgnoreCase));
                                
                                if (targetResource != null)
                                {
                                    GlobalProductionManager.Instance.AdjustResourceModifier(legendBonus.targetStat, modifierValue, isPositive, true, sourceName);
                                    resourceExists = true;
                                }
                            }
                            
                            // Store as pending resource bonus if resource doesn't exist yet (for flat modifiers)
                            if (!resourceExists || legendBonus.modifierType != ModifierType.Percentage)
                            {
                                if (!pendingResourceBonuses.ContainsKey(legendBonus.targetStat))
                                {
                                    pendingResourceBonuses[legendBonus.targetStat] = new List<PendingLegendBonus>();
                                }
                                var pendingBonus = new PendingLegendBonus(sourceName, legendBonus.bonusType, legendBonus.targetStat, modifierValue, legendBonus.modifierType, legendBonus.scope, isPositive);
                                pendingResourceBonuses[legendBonus.targetStat].Add(pendingBonus);
                                
                                GameLoggingSystem.Instance.LogEvent($"Stored individual ResourceModifier as pending bonus for future {legendBonus.targetStat} resource", "GovernmentLogic");
                            }
                        }
                    }
                    break;
                    
                case GameEffectType.ProductionModifier:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float modifierValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        
                        if (legendBonus.scope == ScopeType.Global || string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to all production units (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionModifier: all production units {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Building", modifierValue, true, sourceName);
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Unit", modifierValue, true, sourceName);
                        }
                        else if (legendBonus.scope == ScopeType.Section && !string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific section
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionModifier: {legendBonus.targetStat} section {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustProductionModifierBySection(legendBonus.targetStat, modifierValue, true, sourceName);
                        }
                        else if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific production unit type
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionModifier: {legendBonus.targetStat} type {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustProductionModifierByType(legendBonus.targetStat, modifierValue, true, sourceName);
                        }
                    }
                    break;
                    
                case GameEffectType.ClickPowerBonus:
                    if (GameUnitsLogic.Instance != null)
                    {
                        // Validate target stat if specified
                        if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            ValidateTargetStat(legendBonus.targetStat, "ClickPowerBonus", sourceName);
                        }
                        
                        float modifierValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        
                        if (legendBonus.scope == ScopeType.Global || string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to all resources (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ClickPowerBonus: all resources {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                            foreach (var resource in GameUnitsLogic.Instance.GetAvailableResources())
                            {
                                if (resource != null && resource.gameUnit != null)
                                {
                                    GameUnitsLogic.Instance.AdjustClickPower(resource.gameUnit.name, modifierValue);
                                }
                            }
                        }
                        else if (legendBonus.scope == ScopeType.Section && !string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific section
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ClickPowerBonus: {legendBonus.targetStat} section {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustClickPowerForSection(legendBonus.targetStat, modifierValue);
                        }
                        else if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific resource
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ClickPowerBonus: {legendBonus.targetStat} {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustClickPower(legendBonus.targetStat, modifierValue);
                        }
                    }
                    break;
                    
                case GameEffectType.MaxMoraleModifier:
                    GameLoggingSystem.Instance.LogEvent($"MaxMoraleModifier case triggered for '{sourceName}' with value {legendBonus.modifierValue} {legendBonus.modifierType}", "GovernmentLogic");
                    if (StatManager.Instance != null)
                    {
                        float baseMaxMorale = StatManager.Instance.GetBaseGlobalValue("maxmorale");
                        GameLoggingSystem.Instance.LogEvent($"Retrieved base max morale: {baseMaxMorale}", "GovernmentLogic");
                        float modifierValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, baseMaxMorale);
                        GameLoggingSystem.Instance.LogEvent($"Calculated modifier value: {modifierValue}", "GovernmentLogic");
                        GameLoggingSystem.Instance.LogEvent($"Applying Legend MaxMoraleModifier: +{modifierValue} ({effectiveValue} {legendBonus.modifierType} of base {baseMaxMorale}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("maxmorale", Mathf.RoundToInt(modifierValue), sourceName);
                        GameLoggingSystem.Instance.LogEvent($"AddGlobalBonus call completed for maxMorale", "GovernmentLogic");
                    }
                    else
                    {
                        Debug.LogError($"[GovernmentLogic]StatManager.Instance is null when trying to apply MaxMoraleModifier!");
                    }
                    break;
                    
                case GameEffectType.MoraleBalanceModifier:
                    if (StatManager.Instance != null)
                    {
                        float enhancedValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        int modifierValue = Mathf.RoundToInt(enhancedValue);
                        GameLoggingSystem.Instance.LogEvent($"Applying Legend MoraleBalanceModifier: {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("moraleBalance", modifierValue, sourceName);
                    }
                    break;
                    
                case GameEffectType.SatisfactionThresholdModifier:
                    GameLoggingSystem.Instance.LogEvent($"SatisfactionThresholdModifier case triggered for '{sourceName}' with value {legendBonus.modifierValue} {legendBonus.modifierType}", "GovernmentLogic");
                    if (StatManager.Instance != null)
                    {
                        // Get the base satisfaction upgrade threshold
                        float baseSatisfactionThreshold = StatManager.Instance.GetBaseGlobalValue("satisfactionupgradethreshold");
                        GameLoggingSystem.Instance.LogEvent($"Retrieved base satisfaction threshold: {baseSatisfactionThreshold}", "GovernmentLogic");
                        
                        // Calculate modifier value - for percentage, reduce the threshold
                        float modifierValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, baseSatisfactionThreshold);
                        GameLoggingSystem.Instance.LogEvent($"Calculated modifier value: {modifierValue}", "GovernmentLogic");
                        
                        // Make it easier to upgrade by reducing the threshold (negative value)
                        int finalModifier = -Mathf.RoundToInt(Mathf.Abs(modifierValue));
                        GameLoggingSystem.Instance.LogEvent($"Final modifier (negative for easier upgrades): {finalModifier}", "GovernmentLogic");
                        
                        GameLoggingSystem.Instance.LogEvent($"Applying Legend SatisfactionThresholdModifier: {finalModifier} ({effectiveValue} {legendBonus.modifierType} of base {baseSatisfactionThreshold}, easier upgrades) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("satisfactionupgradethreshold", finalModifier, sourceName);
                        GameLoggingSystem.Instance.LogEvent($"AddGlobalBonus call completed for satisfactionUpgradeThreshold", "GovernmentLogic");
                    }
                    else
                    {
                        Debug.LogError($"[GovernmentLogic] StatManager.Instance is null when trying to apply SatisfactionThresholdModifier!");
                    }
                    break;
                    
                case GameEffectType.HousingBonus:
                    if (PopGrowthLogic.Instance != null)
                    {
                        // For housing, we can use a base housing value assumption for percentage calculations
                        float baseHousing = 100f; // Assumed base housing capacity for percentage calculations
                        float bonusValue = CalculateLegendStatModifierValue(effectiveValue, legendBonus.modifierType, baseHousing);
                        GameLoggingSystem.Instance.LogEvent($"Applying Legend HousingBonus: +{bonusValue} ({effectiveValue} {legendBonus.modifierType} of base {baseHousing}) housing from SOURCE: '{sourceName}'", "GovernmentLogic");
                        PopGrowthLogic.Instance.AddHousingBonus(Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case GameEffectType.ConstructionCostModifier:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float modifierValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        
                        if (legendBonus.scope == ScopeType.Global || string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to all building types (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ConstructionCostModifier: all buildings {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierByType("Building", modifierValue, true, sourceName);
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierByType("Unit", modifierValue, true, sourceName);
                        }
                        else if (legendBonus.scope == ScopeType.Section && !string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific section
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ConstructionCostModifier: {legendBonus.targetStat} section {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierBySection(legendBonus.targetStat, modifierValue, true, sourceName);
                        }
                        else if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific building type
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ConstructionCostModifier: {legendBonus.targetStat} type {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierByType(legendBonus.targetStat, modifierValue, true, sourceName);
                        }
                    }
                    break;
                    
                case GameEffectType.ProductionScalingBonus:
                    if (GameUnitsLogic.Instance != null)
                    {
                        // Validate target stat and condition stat if specified
                        if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            ValidateTargetStat(legendBonus.targetStat, "ProductionScalingBonus", sourceName);
                        }
                        if (!string.IsNullOrEmpty(legendBonus.conditionStat))
                        {
                            ValidateTargetStat(legendBonus.conditionStat, "ProductionScalingBonus", sourceName, true);
                        }
                        
                        float bonusValue = CalculateLegendModifierValue(effectiveValue, legendBonus.modifierType);
                        
                        if (legendBonus.scope == ScopeType.Global || string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to all production units (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionScalingBonus: all production units +{bonusValue} per unit from SOURCE: '{sourceName}'", "GovernmentLogic");
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null)
                                {
                                    GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnit.name, bonusValue, sourceName);
                                }
                            }
                        }
                        else if (legendBonus.scope == ScopeType.Section && !string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific section
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionScalingBonus: {legendBonus.targetStat} section +{bonusValue} per unit from SOURCE: '{sourceName}'", "GovernmentLogic");
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null && 
                                    string.Equals(productionUnit.section, legendBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnit.name, bonusValue, sourceName);
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(legendBonus.targetStat))
                        {
                            // Apply to specific production unit
                            GameLoggingSystem.Instance.LogEvent($"Applying Legend ProductionScalingBonus: {legendBonus.targetStat} +{bonusValue} per unit from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AddProductionScalingBonus(legendBonus.targetStat, bonusValue, sourceName);
                        }
                    }
                    break;
                    
                case GameEffectType.SpecialAbility:
                    GameLoggingSystem.Instance.LogEvent($"Legend SpecialAbility effects not implemented yet for {sourceName}", "GovernmentLogic");
                    break;
                    
                default:
                    Debug.LogWarning($"[GovernmentLogic] Unknown legend bonus type: {legendBonus.bonusType} for {sourceName}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error applying legend bonus for {sourceName}: {e.Message}\n{e.StackTrace}");
        }
        
        // Minimal end logging omitted
    }
    
    /// <summary>
    /// Apply a single seat bonus to the game systems
    /// </summary>
    private void ApplySeatBonus(SeatBonus seatBonus, LegendData legend)
    {
        if (seatBonus == null)
        {
            Debug.LogWarning("[GovernmentLogic] ApplySeatBonus: SeatBonus is null");
            return;
        }
        
        if (legend == null)
        {
            Debug.LogWarning("[GovernmentLogic] ApplySeatBonus: Legend is null");
            return;
        }
        
        string sourceName = $"Council Seat: {legend.legendName}";
        
        // Keep logs concise; detailed summaries logged around pipeline
        
        try
        {
            switch (seatBonus.bonusType)
            {
                case SeatBonusType.PillarBonus:
                    if (string.IsNullOrEmpty(seatBonus.targetStat))
                    {
                        Debug.LogWarning($"[GovernmentLogic] PillarBonus has empty targetStat for {sourceName}");
                        break;
                    }
                    
                    // Validate target stat
                    ValidateTargetStat(seatBonus.targetStat, "PillarBonus", sourceName);
                    
                    if (StatManager.Instance == null)
                    {
                        Debug.LogError($"[GovernmentLogic] StatManager.Instance is null for PillarBonus {sourceName}");
                        break;
                    }
                    
                    // Get base value for proper percentage calculation
                    float basePillarValue = StatManager.Instance.GetBasePillarValue(seatBonus.targetStat);
                    float pillarBonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, basePillarValue);
                    GameLoggingSystem.Instance.LogEvent($"Seat Pillar: {seatBonus.targetStat} +{pillarBonusValue} by {sourceName}", "GovernmentLogic");
                    StatManager.Instance.AddPillarBonus(seatBonus.targetStat, pillarBonusValue, sourceName);
                    
                    break;
                    
                case SeatBonusType.SubstatBonus:
                    if (!string.IsNullOrEmpty(seatBonus.targetStat) && StatManager.Instance != null)
                    {
                        // Validate target stat
                        ValidateTargetStat(seatBonus.targetStat, "SubstatBonus", sourceName);
                        
                        // Get base value for proper percentage calculation
                        float baseSubstatValue = StatManager.Instance.GetBaseSubstatValue(seatBonus.targetStat);
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseSubstatValue);
                        GameLoggingSystem.Instance.LogEvent($"Seat Substat: {seatBonus.targetStat} +{bonusValue} by {sourceName}", "GovernmentLogic");
                        StatManager.Instance.AddSubstatBonus(seatBonus.targetStat, bonusValue, sourceName);
                    }
                    break;
                    
                case SeatBonusType.DerivedStatBonus:
                    if (!string.IsNullOrEmpty(seatBonus.targetStat) && StatManager.Instance != null)
                    {
                        // Validate target stat
                        ValidateTargetStat(seatBonus.targetStat, "DerivedStatBonus", sourceName);
                        
                        // Get base value for proper percentage calculation
                        float baseDerivedValue = StatManager.Instance.GetBaseDerivedValue(seatBonus.targetStat);
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseDerivedValue);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat DerivedStatBonus: {seatBonus.targetStat} +{bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseDerivedValue}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddDerivedStatBonus(seatBonus.targetStat, bonusValue, sourceName);
                    }
                    break;
                    
                case SeatBonusType.ResourceModifier:
                    if (GlobalProductionManager.Instance != null)
                    {
                        // Validate target stat if specified
                        if (!string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            ValidateTargetStat(seatBonus.targetStat, "ResourceModifier", sourceName);
                        }
                        
                        float modifierValue = CalculateModifierValue(seatBonus.modifierValue, seatBonus.modifierType);
                        bool isPositive = modifierValue > 0;
                        
                        if (string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            // Apply to all resources (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ResourceModifier: all resources {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            var allResources = GameUnitsLogic.Instance?.GetAvailableResources() ?? new List<GameResourceSlot>();
                            foreach (var resource in allResources)
                            {
                                if (resource != null && resource.gameUnit != null)
                                {
                                    if (seatBonus.modifierType == ModifierType.Percentage)
                                    {
                                        GlobalProductionManager.Instance.AdjustPercentageModifier(resource.gameUnit.name, Mathf.Abs(modifierValue), isPositive, true, sourceName);
                                    }
                                    else // Add modifier - use regular resource modifier method
                                    {
                                        GlobalProductionManager.Instance.AdjustResourceModifier(resource.gameUnit.name, modifierValue, isPositive, true, sourceName);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Apply to specific resource
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ResourceModifier: {seatBonus.targetStat} {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            if (seatBonus.modifierType == ModifierType.Percentage)
                            {
                                GlobalProductionManager.Instance.AdjustPercentageModifier(seatBonus.targetStat, Mathf.Abs(modifierValue), isPositive, true, sourceName);
                            }
                            else // Add modifier
                            {
                                GlobalProductionManager.Instance.AdjustResourceModifier(seatBonus.targetStat, modifierValue, isPositive, true, sourceName);
                            }
                        }
                    }
                    break;
                    
                case SeatBonusType.ProductionModifier:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float modifierValue = CalculateModifierValue(seatBonus.modifierValue, seatBonus.modifierType);
                        
                        if (string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            // Apply to all production units (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ProductionModifier: all production units {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Building", modifierValue, true, sourceName);
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Unit", modifierValue, true, sourceName);
                        }
                        else
                        {
                            // Check if it's a section name or specific type
                            // First try as section, then as type
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ProductionModifier: {seatBonus.targetStat} {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            
                            // Try section first
                            bool appliedAsSection = false;
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null && 
                                    string.Equals(productionUnit.section, seatBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    GameUnitsLogic.Instance.AdjustProductionModifierByType(productionUnit.type, modifierValue, true, sourceName);
                                    appliedAsSection = true;
                                }
                            }
                            
                            // If not applied as section, apply as type
                            if (!appliedAsSection)
                            {
                                GameUnitsLogic.Instance.AdjustProductionModifierByType(seatBonus.targetStat, modifierValue, true, sourceName);
                            }
                        }
                    }
                    break;
                    
                case SeatBonusType.ClickPowerBonus:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float modifierValue = CalculateModifierValue(seatBonus.modifierValue, seatBonus.modifierType);
                        
                        if (string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            // Apply to all resources (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ClickPowerBonus: all resources {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                            foreach (var resourceSlot in GameUnitsLogic.Instance.GetAvailableResources())
                            {
                                if (resourceSlot != null && resourceSlot.gameUnit != null)
                                {
                                    GameUnitsLogic.Instance.AdjustClickPowerPercent(resourceSlot.gameUnit.name, modifierValue);
                                }
                            }
                        }
                        else
                        {
                            // Check if it's a section name or specific resource
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ClickPowerBonus: {seatBonus.targetStat} {modifierValue} from SOURCE: '{sourceName}'", "GovernmentLogic");
                            
                            // Try section first
                            bool appliedAsSection = false;
                            var allResources = GameUnitsLogic.Instance.GetAvailableResources();
                            foreach (var resource in allResources)
                            {
                                if (resource != null && resource.gameUnit != null && 
                                    string.Equals(resource.gameUnit.section, seatBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    GameUnitsLogic.Instance.AdjustClickPowerPercent(resource.gameUnit.name, modifierValue);
                                    appliedAsSection = true;
                                }
                            }
                            
                            // If not applied as section, apply as individual resource
                            if (!appliedAsSection)
                            {
                                GameUnitsLogic.Instance.AdjustClickPowerPercent(seatBonus.targetStat, modifierValue);
                            }
                        }
                    }
                    break;
                    
                case SeatBonusType.MaxMoraleModifier:
                    if (StatManager.Instance != null)
                    {
                        // Get base value for proper percentage calculation
                        float baseMaxMorale = StatManager.Instance.GetBaseGlobalValue("maxmorale");
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseMaxMorale);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat MaxMoraleModifier: +{bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseMaxMorale}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("maxMorale", Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case SeatBonusType.MoraleModifier:
                    if (StatManager.Instance != null)
                    {
                        // Get base value for proper percentage calculation
                        float baseMorale = StatManager.Instance.GetBaseGlobalValue("morale");
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseMorale);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat MoraleModifier: +{bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseMorale}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("morale", Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case SeatBonusType.MoraleBalanceModifier:
                    if (StatManager.Instance != null)
                    {
                        // Get base value for proper percentage calculation
                        float baseMoraleBalance = StatManager.Instance.GetBaseGlobalValue("moraleBalance");
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseMoraleBalance);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat MoraleBalanceModifier: +{bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseMoraleBalance}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("moraleBalance", Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case SeatBonusType.SatisfactionThresholdModifier:
                    if (StatManager.Instance != null)
                    {
                        // Get base value for proper percentage calculation
                        float baseThreshold = StatManager.Instance.GetBaseGlobalValue("satisfactionupgradethreshold");
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseThreshold);
                        // Make the value negative to make upgrades easier (reduce threshold)
                        bonusValue = -Mathf.Abs(bonusValue);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat SatisfactionThresholdModifier: {bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseThreshold}, made negative for easier upgrades) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        StatManager.Instance.AddGlobalBonus("satisfactionupgradethreshold", Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case SeatBonusType.HousingBonus:
                    if (PopGrowthLogic.Instance != null)
                    {
                        // For housing, we can use a base housing value assumption or treat as a simple modifier
                        // Since housing bonuses are typically additive, but percentages could apply to base housing capacity
                        float baseHousing = 100f; // Assumed base housing capacity for percentage calculations
                        float bonusValue = CalculateStatModifierValue(seatBonus.modifierValue, seatBonus.modifierType, baseHousing);
                        GameLoggingSystem.Instance.LogEvent($"Applying Seat HousingBonus: +{bonusValue} ({seatBonus.modifierValue} {seatBonus.modifierType} of base {baseHousing}) from SOURCE: '{sourceName}'", "GovernmentLogic");
                        PopGrowthLogic.Instance.AddHousingBonus(Mathf.RoundToInt(bonusValue), sourceName);
                    }
                    break;
                    
                case SeatBonusType.ConstructionCostModifier:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float modifierValue = CalculateModifierValue(seatBonus.modifierValue, seatBonus.modifierType);
                        
                        if (string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            // Apply to all building types (global bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ConstructionCostModifier: all buildings {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierByType("Building", modifierValue, true, sourceName);
                            GameUnitsLogic.Instance.AdjustConstructionCostModifierByType("Unit", modifierValue, true, sourceName);
                        }
                        else
                        {
                            // Check if it's a section name or specific building type
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ConstructionCostModifier: {seatBonus.targetStat} {modifierValue}% from SOURCE: '{sourceName}'", "GovernmentLogic");
                            
                            // Try section first
                            bool appliedAsSection = false;
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null && 
                                    string.Equals(productionUnit.section, seatBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    GameUnitsLogic.Instance.AdjustConstructionCostModifierByType(productionUnit.type, modifierValue, true, sourceName);
                                    appliedAsSection = true;
                                }
                            }
                            
                            // If not applied as section, apply as building type
                            if (!appliedAsSection)
                            {
                                GameUnitsLogic.Instance.AdjustConstructionCostModifierByType(seatBonus.targetStat, modifierValue, true, sourceName);
                            }
                        }
                    }
                    break;
                    
                case SeatBonusType.ProductionScalingBonus:
                    if (GameUnitsLogic.Instance != null)
                    {
                        float bonusValue = CalculateModifierValue(seatBonus.modifierValue, seatBonus.modifierType);
                        
                        if (string.IsNullOrEmpty(seatBonus.targetStat))
                        {
                            // Apply to all production units (global scaling bonus)
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ProductionScalingBonus: all production units +{bonusValue} per unit from SOURCE: '{sourceName}'", "GovernmentLogic");
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null)
                                {
                                    GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnit.name, bonusValue, sourceName);
                                }
                            }
                        }
                        else
                        {
                            // Check if it's a section name or specific production unit
                            GameLoggingSystem.Instance.LogEvent($"Applying Seat ProductionScalingBonus: {seatBonus.targetStat} +{bonusValue} per unit from SOURCE: '{sourceName}'", "GovernmentLogic");
                            
                            // Try section first
                            bool appliedAsSection = false;
                            var allProductionUnits = GameUnitsLogic.Instance.GetAvailableProductionUnits();
                            foreach (var productionUnit in allProductionUnits)
                            {
                                if (productionUnit != null && 
                                    string.Equals(productionUnit.section, seatBonus.targetStat, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnit.name, bonusValue, sourceName);
                                    appliedAsSection = true;
                                }
                            }
                            
                            // If not applied as section, apply as individual production unit
                            if (!appliedAsSection)
                            {
                                GameUnitsLogic.Instance.AddProductionScalingBonus(seatBonus.targetStat, bonusValue, sourceName);
                            }
                        }
                    }
                    break;
                    
                case SeatBonusType.CivicBonus:
                    // Civic bonuses are handled separately through the civic system
                    GameLoggingSystem.Instance.LogEvent($"Civic bonus from seat {seatBonus.GetAutoDescription()} - handled by civic system", "GovernmentLogic");
                    break;
                    
                default:
                    Debug.LogWarning($"[GovernmentLogic] Unknown seat bonus type: {seatBonus.bonusType}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error applying seat bonus {seatBonus.bonusType}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Remove all seat bonuses for a specific legend
    /// </summary>
    public void RemoveSeatBonuses(LegendData legend)
    {
        if (legend == null) return;
        
        string seatSourceName = $"Council Seat: {legend.legendName}";
        string legendSourceName = $"Legend: {legend.legendName}";
        
        try
        {
            // Remove all seat bonuses from this source
            if (StatManager.Instance != null)
            {
                StatManager.Instance.ClearBonusesFromSource(seatSourceName);
                StatManager.Instance.ClearBonusesFromSource(legendSourceName);
            }
            
            if (PopGrowthLogic.Instance != null)
            {
                PopGrowthLogic.Instance.ClearHousingBonusesFromSource(seatSourceName);
                PopGrowthLogic.Instance.ClearHousingBonusesFromSource(legendSourceName);
            }
            
            if (GameUnitsLogic.Instance != null)
            {
                GameUnitsLogic.Instance.ClearProductionScalingBonusesFromSource(seatSourceName);
                GameUnitsLogic.Instance.ClearProductionScalingBonusesFromSource(legendSourceName);
            }
            
            if (GlobalProductionManager.Instance != null)
            {
                // Remove ALL resource modifiers registered under these sources
                GlobalProductionManager.Instance.ClearAllModifiersFromSource(seatSourceName);
                GlobalProductionManager.Instance.ClearAllModifiersFromSource(legendSourceName);
            }

            // Also clear any pending bonuses queued for newly added resources from these sources
            ClearPendingBonusesFromSource(seatSourceName);
            ClearPendingBonusesFromSource(legendSourceName);
            
            GameLoggingSystem.Instance.LogEvent($"Removed all seat and legend bonuses for '{seatSourceName}' and '{legendSourceName}' (legend: {legend.legendName})", "GovernmentLogic");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error removing seat and legend bonuses for {legend.legendName}: {e.Message}");
        }
    }

    /// <summary>
    /// Remove any pending resource/section/global legend bonuses queued from a specific source name.
    /// Prevents duplication across centralized recalculations.
    /// </summary>
    private void ClearPendingBonusesFromSource(string sourceName)
    {
        // Global pending
        if (pendingGlobalBonuses != null && pendingGlobalBonuses.Count > 0)
        {
            pendingGlobalBonuses.RemoveAll(pb => pb.legendSource == sourceName);
        }
        
        // Section pending
        if (pendingSectionBonuses != null && pendingSectionBonuses.Count > 0)
        {
            var sectionKeys = new List<string>(pendingSectionBonuses.Keys);
            foreach (var key in sectionKeys)
            {
                var list = pendingSectionBonuses[key];
                if (list != null)
                {
                    list.RemoveAll(pb => pb.legendSource == sourceName);
                    if (list.Count == 0)
                    {
                        pendingSectionBonuses.Remove(key);
                    }
                }
            }
        }
        
        // Resource pending
        if (pendingResourceBonuses != null && pendingResourceBonuses.Count > 0)
        {
            var resourceKeys = new List<string>(pendingResourceBonuses.Keys);
            foreach (var key in resourceKeys)
            {
                var list = pendingResourceBonuses[key];
                if (list != null)
                {
                    list.RemoveAll(pb => pb.legendSource == sourceName);
                    if (list.Count == 0)
                    {
                        pendingResourceBonuses.Remove(key);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Calculate the final modifier value based on modifier type
    /// </summary>
    /// <summary>
    /// Calculate modifier value for non-stat bonuses (resources, production, etc.)
    /// These don't need base values since they work differently
    /// </summary>
    private float CalculateModifierValue(float modifierValue, ModifierType modifierType)
    {
        switch (modifierType)
        {
            case ModifierType.Add:
                return modifierValue; // Additive values are used directly
                
            case ModifierType.Percentage:
                return modifierValue; // Percentage values are handled by the target system
                
            case ModifierType.SetValue:
                return modifierValue; // Set values are used directly
                
            default:
                Debug.LogWarning($"[GovernmentLogic] Unknown modifier type: {modifierType}");
                return modifierValue;
        }
    }
    
    /// <summary>
    /// Calculate modifier value for stat bonuses (pillars, substats, derived stats)
    /// These need base values for proper percentage calculation
    /// </summary>
    private float CalculateStatModifierValue(float modifierValue, ModifierType modifierType, float baseValue)
    {
        switch (modifierType)
        {
            case ModifierType.Add:
                return modifierValue; // Additive values are used directly
                
            case ModifierType.Percentage:
                // Calculate percentage of base value and round to nearest integer
                float percentageBonus = (modifierValue / 100f) * baseValue;
                return Mathf.Round(percentageBonus);
                
            case ModifierType.SetValue:
                // Set value replaces the base entirely
                return modifierValue - baseValue; // Return the difference to add to base
                
            default:
                Debug.LogWarning($"[GovernmentLogic] Unknown modifier type: {modifierType}");
                return modifierValue;
        }
    }
    
    /// <summary>
    /// Calculate modifier value for legend bonuses with Legend Effectiveness enhancement
    /// Legend Effectiveness enhances ALL legend bonuses based on Authority-derived effectiveness
    /// </summary>
    private float CalculateLegendModifierValue(float modifierValue, ModifierType modifierType)
    {
        // Determine which Legend Effectiveness value to use
        float legendEffectiveness;
        if (_currentLegendEffectivenessSnapshot >= 0f)
        {
            legendEffectiveness = _currentLegendEffectivenessSnapshot;
            GameLoggingSystem.Instance.LogEvent($"Using LE snapshot: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        else if (useLegendEffectivenessOverride)
        {
            legendEffectiveness = legendEffectivenessOverride;
            GameLoggingSystem.Instance.LogEvent($"Using LE override: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        else
        {
            legendEffectiveness = StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f;
            GameLoggingSystem.Instance.LogEvent($"Using live LE: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        
        // Round to get percentage bonus (e.g., 53.99 -> 54%)
        int effectivenessPercent = Mathf.RoundToInt(legendEffectiveness);
        effectivenessPercent = Mathf.Max(0, effectivenessPercent); // Only positive bonuses
        
        // Calculate enhancement multiplier (e.g., 54% -> 1.54x)
        float enhancementMultiplier = 1f + (effectivenessPercent / 100f);
        
        // Apply enhancement to the modifier value
        float enhancedValue = modifierValue * enhancementMultiplier;
        
        if (effectivenessPercent > 0)
        {
            GameLoggingSystem.Instance.LogEvent($"Legend Effectiveness applied: {modifierValue} × {enhancementMultiplier:F3} (LE: {legendEffectiveness:F2} = +{effectivenessPercent}%) = {enhancedValue:F2}", "GovernmentLogic");
        }
        
        switch (modifierType)
        {
            case ModifierType.Add:
                return enhancedValue; // Enhanced additive values
                
            case ModifierType.Percentage:
                return enhancedValue; // Enhanced percentage values
                
            case ModifierType.SetValue:
                return enhancedValue; // Enhanced set values
                
            default:
                Debug.LogWarning($"[GovernmentLogic] Unknown modifier type: {modifierType}");
                return enhancedValue;
        }
    }
    
    /// <summary>
    /// Calculate stat modifier value for legend bonuses with Legend Effectiveness enhancement
    /// These need base values for proper percentage calculation
    /// </summary>
    private float CalculateLegendStatModifierValue(float modifierValue, ModifierType modifierType, float baseValue)
    {
        // Determine which Legend Effectiveness value to use
        float legendEffectiveness;
        if (_currentLegendEffectivenessSnapshot >= 0f)
        {
            legendEffectiveness = _currentLegendEffectivenessSnapshot;
            GameLoggingSystem.Instance.LogEvent($"Using LE snapshot: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        else if (useLegendEffectivenessOverride)
        {
            legendEffectiveness = legendEffectivenessOverride;
            GameLoggingSystem.Instance.LogEvent($"Using LE override: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        else
        {
            legendEffectiveness = StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("legendEffectiveness") : 0f;
            GameLoggingSystem.Instance.LogEvent($"Using live LE: {legendEffectiveness:F5}", "GovernmentLogic");
        }
        
        // Round to get percentage bonus (e.g., 53.99 -> 54%)
        int effectivenessPercent = Mathf.RoundToInt(legendEffectiveness);
        effectivenessPercent = Mathf.Max(0, effectivenessPercent); // Only positive bonuses
        
        // Calculate enhancement multiplier (e.g., 54% -> 1.54x)
        float enhancementMultiplier = 1f + (effectivenessPercent / 100f);
        
        // Apply enhancement to the modifier value
        float enhancedValue = modifierValue * enhancementMultiplier;
        
        if (effectivenessPercent > 0)
        {
            GameLoggingSystem.Instance.LogEvent($"Legend Effectiveness applied: {modifierValue} × {enhancementMultiplier:F3} (LE: {legendEffectiveness:F2} = +{effectivenessPercent}%) = {enhancedValue:F2}", "GovernmentLogic");
        }
        
        switch (modifierType)
        {
            case ModifierType.Add:
                return enhancedValue; // Enhanced additive values
                
            case ModifierType.Percentage:
                // Calculate percentage of base value with enhancement
                float percentageBonus = (enhancedValue / 100f) * baseValue;
                return Mathf.Round(percentageBonus);
                
            case ModifierType.SetValue:
                // Enhanced set value replaces the base entirely
                return enhancedValue - baseValue; // Return the difference to add to base
                
            default:
                Debug.LogWarning($"[GovernmentLogic] Unknown modifier type: {modifierType}");
                return enhancedValue;
        }
    }
    
    /// <summary>
    /// Calculate modifier value without Legend Effectiveness enhancement.
    /// Used to prevent recursive enhancement when applying bonuses to Legend Effectiveness itself.
    /// </summary>
    private float CalculateDirectModifierValue(float modifierValue, ModifierType modifierType, float baseValue)
    {
        switch (modifierType)
        {
            case ModifierType.Add:
                return modifierValue; // Direct additive values
                
            case ModifierType.Percentage:
                // Calculate percentage of base value without enhancement
                float percentageBonus = (modifierValue / 100f) * baseValue;
                return Mathf.Round(percentageBonus);
                
            case ModifierType.SetValue:
                // Direct set value replaces the base entirely
                return modifierValue - baseValue; // Return the difference to add to base
                
            default:
                Debug.LogWarning($"[GovernmentLogic] Unknown modifier type: {modifierType}");
                return modifierValue;
        }
    }
    
    // ===== DYNAMIC VALIDATION SYSTEM =====
    
    // Cache for loaded validation data
    private static HashSet<string> validResourceNames = null;
    private static HashSet<string> validSectionNames = null;
    private static HashSet<string> validProductionUnitNames = null;
    private static HashSet<string> validPillarNames = null;
    private static HashSet<string> validSubstatNames = null;
    private static HashSet<string> validDerivedStatNames = null;
    
    /// <summary>
    /// Load all valid resource names from the file system
    /// </summary>
    private static void LoadValidResourceNames()
    {
        if (validResourceNames != null) return; // Already loaded
        
        validResourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Load all ResourceSO assets from anywhere in the Resources folders
            var allResources = Resources.LoadAll<ResourceSO>("");
            foreach (var resource in allResources)
            {
                if (resource != null && !string.IsNullOrEmpty(resource.name))
                {
                    validResourceNames.Add(resource.name);
                }
            }
            
            GameLoggingSystem.Instance.LogEvent($"Loaded {validResourceNames.Count} valid resource names: {string.Join(", ", validResourceNames)}", "GovernmentLogic");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error loading valid resource names: {e.Message}");
            validResourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Empty set as fallback
        }
    }
    
    /// <summary>
    /// Load all valid section names from the file system
    /// </summary>
    private static void LoadValidSectionNames()
    {
        if (validSectionNames != null) return; // Already loaded
        
        validSectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Load sections from Assets/Resources/Sections
            var sections = Resources.LoadAll<SectionData>("Sections");
            foreach (var section in sections)
            {
                if (section != null && !string.IsNullOrEmpty(section.name))
                {
                    validSectionNames.Add(section.name);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error loading valid section names: {e.Message}");
            validSectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Empty set as fallback
        }
        
        GameLoggingSystem.Instance.LogEvent($"Loaded {validSectionNames.Count} valid section names: {string.Join(", ", validSectionNames)}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Load all valid production unit names from the file system
    /// </summary>
    private static void LoadValidProductionUnitNames()
    {
        if (validProductionUnitNames != null) return; // Already loaded
        
        validProductionUnitNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Load all ProductionUnitData assets from Resources/Production
            var productionUnits = Resources.LoadAll<ProductionUnitData>("Production");
            
            foreach (var unit in productionUnits)
            {
                if (unit != null && unit.gameUnit != null && !string.IsNullOrEmpty(unit.gameUnit.name))
                {
                    validProductionUnitNames.Add(unit.gameUnit.name);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error loading production unit names: {e.Message}");
        }
        
        GameLoggingSystem.Instance.LogEvent($"Loaded {validProductionUnitNames.Count} valid production unit names: {string.Join(", ", validProductionUnitNames)}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Load all valid stat names from StatManager
    /// </summary>
    private static void LoadValidStatNames()
    {
        if (validPillarNames != null) return; // Already loaded
        
        validPillarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aureus", "regalia", "waltz", "chorus"
        };
        
        validSubstatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "innovation", "piety", "authority", "ambition", "symphony", "euphony", "arcane", "secrecy"
        };
        
        validDerivedStatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "discoveryEfficiency", "savingRollChance", "legendEffectiveness", "expeditionCostMod", 
            "expeditionTimeMod", "satisfactionEffectiveness", "moraleLossMod", "moraleRecoveryMod", 
            "clickPowerBonus", "magicEffectiveness", "communionStage"
        };
        
    }
    
    /// <summary>
    /// Check if a stat name is a valid global stat or special condition stat
    /// </summary>
    private static bool IsValidGlobalStat(string statName)
    {
        // StatManager global stats that can be used as condition stats
        string[] globalStats = { "satisfaction", "morale", "moraleBalance", "maxmorale", "satisfactionupgradethreshold", "satisfactionPoints", "satisfactionLevel" };
        
        // Special non-StatManager stats that are valid condition stats
        string[] specialStats = { "housing" }; // Handled by PopGrowthLogic
        
        string lowerStat = statName.ToLower();
        return globalStats.Contains(lowerStat) || specialStats.Contains(lowerStat);
    }
    
    /// <summary>
    /// Validate a target stat and provide detailed warnings if invalid
    /// </summary>
    /// <param name="targetStat">The target stat to validate</param>
    /// <param name="bonusType">The type of bonus for context</param>
    /// <param name="sourceName">The source of the bonus for logging</param>
    /// <param name="isConditionStat">Whether this is a condition stat (for scaling bonuses)</param>
    /// <param name="isStartupValidation">Whether this is startup validation (affects return value)</param>
    /// <returns>True if valid, false if invalid</returns>
    private static bool ValidateTargetStat(string targetStat, string bonusType, string sourceName, bool isConditionStat = false, bool isStartupValidation = false)
    {
        if (string.IsNullOrEmpty(targetStat)) return true; // Empty is valid for some bonuses
        
        // Load validation data if not already loaded
        LoadValidResourceNames();
        LoadValidSectionNames();
        LoadValidProductionUnitNames();
        LoadValidStatNames();
        
        string statType = isConditionStat ? "condition stat" : "target stat";
        bool hasWarning = false;
        
        // Check what type of bonus this is and validate accordingly
        switch (bonusType.ToLower())
        {
            case "pillarbonus":
                if (!validPillarNames.Contains(targetStat))
                {
                    Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid pillar name. Valid pillars: {string.Join(", ", validPillarNames)}");
                    hasWarning = true;
                }
                break;
                
            case "substatbonus":
                if (!validSubstatNames.Contains(targetStat))
                {
                    Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid substat name. Valid substats: {string.Join(", ", validSubstatNames)}");
                    hasWarning = true;
                }
                break;
                
            case "derivedstatbonus":
                if (!validDerivedStatNames.Contains(targetStat))
                {
                    Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid derived stat name. Valid derived stats: {string.Join(", ", validDerivedStatNames)}");
                    hasWarning = true;
                }
                break;
                
            case "resourcemodifier":
            case "clickpowerbonus":
                // Can be either a resource name or a section name
                if (!validResourceNames.Contains(targetStat) && !validSectionNames.Contains(targetStat))
                {
                    Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid resource or section name.\nValid resources: {string.Join(", ", validResourceNames)}\nValid sections: {string.Join(", ", validSectionNames)}");
                    hasWarning = true;
                }
                break;
                
            case "productionmodifier":
            case "constructioncostmodifier":
            case "productionscalingbonus":
                if (isConditionStat)
                {
                    // Condition stats for scaling bonuses should be stat names (pillars, substats, derived stats, or globals)
                    if (!validPillarNames.Contains(targetStat) && 
                        !validSubstatNames.Contains(targetStat) && 
                        !validDerivedStatNames.Contains(targetStat) &&
                        !IsValidGlobalStat(targetStat))
                    {
                        Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid stat name.\nValid pillars: {string.Join(", ", validPillarNames)}\nValid substats: {string.Join(", ", validSubstatNames)}\nValid derived stats: {string.Join(", ", validDerivedStatNames)}\nValid global stats: housing, satisfaction, morale, moraleBalance, maxmorale, satisfactionupgradethreshold, satisfactionPoints, satisfactionLevel");
                        hasWarning = true;
                    }
                }
                else
                {
                    // Target stats can be: resources, sections, or production unit names
                    if (!validResourceNames.Contains(targetStat) && 
                        !validSectionNames.Contains(targetStat) && 
                        !validProductionUnitNames.Contains(targetStat))
                    {
                        Debug.LogWarning($"[GovernmentLogic] VALIDATION WARNING: {statType} '{targetStat}' in {bonusType} for {sourceName} is not a valid resource, section, or production unit name.\nValid resources: {string.Join(", ", validResourceNames)}\nValid sections: {string.Join(", ", validSectionNames)}\nValid production units: {string.Join(", ", validProductionUnitNames)}");
                        hasWarning = true;
                    }
                }
                break;
                
            default:
                // Unknown bonus type - can't validate
                Debug.LogWarning($"[GovernmentLogic] VALIDATION INFO: Unknown bonus type '{bonusType}' for {statType} '{targetStat}' in {sourceName} - skipping validation");
                break;
        }
        
        // For startup validation, return false if there was a warning; for runtime, always return true to avoid breaking functionality
        return !hasWarning;
    }
    
    /// <summary>
    /// Validate all legends and civics at startup to catch configuration errors early
    /// </summary>
    public static void ValidateAllDataAtStartup()
    {
        GameLoggingSystem.Instance.LogEvent("Starting validation of all legends and civics...", "GovernmentLogic");
        
        // Ensure validation data is loaded
        LoadValidResourceNames();
        LoadValidSectionNames();
        LoadValidProductionUnitNames();
        LoadValidStatNames();
        
        int totalWarnings = 0;
        
        // Validate all legends
        int legendCount = Resources.LoadAll<LegendData>("").Length;
        totalWarnings += ValidateAllLegends();
        
        // Validate all civics
        int civicCount = Resources.LoadAll<CivicData>("").Length;
        totalWarnings += ValidateAllCivics();
        
        GameLoggingSystem.Instance.LogEvent($"Validation completed: {legendCount} legends, {civicCount} civics processed", "GovernmentLogic");
        
        if (totalWarnings == 0)
        {
            GameLoggingSystem.Instance.LogEvent("✅ All legends and civics passed validation!", "GovernmentLogic");
        }
        else
        {
            Debug.LogWarning($"[GovernmentLogic] ⚠️ Validation completed with {totalWarnings} warnings. Please fix the above issues.");
        }
    }
    
    /// <summary>
    /// Validate all loaded legends
    /// </summary>
    private static int ValidateAllLegends()
    {
        int warningCount = 0;
        
        try
        {
            var allLegends = Resources.LoadAll<LegendData>("");
            
            foreach (var legend in allLegends)
            {
                if (legend == null) continue;
                
                string sourceName = $"Legend: {legend.legendName}";
                
                // Validate legend bonuses
                if (legend.bonuses != null)
                {
                    foreach (var bonus in legend.bonuses)
                    {
                        if (bonus == null) continue;
                        
                        // Validate target stat
                        if (!string.IsNullOrEmpty(bonus.targetStat))
                        {
                            if (!ValidateTargetStat(bonus.targetStat, bonus.bonusType.ToString(), sourceName, false, true))
                            {
                                warningCount++;
                            }
                        }
                        
                        // Validate condition stat for scaling bonuses
                        if (!string.IsNullOrEmpty(bonus.conditionStat))
                        {
                            if (!ValidateTargetStat(bonus.conditionStat, bonus.bonusType.ToString(), sourceName, true, true))
                            {
                                warningCount++;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error validating legends: {e.Message}");
        }
        
        return warningCount;
    }
    
    /// <summary>
    /// Validate all loaded civics
    /// </summary>
    private static int ValidateAllCivics()
    {
        int warningCount = 0;
        
        try
        {
            var allCivics = Resources.LoadAll<CivicData>("");
            
            foreach (var civic in allCivics)
            {
                if (civic == null) continue;
                
                string sourceName = $"Civic: {civic.civicName}";
                
                // Validate civic effects
                if (civic.effects != null)
                {
                    foreach (var effect in civic.effects)
                    {
                        if (effect == null) continue;
                        
                        // Validate target stat
                        if (!string.IsNullOrEmpty(effect.targetStat))
                        {
                            if (!ValidateTargetStat(effect.targetStat, effect.effectType.ToString(), sourceName, false, true))
                            {
                                warningCount++;
                            }
                        }
                        
                        // Validate condition stat for scaling bonuses
                        if (!string.IsNullOrEmpty(effect.conditionStat))
                        {
                            if (!ValidateTargetStat(effect.conditionStat, effect.effectType.ToString(), sourceName, true, true))
                            {
                                warningCount++;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error validating civics: {e.Message}");
        }
        
        return warningCount;
    }
    
    // ===== CIVIC INTEGRATION SYSTEM =====
    
    /// <summary>
    /// Register a civic that grants a council position
    /// This should only be called when a civic is UNLOCKED, not when it's loaded
    /// </summary>
    public void RegisterCivicCouncilPosition(CivicData civic)
    {
        if (civic == null || !civic.grantsCouncilPosition) return;
        
        try
        {
            // Validate legend class restrictions
            var (isValid, errorMessage) = civic.ValidateLegendClassRestrictions();
            if (!isValid)
            {
                Debug.LogError($"[GovernmentLogic] Cannot register civic '{civic.civicName}': {errorMessage}");
                return;
            }
            
            // Create a new council seat for this civic using the new CivicCouncilPosition structure
            var civicSeat = new CouncilSeat(civic.councilPosition?.title ?? civic.civicName, -999);
            civicSeat.isUnlocked = true;
            civicSeat.sourceCivic = civic;
            civicSeat.civicSeatTitle = civic.councilPosition?.title ?? civic.civicName;
            civicSeat.roleplayDescription = civic.councilPosition?.GetAutoDescription() ?? civic.civicName;
            
            // Add allowed legend classes and seat bonuses from the new CivicCouncilPosition structure
            AddCivicCouncilPositionData(civicSeat, civic);
            
            // Add the seat to our tracking
            civicCouncilSeats[civic.civicName] = civicSeat;
            
            // Add to available pool for potential replacement
            availableSeatPool.Add(civicSeat);
            
            GameLoggingSystem.Instance.LogEvent($"Registered UNLOCKED civic council position: {civic.civicName} -> {civicSeat.GetEffectiveTitle()}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"Legend classes: {string.Join(", ", civicSeat.allowedLegendClasses)}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"Added to available seat pool. Total available: {availableSeatPool.Count}", "GovernmentLogic");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error registering civic council position for {civic.civicName}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Check if a civic is currently unlocked and available for council seat replacement
    /// </summary>
    public bool IsCivicUnlocked(string civicName)
    {
        return civicCouncilSeats.ContainsKey(civicName);
    }
    
    /// <summary>
    /// Get all unlocked civic names that can be used for seat replacement
    /// </summary>
    public List<string> GetUnlockedCivicNames()
    {
        return civicCouncilSeats.Keys.ToList();
    }
    
    /// <summary>
    /// Get all available seat titles (default + unlocked civics only)
    /// </summary>
    public List<string> GetAvailableSeatTitles()
    {
        var titles = new List<string>();
        
        // Add default seat titles
        foreach (var template in defaultSeatTemplates)
        {
            titles.Add(template.seatTitle);
        }
        
        // Add unlocked civic seat titles
        foreach (var kvp in civicCouncilSeats)
        {
            titles.Add(kvp.Value.seatTitle);
        }
        
        return titles;
    }
    

    
    /// <summary>
    /// Add council position data (legend classes and seat bonuses) from the new CivicCouncilPosition structure
    /// </summary>
    private void AddCivicCouncilPositionData(CouncilSeat civicSeat, CivicData civic)
    {
        if (civic.councilPosition == null)
        {

            Debug.LogWarning($"[GovernmentLogic] Civic '{civic.civicName}' grants council position but has no councilPosition configuration. Using default settings.");

            // Use default settings for civics without council position configuration
            civicSeat.allowedLegendClasses.AddRange(new[] { 
                LegendClass.Sovereign, LegendClass.Vanguard, LegendClass.Steward, 
                LegendClass.Weaver, LegendClass.Seer, LegendClass.Justiciar 
            });
            return;
        }
        
        var councilPos = civic.councilPosition;
        
        // Add allowed legend classes from the new structure
        if (councilPos.allowAnyLegendClass)
        {
            // Allow all legend classes for this position
            civicSeat.allowedLegendClasses.AddRange(new[] { 
                LegendClass.Sovereign, LegendClass.Vanguard, LegendClass.Steward, 
                LegendClass.Weaver, LegendClass.Seer, LegendClass.Justiciar 
            });
            
            GameLoggingSystem.Instance.LogEvent($"Civic '{civic.civicName}' council position allows any legend class", "GovernmentLogic");
        }
        else if (councilPos.allowedClasses != null && councilPos.allowedClasses.Length > 0)
        {
            // Use the specific legend classes defined in the council position
            civicSeat.allowedLegendClasses.AddRange(councilPos.allowedClasses);
            
            GameLoggingSystem.Instance.LogEvent($"Civic '{civic.civicName}' council position restricted to classes: {string.Join(", ", councilPos.allowedClasses)}", "GovernmentLogic");
        }
        else
        {
            // Default fallback: allow all classes
            civicSeat.allowedLegendClasses.AddRange(new[] { 
                LegendClass.Sovereign, LegendClass.Vanguard, LegendClass.Steward, 
                LegendClass.Weaver, LegendClass.Seer, LegendClass.Justiciar 
            });
            
            GameLoggingSystem.Instance.LogEvent($"Civic '{civic.civicName}' council position using default class restrictions (all classes allowed)", "GovernmentLogic");
        }
        
        // Add seat bonuses from the new structure
        if (councilPos.bonuses != null && councilPos.bonuses.Length > 0)
        {
            foreach (var civicBonus in councilPos.bonuses)
            {
                if (civicBonus != null)
                {
                    var seatBonus = ConvertCivicSeatBonusToSeatBonus(civicBonus);
            if (seatBonus != null)
            {
                civicSeat.seatBonuses.Add(seatBonus);
                    }
                }
            }
            
            GameLoggingSystem.Instance.LogEvent($"Civic '{civic.civicName}' council position added {councilPos.bonuses.Length} seat bonuses", "GovernmentLogic");
        }
    }
    
    /// <summary>
    /// Convert CivicSeatBonus to SeatBonus
    /// </summary>
    private SeatBonus ConvertCivicSeatBonusToSeatBonus(CivicSeatBonus civicBonus)
    {
        var seatBonus = new SeatBonus
        {
            bonusType = civicBonus.bonusType,
            targetStat = civicBonus.targetStat,
            modifierValue = civicBonus.modifierValue,
            modifierType = civicBonus.modifierType,
            description = civicBonus.GetAutoDescription(), // Use automatic description generation
            requiresLegend = civicBonus.requiresLegend
        };
        
        return seatBonus;
    }
    

    
    /// <summary>
    /// Unregister a civic council position when the civic is removed
    /// </summary>
    public void UnregisterCivicCouncilPosition(CivicData civic)
    {
        if (civic == null || !civic.grantsCouncilPosition) return;
        
        try
        {
            if (civicCouncilSeats.TryGetValue(civic.civicName, out var civicSeat))
            {
                // Check if this civic seat is currently active at any position
                int activePosition = GetSeatPosition(civicSeat.seatTitle);
                if (activePosition >= 0)
                {
                    // Replace with a default seat
                    Debug.Log($"[GovernmentLogic] Civic seat '{civic.civicName}' is currently active at position {activePosition}. Replacing with default seat...");
                    ResetSeatToDefault(activePosition);
                }
                
                // Remove all bonuses from this civic seat
                if (civicSeat.assignedLegend != null)
                {
                    RemoveSeatBonuses(civicSeat.assignedLegend);
                }
                
                // Remove from available pool
                availableSeatPool.Remove(civicSeat);
                
                // Remove the seat from tracking
                civicCouncilSeats.Remove(civic.civicName);
                
                GameLoggingSystem.Instance.LogEvent($"Unregistered civic council position: {civic.civicName}", "GovernmentLogic");
                GameLoggingSystem.Instance.LogEvent($"Removed from available pool. Total available: {availableSeatPool.Count}", "GovernmentLogic");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error unregistering civic council position for {civic.civicName}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Get all civic council seats
    /// </summary>
    public List<CouncilSeat> GetCivicCouncilSeats()
    {
        return civicCouncilSeats.Values.ToList();
    }
    
    /// <summary>
    /// Get the total number of council seats (including civic seats)
    /// </summary>
    public int GetTotalCouncilSeatCount()
    {
        return councilSeats.Count + civicCouncilSeats.Count;
    }
    
    // ===== ERROR HANDLING AND VALIDATION =====
    
    /// <summary>
    /// Validate council seat operations
    /// </summary>
    public bool ValidateCouncilSeatOperation(int seatIndex, string operation)
    {
        // Special case for Head of State (seatIndex = -1)
        if (seatIndex == -1)
        {
            // Head of State is always stored at index 0 in the councilSeats list
            if (councilSeats.Count > 0 && councilSeats[0].seatIndex == -1)
            {
                var seat = councilSeats[0];
                if (seat == null)
                {
                    Debug.LogError($"[GovernmentLogic] Head of State seat is null for operation: {operation}");
                    return false;
                }
                return true;
            }
            Debug.LogWarning($"[GovernmentLogic] Head of State not found for operation: {operation}");
            return false;
        }
        
        // For regular seats (0-5), check bounds
        if (seatIndex < 0 || seatIndex >= 6)
        {
            Debug.LogWarning($"[GovernmentLogic] Invalid seat index {seatIndex} for operation: {operation}");
            return false;
        }
        
        // Find the seat with the matching seatIndex
        foreach (var seat in councilSeats)
        {
            if (seat.seatIndex == seatIndex)
            {
                if (seat == null)
                {
                    Debug.LogError($"[GovernmentLogic] Seat {seatIndex} is null for operation: {operation}");
                    return false;
                }
                return true;
            }
        }
        
        Debug.LogWarning($"[GovernmentLogic] Seat {seatIndex} not found for operation: {operation}");
        return false;
    }
    
    /// <summary>
    /// Check if a legend can be assigned to a specific seat
    /// </summary>
    public bool CanAssignLegendToSeat(LegendData legend, int seatIndex)
    {
        if (!ValidateCouncilSeatOperation(seatIndex, "legend assignment"))
            return false;
            
        // Find the seat - handle Head of State specially
        CouncilSeat seat;
        if (seatIndex == -1)
        {
            // Head of State is at index 0 in councilSeats list
            seat = councilSeats[0];
        }
        else
        {
            // Find regular seat by seatIndex
            seat = null;
            foreach (var s in councilSeats)
            {
                if (s.seatIndex == seatIndex)
                {
                    seat = s;
                    break;
                }
            }
            
            if (seat == null)
            {
                Debug.LogError($"[GovernmentLogic] Could not find seat with index {seatIndex}");
                return false;
            }
        }
        
        // Check if seat is unlocked
        if (!seat.isUnlocked)
        {
            GameLoggingSystem.Instance.LogEvent($"Cannot assign {legend.legendName} to locked seat {seatIndex}", "GovernmentLogic");
            return false;
        }
        
        // Check if legend class is allowed for this seat
        if (!seat.allowedLegendClasses.Contains(legend.legendClass))
        {
            GameLoggingSystem.Instance.LogEvent($"Cannot assign {legend.legendName} ({legend.legendClass}) to seat {seatIndex} - class not allowed. Allowed: {string.Join(", ", seat.allowedLegendClasses)}", "GovernmentLogic");
            return false;
        }
        
        // Note: We don't check if seat already has a legend here
        // The AssignLegendToSeat method will handle legend replacement automatically
        
        return true;
    }
    
    /// <summary>
    /// Assign a legend to a council seat
    /// </summary>
    /// <param name="legend">The legend to assign</param>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    /// <param name="bypassCooldown">Whether to bypass cooldown validation (for internal operations)</param>
    /// <returns>True if assignment was successful, false otherwise</returns>
    public bool AssignLegendToSeat(LegendData legend, int seatIndex, bool bypassCooldown = false)
    {
        // Check cooldown validation before proceeding (unless bypassing for internal operations)
        if (!bypassCooldown && !CanChangeSeat(seatIndex))
        {
            int remainingCooldown = GetSeatCooldownRemaining(seatIndex);
            GameLoggingSystem.Instance.LogEvent($"Assignment blocked - Seat {seatIndex} on cooldown for {remainingCooldown} more sevenths", "GovernmentLogic");
            return false; // Assignment blocked by cooldown
        }
        
        if (!CanAssignLegendToSeat(legend, seatIndex))
            return false;
            
        try
        {
            // Find the seat - handle Head of State specially
            CouncilSeat seat;
            if (seatIndex == -1)
            {
                // Head of State is at index 0 in councilSeats list
                seat = councilSeats[0];
            }
            else
            {
                // Find regular seat by seatIndex
                seat = null;
                foreach (var s in councilSeats)
                {
                    if (s.seatIndex == seatIndex)
                    {
                        seat = s;
                        break;
                    }
                }
                
                if (seat == null)
                {
                    Debug.LogError($"[GovernmentLogic] Could not find seat with index {seatIndex}");
                    return false;
                }
            }
            
            // Check if this legend is already assigned to another seat and attempt centralized swap handling
            var previousSeat = GetSeatWithLegend(legend.legendName);
            if (previousSeat != null)
            {
                if (TryExecuteSeatSwap(legend, seat, previousSeat))
                {
                    return true; // Swap completed and events fired
                }
                // If swap could not be performed, ensure previous seat is vacated before standard assignment
                        RemoveLegendFromSeat(previousSeat.seatIndex);
            }
            
            // Standard assignment (no swap involved)
            // ALWAYS reset activation timer when assigning a new legend (even if seat was already active)
            // This forces proper cleanup of any existing bonuses and prevents stacking
            bool wasActive = seat.IsActive();
            
            // Clear bonuses for the incoming legend (in case it had bonuses from a previous seat)
            RemoveSeatBonuses(legend);
            
            // If there's currently a legend in this seat, clear its bonuses too
            if (seat.assignedLegend != null)
            {
                RemoveSeatBonuses(seat.assignedLegend);
            }
            
            seat.assignedLegend = legend;
            seat.seventhsUntilActive = isRecalculatingHeadOfState ? 0 : 3; // Skip grace period during recalculation
            seat.bonusesProcessed = false; // Reset bonus processing flag
            
            GameLoggingSystem.Instance.LogEvent($"Reset activation timer for seat '{seat.seatTitle}' (was active: {wasActive}, now needs {seat.seventhsUntilActive} sevenths)", "GovernmentLogic");
            
            // Do NOT process seat bonuses here - wait for activation timer
            // ProcessSeatBonuses(seat); // REMOVED - bonuses will be applied when seat becomes active
            
            // Record cooldown for the seat change
            RecordSeatChange(seatIndex);
            
            // Notify listeners
            OnCouncilSeatChanged?.Invoke(seat);
            OnLeaderAssigned?.Invoke(seat, legend);
            OnCouncilCompositionChanged?.Invoke();
            
            // Trigger leader pool update to reflect cooldown changes
            OnLeaderPoolChanged?.Invoke();

            // Force deterministic processing after any assignment
            RecalculateFullCouncil("Post-assignment forced", true);
            
            string seatTitle = seatIndex == -1 ? "Head of State" : seat.GetEffectiveTitle();
            GameLoggingSystem.Instance.LogEvent($"Assigned {legend.legendName} to seat {seatIndex} ({seatTitle})", "GovernmentLogic");
            
            // CRITICAL: Only recalculate entire council when Head of State or Legend Effectiveness changes, not for simple assignments
            // This prevents unnecessary recalculation that resets activation timers for all legends
            if (!isRecalculatingHeadOfState)
            {
                // For both Head of State and regular seats, we don't need full recalculation for simple assignments
                // Full recalculation only happens when Head of State or Legend Effectiveness changes (which affects all legend multipliers)
                string seatName = (seatIndex == -1) ? "Head of State" : $"Seat {seatIndex}";
                GameLoggingSystem.Instance.LogEvent($"{seatName} assignment - no full council recalculation needed for simple assignment", "GovernmentLogic");
            }
            
            // Update Head of State status indicators if this was a Head of State assignment
            if (seatIndex == -1)
            {
                UpdateHeadOfStateStatusIndicators();
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error assigning {legend.legendName} to seat {seatIndex}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Centralized helper to execute a perfect swap when possible.
    /// Returns true if a swap was executed, false to fall back to standard assignment behavior.
    /// </summary>
    private bool TryExecuteSeatSwap(LegendData incomingLegend, CouncilSeat targetSeat, CouncilSeat previousSeat)
    {
        if (incomingLegend == null || targetSeat == null || previousSeat == null) return false;
        var displacedLegend = targetSeat.assignedLegend;
        if (displacedLegend == null)
        {
            // Nothing to swap in target; caller will remove previous seat assignment and proceed
            return false;
        }
        
        bool canReassignDisplaced = CanAssignLegendToSeat(displacedLegend, previousSeat.seatIndex);
        bool previousSeatOnCooldown = !CanChangeSeat(previousSeat.seatIndex);
        
        if (canReassignDisplaced && !previousSeatOnCooldown)
        {
            LogSwapOperation(incomingLegend, targetSeat, displacedLegend, previousSeat, true);
            
            // Clear existing bonuses for both legends before reassigning
            RemoveSeatBonuses(incomingLegend);
            RemoveSeatBonuses(displacedLegend);
            
            // Assign the new legend to the target seat
            targetSeat.assignedLegend = incomingLegend;
            targetSeat.seventhsUntilActive = 3;
            targetSeat.bonusesProcessed = false;
            
            // Assign the displaced legend to the previous seat
            previousSeat.assignedLegend = displacedLegend;
            previousSeat.seventhsUntilActive = 3;
            previousSeat.bonusesProcessed = false;
            
            // Record cooldowns for both seats
            RecordPerfectSwapCooldowns(targetSeat.seatIndex, previousSeat.seatIndex);
            
            // Fire events
            OnCouncilSeatChanged?.Invoke(targetSeat);
            OnCouncilSeatChanged?.Invoke(previousSeat);
            OnLeaderAssigned?.Invoke(targetSeat, incomingLegend);
            OnLeaderAssigned?.Invoke(previousSeat, displacedLegend);
            OnCouncilCompositionChanged?.Invoke();
            OnLeaderPoolChanged?.Invoke();
            
            if (targetSeat.seatIndex == -1 || previousSeat.seatIndex == -1)
            {
                UpdateHeadOfStateStatusIndicators();
            }
            
            string seatName = (targetSeat.seatIndex == -1) ? "Head of State" : $"Seat {targetSeat.seatIndex}";
            string prevName = (previousSeat.seatIndex == -1) ? "Head of State" : $"Seat {previousSeat.seatIndex}";
            GameLoggingSystem.Instance.LogEvent($"Centralized perfect swap executed between {seatName} and {prevName}", "GovernmentLogic");
            
            // Force a deterministic recalculation now that both swapped seats are inactive.
            // This ensures any removed LE/direct bonuses are reflected immediately for remaining active seats.
            RecalculateFullCouncil("Perfect swap cleanup", true);
            return true;
        }
        
        // Not a perfect swap; optionally log and let caller handle standard path (removal + assign)
        LogSwapOperation(incomingLegend, targetSeat, displacedLegend, previousSeat, false);
        if (canReassignDisplaced && previousSeatOnCooldown)
        {
            GameLoggingSystem.Instance.LogEvent($"Perfect swap blocked - previous seat {previousSeat.seatIndex} is on cooldown", "GovernmentLogic");
        }
        return false;
    }
    
    /// <summary>
    /// Remove a legend from a council seat
    /// </summary>
    /// <param name="seatIndex">Seat index (-1 for Head of State, 0-5 for regular seats)</param>
    /// <param name="bypassCooldown">Whether to bypass cooldown validation (for internal operations)</param>
    /// <returns>True if removal was successful, false otherwise</returns>
    public bool RemoveLegendFromSeat(int seatIndex, bool bypassCooldown = false)
    {
        // Check cooldown validation before proceeding (unless bypassing for internal operations)
        if (!bypassCooldown && !CanChangeSeat(seatIndex))
        {
            int remainingCooldown = GetSeatCooldownRemaining(seatIndex);
            GameLoggingSystem.Instance.LogEvent($"Removal blocked - Seat {seatIndex} on cooldown for {remainingCooldown} more sevenths", "GovernmentLogic");
            return false; // Removal blocked by cooldown
        }
        
        if (!ValidateCouncilSeatOperation(seatIndex, "legend removal"))
            return false;
            
        try
        {
            // Find the seat - handle Head of State specially
            CouncilSeat seat;
            if (seatIndex == -1)
            {
                // Head of State is at index 0 in councilSeats list
                seat = councilSeats[0];
            }
            else
            {
                // Find regular seat by seatIndex
                seat = null;
                foreach (var s in councilSeats)
                {
                    if (s.seatIndex == seatIndex)
                    {
                        seat = s;
                        break;
                    }
                }
                
                if (seat == null)
                {
                    Debug.LogError($"[GovernmentLogic] Could not find seat with index {seatIndex}");
                    return false;
                }
            }
            
            if (seat.assignedLegend == null)
            {
                GameLoggingSystem.Instance.LogEvent($"Seat {seatIndex} has no legend to remove", "GovernmentLogic");
                return false;
            }
            
            var legend = seat.assignedLegend;
            
            // Remove seat bonuses IMMEDIATELY (regardless of activation status)
            RemoveSeatBonuses(legend);
            
            // Clear the assignment
            seat.assignedLegend = null;
            seat.seventhsUntilActive = 0;
            seat.bonusesProcessed = false; // Reset bonus processing flag
            
            // Record cooldown for the seat change
            RecordSeatChange(seatIndex);
            
            // Notify listeners
            OnCouncilSeatChanged?.Invoke(seat);
            OnLeaderRemoved?.Invoke(seat);
            OnCouncilCompositionChanged?.Invoke();
            
            // Trigger leader pool update to reflect cooldown changes
            OnLeaderPoolChanged?.Invoke();

            // Force deterministic processing after any removal
            RecalculateFullCouncil("Post-removal forced", true);
            
            GameLoggingSystem.Instance.LogEvent($"Removed {legend.legendName} from seat {seatIndex}", "GovernmentLogic");
            
            // CRITICAL: Only recalculate entire council when Head of State or Legend Effectiveness changes, not for simple removals
            // This prevents unnecessary recalculation that resets activation timers for all legends
            if (!isRecalculatingHeadOfState)
            {
                // For both Head of State and regular seats, we don't need full recalculation for simple removals
                // Full recalculation only happens when Head of State or Legend Effectiveness changes (which affects all legend multipliers)
                string seatName = (seatIndex == -1) ? "Head of State" : $"Seat {seatIndex}";
                GameLoggingSystem.Instance.LogEvent($"{seatName} removal - no full council recalculation needed for simple removal", "GovernmentLogic");
            }
            
            // Update Head of State status indicators if this was a Head of State removal
            if (seatIndex == -1)
            {
                UpdateHeadOfStateStatusIndicators();
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error removing legend from seat {seatIndex}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Get the seat where a specific legend is assigned
    /// </summary>
    public CouncilSeat GetSeatWithLegend(string legendName)
    {
        foreach (var seat in councilSeats)
        {
            if (seat.assignedLegend != null && seat.assignedLegend.legendName == legendName)
            {
                return seat;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Get all available legends for a specific seat (considering class restrictions and current assignments)
    /// </summary>
    public List<LegendData> GetAvailableLegendsForSeat(int seatIndex)
    {
        if (!ValidateCouncilSeatOperation(seatIndex, "legend availability check"))
            return new List<LegendData>();
            
        // Find the seat - handle Head of State specially
        CouncilSeat seat;
        if (seatIndex == -1)
        {
            // Head of State is at index 0 in councilSeats list
            seat = councilSeats[0];
        }
        else
        {
            // Find regular seat by seatIndex
            seat = null;
            foreach (var s in councilSeats)
            {
                if (s.seatIndex == seatIndex)
                {
                    seat = s;
                    break;
                }
            }
            
            if (seat == null)
            {
                Debug.LogError($"[GovernmentLogic] Could not find seat with index {seatIndex}");
                return new List<LegendData>();
            }
        }
        
        var availableLegends = new List<LegendData>();
        
        if (LegendLeaderLogic.Instance != null)
        {
            foreach (var legend in LegendLeaderLogic.Instance.GetAvailableLegends())
            {
                // Check if legend class is allowed for this seat
                if (seat.CanAssignLegend(legend))
                {
                    // Check if this legend is assigned to a seat on cooldown
                    var assignedSeat = GetSeatWithLegend(legend.legendName);
                    if (assignedSeat != null)
                    {
                        // Legend is assigned to a seat - check if that seat is on cooldown
                        bool assignedSeatOnCooldown = !CanChangeSeat(assignedSeat.seatIndex);
                        if (assignedSeatOnCooldown)
                        {
                            // Skip this legend - it's assigned to a seat on cooldown
                            GameLoggingSystem.Instance.LogEvent($"Hiding {legend.legendName} from leader pool - assigned to seat {assignedSeat.seatIndex} on cooldown", "GovernmentLogic");
                            continue;
                        }
                    }
                    
                    // Include this legend (either unassigned or assigned to a seat not on cooldown)
                    availableLegends.Add(legend);
                }
            }
        }
        
        return availableLegends;
    }
    
    /// <summary>
    /// Get all legends that can be assigned to any seat (for leader pool display)
    /// </summary>
    public List<LegendData> GetAllAvailableLegends()
    {
        if (LegendLeaderLogic.Instance != null)
        {
            return LegendLeaderLogic.Instance.GetAvailableLegends();
        }
        return new List<LegendData>();
    }
    
    /// <summary>
    /// Get all currently assigned legends with their seat information
    /// </summary>
    public List<(CouncilSeat seat, LegendData legend)> GetAllAssignedLegends()
    {
        var assignments = new List<(CouncilSeat, LegendData)>();
        
        foreach (var seat in councilSeats)
        {
            if (seat.assignedLegend != null)
            {
                assignments.Add((seat, seat.assignedLegend));
            }
        }
        
        return assignments;
    }
    
    /// <summary>
    /// Process seventh changes for council seat activation and cooldown decrement
    /// HARD RESET SYSTEM: Cooldowns can only be decremented, never increased
    /// </summary>
    public void ProcessSeventhChange(int newSeventh)
    {
        // Reset recalculation flag at the start of each new seventh
        hasRecalculatedHeadOfStateThisSeventh = false;
        lastHeadOfStateRecalculationSeventh = newSeventh;
        
        // keep quiet to reduce spam
        
        try
        {
            // Decrement cooldowns for all seats (HARD RESET SYSTEM - only decrement, never increase)
            if (headOfStateCooldownRemaining > 0)
            {
                headOfStateCooldownRemaining--;
                if (headOfStateCooldownRemaining == 0)
                {
                    GameLoggingSystem.Instance.LogEvent("Head of State cooldown expired - ready for changes", "GovernmentLogic");
                }
            }
            
            // Decrement cooldowns for regular seats - create a copy to avoid collection modification during iteration
            var seatsToRemove = new List<int>();
            var cooldownsToProcess = new Dictionary<int, int>(seatCooldownRemaining);
            foreach (var kvp in cooldownsToProcess)
            {
                int seatIndex = kvp.Key;
                int remaining = kvp.Value;
                
                if (remaining > 0)
                {
                    seatCooldownRemaining[seatIndex] = remaining - 1;
                    if (seatCooldownRemaining[seatIndex] == 0)
                    {
                        GameLoggingSystem.Instance.LogEvent($"Seat {seatIndex} cooldown expired - ready for changes", "GovernmentLogic");
                    }
                }
                
                // Remove entries that have reached 0 to keep dictionary clean
                if (seatCooldownRemaining[seatIndex] <= 0)
                {
                    seatsToRemove.Add(seatIndex);
                }
            }
            
            // Clean up expired cooldowns
            foreach (int seatIndex in seatsToRemove)
            {
                seatCooldownRemaining.Remove(seatIndex);
            }
            
            // Process seat activation timers - create a copy to avoid collection modification during iteration
            var seatsToProcess = new List<CouncilSeat>(councilSeats);
            foreach (var seat in seatsToProcess)
            {
                if (seat.isUnlocked && seat.assignedLegend != null && seat.seventhsUntilActive > 0)
                {
                    seat.seventhsUntilActive--;
                    
                    if (seat.seventhsUntilActive <= 0)
                    {
                        // Seat is now active; centralized pipeline will handle applying bonuses
                        GameLoggingSystem.Instance.LogEvent($"Seat active: {seat.GetEffectiveTitle()} → {seat.assignedLegend.legendName}. Triggering pipeline.", "GovernmentLogic");
                        seat.bonusesProcessed = false; // ensure included in next centralized processing
                        // Trigger the centralized pipeline immediately so UI reflects correct state
                        ProcessAllSeatBonuses();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GovernmentLogic] Error processing seventh change for council seats: {e.Message}");
        }
        
        // Update Head of State status indicators after seventh processing
        UpdateHeadOfStateStatusIndicators();
        
        // Trigger leader pool update to reflect cooldown changes
        OnLeaderPoolChanged?.Invoke();
    }

    // ===== SEAT REPLACEMENT SYSTEM =====
    
    /// <summary>
    /// Replace a seat at a specific position with a different seat from the available pool
    /// </summary>
    public bool ReplaceSeatWithAvailable(int position, string seatTitle)
    {
        if (!CanReplaceSeatAtPosition(position))
            return false;
            
        // Find the seat in the available pool
        var availableSeat = FindSeatInPool(seatTitle);
        if (availableSeat == null)
        {
            Debug.LogWarning($"[GovernmentLogic] Seat '{seatTitle}' not found in available pool");
            return false;
        }
        
        // Check if this seat is already in use at another position
        if (IsSeatInUseAtOtherPosition(seatTitle, position))
        {
            GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Seat '{seatTitle}' is already in use at another position", "GovernmentLogic");
            return false;
        }
        
        // Perform the replacement
        ReplaceSeatAtPosition(position, availableSeat);
        
        GameLoggingSystem.Instance.LogEvent($"Successfully replaced seat at position {position} with '{seatTitle}'", "GovernmentLogic");
        
        return true;
    }
    
    /// <summary>
    /// Swap two seats between positions
    /// </summary>
    public bool SwapSeats(int position1, int position2)
    {
        if (!CanReplaceSeatAtPosition(position1) || !CanReplaceSeatAtPosition(position2))
            return false;
            
        if (position1 == position2)
            return true; // No swap needed
            
        var seat1 = activeRegularSeats[position1];
        var seat2 = activeRegularSeats[position2];
        
        if (seat1 == null || seat2 == null)
        {
            Debug.LogError($"[GovernmentLogic] Cannot swap null seats at positions {position1} and {position2}");
            return false;
        }
        
        // Remove both seats from the council list
        councilSeats.Remove(seat1);
        councilSeats.Remove(seat2);
        
        // Swap their positions
        seat1.seatIndex = position2;
        seat2.seatIndex = position1;
        
        // Update the active array
        activeRegularSeats[position1] = seat2;
        activeRegularSeats[position2] = seat1;
        
        // Add them back to the council list
        councilSeats.Add(seat1);
        councilSeats.Add(seat2);
        
        // Notify listeners
        OnCouncilSeatChanged?.Invoke(seat1);
        OnCouncilSeatChanged?.Invoke(seat2);
        
        GameLoggingSystem.Instance.LogEvent($"Swapped seats: {seat1.GetEffectiveTitle()} (pos {position1}) <-> {seat2.GetEffectiveTitle()} (pos {position2})", "GovernmentLogic");
        
        return true;
    }
    
    /// <summary>
    /// Check if a seat can be replaced at a specific position
    /// </summary>
    public bool CanReplaceSeatAtPosition(int position)
    {
        // Head of State (position -1) cannot be replaced
        if (position == -1)
        {
            Debug.LogWarning($"[GovernmentLogic] Head of State cannot be replaced with civics");
            return false;
        }
        
        if (position < 0 || position >= 6)
        {
            Debug.LogWarning($"[GovernmentLogic] Invalid position for seat replacement: {position}");
            return false;
        }
        
        var seat = activeRegularSeats[position];
        if (seat == null)
        {
            Debug.LogWarning($"[GovernmentLogic] No seat at position {position} to replace");
            return false;
        }
        
        if (!seat.isUnlocked)
        {
            Debug.LogWarning($"[GovernmentLogic] Cannot replace locked seat at position {position}");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Find a seat in the available pool by title
    /// </summary>
    private CouncilSeat FindSeatInPool(string seatTitle)
    {
        return availableSeatPool.Find(seat => seat.seatTitle == seatTitle);
    }
    
    /// <summary>
    /// Check if a seat is already in use at a different position
    /// </summary>
    private bool IsSeatInUseAtOtherPosition(string seatTitle, int excludePosition)
    {
        for (int i = 0; i < 6; i++)
        {
            if (i != excludePosition && 
                activeRegularSeats[i] != null && 
                activeRegularSeats[i].isUnlocked && 
                activeRegularSeats[i].seatTitle == seatTitle)
            {
                return true;
            }
        }
        return false;
    }
    

    
    /// <summary>
    /// Get the current seat at a specific position
    /// </summary>
    public CouncilSeat GetSeatAtPosition(int position)
    {
        if (position < 0 || position >= 6)
            return null;
            
        return activeRegularSeats[position];
    }
    
    /// <summary>
    /// Get all currently active regular seats (excluding Head of State)
    /// </summary>
    public CouncilSeat[] GetActiveRegularSeats()
    {
        return activeRegularSeats;
    }
    
    /// <summary>
    /// Get the number of currently unlocked seats
    /// </summary>
    public int GetUnlockedSeatCount()
    {
        return unlockedSeatCount;
    }

    /// <summary>
    /// Replace a regular seat with a civic seat
    /// </summary>
    public bool ReplaceSeatWithCivic(int position, string civicName)
    {
        if (!CanReplaceSeatAtPosition(position))
            return false;
            
        if (!civicCouncilSeats.ContainsKey(civicName))
        {
            Debug.LogWarning($"[GovernmentLogic] Civic '{civicName}' not found or doesn't grant a council position");
            return false;
        }
        
        var civicSeat = civicCouncilSeats[civicName];
        
        // Check if this civic seat is already in use at another position
        if (IsCivicSeatInUseAtOtherPosition(civicName, position))
        {
            Debug.LogWarning($"[GovernmentLogic] Civic seat '{civicName}' is already in use at another position");
            return false;
        }
        
        // Perform the replacement
        ReplaceSeatAtPosition(position, civicSeat);
        
        GameLoggingSystem.Instance.LogEvent($"Successfully replaced seat at position {position} with civic '{civicName}'", "GovernmentLogic");
        
        return true;
    }
    
    /// <summary>
    /// Check if a civic seat is already in use at a different position
    /// </summary>
    private bool IsCivicSeatInUseAtOtherPosition(string civicName, int excludePosition)
    {
        for (int i = 0; i < 6; i++)
        {
            if (i != excludePosition && 
                activeRegularSeats[i] != null && 
                activeRegularSeats[i].isUnlocked && 
                activeRegularSeats[i].sourceCivic != null &&
                activeRegularSeats[i].sourceCivic.civicName == civicName)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get all available civic seat names
    /// </summary>
    public List<string> GetAvailableCivicSeatNames()
    {
        var names = new List<string>();
        foreach (var kvp in civicCouncilSeats)
        {
            names.Add(kvp.Key);
        }
        return names;
    }
    
    // ===== UTILITY METHODS FOR SEAT MANAGEMENT =====
    
    /// <summary>
    /// Get comprehensive information about the current seat configuration
    /// </summary>
    public (int unlockedCount, int totalPositions, List<string> availableSeats, List<string> activeSeats) GetSeatConfigurationInfo()
    {
        var availableSeatTitles = GetAvailableSeatTitles();
        var activeSeatTitles = new List<string>();
        
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null && seat.isUnlocked)
            {
                activeSeatTitles.Add($"{i}: {seat.GetEffectiveTitle()}");
            }
        }
        
        return (unlockedSeatCount, 6, availableSeatTitles, activeSeatTitles);
    }
    
    /// <summary>
    /// Check if a specific seat title is currently active at any position
    /// </summary>
    public bool IsSeatTitleActive(string seatTitle)
    {
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null && seat.isUnlocked && seat.seatTitle == seatTitle)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get the position where a specific seat title is currently active
    /// </summary>
    public int GetSeatPosition(string seatTitle)
    {
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null && seat.isUnlocked && seat.seatTitle == seatTitle)
            {
                return i;
            }
        }
        return -1; // Not found
    }
    
    /// <summary>
    /// Reset a position to use a default seat template
    /// </summary>
    public bool ResetSeatToDefault(int position)
    {
        if (!CanReplaceSeatAtPosition(position))
        {
            Debug.LogWarning($"[GovernmentLogic] Cannot reset seat at position {position} - not replaceable");
            return false;
        }
            
        // Find an available default template
        var availableTemplate = FindAvailableDefaultTemplate();
        if (availableTemplate == null)
        {
            Debug.LogWarning($"[GovernmentLogic] No available default templates to reset position {position}");
            return false;
        }
        
        var currentSeat = GetSeatAtPosition(position);
        string currentTitle = currentSeat?.GetEffectiveTitle() ?? "null";
        string currentCivic = currentSeat?.sourceCivic?.civicName ?? "none";
        string currentLegend = currentSeat?.assignedLegend?.legendName ?? "none";
        
        GameLoggingSystem.Instance.LogEvent($"Resetting position {position} from '{currentTitle}' (Civic: {currentCivic}, Legend: {currentLegend}) to default seat: {availableTemplate.seatTitle}", "GovernmentLogic");
        
        // Perform the replacement
        ReplaceSeatAtPosition(position, availableTemplate);
        
        GameLoggingSystem.Instance.LogEvent($"Successfully reset position {position} to default seat: {availableTemplate.seatTitle}", "GovernmentLogic");
        
        return true;
    }
    
    /// <summary>
    /// Get all positions that can currently be replaced
    /// </summary>
    public List<int> GetReplaceablePositions()
    {
        var positions = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            if (CanReplaceSeatAtPosition(i))
            {
                positions.Add(i);
            }
        }
        return positions;
    }
    
    /// <summary>
    /// Get detailed information about all seat positions for UI display
    /// </summary>
    public List<(int position, string title, string type, bool isUnlocked, bool hasLegend, string legendName)> GetDetailedSeatInfo()
    {
        var seatInfo = new List<(int, string, string, bool, bool, string)>();
        
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null)
            {
                string type = seat.sourceCivic != null ? $"Civic: {seat.sourceCivic.civicName}" : "Default";
                string legendName = seat.assignedLegend != null ? seat.assignedLegend.legendName : "";
                
                seatInfo.Add((i, seat.GetEffectiveTitle(), type, seat.isUnlocked, seat.assignedLegend != null, legendName));
            }
        }
        
        return seatInfo;
    }
    
    /// <summary>
    /// Get all available replacement options for a specific position
    /// </summary>
    public List<(string title, string type, bool isCurrentlyActive)> GetReplacementOptions(int position)
    {
        var options = new List<(string, string, bool)>();
        
        foreach (var seat in availableSeatPool)
        {
            string type = seat.sourceCivic != null ? $"Civic: {seat.sourceCivic.civicName}" : "Default";
            bool isCurrentlyActive = IsSeatTitleActive(seat.seatTitle);
            
            options.Add((seat.seatTitle, type, isCurrentlyActive));
        }
        
        return options;
    }
    
    /// <summary>
    /// Debug method to unlock the next available seat
    /// </summary>
    private void UnlockNextAvailableSeat()
    {
        if (unlockedSeatCount >= 6)
        {
            GameLoggingSystem.Instance.LogEvent("All 6 regular seats are already unlocked!", "GovernmentLogic");
            return;
        }
        
        // Find next locked position
        for (int i = 0; i < 6; i++)
        {
            var seat = activeRegularSeats[i];
            if (seat != null && !seat.isUnlocked)
            {
                GameLoggingSystem.Instance.LogEvent($"Unlocking seat at position {i}...", "GovernmentLogic");
                bool success = UnlockCouncilSeat(i);
                GameLoggingSystem.Instance.LogEvent($"Unlock result: {(success ? "SUCCESS" : "FAILED")}", "GovernmentLogic");
                break;
            }
        }
    }
    
    /// <summary>
    /// Centralized method to unlock a civic
    /// This delegates to CivicManager and ensures proper council seat integration
    /// </summary>
    public bool UnlockCivic(string civicName, string source = "GovernmentLogic")
    {
        if (string.IsNullOrEmpty(civicName))
        {
            Debug.LogWarning("[GovernmentLogic] Cannot unlock civic with null or empty name");
            return false;
        }
        
        // Check if civic is already unlocked
        if (IsCivicUnlocked(civicName))
        {

            GameLoggingSystem.Instance.LogEvent($"[GovernmentLogic] Civic '{civicName}' is already unlocked", "GovernmentLogic");
            return true; // Already unlocked
        }
        
        // Delegate to CivicManager for unlocking logic
        if (CivicManager.Instance == null)
        {
            Debug.LogError("[GovernmentLogic] CivicManager not found - cannot unlock civic");
            return false;
        }
        
        bool unlockSuccess = CivicManager.Instance.UnlockCivic(civicName, source);
        
        if (unlockSuccess)
        {
            GameLoggingSystem.Instance.LogEvent($"Successfully unlocked civic '{civicName}' from '{source}'", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"Civic is now available for council seat replacement", "GovernmentLogic");
        }
        else if (!unlockSuccess)
        {
            Debug.LogWarning($"[GovernmentLogic] Failed to unlock civic '{civicName}' from '{source}'");
        }
        
        return unlockSuccess;
    }
    
    /// <summary>
    /// Check if a civic can be unlocked (without actually unlocking it)
    /// </summary>
    public (bool canUnlock, List<string> reasons) CanUnlockCivic(string civicName)
    {
        if (CivicManager.Instance == null)
        {
            return (false, new List<string> { "CivicManager not found" });
        }
        
        return CivicManager.Instance.CanUnlockCivic(civicName);
    }
    
    /// <summary>
    /// Get all civics that are loaded but not yet unlocked
    /// </summary>
    public List<string> GetLockedCivicNames()
    {
        if (CivicManager.Instance == null)
        {
            return new List<string>();
        }
        
        var allCivics = CivicManager.Instance.GetAllAvailableCivicNames();
        var unlockedCivics = GetUnlockedCivicNames();
        
        return allCivics.Except(unlockedCivics).ToList();
    }
    
    /// <summary>
    /// Get comprehensive civic status information
    /// </summary>
    public (List<string> unlocked, List<string> locked, List<string> availableForSeats) GetCivicStatus()
    {
        var unlocked = GetUnlockedCivicNames();
        var locked = GetLockedCivicNames();
        var availableForSeats = GetAvailableSeatTitles();
        
        return (unlocked, locked, availableForSeats);
    }
    
    
    /// <summary>
    /// Show the complete civic pipeline from loading to unlocking to seat availability
    /// </summary>
    public void LogCivicPipeline()
    {
        if (!GameLoggingSystem.Instance.enableGovernmentLogicLogging) return;
        
        GameLoggingSystem.Instance.LogEvent("=== COMPLETE CIVIC PIPELINE ===", "GovernmentLogic");
        
        if (CivicManager.Instance == null)
        {
            GameLoggingSystem.Instance.LogEvent("CivicManager not found!", "GovernmentLogic");
            return;
        }
        
        // Step 1: Loaded from Resources
        var loadedCivics = CivicManager.Instance.GetAllAvailableCivicNames();
        GameLoggingSystem.Instance.LogEvent($"STEP 1 - LOADED FROM RESOURCES: {loadedCivics.Count} civics", "GovernmentLogic");
        foreach (var civicName in loadedCivics)
        {
            GameLoggingSystem.Instance.LogEvent($"  📁 {civicName}", "GovernmentLogic");
        }
        
        // Step 2: Unlocked through requirements
        var unlockedCivics = GetUnlockedCivicNames();
        GameLoggingSystem.Instance.LogEvent($"STEP 2 - UNLOCKED THROUGH REQUIREMENTS: {unlockedCivics.Count} civics", "GovernmentLogic");
        foreach (var civicName in unlockedCivics)
        {
            GameLoggingSystem.Instance.LogEvent($"  🔓 {civicName}", "GovernmentLogic");
        }
        
        // Step 3: Available for seat replacement
        var availableSeats = GetAvailableSeatTitles();
        GameLoggingSystem.Instance.LogEvent($"STEP 3 - AVAILABLE FOR SEAT REPLACEMENT: {availableSeats.Count} options", "GovernmentLogic");
        foreach (var seatTitle in availableSeats)
        {
            string type = IsCivicSeatTitle(seatTitle) ? "Civic" : "Default";
            GameLoggingSystem.Instance.LogEvent($"  🪑 {seatTitle} ({type})", "GovernmentLogic");
        }
        
        // Step 4: Currently equipped in council
        var detailedSeats = GetDetailedSeatInfo();
        GameLoggingSystem.Instance.LogEvent($"STEP 4 - CURRENTLY EQUIPPED IN COUNCIL: {detailedSeats.Count} positions", "GovernmentLogic");
        foreach (var (position, title, type, isUnlocked, hasLegend, legendName) in detailedSeats)
        {
            string status = isUnlocked ? (hasLegend ? $"Legend: {legendName}" : "Empty") : "Locked";
            GameLoggingSystem.Instance.LogEvent($"  Position {position}: {title} ({type}) - {status}", "GovernmentLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent("=== PIPELINE SUMMARY ===", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Loaded: {loadedCivics.Count} → Unlocked: {unlockedCivics.Count} → Available for Seats: {availableSeats.Count} → Equipped: {detailedSeats.Count(s => s.isUnlocked)}", "GovernmentLogic");
    }
    
    /// <summary>
    /// Check if a seat title is from a civic (not a default seat)
    /// </summary>
    private bool IsCivicSeatTitle(string seatTitle)
    {
        foreach (var kvp in civicCouncilSeats)
        {
            if (kvp.Value.seatTitle == seatTitle)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get detailed information about a seat for UI display
    /// </summary>
    public (string title, string effects, string leaderClasses, Sprite icon) GetSeatDisplayInfo(string seatTitle)
    {
        // First check if it's a civic seat by searching through all civic seats for matching seat title
        foreach (var kvp in civicCouncilSeats)
        {
            if (kvp.Value.seatTitle == seatTitle)
            {
                var civicSeat = kvp.Value;
                var civic = civicSeat.sourceCivic;
                
                string effects;
                string leaderClasses = FormatLeaderClasses(civicSeat.allowedLegendClasses);
                Sprite icon;
                
                // Use new CivicCouncilPosition structure if available, otherwise fall back to legacy
                if (civic?.councilPosition != null)
                {
                    effects = FormatCivicSeatBonuses(civic.councilPosition.bonuses);
                    icon = civic.councilPosition.icon ?? civic.icon;
                }
                else
                {
                    // Legacy fallback
                    effects = FormatCivicEffects(civic);
                    icon = civic?.icon;
                }
                
                return (civicSeat.GetEffectiveTitle(), effects, leaderClasses, icon);
            }
        }
        
        // Check if it's a default seat template
        foreach (var template in defaultSeatTemplates)
        {
            if (template.seatTitle == seatTitle)
            {
                string effects = FormatSeatBonuses(template.seatBonuses);
                string leaderClasses = FormatLeaderClasses(template.allowedLegendClasses);
                
                return (template.seatTitle, effects, leaderClasses, template.seatIcon);
            }
        }
        
        return (seatTitle, "Effects: N/A", "Leader Classes: N/A", null);
    }
    
    /// <summary>
    /// Get the civic data for a given seat title
    /// </summary>
    public CivicData GetCivicDataForSeatTitle(string seatTitle)
    {
        // Search through all civic seats for matching seat title
        foreach (var kvp in civicCouncilSeats)
        {
            if (kvp.Value.seatTitle == seatTitle)
            {
                return kvp.Value.sourceCivic;
            }
        }
        
        return null; // Not a civic seat
    }
    

    
    /// <summary>
    /// Format civic effects for display
    /// </summary>
    private string FormatCivicEffects(CivicData civic)
    {
        if (civic == null || civic.effects == null || civic.effects.Count == 0)
            return "No effects";
        
        var effectStrings = new List<string>();
        foreach (var effect in civic.effects)
        {
            // Use the auto-generated description which already includes all necessary information
            string effectText = effect.GetAutoDescription();
            effectStrings.Add(effectText);
        }
        
        return string.Join("\n", effectStrings);
    }
    

    
    /// <summary>
    /// Format seat bonuses for display
    /// </summary>
    private string FormatSeatBonuses(List<SeatBonus> bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
            return "No bonuses";
        
        var bonusStrings = new List<string>();
        foreach (var bonus in bonuses)
        {
            // Use the auto-generated description which already includes all necessary information
            string bonusText = bonus.GetAutoDescription();
            bonusStrings.Add(bonusText);
        }
        
        return string.Join("\n", bonusStrings);
    }
    
    /// <summary>
    /// Format civic seat bonuses for display (new CivicCouncilPosition structure)
    /// </summary>
    private string FormatCivicSeatBonuses(CivicSeatBonus[] bonuses)
    {
        if (bonuses == null || bonuses.Length == 0)
            return "No bonuses";
        
        var bonusStrings = new List<string>();
        foreach (var bonus in bonuses)
        {
            if (bonus != null)
            {
                // Use the auto-generated description which already includes all necessary information
                string bonusText = bonus.GetAutoDescription();
                bonusStrings.Add(bonusText);
            }
        }
        
        return string.Join("\n", bonusStrings);
    }
    
    /// <summary>
    /// Format leader classes for display
    /// </summary>
    private string FormatLeaderClasses(List<LegendClass> allowedClasses)
    {
        if (allowedClasses == null || allowedClasses.Count == 0)
            return "No leader classes allowed";
        
        if (allowedClasses.Count == 6) // All 6 classes
            return "Council Position for Any/All Classes";
        
        if (allowedClasses.Count == 1)
        {
            return $"Council Position for {allowedClasses[0]}s";
        }
        
        // Multiple but not all classes
        var classNames = allowedClasses.Select(c => c.ToString() + "s").ToList();
        return $"Council Position for {string.Join(", ", classNames)}";
    }
    
    /// <summary>
    /// Log swap operation details for debugging
    /// </summary>
    private void LogSwapOperation(LegendData newLegend, CouncilSeat targetSeat, LegendData displacedLegend, CouncilSeat previousSeat, bool isPerfectSwap)
    {
        if (!GameLoggingSystem.Instance.enableGovernmentLogicLogging) return;
        string targetSeatTitle = targetSeat.seatIndex == -1 ? "Head of State" : targetSeat.GetEffectiveTitle();
        string previousSeatTitle = previousSeat.seatIndex == -1 ? "Head of State" : previousSeat.GetEffectiveTitle();
        string mode = isPerfectSwap ? "PERFECT" : "STANDARD";
        GameLoggingSystem.Instance.LogEvent($"Swap {mode}: {newLegend.legendName} → {targetSeatTitle}, displaced: {displacedLegend.legendName} → {previousSeatTitle}", "GovernmentLogic");
    }

    /// <summary>
    /// Convert civic effect type to seat bonus type
    /// </summary>
    private SeatBonusType ConvertCivicEffectTypeToSeatBonusType(GameEffectType civicEffectType)
    {
        switch (civicEffectType)
        {
            case GameEffectType.PillarBonus: return SeatBonusType.PillarBonus;
            case GameEffectType.SubstatBonus: return SeatBonusType.SubstatBonus;
            case GameEffectType.DerivedStatBonus: return SeatBonusType.DerivedStatBonus;
            case GameEffectType.ResourceModifier: return SeatBonusType.ResourceModifier;
            case GameEffectType.ProductionModifier: return SeatBonusType.ProductionModifier;
            case GameEffectType.ClickPowerBonus: return SeatBonusType.CivicBonus;
            case GameEffectType.MaxMoraleModifier: return SeatBonusType.MaxMoraleModifier;
            case GameEffectType.MoraleBalanceModifier: return SeatBonusType.MoraleBalanceModifier;
            case GameEffectType.SatisfactionThresholdModifier: return SeatBonusType.SatisfactionThresholdModifier;
            case GameEffectType.HousingBonus: return SeatBonusType.HousingBonus;
            case GameEffectType.ProductionScalingBonus: return SeatBonusType.ProductionScalingBonus;
            case GameEffectType.ConstructionCostModifier: return SeatBonusType.ConstructionCostModifier;
            case GameEffectType.SpecialAbility: return SeatBonusType.SpecialAbility;
            default: return SeatBonusType.CivicBonus;
        }
    }
    
    /// <summary>
    /// Debug method to print detailed information about all council seats and their bonuses
    /// </summary>
    [ContextMenu("Debug All Council Seats")]
    public void DebugAllCouncilSeats()
    {
        GameLoggingSystem.Instance.LogEvent("=== COUNCIL SEAT DEBUG REPORT ===", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Total seats: {councilSeats.Count}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Unlocked seats: {GetUnlockedCouncilSeatCount()}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"StatManager.Instance: {(StatManager.Instance != null ? "Available" : "NULL")}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Processing bonuses lock: {isProcessingSeatBonuses}", "GovernmentLogic");
        
        for (int i = 0; i < councilSeats.Count; i++)
        {
            var seat = councilSeats[i];
            GameLoggingSystem.Instance.LogEvent($"\n--- SEAT {i}: {seat.seatTitle} ---", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Unlocked: {seat.isUnlocked}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Has Legend: {seat.assignedLegend != null}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Legend Name: {(seat.assignedLegend != null ? seat.assignedLegend.legendName : "None")}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Sevenths Until Active: {seat.seventhsUntilActive}", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Is Active: {seat.IsActive()} (requires: legend != null && seventhsUntilActive <= 0)", "GovernmentLogic");
            GameLoggingSystem.Instance.LogEvent($"  Bonus Count: {seat.seatBonuses.Count}", "GovernmentLogic");
            
            for (int j = 0; j < seat.seatBonuses.Count; j++)
            {
                var bonus = seat.seatBonuses[j];
                GameLoggingSystem.Instance.LogEvent($"    Bonus {j}: {bonus.bonusType} | Target: '{bonus.targetStat}' | Value: {bonus.modifierValue} | Type: {bonus.modifierType} | Requires Legend: {bonus.requiresLegend}", "GovernmentLogic");
            }
        }
        
        // Show current active bonuses
        GameLoggingSystem.Instance.LogEvent("\n=== CURRENT ACTIVE BONUSES ===", "GovernmentLogic");
        if (StatManager.Instance != null)
        {
            foreach (var pillarName in new[] { "aureus", "regalia", "waltz", "chorus" })
            {
                float bonus = StatManager.Instance.GetPillarBonus(pillarName);
                if (bonus != 0)
                {
                    var sources = StatManager.Instance.GetBonusSources("pillar", pillarName);
                    GameLoggingSystem.Instance.LogEvent($"  {pillarName}: +{bonus:F1} from {sources.Count} sources: {string.Join(", ", sources.Select(kvp => $"'{kvp.Key}'(+{kvp.Value:F1})"))}", "GovernmentLogic");
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"  {pillarName}: No bonuses", "GovernmentLogic");
                }
            }
        }
        
        // Force process all seat bonuses to see what happens
        GameLoggingSystem.Instance.LogEvent("\n=== FORCING SEAT BONUS PROCESSING ===", "GovernmentLogic");
        ProcessAllSeatBonuses();
        
        GameLoggingSystem.Instance.LogEvent("=== END COUNCIL SEAT DEBUG REPORT ===", "GovernmentLogic");
    }
    
    /// <summary>
    /// Debug method to force clear all seat bonuses and reapply only active ones (fixes stacking bugs)
    /// </summary>
    [ContextMenu("Force Clear and Reapply All Seat Bonuses")]
    public void ForceClearAndReapplyAllSeatBonuses()
    {
        GameLoggingSystem.Instance.LogEvent("=== FORCE CLEAR AND REAPPLY SEAT BONUSES ===", "GovernmentLogic");
        
        // Step 1: Clear ALL seat bonuses from StatManager
        if (StatManager.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent("Step 1: Clearing all Council Seat bonuses from StatManager", "GovernmentLogic");
            var allBonusSources = new List<string>();
            
            // Get all pillar bonus sources that contain "Council Seat:"
            foreach (var pillarName in new[] { "aureus", "regalia", "waltz", "chorus" })
            {
                var sources = StatManager.Instance.GetBonusSources("pillar", pillarName);
                foreach (var source in sources.Keys)
                {
                    if (source.StartsWith("Council Seat:") && !allBonusSources.Contains(source))
                    {
                        allBonusSources.Add(source);
                    }
                }
            }
            
            GameLoggingSystem.Instance.LogEvent($"Found {allBonusSources.Count} Council Seat bonus sources to clear: {string.Join(", ", allBonusSources.Select(s => $"'{s}'"))}", "GovernmentLogic");
            
            // Clear each source
            foreach (var source in allBonusSources)
            {
                GameLoggingSystem.Instance.LogEvent($"Clearing bonus source: '{source}'", "GovernmentLogic");
                StatManager.Instance.ClearBonusesFromSource(source);
            }
        }
        
        // Step 2: Reapply bonuses for all currently active seats
        GameLoggingSystem.Instance.LogEvent("\nStep 2: Reapplying bonuses for currently active seats", "GovernmentLogic");
        ProcessAllSeatBonuses();
        
        // Step 3: Show final state
        GameLoggingSystem.Instance.LogEvent("\nStep 3: Final bonus state after cleanup:", "GovernmentLogic");
        DebugAllCouncilSeats();
    }
    
    /// <summary>
    /// Debug method to show current cooldown state and force reset if needed
    /// </summary>
    [ContextMenu("Debug Cooldown System")]
    public void DebugCooldownSystem()
    {
        GameLoggingSystem.Instance.LogEvent("=== COOLDOWN SYSTEM DEBUG ===", "GovernmentLogic");
        
        // Show current cooldown state
        GameLoggingSystem.Instance.LogEvent($"Head of State cooldown remaining: {headOfStateCooldownRemaining}", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"Regular seat cooldowns: {seatCooldownRemaining.Count} seats on cooldown", "GovernmentLogic");
        
        foreach (var kvp in seatCooldownRemaining)
        {
            GameLoggingSystem.Instance.LogEvent($"  Seat {kvp.Key}: {kvp.Value} sevenths remaining", "GovernmentLogic");
        }
        
        // Show which seats can be changed
        Debug.Log("\nSeat change availability:");
        for (int i = -1; i < 6; i++)
        {
            bool canChange = CanChangeSeat(i);
            int remaining = GetSeatCooldownRemaining(i);
            string seatName = (i == -1) ? "Head of State" : $"Seat {i}";
            GameLoggingSystem.Instance.LogEvent($"  {seatName}: {(canChange ? "READY" : $"COOLDOWN ({remaining} sevenths)")}", "GovernmentLogic");
        }
        
        // Show base cooldown values
        GameLoggingSystem.Instance.LogEvent($"\nBase cooldown values:", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Head of State: {headOfStateCooldownSevenths} sevenths", "GovernmentLogic");
        GameLoggingSystem.Instance.LogEvent($"  Regular seats: {councilSeatCooldownSevenths} sevenths", "GovernmentLogic");
        
        GameLoggingSystem.Instance.LogEvent("=== END COOLDOWN SYSTEM DEBUG ===", "GovernmentLogic");
    }
    
    /// <summary>
    /// Debug method to force reset all cooldowns (emergency function)
    /// </summary>
    [ContextMenu("Force Reset All Cooldowns")]
    public void DebugForceResetAllCooldowns()
    {
        Debug.Log("=== FORCE RESET ALL COOLDOWNS ===");
        ForceResetAllCooldowns();
        Debug.Log("All cooldowns have been reset to 0 - all seats are now ready for changes");
    }
}
