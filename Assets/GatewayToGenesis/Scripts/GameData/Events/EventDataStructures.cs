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

    [Tooltip("An actionable Ink issue (# issue: true), offered in the notification feed instead of the random event queue.")]
    public bool isIssue;
    
    [Header("Story Conditions")]
    public List<EventCondition> storyConditions = new List<EventCondition>(); // Conditions to trigger this story
    
    [Header("Story Consequences")]
    public List<EventConsequence> storyConsequences = new List<EventConsequence>(); // What happens when story completes
    
    [Header("Story Cooldown")]
    public int cooldownSevenths = 0; // How many sevenths must pass before this event can trigger again
    
    [Header("Screen Flow")]
    public List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>(); // How screens progress through this story
    
    [Header("Ballad actors and ballads")]
    [Tooltip("The ballad this story is a verse of (# ballad: id); empty for a lone event.")]
    public string ballad;
    [Tooltip("Its verse number within the ballad (# verse: N); the highest verse is the ballad's finale.")]
    public int verse;
    [Tooltip("The ballad's title (# ballad_title:), shown on the event screen.")]
    public string balladTitle;
    [Tooltip("What the story is about (# theme:): a Role Archetype (Leader, Resistor, Scholar, Emotional Core, Sacrificial Lamb) or a kind of Lyrical Fragment.")]
    public string theme;
    [Tooltip("Its ballad actors (# cast: area:defense, co:1, ...; see BalladActors.ParseCast).")]
    public string cast;

    // UI metadata for controlling UI elements from Ink (runtime-only, populated from Ink parsing)
    [System.NonSerialized] public Dictionary<string, string> uiMetadata = new Dictionary<string, string>();
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
        TrueDeathsCheck,     // Check true deaths amount (cannot be revised)
        NoEventInSeventhsCheck, // Check if no events happened in X sevenths
        ValueCheck           // Any other GameValues domain, named by `domain` (building, civic, weather, ...)
    }

    public ConditionType type;
    public string targetName; // Score name, stat name, resource name, etc.
    public int requiredValue;
    public ComparisonOperator comparison;
    [Tooltip("GameValues domain read by ValueCheck conditions (e.g. building, civic, weather, event_completed).")]
    public string domain;
    
    /// <summary>GameValues domain each condition type reads (see <see cref="GameValues"/>).</summary>
    private static readonly System.Collections.Generic.Dictionary<ConditionType, string> Domains = new System.Collections.Generic.Dictionary<ConditionType, string>
    {
        { ConditionType.ScoreCheck, "score" },
        { ConditionType.StatCheck, "stat" },
        { ConditionType.ResourceCheck, "resource" },
        { ConditionType.TechnologyCheck, "technology" },
        { ConditionType.SeventhCheck, "seventh" },
        { ConditionType.PhaseCheck, "phase" },
        { ConditionType.EchoCheck, "echo" },
        { ConditionType.CycleCheck, "cycle" },
        { ConditionType.RitualSeventhCheck, "ritual_seventh" },
        { ConditionType.PopulationCheck, "population" },
        { ConditionType.HousingCheck, "housing" },
        { ConditionType.VagrantsCheck, "vagrants" },
        { ConditionType.DeathsCheck, "deaths" },
        { ConditionType.VagrantDeathsCheck, "vagrant_deaths" },
        { ConditionType.TrueDeathsCheck, "true_deaths" },
        { ConditionType.NoEventInSeventhsCheck, "no_event_in_sevenths" },
    };

    /// <summary>
    /// True when the condition holds now. Technology and ritual checks are yes/no; "no event in X sevenths"
    /// means at least X sevenths; every other type compares the whole-number value with <see cref="comparison"/>.
    /// </summary>
    public bool Evaluate()
    {
        if (!TryGetCurrentValue(out float value)) return false;
        switch (type)
        {
            case ConditionType.TechnologyCheck:
            case ConditionType.RitualSeventhCheck:
                return value >= 1f;
            case ConditionType.NoEventInSeventhsCheck:
                return value >= requiredValue;
            default:
                return GameValues.Compare(Mathf.Round(value), comparison, requiredValue);
        }
    }

    /// <summary>The domain this condition reads: the type's domain, or <see cref="domain"/> for ValueCheck.</summary>
    public string Domain => type == ConditionType.ValueCheck ? domain : DomainOf(type);

    /// <summary>The live value this condition compares (false when its system is not in the scene).</summary>
    public bool TryGetCurrentValue(out float value)
    {
        value = 0f;
        string d = Domain;
        return !string.IsNullOrEmpty(d) && GameValues.TryGet(d, targetName, out value);
    }

    /// <summary>Whether the condition is a yes/no check rather than a number comparison.</summary>
    public bool IsYesNo => type == ConditionType.TechnologyCheck || type == ConditionType.RitualSeventhCheck
        || (type == ConditionType.ValueCheck && (domain == "civic" || domain == "government" || domain == "weather" || domain == "age_reached" || domain == "capability"));

    public static string DomainOf(ConditionType type) => Domains.TryGetValue(type, out var domain) ? domain : null;
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
        DeathRecordsRevision,  // Revise death records for evil empire history manipulation
        ProductionPercentChange, // Adjust global production percentage modifier for a resource
        ProductionPercentChangeSection, // Adjust production percent for all resources in a section
        ClickPowerChange, // Static click power change for a single resource
        ClickPowerPercentChange, // Percent click power change for a single resource
        ClickPowerChangeSection, // Static click power change for all resources in a section
        ClickPowerPercentChangeSection, // Percent click power change for all resources in a section
        WeatherChange, // Change weather profile (targetName = weather profile name or "clear", value = 0 for procedural, 1 for permanent)
        RenownChange, // The older form of FragmentChange: Fragments of Meaning for targetName (a legend, a ballad actor role or "council")
        FragmentChange, // Lyrical Fragments: targetName = "Who Kind"
        LesserOpus, // targetName = "Who catalog-id"; +1; only a completed Ballad finale can award it
        SettlementDamage, // targetName = "capital", "exposed" (the most endangered settlement) or a settlement's name; value < 0 pillages (damage), > 0 repairs (WorldSystem.ApplySettlementDamage)
        AffectionTest, // targetName = "from > to | Thread"; +1 Relation Growth, -1 Relation Fracture
        CultureChange, // targetName = "myth <id>", "embrace", "decline", "leaning <Family>" (value: %), "presence" (value: %) (CultureSystem.ApplyConsequence)
        PopulationPercentChange, // Negative percentage of all residents; positive values never create migrants.
        HousingPercentChange, // Signed percentage of current housing.
        EraScoreChange // targetName = the reason shown in the Chronicle; value = points (AgeProgression.Award)
    }
    
    public ConsequenceType type;
    public string targetName; // Name of resource or section, depending on type
    public int value; // Signed amount (absolute for percents; +/- for static)
    public int durationSevenths; // 0 => permanent; >0 => expires after N sevenths
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

/// <summary>
/// One chorus choice as authored in Ink (see <see cref="EventScript.ParseChorusChoice"/>): its gating
/// requirements and costs, its pillar challenge and where each outcome leads. Instances live in
/// <see cref="EventStoryIndex"/> and are shared, so views and rules read them and never write to them.
/// </summary>
[System.Serializable]
public class ChorusChoiceData
{
    public string choiceId;    // "idealism", "realism" or "pragmatism"
    public string title;
    public string description;
    public string destinationPath;
    public string successPath;
    public string failurePath;
    public List<EventCondition> requirements = new List<EventCondition>();
    // Costs gate availability like requirements and are paid when the choice is made.
    public List<EventCondition> requirementsCost = new List<EventCondition>();
    public List<EventConsequence> consequences = new List<EventConsequence>(); // Always applied when chosen
    public List<EventConsequence> successConsequences = new List<EventConsequence>();
    public List<EventConsequence> failureConsequences = new List<EventConsequence>();

    // Pillar challenge; hasChallenge is false for choices that simply happen ("Time passes...").
    public bool hasChallenge;
    public string challengePillar;
    public int challengeStrength;

    // Extended outcomes
    public int rareEventPercent; // 0 disables
    public string rareEventPath;
    public List<EventConsequence> rareEventConsequences = new List<EventConsequence>();
    public string critSuccessPath;
    public string critFailurePath;
    public List<EventConsequence> critSuccessConsequences = new List<EventConsequence>();
    public List<EventConsequence> critFailureConsequences = new List<EventConsequence>();

    public bool HasRequirements => requirements.Count > 0 || requirementsCost.Count > 0;

    /// <summary>Every knot this choice can lead to (for validation).</summary>
    public IEnumerable<string> TargetKnots()
    {
        if (!string.IsNullOrEmpty(destinationPath)) yield return destinationPath;
        if (!string.IsNullOrEmpty(successPath)) yield return successPath;
        if (!string.IsNullOrEmpty(failurePath)) yield return failurePath;
        if (!string.IsNullOrEmpty(critSuccessPath)) yield return critSuccessPath;
        if (!string.IsNullOrEmpty(critFailurePath)) yield return critFailurePath;
        if (!string.IsNullOrEmpty(rareEventPath)) yield return rareEventPath;
    }
}
