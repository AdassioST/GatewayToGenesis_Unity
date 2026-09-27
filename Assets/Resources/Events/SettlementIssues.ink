// Original local placeholder events grounded in the vault. See Docs/Planning/SETTLEMENT_STORIES.md.
// Once per world, deliberately bounded fragment sources; restoration does not re-run choices.
-> DONE

=== si_hunger ===
# title: An Empty Evening Measure
# description: How should the immediate shortage be met?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:si_hunger < 1; resource:Food <= 10; production_rate:Food < 0
# event_type: Social
# priority: 10
# theme: Resistor
# cast: area:economy, co:1
# issue: true
The kitchen's evening measure is nearly empty, and consumption is still outrunning supply. The server asks for a decision before hunger chooses one for the settlement.

A grower can organize a short emergency gleaning. A storekeeper offers instead to divide what remains openly, so that nobody is fed by an invisible exception.

* Hear the question
-> si_hunger_chorus

=== si_hunger_chorus ===
How should the immediate shortage be met?

* Idealism. Organize the emergency gleaning&D A limited effort improves Food production for six Sevenths; it cannot solve the Age Crisis. -> si_hunger_verse_a
* Realism. Make the ration account public&D Confront the shortage honestly without pretending the stores are fuller. -> si_hunger_verse_b

=== si_hunger_verse_a ===
The gleaners leave with baskets and a stopping time. At the kitchen, the server writes the temporary arrangement beside the ration list. Everyone can see when the extra effort will end.

* Continue&C consequences: production_percent:Food +10; duration:sevenths:6; fragment:cast Defiance +3
-> si_hunger_outro

=== si_hunger_verse_b ===
The list is read where everyone can hear it. There are angry questions, including some the council cannot answer. The server keeps a blank space for corrections. Food still has to be found; secrecy will not be another mouth to feed.

* Continue&C consequences: fragment:cast Lucidity +3; stat:morale +1
-> si_hunger_outro

=== si_hunger_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== si_shelter ===
# title: Names Without a Roof
# description: What can be offered tonight?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:si_shelter < 1; vagrants: >= 1
# event_type: Social
# priority: 10
# theme: Leader
# cast: area:governance, co:1
# issue: true
People without shelter have gathered outside the Residential District. A foreman can make a few additional sleeping places with sound timber. A resident proposes sharing existing rooms while longer repairs are arranged.

The families are tired of being discussed as a number. One asks the clerk to read their names before reading the available space.

* Hear the question
-> si_shelter_chorus

=== si_shelter_chorus ===
What can be offered tonight?

* Idealism. Build three sheltered places&D Spend timber to increase housing by three.&C requirements:cost:resource:Elderwood 8 -> si_shelter_verse_a
* Realism. Let residents arrange voluntary hosting&D Make two additional sleeping places by sharing existing space. -> si_shelter_verse_b

=== si_shelter_verse_a ===
The foreman returns with a short list of completed places. It is read beside the longer list of names. Three people can sleep under a roof tonight; those still waiting are not crossed out.

* Continue&C consequences: housing:housing +3; fragment:cast Vision +3
-> si_shelter_outro

=== si_shelter_verse_b ===
Two households volunteer sleeping space. The clerk records who offered it and agrees that any dispute will be heard. The offered beds are modest; the names of those still waiting remain on the board.

* Continue&C consequences: housing:housing +2; fragment:cast Meaning +3
-> si_shelter_outro

=== si_shelter_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== si_fracture ===
# title: A Quarrel After the Mishap
# description: Who gets to finish the account?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:si_fracture < 1; expedition_dispute: >= 1
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:justice, co:1
# issue: true
An expedition with companions has suffered a mishap. Its report spends more words on who should have prevented it than on what actually happened.

The council receives a second note beneath the official one: 'If you answer only the Director, the rest of us will know what that means.'

* Hear the question
-> si_fracture_chorus

=== si_fracture_chorus ===
Who gets to finish the account?

* Idealism. Hear every companion's version&D Make testimony part of the report rather than a contest for blame. -> si_fracture_verse_a
* Realism. Begin with an admission of uncertainty&D Ask the Director to acknowledge what no one could have guaranteed. -> si_fracture_verse_b

=== si_fracture_verse_a ===
The reply asks each witness to separate what they saw, what they feared and what they did. The council will not pretend these are interchangeable. The party's next decision must still be made on the road, but it will not be made with only one account on record.

* Continue&C consequences: fragment:cast Lucidity +3; stat:morale +1
-> si_fracture_outro

=== si_fracture_verse_b ===
The answer does not absolve anyone of care. It removes the demand that someone should have known the unknowable. A companion writes back one sentence: 'Now we can discuss what we should do differently.'

* Continue&C consequences: fragment:cast Catharsis +3
-> si_fracture_outro

=== si_fracture_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== si_exhaustion ===
# title: The Road Has Become Too Long
# description: What does the council ask of them?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:si_exhaustion < 1; expedition_worn: >= 1
# event_type: Social
# priority: 10
# theme: Sacrificial Lamb
# cast: area:logistics, co:1
# issue: true
An expedition's weariness has become a warning. Its request is not for a greater reward. It asks whether returning without the promised discovery will be counted as failure.

The council has praised endurance so often that the party can no longer tell whether it is allowed to stop.

* Hear the question
-> si_exhaustion_chorus

=== si_exhaustion_chorus ===
What does the council ask of them?

* Idealism. Release them from the promised discovery&D Recognize a safe return as worthwhile; the expedition still needs a safe route or camp. -> si_exhaustion_verse_a
* Realism. Ask for a shorter, measured objective&D Replace a boast with a limited task and an explicit stopping point. -> si_exhaustion_verse_b

=== si_exhaustion_verse_a ===
The reply begins, 'You do not owe us an exhausted body to prove that you walked far enough.' It asks for a safe place to rest and an honest account of the remaining supplies. Permission has been given; a route must still be chosen.

* Continue&C consequences: fragment:cast Acceptance +3; stat:morale +1
-> si_exhaustion_outro

=== si_exhaustion_verse_b ===
The old objective is crossed out on the council's copy. Beneath it is room for the party's own estimate. The reply promises that a warning about their strength will be treated as information, not disobedience.

* Continue&C consequences: fragment:cast Lucidity +3
-> si_exhaustion_outro

=== si_exhaustion_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE

=== si_mourning ===
# title: The Work of Naming the Dead
# description: How should the settlement make room for mourning?
# conditions: age: <= 1; population: >= 1; legends_met: >= 1; event_completed:si_mourning < 1; true_deaths: >= 1
# event_type: Social
# priority: 10
# theme: Emotional Core
# cast: area:culture, co:1
# issue: true
The death count has increased. At the settlement's board, a neighbour has begun adding occupations beneath names: mender, grower, watcher, the person who always arrived early to light the fire.

The clerk asks whether these belong in an official account. The neighbour asks what else the account is for.

* Hear the question
-> si_mourning_chorus

=== si_mourning_chorus ===
How should the settlement make room for mourning?

* Idealism. Hold a modest shared meal&D Spend food to give mourners time together without altering the death ledger.&C requirements:cost:resource:Food 6 -> si_mourning_verse_a
* Realism. Collect the names and their witnesses&D Make an honest record without asking an empty kitchen to host a feast. -> si_mourning_verse_b

=== si_mourning_verse_a ===
The meal is plain. People speak when they can and eat when they cannot. The clerk brings the board inside so names do not have to be remembered against the wind. The count is unchanged; the account has become harder to dismiss.

* Continue&C consequences: fragment:cast Acceptance +3; stat:morale +2
-> si_mourning_outro

=== si_mourning_verse_b ===
The clerk copies each account with the speaker's name beside it. Where two memories disagree, both remain. Nobody is made smaller to make the total easier to bear.

* Continue&C consequences: fragment:cast Meaning +3; stat:morale +1
-> si_mourning_outro

=== si_mourning_outro ===
The account is kept with the settlement's stories.

* Return
-> DONE
