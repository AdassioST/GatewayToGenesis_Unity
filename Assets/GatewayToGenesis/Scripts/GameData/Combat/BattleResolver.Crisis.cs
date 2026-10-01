using System;
using System.Collections.Generic;
using System.Linq;

// Mind Break and the Corrupted Hand (vault: Combat System.md): an elite crossing into Spiraling keeps its turn but its
// hand becomes the maladaptive face of its traits; two compulsions resolved in one Measure are the Corroded Gambit, which
// unlocks the Cathartic Cadenza; Co-Regulation is the other way back. A Mind Broken Conductor's army inherits its
// maladaptive behavior; ordinary sections break by scale. None of it heals persistent strain.
public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        private sealed class Crisis
        {
            public bool active, cadenzaReady;
            public int measure, resolved;
            public readonly List<int> held = new List<int>();
            public readonly HashSet<long> counted = new HashSet<long>();
        }
        private readonly Dictionary<Track, Crisis> crises = new Dictionary<Track, Crisis>();
        /// <summary>Patients a Co-Regulation steadied this Measure: further Battle Composure loss is softened.</summary>
        private readonly HashSet<CombatSection> steadied = new HashSet<CombatSection>();
        private bool composingCrisis;
        private BattleCrisisTuning crisisTuning => combatSettings.Crisis;

        public IReadOnlyList<BattleCrisisView> Crises(bool attacker) => TracksOf(Of(attacker)).Where(Elite).Select(track => {
            crises.TryGetValue(track, out var state); var legend = LegendOf(track); var unit = Performer(track);
            return new BattleCrisisView { voice = track.voice, character = track.Name, expression = BattleCrisisLogic.Expression(legend, track.unit),
                mindBroken = state?.active == true, cadenzaReady = state?.cadenzaReady == true, steadied = track.unit != null && steadied.Contains(track.unit),
                resolvedThisMeasure = state?.measure == m ? state.resolved : 0, battleComposure = track.unit == null ? track.side.bar : unit.composure,
                maximum = track.unit == null ? track.side.barMax : unit.maxComposure, persistentStrain = legend?.strain ?? 0f };
        }).ToList();

        /// <summary>The behavior a Mind Broken Conductor's army has inherited (None while the Conductor is coherent, fallen or replaced).</summary>
        public BattleConductorMaladaptation ConductorMaladaptation(bool attacker) => Of(attacker).maladaptation;

        private void CrisisEvent(Track track, BattleCrisisCause cause, string detail, int resolved = 0) =>
            report.crises.Add(new BattleCrisisEvent { attacker = track.side.attacker, voice = track.voice, character = track.Name,
                measure = m, beat = beat, cause = cause, detail = detail, resolved = resolved });

        private bool CrisisCard(Side s, int hand) => s.perf != null && hand >= 0 && hand < s.perf.hand.Count &&
            (s.perf.deck[s.perf.hand[hand]].card.corrupted || s.perf.deck[s.perf.hand[hand]].card.cathartic);

        /// <summary>Compulsions may be chosen between performed Beats. They never rewrite a sounded Beat or an active phrase.</summary>
        public string CommitCrisisCard(bool attacker, int handIndex, int beatOfMeasure, float rendition = 1f)
        {
            var s = Of(attacker);
            if (!CrisisCard(s, handIndex)) return "Choose a Corrupted Instinct or an unlocked Cathartic Cadenza.";
            if (rhythm != null || pendingCadence != null) return "Finish the current phrase first.";
            if (beatOfMeasure < 1 || beatOfMeasure > 4 || Abs(beatOfMeasure) <= beat) return "Choose an upcoming Beat in this Measure.";
            composingCrisis = true;
            try { return CommitCard(attacker, handIndex, rendition: rendition, beat: beatOfMeasure); }
            finally { composingCrisis = false; }
        }

        /// <summary>Elites (and a Conductor with no section of its own) in Spiraling hold Corrupted Instinct Cards in place of their hand.</summary>
        private void SyncCrises()
        {
            foreach (var track in tracks.Values.Where(x => x.unit != null ? x.unit.eliteRole != BattleEliteRole.None : x.voice < 0 && x.side.conductor != null).ToList())
            {
                var s = track.side; var unit = track.unit;
                bool standing = unit == null ? s.conductor != null && !s.conductorFallen : unit.Standing;
                bool broken = standing && (unit == null ? s.bar <= s.barMax * mt.spiralingBelow : unit.mindBroken);
                if (!crises.TryGetValue(track, out var state)) crises[track] = state = new Crisis();
                if (state.measure != m) { state.measure = m; state.resolved = 0; state.counted.Clear(); }
                if (!broken && state.active) RecoverCrisis(track, state, false);
                if (!broken) continue;
                if (s.perf == null) s.perf = new Performance { deck = new List<DeckCard>(), rng = new CombatRandom(unchecked(report.seed * 7 + track.voice)) };
                if (!state.active)
                {
                    state.active = true; state.cadenzaReady = false;
                    state.resolved = 0; state.counted.Clear();
                    if (unit != null) Pollute(s.attacker, track.voice, BattlePollution.Panic);
                    CrisisEvent(track, BattleCrisisCause.MindBreak, BattleCrisisLogic.Expression(LegendOf(track), unit));
                }
                var p = s.perf;
                foreach (int i in p.hand.Where(i => TrackOf(s, p.deck[i]) == track && !p.deck[i].card.corrupted).ToList())
                { p.hand.Remove(i); if (p.deck[i].card.cathartic) p.exhausted.Add(i); else if (!state.held.Contains(i)) state.held.Add(i); }
                if (!p.hand.Any(i => TrackOf(s, p.deck[i]) == track && p.deck[i].card.corrupted) && !chords.Values.Any(c => c.Live && c.owner == track && c.core.card.corrupted))
                    for (int v = 0; v < 2; v++) AddCrisisCard(track, BattleCrisisLogic.Instinct(LegendOf(track), unit, v, mt.spiralingBelow, crisisTuning));
            }
        }

        private void AddCrisisCard(Track track, CombatCard card)
        {
            var p = track.side.perf; int index = p.deck.Count;
            if (track.unit != null && steadied.Contains(track.unit)) card = BattleCrisisLogic.Steady(card);
            p.deck.Add(new DeckCard { card = card, voice = track.voice, legend = LegendOf(track), source = "Mind Break" }); p.hand.Add(index);
        }

        private void RecoverCrisis(Track track, Crisis state, bool gambit)
        {
            var s = track.side; var p = s.perf; var unit = track.unit; var tuning = crisisTuning;
            state.active = false;
            if (unit != null) { unit.mindBroken = false; if (gambit) unit.composure = Math.Max(unit.composure, unit.maxComposure * tuning.gambitRestore); }
            bool conductor = unit == null || unit.leader == s.conductor && s.conductor != null;
            if (conductor)
            {
                bool restore = s.conductorBroken; s.conductorBroken = false; s.maladaptation = BattleConductorMaladaptation.None; s.withdrawFloor = 0f;
                if (gambit) s.bar = Math.Max(s.bar, s.barMax * tuning.gambitRestore);
                s.stack = s.conductor == null ? None : Boost.Of(s.conductor, t.greatPerStar);
                if (restore) { s.rally += s.stack.rally; s.recon += s.stack.recon; s.woundedShare += s.stack.wounded; }
            }
            if (p != null)
            {
                foreach (int index in p.hand.Where(i => TrackOf(s, p.deck[i]) == track && p.deck[i].card.corrupted).ToList()) { p.hand.Remove(index); p.exhausted.Add(index); }
                foreach (int index in state.held) if (!p.hand.Contains(index) && !p.exhausted.Contains(index)) p.hand.Add(index);
            }
            state.held.Clear();
            if (gambit) { state.cadenzaReady = true; AddCrisisCard(track, BattleCrisisLogic.Cadenza(LegendOf(track), unit, tuning)); }
            CrisisEvent(track, gambit ? BattleCrisisCause.CorrodedGambit : BattleCrisisCause.CoRegulation,
                gambit ? "Two resolved compulsions in one Measure restored tactical control and unlocked the Cadenza." : "Battle Composure restored tactical control.", state.resolved);
            // A heroic recovery of the Conductor reaches the whole army: its psychology is strategic.
            if (gambit && conductor && s.conductor != null)
            {
                foreach (var ally in s.side.Standing) ally.composure = Math.Min(ally.maxComposure, Math.Max(0f, ally.composure) + ally.maxComposure * tuning.conductorGambitComposure);
                s.side.stanceStability = Math.Min(s.side.maxStanceStability, s.side.stanceStability + s.side.maxStanceStability * tuning.conductorGambitStance);
                report.log.Add($"Measure {m}: {s.conductor.name} fights back through the Corroded Gambit; {s.side.name} hear the carrier signal return.");
            }
        }

        private void ResolvedCrisis(Chord c)
        {
            if (!crises.TryGetValue(c.owner, out var state)) return;
            if (c.core.card.cathartic) { state.cadenzaReady = false; return; }
            if (!c.core.card.corrupted || !state.active || c.state != BattleChordState.Resolved || c.releaseCancelled || c.notes.Any(n => n.failed || n.flicker)) return;
            if (state.measure != m) { state.measure = m; state.resolved = 0; state.counted.Clear(); }
            if (!state.counted.Add(c.id)) return;
            state.resolved++; CrisisEvent(c.owner, BattleCrisisCause.CorruptedResolution, c.core.Name + " resolved.", state.resolved);
            if (state.resolved >= 2) RecoverCrisis(c.owner, state, true);
        }

        private CombatSection CrisisTarget(Side s, DeckCard dc) => CrisisLine(s, dc).FirstOrDefault() ??
            (dc.card.cathartic ? s.side.Standing.Where(x => x != Performer(TrackOf(s, dc))).OrderBy(x => SpatialRules.Distance(Performer(TrackOf(s, dc)).battleHex, x.battleHex)).FirstOrDefault()
                ?? Performer(TrackOf(s, dc)) : null);

        /// <summary>Rank Proximity for compulsions: every enemy in Rank 1 (Neutral Ground when Rank 1 is empty), nearest first.</summary>
        private List<CombatSection> CrisisLine(Side s, DeckCard dc)
        {
            var unit = Performer(TrackOf(s, dc));
            var enemies = s.enemy.side.Standing.Where(x => x.battleHex >= 0 && BattleHexLayout.At(x.battleHex).Rank == 1).ToList();
            if (enemies.Count == 0) enemies = s.enemy.side.Standing.Where(x => x.battleHex >= 0 && BattleHexLayout.At(x.battleHex).Territory == BattleTerritory.Neutral).ToList();
            return enemies.OrderBy(x => unit == null ? 0 : SpatialRules.Distance(unit.battleHex, x.battleHex)).ThenBy(x => s.enemy.side.sections.IndexOf(x)).ToList();
        }

        /// <summary>Spatial Chaos: the two nearest allies are knocked back one hex, stacking where they land, and shaken.</summary>
        private void SpatialChaos(Side s, CombatSection source, float damage)
        {
            if (source == null) return;
            foreach (var ally in s.side.Standing.Where(x => x != source).OrderBy(x => SpatialRules.Distance(source.battleHex, x.battleHex)).ThenBy(x => s.side.sections.IndexOf(x)).Take(2).ToList())
            {
                HitOf(hits, ally).composure += damage; HitOf(hits, ally).friendlyFire = true;
                int to = SpatialRules.Retreats(s.attacker, ally).DefaultIfEmpty(-1).First();
                if (to >= 0) Relocate(s, ally, to, BattleSpatialCause.ForcedDisplacement, "A corrupted compulsion displaced its nearest allies.");
            }
        }

        /// <summary>Possession: the two nearest allies are pulled one hex toward the performer (into its hex when adjacent), stacking, and shaken.</summary>
        private void Gather(Side s, CombatSection source, float damage)
        {
            if (source == null || source.battleHex < 0) return;
            foreach (var ally in s.side.Standing.Where(x => x != source && x.battleHex >= 0 && x.battleHex != source.battleHex)
                .OrderBy(x => SpatialRules.Distance(source.battleHex, x.battleHex)).ThenBy(x => s.side.sections.IndexOf(x)).Take(2).ToList())
            {
                HitOf(hits, ally).composure += damage; HitOf(hits, ally).friendlyFire = true;
                int to = BattleHexLayout.AreAdjacent(ally.battleHex, source.battleHex) ? source.battleHex : BattleHexLayout.Adjacent(ally.battleHex)
                    .Where(h => SpatialRules.Passable(h) && !Spatial.Occupants(h, !s.attacker).Any())
                    .Where(h => SpatialRules.Distance(h, source.battleHex) < SpatialRules.Distance(ally.battleHex, source.battleHex)).OrderBy(h => h).DefaultIfEmpty(-1).First();
                if (to >= 0) Relocate(s, ally, to, BattleSpatialCause.ForcedDisplacement, "A possessive compulsion pulled an ally close.");
            }
        }

        private void CadenzaBlast(Side s, CombatSection center, float damage)
        {
            if (center == null) return;
            foreach (var unit in a.side.Standing.Concat(d.side.Standing).Where(x => x.battleHex == center.battleHex || BattleHexLayout.AreAdjacent(x.battleHex, center.battleHex)).ToList())
            { var hit = HitOf(hits, unit); hit.integrity += damage; hit.composure += damage * .25f; hit.friendlyFire |= ownerOf[unit] == s; }
        }

        private void PlanCrises(Side s)
        {
            if (s.perf == null) return;
            foreach (int index in s.perf.hand.Where(i => s.perf.deck[i].card.corrupted || s.perf.deck[i].card.cathartic).ToList())
            {
                var track = TrackOf(s, s.perf.deck[index]);
                int rel = Enumerable.Range(1, 4).FirstOrDefault(b => Abs(b) > beat && !Occupied(track, Abs(b)) && !Rests(s)[b]);
                if (rel > 0) CommitCrisisCard(s.attacker, s.perf.hand.IndexOf(index), rel, st.autoRendition);
            }
        }

        // ===== CO-REGULATION =====

        /// <summary>
        /// A successful heartbeat steadies the patient for the rest of the Measure: further Battle Composure loss is softened
        /// and its corrupted cards keep their power without their collateral. Persistent strain is untouched.
        /// </summary>
        private void Steady(Side s, CombatSection patient)
        {
            if (patient == null || !steadied.Add(patient)) return;
            var track = TrackOf(patient); var p = s.perf;
            if (track != null && p != null)
            {
                foreach (int i in p.hand.Where(i => TrackOf(s, p.deck[i]) == track && p.deck[i].card.corrupted).ToList())
                    p.deck[i] = new DeckCard { card = BattleCrisisLogic.Steady(p.deck[i].card), voice = p.deck[i].voice, legend = p.deck[i].legend, scale = p.deck[i].scale,
                        major = p.deck[i].major, source = p.deck[i].source };
                foreach (var c in chords.Values.Where(c => c.Live && c.owner == track && c.core.card.corrupted && !c.Major.sounded))
                    c.core.card = c.Major.card.card = BattleCrisisLogic.Steady(c.core.card);
            }
            if (track != null) CrisisEvent(track, BattleCrisisCause.Steadied, "The heartbeat met: further deterioration slows and the compulsions lose their collateral.");
        }

        private float ComposureLoss(CombatSection sec, float composure) => composure > 0f && steadied.Contains(sec) ? composure * crisisTuning.steadiedLoss : composure;

        // ===== CONDUCTOR MIND BREAK =====

        /// <summary>The army inherits the Mind Broken Conductor's maladaptive behavior, on the Tracks it no longer composes for.</summary>
        private void ConductorMindBreak(Side s)
        {
            var section = s.side.sections.FirstOrDefault(x => x.eliteRole == BattleEliteRole.Conductor && x.leader == s.conductor);
            if (s.conductorFallen || section != null && !section.Standing) { s.maladaptation = BattleConductorMaladaptation.None; return; }
            s.maladaptation = BattleCrisisLogic.Conductor(s.conductor, section);
            s.withdrawFloor = s.maladaptation == BattleConductorMaladaptation.Fearful ? crisisTuning.fearfulWithdraw : 0f;
            if (s.maladaptation == BattleConductorMaladaptation.None) return;
            var track = TrackOf(s, -1);
            string how = s.maladaptation == BattleConductorMaladaptation.Aggressive ? "pushes the army into reckless attacks" :
                s.maladaptation == BattleConductorMaladaptation.Fearful ? "turns every order defensive and looks for a way out of the fight" : "smothers improvisation and spills its fear on those beside it";
            if (track != null) CrisisEvent(track, BattleCrisisCause.ConductorMaladaptation, $"{s.conductor.name} {how}.");
            report.log.Add($"Measure {m}: the Mind Broken {s.conductor.name} {how}.");
        }

        /// <summary>The Standing Order a Track actually performs: a Mind Broken Conductor's inheritance, or an ordinary section's break.</summary>
        private BattleStandingOrder OrderOf(Track track)
        {
            var order = track.order;
            if (track.unit != null && track.unit.eliteRole == BattleEliteRole.None && track.unit.mindBroken)
                return order == BattleStandingOrder.Volley ? BattleStandingOrder.Volley : BattleStandingOrder.Hold;
            if (Elite(track)) return order;
            switch (track.side.maladaptation)
            {
                case BattleConductorMaladaptation.Aggressive: return order == BattleStandingOrder.Volley ? order : order == BattleStandingOrder.Pursue ? order : BattleStandingOrder.Advance;
                case BattleConductorMaladaptation.Fearful: return order == BattleStandingOrder.Advance || order == BattleStandingOrder.Pursue ? BattleStandingOrder.Hold : order;
                default: return order;
            }
        }

        /// <summary>At Visualization: an aggressive inheritance hits harder and guards less; ordinary broken sections freeze, panic or rout.</summary>
        private void MaladaptiveMeasure(Side s)
        {
            if (s.maladaptation == BattleConductorMaladaptation.Aggressive)
            {
                s.surge += crisisTuning.aggressiveSurge;
                foreach (var unit in s.side.Standing.Where(x => x.eliteRole == BattleEliteRole.None))
                { var k = Mark(unit); k.expose = Math.Max(k.exposeLeft > 0 ? k.expose : 0f, crisisTuning.aggressiveExposure); k.exposeLeft = Math.Max(k.exposeLeft, 1); }
            }
            bool moved = false;
            foreach (var unit in s.side.Standing.Where(x => x.eliteRole == BattleEliteRole.None && x.mindBroken).ToList())
            {
                bool engaged = SkirmishOf(unit) != null;
                var kind = BattleCrisisLogic.OrdinaryBreak(unit, engaged, s.enemy.side.takesCaptives, crisisTuning);
                bool threatened = s.enemy.side.Standing.Any(x => SpatialRules.Distance(x.battleHex, unit.battleHex) <= 1);
                if (kind == BattleOrdinaryBreak.Rout || kind == BattleOrdinaryBreak.Panic && threatened && !engaged)
                {
                    Retreat(s, unit); moved = true;
                    var track = TrackOf(unit); if (track != null) { track.path.Clear(); track.steps.Clear(); track.pathComposed = false; }
                }
                if (kind != BattleOrdinaryBreak.None)
                    report.crises.Add(new BattleCrisisEvent { attacker = s.attacker, voice = s.side.sections.IndexOf(unit), character = unit.name, measure = m, beat = beat,
                        cause = BattleCrisisCause.OrdinaryBreak, detail = kind.ToString() });
            }
            // A disorganized retreat changes the ground before anything is composed: its stacking and exposure count now.
            if (moved) { Pressure(); PumpCascade(); }
        }

        /// <summary>At the Toll: a controlling Conductor's fear spills onto the allies beside it.</summary>
        private void MaladaptiveToll(Side s)
        {
            if (s.maladaptation != BattleConductorMaladaptation.Controlling) return;
            var origin = s.side.sections.FirstOrDefault(x => x.eliteRole == BattleEliteRole.Conductor && x.leader == s.conductor && x.Standing) ?? Origin(s, null);
            if (origin == null) return;
            foreach (var ally in s.side.Standing.Where(x => x != origin && SpatialRules.Distance(x.battleHex, origin.battleHex) <= 1).ToList())
            {
                ally.composure -= ComposureLoss(ally, crisisTuning.controllingSpill);
                Emit(s, ally, BattleSpatialCause.FriendlyFire, "The controlling Conductor's fear spilled onto those beside it.");
            }
        }

        /// <summary>A frozen ordinary section takes no Steps and sounds no drilled Note.</summary>
        private bool Freezes(CombatSection unit) => unit != null && BattleCrisisLogic.OrdinaryBreak(unit, false, false, crisisTuning) == BattleOrdinaryBreak.Freeze;
    }
}
