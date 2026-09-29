> Historical handoff/baseline. Superseded by [FINAL_REPORT.md](FINAL_REPORT.md) and [T10_REPORT.md](T10_REPORT.md). Earlier license and missing-atlas/observance-bridge notes describe the pre-T10 state.

# Culture waves 1–3: integration review

28 September 2026. T01–T09 implementations and their UI modules are present. T09's interrupted documentation is now finished. T10's atlas has not been implemented; its final dispatch is in [T10_HANDOFF.md](T10_HANDOFF.md).

This was a direct code/integration review and regression pass, not a new implementation of the atlas or a fresh Unity playtest. Existing recipe work and the other uncommitted project changes were preserved.

## Reviewed boundaries

- **T01 traditions:** occurrence attribution and deduplication, pipeline phases, lifecycle, source-based effects, snapshot queries, optional save migration and final AfterRestore reconciliation.
- **T02 local culture:** contact/admission attribution, unknown origins, road interruption, former settlements and the distinction between exposure and participation.
- **T03 memory:** recorded causes versus mutable dedications, quiet remembrance, dormancy, lineage, and derived remembrance effects on restore.
- **T04 observances:** preparation, reservation/cancellation, food served once, quiet fallback, missed occasions and legacy holiday reconciliation.
- **T05 hospitality:** exact-resource serving, reserve checks, policy/access records, local transmission and capped patron rewards.
- **T06 transmission:** teaching state, seats, pause/completion, records versus living bearers, recipe references, authored institutions and conditions.
- **T07 syncretism:** authored parents, stable references, local replacement, ruin investigation/inheritance and unknown-history behavior.
- **T08 performance:** booking cancellation, completion/echo effects, settlement strain, relationship input and the non-destructive Coherence overlay.
- **T09 public memory:** evidence selection, story targeting, account resolution, policy lifecycle, accord, restore and missing references.

The review followed shared lifecycle, query, spending and save boundaries and ran all selected ordinary tests. It does not claim every possible gameplay sequence or every rendered UI has been exercised.

## Defects fixed

1. **Wrong dispute in story text.** Text used the newest dispute globally while consequences selected by kind. Stories now bind at start to their own dispute and retain that binding through resolution.
2. **Accord surviving a repealed policy.** Acknowledgment bonuses ignored ended links, and cached accord lagged policy changes. All terms now require an active supported link; the read reflects policy changes immediately without mutating history.
3. **Permanent local contestation.** A sponsored account's local doubt could persist after its configured duration or link ended. It now follows the same lifetime as contestation's accord effect.
4. **Invalid resolution spending/crash.** A dangling link could pass preview, pay Unity and then dereference a null result. Definition/link validity is now checked before payment; the pure rule also rejects inactive links, mismatched definitions and invalid resolution enums.
5. **Incorrect remembrance attribution.** Local support counted unrelated memorials; the Hunger promise also compared every crisis. Local and national comparison now require the promised kind and recorded subject.
6. **Future records counted as present.** Comparison and recent-answer windows now reject dates after the current Seventh.
7. **Event selection during restore.** Reconciliation can restore story availability without immediately invoking random event selection.
8. **Unfinished teaching integration.** Registered `teaching` in GameValues using TeachingValue's existing keys. Added ContentValidator checks for institution venues, IDs, predecessor references, canon notes, numerical constraints and tradition complexity references. The `culture:` aliases remain valid.
9. **Overpromised observance refund.** Preparation preview now states that cancellation returns what the stores can hold, matching the existing capacity-clamped refund behavior.

Added five pure public-memory regressions and one validator regression. Extended the existing public-memory scene test for the story/payment/domain/repeal seams; that scene extension is compiled but unexecuted.

## Verification

- Fresh whole-project-source compile in a private generated project: **0 errors**, 300 runtime sources and 89 editor sources. Existing generated project files were not edited.
- Final selected offline reflection run: **276 passed, 0 failed; 18 ordinary tests require Unity APIs**. The selection was `Culture|Tradition|Hospitality|Observance|Syncretism|Performance|Pantry|Save|Edict|Event|LegendRelationship|ContentTests`.
- The offline runner executes ordinary NUnit Test/TestCase methods and fixture setup/teardown. It does **not** execute UnityTest coroutines and is not equivalent to Unity's test runner. The 18 runtime-dependent results do not include every unexecuted coroutine scenario.
- Initial save-storage failures were sandbox file-replacement failures. The same save tests passed outside that restriction (8 passed, 2 require Unity); the final integrated run used that working context.
- Fresh Unity batch run was attempted but licensing failed before tests: `No valid Unity Editor license found`, exit code 198. No fresh NUnit XML was produced. See `Logs/culture-wrapup-baseline.log`.
- Previous agents reported successful Unity runs, including T07's 1009 passed / 1 ignored full suite and the interrupted agent's 23-test selection. Those precede these fixes and must not be used as proof that the changed scene tests pass.

Local evidence is under `Temp/CultureWrapup`: `build/build.log`, `integration-offline-final.log`, and the isolated runner/build inputs. These are intentionally temporary. The final code and committed regression tests are the durable deliverables.

## T10 readiness and remaining gates

The domain implementation is ready for atlas composition, with fresh compilation and ordinary-rule coverage. Release acceptance remains conditional on a licensed Unity run, the three end-to-end scenarios, visual/keyboard checks, and multi-Cycle balance/save-size measurements.

The atlas still needs typed navigation, a selected practice's connected history, local comparison, delegated previews, existing-panel/journal/Library links, and one contextual HUD opportunity. A Flavor Log local practice and a Flavor Log teaching institution are distinct entities even when their display labels match.

Known feature limits remain explicit: performance bookings are hosted by party festivals, not T04 observance days; carried local customs are not automatically performable repertoire; later choral content is gated beyond currently reachable Ages. Account text is retained after some detailed links/disputes are trimmed, so archival UI must tolerate missing connections. These are not grounds to invent witnesses, unlock later Ages, or imply a missing command exists.

The original third scenario asks for a named observance with performers. That exact bridge is absent. T10 must either implement and test an explicit party/cast adapter as a separately identified domain patch, or leave that acceptance item open and demonstrate the existing festival path without calling it the same feature. A navigation link alone does not close it.

No reset, cleanup, commit, generated Ink edit, recipe redesign or new implementation agent dispatch was performed during this wrap-up.
