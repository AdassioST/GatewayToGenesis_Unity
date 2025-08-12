using UnityEngine;

/// <summary>
/// Simple test script to demonstrate the Ink-driven event system
/// </summary>
public class EventSystemTest : MonoBehaviour
{
    [Header("Test Controls")]
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private float startTestDelaySeconds = 1.0f;
    [Header("Targets")]
    [SerializeField] private string testVolumeName = "WeepingPrincess";
    [SerializeField] private string testNodeName = "weeping_princess";
    
    private void Start()
    {
        if (testOnStart)
        {
            StartCoroutine(RunTestAfterDelay());
        }
    }

    private System.Collections.IEnumerator RunTestAfterDelay()
    {
        if (startTestDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startTestDelaySeconds);
        }

        // Wait until core systems are initialized
        float timeout = 5f; // safety timeout
        float elapsed = 0f;
        while ((EventSystemLogic.Instance == null || EventSystemLogic.Instance.GetVolumeManager() == null) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (EventSystemLogic.Instance == null)
        {
            Debug.LogWarning("[EventSystemTest] EventSystemLogic not ready after delay; aborting auto test.");
            yield break;
        }

        Log("Auto-testing after startup delay");
        TestEventSystem();
    }
    
    /// <summary>
    /// Test the event system by setting up conditions and triggering checks
    /// </summary>
    public void TestEventSystem()
    {
        if (EventSystemLogic.Instance == null)
        {
            Debug.LogError("[EventSystemTest] EventSystemLogic not found!");
            return;
        }
        
        Log("=== Event System Test Started ===");
        
        // Test 1: Ensure starting conditions for Weeping Princess
        Log("Ensuring Weeping Princess initial conditions...");
        ResetScoreToZero("weeping_princess_progress");
        
        // Test 2: Trigger event check (will open Event tab and show Splash if available)
        Log("Triggering event check...");
        EventSystemLogic.Instance.TriggerEventCheck();
        
        // Test 3: Check if event is active
        if (EventSystemLogic.Instance.IsEventActive())
        {
            Log("Event is now active!");
        }
        else
        {
            Log("No events triggered - check conditions");
        }
    }
    
    /// <summary>
    /// Test specific story progression
    /// </summary>
    public void TestWeepingPrincessProgression()
    {
        Log("=== Testing Weeping Princess Progression ===");
        
        // Part 1: Initial event
        Log("Testing Part 1...");
        ResetScoreToZero("weeping_princess_progress");
        EventSystemLogic.Instance.TriggerEventCheck();
        
        // Wait a bit, then test Part 2
        Invoke(nameof(TestPart2), 2f);
    }
    
    private void TestPart2()
    {
        Log("Testing Part 2...");
        EventSystemLogic.Instance.ModifyEventScore("weeping_princess_progress", 1);
        EventSystemLogic.Instance.TriggerEventCheck();
    }
    
    /// <summary>
    /// Test merchant quest events
    /// </summary>
    public void TestMerchantQuests()
    {
        Log("=== Testing Merchant Quests ===");
        
        EventSystemLogic.Instance.ModifyEventScore("merchant_quest_progress", 0);
        EventSystemLogic.Instance.TriggerEventCheck();
    }
    
    /// <summary>
    /// Test ritual seventh events
    /// </summary>
    public void TestRitualEvents()
    {
        Log("=== Testing Ritual Events ===");
        
        EventSystemLogic.Instance.ModifyEventScore("ritual_events_seen", 0);
        EventSystemLogic.Instance.TriggerEventCheck();
    }
    
    /// <summary>
    /// Reset all event scores for testing
    /// </summary>
    public void ResetAllScores()
    {
        Log("=== Resetting All Event Scores ===");
        
        string[] scoresToReset = {
            "weeping_princess_progress",
            "merchant_quest_progress", 
            "ritual_events_seen",
            "tutorial_progress",
            "main_quest_progress"
        };
        
        foreach (string scoreName in scoresToReset)
        {
            ResetScoreToZero(scoreName);
            Log($"Reset {scoreName} to 0");
        }
    }
    
    /// <summary>
    /// Show current event scores
    /// </summary>
    public void ShowCurrentScores()
    {
        Log("=== Current Event Scores ===");
        
        string[] scoresToCheck = {
            "weeping_princess_progress",
            "merchant_quest_progress",
            "ritual_events_seen",
            "tutorial_progress",
            "main_quest_progress"
        };
        
        foreach (string scoreName in scoresToCheck)
        {
            int score = EventSystemLogic.Instance.GetEventScore(scoreName);
            Log($"{scoreName}: {score}");
        }
    }
    
    /// <summary>
    /// Test manual story triggering
    /// </summary>
    public void TestManualStoryTrigger()
    {
        Log("=== Testing Manual Story Trigger ===");
        
        EventVolumeManager volumeManager = EventSystemLogic.Instance.GetVolumeManager();
        if (volumeManager != null)
        {
            StoryNode testStory = volumeManager.GetStoryNode(testVolumeName, testNodeName);
            if (testStory != null)
            {
                Log("Manually triggering Weeping Princess story...");
                EventSystemLogic.Instance.TriggerStory(testStory);
            }
            else
            {
                Log("Could not find test story node - is the Ink file loaded?");
            }
        }
        else
        {
            Log("Volume manager not found!");
        }
    }

    // === Tab Controls ===
    [ContextMenu("Open Event Tab (Toggle)")]
    public void ToggleEventTab()
    {
        TabHotkeys hotkeys = FindObjectOfType<TabHotkeys>();
        if (hotkeys != null)
        {
            hotkeys.ToggleEventTab();
            Log("Toggled Event tab");
        }
        else
        {
            Log("TabHotkeys not found in scene");
        }
    }

    // === Helpers ===
    private void ResetScoreToZero(string scoreName)
    {
        int current = EventSystemLogic.Instance.GetEventScore(scoreName);
        if (current != 0)
        {
            EventSystemLogic.Instance.ModifyEventScore(scoreName, -current);
        }
    }
    
    private void Log(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[EventSystemTest] {message}");
        }
    }
    
    /// <summary>
    /// UI Button methods for testing
    /// </summary>
    [ContextMenu("Test Event System")]
    public void TestEventSystemButton()
    {
        TestEventSystem();
    }
    
    [ContextMenu("Test Weeping Princess")]
    public void TestWeepingPrincessButton()
    {
        TestWeepingPrincessProgression();
    }
    
    [ContextMenu("Test Merchant Quests")]
    public void TestMerchantQuestsButton()
    {
        TestMerchantQuests();
    }
    
    [ContextMenu("Reset All Scores")]
    public void ResetAllScoresButton()
    {
        ResetAllScores();
    }
    
    [ContextMenu("Show Current Scores")]
    public void ShowCurrentScoresButton()
    {
        ShowCurrentScores();
    }
    
    /// <summary>
    /// Test that time is properly paused during events and resumed after
    /// </summary>
    [ContextMenu("Test Time Pausing")]
    public void TestTimePausing()
    {
        if (EventSystemLogic.Instance == null)
        {
            Debug.LogError("[EventSystemTest] EventSystemLogic not found!");
            return;
        }
        
        TimeSystemLogic timeSystem = EventSystemLogic.Instance.GetTimeSystem();
        if (timeSystem == null)
        {
            Debug.LogError("[EventSystemTest] TimeSystemLogic not found!");
            return;
        }
        
        Log("=== Testing Time Pausing ===");
        Log($"Initial time paused state: {timeSystem.isTimePaused}");
        
        // Test pausing
        timeSystem.PauseTime(true);
        Log($"After pause: {timeSystem.isTimePaused}");
        
        // Test resuming
        timeSystem.PauseTime(false);
        Log($"After resume: {timeSystem.isTimePaused}");
        
        Log("=== Time Pausing Test Complete ===");
    }
    
    /// <summary>
    /// Test verse screen functionality to debug duplicate display issues
    /// </summary>
    [ContextMenu("Test Verse Screen")]
    public void TestVerseScreen()
    {
        if (EventSystemLogic.Instance == null)
        {
            Debug.LogError("[EventSystemTest] EventSystemLogic not found!");
            return;
        }
        
        EventVolumeManager volumeManager = EventSystemLogic.Instance.GetVolumeManager();
        if (volumeManager == null)
        {
            Debug.LogError("[EventSystemTest] EventVolumeManager not found!");
            return;
        }
        
        Log("=== Testing Verse Screen ===");
        
        // Find a test story with verse screens
        StoryNode testStory = volumeManager.FindBestAvailableStory();
        if (testStory == null)
        {
            Log("No test story available - create one in the EventVolumeManager");
            return;
        }
        
        Log($"Found test story: {testStory.storyTitle}");
        Log($"Story has {testStory.screenFlow.Count} screens");
        
        // Check for verse screens
        int verseCount = 0;
        foreach (var step in testStory.screenFlow)
        {
            if (step.flowType == ScreenFlowStep.FlowType.Verse)
            {
                verseCount++;
                Log($"Verse screen found: {step.screenId} (knot: {step.inkKnot})");
            }
        }
        
        Log($"Total verse screens: {verseCount}");
        
        if (verseCount > 0)
        {
            Log("Triggering test story to check verse screen behavior...");
            EventSystemLogic.Instance.TriggerStory(testStory);
        }
        else
        {
            Log("No verse screens found in test story");
        }
        
        Log("=== Verse Screen Test Complete ===");
    }
    
    /// <summary>
    /// Diagnose the current state of the event system
    /// </summary>
    [ContextMenu("Diagnose Event System")]
    public void DiagnoseEventSystem()
    {
        if (EventSystemLogic.Instance == null)
        {
            Debug.LogError("[EventSystemTest] EventSystemLogic not found!");
            return;
        }
        
        Log("=== Event System Diagnosis ===");
        
        // Check event system state
        Log($"Event system active: {EventSystemLogic.Instance.isEventActive}");
        Log($"Current story: {EventSystemLogic.Instance.GetCurrentStoryNode()?.storyTitle ?? "None"}");
        
        // Check time system
        TimeSystemLogic timeSystem = EventSystemLogic.Instance.GetTimeSystem();
        if (timeSystem != null)
        {
            Log($"Time system paused: {timeSystem.isTimePaused}");
        }
        else
        {
            Log("Time system not found!");
        }
        
        // Check volume manager
        EventVolumeManager volumeManager = EventSystemLogic.Instance.GetVolumeManager();
        if (volumeManager != null)
        {
            Log($"Volume manager current story: {volumeManager.GetCurrentStoryNode()?.storyTitle ?? "None"}");
            Log($"Volume manager current screen index: {volumeManager.GetCurrentScreenIndex()}");
            Log($"Volume manager current screen: {(volumeManager.GetCurrentScreenIndex() >= 0 ? "Screen " + volumeManager.GetCurrentScreenIndex() : "None")}");
        }
        else
        {
            Log("Volume manager not found!");
        }
        
        // Check screen manager
        EventScreenManager screenManager = EventSystemLogic.Instance.GetScreenManager();
        if (screenManager != null)
        {
            // Use reflection to access private fields for debugging
            var currentScreenField = typeof(EventScreenManager).GetField("currentScreen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentScreen = currentScreenField?.GetValue(screenManager);
            Log($"Screen manager current screen: {currentScreen?.GetType().Name ?? "None"}");
            
            var isScreenCompleteField = typeof(EventScreenManager).GetField("isScreenComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isScreenComplete = isScreenCompleteField?.GetValue(screenManager);
            Log($"Screen manager screen complete: {isScreenComplete}");
            
            var isProgressiveRevealCompleteField = typeof(EventScreenManager).GetField("isProgressiveRevealComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isProgressiveRevealComplete = isProgressiveRevealCompleteField?.GetValue(screenManager);
            Log($"Screen manager progressive reveal complete: {isProgressiveRevealComplete}");
        }
        else
        {
            Log("Screen manager not found!");
        }
        
        Log("=== Diagnosis Complete ===");
    }
} 