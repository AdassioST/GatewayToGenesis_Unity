using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

/// <summary>
/// Core event system for managing narrative events with Ink integration
/// </summary>
public class EventSystemLogic : MonoBehaviour
{
    // ===== THE BOSS'S OFFICE =====
    // This is where the boss (EventSystem) keeps track of everything
    
    [Header("Event System Settings")]
    [SerializeField] private bool enableEventLogging = true; // Whether to log what the boss is doing
    
    [Header("Event Data")]
    [SerializeField] private List<EventScore> eventScores = new List<EventScore>(); // The scoreboard tracking player progress
    
    [Header("System References")]
    [SerializeField] private GameUnitsLogic gameUnitsLogic; // Direct reference to resource/production manager
    [SerializeField] private StatManager statManager; // Direct reference to stats manager
    [SerializeField] private TimeSystemLogic timeSystem; // Direct reference to time system
    [SerializeField] private EventVolumeManager volumeManager; // Direct reference to volume manager
    
    // ===== THE BOSS'S ID CARD =====
    // Singleton pattern - makes sure there's only ONE boss in the whole game
    private static EventSystemLogic instance;
    public static EventSystemLogic Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EventSystemLogic>();
                if (instance == null)
                {
                    GameObject go = new GameObject("EventSystem");
                    instance = go.AddComponent<EventSystemLogic>();
                }
            }
            return instance;
        }
    }
    
    // ===== THE BOSS'S CURRENT TASK =====
    // What story is the boss currently telling?
    private StoryNode currentStoryNode; // The story node currently being executed
    public bool isEventActive = false; // Is the boss busy telling a story?
    
    // ===== THE BOSS'S FIRST TIME EXPERIENCE =====
    // Track if this is the very first time the system has processed time changes
    // Start with 0 so first change makes it 1, allowing events on the second seventh change
    private int seventhChangeCount = 0; // Count of seventh changes, 0 = no events, 1+ = events allowed
    
    // ===== THE BOSS'S HELPERS =====
    // The boss has two helpers: the story teller and the magic book keeper
    private EventScreenManager screenManager; // The story teller who shows the pages
    private InkStoryManager inkManager; // The magic book keeper who provides the text
    
    // ===== THE BOSS STARTS WORK =====
    // When the game starts, the boss sets up their office and hires their helpers
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // Ensure this GameObject is a root object before marking as persistent
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject); // Boss stays even when scenes change
            InitializeEventSystem(); // Boss hires their helpers
        }
        else if (instance != this)
        {
            Destroy(gameObject); // Only one boss allowed!
        }
    }
    
    // ===== THE BOSS STARTS THEIR DAILY ROUTINE =====
    // The boss starts checking for stories every second
    private void Start()
    {
        // PSEUDOCODE: No longer need arbitrary time loop - events trigger based on time system
        // StartCoroutine(EventCheckLoop()); // Boss starts their daily routine
    }
    
    // ===== THE BOSS'S TIME-BASED EVENT TRIGGERS =====
    // PSEUDOCODE: The boss now responds to the magical time system instead of arbitrary intervals
    
    // PSEUDOCODE: Check for events when seventh changes
    private void OnSeventhChanged(int newSeventh)
    {
        // Increment the seventh change count
        seventhChangeCount++;
        
        // Events are only allowed after the first seventh change (when count reaches 1)
        if (seventhChangeCount < 1)
        {
            LogEvent($"Seventh {newSeventh} - change count {seventhChangeCount}, skipping event check (system initialization protection)");
            return;
        }
        
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            LogEvent($"Seventh changed to {newSeventh} - change count {seventhChangeCount}, checking for events"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when phase changes
    private void OnPhaseChanged(int newPhase)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            LogEvent($"Phase changed to {newPhase} - checking for events"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when echo changes
    private void OnEchoChanged(int newEcho)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            LogEvent($"Echo changed to {newEcho} - checking for events"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when cycle changes
    private void OnCycleChanged(int newCycle)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            LogEvent($"Cycle changed to {newCycle} - checking for events"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events on ritual sevenths (special magical moments)
    private void OnRitualSeventh(int ritualSeventh)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            LogEvent($"Ritual Seventh {ritualSeventh} - checking for special events"); // Boss announces ritual
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // ===== THE BOSS HIRES THEIR HELPERS =====
    // The boss finds and hires the story teller and magic book keeper
    private void InitializeEventSystem()
    {
        // Hire the story teller (EventScreenManager)
        screenManager = GetComponent<EventScreenManager>();
        if (screenManager == null)
        {
            screenManager = gameObject.AddComponent<EventScreenManager>(); // Create if doesn't exist
        }
        
        // Hire the magic book keeper (InkStoryManager)
        inkManager = GetComponent<InkStoryManager>();
        if (inkManager == null)
        {
            inkManager = gameObject.AddComponent<InkStoryManager>(); // Create if doesn't exist
        }
        
        // PSEUDOCODE: Find system references if not assigned in editor
        if (gameUnitsLogic == null)
        {
            gameUnitsLogic = FindObjectOfType<GameUnitsLogic>(); // Find resource manager if not assigned
            if (gameUnitsLogic != null)
            {
                LogEvent("Auto-assigned GameUnitsLogic reference"); // Boss announces the assignment
            }
        }
        
        if (statManager == null)
        {
            statManager = FindObjectOfType<StatManager>(); // Find stats manager if not assigned
            if (statManager != null)
            {
                LogEvent("Auto-assigned StatManager reference"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Find time system reference if not assigned in editor
        if (timeSystem == null)
        {
            timeSystem = FindObjectOfType<TimeSystemLogic>(); // Find time system if not assigned
            if (timeSystem != null)
            {
                LogEvent("Auto-assigned TimeSystemLogic reference"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Find volume manager reference if not assigned in editor
        if (volumeManager == null)
        {
            volumeManager = FindObjectOfType<EventVolumeManager>(); // Find volume manager if not assigned
            if (volumeManager != null)
            {
                LogEvent("Auto-assigned EventVolumeManager reference"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Subscribe to time system events for thematic event triggering
        if (timeSystem != null)
        {
            // Wait for all systems to be ready before subscribing to time events
            StartCoroutine(WaitForSystemsAndSubscribeToTimeEvents());
            LogEvent("Started coroutine to wait for systems and subscribe to time events");
        }
        else
        {
            LogEvent("Time system not found - cannot subscribe to time events");
        }
        
        LogEvent("Event System Logic initialized"); // Boss announces they're ready for work
    }
    
    /// <summary>
    /// Wait for all systems to be ready before subscribing to time system events
    /// </summary>
    private IEnumerator WaitForSystemsAndSubscribeToTimeEvents()
    {
        // Wait for all systems to be ready
        while (!AreSystemsReady())
        {
            LogEvent("Waiting for systems to be ready...");
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }
        
        LogEvent("All systems ready - subscribing to time system events");
        
        // Now subscribe to time system events
        timeSystem.OnPhaseChange += OnPhaseChanged; // PSEUDOCODE: Check events when phase changes
        timeSystem.OnEchoChange += OnEchoChanged; // PSEUDOCODE: Check events when echo changes
        timeSystem.OnCycleChange += OnCycleChanged; // PSEUDOCODE: Check events when cycle changes
        timeSystem.OnRitualSeventh += OnRitualSeventh; // PSEUDOCODE: Check events on ritual sevenths
        timeSystem.OnSeventhChange += OnSeventhChanged; // Check events when seventh changes
        LogEvent("Subscribed to time system events");
    }
    
    /// <summary>
    /// Check if all required systems are ready for events
    /// </summary>
    private bool AreSystemsReady()
    {
        if (PopGrowthLogic.Instance == null)
        {
            LogEvent("PopGrowthLogic.Instance is null - population system not ready");
            return false;
        }
        
        if (gameUnitsLogic == null)
        {
            LogEvent("GameUnitsLogic is null - resource system not ready");
            return false;
        }
        
        if (statManager == null)
        {
            LogEvent("StatManager is null - stats system not ready");
            return false;
        }
        
        if (timeSystem == null)
        {
            LogEvent("TimeSystemLogic is null - time system not ready");
            return false;
        }
        
        if (volumeManager == null)
        {
            LogEvent("EventVolumeManager is null - event system not ready");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check all volumes and trigger the highest priority story that meets conditions
    /// </summary>
    private void CheckForAvailableEvents()
    {
        if (!AreSystemsReady())
        {
            LogEvent("Systems not ready - cannot check for events");
            return;
        }
        
        StoryNode bestStory = volumeManager.FindBestAvailableStory(); // Find the best story to tell
        
        if (bestStory != null) // Did we find a story to tell?
        {
            TriggerStory(bestStory); // Start telling the best story!
        }
    }
    
    // ===== THE BOSS STARTS TELLING A STORY =====
    // The boss picks up a storybook and starts reading it to the player
    /// <summary>
    /// Trigger a specific story node
    /// </summary>
    public void TriggerStory(StoryNode storyNode)
    {
        if (isEventActive) // Is the boss already telling a story?
        {
            LogEvent($"Cannot trigger story {storyNode.storyTitle}: another event is active");
            return; // Boss is busy, can't start another story
        }
        
        if (!AreSystemsReady())
        {
            LogEvent($"Cannot trigger story {storyNode.storyTitle}: systems not ready");
            return;
        }
        
        currentStoryNode = storyNode; // Remember which story we're telling
        isEventActive = true; // Mark that boss is now busy
        
        LogEvent($"Triggering story: {storyNode.storyTitle}"); // Boss announces they're starting
        
        // Pause time when event starts
        if (timeSystem != null)
        {
            timeSystem.PauseTime(true);
            LogEvent("Time paused for event");
        }
        
        // Remember tab states and hide all tabs for the event
        TabHotkeys hotkeys = FindObjectOfType<TabHotkeys>();
        if (hotkeys != null)
        {
            hotkeys.RememberTabStatesAndHideForEvent();
        }
        
        // Start the story through the volume manager
        if (volumeManager != null)
        {
            volumeManager.StartStory(storyNode, volumeManager.GetCurrentVolume());
        }
    }
    
    // ===== THE BOSS SHOWS ONE PAGE AT A TIME =====
    // The boss shows one page, waits for the player to finish, then turns to the next page
    /// <summary>
    /// Execute a single screen
    /// </summary>
    public void ExecuteScreen(EventScreen screen)
    {
        LogEvent($"Executing screen: {screen.screenType}");
        screenManager.ShowScreen(screen);
    }
    
    // ===== THE BOSS'S SCOREBOARD =====
    // The boss keeps track of how far the player has gotten in each story
    // Event Score Management
    public void ModifyEventScore(string scoreName, int change)
    {
        EventScore score = GetOrCreateEventScore(scoreName); // Find or create the score
        score.value += change; // Add or subtract from the score
        LogEvent($"Event score '{scoreName}' changed by {change} (new value: {score.value})"); // Boss announces the change
    }
    
    public int GetEventScore(string scoreName)
    {
        EventScore score = GetEventScoreByName(scoreName); // Find the score
        return score?.value ?? 0; // Return the value, or 0 if not found
    }
    
    private EventScore GetOrCreateEventScore(string scoreName)
    {
        EventScore score = GetEventScoreByName(scoreName); // Try to find existing score
        if (score == null) // If score doesn't exist
        {
            score = new EventScore { name = scoreName, value = 0 }; // Create new score starting at 0
            eventScores.Add(score); // Add it to the scoreboard
        }
        return score;
    }
    
    private EventScore GetEventScoreByName(string scoreName)
    {
        return eventScores.Find(s => s.name == scoreName); // Find score by name
    }
    
    // ===== THE BOSS'S CONSEQUENCE TRACKER =====
    // The boss keeps track of all consequences that happen during a story
    private List<EventConsequence> cumulativeConsequences = new List<EventConsequence>();
    // Timed consequences tracked with remaining sevenths
    private class TimedConsequence
    {
        public EventConsequence consequence;
        public int remainingSevenths;
    }
    private List<TimedConsequence> activeTimed = new List<TimedConsequence>();
    
    /// <summary>
    /// Add a consequence to the cumulative list (called during story flow)
    /// </summary>
    public void AddConsequence(EventConsequence consequence)
    {
        if (consequence != null)
        {
            cumulativeConsequences.Add(consequence);
            LogEvent($"Added consequence: {consequence.type} {consequence.targetName} {consequence.value}");
        }
    }
    
    /// <summary>
    /// Get all cumulative consequences for display
    /// </summary>
    public List<EventConsequence> GetCumulativeConsequences()
    {
        return new List<EventConsequence>(cumulativeConsequences);
    }
    
    /// <summary>
    /// Clear all cumulative consequences (called when story ends)
    /// </summary>
    public void ClearCumulativeConsequences()
    {
        cumulativeConsequences.Clear();
        LogEvent("Cleared cumulative consequences");
    }
    
    // ===== THE BOSS'S PUBLIC OFFICE =====
    // Other parts of the game can ask the boss questions or give them new stories
    
    // Public API
    public void AddVolume(EventVolume volume)
    {
        if (volumeManager != null)
        {
            volumeManager.AddVolume(volume); // Add the new volume to the manager
            LogEvent($"Added volume: {volume.volumeName}"); // Boss announces the new volume
        }
    }
    
    public bool IsEventActive()
    {
        return isEventActive; // Tell others if the boss is currently telling a story
    }
    
    public StoryNode GetCurrentStoryNode() => currentStoryNode;
    
    // Public accessors for system references
    public StatManager GetStatManager() => statManager;
    public GameUnitsLogic GetGameUnitsLogic() => gameUnitsLogic;
    public TimeSystemLogic GetTimeSystem() => timeSystem;
    public EventVolumeManager GetVolumeManager() => volumeManager;
    public EventScreenManager GetScreenManager() => screenManager;
    
    /// <summary>
    /// Public method to manually trigger event checking (for testing and debugging)
    /// </summary>
    public void TriggerEventCheck()
    {
        if (isEventActive) // If boss is busy, don't check for new events
        {
            return;
        }
        
        if (!AreSystemsReady())
        {
            LogEvent("Systems not ready - cannot check for events");
            return;
        }
        
        // Respect system initialization - don't trigger events until first seventh change has passed
        if (seventhChangeCount < 1)
        {
            LogEvent($"System not yet initialized (change count: {seventhChangeCount}) - skipping event check until first seventh change passes");
            return;
        }
        
        // Check for available events
        CheckForAvailableEvents();
    }
    
    /// <summary>
    /// Called when a story completes to resume time and clean up
    /// </summary>
    public void OnStoryCompleted()
    {
        LogEvent("Story completed - resuming time and cleaning up");
        
        // Hide the current event screen first
        if (screenManager != null)
        {
            screenManager.HideCurrentScreen();
            LogEvent("Event screen hidden");
        }
        
        // Apply cumulative consequences from the entire story flow
        if (cumulativeConsequences.Count > 0)
        {
            if (AreSystemsReady())
            {
                LogEvent($"Applying {cumulativeConsequences.Count} cumulative consequences from story flow");
                foreach (EventConsequence consequence in cumulativeConsequences)
                {
                    if (IsTimedEligible(consequence) && consequence.durationSevenths > 0)
                    {
                        // Apply once and register for expiration
                        ApplyConsequence(consequence);
                        activeTimed.Add(new TimedConsequence { consequence = consequence, remainingSevenths = consequence.durationSevenths });
                    }
                    else
                    {
                        ApplyConsequence(consequence);
                    }
                }
            }
            else
            {
                LogEvent($"Cannot apply cumulative consequences: systems not ready");
            }
        }
        
        // Clear the cumulative consequences for the next story
        ClearCumulativeConsequences();
        
        // Resume time when event ends
        if (timeSystem != null)
        {
            timeSystem.PauseTime(false);
            LogEvent("Time resumed after event");
            // Ensure we are subscribed to seventh changes for timed expirations
            timeSystem.OnSeventhChange -= OnTimedSeventh; // avoid dup
            timeSystem.OnSeventhChange += OnTimedSeventh;
        }
        
        // Restore previous tab states and HUD visibility
        TabHotkeys hotkeys = FindObjectOfType<TabHotkeys>();
        if (hotkeys != null)
        {
            hotkeys.RestoreTabStatesAfterEvent();
            LogEvent("Tab states and HUD restored");
        }
        else
        {
            LogEvent("ERROR: TabHotkeys not found for restoration");
        }
        
        // Reset event state
        isEventActive = false;
        currentStoryNode = null;
    }

    private bool IsTimedEligible(EventConsequence c)
    {
        switch (c.type)
        {
            case EventConsequence.ConsequenceType.ProductionPercentChange:
            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
            case EventConsequence.ConsequenceType.ClickPowerChange:
            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                return true;
            default:
                return false;
        }
    }

    private void OnTimedSeventh(int currentSeventh)
    {
        if (activeTimed.Count == 0) return;
        // Decrement and expire any that hit 0; for expiration we remove the same effect
        for (int i = activeTimed.Count - 1; i >= 0; i--)
        {
            var t = activeTimed[i];
            t.remainingSevenths = Mathf.Max(0, t.remainingSevenths - 1);
            if (t.remainingSevenths == 0)
            {
                RevertTimedConsequence(t.consequence);
                activeTimed.RemoveAt(i);
            }
        }
    }

    private void RevertTimedConsequence(EventConsequence c)
    {
        // Revert by removing the source entry used when applying (story title)
        string source = currentStoryNode != null ? currentStoryNode.storyTitle : "EventConsequence";
        switch (c.type)
        {
            case EventConsequence.ConsequenceType.ProductionPercentChange:
                if (GlobalProductionManager.Instance != null)
                {
                    bool isPositive = c.value >= 0;
                    GlobalProductionManager.Instance.AdjustPercentageModifier(c.targetName, Mathf.Abs(c.value), isPositive, false, source);
                }
                break;
            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
                if (GlobalProductionManager.Instance != null)
                {
                    bool isPositive = c.value >= 0;
                    GlobalProductionManager.Instance.AdjustPercentageModifierForSection(c.targetName, Mathf.Abs(c.value), isPositive, false, source);
                }
                break;
            case EventConsequence.ConsequenceType.ClickPowerChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPower(c.targetName, -c.value);
                }
                break;
            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                if (gameUnitsLogic != null)
                {
                    // Revert percentage by inverse factor
                    float revertPercent = -c.value;
                    gameUnitsLogic.AdjustClickPowerPercent(c.targetName, revertPercent);
                }
                break;
            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerForSection(c.targetName, -c.value);
                }
                break;
            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                if (gameUnitsLogic != null)
                {
                    float revertSectionPercent = -c.value;
                    gameUnitsLogic.AdjustClickPowerPercentForSection(c.targetName, revertSectionPercent);
                }
                break;
            default:
                break;
        }
    }
    
    /// <summary>
    /// Apply a single consequence from a completed story
    /// </summary>
    private void ApplyConsequence(EventConsequence consequence)
    {
        if (!AreSystemsReady())
        {
            LogEvent($"Cannot apply consequence {consequence.type}: systems not ready");
            return;
        }
        
        switch (consequence.type)
        {
            case EventConsequence.ConsequenceType.ScoreChange:
                ModifyEventScore(consequence.targetName, consequence.value);
                break;
                
            case EventConsequence.ConsequenceType.StatChange:
                if (statManager != null)
                {
                    int currentValue = statManager.GetStatValue(consequence.targetName);
                    statManager.UpdateStat(consequence.targetName, currentValue + consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply StatChange consequence: StatManager is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.ResourceChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.ChangeResourceFromName(consequence.targetName, consequence.value, false);
                }
                else
                {
                    LogEvent($"Cannot apply ResourceChange consequence: GameUnitsLogic is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.ProductionUnitChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.ChangeProductionUnitFromName(consequence.targetName, consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply ProductionUnitChange consequence: GameUnitsLogic is null");
                }
                break;
            
            case EventConsequence.ConsequenceType.ProductionPercentChange:
                if (GlobalProductionManager.Instance != null)
                {
                    // Positive value increases production; negative decreases
                    bool isPositive = consequence.value >= 0;
                    float amount = Mathf.Abs(consequence.value);
                    // Add a persistent percentage modifier, tracked by event name for source
                    GlobalProductionManager.Instance.AdjustPercentageModifier(
                        consequence.targetName,
                        amount,
                        isPositive,
                        true,
                        currentStoryNode != null ? currentStoryNode.storyTitle : "EventConsequence"
                    );
                }
                else
                {
                    LogEvent($"Cannot apply ProductionPercentChange consequence: GlobalProductionManager.Instance is null");
                }
                break;

            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
                if (GlobalProductionManager.Instance != null)
                {
                    bool isPositive = consequence.value >= 0;
                    float amount = Mathf.Abs(consequence.value);
                    GlobalProductionManager.Instance.AdjustPercentageModifierForSection(
                        consequence.targetName,
                        amount,
                        isPositive,
                        true,
                        currentStoryNode != null ? currentStoryNode.storyTitle : "EventConsequence"
                    );
                }
                else
                {
                    LogEvent($"Cannot apply ProductionPercentChangeSection consequence: GlobalProductionManager.Instance is null");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPower(consequence.targetName, consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply ClickPowerChange consequence: GameUnitsLogic is null");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerPercent(consequence.targetName, consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply ClickPowerPercentChange consequence: GameUnitsLogic is null");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerForSection(consequence.targetName, consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply ClickPowerChangeSection consequence: GameUnitsLogic is null");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerPercentForSection(consequence.targetName, consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply ClickPowerPercentChangeSection consequence: GameUnitsLogic is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.TechnologyEnlightened:
                if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null)
                {
                    GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == consequence.targetName);
                    if (techSlotObj != null)
                    {
                        GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                        if (techSlot != null)
                        {
                            techSlot.enlightenedCompleted = true;
                            techSlot.RefreshTechnologyUI();
                        }
                    }
                }
                else
                {
                    LogEvent($"Cannot apply TechnologyEnlightened consequence: GameUnitsLogic or researchTab is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.PopulationChange:
                if (PopGrowthLogic.Instance != null)
                {
                    // Population can only be REMOVED (negative values)
                    // Positive values are not allowed - use VagrantsChange instead
                    if (consequence.value < 0)
                    {
                        PopGrowthLogic.Instance.ModifyPopulation(consequence.value);
                    }
                    else
                    {
                        Debug.LogWarning($"PopulationChange consequence with positive value {consequence.value} is not allowed. Use VagrantsChange instead.");
                    }
                }
                else
                {
                    LogEvent($"Cannot apply PopulationChange consequence: PopGrowthLogic.Instance is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.HousingChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ModifyHousing(consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply HousingChange consequence: PopGrowthLogic.Instance is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.VagrantsChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ModifyVagrants(consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply VagrantsChange consequence: PopGrowthLogic.Instance is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.DeathsChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ProcessEventDeaths(consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply DeathsChange consequence: PopGrowthLogic.Instance is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.DeathRecordsRevision:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ReviseDeathRecords(consequence.value);
                }
                else
                {
                    LogEvent($"Cannot apply DeathRecordsRevision consequence: PopGrowthLogic.Instance is null");
                }
                break;
                
            case EventConsequence.ConsequenceType.UnlockEvent:
                LogEvent($"UnlockEvent consequence for '{consequence.targetName}' - this would need to be implemented");
                break;
        }
    }
    
    // PSEUDOCODE: Cleanup method to unsubscribe from time system events
    private void OnDestroy()
    {
        if (timeSystem != null)
        {
            timeSystem.OnPhaseChange -= OnPhaseChanged; // PSEUDOCODE: Unsubscribe from phase changes
            timeSystem.OnEchoChange -= OnEchoChanged; // PSEUDOCODE: Unsubscribe from echo changes
            timeSystem.OnCycleChange -= OnCycleChanged; // PSEUDOCODE: Unsubscribe from cycle changes
            timeSystem.OnRitualSeventh -= OnRitualSeventh; // PSEUDOCODE: Unsubscribe from ritual sevenths
            timeSystem.OnSeventhChange -= OnSeventhChanged; // Unsubscribe from seventh changes
            LogEvent("Unsubscribed from time system events"); // Boss announces the cleanup
        }
    }
    
    // ===== THE BOSS'S ANNOUNCEMENT SYSTEM =====
    // The boss announces what they're doing so others can keep track
    // Logging
    private void LogEvent(string message)
    {
        if (enableEventLogging) // Only announce if logging is turned on
        {
            Debug.Log($"[EventSystem] {message}"); // Boss makes an announcement
        }
    }
} 