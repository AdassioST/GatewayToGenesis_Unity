# Symphony of War playable validation

This is the remaining release checklist, not a record of completed native testing. Unity 6000.5.8f1 currently exits 198 with **No valid Unity Editor license found**. Offline rule checks do not validate scene imports, rendered UI, audio devices, physical input or frame timing.

## Reproduce the verified rule checks

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CombatVerification/Verify.ps1 -World
```

Expected result: **474 passed, zero failed, one native Resources-loader skip**, compiling 440 runtime sources and 130 Editor/test sources. Evidence is written to `Logs/symphony-verify`. The harness runs real NUnit assertions against project code. It does not run UnityTest coroutines.

## Licensed Editor checks

1. Open the project in its configured Unity version. Let it import all new scripts/metadata; inspect Console for errors. Run the combat, world, save, legend and content EditMode suites, including `BattleTempoTests`, `BattleRhythmTests` and `BattleRhythmPlayTests`. The formerly skipped real-bestiary test must run here.
2. Open `ClickerScreen`, enter Play mode and use **Tools > Gateway to Genesis > Combat > Rhythm Rehearsal**. Listen/echo with numbered keys and clickable lanes, then answer the Cadence. Verify Miss/Clean/Perfect feedback and that lanes remain clickable above the encounter Canvas.
3. Check Options > Audio at default settings, latency offsets in both directions, narrow/wide windows and Clean assistance. Check actual DSP scheduling with the chosen speakers/headphones. Assistance must not ignite Fever; repeated taps/listen taps must not earn Perfect.
4. Trigger player world contact. Confirm strategic time pauses, the original forecast remains stable, eligible auto/manual choices appear, and boss/major/decisive/Original Eight flags force manual play. Confirm accepted aftermath is applied once and the previous pause state is restored.
5. Compose a four-Beat Measure: three Spotlit Tracks, legal path/arrival Beats, Standing Orders, a Core with ordered Minors, early Ward, Redraw Rest, Ensemble and Rehearsal. Verify the sixteen-hex layout, text wrapping, legal-target selection, held/suspended work and Ground/Stabilize controls at the target display size.
6. Fill the four Fever pips with Perfect sequences (two Measures against an enemy attacking every Cadence, four without attacks); Fever ignites at that Assessment at 25%. Compose an Overbeat strike, Minor, Step or Reaction on a synchronized Spotlit Track: it appears between Beats 3 and 4, without another downstroke, Attrition tick, Countdown tick or Ward-duration increase. One Clean/Missed Measure must Falter (percentage held, Overbeat kept); a flawless one steadies it; two imperfect Measures in a row break it at Assessment. Losing synchronization must leave the Overbeat choice unresolved.
7. Climb to 60%: the Spotlit Tracks show Sympathetic, their windows widen and Hold rises by one. Collapse a Sympathetic working: every Sympathetic Track takes the backlash, falls to Detuned, loses its Overbeat/Ensemble Notes, and the Fever shatters. Perform each Climax composition at its threshold (Unison 60, Dyad 65, Ensemble 70, Linked 75, Fused Finale 80, Linked Finales 85, Grand Resolution 90), confirm the Resolution ends the Fever at Assessment, linked workings collapse together, and the Resolution appears in the report and the performers' deeds. Listen for the music cue list once audio stems exist.
8. Test Mind Break for Legends with different traits (each maladaptive face has its own two compulsions and its own Cadenza), future corrupted choices, same-Measure Gambit, Cadenza collateral and helper Co-Regulation (the patient shows Steadied and its compulsions lose collateral) while enemy Countdowns advance. Break an aggressive, a fearful and a controlling Conductor and watch the formation Tracks it no longer composes; break a Conductor's Gambit and watch the army recover. Break ordinary sections of 5, 50 and 2,000 (freeze, panic, rout) and a broken section held in contact below 60% Integrity (surrender). Test Death Knell, repeated harm, Saving Grace, evacuation, Checkmate and healthy-body capture. Inspect all six population fate categories and named biographies.
9. Prepare unlocked, resource-funded premonitions before contact. Lose three attempts, saving/reloading between them; verify untouched original roster/resources/souls and decreasing charges. The fourth loss commits. Reload the same boss boundary without recharge. Check the retry achievement only for qualifying Atonalis evidence.
10. Save/reload during Composition, an active echo, an active Overbeat, a composed Climax/Grand Resolution, the Cadence fermata, an inter-Beat crisis decision and after Assessment. Verify elapsed phrase time, measured inputs, piles, positions, Score, prepared Wards, crisis state, Fever and immutable forecast. Repeat with an older campaign save missing the new optional world fields.
11. Return a traumatized expedition to genuine home territory. Verify pollution/trauma clearing, restored body/Clouded baseline, selection of at most two new qualifying Opus techniques, preservation of old Opus and biography entries. A field rest must retain trauma.

## Scale scenarios and performance

Use authored encounter fixtures at **2, 50, 500, 5,000 and 20,000 combatants**, preserving named elites separately. These represent Elite, Micro, Formation, Medium and Grand scales. Complete both eligible auto and manual runs with matched Clean decisions and compare state/fates. Verify two elites losing one produces Pyrrhic, twelve ordinary losses among 20,000 can remain Decisive, and permanent conductor loss weighs heavily. Verify Mythical Decisive/Close/Pyrrhic retain their base casualty category.

Record composition/Rehearsal/Commit/Beat/Assessment frame times and peak allocations in the Unity Profiler, plus save payload size and replay duration. Aggregation must follow tactical sections and elite pieces, rather than allocating a Track for every ordinary soldier. No native performance budget is claimed by this continuation; record measurements before setting release targets.

## Explicit extensions before full-reference sign-off

The Tempo Fever rules, including Sympathetic Resonance and every Climax composition, are now implemented as runtime rules; **adaptive audio stems** for the music cues are not. Soul-Key Refusal cards for Mind Broken Legends remain open. **Sacrifice Choice and Original Eight miracles**, full Clarity weak-point/pivot/projection information, cross-working resolution guarantees, all soul-key override/refusal rules and the required authored late-game content also remain open. Use the 133-row section coverage matrix to track these separately from native validation.
