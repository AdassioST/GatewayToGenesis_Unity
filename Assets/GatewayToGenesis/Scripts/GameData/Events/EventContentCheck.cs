using System;
using System.Collections.Generic;

/// <summary>
/// Checks that the names an Ink story uses exist in the game's catalogs: resources, buildings, sections,
/// technologies, weather profiles, civics and government types. A typo ("Old World Materials" for the
/// "Old World Relics" section) would otherwise parse fine and silently do nothing in play. Used by
/// <see cref="EventStoryIndex.Validate"/> at start-up and by the EditMode content tests. A catalog with no
/// content (a test scene without Resources) is not checked.
/// </summary>
public static class EventContentCheck
{
    public static void Consequences(string owner, IEnumerable<EventConsequence> consequences, List<string> problems)
    {
        if (consequences == null) return;
        foreach (var c in consequences)
        {
            if (c == null) continue;
            string kind = UnknownKind(c);
            if (kind != null) problems.Add($"{owner}: {c.type} names unknown {kind} '{c.targetName}'.");
        }
    }

    public static void Conditions(string owner, IEnumerable<EventCondition> conditions, List<string> problems)
    {
        if (conditions == null) return;
        foreach (var c in conditions)
        {
            if (c == null) continue;
            string kind = UnknownKind(c);
            if (kind != null) problems.Add($"{owner}: condition '{c.Domain}:{c.targetName}' names unknown {kind}.");
        }
    }

    /// <summary>What kind of name <paramref name="c"/> needs, when it names something that does not exist; null when fine.</summary>
    public static string UnknownKind(EventConsequence c)
    {
        switch (c.type)
        {
            case EventConsequence.ConsequenceType.ResourceChange:
            case EventConsequence.ConsequenceType.ClickPowerChange:
            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                return Known(GameCatalog.Resources.Count, GameCatalog.Resources.Contains(c.targetName), "resource");
            case EventConsequence.ConsequenceType.ProductionPercentChange:
                return Known(GameCatalog.Units.Count, GameCatalog.Units.Contains(c.targetName), "resource or building");
            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                return Known(GameCatalog.Sections.Count, GameCatalog.Sections.Contains(c.targetName), "section");
            case EventConsequence.ConsequenceType.ProductionUnitChange:
                return Known(GameCatalog.ProductionUnits.Count, GameCatalog.ProductionUnits.Contains(c.targetName), "building");
            case EventConsequence.ConsequenceType.TechnologyEnlightened:
                return Known(GameCatalog.Technologies.Count, GameCatalog.Technologies.Contains(c.targetName), "technology");
            case EventConsequence.ConsequenceType.WeatherChange:
                if (string.Equals(c.targetName, "clear", StringComparison.OrdinalIgnoreCase)) return null;
                return Known(GameCatalog.Weather.Count, GameCatalog.FindWeather(c.targetName) != null, "weather profile");
            default:
                return null;
        }
    }

    public static string UnknownKind(EventCondition c)
    {
        switch (c.Domain)
        {
            case "resource":
            case "resource_capacity":
            case "production_rate":
            case "clicked":
                return Known(GameCatalog.Resources.Count, GameCatalog.Resources.Contains(c.targetName), "resource");
            case "building":
                return Known(GameCatalog.ProductionUnits.Count, GameCatalog.ProductionUnits.Contains(c.targetName), "building");
            case "technology":
                return Known(GameCatalog.Technologies.Count, GameCatalog.Technologies.Contains(c.targetName), "technology");
            case "civic":
                return Known(GameCatalog.Civics.Count, GameCatalog.Civics.Contains(c.targetName), "civic");
            case "weather":
                return Known(GameCatalog.Weather.Count, GameCatalog.FindWeather(c.targetName) != null, "weather profile");
            case "government":
                return Enum.TryParse(c.targetName, true, out GovernmentType _) ? null : "government type";
            case "stat":
                return StatDefinitions.IsKnown(c.targetName) ? null : "stat";
            default:
                return null;
        }
    }

    private static string Known(int catalogSize, bool found, string kind) => catalogSize == 0 || found ? null : kind;
}
