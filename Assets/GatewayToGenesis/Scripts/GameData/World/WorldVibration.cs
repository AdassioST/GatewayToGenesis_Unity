using System;
using System.Collections.Generic;

/// <summary>
/// Vibrational Density and Vibrational Fallout, with no scene state (tested in <c>ExpeditionTests</c>); read by
/// <see cref="WorldMagic"/> whenever it lays down its fields, so both move with the Ages as the leylines do (fallout never
/// moves).
///
/// Vault anchors (Law of Relics.md, Spellweaving.md, Great Harmonic Loom.md, Arcanoria.md, Sacred Site.md):
/// - Density is "how tightly reality's threads maintain harmonic integrity"; the atmosphere stratifies by it (Sky Glass
///   lies "in the highest peaks... where Vibrational Density is at its highest"), it peaks along the Leylines (the
///   Symphonic Veins), and Sacred Sites hold the most there is. Here: a base, the strata (height above the sea), the
///   leylines' influence, their junctions and silver water; 1 on Sacred ground. Coherence "increases through the natural
///   Leylines and Vibrational Density": each cell's Coherence moves by <see cref="VibrationSettings.coherence"/> per point
///   of density off <see cref="VibrationSettings.neutralDensity"/>. Religious Havens yield by it.
/// - Fallout is "a persistent Corruption zone where shattered Coherence feeds continuously Primal White Noise", left where
///   the Dissonance broke the Loom; it "cannot be healed and requires specialized dampeners to contain". Here: where the
///   Age-free Dissonance of the Dissonance Seeds passes <see cref="VibrationSettings.falloutFrom"/>. Its density collapses
///   with it; Anchors and listening blooms cannot lift it; it is slow and wearing to cross, and the attuned (legends,
///   Spellweavers all) suffer it far more than Dissonance alone. Chaotic Resonant Cascades "erupt along the boundary".
/// </summary>
public static class WorldVibration
{
    /// <summary>A cell's fallout (0-1) from its Age-free Dissonance (0 on Sacred ground, which shrugs off common Dissonance).</summary>
    public static float Fallout(VibrationSettings v, float baseDissonance, bool sacred)
    {
        if (v == null || sacred) return 0f;
        float span = Math.Max(1e-4f, v.falloutFull - v.falloutFrom);
        return Clamp01((baseDissonance - v.falloutFrom) / span);
    }

    /// <summary>
    /// A cell's Vibrational Density (0-1): the base, the strata, the leylines' influence, their junctions and silver water
    /// (water holds its own); 1 on Sacred ground; collapsing with the fallout.
    /// </summary>
    public static float Density(VibrationSettings v, WorldTile t, float seaLevel, float fallout)
    {
        if (v == null || t == null) return 0f;
        if (t.sacred) return 1f;
        float d;
        if (t.water && !t.lake) d = v.waterDensity;
        else
        {
            float height = Clamp01((t.elevation - seaLevel) / Math.Max(1e-4f, 1f - seaLevel));
            d = (t.lake ? v.waterDensity : v.baseDensity) + v.strata * height;
        }
        d += v.leylines * Clamp01(t.leylineInfluence);
        d += t.junction >= 3 ? v.basin : t.junction == 2 ? v.convergence : 0f;
        if (t.silver) d += v.silver;
        return Clamp01(d) * (1f - Clamp01(fallout));
    }

    /// <summary>The Coherence density lends (or takes, below the neutral point).</summary>
    public static float CoherenceShift(VibrationSettings v, float density) => v == null ? 0f : v.coherence * (density - v.neutralDensity);

    /// <summary>What fallout does to one step into its cell (1 none).</summary>
    public static float TravelFactor(VibrationSettings v, float fallout) => 1f + Math.Max(0f, v?.travel ?? 0f) * Clamp01(fallout);

    /// <summary>
    /// Chaotic Resonant Cascades: along fallout's edge, each cell within <see cref="VibrationSettings.cascadeReach"/> steps
    /// of fallout takes the strongest fallout near it, fading with the steps, less its own (the heart of the fallout is
    /// dead, not cascading).
    /// </summary>
    public static void Cascades(WorldMap map, VibrationSettings v)
    {
        if (map == null) return;
        foreach (var t in map.Tiles) t.cascade = 0f;
        int reach = Math.Max(0, v?.cascadeReach ?? 0);
        if (reach == 0) return;
        foreach (var source in map.Tiles)
        {
            if (source.fallout <= 0.05f) continue;
            foreach (var coord in HexCoord.Spiral(source.coord, reach))
            {
                var t = map.Get(coord);
                if (t == null || t.index == source.index) continue;
                float near = source.fallout * (1f - (HexCoord.Distance(coord, source.coord) - 1) / (float)reach);
                t.cascade = Math.Max(t.cascade, Clamp01(near - t.fallout));
            }
        }
    }

    /// <summary>A word for a density.</summary>
    public static string Word(float density) =>
        density >= 0.95f ? "sacred" : density >= 0.7f ? "dense" : density >= 0.45f ? "firm" : density >= 0.25f ? "thin" : density > 0f ? "a dead zone" : "shattered";

    /// <summary>What density and fallout do to a cell, for the hover card (null when there is nothing to say).</summary>
    public static string Describe(WorldTile t)
    {
        if (t == null) return null;
        var parts = new List<string> { $"{t.vibrationalDensity:0.00} ({Word(t.vibrationalDensity)})" };
        if (t.fallout > 0.01f) parts.Add($"Vibrational Fallout {t.fallout:P0}: travel x{t.falloutTravel:0.#}, Composure drains fast");
        else if (t.cascade > 0.05f) parts.Add($"Chaotic Resonant Cascade {t.cascade:P0} (fallout's edge)");
        return string.Join("; ", parts);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
