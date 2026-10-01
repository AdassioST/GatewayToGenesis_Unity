using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>A side's Symphony weighed (<see cref="SymphonyPower.Rate"/>).</summary>
public struct SymphonyRating
{
    /// <summary>Harm it deals a measure (Integrity, with Composure harm at half): by drill (the ostinato) and by its cards (the melody).</summary>
    public float ostinato, melody;
    /// <summary>What it can take: its line's Integrity, half its Composure, and what its parries, guards and mending spare it over a battle.</summary>
    public float endurance;
    /// <summary>Nominal timeline Beats per Measure and cards in the deck; not an action allowance.</summary>
    public int beats, cards;
    /// <summary>The one number: sqrt(offense x endurance), as a Lanchester strength. Two sides' powers compare as their odds.</summary>
    public float power;

    public float Offense => ostinato + melody;
    /// <summary>The share of its harm its cards carry.</summary>
    public float MelodyShare => Offense <= 0f ? 0f : melody / Offense;
}

/// <summary>
/// The Symphony's power (the owner, Sept 29, 2026: "each symphony has some total power output based on the deck and
/// that's how it should gauge"), with no scene state (tested in <c>SymphonyTests</c>). Every side is weighed the same way,
/// armies, expedition parties and creature bands alike: its sections' drilled output, plus its deck's expected output
/// per measure (the mean worth of its cards per Beat, times the timeline cadence), against how much it can take.
/// On a given field the ground, the side's footing and the cards at home there are counted. It is a gauge for the map
/// and the forecast; the battle itself is decided by playing the cards (<see cref="BattleResolver"/>). Every weight here
/// is a proposal.
/// </summary>
public static class SymphonyPower
{
    /// <summary>Measures a battle is expected to last, for what guards and mending spare over it.</summary>
    public const float ExpectedMeasures = 6f;
    /// <summary>Guards, wards, mending and rallies spare more than their face value (a Mind Break avoided keeps a whole section fighting): calibrated so a par deck reads as the edge it gives in play.</summary>
    public const float CardDefenseWeight = 2.5f;

    public static SymphonyRating Rate(BattleSide side, CombatSettings settings = null, Battlefield field = null, bool attacking = false)
    {
        var r = new SymphonyRating();
        if (side == null) return r;
        settings = settings ?? new CombatSettings();
        var t = settings.Tuning;
        var st = settings.Symphony.Tuning;
        var mt = settings.Measure;
        r.beats = BattleMeasureMath.Beats;
        var ground = field == null ? null : settings.Ground(field.ground);
        var footing = attacking && field?.attackerGround != null ? field.attackerGround.Value : ground?.ground ?? BattleGround.Open;
        float harmPerAttack = t.integrityPerHit * (t.undefendedHit + t.defendedHit) * 0.5f;
        float harmPerPotency = t.spellHit * (t.integrityPerSpellHit + 0.5f * t.composurePerSpellHit);
        float magic = (field?.magicAccess ?? 1f) * (field == null || AgeMagic.MajorNotes(field.age) ? 1f : t.minorNoteMagic);
        bool deck = side.HasSymphony;

        float Attack(CombatSection s)
        {
            if (!s.Standing || s.row == FormationRow.Support) return 0f;
            float a = s.attack * s.IntegrityShare * (s.mindBroken ? t.mindBreakAttack : 1f) * (s.On(footing)?.attack ?? 1f);
            if (ground != null && s.row == FormationRow.Back) a *= ground.missiles;
            return a;
        }
        float Potency(CombatSection s) => s.Standing && s.Casts && !s.mindBroken && s.row != FormationRow.Support
            ? s.potency * s.IntegrityShare * (s.soulWeaver ? magic : 1f) * (s.row == FormationRow.Back ? 1f - t.signalLoss : 1f) : 0f;
        float Parry(CombatSection s) => s.Standing ? (attacking ? s.breakthrough : s.defense) * s.IntegrityShare * (s.On(footing)?.defense ?? 1f) : 0f;
        // Each Measure every Track sounds one drilled Note; a front section engaged in a Skirmish also trades Attrition on the Beats after contact.
        float Drill(CombatSection s) => s.Casts && !s.mindBroken ? Potency(s) * harmPerPotency : Attack(s) * harmPerAttack;
        float Attrition(CombatSection s) => s.row == FormationRow.Front ? Attack(s) * harmPerAttack * (BattleMeasureMath.Beats - 1) * mt.attritionShare : 0f;

        var drilled = side.sections.Where(s => s.Standing).Select(Drill).ToList();
        r.ostinato = drilled.Sum() + side.sections.Where(s => s.Standing).Sum(Attrition);
        float line = side.sections.Where(s => s.Standing && s.row != FormationRow.Support).Sum(s => Math.Max(0f, s.integrity) + 0.5f * Math.Max(0f, s.composure));
        float parry = side.sections.Where(s => s.row != FormationRow.Support).Sum(Parry) * (!attacking && ground != null ? ground.defense : 1f);
        r.endurance = line + parry * (t.undefendedHit - t.defendedHit) * t.integrityPerHit * ExpectedMeasures * 0.5f;

        if (deck)
        {
            var live = side.deck.Where(dc => dc?.card != null && (dc.voice < 0 ? side.conductor != null && side.conductor.State != ComposureState.Surrender
                : dc.voice < side.sections.Count && side.sections[dc.voice].Standing)).ToList();
            r.cards = live.Count;
            int voices = live.Select(dc => dc.voice).Distinct().Count();
            int handCapacity = Math.Max(st.minBeats, Math.Min(st.maxBeats, (int)Math.Floor(st.beatsBase + st.beatsPerVoice * voices + 0.5f)));
            if (side.conductor != null && side.conductor.Stars(LegendClass.Sovereign) >= 2) handCapacity += st.sovereignBeat;
            var majors = live.Where(dc => dc.card.noteRole != BattleNoteRole.Minor && dc.card.reaction == null).ToList();
            if (majors.Count > 0)
            {
                float gain = 0f, defense = 0f;
                foreach (var dc in majors)
                {
                    var sec = dc.voice >= 0 ? side.sections[dc.voice] : null;
                    var (o, dfn) = Worth(dc, sec, side, field, attacking, footing, t, st, harmPerAttack, harmPerPotency, magic);
                    // A card replaces its Track's drilled Note only when it is worth more: what it adds is the difference.
                    gain += Math.Max(0f, o - (sec == null ? 0f : Drill(sec))); defense += dfn;
                }
                // One Major per Track per Measure: the hand and the Command Bandwidth bound how many Tracks play a card
                // instead of their drilled Note. A field-independent gauge; the forecast simulates the actual Measures.
                int bandwidth = mt.bandwidthBase + (side.conductor == null ? 0 : BattleMeasureMath.Mastery(side.conductor.Score(SpellBinding.Resonance)));
                int elites = side.sections.Count(s => s.Standing && (s.eliteRole != BattleEliteRole.None || s.leader != null)) + (side.conductor != null ? 1 : 0);
                int plays = Math.Min(handCapacity + st.handExtra, Math.Min(voices, bandwidth + elites));
                r.melody = gain / majors.Count * plays;
                r.endurance += defense / majors.Count * plays * ExpectedMeasures * CardDefenseWeight * 0.5f;
            }
        }
        r.power = (float)Math.Sqrt(Math.Max(0f, r.Offense) * Math.Max(0f, r.endurance));
        return r;
    }

    /// <summary>What one card is worth when played: harm it deals, and harm it spares or restores (Composure at half).</summary>
    private static (float offense, float defense) Worth(DeckCard dc, CombatSection sec, BattleSide side, Battlefield field, bool attacking, BattleGround footing,
        CombatTuning t, SymphonyTuning st, float harmPerAttack, float harmPerPotency, float magic)
    {
        var card = dc.card;
        float f = dc.scale;
        if (field != null)
        {
            bool onGround = card.grounds.Count > 0 && card.grounds.Contains(footing);
            bool holds = card.condition != CardCondition.None && Holds(card.condition, card.binding, field, attacking, side);
            if (card.requires && ((card.condition != CardCondition.None && !holds) || (card.grounds.Count > 0 && !onGround))) return (0f, 0f);
            if (onGround) f *= card.groundBonus;
            if (holds) f *= card.conditionBonus;
        }
        float attack = sec == null ? 0f : Math.Max(sec.attack, st.handAttack) * sec.IntegrityShare * (sec.On(footing)?.attack ?? 1f);
        float defense = sec == null ? 0f : (attacking ? sec.breakthrough : sec.defense) * sec.IntegrityShare;
        var root = card.binding != SpellBinding.Unattuned ? card.binding : sec != null && sec.primary != SpellBinding.Unattuned ? sec.primary : dc.legend?.leitmotif ?? SpellBinding.Unattuned;
        float potency = dc.legend != null ? t.conductorPotency * dc.legend.Score(root) / 21f : sec != null && sec.Casts ? sec.potency * sec.IntegrityShare : st.humPotency;
        if (dc.Weight == NoteWeight.Major) potency *= st.majorPotency;
        if (card.kind != CardKind.Instinct) potency *= magic;
        int allies = Math.Max(1, side.sections.Count(s => s.Standing));
        float meanDefense = side.sections.Where(s => s.Standing && s.row != FormationRow.Support).Select(s => (attacking ? s.breakthrough : s.defense) * s.IntegrityShare).DefaultIfEmpty(0f).Average();
        float meanIntegrity = side.sections.Where(s => s.Standing).Select(s => s.maxIntegrity).DefaultIfEmpty(0f).Average();
        float guardWorth = (t.undefendedHit - t.defendedHit) * t.integrityPerHit;

        float o = 0f, d = 0f;
        foreach (var e in card.effects)
        {
            float x = e.amount * f;
            int n = e.aim == CardAim.Allies ? allies : 1;
            switch (e.op)
            {
                case CardOp.Strike: o += x * attack * harmPerAttack; break;
                case CardOp.IntegrityDamage: o += x; break;
                case CardOp.Spell: o += x * potency * harmPerPotency; break;
                case CardOp.Dread: o += x * 0.5f; break;
                case CardOp.Burn: o += x * st.statusMeasures; break;
                case CardOp.Expose: o += x * attack * harmPerAttack * st.statusMeasures; break;
                case CardOp.Push: o += attack * harmPerAttack * 0.5f + 2f; break;
                case CardOp.Surge: o += x * side.sections.Sum(s => s.Standing && s.row != FormationRow.Support ? s.attack * s.IntegrityShare : 0f) * harmPerAttack; break;
                case CardOp.Crescendo: o += x * side.sections.Sum(s => s.Standing && s.Casts ? s.potency * s.IntegrityShare : 0f) * harmPerPotency; break;
                case CardOp.Sure: o += side.sections.Where(s => s.Standing && s.row == FormationRow.Back).Sum(s => s.attack * s.IntegrityShare) * guardWorth * 0.5f; break;
                case CardOp.Guard: d += x * n * (e.flat ? 1f : (e.aim == CardAim.Self ? defense : meanDefense) * guardWorth); break;
                case CardOp.Ward: d += x * n * 4f; break;
                case CardOp.Mend: d += x * n * meanIntegrity * 0.5f; break;
                case CardOp.Rally: case CardOp.Douse: d += x * n * 0.5f; break;
                case CardOp.Blind: d += x * st.statusMeasures * 3f; break;
                case CardOp.Entrench: d += attacking ? 0f : x * 3f; break;
                case CardOp.Draw: case CardOp.Beat: o += e.amount * 2f; break;
                case CardOp.Delay: case CardOp.Synchronize: d += e.amount * 2f; break;
                case CardOp.Accelerate: o += e.amount * 2f; break;
                case CardOp.Recoil: d -= sec == null ? 0f : x * sec.maxIntegrity * 0.5f; break;
            }
        }
        if (card.kind == CardKind.Spell)
        {
            float flicker = card.flicker >= 0f ? card.flicker : dc.Weight == NoteWeight.Major ? st.majorFlicker : st.minorFlicker;
            float chance = Math.Min(field == null ? flicker : AgeMagic.Interference(ChordTier.Unison, field.age, t), flicker);
            float kept = 1f - chance * (1f - t.misfireLands);
            o *= kept;
            d *= kept;
        }
        return (o, d);
    }

    private static bool Holds(CardCondition c, SpellBinding binding, Battlefield field, bool attacking, BattleSide side)
    {
        bool ok = true;
        if ((c & CardCondition.Concealed) != 0) ok &= !attacking && field.concealed;
        if ((c & CardCondition.HighGround) != 0) ok &= attacking ? field.downhill > 0.02f : field.height > 0.02f;
        if ((c & CardCondition.RiverCrossing) != 0) ok &= !attacking && field.riverCrossing;
        if ((c & CardCondition.Settlement) != 0) ok &= !attacking && field.settlement;
        if ((c & CardCondition.Defending) != 0) ok &= !attacking;
        if ((c & CardCondition.Attacking) != 0) ok &= attacking;
        if ((c & CardCondition.Opening) != 0) ok &= false; // one measure of several: not counted in the gauge
        if ((c & CardCondition.Hunting) != 0) ok &= false; // depends on the enemy, not counted in the gauge
        if ((c & CardCondition.LandElement) != 0) ok &= binding != SpellBinding.Unattuned && binding == field.element;
        if ((c & CardCondition.Sacred) != 0) ok &= field.sacred;
        if ((c & CardCondition.Leyline) != 0) ok &= field.leyline;
        return ok;
    }

    // ===== THE BREAKDOWN (Civ VI's strength and its modifiers) =====

    private static BattleSide Bare(BattleSide side, bool heal) => new BattleSide
    {
        name = side.name, conductor = side.conductor, tempo = side.tempo, wild = side.wild, entrenchment = side.entrenchment,
        sections = heal ? side.sections.Select(s => { var c = s.Clone(); c.Reset(); return c; }).ToList() : side.sections,
        deck = new List<DeckCard>(),
    };

    /// <summary>
    /// A side's strength on a field against <paramref name="enemy"/> (null: none), and what makes it, the way Civ VI lists
    /// a unit's combat strength: its base (sections at full strength), then each modifier as strength added or taken
    /// away: wounds and weariness, its Symphony, the ground, its commander and legends, heights, rivers, a settlement,
    /// earthworks, the land's element, the Loom, the elements against the enemy, who reads the ground first, Fallout.
    /// Structural modifiers (the ground, the Symphony) are measured by rating the side with and without them; the others
    /// by their effect on its offense and endurance (strength goes as their geometric mean). Proposals all.
    /// </summary>
    public static List<StrengthFactor> Breakdown(BattleSide side, BattleSide enemy, CombatSettings settings, Battlefield field, bool attacking, out float total)
    {
        var list = new List<StrengthFactor>();
        total = 0f;
        if (side == null) return list;
        settings = settings ?? new CombatSettings();
        var t = settings.Tuning;
        field = field ?? new Battlefield();

        float full = Rate(Bare(side, true), settings, null, attacking).power;
        int people = side.sections.Where(s => s.Standing).Sum(s => s.count);
        list.Add(new StrengthFactor { label = "Base strength", amount = full, detail = $"{side.sections.Count(s => s.Standing)} section{(side.sections.Count(s => s.Standing) == 1 ? "" : "s")}{(people > 0 ? $", {people} strong" : "")}" });
        float running = full;
        void Step(string label, float next, string detail = null)
        {
            if (Math.Abs(next - running) >= 0.05f) list.Add(new StrengthFactor { label = label, amount = next - running, detail = detail });
            running = next;
        }
        void Mult(string label, float k, string detail = null) => Step(label, running * k, detail);

        Step("Wounds and weariness", Rate(Bare(side, false), settings, null, attacking).power);
        if (side.HasSymphony)
        {
            var r = Rate(side, settings, null, attacking);
            Step("Symphony", r.power, $"{r.cards} cards, {r.beats} Beats a measure, {r.MelodyShare:P0} of its harm");
        }
        var ground = settings.Ground(field.ground);
        var footing = attacking && field.attackerGround != null ? settings.Ground(field.attackerGround.Value) : ground;
        float onField = Rate(side, settings, field, attacking).power;
        Step(onField >= running ? "Ideal terrain" : "Poor terrain", onField, attacking && footing != ground ? $"{footing.name}, into {ground.name}" : ground.name);

        // What fights by steel and what by spells: the modifiers below touch one or the other.
        float steel = side.sections.Where(s => s.Standing && s.row != FormationRow.Support).Sum(s => s.attack * s.IntegrityShare) * t.integrityPerHit * (t.undefendedHit + t.defendedHit) * 0.5f;
        float spells = side.sections.Where(s => s.Standing && s.Casts && s.row != FormationRow.Support).Sum(s => s.potency * s.IntegrityShare) * t.spellHit * (t.integrityPerSpellHit + 0.5f * t.composurePerSpellHit)
                       + (side.conductor != null && side.conductor.leitmotif != SpellBinding.Unattuned ? t.conductorPotency * t.spellHit * 3f : 0f);
        float spellShare = steel + spells <= 0f ? 0f : spells / (steel + spells);
        float Sq(float offense, float endurance) => (float)Math.Sqrt(Math.Max(0f, offense) * Math.Max(0f, endurance));

        var c = side.conductor;
        if (c != null && c.State != ComposureState.Surrender)
        {
            float per = t.greatPerStar;
            float atk = 1f + per * c.Stars(LegendClass.Vanguard), def = 1f + per * c.Stars(LegendClass.Architect), pot = 1f + per * c.Stars(LegendClass.Concertist);
            float nerve = 1f + per * (c.Stars(LegendClass.Sovereign) + c.Stars(LegendClass.Chronicler));
            float steady = t.conductorNerve != null && t.conductorNerve.Length > 0 ? t.conductorNerve[0] : 1f;
            Mult($"Commander {c.name}", steady * Sq(atk * (1f - spellShare) + pot * spellShare, def * nerve), c.Title);
        }
        var leaders = side.sections.Where(s => s.Standing && s.leader != null && s.leader.State != ComposureState.Surrender).ToList();
        if (leaders.Count > 0)
        {
            float lift = leaders.Sum(s => s.maxIntegrity * t.leaderPerStar * s.leader.greats.Values.Sum()) / Math.Max(1f, side.sections.Sum(s => s.maxIntegrity));
            Mult(leaders.Count == 1 ? $"Led by {leaders[0].leader.name}" : $"Led by {leaders.Count} legends", 1f + lift);
        }
        float bonded = side.sections.Where(s => s.Standing && s.bonds != null && s.bonds.Count > 0).Sum(s => s.maxIntegrity * t.attachmentPerStar * s.bonds.Values.Max());
        if (bonded > 0f) Mult("Attached companies", 1f + bonded / Math.Max(1f, side.sections.Sum(s => s.maxIntegrity)), "they fight for their legend");

        if (attacking)
        {
            if (field.downhill > 0.02f) Mult("Coming downhill", Sq(1f + Math.Min(t.heightCap, field.downhill / 0.1f * t.heightDefense), 1f));
            if (field.riverCrossing) Mult("Crossing a river", Sq(t.riverCrossing, 1f));
            if (ground.assault != 1f) Mult(ground.assault > 1f ? "Open to assault" : "Hard to assault", Sq(ground.assault, 1f), ground.name);
        }
        else
        {
            if (field.height > 0.02f) Mult("Higher ground", Sq(1f, 1f + Math.Min(t.heightCap, field.height / 0.1f * t.heightDefense)));
            if (field.settlement) Mult("Holding a settlement", Sq(1f, t.settlementDefense));
            float dug = Math.Min(t.maxEntrench, side.entrenchment + side.sections.Where(s => s.Standing && s.row == FormationRow.Support).Sum(s => s.entrench));
            if (dug > 0f) Mult("Dug in", Sq(1f, 1f + dug * t.entrenchDefense), $"{dug:0} level{(dug == 1f ? "" : "s")}");
        }

        if (spellShare > 0f)
        {
            var roots = side.sections.Where(s => s.Standing && s.Casts).Select(s => s.primary).Concat(c != null ? new[] { c.leitmotif } : Array.Empty<SpellBinding>()).Where(b => b != SpellBinding.Unattuned).ToList();
            if (field.element != SpellBinding.Unattuned && roots.Contains(field.element))
                Mult($"The land sings {field.element}", 1f + spellShare * (t.landElement - 1f) * roots.Count(b => b == field.element) / roots.Count);
            float loom = (t.incoherentPotency + (t.coherentPotency - t.incoherentPotency) * Mathf.Clamp01(field.coherence)) * (field.leyline ? t.leylinePotency : 1f);
            if (Math.Abs(loom - 1f) > 0.01f) Mult(loom > 1f ? "A coherent Loom" : "A frayed Loom", 1f + spellShare * (loom - 1f), field.leyline ? "a leyline beneath" : $"Coherence {field.coherence:P0}");
            if (enemy != null && roots.Count > 0)
            {
                var foes = enemy.sections.Where(s => s.Standing && s.row != FormationRow.Support).ToList();
                if (foes.Count > 0)
                {
                    float mean = foes.Average(f => HarmonicCircle.Multiplier(HarmonicCircle.BestAgainst(roots, f.primary, t), f.primary, t));
                    var best = foes.Select(f => (f, b: HarmonicCircle.BestAgainst(roots, f.primary, t))).OrderByDescending(x => HarmonicCircle.Multiplier(x.b, x.f.primary, t)).First();
                    if (Math.Abs(mean - 1f) > 0.01f)
                        Mult(mean > 1f ? "The elements favour it" : "The elements are against it", 1f + spellShare * (mean - 1f), $"{best.b} on {best.f.primary}");
                }
            }
        }
        if (enemy != null)
        {
            float recon = side.sections.Where(s => s.Standing && s.row == FormationRow.Support).Sum(s => s.recon) + (c != null && c.Stars(LegendClass.Seer) > 0 ? 1f : 0f) + (!attacking && field.concealed ? 1f : 0f);
            float enemyRecon = enemy.sections.Where(s => s.Standing && s.row == FormationRow.Support).Sum(s => s.recon) + (enemy.conductor != null && enemy.conductor.Stars(LegendClass.Seer) > 0 ? 1f : 0f) + (attacking && field.concealed ? 1f : 0f);
            if (recon > enemyRecon) Mult(!attacking && field.concealed ? "Ambush from cover" : "Reads the ground first", 1f + (t.initiative - 1f) / ExpectedMeasures + (!attacking && field.concealed ? t.ambushShock * 0.25f : 0f));
            else if (enemyRecon > recon && attacking && field.concealed) Mult("Walking into an ambush", Sq(1f, 1f - t.ambushShock * 0.5f));
        }
        if (field.fallout > 0.05f)
        {
            float pure = side.sections.Where(s => s.Standing && s.PureLight && s.niche != HarmonicNiche.Discordant).Sum(s => s.maxIntegrity) / Math.Max(1f, side.sections.Sum(s => s.maxIntegrity));
            if (pure > 0f) Mult("Vibrational Fallout", 1f - pure * Math.Min(0.6f, field.fallout * 0.3f), $"{field.fallout:P0}");
        }
        total = Math.Max(0f, running);
        return list;
    }

    /// <summary>The attacker's odds from the two powers (0.5 even; squared, as Lanchester has it): a quick reading of the odds before a battle.</summary>
    public static float Odds(BattleSide attacker, BattleSide defender, CombatSettings settings = null, Battlefield field = null)
    {
        float a = Rate(attacker, settings, field, true).power, d = Rate(defender, settings, field, false).power;
        // Lanchester: odds go as the square of strength.
        return a * a + d * d <= 0f ? 0.5f : a * a / (a * a + d * d);
    }

    /// <summary>"overwhelming", "favoured", "even", "dangerous", "deadly": how an odds share reads.</summary>
    public static string Words(float odds) =>
        odds >= 0.75f ? "overwhelming" : odds >= 0.58f ? "favoured" : odds > 0.42f ? "even" : odds > 0.25f ? "dangerous" : "deadly";
}
