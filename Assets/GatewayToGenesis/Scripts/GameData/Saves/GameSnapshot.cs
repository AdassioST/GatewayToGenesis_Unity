using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Version 1's explicit state contract. UI references, event delegates and derived caches are excluded.</summary>
public static class GameSnapshot
{
    public static readonly Dictionary<Type, string[]> Schema = new Dictionary<Type, string[]>
    {
        { typeof(TimeSystemLogic), Words("CurrentCycle CurrentEcho CurrentPhase CurrentSeventh timeSinceLastSeventh totalPhaseIndex currentCycleName canTrackTime isTimePaused") },
        { typeof(GameResourceSlot), Words("amount maxAmount baseClickPower") },
        { typeof(GameProductionSlot), Words("_built _insufficientProduction incrementalCost") },
        { typeof(GameTechnologySlot), Words("amount maxAmount researchProgress researchCost isUnlocked enlightenedCompleted alreadyClicked isVisible enlightenedBonusPercent enlightenedBonusApplied") },
        { typeof(GameUnitsLogic), Words("technologyProgress activeTechnologySlot storageBreakdown _permanentClickPower _clickedTotals ProductionEfficiency ConstructionCost ClickPower ProductionScaling") },
        { typeof(GlobalProductionManager), Words("techTier costBalance ResourceModifiers") },
        { typeof(PopGrowthLogic), Words("foodThreshold vagrantToPopulationRate researchPerPopulation starvationRecoveryRate foodDemandBuffer demandRateConstant sustainabilityTier demandModifier housing population vagrants freeHousing deaths vagrantDeaths trueDeaths allowVagrants isFoodScarce _birthCarry _naturalDeathCarry _famineDeathCarry _hungerDays _arrivalElapsed _foundingMigrantsRemaining _demographyInitialized _births _bandsOnTheRoad _caravanProgress _caravans _gateLots _admissions _baseHousingBySource _sceneHousing HousingModifiers") },
        { typeof(StatManager), Words("_basePillars _substatAdjustments _baseGlobals _moraleTimedRemainingBySource _moraleTimedValueBySource _moralePersistentBySource morale satisfactionPoints satisfactionLevel Modifiers") },
        { typeof(GovernmentLogic), Words("unlockedSeatCount _headOfState activeRegularSeats civicCouncilSeats _cooldowns _appliedCouncilSources") },
        { typeof(CivicManager), Words("startingAeonicSlots startingMajorSlots startingMinorSlots maxAeonicSlots maxMajorSlots maxMinorSlots _activeByTier removalPenalties") },
        { typeof(LegendProgress), Words("_recruited _started") },
        { typeof(LegendLeaderLogic), Words("seventhsForActivation") },
        { typeof(AgeProgression), Words("Current Sevenths StageReached CrisisDeclared Ending _history _stories _schedule _nextBeat _waitingStory _waitingBaseline _waitedSevenths _appliedFoodPercent _appliedCrisisSource _begun _resting _eraScoreByAct _eraLog _eraTimeline") },
        { typeof(WorldSystem), Words("_agesPlaced _yieldSources LastLeader LastNotice _settlements _routes _enclaves _units _claims _granted _expeditionLines _journeys _nextUnitId _eraAwarded _unitTimeRemainder _adopted _adoptionProgress _driftProgress _borderPolicy _plantings _grievances _resourceSites _claiming _claimProgress _adoptionRolls _ruins _lostLand _echoesGrown _populations _behaviors _outbreaks _plagueRevealTold _army _battlesFought _threatTimers _suffering") },
        { typeof(EventSystemLogic), Words("eventScores seventhChangeCount seventhsSinceLastEvent eventCooldowns completionCounts cumulativeConsequences activeTimed timedEffectCounter _ballads _summons") },
        { typeof(CelestialWeatherSystemLogic), Words("activeWeatherProfile previousWeatherProfile timedWeatherProfile timedWeatherRemainingSevenths weatherBeforeTimed isHardSetWeather isDecayingWeather consecutiveRetentions weatherCooldowns weatherSelectionLockRemainingSevenths pendingForcedChangeDueToInvalidation pendingTimedRevert seventhsSinceLastOccurrence enableProceduralWeather regionalWeather nextFrontId regionalWeatherStep") },
        { typeof(CultureSystem), Words("_state") },
        { typeof(PopulationHealth), Words("_state") },
        { typeof(SpeciesLoreKeeper), Words("_state") },
        { typeof(EdictSystem), Words("_state") },
        { typeof(RumourKeeper), Words("_state") },
    };
    // Systems added after saves already existed: a save made before them loads, and they keep their fresh state.
    private static readonly HashSet<Type> LaterSystems = new HashSet<Type> { typeof(CultureSystem), typeof(PopulationHealth), typeof(SpeciesLoreKeeper), typeof(EdictSystem), typeof(RumourKeeper) };
    private static readonly string[] TileFields = Words("coord feature featureAge revealed known explored foragedAge authorityId administrativeAuthority impassable lunehymn silver coherence dissonance magicFertility leylineInfluence improvement microKnownMask microSurveyMask harvestedAge microHeldMask");
    private static string[] Words(string text) => text.Split(' ');
    public static void ValidateSchema()
    {
        foreach (var entry in Schema) foreach (var field in entry.Value) SaveStateCodec.Field(entry.Key, field);
    }
    private static string Key(Component component) => component is IGameUnitSlot slot ? slot.gameUnit.name : "system";
    private static IEnumerable<Component> Instances(Type type) => UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include)
        .Cast<Component>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded);

    public static void Capture(SaveDocument save)
    {
        ValidateSchema();
        if (WorldSystem.Instance?.Map == null) throw new InvalidOperationException("World generation has not finished.");
        save.systems.Clear(); save.worldTiles.Clear(); save.ink.Clear(); save.storyLocks.Clear();
        foreach (var slot in Instances(typeof(GameTechnologySlot)).Cast<GameTechnologySlot>()) slot.EnsureInitializedForSave();
        foreach (var entry in Schema)
            foreach (var component in Instances(entry.Key))
                save.systems.Add(new SavedSystem { type = entry.Key.Name, key = Key(component), state = SaveStateCodec.Capture(component, entry.Value) });
        foreach (var tile in WorldSystem.Instance.Map.Tiles) save.worldTiles.Add(SaveStateCodec.Capture(tile, TileFields));
        EventVolumeManager.Instance?.CaptureSave(save);
        save.pendingStory = EventSystemLogic.Instance?.PendingSaveStory;
        save.researchPlan = GameUnitsLogic.Instance != null ? new List<string>(GameUnitsLogic.Instance.ResearchPlan) : new List<string>();
        // Only the made resources that have a slot to load (the culture makes the others again from its own state).
        save.runtimeUnits = RuntimeUnits.Records.Where(r => GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.GetResourceSlotFromName(r.name) != null).ToList();
        save.ageId = GameAge.Id; save.ageNumber = GameAge.Number;
        save.randomState = JsonUtility.ToJson(UnityEngine.Random.state);
        save.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        save.savedUtc = DateTime.UtcNow.ToString("O");
    }
    public static void PrepareSlots(SaveDocument save)
    {
        RuntimeUnits.Restore(save.runtimeUnits);
        foreach (var record in save.systems.Where(r => r.type == nameof(GameResourceSlot) || r.type == nameof(GameProductionSlot) || r.type == nameof(GameTechnologySlot)))
        {
            if (!GameCatalog.Units.TryGet(record.key, out var unit) && (record.type != nameof(GameResourceSlot) || !RuntimeUnits.TryGet(record.key, out unit)))
                throw new InvalidOperationException("Missing saved unit: " + record.key);
            var tabType = record.type == nameof(GameResourceSlot) ? TabBuilderLogic.TabType.Storage : record.type == nameof(GameProductionSlot) ? TabBuilderLogic.TabType.Production : TabBuilderLogic.TabType.Technology;
            var tab = UnityEngine.Object.FindObjectsByType<TabBuilderLogic>(FindObjectsInactive.Include).Single(t => t.tabType == tabType);
            var slotObject = tab.AddNewUnit(unit);
            slotObject.GetComponent<GameTechnologySlot>()?.EnsureInitializedForSave();
        }
    }
    public static void Restore(SaveDocument save)
    {
        ValidateSchema();
        var map = WorldSystem.Instance.Map;
        var saved = new HashSet<string>(save.systems.Select(s => s.type));
        var expected = Schema.Where(e => !LaterSystems.Contains(e.Key) || saved.Contains(e.Key.Name))
            .SelectMany(e => Instances(e.Key).Select(c => e.Key.Name + ":" + Key(c))).OrderBy(k => k).ToArray();
        var actual = save.systems.Select(s => s.type + ":" + s.key).OrderBy(k => k).ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Save system roster does not match the game scene.");
        if (map.Count != save.worldTiles.Count || map.seed != save.identity.seed) throw new InvalidOperationException("World reconstruction mismatch.");
        // No Begin/Unlock/Discover calls here: restoration must never grant rewards or replay effects.
        foreach (var record in save.systems)
        {
            var entry = Schema.Single(e => e.Key.Name == record.type);
            var target = Instances(entry.Key).Single(c => Key(c) == record.key);
            SaveStateCodec.Restore(target, record.state, entry.Value);
            if (target is PopGrowthLogic population)
                population.RestoreDemography(!record.state.children.Any(c => c.name == "_demographyInitialized"));
        }
        map.Magic.Apply(map, save.ageNumber);
        for (int i = 0; i < map.Count; i++)
        {
            var oldCoord = map[i].coord;
            SaveStateCodec.Restore(map[i], save.worldTiles[i], TileFields);
            if (map[i].coord != oldCoord) throw new InvalidOperationException("World tile coordinates changed.");
        }
        map.RebuildKnowledge();
        WorldSystem.Instance.AfterRestore();
        EventVolumeManager.Instance?.RestoreSave(save);
        EventSystemLogic.Instance?.RestorePendingSaveStory(save.pendingStory);
        GameAge.Set(save.ageId, save.ageNumber);
        StatManager.Instance?.Recalculate();
        GlobalProductionManager.Instance?.MarkDirty();
        PopGrowthLogic.Instance?.RefreshHUD();
        // The crowd follows the people by itself; after a load it is simply there, with no one arriving.
        GlobalCharacterManager.Instance?.Resync();
        if (CelestialWeatherSystemLogic.Instance != null)
            CelestialWeatherSystemLogic.Instance.GetVisualLogic()?.InitializeWithWeather(CelestialWeatherSystemLogic.Instance.ActiveWeatherProfile);
        CelestialWeatherSystemLogic.Instance?.RestoreRegionalWeather();
        TimeSystemLogic.Instance?.UpdateUI();
        foreach (var c in Instances(typeof(GameResourceSlot)).Cast<GameResourceSlot>()) c.RefreshProductionAmount();
        foreach (var c in Instances(typeof(GameProductionSlot)).Cast<GameProductionSlot>()) c.UpdateMaxAmount();
        foreach (var c in Instances(typeof(GameTechnologySlot)).Cast<GameTechnologySlot>()) c.RefreshAfterLoad();
        GameUnitsLogic.Instance?.RestoreResearchPlan(save.researchPlan);
        TechnologyTreeLogic.RefreshAll();
        UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(save.randomState);
        Library.Invalidate();
        GovernmentLogic.Instance?.OnCouncilCompositionChanged?.Invoke();
        GovernmentLogic.Instance?.OnCivicPoolChanged?.Invoke();
        CivicManager.Instance?.RefreshAfterLoad();
        // Last: the culture reconciles its derived effects and caches with what was restored (world, runtime units,
        // civics), idempotently and without granting anything.
        CultureSystem.Instance?.AfterRestore();
    }
}
