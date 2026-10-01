using System;
using System.Collections.Generic;
using System.Linq;

// Chord Layering in time (vault: Combat System.md): the Major Note is the Core; Minor Notes sound on the Beats before it
// and take effect there. Every Chord has a Hold Limit (base 4, one less per Interference); on the downstroke of a Beat
// any Chord that would sound beyond it collapses whole, and its power returns along the channels that built it.
public static partial class BattleResolver
{
    /// <summary>One release on a Beat: what it would do to each target, held until the simultaneous Impact.</summary>
    private sealed class Release
    {
        public Chord chord;
        public Side side;
        public SpellBinding root;
        public bool physical, magical, syncopated, reaction;
        public readonly Dictionary<CombatSection, Hit> hits = new Dictionary<CombatSection, Hit>();
        public readonly List<(CombatSection target, Action apply)> deferred = new List<(CombatSection, Action)>();
        public readonly List<(Side owner, CombatSection target)> pushes = new List<(Side, CombatSection)>();
        public readonly HashSet<CombatSection> cancelled = new HashSet<CombatSection>();
    }

    public sealed partial class BattleRun
    {
        private BattleChordTuning ct => combatSettings.Chords;
        private Release release;
        private List<Release> releasing;
        private readonly HashSet<long> interruptedThisBeat = new HashSet<long>();
        private readonly Dictionary<long, int> groundAt = new Dictionary<long, int>();

        private void ChordEvent(Side s, long action, BattleChordCause cause, string detail, int note = -1, float strain = 0f, float integrity = 0f, float composure = 0f)
        {
            report.chordEvents.Add(new BattleChordEvent { measure = m, beat = beat, action = action, attacker = s.attacker,
                cause = cause, detail = detail, note = note, interference = strain, integrity = integrity, composure = composure });
        }

        private float Bond(Side s, CombatSection first, CombatSection second)
        {
            if (first == null || second == null || first == second) return 0f;
            var namesA = new[] { first.leader?.name, first.name }.Where(n => !string.IsNullOrEmpty(n));
            var namesB = new[] { second.leader?.name, second.name }.Where(n => !string.IsNullOrEmpty(n));
            return (s.side.echoingBonds ?? new List<BattleEchoingBond>()).Where(b => b != null && (namesA.Contains(b.first) && namesB.Contains(b.second) || namesB.Contains(b.first) && namesA.Contains(b.second)))
                .Select(b => Math.Max(0f, Math.Min(1f, b.strength))).DefaultIfEmpty(0f).Max();
        }

        private bool IsWeaving(CombatCard card) => Magic(card) || card.weaving || card.chord?.binding != null && card.chord.binding != SpellBinding.Unattuned;
        private static SpellBinding NoteBinding(ChordNote n) => n.card.card.chord?.binding ?? n.card.card.binding;
        private int OccupiesFrom(Chord c) => c.hyperMeasures > 1 ? c.firstBeat : c.ChannelStart;

        // ===== THE HOLD LIMIT =====

        /// <summary>
        /// Base 4, raised by preparation (Channels, Anchors and leylines in the performer's hex, Fermata, Sustain Notes from
        /// other Tracks, a bonded Ensemble, Crystal mastery and a Crystal Minor, Resonant Tuning, Hyper Stabilizations),
        /// lowered by Tuning and by one for every Interference. <paramref name="planned"/> counts Minors not yet sounded.
        /// </summary>
        private int HoldLimit(Chord c, bool planned = true)
        {
            int hold = mt.baseHold;
            var performer = Performer(c.owner);
            int hex = performer?.battleHex ?? -1;
            if (hex >= 0)
            {
                var local = Spatial.Terrain[hex];
                if (local.harmonicChannel) hold += mt.channelHold;
                if (local.leyline) hold += mt.channelHold;
                if (anchors.TryGetValue(hex, out int laid)) hold += laid * mt.channelHold;
            }
            hold += c.fermata;
            // A Supporting Major fused into a finale sounds from its own first Beat; it is a Major, not an ornament.
            var minors = c.notes.Skip(1).Where(n => !n.failed && (planned || n.sounded || n.kind == BattleNoteKind.SupportingMajor && n.from <= beat)).ToList();
            foreach (var n in minors.Where(n => n.kind != BattleNoteKind.SupportingMajor))
            {
                if (n.kind == BattleNoteKind.Sustain) hold += mt.sustainHold;
                else if (n.track == c.owner && n.card.card.chord?.holdBeats > 0) hold += Math.Min(mt.fermataCap, n.card.card.chord.holdBeats);
            }
            if (minors.Any(n => n.track != c.owner && Bond(c.side, Performer(n.track), performer) >= ct.bondThreshold)) hold += mt.bondedEnsembleHold;
            var legend = LegendOf(c.owner) ?? c.core.legend;
            int crystal = legend == null ? 0 : BattleMeasureMath.Mastery(legend.Score(SpellBinding.Crystal));
            hold += crystal >= 4 ? mt.crystalMasterHold : crystal >= 3 ? mt.crystalExpertHold : 0;
            if (minors.Any(n => n.kind != BattleNoteKind.SupportingMajor && NoteBinding(n) == SpellBinding.Crystal)) hold += mt.crystalMinorHold;
            hold += BattleMeasureMath.HoldOf(Tune(c.owner), mt);
            hold += c.stabilizations * mt.hyperStabilization;
            return hold - BattleTempoFever.InterferenceWeight(c.interference, TempoPercentage(c.side.attacker));
        }

        /// <summary>What the working carries: its declared output, which is the amplitude a Collapse returns home.</summary>
        private float Power(Side s, Chord c)
        {
            var intent = new BattleActionIntent { binding = RootOf(c.core, Performer(c.owner)) };
            var dc = c.core;
            if (c.drilled)
            {
                var unit = c.owner.unit;
                return Math.Max(mt.collapseFloor, c.spell ? (unit?.potency ?? t.conductorPotency) * t.spellHit * (t.integrityPerSpellHit + .5f * t.composurePerSpellHit) * t.tierPotency[Math.Min(3, c.notes.Count - 1)]
                    : (unit?.attack ?? 0f) * HarmPerAttack);
            }
            DescribeOutput(s, dc, new Queued { frozen = dc, target = c.target, rendition = 1f }, intent);
            int ornaments = c.notes.Count(n => n != c.Major && n.kind != BattleNoteKind.SupportingMajor);
            return Math.Max(mt.collapseFloor, (intent.expectedIntegrity + intent.expectedComposure + intent.expectedGuard + intent.expectedWard) * (1f + .25f * ornaments)) + c.support;
        }

        private void AddInterference(Chord c, int amount, BattleChordCause cause, string detail, int note = -1)
        {
            if (amount == 0) return;
            c.interference = Math.Max(0, c.interference + amount);
            ChordEvent(c.side, c.id, cause, detail, note, c.interference);
        }

        private void Fail(Chord c, ChordNote n, string why)
        {
            if (n.failed || n.sounded) return;
            n.failed = true; n.failure = why;
            int index = c.notes.IndexOf(n);
            AddInterference(c, 1, BattleChordCause.NoteFailed, why, index);
            if (index > 0) report.plays.Add(new CardPlay { measure = m, beat = beat, action = c.id, attacker = c.side.attacker, minor = true, card = n.card.card.id,
                cardName = n.card.Name, voice = n.track.Name, purpose = n.card.card.purpose, failed = true, failure = why });
        }

        // ===== THE DOWNSTROKE =====

        private void Downstroke()
        {
            foreach (var c in chords.Values.Where(c => c.Live && c.firstBeat <= beat).OrderBy(c => c.id).ToList())
            {
                if (groundAt.TryGetValue(c.id, out int at) && at == beat) { GroundNow(c); continue; }
                if (c.state == BattleChordState.Composed) c.state = BattleChordState.Sounding;
                int sounding = beat - c.firstBeat + 1, limit = HoldLimit(c, planned: false);
                if (sounding > limit) { Collapse(c, $"It would sound {sounding} Beats against a Hold Limit of {limit}."); continue; }
                if (sounding > BattleMeasureMath.Beats && c.magical)
                {
                    // Holding is never free: every Beat beyond the fourth is a direct tax on the mind.
                    var performer = Performer(c.owner);
                    if (c.owner.unit != null) performer.composure -= mt.holdDrain; else c.side.bar -= mt.holdDrain;
                    ChordEvent(c.side, c.id, BattleChordCause.HoldDrain, $"Held into its {sounding}th Beat.", strain: c.interference, composure: mt.holdDrain);
                }
            }
            FlushReactionsAndHits();
        }

        // ===== THE NOTES OF A BEAT =====

        private void Notes(bool cadence)
        {
            Heartbeats();
            var contexts = new List<Release>();
            releasing = contexts;
            var due = new List<Chord>();
            try
            {
                // Wards raised on this Beat stand first; then every Minor Note scheduled here sounds, Wards before the rest.
                foreach (var c in chords.Values.Where(c => c.Live && c.ward && c.ChannelStart == beat && !c.Major.sounded).OrderBy(c => c.id).ToList()) RaiseWard(c);
                foreach (var (c, n) in chords.Values.Where(c => c.Live).SelectMany(c => c.notes.Skip(1).Where(n => !n.overbeat && n.kind != BattleNoteKind.SupportingMajor && n.beat == beat && !n.sounded && !n.failed).Select(n => (c, n)))
                    .OrderBy(x => WardMinor(x.n.card.card) ? 0 : 1).ThenBy(x => x.c.id).ToList())
                    if (c.Live) SoundMinor(c, n, contexts);
                if (cadence) { StabilizationCadences(); LinkedClimaxGuarantee(); }
                // Every Major reaching its Beat releases at once: no initiative.
                due = chords.Values.Where(c => c.Live && !c.overbeat && c.state != BattleChordState.Suspended && c.majorBeat == beat).OrderBy(c => c.id).ToList();
                foreach (var c in due.ToList())
                {
                    if (c.ward) { c.Major.sounded = true; continue; }
                    var ctx = ReleaseChord(c);
                    if (ctx != null) contexts.Add(ctx); else due.Remove(c);
                }
                if (contexts.Count > 0)
                {
                    Enter(BattlePhase.Reaction);
                    ProcessReactions();
                    if (cadence) Enter(BattlePhase.Abjuration);
                    if (cadence && interactiveCadence)
                    {
                        pendingCadence = contexts; pendingDue = due;
                        return;
                    }
                    Impact(contexts, cadence);
                }
                foreach (var c in due.Where(c => c.Live || c.state == BattleChordState.Resolved)) { c.state = BattleChordState.Resolved; FinishChord(c); }
            }
            finally { releasing = null; }
            FlushReactionsAndHits();
        }

        private void RaiseWard(Chord c)
        {
            var s = c.side; var dc = c.core;
            c.Major.sounded = true; c.state = BattleChordState.Sounding;
            string why = WhyNot(s, dc) ?? (dc.card.requiresPosition && Performer(c.owner)?.battleHex != c.origin ? "The prepared position was displaced." : null);
            if (why != null) { Suspend(c, why); return; }
            var q = new Queued { index = c.Major.handIndex, target = c.target, rendition = c.Major.rendition, origin = c.origin, frozen = Composed(c), seed = c.seed,
                action = c.id, explicitChord = true, stanceHeld = c.stanceHeld == BattleStanceHold.Held, ward = c };
            actingVoice = Performer(c.owner); actingAction = c.id;
            Execute(s, q);
        }

        private void SoundMinor(Chord c, ChordNote n, List<Release> contexts)
        {
            var s = c.side;
            string why = NoteFailure(c, n);
            if (why != null) { Fail(c, n, why); return; }
            n.sounded = true;
            if (c.state == BattleChordState.Composed) c.state = BattleChordState.Sounding;
            int index = c.notes.IndexOf(n);
            var voice = Performer(n.track);
            if (BattleMeasureMath.Grade(n.rendition) == BattleExecution.Missed && !n.missApplied)
            { n.missApplied = true; AddInterference(c, 1, BattleChordCause.Missed, $"{n.card.Name} was Missed.", index); }
            var previous = c.notes.Where(x => x.sounded && x != n && x.beat == n.beat - 1).Select(x => Performer(x.track)).FirstOrDefault();
            float bond = Bond(s, previous, voice);
            if (IsWeaving(n.card.card))
            {
                var binding = NoteBinding(n);
                float stability = Math.Max(0f, n.card.card.chord?.stability ?? 0f) + bond;
                float chance = Interference(s, voice, ChordTier.Unison, n.card.card.kind != CardKind.Instinct, new List<SpellBinding> { binding }, field, t, Flicker(n.card)) / (1f + stability);
                if (new CombatRandom(unchecked(c.seed + index * 7919 + beat)).NextFloat() < chance)
                { n.flicker = true; AddInterference(c, 1, BattleChordCause.NoteFailed, $"{n.card.Name} flickered into Discordant Interference.", index); }
                if (voice != null && n.track.unit != null) { voice.composure -= t.essenceCost[0]; voice.casts++; }
                else s.bar -= t.essenceCost[0] * t.conductorEssence;
            }
            // A Minor Note takes effect on the Beat it sounds: Ward Minors stand at half Strength, ornaments play their own effects.
            var card = n.card.card;
            bool wardMinor = WardMinor(card);
            var effects = wardMinor ? card.effects : card.chord?.effects ?? new List<CardEffect>();
            if (effects.Count > 0)
            {
                var mini = new CombatCard { id = card.id, name = card.name, kind = card.kind, binding = card.chord?.binding ?? card.binding, purpose = card.purpose,
                    effects = effects.Select(e => e.Clone()).ToList() };
                float factor = (wardMinor ? mt.wardAsMinor : 1f) * (1f + ct.bondPower * bond);
                var frozen = new DeckCard { card = mini, voice = n.card.voice, legend = n.card.legend, scale = n.card.scale * factor, major = false, source = n.card.source };
                var aimed = mini.effects.FirstOrDefault(e => e.aim == CardAim.Enemy || e.aim == CardAim.Ally);
                // A Minor with its own Rank Proximity rule finds its own target; otherwise it serves the Core's.
                CombatSection target = aimed == null ? null : aimed.proximity != BattleProximity.Any ? EffectTarget(s, frozen, aimed, null)
                    : aimed.aim == CardAim.Enemy ? c.target : Performer(c.owner);
                var q = new Queued { index = -1, target = target, rendition = n.rendition, origin = voice?.battleHex ?? -1, seed = unchecked(c.seed + index),
                    action = c.id, explicitChord = true, reacting = true, ward = wardMinor ? c : null, frozen = frozen };
                var ctx = new Release { chord = c, side = s, root = mini.binding, syncopated = true, magical = IsWeaving(mini), physical = !IsWeaving(mini) };
                var saved = hits; hits = ctx.hits; release = ctx; actingVoice = voice; actingAction = c.id;
                try { Execute(s, q); } finally { hits = saved; release = null; }
                if (ctx.hits.Count > 0 || ctx.deferred.Count > 0 || ctx.pushes.Count > 0) contexts.Add(ctx);
            }
            ShedEcho(c);
            report.plays.Add(new CardPlay { measure = m, beat = beat, action = c.id, attacker = s.attacker, minor = true, card = card.id, cardName = n.card.Name,
                voice = n.track.Name, target = c.target?.name, purpose = card.purpose, flicker = n.flicker, rendition = n.rendition });
        }

        private string NoteFailure(Chord c, ChordNote n)
        {
            var s = c.side; var voice = Performer(n.track);
            if (voice == null || !voice.Standing || !VoiceAlive(s, n.card) && n.track.unit != null) return "The committed voice is no longer available.";
            string condition = WhyNot(s, n.card); if (condition != null) return condition;
            if (IsWeaving(n.card.card) && voice.mindBroken && !n.card.card.corrupted) return "The committed voice is Mind Broken.";
            if (n.track != c.owner && Tune(n.track) < BattleTuningState.InTune) return "Detuned: the contributor lost synchronization.";
            if (n.card.card.requiresPosition && voice.battleHex != n.origin) return "The committed position was displaced.";
            if (n.track != c.owner && n.card.card.chord?.requiresAdjacent == true && !SpatialRules.Support(voice, Performer(c.owner))) return "An adjacent collaborator is no longer available.";
            return null;
        }

        // ===== RELEASE =====

        private DeckCard Composed(Chord c)
        {
            var sounded = c.notes.Skip(1).Where(n => n.sounded && !n.failed && n.kind != BattleNoteKind.SupportingMajor && n.card.card.chord != null).Select(n => n.card.card);
            var combined = BattleChordLogic.Compose(c.core.card, sounded, ct);
            if (c.drilled) combined.minors = c.notes.Skip(1).Where(n => n.sounded && !n.failed).Select(NoteBinding).Distinct().ToList();
            return new DeckCard { card = combined, voice = c.core.voice, legend = c.core.legend, scale = c.core.scale, major = c.core.major, source = c.core.source };
        }

        private string CoreFailure(Chord c, DeckCard composed)
        {
            var s = c.side; var voice = Performer(c.owner);
            if (voice == null || c.owner.unit != null && !c.owner.unit.Standing || c.owner.unit == null && !s.Conducted) return "The performer is incapacitated.";
            if (c.drilled) return c.target != null && !c.target.Standing ? "The declared target is gone." : null;
            string why = WhyNot(s, c.core) ?? TargetFailure(s, composed, c.target);
            if (why != null) return why;
            if ((c.core.card.requiresPosition || c.footing == BattleFooting.Anchored) && voice.battleHex != c.origin) return "The Core lost its committed position.";
            if ((c.core.card.requiresPosition || c.core.card.purpose == SpellPurpose.Setup) && c.stanceHeld == BattleStanceHold.Held && s.side.StanceBroken) return "Stance Break invalidated prepared positional safety.";
            if (c.legato && c.magical && c.majorBeat - Math.Max(c.firstBeat, c.ChannelStart) + 1 < Math.Min(c.channel, mt.legatoAnchor - 1)) return "The channel did not sound long enough to anchor.";
            return null;
        }

        private static bool Adaptive(Chord c) => c.drilled || c.core.card.effects.Any(e => e != null && e.proximity != BattleProximity.Any);

        private ReleaseFallback Fallback(Chord c, DeckCard composed)
        {
            var e = composed.card.effects.FirstOrDefault(x => x != null && (x.aim == CardAim.Enemy || x.aim == CardAim.Ally));
            if (e == null) return null;
            var pool = EffectTargets(c.side, composed, e);
            var origin = Performer(c.owner);
            var next = pool.OrderBy(x => SpatialRules.Distance(origin.battleHex, x.battleHex)).ThenBy(x => ownerOf[x].side.sections.IndexOf(x)).FirstOrDefault();
            return next == null ? null : new ReleaseFallback { target = next };
        }

        private sealed class ReleaseFallback { public CombatSection target; }

        private Release ReleaseChord(Chord c)
        {
            var s = c.side;
            foreach (var n in c.notes.Skip(1).Where(n => !n.sounded && !n.failed && n.kind != BattleNoteKind.SupportingMajor).ToList()) Fail(c, n, "Left unresolved when its Major released.");
            var composed = Composed(c);
            if (c.core.card.corrupted || c.core.card.cathartic) c.target = CrisisTarget(c.side, composed);
            // A Charge's impact Step happens at the moment of collision, before the strike is read.
            bool advance = composed.card.chord?.advance == true || c.notes.Skip(1).Any(n => n.sounded && n.card.card.chord?.advance == true);
            if (advance && c.owner.unit?.Standing == true && c.target?.Standing == true && !Charge(c, Performer(c.owner)))
            { Suspend(c, "The Core's promised advance has no legal destination."); return null; }
            string why = CoreFailure(c, composed);
            if (why != null && Adaptive(c) && c.owner.unit?.Standing != false && Fallback(c, composed) is ReleaseFallback fallback)
            {
                ChordEvent(s, c.id, BattleChordCause.Fallback, $"{why} It resolves through Rank Proximity against {fallback.target.name} instead.");
                c.target = fallback.target; why = CoreFailure(c, composed);
            }
            if (why != null)
            {
                if (c.drilled && !c.spell) { c.state = BattleChordState.Resolved; return null; }
                Suspend(c, why); return null;
            }
            if (BattleMeasureMath.Grade(c.Major.rendition) == BattleExecution.Missed && !c.Major.missApplied)
            { c.Major.missApplied = true; AddInterference(c, 1, BattleChordCause.Missed, "The Major Note was Missed.", 0); }
            bool perfect = c.notes.All(n => !n.failed && !n.flicker && BattleMeasureMath.Grade(n.rendition) == BattleExecution.Perfect);
            if (perfect) ShedEcho(c);
            float rendition = perfect ? 1f + mt.perfectPower * Math.Min(1f, (c.Major.rendition - 1f) / Math.Max(.001f, mt.perfectPower)) : BattleMeasureMath.Grade(c.Major.rendition) == BattleExecution.Missed ? 1f : Math.Min(1f, c.Major.rendition);
            var performer = Performer(c.owner);
            var root = RootOf(composed, performer);
            bool magical = c.spell || composed.card.effects.Any(e => e?.op == CardOp.Spell);
            var ctx = new Release { chord = c, side = s, root = root, syncopated = c.Syncopated, magical = magical,
                physical = !magical || composed.card.effects.Any(e => e != null && (e.op == CardOp.Strike || e.op == CardOp.Push)) };
            // Contributing voices pay their own Essence when the working they joined releases.
            foreach (var n in c.notes.Skip(1).Where(n => n.sounded && n.track != c.owner && IsWeaving(n.card.card))) { var v = Performer(n.track); if (v != null) v.casts++; }
            var saved = hits; hits = ctx.hits; release = ctx; actingVoice = performer; actingAction = c.id;
            int before = report.plays.Count;
            try
            {
                if (c.drilled) ResolveDrill(s, c.owner.unit, c.target, c.seed, c.spell, root, composed.card.minors, collided.TryGetValue(c.owner, out int hit) && hit == m);
                else
                {
                    Execute(s, new Queued { index = c.Major.handIndex, target = c.target, rendition = rendition, origin = c.origin, frozen = composed, seed = c.seed,
                        action = c.id, explicitChord = true, stanceHeld = c.stanceHeld == BattleStanceHold.Held, chord = c });
                    if (c.climax) ReleaseSupportingMajors(c);
                }
            }
            finally { hits = saved; release = null; }
            foreach (var play in report.plays.Skip(before)) { play.action = c.id; play.beat = beat; }
            // Bonded performers steady one another as the shared working lands.
            var bonded = c.notes.Skip(1).Where(n => n.sounded && n.track != c.owner).Select(n => Performer(n.track)).Where(v => v != null && Bond(s, v, performer) >= ct.bondThreshold).Distinct().ToList();
            foreach (var v in bonded.Concat(bonded.Count > 0 ? new[] { performer } : Array.Empty<CombatSection>()))
                v.composure = Math.Min(v.maxComposure, v.composure + ct.bondPower * 10f);
            c.state = BattleChordState.Resolved;
            if (!c.drilled) ChordEvent(s, c.id, BattleChordCause.Resolved, $"{composed.Name} resolved on Beat {BattleMeasureMath.Rel(beat)}{(perfect ? " in Perfect Echo" : string.Empty)}.");
            return ctx;
        }

        private void ShedEcho(Chord c)
        {
            if (!c.echoShed && c.notes.All(n => (n.sounded || n == c.Major) && !n.failed && !n.flicker && BattleMeasureMath.Grade(n.rendition) == BattleExecution.Perfect) && c.interference > 0)
            {
                c.echoShed = true;
                AddInterference(c, -1, BattleChordCause.PerfectEcho, "Every Note was a Perfect Echo: the Chord sheds one Interference.");
            }
        }

        /// <summary>A Charge: one impact Step toward the Core's target at the moment of collision.</summary>
        private bool Charge(Chord c, CombatSection performer)
        {
            if (c.target == null || performer == null) return true;
            var s = c.side;
            if (performer.battleHex == c.target.battleHex || SkirmishOf(performer) != null) return true;
            int next = BattleHexLayout.Adjacent(performer.battleHex).Where(h => SpatialRules.Passable(h))
                .Where(h => SpatialRules.Distance(h, c.target.battleHex) < SpatialRules.Distance(performer.battleHex, c.target.battleHex))
                .OrderBy(h => SpatialRules.Distance(h, c.target.battleHex)).ThenBy(h => h).DefaultIfEmpty(-1).First();
            if (next < 0) return false;
            Relocate(s, performer, next, BattleSpatialCause.Movement, "The Charge's impact Step.");
            c.origin = next;
            Contacts(new List<Track> { c.owner });
            // Impetus belongs to a collision: only a Charge that makes contact strikes with it.
            if (SkirmishOf(performer) != null) collided[c.owner] = m;
            return true;
        }

        private void StabilizationCadences()
        {
            foreach (var c in chords.Values.Where(c => c.Live && c.hyperMeasures > 1 && c.majorBeat > beat).OrderBy(c => c.id).ToList())
            {
                if (BattleMeasureMath.Grade(c.Major.rendition) == BattleExecution.Missed) { Collapse(c, "A Missed Stabilization Cadence."); continue; }
                c.stabilizations++; c.criticality++;
                ChordEvent(c.side, c.id, BattleChordCause.StabilizationCadence, $"Stabilization Cadence: the Hold extends four Beats; Criticality {c.criticality}.", strain: c.interference);
            }
        }

        // ===== IMPACT AND ABJURATION =====

        private static float Weight(Hit h) => h.integrity + h.direct + h.attacks + .5f * h.composure;

        private void Impact(List<Release> contexts, bool cadence)
        {
            var targets = contexts.SelectMany(c => c.hits.Keys.Concat(c.deferred.Select(x => x.target)).Concat(c.pushes.Select(x => x.target))).Distinct()
                .OrderBy(x => ownerOf[x].attacker ? 0 : 1).ThenBy(x => ownerOf[x].side.sections.IndexOf(x)).ToList();
            var results = new List<(CombatSection target, float integrity, float composure, bool friendly, CombatSection actor)>();
            foreach (var target in targets)
            {
                var owner = ownerOf[target];
                var parts = contexts.Where(c => c.hits.ContainsKey(target) || c.deferred.Any(x => x.target == target) || c.pushes.Any(x => x.target == target)).ToList();
                var pooled = new Hit();
                foreach (var h in parts.Where(c => c.hits.ContainsKey(target)).Select(c => c.hits[target]))
                {
                    pooled.integrity += h.integrity; pooled.direct += h.direct; pooled.composure += h.composure; pooled.attacks += h.attacks; pooled.pierce += h.pierce;
                    pooled.dread += h.dread; pooled.armorBypass += h.armorBypass; pooled.guardBypass += h.guardBypass; pooled.sure += h.sure; pooled.friendlyFire |= h.friendlyFire;
                }
                var (integrity, composure) = Harm(target, pooled, owner);
                float total = parts.Where(c => c.hits.ContainsKey(target)).Sum(c => Weight(c.hits[target])), fi = 0f, fc = 0f;
                foreach (var part in parts)
                {
                    float w = total <= 0f ? 1f / parts.Count : part.hits.TryGetValue(target, out var hit) ? Weight(hit) / total : 0f;
                    float i = integrity * w, c = composure * w;
                    if (part.side != owner && !part.reaction) Abjure(part, target, ref i, ref c, cadence && !part.syncopated);
                    fi += i; fc += c;
                }
                results.Add((target, fi, fc, pooled.friendlyFire, parts.Select(p => p.chord == null ? null : Performer(p.chord.owner)).FirstOrDefault(x => x != null)));
            }
            foreach (var r in results) Resolve(r.target, r.integrity, r.composure, r.friendly, r.actor);
            foreach (var ctx in contexts)
                foreach (var (target, apply) in ctx.deferred) if (!ctx.cancelled.Contains(target)) apply();
            PumpCascade();
            // Forced displacement: knockbacks, pushes and pulls resolve after the impact.
            foreach (var ctx in contexts)
                foreach (var (owner, target) in ctx.pushes) if (!ctx.cancelled.Contains(target) && target.Standing) { Retreat(owner, target, displaced: true); Pressure(); PumpCascade(); }
            foreach (var ctx in contexts.Where(c => c.chord != null))
            {
                var hostile = ctx.hits.Keys.Concat(ctx.deferred.Select(x => x.target)).Concat(ctx.pushes.Select(x => x.target)).Distinct().Where(x => ownerOf[x] != ctx.side).ToList();
                ctx.chord.releaseCancelled = hostile.Count > 0 && hostile.All(ctx.cancelled.Contains);
            }
        }

        /// <summary>
        /// Abjuration: the Ward's Strength is subtracted (a Missed response halves it first), what passes through is reduced
        /// by the performance (0% Missed, 20% Clean, up to 45% Perfect), and a Ward at least as strong as the release on a
        /// Clean or better response cancels it outright, statuses and displacement included. A syncopated strike meets the
        /// standing Ward at Clean, with no performance reduction. Uncovered releases land in full.
        /// </summary>
        private void Abjure(Release ctx, CombatSection target, ref float integrity, ref float composure, bool window)
        {
            float amplitude = integrity + .5f * composure;
            bool statusOnly = amplitude <= 0f;
            if (statusOnly) amplitude = Math.Max(1f, ctx.chord?.power ?? 1f);
            var ward = BestWard(target, ctx, out float strength);
            if (ward == null) return;
            float rendition = window && responses.TryGetValue((ctx, target), out float performed) ? performed : ward.rendition;
            var grade = window ? BattleMeasureMath.Grade(rendition) : BattleExecution.Clean;
            if (grade == BattleExecution.Missed) strength *= .5f;
            bool cancel = strength >= amplitude && grade != BattleExecution.Missed;
            float pass = cancel ? 0f : Math.Max(0f, amplitude - strength);
            if (window && !cancel) pass *= 1f - (grade == BattleExecution.Perfect ? mt.cleanAbjuration + (mt.perfectAbjuration - mt.cleanAbjuration) *
                Math.Min(1f, (rendition - 1f) / Math.Max(.001f, mt.perfectPower)) : grade == BattleExecution.Clean ? mt.cleanAbjuration : 0f);
            float ratio = pass / amplitude;
            integrity *= ratio; composure *= ratio;
            if (cancel)
            {
                ctx.cancelled.Add(target);
                if (ctx.chord != null) ChordEvent(ctx.side, ctx.chord.id, BattleChordCause.Cancelled, $"{target.name}'s Ward ({strength:0}) cancelled the release ({amplitude:0}) outright.");
            }
        }

        private GuardLink BestWard(CombatSection target, Release ctx, out float best)
        {
            best = 0f;
            var owner = ownerOf[target];
            var marks = owner.Marks(target);
            if (marks == null) return null;
            var only = ctx.chord?.core.card.abjuredOnlyBy ?? new List<string>();
            GuardLink chosen = null;
            foreach (var w in marks.wards.Where(w => w.raised <= beat && w.expires >= beat && w.source.Standing && !w.source.mindBroken &&
                SpatialRules.Support(w.source, target, w.range) && (w.chord == null || w.chord.Live || w.chord.state == BattleChordState.Resolved)))
            {
                if (only.Count > 0 && !only.Contains(w.card)) continue;
                if (beat - w.raised < (ctx.chord?.core.card.minimumWardBeats ?? 0)) continue;
                if (!(ctx.physical && w.physical || ctx.magical && w.magical)) continue;
                float strength = w.amount * (1f + mt.wardGrowth * Math.Max(0, Math.Min(beat, Cadence) - w.raised));
                if (w.binding != SpellBinding.Unattuned && ctx.root != SpellBinding.Unattuned)
                {
                    if (HarmonicCircle.Multiplier(w.binding, ctx.root, t) > 1f) strength *= mt.wardCounters;
                    else if (HarmonicCircle.Multiplier(ctx.root, w.binding, t) > 1f) strength *= mt.wardCountered;
                }
                if (strength > best) { best = strength; chosen = w; }
            }
            return chosen;
        }

        // ===== COHERENCE =====

        /// <summary>Notes made impossible during the Beat become Interference; Majors that can no longer resolve become Suspended.</summary>
        private void Coherence()
        {
            foreach (var c in chords.Values.Where(c => c.Live).OrderBy(c => c.id).ToList())
            {
                foreach (var n in c.notes.Skip(1).Where(n => !n.sounded && !n.failed).ToList())
                {
                    var voice = Performer(n.track);
                    if (voice == null || !voice.Standing) Fail(c, n, "The contributing voice fell before its Note.");
                    else if (n.track != c.owner && n.card.card.chord?.requiresAdjacent == true && voice.battleHex != n.origin && !SpatialRules.Support(voice, Performer(c.owner)))
                        Fail(c, n, "The contributor was displaced out of the Ensemble.");
                }
                if (c.state == BattleChordState.Suspended || c.drilled) continue;
                var performer = Performer(c.owner);
                if (c.owner.unit != null && !c.owner.unit.Standing) Suspend(c, "Its performer was incapacitated.");
                else if (c.target != null && !c.target.Standing && !Adaptive(c) && !c.ward) Suspend(c, "Its target is gone.");
            }
            PruneSympathy();
        }

        private void Suspend(Chord c, string why)
        {
            if (c.state == BattleChordState.Suspended || !c.Live) return;
            c.state = BattleChordState.Suspended;
            ChordEvent(c.side, c.id, BattleChordCause.Suspended, why + " The Chord is Suspended: still sounding, still held, seeking a resolution.", strain: c.interference);
            report.plays.Add(new CardPlay { measure = m, beat = beat, action = c.id, attacker = c.side.attacker, card = c.core.card.id, cardName = c.core.Name,
                voice = c.owner.Name, target = c.target?.name, purpose = c.core.card.purpose, failed = true, failure = why });
            Emit(c.side, Performer(c.owner), BattleSpatialCause.PreparationFailed, why);
        }

        // ===== COLLAPSE =====

        /// <summary>
        /// The universe resolves an unresolved composition with the composer's own side: the performer first, then every
        /// contributing Track, then the nearest allied positions. It never strikes the enemy. A Hyper Chord detonates.
        /// </summary>
        private void Collapse(Chord c, string why)
        {
            if (!c.Live) return;
            var s = c.side;
            c.state = BattleChordState.Collapsed;
            int sounded = Math.Max(0, beat - c.firstBeat);
            float amplitude = c.magical ? Math.Max(mt.collapseFloor, c.power) * Math.Max(.25f, Math.Min(BattleMeasureMath.Beats, sounded) / (float)BattleMeasureMath.Beats) * (1f + .25f * c.stabilizations) : 0f;
            amplitude *= 1f + TempoPercentage(c.side.attacker) / 100f;
            var performer = Performer(c.owner);
            // Resolution Climax: a working performed or contributed to by a Sympathetic Track rings through all of them.
            var ring = SympatheticRing(c);
            float integrity = 0f, composure = 0f;
            var struck = new Dictionary<CombatSection, float>();
            void Strike(CombatSection x, float share, bool friendly)
            {
                if (x == null || !x.Standing || share <= 0f || amplitude <= 0f) return;
                var hit = HitOf(hits, x); hit.friendlyFire |= friendly;
                hit.integrity += amplitude * mt.collapseIntegrity * share; hit.composure += amplitude * mt.collapseComposure * share;
                integrity += amplitude * mt.collapseIntegrity * share; composure += amplitude * mt.collapseComposure * share;
                struck[x] = (struck.TryGetValue(x, out float before) ? before : 0f) + share;
            }
            if (c.hyperMeasures > 1 && performer != null)
            {
                // Static Criticality: a Coherence Detonation cascading into adjacent hexes, harming everyone in them.
                var blast = a.side.Standing.Concat(d.side.Standing).Where(x => x.battleHex == performer.battleHex || BattleHexLayout.AreAdjacent(x.battleHex, performer.battleHex)).ToList();
                foreach (var x in blast) Strike(x, 1f / Math.Max(1, blast.Count) * 2f, ownerOf[x] == s);
                ChordEvent(s, c.id, BattleChordCause.StaticCriticality, why + " Static Criticality: the detonation spares no one nearby.", strain: c.interference, integrity: integrity, composure: composure);
            }
            else
            {
                var contributors = c.notes.Skip(1).Select(n => Performer(n.track)).Where(x => x != null && x != performer && x.Standing).Distinct().ToList();
                var spill = performer == null ? new List<CombatSection>() : s.side.Standing.Where(x => x != performer && !contributors.Contains(x) &&
                    SpatialRules.Distance(performer.battleHex, x.battleHex) <= SpatialRules.Tuning.supportReach).ToList();
                if (spill.Count > 0) { int near = spill.Min(x => SpatialRules.Distance(performer.battleHex, x.battleHex)); spill = spill.Where(x => SpatialRules.Distance(performer.battleHex, x.battleHex) == near).ToList(); }
                bool performerStands = performer != null && performer.Standing && c.owner.unit != null;
                float own = mt.collapsePerformer, theirs = contributors.Count > 0 ? mt.collapseContributors : 0f, rest = spill.Count > 0 ? Math.Max(0f, 1f - mt.collapsePerformer - mt.collapseContributors) : 0f;
                if (!performerStands) { own = 0f; if (contributors.Count > 0) theirs = 1f - rest; else if (spill.Count > 0) rest = 1f; }
                else own = 1f - theirs - rest;
                Strike(performerStands ? performer : null, own, true);
                foreach (var x in contributors) Strike(x, theirs / contributors.Count, true);
                foreach (var x in spill) Strike(x, rest / spill.Count, true);
                if (c.owner.unit == null && amplitude > 0f) { s.bar -= amplitude * mt.collapseComposure * own; composure += amplitude * mt.collapseComposure * own; }
                if (performer != null)
                    foreach (var witness in s.side.Standing.Where(x => x != performer && !contributors.Contains(x) && !spill.Contains(x) && BattleHexLayout.AreAdjacent(x.battleHex, performer.battleHex)).ToList())
                        Shock(s, witness, mt.collapseWitness);
                ChordEvent(s, c.id, BattleChordCause.Collapsed, why + " The entire stack collapses; its power returns along the channels that built it.", strain: c.interference, integrity: integrity, composure: composure);
            }
            if (ring.Count > 0)
            {
                // The backlash rings through every Sympathetic Track at the working's full amplified Amplitude.
                foreach (var track in ring)
                {
                    var voice = Performer(track);
                    if (track.unit == null) { if (amplitude > 0f) s.bar -= amplitude * mt.collapseComposure; continue; }
                    Strike(voice, Math.Max(0f, 1f - (struck.TryGetValue(voice, out float taken) ? taken : 0f)), true);
                }
                SympatheticFall(s, ring, c);
            }
            report.plays.Add(new CardPlay { beat = beat, measure = m, action = c.id, attacker = s.attacker, card = c.core.card.id, cardName = c.core.Name, voice = c.owner.Name,
                target = c.target?.name, purpose = c.core.card.purpose, failed = true, failure = why });
            foreach (var minor in c.notes.Skip(1)) report.plays.Add(new CardPlay { beat = beat, measure = m, action = c.id, attacker = s.attacker, minor = true,
                card = minor.card.card.id, cardName = minor.card.Name, voice = minor.track.Name, target = c.target?.name, purpose = minor.card.card.purpose, failed = true,
                failure = "The entire Core's stack collapsed: " + why });
            foreach (var k in marks.Values) k.wards.RemoveAll(w => w.chord == c);
            Emit(s, performer, BattleSpatialCause.PreparationFailed, why);
            QueueReaction(BattleReactionTrigger.WeaveCollapsed, s, performer, performer, c.id);
            FinishChord(c);
            // Linked workings resolve together or collapse together.
            if (c.climax) foreach (var linked in LinkedClimaxes(c).ToList()) Collapse(linked, "A linked Climax working collapsed.");
        }

        private void FinishChord(Chord c)
        {
            if (c.climax) RememberPerformance(c);
            if (c.state == BattleChordState.Resolved && c.Major.handIndex >= 0 && c.core.card.pollutionAfter != BattlePollution.None && c.owner.unit?.Standing == true)
                Pollute(c.side.attacker, c.owner.voice, c.core.card.pollutionAfter, intoDiscard: true);
            ResolvedCrisis(c);
            var p = c.side.perf;
            groundAt.Remove(c.id);
            if (p == null || c.drilled) return;
            foreach (var n in c.notes.Where(n => n.handIndex >= 0))
            {
                if (n.card.card.exhaust) p.exhausted.Add(n.handIndex);
                else if (!p.discard.Contains(n.handIndex) && !p.hand.Contains(n.handIndex)) p.discard.Add(n.handIndex);
                n.handIndex = -1;
            }
        }

        // ===== SUSPENDED: FALLBACK, STABILIZE, GROUND =====

        /// <summary>Stabilize a Suspended Chord in Composition: a new valid target on an upcoming Beat, at the price of one Interference.</summary>
        public string Stabilize(bool attacker, long chord, int target = -1, int beatOfMeasure = 0)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s); if (why != null) return why;
            if (!chords.TryGetValue(chord, out var c) || c.side != s || c.state != BattleChordState.Suspended) return "Only a Suspended Chord can be stabilized.";
            var composed = Composed(c);
            var aimed = AimsAtEnemy(composed.card) ? s.enemy : s;
            CombatSection chosen = target >= 0 && target < aimed.side.sections.Count ? aimed.side.sections[target] : Fallback(c, composed)?.target;
            if (chosen == null) return "No valid target remains for this working.";
            planning = true;
            try { why = TargetFailure(s, composed, chosen); } finally { planning = false; }
            if (why != null) return why;
            int rel = beatOfMeasure > 0 ? beatOfMeasure : BattleMeasureMath.Beats;
            if (rel < 1 || rel > BattleMeasureMath.Beats || rel != BattleMeasureMath.Beats && !c.staccato) return "A Stabilized Major resolves at the Cadence unless it is Staccato.";
            // Still sounding and still held: the working occupies its performer's Track until its new resolution.
            c.target = chosen; c.majorBeat = Abs(rel); c.state = BattleChordState.Sounding; c.channel = Math.Min(BattleMeasureMath.Beats, c.majorBeat - Math.Max(c.firstBeat, Abs(1)) + 1);
            AddInterference(c, 1, BattleChordCause.Stabilized, $"Stabilized against {chosen.name} on Beat {rel}: each Stabilization adds one Interference.");
            return null;
        }

        /// <summary>Ground a Suspended Chord: its performer spends the next Beat discharging it through their own body; it spills onto no one else.</summary>
        public string Ground(bool attacker, long chord)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s); if (why != null) return why;
            if (!chords.TryGetValue(chord, out var c) || c.side != s || c.state != BattleChordState.Suspended) return "Only a Suspended Chord can be grounded.";
            if (c.owner.unit == null || !c.owner.unit.Standing) return "An incapacitated performer cannot ground the working.";
            int at = Enumerable.Range(Abs(1), BattleMeasureMath.Beats).FirstOrDefault(b => !Rests(s)[BattleMeasureMath.Rel(b)] && !Occupied(c.owner, b, c));
            if (at <= 0) return "The performer has no free Beat this Measure.";
            groundAt[c.id] = at;
            return null;
        }

        private void GroundNow(Chord c)
        {
            var s = c.side; var performer = Performer(c.owner);
            c.state = BattleChordState.Grounded;
            int sounded = Math.Max(0, beat - c.firstBeat);
            float amplitude = c.magical ? Math.Max(mt.collapseFloor, c.power) * Math.Max(.25f, Math.Min(BattleMeasureMath.Beats, sounded) / (float)BattleMeasureMath.Beats) : 0f;
            // Grounding takes the whole Collapse through one body; Tempo Fever raises that Amplitude like any friendly Collapse.
            amplitude *= 1f + TempoPercentage(s.attacker) / 100f;
            if (performer != null && performer.Standing && amplitude > 0f)
            {
                var hit = HitOf(hits, performer); hit.friendlyFire = true;
                hit.integrity += amplitude * mt.collapseIntegrity; hit.composure += amplitude * mt.collapseComposure;
            }
            foreach (var k in marks.Values) k.wards.RemoveAll(w => w.chord == c);
            ChordEvent(s, c.id, BattleChordCause.Grounded, "Grounded through the performer's own body: the Collapse spills onto no one else.", strain: c.interference,
                integrity: amplitude * mt.collapseIntegrity, composure: amplitude * mt.collapseComposure);
            FinishChord(c);
        }

        /// <summary>The performer's answer to its own Suspended Chords: Stabilize when a valid target leaves margin, else Ground it.</summary>
        private void ResolveSuspensions(Side s)
        {
            foreach (var c in chords.Values.Where(c => c.side == s && c.state == BattleChordState.Suspended).OrderBy(c => c.id).ToList())
            {
                var next = Fallback(c, Composed(c))?.target;
                if (next != null && Cadence - c.firstBeat + 1 <= HoldLimit(c, false) - 1 && Stabilize(s.attacker, c.id, ownerOf[next].side.sections.IndexOf(next)) == null) continue;
                Ground(s.attacker, c.id);
            }
        }

        // ===== INTERRUPTION AND TEMPO =====

        /// <summary>Incoming harm past the performer's threshold (Cindergale mastery raises it) interrupts its Braced or Anchored working.</summary>
        private void Interruption(CombatSection unit, float loss)
        {
            if (unit == null || loss <= 0f) return;
            var track = TrackOf(unit); if (track == null) return;
            var legend = LegendOf(track);
            float threshold = mt.interruptShare * unit.maxIntegrity * (1f + mt.cindergaleInterrupt * (legend == null ? 0 : BattleMeasureMath.Mastery(legend.Score(SpellBinding.Cindergale))));
            if (loss < threshold) return;
            foreach (var c in chords.Values.Where(c => c.Live && c.owner == track && c.firstBeat <= beat && c.footing >= BattleFooting.Braced && interruptedThisBeat.Add(c.id)).ToList())
                AddInterference(c, 1, BattleChordCause.Interrupted, $"{unit.name} was struck while channeling: an Interruption.");
        }

        private void InterruptDisplaced(CombatSection unit)
        {
            var track = TrackOf(unit); if (track == null) return;
            foreach (var c in chords.Values.Where(c => c.Live && c.owner == track && c.firstBeat <= c.majorBeat && c.footing >= BattleFooting.Braced && interruptedThisBeat.Add(c.id)).ToList())
                AddInterference(c, 1, BattleChordCause.Interrupted, $"{unit.name} was displaced while {c.footing}: an Interruption.");
        }

        /// <summary>Ritardando and Disruption push a working's unsounded Notes later; Accelerando pulls its Major earlier, leaving unsounded Minors unresolved.</summary>
        private void Shift(CombatSection unit, int beats)
        {
            var track = TrackOf(unit); if (track == null || beats == 0) return;
            foreach (var c in chords.Values.Where(c => c.Live && c.owner == track && c.majorBeat > beat).OrderBy(c => c.id).ToList())
            {
                if (beats > 0)
                {
                    c.majorBeat += beats;
                    foreach (var n in c.notes.Skip(1).Where(n => !n.sounded && !n.failed)) n.beat += beats;
                    if (c.firstBeat > beat) c.firstBeat += beats;
                    ChordEvent(c.side, c.id, BattleChordCause.Delayed, $"Delayed {beats} Beat{(beats == 1 ? "" : "s")}: it now resolves on Beat {c.majorBeat}.", strain: c.interference);
                }
                else
                {
                    int start = c.ChannelStart;
                    c.majorBeat = Math.Max(beat + 1, c.majorBeat + beats);
                    if (c.firstBeat <= beat) c.channel = Math.Max(1, Math.Min(c.channel, c.majorBeat - Math.Max(c.firstBeat, start) + 1));
                    else { c.channel = Math.Max(1, Math.Min(c.channel, c.majorBeat - beat)); c.firstBeat = Math.Min(c.firstBeat, c.ChannelStart); }
                    foreach (var n in c.notes.Skip(1).Where(n => !n.sounded && !n.failed && n.beat >= c.ChannelStart).ToList())
                        Fail(c, n, "Left unresolved when its Major was pulled forward.");
                    ChordEvent(c.side, c.id, BattleChordCause.Accelerated, $"Accelerated: it now resolves on Beat {c.majorBeat}.", strain: c.interference);
                }
            }
        }

        private void SupportChords(Side s, CardEffect effect, CombatSection target, float amount)
        {
            var track = target == null ? null : TrackOf(target);
            int x = Math.Max(1, (int)Math.Round(amount));
            switch (effect.op)
            {
                case CardOp.ExtendHold:
                case CardOp.WeaveCapacity:
                    foreach (var c in chords.Values.Where(c => c.Live && c.owner == track))
                        c.fermata += effect.op == CardOp.WeaveCapacity ? 1 : Math.Min(mt.fermataCap, x);
                    break;
                case CardOp.Stabilize:
                    foreach (var c in chords.Values.Where(c => c.side == s && c.state == BattleChordState.Suspended && c.owner == track).ToList())
                    {
                        var next = Fallback(c, Composed(c))?.target;
                        if (next == null) { ChordEvent(s, c.id, BattleChordCause.StabilizationFailed, "No valid target remained to stabilize toward."); continue; }
                        c.target = next; c.majorBeat = Math.Max(beat + 1, Cadence); c.state = BattleChordState.Sounding;
                        AddInterference(c, 1, BattleChordCause.Stabilized, $"A helper stabilized the working toward {next.name}.");
                    }
                    break;
                case CardOp.Anchor:
                    if (target != null && target.battleHex >= 0) { anchors.TryGetValue(target.battleHex, out int laid); anchors[target.battleHex] = laid + 1; }
                    break;
            }
        }

        // ===== VIEWS =====

        public IReadOnlyList<BattleWeaveView> PreparedWeaves => chords.Values.Where(c => c.Live && !c.drilled).OrderBy(c => c.id).Select(c => new BattleWeaveView
        {
            action = c.id, attacker = c.side.attacker, core = c.core.Name, coreLost = c.state == BattleChordState.Suspended, suspended = c.state == BattleChordState.Suspended,
            state = c.state, footing = c.footing, firstBeat = c.firstBeat, dueBeat = c.majorBeat, holdLimit = HoldLimit(c, c.firstBeat > beat),
            deadline = c.firstBeat + HoldLimit(c, c.firstBeat > beat) - 1, sounding = Math.Max(0, Math.Min(beat, c.majorBeat) - c.firstBeat + 1),
            margin = HoldLimit(c, c.firstBeat > beat) - (c.majorBeat - c.firstBeat + 1), interference = c.interference, attempts = c.stabilizations,
            criticality = c.criticality, hyper = c.hyperMeasures > 1, load = c.notes.Count, capacity = HoldLimit(c, c.firstBeat > beat),
            awaitingTrigger = false, compatibility = c.notes.Skip(1).Count(n => n.track != c.owner && Bond(c.side, Performer(n.track), Performer(c.owner)) >= ct.bondThreshold),
            notes = c.notes.Select(n => n.card.Name).ToList(),
        }).ToList();
    }
}
