# Game Wiki

The White-Haven Library's second half: how the game works, in the game's own words (the first half, the Glossary,
is the vault's lore, written by the vault import). Every resource, building, technology, section, pillar, council
seat, legend class, legend and civic also has an entry written from the game's data; an article here with the same
title takes its place.

**Format.** One `##` heading per article, then optional lines `shelf:`, `category:`, `also:` (other names,
comma-separated) and `id:` (default: `game-` and the title in lower case with dashes, as keyword cards name it in
`wiki:`), then the article in the vault's Markdown. Its first paragraph is its summary. `[[Links]]` open other
entries, this half first. Anything inside `<!-- -->` is ignored.

## Population
shelf: Settlement
category: Game Rule
also: Citizens

The citizens of your settlement. Each needs [[Housing]] and eats [[Food]], and each produces [[Research]].

### Growth

Whenever stored [[Food]] reaches the growth threshold, one threshold of Food is eaten and one person arrives: a
citizen when there is free [[Housing]], otherwise one of the [[Vagrants]], once vagrants are unlocked. [[Morale]]
moves the threshold: above its balance, growth needs less Food.

## Housing
shelf: Settlement
category: Game Rule

Room for citizens. Newcomers settle only where there is free Housing; without it they wander as [[Vagrants]].

Housing comes from the settlement, its buildings and events, and grows with the bonuses of legends, civics, council
seats and the weather: the flat bonuses are added first, then the percentages multiply the total.

## Vagrants
shelf: Settlement
category: Game Rule
also: Vagrant

People without a home. They join the [[Population]] as [[Housing]] frees up.

## Stored Food
shelf: Settlement
category: Game Rule
also: Stores, Food Stores, Pantry, Food Value

Food laid by against lean seasons: [[Dried Auric Peaches]], [[Earth-Beans]], [[Bitter Roots]], [[Deep-Rooted Grain]],
[[Ash-Bread]] and [[Behemoth Meat]]. Each is worth a different amount of [[Food]]: two Earth-Beans feed like one Food,
one Behemoth Meat like five.

- **A reserve.** Whenever Food is falling, the stores feed in the shortfall, the most perishable kinds first, until
  they run out. Only then do people starve.
- **They spoil.** Each kind loses a share of itself every [[Seventh]]: meat quickly, peaches and roots slowly, grain
  hardly at all, ash-bread never. [[Resource Storage]] and [[Resource Preservation]] slow it and add room.
- **They pay for land.** Claiming a cell of wilderness costs food value from the stores, of any kind.
- **Where they come from.** The survivors start with cellars of dried peaches. Held, surveyed land grows its crop, an
  expedition can forage the ground once each Age, and sites and stories give more.
- **The famine reads them.** Their food value counts as reserve against [[Crisis Severity]]; several kinds in quantity
  ease it, and stores that are nearly all peaches make it worse.

## Morale
shelf: Civilization
category: Game Rule

The mood of your people. Above its balance, growth needs less [[Food]] and production runs faster; below it, both
suffer.

## Piety
shelf: Decisions
category: Game Rule

An aspect of [[Aureus]]. Piety gives every decision roll its Saving Roll bonus: the bonus is added to the roll
(never above 100), turning some failures into successes. It never lowers a roll. With a bonus of 10 or more, a
[[Critical Failure]] can no longer happen.

## Luck
shelf: Decisions
category: Game Rule

Each challenge shows its chance of success as a sign of Luck, from Forsaken to Fated. [[Piety]] improves every roll.

- **Fated**: above 90%
- **Blessed**: 60 to 90%
- **Gamble**: 40 to 60%
- **Cursed**: 10 to 40%
- **Forsaken**: below 10%

## Critical Failure
shelf: Decisions
category: Game Rule
also: Critical Failures

The worst decision rolls, 10 or less after [[Piety]], fail critically when the choice has a critical failure written
for it. A choice's tooltip warns when one is possible.

## Decisions
shelf: Decisions
category: Game Rule
also: Choices

When a story asks for a choice, one roll of a hundred decides it, with the Saving Roll bonus of [[Piety]] added
(never above 100).

- A choice without a challenge simply happens.
- A challenge sets your strength in a pillar ([[Waltz]], [[Regalia]], [[Aureus]] or [[Chorus]]) against the
  challenge's strength: your chance of success is the one divided by the other, and meeting it makes success
  certain. The roll succeeds when it lands above 100 minus that chance.
- A success on 91 or more is critical, and a failure on 10 or less is a [[Critical Failure]], when the story has
  one written for it.
- Some choices keep a rare event in their highest rolls.

A choice's tooltip shows its [[Luck]] and whether it can fail critically. Rare events and critical successes are
never shown beforehand.

## The Council
shelf: The Council
category: Game Rule
also: Council, Head of State

Legends govern with you: a Head of State and up to six positions on the council. At first only the Head of State
governs; [[The Rekindling]] and [[Edicts of Stone and Bone]] each open a position, so the council holds three by the
end of the first Act of Fate. Civics in force add positions of their own.

- Each position is a seat, such as the [[High Arbiter]] or the [[Oracle]], that legends of several classes can hold:
  a High Arbiter can be a [[Great Sovereign]], a [[Great Justiciar]] or a [[Great Architect]].
- A seat's bonuses apply as soon as a legend sits in it. The legend's own bonuses apply once it has settled in, a
  few [[Seventh]]s later. An empty seat gives nothing.
- The Head of State multiplies the bonuses of the legend who holds it.
- Once a seat's legend changes, it cannot change again until its cooldown has passed.

## The Calendar
shelf: Time
category: Game Rule
also: Calendar

Time is kept in [[Seventh]]s: 21 Sevenths make a [[Phase]], 3 Phases an [[Echo]], and 4 Echoes a [[Cycle]]. The
21st Seventh of every Phase is a [[Ritual Seventh]].

While a story waits for your choice, time runs slower.

## Ages
shelf: The World
category: Game Rule

The world begins in the [[Age of Desolation]], Age 0, and passes from Age to Age. The banner at the top of the screen
shows the Age, its Act and how much of it has passed; hover it for the details.

- Every Age is made of three [[Act of Fate|Acts of Fate]] (four in the long Ages). Each new Act opens with a story.
- Halfway through the Age its [[Age Crisis]] begins. It does not announce itself: at first only the harvest changes
  colour, golden to pink. Once the crisis is named, the banner turns red, shows its severity, and Food output falls
  while it lasts.
- When the Age's time is spent and its last story is told, the crisis resolves: part of the population is lost, never
  all of it, and the next Age begins. A card tells who survived and which Age begins.
- What a golden word says in a tooltip changes as the Ages pass, and with the civics in force, as the world comes to
  understand it. The White-Haven Library keeps everything written, whatever the Age.

## Crisis Severity
shelf: The World
category: Game Rule
also: Severity

How hard an [[Age Crisis]] falls, from 0% to 100%: the share of people it takes grows with it. Severity is a sum
you can read in the Age banner's tooltip once the crisis is named.

- The crisis itself sets the start.
- Researching the crisis's own technologies, keeping [[Food]] in store, and exploring fertile ground on the world
  map lower it, and so do the careful choices in its stories.
- Desperate relief lowers it too, at a price the stories name.
- Crowding and the choices that feed the crisis raise it.

However badly it goes, a few people always survive, and the world always reaches the next Age.

## The World Map
shelf: The World
category: Game Rule
also: World Map

Before [[Pathfinder Training]] only the capital is known. Researching it opens the world map and sends your first
expedition out of the capital, directed by a legend. Then scroll out past the capital's widest view, press M, or
click The World on the Age banner, and the view pulls back from your capital into the world beyond the walls: the
ground you have explored in full colour, known ground dimmed, and the fog, which begins right beside the capital.
Scroll in again at the closest zoom over your capital, or press Esc, and you return to the capital at its widest
view.

- The world reads at three scales as you zoom: **micro**, the small hexes of the ground itself; **meso**, the cells
  you explore and settle (each holds seven small hexes); **macro**, the atlas, where 49 cells make one region and the
  great rivers, coasts and biomes show. Zooming never changes the land, only how much of it you see.
- Drag to look around; the card under the cursor tells what is known of a cell, and zoomed in, of the hex itself.
  Lenses show the fertility of the land, Coherence, the magic this Age lets you use and more; layers show rivers,
  leylines and the next Age's leylines.
- Click the capital to form expeditions and train builders there (click again to pick the units standing on it).
  Select a unit and right click the map to send it: zoomed in, to the very hex you click; zoomed out, to the heart
  of the cell, or the
  nearest ground it can reach. Units walk the small hexes one by one, a few every [[Seventh]]: rough ground, climbing
  and fording rivers slow them, roads and leylines speed them, and the crags of steep escarpments must be walked
  around. Hold Shift to survey where it arrives. Expeditions are legends: see [[Expeditions]].
- Units live on rations. Inside your authority your stores keep them fed, a settlement best of all; beyond it they
  eat what they carry, and a camp gathers what the land gives. Walking and working tire them; harsh weather, danger,
  dissonance and above all hunger wear them down. A tired, worn or hungry unit walks and works slower, an exhausted
  one makes camp and goes on once rested, and one worn out entirely is lost. Make camp to rest, and send a unit back
  for rations before they run out.
- An expedition comes to know the hexes it walks beside. Surveying explores them: the hexes around it, or a whole
  cell at once. A cell is explored when all its hexes are: then its ground yields if you hold it, and what stands on
  it is found.
- Your authority begins at the capital's own cell; everything around it is wilderness. Once an expedition has passed
  over a cell (not only seen it from afar), click it and claim it for [[Food]] and Elderwood if it borders your
  authority. Each claim costs a little more than the last. See [[Territory and Administration]] for how your land
  also grows by itself.
- Held, explored ground yields resources every second, and what stands on it pays once when it is reached: ruins,
  orchards, hamlets where a legend waits to be met, spires whose songs become ballads.
- Every new Age turns the Grand Thread Rings: the leylines move, rivers they cross run silver, and new things appear
  on the map. The land itself, its rivers and its Sacred Sites stay where they are.
- Every legend of an expedition earns renown for its long roads, its finds and the settlements it founds.

## Territory and Administration
shelf: The World
category: Game Rule
also: Territorial Pull, Administrative Capacity, Border Policy, Beauty

Everything you build on the map pulls the land around it into your [[Administrative Authority]]. The capital pulls
hardest and farthest, then Major Settlements, a Trade Nexus joined to your roads, Developing Towns, extracted
grandfields, Religious Havens, the Trade Nodes along your roads and Outposts. Pull fades over hard ground: open plains
carry it far, forests and marshes less, highlands and cliffs little; roads and river valleys carry it farther.

- **Your society adopts land by itself**, a cell at a time every [[Seventh]], from what borders your land and is known
  to your people. It takes the best land first: fertile, watered, coherent and beautiful ground, grandfields and sites;
  dangerous, dissonant and mountainous ground last. Each settlement holds only so many cells by its own pull: to hold
  more, found or grow another. Land drawn in by an Outpost stays detached, like the Outpost itself. An enclave's pull
  keeps your people away from its land.
- **Administrative Capacity** is how much land your administration can run: the capital's bureaucracy, your
  Government Capacity, each settlement (more as its City Development grows), roads to the capital, coherent land and a
  legend answering for governance on the council. Every cell you hold weighs on it, far and hard ground most.
- **Wide or tall**: while the load stays under capacity, every new cell is pure gain. Past that, yields and City
  Development slow across the whole realm; past the break-even, more land lowers your total output, and developing
  what you hold pays more. Over capacity your people stop adopting land, and far past it the weakest-held land slips
  back into the wilderness. The Realm panel on the world map shows where you stand and where each threshold lies.
- **Border policy** (Realm panel): *Expand* keeps adopting until the administration is full; *Measured* stops where
  more land stops paying; *Hold the borders* adopts nothing. Claiming still brings a cell in at once.
- **Beauty**: some places are fair, some hideous: rolling meadows, groves and lakes in view, Sacred ground and the
  lights of leylines against ash, ruins, scars, dissonance and danger. People settle and work beautiful land first;
  it yields a little more, develops a little further and is easier to govern. The Beauty lens shows it.

## Expeditions
shelf: The World
category: Game Rule
also: Expedition, Expedition Slots, Director, Mishap, Mishaps

Your legends walk the world as expeditions: parties of up to four legends, one of them the **Director**, who leads.
There are no nameless scouts or settlers: whoever explores, founds towns and faces the road is one of your legends.

- **Slots.** Each legend in the field takes one of your expedition slots: one, two more for each point of Government
  Capacity (Capital improvements raise it) and one for each [[Hollow Watchpost]]. A settlement's card shows them.
- **Forming.** Click one of your settlements (not an Outpost), choose a Director and form an expedition. While it
  stands in one of your settlements, legends join it as companions or leave it, it takes on settlers, and it can
  disband: its legends go home. A legend on the council leaves its seat to set out (not while the seat is on
  cooldown), and a legend on the road cannot sit on the council.
- **The Director's strength.** The Director's [[Soul Leitmotif]] gives the party one strength: [[Luminance]] sees
  farther, [[Cindergale]] walks faster, [[Crystal]] wears slower, [[Void]] eats less, [[Strand]] brings back more,
  [[Flux]] rests faster in camp and [[Resonance]] bears hardship better. Each companion brings back a little more
  from what the party finds, and every legend carries and eats its own rations.
- **Settlers.** Taking on settlers costs citizens and Elderwood. They slow the party and let it found a Developing
  Town, an Outpost or a Religious Haven where it stands; the legends walk on, each honoured for the founding.
- **Hardship.** When things go bad on the road (wear past a fifth, hunger, marching exhausted, harsh weather,
  [[Dissonance]]), the road strains every legend's [[Composure]] each [[Seventh]], the Director's most. Camped in
  one of your settlements, they rest as at home.
- **Mishaps.** The worse things go, and the more strained its legends, the likelier a mishap strikes each Seventh:
  an injury, a fever, spoiled rations, lost bearings, a quarrel, whispers of Dissonance, an ambush. Each strains the
  legend it strikes and wears the party further, and a Spiraling companion may desert and make for home. The
  expedition's card shows the chance. In your settlements only quarrels and desertions can happen.
- **Breaking.** Worn out entirely, an expedition breaks: its legends limp home carrying the road with them, and any
  settlers are lost. A legend lost to Dissonance on the road leaves its companions grieving.
- **Party size.** How many legends walk together is a choice, and each size walks differently (the card's Party row):
  - **Solo**: the fastest and the slowest to wear, and hard to find in dangerous land; half the ambushes it simply
    slips. But no one tends its wounds or finds the way when it is lost, solitude strains it even on a good road,
    and it works the land slowly. It can **retreat** from danger or Dissonance to the nearest safe ground, faster
    still, and once its Composure is Fractured or worse it can **go to ground**: missing in action, it turns up
    Sevenths later at the nearest ground of your territory, worn, hungry and camped. Worn out entirely it goes to
    ground instead of breaking.
  - **Duo**: a little faster than a trio, and the two share the weight of a bad road best. The other handles some
    mishaps, and it can still retreat. The pair quarrels once one of them is Fractured.
  - **Trio**: the ordinary pace. Nearly half its mishaps are handled by the others, and a third legend can step
    between two who quarrel. It is too many to slip away.
  - **Company** (four): slow, conspicuous and quarrelsome, even while its legends are merely Clouded; it has its
    accidents when all goes well and chafes on the road. But more than half its mishaps are handled by the others,
    it sees farther and it surveys and forages quickest.
  A mishap the others handle lands only in part, and the one who stepped in is honoured for it.

## Renown
shelf: The Council
category: Game Rule
also: Legend Rank, Rank

Legends grow through their deeds. Renown comes from leading expeditions, from the verses of the ballads they live,
from sitting on the council through an Act of Fate or an Age Crisis, and from each [[Motif Awakening]]. Renown raises
a legend's rank, up to 5, and each rank above the first adds a fifth to the legend's own council bonuses. A seat's
bonuses never grow. Reaching a new rank is a moment of joy that eases the legend's [[Composure]].

Only the legends your civilization has met can sit on the council: two are known from the start, and the rest are
met on the world map.

## Legend Composure
shelf: The Council
category: Game Rule
also: Legend Traits, Spiraling, Fractured, Awakened State
id: game-composure

A legend's [[Composure]] is how much they can still bear, read in their [[Soul Leitmotif]]: Pristine, Clouded,
Fractured, Spiraling, then Surrender. Hover a legend to see theirs; the numbers in brackets run from 0 to 100.

- **What strains it.** Only the council carries the Age Crisis: every seated legend, the Head of State included,
  strains a little while the crisis grows unannounced and more once it is named, and grieves each time the people
  lose some of their own, citizens and homeless alike. A legend off the council is spared both. Legends on an
  expedition carry the road instead: its hardship when things go bad, and its mishaps (see [[Expeditions]]).
- **What mends it.** Rest. Legends at home ease back toward Clouded quickly, those on an expedition more slowly
  (as at home while it is camped in one of your settlements), seated ones barely. Rising in rank eases it at once.
- **What it costs.** A Fractured legend's own council bonuses fall by a tenth, a Spiraling one's by three tenths.
  A Spiraling legend at work stays among the capital's notices until they rest.
- **Surrender.** A legend whose Soul Leitmotif reaches Surrender is lost to Dissonance: they leave the council and
  their unit, and are never met again. Their deeds are kept.

### Motif Awakening

A legend who stayed Fractured or deeper for a while and heals back to Clouded has a [[Motif Awakening]]: their Soul
Leitmotif gains an [[Ornament]] (the first is its primary Ornament, the second its secondary) in a binding their
personality leans toward, one of their personality traits evolves along it into a middle trait, and the deed brings
renown. A short crack heals without one. Joy can bring the healing too.

With both Ornaments, only a legend brought back from Spiraling awakens again, and they drown in the
[[Catalytic Abyss of Emotion]]: their Soul Leitmotif enters its Awakened State, doubling their council bonuses for a
few Sevenths, then collapses back into cracks.

### Legend Traits

Every legend is born with a Wish (an origin trait), an Expression (three personality traits) and a Soul Leitmotif,
read from the [[Legend Trait]] note of the White-Haven Library. A legend's primary binding is usually the affinity of
their class (a Great Architect's is [[Crystal]]); legends without authored traits draw them from the Library's
tables, the same for the same world. Traits set the seven binding scores shown in a legend's tooltip: every binding
starts at Apprentice, the primary binding adds 5, an origin trait adds or takes what the Library says, and each
Ornament and evolved trait adds more. Bindings do not yet change what a legend does on the council.

## The Technology Tree
shelf: Research
category: Game Rule
also: Technology Tree, Tech Tree, Research Plan

Each Age has its tree of technologies in the research tab (R). A technology is researched by paying its cost in
[[Research]] (and sometimes other resources) a little every second; what is paid is never lost, even when research
moves elsewhere.

### What shows

The tree uncovers itself as you go. It shows what you have researched, what you can research now, and one step
beyond: a technology whose prerequisite can be researched now shows under a blue veil, locked. Anything further is
hidden, with its name, its tooltip and its lines. A technology also shows early when it is [[Enlightenment|enlightened]], and
when the Age waits for it to turn the Act of Fate.

Lines join each technology to what it needs, arrowheads pointing to what comes next. A line appears once both of its
ends are uncovered. Dim stone lines wait on research; a line turns gold once its prerequisite is researched, and
glows softly into a technology you can research now. A short dashed line from nowhere means a prerequisite not
uncovered yet. Hover a technology to light the way to it (or, over a researched one, where it leads).

### The research plan

- Click a technology you can research to research it now.
- Click a locked one to plan its way: every prerequisite still missing is planned first, in order, and research
  starts on the first step. Undiscovered prerequisites are planned too; they show as the way reaches them.
- Shift+click adds a technology to the end of the plan instead of replacing it.
- Right click a planned technology to take it out of the plan, with whatever planned needs it.

Planned technologies carry their number on a small rhombus, gold on the research under way, and the plan's lines turn
violet. When a technology is researched, research moves straight on to the next in the plan.

## Enlightenment
shelf: Research
category: Game Rule
also: Enlightened, Eureka, Eurekas, Enlightenment Goal

Most technologies have an Enlightenment goal, written on the bar along the bottom of their card with how far along it
is: gather so much by hand, build so many of something, witness an event. Meet every goal of a technology and it is
enlightened: it shows in the tree at once, even before its prerequisites, and part of its research (30% of every
cost) is paid on the spot. The bar turns gold. A technology you can research now that the gift pays in full is
researched at once. Some stories enlighten technologies too.

Enlightening an Event or Crisis Technology is a great deed of the Age: it earns 1 Era Score (researching an Event
Technology earns 2 more). Other technologies earn none for their Enlightenment.

## The White-Haven Library
shelf: The World
category: How to Read

Press L to open the Library, or click "Open in the White-Haven Library" on a card. The **Glossary** holds the lore
of [[Arcanoria]] as its records tell it; the **Game Wiki** holds how the world is kept: its rules, and an entry for
every resource, building, technology, section, pillar, council seat, legend and civic.

Golden words in any tooltip open their card when hovered, and a card says only what the world knows by now. In the
Library, links open their entry and hovering one shows its card. Back and Forward walk the pages read; Esc closes
the Library.
