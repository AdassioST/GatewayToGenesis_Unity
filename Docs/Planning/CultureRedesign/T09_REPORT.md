> Closeout update: the fresh full Unity baseline passed 1,015 tests, with zero failures and one existing ignored test, including the expanded T09 scene scenario. T10 is now implemented. See [FINAL_REPORT.md](FINAL_REPORT.md) for final validation. The validation section below preserves the earlier handoff evidence and its then-current license blocker.

# T09 — Public memory and civic legitimacy

Completed implementation handoff, 28 September 2026. This report finishes the interrupted Agent C documentation and includes the subsequent integration fixes. T10 remains a separate UI composition task.

## Delivered behavior

The council can link a practised tradition to a promise currently supported by a stance or civic. Four authored promises cover the Open Gate, Almshouse Charter, Feast of Abundance, and Lesson of the Hunger. Two disputes, **The Hollow Table** and **The Loaf No One Bakes**, compare those claims with recorded tables, admissions, withheld luxuries, and relevant memorial practice.

Players can acknowledge the gap, revise the claim, or have an available Legend sponsor another account. Previews explain costs and consequences. Commands revalidate before paying. Harmonic Quorum restricts revision and sponsorship when the council is in discord. Each answer appends an account version; it never rewrites historical evidence. A resolved dispute cannot be answered twice.

Culture contributes a capped, explained component to the council's accord. Repealing the supporting law removes that component immediately; the next Seventh records the formal lapse. Acknowledgment benefits and contested local readings expire. The Hunger promise only reads evidence whose recorded subject is The Inescapable Hunger, including local memorials; a different crisis does not count.

The Accounts panel and PublicMemory.ink expose these decisions. A story binds to one dispute when it opens, so its text, answer requirements, consequences and ending do not jump to another open dispute. The sponsor token uses the recorded speaker after resolution. Loading reconciles state and story availability without immediately triggering event selection.

## Implementation

- `Scripts/GameData/Culture/PublicMemory/PublicMemoryModel.cs`: optional saved extension, links, account versions, evidence snapshots, dispute history, authored promises and responses.
- `PublicMemoryRules.cs`: comparison, local readings, dispute creation/resolution, lapse, capped accord and history retention.
- `CultureSystem.PublicMemory.cs`: snapshots, command previews, guarded commands, source facts and the SocialEffects feature at order 60.
- `Scripts/UI/Culture/PublicMemory/AccountsPanel.cs` and `CultureWindow.OpenAccounts`: reachable UI.
- `Scripts/GameData/Government/EdictSystem.cs`: explained culture contribution to accord.
- `Scripts/GameData/Events/EventSystemLogic.cs`: binds the dispute when starting a story; existing culture grammar routes the answer.
- `Assets/Resources/Events/PublicMemory.ink` and compiled asset: the two authored disputes. The wrap-up did not hand-edit generated Ink JSON.
- `CulturePublicMemoryTests.cs`, `CulturePublicMemoryPlayTests.cs`: rules, content grammar, persistence and scene coverage.

Paths above are relative to `Assets/GatewayToGenesis` unless they begin with `Assets/Resources`.

## Persistence and failure behavior

The extension is optional for older saves. Stable IDs and account versions survive serialization; missing historical facts stay unknown. Links/disputes have bounded detailed retention, while account text is retained. After detailed records have been pruned, the atlas must not fabricate their former connections.

Missing or inactive links and missing dispute definitions refuse an answer before spending. Unknown numeric resolution values are rejected. Future-dated participation cannot count as present evidence. Policy reversal preserves previous accounts. Queries return snapshots; the story's transient binding is rebuilt when it starts, and cleared on restore. The game's save flow restores a pending story notification rather than a live Ink continuation.

## Validation evidence

- The interrupted agent's `t09.xml` records **23 passed, 0 failed** for its selected Unity run, before these wrap-up changes. This is historical evidence, not a fresh result.
- Fresh isolated compilation of the integrated tree: **300 runtime and 89 editor sources, 0 errors**. Two non-field warnings concern obsolete Unity APIs in the existing LegendRelationshipPlayTests.
- Fresh offline reflection regression: **276 passed, 0 failed, 18 require Unity runtime**, across culture and neighboring save, event, edict, pantry and relationship suites. T09's **17 ordinary tests passed** in this run.
- Added regression coverage for story-specific selection, ended/repealed acknowledgment effects, invalid answers, future records, and cause-specific local/national remembrance. Expanded the scene scenario to check story binding, missing-link refusal without spending, teaching condition routing, and immediate repeal effects.
- **The expanded scene scenario has compiled but has not run.** Unity batchmode exits with `No valid Unity Editor license found` (code 198). A fresh full EditMode run and visual Accounts check remain release gates.

Fresh evidence: `Temp/CultureWrapup/build/build.log`, `Temp/CultureWrapup/integration-offline-final.log`, `Logs/culture-wrapup-baseline.log`. These temporary/local files are not release artifacts.

## Reproducible acceptance path

1. Found the culture; bake Ash-Loaf on three different Sevenths until its table is a lived custom.
2. Establish the Edicts and adopt Welcome the Wanderers. Link the Ash-Loaf Table to the Open Gate.
3. Hold luxury dishes without sharing them. Advance a Seventh: The Hollow Table opens once, with the records cited.
4. Inspect all three previews. They leave resources, evidence and account versions unchanged.
5. Save/reload twice. Sponsor another account through the story or Accounts panel: Unity is paid once, evidence remains intact, and the new speaker/account is recorded.
6. Repeal Welcome the Wanderers. The accord contribution disappears immediately; the following Seventh marks the link lapsed while retaining its history.
7. Also open a remembrance dispute. Each story must display and answer its own evidence; an unrelated crisis memorial must not satisfy the Hunger promise.

## Remaining scope

The numbers, political comparison, disputes and response rules are gameplay proposals/adaptations with canon labels; they are not new historical facts. T10 must compose the evidence/account distinction into its atlas and run the integrated scenarios. Do not treat the existing Accounts panel or the prior agent's passing suite as T10 completion.
