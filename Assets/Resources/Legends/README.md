# Legends

Legends are great people the player seats on the council. A seated legend becomes **active** after the
activation delay (`LegendLeaderLogic.seventhsForActivation`, 3 sevenths in the scene); from then on its
bonuses apply until it leaves the seat. Code: `LegendLeaderLogic` (roster), `GovernmentLogic` (council),
`EffectRouter` (effects). Effect fields are explained in `Scripts/GameData/GameObjects/EFFECTS_SYSTEM_README.md`.

## Adding a legend

1. Project window → Create → Game Object → Legend Data, inside this folder (`Resources/Legends`).
2. Fill in:

| Field | Notes |
|---|---|
| `legendName` | Unique. This is how code, logs and tooltips refer to the legend (`Legend: {legendName}`). |
| `originStory`, `portrait`, `councilAssignmentDescription`, `personalQuote` | Flavor. |
| `legendClass` | Sovereign, Vanguard, Steward, Weaver, Seer or Justiciar. Seats accept specific classes. |
| `rarity` | Common, Uncommon, Rare, Epic, Mythic (Legendary also exists). |
| `bonuses` | List of effects (type, value, modifier type, target, condition, scope). |
| `canBeHeadOfState` | Required to be seated as Head of State. |
| `compatibleSeatClasses` | Not used by the game: seats decide which classes they accept. |

3. Press Play (or run the EditMode test `ContentTests.AllContentValidates`). Any bonus with an unknown stat,
   resource, section or unit is reported as a warning naming this legend.

No code or scene change is needed: the roster is read from this folder.

## The council

| Position | Index | Accepts |
|---|---|---|
| Head of State | -1 | any legend with `canBeHeadOfState` |
| Regular positions | 0-5 | the classes of the seat placed there |

- The six regular positions hold **default seats** (configured on `GovernmentLogic.defaultSeatConfigs` in the
  scene: High Arbiter, Oracle, Master of Craft, Treasurer, Supreme Commander, Civil Regent) or **civic seats**
  granted by active civics. `unlockedSeatCount` positions are open at the start (3); more open with
  `GovernmentLogic.UnlockCouncilSeat(index)` or `UnlockNextCouncilSeat()`.
- Changing a seat's legend starts a cooldown on that position (1 seventh; 3 for the Head of State). A legend in
  a seat that is on cooldown cannot be moved elsewhere.
- Moving a legend onto an occupied seat swaps the two legends when the other legend qualifies for the vacated
  seat and it is off cooldown; otherwise the occupant is unseated.

## How legend bonuses are calculated

The whole council is re-applied whenever anything about it changes, in three phases:

1. `authority` bonuses (never scaled by Legend Effectiveness, which Authority produces),
2. direct `legendEffectiveness` bonuses,
3. everything else, multiplied by `1 + Legend Effectiveness%`.

A legend in the Head of State seat has every bonus multiplied by `headOfStateMultiplier` (2 in the scene).
Seat bonuses are never scaled.

Example: a legend with `ResourceModifier +10% Food`, seated as Head of State, with Legend Effectiveness 20%:
`10 × 2 × 1.20 = +24% Food` once active.

## Code

```csharp
var legend = GameCatalog.Legends.Get("Vittoria Frauter", "MySystem");
GovernmentLogic.Instance.AssignLegendToSeat(legend, 0);      // or -1 for Head of State
GovernmentLogic.Instance.RemoveLegendFromSeat(0);
CouncilSeat seat = GovernmentLogic.Instance.GetSeatWithLegend("Vittoria Frauter");   // null when unseated
bool active = seat != null && seat.IsActive();
List<LegendData> candidates = GovernmentLogic.Instance.GetAvailableLegendsForSeat(0);
```

## Debugging

- Ctrl+L (editor/development builds): print every legend, where it sits and what it contributes.
- `GovernmentLogic` context menu: Debug All Council Seats, Debug Cooldown System, Unlock Next Council Seat.
- Tick the `Council` channel on `GameLoggingSystem`.
