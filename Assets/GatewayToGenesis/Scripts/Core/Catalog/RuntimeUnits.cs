using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>What a resource made during play is (its GameUnit's fields): kept in the save so the slot can be rebuilt before its state loads.</summary>
[Serializable]
public class RuntimeUnitRecord
{
    public string name, type, section, description;
    /// <summary>The unit whose icon it borrows (an invented dish borrows its template's).</summary>
    public string iconFrom;
}

/// <summary>
/// Resources made during play rather than authored under Resources/GameUnits: the dishes and drinks the people invent
/// and name (<see cref="CultureSystem"/>). <see cref="GameCatalog.IsResource"/> and
/// <see cref="GameUnitsLogic.GetGameUnitByName"/> find them beside the catalog; a save keeps their records
/// (<see cref="SaveDocument.runtimeUnits"/>) and <see cref="GameSnapshot.PrepareSlots"/> makes them again first.
/// </summary>
public static class RuntimeUnits
{
    private static readonly Dictionary<string, (GameUnit unit, RuntimeUnitRecord record)> _units =
        new Dictionary<string, (GameUnit, RuntimeUnitRecord)>(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<RuntimeUnitRecord> Records => _units.Values.Select(v => v.record);

    public static bool Contains(string name) => !string.IsNullOrEmpty(name) && _units.ContainsKey(name);

    public static bool TryGet(string name, out GameUnit unit)
    {
        unit = null;
        if (string.IsNullOrEmpty(name) || !_units.TryGetValue(name, out var entry)) return false;
        unit = entry.unit;
        return unit != null;
    }

    /// <summary>The unit for <paramref name="record"/>: made the first time, its description and icon brought up to date after.</summary>
    public static GameUnit Ensure(RuntimeUnitRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.name)) return null;
        if (!_units.TryGetValue(record.name, out var entry) || entry.unit == null)
        {
            var created = ScriptableObject.CreateInstance<GameUnit>();
            ((UnityEngine.Object)created).name = record.name;
            created.hideFlags = HideFlags.DontSave;
            entry = (created, record);
        }
        var unit = entry.unit;
        unit.name = record.name;
        unit.type = record.type ?? string.Empty;
        unit.section = record.section ?? string.Empty;
        unit.description = record.description ?? string.Empty;
        if (!string.IsNullOrEmpty(record.iconFrom) && !string.Equals(record.iconFrom, record.name, StringComparison.OrdinalIgnoreCase))
        {
            GameUnit source = GameCatalog.Units.TryGet(record.iconFrom, out var authored) ? authored : TryGet(record.iconFrom, out var made) ? made : null;
            if (source != null && source.icon != null) unit.icon = source.icon;
        }
        _units[record.name] = (unit, record);
        return unit;
    }

    /// <summary>Forget every made unit (a new world), or replace them with a save's (before its slots are made).</summary>
    public static void Restore(IEnumerable<RuntimeUnitRecord> records)
    {
        Clear();
        foreach (var r in records ?? Enumerable.Empty<RuntimeUnitRecord>()) Ensure(r);
    }

    // Statics survive between play sessions when domain reload is off: each session starts with none.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear() => _units.Clear();
}
