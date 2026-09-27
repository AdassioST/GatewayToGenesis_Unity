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
| `legendClass` | The kind of Great Spellweaver the legend became: Sovereign, Seer, Concertist, Justiciar, Vanguard, Architect or Chronicler (shown as "Great Sovereign"...). A seat accepts several classes (a High Arbiter can be a Great Sovereign, Justiciar or Architect). |
| `rarity` | Common, Uncommon, Rare, Epic, Mythic (Legendary also exists). |
| `bonuses` | List of effects (type, value, modifier type, target, condition, scope). |
| `canBeHeadOfState` | Required to be seated as Head of State. |
| `compatibleSeatClasses` | Not used by the game: seats decide which classes they accept. |
| `soulLeitmotif` | Optional. The Soul Leitmotif's primary binding (Resonance, Luminance, Flux, Void, Cindergale, Crystal, Strand). Empty: the class's affinity (Stellar Legacy Score.md: a Great Architect is usually Crystal). |
| `originTraits` | Optional. Origin traits (The Wish) by their names in `Legend Trait.md`'s tables. Empty: one is drawn whose Scaled Cost fits the rarity. |
| `personalityTraits` | Optional, up to three. Personality traits (The Expression) by their names in `Legend Trait.md`. Missing ones are drawn; the first drawn leans toward the primary binding. |

3. Press Play (or run the EditMode test `ContentTests.AllContentValidates`). Any bonus with an unknown stat,
   resource, section or unit is reported as a warning naming this legend, and so is a trait name the vault's
   tables do not have.

No code or scene change is needed: the roster is read from this folder.

## Souls: traits, bindings and Composure

Who a legend is lives beside its asset, in `LegendProgress` (saved with the run): a `LegendSoul` created when the
legend is met. Drawn traits are seeded by the world and the legend's name, so one world always gives a legend the
same traits.

| File here | What it is |
|---|---|
| `Legend Trait.md` | The vault's note, copied verbatim (Tools > Gateway to Genesis > Import Lore From Vault, or Import Legend Notes From Vault). Its origin tables, personality tables and middle-trait table are read by `LegendTraitNote`; its gaps are logged by the import and listed in `Library/Vault~/Canon Gaps.md`. |
| `Composure.md` | The vault's note, copied verbatim; its five bullets describe the states in tooltips, and its bold lines word the Spiraling and Surrender notices. |
| `LegendSettings.asset` | Every number of Composure and the soul (thresholds, strain and rest rates, council factors, Motif Awakening, the Awakened State, binding bonuses, origin budgets by rarity). All proposals: tune here, not in code. |

- **Composure** (`ComposureRules`, `LegendSoulLife`): seated legends carry the Age Crisis and grieve the dead;
  legends on an expedition carry the road's hardship and its mishaps (`Expeditions`, `WorldSystem.HardshipOf`); rest
  heals. Fractured and Spiraling dim the legend's own council bonuses; Surrender loses the legend to Dissonance.
- **Motif Awakening** (`LegendSoulRules.Awaken`): healing back to Clouded after a real wound adds an Ornament and
  evolves one personality trait along it; with both Ornaments, healing from Spiraling reaches the Catalytic Abyss of
  Emotion (the Awakened State: council bonuses doubled for a while, then a collapse).
- The Game Wiki's "Legend Composure" article is the player's version of these rules.

## The council

| Position | Index | Accepts |
|---|---|---|
| Head of State | -1 | any legend with `canBeHeadOfState` |
| Regular positions | 0-5 | the classes of the seat placed there |

- The six regular positions hold **default seats** (assets in `Resources/Council`: High Arbiter, Oracle, Master of
  Craft, Treasurer, Supreme Commander, Civil Regent; see the README there) or **civic seats** granted by active
  civics. Each seat accepts several legend classes. `unlockedSeatCount` positions are open at the start (3); more open with
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
Each legend's bonuses are also multiplied by its rank (+20% per rank above the first) and its Composure
(`LegendProgress.CouncilMultiplier`: 0.9 Fractured, 0.7 Spiraling, 2 in the Awakened State; `LegendSettings`).
Seat bonuses are never scaled.

Example: a rank 1, Clouded legend with `ResourceModifier +10% Food`, seated as Head of State, with Legend
Effectiveness 20%: `10 × 2 × 1.20 = +24% Food` once active; Spiraling, `10 × 2 × 0.7 × 1.20 = +16.8%`.

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
