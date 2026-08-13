using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ink.Runtime;
using System;
using DG.Tweening;

/// <summary>
/// Data structure for holding parsed choice information before creating ChorusChoice objects
/// </summary>
[System.Serializable]
public class ChorusChoiceData
{
    public string choiceId;
    public string title;
    public string description;
    public string hoverDescription;
    public string destinationPath;
    public string successPath;
    public string failurePath;
    public ChoiceValidationType validationType;
    public bool hasChallenge;
    public bool hasRequirements;
    public List<EventCondition> requirements = new List<EventCondition>();
    public List<EventConsequence> consequences = new List<EventConsequence>(); // Legacy: always apply
    public List<EventConsequence> successConsequences = new List<EventConsequence>(); // Apply only on success
    public List<EventConsequence> failureConsequences = new List<EventConsequence>(); // Apply only on failure
    public ChallengeSlot challenge;
    // Requirements
    public List<EventCondition> requirementsCost = new List<EventCondition>(); // Resources/housing/population to consume at outro
    // Extended outcome support
    public int rareEventPercent; // 0 disables
    public string rareEventPath;
    public List<EventConsequence> rareEventConsequences = new List<EventConsequence>();
    public string critSuccessPath;
    public string critFailurePath;
    public List<EventConsequence> critSuccessConsequences = new List<EventConsequence>();
    public List<EventConsequence> critFailureConsequences = new List<EventConsequence>();
    
    // Roll tracking for saving roll system
    public int naturalRoll;
    public int enhancedRoll;
    
    // Public properties for easy access
    public string ChoiceId => choiceId;
    public string Title => title;
    public string Description => description;
    public string HoverDescription => hoverDescription;
    public string DestinationPath => destinationPath;
    public string SuccessPath => successPath;
    public string FailurePath => failurePath;
    public ChoiceValidationType ValidationType => validationType;
    public bool HasChallenge => hasChallenge;
    public bool HasRequirements => hasRequirements;
    public List<EventCondition> RequirementsCost => requirementsCost;
    public List<EventCondition> Requirements => requirements;
    public List<EventConsequence> Consequences => consequences;
    public List<EventConsequence> SuccessConsequences => successConsequences;
    public List<EventConsequence> FailureConsequences => failureConsequences;
    public ChallengeSlot Challenge => challenge;
    
    // Challenge data for choices with challenges
    public string challengePillar;
    public int challengeStrength;
    public string ChallengePillar => challengePillar;
    public int ChallengeStrength => challengeStrength;
}

/// <summary>
/// Manages the Chorus Screen - the main decision point for events
/// Updated for new structure with alpha transitions and hidden background elements
/// </summary>
public class ChorusScreenManager : MonoBehaviour
{
    [Header("Screen Variants")]
    [SerializeField] private GameObject fullOutcomesVariant; // 3 choices: Idealism, Realism, Pragmatism
    [SerializeField] private GameObject twoChoicesVariant;   // 2 choices: Idealism, Realism
    
    [Header("Static Pillar References")]
    [SerializeField] private TMP_Text aureusAmountText;
    [SerializeField] private TMP_Text chorusAmountText;
    [SerializeField] private TMP_Text regaliaAmountText;
    [SerializeField] private TMP_Text waltzAmountText;
    
    [Header("Choice Containers")]
    [SerializeField] private Transform fullOutcomesChoicesContainer;
    [SerializeField] private Transform twoChoicesChoicesContainer;
    
    [Header("Static Choice References - FullOutcomes")]
    [SerializeField] private ChorusChoice fullOutcomesIdealism;
    [SerializeField] private ChorusChoice fullOutcomesRealism;
    [SerializeField] private ChorusChoice fullOutcomesPragmatism;
    
    [Header("Static Choice References - TwoChoices")]
    [SerializeField] private ChorusChoice twoChoicesIdealism;
    [SerializeField] private ChorusChoice twoChoicesRealism;
    
    [Header("Background Elements")]
    [SerializeField] private Transform fullOutcomesBackgrounds; // Contains Idealism, Realism, Pragmatism background images
    [SerializeField] private Transform twoChoicesBackgrounds;   // Contains Idealism, Realism background images
    
    [Header("Decision Token")]
    [SerializeField] private GameObject decisionToken;
    [SerializeField] private Transform tokenStartPosition;
    
    [Header("Description Display")]
    [SerializeField] private TMP_Text descriptionText; // Shows content from Chorus knot
    
    [Header("Choice Prefabs")]
    [SerializeField] private GameObject requirementSlotPrefab;
    [SerializeField] private GameObject challengeResultPrefab;
    
    [Header("Pillar Icons")]
    [SerializeField] private Sprite aureusIcon;
    [SerializeField] private Sprite regaliaIcon;
    [SerializeField] private Sprite waltzIcon;
    [SerializeField] private Sprite chorusIcon;
    
    private EventScreen currentEventScreen;
    private Story currentStory;
    private List<ChorusChoiceData> availableChoices = new List<ChorusChoiceData>();
    private List<ChorusChoiceBackground> backgroundChoices = new List<ChorusChoiceBackground>(); // Background elements
    private ChorusChoiceData selectedChoice;
    private bool isPragmatismAvailable = false;
    private bool isDragging = false;
    private bool choiceWasMade = false; // Track if a choice was made to prevent choices from fading back in
    private string overrideNextPath; // Optional next path override (e.g., rare event for non-challenge)
    
    private EventSystemLogic eventSystem;
    private InkStoryManager inkManager;
    private StatManager statManager;
    
    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemLogic>();
        inkManager = FindFirstObjectByType<InkStoryManager>();
        statManager = FindFirstObjectByType<StatManager>();
        
        // Debug logging removed for cleaner output
    }
    
    private void Start()
    {
        InitializePillarDisplay();
        SetupDecisionToken();
    }

    private void OnEnable()
    {
        if (statManager == null) statManager = FindFirstObjectByType<StatManager>();
        if (statManager != null)
        {
            statManager.OnPillarChanged += HandlePillarChanged;
        }
        // Immediate refresh in case values changed while disabled
        UpdatePillarDisplay();
        RefreshChoices();
    }

    private void OnDisable()
    {
        if (statManager != null)
        {
            statManager.OnPillarChanged -= HandlePillarChanged;
        }
    }

    private void HandlePillarChanged(string pillar, int newValue)
    {
        // Refresh top-of-screen pillar text and challenge chances to reflect runtime changes
        UpdatePillarDisplay();
        RefreshChoices();
        // Re-evaluate background availability visuals
        foreach (var bg in backgroundChoices)
        {
            if (bg != null)
            {
                bg.InitializeChoice(bg.GetChoiceId(), availableChoices.Find(c => c.ChoiceId == bg.GetChoiceId()));
            }
        }
    }

    /// <summary>
    /// Initialize the chorus screen with event data
    /// </summary>
    public void InitializeChorusScreen(EventScreen screen)
    {
        currentEventScreen = screen;
        
        // Reset choice state for new chorus screen
        choiceWasMade = false;
        
        // Try to get the story from the event system first
        if (eventSystem != null)
        {
            var volumeManager = eventSystem.GetVolumeManager();
            if (volumeManager != null)
            {
                currentStory = volumeManager.GetCurrentStory();
            }
        }
        
        // Fallback to inkManager if event system doesn't have it
        if (currentStory == null && inkManager != null)
        {
            currentStory = inkManager.GetCurrentStory();
        }
        
        if (currentStory == null)
        {
            Debug.LogWarning("[ChorusScreenManager] No Ink story available for chorus screen");
            return;
        }
        
        try
        {
            // Parse choices from the chorus knot itself
            ParseChoicesFromChorusKnot();
            
            // Select screen variant and create choices
            SelectScreenVariant();
            CreateChoices();
            
            // Update UI elements
            UpdatePillarDisplay();
            SetupDescription();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChorusScreenManager] Error during InitializeChorusScreen: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Setup the description text from the Chorus knot
    /// </summary>
    private void SetupDescription()
    {
        if (descriptionText == null || currentStory == null) return;
        
        try
        {
            // Navigate to the chorus knot and continue until choices appear;
            // use the last non-empty line before choices as the description
            currentStory.ChoosePathString(currentEventScreen.inkKnot);
            string lastNonEmpty = null;
            while (currentStory.canContinue)
            {
                string line = currentStory.Continue();
                if (!string.IsNullOrEmpty(line))
                {
                    string trimmed = line.Replace("\r\n", "\n").Trim();
                    if (!string.IsNullOrEmpty(trimmed)) lastNonEmpty = trimmed;
                }
                if (currentStory.currentChoices != null && currentStory.currentChoices.Count > 0)
                {
                    break;
                }
            }
            descriptionText.text = !string.IsNullOrEmpty(lastNonEmpty) ? lastNonEmpty : "Make your choice.";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Failed to get chorus content: {ex.Message}");
            descriptionText.text = "Make your choice.";
        }
    }
    
    /// <summary>
    /// Parse choice data directly from the chorus knot (not from separate choice knots)
    /// </summary>
    private void ParseChoicesFromChorusKnot()
    {
        availableChoices.Clear();
        backgroundChoices.Clear();
        
        if (currentStory == null || string.IsNullOrEmpty(currentEventScreen.inkKnot)) return;
        
        try
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Parsing chorus knot '{currentEventScreen.inkKnot}'", "ChorusScreenManager");
            // PRIMARY: use precompiled metadata built at startup (fast and reliable)
            var precompiled = inkManager != null
                ? inkManager.GetPrecompiledChoicesForKnot(currentEventScreen.inkKnot)
                : InkStoryManager.GetPrecompiledChoices(currentEventScreen.inkKnot);

            availableChoices.AddRange(precompiled);
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Precompiled choices found: {availableChoices.Count}", "ChorusScreenManager");

            // If for some reason precompiled is empty, do a minimal UI-only fallback using current story choices
            if (availableChoices.Count == 0 && currentStory != null)
            {
                currentStory.ChoosePathString(currentEventScreen.inkKnot);
                currentStory.ContinueMaximally();
                var inkChoices = currentStory.currentChoices;
                if (inkChoices != null)
                {
                    foreach (var inkChoice in inkChoices)
                    {
                        var cd = ParseChoiceFromInkChoice(inkChoice);
                        if (cd != null) availableChoices.Add(cd);
                    }
                }
            }

            // Removed runtime raw ink fallback/merge; rely on precompiled metadata only

            // Pragmatism availability drives which variant we show
            isPragmatismAvailable = availableChoices.Exists(c => c.ChoiceId.ToLower().Contains("pragmatism"));
            
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Using precompiled choices for '{currentEventScreen.inkKnot}': {availableChoices.Count}", "ChorusScreenManager");

            {
                foreach (var c in availableChoices)
                {
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice summary -> id={c.ChoiceId}, title='{c.Title}', hasReqs={c.HasRequirements} (count={c.Requirements?.Count ?? 0}), hasChallenge={c.HasChallenge}, validation={c.ValidationType}", "ChorusScreenManager");
                    if (c.HasRequirements && c.Requirements != null)
                    {
                        for (int i = 0; i < c.Requirements.Count; i++)
                        {
                            var r = c.Requirements[i];
                            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager]   - req[{i}] type={r.type}, target='{r.targetName}', cmp={r.comparison}, value={r.requiredValue}", "ChorusScreenManager");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ChorusScreenManager] Failed to parse chorus knot '{currentEventScreen.inkKnot}': {ex.Message}");
        }
    }

    /// <summary>
    /// Parse choices from content into a list (non-destructive helper)
    /// Expected choice format per line: "* ChoiceType. Title. Description #metadata -> path"
    /// </summary>
    private List<ChorusChoiceData> ParseChoicesFromContentToList(string content)
    {
        List<ChorusChoiceData> list = new List<ChorusChoiceData>();
        if (string.IsNullOrEmpty(content)) return list;
        string[] lines = content.Split('\n');
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("* ") && trimmedLine.Contains("->"))
            {
                var cd = ParseChoiceFromLine(trimmedLine);
                if (cd != null)
                {
                    list.Add(cd);
                }
            }
        }
        return list;
    }

    // Removed non-Resources/Events sources

    // Replaced scanning with simpler loader below

    // Removed editor-wide scan

    // Simpler knot extraction without regex
    private string ExtractKnotBlock(string fullInk, string knotName)
    {
        if (string.IsNullOrEmpty(fullInk) || string.IsNullOrEmpty(knotName)) return string.Empty;
        string text = fullInk.Replace("\r\n", "\n").Replace("\r", "\n");
        string header = "=== " + knotName + " ===";
        int start = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return string.Empty;
        start += header.Length;
        int next = text.IndexOf("\n=== ", start, StringComparison.Ordinal);
        string block = next >= 0 ? text.Substring(start, next - start) : text.Substring(start);
        return block;
    }

    // Deprecated: runtime raw ink loader removed (precompiled metadata is authoritative)
    
    /// <summary>
    /// Parse choices from the chorus knot content text
    /// </summary>
    private void ParseChoicesFromContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        
        // Split content into lines
        string[] lines = content.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            
            // Look for choice lines that start with "* " and contain "->"
            if (trimmedLine.StartsWith("* ") && trimmedLine.Contains("->"))
            {
                ChorusChoiceData choiceData = ParseChoiceFromLine(trimmedLine);
                if (choiceData != null)
                {
                    availableChoices.Add(choiceData);
                }
            }
        }
    }
    
    /// <summary>
    /// Parse a single choice from an Ink Choice object
    /// </summary>
    private ChorusChoiceData ParseChoiceFromInkChoice(Choice inkChoice)
    {
        try
        {
            // Get the choice text from the Ink choice
            string choiceText = inkChoice.text;
            
            // IMPORTANT: The Ink engine strips metadata, so we need to parse the raw choice data differently
            // The choice text we get is already cleaned (e.g., "Idealism. Embrace Hope - Welcome the Caravan")
            // But the actual challenge data is in the Ink file as metadata
            
            // Robust format with description marker:
            // "ChoiceType. Title... d Description..."
            string rawChoiceType;
            string title;
            string description;
            int dIdx = choiceText.IndexOf("&D ");
            int cIdx = choiceText.IndexOf("&C ");
            string left;
            if (dIdx >= 0)
            {
                left = choiceText.Substring(0, dIdx).Trim();
                int descEnd = (cIdx > dIdx) ? cIdx : choiceText.Length;
                description = choiceText.Substring(dIdx + 3, descEnd - (dIdx + 3)).Trim();
            }
            else
            {
                left = cIdx >= 0 ? choiceText.Substring(0, cIdx).Trim() : choiceText.Trim();
                description = "Make your choice.";
            }
            int firstDot = left.IndexOf('.');
            if (firstDot >= 0)
            {
                rawChoiceType = left.Substring(0, firstDot).Trim();
                title = left.Substring(firstDot + 1).Trim();
            }
            else
            {
                rawChoiceType = left.Trim();
                title = left.Trim();
            }
            // Determine choice type
            string choiceId = DetermineChoiceType(rawChoiceType);
            
            // Create the choice data
            ChorusChoiceData choiceData = new ChorusChoiceData();
            choiceData.choiceId = choiceId;
            choiceData.title = title;
            choiceData.description = description;
            choiceData.hoverDescription = title; // Use title as hover description for now
            choiceData.destinationPath = inkChoice.targetPath.ToString(); // Convert Ink.Path to string
            
                        // Since the Ink engine strips metadata, we need to manually add challenge data based on choice type
            // This is based on the WeepingPrincess.ink file structure
            // Note: challenge and metadata will be merged from content parsing in ParseChoicesFromChorusKnot
            
            // If no specific validation type found, set to basic choice
            if (!choiceData.hasChallenge && !choiceData.hasRequirements)
            {
                choiceData.validationType = ChoiceValidationType.None;
            }

            // Parse inline metadata after &C if present
            if (cIdx >= 0)
            {
                string metadata = choiceText.Substring(cIdx + 3).Trim();
                var metadataDict = ParseMetadata(metadata);
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] (Compiled Choice) Parsed metadata keys: {string.Join(",", metadataDict.Keys)}", "ChorusScreenManager");

                // Requirements
                if (metadataDict.ContainsKey("requirements"))
                {
                    choiceData.requirements = ParseRequirementsFromString(metadataDict["requirements"]);
                    choiceData.hasRequirements = choiceData.requirements != null && choiceData.requirements.Count > 0;
                }
                if (metadataDict.ContainsKey("requirements:cost"))
                {
                    choiceData.requirementsCost = ParseRequirementsFromString(metadataDict["requirements:cost"]);
                }

                // Outcome branches
                if (metadataDict.ContainsKey("success")) choiceData.successPath = metadataDict["success"];
                if (metadataDict.ContainsKey("failure")) choiceData.failurePath = metadataDict["failure"];
                if (metadataDict.ContainsKey("success:consequences")) choiceData.successConsequences = ParseConsequencesFromString(metadataDict["success:consequences"]);
                if (metadataDict.ContainsKey("failure:consequences")) choiceData.failureConsequences = ParseConsequencesFromString(metadataDict["failure:consequences"]);
                if (metadataDict.ContainsKey("consequences")) choiceData.consequences = ParseConsequencesFromString(metadataDict["consequences"]);

                // Extended outcomes
                if (metadataDict.ContainsKey("crit_success")) choiceData.critSuccessPath = metadataDict["crit_success"];
                if (metadataDict.ContainsKey("crit_failure")) choiceData.critFailurePath = metadataDict["crit_failure"];
                if (metadataDict.ContainsKey("crit_success:consequences")) choiceData.critSuccessConsequences = ParseConsequencesFromString(metadataDict["crit_success:consequences"]);
                if (metadataDict.ContainsKey("crit_failure:consequences")) choiceData.critFailureConsequences = ParseConsequencesFromString(metadataDict["crit_failure:consequences"]);
                if (metadataDict.ContainsKey("rare_event")) choiceData.rareEventPath = metadataDict["rare_event"];
                if (metadataDict.ContainsKey("rare_event_percent")) int.TryParse(metadataDict["rare_event_percent"], out choiceData.rareEventPercent);
                if (metadataDict.ContainsKey("rare_event:consequences")) choiceData.rareEventConsequences = ParseConsequencesFromString(metadataDict["rare_event:consequences"]);

                // Challenge
                if (metadataDict.ContainsKey("pillar"))
                {
                    choiceData.challengePillar = metadataDict["pillar"];
                    int st = 10;
                    if (metadataDict.ContainsKey("strength")) int.TryParse(metadataDict["strength"], out st);
                    if (st <= 0) st = 10;
                    choiceData.challengeStrength = st;
                    choiceData.hasChallenge = !string.IsNullOrEmpty(choiceData.challengePillar);
                }

                // Validation type
                choiceData.validationType = DetermineValidationType(choiceData);
            }

            return choiceData;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Failed to parse Ink choice '{inkChoice.text}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parse a single choice from a choice line (fallback method)
    /// </summary>
    private ChorusChoiceData ParseChoiceFromLine(string choiceLine)
    {
        // Example format: "* Idealism. Title...&D Description... #pillar:aureus;strength:15;requirements:...;success:...;failure:... -> some_path"
        
        try
        {
            // Extract the choice text and metadata
            string[] parts = choiceLine.Split(new[] { "->" }, 2, StringSplitOptions.None);
            if (parts.Length != 2) return null;
            
            string choicePart = parts[0].Trim();
            string destination = parts[1].Trim();
            
            // Remove the "* " prefix
            if (choicePart.StartsWith("* "))
            {
                choicePart = choicePart.Substring(2);
            }
            
            // Extract metadata after '&C ' marker (primary) or after '#' as fallback
            int cMarkerIndex = choicePart.IndexOf("&C ");
            string metadata = "";
            string choiceText = choicePart;
            if (cMarkerIndex >= 0)
            {
                choiceText = choicePart.Substring(0, cMarkerIndex).Trim();
                metadata = choicePart.Substring(cMarkerIndex + 3).Trim();
            }
            else
            {
                // Legacy fallback: '#metadata'
                string[] choiceAndMetadata = choicePart.Split('#', 2);
                choiceText = choiceAndMetadata[0].Trim();
                metadata = choiceAndMetadata.Length > 1 ? choiceAndMetadata[1].Trim() : string.Empty;
            }
            
            // Parse text with robust marker for description: "&D "
            string title = "";
            string description = "Make your choice.";
            int dIdx = choiceText.IndexOf("&D ");
            string left = dIdx >= 0 ? choiceText.Substring(0, dIdx).Trim() : choiceText.Trim();
            if (dIdx >= 0)
            {
                description = choiceText.Substring(dIdx + 3).Trim();
            }
            // left side: "ChoiceType. Title..."
            string rawChoiceType = left;
            int firstDot = left.IndexOf('.');
            if (firstDot >= 0)
            {
                rawChoiceType = left.Substring(0, firstDot).Trim();
                title = left.Substring(firstDot + 1).Trim();
            }
            else
            {
                title = left.Trim();
            }

            // Determine choice type from the raw type token
            string choiceId = DetermineChoiceType(rawChoiceType);
            
            // Parse metadata for requirements, pillar, strength, success/failure paths
            Dictionary<string, string> metadataDict = ParseMetadata(metadata);
            // Debug: show raw metadata string and parsed keys
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Raw metadata for '{choiceText}': {metadata}", "ChorusScreenManager");
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Parsed metadata keys: {string.Join(",", metadataDict.Keys)}", "ChorusScreenManager");
            
            // Create the choice data
            ChorusChoiceData choiceData = new ChorusChoiceData();
            choiceData.choiceId = choiceId;
            choiceData.title = title;
            choiceData.description = description;
            choiceData.hoverDescription = title; // Use title as hover description for now
            choiceData.destinationPath = destination;
            
            // Parse requirements if specified
            if (metadataDict.ContainsKey("requirements"))
            {
                choiceData.requirements = ParseRequirementsFromString(metadataDict["requirements"]);
                choiceData.hasRequirements = true;
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Requirements count for '{title}': {choiceData.requirements.Count}", "ChorusScreenManager");
            }
            else
            {
                choiceData.hasRequirements = false;
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] No requirements for '{title}'", "ChorusScreenManager");
            }

            // Parse requirement costs if specified (resources/housing/population to consume later)
            if (metadataDict.ContainsKey("requirements:cost"))
            {
                choiceData.requirementsCost = ParseRequirementsFromString(metadataDict["requirements:cost"]);
            }
            
            // Parse success and failure paths if specified
            if (metadataDict.ContainsKey("success"))
            {
                choiceData.successPath = metadataDict["success"];
            }
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Missing 'success' in metadata for '{choiceText}'", "ChorusScreenManager");
            
            if (metadataDict.ContainsKey("failure"))
            {
                choiceData.failurePath = metadataDict["failure"];
            }
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Missing 'failure' in metadata for '{choiceText}'", "ChorusScreenManager");
            
            // Extended: critical success/failure and rare event
            if (metadataDict.ContainsKey("crit_success")) choiceData.critSuccessPath = metadataDict["crit_success"];
            if (metadataDict.ContainsKey("crit_failure")) choiceData.critFailurePath = metadataDict["crit_failure"];
            if (metadataDict.ContainsKey("crit_success:consequences")) choiceData.critSuccessConsequences = ParseConsequencesFromString(metadataDict["crit_success:consequences"]);
            if (metadataDict.ContainsKey("crit_failure:consequences")) choiceData.critFailureConsequences = ParseConsequencesFromString(metadataDict["crit_failure:consequences"]);
            if (metadataDict.ContainsKey("rare_event")) choiceData.rareEventPath = metadataDict["rare_event"];
            if (metadataDict.ContainsKey("rare_event_percent")) int.TryParse(metadataDict["rare_event_percent"], out choiceData.rareEventPercent);
            if (metadataDict.ContainsKey("rare_event:consequences")) choiceData.rareEventConsequences = ParseConsequencesFromString(metadataDict["rare_event:consequences"]);
            
            // Parse conditional consequences if specified
            if (metadataDict.ContainsKey("success:consequences"))
            {
                choiceData.successConsequences = ParseConsequencesFromString(metadataDict["success:consequences"]);
            }
            
            if (metadataDict.ContainsKey("failure:consequences"))
            {
                choiceData.failureConsequences = ParseConsequencesFromString(metadataDict["failure:consequences"]);
            }
            
            // Parse legacy consequences if specified (always apply)
            if (metadataDict.ContainsKey("consequences"))
            {
                choiceData.consequences = ParseConsequencesFromString(metadataDict["consequences"]);
            }
            
            // Capture challenge data if pillar is specified
            if (metadataDict.ContainsKey("pillar"))
            {
                string pillarType = metadataDict["pillar"];
                int strength = 10;
                if (metadataDict.ContainsKey("strength"))
                {
                    int.TryParse(metadataDict["strength"], out strength);
                    if (strength <= 0) strength = 10;
                }
                choiceData.challengePillar = pillarType;
                choiceData.challengeStrength = strength;
                choiceData.hasChallenge = true;
            }
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] No 'pillar' found in metadata for '{choiceText}' (no challenge)", "ChorusScreenManager");
            
            // Determine validation type based on what's present
            choiceData.validationType = DetermineValidationType(choiceData);
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice '{title}' validation type: {choiceData.validationType}", "ChorusScreenManager");
            
            return choiceData;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Failed to parse choice line '{choiceLine}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Determine the validation type for a choice
    /// </summary>
    private ChoiceValidationType DetermineValidationType(ChorusChoiceData choiceData)
    {
        if (choiceData.hasRequirements && choiceData.hasChallenge)
        {
            return ChoiceValidationType.Both;
        }
        else if (choiceData.hasRequirements && !choiceData.hasChallenge)
        {
            return ChoiceValidationType.Requirements;
        }
        else if (!choiceData.hasRequirements && choiceData.hasChallenge)
        {
            return ChoiceValidationType.Challenge;
        }
        else
        {
            return ChoiceValidationType.None;
        }
    }
    
    /// <summary>
    /// Determine the choice type from the choice title
    /// </summary>
    private string DetermineChoiceType(string title)
    {
        string lowerTitle = title.ToLower();
        
        if (lowerTitle.Contains("idealism") || lowerTitle.Contains("idealistic"))
            return "idealism";
        else if (lowerTitle.Contains("realism") || lowerTitle.Contains("realistic"))
            return "realism";
        else if (lowerTitle.Contains("pragmatism") || lowerTitle.Contains("pragmatic"))
            return "pragmatism";
        else
            return lowerTitle.Replace(" ", "_").ToLower(); // Fallback to generic naming
    }
    
    /// <summary>
    /// Parse metadata string into key-value pairs
    /// </summary>
    private Dictionary<string, string> ParseMetadata(string metadata)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrEmpty(metadata)) return result;
        
        // Known consequence prefixes to accumulate when in a consequences group
        string[] consequencePrefixes = new[] {
            "score:", "resource:", "production:", "stat:", "population:",
            "housing:", "vagrants:", "deaths:", "death_records_revision:", "technology:"
        };

        // State for grouping items following success:consequences / failure:consequences
        string activeGroupKey = null; // "success:consequences" or "failure:consequences"
        System.Text.StringBuilder activeGroupValue = null;

        void FlushActiveGroup()
        {
            if (!string.IsNullOrEmpty(activeGroupKey) && activeGroupValue != null)
            {
                string existing = result.ContainsKey(activeGroupKey) ? result[activeGroupKey] : string.Empty;
                string merged = string.IsNullOrEmpty(existing) ? activeGroupValue.ToString().Trim() : (existing + ";" + activeGroupValue.ToString().Trim());
                result[activeGroupKey] = merged;
            }
            activeGroupKey = null;
            activeGroupValue = null;
        }

        string[] items = metadata.Split(';');
        foreach (string raw in items)
        {
            string item = raw.Trim();
            if (string.IsNullOrEmpty(item)) continue;

            int firstColon = item.IndexOf(':');
            if (firstColon <= 0)
            {
                // Not a key:value, try to accumulate into active group if it looks like a consequence
                if (!string.IsNullOrEmpty(activeGroupKey))
                {
                    foreach (var p in consequencePrefixes)
                    {
                        if (item.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (activeGroupValue.Length > 0) activeGroupValue.Append(';');
                            activeGroupValue.Append(item);
                            goto ContinueLoop;
                        }
                    }
                }
                // Otherwise skip
                continue;
            }

            string left = item.Substring(0, firstColon).Trim();
            string rest = item.Substring(firstColon + 1).Trim();

            // Enter a new consequences group
            if ((left.Equals("success", System.StringComparison.OrdinalIgnoreCase) || left.Equals("failure", System.StringComparison.OrdinalIgnoreCase))
                && rest.StartsWith("consequences:", System.StringComparison.OrdinalIgnoreCase))
            {
                // Flush any previous group
                FlushActiveGroup();

                int secondColon = rest.IndexOf(':');
                string key = left.ToLower() + ":consequences";
                string firstValue = secondColon > 0 ? rest.Substring(secondColon + 1).Trim() : string.Empty;

                activeGroupKey = key;
                activeGroupValue = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(firstValue))
                {
                    activeGroupValue.Append(firstValue);
                }
                goto ContinueLoop;
            }

            // Accumulate requirements and requirements:cost entries across multiple occurrences
            if (left.Equals("requirements", System.StringComparison.OrdinalIgnoreCase))
            {
                // Handle requirements:cost:... specially
                if (rest.StartsWith("cost:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string costValue = rest.Substring("cost:".Length).Trim();
                    string existingCost = result.ContainsKey("requirements:cost") ? result["requirements:cost"] : string.Empty;
                    string mergedCost = string.IsNullOrEmpty(existingCost) ? costValue : (existingCost + ";" + costValue);
                    result["requirements:cost"] = mergedCost;
                }
                else
                {
                    string existingReq = result.ContainsKey("requirements") ? result["requirements"] : string.Empty;
                    string mergedReq = string.IsNullOrEmpty(existingReq) ? rest : (existingReq + ";" + rest);
                    result["requirements"] = mergedReq;
                }

                // No further processing for this item
                goto ContinueLoop;
            }

            // When in a group, accumulate additional consequence-looking items
            if (!string.IsNullOrEmpty(activeGroupKey))
            {
                bool looksLikeConsequence = false;
                foreach (var p in consequencePrefixes)
                {
                    if (item.StartsWith(p, System.StringComparison.OrdinalIgnoreCase) || rest.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))
                    {
                        looksLikeConsequence = true;
                        break;
                    }
                }
                if (looksLikeConsequence)
                {
                    if (activeGroupValue.Length > 0) activeGroupValue.Append(';');
                    activeGroupValue.Append(item);
                    goto ContinueLoop;
                }
            }

            // Exiting group on other keys
            if (!string.IsNullOrEmpty(activeGroupKey))
            {
                FlushActiveGroup();
            }

            // Normal key:value assignment
            result[left] = rest;

            ContinueLoop:;
        }

        // Flush any remaining group
        FlushActiveGroup();
        
        return result;
    }
    
    /// <summary>
    /// Parse requirements from a requirements string
    /// </summary>
    private List<EventCondition> ParseRequirementsFromString(string requirementsString)
    {
        List<EventCondition> conditions = new List<EventCondition>();
        
        if (string.IsNullOrEmpty(requirementsString)) return conditions;
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Parsing requirements string: '{requirementsString}'", "ChorusScreenManager");
        
        // Split by semicolon for multiple requirements
        string[] requirements = requirementsString.Split(';');
        
        foreach (string req in requirements)
        {
            string trimmedReq = req.Trim();
            if (string.IsNullOrEmpty(trimmedReq)) continue;
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Requirement token: '{trimmedReq}'", "ChorusScreenManager");
            
            EventCondition condition = ParseRequirementString(trimmedReq);
            if (condition != null)
            {
                conditions.Add(condition);
            }
        }
        
        return conditions;
    }
    
    /// <summary>
    /// Parse a single requirement string
    /// </summary>
    private EventCondition ParseRequirementString(string requirementStr)
    {
        // Format: "type:target comparison value" or "type:target" for simple checks
        // Examples: "score:quest_progress >= 1", "technology:agriculture == 1"
        
        string[] parts = requirementStr.Split(':');
        if (parts.Length != 2) return null;
        
        string type = parts[0].Trim();
        string condition = parts[1].Trim();
        
        // Check if condition has comparison operator
        if (condition.Contains(">=") || condition.Contains("<=") || condition.Contains("==") || 
            condition.Contains("!=") || condition.Contains(">") || condition.Contains("<"))
        {
            // Parse with comparison operator
            return ParseRequirementWithComparison(type, condition);
        }
        else
        {
            // Simple check (e.g., "technology:agriculture" means check if unlocked)
            return ParseSimpleRequirement(type, condition);
        }
    }
    
    /// <summary>
    /// Parse requirement with comparison operator
    /// </summary>
    private EventCondition ParseRequirementWithComparison(string type, string condition)
    {
        // Robust parsing with regex to allow multi-word target names
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                condition,
                @"^(?<target>.+?)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<value>-?\d+)\s*$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                Debug.LogWarning($"[ChorusScreenManager] Requirement parse failed for '{condition}'");
                return null;
            }
            string targetName = match.Groups["target"].Value.Trim();
            string operatorStr = match.Groups["op"].Value.Trim();
            string valueStr = match.Groups["value"].Value.Trim();
            if (!int.TryParse(valueStr, out int value))
            {
                Debug.LogWarning($"[ChorusScreenManager] Failed to parse requirement value '{valueStr}'");
                return null;
            }
        
            EventCondition eventCondition = new EventCondition
            {
                targetName = targetName,
                requiredValue = value,
                comparison = ParseComparisonOperator(operatorStr)
            };
        
            // Set condition type based on the prefix
            eventCondition.type = GetConditionTypeFromString(type);
            return eventCondition;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Error parsing requirement '{condition}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parse simple requirement without comparison
    /// </summary>
    private EventCondition ParseSimpleRequirement(string type, string targetName)
    {
        EventCondition eventCondition = new EventCondition
        {
            targetName = targetName,
            requiredValue = 1, // Default to 1 for simple checks
            comparison = ComparisonOperator.Equals
        };
        
        // Set condition type based on the prefix
        eventCondition.type = GetConditionTypeFromString(type);
        
        return eventCondition;
    }
    
    /// <summary>
    /// Get condition type from string
    /// </summary>
    private EventCondition.ConditionType GetConditionTypeFromString(string type)
    {
        switch (type.ToLower())
        {
            case "score": return EventCondition.ConditionType.ScoreCheck;
            case "resource": return EventCondition.ConditionType.ResourceCheck;
            case "technology": return EventCondition.ConditionType.TechnologyCheck;
            case "stat": return EventCondition.ConditionType.StatCheck;
            case "seventh": return EventCondition.ConditionType.SeventhCheck;
            case "phase": return EventCondition.ConditionType.PhaseCheck;
            case "echo": return EventCondition.ConditionType.EchoCheck;
            case "cycle": return EventCondition.ConditionType.CycleCheck;
            case "ritual_seventh": return EventCondition.ConditionType.RitualSeventhCheck;
            case "population": return EventCondition.ConditionType.PopulationCheck;
            case "housing": return EventCondition.ConditionType.HousingCheck;
            case "vagrants": return EventCondition.ConditionType.VagrantsCheck;
            case "deaths": return EventCondition.ConditionType.DeathsCheck;
            case "vagrant_deaths": return EventCondition.ConditionType.VagrantDeathsCheck;
            case "true_deaths": return EventCondition.ConditionType.TrueDeathsCheck;
            default: return EventCondition.ConditionType.ScoreCheck;
        }
    }
    
    /// <summary>
    /// Parse consequences from a consequences string
    /// </summary>
    private List<EventConsequence> ParseConsequencesFromString(string consequencesString)
    {
        List<EventConsequence> consequences = new List<EventConsequence>();
        
        if (string.IsNullOrEmpty(consequencesString)) return consequences;
        
        // Split by semicolon for multiple consequences
        string[] items = consequencesString.Split(';');
        
        foreach (string item in items)
        {
            string trimmedItem = item.Trim();
            if (string.IsNullOrEmpty(trimmedItem)) continue;
            
            EventConsequence consequence = ParseConsequenceString(trimmedItem);
            if (consequence != null)
            {
                consequences.Add(consequence);
            }
        }
        
        return consequences;
    }
    
    /// <summary>
    /// Parse a single consequence string
    /// </summary>
    private EventConsequence ParseConsequenceString(string consequenceStr)
    {
        // Format: "type:target value" or "type:target"
        // Examples: "resource:aethelight +5", "population:-10", "score:quest_progress +1"
        
        string[] parts = consequenceStr.Split(':');
        if (parts.Length != 2) return null;
        
        string type = parts[0].Trim();
        string consequence = parts[1].Trim();
        
        EventConsequence eventConsequence = new EventConsequence();
        
        // Parse the consequence part
        if (consequence.Contains("enlightened"))
        {
            eventConsequence.type = EventConsequence.ConsequenceType.TechnologyEnlightened;
            eventConsequence.targetName = consequence.Replace("enlightened", "").Trim();
            eventConsequence.value = 0;
        }
        else
        {
            // Parse numeric consequences like "aethelight +5" or "population -10"
            int lastSpaceIndex = consequence.LastIndexOf(' ');
            if (lastSpaceIndex > 0)
            {
                string targetName = consequence.Substring(0, lastSpaceIndex).Trim();
                string valueStr = consequence.Substring(lastSpaceIndex + 1).Trim();
                
                if (int.TryParse(valueStr, out int value))
                {
                    eventConsequence.targetName = targetName;
                    eventConsequence.value = value;
                    
                    // Set the consequence type based on the prefix
                    eventConsequence.type = GetConsequenceTypeFromString(type);
                }
                else
                {
                    Debug.LogWarning($"[ChorusScreenManager] Failed to parse consequence value '{valueStr}' from '{consequenceStr}'");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"[ChorusScreenManager] Invalid consequence format (no space found): '{consequenceStr}'");
                return null;
            }
        }
        
        return eventConsequence;
    }
    
    /// <summary>
    /// Get consequence type from string
    /// </summary>
    private EventConsequence.ConsequenceType GetConsequenceTypeFromString(string type)
    {
        switch (type.ToLower())
        {
            case "score": return EventConsequence.ConsequenceType.ScoreChange;
            case "resource": return EventConsequence.ConsequenceType.ResourceChange;
            case "production": return EventConsequence.ConsequenceType.ProductionUnitChange;
            case "stat": return EventConsequence.ConsequenceType.StatChange;
            case "population": return EventConsequence.ConsequenceType.PopulationChange;
            case "housing": return EventConsequence.ConsequenceType.HousingChange;
            case "vagrants": return EventConsequence.ConsequenceType.VagrantsChange;
            case "deaths": return EventConsequence.ConsequenceType.DeathsChange;
            case "death_records_revision": return EventConsequence.ConsequenceType.DeathRecordsRevision;
            case "technology": return EventConsequence.ConsequenceType.TechnologyEnlightened;
            case "weather": return EventConsequence.ConsequenceType.WeatherChange;
            default: return EventConsequence.ConsequenceType.ScoreChange;
        }
    }
    
    /// <summary>
    /// Parse comparison operator string
    /// </summary>
    private ComparisonOperator ParseComparisonOperator(string operatorStr)
    {
        switch (operatorStr)
        {
            case "==": return ComparisonOperator.Equals;
            case "!=": return ComparisonOperator.NotEquals;
            case ">=": return ComparisonOperator.GreaterThanOrEqual;
            case "<=": return ComparisonOperator.LessThanOrEqual;
            case ">": return ComparisonOperator.GreaterThan;
            case "<": return ComparisonOperator.LessThan;
            default: return ComparisonOperator.Equals;
        }
    }
    
    /// <summary>
    /// Create a challenge slot for a choice
    /// </summary>
    private ChallengeSlot CreateChallengeSlot(string pillarType, int requiredStrength)
    {
        // Since ChallengeSlot is now assigned directly on ChorusChoice,
        // we just return null and let the existing component handle initialization
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] ChallengeSlot should be assigned directly on ChorusChoice for {pillarType}", "ChorusScreenManager");
        return null;
    }
    
    /// <summary>
    /// Get the icon for a pillar type
    /// </summary>
    private Sprite GetPillarIcon(string pillarType)
    {
        switch (pillarType.ToLower())
        {
            case "aureus": return aureusIcon;
            case "regalia": return regaliaIcon;
            case "waltz": return waltzIcon;
            case "chorus": return chorusIcon;
            default: return null;
        }
    }
    
    /// <summary>
    /// Get the choice type name from choice ID for background element matching
    /// </summary>
    private string GetChoiceTypeFromId(string choiceId)
    {
        if (string.IsNullOrEmpty(choiceId)) return null;
        
        string lowerId = choiceId.ToLower();
        
        if (lowerId.Contains("idealism") || lowerId.Contains("idealistic"))
            return "Idealism";
        else if (lowerId.Contains("realism") || lowerId.Contains("realistic"))
            return "Realism";
        else if (lowerId.Contains("pragmatism") || lowerId.Contains("pragmatic"))
            return "Pragmatism";
        else
            return choiceId; // Return original if no match
    }
    
    /// <summary>
    /// Select which screen variant to show based on available choices
    /// </summary>
    private void SelectScreenVariant()
    {
        if (fullOutcomesVariant != null)
        {
            fullOutcomesVariant.SetActive(isPragmatismAvailable);
        }
        
        if (twoChoicesVariant != null)
        {
            twoChoicesVariant.SetActive(!isPragmatismAvailable);
        }
        
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Selected {(isPragmatismAvailable ? "FullOutcomes" : "TwoChoices")} variant", "ChorusScreenManager");
    }
    
    /// <summary>
    /// Create the choice UI elements
    /// </summary>
    private void CreateChoices()
    {
        Transform choicesContainer = isPragmatismAvailable ? fullOutcomesChoicesContainer : twoChoicesChoicesContainer;
        Transform backgroundsContainer = isPragmatismAvailable ? fullOutcomesBackgrounds : twoChoicesBackgrounds;
        
        if (choicesContainer == null) return;
        
        // Clear existing background choices
        backgroundChoices.Clear();
        
        // Update static choice objects with new data
        if (isPragmatismAvailable)
        {
            // FullOutcomes variant - 3 choices
            UpdateChoiceObject(fullOutcomesIdealism, availableChoices.Find(c => c.ChoiceId.ToLower().Contains("idealism")));
            UpdateChoiceObject(fullOutcomesRealism, availableChoices.Find(c => c.ChoiceId.ToLower().Contains("realism")));
            UpdateChoiceObject(fullOutcomesPragmatism, availableChoices.Find(c => c.ChoiceId.ToLower().Contains("pragmatism")));
            
            // Show FullOutcomes choices, hide TwoChoices
            if (fullOutcomesChoicesContainer != null) fullOutcomesChoicesContainer.gameObject.SetActive(true);
            if (twoChoicesChoicesContainer != null) twoChoicesChoicesContainer.gameObject.SetActive(false);
        }
        else
        {
            // TwoChoices variant - 2 choices
            UpdateChoiceObject(twoChoicesIdealism, availableChoices.Find(c => c.ChoiceId.ToLower().Contains("idealism")));
            UpdateChoiceObject(twoChoicesRealism, availableChoices.Find(c => c.ChoiceId.ToLower().Contains("realism")));
            
            // Show TwoChoices choices, hide FullOutcomes
            if (twoChoicesChoicesContainer != null) twoChoicesChoicesContainer.gameObject.SetActive(true);
            if (fullOutcomesChoicesContainer != null) fullOutcomesChoicesContainer.gameObject.SetActive(false);
        }
        
        // Find corresponding background elements
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Looking for background elements in container: {(backgroundsContainer != null ? backgroundsContainer.name : "NULL")}", "ChorusScreenManager");
        
        // Debug: List all children in backgrounds container
        if (backgroundsContainer != null)
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Background container children:", "ChorusScreenManager");
            for (int i = 0; i < backgroundsContainer.childCount; i++)
            {
                Transform child = backgroundsContainer.GetChild(i);
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager]   - {child.name}", "ChorusScreenManager");
                
                // Check if this child has ChorusChoiceBackground component
                ChorusChoiceBackground existingComponent = child.GetComponent<ChorusChoiceBackground>();
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager]   - {child.name} has ChorusChoiceBackground: {(existingComponent != null ? "YES" : "NO")}", "ChorusScreenManager");
                
                // Check if this child has Image component
                Image existingImage = child.GetComponent<Image>();
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager]   - {child.name} has Image: {(existingImage != null ? "YES" : "NO")}", "ChorusScreenManager");
            }
        }
        
        foreach (var choiceData in availableChoices)
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Processing choice: {choiceData.ChoiceId}", "ChorusScreenManager");
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice data - Title: '{choiceData.Title}', HasChallenge: {choiceData.HasChallenge}, ValidationType: {choiceData.ValidationType}", "ChorusScreenManager");
            
            // Try to find background element by choice ID first
            Transform backgroundElement = backgroundsContainer?.Find(choiceData.ChoiceId);
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Direct search for '{choiceData.ChoiceId}': {(backgroundElement != null ? "Found" : "Not found")}", "ChorusScreenManager");
            
            // If not found, try to find by matching the choice type in the name
            if (backgroundElement == null)
            {
                string choiceType = GetChoiceTypeFromId(choiceData.ChoiceId);
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Trying to find by choice type: '{choiceType}'", "ChorusScreenManager");
                if (!string.IsNullOrEmpty(choiceType))
                {
                    backgroundElement = backgroundsContainer?.Find(choiceType);
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Search by type '{choiceType}': {(backgroundElement != null ? "Found" : "Not found")}", "ChorusScreenManager");
                }
            }
            
            if (backgroundElement != null)
            {
                ChorusChoiceBackground backgroundChoice = backgroundElement.GetComponent<ChorusChoiceBackground>();
                if (backgroundChoice != null)
                {
                    // Initialize the background with choice data
                    backgroundChoice.InitializeChoice(choiceData.ChoiceId, choiceData);
                    backgroundChoices.Add(backgroundChoice);
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Found and initialized background element for {choiceData.ChoiceId}", "ChorusScreenManager");
                }
                else
                {
                    Debug.LogWarning($"[ChorusScreenManager] Background element {backgroundElement.name} missing ChorusChoiceBackground component! Please add it manually in the inspector.");
                }
            }
            else
            {
                Debug.LogWarning($"[ChorusScreenManager] Could not find background element for choice {choiceData.ChoiceId}");
            }
        }
    }
    
    /// <summary>
    /// Update a static choice object with new data
    /// </summary>
    private void UpdateChoiceObject(ChorusChoice choiceObject, ChorusChoiceData choiceData)
    {
        if (choiceObject == null || choiceData == null) return;
        
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Updating choice object {choiceObject.name} with data: {choiceData.ChoiceId}", "ChorusScreenManager");
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice validation type: {choiceData.ValidationType}, has challenge: {choiceData.HasChallenge}", "ChorusScreenManager");
        
        // Combine gating requirements with cost-display requirements to show all icons in UI
        List<EventCondition> combinedRequirements = new List<EventCondition>();
        if (choiceData.Requirements != null) combinedRequirements.AddRange(choiceData.Requirements);
        if (choiceData.RequirementsCost != null) combinedRequirements.AddRange(choiceData.RequirementsCost);

        choiceObject.InitializeChoice(
            choiceData.ChoiceId,
            choiceData.Title,
            choiceData.Description,
            choiceData.HoverDescription,
            combinedRequirements,
            null,
            choiceData.DestinationPath,
            availableChoices.IndexOf(choiceData), // Set the choice index
            choiceData.SuccessPath,
            choiceData.FailurePath,
            choiceData.ValidationType,
            choiceData.SuccessConsequences,
            choiceData.FailureConsequences,
            choiceData.Consequences
        );

        // Configure challenge prefab on the choice
        if (!string.IsNullOrEmpty(choiceData.ChallengePillar) && choiceData.ChallengeStrength > 0)
        {
            var icon = GetPillarIcon(choiceData.ChallengePillar);
            choiceObject.ConfigureChallenge(choiceData.ChallengePillar, choiceData.ChallengeStrength, icon);
        }
        else
        {
            choiceObject.ConfigureChallenge(null, 0, null);
        }

        // Apply availability visuals on the matching background after choice data is bound
        var bg = backgroundChoices.Find(b => b != null && string.Equals(b.GetChoiceId(), choiceData.ChoiceId, System.StringComparison.OrdinalIgnoreCase));
        if (bg != null)
        {
            // Re-initialize to recalc availability and alpha states
            bg.InitializeChoice(choiceData.ChoiceId, choiceData);
        }
        
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice object {choiceObject.name} updated successfully", "ChorusScreenManager");
    }
    
    /// <summary>
    /// Initialize the civilization pillar displays
    /// </summary>
    private void InitializePillarDisplay()
    {
        UpdatePillarDisplay();
        // Periodically poll as an additional safety net for UI drift (e.g., if no events fire)
        CancelInvoke(nameof(UpdatePillarDisplay));
        InvokeRepeating(nameof(UpdatePillarDisplay), 0.5f, 0.5f);
    }
    
    /// <summary>
    /// Update the civilization pillar displays
    /// </summary>
    private void UpdatePillarDisplay()
    {
        if (statManager == null)
        {
            Debug.LogWarning("[ChorusScreenManager] StatManager is null in UpdatePillarDisplay");
            return;
        }
        
        // Update static pillar displays
        if (aureusAmountText != null)
        {
            try
            {
                aureusAmountText.text = statManager.GetPillarValue("aureus").ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChorusScreenManager] Error updating aureus display: {ex.Message}");
                aureusAmountText.text = "0";
            }
        }
        
        if (chorusAmountText != null)
        {
            try
            {
                chorusAmountText.text = statManager.GetPillarValue("chorus").ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChorusScreenManager] Error updating chorus display: {ex.Message}");
                chorusAmountText.text = "0";
            }
        }
        
        if (regaliaAmountText != null)
        {
            try
            {
                regaliaAmountText.text = statManager.GetPillarValue("regalia").ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChorusScreenManager] Error updating regalia display: {ex.Message}");
                regaliaAmountText.text = "0";
            }
        }
        
        if (waltzAmountText != null)
        {
            try
            {
                waltzAmountText.text = statManager.GetPillarValue("waltz").ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChorusScreenManager] Error updating waltz display: {ex.Message}");
                waltzAmountText.text = "0";
            }
        }
        
        // Update challenge slots
        foreach (var choice in availableChoices)
        {
            if (choice.Challenge != null)
            {
                // Ensure pillar icon uses central icon mapping
                var icon = GetPillarIcon(choice.ChallengePillar ?? choice.challengePillar);
                if (icon != null)
                {
                    choice.Challenge.SetPillarIcon(icon);
                }
                choice.Challenge.UpdateChallengeDisplay();
            }
        }
    }
    
    /// <summary>
    /// Setup the decision token
    /// </summary>
    private void SetupDecisionToken()
    {
        if (decisionToken != null && tokenStartPosition != null)
        {
            decisionToken.transform.position = tokenStartPosition.position;
        }
    }
    
    /// <summary>
    /// Called when token drag starts
    /// </summary>
    public void OnTokenDragStarted()
    {
        isDragging = true;
        
        // Fade out only AVAILABLE choices, keep unavailable ones visible with lock overlay
        Transform choicesContainer = isPragmatismAvailable ? fullOutcomesChoicesContainer : twoChoicesChoicesContainer;
        if (choicesContainer != null)
        {
            // Build availability map from backgrounds
            var idToAvailable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var bg in backgroundChoices)
            {
                if (bg == null) continue;
                idToAvailable[bg.GetChoiceId() ?? string.Empty] = bg.IsAvailable();
            }
            var choices = choicesContainer.GetComponentsInChildren<ChorusChoice>(true);
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                string id = choice.ChoiceId ?? string.Empty;
                bool available;
                if (!idToAvailable.TryGetValue(id, out available)) available = true; // default to available if unknown
                var cg = choice.GetComponent<CanvasGroup>();
                if (cg == null) cg = choice.gameObject.AddComponent<CanvasGroup>();
                DOTween.Kill(cg);
                if (available)
                {
                    cg.DOFade(0f, 0.25f).SetEase(Ease.OutQuad).SetLink(choice.gameObject);
                }
                else
                {
                    cg.alpha = 1f; // keep locked choices fully visible
                }
            }
        }
        
        // Dim only available backgrounds (locked backgrounds remain static per their availability visuals)
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice != null && backgroundChoice.IsAvailable())
            {
                backgroundChoice.ApplyDragBaseAlpha(0.15f);
            }
        }
    }
    
    /// <summary>
    /// Called when token drag ends
    /// </summary>
    public void OnTokenDragEnded()
    {
        isDragging = false;
        
        // Restore AVAILABLE choices if no choice was made; keep locked ones unchanged
        if (!choiceWasMade)
        {
            Transform choicesContainer = isPragmatismAvailable ? fullOutcomesChoicesContainer : twoChoicesChoicesContainer;
            if (choicesContainer != null)
            {
                // Build availability map from backgrounds
                var idToAvailable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var bg in backgroundChoices)
                {
                    if (bg == null) continue;
                    idToAvailable[bg.GetChoiceId() ?? string.Empty] = bg.IsAvailable();
                }
                var choices = choicesContainer.GetComponentsInChildren<ChorusChoice>(true);
                foreach (var choice in choices)
                {
                    if (choice == null) continue;
                    string id = choice.ChoiceId ?? string.Empty;
                    bool available;
                    if (!idToAvailable.TryGetValue(id, out available)) available = true;
                    var cg = choice.GetComponent<CanvasGroup>();
                    if (cg == null) cg = choice.gameObject.AddComponent<CanvasGroup>();
                    DOTween.Kill(cg);
                    if (available)
                    {
                        cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).SetLink(choice.gameObject);
                    }
                    else
                    {
                        cg.alpha = 1f;
                    }
                }
            }
        }
        else
        {
            // Choice was made: leave choices as-is (screen will transition)
        }
        
        // Restore base alpha for available backgrounds
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice != null && backgroundChoice.IsAvailable())
            {
                backgroundChoice.RestoreOriginalBaseAlpha();
            }
        }
        
        // Reset all background alphas to base
        ResetBackgroundAlphas();
    }
    
    /// <summary>
    /// Update background alpha based on token distance from center
    /// </summary>
    public void UpdateBackgroundAlphaByTokenDistance(float distanceFromCenter, float maxDistance)
    {
        if (!isDragging) return;
        
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice != null && backgroundChoice.IsAvailable())
            {
                backgroundChoice.UpdateAlphaByTokenDistance(distanceFromCenter, maxDistance);
            }
        }
    }

    /// <summary>
    /// Highlight a single background as the current hover target. Others reset to base alpha.
    /// </summary>
    public void UpdateBackgroundHover(ChorusChoiceBackground activeBackground, float distanceFromCenter, float maxDistance)
    {
        if (!isDragging) return;
        
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice == null) continue;
            if (backgroundChoice == activeBackground)
            {
                if (backgroundChoice.IsAvailable())
                {
                    backgroundChoice.UpdateAlphaByTokenDistance(distanceFromCenter, maxDistance);
                }
            }
            else
            {
                if (backgroundChoice.IsAvailable())
                {
                    backgroundChoice.ResetToBaseAlpha();
                }
            }
        }
    }

    /// <summary>
    /// Reset all background elements to base alpha.
    /// </summary>
    public void ResetAllBackgroundsToBase()
    {
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice != null)
            {
                backgroundChoice.ResetToBaseAlpha();
            }
        }
    }
    
    /// <summary>
    /// Reset all background alphas to base value
    /// </summary>
    private void ResetBackgroundAlphas()
    {
        foreach (var backgroundChoice in backgroundChoices)
        {
            if (backgroundChoice != null)
            {
                backgroundChoice.ResetToBaseAlpha();
            }
        }
    }
    
    /// <summary>
    /// Handle choice selection from UI
    /// </summary>
    public void OnChoiceSelected(ChorusChoice choice)
    {
        if (choice == null) return;
        
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] OnChoiceSelected called for choice: {choice.ChoiceId}", "ChorusScreenManager");
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice validation type: {choice.ValidationType}, has challenge: {choice.HasChallenge}", "ChorusScreenManager");
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice destination path: {choice.DestinationPath}", "ChorusScreenManager");
        
        // Find the corresponding choice data
        ChorusChoiceData choiceData = availableChoices.Find(c => c.ChoiceId == choice.ChoiceId);
        if (choiceData == null) 
        {
            Debug.LogWarning($"[ChorusScreenManager] No choice data found for choice: {choice.ChoiceId}");
            return;
        }
        
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Found choice data, resolving choice: {choice.ChoiceId} with validation type: {choice.ValidationType}", "ChorusScreenManager");
        
        // Before handling navigation/roll, accumulate requirement costs as consequences for Outro application
        // Costs do not gate availability; they are consumed at the end
        if (choiceData != null && choiceData.RequirementsCost != null && choiceData.RequirementsCost.Count > 0)
        {
            foreach (var cost in choiceData.RequirementsCost)
            {
                // Only certain types are consumable
                switch (cost.type)
                {
                    case EventCondition.ConditionType.ResourceCheck:
                    {
                        var ec = new EventConsequence { type = EventConsequence.ConsequenceType.ResourceChange, targetName = cost.targetName, value = -Mathf.Abs(cost.requiredValue) };
                        EventSystemLogic.Instance.AddConsequence(ec);
                        break;
                    }
                    case EventCondition.ConditionType.PopulationCheck:
                    {
                        var ec = new EventConsequence { type = EventConsequence.ConsequenceType.PopulationChange, targetName = "population", value = -Mathf.Abs(cost.requiredValue) };
                        EventSystemLogic.Instance.AddConsequence(ec);
                        break;
                    }
                    case EventCondition.ConditionType.HousingCheck:
                    {
                        var ec = new EventConsequence { type = EventConsequence.ConsequenceType.HousingChange, targetName = "housing", value = -Mathf.Abs(cost.requiredValue) };
                        EventSystemLogic.Instance.AddConsequence(ec);
                        break;
                    }
                    default:
                        break; // other types are not consumable
                }
            }
        }

        // Handle different validation types
        switch (choice.ValidationType)
        {
            case ChoiceValidationType.None:
                // Non-challenge: show confirmation card (Time passes...) or Rare Event
                {
                    var cd = availableChoices.Find(c => c.ChoiceId == choice.ChoiceId);
                    int naturalRoll = UnityEngine.Random.Range(1, 101);
                    int enhancedRoll = GetEnhancedRoll(naturalRoll);
                    
                    // Store roll information for result display
                    cd.naturalRoll = naturalRoll;
                    cd.enhancedRoll = enhancedRoll;
                    
                    if (cd != null && cd.rareEventPercent > 0 && !string.IsNullOrEmpty(cd.rareEventPath))
                    {
                        int rareThreshold = Mathf.Clamp(100 - cd.rareEventPercent, 1, 100);
                        bool isRare = enhancedRoll >= rareThreshold;
                        bool savedByRoll = DidSavingRollMakeDifference(naturalRoll, enhancedRoll, rareThreshold, "rare_event");
                        if (isRare)
                        {
                            overrideNextPath = cd.rareEventPath;
                            ShowChallengeResult(choice, true, savedByRoll, "rare_event");
                        }
                        else
                        {
                            overrideNextPath = choice.DestinationPath;
                            ShowChallengeResult(choice, true, false, "time_passes");
                        }
                    }
                    else
                    {
                        overrideNextPath = choice.DestinationPath;
                        ShowChallengeResult(choice, true, false, "time_passes");
                    }
                }
                return;
                
            case ChoiceValidationType.Requirements:
                // Requirements-only: treat like non-challenge, show confirmation card and continue
                {
                    var cd = availableChoices.Find(c => c.ChoiceId == choice.ChoiceId);
                    int naturalRoll = UnityEngine.Random.Range(1, 101);
                    int enhancedRoll = GetEnhancedRoll(naturalRoll);
                    
                    // Store roll information for result display
                    cd.naturalRoll = naturalRoll;
                    cd.enhancedRoll = enhancedRoll;
                    
                    if (cd != null && cd.rareEventPercent > 0 && !string.IsNullOrEmpty(cd.rareEventPath))
                    {
                        int rareThreshold = Mathf.Clamp(100 - cd.rareEventPercent, 1, 100);
                        bool isRare = enhancedRoll >= rareThreshold;
                        bool savedByRoll = DidSavingRollMakeDifference(naturalRoll, enhancedRoll, rareThreshold, "rare_event");
                        if (isRare)
                        {
                            overrideNextPath = cd.rareEventPath;
                            ShowChallengeResult(choice, true, savedByRoll, "rare_event");
                        }
                        else
                        {
                            overrideNextPath = choice.DestinationPath;
                            ShowChallengeResult(choice, true, false, "time_passes");
                        }
                    }
                    else
                    {
                        overrideNextPath = choice.DestinationPath;
                        ShowChallengeResult(choice, true, false, "time_passes");
                    }
                }
                return;
                
            case ChoiceValidationType.Challenge:
                // Challenge-only choice - resolve challenge and branch
                ResolveChallengeChoice(choice);
                break;
                
            case ChoiceValidationType.Both:
                // Both requirements and challenge - requirements already validated, resolve challenge
                ResolveChallengeChoice(choice);
                break;
        }
        
        // For challenge choices, the result flow will handle applying consequences and navigation later
    }
    
    /// <summary>
    /// Resolve a choice that involves a challenge roll
    /// </summary>
    private void ResolveChallengeChoice(ChorusChoice choice)
    {
        // Find the corresponding choice data to get challenge information
        ChorusChoiceData choiceData = availableChoices.Find(c => c.ChoiceId == choice.ChoiceId);
        if (choiceData == null || !choiceData.HasChallenge)
        {
            Debug.LogWarning($"[ChorusScreenManager] Choice {choice.ChoiceId} has challenge type but no challenge data");
            NavigateToPath(choice.DestinationPath);
            return;
        }
        
        // Single roll governs everything - get both natural and enhanced versions
        int naturalRoll = UnityEngine.Random.Range(1, 101);
        int enhancedRoll = GetEnhancedRoll(naturalRoll);
        
        // Store roll information for result display
        choiceData.naturalRoll = naturalRoll;
        choiceData.enhancedRoll = enhancedRoll;
        
        // If rare event is defined, it exclusively takes precedence and disables criticals by design
        if (choiceData.rareEventPercent > 0 && !string.IsNullOrEmpty(choiceData.rareEventPath))
        {
            int rareThreshold = Mathf.Clamp(100 - choiceData.rareEventPercent, 1, 100); // top X% triggers
            if (enhancedRoll >= rareThreshold)
            {
                bool rareSavedByRoll = DidSavingRollMakeDifference(naturalRoll, enhancedRoll, rareThreshold, "rare_event");
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Rare event triggered for {choice.ChoiceId}: enhancedRoll={enhancedRoll} >= {rareThreshold} (top {choiceData.rareEventPercent}%)", "ChorusScreenManager");
                ApplyConsequences(choiceData.rareEventConsequences);
                overrideNextPath = choiceData.rareEventPath;
                ShowChallengeResult(choice, true, rareSavedByRoll, "rare_event");
                return;
            }
        }

        // Resolve success baseline using enhanced roll: success if enhancedRoll > requiredRoll
        int currentStrength = statManager != null ? statManager.GetPillarValue(choiceData.ChallengePillar) : 0;
        int successPercent = Mathf.Clamp(Mathf.RoundToInt((currentStrength / (float)Mathf.Max(1, choiceData.ChallengeStrength)) * 100f), 0, 100);
        int requiredRoll = Mathf.Clamp(100 - successPercent, 0, 100);
        bool successBaseline = enhancedRoll > requiredRoll;
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Baseline check: {choiceData.ChallengePillar} current={currentStrength} required={choiceData.ChallengeStrength} success%={successPercent} requiredRoll>{requiredRoll} enhancedRoll={enhancedRoll} -> {(successBaseline ? "Success" : "Failure")} (success if enhancedRoll > requiredRoll)", "ChorusScreenManager");

        // Critical tiers: bottom 10 => critical failure if failure; top 10 => critical success if success
        bool isCritFail = (enhancedRoll <= 10) && !successBaseline && !string.IsNullOrEmpty(choiceData.critFailurePath);
        bool isCritSuccess = (enhancedRoll >= 91) && successBaseline && !string.IsNullOrEmpty(choiceData.critSuccessPath);

        if (isCritSuccess)
        {
                            bool critTriggeredByRoll = DidSavingRollMakeDifference(naturalRoll, enhancedRoll, 90, "critical_success");
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] CRITICAL SUCCESS! enhancedRoll={enhancedRoll}", "ChorusScreenManager");
                ApplyConsequences(choiceData.critSuccessConsequences);
                overrideNextPath = choiceData.critSuccessPath;
                ShowChallengeResult(choice, true, critTriggeredByRoll, "critical_success");
                return;
        }
        if (isCritFail)
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] CRITICAL FAILURE! enhancedRoll={enhancedRoll}", "ChorusScreenManager");
            ApplyConsequences(choiceData.critFailureConsequences);
            overrideNextPath = choiceData.critFailurePath;
            ShowChallengeResult(choice, false, false, "critical_failure");
            return;
        }

        // Normal success/failure
        bool normalSavedByRoll = successBaseline && DidSavingRollMakeDifference(naturalRoll, enhancedRoll, requiredRoll, "success");
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Challenge result for {choice.ChoiceId}: {(successBaseline ? "Success" : "Failure")} (enhancedRoll={enhancedRoll})", "ChorusScreenManager");
        ShowChallengeResult(choice, successBaseline, normalSavedByRoll, successBaseline ? "success" : "failure");
    }

    private void ApplyConsequences(List<EventConsequence> list)
    {
        if (list == null || list.Count == 0) return;
        foreach (var c in list)
        {
            eventSystem?.AddConsequence(c);
        }
    }
    
    /// <summary>
    /// Resolve a simple challenge using pillar strength
    /// </summary>
    private bool ResolveSimpleChallenge(string pillarType, int requiredStrength)
    {
        if (statManager == null) return false;
        
        try
        {
            int currentStrength = statManager.GetPillarValue(pillarType);
            int successPercent = Mathf.Clamp(Mathf.RoundToInt((currentStrength / (float)Mathf.Max(1, requiredStrength)) * 100f), 0, 100);
            int naturalRoll = UnityEngine.Random.Range(1, 101); // 1..100
            int enhancedRoll = GetEnhancedRoll(naturalRoll);
            // New rule: numbers ABOVE the threshold succeed, numbers BELOW OR EQUAL fail
            bool success = enhancedRoll > successPercent;
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Challenge roll: {pillarType} pillar. Current: {currentStrength}, Required: {requiredStrength}, Success%: {successPercent}, EnhancedRoll: {enhancedRoll} -> {(success ? "Success" : "Failure")} (success if enhancedRoll > Success%)", "ChorusScreenManager");
            return success;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Error resolving challenge for {pillarType}: {ex.Message}");
            return false; // Default to failure on error
        }
    }
    
    /// <summary>
    /// Show the challenge result prefab and handle the result flow
    /// </summary>
    private void ShowChallengeResult(ChorusChoice choice, bool success, bool savedByRoll = false, string outcomeType = "")
    {
        if (challengeResultPrefab == null || choice == null) return;
        
        // Instantiate the result prefab at the choice position but parent to the screen manager
        GameObject resultObj = Instantiate(challengeResultPrefab, choice.transform.position, Quaternion.identity, transform);
        
        // Get the CanvasGroup for fade animations
        CanvasGroup canvasGroup = resultObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = resultObj.AddComponent<CanvasGroup>();
        }
        
        // Set initial alpha to 0
        canvasGroup.alpha = 0f;
        
        // Set the result text and color
        TMP_Text resultText = resultObj.transform.Find("Result")?.GetComponent<TMP_Text>();
        TMP_Text rollChanceText = resultObj.transform.Find("RollChance")?.GetComponent<TMP_Text>();
        
        if (resultText != null)
        {
            // Use the outcomeType parameter to determine the main result text
            switch (outcomeType.ToLower())
            {
                case "rare_event":
                    resultText.text = "RARE EVENT!";
                    resultText.color = new Color(1f, 0.84f, 0f); // Golden yellow
                    break;
                case "critical_success":
                    resultText.text = "CRITICAL SUCCESS!";
                    resultText.color = Color.green; // Green for critical success
                    break;
                case "critical_failure":
                    resultText.text = "CRITICAL FAILURE!";
                    resultText.color = new Color(0.6f, 0f, 0f); // Deep red
                    break;
                case "success":
                    resultText.text = "SUCCESS!";
                    resultText.color = Color.green;
                    break;
                case "failure":
                    resultText.text = "FAILURE...";
                    resultText.color = Color.red;
                    break;
                case "time_passes":
                    resultText.text = "TIME PASSES...";
                    resultText.color = Color.gray;
                    break;
                default:
                    // Fallback to old logic for backward compatibility
                    var cd = availableChoices.Find(c => c.ChoiceId == choice.ChoiceId);
                    if (cd != null && !cd.HasChallenge)
                    {
                        // Non-challenge choice: show Rare Event or Time passes
                        bool isRare = !string.IsNullOrEmpty(overrideNextPath) && overrideNextPath == cd.rareEventPath;
                        if (isRare)
                        {
                            resultText.text = "RARE EVENT!";
                            resultText.color = new Color(1f, 0.84f, 0f); // Golden yellow
                        }
                        else
                        {
                            resultText.text = "TIME PASSES...";
                            resultText.color = Color.gray;
                        }
                    }
                    else
                    {
                        // Challenge choice: show critical tiers or normal success/failure
                        bool isCritSuccess = success && cd != null && !string.IsNullOrEmpty(cd.critSuccessPath) && overrideNextPath == cd.critSuccessPath;
                        bool isCritFailure = !success && cd != null && !string.IsNullOrEmpty(cd.critFailurePath) && overrideNextPath == cd.critFailurePath;
                        if (isCritSuccess)
                        {
                            resultText.text = "CRITICAL SUCCESS!";
                            resultText.color = Color.green; // Green for critical success
                        }
                        else if (isCritFailure)
                        {
                            resultText.text = "CRITICAL FAILURE!";
                            resultText.color = new Color(0.6f, 0f, 0f); // Deep red
                        }
                        else
                        {
                            resultText.text = success ? "SUCCESS!" : "FAILURE...";
                            resultText.color = success ? Color.green : Color.red;
                        }
                    }
                    break;
            }
        }
        
        // Show saving roll message if applicable
        if (rollChanceText != null && savedByRoll)
        {
            // Use the outcomeType parameter to determine the appropriate message
            switch (outcomeType.ToLower())
            {
                case "rare_event":
                    rollChanceText.text = "TRIGGERED BY ENHANCED ROLL CHANCE!";
                    rollChanceText.color = new Color(1f, 0.84f, 0f); // Golden yellow
                    break;
                case "critical_success":
                    rollChanceText.text = "TRIGGERED BY ENHANCED ROLL CHANCE!";
                    rollChanceText.color = Color.green;
                    break;
                case "success":
                    rollChanceText.text = "SAVED BY ENHANCED ROLL CHANCE!";
                    rollChanceText.color = Color.green;
                    break;
                default:
                    rollChanceText.text = "SAVED BY ENHANCED ROLL CHANCE!";
                    rollChanceText.color = Color.green;
                    break;
            }
            
            rollChanceText.gameObject.SetActive(true);
        }
        else if (rollChanceText != null)
        {
            rollChanceText.gameObject.SetActive(false);
        }
        
        // Fade in the result
        canvasGroup.DOFade(1f, 0.5f).SetEase(Ease.OutQuad).SetLink(resultObj).OnComplete(() => {
            // Wait for user input, then fade out and proceed
            StartCoroutine(WaitForInputAndProceed(resultObj, choice, success));
        });
    }
    
    /// <summary>
    /// Wait for user input, then fade out and proceed to next screen
    /// </summary>
    private IEnumerator WaitForInputAndProceed(GameObject resultObj, ChorusChoice choice, bool success)
    {
        // Wait for any input
        while (!Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
        {
            yield return null;
        }
        
        // Fade out the result
        CanvasGroup canvasGroup = resultObj.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad).SetLink(resultObj).OnComplete(() => {
                Destroy(resultObj);
                
                // Consequences are now deferred to verse screens; no immediate application here
                
                // Navigate to appropriate path based on result
                string targetPath;
                if (!string.IsNullOrEmpty(overrideNextPath))
                {
                    targetPath = overrideNextPath;
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Using override next path: {targetPath}", "ChorusScreenManager");
                    overrideNextPath = null;
                }
                else if (success && !string.IsNullOrEmpty(choice.SuccessPath))
                {
                    targetPath = choice.SuccessPath;
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Navigating to success path: {targetPath}", "ChorusScreenManager");
                }
                else if (!success && !string.IsNullOrEmpty(choice.FailurePath))
                {
                    targetPath = choice.FailurePath;
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Navigating to failure path: {targetPath}", "ChorusScreenManager");
                }
                else
                {
                    // Fallback to destination path if no success/failure paths specified
                    targetPath = choice.DestinationPath;
                    GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] No success/failure path specified, using destination: {targetPath}", "ChorusScreenManager");
                }
                
                // Navigate directly by knot without overrides/static flow
                var vm = eventSystem != null ? eventSystem.GetVolumeManager() : null;
                vm?.NavigateToKnot(targetPath);
            });
        }
    }
    
    /// <summary>
    /// Navigate to a specific path in the Ink story
    /// </summary>
    private void NavigateToPath(string path)
    {
        if (currentStory == null || string.IsNullOrEmpty(path)) return;
        
        try
        {
            // Navigate to the specified path
            currentStory.ChoosePathString(path);
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Successfully navigated to path: {path}", "ChorusScreenManager");
            var vm = eventSystem != null ? eventSystem.GetVolumeManager() : null;
            if (vm != null)
            {
                // Prevent accidental self-navigation loops and normalize
                var targetTop = InkDrivenEventSetup.NormalizeKnotName(path) ?? path;
                GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] NavigateToPath normalized '{path}' -> '{targetTop}'", "ChorusScreenManager");
                if (!string.IsNullOrEmpty(targetTop)) vm.NavigateToKnot(targetTop);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ChorusScreenManager] Failed to navigate to path '{path}': {ex.Message}");
            // Try to continue with the story if navigation fails
            if (currentStory.canContinue)
            {
                currentStory.Continue();
            }
        }
    }
    
    /// <summary>
    /// Get the current choices container for background elements to reference
    /// </summary>
    public Transform GetChoicesContainer()
    {
        return isPragmatismAvailable ? fullOutcomesChoicesContainer : twoChoicesChoicesContainer;
    }

    /// <summary>
    /// Whether the decision token is currently being dragged
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }
    
    /// <summary>
    /// Called when a choice is made by dropping the token
    /// </summary>
    public void OnChoiceMade(string choiceId)
    {
        GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Choice made: {choiceId}", "ChorusScreenManager");
        
        // Mark that a choice was made to prevent choices from fading back in
        choiceWasMade = true;
        
        // The choice selection will handle the challenge resolution and navigation
        // This method just ensures the UI state is maintained
    }
    
    /// <summary>
    /// Refresh all choices (called when game state changes)
    /// </summary>
    public void RefreshChoices()
    {
        // Refresh the static choice objects
        if (isPragmatismAvailable)
        {
            if (fullOutcomesIdealism != null) fullOutcomesIdealism.RefreshChoice();
            if (fullOutcomesRealism != null) fullOutcomesRealism.RefreshChoice();
            if (fullOutcomesPragmatism != null) fullOutcomesPragmatism.RefreshChoice();
        }
        else
        {
            if (twoChoicesIdealism != null) twoChoicesIdealism.RefreshChoice();
            if (twoChoicesRealism != null) twoChoicesRealism.RefreshChoice();
        }
        
        UpdatePillarDisplay();
    }

    /// <summary>
    /// Get an enhanced roll that includes saving roll chance bonus
    /// </summary>
    /// <param name="naturalRoll">The natural 1-100 roll</param>
    /// <returns>Enhanced roll value capped at 100 (best possible roll)</returns>
    private int GetEnhancedRoll(int naturalRoll)
    {
        if (statManager == null) return naturalRoll;
        
        float savingRollBonus = statManager.GetSavingRollChancePercentCapped();
        int enhancedRoll = naturalRoll + Mathf.RoundToInt(savingRollBonus);
        
        // Cap the enhanced roll to 100 (best possible roll)
        enhancedRoll = Mathf.Min(enhancedRoll, 100);
        
        // Log the enhancement for debugging
        if (savingRollBonus > 0f)
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Roll enhanced: {naturalRoll} + {savingRollBonus:F1}% = {enhancedRoll} (capped at 100)", "ChorusScreenManager");
        }
        
        return enhancedRoll;
    }
    
    /// <summary>
    /// Check if saving roll made the difference for a given outcome
    /// </summary>
    /// <param name="naturalRoll">The natural roll before enhancement</param>
    /// <param name="enhancedRoll">The enhanced roll after saving roll bonus</param>
    /// <param name="threshold">The threshold that needed to be met</param>
    /// <param name="outcomeType">Type of outcome (success, critical, rare)</param>
    /// <returns>True if saving roll made the difference</returns>
    private bool DidSavingRollMakeDifference(int naturalRoll, int enhancedRoll, int threshold, string outcomeType)
    {
        if (statManager == null) return false;
        
        float savingRollBonus = statManager.GetSavingRollChancePercentCapped();
        if (savingRollBonus <= 0f) return false;
        
        // Check if natural roll would have failed but enhanced roll succeeded
        bool naturalWouldFail = naturalRoll <= threshold;
        bool enhancedSucceeds = enhancedRoll > threshold;
        
        if (naturalWouldFail && enhancedSucceeds)
        {
            GameLoggingSystem.Instance.LogEvent($"[ChorusScreenManager] Saving roll made difference: natural {naturalRoll} <= {threshold} but enhanced {enhancedRoll} > {threshold} for {outcomeType}", "ChorusScreenManager");
            return true;
        }
        
        return false;
    }
} 