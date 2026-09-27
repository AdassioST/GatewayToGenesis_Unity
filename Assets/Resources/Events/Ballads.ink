// Ballads: storylines of several events that grow the legends who live them. Each verse is unlocked by the world map
// (a feature's story) or by the verse before it. Its ballad actors are the expedition that found it (# cast: expedition),
// carried from verse to verse; fragment: consequences pay its roles, and the finale pays the ballad's theme
// (# theme: Scholar: Lucidity x5, Rebirth x3 to the protagonist, a share to each co-protagonist).
// In-world quotations are the vault's ("The World's Dirge", The Inescapable Hunger.md). Em dashes are written as
// hyphens for the game font.

-> DONE

// ===== THE BALLAD OF THE RUIN-SONG =====
// Verse I: a Silent Spire is explored. Verse II: a second one. Verse III: the Age of Renewal has begun.

=== ballad_ruin_song_1 ===
# title: The Ruin-Song, Verse I
# ballad: ruin_song
# verse: 1
# ballad_title: The Ballad of the Ruin-Song
# theme: Scholar
# cast: expedition
# description: A global, tuneless hum that carries for leagues.
# conditions: event_completed:ballad_ruin_song_1 < 1
# event_type: Mystical
# priority: 50
# locked: true

Former capital spires stab the horizon like grave markers. Upper levels are open to the sky, choked with wind and dust.

Wind through broken spires, hollow towers, and fractured resonators creates a constant low harmonic: a global, tuneless hum that carries for leagues.

* Listen&C consequences: fragment:protagonist Lucidity +3; score:ballad_ruin_song +1; unlock_event:ballad_ruin_song_2
-> ballad_ruin_song_1_outro

=== ballad_ruin_song_1_outro ===
The expedition comes home humming it.

* Return
-> DONE

=== ballad_ruin_song_2 ===
# title: The Ruin-Song, Verse II
# ballad: ruin_song
# verse: 2
# cast: expedition
# description: A falling third from eastern citadels.
# conditions: event_completed:ballad_ruin_song_2 < 1; map_explored:spire >= 2
# event_type: Mystical
# priority: 50
# locked: true

Local communities can distinguish the "voices" of nearby ruins by ear:

A falling third from eastern citadels. A minor seventh from the broken aqueduct. A dissonant cluster from a collapsed resonator field.

* Name the voices&C consequences: fragment:protagonist Lucidity +4; fragment:co Lucidity +2; score:ballad_ruin_song +1; unlock_event:ballad_ruin_song_3; resource:Research +30
-> ballad_ruin_song_2_outro

=== ballad_ruin_song_2_outro ===
Two spires, two voices. The expedition's songs are sung at the fires now.

* Return
-> DONE

=== ballad_ruin_song_3 ===
# title: The Ruin-Song, Verse III
# ballad: ruin_song
# verse: 3
# description: The world itself groaning over what it has lost.
# conditions: event_completed:ballad_ruin_song_3 < 1; ages_survived:all >= 1
# event_type: Mystical
# priority: 50
# locked: true

To the people of this Age, this ever-present sound is simply called "the ruin-song" - the world itself groaning over what it has lost.

Some communities time their fallow years with the softening of the ruin-song, feeling intuitively that fields, like music, need rests.

* Sing the ruin-song&C consequences: fragment:cast Rebirth +3; fragment:council Meaning +2; score:ballad_ruin_song +1; production_percent:Food +10; stat:morale +10
-> ballad_ruin_song_3_outro

=== ballad_ruin_song_3_outro ===
The Ballad of the Ruin-Song is complete. Those who lived it are remembered in it.

* Return
-> DONE
