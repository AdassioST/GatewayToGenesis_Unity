using System;

/// <summary>The world's three readings (WORLD_GENERATION.md §3).</summary>
public enum WorldScale
{
    /// <summary>Micro: local ground, riverbanks, the seven hexes of each cell.</summary>
    Micro,
    /// <summary>Meso: the playable strategy cells (expeditions, yields, borders).</summary>
    Meso,
    /// <summary>Macro: the atlas of 49-cell aggregates, regional biome mixtures, major rivers.</summary>
    Macro,
}

/// <summary>
/// How the world view zooms, with no scene state (tested in <c>WorldGenerationTests</c>). The camera's
/// orthographic half-height (in micro units) is the zoom; <see cref="Level"/> turns it into the continuous reading
/// the terrain shader blends (0 micro, 1 meso, 2 macro), with a soft band between readings. Scrolling out of the
/// capital opens the world at <see cref="CapitalSize"/> and settles on the micro reading; scrolling in at the closest
/// zoom over the capital returns to it.
/// </summary>
public static class WorldZoom
{
    /// <summary>The capital's own hex fills the screen: where the world opens from and closes into.</summary>
    public const float CapitalSize = 1.4f;
    public const float Closest = 5f;
    public const float MicroSize = 13f, MesoSize = 62f, MacroSize = 300f;
    /// <summary>Soft bands between readings.</summary>
    public const float MicroEnd = 24f, MesoStart = 40f, MesoEnd = 125f, MacroStart = 210f;
    /// <summary>Zoom factor per scroll notch.</summary>
    public const float Notch = 1.22f;

    public static float Level(float size)
    {
        if (size <= MicroEnd) return 0f;
        if (size < MesoStart) return Band(size, MicroEnd, MesoStart);
        if (size <= MesoEnd) return 1f;
        if (size < MacroStart) return 1f + Band(size, MesoEnd, MacroStart);
        return 2f;
    }

    // Smooth 0-1 across a band, in log space (zoom feels even).
    private static float Band(float size, float from, float to)
    {
        double t = (Math.Log(size) - Math.Log(from)) / (Math.Log(to) - Math.Log(from));
        t = Math.Max(0, Math.Min(1, t));
        return (float)(t * t * (3 - 2 * t));
    }

    public static WorldScale ScaleOf(float size)
    {
        float level = Level(size);
        return level < 0.5f ? WorldScale.Micro : level < 1.5f ? WorldScale.Meso : WorldScale.Macro;
    }

    public static float SizeFor(WorldScale scale, float farthest)
    {
        switch (scale)
        {
            case WorldScale.Micro: return MicroSize;
            case WorldScale.Meso: return MesoSize;
            default: return Math.Min(MacroSize, farthest);
        }
    }

    /// <summary>Farthest zoom: the whole world in view, with a margin.</summary>
    public static float Farthest(float worldHalfHeight, float worldHalfWidth, float aspect) =>
        Math.Max(MacroStart * 1.1f, Math.Max(worldHalfHeight, worldHalfWidth / Math.Max(0.1f, aspect)) * 1.08f);

    /// <summary>The zoom after <paramref name="notches"/> scroll notches (positive zooms in), kept in range.</summary>
    public static float Step(float size, float notches, float farthest) =>
        Clamp(size * (float)Math.Pow(Notch, -notches), Closest, farthest);

    /// <summary>
    /// The camera centre that keeps the world point under the cursor fixed when zooming from
    /// <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static void ZoomAbout(float centerX, float centerY, float pointX, float pointY, float from, float to, out float newX, out float newY)
    {
        float k = to / from;
        newX = pointX - (pointX - centerX) * k;
        newY = pointY - (pointY - centerY) * k;
    }

    /// <summary>Whether scrolling in further should return to the capital: already at the closest zoom.</summary>
    public static bool AtClosest(float size) => size <= Closest * 1.001f;

    public static string Name(WorldScale scale) => scale == WorldScale.Micro ? "Micro" : scale == WorldScale.Meso ? "Meso" : "Macro";

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
