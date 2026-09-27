using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// System logic for weather management - handles weather selection, procedural system, and gameplay effects.
/// Separated from visual rendering concerns for cleaner architecture.
/// Gameplay effects go through <see cref="EffectRouter"/> under <see cref="WeatherProfileSO.GetModifierSourceName"/>,
/// so changing weather is one RemoveSource plus one ApplySet.
/// </summary>
public partial class CelestialWeatherSystemLogic : SingletonBehaviour<CelestialWeatherSystemLogic>
{
    private const LogChannel Log = LogChannel.Weather;

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
    
    // Weather that was forced to change sits out a few sevenths
    private readonly Countdowns<WeatherProfileSO> weatherCooldowns = new Countdowns<WeatherProfileSO>();
    
    
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
    
    
    #region Properties
    public WeatherProfileSO ActiveWeatherProfile => resolvedCapitalWeather ?? activeWeatherProfile;
    public bool IsHardSetWeather => isHardSetWeather;
    public bool IsProceduralWeatherEnabled => enableProceduralWeather;
    #endregion
    
    #region Initialization
    protected override void OnSingletonAwake()
    {
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
            GameLog.Error("TimeSystemLogic not found! Weather system cannot function.", Log);
            enabled = false;
            return;
        }
        


        
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
                GameLog.Event("Using 'Calm Winds' as initial weather (procedural)", Log);
            }
            else if (defaultWeatherProfile != null)
            {
                activeWeatherProfile = defaultWeatherProfile;
                isHardSetWeather = false;
                GameLog.Event($"Using default weather profile: {defaultWeatherProfile.weatherDisplayName} (procedural)", Log);
            }
            else if (proceduralWeatherPool.Count > 0)
            {
                activeWeatherProfile = proceduralWeatherPool[0];
                isHardSetWeather = false;
                GameLog.Event($"Using first weather from pool: {activeWeatherProfile.weatherDisplayName} (procedural)", Log);
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
            SyncCapitalWeather();
            // Apply initial minimum-duration lock so starting weather also respects minimum duration
            weatherSelectionLockRemainingSevenths = Mathf.Max(0, minSeventhsBeforeDecay);
            GameLog.Event($"Initial minimum-duration lock applied: {weatherSelectionLockRemainingSevenths} sevenths", Log);
        }
        
        isInitialized = true;
        
        GameLog.Event("Celestial Weather System Logic initialized", Log);
    }
    
    protected override void OnSingletonDestroy()
    {
        if (timeSystem != null)
        {
            timeSystem.OnSeventhChange -= OnSeventhChanged;
            timeSystem.OnPhaseChange -= OnTimePhaseChanged;
            timeSystem.OnCycleChange -= OnCycleChanged;
            timeSystem.OnEchoChange -= OnEchoChanged;
        }
        if (activeWeatherProfile != null) RemoveWeatherEffects(activeWeatherProfile);
    }
    #endregion
    
    #region Time System Event Handlers
    private void OnSeventhChanged(int newSeventh)
    {
        TickRegionalWeather();
        IncrementSeventhsSinceOccurrence();
        if (weatherSelectionLockRemainingSevenths > 0) weatherSelectionLockRemainingSevenths--;
        
        // Timed weather counts down; its revert waits for the minimum-duration lock.
        if (timedWeatherProfile != null)
        {
            if (timedWeatherRemainingSevenths > 0) timedWeatherRemainingSevenths--;
            if (timedWeatherRemainingSevenths <= 0)
            {
                if (weatherSelectionLockRemainingSevenths > 0)
                {
                    if (!pendingTimedRevert) GameLog.Event($"Timed weather '{timedWeatherProfile.weatherDisplayName}' expired; revert deferred by the minimum-duration lock ({weatherSelectionLockRemainingSevenths} left)", Log);
                    pendingTimedRevert = true;
                }
                else
                {
                    RevertTimedWeather();
                }
            }
        }
        
        // Deferred changes apply once the minimum-duration lock expires.
        if (weatherSelectionLockRemainingSevenths <= 0 && enableProceduralWeather && (!isHardSetWeather || isDecayingWeather))
        {
            if (pendingTimedRevert && timedWeatherProfile != null)
            {
                RevertTimedWeather();
                return;
            }
            if (pendingForcedChangeDueToInvalidation && timedWeatherProfile == null)
            {
                pendingForcedChangeDueToInvalidation = false;
                consecutiveRetentions = 0;
                GameLog.Event("Minimum-duration lock expired - applying deferred forced weather change", Log);
                SelectProceduralWeather();
                return;
            }
        }
        
        if (enableProceduralWeather && (!isHardSetWeather || isDecayingWeather) && timedWeatherProfile == null && weatherSelectionLockRemainingSevenths <= 0)
        {
            ProcessProceduralWeatherChange();
        }
    }
    
    private void RevertTimedWeather()
    {
        GameLog.Event($"Timed weather '{timedWeatherProfile.weatherDisplayName}' expired, reverting to previous weather", Log);
        var previous = weatherBeforeTimed;
        timedWeatherProfile = null;
        weatherBeforeTimed = null;
        pendingTimedRevert = false;
        if (previous != null) SetWeatherProfileInternal(previous, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
        else isHardSetWeather = false;
    }
    
    private void OnTimePhaseChanged(int newPhase)
    {
        GameLog.Event($"TimeSystem phase changed to {newPhase}", Log);
    }
    
    private void OnCycleChanged(int newCycle) => RevalidateActiveWeather($"cycle {newCycle}");
    
    private void OnEchoChanged(int newEcho) => RevalidateActiveWeather($"echo {newEcho}");
    
    /// <summary>Procedural (or decaying) weather whose conditions stop holding is replaced, respecting the minimum-duration lock.</summary>
    private void RevalidateActiveWeather(string reason)
    {
        if ((isHardSetWeather && !isDecayingWeather) || activeWeatherProfile == null || activeWeatherProfile.AreConditionsMet()) return;
        if (weatherSelectionLockRemainingSevenths > 0)
        {
            GameLog.Event($"'{activeWeatherProfile.weatherDisplayName}' no longer meets its conditions after {reason}; change deferred by the minimum-duration lock", Log);
            pendingForcedChangeDueToInvalidation = true;
            return;
        }
        GameLog.Event($"'{activeWeatherProfile.weatherDisplayName}' no longer meets its conditions after {reason}; selecting new weather", Log);
        consecutiveRetentions = 0;
        SelectProceduralWeather();
    }
    #endregion
    
    #region Weather Effect Lifecycle
    /// <summary>Apply a profile's gameplay effects under its source name (replacing anything it applied before).</summary>
    private void ApplyWeatherEffects(WeatherProfileSO profile)
    {
        if (profile == null) return;
        EffectRouter.ApplySet(profile.GetModifierSourceName(), profile.effects.Where(e => e != null).Select(e => e.ToEffect()));
        GameLog.Event($"Applied weather effects from profile: {profile.weatherDisplayName}", Log);
    }

    /// <summary>Remove every gameplay effect a profile applied, from every system.</summary>
    private void RemoveWeatherEffects(WeatherProfileSO profile)
    {
        if (profile == null) return;
        EffectRouter.RemoveSource(profile.GetModifierSourceName());
        GameLog.Event($"Removed weather effects from profile: {profile.weatherDisplayName}", Log);
    }
    #endregion
    
    #region Guaranteed Variation
    private void InitializeVariationTracking()
    {
        seventhsSinceLastOccurrence.Clear();
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile != null) seventhsSinceLastOccurrence[profile] = 0;
        }
    }
    
    /// <summary>The active weather's counter resets; every other weather's grows by one.</summary>
    private void IncrementSeventhsSinceOccurrence()
    {
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile == null) continue;
            seventhsSinceLastOccurrence.TryGetValue(profile, out int since);
            seventhsSinceLastOccurrence[profile] = profile == activeWeatherProfile ? 0 : since + 1;
        }
    }
    
    private float GetVariationAdjustedWeight(WeatherProfileSO profile)
    {
        seventhsSinceLastOccurrence.TryGetValue(profile, out int since);
        return WeatherRules.VariationWeight(profile.triggerWeight, since, variationSeventhsThreshold, variationWeightIncreasePerSeventh, variationMaxWeightMultiplier);
    }
    #endregion
    
    #region Procedural Weather System
    /// <summary>One seventh of procedural weather: cooldowns tick, then the active weather rolls to stay (see <see cref="WeatherRules"/>).</summary>
    private void ProcessProceduralWeatherChange()
    {
        foreach (var expired in weatherCooldowns.Tick()) GameLog.Event($"Cooldown over: {expired.weatherDisplayName} can be picked again", Log);
        
        if (activeWeatherProfile == null)
        {
            consecutiveRetentions = 0;
            SelectProceduralWeather();
            return;
        }
        
        float chance = WeatherRules.RetentionChance(activeWeatherProfile.retentionProbability, consecutiveRetentions, probabilityDecayPerRetention);
        float roll = UnityEngine.Random.Range(0f, 100f);
        switch (WeatherRules.CheckRetention(chance, consecutiveRetentions, maxConsecutiveRetentions, roll))
        {
            case WeatherRules.Retention.Kept:
                consecutiveRetentions++;
                GameLog.Event($"{activeWeatherProfile.weatherDisplayName} stays (roll {roll:F1} ≤ {chance:F1}%, {consecutiveRetentions}/{maxConsecutiveRetentions})", Log);
                return;
            case WeatherRules.Retention.Forced:
                GameLog.Event($"{activeWeatherProfile.weatherDisplayName} must change after {consecutiveRetentions} sevenths (chance {chance:F1}%); cooldown {weatherCooldownSevenths}", Log);
                weatherCooldowns.Start(activeWeatherProfile, weatherCooldownSevenths);
                break;
            default:
                GameLog.Event($"{activeWeatherProfile.weatherDisplayName} ends (roll {roll:F1} > {chance:F1}%)", Log);
                break;
        }
        consecutiveRetentions = 0;
        SelectProceduralWeather();
    }
    
    /// <summary>Pick new weather from the candidates by variation-adjusted weight; with none, fall back or keep the current one.</summary>
    private void SelectProceduralWeather()
    {
        if (timeSystem == null) return;
        
        var candidates = new List<WeatherProfileSO>();
        var weights = new List<float>();
        var excluded = new List<string>();
        foreach (var profile in proceduralWeatherPool)
        {
            if (profile == null) continue;
            var candidacy = WeatherRules.CheckCandidate(profile.triggerWeight, profile == activeWeatherProfile, weatherCooldowns.IsRunning(profile), profile.AreConditionsMet());
            if (candidacy == WeatherRules.Candidacy.Candidate)
            {
                candidates.Add(profile);
                weights.Add(GetVariationAdjustedWeight(profile));
            }
            else if (candidacy == WeatherRules.Candidacy.OnCooldown || candidacy == WeatherRules.Candidacy.ConditionsNotMet)
            {
                excluded.Add($"{profile.weatherDisplayName} ({candidacy})");
            }
        }
        if (excluded.Count > 0) GameLog.Event($"Not eligible: {string.Join(", ", excluded)}", Log);
        
        if (candidates.Count == 0)
        {
            if (fallbackWeather != null && fallbackWeather != activeWeatherProfile)
            {
                GameLog.Event($"No weather is eligible; using fallback {fallbackWeather.weatherDisplayName}", Log);
                SetWeatherProfileInternal(fallbackWeather, ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
            }
            else
            {
                GameLog.Event($"No weather is eligible; keeping {(activeWeatherProfile != null ? activeWeatherProfile.weatherDisplayName : "none")}", Log);
            }
            return;
        }
        
        int picked = WeatherRules.PickWeighted(weights, UnityEngine.Random.value);
        GameLog.Event($"Picked {candidates[picked].weatherDisplayName} ({WeatherRules.Chance(weights, picked):F1}% of {candidates.Count} candidates)", Log);
        SetWeatherProfileInternal(candidates[picked], ignoreEchoValidation: true, isHardSet: false, isDecaying: false);
        consecutiveRetentions = 0;
    }
    #endregion
    
    #region Weather Profile Management
    /// <summary>Every profile in Resources/WeatherProfiles joins the procedural pool; manually assigned ones are kept.</summary>
    private void AutoPopulateProceduralWeatherPool()
    {
        int manual = proceduralWeatherPool.Count;
        foreach (var profile in GameCatalog.Weather.All)
        {
            if (!proceduralWeatherPool.Contains(profile)) proceduralWeatherPool.Add(profile);
        }
        GameLog.Event($"Procedural weather pool: {proceduralWeatherPool.Count} profiles ({manual} manual, {proceduralWeatherPool.Count - manual} from Resources)", Log);
    }
    
    /// <summary>Weather by asset or display name (null when missing, with a warning).</summary>
    public static WeatherProfileSO FindWeatherProfile(string profileName)
    {
        var profile = GameCatalog.FindWeather(profileName);
        if (profile == null && !string.IsNullOrEmpty(profileName)) GameLog.Warning($"Weather profile '{profileName}' not found in Resources/WeatherProfiles.", Log);
        return profile;
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
        GameLog.Event($"Event-triggered weather set: {newProfile.weatherDisplayName} ({weatherType})", Log);
    }
    
    /// <summary>
    /// Set weather for a limited duration
    /// </summary>
    public void SetTimedWeatherProfile(WeatherProfileSO newProfile, int durationSevenths, bool ignoreEchoValidation = false)
    {
        if (newProfile == null || durationSevenths <= 0)
        {
            GameLog.Warning("Invalid timed weather parameters", Log);
            return;
        }
        
        if (timedWeatherProfile == null)
        {
            weatherBeforeTimed = activeWeatherProfile;
        }
        
        timedWeatherProfile = newProfile;
        timedWeatherRemainingSevenths = durationSevenths;
        
        SetWeatherProfileInternal(newProfile, ignoreEchoValidation, isHardSet: true, isDecaying: false);
        
        GameLog.Event($"Timed weather '{newProfile.weatherDisplayName}' set for {durationSevenths} sevenths", Log);
    }
    
    /// <summary>End timed weather now, exactly as if it had expired (the previous weather returns as procedural).</summary>
    public void CancelTimedWeather()
    {
        if (timedWeatherProfile == null) return;
        timedWeatherRemainingSevenths = 0;
        RevertTimedWeather();
    }
    
    /// <summary>
    /// Internal weather change with hard-set tracking
    /// </summary>
    private void SetWeatherProfileInternal(WeatherProfileSO newProfile, bool ignoreEchoValidation = false, bool isHardSet = false, bool isDecaying = false)
    {
        if (newProfile == null)
        {
            GameLog.Warning("Attempted to set null weather profile", Log);
            return;
        }
        
        // Validate echo compatibility
        if (!ignoreEchoValidation && timeSystem != null)
        {
            int currentEcho = timeSystem.CurrentEcho;
            if (!newProfile.IsValidForEcho(currentEcho))
            {
                GameLog.Warning($"Weather profile '{newProfile.weatherDisplayName}' is not valid for Echo {currentEcho}", Log);
                return;
            }
        }
        
        
        // Store previous for tracking
        previousWeatherProfile = activeWeatherProfile;
        activeWeatherProfile = newProfile;
        
        // Validate curves
        if (!newProfile.ValidateSeamlessCurves())
        {
            GameLog.Warning($"Weather profile '{newProfile.name}' has non-seamless curves:\n{newProfile.GetCurveSeamlessStatus()}", Log);
        }
        
        // Update hard-set tracking
        isHardSetWeather = isHardSet;
        isDecayingWeather = isDecaying;
        RefreshWeatherTiles();
        SyncCapitalWeather();
        
        // Reset minimum-duration lock for any new weather set
        weatherSelectionLockRemainingSevenths = Mathf.Max(0, minSeventhsBeforeDecay);
        pendingForcedChangeDueToInvalidation = false;
        // If we just changed weather, any pending timed revert should no longer apply
        if (timedWeatherProfile == null)
        {
            pendingTimedRevert = false;
        }
        
        // Guaranteed variation: the newly active weather has just occurred.
        seventhsSinceLastOccurrence[activeWeatherProfile] = 0;
        
        
        string setType = isHardSet ? (isDecaying ? "hard-set (decaying)" : "hard-set (permanent)") : "procedural";
        GameLog.Event($"Weather changed to: {newProfile.weatherDisplayName} ({setType})", Log);
    }
    
    public void SetProceduralWeatherEnabled(bool enabled)
    {
        enableProceduralWeather = enabled;
        GameLog.Event($"Procedural weather {(enabled ? "enabled" : "disabled")}", Log);
    }
    
    public void ResetToProceduralWeather()
    {
        isHardSetWeather = false;
        isDecayingWeather = false;
        RefreshWeatherTiles();
        SyncCapitalWeather();
        GameLog.Event("Hard-set weather cleared - procedural weather can now take over", Log);
    }
    
    public void ForceProceduralWeatherChange()
    {
        if (isHardSetWeather && !isDecayingWeather)
        {
            GameLog.Warning("Cannot force procedural change - permanent hard-set weather is active.", Log);
            return;
        }
        
        SelectProceduralWeather();
    }
    
    /// <summary>
    /// Clear all weather and return to procedural weather system
    /// </summary>
    public void ClearWeather()
    {
        regionalWeather.Clear();
        RefreshWeatherTiles();
        // We swap back to default weather (or fallback) and apply minimum-duration lock
        GameLog.Event("Weather cleared - swapping to default and applying minimum-duration lock", Log);
        
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
            GameLog.Warning("Attempted to register null weather profile", Log);
            return;
        }
        
        if (!GameCatalog.Weather.Contains(profile.name))
        {
            GameLog.Warning($"Cannot register weather profile '{profile.name}' - it does not exist in Resources/WeatherProfiles.", Log);
            return;
        }
        
        if (!proceduralWeatherPool.Contains(profile))
        {
            proceduralWeatherPool.Add(profile);
            GameLog.Event($"Registered weather profile '{profile.weatherDisplayName}'", Log);
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
        GameLog.Event($"Unregistered weather profile '{profile.weatherDisplayName}'", Log);
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
        if (ActiveWeatherProfile == null)
            return "No active weather";
        
        return $"{ActiveWeatherProfile.weatherDisplayName}\n{ActiveWeatherProfile.GetEffectSummary()}";
    }
    
    public string GetCurrentWeatherName()
    {
        return ActiveWeatherProfile != null ? ActiveWeatherProfile.name : "";
    }
    
    /// <summary>True when the active weather has this asset name or display name (the same names lookups accept).</summary>
    public bool IsWeatherActive(string weatherProfileName)
    {
        if (ActiveWeatherProfile == null || string.IsNullOrEmpty(weatherProfileName)) return false;
        return string.Equals(ActiveWeatherProfile.name, weatherProfileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActiveWeatherProfile.weatherDisplayName, weatherProfileName, StringComparison.OrdinalIgnoreCase);
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
        return new List<string>(GameCatalog.Weather.Keys);
    }
    
    // Provide access to visual logic for external systems
    public CelestialWeatherVisualLogic GetVisualLogic() => visualLogic;
    #endregion
    
    #region Debug Visualization
    private GUIStyle debugStyle;

    private void OnGUI()
    {
        if (!showDebugInfo || !isInitialized) return;
        
        if (debugStyle == null)
        {
            debugStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
            debugStyle.normal.textColor = Color.white;
        }
        GUIStyle style = debugStyle;
        
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
