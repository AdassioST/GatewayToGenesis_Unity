# Settlement stories and Lesser Opus endpoint

Updated 2026-09-27. Four new three-verse Ballads, twenty miscellaneous events and five player-triggered issues are authored in `Assets/Resources/Events/SettlementBallads.ink`, `SettlementMiscellany.ink` and `SettlementIssues.ink`, with compiled JSON beside each source. Existing volumes remain independent.

## Canon and scope

These are original canon-compatible placeholders, not transcriptions. Sources reviewed in the Arcanoria vault: `Society/Stellar Legacy/Ballad.md`, `Fate Stage.md`, `Stellar Legacy Score.md`, `Legend Opus.md`; `World Environment/Landmarks/Memory Field.md`; Age of Desolation and Hunger material. Ballads are local multi-event stories. Memory Field preserves moments, without granting time travel or reliable knowledge of their subjects. The specific canonical well story, A Burrowed Name, remains reserved for its own gates. No hidden Truth, Magnum Opus or canonical named feat is awarded here.

Owner correction: legend Ornaments are obtainable in Age 0. Ornamental Magic belongs to Symphony of War and other symphonic cards; settlement Ornament gates are separate.

Lesser Opus are automatically earned at authored resolutions. Corresponding titles appear on the legend as locked prospects for later fragment purchases. This endpoint grants no title ownership, deducts no fragments and adds no progression bonus or achievement entitlement.

## Four Ballads

Each has three episodes and two outcomes per episode. Both opening routes unlock the next verse. Later episodes require prior completion and two quiet Sevenths. The opening determines the supported finale.

- `sb_orchard_1..3`, **The Measure of an Orchard**: Auric Orchard's abundant sweetness threatens older crop knowledge. Preserve seeds or take a necessary harvest, then return a crop record or honestly account for its cost. Lesser Opus: **Three Empty Seed-Pouches** / **The Harvest's Full Account**. Locked titles: Keeper of the Unsown / The Honest Harvester.
- `sb_crossing_1..3`, **A Road with Two Names**: two Reedwater Crossing guides bring their quarrel to council. Measure conflicting routes or listen to their fears; preserve both corrections or their promise to return together. Lesser Opus: **A Map with Two Names** / **Where We Can See One Another**. Locked titles: The One Who Measured Twice / Keeper of the Return Path.
- `sb_threshold_1..3`, **The House That Kept a Place**: a mother in Survivor Hamlet keeps a chair for someone missing. Search or sustain her home; an ambiguous coat tests whether people can welcome the living without inventing closure. Lesser Opus: **The Question That Kept Travelling** / **A Chair Turned Sideways**. Locked titles: Bearer of the Unanswered Name / The One Who Kept a Place.
- `sb_stillhour_1..3`, **The Hour That Would Not Leave**: in Age I, Memory Field preserves a hand reaching toward an empty bowl. Observe or keep a witness outside; answer hunger among the living, preserving uncertainty or the voice that brings companions home. Lesser Opus: **The Bowl in the Quiet** / **The Voice Outside**. Locked titles: Witness of the Honest Blank / The Answering Voice.

## Twenty miscellaneous events

The IDs below have the `sm_` prefix. Each has two fragment-bearing resolutions and three quiet Sevenths between random offers, subject to local gates.

1. `caravan` — Wheels at the Outskirts: receive a caravan after Reconstruction.
2. `extra_bowl` — The Extra Bowl: share supper.
3. `seed_names` — A Name for Every Seed: preserve Resting Field knowledge.
4. `wet_timber` — The Timber That Would Not Burn: work with damp Elderwood.
5. `stone` — The Crack in the Duskstone: account for imperfect material.
6. `windmill` — A Tooth in the Windmill: maintain an Ancient Windmill.
7. `watchlamp` — The Last Watch Lamp: support a Hollow Watchpost.
8. `quarrel` — Whose Map Is It?: answer an expedition party's dispute.
9. `sock` — The Last Dry Sock: acknowledge an exhausted expedition's needs.
10. `path` — The Shorter Way Home: weigh returning travellers' knowledge.
11. `resonator` — Cloth for a Broken Resonator: tend a discovered ruin.
12. `tools` — The Borrowed Adze: negotiate shared tools after Reconstruction.
13. `window` — A Window Facing Nobody: notice an overlooked home.
14. `barrel` — The Barrel at the Back: allocate orchard surplus.
15. `burial` — The Name on the Board: remember a true death.
16. `spire` — A Lesson in the Ruin-Song: respond to the Silent Spire.
17. `grove` — The Basket from the Grove: share the Moonlit Grove's work.
18. `letters` — Letters on a Crate: use written knowledge.
19. `rain` — A Roof for the Rain: care for leaking shelter.
20. `return` — Supper for the Returned: acknowledge a completed journey.

## Five triggerable issues

Issues appear in the notification feed while live conditions hold. Clicking rechecks availability; issues never enter random selection or interrupt an active story/pending offer.

- `si_hunger`, An Empty Evening Measure: Food at most 10 and negative net Food production; temporary production support or a truthful ration account.
- `si_shelter`, Names Without a Roof: at least one vagrant; spend timber or establish shared space. Housing changes persist, rather than using unsupported timed housing effects.
- `si_fracture`, A Quarrel After the Mishap: the same expedition has companions and a mishap; answer its dispute.
- `si_exhaustion`, The Road Has Become Too Long: expedition fatigue at least 40; council responses do not silently issue movement, rest or supply orders.
- `si_mourning`, The Work of Naming the Dead: at least one true death; shared food or written remembrance, without changing the death ledger.

All 37 entries are once per world, restricted to Ages 0–I, and require population and a met legend. Memory Field additionally requires Age I. Landmark gates require explored exact feature IDs. Roles use existing cast assignment; recruited participating actors receive rewards. Expedition reports do not invent expedition party membership for council actors. Most explicit miscellaneous fragment awards are two or three, in addition to existing themed actor rewards; all balance values remain prototypes.

## Runtime and persistence contract

The Resources loader discovers compiled volumes. `# issue: true` selects the live issue feed. `lesser_opus:cast <catalog-id> +1` is a validated permanent consequence on each of eight finale outcomes. Completion applies ordinary consequences, records actual participation and existing actor rewards, then awards Lesser Opus.

A legend receives at most one resolution per Ballad. Late replacements receive only their own verse credit. History snapshots authored wording, age, role and source; it survives with retained lost-legend records. The legend tooltip displays Lesser Opus and locked title prospects. Older saves initialize the optional `balladHistory` field empty; unknown, duplicate and missing required fields remain errors. Previously completed stories do not receive invented retroactive credit.

## Verification and handoff

Verified: bundled Ink compilation, all 37 entries and 74 terminal branches, serialized Ink continuation, full runtime and editor test assembly compilation. Existing obsolete Unity API warnings remain.

Executed against production assemblies in a standalone runner: reward paths and supported timed effects; forward chains and reachable finales; actual participation and duplicate prevention; history serialization; old-record defaults and rejection of unknown/missing required save fields. Game metadata checks caught and corrected quiet-Sevenths syntax. Disposable runner: `Library/SettlementStoryValidation`.

Checked-in tests: `SettlementStoryTests.cs`, `LegendBalladHistoryTests.cs`. Native Resources/catalog checks, notification interaction, save-menu flow and visual tooltip review still require a Unity Editor run. Standalone checks are not scene playtests.

Next task owns title catalog and costs, atomic fragment spending, ownership/equipping, full legend progression and any new reward effects. Reuse stable Lesser Opus IDs and outcome provenance. Decide how typed-purse spending interacts with existing total-fragment rank calculations before enabling purchases. S12/S15 and broader achievement integration remain open.
