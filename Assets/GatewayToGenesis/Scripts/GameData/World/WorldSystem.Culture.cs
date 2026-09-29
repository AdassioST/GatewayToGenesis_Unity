using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Expedition charters and the culture's festivals on the map. An expedition forms with a charter
/// (<see cref="ExpeditionCharter"/>): to explore, to build (Builders, once the land can be improved) or to celebrate (a
/// Cultural Party, once the culture is founded and <see cref="CultureLifeTuning.festivalTechnology"/> is researched).
/// A cultural party standing in one of your settlements holds a festival there: its table is set from the stores as it
/// begins, and when it ends the culture celebrates (<see cref="CultureSystem.CelebrateFestival"/>) and each legend of
/// the party earns Fragments of Meaning.
/// </summary>
public partial class WorldSystem
{
    /// <summary>"Expedition", "Builders", "Cultural Party" (plural: "Expeditions", "Builders", "Cultural Parties").</summary>
    public static string CharterName(ExpeditionCharter charter, bool plural = true)
    {
        switch (charter)
        {
            case ExpeditionCharter.Builders: return "Builders";
            case ExpeditionCharter.CulturalParty: return plural ? "Cultural Parties" : "Cultural Party";
            default: return plural ? "Expeditions" : "Expedition";
        }
    }

    /// <summary>One line for the forming card: what a charter's party does.</summary>
    public static string CharterDescription(ExpeditionCharter charter)
    {
        switch (charter)
        {
            case ExpeditionCharter.Builders: return "works the land only: improves hotspots, plants seeds and forages, faster and wearing less, but surveys nothing and escorts no settlers";
            case ExpeditionCharter.CulturalParty: return "holds festivals in your settlements: Unity, morale, eased Composure, the culture taking root around them; it carries the customs it takes up from one settlement to the next";
            default: return "explores: surveys, forages, harvests, investigates ruins, escorts settlers and improves hotspots once it knows how";
        }
    }

    /// <summary>Why no party of this charter can form now, or null.</summary>
    public string WhyNotCharter(ExpeditionCharter charter)
    {
        switch (charter)
        {
            case ExpeditionCharter.Builders:
                return CanImprove ? null : $"Research {ExpeditionRules.improveTechnology} to send builders.";
            case ExpeditionCharter.CulturalParty:
                var culture = CultureSystem.Instance;
                if (culture == null || !CultureSystem.IsFounded) return "A cultural party carries the culture's songs: found the culture first (Horology).";
                return culture.FestivalsUnlocked ? null : $"Research {culture.Life.festivalTechnology} to send cultural parties.";
            default: return null;
        }
    }

    /// <summary>The next charter after <paramref name="charter"/>, for the forming card's cycle button.</summary>
    public static ExpeditionCharter NextCharter(ExpeditionCharter charter) =>
        charter == ExpeditionCharter.Expedition ? ExpeditionCharter.Builders : charter == ExpeditionCharter.Builders ? ExpeditionCharter.CulturalParty : ExpeditionCharter.Expedition;

    /// <summary>The settlement standing in the unit's cell, or null.</summary>
    public Settlement SettlementAt(WorldUnit unit)
    {
        var t = unit != null && Map != null ? Map.Get(unit.coord) : null;
        return t != null && t.settlement >= 0 && t.settlement < Map.Settlements.Count ? Map.Settlements[t.settlement] : null;
    }

    /// <summary>Why the party cannot hold a festival where it stands, or null (its table included).</summary>
    public string WhyNotFestival(WorldUnit unit)
    {
        if (unit == null) return "No party selected.";
        if (!WorldUnits.Can(SpecOf(unit), UnitAbility.Celebrate)) return "Only a cultural party holds festivals.";
        if (unit.Moving) return "Stop the party first.";
        if (unit.Working) return "Already at work.";
        var culture = CultureSystem.Instance;
        if (culture == null) return "There is no culture to celebrate.";
        var s = SettlementAt(unit);
        if (s == null) return "A festival is held in one of your settlements: walk the party into one.";
        return culture.WhyNotFestival(s);
    }

    /// <summary>What the festival where the party stands would set on the table ("12 food value"), or null.</summary>
    public string FestivalCostText(WorldUnit unit)
    {
        var s = SettlementAt(unit);
        return s == null || CultureSystem.Instance == null ? null : $"{CultureSystem.Instance.FestivalFoodValue(s):0.#} food value";
    }

    /// <summary>Begin a festival where the party stands: the table is set from the stores now, the celebration lasts its Sevenths.</summary>
    public bool HoldFestival(WorldUnit unit)
    {
        string why = WhyNotFestival(unit);
        if (why != null) { Say(why); return false; }
        var s = SettlementAt(unit);
        if (!CultureSystem.Instance.PrepareFestival(s)) { Say("The stores cannot set the festival's table."); return false; }
        StartTask(unit, SpecOf(unit), UnitAbilities.Festival);
        Say($"{unit.name} begins a festival in {s.name}.");
        return true;
    }

    private void FinishFestival(WorldUnit unit, List<string> notice)
    {
        var s = SettlementAt(unit);
        var culture = CultureSystem.Instance;
        if (s == null || culture == null) return;
        var members = Expeditions.Members(unit).Where(m => !string.IsNullOrEmpty(m)).ToList();
        string text = culture.CelebrateFestival(s, members);
        if (string.IsNullOrEmpty(text)) return;
        // The party's repertoire, performed: the host sees each custom it carried (once per festival).
        string visit = CompleteCulturalVisit(unit, s, members);
        if (visit != null) text += " " + visit;
        // Its planned performance, given once with the voices still there (T08).
        string performance = CompleteCulturalPerformance(unit, s, members);
        if (performance != null) text += " " + performance;
        int fragments = culture.Life.festivalFragments;
        var legends = LegendProgress.Instance;
        if (fragments > 0) foreach (var member in members) legends?.Award(member, FragmentKind.Meaning, fragments, $"A festival in {s.name}");
        notice.Add($"{UnitLabel(unit)}: {text}");
        Map.CivilizationVersion++;
    }
}
