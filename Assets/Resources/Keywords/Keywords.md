# Keyword Masterfile

Everything a player sees when they hover a golden word in a tooltip. This file is the whole of the tooltip lore
layer: the White-Haven Library (`Resources/Library`, opened with L) is the archive, a separate system, and nothing
here needs it to change. The same format as the Sonata website's masterfile, with Ages and civics where the website
has chapters.

**Format.** One `##` heading per term. Under it, optional metadata, then one or more states:

    ## Auric Peach
    wiki: auric-peach
    also: Golden Peach

    @default
    Shown until a state below applies.

    @age-1
    Shown from Age 1 onward (the vault's "Ages 1": the Ages after the Age of Desolation), until a later one.

    @age-of-renewal
    Shown only during the Age of Renewal.

    @civic-moonlit-vigil
    Shown while the civic Moonlit Vigil is in force, whatever the Age.

`@default` is what a term says before a later state applies. The game starts in the Age of Desolation (Age 0), so
the default is what the first Age knows. `@age-N` replaces it from Age N onward (the vault's "Ages N" folders).
An Age's own id (`@age-of-renewal`: its title in lower case with dashes) applies only during that Age, over any
`@age-N`. `@civic-<name>` (the civic's name, the same way) applies while that civic is in force, over any Age. The
Library's ages table (the top of `Library.json`) lists every Age and its number.

`wiki:` is the White-Haven Library entry the card's "Open in the White-Haven Library" opens: the title in lower case
with dashes (`auric-peach`; a section of a long note `note--section`; a Game Wiki article `game-housing`). Leave it
out and the card finds the entry of its title. `also:` lists extra spellings that resolve to the same card; plurals,
possessives and a leading "The" are handled already.

Game extensions: `id:` binds the card to a game term so it also shows live numbers (`pillar:waltz`, `stat:piety`,
`resource:food`, `tech:horology`, `section:heartlands`, `time:cycle`, `term:population`...), `category:` sets the
small-caps line (default: the game's kind, or the entry's category), and `autolink: off` leaves the word plain
unless a `[[link]]` names it.

A word with no card still opens one: its Library entry's summary (the lead of its note), as the website falls back
to the article's opening. A card is for when that is too much, or when what the word means changes with the Ages or
the civics. Lore is quoted from the vault as written, never paraphrased; rules are the game's own words. A state
still reading `TODO` is never shown, and anything inside `<!-- -->` is ignored, so leave yourself notes freely.

<!-- ==================================================================== -->
<!-- The settlement: game rules (the Game Wiki has the long read)          -->
<!-- ==================================================================== -->

## Population
id: term:population
wiki: game-population
also: Citizens

@default
The citizens of your settlement. Each needs [[Housing]] and eats [[Food]], and each produces [[Research]].

## Housing
id: term:housing
wiki: game-housing

@default
Room for citizens. Newcomers settle only where there is free Housing; without it they wander as [[Vagrants]].

## Vagrants
id: term:vagrants
wiki: game-vagrants
also: Vagrant

@default
People without a home. They join the [[Population]] as [[Housing]] frees up.

## Morale
id: stat:morale
wiki: game-morale

@default
The mood of your people. Above its balance, growth needs less [[Food]] and production runs faster; below it, both
suffer.

<!-- The vault's Piety.md is empty: the card is the game's rule until it has words. -->
## Piety
id: stat:piety
wiki: game-piety

@default
Every decision roll gains the Saving Roll bonus below (capped), turning some failures into successes. It never
lowers a roll. With a bonus of 10 or more, a [[Critical Failure]] can no longer happen.

## Critical Failure
id: term:critical-failure
wiki: game-critical-failure
also: Critical Failures, critically

@default
The worst decision rolls, 10 or less after [[Piety]], fail critically when the choice has a critical failure
written for it. A choice's tooltip warns when one is possible.

## Enlightenment
id: term:enlightenment
wiki: game-enlightenment
also: Enlightened, Eureka

@default
Meet every goal written on a technology's bar and it is enlightened: it shows in the tree at once, even before its
prerequisites, and part of its research is paid on the spot.

## Luck
id: term:luck
wiki: game-luck

@default
Each challenge shows its chance of success as a sign of Luck, from Forsaken to Fated. [[Piety]] improves every roll.

<!-- ==================================================================== -->
<!-- Pillars of Civilization: each pillar's epigraph (Waltz Pillar.md...)  -->
<!-- ==================================================================== -->

## Waltz
id: pillar:waltz
wiki: waltz-pillar
also: Waltz Pillar

@default
_Power should be democratized in the hands of the many in a massive orchestra of [[Spellweaver]]s, their collective
performance dictates equal access in responsibility._

## Regalia
id: pillar:regalia
wiki: regalia-pillar
also: Regalia Pillar

@default
_Power should be concentrated in the hands of a few highly potent [[Spellweaver]] soloists, their individual
performance harnesses the entire responsibility of magic._

## Aureus
id: pillar:aureus
wiki: aureus-pillar
also: Aureus Pillar

@default
_Power is found in the indomitable spirit of [[Humanity]], in our creator the [[Auric Aria]], and in the fate that
[[Humanity]] carves out._

## Chorus
id: pillar:chorus
wiki: chorus-pillar
also: Chorus Pillar

@default
_Power is found in the reverence and awe of nature, in the unknown that exists outside of our control, and in the
vast beyond of [[The Infinite Void]]._

<!-- ==================================================================== -->
<!-- The Ages: what each Age knows (Ages.md, "Tier 1: Ages of Foundations  -->
<!-- & Early Magic"). Later Ages' lines are in the same note.              -->
<!-- ==================================================================== -->

## Ages
wiki: ages

@default
Fractured [[Arcanoria]] after a [[Cataclysmic Aftermath]].

Flickering Staccato Rhythm. Unreliable [[Unison]] and [[Dyad]] Spells. Mostly [[Minor Note]] magic.

@age-1
Healing from the [[Cataclysmic Aftermath]] fallout.

Introducing Legato. First Stable [[Unison]]. Introducing [[Major Note]]s, and sparse [[Dyad Chord]]s Magic.

@age-2
Establishment of [[The Principles of Magic]]. Rise of the [[Mythical Virtuoso]]

Introducing Ritardando. Stable [[Dyad Chord]]s. [[Unison]] Primacy. Rare [[Triad Chord]]s. Basic Magic.

## Age Crisis
wiki: age-crisis

@default
The [[Age Crisis]] teaches that there are [[Civilization]] changing events at the end of all [[Ages]].

_[[Age Crisis]] exist._

@age-1
The [[Age Crisis]] teaches that [[Civilization]] can fail and the resolution of the [[Cataclysmic Aftermath]] leads
to a different [[Golden Age]], [[Classical Age]], or [[Dark Age]] from the survivors.

_You can fail an [[Age Crisis]]._

@age-2
The [[Age Crisis]] teaches that there's more than one [[Age Crisis]] possible, and the paths of [[World Event]]s have
variation depending on the resolution.

<!-- ==================================================================== -->
<!-- Age 0: the Age of Desolation (Age of Desolation.md and                -->
<!-- The Inescapable Hunger.md). Later states quote "Themes and Legacy",   -->
<!-- what later Ages remember.                                             -->
<!-- ==================================================================== -->

## Age of Desolation
id: section:age of desolation
wiki: age-of-desolation
also: Age 0

@default
The once-great civilization is now reduced to a faraway memory, dwindled to dust, and scattered survivors
struggling to find food, shelter, and safety.

## The Inescapable Hunger
wiki: the-inescapable-hunger

@default
It is simply **the arithmetic of too many mouths in a wounded world** depending on a single miraculous harvest: the
[[Auric Peach]].

@age-1
_Also known in later Ages as: **The Auric Peach Famine**, **The Ash-Bread Winter**, and **The First Reckoning**_

> “No single gift can replace understanding.”

## Auric Peach
wiki: auric-peach

@default
The primary food source of this era is the **Auric peach**: a fast‑growing, shallow‑rooted fruit tree that can take
hold even in ash‑choked, Fallout‑touched soils brushed by weakened leylines.

@age-1
Whenever a ruler, priesthood, or Enclave proposes relying on:

- One crop,
- One relic,
- One god‑touched technology,

there is always some historian or Seed‑Keeper who quietly asks:

> “Have you forgotten the peaches?”

## Peach Sickness
wiki: peach-sickness

@default
Crucially, this is **not famine in the cinematic sense**. There are still peaches. People are eating. Storerooms are
not yet empty.

They are dying of **malnutrition inside a monocrop plenty**.

## Old World Relics
wiki: old-world-relics

@default
[[Old World Relics]]—broken Ornaments, inert tools, shards of Emberwhisper and Glimmerfern—are everywhere in ruins.

@age-1
Every later scholar of relic‑lore remembers that, in the Age of Desolation, people **ground up the last words of the
past to stretch one more season**.

## Agromagical Conclaves
wiki: agromagical-conclaves

@default
From these roots, the earliest **Agromagical Conclaves** form—seed communities that will later crystallize into
full [[Agromagical Enclaves]] in the [[Age of Renewal]].

@age-of-renewal
Fields are understood—by those who study history—to be **instruments**:

- They can be tuned, overplayed, muted, or broken.
- Endless extraction without rest leads not just to exhaustion, but to **silence**.

This philosophy becomes the core ethic of later [[Agromagical Enclaves]]: the land’s health is an ongoing
composition, not a pile of resources.

<!-- Still to write: until then each shows its Library summary. -->

## Golden Ash
wiki: golden-ash

@default
TODO — the card, if the Library's summary is too much.

## Searstone
wiki: searstone

@default
TODO — the card, if the Library's summary is too much.

## Phosflare
wiki: phosflare

@default
TODO — the card, if the Library's summary is too much.

## Cataclysmic Aftermath
wiki: cataclysmic-aftermath

@default
TODO — the card, if the Library's summary is too much.

## Vaelia
wiki: vaelia

@default
TODO — the card, if the Library's summary is too much.

<!-- ==================================================================== -->
<!-- Civics. The vault's Age 0 civics (Civic.md) are not in the game yet,  -->
<!-- so no card follows a civic. Once Moonlit Vigil is one, this card      -->
<!-- (quoting its entry) can move above the line:                          -->
<!--                                                                      -->
<!-- ## Glimmerfern                                                        -->
<!-- wiki: glimmerfern                                                     -->
<!--                                                                      -->
<!-- @civic-moonlit-vigil                                                  -->
<!-- Courtship rituals in [[Glimmerfern]] groves during a full [[Moon]].   -->
<!-- In silence people meet a matching frequency.                          -->
<!-- ==================================================================== -->
