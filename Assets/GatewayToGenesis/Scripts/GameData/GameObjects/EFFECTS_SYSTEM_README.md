# Effects: Authoring Guide

Legends, civics, council seats, weather and event consequences all describe their gameplay effects the
same way and are applied by the same code (`EffectRouter`). Learn the fields once and they work everywhere.
For how the system is built, see `Scripts/ARCHITECTURE.md`.

## 1. The fields

| Field | Legend (`LegendBonus`) | Civic (`CivicEffect`) | Weather (`WeatherEffect`) | Seat (`SeatBonus` / `CivicSeatBonus`) |
|---|---|---|---|---|
| What it does | `bonusType` | `effectType` | `effectType` | `bonusType` (seat vocabulary) |
| Amount | `modifierValue` | `modifierValue` | `modifierValue` | `modifierValue` |
| How the amount applies | `modifierType` | `modifierType` | `modifierType` | `modifierType` |
| What it affects | `targetStat` | `targetStat` | `targetStat` | `targetStat` (empty = everything) |
| What it scales with | `conditionStat` | `conditionStat` | `conditionStat` | — |
| How wide | `scope` | `scope` | `scope` | — |

- **`modifierType`**: `Add` (flat amount), `Percentage` (25 = +25%), `SetValue` (stats only: sets the value).
- **`scope`**: `Individual` (the named target), `Section` (every item whose `section` is the target), `Global`
  (everything; `targetStat` is ignored). An empty `targetStat` also means everything.
- Names are **case-insensitive** (`Waltz` = `waltz`) but must otherwise match exactly: a section is
  `Vital Resource`, not `Vital Resources`.

## 2. Effect types

| Type | `targetStat` | Positive value means | Notes |
|---|---|---|---|
| `PillarBonus` | `aureus`, `regalia`, `waltz`, `chorus` | higher pillar | Substats and derived stats follow automatically |
| `SubstatBonus` | `innovation`, `piety`, `authority`, `ambition`, `symphony`, `euphony`, `arcane`, `secrecy` | higher substat | |
| `DerivedStatBonus` | `discoveryEfficiency`, `savingRollChance`, `legendEffectiveness`, `expeditionCostMod`, `expeditionTimeMod`, `satisfactionEffectiveness`, `moraleLossMod`, `moraleRecoveryMod`, `clickPowerBonus`, `magicEffectiveness` | higher value | Derived stats are percentages (4.5 = 4.5%) |
| `ResourceModifier` | resource, or section with Section scope | more output | `Add` = +N per second; `Percentage` = +N% output |
| `ProductionModifier` | production unit, section, unit type (`Workshop`...), `units`, or a resource | more output | Always a percentage. A resource target boosts that resource's output from every producer |
| `ClickPowerBonus` | resource, or section | stronger clicks | `Add` or `Percentage` |
| `ConstructionCostModifier` | production unit, section, unit type | **more expensive** | Always a percentage. **Use negative values for discounts** (-15 = 15% cheaper). Never below 10% of base cost |
| `ProductionScalingBonus` | resource produced | more output | +N per second for each counted unit. `conditionStat` = what is counted: a production unit, section or unit type, or a game value (`housing`, `population`, a stat...). Empty = every unit that produces the target |
| `MaxMoraleModifier` | — | higher max morale | |
| `MoraleBalanceModifier` | — | **lower** balance (easier to stay above it) | Shifts the reference for production and satisfaction, not where morale drifts |
| `SatisfactionThresholdModifier` | — | **lower** upgrade threshold (easier upgrades) | |
| `HousingBonus` | — | more housing | `Add` or `Percentage` of total housing. Removed with its source like every effect |
| `MoraleModifier` | — | higher displayed morale | While the source is active |
| `SpecialAbility` | free text | — | Placeholder: logged, no generic behaviour yet |

Seat bonuses use the same list under `SeatBonusType` names. `SatisfactionModifier` (legacy) means
`+satisfactionEffectiveness`; `CivicBonus` is a marker with no effect.

## 3. When effects apply and how they scale

| Source | Applies | Removed | Scaling |
|---|---|---|---|
| Civic effects | when the civic is unlocked | when it is removed | none |
| Weather effects | while the weather is active | when it changes | none |
| Legend bonuses | once the legend's seat is **active** (after the activation delay, 3 sevenths by default) | when unseated or the seat changes | × Head of State multiplier (Head of State only); × (1 + Legend Effectiveness%) except `authority` and `legendEffectiveness` bonuses |
| Seat bonuses | from the moment a legend sits in the seat (legend bonuses wait for activation) | seat replaced or legend leaves | none |
| Event consequences | when the story completes | timed ones after `duration:sevenths:N`; others never | none |

Nothing can be applied twice: every source replaces its whole contribution when it re-applies, and removing
a source always restores the previous values exactly.

## 4. Examples from the game's content

```
Guild Masters (civic)       ProductionModifier   +10%  targetStat Elderwood          → +10% Elderwood from every producer
Innovation Council (civic)  ProductionModifier   +25%  targetStat Food               → +25% Food from every producer
                            SubstatBonus         +25%  targetStat authority
Military Academy (civic)    ProductionScalingBonus +35 targetStat Food, condition housing → +35 Food/s per point of housing
                            ConstructionCostModifier +35% targetStat Decaying Hut    → Decaying Huts cost 35% MORE
Dragon McPusey (legend)     ResourceModifier     +20%  Section "Old World Remnants"    → +20% output of that section
Boiling Rain (weather)      ResourceModifier     +120% Section "Vital Resource"
```

The Guild Masters, Innovation Council and Military Academy effects targeting a resource or a single building did
nothing before the effect system was unified (they were looked up as unit types). They now apply as written, so
their values deserve a balance pass; Military Academy's scaling in particular grows with housing.

## 5. Checking your work

Content is validated at start-up (and by the EditMode test `ContentTests.AllContentValidates`). Each problem is
one Console warning naming the asset, the effect type and what is wrong, for example:

```
[ContentValidator] Weather 'Boiling Rain': ResourceModifier 'Vital Resources' is neither a resource nor a section
```

Common mistakes:
- Plural or misspelled section names (the validator catches them; at runtime the effect silently does nothing).
- Expecting a positive `ConstructionCostModifier` to be a discount.
- Putting a building name in `ResourceModifier` (use `ProductionModifier` for buildings).
- `SetValue` on anything other than a stat (treated as `Add`, with a warning).
