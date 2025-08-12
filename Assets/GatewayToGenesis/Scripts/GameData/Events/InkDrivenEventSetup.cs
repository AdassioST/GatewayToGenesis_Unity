using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using System.Text.RegularExpressions;
using System.Linq;

/// <summary>
/// Automatically creates EventVolumes from Ink files in the Resources/Events folder
/// This eliminates the need for separate C# scripts for each volume
/// </summary>
public class InkDrivenEventSetup : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool enableDebugLogging = true;
    [Header("Auto Setup Settings")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private string eventsFolderPath = "Events";
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupAllVolumesFromInk();
        }
    }
    
    /// <summary>
    /// Automatically create EventVolumes from all Ink files in the Resources/Events folder
    /// </summary>
    public void SetupAllVolumesFromInk()
    {
        // Load all text assets from Resources/Events (both .ink and compiled .json)
        TextAsset[] assets = Resources.LoadAll<TextAsset>(eventsFolderPath);

        // Group by base name (Unity strips extensions)
        var grouped = new Dictionary<string, List<TextAsset>>();
        foreach (var ta in assets)
        {
            if (!grouped.ContainsKey(ta.name)) grouped[ta.name] = new List<TextAsset>();
            grouped[ta.name].Add(ta);
        }

        int created = 0;
        foreach (var kvp in grouped)
        {
            TextAsset jsonAsset = null;
            TextAsset inkAsset = null;

            foreach (var ta in kvp.Value)
            {
                string text = ta.text?.TrimStart();
                if (!string.IsNullOrEmpty(text) && text.StartsWith("{"))
                {
                    // Likely compiled Ink JSON
                    jsonAsset = ta;
                }
                else
                {
                    // Likely raw ink file
                    inkAsset = ta;
                }
            }

            // Create a volume if we have at least one asset
            CreateVolumeFromAssets(baseName: kvp.Key, compiledJson: jsonAsset ?? inkAsset, inkSource: inkAsset);
            created++;
        }

        Debug.Log($"[InkDrivenEventSetup] Created {created} volumes from Events folder");
    }
    
    /// <summary>
    /// Create a single EventVolume from an Ink file
    /// </summary>
    private void CreateVolumeFromAssets(string baseName, TextAsset compiledJson, TextAsset inkSource)
    {
        if (compiledJson == null)
        {
            Debug.LogWarning($"[InkDrivenEventSetup] No compiled Ink JSON found for '{baseName}'. Ensure the .ink is compiled to .json in Resources.");
        }

        EventVolume volume = new EventVolume
        {
            volumeName = baseName,
            inkMasterfile = compiledJson, // Prefer compiled JSON for runtime Story
            isUnlocked = true,
            priority = 5
        };

        // Prefer building nodes from compiled story JSON (Ink-native), fallback to raw ink parsing
        bool builtFromStory = false;
        if (compiledJson != null)
        {
            try
            {
                BuildStoryNodesFromCompiledStory(volume, compiledJson.text);
                builtFromStory = volume.storyNodes.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to build nodes from compiled story for '{baseName}': {ex.Message}");
            }
        }
        if (!builtFromStory)
        {
            string parseText = inkSource != null ? inkSource.text : compiledJson != null ? compiledJson.text : string.Empty;
            ParseInkFileForStoryNodes(volume, parseText);
        }

        // Register the volume
        EventSystemLogic.Instance?.AddVolume(volume);
    }

    /// <summary>
    /// Build StoryNodes using Ink's compiled Story data: enumerate knots and read tags with Story.TagsForContentAtPath
    /// </summary>
    private void BuildStoryNodesFromCompiledStory(EventVolume volume, string compiledJsonText)
    {
        if (string.IsNullOrEmpty(compiledJsonText)) return;
        Story story = new Story(compiledJsonText);

        // The main container holds named knots in namedContent
        var named = story.mainContentContainer?.namedContent;
        if (named == null || named.Count == 0)
        {
            if (enableDebugLogging) Debug.Log("[InkDrivenEventSetup] Compiled story has no named content");
            return;
        }

        foreach (var kv in named)
        {
            string knotName = kv.Key;
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Processing knot: '{knotName}'");
            }
            
            // Get tags for this knot (metadata lines imported as tags)
            List<string> tags = story.TagsForContentAtPath(knotName) ?? new List<string>();
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Found {tags.Count} tags for knot '{knotName}': {string.Join(", ", tags)}");
            }

            string title = null, description = null, conditions = null, consequences = null, screenFlow = null;
            Dictionary<string, string> uiMetadata = new Dictionary<string, string>();

            foreach (string tag in tags)
            {
                string t = tag.Trim();
                if (t.StartsWith("title:")) title = t.Substring(6).Trim();
                else if (t.StartsWith("description:")) description = t.Substring(12).Trim();
                else if (t.StartsWith("conditions:")) conditions = t.Substring(11).Trim();
                else if (t.StartsWith("consequences:")) consequences = t.Substring(13).Trim();
                else if (t.StartsWith("screen_flow:")) screenFlow = t.Substring(12).Trim();
                else if (t.StartsWith("splash_art:")) uiMetadata["splash_art"] = t.Substring(11).Trim();
                else if (t.StartsWith("event_type:")) uiMetadata["event_type"] = t.Substring(11).Trim();
                else if (t.StartsWith("priority:")) uiMetadata["priority"] = t.Substring(9).Trim();
                else if (t.StartsWith("event_color:")) uiMetadata["event_color"] = t.Substring(12).Trim();
                else if (t.StartsWith("button_text:")) uiMetadata["button_text"] = t.Substring(12).Trim();
                else if (t.StartsWith("background:")) uiMetadata["background"] = t.Substring(11).Trim();
                else if (t.StartsWith("speaker:")) uiMetadata["speaker"] = t.Substring(9).Trim();
                else if (t.StartsWith("portrait:")) uiMetadata["portrait"] = t.Substring(10).Trim();
                else if (t.StartsWith("layout:")) uiMetadata["layout"] = t.Substring(8).Trim();
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Extracted metadata for '{knotName}': title='{title}', conditions='{conditions}', event_type='{uiMetadata.GetValueOrDefault("event_type")}'");
            }

            // Only create story nodes for knots that have the essential metadata (title and conditions)
            // This prevents internal story flow knots from being treated as separate events
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(conditions) && !string.IsNullOrEmpty(uiMetadata.GetValueOrDefault("event_type")))
            {
                StoryNode node = CreateStoryNodeFromParsedData(
                    knotName,
                    title,
                    description,
                    conditions,
                    consequences,
                    screenFlow,
                    uiMetadata
                );

                volume.storyNodes.Add(node);
                if (enableDebugLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] (Compiled) Added story node '{knotName}' with title '{node.storyTitle}' and {node.storyConditions.Count} conditions");
                }
            }
            else if (enableDebugLogging)
            {
                string missingFields = "";
                if (string.IsNullOrEmpty(title)) missingFields += "title, ";
                if (string.IsNullOrEmpty(conditions)) missingFields += "conditions, ";
                if (string.IsNullOrEmpty(uiMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                missingFields = missingFields.TrimEnd(',', ' ');
                
                Debug.Log($"[InkDrivenEventSetup] (Compiled) Skipped internal knot '{knotName}' - missing: {missingFields}");
            }
        }

        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] (Compiled) Parsed {volume.storyNodes.Count} story node(s) for volume '{volume.volumeName}'");
        }
    }
    
    /// <summary>
    /// Parse Ink file content to find story nodes and their metadata
    /// </summary>
    private void ParseInkFileForStoryNodes(EventVolume volume, string inkContent)
    {
        string[] lines = inkContent.Split('\n');
        string currentNodeName = null;
        string currentTitle = "";
        string currentDescription = "";
        string currentConditions = "";
        string currentConsequences = "";
        string currentScreenFlow = "";
        Dictionary<string, string> currentUIMetadata = new Dictionary<string, string>();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            // Check for knot definition (robust): lines like "=== name ===" or with extra spaces
            if (trimmedLine.StartsWith("==="))
            {
                // Save previous node if exists
                if (!string.IsNullOrEmpty(currentNodeName))
                {
                    // Only create story nodes for knots that have the essential metadata (title and conditions)
                    // This prevents internal story flow knots from being treated as separate events
                    if (!string.IsNullOrEmpty(currentTitle) && !string.IsNullOrEmpty(currentConditions) && !string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type")))
                    {
                        StoryNode storyNode = CreateStoryNodeFromParsedData(
                            currentNodeName, 
                            currentTitle, 
                            currentDescription, 
                            currentConditions, 
                            currentConsequences, 
                            currentScreenFlow, 
                            currentUIMetadata
                        );
                        volume.storyNodes.Add(storyNode);
                        
                        if (enableDebugLogging)
                        {
                            Debug.Log($"[InkDrivenEventSetup] Added story node '{currentNodeName}' with title '{currentTitle}' and {storyNode.storyConditions.Count} conditions");
                        }
                    }
                    else if (enableDebugLogging)
                    {
                        string missingFields = "";
                        if (string.IsNullOrEmpty(currentTitle)) missingFields += "title, ";
                        if (string.IsNullOrEmpty(currentConditions)) missingFields += "conditions, ";
                        if (string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                        missingFields = missingFields.TrimEnd(',', ' ');
                        
                        Debug.Log($"[InkDrivenEventSetup] Skipped internal knot '{currentNodeName}' - missing: {missingFields}");
                    }
                }

                // Start new node: extract between leading/trailing '='
                // Use regex to capture the name between === ... ===
                var match = Regex.Match(trimmedLine, "^=+\\s*(.*?)\\s*=+$");
                currentNodeName = match.Success ? match.Groups[1].Value : trimmedLine.Trim('=').Trim();
                currentTitle = "";
                currentDescription = "";
                currentConditions = "";
                currentConsequences = "";
                currentScreenFlow = "";
                currentUIMetadata = new Dictionary<string, string>();

                if (enableDebugLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] Found knot: '{currentNodeName}'");
                }
            }
            // Check for metadata lines
            else if (trimmedLine.StartsWith("#"))
            {
                ParseMetadataLine(trimmedLine, ref currentTitle, ref currentDescription, 
                    ref currentConditions, ref currentConsequences, ref currentScreenFlow, ref currentUIMetadata);
            }
        }

        // Save the last node
        if (!string.IsNullOrEmpty(currentNodeName))
        {
            // Only create story nodes for knots that have the essential metadata (title and conditions)
            // This prevents internal story flow knots from being treated as separate events
            if (!string.IsNullOrEmpty(currentTitle) && !string.IsNullOrEmpty(currentConditions) && !string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type")))
            {
                StoryNode storyNode = CreateStoryNodeFromParsedData(
                    currentNodeName, 
                    currentTitle, 
                    currentDescription, 
                    currentConditions, 
                    currentConsequences, 
                    currentScreenFlow, 
                    currentUIMetadata
                );
                volume.storyNodes.Add(storyNode);
                
                if (enableDebugLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] Added story node '{currentNodeName}' with title '{currentTitle}' and {storyNode.storyConditions.Count} conditions");
                }
            }
            else if (enableDebugLogging)
            {
                string missingFields = "";
                if (string.IsNullOrEmpty(currentTitle)) missingFields += "title, ";
                if (string.IsNullOrEmpty(currentConditions)) missingFields += "conditions, ";
                if (string.IsNullOrEmpty(currentUIMetadata.GetValueOrDefault("event_type"))) missingFields += "event_type, ";
                missingFields = missingFields.TrimEnd(',', ' ');
                
                Debug.Log($"[InkDrivenEventSetup] Skipped internal knot '{currentNodeName}' - missing: {missingFields}");
            }
        }

        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] Parsed {volume.storyNodes.Count} story node(s) for volume '{volume.volumeName}'");
            for (int i = 0; i < volume.storyNodes.Count; i++)
            {
                var sn = volume.storyNodes[i];
                Debug.Log($"[InkDrivenEventSetup] Node[{i}]: name='{sn.nodeName}', title='{sn.storyTitle}', conditions={sn.storyConditions.Count}, flowSteps={sn.screenFlow.Count}");
            }
        }
    }
    
    /// <summary>
    /// Parse metadata line and extract information
    /// </summary>
    private void ParseMetadataLine(string line, ref string title, ref string description, ref string conditions, ref string consequences, ref string screenFlow, ref Dictionary<string, string> uiMetadata)
    {
        line = line.Trim();
        
        if (line.StartsWith("# title:"))
        {
            title = line.Substring(8).Trim();
        }
        else if (line.StartsWith("# description:"))
        {
            description = line.Substring(14).Trim();
        }
        else if (line.StartsWith("# conditions:"))
        {
            conditions = line.Substring(13).Trim();
        }
        else if (line.StartsWith("# consequences:"))
        {
            consequences = line.Substring(15).Trim();
        }
        else if (line.StartsWith("# screen_flow:"))
        {
            screenFlow = line.Substring(15).Trim();
        }
        // New UI metadata parsing
        else if (line.StartsWith("# splash_art:"))
        {
            uiMetadata["splash_art"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# event_type:"))
        {
            uiMetadata["event_type"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# event_color:"))
        {
            uiMetadata["event_color"] = line.Substring(13).Trim();
        }
        else if (line.StartsWith("# button_text:"))
        {
            uiMetadata["button_text"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# background:"))
        {
            uiMetadata["background"] = line.Substring(12).Trim();
        }
        else if (line.StartsWith("# speaker:"))
        {
            uiMetadata["speaker"] = line.Substring(9).Trim();
        }
        else if (line.StartsWith("# portrait:"))
        {
            uiMetadata["portrait"] = line.Substring(10).Trim();
        }
        else if (line.StartsWith("# layout:"))
        {
            uiMetadata["layout"] = line.Substring(8).Trim();
        }
        else if (line.StartsWith("# priority:"))
        {
            uiMetadata["priority"] = line.Substring(10).Trim();
        }
    }
    
    /// <summary>
    /// Create a story node from parsed data
    /// </summary>
    private StoryNode CreateStoryNodeFromParsedData(string nodeName, string title, string description, string conditions, string consequences, string screenFlow, Dictionary<string, string> uiMetadata)
    {
        StoryNode storyNode = new StoryNode();
        storyNode.nodeName = nodeName;
        storyNode.storyTitle = title ?? nodeName;
        storyNode.storyDescription = description ?? "";
        storyNode.isUnlocked = true;
        
        // Parse priority from metadata, default to 0 if not specified
        if (uiMetadata.ContainsKey("priority") && int.TryParse(uiMetadata["priority"], out int parsedPriority))
        {
            storyNode.priority = parsedPriority;
        }
        else
        {
            storyNode.priority = 0; // Default priority for events without explicit priority
        }

        // Parse conditions and consequences
        if (!string.IsNullOrEmpty(conditions))
        {
            storyNode.storyConditions = ParseConditions(conditions);
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Node '{nodeName}' parsed {storyNode.storyConditions.Count} condition(s): '{conditions}'");
            }
        }

        if (!string.IsNullOrEmpty(consequences))
        {
            storyNode.storyConsequences = ParseConsequences(consequences);
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Node '{nodeName}' parsed {storyNode.storyConsequences.Count} consequence(s): '{consequences}'");
            }
        }

        // Generate screen flow
        storyNode.screenFlow = GenerateScreenFlowFromNodeName(nodeName, screenFlow);

        // Store UI metadata in the story node for later use
        storyNode.uiMetadata = uiMetadata;

        return storyNode;
    }
    
    /// <summary>
    /// Parse condition strings into EventCondition objects
    /// </summary>
    private List<EventCondition> ParseConditions(string conditionString)
    {
        List<EventCondition> conditions = new List<EventCondition>();
        
        if (string.IsNullOrEmpty(conditionString))
            return conditions;
        
        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] Parsing conditions: '{conditionString}'");
        }
        
        // Support multiple conditions separated by ';'
        string[] parts = conditionString.Split(';');
        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Processing condition part: '{trimmed}'");
            }
            
            EventCondition condition = ParseConditionString(trimmed);
            if (condition != null)
            {
                conditions.Add(condition);
                if (enableDebugLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] Successfully parsed condition: {condition.type} {condition.targetName} {condition.comparison} {condition.requiredValue}");
                }
            }
            else
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse condition part: '{trimmed}'");
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] Parsed {conditions.Count} conditions successfully");
        }
        
        return conditions;
    }
    
    /// <summary>
    /// Parse a single condition string
    /// </summary>
    private EventCondition ParseConditionString(string conditionStr)
    {
        // Example formats:
        // "score:quest_progress >= 1"
        // "resource:gold >= 100"
        // "technology:agriculture == 1"
        // "ritual_seventh == 1"
        
        string[] parts = conditionStr.Split(':');
        if (parts.Length != 2) return null;
        
        string type = parts[0].Trim();
        string condition = parts[1].Trim();
        
        // Parse the condition part (e.g., "quest_progress >= 1")
        string[] conditionParts = condition.Split(' ');
        
        // Special handling for technology conditions (no comparison operator needed)
        if (type.ToLower() == "technology")
        {
            // Technology conditions just check if the technology is unlocked
            // Format: technology:Technology Name
            EventCondition techCondition = new EventCondition
            {
                targetName = condition.Trim(), // Use the full technology name
                requiredValue = 1, // Always check for unlocked (1 = true)
                comparison = ComparisonOperator.Equals
            };
            techCondition.type = EventCondition.ConditionType.TechnologyCheck;
            return techCondition;
        }
        
        // For all other condition types, expect the standard format
        if (conditionParts.Length < 3) return null;
        
        string targetName = conditionParts[0];
        string operatorStr = conditionParts[1];
        
        // Use TryParse instead of Parse to avoid exceptions
        if (!int.TryParse(conditionParts[2], out int value))
        {
            Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse condition value '{conditionParts[2]}' from condition string: '{conditionStr}'");
            return null;
        }
        
        EventCondition eventCondition = new EventCondition
        {
            targetName = targetName,
            requiredValue = value,
            comparison = ParseComparisonOperator(operatorStr)
        };
        
        // Set the condition type based on the prefix
        switch (type.ToLower())
        {
            case "score":
                eventCondition.type = EventCondition.ConditionType.ScoreCheck;
                break;
            case "resource":
                eventCondition.type = EventCondition.ConditionType.ResourceCheck;
                break;
            case "technology":
                eventCondition.type = EventCondition.ConditionType.TechnologyCheck;
                break;
            case "stat":
                eventCondition.type = EventCondition.ConditionType.StatCheck;
                break;
            case "seventh":
                eventCondition.type = EventCondition.ConditionType.SeventhCheck;
                break;
            case "phase":
                eventCondition.type = EventCondition.ConditionType.PhaseCheck;
                break;
            case "echo":
                eventCondition.type = EventCondition.ConditionType.EchoCheck;
                break;
            case "cycle":
                eventCondition.type = EventCondition.ConditionType.CycleCheck;
                break;
            case "ritual_seventh":
                eventCondition.type = EventCondition.ConditionType.RitualSeventhCheck;
                break;
            case "population":
                eventCondition.type = EventCondition.ConditionType.PopulationCheck;
                break;
            case "housing":
                eventCondition.type = EventCondition.ConditionType.HousingCheck;
                break;
            case "vagrants":
                eventCondition.type = EventCondition.ConditionType.VagrantsCheck;
                break;
            case "deaths":
                eventCondition.type = EventCondition.ConditionType.DeathsCheck;
                break;
            case "vagrant_deaths":
                eventCondition.type = EventCondition.ConditionType.VagrantDeathsCheck;
                break;
            case "true_deaths":
                eventCondition.type = EventCondition.ConditionType.TrueDeathsCheck;
                break;
            default:
                Debug.LogWarning($"[InkDrivenEventSetup] Unknown condition type '{type}' in condition string: '{conditionStr}'");
                return null;
        }
        
        return eventCondition;
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
    /// Parse consequence strings into EventConsequence objects
    /// </summary>
    private List<EventConsequence> ParseConsequences(string consequenceString)
    {
        List<EventConsequence> consequences = new List<EventConsequence>();
        
        if (string.IsNullOrEmpty(consequenceString))
            return consequences;
        
        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] Parsing consequences: '{consequenceString}'");
        }
        
        // Support multiple consequences separated by ';'
        string[] parts = consequenceString.Split(';');
        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[InkDrivenEventSetup] Processing consequence part: '{trimmed}'");
            }
            
            EventConsequence consequence = ParseConsequenceString(trimmed);
            if (consequence != null)
            {
                consequences.Add(consequence);
                if (enableDebugLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] Successfully parsed consequence: {consequence.type} {consequence.targetName} {consequence.value}");
                }
            }
            else
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse consequence part: '{trimmed}'");
            }
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[InkDrivenEventSetup] Parsed {consequences.Count} consequences successfully");
        }
        
        return consequences;
    }
    
    /// <summary>
    /// Parse a single consequence string
    /// </summary>
    private EventConsequence ParseConsequenceString(string consequenceStr)
    {
        // Example formats:
        // "score:quest_progress +1"
        // "resource:gold +50"
        // "technology:agriculture enlightened"
        // "production:Timber Camp -1" (note: production names can have spaces)
        
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
            // Parse numeric consequences like "quest_progress +1" or "Timber Camp -1"
            // For production units, the name might contain spaces, so we need to find the last space
            int lastSpaceIndex = consequence.LastIndexOf(' ');
            if (lastSpaceIndex > 0)
            {
                string targetName = consequence.Substring(0, lastSpaceIndex).Trim();
                string valueStr = consequence.Substring(lastSpaceIndex + 1).Trim();
                
                // Use TryParse instead of Parse to avoid exceptions
                if (int.TryParse(valueStr, out int value))
                {
                    eventConsequence.targetName = targetName;
                    eventConsequence.value = value;
                    
                    // Set the consequence type based on the prefix
                    switch (type.ToLower())
                    {
                        case "score":
                            eventConsequence.type = EventConsequence.ConsequenceType.ScoreChange;
                            break;
                        case "resource":
                            eventConsequence.type = EventConsequence.ConsequenceType.ResourceChange;
                            break;
                        case "production":
                            eventConsequence.type = EventConsequence.ConsequenceType.ProductionUnitChange;
                            break;
                        case "stat":
                            eventConsequence.type = EventConsequence.ConsequenceType.StatChange;
                            break;
                        case "population":
                            eventConsequence.type = EventConsequence.ConsequenceType.PopulationChange;
                            break;
                        case "housing":
                            eventConsequence.type = EventConsequence.ConsequenceType.HousingChange;
                            break;
                        case "vagrants":
                            eventConsequence.type = EventConsequence.ConsequenceType.VagrantsChange;
                            break;
                        case "deaths":
                            eventConsequence.type = EventConsequence.ConsequenceType.DeathsChange;
                            break;
                        case "death_records_revision":
                            eventConsequence.type = EventConsequence.ConsequenceType.DeathRecordsRevision;
                            break;
                        default:
                            eventConsequence.type = EventConsequence.ConsequenceType.ScoreChange;
                            break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[InkDrivenEventSetup] Failed to parse consequence value '{valueStr}' from consequence string: '{consequenceStr}'");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"[InkDrivenEventSetup] Invalid consequence format (no space found): '{consequenceStr}'");
                return null;
            }
        }
        
        return eventConsequence;
    }
    
    /// <summary>
    /// Generate screen flow based on node name and optional custom screen flow
    /// This allows for truly flexible screen sequences per node
    /// </summary>
    private List<ScreenFlowStep> GenerateScreenFlowFromNodeName(string nodeName, string customScreenFlow = null)
    {
        List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>();
        
        // If custom screen flow is provided, use it
        if (!string.IsNullOrEmpty(customScreenFlow))
        {
            return ParseCustomScreenFlow(nodeName, customScreenFlow);
        }
        else
        {
            // Default flow: Splash -> Verse -> Chorus -> Outro
            screenFlow.Add(new ScreenFlowStep
            {
                flowType = ScreenFlowStep.FlowType.Splash,
                screenId = $"{nodeName}_splash",
                // Splash now uses the main node content and Ink choices
                inkKnot = nodeName,
                displayDuration = 3f,
                waitForInput = false
            });
            
            screenFlow.Add(new ScreenFlowStep
            {
                flowType = ScreenFlowStep.FlowType.Verse,
                screenId = $"{nodeName}_verse",
                inkKnot = nodeName,
                waitForInput = true
            });
            
            screenFlow.Add(new ScreenFlowStep
            {
                flowType = ScreenFlowStep.FlowType.Chorus,
                screenId = $"{nodeName}_chorus",
                // Optional: if you later want chorus Ink content, name the knot {nodeName}_chorus
                waitForInput = true
            });
            
            screenFlow.Add(new ScreenFlowStep
            {
                flowType = ScreenFlowStep.FlowType.Outro,
                screenId = $"{nodeName}_outro",
                inkKnot = $"{nodeName}_outro",
                displayDuration = 2f,
                waitForInput = false
            });
        }
        
        return screenFlow;
    }
    
    /// <summary>
    /// Parse custom screen flow from metadata string
    /// </summary>
    private List<ScreenFlowStep> ParseCustomScreenFlow(string nodeName, string screenFlowString)
    {
        List<ScreenFlowStep> screenFlow = new List<ScreenFlowStep>();
        
        // Parse screen flow like "splash,verse,verse,chorus,bridge,chorus,verse,outro"
        string[] screenTypes = screenFlowString.Split(',');
        
        for (int i = 0; i < screenTypes.Length; i++)
        {
            string screenType = screenTypes[i].Trim().ToLower();
            ScreenFlowStep step = new ScreenFlowStep();
            
            switch (screenType)
            {
                case "splash":
                    step.flowType = ScreenFlowStep.FlowType.Splash;
                    step.screenId = $"{nodeName}_splash";
                    step.inkKnot = nodeName; // splash uses main node content
                    step.displayDuration = 3f;
                    step.waitForInput = false;
                    break;
                    
                case "verse":
                    step.flowType = ScreenFlowStep.FlowType.Verse;
                    step.screenId = $"{nodeName}_verse_{GetVerseNumber(screenFlow, screenType)}";
                    step.inkKnot = $"{nodeName}_verse_{GetVerseNumber(screenFlow, screenType)}";
                    step.waitForInput = true;
                    break;
                    
                case "chorus":
                    step.flowType = ScreenFlowStep.FlowType.Chorus;
                    step.screenId = $"{nodeName}_chorus_{GetChorusNumber(screenFlow, screenType)}";
                    // Optional: allow content via Ink if provided
                    step.inkKnot = $"{nodeName}_chorus_{GetChorusNumber(screenFlow, screenType)}";
                    step.waitForInput = true;
                    break;
                    
                case "bridge":
                    step.flowType = ScreenFlowStep.FlowType.Bridge;
                    step.screenId = $"{nodeName}_bridge_{GetBridgeNumber(screenFlow, screenType)}";
                    step.inkKnot = $"{nodeName}_bridge_{GetBridgeNumber(screenFlow, screenType)}";
                    step.displayDuration = 2f;
                    step.waitForInput = false;
                    break;
                    
                case "outro":
                    step.flowType = ScreenFlowStep.FlowType.Outro;
                    step.screenId = $"{nodeName}_outro";
                    step.inkKnot = $"{nodeName}_outro";
                    step.displayDuration = 2f;
                    step.waitForInput = false;
                    break;
                    
                default:
                    // Unknown screen type, skip
                    continue;
            }
            
            screenFlow.Add(step);
        }
        
        return screenFlow;
    }
    
    /// <summary>
    /// Get the verse number for this verse screen
    /// </summary>
    private int GetVerseNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Verse) + 1;
    }
    
    /// <summary>
    /// Get the chorus number for this chorus screen
    /// </summary>
    private int GetChorusNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Chorus) + 1;
    }
    
    /// <summary>
    /// Get the bridge number for this bridge screen
    /// </summary>
    private int GetBridgeNumber(List<ScreenFlowStep> currentFlow, string screenType)
    {
        return currentFlow.Count(s => s.flowType == ScreenFlowStep.FlowType.Bridge) + 1;
    }
} 