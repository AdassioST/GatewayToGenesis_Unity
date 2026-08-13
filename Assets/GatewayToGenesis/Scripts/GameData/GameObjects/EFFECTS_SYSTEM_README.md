# 🎯 **Effects System Development Guide**

## 🎯 **Overview**

This comprehensive guide explains how to create effects for both **Legends** and **Civics** in the Gateway to Genesis system. The effects system provides a powerful, metadata-driven approach to creating bonuses and modifiers that integrate seamlessly with the game's core mechanics.

---

## 🏗️ **System Architecture**

### **🎯 Core Components**
The Effects System consists of several interconnected components:

- **`LegendData`**: ScriptableObject for individual legend leaders with their bonuses
- **`CivicData`**: ScriptableObject for civic policies with their effects
- **`LegendLeaderLogic`**: Manages legend loading and applies bonuses to game systems
- **`CivicManager`**: Manages civic loading and applies effects to game systems
- **`StatManager`**: Central hub for stat modifications and persistent bonuses
- **`GovernmentLogic`**: Integrates council seat bonuses with the government system

### **🎨 Effect Flow System**
Effects progress through a structured application system:

1. **Creation**: Effects defined in ScriptableObjects with metadata
2. **Validation**: System checks required fields before applying
3. **Application**: Effects modify game systems through appropriate managers
4. **Persistence**: Bonuses are stored and maintained until removed
5. **Integration**: Effects work with council seats, government types, and other systems

---

## 📁 **File Organization & Setup**

### **🎯 Directory Structure**
```
Assets/
├── GatewayToGenesis/
│   └── Scripts/
│       └── GameData/
│           ├── GameObjects/
│           │   ├── LegendData.cs          # Legend effect definitions
│           │   └── CivicData.cs           # Civic effect definitions
│           ├── LegendLeaderLogic.cs       # Legend bonus application
│           ├── CivicManager.cs            # Civic effect application
│           └── GovernmentLogic.cs         # Council seat integration
└── Resources/
    ├── Legends/                           # Legend ScriptableObjects
    └── Civics/                            # Civic ScriptableObjects
```

### **🚀 Automatic Integration System**
The system automatically:
- ✅ Loads all Legend and Civic ScriptableObjects
- ✅ Validates effect configurations
- ✅ Applies bonuses when legends/civics are assigned
- ✅ Removes bonuses when legends/civics are removed
- ✅ Integrates with council seat system for additional bonuses

---

## ✍️ **Effect Structure & Syntax**

### **🎯 Basic Effect Structure**
```csharp
// In LegendData or CivicData ScriptableObject
public List<LegendBonus> bonuses = new List<LegendBonus>();  // For Legends
public List<CivicEffect> effects = new List<CivicEffect>();  // For Civics

// Each bonus/effect contains:
public LegendBonusType bonusType;        // Type of effect
public float modifierValue;              // How much to modify
public ModifierType modifierType;        // How to apply the modifier
public string targetStat;                // What to modify (when needed)
public string conditionStat;             // What triggers scaling effects
public string scope;                     // Global scope for wide effects
```

### **🎯 Required Fields by Effect Type**
Different effect types require different field combinations:

| Effect Type | Required Fields | Optional Fields | Description |
|-------------|----------------|-----------------|-------------|
| **PillarBonus** | `targetStat` | - | Modifies pillar stats (aureus, regalia, waltz, chorus) |
| **SubstatBonus** | `targetStat` | - | Modifies substats (innovation, piety, authority, etc.) |
| **DerivedStatBonus** | `targetStat` | - | Modifies derived stats (discovery efficiency, etc.) |
| **ResourceModifier** | `targetStat` OR `scope` | - | Modifies resource production |
| **ProductionModifier** | `targetStat` OR `scope` | - | Modifies building/unit production efficiency |
| **ClickPowerBonus** | `targetStat` OR `scope` | - | Modifies click power for resources |
| **ProductionScalingBonus** | `targetStat`, `conditionStat` | - | Adds bonus per production unit |
| **ConstructionCostModifier** | `targetStat` OR `scope` | - | Reduces construction costs |
| **MaxMoraleModifier** | - | - | Increases maximum morale |
| **MoraleBalanceModifier** | - | - | Makes it easier to maintain positive morale |
| **SatisfactionThresholdModifier** | - | - | Makes satisfaction upgrades easier |
| **HousingBonus** | - | - | Increases housing capacity |

---

## 🎯 **Effect Type Reference**

### **🏛️ Pillar & Substat Bonuses**
**Purpose**: Modify core civilization statistics

```csharp
// Example: +2 Aureus pillar bonus
bonusType = LegendBonusType.PillarBonus;
targetStat = "aureus";
modifierValue = 2;
modifierType = ModifierType.Add;

// Example: +15% Innovation substat bonus
bonusType = LegendBonusType.SubstatBonus;
targetStat = "innovation";
modifierValue = 15;
modifierType = ModifierType.Percentage;
```

**Supported Stats**:
- **Pillars**: `aureus`, `regalia`, `waltz`, `chorus`
- **Substats**: `innovation`, `piety`, `authority`, `ambition`, `symphony`, `euphony`, `arcane`, `secrecy`
- **Derived**: `discoveryEfficiency`, `savingRollChance`, `legendEffectiveness`, `expeditionCostMod`, `expeditionTimeMod`, `satisfactionEffectiveness`, `moraleLossMod`, `moraleRecoveryMod`, `clickPowerBonus`, `magicEffectiveness`

### **🏭 Production & Resource Modifiers**
**Purpose**: Modify production efficiency and resource generation

```csharp
// Example: +10% Food production
bonusType = LegendBonusType.ResourceModifier;
targetStat = "food";
modifierValue = 10;
modifierType = ModifierType.Percentage;

// Example: +5% all building efficiency
bonusType = LegendBonusType.ProductionModifier;
scope = "all buildings";
modifierValue = 5;
modifierType = ModifierType.Percentage;
```

**Scope Options**:
- **Specific**: Use `targetStat` for individual resources/buildings
- **Global**: Use `scope` for wide effects:
  - `"all buildings"` - affects all production buildings
  - `"all resources"` - affects all resource production
  - `"Building"` - affects building-type production units
  - `"Unit"` - affects unit-type production units

### **⚡ Click Power Bonuses**
**Purpose**: Modify click power for manual resource gathering

```csharp
// Example: +25% Food click power
bonusType = LegendBonusType.ClickPowerBonus;
targetStat = "food";
modifierValue = 25;
modifierType = ModifierType.Percentage;

// Example: +5 click power for all resources
bonusType = LegendBonusType.ClickPowerBonus;
scope = "all resources";
modifierValue = 5;
modifierType = ModifierType.Add;
```

### **🏗️ Construction Cost Modifiers**
**Purpose**: Reduce the cost of building construction

```csharp
// Example: -15% Housing construction cost
bonusType = LegendBonusType.ConstructionCostModifier;
targetStat = "housing";
modifierValue = 15;
modifierType = ModifierType.Percentage;

// Example: -10% all building construction costs
bonusType = LegendBonusType.ConstructionCostModifier;
scope = "all buildings";
modifierValue = 10;
modifierType = ModifierType.Percentage;
```

### **📈 Production Scaling Bonuses**
**Purpose**: Add bonus production based on existing production units

```csharp
// Example: +0.1 Food per Housing
bonusType = LegendBonusType.ProductionScalingBonus;
targetStat = "food";           // What is produced
conditionStat = "housing";      // What triggers the bonus
modifierValue = 0.1;
modifierType = ModifierType.Add;

// Example: +2 Elderwood per Ancient Windmill
bonusType = LegendBonusType.ProductionScalingBonus;
targetStat = "elderwood";      // What is produced
conditionStat = "ancient windmill"; // What triggers the bonus
modifierValue = 2;
modifierType = ModifierType.Add;
```

**How It Works**:
- **`targetStat`**: The resource that gets the bonus production
- **`conditionStat`**: The production unit that triggers the bonus
- **`modifierValue`**: How much bonus production per unit
- **Result**: If you have 10 Housing, you get +1 Food per seventh

### **😊 Morale & Satisfaction Modifiers**
**Purpose**: Modify civilization happiness and satisfaction systems

```csharp
// Example: +20 max morale
bonusType = LegendBonusType.MaxMoraleModifier;
modifierValue = 20;
modifierType = ModifierType.Add;

// Example: Morale balance -5 (easier to stay positive)
bonusType = LegendBonusType.MoraleBalanceModifier;
modifierValue = 5;
modifierType = ModifierType.Add;

// Example: +10 satisfaction threshold (easier upgrades)
bonusType = LegendBonusType.SatisfactionThresholdModifier;
modifierValue = 10;
modifierType = ModifierType.Add;
```

**Morale System**:
- **Max Morale**: Increases the upper limit of morale (default: 200)
- **Morale Balance**: Decreases the equilibrium point, making it easier to stay above 100
- **Satisfaction Threshold**: Makes it easier to upgrade satisfaction levels

### **🏠 Housing Bonuses**
**Purpose**: Increase housing capacity for population growth

```csharp
// Example: +15 housing capacity
bonusType = LegendBonusType.HousingBonus;
modifierValue = 15;
modifierType = ModifierType.Add;

// Example: +25% housing capacity
bonusType = LegendBonusType.HousingBonus;
modifierValue = 25;
modifierType = ModifierType.Percentage;
```

**Housing System**:
- **Persistent**: Housing bonuses cannot be destroyed or removed
- **Population Growth**: More housing allows more population growth
- **Vagrant Conversion**: Excess housing converts vagrants to population

---

## 🔧 **Modifier Type System**

### **🎯 ModifierType Enum**
```csharp
public enum ModifierType
{
    Add,         // Add to base value (e.g., +5)
    Percentage,  // Add as percentage (e.g., +15%)
    SetValue     // Set to specific value (e.g., = 100)
}
```

### **📊 How Modifiers Work**

#### **Add Modifier**
```csharp
modifierType = ModifierType.Add;
modifierValue = 5;
// Result: Base value + 5
// Example: If base Aureus is 10, final is 15
```

#### **Percentage Modifier**
```csharp
modifierType = ModifierType.Percentage;
modifierValue = 25;
// Result: Base value × (1 + 25/100) = Base value × 1.25
// Example: If base Food production is 100, final is 125
```

#### **SetValue Modifier**
```csharp
modifierType = ModifierType.SetValue;
modifierValue = 100;
// Result: Value is set to exactly 100
// Example: Sets a stat to exactly 100 regardless of base value
```

---

## 🎨 **Creating Effects in Unity Editor**

### **🎯 Step-by-Step Process**

#### **1. Create Legend/Civic ScriptableObject**
```
Right-click in Project → Create → Game Object → Legend Data
Right-click in Project → Create → Game Object → Civic Data
```

#### **2. Configure Basic Information**
- **Name**: Descriptive name for your legend/civic
- **Description**: Flavor text explaining the character/policy
- **Icon**: Visual representation
- **Legend Class** (for Legends): Sovereign, Vanguard, Steward, Weaver, Seer, Justiciar
- **Rarity & Tier** (for Civics): Common/Uncommon/Rare/Legendary, Minor/Major/Aeonic

#### **3. Add Effects**
- Click the **+** button in the Effects list
- Select the **Effect Type** from the dropdown
- Fill in **Required Fields** based on the effect type
- Set **Modifier Value** and **Modifier Type**
- The system automatically generates descriptions

#### **4. Validation**
- The system automatically validates your effect configuration
- Check the Console for any validation errors
- Required fields are highlighted in the Inspector

---

## 🏛️ **Council Seat Integration**

### **🎯 How Council Seats Work**
Council seats automatically convert civic effects into seat bonuses:

```csharp
// Civic effect automatically becomes seat bonus
civicEffect.effectType = CivicEffectType.ResourceModifier;
civicEffect.targetStat = "food";
civicEffect.modifierValue = 10;
civicEffect.modifierType = ModifierType.Percentage;

// Becomes seat bonus when assigned to council
seatBonus.bonusType = SeatBonusType.ResourceModifier;
seatBonus.targetStat = "food";
seatBonus.modifierValue = 10;
seatBonus.modifierType = ModifierType.Percentage;
```

### **🎯 Council Seat Requirements**
- **Legend Class**: Only legends of allowed classes can be assigned
- **Active State**: Bonuses only apply when the seat is active (3 sevenths after assignment)
- **Legend Required**: Most bonuses require a legend to be assigned

---

## 📚 **Complete Effect Examples**

### **🎯 Legend Example: Master Craftsman**
```csharp
// Legend: Master Craftsman (Weaver Class)
// Effect 1: +15% all building production efficiency
bonusType = LegendBonusType.ProductionModifier;
scope = "all buildings";
modifierValue = 15;
modifierType = ModifierType.Percentage;

// Effect 2: +2 Elderwood per Ancient Windmill
bonusType = LegendBonusType.ProductionScalingBonus;
targetStat = "elderwood";
conditionStat = "ancient windmill";
modifierValue = 2;
modifierType = ModifierType.Add;

// Effect 3: -10% construction costs for all buildings
bonusType = LegendBonusType.ConstructionCostModifier;
scope = "all buildings";
modifierValue = 10;
modifierType = ModifierType.Percentage;
```

**Result**: A Weaver legend that boosts building efficiency, provides scaling bonuses, and reduces construction costs.

### **🎯 Civic Example: Agricultural Renewal**
```csharp
// Civic: Agricultural Renewal (Minor Tier)
// Effect 1: +20% Food production
effectType = CivicEffectType.ResourceModifier;
targetStat = "food";
modifierValue = 20;
modifierType = ModifierType.Percentage;

// Effect 2: +0.5 Food per Housing
effectType = CivicEffectType.ProductionScalingBonus;
targetStat = "food";
conditionStat = "housing";
modifierValue = 0.5;
modifierType = ModifierType.Add;

// Effect 3: +10 housing capacity
effectType = CivicEffectType.HousingBonus;
modifierValue = 10;
modifierType = ModifierType.Add;
```

**Result**: A civic that significantly boosts food production through multiple mechanisms.

---

## ⚠️ **Best Practices & Guidelines**

### **🎯 Effect Design**
- **Keep effects focused** - one clear purpose per effect
- **Use appropriate scopes** - `targetStat` for specific, `scope` for general
- **Balance modifier values** - avoid overpowered combinations
- **Consider synergies** - effects can work together for powerful combinations

### **🎯 Technical Considerations**
- **Validate effects** - ensure all required fields are filled
- **Test combinations** - verify effects work together correctly
- **Use consistent naming** - maintain clear naming conventions
- **Document dependencies** - track which effects work with which systems

### **🎯 Performance Optimization**
- **Limit effect complexity** - avoid overly complex effect chains
- **Use efficient scopes** - prefer specific over general when possible
- **Reasonable modifier values** - avoid extreme values that could cause issues
- **Clean configurations** - avoid unnecessary or duplicate effects

---

## 🚀 **Testing & Validation**

### **🎯 Development Workflow**
1. **Create** your effect configuration in the Unity Editor
2. **Validate** required fields are properly set
3. **Test** effect application in isolation
4. **Integrate** with council seat system
5. **Playtest** complete effect combinations

### **🎯 Debug Tools**
The system provides extensive logging:
- **Effect validation** - see why effects are/aren't valid
- **Bonus application** - track all system modifications
- **Council integration** - monitor seat bonus processing
- **Performance metrics** - track effect application efficiency

### **🎯 Common Issues & Solutions**
- **Effects not applying**: Check required fields and validation
- **Bonuses not showing**: Verify council seat assignment and activation
- **Unexpected results**: Check modifier types and value ranges
- **Performance issues**: Limit effect complexity and scope usage

---

## 🎉 **System Capabilities Summary**

### **✅ What You Can Create**
- **Complex stat modifications** with multiple modifier types
- **Production scaling systems** based on existing infrastructure
- **Global and specific effects** for different gameplay styles
- **Council seat integration** for government mechanics
- **Persistent bonus systems** that survive game state changes
- **Synergistic effect combinations** for powerful gameplay

### **✅ Advanced Features**
- **Automatic validation** - prevents invalid configurations
- **Smart description generation** - no manual description writing needed
- **Flexible scope system** - target specific or general effects
- **Modifier type variety** - additive, percentage, and set value options
- **Council seat conversion** - automatic civic-to-seat bonus conversion
- **Performance optimization** - efficient bonus application and tracking

---

## 🔮 **Future Expansion**

The Effects System is designed for extensibility:
- **New effect types** can be easily added
- **Additional modifier types** expand gameplay possibilities
- **Enhanced validation** provides more robust error checking
- **Advanced scoping** supports more complex effect targeting
- **Integration systems** connect with other game mechanics

---

## 📖 **Additional Resources**

- **`LegendData.cs`**: Complete effect type definitions
- **`CivicData.cs`**: Civic effect system implementation
- **`LegendLeaderLogic.cs`**: Legend bonus application logic
- **`CivicManager.cs`**: Civic effect application logic
- **Unity Console**: Extensive debug logging and error reporting

---

## 🎯 **Getting Started Checklist**

- [ ] Create your first Legend or Civic ScriptableObject
- [ ] Add basic effect with required fields
- [ ] Test effect validation in the Inspector
- [ ] Verify automatic description generation
- [ ] Test effect application in-game
- [ ] Integrate with council seat system
- [ ] Create synergistic effect combinations
- [ ] Test complete effect chains
- [ ] Iterate and expand your effect universe

---

**The Gateway to Genesis Effects System provides a powerful, flexible foundation for creating rich, interactive bonuses that deeply integrate with your game's mechanics and progression systems. Start simple, build complexity gradually, and unleash your creativity!** 🚀✨ 