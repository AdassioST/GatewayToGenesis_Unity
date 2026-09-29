# T10 implementation report

T10 is implemented in the current working tree. Validation results are consolidated in [FINAL_REPORT.md](FINAL_REPORT.md). The atlas is a player interface over the existing systems, not a new canonical institution.

## Delivered

- `Scripts/UI/Culture/Atlas/CultureAtlasReadModel.cs`: detached graph of recipes, lived traditions, local customs, settlements, evidence, dedications, occurrences, teachers/institutions/records/variants, observances, tables, public links/account versions/disputes, performances, ruin inheritance and Age lineages. Typed IDs prevent similarly named entities from merging. Missing records are explicitly unknown. Place comparison distinguishes exposure from practiced or quiet customs.
- `CultureAtlasWindow.cs`: responsive anchored panel, wrapped text, scrollable records, category browsing/pagination, Back/Escape, explicit keyboard navigation and focus visibility. Selection stores an ID, and refresh rebuilds the snapshot after culture changes/load. Tradition decisions display the domain preview and revalidate before execution. Other decisions open their existing modules. Library and Ballads & Bonds remain the prose/journal owners.
- `CultureWindow`, `CultureHud`, `HeritagePanel`, `WorldView`, `BalladJournalView`: reachable atlas and detail routes. Culture navigation uses a four-column grid instead of overflowing one horizontal row. The HUD presents one contextual opportunity rather than emitting notifications every Seventh. Ruin cards now lead to all Heritage decisions.
- Explicit T04/T08 integration patch: `CultureSystem.ObservancePerformance.cs`, performance model/query/panel, and observance completion. A player can reserve a real cultural party for a compatible nearby calendar occasion. The party must remain free and present. A missed/changed date, departure or unavailable cast cancels it. The existing single performance completion boundary owns rewards and occurrences. Save-optional host/date fields preserve older festival bookings.
- Requested exploration presentation: `WorldExplorationAppearance`, `WorldTerrain.shader`, renderer material setup and map legend. Existing fog and knowledge masks remain authoritative. Wilderness retains 25% saturation, glimpsed local ground 42%, known ground 62%, surveyed ground full colour. No exploration stamps/checks/bars are drawn. Regional cells become fully coloured only when regionally surveyed; zooming in exposes the existing individual survey masks.

## Important boundaries

Recipe composition, derivation and runtime resource registration remain owned by the existing kitchen. Atlas links never reinterpret ingredients as attendance. Evidence and accounts are separate records; a public account cannot rewrite the underlying occurrence. Written records are not counted as living practitioners. Authored definitions are distinct from runtime tradition/institution IDs. Later-Age repertoire retains its eligibility gates.

The adapter closes the previously documented missing calendar/performance connection. It is explicitly a domain patch, not a claim that a navigation button completed the original scenario. Calendar views intentionally show the next date after today; completion checks the still-unresolved booked occasion on its actual day.

## Acceptance paths and checks

1. Open Your Nation, then Atlas. Select a memorial dish; inspect its dedication/evidence and recorded uses, then its practice and place. Recipe and teaching scene regressions verify these links on actual snapshots and assert that graph reads do not alter saved state.
2. Compare two recorded settlements. Exposure, practice, quiet history and provenance remain visible separately. Local-culture scene coverage checks the atlas alongside road/contact persistence and unchanged territorial ownership.
3. Open a dispute and follow its public link/account versions. The public-memory scene regression checks story-specific targeting, reloads, missing-link refusal without spending, evidence/account separation and immediate repeal effects. Performance scene coverage separately reserves a named calendar host, reloads twice, refuses premature/festival completion, completes with real voices once, and cancels a subsequent booking on departure.
4. With a record open, reload and refresh. The UI regression checks a 520-by-700 panel, wrapped long text, positive button geometry, explicit keyboard neighbours and unknown selection fallback.

The original three narrative scenarios are covered through related scene fixtures rather than one single uninterrupted campaign fixture. Automated geometry checks do not establish subjective visual quality or final balance. Those distinctions are retained in the final report.

## Source basis

The implementation follows the source/canon labels and research-to-design boundaries in [CULTURE_REDESIGN.md](../CULTURE_REDESIGN.md), especially living practitioners versus archives (R1) and recorded events versus public retellings (R3). It adds no claim that the UI or its numerical tuning is established canon.

## Rendered review

Graphics-enabled Unity captures are saved under `Logs/CultureValidation/screenshots/` (atlas-wide and atlas-narrow) and `Logs/CultureValidation/world-shots/` (regional, local and distant map views). These are local validation artifacts, not gameplay assets. The first narrow capture exposed inherited width-based scaling; the atlas now uses height-based scaling and wraps instead of shrinking the prose. The regional map capture shows the intended colour progression with the existing site/party symbols unchanged.

Final results: full integrated run 1,021 passed / 0 failed / 1 ignored; graphics-enabled changed-feature run 12 passed / 0 failed; final atlas layout run 5 passed / 0 failed. Narrow and wide atlas captures and regional/local map captures were inspected. Test XML and logs live in Logs; exact filenames and limitations are in FINAL_REPORT.md.
