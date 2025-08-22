using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/// <summary>
/// Displays and manages a single requirement for a Chorus Screen choice
/// Reuses the existing EventCondition system for consistency
/// </summary>
public class RequirementSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image requirementIcon;
    [SerializeField] private Image panel; // Colored panel for met/unmet (green/red)
    
    [Header("Unique Requirement Icons (Assign in Inspector)")]
    [SerializeField] private Sprite populationIcon;
    [SerializeField] private Sprite housingIcon;
    [SerializeField] private Sprite vagrantsIcon;
    [SerializeField] private Sprite deathsIcon;
    [SerializeField] private Sprite vagrantDeathsIcon;
    [SerializeField] private Sprite trueDeathsIcon;
    [SerializeField] private Sprite technologyIcon;
    [SerializeField] private Sprite scoreIcon;
    [SerializeField] private Sprite foodIcon;
    [SerializeField] private Sprite moraleIcon;
    
    [Header("Requirement Data")]
    [SerializeField] private EventCondition condition;
    
    private bool isRequirementMet = false;
    
    /// <summary>
    /// Initialize the requirement slot with condition data
    /// </summary>
    public void InitializeRequirement(EventCondition eventCondition)
    {
        condition = eventCondition;
        UpdateRequirementDisplay();
    }
    
    /// <summary>
    /// Update the requirement display and check if it's met
    /// </summary>
    public void UpdateRequirementDisplay()
    {
        if (condition == null) return;
        
        // Hide or clear text for now (tooltips will provide details later)
        // No text for requirements (tooltips will handle messaging)
        
        // Set the requirement icon based on type/name
        if (requirementIcon != null)
        {
            requirementIcon.sprite = ResolveRequirementIcon(condition);
            requirementIcon.enabled = requirementIcon.sprite != null;
        }
        
        // Check if requirement is met and update status FIRST
        CheckRequirementStatus();
        UpdatePanelColor();

        // Attach/update tooltip with human-readable requirement text AFTER status is determined
        var trigger = GetComponent<TooltipTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<TooltipTrigger>();
        trigger.useCustomTooltip = true;
        trigger.isBreakdownDisplay = false; // Not a breakdown display
        // Build rich tooltip content: title + current vs required + status
        BuildRequirementTooltip(condition, out string title, out string desc);
        trigger.customTitle = title;
        trigger.customDescription = desc;
        trigger.customType = string.Empty;
    }
    
    /// <summary>
    /// Check if the current requirement is met
    /// </summary>
    private void CheckRequirementStatus()
    {
        if (condition == null) return;
        
        isRequirementMet = condition.Evaluate();
    }
    
    /// <summary>
    /// Update the status indicator (checkmark/X)
    /// </summary>
    private void UpdatePanelColor()
    {
        if (panel == null) return;
        panel.color = isRequirementMet ? Color.green : Color.red;
    }
    
    /// <summary>
    /// Format the requirement text for display
    /// </summary>
    private string FormatRequirementText()
    {
        if (condition == null) return "Unknown Requirement";
        
        string comparisonSymbol = GetComparisonSymbol(condition.comparison);
        
        switch (condition.type)
        {
            case EventCondition.ConditionType.ScoreCheck:
                return $"Score: {condition.targetName} {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.StatCheck:
                return $"Stat: {condition.targetName} {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.ResourceCheck:
                return $"Resource: {condition.targetName} {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.TechnologyCheck:
                return $"Technology: {condition.targetName}";
                
            case EventCondition.ConditionType.SeventhCheck:
                return $"Seventh {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.PhaseCheck:
                return $"Phase {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.EchoCheck:
                return $"Echo {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.CycleCheck:
                return $"Cycle {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.RitualSeventhCheck:
                return "Ritual Seventh";
                
            case EventCondition.ConditionType.PopulationCheck:
                return $"Population {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.HousingCheck:
                return $"Housing {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.VagrantsCheck:
                return $"Vagrants {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.DeathsCheck:
                return $"Deaths {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.VagrantDeathsCheck:
                return $"Vagrant Deaths {comparisonSymbol} {condition.requiredValue}";
                
            case EventCondition.ConditionType.TrueDeathsCheck:
                return $"True Deaths {comparisonSymbol} {condition.requiredValue}";
                
            default:
                return $"Requirement: {condition.targetName} {comparisonSymbol} {condition.requiredValue}";
        }
    }
    
    /// <summary>
    /// Get the comparison symbol for display
    /// </summary>
    private string GetComparisonSymbol(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return "=";
            case ComparisonOperator.NotEquals: return "≠";
            case ComparisonOperator.GreaterThan: return ">";
            case ComparisonOperator.LessThan: return "<";
            case ComparisonOperator.GreaterThanOrEqual: return "≥";
            case ComparisonOperator.LessThanOrEqual: return "≤";
            default: return "=";
        }
    }
    
    private Sprite ResolveRequirementIcon(EventCondition requirement)
    {
        if (requirement == null) return null;
        switch (requirement.type)
        {
            case EventCondition.ConditionType.PopulationCheck:
                return populationIcon;
            case EventCondition.ConditionType.HousingCheck:
                return housingIcon;
            case EventCondition.ConditionType.VagrantsCheck:
                return vagrantsIcon;
            case EventCondition.ConditionType.DeathsCheck:
                return deathsIcon;
            case EventCondition.ConditionType.VagrantDeathsCheck:
                return vagrantDeathsIcon;
            case EventCondition.ConditionType.TrueDeathsCheck:
                return trueDeathsIcon;
            case EventCondition.ConditionType.TechnologyCheck:
                // Generic technology icon; detail is in tooltip/hover
                return technologyIcon;
            case EventCondition.ConditionType.ScoreCheck:
                // Generic event score icon (assign in inspector)
                return scoreIcon != null ? scoreIcon : null;
            case EventCondition.ConditionType.ResourceCheck:
            case EventCondition.ConditionType.StatCheck:
                // Manual icon map for morale; else try to find a game unit icon
                if (string.Equals(requirement.targetName, "morale", System.StringComparison.OrdinalIgnoreCase) && moraleIcon != null)
                {
                    return moraleIcon;
                }
                if (GameUnitsLogic.Instance != null && !string.IsNullOrEmpty(requirement.targetName))
                {
                    if (string.Equals(requirement.targetName, "Food", System.StringComparison.OrdinalIgnoreCase) && foodIcon != null)
                    {
                        return foodIcon;
                    }
                    return GameUnitsLogic.Instance.GetGameUnitIconByName(requirement.targetName);
                }
                break;
            default:
                break;
        }
        return null;
    }

    private string BuildRequirementHoverText(EventCondition c)
    {
        if (c == null) return "Requirement";
        string phrase = GetComparisonPhrase(c.comparison);
        if (string.IsNullOrEmpty(phrase)) phrase = "at least"; // Fallback for unknown comparison operators
        
        switch (c.type)
        {
            case EventCondition.ConditionType.TechnologyCheck:
                return $"Needs {Humanize(c.targetName)} Unlocked";
            case EventCondition.ConditionType.ScoreCheck:
                return $"Needs {phrase} {c.requiredValue} {Humanize(c.targetName)}";
            case EventCondition.ConditionType.StatCheck:
                return $"Needs {phrase} {c.requiredValue} {Humanize(c.targetName)}";
            case EventCondition.ConditionType.ResourceCheck:
                return $"Needs {phrase} {c.requiredValue} {Humanize(c.targetName)}";
            case EventCondition.ConditionType.PopulationCheck:
                return $"Needs {phrase} {c.requiredValue} Population";
            case EventCondition.ConditionType.HousingCheck:
                return $"Needs {phrase} {c.requiredValue} Housing";
            case EventCondition.ConditionType.VagrantsCheck:
                return $"Needs {phrase} {c.requiredValue} Vagrants";
            case EventCondition.ConditionType.DeathsCheck:
                return $"Needs {phrase} {c.requiredValue} Deaths";
            case EventCondition.ConditionType.VagrantDeathsCheck:
                return $"Needs {phrase} {c.requiredValue} Vagrant Deaths";
            case EventCondition.ConditionType.TrueDeathsCheck:
                return $"Needs {phrase} {c.requiredValue} True Deaths";
            case EventCondition.ConditionType.SeventhCheck:
                return $"Needs Seventh {GetComparisonSymbol(c.comparison)} {c.requiredValue}";
            case EventCondition.ConditionType.PhaseCheck:
                return $"Needs Phase {GetComparisonSymbol(c.comparison)} {c.requiredValue}";
            case EventCondition.ConditionType.EchoCheck:
                return $"Needs Echo {GetComparisonSymbol(c.comparison)} {c.requiredValue}";
            case EventCondition.ConditionType.CycleCheck:
                return $"Needs Cycle {GetComparisonSymbol(c.comparison)} {c.requiredValue}";
            case EventCondition.ConditionType.RitualSeventhCheck:
                return "Needs Ritual Seventh";
            default:
                return $"Needs {phrase} {c.requiredValue} {Humanize(c.targetName)}";
        }
    }

    private void BuildRequirementTooltip(EventCondition c, out string title, out string description)
    {
        title = BuildRequirementHoverText(c);
        var sb = new StringBuilder();
        string statusColor = isRequirementMet ? "green" : "red";

        // Helper local to append current/required lines
        void AppendCurrentRequired(int current)
        {
            string symbol = GetComparisonSymbol(c.comparison);
            sb.AppendLine($"Required: {symbol} {c.requiredValue}");
            sb.Append($"Current: <color={statusColor}>{current}</color>");
        }

        switch (c.type)
        {
            case EventCondition.ConditionType.TechnologyCheck:
            {
                bool unlocked = IsTechnologyUnlocked(c.targetName);
                string techStatus = unlocked ? "<color=green>Unlocked</color>" : "<color=red>Locked</color>";
                sb.Append($"Status: {techStatus}");
                break;
            }
            case EventCondition.ConditionType.ScoreCheck:
            {
                int current = EventSystemLogic.Instance?.GetEventScore(c.targetName) ?? 0;
                AppendCurrentRequired(current);
                break;
            }
            case EventCondition.ConditionType.StatCheck:
            {
                int current = 0;
                var sm = EventSystemLogic.Instance?.GetStatManager();
                if (sm != null) current = sm.GetStatValue(c.targetName);
                AppendCurrentRequired(current);
                break;
            }
            case EventCondition.ConditionType.ResourceCheck:
            {
                int current = EventSystemLogic.Instance?.GetGameUnitsLogic()?.GetResourceAmount(c.targetName) ?? 0;
                AppendCurrentRequired(current);
                break;
            }
            case EventCondition.ConditionType.SeventhCheck:
            case EventCondition.ConditionType.PhaseCheck:
            case EventCondition.ConditionType.EchoCheck:
            case EventCondition.ConditionType.CycleCheck:
            {
                var ts = EventSystemLogic.Instance?.GetTimeSystem();
                int current = 0;
                if (ts != null)
                {
                    if (c.type == EventCondition.ConditionType.SeventhCheck) current = ts.CurrentSeventh;
                    else if (c.type == EventCondition.ConditionType.PhaseCheck) current = ts.CurrentPhase;
                    else if (c.type == EventCondition.ConditionType.EchoCheck) current = ts.CurrentEcho;
                    else if (c.type == EventCondition.ConditionType.CycleCheck) current = ts.CurrentCycle;
                }
                AppendCurrentRequired(current);
                break;
            }
            case EventCondition.ConditionType.RitualSeventhCheck:
            {
                var ts = EventSystemLogic.Instance?.GetTimeSystem();
                int current = ts != null ? ts.CurrentSeventh : 0;
                sb.AppendLine("Required: Seventh = 21 (Ritual)");
                sb.Append($"Current Seventh: <color={statusColor}>{current}</color>");
                break;
            }
            case EventCondition.ConditionType.PopulationCheck:
            case EventCondition.ConditionType.HousingCheck:
            case EventCondition.ConditionType.VagrantsCheck:
            case EventCondition.ConditionType.DeathsCheck:
            case EventCondition.ConditionType.VagrantDeathsCheck:
            case EventCondition.ConditionType.TrueDeathsCheck:
            {
                int current = 0;
                if (PopGrowthLogic.Instance != null)
                {
                    switch (c.type)
                    {
                        case EventCondition.ConditionType.PopulationCheck: current = PopGrowthLogic.Instance.population; break;
                        case EventCondition.ConditionType.HousingCheck: current = PopGrowthLogic.Instance.housing; break;
                        case EventCondition.ConditionType.VagrantsCheck: current = PopGrowthLogic.Instance.vagrants; break;
                        case EventCondition.ConditionType.DeathsCheck: current = PopGrowthLogic.Instance.deaths; break;
                        case EventCondition.ConditionType.VagrantDeathsCheck: current = PopGrowthLogic.Instance.vagrantDeaths; break;
                        case EventCondition.ConditionType.TrueDeathsCheck: current = PopGrowthLogic.Instance.trueDeaths; break;
                    }
                }
                AppendCurrentRequired(current);
                break;
            }
            default:
            {
                sb.Append("Requirement details unavailable.");
                break;
            }
        }

        // Status line
        sb.AppendLine();
        sb.Append($"Status: <b><color={statusColor}>{(isRequirementMet ? "Met" : "Not Met")}</color></b>");

        description = sb.ToString();
    }

    private bool IsTechnologyUnlocked(string technologyName)
    {
        GameUnitsLogic gul = EventSystemLogic.Instance?.GetGameUnitsLogic();
        if (gul != null && gul.researchTab != null)
        {
            GameObject techSlotObj = gul.researchTab.slots.Find(slot => slot.name == technologyName);
            if (techSlotObj != null)
            {
                GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                return techSlot != null && techSlot.isUnlocked;
            }
        }
        return false;
    }

    private string GetComparisonPhrase(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return "Exactly";
            case ComparisonOperator.NotEquals: return "Not Equal To";
            case ComparisonOperator.GreaterThan: return "More Than";
            case ComparisonOperator.GreaterThanOrEqual: return "At Least";
            case ComparisonOperator.LessThan: return "Less Than";
            case ComparisonOperator.LessThanOrEqual: return "At Most";
            default: return "";
        }
    }

    private string Humanize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Unknown";
        string s = raw.Replace('_', ' ').Trim();
        if (string.IsNullOrEmpty(s)) return raw; // Return original if nothing after trim
        
        // Title-case words
        var parts = s.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            parts[i] = char.ToUpper(parts[i][0]) + (parts[i].Length > 1 ? parts[i].Substring(1) : "");
        }
        return string.Join(" ", parts);
    }
    
    /// <summary>
    /// Check if the requirement is currently met
    /// </summary>
    public bool IsRequirementMet()
    {
        return isRequirementMet;
    }
    
    /// <summary>
    /// Get the underlying event condition
    /// </summary>
    public EventCondition GetCondition()
    {
        return condition;
    }
    
    /// <summary>
    /// Refresh the requirement status (called when game state changes)
    /// </summary>
    public void RefreshRequirementStatus()
    {
        CheckRequirementStatus();
        UpdatePanelColor();
        // Also refresh tooltip details to reflect current values
        var trigger = GetComponent<TooltipTrigger>();
        if (trigger != null && condition != null)
        {
            BuildRequirementTooltip(condition, out string title, out string desc);
            trigger.customTitle = title;
            trigger.customDescription = desc;
            trigger.customType = string.Empty;
        }
    }
} 