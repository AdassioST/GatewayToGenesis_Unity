using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Who one legend is, beyond its authored <see cref="LegendData"/>: its Soul Leitmotif and Ornaments, its Legend
/// Traits as they stand now, and its Composure. One per legend the civilization has met, kept in
/// <see cref="LegendProgress"/> and saved with it (field names are part of the save: rename with care).
/// </summary>
[Serializable]
public class LegendSoul
{
    /// <summary>The Soul Leitmotif's primary binding.</summary>
    public string leitmotif;
    /// <summary>Up to two Ornaments, in the order awakened (the first is the primary Ornament, the second the secondary).</summary>
    public List<string> ornaments = new List<string>();
    /// <summary>Has reached the Catalytic Abyss of Emotion (the Awakened State) at least once.</summary>
    public bool awakenedState;
    /// <summary>Sevenths left of the Awakened State's surge.</summary>
    public int surgeSevenths;

    /// <summary>The Wish: origin traits.</summary>
    public List<string> wish = new List<string>();
    /// <summary>The Expression as it stands: three personality traits, an evolved one under its middle trait's name.</summary>
    public List<string> expression = new List<string>();
    /// <summary>The Expression the legend started with, in the same order.</summary>
    public List<string> startingExpression = new List<string>();
    /// <summary>Beside each trait of the Expression, the binding it evolved along ("" while it has not).</summary>
    public List<string> evolvedAlong = new List<string>();

    /// <summary>Composure's strain: 0 is the calmest Pristine, <see cref="ComposureTuning.surrenderAt"/> is Surrender.</summary>
    public float strain;
    /// <summary>The deepest Composure since the last Motif Awakening.</summary>
    public ComposureState deepest = ComposureState.Clouded;
    /// <summary>Sevenths spent Fractured or deeper since the last Motif Awakening.</summary>
    public int woundSevenths;
    /// <summary>Motif Awakenings and the Catalytic Abyss, oldest first ("Motif Awakening to Flux, its primary Ornament").</summary>
    public List<string> awakenings = new List<string>();

    public bool HasEvolved(int index) => index >= 0 && index < evolvedAlong.Count && !string.IsNullOrEmpty(evolvedAlong[index]);
}

/// <summary>What a Motif Awakening did.</summary>
public struct AwakeningResult
{
    /// <summary>The Catalytic Abyss of Emotion: both Ornaments were already embellished, so the feeling forced the Awakened State.</summary>
    public bool abyss;
    /// <summary>The binding of the new Ornament.</summary>
    public string element;
    /// <summary>1 for the primary Ornament, 2 for the secondary.</summary>
    public int ornament;
    /// <summary>The personality trait that evolved along the new Ornament, and what it became (null when none could).</summary>
    public string evolvedFrom, evolvedTo;
    /// <summary>The legend healed from Spiraling (vault achievement: "recover from Spiraling Composure...").</summary>
    public bool afterSpiraling;

    public bool Evolved => !string.IsNullOrEmpty(evolvedTo);
}

/// <summary>
/// How a legend's soul is born and grows, with no scene state (tested in <c>LegendSoulTests</c>). Everything drawn is
/// seeded by the world and the legend's name, so the same world gives the same legends the same traits.
/// <list type="bullet">
/// <item>The Soul Leitmotif's primary binding is the legend's authored one, else its class's affinity (the vault: a
/// Great Spellweaver is "usually of their element affinity").</item>
/// <item>The Expression is exactly three personality traits (vault); drawn ones start with one that points toward the
/// primary binding (vault: they "should point towards the same elements of their Soul Leitmotif").</item>
/// <item>The Wish is one origin trait whose Scaled Cost fits the legend's rarity (a proposal).</item>
/// <item>A Motif Awakening embellishes the Soul Leitmotif with an Ornament drawn from the elements its unevolved
/// personality traits lean toward, and one of those traits evolves along it into its middle trait (vault: "all
/// personality Legend Traits are a blend of several elements that will eventually awaken as their Soul Leitmotif
/// Ornaments"). With both Ornaments, the feeling drowns in the Catalytic Abyss of Emotion instead.</item>
/// </list>
/// </summary>
public static class LegendSoulRules
{
    /// <summary>The vault: "A Legend has exactly three personality Legend Traits."</summary>
    public const int ExpressionCount = 3;
    /// <summary>The vault: a Soul Leitmotif "can receive up to two Ornaments".</summary>
    public const int MaxOrnaments = 2;

    /// <summary>What an authored legend brings to its soul (empty lists and a blank leitmotif are drawn instead).</summary>
    public struct Template
    {
        public string name;
        public LegendClass legendClass;
        public GameRarity rarity;
        public string leitmotif;
        public IList<string> origins;
        public IList<string> personality;

        public static Template Of(LegendData legend) => legend == null ? default : new Template
        {
            name = legend.legendName,
            legendClass = legend.legendClass,
            rarity = legend.rarity,
            leitmotif = legend.soulLeitmotif,
            origins = legend.originTraits,
            personality = legend.personalityTraits,
        };
    }

    public static LegendSoul Create(in Template template, LegendTraitCatalog catalog, SoulTuning tuning, int worldSeed, float strain)
    {
        catalog = catalog ?? new LegendTraitCatalog();
        var rng = new SoulRandom(Seed(worldSeed, template.name, 0));
        var soul = new LegendSoul
        {
            leitmotif = MagicBindings.Canonical(template.leitmotif) ?? MagicBindings.AffinityOf(template.legendClass),
            strain = strain,
        };

        foreach (var name in template.personality ?? Array.Empty<string>())
        {
            var trait = catalog.Personality(name);
            if (trait != null && soul.expression.Count < ExpressionCount && !soul.expression.Contains(trait.name, StringComparer.OrdinalIgnoreCase)) soul.expression.Add(trait.name);
        }
        while (soul.expression.Count < ExpressionCount)
        {
            var open = catalog.personalities.Where(t => t.elements.Count > 0 && !soul.expression.Contains(t.name, StringComparer.OrdinalIgnoreCase)).ToList();
            if (open.Count == 0) break;
            // The first trait points toward the primary binding when any can.
            if (soul.expression.Count == 0)
            {
                var leaning = open.Where(t => t.elements.Contains(soul.leitmotif, StringComparer.OrdinalIgnoreCase)).ToList();
                if (leaning.Count > 0) open = leaning;
            }
            soul.expression.Add(open[rng.Range(open.Count)].name);
        }
        soul.startingExpression.AddRange(soul.expression);
        soul.evolvedAlong.AddRange(soul.expression.Select(_ => string.Empty));

        foreach (var name in template.origins ?? Array.Empty<string>())
        {
            var trait = catalog.Origin(name);
            if (trait != null && !soul.wish.Contains(trait.name, StringComparer.OrdinalIgnoreCase)) soul.wish.Add(trait.name);
        }
        if (soul.wish.Count == 0)
        {
            int budget = (tuning ?? new SoulTuning()).BudgetFor(template.rarity);
            var affordable = catalog.origins.Where(o => o.scaledCost <= budget).ToList();
            if (affordable.Count > 0) soul.wish.Add(affordable[rng.Range(affordable.Count)].name);
        }
        return soul;
    }

    /// <summary>
    /// The seven binding scores: the base, +5 on the primary binding (vault), each origin trait's Binding Effect
    /// (vault), each Ornament and each evolved trait (proposals). Never below the scale's floor of 1.
    /// </summary>
    public static Dictionary<string, int> Bindings(LegendSoul soul, LegendTraitCatalog catalog, SoulTuning t)
    {
        t = t ?? new SoulTuning();
        var sheet = MagicBindings.Sheet(t.baseScore);
        if (soul == null) return sheet;
        Add(sheet, soul.leitmotif, t.leitmotifBonus);
        foreach (var name in soul.wish)
        {
            var trait = catalog?.Origin(name);
            if (trait == null) continue;
            foreach (var effect in trait.bindings) Add(sheet, effect.Key, effect.Value);
        }
        foreach (var ornament in soul.ornaments) Add(sheet, ornament, t.ornamentBonus);
        for (int i = 0; i < soul.expression.Count; i++)
            if (soul.HasEvolved(i)) Add(sheet, soul.evolvedAlong[i], t.evolvedTraitBonus);
        foreach (var binding in MagicBindings.All) sheet[binding] = Math.Max(MagicBindings.MinimumScore, sheet[binding]);
        return sheet;
    }

    /// <summary>
    /// A Motif Awakening (or, with both Ornaments embellished, the Catalytic Abyss of Emotion). Changes
    /// <paramref name="soul"/> and says what happened. Composure bookkeeping (the wound, the surge) is the caller's.
    /// </summary>
    public static AwakeningResult Awaken(LegendSoul soul, string legendName, LegendTraitCatalog catalog, int worldSeed)
    {
        var result = new AwakeningResult { afterSpiraling = soul.deepest >= ComposureState.Spiraling };
        if (soul.ornaments.Count >= MaxOrnaments)
        {
            result.abyss = true;
            soul.awakenedState = true;
            soul.awakenings.Add("The Catalytic Abyss of Emotion: the Awakened State");
            return result;
        }
        catalog = catalog ?? new LegendTraitCatalog();
        var rng = new SoulRandom(Seed(worldSeed, legendName, 100 + soul.ornaments.Count));
        bool Taken(string binding) => string.Equals(binding, soul.leitmotif, StringComparison.OrdinalIgnoreCase) || soul.ornaments.Contains(binding, StringComparer.OrdinalIgnoreCase);

        // The elements its unevolved personality traits lean toward, weighted by how many lean there.
        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < soul.expression.Count; i++)
        {
            if (soul.HasEvolved(i)) continue;
            var trait = catalog.Personality(soul.expression[i]);
            if (trait == null) continue;
            foreach (var element in trait.elements.Where(e => !Taken(e))) weights[element] = (weights.TryGetValue(element, out int w) ? w : 0) + 1;
        }
        // Ornaments "aren't necessarily tied to the expression of the original wound": any binding not yet held.
        if (weights.Count == 0) foreach (var binding in MagicBindings.All.Where(b => !Taken(b))) weights[binding] = 1;
        if (weights.Count == 0) return result;

        // In the vault's order, so the draw does not depend on dictionary order.
        var choices = MagicBindings.All.Where(weights.ContainsKey).ToList();
        int roll = rng.Range(choices.Sum(c => weights[c]));
        string chosen = choices[choices.Count - 1];
        foreach (var choice in choices)
        {
            if (roll < weights[choice]) { chosen = choice; break; }
            roll -= weights[choice];
        }
        soul.ornaments.Add(chosen);
        result.element = chosen;
        result.ornament = soul.ornaments.Count;

        // One unevolved trait with a middle trait written for this element evolves along it.
        var ready = Enumerable.Range(0, soul.expression.Count)
            .Where(i => !soul.HasEvolved(i) && (catalog.Personality(soul.expression[i])?.PathFor(chosen)?.middleTraits.Count ?? 0) > 0).ToList();
        if (ready.Count > 0)
        {
            int index = ready[rng.Range(ready.Count)];
            var options = catalog.Personality(soul.expression[index]).PathFor(chosen).middleTraits;
            result.evolvedFrom = soul.expression[index];
            result.evolvedTo = options[rng.Range(options.Count)];
            soul.expression[index] = result.evolvedTo;
            while (soul.evolvedAlong.Count < soul.expression.Count) soul.evolvedAlong.Add(string.Empty);
            soul.evolvedAlong[index] = chosen;
        }
        soul.awakenings.Add($"Motif Awakening to {chosen}, the {(result.ornament == 1 ? "primary" : "secondary")} Ornament" +
                            (result.Evolved ? $": {result.evolvedFrom} became {result.evolvedTo}" : string.Empty));
        return result;
    }

    /// <summary>Middle traits in the Expression now (evolved personality traits).</summary>
    public static IEnumerable<string> MiddleTraits(LegendSoul soul) =>
        soul == null ? Enumerable.Empty<string>() : soul.expression.Where((_, i) => soul.HasEvolved(i));

    // ===== DETERMINISTIC DRAWS =====

    /// <summary>A seed from the world, the legend and what is being drawn (stable across runs and platforms).</summary>
    public static ulong Seed(int worldSeed, string legendName, int salt)
    {
        ulong hash = 14695981039346656037UL; // FNV-1a
        foreach (char c in legendName ?? string.Empty)
        {
            hash ^= char.ToLowerInvariant(c);
            hash *= 1099511628211UL;
        }
        return hash ^ ((ulong)(uint)worldSeed << 32) ^ (uint)salt;
    }

    private static void Add(Dictionary<string, int> sheet, string binding, int amount)
    {
        string canonical = MagicBindings.Canonical(binding);
        if (canonical != null) sheet[canonical] += amount;
    }

    /// <summary>SplitMix64: small, fast and the same everywhere.</summary>
    private struct SoulRandom
    {
        private ulong _state;

        public SoulRandom(ulong seed) { _state = seed; }

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Range(int count) => count <= 0 ? 0 : (int)(Next() % (ulong)count);
    }
}
