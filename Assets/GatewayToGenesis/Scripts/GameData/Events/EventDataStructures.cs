using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a Volume (DLC) containing multiple storylines in a single Ink masterfile
/// </summary>
[System.Serializable]
public class EventVolume
{
    public string volumeName; // e.g., "Tutorial Volume", "Main Quest Volume"
    public string volumeDescription;
    public TextAsset inkMasterfile; // The .ink file containing all stories for this volume
    public bool isUnlocked = true;
    public int priority = 0; // Volume priority for loading order
    
    [Header("Volume Conditions")]
    public List<EventCondition> volumeConditions = new List<EventCondition>(); // Conditions to unlock this volume
    
    [Header("Story Nodes")]
    public List<StoryNode> storyNodes = new List<StoryNode>(); // Individual story nodes within this volume
}

/// <summary>
/// Represents a single story node within an Ink masterfile
/// </summary>
[System.Serializable]
public class StoryNode
{
    public string nodeName; // The knot name in Ink (e.g., "tutorial_start", "main_quest_beginning")
    public string storyTitle; // Display name for the story
    public string storyDescription;
    public bool isUnlocked = true;
    public int priority = 0; // Story priority within the volume
    
    [Header("Story Conditions")]
    public List<EventCondition> storyConditions = new List<EventCondition>(); // Conditions to trigger this story
    
    [Header("Story Consequences")]
    public List<EventConsequence> storyConsequences = new List<EventConsequence>(); // What happens when story completes
    
    [Header("Screen Flow")]
    public List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>(); // How screens progress through this story
    
    // UI metadata for controlling UI elements from Ink
    public Dictionary<string, string> uiMetadata = new Dictionary<string, string>();
}

/// <summary>
/// Defines how screens flow through a story
/// </summary>
[System.Serializable]
public class ScreenFlowStep
{
    public enum FlowType
    {
        Splash,     // Intro screen
        Verse,      // Narrative text (Ink)
        Chorus,     // Decision point
        Bridge,     // Transition
        Outro       // Results/conclusion
    }
    
    public FlowType flowType;
    public string screenId; // Unique identifier for this screen
    public float displayDuration = 2f; // For splash/outro screens
    public bool waitForInput = true; // Whether to wait for player input
    public string inkKnot; // Which Ink knot to load for this step (for Verse/Chorus)
}

/// <summary>
/// Event score tracking for story progression
/// </summary>
[System.Serializable]
public class EventScore
{
    public string name;
    public int value;
    public string description;
}

/// <summary>
/// Conditions that must be met for events to trigger
/// </summary>
[System.Serializable]
public class EventCondition
{
    public enum ConditionType
    {
        ScoreCheck,         // Check event score value
        StatCheck,          // Check civilization stat value
        ResourceCheck,      // Check resource amount
        TechnologyCheck,    // Check if technology is unlocked
        SeventhCheck,       // Check current seventh
        PhaseCheck,         // Check current phase
        EchoCheck,          // Check current echo
        CycleCheck,         // Check current cycle
        RitualSeventhCheck, // Check if it's a ritual seventh
        PopulationCheck,    // Check population amount
        HousingCheck,       // Check housing amount
        VagrantsCheck,      // Check vagrants amount
        DeathsCheck,        // Check deaths amount
        VagrantDeathsCheck,  // Check vagrant deaths amount
        TrueDeathsCheck      // Check true deaths amount (cannot be revised)
    }
    
    public ConditionType type;
    public string targetName; // Score name, stat name, resource name, etc.
    public int requiredValue;
    public ComparisonOperator comparison;
    
    public bool Evaluate()
    {
        switch (type)
        {
            case ConditionType.ScoreCheck:
                int currentScore = EventSystemLogic.Instance?.GetEventScore(targetName) ?? 0;
                return CompareValues(currentScore, requiredValue, comparison);
                
            case ConditionType.StatCheck:
                StatManager statManager = EventSystemLogic.Instance?.GetStatManager();
                if (statManager != null)
                {
                    int statValue = statManager.GetStatValue(targetName);
                    return CompareValues(statValue, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.ResourceCheck:
                GameUnitsLogic gameUnitsLogic = EventSystemLogic.Instance?.GetGameUnitsLogic();
                if (gameUnitsLogic != null)
                {
                    int resourceAmount = gameUnitsLogic.GetResourceAmount(targetName);
                    return CompareValues(resourceAmount, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.TechnologyCheck:
                return CheckTechnologyUnlocked(targetName);
                
            case ConditionType.SeventhCheck:
                TimeSystemLogic timeSystem = EventSystemLogic.Instance?.GetTimeSystem();
                if (timeSystem != null)
                {
                    return CompareValues(timeSystem.CurrentSeventh, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.PhaseCheck:
                TimeSystemLogic phaseSystem = EventSystemLogic.Instance?.GetTimeSystem();
                if (phaseSystem != null)
                {
                    return CompareValues(phaseSystem.CurrentPhase, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.EchoCheck:
                TimeSystemLogic echoSystem = EventSystemLogic.Instance?.GetTimeSystem();
                if (echoSystem != null)
                {
                    return CompareValues(echoSystem.CurrentEcho, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.CycleCheck:
                TimeSystemLogic cycleSystem = EventSystemLogic.Instance?.GetTimeSystem();
                if (cycleSystem != null)
                {
                    return CompareValues(cycleSystem.CurrentCycle, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.RitualSeventhCheck:
                TimeSystemLogic ritualSystem = EventSystemLogic.Instance?.GetTimeSystem();
                if (ritualSystem != null)
                {
                    return ritualSystem.CurrentSeventh == 21; // Ritual seventh is always 21
                }
                return false;
                
            case ConditionType.PopulationCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int populationAmount = PopGrowthLogic.Instance.population;
                    return CompareValues(populationAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate PopulationCheck condition");
                    return false;
                }
                
            case ConditionType.HousingCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int housingAmount = PopGrowthLogic.Instance.housing;
                    return CompareValues(housingAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate HousingCheck condition");
                    return false;
                }
                
            case ConditionType.VagrantsCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int vagrantsAmount = PopGrowthLogic.Instance.vagrants;
                    return CompareValues(vagrantsAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate VagrantsCheck condition");
                    return false;
                }
                
            case ConditionType.DeathsCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int deathsAmount = PopGrowthLogic.Instance.deaths;
                    return CompareValues(deathsAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate DeathsCheck condition");
                    return false;
                }
                
            case ConditionType.VagrantDeathsCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int vagrantDeathsAmount = PopGrowthLogic.Instance.vagrantDeaths;
                    return CompareValues(vagrantDeathsAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate VagrantDeathsCheck condition");
                    return false;
                }
                
            case ConditionType.TrueDeathsCheck:
                if (PopGrowthLogic.Instance != null)
                {
                    int trueDeathsAmount = PopGrowthLogic.Instance.trueDeaths;
                    return CompareValues(trueDeathsAmount, requiredValue, comparison);
                }
                else
                {
                    Debug.LogWarning("[EventDataStructures] PopGrowthLogic.Instance is null - cannot evaluate TrueDeathsCheck condition");
                    return false;
                }
                
            default:
                return false;
        }
    }
    
    private bool CompareValues(int actual, int expected, ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return actual == expected;
            case ComparisonOperator.NotEquals: return actual != expected;
            case ComparisonOperator.GreaterThan: return actual > expected;
            case ComparisonOperator.LessThan: return actual < expected;
            case ComparisonOperator.GreaterThanOrEqual: return actual >= expected;
            case ComparisonOperator.LessThanOrEqual: return actual <= expected;
            default: return false;
        }
    }
    
    private bool CheckTechnologyUnlocked(string technologyName)
    {
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
    }
}

/// <summary>
/// Consequences that occur when events complete
/// </summary>
[System.Serializable]
public class EventConsequence
{
    public enum ConsequenceType
    {
        ScoreChange,           // Modify event score
        StatChange,            // Modify civilization stat
        ResourceChange,        // Modify resource amount
        ProductionUnitChange,  // Modify production unit amount
        TechnologyEnlightened,      // Trigger technology Enlightened state
        UnlockEvent,          // Unlock another event
        PopulationChange,     // Only for REMOVING population (registers deaths)
        HousingChange,        // Modify housing amount
        VagrantsChange,       // Modify vagrants amount (can become population)
        DeathsChange,         // Process event deaths (kill population, track deaths)
        DeathRecordsRevision  // Revise death records for evil empire history manipulation
    }
    
    public ConsequenceType type;
    public string targetName; // Score name, stat name, resource name, etc.
    public int value; // Amount to change
}

/// <summary>
/// Comparison operators for conditions
/// </summary>
public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

/// <summary>
/// Screen types for modular UI
/// </summary>
public enum ScreenType
{
    Splash,     // Intro screen
    Verse,      // Narrative text (Ink)
    Chorus,     // Decision point
    Bridge,     // Transition
    Outro       // Results/conclusion
}

/// <summary>
/// Represents a single screen in the event system
/// </summary>
[System.Serializable]
public class EventScreen
{
    public string screenId;
    public ScreenType screenType;
    public string title;
    public string description;
    public string inkKnot;
    
    // New UI elements for splash and outro screens
    public Sprite splashImage;
    public string buttonText;
    public EventType eventType = EventType.Environmental;
    
    // Display settings
    public float displayDuration = 3f;
    public bool waitForInput = true;
}

/// <summary>
/// Represents a choice in a decision screen
/// </summary>
[System.Serializable]
public class EventChoice
{
    public string choiceText;
    public string inkPath; // The Ink path to follow
    public List<EventConsequence> consequences = new List<EventConsequence>();
}

public enum EventType
{
    Environmental,
    Mystical,
    Social,
    Crisis
}

 