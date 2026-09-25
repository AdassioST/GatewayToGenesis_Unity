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

    // Council seats authored before councilPosition existed stored these at the top level. They are read
    // here and moved into councilPosition on load; saving the asset persists the migrated form.
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("councilPositionName")]
    private string legacyCouncilPositionName;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("allowAnyLegendClass")]
    private bool legacyAllowAnyLegendClass;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("allowedLegendClasses")]
    private LegendClass[] legacyAllowedLegendClasses;

    private void OnEnable() => MigrateLegacyCouncilPosition();

    private void OnValidate() => MigrateLegacyCouncilPosition();

    private void MigrateLegacyCouncilPosition()
    {
        bool hasLegacyData = !string.IsNullOrEmpty(legacyCouncilPositionName) || legacyAllowAnyLegendClass
                             || (legacyAllowedLegendClasses != null && legacyAllowedLegendClasses.Length > 0);
        if (!hasLegacyData) return;

        if (councilPosition == null) councilPosition = new CivicCouncilPosition();
        if (string.IsNullOrEmpty(councilPosition.title)) councilPosition.title = legacyCouncilPositionName;
        bool hasClasses = councilPosition.allowAnyLegendClass || (councilPosition.allowedClasses != null && councilPosition.allowedClasses.Length > 0);
        if (!hasClasses)
        {
            councilPosition.allowAnyLegendClass = legacyAllowAnyLegendClass;
            councilPosition.allowedClasses = legacyAllowedLegendClasses;
        }
        legacyCouncilPositionName = null;
        legacyAllowAnyLegendClass = false;
        legacyAllowedLegendClasses = null;
    }
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

    /// <summary>Player-facing wording (<see cref="GameEffect.Describe"/>). Checked at start-up by ContentValidator.</summary>
    public string GetAutoDescription() => this.ToEffect().Describe();
}





/// <summary>
/// A requirement for unlocking a civic. It has no rules of its own: <see cref="ToCondition"/> turns it into the
/// same <see cref="EventCondition"/> an event would use, so it is evaluated, worded (<see cref="EventText"/>) and
/// validated (<see cref="EventContentCheck"/>) exactly like event requirements.
/// </summary>
[System.Serializable]
public class CivicRequirement
{
    [Header("Requirement Configuration")]
    public RequirementType requirementType;
    public string requirementTarget; // Stat name, government type, or civic name
    [Tooltip("Compared as a whole number, like event requirements.")]
    public float requiredValue; // Required value
    public ComparisonType comparison; // How to compare the values
    
    /// <summary>
    /// The equivalent event condition. Null for <see cref="RequirementType.EraUnlock"/>: eras do not exist yet, so
    /// that requirement always passes (ContentValidator reports it).
    /// </summary>
    public EventCondition ToCondition()
    {
        var op = GameValues.ToOperator(comparison);
        int value = Mathf.RoundToInt(requiredValue);
        switch (requirementType)
        {
            case RequirementType.PillarStat:
            case RequirementType.SubstatStat:
                return new EventCondition { type = EventCondition.ConditionType.StatCheck, targetName = requirementTarget, requiredValue = value, comparison = op };
            case RequirementType.GovernmentType:
                return Value("government", requirementTarget, 1, ComparisonOperator.GreaterThanOrEqual);
            case RequirementType.CivicPresent:
                return Value("civic", requirementTarget, 1, ComparisonOperator.GreaterThanOrEqual);
            case RequirementType.CivicAbsent:
                return Value("civic", requirementTarget, 0, ComparisonOperator.Equals);
            case RequirementType.SatisfactionLevel:
                return Value("satisfaction", null, value, op);
            case RequirementType.MoraleLevel:
                return Value("morale", null, value, op);
            default:
                return null;
        }
    }
    
    private static EventCondition Value(string domain, string target, int value, ComparisonOperator op) =>
        new EventCondition { type = EventCondition.ConditionType.ValueCheck, domain = domain, targetName = target, requiredValue = value, comparison = op };
    
    /// <summary>True when the requirement holds now.</summary>
    public bool IsMet()
    {
        var condition = ToCondition();
        return condition == null || condition.Evaluate();
    }
    
    /// <summary>Player-facing wording, e.g. "Needs At Least 10 Regalia" (<see cref="EventText.DescribeRequirement"/>).</summary>
    public string GetAutoDescription()
    {
        var condition = ToCondition();
        return condition != null ? EventText.DescribeRequirement(condition) : $"Needs The {EventText.Humanize(requirementTarget)} Era";
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

    /// <summary>Player-facing wording (<see cref="SeatBonus.Describe"/>, the same rule as default seats).</summary>
    public string GetAutoDescription() => SeatBonus.Describe(bonusType, this.ToEffect());
}



 