using System;
using System.Collections.Generic;

/// <summary>
/// Technologies renamed by the Act I rework (Docs/Planning/TECH_TREE_ACT_I.md, Sept 29, 2026): the old name still
/// finds the technology, so a save, a story or a setting written before the rename keeps working
/// (<see cref="GameUnitsLogic.GetTechnologySlot"/>, the save's slot records). New content uses the new names.
/// </summary>
public static class TechnologyAliases
{
    public static readonly IReadOnlyDictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Efficient Rations"] = "Shared Embers",
        ["Woodcraft Mastery"] = "Elderwood Felling",
        ["Rites of Harvest"] = "Harvest Hymns",
        ["Agricultural Renewal"] = "Earth-Bean Rows",
        ["Pathfinder Training"] = "Lookout Towers",
        ["Resource Storage"] = "Ash-Cellars",
        ["Advanced Woodworking"] = "Heartwood Joinery",
        ["Edicts of Stone and Bone"] = "Call and Response",
        ["Resource Preservation"] = "Salt and Smoke",
        ["Celestial Astrology"] = "The Stave of Stars",
        ["Vital Winds Mastery"] = "Reading the Vital Winds",
        ["Fortified Living"] = "Stonebound Walls",
    };

    /// <summary>The current name of a technology known by an old one; any other name comes back as given (trimmed).</summary>
    public static string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        string trimmed = name.Trim();
        return Renamed.TryGetValue(trimmed, out var current) ? current : trimmed;
    }

    /// <summary>True when <paramref name="name"/> is an old name of a renamed technology.</summary>
    public static bool IsOldName(string name) => !string.IsNullOrWhiteSpace(name) && Renamed.ContainsKey(name.Trim());
}
