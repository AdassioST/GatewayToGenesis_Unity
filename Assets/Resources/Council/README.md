# Council seats

A **seat** is a position on the council (High Arbiter, Oracle...). A **legend class** is the kind of Great
Spellweaver a legend became (vault: `Society/Stellar Legacy/Stellar Legacy Score.md`): Great Sovereign, Seer,
Concertist, Justiciar, Vanguard, Architect or Chronicler. A seat accepts several classes, so the same position can be
held by different kinds of legend: a High Arbiter can be a Great Sovereign, a Great Justiciar or a Great Architect.

The default seats are `CouncilSeatData` assets in this folder (Create > Game Object > Council Seat); no scene or code
change is needed to add or edit one. Civics add seats of their own while they are active, and unlocks decide how many
positions are open, so the seats on the council change as the game goes on.

| Seat | Accepts | Answers for | Bonuses once a legend sits in it |
|---|---|---|---|
| High Arbiter | Great Sovereign, Great Justiciar, Great Architect | Justice, Defense, Diplomacy | +2 Regalia |
| Oracle | Great Seer, Great Concertist | Mysticism, Faith, Culture | +2 Chorus |
| Grand Archivist | Great Chronicler, Great Architect, Great Sovereign | Lore, Innovation, Governance | +1 Aureus, +1 Regalia |
| Master of Craft | Great Concertist, Great Seer, Great Architect | Industry, Innovation, Culture | +2 Waltz |
| Treasurer | Great Architect, Great Sovereign, Great Seer | Economy, Logistics, Welfare | +2 Aureus |
| Supreme Commander | Great Vanguard, Great Justiciar, Great Sovereign | Defense, Security, Exploration | +1 Aureus, +1 Chorus |
| Civil Regent | Great Sovereign, Great Vanguard, Great Architect, Great Concertist | Governance, Welfare, Security | +1 Regalia, +1 Waltz |

The council is a Head of State (any legend that can be one) and six regular positions (vault: "6 Legends and a Head
of State"). Positions open with the seats in `order`.

The Grand Archivist is the only seat that accepts the Great Chronicler (the class is new with the vault's seven).
Seven default seats share six positions: the first three open at the start (High Arbiter, Oracle, Grand Archivist),
and the rest wait in the seat pool until a position opens or the player swaps one in.

## Fields

| Field | Notes |
|---|---|
| `title` | Unique. How the council, logs and tooltips name the seat. |
| `description` | Flavour line for the seat's tooltip. |
| `icon` | Seat icon. |
| `order` | Position in the pool: the first open council positions take the lowest. |
| `allowedClasses` | Legend classes that can sit here. |
| `areas` | The areas of affairs the seat answers for, its main charge first (ids or aliases from `Council Areas`). Stories call on seats by area (`# cast: area:defense`), never by title. |
| `bonuses` | Given as soon as a legend sits in the seat (see `GameData/GameObjects/EFFECTS_SYSTEM_README.md`). |

## Areas

`Council Areas.asset` (a `CouncilAreaCatalog`) is the vocabulary of areas: each has an id, a name, aliases (other words
a seat or a story may use: "military" means defense) and related areas. A story that asks for an area is played by the
seated legend whose seat covers it (the seat that lists it first wins a tie); with none free, the seat of the closest
related area (up to `maxDistance` links); then the Head of State; then the player chooses. To add a seat, give it
areas; to add an area, add it here and relate it to its neighbours. Civic positions (`CivicData.councilPosition.areas`)
work the same: Field Marshall answers for defense, Grand Archmage for mysticism, Major Chief Innovator for innovation.
The areas and their links are proposals; the vault names seat duties only in their descriptions.
