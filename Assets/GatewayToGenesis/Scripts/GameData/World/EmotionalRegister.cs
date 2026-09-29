using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The sixteen notes of the Emotional Register: eight axes, one for each of the Eight-Born Paths in the vault's order
/// (Eight-Born Paths.md), each with a <b>dissonant</b> face (the wound the Path embodies and feeds on) and a
/// <b>consonant</b> face (the same feeling in its healthy state). The dissonant faces are 0-7 (the Paths I-VIII) and each
/// consonant face is its pair + 8. Emotional Residue (Eleos Bloom.md) is made of these notes; blooms, Formless Masses and
/// the Atonalis eat cocktails of them (<see cref="EmotionalAlchemy"/>). Append only: saved by index.
/// </summary>
public enum Feeling
{
    // ----- Dissonance: the wounds (I-VIII) -----
    /// <summary>Fear, dread, the wait for the blow (Anxithor).</summary>
    Dread,
    /// <summary>Emotion that cannot stabilize: the crash after the high, despair (Discant).</summary>
    Tumult,
    /// <summary>The thought that will not let go: obsession, toil without rest (Obsessian).</summary>
    Fixation,
    /// <summary>Reality unmoored: confusion, the vertigo of a torn Loom (Signath).</summary>
    Doubt,
    /// <summary>The body's suffering: wounds, hunger, sickness, the slaughtered (Carnalix).</summary>
    Pain,
    /// <summary>A self coming apart: exile, the lost and displaced (Animach).</summary>
    Estrangement,
    /// <summary>What was done against the will: transgression, humiliation (Violux).</summary>
    Shame,
    /// <summary>Intimacy denied: loneliness, desire unmet (Erosyx).</summary>
    Longing,
    // ----- Consonance: the same feelings in their healthy state (pairs of 0-7) -----
    /// <summary>Fear faced and held (Dread's pair).</summary>
    Courage,
    /// <summary>Strong feeling that holds its rhythm: elation, delight (Tumult's pair).</summary>
    Joy,
    /// <summary>Focus given freely: dedication, the vow kept (Fixation's pair).</summary>
    Devotion,
    /// <summary>Many truths held at once without falling: awe, curiosity (Doubt's pair).</summary>
    Wonder,
    /// <summary>The body in tune: health, warmth, pleasure, being fed (Pain's pair).</summary>
    Vitality,
    /// <summary>A whole self among its own: home, kinship (Estrangement's pair).</summary>
    Belonging,
    /// <summary>The will aligned with what it values: dignity, honour (Shame's pair).</summary>
    Pride,
    /// <summary>Intimacy met: tenderness, being known (Longing's pair).</summary>
    Love,
}

/// <summary>Consonance (a feeling's healthy state) or Dissonance (its wound).</summary>
public enum Polarity { Dissonance, Consonance }

/// <summary>A note and how much of it (a recipe line of a cocktail, a bloom's palate, a Path's hunger).</summary>
[Serializable]
public class FeelingWeight
{
    public Feeling feeling;
    public float weight = 1f;
}

/// <summary>
/// An emotional meter: how much of each of the sixteen notes a place holds, a people gives off, or a Formless Mass has
/// drunk. The land keeps it as its lasting imprint (<see cref="SufferingScar"/>), settlements give it off
/// (<see cref="Settlement.feelings"/>), blooms and Atonalis eat cocktails of it (<see cref="EmotionalAlchemy"/>).
/// </summary>
[Serializable]
public class EmotionalRegister
{
    public float dread, tumult, fixation, doubt, pain, estrangement, shame, longing;
    // The consonant faces, added after the register first shipped: older saves load them as zero.
    [SaveOptionalField] public float courage, joy, devotion, wonder, vitality, belonging, pride, love;

    public const int Count = 16, Axes = 8;
    public static readonly Feeling[] All = (Feeling[])Enum.GetValues(typeof(Feeling));
    public static readonly Feeling[] Dissonant = All.Where(f => (int)f < Axes).ToArray();
    public static readonly Feeling[] Consonant = All.Where(f => (int)f >= Axes).ToArray();

    /// <summary>The axis (0-7: the Path's) a note lies on.</summary>
    public static int Axis(Feeling f) => (int)f % Axes;
    public static Polarity PolarityOf(Feeling f) => (int)f >= Axes ? Polarity.Consonance : Polarity.Dissonance;
    public static bool IsConsonant(Feeling f) => (int)f >= Axes;
    /// <summary>The same feeling in the other state (Dread's pair is Courage, Courage's is Dread).</summary>
    public static Feeling Pair(Feeling f) => (Feeling)(((int)f + Axes) % Count);
    /// <summary>The healthy and the wounded face of an axis.</summary>
    public static Feeling ConsonantOf(int axis) => (Feeling)(axis % Axes + Axes);
    public static Feeling DissonantOf(int axis) => (Feeling)(axis % Axes);

    public float this[Feeling f]
    {
        get
        {
            switch (f)
            {
                case Feeling.Dread: return dread;
                case Feeling.Tumult: return tumult;
                case Feeling.Fixation: return fixation;
                case Feeling.Doubt: return doubt;
                case Feeling.Pain: return pain;
                case Feeling.Estrangement: return estrangement;
                case Feeling.Shame: return shame;
                case Feeling.Longing: return longing;
                case Feeling.Courage: return courage;
                case Feeling.Joy: return joy;
                case Feeling.Devotion: return devotion;
                case Feeling.Wonder: return wonder;
                case Feeling.Vitality: return vitality;
                case Feeling.Belonging: return belonging;
                case Feeling.Pride: return pride;
                default: return love;
            }
        }
        set
        {
            value = Math.Max(0f, value);
            switch (f)
            {
                case Feeling.Dread: dread = value; break;
                case Feeling.Tumult: tumult = value; break;
                case Feeling.Fixation: fixation = value; break;
                case Feeling.Doubt: doubt = value; break;
                case Feeling.Pain: pain = value; break;
                case Feeling.Estrangement: estrangement = value; break;
                case Feeling.Shame: shame = value; break;
                case Feeling.Longing: longing = value; break;
                case Feeling.Courage: courage = value; break;
                case Feeling.Joy: joy = value; break;
                case Feeling.Devotion: devotion = value; break;
                case Feeling.Wonder: wonder = value; break;
                case Feeling.Vitality: vitality = value; break;
                case Feeling.Belonging: belonging = value; break;
                case Feeling.Pride: pride = value; break;
                default: love = value; break;
            }
        }
    }

    public float Total { get { float t = 0f; foreach (var f in All) t += this[f]; return t; } }
    /// <summary>What it holds of the wounds, and of the healthy states.</summary>
    public float Dissonance { get { float t = 0f; foreach (var f in Dissonant) t += this[f]; return t; } }
    public float Consonance { get { float t = 0f; foreach (var f in Consonant) t += this[f]; return t; } }
    /// <summary>Consonance's share of all it holds (0.5 with nothing), the register's harmony.</summary>
    public float Harmony { get { float c = Consonance, d = Dissonance; return c + d <= 1e-6f ? 0.5f : c / (c + d); } }

    public void Add(Feeling f, float amount) => this[f] = this[f] + Math.Max(0f, amount);

    /// <summary>Adds <paramref name="scale"/> of another register.</summary>
    public void Add(EmotionalRegister other, float scale = 1f)
    {
        if (other == null || scale <= 0f) return;
        foreach (var f in All) this[f] = this[f] + other[f] * scale;
    }

    public void Clear() { foreach (var f in All) this[f] = 0f; }

    public void Scale(float s) { foreach (var f in All) this[f] = this[f] * s; }

    /// <summary>Takes <paramref name="share"/> (0-1) of every note out of this register into <paramref name="into"/>.</summary>
    public void Drain(float share, EmotionalRegister into)
    {
        share = Math.Max(0f, Math.Min(1f, share));
        foreach (var f in All)
        {
            float taken = this[f] * share;
            this[f] = this[f] - taken;
            if (into != null) into[f] = into[f] + taken;
        }
    }

    /// <summary>Takes <paramref name="share"/> (0-1) of one note out; returns what it took.</summary>
    public float Drain(Feeling f, float share)
    {
        float taken = this[f] * Math.Max(0f, Math.Min(1f, share));
        this[f] = this[f] - taken;
        return taken;
    }

    /// <summary>The note that weighs most (ties in the enum's order).</summary>
    public Feeling Dominant
    {
        get
        {
            var best = Feeling.Dread;
            foreach (var f in All) if (this[f] > this[best] + 1e-6f) best = f;
            return best;
        }
    }

    /// <summary>The strongest of its dissonant notes (the wound it holds most).</summary>
    public Feeling DominantWound
    {
        get
        {
            var best = Feeling.Dread;
            foreach (var f in Dissonant) if (this[f] > this[best] + 1e-6f) best = f;
            return best;
        }
    }

    /// <summary>A note's share of the whole (0 when it holds nothing).</summary>
    public float Share(Feeling f)
    {
        float total = Total;
        return total <= 1e-6f ? 0f : this[f] / total;
    }

    /// <summary>The strongest notes, strongest first, as "Pain 62%, Joy 21%" (at most <paramref name="top"/>).</summary>
    public string Words(int top = 3)
    {
        float total = Total;
        if (total <= 1e-4f) return "nothing to speak of";
        return string.Join(", ", All.Where(f => this[f] > 1e-4f).OrderByDescending(f => this[f]).ThenBy(f => (int)f).Take(top)
            .Select(f => $"{f} {Math.Round(this[f] / total * 100f, MidpointRounding.AwayFromZero):0}%"));
    }

    public EmotionalRegister Copy()
    {
        var r = new EmotionalRegister();
        r.Add(this);
        return r;
    }

    /// <summary>A register from recipe lines (each note's weight added).</summary>
    public static EmotionalRegister Of(IEnumerable<FeelingWeight> lines)
    {
        var r = new EmotionalRegister();
        foreach (var l in lines ?? Enumerable.Empty<FeelingWeight>()) if (l != null) r.Add(l.feeling, l.weight);
        return r;
    }
}

/// <summary>
/// What each Path eats (vault: Eight-Born Paths.md, "Feeds on"; Atonalis.md: an Atonalis "feeds on the echoing
/// Resonance of its own wound manifesting in others", and hybrids feed on "tears combined with despair"): a cocktail of
/// notes, mostly its own wound, often a healthy note it preys on (a Discant on joy, an Erosyx and the pleasure parasites on
/// love and the body's pleasure), and a little of its neighbours'. A Formless Mass hatches into the Path whose hunger its
/// feeding most resembles; two close Paths make a hybrid, and because the land's feelings are always mixed, hybrids are
/// the common birth and purists the rare one. Every number is a proposal.
/// </summary>
public static class EmotionalProfile
{
    private static FeelingWeight W(Feeling f, float w) => new FeelingWeight { feeling = f, weight = w };

    private static readonly Dictionary<AtonalPath, FeelingWeight[]> Hunger = new Dictionary<AtonalPath, FeelingWeight[]>
    {
        [AtonalPath.Anxithor] = new[] { W(Feeling.Dread, 50), W(Feeling.Courage, 15), W(Feeling.Pain, 10), W(Feeling.Doubt, 10), W(Feeling.Fixation, 10), W(Feeling.Estrangement, 5) },
        [AtonalPath.Discant] = new[] { W(Feeling.Tumult, 45), W(Feeling.Joy, 20), W(Feeling.Longing, 10), W(Feeling.Estrangement, 10), W(Feeling.Pain, 10), W(Feeling.Love, 5) },
        [AtonalPath.Obsessian] = new[] { W(Feeling.Fixation, 50), W(Feeling.Devotion, 20), W(Feeling.Dread, 10), W(Feeling.Doubt, 10), W(Feeling.Shame, 10) },
        [AtonalPath.Signath] = new[] { W(Feeling.Doubt, 50), W(Feeling.Wonder, 20), W(Feeling.Estrangement, 15), W(Feeling.Dread, 10), W(Feeling.Fixation, 5) },
        [AtonalPath.Carnalix] = new[] { W(Feeling.Pain, 55), W(Feeling.Vitality, 20), W(Feeling.Dread, 10), W(Feeling.Tumult, 10), W(Feeling.Longing, 5) },
        [AtonalPath.Animach] = new[] { W(Feeling.Estrangement, 50), W(Feeling.Belonging, 20), W(Feeling.Doubt, 15), W(Feeling.Shame, 10), W(Feeling.Longing, 5) },
        [AtonalPath.Violux] = new[] { W(Feeling.Shame, 50), W(Feeling.Pride, 15), W(Feeling.Fixation, 15), W(Feeling.Longing, 10), W(Feeling.Tumult, 10) },
        [AtonalPath.Erosyx] = new[] { W(Feeling.Longing, 40), W(Feeling.Love, 20), W(Feeling.Vitality, 15), W(Feeling.Shame, 15), W(Feeling.Estrangement, 10) },
    };

    /// <summary>
    /// A second Path whose hunger the feeding resembles at least this much (cosine), and at least <see cref="HybridShare"/> as
    /// much as the best, makes a hybrid. The land's feelings are always mixed, so hybrids are the common birth; a feeding that
    /// leans on one axis makes a purist.
    /// </summary>
    public const float HybridFloor = 0.27f, HybridShare = 0.45f;

    public static AtonalPath PathOf(Feeling f) => (AtonalPath)(EmotionalRegister.Axis(f) + 1);

    /// <summary>The dissonant note a Path embodies.</summary>
    public static Feeling FeelingOf(AtonalPath p) => p == AtonalPath.None ? Feeling.Dread : (Feeling)((int)p - 1);

    /// <summary>A Path's hunger (its cocktail) as recipe lines (empty for None).</summary>
    public static IReadOnlyList<FeelingWeight> CocktailOf(AtonalPath path) => Hunger.TryGetValue(path, out var c) ? c : Array.Empty<FeelingWeight>();

    /// <summary>A Path's hunger as a register (all zero for None).</summary>
    public static EmotionalRegister Of(AtonalPath path) => EmotionalRegister.Of(CocktailOf(path));

    /// <summary>A hybrid's hunger: its Path's and a lighter share of its second Path's.</summary>
    public static EmotionalRegister Of(AtonalPath path, AtonalPath hybrid)
    {
        var r = Of(path);
        if (hybrid != AtonalPath.None && hybrid != path) r.Add(Of(hybrid), 0.6f);
        return r;
    }

    /// <summary>How much a register resembles a Path's hunger (cosine, 0-1).</summary>
    public static float Resemblance(EmotionalRegister fed, AtonalPath path) => EmotionalAlchemy.Cosine(fed, Of(path));

    /// <summary>
    /// The Path a feeding makes (the most resembled hunger) and a hybrid second Path when one resonates too
    /// (<see cref="HybridFloor"/>, <see cref="HybridShare"/>; None otherwise: a purist). Nothing fed: Anxithor, the commonest wound.
    /// </summary>
    public static (AtonalPath path, AtonalPath hybrid) Match(EmotionalRegister fed)
    {
        if (fed == null || fed.Total <= 1e-6f) return (AtonalPath.Anxithor, AtonalPath.None);
        var ranked = Enumerable.Range(1, 8).Select(i => (AtonalPath)i).Select(p => (p, r: Resemblance(fed, p)))
            .OrderByDescending(x => x.r).ThenBy(x => (int)x.p).ToList();
        var second = ranked[1];
        return (ranked[0].p, second.r >= HybridFloor && second.r >= ranked[0].r * HybridShare ? second.p : AtonalPath.None);
    }
}
