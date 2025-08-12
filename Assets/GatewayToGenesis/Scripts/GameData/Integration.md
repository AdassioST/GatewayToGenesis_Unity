# Multiple Story Testing - Event Volume System

## Overview
This document describes the testing setup for multiple stories within a single event volume, demonstrating the system's ability to handle complex story collections and various condition/consequence types.

## Test Stories in WeepingPrincess.ink

### 1. Weeping Princess (Environmental)
**Purpose**: Test basic population/housing conditions and death revisionism with negative values.

**Conditions**:
- `score:weeping_princess_completed == 0` (first time only)
- `resource:Food > 6` (sufficient food)
- `population:population >= 10` (flexible population check)
- `housing:housing >= 15` (flexible housing check)

**Consequences**:
- `score:weeping_princess_completed +1` (mark completed)
- `resource:Food +10` (gain food)
- `technology:Horology enlightened` (unlock technology)
- `population:population -5` (kill 5 people)
- `vagrants:vagrants +45` (add 45 vagrants)
- `death_records_revision:deaths -3` (wipe 3 deaths from public records)

**What it tests**:
- ✅ Population removal (registers deaths)
- ✅ Death revisionism (negative values)
- ✅ Resource changes
- ✅ Technology unlocking
- ✅ Flexible condition operators (>=)

### 2. Hollow Caravan (Crisis)
**Purpose**: Test death revisionism with positive values and resource penalties.

**Conditions**:
- `score:hollow_caravan_completed == 0` (first time only)
- `resource:Food > 4` (minimal food requirement)
- `population:population >= 8` (lower population threshold)
- `housing:housing >= 10` (lower housing threshold)

**Consequences**:
- `score:hollow_caravan_completed +1` (mark completed)
- `resource:Food -15` (lose food)
- `population:population -12` (kill 12 people)
- `vagrants:vagrants +20` (add 20 vagrants)
- `resource:Bone +30` (gain bone resource)
- `death_records_revision:deaths +12` (add 12 deaths to public records)

**What it tests**:
- ✅ Death revisionism (positive values)
- ✅ Resource penalties (negative changes)
- ✅ New resource types (Bone)
- ✅ Higher death counts
- ✅ Different event types (Crisis vs Environmental)

### 3. Forgotten Graves (Mystical)
**Purpose**: Test new condition types and complex death manipulation.

**Conditions**:
- `score:forgotten_graves_completed == 0` (first time only)
- `vagrant_deaths:vagrant_deaths >= 5` (test vagrant deaths condition)
- `true_deaths:true_deaths >= 20` (test true deaths condition)
- `resource:Bone > 10` (test bone resource condition)

**Consequences**:
- `score:forgotten_graves_completed +1` (mark completed)
- `resource:Bone -20` (lose bone resource)
- `vagrants:vagrants -8` (remove vagrants)
- `death_records_revision:deaths -15` (wipe 15 deaths from public records)

**What it tests**:
- ✅ Vagrant deaths conditions
- ✅ True deaths conditions
- ✅ Resource-specific conditions
- ✅ Vagrant removal (registers vagrant deaths)
- ✅ Large death record revisions
- ✅ Different event types (Mystical)

## Testing Scenarios

### Scenario 1: Weeping Princess First
**Trigger Conditions**: Population ≥ 10, Housing ≥ 15, Food > 6
**Expected Result**: Event triggers, kills 5 population, adds 45 vagrants, wipes 3 deaths from public records

### Scenario 2: Hollow Caravan Second
**Trigger Conditions**: Population ≥ 8, Housing ≥ 10, Food > 4
**Expected Result**: Event triggers, kills 12 population, adds 20 vagrants, adds 12 deaths to public records

### Scenario 3: Forgotten Graves Third
**Trigger Conditions**: Vagrant deaths ≥ 5, True deaths ≥ 20, Bone > 10
**Expected Result**: Event triggers, removes 8 vagrants, wipes 15 deaths from public records

## System Capabilities Demonstrated

### ✅ Multiple Stories in Single Volume
- Three complete stories with different themes
- Independent completion tracking
- No story interference

### ✅ Flexible Condition System
- Population: `>=` operators (not just `==`)
- Resource thresholds
- Score-based completion tracking
- New condition types (vagrant_deaths, true_deaths)

### ✅ Comprehensive Consequence System
- Population changes (removal only)
- Vagrant changes (addition/removal)
- Resource changes (positive/negative)
- Death revisionism (positive/negative values)
- Technology unlocking

### ✅ Death Tracking System
- **Deaths**: Public records that can be revised
- **TrueDeaths**: Actual count that cannot be revised
- **VagrantDeaths**: Separate tracking for homeless deaths

### ✅ Event Type Variety
- **Environmental**: Weeping Princess
- **Crisis**: Hollow Caravan  
- **Mystical**: Forgotten Graves

## Expected Volume Behavior

1. **Story Selection**: System should choose highest priority story that meets conditions
2. **Condition Evaluation**: All conditions must be met for story to trigger
3. **Consequence Application**: All consequences applied when story completes
4. **Completion Tracking**: Each story marked as completed independently
5. **No Interference**: Stories don't block each other's completion

## Debug Information

The system should log:
- Which stories are being evaluated
- Which conditions are being checked
- Which story is selected and why
- Consequence application details
- Death tracking changes (deaths vs trueDeaths)

This testing setup validates the entire event volume system's ability to handle complex, multi-story scenarios with various condition and consequence types. 