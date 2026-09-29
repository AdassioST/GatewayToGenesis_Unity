# The discovery loop

Built 28 September 2026 (uncommitted). Every number, name, recipe, riddle and line of wording below is a gameplay
proposal; what the vault does and does not say is in Canon Gaps ("Kitchen trials", "Rumours", "Species hypotheses and
tech riddles").

## The rule

Four places in the game used to hand the player an answer to read. Each now asks a question the player answers by
acting:

1. **Guess.** The player commits to something they do not yet know: a mix, a direction on the map, an answer about a
   creature, the meaning of a riddle.
2. **Test by doing.** Only the act answers it: cooking the batch, walking there, watching and hunting, meeting the goal.
   Reading a tooltip never does.
3. **Every test teaches.** A miss still says something: how near the mix came, which guess is ruled out, where the
   rumour's area lies, when the clue appears.
4. **Never the same mystery twice.** What was learned is saved and shown again (the trial log, ruled-out guesses,
   confirmed rumours, answered riddles).
5. **Finding pays once.** A small Era Score reward for a real discovery, never for repetition.

## 1. Kitchen trials

`GameData/Discovery/Experiments.cs` (shared, no scene state), `GameData/Culture/KitchenTrials.cs`,
`GameData/Culture/CultureSystem.Experiments.cs`, `UI/Culture/RecipeInventionDialog.cs`.

- A dish or drink of the player's own cannot be kept and named until a batch of it has been tasted. **Try a batch**
  cooks (or ferments or distills) it once, using up what goes in (`CultureSystem.TryBatch`, refused by `WhyNotTry`).
  The kitchen tries `CultureLifeTuning.trialsPerSeventh` (3) batches a Seventh.
- A mix is recognised by its shares, not its size (`Experiments.Signature`), so the same mix at any batch size is
  one trial; trying it again replaces the older one. The log keeps 40 trials (`KitchenTrials.MaxRemembered`),
  forgetting the oldest unfound ones first.
- **Hidden recipes** (`CultureLifeTuning.hiddenRecipes`, defaults in `KitchenTrials.Defaults()`): a `SecretFormula`
  of parts, each with a least and most share, a way of making, and optionally *exclusive* (anything else spoils it).
  Each trial reads its **warmth** against the unfound recipes: a part at its share counts whole, at the wrong share
  half; Faint above 0, Close at 0.5, Very close at 0.75, Found only on an exact match.
- Each recipe has three hints: the first is whispered once its way of making is known, the second at Close, the third
  at Very close (all once found). The dialog lists the whispers under **Whispered recipes**; a new hint sends a notice.
- Finding one: 2 Era Score, joy, a moment in the culture's memory and a notice. A dish kept from it is finer
  (`KitchenTrials.Apply`: x food value, may never spoil, an extra luxury, Unity a batch, Faith a unit).
- The four recipes: Everkeep Honeycake, Moonwater Broth, Ashen Hearthbread (cooked), Sunmead of the First Harvest
  (fermented, exclusive). `ExperimentTests.EveryWhisperedRecipe_IsValid_AndFoundByABatchOfWholeUnits` checks that each
  can be made from whole units of real stored foods, and that the cellar never brews a bloom.
- Saved in `CultureExtensionState.kitchen` (`[SaveOptionalField]`, so older saves load).
- GameValues `kitchen` (trials), `kitchen:found`, `kitchen:found:<id>`.
- `Experiments` names nothing of the kitchen: it is meant for spells later (components as notes, elements or bindings).

## 2. Rumours

`GameData/World/WorldRumours.cs` (rules), `GameData/World/RumourKeeper.cs` (saved state, created by `GenesisLoop`),
`UI/World/RumoursWindow.cs`, `UI/World/WorldRenderer.cs` (haze), `UI/World/WorldView.cs` (dock button, hover card).

- Landmarks, wonders, ruins, Sacred Sites, Enclaves and creature dens no longer show through the fog. A landmark
  still shows from afar once the people's sight reaches it (`FeatureSpec` tooltips say so).
- The people hear of them instead: 2 at the world's start, 1 each Echo, 1 each time an expedition completes a journey,
  none while 5 are open. Nothing within 5 cells of the capital is rumoured.
- A rumour reads as a compass direction from the capital (plus the Sector once its Macro Biome has been seen), a
  distance word (a few days away within 12 cells, far within 30, past that the edge of what anyone knows) and a
  vague line: `FeatureSpec.rumour` when authored (none are yet), otherwise one made from its kind. Creature dens are
  told by size and ways, never by name, and the Bestiary lists them under **Rumoured creatures**.
- The map shows a parchment haze and ring over the area, centred on a cell within 3 cells of the thing (never on it),
  radius 5. The hover card reads out the rumours covering a cell. Finding the thing confirms the rumour: it is named,
  +1 Era Score, a notice.
- Choices are seeded by the world and `RumourState.told`, so a save replays the same rumours.
- Saved whole as `RumourKeeper._state` (GameSnapshot schema; a later system, so older saves load without it).
- GameValues `rumours` (heard), `rumours:open`, `rumours:confirmed`, `rumours:confirmed:<kind>`.

## 3. Species hypotheses

`GameData/World/SpeciesHypotheses.cs` (rules), `SpeciesLore.cs` (saved fields), `SpeciesLoreKeeper.cs` (evidence),
`UI/BestiaryWindow.cs` (**What your people think**), `UI/World/WorldView.cs` (map card).

- Once a species is identified the Bestiary asks: *When does it breed?* (a breeding Echo, or none of its own), *Where
  does it thrive?* (its harmonic niche) and, for carnivores and omnivores only, *What does it hunt?* (an identified
  species, or none). The player clicks a guess (TMP links `hyp|species|question|answer`).
- Evidence tests only the guess made. **Watching**: each Echo, an identified species with an identified den within
  `SpeciesLoreTuning.watchReach` (2) cells of held land is watched; that tests the niche, and the breeding guess only
  in the Echo guessed. "No season of its own" needs all four Echoes watched (`SpeciesRecord.echoMask`) and fails in
  the breeding Echo. **Hunting**: each hunt by the player's people tests the prey guess once (`huntsWeighed`).
- A refuted guess is kept as ruled out and can't be picked again. A guess confirmed with nothing ruled out first is
  insight: +1 Era Score (`SpeciesLoreTuning.insightEra`).
- Every question answered sets `deduced`: the species counts as Understood without the research, with its rewards.
- Niche and breeding stay hidden on the map card and in the Bestiary until worked out or understood
  (`SpeciesHypotheses.Knows`).
- Saved per species (`hypotheses`, `echoMask`, `huntsWeighed`, `deduced`, all `[SaveOptionalField]`).
- GameValues `species_deduced`, `species_deduced:<id>` (questions answered for one).

## 4. Riddles on Enlightenment goals

`GameData/GameObjects/TechnologyData.cs` (`EnlightenedCondition.riddle`, `clue`, `clueAt`, `clueTrigger`,
`clueAmount`), `GameTechnologySlot.cs`, `TooltipContent.cs`, `Core/Validation/ContentValidator.cs`.

- A goal authored with a riddle shows the riddle instead of the goal, with no progress; the card's bar says only
  "A riddle (hover to read)". The clue appears once the goal is `clueAt` (0.5) met, or once the GameValue
  `clueTrigger` reaches `clueAmount`. Once met, the goal is shown plainly, "(the riddle answered)".
- Authored on Chants of Ash, Knowledge Sanctums, Resource Preservation, Songs of the Moon and The Rekindling.

## Verification

- 28 new pure tests: `ExperimentTests` (9), `RiddleTests` (5), `RumourTests` (6), `SpeciesHypothesesTests` (8). All
  pass offline, and the whole offline pure suite passed (991; 37 need the Unity runtime).
- Full batchmode EditMode run, 28 September 23:47: 1,097 passed, 0 failed, 1 skipped (another agent's ignored
  `ZzWorldShotsTemp` screenshot helper), including the play tests (`GenesisLoopPlayTests`, `WorldViewPlayTests`).
- No play test yet covers the Rumours window, the haze, the Bestiary's guess links or the trial dialog.

## Left open

- **Play tests**: open the Rumours window; click a guess in the Bestiary; try a batch and keep it in
  `RecipeInventionDialog`; a batch screenshot of the rumour haze over the fog.
- **Authored rumour lines**: no `FeatureSpec.rumour` is written; every rumour uses the generated line.
- **Rumours in the story**: rumours could come from events, ballads and Enclaves, and some could be false or
  outdated (none are now). A rumour never expires.
- **Culture.asset tuning**: the hidden recipes and `trialsPerSeventh` are code defaults; the asset has no block for
  them. No hidden recipe is distilled yet.
- **Spells**: `Experiments` is ready for spell formulas (notes, elements, bindings) once spellcraft exists.
- **More questions**: the Bestiary asks three; range, group size or temper could follow the same pattern. Blooms are
  not asked anything.
- **More riddles**: five technologies have one; the rest of the Act I tree, and a Library entry for each once
  answered, are open.
- **Balance**: every number above is a proposal (3 trials a Seventh, 2/1/1 Era Score, 5 open rumours, reach 2).
