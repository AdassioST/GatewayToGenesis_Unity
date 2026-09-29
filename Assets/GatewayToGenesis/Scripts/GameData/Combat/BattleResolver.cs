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
public static class BattleResolver
{
    private sealed class Hit
    {
        public float integrity, composure, attacks, pierce, dread;
    }

    /// <summary>What a legend lends: multipliers on attack, parry, potency, dread; Composure harm divided by nerve.</summary>
    private sealed class Boost
    {
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
        public Boost stack = None;
        public readonly Dictionary<CombatSection, Boost> leaders = new Dictionary<CombatSection, Boost>();
        /// <summary>The ground its own sections stand on, when not the field's (an attacker coming from its own hex).</summary>
        public BattleGround? footing;
        public float recon, catalyst, rally, mending, woundedShare, entrench;
        public bool initiative;
        public int misfiresLogged;
        public readonly Dictionary<CombatSection, float> startIntegrity = new Dictionary<CombatSection, float>();
        public float startComposure;

        // The commander's battle Composure (its own bar, not its real Composure).
        public BattleLegend conductor;
        public float bar, barMax;
        public bool conductorBroken;
        /// <summary>Section leaders who left the field when their section was cut down or taken (measure).</summary>
        public readonly Dictionary<BattleLegend, int> leftField = new Dictionary<BattleLegend, int>();

        public bool Conducted => conductor != null && !conductorBroken;
        public float Withdraw(CombatTuning t) => side.withdrawAt >= 0f ? side.withdrawAt : t.withdrawAt;
        public IEnumerable<CombatSection> Line => side.sections.Where(s => s.Standing && s.row != FormationRow.Support);
        public Boost Of(CombatSection sec) => sec != null && leaders.TryGetValue(sec, out var b) ? b : None;

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
    /// </summary>
    public static BattleReport Resolve(BattleSetup setup, CombatSettings settings = null)
    {
        settings = settings ?? new CombatSettings();
        var t = settings.Tuning;
        var field = setup.field ?? new Battlefield();
        var ground = settings.Ground(field.ground);
        var rng = new CombatRandom(setup.seed);
        var report = new BattleReport { seed = setup.seed };

        var a = new Side { side = setup.attacker, attacker = true, footing = field.attackerGround };
        var d = new Side { side = setup.defender, attacker = false };
        a.enemy = d; d.enemy = a;
        var ownerOf = new Dictionary<CombatSection, Side>();
        foreach (var sec in a.side.sections) ownerOf[sec] = a;
        foreach (var sec in d.side.sections) ownerOf[sec] = d;
        foreach (var s in new[] { a, d })
        {
            foreach (var sec in s.side.sections)
            {
                sec.committed = false;
                sec.fled = false;
                sec.captured = false;
                sec.destroyed = sec.integrity <= 0f;
                sec.mindBroken = !sec.destroyed && sec.composure <= 0f;
                sec.lost = sec.dead = sec.wounded = 0f;
                sec.casts = sec.misfires = sec.timesMindBroken = 0;
                s.startIntegrity[sec] = Math.Max(0f, sec.integrity);
            }
            s.startComposure = s.side.LineComposure;
            Prepare(s, t);
        }

        float width = Math.Max(1f, ground.width - (field.concealed ? t.coverWidth : 0f));
        float entrenchLevels = Math.Min(t.maxEntrench, setup.defender.entrenchment + d.entrench);
        float defenderDefense = ground.defense * (1f + entrenchLevels * t.entrenchDefense) * (field.settlement ? t.settlementDefense : 1f) *
                                (1f + Math.Min(t.heightCap, field.height / 0.1f * t.heightDefense));
        // Coming downhill carries the attacker as higher ground holds the defender.
        float assault = ground.assault * (field.riverCrossing ? t.riverCrossing : 1f) * (1f + Math.Min(t.heightCap, field.downhill / 0.1f * t.heightDefense));

        Opening(report, a, d, field, ground, width, entrenchLevels);

        // The Overture: who reads the ground first.
        float reconA = a.recon, reconD = d.recon + (field.concealed ? 1f : 0f);
        if (reconA > reconD) a.initiative = true;
        else if (reconD > reconA) d.initiative = true;
        if (a.initiative) report.log.Add($"Overture: {a.side.name} read the ground first and strike before the enemy is ready.");
        if (d.initiative)
        {
            if (field.concealed)
            {
                foreach (var s in a.side.Standing) s.composure -= t.ambushShock * s.maxComposure;
                report.log.Add($"Overture: {a.side.name} walk into an ambush under the {(string.IsNullOrEmpty(field.cover) ? "cover" : field.cover.Replace('-', ' '))}; the line wavers before a blow is struck.");
            }
            else report.log.Add($"Overture: {d.side.name} saw them coming and strike first.");
        }
        report.timeline.Add(new BattleMeasure { measure = 0, attacker = Bars(a), defender = Bars(d) });

        int end = 0; // 1 attacker won, -1 defender won
        bool aLeft = false, dLeft = false;
        int m = 1;
        for (; m <= t.maxMeasures; m++)
        {
            Commit(a, width, m, report);
            Commit(d, width, m, report);
            var hits = new Dictionary<CombatSection, Hit>();

            Spells(a, m, field, t, rng, hits, report);
            Spells(d, m, field, t, rng, hits, report);
            Steel(a, m, field, ground, assault, t, rng, hits);
            Steel(d, m, field, ground, 1f, t, rng, hits);

            foreach (var pair in hits) Parry(pair.Key, pair.Value, ownerOf[pair.Key], ground, defenderDefense, t);
            foreach (var pair in hits)
            {
                var sec = pair.Key;
                var owner = ownerOf[sec];
                float before = Math.Max(0f, sec.integrity);
                float loss = Math.Min(before, pair.Value.integrity);
                sec.integrity -= pair.Value.integrity;
                sec.lost += loss;
                float fear = pair.Value.composure / (owner.stack.nerve * owner.Of(sec).nerve);
                sec.composure -= fear;
                if (owner.Conducted) owner.bar -= fear * t.conductorExposure;
                if (owner.mending > 0f && sec.integrity > 0f)
                {
                    float mended = Math.Min(sec.maxIntegrity - sec.integrity, loss * owner.mending);
                    sec.integrity += mended;
                    sec.lost -= mended;
                }
            }

            Toll(a, m, field, t);
            Toll(d, m, field, t);
            States(a, m, t, report);
            States(d, m, t, report);
            Conductor(a, m, t, report);
            Conductor(d, m, t, report);
            report.timeline.Add(new BattleMeasure { measure = m, attacker = Bars(a), defender = Bars(d) });

            bool aDone = Done(a, t, out aLeft), dDone = Done(d, t, out dLeft);
            if (aDone && dDone)
            {
                // Both give out together: the one with more of its line left holds.
                if (LineShare(a) > LineShare(d)) aDone = false; else dDone = false;
            }
            if (aDone) { end = -1; Beaten(a, aLeft, report.attacker, m, t, report); break; }
            if (dDone) { end = 1; Beaten(d, dLeft, report.defender, m, t, report); break; }
        }
        report.measures = Math.Min(m, t.maxMeasures);
        if (end == 0) report.log.Add($"After {t.maxMeasures} measures neither line has given way: {d.side.name} hold the field and {a.side.name} draw off.");

        report.winner = end;
        Tally(a, report.attacker, t, lost: end <= 0);
        Tally(d, report.defender, t, lost: end > 0);
        Fates(a, report, won: end > 0, doomed: end < 0 && !aLeft, t);
        Fates(d, report, won: end < 0, doomed: end > 0 && !dLeft, t);
        report.outcome = Grade(report, end);
        report.log.Add($"{BattleReport.Words(report.outcome)} for {a.side.name}. {Losses(report.attacker)}; {Losses(report.defender)}.");
        return report;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Preparation

    private static void Prepare(Side s, CombatTuning t)
    {
        foreach (var sec in s.side.sections.Where(x => x.Standing && x.row == FormationRow.Support))
        {
            s.recon += sec.recon;
            s.catalyst += sec.catalyst;
            s.rally += sec.rally;
            s.mending += sec.mending;
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

    private static void Opening(BattleReport report, Side a, Side d, Battlefield field, GroundSpec ground, float width, float entrench)
    {
        var parts = new List<string> { $"{a.side.name} attack {d.side.name} on {field.place} ({ground.name}, room for {width:0} sections abreast)" };
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

    private static void Commit(Side s, float width, int measure, BattleReport report)
    {
        float used = s.side.sections.Where(x => x.committed && x.Standing && x.row == FormationRow.Front).Sum(x => x.width);
        foreach (var sec in s.side.sections.Where(x => x.row == FormationRow.Front && x.Standing && !x.committed))
        {
            if (used > 0f && used + sec.width > width + 1e-3f) continue;
            sec.committed = true;
            used += sec.width;
            if (measure > 1) report.log.Add($"Measure {measure}: {sec.name} steps into the gap in {s.side.name}'s line.");
        }
    }

    private static int FrontCount(Side s) => s.side.sections.Count(x => x.row == FormationRow.Front && x.committed && x.Standing);

    /// <summary>What an enemy can reach: the committed front, then the back lane once the front is gone, then the support.</summary>
    private static List<CombatSection> Targets(Side enemy, bool reachesBack)
    {
        var front = enemy.side.sections.Where(x => x.row == FormationRow.Front && x.committed && x.Standing).ToList();
        var back = enemy.side.sections.Where(x => x.row == FormationRow.Back && x.Standing).ToList();
        if (reachesBack) { var both = front.Concat(back).ToList(); if (both.Count > 0) return both; }
        else if (front.Count > 0) return front;
        else if (back.Count > 0) return back;
        return enemy.side.sections.Where(x => x.row == FormationRow.Support && x.Standing).ToList();
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

    private static void Spells(Side s, int m, Battlefield field, CombatTuning t, CombatRandom rng, Dictionary<CombatSection, Hit> hits, BattleReport report)
    {
        bool smart = s.Conducted || s.recon > s.enemy.recon;
        // Composure is the magical reserve: a Mind Broken caster has nothing left to pay a spell with.
        foreach (var sec in s.side.sections.Where(x => x.Fighting && x.Casts && !x.mindBroken && x.row != FormationRow.Support).ToList())
        {
            var targets = Targets(s.enemy, sec.row == FormationRow.Back);
            if (targets.Count == 0) return;
            Cast(s, sec, sec.name, sec.potency * sec.IntegrityShare, sec.Roots.ToList(), sec.harmony, sec.soulWeaver, sec.primary,
                sec.row == FormationRow.Back, sec.dread, targets, smart, m, field, t, rng, hits, report);
        }
        var c = s.conductor;
        if (s.Conducted && c.leitmotif != SpellBinding.Unattuned)
        {
            var targets = Targets(s.enemy, true);
            if (targets.Count == 0) return;
            var roots = new List<SpellBinding> { c.leitmotif };
            // D13: Ornaments are the legend's from Age 0, but Ornamental Magic in the Symphony of War waits for its Age.
            if (AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, field.age)) roots.AddRange(c.ornaments.Where(o => o != c.leitmotif));
            var target = Choose(targets, roots, true, t, rng);
            var root = HarmonicCircle.BestAgainst(roots, target.primary, t);
            var tier = ConductorTier(field.age, t);
            var harmony = HarmonicCircle.Seven.Where(b => b != root).OrderByDescending(c.Score).ThenBy(b => (int)b).Take((int)tier).ToList();
            float potency = t.conductorPotency * c.Score(root) / 21f;
            Cast(s, null, c.name, potency, new List<SpellBinding> { root }, harmony, true, c.leitmotif, true, 1f, new List<CombatSection> { target }, true, m, field, t, rng, hits, report);
        }
    }

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
        CombatTuning t, CombatRandom rng, Dictionary<CombatSection, Hit> hits, BattleReport report)
    {
        if (roots.Count == 0 || basePotency <= 0f) return;
        var target = targets.Count == 1 ? targets[0] : Choose(targets, roots, smart, t, rng);
        var root = HarmonicCircle.BestAgainst(roots, target.primary, t);
        harmony = harmony ?? new List<SpellBinding>();
        var tier = (ChordTier)Math.Min(3, harmony.Count);

        float p = basePotency * t.tierPotency[(int)tier];
        if (soulWeaver)
        {
            p *= Tempo(s.side.tempo, m, t) * field.magicAccess;
            if (!AgeMagic.MajorNotes(field.age)) p *= t.minorNoteMagic;
        }
        p *= Lerp(t.incoherentPotency, t.coherentPotency, field.coherence);
        if (field.leyline) p *= t.leylinePotency;
        if (root == field.element) p *= t.landElement;
        if (root == SpellBinding.Cindergale && field.echo == 2) p *= t.crescendoFire;
        if (root == SpellBinding.Cindergale && field.echo == 4) p *= t.silenceFire;
        if (root == SpellBinding.Resonance && field.echo == 1) p *= t.resonanceEcho;
        p *= 1f + s.catalyst;
        if (caster != null && caster.needsAccompaniment && s.catalyst <= 0f) p *= 0.6f;
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
            if (field.leyline) loss *= t.leylineChannel;
            p *= 1f - loss;
        }
        float spread = t.variance + (soulWeaver && s.side.tempo == SpellTempo.Polyrhythm ? t.polyrhythmSpread : 0f);
        p *= Math.Max(0f, 1f + (rng.NextFloat() * 2f - 1f) * spread);

        float chance = soulWeaver ? AgeMagic.Interference(tier, field.age, t) : t.instinctInterference;
        chance += field.dissonance * t.dissonanceInterference + (field.echo == 3 ? t.dissonanceEcho : 0f);
        if (harmony.Contains(SpellBinding.Luminance)) chance *= t.precisionNote;
        if (harmony.Contains(SpellBinding.Crystal)) chance *= t.precisionNote;
        if (soulWeaver && s.side.tempo == SpellTempo.Ritardando) chance *= t.ritardandoInterference;
        if (soulWeaver && s.side.tempo == SpellTempo.Polyrhythm) chance *= t.polyrhythmInterference;
        chance = Math.Max(0f, Math.Min(0.95f, chance * s.stack.interference * boost.interference));

        float essence = soulWeaver ? t.essenceCost[(int)tier] : t.essenceCost[0];
        bool misfire = rng.NextFloat() < chance;
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
        if (caster != null) { caster.composure -= essence; caster.casts++; if (misfire) caster.misfires++; }
        else s.bar -= essence * t.conductorEssence;

        float mult = HarmonicCircle.Multiplier(root, target.primary, t);
        float sensitivity = Lerp(t.structureSpellTaken, t.pureLightSpellTaken, 1f - target.structure);
        bool barriers = s.enemy.side.tempo == SpellTempo.Legato && s.enemy.side.sections.Any(x => x.Standing && x.soulWeaver && x.Casts && !x.mindBroken);
        float ward = target.ward + (barriers ? t.legatoWard : 0f) +
                     (target.harmony.Contains(SpellBinding.Crystal) ? t.crystalWard : 0f);
        if (target.mindBroken) ward *= t.mindBreakWard;
        ward = Math.Max(0f, Math.Min(0.9f, ward));
        float spellHits = p * t.spellHit * mult * sensitivity * (1f - ward);
        var hit = HitOf(hits, target);
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

    private static void Steel(Side s, int m, Battlefield field, GroundSpec ground, float assault, CombatTuning t, CombatRandom rng, Dictionary<CombatSection, Hit> hits)
    {
        int flank = Math.Min(t.maxFlank, Math.Max(0, FrontCount(s) - FrontCount(s.enemy)));
        float nerve = s.Nerve(t);
        foreach (var sec in s.side.sections.Where(x => x.Fighting && x.attack > 0f && x.row != FormationRow.Support).ToList())
        {
            bool missile = sec.row == FormationRow.Back;
            var targets = Targets(s.enemy, false);
            if (targets.Count == 0) return;
            float a = sec.attack * sec.IntegrityShare * s.stack.atk * s.Of(sec).atk * nerve * assault;
            if (sec.mindBroken) a *= t.mindBreakAttack;
            a *= sec.On(s.footing ?? ground.ground)?.attack ?? 1f;
            if (missile)
            {
                a *= ground.missiles;
                if (field.weather > 1.2f) a *= t.stormMissiles;
                if (field.concealed) a *= t.coverMissiles;
            }
            else
            {
                if (m == 1 && ground.charges && !sec.mindBroken) a *= sec.charge;
                a *= 1f + flank * t.flankPerSection;
            }
            if (m == 1 && s.initiative) a *= t.initiative;
            a *= Math.Max(0f, 1f + (rng.NextFloat() * 2f - 1f) * t.variance);
            // Half the blow falls on one chosen section, the rest spreads along the line by frontage.
            var target = Pick(targets, rng);
            float frontage = targets.Sum(x => Math.Max(0.1f, x.width));
            foreach (var x in targets)
            {
                float share = a * ((1f - t.focus) * Math.Max(0.1f, x.width) / frontage + (x == target ? t.focus : 0f));
                var hit = HitOf(hits, x);
                hit.attacks += share;
                hit.pierce += share * sec.piercing;
                hit.dread += share * sec.dread * s.stack.dread * s.Of(sec).dread;
            }
        }
    }

    private static void Parry(CombatSection sec, Hit hit, Side owner, GroundSpec ground, float defenderDefense, CombatTuning t)
    {
        if (hit.attacks <= 0f) return;
        float parry = (owner.attacker ? sec.breakthrough : sec.defense) * sec.IntegrityShare * owner.stack.def * owner.Of(sec).def * owner.Nerve(t);
        parry *= sec.On(owner.footing ?? ground.ground)?.defense ?? 1f;
        if (!owner.attacker) parry *= defenderDefense;
        if (sec.mindBroken) parry *= t.mindBreakParry;
        float defended = Math.Min(hit.attacks, parry);
        float struck = defended * t.defendedHit + (hit.attacks - defended) * t.undefendedHit;
        float armor = sec.armor * (sec.mindBroken ? t.mindBreakArmor : 1f);
        if (hit.pierce / hit.attacks < armor) struck *= t.armorBlock;
        hit.integrity += struck * t.integrityPerHit;
        hit.composure += struck * t.composurePerHit * (hit.dread / hit.attacks);
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
        bool lineGone = !s.side.sections.Any(x => x.row == FormationRow.Front && x.Standing);
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
            if (field.echo == 4) sec.composure -= t.silenceChill;
            if (m >= t.fatigueFrom) sec.composure -= t.fatigue;
            if (lineGone && sec.row == FormationRow.Back) sec.composure -= t.exposed;
            // A Mind Broken section sits at empty and climbs back from there.
            if (sec.mindBroken) sec.composure = Math.Max(0f, sec.composure);
            float lift = rally + s.Of(sec).rally;
            if (lift > 0f) sec.composure = Math.Min(sec.maxComposure, sec.composure + lift);
        }
    }

    private static void States(Side s, int m, CombatTuning t, BattleReport report)
    {
        int fell = 0, broke = 0;
        foreach (var sec in s.side.sections.Where(x => x.Standing))
        {
            if (sec.integrity <= 0f)
            {
                sec.integrity = 0f;
                sec.destroyed = true;
                sec.committed = false;
                fell++;
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} is cut down.");
                LeaderLeaves(s, sec, m, report);
                continue;
            }
            if (!sec.mindBroken && sec.composure <= 0f)
            {
                sec.composure = 0f;
                sec.mindBroken = true;
                sec.timesMindBroken++;
                broke++;
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} suffers a Mind Break: its Composure breaks, its guard drops{(sec.Casts ? " and its magic gutters out" : string.Empty)}.");
            }
            else if (sec.mindBroken && sec.ComposureShare >= t.steadyAt)
            {
                sec.mindBroken = false;
                report.log.Add($"Measure {m}: {sec.name} of {s.side.name} steadies and takes up the song again.");
            }
            if (TryCapture(s, sec, m, t, report)) fell++;
        }
        if (fell + broke == 0) return;
        // Each fall shakes the rest of the line, after the measure (so the order of the sections does not matter).
        float shock = fell * t.fallenShock + broke * t.mindBreakShock;
        foreach (var sec in s.side.sections.Where(x => x.Standing)) sec.composure -= shock;
        if (s.Conducted) s.bar -= (fell + 0.5f * broke) * t.sectionLostStrain;
    }

    /// <summary>A Mind Broken section below <see cref="CombatTuning.captureBelow"/> Integrity is subdued and taken alive, if the enemy takes captives.</summary>
    private static bool TryCapture(Side s, CombatSection sec, int m, CombatTuning t, BattleReport report)
    {
        if (!s.enemy.side.takesCaptives || !sec.Standing || !sec.mindBroken || sec.integrity <= 0f || sec.IntegrityShare >= t.captureBelow) return false;
        sec.captured = true;
        sec.committed = false;
        report.captives.Add(new BattleCaptive
        {
            takenByAttacker = !s.attacker, from = s.side.name, name = sec.name, speciesId = sec.speciesId, specId = sec.specId, unitId = sec.unitId,
            individuals = sec.Alive, integrity = Math.Max(0f, sec.integrity), measure = m,
        });
        report.log.Add($"Measure {m}: {sec.name} of {s.side.name}, Mind Broken and bleeding, is subdued and taken alive by {s.enemy.side.name}.");
        LeaderLeaves(s, sec, m, report);
        return true;
    }

    /// <summary>A legend never goes down with its section: when the section is cut down or taken, it slips away alone.</summary>
    private static void LeaderLeaves(Side s, CombatSection sec, int m, BattleReport report)
    {
        var leader = sec.leader;
        if (leader == null || !s.leaders.ContainsKey(sec) || s.leftField.ContainsKey(leader)) return;
        s.leftField[leader] = m;
        report.log.Add($"Measure {m}: {leader.name} is torn from {sec.name}'s fall and slips away from the field, alone.");
    }

    private static void Conductor(Side s, int m, CombatTuning t, BattleReport report)
    {
        if (!s.Conducted || s.bar > 0f) return;
        s.conductorBroken = true;
        s.bar = 0f;
        // A Mind Break: the stack loses its commander's gifts and its heart, and fights on leaderless.
        s.rally -= s.stack.rally;
        s.stack = None;
        foreach (var sec in s.side.sections.Where(x => x.Standing)) sec.composure -= t.conductorFallShock;
        report.log.Add($"Measure {m}: {s.conductor.name} suffers a Mind Break; the Soul Leitmotif goes dark and {s.side.name} fight on without a commander.");
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
            if (sec.integrity <= 0f) { sec.integrity = 0f; sec.destroyed = true; continue; }
            if (attached > 0) { sec.fled = true; continue; }
            // The broken and the bleeding are caught, not killed, by a side that takes captives.
            if (!TryCapture(s, sec, m, t, report)) sec.fled = true;
        }
        report.log.Add(leftOnItsOwn ? $"{s.side.name} lose their nerve and scatter." : $"{s.side.name}'s line gives way; they scatter and are run down as they flee.");
    }

    private static SideBars Bars(Side s) => new SideBars
    {
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
            Add(s.conductor, BattleRole.Commander, s.side.name, s.conductorBroken, doomed, weight);
        }
        foreach (var sec in s.side.sections.Where(x => x.leader != null && s.leaders.ContainsKey(x)))
        {
            bool left = s.leftField.ContainsKey(sec.leader);
            Add(sec.leader, BattleRole.SectionLeader, sec.name, sec.timesMindBroken > 0, doomed || left, 0f);
        }
        foreach (var fate in report.legends.Where(f => f.attacker == s.attacker && f.missing))
            report.log.Add($"{fate.name} leaves the rest behind and is missing in action.");
    }

    private static BattleOutcome Grade(BattleReport report, int end)
    {
        float aLoss = report.attacker.LossShare, dLoss = report.defender.LossShare;
        if (end > 0)
        {
            if (aLoss < 0.15f && dLoss > 0.4f) return BattleOutcome.DecisiveVictory;
            if (aLoss >= 0.5f || aLoss > dLoss * 1.5f) return BattleOutcome.PyrrhicVictory;
            return BattleOutcome.Victory;
        }
        if (end < 0) return dLoss < 0.15f && aLoss > 0.4f ? BattleOutcome.Rout : BattleOutcome.Defeat;
        return BattleOutcome.Stalemate;
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
            var r = Resolve(setup.Clone(unchecked(setup.seed * 31 + i * 7919 + 1)), settings);
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
