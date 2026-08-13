using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Legend Data", menuName = "Game Object/Legend Data", order = 6)]
public class LegendData : ScriptableObject
{
    [Header("Basic Information")]
    public string legendName;
    [TextArea(3, 6)]
    public string originStory; // Background story and how they came to power
    public Sprite portrait;
    
    [Header("Legend Classification")]
    public LegendClass legendClass;
    public GameRarity rarity;
    
    [Header("Mechanical Bonuses")]
    public List<LegendBonus> bonuses = new List<LegendBonus>();
    
    [Header("Council Assignment")]
    public bool canBeHeadOfState = false; // Whether this legend can serve as Head of State
    public List<LegendClass> compatibleSeatClasses = new List<LegendClass>(); // Which seat classes this legend can fill
    
    [Header("Flavor Text")]
    [TextArea(2, 4)]
    public string councilAssignmentDescription; // Description when assigned to council
    public string personalQuote; // Famous quote or motto
}



/// <summary>
/// Legend bonus that modifies game systems
/// </summary>
[System.Serializable]
public class LegendBonus
{
    [Header("Effect Configuration")]
    public GameEffectType bonusType;
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
            case GameEffectType.PillarBonus:
            case GameEffectType.SubstatBonus:
            case GameEffectType.DerivedStatBonus:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{bonusType} requires a targetStat field");
                }
                break;
                
            case GameEffectType.ResourceModifier:
            case GameEffectType.ProductionModifier:
            case GameEffectType.ClickPowerBonus:
            case GameEffectType.ConstructionCostModifier:
                // These can work with either targetStat OR scope
                if (string.IsNullOrEmpty(targetStat) && scope == ScopeType.Individual)
                {
                    return (false, $"{bonusType} requires either targetStat field or scope set to Section/Global");
                }
                break;
                
            case GameEffectType.ProductionScalingBonus:
                if (string.IsNullOrEmpty(targetStat))
                {
                    return (false, $"{bonusType} requires a targetStat field (what is produced)");
                }
                if (string.IsNullOrEmpty(conditionStat))
                {
                    return (false, $"{bonusType} requires a conditionStat field (what triggers the bonus)");
                }
                break;
        }
        
        return (true, "");
    }
}



 