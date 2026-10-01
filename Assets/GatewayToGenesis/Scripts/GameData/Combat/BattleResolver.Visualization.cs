using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        /// <summary>
        /// Rehearsal: compose and perform a proposed sequence on an independent copy (Score, Tracks, Chords, Wards, piles,
        /// Skirmishes and random streams), so the live battle never consumes time, cards or randomness. A replacement hand
        /// after a rehearsed Redraw Bell stays unknown.
        /// </summary>
        public BattleSequencePreview Preview(IEnumerable<BattleCommand> sequence)
        {
            var result = new BattleSequencePreview { before = Visualize(), failures = new List<string>() };
            if (rhythm != null || pendingCadence != null)
            { result.failures.Add("Finish the rhythm phrase before rehearsing another Score."); result.after = Visualize(); return result; }
            var copy = SnapshotCopy();
            bool redrawn = false;
            foreach (var command in sequence ?? Array.Empty<BattleCommand>())
            {
                if (redrawn && (command.kind == BattleCommandKind.Card || command.kind == BattleCommandKind.Chord))
                { result.failures.Add("A replacement hand is unknown until the Redraw Bell is committed."); break; }
                string why;
                switch (command.kind)
                {
                    case BattleCommandKind.Card: why = copy.CommitCard(command.attacker, command.handIndex, command.target, command.rendition, command.beat); break;
                    case BattleCommandKind.Chord: why = copy.CommitChord(command.attacker, command.handIndex, command.minors, command.target, command.rendition, command.beat,
                        command.minorBeats.Count > 0 ? command.minorBeats : null); break;
                    case BattleCommandKind.Movement: why = copy.CommitMovement(command.attacker, command.section, command.hex); break;
                    case BattleCommandKind.Path: why = copy.ComposePath(command.attacker, command.section, command.path, command.stepBeats.Count > 0 ? command.stepBeats : null); break;
                    case BattleCommandKind.Redraw: why = copy.Redraw(command.attacker); redrawn = why == null; break;
                    case BattleCommandKind.Order: why = copy.Order(command.attacker, command.section, command.order); break;
                    case BattleCommandKind.Retract: why = copy.Retract(command.attacker, command.chord); break;
                    case BattleCommandKind.Stabilize: why = copy.Stabilize(command.attacker, command.chord, command.target, command.beat); break;
                    case BattleCommandKind.Ground: why = copy.Ground(command.attacker, command.chord); break;
                    case BattleCommandKind.Seer: why = copy.DesignateSeer(command.attacker, command.section); break;
                    case BattleCommandKind.OverbeatCard: why = copy.CommitOverbeat(command.attacker, command.handIndex, command.target, command.chord); break;
                    case BattleCommandKind.OverbeatStep: why = copy.CommitOverbeatStep(command.attacker, command.section, command.hex); break;
                    case BattleCommandKind.ClimaxMajor: why = copy.CommitClimaxMajor(command.attacker, command.handIndex, command.target, command.fuse); break;
                    case BattleCommandKind.GrandResolution: why = copy.CommitGrandResolution(command.attacker, command.section); break;
                    default: why = copy.Perform(command.beats); break;
                }
                if (why != null) { result.failures.Add(why); break; }
            }
            result.after = copy.Visualize();
            result.plays = copy.report.plays;
            result.spatialEvents = copy.report.spatialEvents;
            result.chordEvents = copy.report.chordEvents;
            result.harmonicInteractions = new Dictionary<string, int>(copy.report.matchups);
            return result;
        }

        private BattleRun SnapshotCopy()
        {
            var setup = new BattleSetup { attacker = a.side, defender = d.side, field = field, seed = report.seed, majorEncounter = requiresManual }.Clone(report.seed);
            setup.field.hexes = Spatial.Terrain.Select(x => x.Clone()).ToList();
            var copy = new BattleRun(setup, combatSettings, auto);
            CombatSection Map(CombatSection unit) => unit == null ? null : copy.Of(ownerOf[unit].attacker).side.sections[ownerOf[unit].side.sections.IndexOf(unit)];
            Side MapSide(Side s) => s == null ? null : copy.Of(s.attacker);
            DeckCard CopyDeck(Side source, DeckCard card) => card == null ? null : new DeckCard
            {
                card = card.card.Clone(), voice = card.voice, scale = card.scale, major = card.major, source = card.source,
                legend = card.legend == null ? null : card.legend == source.side.conductor ? MapSide(source).side.conductor :
                    source.side.sections.Where(x => x.leader == card.legend).Select(x => Map(x).leader).FirstOrDefault() ?? card.legend.Clone(),
            };
            Track MapTrack(Track track) => track == null ? null : copy.tracks[(track.side.attacker, track.voice)];
            foreach (var source in new[] { a, d })
            {
                var target = MapSide(source);
                for (int i = 0; i < source.side.sections.Count; i++)
                {
                    var before = source.side.sections[i]; var after = target.side.sections[i];
                    after.committed = before.committed; after.fled = before.fled; after.captured = before.captured; after.destroyed = before.destroyed;
                    after.mindBroken = before.mindBroken; after.lost = before.lost; after.dead = before.dead; after.wounded = before.wounded;
                    after.deathKnell = before.deathKnell; after.permanentDeath = before.permanentDeath; after.evacuated = before.evacuated;
                    after.deathblowChecks = before.deathblowChecks; after.severeWounds = before.severeWounds; after.parasiticStrain = before.parasiticStrain;
                    after.casts = before.casts; after.misfires = before.misfires; after.timesMindBroken = before.timesMindBroken;
                    after.battleHex = before.battleHex;
                }
                target.bar = source.bar; target.barMax = source.barMax; target.conductorBroken = source.conductorBroken; target.conductorFallen = source.conductorFallen;
                target.maladaptation = source.maladaptation; target.withdrawFloor = source.withdrawFloor;
                target.conductor = source.conductor == null ? null : target.side.conductor;
                target.stack = source.stack.Clone(); target.leaders.Clear();
                foreach (var pair in source.leaders) target.leaders[Map(pair.Key)] = pair.Value.Clone();
                target.rally = source.rally; target.recon = source.recon; target.woundedShare = source.woundedShare; target.entrench = source.entrench;
                target.surge = source.surge; target.crescendo = source.crescendo; target.entrenched = source.entrenched; target.sure = source.sure;
                target.ostinato = source.ostinato; target.initiative = source.initiative; target.misfiresLogged = source.misfiresLogged;
                target.startIntegrity.Clear(); foreach (var pair in source.startIntegrity) target.startIntegrity[Map(pair.Key)] = pair.Value;
                target.startComposure = source.startComposure;
                target.leftField.Clear();
                foreach (var pair in source.leftField)
                {
                    var legend = pair.Key == source.side.conductor ? target.side.conductor :
                        source.side.sections.Where(x => x.leader == pair.Key).Select(x => Map(x).leader).FirstOrDefault() ?? pair.Key;
                    target.leftField[legend] = pair.Value;
                }
                if (source.perf == null) { target.perf = null; continue; }
                var p = source.perf;
                var next = new Performance { deck = p.deck.Select(card => CopyDeck(source, card)).ToList(), rng = p.rng.Clone(), handCapacity = p.handCapacity, handSize = p.handSize, bonusDraw = p.bonusDraw,
                    cardScale = p.cardScale, played = p.played, flickers = p.flickers, logged = p.logged,
                    setups = p.setups, plannedSetups = p.plannedSetups, plannedSurge = p.plannedSurge, plannedCrescendo = p.plannedCrescendo, plannedSure = p.plannedSure };
                next.draw.AddRange(p.draw); next.hand.AddRange(p.hand); next.discard.AddRange(p.discard); next.exhausted.UnionWith(p.exhausted);
                foreach (var pair in p.tally) next.tally[pair.Key] = pair.Value;
                foreach (var pair in p.plannedGuard) next.plannedGuard[Map(pair.Key)] = pair.Value;
                foreach (var pair in p.plannedMend) next.plannedMend[Map(pair.Key)] = pair.Value;
                foreach (var pair in p.plannedRally) next.plannedRally[Map(pair.Key)] = pair.Value;
                foreach (var pair in p.plannedExpose) next.plannedExpose[Map(pair.Key)] = pair.Value;
                next.plannedPush.UnionWith(p.plannedPush.Select(Map));
                target.perf = next;
            }
            foreach (var pair in crises)
            {
                var state = pair.Value; var cloned = new Crisis { active = state.active, cadenzaReady = state.cadenzaReady, measure = state.measure, resolved = state.resolved };
                cloned.held.AddRange(state.held); cloned.counted.UnionWith(state.counted); copy.crises[MapTrack(pair.Key)] = cloned;
            }
            copy.steadied.UnionWith(steadied.Select(Map));
            copy.phantoms.AddRange(phantoms.Select(p => p.Clone()));
            copy.veils.AddRange(veils.Select(v => new Veil { attacker = v.attacker, hex = v.hex, strength = v.strength, expires = v.expires }));
            foreach (var pair in tracks)
            {
                var from = pair.Value; var to = copy.tracks[pair.Key];
                to.order = from.order; to.pathComposed = from.pathComposed; to.spotlit = from.spotlit; to.rushing = from.rushing; to.charging = from.charging;
                to.detuned = from.detuned; to.attuned = from.attuned; to.duet = from.duet; to.overrode = from.overrode;
                to.fever = from.fever; to.feverExtra = from.feverExtra; to.perfectPhrases = from.perfectPhrases;
                to.path.Clear(); to.path.AddRange(from.path); to.steps.Clear(); to.steps.AddRange(from.steps); to.stepBeats.Clear(); to.stepBeats.AddRange(from.stepBeats);
            }
            var chordMap = new Dictionary<Chord, Chord>();
            Chord CloneChord(Chord c)
            {
                var cloned = new Chord { id = c.id, side = MapSide(c.side), owner = MapTrack(c.owner), core = CopyDeck(c.side, c.core), target = Map(c.target), majorBeat = c.majorBeat,
                    channel = c.channel, firstBeat = c.firstBeat, origin = c.origin, interference = c.interference, fermata = c.fermata, stabilizations = c.stabilizations,
                    criticality = c.criticality, hyperMeasures = c.hyperMeasures, seed = c.seed, composedMeasure = c.composedMeasure, footing = c.footing, legato = c.legato,
                    staccato = c.staccato, drilled = c.drilled, spell = c.spell, ward = c.ward, magical = c.magical, committed = c.committed, anchored = c.anchored, feverExtra = c.feverExtra, echoShed = c.echoShed,
                    state = c.state, power = c.power, heartbeatShare = c.heartbeatShare, heartbeatRendition = c.heartbeatRendition, stanceHeld = c.stanceHeld, releaseCancelled = c.releaseCancelled, overbeat = c.overbeat,
                    climax = c.climax, grand = c.grand, support = c.support };
                foreach (var n in c.notes) cloned.notes.Add(new ChordNote { card = n == c.Major ? cloned.core : CopyDeck(c.side, n.card), track = MapTrack(n.track), beat = n.beat,
                    handIndex = n.handIndex, origin = n.origin, kind = n.kind, rendition = n.rendition, sounded = n.sounded, failed = n.failed, flicker = n.flicker,
                    measured = n.measured, missApplied = n.missApplied, failure = n.failure, overbeat = n.overbeat, heard = n.heard, from = n.from, target = Map(n.target) });
                cloned.merged.AddRange(c.merged.Select(CloneChord));
                chordMap[c] = cloned;
                return cloned;
            }
            foreach (var pair in chords) copy.chords[pair.Key] = CloneChord(pair.Value);
            Chord MapChord(Chord c) => c == null ? null : chordMap.TryGetValue(c, out var mapped) ? mapped : null;
            Queued CopyQueued(Side side, Queued q) => new Queued { index = q.index, target = Map(q.target), frozen = CopyDeck(side, q.frozen), origin = q.origin, rendition = q.rendition,
                seed = q.seed, action = q.action, stanceHeld = q.stanceHeld, explicitChord = q.explicitChord, reacting = q.reacting, chord = MapChord(q.chord), ward = MapChord(q.ward) };
            copy.reactions.AddRange(reactions.Select(r => new ArmedReaction { id = r.id, attacker = r.attacker, fired = r.fired, measure = r.measure,
                track = MapTrack(r.track), preparation = CopyQueued(Of(r.attacker), r.preparation) }));
            foreach (bool attacker in new[] { true, false }) copy.tempo[attacker] = tempo[attacker].Clone();
            copy.sympathetic.UnionWith(sympathetic.Select(MapTrack)); copy.sympatheticFall.UnionWith(sympatheticFall.Select(MapTrack));
            copy.pendingOverbeat = pendingOverbeat;
            foreach (var pair in overbeatSteps) copy.overbeatSteps[MapTrack(pair.Key)] = pair.Value;
            copy.overbeatReactions.AddRange(overbeatReactions.Select(r => (MapTrack(r.track), CopyQueued(r.track.side, r.preparation))));
            copy.report.rhythm.AddRange(report.rhythm.Select(r => new BattleRhythmRecord { measure = r.measure, beat = r.beat, attacker = r.attacker, abjuration = r.abjuration,
                overbeat = r.overbeat, start = r.start, end = r.end, options = r.options?.Clone(), inputs = r.inputs.Select(i => new BattleRhythmInput { lane = i.lane, dspTime = i.dspTime }).ToList(),
                results = r.results.Select(x => new BattleRhythmResult { action = x.action, voice = x.voice, note = x.note, target = x.target, hits = x.hits, missed = x.missed, extra = x.extra,
                    grade = x.grade, rendition = x.rendition, playable = x.playable, assisted = x.assisted }).ToList() }));
            foreach (var signal in reactionSignals)
            {
                var copied = new ReactionSignal { trigger = signal.trigger, attacker = signal.attacker, actor = Map(signal.actor), target = Map(signal.target), action = signal.action, depth = signal.depth };
                copied.chain.UnionWith(signal.chain.Select(Map));
                copy.reactionSignals.Enqueue(copied);
            }
            copy.reactionsFired.UnionWith(reactionsFired); copy.emergencyBonds.UnionWith(emergencyBonds); copy.witnessedFalls.UnionWith(witnessedFalls);
            foreach (var pair in pollutionDrawn) copy.pollutionDrawn[MapSide(pair.Key)] = new HashSet<int>(pair.Value);
            copy.nextReaction = nextReaction; copy.reactionsThisBeat = reactionsThisBeat; copy.actingVoice = Map(actingVoice); copy.actingAction = actingAction;
            copy.marks.Clear();
            foreach (var pair in marks)
            {
                var mark = pair.Value;
                var cloned = new CardMarks { expose = mark.expose, exposeLeft = mark.exposeLeft, blind = mark.blind, blindLeft = mark.blindLeft, burn = mark.burn, burnLeft = mark.burnLeft };
                cloned.wards.AddRange(mark.wards.Select(g => new GuardLink { source = Map(g.source), range = g.range, amount = g.amount, raised = g.raised, expires = g.expires,
                    physical = g.physical, magical = g.magical, binding = g.binding, card = g.card, chord = MapChord(g.chord), rendition = g.rendition }));
                copy.marks[Map(pair.Key)] = cloned;
            }
            copy.skirmishes.Clear();
            foreach (var sk in skirmishes)
            {
                var cloned = new Skirmish { hex = sk.hex, seam = sk.seam, contactBeat = sk.contactBeat, aEntered = sk.aEntered, dEntered = sk.dEntered, aLost = sk.aLost, dLost = sk.dLost };
                cloned.a.UnionWith(sk.a.Select(Map)); cloned.d.UnionWith(sk.d.Select(Map));
                copy.skirmishes.Add(cloned);
            }
            foreach (var pair in collided) copy.collided[MapTrack(pair.Key)] = pair.Value;
            foreach (var pair in groundAt) copy.groundAt[pair.Key] = pair.Value;
            foreach (var pair in anchors) copy.anchors[pair.Key] = pair.Value;
            copy.interruptedThisBeat.UnionWith(interruptedThisBeat);
            foreach (var pair in behavior) copy.behavior[MapSide(pair.Key)] = new Dictionary<string, int>(pair.Value);
            copy.composureAtOpening.Clear(); foreach (var pair in composureAtOpening) copy.composureAtOpening[Map(pair.Key)] = pair.Value;
            copy.emitted.UnionWith(emitted); copy.panicked.UnionWith(panicked.Select(Map));
            copy.brokenStances.Clear(); copy.brokenStances.UnionWith(brokenStances.Select(MapSide));
            Array.Copy(restA, copy.restA, restA.Length); Array.Copy(restD, copy.restD, restD.Length);
            copy.ringsA = ringsA; copy.ringsD = ringsD; copy.beat = beat; copy.scoresCommitted = scoresCommitted; copy.nextChord = nextChord;
            copy.rng = rng.Clone(); copy.m = m; copy.end = end; copy.open = open; copy.Over = Over;
            copy.finished = finished; copy.finishing = finishing; copy.Phase = Phase;
            copy.aLeft = aLeft; copy.dLeft = dLeft; copy.handA = handA; copy.handD = handD; copy.defenderDefense = defenderDefense;
            copy.report.log.Clear(); copy.report.timeline.Clear(); copy.report.phases.Clear(); copy.report.commitments.Clear();
            copy.report.chordEvents.Clear(); copy.report.spatialEvents.Clear(); copy.report.plays.Clear();
            return copy;
        }
    }
}
