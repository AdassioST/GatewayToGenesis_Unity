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
/// A weather availability condition. Values are read through <see cref="GameValues"/>, like event
/// conditions and civic requirements; the serialized fields stay as authored.
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
    
    /// <summary>The <see cref="GameValues"/> domain each condition reads (a guard test covers every type).</summary>
    public static string DomainOf(ConditionType type)
    {
        switch (type)
        {
            case ConditionType.CycleCheck: return "cycle";
            case ConditionType.TechnologyCheck: return "technology";
            case ConditionType.EventScoreCheck: return "score";
            default: return null;
        }
    }

    /// <summary>
    /// True when the condition holds now; false when its system is not in the scene yet. A technology check
    /// only asks "is it researched?" (requiredValue and comparison are ignored); the others compare the value.
    /// </summary>
    public bool Evaluate()
    {
        string domain = DomainOf(type);
        if (type == ConditionType.TechnologyCheck) return GameValues.Get(domain, targetName) >= 1f;
        return GameValues.Evaluate(domain, targetName, comparison, requiredValue);
    }
}

/// <summary>
/// Scriptable Object defining all weather and celestial parameters as curves over a 0-100% cycle
/// Extended with gameplay effects on resources, production, and morale
/// </summary>
[CreateAssetMenu(fileName = "New Weather Profile", menuName = "Environment/Weather Profile", order = 1)]
public class WeatherProfileSO : ScriptableObject
{
    [Header("Regional map weather")]
    [Tooltip("Travel fatigue on tiles covered by this weather; also affects units travelling to other cities.")]
    [Range(0.25f, 4f)] public float mapTravelMultiplier = 1f;
    [Tooltip("Snow on the combat hexes covered by this regional weather.")]
    public bool combatSnow;
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

        /// <summary>Player-facing wording (<see cref="GameEffect.Describe"/>). Checked at start-up by ContentValidator.</summary>
        public string GetAutoDescription() => this.ToEffect().Describe();
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
        var durations = GetNormalizedPhaseDurations();
        
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
        var durations = GetNormalizedPhaseDurations();
        
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
            if (condition != null && !condition.Evaluate())
            {
                return false; // Any failed condition blocks availability
            }
        }
        
        return true; // All conditions met
    }
    
    /// <summary>
    /// Get unique modifier source name for tracking
    /// </summary>
    public string GetModifierSourceName()
    {
        return $"Weather: {name}";
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

    /// <summary>Curves whose value at 100% must match 0%, so one seventh flows into the next without a jump.</summary>
    private IEnumerable<(string label, AnimationCurve curve)> SeamlessCurves()
    {
        yield return ("Light", lightIntensityCurve);
        yield return ("Color", colorTemperatureCurve);
        yield return ("Moon", moonVisibilityCurve);
        yield return ("Sky", skyBrightnessCurve);
        yield return ("Stars", starVisibilityCurve);
    }

    private const float SeamlessTolerance = 0.01f;

    /// <summary>True when every seamless curve ends where it starts (within 0.01). Reports nothing; see <see cref="GetCurveSeamlessStatus"/>.</summary>
    public bool ValidateSeamlessCurves()
    {
        foreach (var (_, curve) in SeamlessCurves())
        {
            if (curve != null && Mathf.Abs(curve.Evaluate(0f) - curve.Evaluate(100f)) > SeamlessTolerance) return false;
        }
        return true;
    }

    /// <summary>Start → end of every seamless curve, one per line, for the warning when a curve is not seamless.</summary>
    public string GetCurveSeamlessStatus()
    {
        var status = new List<string>();
        foreach (var (label, curve) in SeamlessCurves())
        {
            if (curve == null) continue;
            float start = curve.Evaluate(0f), end = curve.Evaluate(100f);
            status.Add($"{label}: {start:F3} → {end:F3} (diff: {Mathf.Abs(start - end):F3})");
        }
        return string.Join("\n", status);
    }

    [NonSerialized] private float[] _normalizedDurations;
    
    /// <summary>
    /// Phase durations (dawn, midday, dusk, night, post-midnight) normalized to 100% by
    /// <see cref="WeatherRules.NormalizePhaseDurations"/>. Computed once and cached (the sky reads it every frame);
    /// editing the profile in the inspector refreshes it.
    /// </summary>
    public IReadOnlyList<float> GetNormalizedPhaseDurations()
    {
        return _normalizedDurations ??= WeatherRules.NormalizePhaseDurations(new[]
        {
            dawnDurationPercentage, middayDurationPercentage, duskDurationPercentage, nightDurationPercentage, postMidnightDurationPercentage
        });
    }
    
    private void OnValidate() => _normalizedDurations = null;
    
    /// <summary>
    /// Get current total percentage (for validation)
    /// </summary>
    public float GetTotalPhasePercentage()
    {
        return dawnDurationPercentage + middayDurationPercentage + duskDurationPercentage + 
               nightDurationPercentage + postMidnightDurationPercentage;
    }
    
    /// <summary>
    /// Validate that phase percentages are reasonable (checked at start-up by ContentValidator)
    /// </summary>
    public bool ValidatePhasePercentages()
    {
        float total = GetTotalPhasePercentage();
        return total >= 50f && total <= 150f; // Allow some tolerance for manual adjustment
    }
    
    #endregion
}
