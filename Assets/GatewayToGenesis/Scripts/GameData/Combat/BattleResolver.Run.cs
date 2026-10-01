using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    /// <summary>
    /// One battle's shared state, played Measure by Measure. <see cref="BeginMeasure"/> is the Visualization: hands are
    /// dealt and every performed side composes and commits its Score (truthful, unchangeable intent). A manual side then
    /// composes in held time (<see cref="CommitCard"/>, <see cref="CommitChord"/>, <see cref="ComposePath"/>,
    /// <see cref="Redraw"/>...), which costs no Beats. <see cref="ResolveMeasure"/> (or <see cref="Perform"/>, Beat by
    /// Beat) commits and performs Beats 1-4 for both armies at once, then the Toll and Assessment. Chords that span
    /// Measures (delayed, Suspended, Hyper) carry over. <see cref="Finish"/> plays out what is left and returns the report.
    /// </summary>
    public sealed partial class BattleRun
    {
        private readonly CombatTuning t;
        private readonly SymphonyTuning st;
        private readonly Battlefield field;
        private readonly GroundSpec ground;
        private CombatRandom rng;
        private readonly BattleReport report;
        private readonly Side a, d;
        private readonly Dictionary<CombatSection, Side> ownerOf = new Dictionary<CombatSection, Side>();
        private readonly Dictionary<CombatSection, CardMarks> marks = new Dictionary<CombatSection, CardMarks>();
        private readonly bool auto;
        private readonly float assault, baseEntrench;
        private float defenderDefense;
        private Dictionary<CombatSection, Hit> hits = new Dictionary<CombatSection, Hit>();
        private int m, end;
        private bool aLeft, dLeft, open, finished, finishing;

        /// <summary>The report so far (complete once <see cref="Finish"/> has run).</summary>
        public BattleReport Report => report;
        /// <summary>The measure being played (0 before the first).</summary>
        public int Measure => m;
        public bool Over { get; private set; }
        public Battlefield Field => field;
        public BattleSide Attacker => a.side;
        public BattleSide Defender => d.side;
        /// <summary>The shared sixteen-hex state in manual battles and auto-resolution.</summary>
        public BattleSpatialState Spatial { get; }
        /// <summary>What the auto-resolve expected before the first measure (null: not forecast). A manual side that wins what it gave it at most <see cref="BattleVerdicts.LegendaryChance"/> wins a Legendary Victory.</summary>
        public BattleForecast Prediction { get; set; }
        // Cards the player played by hand, per side (a battle the performer played for them is not won by hand).
        private int handA, handD;
        private readonly bool requiresManual;

        internal BattleRun(BattleSetup setup, CombatSettings settings, bool auto)
        {
            settings = settings ?? new CombatSettings();
            combatSettings = settings; mt = settings.Measure;
            this.auto = auto;
            requiresManual = setup.RequiresManual && !auto;
            t = settings.Tuning;
            st = settings.Symphony.Tuning;
            field = setup.field ?? new Battlefield();
            ground = settings.Ground(field.ground);
            rng = new CombatRandom(setup.seed);
            report = new BattleReport { seed = setup.seed };

            a = new Side { side = setup.attacker, attacker = true, footing = field.attackerGround, marks = marks, spiralingBelow = mt.spiralingBelow };
            d = new Side { side = setup.defender, attacker = false, marks = marks, spiralingBelow = mt.spiralingBelow };
            a.enemy = d; d.enemy = a;
            a.commandCulture = BattleDeploymentLogic.Doctrine(settings, a.side).commandCulture;
            d.commandCulture = BattleDeploymentLogic.Doctrine(settings, d.side).commandCulture;
            foreach (var sec in a.side.sections) ownerOf[sec] = a;
            foreach (var sec in d.side.sections) ownerOf[sec] = d;
            foreach (var s in new[] { a, d })
            {
                foreach (var sec in s.side.sections)
                {
                    sec.committed = false;
                    sec.fled = false;
                    sec.captured = false;
                    sec.destroyed = sec.permanentDeath || sec.integrity <= 0f && sec.eliteRole == BattleEliteRole.None;
                    if (!sec.destroyed) EnterDeathKnell(sec);
                    sec.mindBroken = !sec.destroyed && (sec.eliteRole != BattleEliteRole.None ? sec.ComposureShare < mt.spiralingBelow : sec.composure <= 0f);
                    sec.lost = sec.dead = sec.wounded = 0f;
                    sec.casts = sec.misfires = sec.timesMindBroken = 0;
                    s.startIntegrity[sec] = Math.Max(0f, sec.integrity);
                }
                s.startComposure = s.side.LineComposure;
                Prepare(s, t);
            }

            Spatial = new BattleSpatialState(a.side, d.side, field, settings);
            SpatialRules = new BattleSpatialRules(Spatial, settings.Spatial);
            foreach (var s in new[] { a, d }) { s.rules = SpatialRules; s.spatialEvent = Emit; s.beat = () => beat; }
            a.side.scoreInputs = a.side.scoreInputs ?? BattleCompositionLogic.Describe(a.side, field, settings);
            d.side.scoreInputs = d.side.scoreInputs ?? BattleCompositionLogic.Describe(d.side, field, settings);
            BuildTracks();

            baseEntrench = Math.Min(t.maxEntrench, setup.defender.entrenchment + d.entrench);
            defenderDefense = DefenderDefense(baseEntrench);
            // Coming downhill carries the attacker as higher ground holds the defender.
            assault = ground.assault * (field.riverCrossing ? t.riverCrossing : 1f) * (1f + Math.Min(t.heightCap, field.downhill / 0.1f * t.heightDefense));

            // Each side's Symphony is weighed on this field before a note is played.
            report.attacker.symphonyPower = SymphonyPower.Rate(a.side, settings, field, true).power;
            report.defender.symphonyPower = SymphonyPower.Rate(d.side, settings, field, false).power;

            Opening(report, a, d, field, ground, baseEntrench);
            StartSymphony(a, setup.seed);
            StartSymphony(d, setup.seed);

            // The Overture: who reads the ground first (a Context input; no side moves or strikes first).
            float reconA = a.recon, reconD = d.recon + (field.concealed ? 1f : 0f);
            if (reconA > reconD) a.initiative = true;
            else if (reconD > reconA) d.initiative = true;
            if (a.initiative) report.log.Add($"Overture: {a.side.name} read the ground first; their first Measure lands harder.");
            if (d.initiative)
            {
                if (field.concealed)
                {
                    foreach (var s in a.side.Standing) s.composure -= t.ambushShock * s.maxComposure;
                    report.log.Add($"Overture: {a.side.name} walk into an ambush under the {(string.IsNullOrEmpty(field.cover) ? "cover" : field.cover.Replace('-', ' '))}; the line wavers before a blow is struck.");
                }
                else report.log.Add($"Overture: {d.side.name} saw them coming; their first Measure lands harder.");
            }
            foreach (var s in new[] { a, d }) if (s.side.StanceBroken) brokenStances.Add(s);
            RefreshSkirmishes();
            report.timeline.Add(new BattleMeasure { measure = 0, attacker = Bars(a), defender = Bars(d), positions = Positions() });
            if (t.maxMeasures <= 0) Over = true;
        }

        private float DefenderDefense(float entrenchLevels) =>
            ground.defense * (1f + entrenchLevels * t.entrenchDefense) * (field.settlement ? t.settlementDefense : 1f) *
            (1f + Math.Min(t.heightCap, field.height / 0.1f * t.heightDefense));

        private Side Of(bool attacker) => attacker ? a : d;

        // ===== THE MEASURE =====

        /// <summary>Visualization: opens the next Measure, deals the hands, and every performed side composes and commits its Score.</summary>
        public void BeginMeasure()
        {
            if (Over || open) return;
            m++;
            open = true; scoresCommitted = false;
            pendingOverbeat = false; overbeatSteps.Clear(); overbeatReactions.Clear();
            ringsA = ringsD = 0;
            Array.Clear(restA, 0, restA.Length); Array.Clear(restD, 0, restD.Length);
            Enter(BattlePhase.Visualization);
            emitted.Clear(); panicked.Clear();
            composureAtOpening.Clear(); foreach (var unit in a.side.Standing.Concat(d.side.Standing)) composureAtOpening[unit] = unit.composure;
            Muster(a);
            Muster(d);
            hits = new Dictionary<CombatSection, Hit>();
            RefreshSkirmishes();
            steadied.Clear();
            MaladaptiveMeasure(a); MaladaptiveMeasure(d);
            foreach (var track in tracks.Values)
            {
                track.steps.Clear(); track.stepBeats.Clear(); track.spotlit = false;
                track.feverExtra = false; track.fever = TempoPercentage(track.side.attacker) > 0;
                track.detuned = track.attuned = track.duet = track.overrode = false;
                track.charging = chords.Values.Any(c => c.Live && c.owner == track && c.core.card.chord?.advance == true);
                track.rushing = chords.Values.Any(c => c.Live && c.owner == track && Has(c.core.card, CardOp.Rush));
            }
            foreach (var s in new[] { a, d }) if (s.perf != null) DealHand(s);
            SyncCrises();
            // The performed sides commit first: their Confirmed intent is what the player visualizes.
            foreach (var s in new[] { a, d }.Where(Automatic))
            {
                planningSide = s;
                try
                {
                    foreach (var track in TracksOf(s).ToList()) { if (!track.pathComposed || track.path.Count == 0) { track.pathComposed = false; PlanPath(track); } else ScheduleSteps(track); }
                    Plan(s);
                }
                finally { planningSide = null; }
            }
            foreach (var s in new[] { a, d }.Where(x => !Automatic(x)))
                foreach (var track in TracksOf(s).ToList()) { if (track.pathComposed && track.path.Count > 0) ScheduleSteps(track); else track.pathComposed = false; }
            foreach (var s in new[] { a, d }.Where(Automatic))
            {
                planningSide = s;
                try { foreach (var track in TracksOf(s).ToList()) if (MajorOf(track) == null && !chords.Values.Any(c => c.Live && c.owner == track && c.drilled)) ComposeDrill(track); }
                finally { planningSide = null; }
            }
            Enter(BattlePhase.Composition);
        }

        /// <summary>Commits the open Measure (opening it first if needed) and performs all four Beats, the Toll and the Assessment.</summary>
        public void ResolveMeasure()
        {
            if (rhythm != null || pendingCadence != null) return;
            if (Over) return;
            if (!open) BeginMeasure();
            if (Over) return;
            Commit();
            while (open && !Over && pendingCadence == null) PerformBeat();
        }

        private void CloseMeasure()
        {
            Enter(BattlePhase.Toll);
            foreach (var unit in a.side.Standing.Concat(d.side.Standing).Where(x => x.deathKnell).ToList())
            {
                var mark = Mark(unit);
                if (mark.burnLeft > 0 && mark.burn > 0f || field.fallout > 0f && unit.PureLight)
                    DamageSurvival(unit, mark.burnLeft > 0 ? mark.burn : field.fallout * t.falloutIntegrity, 0f);
            }
            Toll(a, m, field, t);
            Toll(d, m, field, t);
            MaladaptiveToll(a); MaladaptiveToll(d);
            AssessComposureShock();
            FlushReactionsAndHits();
            Enter(BattlePhase.Assessment);
            foreach (bool attacker in new[] { true, false }) { tempo[attacker].Assess(report.rhythm, attacker, m); AssessSympathy(Of(attacker)); }
            SkirmishVerdicts();
            Pressure();
            PumpCascade();
            AttachmentFalls(); FlushReactionsAndHits();
            report.timeline.Add(new BattleMeasure { measure = m, beat = beat, attacker = Bars(a), defender = Bars(d), positions = Positions() });
            EndMeasure();
            open = false;

            bool aDone = Done(a, t, out aLeft), dDone = Done(d, t, out dLeft);
            if (aDone && dDone)
            {
                // Both give out together: the one with more of its line left holds.
                if (LineShare(a) > LineShare(d)) aDone = false; else dDone = false;
            }
            if (aDone) { end = -1; Beaten(a, aLeft, report.attacker, m, t, report); Over = true; }
            else if (dDone) { end = 1; Beaten(d, dLeft, report.defender, m, t, report); Over = true; }
            else if (m >= t.maxMeasures) Over = true;
        }

        private void ApplyHits()
        {
            foreach (var pair in hits.ToList())
            {
                var owner = ownerOf[pair.Key];
                var (integrity, composure) = Harm(pair.Key, pair.Value, owner);
                Resolve(pair.Key, integrity, composure, pair.Value.friendlyFire, hitActors.TryGetValue(pair.Key, out var actor) ? actor : actingVoice);
            }
            hits.Clear();
            hitActors.Clear();
        }

        /// <summary>What a hit would do to a section (its parries, armor and exposure counted), without applying it.</summary>
        private (float integrity, float composure) Harm(CombatSection sec, Hit hit, Side owner)
        {
            float integrity = hit.integrity + Math.Max(0f, hit.direct);
            var (struck, fear) = Parry(sec, hit, owner, defenderDefense, t);
            integrity = (integrity + struck) * (1f + owner.ExposeOf(sec));
            float composure = (hit.composure + fear) / (owner.stack.nerve * owner.Of(sec).nerve);
            return (integrity, composure);
        }

        private void Resolve(CombatSection sec, float integrity, float composure, bool friendlyFire, CombatSection actor)
        {
            composure = ComposureLoss(sec, composure);
            var owner = ownerOf[sec];
            DamageSurvival(sec, integrity, composure);
            float before = Math.Max(0f, sec.integrity);
            float loss = Math.Min(before, Math.Max(0f, integrity));
            sec.integrity -= Math.Max(0f, integrity);
            if (sec.eliteRole != BattleEliteRole.None && before >= sec.maxIntegrity * .5f && sec.integrity < sec.maxIntegrity * .5f)
                Pollute(owner.attacker, owner.side.sections.IndexOf(sec), BattlePollution.Wound);
            EnterDeathKnell(sec);
            sec.lost += loss;
            sec.composure -= Math.Max(0f, composure);
            if (loss > 0f || composure > 0f) QueueReaction(BattleReactionTrigger.Damaged, owner, actor ?? actingVoice, sec);
            if (owner.Conducted) owner.bar -= Math.Max(0f, composure) * t.conductorExposure;
            if (composure >= sec.maxComposure * SpatialRules.Tuning.severeShockShare) Emit(owner, sec, BattleSpatialCause.SevereComposureShock, "Incoming harm severely shocked Composure.");
            if (friendlyFire && (loss > 0f || composure > 0f)) Emit(owner, sec, BattleSpatialCause.FriendlyFire, "A friendly attack struck this combatant.");
            float mending = owner.SupportOf(sec, x => x.mending);
            if (mending > 0f && sec.integrity > 0f)
            {
                float mended = Math.Min(loss, Math.Min(sec.maxIntegrity - sec.integrity, loss * mending));
                sec.integrity += mended;
                sec.lost -= mended;
            }
            SkirmishLoss(sec, loss);
            Interruption(sec, loss);
        }

        /// <summary>A drilled Note's release: steel on its declared target (Rank Proximity's nearest if it moved), or the caster's chord.</summary>
        private void ResolveDrill(Side s, CombatSection source, CombatSection target, int seed, bool spell, SpellBinding root, List<SpellBinding> harmony, bool collidedNow)
        {
            var random = new CombatRandom(seed);
            var origin = Origin(s, source);
            if (origin == null) return;
            var reach = Reachable(s, source, spell);
            if (target == null || !reach.Contains(target))
                target = reach.OrderBy(x => SpatialRules.Distance(origin.battleHex, x.battleHex)).ThenBy(x => s.enemy.side.sections.IndexOf(x)).FirstOrDefault();
            if (target == null) return;
            if (spell)
            {
                var c = s.conductor;
                Cast(s, source, source?.name ?? c.name, source == null ? t.conductorPotency * c.Score(root) / 21f : source.potency * source.IntegrityShare,
                    new List<SpellBinding> { root }, harmony ?? new List<SpellBinding>(), source?.soulWeaver ?? true, source?.primary ?? c.leitmotif, source == null || source.row == FormationRow.Back,
                    source?.dread ?? 1f, new List<CombatSection> { target }, true, m, field, t, random, hits, report);
            }
            else if (source != null && source.row != FormationRow.Support && source.attack > 0f)
                Steel(s, m, field, ground, s.attacker ? assault : 1f, t, random, hits, source, target, collided: collidedNow);
        }

        /// <summary>Plays out the rest of the battle (the performer plays for every side from here) and returns the report.</summary>
        public BattleReport Finish()
        {
            if (requiresManual && !Over) throw new InvalidOperationException("Perform this required manual encounter before finalizing its aftermath.");
            if (finished) return report;
            // Missing input is a Missed performance, including when a UI closes during a phrase.
            if (rhythm != null) CompleteRhythm(rhythm.end + .25);
            if (pendingCadence != null) { BeginAbjuration(0); CompleteRhythm(rhythm.end + .25); }
            finishing = true;
            if (open && !scoresCommitted)
                foreach (var s in new[] { a, d }.Where(x => x.side.manual))
                {
                    planningSide = s;
                    try
                    {
                        foreach (var track in TracksOf(s).ToList()) if (!track.pathComposed) PlanPath(track);
                        Plan(s);
                    }
                    finally { planningSide = null; }
                }
            while (!Over)
            {
                BeginMeasure();
                ResolveMeasure();
            }
            finished = true;
            report.measures = Math.Min(m, Math.Max(0, t.maxMeasures));
            if (end == 0) report.log.Add($"After {t.maxMeasures} measures neither line has given way: {d.side.name} hold the field and {a.side.name} draw off.");

            report.winner = end;
            Tally(a, report.attacker, t, lost: end <= 0);
            Tally(d, report.defender, t, lost: end > 0);
            Fates(a, report, won: end > 0, doomed: end < 0 && !aLeft, t);
            Fates(d, report, won: end < 0, doomed: end > 0 && !dLeft, t);
            PopulationFates();
            RecordDeckEvolution();
            report.attacker.lossBurden = BattleLossBurden.Assess(a.side, report.attacker);
            report.defender.lossBurden = BattleLossBurden.Assess(d.side, report.defender);
            if (end > 0) { report.attacker.outcome = BattleLossBurden.Victory(a.side, report.attacker.lossBurden, report.defender.lossBurden); report.defender.outcome = BattleVerdicts.Mirror(report.attacker.outcome); }
            else { report.defender.outcome = BattleLossBurden.Victory(d.side, report.defender.lossBurden, report.attacker.lossBurden); report.attacker.outcome = BattleVerdicts.Mirror(report.defender.outcome); }
            report.outcome = report.attacker.outcome;
            // The Legendary Victory: won by hand against the forecast (the micro layer's alone).
            foreach (bool attacker in new[] { true, false })
            {
                var s = Of(attacker);
                bool won = attacker ? end > 0 : end <= 0;
                if (!s.side.manual || Prediction == null) continue;
                if (!BattleLossBurden.Mythical(won, !auto, BattleVerdicts.WinChance(Prediction, attacker))) continue;
                (attacker ? report.attacker : report.defender).mythical = true;
                report.legendaryFor = attacker;
                report.log.Add($"Against a Doomed forecast ({BattleVerdicts.WinChance(Prediction, attacker):P0} to win), {s.side.name} carry the day: Mythical {BattleVerdicts.Words((attacker ? report.attacker : report.defender).outcome)}.");
            }
            TallySymphony(a, report.attacker);
            TallySymphony(d, report.defender);
            foreach (var fate in report.legends)
                if ((fate.attacker ? report.attacker : report.defender).mythical) fate.deed = "Mythical " + BattleVerdicts.Words((fate.attacker ? report.attacker : report.defender).outcome) + "; " + fate.deed;
            // Performances Remembered: a Resolution is the thing these particular people did together.
            foreach (var record in report.resolutions)
                foreach (var fate in report.legends.Where(f => f.attacker == record.attacker && record.contributors.Contains(f.name)))
                    fate.deed = (string.IsNullOrEmpty(fate.deed) ? string.Empty : fate.deed + "; ") + "Performed a " + record.Words;
            report.log.Add($"{BattleReport.Words(report.outcome)} for {a.side.name}. {Losses(report.attacker)}; {Losses(report.defender)}.");
            return report;
        }

        // ===== THE PLAYER'S HAND (a manual side) =====

        /// <summary>The cards in a side's hand this measure (empty with no Symphony, or before <see cref="BeginMeasure"/>).</summary>
        public IReadOnlyList<DeckCard> Hand(bool attacker)
        {
            var p = Of(attacker).perf;
            return p == null ? (IReadOnlyList<DeckCard>)Array.Empty<DeckCard>() : p.hand.Select(i => p.deck[i]).ToList();
        }

        /// <summary>Why a card in the hand cannot be composed now, or null.</summary>
        public string WhyNotPlay(bool attacker, int handIndex) => WhyNotCompose(Of(attacker)) ?? WhyNotHand(attacker, handIndex, false);

        private string WhyNotHand(bool attacker, int handIndex, bool composing)
        {
            var s = Of(attacker);
            if (Over) return "The battle is over.";
            if (!open) return "No measure is open.";
            if (s.perf == null) return "This side has no Symphony.";
            if (handIndex < 0 || handIndex >= s.perf.hand.Count) return "No such card in the hand.";
            var dc = s.perf.deck[s.perf.hand[handIndex]];
            if (dc.card.noteRole == BattleNoteRole.Minor) return "A structural Minor must be attached to a Core.";
            if (!composing && (dc.card.requiredVoices > 1 || dc.card.requiredBond > 0f)) return "This Core requires its composed duet or ensemble.";
            return WhyNot(s, dc);
        }

        /// <summary>Lets the performer compose the rest of a side's Score this Measure.</summary>
        public void AutoPlay(bool attacker)
        {
            var s = Of(attacker);
            if (!open || scoresCommitted) return;
            planningSide = s;
            try
            {
                foreach (var track in TracksOf(s).ToList()) if (!track.pathComposed) PlanPath(track);
                if (s.perf != null) Plan(s);
            }
            finally { planningSide = null; }
        }

        // ===== THE SYMPHONY =====

        private void StartSymphony(Side s, int seed)
        {
            if (!s.side.HasSymphony && !s.side.sections.Any(u => u.leader?.deckEvolution?.pollution.Count > 0)) return;
            var p = new Performance { deck = (s.side.deck ?? new List<DeckCard>()).ToList(), rng = new CombatRandom(unchecked(seed * 7 + (s.attacker ? 0x51F15E : 0x2C1B3C6D))) };
            for (int i = 0; i < p.deck.Count; i++)
                if (p.deck[i].legend?.deckEvolution?.alterations.Any(a => a.card == p.deck[i].card.id) == true)
                { var card = Frozen(p.deck[i]); card.card = card.legend.deckEvolution.Alter(card.card); p.deck[i] = card; }
            for (int voice = 0; voice < s.side.sections.Count; voice++)
                foreach (var kind in s.side.sections[voice].leader?.deckEvolution?.pollution ?? new List<BattlePollution>())
                    p.deck.Add(new DeckCard { voice = voice, legend = s.side.sections[voice].leader, card = BattlePollutionCards.Of(kind), source = "Expedition Pollution" });
            for (int i = 0; i < p.deck.Count; i++) if (p.deck[i]?.card != null) p.draw.Add(i);
            Shuffle(p.draw, p.rng);
            s.perf = p;
            int voices = p.deck.Where(x => x?.card != null).Select(x => x.voice).Distinct().Count();
            report.log.Add($"{s.side.name} perform a Symphony of {SymphonyDecks.Describe(p.deck)} ({voices} voice{(voices == 1 ? "" : "s")}, power {(s.attacker ? report.attacker : report.defender).symphonyPower:0}).");
        }

        private void EndMeasure()
        {
            foreach (var s in new[] { a, d })
            {
                s.surge = s.crescendo = 0f;
                s.sure = false;
                var p = s.perf;
                if (p == null) continue;
                foreach (int index in p.hand)
                { PollutionDiscard(s, index); if (p.deck[index].card.corrupted) p.exhausted.Add(index); else p.discard.Add(index); }
                p.hand.Clear();
                p.setups = 0;
                // A primed Reaction lasts its Measure; an unfired one returns to the discard pile.
                foreach (var r in reactions.Where(r => r.attacker == s.attacker && r.measure == m && !r.fired && r.preparation.index >= 0))
                    if (r.preparation.frozen.card.exhaust) p.exhausted.Add(r.preparation.index); else if (!p.discard.Contains(r.preparation.index)) p.discard.Add(r.preparation.index);
            }
            reactions.RemoveAll(r => r.measure <= m);
            pollutionDrawn.Clear();
            foreach (var k in marks.Values)
            {
                k.wards.RemoveAll(x => x.expires <= beat);
                if (k.exposeLeft > 0 && --k.exposeLeft == 0) k.expose = 0f;
                if (k.blindLeft > 0 && --k.blindLeft == 0) k.blind = 0f;
                if (k.burnLeft > 0 && --k.burnLeft == 0) k.burn = 0f;
            }
            foreach (var id in chords.Where(x => !x.Value.Live).Select(x => x.Key).ToList()) chords.Remove(id);
            foreach (var track in tracks.Values)
            {
                track.steps.Clear(); track.stepBeats.Clear();
                if (!track.pathComposed) track.path.Clear();
                else if (track.path.Count == 0) track.pathComposed = false;
            }
        }

        private void TallySymphony(Side s, SideResult r)
        {
            var p = s.perf;
            if (p == null) return;
            r.cardsPlayed = p.played;
            r.cardFlickers = p.flickers;
            r.signatureCard = p.tally.Count == 0 ? null : p.tally.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key;
        }
    }
}
