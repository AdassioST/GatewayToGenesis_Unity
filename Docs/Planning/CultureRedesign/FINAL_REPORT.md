# Culture system final review

The three-wave culture implementation and T10 atlas are integrated in the existing working tree. The recipe system remains the kitchen's authority. This report covers the closeout edits; it is not a claim of ownership of the many unrelated, pre-existing working-tree changes.

## Fixed issues

1. Public-memory stories could display or answer an unrelated open dispute. Text, availability checks and consequences now bind to the same story-specific dispute.
2. Repealed civic promises could leave cached accord effects. Contributions now read the law and tradition's current validity immediately, including acknowledgment terms.
3. Future-dated records could satisfy historical windows. Evidence comparisons now bound both ends of the time window.
4. Unrelated crisis remembrance could fulfill the Hunger promise. The authored promise can require its specific crisis subject, with matching local/national accounting.
5. Missing definitions/links and invalid numeric resolution values could reach the spending path. They now fail before paying or changing accounts.
6. Restore could retrigger public-memory events. Restore rebuilds eligibility without random story activation or repeated rewards.
7. Teaching conditions lacked their direct domain route. `teaching:` now resolves through the existing teaching query.
8. Transmission authoring checks were missing from content validation. Institutions, venues, IDs and complexity constraints now run with culture validation.
9. Preparation cancellation promised a whole refund even when stores were full. Preview text now states the storage limitation.
10. Culture navigation overflowed a single button row. Actions now use a four-column grid; the new atlas uses a responsive scrollable panel.
11. Ruin cards did not reach all inheritance decisions. They now open the Heritage panel.
12. T10 lacked the unified, reload-safe biography view. The new atlas joins stable typed IDs, preserves placeholder links, distinguishes evidence from accounts and provides previews, comparisons and journal routes.
13. Named observances had no explicit ensemble host. The calendar/performance adapter now reserves an actual party and reuses the existing completion/reward boundary, including cancellation and persistence.
14. The calendar's next-date query skipped today's booked performance. Due-day validation now reads the unresolved occasion; early completion is refused.
15. Narrow-window canvas scaling shrank atlas text. The atlas now scales by height and wraps the available width.
16. Exploration status added unwanted glyph clutter. New glyphs were removed; fog, muted wilderness/known ground and fully coloured surveyed ground use existing player-knowledge textures at both scales.

## Validation

Full integrated Unity run (`Logs/culture-t10-final.xml`): **1,021 passed, zero failed, one existing ignored screenshot test** (1,022 total), including runtime-entering scene fixtures. The earlier full baseline passed 1,015 tests. Graphics-enabled focused regression (`Logs/culture-t10-visual.xml`): **12 passed, zero failed**, covering atlas, exploration, memory, teaching, public accounts, locality, performance and the world screenshot fixture. Final atlas typography/layout regression (`Logs/culture-t10-layout.xml`): **5 passed, zero failed** after the scaling fix; the regenerated 720-by-900 and 1920-by-1080 captures were inspected. Latest isolated compilation: **304 runtime sources, 91 editor sources, zero errors**, two existing obsolete-API warnings in LegendRelationshipPlayTests.

## Scope and practical limits

The authored, bounded T01-T10 feature set is implemented. No generated Ink or canon vault content was manually rewritten. All later-Age eligibility remains in place. No commit, reset or cleanup of unrelated work was performed.

Automated fixtures validate the memorial/teaching, local exchange/inheritance, and public-account/performance chains across the participating systems. They do not constitute a single continuous manual playthrough of all three narrative demonstrations. UI checks cover runtime construction, reload, keyboard neighbours and narrow geometry. Rendered map captures confirm the distinct fog/muted/full-colour treatment without added exploration glyphs. Atlas captures exposed and led to a narrow-layout scaling fix. Final balance still requires playtesting. Existing bounded history and reward caps remain in force; no new measured multi-Cycle performance or balance benchmark is claimed by this closeout.

See [T10_REPORT.md](T10_REPORT.md) for files, player routes and test coverage. Earlier handoff notes are retained as historical documents and marked superseded.

## Visual evidence

- [Regional exploration map](../../../Logs/CultureValidation/world-shots/2-meso.png).
- [Local exploration map](../../../Logs/CultureValidation/world-shots/7-micro-far.png).
- [Wide atlas](../../../Logs/CultureValidation/screenshots/atlas-wide.png).
- [Narrow atlas and long-text stress fixture](../../../Logs/CultureValidation/screenshots/atlas-narrow.png).

No failing automated checks or outstanding defects found in this review remain. The playtesting and benchmark limitations above are not represented as completed validation.
