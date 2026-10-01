using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Symphony of War's auto-resolve (vault: Combat System.md, the macro layer), with no scene state (tested in
/// <c>CombatTests</c>). Every section carries two bars, as Stellaris ships carry armor and hull and Total War units carry
/// men and morale:
///
/// - Integrity, the body. Steel wears it down, spells harm it less. At 0 a section is cut down.
/// - Composure, the mind and the magical reserve. Spells and dread wear it down, steel a little, the fallen beside
///   you more; every spell is paid from it (Essence Sacrifice). At 0 a section is Mind Broken: it fights on at a heavy
///   penalty to its attacks, parries, armor and wards, and casts nothing, until musicians or Sacred ground steady it.
///
/// Legends fight too. A stack's commander (the vault's Battle Conductor) lends its Greats to the whole stack and plays
/// its own chord; a section's leader lends its Greats to that section alone (<see cref="BattleLegend"/>). A commander
/// has a battle Composure of its own, separate from its real Composure: the stack's fear, its fallen sections and its
/// own chord wear it down, its share shapes the whole stack each measure, and if it breaks (a Mind Break) the stack
/// fights on leaderless. What the battle did to each legend is returned (<see cref="LegendBattleFate"/>): real strain
/// for defeats and Mind Breaks, fragments, conditions; a legend whose side or section is doomed retreats alone and goes
/// missing in action.
///
/// A battle is played in measures. In each, both sides at once: spells (the Elemental Harmonic Circle against each
/// target's primary binding, Signal Loss, the Age's command of chords, the Loom and the land), then steel (HOI4's
/// attacks against parries, armor against piercing, the ground, charges, flanks, rivers, heights, entrenchment), then
/// the toll (Fallout, the cold, fatigue, rallying) and the states (Mind Break, steadied, cut down, taken captive; each
/// fall shakes the rest of the line). A Mind Broken section below <see cref="CombatTuning.captureBelow"/> Integrity is
/// subdued and taken alive by a side that takes captives, instead of cut down. A side is beaten when its line's
/// Integrity gives out; it scatters and is run down. Composure breaking is how a line gets there. A battle that runs all
/// its measures is a stalemate: the defender holds (Total War's timer). Deterministic for a seed. Every number is a
/// proposal (<see cref="CombatTuning"/>).
/// </summary>
public static partial class BattleResolver
{
    private sealed class Hit
    {
        public bool friendlyFire;
        public float integrity, direct, composure, attacks, pierce, dread, armorBypass, guardBypass;
        /// <summary>Attacks that are not parried (a Sure card's missiles).</summary>
        public float sure;
    }

    /// <summary>What a legend lends: multipliers on attack, parry, potency, dread; Composure harm divided by nerve.</summary>
    private sealed class Boost
    {
        public Boost Clone() => (Boost)MemberwiseClone();
        public float atk = 1f, def = 1f, potency = 1f, dread = 1f, nerve = 1f, interference = 1f, channel = 1f, wounded, rally, recon;

        public static Boost Of(BattleLegend legend, float perStar)
        {
            var b = new Boost();
            if (legend == null) return b;
            b.atk += perStar * legend.Stars(LegendClass.Vanguard);                                  // Perfect Focus: the blazing arc
            b.def += perStar * legend.Stars(LegendClass.Architect);                                 // Absolute Certainty: the unbreakable structure
            b.potency += perStar * legend.Stars(LegendClass.Concertist);                            // Emotional Authenticity: the song flows
            b.dread += perStar * legend.Stars(LegendClass.Justiciar);                               // Essence Sacrifice: the Void's cost, felt by the enemy
            b.nerve += perStar * legend.Stars(LegendClass.Sovereign);                               // Key of Attunement: one purpose, one carrier wave
            b.channel -= perStar * legend.Stars(LegendClass.Sovereign);
            b.interference -= 2f * perStar * legend.Stars(LegendClass.Seer);                        // Sufficient Precision: clean notes
            b.recon += legend.Stars(LegendClass.Seer) > 0 ? 1f : 0f;                                //  ... and a clear view
            b.wounded += perStar * legend.Stars(LegendClass.Chronicler);                            // Echoing Bonds: no one is left behind
            b.rally += 0.25f * legend.Stars(LegendClass.Chronicler);
            b.interference = Math.Max(0.1f, b.interference);
            b.channel = Math.Max(0.1f, b.channel);
            return b;
        }
    }

    private static readonly Boost None = new Boost();

    private sealed class Side
    {
        public BattleSide side;
        public bool attacker;
        public Side enemy;
        public BattleSpatialRules rules;
        public Action<Side, CombatSection, BattleSpatialCause, string> spatialEvent;
        public Func<int> beat;
        public Boost stack = None;
        public readonly Dictionary<CombatSection, Boost> leaders = new Dictionary<CombatSection, Boost>();
        /// <summary>The ground its own sections stand on, when not the field's (an attacker coming from its own hex).</summary>
        public BattleGround? footing;
        public float recon, rally, woundedShare, entrench;
        public float spiralingBelow;
        public BattleCommandCulture commandCulture;
        public bool initiative;
        public int misfiresLogged;
        public readonly Dictionary<CombatSection, float> startIntegrity = new Dictionary<CombatSection, float>();
        public float startComposure;

        // The Symphony (BattleResolver.Symphony.cs): its deck in play, the share of its fighting that goes on without
        // cards, what its cards did this measure, and the marks cards leave on sections of either side (shared).
        public Performance perf;
        public float ostinato = 1f;
        public float surge, crescendo, entrenched;
        public bool sure;
        public Dictionary<CombatSection, CardMarks> marks;

        public CardMarks Marks(CombatSection sec) => marks != null && sec != null && marks.TryGetValue(sec, out var k) ? k : null;
        /// <summary>The strongest Ward standing on a section now for steel (false) or magic (true), before growth and the Circle.</summary>
        public float WardOf(CombatSection sec, bool magical) => Marks(sec)?.wards
            .Where(g => g.raised <= beat() && g.expires >= beat() && (magical ? g.magical : g.physical) && g.source.Standing && !g.source.mindBroken && rules.Support(g.source, sec, g.range))
            .Select(g => g.amount).DefaultIfEmpty(0f).Max() ?? 0f;
        public float SupportOf(CombatSection sec, Func<CombatSection, float> amount) => side.Standing
            .Where(x => x.row == FormationRow.Support && !x.mindBroken && rules.Support(x, sec)).Sum(amount);
        public float BlindOf(CombatSection sec) { var k = Marks(sec); return k != null && k.blindLeft > 0 ? k.blind : 0f; }
        public float ExposeOf(CombatSection sec) { var k = Marks(sec); return k != null && k.exposeLeft > 0 ? k.expose : 0f; }

        // The commander's battle Composure (its own bar, not its real Composure).
        public BattleLegend conductor;
        public float bar, barMax;
        public bool conductorBroken;
        /// <summary>The Conductor was cut down, taken or evacuated (not a Mind Break): no maladaptive inheritance, only the loss.</summary>
        public bool conductorFallen;
        /// <summary>What the army inherits from a Mind Broken Conductor, and the line Composure at which a fearful one abandons the fight.</summary>
        public BattleConductorMaladaptation maladaptation;
        public float withdrawFloor;
        /// <summary>Section leaders who left the field when their section was cut down or taken (measure).</summary>
        public readonly Dictionary<BattleLegend, int> leftField = new Dictionary<BattleLegend, int>();

        public bool Conducted => conductor != null && !conductorBroken;
        public float Withdraw(CombatTuning t) => Math.Max(withdrawFloor, side.withdrawAt >= 0f ? side.withdrawAt : t.withdrawAt);
        public IEnumerable<CombatSection> Line => side.sections.Where(s => s.Standing && s.row != FormationRow.Support);
        public Boost Of(CombatSection sec) => sec != null && leaders.TryGetValue(sec, out var b) &&
            !(sec.leader != null && side.sections.Any(x => x.leader == sec.leader && x.eliteRole != BattleEliteRole.None && !x.Standing)) ? b : None;

        /// <summary>The whole stack's multiplier from its commander's battle Composure this measure.</summary>
        public float Nerve(CombatTuning t)
        {
            if (conductor == null) return 1f;
            var n = t.conductorNerve;
            if (n == null || n.Length < 4) return 1f;
            if (conductorBroken || bar <= 0f) return n[3];
            float share = barMax <= 0f ? 0f : bar / barMax;
            return share >= 0.5f ? n[0] : share >= 0.25f ? n[1] : n[2];
        }
    }

    /// <summary>
    /// Plays the battle. The sides' sections end in the battle's state (Integrity, Composure, Mind Broken, cut down, fled,
    /// captured), so a formation can carry its wounds into the next; clone the setup to keep it (<see cref="BattleSetup.Clone"/>).
    /// A side with a Symphony (<see cref="BattleSide.deck"/>) has its cards played for it by the resolver's performer, even
    /// one marked <see cref="BattleSide.manual"/> (use <see cref="Begin"/> to play it by hand).
    /// </summary>
    public static BattleReport Resolve(BattleSetup setup, CombatSettings settings = null)
    {
        if (setup.RequiresManual) throw new InvalidOperationException("This major, boss, decisive or Original Eight encounter must be performed manually.");
        var run = Begin(setup, settings, auto: true);
        return run.Finish();
    }

    /// <summary>
    /// Starts a battle to be played measure by measure (the micro layer): <see cref="BattleRun.BeginMeasure"/> draws each
    /// side's hand, the player plays cards for a <see cref="BattleSide.manual"/> side (<see cref="BattleRun.Play"/>), and
    /// <see cref="BattleRun.ResolveMeasure"/> plays the measure; <see cref="BattleRun.Finish"/> plays whatever is left and
    /// returns the report. The same rules as <see cref="Resolve"/>, so a battle played by hand and one resolved on its
    /// own weigh the same Symphony the same way.
    /// A battle with a <see cref="BattleSide.manual"/> side is forecast first (<paramref name="forecastRuns"/> auto-resolved
    /// copies; or pass a preview's with <see cref="BattleRun.Prediction"/>): if the player wins by hand what the forecast
    /// said would be lost, it is a Legendary Victory (<see cref="BattleVerdicts.Legendary"/>).
    /// </summary>
    public static BattleRun Begin(BattleSetup setup, CombatSettings settings = null, int forecastRuns = 20)
    {
        var prediction = setup.attacker.manual || setup.defender.manual ? Forecast(setup, settings, forecastRuns) : null;
        var run = Begin(setup, settings, auto: false);
        run.Prediction = prediction;
        return run;
    }

    private static BattleRun Begin(BattleSetup setup, CombatSettings settings, bool auto) => new BattleRun(setup, settings, auto);

    // ---------------------------------------------------------------------------------------------------------------
    // Preparation

    private static void Prepare(Side s, CombatTuning t)
    {
        foreach (var sec in s.side.sections.Where(x => x.Standing && x.row == FormationRow.Support))
        {
            s.recon += sec.recon;
            s.woundedShare += sec.woundedShare;
            s.entrench += sec.entrench;
        }
        // Section leaders lend their Greats to their own section; a legend in Surrender cannot lead.
        foreach (var sec in s.side.sections.Where(x => x.leader != null && x.leader.State != ComposureState.Surrender))
        {
            var b = Boost.Of(sec.leader, t.leaderPerStar);
            s.leaders[sec] = b;
            s.recon += b.recon * 0.5f;
        }
        var c = s.side.conductor;
        if (c != null && c.State != ComposureState.Surrender)
        {
            s.conductor = c;
            s.bar = s.barMax = c.BattleComposure(t);
            s.stack = Boost.Of(c, t.greatPerStar);
            s.recon += s.stack.recon;
            s.rally += s.stack.rally;
            s.woundedShare += s.stack.wounded;
            if (c.Stars(LegendClass.Architect) >= 2) s.entrench += 1f;
        }
        // A company fights harder with a legend it is attached to on the field (its commander, or its own leader).
        foreach (var sec in s.side.sections)
        {
            int stars = Attachment(s, sec);
            if (stars <= 0) continue;
            if (!s.leaders.TryGetValue(sec, out var b)) s.leaders[sec] = b = new Boost();
            float k = 1f + t.attachmentPerStar * stars;
            b.atk *= k;
            b.def *= k;
            b.nerve *= k;
        }
    }

    /// <summary>Stars of the strongest attachment to <paramref name="sec"/> among the legends able to fight on its side.</summary>
    private static int Attachment(Side s, CombatSection sec)
    {
        if (sec.bonds == null || sec.bonds.Count == 0) return 0;
        int Of(BattleLegend l) => l != null && l.State != ComposureState.Surrender && sec.bonds.TryGetValue(l.name, out int v) ? Math.Max(0, Math.Min(3, v)) : 0;
        return Math.Max(Of(s.conductor), Of(sec.leader));
    }

    /// <summary>Stars of <paramref name="legend"/>'s attachment to <paramref name="sec"/>.</summary>
    private static int Bond(CombatSection sec, BattleLegend legend) =>
        legend != null && sec.bonds != null && sec.bonds.TryGetValue(legend.name, out int v) ? Math.Max(0, Math.Min(3, v)) : 0;

    private static void Opening(BattleReport report, Side a, Side d, Battlefield field, GroundSpec ground, float entrench)
    {
        var parts = new List<string> { $"{a.side.name} attack {d.side.name} on {field.place} ({ground.name}, sixteen combat hexes)" };
        if (field.riverCrossing) parts.Add("across a river");
        if (field.height > 0.02f) parts.Add("uphill");
        if (field.settlement) parts.Add("against a settlement");
        if (entrench > 0f) parts.Add($"into {entrench:0} levels of earthworks");
        var notes = new List<string>();
        if (field.element != SpellBinding.Unattuned) notes.Add($"The land here sings {field.element}");
        if (field.leyline) notes.Add("a leyline runs beneath the field");
        if (field.sacred) notes.Add("the ground is Sacred");
        if (field.fallout > 0.05f) notes.Add($"Vibrational Fallout ({field.fallout:P0}) gnaws at every Pure Light being");
        if (field.dissonance > 0.3f) notes.Add("Dissonance hangs heavy, and chords come apart easily");
        string echo = EchoName(field.echo);
        if (echo != null) notes.Add($"it is the {echo}");
        var magic = $"Age {AgeMagic.Numeral(field.age)}: chords up to {AgeMagic.MaxTier(field.age)}{(AgeMagic.MajorNotes(field.age) ? string.Empty : ", mostly Minor Note magic")}";
        report.log.Add(string.Join(", ", parts) + "." + (notes.Count > 0 ? " " + string.Join("; ", notes) + "." : string.Empty) + " " + magic + ".");
        foreach (var s in new[] { a, d })
        {
            var c = s.side.conductor;
            if (c != null)
                report.log.Add(s.conductor == null
                    ? $"{c.name} has surrendered to Dissonance and cannot command; {s.side.name} fight without a commander."
                    : $"{c.name} ({c.Title}, {(c.leitmotif == SpellBinding.Unattuned ? "no" : c.leitmotif.ToString())} leitmotif, {c.State}) commands {s.side.name} at {s.side.tempo}.");
            foreach (var sec in s.side.sections.Where(x => x.leader != null))
                report.log.Add(s.leaders.ContainsKey(sec) ? $"{sec.leader.name} ({sec.leader.Title}) leads {sec.name}." : $"{sec.leader.name} cannot lead {sec.name}: surrendered to Dissonance.");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The line

    /// <summary>Marks the sections that take the field this Measure.</summary>
    private static void Muster(Side s)
    {
        foreach (var sec in s.side.sections) sec.committed = sec.Fighting;
    }

    private static CombatSection Pick(List<CombatSection> targets, CombatRandom rng)
    {
        float total = targets.Sum(x => Math.Max(0.1f, x.width));
        float r = rng.NextFloat() * total;
        foreach (var x in targets) { r -= Math.Max(0.1f, x.width); if (r <= 0f) return x; }
        return targets[targets.Count - 1];
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Spells

    /// <summary>A Legend conducts with the largest chord its Age plays reliably (Discordant Interference at most 10%).</summary>
    public static ChordTier ConductorTier(int age, CombatTuning t)
    {
        var tier = ChordTier.Unison;
        foreach (ChordTier x in Enum.GetValues(typeof(ChordTier)))
            if (AgeMagic.Playable(x, age) && AgeMagic.Interference(x, age, t) <= 0.1f) tier = x;
        return tier;
    }

    /// <summary>
    /// A caster sees its enemies' elements (Composure.md: a Soul Leitmotif glows with the color of its primary binding;
    /// Coherence-Binding Tissue glows too), so it always goes for the ones it lands hardest on. Among those, a directed
    /// formation (a conductor, or the better scouting) picks the front line's shakiest; otherwise chance decides.
    /// </summary>
    private static CombatSection Choose(List<CombatSection> targets, List<SpellBinding> roots, bool smart, CombatTuning t, CombatRandom rng)
    {
        if (roots.Count == 0) return Pick(targets, rng);
        float Mult(CombatSection x) => HarmonicCircle.Multiplier(HarmonicCircle.BestAgainst(roots, x.primary, t), x.primary, t);
        float best = targets.Max(Mult);
        var pool = targets.Where(x => Mult(x) >= best - 1e-4f).ToList();
        if (!smart) return Pick(pool, rng);
        return pool.OrderBy(x => x.row == FormationRow.Front ? 0 : 1).ThenBy(x => x.ComposureShare).First();
    }

    private static void Cast(Side s, CombatSection caster, string casterName, float basePotency, List<SpellBinding> roots, List<SpellBinding> harmony,
        bool soulWeaver, SpellBinding primary, bool fromBack, float dread, List<CombatSection> targets, bool smart, int m, Battlefield field,
        CombatTuning t, CombatRandom rng, Dictionary<CombatSection, Hit> hits, BattleReport report, bool? forcedMisfire = null, bool payEssence = true)
    {
        if (roots.Count == 0 || basePotency <= 0f) return;
        var target = targets.Count == 1 ? targets[0] : Choose(targets, roots, smart, t, rng);
        var root = HarmonicCircle.BestAgainst(roots, target.primary, t);
        harmony = harmony ?? new List<SpellBinding>();
        var tier = (ChordTier)Math.Min(3, harmony.Count);

        float p = basePotency * t.tierPotency[(int)tier];
        if (!s.side.sections.Contains(target)) p *= s.rules.TargetAttackFactor(s.attacker, target);
        if (soulWeaver)
        {
            p *= Tempo(s.side.tempo, m, t) * field.magicAccess;
            if (!AgeMagic.MajorNotes(field.age)) p *= t.minorNoteMagic;
        }
        var origin = Origin(s, caster);
        var local = origin == null ? null : s.rules.State.Terrain[origin.battleHex];
        bool channel = local == null ? field.leyline : local.leyline || local.harmonicChannel;
        p *= Lerp(t.incoherentPotency, t.coherentPotency, local?.coherence ?? field.coherence);
        if (channel) p *= t.leylinePotency;
        if (root == field.element) p *= t.landElement;
        if (root == SpellBinding.Cindergale && field.echo == 2) p *= t.crescendoFire;
        if (root == SpellBinding.Cindergale && field.echo == 4) p *= t.silenceFire;
        if (root == SpellBinding.Resonance && field.echo == 1) p *= t.resonanceEcho;
        float catalyst = origin == null ? 0f : s.SupportOf(origin, x => x.catalyst);
        p *= 1f + catalyst;
        if (origin != null) p *= s.rules.AttackFactor(s.attacker, origin);
        p *= 1f + s.crescendo;
        if (caster != null && caster.needsAccompaniment && catalyst <= 0f) p *= 0.6f;
        var boost = s.Of(caster);
        p *= s.stack.potency * boost.potency * s.Nerve(t);
        if (harmony.Contains(SpellBinding.Cindergale) && (caster == null || caster.ComposureShare >= t.focusFrom)) p *= t.focusNote;
        if (root != primary) p *= t.secondaryPotency;
        if (fromBack)
        {
            float loss = t.signalLoss * s.stack.channel * boost.channel;
            if (harmony.Contains(SpellBinding.Resonance)) loss *= t.resonanceChannel;
            if (harmony.Contains(SpellBinding.Flux)) loss *= t.fluxChannel;
            if (harmony.Contains(SpellBinding.Void)) loss *= t.voidChannel;
            if (channel) loss *= t.leylineChannel;
            p *= 1f - loss;
        }
        float spread = t.variance + (soulWeaver && s.side.tempo == SpellTempo.Polyrhythm ? t.polyrhythmSpread : 0f);
        p *= Math.Max(0f, 1f + (rng.NextFloat() * 2f - 1f) * spread);

        float essence = soulWeaver ? t.essenceCost[(int)tier] : t.essenceCost[0];
        bool misfire = forcedMisfire ?? rng.NextFloat() < Interference(s, caster, tier, soulWeaver, harmony, field, t);
        if (misfire)
        {
            p *= t.misfireLands;
            essence += t.backlash * p / Math.Max(0.01f, t.misfireLands);
            if (s.misfiresLogged < 2)
            {
                s.misfiresLogged++;
                report.log.Add($"Measure {m}: {casterName}'s {tier} slips into Discordant Interference; the working tears and the backlash rakes their own Composure.");
            }
        }
        // Essence Sacrifice: the spell is paid from the caster's Composure (the conductor's own strain).
        if (payEssence)
        {
            if (caster != null) { caster.composure -= essence; caster.casts++; if (misfire) caster.misfires++; }
            else s.bar -= essence * t.conductorEssence;
        }

        float mult = HarmonicCircle.Multiplier(root, target.primary, t);
        float sensitivity = Lerp(t.structureSpellTaken, t.pureLightSpellTaken, 1f - target.structure);
        var targetOwner = s.side.sections.Contains(target) ? s : s.enemy;
        bool barriers = targetOwner.side.tempo == SpellTempo.Legato && targetOwner.side.sections.Any(x => x.Standing && x.soulWeaver && x.Casts && !x.mindBroken && targetOwner.rules.Support(x, target));
        // Card Wards answer releases as Abjuration (BattleRun.Abjure); what is left here is the target's own resistance.
        float ward = target.ward + (barriers ? t.legatoWard : 0f) + (target.harmony.Contains(SpellBinding.Crystal) ? t.crystalWard : 0f);
        ward *= targetOwner.rules.DefenseFactor(targetOwner.attacker, target);
        if (target.mindBroken) ward *= t.mindBreakWard;
        ward = Math.Max(0f, Math.Min(0.9f, ward));
        float spellHits = p * t.spellHit * mult * sensitivity * (1f - ward);
        var hit = HitOf(hits, target);
        hit.friendlyFire |= targetOwner == s;
        hit.integrity += spellHits * t.integrityPerSpellHit;
        hit.composure += spellHits * t.composurePerSpellHit * dread * s.stack.dread * boost.dread * (soulWeaver && s.side.tempo == SpellTempo.Polyrhythm ? t.polyrhythmDread : 1f);

        if (root != SpellBinding.Unattuned && target.primary != SpellBinding.Unattuned)
        {
            string key = $"{root} on {target.primary}";
            report.matchups.TryGetValue(key, out int n);
            report.matchups[key] = n + 1;
            if (n == 0 && mult != 1f)
            {
                string why = HarmonicCircle.Why(root, target.primary);
                report.log.Add(mult > 1f
                    ? $"Measure {m}: {casterName} roots its chord in {root} against {target.name}: {why} (x{mult:0.##})."
                    : $"Measure {m}: {casterName}'s {root} breaks on {target.name}: {why} (x{mult:0.##}).");
            }
        }
        if (caster != null && harmony.Contains(SpellBinding.Strand) && caster.integrity > 0f)
        {
            float mended = Math.Min(caster.maxIntegrity - caster.integrity, Math.Min(caster.lost, caster.maxIntegrity * t.strandMending));
            caster.integrity += mended;
            caster.lost -= mended;
        }
    }

    /// <summary>
    /// The chance a chord collapses into Discordant Interference: the Age's command of its tier (a creature's instinct
    /// instead), capped at <paramref name="cap"/> (a card's own flicker), then the field's Dissonance, the Echo, the
    /// precision of Luminance and Crystal Minor Notes, the tempo, and the legends' Seer stars.
    /// </summary>
    private static float Interference(Side s, CombatSection caster, ChordTier tier, bool soulWeaver, List<SpellBinding> harmony, Battlefield field, CombatTuning t, float cap = 1f)
    {
        float chance = soulWeaver ? AgeMagic.Interference(tier, field.age, t) : t.instinctInterference;
        chance = Math.Min(chance, cap);
        var origin = Origin(s, caster);
        chance += (origin == null ? field.dissonance : s.rules.State.Terrain[origin.battleHex].dissonance) * t.dissonanceInterference + (field.echo == 3 ? t.dissonanceEcho : 0f);
        if (harmony.Contains(SpellBinding.Luminance)) chance *= t.precisionNote;
        if (harmony.Contains(SpellBinding.Crystal)) chance *= t.precisionNote;
        if (soulWeaver && s.side.tempo == SpellTempo.Ritardando) chance *= t.ritardandoInterference;
        if (soulWeaver && s.side.tempo == SpellTempo.Polyrhythm) chance *= t.polyrhythmInterference;
        return Math.Max(0f, Math.Min(0.95f, chance * s.stack.interference * s.Of(caster).interference));
    }

    private static float Tempo(SpellTempo tempo, int m, CombatTuning t)
    {
        int k = m - 1;
        switch (tempo)
        {
            case SpellTempo.Staccato: return Math.Max(t.staccatoFloor, t.staccatoOpen - t.staccatoStep * k);
            case SpellTempo.Legato: return t.legato;
            case SpellTempo.Accelerando: return Math.Min(t.accelerandoCap, t.accelerandoOpen + t.accelerandoStep * k);
            case SpellTempo.Ritardando: return Math.Max(t.ritardandoFloor, t.ritardandoOpen - t.ritardandoStep * k);
            default: return 1f;
        }
    }

    /// <summary>How a tempo shapes a formation's spells at measure <paramref name="measure"/> (1 is the first).</summary>
    public static float TempoCurve(SpellTempo tempo, int measure, CombatTuning t = null) => Tempo(tempo, measure, t ?? CombatTuning.Default);

    // ---------------------------------------------------------------------------------------------------------------
    // Steel

    /// <summary>
    /// Drilled steel: the section's attacks on its reachable targets (or <paramref name="pool"/>, a Skirmish's opponents),
    /// half on one and the rest spread by frontage. <paramref name="share"/> is the Beat's Attrition share;
    /// <paramref name="collided"/> applies the section's Impetus on the Measure its Charge or advance made contact.
    /// </summary>
    private static void Steel(Side s, int m, Battlefield field, GroundSpec ground, float assault, CombatTuning t, CombatRandom rng, Dictionary<CombatSection, Hit> hits,
        CombatSection only = null, CombatSection declaredTarget = null, List<CombatSection> pool = null, float share = 1f, bool collided = false)
    {
        float nerve = s.Nerve(t);
        foreach (var sec in s.side.sections.Where(x => x.Fighting && x.attack > 0f && x.row != FormationRow.Support && (only == null || x == only)).ToList())
        {
            bool missile = sec.row == FormationRow.Back;
            var targets = Reachable(s, sec);
            if (pool != null) targets = targets.Where(pool.Contains).ToList();
            if (declaredTarget != null) targets = targets.Where(x => x == declaredTarget).ToList();
            if (targets.Count == 0) continue;
            float a = sec.attack * sec.IntegrityShare * s.stack.atk * s.Of(sec).atk * nerve * assault * share;
            a *= (1f + s.surge) * (1f - s.BlindOf(sec));
            if (sec.mindBroken) a *= t.mindBreakAttack;
            a *= sec.On(s.rules.State.Terrain[sec.battleHex].ground)?.attack ?? 1f;
            a *= s.rules.AttackFactor(s.attacker, sec);
            if (missile)
            {
                a *= ground.missiles;
                if (field.weather > 1.2f) a *= t.stormMissiles;
                if (field.concealed) a *= t.coverMissiles;
            }
            else if (collided && ground.charges && !sec.mindBroken) a *= sec.charge;
            if (m == 1 && s.initiative) a *= t.initiative;
            a *= Math.Max(0f, 1f + (rng.NextFloat() * 2f - 1f) * t.variance);
            // Half the blow falls on one chosen section, the rest spreads along the line by frontage.
            var target = Pick(targets, rng);
            float frontage = targets.Sum(x => Math.Max(0.1f, x.width));
            foreach (var x in targets)
            {
                float portion = a * ((1f - t.focus) * Math.Max(0.1f, x.width) / frontage + (x == target ? t.focus : 0f));
                portion *= s.rules.TargetAttackFactor(s.attacker, x);
                var hit = HitOf(hits, x);
                hit.attacks += portion;
                hit.pierce += portion * sec.piercing;
                hit.dread += portion * sec.dread * s.stack.dread * s.Of(sec).dread;
                if (missile && s.sure) hit.sure += portion;
            }
        }
    }

    /// <summary>The Integrity and Composure a hit's attacks inflict after the section's own parries and armor (card Guards answer as Wards).</summary>
    private static (float integrity, float composure) Parry(CombatSection sec, Hit hit, Side owner, float defenderDefense, CombatTuning t)
    {
        if (hit.attacks <= 0f) return (0f, 0f);
        float parry = (owner.attacker ? sec.breakthrough : sec.defense) * sec.IntegrityShare * owner.stack.def * owner.Of(sec).def * owner.Nerve(t);
        parry *= sec.On(owner.rules.State.Terrain[sec.battleHex].ground)?.defense ?? 1f;
        if (!owner.attacker) parry *= defenderDefense;
        if (sec.mindBroken) parry *= t.mindBreakParry;
        parry *= owner.rules.DefenseFactor(owner.attacker, sec);
        float defended = Math.Min(hit.attacks - Math.Min(hit.attacks, hit.sure), parry);
        float struck = defended * t.defendedHit + (hit.attacks - defended) * t.undefendedHit;
        float armor = sec.armor * (sec.mindBroken ? t.mindBreakArmor : 1f) * owner.rules.DefenseFactor(owner.attacker, sec);
        float armored = Math.Max(0f, hit.attacks - hit.armorBypass);
        if (armored > 0f && hit.pierce / armored < armor) struck *= (hit.armorBypass + armored * t.armorBlock) / hit.attacks;
        return (struck * t.integrityPerHit, struck * t.composurePerHit * (hit.dread / hit.attacks));
    }

    private static Hit HitOf(Dictionary<CombatSection, Hit> hits, CombatSection target)
    {
        if (!hits.TryGetValue(target, out var hit)) hits[target] = hit = new Hit();
        return hit;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The toll and the states

    private static void Toll(Side s, int m, Battlefield field, CombatTuning t)
    {
        float rally = s.rally + (field.sacred ? t.sacredRally : 0f);
        foreach (var sec in s.side.sections.Where(x => x.Standing))
        {
            // Canon (Soliton.md, Pure Light.md): the more attuned to magic, the more the torn Loom is suffered.
            if (field.fallout > 0f && sec.PureLight && sec.niche != HarmonicNiche.Discordant)
            {
                float worn = Math.Min(Math.Max(0f, sec.integrity), field.fallout * t.falloutIntegrity);
                sec.composure -= field.fallout * t.falloutComposure;
                sec.integrity -= worn;
                sec.lost += worn;
            }
            // Cindergale's flame keeps burning (a Burn card): no armor stops it.
            var burning = s.Marks(sec);
            if (burning != null && burning.burnLeft > 0 && burning.burn > 0f)
            {
                float burnt = Math.Min(Math.Max(0f, sec.integrity), burning.burn);
                sec.integrity -= burnt;
                sec.lost += burnt;
            }
            if (field.echo == 4) sec.composure -= t.silenceChill;
            if (m >= t.fatigueFrom) sec.composure -= t.fatigue;
            if (sec.row == FormationRow.Back && s.enemy.side.Standing.Any(e => s.rules.Distance(e.battleHex, sec.battleHex) <= 1) &&
                (s.side.StanceBroken || !s.rules.Protected(sec, s.attacker, s.enemy.side.Standing.OrderBy(e => s.rules.Distance(e.battleHex, sec.battleHex)).First().battleHex))) sec.composure -= t.exposed;
            // A Mind Broken section sits at empty and climbs back from there.
            if (sec.mindBroken) sec.composure = Math.Max(0f, sec.composure);
            float lift = rally + s.Of(sec).rally + s.SupportOf(sec, x => x.rally);
            if (lift > 0f) sec.composure = Math.Min(sec.maxComposure, sec.composure + lift);
        }
    }

    private static void States(Side s, int m, CombatTuning t, BattleReport report)
    {
        foreach (var sec in s.side.sections.Where(x => x.Standing))
        {
            if (sec.integrity <= 0f && !sec.deathKnell)
            {
                sec.integrity = 0f;
                sec.destroyed = true;
                sec.committed = false;
                s.spatialEvent(s, sec, sec.eliteRole != BattleEliteRole.None ? BattleSpatialCause.EliteCasualty : BattleSpatialCause.AdjacentFormationCollapse, "A combatant fell; adjacent allies witness the collapse.");
                if (sec.row == FormationRow.Front && !s.side.Standing.Any(x => x.battleHex == sec.battleHex && x.row == FormationRow.Front))
                    s.spatialEvent(s, sec, BattleSpatialCause.CriticalPositionLost, "The last front element at this position fell.");
                if (sec.leader == s.conductor && sec.eliteRole != BattleEliteRole.None && s.Conducted) { s.bar = 0f; s.conductorFallen = true; }
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} is cut down.");
                LeaderLeaves(s, sec, m, report);
                continue;
            }
            if (!sec.mindBroken && (sec.eliteRole != BattleEliteRole.None ? sec.ComposureShare < s.spiralingBelow : sec.composure <= 0f))
            {
                sec.composure = Math.Max(0f, sec.composure);
                sec.mindBroken = true;
                sec.timesMindBroken++;
                s.spatialEvent(s, sec, BattleSpatialCause.MindBreak, "Composure failed; adjacent allies are shaken.");
                if (sec.leader == s.conductor && sec.eliteRole != BattleEliteRole.None && s.Conducted) s.bar = Math.Min(s.bar, s.barMax * s.spiralingBelow);
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} suffers a Mind Break: its Composure breaks, its guard drops{(sec.Casts ? " and its magic gutters out" : string.Empty)}.");
            }
            else if (sec.mindBroken && sec.ComposureShare >= t.steadyAt)
            {
                sec.mindBroken = false;
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} steadies and takes up the song again.");
            }
            if (TryCapture(s, sec, m, t, report))
            {
                s.spatialEvent(s, sec, sec.eliteRole != BattleEliteRole.None ? BattleSpatialCause.EliteCasualty : BattleSpatialCause.AdjacentFormationCollapse, "A combatant was subdued.");
                if (sec.leader == s.conductor && sec.eliteRole != BattleEliteRole.None && s.Conducted) { s.bar = 0f; s.conductorFallen = true; }
            }
        }
    }

    /// <summary>A Mind Broken section below <see cref="CombatTuning.captureBelow"/> Integrity is subdued and taken alive, if the enemy takes captives.</summary>
    private static bool TryCapture(Side s, CombatSection sec, int m, CombatTuning t, BattleReport report)
    {
        if (!s.enemy.side.takesCaptives || !sec.Standing || !sec.mindBroken) return false;
        // Mass surrender: a broken ordinary section held in contact gives up with bodies still whole (60% Integrity and no will).
        bool engaged = sec.eliteRole == BattleEliteRole.None && s.enemy.side.Standing.Any(x => x.battleHex == sec.battleHex);
        if (sec.eliteRole != BattleEliteRole.None ? sec.ComposureShare > .05f && !sec.eliteCheckmate : sec.integrity <= 0f || sec.IntegrityShare >= (engaged ? Math.Max(t.captureBelow, t.surrenderBelow) : t.captureBelow)) return false;
        if (!s.enemy.side.Standing.Any(x => s.rules.Distance(x.battleHex, sec.battleHex) <= 1)) return false;
        Capture(s, sec, m, report);
        return true;
    }

    private static void Capture(Side s, CombatSection sec, int m, BattleReport report)
    {
        sec.captured = true;
        sec.committed = false;
        report.captives.Add(new BattleCaptive
        {
            takenByAttacker = !s.attacker, from = s.side.name, name = sec.name, speciesId = sec.speciesId, specId = sec.specId, unitId = sec.unitId,
            individuals = sec.Alive, integrity = Math.Max(0f, sec.integrity), measure = m,
        });
        report.log.Add($"Measure {m}: {sec.name} of {s.side.name}, Mind Broken and bleeding, is subdued and taken alive by {s.enemy.side.name}.");
        if (sec.eliteRole == BattleEliteRole.None) LeaderLeaves(s, sec, m, report);
    }

    /// <summary>A legend never goes down with its section: when the section is cut down or taken, it slips away alone.</summary>
    private static void LeaderLeaves(Side s, CombatSection sec, int m, BattleReport report)
    {
        var leader = sec.leader;
        if (leader == null || !s.leaders.ContainsKey(sec) || s.leftField.ContainsKey(leader)) return;
        if (sec.eliteRole == BattleEliteRole.None && s.side.Standing.Any(x => x != sec && x.eliteRole != BattleEliteRole.None && x.leader == leader)) return;
        s.leftField[leader] = m;
        report.log.Add($"Measure {m}: {leader.name} is torn from {sec.name}'s fall and slips away from the field, alone.");
    }

    /// <summary>The Conductor's Mind Break comes at Spiraling, like every elite's; a fallen Conductor's bar is already empty. True when it newly broke.</summary>
    private static bool Conductor(Side s, int m, CombatTuning t, BattleReport report)
    {
        if (!s.Conducted || s.bar > s.barMax * s.spiralingBelow && !s.conductorFallen &&
            !s.side.sections.Any(x => x.eliteRole == BattleEliteRole.Conductor && x.leader == s.conductor && x.mindBroken)) return false;
        s.conductorBroken = true;
        if (s.conductorFallen) s.bar = 0f;
        // A Mind Break: the stack loses its commander's gifts and its heart, and fights on leaderless.
        s.rally -= s.stack.rally;
        s.recon -= s.stack.recon; s.woundedShare -= s.stack.wounded;
        s.stack = None;
        float shock = s.commandCulture == BattleCommandCulture.Disciplined ? .5f : s.commandCulture == BattleCommandCulture.Fanatical ? .75f : s.commandCulture == BattleCommandCulture.Decentralized ? .25f : 1f;
        foreach (var sec in s.side.sections.Where(x => x.Standing))
        { sec.composure -= t.conductorFallShock * shock; if (s.commandCulture == BattleCommandCulture.Fanatical) sec.attack *= 1.1f; }
        s.spatialEvent(s, s.side.sections.FirstOrDefault(x => x.eliteRole == BattleEliteRole.Conductor), BattleSpatialCause.ConductorDisrupted, "Command continuity failed.");
        report.log.Add(s.conductorFallen ? $"Measure {m}: {s.conductor.name} falls; {s.side.name} fight on without a commander."
            : $"Measure {m}: {s.conductor.name} suffers a Mind Break; the Soul Leitmotif goes dark and {s.side.name} fight on without a commander.");
        return true;
    }

    private static float LineShare(Side s)
    {
        float max = s.side.LineMaxIntegrity;
        return max <= 0f ? 0f : s.side.LineIntegrity / max;
    }

    /// <summary>A side is beaten when its line's Integrity gives out; wild creatures may leave sooner, when frightened.</summary>
    private static bool Done(Side s, CombatTuning t, out bool leftOnItsOwn)
    {
        leftOnItsOwn = false;
        if (!s.Line.Any() || LineShare(s) <= t.integrityBreak) return true;
        float withdraw = s.Withdraw(t);
        if (withdraw > 0f && s.side.LineMaxComposure > 0f && s.side.LineComposure / s.side.LineMaxComposure <= withdraw)
        {
            leftOnItsOwn = true;
            return true;
        }
        return false;
    }

    private static void Beaten(Side s, bool leftOnItsOwn, SideResult r, int m, CombatTuning t, BattleReport report)
    {
        r.beaten = !leftOnItsOwn;
        r.withdrew = leftOnItsOwn;
        float pace = s.enemy.side.sections.Where(x => x.Standing && x.row == FormationRow.Front).Select(x => x.speed).DefaultIfEmpty(0f).Max();
        bool shock = s.enemy.side.sections.Any(x => x.Standing && x.kind == SectionKind.Shock && !x.mindBroken);
        foreach (var sec in s.side.sections.Where(x => x.Standing).ToList())
        {
            // Total War's run-down: the faster pursuer catches the slower, and a Mind Broken section runs in no order at all.
            float run = t.pursuit * Math.Max(0f, pace - sec.speed + 1f) * (shock ? 2f : 1f) * (s.enemy.side.wild ? 0.5f : 1f) *
                        (sec.mindBroken ? 1f : 0.5f) * (leftOnItsOwn ? 0.5f : 1f);
            // A legend attached to the company covers its escape: most of it gets away, and none of it is lost in the rout.
            int attached = Attachment(s, sec);
            if (attached > 0) run *= Math.Max(0f, 1f - t.attachmentCover * attached);
            float worn = Math.Min(Math.Max(0f, sec.integrity) - (attached > 0 ? 1f : 0f), run);
            worn = Math.Max(0f, worn);
            sec.integrity -= worn;
            sec.lost += worn;
            sec.committed = false;
            if (sec.integrity <= 0f)
            {
                sec.integrity = 0f;
                if (sec.eliteRole == BattleEliteRole.None) { sec.destroyed = true; continue; }
                sec.deathKnell = true; sec.evacuated = sec.fled = true; continue;
            }
            if (attached > 0) { sec.fled = true; continue; }
            // The broken and the bleeding are caught, not killed, by a side that takes captives.
            if (!TryCapture(s, sec, m, t, report)) sec.fled = true;
        }
        report.log.Add(leftOnItsOwn ? $"{s.side.name} lose their nerve and scatter." : $"{s.side.name}'s line gives way; they scatter and are run down as they flee.");
    }

    private static SideBars Bars(Side s) => new SideBars
    {
        stance = s.side.stance, stanceStability = s.side.stanceStability, stanceStabilityMax = s.side.maxStanceStability,
        integrity = s.side.LineIntegrity, integrityMax = s.side.LineMaxIntegrity,
        composure = s.side.LineComposure, composureMax = s.side.LineMaxComposure,
        conductor = s.conductor == null ? 0f : Math.Max(0f, s.bar), conductorMax = s.barMax,
        mindBroken = s.side.sections.Count(x => x.Standing && x.mindBroken),
    };

    // ---------------------------------------------------------------------------------------------------------------
    // The reckoning

    private static void Tally(Side s, SideResult r, CombatTuning t, bool lost)
    {
        r.name = s.side.name;
        r.integrityBefore = s.startIntegrity.Values.Sum();
        r.integrityAfter = s.side.Integrity;
        r.composureBefore = s.startComposure;
        r.composureAfter = s.side.LineComposure;
        foreach (var sec in s.side.sections)
        {
            // What was lost before a section was taken is its dead and wounded; the rest went alive to the enemy.
            float share = Math.Max(0f, Math.Min(0.9f, t.baseWounded + s.woundedShare + s.Of(sec).wounded));
            float gone = Math.Max(0f, s.startIntegrity[sec] - Math.Max(0f, sec.integrity));
            sec.wounded = gone * share;
            sec.dead = gone - sec.wounded;
            if (sec.eliteRole != BattleEliteRole.None) { sec.dead = sec.permanentDeath ? s.startIntegrity[sec] : 0f; sec.wounded = sec.permanentDeath ? 0f : gone; }
            r.wounded += sec.wounded;
            r.dead += sec.dead;
            if (sec.mindBroken && sec.Standing) r.mindBroken++;
            if (sec.timesMindBroken > 0) r.everMindBroken++;
            if (sec.destroyed) r.destroyed++;
            if (sec.captured) { r.captured++; r.capturedIndividuals += sec.Alive; r.capturedIntegrity += Math.Max(0f, sec.integrity); }
            r.misfires += sec.misfires;
            r.casts += sec.casts;
        }
        if (s.conductor == null) return;
        r.conductorBroke = s.conductorBroken;
        r.conductorBefore = s.barMax;
        r.conductorAfter = Math.Max(0f, s.bar);
    }

    /// <summary>
    /// What the battle did to each legend of a side. A doomed side's legends (and a leader whose section fell) leave
    /// alone and go missing in action; losing and Mind Breaks strain their real Composure; victories pay Defiance, and
    /// surviving a defeat pays Acceptance.
    /// </summary>
    private static void Fates(Side s, BattleReport report, bool won, bool doomed, CombatTuning t)
    {
        bool lost = !won && (doomed || report.winner != 0 || s.attacker);
        void Add(BattleLegend legend, BattleRole role, string section, bool mindBroken, bool missing, float weight)
        {
            if (legend == null || report.legends.Any(f => f.name == legend.name)) return;
            var fate = new LegendBattleFate
            {
                name = legend.name, role = role, section = section, attacker = s.attacker, won = won,
                mindBroken = mindBroken, missing = missing, missingSevenths = missing ? t.missingSevenths : 0,
            };
            var body = s.side.sections.FirstOrDefault(x => x.eliteRole != BattleEliteRole.None && x.leader == legend);
            if (body != null) { fate.dead = body.permanentDeath; fate.captured = body.captured; fate.deathKnell = body.deathKnell; fate.missing = !fate.dead && !fate.captured && (body.evacuated || body.fled || missing); fate.missingSevenths = fate.missing ? t.missingSevenths : 0; }
            fate.strain = weight + (lost ? t.defeatStrain : 0f) + (mindBroken ? t.mindBreakStrain : 0f) + (missing ? t.missingStrain : 0f);
            // The companies it is attached to weigh on it: those cut down or taken, and those it left behind.
            foreach (var sec in s.side.sections)
            {
                int bond = Bond(sec, legend);
                if (bond <= 0) continue;
                if (sec.destroyed || sec.captured) fate.grief += t.attachmentLoss * bond;
                else if (missing) fate.grief += t.attachmentGrief * bond;
            }
            fate.strain += fate.grief;
            fate.parasiticStrain = s.side.sections.Where(x => x.leader == legend).Select(x => x.parasiticStrain).DefaultIfEmpty(0f).Max();
            fate.strain += fate.parasiticStrain;
            if (won) fate.fragments.Add(new FragmentAward(FragmentKind.Defiance, role == BattleRole.Commander ? t.victoryDefiance : t.leaderDefiance));
            else if (lost) fate.fragments.Add(new FragmentAward(FragmentKind.Acceptance, t.defeatAcceptance));
            if (mindBroken) fate.conditions.Add(new LegendCondition(LegendConditions.Traumatized, t.traumaSevenths, "battle"));
            if (missing) fate.conditions.Add(new LegendCondition(LegendConditions.Haunted, t.hauntedSevenths, "missing in action"));
            string how = won ? "won" : lost ? "lost" : "held off";
            fate.deed = role == BattleRole.Commander
                ? $"Commanded {s.side.name} in a battle {how}{(missing ? ", and walked home alone" : string.Empty)}"
                : $"Led {section} in a battle {how}{(missing ? ", and walked home alone" : string.Empty)}";
            report.legends.Add(fate);
        }

        if (s.conductor != null)
        {
            float weight = s.barMax <= 0f ? 0f : t.battleWeight * Math.Max(0f, s.barMax - Math.Max(0f, s.bar)) / s.barMax;
            Add(s.conductor, BattleRole.Commander, s.side.name, s.conductorBroken, doomed || s.leftField.ContainsKey(s.conductor), weight);
        }
        foreach (var sec in s.side.sections.Where(x => x.leader != null && s.leaders.ContainsKey(x)))
        {
            bool left = s.leftField.ContainsKey(sec.leader);
            Add(sec.leader, BattleRole.SectionLeader, sec.name, sec.timesMindBroken > 0, doomed || left, 0f);
        }
        foreach (var fate in report.legends.Where(f => f.attacker == s.attacker && f.missing))
            report.log.Add($"{fate.name} leaves the rest behind and is missing in action.");
    }

    /// <summary>Both sides' verdicts (<see cref="BattleVerdicts.Tier"/>; a held field is the defender's win); returns the attacker's.</summary>
    private static BattleOutcome Grade(BattleReport report, int end)
    {
        float aLoss = report.attacker.LossShare, dLoss = report.defender.LossShare;
        report.attacker.outcome = BattleVerdicts.Tier(end > 0, aLoss, dLoss);
        report.defender.outcome = BattleVerdicts.Tier(end <= 0, dLoss, aLoss);
        return report.attacker.outcome;
    }

    private static string Losses(SideResult r) =>
        $"{r.name} lose {r.LossShare:P0} of their Integrity ({r.dead:0} dead, {r.wounded:0} wounded" +
        (r.captured > 0 ? $", {r.captured} section{(r.captured == 1 ? "" : "s")} taken captive" : string.Empty) +
        (r.everMindBroken > 0 ? $", {r.everMindBroken} section{(r.everMindBroken == 1 ? "" : "s")} Mind Broken" : string.Empty) + ")" +
        (r.conductorBroke ? ", their commander Mind Broken" : string.Empty);

    private static string EchoName(int echo)
    {
        switch (echo)
        {
            case 1: return "Echo of Resonance";
            case 2: return "Echo of Crescendo";
            case 3: return "Echo of Dissonance";
            case 4: return "Echo of Silence";
            default: return null;
        }
    }

    private static float Lerp(float from, float to, float x) => from + (to - from) * Math.Max(0f, Math.Min(1f, x));

    // ---------------------------------------------------------------------------------------------------------------
    // Forecast

    /// <summary>
    /// Total War's balance of power: the battle played <paramref name="runs"/> times on fresh copies with different
    /// seeds. The setup itself is left untouched.
    /// </summary>
    public static BattleForecast Forecast(BattleSetup setup, CombatSettings settings = null, int runs = 25)
    {
        var f = new BattleForecast { runs = Math.Max(1, runs) };
        for (int i = 0; i < f.runs; i++)
        {
            var r = Begin(setup.Clone(unchecked(setup.seed * 31 + i * 7919 + 1)), settings, auto: true).Finish();
            if (r.winner > 0) f.attackerWins++; else if (r.winner < 0) f.defenderWins++; else f.stalemates++;
            f.attackerLoss += r.attacker.LossShare / f.runs;
            f.defenderLoss += r.defender.LossShare / f.runs;
            f.measures += r.measures / (float)f.runs;
        }
        return f;
    }
}

/// <summary>What <see cref="BattleResolver.Forecast"/> expects.</summary>
public sealed class BattleForecast
{
    public int runs, attackerWins, defenderWins, stalemates;
    /// <summary>Mean share of Integrity each side loses, and the mean length of the battle.</summary>
    public float attackerLoss, defenderLoss, measures;
    public float WinChance => runs == 0 ? 0f : attackerWins / (float)runs;
    /// <summary>0 the defender's, 1 the attacker's (a stalemate counts half).</summary>
    public float Balance => runs == 0 ? 0.5f : (attackerWins + 0.5f * stalemates) / runs;
}

/// <summary>A small deterministic generator (xorshift64*), so a seed replays the same battle.</summary>
public sealed class CombatRandom
{
    public CombatRandom Clone() => new CombatRandom(0) { _state = _state };
    private ulong _state;

    public CombatRandom(int seed)
    {
        _state = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xBF58476D1CE4E5B9UL;
        if (_state == 0) _state = 1;
        Next();
    }

    public ulong Next()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>In [0, 1).</summary>
    public float NextFloat() => (Next() >> 40) / (float)(1UL << 24);
}
