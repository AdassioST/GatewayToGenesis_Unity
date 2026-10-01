using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A named compound feeling: a cocktail of notes with a character of its own (Anemoia, Saudade, Catharsis...).</summary>
public sealed class Compound
{
    public string id, name, gloss;
    public FeelingWeight[] recipe;
}

/// <summary>
/// Emotional alchemy, with no scene state (tested in <c>EmotionalAlchemyTests</c>). Feelings combine: every eater of
/// feeling (an Eleos Bloom, a Formless Mass, an Atonalis) eats a <b>cocktail</b>, a recipe of notes in proportion, and
/// every place holds a mix that some cocktails fit and others do not. The rules:
///
/// - <b>Servings.</b> A cocktail made exactly as its recipe asks can be poured as many times as its scarcest note allows
///   (<see cref="Servings"/>, a strict eater's food); a loose eater takes any of its notes where it finds them
///   (<see cref="Loose"/>). How exactly a place's mix matches a recipe is its <see cref="Fit"/> (0-1).
/// - <b>Compounds.</b> Some mixes have names of their own (<see cref="Compounds"/>): Anemoia is longing for a time never
///   known (Longing, Wonder, Estrangement, a little Love); Grief is love that stays (Tumult, Love, Estrangement).
///   <see cref="Detect"/> reads which a register holds.
/// - <b>Transmutation.</b> A note can turn into its pair on the same axis (<see cref="Transmute"/>): healers metabolize
///   the wounded face into the healthy one ("nothing is wasted", Eleos Bloom.md); an Atonalis feeding turns the healthy
///   face it preys on into its own wound ("their presence amplifies pain that mirrors their own", Atonalis.md).
///
/// Every recipe is a proposal.
/// </summary>
public static class EmotionalAlchemy
{
    // ===== COCKTAILS =====

    /// <summary>A recipe's shares (each line over the whole; lines of one note summed), strongest first.</summary>
    public static List<FeelingWeight> Shares(IEnumerable<FeelingWeight> recipe)
    {
        var lines = (recipe ?? Enumerable.Empty<FeelingWeight>()).Where(l => l != null && l.weight > 0f)
            .GroupBy(l => l.feeling).Select(g => new FeelingWeight { feeling = g.Key, weight = g.Sum(l => l.weight) }).ToList();
        float total = lines.Sum(l => l.weight);
        foreach (var l in lines) l.weight = total <= 0f ? 0f : l.weight / total;
        return lines.OrderByDescending(l => l.weight).ThenBy(l => (int)l.feeling).ToList();
    }

    /// <summary>How many exact servings of the cocktail a register pours: its scarcest note over what the recipe asks of it.</summary>
    public static float Servings(EmotionalRegister r, IEnumerable<FeelingWeight> recipe)
    {
        var shares = Shares(recipe);
        if (r == null || shares.Count == 0) return 0f;
        float servings = float.PositiveInfinity;
        foreach (var l in shares) servings = Math.Min(servings, r[l.feeling] / l.weight);
        return float.IsPositiveInfinity(servings) ? 0f : servings;
    }

    /// <summary>What a loose eater of the cocktail finds: every note of its recipe, whatever the proportion.</summary>
    public static float Loose(EmotionalRegister r, IEnumerable<FeelingWeight> recipe)
    {
        if (r == null) return 0f;
        return Shares(recipe).Sum(l => r[l.feeling]);
    }

    /// <summary>How exactly a register's mix matches a recipe (cosine over all sixteen notes, 0-1).</summary>
    public static float Fit(EmotionalRegister r, IEnumerable<FeelingWeight> recipe) => Cosine(r, EmotionalRegister.Of(recipe));

    /// <summary>The cosine between two registers (0 when either is empty).</summary>
    public static float Cosine(EmotionalRegister a, EmotionalRegister b)
    {
        if (a == null || b == null) return 0f;
        double dot = 0, na = 0, nb = 0;
        foreach (var f in EmotionalRegister.All)
        {
            double x = a[f], y = b[f];
            dot += x * y; na += x * x; nb += y * y;
        }
        return na <= 1e-12 || nb <= 1e-12 ? 0f : (float)(dot / Math.Sqrt(na * nb));
    }

    /// <summary>How many notes a recipe really leans on (inverse Simpson of its shares: 1 a purist's, up to 16).</summary>
    public static float Breadth(IEnumerable<FeelingWeight> recipe)
    {
        var shares = Shares(recipe);
        float sum = shares.Sum(l => l.weight * l.weight);
        return sum <= 0f ? 0f : 1f / sum;
    }

    /// <summary>A recipe in words: "Longing 40%, Wonder 30%, Estrangement 20%, Love 10%".</summary>
    public static string Words(IEnumerable<FeelingWeight> recipe, int top = 4) =>
        string.Join(", ", Shares(recipe).Take(top).Select(l => $"{l.feeling} {Math.Round(l.weight * 100f, MidpointRounding.AwayFromZero):0}%"));

    // ===== TRANSMUTATION =====

    /// <summary>Turns <paramref name="share"/> (0-1) of a note into its pair on the same axis; returns how much turned.</summary>
    public static float Transmute(EmotionalRegister r, Feeling from, float share)
    {
        if (r == null) return 0f;
        float moved = r.Drain(from, share);
        r.Add(EmotionalRegister.Pair(from), moved);
        return moved;
    }

    // ===== COMPOUNDS =====

    private static FeelingWeight W(Feeling f, float w) => new FeelingWeight { feeling = f, weight = w };

    private static Compound C(string id, string name, string gloss, params FeelingWeight[] recipe) =>
        new Compound { id = id, name = name, gloss = gloss, recipe = recipe };

    /// <summary>The named compound feelings of emotional alchemy (proposals; the owner's example, Anemoia, first).</summary>
    public static readonly Compound[] Compounds =
    {
        C("anemoia", "Anemoia", "longing for a time you never knew", W(Feeling.Longing, 40), W(Feeling.Wonder, 30), W(Feeling.Estrangement, 20), W(Feeling.Love, 10)),
        C("nostalgia", "Nostalgia", "the sweet ache of a home that was", W(Feeling.Longing, 45), W(Feeling.Belonging, 35), W(Feeling.Joy, 20)),
        C("saudade", "Saudade", "love for what is absent and may not return", W(Feeling.Longing, 45), W(Feeling.Love, 35), W(Feeling.Tumult, 20)),
        C("grief", "Grief", "love that stays when its object is gone", W(Feeling.Tumult, 40), W(Feeling.Love, 35), W(Feeling.Estrangement, 25)),
        C("catharsis", "Catharsis", "pain released into relief", W(Feeling.Tumult, 35), W(Feeling.Joy, 35), W(Feeling.Pain, 30)),
        C("melancholy", "Melancholy", "a sorrow that has learned to sit still", W(Feeling.Tumult, 45), W(Feeling.Doubt, 25), W(Feeling.Longing, 30)),
        C("terror", "Terror", "fear with nowhere left to go", W(Feeling.Dread, 70), W(Feeling.Pain, 20), W(Feeling.Doubt, 10)),
        C("paranoia", "Paranoia", "fear that suspects its own senses", W(Feeling.Dread, 50), W(Feeling.Doubt, 50)),
        C("awe", "Awe", "wonder with a tremor of fear in it", W(Feeling.Wonder, 50), W(Feeling.Dread, 25), W(Feeling.Courage, 25)),
        C("hope", "Hope", "courage reaching toward what it longs for", W(Feeling.Courage, 40), W(Feeling.Joy, 30), W(Feeling.Longing, 30)),
        C("defiance", "Defiance", "pride that stands in its wounds", W(Feeling.Courage, 40), W(Feeling.Pride, 40), W(Feeling.Pain, 20)),
        C("triumph", "Triumph", "the joy of a will that held", W(Feeling.Pride, 50), W(Feeling.Joy, 30), W(Feeling.Courage, 20)),
        C("euphoria", "Euphoria", "joy flooding the body", W(Feeling.Joy, 60), W(Feeling.Vitality, 40)),
        C("ecstasy", "Ecstasy", "joy so great it carries the self beyond itself", W(Feeling.Joy, 45), W(Feeling.Wonder, 25), W(Feeling.Vitality, 20), W(Feeling.Love, 10)),
        C("lust", "Lust", "the body's hunger for another, with a thrill of the forbidden (what the Lust Berries ripen on)", W(Feeling.Vitality, 40), W(Feeling.Longing, 30), W(Feeling.Shame, 15), W(Feeling.Joy, 15)),
        C("desire", "Desire", "the body reaching for another (the pleasure parasites' lure)", W(Feeling.Longing, 50), W(Feeling.Vitality, 50)),
        C("tenderness", "Tenderness", "love held gently in a living body", W(Feeling.Love, 60), W(Feeling.Vitality, 20), W(Feeling.Belonging, 20)),
        C("serenity", "Serenity", "a whole self at rest in a wondrous world", W(Feeling.Belonging, 40), W(Feeling.Wonder, 30), W(Feeling.Vitality, 30)),
        C("rapture", "Rapture", "devotion lifted into wonder and joy", W(Feeling.Devotion, 40), W(Feeling.Joy, 30), W(Feeling.Wonder, 30)),
        C("guilt", "Guilt", "shame that cannot stop turning over", W(Feeling.Shame, 60), W(Feeling.Fixation, 25), W(Feeling.Love, 15)),
        C("resentment", "Resentment", "an old hurt the will keeps feeding", W(Feeling.Shame, 40), W(Feeling.Fixation, 40), W(Feeling.Pain, 20)),
        C("humiliation", "Humiliation", "shame that cuts a self off from its own", W(Feeling.Shame, 70), W(Feeling.Estrangement, 30)),
        C("jealousy", "Jealousy", "longing that fears and cannot let go", W(Feeling.Longing, 40), W(Feeling.Fixation, 30), W(Feeling.Dread, 30)),
        C("loneliness", "Loneliness", "longing with no one near", W(Feeling.Longing, 50), W(Feeling.Estrangement, 50)),
        C("dysphoria", "Dysphoria", "a self named wrongly by others", W(Feeling.Estrangement, 50), W(Feeling.Shame, 30), W(Feeling.Longing, 20)),
        C("kenopsia", "Kenopsia", "the eeriness of a place emptied of its people", W(Feeling.Estrangement, 40), W(Feeling.Doubt, 30), W(Feeling.Longing, 30)),
        C("ennui", "Ennui", "a self that has stopped wondering", W(Feeling.Estrangement, 40), W(Feeling.Fixation, 30), W(Feeling.Doubt, 30)),
        C("sonder", "Sonder", "the wonder that every stranger lives a life as full as yours", W(Feeling.Wonder, 40), W(Feeling.Belonging, 30), W(Feeling.Estrangement, 30)),
        C("homecoming", "Homecoming", "an exile's first step back among its own", W(Feeling.Belonging, 40), W(Feeling.Longing, 35), W(Feeling.Joy, 25)),
        C("vigil", "Vigil", "devotion keeping watch over the hurt and the lost", W(Feeling.Devotion, 40), W(Feeling.Pain, 30), W(Feeling.Love, 30)),
    };

    public static Compound CompoundById(string id) => Compounds.FirstOrDefault(c => string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The compound feelings a register holds: those its mix resembles at least <paramref name="threshold"/> (cosine),
    /// most resembled first, each with how strongly (resemblance x what it holds of the compound's notes).
    /// </summary>
    public static List<(Compound compound, float resemblance, float strength)> Detect(EmotionalRegister r, float threshold = 0.7f, int top = 3)
    {
        var found = new List<(Compound, float, float)>();
        if (r == null || r.Total <= 1e-4f) return found;
        foreach (var c in Compounds)
        {
            float fit = Fit(r, c.recipe);
            if (fit >= threshold) found.Add((c, fit, fit * Loose(r, c.recipe)));
        }
        return found.OrderByDescending(x => x.Item2).ThenBy(x => x.Item1.id, StringComparer.Ordinal).Take(top).ToList();
    }

    /// <summary>The compound a recipe most resembles (for naming a variety), and how closely.</summary>
    public static (Compound compound, float resemblance) Nearest(IEnumerable<FeelingWeight> recipe)
    {
        var r = EmotionalRegister.Of(recipe);
        return Compounds.Select(c => (c, Fit(r, c.recipe))).OrderByDescending(x => x.Item2).ThenBy(x => x.c.id, StringComparer.Ordinal).First();
    }
}
