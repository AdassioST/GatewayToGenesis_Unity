using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Startup check of all authored content against the catalogs. Every problem is one warning naming the
/// asset and field to fix; nothing is changed. Effects are checked with <see cref="EffectRouter.Validate"/>,
/// the same rules used when they are applied, so validation and gameplay cannot disagree.
///
/// Add a content type: write a Validate* method below and call it from <see cref="ValidateAll"/>.
/// </summary>
public static class ContentValidator
{
    private const LogChannel Log = LogChannel.Content;
    private static bool _hasRun;

    // Statics survive between play sessions when domain reload is disabled.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlaySession() => _hasRun = false;

    /// <summary>Validate everything once per play session. Returns the number of problems found.</summary>
    /// <param name="defaultSeats">Council seat configuration from the scene, which is not an asset.</param>
    public static int ValidateAll(IEnumerable<DefaultSeatTemplate> defaultSeats = null, bool force = false)
    {
        if (_hasRun && !force) return 0;
        _hasRun = true;

        var problems = new List<string>();
        foreach (var type in EffectRouter.MissingHandlers()) problems.Add($"EffectRouter has no handler for GameEffectType.{type}");

        ValidateLegends(problems);
        ValidateCivics(problems);
        ValidateWeather(problems);
        ValidateResourceRoles(problems);
        ValidateProductionUnits(problems);
        ValidateTechnologies(problems);
        if (defaultSeats != null) ValidateDefaultSeats(defaultSeats, problems);

        foreach (var problem in problems) GameLog.Warning(problem, Log);
        GameLog.Event($"Validated {GameCatalog.Legends.Count} legends, {GameCatalog.Civics.Count} civics, {GameCatalog.Weather.Count} weather profiles, " +
                      $"{GameCatalog.ProductionUnits.Count} production units, {GameCatalog.Technologies.Count} technologies: {problems.Count} problem(s)", Log);
        return problems.Count;
    }

    private static void CheckEffect(in GameEffect effect, string owner, List<string> problems)
    {
        foreach (var problem in EffectRouter.Validate(effect)) problems.Add($"{owner}: {effect.type} {problem}");
    }

    private static void ValidateLegends(List<string> problems)
    {
        foreach (var legend in GameCatalog.Legends.All)
        {
            string owner = $"Legend '{legend.legendName}'";
            foreach (var bonus in legend.bonuses ?? new List<LegendBonus>())
            {
                if (bonus == null) problems.Add($"{owner} has an empty bonus entry");
                else CheckEffect(bonus.ToEffect(), owner, problems);
            }
        }
    }

    private static void ValidateCivics(List<string> problems)
    {
        foreach (var civic in GameCatalog.Civics.All)
        {
            string owner = $"Civic '{civic.civicName}'";
            foreach (var effect in civic.effects ?? new List<CivicEffect>())
            {
                if (effect == null) problems.Add($"{owner} has an empty effect entry");
                else CheckEffect(effect.ToEffect(), owner, problems);
            }
            foreach (var requirement in civic.requirements ?? new List<CivicRequirement>())
            {
                // Checked as the event condition it becomes, with the same rules as story requirements.
                var condition = requirement?.ToCondition();
                if (requirement == null) problems.Add($"{owner} has an empty requirement entry");
                else if (condition == null) problems.Add($"{owner}: {requirement.requirementType} requirements are not implemented yet and always pass");
                else EventContentCheck.Conditions($"{owner} requirement", new[] { condition }, problems);
            }
            foreach (var name in (civic.conflictingCivics ?? new List<string>()).Concat(civic.requiredCivics ?? new List<string>()))
            {
                if (!GameCatalog.Civics.Contains(name)) problems.Add($"{owner} refers to unknown civic '{name}'");
            }
            if (civic.grantsCouncilPosition)
            {
                var (isValid, error) = civic.ValidateLegendClassRestrictions();
                if (!isValid) problems.Add($"{owner}: {error}");
                foreach (var bonus in civic.councilPosition?.bonuses ?? new CivicSeatBonus[0])
                {
                    if (bonus != null && bonus.bonusType != SeatBonusType.CivicBonus) CheckEffect(bonus.ToEffect(), $"{owner} council seat", problems);
                }
            }
        }
    }

    private static void ValidateWeather(List<string> problems)
    {
        foreach (var profile in GameCatalog.Weather.All)
        {
            string owner = $"Weather '{profile.name}'";
            foreach (var effect in profile.effects ?? new List<WeatherProfileSO.WeatherEffect>())
            {
                if (effect != null) CheckEffect(effect.ToEffect(), owner, problems);
            }
            foreach (var condition in profile.availabilityConditions ?? new List<WeatherCondition>())
            {
                if (condition == null) { problems.Add($"{owner} has an empty availability condition"); continue; }
                if (WeatherCondition.DomainOf(condition.type) == null) problems.Add($"{owner} has an unknown condition type {(int)condition.type}");
                else if (condition.type != WeatherCondition.ConditionType.CycleCheck && string.IsNullOrEmpty(condition.targetName)) problems.Add($"{owner}: {condition.type} names no target");
                else if (condition.type == WeatherCondition.ConditionType.TechnologyCheck && !GameCatalog.Technologies.Contains(condition.targetName))
                {
                    problems.Add($"{owner}: TechnologyCheck names '{condition.targetName}', which is not a technology");
                }
            }
            foreach (var echo in profile.requiredEchoes ?? new List<EchoType>())
            {
                if (!System.Enum.IsDefined(typeof(EchoType), echo)) problems.Add($"{owner} requires unknown echo {(int)echo}");
            }
            if (!profile.ValidatePhasePercentages()) problems.Add($"{owner}: phase durations add up to {profile.GetTotalPhasePercentage():0.#}% (allowed 50-150%, 100% intended)");
        }
    }

    private static void ValidateDefaultSeats(IEnumerable<DefaultSeatTemplate> seats, List<string> problems)
    {
        foreach (var seat in seats)
        {
            if (seat == null || string.IsNullOrEmpty(seat.title)) continue;
            string owner = $"Default seat '{seat.title}'";
            if (seat.allowedClasses == null || seat.allowedClasses.Length == 0) problems.Add($"{owner} allows no legend classes, so no legend can sit in it");
            foreach (var bonus in seat.bonuses ?? new DefaultSeatBonus[0])
            {
                if (bonus == null || bonus.bonusType == SeatBonusType.CivicBonus) continue;
                var effect = new SeatBonus { bonusType = bonus.bonusType, targetStat = bonus.targetStat, modifierValue = bonus.modifierValue, modifierType = bonus.modifierType }.ToEffect();
                CheckEffect(effect, owner, problems);
            }
        }
    }

    // Population finds its food and research by role, so each role needs exactly one resource.
    private static void ValidateResourceRoles(List<string> problems)
    {
        foreach (ResourceRole role in System.Enum.GetValues(typeof(ResourceRole)))
        {
            if (role == ResourceRole.None) continue;
            var holders = GameCatalog.Resources.All.Where(r => r.role == role).Select(r => r.name).ToList();
            if (holders.Count == 0) problems.Add($"No resource has role {role}: set GameUnit.role on one asset in Resources/{GameCatalog.Resources.ResourcesPath}");
            else if (holders.Count > 1) problems.Add($"Resources {string.Join(", ", holders)} all have role {role}; only '{holders[0]}' is used");
        }
        foreach (var unit in GameCatalog.Units.All)
        {
            if (unit.role != ResourceRole.None && !GameCatalog.IsResource(unit.name))
            {
                problems.Add($"Game unit '{unit.name}' has role {unit.role} but is not a resource (only assets in Resources/{GameCatalog.Resources.ResourcesPath} can have a role)");
            }
        }
    }

    private static void ValidateProductionUnits(List<string> problems)
    {
        foreach (var unit in GameCatalog.ProductionUnits.All)
        {
            string owner = $"Production unit '{unit.gameUnit.name}' ({unit.name})";
            CheckResourceList(owner, "build requirements", unit.buildResourceRequirements, unit.buildRequirementsAmount.Count, problems);
            CheckResourceList(owner, "produced resources", unit.producedResources, unit.productionRates.Count, problems);
            CheckResourceList(owner, "consumed resources", unit.consumedResources, unit.consumeRates.Count, problems);
            CheckResourceList(owner, "storage resources", unit.storageResources, unit.storageAmount.Count, problems);
        }
    }

    private static void ValidateTechnologies(List<string> problems)
    {
        foreach (var tech in GameCatalog.Technologies.All)
        {
            string owner = $"Technology '{(tech.gameUnit != null ? tech.gameUnit.name : tech.name)}'";
            if (tech.gameUnit == null) problems.Add($"{owner} has no GameUnit");
            foreach (var required in tech.techRequirements)
            {
                if (!GameCatalog.Technologies.Contains(required)) problems.Add($"{owner} requires unknown technology '{required}'");
            }
            CheckResourceList(owner, "research cost", tech.resourceRequirements, tech.resourceAmount.Count, problems);
            foreach (var unlockable in tech.techUnlockables)
            {
                if (unlockable == null) problems.Add($"{owner} has an empty unlockable entry");
                else if (unlockable.gameUnit == null && unlockable.unlockableType != TechUnlockableType.Special && unlockable.unlockableType != TechUnlockableType.DemandModifier)
                {
                    problems.Add($"{owner} unlockable '{unlockable.name}' ({unlockable.unlockableType}) has no GameUnit");
                }
            }
        }
    }

    private static void CheckResourceList(string owner, string label, List<string> resources, int amountCount, List<string> problems)
    {
        if (resources == null) return;
        if (resources.Count != amountCount) problems.Add($"{owner}: {label} lists {resources.Count} resources but {amountCount} amounts");
        foreach (var resource in resources)
        {
            if (!GameCatalog.IsResource(resource)) problems.Add($"{owner}: {label} names '{resource}', which is not a resource in Resources/{GameCatalog.Resources.ResourcesPath}");
        }
    }
}
