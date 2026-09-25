using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Population, housing, vagrants and deaths.
///
/// Growth: whenever stored Food reaches the (morale-adjusted) threshold, one threshold of Food is
/// consumed and one person arrives: a citizen if there is free housing, otherwise a vagrant (once
/// vagrants are unlocked). There is exactly one growth path, used by the per-frame check and by
/// immediate food changes (clicks, events, production ticks).
///
/// Housing = (base + flat) × (1 + %). Base comes from the scene, buildings and events; flat and
/// percent come from <see cref="HousingModifiers"/> (legends, civics, seats, weather).
/// </summary>
public class PopGrowthLogic : SingletonBehaviour<PopGrowthLogic>
{
    private const LogChannel Log = LogChannel.Population;
    public const string HousingKey = "housing";
    private const string FoodDemandSource = "Population Food Demand";
    private const string ResearchSource = "Citizen Research";

    public float foodThreshold = 12f, vagrantToPopulationRate = 1f, researchPerPopulation = 1f, starvationRecoveryRate = 0.05f;

    public float foodDemandBuffer = -2.5f, demandRateConstant = 0.03f, sustainabilityTier = 1f, demandModifier = 1.0f;

    [Header("Population Management")]
    public int housing, population, vagrants, freeHousing, deaths, vagrantDeaths, trueDeaths;

    // The resources playing ResourceRole.Food and ResourceRole.Research (set on the asset, not by name).
    private GameUnit _food, _research;

    public TMP_Text foodText, freeHousingText, populationText, vagrantsText, foodStateText;

    public bool allowVagrants, isFoodScarce;

    [Tooltip("Bounds on how far morale can scale the food threshold (factor = 1 - moraleDelta/100).")]
    [SerializeField] private float minMoraleThresholdFactor = 0.25f, maxMoraleThresholdFactor = 2f;

    /// <summary>Flat and percent housing contributions keyed by <see cref="HousingKey"/>.</summary>
    public ModifierLedger HousingModifiers { get; } = new ModifierLedger();

    private readonly Dictionary<string, int> _baseHousingBySource = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private int _sceneHousing;
    private float _foodDemand;

    protected override void OnSingletonAwake()
    {
        _sceneHousing = housing;
        HousingModifiers.Changed += _ => RecalculateHousing();
        _food = GameCatalog.ResourceFor(ResourceRole.Food);
        _research = GameCatalog.ResourceFor(ResourceRole.Research);
    }

    private void Start()
    {
        if (GameUnitsLogic.Instance == null || GameUnitsLogic.Instance.storageTab == null)
        {
            GameLog.Error("GameUnitsLogic or its storage tab is missing; population is disabled.", Log);
            enabled = false;
            return;
        }
        if (_food == null)
        {
            GameLog.Error("No resource has GameUnit.role = Food; population is disabled.", Log);
            enabled = false;
            return;
        }
        StartCoroutine(InitializePopulationResources());
        InvokeRepeating(nameof(ProcessPopulationChanges), 1f, 1f);
    }

    private IEnumerator InitializePopulationResources()
    {
        yield return null;
        GameUnitsLogic.Instance.storageTab.AddNewUnit(_food);
        if (_research != null) GameUnitsLogic.Instance.storageTab.AddNewUnit(_research);
        UpdateResearchGenerationRate();
    }

    private void Update()
    {
        if (IsPaused()) return;
        TryGrowPopulation(1);
        UpdateFoodDemand();
        RefreshHUD();
    }

    private static bool IsPaused() => EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive();

    // ===== FOOD & GROWTH =====

    /// <summary>Food needed for the next person, scaled by morale (positive morale makes growth cheaper).</summary>
    public float GetEffectiveFoodThreshold()
    {
        float moraleDelta = StatManager.Instance != null ? Mathf.Round(StatManager.Instance.GetMoraleDeltaPercent()) : 0f;
        float factor = Mathf.Clamp(1f - moraleDelta / 100f, minMoraleThresholdFactor, maxMoraleThresholdFactor);
        return Mathf.Max(1f, foodThreshold * factor);
    }

    private bool CanGrow() => housing - population > 0 || allowVagrants;

    /// <summary>
    /// Largest Food gain that can still turn into growth. Stops clicks and rewards from piling up Food
    /// that nobody can eat while housing is full and vagrants are not unlocked.
    /// </summary>
    public float GetMaxUsefulFoodGain(float currentFood)
    {
        if (allowVagrants) return float.MaxValue;
        float threshold = GetEffectiveFoodThreshold();
        int free = housing - population;
        if (free <= 0) return Mathf.Max(0f, threshold - currentFood);
        return Mathf.Max(0f, free * threshold);
    }

    /// <summary>React to Food changing outside the per-frame check (clicks, production ticks, events).</summary>
    public void HandleExternalFoodChange(float oldAmount, float newAmount)
    {
        if (IsPaused() || newAmount <= oldAmount) return;
        TryGrowPopulation(int.MaxValue);
    }

    /// <summary>Convert stored Food into people, at most <paramref name="maxSteps"/> times. Returns how many arrived.</summary>
    private int TryGrowPopulation(int maxSteps)
    {
        var foodSlot = GameUnitsLogic.Instance != null && _food != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(_food.name) : null;
        if (foodSlot == null) return 0;

        float threshold = GetEffectiveFoodThreshold();
        int grown = 0;
        while (grown < maxSteps && foodSlot.amount + 1e-3f >= threshold && CanGrow())
        {
            foodSlot.ChangeAmount(-threshold);
            if (housing - population > 0)
            {
                population++;
                SpawnVillagers(1);
            }
            else
            {
                vagrants++;
            }
            grown++;
        }
        if (grown > 0)
        {
            UpdateResearchGenerationRate();
            GameLog.Event($"{grown} arrival(s) for {threshold:F1} Food each: population {population}, vagrants {vagrants}", Log);
            RefreshHUD();
        }
        return grown;
    }

    public void ChangeFoodMaxThreshold(float newFoodThreshold) => foodThreshold = Mathf.Max(1f, newFoodThreshold);

    /// <summary>Technologies that make the population eat less (fraction, 0.1 = 10% less).</summary>
    public void ReduceFoodDemand(float fraction) => demandModifier = Mathf.Max(0f, demandModifier - fraction);

    private void UpdateFoodDemand()
    {
        _foodDemand = (foodDemandBuffer + Mathf.Exp(demandRateConstant / Mathf.Max(0.0001f, sustainabilityTier) * population)) * demandModifier;
        GlobalProductionManager.Instance?.SetFlatRate(_food.name, FoodDemandSource, _foodDemand > 0f ? -_foodDemand : 0f);
    }

    private void UpdateResearchGenerationRate()
    {
        if (_research == null) return;
        GlobalProductionManager.Instance?.SetFlatRate(_research.name, ResearchSource, population * researchPerPopulation);
    }

    /// <summary>Once per second: starvation deaths and vagrants moving into free housing.</summary>
    private void ProcessPopulationChanges()
    {
        if (IsPaused() || GlobalProductionManager.Instance == null) return;

        isFoodScarce = GlobalProductionManager.Instance.GetNetProductionRate(_food.name) < 0f && GetResourceAmount(_food.name) <= 0f;
        if (isFoodScarce && population > 0)
        {
            population--;
            deaths++;
            trueDeaths++;
            RemoveVillagers(1);
            UpdateResearchGenerationRate();
            GameUnitsLogic.Instance.ChangeResourceFromName(_food.name, foodThreshold * starvationRecoveryRate, false);
            GameLog.Event($"A citizen starved. Total deaths: {deaths}", Log);
        }

        freeHousing = Mathf.Max(0, housing - population);
        if (vagrants > 0 && freeHousing > 0)
        {
            int moving = Mathf.Min((int)vagrantToPopulationRate, Mathf.Min(vagrants, freeHousing));
            if (moving > 0)
            {
                vagrants -= moving;
                population += moving;
                SpawnVillagers(moving);
                UpdateResearchGenerationRate();
                GameLog.Event($"{moving} vagrant(s) moved into housing.", Log);
            }
        }
    }

    private static float GetResourceAmount(string resourceName) => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceAmountExact(resourceName) : 0f;

    // ===== HOUSING =====

    public int GetHousing() => housing;

    public int GetBaseHousing() => _sceneHousing + SumBaseHousing();

    public int GetTotalHousingBonus() => housing - GetBaseHousing();

    /// <summary>Per-source housing modifiers (legends, civics...) for tooltips.</summary>
    public IReadOnlyDictionary<string, ModifierValue> GetHousingBonusSources() => HousingModifiers.Sources(HousingKey);

    /// <summary>Permanent housing from buildings and other owned sources; accumulates per source.</summary>
    public void AddBaseHousing(int amount, string source)
    {
        if (amount == 0) return;
        _baseHousingBySource.TryGetValue(source ?? "Other", out int current);
        _baseHousingBySource[source ?? "Other"] = current + amount;
        RecalculateHousing();
    }

    /// <summary>Housing change from events. Losing housing turns the homeless into vagrants.</summary>
    public void ModifyHousing(int change) => AddBaseHousing(change, "Events");

    private int SumBaseHousing()
    {
        int total = 0;
        foreach (var value in _baseHousingBySource.Values) total += value;
        return total;
    }

    private void RecalculateHousing()
    {
        int oldHousing = housing;
        housing = Mathf.Max(0, Mathf.RoundToInt(HousingModifiers.Total(HousingKey).ApplyTo(GetBaseHousing())));
        if (housing < population)
        {
            int homeless = population - housing;
            population -= homeless;
            vagrants += homeless;
            RemoveVillagers(homeless);
            UpdateResearchGenerationRate();
            GameLog.Event($"Housing fell to {housing}: {homeless} citizen(s) became vagrants.", Log);
        }
        freeHousing = Mathf.Max(0, housing - population);
        if (oldHousing != housing) GameLog.Event($"Housing {oldHousing} → {housing}", Log);
    }

    // ===== EVENT HOOKS =====

    /// <summary>Remove citizens (events). Only negative values are accepted; each removal is a death.</summary>
    public void ModifyPopulation(int change)
    {
        if (change >= 0)
        {
            GameLog.Warning($"ModifyPopulation({change}) ignored: population can only be removed. Use ModifyVagrants to add people.", Log);
            return;
        }
        ProcessEventDeaths(-change);
    }

    /// <summary>Add or remove vagrants. Removed vagrants count as vagrant deaths.</summary>
    public void ModifyVagrants(int change)
    {
        if (change == 0) return;
        if (change < 0)
        {
            int removed = Mathf.Min(-change, vagrants);
            vagrants -= removed;
            vagrantDeaths += removed;
        }
        else
        {
            vagrants += change;
        }
        RefreshHUD();
    }

    /// <summary>Citizens die in an event: recorded in both public and true death counts.</summary>
    public void ProcessEventDeaths(int deathCount)
    {
        if (deathCount <= 0) return;
        int died = Mathf.Min(deathCount, population);
        population -= died;
        deaths += died;
        trueDeaths += died;
        RemoveVillagers(died);
        UpdateResearchGenerationRate();
        GameLog.Event($"{died} citizen(s) died in an event. Population {population}, deaths {deaths}", Log);
        RefreshHUD();
    }

    /// <summary>
    /// Rewrite public death records. Adding deaths also adds to the true count; removing them hides deaths
    /// from history while <see cref="trueDeaths"/> keeps the real number.
    /// </summary>
    public void ReviseDeathRecords(int change)
    {
        if (change == 0) return;
        int oldDeaths = deaths;
        deaths = Mathf.Max(0, deaths + change);
        int actual = deaths - oldDeaths;
        if (actual > 0) trueDeaths += actual;
        RefreshHUD();
    }

    private static void SpawnVillagers(int count)
    {
        var characters = GlobalCharacterManager.Instance;
        if (characters != null) characters.Spawn("Villager", count);
    }

    private static void RemoveVillagers(int count)
    {
        var characters = GlobalCharacterManager.Instance;
        if (characters != null) characters.RemoveRandom(count);
    }

    // ===== HUD =====

    public void RefreshHUD()
    {
        var units = GameUnitsLogic.Instance;
        var production = GlobalProductionManager.Instance;
        if (units == null || production == null || foodText == null || _food == null) return;

        freeHousing = Mathf.Max(0, housing - population);
        foodText.text = units.FormatValue(GetResourceAmount(_food.name));
        freeHousingText.text = units.FormatValue(freeHousing);
        populationText.text = units.FormatValue(population);
        vagrantsText.text = units.FormatValue(vagrants);

        float netFoodRate = production.GetNetProductionRate(_food.name);
        float ratio = _foodDemand > 0f ? Mathf.Abs(netFoodRate / _foodDemand) * 100f : 0f;
        if (Mathf.Approximately(netFoodRate, 0f)) foodStateText.text = "Stagnant";
        else if (netFoodRate > 0f) foodStateText.text = ratio > 200f ? "Thriving" : ratio > 150f ? "Blooming" : "Ripening";
        else foodStateText.text = ratio > 50f ? "Famine" : ratio > 30f ? "Withering" : ratio > 10f ? "Dwindling" : "Stagnant";
    }

    [ContextMenu("Print Population Info")]
    public void PrintPopulationInfo()
    {
        if (!GameLog.IsEnabled(Log)) return;
        GameLog.Event($"Population {population}, vagrants {vagrants}, housing {housing} (base {GetBaseHousing()}), free {freeHousing}", Log);
        GameLog.Event($"Food threshold {foodThreshold} (effective {GetEffectiveFoodThreshold():F1}), demand {_foodDemand:F2}/s, vagrants allowed {allowVagrants}", Log);
        foreach (var pair in _baseHousingBySource) GameLog.Event($"  base housing {pair.Value:+#;-#;0} from {pair.Key}", Log);
        foreach (var pair in HousingModifiers.Sources(HousingKey)) GameLog.Event($"  housing {pair.Value.Flat:+0.##;-0.##;0} / {pair.Value.Percent:+0.##;-0.##;0}% from {pair.Key}", Log);
        GameLog.Event($"Deaths {deaths} (true {trueDeaths}), vagrant deaths {vagrantDeaths}", Log);
    }
}
