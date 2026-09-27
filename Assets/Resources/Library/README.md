# The White-Haven Library

The game's wiki, as the Sonata website keeps its archive. Press **L** in game (or click "Open in the White-Haven
Library" on a keyword card). Two halves, each with its shelves and a search:

| Half | What | From |
|---|---|---|
| **Glossary** | The lore: every note of the vault's `Worldbuilding/`, whole, as written | `Library.json`, written by the vault import |
| **Game Wiki** | How the game works | `GameWiki.md` (hand-written articles) and an entry per resource, building, technology, section, pillar, council seat, legend class, legend and civic, written from the game's data |

An article shows its epigraph, "In this note" (the sections of a long note, each an entry of its own), the whole
note, the same entry in the other half, "Threads from here" (related reading) and "Mentioned in" (backlinks).
Links open their entry; hovering one shows its keyword card.

Tooltips never show an entry whole: they show the keyword's card (`Resources/Keywords/Keywords.md`), which links
here.

## The Glossary: importing the vault

The vault (`C:\Arcanoria Master\Arcanoria`) is the canon. **Tools > Gateway to Genesis > Import Lore From Vault**
writes `Library.json` and `Vault~/ImportReport.md`. Change the vault, then re-import; never edit `Library.json` by
hand.

- `Vault~/Manifest.md` says what the defaults cannot guess: passages to take or leave, other names, "See also",
  notes to split, entries taken from part of a note. Its header explains the fields and the selectors.
- `Vault~/ImportReport.md` lists every entry, every passage left out (and why), every `[[link]]` with no entry,
  and the notes not imported. Skim it after an import.
- `Vault~/Canon Gaps.md` lists what the game still shows without a vault source, and gaps in the vault itself.
- Folders ending in `~` are ignored by Unity.

Each entry of `Library.json`: `id` (the title in lower case with dashes; a section `note--section`), `title`,
`category` (small caps), `shelf`, `epigraph`, `summary` (the lead of the note: what a word with no card shows),
`body` (the note; a long note keeps what comes before its sections), `aliases`, `links` (ids), `parent` and
`section` (for sections), `quiet` (an ordinary word: only `[[links]]` reach it) and `source` (the note). The file
also holds the ages table: every Age of the vault's `Ages N` folders with its number, which keyword cards stage by.

## The Game Wiki: writing an article

`GameWiki.md`: a `## Title` per article, then optional `shelf:`, `category:`, `also:` and `id:` lines (the id
defaults to `game-` and the title in lower case with dashes), then the article in the vault's Markdown. The first
paragraph is its summary. An article takes the place of the entry the game writes from its data under the same
title. Rules are the game's own words; lore belongs in the vault.

## Code

`Library` (loads both halves), `LibraryIndex` and `LibraryEntry` (lookups, graph, search; pure), `LibraryArticles`
(the Game Wiki's file; pure), `GameWikiEntries` (entries from the game's data), `LibraryWindow` (the reading room),
`Scripts/Editor/Lore` (the import). Tested in `LibraryTests`, `LoreImportTests` and `ContentTests`.
