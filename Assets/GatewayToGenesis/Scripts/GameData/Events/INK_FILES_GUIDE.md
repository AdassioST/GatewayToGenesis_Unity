# 📚 **Ink Files Development Guide**

## 🎯 **Overview**

This comprehensive guide explains how to create Ink files that fully utilize the Gateway to Genesis Event System. The system provides a powerful, metadata-driven approach to narrative content creation with advanced population mechanics, dynamic UI control, complex story progression, and seamless technology integration.

---

## 🏗️ **System Architecture**

### **🎯 Core Components**
The Event System consists of several interconnected components that work together to deliver rich narrative experiences:

- **`EventSystemLogic`**: Central coordinator managing event lifecycle, system integration, and time-based triggering
- **`EventVolumeManager`**: Handles story volumes, Ink integration, external function binding, and priority-based event selection
- **`EventScreenManager`**: Manages UI display, screen transitions, vignette transparency, and progressive text reveal
- **`InkDrivenEventSetup`**: Automatically parses Ink files and creates structured story nodes with metadata extraction
- **`PopGrowthLogic`**: Manages population, housing, vagrants, and dual death tracking systems with event pause integration

### **🎨 Screen Flow System**
Events progress through a structured screen flow system:

1. **Splash**: Introduction with metadata-driven content and visual elements
2. **Verse**: Narrative text with progressive reveal mechanics
3. **Chorus**: Decision points (token drop), metadata-only (no immediate consequence application)
4. **Bridge**: Transition screens that advance on player input
5. **Outro**: Results/conclusion with detailed consequences display

---

## 📁 **File Organization & Setup**

### **🎯 Directory Structure**
```
Assets/
├── Resources/
│   └── Events/
│       ├── WeepingPrincess.ink          # Example storyline
│       ├── MainQuest.ink                # Primary narrative
│       ├── SideQuests.ink               # Optional content
│       └── SpecialEvents.ink            # Time-based events
└── GatewayToGenesis/
    └── Scripts/
        └── GameData/
            └── Events/                   # Script files
```

### **🚀 Automatic Loading System**
The `InkDrivenEventSetup` script automatically:
- ✅ Scans the `Resources/Events/` folder
- ✅ Loads all `.ink` files as EventVolumes
- ✅ Parses metadata for conditions, consequences, and UI control
- ✅ Generates screen flows based on node structure
- ✅ Binds external C# functions for game state interaction

### **⚙️ Setup Requirements**
1. **Create** `Resources/Events/` folder in your Assets directory
2. **Add** `InkDrivenEventSetup` script to your scene
3. **Place** `.ink` files in the Events folder
4. **Run** the game - events load automatically

---

## ✍️ **Ink File Structure & Syntax**

### **🎯 Basic Story Node Structure**
```ink
=== story_node_name ===
# title: Your Story Title
# description: Brief description of the story
# conditions: score:progress == 0; resource:Food > 10
# consequences: score:progress +1; resource:Food -5
# screen_flow: splash,verse,chorus,outro
# event_type:Environmental
# priority:50
# button_text:Begin Adventure

Your story content here...
* [Choice 1] -> next_knot
* [Choice 2] -> alternative_knot
```

### **🎯 Required Metadata Fields**
Every story node must include these essential fields:

- **`# title:`** - Display name for the story
- **`# conditions:`** - Requirements that must be met to trigger
- **`# event_type:`** - Story classification (Environmental, Mystical, Social, Crisis)

### **🎯 Optional Metadata Fields**
Enhance your stories with these optional fields:

- **`# description:`** - Story summary for UI display
- **`# screen_flow:`** - Custom screen progression sequence
- **`# splash_art:`** - Custom splash screen image
- **`# event_color:`** - Visual theme color (legacy support)
- **`# button_text:`** - Custom button labels
- **`# background:`** - Background image specification
- **`# priority:`** - Event priority for selection system (default: 0)

---

## 🎯 Chorus Choice Metadata (`&C`)

Chorus choices embed metadata after `&C` and optional description after `&D`.

Example from `hollow_caravan_chorus_1` in `StarterVolume.ink`:

```ink
* Idealism. It's Hope. Welcome the Caravan!&D We all deserve a second chance at life, don't we? We have to share the world with the survivors left.&C pillar:waltz;strength:15;requirements:population:population >= 5;requirements:cost:resource:Elderwood 40;success:consequences:resource:Aetherlight +225;resource:Food +60;score:knowledge_gained +1;failure:consequences:population:-75;score:skinwalker_attack +1;success:hollow_caravan_verse_2;failure:hollow_caravan_verse_3;crit_success:hollow_caravan_verse_12;crit_success:consequences:resource:Aetherlight +100;score:windfalls +1;crit_failure:hollow_caravan_verse_13;crit_failure:consequences:population:-150;deaths:TrueDeaths +25;score:hubris +1;rare_event:hollow_caravan_verse_14;rare_event_percent:3;rare_event:consequences:resource:Food +500;resource:Aetherlight +150;score:rare_boon +1
```

Parsing rules (no redundancy, supports multi-item groups):
- **requirements:** Accumulates multiple entries across the same `&C` block. Supports multi-word targets and comparisons, e.g. `technology:Efficient Rations == 1`, `population:population >= 5`.
- **requirements:cost:** Accumulates multiple entries for costs.
- **success/failure/crit_success/crit_failure/rare_event:**
  - `:consequences:` sections capture multiple semicolon-separated items.
  - Paths are captured separately via `success:`, `failure:`, etc.

### Consequences authoring (reworked)
- **Chorus**: keep only routing (`success:`, `failure:`, `crit_*:`, `rare_event:`) and gating (`requirements:`) and display-only costs (`requirements:cost:`) in `&C`. Do not put consequences here.
- **Verse/Bridge**: put `&C consequences: ...` inline on the button choice for that screen. Example:
  - `* Accept the gifts&C consequences: resource:Aetherlight +225; production_percent:Food +10; score:knowledge_gained +1 -> hollow_caravan_outro`

### Consequences application (runtime)
- On verse/bridge continue, the system parses `&C consequences:` from the single button line and adds them to the cumulative list (no immediate application).
- Outro applies all cumulative consequences at once.

### Requirements & Costs UI
- Each requirement spawns a `RequirementSlot` under the choice's `Requirements` container.
- Slots show the correct icon (population, housing, vagrants, deaths, technology, resources) and met/unmet color.
- Choices are locked if any requirement is unmet OR any cost is unaffordable: background alpha 0.15, foreground stays visible with a `Locked` overlay and layer switched to `UIBlock`.
- Cost requirements: author with `requirements:cost:` in the chorus metadata. They display as icons, GATE availability by affordability, and are automatically converted into cumulative consequences and consumed at Outro.
  - Supported costs: `resource:{Name} {amount}`, `population:population {amount}`, `housing:housing {amount}`.
  - Example: `requirements:cost:resource:Elderwood 5` gates the choice unless you have ≥5 Elderwood and creates a cumulative `resource:Elderwood -5` consequence at Outro.
  - Implicit numeric form like `resource:Elderwood 40` is parsed as `resource:Elderwood >= 40`.

### Generic icons
- `TechnologyCheck` uses a generic technology icon (assign in `RequirementSlot` prefab).
- `ScoreCheck` uses a generic score icon (assign in `RequirementSlot` prefab).
- `Food` has a dedicated `foodIcon` slot in `RequirementSlot` (assign in prefab). All other resources use their `GameUnit` icon.

## 🏗️ **Population Integration System**

### **🎯 New Condition Types**
The system supports advanced population-based conditions:

```ink
# conditions: 
#   population:population >= 10;           # Population threshold
#   housing:housing >= 15;                # Housing availability
#   vagrants:vagrants <= 5;               # Vagrant population
#   deaths:deaths > 20;                   # Public death records
#   vagrant_deaths:vagrant_deaths >= 3;   # Homeless deaths
#   true_deaths:true_deaths > 50;         # Actual death count
```

### **🎯 New Consequence Types**
Implement complex population mechanics:

```ink
# consequences:
#   population:population -5;              # Kill population (registers deaths)
#   housing:housing +10;                  # Build new housing
#   vagrants:vagrants +20;                # Add new vagrants
#   death_records_revision:deaths -3;     # Revise public records
```

### **⚠️ Important Rules**
- **Population can NEVER be added** - only removed (registers deaths)
- **Adding new people** uses `vagrants` instead
- **Vagrants automatically convert** to population when housing is available
- **Death revisionism** affects public records but maintains true count integrity
- **Invariant**: `trueDeaths >= deaths` is always maintained

---

## 🔧 **External Function Integration**

### **🎯 Population Management Functions**
```ink
{GetPopulation()}                    # Returns current population
{GetHousing()}                      # Returns current housing
{GetVagrants()}                     # Returns current vagrants
{ModifyPopulation(-3)}              # Remove population (negative only)
{ModifyHousing(5)}                  # Add housing
{ModifyVagrants(10)}                # Add vagrants
```

### **🎯 Death Tracking Functions**
```ink
{GetDeaths()}                       # Public death records
{GetTrueDeaths()}                   # Actual accumulated deaths
{GetVagrantDeaths()}                # Vagrant death count
{ProcessEventDeaths(5)}             # Kill population and track deaths
```

### **🎯 Resource & Technology Functions**
```ink
{GetResourceAmount("Food")}         # Check resource amounts
{ModifyResource("Aetherlight", 30)} # Modify resources
{CheckTechnology("Rites of Harvest")} # Check technology unlock status
{TriggerTechnologyEnlightened("Advanced Farming")} # Unlock technology
```

**Note**: Resources that don't exist yet will be automatically discovered from the existing GameUnit system and added to the storage tab when events modify them.

### **🎯 Event Score Functions**
```ink
{GetEventScore("quest_progress")}   # Check event progress
{ModifyEventScore("ancient_knowledge", 5)} # Advance event scores
```

### **🎯 Event Triggering Functions**
```ink
{TriggerEvent("Horology")}          # Trigger a specific event by name
```

---

## ⚙️ **Technology-Event Integration System**

### **🎯 How It Works**
The Event System automatically links with the Technology System to create seamless narrative experiences:

1. **Event Technologies**: Technologies marked with `isEventTech = true` in `TechnologyData`
2. **Automatic Triggering**: When an event technology is researched, it automatically triggers the corresponding event
3. **Name Matching**: The system looks for an event with the same name as the technology
4. **Condition Checking**: Events only trigger if their conditions are met

### **🎯 Setting Up Event Technologies**
In your `TechnologyData` ScriptableObject:
- ✅ Set `isEventTech = true` for technologies that should trigger events
- ✅ Ensure the technology name matches exactly with the event name in your Ink files
- ✅ The event will automatically trigger when the technology is unlocked

### **🎯 Example: Horology Technology**
```csharp
// In TechnologyData for "Horology" technology
public bool isEventTech = true;  // This makes it an event technology
```

```ink
=== Horology ===
# title: Horology
# description: The ancient art of timekeeping has been rediscovered
# conditions: technology:Horology; score:horology_completed == 0
# consequences: score:horology_completed +1; resource:Duskstone +30
# event_type:Mystical
# priority:100
```

**Result**: When "Horology" technology is researched, the "Horology" event automatically triggers.

---

## 🚫 **Event Trigger Protection System**

### **🎯 How Initialization Protection Works**
The Event System uses a sophisticated initialization protection mechanism to prevent edge cases:

1. **Zero Start**: System starts with `seventhChangeCount = 0`
2. **First Change**: When first seventh changes, count becomes `1` (events now allowed)
3. **Subsequent Changes**: Count increases with each change, events always allowed
4. **Positive Logic**: Events only trigger when `seventhChangeCount >= 1`

### **🎯 Why This Approach?**
- **Prevents edge cases** when system first initializes
- **Allows events on second seventh change** (not third seventh)
- **Intuitive timing** that matches player expectations
- **Robust protection** against system initialization issues

### **🎯 Timeline Example**
```
System Start:        seventhChangeCount = 0  (no events)
First Change (1→2):  seventhChangeCount = 1  (events allowed!)
Second Change (2→3): seventhChangeCount = 2  (events allowed!)
Third Change (3→4):  seventhChangeCount = 3  (events allowed!)
```

---

## 🏆 **Priority System with Random Selection**

### **🎯 How Priority Works**
The Event System uses a priority-based selection system to determine which events trigger when multiple are available:

1. **Highest Priority Wins**: Events with higher priority values trigger first
2. **Default Priority 0**: Events without explicit priority default to 0
3. **Random Selection**: Events with the same priority are randomly selected for variety
4. **Condition Checking**: Priority only matters between events that meet their conditions
5. **One-Time Events**: Score-based conditions prevent event repetition

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

### **🎯 Priority Examples**
- **Horology (Priority 100)**: Highest priority, triggers first when available
- **Weeping Princess (Priority 0)**: Default priority, triggers after higher priority events
- **Custom Events**: Set any priority value (e.g., `# priority:50` for medium priority)

### **🎯 Random Selection Feature**
When multiple events have the same priority and meet their conditions, the system randomly selects one:

```ink
# All these events have priority 0
=== Weeping Princess ===
# priority: 0

=== Hollow Caravan ===  
# priority: 0

=== Forgotten Graves ===
# priority: 0
```

**Result**: Instead of always picking the first one (Weeping Princess), the system randomly selects from all three, ensuring variety in gameplay!

### **🎯 Best Practices**
- **Use priority 100** for critical story events that should trigger immediately
- **Use priority 0** for background events that can wait
- **Use intermediate values** (10, 25, 50, 75) for nuanced story progression
- **Consider story flow** when setting priorities to ensure logical progression

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

## 🎨 **Metadata-Driven UI System**

### **🎯 Visual Customization**
Control every aspect of your event's presentation:

```ink
=== mystical_library ===
# title: The Forbidden Library
# description: Ancient knowledge lies within these walls
# event_type:Mystical
# splash_art:library_entrance
# event_color:purple
# button_text:Enter the Library
# background:foggy_forest
# screen_flow: splash,verse,verse,bridge,chorus,verse,outro
```

### **🎯 Event Type System**
Four distinct event types with unique visual treatments:

- **`Environmental`**: Nature and world events (green theme)
- **`Mystical`**: Magic and supernatural occurrences (purple theme)
- **`Social`**: Interpersonal and political events (blue theme)
- **`Crisis`**: Dangerous and urgent situations (red theme)

### **🎯 Asset Loading System**
The system automatically loads assets from multiple paths:

1. **Primary**: `Resources/EventArt/{assetName}`
2. **Secondary**: `Resources/UI/{assetName}`
3. **Fallback**: `Resources/Sprites/{assetName}`
4. **Default**: `Resources/{assetName}`

---

### 🧭 Decision Choices: &D and &C Inline Markers (with Crit/Rare/Time Passes)

Chorus choices embed clean display text and machine-readable metadata inline:

```ink
=== sample_event_chorus_1 ===
Context text above choices...
* Idealism. Aid Them &D Compassion guides our hand. &C pillar:aureus;strength:15; 
    success:event_success; failure:event_failure; 
    success:consequences: resource:Aetherlight +10; score:mercy +1;
    crit_success:event_crit_success; crit_success:consequences: resource:Aetherlight +40;
    crit_failure:event_crit_failure; crit_failure:consequences: population:-20;
    rare_event:event_rare_boon; rare_event_percent:3; rare_event:consequences: resource:Food +120
* Realism. Refuse &D We cannot risk it now. &C pillar:regalia;strength:12; failure:event_failure
* Pragmatism. Turn Away &D Time passes without incident.&C consequences: score:caution +1; 
    rare_event:event_quiet_boon; rare_event_percent:3; rare_event:consequences: resource:Food +30
```

- **&D description**: Text between `&D` and `&C` appears in the choice UI.
- **&C metadata**: `key:value` pairs separated by `;` define the outcome logic. Supported keys:
  - Challenge: `pillar:{aureus|regalia|waltz|chorus}`, `strength:{int}` or shorthand `challenge:{pillar}:{strength}`
  - Requirements: `requirements:{type}:{target [op value]}; ...` (gating only)
  - Requirement costs (consumed at Outro): `requirements:cost:{type}:{target [op value]}; ...` (only resource/housing/population are consumed)
  - Primary branches: `success:{knot}` / `failure:{knot}`
  - Conditional consequences: `success:consequences: ...; ...` and `failure:consequences: ...; ...`
  - Legacy consequences (always apply): `consequences: ...; ...`
  - Criticals (optional): `crit_success:{knot}` / `crit_failure:{knot}` with `crit_success:consequences:` / `crit_failure:consequences:`
  - Rare event (optional): `rare_event:{knot}`, `rare_event_percent:{1..100}`, `rare_event:consequences:`

New: production percentage modifiers
- Author at verse/bridge button using `production_percent:{ResourceName} +/-{int}`.
- Example: `production_percent:Food +10` adds a +10% persistent global production bonus for Food; `-15` reduces by 15%.
- Shown in Outro preview as: `Production +10% for Food`.

Temporary effects (duration in sevenths)
- You can make certain effects temporary by appending a `duration:sevenths:N` token on the same button line, after the effect.
- Supported timed effects:
  - `production_percent:{Resource}` and `production_percent_section:{Section}`
  - `click_power:{Resource}` and `click_power_percent:{Resource}`
  - `click_power_section:{Section}` and `click_power_percent_section:{Section}`
- Duration is counted in sevenths (in-game time unit). Example:
  - `* Accept&C consequences: production_percent:Food +10; duration:sevenths:6 -> next_knot`
  - Applies +10% Food production for 6 sevenths, then automatically expires.
- Only one timed effect with duration is expected per line (design constraint) to avoid ambiguity.

Section-wide modifiers
- Production percent: `production_percent_section:{SectionName} +/-{int}`
- Click power (flat): `click_power_section:{SectionName} +/-{int}`
- Click power (percent): `click_power_percent_section:{SectionName} +/-{int}`

Click power modifiers
- Resource flat: `click_power:{ResourceName} +/-{int}`
- Resource percent: `click_power_percent:{ResourceName} +/-{int}`
- Section flat: `click_power_section:{SectionName} +/-{int}`
- Section percent: `click_power_percent_section:{SectionName} +/-{int}`

Implementation details:
- At startup, the system precompiles all chorus choice metadata from compiled Ink in `Resources/Events` and stores it in a global index. UI reads this cache; no runtime parsing or regex.
- Choice description strictly uses the `&D` segment.
- Keep choice prefix `* ChoiceType. Title` to map Idealism/Realism/Pragmatism consistently.

Requirements and costs (new):
- `requirements:` block gates availability. The choice is only pickable if all entries evaluate to true using the same format supported by event `#conditions` (e.g., `resource:Food >= 40`, `technology:Efficient Rations == 1`, `score:caravan_encountered >= 1`).
- `requirements:cost:` block declares resources to consume when the story concludes, applied with other cumulative consequences at Outro. Only the following are consumed:
  - `resource:{Name} {amount}` (positive amounts are removed)
  - `population:population {amount}` (removes people; tracked as deaths)
  - `housing:housing {amount}` (destroys housing)
- Unconsumable types (scores, technologies, etc.) should remain in `requirements:`; they cannot be consumed.

Example:
```ink
* Idealism. Aid &D Act with compassion. &C pillar:waltz;strength:15;
    requirements: population:population >= 5;
    requirements:cost: resource:Elderwood 40;
    success: idealism_success; failure: idealism_failure

* Realism. Seize &D Survival first. &C pillar:regalia;strength:20;
    requirements: technology:Efficient Rations == 1; score:caravan_encountered >= 1;
    success: realism_success; failure: realism_failure

* Pragmatism. Turn Away &D No action needed. &C consequences: score:caution +1
```

Roll logic and thresholds:
- The check computes success% = round((current/required)×100), clamped 0..100.
- A single d100 roll determines the outcome:
  - If `roll ≤ rare_event_percent`, and `rare_event` is defined, the rare path triggers.
  - Otherwise, `success` if `roll ≤ success%`; `failure` if `roll > success%`.
  - Criticals only apply if their paths exist:
    - Critical Failure if `roll ≤ 10` and the baseline outcome is failure.
    - Critical Success if `roll ≥ 91` and the baseline outcome is success.
- For choices without challenges (e.g., Pragmatism), the system shows a confirmation card that reads "Time passes..." and immediately waits for click to proceed to the configured destination or rare event.

Authoring tips:
- Always provide `success` and `failure` targets for challenge choices; add `crit_*` only when you have dedicated content.
- Keep `rare_event_percent` small (default 3) to maintain surprise value.
- Use `consequences` along with the branch-specific consequences when some effects should always occur regardless of outcome.

---

## 📚 **Advanced Story Patterns**

### **🎯 Sequential Story Dependencies**
Create complex story progression systems:

```ink
=== first_story ===
# title: The Beginning
# conditions: score:first_story_completed == 0
# consequences: score:first_story_completed +1

=== second_story ===
# title: The Continuation
# conditions: score:first_story_completed >= 1; resource:Aetherlight > 10
# consequences: score:second_story_completed +1; score:ancient_knowledge +5
```

### **🎯 Technology-Gated Content**
Require specific technological advancements:

```ink
=== advanced_event ===
# title: Technological Breakthrough
# conditions: technology:Rites of Harvest; population:population >= 20
# consequences: score:tech_progress +10; resource:Aetherlight +50
```

### **🎯 Resource-Based Triggers**
Create dynamic events based on player economy:

```ink
=== economic_crisis ===
# title: Market Collapse
# conditions: resource:Food < 5; resource:Aetherlight > 100
# consequences: resource:Aetherlight -50; vagrants:vagrants +15
```

---

## 🎭 **Complex Event Examples**

### **🎯 Multi-Story Volume Example**
```ink
=== weeping_princess ===
# title: Weeping Princess
# description: A mysterious figure appears in the village
# conditions: population:population >= 10; housing:housing >= 15
# consequences: population:population -5; vagrants:vagrants +45; death_records_revision:deaths -3
# screen_flow: splash,verse,verse,bridge,chorus,verse,outro
# event_type:Environmental
# priority:0

=== hollow_caravan ===
# title: Hollow Caravan
# description: Dark forces approach the settlement
# conditions: score:weeping_princess_completed >= 1; technology:Rites of Harvest; resource:Food > 4
# consequences: population:population -12; vagrants:vagrants +20; resource:Aetherlight +30
# screen_flow: splash,bridge,chorus,verse,verse,verse,bridge,verse,outro
# event_type:Crisis
# priority:0

=== forgotten_graves ===
# title: Forgotten Graves
# description: Ancient secrets emerge from the earth
# conditions: score:hollow_caravan_completed >= 1; true_deaths:true_deaths >= 20
# consequences: score:ancient_knowledge +5; death_records_revision:deaths -15
# screen_flow: splash,verse,chorus,verse,outro
# event_type:Mystical
# priority:0
```

### **🎯 What This Creates**
1. **Sequential progression** with story dependencies
2. **Complex conditions** requiring multiple systems
3. **Rich consequences** affecting multiple game aspects
4. **Varied event types** with distinct visual themes
5. **Score advancement** beyond simple completion tracking

---

## ⚠️ **Best Practices & Guidelines**

### **🎯 Content Creation**
- **Keep stories focused** - one clear narrative per node
- **Use descriptive titles** - help players understand content
- **Balance conditions** - make events achievable but meaningful
- **Plan consequences** - consider long-term game impact

### **🎯 Technical Considerations**
- **Test conditions thoroughly** - ensure events trigger as expected
- **Validate consequences** - verify all systems respond correctly
- **Use consistent naming** - maintain clear story progression
- **Document dependencies** - track story relationships

### **🎯 Performance Optimization**
- **Limit complex conditions** - avoid overly complex logic
- **Efficient screen flows** - don't create unnecessary screens
- **Reasonable asset sizes** - optimize visual assets
- **Clean metadata** - avoid unnecessary or duplicate fields

---

## 🚀 **Testing & Validation**

### **🎯 Development Workflow**
1. **Create** your Ink file with basic structure
2. **Test** conditions and consequences in isolation
3. **Validate** screen flows and UI elements
4. **Integrate** with existing story progression
5. **Playtest** complete event sequences

### **🎯 Debug Tools**
The system provides extensive logging:
- **Condition evaluation** - see why events do/don't trigger
- **Consequence application** - track all system changes
- **Asset loading** - monitor resource loading success
- **Screen progression** - follow event flow execution

### **🎯 Common Issues & Solutions**
- **Events not triggering**: Check condition syntax and game state
- **UI not displaying**: Verify metadata format and asset paths
- **Consequences not applying**: Ensure all systems are initialized
- **Screen flow issues**: Validate screen type names and sequences

---

## 🎉 **System Capabilities Summary**

### **✅ What You Can Create**
- **Complex story progression** with dependencies and branching
- **Dynamic population mechanics** with death tracking and revisionism
- **Rich visual experiences** with metadata-driven UI control
- **Economic integration** with resource-based triggers
- **Technological advancement** with research-gated content
- **Score-based progression** with multiple tracking systems
- **Technology-event integration** with automatic event triggering

### **✅ Advanced Features**
- **Event pause system** - halts game progression during events
- **Vignette transparency** - dynamic visual atmosphere control
- **Multiple story support** - complex volume management
- **Asset fallback system** - graceful handling of missing resources
- **External function binding** - direct game state interaction
- **Metadata parsing** - flexible content-driven design
- **Technology integration** - automatic event triggering from research
- **Priority system** - controlled event selection with random variety
- **Resource discovery** - automatic integration of new resources

---

## 🔮 **Future Expansion**

The Event System is designed for extensibility:
- **New condition types** can be easily added
- **Additional consequence types** expand gameplay possibilities
- **Enhanced UI metadata** provides more visual control
- **Advanced story patterns** support complex narratives
- **Integration systems** connect with other game mechanics

---

## 📖 **Additional Resources**

- **`README.md`**: Complete technical documentation
- **`WeepingPrincess.ink`**: Working example implementation
- **Unity Console**: Extensive debug logging and error reporting
- **Event System Scripts**: Source code for advanced customization

---

## 🎯 **Getting Started Checklist**

- [ ] Create `Resources/Events/` folder
- [ ] Add `InkDrivenEventSetup` to scene
- [ ] Create your first `.ink` file with required metadata
- [ ] Test basic conditions and consequences
- [ ] Add visual customization metadata
- [ ] Implement story progression dependencies
- [ ] Set event priorities for controlled selection
- [ ] Test complete event flow
- [ ] Iterate and expand your narrative universe

---

**The Gateway to Genesis Event System provides a powerful, flexible foundation for creating rich, interactive narratives that deeply integrate with your game's mechanics and progression systems. Start simple, build complexity gradually, and unleash your creativity!** 🚀✨ 