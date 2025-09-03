using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Civic Data", menuName = "Game Object/Civic Data", order = 5)]
public class CivicData : ScriptableObject
{
    [Header("Basic Information")]
    public string civicName;
    [TextArea(3, 6)]
    public string description; // Flavor text for daily life/immersion
    public Sprite icon;

    [Header("Civic Classification")]
    public GameRarity rarity;
    public CivicTier tier;
    
    [Header("Council Integration")]
    public bool grantsCouncilPosition;
    public CivicCouncilPosition councilPosition; // Complete council position configuration

    [Header("Effects")]
    public List<CivicEffect> effects = new List<CivicEffect>();
    
    [Header("Requirements")]
    public List<CivicRequirement> requirements = new List<CivicRequirement>();
    public List<string> conflictingCivics = new List<string>(); // Civics that cannot coexist
    public List<string> requiredCivics = new List<string>(); // Civics that must be present
    
    /// <summary>
    /// Validate that this civic has proper legend class restrictions configured
    /// </summary>
    public (bool isValid, string errorMessage) ValidateLegendClassRestrictions()
    {
        if (!grantsCouncilPosition) return (true, ""); // Not a council position, no validation needed
        
        if (councilPosition == null)
        {
            return (false, $"Civic '{civicName}' grants a council position but has no councilPosition configuration defined.");
        }
        
        return councilPosition.ValidateLegendClassRestrictions();
    }
    
    [Header("Removal Penalties")]
    [Tooltip("Satisfaction points penalty when removing this civic")]
    public int satisfactionPenalty = 0;
    [Tooltip("Morale penalty per seventh when removing this civic")]
    public int moralePenaltyPerSeventh = 0;
    [Tooltip("Duration in sevenths for removal penalties")]
    public int removalPenaltyDuration = 0;
}



/// <summary>
/// Civic effect that modifies game systems
/// </summary>
[System.Serializable]
public class CivicEffect
{
    [Header("Effect Configuration")]
    public GameEffectType effectType;
    public float modifierValue; // Value to add/multiply
    public ModifierType modifierType; // How the modifier is applied
    
    [Header("Target Configuration")]
    [Tooltip("What stat/resource to modify (e.g., 'waltz', 'food', 'building')")]
    public string targetStat; // Stat name to modify (when needed)
    
    [Header("Condition Configuration")]
    [Tooltip("What stat triggers this bonus (for scaling effects, e.g., 'housing' for +0.1 food per housing)")]
    public string conditionStat; // What stat triggers the bonus (for scaling effects)
    
    [Header("Scope Configuration")]
    [Tooltip("Scope of application: Individual (single item), Section (group of similar items), or Global (everything)")]
    public ScopeType scope = ScopeType.Individual; // Scope of application
    
    /// <summary>
    /// Get the automatically generated description for this effect
    /// </summary>
    public string GetAutoDescription()
    {
        return GenerateEffectDescription();
    }
    
    /// <summary>
    /// Generate automatic description based on effect type and fields
    /// </summary>
    private string GenerateEffectDescription()
    {
        switch (effectType)
        {
            case GameEffectType.PillarBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string pillarSign = modifierValue > 0 ? "+" : "";
                    return $"{pillarSign}{modifierValue} {targetStat}";
                }
                return $"{modifierValue} Pillar Bonus";
                
            case GameEffectType.SubstatBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string substatSign = modifierValue > 0 ? "+" : "";
                    return $"{substatSign}{modifierValue} {targetStat}";
                }
                return $"{modifierValue} Substat Bonus";
                
            case GameEffectType.DerivedStatBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string derivedSign = modifierValue > 0 ? "+" : "";
                    string derivedUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{derivedSign}{modifierValue}{derivedUnit} {targetStat}";
                }
                return $"{modifierValue} Derived Stat Bonus";
                
            case GameEffectType.ResourceModifier:
                if (scope == ScopeType.Global)
                {
                    string resourceSign = modifierValue > 0 ? "+" : "";
                    string resourceUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{resourceSign}{modifierValue}{resourceUnit} all resources production";
                }
                else if (scope == ScopeType.Section && !string.IsNullOrEmpty(targetStat))
                {
                    string resourceSign = modifierValue > 0 ? "+" : "";
                    string resourceUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{resourceSign}{modifierValue}{resourceUnit} {targetStat} section production";
                }
                else if (!string.IsNullOrEmpty(targetStat))
                {
                    string resourceSign = modifierValue > 0 ? "+" : "";
                    string resourceUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{resourceSign}{modifierValue}{resourceUnit} {targetStat} production";
                }
                return $"{modifierValue} Resource Production";
                
            case GameEffectType.ProductionModifier:
                if (scope == ScopeType.Global)
                {
                    string productionSign = modifierValue > 0 ? "+" : "";
                    string productionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{productionSign}{modifierValue}{productionUnit} all buildings efficiency";
                }
                else if (scope == ScopeType.Section && !string.IsNullOrEmpty(targetStat))
                {
                    string productionSign = modifierValue > 0 ? "+" : "";
                    string productionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{productionSign}{modifierValue}{productionUnit} {targetStat} section efficiency";
                }
                else if (!string.IsNullOrEmpty(targetStat))
                {
                    string productionSign = modifierValue > 0 ? "+" : "";
                    string productionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{productionSign}{modifierValue}{productionUnit} {targetStat} efficiency";
                }
                return $"{modifierValue} Production Efficiency";
                
            case GameEffectType.ClickPowerBonus:
                if (scope == ScopeType.Global)
                {
                    string clickSign = modifierValue > 0 ? "+" : "";
                    string clickUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{clickSign}{modifierValue}{clickUnit} all resources click power";
                }
                else if (scope == ScopeType.Section && !string.IsNullOrEmpty(targetStat))
                {
                    string clickSign = modifierValue > 0 ? "+" : "";
                    string clickUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{clickSign}{modifierValue}{clickUnit} {targetStat} section click power";
                }
                else if (!string.IsNullOrEmpty(targetStat))
                {
                    string clickSign = modifierValue > 0 ? "+" : "";
                    string clickUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{clickSign}{modifierValue}{clickUnit} {targetStat} click power";
                }
                return $"{modifierValue} Click Power";
                
            case GameEffectType.MaxMoraleModifier:
                string moraleSign = modifierValue > 0 ? "+" : "";
                return $"{moraleSign}{modifierValue} max morale";
                
            case GameEffectType.MoraleBalanceModifier:
                return $"Morale balance -{modifierValue} (easier to stay positive)";
                
            case GameEffectType.SatisfactionThresholdModifier:
                string satisfactionSign = modifierValue > 0 ? "+" : "";
                return $"{satisfactionSign}{modifierValue} satisfaction threshold (easier upgrades)";
                
            case GameEffectType.HousingBonus:
                string housingSign = modifierValue > 0 ? "+" : "";
                string housingUnit = modifierType == ModifierType.Percentage ? "%" : "";
                return $"{housingSign}{modifierValue}{housingUnit} housing capacity";
                
            case GameEffectType.ConstructionCostModifier:
                if (scope == ScopeType.Global)
                {
                    string constructionSign = modifierValue > 0 ? "+" : "";
                    string constructionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{constructionSign}{modifierValue}{constructionUnit} all buildings construction cost";
                }
                else if (scope == ScopeType.Section && !string.IsNullOrEmpty(targetStat))
                {
                    string constructionSign = modifierValue > 0 ? "+" : "";
                    string constructionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{constructionSign}{modifierValue}{constructionUnit} {targetStat} section construction cost";
                }
                else if (!string.IsNullOrEmpty(targetStat))
                {
                    string constructionSign = modifierValue > 0 ? "+" : "";
                    string constructionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{constructionSign}{modifierValue}{constructionUnit} {targetStat} construction cost";
                }
                return $"{modifierValue} Construction Cost Modifier";
                
            case GameEffectType.ProductionScalingBonus:
                if (!string.IsNullOrEmpty(targetStat) && !string.IsNullOrEmpty(conditionStat))
                {
                    string scalingSign = modifierValue > 0 ? "+" : "";
                    return $"{scalingSign}{modifierValue} {targetStat} per {conditionStat}";
                }
                else if (!string.IsNullOrEmpty(targetStat))
                {
                    string scalingSign = modifierValue > 0 ? "+" : "";
                    return $"{scalingSign}{modifierValue} {targetStat} per production unit";
                }
                return $"{modifierValue} Production Scaling Bonus";
                
            case GameEffectType.SpecialAbility:
                return "Special Ability";
                
            default:
                return "Unknown Effect";
        }
    }
    
    /// <summary>
    /// Validate that this effect has all required fields for its type
    /// </summary>
    public (bool isValid, string errorMessage) ValidateEffect()
    {
        switch (effectType)
        {
            case GameEffectType.PillarBonus:
            case GameEffectType.SubstatBonus:
            case GameEffectType.DerivedStatBonus:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{effectType} requires a targetStat field");
                }
                break;
                
            case GameEffectType.ResourceModifier:
            case GameEffectType.ProductionModifier:
            case GameEffectType.ClickPowerBonus:
            case GameEffectType.ConstructionCostModifier:
                // These can work with either targetStat OR scope
                if (string.IsNullOrEmpty(targetStat) && scope == ScopeType.Individual)
                {
                    return (false, $"{effectType} requires either targetStat field or scope set to Section/Global");
                }
                break;
                
            case GameEffectType.ProductionScalingBonus:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{effectType} requires a targetStat field (what is produced)");
                }
                break;
        }
        
        return (true, "");
    }
}





/// <summary>
/// Requirements for unlocking a civic
/// </summary>
[System.Serializable]
public class CivicRequirement
{
    [Header("Requirement Configuration")]
    public RequirementType requirementType;
    public string requirementTarget; // Stat name, government type, or civic name
    public float requiredValue; // Required value
    public ComparisonType comparison; // How to compare the values
    
    /// <summary>
    /// Get the automatically generated description for this requirement
    /// </summary>
    public string GetAutoDescription()
    {
        return GenerateRequirementDescription();
    }
    
    /// <summary>
    /// Generate automatic description based on requirement type and fields
    /// </summary>
    private string GenerateRequirementDescription()
    {
        string comparisonText = GetComparisonText();
        
        switch (requirementType)
        {
            case RequirementType.PillarStat:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"{requirementTarget} {comparisonText} {requiredValue}";
                }
                return $"Pillar stat {comparisonText} {requiredValue}";
                
            case RequirementType.SubstatStat:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"{requirementTarget} {comparisonText} {requiredValue}";
                }
                return $"Substat {comparisonText} {requiredValue}";
                
            case RequirementType.GovernmentType:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"Government type: {requirementTarget}";
                }
                return "Specific government type required";
                
            case RequirementType.CivicPresent:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"Requires civic: {requirementTarget}";
                }
                return "Requires specific civic";
                
            case RequirementType.CivicAbsent:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"Cannot have civic: {requirementTarget}";
                }
                return "Cannot have specific civic";
                
            case RequirementType.EraUnlock:
                if (!string.IsNullOrEmpty(requirementTarget))
                {
                    return $"Requires era: {requirementTarget}";
                }
                return "Requires specific era";
                
            case RequirementType.SatisfactionLevel:
                return $"Satisfaction level {comparisonText} {requiredValue}";
                
            case RequirementType.MoraleLevel:
                return $"Morale level {comparisonText} {requiredValue}";
                
            default:
                return "Unknown requirement";
        }
    }
    
    /// <summary>
    /// Get human-readable comparison text
    /// </summary>
    private string GetComparisonText()
    {
        switch (comparison)
        {
            case ComparisonType.GreaterThan: return ">";
            case ComparisonType.GreaterEqual: return ">=";
            case ComparisonType.Equal: return "=";
            case ComparisonType.LessEqual: return "<=";
            case ComparisonType.LessThan: return "<";
            case ComparisonType.NotEqual: return "!=";
            default: return "?";
        }
    }
    
    /// <summary>
    /// Validate that this requirement has all required fields for its type
    /// </summary>
    public (bool isValid, string errorMessage) ValidateRequirement()
    {
        switch (requirementType)
        {
            case RequirementType.PillarStat:
            case RequirementType.SubstatStat:
                if (string.IsNullOrEmpty(requirementTarget))
                {
                    return (false, $"{requirementType} requires a requirementTarget field");
                }
                break;
                
            case RequirementType.GovernmentType:
            case RequirementType.CivicPresent:
            case RequirementType.CivicAbsent:
            case RequirementType.EraUnlock:
                if (string.IsNullOrEmpty(requirementTarget))
                {
                    return (false, $"{requirementType} requires a requirementTarget field");
                }
                break;
        }
        
        return (true, "");
    }
}

/// <summary>
/// Configuration for council positions granted by civics
/// Follows the same structure as DefaultSeatTemplate for consistency
/// </summary>
[System.Serializable]
public class CivicCouncilPosition
{
    [Header("Basic Information")]
    public string title;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    
    [Header("Legend Class Restrictions")]
    [Tooltip("Check this if ANY legend class can fill this position (overrides specific class restrictions)")]
    public bool allowAnyLegendClass = false;
    [Tooltip("Specific legend classes that can fill this council position (ignored if allowAnyLegendClass is true)")]
    public LegendClass[] allowedClasses;
    
    [Header("Position Bonuses")]
    public CivicSeatBonus[] bonuses;
    
    /// <summary>
    /// Validate that this council position has proper legend class restrictions configured
    /// </summary>
    public (bool isValid, string errorMessage) ValidateLegendClassRestrictions()
    {
        if (allowAnyLegendClass)
        {
            // If allowing any class, specific classes are ignored (this is fine)
            return (true, "");
        }
        
        if (allowedClasses == null || allowedClasses.Length == 0)
        {
            return (false, $"Council position '{title}' has no legend class restrictions defined. Either set allowAnyLegendClass to true or specify allowedClasses.");
        }
        
        return (true, "");
    }
    
    /// <summary>
    /// Get the automatically generated description for this council position
    /// </summary>
    public string GetAutoDescription()
    {
        if (!string.IsNullOrEmpty(description))
        {
            return description;
        }
        
        // Generate description from bonuses if no custom description provided
        if (bonuses != null && bonuses.Length > 0)
        {
            var bonusDescriptions = new List<string>();
            foreach (var bonus in bonuses)
            {
                if (bonus != null)
                {
                    bonusDescriptions.Add(bonus.GetAutoDescription());
                }
            }
            return string.Join(", ", bonusDescriptions);
        }
        
        return "Council position with special effects";
    }
}

/// <summary>
/// Configuration for civic council position bonuses
/// Follows the same structure as DefaultSeatBonus for consistency
/// </summary>
[System.Serializable]
public class CivicSeatBonus
{
    public SeatBonusType bonusType;
    [Tooltip("Target stat for pillar/substat/derived bonuses (e.g., 'waltz', 'legendEffectiveness')")]
    public string targetStat;
    public float modifierValue;
    public ModifierType modifierType;
    public bool requiresLegend = true;
    
    /// <summary>
    /// Get the automatically generated description for this bonus
    /// </summary>
    public string GetAutoDescription()
    {
        return GenerateBonusDescription();
    }
    
    /// <summary>
    /// Generate automatic description based on bonus type and fields
    /// </summary>
    private string GenerateBonusDescription()
    {
        switch (bonusType)
        {
            case SeatBonusType.PillarBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string pillarSign = modifierValue > 0 ? "+" : "";
                    string pillarUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{pillarSign}{modifierValue}{pillarUnit} {targetStat}";
                }
                return $"{modifierValue} Pillar Bonus";
                
            case SeatBonusType.SubstatBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string substatSign = modifierValue > 0 ? "+" : "";
                    string substatUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{substatSign}{modifierValue}{substatUnit} {targetStat}";
                }
                return $"{modifierValue} Substat Bonus";
                
            case SeatBonusType.DerivedStatBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string derivedSign = modifierValue > 0 ? "+" : "";
                    string derivedUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{derivedSign}{modifierValue}{derivedUnit} {targetStat}";
                }
                return $"{modifierValue} Derived Stat Bonus";
                
            case SeatBonusType.ResourceModifier:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string resourceSign = modifierValue > 0 ? "+" : "";
                    string resourceUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{resourceSign}{modifierValue}{resourceUnit} {targetStat} production";
                }
                return $"{modifierValue} Resource Production";
                
            case SeatBonusType.ProductionModifier:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string productionSign = modifierValue > 0 ? "+" : "";
                    string productionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{productionSign}{modifierValue}{productionUnit} {targetStat} efficiency";
                }
                return $"{modifierValue} Production Efficiency";
                
            case SeatBonusType.ClickPowerBonus:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string clickSign = modifierValue > 0 ? "+" : "";
                    string clickUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{clickSign}{modifierValue}{clickUnit} {targetStat} click power";
                }
                return $"{modifierValue} Click Power";
                
            case SeatBonusType.MaxMoraleModifier:
                string moraleSign = modifierValue > 0 ? "+" : "";
                return $"{moraleSign}{modifierValue} max morale";
                
            case SeatBonusType.MoraleBalanceModifier:
                return $"Morale balance -{modifierValue} (easier to stay positive)";
                
            case SeatBonusType.SatisfactionThresholdModifier:
                string satisfactionSign = modifierValue > 0 ? "+" : "";
                return $"{satisfactionSign}{modifierValue} satisfaction threshold (easier upgrades)";
                
            case SeatBonusType.HousingBonus:
                string housingSign = modifierValue > 0 ? "+" : "";
                string housingUnit = modifierType == ModifierType.Percentage ? "%" : "";
                return $"{housingSign}{modifierValue}{housingUnit} housing capacity";
                
            case SeatBonusType.ConstructionCostModifier:
                if (!string.IsNullOrEmpty(targetStat))
                {
                    string constructionSign = modifierValue > 0 ? "+" : "";
                    string constructionUnit = modifierType == ModifierType.Percentage ? "%" : "";
                    return $"{constructionSign}{modifierValue}{constructionUnit} {targetStat} construction cost";
                }
                return $"{modifierValue} Construction Cost Modifier";
                
            case SeatBonusType.SpecialAbility:
                return "Special Ability";
                
            default:
                return "Unknown Bonus";
        }
    }
    
    /// <summary>
    /// Validate that this bonus has all required fields for its type
    /// </summary>
    public (bool isValid, string errorMessage) ValidateBonus()
    {
        switch (bonusType)
        {
            case SeatBonusType.PillarBonus:
            case SeatBonusType.SubstatBonus:
            case SeatBonusType.DerivedStatBonus:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{bonusType} requires a targetStat field");
                }
                break;
                
            case SeatBonusType.ResourceModifier:
            case SeatBonusType.ProductionModifier:
            case SeatBonusType.ClickPowerBonus:
            case SeatBonusType.ConstructionCostModifier:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{bonusType} requires a targetStat field");
                }
                break;
        }
        
        return (true, "");
    }
}



 