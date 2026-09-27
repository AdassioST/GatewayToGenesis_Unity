# Sources and decisions

Reviewed 2026-09-27 against the current working tree and original vault; owner clarification D13 takes precedence over the vault typo. Source hierarchy: explicit owner decisions → specific lore notes → general lore/system notes → established implementation roadmap → proposed mechanics. Where notes conflict, record a decision rather than inventing a resolution. The vault is the local `C:/Arcanoria Master/Arcanoria` directory. Imported Library text is a derived presentation source, not a replacement for original lore. The shared external tracker mentioned in the old roadmap was not verified or updated in this completion; the repository is the delivered plan.

## Decisions required before dependent implementation

### D01 — Act timing and numbering
Owner: Design. Blocking: S02-A/B.
Ages specifies three Acts normally and four for long Ages (II and V are explicit). Choose exact durations, timer catch-up behavior and whether boundaries await player resolution. Author counts per Age rather than deriving a zero-based modulo. Hidden XIV must remain a separate progression record.

### D02 — Age type and historical classification
Owner: Design. Blocking: S03-A/B.
Canon specifies one-stage Triadic Pivots, excess Era Score as a crisis buffer, and a 50/50 historical weighting involving the Acts with the greatest Era Score and Lyrical Fragments. It does not fully specify tie resolution, normalization, zero-fragment histories or Requiem/Cadenza evaluation against final versus transient types. Approve worked examples before writing these rules; keep Age 0's fixed Renewal destination.

### D03 — Famine survival and representation
Owner: Design + narrative. Blocking: S04-A.
Age of Desolation explicitly says both outcomes reach Renewal with differing survivors. The famine note also uses broad collapse language. Use the specific Age transition as the slice contract; decide a survivor floor/total-extinction behavior explicitly. Propose ecological/dietary-risk variables over current single-Food economy for the prototype. Define warning cadence, preparation options, crisis duration and loss curves through playtests, not invented canon.

### D04 — Ending and achievement conflict
Owner: Narrative. Blocking: S23-B, X11 final rewards.
Achievement maps The Purest of All Love to wishing for the End of the Third Actor. Cataclysmic Aftermath assigns it to Reset of Arcanoria, and describes End of the Third Actor as the continuation ending. Resolve ending names, branches, continuation/retry behavior and reward mapping in one canonical note. Define the 100% denominator so the final achievement never requires itself. The earlier owner authorization already permits the implemented sacrifice API and its profile award. Final playable branch labels, canonical reward mapping and #97 completion policy still need reconciliation; build route fixtures without silently changing that authorized behavior.

### D05 — Reset carryover and stage semantics
Owner: Design + narrative. Blocking: S04-B, S05-A.
The older roadmap's generic board-wipe summary is insufficient for the specific Great Filters. List exactly what survives each filter: technologies, truths, achievements, constellations, currencies, world deltas and surviving people. Distinguish named Second/Third/Fourth Reset from repeated Divine Reset count. Verify all intermediate Age routes, including VII, instead of relying on a prose list that skips it.

### D06 — Persistence authorized; prestige and Ironman policy remain
Owner: Project owner + design. Status: implementation hold lifted by the earlier explicit owner request, recorded in SAVE_SYSTEM.md. Blocking: final prestige/Ironman acceptance, not ordinary save implementation.
World saves, lifetime profile, per-world awards/debits and sacrifice foundation exist. Current generator is 8 and save schema is 3; current Read rejects other versions/catalogs rather than providing general migration. Define upgrade costs/effects, reset carryover, map-anchor versus prestige terminology and Ironman crash recovery/manual rollback policy. The +50% fragment rounding and stacking question is D12. No new persistence permission is required by this roadmap.

### D07 — World and settlement tuning
Owner: Design. Blocking: authority/trade/suzerainty acceptance and S08-B.
Hex/quadrant generation is established game design rather than a lore fact. Agree authority distance/connectivity, capacity, spacing, outpost rules, route costs and enclave integration. Canon requires settlement Ornaments only after The Truth and three entire Ages; decide what destruction, loss, reclamation and resets do to continuous settlement history.

### D08 — Legend and Atonalis progression
Owner: Design + narrative. Blocking: final balance of S12/S13/S21.
Approve fragment recipes, Myth Grade/drift/purification, Apex progression, composure thresholds and stage transitions. Preserve distinct Atonalis origins: collective suffering must not acquire a fabricated former-person Soul-Key. Atonalis lore includes Nascent; do not start the model at Sectile merely because the old roadmap abbreviates the list.

### D09 — Society and rival mechanics
Owner: Design + narrative. Blocking: final acceptance of S16–S18 and S25.
Religion/Piety, atrocities, condemnation and rival logic have varying levels of specification. Define stable-Piety duration, law history, cover-up evidence, revolt thresholds and rival decision rules. Treat empty or very short notes as design gaps, not permission to label proposed rules as canon.

### D10 — Combat, magic and resonance budgets
Owner: Combat design + engineering. Blocking: production acceptance of S19/S20/S24.
Approve timing windows, input accessibility/assistance, card economy, failure/retreat rules and Static Criticality thresholds. Prototype the complete composition/performance interaction before investing in large content sets. Keep authored Age restrictions independent of engineering delivery order.

### D11 — Achievement evidence, reward recovery and completion lifetime
Owner: Integration + persistence + design. Blocking: U00 award acceptance and U06/U07 completion.
Agree stable event/entity IDs, history lifetime, title-slug aliases, and recovery when Tracker, world ledger and lifetime profile persist at different times. Define per-world versus lifetime progress for cumulative sets and mutually exclusive campaign outcomes. Specify a versioned completion set excluding #97 itself and whether #96 is part of the same final transaction. Existing per-world currency awards do not settle prestige carryover or completion scope. Save/restore must never replay earned currency; profile imports do not grant world currency.

### D12 — Underdog ×3 and Ironman stacking
Owner: Design + legacy + modes. Blocking: The Stone The Builders Rejected and final fragment economy acceptance.
Achievement.md requires ×3 Lyrical Fragments from one Underdog action. Current LyricalFragments uses a provisional threshold of 75 and ×2 against the odds; Gateway To Genesis.md grants Ironman +50%. A multiplicative combination could produce ×3, but this is an inference, not an approved rule. Decide whether this is the intended route, the qualifying base award, eligibility and rounding order. Preserve the action's base/actual award and mode evidence; do not rewrite canon or change the base multiplier just to satisfy the achievement.

### D13 — Owner correction: legend Ornaments are allowed in Age 0
Owner: Project owner. Status: resolved by explicit instruction in this chat on 2026-09-27.
The Age of Desolation note's blanket prohibition is a vault typo. Legends can acquire Ornaments; the locked feature is Ornamental Magic in Symphony of War and other symphonic cards. Legend awakening and its achievements must not be gated off solely because the Age is 0. Model magic/card availability independently. Settlement Ornaments retain their separate Truth-of-Arcanoria and three-complete-Ages prerequisites from Arcanoria.md. This clarification supersedes older X05/S04-A wording; the vault itself was not edited here. D10 still determines the detailed card catalog and availability schedule beyond this distinction.

## Evidence and limits

- AgeProgression calls GameAge.Set and reports AgeSurvived. Desolation/Renewal and Pantry are implemented prototypes; placeholder Age II is not completed campaign content.
- CivicRequirement.ToCondition maps EraUnlock to the age/age_reached values and IsMet fails closed (2026-09-27); AgeCapabilities carries D13's independent magic/legend distinction.
- AchievementTriggers has twelve predicates. SaveSession.Sacrifice separately merges the-purest-of-all-love into the profile; the final choice story is still outstanding.
- SaveSession, GameSnapshot and SaveData implement persistence; current save version is 3 and WorldGenerator.Version is 8. Old comments saying memory-only are not evidence that storage is absent.
- WorldCivilization has settlements, routes, enclaves and influence/suzerainty foundations. WorldSystem increments agesPresent at transitions; U02 requires full-Age interval evidence rather than assuming this proves three complete Ages.
- LyricalFragments, LegendProgress, BalladActors and Pantry are implemented. Deed text is not a deduplicated action identity; U03 closes that evidence contract.
- Achievement.md in the original vault and imported Resources note matched SHA-256 during review. The plan routes all 97 parsed entries and excludes the requirement-less Critically Thinking Hater line.
- Extensive uncommitted work remains shared. This documentation update does not certify compilation or gameplay; previously reported licensing/test results must be re-established by X02. Plan checks validate IDs, coverage and links only.

## Source index

Task source titles below resolve to these original files. The plan's new acceptance criteria and dependency choices are engineering proposals informed by these sources, not verbatim lore requirements.
- [Achievement](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Events/Achievement.md>)
- [Act of Fate](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/The One Symphony/Act of Fate.md>)
- [Age Crisis](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Crisis/Age Crisis.md>)
- [Age of Desolation](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages of Magic/Ages 0/Age of Desolation.md>)
- [Age of Renewal](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages of Magic/Ages 1/Age of Renewal.md>)
- [Ages](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Passage of Time/Ages.md>)
- [Arcanoria](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Arcanoria.md>)
- [Atonalis](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Root of Evil/Atonalis.md>)
- [Atrocity](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Power/Atrocity.md>)
- [Ballad](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Ballad.md>)
- [Cataclysmic Aftermath](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Crisis/Cataclysmic Aftermath.md>)
- [Civic](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Foundation/Civic.md>)
- [Combat System](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Game Systems/Combat System.md>)
- [Composure](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Spellweaving/Composure.md>)
- [Developing Town](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Places/Developing Town.md>)
- [Dual Confluence Stream](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Dual Confluence Stream.md>)
- [Enclave](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Enclaves/Enclave.md>)
- [Fate Stage](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Fate Stage.md>)
- [Gateway To Genesis](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Gateway To Genesis.md>)
- [Legend Relationship](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Legend Relationship.md>)
- [Legend Trait](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Legend Trait.md>)
- [Magic Arts](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Atonalis/Spellweaving Rituals/Magic Arts.md>)
- [Major Settlement](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Places/Major Settlement.md>)
- [Memory Field](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Landmarks/Memory Field.md>)
- [Piety](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Faith/Piety.md>)
- [Scorching Truth](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Truths, Chaos, Rituals/Inescapable Truths/Scorching Truth.md>)
- [Static Criticality](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Static Criticality.md>)
- [Stellar Legacy Score](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Stellar Legacy Score.md>)
- [The Hollowing](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Events/World Events/The Hollowing.md>)
- [The Inescapable Hunger](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md>)
- [The Truth of Arcanoria](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Truths, Chaos, Rituals/Inescapable Truths/The Truth of Arcanoria.md>)
- [ROADMAP.md](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/ROADMAP.md>)
- [ARCHITECTURE.md](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/ARCHITECTURE.md>)
- [GameAge.cs](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/Core/GameAge.cs>)
- [CivicData.cs](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/GameObjects/CivicData.cs>)
- [AchievementTriggers.cs](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Achievements/AchievementTriggers.cs>)
- [AchievementTracker.cs](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Achievements/AchievementTracker.cs>)
- [ProjectVersion.txt](<C:/Arcanoria Master/GatewayToGenesis_Unity/ProjectSettings/ProjectVersion.txt>)
