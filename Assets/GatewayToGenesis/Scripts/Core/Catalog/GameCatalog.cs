using System;
using System.Collections.Generic;

/// <summary>
/// The single registry of authored content. Every system looks content up here instead of calling
/// Resources.Load itself, so adding a resource, building, technology, civic, legend or weather is
/// a matter of creating an asset in the matching folder.
///
/// Folder contract (all under Assets/Resources):
///   GameUnits/                 every GameUnit (resources, buildings, units, technologies)
///   GameUnits/GameResources/   GameUnits that are storable resources
///   Production/                ProductionUnitData, keyed by its GameUnit name
///   Technology/                TechnologyData, keyed by its GameUnit name
///   TechUnlockables/           TechUnlockable
///   Sections/                  SectionData, keyed by SectionData.name
///   Civics/                    CivicData, keyed by civicName
///   Legends/                   LegendData, keyed by legendName
///   WeatherProfiles/           WeatherProfileSO, keyed by asset name
///   EventArt/                  Sprite, keyed by asset name: splash art named after its story knot (or a story's
///                              splash_art tag), and EventColor_{event type} banners
/// </summary>
public static class GameCatalog
{
    public static readonly AssetCatalog<GameUnit> Units =
        new AssetCatalog<GameUnit>("GameUnits", u => !string.IsNullOrEmpty(u.name) ? u.name : ((UnityEngine.Object)u).name, "game unit");

    public static readonly AssetCatalog<GameUnit> Resources =
        new AssetCatalog<GameUnit>("GameUnits/GameResources", u => !string.IsNullOrEmpty(u.name) ? u.name : ((UnityEngine.Object)u).name, "resource");

    public static readonly AssetCatalog<ProductionUnitData> ProductionUnits =
        new AssetCatalog<ProductionUnitData>("Production", p => p.gameUnit != null ? p.gameUnit.name : null, "production unit");

    public static readonly AssetCatalog<TechnologyData> Technologies =
        new AssetCatalog<TechnologyData>("Technology", t => t.gameUnit != null && !string.IsNullOrEmpty(t.gameUnit.name) ? t.gameUnit.name : t.name, "technology");

    public static readonly AssetCatalog<TechUnlockable> TechUnlockables =
        new AssetCatalog<TechUnlockable>("TechUnlockables", t => t.name, "tech unlockable");

    public static readonly AssetCatalog<SectionData> Sections =
        new AssetCatalog<SectionData>("Sections", s => !string.IsNullOrEmpty(s.name) ? s.name : ((UnityEngine.Object)s).name, "section");

    public static readonly AssetCatalog<CivicData> Civics =
        new AssetCatalog<CivicData>("Civics", c => c.civicName, "civic");

    public static readonly AssetCatalog<LegendData> Legends =
        new AssetCatalog<LegendData>("Legends", l => l.legendName, "legend");

    public static readonly AssetCatalog<WeatherProfileSO> Weather =
        new AssetCatalog<WeatherProfileSO>("WeatherProfiles", w => w.name, "weather profile");

    public static readonly AssetCatalog<UnityEngine.Sprite> EventArt =
        new AssetCatalog<UnityEngine.Sprite>("EventArt", s => s.name, "event art");

    private static HashSet<string> _sectionNames;
    private static HashSet<string> _unitTypes;
    private static Dictionary<ResourceRole, GameUnit> _resourceByRole;

    /// <summary>True for any section that has SectionData or is used by at least one GameUnit.</summary>
    public static bool IsSection(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureDerivedSets();
        return _sectionNames.Contains(name);
    }

    /// <summary>True for any GameUnit.type in use ("Workshop", "Residence", "Unit", ...).</summary>
    public static bool IsUnitType(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureDerivedSets();
        return _unitTypes.Contains(name);
    }

    public static IEnumerable<string> SectionNames
    {
        get
        {
            EnsureDerivedSets();
            return _sectionNames;
        }
    }

    public static bool IsResource(string name) => Resources.Contains(name);

    /// <summary>The resource whose <see cref="GameUnit.role"/> is <paramref name="role"/>, or null (the first one if several claim it).</summary>
    public static GameUnit ResourceFor(ResourceRole role)
    {
        if (role == ResourceRole.None) return null;
        EnsureDerivedSets();
        return _resourceByRole.TryGetValue(role, out var unit) ? unit : null;
    }

    /// <summary>Name of the resource playing <paramref name="role"/>, or null when none does.</summary>
    public static string ResourceNameFor(ResourceRole role)
    {
        var unit = ResourceFor(role);
        return unit != null ? unit.name : null;
    }

    /// <summary>True when the resource named <paramref name="name"/> plays <paramref name="role"/>.</summary>
    public static bool HasRole(string name, ResourceRole role) =>
        role != ResourceRole.None && Resources.TryGet(name, out var unit) && unit.role == role;

    public static bool IsProductionUnit(string name) => ProductionUnits.Contains(name);

    /// <summary>Weather lookup by asset name, falling back to the display name authors see in the inspector.</summary>
    public static WeatherProfileSO FindWeather(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Weather.TryGet(name, out var profile)) return profile;
        foreach (var candidate in Weather.All)
        {
            if (string.Equals(candidate.weatherDisplayName, name, StringComparison.OrdinalIgnoreCase)) return candidate;
        }
        return null;
    }

    /// <summary>Drop every cache so the next lookup reloads from disk.</summary>
    // Also runs at the start of each play session: statics survive when domain reload is disabled,
    // and assets edited between sessions must be picked up.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void InvalidateAll()
    {
        Units.Invalidate();
        Resources.Invalidate();
        ProductionUnits.Invalidate();
        Technologies.Invalidate();
        TechUnlockables.Invalidate();
        Sections.Invalidate();
        Civics.Invalidate();
        Legends.Invalidate();
        Weather.Invalidate();
        EventArt.Invalidate();
        _sectionNames = null;
        _unitTypes = null;
        _resourceByRole = null;
    }

    private static void EnsureDerivedSets()
    {
        if (_sectionNames != null) return;
        _sectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _unitTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Sections.Keys) _sectionNames.Add(key);
        foreach (var unit in Units.All)
        {
            if (!string.IsNullOrEmpty(unit.section)) _sectionNames.Add(unit.section);
            if (!string.IsNullOrEmpty(unit.type)) _unitTypes.Add(unit.type);
        }
        _resourceByRole = new Dictionary<ResourceRole, GameUnit>();
        foreach (var resource in Resources.All)
        {
            if (resource.role != ResourceRole.None && !_resourceByRole.ContainsKey(resource.role)) _resourceByRole[resource.role] = resource;
        }
    }
}
