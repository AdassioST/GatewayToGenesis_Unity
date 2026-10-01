using System;
using System.Collections.Generic;
using System.Linq;

// The four-Beat Measure (vault: Combat System.md, "The Temporal Economy"): Visualize -> Compose -> Commit -> Beat 1 ->
// Beat 2 -> Beat 3 -> Beat 4 / Cadence -> Abjure -> Toll -> Assess. Composition is held time and costs nothing; once
// both Scores are committed every Track performs its four Beats at the same time, friend and enemy together.
public static partial class BattleResolver
{
    /// <summary>One voice of the Score: a combatant (or, at larger scales, a section), or the abstract Conductor (voice -1).</summary>
    private sealed class Track
    {
        public Side side;
        public CombatSection unit;
        public int voice;
        public BattleStandingOrder order;
        /// <summary>Hexes still to walk, in order; a path longer than the Pace continues next Measure.</summary>
        public readonly List<int> path = new List<int>();
        /// <summary>This Measure's Steps: the absolute Beat each one lands on and its hex.</summary>
        public readonly List<(int beat, int hex)> steps = new List<(int, int)>();
        public readonly List<int> stepBeats = new List<int>();
        public bool pathComposed, spotlit, rushing, charging;
        public bool detuned, attuned, duet, overrode, fever, feverExtra;
        public int perfectPhrases;
        public string Name => unit?.name ?? side.conductor?.name ?? side.side.name;
    }

    private sealed class ChordNote
    {
        public DeckCard card;
        public Track track;
        public int beat, handIndex = -1, origin = -1;
        /// <summary>A Supporting Major's first sounding Beat (its own channel inside a fused finale).</summary>
        public int from;
        /// <summary>A Supporting Major's own committed target, used when it cannot add its power to the Core's.</summary>
        public CombatSection target;
        public BattleNoteKind kind;
        public float rendition = 1f;
        public bool sounded, failed, flicker, measured, missApplied, overbeat;
        /// <summary>A finishing Major already performed on its Overbeat; it releases at the Cadence with its Core.</summary>
        public bool heard;
        public string failure;
    }

    /// <summary>One Major Note with the Minor Notes built around it, on one or more Tracks (an Ensemble).</summary>
    private sealed class Chord
    {
        public long id;
        public Side side;
        public Track owner;
        public DeckCard core;
        public CombatSection target;
        public readonly List<ChordNote> notes = new List<ChordNote>();
        public int majorBeat, channel, firstBeat, origin, interference, fermata, stabilizations, criticality, hyperMeasures, seed, composedMeasure;
        public BattleFooting footing;
        public bool legato, staccato, drilled, spell, ward, magical, committed, anchored, feverExtra, echoShed, releaseCancelled, overbeat;
        /// <summary>A finishing working of Resolution Climax (a Climax Major, a fused finale or the Grand Resolution); it releases at the Cadence.</summary>
        public bool climax, grand;
        /// <summary>The power of every Supporting Major fused into it, added in full to its release and to its Collapse.</summary>
        public float support;
        /// <summary>The Chords fused into this finale, kept as composed so a Retract in Composition can restore them.</summary>
        public readonly List<Chord> merged = new List<Chord>();
        public BattleChordState state = BattleChordState.Composed;
        public float power, heartbeatShare, heartbeatRendition = 1f;
        public BattleStanceHold stanceHeld;
        public ChordNote Major => notes[0];
        public int ChannelStart => majorBeat - channel + 1;
        public bool Syncopated => BattleMeasureMath.Rel(majorBeat) != BattleMeasureMath.Beats;
        public bool Live => state == BattleChordState.Composed || state == BattleChordState.Sounding || state == BattleChordState.Suspended;
    }

    /// <summary>Whether a Chord's positional safety was composed while its formation's Stance held.</summary>
    private enum BattleStanceHold { Unknown, Held, Broken }

    public sealed partial class BattleRun
    {
        private readonly Dictionary<(bool, int), Track> tracks = new Dictionary<(bool, int), Track>();
        private readonly Dictionary<long, Chord> chords = new Dictionary<long, Chord>();
        private readonly Dictionary<Side, Dictionary<string, int>> behavior = new Dictionary<Side, Dictionary<string, int>>();
        private readonly Dictionary<int, int> anchors = new Dictionary<int, int>();
        private readonly bool[] restA = new bool[BattleMeasureMath.Beats + 1], restD = new bool[BattleMeasureMath.Beats + 1];
        private int ringsA, ringsD, beat;
        private long nextChord = 1;
        private bool scoresCommitted;
        private BattleMeasureTuning mt;
        private CombatSettings combatSettings;

        /// <summary>Absolute Beats performed so far (Measure m's Beats are 4m-3 .. 4m).</summary>
        public int Beat => beat;
        public BattlePhase Phase { get; private set; } = BattlePhase.Visualization;
        /// <summary>The Beat of the open Measure (0 before Beat 1, 4 after the Cadence).</summary>
        public int MeasureBeat => open ? beat - BattleMeasureMath.Absolute(m, 0) : 0;

        private void Enter(BattlePhase phase, long action = 0)
        {
            Phase = phase;
            report.phases.Add(new BattlePhaseEvent { measure = m, beat = beat, phase = phase, action = action });
        }

        private bool Automatic(Side s) => auto || finishing || !s.side.manual;
        private int Cadence => BattleMeasureMath.Cadence(m);
        private int Abs(int rel) => BattleMeasureMath.Absolute(m, rel);
        private bool[] Rests(Side s) => s.attacker ? restA : restD;

        // ===== TRACKS =====

        private void BuildTracks()
        {
            foreach (var s in new[] { a, d })
            {
                for (int i = 0; i < s.side.sections.Count; i++)
                {
                    var unit = s.side.sections[i];
                    tracks[(s.attacker, i)] = new Track { side = s, unit = unit, voice = i, order = DefaultOrder(unit) };
                }
                if (s.side.conductor != null && !s.side.sections.Any(x => x.eliteRole == BattleEliteRole.Conductor))
                    tracks[(s.attacker, -1)] = new Track { side = s, voice = -1, order = BattleStandingOrder.Hold };
            }
        }

        private static BattleStandingOrder DefaultOrder(CombatSection unit)
        {
            if (unit.row == FormationRow.Support || unit.attack <= 0f && !unit.Casts) return BattleStandingOrder.Guard;
            return unit.row == FormationRow.Back ? BattleStandingOrder.Volley : BattleStandingOrder.Advance;
        }

        private Track TrackOf(Side s, int voice)
        {
            if (voice < 0)
            {
                int conductor = s.side.sections.FindIndex(x => x.eliteRole == BattleEliteRole.Conductor);
                if (conductor >= 0) return tracks[(s.attacker, conductor)];
                return tracks.TryGetValue((s.attacker, -1), out var baton) ? baton : null;
            }
            return tracks.TryGetValue((s.attacker, voice), out var t) ? t : null;
        }

        private Track TrackOf(Side s, DeckCard dc) => TrackOf(s, dc.voice);
        private Track TrackOf(CombatSection unit) => ownerOf.TryGetValue(unit, out var s) ? TrackOf(s, s.side.sections.IndexOf(unit)) : null;
        private CombatSection Performer(Track track) => track.unit ?? Origin(track.side, null);

        private IEnumerable<Track> TracksOf(Side s) => tracks.Values.Where(t => t.side == s && (t.unit == null ? s.Conducted : t.unit.Standing));

        /// <summary>Named Legends and the Elite Core are always composed directly; other formation Tracks count against Command Bandwidth.</summary>
        private static bool Elite(Track track) => track.unit == null || track.unit.eliteRole != BattleEliteRole.None || track.unit.leader != null;

        public int Bandwidth(bool attacker)
        {
            var s = Of(attacker);
            int tier = s.Conducted ? BattleMeasureMath.Mastery(s.conductor.Score(SpellBinding.Resonance)) : 0;
            // A controlling Conductor in Mind Break smothers improvisation: fewer formation Tracks are composed.
            int smothered = s.maladaptation == BattleConductorMaladaptation.Controlling ? crisisTuning.controllingBandwidth : 0;
            return Math.Max(0, mt.bandwidthBase + tier - smothered);
        }

        private int ComposedFormations(Side s) => TracksOf(s).Count(t => !Elite(t) && MajorOf(t) != null);

        // ===== PACE =====

        public int PaceOf(bool attacker, int section)
        {
            var track = TrackOf(Of(attacker), section);
            return track == null ? 0 : Pace(track);
        }

        private int Pace(Track track)
        {
            var unit = track.unit;
            if (unit == null || !unit.Standing) return 0;
            int pace = (int)BattleMeasureMath.Pace(unit.speed);
            if (StatusInHand(track.side, unit, BattlePollution.Restrained) || StatusInHand(track.side, unit, BattlePollution.Frostbite)) pace = Math.Max(1, pace - 1);
            if (unit.deathKnell) return 1;
            var hexes = new[] { unit.battleHex }.Concat(track.path.Take(4));
            if (pace == (int)BattlePace.Swift && hexes.Any(h => h >= 0 && Spatial.Terrain[h].snow)) pace -= mt.swiftSnowPenalty;
            if (track.side.side.stance == BattleStance.Spearhead && SpatialRules.DoctrineHeld(track.side.attacker) &&
                track.path.Take(pace + 1).Any(h => BattleHexLayout.At(h).Territory == BattleTerritory.Neutral)) pace += mt.spearheadNeutralStep;
            if (track.charging) pace += mt.chargeStep;
            if (track.rushing) pace *= 2;
            return Math.Max(0, pace);
        }

        private int StepCost(int hex) { var h = Spatial.Terrain[hex]; return h.river && !h.ford && !h.bridge ? Math.Max(1, mt.riverSteps) : 1; }

        // ===== FOOTING =====

        /// <summary>Whether a Track's composed Chords let its performer take a Step on <paramref name="at"/>.</summary>
        private bool MayStep(Track track, int at)
        {
            foreach (var c in chords.Values.Where(c => c.Live && c.owner == track))
            {
                if (c.footing == BattleFooting.Anchored && at >= c.firstBeat && at <= c.majorBeat) return false;
                if (c.footing == BattleFooting.Braced && at >= c.majorBeat - 1 && at <= c.majorBeat) return false;
            }
            return true;
        }

        private BattleFooting FootingOf(Side s, CombatCard card)
        {
            var f = card.footing;
            if (Magic(card) && TempoOf(s, card) == CardTempo.Legato && f < BattleFooting.Braced) f = BattleFooting.Braced;
            return f;
        }

        private CardTempo TempoOf(Side s, CombatCard card)
        {
            if (card.tempo != CardTempo.Inherit) return card.tempo;
            if (card.channeling) return CardTempo.Legato;
            if (card.kind == CardKind.Spell) return (CardTempo)((int)s.side.tempo + 1);
            return CardTempo.Staccato;
        }

        /// <summary>Beats a Major sounds: its channel, at least the Legato anchor (one Beat sooner on a channel, a leyline or with a bonded voice).</summary>
        private int ChannelOf(Side s, CombatCard card, CombatSection performer, bool bonded)
        {
            int channel = card.ChannelBeats;
            if (TempoOf(s, card) == CardTempo.Legato && (Magic(card) || card.channeling))
            {
                int anchor = mt.legatoAnchor;
                var local = performer == null || performer.battleHex < 0 ? null : Spatial.Terrain[performer.battleHex];
                if (local != null && (local.harmonicChannel || local.leyline) || bonded) anchor--;
                int declared = card.channel > 0 ? card.channel : card.actionBeats > 0 ? card.actionBeats : Math.Max(1, card.cost);
                channel = Math.Max(declared, Math.Max(1, anchor));
            }
            var heartbeat = card.effects.FirstOrDefault(e => e?.op == CardOp.CoRegulate);
            if (heartbeat != null) channel = Math.Max(2, Math.Min(3, heartbeat.durationBeats > 0 ? heartbeat.durationBeats : 3));
            return Math.Min(BattleMeasureMath.Beats, channel);
        }

        // ===== TUNING =====

        public BattleTuningState TuningOf(bool attacker, int section) { var t = TrackOf(Of(attacker), section); return t == null ? BattleTuningState.InTune : Tune(t); }

        /// <summary>
        /// The needle, plus Resolution Climax: a member of the Climax's sympathy rings Sympathetic while it stays
        /// synchronized, and a Track struck by a Sympathetic Collapse stands at Detuned (or lower) until Assessment.
        /// </summary>
        private BattleTuningState Tune(Track track)
        {
            var state = Needle(track);
            if (sympatheticFall.Contains(track)) return (BattleTuningState)Math.Min((int)state, (int)BattleTuningState.Detuned);
            if (state >= BattleTuningState.InTune && sympathetic.Contains(track) && tempo[track.side.attacker].Climax && track.side.Conducted &&
                (track.unit == null || track.unit.Standing)) return BattleTuningState.Sympathetic;
            return state;
        }

        private BattleTuningState Needle(Track track)
        {
            var s = track.side;
            float share = track.unit == null ? (s.barMax <= 0f ? 1f : Math.Max(0f, s.bar) / s.barMax) : track.unit.ComposureShare;
            if (share < mt.spiralingBelow) return BattleTuningState.OutOfTune;
            int needle = (int)BattleTuningState.InTune;
            if (share < mt.fracturedBelow) needle--;
            if (s.conductor != null && !s.Conducted) needle--;
            if (track.overrode) needle--;
            if (s.side.StanceBroken) needle--;
            var hex = Performer(track)?.battleHex ?? -1;
            if (hex >= 0 && Spatial.Terrain[hex].dissonance >= mt.dissonantGround) needle--;
            if (track.detuned) needle--;
            // Clouded is the functional baseline: only a soul that entered Pristine and stays near full sounds Pristine.
            var legend = LegendOf(track);
            if (legend != null && legend.State == ComposureState.Pristine && share >= mt.pristineAbove) needle++;
            if (track.duet || track.attuned) needle++;
            if (hex >= 0 && anchors.ContainsKey(hex)) needle++;
            return (BattleTuningState)Math.Max((int)BattleTuningState.OutOfTune, Math.Min((int)BattleTuningState.Resonant, needle));
        }

        /// <summary>Within the Conductor's carrier signal (or bonded to the Chord's owner): able to join an Ensemble.</summary>
        private bool Synchronized(Track contributor, Track owner) =>
            Tune(contributor) >= BattleTuningState.InTune && (contributor.side.Conducted || Bond(contributor.side, Performer(contributor), Performer(owner)) >= ct.bondThreshold);

        // ===== THE SCORE (held time) =====

        private string WhyNotCompose(Side s)
        {
            if (Over) return "The battle is over.";
            if (!open) return "No measure is open.";
            if (scoresCommitted && !composingCrisis) return "The Score is committed; the Measure is being performed.";
            if (s.perf == null) return "This side has no Symphony.";
            return null;
        }

        private bool Occupied(Track track, int at, Chord except = null) =>
            chords.Values.Any(c => c != except && c.Live && (groundAt.TryGetValue(c.id, out int g) ? c.owner == track && g == at :
                c.notes.Any(n => n.track == track && !n.overbeat && !n.failed && (n.kind == BattleNoteKind.Major || n.kind == BattleNoteKind.Ward
                    ? at >= OccupiesFrom(c) && at <= c.majorBeat && c.state != BattleChordState.Suspended
                    : n.kind == BattleNoteKind.SupportingMajor ? at >= n.from && at <= n.beat && !n.sounded : n.beat == at && !n.sounded))));

        /// <summary>The Track's one intention in the open Measure: a composed Chord that sounds in it, a Hyper Chord still sounding, or a Suspended one.</summary>
        private Chord MajorOf(Track track) => chords.Values.FirstOrDefault(c => c.Live && !c.overbeat && !c.climax && c.owner == track && !c.drilled && !groundAt.ContainsKey(c.id) &&
            (c.state == BattleChordState.Suspended || OccupiesFrom(c) <= Cadence && c.majorBeat >= Abs(1)));

        /// <summary>The Track already promised its one intention: its own Chord, or that Chord fused into a Climax finale.</summary>
        private bool HasIntention(Track track) => MajorOf(track) != null || chords.Values.Any(c => c.Live && c.climax &&
            c.notes.Any(n => n.track == track && (n.kind == BattleNoteKind.SupportingMajor || n.kind == BattleNoteKind.Major && !n.overbeat)));

        /// <summary>Compose one card: a Reaction is primed (no Beats), a Ward is raised as the Track's Major, anything else is a Unison Major.</summary>
        public string CommitCard(bool attacker, int handIndex, int target = -1, float rendition = 1f, int beat = 0)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s) ?? WhyNotHand(attacker, handIndex, false);
            if (why != null) return why;
            var dc = s.perf.deck[s.perf.hand[handIndex]];
            if (dc.card.reaction != null) return Prime(s, handIndex, target, rendition);
            return ComposeChord(s, handIndex, new List<int>(), target, rendition, beat, null);
        }

        /// <summary>Compose a Chord: one Core and its ordered Minors (from any synchronized Track), on Beats before the Core.</summary>
        public string CommitChord(bool attacker, int coreIndex, IEnumerable<int> minorIndices, int target = -1, float rendition = 1f, int beat = 0, IEnumerable<int> minorBeats = null)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s) ?? WhyNotHand(attacker, coreIndex, true);
            if (why != null) return why;
            return ComposeChord(s, coreIndex, minorIndices?.ToList() ?? new List<int>(), target, rendition, beat, minorBeats?.ToList());
        }

        private string ComposeChord(Side s, int coreIndex, List<int> minorIndices, int target, float rendition, int majorRel, List<int> minorRels)
        {
            var p = s.perf;
            var selection = new[] { coreIndex }.Concat(minorIndices).ToList();
            if (selection.Distinct().Count() != selection.Count) return "Each card can promise only one note.";
            if (selection.Any(i => i < 0 || i >= p.hand.Count)) return "No such note in the hand.";
            var cards = selection.Select(i => p.deck[p.hand[i]]).ToList();
            var coreCard = cards[0].card;
            if (coreCard.noteRole == BattleNoteRole.Minor) return "A Minor cannot be the Core.";
            if (cards.Skip(1).Any(c => c.card.noteRole == BattleNoteRole.Core || c.card.chord == null && !WardMinor(c.card))) return "A Chord has exactly one Core; every Minor must be a structural modifier or a Ward.";
            if (cards.Any(c => c.card.reaction != null)) return "Prime reaction cards individually.";
            foreach (var card in cards) { var why = WhyNot(s, card); if (why != null) return why; }
            var owner = TrackOf(s, cards[0]);
            if (owner == null) return "That voice has no Track on the field.";
            bool extraMajor = HasIntention(owner);
            if (extraMajor && !coreCard.corrupted && !coreCard.cathartic && !composingOverbeat)
                return "One Major Note per Track per Measure: this Track already has its intention.";
            if (!extraMajor && !Elite(owner) && ComposedFormations(s) >= Bandwidth(s.attacker)) return "Command Bandwidth is full: the remaining formation Tracks follow Standing Orders.";
            if (owner.rushing) return "A Rushing Track sounds no other Notes.";
            var contributors = cards.Skip(1).Select(c => TrackOf(s, c)).ToList();
            if (contributors.Any(t => t == null)) return "A contributing voice has no Track on the field.";
            int voices = new[] { owner }.Concat(contributors).Distinct().Count();
            if (voices < Math.Max(cards[0].card.requiredBond > 0f ? 2 : 1, cards[0].card.requiredVoices)) return "This duet or ensemble needs more distinct voices.";
            if (cards[0].card.requiredBond > 0f && !contributors.Any(t => Bond(s, Performer(t), Performer(owner)) >= cards[0].card.requiredBond)) return "The required duet or ensemble bond is missing.";
            foreach (var t in contributors.Where(t => t != owner).Distinct())
                if (!Synchronized(t, owner)) return $"{t.Name} is not synchronized with {owner.Name}: Detuned voices and voices outside the Conductor's carrier signal cannot join an Ensemble.";

            // Compose the working for its binding limits and Age before anything is consumed.
            var ornaments = cards.Skip(1).Where(c => c.card.chord != null).Select(c => c.card).ToList();
            var combined = BattleChordLogic.Compose(coreCard, ornaments, ct);
            if (combined.minors.Count > 3 || !AgeMagic.Playable(CardFace.Tier(combined), field.age)) return "The working exceeds its binding limit or the Age's command of magic.";
            bool bonded = contributors.Any(t => t != owner && Bond(s, Performer(t), Performer(owner)) >= ct.bondThreshold);
            var performer = Performer(owner);
            var tempo = TempoOf(s, coreCard);
            bool ward = WardMajor(coreCard);
            int channel = ward ? 1 : ChannelOf(s, coreCard, performer, bonded);
            bool hyper = coreCard.hyperMeasures > 1;
            int minorCount = cards.Count - 1;

            // The Beat the Major resolves on: the Cadence, or for a Staccato Major with at most one Minor, Beats 1-3.
            int rel = majorRel > 0 ? majorRel : BattleMeasureMath.Beats;
            if ((coreCard.corrupted || coreCard.cathartic) && (minorCount > 0 || Abs(rel) <= beat)) return "A compulsion occupies one upcoming Beat with no Minor Notes.";
            if (ward) { rel = BattleMeasureMath.Beats; channel = BattleMeasureMath.Beats - (majorRel > 0 ? majorRel : mt.wardRaise) + 1; }
            if (rel < 1 || rel > BattleMeasureMath.Beats) return "A Note lives on Beats 1-4.";
            if (!ward && rel != BattleMeasureMath.Beats)
            {
                if (tempo != CardTempo.Staccato) return "Only a Staccato Major may be syncopated before the Cadence.";
                if (minorCount > 1 || channel > 1) return "A syncopated strike carries at most one Minor Note: anything larger than a Dyad waits for the Cadence.";
            }
            // A Hyper Chord sounds its Tetrad in this Measure and resolves at the Cadence of its last one.
            if (hyper) rel = BattleMeasureMath.Beats;
            int majorBeat = Abs(rel) + (hyper ? (coreCard.hyperMeasures - 1) * BattleMeasureMath.Beats : 0);
            int channelStart = Abs(rel) - channel + 1;
            if (channelStart < Abs(1)) return $"{coreCard.name} must begin sounding {channel} Beats before it resolves; there are not enough Beats before Beat {rel}.";
            var rests = Rests(s);
            if (rests[BattleMeasureMath.Rel(channelStart)]) return "No Note may begin on a Rest.";
            for (int b = channelStart; b <= Abs(rel); b++) if (!composingOverbeat && Occupied(owner, b)) return $"Beat {BattleMeasureMath.Rel(b)} of {owner.Name}'s Track already sounds a Note.";

            // Each Minor sounds on a Beat of its own Track before the Core begins. Unplaced Minors keep their written
            // order in time: the last one sounds latest, immediately before the Core.
            var placed = new int[minorCount];
            var taken = new HashSet<(Track, int)>();
            for (int b = channelStart; b <= Abs(rel); b++) taken.Add((owner, b));
            for (int i = 0; i < minorCount; i++)
            {
                if (minorRels == null || i >= minorRels.Count || minorRels[i] <= 0) continue;
                int at = Abs(minorRels[i]); var track = contributors[i];
                if (at < Abs(1) || at >= channelStart) return "Minor Notes sound on the Beats before their Major Note.";
                if (rests[BattleMeasureMath.Rel(at)]) return "No Note may begin on a Rest.";
                if (taken.Contains((track, at)) || Occupied(track, at)) return $"Beat {BattleMeasureMath.Rel(at)} of {track.Name}'s Track already sounds a Note.";
                taken.Add((track, at)); placed[i] = at;
            }
            for (int i = minorCount - 1; i >= 0; i--)
            {
                if (placed[i] > 0) continue;
                var track = contributors[i];
                int latest = Enumerable.Range(i + 1, minorCount - i - 1).Where(j => contributors[j] == track && placed[j] > 0).Select(j => placed[j]).DefaultIfEmpty(channelStart).Min();
                int at = -1;
                for (int b = latest - 1; b >= Abs(1); b--)
                    if (!taken.Contains((track, b)) && !Occupied(track, b) && !rests[BattleMeasureMath.Rel(b)]) { at = b; break; }
                if (at < 0) return $"{track.Name} has no free Beat before the Core for {cards[i + 1].Name}.";
                taken.Add((track, at)); placed[i] = at;
            }
            var minorBeats = placed.ToList();
            int firstBeat = minorBeats.Concat(new[] { channelStart }).Min();
            var footing = cards.Select(c => FootingOf(s, c.card)).Max();
            if (minorCount >= 3 && footing < BattleFooting.Braced) footing = BattleFooting.Braced;
            if (hyper) footing = BattleFooting.Anchored;

            var dc = new DeckCard { card = combined, voice = cards[0].voice, legend = cards[0].legend, scale = cards[0].scale, major = cards[0].major, source = cards[0].source };
            var aimed = AimsAtEnemy(combined) ? s.enemy : s;
            if (target >= aimed.side.sections.Count) return "No such Core target.";
            planning = true; projectAt = Abs(rel);
            CombatSection victim;
            try
            {
                // A Charge's impact Step carries it one hex further before it strikes.
                bool charge = combined.chord?.advance == true || cards.Skip(1).Any(c => c.card.chord?.advance == true);
                var reach = dc;
                if (charge)
                {
                    reach = new DeckCard { card = combined.Clone(), voice = dc.voice, legend = dc.legend, scale = dc.scale, major = dc.major, source = dc.source };
                    foreach (var e in reach.card.effects.Where(e => e.aim == CardAim.Enemy || e.aim == CardAim.EnemyLine))
                        e.range = (e.range > 0 ? e.range : e.op == CardOp.Strike || e.op == CardOp.Push || e.op == CardOp.IntegrityDamage ? SpatialRules.Reach(performer) : SpatialRules.Tuning.spellReach) + mt.chargeStep;
                }
                victim = coreCard.corrupted || coreCard.cathartic ? CrisisTarget(s, dc) : target >= 0 ? aimed.side.sections[target] : ward ? WardTarget(s, dc) : Value(s, reach, null).target;
                var targetWhy = ward ? null : TargetFailure(s, reach, victim);
                if (targetWhy != null) return targetWhy;
            }
            finally { planning = false; projectAt = 0; }

            var chord = new Chord { id = nextChord, side = s, owner = owner, core = Frozen(cards[0]), target = victim, majorBeat = majorBeat, channel = channel,
                firstBeat = firstBeat, origin = Projected(performer, Abs(rel)), footing = footing, legato = tempo == CardTempo.Legato, staccato = tempo == CardTempo.Staccato,
                ward = ward, spell = Magic(coreCard), magical = cards.Any(c => IsWeaving(c.card)) || combined.weaving, hyperMeasures = hyper ? coreCard.hyperMeasures : 0, feverExtra = extraMajor,
                composedMeasure = m, heartbeatShare = victim?.ComposureShare ?? 1f,
                stanceHeld = s.side.StanceBroken ? BattleStanceHold.Broken : BattleStanceHold.Held };
            chord.notes.Add(new ChordNote { card = chord.core, track = owner, beat = majorBeat, kind = ward ? BattleNoteKind.Ward : BattleNoteKind.Major,
                handIndex = p.hand[coreIndex], origin = chord.origin });
            for (int i = 0; i < minorCount; i++)
            {
                var track = contributors[i];
                var kind = track != owner ? (cards[i + 1].card.chord?.holdBeats > 0 ? BattleNoteKind.Sustain : BattleNoteKind.Contribution) : BattleNoteKind.Minor;
                chord.notes.Add(new ChordNote { card = Frozen(cards[i + 1]), track = track, beat = minorBeats[i], kind = kind, handIndex = p.hand[selection[i + 1]],
                    origin = Projected(Performer(track), minorBeats[i]) });
            }
            // A bonded partner performing into the Chord is a duet: both become Resonant for the Measure.
            var partners = chord.notes.Where(n => n.track != owner && Bond(s, Performer(n.track), performer) >= ct.bondThreshold).Select(n => n.track).Distinct().ToList();
            var priorDuet = partners.Concat(new[] { owner }).Distinct().ToDictionary(x => x, x => x.duet);
            if (partners.Count > 0) foreach (var x in priorDuet.Keys) x.duet = true;
            int sounding = chord.majorBeat - chord.firstBeat + 1, limit = HoldLimit(chord);
            if (limit < Math.Min(sounding, BattleMeasureMath.Beats))
            {
                foreach (var pair in priorDuet) pair.Key.duet = pair.Value;
                return $"The working would sound {sounding} Beats against a Hold Limit of {limit}: build something to hold it first.";
            }
            nextChord++;
            chord.seed = unchecked((int)p.rng.Next());
            foreach (var n in chord.notes) n.rendition = Rendition(n.track, rendition);
            chord.power = Power(s, chord);
            chords[chord.id] = chord;
            if (extraMajor && !coreCard.corrupted && !coreCard.cathartic) owner.feverExtra = true;
            if (combined.chord?.advance == true || cards.Skip(1).Any(c => c.card.chord?.advance == true)) owner.charging = true;
            if (Has(combined, CardOp.Rush)) owner.rushing = true;
            foreach (int handIndex in selection.OrderByDescending(i => i)) p.hand.RemoveAt(handIndex);
            ChordEvent(s, chord.id, BattleChordCause.Committed, $"{dc.Name}: one Core on Beat {BattleMeasureMath.Rel(majorBeat)}, {minorCount} Minor{(minorCount == 1 ? "" : "s")}, {footing}.");
            if (!Automatic(s)) { if (s.attacker) handA += cards.Count; else handD += cards.Count; }
            ScheduleSteps(owner);
            return null;
        }

        private DeckCard Frozen(DeckCard dc) => new DeckCard { card = dc.card.Clone(), voice = dc.voice, legend = dc.legend, scale = dc.scale, major = dc.major, source = dc.source };

        private float Rendition(Track track, float rendition)
        {
            if (BattleMeasureMath.Grade(rendition) == BattleExecution.Clean) return 1f;
            // Only Spotlit Tracks are performed by the player; every other Track performs at Clean.
            if (!track.spotlit && tracks.Values.Count(t => t.side == track.side && t.spotlit) < mt.spotlit) track.spotlit = true;
            return track.spotlit ? Math.Max(0f, Math.Min(1f + mt.perfectPower, rendition)) : 1f;
        }

        public string Spotlight(bool attacker, int section)
        {
            var s = Of(attacker); var track = TrackOf(s, section);
            if (!open || scoresCommitted) return "Spotlit Tracks are chosen during Composition.";
            if (track == null) return "No such Track.";
            if (!track.spotlit && tracks.Values.Count(t => t.side == s && t.spotlit) >= mt.spotlit) return $"Up to {mt.spotlit} Tracks are Spotlit.";
            track.spotlit = true; return null;
        }

        private static bool WardMajor(CombatCard card) => card.effects.Any(e => e != null && (e.op == CardOp.Guard || e.op == CardOp.Ward)) &&
            card.effects.All(e => e != null && (e.op == CardOp.Guard || e.op == CardOp.Ward || e.op == CardOp.Rally));
        private static bool WardMinor(CombatCard card) => card.noteRole != BattleNoteRole.Core && card.purpose == SpellPurpose.Defensive && WardMajor(card) && card.effects.Any(e => e.op == CardOp.Guard || e.op == CardOp.Ward);

        private CombatSection WardTarget(Side s, DeckCard dc)
        {
            var e = dc.card.effects.FirstOrDefault(x => x.op == CardOp.Guard || x.op == CardOp.Ward);
            if (e == null || e.aim != CardAim.Ally) return null;
            return EffectTargets(s, dc, e).OrderByDescending(x => IncomingSteel(s) + (x.row == FormationRow.Back ? 1f : 0f)).ThenBy(x => x.IntegrityShare).FirstOrDefault();
        }

        /// <summary>Prime a Reaction on its Track: it occupies no Beats and fires once this Measure when its condition is met.</summary>
        private string Prime(Side s, int handIndex, int target, float rendition)
        {
            var p = s.perf; var dc = p.deck[p.hand[handIndex]];
            var track = TrackOf(s, dc);
            if (track == null) return "That voice has no Track on the field.";
            if (reactions.Count(r => r.track == track && r.measure == m) >= ReactionSlots(track)) return $"{track.Name} has no free Reaction to prime.";
            CombatSection chosen = null;
            if (target >= 0) { var aimed = AimsAtEnemy(dc.card) ? s.enemy : s; if (target >= aimed.side.sections.Count) return "No such target."; chosen = aimed.side.sections[target]; }
            var q = new Queued { index = p.hand[handIndex], target = chosen, rendition = Rendition(track, rendition), origin = Performer(track)?.battleHex ?? -1,
                frozen = Frozen(dc), seed = unchecked((int)p.rng.Next()), stanceHeld = !s.side.StanceBroken };
            reactions.Add(new ArmedReaction { id = nextReaction++, attacker = s.attacker, preparation = q, track = track, measure = m });
            p.hand.RemoveAt(handIndex);
            ChordEvent(s, 0, BattleChordCause.Reaction, $"Primed visible {dc.card.reaction.trigger} reaction: {dc.Name}.");
            if (!Automatic(s)) { if (s.attacker) handA++; else handD++; }
            return null;
        }

        /// <summary>1 per Track, plus Strand mastery, an Echoing Bond on the field and a held Line.</summary>
        private int ReactionSlots(Track track)
        {
            int slots = mt.reactionsPerTrack;
            var legend = LegendOf(track);
            if (legend != null && BattleMeasureMath.Mastery(legend.Score(SpellBinding.Strand)) >= 3) slots++;
            var unit = Performer(track);
            if (unit != null && track.side.side.Standing.Any(x => x != unit && Bond(track.side, x, unit) >= ct.bondThreshold)) slots++;
            if (track.side.side.stance == BattleStance.Line && SpatialRules.DoctrineHeld(track.side.attacker)) slots++;
            return slots;
        }

        private BattleLegend LegendOf(Track track) => track.unit == null ? track.side.conductor : track.unit.leader ??
            (track.unit.eliteRole == BattleEliteRole.Conductor ? track.side.conductor : null);

        /// <summary>Retract a Chord composed this Measure (held time): its cards return to the hand.</summary>
        public string Retract(bool attacker, long chord)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s); if (why != null) return why;
            if (!chords.TryGetValue(chord, out var c) || c.side != s || c.composedMeasure != m || c.committed || c.drilled) return "Only a Chord composed in this Composition can be retracted.";
            // A fused finale gives its Chords back as they were composed; only its own Climax Notes return to the hand.
            var restored = new HashSet<int>(c.merged.SelectMany(x => x.notes).Select(n => n.handIndex).Where(i => i >= 0));
            foreach (var n in c.notes.Where(n => n.handIndex >= 0 && !restored.Contains(n.handIndex))) s.perf.hand.Add(n.handIndex);
            chords.Remove(chord);
            foreach (var original in c.merged) chords[original.id] = original;
            c.owner.feverExtra = chords.Values.Any(x => x.Live && x.owner == c.owner && x.feverExtra);
            c.owner.charging = chords.Values.Any(x => x.Live && x.owner == c.owner && x.core.card.chord?.advance == true);
            c.owner.rushing = chords.Values.Any(x => x.Live && x.owner == c.owner && Has(x.core.card, CardOp.Rush));
            ChordEvent(s, chord, BattleChordCause.Retracted, "Retracted during Composition.");
            ScheduleSteps(c.owner);
            return null;
        }

        /// <summary>Set a Track's Standing Orders (they persist from one Measure to the next until changed).</summary>
        public string Order(bool attacker, int section, BattleStandingOrder order)
        {
            var s = Of(attacker); var track = TrackOf(s, section);
            if (track == null) return "No such Track.";
            if (scoresCommitted) return "The Score is committed; the Measure is being performed.";
            track.order = order;
            if (!track.pathComposed) { track.path.Clear(); if (open) PlanPath(track); }
            return null;
        }

        /// <summary>
        /// Ring the Redraw Bell: every uncommitted card in the hand is discarded and a new hand drawn. Each ring turns the
        /// next Beat of every friendly Track into a Rest (Beat 1, then Beat 2); Steps still happen, primed Reactions stay.
        /// </summary>
        public string Redraw(bool attacker)
        {
            var s = Of(attacker);
            string why = WhyNotCompose(s); if (why != null) return why;
            int rings = attacker ? ringsA : ringsD;
            if (rings >= mt.maxRings) return $"The Bell may be rung {mt.maxRings} times a Measure.";
            int rested = Abs(rings + 1);
            if (TracksOf(s).Any(t => chords.Values.Any(c => c.Live && c.composedMeasure == m && !c.drilled && c.notes.Any(n => n.track == t && (n.beat == rested ||
                (n.kind == BattleNoteKind.Major || n.kind == BattleNoteKind.Ward) && c.ChannelStart == rested)))))
                return $"A composed Note begins on Beat {rings + 1}; retract it before resting that Beat.";
            Rests(s)[rings + 1] = true;
            if (attacker) ringsA++; else ringsD++;
            s.perf.discard.AddRange(s.perf.hand); s.perf.hand.Clear();
            Draw(s.perf, s.perf.handSize);
            PollutionDraw(s); SyncCrises();
            Remember(s, "redraw");
            report.log.Add($"Measure {m}: {s.side.name} ring the Redraw Bell; Beat {rings + 1} of every Track rests.");
            return null;
        }

        public int Rings(bool attacker) => attacker ? ringsA : ringsD;

        // ===== COMMITMENT AND PERFORMANCE =====

        /// <summary>Lock both Scores. Every Track without a composed intention takes its Standing Orders' drilled Note.</summary>
        public void Commit()
        {
            if (Over || !open || scoresCommitted) return;
            foreach (var s in new[] { a, d })
                foreach (var track in TracksOf(s).ToList())
                {
                    // A Track given a composed intention holds its ground unless a path was composed for it too.
                    if (!track.pathComposed && !Automatic(s) && !HasIntention(track)) PlanPath(track);
                    if (!HasIntention(track) && !chords.Values.Any(c => c.Live && c.owner == track && c.drilled && c.composedMeasure == m)) ComposeDrill(track);
                    ScheduleSteps(track);
                }
            Enter(BattlePhase.Commitment);
            DeclareResolutions();
            foreach (var c in chords.Values.Where(c => c.Live && !c.committed).OrderBy(c => c.id))
            {
                c.committed = true;
                report.commitments.Add(IntentOf(c));
            }
            scoresCommitted = true;
        }

        /// <summary>Perform Beats of the open Measure (committing it first); after Beat 4 the Toll and Assessment close it.</summary>
        public string Perform(int beats = 1)
        {
            if (rhythm != null || pendingCadence != null) return "Finish the current rhythm phrase first.";
            if (Over) return "The battle is over.";
            if (!open) return "No measure is open.";
            if (beats < 1) return "Performing must advance at least one Beat.";
            Commit();
            for (int i = 0; i < beats && open && !Over; i++) PerformBeat();
            return null;
        }

        /// <summary>Kept for older callers: performs Beats (a Beat is not a turn; nothing can be added mid-Measure).</summary>
        public string Wait(int beats = 1) => Perform(beats);

        private void PerformBeat()
        {
            if (pendingOverbeat) PerformOverbeat();
            beat++;
            int rel = BattleMeasureMath.Rel(beat);
            Enter(BattlePhase.Execution);
            reactionsFired.Clear(); emergencyBonds.Clear(); interruptedThisBeat.Clear();
            Downstroke();
            Steps();
            FlushReactionsAndHits();
            ApplyPerformedNotes();
            Notes(rel == BattleMeasureMath.Beats);
            if (pendingCadence != null) return;
            AfterNotes();
        }

        private void AfterNotes()
        {
            Enter(BattlePhase.Reaction);
            FlushReactionsAndHits();
            Attrition();
            Coherence();
            Pressure(); PumpCascade(); AttachmentFalls(); FlushReactionsAndHits();
            SyncCrises();
            EliteChecks();
            foreach (var side in new[] { a, d }.Where(Automatic)) PlanCrises(side);
            if (BattleMeasureMath.Rel(beat) == 3 && AnyOverbeat)
            { pendingOverbeat = true; if (!interactiveCadence) PerformOverbeat(); }
            if (BattleMeasureMath.Rel(beat) == BattleMeasureMath.Beats) CloseMeasure();
        }

        // ===== VISUALIZATION =====

        public IReadOnlyList<BattleActionIntent> Countdowns =>
            chords.Values.Where(c => c.Live).OrderBy(c => c.majorBeat).ThenBy(c => c.id).Select(IntentOf)
                .Concat(tracks.Values.SelectMany(t => t.steps.Where(x => x.beat > beat).Select(x => new BattleActionIntent { attacker = t.side.attacker, voice = t.voice,
                    kind = BattleActionKind.Movement, name = t.Name + " steps", to = x.hex, dueBeat = x.beat, committedMeasure = m, confirmed = scoresCommitted || Automatic(t.side) })))
                .ToList();

        private BattleActionIntent IntentOf(Chord c)
        {
            var s = c.side;
            var dc = c.core;
            var source = Performer(c.owner);
            var intent = new BattleActionIntent
            {
                id = c.id, attacker = s.attacker, voice = c.owner.voice, card = dc.card.id, name = dc.Name, committedMeasure = c.composedMeasure,
                kind = c.drilled ? (c.owner.unit == null ? BattleActionKind.Conductor : BattleActionKind.DrilledAction) : c.notes.Count > 1 ? BattleActionKind.Chord : BattleActionKind.Card,
                from = c.origin, dueBeat = c.majorBeat, actionBeats = c.channel, channeling = c.legato, anchorBeat = c.legato ? c.ChannelStart + mt.legatoAnchor - 1 : 0,
                target = c.target == null ? -1 : ownerOf[c.target].side.sections.IndexOf(c.target), targetAttacker = c.target != null && ownerOf[c.target].attacker,
                targeting = dc.card.effects.FirstOrDefault(e => e != null)?.proximity ?? BattleProximity.Any, aim = dc.card.effects.FirstOrDefault(e => e != null)?.aim ?? CardAim.Self,
                binding = RootOf(dc, source), syncopated = c.Syncopated, ward = c.ward, hyper = c.hyperMeasures > 1, footing = c.footing,
                confirmed = c.committed || Automatic(s), holdLimit = HoldLimit(c), sounding = c.majorBeat - c.firstBeat + 1,
                minimumWardBeats = dc.card.minimumWardBeats, abjuredOnlyBy = new List<string>(dc.card.abjuredOnlyBy),
            };
            var q = new Queued { frozen = dc, target = c.target, rendition = c.Major.rendition };
            DescribeOutput(s, dc, q, intent);
            return intent;
        }

        private void DescribeOutput(Side s, DeckCard dc, Queued q, BattleActionIntent declaration)
        {
            var voice = VoiceOf(s, dc) ?? Origin(s, null);
            float scale = dc.scale * q.rendition * FieldBonus(s, dc.card, voice);
            foreach (var e in dc.card.effects.Where(e => e != null))
            {
                if (e.op == CardOp.IntegrityDamage) declaration.expectedIntegrity += Math.Max(0f, e.amount * scale);
                if (e.op == CardOp.Strike && voice != null) declaration.expectedIntegrity += e.amount * scale * VoiceAttack(s, voice, true) * HarmPerAttack;
                if (e.op == CardOp.Spell) declaration.expectedIntegrity += e.amount * scale * VoicePotency(dc, voice, declaration.binding) * t.spellHit * t.integrityPerSpellHit *
                    (q.target == null ? 1f : HarmonicCircle.Multiplier(declaration.binding, q.target.primary, t));
                if (e.op == CardOp.Dread) declaration.expectedComposure += e.amount * scale;
                if (e.op == CardOp.Guard) declaration.expectedGuard += WardStrength(s, dc, e, q.target ?? voice, scale);
                if (e.op == CardOp.Ward) declaration.expectedWard += WardStrength(s, dc, e, q.target ?? voice, scale);
            }
            declaration.damageCertain = dc.card.effects.Any(e => e?.op == CardOp.IntegrityDamage) && !dc.card.effects.Any(e => e?.op == CardOp.Strike || e?.op == CardOp.Spell);
            // Estimates describe the declared output. Movement, Wards and failed prerequisites can still change reception.
        }

        public BattleVisualization Visualize() => new BattleVisualization
        {
            measure = m, beat = beat, phase = Phase, attacker = Bars(a), defender = Bars(d),
            terrain = Spatial.Terrain.Select(x => x.Clone()).ToList(), intents = Countdowns.Select(x => x.Clone()).ToList(),
            weaves = PreparedWeaves.ToList(), reactions = PreparedReactions.ToList(), tracks = new[] { true, false }.SelectMany(Score).ToList(),
            units = new[] { a, d }.SelectMany(s => s.side.sections.Select((x, index) => new BattleUnitView
            {
                section = index, attacker = s.attacker, hex = x.battleHex, standing = x.Standing, mindBroken = x.mindBroken,
                stacked = Spatial.IsStacked(x.battleHex, s.attacker), engaged = Spatial.IsEngaged(x.battleHex) || SkirmishOf(x) != null, pinned = SkirmishOf(x) != null,
                integrity = x.integrity, composure = x.composure, guard = s.WardOf(x, false), ward = s.WardOf(x, true),
                burn = s.Marks(x)?.burnLeft > 0 ? s.Marks(x).burn : 0f, blind = s.BlindOf(x), exposed = s.ExposeOf(x),
                countdown = chords.Values.Where(c => c.Live && c.owner.side == s && c.owner.voice == index).Select(c => c.majorBeat - beat).DefaultIfEmpty(0).Min(),
                pace = TrackOf(s, index) == null ? 0 : Pace(TrackOf(s, index)), tuning = TrackOf(s, index) == null ? BattleTuningState.InTune : Tune(TrackOf(s, index)),
            })).ToList(),
        };

        /// <summary>The sequencer for one army: four Beat columns, one row per Track.</summary>
        public IReadOnlyList<BattleTrackView> Score(bool attacker)
        {
            var s = Of(attacker);
            var rows = new List<BattleTrackView>();
            foreach (var track in TracksOf(s).OrderBy(t => t.voice))
            {
                var view = new BattleTrackView { attacker = attacker, voice = track.voice, name = track.Name, order = track.order, spotlit = track.spotlit,
                    composed = HasIntention(track), tempoFever = track.fever, primed = reactions.Count(r => r.track == track && r.measure == m && !r.fired), pace = Pace(track), tuning = Tune(track) };
                for (int rel = 1; rel <= BattleMeasureMath.Beats; rel++)
                {
                    int at = Abs(rel);
                    if (Rests(s)[rel]) view.beats[rel] = "Rest";
                    foreach (var c in chords.Values.Where(c => c.Live || c.composedMeasure == m))
                        foreach (var n in c.notes.Where(n => n.track == track && !n.overbeat))
                        {
                            bool major = n.kind == BattleNoteKind.Major || n.kind == BattleNoteKind.Ward;
                            if (n.kind == BattleNoteKind.SupportingMajor && at >= n.from && at <= n.beat)
                                view.beats[rel] = at == n.from ? $"Supporting Major: {n.card.Name} → {c.core.Name}" : "Sustained";
                            else if (major && at >= MajorStarts(c) && at <= Math.Min(c.majorBeat, Cadence))
                                view.beats[rel] = at == MajorStarts(c) ? $"{(c.ward ? "Ward" : "Major")}: {c.core.Name}" : "Sustained";
                            else if (!major && n.beat == at) view.beats[rel] = $"Minor: {n.card.Name}";
                        }
                    foreach (var step in track.steps.Where(x => x.beat == at)) view.steps[rel] = step.hex;
                }
                rows.Add(view);
            }
            return rows;
        }

        /// <summary>What a side will play this measure (each committed or composed Major).</summary>
        public IReadOnlyList<CardPlay> Intent(bool attacker)
        {
            var s = Of(attacker);
            return chords.Values.Where(c => c.side == s && c.Live && !c.drilled).OrderBy(c => c.id).Select(c => new CardPlay
            {
                measure = m, attacker = attacker, card = c.core.card.id, cardName = c.core.Name, voice = c.owner.Name, target = c.target?.name,
                rendition = c.Major.rendition, action = c.id, beat = c.majorBeat, countdown = c.majorBeat - beat,
            }).ToList();
        }

        // ===== MEMORY (enemy Conductors remember behavior, never rewrite committed intent) =====

        private float Intelligence(Side s) => s.Conducted ? s.conductor.tacticalIntelligence >= 0f ? Math.Max(0f, Math.Min(1f, s.conductor.tacticalIntelligence)) :
            Math.Min(1f, .25f + .2f * s.conductor.Stars(LegendClass.Seer) + .1f * s.conductor.Stars(LegendClass.Vanguard) + .05f * s.recon) : .15f;

        private bool Trait(Side s, params string[] fragments) => s.Conducted && s.conductor.traits.Any(x => fragments.Any(f => x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0));
        private int Remembered(Side s, string key) => behavior.TryGetValue(s, out var memory) && memory.TryGetValue(key, out int count) ? count : 0;
        private void Remember(Side observed, string key)
        {
            var observer = observed.enemy;
            if (!behavior.TryGetValue(observer, out var memory)) behavior[observer] = memory = new Dictionary<string, int>();
            memory[key] = Remembered(observer, key) + 1;
        }

        private CombatSection SelectFutureTarget(Side s, CombatSection source, List<CombatSection> targets)
        {
            if (targets.Count == 0) return null;
            if (Intelligence(s) < .25f) return targets[0];
            float Score(CombatSection target)
            {
                float score = 1f - target.IntegrityShare;
                if (source?.Casts == true) score += HarmonicCircle.Multiplier(source.primary, target.primary, t);
                if (Trait(s, "Protect", "Caution", "Patien")) score += target.attack * .01f;
                if (Trait(s, "Ambition", "Confidence", "Fearless")) score += target.eliteRole != BattleEliteRole.None ? .5f : 0f;
                score -= Remembered(s, "guard:" + s.enemy.side.sections.IndexOf(target)) * .1f;
                score += Remembered(s, "move:" + BattleHexLayout.At(target.battleHex).Lane) * .1f;
                return score;
            }
            return targets.OrderByDescending(Score).ThenBy(x => s.enemy.side.sections.IndexOf(x)).First();
        }

        private float TacticalWeight(Side s, DeckCard card)
        {
            float weight = 1f;
            bool attack = Has(card.card, CardOp.Strike) || Has(card.card, CardOp.Spell) || Has(card.card, CardOp.IntegrityDamage);
            bool protect = Has(card.card, CardOp.Guard) || Has(card.card, CardOp.Mend) || Has(card.card, CardOp.Rally);
            if (attack && (s.side.stance == BattleStance.Spearhead || Trait(s, "Ambition", "Confidence", "Fearless"))) weight += .2f;
            if (protect && (s.side.stance == BattleStance.Line || Trait(s, "Protect", "Caution", "Patien"))) weight += .2f;
            if (Has(card.card, CardOp.Push) && s.side.stance == BattleStance.Crescent) weight += .2f;
            // A Mind Broken Conductor's inheritance: reckless attacks, or excessive defensive play.
            if (s.maladaptation == BattleConductorMaladaptation.Aggressive) weight += attack ? .3f : protect ? -.2f : 0f;
            if (s.maladaptation == BattleConductorMaladaptation.Fearful) weight += protect ? .3f : attack ? -.2f : 0f;
            return Math.Max(.1f, weight);
        }
    }
}
