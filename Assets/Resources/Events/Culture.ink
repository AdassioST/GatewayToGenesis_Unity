// The culture of the nation (CultureSystem). Both stories are locked: the culture unlocks them at their moment.
//   culture_founding_myth   told once, when Horology first lets the people count the Sevenths. Its answer to "Why were
//                           we founded?" founds the culture on a baseline (culture:myth <song|hearth|ruins> +1); the
//                           people are then asked their name (the naming dialog, after the story closes).
//   culture_national_food   told whenever a food, ingredient or spice has become familiar enough to be the people's own
//                           (culture:pending_food). culture:embrace makes it national, culture:decline keeps the table varied.
// Story text may name the culture (written \{nation\} in Ink, whose braces are logic): {nation}, {citizens}, {people}, {culture}, {myth}, {national_food}, {national_label}.
// The founding myths are the game's own writing (no vault source yet: Canon Gaps.md). Hyphens stand in for dashes.

-> DONE

// ===== THE FOUNDING MYTH =====

=== culture_founding_myth ===
# title: Why Were We Founded?
# cast: area:culture
# description: Now that the Sevenths can be counted, the children ask what came before the first one.
# conditions: event_completed:culture_founding_myth < 1
# event_type: Social
# priority: 120
# locked: true

The first Seventh is scratched into a doorpost, and the children ask what came before it.

Before the counting there was only the golden dust, the broken roads and the fires of whoever had survived. Somewhere among those fires, strangers stopped being strangers. No one wrote down when, or why. Tonight the elders must decide what to tell.

* Gather at the fire
-> culture_founding_myth_chorus

=== culture_founding_myth_chorus ===
Every people carries a first story. It will be told at every naming and every burial from now on, and it will decide what your people honour. Why were we founded?

* Idealism. To keep the song.&D When the world went quiet, the first of us gathered around those who still remembered a verse.&C success:culture_founding_myth_song;failure:culture_founding_myth_song;consequences:culture:myth song +1 -> culture_founding_myth_song

* Realism. To share the hearth.&D Alone, we starved. Together we ate from one pot and kept one fire burning.&C success:culture_founding_myth_hearth;failure:culture_founding_myth_hearth;consequences:culture:myth hearth +1 -> culture_founding_myth_hearth

* Pragmatism. To build on the ruins.&D We settled where the old walls still stood, and took what they still held.&C success:culture_founding_myth_ruins;failure:culture_founding_myth_ruins;consequences:culture:myth ruins +1 -> culture_founding_myth_ruins

=== culture_founding_myth_song ===
When the world ended, the song went quiet. The first of us were the ones who could not bear the silence: a woman who remembered half a lullaby, a man who hummed the rest, and everyone who stopped walking to listen.

So the story is told: we were founded so that someone would still be singing.

* Sing the first verse
-> culture_founding_myth_outro

=== culture_founding_myth_hearth ===
No one could keep a fire alone through the golden dust. The first of us pooled the last of the seed and the last of the fuel, and ate from one pot. Whoever came to the fire was fed; whoever was fed kept the fire.

So the story is told: we were founded because alone, we starved.

* Share the pot
-> culture_founding_myth_outro

=== culture_founding_myth_ruins ===
The Old World fell, but not all of it. The first of us settled in the lee of a broken wall because it was a wall, and learned to take what the ruins still held. What was left behind would become what came next.

So the story is told: we were founded where the old walls still stood.

* Lay the first stone
-> culture_founding_myth_outro

=== culture_founding_myth_outro ===
The children fall asleep before the end, as children do. The elders keep talking long into the night, and by morning the story has already changed a little in the telling.

It is theirs now. All that is missing is a name.

* Name what we have become
-> DONE

// ===== A NATIONAL FOOD =====

=== culture_national_food ===
# title: A Taste of Home
# cast: area:culture
# description: Your people have grown used to something at their table.
# conditions: culture:pending_food
# event_type: Social
# priority: 60
# locked: true

At every table from the capital to the farthest outpost, \{national_food\} is there. Children who have never known a Seventh without it ask for it by name. Travellers are greeted with it.

Among the \{people\}, it is no longer just something to eat.

* Taste it
-> culture_national_food_chorus

=== culture_national_food_chorus ===
The elders say a people is also what it eats. Should \{national_food\} be the \{national_label\} of \{nation\}?

* Idealism. Make it the heart of the feast.&D Serve it at every naming and every festival: let it be ours.&C success:culture_national_food_feast;failure:culture_national_food_feast;consequences:culture:embrace +1;stat:morale +5 -> culture_national_food_feast

* Realism. It is already ours.&D Nothing needs to be declared. The table has decided.&C success:culture_national_food_table;failure:culture_national_food_table;consequences:culture:embrace +1 -> culture_national_food_table

* Pragmatism. Keep the table varied.&D No single crop should feed a whole people.&C success:culture_national_food_varied;failure:culture_national_food_varied;consequences:culture:decline +1;score:famine_preparation +1 -> culture_national_food_varied

=== culture_national_food_feast ===
The first feast of \{national_food\} runs until the fires burn low. The \{culture\} way of preparing it is argued over at every table, and every table is certain it is right.

* Raise a bowl
-> culture_national_food_outro

=== culture_national_food_table ===
No one proclaims anything. \{national_food\} simply stays, as it always has, and the \{people\} would not know what to do without it.

* Eat
-> culture_national_food_outro

=== culture_national_food_varied ===
The elders keep the old dishes alongside the new one, and teach the children to cook them all. It is more work. It is also harder to lose everything at once.

* Keep the old recipes
-> culture_national_food_outro

=== culture_national_food_outro ===
Whatever the elders decide, the people go on eating. Tastes change slowly, and then all at once.

* Return
-> DONE
