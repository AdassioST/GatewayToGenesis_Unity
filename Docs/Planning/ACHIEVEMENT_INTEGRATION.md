# Achievement unlock integration and agent handoff

Reviewed 2026-09-27 against the live working tree and original Arcanoria vault. This execution overlay preserves the established S01–S25, X01–X12 and WG01–WG13 identifiers. U00–U07 below are integration waves, not replacement system IDs. [PHASED_ROADMAP.md](PHASED_ROADMAP.md) owns sequencing; [ACHIEVEMENT_COVERAGE.csv](ACHIEVEMENT_COVERAGE.csv) routes every imported achievement. Suggested roles are unassigned; this update has not dispatched or messaged other agents.

## Owner clarification: Ornaments versus Ornamental Magic

The owner clarified on 2026-09-27 that the vault's Age 0 prohibition is a typo: **legends can acquire Ornaments in Age 0**. The restriction concerns **Ornamental Magic in Symphony of War and other symphonic cards**, not legend development. This explicit direction supersedes the contradictory vault sentence and earlier roadmap criteria. Do not block Motif Awakening or its achievements merely because the current Age is Desolation. Gate the corresponding magic/card capabilities independently. Settlement Ornaments retain their separate canon requirements: The Truth of Arcanoria and three complete Ages of settlement presence. The original vault has not been edited by this roadmap update.

## Verified implementation inventory and limits

- Unity 6000.5.8f1; `WorldGenerator.Version = 9`, `SaveDocument.version = 3`. Quadrant and Macro Biome generation, relief/drainage, lenses, claims, micro travel, settlements, roads, enclave influence and regional weather have implementation. Earlier version-6/version-1 prose describes earlier snapshots, not current compatibility.
- `AgeProgression` calls `GameAge.Set`; Hunger and Plague carry the prototype through Age 0, Renewal and placeholder Age II. `Pantry` provides food reserves, variety, spoilage and provisions. Do not rebuild these as hypothetical future systems.
- `LyricalFragments` supplies seven fragment kinds, binding growth and the provisional Underdog rules; `LegendProgress` awards fragments and records deed text. `BalladActors` supplies casts and verse records. Stable instance/event identity, full legacy lifecycle and the Fate Stage still need integration.
- Saves, independent lifetime profile, per-world achievement/Anchor ledgers, backup recovery and sacrifice support exist. Persistence authorization supersedes the old hold. Windows/account-bound keys and exact generator/catalog matching constrain compatibility. Prestige upgrades, Ironman and playable final-choice content remain open.
- There are **97 imported definitions and 12 AchievementTriggers predicates**. `SaveSession.Sacrifice` separately grants `the-purest-of-all-love` directly to the lifetime profile. That API is not a thirteenth accepted gameplay achievement: the final story is not playable. The other 84 definitions have neither predicate nor that direct award path.
- `CivicRequirement.ToCondition` now maps `EraUnlock` (2026-09-27, see U01 status); legend Ornaments are not Age-blocked.

These are source-inspection findings, not fresh gameplay certification. Existing documents report earlier standalone checks and an Editor-license block. U00 must capture current compilation, tests and license availability. Preserve the substantial shared uncommitted work.

## Meaning of unlockable and register status

An achievement unlock requires a real, canon-appropriate player action, world/lifetime recording, existing toast/Library/menu presentation and the configured world Anchor award. It does not imply a unique item reward per achievement. Permanent upgrades and gameplay content gates remain separate authored systems.

The register preserves source order, runtime slug, title, exact canon requirement, system dependencies, wave, suggested owner and wiring status. `predicate-wired` means one of the twelve rules exists, not that its player flow passed. `direct-profile-only` identifies the sacrifice path. `planned` means no award rule was found even where supporting mechanics exist. All rows start with fresh acceptance pending. The malformed “Critically Thinking Hater” entry has no requirement and is excluded by the parser; it needs canon content before joining the catalog or completion set. The imported Achievement note and original vault note had identical SHA-256 hashes during this review.

## U00 — Baseline, evidence and award contract

Owner role: integration/persistence/QA. Prerequisites: none. Maps to X02–X04 and S01-A/B. Status: ready to claim.

1. Capture current Unity compilation, relevant EditMode/PlayMode results, seed, generator/catalog/schema and working-tree snapshot. Record blocked tests explicitly; earlier test claims are not evidence for today's tree.
2. Freeze a proposed evidence envelope: event ID, world/run ID, Age instance/Act ID, subject/target identity, cause ID, payload version and committed outcome. Existing AchievementEvent has only signal/amount/flag/chorus; add reviewed typed payloads instead of overloading these fields indefinitely. Use stable identities rather than display names.
3. Emit evidence after successful action commitment. Persist deduplication keys with domain history. Tracker's once-per-achievement set does not deduplicate fragment awards, purchases or source actions. LegendProgress.Award appends deed text; text is not a unique action ID.
4. Reconcile Tracker, world reward ledger, lifetime profile and UI on interrupted awards. Achievements.Report mutates Tracker before SaveSession.Earn; Earn writes the profile while the world normally saves later. Test failures between steps, retry, restart and explicit backup recovery. D11 defines the recovery policy; do not assume cross-file atomicity.
5. Freeze achievement IDs and alias/migration policy before vault titles change. IDs derive from titles, and Tracker.Restore drops unknown IDs. Restoration must not emit gameplay evidence or mint currency. Validate duplicate aliases, orphaned IDs and removed content.
6. Add concrete producer, history fields, test evidence and verification date to each register row as work lands. Preserve exact requirements and distinguish predicate, producer, persistence, presentation and gameplay acceptance.

Exit: baseline recorded, 97 unique rows reconciled, shared contracts accepted by implementing owners, and one representative unlock survives interruption/reload without lost or duplicate rewards.

### U00 status — 2026-09-27 (session "Reconciling issues work review")

**Baseline (X02, engineering half).** HEAD `3847c22` plus the shared uncommitted tree (≈385 changed paths, several peer sessions active). Compile: clean with the Editor's bundled dotnet (0 errors; 6 CS0618 `FindObjectsSortMode` warnings in GameSnapshot/SaveMenu). Unity 6000.5.8f1 batchmode EditMode run, Editor license available: **488/488 passed**, including the seven scene tests (GenesisLoop, BalladActor, Expedition, LegendSoul, Notification, TechTree, WorldView PlayTests). A mid-edit peer change briefly broke compilation (`GameValues.cs` missing `using System.Linq`) and was fixed by its owner within a minute; baselines taken while peers edit must record the time. Not covered: the manual Capital smoke test (economy, pause, population, council, events, Library and achievement UI by hand) and device profiling.

**Award transaction (implemented).** `AchievementAward.Commit` is now the only award path: world reward ledger (Anchors + evidence, in memory) → lifetime profile merge and write → Tracker mark → toast. Each step is idempotent. If the profile write fails, the award is logged and left unannounced. The same happening, or the next world save (which merges the ledger into the profile), then completes it without paying twice. If the game crashes before the world save, the award is lost together with the happening that earned it. Replaying that happening pays the world once, and the profile union stays single. Loading rebuilds the Tracker from the ledger and never emits evidence. `AchievementContractTests` demonstrates failure → retry → crash → reload → re-earn → reload on the real ledger types and `SaveStateCodec` round trips. This is a logic-level demonstration; an in-scene save/kill/reload run is still owed to U07.

**Evidence envelope (implemented, version 1).** `AchievementEvent` gains `source` (stable key of the committed happening) and `subject` (stable entity id). Awards store an `AwardEvidence` record in `WorldRewards.evidence`: signal name, source, subject, Age id/number, UTC, version. There is at most one record per award. Older saves load with none, so the save schema is unchanged at 3. All eight producers now attach keys (`age-passed:<ageId>:<pass>`, `chorus:<knot>:<choice>`, `ledger-revision:<knot>`, `council:<seated>`, `library:opened`, `composure|awakening|abyss:<legend>:…`). The Age pass now reports after the next Age has begun, not mid-transition. Legend subjects are names until U03 introduces instance ids; deduplication of non-achievement domain actions (fragments, deeds, purchases) remains with each domain's history (U03/U06).

**IDs, aliases and completion set (implemented).** `AchievementAliases.Renamed` maps retired title slugs to current ids. World ledgers are migrated on `SaveSession.Read` (a late alias merges duplicate awards and keeps the Anchor sum, so migration never mints or removes currency), and profiles on load and backup recovery. `Tracker.Restore` follows aliases and returns unplaceable ids, which the load path logs instead of silently dropping (their Anchors stay in the ledger). `AchievementCompletion` v1 is every note achievement except `gateway-to-genesis` itself, reported against a named version. Whether #96 and exclusive endings belong to the same final transaction remains D04/D11.

**Register (reconciled).** `AchievementRegisterTests` keeps ACHIEVEMENT_COVERAGE.csv equal to the imported note: 97 unique ids in note order, exact requirements, a wave and owner on every row, `predicate-wired` exactly for the 12 rule ids, `direct-profile-only` only for #96. It also checks alias sanity and the completion-set size. The 12 wired rows and #96 now name their producer and test evidence. Every row stays `fresh-gameplay-evidence-pending`.

**Canon qualifiers found while wiring evidence.** Heads, You Vanish ("high-stakes decision") has no stakes check: any Critical Failure qualifies. An Eccentric Madman fires on the first Library open, which the story treats as meeting the Archivist. Both need a design ruling in U01 before acceptance.

**Proposed shared-file ownership (not yet agreed by the peer sessions).**

| Scope | Owner lane | Rule for others |
| --- | --- | --- |
| `Achievements/*`, `SaveData.cs` reward ledger, `SaveSession.Earn/Read`, ACHIEVEMENT_COVERAGE.csv | U00 integration | Additive requests only; a new signal/payload field lands with its register row and test |
| `GameSnapshot.Schema`, `SaveDocument` | U00 integration | Domain owners propose fields; one owner serializes schema changes |
| `GameValues` resolvers, `EventContentCheck` | shared | Additive `Register(...)` lines only; never repurpose a domain |
| `AgeCapabilities`, `AgeProgression` transition order | U01 | Consumers query capabilities; nobody infers magic from legend Ornaments |
| Producers (`LegendProgress`, `WorldSystem`, `EventSystemLogic`, …) | their domain lane | Report only after commit, with `.From(source, subject)` |

## U01 — Existing awards and Age history

Owner role: Age/canon/achievements. Prerequisite: U00. Maps to X05/X06, S02-A/B, S03-A/B and S04-A. Decisions: D01–D03.

- Reproduce all twelve existing predicates through their producers: LibraryOpened; the three ChorusResolved outcomes; DeathLedgerRevised; CouncilChanged; AgeSurvived 0/1; ComposureFell; MotifAwakened; CatalyticAbyss. Check full council identity, active world and restoration behavior. Check canon qualifiers such as high-stakes decisions and meeting the Archivist against the actual player flow rather than assuming the signal name proves them.
- Implement real EraUnlock and shared Age availability. **Legend awakening remains permitted in Age 0**; separate Ornamental Magic/card eligibility under the owner clarification. Do not let a legend's Ornament bypass the magic restriction.
- Add entered/final historical Age-type evidence for Metanoia, Anagnorisis, The Darkest Hour, Ignis Aurum Probat and The Strauss-Howe Prophecy. D02 must settle final versus transient classification and collection lifetime.
- Validate Hunger with the existing Pantry and different survivor outcomes that both reach Renewal. Arrival at placeholder Age II cannot substitute for surviving Age II or inviting Lacrimosa.

Exit: positive and near-miss tests for five type predicates, current player-flow checks for twelve existing rules, Age/capability eligibility fixtures, and a saved-world playthrough. Preview/load/repeated transitions grant nothing twice.

### U01 status — 2026-09-27: X05 engineering landed; D01–D03 still gate the rest

- **EraUnlock is real and fails closed.** A civic `EraUnlock` naming an Age id is met once the world is in that Age or has passed through it (new `age_reached` value: the current `GameAge` or `AgeProgression.History`). An empty target compares the current Age number. `CivicRequirement.IsMet` no longer passes an unknown requirement. Unknown Age ids and unknown capabilities fail content validation, and requirement tooltips show "Needs <Age title>". No civic asset uses EraUnlock yet, so this changes no current content.
- **Capabilities are independent (D13).** `AgeCapabilities`: `legend-ornament` from Age 0; `ornamental-magic` from Age III, the Ages.md "starting point of proper Ornamental Magic". Ages I–II are unspecified, so it stays locked there pending D10. Content gates via the `capability` condition value. Motif Awakening and its three achievements do not consult the Age. Settlement Ornaments are deliberately not a capability (U02/U06 Truth + three-full-Ages gate).
- **Still open, decision-blocked:** Act timing (D01), Age-type/history classification for Metanoia, Anagnorisis, The Darkest Hour, Ignis Aurum Probat and The Strauss-Howe Prophecy (D02), and the famine survivor floor and prepared/unprepared contract (D03). No Ornamental Magic cards exist yet, so the card-side check waits for U04 content. Technologies drive the Age (its gates wait on research) rather than being Age-locked, keyword cards already read the Age, and no technology or section asset carries an Age requirement to convert. Player-flow evidence for the 12 rules needs the manual/in-scene runs.

## U02 — World provenance and discovery

Owner role: world/civilization. Prerequisites: U00, U01 availability contract. Maps to WG12/13, X07, S08-A, S09-B, S10-A/B, S11-A and S16 inheritance hook. Decision: D07.

- Extend WorldCivilization/WorldSystem, preserving settlements, routes, claims, CityDevelopment and weather queries. Finish route interruption/nexus behavior and real loss/reclaim before awarding ownership achievements.
- Record founding time, full-Age intervals, former ownership, loss cause, ruins and civic provenance. `agesPresent++` at a boundary cannot prove three entire Ages for a settlement founded mid-Age. D07 resolves loss/reclaim/reset continuity.
- Panthalassa compares two discovered unrelated enclave identities with a canonical origin-wound ID. Family, name or nearby placement alone is insufficient.
- Emit committed inheritance/loss/reclaim evidence for Digestive Rebirth, 404: City Not Found and Welcome Back, Traitors. Share the civic hook with U05 rather than implementing civics twice.

Status 2026-09-27: full-Age tenure is now explicit. `Settlement.fullAges` counts only Ages a settlement stood through from start to end; `standsSinceAgeStart` is true for the Capital from the world's birth and for others from the next Age. `WorldCivilization.AgePassed` replaces the bare `agesPresent++`, and `Settlements_OnlyWholeAgesCountTowardTenure` covers it. Loss/reclaim continuity is still D07; founding, ruin and civic provenance events are not started.

Status 2026-09-27 (later): **loss and ruins landed** (owner direction: anything but the Capital can be lost, pillaged or damaged; orphaned tributaries with no road to a hub wither; the Capital can be profoundly damaged but never lost; ruins stay on the map and are investigated for what the failure left behind). `WorldRuins` (pure, `WorldRuinTests`) and `WorldSystem.Ruins`: settlement damage from danger, withering and the new `settlement:` story consequence; falling into a saved `Ruin` record (former kind, district, binding, founding/fallen Age, full Ages, development, detached, cause); investigation by an expedition (salvage, Research, Enlightenment, a civic left for adoption). Wired: **Digestive Rebirth** (`RuinCivicAdopted`, reported by `WorldSystem.AdoptRuinCivic` after `CivicManager.UnlockCivic(..., inherited: true)` commits, source `ruin-civic:<ruinId>:<civic>`) and **404** (`SettlementLost` with flag = stood beyond your Administrative Authority, reported after `WorldRuins.Fall`, source `settlement-lost:<settlementId>`). Still open: the "reform a culture" half of Digestive Rebirth (U05/S16 culture reform; another agent is building that system: its producer should report `RuinCivicAdopted`'s sibling for a culture reformed from ruins rather than a second rule), and fresh gameplay evidence for the rows. Later the same night: settlement damage became **Composure** (the legends' five states), and **Welcome Back, Traitors** is wired (`SettlementReclaimed`, reported by `WorldSystem.Reclaim` after a settlement or tributary is founded on the ruins of one of yours, source `settlement-reclaimed:<ruinId>`; +3 Era Score).

Status 2026-09-28: the owner added **There is Beauty in That** to the vault ("Witness the first birth of your [[Civilization]]", Culture, Civics & Civilization, before Soul Leitmotif of Civilization); the imported note now has 98 achievements, so every later row number moved up by one (Purest of All Love is #97, Gateway To Genesis #98). Wired: `PeopleBorn` (births since the founding), reported by `PopGrowthLogic.OnDemographicSeventh` after calendar births are added, source `births:<total>`. Founding survivors and other arrivals are migration and never report it (Docs/Planning/POPULATION_GROWTH.md).

Exit: explore/found/connect/contact/lose/reclaim sequence passes identity, resources, save and unlock checks. Fully Ornamental settlements and Kenotaph wait for U06 Truth/legacy integration; promotion and map anchors alone do not award them.

## U03 — Fragments, relationships and Stellar Legacy

Owner role: legends/legacy. Prerequisites: U00, U01 capability contract. Maps to S12-A/B, remaining S13-A/B, S14-A/B. Decisions: D08/D12.

- Reuse FragmentKind, LegendProgress and BalladCast/Record. Add stable legend instances and deduplicated deeds before ascension, relationships, Opus and mimicry consume them. Verify snapshot coverage rather than inferring it from serializable types.
- Add atomic multi-fragment spending, Opus recipes, Underdog origin provenance, Myth Grade, drift, purification, syncretism and liberation. Remove active effects once on ascension and retain identity.
- Implement directed relationships, reciprocal bonds and death/severance history. One-sided Infatuation must not imply reciprocity.
- Canon gaps block maladaptive/toxic classification, middle-to-Apex mapping, mastery stars and Weight of Potential thresholds. Do not infer trait categories from their names.
- D12 opportunity: current Underdog rewards are ×2; Ironman canon adds 50%. Multiplicative stacking could satisfy ×3 on a single action. This is a proposal, not settled canon. Record base/actual awards, mode and against-odds evidence; do not change the multiplier to 3 just to fire the predicate. U06 supplies Ironman.

Exit: legacy operations have success, insufficient-fragment, wrong-identity, repeated-event, death and restore fixtures; one playable ascension. Slayer Opus and Spellweaving mastery wait for U04; the ×3 row waits for U06/D12.

## U04 — Symphony of War and Atonalis

Owner role: combat/encounters. Prerequisites: U03 foundations and U02 discovery hooks. Maps to S20-A/B, S21-A/B, S11-B, S19-B foresight hook and X08. Decisions: D08/D10.

- Prototype a complete composition/performance duel before mass-authoring cards. Gate Ornamental Magic and other affected symphonic cards by their own availability rules, independently of a legend acquiring Ornaments. Test an Age 0 ornamented legend with restricted magic still unavailable.
- Record fight IDs, attempts, timing grades, note-stack size, pre-cancel Composure, interference corrections and committed outcomes. Define Mythical Victory and Primal Discordia through authored rules.
- Model Nascent/Sectile/Fracted/Ascendant and distinct origins. Existing surrender/loss is not proof of Dissonance Core transformation. Feeding, dead-legend mimicry, seed stage/trigger-object provenance, last words and Soul-Key liberation need separate outcomes.
- Require at least one foresight attempt for “lose every attempt”; empty lists cannot qualify. The 15+ all-Perfect cancellation and 70%+ spell cancellation need that fight's measurements.
- Here Be Monsters requires a ruined Landmark and an Atonalis encounter, not a threat marker. Goodbye, Mother also waits for U06's actual Eyras/Cradle finale.

Exit: played encounter plus win/loss/retreat/replay fixtures with attributable fragments, deaths and awards. Collective-origin Atonalis cannot invent a former-person Soul-Key; incompatible kill/liberation rewards cannot both pay.

## U05 — Society, law, faith and public history

Owner role: society/history. Prerequisites: U00, U02 provenance and U03 identities; attack-dependent outcomes wait for U04. Maps to S16–S19, S24/S25 and X09. Decisions: D09/D10.

- Preserve starting civic IDs and tenure. “Over two Ages” is strictly over two; a modernization event must reform that qualifying civic. Ordinary replacement does not qualify, and initial Cusp/Dissonance civics do not count as later adoption.
- Build S17-A evidence primitives before S25-A rival consumers. Separate actual deaths from revised official records; retain exposure evidence, past laws, atrocity offers/refusals and campaign boundaries. No recorded atrocity is not proof of refusing every opportunity.
- Connect Sacred Sites/Prophet founding from Arcanoria.md to religion. Stable Piety without Prophet civics is distinct from founding without a Prophet actor. D09 defines the observation window.
- Reuse existing resources for Dual Confluence and record imbalance/recovery. Regional weather does not already provide an independent per-city economy. Forbidden-law tenure must connect legalization to an actual Atonalis attack.
- Justification, ledger exposure, failed cover-up revolt, condemnation, heresy response and unspeakable acts need actor/target/cause evidence. Queuing a story or encounter cannot substitute for its outcome.

Exit: reproducible society/rival story with history, law, resource and save evidence. Whole-campaign refusal also waits for U06's defined completion boundary.

## U06 — Truths, campaign, upgrades and endings

Owner role: narrative/campaign with persistence integration. Prerequisites: accepted U01–U05 contracts; deliver incremental campaign gates. Maps to S04-B/S05/S06/S08-B/S15/S22/S23 and X10/X11. Decisions: D04–D08/D11/D12.

- Reuse Ballad casts/verses and Ink rewards; add Fate Stage Major Actor acquisition/loss/reclaim and terminal outcomes. Protagonist selection is not automatically Major Actor status.
- Add spoiler-safe truth IDs/discovery/sharing and a versioned all-truth set. Ordinary Memory Field discovery does not grant The Truth or every truth.
- Author Renewal content, II–VI/Second Reset, Third Reset through XI, Fourth Reset through XIII, then hidden XIV. Distinguish crisis resolution, named reset stage, numeric run count and master-key tier. Include the Age of Silk from Ages.md rather than treating three placeholder Age II assets as exhaustive.
- Reuse Anchor awards/SpendAnchors; add approved upgrade IDs, costs, owned effects and atomic debit-plus-entitlement. Debit success alone does not grant Compost Your Failures. Decide reset carryover versus new-world behavior; prestige currency and map anchors are separate.
- Create Ironman mode policy, crash recovery, +50% rounding and D12 stacking. Its crisis achievement requires completed resolution.
- Gate settlement Ornaments on The Truth and three complete Ages; Kenotaph requires a specific tragically lost legend/Constellation. Neither restriction applies to a legend's own Ornaments.
- Preserve the authorized sacrifice seam and cosmetic-only restored-copy behavior. Author the final choice through the existing Ink binding. D04 must reconcile branch labels/rewards; it does not reimpose the persistence hold. Integrate #96's direct profile award with presentation and completion without duplicate rewards.

Exit: each campaign increment passes playable route and interruption/resume checks. Final completion is versioned, excludes #97 itself, resolves whether #96 is awarded by the same final transaction, and defines mutually exclusive routes and lifetime versus world scope. Do not gate on every row in an unversioned changing catalog.

## U07 — Full unlock acceptance

Owner role: QA/integration. Prerequisite: earlier accepted outputs. Maps to X12.

For each row attach producer, predicate/exceptional award path, saved evidence, presentation and reproducible test results. Check success, near-miss, wrong Age/reset/mode/identity, duplicate delivery, preview, restoration and relevant write failures. Test hidden content against tooltip/search/requirement leakage; unknown IDs fail validation visibly. No 97/97 claim while any included row is blocked or merely wired.

Use the relevant existing achievement, content, Genesis loop, save, world, expedition, legend soul and fragment suites where present, plus new behavior tests. Record actual suite names and results; compilation does not establish Play Mode or visual acceptance. Finish device profiling and timing/accessibility acceptance.

## Agent synchronization protocol

- Claim a wave/task and exact file scope before editing. Record agent/chat, base revision, shared uncommitted input, contracts and dependencies. Roles above are not agent assignments.
- Integration owner serializes AchievementTriggers/AchievementEvent, GameSnapshot/SaveData, shared enums/catalogs and the coverage register. Domain agents own their producers, content and tests; coordinate additive API changes before merging consumers.
- Handoff: task/owner; files; event/payload example; canon decisions; schema impact; achievement IDs; executed positive/negative/reload checks; blockers; next consumer.
- Update wiring and verification independently. Preserve unrelated work, public IDs and source imports. Do not regenerate vault content or edit unrelated scenes as incidental cleanup.

## Missing opportunities

Spiraling Conscription and the Opus hooks “An Expedition Through the Impossible” and “The Expedition's Last Survivor” can connect existing expedition hardship, Composure and fragments under U03/U05 without inventing achievements. A Burrowed Name is a canon story opportunity for U06. Sacred Sites connect map and religion; origin wounds connect enclaves and Panthalassa; persistent casts connect exploration and legacy. Complete these joins before adding parallel progression systems.

Evidence: original Achievement.md, Ages.md, Age of Desolation.md, Arcanoria.md, Cataclysmic Aftermath.md, Gateway To Genesis.md and Combat System.md; [source register](SOURCES_AND_DECISIONS.md); [import gaps](../../Assets/Resources/Library/Vault~/Canon%20Gaps.md). Proposed engineering rules are not new canon; the owner's Ornament clarification takes precedence over source wording.

## Planning validation, 2026-09-27

Checked 97 unique assigned IDs, all twelve current predicate IDs, one direct-profile award and 84 planned rows. Retained all 62 S/X task headings. Local file links in the roadmap, phase plan, handoff plan, task navigation and decision register resolve. The original/imported achievement notes match SHA-256; git diff whitespace checks pass. These checks validate the documentation and coverage, not C# compilation, Unity tests, gameplay reachability or the proposed APIs.
