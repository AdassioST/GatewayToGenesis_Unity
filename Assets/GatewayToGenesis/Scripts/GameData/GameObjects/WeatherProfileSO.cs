using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Available echo types in the game
/// </summary>
public enum EchoType
{
    Resonance,   // Echo of Resonance
    Crescendo,   // Echo of Crescendo
    Dissonance,  // Echo of Dissonance (keeping original spelling from assets)
    Silence      // Echo of Silence
}

/// <summary>
/// Simplified condition system for weather availability (subset of EventCondition)
/// </summary>
[System.Serializable]
public class WeatherCondition
{
    public enum ConditionType
    {
        CycleCheck,           // Check current cycle number
        TechnologyCheck,      // Check if technology is unlocked
        EventScoreCheck       // Check event score value (for events witnessed)
    }
    
    public ConditionType type;
    public string targetName;     // Technology name, score name, etc.
    public int requiredValue;     // Required value for checks
    public ComparisonOperator comparison = ComparisonOperator.GreaterThanOrEqual;
    
    /// <summary>
    /// Evaluate this weather condition
    /// </summary>
    public bool Evaluate()
    {
        switch (type)
        {
            case ConditionType.CycleCheck:
                TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
                if (timeSystem != null)
                {
                    return CompareValues(timeSystem.CurrentCycle, requiredValue, comparison);
                }
                return false;
                
            case ConditionType.TechnologyCheck:
                return CheckTechnologyUnlocked(targetName);
                
            case ConditionType.EventScoreCheck:
                int score = EventSystemLogic.Instance?.GetEventScore(targetName) ?? 0;
                return CompareValues(score, requiredValue, comparison);
                
                
            default:
                return false;
        }
    }
    
    private bool CompareValues(int actual, int expected, ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equals: return actual == expected;
            case ComparisonOperator.NotEquals: return actual != expected;
            case ComparisonOperator.GreaterThan: return actual > expected;
            case ComparisonOperator.LessThan: return actual < expected;
            case ComparisonOperator.GreaterThanOrEqual: return actual >= expected;
            case ComparisonOperator.LessThanOrEqual: return actual <= expected;
            default: return false;
        }
    }
    
    private bool CheckTechnologyUnlocked(string technologyName)
    {
        GameUnitsLogic gameUnitsLogic = GameUnitsLogic.Instance;
        if (gameUnitsLogic != null && gameUnitsLogic.researchTab != null)
        {
            GameObject techSlotObj = gameUnitsLogic.researchTab.slots.Find(slot => slot.name == technologyName);
            if (techSlotObj != null)
            {
                GameTechnologySlot techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
                return techSlot != null && techSlot.isUnlocked;
            }
        }
        return false;
    }
}

/// <summary>
/// Scriptable Object defining all weather and celestial parameters as curves over a 0-100% cycle
/// Extended with gameplay effects on resources, production, and morale
/// </summary>
[CreateAssetMenu(fileName = "New Weather Profile", menuName = "Environment/Weather Profile", order = 1)]
public class WeatherProfileSO : ScriptableObject
{
    #region Nested Structures
    /// <summary>
    /// Weather effect that modifies game systems (consistent with CivicEffect/LegendBonus)
    /// </summary>
    [Serializable]
    public class WeatherEffect
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
    #endregion
    
    #region Weather Identity
    [Header("Weather Identity")]
    [Tooltip("Display name for this weather type")]
    public string weatherDisplayName = "Clear Weather";
    
    [Tooltip("Description of this weather's effects")]
    [TextArea(2, 4)]
    public string weatherDescription = "Standard weather conditions.";
    
    [Header("Procedural Weather Settings")]
    [Tooltip("Relative weight for random selection (0-100). Higher = more likely to be selected. 0 = never selected procedurally.")]
    [Range(0f, 100f)]
    public float triggerWeight = 50f;
    
    [Tooltip("Chance (%) this weather persists when already active (0-100%). High values = stable weather.")]
    [Range(0f, 100f)]
    public float retentionProbability = 90f;
    
    [Tooltip("If true, this is the default/baseline weather and always available for procedural selection")]
    public bool isDefaultWeather = false;
    
    [Header("Celestial Phase Duration Percentages - DIRECT CONTROL: Each phase duration as percentage of seventh. Must sum to 100%. System auto-normalizes if needed.")]
    [Space(5)]
    [Tooltip("Dawn duration as percentage of seventh (0-100%) - Golden hour phase")]
    [Range(0f, 100f)]
    public float dawnDurationPercentage = 20f;
    
    [Tooltip("Midday duration as percentage of seventh (0-100%) - Bright day phase")]
    [Range(0f, 100f)]
    public float middayDurationPercentage = 30f;
    
    [Tooltip("Dusk duration as percentage of seventh (0-100%) - Sunset phase")]
    [Range(0f, 100f)]
    public float duskDurationPercentage = 20f;
    
    [Tooltip("Night duration as percentage of seventh (0-100%) - Dark night phase")]
    [Range(0f, 100f)]
    public float nightDurationPercentage = 20f;
    
    [Tooltip("Post-Midnight duration as percentage of seventh (0-100%) - Late night phase")]
    [Range(0f, 100f)]
    public float postMidnightDurationPercentage = 10f;
    
    [Header("Availability Conditions")]
    [Tooltip("ALL conditions must be met for this weather to be available. Use TechnologyCheck for unlocks, EventScoreCheck for narrative triggers, CycleCheck for progression. Echo restrictions are handled by Required Echoes list.")]
    public List<WeatherCondition> availabilityConditions = new List<WeatherCondition>();
    
    [Header("Required Echoes")]
    [Tooltip("List of echoes this weather can appear in. If empty, available in all echoes. Weather is available if current echo is in this list.")]
    public List<EchoType> requiredEchoes = new List<EchoType>();
    #endregion
    
    #region Gameplay Effects
    [Header("Weather Effects")]
    [Tooltip("Effects applied by this weather (consistent with civic/legend effects)")]
    public List<WeatherEffect> effects = new List<WeatherEffect>();
    #endregion
    
    #region Visual Effects
    [Header("Particle Effects")]
    [Tooltip("Particle system prefab to spawn (e.g., rain, snow). Leave empty for no particles.")]
    public GameObject particlePrefab;
    
    [Tooltip("Particle spawn position offset from controller")]
    public Vector3 particleSpawnOffset = Vector3.zero;
    
    [Tooltip("Particle system scale multiplier")]
    public float particleScale = 1f;
    #endregion
    
    #region Transition Settings
    [Header("Transition Settings")]
    [Tooltip("Duration in seconds for lighting transitions (intensity, color)")]
    [Range(0f, 10f)]
    public float lightTransitionDuration = 2f;
    
    [Tooltip("Duration in seconds for atmospheric parameter transitions (fog, stars, etc.)")]
    [Range(0f, 10f)]
    public float atmosphericTransitionDuration = 3f;
    
    [Tooltip("Duration in seconds for particle effect fade in/out")]
    [Range(0f, 5f)]
    public float particleFadeDuration = 1f;
    
    [Tooltip("Ease type for transitions")]
    public DG.Tweening.Ease transitionEase = DG.Tweening.Ease.InOutQuad;
    #endregion
    
    [Header("Celestial Control")]
    [Tooltip("Global light intensity over seventh (0-100%). Y-axis: 0=dark, 1=bright. SEAMLESS: 100% matches 0% for smooth seventh transitions")]
    public AnimationCurve lightIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f),      // Dawn start (dim) - MATCHES END OF PREVIOUS SEVENTH
        new Keyframe(20f, 0.7f),     // Dawn end (brightening)
        new Keyframe(35f, 0.9f),     // Midday peak (brightest)
        new Keyframe(50f, 0.8f),     // Midday end (still bright)
        new Keyframe(60f, 0.5f),     // Dusk start (dimming)
        new Keyframe(70f, 0.2f),     // Dusk end (darkening)
        new Keyframe(80f, 0.1f),     // Night (dark)
        new Keyframe(90f, 0.05f),    // Night end (darkest)
        new Keyframe(100f, 0.3f)     // Post-Midnight (dim) - MATCHES START OF NEXT SEVENTH
    );
    
    [Tooltip("Color temperature over seventh. Y-axis: 0=cool/blue, 1=warm/orange. SEAMLESS: 100% matches 0% for smooth seventh transitions")]
    public AnimationCurve colorTemperatureCurve = new AnimationCurve(
        new Keyframe(0f, 0.8f),      // Dawn (warm orange) - MATCHES END OF PREVIOUS SEVENTH
        new Keyframe(20f, 0.5f),     // Dawn end (neutralizing)
        new Keyframe(35f, 0.5f),     // Midday peak (neutral)
        new Keyframe(50f, 0.5f),     // Midday end (neutral)
        new Keyframe(60f, 0.7f),     // Dusk start (warming)
        new Keyframe(70f, 0.9f),     // Dusk end (warm orange)
        new Keyframe(80f, 0.2f),     // Night (cool blue)
        new Keyframe(90f, 0.1f),     // Night end (coolest)
        new Keyframe(100f, 0.8f)     // Post-Midnight (warm orange) - MATCHES START OF NEXT SEVENTH
    );
    
    [Tooltip("Moon visibility over seventh. Y-axis: 0=hidden, 1=visible. SEAMLESS: 100% matches 0% for smooth seventh transitions")]
    public AnimationCurve moonVisibilityCurve = new AnimationCurve(
        new Keyframe(0f, 0.8f),      // Dawn (visible) - MATCHES END OF PREVIOUS SEVENTH
        new Keyframe(20f, 0f),       // Dawn end (fading)
        new Keyframe(35f, 0f),       // Midday peak (hidden)
        new Keyframe(50f, 0f),       // Midday end (hidden)
        new Keyframe(60f, 0.2f),     // Dusk start (appearing)
        new Keyframe(70f, 0.5f),     // Dusk end (more visible)
        new Keyframe(80f, 1f),       // Night (full moon)
        new Keyframe(90f, 0.9f),     // Night end (slightly dimmed)
        new Keyframe(100f, 0.8f)     // Post-Midnight (visible) - MATCHES START OF NEXT SEVENTH
    );
    
    [Header("Atmospheric Effects")]
    [Tooltip("Sky brightness multiplier. Y-axis: 0=dark, 1=bright. SEAMLESS: 100% matches 0% for smooth seventh transitions")]
    public AnimationCurve skyBrightnessCurve = new AnimationCurve(
        new Keyframe(0f, 0.4f),      // Dawn (dim) - MATCHES END OF PREVIOUS SEVENTH
        new Keyframe(20f, 0.7f),     // Dawn end (brightening)
        new Keyframe(35f, 1f),       // Midday peak (brightest)
        new Keyframe(50f, 0.9f),     // Midday end (still bright)
        new Keyframe(60f, 0.5f),     // Dusk start (dimming)
        new Keyframe(70f, 0.3f),     // Dusk end (darkening)
        new Keyframe(80f, 0.1f),     // Night (dark)
        new Keyframe(90f, 0.05f),    // Night end (darkest)
        new Keyframe(100f, 0.4f)     // Post-Midnight (dim) - MATCHES START OF NEXT SEVENTH
    );
    
    [Tooltip("Fog density. Y-axis: 0=clear, 1=dense")]
    public AnimationCurve fogDensityCurve = AnimationCurve.Constant(0, 100, 0f);
    
    [Tooltip("Starfield visibility. Y-axis: 0=hidden, 1=visible. SEAMLESS: 100% matches 0% for smooth seventh transitions")]
    public AnimationCurve starVisibilityCurve = new AnimationCurve(
        new Keyframe(0f, 1f),        // Dawn (visible) - MATCHES END OF PREVIOUS SEVENTH
        new Keyframe(20f, 0f),       // Dawn end (fading)
        new Keyframe(35f, 0f),       // Midday peak (hidden)
        new Keyframe(50f, 0f),       // Midday end (hidden)
        new Keyframe(60f, 0.1f),     // Dusk start (appearing)
        new Keyframe(70f, 0.3f),     // Dusk end (more visible)
        new Keyframe(80f, 1f),       // Night (full stars)
        new Keyframe(90f, 1f),       // Night end (full stars)
        new Keyframe(100f, 1f)       // Post-Midnight (full stars) - MATCHES START OF NEXT SEVENTH
    );
    
    [Header("Weather State Weights")]
    [Tooltip("Clear weather weight. Y-axis: 0-1 blend strength")]
    public AnimationCurve clearWeightCurve = AnimationCurve.Constant(0, 100, 1f);
    
    [Tooltip("Overcast weather weight. Y-axis: 0-1 blend strength")]
    public AnimationCurve overcastWeightCurve = AnimationCurve.Constant(0, 100, 0f);
    
    [Tooltip("Storm weather weight. Y-axis: 0-1 blend strength")]
    public AnimationCurve stormWeightCurve = AnimationCurve.Constant(0, 100, 0f);
    
    [Tooltip("Mist weather weight. Y-axis: 0-1 blend strength")]
    public AnimationCurve mistWeightCurve = AnimationCurve.Constant(0, 100, 0f);
    
    [Tooltip("Precipitation weight. Y-axis: 0-1 blend strength")]
    public AnimationCurve precipitationWeightCurve = AnimationCurve.Constant(0, 100, 0f);
    
    [Header("Color Gradients")]
    [Tooltip("Sky color at dawn phase (variable duration)")]
    public Color dawnSkyColor = new Color(1f, 0.6f, 0.4f);
    
    [Tooltip("Sky color at midday phase (variable duration)")]
    public Color middaySkyColor = new Color(0.4f, 0.7f, 1f);
    
    [Tooltip("Sky color at dusk phase (variable duration)")]
    public Color duskSkyColor = new Color(1f, 0.5f, 0.3f);
    
    [Tooltip("Sky color at night phase (variable duration)")]
    public Color nightSkyColor = new Color(0.05f, 0.05f, 0.15f);
    
    [Tooltip("Sky color at post-midnight phase (variable duration)")]
    public Color postMidnightSkyColor = new Color(0.02f, 0.02f, 0.1f);
    
    [Header("Light Colors")]
    [Tooltip("Light color at dawn")]
    public Color dawnLightColor = new Color(1f, 0.8f, 0.6f);
    
    [Tooltip("Light color during day")]
    public Color dayLightColor = Color.white;
    
    [Tooltip("Light color at dusk")]
    public Color duskLightColor = new Color(1f, 0.6f, 0.4f);
    
    [Tooltip("Light color at night")]
    public Color nightLightColor = new Color(0.6f, 0.7f, 1f);
    
    /// <summary>
    /// Sample a curve at given cycle percentage with wraparound support
    /// </summary>
    public float SampleCurve(AnimationCurve curve, float percentage)
    {
        // Normalize percentage to 0-100 range with wraparound
        float normalizedPercentage = percentage % 100f;
        if (normalizedPercentage < 0) normalizedPercentage += 100f;
        
        return curve.Evaluate(normalizedPercentage);
    }
    
    /// <summary>
    /// Get interpolated sky color based on cycle percentage using 5-phase system with variable durations
    /// </summary>
    public Color GetSkyColor(float percentage)
    {
        float p = percentage % 100f;
        if (p < 0) p += 100f;
        
        // Get normalized phase durations
        float[] durations = GetNormalizedPhaseDurations();
        
        // Calculate phase boundaries
        float dawnEnd = durations[0];
        float middayEnd = dawnEnd + durations[1];
        float duskEnd = middayEnd + durations[2];
        float nightEnd = duskEnd + durations[3];
        
        if (p < dawnEnd) // Dawn
            return Color.Lerp(postMidnightSkyColor, dawnSkyColor, p / dawnEnd);
        else if (p < middayEnd) // Midday
            return Color.Lerp(dawnSkyColor, middaySkyColor, (p - dawnEnd) / durations[1]);
        else if (p < duskEnd) // Dusk
            return Color.Lerp(middaySkyColor, duskSkyColor, (p - middayEnd) / durations[2]);
        else if (p < nightEnd) // Night
            return Color.Lerp(duskSkyColor, nightSkyColor, (p - duskEnd) / durations[3]);
        else // Post-Midnight
            return Color.Lerp(nightSkyColor, postMidnightSkyColor, (p - nightEnd) / durations[4]);
    }
    
    /// <summary>
    /// Get interpolated light color based on cycle percentage using 5-phase system with variable durations
    /// </summary>
    public Color GetLightColor(float percentage)
    {
        float p = percentage % 100f;
        if (p < 0) p += 100f;
        
        // Get normalized phase durations
        float[] durations = GetNormalizedPhaseDurations();
        
        // Calculate phase boundaries
        float dawnEnd = durations[0];
        float middayEnd = dawnEnd + durations[1];
        float duskEnd = middayEnd + durations[2];
        float nightEnd = duskEnd + durations[3];
        
        if (p < dawnEnd) // Dawn
            return Color.Lerp(nightLightColor, dawnLightColor, p / dawnEnd);
        else if (p < middayEnd) // Midday
            return Color.Lerp(dawnLightColor, dayLightColor, (p - dawnEnd) / durations[1]);
        else if (p < duskEnd) // Dusk
            return Color.Lerp(dayLightColor, duskLightColor, (p - middayEnd) / durations[2]);
        else if (p < nightEnd) // Night
            return Color.Lerp(duskLightColor, nightLightColor, (p - duskEnd) / durations[3]);
        else // Post-Midnight
            return nightLightColor;
    }
    
    #region Effect Helper Methods
    /// <summary>
    /// Check if this weather profile is valid for the given echo
    /// Uses requiredEchoes list for flexible echo matching (list-based approach)
    /// If requiredEchoes is empty, available in all echoes
    /// </summary>
    public bool IsValidForEcho(int echoNumber)
    {
        // If no required echoes specified, available for all echoes
        if (requiredEchoes == null || requiredEchoes.Count == 0)
        {
            return true;
        }
        
        // Get current echo name from TimeSystemLogic
        TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
        if (timeSystem == null)
        {
            return true; // If no time system, assume available
        }
        
        // Get current echo type from echo number
        EchoType currentEchoType = GetCurrentEchoType(echoNumber);
        
        // Check if current echo type is in the required echoes list
        return requiredEchoes.Contains(currentEchoType);
    }
    
    /// <summary>
    /// Get the echo type for the given echo number
    /// Maps echo numbers to EchoType enum values
    /// </summary>
    private EchoType GetCurrentEchoType(int echoNumber)
    {
        // Map echo numbers to EchoType enum values
        // Based on the echo assets: Resonance (1), Crescendo (2), Dissonance (3), Silence (4)
        switch (echoNumber)
        {
            case 1: return EchoType.Resonance;
            case 2: return EchoType.Crescendo;
            case 3: return EchoType.Dissonance;
            case 4: return EchoType.Silence;
            default:
                // Fallback: return first echo type if invalid number
                return EchoType.Resonance;
        }
    }
    
    /// <summary>
    /// Get all available echo types for the inspector dropdown
    /// Static method to provide echo types for UI/validation
    /// </summary>
    public static List<EchoType> GetAvailableEchoTypes()
    {
        return new List<EchoType>
        {
            EchoType.Resonance,
            EchoType.Crescendo,
            EchoType.Dissonance,
            EchoType.Silence
        };
    }
    
    /// <summary>
    /// Validate that all required echoes are valid echo types
    /// </summary>
    public bool ValidateRequiredEchoes()
    {
        if (requiredEchoes == null || requiredEchoes.Count == 0)
            return true; // Empty list is valid (available in all echoes)
            
        var validEchoTypes = GetAvailableEchoTypes();
        
        foreach (EchoType echoType in requiredEchoes)
        {
            if (!validEchoTypes.Contains(echoType))
            {
                Debug.LogWarning($"[WeatherProfileSO] Invalid echo type '{echoType}' in requiredEchoes for '{weatherDisplayName}'. Valid echoes: {string.Join(", ", validEchoTypes)}");
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if all availability conditions are met for this weather
    /// </summary>
    public bool AreConditionsMet()
    {
        if (availabilityConditions == null || availabilityConditions.Count == 0)
        {
            return true; // No conditions = always available
        }
        
        foreach (var condition in availabilityConditions)
        {
            if (!condition.Evaluate())
            {
                return false; // Any failed condition blocks availability
            }
        }
        
        return true; // All conditions met
    }
    
    /// <summary>
    /// Check if this weather is available for procedural selection (all conditions including echo)
    /// Single source of truth - only checks availabilityConditions
    /// </summary>
    public bool IsAvailableForProcedural(int currentEcho)
    {
        return AreConditionsMet(); // Conditions include echo checks
    }
    
    /// <summary>
    /// Get unique modifier source name for tracking
    /// </summary>
    public string GetModifierSourceName()
    {
        return $"Weather:{name}";
    }
    
    /// <summary>
    /// Get summary of all effects for UI/tooltips
    /// </summary>
    public string GetEffectSummary()
    {
        List<string> effects = new List<string>();
        
        // Weather effects
        foreach (var effect in this.effects)
        {
            if (effect != null)
            {
                effects.Add(effect.GetAutoDescription());
            }
        }
        
        return effects.Count > 0 ? string.Join("\n", effects) : "No gameplay effects";
    }
    
    /// <summary>
    /// Check if this profile has any visual effects
    /// </summary>
    public bool HasVisualEffects()
    {
        return particlePrefab != null;
    }
    
    /// <summary>
    /// Check if this profile has any gameplay effects
    /// </summary>
    public bool HasGameplayEffects()
    {
        return effects.Count > 0;
    }
    
    /// <summary>
    /// Validate that curves are set up for seamless seventh transitions
    /// Returns true if all curves have matching start/end values (100% matches 0%)
    /// </summary>
    public bool ValidateSeamlessCurves()
    {
        float tolerance = 0.01f; // Allow small floating point differences
        
        // Check light intensity curve
        float lightStart = lightIntensityCurve.Evaluate(0f);
        float lightEnd = lightIntensityCurve.Evaluate(100f);
        if (Mathf.Abs(lightStart - lightEnd) > tolerance)
        {
            Debug.LogWarning($"[WeatherProfileSO] {name}: Light intensity curve not seamless (0%={lightStart:F3}, 100%={lightEnd:F3})");
            return false;
        }
        
        // Check color temperature curve
        float colorStart = colorTemperatureCurve.Evaluate(0f);
        float colorEnd = colorTemperatureCurve.Evaluate(100f);
        if (Mathf.Abs(colorStart - colorEnd) > tolerance)
        {
            Debug.LogWarning($"[WeatherProfileSO] {name}: Color temperature curve not seamless (0%={colorStart:F3}, 100%={colorEnd:F3})");
            return false;
        }
        
        // Check moon visibility curve
        float moonStart = moonVisibilityCurve.Evaluate(0f);
        float moonEnd = moonVisibilityCurve.Evaluate(100f);
        if (Mathf.Abs(moonStart - moonEnd) > tolerance)
        {
            Debug.LogWarning($"[WeatherProfileSO] {name}: Moon visibility curve not seamless (0%={moonStart:F3}, 100%={moonEnd:F3})");
            return false;
        }
        
        // Check sky brightness curve
        float skyStart = skyBrightnessCurve.Evaluate(0f);
        float skyEnd = skyBrightnessCurve.Evaluate(100f);
        if (Mathf.Abs(skyStart - skyEnd) > tolerance)
        {
            Debug.LogWarning($"[WeatherProfileSO] {name}: Sky brightness curve not seamless (0%={skyStart:F3}, 100%={skyEnd:F3})");
            return false;
        }
        
        // Check star visibility curve
        float starStart = starVisibilityCurve.Evaluate(0f);
        float starEnd = starVisibilityCurve.Evaluate(100f);
        if (Mathf.Abs(starStart - starEnd) > tolerance)
        {
            Debug.LogWarning($"[WeatherProfileSO] {name}: Star visibility curve not seamless (0%={starStart:F3}, 100%={starEnd:F3})");
            return false;
        }
        
        return true; // All curves are seamless
    }
    
    /// <summary>
    /// Get a summary of curve seamless status for debugging
    /// </summary>
    public string GetCurveSeamlessStatus()
    {
        List<string> status = new List<string>();
        
        float lightStart = lightIntensityCurve.Evaluate(0f);
        float lightEnd = lightIntensityCurve.Evaluate(100f);
        status.Add($"Light: {lightStart:F3} → {lightEnd:F3} (diff: {Mathf.Abs(lightStart - lightEnd):F3})");
        
        float colorStart = colorTemperatureCurve.Evaluate(0f);
        float colorEnd = colorTemperatureCurve.Evaluate(100f);
        status.Add($"Color: {colorStart:F3} → {colorEnd:F3} (diff: {Mathf.Abs(colorStart - colorEnd):F3})");
        
        float moonStart = moonVisibilityCurve.Evaluate(0f);
        float moonEnd = moonVisibilityCurve.Evaluate(100f);
        status.Add($"Moon: {moonStart:F3} → {moonEnd:F3} (diff: {Mathf.Abs(moonStart - moonEnd):F3})");
        
        float skyStart = skyBrightnessCurve.Evaluate(0f);
        float skyEnd = skyBrightnessCurve.Evaluate(100f);
        status.Add($"Sky: {skyStart:F3} → {skyEnd:F3} (diff: {Mathf.Abs(skyStart - skyEnd):F3})");
        
        float starStart = starVisibilityCurve.Evaluate(0f);
        float starEnd = starVisibilityCurve.Evaluate(100f);
        status.Add($"Stars: {starStart:F3} → {starEnd:F3} (diff: {Mathf.Abs(starStart - starEnd):F3})");
        
        return string.Join("\n", status);
    }
    
    /// <summary>
    /// Get normalized phase durations that sum to exactly 100%
    /// If percentages don't sum to 100%, applies normalization rules:
    /// - Below 100%: adds missing percentage to lowest phase (random if tied)
    /// - Above 100%: trims excess from highest phase (random if tied)
    /// </summary>
    public float[] GetNormalizedPhaseDurations()
    {
        float[] durations = {
            dawnDurationPercentage,
            middayDurationPercentage,
            duskDurationPercentage,
            nightDurationPercentage,
            postMidnightDurationPercentage
        };
        
        // Calculate current total
        float total = durations[0] + durations[1] + durations[2] + durations[3] + durations[4];
        
        // If already 100%, return as-is
        if (Mathf.Abs(total - 100f) < 0.01f)
        {
            return durations;
        }
        
        if (total < 100f)
        {
            // Below 100%: add missing percentage to lowest phase
            float missing = 100f - total;
            int lowestIndex = FindLowestPhaseIndex(durations);
            durations[lowestIndex] += missing;
        }
        else
        {
            // Above 100%: trim excess from highest phase
            float excess = total - 100f;
            int highestIndex = FindHighestPhaseIndex(durations);
            durations[highestIndex] = Mathf.Max(0f, durations[highestIndex] - excess);
        }
        
        return durations;
    }
    
    /// <summary>
    /// Find index of phase with lowest duration (random if tied)
    /// </summary>
    private int FindLowestPhaseIndex(float[] durations)
    {
        float minValue = Mathf.Min(durations);
        List<int> lowestIndices = new List<int>();
        
        for (int i = 0; i < durations.Length; i++)
        {
            if (Mathf.Abs(durations[i] - minValue) < 0.01f)
            {
                lowestIndices.Add(i);
            }
        }
        
        // Return random index if multiple phases have same lowest value
        return lowestIndices[UnityEngine.Random.Range(0, lowestIndices.Count)];
    }
    
    /// <summary>
    /// Find index of phase with highest duration (random if tied)
    /// </summary>
    private int FindHighestPhaseIndex(float[] durations)
    {
        float maxValue = Mathf.Max(durations);
        List<int> highestIndices = new List<int>();
        
        for (int i = 0; i < durations.Length; i++)
        {
            if (Mathf.Abs(durations[i] - maxValue) < 0.01f)
            {
                highestIndices.Add(i);
            }
        }
        
        // Return random index if multiple phases have same highest value
        return highestIndices[UnityEngine.Random.Range(0, highestIndices.Count)];
    }
    
    /// <summary>
    /// Get current total percentage (for validation)
    /// </summary>
    public float GetTotalPhasePercentage()
    {
        return dawnDurationPercentage + middayDurationPercentage + duskDurationPercentage + 
               nightDurationPercentage + postMidnightDurationPercentage;
    }
    
    /// <summary>
    /// Validate that phase percentages are reasonable
    /// </summary>
    public bool ValidatePhasePercentages()
    {
        float total = GetTotalPhasePercentage();
        return total >= 50f && total <= 150f; // Allow some tolerance for manual adjustment
    }
    
    /// <summary>
    /// Validate all weather profile settings
    /// </summary>
    public bool ValidateWeatherProfile()
    {
        bool isValid = true;
        
        // Validate phase percentages
        if (!ValidatePhasePercentages())
        {
            Debug.LogWarning($"[WeatherProfileSO] Invalid phase percentages for '{weatherDisplayName}'. Total: {GetTotalPhasePercentage()}%");
            isValid = false;
        }
        
        // Validate required echoes
        if (!ValidateRequiredEchoes())
        {
            isValid = false;
        }
        
        return isValid;
    }
    #endregion
}
