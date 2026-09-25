# Gateway to Genesis: Code Architecture

This is the reference for how the game's code fits together and how to extend it. Content authoring
details live next to the content: `GameData/GameObjects/EFFECTS_SYSTEM_README.md` (effects),
`Resources/Legends/README.md`, `Resources/Civics/README.md`, `GameData/Events/INK_FILES_GUIDE.md`.

## 1. Layers

| Layer | Folder | Rule |
|---|---|---|
| Core | `Scripts/Core` | Shared services with no scene state of their own. Everything else builds on these. |
| Systems | `Scripts/GameData` | One scene singleton per game system. Each owns its state and the modifier ledgers it consumes. |
| Presentation | `Scripts/UI`, `GameData/Events/*Screen*`, tooltips, tabs | Reads systems and calls their public API. Never computes game rules. |
| Content | `Assets/Resources` (+ Ink) | ScriptableObjects and Ink files. Adding content means adding assets, not code. |

## 2. Core services

| Service | Answers | Use it instead of |
|---|---|---|
| `ModifierLedger` | "What does each source contribute to X?" Flat + percent per (target, source). | Per-system bonus dictionaries |
| `ModifierTargets` | Scoped keys: `Food`, `section:Vital Resource`, `type:Workshop`, `*`. `Resolve` sums all four for one item. | "Pending bonus" lists for items that do not exist yet |
| `GameEffect` + `GameEffectAdapters` | One shape for every authored effect (legend, civic, seat, weather, event). | Converting between bonus classes by hand |
| `EffectRouter` | Applies/removes effects: `ApplySet(source, effects, multiplier)`, `RemoveSource(source)`, `Validate(effect)`. One handler per `GameEffectType`. | Switch statements over effect types |
| `GameCatalog` / `AssetCatalog<T>` | Every authored asset by name (case-insensitive), loaded once, including event art; resources by role (`ResourceFor(ResourceRole.Food)`). | `Resources.Load*` in systems, resource names in code |
| `GameValues` | Current value of any condition: `("stat","waltz")`, `("resource","Food")`, `("population","")`... | Per-feature condition switches |
| `StatDefinitions` / `StatGrowth` | Stat vocabulary (kinds, parents, canonical keys) and derived-stat growth curves (pure math). | Stat-name string lists and formulas in systems |
| `ContentValidator` | Start-up check of all content with the same rules used at runtime. | Per-system validation |
| `SingletonBehaviour<T>` | Scene singleton with `OnSingletonAwake/Destroy`. Read other systems' `Instance` when you use them, not in `Awake` (wake order is not guaranteed). | Hand-written `Instance` patterns, `FindAnyObjectByType` |
| `GameLog` + `LogChannel` | Logging by topic channel (Units, Weather, Council...), switched on in the scene's `GameLoggingSystem` (one multi-select field). `Warning`/`Error` always print. | `Debug.Log` in gameplay code |
| `InkFunctions` (Events) | The one set of C# functions Ink can call; preview mode for read-only stories. | Binding functions per Ink consumer |
| `GovernmentCompass` (GameData) | Pure political-compass math and government names. | Classification switches |

### Who owns which ledger

| Ledger | Owner | Target keys | Meaning |
|---|---|---|---|
| `Modifiers` | `StatManager` | stat key (`waltz`, `legendeffectiveness`, `maxmorale`...) | flat / % on the stat |
| `ResourceModifiers` | `GlobalProductionManager` | resource / section / type / `*` | flat per second (negative = consumption) / % output |
| `ProductionEfficiency` | `GameUnitsLogic` | production unit / section / type / `*` | % output of the unit |
| `ConstructionCost` | `GameUnitsLogic` | production unit / section / type / `*` | % build cost (positive = more expensive) |
| `ClickPower` | `GameUnitsLogic` | resource / section / type / `*` | flat / % click power |
| `ProductionScaling` | `GameUnitsLogic` | `resource\|counter` | + resource per counted unit (or game value) |
| `HousingModifiers` | `PopGrowthLogic` | `housing` | flat / % housing |

## 3. Effect lifecycle

```
LegendBonus / CivicEffect / SeatBonus / WeatherEffect / EventConsequence
        │  .ToEffect()  (GameEffectAdapters, EventSystemLogic.TryGetEffect)
        ▼
EffectRouter.ApplySet(source, effects, multiplier)      ◄── removes the source first: re-applying never stacks
        │  one handler per GameEffectType, target resolved to a scoped key
        ▼
owner.Ledger.Add(targetKey, source, value)
        │  ledger.Changed
        ▼
owning system recomputes (StatManager.Recalculate, GlobalProductionManager.MarkDirty, housing...)

Removal anywhere: EffectRouter.RemoveSource(source)     ◄── restores the exact previous state
```

A target that does not exist yet (a resource discovered later) is simply stored under its key and read
when the item appears. Nothing needs to be re-applied when content is unlocked.

### Source names

| Source | Name | Lifetime |
|---|---|---|
| Legend in a council seat | `Legend: {legendName}` | Once it has finished activating (`SeatActivationSevenths` after seating), while seated |
| Council seat bonus | `Council Seat: {seat title}` | From the moment a legend sits in the seat, while it stays |
| Civic | `Civic: {civicName}` | While the civic is active |
| Weather | `Weather: {asset name}` | While the weather is active |
| Event consequence (permanent) | `Event: {story title}` | Forever; repeats accumulate |
| Event consequence (timed) | `Event: {story title} #{n}` | `durationSevenths`, then removed |
| Technology modifier unlockable | `Technology: {tech}` | Forever |

These names appear in tooltips as the "from ..." label.

## 4. Core formulas

- **Pillar** = `(base + flat) × (1 + % / 100)`, min 1. Base = inspector value + event changes.
- **Substat** = `(round(pillar × pillarMultiplier) + event adjustments + flat) × (1 + %)`, min 1.
- **Derived stat** = `(StatGrowth.Evaluate(substat) + flat) × (1 + %)`. Curves are tiered (42/86/150/220 by default).
- **Thresholds** (max morale, morale balance, satisfaction upgrade) = `(base + flat) × (1 + %)`.
- **Resource net rate** = `[Σ producers rate × units × (1 + efficiency%) + scaling + positive flat] × (1 + (resource% + morale delta)/100) − consumption`.
- **Click power** = `(base + permanent upgrades + flat) × (1 + (% + Click Power Bonus stat)/100)`, never below base.
- **Build cost** = `base × (1 + cost%)` (floored at 10% of base) `× e^(costBalance / techTier × owned)` for buildings.
- **Housing** = `(scene + buildings + events + flat) × (1 + %)`.
- **Council** (`CouncilRules`, re-applied from scratch on any change): a seat's bonuses apply as soon as a legend
  sits in it, the legend's own bonuses once it has activated; an empty seat gives nothing. Phase A authority, phase B
  direct Legend Effectiveness, phase C everything else. Legend bonuses are multiplied by the Head of State multiplier
  (Head of State only) and, in phase C, by `1 + LE%`. Seat bonuses are never scaled.

## 5. Systems

| System | Owns | Listens to | Raises |
|---|---|---|---|
| `TimeSystemLogic` | Calendar (seventh/phase/echo/cycle), pause, slow motion | — | `OnSeventhChange`, `OnPhaseChange`, `OnEchoChange`, `OnCycleChange`, `OnRitualSeventh` |
| `StatManager` | Pillars, substats, derived stats, morale, satisfaction | seventh | `OnStatsChanged`, `OnPillarChanged`, `OnDerivedChanged`, `OnMoraleChanged`, satisfaction events |
| `GameUnitsLogic` | Storage/production/research tabs, clicking, building, research, unit ledgers | — | `OnProductionUnitBuilt` |
| `GlobalProductionManager` | Net rates of every resource | ledgers, morale | writes `GameResourceSlot.productionRate` |
| `ProductionLogic` (per resource slot) | Accrues the net rate every second (paused during events) | — | — |
| `PopGrowthLogic` | Population, housing, vagrants, deaths, food demand | Food changes | — |
| `GovernmentLogic` | Political compass, council seats, cooldowns, council effects (rules in `CouncilRules`) | stats, seventh | government and council events |
| `LegendLeaderLogic` | Legend roster, activation delay setting | — | — |
| `CivicManager` | Active civics, slots, requirements, removal penalties | seventh | civic events |
| `CelestialWeatherSystemLogic` | Weather selection and effects (rules in `WeatherRules`) | seventh, echo, cycle | `OnWeatherChanged` |
| `EventSystemLogic` | When stories trigger, consequences, timed effects, scores | time events | — |
| `InkDrivenEventSetup` | Turns every compiled story in `Resources/Events` into a volume and validates it | — | — |
| `EventVolumeManager` | Live Ink stories, story start/navigation/completion (the one `CompleteStory`) | — | — |
| `EventScreenManager`, `ChorusScreenManager` | Event screens and the chorus drag-and-roll, as views over the story index | pillars (chorus) | — |
| `TooltipSystemLogic` | The one tooltip box: shows, refreshes (4 Hz) and hides it | — | — |

The event layer is split so each rule lives once and is testable without a scene:

| Module | Owns |
|---|---|
| `EventScript` | The Ink authoring grammar: knot tags, conditions, consequences (with `duration:`), choice metadata |
| `EventStoryIndex` | Every knot of every compiled story, precompiled at start-up (content, choices, chorus choices, targets) |
| `ChorusRules` | The chorus d100: success %, Piety bonus, crit bands, rare events, costs, outcome odds |
| `EventText` | Player-facing wording of consequences and requirements, with running "New Total"s |
| `EventContentCheck` | Names in stories (resources, sections, buildings, technologies, weather, civics) exist in the catalogs |

Tooltips follow the same pattern: a `TooltipTrigger` shows either its custom text (`SetCustom`) or content from
the nearest `ITooltipSource` above it (resource, building and technology slots, unlockables, legends, seats,
civics, the Head of State); every game-object tooltip is worded in `TooltipContent`, which reads numbers from the
system that owns them (for example build costs from `GameUnitsLogic.GetBuildCost`).

Start-up order does not matter: singletons register in `Awake`, subscriptions happen in `Start` (or through
`TimeSystemLogic.WhenReady`), and ledgers accept effects before their owners' dependents exist.

## 6. Adding content (no code)

| To add | Create | Where |
|---|---|---|
| Resource | `GameUnit` (set `role` only for the one food and the one research resource) | `Resources/GameUnits/GameResources/` |
| Building or unit | `GameUnit` + `ProductionUnitData` (link the unit) | `Resources/GameUnits/GameProductionUnits/`, `Resources/Production/` |
| Technology | `GameUnit` + `TechnologyData` (+ `TechUnlockable`s) | `Resources/GameUnits/GameTechnologies/`, `Resources/Technology/`, `Resources/TechUnlockables/` |
| Section (tab group, effect scope) | `SectionData` (name must match `GameUnit.section`) | `Resources/Sections/` |
| Legend | `LegendData` | `Resources/Legends/` |
| Civic | `CivicData` | `Resources/Civics/` |
| Weather | `WeatherProfileSO` | `Resources/WeatherProfiles/` |
| Council seat | entry in `GovernmentLogic.defaultSeatConfigs` (scene) or a civic's council position | scene / civic asset |
| Event | Ink story + compiled JSON | `Resources/Events/` (see INK_FILES_GUIDE) |

After adding content, run the EditMode tests **ContentTests.AllContentValidates** and **ContentTests.AllStoriesValidate**
(or press Play and read the Console): every broken reference is reported by asset and field, and every story
problem (unknown keyword, broken knot link, a resource or section name that does not exist) by knot.

## 7. Extending the engine

- **New effect type**: append to `GameEffectType` (never insert: enums are serialized by index), add a handler in
  `EffectRouter.Handlers`, its wording in `GameEffect.Sentence`, and validation in `EffectRouter.Validate`.
  `CoreSystemsTests.EveryEffectTypeHasAHandler` and `EveryEffectTypeHasItsOwnSentence` fail until both exist.
  Authored effect classes (`LegendBonus`, `CivicEffect`, `WeatherEffect`, `SeatBonus`, `CivicSeatBonus`) have no
  wording or validation of their own: `GetAutoDescription()` is `ToEffect().Describe()`, and `ContentValidator`
  checks them with `EffectRouter.Validate`.
- **New system that accepts modifiers**: give it a `ModifierLedger`, recompute on `ledger.Changed`, and add it to
  `EffectRouter.Ledgers()` so `RemoveSource` reaches it.
- **New condition / value** (usable by events, civic requirements, weather, technologies and Ink at once):
  `GameValues.Register("domain", resolver)`. For event conditions, add the enum value and its domain to
  `EventCondition.Domains`; for weather conditions, append to `WeatherCondition.ConditionType` and map it in
  `WeatherCondition.DomainOf`; for civic requirements, append to `RequirementType` and map it in
  `CivicRequirement.ToCondition` (tests enforce all three). Civic requirements are event conditions, so they are
  worded by `EventText` and checked by `EventContentCheck` like story requirements.
- **New Ink function**: add it once in `InkFunctions.Bind`; wrap game-changing actions in `Act(...)` so preview
  stories stay side-effect free.
- **New stat**: add it to `StatDefinitions` (and a curve in `StatGrowth.Curves` for derived stats).
- **New event consequence that is a modifier**: map it in `EventSystemLogic.TryGetEffect`; timed durations then
  work automatically.
- **New Ink keyword** (condition domain or consequence type): add it to the maps in `EventScript`, its wording to
  `EventText` (`EventSystemTests.Wording_EveryConsequenceTypeHasItsOwnSentence` fails until it has one), and its
  effect to `EventSystemLogic`; a named target also goes into `EventContentCheck`.
- **New chorus rule**: change `ChorusRules` only; the roll, the challenge slot's chance and the outcome tooltip all
  read it, and `EventSystemTests.Odds_MatchTheDisplayedChanceAndCoverEveryRoll` keeps them in step.
- **Tooltip for a new UI element**: implement `ITooltipSource` on its component and word it in `TooltipContent`;
  put a `TooltipTrigger` on the element (or a child). Fixed text from code: `TooltipTrigger.Ensure(go).SetCustom(...)`.
- **New HUD click button with a resource picker**: subclass `ResourceSelectionWindow` and route resources to it in
  `ResourceSelectionWindow.ClickButtonFor`.

## 8. Conventions

- Scene singletons derive from `SingletonBehaviour<T>`; put setup in `OnSingletonAwake`, teardown in `OnSingletonDestroy`.
- Compare Unity objects with `== null`, not `?.`/`??` (destroyed and missing objects are "fake null").
- Never store a value that can be derived: keep sources in a ledger and compute totals.
- Serialized field names are scene data. Rename with `[FormerlySerializedAs]`; append enum values, never reorder.
- Log through `GameLog` with the system's channel (`private const LogChannel Log = LogChannel.X;`). A new system
  reuses the channel of its topic; a new topic appends a value to `LogChannel` (next free bit). No per-system
  "log this" booleans: the channel is the switch.
- Developer shortcuts use `InputUtils.DebugKeyDown` (Ctrl + key, editor and development builds only).

## 9. Tests

`Assets/GatewayToGenesis/Tests/Editor` (Window > General > Test Runner > EditMode):
- `CoreSystemsTests`: ledger math, scoped targets, effect routing and validation, stat growth, compass, and guards
  that fail when an enum value is added without being wired up.
- `EventSystemTests`: the Ink grammar, chorus odds and outcomes, event wording, sentence splitting, tooltip
  formatting. Pure logic, so they also run outside Unity.
- `GameRulesTests`: weather selection (retention, variation boost, candidacy, weighted pick), council timing,
  scaling and assignment plans, cooldown timers, civic requirements (as event conditions) and log channels.
- `ContentTests`: every authored asset and every compiled story validates against the catalogs.

Game rules that decide outcomes live in pure classes next to their system (`ChorusRules`, `WeatherRules`,
`CouncilRules`, `GovernmentCompass`, `StatGrowth`) so they can be tested; the system keeps the state and supplies
random rolls.

## 10. Known limitations and risk areas

Ranked by how likely they are to cause trouble as content grows.

1. **No save/load.** All state is runtime-only. The ledger design helps: persist sources (active civics, seated
   legends, weather, timed events, bases), not totals, and re-apply on load.
2. **Hard-coded names**: special technology unlockables are matched by asset name in
   `GameUnitsLogic.HandleSpecialUnlockable`. Food and Research
   are no longer names: the population finds them by `GameUnit.role` (`GameCatalog.ResourceFor`), so either can be
   renamed. The population still has exactly one food: a second food-like resource is an ordinary resource unless
   `PopGrowthLogic` is extended to eat from several.
3. **Unimplemented hooks**: `SpecialAbility` effects and `EraUnlock` requirements are accepted but do nothing
   (effects log when applied; `ContentValidator` reports every `EraUnlock` requirement, which always passes).
   `LegendData.compatibleSeatClasses` is unused: seats decide eligibility.
4. **Legacy data**: `GatewayToGenesis/ScriptableObjects/` (ResourceSO, ProductionEntitySO, AgeSO, Decision,
   PillarCivilization) belongs to the first prototype and is not read by the game, except that the HUD's
   VitalResource and BuildingMaterial buttons still carry a second OnClick entry calling `addResource` on the old
   Food/Elderwood `ResourceSO` assets (in the Editor they write to the assets every click). Run
   **Tools > Gateway to Genesis > Retire Legacy ScriptableObjects** twice: the first run removes those entries
   (then save the scene), the second archives the folder once nothing references it. Delete
   `Scripts/Editor/LegacyScriptableObjectsCleanup.cs` afterwards. Unused prototype scripts were moved to
   `GatewayToGenesis/_Archive~` (ignored by Unity).
5. **UI found by name**: a few views locate scene objects by GameObject name rather than a reference: the chorus
   drop areas (`Idealism`/`Realism`/`Pragmatism` under the backgrounds container), the HUD click buttons
   (`BuildingMaterial`/`VitalResource`) and the Head of State portrait (`HeadOfState/Sprite`). Renaming those objects
   breaks the link silently; keep the names or replace them with serialized references. Two more lookups by name
   warn at start-up instead of failing silently: every tab managed by `TabHotkeys` must keep its panel under a
   `Display` child, and `GlobalCharacterManager` parents villagers under the spawn area's `Population` child
   unless its `populationParent` field is assigned.
6. **Event screen prefabs**: `EventScreenManager` fills screens by child names (`Title`, `Subtitle`, `Content`,
   `Button`/`ButtonText`, `Consequences`, `HasHappened`, `EventColor`, `Background`, ...) and the chorus result card
   by `Result` and `RollChance`. A prefab variant must keep those names.
