# T01 completion report: living traditions and the culture's shared contract

Agent A, Wave 1, 28 September 2026. T02 (Agent B) and T03 (Agent C) build against the contract below. Enums are append-only. Change the contract only through Agent A, never by forking it.

## Owned files

**Shared contract, `Assets/GatewayToGenesis/Scripts/GameData/Culture/Integration/`:**

- `CultureContracts.cs` holds the vocabulary every task shares:
  - `CultureEntityRef`: a stable kind and id, plus a display label that is never used as a key.
  - `CultureIds`: helpers such as `Settlement(id)` and `Landmark(settlement, spec)`.
  - `CultureStamp`: the culture's own Seventh, plus the world's Cycle/Echo/Phase/Seventh and the Age id.
  - `CulturalOccurrence`: a committed action with a dedupe key, kind, subject, settlement, quantity and unit, actors and source.
  - `CultureCommandResult`: preview/commit results with a reason, what was paid, and the occurrences recorded.
  - `CanonStatus`: `ExplicitCanon`, `CanonSupported` or `NewGameRule`. It moved here from T02 when both tasks defined it.
- `CultureExtensionState.cs` is the saved envelope: a partial class in `CultureState.extensions`. Each feature adds its own `[SaveOptionalField]` in its own file (traditions here, `memory` in T03, `local` in T02).
  - It also holds the occurrence ledger, `CultureOccurrences`: pending, recent and accepted keys, deduped over a 126-Seventh window.
  - `CultureMigration` is idempotent. It creates the envelope for an older save, and it links a national food from an older save only as a legacy record, with no invented participants.
- `CulturePipeline.cs`:
  - `ICultureFeature`, discovered by reflection.
  - `CulturePhase`: SourceActions, Dedupe, LocalParticipation, TraditionLifecycle, SocialEffects, Notifications.
  - `CultureSeventh`, the context one Seventh runs in. Occurrences recorded during it join that Seventh.
  - `CultureReconcileReason`: Founded or Restored.
- `CultureQuery.cs`: `ICultureQuery`, a partial interface. It returns snapshots such as `TraditionView` and read-only previews, never mutable lists.
- `CultureEffectPolicy.cs`: one effect source per definition, named `Culture: {name}`. Sources are applied and removed as a set, so a reload never stacks one.

**Traditions, `.../GameData/Culture/Traditions/`:**

- `TraditionModel.cs`: definitions, triggers and effects; instances with origin, bearers, venues, links and a bounded participation history; `TraditionState`.
  - The stages are Emerging, Practiced, Dormant and Revived.
  - Recognition is separate from the stage: None, Recognized or Preserved, and a deferral.
- `TraditionTuning.cs` holds every number (all proposals) and the authored definitions.
- `TraditionRules.cs` is pure. It covers crediting (capped per Seventh), establishment, lapse and revival, and "firsts" remembered once. It also builds benefits: per definition, capped at `maxActiveBenefits`, never per settlement. It moves a fallen settlement's traditions into its ruins, links national foods, and writes the explanation text.
- `TraditionLifecycle.cs`: the orchestrator (`ICultureFeature`, phase TraditionLifecycle) and the notice text.
- `CultureSystem.Traditions.cs`:
  - The ledger (`Record`, `Observe`, `Stamp`, `NextOccurrenceKey`) and the pipeline run.
  - `AfterRestore`.
  - Adapters from existing actions.
  - The Recognize, Preserve and Defer commands with previews.
  - Queries, effects, and condition values.
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Traditions/TraditionPanel.cs`: the Traditions panel. T03 registered it in `CultureWindow`.
- Tests: `Tests/Editor/TraditionTests.cs` (16 pure tests) and `Tests/Editor/TraditionPlayTests.cs` (ClickerScreen).

## Shared patches (Agent A's integration files)

- `CultureSystem.cs`:
  - The Seventh records national foods at the table (`RecordTable`), then runs the pipeline after `LifeSeventh`.
  - The founding reconciles every feature.
  - An embraced national food gets its tradition record (`RecordNationalFood`).
  - `Value` answers the tradition targets.
  - The world's `SettlementFell` is hooked.
- `CultureSystem.Life.cs`: rites, festivals, holidays, landmarks and cooking each record one occurrence at commit, never at preview.
- `CultureLifeTuning.cs`: a new free rite, "tales" (Tales at the Hearth), which is the non-food gathering. `CultureSettings.cs` gained `traditions`.
- `GameSnapshot.Restore` ends with `CultureSystem.Instance?.AfterRestore()`, after the world, runtime resources and civics are restored.
- `ContentValidator.ValidateTraditions` checks ids, vault notes, Ages, technologies, families, trigger targets, effect shapes and thresholds.
  - It now also checks T03's `MemorialSuggestion` targets, which T03 requested.
- `GameValues`: the `culture` domain answers `traditions`, `traditions_recognized`, `traditions_dormant`, `tradition_choices` and `tradition:<id>`.
  - It now also has a `memory` domain, which T03 requested: `kept` (the default), `causes`, `dedications`, `morale` and `cause:<evidence id>`.
- Docs: `ARCHITECTURE.md` (contract, effect sources, condition targets, tests), `INK_FILES_GUIDE.md` (conditions), and Canon Gaps "Living traditions".

## Player-visible behaviour

- **Authored traditions** (the content floor is three, including one non-food):

  | Tradition | Label | Vault note | Fed by | Gate |
  |---|---|---|---|---|
  | Tales at the Hearth | New game rule | Civic.md | The "tales" rite | None |
  | The Evening Song | Canon-supported | Waltz Pillar.md | "song" rite, T02's local evening song, festivals (half weight), performances | None |
  | The Naming of the Lost | Canon-supported | The Inescapable Hunger.md | The "remembrance" rite, T03 Memorial occurrences | Rites of Harvest |
  | The Ash-Loaf Table | Canon-supported | The Inescapable Hunger.md | Ash-Loaf baked (half weight), served, or dedicated | Echoes of Hunger |
  | The National Table | New game rule | Civic.md | Linked only, never arises alone: one record per national food, kept while the food is at the table | None |

- **Lifecycle:**
  - A practice emerges where it happened, with its origin occurrence. Only the first occurrence becomes the origin; later ones join the history.
  - It becomes a custom after 3 occasions spread over at least 3 distinct Sevenths. One burst is not a custom.
  - It lies dormant after 42 Sevenths without practice, or 84 if preserved.
  - 2 Sevenths of practice revive it. An emerging practice that lapses emerges again rather than "reviving".
- **Choices:** once a practice is a custom the player is offered three options. None is ever taken automatically.
  - Recognize it for the nation: it costs Unity (25–30), paid only on commit, and adds the recognized benefit (a pillar point, or max morale for the Ash-Loaf Table).
  - Preserve it locally: free, and it lapses at half speed.
  - Defer: nothing is spent, and it is offered again after 21 Sevenths.
  - Each option has a preview with the reason it is unavailable.
- **Benefits:**
  - Practiced traditions give a small effect of their own (for example Research +3%, max morale +1).
  - There is one source per definition, and at most 4 apply at once.
  - Lived traditions lean the ten-family drift toward their family at 25% of a rite's weight. The ten-family vector stays the aggregate description; no family bonus is multiplied.
- **Where it shows:**
  - The Culture window's Traditions panel shows each tradition's stage, where it lives, its origin, bearers, venues, bounded history, the explanation "because…", progress, benefits and the three choices.
  - Notices and chronicle moments mark emergence, becoming a custom, recognition, lapse and revival. Each is a "first" and is recorded once.

## Save and edge behaviour

- **Reload determinism:** the same history gives the same stages, benefits and explanation after a save round trip (`TheSameHistoryGivesTheSameTraditionsAfterASave`).
  - `AfterRestore` drops transient caches, runs the idempotent migration, reconciles each feature, and re-applies effects from saved state. It grants no reward, replays no story, and sends no notice.
- **Duplicate events:** every occurrence carries a key, for example `rite:<id>:<count>`, `festival:<settlement>:<count>`, `holiday:<name>:<echo key>`, `cook:<recipe>:<serial>`, `table:<food>:<seventh>` or `landmark:<id>`.
  - The ledger accepts a key once, including across a save (`TheLedgerCountsOneActionOnce_EvenAcrossASave`).
  - A legacy `Pantry.Eaten` callback creates no social participation.
- **Independent instances:** three instances in three places keep their own origins, histories and bearers (`ThreeInstancesInThreePlacesKeepTheirOwnCauses`).
- **Dormancy:** it removes the benefit source and keeps the whole record (`DormancyStopsTheBenefitsButKeepsTheRecord`).
- **Revival:** the benefit returns. Chronicle "firsts" and recognition are never repeated (`RevivalRestoresTheBenefit_AndNeverRepeatsItsFirsts`, `RecognitionIsNeverDecidedByAThreshold_AndIsOncePerDefinition`).
- **Old saves:** they load with an empty tradition history. Their national foods get linked records marked legacy, with no participants and no invented occasions (`AnOldSaveLoadsWithoutInventingHistory_AndItsNationalFoodsGetHonestRecords`).
- **Missing references:**
  - A settlement that falls moves its traditions into its ruins record (`fallen`, `formerPlace`), so a later settlement that reuses the id inherits nothing.
  - An unknown definition id in a save is kept but not applied.
  - Technology/Age gates stop new traditions from arising and never delete existing ones.
- **Existing flows:** rites, festivals, holidays, cooking, landmarks and national-food adoption behave exactly as before. The adapters only record the occurrence; the old rewards keep their single owner.

## Tests actually run

- Private compile (`mkbuild.sh`, runtime and editor projects), after the shared patches above: 0 errors.
- Offline reflection runner, pure suites: `TraditionTests` 16/16, `CultureMemoryTests` 17/17, `LocalCultureTests` 14/14, `CultureInventionTests` 6/6, `CultureLifeTests` 31/31 (plus 5 that need the Unity runtime).
- Unity batchmode EditMode, 17:25 run (before the two new patches): all 96 culture tests passed, including `TraditionPlayTests` 1/1, `CulturePlayTests` 1/1 and `CultureLifePlayTests` 2/2. `ContentTests` and `SaveTests` passed 20/20.
- Unity batchmode EditMode after the `memory` domain and memorial-suggestion validation (culture, tradition, memory, content and save suites): 134/136.
  - `TraditionPlayTests` failed with a NullReferenceException. The Traditions-panel check added at 17:26 had lambdas capturing the coroutine's locals, and that closure is lost at EnterPlayMode.
  - The fix moves the check into the static `CheckPanel` helper. On rerun, `TraditionPlayTests` and `ContentTests` passed 11/11, so `ContentTests` also validates the new memorial check.
  - The other failure is T02's `LocalCulturePlayTests`: `HamletSite` found no hamlet site on this world ("Sequence contains no elements"). It is left to Agent B.
- Not covered by a dedicated test: the `memory` condition domain. It is compiled and read through T03's public queries only.

## One acceptance path

1. Start a game and found the culture (Culture.ink).
2. Hold "Tales at the Hearth" (free) in three different Sevenths.
   - The first time, a notice says a practice emerges, with its origin.
   - The third time it becomes a custom, and the Traditions panel offers Recognize (25 Unity), Preserve or Decide later.
3. Preview Recognize: the panel shows the cost and the benefit (+1 Aureus while it is kept, on top of Research +3%). Nothing is paid until you confirm.
4. Save and reload, then advance one Seventh. The tradition, its history and the benefit are unchanged, and nothing is paid or granted twice.
5. Stop holding it. After 42 Sevenths it lies dormant: its benefit leaves the Research and pillar breakdowns, and the record stays.
6. Hold it in two Sevenths: it revives. The benefit returns with no new chronicle entry, and recognition is not offered again.

## Remaining defects and proposals

- All thresholds, caps, costs and benefits are proposals (Canon Gaps "Living traditions"). The lifecycle is a new game rule.
- The Evening Song listens to T02's `local:evening-song` activity. If T02 renames that id, the trigger silently stops matching. The validator skips `local:` ids on purpose, because T02 builds them at runtime.
- Recognition costs Unity only. Recognition through a council or edict route is left to T06/T09.
- Waiting for later waves: T04 holidays/calendar, T05 exact-dish serving with reservations, and T08 performances with Legend participation (`LegendProgress.ShareExperience` with a stable occasion id).
  - The `Hospitality` and `Performance` occurrence kinds and triggers are ready for them.
  - No current action emits them except T02's local gatherings.
