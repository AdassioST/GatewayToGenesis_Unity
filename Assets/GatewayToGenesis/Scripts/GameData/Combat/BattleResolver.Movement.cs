using System;
using System.Collections.Generic;
using System.Linq;

// Movement and Contact (vault: Combat System.md): a path is planned once and walked Beat by Beat, one hex per Step,
// up to the unit's Pace. Every Step scheduled on the same Beat resolves at once; contact engages, pins and starts a
// Skirmish whose verdict waits for Assessment.
public static partial class BattleResolver
{
    /// <summary>The fight over one hex (or, for a Clash, across the edge between two).</summary>
    private sealed class Skirmish
    {
        public int hex, seam = -1, contactBeat;
        public readonly HashSet<CombatSection> a = new HashSet<CombatSection>(), d = new HashSet<CombatSection>();
        public float aEntered, dEntered, aLost, dLost;
        public bool Clash => seam >= 0;
        public HashSet<CombatSection> Of(bool attacker) => attacker ? a : d;
    }

    public sealed partial class BattleRun
    {
        private readonly List<Skirmish> skirmishes = new List<Skirmish>();
        private readonly Dictionary<Track, int> collided = new Dictionary<Track, int>();
        private int projectAt;
        // While a side composes it projects only its own Steps: the enemy's uncommitted plan is never read.
        private Side planningSide;

        // ===== PROJECTION (planning reads where a combatant will stand) =====

        private int Projected(CombatSection section, int at)
        {
            if (section == null) return -1;
            var track = TrackOf(section);
            int hex = section.battleHex;
            if (track == null || planningSide != null && track.side != planningSide) return hex;
            foreach (var step in track.steps) if (step.beat > beat && step.beat <= at) hex = step.hex;
            return hex;
        }

        private int Projected(CombatSection section) => !planning || section == null ? section?.battleHex ?? -1 : Projected(section, projectAt > 0 ? projectAt : Cadence);

        // ===== PATHS (held time) =====

        public IReadOnlyList<BattleMovementIntent> MovementIntent(bool attacker) => tracks.Values.Where(t => t.side.attacker == attacker && t.unit != null)
            .SelectMany(t => t.steps.Where(x => x.beat > beat).Select((x, i) => new BattleMovementIntent { attacker = attacker, section = t.voice,
                from = i == 0 ? t.unit.battleHex : t.steps.Where(y => y.beat > beat).ElementAt(i - 1).hex, to = x.hex, beat = x.beat })).ToList();

        /// <summary>Compose a path; its Steps take the earliest Beats the Track's Footing allows unless <paramref name="beats"/> drags them later.</summary>
        public string ComposePath(bool attacker, int section, IEnumerable<int> hexes, IEnumerable<int> beats = null)
        {
            var s = Of(attacker);
            if (Over) return "The battle is over.";
            if (!open) return "No measure is open.";
            if (scoresCommitted) return "The Score is committed; the Measure is being performed.";
            var track = TrackOf(s, section);
            if (track?.unit == null || !track.unit.Standing) return "The combatant is no longer on the field.";
            var list = (hexes ?? Enumerable.Empty<int>()).ToList();
            if (list.Count > 0 && SkirmishOf(track.unit) != null) return "An engaged combatant is pinned: only a Withdraw breaks engagement.";
            int at = track.unit.battleHex;
            foreach (int h in list)
            {
                if (h < 0 || h >= BattleHexLayout.HexCount || !SpatialRules.Passable(h)) return "That hex is blocked.";
                if (!BattleHexLayout.AreAdjacent(at, h)) return "Each Step crosses one shared hex edge.";
                at = h;
            }
            var oldPath = track.path.ToList(); var oldBeats = track.stepBeats.ToList(); bool oldComposed = track.pathComposed;
            track.path.Clear(); track.path.AddRange(list); track.stepBeats.Clear();
            if (beats != null) track.stepBeats.AddRange(beats.Select(Abs));
            track.pathComposed = true;
            string why = ScheduleSteps(track, strict: true);
            if (why != null)
            {
                track.path.Clear(); track.path.AddRange(oldPath); track.stepBeats.Clear(); track.stepBeats.AddRange(oldBeats); track.pathComposed = oldComposed;
                ScheduleSteps(track); return why;
            }
            if (list.Count > 0) Remember(s, "move:" + BattleHexLayout.At(list.Last()).Lane);
            return null;
        }

        /// <summary>Compose a one-Step path into an adjacent hex.</summary>
        public string CommitMovement(bool attacker, int section, int hex) => ComposePath(attacker, section, new[] { hex });

        /// <summary>
        /// Place this Measure's Steps: each takes one Beat (a river two), up to the Pace, on Beats the Footing allows.
        /// A path longer than the Pace continues automatically in the following Measure.
        /// </summary>
        private string ScheduleSteps(Track track, bool strict = false)
        {
            track.steps.Clear();
            if (!open || track.unit == null || !track.unit.Standing || track.path.Count == 0) return null;
            if (SkirmishOf(track.unit) != null) return strict ? "An engaged combatant is pinned." : null;
            int budget = Pace(track), perBeat = track.rushing ? 2 : 1, last = beat, lastCount = 0;
            for (int i = 0; i < track.path.Count; i++)
            {
                int cost = StepCost(track.path[i]);
                if (cost > budget) break;
                int wanted = i < track.stepBeats.Count ? track.stepBeats[i] : 0;
                int arrival = -1, taken = 0, cursor = last, cursorCount = lastCount;
                while (taken < cost)
                {
                    int b = cursorCount < perBeat && cursor > beat ? cursor : cursor + 1;
                    if (b != cursor) { cursor = b; cursorCount = 0; }
                    if (cursor > Cadence) break;
                    bool final = taken == cost - 1;
                    if (!MayStep(track, cursor) || final && wanted > 0 && cursor < wanted) { cursorCount = perBeat; continue; }
                    cursorCount++; taken++;
                    if (final) arrival = cursor;
                }
                if (arrival < 0)
                {
                    if (strict && wanted > 0) return $"Step {i + 1} cannot land on Beat {BattleMeasureMath.Rel(wanted)}: the Footing or the Measure forbids it.";
                    break;
                }
                if (strict && wanted > 0 && arrival != wanted) return $"Step {i + 1} cannot land on Beat {BattleMeasureMath.Rel(wanted)}.";
                track.steps.Add((arrival, track.path[i]));
                budget -= cost; last = cursor; lastCount = cursorCount;
            }
            return null;
        }

        // ===== EXECUTION: STEPS =====

        private void Steps()
        {
            var moving = new List<(Track track, int from, int to)>();
            foreach (var track in tracks.Values.Where(t => t.unit != null && t.steps.Count > 0 && t.steps[0].beat == beat).OrderBy(t => t.side.attacker ? 0 : 1).ThenBy(t => t.voice).ToList())
            {
                var unit = track.unit;
                int to = track.steps[0].hex;
                string why = !unit.Standing ? "The combatant left the field." : SkirmishOf(unit) != null ? "Contact pinned the combatant before its Step." :
                    !MayStep(track, beat) ? "Its Footing forbids a Step on this Beat." : !BattleHexLayout.AreAdjacent(unit.battleHex, to) ? "Displacement broke the path." :
                    !SpatialRules.Passable(to) ? "The path is blocked." : null;
                if (why != null) { if (unit.Standing) BreakPath(track, why); continue; }
                moving.Add((track, unit.battleHex, to));
            }
            if (moving.Count == 0) return;
            // Two hostile units trying to exchange hexes cannot pass: they meet at the seam in a Clash.
            var clashed = new HashSet<Track>();
            foreach (var x in moving)
                foreach (var y in moving.Where(y => y.track.side != x.track.side && y.from == x.to && y.to == x.from && !clashed.Contains(y.track) && !clashed.Contains(x.track)))
                {
                    clashed.Add(x.track); clashed.Add(y.track);
                    var clash = new Skirmish { hex = x.from, seam = y.from, contactBeat = beat };
                    foreach (var unit in new[] { x.track.unit, y.track.unit }) Join(clash, unit);
                    skirmishes.Add(clash);
                    foreach (var t in new[] { x.track, y.track })
                    {
                        Record(t.side, t.unit, BattleSpatialCause.Clash, "Hostile units tried to exchange hexes and met at the seam.");
                        t.steps.Clear(); t.path.Clear();
                    }
                }
            var moves = moving.Where(x => !clashed.Contains(x.track)).ToList();
            // Simultaneous: every Step lands at once, then the geometry is read from the complete set of intentions.
            foreach (var mv in moves) mv.track.unit.battleHex = mv.to;
            foreach (var mv in moves)
            {
                var s = mv.track.side; var unit = mv.track.unit;
                mv.track.steps.RemoveAt(0);
                if (mv.track.path.Count > 0 && mv.track.path[0] == mv.to) mv.track.path.RemoveAt(0);
                QueueReaction(BattleReactionTrigger.Moved, s, unit, unit);
                Record(s, unit, BattleSpatialCause.Movement, "Step resolved on its Beat.", mv.from, mv.to);
                if (Spatial.IsStacked(mv.to, s.attacker)) Emit(s, unit, BattleSpatialCause.Stacking, "Friendly elements overlap; attack and defense are penalized.");
            }
            Contacts(moves.Select(x => x.track).ToList());
            foreach (var mv in moves) MovementReactions(mv.track, mv.from, mv.to);
        }

        private void BreakPath(Track track, string why)
        {
            if (track.steps.Count == 0 && track.path.Count == 0) return;
            track.steps.Clear(); track.path.Clear();
            Emit(track.side, track.unit, BattleSpatialCause.FailedMovementCommitment, why);
        }

        /// <summary>Contact is immediate: engaged units are pinned, and a path broken short of its destination is a failed commitment.</summary>
        private void Contacts(List<Track> movers)
        {
            foreach (var hex in BattleHexLayout.Hexes.Select(h => h.Id).Where(Spatial.IsEngaged))
            {
                var sk = skirmishes.FirstOrDefault(x => !x.Clash && x.hex == hex);
                bool fresh = sk == null;
                if (fresh) skirmishes.Add(sk = new Skirmish { hex = hex, contactBeat = beat });
                foreach (var unit in Spatial.Occupants(hex, true).Concat(Spatial.Occupants(hex, false)).ToList())
                {
                    bool joined = !sk.a.Contains(unit) && !sk.d.Contains(unit);
                    Join(sk, unit);
                    var track = TrackOf(unit);
                    if (track == null) continue;
                    if (movers.Contains(track) && joined) collided[track] = m;
                    if (track.steps.Count > 0 || track.path.Count > 0)
                        BreakPath(track, movers.Contains(track) ? "The path ran into contact before its destination." : "An enemy engaged it before its Steps.");
                    if (joined) Record(track.side, unit, BattleSpatialCause.Contact, fresh ? "Contact: a Skirmish begins over this hex." : "Joined the Skirmish over this hex.");
                }
            }
        }

        private void Join(Skirmish sk, CombatSection unit)
        {
            bool attacker = ownerOf[unit].attacker;
            if (!sk.Of(attacker).Add(unit)) return;
            if (attacker) sk.aEntered += Math.Max(0f, unit.integrity); else sk.dEntered += Math.Max(0f, unit.integrity);
        }

        private Skirmish SkirmishOf(CombatSection unit) => unit == null || !unit.Standing ? null : skirmishes.FirstOrDefault(sk =>
            (sk.a.Contains(unit) || sk.d.Contains(unit)) && (unit.battleHex == sk.hex || sk.Clash && unit.battleHex == sk.seam) &&
            sk.a.Any(x => x.Standing && (x.battleHex == sk.hex || sk.Clash && x.battleHex == sk.seam)) &&
            sk.d.Any(x => x.Standing && (x.battleHex == sk.hex || sk.Clash && x.battleHex == sk.seam)));

        private void RefreshSkirmishes()
        {
            skirmishes.RemoveAll(sk => !sk.a.Concat(sk.d).Any(x => SkirmishOf(x) == sk));
            Contacts(new List<Track>());
        }

        private void SkirmishLoss(CombatSection unit, float loss)
        {
            if (loss <= 0f) return;
            var sk = SkirmishOf(unit);
            if (sk == null) return;
            if (ownerOf[unit].attacker) sk.aLost += loss; else sk.dLost += loss;
        }

        // ===== ATTRITION =====

        /// <summary>On every Beat after contact both sides exchange Skirmish Attrition, modified by stacking, flanking, Stance and Impetus.</summary>
        private void Attrition()
        {
            RefreshSkirmishes();
            int k = 0;
            foreach (var sk in skirmishes.Where(x => x.contactBeat < beat).OrderBy(x => x.hex).ThenBy(x => x.seam).ToList())
                foreach (var s in new[] { a, d })
                {
                    var opponents = sk.Of(!s.attacker).Where(x => SkirmishOf(x) == sk).ToList();
                    foreach (var unit in sk.Of(s.attacker).Where(x => SkirmishOf(x) == sk && x.attack > 0f).OrderBy(x => s.side.sections.IndexOf(x)).ToList())
                    {
                        float share = mt.attritionShare * (unit.row == FormationRow.Back || unit.Casts ? mt.caughtInMelee : 1f);
                        var random = new CombatRandom(unchecked(report.seed * 31 + beat * 7919 + ++k));
                        Steel(s, m, field, ground, s.attacker ? assault : 1f, t, random, hits, unit, null, opponents, share);
                    }
                }
            if (hits.Count > 0) { ApplyHits(); PumpCascade(); }
        }

        // ===== ASSESSMENT: SKIRMISH VERDICTS =====

        /// <summary>The side that suffered the greater Pressure (Integrity lost inside the Skirmish over what it entered with) loses the ground.</summary>
        private void SkirmishVerdicts()
        {
            RefreshSkirmishes();
            foreach (var sk in skirmishes.OrderBy(x => x.hex).ThenBy(x => x.seam).ToList())
            {
                float pa = sk.aLost / Math.Max(1f, sk.aEntered), pd = sk.dLost / Math.Max(1f, sk.dEntered);
                if (Math.Abs(pa - pd) < mt.pressureMargin)
                {
                    // The ground holds and the Skirmish continues into the next Measure.
                    sk.aLost = sk.dLost = 0f;
                    sk.aEntered = sk.a.Where(x => SkirmishOf(x) == sk).Sum(x => Math.Max(0f, x.integrity));
                    sk.dEntered = sk.d.Where(x => SkirmishOf(x) == sk).Sum(x => Math.Max(0f, x.integrity));
                    continue;
                }
                var loser = pa > pd ? a : d;
                foreach (var unit in sk.Of(loser.attacker).Where(x => SkirmishOf(x) == sk).OrderBy(x => loser.side.sections.IndexOf(x)).ToList())
                {
                    Emit(loser, unit, BattleSpatialCause.SkirmishLost, $"Lost the Skirmish (Pressure {Math.Max(pa, pd):P0} against {Math.Min(pa, pd):P0}).");
                    Retreat(loser, unit);
                }
                skirmishes.Remove(sk);
            }
            RefreshSkirmishes();
        }

        // ===== STANDING ORDERS =====

        private List<int> ShortestPath(int from, Func<int, bool> goal, bool attacker, bool enterEnemy)
        {
            var previous = Enumerable.Repeat(-2, BattleHexLayout.HexCount).ToArray();
            var queue = new Queue<int>(); queue.Enqueue(from); previous[from] = -1;
            while (queue.Count > 0)
            {
                int h = queue.Dequeue();
                if (h != from && goal(h))
                {
                    var path = new List<int>();
                    for (int x = h; x != from; x = previous[x]) path.Insert(0, x);
                    return path;
                }
                if (h != from && Spatial.Occupants(h, !attacker).Any()) continue;
                foreach (int n in BattleHexLayout.Adjacent(h).OrderBy(x => x))
                {
                    if (previous[n] != -2 || !SpatialRules.Passable(n)) continue;
                    if (!enterEnemy && Spatial.Occupants(n, !attacker).Any()) continue;
                    previous[n] = h; queue.Enqueue(n);
                }
            }
            return new List<int>();
        }

        /// <summary>The doctrine-driven path of a Track that was not given one: exactly what Auto-Resolve would walk.</summary>
        private void PlanPath(Track track)
        {
            track.path.Clear(); track.stepBeats.Clear();
            var unit = track.unit; var s = track.side;
            if (unit == null || !unit.Standing || unit.battleHex < 0 || SkirmishOf(unit) != null || Freezes(unit)) { track.steps.Clear(); return; }
            var enemies = s.enemy.side.Standing.Where(x => x.battleHex >= 0).ToList();
            if (enemies.Count == 0) { track.steps.Clear(); return; }
            List<int> path = new List<int>();
            var order = OrderOf(track);
            switch (order)
            {
                case BattleStandingOrder.Hold: break;
                case BattleStandingOrder.Advance:
                case BattleStandingOrder.Pursue:
                {
                    var goal = order == BattleStandingOrder.Pursue ? enemies.OrderBy(x => x.mindBroken ? 0 : 1).ThenBy(x => x.IntegrityShare).First().battleHex : -1;
                    path = goal >= 0 ? ShortestPath(unit.battleHex, h => h == goal, s.attacker, true)
                        : ShortestPath(unit.battleHex, h => Spatial.Occupants(h, !s.attacker).Any(), s.attacker, true);
                    break;
                }
                case BattleStandingOrder.Volley:
                {
                    bool spell = unit.Casts && !unit.mindBroken;
                    if (Reachable(s, unit, spell).Count > 0) break;
                    int reach = SpatialRules.Reach(unit, spell);
                    path = ShortestPath(unit.battleHex, h => enemies.Any(e => SpatialRules.Distance(h, e.battleHex) <= reach), s.attacker, false);
                    break;
                }
                default:
                {
                    // Guard and Screen keep their charges within support access, without walking into contact.
                    var friends = s.side.Standing.Where(x => x != unit && x.row == FormationRow.Front && x.battleHex >= 0).ToList();
                    // In a crowded hex the first occupant keeps it; the others spread, never all into the same new hex.
                    bool crowded = Spatial.Occupants(unit.battleHex, s.attacker).First() != unit;
                    if (friends.Count == 0) break;
                    int gap = friends.Min(x => SpatialRules.Distance(unit.battleHex, x.battleHex));
                    if (gap <= SpatialRules.Tuning.supportReach && !crowded) break;
                    var claimed = new HashSet<int>(TracksOf(s).Where(x => x != track).SelectMany(x => x.steps.Select(st => st.hex)));
                    var next = BattleHexLayout.Adjacent(unit.battleHex).Where(h => SpatialRules.Passable(h) && !Spatial.Occupants(h, !s.attacker).Any())
                        .OrderBy(h => Spatial.Occupants(h, s.attacker).Count() + (claimed.Contains(h) ? 1 : 0)).ThenBy(h => friends.Min(x => SpatialRules.Distance(h, x.battleHex))).ThenBy(h => h).ToList();
                    if (next.Count > 0) path.Add(next[0]);
                    break;
                }
            }
            track.path.AddRange(path);
            ScheduleSteps(track);
        }

        /// <summary>The drilled Major of a Track on Standing Orders (Advance, Hold, Pursue strike; Volley looses; Guard and Screen protect).</summary>
        private void ComposeDrill(Track track)
        {
            var s = track.side; var unit = track.unit;
            bool spell, steel;
            if (unit == null)
            {
                if (!s.Conducted || s.conductor.leitmotif == SpellBinding.Unattuned) return;
                spell = true; steel = false;
            }
            else
            {
                if (!unit.Standing || unit.battleHex < 0 || Freezes(unit)) return;
                spell = unit.Casts && !unit.mindBroken; steel = unit.attack > 0f && unit.row != FormationRow.Support;
                if (OrderOf(track) == BattleStandingOrder.Guard && unit.row == FormationRow.Support) return;
            }
            if (!spell && !steel) return;
            var origin = Performer(track);
            // The drilled Note reads its own side's Steps only, the same whether the side is performed or played.
            var priorSide = planningSide; planningSide = s; planning = true; projectAt = Cadence;
            CombatSection target;
            try
            {
                var targets = SpatialRules.Targets(s.attacker, origin, true, SpatialRules.Reach(origin, spell), from: Projected(origin, Cadence), position: (Func<CombatSection, int>)Projected);
                target = SelectFutureTarget(s, unit, targets);
            }
            finally { planning = false; projectAt = 0; planningSide = priorSide; }
            var roots = unit == null ? new List<SpellBinding> { s.conductor.leitmotif } : unit.Roots.ToList();
            if (unit == null && AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, field.age)) roots.AddRange(s.conductor.ornaments);
            var root = spell && target != null && roots.Count > 0 ? HarmonicCircle.BestAgainst(roots, target.primary, t) : unit?.primary ?? s.conductor?.leitmotif ?? SpellBinding.Unattuned;
            var harmony = !spell ? new List<SpellBinding>() : unit == null
                ? HarmonicCircle.Seven.Where(b => b != root).OrderByDescending(s.conductor.Score).ThenBy(b => (int)b).Take((int)ConductorTier(field.age, t)).ToList()
                : unit.harmony.Where(b => b != root).ToList();
            bool legato = spell && s.side.tempo == SpellTempo.Legato;
            int channel = legato ? ChannelOf(s, new CombatCard { kind = CardKind.Spell, channeling = true }, origin, false) : 1;
            int start = Cadence - channel + 1;
            // Harmony sounds as Minor Notes on the Beats before the Core; what does not fit the Measure is not played.
            var free = Enumerable.Range(Abs(1), Math.Max(0, start - Abs(1))).Where(b => !Occupied(track, b)).OrderByDescending(b => b).ToList();
            harmony = harmony.Take(free.Count).ToList();
            var card = new CombatCard { id = spell ? "drilled-spell" : "drilled-strike", name = unit?.name ?? s.conductor.name, kind = spell ? CardKind.Spell : CardKind.Attack,
                binding = spell ? root : SpellBinding.Unattuned, minors = harmony.ToList(), purpose = SpellPurpose.Offensive,
                effects = { new CardEffect(spell ? CardOp.Spell : CardOp.Strike, CardAim.Enemy, 1f) { proximity = BattleProximity.Nearest } } };
            var dc = new DeckCard { card = card, voice = track.voice };
            var chord = new Chord { id = nextChord++, side = s, owner = track, core = dc, target = target, majorBeat = Cadence, channel = channel, firstBeat = start,
                origin = Projected(origin, Cadence), legato = legato, staccato = !legato, drilled = true, spell = spell, magical = spell, composedMeasure = m,
                seed = unchecked((int)rng.Next()), footing = legato || harmony.Count >= 2 ? BattleFooting.Braced : BattleFooting.Free,
                stanceHeld = s.side.StanceBroken ? BattleStanceHold.Broken : BattleStanceHold.Held };
            chord.notes.Add(new ChordNote { card = dc, track = track, beat = Cadence, kind = BattleNoteKind.Major, origin = chord.origin });
            for (int i = 0; i < harmony.Count; i++)
            {
                var note = new CombatCard { id = "drilled-harmony", name = harmony[i] + " harmony", kind = CardKind.Spell, binding = harmony[i], noteRole = BattleNoteRole.Minor,
                    chord = new BattleChordModifier { binding = harmony[i], joinBinding = true, requiresAdjacent = false } };
                chord.notes.Add(new ChordNote { card = new DeckCard { card = note, voice = track.voice }, track = track, beat = free[i], kind = BattleNoteKind.Minor, origin = chord.origin });
            }
            chord.firstBeat = chord.notes.Min(n => n.kind == BattleNoteKind.Major ? chord.ChannelStart : n.beat);
            chord.power = Power(s, chord);
            chords[chord.id] = chord;
        }

        /// <summary>Withdraw: break engagement and fall back up to two hexes, without a forced retreat's Composure loss.</summary>
        private void WithdrawFrom(Side s, CombatSection unit)
        {
            foreach (var sk in skirmishes.Where(x => x.a.Contains(unit) || x.d.Contains(unit))) { sk.a.Remove(unit); sk.d.Remove(unit); }
            for (int i = 0; i < mt.withdrawHexes; i++)
            {
                var exits = SpatialRules.Retreats(s.attacker, unit);
                if (exits.Count == 0) break;
                Relocate(s, unit, exits[0], BattleSpatialCause.Movement, "Withdrew from the engagement.");
            }
            var track = TrackOf(unit); if (track != null) { track.steps.Clear(); track.path.Clear(); }
            RefreshSkirmishes();
        }
    }
}
