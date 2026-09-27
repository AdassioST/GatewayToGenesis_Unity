// Original local placeholder events grounded in the vault. See Docs/Planning/SETTLEMENT_STORIES.md.
// Once per world, deliberately bounded fragment sources; restoration does not re-run choices.
-> DONE

=== sm_caravan ===
# title: Wheels at the Outskirts
# description: How should the caravan be received?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_caravan < 1; technology:Reconstruction; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Leader
# cast: area:diplomacy, co:1

The caravan arrives with one sound wheel and three that complain. Its leader sets a sack of grain on the ground before asking to enter.

'You may count it outside,' she says. 'We have been strangers before.' The watch is relieved by the offer, then ashamed of how relieved it is.

* Hear the question
-> sm_caravan_chorus

=== sm_caravan_chorus ===
How should the caravan be received?

* Idealism. Trade beneath the watchtower&D Offer timber for food, with both measures visible.&C requirements:cost:resource:Elderwood 6 -> sm_caravan_verse_a
* Realism. Offer water and a safe stopping place&D Welcome the travellers without promising supplies you cannot spare. -> sm_caravan_verse_b

=== sm_caravan_verse_a ===
The grain is counted twice, once by each side. A carpenter examines the wheels while the drivers eat. By dusk the gate has acquired a place where strangers can argue over a fair price without being treated as enemies.

* Continue&C consequences: resource:Food +15; fragment:cast Meaning +2
-> sm_caravan_outro

=== sm_caravan_verse_b ===
The drivers tether their animals beyond the gate. One leaves a route sketch with the watch. It marks a broken crossing and a settlement that still answers a hail. The caravan departs owing no invented debt.

* Continue&C consequences: resource:Research +5; fragment:cast Lucidity +2
-> sm_caravan_outro

=== sm_caravan_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_extra_bowl ===
# title: The Extra Bowl
# description: What should the server count?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_extra_bowl < 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:governance, co:1

At the Field Kitchen, the server finds one more bowl than names on the list. A child admits she put it there for a neighbour too proud to stand in line.

The neighbour is waiting outside, pretending to mend a basket that has already been mended.

* Hear the question
-> sm_extra_bowl_chorus

=== sm_extra_bowl_chorus ===
What should the server count?

* Idealism. Count the person&D Supply the meal openly, without making gratitude its price.&C requirements:cost:resource:Food 3 -> sm_extra_bowl_verse_a
* Realism. Change the way the line is called&D Invite households to collect for absent neighbours. -> sm_extra_bowl_verse_b

=== sm_extra_bowl_verse_a ===
A bowl is carried out with a spoon. The server asks for a name only when it is time to add tomorrow's portion. The basket is put down.

* Continue&C consequences: fragment:cast Meaning +2; stat:morale +1
-> sm_extra_bowl_outro

=== sm_extra_bowl_verse_b ===
Tomorrow's list has room for who will carry a portion, as well as who will eat it. No food appears by decree, but fewer people must announce their hunger in front of a crowd.

* Continue&C consequences: fragment:cast Vision +2
-> sm_extra_bowl_outro

=== sm_extra_bowl_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_seed_names ===
# title: A Name for Every Seed
# description: How should the seeds be recorded?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_seed_names < 1; map_feature:resting-field >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:economy, co:1

Two growers have brought the same handful of seeds to the council. One calls them winter grain. The other says that name killed a planting in her old village: those seeds belonged to a different soil.

They are willing to compare notes, provided the clerk does not decide that one spelling must be wrong.

* Hear the question
-> sm_seed_names_chorus

=== sm_seed_names_chorus ===
How should the seeds be recorded?

* Idealism. Keep both names and their soils&D Pay for small sample plots rather than settle the question by rank.&C requirements:cost:resource:Food 4 -> sm_seed_names_verse_a
* Realism. Ask the growers to teach together&D Use testimony while the settlement cannot spare a trial bed. -> sm_seed_names_verse_b

=== sm_seed_names_verse_a ===
The labels grow longer. Each names the ground, the planting time and the person who remembers it. The seed ledger becomes less tidy and more useful.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +6
-> sm_seed_names_outro

=== sm_seed_names_verse_b ===
They disagree in front of the apprentices. By the end, every apprentice knows one question to ask before sowing. The clerk records the disagreement as part of the lesson.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_seed_names_outro

=== sm_seed_names_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_wet_timber ===
# title: The Timber That Would Not Burn
# description: What gets repaired first?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_wet_timber < 1; resource:Elderwood >= 4; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Resistor
# cast: area:logistics, co:1

The Timber Camp's latest bundle hisses in the cooking fire. A worker insists it was dry when loaded. Another points to the rain marks on the cart cover.

The cook places an uncooked pot between them. Finding someone to blame will not bring it to a boil.

* Hear the question
-> sm_wet_timber_chorus

=== sm_wet_timber_chorus ===
What gets repaired first?

* Idealism. Repair the cover&D Use sound timber for a rack and keep the next load out of the wet.&C requirements:cost:resource:Elderwood 4 -> sm_wet_timber_verse_a
* Realism. Change the loading order&D Separate wet and dry bundles and make the handover visible. -> sm_wet_timber_verse_b

=== sm_wet_timber_verse_a ===
The rack takes the best of the bundle. The cook uses the last dry scraps to finish the pot. Neither worker calls the exchange efficient; both help lift the cover.

* Continue&C consequences: fragment:cast Vision +2; stat:morale +1
-> sm_wet_timber_outro

=== sm_wet_timber_verse_b ===
The next cart carries two stacks and a tally tied to each. The workers agree to sign for what they can see, rather than for what the other claims to have sent.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_wet_timber_outro

=== sm_wet_timber_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_stone ===
# title: The Crack in the Duskstone
# description: What lesson should the workshop take?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_stone < 1; resource:Duskstone >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:logistics, co:1

A stoneworker sets a split piece of Duskstone on the council table. The apprentice who cut it has already offered to lose a meal.

The fracture runs through a seam that was hidden before the cut. The master turns the pieces over, considering how to answer with the whole workshop watching.

* Hear the question
-> sm_stone_chorus

=== sm_stone_chorus ===
What lesson should the workshop take?

* Idealism. Let the broken piece teach&D Keep it as a sample of a hidden flaw. -> sm_stone_verse_a
* Realism. Ask the apprentice to find another use&D Recover what can be used without disguising the mistake. -> sm_stone_verse_b

=== sm_stone_verse_a ===
The apprentice marks the seam with chalk. By evening three workers have brought stones with the same faint line. The workshop has spent a piece of material and bought a better question.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +4
-> sm_stone_outro

=== sm_stone_verse_b ===
The pieces become small weights for holding pattern cloth. The apprentice writes the original cut beside the new use. Nothing in the account requires hunger as tuition.

* Continue&C consequences: fragment:cast Vision +2
-> sm_stone_outro

=== sm_stone_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_windmill ===
# title: A Tooth in the Windmill
# description: When should the work stop?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_windmill < 1; building:Ancient Windmill >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Resistor
# cast: area:economy, co:1

The Ancient Windmill catches, shudders and turns again. A child has learned to clap at the pause. The miller says one tooth is wearing through; the child says the mill has a limp.

The repair can be made now, or the miller can nurse it through another short run.

* Hear the question
-> sm_windmill_chorus

=== sm_windmill_chorus ===
When should the work stop?

* Idealism. Replace the tooth now&D Spend timber on a repair before the break.&C requirements:cost:resource:Elderwood 5 -> sm_windmill_verse_a
* Realism. Measure the wear between runs&D Give the miller a documented stopping point. -> sm_windmill_verse_b

=== sm_windmill_verse_a ===
The new tooth is rough but sound. The child waits for the old pause and misses the clap. For a while the mill sounds strangely incomplete: nothing is going wrong.

* Continue&C consequences: fragment:cast Vision +2; resource:Food +4
-> sm_windmill_outro

=== sm_windmill_verse_b ===
The miller marks the tooth and shortens the runs. The child is given a safer job: carrying the tally. The machine's limp has become a warning someone knows how to read.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_windmill_outro

=== sm_windmill_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_watchlamp ===
# title: The Last Watch Lamp
# description: What does a working tool owe its keeper?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_watchlamp < 1; building:Hollow Watchpost >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Leader
# cast: area:defense, co:1

The Hollow Watchpost has been signalling with a lamp whose handle burns the watcher's palm. Its keeper asks for a replacement. A councillor asks whether the old one still works.

The keeper opens a blistered hand on the table and waits.

* Hear the question
-> sm_watchlamp_chorus

=== sm_watchlamp_chorus ===
What does a working tool owe its keeper?

* Idealism. Repair it properly&D Supply material before demanding another night of service.&C requirements:cost:resource:Elderwood 3 -> sm_watchlamp_verse_a
* Realism. Share the watch while repairs wait&D Rearrange the duty so one hand does not carry the whole night. -> sm_watchlamp_verse_b

=== sm_watchlamp_verse_a ===
The lamp returns with a wrapped handle and a steady hook. The keeper tests it after sunset. From the gate, the signal looks exactly as it always did. That is the point.

* Continue&C consequences: fragment:cast Meaning +2; stat:morale +1
-> sm_watchlamp_outro

=== sm_watchlamp_verse_b ===
The next report has two signatures. The lamp is lowered between signals. It is a temporary arrangement, and the keeper makes the clerk write that down.

* Continue&C consequences: fragment:cast Acceptance +2
-> sm_watchlamp_outro

=== sm_watchlamp_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_quarrel ===
# title: Whose Map Is It?
# description: What should the council send back?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_quarrel < 1; expedition_party: >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:justice, co:1

A courier brings two versions of an expedition map. A companion has marked a water stop that the Director left out. The Director says it was uncertain. The companion says uncertainty did not stop everyone drinking there.

The margin is now full of corrections aimed at people rather than places.

* Hear the question
-> sm_quarrel_chorus

=== sm_quarrel_chorus ===
What should the council send back?

* Idealism. Ask for a map of disagreements&D Keep observations and confidence separate from rank. -> sm_quarrel_verse_a
* Realism. Ask each to carry the other's account&D Have them read one another's reasons before redrawing. -> sm_quarrel_verse_b

=== sm_quarrel_verse_a ===
The new copy uses a dotted mark for the disputed water. Both names sit beside it. The companions are asked to check the source on their next passage, not to prove which person deserves to have seen it.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_quarrel_outro

=== sm_quarrel_verse_b ===
The Director reads the account of an empty waterskin. The companion reads the account of a vanished spring. Neither withdraws the observation. Both remove a sentence about the other's character.

* Continue&C consequences: fragment:cast Catharsis +2
-> sm_quarrel_outro

=== sm_quarrel_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_sock ===
# title: The Last Dry Sock
# description: What should be sent with the reply?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_sock < 1; expedition_worn: >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:logistics, co:1

The expedition's request for replacement cloth includes a note: one dry sock has been circulating between three companions. Nobody agrees who first lent it.

A provisioner suggests returning it to its owner. The courier asks whether the council could first arrange for there to be more than one.

* Hear the question
-> sm_sock_chorus

=== sm_sock_chorus ===
What should be sent with the reply?

* Idealism. A small care parcel&D Trade food for spare cloth and send it with the next provisioner.&C requirements:cost:resource:Food 4 -> sm_sock_verse_a
* Realism. A less foolish packing list&D Document shared necessities for the next departure. -> sm_sock_verse_b

=== sm_sock_verse_a ===
The parcel contains no matching pairs. Someone has stitched a tiny mark into each heel so that drying lines need no argument. The courier includes the old sock, washed, as evidence that the request was not exaggerated.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_sock_outro

=== sm_sock_verse_b ===
The provisioner adds dry cloth beneath rations and beside the return route. The courier circles it twice. This does not shorten today's road, but the next party will pack with someone else's blisters in mind.

* Continue&C consequences: fragment:cast Vision +2
-> sm_sock_outro

=== sm_sock_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_path ===
# title: The Shorter Way Home
# description: How should a route's length be judged?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_path < 1; journeys: >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:logistics, co:1

A returning traveller offers a shortcut between two familiar hills. It is shorter on the page because the page does not have to climb it.

The expedition's oldest companion lays a worn boot beside the map. The toe has parted from the sole. 'Include this distance too,' she says.

* Hear the question
-> sm_path_chorus

=== sm_path_chorus ===
How should a route's length be judged?

* Idealism. Compare the journeys, not just the lines&D Add rest, provisions and rough ground to the account. -> sm_path_verse_a
* Realism. Let the companion teach the next party&D Pay for an evening of practical instruction.&C requirements:cost:resource:Food 3 -> sm_path_verse_b

=== sm_path_verse_a ===
The shortcut remains on the map. Beside it are the time, the water stops and a warning about the slope. It will suit some travellers. The council stops calling it the better road.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +4
-> sm_path_outro

=== sm_path_verse_b ===
The lesson begins with the boot. By the end the new travellers have each moved something in their packs. The companion leaves with supper and a promise that her route notes will bear her name.

* Continue&C consequences: fragment:cast Rebirth +2
-> sm_path_outro

=== sm_path_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_resonator ===
# title: Cloth for a Broken Resonator
# description: What should be preserved?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_resonator < 1; map_feature:fallen-resonator >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:culture, co:1

Wind passing the Fallen Resonator catches one loose edge and produces a shrill, uneven note. A traveller has stuffed cloth into the gap so the party can sleep.

The surveyor wants to remove it and hear the structure as it stands. The exhausted traveller asks whether every discovery must be made tonight.

* Hear the question
-> sm_resonator_chorus

=== sm_resonator_chorus ===
What should be preserved?

* Idealism. The sound, with a proper rest first&D Provision a daylight observation and leave the cloth until morning.&C requirements:cost:resource:Food 3 -> sm_resonator_verse_a
* Realism. The travellers' right to quiet&D Record the alteration rather than pretending it was never there. -> sm_resonator_verse_b

=== sm_resonator_verse_a ===
In daylight the gap can be measured. The surveyor records the pitch without calling it a message. The cloth is returned to its owner. Everyone remembers the ruin more accurately after sleeping.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +5
-> sm_resonator_outro

=== sm_resonator_verse_b ===
The report includes a sketch of the cloth and why it was placed. The ruin has survived greater interventions. The traveller folds the scrap carefully for the next uncomfortable camp.

* Continue&C consequences: fragment:cast Acceptance +2
-> sm_resonator_outro

=== sm_resonator_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_tools ===
# title: The Borrowed Adze
# description: How should the debt be settled?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_tools < 1; technology:Reconstruction; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Leader
# cast: area:justice, co:1

An adze lent between two households has returned with a new handle. Its owner says the replacement is inferior. The borrower says the old one broke from age.

The joiner asked to judge them recognizes the new handle as one cut from her own dwindling stock. Three people now have a claim to one useful tool.

* Hear the question
-> sm_tools_chorus

=== sm_tools_chorus ===
How should the debt be settled?

* Idealism. Replace the joiner's stock&D Pay for the repair and make the lending terms clear.&C requirements:cost:resource:Elderwood 4 -> sm_tools_verse_a
* Realism. Agree on shared work in repayment&D Let each describe a contribution they can actually make. -> sm_tools_verse_b

=== sm_tools_verse_a ===
The owner keeps the adze, the borrower keeps permission to ask again, and the joiner receives sound timber. The register records the repair instead of a winner.

* Continue&C consequences: fragment:cast Vision +2
-> sm_tools_outro

=== sm_tools_verse_b ===
The owner supplies the blade, the borrower supplies labour, and the joiner teaches how a handle should fit. They leave with an appointment rather than an apology imposed by the council.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_tools_outro

=== sm_tools_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_window ===
# title: A Window Facing Nobody
# description: What can they change together?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_window < 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:governance, co:1

A repaired shelter in the Residential District has a window facing a wall. The family inside asks whether the wall can be lowered. The neighbour says it keeps the dust out of her sleeping place.

At midday both households gather in the same narrow strip of shade.

* Hear the question
-> sm_window_chorus

=== sm_window_chorus ===
What can they change together?

* Idealism. Build a screened opening&D Use timber to keep shelter while admitting a little light.&C requirements:cost:resource:Elderwood 5 -> sm_window_verse_a
* Realism. Agree on a shared sitting place&D Keep the shelter and make the common space usable. -> sm_window_verse_b

=== sm_window_verse_a ===
The opening is no grand work. It gives one household a patch of daylight and the other a ledge for a cup. At midday they still share the shade, now with something less to resent.

* Continue&C consequences: fragment:cast Vision +2; stat:morale +1
-> sm_window_outro

=== sm_window_verse_b ===
The households clear the shaded strip and set their stools there. It does not become a better window. It becomes somewhere to ask again without beginning with an accusation.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_window_outro

=== sm_window_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_barrel ===
# title: The Barrel at the Back
# description: What should follow an honest loss?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_barrel < 1; map_feature:auric-orchard >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Resistor
# cast: area:economy, co:1

A storekeeper finds a barrel of fruit behind newer deliveries. The top layer is sound. Beneath it the smell changes.

The keeper could copy the old tally and send the barrel to the kitchen. Instead he brings a spoiled peach to the council, wrapped as carefully as a gift.

* Hear the question
-> sm_barrel_chorus

=== sm_barrel_chorus ===
What should follow an honest loss?

* Idealism. Sort what can still be used&D Supply a separate meal while the suspect fruit is examined.&C requirements:cost:resource:Food 4 -> sm_barrel_verse_a
* Realism. Change how stores are rotated&D Put delivery dates where the next keeper can see them. -> sm_barrel_verse_b

=== sm_barrel_verse_a ===
The kitchen receives only the sound portion. The spoiled fruit is recorded as spoiled; nobody is ordered to turn it into proof of thrift. The keeper leaves with a shorter stock list and permission to keep it honest.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_barrel_outro

=== sm_barrel_verse_b ===
Old stock is moved to the front and every barrel receives a visible mark. The keeper signs the correction. The missing fruit remains missing, but the mistake no longer has a hiding place.

* Continue&C consequences: fragment:cast Vision +2
-> sm_barrel_outro

=== sm_barrel_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_burial ===
# title: The Name on the Board
# description: Which name should be kept?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_burial < 1; true_deaths: >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Sacrificial Lamb
# cast: area:culture, co:1

A burial board carries a name that nobody at the gathering recognizes. A neighbour says the dead person used another name here. A traveller says the first was the one used before the roads failed.

The grave warden has enough room to cut both.

* Hear the question
-> sm_burial_chorus

=== sm_burial_chorus ===
Which name should be kept?

* Idealism. Keep both, with their witnesses&D Let neither life erase the other. -> sm_burial_verse_a
* Realism. Ask the household how to remember them&D Record the other name without overruling those who shared the last home. -> sm_burial_verse_b

=== sm_burial_verse_a ===
The warden cuts the second name beneath the first. Two people touch different lines on the same board. The record becomes longer by a few words and less certain of whom it could have left out.

* Continue&C consequences: fragment:cast Acceptance +2
-> sm_burial_outro

=== sm_burial_verse_b ===
The household chooses the name spoken over supper. The traveller's account is kept with the burial record. Nobody is asked to surrender a memory at the edge of the grave.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_burial_outro

=== sm_burial_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_spire ===
# title: A Lesson in the Ruin-Song
# description: What should the lesson become?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_spire < 1; map_feature:silent-spire >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:culture, co:1

Children imitate the Silent Spire by blowing across bottles. An adult tells them to stop making a game of the ruins. One child asks what sound would count as respectful.

The adult has an answer for silence, but not for that question.

* Hear the question
-> sm_spire_chorus

=== sm_spire_chorus ===
What should the lesson become?

* Idealism. Listen before imitating&D Let someone who walked the ruins describe their sound. -> sm_spire_verse_a
* Realism. Ask what the song means at home&D Let memory include the people who must live beside it. -> sm_spire_verse_b

=== sm_spire_verse_a ===
The bottles are put down while the account is told. Then the children try again, this time listening to one another. The imitation is still poor. It is no longer thoughtless.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_spire_outro

=== sm_spire_verse_b ===
The answers are small: a warning of wind, a way to find the path, the noise heard while waiting for someone late. The adult adds one of her own. The children lower the bottles to hear it.

* Continue&C consequences: fragment:cast Catharsis +2
-> sm_spire_outro

=== sm_spire_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_grove ===
# title: The Basket from the Grove
# description: How should the unfamiliar bundle be treated?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_grove < 1; building:Moonlit Grove >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:economy, co:1

A gatherer brings leaves from a moonlit grove, sorted into three bundles. She knows one as food, one as a wrapping, and the third only as something that grew beside them.

An eager clerk writes 'three useful plants.' The gatherer puts a hand over the page.

* Hear the question
-> sm_grove_chorus

=== sm_grove_chorus ===
How should the unfamiliar bundle be treated?

* Idealism. Keep it separate until it is identified&D Pay for careful observation, not an experiment on a hungry neighbour.&C requirements:cost:resource:Food 3 -> sm_grove_verse_a
* Realism. Return it and record the place&D Leave no claim of usefulness that another reader might trust. -> sm_grove_verse_b

=== sm_grove_verse_a ===
The third bundle receives a label reading 'unknown.' The gatherer looks relieved. The known leaves go to their ordinary uses; the unknown ones acquire a description rather than a miracle.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +5
-> sm_grove_outro

=== sm_grove_verse_b ===
The clerk draws the leaves and the ground where they grew. The record says where to look again, and why not to eat them yet. An empty basket can also be a useful result.

* Continue&C consequences: fragment:cast Acceptance +2
-> sm_grove_outro

=== sm_grove_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_letters ===
# title: Letters on a Crate
# description: What belongs in the first translation?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_letters < 1; resource:Research >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Scholar
# cast: area:culture, co:1

A crate from the Old World Remnants bears writing that nobody present can read. A labourer recognizes the little mark beside the words: her grandmother put it on things that must remain dry.

The scholar has a page of possible royal names. The labourer has a memory of sacks lifted off a wet floor.

* Hear the question
-> sm_letters_chorus

=== sm_letters_chorus ===
What belongs in the first translation?

* Idealism. Start with the practical memory&D Treat the labourer's account as evidence. -> sm_letters_verse_a
* Realism. Keep both accounts visibly uncertain&D Separate the two interpretations and name their sources. -> sm_letters_verse_b

=== sm_letters_verse_a ===
The mark is copied beside its remembered use. The royal names remain possibilities on another page. When the next shower comes, somebody moves the crate under cover without waiting for a dynasty to be identified.

* Continue&C consequences: fragment:cast Lucidity +2; resource:Research +5
-> sm_letters_outro

=== sm_letters_verse_b ===
The label has two columns. One says what the scholar suspects. The other says what the labourer remembers. A future reader will know which part kept the crate dry.

* Continue&C consequences: fragment:cast Meaning +2
-> sm_letters_outro

=== sm_letters_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_rain ===
# title: A Roof for the Rain
# description: What can be done before the next shower?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_rain < 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Leader
# cast: area:governance, co:1

The first drops reveal a leak directly above the shared grain measure. A neighbour offers a basin, another offers advice, and a third stands holding the measure as if willingness alone could keep it dry.

From under the eaves, a child points to an empty shelf.

* Hear the question
-> sm_rain_chorus

=== sm_rain_chorus ===
What can be done before the next shower?

* Idealism. Patch the roof&D Buy a repair instead of borrowing everyone's attention.&C requirements:cost:resource:Elderwood 4 -> sm_rain_verse_a
* Realism. Move the stores and mark the leak&D Use the dry space and make the unfinished repair visible. -> sm_rain_verse_b

=== sm_rain_verse_a ===
The patch is checked with a bucket. The child supervises from below, declaring the first drip a failure and the second test a success. The measure is returned to a dry shelf.

* Continue&C consequences: fragment:cast Vision +2
-> sm_rain_outro

=== sm_rain_verse_b ===
The shelf fills. A chalk circle remains beneath the leak, large enough that nobody can mistake moving the grain for fixing the roof. The basin is returned to its owner.

* Continue&C consequences: fragment:cast Lucidity +2
-> sm_rain_outro

=== sm_rain_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== sm_return ===
# title: Supper for the Returned
# description: What should the council hear first?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:sm_return < 1; journeys: >= 1; no_event_in_sevenths:3
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:logistics, co:1

The returning party is asked to report before it has put down its packs. The first answer is a list of landmarks. The second is an argument about who carried the last provision sack.

The cook sets a pot between the map and the clerk. 'Ask them again when they can hold a spoon,' she says.

* Hear the question
-> sm_return_chorus

=== sm_return_chorus ===
What should the council hear first?

* Idealism. Serve supper before the account&D Give the travellers a meal without making it a reward for good news.&C requirements:cost:resource:Food 5 -> sm_return_verse_a
* Realism. Take only the urgent warning&D Let the full account wait until the party has rested. -> sm_return_verse_b

=== sm_return_verse_a ===
After eating, the report gains a missing water stop, an apology and the name of a companion who noticed danger first. The landmarks have not moved. The people describing them have returned a little further.

* Continue&C consequences: fragment:cast Catharsis +2; stat:morale +1
-> sm_return_outro

=== sm_return_verse_b ===
The clerk writes one line about a broken path and leaves the rest of the page blank. The Director signs it without pretending it is the whole journey. Tomorrow there will be time for the names.

* Continue&C consequences: fragment:cast Acceptance +2
-> sm_return_outro

=== sm_return_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE


