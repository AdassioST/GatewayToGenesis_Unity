# Save identities, lifetime achievements and sacrifice

Implemented 2026-09-26. The user's request lifts the earlier S01 hold.

## Player flow

The game scene now opens behind a paused main menu. Continue, create world, load, recover the previous backup, delete and quit are available. Escape opens the menu during play. New universes receive a cryptographically random identity and seed, are durably saved, then emerge from black through a 3.5-second expanding cosmic light. Existing worlds do not replay creation. The HUD displays Resonance Anchors and opens the menu when selected.

Manual saves, two-minute autosaves during play, suspend saves and quit saves share the same writer. Saving during an active story is deliberately refused; finish the story first. Pending notifications, Ink variables and completed stories are saved. Failed saves surface an error; a failed load stays paused and does not overwrite the original slot. Backup recovery is explicit rather than silently rolling progress back. New/load switches save the current world first.

Lifetime achievements live in an independent profile. Each world has its own unlocked IDs, immutable per-achievement award amounts and spent Anchor total. An unlock awards once in that world even if another world already earned it. Loading never awards again. The provisional default is **10 Anchors**, configurable in `Assets/Resources/Saves/AnchorRewards.asset`, with per-achievement overrides. `SaveSession.SpendAnchors` persists a debit before reporting success. Lifetime imports do not award world currency. The menu lists lifetime unlocks and distinguishes those earned in the active world.

## Storage contract

Under `Application.persistentDataPath`:

- `Saves/<world-guid>.arc`: authenticated encrypted version-1 snapshot.
- `Saves/<world-guid>.arc.bak`: previous completed write.
- `Saves/profile.arc`: lifetime achievement union and retired identities; also has a backup.
- `Saves/identity.key`: random encryption/authentication key protected by Windows user-scoped DPAPI.
- `retired-worlds.arc`: legacy retirement ledger, retained for compatibility outside the slot directory.
- `retired-worlds.arc.cinder`, `.tide`, `.iris`: three authenticated retirement witnesses. Each encrypts its own domain tag and the union of sacrificed world IDs. A surviving valid witness repairs missing, corrupt or stale siblings. Neutral labels are cosmetic naming, not cryptographic protection.

The serial encrypts and authenticates the world GUID, creation time, seed, generator version and catalog/stencil fingerprint. Save payloads use AES-CBC with random IVs and encrypt-then-HMAC-SHA256, with separate encryption/MAC key halves. Authentication precedes decryption. No universal embedded key exists. Atomic writes flush a temporary file and replace the destination while retaining its predecessor. Unsupported schema, missing content, changed generator/catalog, unknown keys, corruption and missing identity keys fail closed.

Windows is currently the supported key-protection target. A different operating system needs a platform keystore adapter. Copying the protected key to a different Windows account does not make it readable; a future explicit portable profile/export workflow needs its own credential protection. This version does not claim cross-account/cloud migration.

`GameSnapshot.Schema` explicitly selects game fields; the codec never creates a type selected by a file. It preserves resources/buildings, research payments, population/deaths/housing, stats and source ledgers, council assignments/activation/cooldowns, civics/penalties, legends, calendar, Age queue/history/crisis, expeditions, tile knowledge/authority/magic residue, event scores/cooldowns/consequences, Ink state and weather timers. Unity object references resolve through content names; unavailable/ambiguous content rejects loading. The world is regenerated from its bound seed and then receives saved mutations. Cached exploration, production and presentation are refreshed without replaying unlock/discovery actions. Cosmetic villagers are recreated rather than saving their individual random positions.

## Final sacrifice and the continuing ending

The final-choice integration declares `EXTERNAL SacrificeWorld()` in Ink and invokes it only after the player chooses sacrifice. Its binding requires `age-of-the-end`, does nothing in previews, and calls `SaveMenu.CompleteSacrifice()` (which delegates to `SaveSession.Sacrifice`). `EXTERNAL GetResonanceAnchors()` exposes the wallet to story queries. Ordinary slot deletion is not sacrifice. The Age of The End story is not yet authored as a playable final-choice sequence; this API is its integration point, not an exposed debug button.

Sacrifice commits the retired identity and lifetime unlocks first, including `the-purest-of-all-love`. It then removes the active slot, its managed backups and all other authenticated matching copies in the managed Saves directory, including renamed copies. The three witnesses and legacy ledger are merged into the profile, so removing the original save or recovering an older profile alone does not erase the history. All three witnesses must be committed before sacrifice deletes managed saves. A crash after commitment may leave a copy, which is detected by the cosmetic check.

The menu scans every two seconds. A valid sacrificed world or backup found under any filename in the managed Saves directory replaces **THE RELIC OF ARCANORIA** title presentation with **THE PUREST OF LOVE**, wounded wings/eyes and “You are mine, and mine alone.” This is cosmetic only: scanning does not open or pause the menu, hide controls, block Escape, or prohibit loading/saving a restored world. Every normal menu control remains available. Removing all matching files restores the regular title on the next scan. Retirement records remain for detecting a later return, but retirement history alone never activates the cosmetic. A restored world being played can create another matching file when saved, activating the artwork again.
Scope is the game's managed saves. No unrelated folders, external drives, cloud backup histories or installation files are searched or deleted. In particular, the lore's self-uninstall passage is not implemented. A local client cannot prevent a user with control of the computer from patching it or restoring **all** saves, keys and retirement records. An external authoritative record would be needed to resist complete offline rollback; this implementation does not claim that guarantee.

## Verification and remaining acceptance

The standalone harness passed 20 checks: identity uniqueness/binding, tampering, unsafe paths, atomic backup replacement, renamed-copy detection/removal/reappearance, reward idempotency and spending, lifetime union, schema field resolution, nested reward/ledger and readonly Age-beat round trips. Full game C# compilation passes. Editor NUnit tests additionally cover the sacrifice transaction and continued detection of a restored copy.

Unity reports no valid Editor license on this machine. Therefore the NUnit sacrifice integration test, real scene round trip, first-birth animation, window suspend/quit and final-screen visuals have not been run in Unity. Before release, run `SaveTests` plus scene acceptance with a researched technology in progress, active council/civic/weather timers, a pending story, changed-Age magic and explored territory. Test storage-full/access-denied interruption and retirement crash points on the target filesystem. Do not mark final-ending content, cross-device migration, Ironman or Play Mode acceptance complete based on the standalone checks.

## Cosmetic-only revision verification

The current change adds independent record-domain authentication and union/repair tests for a missing witness, a corrupted witness and a valid witness substituted into another position. The storage harness passes 24 checks. Modified save/menu files compile against the previous successful game assembly. Full current-project compilation is separately blocked by world-map references to missing Grandfield, ThreatSite, Settlement, TradeRoute and Enclave definitions. Those unrelated edits were not changed. Unity visual verification still requires an active Editor license.