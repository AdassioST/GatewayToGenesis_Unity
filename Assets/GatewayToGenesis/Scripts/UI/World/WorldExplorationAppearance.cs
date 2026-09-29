using UnityEngine;

/// <summary>Colour treatment uses existing knowledge; it adds no map marks or discoveries.</summary>
public static class WorldExplorationAppearance
{
    public const string Legend = "Exploration: fog = unseen; muted colour = wilderness in sight; clearer colour = known ground; full colour = surveyed. Zoom in to read individual hexes.";
    public static readonly Vector4 Saturation = new Vector4(.25f, .42f, .62f, 1f);
}
