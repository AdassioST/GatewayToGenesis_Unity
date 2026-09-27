/// <summary>
/// Scoped target keys used by every <see cref="ModifierLedger"/>.
///
/// A modifier can be stored against a single item ("Food"), a section ("section:Vital Resource"),
/// a unit type ("type:Workshop") or everything ("*"). Consumers resolve all four scopes when they
/// evaluate an item, so a bonus registered before the item exists applies automatically the moment
/// it appears. No "pending bonus" bookkeeping is needed anywhere.
/// </summary>
public static class ModifierTargets
{
    public const string All = "*";

    private const string SectionPrefix = "section:";
    private const string TypePrefix = "type:";

    public static string Section(string sectionName) => string.IsNullOrEmpty(sectionName) ? null : SectionPrefix + sectionName;

    public static string Type(string typeName) => string.IsNullOrEmpty(typeName) ? null : TypePrefix + typeName;

    public static bool IsSection(string key) => key != null && key.StartsWith(SectionPrefix, System.StringComparison.OrdinalIgnoreCase);

    public static bool IsType(string key) => key != null && key.StartsWith(TypePrefix, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Human-readable form of a key for tooltips ("section:Heartlands" becomes "Heartlands section").</summary>
    public static string Describe(string key)
    {
        if (key == All) return "everything";
        if (IsSection(key)) return key.Substring(SectionPrefix.Length) + " section";
        if (IsType(key)) return "all " + key.Substring(TypePrefix.Length);
        return key;
    }

    /// <summary>Total modifier affecting one item, across its own name, section, type and the global scope.</summary>
    public static ModifierValue Resolve(ModifierLedger ledger, string itemName, string sectionName, string typeName)
    {
        if (ledger == null) return default;
        var total = ledger.Total(All);
        if (!string.IsNullOrEmpty(itemName)) total += ledger.Total(itemName);
        if (!string.IsNullOrEmpty(sectionName)) total += ledger.Total(Section(sectionName));
        if (!string.IsNullOrEmpty(typeName)) total += ledger.Total(Type(typeName));
        return total;
    }

    public static ModifierValue Resolve(ModifierLedger ledger, GameUnit unit)
    {
        if (unit == null) return ledger != null ? ledger.Total(All) : default;
        return Resolve(ledger, unit.name, unit.section, unit.type);
    }

    /// <summary>True when a scoped key covers the given item.</summary>
    public static bool Covers(string key, string itemName, string sectionName, string typeName)
    {
        if (key == All) return true;
        if (IsSection(key)) return string.Equals(key.Substring(SectionPrefix.Length), sectionName, System.StringComparison.OrdinalIgnoreCase);
        if (IsType(key)) return string.Equals(key.Substring(TypePrefix.Length), typeName, System.StringComparison.OrdinalIgnoreCase);
        return string.Equals(key, itemName, System.StringComparison.OrdinalIgnoreCase);
    }
}
