# Expedition world rules handoff

This endpoint wires the expedition road rules through the map, simulation and world UI.

## Implemented

- `Expeditions.Burden` includes rations and weighted cargo. `Expeditions.Effective` turns the current burden into a cached pace and travel-fatigue multiplier; `WorldSystem.SpecOf` invalidates that cache as supplies or cargo change.
- Cargo weighs by resource (`ExpeditionSettings.cargoWeights`, 1 when unlisted). The pack cap (`cargoPerLegend`) and the harvest share are measured in that weight too (`WorldSystem.CargoLoad` is weighted), so stone and timber fill the packs sooner than herbs and wool. Serialized proposals: Duskstone 2.5; Sky Glass, Elderwood 2; Peat, Game Meat 1.5; Hides, fish, grains, roots 1; Wild Honey 0.8; Dried Auric Peaches 0.6; Silverreed 0.5; Lumenwool, Glimmerfern, Emberwhisper, Lunehymn 0.4; Aetherlight, Eleos Tea 0.3.
- `WorldSystem.Forage` and its preview scale forage by local fertility, living-site bounty and Resource Grandfield density (`WorldUnits.ForageRichness`).
- Field expedition cards expose carried weight, load-at-ease, pace (with the travelling-light / burdened line) and cargo. Each cargo resource can be discarded at half or in full through `WorldSystem.DropCargo`; the button shows the weight freed. Discarded valuables are permanently lost and the party spec is refreshed.
- `WorldResources.Refresh` derives tile solace from moonlit-grove ground and living sites. Silver-river and silver-fed-lake distances, beauty, and fallout are combined by `Expeditions.Solace`; `LegendProgress` passes the result into Composure recovery. The serialized world gives moonlit ground and Glimmerfern sites increasing solace, with Glimmerfern Lakeshore strongest.
- `WorldVibration` derives per-tile Vibrational Density, permanent Fallout and edge Cascades from the magic fields. Fallout increases micro travel cost, provisions attrition, Composure hardship and mishap risk; the world hover card describes the field.

## Verification

- Unity batchmode, Sept 28 2026: focused filter `ExpeditionTests|WorldUnitTests|WorldResourcesTests|WorldViewPlayTests|ExpeditionPlayTests|SurveyPlayTests|WorldRuinTests|WorldTributaryTests|LegendSoulTests|SaveTests` passed 159/159 (play tests included). Full EditMode suite: 669 passed, 0 failed, 1 ignored.
- `ExpeditionTests.Weight_CargoAndRationsChangePaceAndTravelFatigue` had a fixture mismatch (5 Sky Glass at weight 2 is 10, the test expected 20); the fixture now carries 10.

## Next

Tune the serialized `ExpeditionSettings` (load, cargo weights, solace) and `VibrationSettings` values against playtest pacing.
