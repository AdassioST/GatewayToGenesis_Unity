# Population, food and settlement crowds

Implemented September 28, 2026. This replaces the earlier Civ/Stellaris proposal. The owner chose one person per citizen at every scale and asked that historical evidence take precedence over canon population examples.

## What is implemented

`GrowthSettings` at `Resources/Population/Growth` supplies units and explicit scenario assumptions. `GrowthRules` contains dimensioned pure functions. `PopGrowthLogic` integrates these with the calendar, food ledger, housing, stories and saves. `GlobalCharacterManager` renders a pooled sample; it never owns demographic state.

One Cycle is one year: 21 Sevenths per phase, three phases per Echo, four Echoes per Cycle = 252 Sevenths. At the standard 180 seconds per Seventh a year takes 12.6 playing hours. There is no hidden demographic time multiplier. This deliberately prevents a town becoming a metropolis in a few in-world months. The current Age story schedule remains a narrative schedule, not a reconstruction of historical era duration.

## Food has units

One Food is defined as a 2,100 kcal ration equivalent. The default mean requirement is also 2,100 kcal per person per day. This is a population planning baseline, not an assertion that every adult, child or worker needs the same diet. FAO describes energy requirements varying with population composition and activity and gives 2,100 kcal/day as an emergency planning baseline:

- https://www.fao.org/4/y5815e/y5815e0b.htm
- https://www.fao.org/4/x8622e/x8622e05.htm

Rations per person-day = requirement / kcal per Food * (1 + distribution allowance * remaining loss modifier).

Demand per real gameplay second = all residents * rations per person-day * 365.2425 / (252 * effective seconds per Seventh).

The default distribution allowance is 20% of food eaten, a transparent scenario assumption. It is an extra provisioning allowance, not a measured universal historical percentage of deliveries lost. Existing ration technologies reduce this allowance; they cannot reduce basic calorie needs to zero. Thus the default community provisions 1.2 Food per person-day, or 438.291 per person-year. A million residents require a million times the food of one resident, not an exponential amount. No small-population free-food buffer remains.

Both housed and unhoused people consume food. Food storage remains available when housing is full. The Food meter now shows reserves rather than progress toward a birth. Supply-supported population comes from current net food production plus resident consumption, excluding Pantry withdrawals; withdrawing reserves is not sustainable production. This is an instantaneous supply indicator, not an agricultural land or trade simulation.

## Births, deaths and migration are separate

The shipped preindustrial scenario uses 35 births and 30 baseline deaths per 1,000 residents per year: 0.5% net natural growth with adequate food and no additional health pressures. These are chosen scenario rates, not universal historical estimates. Crude-rate models omit age structure, sex ratios, gestation and household-level variability. A population of 10,000 therefore expects 350 births and 300 baseline deaths over a year at fixed exposure. Fractions persist across calendar ticks and saves. With changing population and no constraints, the same rates give approximately 16,487 people after a century from an initial 10,000.

Wrigley documents the close relationship between high preindustrial mortality and fertility, and a long-run crude birth-rate upper range of about 50 per 1,000. This supports an annual vital-rate model, not the previous food-conversion rule:

- https://www.cambridge.org/core/books/abs/poverty-progress-and-population/no-death-without-birth-the-implications-of-english-mortality-in-the-early-modern-period/BCA15BD3884D8F19788AAE0CAD79F7F5

An empty settlement cannot reproduce. The world begins with 21 founding members waiting at the gates (owner decision, Sept 28, 2026). Each founder is let in the moment 12 ration equivalents are gathered (the Food bar fills 0-12 toward the next one, as it did before the realism layer). Later survivors cost the same 12 but come in at most one every 1.5 gameplay seconds; that timer only restarts when someone comes in, so after a wait the click that fills a ration lets the next survivor in at once. This is presentation pacing, not fertility. Existing world settler transfers and narrative arrivals remain explicit migration sources. The founding pool never refills on an Age change or an old-save load. Population is not fabricated to meet an Age target.

Health reduces the birth rate; food deprivation suspends births in this aggregate model. Births can occur when homes are full and the unhoused newborns still count. Baseline mortality affects housed and unhoused residents proportionally. Every real death, including an unhoused death, increments trueDeaths. Deaths never return food to storage.

Famine is proportional to unmet food demand and exposure time. The scenario adds up to two deaths per 10,000 person-days at complete deprivation after seven days of exposure, with fractional deaths carried. FAO uses two deaths per 10,000 per day as an extreme emergency indicator; applying it as an additional famine hazard, and the seven-day grace period, are gameplay modelling choices, not a clinical survival prediction. Baseline mortality still applies. Individual nutritional reserves are not simulated.

## The frontier: from founding band to village (gameplay pacing)

The owner asked that the start keep the old loop, where clicking pays off at once, while people still come from somewhere. Below `realismFrom` (150 people, about a village) three frontier rules apply. They fade out on a log scale from `frontierFullBelow` (21) and are gone by 150. From there only the historical rates above remain. None of these numbers is a historical estimate.

- **The gates.** Survivors wait at the gates: the 21 founders, bands found by expeditions and caravans. Each is let in as soon as there is a ration of 12 Food and a home, or vagrancy is allowed. Food therefore still turns into people at once, but only people who already exist.
- **Frontier births.** The birth rate is multiplied by `frontierBirthPace` (100) for a founding band, fading to 1 by 150 people. Deaths always use the historical rate. By births alone, 21 people become 150 in a few Cycles; caravans and expeditions do most of the frontier growth.
- **Caravans.** Stores draw caravans of 3 to 8 survivors: up to 36 per Cycle (about one every 7 Sevenths, 21 minutes) at full pull. Pull is frontier pacing times the days of rations stored for everyone over 30 days. An empty granary draws no one, and caravans stop at a village. Their count and fractional progress are saved.
- **Expeditions find survivors.** Each explored cell draws once (seeded, stream "survivors"): 10% when explored in passing, 35% when surveyed. The chance is doubled in survivor-architecture and crumbling-infrastructure and cut to 0.35x in the golden ash and magical rifts. One draw serves both ways of exploring, so a cell never yields two bands. A band of 2 to 7 walks one cell per Seventh to the Capital, is saved while on the road, and then waits at the gates. This source is not tied to frontier pacing: it is explicit, finite migration, as are returning settlers (now housed where there is room, the rest as vagrants).

## The founders are provisioned for good (owner direction, Sept 28, 2026)

- **Their 12 is permanent.** The 12 Food paid for each of the 21 founders at the gates is their whole food bill: daily food demand counts only people beyond `provisionedPeople` (21, `Growth.asset`). Below 21 another person costs nothing to keep; from the 22nd on, each eats the daily ration. Famine deaths are drawn from those who eat. `GrowthRules.Eating`, `PopGrowthLogic.EatingPeople`.
- **Surplus goes into the stores.** Once the founders are all in and Resource Storage is known, Food gathered beyond `keepInHand` (12) becomes stored food, as `bankedKind` (Dried Auric Peaches, the survivors' cellar staple; proposal) at its food value, as far as the room allows (`Pantry.Bank`, `PantryRules.Banked`, Resources/Food/Pantry.asset). The Food bar then fills toward that 12. World costs in Food (towns, promotions, envoys, roads) are paid from Food in hand first, then from the stores' food value.
- **The first lesson.** While the founders wait, a card in the middle of the screen says to gather Food and rings the Food button with a breathing golden border (`UI/Tutorials`: `Tutorials` host, `TutorialLesson`, `TutorialGlow`, `FoundingLesson`). New mini tutorials derive from `TutorialLesson` and are listed in `Tutorials.Lessons`.
- **Quiet hints (first playthrough).** One line at the top of the screen, a softer glow on the control or a gold ring on a spot of the map, one at a time with a 7 s pause, each shown once ever (PlayerPrefs `g2g.tutorials.seen`, written only in a game begun from the save menu) and skipped for good when the player already did the thing; up at most 9-14 s. In order: world opens (M or scroll out), look about (drag, zoom, Esc), lead a party (ring on its token when none is selected), send it (right click), rations low (Make camp), survey (Survey here), harvest or hunt, claim, form another expedition (ring on the capital, then Form), ruins (ring), lenses after 90 s on the map (Map), parties with two expeditions (Parties), realm once land was claimed or adopted (Realm), Era Score (the sun). `UI/Tutorials/WorldHints.cs`; wording is a proposal. `Tutorials.HintsOn` can turn them off (no menu toggle yet).

## The first birth

Births since the founding are counted in `PopGrowthLogic.Births` (saved, optional for old saves). Each Seventh that brings a whole birth reports `AchievementSignal.PeopleBorn`, which awards "There is Beauty in That" (vault: "Witness the first birth of your [[Civilization]]"). Founding survivors, settler transfers and story arrivals are migration and never count. Reporting every birth rather than only the first is deliberate: the award is idempotent and a failed profile write completes on the next birth.

Both paths of the first-birth story (StarterVolume `first_birth`, "The First Cry", condition `births: >= 1`) give +1 Era Score through the new `era_score:<reason> +N` consequence and raise morale (+12 for sharing a meal, which costs 6 Food; +8 for keeping the day as any other). PopGrowthLogic asks the event system to check at once when the first child is born. The prose is placeholder, not canon. With frontier pacing, 21 founders have their first child in about 5 Sevenths (15 minutes at 180 seconds per Seventh). At the historical rate alone it would take about 1.4 Cycles, around 17 hours.

## Health, housing and stories

Nutrition, infection, sanitation and exposure can affect a small community. Their shipped population checkpoints are zero. Technology treatments ease the relevant burden rather than making everyone below a new population ceiling immune. Physical pressure maxima are annual scenario hazards expressed in the existing serialized per-Seventh field: Nutrition 1%, Disease Burden 3%, Sanitation 0.5%, Exposure 1.5% divided by 252. These are additive stress assumptions, not independently estimated historical cause-specific death rates. The health asset's survivor floor is zero. Harmonic Stability remains an explicitly fictional mechanic, also paced annually for mortality.

A city need not automatically have deaths exceed births because an Age number changed. Historical London provides evidence of urban natural decrease sustained by migration, but that does not justify imposing it on every city and period:

- https://www.cambridge.org/core/books/abs/population-and-metropolis/population-and-metropolis/2A0B632CBA16569264FE74EDBA11BD5E

Homes remain real places supplied by buildings, districts and existing modifiers. A hut housing three people is plausible as a small dwelling scenario; thousands of households require thousands of dwellings. Non-unique housing and Food-producing facilities no longer gain exponential material prices merely because more are owned. Their material costs remain paid. They are numeric production counts, not one rendered building object per household. Other resources, research, construction bonuses, magic and technology costs remain game abstractions, not measured historical quantities.

`population_percent` removes a percentage of all residents, including the unhoused, rounded down to whole people. `housing_percent` changes a percentage of housing. Types are appended to preserve serialized enum values. StarterVolume's large generic casualties and damage use these proportional effects. The old numbers were divided by a reference settlement of 500 for narrative severity: this is an explicit design conversion, not historical casualty evidence. Specific named-person consequences retain absolute counts. The source Ink and compiled story are updated together.

## Historical sizes are checks, not Age ceilings

Archaeological estimates are uncertain and geographically specific. Kuijt and Marciniak's 2024 reassessment estimates 600-800 residents during an average year of Catalhoyuk East's Middle phase; it does not establish a universal Neolithic maximum:

- https://doi.org/10.1016/j.jaa.2024.101573

A study of 173 European settlements around 1300 includes populations above 1,000, with most cases between 2,000 and 10,000. Larger places were generally denser. That supports modelling different settlement scales and infrastructure, not forcing all late-medieval settlements toward a common cap:

- https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0162678

Automated numerical checks cover 1, 50, 700, 5,000, 10,000, 50,000, 300,000 and 1,000,000 residents. These include a revised Neolithic comparison, town scales and large stress cases. The larger stress cases are not assigned to Ages or asserted to be exact historical city estimates. Every case conserves annual ration energy and remains finite. One million is a stress test, not a population limit. The former 10,000 target and canon's 50,000 plague loss are not calibration inputs.

## Visible life and bounded simulation cost

Up to 40 people are each represented by a near villager. Beyond that, an additional distant sample grows logarithmically toward 160 figures at 300,000 people. The full counts remain in `PopGrowthLogic`; background residents do not require GameObjects. The distant sample never exceeds the number of people it represents. The default hard allocation budget is 200 figures, including pooled objects and departing figures. Inspector limits prevent accidental multi-million allocations.

One central loop animates variable walking speeds, pauses, depth scale, haze, stepping and fades. Unhoused residents are represented proportionally. Paths are checked inside the valley polygon rather than letting figures walk straight across concave gaps. The pool reuses objects after deaths and reloads. Hidden settlement views, save screens, event screens and paused gameplay do not keep creating or moving villagers. Screen figures are a visual sample, not a demographic census of adults versus children.

## Saves and validation

The birth, baseline death and famine carries; hunger exposure; arrival timer; remaining founding survivors; and initialization marker are explicit optional fields in the snapshot schema. Existing saved population and housing are preserved. Old saves with no demographic state initialize with no new founding migrants. Crowd visuals are rebuilt from restored population; no rendered objects are serialized.

`PopulationRealismTests` checks energy conservation, linear scaling, annual units, century integration, source-bounded migration, relative effects, content and crowd limits. `PopulationRealismPlayTests` uses the actual settlement scene to check food deliveries, save-state round trips, old-save migration, unhoused consumption, death conservation, housing prices and 25/5,000/300,000-person crowds. Optional screenshots use G2G_POPULATION_SHOTS. Existing economy, health, save and event regressions are also run. These checks establish implementation correctness for the stated model; they do not turn scenario assumptions into historical measurements.
