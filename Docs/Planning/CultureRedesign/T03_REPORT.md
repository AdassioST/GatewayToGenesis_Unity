# T03 completion report: shared wounds, memorial places and inheritance

Agent C, Wave 1, 28 September 2026. Built against T01's frozen contract (`Culture/Integration/*`: `CultureEntityRef`, `CultureStamp`, `CulturalOccurrence`, `CanonStatus`, `ICultureFeature`, `CultureExtensionState`, `ICultureQuery`).

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Memory/MemoryModel.cs`: saved records (`MemoryEvidence`, `MemorialDedication`, `MemoryPractice`, `MemoryLineage`, `MemoryState` in `CultureExtensionState.memory`), `MemoryTuning`, the authored `MemorialSuggestion` table and the read-only `MemorialGround`/`MemorialBloom`.
- `.../Culture/Memory/MemoryRules.cs`: pure rules. It learns a cause once, handles dedication, release and dormancy, and keeps practice once per Seventh. It also computes capped recovery, keeps the Age lineage, records recognition and reads the memorial ground.
- `.../Culture/Memory/CultureSystem.Memory.cs`: the owner adapters (`AgeProgression.AgePassed`, `LegendProgress.Lost`, `WorldSystem.SettlementFell`/`RuinReclaimed`) and reconciliation from the world's own records. It holds the commands, the query snapshots, the `MemoryFeature` (phase `SourceActions`, order 50) and the `Culture: Remembrance` effect source.
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Memory/MemorialPanel.cs`: the Memorials panel and body section.
- Tests: `Tests/Editor/CultureMemoryTests.cs` (17 pure) and `Tests/Editor/CultureMemoryPlayTests.cs` (scene).

## Shared patches (small, for Agent A to keep at integration)

- `WorldSystem.Ruins.cs`: two instance events, `SettlementFell` (raised at the end of `AfterFall`) and `RuinReclaimed` (raised in `Reclaim`). These are the owner adapter for settlement loss, which previously had no event.
- `CultureWindow.cs` (C is its final editor): `LifeMode.Memorials` and the "Memorials" bar button, the panel fill, `MemorialPanel.Reset/Describe`. T01's `TraditionPanel` is registered the same way. To fit eight buttons, the bar's spacing went from 28 to 18, its font from +6 to +2, and "Show on the map" became "Map lens".
- Requested but not done (A's registries): a `memory` condition domain in `GameValues` for Ink (`memory:kept`, `memory:causes`), and a `ContentValidator` check that every `MemorialSuggestion` target exists. The pure test `Suggestions_NameRealRecipesAndRites_WithTheirCanonStatus` covers the second for now.

## Player-visible behaviour

- **Causes remembered:** an Age Crisis lived through, a settlement that fell, its ruins reclaimed, and a Legend lost to Dissonance. A witnessed cause is announced once, suggesting a fitting memorial when one exists. A cause read from an older save's records is marked "learned from older records". Its moment stays unknown and it is never announced.
- **Culture window, Memorials:** the list of causes with how each was last kept. Opening a cause offers the following:
  - "Keep a quiet remembrance". It is free, needs no feast, Unity or morale, and can be kept once per cause every 7 Sevenths.
  - Dedicate an existing dish, rite, holiday or landmark. The vault's own memorial is listed first. The Ash-Loaf for the Hunger is explicit canon: The Inescapable Hunger.md, gated by Echoes of Hunger.
  - Release a dedication. Its history is kept.
  - Recognize an inherited memorial as this Age's own.
  - The memorial ground: the blooms that actually live there and whether each one's own needs are met.
- **Practice:** a dedicated recipe baked (T01 `Production`), rite held (`Gathering`), holiday kept (`Observance`), or a festival in a dedicated landmark's settlement keeps the memory once per Seventh. Each emits a `Memorial` occurrence, which T01's "The Naming of the Lost" and "The Ash-Loaf Table" listen to.
- **Recovery:** +1 morale per cause kept within the last 21 Sevenths, capped at +3 however many were lost. A loss that nobody keeps gives nothing. When the practice lapses, the effect is removed.
- **Inheritance:** each Age passage keeps a lineage record: its causes, a copy of the dedications as they stood, and the practices lived (holidays, national foods, rites held, landmarks, T01 traditions). Living and dormant dedications go on into the next Age, awaiting recognition. A lost Legend carries `worldTruthCandidate` as a future World Truth link. Nothing is canonized.
- **Ecology:** read only. No residue is added, no strain is kept, and the bonus is always 0 in this release. A comforting memorial does not make a grief bloom thrive (each species keeps its own `residueNeed`).

All numbers are proposals (`MemoryTuning`). The Ash-Loaf-for-the-Hunger link is explicit canon. The remembrance rite for the Hunger, the shrine for a fallen settlement and the evening of song for a lost Legend are canon-supported adaptations. The records, recovery and inheritance rules are new game rules.

## Save and edge behaviour

- The memory saves in `CultureState.extensions.memory` (`[SaveOptionalField]`). An envelope from before it loads with a fresh memory; this is tested.
- On reload, `AfterRestore → MemoryFeature.Reconcile(Restored)` re-subscribes, reads the causes already in the world's records by stable id (no duplicates), settles dormancy, and re-applies the effect. It grants no reward and makes no announcement.
- Duplicate events are deduped by evidence id (`crisis:<age>:<pass>`, `fall:<ruin>`, `recovered:<ruin>` → related `fall:<ruin>`, `legend-lost:<name>`). Practice counts once per dedication per Seventh, and occurrences are deduped by key in T01's ledger.
- A missing reference makes the dedication dormant with a reason (for example, a landmark whose settlement fell). It wakes if the target returns and is never deleted.
- A renamed recipe keeps its dedication, because the key is the recipe id. The label it was dedicated under is kept for the history.

## Tests actually run

- Offline reflection runner, after a clean private build: `CultureMemoryTests` 17/17 and every `Culture*` pure test (75 passed, 5 needing the Unity runtime).
- Unity batchmode EditMode: `CultureMemoryTests` 17/17, `CultureLifePlayTests` 2/2 and `CulturePlayTests` 1/1 passed. `CultureMemoryPlayTests` first failed on fixture issues: a lambda captured iterator locals, and the new game had no surveyed cell for a town. Both are fixed.

- Final run (batchmode, filter `Culture|Tradition|LocalCulture|WorldRuinTests`): 123/125. `CultureMemoryPlayTests` and `CultureMemoryTests` passed. The two failures were in peers' play tests edited minutes before the run (`TraditionPlayTests` NRE near line 72; `LocalCulturePlayTests.HamletSite` found no site on the seed world). They are not in T03 code. Offline pure suites after the last change: CultureMemoryTests 17, TraditionTests 16, LocalCultureTests 14, CultureLifeTests 31 (+5 runtime-only), CultureTests all passed.
- Hook lifetime: `CultureSystem.Update` calls `EnsureMemoryHooks()` (cheap reference checks) so owners that start late are hooked before their first event, and `OnSingletonDestroy` calls `UnhookMemory()`.

## One acceptance path

1. Found a culture. A town falls: Memorials lists "The fall of <town>".
2. Keep a quiet remembrance. It costs nothing and gives +1 morale.
3. The Hunger passes (Age of Desolation to Renewal) and is listed.
4. Research Echoes of Hunger and dedicate the Ash-Loaf to the Hunger (marked as the vault's own memorial). Bake a batch; at the next Seventh the dedication is kept once and morale is +2.
5. Save and load, then advance a Seventh: still two causes and one dedication.
6. The next Age passes: a lineage is recorded, the Ash-Loaf dedication shows both Ages and "Recognize ... as this Age's inheritance".
