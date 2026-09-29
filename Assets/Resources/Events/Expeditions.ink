// Expedition lines: stories an expedition finds when it surveys a site, one verse per expedition that surveys a site
// of that kind (FeatureSpec.expeditionStories in World.asset). Their ballad actors are the expedition (# cast:
// expedition): its Director leads, its companions are co-protagonists. In-world quotations are the vault's (The
// Inescapable Hunger.md: "Crumbling Infrastructure", "Relics as fertilizer"; The Registers of Magic.md: Resonator
// Arts). Connective prose is placeholder, listed in Canon Gaps.md. Dashes are written as hyphens for the game font.

-> DONE

// ===== THE BALLAD OF THE INFINITE HUM (Fallen Auric Resonators) =====
// Verse I: the first expedition to survey a Fallen Auric Resonator. Verse II: the second. Verse III: the third.

=== expedition_resonator_1 ===
# title: The Infinite Hum
# ballad: resonator_hum
# verse: 1
# ballad_title: The Ballad of the Infinite Hum
# theme: Scholar
# cast: expedition
# description: A fallen resonator that has not stopped singing.
# conditions: event_completed:expedition_resonator_1 < 1
# event_type: Mystical
# priority: 60
# locked: true

Crystalline towers that once tuned weather and stabilized local space lie toppled in fields and valleys. Their faceted surfaces still faintly echo forgotten frequencies, glowing weakly at dusk.

The expedition makes camp in the tower's shadow. The hum does not stop at dusk, nor at dawn.

* Listen closely
-> expedition_resonator_1_chorus_1

=== expedition_resonator_1_chorus_1 ===
The crystal is cracked, but whole enough to learn from, to carry home, or to grind.

* Idealism. Study the hum where it stands.&D Leave the tower whole and write down what it sings.&C pillar:chorus;strength:16;success:expedition_resonator_1_verse_1;failure:expedition_resonator_1_verse_2;success:consequences:resource:Research +45;fragment:protagonist Lucidity +3;score:resonator_hum +1;failure:consequences:resource:Research +15 -> expedition_resonator_1_verse_1

* Realism. Salvage the crystal.&D Aetherlight and stone for the capital's workshops.&C success:expedition_resonator_1_verse_3;failure:expedition_resonator_1_verse_3;consequences:resource:Aetherlight +90;resource:Duskstone +40 -> expedition_resonator_1_verse_3

* Pragmatism. Grind it into the orchards.&D Luminous stone as fertilizer: one more golden harvest, at the soil's expense.&C success:expedition_resonator_1_verse_4;failure:expedition_resonator_1_verse_4;consequences:resource:Dried Auric Peaches +40;score:monocrop +1 -> expedition_resonator_1_verse_4

=== expedition_resonator_1_verse_1 ===
Its famous failure is the infinite hum, which is why it is often paired with Soundproofing Arts and Coherence Arts.

* Keep the notes
-> expedition_resonator_1_outro

=== expedition_resonator_1_verse_2 ===
The hum swallows every note before anyone can write it down.

* Keep what little there is
-> expedition_resonator_1_outro

=== expedition_resonator_1_verse_3 ===
The tower comes apart in faceted shards. Each one goes on humming, quieter, in the packs.

* Carry it home
-> expedition_resonator_1_outro

=== expedition_resonator_1_verse_4 ===
Luminous stones harvested from shattered leylines act as sulfur- and phosphorus-rich modifiers, accelerating root growth and fruiting

* Spread it
-> expedition_resonator_1_outro

=== expedition_resonator_1_outro ===
There are other towers. The expedition can still hear this one when the wind is right.

* Return
-> DONE

=== expedition_resonator_2 ===
# title: The Infinite Hum, Verse II
# ballad: resonator_hum
# verse: 2
# cast: expedition
# description: A second tower, singing the same note.
# conditions: event_completed:expedition_resonator_2 < 1; event_completed:expedition_resonator_1 >= 1
# event_type: Mystical
# priority: 60
# locked: true

A second fallen resonator, leagues from the first, holds the same note. The expedition's scholars swear it answers the one they left behind.

* Compare them
-> expedition_resonator_2_chorus_1

=== expedition_resonator_2_chorus_1 ===
Two towers, one hum. Whoever tuned them tuned them together.

* Idealism. Tune it by ear.&D Match the two towers and learn the interval between them.&C pillar:chorus;strength:18;success:expedition_resonator_2_verse_1;failure:expedition_resonator_2_verse_2;success:consequences:resource:Research +70;fragment:protagonist Lucidity +4;fragment:co Lucidity +2;score:resonator_hum +1;failure:consequences:resource:Research +20 -> expedition_resonator_2_verse_1

* Realism. Take a shard of each.&D Two voices for the capital's workshops.&C success:expedition_resonator_2_verse_3;failure:expedition_resonator_2_verse_3;consequences:resource:Aetherlight +120 -> expedition_resonator_2_verse_3

=== expedition_resonator_2_verse_1 ===
The interval holds. Somewhere between the two towers, a third should stand.

* Mark it on the map
-> expedition_resonator_2_outro

=== expedition_resonator_2_verse_2 ===
The towers drift apart in pitch as the night cools, and the notes are lost.

* Move on
-> expedition_resonator_2_outro

=== expedition_resonator_2_verse_3 ===
The shards are wrapped in cloth so the party can sleep.

* Carry them home
-> expedition_resonator_2_outro

=== expedition_resonator_2_outro ===
The hum follows the expedition home.

* Return
-> DONE

=== expedition_resonator_3 ===
# title: The Infinite Hum, Verse III
# ballad: resonator_hum
# verse: 3
# cast: expedition
# description: The last tower of the chord.
# conditions: event_completed:expedition_resonator_3 < 1; event_completed:expedition_resonator_2 >= 1
# event_type: Mystical
# priority: 60
# locked: true

The third tower completes the chord. Standing between all three, the expedition hears what the hum was meant to hold: weather, once, and the ground kept still beneath it.

* Stand in the chord
-> expedition_resonator_3_chorus_1

=== expedition_resonator_3_chorus_1 ===
The chord could be written down for those who will one day rebuild the towers, or quieted so the land can rest.

* Idealism. Write the chord down.&D For whoever mends the hum.&C pillar:chorus;strength:20;success:expedition_resonator_3_verse_1;failure:expedition_resonator_3_verse_2;success:consequences:resource:Research +120;fragment:protagonist Lucidity +5;fragment:co Lucidity +3;score:resonator_hum +1;failure:consequences:resource:Research +40 -> expedition_resonator_3_verse_1

* Realism. Quiet the towers.&D Stuff the gaps and let the fields around them sleep.&C success:expedition_resonator_3_verse_3;failure:expedition_resonator_3_verse_3;consequences:stat:morale +8;score:famine_preparation +1 -> expedition_resonator_3_verse_3

=== expedition_resonator_3_verse_1 ===
The chord fills three pages. No one yet knows how to stop a hum, but now someone knows what it is.

* Bring it home
-> expedition_resonator_3_outro

=== expedition_resonator_3_verse_2 ===
The chord slips away between one tower and the next.

* Bring home what was heard
-> expedition_resonator_3_outro

=== expedition_resonator_3_verse_3 ===
Cloth, clay and patience. For the first time in memory, the valley is quiet at dusk.

* Leave them sleeping
-> expedition_resonator_3_outro

=== expedition_resonator_3_outro ===
The Ballad of the Infinite Hum is complete. Those who walked it are remembered in it.

* Return
-> DONE

// ===== THE GOLDEN ORCHARD'S SEEDS (Auric Orchards) =====

=== expedition_orchard ===
# title: The Golden Orchard's Seeds
# cast: expedition
# description: Between the golden trees, the old plots.
# conditions: event_completed:expedition_orchard < 1
# event_type: Environmental
# priority: 55
# locked: true

Villages rip out bitter roots and stubborn grains to plant more trees.

Between the rows of this orchard the expedition finds what the villagers ripped out, gone wild: bean vines, bitter roots, a few stalks of grain.

* Walk the rows
-> expedition_orchard_chorus_1

=== expedition_orchard_chorus_1 ===
Survivors abandon deep-rooted grains, bitter roots, leguminous "earth-beans" that could restore nitrogen.

* Idealism. Gather the old plots.&D Seed and root to carry home, for fields that are not all peach.&C pillar:waltz;strength:14;success:expedition_orchard_verse_1;failure:expedition_orchard_verse_2;success:consequences:resource:Earth-Beans +30;resource:Bitter Roots +20;resource:Deep-Rooted Grain +10;score:famine_preparation +1;fragment:protagonist Vision +2;failure:consequences:resource:Earth-Beans +10 -> expedition_orchard_verse_1

* Realism. Gather the golden fruit.&D The peaches are ripe now; the old plots can wait.&C success:expedition_orchard_verse_3;failure:expedition_orchard_verse_3;consequences:resource:Dried Auric Peaches +60;score:monocrop +1 -> expedition_orchard_verse_3

=== expedition_orchard_verse_1 ===
They begin sequencing peaches with nitrogen-fixing earth-beans, reintroducing bitter roots and grains, and treating ash, relic dust, and mineral modifiers as precise tools rather than blanket miracles

* Carry the seed home
-> expedition_orchard_outro

=== expedition_orchard_verse_2 ===
Most of the old plants have gone to seed and rot. A few bean pods are all that is left to take.

* Take what there is
-> expedition_orchard_outro

=== expedition_orchard_verse_3 ===
Cellars are full of dried peaches; shrines glow under luminous boughs.

* Fill the packs
-> expedition_orchard_outro

=== expedition_orchard_outro ===
The expedition turns for home, heavy with what it chose.

* Return
-> DONE

// ===== RESOURCE SITES (ResourceSiteSpec.expeditionStories in World.asset) =====
// Offered to the first expedition that identifies a site of that kind by surveying it. The Moonlit Vigil is the vault's
// (Gateway To Genesis.md: courtship in Glimmerfern groves under a full Moon); Pure Light herds and the Domestication
// Enclave's catalogue are the vault's ideas; the prose is placeholder, listed in Canon Gaps.md.

=== expedition_terraces ===
# title: The Terraces No One Planted
# cast: expedition
# description: Rice on the mountain, and no farmers.
# conditions: event_completed:expedition_terraces < 1
# event_type: Environmental
# priority: 55
# locked: true

Below the peaks, the slope has been cut into steps, and every step is green with rice. Snowmelt runs from one terrace to the next through channels of fitted stone.

There are no farmers, no huts, no tools. Only the rice, and the water, and the steps.

* Walk the terraces
-> expedition_terraces_chorus_1

=== expedition_terraces_chorus_1 ===
The grain is ripe. Whoever built this is long gone, and the terraces have kept going without them.

* Idealism. Take seed, not harvest.&D Carry the seed down to your own fields and let the terraces be.&C pillar:waltz;strength:14;success:expedition_terraces_verse_1;failure:expedition_terraces_verse_2;success:consequences:resource:Highland Rice +15;score:famine_preparation +1;fragment:protagonist Vision +2;failure:consequences:resource:Highland Rice +5 -> expedition_terraces_verse_1

* Realism. Harvest all of it.&D The capital is hungry now.&C success:expedition_terraces_verse_3;failure:expedition_terraces_verse_3;consequences:resource:Highland Rice +45 -> expedition_terraces_verse_3

=== expedition_terraces_verse_1 ===
They take a handful from every terrace, top to bottom, so that the seed remembers the whole slope.

* Carry it home
-> expedition_terraces_outro

=== expedition_terraces_verse_2 ===
A storm comes off the peaks before the work is done. They save what they can.

* Take what there is
-> expedition_terraces_outro

=== expedition_terraces_verse_3 ===
By nightfall the terraces are stubble. The water keeps running through the empty steps.

* Load the packs
-> expedition_terraces_outro

=== expedition_terraces_outro ===
The expedition turns downhill, the terraces behind it catching the last of the light.

* Return
-> DONE

=== expedition_glimmerfern ===
# title: The Moonlit Vigil
# cast: expedition
# description: A glimmerfern grove, and carved benches in the moss.
# conditions: event_completed:expedition_glimmerfern < 1
# event_type: Mystical
# priority: 55
# locked: true

The grove glows faintly, every frond a branching pattern of light. In the moss between the ferns, someone once set stone benches in a ring.

It is a full Moon tonight.

* Sit and wait
-> expedition_glimmerfern_chorus_1

=== expedition_glimmerfern_chorus_1 ===
Courtship rituals in Glimmerfern groves during a full Moon. In silence people meet a matching frequency.

* Idealism. Keep the vigil.&D Sit in silence until the grove answers.&C pillar:chorus;strength:15;success:expedition_glimmerfern_verse_1;failure:expedition_glimmerfern_verse_2;success:consequences:stat:morale +6;fragment:cast Meaning +2;score:moonlit_vigil +1;failure:consequences:stat:morale +2 -> expedition_glimmerfern_verse_1

* Realism. Cut fronds for the workshops.&D Glimmerfern is worth more in the capital than in the moss.&C success:expedition_glimmerfern_verse_3;failure:expedition_glimmerfern_verse_3;consequences:resource:Glimmerfern +20 -> expedition_glimmerfern_verse_3

=== expedition_glimmerfern_verse_1 ===
Near midnight, two of the party realize they have been breathing in time. Neither says anything. Neither needs to.

* Remember it
-> expedition_glimmerfern_outro

=== expedition_glimmerfern_verse_2 ===
The Moon goes behind cloud, and the grove is only a grove. Still, it was quiet, and quiet is rare.

* Rest
-> expedition_glimmerfern_outro

=== expedition_glimmerfern_verse_3 ===
The cut fronds keep glowing in the packs for three nights, then fade to green.

* Pack them well
-> expedition_glimmerfern_outro

=== expedition_glimmerfern_outro ===
The benches will be here at the next full Moon.

* Return
-> DONE

=== expedition_lanternbacks ===
# title: Lanterns in the Grass
# cast: expedition
# description: A Pure Light herd grazing at dusk.
# conditions: event_completed:expedition_lanternbacks < 1
# event_type: Environmental
# priority: 55
# locked: true

At dusk the grass lights up. A herd of grazers moves through it slowly, and their backs glow like lanterns carried at walking pace.

They are not afraid. They have never needed to be.

* Watch them
-> expedition_lanternbacks_chorus_1

=== expedition_lanternbacks_chorus_1 ===
Pure Light beings: part of the land's song, and not quite animals.

* Idealism. Catalogue them.&D Follow the herd and write down everything.&C pillar:aureus;strength:14;success:expedition_lanternbacks_verse_1;failure:expedition_lanternbacks_verse_2;success:consequences:resource:Research +40;fragment:protagonist Lucidity +2;failure:consequences:resource:Research +10 -> expedition_lanternbacks_verse_1

* Realism. Gather their shed wool.&D It keeps the light. The capital will pay for it.&C success:expedition_lanternbacks_verse_3;failure:expedition_lanternbacks_verse_3;consequences:resource:Lumenwool +15 -> expedition_lanternbacks_verse_3

* Pragmatism. Try to lead a few home.&D A herd near the capital would light the fields.&C pillar:regalia;strength:18;success:expedition_lanternbacks_verse_4;failure:expedition_lanternbacks_verse_2;success:consequences:resource:Aetherlight +40;score:domestication +1;failure:consequences:stat:morale -3 -> expedition_lanternbacks_verse_4

=== expedition_lanternbacks_verse_1 ===
They graze in a spiral, never crossing their own path, and the grass they leave glows faintly until dawn.

* Keep the notes
-> expedition_lanternbacks_outro

=== expedition_lanternbacks_verse_2 ===
The herd drifts away into the dark, unhurried, and the grass goes dim behind it.

* Let them go
-> expedition_lanternbacks_outro

=== expedition_lanternbacks_verse_3 ===
The wool is soft and warm, and it shines faintly in the dark of the packs.

* Pack it
-> expedition_lanternbacks_outro

=== expedition_lanternbacks_verse_4 ===
Two young ones follow the expedition for a day before turning back. They leave light behind them, and it lingers.

* Watch them go
-> expedition_lanternbacks_outro

=== expedition_lanternbacks_outro ===
Some nights after, the expedition swears it can still see lanterns on the horizon.

* Return
-> DONE

=== expedition_spire ===
# title: The Whispering Spire
# cast: expedition
# description: Red crystal that whispers, and a land that has lost its tune.
# conditions: event_completed:expedition_spire < 1
# event_type: Mystical
# priority: 55
# locked: true

The spire is velvet-red crystal, taller than any tower, and it whispers when the wind turns. It is the most beautiful thing any of them has seen in years.

Around it, the land is wrong. The grass leans the wrong way, the air tastes of metal, and the party quarrels over nothing.

* Go closer
-> expedition_spire_chorus_1

=== expedition_spire_chorus_1 ===
Gleaming, velvet red crystal with quiet whispers. It resonates with Aetherlight.

* Idealism. Listen to the whispers.&D Learn what the spire is saying, whatever it costs.&C pillar:chorus;strength:18;success:expedition_spire_verse_1;failure:expedition_spire_verse_2;success:consequences:resource:Research +60;fragment:protagonist Lucidity +3;failure:consequences:stat:morale -4 -> expedition_spire_verse_1

* Realism. Chip crystal from its base.&D Emberwhisper for the capital. The spire has plenty.&C success:expedition_spire_verse_3;failure:expedition_spire_verse_3;consequences:resource:Emberwhisper +20 -> expedition_spire_verse_3

* Pragmatism. Leave before it gets into your heads.&D Mark it on the map and keep walking.&C success:expedition_spire_verse_4;failure:expedition_spire_verse_4;consequences:stat:morale +2 -> expedition_spire_verse_4

=== expedition_spire_verse_1 ===
The whispers are a melody, half-remembered and out of tune with everything around it. That is what unsettles the land: it is singing a different song.

* Write it down
-> expedition_spire_outro

=== expedition_spire_verse_2 ===
The whispers get under their skin. For two days after, no one in the party can hold a tune.

* Walk it off
-> expedition_spire_outro

=== expedition_spire_verse_3 ===
The crystal comes away in warm, heavy shards. The whispering follows them for a mile.

* Wrap it well
-> expedition_spire_outro

=== expedition_spire_verse_4 ===
They mark it on the map and walk until the whispering is behind them. The quarrels stop.

* Keep walking
-> expedition_spire_outro

=== expedition_spire_outro ===
From the next ridge the spire still glows red on the skyline.

* Return
-> DONE
