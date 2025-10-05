using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// System logic for weather management - handles weather selection, procedural system, and gameplay effects.
/// Separated from visual rendering concerns for cleaner architecture.
/// </summary>
public class CelestialWeatherSystemLogic : MonoBehaviour
{
    public static CelestialWeatherSystemLogic Instance { get; private set; }
    
    #region Serialized Fields
    [Header("Weather Configuration")]
    [SerializeField] private WeatherProfileSO activeWeatherProfile;
    [SerializeField] private WeatherProfileSO defaultWeatherProfile;
    
    [Header("Procedural Weather System")]
    [SerializeField] private bool enableProceduralWeather = true;
    [Tooltip("Auto-populated from Resources/WeatherProfiles on initialization. Manually added profiles are preserved.")]
    [SerializeField] private List<WeatherProfileSO> proceduralWeatherPool = new List<WeatherProfileSO>();
    
    [Header("Retention & Variety Balancing")]
    [Tooltip("Maximum consecutive retentions before forcing a change (0 = no limit)")]
    [SerializeField] private int maxConsecutiveRetentions = 20;
    [Tooltip("Probability reduction per consecutive retention (% decrease per seventh). Example: 2.0 = -2% per seventh")]
    [Range(0f, 10f)]
    [SerializeField] private float probabilityDecayPerRetention = 2f;
    [Tooltip("Sevenths a weather stays on cooldown after being forced to change")]
    [SerializeField] private int weatherCooldownSevenths = 3;
    [Tooltip("Fallback weather when no conditions are met (should have no conditions)")]
    [SerializeField] private WeatherProfileSO fallbackWeather;
    
    [Header("Duration & Locks")]
    [Tooltip("Minimum number of sevenths a weather must last before procedural changes/decay are allowed (0 = no minimum)")]
    [SerializeField] private int minSeventhsBeforeDecay = 3;
    
    [Header("Guaranteed Variation")]
    [Tooltip("If a weather hasn't occurred in this many sevenths, begin boosting its weight")] 
    [SerializeField] private int variationSeventhsThreshold = 15;
    [Tooltip("Additive weight increase applied each seventh past the threshold (linear, not multiplicative)")] 
    [SerializeField] private float variationWeightIncreasePerSeventh = 1.2f;
    [Tooltip("Maximum multiplier on base trigger weight due to variation boost (2.0 = up to double weight)")]
    [SerializeField] private float variationMaxWeightMultiplier = 2f;
    
    [Header("Debug Settings")]
    [SerializeField] private bool logWeatherChanges = true;
    [SerializeField] private bool showDebugInfo = true;
    
    [Header("Visual Logic Reference")]
    [SerializeField] private CelestialWeatherVisualLogic visualLogic;
    #endregion
    
    #region Private State
    private TimeSystemLogic timeSystem;
    
    // Weather effect management
    private WeatherProfileSO previousWeatherProfile;
    
    // Timed weather tracking
    private WeatherProfileSO timedWeatherProfile;
    private int timedWeatherRemainingSevenths;
    private WeatherProfileSO weatherBeforeTimed;
    
    // Hard-set weather tracking (from techs/events/manual changes)
    private bool isHardSetWeather = false;
    private bool isDecayingWeather = false; // Whether current hard-set weather is decaying back to procedural
    
    // Retention tracking for forced changes
    private int consecutiveRetentions = 0;
    
    // Weather cooldown tracking (weather -> sevenths remaining)
    private Dictionary<WeatherProfileSO, int> weatherCooldowns = new Dictionary<WeatherProfileSO, int>();
    
    // Cached pool to avoid duplicate lookups
    private HashSet<WeatherProfileSO> cachedPoolSet = new HashSet<WeatherProfileSO>();
    
    // Pending weather bonuses for resources that aren't instantiated yet
    private Dictionary<string, List<PendingWeatherBonus>> pendingResourceBonuses = new Dictionary<string, List<PendingWeatherBonus>>();
    private Dictionary<string, List<PendingWeatherBonus>> pendingSectionBonuses = new Dictionary<string, List<PendingWeatherBonus>>();
    private List<PendingWeatherBonus> pendingGlobalBonuses = new List<PendingWeatherBonus>();
    
    // Minimum-duration lock state
    private int weatherSelectionLockRemainingSevenths = 0;
    private bool pendingForcedChangeDueToInvalidation = false;
    private bool pendingTimedRevert = false;
    
    // Variation tracking: sevenths since each weather last occurred
    private Dictionary<WeatherProfileSO, int> seventhsSinceLastOccurrence = new Dictionary<WeatherProfileSO, int>();
    
    private bool isInitialized = false;
    #endregion
    
    #region Events
    public event Action<WeatherProfileSO> OnWeatherChanged;
    #endregion
    
    #region Pending Bonus System
    /// <summary>
    /// Tracks weather bonuses for resources that aren't instantiated yet
    /// </summary>
    [System.Serializable]
    public struct PendingWeatherBonus
    {
        public string weatherSource;
        public GameEffectType bonusType;
        public string targetStat;
        public float modifierValue;
        public ModifierType modifierType;
        public ScopeType scope;
        public bool isPositive;
        
        public PendingWeatherBonus(string source, GameEffectType type, string target, float value, ModifierType modType, ScopeType scopeType, bool positive)
        {
            this.weatherSource = source;
            this.bonusType = type;
            this.targetStat = target;
            this.modifierValue = value;
            this.modifierType = modType;
            this.scope = scopeType;
            this.isPositive = positive;
        }
    }
    #endregion
    
    #region Properties
    public WeatherProfileSO ActiveWeatherProfile => activeWeatherProfile;
    public bool IsHardSetWeather => isHardSetWeather;
    public bool IsProceduralWeatherEnabled => enableProceduralWeather;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CelestialWeatherSystemLogic] Duplicate instance detected, destroying new one.");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Auto-find visual logic if not assigned
        if (visualLogic == null)
        {
            visualLogic = GetComponent<CelestialWeatherVisualLogic>();
            if (visualLogic == null)
            {
                visualLogic = gameObject.AddComponent<CelestialWeatherVisualLogic>();
            }
        }
        
        // Assign default profile if none set
        if (activeWeatherProfile == null && defaultWeatherProfile != null)
        {
            activeWeatherProfile = defaultWeatherProfile;
        }
    }
    
    private void Start()
    {
        InitializeSystem();
    }
    
    private void InitializeSystem()
    {
        // Get TimeSystemLogic reference
        timeSystem = TimeSystemLogic.Instance;
        if (timeSystem == null)
        {
            Debug.LogError("[CelestialWeatherSystemLogic] TimeSystemLogic not found! Weather system cannot function.");
            enabled = false;
            return;
        }
        
        // Ensure GameAssetValidator is initialized
        GameAssetValidator.LoadWeatherProfiles();
        
        // Auto-populate procedural weather pool
        AutoPopulateProceduralWeatherPool();
        
        // Assign default weather if none set (mark as procedural, not hard-set)
        if (activeWeatherProfile == null)
        {
            WeatherProfileSO calmWinds = FindWeatherProfile("Calm Winds");
            if (calmWinds != null)
            {
                activeWeatherProfile = calmWinds;
                isHardSetWeather = false;
                GameLoggingSystem.Instance.LogEvent("Using 'Calm Winds' as initial weather (procedural)", "CelestialWeatherSystemLogic");
            }
            else if (defaultWeatherProfile != null)
            {
                activeWeatherProfile = defaultWeatherProfile;
                isHardSetWeather = false;
                GameLoggingSystem.Instance.LogEvent($"Using default weather profile: {defaultWeatherProfile.weatherDisplayName} (procedural)", "CelestialWeatherSystemLogic");
            }
            else if (proceduralWeatherPool.Count > 0)
            {
                activeWeatherProfile = proceduralWeatherPool[0];
                isHardSetWeather = false;
                GameLoggingSystem.Instance.LogEvent($"Using first weather from pool: {activeWeatherProfile.weatherDisplayName} (procedural)", "CelestialWeatherSystemLogic");
            }
        }
        
        // Initialize variation tracking for all profiles
        InitializeVariationTracking();
        
        // Subscribe to time system events
        timeSystem.OnSeventhChange += OnSeventhChanged;
        timeSystem.OnPhaseChange += OnTimePhaseChanged;
        timeSystem.OnCycleChange += OnCycleChanged;
        timeSystem.OnEchoChange += OnEchoChanged;
        
        // Initialize visual logic with current weather
        if (visualLogic != null && activeWeatherProfile != null)
        {
            visualLogic.InitializeWithWeather(activeWeatherProfile);
            ApplyWeatherEffects(activeWeatherProfile);
            // Apply initial minimum-duration lock so starting weather also respects minimum duration
            weatherSelectionLockRemainingSevenths = Mathf.Max(0, minSeventhsBeforeDecay);
            if (logWeatherChanges)
            {
                GameLoggingSystem.Instance.LogEvent($"Initial minimum-duration lock applied: {weatherSelectionLockRemainingSevenths} sevenths", "CelestialWeatherSystemLogic");
            }
        }
        
        isInitialized = true;
        
        GameLoggingSystem.Instance.LogEvent("Celestial Weather System Logic initialized", "CelestialWeatherSystemLogic");
    }
    
    private void OnDestroy()
    {
        if (timeSystem != null)
        {
            timeSystem.OnSeventhChange -= OnSeventhChanged;
            timeSystem.OnPhaseChange -= OnTimePhaseChanged;
            timeSystem.OnCycleChange -= OnCycleChanged;
            timeSystem.OnEchoChange -= OnEchoChanged;
        }
        
        // Clean up active weather effects
        if (activeWeatherProfile != null)
        {
            RemoveWeatherEffects(activeWeatherProfile);
        }
    }
    #endregion
    
    #region Time System Event Handlers
    private void OnSeventhChanged(int newSeventh)
    {
        // Update variation counters each seventh
        IncrementSeventhsSinceOccurrence();
        
        // Decrement minimum-duration lock
        if (weatherSelectionLockRemainingSevenths > 0)
        {
            weatherSelectionLockRemainingSevenths--;
        }
        
        // Handle timed weather expiration
        if (timedWeatherProfile != null)
        {
            if (timedWeatherRemainingSevenths > 0)
            {
                timedWeatherRemainingSevenths--;
            }
            
            if (timedWeatherRemainingSevenths <= 0)
            {
                if (weatherSelectionLockRemainingSevenths > 0)
                {
                    if (!pendingTimedRevert)
                    {
                        GameLoggingSystem.Instance.LogEvent(
                            $"Timed weather '{timedWeatherProfile.weatherDisplayName}' expired but minimum-duration lock is active ({weatherSelectionLockRemainingSevenths} left) - deferring revert",
                            "CelestialWeatherSystemLogic"
                        );
                    }
                    pendingTimedRevert = true;
                    timedWeatherRemainingSevenths = 0; // clamp
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Timed weather '{timedWeatherProfile.weatherDisplayName}' expired, reverting to previous weather",
                        "CelestialWeatherSystemLogic"
                    );
                    
                    if (weatherBeforeTimed != null)
                    {
                        SetWeatherProfileInternal(weatherBeforeTimed, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
                    }
                    else
                    {
                        isHardSetWeather = false;
                    }
                    
                    timedWeatherProfile = null;
                    weatherBeforeTimed = null;
                    pendingTimedRevert = false;
                }
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Timed weather '{timedWeatherProfile.weatherDisplayName}' - {timedWeatherRemainingSevenths} sevenths remaining",
                    "CelestialWeatherSystemLogic"
                );
            }
        }
        
        // Apply any deferred forced change once the minimum-duration lock expires
        if (weatherSelectionLockRemainingSevenths <= 0 && (pendingForcedChangeDueToInvalidation || pendingTimedRevert) && enableProceduralWeather && (!isHardSetWeather || isDecayingWeather))
        {
            if (pendingTimedRevert && timedWeatherProfile != null)
            {
                // Timed revert takes precedence
                GameLoggingSystem.Instance.LogEvent("Minimum-duration lock expired - applying deferred timed weather revert", "CelestialWeatherSystemLogic");
                if (weatherBeforeTimed != null)
                {
                    SetWeatherProfileInternal(weatherBeforeTimed, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
                }
                else
                {
                    isHardSetWeather = false;
                }
                timedWeatherProfile = null;
                weatherBeforeTimed = null;
                pendingTimedRevert = false;
                return;
            }
            if (pendingForcedChangeDueToInvalidation && timedWeatherProfile == null)
            {
                pendingForcedChangeDueToInvalidation = false;
                consecutiveRetentions = 0;
                GameLoggingSystem.Instance.LogEvent("Minimum-duration lock expired - applying deferred forced weather change", "CelestialWeatherSystemLogic");
                SelectProceduralWeather();
                return;
            }
        }
        
        // Trigger procedural weather change if enabled and no hard-set weather (or decaying weather)
        if (enableProceduralWeather && (!isHardSetWeather || isDecayingWeather) && timedWeatherProfile == null)
        {
            if (weatherSelectionLockRemainingSevenths > 0)
            {
                if (logWeatherChanges)
                {
                    GameLoggingSystem.Instance.LogEvent($"Minimum-duration lock active ({weatherSelectionLockRemainingSevenths} left) - skipping procedural change this seventh", "CelestialWeatherSystemLogic");
                }
            }
            else
            {
                ProcessProceduralWeatherChange();
            }
        }
    }
    
    private void OnTimePhaseChanged(int newPhase)
    {
        GameLoggingSystem.Instance.LogEvent($"TimeSystem phase changed to {newPhase}", "CelestialWeatherSystemLogic");
    }
    
    private void OnCycleChanged(int newCycle)
    {
        GameLoggingSystem.Instance.LogEvent($"Cycle changed to {newCycle}", "CelestialWeatherSystemLogic");
        
        // Check if current weather is still valid for new cycle (allow decaying weather to change)
        if ((!isHardSetWeather || isDecayingWeather) && activeWeatherProfile != null)
        {
            if (!activeWeatherProfile.AreConditionsMet())
            {
                if (weatherSelectionLockRemainingSevenths > 0)
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Current weather '{activeWeatherProfile.weatherDisplayName}' no longer meets conditions after cycle change, but minimum-duration lock is active ({weatherSelectionLockRemainingSevenths} left) - deferring change",
                        "CelestialWeatherSystemLogic"
                    );
                    pendingForcedChangeDueToInvalidation = true;
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Current weather '{activeWeatherProfile.weatherDisplayName}' no longer meets conditions after cycle change - forcing new selection",
                        "CelestialWeatherSystemLogic"
                    );
                    consecutiveRetentions = 0;
                    SelectProceduralWeather();
                }
            }
        }
    }
    
    private void OnEchoChanged(int newEcho)
    {
        GameLoggingSystem.Instance.LogEvent($"Echo changed to {newEcho}", "CelestialWeatherSystemLogic");
        
        // Check if current weather is still valid for new echo (allow decaying weather to change)
        if ((!isHardSetWeather || isDecayingWeather) && activeWeatherProfile != null)
        {
            if (!activeWeatherProfile.AreConditionsMet())
            {
                if (weatherSelectionLockRemainingSevenths > 0)
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Current weather '{activeWeatherProfile.weatherDisplayName}' no longer meets conditions after echo change, but minimum-duration lock is active ({weatherSelectionLockRemainingSevenths} left) - deferring change",
                        "CelestialWeatherSystemLogic"
                    );
                    pendingForcedChangeDueToInvalidation = true;
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Current weather '{activeWeatherProfile.weatherDisplayName}' no longer meets conditions after echo change - forcing new selection",
                        "CelestialWeatherSystemLogic"
                    );
                    consecutiveRetentions = 0;
                    SelectProceduralWeather();
                }
            }
        }
    }
    #endregion
    
    #region Weather Effect Lifecycle
    /// <summary>
    /// Apply all gameplay effects from a weather profile
    /// </summary>
    private void ApplyWeatherEffects(WeatherProfileSO profile)
    {
        if (profile == null) return;
        
        string sourceName = profile.GetModifierSourceName();
        
        // Apply weather effects using consistent pattern
        foreach (var effect in profile.effects)
        {
            if (effect == null) continue;
            
            // Validate effect before applying
            var validation = effect.ValidateEffect();
            if (!validation.isValid)
            {
                Debug.LogWarning($"[CelestialWeatherSystemLogic] Invalid weather effect in '{profile.weatherDisplayName}': {validation.errorMessage}");
                continue;
            }
            
            // Apply effect based on type using consistent pattern
            ApplyWeatherEffect(effect, sourceName, isAdding: true);
        }
        
        GameLoggingSystem.Instance.LogEvent(
            $"Applied weather effects from profile: {profile.weatherDisplayName}",
            "CelestialWeatherSystemLogic"
        );
    }
    
    /// <summary>
    /// Remove all gameplay effects from a weather profile
    /// </summary>
    private void RemoveWeatherEffects(WeatherProfileSO profile)
    {
        if (profile == null) return;
        
        string sourceName = profile.GetModifierSourceName();
        
        // Remove weather effects using consistent pattern
        foreach (var effect in profile.effects)
        {
            if (effect == null) continue;
            
            // Remove effect based on type using consistent pattern
            ApplyWeatherEffect(effect, sourceName, isAdding: false);
        }
        
        // Clear all bonuses from this weather source
        if (StatManager.Instance != null)
        {
            StatManager.Instance.ClearBonusesFromSource(sourceName);
        }
        
        // Clear all modifiers from this weather source
        if (GlobalProductionManager.Instance != null)
        {
            GlobalProductionManager.Instance.ClearAllModifiersFromSource(sourceName);
        }
        
        // Clear pending bonuses from this weather source
        ClearPendingBonusesFromSource(sourceName);
        
        GameLoggingSystem.Instance.LogEvent(
            $"Removed weather effects from profile: {profile.weatherDisplayName}",
            "CelestialWeatherSystemLogic"
        );
    }
    
    /// <summary>
    /// Apply or remove a single weather effect to the appropriate systems
    /// Uses consistent pattern like CivicManager
    /// </summary>
    private void ApplyWeatherEffect(WeatherProfileSO.WeatherEffect effect, string sourceName, bool isAdding)
    {
        StatManager statManager = StatManager.Instance;
        GlobalProductionManager productionManager = GlobalProductionManager.Instance;
        
        float value = isAdding ? effect.modifierValue : -effect.modifierValue;
        string action = isAdding ? "Applied" : "Removed";
        
        switch (effect.effectType)
        {
            case GameEffectType.PillarBonus:
                if (statManager != null && !string.IsNullOrEmpty(effect.targetStat))
                {
                    if (isAdding)
                    {
                        // Get base value for proper percentage calculation (same as GovernmentLogic)
                        float basePillarValue = statManager.GetBasePillarValue(effect.targetStat);
                        float pillarBonusValue = CalculateWeatherStatModifierValue(effect.modifierValue, effect.modifierType, basePillarValue);
                        statManager.AddPillarBonus(effect.targetStat, pillarBonusValue, sourceName);
                        
                        GameLoggingSystem.Instance.LogEvent($"{action} pillar effect: {effect.targetStat} +{pillarBonusValue} (base: {basePillarValue}, {effect.modifierValue} {effect.modifierType}) ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                    }
                    else
                    {
                        statManager.RemovePillarBonus(effect.targetStat, sourceName);
                    }
                }
                break;
                
            case GameEffectType.SubstatBonus:
                if (statManager != null && !string.IsNullOrEmpty(effect.targetStat))
                {
                    if (isAdding)
                    {
                        // Get base value for proper percentage calculation (same as GovernmentLogic)
                        float baseSubstatValue = statManager.GetBaseSubstatValue(effect.targetStat);
                        float substatBonusValue = CalculateWeatherStatModifierValue(effect.modifierValue, effect.modifierType, baseSubstatValue);
                        statManager.AddSubstatBonus(effect.targetStat, substatBonusValue, sourceName);
                        
                        GameLoggingSystem.Instance.LogEvent($"{action} substat effect: {effect.targetStat} +{substatBonusValue} (base: {baseSubstatValue}, {effect.modifierValue} {effect.modifierType}) ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                    }
                    else
                    {
                        statManager.RemoveSubstatBonus(effect.targetStat, sourceName);
                    }
                }
                break;
                
            case GameEffectType.DerivedStatBonus:
                if (statManager != null && !string.IsNullOrEmpty(effect.targetStat))
                {
                    if (isAdding)
                    {
                        // Get base value for proper percentage calculation (same as GovernmentLogic)
                        float baseDerivedValue = statManager.GetBaseDerivedValue(effect.targetStat);
                        float derivedBonusValue = CalculateWeatherStatModifierValue(effect.modifierValue, effect.modifierType, baseDerivedValue);
                        statManager.AddDerivedStatBonus(effect.targetStat, derivedBonusValue, sourceName);
                        
                        GameLoggingSystem.Instance.LogEvent($"{action} derived stat effect: {effect.targetStat} +{derivedBonusValue} (base: {baseDerivedValue}, {effect.modifierValue} {effect.modifierType}) ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                    }
                    else
                    {
                        statManager.RemoveDerivedStatBonus(effect.targetStat, sourceName);
                    }
                }
                break;
                
            case GameEffectType.ResourceModifier:
                if (productionManager != null)
                {
        if (effect.scope == ScopeType.Global)
        {
            // Apply to all existing resources
            foreach (var resourceSlot in productionManager.GetAllResourceSlots())
            {
                if (resourceSlot?.gameUnit != null)
                {
                    productionManager.AdjustPercentageModifier(
                        resourceSlot.gameUnit.name,
                        effect.modifierValue,
                                    effect.modifierValue > 0,
                                    isAdding,
                        sourceName
                    );
                            }
                        }
                        
                        // Store pending bonus for future resources
                        if (isAdding)
                        {
                            pendingGlobalBonuses.Add(new PendingWeatherBonus(
                                sourceName, effect.effectType, "", effect.modifierValue, 
                                ModifierType.Percentage, effect.scope, effect.modifierValue > 0
                            ));
                        }
        }
        else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
        {
            // Apply to specific section
            productionManager.AdjustPercentageModifierForSection(
                effect.targetStat,
                effect.modifierValue,
                            effect.modifierValue > 0,
                            isAdding,
                sourceName
            );
            
                        // Store pending bonus for future resources in this section
                        if (isAdding)
            {
                            if (!pendingSectionBonuses.ContainsKey(effect.targetStat))
                pendingSectionBonuses[effect.targetStat] = new List<PendingWeatherBonus>();
                            
                            pendingSectionBonuses[effect.targetStat].Add(new PendingWeatherBonus(
                                sourceName, effect.effectType, effect.targetStat, effect.modifierValue, 
                                ModifierType.Percentage, effect.scope, effect.modifierValue > 0
                            ));
                        }
        }
        else if (!string.IsNullOrEmpty(effect.targetStat))
        {
            // Check if resource exists
                        bool resourceExists = productionManager.GetAllResourceSlots().Any(slot => slot.gameUnit.name == effect.targetStat);
                        
                        if (resourceExists)
            {
                            // Apply directly to existing resource
                    productionManager.AdjustPercentageModifier(
                        effect.targetStat,
                        effect.modifierValue,
                                effect.modifierValue > 0,
                                isAdding,
                        sourceName
                    );
                        }
                        else if (isAdding)
                        {
                            // Store pending bonus for future resource
                            if (!pendingResourceBonuses.ContainsKey(effect.targetStat))
                                pendingResourceBonuses[effect.targetStat] = new List<PendingWeatherBonus>();
                            
                            pendingResourceBonuses[effect.targetStat].Add(new PendingWeatherBonus(
                                sourceName, effect.effectType, effect.targetStat, effect.modifierValue, 
                                ModifierType.Percentage, effect.scope, effect.modifierValue > 0
                            ));
                        }
                    }
                    
                    string target = effect.scope == ScopeType.Global ? "all resources" : 
                                 effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                 effect.targetStat;
                    GameLoggingSystem.Instance.LogEvent($"{action} resource modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                    break;
                
            case GameEffectType.MaxMoraleModifier:
                if (statManager != null)
                {
                    if (isAdding)
                    {
                        statManager.AddGlobalBonus("maxMorale", Mathf.RoundToInt(effect.modifierValue), sourceName);
                    }
                    else
                    {
                        statManager.RemoveGlobalBonus("maxMorale", sourceName);
                    }
                    
                    GameLoggingSystem.Instance.LogEvent($"{action} max morale effect: +{effect.modifierValue} ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                break;
                
            case GameEffectType.MoraleBalanceModifier:
                if (statManager != null)
                {
                    if (isAdding)
                    {
                        // Decrease morale balance (easier to stay above balance)
                        statManager.AddGlobalBonus("moraleBalance", -Mathf.RoundToInt(effect.modifierValue), sourceName);
                    }
                    else
                    {
                        // Remove morale balance bonus (restore original balance)
                        statManager.RemoveGlobalBonus("moraleBalance", sourceName);
                    }
                    
                    GameLoggingSystem.Instance.LogEvent($"{action} morale balance effect: Balance -{effect.modifierValue} ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                break;
                
            case GameEffectType.SatisfactionThresholdModifier:
                if (statManager != null)
                {
                    if (isAdding)
                    {
                        statManager.AddGlobalBonus("satisfactionUpgradeThreshold", Mathf.RoundToInt(effect.modifierValue), sourceName);
                    }
                    else
                    {
                        statManager.RemoveGlobalBonus("satisfactionUpgradeThreshold", sourceName);
                    }
                    
                    GameLoggingSystem.Instance.LogEvent($"{action} satisfaction threshold effect: +{effect.modifierValue} ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                break;
                
            case GameEffectType.ProductionModifier:
                // Apply production efficiency modifier using GameUnitsLogic
                if (GameUnitsLogic.Instance != null)
                {
                    if (effect.scope == ScopeType.Global)
                    {
                        // Apply to all building types
                        GameUnitsLogic.Instance.AdjustProductionModifierByType("Building", effect.modifierValue, isAdding, sourceName);
                        GameUnitsLogic.Instance.AdjustProductionModifierByType("Unit", effect.modifierValue, isAdding, sourceName);
                    }
                    else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
                    {
                        // Apply to specific section
                        GameUnitsLogic.Instance.AdjustProductionModifierBySection(effect.targetStat, effect.modifierValue, isAdding, sourceName);
                    }
                    else if (!string.IsNullOrEmpty(effect.targetStat))
                    {
                        if (effect.targetStat.ToLower() == "building" || effect.targetStat.ToLower() == "unit")
                        {
                            // Apply to specific building type
                            GameUnitsLogic.Instance.AdjustProductionModifierByType(effect.targetStat, effect.modifierValue, isAdding, sourceName);
                        }
                        else
                        {
                            // Apply to specific section
                            GameUnitsLogic.Instance.AdjustProductionModifierBySection(effect.targetStat, effect.modifierValue, isAdding, sourceName);
                        }
                    }
                    
                    string target = effect.scope == ScopeType.Global ? "all buildings" : 
                                 effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                 effect.targetStat;
                    GameLoggingSystem.Instance.LogEvent($"{action} production modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                break;
                
            case GameEffectType.ClickPowerBonus:
                // Apply click power modifier using GameUnitsLogic
                if (GameUnitsLogic.Instance != null)
                {
                    if (effect.scope == ScopeType.Global)
                    {
                        // Apply to all resources
                        GameUnitsLogic.Instance.AdjustClickPowerPercentForSection("", effect.modifierValue);
                    }
                    else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
                    {
                        // Apply to specific section
                        GameUnitsLogic.Instance.AdjustClickPowerPercentForSection(effect.targetStat, effect.modifierValue);
                    }
                    else if (!string.IsNullOrEmpty(effect.targetStat))
                    {
                        // Apply to specific resource
                        GameUnitsLogic.Instance.AdjustClickPowerPercent(effect.targetStat, effect.modifierValue);
                    }
                    
                    string target = effect.scope == ScopeType.Global ? "all resources" : 
                                 effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                 effect.targetStat;
                    GameLoggingSystem.Instance.LogEvent($"{action} click power modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                }
                break;
                
            case GameEffectType.HousingBonus:
                // Apply housing bonus using PopGrowthLogic persistent system
                if (effect.modifierValue != 0)
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        if (isAdding)
                        {
                            PopGrowthLogic.Instance.AddHousingBonus(Mathf.RoundToInt(effect.modifierValue), sourceName);
                        }
                        else
                        {
                            PopGrowthLogic.Instance.RemoveHousingBonus(sourceName);
                        }
                        
                        GameLoggingSystem.Instance.LogEvent($"{action} housing bonus: +{effect.modifierValue} ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                    }
                }
                break;
                
            case GameEffectType.ProductionScalingBonus:
                // Apply production scaling bonus (bonus per production unit)
                if (!string.IsNullOrEmpty(effect.targetStat) && effect.modifierValue > 0)
                {
                    if (GameUnitsLogic.Instance != null)
                    {
                        // Use conditionStat if available, otherwise use targetStat as the production unit
                        string productionUnitName = !string.IsNullOrEmpty(effect.conditionStat) ? effect.conditionStat : effect.targetStat;
                        
                        if (isAdding)
                        {
                            GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnitName, effect.modifierValue, sourceName);
                        }
                        else
                        {
                            GameUnitsLogic.Instance.RemoveProductionScalingBonus(productionUnitName, sourceName);
                        }
                        
                        string description = !string.IsNullOrEmpty(effect.conditionStat) 
                            ? $"+{effect.modifierValue} {effect.targetStat} per {effect.conditionStat}"
                            : $"+{effect.modifierValue} {effect.targetStat} per production unit";
                        GameLoggingSystem.Instance.LogEvent($"{action} production scaling bonus: {description} ({effect.GetAutoDescription()})", "CelestialWeatherSystemLogic");
                    }
                }
                break;
                
            case GameEffectType.ConstructionCostModifier:
            case GameEffectType.SpecialAbility:
                // Handle special abilities and construction cost modifiers
                GameLoggingSystem.Instance.LogEvent(
                    $"{action} special effect: {effect.GetAutoDescription()}",
                    "CelestialWeatherSystemLogic"
                );
                break;
                
            default:
                Debug.LogWarning($"[CelestialWeatherSystemLogic] Unhandled weather effect type: {effect.effectType}");
                break;
        }
    }
    
    /// <summary>
    /// Apply pending weather bonuses to a newly added resource slot
    /// This is called from GlobalProductionManager when a new resource slot is added
    /// </summary>
    public void ApplyPendingBonusesToResource(string resourceName, string sectionName)
    {
        if (string.IsNullOrEmpty(resourceName)) return;
        
        int appliedBonuses = 0;
        
        // Apply global pending bonuses
        foreach (var pendingBonus in pendingGlobalBonuses)
        {
            if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
            {
                if (pendingBonus.modifierType == ModifierType.Percentage)
                {
                    GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.weatherSource);
                }
                else
                {
                    GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.weatherSource);
                }
                appliedBonuses++;
            }
        }
        
        // Apply section-specific pending bonuses
        if (!string.IsNullOrEmpty(sectionName) && pendingSectionBonuses.ContainsKey(sectionName))
        {
            foreach (var pendingBonus in pendingSectionBonuses[sectionName])
            {
                if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
                {
                    if (pendingBonus.modifierType == ModifierType.Percentage)
                    {
                        GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.weatherSource);
                    }
                    else
                    {
                        GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.weatherSource);
                    }
                    appliedBonuses++;
                }
            }
        }
        
        // Apply resource-specific pending bonuses
        if (pendingResourceBonuses.ContainsKey(resourceName))
        {
            foreach (var pendingBonus in pendingResourceBonuses[resourceName])
            {
                if (pendingBonus.bonusType == GameEffectType.ResourceModifier)
                {
                    if (pendingBonus.modifierType == ModifierType.Percentage)
                    {
                        GlobalProductionManager.Instance.AdjustPercentageModifier(resourceName, Mathf.Abs(pendingBonus.modifierValue), pendingBonus.isPositive, true, pendingBonus.weatherSource);
                    }
                    else
                    {
                        GlobalProductionManager.Instance.AdjustResourceModifier(resourceName, pendingBonus.modifierValue, pendingBonus.isPositive, true, pendingBonus.weatherSource);
                    }
                    appliedBonuses++;
                }
            }
            
            // Remove applied bonuses from pending list
            pendingResourceBonuses[resourceName].Clear();
        }
        
        if (appliedBonuses > 0)
        {
            GameLoggingSystem.Instance.LogEvent($"Applied {appliedBonuses} pending weather bonuses to new resource: {resourceName} (section: {sectionName})", "CelestialWeatherSystemLogic");
        }
    }
    
    /// <summary>
    /// Clear all pending bonuses from a specific weather source
    /// </summary>
    private void ClearPendingBonusesFromSource(string sourceName)
    {
        // Clear global pending bonuses
        pendingGlobalBonuses.RemoveAll(bonus => bonus.weatherSource == sourceName);
        
        // Clear section pending bonuses
        var sectionsToRemove = new List<string>();
        foreach (var kvp in pendingSectionBonuses)
        {
            kvp.Value.RemoveAll(bonus => bonus.weatherSource == sourceName);
            if (kvp.Value.Count == 0)
            {
                sectionsToRemove.Add(kvp.Key);
            }
        }
        foreach (var section in sectionsToRemove)
        {
            pendingSectionBonuses.Remove(section);
        }
        
        // Clear resource pending bonuses
        var resourcesToRemove = new List<string>();
        foreach (var kvp in pendingResourceBonuses)
        {
            kvp.Value.RemoveAll(bonus => bonus.weatherSource == sourceName);
            if (kvp.Value.Count == 0)
            {
                resourcesToRemove.Add(kvp.Key);
            }
        }
        foreach (var resource in resourcesToRemove)
        {
            pendingResourceBonuses.Remove(resource);
        }
    }
    
    /// <summary>
    /// Calculate weather stat modifier value based on modifier type and base value
    /// Uses the same logic as GovernmentLogic for consistency
    /// </summary>
    private float CalculateWeatherStatModifierValue(float modifierValue, ModifierType modifierType, float baseValue)
    {
        switch (modifierType)
        {
            case ModifierType.Add:
                return modifierValue; // Direct additive values
                
            case ModifierType.Percentage:
                // Calculate percentage of base value
                float percentageBonus = (modifierValue / 100f) * baseValue;
                return Mathf.Round(percentageBonus);
                
            case ModifierType.SetValue:
                // Set value replaces the base entirely
                return modifierValue - baseValue; // Return the difference to add to base
                
            default:
                Debug.LogWarning($"[CelestialWeatherSystemLogic] Unknown modifier type: {modifierType}");
                return modifierValue;
        }
    }
    #endregion
    
    #region Guaranteed Variation
    private void InitializeVariationTracking()
    {
        seventhsSinceLastOccurrence.Clear();
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile == null) continue;
            seventhsSinceLastOccurrence[profile] = (profile == activeWeatherProfile) ? 0 : 0;
        }
    }
    
    private void IncrementSeventhsSinceOccurrence()
    {
        if (proceduralWeatherPool == null || proceduralWeatherPool.Count == 0) return;
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile == null) continue;
            if (!seventhsSinceLastOccurrence.ContainsKey(profile))
                seventhsSinceLastOccurrence[profile] = 0;
            
            if (activeWeatherProfile == profile)
            {
                // Reset counter for active weather
                seventhsSinceLastOccurrence[profile] = 0;
            }
            else
            {
                // Increase counter for all other weathers
                seventhsSinceLastOccurrence[profile] = Mathf.Clamp(seventhsSinceLastOccurrence[profile] + 1, 0, int.MaxValue);
            }
        }
    }
    
    private float GetVariationAdjustedWeight(WeatherProfileSO profile, float baseWeight)
    {
        if (profile == null || baseWeight <= 0f) return 0f;
        if (!seventhsSinceLastOccurrence.TryGetValue(profile, out int since)) since = 0;
        
        // Begin bonus once threshold is reached; at exactly threshold, add one step
        int eligibleSteps = since - variationSeventhsThreshold + 1;
        if (eligibleSteps <= 0) return baseWeight;
        
        float additiveBonus = eligibleSteps * variationWeightIncreasePerSeventh;
        float maxWeight = baseWeight * Mathf.Max(1f, variationMaxWeightMultiplier);
        float adjusted = baseWeight + additiveBonus;
        return Mathf.Min(adjusted, maxWeight);
    }
    #endregion
    
    #region Procedural Weather System
    /// <summary>
    /// Process procedural weather change based on retention and weighted selection
    /// </summary>
    private void ProcessProceduralWeatherChange()
    {
        DecrementWeatherCooldowns();
        
        if (activeWeatherProfile == null)
        {
            consecutiveRetentions = 0;
            SelectProceduralWeather(forcedChange: false);
            return;
        }
        
        // Calculate dynamic retention probability
        float baseRetention = activeWeatherProfile.retentionProbability;
        float probabilityDecay = consecutiveRetentions * probabilityDecayPerRetention;
        float effectiveRetention = Mathf.Max(0f, baseRetention - probabilityDecay);
        
        // Check if maximum consecutive retentions reached
        bool forceChange = maxConsecutiveRetentions > 0 && consecutiveRetentions >= maxConsecutiveRetentions;
        
        if (effectiveRetention <= 0f && !forceChange)
        {
            forceChange = true;
            if (logWeatherChanges)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather retention probability decayed to 0% after {consecutiveRetentions} retentions - forcing change",
                    "CelestialWeatherSystemLogic"
                );
            }
        }
        
        if (forceChange)
        {
            if (logWeatherChanges)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather forced to change: {activeWeatherProfile.weatherDisplayName} " +
                    $"(retentions: {consecutiveRetentions}/{maxConsecutiveRetentions}, decay: {probabilityDecay:F1}%)",
                    "CelestialWeatherSystemLogic"
                );
            }
            
            SetWeatherCooldown(activeWeatherProfile, weatherCooldownSevenths);
            consecutiveRetentions = 0;
            SelectProceduralWeather(forcedChange: true);
            return;
        }
        
        // Check retention probability
        float retentionRoll = UnityEngine.Random.Range(0f, 100f);
        
        if (retentionRoll <= effectiveRetention)
        {
            consecutiveRetentions++;
            
            if (logWeatherChanges)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather retained: {activeWeatherProfile.weatherDisplayName} " +
                    $"(roll: {retentionRoll:F1}% ≤ {effectiveRetention:F1}% [base {baseRetention:F0}% - decay {probabilityDecay:F1}%], " +
                    $"count: {consecutiveRetentions}/{maxConsecutiveRetentions})",
                    "CelestialWeatherSystemLogic"
                );
            }
            return;
        }
        
        // Retention failed, select new weather
        consecutiveRetentions = 0;
        
        if (logWeatherChanges)
        {
            GameLoggingSystem.Instance.LogEvent(
                $"Weather retention failed: {activeWeatherProfile.weatherDisplayName} " +
                $"(roll: {retentionRoll:F1}% > {effectiveRetention:F1}%) - selecting new weather",
                "CelestialWeatherSystemLogic"
            );
        }
        
        SelectProceduralWeather(forcedChange: false);
    }
    
    private void DecrementWeatherCooldowns()
    {
        if (weatherCooldowns.Count == 0) return;
        
        List<WeatherProfileSO> toRemove = new List<WeatherProfileSO>();
        
        foreach (var kvp in weatherCooldowns.ToList())
        {
            int remaining = kvp.Value - 1;
            
            if (remaining <= 0)
            {
                toRemove.Add(kvp.Key);
                if (logWeatherChanges)
                {
                    GameLoggingSystem.Instance.LogEvent(
                        $"Weather cooldown expired: {kvp.Key.weatherDisplayName} is now available",
                        "CelestialWeatherSystemLogic"
                    );
                }
            }
            else
            {
                weatherCooldowns[kvp.Key] = remaining;
            }
        }
        
        foreach (var weather in toRemove)
        {
            weatherCooldowns.Remove(weather);
        }
    }
    
    private void SetWeatherCooldown(WeatherProfileSO weather, int sevenths)
    {
        if (weather == null || sevenths <= 0) return;
        
        weatherCooldowns[weather] = sevenths;
        
        if (logWeatherChanges)
        {
            GameLoggingSystem.Instance.LogEvent(
                $"Weather on cooldown: {weather.weatherDisplayName} for {sevenths} sevenths",
                "CelestialWeatherSystemLogic"
            );
        }
    }
    
    private bool IsWeatherOnCooldown(WeatherProfileSO weather)
    {
        return weatherCooldowns.ContainsKey(weather) && weatherCooldowns[weather] > 0;
    }
    
    private void SelectProceduralWeather(bool forcedChange = false)
    {
        if (timeSystem == null) return;
        
        List<(WeatherProfileSO profile, float weight)> weightedPool = new List<(WeatherProfileSO profile, float weight)>();
        List<WeatherProfileSO> failedConditions = new List<WeatherProfileSO>();
        List<WeatherProfileSO> onCooldown = new List<WeatherProfileSO>();
        
        cachedPoolSet.Clear();
        
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile == null || profile.triggerWeight <= 0f) continue;
            if (profile == activeWeatherProfile) continue;
            if (forcedChange && profile == activeWeatherProfile) continue;
            
            if (IsWeatherOnCooldown(profile))
            {
                onCooldown.Add(profile);
                continue;
            }
            
            if (profile.AreConditionsMet())
            {
                float adjusted = GetVariationAdjustedWeight(profile, profile.triggerWeight);
                weightedPool.Add((profile, adjusted));
                cachedPoolSet.Add(profile);
            }
            else
            {
                failedConditions.Add(profile);
            }
        }
        
        // Log exclusions
        if (logWeatherChanges)
        {
            if (failedConditions.Count > 0)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather excluded by conditions ({failedConditions.Count}): {string.Join(", ", failedConditions.ConvertAll(p => p.weatherDisplayName))}",
                    "CelestialWeatherSystemLogic"
                );
            }
            
            if (onCooldown.Count > 0)
            {
                var cooldownInfo = onCooldown.ConvertAll(p => $"{p.weatherDisplayName} ({weatherCooldowns[p]}s)");
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather excluded by cooldown ({onCooldown.Count}): {string.Join(", ", cooldownInfo)}",
                    "CelestialWeatherSystemLogic"
                );
            }
        }
        
        // Fallback handling
        if (weightedPool.Count == 0)
        {
            if (fallbackWeather != null && fallbackWeather != activeWeatherProfile)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"No procedural weather meets conditions - using fallback: {fallbackWeather.weatherDisplayName}",
                    "CelestialWeatherSystemLogic"
                );
                SetWeatherProfileInternal(fallbackWeather, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
                return;
            }
            
            if (activeWeatherProfile != null && activeWeatherProfile.AreConditionsMet())
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"No new weather available - retaining current: {activeWeatherProfile.weatherDisplayName}",
                    "CelestialWeatherSystemLogic"
                );
                return;
            }
            
            GameLoggingSystem.Instance.LogEvent(
                $"No procedural weather available - keeping current",
                "CelestialWeatherSystemLogic"
            );
            return;
        }
        
        // Weighted selection
        WeatherProfileSO selectedWeather = SelectWeightedRandom(weightedPool);
        
        if (selectedWeather != null)
        {
            if (logWeatherChanges)
            {
                float totalWeight = GetTotalWeight(weightedPool);
                float selectedWeight = 0f;
                for (int i = 0; i < weightedPool.Count; i++)
                {
                    if (weightedPool[i].profile == selectedWeather)
                    {
                        selectedWeight = weightedPool[i].weight;
                        break;
                    }
                }
                float selectionProbability = totalWeight > 0 ? (selectedWeight / totalWeight * 100f) : 0f;
                
                GameLoggingSystem.Instance.LogEvent(
                    $"Procedural weather selected: {selectedWeather.weatherDisplayName} " +
                    $"(weight: {selectedWeight:F1}/{totalWeight:F1} = {selectionProbability:F1}%) " +
                    $"from {weightedPool.Count} candidates",
                    "CelestialWeatherSystemLogic"
                );
            }
            
            SetWeatherProfileInternal(selectedWeather, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
            consecutiveRetentions = 0;
        }
    }
    
    private float GetTotalWeight(List<(WeatherProfileSO profile, float weight)> pool)
    {
        float total = 0f;
        foreach (var item in pool)
        {
            total += item.weight;
        }
        return total;
    }
    
    private WeatherProfileSO SelectWeightedRandom(List<(WeatherProfileSO profile, float weight)> weightedPool)
    {
        if (weightedPool.Count == 0) return null;
        if (weightedPool.Count == 1) return weightedPool[0].profile;
        
        float totalWeight = 0f;
        foreach (var item in weightedPool)
        {
            totalWeight += item.weight;
        }
        
        if (totalWeight <= 0f)
        {
            int randomIndex = UnityEngine.Random.Range(0, weightedPool.Count);
            return weightedPool[randomIndex].profile;
        }
        
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;
        
        foreach (var item in weightedPool)
        {
            cumulativeWeight += item.weight;
            if (randomValue <= cumulativeWeight)
            {
                return item.profile;
            }
        }
        
        return weightedPool[weightedPool.Count - 1].profile;
    }
    #endregion
    
    #region Weather Profile Management
    private void AutoPopulateProceduralWeatherPool()
    {
        HashSet<WeatherProfileSO> manualProfiles = new HashSet<WeatherProfileSO>(proceduralWeatherPool);
        
        List<string> validNames = GameAssetValidator.GetAllWeatherProfileNames();
        foreach (string profileName in validNames)
        {
            WeatherProfileSO profile = GameAssetValidator.GetWeatherProfile(profileName, "CelestialWeatherSystemLogic");
            if (profile != null && !proceduralWeatherPool.Contains(profile))
            {
                proceduralWeatherPool.Add(profile);
            }
        }
        
        int autoAdded = proceduralWeatherPool.Count - manualProfiles.Count;
        
        GameLoggingSystem.Instance.LogEvent(
            $"Auto-populated procedural weather pool: {proceduralWeatherPool.Count} total " +
            $"({manualProfiles.Count} manual, {autoAdded} auto-loaded)",
            "CelestialWeatherSystemLogic"
        );
    }
    
    public static WeatherProfileSO FindWeatherProfile(string profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return null;
        return GameAssetValidator.GetWeatherProfile(profileName, "CelestialWeatherSystemLogic");
    }
    #endregion
    
    #region Public API
    /// <summary>
    /// Change active weather profile (hard-set, blocks procedural)
    /// </summary>
    public void SetWeatherProfile(WeatherProfileSO newProfile, bool ignoreEchoValidation = false)
    {
        SetWeatherProfileInternal(newProfile, ignoreEchoValidation, isHardSet: true, isDecaying: false);
    }
    
    /// <summary>
    /// Set weather profile from event consequences (can be permanent or temporary)
    /// </summary>
    public void SetWeatherProfileFromEvent(WeatherProfileSO newProfile, bool isPermanent, bool ignoreEchoValidation = false)
    {
        SetWeatherProfileInternal(newProfile, ignoreEchoValidation, isHardSet: true, isDecaying: !isPermanent);
        
        string weatherType = isPermanent ? "permanent" : "temporary";
        GameLoggingSystem.Instance.LogEvent(
            $"Event-triggered weather set: {newProfile.weatherDisplayName} ({weatherType})",
            "CelestialWeatherSystemLogic"
        );
    }
    
    /// <summary>
    /// Set weather for a limited duration
    /// </summary>
    public void SetTimedWeatherProfile(WeatherProfileSO newProfile, int durationSevenths, bool ignoreEchoValidation = false)
    {
        if (newProfile == null || durationSevenths <= 0)
        {
            Debug.LogWarning("[CelestialWeatherSystemLogic] Invalid timed weather parameters");
            return;
        }
        
        if (timedWeatherProfile == null)
        {
            weatherBeforeTimed = activeWeatherProfile;
        }
        
        timedWeatherProfile = newProfile;
        timedWeatherRemainingSevenths = durationSevenths;
        
        SetWeatherProfileInternal(newProfile, ignoreEchoValidation, isHardSet: true, isDecaying: false);
        
        GameLoggingSystem.Instance.LogEvent(
            $"Timed weather '{newProfile.weatherDisplayName}' set for {durationSevenths} sevenths",
            "CelestialWeatherSystemLogic"
        );
    }
    
    public void CancelTimedWeather()
    {
        if (timedWeatherProfile == null) return;
        
        GameLoggingSystem.Instance.LogEvent(
            $"Cancelled timed weather '{timedWeatherProfile.weatherDisplayName}'",
            "CelestialWeatherSystemLogic"
        );
        
        if (weatherBeforeTimed != null)
        {
            SetWeatherProfile(weatherBeforeTimed, ignoreEchoValidation: true);
        }
        
        timedWeatherProfile = null;
        weatherBeforeTimed = null;
        timedWeatherRemainingSevenths = 0;
    }
    
    /// <summary>
    /// Internal weather change with hard-set tracking
    /// </summary>
    private void SetWeatherProfileInternal(WeatherProfileSO newProfile, bool ignoreEchoValidation = false, bool isHardSet = false, bool isDecaying = false)
    {
        if (newProfile == null)
        {
            Debug.LogWarning("[CelestialWeatherSystemLogic] Attempted to set null weather profile");
            return;
        }
        
        // Validate echo compatibility
        if (!ignoreEchoValidation && timeSystem != null)
        {
            int currentEcho = timeSystem.CurrentEcho;
            if (!newProfile.IsValidForEcho(currentEcho))
            {
                Debug.LogWarning($"[CelestialWeatherSystemLogic] Weather profile '{newProfile.weatherDisplayName}' is not valid for Echo {currentEcho}");
                return;
            }
        }
        
        // Remove effects from current active profile BEFORE changing
        if (activeWeatherProfile != null)
        {
            RemoveWeatherEffects(activeWeatherProfile);
        }
        
        // Store previous for tracking
        previousWeatherProfile = activeWeatherProfile;
        activeWeatherProfile = newProfile;
        
        // Validate curves
        if (!newProfile.ValidateSeamlessCurves())
        {
            Debug.LogWarning($"[CelestialWeatherSystemLogic] Weather profile '{newProfile.name}' has non-seamless curves:\n{newProfile.GetCurveSeamlessStatus()}");
        }
        
        // Apply effects from new profile
        ApplyWeatherEffects(newProfile);
        
        // Update hard-set tracking
        isHardSetWeather = isHardSet;
        isDecayingWeather = isDecaying;
        
        // Reset minimum-duration lock for any new weather set
        weatherSelectionLockRemainingSevenths = Mathf.Max(0, minSeventhsBeforeDecay);
        pendingForcedChangeDueToInvalidation = false;
        // If we just changed weather, any pending timed revert should no longer apply
        if (timedWeatherProfile == null)
        {
            pendingTimedRevert = false;
        }
        
        // Reset variation counter for the newly active weather immediately (guaranteed variation reset)
        if (activeWeatherProfile != null)
        {
            if (!seventhsSinceLastOccurrence.ContainsKey(activeWeatherProfile))
            {
                seventhsSinceLastOccurrence[activeWeatherProfile] = 0;
            }
            else
            {
                seventhsSinceLastOccurrence[activeWeatherProfile] = 0;
            }
        }
        
        // Notify visual logic of weather change
        if (visualLogic != null)
        {
            visualLogic.OnWeatherChanged(newProfile);
        }
        
        // Trigger event
        OnWeatherChanged?.Invoke(newProfile);
        
        string setType = isHardSet ? (isDecaying ? "hard-set (decaying)" : "hard-set (permanent)") : "procedural";
        GameLoggingSystem.Instance.LogEvent(
            $"Weather changed to: {newProfile.weatherDisplayName} ({setType})",
            "CelestialWeatherSystemLogic"
        );
    }
    
    public void SetProceduralWeatherEnabled(bool enabled)
    {
        enableProceduralWeather = enabled;
        GameLoggingSystem.Instance.LogEvent(
            $"Procedural weather {(enabled ? "enabled" : "disabled")}",
            "CelestialWeatherSystemLogic"
        );
    }
    
    public void ResetToProceduralWeather()
    {
        isHardSetWeather = false;
        isDecayingWeather = false;
        GameLoggingSystem.Instance.LogEvent(
            "Hard-set weather cleared - procedural weather can now take over",
            "CelestialWeatherSystemLogic"
        );
    }
    
    public void ForceProceduralWeatherChange()
    {
        if (isHardSetWeather && !isDecayingWeather)
        {
            Debug.LogWarning("[CelestialWeatherSystemLogic] Cannot force procedural change - permanent hard-set weather is active.");
            return;
        }
        
        SelectProceduralWeather();
    }
    
    /// <summary>
    /// Clear all weather and return to procedural weather system
    /// </summary>
    public void ClearWeather()
    {
        // We swap back to default weather (or fallback) and apply minimum-duration lock
        GameLoggingSystem.Instance.LogEvent("Weather cleared - swapping to default and applying minimum-duration lock", "CelestialWeatherSystemLogic");
        
        // Clear timed weather tracking
        timedWeatherProfile = null;
        weatherBeforeTimed = null;
        timedWeatherRemainingSevenths = 0;
        pendingTimedRevert = false;
        pendingForcedChangeDueToInvalidation = false;
        
        WeatherProfileSO target = defaultWeatherProfile != null ? defaultWeatherProfile : fallbackWeather;
        if (target != null)
        {
            SetWeatherProfileInternal(target, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
        }
        else
        {
            // No default/fallback provided; select procedurally immediately
            SelectProceduralWeather();
        }
    }
    
    public void RegisterWeatherProfile(WeatherProfileSO profile)
    {
        if (profile == null)
        {
            Debug.LogWarning("[CelestialWeatherSystemLogic] Attempted to register null weather profile");
            return;
        }
        
        if (!GameAssetValidator.ValidateWeatherProfile(profile.name, "CelestialWeatherSystemLogic"))
        {
            Debug.LogWarning(
                $"[CelestialWeatherSystemLogic] Cannot register weather profile '{profile.name}' - " +
                $"it does not exist in Resources/WeatherProfiles."
            );
            return;
        }
        
        if (!proceduralWeatherPool.Contains(profile))
        {
            proceduralWeatherPool.Add(profile);
            GameLoggingSystem.Instance.LogEvent(
                $"Registered weather profile '{profile.weatherDisplayName}'",
                "CelestialWeatherSystemLogic"
            );
            // Initialize variation counter for new profile
            if (!seventhsSinceLastOccurrence.ContainsKey(profile))
            {
                seventhsSinceLastOccurrence[profile] = 0;
            }
        }
    }
    
    public void UnregisterWeatherProfile(WeatherProfileSO profile)
    {
        if (profile == null) return;
        
        proceduralWeatherPool.Remove(profile);
        GameLoggingSystem.Instance.LogEvent(
            $"Unregistered weather profile '{profile.weatherDisplayName}'",
            "CelestialWeatherSystemLogic"
        );
        // Remove from variation tracking
        if (seventhsSinceLastOccurrence.ContainsKey(profile))
        {
            seventhsSinceLastOccurrence.Remove(profile);
        }
    }
    
    public List<WeatherProfileSO> GetAvailableWeatherProfiles()
    {
        List<WeatherProfileSO> available = new List<WeatherProfileSO>();
        
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile != null && profile.AreConditionsMet())
            {
                available.Add(profile);
            }
        }
        
        return available;
    }
    
    public bool IsWeatherAvailable(WeatherProfileSO profile)
    {
        if (profile == null) return false;
        return profile.AreConditionsMet();
    }
    
    public string GetActiveWeatherEffectSummary()
    {
        if (activeWeatherProfile == null)
            return "No active weather";
        
        return $"{activeWeatherProfile.weatherDisplayName}\n{activeWeatherProfile.GetEffectSummary()}";
    }
    
    public string GetCurrentWeatherName()
    {
        return activeWeatherProfile != null ? activeWeatherProfile.name : "";
    }
    
    public bool IsWeatherActive(string weatherProfileName)
    {
        if (activeWeatherProfile == null || string.IsNullOrEmpty(weatherProfileName))
            return false;
        
        return string.Equals(activeWeatherProfile.name, weatherProfileName, StringComparison.OrdinalIgnoreCase);
    }
    
    public bool IsTimedWeatherActive()
    {
        return timedWeatherProfile != null;
    }
    
    public int GetTimedWeatherRemainingSevenths()
    {
        return timedWeatherProfile != null ? timedWeatherRemainingSevenths : 0;
    }
    
    public static List<string> GetAllWeatherProfileNames()
    {
        return GameAssetValidator.GetAllWeatherProfileNames();
    }
    
    // Provide access to visual logic for external systems
    public CelestialWeatherVisualLogic GetVisualLogic() => visualLogic;
    #endregion
    
    #region Debug Visualization
    private void OnGUI()
    {
        if (!showDebugInfo || !isInitialized) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        
        string weatherType = isHardSetWeather ? (isDecayingWeather ? "HARD-SET (DECAYING)" : "HARD-SET (PERMANENT)") : "PROCEDURAL";
        string timedInfo = timedWeatherProfile != null ? $" (TIMED: {timedWeatherRemainingSevenths}s)" : "";
        
        // Calculate effective retention with decay
        float baseRetention = activeWeatherProfile != null ? activeWeatherProfile.retentionProbability : 0f;
        float decay = consecutiveRetentions * probabilityDecayPerRetention;
        float effectiveRetention = Mathf.Max(0f, baseRetention - decay);
        
        string weatherInfo = activeWeatherProfile != null 
            ? $"{activeWeatherProfile.weatherDisplayName} [{weatherType}]{timedInfo}\n" +
              $"Weight: {activeWeatherProfile.triggerWeight} | Base Retention: {baseRetention:F0}%\n" +
              $"Effective Retention: {effectiveRetention:F1}% (decay: -{decay:F1}%)\n" +
              $"Consecutive: {consecutiveRetentions}/{maxConsecutiveRetentions}\n" +
              $"Active Cooldowns: {weatherCooldowns.Count}\n" +
              $"{(activeWeatherProfile.HasGameplayEffects() ? "Effects: Active" : "Effects: None")}"
            : "No active weather";
        
        TimeSystemLogic timeSystem = TimeSystemLogic.Instance;
        string timeInfo = timeSystem != null 
            ? $"Cycle: {timeSystem.CurrentCycle} | Echo: {timeSystem.CurrentEcho} | Phase: {timeSystem.CurrentPhase} | Seventh: {timeSystem.CurrentSeventh}"
            : "TimeSystem not available";
        
        string debugText = $"CELESTIAL WEATHER SYSTEM\n" +
                          $"─────────────────────────────\n" +
                          $"{timeInfo}\n" +
                          $"─────────────────────────────\n" +
                          $"Active Weather:\n{weatherInfo}\n" +
                          $"─────────────────────────────\n" +
                          $"Procedural: {(enableProceduralWeather ? "ENABLED" : "DISABLED")}\n" +
                          $"Hard-Set Block: {(isHardSetWeather ? "ACTIVE" : "NONE")}\n" +
                          $"Min Duration: {minSeventhsBeforeDecay} sevenths (remaining lock: {weatherSelectionLockRemainingSevenths})\n" +
                          $"Decay Rate: {probabilityDecayPerRetention:F1}% per retention\n" +
                          $"Cooldown Duration: {weatherCooldownSevenths} sevenths\n" +
                          $"Weather Pool: {proceduralWeatherPool.Count} profiles";
        
        GUI.Box(new Rect(10, 10, 350, 260), debugText, style);
    }
    #endregion
}
