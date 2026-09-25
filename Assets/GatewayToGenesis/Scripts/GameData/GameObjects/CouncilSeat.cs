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
    /// The one eligibility rule for seating a legend: the Head of State takes any legend marked
    /// canBeHeadOfState; every other seat takes the classes it allows.
    /// </summary>
    public bool CanAssignLegend(LegendData legend)
    {
        if (legend == null) return false;
        if (isHeadOfState) return legend.canBeHeadOfState;
        return allowedLegendClasses.Contains(legend.legendClass);
    }
    
    /// <summary>
    /// Assign a legend to this seat
    /// </summary>
    public bool AssignLegend(LegendData legend)
    {
        if (!CanAssignLegend(legend)) return false;
        
        assignedLegend = legend;
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
    
    /// <summary>True once the seated legend has finished activating and its own bonuses apply (the seat's bonuses apply from the moment it is seated).</summary>
    public bool IsActive() => CouncilRules.LegendBonusesApply(assignedLegend != null, seventhsUntilActive);
    
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

    /// <summary>The authored <see cref="description"/> if there is one, otherwise the effect's own wording.</summary>
    public string GetAutoDescription() => !string.IsNullOrEmpty(description) ? description : Describe(bonusType, this.ToEffect());

    /// <summary>
    /// Wording of a seat bonus (default seats and civic seats): <see cref="GameEffect.Describe"/>, except the
    /// CivicBonus marker, which has no effect of its own.
    /// </summary>
    public static string Describe(SeatBonusType type, in GameEffect effect) => type == SeatBonusType.CivicBonus ? "Civic Bonus" : effect.Describe();
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