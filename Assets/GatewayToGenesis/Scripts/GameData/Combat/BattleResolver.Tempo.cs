using System;
using System.Collections.Generic;
using System.Linq;

// Tempo Fever (vault: Combat System.md, "Tempo Fever — Crescendo and Resolution"): the Overbeat between Beat 3 and the
// Cadence, Sympathetic Resonance in Resolution Climax, and the Climax compositions a Resolution spends.
public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        private readonly Dictionary<bool, BattleTempoFever> tempo = new Dictionary<bool, BattleTempoFever> {
            [true] = new BattleTempoFever(), [false] = new BattleTempoFever() };
        private readonly Dictionary<Track, int> overbeatSteps = new Dictionary<Track, int>();
        private readonly List<(Track track, Queued preparation)> overbeatReactions = new List<(Track, Queued)>();
        /// <summary>Tracks ringing as one instrument in Resolution Climax, and Tracks a Sympathetic Collapse threw to Detuned until Assessment.</summary>
        private readonly HashSet<Track> sympathetic = new HashSet<Track>(), sympatheticFall = new HashSet<Track>();
        private bool pendingOverbeat, composingOverbeat;
        public int TempoPercentage(bool attacker) => tempo[attacker].percentage;
        public string TempoMovement(bool attacker) => tempo[attacker].Movement;
        /// <summary>Consecutive Perfect sequences toward ignition (four pips beneath the Conductor's tempo marking).</summary>
        public int TempoStreak(bool attacker) => tempo[attacker].streak;
        public bool TempoFaltering(bool attacker) => tempo[attacker].faltering;
        public string TempoChange(bool attacker) => tempo[attacker].lastChange;
        public BattleClimaxTier ClimaxTier(bool attacker) => tempo[attacker].Tier;
        public IReadOnlyList<string> MusicLayers(bool attacker) => BattleTempoFever.MusicLayers(TempoPercentage(attacker));
        public bool IsSympathetic(bool attacker, int voice) { var track = TrackOf(Of(attacker), voice); return track != null && Tune(track) == BattleTuningState.Sympathetic; }
        /// <summary>A Climax composition is written this Measure: committing it declares the Resolution, and the Fever ends at Assessment.</summary>
        public bool ResolutionComposed(bool attacker) => chords.Values.Any(c => c.Live && c.climax && c.side.attacker == attacker && c.composedMeasure == m);
        public bool AwaitingOverbeat => pendingOverbeat;
        public bool HasOverbeat(bool attacker, int voice) => EligibleOverbeat(TrackOf(Of(attacker), voice));
        private bool EligibleOverbeat(Track track) => track != null && TempoPercentage(track.side.attacker) > 0 && track.spotlit && Synced(track);
        /// <summary>Within the Conductor's synchronization and at least In Tune, still able to perform.</summary>
        private bool Synced(Track track) => track != null && track.side.Conducted && Tune(track) >= BattleTuningState.InTune &&
            (track.unit == null || track.unit.Standing && !track.unit.deathKnell);
        private bool OverbeatTaken(Track track) => overbeatSteps.ContainsKey(track) || overbeatReactions.Any(r => r.track == track) ||
            chords.Values.Any(c => c.Live && c.notes.Any(n => n.track == track && n.overbeat && !n.failed));

        /// <summary>One extra Note on a synchronized Spotlit Track, between Beat 3 and Beat 4; no time is added.</summary>
        public string CommitOverbeat(bool attacker, int handIndex, int target = -1, long working = 0)
        {
            var s = Of(attacker); string why = WhyNotCompose(s);
            if (why != null) return why;
            if (handIndex < 0 || handIndex >= s.perf.hand.Count) return "No such card in the hand.";
            var dc = s.perf.deck[s.perf.hand[handIndex]]; var track = TrackOf(s, dc);
            if (!EligibleOverbeat(track) || OverbeatTaken(track)) return "This synchronized Spotlit Track has no free Overbeat.";
            why = WhyNot(s, dc); if (why != null) return why;
            if (dc.card.reaction != null)
            {
                var before = reactions.Select(r => r.id).ToHashSet(); why = Prime(s, handIndex, target, 1);
                if (why != null) return why;
                var primed = reactions.Single(r => !before.Contains(r.id)); reactions.Remove(primed);
                overbeatReactions.Add((track, primed.preparation)); return null;
            }
            if (dc.card.noteRole == BattleNoteRole.Minor)
            {
                var c = working > 0 && chords.TryGetValue(working, out var chosen) ? chosen : MajorOf(track);
                if (c == null || c.side != s || !c.Live || c.majorBeat < Cadence || c.drilled || c.ward) return "Choose a friendly working releasing at or after the Cadence.";
                if (dc.card.chord == null && !WardMinor(dc.card)) return "The Overbeat Minor needs a structural modifier or Ward.";
                if (track != c.owner && !Synchronized(track, c.owner)) return "The contributor is not synchronized with the Core.";
                if (c.climax && track != c.owner)
                {
                    var tier = tempo[attacker].Tier;
                    if (tier < BattleClimaxTier.Dyad) return "Climax contributions open at 65% (Climax Dyad).";
                    if (tier < BattleClimaxTier.Ensemble && c.notes.Any(n => n.overbeat && n.track != c.owner && n.kind == BattleNoteKind.Contribution))
                        return "Climax Dyad: the Climax Major carries one Minor from another Track; every Track may contribute from 70% (Climax Ensemble).";
                }
                if (!c.climax)
                {
                    var combined = BattleChordLogic.Compose(c.core.card, c.notes.Skip(1).Where(n => n.kind != BattleNoteKind.SupportingMajor).Select(n => n.card.card)
                        .Concat(new[] { dc.card }).Where(card => card.chord != null), ct);
                    if (combined.minors.Count > 3 || !AgeMagic.Playable(CardFace.Tier(combined), field.age)) return "The working exceeds its binding or Age limit.";
                }
                var footing = (BattleFooting)Math.Max((int)c.footing, (int)FootingOf(s, dc.card));
                if (footing == BattleFooting.Anchored && c.notes.Select(n => n.track).Append(track).Any(t => t.steps.Count > 0 || overbeatSteps.ContainsKey(t))) return "Anchored workings cannot promise Steps.";
                c.footing = footing;
                c.notes.Add(new ChordNote { card = Frozen(dc), track = track, beat = Abs(3), origin = Projected(Performer(track), Abs(3)),
                    handIndex = s.perf.hand[handIndex], kind = track == c.owner ? BattleNoteKind.Minor : BattleNoteKind.Contribution, overbeat = true });
                c.power = Power(s, c); s.perf.hand.RemoveAt(handIndex); return null;
            }
            if (dc.card.ChannelBeats != 1 || TempoOf(s, dc.card) != CardTempo.Staccato || WardMajor(dc.card) || dc.card.hyperMeasures > 1 || dc.card.corrupted || dc.card.cathartic)
                return "An ordinary Overbeat Major must be a Staccato Unison.";
            long beforeChord = nextChord; composingOverbeat = true;
            try { why = ComposeChord(s, handIndex, new List<int>(), target, 1, 3, null); }
            finally { composingOverbeat = false; }
            if (why != null) return why;
            var extra = chords[beforeChord]; extra.overbeat = extra.Major.overbeat = true;
            // Sounding on this compressed slot costs no elapsed Beat and cannot extend Hold or Wards.
            extra.firstBeat = Cadence; return null;
        }
        public string CommitOverbeatStep(bool attacker, int voice, int hex)
        {
            var track = TrackOf(Of(attacker), voice);
            if (!open || scoresCommitted || !EligibleOverbeat(track) || OverbeatTaken(track)) return "Compose on a free synchronized Overbeat.";
            if (track.unit == null || track.rushing || !SpatialRules.Passable(hex) || StepCost(hex) > 1) return "This Overbeat cannot carry the Step.";
            int from = Projected(track.unit, Abs(3));
            if (!BattleHexLayout.AreAdjacent(from, hex) || !MayStep(track, Abs(3))) return "The Step must obey adjacency and Footing.";
            overbeatSteps[track] = hex; return null;
        }

        // ===== RESOLUTION CLIMAX =====

        /// <summary>
        /// A finishing Major on a synchronized Spotlit Track's Climax Overbeat, releasing at the Cadence (Climax Unison, 60%).
        /// A second Track may place one from 75% (Linked Climax), every Track from 85% (Linked Finales); all of a Measure's
        /// Climax Majors are linked. <paramref name="fuse"/> folds the Track's own Chord into it (Fused Finale, 80%): that
        /// Chord's Major becomes a Supporting Major and its Minors ornament the finale. Committing any of it declares the Resolution.
        /// </summary>
        public string CommitClimaxMajor(bool attacker, int handIndex, int target = -1, bool fuse = false)
        {
            var s = Of(attacker); string why = WhyNotCompose(s) ?? WhyNotHand(attacker, handIndex, false);
            if (why != null) return why;
            var tier = tempo[attacker].Tier;
            if (tier == BattleClimaxTier.None) return "Climax compositions open at 60% Resolution Climax.";
            var dc = s.perf.deck[s.perf.hand[handIndex]]; var card = dc.card; var track = TrackOf(s, dc);
            if (!EligibleOverbeat(track) || OverbeatTaken(track)) return "This synchronized Spotlit Track has no free Climax Overbeat.";
            if (card.reaction != null || WardMajor(card) || card.corrupted || card.cathartic || card.hyperMeasures > 1)
                return "A Climax Major is a finishing working: not a Reaction, a Ward, a compulsion or a Hyper Chord.";
            var performer = Performer(track);
            if (ChannelOf(s, card, performer, false) > 1) return "A Climax Major sounds only on its Overbeat: a Legato or channeled working cannot anchor there.";
            var written = chords.Values.Where(c => c.Live && c.climax && c.side == s && c.composedMeasure == m).ToList();
            if (written.Any(c => c.grand)) return "The Grand Resolution already holds every synchronized voice.";
            int allowed = tier >= BattleClimaxTier.LinkedFinales ? int.MaxValue : tier >= BattleClimaxTier.Linked ? 2 : 1;
            if (written.Count >= allowed) return tier >= BattleClimaxTier.Linked ? "Linked Climax permits a second Climax Major; every Track may place one from 85% (Linked Finales)."
                : "Climax Unison permits one Climax Major; a second Track may place one from 75% (Linked Climax).";
            Chord fused = null;
            if (fuse)
            {
                if (tier < BattleClimaxTier.FusedFinale) return "Fused Finales open at 80%.";
                if (tier < BattleClimaxTier.LinkedFinales && written.Any(c => c.merged.Count > 0)) return "At 80% one Climax Major may fuse; every Track may from 85% (Linked Finales).";
                fused = MajorOf(track);
                if (fused == null || fused.drilled || fused.ward || fused.hyperMeasures > 1 || fused.state == BattleChordState.Suspended || fused.majorBeat != Cadence)
                    return "Fuse with the Track's own Chord releasing at this Cadence.";
            }
            var aimed = AimsAtEnemy(card) ? s.enemy : s;
            if (target >= aimed.side.sections.Count) return "No such Core target.";
            CombatSection victim;
            planning = true; projectAt = Cadence;
            try { victim = target >= 0 ? aimed.side.sections[target] : Value(s, dc, null).target; why = TargetFailure(s, dc, victim); }
            finally { planning = false; projectAt = 0; }
            if (why != null) return why;
            var chord = new Chord { id = nextChord, side = s, owner = track, core = Frozen(dc), target = victim, majorBeat = Cadence, channel = 1, firstBeat = Cadence,
                origin = Projected(performer, Cadence), footing = FootingOf(s, card), staccato = TempoOf(s, card) == CardTempo.Staccato, spell = Magic(card),
                magical = IsWeaving(card), composedMeasure = m, climax = true, heartbeatShare = victim?.ComposureShare ?? 1f,
                stanceHeld = s.side.StanceBroken ? BattleStanceHold.Broken : BattleStanceHold.Held };
            chord.notes.Add(new ChordNote { card = chord.core, track = track, beat = Cadence, kind = BattleNoteKind.Major, handIndex = s.perf.hand[handIndex], origin = chord.origin, overbeat = true });
            if (fused != null) Merge(chord, fused, false);
            int sounding = chord.majorBeat - chord.firstBeat + 1, limit = HoldLimit(chord);
            if (limit < Math.Min(sounding, BattleMeasureMath.Beats)) return $"The finale would sound {sounding} Beats against a Hold Limit of {limit}: build something to hold it first.";
            nextChord++; chord.seed = unchecked((int)s.perf.rng.Next());
            chord.power = Power(s, chord);
            if (fused != null) chords.Remove(fused.id);
            chords[chord.id] = chord;
            s.perf.hand.RemoveAt(handIndex);
            ChordEvent(s, chord.id, fused != null ? BattleChordCause.ClimaxFused : BattleChordCause.ClimaxComposed,
                $"{dc.Name}: Climax Major on {track.Name}'s Overbeat{(fused != null ? ", fused with " + fused.core.Name : string.Empty)}; it releases at the Cadence.");
            if (!Automatic(s)) { if (s.attacker) handA++; else handD++; }
            return null;
        }

        /// <summary>
        /// The Grand Resolution Chord (90%): the synchronized Spotlit Tracks merge their Majors, Minors and Climax Overbeats into
        /// one Ensemble working at the Cadence. The declaring Track's Climax Major (or its own Major) is the Core; every other
        /// Major supports it with its full power. One Hold from the earliest Note, one Interference count, one Collapse.
        /// </summary>
        public string CommitGrandResolution(bool attacker, int voice)
        {
            var s = Of(attacker); string why = WhyNotCompose(s);
            if (why != null) return why;
            if (tempo[attacker].Tier < BattleClimaxTier.GrandResolution) return "The Grand Resolution Chord needs 90% Resolution Climax.";
            var declaring = TrackOf(s, voice);
            if (declaring == null || !declaring.spotlit || !Synced(declaring)) return "The declaring Track must be a synchronized Spotlit Track.";
            var members = TracksOf(s).Where(t => t.spotlit && Synced(t)).ToList();
            var parts = chords.Values.Where(c => c.Live && c.side == s && !c.drilled && !c.ward && !c.overbeat && c.hyperMeasures <= 1 && c.state != BattleChordState.Suspended &&
                c.majorBeat == Cadence && members.Contains(c.owner)).OrderBy(c => c.id).ToList();
            if (parts.Any(c => c.grand)) return "The Grand Resolution is already composed.";
            var core = parts.FirstOrDefault(c => c.climax && c.owner == declaring && c.Major.overbeat) ?? parts.FirstOrDefault(c => c.owner == declaring);
            if (core == null) return "The declaring Track needs a Major releasing at this Cadence.";
            if (parts.Select(c => c.owner).Distinct().Count() < 2) return "A Grand Resolution merges the workings of several synchronized Tracks.";
            var grand = new Chord { id = nextChord, side = s, owner = core.owner, core = core.core, target = core.target, majorBeat = Cadence, channel = core.channel,
                firstBeat = core.firstBeat, origin = core.origin, footing = core.footing, legato = core.legato, staccato = core.staccato, spell = core.spell, magical = core.magical,
                composedMeasure = m, climax = true, grand = true, seed = core.seed, heartbeatShare = core.heartbeatShare, stanceHeld = core.stanceHeld, state = core.state,
                interference = core.interference, fermata = core.fermata, support = core.support };
            foreach (var n in core.notes) grand.notes.Add(CopyNote(n, n.kind, n.from, n.target));
            grand.merged.Add(core);
            foreach (var part in parts.Where(c => c != core)) Merge(grand, part, true);
            int sounding = grand.majorBeat - grand.firstBeat + 1, limit = HoldLimit(grand);
            if (limit < Math.Min(sounding, BattleMeasureMath.Beats)) return $"The Grand Resolution would sound {sounding} Beats against a Hold Limit of {limit}.";
            nextChord++;
            grand.power = Power(s, grand);
            foreach (var part in parts) chords.Remove(part.id);
            chords[grand.id] = grand;
            ChordEvent(s, grand.id, BattleChordCause.GrandResolution, $"Grand Resolution Chord: {core.core.Name} carries {string.Join(", ", parts.Where(c => c != core).Select(c => c.core.Name))}.");
            return null;
        }

        /// <summary>Folds <paramref name="part"/> into <paramref name="into"/>: its Major supports the Core, its Minors ornament it, its Hold debts come with it.</summary>
        private void Merge(Chord into, Chord part, bool ensemble)
        {
            foreach (var n in part.notes)
            {
                bool major = n == part.Major || n.kind == BattleNoteKind.SupportingMajor;
                var kind = major ? BattleNoteKind.SupportingMajor : ensemble && n.kind == BattleNoteKind.Minor && n.track != into.owner ? BattleNoteKind.Contribution : n.kind;
                into.notes.Add(CopyNote(n, kind, n == part.Major ? (n.overbeat ? Cadence : MajorStarts(part)) : n.from, n == part.Major ? part.target : n.target));
            }
            into.firstBeat = Math.Min(into.firstBeat, part.firstBeat);
            into.interference += part.interference; into.fermata = Math.Max(into.fermata, part.fermata);
            into.footing = (BattleFooting)Math.Max((int)into.footing, (int)part.footing);
            into.magical |= part.magical;
            if (part.state == BattleChordState.Sounding) into.state = BattleChordState.Sounding;
            into.support += part.power;
            into.merged.Add(part);
        }

        private static ChordNote CopyNote(ChordNote n, BattleNoteKind kind, int from, CombatSection target) => new ChordNote { card = n.card, track = n.track, beat = n.beat,
            handIndex = n.handIndex, origin = n.origin, rendition = n.rendition, sounded = n.sounded, failed = n.failed, flicker = n.flicker, measured = n.measured,
            missApplied = n.missApplied, failure = n.failure, overbeat = n.overbeat, heard = n.heard, kind = kind, from = from, target = target };

        /// <summary>Commitment makes a composed Climax real: the Resolution is declared and the Fever ends at this Measure's Assessment.</summary>
        private void DeclareResolutions()
        {
            foreach (var s in new[] { a, d })
            {
                var finales = chords.Values.Where(c => c.Live && c.climax && c.side == s && c.composedMeasure == m).OrderBy(c => c.id).ToList();
                if (finales.Count == 0) continue;
                tempo[s.attacker].resolutionDeclared = true;
                foreach (var c in finales)
                {
                    var record = new BattleResolutionRecord { action = c.id, measure = m, attacker = s.attacker, percentage = TempoPercentage(s.attacker), tier = TierOf(c, finales.Count),
                        core = c.core.Name };
                    record.contributors.AddRange(c.notes.Select(n => LegendOf(n.track)?.name ?? n.track.Name).Distinct());
                    record.notes.AddRange(c.notes.Select(n => n.card.Name));
                    record.bindings.AddRange(c.notes.Select(NoteBinding).Where(b => b != SpellBinding.Unattuned).Distinct().Select(b => b.ToString()));
                    record.inDeathKnell.AddRange(c.notes.Select(n => Performer(n.track)).Where(x => x != null && x.deathKnell).Select(x => x.leader?.name ?? x.name).Distinct());
                    report.resolutions.Add(record);
                    ChordEvent(s, c.id, BattleChordCause.ResolutionDeclared, $"Resolution declared: {record.tier} at {record.percentage}%. Tempo Fever ends at Assessment, resolved or collapsed.");
                }
            }
        }

        private static BattleClimaxTier TierOf(Chord c, int linked)
        {
            if (c.grand) return BattleClimaxTier.GrandResolution;
            if (c.merged.Count > 0) return linked > 1 ? BattleClimaxTier.LinkedFinales : BattleClimaxTier.FusedFinale;
            if (linked > 1) return BattleClimaxTier.Linked;
            int contributions = c.notes.Count(n => n.overbeat && n.track != c.owner);
            return contributions > 1 ? BattleClimaxTier.Ensemble : contributions == 1 ? BattleClimaxTier.Dyad : BattleClimaxTier.Unison;
        }

        private void RememberPerformance(Chord c)
        {
            var record = report.resolutions.LastOrDefault(r => r.action == c.id && r.attacker == c.side.attacker);
            if (record == null) return;
            record.outcome = c.state == BattleChordState.Resolved ? (c.releaseCancelled ? "Abjured" : "Resolved") : c.state.ToString();
        }

        /// <summary>The other Climax workings composed by the same side in the same Measure: they resolve together or collapse together.</summary>
        private IEnumerable<Chord> LinkedClimaxes(Chord c) =>
            chords.Values.Where(x => x != c && x.Live && x.climax && !x.grand && !c.grand && x.side == c.side && x.composedMeasure == c.composedMeasure).OrderBy(x => x.id);

        /// <summary>The Resolution Guarantee at the Cadence: if one linked Climax working cannot release, all of them collapse.</summary>
        private void LinkedClimaxGuarantee()
        {
            foreach (var s in new[] { a, d })
            {
                var group = chords.Values.Where(c => c.Live && c.climax && !c.grand && c.side == s && c.composedMeasure == m).OrderBy(c => c.id).ToList();
                if (group.Count < 2) continue;
                var broken = group.FirstOrDefault(c => !WillRelease(c));
                if (broken != null) Collapse(broken, $"{broken.core.Name} cannot resolve, and linked workings resolve together or collapse together.");
            }
        }

        private bool WillRelease(Chord c)
        {
            if (c.state == BattleChordState.Suspended) return false;
            var composed = Composed(c);
            return CoreFailure(c, composed) == null || Adaptive(c) && c.owner.unit?.Standing != false && Fallback(c, composed) != null;
        }

        /// <summary>Every Supporting Major adds its full power to the finale's release, on the Core's target when it aims the same way.</summary>
        private void ReleaseSupportingMajors(Chord c)
        {
            var s = c.side;
            for (int i = 0; i < c.notes.Count; i++)
            {
                var n = c.notes[i];
                if (n.kind != BattleNoteKind.SupportingMajor || n.failed || n.sounded) continue;
                var voice = Performer(n.track);
                string why = voice == null || !voice.Standing ? "The supporting voice fell before the Cadence." : WhyNot(s, n.card);
                if (why != null) { Fail(c, n, why); continue; }
                n.sounded = true;
                bool sameAim = AimsAtEnemy(n.card.card) == AimsAtEnemy(c.core.card);
                var target = sameAim && c.target?.Standing == true ? c.target : n.target?.Standing == true ? n.target : null;
                float rendition = BattleMeasureMath.Grade(n.rendition) == BattleExecution.Missed ? 1f : Math.Min(1f + mt.perfectPower, n.rendition);
                Execute(s, new Queued { index = -1, target = target, rendition = rendition, origin = voice.battleHex, frozen = n.card, seed = unchecked(c.seed + i * 104729),
                    action = c.id, explicitChord = true, stanceHeld = c.stanceHeld == BattleStanceHold.Held, chord = c });
            }
        }

        // ===== SYMPATHETIC RESONANCE =====

        /// <summary>When a working performed or contributed to by a Sympathetic Track collapses: every Sympathetic Track of that side.</summary>
        private List<Track> SympatheticRing(Chord c)
        {
            if (!tempo[c.side.attacker].Climax) return new List<Track>();
            if (!c.notes.Select(n => n.track).Append(c.owner).Distinct().Any(x => Tune(x) == BattleTuningState.Sympathetic)) return new List<Track>();
            return tracks.Values.Where(x => x.side == c.side && Tune(x) == BattleTuningState.Sympathetic).OrderBy(x => x.voice).ToList();
        }

        /// <summary>Sympathetic resonance becomes sympathetic dissonance: the ring falls to Detuned, loses synchronization, and its unsounded Notes go unresolved.</summary>
        private void SympatheticFall(Side s, List<Track> ring, Chord origin)
        {
            foreach (var x in ring) { sympathetic.Remove(x); sympatheticFall.Add(x); }
            tempo[s.attacker].shattered = true;
            ChordEvent(s, origin.id, BattleChordCause.SympatheticCollapse,
                $"Sympathetic Collapse: {string.Join(", ", ring.Select(x => x.Name))} sound the break, fall to Detuned and lose synchronization. Tempo Fever shatters at Assessment.");
            foreach (var c in chords.Values.Where(x => x.Live && x.side == s && x != origin).OrderBy(x => x.id).ToList())
                foreach (var n in c.notes.Where(n => ring.Contains(n.track) && !n.sounded && !n.failed && (n.overbeat || n.track != c.owner)).ToList())
                {
                    Fail(c, n, "Sympathetic Collapse: the Track fell to Detuned and lost synchronization.");
                    if (n == c.Major) Suspend(c, "Its Overbeat fell silent in the Sympathetic Collapse.");
                }
        }

        /// <summary>A Track that loses synchronization stops being Sympathetic, without triggering anything.</summary>
        private void PruneSympathy() => sympathetic.RemoveWhere(x => !tempo[x.side.attacker].Climax || !x.side.Conducted || Needle(x) < BattleTuningState.InTune ||
            x.unit != null && !x.unit.Standing);

        /// <summary>Assessment: sympathy fades when the Fever ends; in Climax every synchronized Spotlit Track of the Measure joins it.</summary>
        private void AssessSympathy(Side s)
        {
            sympatheticFall.RemoveWhere(x => x.side == s);
            if (!tempo[s.attacker].Climax) { sympathetic.RemoveWhere(x => x.side == s); return; }
            PruneSympathy();
            foreach (var x in TracksOf(s).Where(x => x.spotlit && Synced(x)).ToList()) sympathetic.Add(x);
        }

        // ===== THE OVERBEAT =====

        private bool AnyOverbeat => overbeatSteps.Count > 0 || overbeatReactions.Count > 0 || chords.Values.Any(c => c.Live && c.notes.Any(n => n.overbeat && !n.failed && !n.sounded && !n.heard));
        public string OverbeatDescription(bool attacker, int voice)
        {
            var track = TrackOf(Of(attacker), voice); if (track == null) return "";
            var notes = chords.Values.Where(c => c.Live).SelectMany(c => c.notes.Where(n => n.track == track && n.overbeat && !n.failed)
                .Select(n => (c.climax && (n == c.Major || n.kind == BattleNoteKind.SupportingMajor) ? "Climax Major " : c.climax ? "Climax Minor " : "") + n.card.Name));
            return string.Join(", ", notes.Concat(overbeatSteps.TryGetValue(track, out int hex) ? new[] { "Step to " + hex } : Array.Empty<string>())
                .Concat(overbeatReactions.Where(r => r.track == track).Select(r => "Prime " + r.preparation.frozen.Name)));
        }
        private void PerformOverbeat()
        {
            pendingOverbeat = false;
            try
            {
                Enter(BattlePhase.Execution);
                // Reuse simultaneous movement/contact; save Beat 4's promised path before the compressed Step.
                var saved = tracks.Values.ToDictionary(t => t, t => (steps: t.steps.ToList(), path: t.path.ToList()));
                foreach (var t in tracks.Values) { t.steps.Clear(); t.path.Clear(); }
                foreach (var pair in overbeatSteps)
                    if (EligibleOverbeat(pair.Key)) pair.Key.steps.Add((beat, pair.Value));
                    else Emit(pair.Key.side, pair.Key.unit, BattleSpatialCause.FailedMovementCommitment, "The Step lost Overbeat synchronization.");
                Steps(); FlushReactionsAndHits();
                foreach (var pair in saved)
                { pair.Key.steps.Clear(); pair.Key.steps.AddRange(pair.Value.steps); pair.Key.path.Clear(); pair.Key.path.AddRange(pair.Value.path); }
                overbeatSteps.Clear(); ApplyPerformedNotes();
                var contexts = new List<Release>(); releasing = contexts;
                foreach (var (c, n) in chords.Values.Where(c => c.Live).SelectMany(c => c.notes.Where(n => n.overbeat && !n.sounded && !n.failed && !n.heard).Select(n => (c, n))).OrderBy(x => x.c.id).ToList())
                {
                    bool finishing = c.climax && (n == c.Major || n.kind == BattleNoteKind.SupportingMajor);
                    if (!EligibleOverbeat(n.track))
                    {
                        Fail(c, n, "The Track lost Overbeat synchronization.");
                        if (c.overbeat || finishing && n == c.Major) Suspend(c, "Its Overbeat was lost.");
                        continue;
                    }
                    // A Climax Major sounds on its Overbeat and releases at the Cadence; nothing in a Resolution is syncopated.
                    if (finishing) { n.heard = true; if (c.state == BattleChordState.Composed) c.state = BattleChordState.Sounding; continue; }
                    if (n == c.Major) { var context = ReleaseChord(c); if (context != null) contexts.Add(context); }
                    else SoundMinor(c, n, contexts);
                }
                foreach (var prepared in overbeatReactions.Where(r => EligibleOverbeat(r.track))) reactions.Add(new ArmedReaction { id = nextReaction++, attacker = prepared.track.side.attacker,
                    track = prepared.track, preparation = prepared.preparation, measure = m });
                foreach (var lost in overbeatReactions.Where(r => !EligibleOverbeat(r.track)))
                {
                    if (lost.preparation.index >= 0) lost.track.side.perf.discard.Add(lost.preparation.index);
                    Emit(lost.track.side, lost.track.unit, BattleSpatialCause.PreparationFailed, "The Reaction lost Overbeat synchronization.");
                }
                overbeatReactions.Clear();
                ProcessReactions(); Impact(contexts, false);
                foreach (var c in contexts.Select(ctx => ctx.chord).Where(c => c?.overbeat == true).Distinct()) { c.state = BattleChordState.Resolved; FinishChord(c); }
                FlushReactionsAndHits(); Coherence(); Pressure(); PumpCascade(); SyncCrises(); EliteChecks();
            }
            finally { releasing = null; }
        }
    }
}
