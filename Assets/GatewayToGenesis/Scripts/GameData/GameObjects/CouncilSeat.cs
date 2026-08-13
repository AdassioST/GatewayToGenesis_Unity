using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a council seat with its requirements, bonuses, and allowed legend classes
/// </summary>
[System.Serializable]
public class CouncilSeat
{
    [Header("Seat Information")]
    public string seatTitle; // Title of the seat (e.g., "High Arbiter", "Treasurer")
    public int seatIndex; // Position in the council (0-5 for regular seats, -1 for Head of State)
    public bool isUnlocked = false; // Whether this seat is available
    public Sprite seatIcon; // Icon representing this seat
    
    [Header("Legend Requirements")]
    public List<LegendClass> allowedLegendClasses = new List<LegendClass>(); // Which legend classes can fill this seat
    public bool isHeadOfState = false; // Whether this is the Head of State position
    
    [Header("Seat Bonuses")]
    public List<SeatBonus> seatBonuses = new List<SeatBonus>(); // Bonuses provided by the seat itself
    public string roleplayDescription; // Flavor text describing the role
    
    [Header("Assignment")]
    public LegendData assignedLegend; // Currently assigned legend (null if empty)
    public int seventhsUntilActive = 0; // Sevenths remaining until effects activate
    public bool bonusesProcessed = false; // Flag to prevent duplicate bonus processing
    
    [Header("Civic Integration")]
    public CivicData sourceCivic; // Civic that created this seat (null for default seats)
    public string civicSeatTitle; // Title when created by a civic
    
    public CouncilSeat(string title, int index, bool headOfState = false)
    {
        this.seatTitle = title;
        this.seatIndex = index;
        this.isHeadOfState = headOfState;
        this.isUnlocked = false;
        this.assignedLegend = null;
        this.seventhsUntilActive = 0;
        this.sourceCivic = null;
        this.civicSeatTitle = "";
    }
    
    /// <summary>
    /// Check if a legend can be assigned to this seat
    /// </summary>
    public bool CanAssignLegend(LegendData legend)
    {
        if (legend == null) return false;
        
        // Head of State can be any class if allowed
        if (isHeadOfState && legend.canBeHeadOfState) return true;
        
        // Check if legend class is compatible with seat
        return allowedLegendClasses.Contains(legend.legendClass);
    }
    
    /// <summary>
    /// Assign a legend to this seat
    /// </summary>
    public bool AssignLegend(LegendData legend)
    {
        if (!CanAssignLegend(legend)) return false;
        
        assignedLegend = legend;
        bonusesProcessed = false; // Reset bonus processing flag for new assignment
        // seventhsUntilActive will be set by LegendLeaderLogic based on seventhsForActivation
        return true;
    }
    
    /// <summary>
    /// Remove the assigned legend from this seat
    /// </summary>
    public void RemoveLegend()
    {
        assignedLegend = null;
        seventhsUntilActive = 0;
        bonusesProcessed = false; // Reset bonus processing flag
    }
    
    /// <summary>
    /// Get the effective title for this seat (civic-based or default)
    /// </summary>
    public string GetEffectiveTitle()
    {
        if (sourceCivic != null && !string.IsNullOrEmpty(civicSeatTitle))
        {
            return civicSeatTitle;
        }
        return seatTitle;
    }
    
    /// <summary>
    /// Check if this seat is active (has assigned legend and sevenths completed)
    /// </summary>
    public bool IsActive()
    {
        // Must have an assigned legend AND the activation timer must be complete
        return assignedLegend != null && seventhsUntilActive <= 0;
    }
    
    /// <summary>
    /// Process one seventh (decrease activation timer)
    /// </summary>
    public void ProcessSeventh()
    {
        if (seventhsUntilActive > 0)
        {
            seventhsUntilActive--;
        }
    }
}

/// <summary>
/// Bonus provided by a council seat itself
/// </summary>
[System.Serializable]
public class SeatBonus
{
    public SeatBonusType bonusType;
    public string targetStat; // Stat name to modify
    public float modifierValue; // Value to add/multiply
    public ModifierType modifierType;
    public string description; // Human-readable description of the bonus
    public bool requiresLegend = true; // Whether this bonus requires an assigned legend

    /// <summary>
    /// Get auto-generated description for this bonus (matching legend/civic format)
    /// </summary>
    public string GetAutoDescription()
    {
        if (!string.IsNullOrEmpty(description))
        {
            return description;
        }

        return bonusType switch
        {
            SeatBonusType.PillarBonus => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue} {targetStat}"
                : $"{modifierValue} Pillar Bonus",
                
            SeatBonusType.SubstatBonus => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue} {targetStat}"
                : $"{modifierValue} Substat Bonus",
                
            SeatBonusType.DerivedStatBonus => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} {targetStat}"
                : $"{modifierValue} Derived Stat Bonus",
                
            SeatBonusType.ResourceModifier => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} {targetStat} production"
                : $"{modifierValue} Resource Production",
                
            SeatBonusType.ProductionModifier => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} {targetStat} efficiency"
                : $"{modifierValue} Production Efficiency",
                
            SeatBonusType.ClickPowerBonus => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} {targetStat} click power"
                : $"{modifierValue} Click Power",
                
            SeatBonusType.MaxMoraleModifier => $"{(modifierValue > 0 ? "+" : "")}{modifierValue} max morale",
            
            SeatBonusType.MoraleModifier => $"{(modifierValue > 0 ? "+" : "")}{modifierValue} morale",
            
            SeatBonusType.MoraleBalanceModifier => $"Morale balance -{modifierValue} (easier to stay positive)",
            
            SeatBonusType.SatisfactionModifier => $"{(modifierValue > 0 ? "+" : "")}{modifierValue} satisfaction effectiveness",
            
            SeatBonusType.SatisfactionThresholdModifier => $"{(modifierValue > 0 ? "+" : "")}{modifierValue} satisfaction threshold (easier upgrades)",
            
            SeatBonusType.HousingBonus => $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} housing capacity",
            
            SeatBonusType.ProductionScalingBonus => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue} {targetStat} per production unit"
                : $"{modifierValue} Production Scaling Bonus",
                
            SeatBonusType.ConstructionCostModifier => !string.IsNullOrEmpty(targetStat) 
                ? $"{(modifierValue > 0 ? "+" : "")}{modifierValue}{(modifierType == ModifierType.Percentage ? "%" : "")} {targetStat} construction cost"
                : $"{modifierValue} Construction Cost Modifier",
                
            SeatBonusType.SpecialAbility => "Special Ability",
            SeatBonusType.CivicBonus => "Civic Bonus",
            _ => "Unknown Bonus"
        };
    }
}

/// <summary>
/// Types of bonuses council seats can provide
/// </summary>
public enum SeatBonusType
{
    PillarBonus,           // Bonus to pillar stats
    SubstatBonus,          // Bonus to substats
    DerivedStatBonus,      // Bonus to derived stats
    ResourceModifier,      // Resource production/consumption modifier
    ProductionModifier,    // Building/unit production modifier
    ClickPowerBonus,       // Click power bonus
    MaxMoraleModifier,     // Increases max morale (more room for positive morale)
    MoraleModifier,        // Direct morale bonus (adds to current morale)
    MoraleBalanceModifier, // Decreases morale balance (easier to stay above balance)
    SatisfactionModifier,  // Satisfaction effectiveness modifier (legacy - use SatisfactionThresholdModifier)
    SatisfactionThresholdModifier, // Satisfaction threshold modifier (makes upgrades easier)
    HousingBonus,          // Housing capacity bonus (persistent, cannot be destroyed)
    ProductionScalingBonus, // Bonus production per production unit (e.g., +1 food per Decaying Hut)
    ConstructionCostModifier, // Building cost reduction by section/type
    SpecialAbility,         // Unique effects not covered by other types
    CivicBonus             // Bonus specifically from civic integration
} 