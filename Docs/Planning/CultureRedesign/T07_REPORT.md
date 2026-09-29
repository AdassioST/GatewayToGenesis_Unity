# T07 completion report: earned syncretism and specific ruin inheritance

Agent A, Wave 3, 28 September 2026. Built on T01 (traditions and the shared contract), T02 (settlement profiles, provenance, contact edges), T03 (Age lineage) and T04. All numbers are proposals ("Syncretism and inheritance" in Canon Gaps; the vault was not edited).

**Canon boundary:** the vault's own Syncretism (Achievement.md, "Interpretatio Aeterna") merges Constellations of the Stellar Legacy. The cultural blends here are not that system, and each one carries its own canon label. Digestive Rebirth (Achievement.md) is the existing reform and ruin-civic adoption, extended here rather than duplicated.

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Syncretism/SyncretismModel.cs`: the saved and input types.
  - Enums (append-only): `SyncretismMode` (SideBySide, Adapt, Replace), `InheritanceChoice` and `RuinClueKind`.
  - `HybridRule`, the authored compatibility rule: two parent ids, a result, canon label, Ages, technology and costs.
  - Saved records: `SyncretismDecision` (key, evidence ids, what was retained and lost, what was paid) and `VariantRecord` (parents, depth, the Age passages it has seen).
  - `SyncretismState` (version 1) lives in `CultureExtensionState.syncretism` (`[SaveOptionalField]`).
  - Rule inputs and views: `ParentPresence`, `ContactLink`, `HybridOffer`, `RuinRecord`, `RuinClue`, `RuinHeritage` and `InheritanceRequest`.
- `.../Syncretism/SyncretismTuning.cs`: the numbers and the three authored blends.
- `.../Syncretism/SyncretismRules.cs`: pure rules.
  - Meetings (`WhyNotMet`) and `Offers`.
  - Mode checks and outcomes, including what each choice keeps and what it lets go.
  - Capped `Depth`.
  - Ruin reading (`Read`), `WhyNotInherit` and `WhyNotRenew`.
- `.../Syncretism/CultureSystem.Syncretism.cs`: the commands and the feature.
  - Commands with previews: `PreviewHybrid`/`Hybridize`, `InspectRuin`/`RuinHeritages`, `PreviewInherit`/`Inherit`, `WhyNotReformToward`, and `PreviewRenew`/`Renew`.
  - Queries and a condition domain.
  - `SyncretismFeature`, which tells each new meeting once, after the traditions' lifecycle.
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Syncretism/HeritagePanel.cs`: the reform UI adapter. It has a list mode, a meeting mode and a ruin mode, plus a body section.
- Tests: `Tests/Editor/SyncretismTests.cs` (13 pure) and `Tests/Editor/SyncretismPlayTests.cs` (scene).

## Shared-system integration (Agent A's files, applied here)

- **`CultureSystem.cs` (reform):** `Reform(toward, ruinId)` no longer digests "the first digestible ruin" for any family.
  - With a ruin, it goes through the inheritance: the ruin's own records must support that family, and the ruin is digested once.
  - Without a ruin, it is a reform by the people's own will, paid in presence as before.
  - `ReformCore` keeps the existing `digestedRuins` ledger and the achievement key (`culture-reform:ruin:<id>`) with its flag.
  - `ReformCostText` now explains both paths.
- **`Value`** routes to `SyncretismValue`, which answers `blends`, `inherited`, `syncretism` and `blend:<rule>`.
- **`CultureSettings.syncretism`** (a `SyncretismTuning`): the existing `Culture.asset` keeps the code defaults.
- **T01 (A's own):**
  - `TraditionTuning` gains the three linked-only hybrid traditions: `loaf-of-names`, `crossing-chorus` and `first-fruit-songs`.
  - `TraditionInstance.mergedInto` (`[SaveOptionalField]`) marks a parent blended into a new form.
  - `TraditionRules.Advance` skips a merged instance: its practice there feeds the blend, never itself again.
- **World (`WorldSystem.Ruins.cs`):** `WhyNotAdoptRuinCivic(ruin, replacing)` and `AdoptRuinCivic(ruin, replacing)` add the replace path.
  - Replacing removes your civic, which drops its EffectRouter source and applies its removal penalties, then adopts the ruin's civic. If that adoption fails, your civic is restored.
  - The one-time `Ruin.civicAdopted` flag and the `ruin-civic:<id>:<civic>` achievement are unchanged: there is no second adoption ledger.
- **`CivicManager.CanUnlockCivic(name, inherited, replacing)`:** the civic being replaced no longer counts against slots, conflicts or requirements.
- **T02 (B's files, minimal):** `PracticeChannel.Inherited` is appended to the enum (append-only), with its provenance text in `LocalCultureRules`.
- **`ContentValidator.ValidateSyncretism`:** blends must have unique ids and a vault note each. Both parents must exist (a tradition or a custom), and the result must be a linked-only tradition. The depth must be within the cap, and Ages and technology must exist.
- **`CultureWindow.cs` (C composes it):**
  - a Heritage button and `LifeMode.Heritage`;
  - the panel's Fill, Reset and Describe calls;
  - the Reform body no longer lists "ruins that could be digested" (Heritage does);
  - the Reform button's tooltip now names both paths.

## Player-visible behaviour

- **Culture window, Heritage.** It lists:
  - each meeting open ("Meeting in Riverside: The Evening Song and Crossing Songs, could become The Crossing Chorus");
  - each ruin with what its records support, or "not investigated", "digested" or "nobody knows who lived there";
  - each inherited or blended tradition, with "Renew for this Age" and why it can or cannot;
  - the last eight decisions, with what each kept and lost.
- **A ruin's page.** It shows every clue and what it supports, then the choices:
  - **Civic, preserved:** adopt the civic beside yours.
  - **Civic, replaced:** adopt it in place of one of yours (the row cycles your civics; confirm on the next row).
  - **Adapt:** "Take in the X ways (Digestive Rebirth)", once per family its records support.
  - **Read the stones:** only for a ruin nobody knows.
  - **Revive:** take up a recorded tradition or custom in a chosen place (the row cycles the place).

  Every choice previews what happens, what is kept and what is lost. What a ruin can support:
  - Its civic (Civic.md family table), its district (tributary district family) and the customs its settlement kept (T02 former profile) each support their family.
  - The traditions that fell with it (T01) support their family too.
  - Its binding, kind and fall are remembered but support none: "a binding of magic is not a way of living".
- **A meeting's page.** It shows the blend's canon note, how they met, and the evidence ids. Then the three modes, each with its preview:
  - **Keep them side by side:** always free. Nothing is made, and it is offered again after 63 Sevenths.
  - **Blend a new form beside them:** 20 Unity (15 for the first-fruit songs). The new form starts Emerging and must be kept to last.
  - **Let it take their place:** 10 Unity (8), plus 15 per nation-recognized parent. It is a custom at once with their momentum. The parents here fall Dormant and merge into it (their record stays), and a custom here falls quiet. It is refused where the community keeps a parent as its own (Preserved), and one settlement cannot replace the nation's own tradition.
- **The three authored blends:**

  | Blend | Parents | Canon | Ages | Kind |
  |---|---|---|---|---|
  | The Loaf of Names | Ash-Loaf Table + Naming of the Lost | canon-supported (The Inescapable Hunger: mourning bakeries, recipes dedicated to those who starved) | needs Echoes of Hunger | culinary memorial |
  | The Crossing Chorus | Evening Song + Crossing Songs | new game rule | any | communal performance, not a Soul Leitmotif |
  | Songs of the First Fruit | Rite of the First Golden Fruit + Evening Song | canon-supported (Hunger: minor rites, named offerings, tiny festivals) | Age 0 only | |
- **Meeting rule:** both parents must be kept long enough: a lived tradition, or a custom gathered for at least twice. They meet in one of two ways:
  - kept in the same community, where the nation's own counts everywhere;
  - kept in two settlements joined by an **open** road across which T02 records an exchange: a custom of one reached the other by road, visit, founders, arrival or table.

  A road alone is no meeting, and a cut road stops new meetings. Each end of the road decides for itself.
- **Renewal:** allowed only after an Age passage recorded in `AgeProgression.History` since the tradition was made or last renewed. It costs 15 Unity. The tradition becomes a custom of the Age, and its depth goes up by one, capped at 2.

## Save and edge behaviour

- **Save format:** state saves in `CultureState.extensions.syncretism`. An envelope from before T07 loads with an empty state, and `Ensure` repairs null lists and ids. `mergedInto` is optional. Two reloads duplicate nothing and re-apply the blend's benefit (play-tested).
- **One-time uses:**
  - `digestedRuins` blocks a second digestion of a ruin, whatever path asks: Reform, Inherit or a story.
  - `civicAdopted` blocks a second adoption.
  - A revival is keyed by its clue's evidence id.
  - A blend decision is keyed `hybrid:<rule>:<place>`; only a side-by-side decision reopens.
  - **Policy:** adopting a ruin's civic and digesting its ways are separate one-time uses, and either leaves the other open. The preview says so.
- **Missing data:**
  - A ruin id with no record gives a null record: previews refuse, nothing crashes.
  - A ruin gone from the map is still read from what fell with it (`onMap = false`). Its civic can't be adopted and its ways can't be digested, but what it kept can still be taken up.
  - A tradition or custom id the world no longer defines is refused with a reason.
  - A settlement that fell is refused as a destination.
- **Age gates:** a tradition taken up from a ruin still needs its own Age and technology (`TraditionAvailable`), and so does a blend's result. No fictional unlocks.
- **Inherited traditions and the Age snapshot:** they are T01 instances, so `LivedPractices` carries them into the next Age's lineage (play-tested for both the blend and the revived tradition).

## Tests actually run

- **Offline reflection runner** (private build, 0 errors): `SyncretismTests` 13/13.
- **Unity batchmode** (filter `Syncretism|ContentTests|TraditionPlayTests|TraditionTests|CultureMemoryPlayTests|WorldRuin`): 51/51, including `SyncretismPlayTests` and `ContentTests`. The replace step exchanged Arcane Scholars (ledger effects present) for Military Academy.
- **Full EditMode suite on the integrated tree** (every agent's wave 1-3 work as it stood at 19:30), batchmode with no filter: **1009/1010 passed, 0 failed**. It includes `SyncretismPlayTests`, `ObservancePlayTests`, `CultureTransmissionPlayTests` (which had failed in the T04 run and now passes), and the T08 performance and T09 accounts suites.
  - The one skip is `ZzWorldShotsTemp.WorldShots`, an `[Ignore]`d temporary screenshot helper left by another session. It should be deleted before commit.
- Not run: a visual check of the Heritage panel (the play test only builds it with no exception).

## One acceptance path (`SyncretismPlayTests`)

1. **A town falls.** Found Iridia, then a town that keeps Tales at the Hearth. The town is pillaged, and its tradition is remembered in its ruins.
2. **Only its own records justify a reform.** Before investigation nothing can be taken ("Investigate..."). After investigation, the town's records name its civic and its tales. A reform toward an unrelated family is refused, and nothing is digested. The civic's family is accepted and digested once; a second reform from it is refused.
3. **Its civic replaces one of yours.** Adopt the ruin's civic in place of yours: yours and its ledger effects go, and the ruin's single adoption is spent.
4. **Its tradition comes back.** Take up Tales at the Hearth in the Capital: it is marked inherited and linked to the ruin, once.
5. **A blend takes its parents' place.** The nation keeps the Ash-Loaf Table and the Naming of the Lost. There is no offer before Echoes of Hunger; after it, the Loaf of Names takes their place for 10 Unity. A batch of Ash-Loaf then feeds the blend, not the merged parent.
6. **It lasts into the next Age.** Two reloads change nothing. The Age passes, and the lineage carries the blend and the revived tradition. Renewal was refused before the passage; it works after it (depth 2), then waits again.
7. **Missing ruins don't break inspection.** A ruin that never was, and one removed from the map, are inspected without a crash.

## Remaining defects and proposals

- **Inputs are snapshots:** the rules read T02's views each time they are asked. That is cheap at this scale but not cached, so revisit if profiles grow large.
- **The WorldView ruin card** still offers only "Adopt from the ruins" (preserve). Replace, adapt and revive are in Heritage; a card link to Heritage is a C/B composition task.
- **Proposals for Canon Gaps:** the three blends, the meeting rule, the costs, the side-by-side re-offer, the uncertain share (0.5), renewal at depth 2, and binding-as-record (a binding supports no family).

Closeout update: T10 now supplies the WorldView link into Heritage; the missing card route above is resolved. See FINAL_REPORT.md for integrated validation.
