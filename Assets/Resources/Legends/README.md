# Legend Leader System Documentation

## Overview

The Legend Leader System is a comprehensive framework for managing great people and their assignments to council seats in your civilization game. Legends represent exceptional individuals who can be assigned to council positions to provide combined bonuses from their personal abilities and the civic effects of their assigned seats.

## System Architecture

### Core Components

1. **LegendData** - ScriptableObject defining individual legend leaders
2. **LegendLeaderLogic** - Central system managing legend assignments and bonuses
3. **CouncilSeat** - Council seat structure with requirements and bonuses
4. **GovernmentLogic** - Manages council seat structure and unlocking
5. **Integration Points** - Connected with StatManager, TimeSystemLogic, and CivicManager

### Legend Classes

- **Sovereign** - Noble rulers and political leaders
- **Vanguard** - Military commanders and protectors
- **Steward** - Economic managers and resource overseers
- **Weaver** - Craftsmen and artisans
- **Seer** - Mystics and knowledge keepers
- **Justiciar** - Law enforcers and judges

## Council System

### Seat Structure

The council consists of 7 total positions:

1. **Head of State** (-1 index) - Can be any legend class
2. **High Arbiter** (0 index) - Justiciar, Sovereign, or Vanguard
3. **Oracle** (1 index) - Seer or Weaver
4. **Master of Craft** (2 index) - Weaver or Seer
5. **Treasurer** (3 index) - Steward, Sovereign, or Seer (unlockable)
6. **Commander** (4 index) - Vanguard, Justiciar, or Sovereign (unlockable)
7. **Civil Regent** (5 index) - Sovereign, Vanguard, Steward, or Weaver (unlockable)

### Initial State

- Head of State + 3 seats unlocked at start
- Additional seats unlocked via `GovernmentLogic.Instance.UnlockCouncilSeat()`
- Maximum of 7 total seats

## Creating Legends

### 1. Create LegendData Asset

1. Right-click in Project window → Create → Game Object → Legend Data
2. Fill in the required fields:

#### Basic Information
- **Legend Name**: Unique identifier for the legend
- **Origin Story**: Background story and how they came to power
- **Portrait**: Visual representation (optional)

#### Classification
- **Legend Class**: One of the 6 main classes
- **Rarity**: Common, Uncommon, Rare, Epic, or Mythic
- **Tier**: Power level of the legend

#### Mechanical Bonuses
- **Bonuses List**: Array of LegendBonus objects
- Each bonus has:
  - Bonus Type (PillarBonus, SubstatBonus, etc.)
  - Target Stat (stat name to modify)
  - Modifier Value (amount to add/multiply)
  - Modifier Type (Additive, Multiplicative, etc.)
  - Description (human-readable bonus description)

#### Council Assignment
- **Can Be Head of State**: Whether this legend can serve as Head of State
- **Compatible Seat Classes**: Which seat classes this legend can fill

#### Flavor Text
- **Council Assignment Description**: Description when assigned to council
- **Personal Quote**: Famous quote or motto

### 2. Example Legend: Vittoria Frauter

```yaml
Legend Name: Vittoria Frauter
Class: Sovereign
Rarity: Rare
Tier: 3
Origin Story: "A noblewoman who came into power after the feats beating the Duvious Caravan..."

Bonuses:
- Pillar Bonus: +2 Regalia
- Substat Bonus: +3 Authority
- Morale Modifier: +15

Compatible Seats: Sovereign, Vanguard, Steward
Can Be Head of State: Yes
```

## Using the System

### 1. Assign Legend to Council Seat

```csharp
// Assign a legend to a specific seat
bool success = LegendLeaderLogic.Instance.AssignLegendToSeat("Vittoria Frauter", 0);
```

### 2. Remove Legend from Council Seat

```csharp
// Remove a legend from a specific seat
bool success = LegendLeaderLogic.Instance.RemoveLegendFromSeat(0);
```

### 3. Check Legend Status

```csharp
// Check if legend is assigned
bool isAssigned = LegendLeaderLogic.Instance.IsLegendAssigned("Vittoria Frauter");

// Get seat index where legend is assigned
int seatIndex = LegendLeaderLogic.Instance.GetLegendSeatIndex("Vittoria Frauter");

// Get all assigned legends
Dictionary<int, LegendData> assigned = LegendLeaderLogic.Instance.GetAssignedLegends();
```

### 4. Council Seat Management

```csharp
// Unlock additional council seats
GovernmentLogic.Instance.UnlockCouncilSeat(3); // Treasurer
GovernmentLogic.Instance.UnlockCouncilSeat(4); // Commander
GovernmentLogic.Instance.UnlockCouncilSeat(5); // Civil Regent

// Get council seat information
CouncilSeat seat = GovernmentLogic.Instance.GetCouncilSeat(0);
bool isActive = seat.IsActive(); // Whether effects are active
```

### 5. Get Compatible Legends

```csharp
// Get legends that can be assigned to a specific seat
List<LegendData> compatible = LegendLeaderLogic.Instance.GetCompatibleLegends(0);

// Get legends by class
List<LegendData> sovereigns = LegendLeaderLogic.Instance.GetLegendsByClass(LegendClass.Sovereign);
```

## Integration Points

### StatManager Integration

- Legend bonuses automatically modify pillar and substat values
- Morale modifiers are applied through the existing morale system
- Satisfaction modifiers use the existing satisfaction system

### TimeSystemLogic Integration

- Council effects activate after 3 cycles (configurable)
- Cycle changes trigger seat activation checks
- Ongoing effects can be time-based

### CivicManager Integration

- Civics can create specialized council seats
- Civic effects become seat bonuses
- Seat titles change based on active civics

## Council Seat Activation

### Activation Delay

- New legend assignments take 3 cycles to activate
- Effects are applied immediately but bonuses are delayed
- Cycle countdown is managed automatically

### Combined Bonuses

When a legend is assigned to a council seat, the following bonuses are applied:

1. **Legend Personal Bonuses** - Applied immediately
2. **Seat Bonuses** - Applied after activation delay
3. **Civic Effects** - Applied after activation delay (if civic-based seat)

### Example: Combined Effect

```
Vittoria Frauter (Sovereign) assigned to High Arbiter (Seat 0):

Legend Bonuses (Immediate):
- +2 Regalia
- +3 Authority  
- +15 Morale

Seat Bonuses (After 3 cycles):
- +5 Satisfaction Points

Total Effect:
- +2 Regalia, +3 Authority, +15 Morale, +5 Satisfaction
```

## Debug Controls

### Keyboard Shortcuts

- **L**: Print legend system information
- **A**: Test assigning a legend
- **R**: Test removing a legend
- **G**: Force government recalculation
- **U**: Unlock council seat 3 (Treasurer)
- **I**: Unlock council seat 4 (Commander)
- **O**: Unlock council seat 5 (Civil Regent)

### Console Commands

```csharp
// Print all legend info
LegendLeaderLogic.Instance.PrintLegendInfo();

// Force government recalculation
GovernmentLogic.Instance.ForceRecalculation();

// Check council seat status
var seat = GovernmentLogic.Instance.GetCouncilSeat(0);
Debug.Log($"Seat active: {seat.IsActive()}");
```

## Future Enhancements

### Planned Features

1. **Legend Evolution** - Legends change over time based on assignments
2. **Council Dynamics** - Interactions between different legend combinations
3. **Advanced Bonuses** - Resource modifiers, production bonuses
4. **Legend Recruitment** - Dynamic legend discovery and recruitment
5. **Council Policies** - Policies that affect all council members

### Extension Points

- **LegendBonusType**: Add new bonus types
- **Council Seat Types**: Add new seat categories
- **Activation Conditions**: More complex activation requirements
- **Legend Interactions**: Synergies between different legends
- **Council Size**: Expand beyond 7 seats

## Best Practices

### Design Guidelines

1. **Balance**: Ensure legend bonuses are proportional to their tier
2. **Compatibility**: Make seat class restrictions meaningful
3. **Activation Delay**: Use delay to create strategic timing
4. **Flavor**: Descriptions should enhance immersion
5. **Synergies**: Create interesting combinations between legends and seats

### Performance Considerations

1. **Lazy Loading**: Legends are loaded only when needed
2. **Event-Driven**: Changes trigger events rather than polling
3. **Caching**: Assigned legends are cached for quick access
4. **Batch Updates**: Multiple changes can be batched together

## Troubleshooting

### Common Issues

1. **Legend Not Loading**: Check Resources/Legends folder path
2. **Bonuses Not Applying**: Verify StatManager.Instance exists
3. **Seat Assignment Fails**: Check class compatibility
4. **Council Seat Issues**: Verify seat is unlocked

### Debug Tips

1. Enable legend logging in LegendLeaderLogic inspector
2. Use PrintLegendInfo() to see system state
3. Check console for assignment failures
4. Verify legend asset configuration in inspector
5. Check council seat unlocking status 