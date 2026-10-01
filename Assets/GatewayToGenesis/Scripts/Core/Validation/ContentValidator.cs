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
        ValidateCultureLife(problems);
        ValidateTraditions(problems);
        ValidateDiscoveries(problems);
        ValidateHealth(problems);
        ValidateGrowth(problems);
        ValidateCreatureSchema(problems);
        ValidateCombat(problems);
        ValidateEnclaveEcology(problems);
        ValidateDiscoveryLoop(problems);

        foreach (var problem in problems) GameLog.Warning(problem, Log);
        GameLog.Event($"Validated {GameCatalog.Legends.Count} legends, {GameCatalog.CouncilSeats.Count} council seats, {GameCatalog.Civics.Count} civics, " +
                      $"{GameCatalog.Weather.Count} weather profiles, {GameCatalog.ProductionUnits.Count} production units, {GameCatalog.Technologies.Count} technologies, " +
                      $"{Keywords.Cards.Cards.Count} keyword cards, {Library.Index.Entries.Count} Library entries: {problems.Count} problem(s)", Log);
        return problems.Count;
    }

    // The discovery loop (Docs/Planning/DISCOVERY_LOOP.md): every riddle's clue can come to light, and every hidden recipe
    // can be found (real foods, shares that fit one mix, never a bloom in the cellar) and is not a recipe already known.
    private static void ValidateDiscoveryLoop(List<string> problems)
    {
        foreach (var tech in GameCatalog.Technologies.All)
        {
            if (tech == null) continue;
            string owner = $"Technology '{(tech.gameUnit != null ? tech.gameUnit.name : tech.name)}'";
            foreach (var condition in tech.enlightenedConditions.Where(c => c != null))
            {
                if (!condition.IsRiddle)
                {
                    if (!string.IsNullOrWhiteSpace(condition.clue)) problems.Add($"{owner} Enlightenment goal '{condition.Describe()}' has a clue but no riddle");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(condition.clueTrigger)) continue;
                string domain = condition.clueTrigger.Split(':')[0].Trim();
                if (!GameValues.IsKnownDomain(domain)) problems.Add($"{owner} riddle '{condition.riddle.Trim()}' brings its clue out on unknown value domain '{domain}'");
            }
        }
        var settings = UnityEngine.Resources.Load<CultureSettings>("Culture/Culture");
        var life = settings != null && settings.life != null ? settings.life : new CultureLifeTuning();
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        var blooms = CultureSystem.BloomResources();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in life.hiddenRecipes ?? new List<HiddenRecipeSpec>())
        {
            string id = spec?.formula?.id ?? "(no id)";
            if (spec?.formula?.id != null && !ids.Add(spec.formula.id)) problems.Add($"Culture: hidden recipe '{id}' is listed twice");
            string why = KitchenTrials.Problem(spec, r => GameCatalog.IsResource(r) && (pantry == null || pantry.Kind(r) != null), blooms.Contains);
            if (why != null) { problems.Add($"Culture: hidden recipe '{id}' {why}"); continue; }
            var method = spec.Method;
            // A known recipe that already is the hidden one would be found by cooking it, not by looking for it.
            var known = life.recipes.FirstOrDefault(r => r != null && r.method == method && Experiments.Matches(spec.formula, KitchenTrials.MethodName(method), KitchenTrials.Shares(r.inputs)));
            if (known != null) problems.Add($"Culture: hidden recipe '{id}' is the known recipe '{known.id}' already");
            if (!string.IsNullOrEmpty(spec.luxury) && !life.luxuries.Any(l => l != null && string.Equals(l.category, spec.luxury, StringComparison.OrdinalIgnoreCase)))
                problems.Add($"Culture: hidden recipe '{id}' names luxury '{spec.luxury}', which is no luxury category");
        }
        if (life.trialsPerSeventh < 1) problems.Add("Culture: the kitchen may try no batch at all (trialsPerSeventh below 1)");
    }

    // The Enclaves and the creatures (vault: Arcanorian Ecology.md, "Enclaves and Creatures"): keepers keep something, only keepers name what they keep,
    // and every cost and yield names a real resource.
    private static void ValidateEnclaveEcology(List<string> problems)
    {
        foreach (var world in GameCatalog.World.All)
        {
            var gen = world?.generation;
            var s = gen?.enclaveEcology;
            if (s == null) continue;
            string owner = $"World '{world.name}' enclave ecology";
            CheckAmounts(owner, "herd yields", s.herdYields, problems);
            CheckAmounts(owner, "commission cost", s.commissionCost, problems);
            if (s.standingCost > s.commissionStanding) problems.Add($"{owner}: a commission spends more standing ({s.standingCost}) than it needs ({s.commissionStanding})");
            foreach (var enclave in gen.enclaves.Where(e => e != null))
            {
                if (enclave.family != EnclaveFamily.Domestication)
                {
                    if (enclave.keepsPureLight > 0f) problems.Add($"{owner}: enclave '{enclave.id}' sets keepsPureLight but only Domestication Enclaves keep creatures");
                    continue;
                }
                if (!gen.species.Any(sp => sp != null && WorldEnclaveEcology.Keeps(enclave, sp)))
                    problems.Add($"{owner}: Domestication enclave '{enclave.id}' keeps no species (no creature has {enclave.keepsPureLight:P0} Pure Light or more)");
            }
            foreach (var enclave in gen.enclaves.Where(e => e != null && !string.IsNullOrEmpty(e.trades)))
            {
                if (enclave.family != EnclaveFamily.Domestication) problems.Add($"{owner}: enclave '{enclave.id}' trades '{enclave.trades}' but only Domestication Enclaves farm creatures");
                if (gen.Species(enclave.trades) == null) problems.Add($"{owner}: enclave '{enclave.id}' trades unknown species '{enclave.trades}'");
            }
            ValidateAftermath(world, problems);
            ValidateGreatPlague(world, problems);
        }
    }

    // The aftermath's new lineages exist and arrive only through it (vault: Arcanorian Ecology.md, "The Aftermath of Creatures").
    private static void ValidateAftermath(WorldSettings world, List<string> problems)
    {
        var gen = world.generation;
        var a = gen.aftermath;
        if (a == null) return;
        string owner = $"World '{world.name}' aftermath";
        if (a.emptiedBelow <= 0f && a.lineages.Count > 0) problems.Add($"{owner}: lineages are listed but no Macro Biome can be emptied (emptiedBelow 0)");
        foreach (var lineage in a.lineages)
        {
            var species = gen.Species(lineage?.species);
            if (species == null) { problems.Add($"{owner}: lineage names unknown species '{lineage?.species}'"); continue; }
            var dens = WorldEcology.DensOf(gen, species.id).ToList();
            if (dens.Count == 0) problems.Add($"{owner}: lineage '{species.id}' has no den spec (its habitat is read from one)");
            else if (dens.All(d => d.count > 0)) problems.Add($"{owner}: lineage '{species.id}' is also placed at generation (give one of its den specs count 0 if it should only resettle)");
        }
    }

    // The Great Plague's moths: a vector species, a trading Enclave, and a reveal stage the plague's Age has (E9).
    private static void ValidateGreatPlague(WorldSettings world, List<string> problems)
    {
        var gen = world.generation;
        var g = gen.greatPlague;
        if (g == null || string.IsNullOrEmpty(g.vector)) return;
        string owner = $"World '{world.name}' greatPlague";
        var species = gen.Species(g.vector);
        if (species == null) { problems.Add($"{owner}: vector '{g.vector}' is not a species"); return; }
        if (species.vector <= 0f) problems.Add($"{owner}: vector '{g.vector}' carries no sickness (SpeciesSpec.vector 0)");
        if (!WorldEcology.DensOf(gen, g.vector).Any()) problems.Add($"{owner}: vector '{g.vector}' has no den spec (it has no habitat)");
        if (!gen.enclaves.Any(e => e != null && string.Equals(e.trades, g.vector, StringComparison.OrdinalIgnoreCase))) problems.Add($"{owner}: no Enclave trades '{g.vector}'");
        CheckAmounts(owner, "restrict cost", g.restrictCost, problems);
        if (!GameCatalog.Ages.TryGet(g.age, out var age)) problems.Add($"{owner}: Age '{g.age}' has no asset in Resources/Ages");
        else if (g.revealStage >= (age.crisisStages?.Count ?? 0)) problems.Add($"{owner}: revealStage {g.revealStage} but '{g.age}' has {age.crisisStages?.Count ?? 0} crisis stages");
    }

    // The full creature schema (vault: Arcanorian Ecology.md, "The Nature of a Species"): every species shows somewhere, its magic, ills and rewards are
    // sane, and resonance plagues are tuned to real Pure Light beings (vault: Pure Light.md, Pathogenic Weaknesses).
    // The Symphony of War's units: every conscripted company draws people, costs real resources, needs a real technology
    // and is listed under a real Section (ArmyRoster).
    private static void ValidateCombat(List<string> problems)
    {
        foreach (var world in GameCatalog.World.All)
        {
            string owner = $"World settings '{world.name}'";
            foreach (var spec in (world.combat ?? new CombatSettings()).Sections.Where(s => s != null && s.conscripted))
            {
                string unit = $"{owner} unit '{spec.id}'";
                if (spec.people < 1) problems.Add($"{unit} is conscripted but draws no people");
                foreach (var c in spec.cost ?? new List<ResourceAmount>())
                    if (c == null || !GameCatalog.IsResource(c.resource) || c.amount <= 0f) problems.Add($"{unit} costs '{c?.resource}' {c?.amount}, which is not a resource and an amount");
                if (!string.IsNullOrEmpty(spec.technology) && !GameCatalog.Technologies.Contains(spec.technology)) problems.Add($"{unit} needs '{spec.technology}', which is not a technology");
                if (!string.IsNullOrEmpty(spec.category) && !GameCatalog.Sections.TryGet(spec.category, out _)) problems.Add($"{unit} is listed under '{spec.category}', which is not a Section");
            }
            // The threats' red and orange bands (WorldBattles): a real species, or beings of their own that carry bindings.
            foreach (var threat in world.generation.threats.Where(t => t != null && t.bands > 0))
            {
                string owner2 = $"{owner} threat '{threat.id}'";
                if (!string.IsNullOrEmpty(threat.species) && world.generation.Species(threat.species) == null) problems.Add($"{owner2} sends out '{threat.species}', which is not a species");
                if (string.IsNullOrEmpty(threat.species) && string.IsNullOrEmpty(threat.beingName)) problems.Add($"{owner2} sends out beings of its own but names none");
                if (threat.bandMin < 1 || threat.bandMax < threat.bandMin) problems.Add($"{owner2}: bands of {threat.bandMin}-{threat.bandMax} creatures");
                if ((threat.beingBindings ?? new List<SpellBinding>()).Contains(SpellBinding.Unattuned)) problems.Add($"{owner2}: its beings' bindings list Unattuned (every Atonalis carries a binding)");
                if (threat.respawnSevenths <= 0f) problems.Add($"{owner2}: respawnSevenths must be above 0");
            }
        }
    }

    private static void ValidateCreatureSchema(List<string> problems)
    {
        foreach (var world in GameCatalog.World.All)
        {
            var gen = world.generation;
            string owner = $"World settings '{world.name}'";
            foreach (var species in gen.species ?? new List<SpeciesSpec>())
            {
                if (species == null || string.IsNullOrEmpty(species.id)) continue;
                string speciesOwner = $"{owner} species '{species.id}'";
                if (!string.IsNullOrEmpty(species.entrainedFrom))
                {
                    if (species.entrainedFrom == species.id || gen.Species(species.entrainedFrom) == null)
                        problems.Add($"{speciesOwner}: entrainedFrom must name a different existing lineage");
                    if (WorldEcology.DensOf(gen, species.id).Any(s => s.minAge < 1 || s.count != 0))
                        problems.Add($"{speciesOwner}: entrained descendants need count-zero dens from Renewal onward");
                }
                if (species.unmerged && WorldEcology.DensOf(gen, species.id).Any(s => !s.requiresCoherentRefuge || s.minimumCoherence < 0.8f))
                    problems.Add($"{speciesOwner}: unmerged sprites need high-Coherence refuges");
                if (!(gen.resourceSites ?? new List<ResourceSiteSpec>()).Any(s => s != null && string.Equals(s.species, species.id, System.StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{speciesOwner}: no resource site names it, so it never lives anywhere");
                if (species.breedingEcho < 0 || species.breedingEcho > TimeSystemLogic.EchoesPerCycle) problems.Add($"{speciesOwner}: breedingEcho must be 0 (its diet's rhythm) or 1-{TimeSystemLogic.EchoesPerCycle}");
                if (species.commensal < 0f || species.commensal > 1f || species.vector < 0f || species.vector > 1f) problems.Add($"{speciesOwner}: commensal and vector lie between 0 and 1");
                // Canon: an animal that earns Coherence-Binding Tissue leaves the high-Structure pole for the 50-50 axis.
                if (species.binding != BindingOrgan.None && species.structure > 0.7f)
                    problems.Add($"{speciesOwner} has a Coherence-Binding organ but {species.structure:P0} Auric Structure: a lineage with CBT sits near the 50-50 axis (Pure Light.md)");
                if (species.niche == HarmonicNiche.Discordant && !CreatureTaxonomy.IsPureLightBeing(species))
                    problems.Add($"{speciesOwner} is Discordant but not a Pure Light being: its niche would change nothing");
                // The Elemental Harmonic Circle: every Pure Light being carries a primary binding (its weakness); an
                // ordinary animal has no interface to the Loom and stays Unattuned.
                bool pureLight = CreatureTaxonomy.IsPureLightBeing(species);
                if (pureLight && species.primaryBinding == SpellBinding.Unattuned)
                    problems.Add($"{speciesOwner} is a Pure Light being with no primary binding (HarmonicCircle)");
                if (!pureLight && species.primaryBinding != SpellBinding.Unattuned)
                    problems.Add($"{speciesOwner} is not a Pure Light being but carries {species.primaryBinding}: an ordinary animal is Unattuned");
                var secondaries = species.secondaryBindings ?? new List<SpellBinding>();
                if (secondaries.Count > 2 || secondaries.Contains(SpellBinding.Unattuned) || secondaries.Contains(species.primaryBinding) || secondaries.Distinct().Count() != secondaries.Count)
                    problems.Add($"{speciesOwner}: at most two secondary bindings, distinct and other than its primary");
                if (!pureLight && secondaries.Count > 0) problems.Add($"{speciesOwner}: an Unattuned animal casts in no binding");
                foreach (var reward in species.discovery ?? new List<DiscoveryReward>())
                {
                    if (reward == null) { problems.Add($"{speciesOwner} has an empty discovery reward"); continue; }
                    if ((int)reward.level < (int)SpeciesLevel.Identified || (int)reward.level > 5) problems.Add($"{speciesOwner}: a discovery reward pays at Identified (2) to Mastered (5), not {(int)reward.level}");
                    if (reward.eraScore < 0) problems.Add($"{speciesOwner}: a discovery reward takes Era Score away");
                    if (!string.IsNullOrEmpty(reward.resource) && !GameCatalog.IsResource(reward.resource)) problems.Add($"{speciesOwner}: a discovery reward names '{reward.resource}', which is not a resource");
                    if (!string.IsNullOrEmpty(reward.resource) && reward.amount <= 0f) problems.Add($"{speciesOwner}: a discovery reward of {reward.resource} gives nothing");
                }
            }
            var plagueIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var plague in gen.plagues ?? new List<ResonancePlagueSpec>())
            {
                if (plague == null) { problems.Add($"{owner} has an empty plague"); continue; }
                string plagueOwner = $"{owner} plague '{plague.id}'";
                if (string.IsNullOrEmpty(plague.id) || !plagueIds.Add(plague.id)) problems.Add($"{plagueOwner}: missing or repeated id");
                if (string.IsNullOrEmpty(plague.name)) problems.Add($"{plagueOwner} has no name");
                if (plague.duration < 1 || plague.lethality < 0f || plague.lethality > 1f) problems.Add($"{plagueOwner}: it runs at least one Echo and kills a share between 0 and 1");
                foreach (string host in plague.hosts ?? new List<string>())
                {
                    var species = gen.Species(host);
                    if (species == null) problems.Add($"{plagueOwner} is tuned to unknown species '{host}'");
                    else if (!CreatureTaxonomy.IsPureLightBeing(species)) problems.Add($"{plagueOwner} is tuned to {species.name}, which is not a Pure Light being: high-Structure lineages shrug off resonance plagues");
                }
            }
            if (gen.ecology != null && gen.ecology.vectorFull <= 0f) problems.Add($"{owner} ecology: vectorFull must be above 0");
            if (gen.rhythm != null && (gen.rhythm.breedingPeak < 1f || gen.rhythm.breedingPeak > TimeSystemLogic.EchoesPerCycle))
                problems.Add($"{owner} rhythm: breedingPeak must lie between 1 and {TimeSystemLogic.EchoesPerCycle}");
        }
    }

    private static void ValidateGrowth(List<string> problems)
    {
        var asset = UnityEngine.Resources.Load<GrowthSettings>("Population/Growth");
        var t = asset != null ? asset.tuning : null;
        if (t == null) { problems.Add("Missing Population/Growth settings."); return; }
        if (!(t.daysPerYear > 0f) || !(t.kcalPerFood > 0f) || !(t.kcalPerPersonDay > 0f)
            || !(t.arrivalRations > 0f) || !(t.arrivalIntervalSeconds > 0f)
            || t.foundingMigrants < 0 || !(t.birthsPerThousand >= 0f) || !(t.deathsPerThousand >= 0f)
            || t.birthsPerThousand > 1000f || t.deathsPerThousand > 1000f
            || !(t.distributionLoss >= 0f && t.distributionLoss <= 1f)
            || !(t.famineGraceDays >= 0f) || !(t.famineDeathsPerTenThousandDay >= 0f))
            problems.Add("Population/Growth has invalid units or rates.");
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
            if (seat.requiredStars < 0 || seat.requiredStars > LegendGreats.MaxStars) problems.Add($"{owner} asks for {seat.requiredStars} stars; a Great has 0 to {LegendGreats.MaxStars}");
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
                else if (unlockable.unlockableType == TechUnlockableType.SymphonyCard && unlockable.symphonyCard == null)
                    problems.Add($"{owner} unlockable '{unlockable.name}' grants a Symphony Card but names none");
                else if (unlockable.unlockableType == TechUnlockableType.SymphonyCard && unlockable.symphonyCard.chord > Grimoire.SeatChordLimit)
                    problems.Add($"{owner} card '{unlockable.symphonyCard.DisplayName}' is a {unlockable.symphonyCard.chord}; seats take {Grimoire.SeatChordLimit}s only");
                else if (unlockable.unlockableType == TechUnlockableType.ScoreChange && string.IsNullOrWhiteSpace(unlockable.score))
                    problems.Add($"{owner} unlockable '{unlockable.name}' changes a score but names none");
                else if (unlockable.gameUnit == null && unlockable.unlockableType != TechUnlockableType.Special && unlockable.unlockableType != TechUnlockableType.DemandModifier && unlockable.unlockableType != TechUnlockableType.CouncilSeat
                    && unlockable.unlockableType != TechUnlockableType.GrimoireSeat && unlockable.unlockableType != TechUnlockableType.SymphonyCard
                    && unlockable.unlockableType != TechUnlockableType.SpellWildcard && unlockable.unlockableType != TechUnlockableType.ScoreChange)
                {
                    problems.Add($"{owner} unlockable '{unlockable.name}' ({unlockable.unlockableType}) has no GameUnit");
                }
            }
        }
        var names = GameCatalog.Technologies.All.Where(t => t.gameUnit != null).Select(t => t.gameUnit.name);
        foreach (var looping in TechTreeRules.Cycles(names, TechnologyTreeLogic.Prerequisites))
            problems.Add($"Technology '{looping}' needs itself through its prerequisites, so it can never be researched");
    }

    // Discoveries (SpeciesKnowledge; vault: Arcanorian Ecology.md, "Knowledge of Creatures"): a goal naming a species or a site names a real one, and
    // a discovery is only ever a reward (Enlightenment), never a requirement: the Q5 draw can leave a Macro Biome out of
    // a world, so a required creature could be missing from it. One technology opens the Bestiary.
    private static void ValidateDiscoveries(List<string> problems)
    {
        var species = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var sites = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var world in GameCatalog.World.All)
        {
            foreach (var s in world.generation.species ?? new List<SpeciesSpec>()) if (s != null && !string.IsNullOrEmpty(s.id)) species.Add(s.id);
            foreach (var s in world.generation.resourceSites ?? new List<ResourceSiteSpec>()) if (s != null && !string.IsNullOrEmpty(s.id)) sites.Add(s.id);
        }
        var bestiary = new List<string>();
        var studies = new List<string>();
        foreach (var tech in GameCatalog.Technologies.All)
        {
            if (tech == null) continue;
            string owner = $"Technology '{(tech.gameUnit != null ? tech.gameUnit.name : tech.name)}'";
            if (BestiaryHud.OpensBestiary(tech)) bestiary.Add(owner);
            if (SpeciesLoreKeeper.OpensStudies(tech)) studies.Add(owner);
            foreach (var condition in tech.enlightenedConditions)
            {
                if (condition == null || condition.type != EnlightenedType.GameValue) continue;
                condition.SplitTrigger(out string domain, out string target);
                if (!SpeciesKnowledge.IsDiscoveryDomain(domain)) continue;
                string why = SpeciesKnowledge.GoalProblem(domain, target, condition.requiredAmount, species, sites);
                if (why != null) problems.Add($"{owner} Enlightenment goal '{condition.Describe()}' {why}");
            }
        }
        if (GameCatalog.Technologies.Count > 0 && bestiary.Count != 1)
            problems.Add(bestiary.Count == 0
                ? $"No technology unlocks the Bestiary (a Special unlockable named '{SpeciesKnowledge.BestiaryUnlock}')"
                : $"Several technologies unlock the Bestiary ({string.Join(", ", bestiary)}): keep one");
        if (GameCatalog.Technologies.Count > 0 && studies.Count != 1)
            problems.Add(studies.Count == 0
                ? $"No technology turns observed creatures into understood ones (a Special unlockable named '{SpeciesLore.StudiesUnlock}')"
                : $"Several technologies carry '{SpeciesLore.StudiesUnlock}' ({string.Join(", ", studies)}): keep one");
        foreach (var civic in GameCatalog.Civics.All)
            foreach (var requirement in civic?.requirements ?? new List<CivicRequirement>())
                if (SpeciesKnowledge.IsDiscoveryDomain(requirement?.ToCondition()?.Domain))
                    problems.Add($"Civic '{civic.civicName}' requires a discovery ({requirement.ToCondition().Domain}): discoveries are Enlightenment rewards, never requirements");
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

    // The food stores (Resources/Food/Pantry): every kind is a resource worth something (spices feed no one, so they
    // may be worth nothing); what a kind leaves behind is a resource; starting stores are kinds.
    // The life of the culture (Resources/Culture/Culture, else the code's defaults): Unity is a resource, every dish is a
    // stored food cooked from resources, every Beverage is brewed or distilled in the cellar (never from an Eleos Bloom
    // or tea; spirits from something fermented), and every cost, luxury and technology named exists. Civics may be the vault's
    // (not built yet), so they are not checked.
    // Living traditions (TraditionTuning): unique ids, a vault note and canon label each, an Age that exists, a real
    // technology, a real family, triggers that name rites and recipes that exist, effects that apply, sane thresholds;
    // and the memorials the vault suggests name rites, recipes and landmarks that exist.
    private static void ValidateTraditions(List<string> problems)
    {
        var settings = UnityEngine.Resources.Load<CultureSettings>("Culture/Culture");
        var tuning = settings != null && settings.traditions != null ? settings.traditions : new TraditionTuning();
        var life = settings != null && settings.life != null ? settings.life : new CultureLifeTuning();
        if (tuning.establishSevenths < 2) problems.Add("Culture: a tradition must be kept in at least 2 Sevenths to become a custom (establishSevenths)");
        if (tuning.lapseSevenths <= tuning.establishSevenths) problems.Add("Culture: traditions lapse before they could ever be established (lapseSevenths)");
        if (tuning.maxCreditPerSeventh <= 0f || tuning.historyKept < 1 || tuning.maxActiveBenefits < 0) problems.Add("Culture: tradition credit cap, history or benefit cap is not positive");
        var ages = new HashSet<int>(GameCatalog.Ages.All.Where(a => a != null).Select(a => a.number));
        foreach (var d in tuning.definitions)
        {
            if (d == null || string.IsNullOrEmpty(d.id) || string.IsNullOrEmpty(d.name)) { problems.Add("Culture: a tradition has no id or name"); continue; }
            string owner = $"Culture: tradition '{d.id}'";
            if (tuning.definitions.Count(o => o != null && string.Equals(o.id, d.id, StringComparison.OrdinalIgnoreCase)) > 1) problems.Add($"{owner} is defined twice");
            if (string.IsNullOrEmpty(d.canonSource)) problems.Add($"{owner} cites no vault note");
            if (ages.Count > 0 && !ages.Contains(d.minAge)) problems.Add($"{owner} begins in Age {d.minAge}, which does not exist");
            if (!string.IsNullOrEmpty(d.technology) && !GameCatalog.Technologies.Contains(d.technology)) problems.Add($"{owner} needs technology '{d.technology}', which does not exist");
            if (!string.IsNullOrEmpty(d.family) && !CultureRules.TryFamily(d.family, out _)) problems.Add($"{owner} belongs to '{d.family}', which is not a way of living");
            if (d.triggers == null || d.triggers.Count == 0) problems.Add($"{owner} is fed by nothing");
            foreach (var t in d.triggers ?? new List<TraditionTrigger>())
            {
                if (t == null || t.weight <= 0f) { problems.Add($"{owner} has a trigger that counts for nothing"); continue; }
                if (string.IsNullOrEmpty(t.subjectId)) continue;
                if (t.subjectKind == CultureEntityKind.Activity && !t.subjectId.StartsWith("local:", StringComparison.Ordinal) && life.Activity(t.subjectId) == null)
                    problems.Add($"{owner} listens to rite '{t.subjectId}', which does not exist");
                if (t.subjectKind == CultureEntityKind.Recipe && life.Recipe(t.subjectId) == null) problems.Add($"{owner} listens to recipe '{t.subjectId}', which does not exist");
            }
            foreach (var e in (d.practiced ?? new List<TraditionEffect>()).Concat(d.recognized ?? new List<TraditionEffect>()).Where(e => e != null))
            {
                var (valid, error) = e.ToEffect().ValidateShape();
                if (!valid) problems.Add($"{owner}: {error}");
                if (e.type == GameEffectType.ResourceModifier && !string.IsNullOrEmpty(e.target) && !GameCatalog.IsResource(e.target)) problems.Add($"{owner} gives to '{e.target}', which is not a resource");
            }
            if (d.recognitionUnity < 0f) problems.Add($"{owner} pays Unity back to recognise it");
        }
        ValidateObservances(problems, settings, life);
        ValidateSyncretism(problems, settings, tuning);
        ValidateTransmission(problems, new TransmissionTuning(), tuning, life);
        // The memorials the vault suggests (T03's MemorialSuggestion): each cites its note and names a rite, recipe or landmark that exists.
        foreach (var s in MemorialSuggestion.All)
        {
            string owner = $"Culture: memorial suggestion '{s.target}' ({s.cause})";
            if (string.IsNullOrEmpty(s.source)) problems.Add($"{owner} cites no vault note");
            bool exists = s.kind == CultureEntityKind.Recipe ? life.Recipe(s.target) != null
                : s.kind == CultureEntityKind.Activity ? life.Activity(s.target) != null
                : s.kind == CultureEntityKind.Landmark ? life.Landmark(s.target) != null
                : !string.IsNullOrEmpty(s.target);
            if (!exists) problems.Add($"{owner} names a {s.kind.ToString().ToLowerInvariant()} that does not exist");
        }
    }

    /// <summary>Validate teaching's authored references before an unreachable institution or practice ships.</summary>
    public static void ValidateTransmission(List<string> problems, TransmissionTuning tuning, TraditionTuning traditions, CultureLifeTuning life)
    {
        foreach (var spec in tuning.institutions)
        {
            if (spec == null || string.IsNullOrEmpty(spec.id)) { problems.Add("Culture: a teaching institution has no id"); continue; }
            string owner = $"Culture: institution '{spec.id}'";
            if (tuning.institutions.Count(i => i != null && string.Equals(i.id, spec.id, StringComparison.OrdinalIgnoreCase)) > 1) problems.Add($"{owner} is defined twice");
            if (string.IsNullOrEmpty(spec.vault) || string.IsNullOrEmpty(spec.canonNote)) problems.Add($"{owner} cites no canon note");
            if (spec.seats < 1 || spec.maxComplexity < 1 || spec.foundingUnity < 0f) problems.Add($"{owner} has invalid seats, complexity or cost");
            if (spec.maxAge >= 0 && spec.maxAge < spec.minAge) problems.Add($"{owner} ends before it begins");
            if (!spec.informal && (spec.venues == null || spec.venues.Count == 0)) problems.Add($"{owner} has no venue");
            foreach (var venue in spec.venues ?? new List<string>())
                if (life.Landmark(venue) == null) problems.Add($"{owner} names missing venue '{venue}'");
            if (!string.IsNullOrEmpty(spec.evolvesFrom) && (spec.evolvesFrom == spec.id || tuning.Institution(spec.evolvesFrom) == null))
                problems.Add($"{owner} has invalid predecessor '{spec.evolvesFrom}'");
        }
        foreach (var practice in tuning.complexity)
            if (practice == null || traditions.Definition(practice.definition) == null || practice.complexity < 1)
                problems.Add($"Culture: teaching complexity names an invalid practice '{practice?.definition}'");
    }

    // Blends (SyncretismTuning, T07): authored only, unique, a vault note each, two parents that exist (a tradition or a
    // settlement's custom), a result that is a linked-only tradition, a depth within the cap, Ages and technology that exist.
    private static void ValidateSyncretism(List<string> problems, CultureSettings settings, TraditionTuning traditions)
    {
        var tuning = settings != null && settings.syncretism != null ? settings.syncretism : new SyncretismTuning();
        if (tuning.maxDepth < 1 || tuning.sustainedGatherings < 1 || tuning.reofferSevenths < 1) problems.Add("Culture: syncretism depth, sustain or re-offer numbers are not positive");
        if (tuning.uncertainShare <= 0f || tuning.uncertainShare > 1f) problems.Add("Culture: a guess from a ruin nobody knows must shift less than a known one (uncertainShare 0-1)");
        var ages = new HashSet<int>(GameCatalog.Ages.All.Where(a => a != null).Select(a => a.number));
        foreach (var r in tuning.hybrids)
        {
            if (r == null || string.IsNullOrEmpty(r.id)) { problems.Add("Culture: a blend has no id"); continue; }
            string owner = $"Culture: blend '{r.id}'";
            if (tuning.hybrids.Count(o => o != null && string.Equals(o.id, r.id, StringComparison.OrdinalIgnoreCase)) > 1) problems.Add($"{owner} is defined twice");
            if (string.IsNullOrEmpty(r.canonSource)) problems.Add($"{owner} cites no vault note");
            if (string.Equals(r.parentA, r.parentB, StringComparison.OrdinalIgnoreCase)) problems.Add($"{owner} blends a tradition with itself");
            foreach (var key in new[] { r.parentA, r.parentB })
            {
                var p = SyncretismRules.Parent(key);
                bool exists = p.kind == "tradition" ? traditions.Definition(p.id) != null : p.kind == "local" && LocalPracticeCatalog.Find(p.id) != null;
                if (!exists) problems.Add($"{owner} has a parent '{key}' that is neither a tradition nor a custom");
            }
            var result = traditions.Definition(r.result);
            if (result == null) problems.Add($"{owner} makes '{r.result}', which is not a tradition");
            else if (!result.linkedOnly) problems.Add($"{owner} makes '{r.result}', which could also arise by itself (it must be linked only)");
            int depth = SyncretismRules.Depth(r.result, tuning);
            if (depth > tuning.maxDepth) problems.Add($"{owner} is reworked {depth} times, more than {tuning.maxDepth}");
            if (ages.Count > 0 && !ages.Contains(r.minAge)) problems.Add($"{owner} begins in Age {r.minAge}, which does not exist");
            if (r.maxAge >= 0 && r.maxAge < r.minAge) problems.Add($"{owner} ends before it begins");
            if (!string.IsNullOrEmpty(r.technology) && !GameCatalog.Technologies.Contains(r.technology)) problems.Add($"{owner} needs technology '{r.technology}', which does not exist");
            if (r.adaptUnity < 0f || r.replaceUnity < 0f) problems.Add($"{owner} pays Unity back");
        }
    }

    // Observances (ObservanceTuning): unique ids, a vault note and canon label each, Ages and technology that exist, a
    // recurrence, rites and foods that exist, costs in real resources; a remembrance can always be kept quietly (hardship),
    // a grove or peak observance says where; holidays keep their own definition.
    private static void ValidateObservances(List<string> problems, CultureSettings settings, CultureLifeTuning life)
    {
        var tuning = settings != null && settings.observances != null ? settings.observances : new ObservanceTuning();
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        if (tuning.prepareWindow < 1 || tuning.postponeMax < 1 || tuning.maxObservances < 1 || tuning.historyKept < 1) problems.Add("Culture: observance windows, cap or history are not positive");
        foreach (ObservanceScale s in Enum.GetValues(typeof(ObservanceScale)))
            if (tuning.scales.All(x => x == null || x.scale != s)) problems.Add($"Culture: observances have no numbers for the scale {s}");
        if (tuning.Scale(ObservanceScale.Quiet).food > 0f) problems.Add("Culture: a quiet observance must ask for no food");
        if (tuning.Definition(ObservanceRules.Anniversary) == null) problems.Add($"Culture: no observance '{ObservanceRules.Anniversary}' for the holidays");
        var ages = new HashSet<int>(GameCatalog.Ages.All.Where(a => a != null).Select(a => a.number));
        foreach (var d in tuning.definitions)
        {
            if (d == null || string.IsNullOrEmpty(d.id) || string.IsNullOrEmpty(d.name)) { problems.Add("Culture: an observance has no id or name"); continue; }
            string owner = $"Culture: observance '{d.id}'";
            if (tuning.definitions.Count(o => o != null && string.Equals(o.id, d.id, StringComparison.OrdinalIgnoreCase)) > 1) problems.Add($"{owner} is defined twice");
            if (string.IsNullOrEmpty(d.canonSource)) problems.Add($"{owner} cites no vault note");
            if (ages.Count > 0 && !ages.Contains(d.minAge)) problems.Add($"{owner} begins in Age {d.minAge}, which does not exist");
            // The last Age is the vault's own bound (Civic.md: "Ages 0-III"): it closes a practice and unlocks nothing, so
            // it may lie past the Ages the game has authored yet; it only must not end before it begins.
            if (d.maxAge >= 0 && d.maxAge < d.minAge) problems.Add($"{owner} ends in Age {d.maxAge}, before it begins (Age {d.minAge})");
            if (!string.IsNullOrEmpty(d.technology) && !GameCatalog.Technologies.Contains(d.technology)) problems.Add($"{owner} needs technology '{d.technology}', which does not exist");
            if (!string.IsNullOrEmpty(d.family) && !CultureRules.TryFamily(d.family, out _)) problems.Add($"{owner} belongs to '{d.family}', which is not a way of living");
            if (d.recurrences == null || d.recurrences.Count == 0) problems.Add($"{owner} never comes round (no recurrence)");
            if (d.grounds != null && d.grounds.Count > 0 && string.IsNullOrEmpty(d.groundText)) problems.Add($"{owner} needs a ground but does not say which");
            if ((d.objective == ObservanceObjective.Remembrance || d.objective == ObservanceObjective.Gathering) && !d.Allows(ObservanceScale.Quiet)) problems.Add($"{owner} cannot be kept quietly, so hardship would forbid it");
            foreach (var r in d.repertoire ?? new List<string>())
                if (life.Activity(r) == null) problems.Add($"{owner} is kept with rite '{r}', which does not exist");
            foreach (var f in d.suggestedFoods ?? new List<string>())
            {
                string resource = life.Recipe(f)?.dish ?? f;
                if (pantry != null && pantry.Kind(resource) == null) problems.Add($"{owner} suggests '{f}', which is neither a recipe nor a stored food");
            }
            foreach (var c in (d.establishCost ?? new List<ResourceAmount>()).Concat(d.keepCost ?? new List<ResourceAmount>()))
                if (c != null && !string.IsNullOrEmpty(c.resource) && !GameCatalog.IsResource(c.resource)) problems.Add($"{owner} costs '{c.resource}', which is not a resource");
        }
    }

    private static void ValidateCultureLife(List<string> problems)
    {
        var settings = UnityEngine.Resources.Load<CultureSettings>("Culture/Culture");
        var life = settings != null && settings.life != null ? settings.life : new CultureLifeTuning();
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        void Resource(string owner, string resource) { if (!string.IsNullOrEmpty(resource) && !GameCatalog.IsResource(resource)) problems.Add($"Culture: {owner} names '{resource}', which is not a resource"); }
        void Technology(string owner, string technology) { if (!string.IsNullOrEmpty(technology) && !GameCatalog.Technologies.Contains(technology)) problems.Add($"Culture: {owner} needs technology '{technology}', which does not exist"); }
        Resource("Unity", life.unityResource);
        Technology("festivals", life.festivalTechnology);
        // What the Eleos Blooms give (their resource, yields and harvest): steeped or distilled, a bloom is a tea, never a beverage.
        var blooms = CultureSystem.BloomResources();
        foreach (var r in life.recipes)
        {
            if (r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.dish)) { problems.Add("Culture: a recipe has no id or dish"); continue; }
            Resource($"recipe '{r.id}'", r.dish);
            if (pantry != null && pantry.Kind(r.dish) == null) problems.Add($"Culture: recipe '{r.id}' makes '{r.dish}', which is not a Pantry food kind");
            if (r.inputs == null || r.inputs.Count == 0 || r.output <= 0f) problems.Add($"Culture: recipe '{r.id}' has no inputs or makes nothing");
            foreach (var i in r.inputs ?? new List<ResourceAmount>()) Resource($"recipe '{r.id}'", i?.resource);
            Technology($"recipe '{r.id}'", r.technology);
            if (life.recipes.Count(o => o != null && o.id == r.id) > 1) problems.Add($"Culture: recipe id '{r.id}' is used twice");
            var product = pantry?.Kind(r.dish);
            if (product == null) continue;
            // The cellar makes beverages and only beverages; the kitchen never does.
            if (r.InCellar && product.cuisine != FoodClass.Beverage) problems.Add($"Culture: recipe '{r.id}' is made in the cellar ({r.method}) but '{r.dish}' is not a Beverage");
            if (!r.InCellar && product.cuisine == FoodClass.Beverage) problems.Add($"Culture: recipe '{r.id}' cooks '{r.dish}', a Beverage: brew or distil it in the cellar");
            if (r.InCellar)
            {
                foreach (var i in (r.inputs ?? new List<ResourceAmount>()).Where(i => i != null && !string.IsNullOrEmpty(i.resource)))
                    if (blooms.Contains(i.resource) || pantry.Kind(i.resource)?.cuisine == FoodClass.EleosTea)
                        problems.Add($"Culture: drink recipe '{r.id}' is made from '{i.resource}', an Eleos Bloom or tea: a beverage never is");
                if (r.method == KitchenMethod.Distill && !(r.inputs ?? new List<ResourceAmount>()).Any(i => i != null && pantry.Kind(i.resource)?.cuisine == FoodClass.Beverage))
                    problems.Add($"Culture: recipe '{r.id}' distils nothing fermented: a spirit is distilled from a beverage");
                continue; // a drink is kept for joy: it need not feed more than went into it
            }
            float ValueOf(string resource) { var k = pantry.Kind(resource); return k == null || !CultureRules.Feeds(k.cuisine) ? 0f : k.foodValue; }
            var (input, output) = CultureLifeRules.FoodValue(r, ValueOf, r.output);
            if (output <= input) problems.Add($"Culture: recipe '{r.id}' feeds no more than its inputs ({input:0.##} food value in, {output:0.##} out): cooking should pay");
        }
        // Every beverage the pantry keeps is made in the cellar (the map grows none).
        foreach (var kind in pantry?.kinds ?? new List<FoodKind>())
            if (kind != null && kind.cuisine == FoodClass.Beverage && !life.IsDrink(kind.resource))
                problems.Add($"Culture: '{kind.resource}' is a Beverage no cellar recipe makes");
        foreach (var a in life.activities)
        {
            if (a == null || string.IsNullOrEmpty(a.id)) { problems.Add("Culture: a rite has no id"); continue; }
            foreach (var c in a.cost) Resource($"rite '{a.id}'", c?.resource);
            Technology($"rite '{a.id}'", a.technology);
            if (!string.IsNullOrEmpty(a.family) && !CultureRules.TryFamily(a.family, out _)) problems.Add($"Culture: rite '{a.id}' leans toward unknown family '{a.family}'");
        }
        foreach (var l in life.landmarks)
        {
            if (l == null || string.IsNullOrEmpty(l.id)) { problems.Add("Culture: a landmark has no id"); continue; }
            if (!CultureRules.TryFamily(l.family, out _)) problems.Add($"Culture: landmark '{l.id}' belongs to unknown family '{l.family}'");
            foreach (var c in l.cost) Resource($"landmark '{l.id}'", c?.resource);
            if (life.landmarks.Count(o => o != null && o.id == l.id) > 1) problems.Add($"Culture: landmark id '{l.id}' is used twice");
        }
        foreach (var x in life.luxuries)
            foreach (var resource in x?.resources ?? new List<string>()) Resource($"luxury '{x.category}'", resource);
        foreach (var good in life.goods ?? new List<CulturalGoodSpec>())
        {
            if (good == null || string.IsNullOrEmpty(good.resource)) { problems.Add("Culture: a cultural good has no resource"); continue; }
            Resource("cultural good", good.resource);
            if (good.faithPerUnit < 0f) problems.Add($"Culture: {good.resource} has negative Faith");
            if (life.goods.Count(g => g != null && g.resource == good.resource) > 1) problems.Add($"Culture: repeated good {good.resource}");
            if (!life.luxuries.Any(l => l != null && l.resources.Contains(good.resource))) problems.Add($"Culture: {good.resource} belongs to no luxury category");
        }
        foreach (var garden in life.gardens ?? new List<CulturalSiteSpec>())
        {
            if (garden == null) { problems.Add("Culture: empty garden"); continue; }
            if (!GameCatalog.World.All.Any(w => w.generation.ResourceSite(garden.site) != null)) problems.Add($"Culture: unknown garden {garden.site}");
            if (!life.luxuries.Any(l => l != null && l.category == garden.category)) problems.Add($"Culture: {garden.site} has no amenity category");
            if (garden.amenity <= 0f || garden.faithPerUnit < 0f) problems.Add($"Culture: invalid amenity or Faith for {garden.site}");
        }
        if (life.livingFull <= life.livingFrom) problems.Add("Culture: livingFull must be above livingFrom");
    }

    private static void ValidatePantry(List<string> problems)
    {
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        if (pantry == null) { problems.Add("No Resources/Food/Pantry: there are no food stores (claims cannot be paid)."); return; }
        foreach (var kind in pantry.kinds)
        {
            if (kind == null || string.IsNullOrEmpty(kind.resource)) { problems.Add("Pantry has an empty food kind"); continue; }
            if (!GameCatalog.IsResource(kind.resource)) problems.Add($"Pantry: food kind '{kind.resource}' is not a resource");
            if (kind.foodValue <= 0f && kind.cuisine != FoodClass.Spice) problems.Add($"Pantry: food kind '{kind.resource}' is worth nothing (foodValue {kind.foodValue}); only a Spice may be");
            if (!string.IsNullOrEmpty(kind.leaves) && !GameCatalog.IsResource(kind.leaves)) problems.Add($"Pantry: '{kind.resource}' leaves '{kind.leaves}', which is not a resource");
        }
        foreach (var a in pantry.startingStores)
            if (a != null && pantry.Kind(a.resource) == null) problems.Add($"Pantry: starting store '{a.resource}' is not a food kind");
        foreach (var p in pantry.preservation)
            if (p != null && !string.IsNullOrEmpty(p.technology) && !GameCatalog.Technologies.Contains(p.technology)) problems.Add($"Pantry: preservation technology '{p.technology}' does not exist");
    }

    // The people's health (Resources/Population/Health): each pressure once; treatments name a technology, civic,
    // production unit or score that exists, and ease something; crisis loads name an Age's crisis and fit its stages;
    // harsh weathers are weather profiles.
    private static void ValidateHealth(List<string> problems)
    {
        var health = UnityEngine.Resources.Load<HealthSettings>("Population/Health");
        if (health == null) { problems.Add("No Resources/Population/Health: the people's health has no pressures."); return; }
        var t = health.tuning ?? new HealthTuning();
        if (t.activateAt <= 0f || t.dormantAt > t.activateAt) problems.Add($"Health: a pressure wakes at {t.activateAt} and sleeps below {t.dormantAt}; it must wake above 0 and sleep at or below where it wakes");
        foreach (var pressure in HealthRules.All)
        {
            int count = health.pressures.Count(p => p != null && p.pressure == pressure);
            if (count != 1) problems.Add($"Health: pressure {pressure} is described {count} times (once expected)");
        }
        foreach (var spec in health.pressures.Where(p => p != null))
        {
            string owner = $"Health: {spec.title ?? spec.pressure.ToString()}";
            if (spec.emergesAtPeople < 0 || spec.fullAtPeople < spec.emergesAtPeople) problems.Add($"{owner}: its causes emerge at {spec.emergesAtPeople} people and are full at {spec.fullAtPeople}; neither may be negative, and full must not come first");
            foreach (var cure in spec.treatments)
            {
                if (cure == null) { problems.Add($"{owner} has an empty treatment"); continue; }
                if (cure.ease <= 0f && cure.capacity <= 0f) problems.Add($"{owner}: treatment {cure.kind} '{cure.name}' neither eases the pressure nor raises its checkpoints");
                if (cure.capacity < 0f) problems.Add($"{owner}: treatment {cure.kind} '{cure.name}' has a negative capacity");
                if (cure.fullAt <= 0f && cure.kind != TreatmentKind.Technology && cure.kind != TreatmentKind.Civic) problems.Add($"{owner}: treatment {cure.kind} '{cure.name}' needs fullAt above 0");
                bool named = cure.kind == TreatmentKind.Technology || cure.kind == TreatmentKind.Civic || cure.kind == TreatmentKind.Building || cure.kind == TreatmentKind.Score || cure.kind == TreatmentKind.Enclave;
                if (named && string.IsNullOrWhiteSpace(cure.name)) { problems.Add($"{owner}: a {cure.kind} treatment has no name"); continue; }
                if (cure.kind == TreatmentKind.Enclave && !HealthRules.TryEnclaveFamily(cure.name, out _)) problems.Add($"{owner}: treatment Enclave '{cure.name}' is no Enclave family");
                if (cure.kind == TreatmentKind.Technology && !GameCatalog.Technologies.Contains(cure.name)) problems.Add($"{owner}: treatment technology '{cure.name}' does not exist");
                if (cure.kind == TreatmentKind.Civic && !GameCatalog.Civics.Contains(cure.name)) problems.Add($"{owner}: treatment civic '{cure.name}' does not exist");
                if (cure.kind == TreatmentKind.Building && !GameCatalog.IsProductionUnit(cure.name)) problems.Add($"{owner}: treatment building '{cure.name}' is not a production unit");
            }
            foreach (var cascade in spec.cascades)
                if (cascade == null || cascade.from == spec.pressure) problems.Add($"{owner}: a cascade must come from another pressure");
        }
        foreach (var crisis in health.crises)
        {
            if (crisis == null) { problems.Add("Health has an empty crisis load"); continue; }
            var age = GameCatalog.Ages.All.FirstOrDefault(a => a.HasCrisis && string.Equals(a.crisisTitle?.Trim(), crisis.crisis?.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (age == null) problems.Add($"Health: crisis '{crisis.crisis}' is no Age's crisis title");
            else if (crisis.stageLoads.Count > age.crisisStages.Count) problems.Add($"Health: crisis '{crisis.crisis}' has {crisis.stageLoads.Count} stage loads for {age.crisisStages.Count} stages");
        }
        foreach (var weather in health.weathers)
            if (weather == null || !GameCatalog.Weather.Contains(weather.weather)) problems.Add($"Health: harsh weather '{weather?.weather}' is not a weather profile");
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
            foreach (var rule in gen.intersectionTerrains) if (rule == null || gen.Terrain(rule.terrain) == null) problems.Add($"{owner} intersection ground names unknown terrain '{rule?.terrain}'");
            foreach (var biome in gen.macroBiomes)
            {
                if (biome == null) { problems.Add($"{owner} has an empty Macro Biome"); continue; }
                if (biome.terrains.Count == 0) problems.Add($"{owner} biome '{biome.id}' lays down no terrain");
                foreach (var rule in biome.terrains) if (rule == null || gen.Terrain(rule.terrain) == null) problems.Add($"{owner} biome '{biome.id}' names unknown terrain '{rule?.terrain}'");
                foreach (int o in biome.orientations) if (o < 0 || o > 5) problems.Add($"{owner} biome '{biome.id}' allows orientation {o} (0-5)");
            }
            foreach (var quadrant in gen.quadrants)
            {
                if (quadrant == null) { problems.Add($"{owner} has an empty quadrant"); continue; }
                foreach (var biome in quadrant.macroBiomes) if (gen.MacroBiome(biome) == null) problems.Add($"{owner} quadrant '{quadrant.id}' names unknown Macro Biome '{biome}'");
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
                foreach (var biome in feature.macroBiomes) if (gen.MacroBiome(biome) == null) problems.Add($"{featureOwner} names unknown Macro Biome '{biome}'");
            }
            var siteIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            // The bestiary (CreatureTaxonomy): a real AECOR subgroup for the diet, sane numbers, and no Atonalis.
            var speciesIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var species in gen.species ?? new List<SpeciesSpec>())
            {
                if (species == null) { problems.Add($"{owner} has an empty species"); continue; }
                string speciesOwner = $"{owner} species '{species.id}'";
                if (string.IsNullOrEmpty(species.id) || !speciesIds.Add(species.id)) problems.Add($"{speciesOwner}: missing or repeated id");
                if (string.IsNullOrEmpty(species.name)) problems.Add($"{speciesOwner} has no name");
                if (!CreatureTaxonomy.IsValid(species.diet, species.subgroup))
                    problems.Add($"{speciesOwner}: a {species.diet} cannot be {species.subgroup} (it can be {string.Join(", ", CreatureTaxonomy.SubgroupsOf(species.diet))})");
                if (species.groupMin < 0 || species.groupMax < 0 || species.groupMax > 0 && species.groupMin > species.groupMax) problems.Add($"{speciesOwner}: its group runs from {species.groupMin} to {species.groupMax}");
                if (species.structure < 0f || species.structure > 1f) problems.Add($"{speciesOwner}: structure must lie between 0 and 1");
                if (gen.Threat(species.id) != null || gen.eleos != null && gen.eleos.atonalisThreats.Contains(species.id))
                    problems.Add($"{speciesOwner} shares an id with a threat: the Atonalis are threats, never species of the bestiary");
                foreach (var prey in species.prey ?? new List<string>())
                {
                    if (gen.Species(prey) == null) problems.Add($"{speciesOwner} preys on unknown species '{prey}'");
                    else if (string.Equals(prey, species.id, System.StringComparison.OrdinalIgnoreCase)) problems.Add($"{speciesOwner} preys on itself");
                }
                if (species.prey != null && species.prey.Count > 0 && species.diet == CreatureDiet.Herbivore) problems.Add($"{speciesOwner} is a Herbivore but has prey");
            }
            // The ecology (WorldEcology): capacities and growth must be positive, thresholds in order.
            var eco = gen.ecology;
            if (eco == null) problems.Add($"{owner} has no ecology settings");
            else
            {
                if (eco.smallPerCell <= 0f || eco.mediumPerCell <= 0f || eco.largePerCell <= 0f || eco.gargantuanPerCell <= 0f) problems.Add($"{owner} ecology: groups per cell must be above 0");
                if (eco.verySlowGrowth <= 0f || eco.slowGrowth < eco.verySlowGrowth || eco.mediumGrowth < eco.slowGrowth || eco.fastGrowth < eco.mediumGrowth) problems.Add($"{owner} ecology: growth must rise from very slow to fast");
                if (eco.depletedBelow >= eco.denAt) problems.Add($"{owner} ecology: a new den (denAt) must need more than depletion (depletedBelow)");
                if (eco.huntTake <= 0f || eco.huntYield <= 0f) problems.Add($"{owner} ecology: a hunt must take (huntTake) and bring back (huntYield) something");
            }
            // Behavior (WorldBehavior): a step needs some temper, and living beside a species must be able to calm it one.
            var beh = gen.behavior;
            if (beh == null) problems.Add($"{owner} has no behavior settings");
            else
            {
                if (beh.stepAt <= 0f || beh.stepAt > 1f) problems.Add($"{owner} behavior: stepAt must be above 0 and at most 1 (temper runs -1 to +1)");
                if (beh.coexist > 0f && beh.coexistCap < beh.stepAt) problems.Add($"{owner} behavior: coexistCap is below stepAt, so living beside a species could never calm it a step");
                if (beh.memoryKept < 0f || beh.memoryKept > 1f) problems.Add($"{owner} behavior: memoryKept is a share (0-1)");
            }
            // The creatures' calendar (WorldRhythm): one rhythm per Echo, one shape value per Phase, no negative multiplier.
            var rhythm = gen.rhythm;
            if (rhythm == null || rhythm.echoes == null || rhythm.echoes.Count != TimeSystemLogic.EchoesPerCycle)
                problems.Add($"{owner} rhythm: needs exactly {TimeSystemLogic.EchoesPerCycle} Echo rhythms (Resonance, Crescendo, Dissonance, Silence)");
            else
            {
                foreach (var r in rhythm.echoes)
                {
                    if (r == null || string.IsNullOrEmpty(r.name) || string.IsNullOrEmpty(r.summary)) { problems.Add($"{owner} rhythm: every Echo needs a name and a summary"); continue; }
                    if (r.growth < 0f || r.detritivoreGrowth < 0f || r.dispersal < 0f || r.predation < 0f || r.yields < 0f || r.harm < 0f || r.ease < 0f)
                        problems.Add($"{owner} rhythm: {r.name} has a negative multiplier");
                }
                if (rhythm.phaseShape == null || rhythm.phaseShape.Length != TimeSystemLogic.PhasesPerEcho || rhythm.phaseShape.Any(v => v < 0f || v > 1f))
                    problems.Add($"{owner} rhythm: phaseShape needs {TimeSystemLogic.PhasesPerEcho} values between 0 and 1");
            }

            foreach (var site in gen.resourceSites)
            {
                if (site == null) { problems.Add($"{owner} has an empty resource site"); continue; }
                string siteOwner = $"{owner} resource site '{site.id}'";
                if (string.IsNullOrEmpty(site.id) || !siteIds.Add(site.id)) problems.Add($"{siteOwner}: missing or repeated id");
                CheckAmounts(siteOwner, "yields", site.yields, problems);
                CheckAmounts(siteOwner, "harvest", site.harvest, problems);
                if (!string.IsNullOrEmpty(site.resource) && !GameCatalog.IsResource(site.resource)) problems.Add($"{siteOwner} names '{site.resource}', which is not a resource in Resources/{GameCatalog.Resources.ResourcesPath}");
                foreach (var terrain in site.terrains.Concat(site.nearTerrains)) if (gen.Terrain(terrain) == null) problems.Add($"{siteOwner} names unknown terrain '{terrain}'");
                foreach (var biome in site.macroBiomes) if (gen.MacroBiome(biome) == null) problems.Add($"{siteOwner} names unknown Macro Biome '{biome}'");
                if (site.seeds && site.harvest.Count == 0) problems.Add($"{siteOwner} gives seeds but has no harvest to gather them with");
                if (site.harvest.Count == 0 && string.IsNullOrEmpty(site.untouchable)) problems.Add($"{siteOwner} has no harvest: say why on the card (untouchable)");
                foreach (var cover in site.covers ?? new List<string>()) if (gen.Cover(cover) == null) problems.Add($"{siteOwner} stands in unknown cover '{cover}'");
                // Creatures: every den or herd names its species; a bloom may name one only as a Trapper predator.
                var species = gen.Species(site.species);
                if (!string.IsNullOrEmpty(site.species) && species == null) problems.Add($"{siteOwner} names unknown species '{site.species}'");
                if (site.kind == ResourceKind.Fauna && string.IsNullOrEmpty(site.species)) problems.Add($"{siteOwner} is Fauna but names no species");
                if (!string.IsNullOrEmpty(site.species) && site.kind != ResourceKind.Fauna && site.kind != ResourceKind.Bloom) problems.Add($"{siteOwner} names a species but is neither Fauna nor a Bloom");
                if (species != null && site.kind == ResourceKind.Bloom && (site.niche != BloomNiche.Predator || species.subgroup != CreatureSubgroup.Trapper))
                    problems.Add($"{siteOwner}: a bloom that names a species must be a predator (niche) whose species is a Trapper");
                // Eleos Blooms: only a bloom feeds on residue, drinks Dissonance, heals or preys; a predator needs its menace.
                bool bloomTraits = site.dissonanceAura != 0f || site.dangerAura > 0f || site.soothe > 0f;
                if (site.kind != ResourceKind.Bloom && bloomTraits) problems.Add($"{siteOwner} drinks Dissonance, heals or preys but is not an Eleos Bloom (kind Bloom)");
                if (site.kind != ResourceKind.Bloom && site.flavors != null && site.flavors.Count > 0) problems.Add($"{siteOwner} catalogues feelings but is not an Eleos Bloom (flavors)");
                if (site.kind == ResourceKind.Bloom)
                {
                    if (site.soothe < 0f || site.dangerAura < 0f) problems.Add($"{siteOwner}: soothe and dangerAura cannot be negative");
                    if (site.niche == BloomNiche.Predator && site.dangerAura <= 0f) problems.Add($"{siteOwner} is a predatory bloom with no danger (dangerAura)");
                    if (site.niche != BloomNiche.Predator && site.dangerAura > 0f) problems.Add($"{siteOwner} casts danger but is not a predator (niche)");
                    if (site.niche == BloomNiche.Healer && site.soothe <= 0f) problems.Add($"{siteOwner} is a healing bloom that eases no strain (soothe)");
                    if (site.flavors != null && site.flavors.Any(f => f == null || f.weight <= 0f)) problems.Add($"{siteOwner} catalogues a feeling with no weight (flavors)");
                    if (site.flavors != null && site.flavors.Where(f => f != null).GroupBy(f => f.feeling).Any(g => g.Count() > 1)) problems.Add($"{siteOwner} catalogues a feeling twice (flavors)");
                }
                // Blooms that grow by themselves are sterile; what they grow beside and decay into must exist.
                if (site.sprouts && site.seeds) problems.Add($"{siteOwner} grows by itself (sprouts) but gives seeds: sterile blooms give none");
                if (site.minimumHurt > site.maximumHurt) problems.Add($"{siteOwner}: minimumHurt is above maximumHurt");
                foreach (var near in site.nearSites ?? new List<string>()) if (gen.ResourceSite(near) == null) problems.Add($"{siteOwner} grows beside unknown site '{near}'");
                if (!string.IsNullOrEmpty(site.turnsInto) && (gen.ResourceSite(site.turnsInto) == null || string.Equals(site.turnsInto, site.id, System.StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{siteOwner} decays into unknown site '{site.turnsInto}'");
                if (!string.IsNullOrEmpty(site.turnsInto) && site.turnHurt <= 0f && site.turnCoherence <= 0f) problems.Add($"{siteOwner} turns into '{site.turnsInto}' but nothing turns it (turnHurt or turnCoherence)");
                if (!string.IsNullOrEmpty(site.turnsInto) && site.turnPerEcho <= 0f) problems.Add($"{siteOwner}: its turn never runs (turnPerEcho)");
                if (site.silverRiverReach < 0 || site.silverLakeReach < 0 || site.fadePerEcho < 0f) problems.Add($"{siteOwner}: silver reaches and fadePerEcho cannot be negative");
                // Blooms among the living: what drifts and what the settlements draw up (WorldResources.Drift, Volunteer).
                if (site.driftPerEcho < 0f || site.driftPerEcho > 1f) problems.Add($"{siteOwner}: driftPerEcho is a chance between 0 and 1");
                bool volunteers = site.drawnBy != BloomDraw.None || (site.districts != null && site.districts.Count > 0);
                if (volunteers && site.kind != ResourceKind.Bloom) problems.Add($"{siteOwner}: only Eleos Blooms grow near settlements (drawnBy, districts)");
                if (volunteers && (site.sprouts || !string.IsNullOrEmpty(site.species))) problems.Add($"{siteOwner}: a sterile bloom or a creature's den never grows near settlements (drawnBy, districts)");
                if (volunteers && site.volunteerWeight <= 0f) problems.Add($"{siteOwner} grows near settlements but is never picked (volunteerWeight)");
                foreach (var district in site.districts ?? new List<string>())
                    if (world.settlements?.tributaries == null || !world.settlements.tributaries.AllDistricts.Any(d => string.Equals(d.id, district, System.StringComparison.OrdinalIgnoreCase)))
                        problems.Add($"{siteOwner} is drawn by unknown district '{district}'");
            }
            // Creatures drawn to blooms (SpeciesSpec.blooms): a bloom's id, its niche word, or "bloom".
            foreach (var species in gen.species ?? new List<SpeciesSpec>())
                foreach (var drawn in species?.blooms ?? new List<string>())
                {
                    bool niche = drawn == "bloom" || drawn == "listener" || drawn == "healer" || drawn == "predator";
                    if (!niche && gen.ResourceSite(drawn)?.kind != ResourceKind.Bloom) problems.Add($"{owner} species '{species.id}' is drawn to '{drawn}', which is neither an Eleos Bloom nor a niche (listener, healer, predator, bloom)");
                }
            var eleos = gen.eleos;
            if (eleos == null) problems.Add($"{owner} has no Eleos settings (Emotional Residue)");
            else if (eleos.capitalResidue < 0f || eleos.majorResidue < 0f || eleos.settlementResidue < 0f || eleos.strainResidue < 0f || eleos.ruinResidue < 0f || eleos.dissonanceResidue < 0f)
                problems.Add($"{owner}: Emotional Residue sources cannot be negative");
            if (eleos != null && eleos.contentStrain >= eleos.sufferingStrain) problems.Add($"{owner}: a settlement cannot be Content and Suffering at once (contentStrain must lie below sufferingStrain)");
            var coverIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var cover in gen.covers ?? new List<CoverSpec>())
            {
                if (cover == null) { problems.Add($"{owner} has an empty cover"); continue; }
                string coverOwner = $"{owner} cover '{cover.id}'";
                if (string.IsNullOrEmpty(cover.id) || !coverIds.Add(cover.id)) problems.Add($"{coverOwner}: missing or repeated id");
                CheckAmounts(coverOwner, "yields", cover.yields, problems);
                CheckAmounts(coverOwner, "forage", cover.forage, problems);
                foreach (var terrain in cover.terrains) if (gen.Terrain(terrain) == null) problems.Add($"{coverOwner} names unknown terrain '{terrain}'");
                if (cover.minSize > cover.maxSize) problems.Add($"{coverOwner}: minSize {cover.minSize} is above maxSize {cover.maxSize}");
                if (cover.travel < 1f) problems.Add($"{coverOwner}: travel {cover.travel} below 1 would make it a shortcut (the route search needs every step at least as costly as open ground)");
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
            foreach (var biome in field.macroBiomes) if (gen.MacroBiome(biome) == null) problems.Add($"{fieldOwner} names unknown Macro Biome '{biome}'");
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
        // Outskirt tributaries and their districts.
        var tributaries = rules.tributaries;
        if (tributaries == null) return;
        if (!string.IsNullOrEmpty(tributaries.technology) && !GameCatalog.Technologies.Contains(tributaries.technology)) problems.Add($"{owner}: tributary technology '{tributaries.technology}' does not exist");
        CheckAmounts($"{owner} tributaries", "cost", tributaries.cost, problems);
        if (tributaries.District(tributaries.generalist) == null) problems.Add($"{owner}: the generalist district '{tributaries.generalist}' does not exist");
        var districtIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        // One district for each Enclave category (Enclave.md).
        foreach (EnclaveFamily family in System.Enum.GetValues(typeof(EnclaveFamily)))
            if (!tributaries.AllDistricts.Any(d => string.Equals(d.enclave, family.ToString(), System.StringComparison.OrdinalIgnoreCase)))
                problems.Add($"{owner}: no district follows the {family} Enclave category");
        foreach (var district in tributaries.AllDistricts)
        {
            string districtOwner = $"{owner} district '{district.id}'";
            if (!districtIds.Add(district.id)) problems.Add($"{owner}: two districts have the id '{district.id}'");
            CheckAmounts(districtOwner, "cost", district.cost, problems);
            CheckAmounts(districtOwner, "yields", district.yields, problems);
            if (!string.IsNullOrEmpty(district.enclave) && !System.Enum.TryParse<EnclaveFamily>(district.enclave, true, out _)) problems.Add($"{districtOwner}: '{district.enclave}' is not an Enclave category");
            if (string.IsNullOrEmpty(district.enclave) && !string.Equals(district.id, tributaries.generalist, System.StringComparison.OrdinalIgnoreCase)) problems.Add($"{districtOwner} follows no Enclave category");
            foreach (var stat in district.stats)
                if (stat == null || !WorldTributaries.StatEffectType(stat.stat).HasValue) problems.Add($"{districtOwner}: '{stat?.stat}' is not a pillar, substat or morale");
            foreach (var rule in district.adjacency)
            {
                if (rule != null && rule.source == AdjacencySource.District && !string.IsNullOrEmpty(rule.district) && tributaries.District(rule.district) == null)
                    problems.Add($"{districtOwner}: adjacency names unknown district '{rule.district}'");
                if (rule != null && rule.source == AdjacencySource.Enclave && !string.IsNullOrEmpty(rule.district) && !string.Equals(rule.district, "any", System.StringComparison.OrdinalIgnoreCase)
                    && !System.Enum.TryParse<EnclaveFamily>(rule.district, true, out _))
                    problems.Add($"{districtOwner}: adjacency names unknown Enclave category '{rule.district}'");
            }
        }
    }

    // The stencil, the handmade tiles and the quadrant catalogs fit together (roadmap WG01): every stencil quadrant has a
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
                if (gen.MacroBiome(tile.macroBiome) == null) problems.Add($"{owner}: handmade tile '{tile.id}' names unknown Macro Biome '{tile.macroBiome}'");
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
            foreach (var entry in age.actOfFateStories ?? new List<string>())
                foreach (var knot in (entry ?? string.Empty).Split(',')) Check(owner, knot);
            foreach (var stage in age.crisisStages ?? new List<CrisisStageSpec>()) if (stage != null) Check($"{owner} stage '{stage.name}'", stage.story);
        }
        foreach (var world in GameCatalog.World.All)
        {
            foreach (var feature in world.generation.features) if (feature != null) Check($"World feature '{feature.id}'", feature.story);
            foreach (var site in world.generation.resourceSites)
                foreach (var knot in site?.expeditionStories ?? new List<string>()) Check($"World resource site '{site.id}'", knot);
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
