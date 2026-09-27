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
