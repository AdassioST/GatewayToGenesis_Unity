using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The land's lasting emotional imprint on a cell (saved with the world, sparse: only cells that hold some).</summary>
[Serializable]
public class SufferingScar
{
    public int cell;
    public EmotionalRegister feelings = new EmotionalRegister();
}

/// <summary>What a settlement's life is made of this moment, for <see cref="WorldSuffering.SettlementFeelings"/>.</summary>
public struct SettlementMood
{
    /// <summary>Its strain (0-100) and what strained it last (danger, withering, pillage, or null: the daily grind).</summary>
    public float strain;
    public string harmedBy;
    /// <summary>The realm's happiness (0-100) and joy (0-1); -1 happiness when unknown.</summary>
    public float happiness, joy;
    /// <summary>Health pressures' levels (0-1, 0 when dormant).</summary>
    public float nutrition, disease, sanitation, exposure, harmonic;
    /// <summary>Its district's id (the Indulgent District's revels), or null.</summary>
    public string district;
    /// <summary>How much of the realm's feeling it carries (the Capital all of it, smaller places less).</summary>
    public float share;
    /// <summary>Its City Development (0-100: what its people have built) and the holidays the realm keeps.</summary>
    public float development;
    public int holidays;
}

/// <summary>
/// Emotional Residue on the land (vault: Eleos Bloom.md, Atonalis.md, Formless Masses.md), with no scene state (tested in
/// <c>WorldSufferingTests</c> and <c>EmotionalRegisterTests</c>). One register of sixteen notes (<see cref="Feeling"/>:
/// eight axes, each with a consonant, healthy face and a dissonant, wounded one) runs through everything:
///
/// - The land keeps a lasting imprint of what happened on it (<see cref="SufferingScar"/>): battles leave Dread and Pain
///   and a little Courage, hunts Pain and the hunters' Vitality, the scattered Tumult and Estrangement, grievances Shame,
///   a fallen settlement Tumult and Estrangement, festivals Joy and Love, kept holidays Devotion. Your settlements give off
///   their people's feelings every Seventh (<see cref="SettlementFeelings"/>): a thriving people imprints consonance, a
///   suffering one dissonance.
/// - Consonance soothes: only the wounds, less half the healthy feeling, press toward Formless Masses (<see cref="Apply"/>).
/// - Eleos Blooms eat cocktails (<see cref="EmotionalAlchemy"/>, <see cref="EmotionalEvolution"/>): they drink their
///   recipe out of the imprint (<see cref="Drink"/>), and healers transmute the wounded notes they drink into their
///   healthy pair ("nothing is wasted").
/// - Where the wounds (or the Loom's Dissonance) run too high and nothing drinks them, lingering Consciousness pools into
///   a <b>Formless Mass</b> that drinks every note; fed enough, it cocoons and hatches into the Path whose hunger its
///   feeding most resembles (<see cref="EmotionalProfile.Match"/>), most often a hybrid.
///
/// Every number is a proposal.
/// </summary>
public static class WorldSuffering
{
    /// <summary>What a battle leaves on its field: Dread, Pain for each creature or person slain there, and the Courage of those who stood.</summary>
    public const float BattleDread = 0.1f, PerSlain = 0.015f, BattleCourage = 0.04f;
    /// <summary>What a hunt leaves: a den hunted, a band's creatures taken, a predator's kill (Pain), and the hunters' Vitality (a meal won).</summary>
    public const float HuntPain = 0.06f, PerKill = 0.02f, HuntVitality = 0.03f;
    /// <summary>A party scattered: grief (Tumult) and the lost (Estrangement).</summary>
    public const float ScatterGrief = 0.15f;
    /// <summary>A settlement fallen to ruin: grief and a way of life gone.</summary>
    public const float RuinGrief = 0.35f;
    /// <summary>Taking from another's ground against its grievance (Shame); a people conscripted (Longing and Estrangement).</summary>
    public const float GrievanceShame = 0.05f, ConscriptionLonging = 0.04f;
    /// <summary>A festival: Joy and Love imprinted where it was held; a holiday kept: Devotion at the Capital.</summary>
    public const float FestivalJoy = 0.06f, HolidayDevotion = 0.04f;
    /// <summary>Share of its imprint a cell loses each Seventh by itself (the land slowly heals).</summary>
    public const float FadePerSeventh = 0.015f;
    /// <summary>A settlement gives off its feelings into its cell at this rate per Seventh (x their intensity, 0-1 each).</summary>
    public const float EmitPerSeventh = 0.012f;
    /// <summary>Share of its recipe a thriving bloom drinks from the imprint of its cells each Seventh (a healer drinks more).</summary>
    public const float BloomDrink = 0.05f, HealerDrink = 0.08f;
    /// <summary>Share of the wounded notes a healer drinks that it breathes back out as their healthy pair.</summary>
    public const float HealerTransmutes = 0.6f;
    /// <summary>How much of the healthy feeling offsets the wounds in the pressure toward Formless Masses.</summary>
    public const float ConsonanceSoothes = 0.5f;
    /// <summary>Pressure at which lingering Consciousness pools into a Formless Mass.</summary>
    public const float SpawnPressure = 0.6f;
    /// <summary>At most this many Formless Masses (and cocoons) pooled by the Loom alone; none within this many cells of another.</summary>
    public const int MassCap = 12, MassSpacing = 3;
    /// <summary>Sevenths a cell rests after a mass rose from it before another can.</summary>
    public const float SpawnCooldown = 14f;
    /// <summary>Share of a cell's imprint one mass drains per Seventh while it feeds there, and the Doubt it takes from the Loom's Dissonance (fed, not drained).</summary>
    public const float DrainShare = 0.12f, DissonanceFeed = 0.04f;
    /// <summary>Drained this much, a mass cocoons; the cocoon hatches after this many Sevenths.</summary>
    public const float CocoonAt = 0.8f, CocoonSevenths = 6f;
    /// <summary>A mass creeps at this share of its kind's walk.</summary>
    public const float MassStride = 0.5f;
    /// <summary>An Atonalis drinks this share of its hunger from the imprint where it stands each Seventh (the healthy notes it preys on turn into their wound).</summary>
    public const float AtonalisFeed = 0.05f;
    /// <summary>An imprint this heavy on one of your settlements' cells is told (once, until it eases).</summary>
    public const float WarnImprint = 0.4f;
    public const string MassId = "formless-mass";

    /// <summary>The Formless Mass as a species the battle can read: small, near-pure Dissonance, feeding like a scavenger.</summary>
    public static readonly SpeciesSpec Mass = new SpeciesSpec
    {
        id = MassId, name = "Formless Mass",
        description = "Lingering, collapsed Consciousness pooled into a slime of deep red and dark tones. It creeps toward suffering and drinks it; fed enough, it cocoons and an Atonalis hatches.",
        diet = CreatureDiet.Detritivore, subgroup = CreatureSubgroup.Erratic, size = CreatureSize.Small, structure = 0.05f,
        niche = HarmonicNiche.Discordant, identity = BandIdentity.FormlessMass, intelligence = BandIntelligence.Instinctive,
        habitat = CreatureHabitat.Land, pursuitTiles = 3, territoryTiles = 3, denVisitSevenths = 0f, groupMin = 1, groupMax = 3,
    };

    /// <summary>What hatches from a Dissonance cocoon: a Nascent Atonalis (vault: Atonalis.md, "Any Atonalis begins at Nascent").</summary>
    public static readonly ThreatSpec Hatchling = new ThreatSpec
    {
        id = "dissonance-cocoon", name = "Dissonance Cocoon", beingName = "Nascent Atonalis", beingSize = CreatureSize.Medium, beingStructure = 0.35f,
        beingDiet = CreatureDiet.Carnivore, beingSubgroup = CreatureSubgroup.Marauder, stance = EnemyStance.Hostile, bands = 0,
    };

    /// <summary>The Path a mass hatches into (the hunger its feeding most resembles; <see cref="EmotionalProfile.Match"/>).</summary>
    public static AtonalPath PathOf(EmotionalRegister fed) => EmotionalProfile.Match(fed).path;

    // ===== THE IMPRINT =====

    /// <summary>The imprint on a cell, or null.</summary>
    public static SufferingScar At(List<SufferingScar> scars, int cell) => scars?.FirstOrDefault(s => s.cell == cell);

    /// <summary>Adds a note to a cell's imprint (an imprint is opened where there was none).</summary>
    public static SufferingScar Add(List<SufferingScar> scars, int cell, Feeling feeling, float amount)
    {
        if (scars == null || cell < 0 || amount <= 0f) return null;
        var scar = At(scars, cell);
        if (scar == null) scars.Add(scar = new SufferingScar { cell = cell });
        scar.feelings.Add(feeling, amount);
        return scar;
    }

    /// <summary>Adds a whole register to a cell's imprint, scaled.</summary>
    public static SufferingScar Add(List<SufferingScar> scars, int cell, EmotionalRegister feelings, float scale)
    {
        if (scars == null || cell < 0 || feelings == null || scale <= 0f || feelings.Total * scale <= 1e-6f) return null;
        var scar = At(scars, cell);
        if (scar == null) scars.Add(scar = new SufferingScar { cell = cell });
        scar.feelings.Add(feelings, scale);
        return scar;
    }

    /// <summary>The land heals a little: each imprint fades by <see cref="FadePerSeventh"/>, and faint ones close.</summary>
    public static void Fade(List<SufferingScar> scars, float sevenths)
    {
        if (scars == null) return;
        float keep = (float)Math.Pow(1f - FadePerSeventh, Math.Max(0f, sevenths));
        foreach (var s in scars) s.feelings.Scale(keep);
        scars.RemoveAll(s => s.feelings.Total < 0.01f);
    }

    /// <summary>The suffering an imprint weighs: its wounds, less what its healthy feeling soothes (never below 0).</summary>
    public static float SufferingOf(EmotionalRegister imprint) =>
        imprint == null ? 0f : Math.Max(0f, imprint.Dissonance - ConsonanceSoothes * imprint.Consonance);

    /// <summary>How hard lingering Consciousness presses to pool here: its suffering, plus the Loom's Doubt.</summary>
    public static float Pressure(WorldTile t, float suffering) => t == null ? 0f : suffering + LoomDoubt(t);

    /// <summary>The Doubt the torn Loom gives off here (Dissonance past a third, the Fallout): not imprinted, but always there.</summary>
    public static float LoomDoubt(WorldTile t) => t == null ? 0f : Math.Max(0f, t.dissonance - 0.35f) * 1.5f + t.fallout * 0.5f;

    /// <summary>Copies each cell's imprint onto the map (<see cref="WorldTile.imprint"/>; <see cref="WorldTile.suffering"/> what it weighs).</summary>
    public static void Apply(WorldMap map, List<SufferingScar> scars)
    {
        if (map == null) return;
        foreach (var t in map.Tiles) { t.suffering = 0f; t.imprint?.Clear(); }
        if (scars == null) return;
        foreach (var s in scars)
        {
            if (s.cell < 0 || s.cell >= map.Count) continue;
            var t = map[s.cell];
            (t.imprint = t.imprint ?? new EmotionalRegister()).Add(s.feelings);
            t.suffering = SufferingOf(t.imprint);
        }
    }

    /// <summary>
    /// Cells where a Formless Mass pools now: pressure past <see cref="SpawnPressure"/>, walkable, not resting from the
    /// last one, and no mass within <see cref="MassSpacing"/> cells; the worst first.
    /// </summary>
    public static List<WorldTile> SpawnSites(WorldMap map, WorldGenSettings gen, Func<int, bool> resting, IEnumerable<HexCoord> masses)
    {
        var near = (masses ?? Enumerable.Empty<HexCoord>()).ToList();
        return map.Tiles.Where(t => MicroNavigation.Walkable(t, gen) && Pressure(t, t.suffering) >= SpawnPressure && !(resting?.Invoke(t.index) ?? false)
                                    && near.All(m => HexCoord.Distance(m, t.coord) > MassSpacing))
            .OrderByDescending(t => Pressure(t, t.suffering)).ThenBy(t => t.index).ToList();
    }

    // ===== WHAT A PEOPLE GIVES OFF =====

    /// <summary>
    /// A settlement's feelings this moment (each 0-1). Its wounds: strain by what strains it (danger: Dread; cut off:
    /// Estrangement and Longing; pillage: Pain and Shame; the daily grind: Fixation and Tumult), hunger and sickness (Pain,
    /// Dread), filth (Shame), a Loom out of tune (Doubt), despair when happiness is low (Tumult, Longing). Its health: joy
    /// and belonging when the people are happy, love when very happy, vitality when fed and well, pride in what they have
    /// built, devotion in the holidays they keep, courage when they stood their ground. The Indulgent District's revels
    /// give Longing, Joy and Vitality (desire). The realm's share of it is the settlement's <see cref="SettlementMood.share"/>.
    /// </summary>
    public static EmotionalRegister SettlementFeelings(SettlementMood m)
    {
        var r = new EmotionalRegister();
        float strain = Clamp01(m.strain / 100f);
        switch (m.harmedBy)
        {
            case WorldRuins.Danger: r.Add(Feeling.Dread, strain * 0.6f); r.Add(Feeling.Fixation, strain * 0.2f); r.Add(Feeling.Courage, (1f - strain) * 0.15f); break;
            case WorldRuins.Withering: r.Add(Feeling.Estrangement, strain * 0.5f); r.Add(Feeling.Longing, strain * 0.3f); break;
            case WorldRuins.Pillage: r.Add(Feeling.Pain, strain * 0.5f); r.Add(Feeling.Shame, strain * 0.3f); break;
            default: r.Add(Feeling.Fixation, strain * 0.4f); r.Add(Feeling.Tumult, strain * 0.3f); break;
        }
        float share = Clamp01(m.share);
        r.Add(Feeling.Pain, share * (0.35f * m.nutrition + 0.25f * m.disease + 0.1f * m.sanitation + 0.2f * m.exposure));
        r.Add(Feeling.Dread, share * 0.2f * m.disease);
        r.Add(Feeling.Shame, share * 0.15f * m.sanitation);
        r.Add(Feeling.Doubt, share * 0.3f * m.harmonic);
        if (m.happiness >= 0f)
        {
            if (m.happiness < 40f)
            {
                float despair = (40f - m.happiness) / 40f;
                r.Add(Feeling.Tumult, share * 0.4f * despair);
                r.Add(Feeling.Longing, share * 0.2f * despair);
            }
            r.Add(Feeling.Joy, share * 0.4f * Clamp01((m.happiness - 50f) / 50f));
            r.Add(Feeling.Belonging, share * 0.3f * Clamp01((m.happiness - 40f) / 60f));
            r.Add(Feeling.Love, share * 0.2f * Clamp01((m.happiness - 60f) / 40f));
            // Well fed and well: the body in tune (the more strained, the less of it).
            r.Add(Feeling.Vitality, share * 0.3f * Clamp01(1f - Math.Max(m.nutrition, m.disease)) * (1f - strain));
        }
        // Joy at its height is strong emotion: it tips into the tumult of a revel.
        if (m.joy > 0.7f) { float high = (m.joy - 0.7f) / 0.3f; r.Add(Feeling.Joy, share * 0.3f * high); r.Add(Feeling.Tumult, share * 0.1f * high); }
        r.Add(Feeling.Pride, 0.3f * Clamp01(m.development / 100f) * (1f - strain));
        r.Add(Feeling.Devotion, share * Math.Min(0.3f, 0.05f * Math.Max(0, m.holidays)));
        if (string.Equals(m.district, "indulgent", StringComparison.OrdinalIgnoreCase))
        {
            r.Add(Feeling.Longing, 0.3f); r.Add(Feeling.Joy, 0.25f); r.Add(Feeling.Vitality, 0.25f);
        }
        return r;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    // ===== WHAT A PLACE HOLDS =====

    /// <summary>A cell's note: what lives there now (<see cref="WorldTile.living"/>), what the land keeps (<see cref="WorldTile.imprint"/>), and the torn Loom's Doubt.</summary>
    public static float Feel(WorldTile t, Feeling f) =>
        t == null ? 0f : (t.living?[f] ?? 0f) + (t.imprint?[f] ?? 0f) + (f == Feeling.Doubt ? LoomDoubt(t) : 0f);

    /// <summary>A cell's whole register (living + imprint + the Loom's Doubt), as a new register.</summary>
    public static EmotionalRegister Feelings(WorldTile t)
    {
        var r = new EmotionalRegister();
        if (t == null) return r;
        r.Add(t.living);
        r.Add(t.imprint);
        r.Add(Feeling.Doubt, LoomDoubt(t));
        return r;
    }

    /// <summary>
    /// How richly a cell feeds a bloom whose recipe is <paramref name="flavors"/>: the notes of its recipe here, weighted
    /// by their share. 0 when it has no recipe.
    /// </summary>
    public static float Catalogued(WorldTile t, IList<FeelingWeight> flavors)
    {
        if (t == null || flavors == null || flavors.Count == 0) return 0f;
        return EmotionalAlchemy.Shares(flavors).Sum(l => l.weight * Feel(t, l.feeling));
    }

    // ===== DRINKING AND TRANSMUTING =====

    /// <summary>
    /// A thriving bloom drinks its recipe out of the imprint of its cells, each note by its share (a healer more, and
    /// breathing <see cref="HealerTransmutes"/> of the wounded notes it drinks back out as their healthy pair), at
    /// <paramref name="strength"/> (its vigor times its potency); returns how much it drank.
    /// </summary>
    public static float Drink(List<SufferingScar> scars, IEnumerable<int> cells, IList<FeelingWeight> recipe, bool healer, float strength, float sevenths)
    {
        if (scars == null || cells == null || recipe == null || recipe.Count == 0 || strength <= 0f || sevenths <= 0f) return 0f;
        var shares = EmotionalAlchemy.Shares(recipe);
        float top = shares.Count > 0 ? shares[0].weight : 1f;
        float rate = (healer ? HealerDrink : BloomDrink) * Math.Min(2.5f, strength) * sevenths, drunk = 0f;
        foreach (int cell in cells)
        {
            var scar = At(scars, cell);
            if (scar == null) continue;
            foreach (var l in shares)
            {
                float taken = scar.feelings.Drain(l.feeling, Math.Min(1f, rate * l.weight / Math.Max(1e-3f, top)));
                drunk += taken;
                if (healer && !EmotionalRegister.IsConsonant(l.feeling)) scar.feelings.Add(EmotionalRegister.Pair(l.feeling), taken * HealerTransmutes);
            }
        }
        return drunk;
    }

    /// <summary>
    /// An Atonalis feeds where it stands: it drinks its hunger (<see cref="EmotionalProfile.CocktailOf"/>) out of the imprint,
    /// and the healthy notes it preys on turn into their wound as it does (its presence amplifies pain that mirrors its own).
    /// Returns what it drank.
    /// </summary>
    public static float Prey(SufferingScar scar, AtonalPath path, AtonalPath hybrid, float sevenths)
    {
        if (scar == null || path == AtonalPath.None || sevenths <= 0f) return 0f;
        var hunger = EmotionalProfile.Of(path, hybrid);
        float top = EmotionalRegister.All.Max(f => hunger[f]), drunk = 0f;
        if (top <= 0f) return 0f;
        foreach (var f in EmotionalRegister.All.Where(f => hunger[f] > 0f))
        {
            float share = Math.Min(1f, AtonalisFeed * sevenths * hunger[f] / top);
            if (EmotionalRegister.IsConsonant(f)) drunk += EmotionalAlchemy.Transmute(scar.feelings, f, share);
            else drunk += scar.feelings.Drain(f, share);
        }
        return drunk;
    }

    // ===== WORDS AND COLOURS =====

    /// <summary>A note's colour on the meter (hex, no '#'): the wounds in bruised and burning tones, the healthy faces bright.</summary>
    public static string Colour(Feeling f)
    {
        switch (f)
        {
            case Feeling.Dread: return "7C92C0";
            case Feeling.Tumult: return "C24FB2";
            case Feeling.Fixation: return "C98A2E";
            case Feeling.Doubt: return "4FA8A0";
            case Feeling.Pain: return "C8323C";
            case Feeling.Estrangement: return "8C80B4";
            case Feeling.Shame: return "7A4E96";
            case Feeling.Longing: return "D0708A";
            case Feeling.Courage: return "E8C15A";
            case Feeling.Joy: return "FFD84D";
            case Feeling.Devotion: return "F2E6B8";
            case Feeling.Wonder: return "7FE3E0";
            case Feeling.Vitality: return "8EDB6A";
            case Feeling.Belonging: return "E3A86B";
            case Feeling.Pride: return "B8C8FF";
            default: return "FF9AB8";
        }
    }

    /// <summary>What a note is, in a few words.</summary>
    public static string Gloss(Feeling f)
    {
        switch (f)
        {
            case Feeling.Dread: return "fear, dread, the wait for the blow";
            case Feeling.Tumult: return "feeling that cannot hold its rhythm: the crash, despair";
            case Feeling.Fixation: return "the thought that will not let go";
            case Feeling.Doubt: return "reality unmoored, the Loom's vertigo";
            case Feeling.Pain: return "the body's suffering: wounds, hunger, sickness";
            case Feeling.Estrangement: return "a self coming apart: exile and loss";
            case Feeling.Shame: return "what was done against the will";
            case Feeling.Longing: return "intimacy denied: loneliness, desire unmet";
            case Feeling.Courage: return "fear faced and held";
            case Feeling.Joy: return "strong feeling that keeps its rhythm: delight";
            case Feeling.Devotion: return "focus given freely: the vow kept";
            case Feeling.Wonder: return "many truths held at once: awe and curiosity";
            case Feeling.Vitality: return "the body in tune: health, warmth, pleasure";
            case Feeling.Belonging: return "a whole self among its own";
            case Feeling.Pride: return "the will aligned with what it values";
            default: return "intimacy met: tenderness, being known";
        }
    }

    /// <summary>
    /// The emotional meter, a line per axis that holds anything (the heaviest first): the healthy face and the wound side
    /// by side, each with a bar of ticks against <paramref name="full"/> and its amount. Empty when it holds nothing.
    /// </summary>
    public static string Meter(EmotionalRegister r, float full = 1f, int cells = 10, float floor = 0.005f)
    {
        if (r == null) return string.Empty;
        string Bar(Feeling f)
        {
            int n = r[f] <= floor ? 0 : Math.Max(1, Math.Min(cells, (int)Math.Round(r[f] / Math.Max(1e-3f, full) * cells)));
            return $"<color=#{Colour(f)}>{f}</color> <color=#{Colour(f)}>{new string('|', n)}</color><color=#4A4038>{new string('|', cells - n)}</color> {r[f]:0.00}";
        }
        var lines = Enumerable.Range(0, EmotionalRegister.Axes)
            .Select(a => (c: EmotionalRegister.ConsonantOf(a), d: EmotionalRegister.DissonantOf(a)))
            .Where(p => r[p.c] > floor || r[p.d] > floor)
            .OrderByDescending(p => r[p.c] + r[p.d])
            .Select(p => $"{Bar(p.c)}   {Bar(p.d)}");
        return string.Join("\n", lines);
    }

    /// <summary>The compound feelings a register holds, in words ("Anemoia, Grief"), or null.</summary>
    public static string CompoundWords(EmotionalRegister r, float threshold = 0.7f)
    {
        var found = EmotionalAlchemy.Detect(r, threshold, 2);
        return found.Count == 0 ? null : string.Join(", ", found.Select(x => $"{x.compound.name} ({x.compound.gloss})"));
    }

    /// <summary>A hatchling's name for its Path (and its hybrid): "Carnalix", "Carnalix-Discant".</summary>
    public static string PathName(AtonalPath path, AtonalPath hybrid) => hybrid != AtonalPath.None && hybrid != path ? $"{path}-{hybrid}" : path.ToString();
}
