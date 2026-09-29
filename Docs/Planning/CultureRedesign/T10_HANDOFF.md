> Historical handoff/baseline. Superseded by [FINAL_REPORT.md](FINAL_REPORT.md) and [T10_REPORT.md](T10_REPORT.md). Earlier license and missing-atlas/observance-bridge notes describe the pre-T10 state.

# T10 final dispatch — cultural atlas

Read [WRAPUP_REVIEW.md](WRAPUP_REVIEW.md), [T09_REPORT.md](T09_REPORT.md), and [T10.md](T10.md). T01–T09 are integrated in the current working tree. The old INTEGRATION.md is the pre-implementation design baseline; verify present signatures in code. Keep the active recipe implementation and all unrelated edits.

## Deliverable and ownership

Build the atlas in `Assets/GatewayToGenesis/Scripts/UI/Culture/Atlas/`, with a `CultureAtlasReadModel` that composes snapshots. Integrate it into the existing CultureWindow, HUD and appropriate world-card navigation. Reuse the kitchen and recipe editor. Add atlas tests and integrated scenario fixtures. Do not rebuild domain rules in UI or mutate CultureSystem.State to make a button work.

Use one selected entity and clear paths to its origin, evidence, public accounts, parent practices, local practice, carriers, venues, dishes and next decisions. Display unavailable links as unknown, missing or historical with their recorded label. Never infer origin from a culture name, ingredient mix or world truth hidden from the player.

## Stable inputs already available

- T01: `ICultureQuery.Traditions`, `Tradition`, `TraditionsAt`, pending choices and recognition/preservation/deferral previews. TraditionView includes typed subject/venue/link references, participation, stage and benefit explanations.
- T02: local profiles, contact edges, arrivals and carried practices from the Local partial query contract. Use recorded origin and former profiles, not settlement ID alone across historical generations.
- T03: `MemoryCauses`, `MemorialDedications`, `MemoryLineages`; join dedication evidence IDs and typed target references. Keep evidence separate from account text.
- T04: CultureSystem's `Observances`, `ObservanceOf`, preview/configuration/preparation commands. Snapshot methods exist on the concrete system; do not assume every one is declared on ICultureQuery.
- T05: table/access snapshots and `PreviewTable`/`SetTable`; preserve the shared serving boundary. Recipe references resolve through `IRecipeCultureRead`.
- T06: `TeachingOrders`, `TeachingInstitutions`, `PracticeVariants`, `WrittenRecords`, `CarriersOf`, `PreviewTeaching`. A written record is not a living bearer. Teaching variants and T07 variants have different types and meanings.
- T07: inspect CultureSystem.Syncretism's current query/preview methods for hybrid offers, decisions, variants and ruins. Preserve/adapt/replace/revive buttons must delegate to the matching existing command with its exact reason and cost.
- T08: performance snapshots, score explanations and booking previews. A planned booking is not a performance that occurred. Explain both relationship and venue inputs without generating a new relationship reward.
- T09: `PublicLinks`, `AccountsOf`, `PublicDisputes`, `PreviewLinkPromise`, `PreviewResolve`; concrete `CompareNow` and `PublicAccord` offer current explanations. IDs join accounts to links and disputes; records stay separate from claims.

Use typed keys such as `(entity kind, stable id)` in selections and joins. An authored definition is not a runtime instance. In particular, `local:flavor-log`, institution spec `flavor-log`, and runtime institution `inst-N` are not interchangeable. Display names must never become identity keys.

## Implementation sequence

1. Build the snapshot read model and missing-reference behavior. Start with one memorial recipe connected to evidence, practice, venue and carrier; make that path work before adding every panel.
2. Add the atlas view and stable-ID navigation. Keep selection through refresh by re-resolving IDs, never retaining a mutable entity from a previous save. Add Back and keyboard focus recovery. Link existing detail panels instead of copying their command implementations.
3. Add local comparison and command previews. Call the domain's preview again before executing. Unsupported actions have an explicit reason; they are not decorative buttons. Account versions must remain distinguishable from the cited evidence.
4. Add one contextual HUD opportunity and the missing ruin-card route into Heritage. Avoid periodic notification spam. Then run the acceptance scenarios and release checks below.

## Required scenarios

**The bread that remembers:** record the Hunger; dedicate an existing or invented non-peach dish; serve it locally; establish a quiet observance; teach it; cross an Age. Reach its history, venue and living practice within three intentional selections. Assert recipe properties are unchanged, food is spent once, and provenance persists after two loads.

**The road between two songs:** create different local practices in two settlements; complete a real cultural-party visit; learn a practice; preserve/adapt through an eligible institution; earn an authored hybrid. Interrupt the road and verify future contact stops while learned practice remains and land ownership does not change. Compare the two places in the atlas.

**The promise and the feast:** link a hospitality promise; create a record-backed contradiction; open both dispute types to test targeting; reload before resolving; answer one; inspect original evidence beside the new account; repeal its law and verify immediate removal of accord effects. Perform with real participants and inspect the performance record. **The original plan's observance-to-ensemble bridge is not implemented**: a party festival demonstrates T08, but does not satisfy the named-observance acceptance item. If completing that exact item, add an explicitly reviewed party/cast binding and reuse CompletePerformance; test cancellation, participant departure, save/load and exactly-once rewards. Do not silently declare it covered.

## Release checks and evidence

- Fresh licensed Unity full EditMode run, including the new CulturePublicMemoryPlayTests assertions and ContentTests. Existing wrap-up compilation and 276 passing offline tests are a starting point, not a substitute.
- Atlas read/preview purity: compare saved state, resources, occurrences and reward counters before/after reads and repeated navigation.
- Save/load with atlas open; entity removed, settlement fallen/reused, dish renamed, teaching paused, dispute resolved elsewhere. No stale command targets or exceptions.
- Keyboard-only access and return focus; narrow layouts, long player names, empty/unfounded state and missing history. Record actual screenshots/results.
- Multi-Cycle runs: Unity gains/costs, food consumption, morale sources, tradition/notification counts, runtime and save growth. Derive budgets from the project machine; do not claim a performance target without measurements.
- Keep later-age institutions/choruses honest about prerequisites. Do not unlock them merely to make the atlas appear complete.

Unity currently exits before tests because no valid editor license is available. Preserve this as a validation blocker until a licensed run succeeds; do not mark T10 release-complete solely on compilation. When finished, write T10_REPORT.md with exact changed files, test commands/results, scenario outcomes, unresolved acceptance items and visual evidence. Update README status.

## Ready-to-use continuation

Implement T10 from this handoff on the existing integrated working tree. Start by checking current signatures and the recipe boundary. Compose a typed, read-only atlas over T01–T09, delegate all commands/previews to their owning systems, wire the existing window/HUD/world-card entry points, and add/run the three scenario fixtures. Preserve all unrelated changes. Explicitly handle the missing observance/performance bridge and report any acceptance item that remains unverified. Do not claim fresh Unity validation unless the licensed editor actually runs the tests.
