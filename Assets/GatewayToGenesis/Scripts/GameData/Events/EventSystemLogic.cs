using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using DG.Tweening;

/// <summary>
/// Core event system for managing narrative events with Ink integration
/// </summary>
public class EventSystemLogic : MonoBehaviour
{
    // ===== THE BOSS'S OFFICE =====
    // This is where the boss (EventSystem) keeps track of everything

    [Header("Event Data")]
    [SerializeField] private List<EventScore> eventScores = new List<EventScore>(); // The scoreboard tracking player progress
    
    [Header("System References")]
    [SerializeField] private GameUnitsLogic gameUnitsLogic; // Direct reference to resource/production manager
    [SerializeField] private StatManager statManager; // Direct reference to stats manager
    [SerializeField] private TimeSystemLogic timeSystem; // Direct reference to time system
    [SerializeField] private EventVolumeManager volumeManager; // Direct reference to volume manager
    
    [Header("Event Notification System")]
    [SerializeField] private Transform eventsContainer; // Container for event notifications in HUD
    [SerializeField] private GameObject eventNotificationPrefab; // Prefab for event notifications
    
    // Track current notification to prevent stacking
    private GameObject currentNotification;
    
    // Track how the current event was triggered
    private string currentEventTriggerSource = "unknown";
    
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
    
    // ===== THE BOSS'S EVENT SPACING TRACKER =====
    // Track how many sevenths have passed since the last event completed
    private int seventhsSinceLastEvent = 0; // Count of sevenths since last event completion
    
    // ===== THE BOSS'S EVENT COOLDOWN TRACKER =====
    // Track when each specific event was last completed (for individual cooldowns)
    private Dictionary<string, int> eventCooldowns = new Dictionary<string, int>(); // Key: event name, Value: sevenths since that event
    
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
        
        // Increment the sevenths since last event counter (if no event is currently active)
        if (!isEventActive)
        {
            seventhsSinceLastEvent++;
            GameLoggingSystem.Instance.LogEvent($"Seventh {newSeventh} - sevenths since last event: {seventhsSinceLastEvent}", "EventSystemLogic");
        }
        else
        {
            GameLoggingSystem.Instance.LogEvent($"Seventh {newSeventh} - event active, sevenths counter frozen at: {seventhsSinceLastEvent}", "EventSystemLogic");
        }
        
        // Increment all individual event cooldowns
        var cooldownKeys = new List<string>(eventCooldowns.Keys);
        foreach (string eventName in cooldownKeys)
        {
            eventCooldowns[eventName]++;
        }
        
        // Events are only allowed after the first seventh change (when count reaches 1)
        if (seventhChangeCount < 1)
        {
            GameLoggingSystem.Instance.LogEvent($"Seventh {newSeventh} - change count {seventhChangeCount}, skipping event check (system initialization protection)", "EventSystemLogic");
            return;
        }
        
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            GameLoggingSystem.Instance.LogEvent($"Seventh changed to {newSeventh} - change count {seventhChangeCount}, checking for events", "EventSystemLogic"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when phase changes
    private void OnPhaseChanged(int newPhase)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            GameLoggingSystem.Instance.LogEvent($"Phase changed to {newPhase} - checking for events", "EventSystemLogic"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when echo changes
    private void OnEchoChanged(int newEcho)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            GameLoggingSystem.Instance.LogEvent($"Echo changed to {newEcho} - checking for events", "EventSystemLogic"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events when cycle changes
    private void OnCycleChanged(int newCycle)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            GameLoggingSystem.Instance.LogEvent($"Cycle changed to {newCycle} - checking for events", "EventSystemLogic"); // Boss announces time change
            CheckForAvailableEvents(); // CHOOSE BETWEEN POSSIBLE EVENTS
        }
    }
    
    // PSEUDOCODE: Check for events on ritual sevenths (special magical moments)
    private void OnRitualSeventh(int ritualSeventh)
    {
        if (!isEventActive && AreSystemsReady()) // Only check if boss isn't already telling a story and systems are ready
        {
            GameLoggingSystem.Instance.LogEvent($"Ritual Seventh {ritualSeventh} - checking for special events", "EventSystemLogic"); // Boss announces ritual
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
                GameLoggingSystem.Instance.LogEvent("Auto-assigned GameUnitsLogic reference", "EventSystemLogic"); // Boss announces the assignment
            }
        }
        
        if (statManager == null)
        {
            statManager = FindObjectOfType<StatManager>(); // Find stats manager if not assigned
            if (statManager != null)
            {
                GameLoggingSystem.Instance.LogEvent("Auto-assigned StatManager reference", "EventSystemLogic"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Find time system reference if not assigned in editor
        if (timeSystem == null)
        {
            timeSystem = FindObjectOfType<TimeSystemLogic>(); // Find time system if not assigned
            if (timeSystem != null)
            {
                GameLoggingSystem.Instance.LogEvent("Auto-assigned TimeSystemLogic reference", "EventSystemLogic"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Find volume manager reference if not assigned in editor
        if (volumeManager == null)
        {
            volumeManager = FindObjectOfType<EventVolumeManager>(); // Find volume manager if not assigned
            if (volumeManager != null)
            {
                GameLoggingSystem.Instance.LogEvent("Auto-assigned EventVolumeManager reference", "EventSystemLogic"); // Boss announces the assignment
            }
        }
        
        // PSEUDOCODE: Subscribe to time system events for thematic event triggering
        if (timeSystem != null)
        {
            // Wait for all systems to be ready before subscribing to time events
            StartCoroutine(WaitForSystemsAndSubscribeToTimeEvents());
            GameLoggingSystem.Instance.LogEvent("Started coroutine to wait for systems and subscribe to time events", "EventSystemLogic");
        }
        else
        {
            GameLoggingSystem.Instance.LogEvent("Time system not found - cannot subscribe to time events", "EventSystemLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent("Event System Logic initialized", "EventSystemLogic"); // Boss announces they're ready for work
    }
    
    /// <summary>
    /// Wait for all systems to be ready before subscribing to time system events
    /// </summary>
    private IEnumerator WaitForSystemsAndSubscribeToTimeEvents()
    {
        // Wait for all systems to be ready
        while (!AreSystemsReady())
        {
            GameLoggingSystem.Instance.LogEvent("Waiting for systems to be ready...", "EventSystemLogic");
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }
        
        GameLoggingSystem.Instance.LogEvent("All systems ready - subscribing to time system events", "EventSystemLogic");
        
        // Now subscribe to time system events
        timeSystem.OnPhaseChange += OnPhaseChanged; // PSEUDOCODE: Check events when phase changes
        timeSystem.OnEchoChange += OnEchoChanged; // PSEUDOCODE: Check events when echo changes
        timeSystem.OnCycleChange += OnCycleChanged; // PSEUDOCODE: Check events when cycle changes
        timeSystem.OnRitualSeventh += OnRitualSeventh; // PSEUDOCODE: Check events on ritual sevenths
        timeSystem.OnSeventhChange += OnSeventhChanged; // Check events when seventh changes
        GameLoggingSystem.Instance.LogEvent("Subscribed to time system events", "EventSystemLogic");
    }
    
    /// <summary>
    /// Check if all required systems are ready for events
    /// </summary>
    private bool AreSystemsReady()
    {
        if (PopGrowthLogic.Instance == null)
        {
            GameLoggingSystem.Instance.LogEvent("PopGrowthLogic.Instance is null - population system not ready", "EventSystemLogic");
            return false;
        }
        
        if (gameUnitsLogic == null)
        {
            GameLoggingSystem.Instance.LogEvent("GameUnitsLogic is null - resource system not ready", "EventSystemLogic");
            return false;
        }
        
        if (statManager == null)
        {
            GameLoggingSystem.Instance.LogEvent("StatManager is null - stats system not ready", "EventSystemLogic");
            return false;
        }
        
        if (timeSystem == null)
        {
            GameLoggingSystem.Instance.LogEvent("TimeSystemLogic is null - time system not ready", "EventSystemLogic");
            return false;
        }
        
        if (volumeManager == null)
        {
            GameLoggingSystem.Instance.LogEvent("EventVolumeManager is null - event system not ready", "EventSystemLogic");
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
            GameLoggingSystem.Instance.LogEvent("Systems not ready - cannot check for events", "EventSystemLogic");
            return;
        }
        
        // Prevent new events if there's already a notification pending (anti-stacking)
        if (currentNotification != null)
        {
            GameLoggingSystem.Instance.LogEvent("Event notification already pending - skipping new event check", "EventSystemLogic");
            return;
        }
        
        StoryNode bestStory = volumeManager.FindBestAvailableStory(); // Find the best story to tell
        
        if (bestStory != null) // Did we find a story to tell?
        {
            // Determine the trigger source for this event
            currentEventTriggerSource = DetermineEventTriggerSource(bestStory);
            
            GameLoggingSystem.Instance.LogEvent($"Event '{bestStory.storyTitle}' triggered by: {currentEventTriggerSource}", "EventSystemLogic");
            
            CreateEventNotification(bestStory); // Create notification instead of instantly triggering
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
            GameLoggingSystem.Instance.LogEvent($"Cannot trigger story {storyNode.storyTitle}: another event is active", "EventSystemLogic");
            return; // Boss is busy, can't start another story
        }
        
        if (!AreSystemsReady())
        {
            GameLoggingSystem.Instance.LogEvent($"Cannot trigger story {storyNode.storyTitle}: systems not ready", "EventSystemLogic");
            return;
        }
        
        currentStoryNode = storyNode; // Remember which story we're telling
        isEventActive = true; // Mark that boss is now busy
        
        GameLoggingSystem.Instance.LogEvent($"Triggering story: {storyNode.storyTitle}", "EventSystemLogic"); // Boss announces they're starting
        
        // Pause time when event starts
        if (timeSystem != null)
        {
            timeSystem.PauseTime(true);
            GameLoggingSystem.Instance.LogEvent("Time paused for event", "EventSystemLogic");
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
        GameLoggingSystem.Instance.LogEvent($"Executing screen: {screen.screenType}", "EventSystemLogic");
        screenManager.ShowScreen(screen);
    }
    
    // ===== THE BOSS'S SCOREBOARD =====
    // The boss keeps track of how far the player has gotten in each story
    // Event Score Management
    public void ModifyEventScore(string scoreName, int change)
    {
        EventScore score = GetOrCreateEventScore(scoreName); // Find or create the score
        score.value += change; // Add or subtract from the score
        GameLoggingSystem.Instance.LogEvent($"Event score '{scoreName}' changed by {change} (new value: {score.value})", "EventSystemLogic"); // Boss announces the change
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
            GameLoggingSystem.Instance.LogEvent($"Added consequence: {consequence.type} {consequence.targetName} {consequence.value}", "EventSystemLogic");
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
        GameLoggingSystem.Instance.LogEvent("Cleared cumulative consequences", "EventSystemLogic");
    }
    
    // ===== THE BOSS'S PUBLIC OFFICE =====
    // Other parts of the game can ask the boss questions or give them new stories
    
    /// <summary>
    /// Create an event notification instead of instantly triggering the event
    /// </summary>
    private void CreateEventNotification(StoryNode storyNode)
    {
        if (eventsContainer == null || eventNotificationPrefab == null)
        {
            GameLoggingSystem.Instance.LogEvent("Cannot create event notification: missing container or prefab reference", "EventSystemLogic");
            return;
        }
        
        // Check if we already have a notification - prevent stacking
        if (currentNotification != null)
        {
            GameLoggingSystem.Instance.LogEvent($"Event notification already exists for: {currentNotification.name}, skipping new event: {storyNode.storyTitle}", "EventSystemLogic");
            return;
        }
        
        // Create the notification GameObject
        GameObject notification = Instantiate(eventNotificationPrefab, eventsContainer);
        currentNotification = notification; // Track the current notification
        
        // Set up the notification with story data
        SetupEventNotification(notification, storyNode);
        
        // Enable slow motion time while event is pending
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.EnableSlowMotion();
        }
        
        GameLoggingSystem.Instance.LogEvent($"Created event notification for: {storyNode.storyTitle}", "EventSystemLogic");
    }
    
    /// <summary>
    /// Setup the event notification with story data and fade-in animation
    /// </summary>
    private void SetupEventNotification(GameObject notification, StoryNode storyNode)
    {
        // Store the story node reference for when notification is clicked
        notification.name = $"EventNotification_{storyNode.storyTitle}";
        
        // Set up fade-in animation
        CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = notification.AddComponent<CanvasGroup>();
        }
        
        // Start at 0 alpha and fade in over 1 second
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 1f).SetEase(DG.Tweening.Ease.OutQuad);
        
        // Store story reference for the button click
        var storyReference = notification.AddComponent<EventNotificationData>();
        storyReference.storyNode = storyNode;
        
        // Automatically set up the button click handler
        SetupNotificationButton(notification);
    }
    
    /// <summary>
    /// Automatically set up the notification button click handler
    /// </summary>
    private void SetupNotificationButton(GameObject notification)
    {
        // Find the button component in the notification
        UnityEngine.UI.Button button = notification.GetComponentInChildren<UnityEngine.UI.Button>();
        if (button == null)
        {
            GameLoggingSystem.Instance.LogEvent("EventNotification prefab must have a Button component for click handling", "EventSystemLogic");
            return;
        }
        
        // Clear any existing listeners and add our handler
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => StartEventFromNotification(notification));
        
        GameLoggingSystem.Instance.LogEvent("EventNotification button click handler configured automatically", "EventSystemLogic");
    }
    
    /// <summary>
    /// Start the event from notification (called by notification button click)
    /// </summary>
    public void StartEventFromNotification(GameObject notification)
    {
        var storyReference = notification.GetComponent<EventNotificationData>();
        if (storyReference?.storyNode == null)
        {
            GameLoggingSystem.Instance.LogEvent("Cannot start event: notification has no story reference", "EventSystemLogic");
            return;
        }
        
        // Fade out the notification
        CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.5f).SetEase(DG.Tweening.Ease.InQuad)
                .OnComplete(() => {
                    if (notification != null)
                    {
                        Destroy(notification);
                    }
                    // Clear the current notification reference
                    if (currentNotification == notification)
                    {
                        currentNotification = null;
                        // Note: Slow motion remains active during the event
                    }
                });
        }
        else
        {
            Destroy(notification);
            // Clear the current notification reference
            if (currentNotification == notification)
            {
                currentNotification = null;
                // Note: Slow motion remains active during the event
            }
        }
        
        // Start the actual event
        TriggerStory(storyReference.storyNode);
        
        // Keep slow motion active during the event (will be disabled when event completes)
        GameLoggingSystem.Instance.LogEvent($"Event started - slow motion remains active until completion", "EventSystemLogic");
        
        // Switch to event tab
        TabHotkeys hotkeys = FindObjectOfType<TabHotkeys>();
        if (hotkeys != null)
        {
            hotkeys.SwitchToEventTab();
        }
        
        GameLoggingSystem.Instance.LogEvent($"Started event from notification: {storyReference.storyNode.storyTitle}", "EventSystemLogic");
    }
    
    // Public API
    public void AddVolume(EventVolume volume)
    {
        if (volumeManager != null)
        {
            volumeManager.AddVolume(volume); // Add the new volume to the manager
            GameLoggingSystem.Instance.LogEvent($"Added volume: {volume.volumeName}", "EventSystemLogic"); // Boss announces the new volume
        }
    }
    
    public bool IsEventActive()
    {
        return isEventActive; // Tell others if the boss is currently telling a story
    }
    
    public StoryNode GetCurrentStoryNode() => currentStoryNode;
    
    /// <summary>
    /// Get the trigger source of the current event
    /// </summary>
    public string GetCurrentEventTriggerSource() => currentEventTriggerSource;
    
    /// <summary>
    /// Get the number of sevenths since the last event completed
    /// </summary>
    public int GetSeventhsSinceLastEvent() => seventhsSinceLastEvent;
    
    /// <summary>
    /// Check if a specific event is on cooldown
    /// </summary>
    public bool IsEventOnCooldown(string eventName, int requiredCooldown)
    {
        if (requiredCooldown <= 0) return false; // No cooldown required
        
        if (!eventCooldowns.ContainsKey(eventName))
        {
            // Event has never been completed, not on cooldown
            return false;
        }
        
        int seventhsSinceEvent = eventCooldowns[eventName];
        bool onCooldown = seventhsSinceEvent < requiredCooldown;
        
        if (onCooldown)
        {
            GameLoggingSystem.Instance.LogEvent($"Event '{eventName}' is on cooldown: {seventhsSinceEvent}/{requiredCooldown} sevenths", "EventSystemLogic");
        }
        
        return onCooldown;
    }
    
    /// <summary>
    /// Reset the cooldown for a specific event (called when event completes)
    /// </summary>
    private void ResetEventCooldown(string eventName)
    {
        eventCooldowns[eventName] = 0;
        GameLoggingSystem.Instance.LogEvent($"Event '{eventName}' cooldown reset to 0", "EventSystemLogic");
    }
    
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
            GameLoggingSystem.Instance.LogEvent("Systems not ready - cannot check for events", "EventSystemLogic");
            return;
        }
        
        // Respect system initialization - don't trigger events until first seventh change has passed
        if (seventhChangeCount < 1)
        {
            GameLoggingSystem.Instance.LogEvent($"System not yet initialized (change count: {seventhChangeCount}) - skipping event check until first seventh change passes", "EventSystemLogic");
            return;
        }
        
        // Check for available events
        CheckForAvailableEvents();
        
        // Log current time state for debugging
        if (TimeSystemLogic.Instance != null)
        {
            float effectiveTime = TimeSystemLogic.Instance.GetEffectiveSecondsPerSeventh();
            float baseTime = TimeSystemLogic.Instance.BaseSecondsPerSeventh;
            float progress = TimeSystemLogic.Instance.GetSeventhProgress();
            float remaining = TimeSystemLogic.Instance.GetRemainingSecondsToSeventh();
            
            if (effectiveTime > baseTime)
            {
                GameLoggingSystem.Instance.LogEvent($"Time is in SLOW MOTION: {baseTime}s → {effectiveTime}s per seventh | Progress: {progress:P1} | Remaining: {remaining:F1}s", "EventSystemLogic");
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"Time is NORMAL: {baseTime}s per seventh | Progress: {progress:P1} | Remaining: {remaining:F1}s", "EventSystemLogic");
            }
        }
        
        // Ensure slow motion state is consistent with event state
        EnsureSlowMotionConsistency();
    }
    
    /// <summary>
    /// Manually trigger a specific event with a custom trigger source
    /// Useful for testing, special events, or scripted sequences
    /// </summary>
    /// <param name="storyNode">The story node to trigger</param>
    /// <param name="triggerSource">Custom trigger source description</param>
    public void TriggerSpecificEvent(StoryNode storyNode, string triggerSource = "manual")
    {
        if (isEventActive)
        {
            GameLoggingSystem.Instance.LogEvent($"Cannot trigger specific event: another event is active", "EventSystemLogic");
            return;
        }
        
        if (!AreSystemsReady())
        {
            GameLoggingSystem.Instance.LogEvent($"Cannot trigger specific event: systems not ready", "EventSystemLogic");
            return;
        }
        
        // Set the trigger source for this manually triggered event
        currentEventTriggerSource = triggerSource;
        
        GameLoggingSystem.Instance.LogEvent($"Manually triggering event '{storyNode.storyTitle}' with source: {triggerSource}", "EventSystemLogic");
        
        // Start the event directly
        TriggerStory(storyNode);
    }
    
    /// <summary>
    /// Ensure slow motion state is consistent with current event state
    /// </summary>
    private void EnsureSlowMotionConsistency()
    {
        if (TimeSystemLogic.Instance == null) return;
        
        bool shouldHaveSlowMotion = currentNotification != null && !isEventActive;
        bool currentlyHasSlowMotion = TimeSystemLogic.Instance.GetEffectiveSecondsPerSeventh() > TimeSystemLogic.Instance.BaseSecondsPerSeventh;
        
        if (shouldHaveSlowMotion && !currentlyHasSlowMotion)
        {
            // Should have slow motion but doesn't - enable it
            TimeSystemLogic.Instance.EnableSlowMotion();
            GameLoggingSystem.Instance.LogEvent("Slow motion consistency check: re-enabled slow motion for pending notification", "EventSystemLogic");
        }
        else if (!shouldHaveSlowMotion && currentlyHasSlowMotion)
        {
            // Shouldn't have slow motion but does - disable it
            TimeSystemLogic.Instance.DisableSlowMotion();
            GameLoggingSystem.Instance.LogEvent("Slow motion consistency check: disabled slow motion (no pending notifications)", "EventSystemLogic");
        }
    }
    
    /// <summary>
    /// Called when a story completes to resume time and clean up
    /// </summary>
    public void OnStoryCompleted()
    {
        GameLoggingSystem.Instance.LogEvent("Story completed - resuming time and cleaning up", "EventSystemLogic");
        
        // Apply satisfaction penalty only for events triggered by dark_morale conditions
        if (currentStoryNode != null && IsEventTriggeredByDarkMorale(currentStoryNode))
        {
            if (StatManager.Instance != null)
            {
                StatManager.Instance.ChangeSatisfactionPoints(-15, $"Dark Morale Event: {currentStoryNode.storyTitle}");
            }
        }
        
        // Hide the current event screen first
        if (screenManager != null)
        {
            screenManager.HideCurrentScreen();
            GameLoggingSystem.Instance.LogEvent("Event screen hidden", "EventSystemLogic");
        }
        
        // Apply cumulative consequences from the entire story flow
        if (cumulativeConsequences.Count > 0)
        {
            if (AreSystemsReady())
            {
                GameLoggingSystem.Instance.LogEvent($"Applying {cumulativeConsequences.Count} cumulative consequences from story flow", "EventSystemLogic");
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
                
                // Ensure all resources maintain their minimum click power after applying consequences
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.EnsureAllResourceClickPowerMinimums();
                    GameLoggingSystem.Instance.LogEvent("Validated all resource click power minimums after applying consequences", "EventSystemLogic");
                }
                
                // Trigger immediate council recalculation to reflect event consequence effects
                if (GovernmentLogic.Instance != null)
                {
                    GovernmentLogic.Instance.ProcessAllSeatBonuses();
                    GameLoggingSystem.Instance.LogEvent("Triggered immediate council recalculation after applying event consequences", "EventSystemLogic");
                }
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"Cannot apply cumulative consequences: systems not ready", "EventSystemLogic");
            }
        }
        
        // Clear the cumulative consequences for the next story
        ClearCumulativeConsequences();
        
        // Resume time when event ends
        if (timeSystem != null)
        {
            timeSystem.PauseTime(false);
            GameLoggingSystem.Instance.LogEvent("Time resumed after event", "EventSystemLogic");
            // Ensure we are subscribed to seventh changes for timed expirations
            timeSystem.OnSeventhChange -= OnTimedSeventh; // avoid dup
            timeSystem.OnSeventhChange += OnTimedSeventh;
        }
        
        // Disable slow motion time when event completes
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.DisableSlowMotion();
            GameLoggingSystem.Instance.LogEvent("Slow motion disabled - event completed", "EventSystemLogic");
        }
        
        // Restore previous tab states and HUD visibility
        TabHotkeys hotkeys = FindObjectOfType<TabHotkeys>();
        if (hotkeys != null)
        {
            hotkeys.RestoreTabStatesAfterEvent();
            GameLoggingSystem.Instance.LogEvent("Tab states and HUD restored", "EventSystemLogic");
        }
        else
        {
            GameLoggingSystem.Instance.LogEvent("ERROR: TabHotkeys not found for restoration", "EventSystemLogic");
        }
        
        // Reset event state
        isEventActive = false;
        
        // Reset the cooldown for this specific event
        if (currentStoryNode != null)
        {
            ResetEventCooldown(currentStoryNode.nodeName);
        }
        
        currentStoryNode = null;
        
        // Set the sevenths counter to -1 to avoid issues in logic
        seventhsSinceLastEvent = -1;
    }

    /// <summary>
    /// Determine what triggered this event by analyzing its conditions
    /// </summary>
    /// <param name="storyNode">The story node to analyze</param>
    /// <returns>String describing the trigger source</returns>
    private string DetermineEventTriggerSource(StoryNode storyNode)
    {
        if (storyNode == null) return "unknown";
        
        // Check for dark_morale score conditions first (highest priority)
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.ScoreCheck && 
                string.Equals(condition.targetName, "dark_morale", System.StringComparison.OrdinalIgnoreCase))
            {
                return $"dark_morale_score_{condition.requiredValue}";
            }
        }
        
        // Check for time-based triggers
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.SeventhCheck ||
                condition.type == EventCondition.ConditionType.PhaseCheck ||
                condition.type == EventCondition.ConditionType.EchoCheck ||
                condition.type == EventCondition.ConditionType.CycleCheck ||
                condition.type == EventCondition.ConditionType.RitualSeventhCheck)
            {
                return $"time_based_{condition.type}";
            }
        }
        
        // Check for resource-based triggers
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.ResourceCheck)
            {
                return $"resource_{condition.targetName}_{condition.comparison}_{condition.requiredValue}";
            }
        }
        
        // Check for technology-based triggers
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.TechnologyCheck)
            {
                return $"technology_{condition.targetName}";
            }
        }
        
        // Check for stat-based triggers
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.StatCheck)
            {
                return $"stat_{condition.targetName}_{condition.comparison}_{condition.requiredValue}";
            }
        }
        
        // Check for population-based triggers
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.PopulationCheck ||
                condition.type == EventCondition.ConditionType.HousingCheck ||
                condition.type == EventCondition.ConditionType.VagrantsCheck ||
                condition.type == EventCondition.ConditionType.DeathsCheck)
            {
                return $"population_{condition.type}_{condition.comparison}_{condition.requiredValue}";
            }
        }
        
        // Check for no-event-in-sevenths conditions
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.NoEventInSeventhsCheck)
            {
                return $"no_events_{condition.requiredValue}_sevenths";
            }
        }
        
        return "unknown_trigger";
    }

    /// <summary>
    /// Check if an event was triggered by dark_morale conditions
    /// </summary>
    /// <param name="storyNode">The story node to check</param>
    /// <returns>True if the event was triggered by dark_morale conditions</returns>
    private bool IsEventTriggeredByDarkMorale(StoryNode storyNode)
    {
        if (storyNode == null) return false;
        
        // Check if this event has conditions that depend on dark_morale score
        foreach (var condition in storyNode.storyConditions)
        {
            if (condition.type == EventCondition.ConditionType.ScoreCheck && 
                string.Equals(condition.targetName, "dark_morale", System.StringComparison.OrdinalIgnoreCase))
            {
                // This event requires a certain dark_morale score to trigger
                // If it's triggering, it means dark_morale conditions were met
                return true;
            }
        }
        
        return false;
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
        
        bool anyConsequencesReverted = false;
        
        // Decrement and expire any that hit 0; for expiration we remove the same effect
        for (int i = activeTimed.Count - 1; i >= 0; i--)
        {
            var t = activeTimed[i];
            t.remainingSevenths = Mathf.Max(0, t.remainingSevenths - 1);
            if (t.remainingSevenths == 0)
            {
                RevertTimedConsequence(t.consequence);
                activeTimed.RemoveAt(i);
                anyConsequencesReverted = true;
            }
        }
        
        // Ensure all resources maintain their minimum click power after processing timed consequences
        if (gameUnitsLogic != null)
        {
            gameUnitsLogic.EnsureAllResourceClickPowerMinimums();
        }
        
        // Trigger immediate council recalculation if any timed consequences were reverted
        if (anyConsequencesReverted && GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.ProcessAllSeatBonuses();
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
            GameLoggingSystem.Instance.LogEvent($"Cannot apply consequence {consequence.type}: systems not ready", "EventSystemLogic");
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
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply StatChange consequence: StatManager is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.ResourceChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.ChangeResourceFromName(consequence.targetName, consequence.value, false);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ResourceChange consequence: GameUnitsLogic is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.ProductionUnitChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.ChangeProductionUnitFromName(consequence.targetName, consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ProductionUnitChange consequence: GameUnitsLogic is null", "EventSystemLogic");
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
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ProductionPercentChange consequence: GlobalProductionManager.Instance is null", "EventSystemLogic");
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
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ProductionPercentChangeSection consequence: GlobalProductionManager.Instance is null", "EventSystemLogic");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPower(consequence.targetName, consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ClickPowerChange consequence: GameUnitsLogic is null", "EventSystemLogic");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerPercent(consequence.targetName, consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ClickPowerPercentChange consequence: GameUnitsLogic is null", "EventSystemLogic");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerForSection(consequence.targetName, consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ClickPowerChangeSection consequence: GameUnitsLogic is null", "EventSystemLogic");
                }
                break;

            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                if (gameUnitsLogic != null)
                {
                    gameUnitsLogic.AdjustClickPowerPercentForSection(consequence.targetName, consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply ClickPowerPercentChangeSection consequence: GameUnitsLogic is null", "EventSystemLogic");
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
                            // Visually reflect enlightened progress immediately
                            techSlot.researchProgress = Mathf.Max(techSlot.researchProgress, Mathf.Clamp01(techSlot.enlightenedBonusPercent));
                            techSlot.RefreshTechnologyUI();
                            techSlot.UpdateProgressUI();
                            // Ensure enlightened technologies become visible regardless of prerequisites
                            if (techSlot.technologyTreeLogic != null)
                            {
                                techSlot.technologyTreeLogic.DetermineTechnologyVisibility(techSlot);
                            }
                        }
                    }
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply TechnologyEnlightened consequence: GameUnitsLogic or researchTab is null", "EventSystemLogic");
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
                        GameLoggingSystem.Instance.LogEvent($"PopulationChange consequence with positive value {consequence.value} is not allowed. Use VagrantsChange instead.", "EventSystemLogic");
                    }
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply PopulationChange consequence: PopGrowthLogic.Instance is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.HousingChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ModifyHousing(consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply HousingChange consequence: PopGrowthLogic.Instance is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.VagrantsChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ModifyVagrants(consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply VagrantsChange consequence: PopGrowthLogic.Instance is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.DeathsChange:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ProcessEventDeaths(consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply DeathsChange consequence: PopGrowthLogic.Instance is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.DeathRecordsRevision:
                if (PopGrowthLogic.Instance != null)
                {
                    PopGrowthLogic.Instance.ReviseDeathRecords(consequence.value);
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"Cannot apply DeathRecordsRevision consequence: PopGrowthLogic.Instance is null", "EventSystemLogic");
                }
                break;
                
            case EventConsequence.ConsequenceType.UnlockEvent:
                GameLoggingSystem.Instance.LogEvent($"UnlockEvent consequence for '{consequence.targetName}' - this would need to be implemented", "EventSystemLogic");
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
            GameLoggingSystem.Instance.LogEvent("Unsubscribed from time system events", "EventSystemLogic");
        }
    }
}

/// <summary>
/// Simple component to store story reference in event notification
/// </summary>
public class EventNotificationData : MonoBehaviour
{
    public StoryNode storyNode;
} 