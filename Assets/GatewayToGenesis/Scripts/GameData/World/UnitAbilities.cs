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
/// one case in <see cref="WorldSystem"/>). A cell is explored, its sites investigated, once an expedition has seen all
/// of its hexes in passing, or when one sent to survey it has walked each of them (<see cref="SurveyMeso"/>, one
/// <see cref="HexSevenths"/> of work per hex): the survey is slower and turns up more (WorldSystem.SurveyCell). The
/// older <see cref="Survey"/> of the hexes around a unit is kept for saves made while one was under way. With no scene state.
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
        name = "Survey",
        doing = "surveying",
        description = "walk to each of a meso hex's seven micro hexes and survey it there (WorldSystem.SurveyCell)",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.mesoSurveySevenths : 1.25f,
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

    public static readonly AbilityInfo Harvest = new AbilityInfo
    {
        ability = UnitAbility.Harvest,
        task = UnitTask.Harvest,
        name = "Harvest",
        doing = "harvesting",
        description = "gather an identified resource site's harvest as cargo, once an Age, without claiming the ground",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.harvestSevenths : 1f,
        fatigue = 1.2f,
    };

    public static readonly AbilityInfo Plant = new AbilityInfo
    {
        ability = UnitAbility.Plant,
        task = UnitTask.Plant,
        name = "Plant seeds",
        doing = "planting",
        description = "plant carried seeds on fertile ground you hold",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.plantSevenths : 1.5f,
        fatigue = 1.2f,
    };

    /// <summary>
    /// Investigate the ruins of a fallen settlement where it stands (<see cref="WorldRuins"/>): any unit that surveys can,
    /// so it rides on <see cref="UnitAbility.SurveyMeso"/> rather than a flag of its own.
    /// </summary>
    public static readonly AbilityInfo Investigate = new AbilityInfo
    {
        ability = UnitAbility.SurveyMeso,
        task = UnitTask.Investigate,
        name = "Investigate the ruins",
        doing = "investigating the ruins",
        description = "search the ruins of a fallen settlement for what its failure left behind, once",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.investigateSevenths : 3f,
        fatigue = 1.3f,
    };

    /// <summary>A cultural party's festival in the settlement it stands in (<see cref="CultureSystem.CelebrateFestival"/>).</summary>
    public static readonly AbilityInfo Festival = new AbilityInfo
    {
        ability = UnitAbility.Celebrate,
        task = UnitTask.Festival,
        name = "Hold a festival",
        doing = "celebrating",
        description = "hold a festival in the settlement it stands in: Unity, morale, eased Composure, the culture taking root",
        reach = AbilityReach.Meso,
        sevenths = s => s != null ? s.festivalSevenths : 2f,
        fatigue = 0.5f,
    };

    /// <summary>Every ability worked at on the land, in the order the card lists them.</summary>
    public static readonly IReadOnlyList<AbilityInfo> All = new[] { Survey, SurveyMeso, Forage, Harvest, Plant, Improve, Investigate, Festival };

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

    /// <summary>Sevenths a survey spends at work on one micro hex: its share of <see cref="UnitSpec.mesoSurveySevenths"/>.</summary>
    public static float HexSevenths(UnitSpec spec) => Math.Max(0.05f, SurveyMeso.sevenths(spec) / MicroNavigation.PerCell);

    /// <summary>The micro hexes of <paramref name="tile"/> a survey still has to visit (ids): those that can be entered and are not surveyed yet.</summary>
    public static List<int> CellTargets(WorldMap map, WorldGenSettings settings, WorldTile tile)
    {
        var left = new List<int>();
        if (map == null || tile == null || tile.water) return left;
        var grid = MicroNavigation.Grid(map, settings);
        for (int k = 0; k < MicroNavigation.PerCell; k++)
        {
            int id = tile.index * MicroNavigation.PerCell + k;
            if (!MicroNavigation.Surveyed(map, id) && !float.IsPositiveInfinity(grid.Enter(id))) left.Add(id);
        }
        return left;
    }

    /// <summary>Sevenths the work takes for this unit here (a meso survey of a partly surveyed hex takes its share, never less than a fifth).</summary>
    public static float Duration(WorldMap map, WorldGenSettings settings, WorldUnit unit, UnitSpec spec, AbilityInfo ability)
    {
        if (ability == null) return 0f;
        float sevenths = Math.Max(0.1f, ability.sevenths(spec));
        if (!ability.scalesWithGround) return sevenths;
        // Surveys take longer in dense cover (WorldCover).
        sevenths *= Math.Max(0.2f, map?.Get(unit.coord)?.coverSurvey ?? 1f);
        var grid = MicroNavigation.Grid(map, settings);
        var area = Area(map, unit, spec, ability).Where(c => !float.IsPositiveInfinity(grid.Enter(MicroNavigation.Index(map, c)))).ToList();
        if (area.Count == 0) return sevenths;
        int left = area.Count(c => !MicroNavigation.Surveyed(map, c));
        return Math.Max(sevenths * 0.2f, sevenths * left / area.Count);
    }
}
