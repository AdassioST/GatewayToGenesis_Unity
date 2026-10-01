# Combat verification

Run from the Unity project with its generated `Assembly-CSharp*.csproj` files, installed Editor references and package-cache assemblies available:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CombatVerification/Verify.ps1 -World
```

This compiles current runtime sources (including new files not yet in generated projects), the Editor/test assembly, and runs actual NUnit assertions without native Unity. `-World` adds pursuit, unit, civilization and generation integration suites. Omit it for the focused combat suites. `-EditorPath` can override the installation directory ending in `Editor`. Evidence is written to ignored `Logs/symphony-verify`.

The harness explicitly skips the existing real-bestiary Resources-loader case. It does not run UnityTest coroutines or verify audio, UI rendering, native serialization, scene imports or physical input latency. Those require a licensed Editor.

Run `BattleRhythmTests` and `BattleRhythmPlayTests` in the Editor Test Runner's EditMode tab. For manual playback, open `ClickerScreen`, enter Play mode, then select **Tools → Gateway to Genesis → Combat → Rhythm Rehearsal**. Listen, then echo with numbered keys or clickable lanes; the later fermata answers the hostile release. Options → Audio provides latency, window width and Clean assistance.

The harness currently compiles 440 runtime sources and 130 Editor/test sources and passes 474 cases with one native Resources skip. `BattleTempoTests` covers the full Tempo Fever reference (pips, Falter, Sympathetic Resonance, Climax compositions, Grand Resolution); `BattleCrisisTests` covers the completed SOW-07.

Implementation, all 133 design-section coverage rows and the licensed-Editor release checklist are in `Docs/Planning/SYMPHONY_OF_WAR_IMPLEMENTATION.md`, `SYMPHONY_OF_WAR_COVERAGE.csv` and `SYMPHONY_OF_WAR_PLAYABLE_VALIDATION.md`.
