using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

// ===== INK STORY MANAGER =====
// PSEUDOCODE: Manages Ink narrative engine integration and external function bindings
// PURPOSE: Provides narrative content, choices, and game state interaction for events
// FLOW: Load story -> Bind functions -> Provide content -> Handle choices -> Update state
/// <summary>
/// Manages Ink story integration with the event system
/// </summary>
public class InkStoryManager : MonoBehaviour
{
    // PSEUDOCODE: Configuration settings for story management
    [Header("Ink Settings")]
    [SerializeField] private bool enableStoryLogging = true;  // PSEUDOCODE: Enable debug logging for story operations
    [SerializeField] private bool saveStoryState = true;      // PSEUDOCODE: Enable story state persistence
    
    // PSEUDOCODE: Current story runtime state
    private Story currentStory;                              // PSEUDOCODE: Active Ink story instance
    private TextAsset currentStoryAsset;                     // PSEUDOCODE: Currently loaded story file
    
    // PSEUDOCODE: Story state tracking for debugging and persistence
    private Dictionary<string, object> storyVariables = new Dictionary<string, object>(); // PSEUDOCODE: Story variable cache
    private Dictionary<string, object> storyTags = new Dictionary<string, object>();      // PSEUDOCODE: Story tag cache
    
    // PSEUDOCODE: External system references for function bindings
    private StatManager statManager;                         // PSEUDOCODE: Player stats manager
    private GameUnitsLogic gameUnitsLogic;                   // PSEUDOCODE: Resource management system
    private EventSystemLogic eventSystem;                    // PSEUDOCODE: Main event system coordinator
    
    // PSEUDOCODE: Initialize component references for external function bindings
    private void Awake()
    {
        // PSEUDOCODE: Get references to external systems for Ink function bindings
        eventSystem = GetComponent<EventSystemLogic>();      // PSEUDOCODE: Get event system from same GameObject
        
        // PSEUDOCODE: Get system references from EventSystemLogic to avoid FindObjectOfType calls
        if (eventSystem != null)
        {
            statManager = eventSystem.GetStatManager();      // PSEUDOCODE: Get stats manager from EventSystemLogic
            gameUnitsLogic = eventSystem.GetGameUnitsLogic(); // PSEUDOCODE: Get resource manager from EventSystemLogic
        }
        else
        {
            // PSEUDOCODE: Fallback to FindObjectOfType if EventSystemLogic not found
            statManager = FindObjectOfType<StatManager>();   // PSEUDOCODE: Find stats manager in scene
            gameUnitsLogic = FindObjectOfType<GameUnitsLogic>(); // PSEUDOCODE: Find resource manager in scene
        }
    }
    
    /// <summary>
    /// Get precompiled choice metadata for a chorus knot (built at game start from Resources/Events)
    /// </summary>
    public List<ChorusChoiceData> GetPrecompiledChoicesForKnot(string knotName)
    {
        return InkDrivenEventSetup.GetChoicesForKnot(knotName);
    }

    /// <summary>
    /// Static convenience to access precompiled choice metadata
    /// </summary>
    public static List<ChorusChoiceData> GetPrecompiledChoices(string knotName)
    {
        return InkDrivenEventSetup.GetChoicesForKnot(knotName);
    }

    // PSEUDOCODE: Main story loading function - creates new Ink story instance
    // FLOW: Validate asset -> Create story instance -> Bind external functions -> Log success
    /// <summary>
    /// Load a new Ink story
    /// </summary>
    public void LoadStory(TextAsset storyAsset)
    {
        if (storyAsset == null)                             // PSEUDOCODE: Validate story asset exists
        {
            LogStory("Cannot load null story asset");
            return;
        }
        
        currentStoryAsset = storyAsset;                     // PSEUDOCODE: Store reference to loaded asset
        currentStory = new Story(storyAsset.text);          // PSEUDOCODE: Create new Ink story instance
        
        // PSEUDOCODE: Setup external function bindings for game state interaction
        BindExternalFunctions();
        
        LogStory($"Loaded story: {storyAsset.name}");       // PSEUDOCODE: Log successful story load
    }
    
    // PSEUDOCODE: Accessor for current story instance
    /// <summary>
    /// Get the current story instance
    /// </summary>
    public Story GetCurrentStory()
    {
        return currentStory;                                // PSEUDOCODE: Return active story instance
    }
    
    // PSEUDOCODE: Navigation function - jumps to specific story section
    /// <summary>
    /// Jump to a specific knot in the current story
    /// </summary>
    public void JumpToKnot(string knotName)
    {
        if (currentStory != null)                          // PSEUDOCODE: Check if story is loaded
        {
            currentStory.ChoosePathString(knotName);       // PSEUDOCODE: Navigate to specified story section
            LogStory($"Jumped to knot: {knotName}");       // PSEUDOCODE: Log navigation action
        }
    }
    
    // PSEUDOCODE: Story progression function - advances story and returns text
    // FLOW: Check if story can continue -> Advance story -> Return narrative text
    /// <summary>
    /// Continue the story and return the text
    /// </summary>
    public string ContinueStory()
    {
        if (currentStory != null && currentStory.canContinue) // PSEUDOCODE: Check if story can progress
        {
            string text = currentStory.ContinueMaximally(); // PSEUDOCODE: Advance story and get text
            LogStory($"Story continued: {text}");          // PSEUDOCODE: Log story progression
            return text;                                    // PSEUDOCODE: Return narrative content
        }
        return "";                                         // PSEUDOCODE: Return empty if no progression possible
    }
    
    // PSEUDOCODE: Choice accessor - returns available player choices
    /// <summary>
    /// Get current choices from the story
    /// </summary>
    public List<Choice> GetCurrentChoices()
    {
        if (currentStory != null)                          // PSEUDOCODE: Check if story is loaded
        {
            return currentStory.currentChoices;             // PSEUDOCODE: Return available choices
        }
        return new List<Choice>();                         // PSEUDOCODE: Return empty list if no story
    }
    
    // PSEUDOCODE: Choice execution - processes player decision
    /// <summary>
    /// Make a choice in the story
    /// </summary>
    public void MakeChoice(int choiceIndex)
    {
        if (currentStory != null && choiceIndex < currentStory.currentChoices.Count) // PSEUDOCODE: Validate choice
        {
            currentStory.ChooseChoiceIndex(choiceIndex);   // PSEUDOCODE: Execute player choice
            LogStory($"Made choice: {choiceIndex}");       // PSEUDOCODE: Log choice action
        }
    }
    
    /// <summary>
    /// Set a variable in the Ink story
    /// </summary>
    public void SetVariable(string variableName, object value)
    {
        if (currentStory != null)
        {
            currentStory.variablesState[variableName] = value;
            storyVariables[variableName] = value;
            LogStory($"Set variable {variableName} = {value}");
        }
    }
    
    /// <summary>
    /// Get a variable from the Ink story
    /// </summary>
    public object GetVariable(string variableName)
    {
        if (currentStory != null)
        {
            return currentStory.variablesState[variableName];
        }
        return null;
    }
    
    /// <summary>
    /// Observe a variable in the Ink story
    /// </summary>
    public void ObserveVariable(string variableName, System.Action<string, object> callback)
    {
        if (currentStory != null)
        {
            currentStory.ObserveVariable(variableName, (string varName, object newValue) => callback(varName, newValue));
        }
    }
    
    /// <summary>
    /// Get all current tags from the story
    /// </summary>
    public List<string> GetCurrentTags()
    {
        if (currentStory != null)
        {
            return currentStory.currentTags;
        }
        return new List<string>();
    }
    
    /// <summary>
    /// Check if the story can continue
    /// </summary>
    public bool CanContinue()
    {
        return currentStory != null && currentStory.canContinue;
    }
    
    /// <summary>
    /// Check if the story has choices
    /// </summary>
    public bool HasChoices()
    {
        return currentStory != null && currentStory.currentChoices.Count > 0;
    }
    
    /// <summary>
    /// Save the current story state
    /// </summary>
    public string SaveStoryState()
    {
        if (currentStory != null && saveStoryState)
        {
            string state = currentStory.state.ToJson();
            LogStory("Story state saved");
            return state;
        }
        return "";
    }
    
    /// <summary>
    /// Load a story state
    /// </summary>
    public void LoadStoryState(string stateJson)
    {
        if (currentStory != null && !string.IsNullOrEmpty(stateJson))
        {
            currentStory.state.LoadJson(stateJson);
            LogStory("Story state loaded");
        }
    }
    
    /// <summary>
    /// Reset the story to its initial state
    /// </summary>
    public void ResetStory()
    {
        if (currentStory != null)
        {
            currentStory.ResetState();
            LogStory("Story reset");
        }
    }
    
    // PSEUDOCODE: External function binding system - allows Ink to call C# functions
    // PURPOSE: Enables Ink stories to interact with game state (stats, resources, events)
    // FLOW: Bind each function -> Ink can call function -> C# executes logic -> Return result to Ink
    /// <summary>
    /// Bind external functions to the Ink story
    /// </summary>
    private void BindExternalFunctions()
    {
        if (currentStory == null) return;                  // PSEUDOCODE: Exit if no story loaded
        
        // PSEUDOCODE: Stat checking functions - allow Ink to check player stats
        currentStory.BindExternalFunction("CheckStat", (string statName, int requiredValue) => 
        {
            if (statManager != null)                       // PSEUDOCODE: Check if stats manager exists
            {
                return statManager.CheckStat(statName, requiredValue); // PSEUDOCODE: Check if stat meets requirement
            }
            return false;                                  // PSEUDOCODE: Return false if manager not found
        });
        
        currentStory.BindExternalFunction("GetStat", (string statName) => 
        {
            if (statManager != null)                       // PSEUDOCODE: Check if stats manager exists
            {
                return statManager.GetStatValue(statName); // PSEUDOCODE: Get current stat value
            }
            return 0;                                      // PSEUDOCODE: Return 0 if manager not found
        });
        
        // PSEUDOCODE: Resource checking functions - allow Ink to check player resources
        currentStory.BindExternalFunction("CheckResource", (string resourceName, int requiredAmount) => 
        {
            if (gameUnitsLogic != null)                    // PSEUDOCODE: Check if resource manager exists
            {
                int currentAmount = gameUnitsLogic.GetResourceAmount(resourceName); // PSEUDOCODE: Get current resource amount
                return currentAmount >= requiredAmount;     // PSEUDOCODE: Check if player has enough resources
            }
            return false;                                   // PSEUDOCODE: Return false if manager not found
        });
        
        currentStory.BindExternalFunction("GetResource", (string resourceName) => 
        {
            if (gameUnitsLogic != null)                    // PSEUDOCODE: Check if resource manager exists
            {
                return gameUnitsLogic.GetResourceAmount(resourceName); // PSEUDOCODE: Get current resource amount
            }
            return 0;                                       // PSEUDOCODE: Return 0 if manager not found
        });
        
        // PSEUDOCODE: Event score functions - allow Ink to check story progress
        currentStory.BindExternalFunction("CheckEventScore", (string scoreName, int requiredValue) => 
        {
            if (eventSystem != null)                       // PSEUDOCODE: Check if event system exists
            {
                int currentScore = eventSystem.GetEventScore(scoreName); // PSEUDOCODE: Get current story progress
                return currentScore >= requiredValue;       // PSEUDOCODE: Check if progress meets requirement
            }
            return false;                                   // PSEUDOCODE: Return false if system not found
        });
        
        currentStory.BindExternalFunction("GetEventScore", (string scoreName) => 
        {
            if (eventSystem != null)                       // PSEUDOCODE: Check if event system exists
            {
                return eventSystem.GetEventScore(scoreName); // PSEUDOCODE: Get current story progress value
            }
            return 0;                                       // PSEUDOCODE: Return 0 if system not found
        });
        
        // PSEUDOCODE: Technology checking functions - allow Ink to check if technologies are researched
        currentStory.BindExternalFunction("CheckTechnology", (string technologyName) => 
        {
            if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null) // PSEUDOCODE: Check if research system exists
            {
                // PSEUDOCODE: Find technology slot and check if it's unlocked
                GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == technologyName);
                if (techSlotObj != null)
                {
                    GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                    return techSlot != null && techSlot.isUnlocked; // PSEUDOCODE: Return true if technology is researched
                }
            }
            return false;                                   // PSEUDOCODE: Return false if technology not found or not researched
        });
        
        // PSEUDOCODE: Stat modification functions - allow Ink to change player stats
        currentStory.BindExternalFunction("ModifyStat", (string statName, int change) => 
        {
            if (statManager != null)                       // PSEUDOCODE: Check if stats manager exists
            {
                int currentValue = statManager.GetStatValue(statName); // PSEUDOCODE: Get current stat value
                statManager.UpdateStat(statName, currentValue + change); // PSEUDOCODE: Update stat with change
                LogStory($"Modified stat {statName} by {change}"); // PSEUDOCODE: Log stat modification
            }
        });
        
        // PSEUDOCODE: Morale modification helper - mirrors ModifyStat but explicit for clarity in Ink
        currentStory.BindExternalFunction("ModifyMorale", (int change) =>
        {
            if (statManager != null)
            {
                int currentValue = statManager.GetStatValue("morale");
                statManager.UpdateStat("morale", currentValue + change);
                LogStory($"Modified morale by {change}");
            }
        });
        
        // PSEUDOCODE: Resource modification functions - allow Ink to change player resources using existing system
        currentStory.BindExternalFunction("ModifyResource", (string resourceName, int change) => 
        {
            if (gameUnitsLogic != null)                    // PSEUDOCODE: Check if resource manager exists
            {
                gameUnitsLogic.ChangeResourceFromName(resourceName, change, false); // PSEUDOCODE: Use existing resource change system
                LogStory($"Modified resource {resourceName} by {change}"); // PSEUDOCODE: Log resource modification
            }
        });
        
        // PSEUDOCODE: Production unit modification functions - allow Ink to add/remove buildings and units
        currentStory.BindExternalFunction("ModifyProductionUnit", (string unitName, int change) => 
        {
            if (gameUnitsLogic != null)                    // PSEUDOCODE: Check if production manager exists
            {
                gameUnitsLogic.ChangeProductionUnitFromName(unitName, change); // PSEUDOCODE: Use existing production change system
                LogStory($"Modified production unit {unitName} by {change}"); // PSEUDOCODE: Log production modification
            }
        });
        
        // PSEUDOCODE: Building functions - allow Ink to construct buildings
        currentStory.BindExternalFunction("BuildProductionUnit", (string unitName) => 
        {
            if (gameUnitsLogic != null)                    // PSEUDOCODE: Check if production manager exists
            {
                bool success = gameUnitsLogic.BuildProductionUnit(unitName); // PSEUDOCODE: Use existing build system
                LogStory($"Attempted to build {unitName}: {(success ? "Success" : "Failed")}"); // PSEUDOCODE: Log build attempt
                return success;                             // PSEUDOCODE: Return success status
            }
            return false;                                   // PSEUDOCODE: Return false if manager not found
        });
        
        // PSEUDOCODE: Technology research functions - allow Ink to start technology research
        currentStory.BindExternalFunction("TriggerTechnologyEnlightened", (string technologyName) => 
        {
            if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null) // PSEUDOCODE: Check if research system exists
            {
                // PSEUDOCODE: Find technology slot and trigger Enlightened state
                GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == technologyName);
                if (techSlotObj != null)
                {
                    GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                    if (techSlot != null)
                    {
                                        techSlot.enlightenedCompleted = true; // PSEUDOCODE: Set Enlightened state to true
                techSlot.RefreshTechnologyUI(); // PSEUDOCODE: Update UI to reflect Enlightened state
                LogStory($"Triggered Enlightenment for technology {technologyName}"); // PSEUDOCODE: Log Enlightenment trigger
                        return true;                        // PSEUDOCODE: Return true if Enlightenment triggered
                    }
                }
            }
                            return false;                                   // PSEUDOCODE: Return false if Enlightenment couldn't be triggered
        });
        
        // PSEUDOCODE: Time system functions - allow Ink to check current time values
        currentStory.BindExternalFunction("GetCurrentSeventh", () => 
        {
            TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
            if (timeSystem != null)
            {
                return timeSystem.CurrentSeventh; // PSEUDOCODE: Return current seventh (1-21)
            }
            return 1; // PSEUDOCODE: Return default if time system not found
        });
        
        currentStory.BindExternalFunction("GetCurrentPhase", () => 
        {
            TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
            if (timeSystem != null)
            {
                return timeSystem.CurrentPhase; // PSEUDOCODE: Return current phase (1-3)
            }
            return 1; // PSEUDOCODE: Return default if time system not found
        });
        
        currentStory.BindExternalFunction("GetCurrentEcho", () => 
        {
            TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
            if (timeSystem != null)
            {
                return timeSystem.CurrentEcho; // PSEUDOCODE: Return current echo (1-4)
            }
            return 1; // PSEUDOCODE: Return default if time system not found
        });
        
        currentStory.BindExternalFunction("GetCurrentCycle", () => 
        {
            TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
            if (timeSystem != null)
            {
                return timeSystem.CurrentCycle; // PSEUDOCODE: Return current cycle
            }
            return 1; // PSEUDOCODE: Return default if time system not found
        });
        
        currentStory.BindExternalFunction("IsRitualSeventh", () => 
        {
            TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
            if (timeSystem != null)
            {
                return timeSystem.CurrentSeventh == 21; // PSEUDOCODE: Return true if it's ritual seventh
            }
            return false; // PSEUDOCODE: Return false if time system not found
        });
        
        // PSEUDOCODE: Event score modification functions - allow Ink to change story progress
        currentStory.BindExternalFunction("ModifyEventScore", (string scoreName, int change) => 
        {
            if (eventSystem != null)                       // PSEUDOCODE: Check if event system exists
            {
                eventSystem.ModifyEventScore(scoreName, change); // PSEUDOCODE: Modify story progress
                LogStory($"Modified event score {scoreName} by {change}"); // PSEUDOCODE: Log progress change
            }
        });
        
        // PSEUDOCODE: Utility functions for Ink story logic
        currentStory.BindExternalFunction("Random", (int min, int max) => 
        {
            return Random.Range(min, max + 1);             // PSEUDOCODE: Generate random number between min and max
        });
        
        // PSEUDOCODE: Logging function for Ink story debugging
        currentStory.BindExternalFunction("Log", (string message) => 
        {
            LogStory($"Ink Log: {message}");               // PSEUDOCODE: Log message from Ink story
        });
        
        LogStory("External functions bound");               // PSEUDOCODE: Confirm all functions are bound
    }
    
    /// <summary>
    /// Get all available knots in the current story
    /// </summary>
    public List<string> GetAvailableKnots()
    {
        List<string> knots = new List<string>();
        
        if (currentStory != null)
        {
            // This would require accessing the story's knot list
            // For now, return empty list
            LogStory("Getting available knots (not implemented)");
        }
        
        return knots;
    }
    
    /// <summary>
    /// Check if a knot exists in the current story
    /// </summary>
    public bool KnotExists(string knotName)
    {
        if (currentStory != null)
        {
            try
            {
                currentStory.ChoosePathString(knotName);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get the current path in the story
    /// </summary>
    public string GetCurrentPath()
    {
        if (currentStory != null)
        {
            return currentStory.state.currentPathString;
        }
        return "";
    }
    
    /// <summary>
    /// Get the current story asset
    /// </summary>
    public TextAsset GetCurrentStoryAsset()
    {
        return currentStoryAsset;
    }
    
    /// <summary>
    /// Check if a story is currently loaded
    /// </summary>
    public bool HasStory()
    {
        return currentStory != null;
    }
    
    /// <summary>
    /// Unload the current story
    /// </summary>
    public void UnloadStory()
    {
        currentStory = null;
        currentStoryAsset = null;
        storyVariables.Clear();
        storyTags.Clear();
        LogStory("Story unloaded");
    }
    
    /// <summary>
    /// Log story-related messages
    /// </summary>
    private void LogStory(string message)
    {
        if (enableStoryLogging)
        {
            Debug.Log($"[InkStoryManager] {message}");
        }
    }
    
    /// <summary>
    /// Get story variables for debugging
    /// </summary>
    public Dictionary<string, object> GetStoryVariables()
    {
        return new Dictionary<string, object>(storyVariables);
    }
    
    /// <summary>
    /// Get story tags for debugging
    /// </summary>
    public Dictionary<string, object> GetStoryTags()
    {
        return new Dictionary<string, object>(storyTags);
    }
} 