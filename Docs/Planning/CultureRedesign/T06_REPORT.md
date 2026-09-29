# T06 completion report: apprenticeships and cultural institutions

Agent C, Wave 2, 28 September 2026. Built against T01's contract (`Culture/Integration/*`: `CultureEntityRef`,
`CultureIds`, `CulturalOccurrence` kind `Teaching`, `CultureCommandResult`, `CanonStatus`, `ICultureFeature`,
`CultureExtensionState`, `ICultureQuery`) and T01's traditions (`TraditionInstance`, `TraditionView`). Player promise:
"Our traditions survive because someone learned to carry them."

## Owned files

- `Scripts/GameData/Culture/Transmission/TransmissionModel.cs`: saved records (`TeachingOrder`, `InstitutionRecord`,
  `TaughtBearer`, `PracticeVariant`, `WrittenRecord`, `TransmissionState` in `CultureExtensionState.transmission`,
  `[SaveOptionalField]`), the append-only enums, and `TransmissionTuning` (the Hearth, the three institutions, complexities).
- `.../Culture/Transmission/TransmissionRules.cs`: pure rules (seats, complexity limits, availability of teacher and
  learner, Sevenths needed, advance/pause, completion once, cancel, reassign, founding and evolution, trimming).
- `.../Culture/Transmission/CultureSystem.Transmission.cs`: the world adapter (Legend availability, settlements,
  landmarks, the kitchen), commands with previews, snapshots, `TransmissionFeature` (phase `SourceActions`, order 60),
  `TeachingValue` for conditions, the `ICultureQuery` additions.
- `Scripts/UI/Culture/Transmission/TeachingPanel.cs`: the Teaching panel and its section in the window body.
- Tests: `Tests/Editor/CultureTransmissionTests.cs` (10 pure), `Tests/Editor/CultureTransmissionPlayTests.cs` (scene).

## Shared edits and patch requests

- `CultureWindow.cs` (C is its final editor): `LifeMode.Teaching`, the "Teaching" bar button, `TeachingPanel.Reset/Fill/Describe`.
  T05 edited the window at the same time. Its Tables registration was kept and merged by hand.
- `ARCHITECTURE.md`: Transmission module and test entries. `Vault~/Canon Gaps.md`: "Apprenticeships and institutions".
- Requested from Agent A (their registries; not edited here):
  1. A `teaching` condition domain in `GameValues`, routed to `CultureSystem.TeachingValue`.
     Keys: `teaching_orders`, `teaching_bearers`, `teaching_variants`, `teaching_records`, `institutions`, `institution:<spec>`.
  2. A `ContentValidator` check that each `InstitutionSpec.venues` id is a `LandmarkSpec.id` and each
     `PracticeComplexity.definition` is a `TraditionDefinition.id`. The pure test
     `TheAuthoredInstitutionsMeetInRealLandmarks_AndNameTheirAgesAndCanon` covers this until then.
- Note for T10: T02's local custom "Village Flavor Log" (`LocalPracticeCatalog` id `flavor-log`) and T06's institution
  `flavor-log` share an id in different catalogs. Both rest on the same Civic.md line. The Atlas should show them as the
  custom (kept by one village) and the institution (founded at a Feast Hall) rather than merge them.

## Player-visible behaviour

- **Culture window, Teaching.** An overview of open orders, recent completions and every teachable custom (T01
  `established`), each showing who carries it: practised, Legends, communities, local forms, records.
- **Setting up a teaching** is step by step, each option with its reason when blocked:
  1. Preserve, adapt, or write it down (a variant can be taught or written down too).
  2. The teacher: a Legend who carries it, "the people of <settlement>" (no Legend needed), or a written record.
  3. The learner: a Legend, or a settlement's people.
  4. For an adapted food practice, the dish: any recipe the kitchen can already make, invented ones included.
  5. The place: a settlement's hearth, or an institution, with its seats taken.
  6. A preview (complexity, Sevenths, seat, what it will make; "Nothing is spent"), then "Begin the teaching".
- **Orders.** An open order can be cancelled (seat freed, nothing made, history kept), or another carrier can take it
  over (progress kept; a record as teacher makes it slower).
- **Institutions.** Founded at a standing landmark within their Ages, for Unity:
  - Flavor Log: Feast Hall, Ages I-III, 2 seats, up to complexity 3, keeps records, 15 Unity.
  - Guild of Ballads and Plays: Hall of Song or Amphitheatre, Ages II-IV, 2 seats, 3, records, 20 Unity.
  - Cooking Guild: Feast Hall or Pleasure Garden, Ages IV-VI, 3 seats, 4, records, 40 Unity. A Flavor Log in the same
    hall grows into it, and its open orders move over with their progress.
  - Out-of-Age institutions are listed muted with their Ages. The Hearth needs no founding: every settlement has one
    seat, up to complexity 2, from the Age of Desolation.
- **Each Seventh.** Every open order advances once when teacher, learner, place and dish are all there. Otherwise it
  pauses with its reason, keeping its progress and seat, and a notice is posted once per pause. Reasons: away with
  an expedition, lost to Dissonance, a settlement that fell, a landmark gone, an institution that grew into another,
  a dish no longer makeable.
- **On completion.** Preserve makes a bearer (a Legend then carries it and can teach it on). Adapt makes a documented
  local form, "<parent> of <settlement>, with <dish>", which keeps its parent id and definition and its own recipe id.
  Write down makes a record. Each also gives a `Teaching` occurrence (`teaching:<order>`, actors when Legends, recipe
  when a dish, source = the institution's landmark), a history moment and a notice.
- **Records against practitioners.** `CarriersOf(tradition)` lists living, away and lost Legends, standing and former
  communities, variants and records separately. `Living` is never true from a record. A record can teach a practice
  of complexity below 3 again (x1.5 Sevenths). Complexity 3 or more needs someone living.
- **Minimal content.** One culinary practice (The Ash-Loaf Table, adapted at a Flavor Log) and one song/rite (Tales at the
  Hearth; The Evening Song, The Naming of the Lost are complexity 1 too). Ordinary practice (cooking, rites, festivals)
  never requires teaching.
- **Canon.** Explicit canon: the three institutions and their Ages (Civic.md, cited on each spec). Canon-supported
  adaptations: their venues and the Flavor Log's evolution. New game rules: the Hearth, orders, seats, complexities and
  every number (proposals, Canon Gaps).

## Save and edge behaviour

- Saved in `CultureState.extensions.transmission`. An older envelope loads with an empty transmission; this is tested.
  `ReconcileTransmission` (on `AfterRestore`) only makes lists whole and raises id counters past any saved id. Nothing
  is replayed or granted.
- **Repeated load, duplicate completion.** `Complete` refuses an order that is not open or already has a `result`, and
  `AddBearer` dedupes by order id. `Advance` counts a Seventh once (`lastProgress`). The occurrence key `teaching:<order>`
  is deduped by T01's ledger. The play test saves and restores twice, then ticks: one bearer, one moment.
- **Cancellation** frees the seat and makes nothing. A paused order keeps its seat until cancelled (the real capacity cost).
- **Missing references** pause, never delete:
  - A lost Legend: another carrier can take over.
  - A fallen settlement: matched by id and name, so a later settlement reusing the id is never taken for it.
  - A removed landmark or institution.
  - A missing record, or an unknown recipe.
- **Recipes.** A draft or an order whose dish fails `WhyNotRecipe` is refused or paused: unknown recipe, technology
  not researched, or resource not registered. Teaching metadata never grants an ingredient, technology or recipe.
  Invented recipes resolve through `CultureSystem.Life` (authored plus `WithInventions`, not a cached authored list), by
  recipe id.
- Finished orders beyond 40 are trimmed; the bearers, variants and records they made are kept for good.

## Tests actually run

- Private build (whole tree, scratchpad copy of the csproj files): 0 errors.
- Offline reflection runner: `CultureTransmissionTests` 10/10 passed.
- Unity batchmode EditMode, filter `CultureTransmission`: 11/11 passed (10 pure + the scene play test).
- Unity batchmode EditMode, filter `Culture|Tradition|Hospitality` (every culture suite incl. peers' T01-T05): 143/143 passed.
- Two defects found by the scene run and fixed:
  - The play test's lambdas captured iterator locals, which the domain reload loses. The scenario now runs in a
    static `Scenario()`.
  - `WhyNotRecipe` wrongly refused an invented dish: it has no store slot until its first batch. It now asks
    `GameCatalog.IsResource`, which includes `RuntimeUnits`.

## One acceptance path

1. Found the culture and hold Tales at the Hearth three times: it becomes a custom and appears under Teaching.
2. Teach it: the people of the Capital, to a Legend, at the Capital's hearth. The preview says 3 Sevenths, one of 1
   seat. A second order at that hearth is refused ("Every seat ... is taken").
3. After 3 Sevenths the Legend carries it: one occurrence, one moment. Save, load, load, and tick: still one.
4. That Legend teaches a second Legend. Send the teacher on an expedition: the order pauses at 1 of 3 ("... is away
   with ..."). When they return it resumes at 2.
5. In the Age of Desolation the Flavor Log is refused ("Ages I-III"). In Age I, found it at a Feast Hall for 15 Unity.
   A Cooking Guild is still refused ("Ages IV-VI").
6. Make the Ash-Loaf Table a custom and invent "Ember Bread". Adapting at the hearth is refused as too complex.
   At the Flavor Log the Capital adapts it with Ember Bread, and the log writes it down (both seats).
7. Save and load mid-lesson. Ember Bread still resolves. After 12 Sevenths there is one variant, "The Ash-Loaf Table
   of <Capital>, with Ember Bread", with its parent kept, and one record. No new plain bearer was made by writing.

## Remaining defects and proposals

- `WhyNotFound` names venues from their ids ("Feast hall"), not the landmark's display name. It is pure and has no Life
  tuning. Cosmetic.
- The `teaching` condition domain and the validator check wait on Agent A (above).
- Every number is a proposal. Complexity is a per-practice teaching cost, not a culture rating.
