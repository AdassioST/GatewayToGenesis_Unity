using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        
        // Check if requirement is met and update status
        CheckRequirementStatus();
        UpdatePanelColor();
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
                // Use existing GameUnits system to get icon for known resources/stats
                if (GameUnitsLogic.Instance != null && !string.IsNullOrEmpty(requirement.targetName))
                {
                    // Manual override for common special resources lacking explicit GameUnit assets
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
    }
} 