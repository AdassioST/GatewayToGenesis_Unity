# Keywords

Golden words in tooltips. Hover one and its **card** opens: a few lines saying what the word means *now*, and a
link into the White-Haven Library for the whole story. The same two layers as the Sonata website:

| | What | Where |
|---|---|---|
| **Cards** | Short, hand-written, staged by Age and by the civics in force: a card never knows more than the world does yet | `Keywords.md` (this folder) |
| **The White-Haven Library** | The wiki: the Glossary (the vault's lore, every note whole) and the Game Wiki (how the game works). Opened with L, or from a card | `Resources/Library` (see its README) |

Neither needs the other: a card with no Library entry still opens (it has nowhere deeper to go), and a word with
an entry but no card shows the entry's summary, the lead of its note.

## Writing a card

`Keywords.md` is the masterfile, in the website's format (its header explains it all):

    ## Auric Peach
    wiki: auric-peach
    also: Golden Peach

    @default
    What the first Age knows.

    @age-1
    From Age 1 onward.

    @age-of-renewal
    Only during the Age of Renewal.

    @civic-moonlit-vigil
    While the civic Moonlit Vigil is in force.

- **States**: `@default`; `@age-N` from Age N onward (the vault's `Ages N` folders; the game starts in the Age of
  Desolation, Age 0); an Age's id (`@age-of-renewal`) only during that Age; `@civic-<name>` while that civic is in
  force. The civic wins over any Age, an Age's own state over `@age-N`, the latest `@age-N` reached over the
  default. A card with a later Age's state says "Its meaning deepens in a later Age."
- **`wiki:`** the Library entry to open (the title in lower case with dashes; sections `note--section`; Game Wiki
  articles `game-housing`). Without it, the entry of the card's title.
- **`also:`** other spellings. Plurals (Peaches, Treasuries), possessives and a leading "The" work already.
- **Game extensions**: `id: kind:name` shows live numbers too (`pillar:`, `stat:`, `resource:`, `tech:`,
  `section:`, `time:`, `term:`), `category:` sets the small-caps line, `autolink: off` keeps the word plain unless a
  `[[link]]` names it.
- A state reading `TODO...` is never shown; `<!-- -->` is a note to yourself. Lines wrap freely: a single line break
  is a space (lists and quotes keep theirs).
- Lore is quoted from the vault as written, never paraphrased. Rules are the game's own words.

## Which word means what

In tooltip text, a word is looked up in this order: a card (its title and `also:`), a game term (pillars, aspects,
resources, technologies, sections, Seventh and Ritual Seventh; Phase, Echo and Cycle by `[[link]]` only), then a
Glossary entry. Quiet entries (ordinary words such as Void, Echo, Legend) light up only through `[[links]]`. Write
`[[Term]]`, `[[Term|shown words]]` or `[[Note#Section]]` in any tooltip text to link on purpose.

`ContentValidator` (at start-up, and `ContentTests` in the Test Runner) reports a card whose `wiki:` names no entry,
whose `id:` is no game term, or whose states name an Age or civic that does not exist.

## Code

`Keywords` (the words, `Linkify`, `TryBuild`, `AddLore`), `KeywordMasterfile` (parsing and readings, pure and tested
in `LibraryTests`), `KeywordLiveValues` (live numbers), `KeywordMarkup` (links, Markdown, glyphs). The current Age is
`GameAge`. See `Scripts/ARCHITECTURE.md`, "Keywords and the White-Haven Library".
