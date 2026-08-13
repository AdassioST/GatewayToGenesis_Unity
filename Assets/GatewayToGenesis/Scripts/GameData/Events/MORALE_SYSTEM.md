## Morale System — Design, Integration, and Pipelines

### What this is
- Morale is a civilization-wide integer stat managed in `StatManager`. It represents societal well-being, unity, and faith.
- Morale gravitates toward an equilibrium (`moraleBalance`, default 100) every seventh, with the strength of adjustment controlled by the `Waltz` pillar.
- Morale affects: global production efficiency, population growth food thresholds, and the `dark_morale` score for dark-event pressure. Future hooks for military and religion are planned.

## Where it lives
- `StatManager` (`Assets/GatewayToGenesis/Scripts/Managers/StatManager.cs`)
  - Holds `morale`, `moralebalance`, clamps, and seventh-based oscillation.
  - Exposes morale via the same `UpdateStat`/`GetStatValue`/`CheckStat` pathways used elsewhere.
  - Helpers: `GetMorale()`, `GetMoraleBalance()`, `GetMoraleDeltaPercent()`, `ApplyMoraleShift(...)`, `OnMoraleChanged`.
- `GlobalProductionManager` (`Assets/GatewayToGenesis/Scripts/GameData/GlobalProductionManager.cs`)
  - Adds a global morale percent modifier to all resources’ positive production.
- `PopGrowthLogic` (`Assets/GatewayToGenesis/Scripts/GameData/PopGrowthLogic.cs`)
  - Scales the food threshold for growth by morale.
- `TimeSystemLogic` (`Assets/GatewayToGenesis/Scripts/GameData/TimeSystemLogic.cs`)
  - Emits `OnSeventhChange`, which drives morale’s oscillation and timed shift expiration.
- `InkStoryManager` / Events
  - Ink can read/modify morale using existing stat pathways or `ModifyMorale`.

## Data model (StatManager)
- `morale` (int, default 100) — current morale (clamped `[minMorale, maxMorale]`).
- `moraleBalance` (int, default 100) — equilibrium target.
- `minMorale` (int, default 0), `maxMorale` (int, default 200).
- Tunables:
  - `aboveBalanceRecoveryFactor` (default 0.5): slows decay above balance.
  - `darkMoraleIncreaseFactor` (default 0.2): deficit → increases `dark_morale` per seventh.
  - `darkMoraleDecreaseFactor` (default 0.1): surplus → decreases `dark_morale` per seventh.
- Events: `OnMoraleChanged(int newMorale)`.

## Seventh-based oscillation (Waltz-controlled)
Runs on each `OnSeventhChange` tick:
- If `morale > moraleBalance`: morale decreases by `ceil(Waltz × aboveBalanceRecoveryFactor)`.
- If `morale < moraleBalance`: morale increases by `Waltz`.
- Always clamped to `[minMorale, maxMorale]`.

This creates: prosperity linger (slow decay above balance) and eager recovery (fast rise below balance).

## Integration points
- Global Production (in `GlobalProductionManager`)
  - Compute `moraleDelta = morale - moraleBalance` (rounded as int).
  - Add `moraleDelta` to each resource’s total percentage modifiers before applying.
  - Example: morale 110 vs balance 100 → `+10%` global bonus; morale 85 vs 100 → `-15%` global malus.

- Population Growth (in `PopGrowthLogic`)
  - Compute morale-adjusted threshold: `factor = 1 - (moraleDelta / 100f)`.
  - `adjustedThreshold = clamp(baseThreshold × factor, factor ∈ [0.25, 2.0], minValue = 1f)`.
  - Positive morale lowers required food; negative raises it.

- Dark Events Pressure (in `StatManager`)
  - Each seventh, compute `diff = moraleBalance - morale`.
  - If `diff > 0`: `ModifyEventScore("dark_morale", ceil(diff × darkMoraleIncreaseFactor))`.
  - If `diff < 0`: decrease `dark_morale` by `ceil((-diff) × darkMoraleDecreaseFactor)` (not below 0).
  - Use `score:dark_morale` in conditions to gate dark events.

- Time Pause
  - Events pause game time → seventh ticks (and oscillation/dark_morale drift) pause until events complete.

## Ink / Event integration
- Conditions (either in `# conditions:` or `&C requirements:`):
  - `stat:morale >= 110`
- Consequences:
  - `stat:morale +15`
- External Ink functions:
  - `ModifyStat("morale", X)` — preferred for consistency.
  - `ModifyMorale(X)` — explicit helper.
  - `GetStat("morale")` — read current morale.

Authoring guidance:
- Do NOT edit compiled JSON; change `.ink` files and let Unity auto-compile.
- Example morale usage was added in `Assets/Resources/Events/StarterVolume.ink` for testing.

## C# API (primary)
- From `StatManager`:
  - `int GetMorale()`
  - `int GetMoraleBalance()`
  - `float GetMoraleDeltaPercent()` — morale minus balance, as a percent-like delta.
  - `void UpdateStat("morale", newValue)` — set morale.
  - `void ApplyMoraleShift(int amount, string source = null, bool temporary = false, int durationSevenths = 0)` — optional timed/persistent shifts by source.
  - `event Action<int> OnMoraleChanged`
- From Ink (external):
  - `ModifyStat(statName, change)`, `ModifyMorale(change)`, `GetStat(statName)`

## Calculation pipelines
- Seventh tick
  1) `TimeSystemLogic` fires `OnSeventhChange`.
  2) `StatManager` adjusts morale toward balance using Waltz.
  3) Timed morale shifts (if any) expire and revert.
  4) `dark_morale` score is increased/decreased based on deficit/surplus.

- Production (each frame when not in event)
  1) Recompute positive/negative modifiers per resource.
  2) Aggregate percentage modifiers per resource.
  3) Add `moraleDelta` to total percent, then apply to positive production.
  4) Update net rates and resource slots.

- Population (each frame when not in event)
  1) Compute morale-adjusted food threshold.
  2) If Food ≥ threshold and rules allow, consume threshold and grow population/vagrants.
  3) Update research generation from population.

- Events
  - Pause time → pauses seventh ticks and morale drift.
  - Outro applies cumulative consequences (including `stat:morale` effects).

## Tuning (Inspector)
- In `StatManager`:
  - `morale`, `moraleBalance`, `minMorale`, `maxMorale`.
  - `aboveBalanceRecoveryFactor`, `darkMoraleIncreaseFactor`, `darkMoraleDecreaseFactor`.
- In `PopGrowthLogic`:
  - `foodThreshold` is the base; morale scales it dynamically.
- In `GlobalProductionManager`:
  - No new fields; morale is read automatically from `StatManager`.

## UI / Debug
- `StatManager.PrintAllStats()` includes morale and balance.
- Subscribe to `OnMoraleChanged` for HUD indicators.

## Future hooks
- Military: morale-driven cohesion/combat modifiers via `OnMoraleChanged`.
- Religion: high morale → boons; low morale → unrest/dark entities (use `score:dark_morale`).
- Milestones: plug population-based base thresholds into `GetMoraleAdjustedFoodThreshold()` if needed.

## Authoring examples (Ink)
```
# conditions: stat:morale >= 110; score:dark_morale < 20
# consequences: stat:morale +10

{ ModifyStat("morale", -5) }
{ ModifyMorale(+3) }
Current morale: { GetStat("morale") }
```

## Design rationale
We embedded Morale in `StatManager` to reuse existing stat/event/Ink pathways with minimal code. It centralizes seventh-based oscillation and exposes clear APIs. Production and population required small, isolated changes to integrate global effects, keeping the system clean, scalable, and easy to extend.

 