using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        private BattleRhythmChallenge rhythm;
        private bool rhythmSide, interactiveCadence;
        private BattleRhythmOptions rhythmOptions;
        private readonly Dictionary<int, (Chord chord, ChordNote note)> rhythmNotes = new Dictionary<int, (Chord, ChordNote)>();
        private readonly Dictionary<int, (Release release, CombatSection target)> rhythmWards = new Dictionary<int, (Release, CombatSection)>();
        private readonly Dictionary<(Release, CombatSection), float> responses = new Dictionary<(Release, CombatSection), float>();
        private List<Release> pendingCadence;
        private List<Chord> pendingDue;
        private readonly List<(Chord chord, ChordNote note, BattleRhythmResult result)> performedNotes = new List<(Chord, ChordNote, BattleRhythmResult)>();

        public BattleRhythmChallenge Rhythm => rhythm;
        public bool AwaitingAbjuration => pendingCadence != null;
        public bool TempoFever(bool attacker, int section) => TrackOf(Of(attacker), section) != null && TempoPercentage(attacker) > 0;
        public float ScoreBpm => Math.Max(30f, Math.Min(240f, combatSettings.Rhythm.bpm));

        /// <summary>Locks the Score and opens the next Beat's call/echo. Battlefield time advances only on completion.</summary>
        public BattleRhythmChallenge BeginRhythmBeat(bool attacker, double dspStart, BattleRhythmOptions options = null)
        {
            if (double.IsNaN(dspStart) || double.IsInfinity(dspStart)) throw new ArgumentOutOfRangeException(nameof(dspStart));
            if (Over || !open || rhythm != null || pendingCadence != null) throw new InvalidOperationException("There is no available Beat to perform.");
            if (Automatic(Of(attacker))) throw new InvalidOperationException("This side is performed automatically.");
            Commit();
            rhythmSide = attacker; rhythmOptions = (options ?? new BattleRhythmOptions()).Clone(); interactiveCadence = true;
            rhythmNotes.Clear(); rhythmWards.Clear(); responses.Clear();
            bool overbeat = pendingOverbeat; int next = overbeat ? beat : beat + 1;
            double duration = 60d / ScoreBpm;
            var lanes = new List<BattleRhythmLane>();
            foreach (var c in chords.Values.Where(c => c.Live && !c.drilled && c.side.attacker == attacker).OrderBy(c => c.id))
            {
                foreach (var n in c.notes.Where(n => n.track.spotlit && !n.failed && n.overbeat == overbeat &&
                    (overbeat ? !n.sounded && !n.heard && EligibleOverbeat(n.track) : n == c.Major ? c.state != BattleChordState.Suspended && (c.ward ? c.ChannelStart == next : next >= MajorStarts(c) && next <= c.majorBeat)
                        : !n.sounded && n.beat == next)))
                {
                    var recovery = c.core.card.effects.FirstOrDefault(e => e?.op == CardOp.CoRegulate);
                    var mode = recovery == null ? BattleRhythmMode.Execution : BattleRhythmMode.Heartbeat;
                    float pulse = recovery == null ? 0 : BattleRhythmLogic.HeartbeatBpm(ScoreBpm, c.heartbeatShare, next - c.ChannelStart, c.channel, combatSettings.Rhythm.heartbeatAcceleration);
                    int complexity = recovery == null ? 1 + (c.notes.Count - 1) / 3 : 2;
                    var lane = BattleRhythmLogic.Lane(c.id, n.track.voice, c.notes.IndexOf(n), n.track.Name + ": " + n.card.Name,
                        NoteBinding(n), complexity, duration, combatSettings.Rhythm, rhythmOptions, Focus(n.track), mode, pulse, TuningSteps(n.track));
                    rhythmNotes[lanes.Count] = (c, n); lanes.Add(lane);
                }
            }
            return rhythm = new BattleRhythmChallenge(dspStart, duration, lanes, rhythmOptions, overbeat: overbeat);
        }

        /// <summary>One fermata over all hostile Cadence releases, including visibly unplayable uncovered lanes.</summary>
        public BattleRhythmChallenge BeginAbjuration(double dspStart)
        {
            if (pendingCadence == null || rhythm != null) throw new InvalidOperationException("There is no Abjuration Window to answer.");
            var lanes = new List<BattleRhythmLane>();
            double duration = 60d / ScoreBpm * 2;
            foreach (var ctx in pendingCadence.Where(c => c.side.attacker != rhythmSide && !c.syncopated && !c.reaction))
            {
                foreach (var target in ctx.hits.Keys.Concat(ctx.deferred.Select(x => x.target)).Concat(ctx.pushes.Select(x => x.target)).Distinct()
                    .Where(x => ownerOf[x].attacker == rhythmSide).OrderBy(x => ownerOf[x].side.sections.IndexOf(x)))
                {
                    var ward = BestWard(target, ctx, out _);
                    var lane = BattleRhythmLogic.Lane(ctx.chord?.id ?? 0, ward == null ? -1 : TrackOf(ward.source)?.voice ?? -1, -1,
                        (ctx.chord?.core.Name ?? "Hostile release") + " → " + target.name, ctx.root, 1 + (ctx.chord?.notes.Count ?? 1) / 3,
                        duration, combatSettings.Rhythm, rhythmOptions, ward == null ? 0 : Focus(TrackOf(ward.source)), BattleRhythmMode.Abjuration, 0,
                        ward == null ? 0 : TuningSteps(TrackOf(ward.source)));
                    lane.target = ownerOf[target].side.sections.IndexOf(target); lane.playable = ward != null;
                    rhythmWards[lanes.Count] = (ctx, target); lanes.Add(lane);
                }
            }
            return rhythm = new BattleRhythmChallenge(dspStart, duration, lanes, rhythmOptions, abjuration: true);
        }

        public string RhythmInput(int lane, double dspTime) => rhythm == null ? "No phrase is being performed." : rhythm.Input(lane, dspTime);

        public string CompleteRhythm(double dspTime)
        {
            if (rhythm == null) return "No phrase is being performed.";
            if (!rhythm.CanComplete(dspTime)) return "The response has not finished.";
            var frame = rhythm;
            var results = frame.Complete(dspTime, mt.perfectPower);
            report.rhythm.Add(new BattleRhythmRecord { measure = m, beat = frame.abjuration || frame.overbeat ? beat : beat + 1, attacker = rhythmSide, abjuration = frame.abjuration, overbeat = frame.overbeat,
                start = frame.start, end = frame.end, options = frame.options, inputs = frame.inputs.ToList(), results = results.ToList() });
            rhythm = null;
            if (frame.abjuration)
            {
                foreach (var pair in rhythmWards) responses[(pair.Value.release, pair.Value.target)] = results[pair.Key].rendition;
                CompleteCadence();
                interactiveCadence = false;
                AfterNotes();
                responses.Clear(); rhythmWards.Clear();
            }
            else
            {
                foreach (var pair in rhythmNotes)
                {
                    var (c, n) = pair.Value; var result = results[pair.Key];
                    performedNotes.Add((c, n, result));
                }
                rhythmNotes.Clear();
                if (frame.overbeat) PerformOverbeat(); else PerformBeat();
                if (pendingCadence == null) interactiveCadence = false;
            }
            return null;
        }

        private void ApplyPerformedNotes()
        {
            foreach (var (c, n, result) in performedNotes)
            {
                if (!c.Live || n.failed) continue;
                // A channel must stay clean on every Beat, not just its last downstroke.
                n.rendition = n.measured ? Math.Min(n.rendition, result.rendition) : result.rendition;
                n.measured = true;
                if (c.core.card.effects.Any(e => e?.op == CardOp.CoRegulate)) c.heartbeatRendition = result.rendition;
                if (result.grade == BattleExecution.Missed && !n.missApplied)
                { n.missApplied = true; AddInterference(c, 1, BattleChordCause.Missed, n.card.Name + " missed its echo.", c.notes.IndexOf(n)); }
            }
            performedNotes.Clear();
        }

        private void CompleteCadence()
        {
            var contexts = pendingCadence; var due = pendingDue;
            pendingCadence = null; pendingDue = null;
            Impact(contexts, true);
            foreach (var c in due.Where(c => c.Live || c.state == BattleChordState.Resolved)) { c.state = BattleChordState.Resolved; FinishChord(c); }
            FlushReactionsAndHits();
        }

        private int Focus(Track track) => track == null ? 0 : BattleMeasureMath.Mastery(LegendOf(track)?.Score(SpellBinding.Cindergale) ?? 0);
        /// <summary>Tuning widens or narrows timing windows: Resonant and Sympathetic +1 step, Detuned -1, Out of Tune -2.</summary>
        private int TuningSteps(Track track) => track == null ? 0 : Math.Min(1, (int)Tune(track) - (int)BattleTuningState.InTune);
        private static int MajorStarts(Chord c) => c.hyperMeasures > 1 ? BattleMeasureMath.Cadence(c.composedMeasure) - c.channel + 1 : c.ChannelStart;

        /// <summary>A helper pays real Track time; each sounding Beat restores only the patient's battle mind.</summary>
        private void Heartbeats()
        {
            foreach (var c in chords.Values.Where(c => c.Live && c.state != BattleChordState.Suspended && c.ChannelStart <= beat && c.majorBeat >= beat &&
                c.core.card.effects.Any(e => e?.op == CardOp.CoRegulate)).OrderBy(c => c.id).ToList())
            {
                var helper = Performer(c.owner); var patient = c.target;
                var e = c.core.card.effects.First(x => x?.op == CardOp.CoRegulate);
                if (helper?.Standing != true || patient?.Standing != true || !SpatialRules.Support(helper, patient, e.range > 0 ? e.range : SpatialRules.Tuning.supportReach))
                { Suspend(c, "The helper can no longer reach the distressed performer."); continue; }
                if (BattleMeasureMath.Grade(c.heartbeatRendition) == BattleExecution.Missed) continue;
                float amount = Math.Max(0, e.amount * c.core.scale * Math.Min(1f + mt.perfectPower, c.heartbeatRendition));
                patient.composure = Math.Min(patient.maxComposure, Math.Max(0, patient.composure) + amount);
                if (patient.ComposureShare >= mt.spiralingBelow) patient.mindBroken = false;
                if (patient.leader != null && patient.leader == c.side.conductor)
                { c.side.bar = Math.Min(c.side.barMax, Math.Max(0, c.side.bar) + amount); if (c.side.bar >= c.side.barMax * mt.spiralingBelow) c.side.conductorBroken = false; }
                ChordEvent(c.side, c.id, BattleChordCause.CoRegulation, "Heartbeat met, then slowed toward the Score.", composure: amount);
                Steady(c.side, patient);
            }
        }
    }
}
