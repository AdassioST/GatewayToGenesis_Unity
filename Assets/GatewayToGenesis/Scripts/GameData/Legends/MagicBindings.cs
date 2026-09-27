using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The seven bindings of The Principles of Magic as legends carry them, with no scene state: the vault's names and
/// order (Legend Trait.md: "Resonance, Luminance, Flux, Void, Cindergale, Crystal, Strand"), each legend class's
/// affinity (Stellar Legacy Score.md), and the proficiency scale of a binding score (Legend Trait.md's table).
/// </summary>
public static class MagicBindings
{
    /// <summary>The seven bindings in the vault's order.</summary>
    public static readonly string[] All = { "Resonance", "Luminance", "Flux", "Void", "Cindergale", "Crystal", "Strand" };

    /// <summary>The binding's canonical spelling, or null when <paramref name="name"/> is not one of the seven.</summary>
    public static string Canonical(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string trimmed = name.Trim();
        return All.FirstOrDefault(b => string.Equals(b, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsBinding(string name) => Canonical(name) != null;

    /// <summary>
    /// The binding a class of Great Spellweaver shares an affinity with (Stellar Legacy Score.md: "Great Sovereign
    /// (Resonance Affinity)"...). The vault adds that a Great Spellweaver is "usually of their element affinity" but
    /// not locked to it.
    /// </summary>
    public static string AffinityOf(LegendClass legendClass)
    {
        switch (legendClass)
        {
            case LegendClass.Sovereign: return "Resonance";
            case LegendClass.Seer: return "Luminance";
            case LegendClass.Concertist: return "Flux";
            case LegendClass.Justiciar: return "Void";
            case LegendClass.Vanguard: return "Cindergale";
            case LegendClass.Architect: return "Crystal";
            case LegendClass.Chronicler: return "Strand";
            default: return "Resonance";
        }
    }

    /// <summary>A binding score's proficiency, the vault's table: 1-4 Terrible ... 42+ Master.</summary>
    public static string Proficiency(int score)
    {
        if (score >= 42) return "Master";
        if (score >= 33) return "Expert";
        if (score >= 28) return "Advanced";
        if (score >= 21) return "Skilled";
        if (score >= 13) return "Average";
        if (score >= 5) return "Apprentice";
        return "Terrible";
    }

    /// <summary>The lowest score a binding can have (the vault's scale starts at 1).</summary>
    public const int MinimumScore = 1;

    /// <summary>A fresh score sheet with every binding at <paramref name="value"/>.</summary>
    public static Dictionary<string, int> Sheet(int value) => All.ToDictionary(b => b, _ => value, StringComparer.OrdinalIgnoreCase);
}
