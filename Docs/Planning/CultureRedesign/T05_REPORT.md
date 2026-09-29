# T05 completion report: hospitality, redistribution and access to culture

Agent B, Wave 2, 28 September 2026. Built on T01's contract (`CulturalOccurrence`, `CultureCommandResult`, `CultureExtensionState`, `ICultureFeature`, `ICultureQuery`), T02's contact graph and arrivals, and the shared serving boundary in `Culture/Integration` (`CultureServing`, `CultureSystem.Serving.cs`). T04 uses the same boundary for observances. T05 does not depend on T04.

Player promise: "A feast changes who belongs at the table."

## Owned files

- `Assets/GatewayToGenesis/Scripts/GameData/Culture/Hospitality/HospitalityModel.cs` holds the saved state, inside `CultureExtensionState.hospitality` (`[SaveOptionalField]`):
  - `TablePolicy`: PublicWelcome, RecoverySupport, PatronHosted. It is append-only.
  - `TableRecord` (`table-N`): its policy, settlement, patron, served lines, the groups represented, guests by road, Legends present, the stamp and whether a luxury was shared.
  - `ServedLine`: the resource plus the recipe id, the portions and food value, luxury categories, and invented/national flags.
  - Bounded history (40 tables), with per-settlement `SettlementTables` and per-patron `PatronRecord` summaries that keep the totals.
  - `HospitalityTuning` holds every number. All are proposals.
  - `TableRequest`.
- `.../Hospitality/TableServing.cs` is pure. It holds a table's menu and its policy checks:
  - 1–3 finished foods (edibles, teas, drinks), with portions from each food's own food value.
  - Ingredients and spices are refused, with the reason "cook it into a dish first (the Kitchen), where its recipe is checked".
  - No table while Food is falling, and none that draws the stores below the survival reserve.
  - The stock check is the boundary's own `CultureServing.WhyNot`.
  - `Settle` makes the record match what the boundary actually took.
  - It contains no spending code.
- `.../Hospitality/HospitalityRules.cs` is pure:
  - effects by policy, table size, cooldown and patron rest;
  - coverage per settlement (never per head) and its bounded living contribution;
  - the access report;
  - served names through a rename.
- `.../Hospitality/CultureSystem.Hospitality.cs` covers the scene side:
  - servable foods, patron choices and the survival reserve;
  - `PreviewTable` and `SetTable`;
  - snapshot reads (`RecentTables`, `TablesAt`, `TableAccess`, `SharedTableCoverage`, on `ICultureQuery`);
  - `HospitalityFeature` (phase SocialEffects, reconcile only).
- `Assets/GatewayToGenesis/Scripts/UI/Culture/Hospitality/HospitalityPanel.cs`: the Tables panel and the window-body section.
- Tests:
  - `Tests/Editor/HospitalityTests.cs`: 16 pure tests.
  - `Tests/Editor/HospitalityPlayTests.cs`: a ClickerScreen scenario.
- Lane B files extended (T02): `LocalCultureRules.Table` (an open table exposes customs between host and guests, once per table key), `PracticeChannel.Table` (appended) with its provenance text, and the settlement card's "Set a communal table…" action in `LocalCultureCard`.

## Shared patches (already in the tree; for Agent A and Agent C to keep at integration)

- **Agent A.**
  - `CultureLifeRules.WellbeingInputs.sharedTables` and its term in `Living`, apart from the luxuries' demand.
  - `CultureSystem.Life.cs` `WellbeingNow` fills it from `HospitalityRules.LivingFrom(SharedTableCoverage())`.
  - The luxury allocator is unchanged.
- **Agent C.** `CultureWindow.cs` gains:
  - `LifeMode.Tables`, the "Tables" button, the `OpenTables(settlement)` deep link, `HospitalityPanel.Reset` on open, `Fill` in the life switch, and `Describe` in the body.
  - These are one-line registrations. T10 may recompose them.
- **Docs.** `Scripts/ARCHITECTURE.md` gains the T05 paragraph. `Resources/Library/Vault~/Canon Gaps.md` gains "Hospitality and shared tables".
- **Serving boundary.** An earlier draft had its own spending `ServingAdapter`. It was removed so there is exactly one atomic spend path: `CultureSystem.Serve(items)` in Culture/Integration. No change to A's serving files was needed.
- **Not requested.** No new condition domain. The traditions already listen for `Hospitality` occurrences (the Ash-Loaf Table by recipe, the National Table by resource).

## Player-visible behaviour

- **Where the player sets a table:**
  - Culture window → **Tables**. The overview shows every settlement's access, recent tables, and access problems in plain words.
  - Choosing a settlement, or its world-map card's "Set a communal table…", opens that settlement's table.
- **Policies** (numbers are proposals):

  | Policy | Size (food value) | Unity | Strain eased | Reach | Special |
  |---|---|---|---|---|---|
  | Public welcome | 6 + 0.05/development | +3 (+2 under the Feast of Abundance) | 6 | all of the settlement | Neighbours by open road come: 0.15 exposure to each other's customs (T02); a luxury counts as shared openly (+4 joy) |
  | Recovery support | 4 + … | +1 | 15 | all | Only where strain is 20 or more; a luxury is shared openly |
  | Patron-hosted | 5 + … | +5 | 2 | 25% | A Legend at home hosts; 1 Meaning fragment of standing at most once in 21 Sevenths; a luxury stays private |

- **Preview** (nothing is paid until "Set the table"):
  - the planned portions of each food and the food value;
  - the stores before and after, and the survival reserve (0.25 food value per citizen);
  - who is represented, only as far as known: "the people of X", recent arrivals by their reported origin (unknown stays unknown), Legends whose parties stand there, guests by open road;
  - the gains, and why it cannot be set now: cooldown (7 Sevenths per settlement), not strained, no patron, patron away, hunger, the reserve, or what the stores lack.
- **On success:**
  - the exact foods leave the stores;
  - one `Hospitality` occurrence is recorded per food, carrying its recipe id;
  - the settlement's strain is eased and its coverage lasts 21 Sevenths;
  - a chronicle "first shared table" is added (once);
  - a notice opens the Culture window.
- **Access:**
  - Luxuries held while no open table has shared one in 21 Sevenths are named ("Held but not shared: 12 Honeyed Peach Tart").
  - So are luxuries served only at a patron's table ("In Ashford, Sweets reached only a patron's guests"), and luxuries shared in some towns and not others.
  - A poor settlement's plain shared pot counts as a full place at the table without claiming any luxury.
- **Wellbeing:** coverage adds up to +0.1 living (of 1), separate from and additional to luxury demand. Five tables in one town still reach only one town.

## Acceptance criteria

- **Serving cannot count the same batch three times** (feast, luxury draw, ordinary consumption):
  - Served portions leave the stores at commit, so the luxury allocator and hunger cannot draw them.
  - `Pantry.Eaten` is never raised (the play test counts 0).
  - The only social record is the table's own `Hospitality` occurrence. A generic `Pantry.TrySpend` is never used.
- **A public meal differs from hoarding the same dish:** the effects differ by policy (`APublicMeal_AndAPatronsTable_HaveDistinctConsequences`). The same Sweets at a patron's table show as private-only, and held Sweets never shared show as hoarded (`LuxuryMonopolization_IsAVisibleAccessProblem`; the play test with a real patron).
- **Insufficient stock fails without partial rewards:**
  - The table's checks run, then the boundary checks every line before spending any.
  - The play test drains the soup, and the table fails with stock, Unity, strain and history unchanged (`InsufficientStock_RefusesTheWholeTable`).
- **National-food familiarity records actual use once:** the boundary calls `RecordUse` once per food with the exact amount. The play test reads `_usedThisSeventh`.
- **Recipe rename/load keeps references:** lines store the recipe id, and `ServedName` resolves the current dish.
  - `AServedRecipe_KeepsItsIdentity_ThroughARename` covers the rename.
  - The play test serves an invented dish and finds it by recipe id after a culture save and restore.
- **Raw custom inputs never bypass recipe validation:** ingredients and spices are refused at planning. Only stored finished foods can be chosen, and invented dishes exist only through `Invent` and the kitchen.

## Save, reload and edge behaviour

- **Old saves:** an older envelope loads with empty hospitality (`HospitalityState_SurvivesTheSaveCodec_AndAnOlderEnvelopeLoads`). Nothing is reconstructed from `Foodway.eaten`.
- **Repeated load:** tables act when set, never per Seventh. `HospitalityFeature` only normalises lists, so a reload or a Seventh grants nothing. The cooldown and patron rest are saved (the play test checks the cooldown after restore).
- **Duplicate events:** each occurrence key is `hospitality:table-N:i` and the serial is saved. T02 contact is applied once per table key.
- **Missing references:**
  - A table whose settlement has fallen keeps its saved name.
  - A recipe no longer known shows the name it was served under.
  - A menu food no longer held shows "(no longer held)" and can be set aside.
- **Cancellation:** a table commits at once. There is no reservation, so there is nothing to cancel or expire. Stock lost between preview and commit fails the commit.
- **Abstraction, labelled:** the pantry is global. Coverage is per settlement, and the panel says the simulation keeps no head count.

## Tests actually run

- Private compile (`mkbuild.sh`, runtime and editor): 0 errors.
- Offline pure runner: `HospitalityTests` 15/15, `LocalCultureTests` 14/14, `TraditionTests` 16/16, `CultureLifeTests` 31/31 (5 need Unity), `CultureMemoryTests` 17/17, `CultureInventionTests` 6/6, `CultureTransmissionTests` 10/10, `Pantry*` 7/7.
- Unity batchmode EditMode, 00:47 run (filter: hospitality, local culture, content, save, tradition, culture life, observance):
  - 53/55 passed, including `HospitalityPlayTests` and all 15 `HospitalityTests`.
  - `LocalCulturePlayTests` now passes (T02's earlier fixes are verified).
  - `SaveTests` 10/10 and `TraditionPlayTests` passed.
  - The 2 failures belong to T04 (Agent A):
    - `ContentTests.AllContentValidates`: observances `moonlit-vigil` and `sky-glass-burial` end in Age 3, "which does not exist".
    - `CultureLifePlayTests.AFoundedCultureCooksCelebratesAndKeepsAHoliday`: "kept again on its day", expected 2, was 1.
- Unity batchmode EditMode, 00:50 run, after the recovery/invented-dish case was added to `HospitalityPlayTests` (filter: hospitality, local culture, tradition, culture life): 19/20 passed.
  - `HospitalityPlayTests` passed: public welcome, the refusal for insufficient stock, the ingredient refusal, a patron's private luxury (that branch runs only when a Legend is at home in the scene), recovery support with the invented "Hearth Crumble" by recipe id, and the culture save and restore.
  - The one failure is again T04's `CultureLifePlayTests` holiday assertion.
- Not run: the full EditMode suite (other agents were editing observance files; one private build had to exclude A's half-written `ObservancePlayTests.cs`).

## One acceptance path

1. Start a game and found the culture. Cook some Peach Soup, or hold any finished dish.
2. On the world map, open the Capital's card and choose **Set a communal table…**.
3. Keep **Public welcome** and tick Peach Soup. The preview shows the portions, the food value, the stores before and after, the reserve, who is at the table and the gains.
4. Set it:
   - the soup leaves the stores, Unity rises and the Capital's strain eases;
   - Tables shows the Capital reached;
   - the Traditions panel may show the National Table credited if the soup is a national food.
5. Try again at once: it is refused, "set a table not long ago: 7 more Sevenths".
6. Hold Honeyed Grain Porridge and choose **Patron-hosted** with a Legend at home. After it is set, Tables names "Sweets reached only a patron's guests".
7. Invent a dish in the Kitchen, cook it, and wait for strain of 20 or more somewhere (or a crisis). Set **Recovery support** with the invented dish: strain eases by 15.
8. Save and reload: the tables, names, cooldowns and access are unchanged, and nothing is granted again.

## Remaining defects and proposals

- Every number is a proposal (Canon Gaps "Hospitality and shared tables"). Recovery support is a new game rule. The public welcome and the patron's table are canon-supported adaptations of the Cozy Inn and the Feast of Abundance.
- The Feast of Abundance civic only adds Unity to open tables. Its canon hoarding tax is not modelled. Hoarding is only named in the access report.
- There are no per-settlement warehouses. A table in a far settlement draws on the global stores, as festivals do.
- Not built (outside T05): a hospitality promise and dispute (T09 reads `TableRecord`/`Hospitality` occurrences), holiday tables (T04 uses the shared boundary itself), and Legend relationships from sitting at a table (T08's `ShareExperience`; tables record the Legends present for it).
