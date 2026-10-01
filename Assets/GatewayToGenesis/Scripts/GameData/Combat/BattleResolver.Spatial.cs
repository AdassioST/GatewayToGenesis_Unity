using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    private static CombatSection Origin(Side s, CombatSection voice) => voice ??
        s.side.Standing.FirstOrDefault(x => x.eliteRole == BattleEliteRole.Conductor) ?? s.side.Standing.FirstOrDefault();

    private static List<CombatSection> Reachable(Side s, CombatSection voice, bool spell = false) =>
        s.rules.Targets(s.attacker, Origin(s, voice), true, s.rules.Reach(Origin(s, voice), spell));

    public sealed partial class BattleRun
    {
        public BattleSpatialRules SpatialRules { get; private set; }
        private readonly Queue<(Side side, CombatSection source, BattleSpatialCause cause, string detail)> cascade =
            new Queue<(Side, CombatSection, BattleSpatialCause, string)>();
        private readonly HashSet<string> emitted = new HashSet<string>();
        private readonly HashSet<Side> brokenStances = new HashSet<Side>();
        private readonly HashSet<CombatSection> panicked = new HashSet<CombatSection>();
        private readonly Dictionary<CombatSection, float> composureAtOpening = new Dictionary<CombatSection, float>();
        private bool planning;

        private void Relocate(Side s, CombatSection sec, int to, BattleSpatialCause cause, string detail)
        {
            int from = sec.battleHex;
            sec.battleHex = to;
            QueueReaction(BattleReactionTrigger.Moved, s, sec, sec);
            Record(s, sec, cause, detail, from, to);
            if (cause != BattleSpatialCause.Movement)
            {
                // Being displaced while Braced or Anchored is an Interruption.
                InterruptDisplaced(sec);
                var track = TrackOf(sec);
                if (track != null && (track.steps.Count > 0 || track.path.Count > 0)) { track.steps.Clear(); track.path.Clear(); }
                if (sec.row == FormationRow.Front && !s.side.Standing.Any(x => x != sec && x.row == FormationRow.Front && x.battleHex == from))
                    Emit(s, sec, BattleSpatialCause.CriticalPositionLost, "A frontline position was abandoned.");
            }
            if (Spatial.IsStacked(to, s.attacker)) Emit(s, sec, BattleSpatialCause.Stacking, "Friendly elements overlap; attack and defense are penalized.");
        }

        private void Retreat(Side s, CombatSection sec, bool displaced = false)
        {
            if (!sec.Standing) return;
            Emit(s, sec, displaced ? BattleSpatialCause.ForcedDisplacement : BattleSpatialCause.ForcedRetreat, "Forced one hex toward home.");
            sec.composure = Math.Max(0f, sec.composure - SpatialRules.Tuning.retreatShock);
            bool rearEdge = BattleHexLayout.IsNative(sec.battleHex, s.attacker) && BattleHexLayout.At(sec.battleHex).Rank == 2;
            var exits = SpatialRules.Retreats(s.attacker, sec);
            foreach (var sk in skirmishes.Where(x => x.a.Contains(sec) || x.d.Contains(sec))) { sk.a.Remove(sec); sk.d.Remove(sec); }
            if (rearEdge || exits.Count == 0)
            {
                bool trapped = !rearEdge && SpatialRules.Surrounded(s.attacker, sec);
                if (trapped && s.enemy.side.takesCaptives && sec.eliteRole == BattleEliteRole.None)
                {
                    Capture(s, sec, m, report);
                    Emit(s, sec, BattleSpatialCause.AdjacentFormationCollapse, "A surrounded section was subdued.");
                    Record(s, sec, BattleSpatialCause.Subjugation, "Encircled with no physical escape.");
                }
                else if (trapped && sec.eliteRole == BattleEliteRole.None)
                {
                    sec.lost += Math.Max(0f, sec.integrity); sec.integrity = 0f;
                    // States emits the casualty and adjacent collapse from this body loss.
                }
                else { sec.fled = true; sec.committed = false; Record(s, sec, BattleSpatialCause.Rout, "No legal backward exit."); }
                return;
            }
            Relocate(s, sec, exits[0], displaced ? BattleSpatialCause.ForcedDisplacement : BattleSpatialCause.ForcedRetreat, "Fallback may stack with allies.");
        }

        private void Emit(Side side, CombatSection source, BattleSpatialCause cause, string detail)
        {
            // Pressure is assessed once per cause, position and combatant each Measure; cascades cannot spin forever.
            string key = side.attacker + ":" + (source == null || cause == BattleSpatialCause.Stacking ? -1 : side.side.sections.IndexOf(source)) + ":" + source?.battleHex + ":" + cause;
            if (emitted.Add(key)) cascade.Enqueue((side, source, cause, detail));
        }

        private void Record(Side s, CombatSection sec, BattleSpatialCause cause, string detail, int from = -1, int to = -1, float loss = 0f)
        {
            report.spatialEvents.Add(new BattleSpatialEvent { measure = m, beat = beat, attacker = s.attacker, section = sec?.name, cause = cause,
                from = from < 0 ? sec?.battleHex ?? -1 : from, to = to < 0 ? sec?.battleHex ?? -1 : to, stabilityDamage = loss, detail = detail });
            report.log.Add($"Measure {m}, Beat {BattleMeasureMath.Rel(beat)}: {s.side.name} / {sec?.name ?? "formation"}: {cause} — {detail}");
        }

        private void Pressure()
        {
            foreach (var s in new[] { a, d })
            {
                foreach (var hex in s.side.Standing.Select(x => x.battleHex).Distinct().Where(h => h >= 0 && Spatial.IsStacked(h, s.attacker)))
                    Emit(s, Spatial.Occupants(hex, s.attacker).First(), BattleSpatialCause.Stacking, "Friendly stacking strains cohesion.");
                foreach (var sec in s.side.Standing.Where(x => x.battleHex >= 0).ToList())
                {
                    if (SpatialRules.Flanked(s.attacker, sec)) Emit(s, sec, BattleSpatialCause.Flanking, "Enemy access crosses the flank or rear.");
                    if (SpatialRules.Surrounded(s.attacker, sec)) Emit(s, sec, BattleSpatialCause.Surrounded, "Every adjacent physical exit is blocked or enemy occupied.");
                    var terrain = Spatial.Terrain[sec.battleHex];
                    if (terrain.river && !terrain.ford && !terrain.bridge || terrain.snow && sec.kind == SectionKind.Shock ||
                        (terrain.narrowPass || terrain.forest) && sec.width > 1f ||
                        s.side.stance == BattleStance.Crescent && !SpatialRules.Passable(2) && !SpatialRules.Passable(13))
                        Emit(s, sec, BattleSpatialCause.TerrainIncompatibility, "The ground conflicts with the occupied formation.");
                }
            }
        }

        private void PumpCascade()
        {
            do
            {
                while (cascade.Count > 0)
                {
                    var e = cascade.Dequeue(); var s = e.side; var sec = e.source;
                    float damage = SpatialRules.Tuning.Damage(e.cause) * Math.Max(0f, s.side.maxStanceStability) / 100f;
                    if (s.side.stance == BattleStance.Line && SpatialRules.DoctrineHeld(s.attacker)) damage *= SpatialRules.Tuning.lineResilience;
                    float loss = Math.Min(Math.Max(0f, s.side.stanceStability), Math.Max(0f, damage));
                    s.side.stanceStability = Math.Max(0f, s.side.stanceStability - loss);
                    Record(s, sec, e.cause, e.detail, loss: loss);
                    if (e.cause == BattleSpatialCause.EliteCasualty || e.cause == BattleSpatialCause.AdjacentFormationCollapse || e.cause == BattleSpatialCause.MindBreak)
                    {
                        foreach (var ally in s.side.Standing.Where(x => x != sec && sec != null &&
                            (x.battleHex == sec.battleHex || BattleHexLayout.AreAdjacent(x.battleHex, sec.battleHex))).ToList())
                        {
                            float shock = e.cause == BattleSpatialCause.EliteCasualty ? ally.maxComposure * SpatialRules.Tuning.eliteFallShock :
                                e.cause == BattleSpatialCause.MindBreak ? t.mindBreakShock : t.fallenShock;
                            Shock(s, ally, shock);
                        }
                        if (s.Conducted) s.bar -= t.sectionLostStrain;
                        if (e.cause == BattleSpatialCause.MindBreak && sec != null) QueueReaction(BattleReactionTrigger.AllyMindBroken, s, sec, sec);
                    }
                    if (e.cause == BattleSpatialCause.MindBreak && sec != null && sec.Standing && sec.eliteRole == BattleEliteRole.None && panicked.Add(sec) &&
                        s.enemy.side.Standing.Any(x => SpatialRules.Distance(sec.battleHex, x.battleHex) <= 1)) Retreat(s, sec);
                    if (e.cause == BattleSpatialCause.ConductorDisrupted) QueueReaction(BattleReactionTrigger.ConductorFaltered, s, sec, sec);
                    if (s.side.StanceBroken && brokenStances.Add(s))
                    {
                        Record(s, null, BattleSpatialCause.StanceBreak, "Doctrine, guards and prepared positional safety fail; Composure shock crosses the battlefield.");
                        foreach (var affected in new[] { a, d })
                        {
                            foreach (var unit in affected.side.Standing.ToList()) Shock(affected, unit, unit.maxComposure * SpatialRules.Tuning.stanceBreakShock);
                            if (affected.Conducted) affected.bar -= affected.barMax * SpatialRules.Tuning.stanceBreakShock;
                        }
                    }
                }
                foreach (var unit in a.side.Standing.Concat(d.side.Standing).Where(x => x.eliteRole != BattleEliteRole.None && x.integrity <= 0f).ToList()) EnterDeathKnell(unit);
                States(a, m, t, report); States(d, m, t, report);
                if (Conductor(a, m, t, report)) ConductorMindBreak(a);
                if (Conductor(d, m, t, report)) ConductorMindBreak(d);
                PromoteSecondary(a); PromoteSecondary(d);
                foreach (var pair in marks)
                {
                    pair.Value.wards.RemoveAll(g => g.expires < beat);
                    var s = ownerOf[pair.Key];
                    var lost = pair.Value.wards.Where(g => s.side.StanceBroken && g.raised < beat || !g.source.Standing ||
                        g.source.mindBroken || !SpatialRules.Support(g.source, pair.Key, g.range)).ToList();
                    if (lost.Count > 0)
                    {
                        Emit(s, pair.Key, BattleSpatialCause.GuardLost, "The provider, adjacency or Stance no longer supports this Ward.");
                        foreach (var link in lost) pair.Value.wards.Remove(link);
                    }
                }
            } while (cascade.Count > 0);
        }

        private void PromoteSecondary(Side s)
        {
            if (!s.conductorBroken) return;
            var successor = s.side.Standing.FirstOrDefault(x => x.eliteRole == BattleEliteRole.CommandStaff && x.leader != null && !x.mindBroken && !x.deathKnell);
            if (successor == null) return;
            successor.eliteRole = BattleEliteRole.Conductor;
            s.conductor = s.side.conductor = successor.leader; s.conductorBroken = s.conductorFallen = false;
            s.maladaptation = BattleConductorMaladaptation.None; s.withdrawFloor = 0f;
            s.barMax = Math.Max(1f, successor.maxComposure); s.bar = Math.Min(s.barMax, successor.composure);
            s.stack = Boost.Of(s.conductor, t.greatPerStar); s.rally += s.stack.rally; s.recon += s.stack.recon; s.woundedShare += s.stack.wounded;
            report.log.Add($"Measure {m}: {successor.name} takes the baton as secondary Conductor; the original loss and its shock remain.");
        }

        private void AssessComposureShock()
        {
            foreach (var pair in composureAtOpening)
                if (pair.Value - pair.Key.composure >= pair.Key.maxComposure * SpatialRules.Tuning.severeShockShare)
                    Emit(ownerOf[pair.Key], pair.Key, BattleSpatialCause.SevereComposureShock, "Accumulated harm, backlash or Toll severely shocked Composure.");
        }

        private void Shock(Side s, CombatSection sec, float shock)
        {
            if (shock <= 0f) return;
            shock = ComposureLoss(sec, shock);
            sec.composure -= shock;
            if (shock >= sec.maxComposure * SpatialRules.Tuning.severeShockShare)
                Emit(s, sec, BattleSpatialCause.SevereComposureShock, "Severe Composure shock strains the formation.");
        }

        private List<BattlePosition> Positions() => new[] { a, d }.SelectMany(s => s.side.sections.Select((x, i) =>
            new BattlePosition { attacker = s.attacker, section = i, hex = x.battleHex, standing = x.Standing, mindBroken = x.mindBroken })).ToList();

        private List<CombatSection> EffectTargets(Side s, DeckCard dc, CardEffect effect)
        {
            var voice = Origin(s, VoiceOf(s, dc));
            // Compulsions always resolve through Rank Proximity: the nearest Rank 1 enemy, or the whole Rank for a line.
            if ((dc.card.corrupted || dc.card.cathartic) && effect.aim == CardAim.EnemyLine) return CrisisLine(s, dc);
            if ((dc.card.corrupted || dc.card.cathartic) && effect.aim == CardAim.Enemy)
            { var target = CrisisTarget(s, dc); return target == null ? new List<CombatSection>() : new List<CombatSection> { target }; }
            bool enemy = effect.aim == CardAim.Enemy || effect.aim == CardAim.EnemyLine;
            if (effect.aim == CardAim.Self) return voice != null && voice.Standing ? new List<CombatSection> { voice } : new List<CombatSection>();
            int range = effect.range > 0 ? effect.range : enemy ?
                effect.op == CardOp.Strike || effect.op == CardOp.Push || effect.op == CardOp.IntegrityDamage ? SpatialRules.Reach(voice) : SpatialRules.Tuning.spellReach :
                dc.card.kind == CardKind.Command ? BattleHexLayout.HexCount : SpatialRules.Tuning.supportReach;
            return SpatialRules.Targets(s.attacker, voice, enemy, range, effect.proximity, effect.ignoreGuard, Projected(voice), planning ? (Func<CombatSection, int>)Projected : null);
        }

        public IReadOnlyList<CombatSection> LegalTargets(bool attacker, int handIndex, int beatOfMeasure = 0)
        {
            var s = Of(attacker);
            if (!open || s.perf == null || handIndex < 0 || handIndex >= s.perf.hand.Count) return Array.Empty<CombatSection>();
            planning = true; projectAt = beatOfMeasure > 0 ? Abs(beatOfMeasure) : Cadence;
            try
            {
                var dc = s.perf.deck[s.perf.hand[handIndex]];
                var pools = dc.card.effects.Where(e => e != null && (e.aim == CardAim.Enemy || e.aim == CardAim.Ally)).Select(e => EffectTargets(s, dc, e)).ToList();
                return pools.Count == 0 ? new List<CombatSection>() : pools.Skip(1).Aggregate(pools[0], (legal, next) => legal.Intersect(next).ToList());
            }
            finally { planning = false; projectAt = 0; }
        }

        private string TargetFailure(Side s, DeckCard dc, CombatSection target)
        {
            if (target != null && !target.Standing) return "The committed target is no longer available.";
            var aimed = dc.card.effects.Where(e => e != null && (e.aim == CardAim.Enemy || e.aim == CardAim.Ally)).ToList();
            if (aimed.Count > 0 && (target == null || aimed.Any(e => !EffectTargets(s, dc, e).Contains(target)))) return "The committed target is outside legal reach or protected by a guard.";
            if (dc.card.effects.Any(e => e != null && e.aim == CardAim.EnemyLine && EffectTargets(s, dc, e).Count == 0)) return "There is no enemy in legal reach.";
            if (target != null && dc.card.effects.Any(e => e != null && e.aim == CardAim.EnemyLine && !EffectTargets(s, dc, e).Contains(target))) return "The committed area target is outside legal reach.";
            return null;
        }

        private IEnumerable<CombatSection> EffectAllies(Side s, DeckCard dc, CardEffect e, CombatSection target)
        {
            var allies = EffectTargets(s, dc, e);
            return e.aim == CardAim.Ally ? allies.Where(x => x == target) : allies;
        }

        private CombatSection EffectTarget(Side s, DeckCard dc, CardEffect e, CombatSection wanted)
        {
            var targets = EffectTargets(s, dc, e);
            return wanted != null ? targets.Contains(wanted) ? wanted : null : targets.OrderBy(x => x.IntegrityShare).FirstOrDefault();
        }

        private IEnumerable<CombatSection> ResolvingTargets(Side s, DeckCard dc, CardEffect e, CombatSection wanted) =>
            e.aim == CardAim.EnemyLine || e.aim == CardAim.Allies ? EffectTargets(s, dc, e) :
            EffectTarget(s, dc, e, wanted) is CombatSection target ? new[] { target } : Array.Empty<CombatSection>();

        /// <summary>A Ward's Strength in Integrity points: a physical Guard counts the parries it lends; a magical Ward its share times the tuning.</summary>
        private float WardStrength(Side s, DeckCard dc, CardEffect e, CombatSection ally, float scale)
        {
            if (e.flat || ally == null) return Math.Max(0f, e.amount * scale);
            return e.op == CardOp.Guard
                ? Math.Max(0f, e.amount * scale * BaseParry(s, ally) * (t.undefendedHit - t.defendedHit) * t.integrityPerHit)
                : Math.Max(0f, e.amount * scale * mt.wardPerShare);
        }

        /// <summary>Raises a Ward over the effect's allies from this Beat; by default it stands until the Measure's Abjuration has answered.</summary>
        private void GrantGuard(Side s, DeckCard dc, CardEffect e, CombatSection target, float scale, Chord chord, float rendition)
        {
            var source = Origin(s, VoiceOf(s, dc));
            if (source == null || source.mindBroken || s.side.StanceBroken) return;
            int range = e.range > 0 ? e.range : dc.card.kind == CardKind.Command ? BattleHexLayout.HexCount : SpatialRules.Tuning.supportReach;
            int resonance = e.op == CardOp.Ward ? BattleMeasureMath.Mastery(LegendOf(TrackOf(source))?.Score(SpellBinding.Resonance) ?? 0) : 0;
            if (e.range <= 0 && resonance > 0) range += resonance / Math.Max(1, combatSettings.Rhythm.resonanceReachPerTiers);
            var scope = e;
            if (range > (e.range > 0 ? e.range : SpatialRules.Tuning.supportReach)) { scope = e.Clone(); scope.range = range; }
            foreach (var ally in EffectAllies(s, dc, scope, target))
            {
                Mark(ally).wards.Add(new GuardLink { source = source, range = range, amount = WardStrength(s, dc, e, ally, scale) * (1f + resonance * combatSettings.Rhythm.resonanceWardPower), raised = beat,
                    expires = e.durationBeats > BattleMeasureMath.Beats ? beat + e.durationBeats : Math.Max(beat, Cadence),
                    physical = e.op == CardOp.Guard, magical = e.op == CardOp.Ward, binding = RootOf(dc, source), card = chord?.core.card.id ?? dc.card.id,
                    chord = chord, rendition = rendition });
                QueueReaction(BattleReactionTrigger.Guarded, s, reactionAttacker ?? actingVoice, ally);
            }
        }
    }
}
