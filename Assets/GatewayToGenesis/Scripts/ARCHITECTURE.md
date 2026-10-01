# Gateway to Genesis: Code Architecture

What is still to build, and in what order, is [ROADMAP.md](ROADMAP.md).

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
| `GameCatalog` / `AssetCatalog<T>` | Every authored asset by name (case-insensitive), loaded once, including event art; resources by role (`ResourceFor(ResourceRole.Food)`). Resources made during play (invented dishes) live in `RuntimeUnits`, which `IsResource` also reads; the save keeps their records (`SaveDocument.runtimeUnits`) and `GameSnapshot.PrepareSlots` makes them first. | `Resources.Load*` in systems, resource names in code |
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
| Declared Age Crisis (Food output) | `Age Crisis: {crisis title}` | From the stage marked declared until the Age passes |
| Explored land (flat production) | `Land: {terrain or feature name}` | Forever; re-applied as a total per kind when a hex is explored |
| Founding myth | `Culture: Founding Myth` | From the founding, forever |
| Culture's character (its leading leaning) | `Culture: Character` | While that family leads |
| National foods (output of each) | `Culture: National Foods` | From each embrace, forever |
| A living tradition (one per definition, e.g. `Culture: Tales at the Hearth`) | `Culture: {tradition name}` | While any instance is practised or revived and within `TraditionTuning.maxActiveBenefits`; its recognised part only once the nation recognised it; removed when all lie dormant |
| Memories kept (a dedicated practice or a quiet remembrance within the window; `MemoryTuning`) | `Culture: Remembrance` | +1 morale per cause kept in the last 21 Sevenths, capped at +3; removed when the practice lapses. A loss itself gives nothing |
| Stances in force (their pillar leans and effects) | `Edicts: Stances` | While each option is the law |
| Edicts sealed by the Head of State | `Edicts: Decrees` | Each edict's duration, at the strength it was sealed with |
| The council's mood over the laws | `Edicts: Council Accord` | While the council is in harmony or discord |

These names appear in tooltips as the "from ..." label.

## 4. Core formulas

Each formula is a pure function (`StatRules`, `ProductionRules`, `PopulationRules`, `StatGrowth`, `CouncilRules`), pinned
to worked numbers in `EconomyRulesTests`; the systems gather the inputs and call them.

- **Pillar** = `(base + flat) × (1 + % / 100)`, min 1. Base = inspector value + event changes.
- **Substat** = `(round(pillar × pillarMultiplier) + event adjustments + flat) × (1 + %)`, min 1.
- **Derived stat** = `(StatGrowth.Evaluate(substat) + flat) × (1 + %)`. Curves are tiered (42/86/150/220 by default).
- **Civilization properties** (`CivilizationProperties`, the read side of the stats): Innovation → Discovery Rate
  (research cost), Piety → Saving Roll, Authority → Legend Effectiveness, Ambition → Expedition Cost (× the outfit paid to
  form one, add a companion or take on settlers, × the wear it takes from the land, hunger, strain and mishaps, and × its
  rations eaten or spoiled) and Completion Time (its pace ÷ it); both are reductions capped like the Saving Roll
  (`StatManager.expeditionCostReductionCap` / `expeditionTimeReductionCap`, 0-1, never a penalty); Symphony → Satisfaction Effectiveness,
  Euphony → Morale Loss and Recovery, Arcane → Click Power and Magic Effectiveness, Secrecy → Communion Stage (one per
  5) and Communion Bonus Effectiveness. Magic and Communion are open: their systems scale a magnitude with
  `CivilizationProperties.Magic(x)` / `Communion(x)`.
- **Thresholds** (max morale, morale balance, satisfaction upgrade) = `(base + flat) × (1 + %)`.
- **Resource net rate** = `[Σ producers rate × units × (1 + efficiency%) + scaling + positive flat] × (1 + (resource% + morale delta)/100) − consumption`.
- **Click power** = `(base + permanent upgrades + flat) × (1 + (% + Click Power Bonus stat)/100)`, never below base.
- **Build cost** = `base × (1 + cost%)` (floored at 10% of base) `× e^(costBalance / techTier × owned)` for buildings.
- **Housing** = `(scene + buildings + events + flat) × (1 + %)`.
- **Population:** one person per citizen at every scale. GrowthRules converts rations and annual birth/death rates to the 252-Seventh calendar. Food = 2,100 kcal equivalent by default; demand includes housed and unhoused residents plus a distribution allowance. Founding immigration draws from a finite saved pool. Food deliveries do not cause births. See Docs/Planning/POPULATION_GROWTH.md.

- **Morale** drifts to its resting point each seventh (down `⌈Waltz × factor⌉`, up `round(Waltz × Morale Recovery)`, min 1);
  losses shrink and gains grow with Morale Loss Mitigation.
- **Council** (`CouncilRules`, re-applied from scratch on any change): a seat's bonuses apply as soon as a legend
  sits in it, the legend's own bonuses once it has activated; an empty seat gives nothing. Phase A authority, phase B
  direct Legend Effectiveness, phase C everything else. Legend bonuses are multiplied by the Head of State multiplier
  (Head of State only) and, in phase C, by `1 + LE%`. Seat bonuses are never scaled.

## 5. Systems

| System | Owns | Listens to | Raises |
|---|---|---|---|
| `TimeSystemLogic` | Calendar (seventh/phase/echo/cycle), pause, slow motion, the player's own pause (Space, `PlayerPaused`, kept apart from the stories' `isTimePaused`; `SimulationHeld` is what production, the pantry, the people, the crowd and map units check); `TimeFlowHud` shows the flow beside the Age banner (click to pause) with a gold frame while paused | pause key (`KeyBindings`) | `OnSeventhChange`, `OnPhaseChange`, `OnEchoChange`, `OnCycleChange`, `OnRitualSeventh` |
| `StatManager` | Pillars, substats, derived stats, morale, satisfaction | seventh | `OnStatsChanged`, `OnPillarChanged`, `OnDerivedChanged`, `OnMoraleChanged`, satisfaction events |
| `GameUnitsLogic` | Storage/production/research tabs, clicking, building, research and the research plan, Enlightenment's gift, unit ledgers | — | `OnProductionUnitBuilt`, `ResearchPlanChanged` |
| `TechnologyTreeLogic` (one per Age tree) | Its slots, what is uncovered, the lines (`TechTreeConnectors`), the plan's numbers, the Enlightenment goals (rules in `TechTreeRules`) | `Researched`, `Enlightened`, `ResearchPlanChanged`, the Age's waiting gate | — (slots raise `GameTechnologySlot.Researched`, `Enlightened`) |
| `GlobalProductionManager` | Net rates of every resource | ledgers, morale | writes `GameResourceSlot.productionRate` |
| `ProductionLogic` (per resource slot) | Accrues the net rate every second (paused during events) | — | — |
| `PopGrowthLogic` | Population, housing, vagrants, deaths, food demand | calendar Seventh, paced arrivals | — |
| `GovernmentLogic` | Political compass, council seats, cooldowns, council effects (rules in `CouncilRules`) | stats, seventh | government and council events |
| `LegendLeaderLogic` | Legend roster, activation delay setting | — | — |
| `CivicManager` | Active civics, slots, requirements, removal penalties | seventh | civic events |
| `CelestialWeatherSystemLogic` | Weather selection and effects (rules in `WeatherRules`) | seventh, echo, cycle | `OnWeatherChanged` |
| `EventSystemLogic` | When stories trigger, consequences, timed effects, scores; the story's ballad actors (`CurrentCast`, rules in `BalladActors`: carried ballad actors, the expedition that found it, a named legend, the seat answering for its area via `GovernmentLogic.AnswerFor`/`CouncilAreaRules`, the Head of State, else the player picks) and ballad progress (`Ballads`, finale pays the theme) | time events | `CastChanged`, `BalladCompleted` |
| `InkDrivenEventSetup` | Turns every compiled story in `Resources/Events` into a volume and validates it | — | — |
| `EventVolumeManager` | Live Ink stories, story start/navigation/completion (the one `CompleteStory`) | — | — |
| `EventScreenManager`, `ChorusScreenManager` | Event screens and the chorus drag-and-roll, as views over the story index | pillars (chorus) | — |
| `TooltipSystemLogic` | Open tooltips (a root plus nested keyword tooltips): follows the cursor, turns them solid, refreshes (4 Hz) and closes them | — | — |
| `GameInput` | Every hotkey, as actions of `PlayerControls.inputactions` (tabs, Library, cancel) with the player's own keys laid over them (`KeyBindings`); none while typing or with Ctrl/Alt held. Escape goes to the menu while it is up, else to the open windows, and opens the quick menu when `OpenWindows.Any` was false | — | tab UnityEvents, `LibraryPressed`, `CancelPressed`, `MenuCancelPressed`, `CancelUnclaimed` |
| `SaveMenu` + `MenuView` | The title screen and the in-game quick menu (Resume, Save, Load, Achievements, Options, Return to title, Quit): `SaveMenu` holds the logic (entering worlds, autosave, `BlocksGameplay`); the autosave captures on the main thread and encrypts and writes on a worker (`SaveSession.SaveInBackground`), the slot list reads headers only (`SaveHeader`) and remembers files by their stamp, `MenuView` draws it in code on a canvas over everything | Escape (unclaimed) | — |
| `GameSettings` (static) + `SettingsApplier` | The player's Options, in PlayerPrefs apart from any save (General, Display, Audio; keys in `KeyBindings`); the applier pushes window, frame rate, listener volume and brightness into Unity; sounds join a channel with `SoundVolume` | — | `Changed(section)` |
| `GovernmentTab` | The council, civics and the legend / seat pools, as views re-bound in place once per frame | council, civics | — |
| `Achievements` (static) | The vault's achievements (Resources/Achievements), which are unlocked, the unlock toast; rules in `AchievementTriggers`, awarded only through `AchievementAward.Commit` (world ledger, then profile, then Tracker), renamed ids in `AchievementAliases`, the 100% set in `AchievementCompletion` | reported signals (after the action commits, with `.From(source, subject)` evidence) | `Unlocked` |
| `SaveSession` (static) | World slots, the lifetime profile, per-world achievement/Anchor ledgers (`WorldRewards`, with award evidence), retirement; state contract in `GameSnapshot.Schema`; world tiles are saved one column per field (`SaveDocument.tileColumns`, `GameSnapshot.TileFields`, only scalars and structs of scalars; older saves' per-tile trees still load) | save menu, autosave | — |
| `AgeProgression` | The Age clock (sevenths into the Age), Acts of Fate, crisis stages, the passage between Ages (rules in `AgeRules`, `CrisisRules`; data in `AgeDefinition`) | seventh | `Changed`, `AgeBegan`, `AgePassed` |
| `WorldSystem` | The world (`WorldMap`, built by `WorldGenerator` from `WorldSettings`, the stencil and the handmade tiles; see "The world" below), units walking the micro grid and their needs, expeditions of legends (rules in `Expeditions`: slots, the party's unit, hardship, mishaps rolled each Seventh; `HardshipOf` feeds `LegendProgress`; party size in `PartyShapes`: Solo/Duo/Trio/Company numbers, companions answering mishaps, retreat and going missing in action, the moves in `WorldSystem.Parties.cs`), land yields, each Age's magic and new features | seventh, `AgeBegan` | `Changed`, `Notice`, `UnitNotice` |
| `LegendProgress` | Which legends are met, their Lyrical Fragments by kind (rules in `LyricalFragments`), rank and deeds (rules in `LegendGrowthRules`), and who they are: each legend's `LegendSoul` (Soul Leitmotif, Legend Traits read from the vault note by `LegendTraitNote`, Composure settled each Seventh by `LegendSoulLife`/`ComposureRules`, Motif Awakenings by `LegendSoulRules`; tuning in `Resources/Legends/LegendSettings`); rank and Composure feed `CouncilRules` through `CouncilMultiplier` | seventh | `Changed`, `Recruited`, `RankedUp`, `ComposureChanged`, `Awakened`, `Lost` |
| `CultureSystem` | The nation's culture (`CultureState`, saved whole): the founding myth told when Horology is researched (`Culture.ink`, three `FoundingMyths`), the nation's names (`CultureIdentity`: Iridia / Citizens: Iridian / Culture: Iridian, asked by `CultureNamingDialog`), leanings over the ten `EnclaveFamily` ways drifting each Seventh toward what it lives (myth, pillars, civics, what is eaten and worked, land, districts), its character's effect, foodways by `FoodClass` (Edible / Ingredient / Spice / Eleos Tea (teas) / Beverage, from `FoodKind.cuisine`; the cellar brews and distils beverages through `RecipeSpec.method`), dishes and drinks the people invent and name (`CultureInvention`, `CultureSystem.Invention.cs`: made the way of the closest known recipe, effects from their ingredients by share; each a runtime resource in `RuntimeUnits` and a pantry kind; the most eligible to become national) and national foods (`culture_national_food`), presence on held land (`CultureField`, the Culture lens), reforms (Digestive Rebirth from investigated ruins), its own history. Rules in `CultureRules`, tables and numbers in `CultureTuning` (`Resources/Culture/Culture`); views `CultureWindow`, `CultureHud` (the capital's NationSlot), `NationBanner` (the world view's corner) | seventh, `GameTechnologySlot.Researched`, `Pantry.Eaten`, civics | `Changed`, `Founded`, `Named`, `NationalFoodAdopted`, `Reformed` |
| `EdictSystem` | The government's Edicts (`EdictState`, saved whole), established once the council holds three seats counting the Head of State: stances (one option in force each: The Roofless, Strangers at the Gates, Hearth and Cradle, The Borders, Who May Weave, Where Power Is Heard; each option leans a pillar, so the laws pull the political compass) with their effects and `EdictLevers` read by `PopGrowthLogic` (arrivals, the roofless, births, caravans, vagrants housed) and `WorldSystem` (The Borders is the Realm's border policy; the Realm panel decrees it through `RequestBorderPolicy`); edicts sealed by the Head of State for a cost into slots (seats minus two), strengthened by the legend answering for their council area, running out then resting; the council's accord (seated legends weigh the laws by their seats' areas, the compass by resonance). Rules in `EdictRules`, content in `EdictCatalog`, numbers in `EdictTuning` (optional `Resources/Government/Edicts`); view `EdictsSection` (built in code inside the Government tab's Display) | seventh, council and compass events | `Changed` |

`AgeProgression`, `WorldSystem`, `LegendProgress`, `CultureSystem` and their views (`AgeBanner`, `WorldView`, built in code with
`CodeUI`) are created by `GenesisLoop` once a scene with the calendar loads; the scene file holds none of them. Age
stories are Ink knots authored `# locked: true` with a one-shot `event_completed:<knot> < 1` condition: the Age unlocks
them one at a time at their moment and waits for each to be told. New condition domains: `age` (the Age number, or 1
while a named Age is current), `age_reached` (an Age id reached or passed), `capability` (`AgeCapabilities`: e.g.
`ornamental-magic`, locked until Age III, independent of a legend's own Ornaments), `act`, `age_progress`,
`crisis_stage`, `ages_survived`, `map_explored` (by feature tag), `legend`, `legend_rank`, `fragments` (`renown` is the older
name), `legends_met`, `ballad`, `ballad_verses`; new consequence: `fragment:<who> <Kind> +N` (who: a legend, `protagonist`,
`co`, `cast`, `council`, or `leader`; `renown:<who> +N` is the older form, paid in Meaning). Story tags `# cast:`,
`# theme:`, `# ballad:`, `# verse:`, `# ballad_title:` give every story its ballad actors (`BalladActorsView`, the faces
beside the event screens). Council seats list the areas they answer for (`areas`, vocabulary in
`Resources/Council/Council Areas`); stories ask for an area (`# cast: area:defense`), never a seat title. The culture
adds the condition domains `culture` (`founded`, `named`, `cohesion`, `sevenths`, `reforms`, `cells`,
`national_foods`, `pending_food`, `traditions`, `traditions_recognized`, `traditions_dormant`, `tradition_choices`,
`tradition:<id>` (0 none, 1 emerging, 2 dormant, 3 lived), `myth:<id>`, or a family's share 0-100 such as `culture:Weaver`) and
`national_food` (a resource), `memory` (`kept` (the default), `causes`, `dedications`, `morale`, `cause:<evidence id>`), the consequence `culture:` (`myth <song|hearth|ruins>`, `embrace`, `decline`,
`leaning <Family> +N`, `presence +N`), and name tokens any story text may use, filled in by the event screens
(`CultureSystem.ExpandText`): `{nation}`, `{citizens}`, `{people}`, `{culture}`, `{myth}`, `{national_food}`,
`{national_label}` (in Ink write `\{nation\}`: its braces are logic). The Edicts add the condition domains `edicts` (`established`, `capacity`, `active`, `accord`, `decrees`), `stance` (`<stance>:<option>`, 1 while it is the law, e.g. `stance:strangers:sealed`) and `edict` (an edict id, 1 while in force). The Fate Stage
proper (super ballads, vault: Fate Stage.md) is reserved and not built.

### The technology tree

Each Age's tree is an `AgeTechnology` section in the research tab: `TechnologyTreeLogic` builds its slots from
`eraTechnologies` (list order is grid order, column by column; empty entries leave a cell free) and initializes them
as it builds them, so nothing flashes before visibility is decided. The rules are pure, in `TechTreeRules`
(`TechTreeTests`): a technology shows when researched, researchable, one step past a researchable one, enlightened, or
waited for by the Age (`AgeProgression.WaitingGate`); everything else is hidden, pointer included (the slot's hit area
and button turn off, so no tooltip, hover or click reaches it). Tooltips name a prerequisite only once it is uncovered.

- **Lines**: `TechTreeConnectors` (a layer under the tree's Slots, ignored by its grid and drawn behind the slots, one
  mesh through `TechTreeLines`) joins every uncovered prerequisite to its technology. Routes are orthogonal and run only
  in the gaps between columns, along rows whose cells between are free, or along the channel between two rows, so no
  line crosses a slot. Each line's branch is toned by its prerequisite, the trunk into the technology (shared by all its
  lines) by the technology itself; a hidden prerequisite is a dashed stub from nowhere. Rebuilt only on change.
- **Research plan**: `GameUnitsLogic.ResearchPlan`, prerequisites first. A click plans the way to a technology
  (`PlanResearch`), Shift+click appends, right click removes it and its planned dependants (`RemoveFromPlan`, via
  `TechnologySlotPointer`); the active research is always the plan's first researchable technology, and researching
  it (by any path: `GameTechnologySlot.UnlockTechnology` calls `OnTechnologyResearched`) moves the plan on. Saved in
  `SaveDocument.researchPlan`, outside the strict system snapshot, so older saves load with an empty plan.
- **Enlightenment** (eurekas): `TechnologyData.enlightenedConditions`, read through `GameValues`; the tree checks them
  twice a second and, once all are met, calls `GameUnitsLogic.EnlightenTechnology`, the one entry point stories use too
  (`technology:X enlightened`, Ink `TriggerTechnologyEnlightened`): uncovered at once, `enlightenedBonusPercent` of every
  cost paid on the spot, announced in the notices. Condition domain `enlightened` counts them (or tests one). An Event
  or Crisis Technology's Enlightenment is a great deed: `AgeProgression` awards `AgeDefinition.enlightenedTechnologyEraScore`
  (1; others earn none), next to the 2 an Event Technology earns when researched (`eventTechnologyEraScore`).

### The world (roadmap 3.1, Docs/Planning/WORLD_GENERATION.md)

Generation is pure (`GameData/World`, no UnityEngine) and runs once per game in about half a second:

| Piece | Owns |
|---|---|
| `HexCoord`, `HexHierarchy` | Axial hexes; the index-7 lattice: micro hexes group into meso cells (the playable cells, `WorldTile`), 49 meso cells into a macro aggregate. World positions are micro units |
| `WorldStencil` | The composition (`Resources/World/Composition.txt`): each same-quadrant block is a slot, slots only an intersection apart form a quadrant instance; slot tags (north/south/east/west, coast/inland, core) |
| `TileTemplate` | Handmade tiles (`Resources/World/Tiles/*.txt`): honeycomb drawings of one Macro Biome with a legend, optional heights and allowed rotations |
| `SlotSolver` | Each Quadrant's Macro Biome catalog into its slots with orientations: seeded, most-constrained-first backtracking, relaxations and catalog errors reported |
| `WorldGenerator` | Domain, warped stencil, slot distances and continuous seam blending, land and sea, fields, stamping tiles, topology repair, priority-flood drainage, lakes, rivers, ground, land fertility, the capital, features (`PlaceFeatures`); every step records into `WorldGenReport` |
| `WorldMagic` | Coherence/Dissonance Seeds, Sacred Sites, the Grand Thread Rings' field, leylines per Age, Convergences/Basins, silver rivers, Coherence and magical fertility; `Apply(age)` commits once, `Forecast(age)` previews |
| `WorldNoise` | Seeded noise and hashes, one stream per purpose |
| `WorldZoom` | The zoom: camera size to the micro/meso/macro reading, steps, zoom about the cursor |
| `MicroNavigation`, `MicroGrid` | The travel grid units walk: micro hexes addressed as cell index x 7 + child with one neighbour pattern; step costs (ground and ragged seams, crags, fords, roads hex by hex, leylines, danger, weather, slope), ground components, an exact A* with pooled buffers; orders to a hex or a cell's heart with the nearest-reachable fallback. Built lazily per map, never saved |
| `WorldGeometry` | The river meander and road lines, shared by the renderer (what is drawn) and the grid (what is forded and followed) |
| `WorldUnits`, `UnitAbilities` | Units: micro movement, needs (rations drawn from the stores or gathered in camp, fatigue, attrition, pace; `ProvisionRules`), sight, surveys per hex; abilities with a reach (hex, around, whole meso hex), duration and toll |
| `WorldTerritory` | Administrative Authority as territory: seats (`SeatKind`: Capital, Major, Trade Nexus, Trade Node, Town, Haven, Grandfield, Outpost; enclaves as rivals) and their pull over governance travel; passive adoption per seat (`Tick`, `Candidates`, `Priority`, the player's `BorderPolicy`); Administrative Capacity vs load, strain, efficiency, drift, and where horizontal gives way to vertical (`Realm` → `RealmReport`). Derived each rebuild (`WorldMap.territory`); adopted cells saved (`WorldMap.Adopted`). Rules: `SettlementRules.territory` (`TerritoryRules`) |
| `WorldBeauty` | How fair or hideous a cell is (-1..1) from its ground, what stands on it, water in view, Sacred Sites, leyline lights, vistas, Coherence, dissonance and danger; read by adoption priority, City Development, land yields and load |
| `WorldSectors` | The nine compass Sectors of each Macro Biome (`CompassSector` on `WorldTile.sector`), assigned once after generation from the centre of the slot's own cells: the Central Sector is about a ninth of it, the rest are 45-degree wedges. None in the intersections and the ocean |
| `WorldEcology`, `CreatureTaxonomy` | The bestiary and its living creatures (vault: Arcanorian Ecology.md, "The Nature of a Species" and "Where Creatures Live"): AECOR's 21 diet-subgroup pairs and the species (`SpeciesSpec`); populations per Macro Biome slot x species in groups (`WorldMap.Populations`, saved), ticked each Echo: habitat (`WorldTile.habitatSlot` range, cached per Age), capacity by size, people's pressure and Pure Light fragility, prey, logistic growth, migration by ranging, new dens (`WorldResources.PlaceDen`). Dens' yields and harvests follow abundance; a den is hunted once a Phase (`Hunt`, `ResourceSite.huntedPhase` against `WorldSystem.PhaseNow`) and can be depleted. Rules: `generation.ecology` |
| `WorldBehavior` | Behavior, the species' memory (vault: Arcanorian Ecology.md, "Behavior: The Memory of a Lineage"): a temper per species and authority (`BehaviorBond`) and an overall one per species (`WorldMap.Behaviors`, saved), moved by hunts, habitat taken and living beside it, spread to the lineage, eased at each Age (scar spectra). It moves the response along AECOR's ladders; read by the ecology's pressure (per presser), hunts (less from species that learned to flee you) and den danger (`WorldResources.Refresh`). Ticks after the ecology each Echo. Rules: `generation.behavior` |
| `WorldRhythm` | The creatures' calendar (vault: Arcanorian Ecology.md, "The Creatures' Calendar"): one `EchoRhythm` per Echo (the Echo just lived weighs births, decay, spreading, predation and behavior's harm and easing; live effects on den yields, hunt harm and Silence's hungry-predator danger peak in the Echo's middle Phase) and the Ritual Seventh's attunement (`Attune`: Pure Light surge or overload by Coherence and silver water; `WorldBehavior.Attune`). Reads the calendar mirrored on `WorldMap.echo`/`echoPhase`/`phaseCount`/`ritualSeventh` (derived). Rules: `generation.rhythm` |

#### World scales (who owns what, and how often it changes)

The world's scales use the names of the owner's earlier design, AECOR (owner decision, Sept 28, 2026). Largest first:

| Scale | In code | Size | Owns | Changes |
|---|---|---|---|---|
| Overall Map | `WorldMap` | ~34,000 meso cells | The Age and its crisis, the leyline network, the Old World, the aftermath between Ages; later Resonance Tides | Per Age (and Echo for Tides) |
| Quadrant | `QuadrantSpec`, `WorldTile.quadrant` (Q1-Q7) | One stencil colour; one or more instances | Regional character: its Macro Biome catalog, mountain backbone, island seas, Coherence Seeds; weather fronts can cover whole Quadrants | Fixed at generation (weather per Seventh) |
| Macro Biome | `MacroBiomeSpec` placed in a slot: `WorldTile.slot`, `WorldTile.macroBiome` | A few hundred cells | The ground recipe today; the ecology (vault: Arcanorian Ecology.md): species populations, habitat and species behavior | Per Echo (ecology) |
| Inter-Biome Definitions | `CoverSpec`, `FeatureSpec`, `GrandfieldSpec`, `ResourceSiteSpec` | A cell to a few dozen | What lies inside a Macro Biome and may cross its Sectors: covers, landmarks, grandfields, resource sites (and their bloom vigor) | Placed per Age; sites grow per Echo (`WorldResources.GrowEcho`) |
| Sector | `WorldTile.sector` (`CompassSector`) | About thirty cells | A name for part of a Macro Biome ("Violet Grove, North-East Sector"); later where knowledge is mapped and dens sit | Fixed at generation |
| Cell (meso hex) | `WorldTile` | One strategy hex | Terrain, settlements, territory, danger, forage, claims, knowledge | Per Seventh |
| Micro hex | `MicroGrid` | A seventh of a cell | Travel, sight, camps, surveys | Per Seventh, walked in real time |

The **intersections** (stencil I, `WorldComposition.Intersection`) are the procedural ground between Macro Biomes. They
carry their neighbours' ground but belong to no Macro Biome, so they have no Sector.

Rules for new systems:

1. **State lives at the coarsest scale that can express it; finer scales read it.** A den's yield reads its Macro
   Biome's population; the population is never stored per cell.
2. **Change follows scale.** Cells and micro hexes tick per Seventh; Macro Biome state per Echo (next to
   `WorldSystem.OnEcho`); the Overall Map per Age. An Echo tick may refresh derived cell fields, but it never
   simulates per cell.
3. **Key Macro Biome state by slot, not by `MacroBiomeSpec.id`.** A catalog that allows repeats places the same Macro
   Biome in several slots, and each placement is its own living place.
4. **Name scales only with these words.** "Region" is not a scale. `WorldComposition` says which part of the
   composition a cell grew from (ocean, intersection, Macro Biome), and a cell's quarter around the capital
   (`WorldTile.quarter`) only spreads landmarks fairly.

The view (`UI/World`): `WorldRenderer` draws the whole world on one quad with the `WorldTerrain` shader, which finds
each pixel's micro hex, meso cell and macro aggregate with the same lattice arithmetic and reads their colours from
small point-sampled textures (knowledge, overlays and selection are textures too), plus two batched meshes
(`WorldMarks` shader) for rivers/leylines and markers. The world sits at z = 20000 with its own camera, out of the
capital camera's range. `WorldView` is the screen: scrolling out of the capital (not over a list) opens it, the three
readings follow the zoom, scrolling in at the closest zoom over the capital closes it, and the capital's canvases fade
while it is open (the event overlay keeps its own group, so stories still show).

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
civics, the Head of State, chorus choice cards); every game-object tooltip is worded in `TooltipContent`, which reads
numbers from the system that owns them (for example build costs from `GameUnitsLogic.GetBuildCost`).

Tooltips have two states:

- **Fluid**: a tooltip follows the cursor and lets clicks through, as tooltips always did. One with nothing to
  explore stays fluid.
- **Solid**: a tooltip with golden keywords or text to expand locks once the pointer holds still for
  `TooltipTheme.lockDelay`; the seal on its bottom edge fills as it does. A solid tooltip stays put, catches
  the pointer and can be entered. Hovering one of its keywords opens that keyword's tooltip, fluid again. It
  closes when the pointer leaves the keyword, unless the pointer holds still until it turns solid too, and so
  on down the chain. Clicking "+N more" or "More..." in a solid tooltip expands it; a long read turns pages
  (Previous / Next, `TooltipTheme.detailsPageChars` per page).

Long lists (more than `collapseAfter` bullets) show their first lines and a "+N more", and long `details` wait behind
"More...". A tooltip is never a wall of text until the player asks for one.

| Piece | Owns |
|---|---|
| `TooltipSystemLogic` | The chain of open tooltips: follow, stillness and locking, keyword hover, expand clicks, a card's "Open in the White-Haven Library" (`ui:library:<id>`), grace periods, refresh. |
| `TooltipView` | One box, built in code: shadow, the Storage Tab texture tiled at its own pixel size (90% opaque, `TooltipTheme.backgroundAlpha`), a weathered stone frame with an inset copper line, title, type line, then a block per `TooltipData` section (flavour, summary, requirements, modifiers, breakdown, effects, prerequisites, notes, details, related), and the lock seal. A stone-and-copper rule appears only under the title and between story and numbers; everything else is set apart by spacing (`TooltipText.RowSpacing`, `TooltipTheme.spacing`). |
| `TooltipTheme` (`Resources/UI/TooltipTheme`) | Every visual choice and timing: font, sprites, colours, sizes, lock delay, stillness tolerance, collapse length. Tune the look and feel here. The sprites in `Visual/UI/Tooltip` are generated by `Visual/UI/Tooltip/Source~/make_tooltip_sprites.ps1`. |
| `TooltipText` | The palette and rich-text building blocks: `Heading`, `Row` (label left, value right), `Bullet` (nested), `Numbered`, `Quote`, `Signed`, `Consequences`, `Symbol`, `Collapse`, `Pages`/`Paged`, `LibraryLink`. Write tooltip text with these, never raw `<color=green>`. Keywords are golden, minor details soft purple. |
| `KeywordMarkup` | Pure text rules: `[[Term]]`, `[[Term\|shown]]` and `[[Note#Section]]` links (the vault's syntax), automatic highlighting, the vault's Markdown (nested and numbered lists, quotes, table rows; embeds, tags and comments dropped), and `SafeGlyphs` (below). |

The game font (Chantelli Antiqua) draws ASCII, the Latin-1 letters and curly quotes; its dashes, `…`, `•`, `·`, `×`, `≥`, `≤`
are blank and most other characters are missing. `TooltipText.Symbol` draws such characters with TextMesh Pro's bundled
LiberationSans, and `KeywordMarkup.SafeGlyphs` (run by `Keywords.Linkify` and on every title) does it for any text:
`…` becomes `...`, a non-breaking hyphen `-`, emoji are dropped, the rest is drawn in the symbol font.

### Species knowledge and the Bestiary (vault: Arcanorian Ecology.md, "Knowledge of Creatures")

The owner's rule: the UI shows only what the civilization knows. Knowledge of creatures is **derived, never saved**:

| Piece | Owns |
|---|---|
| `SpeciesKnowledge` (`GameData/World`, pure) | `SpeciesLevel` per species, read from `WorldMap.ResourceSites` and `ResourceSiteSpec.species`: Unknown, Sighted (a den `WorldResources.Sighted`), Identified (a den `WorldResources.Identified`). The identified species with their identified dens, unidentified Fauna sightings one by one, identified-site counts, den places ("Violet Grove, North-East Sector"), and the discovery goal check. Observed, Understood and Mastered: see below |
| `BestiaryHud` | The capital's Bestiary button (built in code under Ballads & Bonds, added by `GenesisLoop`): shown once a researched technology carries the Special unlockable `Bestiary` (read from the technologies, so loads need nothing), hidden during stories and on the world map |
| `BestiaryWindow` | The Bestiary: each identified species' card (subgroup, summary, how it lives, Auric Structure / Pure Light, where its dens were found), then "Unidentified fauna" sightings |

Spoiler rules: a species no one identified is never named, listed or counted anywhere. Sighted dens are listed one by
one (grouping them would say which are alike), and only Fauna (an unidentified bloom listed in a bestiary would say it
is a creature). A one-species Enlightenment goal hides its progress (`SpeciesKnowledge.HidesProgress`; "(1/2)" would say
which unidentified den is that species).

`GameValues`: `species_known` (identified species; with a species id, its level 0-2) and `sites_identified` (identified
sites, planted ones aside; with a kind word or a site id, only those). They are **Enlightenment rewards only**: the Q5
draw can leave a Macro Biome out of a world, so `ContentValidator.ValidateDiscoveries` refuses them as civic
requirements, checks that a goal names a real species or site and a level of 1-2, and wants exactly one technology to
unlock the Bestiary.

**Second half (the memory; `SpeciesLoreTests`).** What the map shows stays derived, but a civilization *remembers*:

| Piece | Owns |
|---|---|
| `SpeciesLore` (`GameData/World`, pure) | `SpeciesLoreState` (one `SpeciesRecord` per identified species: identified, Echoes watched, observed, places found). `Refresh` records every identified den (a faded Trapper bloom or a lost den no longer takes its species' knowledge with it) and marks Observed; `Watch` (each Echo) counts a watch for every identified species with an identified den within `watchReach` of land your people hold (outposts included). Observations = watches + your hunts (E3's `BehaviorBond.hunts`, read, never written) + each further place found. `PopulationPercent` and `BehaviorPercent` for GameValues |
| `SpeciesLoreKeeper` (added by `GenesisLoop`, saved as `_state`, a later system) | Refreshes on `WorldSystem.Changed`, watches on the Echo, and posts one Discovery notice when a species becomes Observed. `View` hands surfaces a `LoreView` (state, tuning, and `Understands`: a researched technology carries the Special unlockable `Creature Studies`) |
| `SpeciesLoreSettings` (`Resources/World/SpeciesLore`) | `observeAt` 3, `watchReach` 2, `perHunt` 1, `perPlace` 1 |

Levels now run Unknown 0, Sighted 1, Identified 2, **Observed 3** (enough observations), **Understood 4** (Observed and
Creature Studies researched, an Auric Enclave held as suzerain, or the species studied by an Auric Enclave), **Mastered 5**
(identified, and kept by a Domestication Enclave you hold as suzerain: derived from the map, E7). `SpeciesKnowledge.LevelOf/Levels/Identified/
IdentifiedCount/Value` take an optional `LoreView`: without one they read the map alone, as before. What the Bestiary
card adds per level: Observed, its behavior toward your people (with "warier than its nature" or "gentler"), your
hunts, and its numbers in words (thriving/steady/scarce, growing/declining) in each Macro Biome where an identified den
stands; Understood, its numbers (groups of what the land holds), what newcomers meet (overall behavior), what it hunts
and what hunts it (identified species by name; the rest as "creatures not yet identified", never counted). Below
Observed the card shows its observations (n/3) and what teaches more; Mastered adds its keepers. Rumored waits for
its reader (expedition stories).

More `GameValues`: `species_population:<id>` (groups over capacity across its Macro Biomes, 0-100) and
`species_behavior:<id>` (temper toward you, -100 to +100, 0 before meeting). Both read the world as it is, need a
species id, are Enlightenment rewards only (`IsDiscoveryDomain`), and hide their progress; `species_known` accepts
levels 1-5. The validator wants exactly one technology to carry Creature Studies.

### Enclaves and the creatures (vault: Arcanorian Ecology.md, "Enclaves and Creatures")

A thin layer over standing and Suzerainty (the owner's choice): the full Enclave system later changes what triggers
these effects, not the effects.

| Piece | Owns |
|---|---|
| `WorldEnclaveEcology` (`GameData/World`, pure; `EnclaveEcologyTests`) | What each family does with the creatures. Domestication keeps the species within `keepReach` that `EnclaveSpec.keepsPureLight` allows and befriends them each Echo (and you, while suzerain); `KeptForYou` makes a known species Mastered and scales your hunts' harm (`WorldBehavior.Hunted`); `Yields` are the herds. Agromagical tends the land around it. `AuricUnderstanding` for the lore keeper. `WhyNotCommission`/`Commission`: cull, tame, restore, study |
| `WorldSystem.Enclaves.cs` | The Echo call (after `WorldBehavior.Tick`), the kept/released notices (known species only), `KeptBy` for the card, and the commission API the den card calls (`Commissions`, `WhyNotCommission`, `Commission`) |
| `generation.enclaveEcology` (`EnclaveEcologySettings`) | Every number (proposals: Canon Gaps.md, "Enclave ecology") |

Saved: `Enclave.kept` and `Enclave.commissionedPhase` (optional fields on the saved enclaves), `SpeciesRecord.studied`.
Derived: what an Enclave keeps and Mastered. Enclaves are authorities to the creatures (`enclave:N`); never write a
species' temper directly, go through `WorldBehavior.Befriend`/`Hunted`. `EnclaveService` is append-only (Trading and
Industrious services arrive with E9 and the technology templates).

### The full creature schema (vault: Arcanorian Ecology.md, "The Nature of a Species")

Every field of `SpeciesSpec` has a reader in a system that already runs ("no field without a reader"):

| Field | Read by |
|---|---|
| `structure` | `CreatureTaxonomy.IsPureLightBeing` (canon: under 65% Structure, or a CBT organ): Ritual Seventh attunement (`WorldRhythm.Attune`), plague hosts, the validator. Vibrational Fallout wears a cell's quality down by fallout × Pure Light × `ecology.falloutHarm` (`WorldEcology.Quality`) |
| `binding` (`BindingOrgan`) | where its Coherence-Binding Tissue lies: the Pure Light being rule, the cards |
| `niche` (`HarmonicNiche`) | `WorldEcology.Attunement` (Coherence, or Dissonance for Discordant lineages, which Fallout spares) and `OnLeyline` (Leyline lineages keep `offLeyline` of a cell away from leylines and silver water); `Quality` and `Attune` share them |
| `breedingEcho` | `WorldRhythm.Births` (every birth multiplier goes through it: the breeding Echo, else the diet's rhythm) |
| `commensal` | `Quality`: people's pressure feeds it instead of pressing it out, up to `commensalCap` |
| `vector` | `WorldEcology.VectorPressure`/`VectorSources` → `PopulationHealth.GatherInputs` (E6's `vectorPressure`): average infestation near your settlements |
| `discovery` | `SpeciesLore.Rewards` (pure: what each level of knowledge has earned and not been paid; `SpeciesRecord.rewarded`, optional saved field) → `SpeciesLoreKeeper.Pay` (Era Score through `AgeProgression.Award`, resources through `ChangeResourceFromName`), on world changes, Echoes and `GameTechnologySlot.Researched`; only while an Age runs, so nothing is lost before |

**Resonance plagues** (`WorldPlagues`, pure; `generation.plagues` of `ResonancePlagueSpec`; saved `WorldMap.Outbreaks` =
`WorldSystem._outbreaks`, optional, in `GameSnapshot.Schema`). Ticked in `WorldSystem.OnEcho` right after
`WorldEcology.Tick`: running outbreaks kill their hosts in their Macro Biome, spread to neighbouring ones and pass into
a period of immunity; new ones break out with chance × the torn share of the hosts' range (`Torn`: cells with
Dissonance + Static Criticality at `dissonanceAt`) × `EchoRhythm.plagues`. Rolls hash the world's seed, the plague, the
slot and the Echo. Creatures only: sickness reaching people is Disease Burden through `vector`. A plague is named by its
`folklore` until one of its hosts is Understood (`WorldPlagues.Name`); `WorldSystem.PlagueLine` and the den card follow
that, and name only identified species.

Rules for later stages: new creature behaviour goes through these fields and readers, not new per-cell ticks (Macro
Biome scale, "World scales"). A new vector needs only `vector > 0` and a den. Validation: `ContentValidator.ValidateCreatureSchema`.
Tests: `CreatureSchemaTests`.

### The aftermath and the Great Plague's moths (vault: Arcanorian Ecology.md, "The Aftermath of Creatures" and "The Great Plague's Moths")

| Piece | Owns |
|---|---|
| `WorldAftermath` (`GameData/World`, pure; `AftermathTests`) | `Pass`: once per Age, the crash by the ended crisis' severity and Pure Light (`Survival`), ranges shifting to neighbouring Macro Biomes with habitat, new lineages (`aftermath.lineages`, `Candidates`: Dead-zone ones on Fallout first) settling emptied Macro Biomes, and scar spectra through `WorldBehavior.AgePassed(map, settings, keptOf)` |
| `WorldGreatPlague` (`GameData/World`, pure; `GreatPlagueTests`) | `Echo`: trade routes carry the vector between the Macro Biomes they pass, `EnclaveSpec.trades` Enclaves bring it to your settlements, and it breeds on Disease Burden while the plague runs. `Revealed` (crisis stage, Age passed, or Understood). `WhyNotRestrict`/`Restrict` (the farms closed until `Enclave.restrictedUntil`) |
| `WorldSystem.Aftermath.cs` | `AftermathLines` (from `OnAgeBegan`, after the new Age's sites, severity from `AgeProgression.History`), `PlagueLines` (from `OnEcho`, after the Enclaves), the one-time reveal notice (`_plagueRevealTold`, saved, also told on `AgeProgression.Changed`), `KnownVector` for the den card and the Bestiary, and the Enclave card's `TradedBy`/`Restrict` |
| `generation.aftermath`, `generation.greatPlague` | Every number (proposals: Canon Gaps.md, "Aftermath ecology" and "The Great Plague's moths") |

The moths feed Disease Burden only through `SpeciesSpec.vector` (E10's `VectorPressure`); nothing here touches the
crisis' severity. Before the reveal nothing may name the moths as a cause. Validation: `ValidateAftermath`,
`ValidateGreatPlague`.

### The Symphony of War's macro layer (vault: Combat System.md, The Principles of Magic.md)

Auto-resolved battles, the outer layer the manual (micro) battles will sit inside. Pure logic, tested in `CombatTests`,
`ConscriptionTests`, `LegendGreatsTests` and `WorldBattleTests`; on the map through `WorldSystem.Battles.cs`. There is
no army UI yet. Every number is a proposal (roadmap D10).

**On the map** (`WorldBattles`, `WorldSystem.Battles.cs`). `WorldSystem.Army` (saved as `_army`) holds the roster;
`Conscript(spec)` raises a company, `MarchArmy(stack)` puts a stack on the map as an `army` unit (`WorldUnit.armyStack`).
Every one of your units fights and can be attacked: armies with their companies (military: far more Integrity and
Composure), every other party as itself (`WorldBattles.PartySide`: the role's or spec's `battleIntegrity`... plus each
legend's share, the Director commanding, companions leading their own sections, settlers behind; wounds carry over as
attrition, lost nerve as `nerveLost`, recovered by rest). A beaten party scatters: its legends go missing in action,
surviving settlers walk home.

**Enemies** are `creature-band` units (`faction = wild`): **red** (`EnemyStance.Hostile`) hunt the nearest of your units
they see; **orange** (`Wary`) turn red for `ProvokedSevenths` when one of your units stands adjacent or passes within
`PassingReach` (`WorldBattles.Provokes`). The map shows their colour through `WorldSystem.SpecOf`. Threats send them out
(`ThreatSpec`: `stance`, a bestiary `species` or their own beings `beingName`/`beingSize`/`beingStructure`/...,
`beingBindings`, band size, `bands` out at once, `respawnSevenths`, `roam`), once a site's cell is revealed
(`TickThreats`, timers saved as `_threatTimers`): the Fallout Scar's orange Dead-zone Sprites, the Atonalis Nest's red
Nascent Atonalis. Every Atonalis band draws a binding (`WorldBattles.DrawBinding`: its list, or any of the seven) as its
primary: its pseudo-spellweaving is rooted in it and it is its weakness; slain Atonalis leave a Rose Seed (named in the
notice only). The threats' danger aura is unchanged. Known dens send a visiting band out too (`TickDens`, one per den,
`SpeciesSpec.denVisitSevenths`, at most `DenVisitorCap` out): it stays out that long (`WorldUnit.denLife`), then walks
home and is gone. Its stance is its species' memory of you (`WorldBehavior.Toward`): **muted orange** (`Timid`) flee you.

**Encounters** (`WorldPursuit`, pure, tested in `WorldPursuitTests`; `WorldSystem.Encounters.cs`, play-tested in
`EncounterPlayTests`). A band knows what it is (`BandIdentity`: creature, Atonalis, humans, demihumans, humanoids, each
with its own mark in `WorldMarks.shader`, shapes 8-12) and how it thinks (`BandIntelligence`, the species' setting raised
by its nature: the Smart subgroup is Smart, omnivores and pack hunters Regular, beasts Instinctive). Every
`DecisionSevenths` it flees what it fears (a hungry predator of its kind; your parties if timid, or if Smart and weaker
by `Power`), heads home after a chase, chases its quarry (your parties while angry, its `prey` species while not `sated`)
until its leash (`WorldPursuit.Leash`: `pursuitLimit` hexes, its territory if it defends one, a Smart band your
territory, a Regular one a settlement), searches where it last saw it, else roams its `territoryRadius`. Regular and
Smart bands will not cross a river or ford after you (`SafeStep`; the far bank ends the chase), except when fleeing;
Smart ones keep off steep climbs and deeper danger, and stalk two hexes off rather than attack onto ground that favours
you (`Field`: river, height, cover, settlement). **Endurance** (0-100, every unit): running (a band pursuing or fleeing,
a party told to run or giving chase via `EngageBand`) goes at `sprintPace` and spends `RunDrain x wind` per travel
fatigue; below `FullPaceEndurance` it slows, at 0 it is winded and cannot move until `ResumeEndurance`. People tire
slowest (persistence hunters), so a timid band chased long enough is run down; a spent side fights with less Composure
and defense (`Tire`), and creatures a party takes are carried home as the den's harvest (`HuntSpoils`). Predators that
catch their prey take some of it and rest (`Predation`). Bands show only while one of your parties sees them or they
walk your territory (`Sees`); they take no orders (every order API answers `WorldSystem.NotYours`), and right-clicking
one with a party selected gives chase.

**Habitats.** `SpeciesSpec.habitat` (Land, Water, Amphibious; a finned Pure Light being is at least Water) is copied to the
band. `WorldPursuit.StepCost` is the one step cost for every entity: what walks pays the land's cost and cannot enter
water cells (a river is forded); what swims moves only on water (lake and sea cells, river hexes) and never leaves it,
so the shore ends its chase; an amphibian takes either. `Route`, `Escape`, `Roam`, spawns (`FreeHexNear` by habitat)
and `WorldUnits.Move` (its `stepCost`) all go through it. river-trout is Water, bog-eel Amphibious (World.asset).
**Fleeing** is one bounded search (`Escape`): the reachable hex within five that puts the most ground between it and
the danger for the least effort, not far from home, never through perilous ground (`Peril`: a ford for what walks,
hazard or fresh signs, Fallout, Dissonance, hard cover, foul weather, a steep climb) unless every way out is perilous;
a Smart band swims a river on purpose and values high ground.

**The Eight-Born Paths** (`AtonalPaths`, vault: Eight-Born Paths.md, Atonalis.md). A threat's band draws its Path by the
vault's shares (or `ThreatSpec.beingPaths`) and takes its traits: cleverness by the vault's tiers (Carnalix
Instinctive; Discant, Obsessian, Anxithor, Signath Regular; Violux, Erosyx, Animach Smart), leash and memory, and its
behavior: Anxithor guards its ground; Discant's despair bites deepest into Composure; Obsessian walks a loop; Signath's
steps go astray; Carnalix preys on any creature, drains a party (wounds, strain, it grows) and is never sated; Animach
waits for good ground; Violux pursues 40 hexes and keeps a `grudge`; Erosyx comes only for a party of one. Every Path
but Carnalix **takes legends captive** when they break or are left behind and the band still stands
(`LegendProgress.TakeCaptive`: off the roster, fed upon each Seventh, `WorldUnit.captives`); destroying the band frees
them (`FreeCaptives`, also on any removal, and a sweep in `TickEcosystem`).

**The Emotional Register and emotional alchemy** (`EmotionalRegister`, `EmotionalProfile`, `EmotionalAlchemy`,
`EmotionalEvolution`, `WorldSuffering`; tested in `EmotionalRegisterTests`, `EmotionalAlchemyTests`). Emotional Residue is
made of sixteen notes on eight axes, one axis per Eight-Born Path, each with a **consonant** (healthy) and a **dissonant**
(wounded) face: Courage/Dread (Anxithor), Joy/Tumult (Discant), Devotion/Fixation (Obsessian), Wonder/Doubt (Signath),
Vitality/Pain (Carnalix), Belonging/Estrangement (Animach), Pride/Shame (Violux), Love/Longing (Erosyx). `Feeling` keeps
the wounds at 0-7 and each healthy face at its pair + 8 (`Pair`, `Axis`, `Harmony`).
- **Cocktails.** Every eater of feeling eats a recipe of notes in proportion. A strict eater gets `Servings` (its scarcest
  note over what the recipe asks), a loose one `Loose` (any of its notes); `Fit` is how exactly a place's mix matches.
  Named **compound feelings** (`Compounds`: Anemoia, Nostalgia, Saudade, Grief, Catharsis, Awe, Desire, Serenity,
  Kenopsia...) are recipes too; `Detect` reads which a register holds (tile report, hover: "The air holds Anemoia").
- **Transmutation.** A note turns into its pair on its axis: Eleos healers breathe `HealerTransmutes` of the wounds they
  drink back out as the healthy face ("nothing is wasted"); an Atonalis feeding (`Prey`) turns the healthy notes it preys
  on into their wound. Consonance soothes: the land's suffering is its wounds less half its health (`SufferingOf`).
- **Sources.** Imprint (saved `_suffering`): battles Dread, Pain per fallen and Courage; hunts Pain and the hunters'
  Vitality; scattered parties Tumult and Estrangement; fallen settlements both; grievances Shame; conscription Longing;
  festivals Joy and Love; kept holidays Devotion. People (`Settlement.feelings`): wounds from strain and its cause, hunger,
  sickness, despair; health from happiness (Joy, Belonging, Love), being fed (Vitality), development (Pride), holidays
  (Devotion), standing their ground (Courage); the Indulgent District's desire. Living land (`WorldResources.Residue`):
  Wonder where Coherence runs high or leylines pass, Devotion on sacred ground; the torn Loom's Doubt.
- **Blooms eat cocktails and evolve.** `ResourceSiteSpec.flavors` is a bloom's cocktail and `specificity` its niche (0
  Generalist .. 1 Purist; World.asset, 15 blooms, e.g. Vow Orchids Devotion/Love/Pride at 0.8, Glimmerfern
  Doubt/Tumult/Wonder at 0.2; the Anemoia Lunaria a near-purist healer of the Anemoia compound whose seed pods,
  `Anemoia Pods`, are an Ornaments luxury and amenity in `CultureLifeTuning`; Lust Berries eat the Lust compound). `EmotionalEvolution.Feed`: food = specificity x servings + (1 - specificity) x loose;
  growth = food/need x (1 - nicheGrowthCost x specificity); potency = superloaded on its exact cocktail. Vigor is the
  palate's growth (never below 3/5 of the residue alone); `Gift` scales by potency; healers drink harder. Each Echo
  `WorldSystem.EvolveBlooms` passes a generation for every lineage (`ResourceSite.lineage`, `Lineage`/`Palate`,
  `EvolutionSettings` in `EleosSettings.evolution`): it tries to specialize (sharpen to its place's proportions, pickier)
  or generalize (take in its place's rich notes, looser) and keeps the fitter; far from its ancestors it becomes a named
  variety ("Memory Marigolds of Anemoia"), back near them it reverts. API for other systems: `IEvolvingLineage`,
  `EmotionalEvolution.Seed/Feed/Evolve/Specialize/Generalize`, `WorldSystem.Lineages`, event `LineageEvolved`.
- **Atonalis eat cocktails but do not evolve.** Each Path's hunger (`EmotionalProfile.CocktailOf`) is mostly its wound,
  often a healthy note it preys on (Discant joy, Erosyx love and the body's pleasure), and some of its neighbours'. Roaming
  Atonalis follow the scent of their hunger (`Scented`: the pleasure parasites to revels, a Carnalix to slaughter
  grounds) and feed where they stand (`FeedAtonalis`).
- **Formless Masses** pool where suffering plus the Loom's Doubt passes `SpawnPressure` (your land and real suffering
  first; Loom-only masses within `MassCap`), drink every note into their meter, and hatch into the Path whose hunger
  their feeding resembles (`Match`); a second Path resonating past `HybridFloor` and `HybridShare` makes a **hybrid**,
  the common birth on mixed land, while feeding on one axis makes a purist. Joy makes a Discant; tears and despair a
  Carnalix-Discant. The **Feelings lens** paints each cell in its dominant note's colour; cards show the paired meter.

**Signs instead of the aura.** Threats no longer stamp danger around their sites (`WorldSites.RecomputeDanger` keeps only
predatory blooms and sacred calm). Every hunting band leaves fresh signs on its cell (`WorldTile.signs`, fading); an
explorer who comes within sight of fresh signs of a hunter it cannot see is warned that something hunts nearby, with a
clue by kind (tracks, a carcass, static and drained husks, a slime trail). Signs raise a party's danger (mishaps,
ambushes) and are what the Danger lens shows, over the land's suffering.

**Contact.** Units of different sides never share a micro hex (`WorldBattles.Held` in `WorldUnits.Move`: the mover halts
beside the other), so after units move each tick every hostile pair on adjacent hexes fights once
(`WorldBattles.Clashes`; the mover attacks, else the angry enemy, else the lower id). The field is read from where they stand (`WorldBattles.Field`): the defender's hex gives the
ground (a rim hex may carry its neighbour's), cover and a settlement (on its cell's centre hex); a ford on either hex
(no road on the defender's) is a river crossing; the height between the two hexes favours whoever stands higher
(`height` for the defender, `downhill` for the attacker; a ridge or escarpment's lip adds `VantageHeight`); the
attacker's sections fight by their own hex's footing (`attackerGround`). Afterwards: the roster records the battle, your
legends' fates reach their souls, captives raise `CaptiveTaken`, a beaten army falls back toward your settlements, a
band keeps its survivors (or leaves the map), and both sides keep a one-Seventh truce.

| Piece | Owns |
|---|---|
| `HarmonicCircle` | `SpellBinding` (Unattuned + the seven, saved by index), the canon circle Flux ▶ Cindergale ▶ Crystal ▶ Resonance ▶ Strand ▶ Flux and the Luminance/Void Dance, `Multiplier` against a defender's **primary** binding (its weakness), `BestAgainst`, `Why` (the vault's reason, for reports), the owner's seven colors |
| `AgeMagic` | Ages.md's lines as data: tempos per Age, the command of each chord tier (Unreliable ... Mastery), Discordant Interference chances. Binds civilizations' Spellweavers only; creatures cast by instinct |
| `CombatSpecs.cs` | `CombatSectionSpec` (HOI4 battalion: Integrity, Composure, attack/defense/breakthrough, armor/piercing, width, potency, ward, support abilities, per-ground modifiers; conscription: people, cost, category, quote), `FormationTemplate`, `GroundSpec`, `TerrainElement`, `CombatTuning`, `CombatSettings` (`World.asset` `combat`; empty lists fall back to `CombatDefaults`) |
| `CombatModel.cs` | `CombatSection` (two bars; Mind Break; captured; head count; leader; company bonds), `BattleLegend` (a legend in battle: leitmotif, Ornaments, scores, stars in the Greats, real strain), `BattleSide` (commander, `takesCaptives`), `Battlefield.From(tile, from, ...)`, `BattleReport` (per-measure `timeline` of `SideBars`, `captives`, `legends` fates) |
| `OrchestralFormations` | `Validate`/`Raise` a template for an Age (sections before their Age left out, chords cut to what the Age plays, tempo falls back to Staccato; every cut reported) |
| `CreatureCombat` | A species as sections: size and stance give steel, threat response gives nerve and when wild groups leave, Structure gives toughness, Pure Light gives magic and spell fragility, the CBT organ shapes it, `primaryBinding` its element, the group's head count (for captives) |
| `BattleResolver` | `Resolve` (measures: spells, steel, parries, toll, states, captures), `Forecast` (Total War's balance of power: many seeds), `Begin` → `BattleRun` (the same battle measure by measure for the micro layer: `BeginMeasure`, `Hand`, `Intent`, `Play(side, card, target, rendition)`, `ResolveMeasure`, `Finish`, `Prediction`) |
| `BattleResolver.Symphony.cs` | The card phase: hands and Beats, the performer's valuation (`Value`), `Execute` (effects through the same steel and `Cast` paths), ensemble chord layering, field conditions, marks (guard, ward, expose, blind, burn) |
| `Symphony/` | `CombatCard` + `CardEffect` + `DeckCard` + `ExpeditionKit` + `SymphonyTuning`/`SymphonySettings` (`CombatSettings.symphony`), `SymphonyCards` (default library, creature instincts from species), `SymphonyDecks` (a side's deck) + `LegendGrimoires` (personal grimoires), `SymphonyPower` (`Rate`, `Breakdown`, `Odds`) |
| `BattleVerdicts.cs` | The seven verdicts (`Tier`, `Mirror`, `Legendary`, words, meanings, colours), `BattlePreview` (strengths, Civ VI-style modifiers, balance, advisors' prediction, casualties) |
| `Conscription.cs` | `ArmyRoster` (companies raised from the population, stacks, posts, `Muster` a stack into a side, `Record` a battle back, attachment, naming, promotion ledger), `ConscriptUnit`, `ArmyStack`, `CompanyBond`, `IConscriptionBank` + `LiveConscriptionBank` (population via `PopGrowthLogic.Enlist/Discharge`, resource slots, technologies, fragments, Era Score, shared battles, the naming notice) |

**Two bars.** Integrity is the body: steel wears it (HOI4: parried attacks hit 0.12, the rest 0.36; armor halves what
does not pierce it), spells less. At 0 a section is cut down. Composure is the mind and the magical reserve: spells
and dread wear it, steel a little, every spell is paid from it. At 0 a section suffers a **Mind Break**: it fights on
with its attacks, parries, armor and wards cut (`mindBreakAttack`...) and cannot cast, until it steadies (`steadyAt`).
Each Mind Break and each section lost shakes the rest of its line (`mindBreakShock`, `fallenShock`); the side is
**beaten** when its line's Integrity falls to `integrityBreak`, then run down by faster pursuers. A battle that runs
`maxMeasures` is a stalemate: the defender holds (and so wins it: `BattleReport.Held`).

**Symphonies (the card layer).** Every side can carry a deck (`BattleSide.deck`, built by `SymphonyDecks`): section
decks (Grave Warden, Wasteland Archer...), an expedition's kit (`WorldUnit.kit`, changed in a settlement), each legend's
personal grimoire (its leitmotif as its binding's Principle, its Ornaments from Age III, learned Symphony Cards saved on
its `LegendProgress` record), the commander's orders (one per Great), an army's war score (`WorldSystem._warScore`), a
band's instincts (`SymphonyCards.Creature`). With a deck, a side's drilled fighting runs at `ostinato` (0.7) and its
cards carry the rest: each measure deals Beats and a hand, every card scales with the section that voices it (and by
`cardScale`, so a voice's share of the Beats plays about `melody` of its output), spell cards go through `Cast`, and
cards at home on the field (grounds, conditions) land harder or are only playable there. A side with no deck is the
plain auto-resolve, unchanged. Tested in `SymphonyTests`; the sweep in the design doc calibrates a par deck at about
+10-15% raw strength.

**Verdicts and the battle screen.** Seven verdicts per side (`BattleOutcome`: Decisive, Close, Pyrrhic Victory;
Close, Valiant, Crushing Defeat; Legendary Victory), read from both sides' losses and mirrored between them; the
Legendary only when a `manual` side wins by hand what `BattleRun.Prediction` gave at most 30%. `BattlePreview` is the
pre-battle screen (strengths with Civ VI's modifier list, Lanchester balance, forecast prediction and casualties);
`WorldSystem.PreviewBattle`/`QuickPreview` build it for two map units, every map battle leaves a `BattleRecord`
(`BattleRecorded`), and `BattleWindow` shows either. Tested in `BattleVerdictTests`.

**Captives.** A Mind Broken section below `captureBelow` (40%) Integrity is subdued and taken alive by a side that
takes captives (wild creatures do not), in the battle or in the rout: `BattleReport.captives` (species, individuals) is
how animals come home. The world does not consume captives yet.

**Legends in battle.** A stack's commander (the Battle Conductor) lends its stars in the Greats to the whole stack
(`greatPerStar`: Vanguard attack, Architect parry, Concertist potency, Justiciar dread, Sovereign nerve and carrier
wave, Seer precision and scouting, Chronicler wounded saved and rally) and plays its own chord; a company's leader
lends them to its company (`leaderPerStar`). The commander has a **battle Composure** of its own
(`legendComposure` × its real state's `legendComposureByState`), separate from its real Composure: the stack's fear,
its lost sections and its chord wear it; its share scales the stack (`conductorNerve`); at 0 it suffers a Mind Break
and the stack fights on leaderless. `BattleReport.legends` (`LegendBattleFate`) is what reaches the real soul through
`LegendProgress.ApplyBattleFate`: strain for defeats, Mind Breaks and the weight of the battle; Defiance for victories,
Acceptance for defeats survived; conditions (Traumatized, Haunted). A legend on a doomed side, or whose company was cut
down or taken, retreats alone and goes **missing in action** (`LegendProgress.GoMissing`: off the council, out of
expeditions and stories) and turns up at the Capital `missingSevenths` later carrying its strain. Legends are never
captured yet.

**Conscription.** `CombatSectionSpec.conscripted` units (Grave Warden: front, 1 Attack 2 Defense, 2 people, 125
Elderwood; Wasteland Archer: back, 2 Attack 1 Defense, 1 person, 50 Duskstone; both The Rekindling, Golden Hymn Citadel)
are raised as nameless companies ("1st Grave Wardens"). A legend holds one post at a time (commands a stack or leads a
company). `Record` heals wounds back into Integrity, reports the fallen, strikes companies cut down or taken, takes posts
from legends gone missing, and adds merit; `PromotionCandidates`/`Promote` are the open end for raising a soldier as a
legend (the legend-earning system is not built).

**Attachment.** Each battle a legend fights with a company (as its commander or its leader) counts toward a
`CompanyBond`: 1★/2★/3★ at `attachmentBattles` (3, 7, 21). A new star pays Defiance and Meaning; the first star asks the
player to name the company (`ArmyRoster.Name`) and, the first time in an Age, pays `attachmentEraScore`. With an
attached legend on the field the company fights harder (`attachmentPerStar`), and in a rout it is covered (less
run-down, never cut down or taken while fleeing); what is lost or left behind weighs more (`attachmentGrief`,
`attachmentLoss` → `LegendBattleFate.grief`). Legends in one stack share each battle (`LegendProgress.ShareExperience`).

**Bindings.** A section's primary binding is its weakness and the root it casts in first; troops who never had a Motif
Awakening, and ordinary animals, are Unattuned (no weakness, no resistance). Every Pure Light species carries a primary
(`SpeciesSpec.primaryBinding`; `ContentValidator` checks it, at most two `secondaryBindings`, none in content yet).

### The Greats as earned standings (`LegendGreats`, `LegendProgress.Service.cs`)

Legends are no longer born Great Vanguards: every legend starts an **Unattuned Legend**, and its Lyrical Fragments of
each kind are its affinity toward the Great tied to that kind's binding (Defiance → Vanguard, Meaning → Sovereign,
Vision → Architect, Catharsis → Concertist, Lucidity → Seer, Acceptance → Justiciar, Rebirth → Chronicler). Affinity
becomes 1-3 stars at `FragmentTuning.greatStars` (15, 40, 90), so a legend can be a 3★ Great Sovereign, 2★ Great
Vanguard and 1★ Great Architect at once. `LegendData.legendClass` stays as the legend's calling (its Soul Leitmotif's
lean). Council seats ask for stars in one of their Greats (`CouncilSeatData.requiredStars`,
`CivicCouncilPosition.requiredStars`; 0 takes any legend): High Arbiter, Oracle and Grand Archivist 0, Master of Craft and
Treasurer 1, Supreme Commander and Civil Regent 2. Every Act of Fate, each seated legend earns `seatService` fragments
toward its seat's main Great. Conditions (`LegendConditions`: Traumatized, Haunted, and any a system registers) are
saved on the legend's record and count down each Seventh; nothing reads their effects yet.

### Population health (vault: Arcanorian Ecology.md, "Creatures and the Health of People")

AECOR's conditions sorted by cause, at the scale of a civilization: five pressures, each 0-1 and **dormant** (hidden,
harmless) until their causes wake them. No survival meter and no second population model:

| Piece | Owns |
|---|---|
| `HealthRules` (`GameData/Population`, pure; `HealthRulesTests`) | Named causes per pressure, crisis stage loads, cascades (`Susceptibility`), Scar Spectra, treatments (`1 − Π(1 − ease × share)`, capped), the step toward the target, wake/sleep hysteresis, and the effects: `GrowthFactor`, `MoralePenalty`, `Deaths` (fractions carried) and `Split` (vagrants first for Exposure, citizens never below the survivor floor) |
| `HealthSettings` (`Resources/Population/Health`) | Every number and name: each pressure's growth weight, deaths, morale, treatments and cascades; crisis loads by crisis title and stage; harsh weathers by profile name; the tuning |
| `PopulationHealth` (added by `GenesisLoop`, saved as `_state`) | Each Seventh: gathers `HealthInputs` from the systems, sets targets, steps, then applies morale (`Health: {pressure}` sources) and deaths (`PopGrowthLogic.ProcessEventDeaths` / `ModifyVagrants`) |

- **Physical health** no longer has population-size immunity: Nutrition, Disease Burden, Sanitation and Exposure depend on causes at every size. Their asset checkpoints are zero, and their technology treatments ease exposure instead of moving an imaginary safe population ceiling. Harmonic Stability retains its explicitly fictional population ramp. Mortality settings use annual scenario rates divided by 252, with no immortal survivor floor in the shipped asset.
- **Target** = `clamp01(causes × emergence × susceptibility × (1 + scar × scarSensitivity)) × (1 − treatment)`; the level moves
  toward it at most `risePerSeventh` up or `fallPerSeventh` down, wakes at `activateAt`, sleeps below `dormantAt`.
- **Susceptibility** = `1 + Σ cascade weight × level` of each *active* source pressure (untreated Nutrition makes
  Disease Burden's causes weigh more). Susceptibility alone never makes a pressure: it multiplies causes.
- **Growth:** PopulationHealth.ThresholdFactor divides the annual birth rate, rather than changing a food-to-person price. Fractional births, deaths and hunger exposure are saved. The baseline is a crude-rate model, without individual ages or sex.
- **Inputs** (read only): Pantry variety and peach share, `isFoodScarce`, the Age Crisis title and `StageReached`,
  vagrants and housing, `TimeSystemLogic.CurrentEcho` (the Echo of Silence), the weather over the Capital, settlement
  strain, ruins within `ruinReach` of a settlement, the Capital cell's `dissonance`, `fallout` and `cascade` (Static
  Criticality). `vectorPressure` is a plain input, **0 until the ecology feeds it** (E2, then the Luminant Moths, E9).
- **Treatments** (`TreatmentKind`): Technology, Civic, Building, Score, StoreVariety, FreeHousing, Coherence,
  ResonanceAnchors, Enclave (a family under your Suzerainty). `ContentValidator.ValidateHealth` checks each name, the
  crisis titles and stage counts, the weathers, and that every pressure is described once.
- **UI**: a NotificationFeed notice per active pressure (`health:{pressure}`, Topic Crisis, `Describe` as its
  tooltip: level, trend, causes, cascades, scars, treatments, effects) and a line per active pressure on the population
  tooltip. `GameValues` domain `health`: a pressure's level 0-100 while active (0 while dormant), alone the number of
  active pressures.

### Keywords and the White-Haven Library

Lore reaches the player in two layers, kept apart as on the Sonata website (its keyword masterfile and its
White-Haven Library):

- **Keyword cards** are what a golden word shows when hovered: short, hand-written, and staged by Age and by the
  civics in force, so a card never knows more than the world does yet (the website stages them by chapter). They
  live in one file, `Resources/Keywords/Keywords.md`, in the website's format (see the README there).
- **The White-Haven Library** is the game's wiki, opened with L or from a card: the **Glossary** (the vault's lore,
  every note whole, as the import writes it to `Resources/Library/Library.json`) and the **Game Wiki** (how the game
  works: `Resources/Library/GameWiki.md` plus an entry per resource, building, technology, section, pillar, council
  seat, legend class, legend and civic, written from the game's data). A word with an entry but no card shows the
  entry's summary.

| Piece | Owns |
|---|---|
| `Keywords` | Every golden word and its card. A word means, in order: a card, a game term (pillars, aspects, resources, technologies, sections, the calendar), a Glossary entry (not quiet ones: ordinary words like Void reach it through `[[links]]` only). `Linkify` turns them golden in any tooltip text; `TryBuild` fills a card (title, kind, what it says now, live numbers, "Its meaning deepens in a later Age.", and the Library link); `AddLore` adds a game object's card reading and Library link to that object's own tooltip. |
| `KeywordMasterfile` | Pure: reads the masterfile (`## Term`, `wiki:`, `also:`, `id:`, `category:`, `autolink:`, `@default`, `@age-N`, `@<age-id>`, `@civic-<name>`; `TODO` states skipped) and picks a card's reading for a `KeywordContext` (Age id and number, civics in force). |
| `KeywordLiveValues` | Live numbers for game-bound ids (`pillar:`, `stat:`, `resource:`, `tech:`, `section:`, `term:`, `time:`). |
| `GameAge` (Core) | The Age the world is in, set by `AgeProgression` as the Ages pass (the Age of Desolation, Age 0, first). |
| `Library` | Loads both halves (the Glossary's JSON with `JsonUtility`, the Game Wiki's articles and generated entries) into one `LibraryIndex`. |
| `LibraryIndex` / `LibraryEntry` | Pure: entries by id and by name (titles, aliases, `Note#Section`, plurals and possessives; each half resolves its own names first), sections, the whole article of a split note, backlinks, related reading, shelves, ranked search, the ages table. The website's rules. |
| `LibraryArticles` | Pure: reads `GameWiki.md` (the masterfile's shape, with an article instead of states) and unwraps hand-written prose. |
| `GameWikiEntries` | The Game Wiki's entries written from the catalogs (costs, rates, who can sit where, civic effects...). |
| `LibraryWindow` | The reading room, built in code on its own canvas just under the tooltips': Glossary and Game Wiki tabs, shelves, search, the article (epigraph, "In this note", the whole note, "Threads from here", "Mentioned in"), links that open entries and show their cards on hover, Back and Forward, L and Esc. The HUD tabs ignore their hotkeys while it is open. |

### Lore from the vault

The Obsidian vault (`C:\Arcanoria Master\Arcanoria`) is the canon. **Tools > Gateway to Genesis > Import Lore From
Vault** (`Scripts/Editor/Lore`) turns its `Worldbuilding/` notes into the Library's Glossary
(`Resources/Library/Library.json`), copying the notes' words as written:

| Piece | Owns |
|---|---|
| `LoreNotes` | Reading a note as blocks (paragraphs, lists, tables, quotes, headings) and telling lore from the rest: design notes, status checklists, formulas, real-world references and pasted chat replies are left out, a whole block at a time. Epigraphs and sentence spans. |
| `LoreImport` | The manifest (`Resources/Library/Vault~/Manifest.md`: passages, other names, "See also", notes to split), long notes' sections and catalogue notes' (Civic) entries as entries of their own, the ages table (the vault's `Ages N` folders), every `[[link]]` resolved to an id, `Library.json`, and `Vault~/ImportReport.md` (every passage left out, every link with no entry). Pure .NET: it runs from the menu and outside Unity. |
| `LoreImportMenu` | The Editor menu and the vault folder setting. |

The import owns `Library.json`: re-running it rewrites it (and leaves it untouched when nothing changed). What a
tooltip says is never generated: that is the masterfile's, written by hand.

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
| Council seat | `CouncilSeatData` (a position such as High Arbiter, open to legends of several classes), or a civic's council position | `Resources/Council/` (see README) / civic asset |
| Lore (the Glossary) | a note in the vault, then **Import Lore From Vault** (never edit `Library.json` by hand) | the vault; `Resources/Library/Vault~/Manifest.md` |
| Keyword card (what a hovered word says) | a `## Term` in the masterfile, staged by `@age-N`, `@<age-id>`, `@civic-<name>` | `Resources/Keywords/Keywords.md` (see README) |
| Game Wiki article | a `## Title` in `GameWiki.md` (entries for game data are written by themselves) | `Resources/Library/GameWiki.md` |
| Event | Ink story + compiled JSON | `Resources/Events/` (see INK_FILES_GUIDE) |
| Age | `AgeDefinition` (Acts, Act of Fate and crisis stories, preparation technologies and scores, next Ages) + its locked Ink stories | `Resources/Ages/`, `Resources/Events/` |
| Terrain, biome, quadrant catalog or map feature | an entry in `WorldSettings` (features arrive with their `minAge`; a quadrant's biomes are shuffled into its stencil slots) | `Resources/World/World.asset` |
| Handmade tile (a hand-drawn patch of a biome) | a `.txt` honeycomb drawing (format in `TileTemplate`) | `Resources/World/Tiles/` |
| World composition | the stencil: Q1-Q7 quadrants, I intersections, W ocean | `Resources/World/Composition.txt` |

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
- **Tooltip for a new UI element**: implement `ITooltipSource` on its component and word it in `TooltipContent` with
  `TooltipText`; put a `TooltipTrigger` on the element (or a child). Fixed text from code:
  `TooltipTrigger.Ensure(go).SetCustom(title, flavour, type, body)`. Numbers and mechanics go in the body; the
  description slot is in-world flavour.
- **New keyword**: lore comes from the vault (write the note, re-import) and shows its summary until it has a card;
  a card is a `## Term` in `Resources/Keywords/Keywords.md`, and a rule a `## Title` in `Resources/Library/GameWiki.md`
  (no code; see the READMEs). For live
  values of a new kind of game thing, add a case to `KeywordLiveValues.TryFill` and give cards the matching
  `kind:` id (`id: kind:name`). Resources, technologies and sections are keywords automatically.
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
- Hotkeys are actions in `Input/PlayerControls.inputactions`, read only through `GameInput`; never poll the keyboard
  for a hotkey. Developer shortcuts use `InputUtils.DebugKeyDown` (Ctrl + key, editor and development builds only).
  A rebindable key is listed in `KeyBindings.Entries`. A window that closes on `GameInput.CancelPressed` adds its
  `IsOpen` to `OpenWindows.Any`, or its Escape also opens the quick menu.
- Views that show a list (seats, legends, civics) keep their items in a `ViewList<T>` and re-bind them in place;
  they redraw on a dirty flag at the end of the frame instead of rebuilding on every event.

## 9. Tests

`Assets/GatewayToGenesis/Tests/Editor` (Window > General > Test Runner > EditMode):
- `CoreSystemsTests`: ledger math, scoped targets, effect routing and validation, stat growth, compass, and guards
  that fail when an enum value is added without being wired up.
- `EventSystemTests`: the Ink grammar, chorus odds and outcomes, event wording, sentence splitting, tooltip
  formatting. Pure logic, so they also run outside Unity.
- `GameRulesTests`: weather selection (retention, variation boost, candidacy, weighted pick), council timing,
  scaling and assignment plans, cooldown timers, civic requirements (as event conditions) and log channels.
- `KeywordTests`: keyword links (sections too), the vault's Markdown, glyph safety, collapsing and pages.
- `LibraryTests`: the keyword masterfile (the website's format, readings by Age and civic, unwrapping) and the
  White-Haven Library (names as prose writes them, each half first, sections, backlinks, shelves, search, Game Wiki
  articles).
- `LoreImportTests`: reading notes, what counts as lore, epigraphs and sentences, and a whole import into a temporary
  vault (sections and the whole note, resolved links, the ages table, Library.json, removed notes).
- `EconomyRulesTests`: the §4 formulas (stats, morale, production, click power, build and research costs,
  population and food) against worked numbers.
- `GenesisLoopTests`: the Age schedule (every beat once), crisis severity and losses, hexes, legend growth.
- `TechTreeTests`: on the Act I tree as authored, what is uncovered and what stays hidden, line tones, routes that
  never cross a slot, research plans (prerequisites first, replace/append/remove) and Enlightenment wording. Pure.
  `TechTreePlayTests` checks the same in ClickerScreen: hidden slots take no pointer, a click on a locked technology
  plans its way, a met goal enlightens, research moves along the plan.
- `WorldGenerationTests`: the index-7 hierarchy, the stencil and handmade tiles, the slot solver (Q2 both orders, Q5
  three distinct of four, allowed orientations, catalog errors), the continent (determinism, no gaps, ocean ring,
  connected mainland, water running downhill, the capital on freshwater, seams without cliffs, a seed suite),
  features, the magic (junctions by family, an Age moving leylines but not land, silver downstream) and the zoom.
  Pure, so they also run outside Unity. `WorldViewPlayTests` scrolls out of the capital into the world and back with a
  virtual mouse. `GenesisLoopPlayTests` (a `[UnityTest]`) opens ClickerScreen and plays the Age of Desolation into the Age of
  Renewal. Inside it, avoid lambdas that capture locals: Play Mode's domain reload does not restore their closures.
- `ContentTests`: every authored asset and every compiled story validates against the catalogs; the Library loads
  both halves and every keyword card builds. Every legend class has a council seat.
- Ages and achievements: `AgeGateTests` (technology gates on the Age clock, crisis factors), `AchievementTests`
  (the note's reader, the rules, the Tracker), `AchievementContractTests` (the award transaction through a failed
  write, retry, crash and reload; aliases; evidence; the completion set; EraUnlock and `AgeCapabilities`),
  `AchievementRegisterTests` (Docs/Planning/ACHIEVEMENT_COVERAGE.csv against the note and the rules).
- Saves: `SaveTests` (identity binding, tampering, backups, retirement witnesses and sacrifice, reward ledgers, deep
  state trees, the state contract).
- Legends and stories: `LegendSoulTests`, `LyricalFragmentTests`, `LegendBalladHistoryTests`, `BalladActorTests`,
  `CouncilAreaTests`, `ExpeditionTests`, `SettlementStoryTests` (pure), with `LegendSoulPlayTests`,
  `BalladActorPlayTests`, `ExpeditionPlayTests` and `NotificationPlayTests` in ClickerScreen.
- The culture: `CultureTests` (pure: names and demonyms, the founding myths, leanings and reforms, foodways and
  national foods, spread, the kitchen labels of the stores, the `culture:` grammar, its achievement rules).
- Living traditions and the culture's shared contract: `TraditionTests` (pure: emergence, repetition before a custom,
  per-Seventh credit cap, dormancy keeping history, revival without repeated firsts, three places with three causes,
  benefits per definition under the cap, deferral and preservation, the occurrence ledger's dedupe across a save,
  replay after a save, an older save's honest migration, phase order), with `TraditionPlayTests` in ClickerScreen.
- Teaching: `CultureTransmissionTests` (pure: completion once, seats, pauses keeping progress, records versus
  practitioners, variants keeping their parent, Age-gated institutions, save round trip), with
  `CultureTransmissionPlayTests` in ClickerScreen (a community teaches a Legend, a teacher away pauses, reloads grant
  nothing twice, a Flavor Log adapts the Ash-Loaf with an invented bread that resolves after a load).

### The culture's shared contract (Culture/Integration, Culture/Traditions)

Every later culture feature (local cultures, memory, observances, hospitality, teaching...) builds on the same
contract, owned by the traditions task (T01, Docs/Planning/CultureRedesign):
- `CultureEntityRef` (kind + stable id; names are never keys), `CultureStamp` (culture Seventh, world
  Cycle/Echo/Phase/Seventh, Age), `CulturalOccurrence` (a committed action: dedupe key, purpose kind, subject,
  settlement or -1 unknown, actors when known, quantity with explicit units), `CultureCommandResult`, `CanonStatus`.
- `CultureSystem.Record(occurrence)` accepts each key once (`CultureOccurrences`, window of 126 Sevenths); occurrences
  wait in the saved ledger and are processed once per Seventh. `ICultureFeature` classes are found by reflection and
  run in `CulturePhase` order (source actions, dedupe, local participation, tradition lifecycle, social effects,
  notifications); a feature's own records during the Seventh join it.
- `CultureState.extensions` (`CultureExtensionState`, a partial class: each feature adds its own optional record in
  its own file) is migrated by `CultureMigration` (idempotent, never invents history). `GameSnapshot.Restore` ends
  with `CultureSystem.AfterRestore()`: caches cleared, envelope upgraded, every feature's `Reconcile(Restored)`, derived
  effects re-applied, nothing granted.
- `ICultureQuery` (partial interface) answers with snapshots (`TraditionView`...) and read-only command previews.
- Traditions: `TraditionTuning.definitions` (authored, canon-labelled, Age/technology gated), `TraditionRules` (pure
  lifecycle: emerging, practised, dormant, revived; recognition offered, deferred, recognised, preserved),
  `TraditionLifecycle` (the orchestrator), `CultureSystem.Traditions.cs` (adapters from rites, festivals, kitchen
  batches, holidays kept, landmarks and national foods; commands; effects under `CultureEffectPolicy`),
  `TraditionPanel` (UI module registered by the Culture window).
- Local cultures (T02, Culture/Local): `LocalPracticeCatalog` (authored customs with ground words, Ages, canon labels),
  `LocalCultureRules` (pure: profiles, exposure, participation, adoption, quiet/revival, visits, founders, arrivals,
  variants kept on recognition), `ContactSnapshot` (settlement adjacency from built roads, through fallen settlements,
  open or interrupted), `CultureSystem.Local.cs` (local gatherings, admissions, queries; `LocalCultureFeature` in
  phase LocalParticipation), `WorldSystem.CulturalContact.cs` (`CulturalContactMap`: contact and ground from the map;
  party repertoires; founding, settlers and festival hooks), `LocalCultureCard` (settlement and party cards).
  Admissions: `PopGrowthLogic` keeps the gate pool's origins as FIFO lots and reports each admission
  (`CultureSystem.Admitted`) with its origin, or none. Tests: `LocalCultureTests` (pure), `LocalCulturePlayTests`.
- Shared wounds (T03, Culture/Memory): `MemoryModel` (immutable `MemoryEvidence` by stable id: `crisis:<age>:<pass>`,
  `fall:<ruin>`, `recovered:<ruin>`, `legend-lost:<name>`; `MemorialDedication` links an existing recipe, rite, holiday or
  landmark by id, dormant when it is gone; `MemoryLineage` per Age passage; `MemorialSuggestion` canon-labelled),
  `MemoryRules` (pure: learn once, dedicate, settle, practise once per Seventh, capped recovery, lineage, recognition,
  memorial ground read-only with no bloom bonus), `CultureSystem.Memory.cs` (hooks `AgeProgression.AgePassed`,
  `LegendProgress.Lost`, `WorldSystem.SettlementFell`/`RuinReclaimed`; reads older saves' records as reconstructed;
  `MemoryFeature` in phase SourceActions), `MemorialPanel` (Culture window, Memorials). Tests: `CultureMemoryTests`
  (pure), `CultureMemoryPlayTests`.
- Hospitality and shared tables (T05, Culture/Hospitality): `HospitalityModel` (`TableRecord` "table-N" with its
  `TablePolicy` (public welcome, recovery support, patron-hosted), `ServedLine` by resource plus recipe id (a renamed
  recipe keeps its tables), the groups represented only as far as known; bounded history with per-settlement and
  per-patron summaries; `HospitalityTuning`), `TableServing` (pure: a menu of 1-3 finished foods sized in food value,
  no ingredients or spices, no table while Food falls or below the survival reserve), `HospitalityRules` (pure: effects
  by policy, cooldown, patron rest, coverage per settlement (never per head) and its bounded living contribution
  (`WellbeingInputs.sharedTables`, apart from the luxuries' demand), the access report naming hoarded or private-only
  luxuries), `CultureSystem.Hospitality.cs` (preview and `SetTable`: the spend goes through the one serving boundary,
  `CultureSystem.Serve` in Culture/Integration, which spends exact resources all or nothing and records the foodways'
  use once; one `Hospitality` occurrence per food served with the recipe's id; an open table exposes customs with
  neighbours by open road through `LocalCultureRules.Table`), `HospitalityPanel` (Culture window, Tables; the settlement
  card's "Set a communal table" opens it on that settlement). Saved in `CultureState.extensions.hospitality`. Tests:
  `HospitalityTests` (pure), `HospitalityPlayTests`.
- Ensembles and performance (T08, Culture/Performance): `PerformanceModel` (`RepertoireSpec` authored with canon label,
  vault note, Ages, technology, civic, binding and intents; `PerformanceBooking` "perf-N" reserving a cultural party's
  festival; `PerformanceEcho`, the bounded expiring benefit; `PerformanceRecord` with its explanation;
  `PerformanceTuning`), `PerformanceRules` (pure: eligibility (the Ages IV-VI chorus needs its Age and civic), the
  deterministic evaluation (Composure and binding readiness, the weakest voice counting 1/n, the Legends' directional
  readings of each other, the place), booking checks (lapse, walk-away, a stopped festival), completion once, echoes
  (one per settlement, morale for the strongest few, expiry) and the Coherence overlay), `CultureSystem.Performance.cs`
  (previews, plan/cancel, `CompletePerformance` called once by `WorldSystem.FinishFestival` through
  `WorldSystem.Performance.cs`, one `Performance` occurrence, `LegendProgress.ShareExperience` with no direction and no
  wound, named morale sources `Culture: <piece> in <place>`, `PerformanceFeature` in phase SocialEffects order 30),
  `CityDevelopment.CoherenceOverlay` / `CoherenceOf` (every development read of Coherence adds the overlay; the tile's
  own value never changes), `PerformancePanel` (Culture window, Performances; the party card's "Plan a performance"
  opens it on that party). Saved in `CultureState.extensions.performance`. Tests: `PerformanceTests` (pure),
  `PerformancePlayTests`.
- Apprenticeships and institutions (T06, Culture/Transmission): `TransmissionModel` (`TeachingOrder` by stable id
  `teach-N`: a tradition by T01 instance id, taught by a Legend, a community (`settlement:<id>`, no Legend needed) or a
  written record, to a Legend or a community, at a settlement's hearth or an `InstitutionRecord` at a landmark
  (`CultureIds.Landmark`); preserve makes a `TaughtBearer`, adapt a `PracticeVariant` that keeps its parent and its own
  dish (`CultureEntityRef` recipe id, authored or invented), a records-keeping institution a `WrittenRecord`, which is
  never a practitioner; `TransmissionTuning`: the Hearth and the Age-gated Flavor Log / Guild of Ballads and Plays /
  Cooking Guild from Civic.md, complexities, seats), `TransmissionRules` (pure: seats as the real capacity, complexity
  limits, one teaching per Legend, Sevenths needed, pause keeping progress and seat, completion once, cancel, reassign,
  founding and a Flavor Log growing into a Cooking Guild), `CultureSystem.Transmission.cs` (availability read from the
  world: `LegendProgress.IsLost/IsRecruited`, `WorldSystem.ExpeditionOf`, settlements by id and name, landmarks; a dish
  is only taught when `WhyNotRecipe` says the kitchen can make it (teaching never unlocks one); commands with previews;
  `TransmissionFeature` in phase SourceActions order 60, a `Teaching` occurrence per completion; `TeachingValue` for
  conditions), `TeachingPanel` (Culture window, Teaching). Saved in `CultureState.extensions.transmission`. Tests:
  `CultureTransmissionTests` (pure), `CultureTransmissionPlayTests`.
- The Edicts: `EdictTests` (pure: establishment and slots, stances and cooldowns, levers, sealing and resting, the council's accord, the story values), with `EdictPlayTests` in ClickerScreen (the third seat establishes them, a stance moves the pillars, caravans and borders, an edict fills its slot and rests, save round trip).
- The world beyond generation: `WorldCivilizationTests`, `WorldTerritoryTests`, `WorldUnitTests`, `WorldReliefTests`,
  `WorldWeatherTests` (pure), with `TerritoryPlayTests` in ClickerScreen.

Game rules that decide outcomes live in pure classes next to their system (`ChorusRules`, `WeatherRules`,
`CouncilRules`, `StatRules`, `ProductionRules`, `PopulationRules`, `GovernmentCompass`, `StatGrowth`) so they can be tested; the system keeps the state and supplies
random rolls.

## 10. Known limitations and risk areas

Ranked by how likely they are to cause trouble as content grows.

1. **Saves are exact-match only.** `SaveSession`/`GameSnapshot` persist world slots (save schema 3), the lifetime
   profile and per-world achievement/Anchor ledgers, and restore without replaying effects or awards
   (Docs/Planning/SAVE_SYSTEM.md). A save loads only with its original generator version and catalog; there is no
   general migration yet, and a field added to a saved system must be added to `GameSnapshot.Schema`.
2. **Hard-coded names**: special technology unlockables are matched by asset name in
   `GameUnitsLogic.HandleSpecialUnlockable`. Food and Research
   are no longer names: the population finds them by `GameUnit.role` (`GameCatalog.ResourceFor`), so either can be
   renamed. The population still has exactly one food: a second food-like resource is an ordinary resource unless
   `PopGrowthLogic` is extended to eat from several.
3. **Unimplemented hooks**: `SpecialAbility` effects are accepted but do nothing (they log when applied).
   `EraUnlock` civic requirements are real (the Age reached or the Age number), and an unknown requirement fails
   closed.
   `LegendData.compatibleSeatClasses` is unused: seats decide eligibility.
4. **Archived prototype**: the first prototype's data (`ResourceSO`, `ProductionEntitySO`, `AgeSO`, `Decision`,
   `PillarCivilization`), its scripts and the old tooltip prefab live in `GatewayToGenesis/_Archive~`, which Unity
   ignores. Nothing in the game references them; restore from there only to read how something used to work.
5. **UI found by name**: a few views locate scene objects by GameObject name rather than a reference: the chorus
   drop areas (`Idealism`/`Realism`/`Pragmatism` under the backgrounds container), the HUD click buttons
   (`BuildingMaterial`/`VitalResource`). Renaming those objects
   breaks the link silently; keep the names or replace them with serialized references. Two more lookups by name
   warn at start-up instead of failing silently: every tab managed by `TabHotkeys` must keep its panel under a
   `Display` child, and `GlobalCharacterManager` parents villagers under the spawn area's `Population` child
   unless its `populationParent` field is assigned.
6. **Event screen prefabs**: `EventScreenManager` fills screens by child names (`Title`, `Subtitle`, `Content`,
   `Button`/`ButtonText`, `Consequences`, `HasHappened`, `EventColor`, `Background`, ...) and the chorus result card
   by `Result` and `RollChance`. A prefab variant must keep those names.
7. **The lore filter is a heuristic**: the vault import tells design notes from lore by their words (the game, the
   player, mechanics, real-world works...), a whole passage at a time. It errs both ways now and then; after editing the
   vault, skim `Resources/Library/Vault~/ImportReport.md` and correct a passage with the manifest (`skip:`, or name
   it in `details:`), never by editing `Library.json`.
8. **Few cards yet**: only the words in `Resources/Keywords/Keywords.md` have hand-written, Age-staged cards; every
   other Glossary word shows the lead of its note, which can run to a few sentences. The Age now changes
   (`AgeProgression`, saved with the world), so cards staged `@age-1` start to show.

### Sprite ecotypes and entrainment

Early sprite ecotypes and their Renewal slime descendants are separate `SpeciesSpec` entries, linked by `entrainedFrom`. `ecotypeFamily` groups these variants for lineage-level diet accounting only. `WorldEcology.Entrain` runs in the Echo tick after den founding, transferring a small share into matching descendants in Agromagical-tended habitat from Age 1. Normal saved populations carry progress; normal ecology founds the new dens. Descendant site specs have `count = 0`, `minAge = 1`.

Unmerged Elemental Sprites use `unmerged` and `ResourceSiteSpec.requiresCoherentRefuge`. `WorldResources.CoherentRefuge` requires ambient Coherence >= 0.8 plus sacred ground or a leyline junction >= 2. Placement prioritizes the nearest fitting refuge before common resource patches; den founding respects its world-wide count cap. `Quality` enforces this restriction as magic changes. The bestiary and survey reward pipeline expose the discovery without revealing its identity before survey. Ice/snow and saltwater habitats use `maxTemperature` and `coastal`; those fields and the refuge rule participate in the catalog fingerprint.

### Cultural luxuries and living amenities

`CultureLifeTuning.goods` extends luxury-category resources with durable-amenity and Faith-per-unit behavior;
`gardens` names world-site amenities. `CultureLifeRules.Luxuries` allocates only as many best-met categories as the
population wants, sharing an availability ledger so one unit cannot count twice. Lasting Sky Glass and surveyed,
held, healthy Eleos gardens are enjoyed without consumption. Sacred teas are consumed; Faith is paid per unit
actually enjoyed. Food scarcity excludes edible luxury draws. The ordinary Seventh flow records consumption in
foodways and cultural leanings; the last Faith award is saved as optional `CultureState.culturalFaith` for display.
The stored food-class index `EleosTea` remains compatible with saves and now displays as Tea, including ordinary brews.

Saffron, salt and everyday tea sources use resource-site habitat restrictions; `minimumDesirability` reads the
natural land score and `requiresSacred` accepts a sacred cell or immediate neighbour. The new dishes are ordinary
Flavor Log recipes and Pantry foods. See `Docs/Planning/LUXURIES_AND_AMENITIES.md` for content and validation details.

## Culture redesign closeout: public accounts and atlas

T09 lives in `GameData/Culture/PublicMemory`: saved public links connect an established tradition to an eligible civic promise; immutable, versioned accounts remain distinct from evidence. Comparisons use bounded past records, not future-dated facts. The Hunger promise filters the named Hunger crisis, not every crisis. `PublicAccord` evaluates active laws and surviving traditions on each read, so repeal removes its effects immediately. Resolution validates the definition, link, current eligibility and answer before spending. `EventSystemLogic.TriggerStory` binds public-memory story text, conditions and consequences to the same dispute. Restore rebuilds pending notifications without randomly starting stories or replaying rewards. `ContentValidator` includes transmission definitions; `teaching:` conditions route to `TeachingValue`.

T10's `UI/Culture/Atlas/CultureAtlasReadModel` builds detached snapshots over T01-T09. Keys include entity kind and stable ID; local definitions, tradition definitions, instances, institutions and teaching variants never merge on a display name. It joins recipes to recorded use, evidence, teaching and places, keeps account versions separately navigable, and preserves missing references as unknown. `CultureAtlasWindow` keeps only selection IDs between refreshes and delegates previews/commands to domain owners. It provides comparison, scroll/keyboard navigation, Library and Ballads & Bonds links, and routes to the existing action panels. The Nation HUD presents one contextual opportunity; the ruin card routes to Heritage.

The explicit T04/T08 adapter is `CultureSystem.ObservancePerformance.cs`. Calendar bookings store optional host ID and occasion date. A compatible celebration, performance or remembrance can host a real free party at its settlement. Date changes, departure, missing voices or a missed observance cancel the booking. The due-day path inspects the unresolved occasion, because the public calendar already displays the next recurrence. Only the shared completion gate issues performance rewards and participant occurrences. Old festival bookings retain their existing path.

Exploration uses the existing regional/micro knowledge textures: fog hides unseen ground, visible wilderness retains muted colour, known ground has intermediate saturation, and surveyed ground has full colour. `WorldExplorationAppearance` supplies saturation values to `WorldTerrain.shader`. There are no new exploration glyphs. Partial regional surveys remain visible at individual-hex zoom without claiming the whole region is surveyed.
