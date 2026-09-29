# Gateway to Genesis — delivery roadmap

Reviewed 2026-09-27 against the current working tree and original vault. This is a planning update, not fresh gameplay certification. Preserve the established S01–S25, X01–X12 and WG01–WG13 identifiers. The detailed system backlog remains in [ROADMAP.md](../../Assets/GatewayToGenesis/Scripts/ROADMAP.md); the current execution overlay is [ACHIEVEMENT_INTEGRATION.md](ACHIEVEMENT_INTEGRATION.md), with a [97-entry coverage register](ACHIEVEMENT_COVERAGE.csv).

## Owner clarification

Legends may acquire Ornaments in Age 0. The vault prohibition is a typo referring to Ornamental Magic in Symphony of War and other symphonic cards; magic/card availability must be gated independently of legend development. Settlement Ornaments still require The Truth of Arcanoria and three complete Ages of presence. This explicit owner correction supersedes earlier blanket restrictions.

## Current baseline and priority

Story endpoint, 2026-09-27: [Settlement stories and Lesser Opus](SETTLEMENT_STORIES.md) adds four three-verse Ballads, twenty miscellaneous events and five triggerable issues through Ink. Finales automatically record Lesser Opus and show associated titles as locked future fragment purchases. Full legend progression and title spending remain the next task; S12/S15 are not complete. Compilation, branch and standalone history checks passed; Unity scene acceptance remains pending.

Accept the modernized world → story → legend → Age → save → achievement loop before expanding the campaign. World acceptance remains an immediate dependency, but its implemented foundations must not be scheduled as absent.

- Unity 6000.5.8f1; generator version 9; save schema 3. Quadrant and Macro Biome world generation, relief, regional weather, map lenses, claims, micro travel, settlements, roads and enclave influence exist at implementation/prototype depth.
- AgeProgression advances Desolation → Renewal → placeholder Age II. Pantry now supplies stored-food value, variety, spoilage and provisions. Hunger/Plague are prototypes requiring current acceptance, not unbuilt systems.
- Legends have souls, Composure, Ornaments, seven fragment kinds and fragment-based growth. Legend-led expeditions and Ballad casts/verse records exist. Durable deed identity, relationships, full legacy and Fate Stage integration remain open.
- Persistence is authorized and implemented: world saves, lifetime profile, per-world achievement/Anchor rewards, recovery and sacrifice API. Prestige upgrades, Ironman, migration policy and playable final-choice content remain open. See [SAVE_SYSTEM.md](SAVE_SYSTEM.md) for platform and compatibility limits.
- 97 definitions have 12 rule predicates. A separate direct profile award for The Purest of All Love exists in SaveSession.Sacrifice; its final story is not playable. The other 84 have no award implementation. No row is freshly play-certified in this update.
- X05 engineering landed 2026-09-27: EraUnlock evaluates Age reached/number and unknown requirements fail closed; AgeCapabilities separates legend Ornaments (Age 0) from Ornamental Magic (Age III). U00 award transaction, evidence envelope, aliases and register test landed the same day (see ACHIEVEMENT_INTEGRATION status blocks).

The tree contains extensive shared uncommitted work. Earlier compilation/test reports and the Editor-license block are historical evidence. X02 must establish today's baseline; do not discard unrelated edits or infer current acceptance from old reports.

## Phases and exit gates

### P0 — Reconcile contracts (U00)

Close X02–X04 around stable entity/event IDs, evidence lifetime, reward transactions, restoration and completion-set versioning. Exit: 97 unique register rows with owner role/wave, current baseline recorded, shared-file ownership agreed and one interruption-safe unlock demonstrated.

### P1 — Accept the first playable loop (U01)

Extend existing S02/S03/S04 and X05/X06. Resolve D01–D03, implement real Age gates, use Pantry in famine acceptance, and complete Age-type/history rules. Exit: prepared and unprepared famine routes both reach Renewal once; Plague is reproducible; legends can awaken in Age 0 while restricted Ornamental Magic remains unavailable; twelve existing rules and five Age-type additions have player-flow and restore evidence.

### P2 — Accept civilization on the world (U02)

Complete WG12/WG13, S08–S11 and X07 using existing world/civilization systems. Add origin-wound, founding, ownership, ruin and civic provenance. Exit: explore/found/connect/contact/lose/reclaim persists correctly; trade interruption and local weather travel work; full-Age tenure is explicit. Panthalassa requires two unrelated discovered identities, not matching display text.

### P3 — Integrate legacy and encounters (U03, then U04)

Build on fragment purses and casts for S12–S14, then S20/S21 and X08. Exit: deduplicated deeds, directed relationships, ascension, Constellation operations and a complete Symphony of War encounter produce attributable consequences and unlocks. Collective-origin Atonalis never invent a former-person Soul-Key. Card availability is independent of legend Ornament acquisition.

### P4 — Make society consequential (U05)

Integrate S16–S19, S24/S25 and X09. Build S17-A history primitives before S25-A consumers. Exit: civics, laws, public versus actual records, religion/Piety, forbidden magic, resonance and rival outcomes share one explainable persisted history. Whole-history achievements cannot be inferred from current state alone.

### P5 — Complete campaign and reset routes (U06)

Complete S04-B/S05/S06/S08-B/S15/S22/S23 and X10/X11 incrementally: Renewal content → II–VI/Second Reset → Third Reset through XI → Fourth Reset through XIII → hidden XIV/endings. Exit: each increment has playable route/resume evidence before the next; named reset stage differs from run count; Truth gates settlement Ornaments; purchases atomically grant permanent entitlements; Ironman has explicit rounding/recovery; D04 resolves final reward labels and the noncircular completion set.

### P6 — Certify every unlock (U07)

Close X12. Exit: every included achievement has production, negative, repeat/reload and player-flow evidence; aliases preserve historic IDs; spoilers, input accessibility and device budgets are checked. A predicate or asset alone never means complete. Do not claim 97/97 while any included row remains blocked.

## Immediate agent queue

1. U00: baseline, coverage and shared evidence/award contract.
2. U01: X05 Age/magic gates and saved-world verification; then final Age-type/history predicates.
3. U02: world acceptance/provenance; U03: fragment/deed/cast identities and legacy once shared contracts are stable.
4. U04 encounters and U05 society consume those outputs; U06 integrates campaign consequences; U07 certifies.

These are proposed ownership lanes, not dispatched agents. Claim file scopes and attach evidence through the synchronization protocol in ACHIEVEMENT_INTEGRATION.md. Canon-dependent tasks can prepare interfaces and fixtures, but cannot invent missing lore to close an achievement.
