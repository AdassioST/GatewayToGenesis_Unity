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

Each citizen is one person, at every settlement size. Families have children and people die as the calendar passes; a Cycle represents one year. Health and food shortages affect the balance.

At the beginning, 50 survivors seek a place here. Each arrival needs a roof (or permission to settle without one) and 12 rations for the journey. This group is finite. Later newcomers arrive through the world's migration and story events. Filling the food stores does not create children.

All residents eat, including those without homes. One Food represents a daily ration equivalent of 2,100 kcal, with an additional distribution allowance. Better rationing reduces waste, never the need to eat. Full housing does not prevent storing food for winter.

A small community shows each person in the settlement. As it grows, a bounded crowd represents the rest in the distance; the population count remains the full number of people.

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

People without a home. They join the [[Population]] as [[Housing]] frees up. Once the council issues [[Edicts]],
*The Roofless* decides whether they are let in at all (Homes First turns them away; the Almshouse Charter houses them
twice as fast).

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
- **Edibles, Teas, Beverages, Ingredients and Spices.** Every store is labelled for the kitchen. Edibles are actual
  food and are eaten first. Teas, the Eleos brews steeped from the [[Eleos Bloom]]s and ordinary infusions, are drunk
  like food alongside the edibles, but are a category of their own: there will be many kinds, and a nation can have a
  national tea. Beverages are the cellar's ales, meads, wines and spirits, brewed and distilled in the cellar from
  grain, honey, rice and peaches and never from a bloom (a bloom steeped or distilled is a tea). They are drunk for
  joy rather than hunger: a luxury of their own, and the last thing the stores open when Food runs short. Ingredients
  (Peach Pits, Wild Honey) are what dishes are made from, eaten raw only once the edibles and teas run out; eating
  dried peaches leaves Peach Pits behind. Spices ([[Glimmerfern]]) feed no one and are never drawn on for hunger. All
  five grow into your [[Culture]]'s foodways.

## Culture
shelf: Civilization
category: Game Rule
also: Nation, Founding Myth, National Food, Foodways, Culture Lens, Reform, Cohesion, Rootedness, Local Customs

Your nation's own ways: unique to it, and grown from what it lives. Open it from **Your Nation** on the capital's HUD
or the nation's banner in the world view's top left corner.

- **The founding myth.** Researching [[Horology]] lets the people count the Sevenths, and the children ask what came
  before the first. "Why were we founded?" has three answers: to keep the song (Idealism), to share the hearth
  (Realism), to build on the ruins (Pragmatism). The answer founds the culture on a baseline and gives a small effect
  for as long as the myth is told. Then the people name the nation, what its citizens are called and what its
  culture is called (Iridia; Citizens: Iridian; Culture: Iridian). A nation may rename itself later.
- **Leanings and character.** The culture leans toward the ten ways of living the [[Civic]]s and [[Enclave]]s are
  divided into (Agromagical, Militant, Auric, Weaver, Domestication, Trading, Industrious, Regal, Indulgent,
  Esoteric). Each [[Seventh]] it drifts a little toward what it lives: its founding myth, its [[Pillars]] (Aureus
  leans Auric, Regalia Regal, Waltz Weaver, Chorus Esoteric), its civics, what its people eat and work, the land it
  lives on and its districts. Its strongest leaning is its character, which gives a small effect of its own.
- **Foodways and national foods.** What the people keep and eat grows familiar, Edibles, Teas, Beverages, Ingredients
  and Spices each apart (see [[Stored Food]]). When one has been the first of its class for a whole Phase and is familiar enough, a
  story asks whether it is the nation's own. Embraced, it becomes the national food (or tea, or drink, or ingredient, or spice) and its
  output grows; the people may instead keep their table varied. Embracing the Auric peach deepens the monocrop.
- **The land.** The culture takes root on the land you hold, fastest near settlements, and fades from land you lose.
  Rootedness is how much of your land it has become: how long your people have lived there, not whether they
  agree. The Culture lens on the [[The World Map|world map]] shows it.
- **Local customs.** Each settlement keeps its own customs, begun where its ground knows them (Crossing Songs on a
  river, the Rite of the First Golden Fruit among orchards, the [[Moonlit Vigil]] in Glimmerfern groves) with a
  gathering from its card on the world map. Customs travel only by real contact: an open road between settlements,
  a cultural party that takes a custom up in one town and performs it at a festival in another, the founders of a
  new settlement, and returning settlers. A community takes a custom up only after it has come to know it and
  gathered for it; its card shows who brought each one. A road cut by a threat or another authority stops new
  contact but takes nothing away. People who arrive from an unknown origin bring no assumed custom.
- **Reform.** Once in a while the culture can be turned toward another way. Taking in the ways of the investigated
  ruins of a fallen settlement is a Digestive Rebirth; with no ruins to digest, the people follow slowly and the
  culture loosens its hold on the land.
- **Its life.** Once founded, the culture also sings, feasts, cooks, names and celebrates: see
  [[The Life of the People]].

## The Life of the People
shelf: Civilization
category: Game Rule
also: Unity, Happiness, Luxuries, Festival, Holiday, Cultural Party, Kitchen, Landmark, Flavor Log, Trial Batch, Whispered Recipes

What your people do once surviving is not all they do. Everything here opens once the [[Culture]] is founded; every
number is a proposal.

- **Unity.** The shared life of your people, a resource: an orchestra over a soloist. It is made by song (the more the
  culture leans Weaver, the more it sings), civics of music, rite and feast, districts of song, faith and pleasure,
  their landmarks, and in bursts by rites, festivals and holidays. Happy people make more of it. It is spent to raise
  landmarks and to set holidays apart.
- **Rites and gatherings** (Culture window: Rites). An Evening of Song, a Rite of Remembrance for those the Hunger
  took, the Rite of the First Golden Fruit (once an Echo), the Feast of Abundance: each costs a little, gives Unity,
  morale for a while and joy, and leans the culture toward its way of living.
- **Cultural parties and festivals.** An expedition forms with a charter: an Expedition explores, Builders only work
  the land (faster, wearing less), and a Cultural Party (after The Rekindling) holds festivals. Walked into one of your
  settlements, a party sets a festival's table from the stores and celebrates for a couple of Sevenths: Unity, morale,
  joy, the settlement's Composure eased, the culture taking root around it, and Fragments of Meaning for every legend
  in the party. A settlement can celebrate once a Phase.
- **Holidays** (Culture window: Holidays). Once your people have held a festival, with morale well above its balance
  and Unity to spend, they may set the day apart. The holiday records its date (the Seventh, the Phase, the Echo and
  the Cycle it was set apart on) and remembers the latest moment of the heritage (a national food, a festival, a
  landmark, or simply their joy). It returns on that Seventh of that Phase of every Echo: a table set from the stores,
  Unity, morale and joy; each holiday on the calendar raises max morale for good. The eve is announced.
- **Names.** The culture names what it builds in its own voice: its character picks the words (a Weaver people names
  in verses and canticles, an Esoteric one in veils and moons) and its founding myth the memory (the Last Verse, the
  Shared Fire, the Standing Wall). New hamlets and new districts take the names it gives; so do landmarks and
  holidays.
- **Landmarks.** Districts of faith raise a shrine, then a temple, then a cathedral (a Religious Haven can too);
  districts of song a Hall of Song and an Amphitheatre; districts of pleasure a Feast Hall and a Pleasure Garden. Each
  is raised from the district's card on the map, is named by your people, and gives Unity (faith landmarks Faith too)
  and a place in their happiness.
- **The kitchen** (Culture window: Kitchen). The Flavor Log turns what your [[Stored Food]] holds into dishes that feed
  more than their ingredients: Peach Soup, Pit-Oil Flatbread, Riverfish Rice, Hunter's Stew, Honeyed Grain Porridge,
  the Ash-Loaf baked in memory of the starved, the Behemoth Feast Roast, Saffron Riverfish Pilaf and Honeyed Peach Tart. Cook a batch, or leave a standing order to
  cook every Seventh as far as the stores allow. Dishes grow familiar like any food, and may become national.
- **The cellar** (Culture window: Kitchen, then The cellar). The same Flavor Log brews Beverages: Rootgrain Ale from
  grain, Wild Honey Mead, Highland Rice Wine and Auric Peach Wine, and, once the fire is mastered, distils spirits
  from what was fermented (Bitterroot Spirit from ale, Auric Peach Brandy from peach wine). A drink need not feed
  more than went into it: it is kept for joy, ales sour quickly, wines keep and spirits never spoil. Drinks meet
  the people's want for wines and spirits, grow familiar, and may become the national drink. Peach wine and brandy
  are still the peach. No drink is ever made from an [[Eleos Bloom]]: that is a tea.
- **Dishes and drinks of your own** (Culture window: Kitchen, then Invent a dish, or in the cellar Invent a drink).
  Choose what goes in (up to four stored foods, as much of each as you like), whether a drink is brewed or distilled,
  and name it. It is made the way of the dish or drink your people already know that is closest to it: that decides
  how much a batch makes, how much more it feeds, how it keeps (a spirit never spoils), its luxury and its look. What
  it does comes from its ingredients, each scaled down by its share of the batch: food value, spoilage, the peach,
  the ways of living it leans your culture toward, the Faith a sacred tea gives, and the luxury of any ingredient
  that makes up enough of it (honey in a sweet). It is cooked, brewed and ordered like any other recipe. Your people
  take to their own sooner than to anything else: it grows familiar faster, comes first at the table, needs less to
  be offered as national, and is offered before anything else. A drink is still never made from a bloom or a tea,
  and a spirit is still distilled from something fermented.
- **Trial batches.** No one names a dish they have never tasted. Before keeping a mix, choose **Try a batch**: it is
  cooked or brewed once and uses up what goes in, and only then do your people know what it makes. The kitchen tries
  three batches a Seventh. Every trial is remembered with its numbers, whatever the batch size, so the same mix is
  never a mystery twice.
- **Whispered recipes.** The Flavor Log also whispers of recipes no one ever wrote down, one riddle at a time. Each
  trial says how near it came to one (nothing, a faint hint, close, very close); coming close brings its next hint to
  light, and only a batch that makes it finds it. Finding one earns 2 Era Score and a place in your people's
  history, and the dish or drink they keep from it is finer than its ingredients alone: it feeds more, and may never
  spoil, count as a luxury, or give Unity or Faith.
- **Happiness and luxuries.** Happiness is surviving (fed, housed, stores that feel secure, a varied cellar) and living
  (luxuries, fine dishes, the joy of festivals and holidays, landmarks). On the frontier only surviving counts; past
  150 citizens living starts to weigh in, and a grown people cares as much about living as about surviving. Luxuries
  (sweets, fine dishes, teas, wines and spirits, spices, fine cloth, ornaments and living gardens) are wanted per hundred citizens,
  one more kind as the people grow. Each Seventh only the best available categories needed are used. Happiness
  moves morale and how much Unity is made. Edible luxuries are reserved for survival during food shortages.
- **Lasting amenities and Faith.** Sky Glass is enjoyed without consuming it, so it remains available for cathedrals.
  Surveyed Vow Orchids, Candlevein Blooms, Xochi-Singers, Memory Marigolds and Lullroots on land you hold provide living
  garden amenities, scaled by their vigor and the share of the patch you hold. Withered blooms provide none; nothing
  needs to be picked. These gardens, Sky Glass and sacred Eleos teas also give Faith when enjoyed. Candlevein Grief
  Tea and Lullroot Tea have their own stocks; ordinary Hearthleaf Tea brings comfort without Faith.
- **Specialty resources.** Auric Saffron and Hearthleaf grow on naturally desirable land. Silver Salt comes from
  highly coherent sea shores touched by leylines. Sky Glass lies on coherent high ground and beside sacred sites.
  Auric Saffron and Silver Salt are spices, not sustenance; combine them with fish and rice in the kitchen.

## Morale
shelf: Civilization
category: Game Rule

The mood of your people. Above its balance production runs faster; below it, production suffers. Morale does not reduce a person's nutritional needs.

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
- Once the council holds three seats, counting the Head of State, it issues [[Edicts]].

## Edicts
shelf: The Council
category: Game Rule
also: Edict, Stance, Stances, Laws, Council Accord, Decree

The realm's laws and decrees, set from the **Edicts** section of the Government tab. They are established once the
council holds three seats, counting the Head of State ([[Edicts of Stone and Bone]] usually opens the third); until
then the realm keeps its customs.

- **Stances** are the standing laws: one option is always in force for each. *The Roofless* (who may enter without a
  home: Homes First, Open Gates, the Almshouse Charter), *Strangers at the Gates* (Welcome the Wanderers draws more
  caravans, Sealed Gates draws none), *Hearth and Cradle* (more or fewer births), *The Borders* (the Realm's border
  policy: Claim the Horizon, Measured Borders, Hold the Borders), and the two questions of the [[Pillars]]: *Who May
  Weave* (the Guarded Solo or the Open Orchestra) and *Where Power Is Heard* (inward or outward). A changed stance
  stands for a Phase before it can change again.
- **Laws shape the government.** Each leaning option adds to its pillar, so the laws you keep pull your political
  compass, and with it your government type.
- **Edicts** are decrees the Head of State seals for a cost: open the granaries, call every hand to toil, keep the
  scholars' vigil, muster the wardens. Each fills one slot while it lasts (three seats hold one, a full council of
  seven holds five), then rests before it can be sealed again. The legend whose seat answers for an edict's area
  (defense, lore, welfare...) carries it out, and makes it stronger. With no legend in the Head of State's seat, no
  edict can be sealed.
- **The council's accord.** Each seated legend weighs the laws in force by the areas their seat answers for: a
  Supreme Commander approves of Sealed Gates, a Treasurer objects. The government weighs them too: a law that leans
  the way its compass leans sits well, one against a pillar it holds firmly does not. A council in harmony makes its
  legends more effective; one in discord costs morale, and its laws take twice as long to change.

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
  around. Hold Shift to survey the cell you click. Expeditions are legends: see [[Expeditions]].
- Units live on rations. Inside your authority your stores keep them fed, a settlement best of all; beyond it they
  eat what they carry, and a camp gathers what the land gives. Walking and working tire them; harsh weather, danger,
  dissonance and above all hunger wear them down. A tired, worn or hungry unit walks and works slower, an exhausted
  one makes camp and goes on once rested, and one worn out entirely is lost. Make camp to rest, and send a unit back
  for rations before they run out.
- An expedition comes to know the hexes it walks on and beside. A cell is explored once an expedition has seen all
  seven of its hexes up close: then its ground yields if you hold it, and what stands on it is found. Now and then a
  party passing by also stumbles on spare resources or something more.
- **Surveying**: select an expedition and choose *Survey this meso hex* or *Survey another meso hex* (then click it),
  or Shift + right click a cell, or select a cell and send the nearest expedition from its card. The party walks to
  each of the cell's hexes in turn and surveys it there. It is slower than passing by, but a survey is far likelier to
  turn up an event (old waymarks, a hidden spring, traces of those before) and spare resources. A tag over the
  party shows how far along it is ("Surveying 43%"), and the map shows its plan like the research plan in the
  technology tree: each hex still to survey is ringed and numbered in the order the party will walk it (gold on the
  one under way, violet after), with its way drawn from the party. While you pick a cell to survey, hovering one shows
  the tour it would walk there. Moving it within the cell keeps the survey going; resting,
  retreating or walking back for rations only interrupt it. Any other order sets the survey aside with its progress
  kept: choose *Resume survey*, or send the party back into the cell. *Survey by itself* has a party survey cell after
  cell, resting and resupplying as it needs.
- Your authority begins at the capital's own cell; everything around it is wilderness. Once an expedition has passed
  over a cell (not only seen it from afar), click it and claim it for stored food and Elderwood if it borders your
  authority. It joins your authority at once, all seven of its hexes. Each claim costs a little more than the last.
  See [[Territory and Administration]] for how your land also grows by itself.
- Held, explored ground yields resources every second, and what stands on it pays once when it is reached: ruins,
  orchards, hamlets where a legend waits to be met, spires whose songs become ballads.
- Every new Age turns the Grand Thread Rings: the leylines move, rivers they cross run silver, and new things appear
  on the map. The land itself, its rivers and its Sacred Sites stay where they are.
- Every legend of an expedition earns renown for its long roads, its finds and the settlements it founds.
- Landmarks, wonders and ruins do not show through the fog. A landmark shows from afar once your people's sight
  reaches it; before that, only [[Rumours]] tell of it.

## Rumours
shelf: The World
category: Game Rule
also: Rumour, Rumoured Creatures

What travellers, hunters and pilgrims speak of: something worth the walk, somewhere beyond the fog. A rumour is a
guess about the map that only going there can test.

- Your people already tell two rumours when the world begins. Travellers bring one more each Echo, and every
  expedition that completes a journey brings one home. No more are heard while five wait to be followed.
- A rumour gives a direction from the capital (and the region, once its biome has been seen), how far it lies (a few
  days away, far, or at the edge of what anyone knows) and a vague word of what is there. It never names the thing.
  Things close to the capital are never rumoured: your people can see them.
- The map marks the area a rumour points at with a pale haze, never the place itself: the thing lies somewhere near,
  and the haze is not centred on it. Hovering a cell under the haze reads the rumour out.
- Rumours tell of landmarks and places worth the walk, Sacred Sites, wonders seen from afar, the ruins of the Old
  World, Enclaves, and the dens of creatures (told by their size and ways, never by name: the Bestiary lists these as
  rumoured creatures).
- Find the thing and the rumour is confirmed: it is named, and it earns 1 Era Score on top of whatever finding it
  pays. The Rumours button on the world map lists what is heard and what was found.

## Hypotheses
shelf: The World
category: Game Rule
also: Hypothesis, Guess, Insight, What your people think

Once your people identify a creature, the Bestiary asks what they cannot see at a glance: *When does it breed?*
*Where does it thrive?* and, for a meat-eater, *What does it hunt?* Choose a guess under **What your people think**;
watching and hunting then test the guess you chose, and only that guess.

- **Watching**: every Echo, a creature with an identified den within two cells of land you hold is watched. A watch
  tests where it thrives, and tests a breeding guess only in the Echo guessed (its young crowd its dens then, or they
  do not). "No season of its own" holds once it has been watched through all four Echoes with no crowding.
- **Hunting**: every hunt of it by your people tests what it hunts (what it had eaten tells).
- A guess the evidence refutes is ruled out, and the Bestiary keeps it ruled out: choose another. A guess confirmed
  with nothing ruled out before it is **insight**: 1 Era Score.
- Answer every question and your people understand the creature without the research. Until then its niche and its
  breeding season stay hidden, in the Bestiary and on the map.

## Territory and Administration
shelf: The World
category: Game Rule
also: Territorial Pull, Administrative Capacity, Border Policy, Beauty

Everything you build on the map pulls the land around it into your [[Administrative Authority]]. The capital pulls
hardest and farthest, then Major Settlements, a Trade Nexus joined to your roads, Developing Towns, extracted
grandfields, [[Outskirts and Districts|outskirt tributaries]], Religious Havens, the Trade Nodes along your roads and
Outposts. Pull fades over hard ground: open plains
carry it far, forests and marshes less, highlands and cliffs little; roads and river valleys carry it farther.

- **Your society adopts land by itself**, one small hex at a time, from what borders your land and is known to your
  people; a cell joins your land once all seven of its hexes are settled, and your people finish a cell before
  starting the next. It needs people: nothing moves until the capital has 25 citizens, and the more it has, the likelier
  each [[Seventh]] brings in a new hex. The map shows what comes next: the hexes each settlement will bring in glow in
  your border's colour, the next one pulsing, with about how many Sevenths until each joins (up close) or until the
  whole cell is yours (farther out). It takes the best land first: fertile, watered, coherent and beautiful ground, grandfields and sites;
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
  more land stops paying; *Hold the borders* adopts nothing. Claims still go ahead, at once. Once the council
  issues [[Edicts]], the policy is the stance *The Borders*: changing it is a decree, and it then stands a Phase.
- **Beauty**: some places are fair, some hideous: rolling meadows, groves and lakes in view, Sacred ground and the
  lights of leylines against ash, ruins, scars, dissonance and danger. People settle and work beautiful land first;
  it yields a little more, develops a little further and is easier to govern. The Beauty lens shows it.

## Outskirts and Districts
shelf: The World
category: Game Rule
also: Tributary, Tributaries, Outskirt Tributary, District, Districts, Desirability, Minor Hub

There are two ways to settle. An expedition escorting settlers founds an **independent settlement**, a major hub:
a Developing Town inside your authority, an Outpost out in the wilderness, a Religious Haven by Sacred ground. From
[[The Rekindling]] on, you can also raise an **outskirt tributary** from home: a minor hub on land you already hold,
serving a major hub within three cells, the way homes and fields spill out past a city's walls.

- **Desirability**: people want to live on fair, coherent, fertile ground, rich in magic and near the leylines, among
  neighbouring hexes just as good; danger and dissonance drive them off. Every settlement grows faster on desirable
  ground, and a tributary develops toward its ground's desirability (never far above its hub). The Desirability lens
  shows it, and lights up where a tributary could stand now.
- **A tributary leads back to its hub**: it is raised with a road home, stands as a Trade Node on your roads, answers to
  its hub and lends it City Development. It pulls the land around it into your authority and adds Administrative
  Capacity, without being a full settlement: it forms no expeditions (a Barracks aside), holds no Resonance Anchor and
  is never promoted. Each hub keeps only so many (more as its City Development grows), and each costs more than the
  last.
- **Never side by side**: a tributary never stands right beside another settlement, but two cells apart their
  districts touch, and the whole quarter reads as one.
- **Districts**: every tributary begins as an Outskirt Hamlet, homes for the Capital's people. Once it has grown, it
  can be upgraded to one district, one for each kind of [[Enclave]] (changing an upgraded district again costs more
  and rebuilds the quarter):
  - **Agromagical**: growth, health, sanitation, sustenance. Food and a little Glimmerfern, homes kept in health.
  - **Militant**: tactics, weapons, mercenaries, hunters. Wards off danger and holds the land it watches, pulls hard,
    adds an expedition slot and outfits expeditions; Game Meat and Hides; raises Ambition.
  - **Auric**: Research and Faith; raises [[Aureus]].
  - **Weaver**: culture, poetry, songs, communal arts. Lends its hub City Development; raises [[Waltz]].
  - **Domestication**: creatures, [[Pure Light]], taming, druids. Game Meat, Hides and Lumenwool.
  - **Trading**: economics, wealth, efficiency. Much City Development for its hub, Administrative Capacity.
  - **Industrious**: equipment, forges, artisans, metalworks. Elderwood and Duskstone.
  - **Regal**: politics, diplomacy, prestige, espionage. Much Administrative Capacity and pull; raises [[Regalia]].
  - **Indulgent**: entertainment, pleasure, joy, luxuries. Houses travellers and raises [[Morale]]; Wild Honey.
  - **Esoteric**: Outer Gods, dark Magic Arts, witchcraft, spirits. A little Research and Sky Glass; raises
    [[Chorus]].
- **Adjacency**: a district's effects grow with what lies around it, with the districts linked to it and with an
  enclave of its own kind nearby (twice if you hold its Suzerainty; a Regal District's envoys count every enclave).
  An Auric District thrives on leylines, Coherence and Sacred ground, a Militant one on heights and danger, a Trading
  one on roads and a Trade Nexus, an Agromagical one on fertile, watered land, an Esoteric one where dissonance runs.
  Neighbours matter: Weaver and Indulgent quarters feed each other, Agromagical fields help the Domestication herds, a
  Militant District buys the Industrious forges' equipment; but Militant drill yards trouble the Auric scholars, the
  forges' soot troubles the hamlets, and danger spoils the fields unless a Militant District stands beside them. A
  tributary's card shows every district's adjacency on its ground before you choose.
- **A lost hub**: if the hub a tributary serves is lost, the tributary rejoins the nearest hub still standing
  (one with room first) and lays a road to it, keeping its district. One that no road joins to a hub withers away.

## Loss and Ruins
shelf: The World
category: Game Rule
also: Ruins, Damage, Pillage, Repair, Fallen Settlement

Nothing on the map is safe for ever. Every settlement can be damaged: by the danger around it, by stories of raids and
pillage, and, for an outskirt tributary that no road joins to a hub, by slowly withering away. Damage wears its City
Development down and slows its growth; a settlement heals when nothing harms it (faster on the Capital's roads), or you
can pay to repair it at once. The settlement's card shows its damage and what harms it.

- **Anything but the Capital can be lost.** At full damage a settlement falls. The Capital can be profoundly damaged,
  and while it is the whole realm yields less, but it never falls.
- **Loss is transformation.** A fallen settlement leaves its ruins on the map, and its roads stay. Send an expedition
  to investigate them, once: the party salvages what the settlement used to produce, recovers Research from its
  records, and sometimes its records enlighten a technology. Sometimes the ways its people lived by survive: that civic
  can then be adopted from the ruins, without its usual requirements.
- The larger and more cultured the fallen settlement was (a town, a Major Settlement, a haven, a Weaver, Regal or Auric
  district), the likelier its ways survive.

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

Some goals are riddles: the card says only "A riddle", and hovering it reads the riddle out, with no goal and no
progress. Once your people are on the right path (halfway to the goal, or some other sign such as journeys made), a
plainer clue appears under it. A riddle met says what it was.

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
