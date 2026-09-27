// The Age of Desolation (Ages 0): its Acts of Fate and its Age Crisis, The Inescapable Hunger.
// Offered by AgeProgression at their moments (every story is locked until then, and tells once).
// In-world quotations are the vault's (Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md), with
// em dashes written as hyphens for the game font. Scores read by the Age asset:
//   famine_preparation  lowers the famine's severity (diverse crops, stores, restraint)
//   monocrop            raises it (monocrop drift)
//   ash_ledger          desperate relief: lowers it now, burns the relics of the past

-> DONE

// ===== ACT OF FATE: ACT II BEGINS =====

=== desolation_golden_orchard ===
# title: The Golden Orchard
# cast: area:welfare
# description: For a handful of years, we believed the world had forgiven us.
# conditions: event_completed:desolation_golden_orchard < 1
# event_type: Environmental
# priority: 100
# locked: true

"For a handful of years, we believed the world had forgiven us. The peaches were sweet. The children grew tall. The ruins were just scenery. We did not yet understand that comfort can be a symptom."
- Elder Maris of the Seed-Keepers, as recorded in the Age of Renewal

Settlements encircle themselves with orchard belts - rings of trees that double as food security and soft perimeter. Caravans reorient trade around pits and saplings instead of multi-crop seeds.

* Walk the orchard belts
-> desolation_golden_orchard_chorus_1

=== desolation_golden_orchard_chorus_1 ===
In the early Age of Desolation, Auric peaches feel like the last mercy of a distant Auric Aria. Villages rip out bitter roots and stubborn grains to plant more trees. What will your people plant?

* Idealism. Plant the Auric peach everywhere.&D Children born under the first great harvests are named Auric-Born.&C success:desolation_golden_orchard_verse_1;failure:desolation_golden_orchard_verse_1;consequences:production_percent:Food +25;score:monocrop +2 -> desolation_golden_orchard_verse_1

* Realism. Keep the bitter roots and earth-beans between the trees.&D The dull, deep-rooted crops no one wants, kept against every neighbour's advice.&C pillar:regalia;strength:12;success:desolation_golden_orchard_verse_2;failure:desolation_golden_orchard_verse_3;success:consequences:score:famine_preparation +2;failure:consequences:score:famine_preparation +1;production_percent:Food -10;duration:sevenths:21 -> desolation_golden_orchard_verse_2

* Pragmatism. Half orchard, half old field.&D Plant the miracle, but do not burn the old seed stores yet.&C success:desolation_golden_orchard_verse_4;failure:desolation_golden_orchard_verse_4;consequences:production_percent:Food +10;score:monocrop +1;score:famine_preparation +1 -> desolation_golden_orchard_verse_4

=== desolation_golden_orchard_verse_1 ===
Labor calendars restructure around the peach cycle: planting, tending, harvest, preservation. Full cellars of dried peaches become the baseline of security; anything else is luxury.

Status is counted in trees.

* Count the trees
-> desolation_golden_orchard_outro

=== desolation_golden_orchard_verse_2 ===
The earth-beans stay in the ground between the rows, and the bitter roots in the cellars beside the peaches. Your neighbours laugh at the dull harvest.

* Let them laugh
-> desolation_golden_orchard_outro

=== desolation_golden_orchard_verse_3 ===
The old crops are kept, but grudgingly: fields are torn between trees and roots, and for a season neither yields as it should.

* Hold to the old seed
-> desolation_golden_orchard_outro

=== desolation_golden_orchard_verse_4 ===
Orchard belts rise around the walls, and the old fields stay behind them, half-tended.

* Tend both
-> desolation_golden_orchard_outro

=== desolation_golden_orchard_outro ===
Even in the "good years," warning signs appear: golden peaches grow rarer, confined to a shrinking number of groves. Most harvests trend pink.

But no one tracks these things as data. Survivors see only that there is still fruit, and in a post-Cataclysm world, that feels like enough.

* Return to the orchards
-> DONE

// ===== THE AGE CRISIS BEGINS (never announced): HALFWAY THROUGH THE AGE =====

=== desolation_brown_peach ===
# title: A Brown Auric Peach?
# cast: area:industry
# description: Only a low quality harvest.
# conditions: event_completed:desolation_brown_peach < 1
# event_type: Environmental
# priority: 100
# locked: true

A basket comes in from the far orchard: withered, blotched skin; mealy, bitter flesh, sometimes hollow with dry rot.

It is only a low quality harvest.

* Look closer
-> desolation_brown_peach_chorus_1

=== desolation_brown_peach_chorus_1 ===
One brown basket in a season of pink ones. The soil is damaged. Even if trees still stand, hunger has effectively begun - but no one says so aloud.

* Idealism. Ration now, while the cellars are full.&D Every family sets aside a share of the dried peaches, before it is needed.&C pillar:waltz;strength:12;success:desolation_brown_peach_verse_1;failure:desolation_brown_peach_verse_2;success:consequences:score:famine_preparation +2;stat:morale -5;failure:consequences:score:famine_preparation +1;stat:morale -10 -> desolation_brown_peach_verse_1

* Realism. It is one bad basket. Plant more trees.&D Practically, the mainstream reaction is almost always the same: plant more trees.&C success:desolation_brown_peach_verse_3;failure:desolation_brown_peach_verse_3;consequences:score:monocrop +1;production_percent:Food +10 -> desolation_brown_peach_verse_3

* Pragmatism. Dig into the soil and see what the roots see.&D Soil gradually compacts and loses spring underfoot; ash and dust dominate.&C requirements:cost:resource:Research 20;success:desolation_brown_peach_verse_4;failure:desolation_brown_peach_verse_4;consequences:score:famine_preparation +1;technology:Agricultural Renewal enlightened -> desolation_brown_peach_verse_4

=== desolation_brown_peach_verse_1 ===
The shares are set aside, and the cellars hold a little more than the season needs.

* Seal the cellars
-> desolation_brown_peach_outro

=== desolation_brown_peach_verse_2 ===
The shares are set aside, with bitterness. There is still fruit; why go hungry now?

* Seal the cellars anyway
-> desolation_brown_peach_outro

=== desolation_brown_peach_verse_3 ===
More pits go into the ground. There is still fruit, and in a post-Cataclysm world, that feels like enough.

* Plant
-> desolation_brown_peach_outro

=== desolation_brown_peach_verse_4 ===
The soil comes up in dry grey clods. It is mineral-rich but lifeless, beautiful and sterile at once.

* Take notes
-> desolation_brown_peach_outro

=== desolation_brown_peach_outro ===
The ruin-song subtly changes pitch as collapsed infrastructure decays further and leylines shift.

* Return
-> DONE

// ===== ACT OF FATE: ACT III BEGINS =====

=== desolation_peach_sickness ===
# title: Peach Sickness
# cast: area:welfare
# description: Full bellies, hollow bodies.
# conditions: event_completed:desolation_peach_sickness < 1
# event_type: Crisis
# priority: 100
# locked: true

"He ate until his belly ached, and still he faded. When his teeth fell out all at once, he laughed through the blood and asked for another peach. There was nothing else to give him."
- Fragment from an anonymous orchard-village tablet

* Visit the sick
-> desolation_peach_sickness_chorus_1

=== desolation_peach_sickness_chorus_1 ===
Bleeding gums and tooth loss. Slow or absent healing. "Ashbone" fractures. The Hollow Fullness: sufferers feel stuffed and heavy, not hungry, even as their muscles waste and they can barely stand. There are still peaches. People are eating.

* Idealism. It is a test from the Auric Aria. Give thanks, and share.&D As a test from the Auric Aria: can mortals endure sweetness without gratitude?&C pillar:chorus;strength:14;success:desolation_peach_sickness_verse_1;failure:desolation_peach_sickness_verse_2;success:consequences:stat:morale +10;score:famine_preparation +1;failure:consequences:stat:morale -5 -> desolation_peach_sickness_verse_1

* Realism. It is an overfull wasting. Feed the sick anything but peaches.&D As an "overfull wasting" - evidence that too much of even a good thing becomes poison.&C pillar:regalia;strength:15;requirements:cost:resource:Food 15;success:desolation_peach_sickness_verse_3;failure:desolation_peach_sickness_verse_2;success:consequences:score:famine_preparation +2;failure:consequences:score:famine_preparation +1 -> desolation_peach_sickness_verse_3

* Pragmatism. Use more relic-dust on the orchards.&D Plant more trees. Use more relic-dust. Eat more of what is already failing them.&C success:desolation_peach_sickness_verse_4;failure:desolation_peach_sickness_verse_4;consequences:score:monocrop +1;score:ash_ledger +1;production_percent:Food +15;duration:sevenths:21 -> desolation_peach_sickness_verse_4

=== desolation_peach_sickness_verse_1 ===
Shrines rise beneath boughs that generate their own twilight, and the sick are carried beneath them.

* Pray with them
-> desolation_peach_sickness_outro

=== desolation_peach_sickness_verse_2 ===
They are dying of malnutrition inside a monocrop plenty.

* Bury the dead
-> desolation_peach_sickness_outro

=== desolation_peach_sickness_verse_3 ===
Broth of bitter roots, earth-beans, greens from the ruins' edges. Some of the sick keep their teeth.

* Keep them fed
-> desolation_peach_sickness_outro

=== desolation_peach_sickness_verse_4 ===
The system doubles down on the very pattern that is killing it.

* Spread the dust
-> desolation_peach_sickness_outro

=== desolation_peach_sickness_outro ===
Bodies become energetically plump but structurally hollow.

* Return
-> DONE

// ===== THE CRISIS IS NAMED: THE FIRST REGIONAL FAILURE =====

=== desolation_regional_failure ===
# title: The Inescapable Hunger
# cast: area:governance
# description: The world remembers excess; the survivors inherit the bill.
# conditions: event_completed:desolation_regional_failure < 1
# event_type: Crisis
# priority: 100
# locked: true

One major orchard region collapses into predominantly brown fruit, or flower-drop with no fruit set. Once-lush Auric belts blossom but do not fruit - trees exhaust themselves in flower then shed everything.

Refugee flows. Price spikes. Banditry and cannibalized trade routes.

* Face it
-> desolation_regional_failure_chorus_1

=== desolation_regional_failure_chorus_1 ===
Hungry groups raid lightly defended stores and caravans. Choose between defending, negotiating, or sacrificing.

* Idealism. Open the gates to the refugees and share the stores.&D Every mouth is a hand for the fields that will come.&C pillar:waltz;strength:16;success:desolation_regional_failure_verse_1;failure:desolation_regional_failure_verse_2;consequences:vagrants:+12;success:consequences:score:famine_preparation +1;stat:morale +5;failure:consequences:resource:Food -20;stat:morale -5 -> desolation_regional_failure_verse_1

* Realism. Defend the stores.&D The walls are for the people inside them.&C pillar:regalia;strength:14;success:desolation_regional_failure_verse_3;failure:desolation_regional_failure_verse_4;success:consequences:stat:morale -5;failure:consequences:resource:Food -30;deaths:+6 -> desolation_regional_failure_verse_3

* Pragmatism. Negotiate: food for labour in the old fields.&D Those who eat, dig.&C requirements:cost:resource:Food 10;success:desolation_regional_failure_verse_5;failure:desolation_regional_failure_verse_5;consequences:vagrants:+6;score:famine_preparation +1 -> desolation_regional_failure_verse_5

=== desolation_regional_failure_verse_1 ===
The refugees come in with nothing, and are given a share and a spade.

* Put them to work
-> desolation_regional_failure_outro

=== desolation_regional_failure_verse_2 ===
The stores empty faster than anyone counted.

* Tighten the rations
-> desolation_regional_failure_outro

=== desolation_regional_failure_verse_3 ===
The gates hold. Outside them, the caravans stop coming.

* Keep the watch
-> desolation_regional_failure_outro

=== desolation_regional_failure_verse_4 ===
The raiders are driven off, but not before the fire reaches the granary.

* Count the losses
-> desolation_regional_failure_outro

=== desolation_regional_failure_verse_5 ===
The newcomers turn the old fields over in exchange for their bread.

* Share the bread
-> desolation_regional_failure_outro

=== desolation_regional_failure_outro ===
This is when hunger becomes inescapable.

* Return
-> DONE

// ===== THE ASH-BREAD WINTER =====

=== desolation_ash_bread ===
# title: The Ash-Bread Winter
# cast: area:welfare
# description: We ground our miracles to dust and called it wisdom.
# conditions: event_completed:desolation_ash_bread < 1
# event_type: Crisis
# priority: 100
# locked: true

"We ground our miracles to dust and called it wisdom."
- From The Lament of the Ash-Priests, fragmentary hymn

Felling the goldenwood groves, burning them in deep pits, and spreading the golden ash on failing orchards produces astonishing short-term results.

* Go to the pits
-> desolation_ash_bread_chorus_1

=== desolation_ash_bread_chorus_1 ===
The Old World Relics could be ground into the fields, or baked into ash-bread and gruel. Every grove burned is another irreplaceable magical biome destroyed for one last good year.

* Idealism. Refuse. The relics stay whole.&D Some would rather starve than grind certain relics.&C pillar:chorus;strength:18;success:desolation_ash_bread_verse_1;failure:desolation_ash_bread_verse_2;success:consequences:score:famine_preparation +1;stat:morale +5;failure:consequences:stat:morale -10 -> desolation_ash_bread_verse_1

* Realism. Grind them. Bake the ash-bread.&D Low-nutrition emergency rations that stave off Starvation.&C success:desolation_ash_bread_verse_3;failure:desolation_ash_bread_verse_3;consequences:score:ash_ledger +3;resource:Ash-Bread +150;resource:Aetherlight -40 -> desolation_ash_bread_verse_3

* Pragmatism. A pinch in a furrow; a dusting before a known deficiency.&D Golden ash as a precise tool, not an offering.&C pillar:aureus;strength:16;success:desolation_ash_bread_verse_4;failure:desolation_ash_bread_verse_3;success:consequences:score:famine_preparation +2;score:ash_ledger +1;failure:consequences:score:ash_ledger +2;resource:Aetherlight -20 -> desolation_ash_bread_verse_4

=== desolation_ash_bread_verse_1 ===
The relics stay in their niches. The cellars stay thin.

* Endure
-> desolation_ash_bread_outro

=== desolation_ash_bread_verse_2 ===
The relics stay whole, and the people who guarded them grow thinner through the winter.

* Endure
-> desolation_ash_bread_outro

=== desolation_ash_bread_verse_3 ===
Visually, thickly ashed fields shimmer like sanctified ground. To the untrained eye, they look blessed. In truth, they are being pushed toward sterility.

* Eat the bread
-> desolation_ash_bread_outro

=== desolation_ash_bread_verse_4 ===
Light application stabilizes the soil and moderately boosts yield; sustainable if used sparingly and rotated.

* Measure it out
-> desolation_ash_bread_outro

=== desolation_ash_bread_outro ===
This is the Ash-Bread Winter: the period in which people survive by literally eating ground history.

* Return
-> DONE

// ===== THE CHOOSING OF SEEDS =====

=== desolation_choosing_of_seeds ===
# title: The Choosing of Seeds
# cast: area:lore
# description: The land is a chord, not a note.
# conditions: event_completed:desolation_choosing_of_seeds < 1
# event_type: Crisis
# priority: 100
# locked: true

Abandoned fields, left uncultivated because everyone fled or died, begin to show small signs of recovery: wild grasses, hardy weeds, and volunteer legumes appear.

Decide which fledgling communities receive the last stable food caches and best land.

* Decide
-> desolation_choosing_of_seeds_chorus_1

=== desolation_choosing_of_seeds_chorus_1 ===
Which proto-Conclave will you keep alive?

* Idealism. The soil-focused seed-keepers.&D "The land is a chord, not a note. No stone alone can save a starving field."&C success:desolation_choosing_of_seeds_verse_1;failure:desolation_choosing_of_seeds_verse_1;consequences:score:famine_preparation +2;score:seed_keepers +1 -> desolation_choosing_of_seeds_verse_1

* Realism. The trade-obsessed caravaneers.&D Future Trading Enclaves.&C success:desolation_choosing_of_seeds_verse_2;failure:desolation_choosing_of_seeds_verse_2;consequences:score:famine_preparation +1;score:caravaneers +1;resource:Elderwood +30 -> desolation_choosing_of_seeds_verse_2

* Pragmatism. The Ash-Priest cults.&D Those who sacralize relic-burning.&C success:desolation_choosing_of_seeds_verse_3;failure:desolation_choosing_of_seeds_verse_3;consequences:score:ash_ledger +2;score:ash_priests +1;stat:morale +5 -> desolation_choosing_of_seeds_verse_3

=== desolation_choosing_of_seeds_verse_1 ===
Auric peaches, then earth-beans, then hardy grains and greens, then fallow. Letting land lie unused for a year becomes a kind of ritual silence.

* Let the land rest
-> desolation_choosing_of_seeds_outro

=== desolation_choosing_of_seeds_verse_2 ===
The caravaneers take the last good carts and the last good routes, and promise to come back with seed.

* Watch them go
-> desolation_choosing_of_seeds_outro

=== desolation_choosing_of_seeds_verse_3 ===
The Ash-Priests keep the pits burning, and the survivors who ate the bread remember guilt and fear of plenty.

* Tend the fires
-> desolation_choosing_of_seeds_outro

=== desolation_choosing_of_seeds_outro ===
People begin - very slowly - to experiment instead of repeat.

* Return
-> DONE
