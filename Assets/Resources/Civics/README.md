# Civics

Civics are the institutions of the civilization. An active civic applies its effects for as long as it stays
active, may grant a council seat, and leaves penalties behind when removed. Code: `CivicManager` (slots,
requirements, penalties), `GovernmentLogic` (civic council seats), `EffectRouter` (effects). Effect fields are
explained in `Scripts/GameData/GameObjects/EFFECTS_SYSTEM_README.md`.

## Adding a civic

1. Project window → Create → Game Object → Civic Data, inside this folder (`Resources/Civics`).
2. Fill in:

| Field | Notes |
|---|---|
| `civicName` | Unique. Used by requirements, conflicts, events and logs (`Civic: {civicName}`). |
| `description`, `icon` | Flavor and UI. |
| `rarity` | Common … Legendary. |
| `tier` | `Aeonic`, `Major` or `Minor`. Each tier has its own slots (configured on `CivicManager`: max 2 / 4 / 6). |
| `effects` | Effects applied while the civic is active. |
| `requirements` | All must hold to unlock (see below). |
| `conflictingCivics` | Cannot be unlocked while any of these is active. |
| `requiredCivics` | All of these must be active. |
| `grantsCouncilPosition` + `councilPosition` | Adds a seat the player can place in the council (see below). |
| `satisfactionPenalty` | Satisfaction points lost when removed (always a loss, whatever the sign). |
| `moralePenaltyPerSeventh`, `removalPenaltyDuration` | Morale lost each seventh for that many sevenths after removal. |

3. Press Play (or run the EditMode test `ContentTests.AllContentValidates`). Unknown targets, unknown civic names
   in conflicts/requirements and council seats without legend classes are reported as warnings.

No code or scene change is needed: civics are read from this folder.

## Requirements

| `requirementType` | `requirementTarget` | Compares |
|---|---|---|
| `PillarStat`, `SubstatStat` | stat name | the stat's current value |
| `SatisfactionLevel` | — | satisfaction level (0 Forsaken … 6 Utopian) |
| `MoraleLevel` | — | current morale |
| `GovernmentType` | a `GovernmentType` name, e.g. `WaltzChorus` | exact match |
| `CivicPresent` / `CivicAbsent` | civic name | whether it is active |
| `EraUnlock` | — | not implemented yet (always met) |

Values are read through `GameValues`, the same source events and weather use.

## Council positions

When `grantsCouncilPosition` is on, `councilPosition` defines a seat:

- `title`, `description`, `icon`
- `allowAnyLegendClass` or `allowedClasses` (one of the two is required)
- `bonuses`: seat bonuses. They apply as soon as a legend sits in the seat (the legend's own bonuses follow once it
  has activated) and stop when it leaves.

The seat becomes available when the civic is unlocked; the player places it in any open position. When the civic
is removed, a position holding its seat reverts to a default seat and the legend there is unseated.

Civic **effects** and council seat **bonuses** are independent: effects apply as soon as the civic is active,
seat bonuses only through the council.

## Code

```csharp
CivicManager.Instance.UnlockCivic("Guild Masters", "Event: The Guild Charter");
var (canUnlock, reasons) = CivicManager.Instance.CanUnlockCivic("Guild Masters");
CivicManager.Instance.RemoveCivic("Guild Masters", "Reform");
bool active = CivicManager.Instance.IsCivicActive("Guild Masters");
```

## Debugging

- Ctrl+U / Ctrl+R / Ctrl+G / Ctrl+F (editor/development builds, when `enableDebugKeys` is on): unlock Innovation
  Council / remove it / unlock Guild Masters / unlock Military Academy.
- Tick the `Civics` channel on `GameLoggingSystem`.
