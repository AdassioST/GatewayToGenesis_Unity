// The Age of Renewal (Ages I): its opening, its Acts of Fate and its Age Crisis, the Great Plague.
// Offered by AgeProgression at their moments (every story is locked until then, and tells once).
// In-world quotations are the vault's (The Inescapable Hunger.md, Great Plague.md); the Age of Renewal's own note is
// still empty in the vault (listed in Canon Gaps). Em dashes are written as hyphens for the game font.
// Scores read by the Age asset:
//   plague_preparation  lowers the plague's severity
//   aromatics           raises it (the Aromatics Trap: fumigation drives survivors indoors, with the moths)
//   refuge_flight       desperate relief: flight to low-saturation ground saves lives now

-> DONE

// ===== THE AGE BEGINS =====

=== renewal_dawn ===
# title: The Age of Renewal
# description: The land is a chord, not a note.
# conditions: event_completed:renewal_dawn < 1
# event_type: Environmental
# priority: 100
# locked: true

From these roots, the earliest Agromagical Conclaves form - seed communities that will later crystallize into full Agromagical Enclaves in the Age of Renewal.

Their core doctrine is simple and heretical in a world raised on miracles:

"The land is a chord, not a note. No stone alone can save a starving field."

* Walk the new fields
-> renewal_dawn_chorus_1

=== renewal_dawn_chorus_1 ===
The Auric Peach Famine is remembered already, and by many names: The Ash-Bread Winter. The First Reckoning. How will your people remember it?

* Idealism. As the First Reckoning: never again.&D Those who survived by diversity and restraint remember hard-won pride in understanding.&C success:renewal_dawn_verse_1;failure:renewal_dawn_verse_1;consequences:stat:morale +8;score:plague_preparation +1 -> renewal_dawn_verse_1

* Realism. As the Ash-Bread Winter: we lived, and that is all.&D Those who survived by desperate ash-burning remember guilt and fear of plenty.&C success:renewal_dawn_verse_2;failure:renewal_dawn_verse_2;consequences:production_percent:Food +10 -> renewal_dawn_verse_2

* Pragmatism. As a lesson in rotation.&D Auric peaches, then earth-beans, then hardy grains and greens, then fallow.&C success:renewal_dawn_verse_3;failure:renewal_dawn_verse_3;consequences:production_percent:Food +5;score:plague_preparation +1 -> renewal_dawn_verse_3

=== renewal_dawn_verse_1 ===
The field-weavers go out at dawn: not the war-caster or priest, but a person whose Motif Awakening aligns to the soil, rain, and root rather than flame or blade.

* Follow them
-> renewal_dawn_outro

=== renewal_dawn_verse_2 ===
The cellars fill again. No one speaks of the pits.

* Fill the cellars
-> renewal_dawn_outro

=== renewal_dawn_verse_3 ===
Letting land lie unused for a year - sown only with wild groundcovers - becomes a kind of ritual silence.

* Let it rest
-> renewal_dawn_outro

=== renewal_dawn_outro ===
The world remembers excess; the survivors inherit the bill.

* Begin again
-> DONE

// ===== ACT OF FATE: ACT II BEGINS =====

=== renewal_relief_festival ===
# title: The Relief Festival
# cast: area:culture
# description: The first infections appear in festival capitals.
# conditions: event_completed:renewal_relief_festival < 1
# event_type: Mystical
# priority: 100
# locked: true

The hunger is over, and the capitals mean to say so: a Great Hunger relief festival, with every Spellweaver who can still call an element singing at once.

* Prepare the festival
-> renewal_relief_festival_chorus_1

=== renewal_relief_festival_chorus_1 ===
How loud will the festival be?

* Idealism. Every voice, every rite, all at once.&D Massive synchronized spellcasting.&C pillar:chorus;strength:14;success:renewal_relief_festival_verse_1;failure:renewal_relief_festival_verse_1;consequences:stat:morale +12;score:aromatics +1;success:consequences:resource:Aetherlight +40 -> renewal_relief_festival_verse_1

* Realism. A quiet festival in the fields.&D Bread, songs, and no rites.&C success:renewal_relief_festival_verse_2;failure:renewal_relief_festival_verse_2;consequences:stat:morale +4;score:plague_preparation +1 -> renewal_relief_festival_verse_2

* Pragmatism. Let the Conclaves decide.&D The field-weavers keep their own calendar.&C pillar:waltz;strength:12;success:renewal_relief_festival_verse_2;failure:renewal_relief_festival_verse_1;success:consequences:stat:morale +6;score:plague_preparation +1;failure:consequences:stat:morale +8;score:aromatics +1 -> renewal_relief_festival_verse_2

=== renewal_relief_festival_verse_1 ===
Leylines surge under the weight of the song. The festival is remembered for a generation.

* Sing
-> renewal_relief_festival_outro

=== renewal_relief_festival_verse_2 ===
The fields hear more of the festival than the leylines do.

* Rest
-> renewal_relief_festival_outro

=== renewal_relief_festival_outro ===
Luminant Moths gather around the lanterns long after the last song.

* Return
-> DONE

// ===== THE AGE CRISIS BEGINS: WAVE I =====

=== renewal_silent_vectors ===
# title: A Simple Cough
# cast: area:welfare
# description: Relatively mild symptoms.
# conditions: event_completed:renewal_silent_vectors < 1
# event_type: Environmental
# priority: 100
# locked: true

It begins with a simple cough that has relatively mild symptoms. Cases appear in capitals and trade nodes. The infected have sweet-smelling breath: a sweet, floral aroma, honey mixed with fermenting fruit.

* Visit the sick
-> renewal_silent_vectors_chorus_1

=== renewal_silent_vectors_chorus_1 ===
Authorities observe: "Sweet smell + death = smell causes death."

* Idealism. Burn incense. Mask the sweet air.&D Distribute aromatic masks; burn incense; fumigate streets with herbs and compounds.&C success:renewal_silent_vectors_verse_1;failure:renewal_silent_vectors_verse_1;consequences:score:aromatics +2;stat:morale +5 -> renewal_silent_vectors_verse_1

* Realism. Close the trade nodes. Isolate the sick.&D Quarantine; masks; standard medical isolation.&C pillar:regalia;strength:16;success:renewal_silent_vectors_verse_2;failure:renewal_silent_vectors_verse_3;success:consequences:score:plague_preparation +2;production_percent:Food -10;duration:sevenths:14;failure:consequences:score:plague_preparation +1;stat:morale -8 -> renewal_silent_vectors_verse_2

* Pragmatism. Watch where it spreads, and what follows the sick.&D Something follows the sick from house to house.&C requirements:cost:resource:Research 30;success:renewal_silent_vectors_verse_4;failure:renewal_silent_vectors_verse_4;consequences:score:plague_preparation +1 -> renewal_silent_vectors_verse_4

=== renewal_silent_vectors_verse_1 ===
The streets smell of herbs. It seems effective, and creates false confidence.

* Breathe easier
-> renewal_silent_vectors_outro

=== renewal_silent_vectors_verse_2 ===
The trade nodes fall silent. The markets suffer, and the cough slows.

* Hold the line
-> renewal_silent_vectors_outro

=== renewal_silent_vectors_verse_3 ===
The quarantine leaks. Families hide their sick from the wardens.

* Keep trying
-> renewal_silent_vectors_outro

=== renewal_silent_vectors_verse_4 ===
Moths. Wherever the sick sleep, the lanterns gather moths.

* Write it down
-> renewal_silent_vectors_outro

=== renewal_silent_vectors_outro ===
Wave I subsides in the capitals, and false confidence spreads.

* Return
-> DONE

// ===== ACT OF FATE: ACT III BEGINS =====

=== renewal_sweet_smell ===
# title: The Sweet Smell
# cast: area:innovation
# description: Tragic misdirection.
# conditions: event_completed:renewal_sweet_smell < 1
# event_type: Crisis
# priority: 100
# locked: true

The sweet smell is faint initially, and becomes overwhelming as the infection progresses. It is most noticeable in early morning.

It is different from the typical illness smell: not foul, genuinely pleasant.

* Decide what to do about the air
-> renewal_sweet_smell_chorus_1

=== renewal_sweet_smell_chorus_1 ===
Outdoor moths are killed by the fumes. Survivors are driven indoors.

* Idealism. Fumigate everything, day and night.&D Stronger fumigation; mandatory masks; prayer.&C success:renewal_sweet_smell_verse_1;failure:renewal_sweet_smell_verse_1;consequences:score:aromatics +2;stat:morale +5 -> renewal_sweet_smell_verse_1

* Realism. Stop the fumigation. Open the windows.&D It does not smell like death. Why fight the smell?&C pillar:aureus;strength:18;success:renewal_sweet_smell_verse_2;failure:renewal_sweet_smell_verse_3;success:consequences:score:plague_preparation +2;failure:consequences:stat:morale -10 -> renewal_sweet_smell_verse_2

* Pragmatism. Send the frail to the secluded settlements.&D Secluded Refuges get hit last.&C requirements:cost:resource:Food 25;success:renewal_sweet_smell_verse_4;failure:renewal_sweet_smell_verse_4;consequences:score:refuge_flight +2 -> renewal_sweet_smell_verse_4

=== renewal_sweet_smell_verse_1 ===
The houses fill with smoke, the people, and the moths.

* Seal the doors
-> renewal_sweet_smell_outro

=== renewal_sweet_smell_verse_2 ===
The priests call it heresy. The physicians call it a guess. The fewer the fumes, the fewer the moths in the houses.

* Keep the windows open
-> renewal_sweet_smell_outro

=== renewal_sweet_smell_verse_3 ===
The people will not open their windows to the smell of death.

* Let them be
-> renewal_sweet_smell_outro

=== renewal_sweet_smell_verse_4 ===
The carts leave by night, for the hidden settlements alongside the map.

* See them off
-> renewal_sweet_smell_outro

=== renewal_sweet_smell_outro ===
The sweet smell contains no infectious material. No one knows this.

* Return
-> DONE

// ===== THE CRISIS IS NAMED: WAVE II =====

=== renewal_escalation ===
# title: The Great Plague
# cast: area:welfare
# description: The music is eternal. The movement is infinite.
# conditions: event_completed:renewal_escalation < 1
# event_type: Crisis
# priority: 100
# locked: true

"I feel alive in a way I've never felt. Everything is bright. My body moves like it's dancing with the stars. I don't know why I'm moving. I don't want to stop. I can't stop. My heart is racing so fast. Is this dying? It doesn't feel like dying. It feels like transcendence. Why are others joining me? Can they feel it too? Are we all becoming something greater? The music is eternal. The movement is infinite. I am-"

* Look away
-> renewal_escalation_chorus_1

=== renewal_escalation_chorus_1 ===
The Dancing Plague is dominant. Authority structures crumble.

* Idealism. Pray with the dancers.&D Prayer and ritual.&C pillar:chorus;strength:20;success:renewal_escalation_verse_1;failure:renewal_escalation_verse_2;success:consequences:stat:morale +10;failure:consequences:deaths:+8;stat:morale -10 -> renewal_escalation_verse_1

* Realism. Flee to low-saturation ground.&D Survivors either flee or enter underground shelters.&C requirements:cost:resource:Food 30;success:renewal_escalation_verse_3;failure:renewal_escalation_verse_3;consequences:score:refuge_flight +3;vagrants:-5 -> renewal_escalation_verse_3

* Pragmatism. Go underground and wait.&D Underground shelters, sealed.&C pillar:regalia;strength:18;success:renewal_escalation_verse_4;failure:renewal_escalation_verse_2;success:consequences:score:plague_preparation +2;failure:consequences:deaths:+6 -> renewal_escalation_verse_4

=== renewal_escalation_verse_1 ===
The dancers are carried to the shrines, and the shrines keep them.

* Keep vigil
-> renewal_escalation_outro

=== renewal_escalation_verse_2 ===
The dance spreads through the vigil.

* Bury the dead
-> renewal_escalation_outro

=== renewal_escalation_verse_3 ===
The roads fill with carts heading away from the cities.

* Go with them
-> renewal_escalation_outro

=== renewal_escalation_verse_4 ===
The cellars are sealed with the survivors inside, and the music stays out.

* Wait
-> renewal_escalation_outro

=== renewal_escalation_outro ===
One city's cascades raise saturation in adjacent cities, triggering cascades there.

* Return
-> DONE

// ===== WAVE III =====

=== renewal_endemic ===
# title: The Endemic Phase
# cast: area:governance
# description: Civilization continues in reduced form.
# conditions: event_completed:renewal_endemic < 1
# event_type: Crisis
# priority: 100
# locked: true

The plague has transitioned from epidemic to endemic status. All regions are simultaneously affected; there are no new populations to infect.

Life is disrupted but functional. Some settlements thrive relative to others.

* Take stock
-> renewal_endemic_chorus_1

=== renewal_endemic_chorus_1 ===
What will this Age remember?

* Idealism. The healers who stayed.&D The first to publish the word of the first vaccine.&C success:renewal_endemic_verse_1;failure:renewal_endemic_verse_1;consequences:score:plague_preparation +1;stat:morale +5 -> renewal_endemic_verse_1

* Realism. The cities that burned.&D Regions become "Wasted Zones".&C success:renewal_endemic_verse_2;failure:renewal_endemic_verse_2;consequences:stat:morale -5;resource:Research +40 -> renewal_endemic_verse_2

=== renewal_endemic_verse_1 ===
Their names are written down, in the settlements that still keep records.

* Remember them
-> renewal_endemic_outro

=== renewal_endemic_verse_2 ===
The wastes are mapped, and no one goes back.

* Remember them
-> renewal_endemic_outro

=== renewal_endemic_outro ===
The plague becomes an endemic disease, not an epidemic.

* Return
-> DONE
