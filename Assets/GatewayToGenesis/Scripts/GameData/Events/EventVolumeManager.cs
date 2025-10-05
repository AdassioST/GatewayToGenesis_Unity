using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using System.Linq; // Added for .Any()

/// <summary>
/// Manages Event Volumes (DLC-style story collections) loaded from Ink masterfiles
/// </summary>
public class EventVolumeManager : MonoBehaviour
{
    [Header("Event Volumes")]
    [SerializeField] private List<EventVolume> eventVolumes = new List<EventVolume>();
    
    [Header("Current State")]
    [SerializeField] private EventVolume currentVolume;
    [SerializeField] private StoryNode currentStoryNode;
    [SerializeField] private int currentScreenIndex = 0;
    
    private Dictionary<string, Story> volumeStories = new Dictionary<string, Story>();
    private Dictionary<string, EventVolume> volumeLookup = new Dictionary<string, EventVolume>();
    private Dictionary<string, StoryNode> storyNodeLookup = new Dictionary<string, StoryNode>();
    
    private void Awake()
    {
        InitializeVolumeLookups();
        LoadAllVolumeStories();
    }
    
    /// <summary>
    /// Initialize lookup dictionaries for fast access
    /// </summary>
    private void InitializeVolumeLookups()
    {
        volumeLookup.Clear();
        storyNodeLookup.Clear();
        
        foreach (EventVolume volume in eventVolumes)
        {
            volumeLookup[volume.volumeName] = volume;
            
            foreach (StoryNode storyNode in volume.storyNodes)
            {
                string key = $"{volume.volumeName}.{storyNode.nodeName}";
                storyNodeLookup[key] = storyNode;
            }
        }

        Debug.Log($"[EventVolumeManager] Initialized lookups: volumes={eventVolumes.Count}, storyNodes={storyNodeLookup.Count}");
    }
    
    /// <summary>
    /// Load all Ink stories for all volumes
    /// </summary>
    private void LoadAllVolumeStories()
    {
        volumeStories.Clear();
        
        foreach (EventVolume volume in eventVolumes)
        {
            if (volume.inkMasterfile != null)
            {
                Story story = new Story(volume.inkMasterfile.text);
                volumeStories[volume.volumeName] = story;
                
                // Bind external functions for this story
                BindExternalFunctions(story);

                Debug.Log($"[EventVolumeManager] Loaded story for volume '{volume.volumeName}' (text starts with '{(volume.inkMasterfile.text.Length>1?volume.inkMasterfile.text[0]:'?')}')");
            }
            else
            {
                Debug.LogWarning($"[EventVolumeManager] Volume '{volume.volumeName}' has no masterfile assigned");
            }

        }
    }
    
    /// <summary>
    /// Bind C# functions to Ink story for external function calls
    /// </summary>
    private void BindExternalFunctions(Story story)
    {
        // Event Score Management
        story.BindExternalFunction("ModifyEventScore", (string scoreName, int change) => {
            EventSystemLogic.Instance?.ModifyEventScore(scoreName, change);
        });
        
        story.BindExternalFunction("GetEventScore", (string scoreName) => {
            return EventSystemLogic.Instance?.GetEventScore(scoreName) ?? 0;
        });
        
        // Resource Management
        story.BindExternalFunction("ModifyResource", (string resourceName, int change) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            if (gameUnitsLogic != null)
            {
                gameUnitsLogic.ChangeResourceFromName(resourceName, change, false);
            }
        });
        
        story.BindExternalFunction("GetResourceAmount", (string resourceName) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            return gameUnitsLogic?.GetResourceAmount(resourceName) ?? 0;
        });
        
        // Production Unit Management
        story.BindExternalFunction("ModifyProductionUnit", (string unitName, int change) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            if (gameUnitsLogic != null)
            {
                gameUnitsLogic.ChangeProductionUnitFromName(unitName, change);
            }
        });
        
        story.BindExternalFunction("BuildProductionUnit", (string unitName) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            return gameUnitsLogic?.BuildProductionUnit(unitName) ?? false;
        });
        
        // Technology Management
                    story.BindExternalFunction("TriggerTechnologyEnlightened", (string technologyName) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null)
            {
                GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == technologyName);
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
        });
        
        story.BindExternalFunction("CheckTechnology", (string technologyName) => {
            GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
            if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null)
            {
                GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == technologyName);
                if (techSlotObj != null)
                {
                    GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                    return techSlot != null && techSlot.isUnlocked;
                }
            }
            return false;
        });
        
        // Time System
        story.BindExternalFunction("GetCurrentSeventh", () => {
            TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
            return timeSystem?.CurrentSeventh ?? 1;
        });
        
        story.BindExternalFunction("GetCurrentPhase", () => {
            TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
            return timeSystem?.CurrentPhase ?? 1;
        });
        
        story.BindExternalFunction("GetCurrentEcho", () => {
            TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
            return timeSystem?.CurrentEcho ?? 1;
        });
        
        story.BindExternalFunction("GetCurrentCycle", () => {
            TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
            return timeSystem?.CurrentCycle ?? 1;
        });
        
        story.BindExternalFunction("IsRitualSeventh", () => {
            TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
            return timeSystem?.CurrentSeventh == 21;
        });
        
        // Stat Management
        story.BindExternalFunction("ModifyStat", (string statName, int change) => {
            StatManager statManager = EventSystemLogic.Instance?.GetStatManager();
            if (statManager != null)
            {
                int currentValue = statManager.GetStatValue(statName);
                statManager.UpdateStat(statName, currentValue + change);
            }
        });
        
        story.BindExternalFunction("GetStatValue", (string statName) => {
            StatManager statManager = EventSystemLogic.Instance?.GetStatManager();
            return statManager?.GetStatValue(statName) ?? 0;
        });
        
        // Population Management
        story.BindExternalFunction("ModifyPopulation", (int change) => {
            if (PopGrowthLogic.Instance != null)
            {
                // Only allow population removal (negative values)
                if (change < 0)
                {
                    PopGrowthLogic.Instance.ModifyPopulation(change);
                }
                else
                {
                    Debug.LogWarning($"ModifyPopulation called with positive value {change}. Use ModifyVagrants instead to add people.");
                }
            }
        });
        
        story.BindExternalFunction("GetPopulation", () => {
            return PopGrowthLogic.Instance?.population ?? 0;
        });
        
        story.BindExternalFunction("ModifyHousing", (int change) => {
            if (PopGrowthLogic.Instance != null)
            {
                PopGrowthLogic.Instance.ModifyHousing(change);
            }
        });
        
        story.BindExternalFunction("GetHousing", () => {
            return PopGrowthLogic.Instance?.housing ?? 0;
        });
        
        story.BindExternalFunction("ModifyVagrants", (int change) => {
            if (PopGrowthLogic.Instance != null)
            {
                PopGrowthLogic.Instance.ModifyVagrants(change);
            }
        });
        
        story.BindExternalFunction("GetVagrants", () => {
            return PopGrowthLogic.Instance?.vagrants ?? 0;
        });
        
        story.BindExternalFunction("ProcessEventDeaths", (int deathCount) => {
            if (PopGrowthLogic.Instance != null)
            {
                PopGrowthLogic.Instance.ProcessEventDeaths(deathCount);
            }
        });
        
        story.BindExternalFunction("GetDeaths", () => {
            return PopGrowthLogic.Instance?.deaths ?? 0;
        });
        
        story.BindExternalFunction("GetVagrantDeaths", () => {
            return PopGrowthLogic.Instance?.vagrantDeaths ?? 0;
        });
        
        story.BindExternalFunction("GetTrueDeaths", () => {
            return PopGrowthLogic.Instance?.trueDeaths ?? 0;
        });
        
        // Weather Management
        story.BindExternalFunction("ChangeWeather", (string weatherProfileName) => {
            if (CelestialWeatherSystemLogic.Instance != null)
            {
                WeatherProfileSO foundProfile = CelestialWeatherSystemLogic.FindWeatherProfile(weatherProfileName);
                if (foundProfile != null)
                {
                    CelestialWeatherSystemLogic.Instance.SetWeatherProfile(foundProfile, ignoreEchoValidation: true);
                    return true;
                }
            }
            return false;
        });
        
        story.BindExternalFunction("GetCurrentWeather", () => {
            if (CelestialWeatherSystemLogic.Instance != null)
            {
                return CelestialWeatherSystemLogic.Instance.GetCurrentWeatherName();
            }
            return "";
        });
        
        story.BindExternalFunction("IsWeather", (string weatherProfileName) => {
            if (CelestialWeatherSystemLogic.Instance != null)
            {
                return CelestialWeatherSystemLogic.Instance.IsWeatherActive(weatherProfileName);
            }
            return false;
        });
        
        story.BindExternalFunction("SetTimedWeather", (string weatherProfileName, int durationSevenths) => {
            if (CelestialWeatherSystemLogic.Instance != null)
            {
                WeatherProfileSO foundProfile = CelestialWeatherSystemLogic.FindWeatherProfile(weatherProfileName);
                if (foundProfile != null && durationSevenths > 0)
                {
                    CelestialWeatherSystemLogic.Instance.SetTimedWeatherProfile(foundProfile, durationSevenths, ignoreEchoValidation: true);
                    return true;
                }
            }
            return false;
        });
        
        // Utility Functions
        story.BindExternalFunction("Log", (string message) => {
            Debug.Log($"[Ink] {message}");
        });
        
        story.BindExternalFunction("Random", (int min, int max) => {
            return Random.Range(min, max + 1);
        });
    }
    
    /// <summary>
    /// Find the best available story node across all volumes
    /// </summary>
    public StoryNode FindBestAvailableStory()
    {
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Finding best available story from {eventVolumes.Count} volumes", "EventVolumeManager");
        
        // Check if there are any story nodes at all
        int totalStoryNodes = eventVolumes.Sum(v => v.storyNodes.Count);
        if (totalStoryNodes == 0)
        {
            GameLoggingSystem.Instance.LogEvent("[EventVolumeManager] No story nodes found in any volume - no events available", "EventVolumeManager");
            return null;
        }
        
        // Check if PopGrowthLogic is available for population-related conditions
        bool hasPopulationConditions = eventVolumes.Any(v => 
            v.storyNodes.Any(sn => sn.storyConditions.Any(c => 
                c.type == EventCondition.ConditionType.PopulationCheck ||
                c.type == EventCondition.ConditionType.HousingCheck ||
                c.type == EventCondition.ConditionType.VagrantsCheck ||
                c.type == EventCondition.ConditionType.DeathsCheck ||
                c.type == EventCondition.ConditionType.VagrantDeathsCheck)));
                
        if (hasPopulationConditions && PopGrowthLogic.Instance == null)
        {
            GameLoggingSystem.Instance.LogEvent("[EventVolumeManager] Some stories have population conditions but PopGrowthLogic.Instance is null - waiting for system to be ready", "EventVolumeManager");
            return null;
        }
        
        StoryNode bestStory = null;
        int highestPriority = -1;
        List<StoryNode> samePriorityCandidates = new List<StoryNode>();
        EventVolume bestVolume = null;
        
        foreach (EventVolume volume in eventVolumes)
        {
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Checking volume '{volume.volumeName}' (unlocked: {volume.isUnlocked})", "EventVolumeManager");
            
            if (!IsVolumeAvailable(volume)) 
            {
                GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Volume '{volume.volumeName}' is not available", "EventVolumeManager");
                continue;
            }
            
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Volume '{volume.volumeName}' is available, checking {volume.storyNodes.Count} story nodes", "EventVolumeManager");
            
            foreach (StoryNode storyNode in volume.storyNodes)
            {
                GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Checking story node '{storyNode.nodeName}' (unlocked: {storyNode.isUnlocked}, priority: {storyNode.priority})", "EventVolumeManager");
                
                if (!storyNode.isUnlocked) 
                {
                    GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' is locked", "EventVolumeManager");
                    continue;
                }
                
                if (AreStoryConditionsMet(storyNode))
                {
                    GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' meets conditions with priority {storyNode.priority}", "EventVolumeManager");
                    
                    if (storyNode.priority > highestPriority)
                    {
                        // New highest priority - clear previous candidates and start fresh
                        highestPriority = storyNode.priority;
                        samePriorityCandidates.Clear();
                        samePriorityCandidates.Add(storyNode);
                        bestVolume = volume;
                        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] New highest priority: '{storyNode.nodeName}' (priority {storyNode.priority})", "EventVolumeManager");
                    }
                    else if (storyNode.priority == highestPriority)
                    {
                        // Same priority - add to candidates for random selection
                        samePriorityCandidates.Add(storyNode);
                        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Added to same priority candidates: '{storyNode.nodeName}' (priority {storyNode.priority})", "EventVolumeManager");
                    }
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' did not meet conditions", "EventVolumeManager");

                }
            }
        }
        
        // Randomly select from highest priority candidates
        if (samePriorityCandidates.Count > 0)
        {
            int randomIndex = Random.Range(0, samePriorityCandidates.Count);
            bestStory = samePriorityCandidates[randomIndex];
            currentVolume = bestVolume;
            
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Randomly selected from {samePriorityCandidates.Count} candidates: '{bestStory.nodeName}' (priority {highestPriority})", "EventVolumeManager");

        }
        
        GameLoggingSystem.Instance.LogEvent(bestStory != null
            ? $"[EventVolumeManager] Selected best story '{bestStory.nodeName}' (priority {highestPriority})"
            : "[EventVolumeManager] No available story found", "EventVolumeManager");

        return bestStory;
    }
    
    /// <summary>
    /// Check if a volume is available based on its conditions
    /// </summary>
    private bool IsVolumeAvailable(EventVolume volume)
    {
        if (!volume.isUnlocked) return false;
        
        foreach (EventCondition condition in volume.volumeConditions)
        {
            if (!condition.Evaluate()) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if all conditions for a story node are met
    /// </summary>
    private bool AreStoryConditionsMet(StoryNode storyNode)
    {
        // Check cooldown FIRST before evaluating other conditions
        if (storyNode.cooldownSevenths > 0)
        {
            EventSystemLogic eventSystem = EventSystemLogic.Instance;
            if (eventSystem != null && eventSystem.IsEventOnCooldown(storyNode.nodeName, storyNode.cooldownSevenths))
            {
                GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' is on cooldown - skipping", "EventVolumeManager");

                return false; // Event is on cooldown, don't allow it to trigger
            }
        }
        
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Checking conditions for story '{storyNode.nodeName}' - {storyNode.storyConditions.Count} conditions to evaluate", "EventVolumeManager");
        
        if (storyNode.storyConditions.Count == 0)
        {
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' has no conditions - allowing trigger", "EventVolumeManager");

            return true;
        }
        
        // Check if PopGrowthLogic is available for population-related conditions
        bool hasPopulationConditions = storyNode.storyConditions.Any(c => 
            c.type == EventCondition.ConditionType.PopulationCheck ||
            c.type == EventCondition.ConditionType.HousingCheck ||
            c.type == EventCondition.ConditionType.VagrantsCheck ||
            c.type == EventCondition.ConditionType.DeathsCheck ||
            c.type == EventCondition.ConditionType.VagrantDeathsCheck);
            
        if (hasPopulationConditions && PopGrowthLogic.Instance == null)
        {
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' has population conditions but PopGrowthLogic.Instance is null - blocking trigger", "EventVolumeManager");

            return false;
        }
        
        foreach (EventCondition condition in storyNode.storyConditions)
        {
            bool result = condition.Evaluate();
            GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Condition check for '{storyNode.nodeName}': {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue} => {result}", "EventVolumeManager");

            if (!result) 
            {
                GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Story '{storyNode.nodeName}' blocked by condition: {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue}", "EventVolumeManager");

                return false;
            }
        }
        
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] All conditions met for story '{storyNode.nodeName}' - allowing trigger", "EventVolumeManager");

        return true;
    }
    
    /// <summary>
    /// Start a story node
    /// </summary>
    public void StartStory(StoryNode storyNode, EventVolume volume)
    {
        currentStoryNode = storyNode;
        currentVolume = volume;
        currentScreenIndex = 0;
        
        // Load the story's Ink content
        if (volumeStories.ContainsKey(volume.volumeName))
        {
            Story story = volumeStories[volume.volumeName];
            story.ChoosePathString(storyNode.nodeName);
        }
        
        // Start the screen flow
        ExecuteNextScreen();
    }
    
    /// <summary>
    /// Execute the next screen in the current story's flow
    /// </summary>
    public void ExecuteNextScreen()
    {
        // Dynamic overrides are deprecated; flow now navigates directly by knot. Preserve for legacy no-op.

        if (currentStoryNode == null || currentScreenIndex >= currentStoryNode.screenFlow.Count)
        {
            CompleteStory();
            return;
        }
        
        ScreenFlowStep step = currentStoryNode.screenFlow[currentScreenIndex];
        
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Processing ScreenFlowStep: type={step.flowType}, id={step.screenId}, inkKnot={step.inkKnot}", "EventVolumeManager");
        
        // Create EventScreen from ScreenFlowStep
        EventScreen screen = new EventScreen
        {
            screenType = ConvertFlowTypeToScreenType(step.flowType),
            screenId = step.screenId,
            displayDuration = step.displayDuration,
            waitForInput = step.waitForInput,
            inkKnot = step.inkKnot
        };
        
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] Created EventScreen: type={screen.screenType}, id={screen.screenId}, inkKnot={screen.inkKnot}", "EventVolumeManager");
        
        // Execute the screen
        EventSystemLogic.Instance?.ExecuteScreen(screen);
        
        currentScreenIndex++;
    }

    // Deprecated override API retained as no-ops to avoid breaking references
    public void SetNextScreensOverride(System.Collections.Generic.IEnumerable<ScreenFlowStep> steps) {}

    /// <summary>
    /// Advance the static flow index by the specified number of steps, to avoid duplicating the just-overridden screen.
    /// </summary>
    public void AdvanceStaticFlow(int steps = 1)
    {
        // Deprecated with direct navigation; keep no-op for compatibility
    }

    /// <summary>
    /// Navigate directly to a specific Ink knot by name and render its corresponding screen.
    /// </summary>
    public void NavigateToKnot(string knotName)
    {
        if (string.IsNullOrEmpty(knotName))
        {
            CompleteStory();
            return;
        }
        // Normalize to top-level knot name to avoid internal path suffixes
        string normalized = InkDrivenEventSetup.NormalizeKnotName(knotName) ?? knotName;
        // Choose screen type based on naming convention (prefix match to avoid accidental contains)
        var lower = normalized.ToLower();
        ScreenType type = ScreenType.Verse;
        if (lower.EndsWith("_chorus") || lower.Contains("_chorus_")) type = ScreenType.Chorus;
        else if (lower.EndsWith("_outro") || lower.Contains("_outro_")) type = ScreenType.Outro;
        else if (lower.EndsWith("_bridge") || lower.Contains("_bridge_")) type = ScreenType.Bridge;
        // Build and execute screen
        var screen = new EventScreen
        {
            screenType = type,
            screenId = normalized + "_screen",
            inkKnot = normalized,
            waitForInput = true
        };
        GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] NavigateToKnot: knot='{normalized}', inferredType={type}", "EventVolumeManager");

        EventSystemLogic.Instance?.ExecuteScreen(screen);
    }
    
    /// <summary>
    /// Convert ScreenFlowStep.FlowType to ScreenType
    /// </summary>
    private ScreenType ConvertFlowTypeToScreenType(ScreenFlowStep.FlowType flowType)
    {
        switch (flowType)
        {
            case ScreenFlowStep.FlowType.Splash: return ScreenType.Splash;
            case ScreenFlowStep.FlowType.Verse: return ScreenType.Verse;
            case ScreenFlowStep.FlowType.Chorus: return ScreenType.Chorus;
            case ScreenFlowStep.FlowType.Bridge: return ScreenType.Bridge;
            case ScreenFlowStep.FlowType.Outro: return ScreenType.Outro;
            default: return ScreenType.Splash;
        }
    }
    
    /// <summary>
    /// Complete the current story and apply consequences
    /// </summary>
    public void CompleteStory()
    {
        if (currentStoryNode == null || currentVolume == null)
        {
            Debug.LogWarning("[EventVolumeManager] Cannot complete story: no active story or volume");
            return;
        }

        Debug.Log($"[EventVolumeManager] Completing story: {currentStoryNode.storyTitle}");

        // Notify event system that story is complete - it will handle everything else
        if (EventSystemLogic.Instance != null)
        {
            EventSystemLogic.Instance.OnStoryCompleted();
        }
        else
        {
            Debug.LogError("[EventVolumeManager] EventSystemLogic.Instance is null!");
        }

        GameLoggingSystem.Instance.LogEvent("[EventVolumeManager] Story completion delegated to EventSystemLogic", "EventVolumeManager");
    }
    

    
    /// <summary>
    /// Add a volume to the manager
    /// </summary>
    public void AddVolume(EventVolume volume)
    {
        if (!eventVolumes.Contains(volume))
        {
            eventVolumes.Add(volume);
            InitializeVolumeLookups();
            
            if (volume.inkMasterfile != null)
            {
                Story story = new Story(volume.inkMasterfile.text);
                volumeStories[volume.volumeName] = story;
                BindExternalFunctions(story);

                GameLoggingSystem.Instance.LogEvent($"[EventVolumeManager] AddVolume loaded story for '{volume.volumeName}'", "EventVolumeManager");
            }
            else
            {
                Debug.LogWarning($"[EventVolumeManager] AddVolume: '{volume.volumeName}' has null masterfile");

            }
        }
    }
    
    /// <summary>
    /// Get a story by volume and node name
    /// </summary>
    public StoryNode GetStoryNode(string volumeName, string nodeName)
    {
        string key = $"{volumeName}.{nodeName}";
        return storyNodeLookup.ContainsKey(key) ? storyNodeLookup[key] : null;
    }
    
    /// <summary>
    /// Get the current story's Ink story
    /// </summary>
    public Story GetCurrentStory()
    {
        if (currentVolume != null && volumeStories.ContainsKey(currentVolume.volumeName))
        {
            return volumeStories[currentVolume.volumeName];
        }
        return null;
    }
    
    /// <summary>
    /// Get the current story node
    /// </summary>
    public StoryNode GetCurrentStoryNode()
    {
        return currentStoryNode;
    }

    /// <summary>
    /// Get the current volume
    /// </summary>
    public EventVolume GetCurrentVolume()
    {
        return currentVolume;
    }
    
    /// <summary>
    /// Get the current screen index for debugging purposes
    /// </summary>
    public int GetCurrentScreenIndex()
    {
        return currentScreenIndex;
    }
} 