using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The attuned element of one of the seven bindings (vault: The Principles of Magic.md), or none. Unattuned: a being
/// with no interface to the Great Harmonic Loom (no Soul Leitmotif awakened, no Coherence-Binding Tissue): an ordinary
/// animal, a levy of people who never had a Motif Awakening. Saved by index: append only, and keep the seven in the
/// vault's order after <see cref="Unattuned"/> (<see cref="MagicBindings.All"/>).
/// </summary>
public enum SpellBinding { Unattuned, Resonance, Luminance, Flux, Void, Cindergale, Crystal, Strand }

/// <summary>How an attack's element meets a defender's primary binding.</summary>
public enum HarmonicMatch
{
    /// <summary>No relation (either side unattuned, or elements that do not touch in the circle).</summary>
    Neutral,
    /// <summary>The attack's element overcomes the defender's primary binding: the defender takes more.</summary>
    Overcomes,
    /// <summary>The defender's primary binding overcomes the attack's element: the defender takes less.</summary>
    Resisted,
    /// <summary>Luminance against Void (or Void against Luminance): the Dance of Light and Shadow, each effective against the other.</summary>
    Dance,
}

/// <summary>
/// The Elemental Harmonic Circle (vault: The Principles of Magic.md, "Elemental Harmonic Circle"), with no scene state
/// (tested in <c>CombatTests</c>). Elements interact by the spell's Major Note (its root), never its Minor Notes:
///
/// - The Pentatonic Effectiveness Circle: Flux ▶ Cindergale ▶ Crystal ▶ Resonance ▶ Strand ▶ back to Flux.
/// - The Dance of Light and Shadow: Luminance and Void, outside the circle, each effective and weak to the other.
///
/// A being's PRIMARY binding is its weakness (owner's rule, Sept 28 2026): it is what an attack's element is measured
/// against. Secondary bindings (a legend's Ornaments, a creature's other attunements) only widen what it can cast.
/// The multipliers are proposals (<see cref="CombatTuning"/>); the circle itself is canon.
/// </summary>
public static class HarmonicCircle
{
    /// <summary>The seven attuned elements, in the vault's order.</summary>
    public static readonly IReadOnlyList<SpellBinding> Seven = new[]
    {
        SpellBinding.Resonance, SpellBinding.Luminance, SpellBinding.Flux, SpellBinding.Void,
        SpellBinding.Cindergale, SpellBinding.Crystal, SpellBinding.Strand,
    };

    /// <summary>The Pentatonic Effectiveness Circle in the vault's order: each overcomes the next.</summary>
    public static readonly IReadOnlyList<SpellBinding> Pentatonic = new[]
    {
        SpellBinding.Flux, SpellBinding.Cindergale, SpellBinding.Crystal, SpellBinding.Resonance, SpellBinding.Strand,
    };

    /// <summary>The element <paramref name="binding"/> overcomes in the pentatonic circle, or Unattuned (Luminance, Void, none).</summary>
    public static SpellBinding Overcomes(SpellBinding binding)
    {
        int i = IndexIn(binding);
        return i < 0 ? SpellBinding.Unattuned : Pentatonic[(i + 1) % Pentatonic.Count];
    }

    /// <summary>The element that overcomes <paramref name="binding"/> in the pentatonic circle, or Unattuned.</summary>
    public static SpellBinding OvercomeBy(SpellBinding binding)
    {
        int i = IndexIn(binding);
        return i < 0 ? SpellBinding.Unattuned : Pentatonic[(i + Pentatonic.Count - 1) % Pentatonic.Count];
    }

    private static int IndexIn(SpellBinding binding)
    {
        for (int i = 0; i < Pentatonic.Count; i++) if (Pentatonic[i] == binding) return i;
        return -1;
    }

    public static bool IsLightOrShadow(SpellBinding b) => b == SpellBinding.Luminance || b == SpellBinding.Void;

    /// <summary>How an attack rooted in <paramref name="attack"/> meets a defender whose primary binding is <paramref name="defender"/>.</summary>
    public static HarmonicMatch Match(SpellBinding attack, SpellBinding defender)
    {
        if (attack == SpellBinding.Unattuned || defender == SpellBinding.Unattuned) return HarmonicMatch.Neutral;
        if (IsLightOrShadow(attack) || IsLightOrShadow(defender))
            return IsLightOrShadow(attack) && IsLightOrShadow(defender) && attack != defender ? HarmonicMatch.Dance : HarmonicMatch.Neutral;
        if (Overcomes(attack) == defender) return HarmonicMatch.Overcomes;
        if (Overcomes(defender) == attack) return HarmonicMatch.Resisted;
        return HarmonicMatch.Neutral;
    }

    /// <summary>The damage multiplier of <paramref name="attack"/> against a defender whose primary is <paramref name="defender"/>.</summary>
    public static float Multiplier(SpellBinding attack, SpellBinding defender, CombatTuning tuning)
    {
        tuning = tuning ?? CombatTuning.Default;
        switch (Match(attack, defender))
        {
            case HarmonicMatch.Overcomes: return tuning.overcomes;
            case HarmonicMatch.Resisted: return tuning.resisted;
            case HarmonicMatch.Dance: return tuning.dance;
            default: return 1f;
        }
    }

    /// <summary>What a being of primary <paramref name="primary"/> takes more from.</summary>
    public static IEnumerable<SpellBinding> WeakTo(SpellBinding primary)
    {
        if (primary == SpellBinding.Unattuned) yield break;
        if (IsLightOrShadow(primary)) { yield return primary == SpellBinding.Luminance ? SpellBinding.Void : SpellBinding.Luminance; yield break; }
        yield return OvercomeBy(primary);
    }

    /// <summary>What a being of primary <paramref name="primary"/> takes less from.</summary>
    public static IEnumerable<SpellBinding> Resists(SpellBinding primary)
    {
        if (primary == SpellBinding.Unattuned || IsLightOrShadow(primary)) yield break;
        yield return Overcomes(primary);
    }

    /// <summary>The element among <paramref name="options"/> that lands hardest on <paramref name="defender"/> (the first on a tie).</summary>
    public static SpellBinding BestAgainst(IEnumerable<SpellBinding> options, SpellBinding defender, CombatTuning tuning)
    {
        var best = SpellBinding.Unattuned;
        float bestMul = float.MinValue;
        foreach (var o in options ?? Enumerable.Empty<SpellBinding>())
        {
            if (o == SpellBinding.Unattuned) continue;
            float m = Multiplier(o, defender, tuning);
            if (m > bestMul + 1e-4f) { best = o; bestMul = m; }
        }
        return best;
    }

    /// <summary>The binding named <paramref name="name"/> ("Flux", "flux"), or Unattuned.</summary>
    public static SpellBinding Of(string name)
    {
        string canonical = MagicBindings.Canonical(name);
        if (canonical == null) return SpellBinding.Unattuned;
        return (SpellBinding)(Array.IndexOf(MagicBindings.All, canonical) + 1);
    }

    /// <summary>"Flux", or "Unattuned".</summary>
    public static string Name(SpellBinding binding) => binding.ToString();

    /// <summary>The binding the element is attuned to ("Perfect Focus" for Cindergale), or null.</summary>
    public static string Principle(SpellBinding binding)
    {
        switch (binding)
        {
            case SpellBinding.Resonance: return "Key of Attunement";
            case SpellBinding.Luminance: return "Sufficient Precision";
            case SpellBinding.Flux: return "Emotional Authenticity";
            case SpellBinding.Void: return "Essence Sacrifice";
            case SpellBinding.Cindergale: return "Perfect Focus";
            case SpellBinding.Crystal: return "Absolute Certainty";
            case SpellBinding.Strand: return "Echoing Bonds";
            default: return null;
        }
    }

    /// <summary>What the element is made of (vault notes: "Fire + Wind").</summary>
    public static string Nature(SpellBinding binding)
    {
        switch (binding)
        {
            case SpellBinding.Resonance: return "Sound + Wind";
            case SpellBinding.Luminance: return "Light + Lightning";
            case SpellBinding.Flux: return "Water + Currents";
            case SpellBinding.Void: return "Shadow + Space";
            case SpellBinding.Cindergale: return "Fire + Wind";
            case SpellBinding.Crystal: return "Prisms + Structure";
            case SpellBinding.Strand: return "Time + Memory";
            default: return "no attunement";
        }
    }

    /// <summary>
    /// Why one element overcomes another, the vault's physical reading of each link (The Principles of Magic.md), for
    /// battle reports. Null when there is no link.
    /// </summary>
    public static string Why(SpellBinding attack, SpellBinding defender)
    {
        switch (Match(attack, defender))
        {
            case HarmonicMatch.Dance:
                return attack == SpellBinding.Luminance ? "clarity dispels emptiness" : "emptiness consumes illumination";
            case HarmonicMatch.Overcomes:
                switch (attack)
                {
                    case SpellBinding.Flux: return "flowing water and gusting winds extinguish and disperse flickering embers";
                    case SpellBinding.Cindergale: return "heat and wind reshape ore into precise crystalline facets";
                    case SpellBinding.Crystal: return "faceted crystals trap and ensnare even whispered intention";
                    case SpellBinding.Resonance: return "sound waves make the strings of memory vibrate harder";
                    case SpellBinding.Strand: return "released temporal stasis breaks like a dam into powerful currents";
                }
                break;
            case HarmonicMatch.Resisted:
                return $"{Name(defender)} overcomes {Name(attack)}";
        }
        return null;
    }

    /// <summary>The element's color (owner's palette, Sept 28 2026).</summary>
    public static Color Color(SpellBinding binding)
    {
        switch (binding)
        {
            case SpellBinding.Resonance: return new Color(0.18f, 0.9f, 0.62f);   // bright green / teal luminescence
            case SpellBinding.Luminance: return new Color(1f, 0.96f, 0.72f);     // pure white to pale yellow
            case SpellBinding.Flux: return new Color(0.2f, 0.45f, 0.9f);         // deep shifting blues
            case SpellBinding.Void: return new Color(0.42f, 0.22f, 0.62f);       // purple / black, midnight
            case SpellBinding.Cindergale: return new Color(0.92f, 0.38f, 0.16f); // warm orange to crimson
            case SpellBinding.Crystal: return new Color(0.96f, 0.6f, 0.86f);     // prismatic pink / rainbow
            case SpellBinding.Strand: return new Color(0.85f, 0.68f, 0.22f);     // braided gold
            default: return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    /// <summary>"Cindergale (weak to Flux; resists Crystal)" for cards and tooltips; "Unattuned" for a mundane being.</summary>
    public static string Describe(SpellBinding primary)
    {
        if (primary == SpellBinding.Unattuned) return "Unattuned: no element, so no weakness and no resistance";
        var parts = new List<string>();
        var weak = WeakTo(primary).ToList();
        var resists = Resists(primary).ToList();
        if (weak.Count > 0) parts.Add("weak to " + string.Join(", ", weak.Select(Name)));
        if (resists.Count > 0) parts.Add("resists " + string.Join(", ", resists.Select(Name)));
        return parts.Count == 0 ? Name(primary) : $"{Name(primary)} ({string.Join("; ", parts)})";
    }
}
