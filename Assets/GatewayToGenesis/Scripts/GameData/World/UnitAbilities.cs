using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// An ability a unit works at where it stands: the task it becomes, how much ground it reaches (a hex, the hexes
/// around, or the whole meso hex), how long it takes and what it costs the unit while it works. Its effect is
/// applied by <see cref="WorldSystem"/> when the work is done.
/// </summary>
public sealed class AbilityInfo
{
    public UnitAbility ability;
    public UnitTask task;
    /// <summary>Its name on the unit's card.</summary>
    public string name;
    /// <summary>What the unit is doing while it works ("surveying").</summary>
    public string doing;
    /// <summary>One line for the card: what it does.</summary>
    public string description;
    public AbilityReach reach;
    /// <summary>The micro hexes around it that it reaches (<see cref="AbilityReach.Around"/>).</summary>
    public Func<UnitSpec, int> radius = _ => 0;
    /// <summary>Sevenths the work takes (before <see cref="UnitAbilities.Duration"/> scales it).</summary>
    public Func<UnitSpec, float> sevenths = _ => 1f;
    /// <summary>Fatigue while working, times the unit's <see cref="UnitSpec.workFatiguePerSeventh"/> (walking a whole hex tires more).</summary>
    public float fatigue = 1f;
    /// <summary>Whether its duration shrinks with the share of its ground already done (a survey of a half-surveyed hex is quicker).</summary>
    public bool scalesWithGround;
}

/// <summary>
/// The abilities units work at on the land (groundwork for more: each names its reach, time and toll, and its effect is
/// one case in <see cref="WorldSystem"/>). Surveys work hex by hex: each micro hex reached becomes surveyed, and a meso
/// cell whose every hex is surveyed is explored, its sites investigated. With no scene state.
///
/// Adding an ability: a <see cref="UnitAbility"/> flag and a <see cref="UnitTask"/>, an entry here (and in
/// <see cref="All"/>), its spec numbers in <see cref="UnitSpec"/>, and its effect in WorldSystem.FinishWork.
/// </summary>
public static class UnitAbilities
{
    public static readonly AbilityInfo Survey = new AbilityInfo
    {
        ability = UnitAbility.Survey,
        task = UnitTask.Survey,
        name = "Survey",
        doing = "surveying",
        description = "survey the micro hexes around it",
        reach = AbilityReach.Around,
        radius = s => s != null ? Math.Max(0, s.surveyRadius) : 0,
        sevenths = s => s != null ? s.surveySevenths : 1f,
    };

    public static readonly AbilityInfo SurveyMeso = new AbilityInfo
    {
        ability = UnitAbility.SurveyMeso,
        task = UnitTask.SurveyMeso,
        name = "Survey meso hex",
        doing = "sweeping the meso hex",
        description = "walk and survey all seven micro hexes of the meso hex it stands in",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.mesoSurveySevenths : 2.5f,
        fatigue = 1.5f,
        scalesWithGround = true,
    };

    public static readonly AbilityInfo Forage = new AbilityInfo
    {
        ability = UnitAbility.Forage,
        task = UnitTask.Forage,
        name = "Forage",
        doing = "foraging",
        description = "gather what the meso hex's ground gives, once an Age, for your stores",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.forageSevenths : 1f,
    };

    public static readonly AbilityInfo Improve = new AbilityInfo
    {
        ability = UnitAbility.Improve,
        task = UnitTask.Improve,
        name = "Improve hotspot",
        doing = "improving",
        description = "raise what the meso hex's resource hotspot yields",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.workSevenths : 2f,
        fatigue = 1.5f,
    };

    /// <summary>Every ability worked at on the land, in the order the card lists them.</summary>
    public static readonly IReadOnlyList<AbilityInfo> All = new[] { Survey, SurveyMeso, Forage, Improve };

    public static AbilityInfo For(UnitTask task) => All.FirstOrDefault(a => a.task == task);

    public static AbilityInfo For(UnitAbility ability) => All.FirstOrDefault(a => a.ability == ability);

    /// <summary>The micro hexes an ability reaches from where the unit stands.</summary>
    public static List<HexCoord> Area(WorldMap map, WorldUnit unit, UnitSpec spec, AbilityInfo ability) =>
        ability == null ? new List<HexCoord>() : MicroNavigation.Area(map, WorldUnits.MicroPosition(unit), ability.reach, ability.radius(spec)).ToList();

    /// <summary>What a survey still has to do: the hexes in reach that can be entered and are not surveyed yet.</summary>
    public static List<HexCoord> Targets(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        var grid = MicroNavigation.Grid(map, settings);
        return Area(map, unit, spec, ability).Where(c =>
        {
            int id = MicroNavigation.Index(map, c);
            return id >= 0 && !MicroNavigation.Surveyed(map, id) && !float.IsPositiveInfinity(grid.Enter(id));
        }).ToList();
    }

    /// <summary>Sevenths the work takes for this unit here (a meso survey of a partly surveyed hex takes its share, never less than a fifth).</summary>
    public static float Duration(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        if (ability == null) return 0f;
        float sevenths = Math.Max(0.1f, ability.sevenths(spec));
        if (!ability.scalesWithGround) return sevenths;
        var grid = MicroNavigation.Grid(map, settings);
        var area = Area(map, unit, spec, ability).Where(c => !float.IsPositiveInfinity(grid.Enter(MicroNavigation.Index(map, c)))).ToList();
        if (area.Count == 0) return sevenths;
        int left = area.Count(c => !MicroNavigation.Surveyed(map, c));
        return Math.Max(sevenths * 0.2f, sevenths * left / area.Count);
    }
}
