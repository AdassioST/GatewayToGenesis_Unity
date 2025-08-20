# Chorus Screen System Guide

The Chorus Screen is the most important event screen type, handling conditional checks, luck-based outcomes, and event branching. This system integrates seamlessly with the existing Ink narrative engine and reuses existing requirement-checking logic.

## 🎯 **New System Overview**

The updated Chorus Screen system features:
- **Smooth alpha transitions** for background elements during token dragging
- **Hidden background elements** that reveal as choices become invisible
- **Distance-based alpha changes** - closer to center = lower alpha, further = higher alpha
- **Base alpha 30%** for all choice backgrounds when not interacting
- **Alpha increases to 95%** as token gets further from center

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

### 1. ChorusScreenManager
The main coordinator that:
- Parses choice data from Ink files
- Manages screen variant selection (FullOutcomes vs TwoChoices)
- Handles choice resolution and story advancement
- Manages background alpha transitions during token dragging

### 2. ChorusChoice
Individual choice components that:
- Display choice information (title, description, requirements)
- Handle hover interactions and alpha transitions
- Manage requirement checking and challenge slots
- Become invisible during token drag to reveal background elements

### 3. DecisionToken
Draggable token that:
- Tracks distance from screen center
- Communicates with ChorusScreenManager for alpha updates
- Triggers choice selection when dropped
- Returns to center if dropped on invalid area

### 4. ChallengeSlot
Simplified challenge display with:
- Pillar icon (Aureus, Regalia, Waltz, Chorus)
- Visual chance indicator (sprite changes based on percentage)
- Tooltip-based information display

### 5. PillarDisplay
Civilization pillar values with:
- Icon and current amount
- Tooltip-based name display
- Automatic updates from StatManager

## 🎨 **Alpha Transition System**

### How It Works
1. **Base State**: All background elements start at 30% alpha
2. **During Drag**: Choices container becomes invisible, revealing background elements
3. **Distance Calculation**: Token distance from center determines alpha values
4. **Smooth Transitions**: DOTween handles all alpha changes with easing

### Alpha Values
- **Center (0 distance)**: 30% alpha (base)
- **Edge (max distance)**: 95% alpha (maximum)
- **Smooth interpolation** between these values based on token position

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

### 1. Screen Initialization
1. `ChorusScreenManager.InitializeChorusScreen()` is called
2. Ink story is parsed for choice metadata from the chorus knot
3. Screen variant is selected (FullOutcomes vs TwoChoices)
4. Choice UI elements are created
5. Pillar values are displayed

### 2. Token Interaction
1. Player starts dragging the decision token
2. `OnTokenDragStarted()` is called
3. Choices container becomes invisible
4. Background elements become visible with base alpha

### 3. Alpha Transitions
1. Token position is tracked during drag
2. Distance from center is calculated
3. `UpdateBackgroundAlphaByTokenDistance()` is called
4. Background alpha is smoothly interpolated based on distance

### 4. Choice Selection
1. Token is dropped on a valid choice
2. `OnChoiceSelected()` is called
3. Challenge is resolved (if applicable)
4. Story advances to the chosen path

## 🎯 **Key Features**

- **Smooth Visual Feedback**: Alpha transitions provide clear visual indication of choice preference
- **Efficient Rendering**: Only necessary UI elements are active at any time
- **Tooltip Integration**: Hover information is displayed through existing tooltip system
- **Requirement Checking**: Reuses existing `EventCondition` system
- **Challenge Resolution**: Automatic success/failure calculation based on pillar strengths
- **Ink Integration**: Seamless parsing of choice metadata from chorus knots

## 🔧 **Customization Options**

### Alpha Transition Curves
- Modify `alphaTransitionDuration` for faster/slower transitions
- Adjust `maxDistanceForAlpha` for different screen sizes
- Change easing functions in DOTween calls

### Visual Feedback
- Customize chance sprites for different success percentages
- Modify base and max alpha values
- Add additional visual effects during transitions

### Choice Behavior
- Add custom requirement types
- Implement additional challenge mechanics
- Customize choice resolution logic

## 🐛 **Troubleshooting**

### Common Issues
1. **Choices not appearing**: Check `chorusChoicePrefab` assignment
2. **Alpha not changing**: Verify `backgroundImage` references in ChorusChoice components
3. **Token not draggable**: Ensure `DecisionToken` component is properly configured
4. **Pillars not updating**: Check `StatManager` reference and pillar type names

### Debug Information
- Enable debug logging in `ChorusScreenManager`
- Check console for parsing errors
- Verify Ink file structure and metadata tags

## 🚀 **Future Enhancements**

- **TwoChoices Variant**: Implement simplified two-choice system
- **Additional Choice Types**: Support for more than three choices
- **Advanced Animations**: Particle effects, sound feedback
- **Save/Load System**: Persist choice history and consequences
- **Analytics**: Track player choice patterns and preferences 