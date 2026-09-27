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
| Declared Age Crisis (Food output) | `Age Crisis: {crisis title}` | From the stage marked declared until the Age passes |
| Explored land (flat production) | `Land: {terrain or feature name}` | Forever; re-applied as a total per kind when a hex is explored |

These names appear in tooltips as the "from ..." label.

## 4. Core formulas

Each formula is a pure function (`StatRules`, `ProductionRules`, `PopulationRules`, `StatGrowth`, `CouncilRules`), pinned
to worked numbers in `EconomyRulesTests`; the systems gather the inputs and call them.

- **Pillar** = `(base + flat) × (1 + % / 100)`, min 1. Base = inspector value + event changes.
- **Substat** = `(round(pillar × pillarMultiplier) + event adjustments + flat) × (1 + %)`, min 1.
- **Derived stat** = `(StatGrowth.Evaluate(substat) + flat) × (1 + %)`. Curves are tiered (42/86/150/220 by default).
- **Thresholds** (max morale, morale balance, satisfaction upgrade) = `(base + flat) × (1 + %)`.
- **Resource net rate** = `[Σ producers rate × units × (1 + efficiency%) + scaling + positive flat] × (1 + (resource% + morale delta)/100) − consumption`.
- **Click power** = `(base + permanent upgrades + flat) × (1 + (% + Click Power Bonus stat)/100)`, never below base.
- **Build cost** = `base × (1 + cost%)` (floored at 10% of base) `× e^(costBalance / techTier × owned)` for buildings.
- **Housing** = `(scene + buildings + events + flat) × (1 + %)`.
- **Food per citizen** = `base × clamp(1 − morale delta%/100, min, max)`, min 1; stored food becomes housed citizens,
  then vagrants (if allowed). **Food demand** = `(buffer + e^(rate / sustainability tier × population)) × demand modifier`.
- **Morale** drifts to its resting point each seventh (down `⌈Waltz × factor⌉`, up `round(Waltz × Morale Recovery)`, min 1);
  losses shrink and gains grow with Morale Loss Mitigation.
- **Council** (`CouncilRules`, re-applied from scratch on any change): a seat's bonuses apply as soon as a legend
  sits in it, the legend's own bonuses once it has activated; an empty seat gives nothing. Phase A authority, phase B
  direct Legend Effectiveness, phase C everything else. Legend bonuses are multiplied by the Head of State multiplier
  (Head of State only) and, in phase C, by `1 + LE%`. Seat bonuses are never scaled.

## 5. Systems

| System | Owns | Listens to | Raises |
|---|---|---|---|
| `TimeSystemLogic` | Calendar (seventh/phase/echo/cycle), pause, slow motion | — | `OnSeventhChange`, `OnPhaseChange`, `OnEchoChange`, `OnCycleChange`, `OnRitualSeventh` |
| `StatManager` | Pillars, substats, derived stats, morale, satisfaction | seventh | `OnStatsChanged`, `OnPillarChanged`, `OnDerivedChanged`, `OnMoraleChanged`, satisfaction events |
| `GameUnitsLogic` | Storage/production/research tabs, clicking, building, research and the research plan, Enlightenment's gift, unit ledgers | — | `OnProductionUnitBuilt`, `ResearchPlanChanged` |
| `TechnologyTreeLogic` (one per Age tree) | Its slots, what is uncovered, the lines (`TechTreeConnectors`), the plan's numbers, the Enlightenment goals (rules in `TechTreeRules`) | `Researched`, `Enlightened`, `ResearchPlanChanged`, the Age's waiting gate | — (slots raise `GameTechnologySlot.Researched`, `Enlightened`) |
| `GlobalProductionManager` | Net rates of every resource | ledgers, morale | writes `GameResourceSlot.productionRate` |
| `ProductionLogic` (per resource slot) | Accrues the net rate every second (paused during events) | — | — |
| `PopGrowthLogic` | Population, housing, vagrants, deaths, food demand | Food changes | — |
| `GovernmentLogic` | Political compass, council seats, cooldowns, council effects (rules in `CouncilRules`) | stats, seventh | government and council events |
| `LegendLeaderLogic` | Legend roster, activation delay setting | — | — |
| `CivicManager` | Active civics, slots, requirements, removal penalties | seventh | civic events |
| `CelestialWeatherSystemLogic` | Weather selection and effects (rules in `WeatherRules`) | seventh, echo, cycle | `OnWeatherChanged` |
| `EventSystemLogic` | When stories trigger, consequences, timed effects, scores; the story's ballad actors (`CurrentCast`, rules in `BalladActors`: carried ballad actors, the expedition that found it, a named legend, the seat answering for its area via `GovernmentLogic.AnswerFor`/`CouncilAreaRules`, the Head of State, else the player picks) and ballad progress (`Ballads`, finale pays the theme) | time events | `CastChanged`, `BalladCompleted` |
| `InkDrivenEventSetup` | Turns every compiled story in `Resources/Events` into a volume and validates it | — | — |
| `EventVolumeManager` | Live Ink stories, story start/navigation/completion (the one `CompleteStory`) | — | — |
| `EventScreenManager`, `ChorusScreenManager` | Event screens and the chorus drag-and-roll, as views over the story index | pillars (chorus) | — |
| `TooltipSystemLogic` | Open tooltips (a root plus nested keyword tooltips): follows the cursor, turns them solid, refreshes (4 Hz) and closes them | — | — |
| `GameInput` | Every hotkey, as actions of `PlayerControls.inputactions` (tabs, Library, cancel); none while typing or with Ctrl/Alt held | — | tab UnityEvents, `LibraryPressed`, `CancelPressed` |
| `GovernmentTab` | The council, civics and the legend / seat pools, as views re-bound in place once per frame | council, civics | — |
| `Achievements` (static) | The vault's achievements (Resources/Achievements), which are unlocked, the unlock toast; rules in `AchievementTriggers`, awarded only through `AchievementAward.Commit` (world ledger, then profile, then Tracker), renamed ids in `AchievementAliases`, the 100% set in `AchievementCompletion` | reported signals (after the action commits, with `.From(source, subject)` evidence) | `Unlocked` |
| `SaveSession` (static) | World slots, the lifetime profile, per-world achievement/Anchor ledgers (`WorldRewards`, with award evidence), retirement; state contract in `GameSnapshot.Schema` | save menu, autosave | — |
| `AgeProgression` | The Age clock (sevenths into the Age), Acts of Fate, crisis stages, the passage between Ages (rules in `AgeRules`, `CrisisRules`; data in `AgeDefinition`) | seventh | `Changed`, `AgeBegan`, `AgePassed` |
| `WorldSystem` | The world (`WorldMap`, built by `WorldGenerator` from `WorldSettings`, the stencil and the handmade tiles; see "The world" below), units walking the micro grid and their needs, expeditions of legends (rules in `Expeditions`: slots, the party's unit, hardship, mishaps rolled each Seventh; `HardshipOf` feeds `LegendProgress`; party size in `PartyShapes`: Solo/Duo/Trio/Company numbers, companions answering mishaps, retreat and going missing in action, the moves in `WorldSystem.Parties.cs`), land yields, each Age's magic and new features | seventh, `AgeBegan` | `Changed`, `Notice`, `UnitNotice` |
| `LegendProgress` | Which legends are met, their Lyrical Fragments by kind (rules in `LyricalFragments`), rank and deeds (rules in `LegendGrowthRules`), and who they are: each legend's `LegendSoul` (Soul Leitmotif, Legend Traits read from the vault note by `LegendTraitNote`, Composure settled each Seventh by `LegendSoulLife`/`ComposureRules`, Motif Awakenings by `LegendSoulRules`; tuning in `Resources/Legends/LegendSettings`); rank and Composure feed `CouncilRules` through `CouncilMultiplier` | seventh | `Changed`, `Recruited`, `RankedUp`, `ComposureChanged`, `Awakened`, `Lost` |

`AgeProgression`, `WorldSystem`, `LegendProgress` and their views (`AgeBanner`, `WorldView`, built in code with
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
`Resources/Council/Council Areas`); stories ask for an area (`# cast: area:defense`), never a seat title. The Fate Stage
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
| `WorldStencil` | The composition (`Resources/World/Composition.txt`): each same-sector block is a slot, slots only P apart form a sector instance; slot tags (north/south/east/west, coast/inland, core) |
| `TileTemplate` | Handmade tiles (`Resources/World/Tiles/*.txt`): honeycomb drawings of one biome with a legend, optional heights and allowed rotations |
| `SlotSolver` | Each sector's biome catalog into its slots with orientations: seeded, most-constrained-first backtracking, relaxations and catalog errors reported |
| `WorldGenerator` | Domain, warped stencil, slot distances and continuous seam blending, land and sea, fields, stamping tiles, topology repair, priority-flood drainage, lakes, rivers, ground, land fertility, the capital, features (`PlaceFeatures`); every step records into `WorldGenReport` |
| `WorldMagic` | Coherence/Dissonance Seeds, Sacred Sites, the Grand Thread Rings' field, leylines per Age, Convergences/Basins, silver rivers, Coherence and magical fertility; `Apply(age)` commits once, `Forecast(age)` previews |
| `WorldNoise` | Seeded noise and hashes, one stream per purpose |
| `WorldZoom` | The zoom: camera size to the micro/meso/macro reading, steps, zoom about the cursor |
| `MicroNavigation`, `MicroGrid` | The travel grid units walk: micro hexes addressed as cell index x 7 + child with one neighbour pattern; step costs (ground and ragged seams, crags, fords, roads hex by hex, leylines, danger, weather, slope), ground components, an exact A* with pooled buffers; orders to a hex or a cell's heart with the nearest-reachable fallback. Built lazily per map, never saved |
| `WorldGeometry` | The river meander and road lines, shared by the renderer (what is drawn) and the grid (what is forded and followed) |
| `WorldUnits`, `UnitAbilities` | Units: micro movement, needs (rations drawn from the stores or gathered in camp, fatigue, attrition, pace; `ProvisionRules`), sight, surveys per hex; abilities with a reach (hex, around, whole meso hex), duration and toll |
| `WorldTerritory` | Administrative Authority as territory: seats (`SeatKind`: Capital, Major, Trade Nexus, Trade Node, Town, Haven, Grandfield, Outpost; enclaves as rivals) and their pull over governance travel; passive adoption per seat (`Tick`, `Candidates`, `Priority`, the player's `BorderPolicy`); Administrative Capacity vs load, strain, efficiency, drift, and where horizontal gives way to vertical (`Realm` → `RealmReport`). Derived each rebuild (`WorldMap.territory`); adopted cells saved (`WorldMap.Adopted`). Rules: `SettlementRules.territory` (`TerritoryRules`) |
| `WorldBeauty` | How fair or hideous a cell is (-1..1) from its ground, what stands on it, water in view, Sacred Sites, leyline lights, vistas, Coherence, dissonance and danger; read by adoption priority, City Development, land yields and load |

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
| Terrain, biome, sector catalog or map feature | an entry in `WorldSettings` (features arrive with their `minAge`; a sector's biomes are shuffled into its stencil slots) | `Resources/World/World.asset` |
| Handmade tile (a hand-drawn patch of a biome) | a `.txt` honeycomb drawing (format in `TileTemplate`) | `Resources/World/Tiles/` |
| World composition | the stencil: S1-S7 sectors, P seams, W ocean | `Resources/World/Composition.txt` |

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
- `WorldGenerationTests`: the index-7 hierarchy, the stencil and handmade tiles, the slot solver (S2 both orders, S5
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
