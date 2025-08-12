# 🎮 Event System - Complete Technical Documentation

## 📋 **Overview**

This document provides comprehensive technical documentation for the Gateway to Genesis Event System, including all implemented features: population integration, event pause systems, vignette transparency, metadata-driven UI, technology-event integration, priority systems, random selection, and dynamic resource management.

---

## 🧩 **System Architecture**

### **🎯 Core Components**
- **`EventSystemLogic`**: Central coordinator managing event lifecycle, system integration, and time-based triggering
- **`EventVolumeManager`**: Manages story volumes, Ink integration, external function binding, and random event selection
- **`EventScreenManager`**: Handles UI display, screen transitions, vignette transparency, and progressive text reveal
- **`InkDrivenEventSetup`**: Automatically parses Ink files, extracts metadata, and creates structured story nodes
- **`PopGrowthLogic`**: Manages population, housing, vagrants, and dual death tracking systems with event pause integration

### **🎨 Screen Types & Flow**
- **Splash**: Introduction screen with metadata-driven content, art, and visual elements
- **Verse**: Narrative text with progressive reveal mechanics and manual progression
- **Chorus**: Decision points with auto-advance functionality
- **Bridge**: Transition screens that advance on any player input
- **Outro**: Results/conclusion with detailed consequences display and score tracking

---

## 🏗️ **Population Integration System**

### **🎯 New Condition Types**
- **`PopulationCheck`** - Check current population amount with comparison operators
- **`HousingCheck`** - Check current housing amount with comparison operators
- **`VagrantsCheck`** - Check current vagrants amount with comparison operators
- **`DeathsCheck`** - Check current public death records (can be revised by events)
- **`VagrantDeathsCheck`** - Check current vagrant deaths amount
- **`TrueDeathsCheck`** - Check true accumulated deaths (cannot be revised, always ≥ deaths)

### **🎯 New Consequence Types**
- **`PopulationChange`** - Only for REMOVING population (registers deaths, updates both deaths and trueDeaths)
- **`HousingChange`** - Modify housing amount (automatically converts excess population to vagrants if reduced)
- **`VagrantsChange`** - Modify vagrants amount (can become population when housing is available)
- **`DeathsChange`** - Process event deaths (kill population, track deaths in both systems)
- **`DeathRecordsRevision`** - Revise public death records for evil empire history manipulation

### **🎯 Usage in Ink Files**
```ink
# conditions: population:population >= 10; housing:housing >= 15; true_deaths:true_deaths > 100; technology:Rites of Harvest
# consequences: population:population -3; vagrants:vagrants +45; death_records_revision:deaths -10
```

**Note**: Technology conditions use the format `technology:Technology Name` (no comparison operator or value needed - just checks if the technology is unlocked).

---

## ⚠️ **Important Rules & Systems**

### **🎯 Event Pause System**
- **Production Pause**: All production cycles are paused during active events
- **Population Pause**: Population growth, starvation, and vagrant conversion are paused during events
- **Time Pause**: Game time progression is already paused during events
- **Resume**: All systems resume normal operation when events complete

### **🎯 Vignette Transparency System**
- **Dynamic Transparency**: Vignette (previously "fade") changes transparency for each screen type
- **Screen-Specific Values**: 
  - Splash: 45% opacity
  - Verse: 45% opacity  
  - Chorus: 95% opacity (highest for decision points)
  - Bridge: 80% opacity
  - Outro: 45% opacity
- **Smooth Transitions**: DOTween animations with configurable duration and easing
- **Automatic Management**: No need to reset transparency when events complete
- **Protected from Destruction**: Vignette persists across screen transitions

### **🎯 Population Management Rules**
- **Population can NEVER be directly added** through events
- **Population can only be REMOVED** (which registers as deaths)
- **Adding new people** should use `VagrantsChange` instead
- **Vagrants automatically convert to population** at a fixed rate when housing is available (handled by `PopGrowthLogic.cs`)

### **🎯 Death Tracking System**
- **`deaths`** - Public death records that can be revised by evil empires
- **`trueDeaths`** - Actual accumulated deaths that cannot be revised
- **Invariant**: `trueDeaths >= deaths` is always maintained
- **`DeathsChange`** - Kills population and updates both counters
- **`DeathRecordsRevision`** - Only affects public records, but adding deaths also increases true count

### **🎯 Housing Management**
- **Housing reduction** automatically converts excess population to vagrants
- **Housing increase** provides space for vagrants to become population

---

## 🔧 **External Ink Functions**

### **🎯 Population Management**
- `GetPopulation()` - Returns current population
- `ModifyPopulation(int change)` - Only accepts negative values (removes population)
- `GetHousing()` - Returns current housing
- `ModifyHousing(int change)` - Modifies housing (handles population conversion)
- `GetVagrants()` - Returns current vagrants
- `ModifyVagrants(int change)` - Modifies vagrants
- `ProcessEventDeaths(int deathCount)` - Kills population and tracks deaths

### **🎯 Death Tracking**
- `GetDeaths()` - Returns public death records
- `GetTrueDeaths()` - Returns true accumulated deaths
- `GetVagrantDeaths()` - Returns vagrant deaths

### **🎯 Resource & Technology Management**
- `GetResourceAmount(string resourceName)` - Check resource amounts
- `ModifyResource(string resourceName, int change)` - Modify resources
- `CheckTechnology(string technologyName)` - Check technology unlock status
- `TriggerTechnologyEnlightened(string technologyName)` - Unlock technology

### **🎯 Event Score Management**
- `GetEventScore(string scoreName)` - Check event progress
- `ModifyEventScore(string scoreName, int change)` - Advance event scores

### **🎯 Event Triggering**
- `TriggerEvent(string eventName)` - Trigger a specific event by name

---

## 🚀 **Technology-Event Integration System**

### **🎯 How It Works**
- **Event Technologies**: Technologies marked with `isEventTech = true` in `TechnologyData`
- **Automatic Triggering**: When an event technology is researched, it automatically triggers the corresponding event
- **Name Matching**: The system looks for an event with the same name as the technology
- **Condition Checking**: Events only trigger if their conditions are met
- **Seamless Progression**: Technology research becomes a narrative experience

### **🎯 Setting Up Event Technologies**
In your `TechnologyData` ScriptableObject:
- ✅ Set `isEventTech = true` for technologies that should trigger events
- ✅ Ensure the technology name matches exactly with the event name in your Ink files
- ✅ The event will automatically trigger when the technology is unlocked

---

## 🚫 **Event Trigger Protection System**

### **🎯 System Initialization Protection**
- **Zero Start**: System starts with `seventhChangeCount = 0`
- **First Change**: When first seventh changes, count becomes `1` (events now allowed)
- **Subsequent Changes**: Count increases with each change, events always allowed
- **Positive Logic**: Events only trigger when `seventhChangeCount >= 1`

### **🎯 Why This Approach?**
- **Prevents edge cases** when system first initializes
- **Allows events on second seventh change** (not third seventh)
- **Intuitive timing** that matches player expectations
- **Robust protection** against system initialization issues

---

## 🏆 **Priority System with Random Selection**

### **🎯 How Priority Works**
- **Highest Priority Wins**: Events with higher priority values trigger first
- **Default Priority 0**: Events without explicit priority default to 0
- **Random Selection**: Events with the same priority are randomly selected for variety
- **Condition Checking**: Priority only matters between events that meet their conditions

### **🎯 Setting Event Priority**
Use the `# priority:` metadata in your Ink files:

```ink
=== Horology ===
# title: Horology
# description: The ancient art of timekeeping has been rediscovered
# conditions: technology:Horology; score:horology_completed == 0
# consequences: score:horology_completed +1; resource:Duskstone +30
# screen_flow: splash,verse,chorus,outro
# event_type:Mystical
# priority:100

=== Weeping Princess ===
# title: Weeping Princess
# description: A sorrowful figure appears by the ancient gate
# conditions: score:weeping_princess_completed == 0; resource:Food > 6
# consequences: score:weeping_princess_completed +1; resource:Food +10
# screen_flow: splash,verse,chorus,outro
# event_type:Environmental
# priority:0
```

### **🎯 Random Selection Feature**
When multiple events have the same priority and meet their conditions, the system randomly selects one, ensuring variety in gameplay instead of always picking the first one.

---

## 🔄 **Dynamic Resource Management**

### **🎯 Automatic Resource Discovery**
- **New Resources**: When events add resources that don't exist in storage yet, the system automatically discovers them
- **Asset Loading**: Uses existing `GameUnit` assets from the project (no programmatic creation)
- **Storage Integration**: Automatically adds new resource slots to the storage tab
- **Seamless Experience**: Players see new resources appear naturally through narrative events

### **🎯 How It Works**
1. **Event Consequence**: Event tries to add a resource (e.g., Duskstone)
2. **Resource Check**: System checks if resource exists in storage
3. **Asset Discovery**: If not found, searches for existing `GameUnit` assets
4. **Slot Creation**: Creates new storage slot using `TabBuilderLogic.AddNewUnit()`
5. **Resource Addition**: Applies the resource amount change

---

## 📚 **Example Events & Testing**

### **🎯 Test Stories Overview**
The system supports multiple stories within a single volume with complex dependencies and conditions.

### **🎯 Story 1: Horology (Mystical) - Priority 100**
**Trigger Conditions**: Technology "Horology" unlocked, score:horology_completed == 0
**Expected Result**: Event triggers immediately when technology is researched, adds resources and advances score

### **🎯 Story 2: Weeping Princess (Environmental) - Priority 0**
**Trigger Conditions**: Technology "Horology" unlocked (simplified for testing)
**Expected Result**: Event triggers randomly with other priority 0 events, kills population, adds vagrants, revises death records

### **🎯 Story 3: Hollow Caravan (Crisis) - Priority 0**
**Trigger Conditions**: Technology "Horology" unlocked (simplified for testing)
**Expected Result**: Event triggers randomly with other priority 0 events, kills population, adds vagrants, adds deaths to public records

### **🎯 Story 4: Forgotten Graves (Mystical) - Priority 0**
**Trigger Conditions**: Technology "Horology" unlocked (simplified for testing)
**Expected Result**: Event triggers randomly with other priority 0 events, removes vagrants, wipes deaths from public records

---

## 🎨 **Metadata-Driven UI System**

### **🎯 How It Works**
UI elements are controlled through metadata in your Ink files:

```ink
=== story_start ===
# title: The Mysterious Library
# description: An ancient library filled with forbidden knowledge
# conditions: score:library_progress == 0
# consequences: score:library_progress +10
# screen_flow: splash,verse,verse,bridge,chorus,verse,outro
# splash_art:library_entrance
# event_type:Mystical
# event_color:purple
# button_text:Enter the Library
# background:foggy_forest
```

### **🎯 Available UI Metadata**
- **`# splash_art:`** - Controls the splash image
- **`# event_type:`** - Controls the event type display and color theme
- **`# event_color:`** - Controls the event color (legacy support)
- **`# button_text:`** - Controls the button text
- **`# background:`** - Controls the background image
- **`# priority:`** - Sets event priority for selection system

---

## 🚀 **System Capabilities**

### **✅ Flexible Condition System**
- Population: `>=` operators (not just `==`)
- Resource thresholds
- Score-based completion tracking
- New condition types (vagrant_deaths, true_deaths)
- Technology requirements (format: `technology:Technology Name`)
- Story dependencies (requires previous story completion)

### **✅ Comprehensive Consequence System**
- Population changes (removal only)
- Vagrant changes (addition/removal)
- Resource changes (positive/negative)
- Death revisionism (positive/negative values)
- Technology unlocking
- Event score advancement (not just completion)

### **✅ Death Tracking System**
- **Deaths**: Public records that can be revised
- **TrueDeaths**: Actual count that cannot be revised (always ≥ deaths)
- **VagrantDeaths**: Separate tracking for homeless deaths

### **✅ Event Type Variety**
- **Environmental**: Nature and world events (green theme)
- **Crisis**: Dangerous and urgent situations (red theme)
- **Mystical**: Magic and supernatural occurrences (purple theme)
- **Social**: Interpersonal and political events (blue theme)

### **✅ Story Progression System**
- **Sequential Dependencies**: Stories can require previous completion
- **Score Advancement**: Events can advance scores beyond completion
- **Resource Management**: Complex resource handling and discovery
- **Technology Integration**: Automatic event triggering from research

---

## 🎯 **Benefits of the System**

### **✅ Direct Control**
- **Everything in Ink** - No need for ScriptableObjects
- **Easy to modify** - Change UI elements by editing Ink
- **Version control friendly** - All content in text files
- **Rapid iteration** - Quick changes without Unity editor

### **✅ Flexible Asset Management**
- **Multiple paths** - Flexible asset organization
- **Automatic loading** - No manual asset assignment
- **Fallback system** - Graceful handling of missing assets
- **Clear logging** - Know exactly what's loading

### **✅ Rich UI Experience**
- **Professional presentation** - Polished splash and outro screens
- **Visual distinction** - Event colors and types
- **Clear progression** - Obvious call-to-action buttons
- **Immersive atmosphere** - Background dimming and effects

### **✅ Scalable System**
- **Easy to extend** - Add new metadata types easily
- **Consistent behavior** - Same system for all UI elements
- **Player-friendly** - Professional interface design
- **Future-ready** - Foundation for advanced features

---

## 🎉 **System Status: Fully Operational!**

### **✅ What We've Successfully Implemented**:

#### **1. Population Integration System** ✅
- **New condition types**: Population, Housing, Vagrants, Deaths, TrueDeaths, VagrantDeaths
- **New consequence types**: Population changes, Housing changes, Vagrants changes, Death revisionism
- **External Ink functions** for all population management
- **Robust death tracking** with `trueDeaths >= deaths` invariant

#### **2. Event Pause System** ✅
- **Production cycles paused** during active events
- **Population changes paused** during active events  
- **Time progression paused** during active events
- **Automatic resume** when events complete

#### **3. Vignette Transparency System** ✅
- **Dynamic transparency** for each screen type
- **Smooth DOTween transitions** with configurable settings
- **Screen-specific values**: Splash (45%), Verse (45%), Chorus (95%), Bridge (80%), Outro (45%)
- **Protected from destruction** during screen transitions

#### **4. Multiple Story Support** ✅
- **Four test stories** in WeepingPrincess.ink
- **Complex conditions** (technology, score dependencies, death thresholds)
- **Advanced consequences** (death revisionism, score advancement)
- **Event type variety** (Environmental, Crisis, Mystical)

#### **5. Technology-Event Integration** ✅
- **Automatic event triggering** when event technologies are researched
- **Seamless progression** between technology research and narrative events
- **Name-based matching** between technology and event names
- **Condition checking** ensures events only trigger when appropriate

#### **6. Dynamic Resource Management** ✅
- **Automatic resource loading** for new resources added through events
- **Seamless integration** with existing storage and production systems
- **Resource discovery** through narrative events and technology unlocks
- **No manual setup** required for new resources

#### **7. Event Trigger Protection** ✅
- **System initialization protection** prevents events until first seventh change
- **Zero-to-positive logic** starts at 0, events allowed when count reaches 1
- **Edge case prevention** avoids duplicate event activations when time tracking begins
- **Intuitive timing** ensures events only trigger after proper progression

#### **8. Priority System with Random Selection** ✅
- **Explicit priority support** with `# priority:` metadata in Ink files
- **Default priority 0** for events without explicit priority specification
- **Highest priority wins** when multiple events meet conditions
- **Random selection** from events with the same priority for variety
- **Horology event priority 100** ensures it triggers first when available

### **🎯 Ready for Production**:

Your system is now ready to handle complex, multi-story event scenarios with:
- **Robust population mechanics**
- **Evil empire death revisionism themes**
- **Smooth visual transitions**
- **Complex story dependencies**
- **Production and population pausing**
- **Technology-event integration** for seamless narrative progression
- **Priority-based event selection** with random variety
- **Automatic resource discovery** and management

The event system is a solid foundation that can easily be extended with new story types, conditions, and consequences while maintaining all the optimizations and safety features we've implemented! 🚀 