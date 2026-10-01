# Technology tree rework: Age of Desolation, Act I (40 technologies)

Drafted 2026-09-29 as the plan for the tree rework. **Built the same day** (section 9): names approved by the owner through "implement this"; numbers are still proposals. **Every name, number and card is a proposal until the owner approves it.** Canon was read from the vault that day: Age of Desolation, Ages (Age 0 tier), The Inescapable Hunger, The Principles of Magic (chord tiers, spell crafting examples), The Registers of Magic, Chord Layering, Glyphic Heptastave, Symphony Card, Click Power, Combat System, Civic, Achievement, Game Logic.

## 1. Frame

- **Age 0 has 120 technologies**: 40 per Act of Fate (the three "pivots"). This document covers **Act I only**: 40 technologies, starting at Reconstruction and ending at **Echoes of Hunger**. Echoes of Hunger is Act I's **pivot crisis**, not the Age Crisis (the owner, Sept 29): it opens Act II with a lean season, a minor crisis of an Act of Fate (vault Ages.md: "Famine cults choke the wild plains"), and only foreshadows The Inescapable Hunger, which begins in Act III. It still grants 3 Crisis Advantages against it.
- **Grid**: three lanes (the prefab's three rows) × 13 tiers, plus Reconstruction at tier 0 and Echoes of Hunger alone at tier 14. Tier 13 has two cells. That makes 1 + 36 + 2 + 1 = **40**.
- **20 of the current technologies are kept or renamed, and 20 are new.** Every feature the old tree unlocked still has a home (see section 6).

### Magic limits for Age 0 (vault)

These limits set everything the Song lane and the Grimoire are allowed to do in Act I:

| Vault says | So in Act I |
|---|---|
| Age 0: "Flickering Staccato Rhythm. Unreliable Unison and Dyad Spells. Mostly Minor Note magic." (Ages.md) | Every card is **Staccato** and a **Unison**, and every cast can **flicker** (fail). |
| "only instinctive calling to elements to perform Minor Note basic sorcery, without the knowledge of The Principles of Magic" (Age of Desolation) | Most cards are **Minor Unisons** that anyone can cast. |
| "hard for magic to manifest except for some individuals with very strong Motif Awakenings" | **Major Unison** is only for Awakened Legends, and it stays unreliable. |
| Age I: "Introducing Legato. First Stable Unison. Introducing Major Notes" | No Legato, no stable Unison and no taught Major Note in Age 0. |
| Glyphs belong to the Age of Glyphs (II); the Registers were founded in Ages II | No Glyph technologies, and no Register names in technology titles. Cards cite a register only as their canon anchor. |
| AgeCapabilities D13: Ornamental Magic (Triads, most Dyads) is locked until Age III; legend Ornaments are allowed in Age 0 | Card slots are Unison-only. Legend Ornaments are not touched by this tree. |

## 2. Structure: three lanes, seven Phrases

**Lanes (rows).** The vault pairs Click Power's two buttons with the Dual Confluence Stream: Vital Resources with Lunehymn and Selenea, Building Materials with Aetherlight and the Auric Aria. Resonance, as the Key of Attunement, couples the two. The lanes follow that pairing:

| Row | Lane | Current | Holds |
|---|---|---|---|
| 1 | **Hearth** | Lunehymn, vitality | food, stores, people, births, health, the table, the moon |
| 2 | **Stone** | Aetherlight, structure | materials, buildings, storage, the map, roads, walls, the dead |
| 3 | **Song** | Resonance, coupling | the music theory lane: calendar, council, knowledge, stars, and every Grimoire slot |

**Phrases (column groups).** Act I is split into seven Phrases, named after the seven functional degrees of the Heptastave (Glyphic Heptastave, "The Sevenfold Functional Heptastave"). Each Phrase takes on the interval tendency the vault gives it:

| Phrase | Degree | Tendency (vault) | Tiers | What opens up for the player | Population (target) | Minutes from start (target) |
|---|---|---|---|---|---|---|
| I | Tonic | home | 0–1 | the clicker and the founding | 0–21 | 0–6 |
| II | Supertonic | friction, approach | 2–3 | buildings, stores, the first card, the calendar | 21–28 | 6–20 |
| III | Mediant | definition, coloration | 4–5 | **the world map**, the first Ceremony (**the rhythm clicker**) | 28–36 | 20–35 |
| IV | Subdominant | suspension, preparation | 6–7 | the Rekindling, a second seat, soldiers, legends' Major Unison, the first wildcard | 36–44 | 35–52 |
| V | Dominant | stability, projection | 8–9 | the third seat, which opens **Edicts**; preservation, walls, roads | 44–53 | 52–70 |
| VI | Submediant | extension, memory, echo | 10–11 | Emberwhisper, Glimmerfern, the stars, mourning, the Confluence | 53–62 | 70–88 |
| VII | Leading Tone | unresolved pressure | 12–14 | the peach temptations, Unsustainable Growth, and the resolution into **Echoes of Hunger** | 62–75 | 88–105 |

**Pacing model (Sept 29).** Research is **0.5 per citizen per second** (the owner; `researchPerPopulation` in ClickerScreen). Population follows the realism layer (Docs/Planning/POPULATION_GROWTH.md): 21 founders in the first minutes, then about 30 people an hour from frontier births, caravans and survivor bands, so Act I runs from about 21 to 75 people, not to 1,000. Each technology costs about the Research that population makes in its slice of the Phrase; the Act totals 135,405 Research on the way to Echoes of Hunger, about 105 minutes (35 Sevenths at 180 s), less with Enlightenment refunds. The population and minute columns above are the targets that sizing assumes. Act II then begins on Echoes of Hunger, a Dominant-to-Tonic motion. **Da Capo** is the last Song technology: the music goes back to the beginning, the way the Age does.

## 3. The tree

`E` = Event technology: +2 Era Score when researched, +1 when enlightened. `C` = Crisis technology. `†` = optional: not an ancestor of Echoes of Hunger. Costs are Research at 0.5 per citizen per second.

| Tier | Hearth (Lunehymn) | Stone (Aetherlight) | Song (Resonance) |
|---|---|---|---|
| 0 | | **Reconstruction** | |
| **I Tonic** 1 | Shared Embers | Elderwood Felling | The Ruin-Song |
| **II Supertonic** 2 | Harvest Hymns ◆ | Colonnade Salvage | **Staccato** ◆ |
| 3 | Root-Digging | Ash-Cellars | Horology `E` |
| **III Mediant** 4 | Earth-Bean Rows | Lookout Towers | **The Minor Note** ◆ |
| 5 | Trapper's Patience ◆ | Peat and Kiln | **Ostinato** `E` ◆ |
| **IV Subdominant** 6 | Midwives' Lullaby | The Rekindling `E` | **Unison** ◆ |
| 7 | Wild Honey Keeping | Heartwood Joinery | Knowledge Sanctums |
| **V Dominant** 8 | Salt and Smoke | Stonebound Walls ◆ | Call and Response |
| 9 | Reading the Vital Winds | Old World Roads | **The Seventh Degree** ◆ |
| **VI Submediant** 10 | Songs of the Moon | Chants of Ash | The Stave of Stars `E` |
| 11 | Moonlit Vigil | Sky Glass Burials | **The Dual Confluence** ◆ |
| **VII Leading Tone** 12 | Golden Orchard Belts † | Golden Ash † | **Fermata** ◆ |
| 13 | Unsustainable Growth `C` | | **Da Capo** ◆ |
| 14 | | **Echoes of Hunger** `E` (Act gate) | |

◆ = unlocks something for the Grimoire (section 4). Bold Song names are music-theory technologies.

### 3.1 Every technology

Generated from the assets (scratchpad `doc_table.js`). Unlocks name TechUnlockable assets; those whose effect reads "(Placeholder: not built yet.)" name a system that does not exist yet (section 9).

| # | Tier | Technology (was) | Type | Prerequisites | Research | Unlocks | Enlightenment |
|---|---|---|---|---|---|---|---|
| 0 | 0 | **Reconstruction** (kept) | Normal | none | 5 + 5 Food | Elderwood, Decaying Hut, Building Material Button | none |
| 1 | 1 | **Elderwood Felling** (Woodcraft Mastery) | Normal | Reconstruction | 200 | Timber Camp, Elderwood M1, Elderwood C1, Builders | Build 3 Decaying Hut |
| 2 | 1 | **Shared Embers** (Efficient Rations) | Normal | Reconstruction | 150 | Field Kitchen, Vagrants, Aetherlight, Food C1 | Welcome 12 founders through the gates. |
| 3 | 1 | **The Ruin-Song** *new* | Normal | Reconstruction | 200 | Duskstone, Research M1 | Gather 60 Elderwood by hand |
| 4 | 2 | **Colonnade Salvage** *new* | Normal | Elderwood Felling, The Ruin-Song | 1,400 | Ruin Hunter Camp | Store 150 Elderwood |
| 5 | 2 | **Harvest Hymns** (Rites of Harvest) | Normal | Shared Embers, Elderwood Felling | 1,300 | Food C3, Coaxed Spring Card | Collect 250 Food from Clicks |
| 6 | 2 | **Staccato** *new* | Normal | The Ruin-Song, Shared Embers | 1,400 | Symphony Seat, Igniting Cooking Pot Card | Build 2 Field Kitchen |
| 7 | 3 | **Ash-Cellars** (Resource Storage) | Normal | Colonnade Salvage | 1,900 | Resource Shed | Reach maximum capacity of any resource. |
| 8 | 3 | **Root-Digging** *new* | Normal | Harvest Hymns | 1,900 | Bitter Roots Foraging | Bring every founder home (21 people). |
| 9 | 3 | **Horology** (kept) | Event | Harvest Hymns, Staccato | 2,100 | Horology | Hear the first cry: the first child born to your people. |
| 10 | 4 | **Earth-Bean Rows** (Agricultural Renewal) | Normal | Root-Digging | 2,100 | Ancient Windmill, Food D1, Earth-Beans Planting | Build 3 Timber Camp |
| 11 | 4 | **Lookout Towers** (Pathfinder Training) | Normal | Ash-Cellars | 2,250 | Hollow Watchpost, Expeditions, Settlers | Build 10 Decaying Hut |
| 12 | 4 | **The Minor Note** *new* | Normal | Staccato, Horology | 2,250 | Symphony Seat, Desperate Humming Card | Meet a Legend. |
| 13 | 5 | **Ostinato** *new* | Event | The Minor Note, Harvest Hymns | 2,700 | Ceremony, The Rhythm Ritual, Ash-Clearing Zephyr Card | Collect 1000 Food from Clicks |
| 14 | 5 | **Peat and Kiln** *new* | Normal | Lookout Towers | 2,500 | Peat | Explore 20 hexes. |
| 15 | 5 | **Trapper's Patience** *new* | Normal | Earth-Bean Rows, Lookout Towers | 2,500 | Trapping Lines, Swallowed Light Card | Identify a creature. (riddle) |
| 16 | 6 | **Midwives' Lullaby** *new* | Normal | Earth-Bean Rows, The Minor Note | 3,000 | Midwives | Welcome 5 children born to your people. |
| 17 | 6 | **The Rekindling** (kept) | Event | Lookout Towers, Horology | 3,250 | Maiden's Hold, Grave Warden, Council Seat | Identify a resource site. (riddle) |
| 18 | 6 | **Unison** *new* | Normal | Ostinato, The Minor Note | 3,250 | Awakened Major Unison, Wildcard, Conjured Shard Card | Meet 2 Legends. |
| 19 | 7 | **Heartwood Joinery** (Advanced Woodworking) | Normal | Peat and Kiln, The Rekindling | 3,500 | Elderwood M2, Elderwood C2 | Store 800 Elderwood |
| 20 | 7 | **Knowledge Sanctums** (kept) | Normal | The Rekindling, Ostinato | 3,600 | Innovation Sanctums, Bestiary | Identify 2 creatures. (riddle) |
| 21 | 7 | **Wild Honey Keeping** *new* | Normal | Trapper's Patience | 3,500 | Wild Honey Stores | Identify the Wild Bee, whose honey never spoils. (riddle) |
| 22 | 8 | **Call and Response** (Edicts of Stone and Bone) | Normal | Knowledge Sanctums, The Rekindling | 4,000 | Council Seat | Live through 5 stories. |
| 23 | 8 | **Salt and Smoke** (Resource Preservation) | Normal | Wild Honey Keeping, Heartwood Joinery | 4,000 | Resource Shed Capacity | Hold 5000 resources in all. |
| 24 | 8 | **Stonebound Walls** (Fortified Living) | Normal | Heartwood Joinery | 4,000 | Stonebound Shack, A Hair's Breadth Truer Card | Stand 40 buildings. |
| 25 | 9 | **Old World Roads** *new* | Normal | Stonebound Walls | 4,650 | Old Roads Reclaimed | Complete 4 journeys. |
| 26 | 9 | **Reading the Vital Winds** (Vital Winds Mastery) | Normal | Knowledge Sanctums, Wild Honey Keeping | 4,650 | Vital Winds, Creature Studies | Work out everything about one creature. (riddle) |
| 27 | 9 | **The Seventh Degree** *new* | Normal | Call and Response, Unison | 4,650 | Symphony Seat | Research through a Ritual Seventh. |
| 28 | 10 | **Chants of Ash** (kept) | Normal | Old World Roads | 4,800 | Crystal Mine | Acquire Emberwhisper (riddle) |
| 29 | 10 | **Songs of the Moon** (kept) | Normal | Reading the Vital Winds | 4,800 | Moonlit Grove, Lunehymn | Acquire Glimmerfern (riddle) |
| 30 | 10 | **The Stave of Stars** (Celestial Astrology) | Event | The Seventh Degree, Call and Response | 5,200 | Keynote Relics | Four stars make the first figure: meet 3 Legends. |
| 31 | 11 | **Moonlit Vigil** *new* | Normal | Songs of the Moon, Midwives' Lullaby | 5,400 | Moonlit Vigil Civic | Build 2 Moonlit Grove |
| 32 | 11 | **Sky Glass Burials** *new* | Normal | Chants of Ash, The Stave of Stars | 5,400 | Sky Glass, Sky Glass Burials Civic | Dedicate a memorial to the dead. |
| 33 | 11 | **The Dual Confluence** *new* | Normal | Songs of the Moon, Chants of Ash | 5,400 | Ceremony | Store 30 Lunehymn; Store 30 Aetherlight |
| 34 | 12 | **Fermata** *new* | Normal | The Dual Confluence, The Stave of Stars | 7,000 | Flow State, Wildcard | Let the Weaver leaning reach 20. |
| 35 | 12 | **Golden Ash** † *new* | Normal | Chants of Ash | 3,500 | Food C2, Monocrop Drift +1 | Store 300 Duskstone |
| 36 | 12 | **Golden Orchard Belts** † *new* | Normal | Salt and Smoke | 3,500 | Food M2, Monocrop Drift +2 | Store 200 Dried Auric Peaches |
| 37 | 13 | **Da Capo** *new* | Normal | Fermata | 7,500 | Symphony Seat, Wildcard | Earn 10 Era Score in this Age. |
| 38 | 13 | **Unsustainable Growth** (kept) | Crisis | Moonlit Vigil, Sky Glass Burials, Salt and Smoke | 8,000 | Resource Capacity, Food M1, Food C2 | More mouths than the old fields remember: reach 60 people. |
| 39 | 14 | **Echoes of Hunger** (kept) | Event | Unsustainable Growth, Da Capo | 11,000 | The First Lean Season | Reach 75 people: more than the land has fed since the fall. |

Total on the way to Echoes of Hunger: 135,405 Research.

**Flavor lines for the new technologies** (proposals; kept technologies keep their current lines):
Shared Embers "One fire, many hands." · Elderwood Felling keeps "Claim the ancient wisdom of the forest." · The Ruin-Song "The broken spires still hum. Learn which way they fall." · Colonnade Salvage "Walls from what once held up the sky." · Staccato "In the worst moment, something clicks." · Root-Digging "Starvation food, until it is the only food." · Ash-Cellars "Dig beneath the ash; the cold keeps what the sun would spoil." · Earth-Bean Rows keeps "Revive lost ritual practices to reclaim the barren land." · The Minor Note "A small feeling, spent lightly." · Trapper's Patience "The wild does not come to the hungry. It waits." · Peat and Kiln "The black earth of the fens burns slow and long." · Ostinato "The same beats, again and again, until the field answers." · Midwives' Lullaby "Every child is born into a song." · Unison "One root, sounded with the whole heart." · Wild Honey Keeping "A sweetness no Seventh can spoil." · Salt and Smoke "What is salted remembers its season." · Call and Response "A council is a chord that must agree on its root." · Old World Roads "Someone paved this before the world ended. It still goes somewhere." · The Seventh Degree "The note that leans, and will not rest until it resolves." · Moonlit Vigil "Walk the fern-light in silence, and listen for an answer." · Sky Glass Burials "Give the dead to the sky." · The Dual Confluence "Two currents, one breath." · Golden Orchard Belts "The last mercy of a distant god. Plant more." · Golden Ash "Burn the old wood and the fruit shines gold again. For a while." · Fermata "Hold. The note is not finished." · Da Capo "From the beginning. This time, listen."

### 3.2 Crisis wiring (`Age of Desolation.asset`)

- `actTechnologies[0]` = Echoes of Hunger (the Act I pivot) and `advantageTechnologies` = Echoes of Hunger (3 Crisis Advantages).
- `actOfFateStories[0]` = `desolation_echoes_of_hunger, desolation_golden_orchard`: the pivot crisis is told first, then the Golden Orchard. An entry may now queue several stories in order (AgeProgression). `actOfFateStories[1]` = Peach Sickness opens Act III.
- **The Inescapable Hunger begins in Act III.** Its four stages start at 0.70, 0.78, 0.86 and 0.94 of the Age (Act III starts at 0.667), and no stage waits on a technology any more (A Brown Auric Peach? used to wait on Echoes of Hunger at 0.334).
- `preparationTechnologies` becomes **Root-Digging, Earth-Bean Rows, Harvest Hymns, Wild Honey Keeping, Salt and Smoke**. These are the vault's famine lesson: bitter roots, earth-beans and grains, plus varied stores.
- **The two temptations.** Golden Orchard Belts and Golden Ash are cheap, strong and optional. They are the Act I version of the vault's "Golden Orchard of False Security" and "Golden ash" phases, and each adds to `aggravationScore` (monocrop). The tree itself teaches the famine: the fastest food raises the crisis severity.

## 4. The Grimoire (room for Symphony Cards)

Symphony Cards are the vault's name for the cards. "Grimoire" is this document's name for the player's book of slots, and it is a proposal. The Symphony Card note in the vault has only its first line; the Combat System and Click Power notes carry the rules.

### 4.1 Slot kinds

| Slot | Vault basis | Used | Act I count |
|---|---|---|---|
| **Symphony slot** (the deck) | Combat System: Symphony Cards drive the micro layer; roguelike deck builder | in battle (Symphony of War) **and in the field** (an expedition or legend active) | 4 (Staccato, The Minor Note, The Seventh Degree, Da Capo) |
| **Ceremony** (Ceremonial Arts) | Registers: Ceremonial Arts (Resonance + Flux + Strand), a niche of Phase Locking Arts ("synchronizing several Soul-Keys"). Click Power: the first Ceremonial Arts turn clicking into the rhythm ritual with Auric (Aetherlight) and Silvery (Lunehymn) symbols and a Perfect Focus / Flow bar | off-combat actives: the rhythm ritual plays the Ceremony | 2 Ceremonies × 3 voices = 6 voice seats (Ostinato, The Dual Confluence) |

**Ceremonial Arts are three Spellweavers doing different things, united** (the owner's rule, Sept 29). A Ceremony is a Triad, but no single caster holds more than one note: each of the three performers plays a Unison, and the chord exists between them. That keeps it inside Age 0's limit (unreliable Unisons, no Ornamental Magic for one caster) while the Registers' chord stays whole:

| Voice | Binding (role in the chord) | Principle | What that performer does |
|---|---|---|---|
| Root | **Resonance** | Key of Attunement | phase-locks the three Soul-Keys and holds the shared frequency |
| Third | **Flux** | Emotional Authenticity | carries the feeling: the purpose of the rite, its emotional current |
| Fifth | **Strand** | Echoing Bonds | repeats the fixed sequence and binds it to memory, so chance collapses into the outcome |

- Each voice seat takes a Unison card of its binding, played by a legend (or the people). A seat with no card is filled by the people's untrained hum, which counts as a weak Minor voice. A Ceremony always sounds, and every trained voice makes it stronger.
- The Ceremony's **intent** decides what it yields: Harvest (Vital Resources), Raising (Building Materials) or Vigil (morale). The chord stays Resonance + Flux + Strand.
- In the rhythm ritual, each trained voice adds to the yield. When all three voices are trained and the beat is Perfect, the Flow State bar fills (Fermata).
- A card in a voice seat is not in the deck at the same time, so the Grimoire is a real deck-building choice.
- The first full Ceremony can be voiced the moment Ostinato opens it: Coaxed Spring (Flux) comes with Harvest Hymns, Desperate Humming (Strand) with The Minor Note, and Ash-Clearing Zephyr (Resonance) with Ostinato itself.

That is **10 seats** by the end of Act I (4 Symphony + 6 voices) for **10 cards** (7 scripted + 3 wildcards). Every seat takes a **Unison only**. The slot data keeps a chord tier (Unison, Dyad, Triad, Tetrad) so later Acts and Ages can raise the limit without a schema change.

### 4.2 Cards: seven scripted, three wildcards

Every card follows the vault's spell-crafting form: Root / Harmony / Tempo. In Act I, Harmony is always "none" and Tempo is always Staccato. **One scripted card per binding**, each tied to an Age 0 instinct the vault names:

| Card | Root (Principle) | Weight | Canon anchor | Unlocked by | Field or Ceremony effect (proposal) | Battle effect (proposal) |
|---|---|---|---|---|---|---|
| **Coaxed Spring** | Flux (Emotional Authenticity) | Minor | Water Magic (Flux) | Harvest Hymns | watered crops: Food +10% for a Seventh; the **Third** voice of a Ceremony | douse a burning hex |
| **Igniting Cooking Pot** | Cindergale (Perfect Focus) | Minor | The Principles of Magic's own example "Igniting Cooking Pot (Minor Unison)"; Hunger: "A spark coaxed from damp wood" | Staccato | Field Kitchens +15% for a Seventh | small burn |
| **Desperate Humming** | Strand (Echoing Bonds) | Minor | Hunger: "A wound closed by desperate humming"; Strand heals by Object Permanence | The Minor Note | an expedition ignores one attrition loss; a legend regains a little Composure; the **Fifth** voice of a Ceremony | restore Integrity to one unit |
| **Ash-Clearing Zephyr** | Resonance (Key of Attunement) | Minor | Zephyr Arts (Resonance), Wind Magic | Ostinato | faster road and ash-field travel; eases the Fallout of one hex; the **Root** voice of a Ceremony | pushes a unit back one hex |
| **Swallowed Light** | Void (Essence Sacrifice) | Minor | Shadow Magic (Void) | Trapper's Patience | a party slips past one wary band | enemy accuracy down for a beat |
| **Conjured Shard** | Crystal (Absolute Certainty) | **Major** (Awakened Legend) | Conjuration Arts (Crystal): "from simple projectiles upward" | Unison | cut stone: Duskstone yield up | a shard projectile |
| **A Hair's Breadth Truer** | Luminance (Sufficient Precision) | Minor | Hunger: "An arrow guided a hair's breadth truer" | Stonebound Walls | hunts return more | the next ranged attack cannot miss |

**Wildcards (Spell Maker).** Unison, Fermata and Da Capo each grant one blank card. The player composes it in the Spell Maker from four choices, and every choice traces to a binding and a Principle, as canon requires:

1. **Root**: any binding the civilization has *heard*, meaning the binding of an owned scripted card or of a seated legend's Soul Leitmotif.
2. **Weight**: Minor (anyone, slight fatigue) or Major (Awakened Legend only, paid in Composure as Essence Sacrifice).
3. **Tempo**: Staccato only in Age 0. The field stays in the data because Legato arrives in Age I.
4. **Intent**, checked against the binding's function (Glyphic Heptastave table). The Spell Maker refuses a pairing that isn't in this table:

| Intent | Resonance | Luminance | Flux | Cindergale | Crystal | Void | Strand |
|---|---|---|---|---|---|---|---|
| Strike | gust | precision | | force | shard | | |
| Ward | | | | | structure | absorb | |
| Mend | | | soothe | | | | Object Permanence |
| Hasten work | wind | light to work by | water | fire | | | |
| Reveal | | clarity | | | | | trace |
| Conceal | | | | | | absence | |
| Calm | attune | | emotional current | | | quiet | bond |

**Reliability (Age 0 flicker).** Every Act I cast has a flicker chance: Minor 15%, Major 35% (proposals). A flicker costs the cast, and a Major flicker costs extra Composure as a small Discordant Interference. Legend traits lower the chance, and Age I's "First Stable Unison" removes the Minor flicker.

### 4.3 Where it sits in the systems

- Content asks `AgeCapabilities` (X05). New capability keys (proposal): `unison-minor` (Age 0), `unison-major-awakened` (Age 0), `unison-major` (Age I), `dyad-unreliable` (Age 0, for later Acts), `tempo-legato` (Age I), `ceremonial-triad` (Age 0: a Triad shared by three casters, one note each; this does not open Ornamental Magic for a single caster). D10 ("per-card schedule") is where these belong.
- Battle cards cost Composure, which is the magic pool in the owner's combat rules. Mind Break stops casting.
- The rhythm ritual replaces plain clicking only after Ostinato; before that, Click Power works as it does today. The vault notes that clicking for its own sake makes Discordant Interference, which fits the anti-autoclicker intent.

## 5. How the Enlightenments are paced

Rules followed by every goal in section 3.1:

1. **Reachable**: a goal only reads systems that the technology's own ancestors, or the starting game, have already opened. Knowledge Sanctums' riddle waits on the map from Lookout Towers, which is its ancestor.
2. **One step ahead**: the goal is usually met about one tier before its cost is affordable, so an attentive player enlightens most of the tree. Enlightening refunds `enlightenedBonusPercent` (30%) of the cost.
3. **It teaches the next Phrase**: Phrase I goals point at the gather button and the gates; II at buildings and stores; III at the map and legends; IV at births, stories and species; V at scale and the council; VI at the world's magic resources and memory; VII at the civilization as a whole (Weaver culture, Era Score, population).
4. **Seven riddles**, one per binding: Trapper's Patience, The Rekindling, Knowledge Sanctums, Wild Honey Keeping, Reading the Vital Winds, Chants of Ash, Songs of the Moon. All of them use the existing riddle/clue fields, and none come before Phrase III (onboarding stays plain).
5. **Era Score**: Event technologies give +2 when researched and +1 when enlightened (the owner's rule). The Act's five Events (Horology, Ostinato, The Rekindling, The Stave of Stars, Echoes of Hunger) are worth at most 15 Era Score. Da Capo's goal (`era_score` ≥ 10) checks that the Act was played fully.

## 6. Migration checklist

**Renames.** Every file listed hard-codes an old name:

| Old | New | Files |
|---|---|---|
| Efficient Rations | Shared Embers | CultureLifeTuning.cs, EdictCatalog.cs, EdictsSection.cs, INK_FILES_GUIDE.md, StarterVolume.ink, Age of Desolation.asset, Health.asset, Canon Gaps.md, tests (EdictTests, EventSystemTests, TechTree*) |
| Woodcraft Mastery | Elderwood Felling | WorldSettings.cs (`improveTechnology`), World.asset, Health.asset, Canon Gaps.md, TechTree tests |
| Rites of Harvest | Harvest Hymns | CultureLifeTuning.cs, TraditionTuning.cs, Events README/INK guide, StarterVolume.ink (`technology:Rites of Harvest enlightened`), Age asset, Health.asset, CultureRedesign/T01_REPORT.md, LUXURIES_AND_AMENITIES.md, tests |
| Agricultural Renewal | Earth-Bean Rows | Age asset, AgeOfDesolation.ink, Health.asset, Canon Gaps.md, tests |
| Pathfinder Training | Lookout Towers | CombatDefaults.cs, WorldSettings.cs (`mapTechnology`, unit `unlockTechnology`), WorldSystem.cs, BestiaryHud.cs, WorldHints.cs, World.asset, GameWiki.md, ROADMAP.md, WORLD_GENERATION.md, WorldViewPlayTests, tests |
| Resource Storage | Ash-Cellars | PantrySettings.cs, Pantry.asset, WorldSystem.cs, GameWiki.md, POPULATION_GROWTH.md, FoundingPlayTests, tests |
| Advanced Woodworking | Heartwood Joinery | tests |
| Edicts of Stone and Bone | Call and Response | EdictModel.cs, EdictSystem.cs, GovernmentLogic.cs, EdictsSection.cs, GameWiki.md, PantryPlayTests, tests |
| Resource Preservation | Salt and Smoke | CultureLifeTuning.cs (beverages), Pantry.asset, Age asset, GameWiki.md, Canon Gaps.md, DISCOVERY_LOOP.md, LUXURIES_AND_AMENITIES.md, tests |
| Celestial Astrology | The Stave of Stars | GameValues.cs (doc comment), tests |
| Vital Winds Mastery | Reading the Vital Winds | Canon Gaps.md, tests |
| Fortified Living | Stonebound Walls | Health.asset, Canon Gaps.md, tests |

**Kept names** (8): Reconstruction, Horology, The Rekindling, Knowledge Sanctums, Songs of the Moon, Chants of Ash, Unsustainable Growth, Echoes of Hunger. They are canon-named (Reconstruction, Echoes of Hunger), vault-phrased (Unsustainable Growth), heavily wired (Horology), or already musical. Any of them can still be renamed if the owner wants.

**Moved gates.** Duskstone moves to The Ruin-Song; Ruin Hunter Camp to Colonnade Salvage; Ancient Windmill and Food D1 to Earth-Bean Rows; the wild-bee riddle to Wild Honey Keeping; Wild Honey Mead from Harvest Hymns to Wild Honey Keeping; Wasteland Archer stays with The Rekindling. Food C1 and Elderwood C1 were orphans and now have homes.

**Saves.** Researched technologies and `SaveDocument.researchPlan` store names. Add one alias table (old → new) that is applied on load, the same way achievement aliases keep historic IDs.

## 7. Build order

1. **Data** (no gameplay change): `SymphonyCardData` ScriptableObject (id, name, root binding, weight, chord tier, tempo, uses, flicker chance, effect hook, canon source). Append `TechUnlockableType` values `GrimoireSlot` (resourceModifier = count; slot kind Symphony or Ceremony voice (Root / Third / Fifth)), `SymphonyCard`, `SpellWildcard` and `ScoreChange`, all appended because the enum is serialized by index. Add the `AgeCapabilities` keys.
2. **Tree**: rename 12 technologies with the alias table, and generate 20 new TechnologyData + GameUnit assets with a generator script, as the Act I sheet was built before. Reorder `AgeTechnology.prefab` `eraTechnologies` column by column (three rows) and update the Age asset. Extend `ContentValidator` to check that Act I has exactly 40 technologies, that Echoes of Hunger is the only sink, that every Enlightenment domain is registered, and that no goal reads a system opened by a non-ancestor.
3. **Tests**: rewrite TechTreeTests' Act I graph for the 40 nodes, check that the † technologies are not ancestors of Echoes, update the play tests that name old technologies, and run a pacing simulation (population curve × costs) to validate section 2's minute targets.
4. **Grimoire runtime** (next section of work): GrimoireSystem (slots, owned cards, loadout, save), the Spell Maker (the Unison composer and intent table), the Ceremony (three voice seats, the people's hum, intent) and the rhythm ritual behind Ostinato.

## 8. Gaps and decisions for the owner

- **G1 (settled by the owner, Sept 29): Ceremonial Arts are three Spellweavers doing different things, united.** The Registers' chord (Resonance + Flux + Strand) is kept whole and shared by three casters, one Unison each. The "Rhythmic Baking" achievement fires on Ostinato, which opens the first Ceremony. Still open: whether a later Age lets one Spellweaver hold the whole Ceremonial Triad, and whether Ceremonies with other chords exist.
- **G2: Major Unison in Age 0.** Ages.md places "Introducing Major Notes" in Age I, while the Age of Desolation allows strong Motif Awakenings. This plan allows Major weight for Awakened Legends only, and unreliably.
- **G3: The Symphony Card note is a single sentence.** Slot counts, "Grimoire", "Symphony slot / Ceremony voice seat", flicker, and the Spell Maker (Gateway To Genesis lists "Spell Crafting System" with no note) are all proposals.
- **G4: Card effects** are placeholders until the micro combat layer and the rhythm ritual exist.
- **G5: Moonlit Vigil and Sky Glass Burials** become civics offered by technology. The vault lists them as Age 0–III civics but does not say how they are adopted.
- **G6: Dyad slots.** Ages.md allows "unreliable Dyad" in Age 0. Proposal: one unreliable Dyad slot late in Act III, not in Act I.
- **G7: Existing content bugs** this rework fixes: the Aurean Winds weather does not exist, `keynote_relics` is always 0, and Chants of Ash says "Emberwisp" instead of Emberwhisper.
- **G8 (settled by the owner, Sept 29): research is 0.5 per citizen per second**, set in ClickerScreen (`researchPerPopulation`, was 100); costs were sized to it (section 2).
- **G9: Acts II and III** (80 technologies) are not designed yet. They would cover the famine stages (golden ash overuse, Searstone and Phosflare, the Ash-Bread Winter, the Choosing of Seeds) and end in the Agromagical Conclaves' "land as chord" lesson that opens the Age of Renewal.

## 9. Built (Sept 29, 2026)

- **Tree**: 40 TechnologyData + GameUnit assets in `Resources/Technology` and `Resources/GameUnits/GameTechnologies`, generated by the session scratchpad's `gen_act1.js` (every existing GUID kept; the 12 renames went through `git mv`). `AgeTechnology.prefab` lists them column by column (44 cells, Reconstruction placed by `initializationUnitIndex` 1).
- **Renames**: every hard-coded old name in code, assets, Ink and tests now uses the new one. `TechnologyAliases` still resolves the old names in `GameUnitsLogic.GetTechnologySlot`, in the research plan and in a save's technology records. A save made before the rework loads: its renamed technologies are read under the new names, and the technologies added since have no record and simply stay unresearched (`GameSnapshot.Restore`).
- **Grimoire (data only)**: `SymphonyCardData` (7 cards in `Resources/Grimoire/Cards`), `Grimoire` (pure rules: seats, the three-voice Ceremony, seating limits through `AgeMagic`, the Spell Maker's intent table), new `TechUnlockableType` values `GrimoireSeat`, `SymphonyCard`, `SpellWildcard`, `ScoreChange` (appended), their tooltips and `ContentValidator` checks, the `grimoire` condition domain, and `AgeCapabilities` keys `unison-minor`, `unison-major-awakened`, `ceremonial-triad`. The Grimoire is derived from researched technologies, so it has no save state yet. There is no Grimoire window, Spell Maker UI, rhythm ritual or card play: those are the next section.
- **Placeholders**: Bitter Roots Foraging, Earth-Beans Planting, Trapping Lines, Midwives, Wild Honey Stores, Old Roads Reclaimed, the two civics, The Rhythm Ritual, Awakened Major Unison and Flow State are Special unlockables that only log. Everything else they sit beside works (buildings, modifiers, click power, discovered resources, council seats, the monocrop score).
- **The pivot crisis**: `desolation_echoes_of_hunger` in AgeOfDesolation.ink (placeholder prose past the vault's line), then the Golden Orchard; the Age Crisis stages moved into Act III.
- **Tests**: TechTreeTests (the new graph and grid, the Act's shape, aliases), GrimoireTests, ActITreeContentTests (assets, the Grimoire by Echoes of Hunger, the first Ceremony fully voiced by Ostinato, the Age asset), TechTreePlayTests updated. Pure tests were run outside Unity; the rest need the Test Runner.
