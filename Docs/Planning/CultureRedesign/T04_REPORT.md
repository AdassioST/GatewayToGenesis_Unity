# T04 completion report: a meaningful calendar and ritual repertoire

Agent A, Wave 2, 28 September 2026. Built on T01's contract (`Culture/Integration/*`) and T03's memory queries (`MemoryCauses`, `RememberedCause`, `WhyNotRemembrance`, `KeepRemembrance`). All numbers are proposals ("Observances" in Canon Gaps; the vault was not edited).

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Observance/ObservanceModel.cs`: the saved types. `Observance` is the schedule entry, `ObservancePreparation` a reserved table and `ObservanceOccasion` one decided occasion. `ObservanceState` (version 1) lives in `CultureExtensionState.observances` (`[SaveOptionalField]`). Also the enums (recurrence, objective, scale, outcome; append-only), `ObservancePlan` and the `ObservanceView` snapshot.
- `.../Observance/ObservanceTuning.cs`: scales, objective rewards, windows, caps and the authored definitions. Each carries a canon label and a vault note.
- `.../Observance/ObservanceRules.cs`: pure rules. `ObservanceCalendar` covers dates on the game's clock (Seventh index, Echo 63, Cycle 252, Ritual Seventh = 21st). The rules cover establishment checks, `Due`/`Resolve` (each occasion decided once; `lastResolved` only moves forward), holiday linking, preparation and postponement checks, feast values and rewards.
- `.../Observance/CultureSystem.Observance.cs`: commands with previews, the daily decision (`KeepObservances`, the single completion owner for every day on the calendar), the reservation lifecycle, queries, and `ObservanceFeature` (reconcile only).
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Observance/ObservancePanel.cs`: the calendar panel (list, plan and detail modes) and a body section.
- Tests: `Tests/Editor/ObservanceTests.cs` (14 pure) and `Tests/Editor/ObservancePlayTests.cs` (scene).

## Shared patches (Agent A's integration files, applied here)

- **Serving boundary** (`Culture/Integration/CultureServing.cs`, `CultureSystem.Serving.cs`), shared by T04, T05 and T06:
  - `IRecipeCultureRead` (`RecipeInfo`, `RecipeOfDish`, `RecipeInfos`): read-only, with no derivation.
  - `Serve`, `Reserve`, `Unreserve` and `ServeReserved`: exact resources, all or nothing.
  - `WhyNotServable`: only Edible, Eleos Tea or Beverage stock can be served; ingredients and spices never.
  - `PortionsOf`: converts food value to portions of a dish.
- `CultureSettings.observances` (an `ObservanceTuning`). An existing `Culture.asset` keeps the field initializer defaults.
- `CultureSystem.Life.cs`: `KeepHolidays` is gone. `LifeSeventh` calls `KeepObservances`, and `EstablishHoliday` links the new holiday's observance, so today counts as already decided.
- Conditions: `CultureSystem.Value` routes to `ObservanceValue`, which answers `observances`, `observances_kept` and `observance:<definition>`. The `GameValues` comment lists them.
- `ContentValidator.ValidateObservances`, which checks:
  - ids, the vault note and the family;
  - Ages, technologies and costs;
  - that each definition recurs, and that a ground says where;
  - that remembrances and gatherings can be kept quietly;
  - that rites exist and suggested foods are recipes or stored foods.
  - The last Age may lie past the authored Ages (the vigil and burials end at the vault's Age III); it must not come before the first.
- `CultureWindow.cs` (C composes it): the Holidays mode now shows `ObservancePanel.Fill`, the window's reset calls `ObservancePanel.Reset`, and the body lists observances through `ObservancePanel.Describe`. The change is minimal; C may move it.
- `CultureLifePlayTests`: its holiday check faked "an Echo passed" by clearing `lastKept` on the same date. That is now correctly refused, so the fixture advances the world's Echo instead.

## Player-visible behaviour

- **Culture window, Holidays (the calendar):** today's date, including the full Moon. Each authored observance appears with its objective and canon label. A definition closed by its Age, technology, founding or the calendar's room stays listed with the reason.
- **Setting one apart** (plan mode). The player chooses:
  - the recurrence and day;
  - the place (the vigil's venues only);
  - the cause (a loss the people remember, T03);
  - the rite, the dish, tea or drink, and the scale.

  The final button shows the full preview before anything is committed: the day, the first date and how far off it is, the cost, and what each occasion asks and gives. It also discloses the fallback: "if the stores cannot set it on the day, it is kept quietly".
- **One observance** (detail mode):
  - prepare its table, or put a prepared table away;
  - postpone the next day by 3 or 7 Sevenths;
  - scale it down or up, or serve another food from now on;
  - take it off the calendar.
  - The last 8 occasions are listed with what each spent.
- **Authored content:**
  - **Holiday** (the anniversary): every existing holiday, migrated on its own day.
  - **Day of Remembrance**: canon-supported (The Inescapable Hunger). Every Echo, needs a remembered loss, quiet or modest, with Ash-Loaf or Candlevein Grief Tea suggested.
  - **Offering of the First Golden Fruit**: canon-supported, Age 0 only. Every Echo or once per Cycle; a shared table with Dried Auric Peaches or Honeyed Peach Tart; costs 10 Unity.
  - **Moonlit Vigil**: explicit canon (Civic.md, Ages 0-III). Ritual Seventh only, in a settlement by Glimmerfern, a grove or a glade. Quiet only, and one per venue.
  - **Sky Glass Burial**: explicit canon (Civic.md, Ages 0-III). Every Echo, near peaks or Sky Glass, needs a loss, and asks 3 Faith above quiet.

  No Carnival or choral institution appears in Age 0.
- **Objectives:**

  | Objective | Unity | Morale | Joy |
  |---|---|---|---|
  | Celebration | 8 | +4 | 0.3 |
  | Remembrance | 4 | 0 | 0 |
  | Hospitality | 6 | +3 | 0.2 |
  | Performance | 7 | +2 | 0.2 |
  | Gathering | 3 | +2 | 0.1 |

  A remembrance also keeps its cause through T03's `KeepRemembrance`. Scale shares: Quiet gives half the rewards and serves no food; Modest gives 0.75 and serves half the food; Full gives everything.
- **Occurrences:** keeping a day records up to three, one per purpose:
  - `Observance`, key `obs:<id>:<date>`;
  - its table as `Hospitality` (portions, with the recipe id when there is one), key `…:table`;
  - its rite as `Performance`, or `Gathering` for a remembrance, key `…:rite`.
- **Quiet memorial:** a Day of Remembrance needs no festival, high morale, Unity or food. It does not go through `WhyNotHoliday`.

## Save and edge behaviour

- **Migration:** `AfterRestore` → `ObservanceFeature.Reconcile` → `LinkHolidays`, which is idempotent. Each holiday gets a legacy observance on its own Seventh and Phase with its tally. Its `lastResolved` is set to today, so nothing past is replayed or invented. A holiday nobody customized is still kept by the original `KeepHoliday` (its `lastKept` guard is also honoured).
- **Once only:** each occasion is decided once, because `lastResolved` never moves back. A second Tick on the same date, a reload, or two reloads followed by a Tick give no second reward and no second charge (play-tested).
- **Time jumps:** days jumped over are resolved as `Missed`: nothing spent, nothing given, and a prepared table is returned whole. At most 64 occasions per observance are decided per Seventh; any excess is caught up next Seventh, all as missed.
- **Preparation:** the food leaves the stores at preparation, all or nothing, and the reservation is saved. Cancelling, missing the day, retiring the observance or loading a stale table returns it: `Unreserve` gives back what the stores have room for and reports that amount. A reserved table is served on its day without being spent again. The plan cannot be changed while a table is prepared.
- **Insufficient food with nothing prepared:** the table is revalidated on the day. If the chosen dish or the generic food value is not there, the day is kept quietly (`KeptSmaller`) and nothing is spent. An unpaid keep cost reduces an unprepared table to quiet, or a prepared Full table to Modest.
- **Postponement:** once per occasion, 1-7 Sevenths, never onto the next occasion, and never for a Ritual Seventh (the full Moon does not move). A prepared table follows the new date.
- **Missing references:**
  - A vigil or burial whose settlement is gone, or whose ground no longer fits, is decided as `Missed` with the reason.
  - A remembrance whose cause is unknown can't be established.
  - A food whose recipe disappears fails `WhyNotServable`, and the day falls back to quiet.
  - A legacy observance whose holiday is gone ends.
  - `Ensure` repairs null lists, raises `nextId` past saved ids, and drops preparations for unknown observances.
- **New game:** the state lives in `CultureState.extensions`, so a new game starts empty. Event subscribers are only UI.

## Tests actually run

- **Offline reflection runner** (private build, 0 errors): `ObservanceTests` 14/14.
- **Unity batchmode, run 1** (filter `Observance|Culture|Tradition|Content|Save|Hospitality`): 189/191.
  - `ObservanceTests` 14/14 and `ObservancePlayTests` 1/1 passed.
  - Failure 1: `ContentTests.AllContentValidates` (mine). The vigil and burials end in Age 3, which the catalog lacks. Fixed in the validator (above).
  - Failure 2: `CultureTransmissionPlayTests` ("Ember Bread is not known to the stores yet"). This is T06's lane; its agent was re-running it at the time.
- **Unity batchmode, run 2** after the fix (filter `ContentTests|Observance|CultureLifePlayTests|CultureMemoryPlayTests|TraditionPlayTests`): 29/29.
- Not run: a visual check of the panel (the play test only builds it with no exception), and the full EditMode suite.

## One acceptance path (`ObservancePlayTests`)

1. **Migrate a holiday.** Found Iridia. A holiday from before observances ("The Day of Sparks", 5th Seventh, first Phase, kept twice) is loaded. It appears on the calendar on the same day, is kept once on it (kept 3), and a second Tick that day keeps nothing.
2. **Keep a quiet remembrance.** The Hunger passes. With no festival and a holiday still out of reach, set apart a Day of Remembrance for it. It is quiet and free, and the preview says "nothing from the stores". On its day it is kept with nothing spent, and one occurrence is recorded.
3. **Prepare a table and reload.** Set apart the Offering of the First Golden Fruit (10 Unity) with Dried Auric Peaches on the 14th Seventh. Prepare its table: the peaches leave the stores. Reload twice: still prepared, stores unchanged. Cancel: returned whole. Prepare again and reload.
4. **Keep it on its day.** It is kept once, and the table is served without being spent again. A Tick, a reload and a Tick that same day give nothing more.
5. **Bare stores, a time jump, a postponement.**
   - Next Echo with bare stores and nothing prepared: `KeptSmaller`, nothing spent.
   - Jump over two Echoes: both missed, nothing spent.
   - Prepare, then jump past the day: missed, with the table returned.
   - Postpone 3 Sevenths: its own day passes, and it is kept on the new one. The following Echo keeps its original day.
6. **Scale down, the vigil, retire.** Scale it down to modest. The Moonlit Vigil is refused on an ordinary day; with no fitting grove it asks for a place, otherwise it is set on the Ritual Seventh. Retire the offering (its history is kept); a holiday cannot be retired.

## Remaining defects and proposals

- **The vigil on the seed world:** whether any settlement's ground fits the vigil depends on the seed (`ObservanceVenues`), so step 6 checks whichever case the world has. A grove-pinned fixture would make it deterministic.
- **Legacy holidays:** an uncustomized holiday still draws generic food value (`Pantry.TrySpend`) exactly as before. Its table cannot be prepared until a dish is chosen, which customizes it.
- **Rewards:** these are per-objective constants. Participation for Legends (`LegendProgress.ShareExperience`) and performance quality are left to T08, which can read the `…:rite` occurrence.
- **Proposals for Canon Gaps** (not written to the vault):
  - the numbers in `ObservanceTuning`;
  - keeping the First Golden Fruit once per Cycle as the Cycle's great offering;
  - a yearly Sky Glass climb for a remembered loss;
  - a Day of Remembrance as the game's form of the mourning bakeries.
