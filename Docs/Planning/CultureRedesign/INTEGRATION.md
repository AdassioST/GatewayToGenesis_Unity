> Historical handoff/baseline. Superseded by [FINAL_REPORT.md](FINAL_REPORT.md) and [T10_REPORT.md](T10_REPORT.md). Earlier license and missing-atlas/observance-bridge notes describe the pre-T10 state.

# Culture redesign: integration contract notes

Companion to [the full design](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CULTURE_REDESIGN.md>). Observed in the working tree on 28 September 2026; the recipe agent is actively editing this area. Verify signatures at handoff. Proposed interfaces in the design document do not exist merely because they are named there.

## Verified entry points and required additions

### Culture lifecycle and input attribution — T01, A

`CultureSystem.Tick` currently increments culture time, spreads presence, runs `LifeSeventh`, updates food familiarity, gathers drift inputs, clears resource/lived input accumulators, then applies character effects. `RecordUse` combines cooking inputs and consumption into one per-resource accumulator. Preserve this existing economic influence while separating new participation events by purpose: production, ordinary consumption, hospitality, luxury use, teaching, performance, and memorial observance.

Do not reinterpret every `RecordUse` as a person attending a meal. New tradition participation should observe committed actions with dedupe IDs. `Pantry.Eaten` reports resource and quantity, not settlement, participants, or occasion; missing context stays unknown. Migration must not fabricate old participant histories from aggregate `Foodway.eaten` values.

`CultureState` currently holds the model; `CultureSystem.State` exposes it directly. The new public query contract should return snapshots rather than extending mutable-list access to every feature. Retain existing callers while moving new views to read-only queries.

`ApplyCharacter` currently applies only the dominant family; the two-family `Character` string is descriptive. The scoped redesign adds practice-specific effects with caps and transparent sources. It does not silently activate the full bonus of all ten families. Treat first-match terrain strings as legacy input; prefer explicit authored family tags for new content and validate unresolved mappings.

### Recipe and pantry boundary — T01/T05, A integrates; recipe owner consulted

Current `InventedRecipe.id` and `RecipeSpec.id` are suitable recipe reference candidates; the produced resource still uses a name. `CultureLifeTuning.WithInventions` creates a copy with recipes and luxury/good classifications. Do not cache a stale authored-only list or mutate the asset when resolving inventions.

`CultureSystem.Cook`, `SetStandingOrder`, and `CookOrders` already own production. `Pantry.TrySpend(float value)` pays fungible food value, which is appropriate for current generic feasts but does not guarantee that a specifically selected recipe was served. New selected-dish serving requires an atomic exact-resource spend adapter; a generic `TrySpend` call alone cannot prove attendance ate the named dish.

Freeze how authored IDs, invented IDs, resource names, and any supported renaming map together. Recipe renaming is not assumed to exist; the reference design must remain safe if it is added. No fabricated recipe IDs for foods without a recipe. A food-resource reference can remain a resource reference.

Preparation requires either an owned reservation or an all-or-nothing check at commit. Reservations need save state, cancellation, and explicit expiry handling. A resource loss between preview and commit must fail or downgrade according to a disclosed policy, never award the preview's full benefit.

Record units: ingredient amounts, finished portions, and nutritional food value are different quantities. Existing recorded use may continue influencing economic leanings; the new social event gives only one participation credit per committed occasion. Receiving another legacy `Eaten` callback cannot create a second social reward.

### Save/load — T01, A

`GameSnapshot.Schema` saves `CultureSystem._state`. `CultureSystem` is already in `LaterSystems`, permitting saves from before that system. `SaveOptionalField` is the established compatibility mechanism. `GameSnapshot` restores runtime units before resolving saved resource slots; culture references must resolve after this reconstruction.

`GameSnapshot.Restore` explicitly forbids Begin/Unlock/Discover calls while restoring: these would replay rewards. There is not currently an explicit culture post-restore invocation in the inspected restore sequence. Add a controlled idempotent rebind/derived-effect reconciliation hook after systems, world, and runtime resources are restored; do not rely solely on `Update` timing or `Awake` order. Reset transient cached application keys as needed, without clearing saved history.

Test a save predating culture, one with existing culture but no extension, and one with invented recipes. In each case also reload twice, then advance one Seventh: exactly one new tick's costs/rewards should occur. A new-game reset must remove stale subscriptions, runtime IDs, and cached previews from the previous game.

### World contact and migration — T02, B

`WorldPaths` exposes pathfinding/travel-cost helpers, not a ready-made cultural network. Build a small settlement adjacency snapshot from actual route topology and invalidate it on topology changes. Do not run pathfinding per tradition per frame.

`PopGrowthLogic` has `AdmitFoundingMigrants`, `AddMigrants`, and a waiting pool; a population delta alone does not identify origin or destination culture. Publish an admission occurrence at a confirmed admission, carrying whatever origin metadata exists. Unknown-origin arrivals can participate locally without retroactively inventing a distant nation. New provenance records must not replace the population ledger or change ration costs.

The pantry is global. Settlement participation is a cultural model; it is not proof that there are independent local warehouses or per-settlement demographic cohorts. Do not invent a full distribution simulation to make T02/T05 work. Show allocated servings and known participation, and label any abstraction plainly.

### Ruins and memorial ecology — T03/T07

`WorldSystem.AdoptRuinCivic` and `WhyNotAdoptRuinCivic` already exist. `Ruin.civicAdopted` and culture's `digestedRuins` have different purposes; reconcile through the existing commands and one explicit policy instead of resetting either flag. Decide whether a previously adopted civic leaves any remaining reform option and explain it. A spent option must not reappear after reload.

`CultureSystem.Reform` currently accepts a family and optional ruin ID; it does not derive the family from the ruin. T07 constrains inherited options using known ruin evidence while preserving a separately priced self-directed reform path. Unknown ruins should not imply a fabricated civic or moral history.

`WorldResources.MoodOf` reads strain; `DrawReason` reads bloom/district compatibility and exclusions; `SeedSource` checks physical seed or ecological sources. These are existing culture/ecology connections. T03 adds a memorial reference and, only where useful, an ecology-owned practice modifier. Do not maintain grief by damaging Composure. Do not bypass sterile bloom or species-population rules. Memorial UI without a new numerical ecological bonus is an acceptable first release.

### Festivals, time, and Legends — T04/T08

`WorldSystem.HoldFestival` prepares food then starts the world task; `FinishFestival` calls `CelebrateFestival` and awards Meaning fragments. `CultureSystem.CelebrateFestival` adds Unity, morale, joy, strain relief, presence, and records. Choose one completion owner for both old and new rewards. Wrapping this method and also granting an independent new festival reward is not acceptable.

Legacy holiday records use Seventh/Phase recurrence and an Echo dedupe key; keep these when migrating. Use absolute occurrence IDs and explicit recurrence for new schedules. Do not use a display name or culture-relative age as the sole uniqueness key. Culture history can predate new event recording, and game Age changes need not mean “new holiday occurrence.”

`LegendProgress.ShareExperience` and `LegendRelationshipRules.Experience` already implement directional records, meaningful-test progression, deduplication, and significant ties. Call them with a stable occasion ID. Ordinary participation uses a non-wounding, non-test encounter; an authored significant event may have stronger semantics only when justified by that event. A repeated successful performance is not permission to create seven new bonds.

### Accounts, history, and presentation — T09/T10, C

`AgeProgression.AgePassed`/`AgeBegan`, `LegendProgress.Lost`, `BalladJournal`, and `EraTimeline` are existing entry points. Historical facts may be learned later than they occurred; preserve both dates when known. Do not equate a missing record in an old save with proof an action never happened.

`CultureMoment` supplies text, kind, Age ID, and culture-relative Seventh; it has no stable evidence ID or participant list. Link legacy moments with clearly marked incomplete provenance. Do not parse localized titles to reconstruct reliable identities.

Use the established event scheduler and condition/effect routing. Separate account versions from evidence; public accounts may be mistaken without rewriting the actual world. The atlas reads query snapshots and delegates commands. It neither computes cultural rules nor stores a competing copy of history.

## Content floor for this scoped release

- T01: three initial tradition definitions, including one non-food practice, with all lifecycle states exercisable in fixtures.
- T02: two settlement profiles with different practiced repertoires, one road-contact case, and one completed cultural visit.
- T03: one crisis memorial, one named loss dedication, and one Age-passage preservation scenario.
- T04: one migrated Echo holiday, one annual/once-per-Cycle observance, and one location/calendar-gated gathering.
- T05: one public welcome and one targeted recovery gathering, with an authored and a custom food selectable.
- T06: one culinary teaching order and one nonculinary practice, with early informal and later gated institution definitions.
- T07: three authored inheritance/hybrid options, plus side-by-side preservation and an unknown-history fallback.
- T08: two repertoires with different intents, two casts producing explainable differences, and later magic locked behind its real prerequisites.
- T09: two short multi-choice disputes, each linked to recorded actions and supporting more than one viable resolution.
- T10: the three end-to-end scenarios in the main plan and a player-facing trace/preview screen.

These are coverage minima, not extra independent tasks. All new prose, numeric tuning, and mappings retain canon-status labels. Authored content must validate against current Age/technology IDs rather than introduce fictional unlocks just to satisfy a fixture.

## Handoff report required from every agent

List owned files changed, shared integration patches requested, actual tests run and outcomes, remaining known defects, one reproducible playable acceptance path, and any proposed canon changes. Keep unverified work labelled unverified. Agents should continue work on independent modules while shared integration patches are pending; they should not all modify the same registry to avoid waiting.
