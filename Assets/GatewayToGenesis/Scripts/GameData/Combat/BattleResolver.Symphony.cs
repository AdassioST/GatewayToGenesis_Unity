using System;
using System.Collections.Generic;
using System.Linq;

// The Symphony of War's melody: each side's deck performed Measure by Measure (vault: Combat System.md, "The attacks
// are dictated by Symphony Cards"). A Measure deals a hand; each card is written onto a Track of the four-Beat Score and
// is voiced by one of the side's sections (or its Battle Conductor), scaling with it. Spell cards go through the same
// casting as the drilled spells (the Elemental Harmonic Circle, Signal Loss, the Age's command of chords, the Loom and
// the land). Several voices join one working only as an explicit Ensemble (Minor Notes on their own Tracks).
// Cards at home on the field (their ground, their condition) land harder, and some are only playable there.
public static partial class BattleResolver
{
    /// <summary>Wards standing on a section and the remaining Measure-based statuses.</summary>
    private sealed class CardMarks
    {
        public float expose, blind, burn;
        public readonly List<GuardLink> wards = new List<GuardLink>();
        public int exposeLeft, blindLeft, burnLeft;
    }

    /// <summary>
    /// A Ward (an Abjuration Art): Strength in Integrity points over a scope, for a set of frequencies (physical steel,
    /// magic, or both), standing from the Beat it is raised and growing with every Beat held before the Cadence.
    /// </summary>
    private sealed class GuardLink
    {
        public CombatSection source;
        public float amount, rendition = 1f;
        public int range, raised, expires;
        public bool physical, magical;
        public SpellBinding binding;
        public string card;
        public Chord chord;
    }

    /// <summary>A card being performed: its frozen content, target, rendition and random seed.</summary>
    private sealed class Queued
    {
        public int index;
        public CombatSection target;
        public float rendition = 1f;
        public int origin;
        public bool stanceHeld;
        public DeckCard frozen;
        public int seed;
        public long action;
        public bool explicitChord, reacting;
        /// <summary>The Chord it belongs to, and the Ward chord it raises (wards remember their working).</summary>
        public Chord chord, ward;
    }

    /// <summary>A side's draw state; no energy pool is present.</summary>
    private sealed class Performance
    {
        public List<DeckCard> deck;
        public readonly List<int> draw = new List<int>(), discard = new List<int>(), hand = new List<int>();
        public readonly HashSet<int> exhausted = new HashSet<int>();
        public CombatRandom rng;
        public int handCapacity, handSize, bonusDraw;
        /// <summary>Each card is a whole Note of its Track (1); kept as a field for the gauge and older callers.</summary>
        public float cardScale = 1f;
        public int played, flickers, logged;
        /// <summary>Setup cards performed this measure (groundwork for the Offensive cards after them), and composed by the performer.</summary>
        public int setups, plannedSetups;
        public readonly Dictionary<string, int> tally = new Dictionary<string, int>();
        /// <summary>What the performer has already composed this measure (so a second guard on the same section is worth less).</summary>
        public readonly Dictionary<CombatSection, float> plannedGuard = new Dictionary<CombatSection, float>(), plannedMend = new Dictionary<CombatSection, float>(),
            plannedRally = new Dictionary<CombatSection, float>(), plannedExpose = new Dictionary<CombatSection, float>();
        public readonly HashSet<CombatSection> plannedPush = new HashSet<CombatSection>();
        public float plannedSurge, plannedCrescendo;
        public bool plannedSure;
    }

    private static void Shuffle(List<int> list, CombatRandom rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Math.Min(i, (int)(rng.NextFloat() * (i + 1)));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static bool Has(CombatCard card, CardOp op) => card.effects.Any(e => e != null && e.op == op);

    /// <summary>A card that aims at the enemy (its first aimed effect does).</summary>
    private static bool AimsAtEnemy(CombatCard card)
    {
        var first = card.effects.FirstOrDefault(e => e != null && e.aim != CardAim.Self && e.aim != CardAim.Allies);
        return first == null || first.aim == CardAim.Enemy || first.aim == CardAim.EnemyLine;
    }

    /// <summary>A card that is magic: a spell, or a creature's instinct that casts.</summary>
        private static bool Magic(CombatCard card) => card.weaving || card.kind == CardKind.Spell || card.kind == CardKind.Instinct && Has(card, CardOp.Spell);

    public sealed partial class BattleRun
    {
        // ===== DEALING =====

        private int HandCapacity(Side s, out int voices)
        {
            var p = s.perf;
            voices = p.deck.Select((x, i) => (x, i)).Where(x => x.x?.card != null && !p.exhausted.Contains(x.i) && VoiceAlive(s, x.x)).Select(x => x.x.voice).Distinct().Count();
            int capacity = (int)Math.Floor(st.beatsBase + st.beatsPerVoice * voices + 0.5f);
            capacity = Math.Max(st.minBeats, Math.Min(st.maxBeats, capacity));
            if (s.Conducted && s.conductor.Stars(LegendClass.Sovereign) >= 2) capacity += st.sovereignBeat;
            return capacity;
        }

        private void DealHand(Side s)
        {
            var p = s.perf;
            p.handCapacity = HandCapacity(s, out _);
            // A card is a whole Note of its Track, so it carries its full value (no melody normalization).
            p.cardScale = 1f;
            int size = p.handCapacity + st.handExtra + (s.Conducted && s.conductor.Stars(LegendClass.Seer) > 0 ? st.seerDraw : 0);
            p.handSize = size;
            Draw(p, size + p.bonusDraw);
            PollutionDraw(s);
            p.bonusDraw = 0;
            p.plannedGuard.Clear(); p.plannedMend.Clear(); p.plannedRally.Clear(); p.plannedExpose.Clear(); p.plannedPush.Clear();
            p.plannedSurge = p.plannedCrescendo = 0f;
            p.plannedSure = false;
            p.plannedSetups = 0;
        }

        private static void Draw(Performance p, int n)
        {
            for (int k = 0; k < n; k++)
            {
                if (p.draw.Count == 0)
                {
                    if (p.discard.Count == 0) return;
                    p.draw.AddRange(p.discard);
                    p.discard.Clear();
                    Shuffle(p.draw, p.rng);
                }
                int last = p.draw.Count - 1;
                p.hand.Add(p.draw[last]);
                p.draw.RemoveAt(last);
            }
        }

        // ===== WHO CAN PLAY WHAT =====

        private static CombatSection VoiceOf(Side s, DeckCard dc) => dc.voice >= 0 && dc.voice < s.side.sections.Count ? s.side.sections[dc.voice] : null;

        private static bool VoiceAlive(Side s, DeckCard dc) => dc.voice < 0 ? s.Conducted || dc.card.corrupted && s.conductor != null : VoiceOf(s, dc)?.Standing == true;

        private static string VoiceName(Side s, DeckCard dc) => dc.voice < 0 ? s.conductor?.name ?? s.side.name : VoiceOf(s, dc)?.name ?? s.side.name;

        /// <summary>Why a card cannot be performed now (null: it can): its voice is gone, waits in reserve, is Mind Broken, or the field is wrong for it.</summary>
        private string WhyNot(Side s, DeckCard dc)
        {
            var card = dc.card;
            if (card.pollution != BattlePollution.None) return "A Status Pollution card needs a purge; it cannot be composed as a clean Note.";
            if (card.corrupted || card.cathartic)
            {
                var owner = TrackOf(s, dc);
                if (owner == null || !crises.TryGetValue(owner, out var crisis) || (card.corrupted ? !crisis.active : !crisis.cadenzaReady))
                    return "This crisis working is not available to its owner.";
            }
            if (!VoiceAlive(s, dc)) return dc.voice < 0 ? "Its commander cannot give orders." : $"{VoiceName(s, dc)} is no longer on the field.";
            if (card.voice == CardVoice.Commander && !s.Conducted) return "Only a commander gives this order.";
            var sec = VoiceOf(s, dc);
            if (sec != null)
            {
                if (sec.deathKnell && (card.ChannelBeats > 1 || Has(card, CardOp.Rush) || card.hyperMeasures > 1)) return "Death Knell cannot sustain this working or Rush.";
                if (StatusInHand(s, sec, BattlePollution.MindControl) && !card.corrupted && card.purpose == SpellPurpose.Offensive) return "Mind Control obstructs deliberate offense until purged or discarded.";
                if (Has(card, CardOp.Strike) && !sec.Fighting) return $"{sec.name} waits in reserve.";
                if (Magic(card) && sec.mindBroken && !card.corrupted) return $"{sec.name} is Mind Broken and cannot cast.";
            }
            if (card.minors != null && card.minors.Count > 0 && !AgeMagic.Playable(CardFace.Tier(card), field.age))
                return $"Age {AgeMagic.Numeral(field.age)} cannot hold a {CardFace.TierName(CardFace.Tier(card))}.";
            if (FieldBonus(s, card, sec) <= 0f) return $"{card.name} needs {ConditionWords(card)}.";
            if (card.requiresChannel && !Spatial.Terrain[Origin(s, sec).battleHex].harmonicChannel) return "The required harmonic channel is unavailable.";
            if (card.effects.Count > 0 && card.effects.All(e => e.op == CardOp.Entrench) && s.attacker) return "Only a defender digs in.";
            return null;
        }

        private static string ConditionWords(CombatCard card)
        {
            var parts = new List<string>();
            foreach (CardCondition c in Enum.GetValues(typeof(CardCondition)))
                if (c != CardCondition.None && (card.condition & c) != 0) parts.Add(c.ToString().ToLowerInvariant());
            if (card.grounds.Count > 0) parts.Add(string.Join(" or ", card.grounds.Select(g => g.ToString().ToLowerInvariant())) + " ground");
            return parts.Count == 0 ? "another field" : string.Join(", ", parts);
        }

        /// <summary>Whether every flag of <paramref name="condition"/> holds for side <paramref name="s"/> this measure.</summary>
        private bool Holds(Side s, CombatCard card, CombatSection voice)
        {
            var c = card.condition;
            if (c == CardCondition.None) return false;
            bool ok = true;
            if ((c & CardCondition.Concealed) != 0) ok &= !s.attacker && field.concealed;
            if ((c & CardCondition.HighGround) != 0) ok &= voice != null && Spatial.Terrain[Projected(voice)].highGround;
            if ((c & CardCondition.RiverCrossing) != 0) ok &= !s.attacker && field.riverCrossing;
            if ((c & CardCondition.Settlement) != 0) ok &= !s.attacker && field.settlement;
            if ((c & CardCondition.Defending) != 0) ok &= !s.attacker;
            if ((c & CardCondition.Attacking) != 0) ok &= s.attacker;
            if ((c & CardCondition.Opening) != 0) ok &= m <= 1;
            if ((c & CardCondition.Hunting) != 0) ok &= s.enemy.side.wild;
            if ((c & CardCondition.LandElement) != 0) ok &= card.binding != SpellBinding.Unattuned && card.binding == field.element;
            if ((c & CardCondition.Sacred) != 0) ok &= field.sacred;
            if ((c & CardCondition.Leyline) != 0) ok &= voice != null && Spatial.Terrain[Projected(voice)].leyline;
            return ok;
        }

        /// <summary>The field's multiplier on a card for a side: its ground and its condition (0: it needs a field it is not on).</summary>
        private float FieldBonus(Side s, CombatCard card, CombatSection voice = null)
        {
            float f = 1f;
            voice = Origin(s, voice);
            var footing = voice == null ? s.footing ?? ground.ground : Spatial.Terrain[Projected(voice)].ground;
            bool onGround = card.grounds.Count > 0 && card.grounds.Contains(footing);
            bool holds = Holds(s, card, voice);
            if (card.requires && ((card.condition != CardCondition.None && !holds) || (card.grounds.Count > 0 && !onGround))) return 0f;
            if (onGround) f *= card.groundBonus;
            if (holds) f *= card.conditionBonus;
            return f;
        }

        private float Flicker(DeckCard dc) => dc.card.flicker >= 0f && !dc.major ? dc.card.flicker : dc.Weight == NoteWeight.Major ? st.majorFlicker : st.minorFlicker;

        private SpellBinding RootOf(DeckCard dc, CombatSection sec)
        {
            if (dc.card.binding != SpellBinding.Unattuned) return dc.card.binding;
            if (sec != null && sec.primary != SpellBinding.Unattuned) return sec.primary;
            return dc.legend?.leitmotif ?? SpellBinding.Unattuned;
        }

        // ===== CHOOSING (the performer composes a Score) =====

        /// <summary>
        /// The performer's Composition: Suspended workings are stabilized or grounded, then each Track takes the card that
        /// is worth more than its drilled Note (the Elite Core always, formation Tracks up to Command Bandwidth), with the
        /// Minors its Hold can carry, and one Reaction is primed where a Track has room. Worth is a proposal, not a rule.
        /// </summary>
        private void Plan(Side s)
        {
            var p = s.perf;
            if (p == null || scoresCommitted) return;
            ResolveSuspensions(s);
            PlanCrises(s);
            var candidates = new List<(Track track, int deck, float score)>();
            foreach (var track in TracksOf(s).OrderBy(x => Elite(x) ? 0 : 1).ThenBy(x => x.voice).ToList())
            {
                if (MajorOf(track) != null) continue;
                float drill = DrillWorth(track);
                int best = -1; float bestScore = 0f;
                for (int i = 0; i < p.hand.Count; i++)
                {
                    var dc = p.deck[p.hand[i]];
                    if (dc.card.noteRole == BattleNoteRole.Minor || dc.card.reaction != null || dc.card.requiredVoices > 1 || dc.card.requiredBond > 0f) continue;
                    if (TrackOf(s, dc) != track || WhyNot(s, dc) != null) continue;
                    float value;
                    planning = true; projectAt = Cadence;
                    try
                    {
                        var (v, target) = Value(s, dc, p);
                        value = WardMajor(dc.card) || TargetFailure(s, dc, target) == null ? v : 0f;
                    }
                    finally { planning = false; projectAt = 0; }
                    float score = value * TacticalWeight(s, dc);
                    if (Intelligence(s) < .25f) score = value > 0f ? 1f / (i + 1f) + drill : 0f;
                    if (value > drill * 1.05f && score > bestScore + 1e-4f) { best = p.hand[i]; bestScore = score; }
                }
                if (best >= 0) candidates.Add((track, best, bestScore));
            }
            int bandwidth = Bandwidth(s.attacker);
            foreach (var c in candidates.OrderBy(x => Elite(x.track) ? 0 : 1).ThenByDescending(x => x.score).ThenBy(x => x.track.voice))
            {
                if (!Elite(c.track) && ComposedFormations(s) >= bandwidth) continue;
                if (p.hand.IndexOf(c.deck) < 0) continue;
                if (Compose(s, c.deck)) Plans(s, p.deck[c.deck], st.autoRendition);
            }
            foreach (int deck in p.hand.ToList())
            {
                var dc = p.deck[deck];
                if (dc.card.reaction == null || WhyNot(s, dc) != null) continue;
                Prime(s, p.hand.IndexOf(deck), -1, st.autoRendition);
            }
        }

        /// <summary>What the Track's drilled Note is worth this Measure, the bar a card must clear.</summary>
        private float DrillWorth(Track track)
        {
            var unit = track.unit; var s = track.side;
            if (unit == null) return s.Conducted ? t.conductorPotency * HarmPerPotency : 0f;
            if (!unit.Standing) return 0f;
            if (unit.Casts && !unit.mindBroken) return unit.potency * unit.IntegrityShare * HarmPerPotency * t.tierPotency[Math.Min(3, unit.harmony.Count)];
            return unit.row == FormationRow.Support ? 0f : VoiceAttack(s, unit) * HarmPerAttack;
        }

        /// <summary>Composes one card as the Track's Major with as many structural Minors as its Hold carries safely.</summary>
        private bool Compose(Side s, int deck)
        {
            var p = s.perf;
            var core = p.deck[deck];
            var minors = p.hand.Where(i => i != deck && p.deck[i].card.noteRole == BattleNoteRole.Minor && p.deck[i].card.chord != null && WhyNot(s, p.deck[i]) == null)
                .OrderBy(i => TrackOf(s, p.deck[i]) == TrackOf(s, core) ? 0 : 1).ThenBy(i => i).ToList();
            int margin = Intelligence(s) >= .75f ? 0 : 1;
            for (int n = Math.Min(3, minors.Count); n >= 0; n--)
            {
                int coreIndex = p.hand.IndexOf(deck);
                if (coreIndex < 0) return false;
                long before = nextChord;
                string why = n == 0 ? CommitCard(s.attacker, coreIndex, -1, st.autoRendition)
                    : CommitChord(s.attacker, coreIndex, minors.Take(n).Select(i => p.hand.IndexOf(i)).ToList(), -1, st.autoRendition);
                if (why != null) continue;
                var chord = chords.Values.FirstOrDefault(c => c.id >= before && c.side == s && !c.drilled);
                if (chord != null && n > 0 && HoldLimit(chord) - (chord.majorBeat - chord.firstBeat + 1) < margin) { Retract(s.attacker, chord.id); continue; }
                return true;
            }
            return false;
        }

        /// <summary>Remember what is already composed, so the performer does not stack the same answer twice.</summary>
        private void Plans(Side s, DeckCard dc, float rendition)
        {
            var p = s.perf;
            var target = chords.Values.Where(c => c.side == s && c.core.card.id == dc.card.id && c.composedMeasure == m).Select(c => c.target).LastOrDefault();
            foreach (var e in dc.card.effects.Where(e => e.op == CardOp.Guard))
                foreach (var ally in EffectAllies(s, dc, e, target)) Remember(s, "guard:" + s.side.sections.IndexOf(ally));
            float amount = dc.scale * rendition * FieldBonus(s, dc.card, VoiceOf(s, dc)) * p.cardScale;
            foreach (var e in dc.card.effects)
            {
                float x = e.amount * amount;
                switch (e.op)
                {
                    case CardOp.Guard: foreach (var ally in EffectAllies(s, dc, e, target)) Add(p.plannedGuard, ally, WardStrength(s, dc, e, ally, amount)); break;
                    case CardOp.Ward: foreach (var ally in EffectAllies(s, dc, e, target)) Add(p.plannedGuard, ally, WardStrength(s, dc, e, ally, amount)); break;
                    case CardOp.Mend: foreach (var ally in EffectAllies(s, dc, e, target)) Add(p.plannedMend, ally, x * ally.maxIntegrity); break;
                    case CardOp.Rally: foreach (var ally in EffectAllies(s, dc, e, target)) Add(p.plannedRally, ally, x); break;
                    case CardOp.CoRegulate: foreach (var ally in EffectAllies(s, dc, e, target)) Add(p.plannedRally, ally, x * dc.card.ChannelBeats); break;
                    case CardOp.Expose: if (target != null) Add(p.plannedExpose, target, x); break;
                    case CardOp.Push: if (target != null) p.plannedPush.Add(target); break;
                    case CardOp.Surge: p.plannedSurge += x; break;
                    case CardOp.Crescendo: p.plannedCrescendo += x; break;
                    case CardOp.Sure: p.plannedSure = true; break;
                }
            }
            if (dc.card.purpose == SpellPurpose.Setup) p.plannedSetups++;
        }

        private static void Add(Dictionary<CombatSection, float> d, CombatSection key, float v)
        {
            d.TryGetValue(key, out float old);
            d[key] = old + v;
        }

        private static float Get(Dictionary<CombatSection, float> d, CombatSection key) => key != null && d.TryGetValue(key, out float v) ? v : 0f;

        private float BaseParry(Side s, CombatSection sec) => (s.attacker ? sec.breakthrough : sec.defense) * sec.IntegrityShare;

        /// <summary>A section's steel this measure, as the drilled rhythm would strike (before the ostinato and chance).</summary>
        private float VoiceAttack(Side s, CombatSection sec, bool card = false)
        {
            float attack = card ? Math.Max(sec?.attack ?? 0f, st.handAttack) : sec?.attack ?? 0f;
            if (sec == null || attack <= 0f) return 0f;
            float atk = attack * sec.IntegrityShare * s.stack.atk * s.Of(sec).atk * s.Nerve(t) * (s.attacker ? assault : 1f);
            atk *= (1f + s.surge) * (1f - s.BlindOf(sec));
            if (sec.mindBroken) atk *= t.mindBreakAttack;
            atk *= sec.On(Spatial.Terrain[Projected(sec)].ground)?.attack ?? 1f;
            atk *= SpatialRules.AttackFactor(s.attacker, sec);
            if (sec.row == FormationRow.Back)
            {
                atk *= ground.missiles;
                if (field.weather > 1.2f) atk *= t.stormMissiles;
                if (field.concealed) atk *= t.coverMissiles;
            }
            if (m == 1 && s.initiative) atk *= t.initiative;
            return atk;
        }

        /// <summary>A card's spell potency from its voice: a legend's own voice, a caster's, or anyone's hum.</summary>
        private float VoicePotency(DeckCard dc, CombatSection sec, SpellBinding root)
        {
            float p;
            if (dc.legend != null) p = t.conductorPotency * dc.legend.Score(root) / 21f * (sec?.IntegrityShare ?? 1f);
            else if (sec != null && sec.Casts) p = sec.potency * sec.IntegrityShare;
            else p = st.humPotency * (sec?.IntegrityShare ?? 1f);
            return dc.Weight == NoteWeight.Major ? p * st.majorPotency : p;
        }

        private float HarmPerAttack => t.integrityPerHit * (t.undefendedHit + t.defendedHit) * 0.5f;
        private float HarmPerPotency => t.spellHit * (t.integrityPerSpellHit + 0.5f * t.composurePerSpellHit);

        /// <summary>Steel the enemy is expected to bring down on one of <paramref name="s"/>'s sections this measure.</summary>
        private float IncomingSteel(Side s)
        {
            var e = s.enemy;
            float total = e.side.sections.Where(x => x.Fighting && x.row != FormationRow.Support).Sum(x => x.attack * x.IntegrityShare * (x.mindBroken ? t.mindBreakAttack : 1f)) * e.ostinato;
            if (e.perf != null) total *= 1.3f;
            int targets = Math.Max(1, s.side.Standing.Count());
            return total / targets;
        }

        private float IncomingSpells(Side s)
        {
            var e = s.enemy;
            float total = e.side.sections.Where(x => x.Fighting && x.Casts && !x.mindBroken && x.row != FormationRow.Support).Sum(x => x.potency * x.IntegrityShare) * e.ostinato * HarmPerPotency;
            int targets = Math.Max(1, s.side.sections.Count(x => x.Standing && x.row != FormationRow.Support));
            return total / targets;
        }

        private float OwnSteel(Side s) => s.side.sections.Where(x => x.Fighting && x.row != FormationRow.Support).Sum(x => VoiceAttack(s, x)) * s.ostinato;

        /// <summary>
        /// What a card is worth now, in Integrity-equivalent harm dealt or spared (Composure counts half), and whom it
        /// should aim at. The performer plays the most worth per Beat first. Worth is a proposal, not a rule of the battle.
        /// </summary>
        private (float value, CombatSection target) Value(Side s, DeckCard dc, Performance planned)
        {
            planned = planned ?? s.perf;
            var card = dc.card;
            var sec = VoiceOf(s, dc);
            float f = FieldBonus(s, card, sec);
            if (f <= 0f) return (0f, null);
            float scale = dc.scale * st.autoRendition * f * s.perf.cardScale;
            float value = 0f;
            CombatSection target = null;
            var root = RootOf(dc, sec);
            bool magic = Magic(card);

            foreach (var e in card.effects)
            {
                float x = e.amount * scale;
                switch (e.op)
                {
                    case CardOp.Purge:
                    {
                        int PollutionOn(CombatSection u) => s.perf.hand.Concat(s.perf.draw).Concat(s.perf.discard).Count(i => VoiceOf(s, s.perf.deck[i]) == u && s.perf.deck[i].card.pollution != BattlePollution.None);
                        var clean = EffectTargets(s, dc, e).OrderByDescending(PollutionOn).FirstOrDefault();
                        if (clean != null) { target = target ?? clean; value += PollutionOn(clean) * 5; }
                        break;
                    }
                    case CardOp.Strike:
                    {
                        // Target valuation uses the same spatial pool as actual execution.
                        var targets = EffectTargets(s, dc, e);
                        if (targets.Count == 0 || sec == null) break;
                        var aim = target ?? targets.OrderBy(y => y.IntegrityShare).ThenBy(y => s.enemy.side.sections.IndexOf(y)).First();
                        if (e.aim == CardAim.Enemy) target = target ?? aim;
                        float harm = x * VoiceAttack(s, sec, true) * HarmPerAttack * (1f + s.ExposeOf(aim) + Get(planned.plannedExpose, aim));
                        if (e.aim == CardAim.Enemy && aim.integrity <= harm * 2f) harm *= 1.3f;
                        value += harm;
                        break;
                    }
                    case CardOp.IntegrityDamage:
                        if (EffectTargets(s, dc, e).Count > 0) { target = target ?? EffectTargets(s, dc, e).First(); value += e.amount * dc.scale; }
                        break;
                    case CardOp.Delay:
                    case CardOp.Interrupt:
                    case CardOp.Detune:
                    {
                        // Silence: only workings already committed are read, never the enemy's uncommitted Composition.
                        var held = EffectTargets(s, dc, e).SelectMany(y => chords.Values.Where(c => c.Live && c.committed && !c.drilled && Performer(c.owner) == y && c.majorBeat > beat)).ToList();
                        if (held.Count == 0) break;
                        var aim = target ?? Performer(held.OrderByDescending(c => c.power).First().owner);
                        target = target ?? aim;
                        value += held.Where(c => Performer(c.owner) == aim).Sum(c => c.power) * .5f * Math.Max(1f, Math.Abs(e.amount));
                        break;
                    }
                    case CardOp.Accelerate:
                    case CardOp.Synchronize:
                    case CardOp.ExtendHold:
                    case CardOp.WeaveCapacity:
                    case CardOp.Stabilize:
                    case CardOp.Anchor:
                    case CardOp.Attune:
                        value += Math.Abs(e.amount) * (EffectTargets(s, dc, e).Count > 0 ? 1f : 0f);
                        break;
                    case CardOp.Spell:
                    {
                        if (root == SpellBinding.Unattuned) break;
                        bool fromBack = sec == null || sec.row == FormationRow.Back;
                        var targets = EffectTargets(s, dc, e);
                        if (targets.Count == 0) break;
                        var aim = target != null && targets.Contains(target) ? target : Choose(targets, new List<SpellBinding> { root }, true, t, rng: null);
                        target = target ?? aim;
                        float pot = x * VoicePotency(dc, sec, root) * field.magicAccess * (AgeMagic.MajorNotes(field.age) || card.kind == CardKind.Instinct ? 1f : t.minorNoteMagic);
                        if (fromBack) pot *= 1f - t.signalLoss * s.stack.channel;
                        float mult = HarmonicCircle.Multiplier(root, aim.primary, t);
                        float sensitivity = t.structureSpellTaken + (t.pureLightSpellTaken - t.structureSpellTaken) * (1f - aim.structure);
                        float ward = Math.Min(0.9f, aim.ward);
                        value += Math.Max(0f, pot * (1f + s.crescendo + planned.plannedCrescendo) * HarmPerPotency * mult * sensitivity * (1f - ward) - s.enemy.WardOf(aim, true));
                        break;
                    }
                    case CardOp.Dread:
                    {
                        var targets = EffectTargets(s, dc, e).Where(y => !y.mindBroken).ToList();
                        if (targets.Count == 0) break;
                        var aim = target != null && targets.Contains(target) ? target : targets.OrderBy(y => y.composure).First();
                        if (e.aim == CardAim.Enemy) target = target ?? aim;
                        float fear = x * (sec?.dread ?? 1f) * s.stack.dread;
                        value += fear * (aim.composure <= fear * 1.5f ? 1.2f : 0.5f);
                        break;
                    }
                    case CardOp.Guard:
                    {
                        float incoming = IncomingSteel(s) * HarmPerAttack;
                        foreach (var ally in AllyCandidates(s, dc, e, ref target, y => IncomingSteel(s)))
                        {
                            if (ally.row == FormationRow.Support) continue;
                            float standing = s.WardOf(ally, false) + Get(planned.plannedGuard, ally);
                            value += Math.Min(WardStrength(s, dc, e, ally, scale), Math.Max(0f, incoming - standing));
                        }
                        break;
                    }
                    case CardOp.Ward:
                    {
                        float incoming = IncomingSpells(s);
                        foreach (var ally in AllyCandidates(s, dc, e, ref target, y => incoming))
                            value += Math.Min(WardStrength(s, dc, e, ally, scale), Math.Max(0f, incoming - s.WardOf(ally, true) - Get(planned.plannedGuard, ally)));
                        break;
                    }
                    case CardOp.Mend:
                        foreach (var ally in AllyCandidates(s, dc, e, ref target, y => y.maxIntegrity - y.integrity))
                            if (ally.integrity > 0f) value += Math.Min(x * ally.maxIntegrity, Math.Max(0f, ally.maxIntegrity - ally.integrity - Get(planned.plannedMend, ally)));
                        break;
                    case CardOp.CoRegulate:
                    case CardOp.Rally:
                        foreach (var ally in AllyCandidates(s, dc, e, ref target, y => (y.mindBroken ? 100f : 0f) + y.maxComposure - y.composure))
                        {
                            float gain = Math.Min(x * (e.op == CardOp.CoRegulate ? dc.card.ChannelBeats : 1), Math.Max(0f, ally.maxComposure - ally.composure - Get(planned.plannedRally, ally)));
                            value += gain * 0.5f * (ally.mindBroken ? 2.5f : ally.ComposureShare < 0.3f ? 1.5f : 1f);
                        }
                        break;
                    case CardOp.Draw:
                    case CardOp.Beat:
                        value += (float)Math.Round(e.amount * dc.scale) * AverageHand(s, dc);
                        break;
                    case CardOp.Expose:
                    {
                        var targets = EffectTargets(s, dc, e);
                        if (targets.Count == 0) break;
                        var aim = target ?? targets.OrderBy(y => y.IntegrityShare).ThenBy(y => s.enemy.side.sections.IndexOf(y)).First();
                        target = target ?? aim;
                        float ours = OwnSteel(s) * HarmPerAttack / targets.Count;
                        value += Math.Max(0f, x - s.ExposeOf(aim) - Get(planned.plannedExpose, aim)) * ours * st.statusMeasures * 0.6f;
                        break;
                    }
                    case CardOp.Blind:
                    {
                        var targets = EffectTargets(s, dc, e).Where(y => y.attack > 0f).ToList();
                        if (targets.Count == 0) break;
                        var aim = target != null && targets.Contains(target) ? target : targets.OrderByDescending(y => y.attack * y.IntegrityShare).First();
                        target = target ?? aim;
                        value += Math.Max(0f, x - s.enemy.BlindOf(aim)) * aim.attack * aim.IntegrityShare * s.enemy.ostinato * HarmPerAttack * st.statusMeasures * 0.6f;
                        break;
                    }
                    case CardOp.Burn:
                    {
                        var targets = EffectTargets(s, dc, e);
                        if (targets.Count == 0) break;
                        var aim = target ?? targets.OrderByDescending(y => y.integrity).First();
                        target = target ?? aim;
                        value += x * st.statusMeasures;
                        break;
                    }
                    case CardOp.Douse:
                        foreach (var ally in EffectAllies(s, dc, e, target))
                        {
                            var k = s.Marks(ally);
                            if (k != null && k.burnLeft > 0) value += k.burn * k.burnLeft;
                            value += Math.Min(x, ally.maxComposure - ally.composure) * 0.25f;
                        }
                        break;
                    case CardOp.Push:
                    {
                        var front = EffectTargets(s, dc, e).Where(y => !planned.plannedPush.Contains(y)).ToList();
                        if (front.Count == 0) break;
                        var aim = target != null && front.Contains(target) ? target : front.OrderByDescending(y => y.attack * y.IntegrityShare).First();
                        target = target ?? aim;
                        value += aim.attack * aim.IntegrityShare * s.enemy.ostinato * HarmPerAttack + (aim.Casts ? aim.potency * aim.IntegrityShare * HarmPerPotency * 0.5f : 0f);
                        break;
                    }
                    case CardOp.Entrench:
                        if (!s.attacker && s.entrenched + baseEntrench < t.maxEntrench) value += x * t.entrenchDefense * IncomingSteel(s) * Math.Max(1, s.side.Standing.Count()) * t.integrityPerHit * 0.5f;
                        break;
                    case CardOp.Sure:
                        if (!s.sure && !planned.plannedSure)
                            value += s.side.sections.Where(y => y.Fighting && y.row == FormationRow.Back).Sum(y => VoiceAttack(s, y)) * s.ostinato * (t.undefendedHit - t.defendedHit) * t.integrityPerHit;
                        break;
                    case CardOp.Surge:
                        value += x * OwnSteel(s) * HarmPerAttack;
                        break;
                    case CardOp.Crescendo:
                        value += x * s.side.sections.Where(y => y.Fighting && y.Casts && !y.mindBroken).Sum(y => y.potency * y.IntegrityShare) * s.ostinato * HarmPerPotency;
                        break;
                }
            }
            if (magic)
            {
                // A spell may flicker, and it is paid from the voice's Composure: a caster near breaking holds back.
                var harmony = Chord(s, dc, sec, root);
                float chance = Interference(s, sec, (ChordTier)harmony.Count, card.kind != CardKind.Instinct, harmony, field, t, harmony.Count == 0 && card.kind == CardKind.Spell ? Flicker(dc) : 1f);
                value *= 1f - chance * (1f - t.misfireLands);
                float essence = t.essenceCost[Math.Min(3, harmony.Count)];
                float reserve = sec != null ? sec.composure : s.bar;
                float max = sec != null ? sec.maxComposure : s.barMax;
                value -= essence * 0.5f;
                if (reserve - essence < 0.2f * max) value *= 0.3f;
            }
            // Groundwork: an Offensive card after Setups lands harder; a Setup is worth part of the best attack it prepares.
            if (card.purpose == SpellPurpose.Offensive) value *= 1f + st.groundwork * Math.Min(st.groundworkCap, s.perf.setups + planned.plannedSetups);
            else if (card.purpose == SpellPurpose.Setup && s.perf.setups + planned.plannedSetups < st.groundworkCap)
            {
                float best = 0f;
                foreach (int i in s.perf.hand)
                {
                    var other = s.perf.deck[i];
                    if (other == dc || other.card.purpose != SpellPurpose.Offensive || WhyNot(s, other) != null) continue;
                    best = Math.Max(best, Value(s, other, planned).value);
                }
                value += st.groundwork * best;
            }
            float recoil = card.effects.Where(e => e != null && e.op == CardOp.Recoil).Sum(e => e.amount);
            if (recoil > 0f && sec != null) value -= recoil * sec.maxIntegrity * 0.5f;
            return (value, target);
        }

        /// <summary>
        /// The chord a spell card sounds: its own Minor Notes (a caster's card with no Root of its own plays its caster's
        /// chord), as many as the Age can hold (<see cref="AgeMagic.Playable"/>). Other voices join only as an explicit Ensemble.
        /// </summary>
        private List<SpellBinding> Chord(Side s, DeckCard dc, CombatSection sec, SpellBinding root)
        {
            var notes = new List<SpellBinding>();
            if (root == SpellBinding.Unattuned) return notes;
            var own = (dc.card.minors ?? new List<SpellBinding>()).AsEnumerable();
            if (dc.card.binding == SpellBinding.Unattuned && sec != null && sec.Casts) own = own.Concat(sec.harmony ?? new List<SpellBinding>());
            foreach (var n in own) if (n != SpellBinding.Unattuned && n != root && !notes.Contains(n)) notes.Add(n);
            while (notes.Count > 3 || notes.Count > 0 && !AgeMagic.Playable((ChordTier)notes.Count, field.age)) notes.RemoveAt(notes.Count - 1);
            return notes;
        }

        /// <summary>The allies an effect would fall on; for a single ally, the one that needs it most (and it becomes the card's target).</summary>
        private IEnumerable<CombatSection> AllyCandidates(Side s, DeckCard dc, CardEffect e, ref CombatSection target, Func<CombatSection, float> need)
        {
            if (e.aim == CardAim.Ally)
            {
                var chosen = target != null && EffectTargets(s, dc, e).Contains(target)
                    ? target
                    : EffectTargets(s, dc, e).OrderByDescending(need).ThenBy(y => s.side.sections.IndexOf(y)).FirstOrDefault();
                if (target == null || !s.side.sections.Contains(target)) target = chosen;
                return chosen == null ? Array.Empty<CombatSection>() : new[] { chosen };
            }
            return EffectAllies(s, dc, e, target);
        }

        /// <summary>A rough worth of one more card or Beat: the mean worth of the other cards in hand.</summary>
        private float AverageHand(Side s, DeckCard except)
        {
            var p = s.perf;
            var others = p.hand.Select(i => p.deck[i]).Where(x => x != except && !Has(x.card, CardOp.Draw) && !Has(x.card, CardOp.Beat)).ToList();
            if (others.Count == 0) return 1f;
            return others.Average(x => WhyNot(s, x) == null ? Math.Max(0f, Value(s, x, p).value) : 0f) * 0.8f;
        }


        // ===== PERFORMING =====

        /// <summary>Effects aimed at the enemy wait for the simultaneous Impact of the Beat (an Abjuration can cancel them); the rest happen now.</summary>
        private void Later(Side s, CombatSection target, Action act)
        {
            if (release != null && target != null && ownerOf[target] != s) release.deferred.Add((target, act)); else act();
        }

        private void Execute(Side s, Queued q)
        {
            var p = s.perf;
            var dc = q.frozen;
            var card = dc.card;
            var sec = VoiceOf(s, dc);
            string failure = WhyNot(s, dc) ?? TargetFailure(s, dc, q.target);
            if (failure != null)
            {
                report.plays.Add(new CardPlay { measure = m, beat = beat, action = q.action, attacker = s.attacker, card = card.id, cardName = dc.Name,
                    voice = VoiceName(s, dc), target = q.target?.name, failed = true, failure = failure, purpose = card.purpose, reaction = q.reacting });
                Emit(s, Origin(s, sec), BattleSpatialCause.PreparationFailed, failure);
                return;
            }
            var previousRng = p?.rng;
            if (p != null) p.rng = new CombatRandom(q.seed);
            try { Perform(s, q, p, dc, card, sec); }
            finally { if (p != null) p.rng = previousRng; }
        }

        private void Perform(Side s, Queued q, Performance p, DeckCard dc, CombatCard card, CombatSection sec)
        {
            float fieldBonus = FieldBonus(s, card, sec);
            float scale = dc.scale * q.rendition * fieldBonus * (p?.cardScale ?? 1f);
            float alteration = sec != null && sec.ComposureShare < card.alteredBelowComposure ? 1f + card.alteredPower : 1f;
            alteration *= 1f + Math.Min(2f, card.effects.Where(e => e?.op == CardOp.StatusFuel).Sum(e => Math.Max(0, e.amount) * StatusCount(s, sec, e.pollution)));
            float handPower = StatusInHand(s, sec, BattlePollution.Wound) ? .85f : StatusInHand(s, sec, BattlePollution.Slimed) ? .9f : 1f;
            alteration *= card.purpose == SpellPurpose.Offensive ? handPower : 1f;
            if (card.purpose == SpellPurpose.Offensive) scale *= alteration;
            // Groundwork: every Setup performed earlier this measure makes an Offensive card land harder.
            if (card.purpose == SpellPurpose.Offensive && p != null && p.setups > 0) scale *= 1f + st.groundwork * Math.Min(st.groundworkCap, p.setups);
            // Impetus: a Charge strikes harder on the Measure it collides.
            if (q.chord != null && collided.TryGetValue(q.chord.owner, out int hit) && hit == m &&
                (card.chord?.advance == true || q.chord.notes.Skip(1).Any(n => n.sounded && n.card.card.chord?.advance == true)))
                scale *= mt.impetus * Math.Max(1f, ground.charges ? sec?.charge ?? 1f : 1f);
            var root = RootOf(dc, sec);
            bool magic = Magic(card);
            bool soulWeaver = card.kind != CardKind.Instinct;
            var harmony = magic ? q.explicitChord ? card.minors.ToList() : Chord(s, dc, sec, root) : new List<SpellBinding>();
            var tier = (ChordTier)Math.Min(3, harmony.Count);
            bool misfire = false;
            if (magic && p != null)
            {
                float cap = tier == ChordTier.Unison && card.kind == CardKind.Spell ? Flicker(dc) : 1f;
                misfire = p.rng.NextFloat() < Interference(s, sec, tier, soulWeaver, harmony, field, t, cap);
                if (!Has(card, CardOp.Spell))
                {
                    // A spell with no strike of its own is still paid for (Essence Sacrifice), and a flicker lands weakly and rakes the caster.
                    float essence = soulWeaver ? t.essenceCost[(int)tier] : t.essenceCost[0];
                    if (misfire) { scale *= t.misfireLands; essence += t.backlash * st.humPotency; }
                    if (sec != null) { sec.composure -= essence; sec.casts++; if (misfire) sec.misfires++; }
                    else s.bar -= essence * t.conductorEssence;
                }
            }

            CombatSection target = q.target;
            foreach (var e in card.effects)
            {
                float x = e.amount * scale;
                switch (e.op)
                {
                    case CardOp.Pollute:
                        foreach (var victim in ResolvingTargets(s, dc, e, target))
                        { var unit = victim; Later(s, unit, () => Pollute(ownerOf[unit].attacker, ownerOf[unit].side.sections.IndexOf(unit), e.pollution, Math.Max(1, (int)e.amount))); }
                        break;
                    case CardOp.Purge:
                        foreach (var ally in EffectAllies(s, dc, e, target)) Purge(s.attacker, s.side.sections.IndexOf(ally), e.pollution);
                        break;
                    case CardOp.StatusFuel: break;
                    case CardOp.Veil:
                        foreach (var ally in EffectAllies(s, dc, e, target)) ConcealIntent(s, ally, (int)e.amount, e.durationBeats);
                        break;
                    case CardOp.PhantomIntent:
                        Phantom(s, sec, e); break;
                    case CardOp.Disarm:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        { var unit = aim; Later(s, unit, () => { if (!unit.equipmentLost) { unit.attack *= .5f; unit.equipmentLost = true; } }); }
                        break;
                    case CardOp.SpatialChaos: SpatialChaos(s, sec, e.amount); break;
                    case CardOp.Gather: Gather(s, sec, e.amount); break;
                    case CardOp.CatharticBlast: CadenzaBlast(s, target, x); break;
                    case CardOp.ParasiticResonance:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim;
                            Later(s, victim, () => { victim.parasiticStrain += Math.Max(0, x); CrisisEvent(TrackOf(victim), BattleCrisisCause.ParasiticResonance, "Extraordinary pressure reaches the persistent soul."); });
                        }
                        break;
                    case CardOp.ExtendHold:
                    case CardOp.WeaveCapacity:
                    case CardOp.Stabilize:
                    case CardOp.Anchor:
                        foreach (var ally in e.aim == CardAim.Ally && target != null ? new[] { target } : EffectTargets(s, dc, e).ToArray()) SupportChords(s, e, ally, e.amount * dc.scale);
                        break;
                    case CardOp.Reposition: Reposition(s, dc, e, target); break;
                    case CardOp.IntegrityDamage:
                        foreach (var victim in ResolvingTargets(s, dc, e, target))
                        {
                            var h = HitOf(hits, victim); h.friendlyFire |= ownerOf[victim] == s;
                            float direct = Math.Max(0f, e.amount * dc.scale * q.rendition * fieldBonus * alteration) * (misfire ? t.misfireLands : 1f);
                            if (e.ignoreGuard) h.integrity += direct; else h.direct += direct;
                        }
                        break;
                    case CardOp.Draw: if (p != null) p.bonusDraw += Math.Max(0, (int)Math.Round(e.amount * dc.scale)); break;
                    case CardOp.Beat:
                    case CardOp.Accelerate:
                        foreach (var ally in ResolvingTargets(s, dc, e, target).Where(u => ownerOf[u] == s)) Shift(ally, -Math.Max(1, (int)Math.Round(e.amount)));
                        break;
                    case CardOp.Delay:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim; int n = Math.Max(1, (int)Math.Round(e.amount));
                            Later(s, victim, () => Shift(victim, n));
                        }
                        break;
                    case CardOp.Synchronize:
                    {
                        var allies = ResolvingTargets(s, dc, e, target).Where(u => ownerOf[u] == s).ToList();
                        var pending = chords.Values.Where(c => c.Live && c.majorBeat > beat && allies.Contains(Performer(c.owner))).ToList();
                        if (pending.Count > 1)
                        {
                            int latest = pending.Max(c => c.majorBeat);
                            foreach (var c in pending) if (c.majorBeat < latest) Shift(Performer(c.owner), latest - c.majorBeat);
                        }
                        break;
                    }
                    case CardOp.Interrupt:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim; int n = Math.Max(1, (int)Math.Round(e.amount));
                            Later(s, victim, () =>
                            {
                                var track = TrackOf(victim);
                                foreach (var c in chords.Values.Where(c => c.Live && c.owner == track && c.firstBeat <= beat).ToList())
                                    AddInterference(c, n, BattleChordCause.Interrupted, dc.Name + " broke " + victim.name + "'s Focus.");
                            });
                            if (e.aim == CardAim.Enemy) target = aim;
                        }
                        break;
                    case CardOp.Detune:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim;
                            Later(s, victim, () => { var track = TrackOf(victim); if (track != null) track.detuned = true; });
                        }
                        break;
                    case CardOp.Attune:
                        foreach (var ally in EffectAllies(s, dc, e, target)) { var track = TrackOf(ally); if (track != null) track.attuned = true; }
                        break;
                    case CardOp.Withdraw: if (sec != null) WithdrawFrom(s, sec); break;
                    case CardOp.Rush: break;
                    case CardOp.Reform: s.side.stance = (BattleStance)Math.Max(0, Math.Min(Enum.GetValues(typeof(BattleStance)).Length - 1, (int)Math.Round(e.amount))); break;
                    case CardOp.Strike: target = Strike(s, dc, sec, e, target, x) ?? target; break;
                    case CardOp.Spell: target = Spell(s, dc, sec, e, root, harmony, target, x, misfire) ?? target; break;
                    case CardOp.Dread:
                    {
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var h = HitOf(hits, aim); h.friendlyFire |= ownerOf[aim] == s;
                            h.composure += (e.flat ? e.amount * dc.scale * q.rendition * fieldBonus : x) * (sec?.dread ?? 1f) * s.stack.dread * s.Of(sec).dread;
                            if (e.aim == CardAim.Enemy || e.aim == CardAim.Ally) target = aim;
                        }
                        break;
                    }
                    case CardOp.Guard:
                    case CardOp.Ward:
                        // A Ward's performance is its Abjuration response, so the rendition is read in the window, not in its Strength.
                        GrantGuard(s, dc, e, target, scale / Math.Max(.001f, q.rendition), q.ward ?? q.chord, q.rendition);
                        break;
                    case CardOp.Mend:
                        foreach (var ally in EffectAllies(s, dc, e, target))
                        {
                            if (ally.integrity <= 0f && !ally.deathKnell) continue;
                            float mended = Math.Min(ally.maxIntegrity * x, Math.Max(0f, ally.maxIntegrity - ally.integrity));
                            ally.integrity += mended;
                            if (ally.integrity > 0f) ally.deathKnell = false;
                            ally.lost = Math.Max(0f, ally.lost - mended);
                        }
                        break;
                    case CardOp.Rally:
                        foreach (var ally in EffectAllies(s, dc, e, target)) ally.composure = Math.Min(ally.maxComposure, Math.Max(0f, ally.composure) + x);
                        break;
                    case CardOp.Expose:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim; float share = x;
                            Later(s, victim, () => { var k = Mark(victim); k.expose = Math.Max(k.exposeLeft > 0 ? k.expose : 0f, share); k.exposeLeft = st.statusMeasures; });
                            if (e.aim == CardAim.Enemy || e.aim == CardAim.Ally) target = aim;
                        }
                        break;
                    case CardOp.Blind:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim; float share = x;
                            Later(s, victim, () => { var k = Mark(victim); k.blind = Math.Min(0.9f, Math.Max(k.blindLeft > 0 ? k.blind : 0f, share)); k.blindLeft = st.statusMeasures; });
                            if (e.aim == CardAim.Enemy || e.aim == CardAim.Ally) target = aim;
                        }
                        break;
                    case CardOp.Burn:
                        foreach (var aim in ResolvingTargets(s, dc, e, target))
                        {
                            var victim = aim; float amount = x;
                            Later(s, victim, () => { var k = Mark(victim); k.burn = Math.Max(k.burnLeft > 0 ? k.burn : 0f, amount); k.burnLeft = st.statusMeasures;
                                if (victim.eliteRole != BattleEliteRole.None) Pollute(ownerOf[victim].attacker, ownerOf[victim].side.sections.IndexOf(victim), BattlePollution.Burn); });
                            if (ownerOf[aim] == s) Emit(s, aim, BattleSpatialCause.FriendlyFire, "An allied burn was inflicted.");
                            if (e.aim == CardAim.Enemy || e.aim == CardAim.Ally) target = aim;
                        }
                        break;
                    case CardOp.Douse:
                        foreach (var ally in EffectAllies(s, dc, e, target))
                        {
                            var k = s.Marks(ally);
                            if (k != null) { k.burn = 0f; k.burnLeft = 0; }
                            ally.composure = Math.Min(ally.maxComposure, Math.Max(0f, ally.composure) + x);
                        }
                        break;
                    case CardOp.Push:
                    {
                        var front = EffectTargets(s, dc, e);
                        if (front.Count == 0) break;
                        var aim = target != null && front.Contains(target) ? target : front.OrderByDescending(y => y.attack * y.IntegrityShare).First();
                        if (release != null) release.pushes.Add((ownerOf[aim], aim));
                        else { Retreat(ownerOf[aim], aim, displaced: true); Pressure(); PumpCascade(); }
                        target = aim;
                        break;
                    }
                    case CardOp.Entrench:
                        if (!s.attacker && s.entrenched + baseEntrench < t.maxEntrench)
                        {
                            s.entrenched = Math.Min(t.maxEntrench - baseEntrench, s.entrenched + Math.Max(0f, e.amount));
                            defenderDefense = DefenderDefense(baseEntrench + s.entrenched);
                        }
                        break;
                    case CardOp.Sure: s.sure = true; break;
                    case CardOp.Surge: s.surge += x; break;
                    case CardOp.Crescendo: s.crescendo += x; break;
                    case CardOp.Recoil:
                        // Struggle's price: the voice's own body, never below the last of it.
                        if (sec != null && sec.integrity > 1f)
                        {
                            float cost = Math.Min(sec.integrity - 1f, sec.maxIntegrity * e.amount);
                            sec.integrity -= cost;
                            sec.lost += cost;
                        }
                        break;
                }
            }

            if (p == null) return;
            if (card.purpose == SpellPurpose.Setup) p.setups++;
            if (!q.reacting || card.reaction != null) p.played++;
            if (misfire) p.flickers++;
            p.tally.TryGetValue(dc.Name, out int seen);
            p.tally[dc.Name] = seen + 1;
            if (q.reacting && card.reaction == null) return;
            report.plays.Add(new CardPlay
            {
                reaction = q.reacting, beat = beat, action = q.action,
                measure = m, attacker = s.attacker, card = card.id, cardName = dc.Name, voice = VoiceName(s, dc), target = target?.name,
                flicker = misfire, field = fieldBonus, rendition = q.rendition, purpose = card.purpose, groundwork = card.purpose == SpellPurpose.Offensive ? Math.Min(st.groundworkCap, p.setups) : 0,
            });
            if (p.logged < 6 && (m == 1 || fieldBonus > 1.01f && seen == 0 || harmony.Count > 0 && seen == 0))
            {
                p.logged++;
                string where = fieldBonus > 1.01f ? $", at home here (x{fieldBonus:0.##})" : string.Empty;
                string chord = harmony.Count > 0 ? $", layered into a {tier} with {string.Join(" and ", harmony)}" : string.Empty;
                string who = VoiceName(s, dc) == s.side.name ? s.side.name : $"{VoiceName(s, dc)} of {s.side.name}";
                report.log.Add($"Measure {m}, Beat {BattleMeasureMath.Rel(beat)}: {who} play{(dc.voice < 0 ? "s" : "")} {dc.Name}{where}{chord}{(misfire ? "; it flickers" : string.Empty)}.");
            }
        }

        private CardMarks Mark(CombatSection sec)
        {
            if (!marks.TryGetValue(sec, out var k)) marks[sec] = k = new CardMarks();
            return k;
        }

        /// <summary>A card's blow: as the voice's drilled steel strikes, all on one enemy or spread along the line.</summary>
        private CombatSection Strike(Side s, DeckCard dc, CombatSection sec, CardEffect e, CombatSection wanted, float amount)
        {
            if (sec == null) return null;
            var targets = EffectTargets(s, dc, e);
            if (targets.Count == 0) return null;
            float attacks = amount * VoiceAttack(s, sec, true);
            if (attacks <= 0f) return null;
            bool missile = sec.row == FormationRow.Back;
            float dread = sec.dread * s.stack.dread * s.Of(sec).dread;
            if (e.aim == CardAim.EnemyLine || e.aim == CardAim.Allies)
            {
                float frontage = targets.Sum(x => Math.Max(0.1f, x.width));
                foreach (var x in targets) Blow(s, x, attacks * Math.Max(0.1f, x.width) / frontage, sec.piercing, dread, missile, e.ignoreArmor, e.ignoreGuard);
                return null;
            }
            var target = EffectTarget(s, dc, e, wanted);
            if (target == null) return null;
            Blow(s, target, attacks, sec.piercing, dread, missile, e.ignoreArmor, e.ignoreGuard);
            return target;
        }

        private void Blow(Side s, CombatSection target, float attacks, float piercing, float dread, bool missile, bool ignoreArmor = false, bool ignoreGuard = false)
        {
            if (ownerOf[target] != s) attacks *= SpatialRules.TargetAttackFactor(s.attacker, target);
            var hit = HitOf(hits, target);
            hit.friendlyFire |= ownerOf[target] == s;
            hit.attacks += attacks;
            if (ignoreArmor) hit.armorBypass += attacks; else hit.pierce += attacks * piercing;
            if (ignoreGuard) hit.guardBypass += attacks;
            hit.dread += attacks * dread;
            if (missile && s.sure) hit.sure += attacks;
        }

        /// <summary>A card's spell, cast as the drilled spells are (<see cref="Cast"/>), its flicker already decided.</summary>
        private CombatSection Spell(Side s, DeckCard dc, CombatSection sec, CardEffect e, SpellBinding root, List<SpellBinding> harmony, CombatSection wanted, float amount, bool misfire)
        {
            if (root == SpellBinding.Unattuned) return null;
            bool fromBack = sec == null || sec.row == FormationRow.Back;
            var targets = EffectTargets(s, dc, e);
            if (targets.Count == 0) return null;
            bool area = e.aim == CardAim.EnemyLine || e.aim == CardAim.Allies;
            var target = wanted == null ? Choose(targets, new List<SpellBinding> { root }, true, t, null) : targets.Contains(wanted) ? wanted : null;
            if (!area && target == null) return null;
            float potency = amount * VoicePotency(dc, sec, root);
            var primary = dc.legend != null && dc.legend.leitmotif != SpellBinding.Unattuned ? dc.legend.leitmotif
                : sec != null && sec.primary != SpellBinding.Unattuned ? sec.primary : root;
            var affected = area ? targets : new List<CombatSection> { target };
            for (int i = 0; i < affected.Count; i++)
                Cast(s, sec, VoiceName(s, dc), potency / affected.Count, new List<SpellBinding> { root }, harmony, dc.card.kind != CardKind.Instinct, primary, fromBack,
                    sec?.dread ?? 1f, new List<CombatSection> { affected[i] }, true, m, field, t, s.perf.rng, hits, report, misfire, payEssence: i == 0);
            return area ? null : target;
        }
    }
}
