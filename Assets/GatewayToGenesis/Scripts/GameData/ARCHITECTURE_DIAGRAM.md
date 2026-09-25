# Celestial Weather System Architecture

Weather-specific architecture. For the project as a whole (effects, ledgers, catalogs), see `Scripts/ARCHITECTURE.md`.
Weather effects are ordinary `GameEffect`s: changing weather is `EffectRouter.RemoveSource("Weather: {old}")`
followed by `EffectRouter.ApplySet("Weather: {new}", effects)`.

## System Overview Diagram

```
┌───────────────────────────────────────────────────────────────────────────┐
│                         EXTERNAL TRIGGERS                                  │
├───────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐             │
│  │  Technologies  │  │  Event System  │  │  Ink Stories   │             │
│  │  (Hard-Set)    │  │  (Hard/Timed)  │  │  (Hard/Timed)  │             │
│  └───────┬────────┘  └───────┬────────┘  └───────┬────────┘             │
│          │                   │                    │                       │
│          └───────────────────┴────────────────────┘                       │
│                              │                                             │
│                              ▼                                             │
│         ┌────────────────────────────────────────────────────┐           │
│         │  SetWeatherProfile() / SetTimedWeatherProfile()    │           │
│         └────────────────────────────────────────────────────┘           │
│                              │                                             │
└──────────────────────────────┼─────────────────────────────────────────────┘
                               │
┌──────────────────────────────┼─────────────────────────────────────────────┐
│                              ▼                                             │
│    ┌─────────────────────────────────────────────────────────────────┐   │
│    │         CelestialWeatherSystemLogic (BRAIN)                     │   │
│    ├─────────────────────────────────────────────────────────────────┤   │
│    │                                                                  │   │
│    │  WEATHER SELECTION LOGIC:                                       │   │
│    │  ├── Procedural weather system                                  │   │
│    │  │   ├── Retention probability (with decay)                     │   │
│    │  │   ├── Weighted random selection                              │   │
│    │  │   ├── Cooldown system                                        │   │
│    │  │   └── Condition evaluation                                   │   │
│    │  ├── Hard-set weather tracking                                  │   │
│    │  ├── Timed weather system                                       │   │
│    │  └── Weather pool management                                    │   │
│    │                                                                  │   │
│    │  EFFECT MANAGEMENT (through EffectRouter):                      │   │
│    │  ├── RemoveWeatherEffects(old)                                  │   │
│    │  │   └── EffectRouter.RemoveSource("Weather: old")              │   │
│    │  └── ApplyWeatherEffects(new)                                   │   │
│    │      └── EffectRouter.ApplySet("Weather: new", effects)         │   │
│    │                                                                  │   │
│    │  TIME SYSTEM INTEGRATION:                                       │   │
│    │  ├── OnSeventhChange → Check timed weather / procedural        │   │
│    │  ├── OnCycleChange → Validate conditions                        │   │
│    │  └── OnEchoChange → Validate conditions                         │   │
│    │                                                                  │   │
│    └─────────────────────────────────────────────────────────────────┘   │
│                              │                                             │
│                              │ OnWeatherChanged(newProfile)                │
│                              ▼                                             │
│    ┌─────────────────────────────────────────────────────────────────┐   │
│    │      CelestialWeatherVisualLogic (RENDERER)                     │   │
│    ├─────────────────────────────────────────────────────────────────┤   │
│    │                                                                  │   │
│    │  SEVENTH PERCENTAGE CALCULATION:                                │   │
│    │  ├── UpdateSeventhPercentage()                                  │   │
│    │  │   └── Gets progress from TimeSystemLogic.GetSeventhProgress()│   │
│    │  └── GetPhaseFromPercentage()                                   │   │
│    │      └── Determines Dawn/Midday/Dusk/Night/PostMidnight         │   │
│    │                                                                  │   │
│    │  CURVE SAMPLING (Every Frame):                                  │   │
│    │  ├── Light intensity curve                                      │   │
│    │  ├── Color temperature curve                                    │   │
│    │  ├── Moon visibility curve                                      │   │
│    │  ├── Sky brightness curve                                       │   │
│    │  ├── Fog density curve                                          │   │
│    │  ├── Star visibility curve                                      │   │
│    │  └── Weather state weight curves                                │   │
│    │                                                                  │   │
│    │  RENDERING:                                                     │   │
│    │  ├── ApplyLighting() → Update Light2D                           │   │
│    │  ├── SpawnParticleEffect() → DOTween fade-in                    │   │
│    │  └── FadeOutAndDestroyParticles() → DOTween fade-out            │   │
│    │                                                                  │   │
│    └─────────────────────────────────────────────────────────────────┘   │
│                              │                                             │
└──────────────────────────────┼─────────────────────────────────────────────┘
                               │
┌──────────────────────────────┼─────────────────────────────────────────────┐
│                              ▼                                             │
│                    EFFECT TARGETS (via EffectRouter)                       │
├────────────────────────────────────────────────────────────────────────────┤
│  Each effect type has one handler that writes into the ledger of the       │
│  system that consumes it, keyed by (target, "Weather: {name}"):            │
│                                                                            │
│  • Pillar / Substat / Derived / MaxMorale / MoraleBalance /                │
│    SatisfactionThreshold / Morale  → StatManager.Modifiers                 │
│  • ResourceModifier                → GlobalProductionManager.ResourceModifiers
│  • ProductionModifier / ConstructionCost / ClickPower / ProductionScaling  │
│                                    → GameUnitsLogic ledgers                │
│  • HousingBonus                    → PopGrowthLogic.HousingModifiers       │
│                                                                            │
│  Removing the weather is one RemoveSource call across every ledger.        │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow Example: Storm Weather

```
SetWeatherProfileInternal(Storm)
├─ 1. EffectRouter.RemoveSource("Weather: Calm Winds")     → every ledger drops Calm Winds' entries
├─ 2. activeWeatherProfile = Storm
├─ 3. EffectRouter.ApplySet("Weather: Storm", Storm.effects)
│     ├─ PillarBonus +3 chorus       → StatManager.Modifiers["chorus"]["Weather: Storm"] = +3
│     │                                 → Recalculate: Chorus 10→13, Arcane/Secrecy follow
│     ├─ ResourceModifier -15% Global → ResourceModifiers["*"]["Weather: Storm"] = -15%
│     │                                 → every resource, including ones discovered later
│     └─ MaxMoraleModifier -10       → StatManager.Modifiers["maxmorale"]["Weather: Storm"] = -10
├─ 4. visualLogic.OnWeatherChanged(Storm)                  → curves, lighting, particles
└─ 5. OnWeatherChanged?.Invoke(Storm)
```

---

## Event Subscription Flow

### SystemLogic Subscriptions

```
TimeSystemLogic:
├─ OnSeventhChange
│  └─ CelestialWeatherSystemLogic.OnSeventhChanged()
│     ├─ Decrement timed weather counter
│     ├─ Expire timed weather if needed
│     └─ Trigger procedural weather change (if not hard-set)
│
├─ OnPhaseChange
│  └─ CelestialWeatherSystemLogic.OnTimePhaseChanged()
│     └─ Log phase change
│
├─ OnCycleChange
│  └─ CelestialWeatherSystemLogic.OnCycleChanged()
│     └─ Validate current weather still meets conditions
│     └─ Force new selection if conditions no longer met
│
└─ OnEchoChange
   └─ CelestialWeatherSystemLogic.OnEchoChanged()
      └─ Validate current weather still meets conditions
      └─ Force new selection if conditions no longer met
```

### VisualLogic Subscriptions

```
TimeSystemLogic:
└─ OnSeventhChange
   └─ CelestialWeatherVisualLogic.OnSeventhChanged()
      └─ Mark parameters for update
      └─ Next Update() will refresh visuals
```

### External Subscriptions

```
Other Systems Can Subscribe To:

CelestialWeatherSystemLogic:
└─ OnWeatherChanged(WeatherProfileSO newProfile)
   └─ Triggered when active weather changes
   └─ Use for: UI updates, special effects, gameplay triggers

CelestialWeatherVisualLogic:
├─ OnPhaseChange(CelestialPhase newPhase)
│  └─ Triggered on Dawn/Midday/Dusk/Night/PostMidnight transitions
│  └─ Use for: Time-of-day events, NPC behaviors
│
└─ OnSeventhPercentageUpdate(float percentage)
   └─ Triggered every frame when percentage changes
   └─ Use for: Smooth UI animations, progress bars
```

---

## Data Dependencies

```
┌────────────────────────────────────────────────────────────────┐
│                    TimeSystemLogic                             │
│  - CurrentSeventh                                              │
│  - CurrentCycle                                                │
│  - CurrentEcho                                                 │
│  - GetSeventhProgress() → 0.0 to 1.0                           │
└──────────┬─────────────────────────────────────────────────────┘
           │
           │ (read-only queries)
           │
           ├───────────────────────────────────────┐
           │                                       │
           ▼                                       ▼
┌──────────────────────────────┐    ┌──────────────────────────────┐
│  CelestialWeatherSystemLogic │    │ CelestialWeatherVisualLogic  │
│                              │    │                              │
│  Reads:                      │    │  Reads:                      │
│  • CurrentEcho               │    │  • GetSeventhProgress()      │
│  • CurrentCycle              │    │  • CurrentSeventh (logging)  │
│  • CurrentSeventh (logging)  │    │                              │
│                              │    │  Calculates:                 │
│  Owns:                       │    │  • currentSeventhPercentage  │
│  • activeWeatherProfile  ────┼────┼─ (shared read-only)         │
│                              │    │  • CelestialPhase            │
└──────────┬───────────────────┘    └──────────────────────────────┘
           │                                       │
           │ (effect application)                  │ (rendering)
           │                                       │
           ├────────────────┐                      │
           │                │                      │
           ▼                ▼                      ▼
    ┌───────────┐   ┌─────────────────┐   ┌──────────────┐
    │StatManager│   │GlobalProduction │   │   Light2D    │
    │           │   │    Manager      │   │              │
    │• Pillars  │   │• Resources      │   │• intensity   │
    │• Substats │   │• Production     │   │• color       │
    │• Derived  │   │• Modifiers      │   │              │
    │• Morale   │   │                 │   │              │
    └───────────┘   └─────────────────┘   └──────────────┘
```

---

## Update Loop Flow

### Frame-by-Frame Execution

```
EVERY FRAME:

CelestialWeatherVisualLogic.Update()
├─ if (!isInitialized || timeSystem == null) → Skip
│
├─ UpdateSeventhPercentage()
│  ├─ Get seventhProgress from TimeSystemLogic
│  ├─ Convert to percentage (0-100%)
│  ├─ Check if changed > 0.01%
│  │  └─ If no change → return false (skip update)
│  ├─ Determine new phase from percentage
│  ├─ Trigger OnPhaseChange if phase changed
│  └─ return true (update needed)
│
├─ If needsUpdate OR needsParameterUpdate:
│  ├─ UpdateAllParameters()
│  │  ├─ Sample all curves at currentSeventhPercentage
│  │  └─ Cache results
│  ├─ ApplyLighting()
│  │  ├─ Set globalLight.intensity
│  │  └─ Set globalLight.color (alpha=1.0)
│  └─ needsParameterUpdate = false
│
└─ (Particle tweens update automatically via DOTween)


EVERY SEVENTH (via event):

CelestialWeatherSystemLogic.OnSeventhChanged()
├─ If timed weather active:
│  ├─ Decrement counter
│  └─ Expire and revert if needed
│
└─ If procedural enabled AND not hard-set:
   └─ ProcessProceduralWeatherChange()
      ├─ Decrement cooldowns
      ├─ Calculate retention probability (with decay)
      ├─ Roll for retention vs change
      ├─ If change needed:
      │  ├─ SelectProceduralWeather()
      │  │  ├─ Evaluate conditions for all weathers
      │  │  ├─ Build weighted pool
      │  │  ├─ Weighted random selection
      │  │  └─ SetWeatherProfileInternal(selected)
      │  │     ├─ Remove old effects
      │  │     ├─ Apply new effects
      │  │     └─ Notify visual logic
      │  └─ consecutiveRetentions = 0
      └─ Else: consecutiveRetentions++


CelestialWeatherVisualLogic.OnSeventhChanged()
└─ needsParameterUpdate = true
   └─ Next Update() will refresh visuals
```

---

## Effect Application Order

Order does not matter. Stats are recomputed from bases and ledgers in dependency order
(pillars → substats → derived → thresholds) whenever any stat ledger changes, and production is recomputed
when its ledgers change. Every weather effect uses the source `Weather: {asset name}`, so removal is atomic.

## Thread Safety & Cleanup

### Singleton Pattern
`CelestialWeatherSystemLogic` derives from `SingletonBehaviour<T>` (first instance wins, later duplicates are
destroyed); setup lives in `OnSingletonAwake`, teardown in `OnSingletonDestroy`.

### Event Cleanup
```csharp
// SystemLogic.OnSingletonDestroy()
if (timeSystem != null)
{
    timeSystem.OnSeventhChange -= OnSeventhChanged;
    timeSystem.OnPhaseChange -= OnTimePhaseChanged;
    timeSystem.OnCycleChange -= OnCycleChanged;
    timeSystem.OnEchoChange -= OnEchoChanged;
}

// Remove all active weather effects
if (activeWeatherProfile != null)
{
    RemoveWeatherEffects(activeWeatherProfile);
}

// VisualLogic.OnDestroy()
if (timeSystem != null)
{
    timeSystem.OnSeventhChange -= OnSeventhChanged;
}

// Kill particle tweens
particleFadeTweener?.Kill();

// Destroy particle instances
if (activeParticleInstance != null)
{
    Destroy(activeParticleInstance);
}
```

**Result**: ✅ No memory leaks, proper cleanup

---

## API Surface

### Public APIs Exposed

#### CelestialWeatherSystemLogic
```csharp
// Singleton
static Instance

// Properties
ActiveWeatherProfile
IsHardSetWeather
IsProceduralWeatherEnabled

// Weather Management
SetWeatherProfile(profile, ignoreEchoValidation)
SetTimedWeatherProfile(profile, durationSevenths, ignoreValidation)
CancelTimedWeather()

// Registration
RegisterWeatherProfile(profile)
UnregisterWeatherProfile(profile)

// Procedural Control
SetProceduralWeatherEnabled(enabled)
ResetToProceduralWeather()
ForceProceduralWeatherChange()

// Queries
GetAvailableWeatherProfiles()
IsWeatherAvailable(profile)
GetActiveWeatherEffectSummary()
GetCurrentWeatherName()
IsWeatherActive(weatherName)
IsTimedWeatherActive()
GetTimedWeatherRemainingSevenths()

// Static Methods
FindWeatherProfile(profileName)
GetAllWeatherProfileNames()

// Access Visual System
GetVisualLogic()

// Events
OnWeatherChanged(WeatherProfileSO)
```

#### CelestialWeatherVisualLogic
```csharp
// Visual Queries
GetCurrentSeventhPercentage()      → 0-100%
GetCurrentCelestialPhase()         → Dawn/Midday/Dusk/Night/PostMidnight
GetGlobalLightIntensity()          → 0-1
GetColorTemperature()              → 0-1
GetMoonVisibility()                → 0-1
GetSkyBrightness()                 → 0-1
GetFogDensity()                    → 0-1
GetStarVisibility()                → 0-1
GetCurrentSkyColor()               → Color
GetCurrentLightColor()             → Color
GetSkyColorAtPercentage(%)         → Color
GetCurrentWeatherState()           → Clear/Overcast/Storm/Mist/Precipitation
GetWeatherWeight(state)            → 0-1

// Phase Utilities
IsDaytime()
IsNighttime()
GetCurrentPhaseDurations()

// Lighting Control
GetGlobalLight()
SetGlobalLight(light)

// System Control
MarkParametersForUpdate()

// Events
OnPhaseChange(CelestialPhase)
OnSeventhPercentageUpdate(float)
```

**Total**: 40+ public methods, all preserved from original!

## Status

- Weather effects go through the shared effect system; the old per-system application (which re-multiplied
  click power on every weather change) is gone.
- `CancelTimedWeather` now behaves exactly like natural expiry (the previous weather returns as procedural).
- `IsWeatherActive` accepts asset or display names, like every lookup.
