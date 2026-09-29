# T02 completion report: local cultures and routes of exchange

Agent B, Wave 1, 28 September 2026. Built against T01's frozen contract (`Culture/Integration/*`: `CulturalOccurrence`, `CultureStamp`, `CultureEntityRef`, `CanonStatus`, `ICultureFeature`, `CultureExtensionState`, `ICultureQuery`). No same-wave dependency on T01's implementation. T01 now listens for `local:evening-song` gatherings, and T02 listens for T01's `Recognition` occurrences.

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Local/LocalPracticeCatalog.cs`: the six authored customs (ground words, Ages, family, canon label, vault note) and `LocalCultureTuning`. Every number is a proposal.
- `.../Culture/Local/LocalCultureModel.cs` holds the saved state, inside `CultureExtensionState.local` (`[SaveOptionalField]`):
  - settlement profiles, and former profiles for fallen settlements;
  - practices with exposure, participation, stage, variant and first provenance;
  - party repertoires and settlers' origins;
  - arrival records, with bounded summaries;
  - the applied-keys dedupe list.
- `.../Culture/Local/ContactSnapshot.cs`: a pure contact graph built from road topology. Contact runs through the ruins of fallen settlements, and an edge is open when some route between the two is open. It never uses distance or territory.
- `.../Culture/Local/LocalCultureRules.cs`: pure rules for gathering, exposure, participation, adoption, quiet and revival, visits, founders, arrivals, repertoires, the Seventh, and recognition that keeps variants.
- `.../Culture/Local/LocalCultureViews.cs`: read-only snapshot views.
- `.../Culture/Local/CultureSystem.Local.cs` covers the culture side:
  - the local gathering command, with its preview and why-not text;
  - the admission boundary (`ArrivalAdmission`, `CultureSystem.Admitted`), plus founding and visit completion;
  - the `ICultureQuery` reads;
  - `LocalCultureFeature` (phase `LocalParticipation`).
- `Assets/GatewayToGenesis/Scripts/GameData/World/WorldSystem.CulturalContact.cs`: the world side.
  - A cached contact snapshot, invalidated when `CivilizationVersion`, the route count or the threat count changes.
  - Settlement ground words.
  - Party carry/drop commands.
  - The founding, settlers and festival hooks.
  - `CulturalContactMap` (pure).
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Local/LocalCultureCard.cs`: the settlement card and cultural-party card sections.
- Tests: `Tests/Editor/LocalCultureTests.cs` (14 pure) and `Tests/Editor/LocalCulturePlayTests.cs` (scene).

## Shared patches (small; for Agent A to keep at integration)

- `PopGrowthLogic.cs`: the admission boundary.
  - `AddMigrants(count, origin, channel, settlement)` reports each admission.
  - Gate lots (`_gateLots`, saved) remember what is known of each waiting group's origin.
  - `ReportAdmissions` reports gate admissions oldest first.
  - `_admissions` (saved) keeps the keys unique across loads.
  - Caravans and the founding band report no origin. Survivor bands report `survivors:<cell>`. People who waited from before the lots existed are reported as unknown. Ration costs and the population ledger are unchanged.
- `WorldSystem.cs`: four one-line hook calls.
  - `CulturalFounding` when settlers found a settlement.
  - `CulturalSettlersTaken` when settlers are taken on.
  - `CulturalTributaryFounding` when a hamlet is raised.
  - `SettlersReturnOrigin` when settlers disband and rejoin.
- `WorldSystem.Culture.cs`: `FinishFestival` calls `CompleteCulturalVisit` after `CelebrateFestival`, which remains the single completion owner. The visit adds only exposure and participation, never a second reward.
- `WorldView.cs`: two calls, `LocalCultureCard.Settlement` and `LocalCultureCard.Party`.
- Rootedness wording (presence is no longer shown as agreement): `CultureHud.cs`, `CultureWindow.cs` (Land section), `NationBanner.cs`, `WorldLenses.cs` (Culture lens) and `Resources/Library/GameWiki.md`. The wiki's Culture article now says "Rootedness", adds `Rootedness` and `Local Customs` as aliases (`Cohesion` kept for existing links), and has a short **Local customs** bullet. `GameWiki.md` is read at runtime; `Library.json` was not touched.
- `Resources/Library/Vault~/Canon Gaps.md`: the "Local cultures and routes of exchange" proposal entry.
- `Scripts/ARCHITECTURE.md`: the T02 paragraph.
- Requested but not done (A's registries): a `local` condition domain for Ink (`local:kept:<practice>`, `local:settlements_keeping:<practice>`) and a `ContentValidator` check that each `LocalPracticeSpec.tradition` names a real T01 definition.

## Player-visible behaviour

- **Settlement card (world map)** shows:
  - **Customs**: what the settlement keeps (quiet or recognised), each with its provenance: "it began here", "it came along the road from Ashford", "brought by Cultural Party of Vaelia (Vaelia) from Riverford", "carried by its founders from …".
  - **Known of**: exposure to customs it does not keep, with provenance.
  - **In contact by road**: which settlements, and "(interrupted: a threat at …)" when a road is cut.
  - **Arrivals**: counts from your settlements, found in the wilds (customs not recorded), and of unknown origin.
- **Gather for <custom>** (settlement card): 3 food value, +2 Unity and −4 strain in that settlement, once in 7 Sevenths per settlement.
  - A gathering begins a custom where the ground fits it, revives a quiet one, or takes up a custom the town has come to know.
  - Each choice shows a preview ("takes up …", "tries … (known 40% of 60%; taking part would reach 50% of 50%)") or why it cannot happen.
  - A custom the town has never met and whose ground is elsewhere is not listed at all.
- **Cultural party card:** the party's repertoire (at most 2 customs), "Take up <custom>" for customs kept where the party stands, and "Set down <custom>". At a festival the party performs its repertoire: the host's own customs gain participation, and each carried custom gives the host attributed exposure, once per festival.
- **Contact:** an open road gives 0.04 exposure per Seventh for each custom kept at the other end. Founders give 0.6, and people admitted from a known settlement give 0.3. Anonymous arrivals and survivors give nothing.
- **Adoption** needs 60% exposure and 50% participation (gatherings, festivals). Exposure fades by 2% a Seventh without contact. A kept custom goes quiet after 42 silent Sevenths and is never erased.
- **Recognition:** when the nation recognises a tradition (a T01 `Recognition` occurrence), every settlement keeping its local form is marked recognised and keeps its own variant ("Crossing Songs of Goldbough"). Nothing is merged.
- **Notices:** a custom taken up during the Seventh, or one growing quiet (keyed, so each notice appears once).
- **Authored customs:**

  | Custom | Canon status | Source | Ages | Where it can begin |
  |---|---|---|---|---|
  | Moonlit Vigil | Explicit canon | Civic.md | 0–III | Glimmerfern, groves or glades |
  | Sky Glass Burial | Explicit canon | Civic.md | 0–III | Peaks or Sky Glass |
  | Village Flavor Log | Explicit canon | Civic.md, Culinary Alchemists | I–III | Anywhere |
  | Rite of the First Golden Fruit | Canon-supported | The Inescapable Hunger | 0 only | Orchards or peach groves |
  | Crossing Songs | New game rule | — | All | On a river |
  | Evening of Song (local form of T01's The Evening Song) | New game rule | — | All | Anywhere |

## Save and edge behaviour

- An older envelope without `local` loads with a fresh, empty state (tested). Nothing is reconstructed for old saves: no invented provenance, arrivals or customs.
- On reload, `AfterRestore → LocalCultureFeature.Reconcile` makes sure every list exists and drops the cached contact snapshot and ground words. It grants nothing and replays nothing. T02 has no derived game effects to re-apply; its one-shot Unity and strain relief are paid at the gathering.
- **Duplicates:**
  - Visits are keyed `visit:<unit>:<settlement>:<festival count>`.
  - Foundings are keyed `founding:<settlement>`.
  - Admissions are keyed `admission:<n>`, with the counter saved.
  - All go into the bounded `applied` list (256), so a repeated callback or a reload never transfers exposure twice (tested in the pure test, and after a codec round-trip in the play test).
  - Occurrences are also deduped by T01's ledger.
- **Cancellation:** a party that disbands or is lost before its festival completes transfers nothing. Its repertoire and settlers' origin are pruned when the unit leaves the map.
- **Missing references:**
  - A fallen settlement's profile moves to `former` (history). A new settlement reusing its id starts empty.
  - Roads keep contact running through ruins.
  - An unknown practice id is ignored.
  - An unknown founder origin brings nothing.
- **No land moves:** adoption, contact, visits and arrivals never touch `authorityId` (asserted in both test files).

## Tests actually run

- Private build (session scratchpad `mkbuild.sh`, whole tree): 0 errors, no warnings in T02 files.
- Offline reflection runner:
  - `LocalCultureTests` 14/14.
  - Every `Culture*` pure test: 89 passed, 5 need the Unity runtime (Resources/Debug.Log; the same 5 as in T03's report).
  - `Tradition*`: 16/16.
- **Not run in this session:** `LocalCulturePlayTests` (the Unity Editor held the project, so batchmode was unavailable). T03's report recorded its earlier failure: `HamletSite` found no held cell on a random seed world. This session fixed that and a second latent defect:
  - `HamletSite` now takes held or wild ground within the hub's reach and holds it.
  - `ClearRoad` pins the hamlet's road open (no threat, no foreign authority).
  - The survivors step admits the waiting group in fed rounds and asserts on the Place total.
  - The whole scenario moved into a plain static method, because its lambdas captured iterator locals, which EnterPlayMode's domain reload loses.

  Run it from the Test Runner: `LocalCulturePlayTests.ACustomBeginsTravelsByRoadAndPartyAndArrivalsKeepTheirOrigins`.

## One acceptance path

1. Research Horology and answer "Why were we founded?", then name the nation.
2. On the world map, open the Capital's card and choose **Gather for Evening of Song**. The card shows Customs: "Evening of Song, it began here". A river settlement can begin Crossing Songs instead, and an orchard town the Rite of the First Golden Fruit: different towns, different practices.
3. Raise a tributary hamlet next to the Capital. Its card lists "Known of: Evening of Song (60%, carried by its founders from <Capital>)" and "In contact by road: <Capital>".
4. Form a cultural party in the Capital and choose **Take up Evening of Song**. Walk it to the hamlet and **Hold festival**. When the festival completes, its notice says the party performed it. The hamlet gains participation, and a second completion of the same festival adds nothing.
5. Choose **Gather for Evening of Song** in the hamlet: "takes up Evening of Song (carried by its founders from <Capital>)". Both towns now share the custom, each in its own variant.
6. Put a threat on the road (or let another authority take a cell of it). The card shows "(interrupted: …)"; a custom begun in the Capital afterwards does not reach the hamlet, but the hamlet keeps its Evening of Song.
7. Let caravans arrive. The Capital's card counts them as "of unknown origin", and they bring no custom.

## Canon proposals

These are recorded in Canon Gaps.md ("Local cultures and routes of exchange"). Crossing Songs and the local Evening of Song are new game rules; the vault names no river custom. Keeping Moonlit Vigil, Sky Glass Burial and the Flavor Log in one settlement before any civic is an adaptation. Every number, the ground words, what interrupts a road, and the "<custom> of <settlement>" variant naming are all proposals.

## Known limits

- Local participation is a cultural model. It does not stand for per-settlement warehouses or demographic cohorts: the pantry stays global, and no local population count is inferred.
- The Culture window has no Local panel. The settlement and party cards are the UI, and T10's atlas can compose `LocalProfiles()` / `ContactEdges()` through `ICultureQuery`.
