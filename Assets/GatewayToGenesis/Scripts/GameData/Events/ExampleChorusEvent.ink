# Example Chorus Event
# This demonstrates the new Chorus Screen system with metadata tags and alpha transitions

# screen_flow: splash,verse,chorus,outro
# event_type: Mystical
# priority: 5

=== start ===
# conditions: score:quest_progress >= 1
# consequences: score:quest_progress +1; score:path_chosen +1

You find yourself at a mystical crossroads, where three distinct paths stretch into the misty distance. Each path seems to call to a different aspect of your soul.

* [Continue to the crossroads] -> chorus

=== chorus ===
The paths before you shimmer with ethereal light, each calling to a different part of your being. The air crackles with magical energy as you contemplate your choice.

* Idealism. Follow your heart and embrace the power of hope and dreams. #pillar:aureus;strength:15;requirements:score:quest_progress >= 1;success:weeping_princess_verse_1;failure:weeping_princess_verse_2 -> idealism_challenge
* Realism. Consider the practical consequences and make a measured decision. #requirements:score:quest_progress >= 1 -> weeping_princess_verse_3
* Pragmatism. Find the middle ground and balance all considerations. #pillar:waltz;strength:18;success:weeping_princess_verse_4;failure:weeping_princess_verse_5 -> pragmatism_challenge

=== idealism_challenge ===
The path of Idealism glows with golden light, but shadows dance at its edges. Your heart guides you, but will your courage be enough?

* [Face the challenge] -> idealism_resolution

=== idealism_resolution ===
* [Continue] -> END

=== weeping_princess_verse_1 ===
The path of Idealism has brought you to a realm where dreams take physical form. Your courage and hope have opened new possibilities that you never imagined. The golden light of your convictions illuminates the way forward, revealing hidden wonders that only the pure of heart can see.

* [Embrace the dream realm] -> outro

=== weeping_princess_verse_2 ===
The path was difficult, and your principles were tested. Yet through the struggle, you have grown stronger and more resolute. The shadows that challenged your ideals have taught you valuable lessons about the true nature of courage - it's not the absence of fear, but the willingness to face it.

* [Learn from the experience] -> outro

=== weeping_princess_verse_3 ===
The path of Realism has shown you the true nature of the challenges ahead. Your practical thinking has uncovered solutions that others might have missed. The world reveals its secrets to those who approach it with clear eyes and steady hands.

* [Apply your newfound wisdom] -> outro

=== pragmatism_challenge ===
The path of Pragmatism shimmers with harmonious light, but maintaining balance is never easy.

* [Face the challenge] -> pragmatism_resolution

=== pragmatism_resolution ===
* [Continue] -> END

=== weeping_princess_verse_4 ===
The path of Pragmatism has led you to a place of perfect balance. Your ability to see all sides has created harmony where there was once discord. The world around you resonates with the peaceful energy of your balanced approach.

* [Embrace the harmony] -> outro

=== weeping_princess_verse_5 ===
The path was challenging, requiring you to constantly adjust your approach. Yet through this, you've gained wisdom about balance - sometimes the middle ground is the hardest place to stand, but it's often the most rewarding.

* [Learn from the struggle] -> outro

=== outro ===
The mystical crossroads fade behind you as you continue your journey. The path you chose has changed not only your immediate circumstances, but the very fabric of your destiny.

* [Continue your journey] -> END 