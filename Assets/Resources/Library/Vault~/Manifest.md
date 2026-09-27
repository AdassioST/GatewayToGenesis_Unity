# Vault import manifest

The Obsidian vault is the canon. **Tools > Gateway to Genesis > Import Lore From Vault** turns every lore note of
`Worldbuilding/` into an entry of the White-Haven Library's Glossary (`Resources/Library/Library.json`), copying the
note's words as written, and writes `ImportReport.md` next to this file: every entry, and every passage left out and
why. This manifest only says what the defaults cannot guess. Change the vault, then re-run the import: never edit
`Library.json` by hand. What a tooltip shows is not decided here: that is the keyword's card, written by hand in
`Resources/Keywords/Keywords.md`, which links to the entry (a word with no card shows the entry's summary).

### How a note becomes an entry (the defaults)

- **Title** is the note's name, **category** and **shelf** come from its folder, **id** is the title's slug
  (`auric-peach`).
- **Epigraph** is the note's: the short italic line at the top, or what the note labels "Description:".
- **Summary** is the lead (one to a few sentences) of the first lore paragraph: what a word with no card shows, and
  what the Library's search reads. The paragraph stays whole in the article.
- **Article** is every passage that is lore, in the note's order. Left out, and listed in the report: design notes
  (the game, the player, mechanics, real-world works), status checklists, formulas and pasted chat replies.
- A long note (over 6000 characters, with at least three headings at one level) keeps its whole text, and each of
  its sections is also an entry of its own (id `note--section`), listed under "In this note".
- Titles that are ordinary words (Void, Echo, Legend...) are quiet: they do not light up by themselves in every
  tooltip; `[[links]]` always reach them.
- `[[Links]]` and "See also" are resolved to entry ids by the rules the game uses.

### Writing an entry

A line `## Title` starts an entry; `field: value` lines under it set it. Anything else is a comment. An entry whose
title is a note's name changes how that note is imported; any other title makes a new entry from part of a note.

| Field | Value |
|---|---|
| `note` | The note (its name, or its path from the vault root) |
| `title` | The title shown, when it is not the entry's (the note's name stays another name for it) |
| `category` | Small-caps line under the title |
| `group` | The shelf it is filed on |
| `aliases` | Other words that mean it, comma-separated (they light up too) |
| `autolink` | `off`: quiet, only `[[links]]` reach it |
| `flavor`, `summary`, `details` | Selectors (below) for the epigraph, the summary and the article; `auto` is the default |
| `skip` | Selectors of passages the automatic article leaves out |
| `related` | "See also" entries, comma-separated |
| `split` | `sections`, `none`, `entries` (a catalogue of `**Name \| kind**` lines), or `at "Heading", "Heading"` |
| `import` | `no` leaves the note out |

Selectors, joined with `+`: `auto`, `none`, `all`, `epigraph`, `"words"` (the passage containing them),
`"from" .. "to"` (the passages between), `"from" ..` (to the end of its section), `section "Heading"`,
`line "words"`, `sentence "words"` (or `sentence "from" .. "to"`), `sentences "words"` (the lead of that paragraph).
`note "Title"` before a selector takes it from another note. `text: words` is literal text, for game rules only
(never for lore). Ranges and sections leave out what is not lore; a passage named on its own is always kept.

# Settings

Design documents, not lore.

exclude: Game Systems/, Gateway To Genesis.md, Events/Achievement.md

# Age 0: the Age of Desolation

The game starts here. The section "Age of Desolation" groups its technologies; its tooltip finds this entry by name.

## Age of Desolation
aliases: Age 0
summary: "The once-great civilization"
related: The Inescapable Hunger, Cataclysmic Aftermath, Old World Remnants, Old World Relics, Minor Note

## Old World Remnants
note: Age of Desolation
category: Section
group: Age of Desolation
summary: sentence "The gathering of magic elements"
details: none
related: Old World Relics, Age of Desolation

The note keeps three tellings of the crisis (and pasted chat replies): the second, fullest telling is imported.

## The Inescapable Hunger
aliases: The Auric Peach Famine, Auric Peach Famine, The Ash-Bread Winter, The Ash‑Bread Winter, The First Reckoning
flavor: line "The world remembers excess"
summary: sentences "It is quieter than the radiant terror"
details: epigraph + "It is simply" .. "And that quiet vow"
split: at "The World of Ash", "The Auric Peach", "Structural Vulnerabilities", "Phase I", "Revelation and Response", "Themes and Legacy"
related: Age of Desolation, Auric Peach, Peach Sickness, Golden Ash, Old World Relics, Agromagical Conclaves

## Auric Peach
note: The Inescapable Hunger
category: Crop
group: Age of Desolation
aliases: Auric peach, Auric peaches, Auric Peaches
flavor: sentence "dangerously close to perfect"
summary: "The primary food source of this era"
details: section "Inherent Qualities" + section "Three Faces of the Peach" + "What none of them see yet"
related: Food, The Inescapable Hunger, Peach Sickness, Golden Ash

## Peach Sickness
note: The Inescapable Hunger
category: Affliction
group: Age of Desolation
flavor: "He ate until his belly ached"
summary: sentence "Peach Sickness is the accumulated absence"
details: section "Symptomatology" + section "The Hidden Cause" + section "Social Interpretation"
related: Auric Peach, The Inescapable Hunger

## Golden Ash
note: The Inescapable Hunger
category: Soil Amendment
group: Age of Desolation
aliases: golden ash
summary: line "A fine, shimmering dust"
details: section "The Golden Ash" + section "Arcanoria’s Potash"
related: Searstone, Phosflare, Auric Peach, The Inescapable Hunger

## Searstone
note: The Inescapable Hunger
category: Ore
group: Age of Desolation
summary: line "Sulfur-rich ore"
details: section "Misunderstood Miracle Ores"
related: Phosflare, Golden Ash

## Phosflare
note: The Inescapable Hunger
category: Ore
group: Age of Desolation
summary: line "Phosphorus-heavy rock"
details: section "Misunderstood Miracle Ores"
related: Searstone, Golden Ash

## Ruin-Song
note: The Inescapable Hunger
category: Phenomenon
group: Age of Desolation
aliases: ruin‑song, ruin-song
summary: line "is simply called"
details: section "Dirge"
related: Age of Desolation

## Old World Relics
note: The Inescapable Hunger
category: Relic
group: Age of Desolation
aliases: Old World Relic
summary: line "are everywhere in ruins"
details: section "4. Relics as Fertilizer" + section "On Burning History"
related: Old World Remnants, Golden Ash, The Inescapable Hunger

## Agromagical Conclaves
note: The Inescapable Hunger
category: Conclave
group: Age of Desolation
aliases: Agromagical Conclave, Agromage Conclaves
flavor: "No stone alone can save a starving field"
summary: sentence "From these roots, the earliest"
details: section "Listeners" + section "Birth of Agromancy"
related: The Inescapable Hunger, Auric Peach

## Vaelia
skip: section "Romance"

Most of this note is about resets, the Master-Key and the endings (design): its world is the ruin it leaves and the
first two filters.

## Cataclysmic Aftermath
summary: note "The Inescapable Hunger" sentence "After the [[Cataclysmic Aftermath]] that shattered the prior Age"
details: line "The first filter in" + line "The second filter in" + auto

# The world

Arcanoria's own note is the map design; the world itself is described in the premise of Gateway To Genesis.

## Arcanoria
summary: note "Gateway To Genesis" sentence "the once-thriving world of [[Arcanoria]] lies in ruin"
details: note "Gateway To Genesis" "At the heart of existence flows" + note "Gateway To Genesis" "Magic in [[Arcanoria]] emerges from" + auto
skip: section "Map Features"

# Magic as Age 0 knows it

The age knows only instinctive Minor Note sorcery in a staccato rhythm; these notes have no page of their own in
the vault, so their entries are the passages that define them.

## Minor Note
note: The Principles of Magic
category: Spellweaving
group: Magic
aliases: Minor Notes
summary: sentence "draw upon fleeting emotions"
details: sentence "Though [[Minor Note]] spells cost little" + note "The Inescapable Hunger" "Magic in this age is mostly"
related: Major Note, Staccato, The Principles of Magic

## Major Note
note: The Principles of Magic
category: Spellweaving
group: Magic
aliases: Major Notes
summary: sentence "channel core convictions"
details: sentence "Spell craft can only hold one"
related: Minor Note, The Principles of Magic

## Staccato
note: The Principles of Magic
category: Rhythm of Spellweaving
group: Magic
aliases: staccato
summary: line "**Staccato:**"
details: note "Age of Desolation" sentence "the only known rhythm tempo is staccato"
related: Minor Note, The Principles of Magic

## Unison
note: The Principles of Magic
category: Chord of Spellweaving
group: Magic
summary: line "Spells (Root Only)"
related: Dyad Chord, Triad Chord, Tetrad Chord, The Principles of Magic

## Dyad Chord
note: The Principles of Magic
category: Chord of Spellweaving
group: Magic
aliases: Dyad
summary: line "Spells (Root + Third):"
related: Unison, Triad Chord, The Principles of Magic

## Triad Chord
note: The Principles of Magic
category: Chord of Spellweaving
group: Magic
summary: line "Spells (Root + Third + Fifth):"
related: Dyad Chord, Tetrad Chord, The Principles of Magic

## Tetrad Chord
note: The Principles of Magic
category: Chord of Spellweaving
group: Magic
summary: line "Spells (Root + Third + Fifth + Seventh):"
related: Triad Chord, Hyper Chord, The Principles of Magic

## Hyper Chord
note: The Principles of Magic
category: Chord of Spellweaving
group: Magic
aliases: Hyper Chords
summary: sentence "These are the stress test theoretical limit"
details: sentence "It can be done by laying" .. "The first mortal to stabilize"
related: Tetrad Chord, The Birth of the Hyper Chord, The Principles of Magic

## Dual Confluence Stream
details: auto + note "Gateway To Genesis" "At the heart of existence flows"
related: Aetherlight, Lunehymn, Static Criticality

# Resources the game already has

The game's resources: their tooltips (and every resource slot) find these entries by name and link to them.

## Aetherlight
details: auto + note "Lux Aeterna" sentence "becomes the essential and final"
related: Lunehymn, Dual Confluence Stream, Mirrorbox Trap, Emberwhisper

The game called Lunehymn "Moonshine" before; Glimmerfern's note still does, so the old name points here.

## Lunehymn
aliases: Moonshine
details: auto + note "Lux Aeterna" sentence "becomes the essential and final"
related: Aetherlight, Dual Confluence Stream, Glimmerfern

## Glimmerfern
details: note "Lux Aeterna" sentence "becomes the essential and final"
related: Lunehymn, Emberwhisper

## Emberwhisper
flavor: "Gleaming, velvet red crystal"
summary: "The cosmic crime of theft"
details: note "Lux Aeterna" sentence "becomes the essential and final"
related: Aetherlight, Glimmerfern

## Food
note: The Inescapable Hunger
category: Vital Resource
group: Age of Desolation
summary: "The primary food source of this era"
details: none
related: Auric Peach, The Inescapable Hunger

# Pillars of Civilization

The game's pillars. How a pillar challenge works belongs to the Game Wiki (written from the game's data), not here.

## Pillars
summary: sentence "are the spectrum of philosophical integration"
skip: "The axis functions to determine" + "only available after realizing" + "FANATIC WALTZ" + "This interplay allows"
related: Waltz, Regalia, Aureus, Chorus

## Waltz Pillar
category: Pillar of Civilization
aliases: Waltz
related: Regalia, Pillars

## Regalia Pillar
category: Pillar of Civilization
aliases: Regalia
related: Waltz, Pillars

## Aureus Pillar
category: Pillar of Civilization
aliases: Aureus
related: Chorus, Pillars, Auric Aria

## Chorus Pillar
category: Pillar of Civilization
aliases: Chorus
related: Aureus, Pillars, Outer God

## Civic
split: entries

# The council: Great Spellweavers

A legend's class is the kind of Great Spellweaver it became. The council's seats are positions (High Arbiter, Oracle,
Treasurer...) that legends of several classes can hold (Resources/Council): a High Arbiter can be a Great Sovereign, a
Great Justiciar or a Great Architect.

## Great Spellweaver
note: Stellar Legacy Score
category: Stellar Legacy
aliases: Great Spellweavers
summary: sentence "that are of great renown become the leaders"
details: "[[Great Sovereign]] ([[Resonance]] Affinity)" + "are usually of their element affinity"
related: Legend, Stellar Legacy Score

## Great Sovereign
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Sovereigns
summary: line "[[Great Sovereign]] ("
related: Great Spellweaver, Resonance, Aurelian

## Great Seer
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Seers
summary: line "[[Great Seer]] ("
related: Great Spellweaver, Luminance

## Great Concertist
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Concertists
summary: line "[[Great Concertist]] ("
related: Great Spellweaver, Flux

## Great Justiciar
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Justiciars
summary: line "[[Great Justiciar]] ("
related: Great Spellweaver, Void

## Great Vanguard
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Vanguards
summary: line "[[Great Vanguard]] ("
related: Great Spellweaver, Cindergale, Sephira

## Great Architect
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Architects
summary: line "[[Great Architect]] ("
related: Great Spellweaver, Crystal

## Great Chronicler
note: Stellar Legacy Score
category: Great Spellweaver
group: Legends
aliases: Great Chroniclers
summary: line "[[Great Chronicler]] ("
related: Great Spellweaver, Strand

# The passage of time

The calendar the game keeps: the time bar's tooltips find these entries by name, with where the world stands now.

## Seventh
summary: note "Cycle" sentence "The [[Seventh]] is the minimum scholarly unit" .. "the sacred role of the number 7"
details: auto + note "Cycle" "However, the [[Seventh]] is not a single Day"
related: Ritual Seventh, Phase, Cycle

## Ritual Seventh
details: auto + note "Cycle" sentence "The 21st [[Seventh]] of every [[Phase]]" .. "perceived through different calendars"
related: Seventh, Attunement for Magic

## Phase
summary: sentence "consists of 12 per" .. "a stage of world tuning"
details: "[[Phase of Prelude]]" + "has only one [[Ritual Seventh]]"
related: Echo, Seventh, Cycle

## Echo
related: Phase, Cycle, Echo of Resonance, Echo of Crescendo, Echo of Dissonance, Echo of Silence

## Cycle
summary: sentences "is the main unit to track time"
related: Echo, Phase, Seventh, Lunar Cycle

The four echoes (the seasons of a Cycle) and their phases have no pages of their own: each entry is the Cycle
note's line for its echo. The time bar shows the season line itself (Resources/TimeCycle), so an echo's entry keeps
it as flavour and lists its phases.

## Echo of Resonance
note: Cycle
category: Echo
group: Time
flavor: line "Spring: Birth"
summary: line "[[Phase of Prelude]], [[Phase of Harmonics]]"
details: none
related: Echo, Cycle

## Echo of Crescendo
note: Cycle
category: Echo
group: Time
flavor: line "Summer: Growth"
summary: line "[[Phase of Flourish]], [[Phase of Zenith]]"
details: none
related: Echo, Cycle

## Echo of Dissonance
note: Cycle
category: Echo
group: Time
flavor: line "Autumn: Fracture"
summary: line "[[Phase of Discord]], [[Phase of Ashfall]]"
details: none
related: Echo, Cycle

## Echo of Silence
note: Cycle
category: Echo
group: Time
flavor: line "Winter: Death"
summary: line "[[Phase of Stillness]], [[Phase of Repose]]"
details: none
related: Echo, Cycle

## Phase of Prelude
note: Cycle
category: Phase
group: Time
summary: line "Spring: Birth"
details: none
related: Echo of Resonance, Phase

## Phase of Harmonics
note: Cycle
category: Phase
group: Time
summary: line "Spring: Birth"
details: none
related: Echo of Resonance, Phase

## Phase of Reflection
note: Cycle
category: Phase
group: Time
summary: line "Spring: Birth"
details: none
related: Echo of Resonance, Phase

## Phase of Flourish
note: Cycle
category: Phase
group: Time
summary: line "Summer: Growth"
details: none
related: Echo of Crescendo, Phase

## Phase of Zenith
note: Cycle
category: Phase
group: Time
summary: line "Summer: Growth"
details: none
related: Echo of Crescendo, Phase

## Phase of Transition
note: Cycle
category: Phase
group: Time
summary: line "Summer: Growth"
details: none
related: Echo of Crescendo, Phase

## Phase of Discord
note: Cycle
category: Phase
group: Time
summary: line "Autumn: Fracture"
details: none
related: Echo of Dissonance, Phase

## Phase of Ashfall
note: Cycle
category: Phase
group: Time
summary: line "Autumn: Fracture"
details: none
related: Echo of Dissonance, Phase

## Phase of Fracture
note: Cycle
category: Phase
group: Time
summary: line "Autumn: Fracture"
details: none
related: Echo of Dissonance, Phase

## Phase of Stillness
note: Cycle
category: Phase
group: Time
summary: line "Winter: Death"
details: none
related: Echo of Silence, Phase

## Phase of Repose
note: Cycle
category: Phase
group: Time
summary: line "Winter: Death"
details: none
related: Echo of Silence, Phase

## Phase of Twilight
note: Cycle
category: Phase
group: Time
summary: line "Winter: Death"
details: none
related: Echo of Silence, Phase
