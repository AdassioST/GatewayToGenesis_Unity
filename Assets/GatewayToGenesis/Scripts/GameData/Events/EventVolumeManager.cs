using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using System.Linq; // Added for .Any()

/// <summary>
/// Manages Event Volumes (DLC-style story collections) loaded from Ink masterfiles
/// </summary>
public class EventVolumeManager : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool enableDebugLogging = true;
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

        if (enableDebugLogging)
        {
            Debug.Log($"[EventVolumeManager] Initialized lookups: volumes={eventVolumes.Count}, storyNodes={storyNodeLookup.Count}");
        }
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

                if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] Loaded story for volume '{volume.volumeName}' (text starts with '{(volume.inkMasterfile.text.Length>1?volume.inkMasterfile.text[0]:'?')}')");
                }
            }
            else if (enableDebugLogging)
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
        
        // Utility Functions
        story.BindExternalFunction("Log", (string message) => {
            Debug.Log($"[Ink] {message}");
        });
    }
    
    /// <summary>
    /// Find the best available story node across all volumes
    /// </summary>
    public StoryNode FindBestAvailableStory()
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[EventVolumeManager] Finding best available story from {eventVolumes.Count} volumes");
        }
        
        // Check if there are any story nodes at all
        int totalStoryNodes = eventVolumes.Sum(v => v.storyNodes.Count);
        if (totalStoryNodes == 0)
        {
            if (enableDebugLogging)
            {
                Debug.Log("[EventVolumeManager] No story nodes found in any volume - no events available");
            }
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
            if (enableDebugLogging)
            {
                Debug.Log("[EventVolumeManager] Some stories have population conditions but PopGrowthLogic.Instance is null - waiting for system to be ready");
            }
            return null;
        }
        
        StoryNode bestStory = null;
        int highestPriority = -1;
        List<StoryNode> samePriorityCandidates = new List<StoryNode>();
        EventVolume bestVolume = null;
        
        foreach (EventVolume volume in eventVolumes)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Checking volume '{volume.volumeName}' (unlocked: {volume.isUnlocked})");
            }
            
            if (!IsVolumeAvailable(volume)) 
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] Volume '{volume.volumeName}' is not available");
                }
                continue;
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Volume '{volume.volumeName}' is available, checking {volume.storyNodes.Count} story nodes");
            }
            
            foreach (StoryNode storyNode in volume.storyNodes)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] Checking story node '{storyNode.nodeName}' (unlocked: {storyNode.isUnlocked}, priority: {storyNode.priority})");
                }
                
                if (!storyNode.isUnlocked) 
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' is locked");
                    }
                    continue;
                }
                
                if (AreStoryConditionsMet(storyNode))
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' meets conditions with priority {storyNode.priority}");
                    }
                    
                    if (storyNode.priority > highestPriority)
                    {
                        // New highest priority - clear previous candidates and start fresh
                        highestPriority = storyNode.priority;
                        samePriorityCandidates.Clear();
                        samePriorityCandidates.Add(storyNode);
                        bestVolume = volume;
                        if (enableDebugLogging)
                        {
                            Debug.Log($"[EventVolumeManager] New highest priority: '{storyNode.nodeName}' (priority {storyNode.priority})");
                        }
                    }
                    else if (storyNode.priority == highestPriority)
                    {
                        // Same priority - add to candidates for random selection
                        samePriorityCandidates.Add(storyNode);
                        if (enableDebugLogging)
                        {
                            Debug.Log($"[EventVolumeManager] Added to same priority candidates: '{storyNode.nodeName}' (priority {storyNode.priority})");
                        }
                    }
                }
                else if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' did not meet conditions");
                }
            }
        }
        
        // Randomly select from highest priority candidates
        if (samePriorityCandidates.Count > 0)
        {
            int randomIndex = Random.Range(0, samePriorityCandidates.Count);
            bestStory = samePriorityCandidates[randomIndex];
            currentVolume = bestVolume;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Randomly selected from {samePriorityCandidates.Count} candidates: '{bestStory.nodeName}' (priority {highestPriority})");
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log(bestStory != null
                ? $"[EventVolumeManager] Selected best story '{bestStory.nodeName}' (priority {highestPriority})"
                : "[EventVolumeManager] No available story found");
        }
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
        if (enableDebugLogging)
        {
            Debug.Log($"[EventVolumeManager] Checking conditions for story '{storyNode.nodeName}' - {storyNode.storyConditions.Count} conditions to evaluate");
        }
        
        if (storyNode.storyConditions.Count == 0)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' has no conditions - allowing trigger");
            }
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
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' has population conditions but PopGrowthLogic.Instance is null - blocking trigger");
            }
            return false;
        }
        
        foreach (EventCondition condition in storyNode.storyConditions)
        {
            bool result = condition.Evaluate();
            if (enableDebugLogging)
            {
                Debug.Log($"[EventVolumeManager] Condition check for '{storyNode.nodeName}': {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue} => {result}");
            }
            if (!result) 
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] Story '{storyNode.nodeName}' blocked by condition: {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue}");
                }
                return false;
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[EventVolumeManager] All conditions met for story '{storyNode.nodeName}' - allowing trigger");
        }
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
        if (currentStoryNode == null || currentScreenIndex >= currentStoryNode.screenFlow.Count)
        {
            CompleteStory();
            return;
        }
        
        ScreenFlowStep step = currentStoryNode.screenFlow[currentScreenIndex];
        
        // Create EventScreen from ScreenFlowStep
        EventScreen screen = new EventScreen
        {
            screenType = ConvertFlowTypeToScreenType(step.flowType),
            screenId = step.screenId,
            displayDuration = step.displayDuration,
            waitForInput = step.waitForInput,
            inkKnot = step.inkKnot
        };
        
        // Execute the screen
        EventSystemLogic.Instance?.ExecuteScreen(screen);
        
        currentScreenIndex++;
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

        Debug.Log("[EventVolumeManager] Story completion delegated to EventSystemLogic");
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
                if (enableDebugLogging)
                {
                    Debug.Log($"[EventVolumeManager] AddVolume loaded story for '{volume.volumeName}'");
                }
            }
            else if (enableDebugLogging)
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