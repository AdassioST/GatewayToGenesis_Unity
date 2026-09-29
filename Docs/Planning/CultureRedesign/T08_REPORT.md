# T08 completion report: ensembles whose relationships affect their performance

Agent B, Wave 3, 28 September 2026. Built on T01's contract (`CulturalOccurrence`, `CultureExtensionState`, `ICultureFeature`, `ICultureQuery`, `CultureEffectPolicy`), T02's settlement customs and cultural parties, T03's remembered causes, and T04's festival and calendar work. It reads no T09 or T10 code.

Player promise: "The people performing together matter, and their history can be heard in the result."

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Performance/PerformanceModel.cs` holds the saved state, inside `CultureExtensionState.performance` (`[SaveOptionalField]`):
  - `PerformanceIntent` (Reassurance, Remembrance, Celebration, Restoration), `PerformanceBand` and `PerformanceStatus`. All are append-only.
  - `RepertoireSpec`: authored, with a canon label, vault note, Ages, technology, civic, binding, the least number of voices, intents and the local custom it matches.
  - `PerformanceBooking` (`perf-N`): the reservation of a party's festival for a piece.
  - `PerformanceEcho`: the bounded, expiring benefit.
  - `PerformanceRecord`: history with its explanation, bounded at 30.
  - `PerformanceTuning`: every number (all proposals) and the authored repertoire.
- `.../Performance/PerformanceRules.cs` is pure:
  - eligibility and the deterministic evaluation;
  - booking checks: lapse, walk-away, a stopped festival, a fallen settlement;
  - completion once and ending with nothing given;
  - echoes: one per settlement, morale for the strongest few, expiry;
  - the Coherence overlay sum, capped.
- `.../Performance/CultureSystem.Performance.cs` covers the scene side:
  - casts from the party (met, not lost), each Legend's Composure and binding, their directional ties, and the cause for a remembrance;
  - `PreviewPerformance`, `RepertoireChoices`, `PlanPerformance`, `CancelPerformance`;
  - `CompletePerformance`: the adapter's single entry;
  - the Seventh's upkeep, the named morale sources and the overlay;
  - snapshot reads on `ICultureQuery`;
  - `PerformanceFeature` (phase SocialEffects, order 30).
- `Assets/GatewayToGenesis/Scripts/GameData/World/WorldSystem.Performance.cs`: the festival integration adapter. It contains only `CompleteCulturalPerformance`.
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Performance/PerformancePanel.cs`: the Performances panel and the window-body section.
- `UI/Culture/Local/LocalCultureCard.cs` (lane B, T02): the party card shows its planned performance and "Plan a performance…", which opens the panel on that party.
- Tests:
  - `Tests/Editor/PerformanceTests.cs`: 13 pure tests.
  - `Tests/Editor/PerformancePlayTests.cs`: a ClickerScreen scenario.

## Shared patches (small; for the owners to keep at integration)

- **World owner.**
  - `WorldSystem.Culture.cs` `FinishFestival`: one call to `CompleteCulturalPerformance` after `CompleteCulturalVisit`. `CelebrateFestival` and the festival's own Meaning fragments are unchanged, so the festival stays the single completion owner.
  - `CityDevelopment.cs`: `CoherenceOverlay` (a `Func<WorldTile, float>` hook) and `CoherenceOf(t)`. The four Coherence reads in `Gather`, `Ceiling` and `TileScore` now go through it. `WorldTile.coherence` is never written.
  - `PotentialField` (the Settle lens) still reads the ground's own value. A temporary chorus should not change where to settle.
- **Agent C.** `CultureWindow.cs` gains `LifeMode.Performances`, the "Performances" button, the `OpenPerformances(unit)` deep link, and `Reset`, `Fill` and `Describe` registrations.
  - The action bar now holds 12 buttons, so T10 should check that it still fits, or group the life panels.
- **Docs.** `Scripts/ARCHITECTURE.md` gains the T08 paragraph. `Resources/Library/Vault~/Canon Gaps.md` gains "Ensembles and performance".
- **Not requested, and optional for Agent A:**
  - a `performance` condition domain (for example `performances`, `performance:<repertoire>`);
  - a `ContentValidator` check of `PerformanceTuning.repertoires` (vault note present, Ages ordered, intents non-empty). The pure test checks the note and the intents meanwhile.

## Player-visible behaviour

- **Where the player plans:** a cultural party standing in one of your settlements. Its card has "Plan a performance…", which opens the Culture window at Performances on that party.
- **Each choice** is a piece plus a purpose. It shows its band and score, and its tooltip has every reason.
  - Choosing one shows the full explanation. "Plan …" commits it, and is disabled with the reason when it can't be done.
  - Planning is free, and the performance is given at the end of the party's festival. "Call off" ends it with nothing given, while the festival goes on.
- **Authored repertoire** (the content floor is two early repertoires with different intents, plus later magic locked):

  | Piece | Label | Vault note | Intents | Needs |
  |---|---|---|---|---|
  | The Evening Song | Canon-supported | Waltz Pillar.md | Reassurance, Celebration | 1 voice; Resonance carries it; a town keeping the evening-song custom hears it best |
  | Song of the Named | Canon-supported | The Inescapable Hunger.md | Remembrance | A loss the people remember (T03); Strand carries it; a loss within 2 cells is "remembered here" |
  | Polyphonic Chorus | Explicit canon | Civic.md (Polyphonic Choral Singers) | Restoration, Reassurance | Ages IV-VI, the civic in force, 3 voices |

  The game's Ages end at II, so the chorus is always shown as locked: "Polyphonic Chorus belongs to Age IV-VI: not yet."
- **The explanation**, one line per factor with its signed contribution:
  - each voice ("Oren Fractured, Resonance 6: 0.51");
  - "with 3 voices the weakest counts for 1/3";
  - "3 voices together (an orchestra, not a soloist)";
  - each pair's two readings ("Vaelia and Oren: Sworn Duet / Frayed Tension", plus "a wound they share" in a remembrance);
  - "Ashford keeps it as its own custom", "Rooted in Ashford (60%)", "The Inescapable Hunger is remembered here", "Ashford is too troubled to hear easily (strain 45)";
  - and the score and band (Faltering, Steady, Moving or Resonant).
- **At the festival's end**, the notice adds: "The Evening Song for reassurance: Moving; Ashford's strain eased by 3 a Seventh for 7 Sevenths."
  - **Echo:** a Faltering performance leaves none. Otherwise it eases the settlement's strain each Seventh, by purpose (reassurance most).
  - **Morale:** only a Moving or Resonant performance gives any (+1 or +2), as a named source `Culture: <piece> in <place>`. Only the 2 strongest echoes apply morale, and each settlement keeps one echo.
  - **Coherence:** a later-Age chorus also lends Coherence around its settlement through the overlay.
  - **Relationships:** the performers share one memory. An existing significant bond gains +1 to +3 affection; nothing else changes.
- **Accessibility:** everything is text. No rhythm input and no audio are needed.

## Acceptance criteria

- **Two casts can produce different explained outcomes:** a calm duet with a Sworn Duet tie is Moving, while a rival pair with a Fractured voice is Steady, and each line names why (`TwoCasts_ProduceDifferentExplainedOutcomes`). In the scene, swapping a calm companion for a Fractured one lowers the score, and the text says "<name> Fractured" (the play test).
- **Replaying completion cannot farm Meaning or ties:**
  - `Complete` acts only on an active booking. The occurrence key and the relationship moment key are both `performance:perf-N`.
  - The performance grants no Meaning; the festival grants its own once.
  - The play test checks that Meaning increases by exactly the festival's fragments, and that a replay and a reload change nothing (`Completion_HappensOnce_AndAReplayChangesNothing`).
- **Co-performance cannot create a bond or advance one:** `ShareExperience` is called with direction 0 and no wound, so 60 performances never create a significant tie. The meter can reach its threshold, but the stage never moves, and a repeated key is refused (`CoPerformance_NeverMakesASignificantTie_NorChangesAStage`).
- **Failed or cancelled performances clean up reservations:** calling one off, walking away, a festival stopped before its end, a disbanded party, a fallen settlement, or a 7-Seventh lapse each end the booking with nothing given. The party is freed and the reason is kept in the history (the pure test and the play test).
- **Preview never changes state:** the play test compares the saved culture state before and after `PreviewPerformance`. The evaluation is pure.
- **Removing a source removes its effect:** expired or displaced echoes are dropped. Every morale source this culture applied that is no longer wanted gets an empty `ApplySet`, and the Coherence overlay returns 0 once its echo is gone (`TheCoherenceOverlay_IsBounded_Local_AndGoneWithItsEcho`, `Echoes_AreBounded_Expire_AndOnePerSettlement`).
- **Accessible text:** the panel, body section and party card give all the information as text (the play test's `CheckText`).
- **Advanced magic is inaccessible in the early-Age fixture:**
  - The chorus is refused for its Age, then its civic, then its voices.
  - Restoration is refused for the early songs, and an echo gets no Coherence unless the chorus is unlocked when the performance completes.
  - In the scene, the chorus choice reads "Age IV".

## Save, reload and edge behaviour

- **Old saves:** an older envelope loads empty; nothing is invented. `Ensure` repairs lists and raises the serial past saved ids.
- **Repeated load:** echoes and bookings are saved. After a restore, `Reconcile` re-applies only the morale sources and the overlay hook from saved echoes. The play test reloads, finds the same history and echo, and the completion replay returns nothing.
- **Duplicate events:** a completion runs once per booking, one occurrence is keyed per performance, and one relationship moment is keyed per performance. `FinishFestival` can only complete the booking whose party and settlement match.
- **Missing references:** a Legend lost before the end is not in the cast. Too few voices, or a piece that is no longer eligible (Age, civic, loss), makes the performance **Failed** with the reason. An unknown repertoire id shows the id and cannot be planned.
- **Cancellation:** there are no resources to return, because planning costs nothing. The reservation is the party's festival slot and the settlement's stage (one plan per party and per settlement), released on every end.

## Tests actually run

- Private compile (`mkbuild.sh`): 0 errors. For a while Agent A's T07 `CultureSystem.cs` referenced methods not yet written. I verified my files against a temporary stub kept only in my scratchpad, then rebuilt without it once T07 landed.
- Offline pure runner: `PerformanceTests` 13/13, `HospitalityTests` 15/15, `LocalCultureTests` 14/14, `TraditionTests` 16/16, `WorldCivilizationTests` 15/15.
- Unity batchmode EditMode, 01:24 UTC (filter: performance, hospitality, local culture, tradition, content, save, world civilization, expedition, Legend relationship): 73/74.
  - `PerformancePlayTests` 1/1, `PerformanceTests` 13/13, `ExpeditionPlayTests`, `LegendRelationshipPlayTests` and every `LegendRelationshipTests` case passed.
  - The one failure is T09's (Agent C): `ContentTests.AllStoriesValidate`, where PublicMemory consequences `culture:account …` lack a signed amount.
- Unity batchmode EditMode, second run after adding the panel, body and card text check (filter: performance, culture life, culture): 18/18 passed, including `PerformancePlayTests`.
- Not run: the full EditMode suite, and a visual check of the panel layout in the window.

## One acceptance path

1. Found the culture and research the festival technology. Meet two or three Legends, and form a Cultural Party with two of them. Walk it into the Capital.
2. On its card choose **Plan a performance…**. The Performances panel lists:
   - The Evening Song (reassurance and celebration), with its band and score;
   - Song of the Named (with the reason until a loss is remembered);
   - Polyphonic Chorus: "belongs to Age IV-VI: not yet".
3. Pick The Evening Song for reassurance. The header lists each voice's readiness, the two Legends' readings of each other and the Capital's factors.
   - Swap a companion for a strained Legend: the score falls and the reason names them.
4. Plan it, then **Hold a festival**. At its end, the notice adds the performance's band and echo.
   - The performers share a memory. The history keeps every reason.
   - The Capital's strain eases each Seventh for 7 Sevenths.
5. Plan again and walk the party out of the settlement: it is called off with the reason, and nothing is given.
6. Save and reload: the history and echo are unchanged, and nothing is given twice.

## Remaining defects and proposals

- Every number is a proposal (Canon Gaps "Ensembles and performance").
- Only festivals host performances. T04's observance days with a Performance objective could host a performance through the same `CompletePerformance` path. That is not wired, because an observance has no party and no cast.
- A cultural party's carried T02 customs are not yet performable pieces. Only the authored repertoire is.
- The chorus cannot be reached in play until the game has Ages IV-VI and a way to adopt the Polyphonic Choral Singers civic. Its effect is covered by the pure tests with those prerequisites supplied.
- Morale is national, as the game's morale is. The settlement-local part of the benefit is the strain eased there.

Closeout update: T10 adds an explicit optional calendar host for a real party, reusing CompletePerformance with the same cast/reward boundary. Festival bookings remain compatible; due-day checks, cancellation, early completion refusal and repeated reload are covered by the expanded PerformancePlayTests. See T10_REPORT.md and FINAL_REPORT.md.
