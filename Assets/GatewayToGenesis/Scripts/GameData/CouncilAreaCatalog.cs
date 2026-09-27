using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The areas of the council (Resources/Council/Council Areas.asset). Proposals: the vault names the seats' domains
/// only in their descriptions. Add an area here, tag seats with it, and stories can call for it.
/// </summary>
[CreateAssetMenu(fileName = "Council Areas", menuName = "Game Object/Council Areas")]
public class CouncilAreaCatalog : ScriptableObject
{
    public const string ResourcePath = "Council/Council Areas";

    [Tooltip("How many links away a related seat may still answer for an area (0: exact areas only).")]
    public int maxDistance = 2;
    public List<CouncilArea> areas = new List<CouncilArea>();

    private static CouncilAreaCatalog _loaded;
    private static bool _tried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate()
    {
        _loaded = null;
        _tried = false;
    }

    /// <summary>The asset, or null (the code defaults of <see cref="CouncilAreaRules.Defaults"/> apply).</summary>
    public static CouncilAreaCatalog Loaded
    {
        get
        {
            if (!_tried)
            {
                _loaded = Resources.Load<CouncilAreaCatalog>(ResourcePath);
                _tried = true;
            }
            return _loaded;
        }
    }

    /// <summary>The areas in play: the asset's, or the defaults.</summary>
    public static IReadOnlyList<CouncilArea> Current => Loaded != null && Loaded.areas != null && Loaded.areas.Count > 0 ? Loaded.areas : CouncilAreaRules.Defaults;

    public static int CurrentMaxDistance => Loaded != null ? Loaded.maxDistance : CouncilAreaRules.DefaultMaxDistance;
}

