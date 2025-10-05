using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

/// <summary>
/// Visual rendering logic for celestial weather - handles lighting, curves, particles, and visual effects.
/// Separated from system logic for cleaner architecture.
/// </summary>
public class CelestialWeatherVisualLogic : MonoBehaviour
{
    #region Celestial Phase Definition
    public enum CelestialPhase
    {
        Dawn,           // Variable duration based on weather profile - Golden hour
        Midday,         // Variable duration based on weather profile - Bright day
        Dusk,           // Variable duration based on weather profile - Sunset
        Night,          // Variable duration based on weather profile - Dark night
        PostMidnight    // Variable duration based on weather profile - Late night
    }
    
    public enum WeatherState
    {
        Clear,
        Overcast,
        Storm,
        Mist,
        Precipitation
    }
    #endregion
    
    #region Serialized Fields
    [Header("Lighting References")]
    [SerializeField] private Light2D globalLight;
    [Tooltip("If true, automatically finds Light2D in scene if not assigned")]
    [SerializeField] private bool autoFindGlobalLight = true;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logPhaseChanges = true;
    #endregion
    
    #region Private State
    private TimeSystemLogic timeSystem;
    private WeatherProfileSO activeWeatherProfile;
    
    // Cached seventh percentage calculation (0-100% within current seventh)
    private float currentSeventhPercentage;
    private CelestialPhase currentPhase;
    private CelestialPhase previousPhase;
    
    // Performance optimization: cache phase boundaries
    private float[] cachedPhaseBoundaries;
    private WeatherProfileSO lastBoundaryWeather;
    private bool needsParameterUpdate = true;
    
    // Cached sampled values
    private float cachedLightIntensity;
    private float cachedColorTemperature;
    private float cachedMoonVisibility;
    private float cachedSkyBrightness;
    private float cachedFogDensity;
    private float cachedStarVisibility;
    
    private Color cachedSkyColor;
    private Color cachedLightColor;
    
    // Weather state blend weights
    private float clearWeight;
    private float overcastWeight;
    private float stormWeight;
    private float mistWeight;
    private float precipitationWeight;
    
    // Particle effect management
    private GameObject activeParticleInstance;
    private Tweener particleFadeTweener;
    
    // Weather blending tweeners and state
    private Tweener lightBlendTweener;
    private Tweener atmosBlendTweener;
    private float lightBlendT;   // 0 → from previous profile, 1 → to new profile
    private float atmosBlendT;   // 0 → from previous profile, 1 → to new profile
    private WeatherProfileSO blendFromProfile;
    private WeatherProfileSO blendToProfile;
    
    private bool isInitialized = false;
    #endregion
    
    #region Events
    public event Action<CelestialPhase> OnPhaseChange;
    public event Action<float> OnSeventhPercentageUpdate;
    #endregion
    
    #region Initialization
    public void InitializeWithWeather(WeatherProfileSO initialWeather)
    {
        // Get TimeSystemLogic reference
        timeSystem = TimeSystemLogic.Instance;
        if (timeSystem == null)
        {
            Debug.LogError("[CelestialWeatherVisualLogic] TimeSystemLogic not found! Visual system cannot function.");
            enabled = false;
            return;
        }
        
        // Auto-find global light if needed
        if (globalLight == null && autoFindGlobalLight)
        {
            globalLight = FindFirstObjectByType<Light2D>();
            if (globalLight != null)
            {
                GameLoggingSystem.Instance.LogEvent($"Auto-found Light2D: {globalLight.name}", "CelestialWeatherVisualLogic");
            }
        }
        
        if (globalLight == null)
        {
            Debug.LogWarning("[CelestialWeatherVisualLogic] Global Light is NULL - lighting will not work!");
        }
        
        // Subscribe to time system events
        timeSystem.OnSeventhChange += OnSeventhChanged;
        
        // Set initial weather
        activeWeatherProfile = initialWeather;
        
        // Initial calculation and synchronization
        UpdateSeventhPercentage();
        UpdateAllParameters();
        ApplyLighting();
        
        // Spawn initial particle effects
        if (activeWeatherProfile != null && activeWeatherProfile.HasVisualEffects())
        {
            SpawnParticleEffect(activeWeatherProfile);
        }
        
        isInitialized = true;
        
        GameLoggingSystem.Instance.LogEvent("Celestial Weather Visual Logic initialized", "CelestialWeatherVisualLogic");
        
        // Log initial lighting state
        if (globalLight != null && activeWeatherProfile != null)
        {
            GameLoggingSystem.Instance.LogEvent(
                $"Initial lighting: intensity={cachedLightIntensity:F3}, color=({cachedLightColor.r:F2},{cachedLightColor.g:F2},{cachedLightColor.b:F2})",
                "CelestialWeatherVisualLogic"
            );
        }
    }
    
    private void OnDestroy()
    {
        if (timeSystem != null)
        {
            timeSystem.OnSeventhChange -= OnSeventhChanged;
        }
        
        // Kill all active tweeners
        particleFadeTweener?.Kill();
        lightBlendTweener?.Kill();
        atmosBlendTweener?.Kill();
        
        // Clean up particles
        if (activeParticleInstance != null)
        {
            Destroy(activeParticleInstance);
        }
    }
    #endregion
    
    #region Core Update Loop
    private void Update()
    {
        if (!isInitialized || timeSystem == null) return;
        
        // Only update when necessary
        bool needsUpdate = UpdateSeventhPercentage();
        
        if (needsUpdate || needsParameterUpdate)
        {
            UpdateAllParameters();
            ApplyLighting();
            needsParameterUpdate = false;
        }
    }
    #endregion
    
    #region Seventh Percentage Calculation
    /// <summary>
    /// Calculate current position within the CURRENT SEVENTH as a percentage (0-100)
    /// </summary>
    private bool UpdateSeventhPercentage()
    {
        if (timeSystem == null) return false;
        
        // Get progress within current seventh from TimeSystemLogic
        float seventhProgress = timeSystem.GetSeventhProgress(); // 0.0 to 1.0
        float newPercentage = seventhProgress * 100f;
        
        // Only update if percentage actually changed
        if (Mathf.Abs(newPercentage - currentSeventhPercentage) < 0.01f)
        {
            return false;
        }
        
        currentSeventhPercentage = newPercentage;
        
        // Check for phase transitions
        CelestialPhase newPhase = GetPhaseFromPercentage(currentSeventhPercentage);
        if (newPhase != currentPhase)
        {
            previousPhase = currentPhase;
            currentPhase = newPhase;
            
            if (logPhaseChanges)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Phase changed: {previousPhase} → {currentPhase} at {currentSeventhPercentage:F2}% of seventh {timeSystem.CurrentSeventh}",
                    "CelestialWeatherVisualLogic"
                );
            }
            
            OnPhaseChange?.Invoke(currentPhase);
        }
        
        OnSeventhPercentageUpdate?.Invoke(currentSeventhPercentage);
        return true;
    }
    
    /// <summary>
    /// Determine celestial phase from percentage with weather-modified durations
    /// </summary>
    private CelestialPhase GetPhaseFromPercentage(float percentage)
    {
        float[] phaseBoundaries = GetWeatherModifiedPhaseBoundaries();
        
        if (percentage < phaseBoundaries[0]) return CelestialPhase.Dawn;
        if (percentage < phaseBoundaries[1]) return CelestialPhase.Midday;
        if (percentage < phaseBoundaries[2]) return CelestialPhase.Dusk;
        if (percentage < phaseBoundaries[3]) return CelestialPhase.Night;
        return CelestialPhase.PostMidnight;
    }
    
    /// <summary>
    /// Calculate weather-modified phase boundaries with caching
    /// </summary>
    private float[] GetWeatherModifiedPhaseBoundaries()
    {
        // If atmospheric blending is active, blend boundaries between profiles for smooth phase transitions
        bool isAtmosBlending = atmosBlendTweener != null && atmosBlendTweener.IsActive() && blendFromProfile != null && activeWeatherProfile != null;
        if (isAtmosBlending)
        {
            float t = atmosBlendT;
            float[] fromDur = blendFromProfile.GetNormalizedPhaseDurations();
            float[] toDur = activeWeatherProfile.GetNormalizedPhaseDurations();
            
            float fromDawnEnd = fromDur[0];
            float fromMiddayEnd = fromDawnEnd + fromDur[1];
            float fromDuskEnd = fromMiddayEnd + fromDur[2];
            float fromNightEnd = fromDuskEnd + fromDur[3];
            
            float toDawnEnd = toDur[0];
            float toMiddayEnd = toDawnEnd + toDur[1];
            float toDuskEnd = toMiddayEnd + toDur[2];
            float toNightEnd = toDuskEnd + toDur[3];
            
            float dawnEnd = Mathf.Lerp(fromDawnEnd, toDawnEnd, t);
            float middayEnd = Mathf.Lerp(fromMiddayEnd, toMiddayEnd, t);
            float duskEnd = Mathf.Lerp(fromDuskEnd, toDuskEnd, t);
            float nightEnd = Mathf.Lerp(fromNightEnd, toNightEnd, t);
            
            return new float[] { dawnEnd, middayEnd, duskEnd, nightEnd };
        }
        
        // Return cached boundaries if weather profile hasn't changed
        if (cachedPhaseBoundaries != null && lastBoundaryWeather == activeWeatherProfile)
        {
            return cachedPhaseBoundaries;
        }
        
        float[] boundaries;
        
        if (activeWeatherProfile != null)
        {
            float[] normalizedDurations = activeWeatherProfile.GetNormalizedPhaseDurations();
            
            float dawnEnd = normalizedDurations[0];
            float middayEnd = dawnEnd + normalizedDurations[1];
            float duskEnd = middayEnd + normalizedDurations[2];
            float nightEnd = duskEnd + normalizedDurations[3];
            
            boundaries = new float[] { dawnEnd, middayEnd, duskEnd, nightEnd };
            
            // Log normalization if it occurred
            float originalTotal = activeWeatherProfile.GetTotalPhasePercentage();
            if (Mathf.Abs(originalTotal - 100f) > 0.01f)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Weather '{activeWeatherProfile.weatherDisplayName}' phase percentages normalized: " +
                    $"{originalTotal:F1}% → 100.0%",
                    "CelestialWeatherVisualLogic"
                );
            }
        }
        else
        {
            // Default boundaries
            boundaries = new float[] { 20f, 50f, 70f, 90f };
        }
        
        cachedPhaseBoundaries = boundaries;
        lastBoundaryWeather = activeWeatherProfile;
        
        return boundaries;
    }
    #endregion
    
    #region Parameter Sampling
    /// <summary>
    /// Update all cached parameters by sampling curves at current seventh percentage
    /// </summary>
    private void UpdateAllParameters()
    {
        if (activeWeatherProfile == null) return;
        
        float p = currentSeventhPercentage;
        
        bool isLightBlending = lightBlendTweener != null && lightBlendTweener.IsActive() && blendFromProfile != null;
        bool isAtmosBlending = atmosBlendTweener != null && atmosBlendTweener.IsActive() && blendFromProfile != null;
        
        // Light intensity and color blending
        if (isLightBlending)
        {
            float fromIntensity = blendFromProfile.SampleCurve(blendFromProfile.lightIntensityCurve, p);
            float toIntensity = activeWeatherProfile.SampleCurve(activeWeatherProfile.lightIntensityCurve, p);
            cachedLightIntensity = Mathf.Lerp(fromIntensity, toIntensity, lightBlendT);
            
            Color fromColor = blendFromProfile.GetLightColor(p);
            Color toColor = activeWeatherProfile.GetLightColor(p);
            fromColor.a = 1f; toColor.a = 1f;
            cachedLightColor = Color.Lerp(fromColor, toColor, lightBlendT);
        }
        else
        {
            // Direct sampling
            cachedLightIntensity = activeWeatherProfile.SampleCurve(activeWeatherProfile.lightIntensityCurve, p);
            Color sampledColor = activeWeatherProfile.GetLightColor(p);
            sampledColor.a = 1f;
            cachedLightColor = sampledColor;
        }
        
        // Atmospheric parameters blending
        if (isAtmosBlending)
        {
            float fromCT = blendFromProfile.SampleCurve(blendFromProfile.colorTemperatureCurve, p);
            float toCT = activeWeatherProfile.SampleCurve(activeWeatherProfile.colorTemperatureCurve, p);
            cachedColorTemperature = Mathf.Lerp(fromCT, toCT, atmosBlendT);
            
            float fromMoon = blendFromProfile.SampleCurve(blendFromProfile.moonVisibilityCurve, p);
            float toMoon = activeWeatherProfile.SampleCurve(activeWeatherProfile.moonVisibilityCurve, p);
            cachedMoonVisibility = Mathf.Lerp(fromMoon, toMoon, atmosBlendT);
            
            float fromSkyB = blendFromProfile.SampleCurve(blendFromProfile.skyBrightnessCurve, p);
            float toSkyB = activeWeatherProfile.SampleCurve(activeWeatherProfile.skyBrightnessCurve, p);
            cachedSkyBrightness = Mathf.Lerp(fromSkyB, toSkyB, atmosBlendT);
            
            float fromFog = blendFromProfile.SampleCurve(blendFromProfile.fogDensityCurve, p);
            float toFog = activeWeatherProfile.SampleCurve(activeWeatherProfile.fogDensityCurve, p);
            cachedFogDensity = Mathf.Lerp(fromFog, toFog, atmosBlendT);
            
            float fromStars = blendFromProfile.SampleCurve(blendFromProfile.starVisibilityCurve, p);
            float toStars = activeWeatherProfile.SampleCurve(activeWeatherProfile.starVisibilityCurve, p);
            cachedStarVisibility = Mathf.Lerp(fromStars, toStars, atmosBlendT);
            
            // Weather weights
            float fromClear = blendFromProfile.SampleCurve(blendFromProfile.clearWeightCurve, p);
            float toClear = activeWeatherProfile.SampleCurve(activeWeatherProfile.clearWeightCurve, p);
            clearWeight = Mathf.Lerp(fromClear, toClear, atmosBlendT);
            
            float fromOvercast = blendFromProfile.SampleCurve(blendFromProfile.overcastWeightCurve, p);
            float toOvercast = activeWeatherProfile.SampleCurve(activeWeatherProfile.overcastWeightCurve, p);
            overcastWeight = Mathf.Lerp(fromOvercast, toOvercast, atmosBlendT);
            
            float fromStorm = blendFromProfile.SampleCurve(blendFromProfile.stormWeightCurve, p);
            float toStorm = activeWeatherProfile.SampleCurve(activeWeatherProfile.stormWeightCurve, p);
            stormWeight = Mathf.Lerp(fromStorm, toStorm, atmosBlendT);
            
            float fromMist = blendFromProfile.SampleCurve(blendFromProfile.mistWeightCurve, p);
            float toMist = activeWeatherProfile.SampleCurve(activeWeatherProfile.mistWeightCurve, p);
            mistWeight = Mathf.Lerp(fromMist, toMist, atmosBlendT);
            
            float fromPrecip = blendFromProfile.SampleCurve(blendFromProfile.precipitationWeightCurve, p);
            float toPrecip = activeWeatherProfile.SampleCurve(activeWeatherProfile.precipitationWeightCurve, p);
            precipitationWeight = Mathf.Lerp(fromPrecip, toPrecip, atmosBlendT);
            
            // Sky color blending
            Color fromSky = blendFromProfile.GetSkyColor(p);
            Color toSky = activeWeatherProfile.GetSkyColor(p);
            cachedSkyColor = Color.Lerp(fromSky, toSky, atmosBlendT);
        }
        else
        {
            // Direct sampling for atmospheric values
            cachedColorTemperature = activeWeatherProfile.SampleCurve(activeWeatherProfile.colorTemperatureCurve, p);
            cachedMoonVisibility = activeWeatherProfile.SampleCurve(activeWeatherProfile.moonVisibilityCurve, p);
            cachedSkyBrightness = activeWeatherProfile.SampleCurve(activeWeatherProfile.skyBrightnessCurve, p);
            cachedFogDensity = activeWeatherProfile.SampleCurve(activeWeatherProfile.fogDensityCurve, p);
            cachedStarVisibility = activeWeatherProfile.SampleCurve(activeWeatherProfile.starVisibilityCurve, p);
            
            // Weather weights
            clearWeight = activeWeatherProfile.SampleCurve(activeWeatherProfile.clearWeightCurve, p);
            overcastWeight = activeWeatherProfile.SampleCurve(activeWeatherProfile.overcastWeightCurve, p);
            stormWeight = activeWeatherProfile.SampleCurve(activeWeatherProfile.stormWeightCurve, p);
            mistWeight = activeWeatherProfile.SampleCurve(activeWeatherProfile.mistWeightCurve, p);
            precipitationWeight = activeWeatherProfile.SampleCurve(activeWeatherProfile.precipitationWeightCurve, p);
            
            // Sky color
            cachedSkyColor = activeWeatherProfile.GetSkyColor(p);
        }
    }
    
    /// <summary>
    /// Apply lighting parameters to Unity Light2D
    /// </summary>
    private void ApplyLighting()
    {
        if (globalLight == null) return;
        
        globalLight.intensity = cachedLightIntensity;
        
        // Set color but force alpha to 1.0
        Color lightColor = cachedLightColor;
        lightColor.a = 1f;
        globalLight.color = lightColor;
    }
    #endregion
    
    #region Particle Effects
    /// <summary>
    /// Spawn particle effect from weather profile with DOTween fade-in
    /// </summary>
    private void SpawnParticleEffect(WeatherProfileSO profile)
    {
        if (profile == null || profile.particlePrefab == null) return;
        
        Vector3 spawnPos = transform.position + profile.particleSpawnOffset;
        activeParticleInstance = Instantiate(profile.particlePrefab, spawnPos, Quaternion.identity);
        
        if (profile.particleScale != 1f)
        {
            activeParticleInstance.transform.localScale = Vector3.one * profile.particleScale;
        }
        
        activeParticleInstance.transform.SetParent(transform);
        
        // Fade in particle systems
        ParticleSystem[] particleSystems = activeParticleInstance.GetComponentsInChildren<ParticleSystem>();
        if (particleSystems.Length > 0 && profile.particleFadeDuration > 0f)
        {
            // Start particles at zero alpha
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                Color startColor = main.startColor.color;
                startColor.a = 0f;
                main.startColor = startColor;
            }
            
            // Fade in over duration
            float fadeProgress = 0f;
            particleFadeTweener?.Kill();
            particleFadeTweener = DOTween.To(() => fadeProgress, x => fadeProgress = x, 1f, profile.particleFadeDuration)
                .SetEase(profile.transitionEase)
                .OnUpdate(() =>
                {
                    if (activeParticleInstance != null)
                    {
                        foreach (var ps in particleSystems)
                        {
                            var main = ps.main;
                            Color color = main.startColor.color;
                            color.a = fadeProgress;
                            main.startColor = color;
                        }
                    }
                });
        }
        
        GameLoggingSystem.Instance.LogEvent(
            $"Spawned particle effect for weather: {profile.weatherDisplayName}",
            "CelestialWeatherVisualLogic"
        );
    }
    
    /// <summary>
    /// Fade out and destroy particle effect using DOTween
    /// </summary>
    private void FadeOutAndDestroyParticles(GameObject particleInstance, float fadeDuration)
    {
        if (particleInstance == null) return;
        
        if (fadeDuration <= 0f)
        {
            Destroy(particleInstance);
            return;
        }
        
        ParticleSystem[] particleSystems = particleInstance.GetComponentsInChildren<ParticleSystem>();
        if (particleSystems.Length == 0)
        {
            Destroy(particleInstance);
            return;
        }
        
        float fadeProgress = 1f;
        particleFadeTweener?.Kill();
        particleFadeTweener = DOTween.To(() => fadeProgress, x => fadeProgress = x, 0f, fadeDuration)
            .SetEase(Ease.OutQuad)
            .OnUpdate(() =>
            {
                if (particleInstance != null)
                {
                    foreach (var ps in particleSystems)
                    {
                        var main = ps.main;
                        Color color = main.startColor.color;
                        color.a = fadeProgress;
                        main.startColor = color;
                    }
                }
            })
            .OnComplete(() =>
            {
                if (particleInstance != null)
                {
                    Destroy(particleInstance);
                }
            });
    }
    #endregion
    
    #region Time System Event Handlers
    private void OnSeventhChanged(int newSeventh)
    {
        // Seventh changes provide natural update checkpoints
        needsParameterUpdate = true;
    }
    #endregion
    
    #region Weather Change Handler
    /// <summary>
    /// Called by CelestialWeatherSystemLogic when weather changes
    /// </summary>
    public void OnWeatherChanged(WeatherProfileSO newProfile)
    {
        if (newProfile == null) return;
        
        // Capture previous profile for blending
        WeatherProfileSO previousProfile = activeWeatherProfile;
        
        // Fade out old particles
        if (activeParticleInstance != null && activeWeatherProfile != null)
        {
            float fadeDuration = activeWeatherProfile.particleFadeDuration;
            FadeOutAndDestroyParticles(activeParticleInstance, fadeDuration);
            activeParticleInstance = null;
        }
        
        // Update active weather profile
        activeWeatherProfile = newProfile;
        
        // Clear phase boundary cache
        cachedPhaseBoundaries = null;
        lastBoundaryWeather = null;
        
        // Start blending tweens for light and atmospheric parameters
        lightBlendTweener?.Kill();
        atmosBlendTweener?.Kill();
        lightBlendT = 0f;
        atmosBlendT = 0f;
        blendFromProfile = previousProfile;
        blendToProfile = newProfile;
        
        float lightDuration = Mathf.Max(0f, newProfile.lightTransitionDuration);
        float atmosDuration = Mathf.Max(0f, newProfile.atmosphericTransitionDuration);
        Ease ease = newProfile.transitionEase;
        
        if (blendFromProfile != null && lightDuration > 0f)
        {
            lightBlendTweener = DOTween.To(() => lightBlendT, x => { lightBlendT = x; needsParameterUpdate = true; }, 1f, lightDuration)
                .SetEase(ease)
                .OnComplete(() => { lightBlendTweener = null; lightBlendT = 1f; });
        }
        else
        {
            lightBlendT = 1f;
            lightBlendTweener = null;
        }
        
        if (blendFromProfile != null && atmosDuration > 0f)
        {
            atmosBlendTweener = DOTween.To(() => atmosBlendT, x => { atmosBlendT = x; needsParameterUpdate = true; }, 1f, atmosDuration)
                .SetEase(ease)
                .OnComplete(() => { atmosBlendTweener = null; atmosBlendT = 1f; });
        }
        else
        {
            atmosBlendT = 1f;
            atmosBlendTweener = null;
        }
        
        // Initial update to apply start of blend without abrupt jump
        needsParameterUpdate = true;
        UpdateAllParameters();
        ApplyLighting();
        
        // Spawn new particle effects
        if (newProfile.HasVisualEffects())
        {
            SpawnParticleEffect(newProfile);
        }
        
        GameLoggingSystem.Instance.LogEvent(
            $"Visual effects updated for weather: {newProfile.weatherDisplayName}",
            "CelestialWeatherVisualLogic"
        );
    }
    #endregion
    
    #region Public API
    public float GetCurrentSeventhPercentage() => currentSeventhPercentage;
    public CelestialPhase GetCurrentCelestialPhase() => currentPhase;
    public float GetGlobalLightIntensity() => cachedLightIntensity;
    public float GetColorTemperature() => cachedColorTemperature;
    public float GetMoonVisibility() => cachedMoonVisibility;
    public float GetSkyBrightness() => cachedSkyBrightness;
    public float GetFogDensity() => cachedFogDensity;
    public float GetStarVisibility() => cachedStarVisibility;
    public Color GetCurrentSkyColor() => cachedSkyColor;
    public Color GetCurrentLightColor() => cachedLightColor;
    
    public Color GetSkyColorAtPercentage(float percentage)
    {
        if (activeWeatherProfile == null) return Color.white;
        return activeWeatherProfile.GetSkyColor(percentage);
    }
    
    public WeatherState GetCurrentWeatherState()
    {
        float maxWeight = Mathf.Max(clearWeight, overcastWeight, stormWeight, mistWeight, precipitationWeight);
        
        if (maxWeight == clearWeight) return WeatherState.Clear;
        if (maxWeight == overcastWeight) return WeatherState.Overcast;
        if (maxWeight == stormWeight) return WeatherState.Storm;
        if (maxWeight == mistWeight) return WeatherState.Mist;
        return WeatherState.Precipitation;
    }
    
    public float GetWeatherWeight(WeatherState state)
    {
        return state switch
        {
            WeatherState.Clear => clearWeight,
            WeatherState.Overcast => overcastWeight,
            WeatherState.Storm => stormWeight,
            WeatherState.Mist => mistWeight,
            WeatherState.Precipitation => precipitationWeight,
            _ => 0f
        };
    }
    
    public bool IsDaytime()
    {
        return currentPhase == CelestialPhase.Dawn || 
               currentPhase == CelestialPhase.Midday || 
               currentPhase == CelestialPhase.Dusk;
    }
    
    public bool IsNighttime()
    {
        return currentPhase == CelestialPhase.Night || 
               currentPhase == CelestialPhase.PostMidnight;
    }
    
    public Light2D GetGlobalLight() => globalLight;
    
    public void SetGlobalLight(Light2D light)
    {
        globalLight = light;
        GameLoggingSystem.Instance.LogEvent($"Global light manually set to: {(light != null ? light.name : "null")}", "CelestialWeatherVisualLogic");
    }
    
    public void MarkParametersForUpdate()
    {
        needsParameterUpdate = true;
    }
    
    public string GetCurrentPhaseDurations()
    {
        if (timeSystem == null) return "TimeSystem not available";
        
        float[] boundaries = GetWeatherModifiedPhaseBoundaries();
        float secondsPerSeventh = timeSystem.BaseSecondsPerSeventh;
        
        float dawnDuration = boundaries[0] / 100f * secondsPerSeventh;
        float middayDuration = (boundaries[1] - boundaries[0]) / 100f * secondsPerSeventh;
        float duskDuration = (boundaries[2] - boundaries[1]) / 100f * secondsPerSeventh;
        float nightDuration = (boundaries[3] - boundaries[2]) / 100f * secondsPerSeventh;
        float postMidnightDuration = (100f - boundaries[3]) / 100f * secondsPerSeventh;
        
        string weatherInfo = activeWeatherProfile != null ? $" (Weather: {activeWeatherProfile.name})" : " (No weather)";
        
        return $"Phase Durations{weatherInfo}:\n" +
               $"Dawn: {dawnDuration:F1}s ({boundaries[0]:F1}%)\n" +
               $"Midday: {middayDuration:F1}s ({boundaries[1] - boundaries[0]:F1}%)\n" +
               $"Dusk: {duskDuration:F1}s ({boundaries[2] - boundaries[1]:F1}%)\n" +
               $"Night: {nightDuration:F1}s ({boundaries[3] - boundaries[2]:F1}%)\n" +
               $"PostMidnight: {postMidnightDuration:F1}s ({100f - boundaries[3]:F1}%)";
    }
    #endregion
    
    #region Debug Visualization
    private void OnGUI()
    {
        if (!showDebugInfo || !isInitialized) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        
        string phaseInfo = "";
        if (activeWeatherProfile != null)
        {
            float[] normalizedDurations = activeWeatherProfile.GetNormalizedPhaseDurations();
            float originalTotal = activeWeatherProfile.GetTotalPhasePercentage();
            string normalizationStatus = Mathf.Abs(originalTotal - 100f) > 0.01f ? " (NORMALIZED)" : "";
            phaseInfo = $"\nPhase Durations{normalizationStatus}: Dawn:{normalizedDurations[0]:F1}%, Midday:{normalizedDurations[1]:F1}%, " +
                      $"Dusk:{normalizedDurations[2]:F1}%, Night:{normalizedDurations[3]:F1}%, PostMidnight:{normalizedDurations[4]:F1}%";
        }
        
        string weatherInfo = activeWeatherProfile != null 
            ? $"{activeWeatherProfile.weatherDisplayName}\n" +
              $"{(activeWeatherProfile.HasVisualEffects() ? "Particles: Active" : "Particles: None")}{phaseInfo}"
            : "No active weather";
        
        string debugText = $"CELESTIAL WEATHER VISUAL\n" +
                          $"─────────────────────────────\n" +
                          $"Seventh %: {currentSeventhPercentage:F2}%\n" +
                          $"Celestial Phase: {currentPhase}\n" +
                          $"─────────────────────────────\n" +
                          $"Active Weather:\n{weatherInfo}\n" +
                          $"─────────────────────────────\n" +
                          $"Light Intensity: {cachedLightIntensity:F3}\n" +
                          $"Color Temp: {cachedColorTemperature:F3}\n" +
                          $"Moon: {cachedMoonVisibility:F3} | Stars: {cachedStarVisibility:F3}\n" +
                          $"Sky Brightness: {cachedSkyBrightness:F3}\n" +
                          $"Fog Density: {cachedFogDensity:F3}\n" +
                          $"─────────────────────────────\n" +
                          $"Weather State: {GetCurrentWeatherState()}\n" +
                          $"Clear: {clearWeight:F2} | Overcast: {overcastWeight:F2}\n" +
                          $"Storm: {stormWeight:F2} | Mist: {mistWeight:F2}\n" +
                          $"Precip: {precipitationWeight:F2}";
        
        GUI.Box(new Rect(10, 280, 350, 280), debugText, style);
    }
    #endregion
}
