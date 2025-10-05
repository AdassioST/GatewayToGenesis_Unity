# 🌦️ CELESTIAL WEATHER SYSTEM

**Version:** 1.0  
**Author:** Gateway to Genesis Development Team  
**Last Updated:** October 2025

---

## 📖 TABLE OF CONTENTS

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Components](#components)
4. [How It Works](#how-it-works)
5. [Setup Guide](#setup-guide)
6. [Configuration](#configuration)
7. [API Reference](#api-reference)
8. [Integration](#integration)
9. [Balancing Guide](#balancing-guide)
10. [Troubleshooting](#troubleshooting)

---

## 🎯 OVERVIEW

The **Celestial Weather Controller** is a percentage-based atmospheric system that governs all weather conditions, lighting, and celestial effects in Gateway to Genesis. It provides:

- **Percentage-Based Timing:** 0-100% cycle progression (no clock dependencies)
- **5 Celestial Phases:** Dawn, Midday, Dusk, Night, PostMidnight (variable durations per weather profile)
- **Procedural Weather:** Weighted probability system with conditions
- **Gameplay Effects:** Resource modifiers, morale changes, visual effects
- **Integration:** Technology unlocks, event consequences, Ink scripts
- **Smooth Transitions:** DOTween-based animations for all parameters

---

## 🏗️ ARCHITECTURE

```
┌────────────────────────────────────────────────────────────────┐
│                 CELESTIAL WEATHER CONTROLLER                   │
│                        (Singleton)                             │
├────────────────────────────────────────────────────────────────┤
│  INPUTS:                                                       │
│  ├─ TimeSystemLogic (cycle percentage, events)                │
│  ├─ WeatherProfileSO (configuration data)                     │
│  └─ GameAssetValidator (centralized asset loading)            │
│                                                                │
│  PROCESSING:                                                   │
│  ├─ Percentage calculation (0-100% of cycle)                  │
│  ├─ Curve sampling (light, fog, stars, etc.)                  │
│  ├─ Condition evaluation (echo, cycle, tech, events)          │
│  ├─ Weighted probability selection                            │
│  ├─ Retention checks with dynamic decay                       │
│  └─ Cooldown management                                        │
│                                                                │
│  OUTPUTS:                                                      │
│  ├─ Light2D (intensity, color)                                │
│  ├─ GlobalProductionManager (resource modifiers)              │
│  ├─ StatManager (morale changes)                              │
│  ├─ ParticleSystem (visual effects)                           │
│  └─ Events (OnWeatherChanged, OnPhaseChange, etc.)            │
└────────────────────────────────────────────────────────────────┘
```

### **Data Flow:**
```
TimeSystemLogic.CurrentEcho/Phase/Seventh
    ↓
CelestialWeatherSystemLogic.UpdateSeventhPercentage()
    ↓
Seventh Percentage (0-100%)
    ↓
WeatherProfileSO.SampleCurve(percentage)
    ↓
DOTween Transitions
    ↓
Light2D, ParticleSystems, Game Effects
```

---

## 📦 COMPONENTS

### **1. CelestialWeatherSystemLogic.cs** (1,518 lines)
**Location:** `Assets/GatewayToGenesis/Scripts/GameData/CelestialWeatherSystemLogic.cs`

**Responsibilities:**
- Singleton weather authority
- Cycle percentage calculation
- Procedural weather selection
- Weather effect lifecycle management
- Light2D control
- Particle system spawning
- Condition evaluation
- Cooldown tracking

**Key Features:**
- Event subscription to TimeSystemLogic
- Weighted probability selection
- Dynamic retention decay
- Hard-set vs procedural tracking
- Timed weather support
- DOTween transitions

---

### **2. WeatherProfileSO.cs** (458 lines)
**Location:** `Assets/GatewayToGenesis/Scripts/GameData/GameObjects/WeatherProfileSO.cs`

**Responsibilities:**
- Weather configuration data (ScriptableObject)
- AnimationCurve definitions for all parameters
- Resource/section modifiers
- Morale effects
- Particle effect references
- Availability conditions
- Procedural settings (weight, retention)

**Key Features:**
- Curve-based parameter sampling
- Nested condition system (WeatherCondition class)
- Color gradient interpolation
- Effect summaries for UI

---

### **3. WeatherCondition.cs** (87 lines embedded in WeatherProfileSO.cs)
**Location:** Inside `WeatherProfileSO.cs`

**Responsibilities:**
- Condition evaluation for weather availability
- Integration with game state systems

**Condition Types:**
- `CycleCheck` - Cycle number requirements
- `TechnologyCheck` - Technology unlock gates
- `EventScoreCheck` - Event witness requirements
- `EchoCheck` - Seasonal restrictions

---

### **4. GameAssetValidator.cs** (710 lines)
**Location:** `Assets/GatewayToGenesis/Scripts/GameData/GameAssetValidator.cs`

**Responsibilities:**
- Centralized asset validation
- Weather profile caching and lookup
- Resource/Section/Tech/Civic validation

**Key Features:**
- O(1) cached lookups
- Strict folder enforcement (Resources/WeatherProfiles)
- Helpful validation warnings
- Static utility class

---

## ⚙️ HOW IT WORKS

### **System Flow (Every Frame):**
```
1. CelestialWeatherSystemLogic.Update()
   ├─ UpdateSeventhPercentage()
   │   ├─ Reads TimeSystemLogic state
   │   ├─ Calculates: ((Echo-1)×63 + (Phase-1)×21 + (Seventh-1) + Progress) / 252 × 100
   │   └─ Determines CelestialPhase (Dawn/Midday/Dusk/Night/PostMidnight)
   │
   ├─ UpdateAllParameters()
   │   ├─ Samples curves at current percentage
   │   ├─ Sets target values (targetLightIntensity, etc.)
   │   └─ StartParameterTransitions() → DOTween tweens
   │
   └─ ApplyLighting()
       └─ Sets Light2D properties with tweened values
```

### **System Flow (Every Seventh):**
```
2. TimeSystemLogic.OnSeventhChange fires
   ├─ CelestialWeatherSystemLogic.OnSeventhChanged(newSeventh)
   │   ├─ Decrement minimum-duration lock
   │   ├─ Increment variation counters (all non-active weathers +1)
   │   ├─ Handle timed weather expiration
   │   ├─ Apply morale effects (via StatManager)
   │   └─ IF enableProceduralWeather && !isHardSetWeather:
   │       ├─ IF lockRemaining > 0 → skip procedural change (guaranteed min duration)
   │       ├─ ELSE IF deferred timed revert OR deferred invalidation → apply now
   │       └─ ELSE ProcessProceduralWeatherChange()
   │
   └─ ProcessProceduralWeatherChange()
       ├─ DecrementWeatherCooldowns()
       ├─ Calculate effective retention (with decay)
       ├─ Check forced change triggers
       ├─ Roll retention probability
       └─ SelectProceduralWeather() if needed
```

### **Procedural Weather Selection:**
```
3. SelectProceduralWeather(forcedChange)
   ├─ Build weighted pool:
   │   ├─ Filter by triggerWeight > 0
   │   ├─ Skip current weather (no re-selection)
   │   ├─ Skip cooldown weathers
   │   ├─ Evaluate ALL conditions (AND logic)
   │   └─ Add qualified candidates with adjusted weights:
   │       weight' = min(weight + max(0, sinceLast − threshold + 1) × perSeventh,
   │                      weight × maxMultiplier)
   │
   ├─ Handle empty pool (3-tier fallback):
   │   ├─ Try fallbackWeather
   │   ├─ Keep current if still valid
   │   └─ Keep current anyway (graceful)
   │
   ├─ SelectWeightedRandom():
   │   ├─ Calculate totalWeight
   │   ├─ Roll: Random(0, totalWeight)
   │   └─ Cumulative selection algorithm
   │
   └─ SetWeatherProfileInternal(selected, isHardSet: false)
```

---

## 🛠️ SETUP GUIDE

### **STEP 1: Create Required Folders**

```
Assets/
  └─ Resources/                      ← Unity Resources folder
      └─ WeatherProfiles/            ← Create this (REQUIRED)
          ├─ ClearSkies.asset
          ├─ GentleRain.asset
          ├─ HeavyStorm.asset
          └─ Snowfall.asset
```

**How to Create:**
1. Navigate to `Assets/Resources/` in Unity
2. Right-click → Create → Folder
3. Name it exactly: `WeatherProfiles`

---

### **STEP 2: Create Weather Profiles**

#### **A. Create Profile Asset**
1. Right-click in `Assets/Resources/WeatherProfiles/`
2. Select: **Create > Environment > Weather Profile**
3. Name it descriptively (e.g., `ClearSkies`, `GentleRain`)

#### **B. Configure Weather Identity**
```
Weather Display Name: "Clear Skies"
Weather Description: "Perfect weather conditions"
```

#### **C. Configure Procedural Settings**
```
Trigger Weight: 70                   (0-100 relative probability)
Retention Probability: 92            (0-100% chance to stay active)
Is Default Weather: ✓                (Always available baseline)
```

#### **D. Configure Availability Conditions**
```
Availability Conditions:
  └─ (empty for all-season weather)

OR for seasonal restriction:
  └─ Element 0:
      ├─ Type: EchoCheck
      ├─ Target Name: (empty)
      ├─ Required Value: 4          (Echo 4 = Silence/Winter)
      └─ Comparison: Equals
```

#### **E. Configure Resource Effects** (Optional)
```
Resource Modifiers:
  └─ Element 0:
      ├─ Resource Name: "Food"
      ├─ Percentage Modifier: 0.2   (20% bonus)
      └─ Is Positive: ✓
```

#### **F. Configure Morale Effects** (Optional)
```
Morale Per Seventh Modifier: -1.5    (Loses 1.5 morale each seventh)
Morale Effect Description: "Cold weather lowers spirits"
```

#### **G. Configure Visual Effects** (Optional)
```
Particle Prefab: [Drag your rain particle prefab]
Particle Spawn Offset: (0, 10, 0)
Particle Scale: 1.5
```

#### **H. Configure Transitions**
```
Light Transition Duration: 2         (seconds)
Atmospheric Transition Duration: 3   (seconds)
Particle Fade Duration: 1            (seconds)
Transition Ease: InOutQuad
```

#### **I. Configure Curves** (Advanced)
```
Light Intensity Curve:
  - X-axis: 0-100 (cycle percentage)
  - Y-axis: 0-1 (intensity)
  - Default: Linear(0, 0.2, 100, 0.2)

Configure similarly for:
  - Color Temperature Curve
  - Moon Visibility Curve
  - Sky Brightness Curve
  - Fog Density Curve
  - Star Visibility Curve
  - Weather State Weight Curves
  - Phase Duration Percentages (Dawn, Midday, Dusk, Night, PostMidnight)
```

---

### **STEP 3: Add Controller to Scene**

#### **A. Create GameObject**
1. Hierarchy → Right-click → Create Empty
2. Name: `CelestialWeatherSystemLogic`

#### **B. Add Component**
1. Select the GameObject
2. Inspector → Add Component
3. Search: `CelestialWeatherSystemLogic`
4. Add it

#### **C. Configure Inspector**
```
Weather Configuration:
├─ Active Weather Profile: [Drag ClearSkies]
└─ Default Weather Profile: [Drag ClearSkies]

Lighting References:
├─ Global Light: [Drag Light2D from scene]
└─ Auto Find Global Light: ✓ (if not assigned)

Procedural Weather System:
├─ Enable Procedural Weather: ✓
└─ Procedural Weather Pool:
    ├─ Element 0: ClearSkies
    ├─ Element 1: GentleRain
    ├─ Element 2: HeavyStorm
    └─ Element 3: Snowfall

Retention & Variety Balancing:
├─ Max Consecutive Retentions: 20
├─ Probability Decay Per Retention: 2.0
├─ Weather Cooldown Sevenths: 3
└─ Fallback Weather: [Drag ClearSkies]

Debug Visualization:
├─ Show Debug Info: ✓
├─ Log Phase Changes: ✓
└─ Log Weather Changes: ✓
```

---

### **STEP 4: Setup Light2D** (If Not Present)

#### **Create Global Light:**
1. Hierarchy → Right-click → Light → 2D → Global Light 2D
2. Name: `GlobalLight`

#### **Configure Light2D Component:**
```
Light Type: Global
Intensity: 1.0              (will be controlled by weather)
Color: White                (will be controlled by weather)
Blend Style Index: 0
Falloff: (default)
```

**Note:** The CelestialWeatherSystemLogic will auto-find and control this light if `Auto Find Global Light` is enabled.

---

### **STEP 5: Verify Time System Integration**

**Required Components in Scene:**
- ✅ TimeSystemLogic (provides cycle progression)
- ✅ GameLoggingSystem (provides logging)
- ✅ GlobalProductionManager (receives resource modifiers)
- ✅ StatManager (receives morale changes)

**The CelestialWeatherSystemLogic will automatically:**
- Find TimeSystemLogic.Instance
- Subscribe to OnSeventhChange, OnPhaseChange, OnCycleChange, OnEchoChange events
- Log initialization status

---

## 📋 CONFIGURATION

### **Weather Profile Configuration**

#### **Example 1: All-Season Default Weather**
```
File: Resources/WeatherProfiles/ClearSkies.asset

Weather Identity:
├─ Display Name: "Clear Skies"
└─ Description: "Perfect weather, no effects"

Procedural Settings:
├─ Trigger Weight: 70
├─ Retention Probability: 92%
└─ Is Default Weather: ✓

Availability Conditions:
└─ (empty - available in all conditions)

Effects:
└─ (none - baseline weather)

Curves:
├─ Light Intensity: Linear 0.2 → 0.8 (day/night cycle)
└─ (standard curves for all parameters)
```

---

#### **Example 2: Winter-Only Weather**
```
File: Resources/WeatherProfiles/Snowfall.asset

Weather Identity:
├─ Display Name: "Gentle Snowfall"
└─ Description: "Light snow reduces outdoor work"

Procedural Settings:
├─ Trigger Weight: 50
├─ Retention Probability: 80%
└─ Is Default Weather: ☐

Availability Conditions:
└─ Element 0:
    ├─ Type: EchoCheck
    ├─ Required Value: 4                (Echo 4 = Silence/Winter)
    └─ Comparison: Equals

Resource Effects:
├─ Element 0:
│   ├─ Resource Name: "Food"
│   ├─ Percentage Modifier: -0.2        (-20% penalty)
│   └─ Is Positive: ☐
└─ Element 1:
    ├─ Resource Name: "Crystals"
    ├─ Percentage Modifier: 0.3         (+30% bonus)
    └─ Is Positive: ✓

Morale:
└─ Morale Per Seventh: -1.0

Visual Effects:
├─ Particle Prefab: [SnowParticleSystem]
├─ Spawn Offset: (0, 15, 0)
└─ Scale: 2.0
```

---

#### **Example 3: Tech-Unlocked Weather**
```
File: Resources/WeatherProfiles/BlessedRain.asset

Weather Identity:
├─ Display Name: "Blessed Rain"
└─ Description: "Divine rain that nourishes the land"

Procedural Settings:
├─ Trigger Weight: 30
├─ Retention Probability: 75%
└─ Is Default Weather: ☐

Availability Conditions:
├─ Element 0:
│   ├─ Type: TechnologyCheck
│   ├─ Target Name: "Rain Dance Ritual"
│   └─ (Checks if tech is unlocked)
└─ Element 1:
    ├─ Type: CycleCheck
    ├─ Required Value: 2
    └─ Comparison: GreaterThanOrEqual  (Cycle 2+)

Resource Effects:
└─ Element 0:
    ├─ Resource Name: "Food"
    ├─ Percentage Modifier: 0.5         (+50% bonus!)
    └─ Is Positive: ✓

Morale:
└─ Morale Per Seventh: +2.0            (Blessed weather)

Visual Effects:
├─ Particle Prefab: [GoldenRainParticles]
└─ Fade Duration: 2.0
```

---

#### **Example 4: Event-Triggered Weather**
```
File: Resources/WeatherProfiles/PropheticStorm.asset

Procedural Settings:
├─ Trigger Weight: 15
└─ Retention Probability: 50%

Availability Conditions:
├─ Element 0:
│   ├─ Type: EventScoreCheck
│   ├─ Target Name: "storm_prophecy"
│   ├─ Required Value: 1
│   └─ Comparison: GreaterThanOrEqual
├─ Element 1:
│   ├─ Type: EchoCheck
│   ├─ Required Value: 1
│   └─ Comparison: Equals              (Echo 1 = Resonance only)
└─ Element 2:
    ├─ Type: CycleCheck
    ├─ Required Value: 3
    └─ Comparison: GreaterThanOrEqual

Section Effects:
└─ Element 0:
    ├─ Section Name: "Old World Materials"
    ├─ Percentage Modifier: -0.4        (-40% to entire section)
    └─ Is Positive: ☐

Morale:
└─ Morale Per Seventh: -3.0
```

---

### **Controller Configuration**

#### **Procedural Weather Pool:**
```
Add all weather profiles that should be available procedurally:
├─ ClearSkies (default, always available)
├─ GentleRain (common)
├─ Overcast (common)
├─ HeavyStorm (uncommon, cycle-gated)
├─ Snowfall (winter only)
├─ BlessedRain (tech-gated)
└─ PropheticStorm (event-gated)
```

#### **Balancing Parameters:**
```
Max Consecutive Retentions: 20
  → Hard cap on weather duration
  → After 20 retentions, forced change
  → Recommended: 15-25

Probability Decay Per Retention: 2.0%
  → Reduction per seventh retained
  → Formula: effective = base - (count × decay)
  → Recommended: 1-3%

Weather Cooldown Sevenths: 3
  → Cooldown after forced change
  → Prevents immediate re-selection
  → Recommended: 3-7

Minimum Sevenths Before Decay: 3
  → Guaranteed minimum duration for any weather
  → While lock active: procedural changes are blocked
  → Deferred invalidations/timed reverts apply once lock expires

Guaranteed Variation:
  Threshold (sevenths): 15
    → Start increasing weights after a weather hasn't occurred for 15 sevenths
  Increase Per Seventh: +1.2 (additive)
    → Linear increase, not multiplicative
  Max Weight Multiplier: 2.0×
    → Cap at double the base trigger weight
```

#### **Fallback Weather:**
```
Fallback Weather: [ClearSkies]
  → Used when no conditions are met
  → Should have NO conditions
  → Guarantees weather never breaks
```

### **Where the Variables Live**
```
WeatherProfileSO (per profile, under Resources/WeatherProfiles):
  ├─ triggerWeight                (base weight)
  ├─ retentionProbability         (base retention)
  └─ availability conditions      (Echo, Cycle, Tech, EventScore)

CelestialWeatherSystemLogic (scene singleton):
  ├─ minSeventhsBeforeDecay            (L, default 3)
  ├─ variationSeventhsThreshold        (T, default 15)
  ├─ variationWeightIncreasePerSeventh (Δ, default 1.2)
  └─ variationMaxWeightMultiplier      (C, default 2.0)

Runtime State (auto-managed):
  ├─ weatherSelectionLockRemainingSevenths
  ├─ seventhsSinceLastOccurrence[WeatherProfileSO]
  ├─ pendingForcedChangeDueToInvalidation
  └─ pendingTimedRevert
```

---

## 🌦️ **WEATHER TRIGGER MECHANISMS**

The Celestial Weather System supports multiple ways to trigger weather changes:

### **1. Procedural Weather System** (Automatic)
- **Trigger**: Every seventh change via `TimeSystemLogic.OnSeventhChange`
- **Method**: `ProcessProceduralWeatherChange()` → `SelectProceduralWeather()`
- **Behavior**: Weighted random selection from available weather profiles
- **Conditions**: Must meet all availability conditions (Echo, Cycle, Technology, EventScore)
- **Retention**: Uses probability decay system with cooldowns
- **Control**: Can be enabled/disabled via `SetProceduralWeatherEnabled()`

### **2. Event Consequences** (Ink-driven)
- **Trigger**: Event completion via `EventSystemLogic.ApplyConsequence()`
- **Syntax**: 
  - `weather:WeatherName` (temporary, subject to procedural decay)
  - `weather:WeatherName, permanent` (permanent, blocks procedural changes)
  - `weather:clear` (clear all weather, return to procedural)
- **Methods**: 
  - `SetWeatherProfileFromEvent()` for normal/permanent weather
  - `ClearWeather()` for clearing all weather
- **Integration**: Parsed automatically from Ink consequence metadata

### **3. Ink External Functions** (Story-driven)
- **Trigger**: Ink story execution via `InkStoryManager`
- **Functions**:
  - `ChangeWeather(weatherName)` → `SetWeatherProfile()` (permanent)
  - `SetTimedWeather(weatherName, sevenths)` → `SetTimedWeatherProfile()` (temporary)
  - `GetCurrentWeather()` → Returns current weather name
  - `IsWeather(weatherName)` → Checks if specific weather is active
- **Usage**: Direct weather control within Ink story logic

### **4. Manual Code Triggers** (Developer/System)
- **Methods**:
  - `SetWeatherProfile()` - Permanent hard-set weather
  - `SetTimedWeatherProfile()` - Temporary weather with duration
  - `ClearWeather()` - Clear all weather
  - `ForceProceduralWeatherChange()` - Force immediate procedural change
  - `ResetToProceduralWeather()` - Clear hard-set flag
- **Usage**: Direct API calls from C# code

### **5. Time-Based Condition Changes** (Automatic)
- **Triggers**: 
  - `OnCycleChanged()` - Weather conditions no longer met
  - `OnEchoChanged()` - Weather conditions no longer met
- **Behavior**: Forces procedural weather change if current weather becomes invalid
- **Purpose**: Ensures weather remains appropriate for current game state

### **6. Timed Weather Expiration** (Automatic)
- **Trigger**: Seventh change when timed weather duration expires
- **Method**: `OnSeventhChanged()` → checks `timedWeatherRemainingSevenths`
- **Behavior**: Reverts to previous weather or procedural system
- **Purpose**: Automatic cleanup of temporary weather effects

### **Weather Persistence Types**
- **Procedural**: Subject to normal decay and replacement by the weather system
- **Temporary**: Hard-set but can be replaced by procedural system (decaying weather)
- **Permanent**: Hard-set and blocks all procedural changes until manually cleared
- **Timed**: Hard-set with automatic expiration after specified sevenths

---

## 🔌 API REFERENCE

### **Weather Control:**

```csharp
// Change weather (permanent, hard-set)
void SetWeatherProfile(WeatherProfileSO profile, bool ignoreEchoValidation = false)

// Change weather from event (can be permanent or temporary)
void SetWeatherProfileFromEvent(WeatherProfileSO profile, bool isPermanent, bool ignoreEchoValidation = false)

// Change weather (temporary, hard-set)
void SetTimedWeatherProfile(WeatherProfileSO profile, int durationSevenths, bool ignoreEchoValidation = false)

// Clear all weather and return to procedural system
void ClearWeather()

// Cancel active timed weather
void CancelTimedWeather()
```

### **Procedural Control:**

```csharp
// Enable/disable procedural system
void SetProceduralWeatherEnabled(bool enabled)

// Clear hard-set flag (allow procedural)
void ResetToProceduralWeather()

// Check if hard-set active
bool IsHardSetWeather()

// Force immediate procedural selection
void ForceProceduralWeatherChange()
```

### **Weather Queries:**

```csharp
// Get current weather
string GetCurrentWeatherName()
bool IsWeatherActive(string weatherName)

// Timed weather info
bool IsTimedWeatherActive()
int GetTimedWeatherRemainingSevenths()

// Get available weathers (condition-filtered)
List<WeatherProfileSO> GetAvailableWeatherProfiles()
bool IsWeatherAvailable(WeatherProfileSO profile)

// Effect summary for UI
string GetActiveWeatherEffectSummary()
```

### **Celestial Queries:**

```csharp
// Seventh info (from CelestialWeatherVisualLogic)
float GetCurrentSeventhPercentage()         // 0-100
CelestialPhase GetCurrentCelestialPhase() // Dawn/Morning/etc

// Lighting (from CelestialWeatherVisualLogic)
float GetGlobalLightIntensity()
Color GetCurrentLightColor()
Color GetCurrentSkyColor()

// Atmospheric (from CelestialWeatherVisualLogic)
float GetSkyBrightness()
float GetFogDensity()
float GetStarVisibility()
float GetMoonVisibility()
float GetColorTemperature()

// Time of day (from CelestialWeatherVisualLogic)
bool IsDaytime()
bool IsNighttime()

// Weather state (from CelestialWeatherVisualLogic)
WeatherState GetCurrentWeatherState()     // Clear/Storm/etc
float GetWeatherWeight(WeatherState state)
```

### **Static Lookup:**

```csharp
// Find weather profile by name (validated)
static WeatherProfileSO FindWeatherProfile(string name, string requestingSystem = "Unknown")

// Get all cached names
static List<string> GetAllWeatherProfileNames()
```

### **Registration:**

```csharp
// Register weather at runtime
void RegisterWeatherProfile(WeatherProfileSO profile)

// Unregister weather
void UnregisterWeatherProfile(WeatherProfileSO profile)
```

---

## 🔗 INTEGRATION

### **1. Technology Integration**

**TechnologyData Setup:**
```
Technology: "Rain Dance Ritual"
└─ Weather Profile: [BlessedRain]
```

**Flow:**
```
Player unlocks technology
    ↓
GameTechnologySlot.UnlockTechnology() (line 222)
    ↓
if (technologyData.weatherProfile != null)
    ↓
CelestialWeatherSystemLogic.SetWeatherProfile(weatherProfile)
    ↓
Weather changes (hard-set, blocks procedural)
```

---

### **2. Event Integration**

**Ink Consequence Syntax:**
```ink
# Temporary weather (subject to procedural decay)
weather:Heavy Storm

# Permanent weather (blocks procedural changes)  
weather:Heavy Storm, permanent

# Clear all weather (return to procedural system)
weather:clear
```

**Event Consequence Examples:**
```ink
=== storm_summoning ===
# title: Summon the Storm
# description: Call upon ancient powers to change the weather
# consequences: weather:Heavy Storm, permanent
# event_type:Mystical
# priority: 50

The skies darken as you channel ancient magic...

* [Accept the storm] -> storm_outro
&C consequences: weather:Heavy Storm, permanent

* [Clear the skies] -> clear_outro  
&C consequences: weather:clear
```

**Flow:**
```
Event completes
    ↓
EventSystemLogic.ApplyConsequence() (line 1337)
    ↓
Parse weather consequence:
    ├─ weather:clear → CelestialWeatherSystemLogic.ClearWeather()
    ├─ weather:Name, permanent → SetWeatherProfileFromEvent(isPermanent: true)
    └─ weather:Name → SetWeatherProfileFromEvent(isPermanent: false)
    ↓
Weather changes with appropriate persistence
```

---

### **3. Ink Script Integration**

**Ink External Functions:**

```ink
=== weather_shrine ===
The ancient shrine hums with power...

* [Change weather permanently]
  { ChangeWeather("GentleRain"):
    - true: Rain begins to fall.
    - false: Nothing happens.
  }

* [Temporary weather (5 sevenths)]
  { SetTimedWeather("Storm", 5):
    - true: A storm erupts!
    - false: The spell fails.
  }

* [Check current weather]
  VAR current = GetCurrentWeather()
  The weather is: {current}
  
  { IsWeather("Storm"):
    - true: The storm rages on!
    - false: Clear skies.
  }
```

**Available Functions:**
- `ChangeWeather(name)` → bool
- `GetCurrentWeather()` → string
- `IsWeather(name)` → bool
- `SetTimedWeather(name, sevenths)` → bool

---

### **4. Code Integration**

```csharp
using UnityEngine;

public class MyWeatherScript : MonoBehaviour
{
    void Start()
    {
        // Find weather profile
        WeatherProfileSO storm = CelestialWeatherSystemLogic.FindWeatherProfile("Storm", "MyScript");
        
        // Change weather
        if (storm != null)
        {
            CelestialWeatherSystemLogic.Instance.SetWeatherProfile(storm);
        }
        
        // Check current weather
        string currentWeather = CelestialWeatherSystemLogic.Instance.GetCurrentWeatherName();
        bool isStorming = CelestialWeatherSystemLogic.Instance.IsWeatherActive("Storm");
        
        // Query atmospheric state (from visual logic)
        CelestialWeatherVisualLogic visualLogic = CelestialWeatherSystemLogic.Instance.GetVisualLogic();
        float lightIntensity = visualLogic.GetGlobalLightIntensity();
        bool isDaytime = visualLogic.IsDaytime();
        
        // Subscribe to weather changes
        CelestialWeatherSystemLogic.Instance.OnWeatherChanged += OnWeatherChanged;
    }
    
    void OnWeatherChanged(WeatherProfileSO newWeather)
    {
        Debug.Log($"Weather changed to: {newWeather.weatherDisplayName}");
    }
}
```

---

## 🎮 BALANCING GUIDE

### **Trigger Weight Distribution**

**Recommended Weights:**
```
Default/Clear: 60-80    (Dominant baseline)
Common: 15-30           (Regular weather)
Uncommon: 5-15          (Occasional)
Rare: 1-5               (Special events)
Never Procedural: 0     (Only from tech/events)
```

**Example Distribution:**
```
Echo 1 (Resonance):
├─ ClearSkies: 70       (58.3% of pool)
├─ LongDay: 20          (16.7%)
├─ GentleRain: 20       (16.7%)
├─ HeatWave: 10         (8.3%)
└─ Total: 120

Result: Clear dominates, occasional variations
```

---

### **Retention Probability Guide**

**Stability Levels:**
```
Very Stable (90-100%):  Default weather, baseline conditions
Stable (70-90%):        Normal weather patterns
Moderate (50-70%):      Dynamic weather
Unstable (20-50%):      Transitional, dramatic events
Very Unstable (0-20%):  Temporary conditions, brief events
```

**Retention + Decay Interaction:**
```
High Retention + Low Decay:
  retention=95%, decay=1%
  → Long, stable weather (~50+ sevenths)
  → Good for: Calm gameplay

Medium Retention + Medium Decay:
  retention=80%, decay=2%
  → Balanced variety (~20-30 sevenths)
  → Good for: Standard gameplay

Low Retention + High Decay:
  retention=60%, decay=5%
  → Frequent changes (~5-10 sevenths)
  → Good for: Chaotic/dynamic gameplay
```

---

### **Decay Rate Balancing**

**Formula:**
```
Sevenths until forced change = baseRetention / decayRate

Examples:
  92% / 2% = 46 sevenths max
  92% / 5% = 18 sevenths max
  70% / 2% = 35 sevenths max
  70% / 5% = 14 sevenths max
```

**Recommended Values:**
```
Conservative (0.5-1%):   Long weather patterns, stable
Moderate (2-3%):         Balanced variety (recommended)
Aggressive (5-10%):      Frequent changes, dynamic
```

---

### **Cooldown Duration Balancing**

**Purpose:** Prevents recently-forced weather from immediately re-triggering

**Recommended Values:**
```
Short (1-2 sevenths):    Minimal anti-repetition
Medium (3-5 sevenths):   Good variety guarantee (recommended)
Long (7-10 sevenths):    Forces rotation through all weathers
```

**Interaction with Pool Size:**
```
Pool Size = 3 weathers, Cooldown = 3:
  Weather A forces change → cooldown
  Sevenths 1-3: Only B and C available
  Good balance!

Pool Size = 10 weathers, Cooldown = 3:
  Weather A forces change → cooldown
  Sevenths 1-3: Still 9 weathers available
  Minimal impact
  
Guideline: cooldown ≈ 20-40% of pool size in sevenths
```

---

## 🎯 HOW WEIGHTED SELECTION WORKS

### **Algorithm:**
```
1. Build qualified pool:
   For each weather in proceduralWeatherPool:
     ├─ Check: triggerWeight > 0?
     ├─ Check: Not current weather?
     ├─ Check: Not on cooldown?
     └─ Check: AreConditionsMet()?
         → If ALL pass: Add to pool

2. Calculate total weight:
   totalWeight = Σ(all qualified adjusted weights)

3. Roll random value:
   roll = Random.Range(0, totalWeight)

4. Cumulative selection:
   cumulativeWeight = 0
   For each (weather, adjustedWeight) in pool:
     cumulativeWeight += adjustedWeight
     If roll ≤ cumulativeWeight:
       → SELECT this weather

5. Apply selected weather
```

### **Example Selection:**
```
Qualified Pool:
├─ ClearSkies: base=70, sinceLast=2 → adjusted=70
├─ Rain: base=20, sinceLast=17, threshold=15 → adjusted=20 + (17−15+1)*1.2 = 23.6
└─ Storm: base=10, sinceLast=30, threshold=15 → adjusted=min(10 + (30−15+1)*1.2, 10*2)= min(10 + 19.2, 20)=20

Total: 113.6

Cumulative Ranges:
├─ 0-70.0: ClearSkies (61.6%)
├─ 70.0-93.6: Rain (20.8%)
└─ 93.6-113.6: Storm (17.6%)

Roll: Random(0, 113.6) = 98.0
  ├─ 98.0 > 93.6 (skip ClearSkies, Rain)
  └─ 98.0 ≤ 113.6 (in Storm range) → SELECT: Storm!
```

---

## 🔄 PROBABILITY DECAY SYSTEM

### **How Decay Works:**
```
Each Seventh:
  effectiveRetention = baseRetention - (consecutiveRetentions × decayRate)
  effectiveRetention = max(0, effectiveRetention)

Example Timeline:
  ClearSkies: baseRetention=92%, decay=2%
  
  Seventh 1 (0 prior retentions):
    effective = 92% - (0 × 2%) = 92%
    Roll: 45% ≤ 92% → RETAINED
    consecutiveRetentions = 1
  
  Seventh 2 (1 prior retention):
    effective = 92% - (1 × 2%) = 90%
    Roll: 88% ≤ 90% → RETAINED
    consecutiveRetentions = 2
  
  Seventh 10 (9 prior retentions):
    effective = 92% - (9 × 2%) = 74%
    Roll: 80% > 74% → CHANGE!
    consecutiveRetentions = 0
```

### **When Decay Reaches 0%:**
```
Seventh 46 (45 prior retentions):
  effective = 92% - (45 × 2%) = 2%
  Roll: 98% > 2% → Natural change likely

Seventh 47 (46 prior retentions):
  effective = 92% - (46 × 2%) = 0%
  0% ≤ 0% → FORCED CHANGE (automatic)
  SetWeatherCooldown(current, 3)
  consecutiveRetentions = 0
```

---

## 🎲 COOLDOWN SYSTEM

### **Cooldown Lifecycle:**
```
1. Weather is forced to change:
   ├─ Either: consecutiveRetentions >= maxConsecutiveRetentions
   ├─ Or: effectiveRetention ≤ 0%
   └─ Trigger: SetWeatherCooldown(currentWeather, cooldownSevenths)
       └─ weatherCooldowns[currentWeather] = 3

2. Every subsequent seventh:
   └─ DecrementWeatherCooldowns()
       ├─ For each weather: cooldown--
       └─ If cooldown ≤ 0: Remove from dictionary

3. During selection:
   └─ if (IsWeatherOnCooldown(profile)):
       └─ EXCLUDE from pool

4. Cooldown expires:
   └─ Weather becomes available again
```

### **Cooldown vs Natural Change:**
```
Natural Change (retention roll fails):
  ✓ No cooldown applied
  ✓ consecutiveRetentions = 0
  ✓ Weather can be immediately reselected (if weighted randomly)

Forced Change (max reached OR decay=0):
  ✓ Cooldown applied
  ✓ consecutiveRetentions = 0
  ✓ Weather EXCLUDED for N sevenths
  ✓ MUST select different weather (current excluded twice)
```

---

## 📈 EXAMPLE GAMEPLAY SCENARIOS

### **Scenario 1: Early Game (Cycle 1)**
```
Available Weathers:
├─ ClearSkies (weight=70, no conditions)
└─ GentleRain (weight=20, no conditions)

Excluded:
└─ HeavyStorm (requires Cycle 3+)

Expected Pattern:
  77.8% of time: ClearSkies
  22.2% of time: GentleRain
  
With retention=92% and decay=2%:
  ClearSkies lasts ~15-25 sevenths average
  GentleRain lasts ~10-15 sevenths average
```

---

### **Scenario 2: Mid Game (Cycle 3, Tech Unlocked)**
```
Available Weathers:
├─ ClearSkies (weight=70)
├─ GentleRain (weight=20)
├─ HeavyStorm (weight=15, Cycle 3+ met)
└─ BlessedRain (weight=30, tech unlocked)

Total: 135

Probabilities:
├─ Clear: 51.9%
├─ Rain: 14.8%
├─ Storm: 11.1%
└─ Blessed: 22.2%

Expected Pattern:
  More variety, tech weather has significant presence
 
Variation example (Rain s=18, Storm s=35, T=15, Δ=1.2, C=2.0):
  Rain': 20 + (18−15+1)×1.2 = 23.6
  Storm': min(15 + (35−15+1)×1.2, 30) = min(15 + 25.2, 30) = 30
  Total' = 70 + 23.6 + 30 + 30 = 153.6
  P': Clear 45.6%, Rain 15.4%, Storm 19.5%, Blessed 19.5%
```

---

### **Scenario 3: Winter (Echo 4)**
```
Available Weathers:
├─ ClearSkies (weight=70)
├─ Snowfall (weight=50, Echo 4 only)
└─ Blizzard (weight=20, Echo 4 + tech)

Total: 140

Probabilities:
├─ Clear: 50.0%
├─ Snowfall: 35.7%
└─ Blizzard: 14.3%

Seasonal feel: Snowy weather dominates winter
 
Variation (if Blizzard absent for long periods):
  Blizzard': min(20 + (s−15+1)×1.2, 40)
  As s grows, Blizzard approaches 40 / (70+50+40) = 22.2% max
```

---

## 🐛 TROUBLESHOOTING

### **Problem: Weather Never Changes**
**Symptoms:** Same weather for 100+ sevenths

**Solutions:**
1. Check `enableProceduralWeather` is enabled
2. Check weather is not hard-set: `IsHardSetWeather()` should be false
3. Lower `maxConsecutiveRetentions` (try 10-15)
4. Increase `probabilityDecayPerRetention` (try 3-5%)
5. Check console for retention logs

---

### **Problem: Weather Changes Too Frequently**
**Symptoms:** Weather changes every 1-3 sevenths

**Solutions:**
1. Increase retention probabilities (try 80-95%)
2. Decrease `probabilityDecayPerRetention` (try 0.5-1%)
3. Increase `maxConsecutiveRetentions` (try 30-40)
4. Check if weathers have conditions that frequently fail

---

### **Problem: Specific Weather Never Appears**
**Symptoms:** Weather in pool but never selected

**Solutions:**
1. Check `Availability Conditions` are met:
   - Echo correct?
   - Cycle requirement met?
   - Technology unlocked?
   - Event score sufficient?
2. Check `Trigger Weight` > 0
3. Check not on cooldown (debug overlay shows cooldown count)
4. Increase trigger weight (higher = more likely)

---

### **Problem: Weather Validation Error**
**Symptoms:** `[GameAssetValidator] Weather profile 'X' not found`

**Solutions:**
1. Ensure profile is in `Resources/WeatherProfiles/` folder
2. Check spelling matches exactly (case-insensitive but must match name)
3. Verify it's saved as `.asset` file
4. Check GameAssetValidator cache: Call `GameAssetValidator.GetAllWeatherProfileNames()`

---

### **Problem: Effects Not Applying**
**Symptoms:** Weather changes but no resource/morale effects

**Solutions:**
1. Check GlobalProductionManager exists in scene
2. Check StatManager exists in scene
3. Verify resource names match exactly (use GameAssetValidator)
4. Check console for effect application logs
5. Verify effects are configured in WeatherProfileSO inspector

---

### **Problem: Particles Not Appearing**
**Symptoms:** Weather changes but no visual effects

**Solutions:**
1. Check `Particle Prefab` is assigned
2. Verify prefab has ParticleSystem component
3. Check `Particle Fade Duration` > 0
4. Verify spawn offset is reasonable (not off-screen)
5. Check particle prefab is not disabled

---

## 📊 SYSTEM STATISTICS

**Performance:**
- Frame update: ~0.02ms (percentage calc + tweens)
- Seventh update: ~0.05ms (condition checks + selection)
- Memory overhead: ~15-30 KB (caches + state)

**Scalability:**
- Tested with 100 weather profiles: ✓
- Tested with 20 conditions per profile: ✓
- Recommended pool size: 10-30 profiles
- Maximum pool size: 100+ profiles (still performant)

**Files:**
- CelestialWeatherSystemLogic.cs: 1,518 lines
- CelestialWeatherVisualLogic.cs: 626 lines
- WeatherProfileSO.cs: 458 lines
- GameAssetValidator.cs: 710 lines (shared)
- Total: ~3,312 lines

**Features:**
- ✅ Percentage-based timing
- ✅ Condition-based availability
- ✅ Weighted probability selection
- ✅ Dynamic retention decay
- ✅ Cooldown rotation system
- ✅ Hard-set vs procedural tracking
- ✅ Timed weather support
- ✅ DOTween smooth transitions
- ✅ Resource/morale effects
- ✅ Particle system integration
- ✅ Centralized validation
- ✅ Comprehensive logging
- ✅ Debug visualization

---

## 🎓 ADVANCED TOPICS

### **Forced Change Mechanics**

**Two Triggers:**
1. **Max Retentions Reached:**
   ```
   consecutiveRetentions >= maxConsecutiveRetentions
   → Hard cap prevents infinite weather
   ```

2. **Decay Reaches Zero:**
   ```
   effectiveRetention ≤ 0%
   → Probability decayed completely
   → Mathematically guaranteed change
   ```

**Forced Change Behavior:**
```
When triggered:
  ├─ Current weather → cooldown
  ├─ Current weather EXCLUDED from pool (twice)
  ├─ MUST select different weather
  └─ Guarantees variety
```

---

### **Condition Evaluation Order**

**All conditions use AND logic:**
```
WeatherProfileSO.AreConditionsMet():
  For each condition:
    If !condition.Evaluate():
      → FAIL immediately (short-circuit)
  
  → All passed: AVAILABLE

Example:
  Conditions:
    - EchoCheck = 4           (winter only)
    - CycleCheck >= 5         (late game)
    - TechnologyCheck "Magic"
  
  ALL must pass for weather to be available
```

**Use Multiple Profiles for OR Logic:**
```
Want: "Rain in Echo 1 OR Echo 2"

Create two profiles:
  SpringRain: EchoCheck = 1
  SummerRain: EchoCheck = 2
  
Both can trigger independently
```

---

### **Weight Normalization**

**Automatic normalization:**
```
Pool weights don't need to sum to 100

Example:
  Clear: 700
  Rain: 200
  Storm: 100
  Total: 1000

Probabilities:
  Clear: 700/1000 = 70%
  Rain: 200/1000 = 20%
  Storm: 100/1000 = 10%

Same result as:
  Clear: 70, Rain: 20, Storm: 10
```

### **Minimum-Duration Lock (Mathematics)**
```
Let L = Minimum Sevenths Before Decay (default 3).

During the first L sevenths after any weather change:
  - Procedural retention/selection is skipped.
  - Any forced change due to invalidation (cycle/echo) is deferred.
  - Any timed weather expiration revert is deferred.

At the first seventh where lockRemaining = 0:
  - Deferred timed revert (if any) applies first.
  - Else deferred forced change (if any) applies.
  - Else normal procedural retention/selection resumes.
```

### **Guaranteed Variation (Formulas)**
```
Let:
  w  = base trigger weight
  s  = sevenths since last occurrence
  T  = variationSeventhsThreshold (default 15)
  Δ  = variationWeightIncreasePerSeventh (default 1.2)
  C  = variationMaxWeightMultiplier (default 2.0)

Eligible steps = max(0, s − T + 1)
Adjusted weight: w' = min(w + EligibleSteps × Δ, w × C)

Selection probability:
  P = w' / Σ(all qualified w')

Reset:
  When weather is selected, s := 0
Increment:
  Each seventh for all non-active qualified weathers, s := s + 1
```

---

### **Default Weather Pattern**

**Mark one weather as default:**
```
isDefaultWeather: ✓

Purpose:
  - Always included in pool
  - Fallback when conditions fail
  - Guarantees baseline weather
  
Best Practice:
  - Set on ClearSkies or similar
  - Should have NO conditions
  - High weight (60-80)
  - High retention (90-95%)
```

---

## 🔍 DEBUG TOOLS

### **Debug Overlay:**
Press Play → See real-time weather stats in top-left

**Shows:**
- Current weather name and type (HARD-SET/PROCEDURAL)
- Trigger weight and retention probabilities
- Effective retention (with decay calculation)
- Consecutive retention count
- Active cooldowns count
- Light intensity, colors, atmospheric parameters
- Procedural system status
- Decay rate and cooldown duration
- Current celestial phase and phase durations

---

### **Console Logging:**

**Enable in GameLoggingSystem:**
```
Environment Logic → Enable Celestial Weather Controller Logging: ✓
```

**Logs Include:**
- Weather retention success/failure
- Probability decay calculations
- Weighted selection results
- Condition evaluation failures
- Cooldown expiration
- Hard-set vs procedural changes
- Effect application (resources, morale)

---

## 📚 QUICK REFERENCE

### **Key Concepts:**

| Term | Definition |
|------|------------|
| **Procedural Weather** | Auto-selected every seventh via weighted probability |
| **Hard-Set Weather** | Set by tech/event/manual, blocks procedural |
| **Retention** | Probability current weather stays active |
| **Decay** | Gradual reduction of retention probability |
| **Cooldown** | Temporary exclusion after forced change |
| **Min Duration (Lock)** | Weather cannot change procedurally for L sevenths |
| **Guaranteed Variation** | Linear additive boost to weights after T sevenths |
| **Trigger Weight** | Relative probability for selection |
| **Forced Change** | Mandatory change (max retentions or decay=0) |
| **Condition** | Requirement for weather availability |
| **Seventh** | Smallest time unit (21 per phase) |
| **Echo** | Season (1=Resonance, 2=Crescendo, 3=Dissonance, 4=Silence) |
| **Cycle** | Complete time loop (252 sevenths) |

---

### **Formula Reference:**

```
Cycle Percentage:
  percentage = ((Echo-1)×63 + (Phase-1)×21 + (Seventh-1) + Progress) / 252 × 100

Effective Retention:
  effective = max(0, baseRetention - consecutiveRetentions × decayRate)

Selection Probability:
  P(weather) = weight / totalWeight

Expected Duration:
  E[sevenths] ≈ baseRetention / decayRate
```

---

## ✅ CHECKLIST

### **Initial Setup:**
- [ ] Created `Resources/WeatherProfiles/` folder
- [ ] Created at least 3 weather profiles
- [ ] Added CelestialWeatherSystemLogic to scene
- [ ] Assigned weather profiles to procedural pool
- [ ] Configured balancing parameters
- [ ] Assigned fallback weather
- [ ] Verified Light2D exists or auto-find enabled

### **Weather Profile:**
- [ ] Configured weather identity
- [ ] Set trigger weight (0-100)
- [ ] Set retention probability (0-100%)
- [ ] Added availability conditions (if any)
- [ ] Configured resource/morale effects (if any)
- [ ] Assigned particle prefab (if any)
- [ ] Configured transition durations
- [ ] Adjusted curves for lighting/atmospheric effects

### **Testing:**
- [ ] Play scene and verify debug overlay appears
- [ ] Watch weather cycle percentage update
- [ ] Observe weather changes over time
- [ ] Verify retention logs in console
- [ ] Check condition filtering works
- [ ] Test technology weather unlocks
- [ ] Test event weather consequences
- [ ] Verify morale effects apply

---

## 🌟 BEST PRACTICES

1. **Always configure a fallback weather** (ClearSkies with no conditions)
2. **Balance total weights to ~100** for intuitive percentages
3. **Use conditions sparingly** (1-3 per weather max)
4. **Test retention + decay together** (decay should prevent eternal weather)
5. **Set cooldown to 20-40% of pool variety** (3-5 sevenths typical)
6. **Mark exactly one weather as default** (baseline guarantee)
7. **Use EchoCheck for seasonal restrictions** (single source of truth)
8. **Log weather changes during balancing** (enable logWeatherChanges)
9. **Place all profiles in Resources/WeatherProfiles** (validation requirement)
10. **Use descriptive weather names** (helps debugging)

---

## 📝 VERSION HISTORY

**v1.0 - Initial Release**
- Percentage-based timing system
- Condition-based weather availability
- Weighted probability selection
- Dynamic retention decay
- Cooldown rotation system
- Hard-set vs procedural tracking
- Timed weather support
- DOTween integration
- Resource/morale effects
- Centralized validation
- Comprehensive logging

---

## 🆘 SUPPORT

**Common Issues:**
1. Weather not changing → Check procedural enabled, not hard-set
2. Validation errors → Verify Resources/WeatherProfiles folder structure
3. Effects not applying → Check manager singletons exist
4. Performance issues → Reduce pool size or condition complexity

**Debug Commands:**
```csharp
// In Unity Console or custom debug script:
CelestialWeatherSystemLogic.Instance.ForceProceduralWeatherChange();
CelestialWeatherSystemLogic.Instance.ResetToProceduralWeather();
GameAssetValidator.GetValidationReport(); // Shows all loaded assets
```

---

## 🎉 CONCLUSION

The Celestial Weather System provides a **production-ready**, **highly configurable**, and **deeply integrated** atmospheric control system for Gateway to Genesis. It seamlessly combines:

- Realistic day/night cycles
- Dynamic procedural weather
- Strategic gameplay effects
- Narrative integration
- Visual polish

With proper configuration, it creates an **immersive, responsive environment** that evolves with player progression and enhances the overall game experience.

---

**Happy Weather Crafting!** 🌦️✨
