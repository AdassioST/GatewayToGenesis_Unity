using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Local cultures on the world map's cards (T02): a settlement's own customs, where each came from, the roads it is in
/// contact by, who arrived there, and the gatherings it can hold; a cultural party's repertoire and the customs it could
/// take up where it stands. Text and actions only: every rule and command lives in CultureSystem / WorldSystem.
/// </summary>
public static class LocalCultureCard
{
    /// <summary>A settlement's customs and its gatherings, appended to its card.</summary>
    public static void Settlement(Settlement s, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var culture = CultureSystem.Instance;
        if (culture == null || !CultureSystem.IsFounded || s == null) return;
        var profile = culture.LocalProfile(s.id);
        var kept = profile.Kept.ToList();
        var met = profile.Met.Where(p => p.exposure >= 0.01f).ToList();
        if (kept.Count > 0)
            text.AppendLine(TooltipText.Row("Customs", string.Join("; ", kept.Select(p =>
                $"{p.name}{(p.stage == LocalPracticeStage.Quiet ? " (quiet)" : string.Empty)}{(p.recognized ? " (recognised by the nation, kept in its own way)" : string.Empty)}, {p.provenanceText}"))));
        if (met.Count > 0)
            text.AppendLine(TooltipText.Row("Known of", string.Join("; ", met.Select(p => $"{p.name} ({p.exposure:P0}, {p.provenanceText})"))));
        var edges = culture.ContactEdges().Where(e => e.a == s.id || e.b == s.id).ToList();
        if (edges.Count > 0)
            text.AppendLine(TooltipText.Row("In contact by road", string.Join(", ", edges.Select(e => $"{(e.a == s.id ? e.bName : e.aName)}{(e.open ? string.Empty : $" (interrupted: {e.blockedBy})")}"))));
        var (known, places, unknown) = culture.ArrivalTotals(s.id);
        if (known + places + unknown > 0)
            text.AppendLine(TooltipText.Row("Arrivals", $"{known} from your settlements, {places} found in the wilds (customs not recorded), {unknown} of unknown origin"));
        foreach (var (spec, why, preview) in culture.LocalGatheringChoices(s))
        {
            string id = spec.id;
            actions.Add(($"Gather for {spec.name} ({culture.LocalGatheringCostText}): {preview}", why, () => culture.HoldLocalGathering(s, id)));
        }
        // Shared tables (T05): who has had a place at them here, and a way to set one.
        var access = culture.TableAccess().rows.FirstOrDefault(r => r.settlement == s.id);
        if (access != null && access.tables > 0)
            text.AppendLine(TooltipText.Row("Shared tables", $"{access.tables}; reached {access.reach:P0}"
                + (access.shared.Length > 0 ? $"; {string.Join(", ", access.shared)} shared openly" : access.privateOnly.Length > 0 ? $"; {string.Join(", ", access.privateOnly)} only for a patron's guests" : string.Empty)));
        int settlement = s.id;
        actions.Add(("Set a communal table... (choose who it is for and what to serve)", null, () => CultureWindow.OpenTables(settlement)));
    }

    /// <summary>A cultural party's repertoire, and the customs it can take up or set down where it stands.</summary>
    public static void Party(WorldSystem world, WorldUnit unit, StringBuilder text, List<(string label, string why, Action call)> actions)
    {
        var culture = CultureSystem.Instance;
        if (culture == null || world == null || unit == null || !WorldUnits.Can(world.SpecOf(unit), UnitAbility.Celebrate)) return;
        var carried = culture.CarriedBy(unit.id);
        text.AppendLine(TooltipText.Row("Repertoire", carried.Count == 0
            ? "no customs yet: take some up in a settlement that keeps them"
            : string.Join(", ", carried.Select(c => $"{c.name} (as {c.fromName} keeps it)"))));
        foreach (var c in carried)
        {
            string id = c.practice;
            actions.Add(($"Set down {c.name}", null, () => world.DropPractice(unit, id)));
        }
        foreach (var (practice, why) in world.CarryChoices(unit))
        {
            if (carried.Any(c => c.practice == practice)) continue;
            string id = practice;
            actions.Add(($"Take up {LocalPracticeCatalog.NameOf(id)} (to perform at its festivals)", why, () => world.CarryPractice(unit, id)));
        }
        // Its performance (T08): what it plans for its festival, and where to choose one.
        var planned = culture.PerformanceOf(unit.id);
        text.AppendLine(TooltipText.Row("Performance", planned == null ? "none planned: choose a piece and its purpose for its next festival"
            : $"{planned.name} for {PerformanceRules.IntentName(planned.intent)} in {planned.place}, by {string.Join(", ", planned.cast)} ({(planned.status == PerformanceStatus.Performing ? "under way" : "at the end of its festival")})"));
        int party = unit.id;
        actions.Add((planned == null ? "Plan a performance... (the piece, its purpose, and how it would land)" : "Performance... (why it lands as it does, or call it off)", null, () => CultureWindow.OpenPerformances(party)));
    }
}
