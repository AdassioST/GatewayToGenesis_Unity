# Chorus Screen System Guide

The Chorus Screen is the most important event screen type, handling conditional checks, luck-based outcomes, and event branching. This system integrates seamlessly with the existing Ink narrative engine and reuses existing requirement-checking logic.

## 🎯 **New System Overview**

The updated Chorus Screen system features:
- **Smooth alpha transitions** for background elements during token dragging
- **Hidden background elements** that reveal as choices become invisible
- **Distance-based alpha changes** - closer to center = lower alpha, further = higher alpha
- **Base alpha 35%** for available choice backgrounds when not interacting (15% for locked ones)
- **Alpha increases to 100%** on the drop area under the token as it moves away from the centre

## 🏗️ **UI Hierarchy Structure**

```
ChorusScreen (Empty GameObject)
├── Background (UI Image)
├── CivilizationPillars (Empty GameObject)
│   ├── Aureus (Empty GameObject)
│   │   ├── Icon (UI Image)
│   │   └── Amount (TextMeshPro Text)
│   ├── Chorus (Empty GameObject)
│   │   ├── Icon (UI Image)
│   │   └── Amount (TextMeshPro Text)
│   ├── Regalia (Empty GameObject)
│   │   ├── Icon (UI Image)
│   │   └── Amount (TextMeshPro Text)
│   └── Waltz (Empty GameObject)
│       ├── Icon (UI Image)
│       └── Amount (TextMeshPro Text)
├── FullOutcomes (Empty GameObject) [ChorusScreenManager component]
│   ├── Idealism (Empty GameObject) [ChorusChoice component]
│   │   ├── HoverDescription (TextMeshPro Text)
│   │   ├── Type (TextMeshPro Text)
│   │   └── Choices (Empty GameObject)
│   ├── Realism (same structure as Idealism)
│   ├── Pragmatism (same structure as Idealism)
│   └── Choices (Container for choice UI elements)
│       ├── Idealism (ChorusChoice component)
│       │   ├── Title (TextMeshPro Text)
│       │   ├── Description (TextMeshPro Text)
│       │   ├── ChallengeSlot (Empty GameObject)
│       │   │   ├── Icon (UI Image)
│       │   │   └── Chance (UI Image)
│       │   └── Requirements (Empty GameObject)
│       ├── Realism (...)
│       └── Pragmatism (...)
├── TwoChoices (Empty GameObject) [Same structure but without Pragmatism]
└── Token (UI Image) [DecisionToken component]
└── Description (TMP that reflects Chorus knot content)
```

## 🔧 **Core Components**

Every rule lives in one place; the components are views over it.

| Piece | Role |
|---|---|
| `EventStoryIndex` | Holds each chorus knot's choices (`ChorusChoiceData`), parsed once at start-up by `EventScript.ParseChorusChoice`. Shared and read-only. |
| `ChorusRules` | The d100: success % (pillar ÷ strength), Piety bonus, critical bands, rare events, costs, outcome odds. No Unity code; covered by `EventSystemTests`. |
| `ChorusScreenManager` | Picks the variant (three choices when Pragmatism exists), binds the cards and drop areas, refreshes them (pillar changes and every 0.5 s), resolves the drop, queues costs and consequences, shows the result card, navigates. |
| `ChorusChoice` | One card: title, description, a `RequirementSlot` per requirement and cost, the `ChallengeSlot`, the locked overlay, and a tooltip with requirements, costs, and what success and failure each lead to (odds included, and every effect along the story until its outro or next chorus). Rare events and critical successes are never shown; a possible critical failure is flagged. |
| `ChorusChoiceBackground` | The drop area for one choice, found by name (`Idealism`, `Realism`, `Pragmatism`, or lower case). Locked choices sit at 15% alpha on the `UIBlock` layer. |
| `DecisionToken` | Draggable token. Released over an available drop area it makes that choice; anywhere else it springs back. It cannot be dragged once a choice is made. |
| `ChallengeSlot` | Pillar icon and luck icon (Fated > 90%, Blessed ≥ 60%, Gamble ≥ 40%, Cursed ≥ 10%, else Forsaken), using the same chance as the roll, Piety included. |
| `RequirementSlot` | Icon plus a green/red panel; tooltip wording from `EventText` ("Needs At Least 5 Population", "Costs 5 Elderwood"). |

## 🎨 **Alpha Transition System**

1. **Base state**: available drop areas rest at 35% (`baseAlpha`); locked ones at 15% and never change.
2. **Drag starts**: available cards fade out, their drop areas drop to 15%; locked cards stay readable.
3. **While dragging**: the drop area under the token brightens towards 100% (`maxAlpha`) with the token's distance from the centre (`maxDistanceForAlpha` on the token); the others return to their resting alpha.
4. **Drag ends without a choice**: cards fade back in and drop areas return to 35%.

## 📝 **Ink Integration**

### **Enhanced Choice Format with Narrative Branching**
Choices now lead to different verse screens, creating natural story flow without explicit success/failure knots:

```ink
=== weeping_princess_chorus_1 ===
What penance will you carry?

* Idealism. Accept the penance, we all need something to leave behind us. #pillar:aureus;strength:15;requirements:score:quest_progress >= 1;success:weeping_princess_verse_2;failure:weeping_princess_verse_3 -> weeping_princess_challenge_1
* Realism. Do not, not every spirit needs salvation. #requirements:score:quest_progress >= 1 -> weeping_princess_verse_4
* Pragmatism. We should seek her bow for respect, but nothing more. Let's keep rational. #pillar:waltz;strength:18;success:weeping_princess_verse_5;failure:weeping_princess_verse_6 -> weeping_princess_challenge_2
```

### **Choice Types and Validation**

#### **1. Requirements Only**
```ink
* Realism. Do not, not every spirit needs salvation. #requirements:score:quest_progress >= 1 -> weeping_princess_verse_4
```
- Choice is available/unavailable based on requirements
- No challenge roll - goes directly to verse screen
- Story flows naturally through narrative progression

#### **2. Challenge Only**
```ink
* Courage. Face the darkness head-on. #pillar:aureus;strength:20;success:weeping_princess_verse_7;failure:weeping_princess_verse_8 -> courage_challenge
```
- No requirements - choice is always available
- Challenge roll determines which verse screen to show
- Goes to challenge knot first, then branches to different verse screens

#### **3. Both Requirements and Challenge**
```ink
* Idealism. Accept the penance, we all need something to leave behind us. #pillar:aureus;strength:15;requirements:score:quest_progress >= 1;success:weeping_princess_verse_2;failure:weeping_princess_verse_3 -> idealism_challenge
```
- Must meet requirements to see choice
- Challenge roll determines which verse screen to show
- Most complex but rewarding choice type

#### **4. No Validation**
```ink
* Continue. Move forward with caution. -> weeping_princess_verse_9
```
- Simple choice with no restrictions
- Always available, goes directly to verse screen

### **Metadata Format**
Each choice can have metadata after the `#` symbol:
- **pillar**: Which civilization pillar this choice challenges (aureus, regalia, waltz, chorus)
- **strength**: Required pillar strength for 100% success chance
- **requirements**: Conditions that must be met (semicolon-separated)
- **success**: Verse screen to show on successful challenge roll
- **failure**: Verse screen to show on failed challenge roll

### **Metadata Examples**
```ink
#pillar:aureus;strength:15;requirements:score:quest_progress >= 1;success:weeping_princess_verse_2;failure:weeping_princess_verse_3
#pillar:regalia;strength:12;success:weeping_princess_verse_5;failure:weeping_princess_verse_6
#requirements:score:quest_progress >= 1
#pillar:waltz;strength:18;success:weeping_princess_verse_7;failure:weeping_princess_verse_8
```

### **Story Flow with Narrative Branching**
```ink
=== chorus ===
* Choice with challenge -> challenge_knot -> success_verse OR failure_verse
* Choice without challenge -> destination_verse

=== challenge_knot ===
* [Face the challenge] -> resolution_knot

=== resolution_knot ===
* [Continue] -> END

=== success_verse ===
Success outcome narrative
* [Continue] -> outro

=== failure_verse ===
Failure outcome narrative  
* [Continue] -> outro
```

## 🎯 **Narrative Flow System Implementation**

### **How It Works**
1. **Choice Selection**: Player selects a choice from the chorus screen
2. **Validation Check**: System checks requirements (if any) and challenge (if any)
3. **Path Resolution**: 
   - **No Challenge**: Goes directly to destination verse screen
   - **With Challenge**: Rolls for success/failure, then navigates to appropriate verse screen
4. **Story Continuation**: Story continues from the resolved verse screen, maintaining narrative flow

### **Implementing in Your Ink Files**

#### **Step 1: Define the Chorus Knot**
```ink
=== weeping_princess_chorus_1 ===
What penance will you carry?

* Idealism. Accept the penance, we all need something to leave behind us. #pillar:aureus;strength:15;requirements:score:quest_progress >= 1;success:weeping_princess_verse_2;failure:weeping_princess_verse_3 -> idealism_challenge
* Realism. Do not, not every spirit needs salvation. #requirements:score:quest_progress >= 1 -> weeping_princess_verse_4
* Pragmatism. We should seek her bow for respect, but nothing more. Let's keep rational. #pillar:waltz;strength:18;success:weeping_princess_verse_5;failure:weeping_princess_verse_6 -> pragmatism_challenge
```

#### **Step 2: Create Challenge Knots (if needed)**
```ink
=== idealism_challenge ===
The path of Idealism glows with golden light, but shadows dance at its edges. Your heart guides you, but will your courage be enough?

* [Face the challenge] -> idealism_resolution
```

#### **Step 3: Create Different Verse Screens for Success/Failure**
```ink
=== weeping_princess_verse_2 ===
The path of Idealism has brought you to a realm where dreams take physical form. Your courage and hope have opened new possibilities that you never imagined.

* [Embrace the dream realm] -> outro

=== weeping_princess_verse_3 ===
The path was difficult, and your principles were tested. Yet through the struggle, you've grown stronger and more resolute.

* [Learn from the experience] -> outro
```

### **Metadata Reference**

| Field | Description | Example |
|-------|-------------|---------|
| `pillar` | Civilization pillar for challenge | `pillar:aureus` |
| `strength` | Required strength for 100% success | `strength:15` |
| `requirements` | Conditions that must be met | `requirements:score:quest_progress >= 1` |
| `success` | Verse screen for successful challenge | `success:weeping_princess_verse_2` |
| `failure` | Verse screen for failed challenge | `failure:weeping_princess_verse_3` |

### **Best Practices**
1. **Use descriptive verse screen names** that reflect the story content, not the outcome
2. **Keep challenge knots brief** - they're just for flavor and challenge resolution
3. **Provide meaningful narrative differences** between success and failure verse screens
4. **Use requirements sparingly** - they can make choices feel gated
5. **Balance challenge difficulties** - too easy or too hard reduces tension
6. **Maintain narrative flow** - verse screens should feel like natural story progression
7. **Avoid naming conventions** like "success/failure" - use story-appropriate names

### **Requirement Format**
Requirements use the same format as the existing event system:
```
type:target comparison value
```

Examples:
- `score:quest_progress >= 1`
- `technology:agriculture == 1`
- `resource:gold >= 100`
- `stat:aureus >= 15`

## 🚀 **Setup Instructions**

### 1. Create the ChorusScreen Prefab
1. Create an empty GameObject named "ChorusScreen"
2. Add the `ChorusScreenManager` component
3. Set up the UI hierarchy as shown above
4. Assign all required references in the inspector

### 2. Configure Components
- **ChorusScreenManager**: Assign prefabs, containers, and pillar icons
- **ChorusChoice**: Set up UI references and alpha settings
- **DecisionToken**: Configure drag settings and max distance
- **ChallengeSlot**: Assign chance sprites for different percentages

### 3. Set Up Pillar Icons
Assign the appropriate sprites for:
- Aureus (gold/yellow theme)
- Regalia (purple/royal theme)
- Waltz (blue/dance theme)
- Chorus (green/harmony theme)

### 4. Configure Alpha Settings
- **Base Alpha**: 0.3 (30%)
- **Max Alpha**: 0.95 (95%)
- **Transition Duration**: 0.3 seconds
- **Max Distance**: 200 pixels (adjust based on screen size)

## 🔄 **How the System Works**

1. **Open**: `EventScreenManager` instantiates the chorus prefab and, one frame later, calls `ChorusScreenManager.InitializeChorusScreen(screen)`. The choices come from `EventStoryIndex`; the question shown is the knot's last line.
2. **Availability**: a choice is available when every requirement and cost holds (`ChorusRules.IsAvailable`). This is re-checked when a pillar changes and twice a second while the screen waits, so a requirement met mid-screen unlocks the choice.
3. **Drop**: `DecisionToken` finds the drop area under the pointer (a `UIBlock` element in front blocks it) and asks it to accept. The manager checks availability again, rolls d100, and adds the Piety bonus (capped at 100).
4. **Outcome** (checked in this order): rare event on the top `rare_event_percent` rolls; no challenge means "Time passes..."; otherwise success when the roll beats `100 − success%`. A success above 90 is critical and a failure at 10 or below is critical, when those knots are authored.
5. **Consequences**: costs, then the outcome's consequences, then the choice's own `consequences:` are queued. They apply when the story completes, and the outro lists them with running totals.
6. **Result card**: it shows the outcome, plus "SAVED BY / TRIGGERED BY ENHANCED ROLL CHANCE!" when Piety changed it. The next input continues to the outcome's knot (or ends the story when there is none).

## 🐛 **Troubleshooting**

- **"No ChorusChoiceBackground named 'idealism'" warning**: the drop areas must be named after the choice (`Idealism`, `Realism`, `Pragmatism`).
- **"Chorus 'x' has no choices in the story index"**: the knot's choices must start with `Idealism.`, `Realism.` or `Pragmatism.`; run `ContentTests.AllStoriesValidate` for the exact problem.
- **A choice stays locked**: hover its requirement slots; each tooltip shows the required and the current value.
- **The token will not drop**: a `UIBlock`-layer element is in front of the drop area, or a choice was already made.
- **Chance looks wrong**: the challenge slot and the roll both use `ChorusRules`; `EventSystemTests.Odds_MatchTheDisplayedChanceAndCoverEveryRoll` keeps them equal.
