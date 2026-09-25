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

    /// <summary>Player-facing wording (<see cref="GameEffect.Describe"/>). Checked at start-up by ContentValidator.</summary>
    public string GetAutoDescription() => this.ToEffect().Describe();
}



 