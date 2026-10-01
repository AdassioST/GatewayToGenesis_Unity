# Symphony of War: the card layer and battle verdicts

Built 2026-09-29 at the owner's request: keep the deckbuilder half of combat (Slay the Spire, Chaos Zero Nightmare)
alive in the auto-resolve, give units basic decks, give expeditions kits, give every Spellweaver a personal grimoire,
make creatures play by the same rules, weigh every side's Symphony as one power number, count terrain, and read
results the way Civilization VI, Total War and Crusader Kings III do, with seven verdicts. **Every card, cost,
threshold and number here is a proposal** (roadmap D10; Canon Gaps, "The Symphony of War's card layer").

Vault basis: Combat System.md. The micro layer's attacks "are dictated by Symphony Cards", covering sword attacks and
Magic Arts for offense, defense and utility. Repeating the melody back grows the Chord Layering stack, and the
opposite melody cancels noise on defense. Also Chord Layering.md, The Principles of Magic.md, Ages.md (via `AgeMagic`),
and the owner's Grave Warden and Wasteland Archer cards.

## 1. One engine for both layers

`BattleResolver.Resolve` is now `Begin(...).Finish()`. `BattleRun` plays a battle measure by measure:

| Step | Auto-resolve | Manual (the micro layer, later) |
|---|---|---|
| `BeginMeasure` | steps sections into the line, deals each side Beats and a hand, and the performer plans its plays (shown as **intent**, like Slay the Spire's enemy intents) | same, but a `manual` side's hand waits for the player |
| between | nothing | `Hand`, `BeatsLeft`, `WhyNotPlay`, `Play(side, card, target, rendition)`, `AutoPlay` |
| `ResolveMeasure` | cards first (the side with initiative first), then the drilled rhythm: spells, steel, parries, toll, states | the same |
| `Finish` | plays the rest and grades the battle | the same, plus the Legendary check |

`rendition` is where the rhythm layer plugs in. 1 is clean, `perfect` is 1.25 and `missed` is 0.6. The vault's
"repeat the melody back" is a rendition above 1 on attacks and spells, and its "opposite melody" is the same on guards
and wards.

A side **without a deck is the plain auto-resolve, unchanged**. Its ostinato is 1 and no card runs. The legacy combat
suites still pass, including the 42 `CombatTests`.

## 2. Ostinato and melody

With a deck, a side's drilled fighting (its sections' own steel and spells every measure) runs at `ostinato` = 0.7.
The cards carry the rest. Each measure gives **Beats** = round(1 + 0.5 per standing voice), 1 to 7, plus one for a
commander with at least 2 stars as a Great Sovereign. The hand is the Beats + 2, plus one for any Great Seer stars.
Every effect scales with the section that voices it: a Grave Warden's blow is a Grave Warden's attack. Every effect is
also multiplied by `cardScale` = `melody` × voices / Beats (`melody` = 0.35). That way a side is weighed by how good
its cards are, not by how many voices it has.

Calibration (the scratchpad sweep, 60 forecast runs per line):

| Battle | Win % (or held) | Reading |
|---|---|---|
| Hearth Guard vs Hearth Guard, no decks | 0% (26 of 60 held) | legacy baseline: the defender holds |
| the same, attacker ×1.15 raw strength | 20% | |
| the same, attacker ×1.25 raw strength | 60% | |
| **attacker with a par deck** vs no deck | 8% (55 of 60 held), losses 46%/66% | **a par deck ≈ +10-15% raw strength** |
| both with decks | 0% (60 held) | back to the baseline |
| 2 legends vs 12 wolves, Wayfarer's Kit | 0% | |
| the same, Hunter's Kit | 5-15% | the kit matters, most against creatures |

## 3. Where cards come from (`SymphonyDecks.Build`)

- **Basic decks: every unit brings five cards, 2 Offensive, 2 Defensive, 1 Utility** (`SymphonyCards.Sections`,
  `BasicShape`, one copy per section). Grave Warden: Grave-Spade Strike, Headstone Blow | Vigil Stance, Wall of
  Mourners | In the Shadow of the Fallen. Wasteland Archer: Ash Volley, Loose from Cover | Fall Back and Loose, Stakes
  in the Ash | Scavenge Arrows. All twelve section kinds follow the shape, and so does an expedition party (`party`:
  Walking Staff, Sling Stone | Circle the Packs, Stand Firm | Stand Together) and every creature (`Creature`: a blow by
  size, a second weapon by kind, two guards, and one thing it does between blows).
- **Expedition kits** add the party's specialty on top of its basic deck (`WorldUnit.kit`, changed in a settlement,
  with carried weight added to the load). Setup and Modulation cards live here and in grimoires:

  | Kit | Needs | Cards |
  |---|---|---|
  | Wayfarer's Kit | Lookout Towers | Break and Regroup, Mark the Weak Point, Quicken the Tempo |
  | Hunter's Kit | Trapper's Patience | Snare Line, Thrown Spear, Drive the Quarry |
  | Warden's Kit | The Rekindling | Brace Shields, Lantern Watch, Grave-Spade Strike |
  | Pathfinder's Kit | Old World Roads | Read the Trail, Take the High Ground, Hold the Ford |
  | Mender's Satchel | Midwives' Lullaby | Bind Wounds, Soothing Hum, Carry the Fallen |

- **The fallback**: a section left with no card at all (settlers walking behind, a unit no deck names) gets
  **Struggle** (Offensive: ×0.7 strike, costs its voice 3% of its Integrity, Recoil) and **Endure** (Defensive: guard
  itself, +2 Composure). Voices with little attack of their own (musicians, menders, a settler) strike with at least
  `handAttack` 4.

- **Personal grimoires** (`LegendGrimoires`, saved on each legend's record):
  - the legend's leitmotif as its binding's **Principle** (Key of Attunement, Sufficient Precision, Emotional
    Authenticity, Essence Sacrifice, Perfect Focus, Absolute Certainty, Echoing Bonds), a Major Note once the legend
    is Awakened;
  - its Ornaments, from Age III (Ornamental Magic, D13);
  - the Symphony Cards it has learned, up to its **pages**: 2, plus 1 per Ornament, 1 for the Awakened State, and 1
    per 3 stars in the Greats. Major Notes need an Awakened legend. A legend never given a choice learns the owned
    cards of its own bindings by itself.
  - The legend voices these through the section it leads, or as commander if it leads none.
- **A commander's orders**: Rally to Me, plus one per Great it holds a star in. The Great's card is Press the Attack,
  Hold Fast, One Key, Crescendo, Read the Field, Sentence or No One Left Behind, and gains +25% per star beyond the
  first.
- **The war score** (armies): the civilization's owned Symphony Cards, up to its Symphony seats
  (`WorldSystem.SetWarScore`; it chooses by itself until one is set). A card is voiced by the best caster of its
  binding, or else by the commander.
- **Creatures** (`SymphonyCards.Creature`), read from the species:
  - a blow by size (Swarm, Rend and Bite, Maul, Trample);
  - a Challenge roar by stance;
  - Flank the Weak for hunters, Scatter for the timid;
  - an organ's gift (Harden Hide, Stoop, Undertow in water, Phase Pulse);
  - Instinct of its primary binding (cast by instinct, not bound by the Age);
  - Static Hunger for the Atonalis, Drink the Wound for Formless Masses.

The seven Symphony Cards of Act I have battle cards under their asset ids (Coaxed Spring douses burns, Igniting Cooking
Pot burns, and so on). A later card with no battle card is composed from its Root, as the Spell Maker would compose it.

## 3b. The card format (`CardFace`, `SymphonyCardView`, `SymphonyWindow`)

Every card is written and drawn as the Sonata website's Spell Builder draws a spell (`src/features/spells/SpellCard.tsx`):

- **The five Purposes** (`SpellPurpose`): **Offensive** (The Lead: destabilise the target's frequency), **Defensive**
  (The Counterpoint: preserve your own), **Setup** (The Measure: prepare latent harmonics), **Utility** (The Texture:
  manipulate the environment or the state of matter), **Modulation** (The Ornamentation: alter an effect already
  active).
- **The face**: the whole card in its Root's colours; the rank (Unison, Dyad, Triad, Tetrad Chord, Minor or Major) in
  the corner and the Root's note opposite; the chord drawn on the circle of fifths as the art (C at twelve o'clock, the
  five rests, the B-F tritone left open); the cost in Beats on an orb; the name, the suit line (Root • Minor Notes), the
  practice (The Registers of Magic), the catchline, the Purpose stamp and who brought the card. Steel, orders and
  instincts have no Root: their suit is Steel and their ring stays unlit.
- **The back** ("The reading"): the Purpose and its role, then what the card does, what it costs, who sounds it, the
  image the weaver holds, where it is at home, and its canon anchor.
- **Groundwork**: each Setup played earlier in a measure makes the side's Offensive cards land 15% harder, up to 3
  (`SymphonyTuning.groundwork`, the Spell Builder's "three spells of groundwork, then a cheap attack"). The performer
  values a Setup by the best attack it prepares.
- **A card's own chord is bound by the Age**: a Triad card cannot be played in Age 0; Age II plays its first Triads.
- **The window**: "See its Symphony" on your units' cards and on enemy bands opens every card of that side, face or
  back, filtered by Purpose; touching a card turns it.

## 4. Rules a card follows

- **Voice.**
  - A card voiced by a fallen, fled or taken section is dead in the hand.
  - A strike needs its section in the line.
  - A spell needs a voice that is not Mind Broken.
  - An order needs a commander who has not broken.
- **Spells go through `Cast`**: the Elemental Harmonic Circle, Signal Loss, tempo, the Age's command, the Loom, the
  land's element, and Essence Sacrifice paid in Composure.
  - A Unison card's flicker is capped at its own chance (Minor 0.15, Major 0.35), so Age I's stable Unison takes over.
  - Spell cards with no strike of their own are still paid for, and a flicker weakens them.
- **Ensemble layering.** Spell cards of **different voices** in one measure layer into one chord: earlier Roots become
  later Minor Notes, up to the largest chord the Age plays (an unreliable Dyad in Age 0). This is the Ceremony's
  one-note-each rule carried into battle. A single caster never layers its own notes.
- **The field.**
  - Cards list grounds and conditions: Concealed, High Ground, River Crossing, Settlement, Defending, Attacking, the
    Opening, Hunting, the land's own element, Sacred ground, a Leyline.
  - At home on the field, a card lands ×1.2 to ×1.6. A card marked `requires` is only playable there.
  - Examples: Loose from Cover in a wood ambush (×1.68), Hold the Ford when they wade, Plunging Shot from high ground.
- **Effects**: Strike, Spell, Dread, Guard, Ward, Mend, Rally, Draw, Beat, Expose, Blind, Burn, Douse, Push (thrown out
  of the line for a measure), Entrench, Sure (missiles not parried), Surge, Crescendo.

## 5. The power gauge (`SymphonyPower`)

- `Rate` gives a side's offense and endurance per measure. Offense is its drilled harm plus its deck's expected harm.
  Endurance is Integrity, half its Composure, and what parries, guards and mending spare over about 6 measures.
- **Power** = √(offense × endurance), a Lanchester strength. `Odds` compares two powers squared.
- `Breakdown` lists what makes the number, Civ VI style:
  - base strength and wounds and weariness;
  - Symphony, and ideal or poor terrain;
  - the commander, legends leading sections, attached companies;
  - coming downhill or higher ground, crossing a river, holding a settlement, dug in;
  - the land's element, a coherent or frayed Loom, and the elements for or against the side;
  - who reads the ground first or walks into an ambush, and Vibrational Fallout.
  - The lines add up to the strength.

## 6. Seven verdicts (`BattleVerdicts`)

Drawn from Total War's ladder (Heroic Victory for a battle won against the balance of power; Valiant Defeat), Civ VI's
major and minor results, and the owner's list. The verdict is read from **both sides' losses** (captives count as
lost), and a battle's two verdicts **mirror** each other:

| Winner | Rule | Loser's mirror |
|---|---|---|
| **Decisive Victory** | own losses ≤ 20% and the enemy's at least 30 points higher | **Crushing Defeat** |
| **Close Victory** | anything between | **Close Defeat** |
| **Pyrrhic Victory** | own losses ≥ 50%: the force is left vulnerable | **Valiant Defeat** |
| **Legendary Victory** | won **by hand** (`BattleSide.manual`, at least one card played by the player) when the forecast gave it ≤ 30% | (the loser is graded by its losses) |

- A field held when the measures run out is the defender's win (`BattleReport.Held`).
- The auto-resolve never grants the Legendary Victory. `BattleResolver.Begin` forecasts a manual battle first, or takes
  a preview's forecast through `BattleRun.Prediction`.

## 7. The battle screen (`BattlePreview`, `BattleRecord`, `BattleWindow`)

- **Before**:
  - your side on the left, the enemy on the right;
  - each side's strength with its modifier list, deployed sections and head count, and Symphony (cards, Beats);
  - in the middle: the balance bar, "59 vs 44, Advantage +15 (favoured)", the advisors' line ("With our superior
    forces, our advisors predict a Close Victory"), the chance to win, and predicted casualties (Very low to
    Catastrophic).
  - Open it from a party's card: **Battle preview: <band>**. Each Attack/Hunt order also carries the odds
    ("…, 52 vs 31, favoured").
- **After**:
  - the verdict in its colour and what it means;
  - the same columns, with losses as bars, dead and wounded, captives, Mind Breaks, cards played and the most played;
  - "How it went" (Mind Breaks, the fallen, the taken, cards at home on the field, layered chords), and the aftermath
    on the map.
  - It opens by itself after a battle one of your units fought (`BattleWindow.AutoOpen`). The notification leads with
    the verdict.
- The map cards show each unit's Symphony (strength, deck, kit), each legend's grimoire with its pages, and a band's
  strength against your nearest party. Kits are switched from a party's card in a settlement.

## 8. Open decisions for the owner

1. **Numbers**: `ostinato` 0.7, `melody` 0.35, Beats, hand size, every card's amount and cost, and the verdict
   thresholds (20%, 30 points, 50%, Legendary at ≤ 30%).
2. **Should verdicts carry consequences?**
   - A Decisive Victory could pay more Defiance or merit, and a Crushing Defeat more strain.
   - A Legendary Victory could pay Era Score or a ballad fragment.
   - Today the tiers are read-only: fates and merit are unchanged.
3. **Ensemble layering in Age 0.** Two voices' Unisons form an unreliable Dyad, extending the Ceremony rule. D13 locks
   Ornamental Magic for single casters only. Keep it, or wait for Age I?
4. **Kit costs**: kits cost only carried weight today. Should they cost Elderwood or Duskstone to outfit?
5. **The war score** needs a Grimoire window to seat cards; until then it takes the first owned cards.
6. **A Grimoire UI for personal grimoires**: `LegendProgress.Learn`/`Forget` exist, but no window calls them yet. The
   party card shows each grimoire.
7. **`BattleWindow.AutoOpen`** could become an option in the Options menu.
8. **Card numbers of the new format**: groundwork 15% (cap 3), Struggle ×0.7 with 3% Recoil, Endure, `handAttack` 4,
   and whether Setup/Modulation should also appear in some basic decks (today only kits, grimoires and orders carry them).
9. **The micro layer itself**: a hand UI, the rhythm input that sets `rendition`, and the hex grid. The API is ready.
