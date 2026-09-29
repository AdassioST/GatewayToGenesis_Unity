// Public memory and civic legitimacy (CultureSystem.PublicMemory.cs, T09). Both stories are locked: the culture unlocks
// one when the records of a linked promise disagree with it (culture:dispute:<kind>), and it stays open until the
// council answers here or in the Culture window (Accounts). Each answer is a "culture:account <kind> <answer>"
// consequence; its costs are paid by the culture, never by the story. Only what was recorded is quoted.
//   public_memory_hollow_table   a hospitality promise (the Open Gate, the Almshouse Charter, the Feast of Abundance)
//                                against the tables set, the people admitted and the luxuries held back.
//   public_memory_unbaked_loaf   the Lesson of the Hunger against the dedications to the losses remembered.
// Story words (read only; written \{...\} in Ink, whose braces are logic): {dispute_tradition}, {dispute_promise},
// {dispute_against}, {dispute_for}, {dispute_places}, {dispute_sponsor}, {acknowledge_cost}, {sponsor_cost}.
// The disputes are adaptations (The Ballad of Hollow Banquet, Civic.md; The Inescapable Hunger); the prose is the game's.
// Hyphens stand in for dashes.

-> DONE

// ===== THE HOLLOW TABLE =====

=== public_memory_hollow_table ===
# title: The Hollow Table
# cast: area:governance
# description: The council said a tradition shows a promise. The records of the tables say otherwise.
# conditions: culture:dispute:hollow-table
# event_type: Social
# priority: 70
# locked: true

Someone has read the feast-rolls aloud in the square. The council once said that \{dispute_tradition\} shows \{dispute_promise\}.

The rolls say this: \{dispute_against\}

And this, too: \{dispute_for\}

Place by place: \{dispute_places\}.

* Call the council together
-> public_memory_hollow_table_chorus

=== public_memory_hollow_table_chorus ===
The rolls will not change, whatever is said. What the council says about them will be remembered with them.

* Realism. Admit it: the table was not open to all.&D The records stand as they are. It costs \{acknowledge_cost\} Unity; the council's word is worth more for having been kept honestly.&C requirements:culture:answer:hollow-table:acknowledge;success:public_memory_hollow_table_owned;failure:public_memory_hollow_table_owned;consequences:culture:account hollow-table acknowledge +1 -> public_memory_hollow_table_owned

* Idealism. Take back the claim.&D \{dispute_tradition\} will no longer be said to show \{dispute_promise\}. Free, but the promise loses what the tradition gave it.&C requirements:culture:answer:hollow-table:revise;success:public_memory_hollow_table_revised;failure:public_memory_hollow_table_revised;consequences:culture:account hollow-table revise +1 -> public_memory_hollow_table_revised

* Pragmatism. Let \{dispute_sponsor\} tell it another way.&D The private tables were thanks to those who gave. It costs \{sponsor_cost\} Unity, and the places whose rolls disagree will doubt it.&C requirements:culture:answer:hollow-table:sponsor;success:public_memory_hollow_table_retold;failure:public_memory_hollow_table_retold;consequences:culture:account hollow-table sponsor +1 -> public_memory_hollow_table_retold

=== public_memory_hollow_table_owned ===
The council says it plainly: the table was not open to all. No one cheers. Some of the ones who were turned away nod, slowly, and stay to listen.

* Keep the rolls as they are
-> public_memory_hollow_table_outro

=== public_memory_hollow_table_revised ===
The claim is taken back. The tradition goes on as it did; it is simply no longer held up as proof of anything.

* Let the tradition be only itself
-> public_memory_hollow_table_outro

=== public_memory_hollow_table_retold ===
\{dispute_sponsor\} speaks well, and many are glad to hear it. The feast-rolls are rolled up again, unchanged, and those who read them remember what they said.

* Let the account stand
-> public_memory_hollow_table_outro

=== public_memory_hollow_table_outro ===
What was said is written beside what was recorded. Both will be read again.

* Close the rolls
-> DONE

// ===== THE LOAF NO ONE BAKES =====

=== public_memory_unbaked_loaf ===
# title: The Loaf No One Bakes
# cast: area:governance
# description: The council said a tradition keeps a loss in memory. The memorials say it is not kept.
# conditions: culture:dispute:unbaked-loaf
# event_type: Social
# priority: 70
# locked: true

At a naming, an old baker stops the speaker. The council once said that \{dispute_tradition\} shows \{dispute_promise\}.

What is recorded: \{dispute_against\}

And this: \{dispute_for\}

Place by place: \{dispute_places\}.

* Hear the baker out
-> public_memory_unbaked_loaf_chorus

=== public_memory_unbaked_loaf_chorus ===
The dead are counted as they were counted. What is said about keeping them is another thing, and it will be remembered too.

* Realism. Admit it: the memory was said more than kept.&D What was lost stays recorded as it was. It costs \{acknowledge_cost\} Unity.&C requirements:culture:answer:unbaked-loaf:acknowledge;success:public_memory_unbaked_loaf_owned;failure:public_memory_unbaked_loaf_owned;consequences:culture:account unbaked-loaf acknowledge +1 -> public_memory_unbaked_loaf_owned

* Idealism. Say only what the memorials show.&D \{dispute_tradition\} will no longer be said to show \{dispute_promise\}. Free, but the promise loses what the tradition gave it.&C requirements:culture:answer:unbaked-loaf:revise;success:public_memory_unbaked_loaf_revised;failure:public_memory_unbaked_loaf_revised;consequences:culture:account unbaked-loaf revise +1 -> public_memory_unbaked_loaf_revised

* Pragmatism. Let \{dispute_sponsor\} tell it another way.&D The memory is kept in the heart, not the oven. It costs \{sponsor_cost\} Unity, and the memorials will still say what they say.&C requirements:culture:answer:unbaked-loaf:sponsor;success:public_memory_unbaked_loaf_retold;failure:public_memory_unbaked_loaf_retold;consequences:culture:account unbaked-loaf sponsor +1 -> public_memory_unbaked_loaf_retold

=== public_memory_unbaked_loaf_owned ===
The council admits it. The baker says nothing, only sets a loaf on the table, and this time someone breaks it.

* Break the bread
-> public_memory_unbaked_loaf_outro

=== public_memory_unbaked_loaf_revised ===
The claim is taken back. The memorial is what it is, no more and no less; no one says it is a promise kept.

* Let the memorial be only itself
-> public_memory_unbaked_loaf_outro

=== public_memory_unbaked_loaf_retold ===
\{dispute_sponsor\} speaks of memory kept in the heart. It is a kind thing to hear. The baker goes home and bakes, alone.

* Let the account stand
-> public_memory_unbaked_loaf_outro

=== public_memory_unbaked_loaf_outro ===
The names are spoken again at the next naming, as they were counted. What the council said is remembered beside them.

* Speak the names
-> DONE
