# Civics System Documentation

## Overview

The Civics System is a comprehensive framework for managing foundational principles, institutions, and lived realities of society in your civilization game. Civics represent the backbone of civilizations and evolve over time as pillars shift, eras advance, and institutional reforms occur.

## System Architecture

### Core Components

1. **CivicData** - ScriptableObject defining individual civics
2. **CivicManager** - Central system managing civic acquisition, removal, and effects
3. **Council System** - 6-position temporary council for Major/Aeonic civics
4. **Integration Points** - Connected with StatManager and GovernmentLogic

### Civic Tiers

- **Aeonic Civics (Tier 1)** - Destiny-defining, 1 at start, max 2
  - Always grant Council positions
  - Examples: "Choir of Eternal Harmony"
  
- **Major Civics (Tier 2)** - Defining institutions, 1 slot on start → 3-4 mid/late
  - Often grant Council positions
  - Examples: Guild Masters, Justice System
  
- **Minor Civics (Tier 3)** - Flavor + optimizations, 2 slots on start → 3-5 slots
  - Examples: Cultural Festivals, Economic Traditions

## Creating Civics

### 1. Create CivicData Asset

1. Right-click in Project window → Create → Game Object → Civic Data
2. Fill in the required fields:

#### Basic Information
- **Civic Name**: Unique identifier for the civic
- **Description**: Flavor text describing daily life/immersion
- **Icon**: Visual representation (optional)

#### Classification
- **Rarity**: Common, Uncommon, Rare, Legendary
- **Tier**: Minor, Major, or Aeonic

#### Council Integration
- **Grants Council Position**: Whether this civic creates a council seat
- **Council Position Name**: Name of the council position
- **Council Position Priority**: Lower numbers = higher priority

#### Effects
- **Effects List**: Array of CivicEffect objects
- Each effect has:
  - Effect Type (PillarBonus, SubstatBonus, etc.)
  - Target Stat (stat name to modify)
  - Modifier Value (amount to add/multiply)
  - Modifier Type (Additive, Multiplicative, etc.)
  - Description (human-readable effect description)

#### Requirements
- **Requirements List**: Array of CivicRequirement objects
- Each requirement has:
  - Requirement Type (PillarStat, GovernmentType, etc.)
  - Requirement Target (stat name, government type, etc.)
  - Required Value (threshold value)
  - Comparison (>, >=, ==, <=, <, !=)
  - Description (human-readable requirement)

#### Conflicts & Dependencies
- **Conflicting Civics**: List of civics that cannot coexist
- **Required Civics**: List of civics that must be present

#### Removal Penalties
- **Satisfaction Penalty**: Immediate satisfaction points penalty when removed
- **Morale Penalty Per Seventh**: Ongoing morale penalty per time unit
- **Removal Penalty Duration**: How long penalties last

### 2. Example Civic: Choir of Eternal Harmony

```yaml
Civic Name: Choir of Eternal Harmony
Tier: Aeonic
Rarity: Legendary
Description: "The ancient melodies of the Choir resonate through every aspect of society..."

Effects:
- Pillar Bonus: +2 Waltz
- Substat Bonus: +3 Symphony, +2 Euphony  
- Morale Modifier: +10

Requirements:
- Waltz >= 8
- Chorus >= 6

Removal Penalties:
- Satisfaction: -25 points
- Morale: -2 per seventh for 10 sevenths
```

## Using the System

### 1. Unlock Civic

```csharp
// Unlock a civic from any system
bool success = CivicManager.Instance.UnlockCivic("Choir of Eternal Harmony", "Event System");
```

### 2. Remove Civic

```csharp
// Remove a civic (with penalties)
bool success = CivicManager.Instance.RemoveCivic("Choir of Eternal Harmony", "Reform");
```

### 3. Check Civic Status

```csharp
// Check if civic is active
bool isActive = CivicManager.Instance.IsCivicActive("Choir of Eternal Harmony");

// Get all active civics
List<CivicData> activeCivics = CivicManager.Instance.GetAllActiveCivics();

// Get civics by tier
List<CivicData> aeonicCivics = CivicManager.Instance.GetActiveCivics(CivicTier.Aeonic);
```

### 4. Council System

```csharp
// Get all council positions
List<CouncilPosition> positions = CivicManager.Instance.GetCouncilPositions();

// Check if position is occupied
foreach (var position in positions)
{
    if (position.isOccupied)
    {
        Debug.Log($"Position {position.positionName} occupied by {position.sourceCivic.civicName}");
    }
}
```

## Integration Points

### StatManager Integration

- Civic effects automatically modify pillar and substat values
- Morale modifiers are applied through the existing morale system
- Satisfaction penalties use the existing satisfaction system

### GovernmentLogic Integration

- Government type changes trigger civic requirement checks
- Civics can require specific government types
- Government changes can unlock new civic opportunities

### Time System Integration

- Removal penalties are processed every seventh
- Ongoing effects can be time-based
- Civic effects can be temporary or permanent

## Debug Controls

### Keyboard Shortcuts

- **C**: Print civic system information
- **U**: Test unlock "Choir of Eternal Harmony"
- **R**: Test remove "Choir of Eternal Harmony"
- **G**: Test unlock "Guild Masters"
- **F**: Test unlock "Cultural Festivals"

### Console Commands

```csharp
// Print all civic info
CivicManager.Instance.PrintCivicInfo();

// Force government recalculation
GovernmentLogic.Instance.ForceRecalculation();
```

## Future Enhancements

### Planned Features

1. **Era System Integration** - Civics adapt across different eras
2. **Leader Management** - Assign leaders to council positions
3. **Advanced Effects** - Resource modifiers, production bonuses
4. **Civic Evolution** - Civics change over time based on usage
5. **Dynamic Requirements** - Requirements that change based on game state

### Extension Points

- **CivicEffectType**: Add new effect types
- **RequirementType**: Add new requirement types
- **ModifierType**: Add new modifier application methods
- **Council System**: Expand beyond 6 positions
- **Penalty System**: More sophisticated removal penalties

## Best Practices

### Design Guidelines

1. **Balance**: Ensure civic effects are proportional to their tier
2. **Requirements**: Make requirements meaningful but achievable
3. **Conflicts**: Use conflicts to create interesting choices
4. **Penalties**: Penalties should discourage removal but not be crippling
5. **Flavor**: Descriptions should enhance immersion

### Performance Considerations

1. **Lazy Loading**: Civics are loaded only when needed
2. **Event-Driven**: Changes trigger events rather than polling
3. **Caching**: Active civics are cached for quick access
4. **Batch Updates**: Multiple changes can be batched together

## Troubleshooting

### Common Issues

1. **Civic Not Loading**: Check Resources/Civics folder path
2. **Effects Not Applying**: Verify StatManager.Instance exists
3. **Requirements Not Met**: Check requirement values and comparisons
4. **Council Position Issues**: Verify council system is enabled

### Debug Tips

1. Enable civic logging in CivicManager inspector
2. Use PrintCivicInfo() to see system state
3. Check console for requirement failures
4. Verify civic asset configuration in inspector 