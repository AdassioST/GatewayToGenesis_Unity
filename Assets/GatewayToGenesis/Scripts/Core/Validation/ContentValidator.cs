using System;
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
    public static int ValidateAll(bool force = false)
    {
        if (_hasRun && !force) return 0;
        _hasRun = true;

        var problems = new List<string>();
        foreach (var type in EffectRouter.MissingHandlers()) problems.Add($"EffectRouter has no handler for GameEffectType.{type}");

        ValidateLegends(problems);
        ValidateCivics(problems);
        ValidateWeather(problems);
        ValidateResourceRoles(problems);
        ValidateKeywords(problems);
        ValidateProductionUnits(problems);
        ValidateTechnologies(problems);
        ValidateCouncilSeats(problems);
        ValidateCouncilAreas(problems);
        ValidateAchievements(problems);
        ValidateAges(problems);
        ValidateWorld(problems);
        ValidatePantry(problems);

        foreach (var problem in problems) GameLog.Warning(problem, Log);
        GameLog.Event($"Validated {GameCatalog.Legends.Count} legends, {GameCatalog.CouncilSeats.Count} council seats, {GameCatalog.Civics.Count} civics, " +
                      $"{GameCatalog.Weather.Count} weather profiles, {GameCatalog.ProductionUnits.Count} production units, {GameCatalog.Technologies.Count} technologies, " +
                      $"{Keywords.Cards.Cards.Count} keyword cards, {Library.Index.Entries.Count} Library entries: {problems.Count} problem(s)", Log);
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
        // Souls: the vault notes are present and authored traits exist in them (the notes' own gaps are the vault's; the import lists them).
        problems.AddRange(LegendLore.Problems());
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
                else if (condition == null) problems.Add($"{owner}: {requirement.requirementType} requirements are not implemented and never pass");
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
                CheckAreas($"{owner} council seat", civic.councilPosition?.areas, problems);
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

    // The note's own shape problems are the vault's to fix (the import lists them); here only the game's side is checked.
    private static void ValidateAchievements(List<string> problems)
    {
        var tracker = Achievements.Tracker;
        if (tracker.All.Count == 0) { problems.Add($"No achievements: Resources/{Achievements.NotePath}.md is missing or empty (run Tools > Gateway to Genesis > Import Lore From Vault)"); return; }
        foreach (var id in AchievementTriggers.Rules.Keys)
        {
            if (tracker.Get(id) == null) problems.Add($"AchievementTriggers has a rule for '{id}', but no achievement has that title any more; follow the vault's new title");
        }
    }

    private static void ValidateCouncilSeats(List<string> problems)
    {
        if (GameCatalog.CouncilSeats.Count == 0) problems.Add("Resources/Council has no council seats, so no council position can open");
        foreach (var seat in GameCatalog.CouncilSeats.All)
        {
            if (seat == null) continue;
            if (string.IsNullOrEmpty(seat.title))
            {
                problems.Add($"Council seat asset '{seat.name}' has no title");
                continue;
            }
            string owner = $"Council seat '{seat.title}'";
            if (seat.allowedClasses == null || seat.allowedClasses.Length == 0) problems.Add($"{owner} allows no legend classes, so no legend can sit in it");
            CheckAreas(owner, seat.areas, problems);
            foreach (var bonus in seat.bonuses ?? new DefaultSeatBonus[0])
            {
                if (bonus == null || bonus.bonusType == SeatBonusType.CivicBonus) continue;
                var effect = new SeatBonus { bonusType = bonus.bonusType, targetStat = bonus.targetStat, modifierValue = bonus.modifierValue, modifierType = bonus.modifierType }.ToEffect();
                CheckEffect(effect, owner, problems);
            }
        }
    }

    // Stories call on seats by area (# cast: area:defense): a seat with no area is never called, and an area the catalog
    // does not know is never matched.
    private static void CheckAreas(string owner, IEnumerable<string> areas, List<string> problems)
    {
        var list = (areas ?? Enumerable.Empty<string>()).ToList();
        if (list.Count == 0) problems.Add($"{owner} answers for no area of affairs, so no story calls on it (set its areas)");
        foreach (var unknown in CouncilAreaRules.Unknown(list, CouncilAreaCatalog.Current))
            problems.Add($"{owner}: area '{unknown}' is not in Resources/Council/Council Areas");
    }

    // The area catalog: unique ids, and every related area one it knows.
    private static void ValidateCouncilAreas(List<string> problems)
    {
        var areas = CouncilAreaCatalog.Current;
        foreach (var group in areas.Where(a => a != null).GroupBy(a => a.id ?? string.Empty, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1 || g.Key.Length == 0))
            problems.Add(group.Key.Length == 0 ? "Council Areas has an area with no id" : $"Council Areas lists '{group.Key}' {group.Count()} times");
        foreach (var area in areas.Where(a => a != null))
            foreach (var unknown in CouncilAreaRules.Unknown(area.related, areas))
                problems.Add($"Council area '{area.id}' is related to unknown area '{unknown}'");
    }

    // Keyword cards and the White-Haven Library: a card must open an entry that exists, and its states must name
    // Ages the Library knows and civics the game has. [[Links]] to terms with no card or entry yet are expected while
    // lore is being written (they show as plain words), so they are listed once, not warned.
    private static void ValidateKeywords(List<string> problems)
    {
        foreach (var problem in Library.Problems) problems.Add(problem);
        var cards = Keywords.Cards;
        foreach (var problem in cards.Problems) problems.Add(problem);

        var civics = new HashSet<string>(GameCatalog.Civics.All.Where(c => c != null).Select(c => LibraryIndex.Slug(c.civicName)), System.StringComparer.OrdinalIgnoreCase);
        var unlinked = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards.Cards)
        {
            string owner = $"Keyword card '{card.title}' (Resources/Keywords/Keywords.md)";
            if (card.wiki != null && Library.Get(card.wiki) == null) problems.Add($"{owner}: wiki: '{card.wiki}' is no White-Haven Library entry");
            if (card.gameId != null && !KeywordLiveValues.Exists(card.gameId)) problems.Add($"{owner}: id: '{card.gameId}' is no game term");
            foreach (var state in card.states)
            {
                if (state.kind == KeywordStateKind.DuringAge && Library.Index.Age(state.target) == null) problems.Add($"{owner}: {state} names no Age of the Library (use an Age's id, like @{GameAge.FirstAge}, or an Age number, like @age-1)");
                if (state.kind == KeywordStateKind.Civic && !civics.Contains(state.target)) problems.Add($"{owner}: {state} names no civic");
                foreach (var target in KeywordMarkup.WikiTargets(state.text)) if (Keywords.Resolve(target) == null) unlinked.Add(target);
            }
        }
        foreach (var entry in Library.Index.Entries.Where(e => e.game))
        {
            foreach (var target in KeywordMarkup.WikiTargets(entry.body)) if (Library.Resolve(target, preferGame: true) == null) unlinked.Add(target);
        }
        foreach (var unit in GameCatalog.Units.All)
        {
            foreach (var target in KeywordMarkup.WikiTargets(unit.description)) if (Keywords.Resolve(target) == null) unlinked.Add(target);
        }
        if (unlinked.Count > 0)
        {
            GameLog.Event($"{unlinked.Count} [[linked]] term(s) have no card or Library entry yet and show as plain words: {string.Join(", ", unlinked)}", Log);
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
            // Enlightenment goals (eurekas): a name no catalog knows can never be met.
            foreach (var condition in tech.enlightenedConditions)
            {
                if (condition == null) { problems.Add($"{owner} has an empty Enlightenment goal"); continue; }
                string goal = $"{owner} Enlightenment goal '{condition.Describe()}'";
                string trigger = (condition.trigger ?? string.Empty).Trim();
                switch (condition.type)
                {
                    case EnlightenedType.GatherResourceFromClick:
                    case EnlightenedType.AccumulateResource:
                    case EnlightenedType.ReachProductionRate:
                        if (!GameCatalog.IsResource(trigger)) problems.Add($"{goal} names unknown resource '{trigger}'");
                        break;
                    case EnlightenedType.ConstructUnit:
                        // "section:X", "type:Y" and "*" count every unit in that scope (GameUnitsLogic.GetProductionUnitCount).
                        if (!trigger.Contains(":") && trigger != "*" && !GameCatalog.IsProductionUnit(trigger)) problems.Add($"{goal} names unknown building or unit '{trigger}'");
                        break;
                    case EnlightenedType.UnlockTechnology:
                        if (!GameCatalog.Technologies.Contains(trigger)) problems.Add($"{goal} names unknown technology '{trigger}'");
                        break;
                    case EnlightenedType.GameValue:
                        condition.SplitTrigger(out string domain, out _);
                        if (!GameValues.IsKnownDomain(domain)) problems.Add($"{goal} reads unknown value domain '{domain}'");
                        break;
                }
            }
            foreach (var unlockable in tech.techUnlockables)
            {
                if (unlockable == null) problems.Add($"{owner} has an empty unlockable entry");
                else if (unlockable.gameUnit == null && unlockable.unlockableType != TechUnlockableType.Special && unlockable.unlockableType != TechUnlockableType.DemandModifier && unlockable.unlockableType != TechUnlockableType.CouncilSeat)
                {
                    problems.Add($"{owner} unlockable '{unlockable.name}' ({unlockable.unlockableType}) has no GameUnit");
                }
            }
        }
        var names = GameCatalog.Technologies.All.Where(t => t.gameUnit != null).Select(t => t.gameUnit.name);
        foreach (var looping in TechTreeRules.Cycles(names, TechnologyTreeLogic.Prerequisites))
            problems.Add($"Technology '{looping}' needs itself through its prerequisites, so it can never be researched");
    }

    private static void ValidateAges(List<string> problems)
    {
        if (GameCatalog.Ages.Count == 0) return;
        if (!GameCatalog.Ages.Contains(GameAge.FirstAge)) problems.Add($"No Age asset has the first Age's id '{GameAge.FirstAge}' (Resources/Ages)");
        foreach (var age in GameCatalog.Ages.All)
        {
            string owner = $"Age '{age.title}'";
            int acts = age.actSevenths != null ? age.actSevenths.Count : 0;
            if (acts < 3 || acts > 4) problems.Add($"{owner}: an Age has three Acts of Fate (four in the long Ages), not {acts}");
            if (age.actOfFateStories != null && age.actOfFateStories.Count > System.Math.Max(0, acts - 1)) problems.Add($"{owner}: {age.actOfFateStories.Count} Act of Fate stories for {System.Math.Max(0, acts - 1)} boundaries between Acts");
            if (age.actTechnologies != null && age.actTechnologies.Count > System.Math.Max(0, acts - 1)) problems.Add($"{owner}: {age.actTechnologies.Count} Act technologies for {System.Math.Max(0, acts - 1)} boundaries between Acts");
            var gates = (age.actTechnologies ?? new List<string>()).Concat((age.crisisStages ?? new List<CrisisStageSpec>()).Where(s => s != null).Select(s => s.technology))
                .Concat(age.advantageTechnologies ?? new List<string>());
            foreach (var gate in gates.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct())
                if (!GameCatalog.Technologies.Contains(gate.Trim())) problems.Add($"{owner}: gate technology '{gate}' does not exist");
            float previous = 0f;
            foreach (var stage in age.crisisStages ?? new List<CrisisStageSpec>())
            {
                if (stage == null) { problems.Add($"{owner} has an empty crisis stage"); continue; }
                if (stage.startsAt < previous) problems.Add($"{owner}: crisis stage '{stage.name}' starts before the stage above it");
                previous = stage.startsAt;
            }
            if (age.HasCrisis && string.IsNullOrEmpty(age.crisisTitle)) problems.Add($"{owner} has crisis stages but no crisis title");
            foreach (var tech in age.preparationTechnologies ?? new List<string>())
            {
                if (!GameCatalog.Technologies.Contains(tech)) problems.Add($"{owner}: preparation technology '{tech}' does not exist");
            }
            foreach (var next in age.nextAges ?? new List<NextAgeSpec>())
            {
                if (next == null || !GameCatalog.Ages.Contains(next.age)) problems.Add($"{owner}: next Age '{next?.age}' has no asset in Resources/Ages");
            }
        }
    }

    // The food stores (Resources/Food/Pantry): every kind is a resource worth something; starting stores are kinds.
    private static void ValidatePantry(List<string> problems)
    {
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        if (pantry == null) { problems.Add("No Resources/Food/Pantry: there are no food stores (claims cannot be paid)."); return; }
        foreach (var kind in pantry.kinds)
        {
            if (kind == null || string.IsNullOrEmpty(kind.resource)) { problems.Add("Pantry has an empty food kind"); continue; }
            if (!GameCatalog.IsResource(kind.resource)) problems.Add($"Pantry: food kind '{kind.resource}' is not a resource");
            if (kind.foodValue <= 0f) problems.Add($"Pantry: food kind '{kind.resource}' is worth nothing (foodValue {kind.foodValue})");
        }
        foreach (var a in pantry.startingStores)
            if (a != null && pantry.Kind(a.resource) == null) problems.Add($"Pantry: starting store '{a.resource}' is not a food kind");
        foreach (var p in pantry.preservation)
            if (p != null && !string.IsNullOrEmpty(p.technology) && !GameCatalog.Technologies.Contains(p.technology)) problems.Add($"Pantry: preservation technology '{p.technology}' does not exist");
    }

    private static void ValidateWorld(List<string> problems)
    {
        if (GameCatalog.World.Count > 1) problems.Add($"{GameCatalog.World.Count} WorldSettings assets in Resources/World: only the first is used");
        foreach (var world in GameCatalog.World.All)
        {
            var gen = world.generation;
            string owner = $"World '{world.name}'";
            foreach (var terrain in gen.terrains)
            {
                if (terrain == null) { problems.Add($"{owner} has an empty terrain"); continue; }
                CheckAmounts($"{owner} terrain '{terrain.id}'", "yields", terrain.yields, problems);
            }
            foreach (var id in new[] { gen.oceanTerrain, gen.shallowsTerrain, gen.lakeTerrain })
            {
                if (gen.Terrain(id)?.water != true) problems.Add($"{owner}: water terrain '{id}' does not exist or is not water");
            }
            foreach (var rule in gen.connective) if (rule == null || gen.Terrain(rule.terrain) == null) problems.Add($"{owner} connective ground names unknown terrain '{rule?.terrain}'");
            foreach (var biome in gen.biomes)
            {
                if (biome == null) { problems.Add($"{owner} has an empty biome"); continue; }
                if (biome.terrains.Count == 0) problems.Add($"{owner} biome '{biome.id}' lays down no terrain");
                foreach (var rule in biome.terrains) if (rule == null || gen.Terrain(rule.terrain) == null) problems.Add($"{owner} biome '{biome.id}' names unknown terrain '{rule?.terrain}'");
                foreach (int o in biome.orientations) if (o < 0 || o > 5) problems.Add($"{owner} biome '{biome.id}' allows orientation {o} (0-5)");
            }
            foreach (var sector in gen.sectors)
            {
                if (sector == null) { problems.Add($"{owner} has an empty sector"); continue; }
                foreach (var biome in sector.biomes) if (gen.Biome(biome) == null) problems.Add($"{owner} sector '{sector.id}' names unknown biome '{biome}'");
            }
            ValidateComposition(world, owner, problems);
            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var feature in gen.features)
            {
                if (feature == null) { problems.Add($"{owner} has an empty feature"); continue; }
                if (!ids.Add(feature.id)) problems.Add($"{owner}: two features have the id '{feature.id}'");
                string featureOwner = $"{owner} feature '{feature.id}'";
                CheckAmounts(featureOwner, "rewards", feature.rewards, problems);
                CheckAmounts(featureOwner, "yields", feature.yields, problems);
                foreach (var terrain in feature.terrains) if (gen.Terrain(terrain) == null) problems.Add($"{featureOwner} names unknown terrain '{terrain}'");
                foreach (var biome in feature.biomes) if (gen.Biome(biome) == null) problems.Add($"{featureOwner} names unknown biome '{biome}'");
            }
            if (gen.Feature(gen.capitalFeature) == null) problems.Add($"{owner}: the capital feature '{gen.capitalFeature}' does not exist");
            if (!gen.terrains.Any(t => t != null && t.passable)) problems.Add($"{owner} has no passable terrain");
            if (!string.IsNullOrEmpty(world.unlockTechnology) && !GameCatalog.Technologies.Contains(world.unlockTechnology)) problems.Add($"{owner}: unlock technology '{world.unlockTechnology}' does not exist");
            if (!string.IsNullOrEmpty(world.mapTechnology) && !GameCatalog.Technologies.Contains(world.mapTechnology)) problems.Add($"{owner}: map technology '{world.mapTechnology}' does not exist");
            // An empty list usually means the asset stopped reading partway (a blank line in its YAML did this once).
            if (world.units.Count == 0) problems.Add($"{owner} has no units: nothing can be trained (check the asset's YAML after 'units:')");
            if (world.settlements.claimCost.Count == 0) problems.Add($"{owner}: claiming land costs nothing (settlements.claimCost is empty)");
            foreach (var id in world.startingUnits) if (world.Unit(id) == null) problems.Add($"{owner}: starting unit '{id}' does not exist");
            foreach (var unit in world.units)
            {
                if (unit == null) { problems.Add($"{owner} has an empty unit"); continue; }
                CheckAmounts($"{owner} unit '{unit.id}'", "cost", unit.cost, problems);
                if (!string.IsNullOrEmpty(unit.unlockTechnology) && !GameCatalog.Technologies.Contains(unit.unlockTechnology)) problems.Add($"{owner} unit '{unit.id}': technology '{unit.unlockTechnology}' does not exist");
                if (!string.IsNullOrEmpty(unit.limitBuilding) && !GameCatalog.ProductionUnits.Contains(unit.limitBuilding)) problems.Add($"{owner} unit '{unit.id}': limit building '{unit.limitBuilding}' does not exist");
                if (unit.stamina <= 0f) problems.Add($"{owner} unit '{unit.id}' cannot move (stamina 0)");
                // Scouts and settlers are retired: legend-led expeditions explore and escort settlers.
                if (unit.role == UnitRole.Scout || unit.role == UnitRole.Settler) problems.Add($"{owner} unit '{unit.id}' is a {unit.role}, a retired role: expeditions of legends explore and settle now");
                // A role without its ability leaves the unit useless (a builder that cannot build).
                var needs = unit.role == UnitRole.Builder ? UnitAbility.Improve : unit.role == UnitRole.Expedition ? UnitAbility.Survey | UnitAbility.SurveyMeso : UnitAbility.None;
                if (needs != UnitAbility.None && (unit.abilities & needs) == 0) problems.Add($"{owner} unit '{unit.id}' ({unit.role}) lacks its role's ability ({needs}): set its abilities");
                if (unit.supplyUsePerSeventh > 0f && unit.supplyCapacity <= 0f) problems.Add($"{owner} unit '{unit.id}' eats rations but carries none (supplyCapacity 0)");
            }
            ValidateExpeditions(world, owner, problems);
            ValidateCivilization(world, owner, problems);
        }
    }

    // Expeditions walk as a real unit, their slot building exists, costs name resources and the slots can hold a Director.
    private static void ValidateExpeditions(WorldSettings world, string owner, List<string> problems)
    {
        var x = world.expeditions;
        if (x == null) { problems.Add($"{owner} has no expedition settings"); return; }
        var spec = world.Unit(x.unit);
        if (spec == null) problems.Add($"{owner}: expeditions walk as unit '{x.unit}', which does not exist");
        else if (spec.role != UnitRole.Expedition) problems.Add($"{owner}: expedition unit '{x.unit}' has role {spec.role}, not Expedition");
        if (!string.IsNullOrEmpty(x.slotBuilding) && !GameCatalog.ProductionUnits.Contains(x.slotBuilding)) problems.Add($"{owner}: expedition slot building '{x.slotBuilding}' does not exist");
        if (Expeditions.Slots(x, 1, 0) < 1) problems.Add($"{owner}: no expedition slot at the start (baseSlots + slotsPerCapacity)");
        if (x.maxParty < 1) problems.Add($"{owner}: an expedition party holds no legend (maxParty)");
        CheckAmounts($"{owner} expeditions", "outfitCost", x.outfitCost, problems);
        CheckAmounts($"{owner} expeditions", "settlerCost", x.settlerCost, problems);
        foreach (var m in x.mishaps)
        {
            if (m == null) { problems.Add($"{owner} has an empty expedition mishap"); continue; }
            if (m.weight < 0f || m.strain < 0f || m.partyStrain < 0f || m.attrition < 0f || m.fatigue < 0f) problems.Add($"{owner} mishap '{m.name ?? m.kind.ToString()}' has a negative number");
        }
        // Party shapes (Solo, Duo, Trio, Company): one per size, the first for a legend alone.
        var shapes = (x.shapes ?? new List<PartyShape>()).Where(s => s != null).ToList();
        if (shapes.Count > 0)
        {
            if (!shapes.Any(s => s.size <= 1)) problems.Add($"{owner}: no party shape for a legend alone (size 1)");
            foreach (var dup in shapes.GroupBy(s => s.size).Where(g => g.Count() > 1)) problems.Add($"{owner}: two party shapes for size {dup.Key}");
            foreach (var s in shapes)
                if (s.pace <= 0f || s.wear < 0f || s.workTime <= 0f || s.hardship < 0f || s.roadStrain < 0f || s.mishapRisk < 0f || s.baseRisk < 0f || s.dangerRisk < 0f || s.quarrelWeight < 0f || s.retreatPace < 0f)
                    problems.Add($"{owner} party shape '{s.name}' has a negative (or zero pace/work) number");
        }
    }

    // Grandfields, enclaves, threats and the settlement rules name real resources, terrains, biomes and bindings.
    private static void ValidateCivilization(WorldSettings world, string owner, List<string> problems)
    {
        var gen = world.generation;
        var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var field in gen.grandfields)
        {
            if (field == null) { problems.Add($"{owner} has an empty grandfield"); continue; }
            string fieldOwner = $"{owner} grandfield '{field.id}'";
            if (!ids.Add("grandfield:" + field.id)) problems.Add($"{owner}: two grandfields have the id '{field.id}'");
            if (!GameCatalog.IsResource(field.resource)) problems.Add($"{fieldOwner} concentrates unknown resource '{field.resource}'");
            foreach (var terrain in field.terrains) if (gen.Terrain(terrain) == null) problems.Add($"{fieldOwner} names unknown terrain '{terrain}'");
            foreach (var biome in field.biomes) if (gen.Biome(biome) == null) problems.Add($"{fieldOwner} names unknown biome '{biome}'");
            if (field.size < 1) problems.Add($"{fieldOwner} has no size");
        }
        foreach (var enclave in gen.enclaves)
        {
            if (enclave == null) { problems.Add($"{owner} has an empty enclave"); continue; }
            string enclaveOwner = $"{owner} enclave '{enclave.id}'";
            if (!ids.Add("enclave:" + enclave.id)) problems.Add($"{owner}: two enclaves have the id '{enclave.id}'");
            foreach (var binding in enclave.bindings) if (!CityDevelopment.Bindings.Contains(binding)) problems.Add($"{enclaveOwner} names unknown binding '{binding}'");
            if (enclave.woundMin < 0 || enclave.woundMax > 100 || enclave.woundMin > enclave.woundMax) problems.Add($"{enclaveOwner} has a Wound Resonance range outside 0-100");
            CheckAmounts(enclaveOwner, "suzerainty yields", enclave.suzeraintyYields, problems);
        }
        foreach (var threat in gen.threats)
        {
            if (threat == null) { problems.Add($"{owner} has an empty threat"); continue; }
            if (!ids.Add("threat:" + threat.id)) problems.Add($"{owner}: two threats have the id '{threat.id}'");
            if (threat.radius <= 0f) problems.Add($"{owner} threat '{threat.id}' has no radius");
        }
        var rules = world.settlements;
        if (rules == null) { problems.Add($"{owner} has no settlement rules"); return; }
        foreach (var (name, list) in new[] { ("town cost", rules.townCost), ("outpost cost", rules.outpostCost), ("haven cost", rules.havenCost), ("incorporation cost", rules.incorporateCost),
            ("promotion cost", rules.promoteCost), ("road cost", rules.roadCostPerCell), ("anchor cost", rules.anchorCost), ("envoy cost", rules.envoyCost),
            ("town yields", rules.townYields), ("major yields", rules.majorYields), ("haven yields", rules.havenYields) })
            CheckAmounts($"{owner} settlements", name, list, problems);
        foreach (var binding in rules.bindings)
        {
            if (binding == null || !CityDevelopment.Bindings.Contains(binding.id)) { problems.Add($"{owner} settlements name unknown binding '{binding?.id}'"); continue; }
            CheckAmounts($"{owner} binding '{binding.id}'", "yields", binding.yields, problems);
        }
        if (!string.IsNullOrEmpty(rules.capacityBuilding) && !GameCatalog.ProductionUnits.Contains(rules.capacityBuilding)) problems.Add($"{owner}: capacity building '{rules.capacityBuilding}' does not exist");
    }

    // The stencil, the handmade tiles and the sector catalogs fit together (roadmap WG01): every stencil sector has a
    // catalog that can fill its slots, every tile parses and names a real biome and real terrain.
    private static void ValidateComposition(WorldSettings world, string owner, List<string> problems)
    {
        var gen = world.generation;
        var stencilAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(gen.stencil);
        if (stencilAsset == null)
        {
            problems.Add($"{owner}: no stencil at Resources/{gen.stencil}");
            return;
        }
        WorldStencil stencil;
        try
        {
            stencil = WorldStencil.Parse(stencilAsset.text);
        }
        catch (System.FormatException e)
        {
            problems.Add($"{owner}: the stencil cannot be read: {e.Message}");
            return;
        }
        foreach (var asset in UnityEngine.Resources.LoadAll<UnityEngine.TextAsset>(gen.tilesFolder))
        {
            try
            {
                var tile = TileTemplate.Parse(asset.text, asset.name);
                if (gen.Biome(tile.biome) == null) problems.Add($"{owner}: handmade tile '{tile.id}' names unknown biome '{tile.biome}'");
                foreach (var terrain in tile.cells.Values.Distinct()) if (gen.Terrain(terrain) == null) problems.Add($"{owner}: handmade tile '{tile.id}' draws unknown terrain '{terrain}'");
            }
            catch (System.FormatException e)
            {
                problems.Add($"{owner}: handmade tile {asset.name}: {e.Message}");
            }
        }
        foreach (var error in SlotSolver.Solve(stencil, gen, 1).errors) problems.Add($"{owner}: {error}");
    }

    private static void CheckAmounts(string owner, string label, List<ResourceAmount> amounts, List<string> problems)
    {
        foreach (var amount in amounts ?? new List<ResourceAmount>())
        {
            if (amount == null || !GameCatalog.IsResource(amount.resource)) problems.Add($"{owner}: {label} names '{amount?.resource}', which is not a resource");
        }
    }

    /// <summary>
    /// The Age and world stories that do not exist as locked events. Needs the stories indexed
    /// (<see cref="InkDrivenEventSetup.LoadVolumes"/>); ContentTests runs it after loading them.
    /// </summary>
    public static List<string> AgeAndWorldStoryProblems(ICollection<StoryNode> events)
    {
        var problems = new List<string>();
        var byName = events.Where(e => e != null).GroupBy(e => e.nodeName, System.StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);
        void Check(string owner, string knot)
        {
            if (string.IsNullOrWhiteSpace(knot)) return;
            if (!byName.TryGetValue(knot.Trim(), out var node)) problems.Add($"{owner}: story '{knot}' is not an event in Resources/Events");
            else if (node.isUnlocked) problems.Add($"{owner}: story '{knot}' must be authored '# locked: true' (it is offered at its moment, not at random)");
        }
        foreach (var age in GameCatalog.Ages.All)
        {
            string owner = $"Age '{age.title}'";
            Check(owner, age.openingStory);
            foreach (var knot in age.actOfFateStories ?? new List<string>()) Check(owner, knot);
            foreach (var stage in age.crisisStages ?? new List<CrisisStageSpec>()) if (stage != null) Check($"{owner} stage '{stage.name}'", stage.story);
        }
        foreach (var world in GameCatalog.World.All)
        {
            foreach (var feature in world.generation.features) if (feature != null) Check($"World feature '{feature.id}'", feature.story);
        }
        return problems;
    }

    /// <summary>
    /// Ballads and their actors: a cast names areas the council knows and legends that exist; a ballad's verses run
    /// 1..N with no gap or repeat (its highest verse is the finale that pays its theme), and some verse gives its theme
    /// and title. Needs the stories indexed, like <see cref="AgeAndWorldStoryProblems"/>.
    /// </summary>
    public static List<string> BalladProblems(ICollection<StoryNode> events)
    {
        var problems = new List<string>();
        var stories = events.Where(e => e != null).ToList();
        foreach (var story in stories.Where(s => !string.IsNullOrEmpty(s.cast)))
        {
            var spec = BalladActors.ParseCast(story.cast, problems, story.nodeName);
            if (!string.IsNullOrEmpty(spec.area) && CouncilAreaRules.Resolve(spec.area, CouncilAreaCatalog.Current) == null)
                problems.Add($"{story.nodeName}: cast area '{spec.area}' is not in Resources/Council/Council Areas");
            if (!string.IsNullOrEmpty(spec.legend) && GameCatalog.Legends.Count > 0 && !GameCatalog.Legends.Contains(spec.legend))
                problems.Add($"{story.nodeName}: cast legend '{spec.legend}' does not exist");
            if (!string.IsNullOrEmpty(spec.seat) && GameCatalog.CouncilSeats.Count > 0 && !GameCatalog.CouncilSeats.Contains(spec.seat))
                problems.Add($"{story.nodeName}: cast seat '{spec.seat}' is not a council seat (prefer area:, which any seat can answer)");
        }
        foreach (var ballad in stories.Where(s => !string.IsNullOrEmpty(s.ballad)).GroupBy(s => s.ballad, System.StringComparer.OrdinalIgnoreCase))
        {
            var verses = ballad.Select(s => s.verse).OrderBy(v => v).ToList();
            foreach (var repeated in verses.GroupBy(v => v).Where(g => g.Count() > 1)) problems.Add($"Ballad '{ballad.Key}' has verse {repeated.Key} {repeated.Count()} times");
            var missing = Enumerable.Range(1, Math.Max(0, verses.DefaultIfEmpty(0).Max())).Except(verses).ToList();
            if (missing.Count > 0) problems.Add($"Ballad '{ballad.Key}' has no verse {string.Join(", ", missing)}");
            var (theme, title) = BalladActors.BalladInfo(stories, ballad.Key);
            if (string.IsNullOrEmpty(theme)) problems.Add($"Ballad '{ballad.Key}' has no theme: its finale pays no Lyrical Fragments (# theme: on a verse)");
            if (string.IsNullOrEmpty(title)) problems.Add($"Ballad '{ballad.Key}' has no title (# ballad_title: on a verse)");
        }
        return problems;
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
