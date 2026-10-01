using System;
using System.Collections.Generic;
using System.Linq;

// Reactions and Cascade Chains (vault: Combat System.md): primed during Composition, they occupy no Beats and fire the
// instant their condition is met, once per Measure. A Reaction can trigger another: a chain runs four links (five for a
// Flux-attuned reactor), and no unit reacts twice within the same chain.
public static partial class BattleResolver
{
    private sealed class ArmedReaction
    {
        public long id;
        public bool attacker, fired;
        public Queued preparation;
        public Track track;
        public int measure;
    }

    private sealed class ReactionSignal
    {
        public BattleReactionTrigger trigger;
        public bool attacker;
        public CombatSection actor, target;
        public long action;
        public int depth;
        public HashSet<CombatSection> chain = new HashSet<CombatSection>();
    }

    public sealed partial class BattleRun
    {
        private readonly List<ArmedReaction> reactions = new List<ArmedReaction>();
        private readonly Queue<ReactionSignal> reactionSignals = new Queue<ReactionSignal>();
        private readonly HashSet<long> reactionsFired = new HashSet<long>();
        private readonly HashSet<string> emergencyBonds = new HashSet<string>(), witnessedFalls = new HashSet<string>();
        private long nextReaction = 1;
        private int reactionsThisBeat;
        private CombatSection actingVoice;
        private CombatSection reactionAttacker;
        private ReactionSignal reacting;
        private readonly Dictionary<CombatSection, CombatSection> hitActors = new Dictionary<CombatSection, CombatSection>();
        private long actingAction;

        public IReadOnlyList<BattleReactionView> PreparedReactions => reactions.Where(r => r.measure == m && !r.fired).Select(r => new BattleReactionView
        { id = r.id, attacker = r.attacker, voice = r.preparation.frozen.voice, card = r.preparation.frozen.Name,
            remainingUses = 1, expires = Cadence, rule = r.preparation.frozen.card.reaction.Clone() }).ToList();

        private void QueueReaction(BattleReactionTrigger trigger, Side s, CombatSection actor, CombatSection target, long action = 0)
        {
            var signal = new ReactionSignal { trigger = trigger, attacker = s.attacker, actor = actor, target = target, action = action == 0 ? actingAction : action };
            if (reacting != null) { signal.depth = reacting.depth + 1; signal.chain.UnionWith(reacting.chain); }
            reactionSignals.Enqueue(signal);
        }

        private int Links(CombatSection reactor) => mt.cascadeLinks + (reactor?.primary == SpellBinding.Flux ? mt.fluxCascadeLinks : 0);

        private void Reposition(Side s, DeckCard dc, CardEffect effect, CombatSection wanted)
        {
            foreach (var target in EffectAllies(s, dc, effect, wanted).ToList())
            {
                int to = effect.destinationHex >= 0 ? effect.destinationHex : SpatialRules.Retreats(s.attacker, target).DefaultIfEmpty(-1).First();
                if (to >= 0 && SpatialRules.Passable(to) && BattleHexLayout.AreAdjacent(target.battleHex, to))
                {
                    foreach (var sk in skirmishes.Where(x => x.a.Contains(target) || x.d.Contains(target))) { sk.a.Remove(target); sk.d.Remove(target); }
                    Relocate(s, target, to, BattleSpatialCause.Movement, "An authored utility or reaction repositioned this ally.");
                    var track = TrackOf(target); if (track != null) { track.steps.Clear(); track.path.Clear(); }
                    Contacts(new List<Track>());
                }
                else Emit(s, target, BattleSpatialCause.PreparationFailed, "The promised reposition has no legal destination.");
            }
        }

        private void EmergencyBond(ReactionSignal signal)
        {
            if (signal.trigger != BattleReactionTrigger.Incoming || signal.target == null) return;
            var s = Of(signal.attacker);
            foreach (var partner in s.side.Standing.Where(x => x != signal.target && !x.mindBroken && SpatialRules.Support(x, signal.target)).ToList())
            {
                float strength = Bond(s, partner, signal.target);
                string key = s.attacker + ":" + s.side.sections.IndexOf(partner) + ":" + s.side.sections.IndexOf(signal.target);
                if (strength < ct.bondThreshold || !emergencyBonds.Add(key)) continue;
                Mark(signal.target).wards.Add(new GuardLink { source = partner, range = SpatialRules.Tuning.supportReach, amount = ct.emergencyGuard * strength,
                    raised = beat, expires = beat, physical = true, magical = true, binding = partner.primary, rendition = 1f });
                ChordEvent(s, signal.action, BattleChordCause.Reaction, $"{partner.name} throws themself before their bonded partner {signal.target.name}.");
                QueueReaction(BattleReactionTrigger.Guarded, s, signal.actor, signal.target, signal.action);
            }
        }

        /// <summary>The Incoming signals of the releases being performed on this Beat (before their Impact).</summary>
        private void IncomingSignals(HashSet<CombatSection> notified)
        {
            var pending = (releasing ?? new List<Release>()).SelectMany(r => r.hits.Where(h => r.side != ownerOf[h.Key]).Select(h => (r, h.Key)))
                .Concat(hits.Where(h => !h.Value.friendlyFire && (h.Value.integrity > 0f || h.Value.direct > 0f || h.Value.attacks > 0f || h.Value.composure > 0f)).Select(h => ((Release)null, h.Key)));
            foreach (var (r, target) in pending.ToList())
                if (notified.Add(target))
                {
                    var actor = r?.chord != null ? Performer(r.chord.owner) : hitActors.TryGetValue(target, out var known) ? known : actingVoice;
                    reactionSignals.Enqueue(new ReactionSignal { trigger = BattleReactionTrigger.Incoming, attacker = ownerOf[target].attacker, actor = actor, target = target,
                        action = r?.chord?.id ?? actingAction });
                }
        }

        private void ProcessReactions()
        {
            var notified = new HashSet<CombatSection>();
            while (true)
            {
                IncomingSignals(notified);
                if (reactionSignals.Count == 0) break;
                var signal = reactionSignals.Dequeue();
                EmergencyBond(signal);
                foreach (var armed in reactions.Where(r => r.measure == m && !r.fired && r.attacker == (r.preparation.frozen.card.reaction.enemyTrigger ? !signal.attacker : signal.attacker) &&
                    r.preparation.frozen.card.reaction.trigger == signal.trigger).OrderBy(r => r.id).ToList())
                {
                    var s = Of(armed.attacker);
                    var original = armed.preparation; var source = Performer(armed.track); var rule = original.frozen.card.reaction;
                    if (source == null || !source.Standing || source.mindBroken || signal.target == null) continue;
                    if (signal.chain.Contains(source)) continue;
                    if (signal.depth >= Links(source))
                    {
                        ChordEvent(s, signal.action, BattleChordCause.CascadeLimit, $"The Cascade Chain reached its {Links(source)} links; {original.frozen.Name} stays primed.");
                        continue;
                    }
                    if (SpatialRules.Distance(source.battleHex, signal.target.battleHex) > Math.Max(rule.range, rule.trigger == BattleReactionTrigger.EnemyEntersLane ? BattleHexLayout.HexCount : 0)) continue;
                    if (rule.bondOnly && Bond(s, source, signal.target) < ct.bondThreshold) continue;
                    var aim = original.frozen.card.effects.FirstOrDefault()?.aim ?? CardAim.Self;
                    var target = AimsAtEnemy(original.frozen.card) ? rule.enemyTrigger ? signal.target : signal.actor : aim == CardAim.Self || aim == CardAim.Allies ? source : signal.target;
                    if (original.target != null && original.target != target) continue;
                    if (rule.intercept)
                    {
                        if (signal.target == null || !BattleHexLayout.AreAdjacent(source.battleHex, signal.target.battleHex) || SkirmishOf(source) != null) continue;
                    }
                    else if (target == null || TargetFailure(s, original.frozen, target) != null) continue;
                    if (WhyNot(s, original.frozen) != null || original.frozen.card.requiresPosition && source.battleHex != original.origin) continue;
                    armed.fired = true; reactionsFired.Add(armed.id);
                    var q = new Queued { frozen = original.frozen, target = target, origin = original.origin, rendition = original.rendition, stanceHeld = original.stanceHeld,
                        seed = original.seed, index = original.index, action = signal.action, reacting = true };
                    var context = new ReactionSignal { trigger = signal.trigger, attacker = signal.attacker, depth = signal.depth, action = signal.action };
                    context.chain.UnionWith(signal.chain); context.chain.Add(source);
                    var priorReacting = reacting; reacting = context;
                    var priorAttacker = reactionAttacker; reactionAttacker = signal.actor;
                    var priorRelease = release; var priorHits = hits;
                    Release ctx = null;
                    if (releasing != null)
                    {
                        // A counter answering a release lands with it, in the same simultaneous Impact.
                        ctx = new Release { side = s, root = RootOf(original.frozen, source), syncopated = true, reaction = true,
                            magical = Magic(original.frozen.card), physical = !Magic(original.frozen.card) };
                        release = ctx; hits = ctx.hits;
                    }
                    var beforeHits = new HashSet<CombatSection>(hits.Keys);
                    Enter(BattlePhase.Reaction, signal.action);
                    ChordEvent(s, signal.action, BattleChordCause.Reaction, $"{q.frozen.Name} triggered by {signal.trigger} (link {signal.depth + 1}).");
                    try
                    {
                        if (rule.intercept)
                        {
                            foreach (var sk in skirmishes.Where(x => x.a.Contains(source) || x.d.Contains(source))) { sk.a.Remove(source); sk.d.Remove(source); }
                            Relocate(s, source, signal.target.battleHex, BattleSpatialCause.Movement, "Intercepted the enemy entering an adjacent hex.");
                            var own = TrackOf(source); if (own != null) { own.steps.Clear(); own.path.Clear(); }
                            Contacts(new List<Track> { TrackOf(signal.target) }.Where(x => x != null).ToList());
                        }
                        var previous = s.perf.rng; s.perf.rng = new CombatRandom(original.seed);
                        try { Execute(s, q); } finally { s.perf.rng = previous; }
                    }
                    finally { release = priorRelease; hits = priorHits; reacting = priorReacting; reactionAttacker = priorAttacker; }
                    if (ctx != null && (ctx.hits.Count > 0 || ctx.deferred.Count > 0 || ctx.pushes.Count > 0)) releasing.Add(ctx);
                    foreach (var victim in (ctx?.hits ?? hits).Keys.Where(x => !beforeHits.Contains(x))) hitActors[victim] = source;
                    if (s.perf != null && original.index >= 0 && !s.perf.discard.Contains(original.index))
                    { if (original.frozen.card.exhaust) s.perf.exhausted.Add(original.index); else s.perf.discard.Add(original.index); }
                }
            }
        }

        /// <summary>Movement is a trigger: Intercept answers an enemy entering an adjacent hex, Overwatch the first enemy entering a watched lane.</summary>
        private void MovementReactions(Track mover, int from, int to)
        {
            var s = mover.side; var unit = mover.unit;
            foreach (var enemy in s.enemy.side.Standing.Where(x => x.battleHex >= 0).ToList())
            {
                if (BattleHexLayout.AreAdjacent(enemy.battleHex, to) && !BattleHexLayout.AreAdjacent(enemy.battleHex, from) || enemy.battleHex == to && from != to)
                    reactionSignals.Enqueue(new ReactionSignal { trigger = BattleReactionTrigger.EnemyEntersAdjacent, attacker = !s.attacker, actor = unit, target = unit, action = actingAction });
                if (BattleHexLayout.At(to).Lane == BattleHexLayout.At(enemy.battleHex).Lane && BattleHexLayout.At(from).Lane != BattleHexLayout.At(to).Lane)
                    reactionSignals.Enqueue(new ReactionSignal { trigger = BattleReactionTrigger.EnemyEntersLane, attacker = !s.attacker, actor = unit, target = unit, action = actingAction });
            }
        }

        private void AttachmentFalls()
        {
            foreach (var s in new[] { a, d })
                foreach (var fallen in s.side.sections.Where(x => !x.Standing).ToList())
                {
                    string key = s.attacker + ":" + s.side.sections.IndexOf(fallen);
                    if (!witnessedFalls.Add(key)) continue;
                    QueueReaction(BattleReactionTrigger.AllyFell, s, fallen, fallen);
                    foreach (var ally in s.side.Standing.ToList())
                    {
                        float bond = Bond(s, fallen, ally);
                        float company = fallen.leader == null || ally.bonds == null || !ally.bonds.TryGetValue(fallen.leader.name, out int stars) ? 0f : Math.Min(1f, stars / 3f);
                        float strength = Math.Max(bond, company); if (strength <= 0f) continue;
                        float shock = ally.maxComposure * ct.attachmentShock * strength;
                        Shock(s, ally, shock);
                        if (ally.leader == s.conductor && s.Conducted) s.bar -= s.barMax * ct.attachmentShock * strength;
                        ChordEvent(s, actingAction, BattleChordCause.AttachmentShock, $"{ally.name} witnessed an attached combatant fall.", composure: shock);
                    }
                    if (s.Conducted && fallen.bonds != null && fallen.bonds.TryGetValue(s.conductor.name, out int attachment))
                    {
                        float shock = s.barMax * ct.attachmentShock * Math.Min(1f, attachment / 3f); s.bar -= shock;
                        ChordEvent(s, actingAction, BattleChordCause.AttachmentShock, "The Conductor witnessed the fall of an attached company.", composure: shock);
                    }
                }
        }

        private void FlushReactionsAndHits()
        {
            ProcessReactions();
            int guard = 0;
            while (hits.Count > 0 && guard++ < 64)
            {
                ApplyHits(); PumpCascade(); AttachmentFalls();
                ProcessReactions();
            }
            PumpCascade();
        }
    }
}
