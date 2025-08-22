-> hollow_caravan

=== hollow_caravan ===
# title: The Lost Caravan
# description: A beacon of hope or an omen of ruin? The wind carries whispers of wagon wheels and laughter, but also something darker beneath the surface.
# conditions: technology:Reconstruction; no_event_in_sevenths:2
# consequences: score:caravan_encountered +1
# cooldown: 8
# screen_flow: splash,verse,chorus,verse,verse,verse,verse,verse,verse,outro
# event_type:Crisis
# priority: 0

The horizon shimmers with the promise of something new—a caravan approaches, its wagons painted in colors that speak of distant lands and forgotten markets. But the wind carries more than just the creak of wheels and the laughter of travelers. There's something beneath the surface, like honey poured over broken glass.

The villagers gather at the gates, their faces a mixture of hope and wariness. "Travelers mean trade," says Elder Yarra, her voice carrying the weight of too many winters. "But these days, trade often comes with teeth."

* Watch them approach
-> hollow_caravan_verse_1

=== hollow_caravan_verse_1 ===
The caravan draws closer, and now you can see the details that make your heart race with both hope and dread. The wagons are painted with symbols that speak of ancient knowledge—astronomical charts, alchemical formulae, and maps of places that no longer exist. But there's something about the way the travelers move, something that suggests they're not entirely human.

The lead wagon stops just outside the gates, and a figure steps down. She's tall, her skin the color of old parchment, and when she smiles, her teeth are too white, too perfect. "Greetings, friends," she calls out, her voice carrying across the square like music. "We bring gifts from lands beyond the Desolation. Knowledge, food, and hope for those brave enough to accept them."

Behind her, other figures emerge from the wagons. They're all beautiful in the same unsettling way, their movements too fluid, their eyes too bright. The children of the village are drawn to them like moths to flame, while the elders watch with the wariness of those who have seen too many winters

The caravan leader's name is Seraphina, and as she speaks, you realize she's offering something precious—not just trade goods, but the chance to reclaim knowledge lost in the Desolation. Her wagons carry books bound in leather that seems to shimmer, fruits that smell like summer in a world that remembers warmth, and tools that could transform your village from survival to thriving.

But there's something in her eyes when she speaks of the price. "Knowledge requires sacrifice," she says, her voice dropping to a whisper. "The old ways demand payment in blood, in memory, in the very essence of what makes you human. Are you prepared to pay such a price?"

The villagers gather around, their faces a mixture of hope and fear. Some see salvation in the caravan's offerings; others see the same hunger that has destroyed other settlements.

* Approach the caravan leader
-> hollow_caravan_chorus_1

=== hollow_caravan_chorus_1 ===
 Embrace the hope they offer, or recognize the ruin they might bring?

* Idealism. It's Hope. Welcome the Caravan!&D We all deserve a second chance at life, don't we? We have to share the world with the survivors left.&C pillar:waltz;strength:15;requirements:population:population >= 5;requirements: stat:morale >= 95;requirements:cost:resource:Elderwood 5;success:hollow_caravan_verse_2;failure:hollow_caravan_verse_3;crit_success:hollow_caravan_verse_12;crit_failure:hollow_caravan_verse_13;rare_event:hollow_caravan_verse_14;rare_event_percent:3 -> hollow_caravan_verse_2

* Realism. It's Ruin. Intercept and Seize their Wares.&D Despite how cruel it may be, the city must survive. Whomever is innocent enough to believe otherwise will not stand the test of time.&C pillar:regalia;strength:20;requirements:technology:Efficient Rations == 1;requirements: stat:morale <= 115;score:caravan_encountered >= 1;success:hollow_caravan_verse_4;failure:hollow_caravan_verse_5;crit_success:hollow_caravan_verse_15;crit_failure:hollow_caravan_verse_16;rare_event:hollow_caravan_verse_17;rare_event_percent:3 -> hollow_caravan_verse_4

* Pragmatism. None to Us. Close off the Gate. Turn them away.&D Our people come first, only by the means of our nation we will grow or we will perish. No outsider will change this truth.&C success:hollow_caravan_verse_6;failure:hollow_caravan_verse_6;rare_event:hollow_caravan_verse_18;rare_event_percent:3 -> hollow_caravan_verse_6

=== hollow_caravan_verse_2 ===
"They share peaches...?" Elder Yarra whispers, her voice trembling with wonder. "And lost knowledge."

The caravan's gifts prove genuine—books that contain forgotten wisdom, fruits that taste like memories of summer, and tools that seem to work with a precision that borders on magic. The villagers gather around, their faces alight with hope for the first time in generations.

Seraphina smiles, and this time her teeth don't seem quite so sharp. "Knowledge is meant to be shared," she says. "But remember, friends—what you gain, you must also protect. The Desolation taught us that lesson well."

* Accept the gifts&C consequences: resource:Aetherlight +225; production_percent:Food +10; duration:sevenths:6; score:knowledge_gained +1; stat:morale +10; technology:Rites of Harvest enlightened
-> hollow_caravan_outro

=== hollow_caravan_verse_3 ===
"Skinwalkers gone rogue," someone shouts. "They weren't human!"

The beautiful facade shatters like broken glass. Seraphina's skin ripples and tears, revealing something beneath that makes children scream and elders reach for weapons. The caravan transforms into a nightmare of shifting flesh and hungry mouths.

The attack is swift and brutal. When it's over, the square is littered with bodies, and the wagons are gone, leaving only the stench of blood and the sound of distant laughter. The price of hope was higher than anyone imagined.

* Survey the damage&C consequences: population:population -75; score:skinwalker_attack +1; stat:morale -15
-> hollow_caravan_verse_11

=== hollow_caravan_verse_11 ===
You follow a trail of slick, black residue into an alley where something chittered moments before. Scratches in the stone form a pattern—almost writing.

* Press on
-> hollow_caravan_outro

=== hollow_caravan_verse_4 ===
"Skinwalkers turned to ashes," the village guard captain reports, his sword still smoking. "Salvage their loot."

The ambush was perfectly executed. The caravan never saw it coming, and when the first arrow found its mark, their beautiful disguises melted away like wax. What remains is a treasure trove of goods and knowledge, though some whisper that the price of such violence will be paid in other ways.

The wagons yield their secrets—ancient texts, precious materials, and tools that could transform the village. But the blood on the ground serves as a reminder that every victory comes with a cost.

* Collect the spoils&C consequences: score:caravan_looted +1; production_percent:Food -15; stat:morale -5
-> hollow_caravan_verse_19

=== hollow_caravan_verse_5 ===
"Oh the tragedy!" Elder Yarra wails. "The skinwalkers overran your army and took the village outskirts."

The ambush was a disaster. The caravan's true nature revealed itself at the worst possible moment, and what followed was a massacre. The village's defenders were overwhelmed, and the outskirts were left in ruins.

The skinwalkers didn't just kill—they destroyed. Homes were reduced to kindling, fields were salted, and the survivors were left with nothing but the knowledge that some enemies are better left alone.

* Assess the losses&C consequences: population:population -65; housing:housing -40; score:caravan_retaliation +1
-> hollow_caravan_chorus_2

=== hollow_caravan_chorus_2 ===
 The council is split—pursue the skinwalkers or fortify the walls?

* Idealism. Pursue and rescue survivors &D We cannot leave them to that fate. &C pillar:waltz;strength:18;success:hollow_caravan_verse_8;failure:hollow_caravan_verse_9;crit_success:hollow_caravan_verse_12;crit_failure:hollow_caravan_verse_13;rare_event:hollow_caravan_verse_14;rare_event_percent:3 -> hollow_caravan_verse_8

* Realism. Fortify and recover &D We must survive first. &C pillar:regalia;strength:14;success:hollow_caravan_verse_10;failure:hollow_caravan_bridge_2;crit_success:hollow_caravan_verse_15;crit_failure:hollow_caravan_verse_16;rare_event:hollow_caravan_verse_17;rare_event_percent:3 -> hollow_caravan_verse_10

=== hollow_caravan_verse_8 ===
The rescue is daring and costly, but you bring back a handful of shaken survivors.

* Return&C consequences: score:heroic_rescue +1; resource:Aetherlight +50; stat:morale +5
-> hollow_caravan_outro

=== hollow_caravan_verse_9 ===
Your pursuers do not return. The night swallows their footsteps and the trail ends in silence.

* Mourn the lost&C consequences: population:population -40; score:overreach +1; stat:morale -10
-> hollow_caravan_outro

=== hollow_caravan_verse_10 ===
Walls rise, trenches deepen, and sentries learn to spot the uncanny gait in the dark.

* Hold fast
-> hollow_caravan_bridge_1

=== hollow_caravan_bridge_2 ===
Between grief and duty, the village chooses duty. Preparations begin.

* Continue
-> hollow_caravan_outro

=== hollow_caravan_bridge_1 ===
The last stones fall into place. Dawn finds the village awake, alert, and together.

* Continue
-> hollow_caravan_outro

=== hollow_caravan_verse_6 ===
The gates remain closed, and the caravan moves on. Some villagers whisper that you've made a terrible mistake, that you've turned away salvation. Others nod approvingly, remembering stories of settlements that welcomed such visitors and paid the price.

Days pass, and nothing terrible happens. The village continues as it always has, neither better nor worse off. But sometimes, in the quiet hours before dawn, you hear the distant sound of wagon wheels and wonder what might have been.

* Continue with village life&C consequences: score:caution_chosen +1
-> hollow_caravan_outro

=== hollow_caravan_verse_12 ===
The winds shift in your favor. What should have been a risk turns into a triumph beyond expectation; allies and insights arrive unbidden.

* Carry these fortunes carefully&C consequences: resource:Aetherlight +100; score:windfalls +1; stat:morale +6
-> hollow_caravan_outro

=== hollow_caravan_verse_13 ===
Disaster compounds swiftly. A single misstep unravels plans and costs dearly; the lesson etches itself in grief.

* Count the losses&C consequences: population:population -150; score:hubris +1
-> hollow_caravan_outro

=== hollow_caravan_verse_14 ===
RARE EVENT! A stray wagon, overlooked before, reveals a cache preserved from a kinder age. Time itself seems to offer a gift.

* Accept the boon&C consequences: resource:Food +500; resource:Aetherlight +150; score:rare_boon +1; stat:morale +7
-> hollow_caravan_outro

=== hollow_caravan_verse_15 ===
CRITICAL SUCCESS! Coordination and resolve elevate mere competency to mastery; the work stands for generations.

* Stand proud&C consequences: resource:Elderwood +250; score:decisive_strike +1
-> hollow_caravan_outro

=== hollow_caravan_verse_16 ===
CRITICAL FAILURE! Overconfidence cracks foundations; the cost will take seasons to mend.

* Shoulder the burden&C consequences: housing:housing -25; population:population -20; score:shoddy_work +1; stat:morale -8
-> hollow_caravan_outro

=== hollow_caravan_verse_17 ===
RARE EVENT! A forgotten cache beneath the old road yields scarce materials the village sorely needs.

* Put it to use&C consequences: resource:Duskstone +80; score:unexpected_cache +1
-> hollow_caravan_outro

=== hollow_caravan_verse_18 ===
Time passes... The caravan fades into dust on the horizon. Yet, by some quiet mercy, a parcel remains at your gates at dawn.

* Open it together&C consequences: resource:Food +60; score:quiet_boon +1
-> hollow_caravan_outro

=== hollow_caravan_verse_19 ===
A temporary boon and a restraint ripple through your workshops as decisions are weighed.

* Temper the flow&C consequences: production_percent_section:Vital Resource +6; duration:sevenths:9
-> hollow_caravan_verse_20

=== hollow_caravan_verse_20 ===
The forge hums with a focused rhythm; elsewhere, supplies are rationed to protect reserves.

* Channel to Duskstone forges&C consequences: production_percent:Duskstone +12; duration:sevenths:11; click_power:Elderwood +3; duration:sevenths:5
-> hollow_caravan_verse_21

=== hollow_caravan_verse_21 ===
Efficiency mandates slow certain trades while the village adjusts to recent shifts.

* Restrict trade temporarily&C consequences: production_percent_section:Academia -7; duration:sevenths:7; click_power_percent_section:Old World Materials +15; duration:sevenths:4
-> hollow_caravan_outro

=== hollow_caravan_outro ===
The caravan has left its mark upon your village, for better or worse. Some speak of the knowledge gained, others of the price paid. But all agree that nothing will ever be the same again.

The wind carries the distant sound of wagon wheels, and somewhere beyond the horizon, the caravan continues its journey. Whether it brings hope or ruin to the next settlement depends on the choices made there, just as it did here.

* Return to the village
-> DONE

=== weeping_princess ===
# title: The Weeping Princess
# description: A mystical encounter with a sorrowful spirit who seeks redemption through the offering of sustenance and compassion.
# conditions: resource:Food >= 8
# consequences: score:weeping_princess_encounter +1
# cooldown: 3
# event_type:Mystical
# priority: 0

The forest grows darker as you venture deeper into its heart. Ancient trees whisper secrets to the wind, and the air itself seems to hold its breath. Then you hear it - a sound that pierces the silence like a blade through silk.

It is the sound of weeping.

* Follow the sound
-> weeping_princess_verse_1

=== weeping_princess_verse_1 ===
Through the twisted branches and hanging moss, you find a clearing where moonlight filters down like silver rain. There, beneath an ancient willow tree, sits a figure of ethereal beauty and profound sadness.

She is the Weeping Princess, a spirit bound to this place by sorrow and regret. Her tears fall like diamonds, each one carrying the weight of centuries of grief. As you approach, you notice her form seems to flicker like candlelight in the wind.

* Approach her
-> weeping_princess_chorus_1

=== weeping_princess_chorus_1 ===
The hush around you deepens, drawing in even the far-off tolling of bells until all that remains is the faint rasp of her breathing. Her voice comes not as speech, but as a question pressed into the marrow of your bones: *What penance will you carry?*

* Idealism. Accept the penance, we all need something to leave behind us.&D We all need something to leave behind us.&C pillar:aureus;strength:15;requirements:resource:Food >= 5;success:weeping_princess_verse_2;failure:weeping_princess_verse_3;crit_success:weeping_princess_verse_12;crit_failure:weeping_princess_verse_13;rare_event:weeping_princess_verse_14;rare_event_percent:5 -> weeping_princess_verse_2

* Realism. Do not, not every spirit needs salvation.&D Sometimes the greatest wisdom is knowing when not to interfere.&C requirements:score:weeping_princess_encounter >= 1;success:weeping_princess_verse_4;failure:weeping_princess_verse_5 -> weeping_princess_verse_4

* Pragmatism. We should seek her bow for respect, but nothing more. Let's keep rational.&D Balance is the key to understanding.&C pillar:waltz;strength:18;requirements:stat:morale >= 90;success:weeping_princess_verse_5;failure:weeping_princess_verse_6;crit_success:weeping_princess_verse_15;crit_failure:weeping_princess_verse_16;rare_event:weeping_princess_verse_17;rare_event_percent:3 -> weeping_princess_verse_5

=== weeping_princess_verse_2 ===
Your courage and hope have opened new possibilities that you never imagined. The golden light of your convictions illuminates the way forward, revealing hidden wonders that only the pure of heart can see. The Weeping Princess's tears begin to sparkle with hope rather than sorrow.

* Embrace the dream realm&C consequences: resource:Aetherlight +150; production_percent:Food +15; duration:sevenths:4; score:idealism_triumph +1; stat:morale +8; technology:Rites of Harvest enlightened
-> weeping_princess_outro

=== weeping_princess_verse_3 ===
The path was difficult, and your principles were tested. Yet through the struggle, you have grown stronger and more resolute. The shadows that challenged your ideals have taught you valuable lessons about the true nature of courage - it's not the absence of fear, but the willingness to face it.

* Learn from the experience&C consequences: population:population -25; score:idealism_lesson +1; stat:morale -3
-> weeping_princess_outro

=== weeping_princess_verse_4 ===
The path of Realism has shown you the true nature of the challenges ahead. Your practical thinking has uncovered solutions that others might have missed. The world reveals its secrets to those who approach it with clear eyes and steady hands. Sometimes the greatest wisdom is knowing when not to interfere.

* Apply your newfound wisdom&C consequences: score:realism_wisdom +1; production_percent_section:Academia +8; duration:sevenths:6
-> weeping_princess_outro

=== weeping_princess_verse_5 ===
The path of Pragmatism has led you to a place of perfect balance. Your ability to see all sides has created harmony where there was once discord. The world around you resonates with the peaceful energy of your balanced approach. The Weeping Princess finds solace in your measured response.

* Embrace the harmony&C consequences: resource:Food +200; score:balance_achieved +1; stat:morale +5; click_power:Elderwood +2; duration:sevenths:3
-> weeping_princess_outro

=== weeping_princess_verse_6 ===
The path was challenging, requiring you to constantly adjust your approach. Yet through this, you've gained wisdom about balance - sometimes the middle ground is the hardest place to stand, but it's often the most rewarding. The Weeping Princess appreciates your honest attempt, even if it wasn't perfect.

* Learn from the struggle&C consequences: score:balance_lesson +1; stat:morale -2
-> weeping_princess_outro

=== weeping_princess_verse_12 ===
CRITICAL SUCCESS! The power of your ideals resonates with ancient forces, creating a moment of perfect harmony. The Weeping Princess's transformation is complete, and the forest itself seems to sing with joy.

* Stand in awe&C consequences: resource:Aetherlight +300; score:perfect_idealism +1; stat:morale +12; production_percent:Food +25; duration:sevenths:8
-> weeping_princess_outro

=== weeping_princess_verse_13 ===
CRITICAL FAILURE! The weight of your ideals proves too heavy to bear. The shadows consume your light, and the Weeping Princess's sorrow deepens into despair.

* Face the consequences&C consequences: population:population -50; score:idealism_collapse +1; stat:morale -10; production_percent:Food -20; duration:sevenths:5
-> weeping_princess_outro

=== weeping_princess_verse_14 ===
RARE EVENT! A forgotten shrine hidden in the willow's roots reveals itself, offering ancient knowledge and precious materials that have been waiting for the right moment to emerge.

* Accept the boon&C consequences: resource:Duskstone +120; score:ancient_boon +1; stat:morale +6; technology:Rites of Harvest enlightened
-> weeping_princess_outro

=== weeping_princess_verse_15 ===
CRITICAL SUCCESS! Your balanced approach has unlocked the secrets of the forest itself. The Weeping Princess becomes a guardian spirit, watching over your village with benevolent power.

* Embrace the guardian&C consequences: resource:Food +400; score:guardian_awakened +1; stat:morale +15; production_percent_section:Vital Resource +12; duration:sevenths:10
-> weeping_princess_outro

=== weeping_princess_verse_16 ===
CRITICAL FAILURE! Your attempt at balance has disrupted the natural order. The forest's harmony is shattered, and the consequences ripple through your village.

* Accept responsibility&C consequences: housing:housing -30; score:balance_disrupted +1; stat:morale -8; production_percent:Food -15; duration:sevenths:4
-> weeping_princess_outro

=== weeping_princess_verse_17 ===
RARE EVENT! The Weeping Princess's gratitude manifests as a blessing that affects all who dwell in your village, creating a period of unprecedented prosperity.

* Receive the blessing&C consequences: score:prosperity_blessing +1; stat:morale +10; production_percent_section:Academia +15; duration:sevenths:7; click_power_percent_section:Old World Materials +20; duration:sevenths:5
-> weeping_princess_outro

=== weeping_princess_outro ===
The mystical forest begins to brighten as the Weeping Princess's sorrow transforms. Whether through your courage, wisdom, or balance, you have helped her find a path forward. The ancient trees whisper their approval, and the air itself seems lighter.

The encounter has left its mark upon your village, for better or worse. Some speak of the mystical energies gained, others of the price paid. But all agree that nothing will ever be quite the same again.

* Continue your journey
-> DONE