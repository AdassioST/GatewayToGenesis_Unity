using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Population, housing, vagrants and deaths.
///
/// Births and deaths follow the calendar. Food provisions admit a finite founding group;
/// other migration comes from explicit world/story transfers. Food never creates people.
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

    // Kept only so existing saves retain their schema. Food no longer turns into births.
    [HideInInspector] public float foodThreshold = 12f, starvationRecoveryRate = 0.05f;
    public float vagrantToPopulationRate = 1f, researchPerPopulation = 1f;

    [HideInInspector] public float foodDemandBuffer = -2.5f, demandRateConstant = 0.03f, sustainabilityTier = 1f;
    public float demandModifier = 1f;

    [Header("Population Management")]
    public int housing, population, vagrants, freeHousing, deaths, vagrantDeaths, trueDeaths;

    // The resources playing ResourceRole.Food and ResourceRole.Research (set on the asset, not by name).
    private GameUnit _food, _research;

    public TMP_Text foodText, freeHousingText, populationText, vagrantsText, foodStateText;

    public bool allowVagrants, isFoodScarce;


    /// <summary>Flat and percent housing contributions keyed by <see cref="HousingKey"/>.</summary>
    public ModifierLedger HousingModifiers { get; } = new ModifierLedger();

    private readonly Dictionary<string, int> _baseHousingBySource = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private int _sceneHousing;
    private float _foodDemand;
    private GrowthSettings _growthSettings;
    private readonly GrowthTuning _growthDefaults = new GrowthTuning();
    private TimeSystemLogic _calendar;
    [SaveOptionalField] private double _birthCarry, _naturalDeathCarry, _famineDeathCarry;
    [SaveOptionalField] private float _hungerDays, _arrivalElapsed;
    // Survivors waiting at the gates for rations: the founding members, then bands found by expeditions and caravans.
    // (The name predates the other sources; kept for saves.)
    [SaveOptionalField] private int _foundingMigrantsRemaining;
    [SaveOptionalField] private bool _demographyInitialized;
    // Children born since the founding (arrivals are migrants, not births).
    [SaveOptionalField] private int _births;
    // Bands found by expeditions, still walking to the Capital; caravans due (fraction) and arrived.
    [SaveOptionalField] private List<SurvivorBand> _bandsOnTheRoad = new List<SurvivorBand>();
    [SaveOptionalField] private double _caravanProgress;
    [SaveOptionalField] private int _caravans;
    // Who waits at the gates, oldest first, by what is known of their origin (null: unknown); admissions counted for their keys.
    // People waiting from before these lots existed are admitted with an unknown origin (never guessed).
    [SaveOptionalField] private List<ArrivalLot> _gateLots = new List<ArrivalLot>();
    [SaveOptionalField] private int _admissions;
    public int Births => _births;
    public int Caravans => _caravans;
    public IReadOnlyList<SurvivorBand> BandsOnTheRoad => _bandsOnTheRoad;
    public int PeopleOnTheRoad { get { int n = 0; foreach (var b in _bandsOnTheRoad) n += b.people; return n; } }

    /// <summary>A band of survivors on its way to the Capital (<see cref="SurvivorsFound"/>).</summary>
    [System.Serializable]
    public class SurvivorBand
    {
        public int people, sevenths;
        public string source;
    }

    /// <summary>People waiting at the gates who came the same way: their origin when known ("survivors:431"; null: unknown) and how they came.</summary>
    [System.Serializable]
    public class ArrivalLot
    {
        public int people;
        public string source;
        public string channel;
    }
    public GrowthTuning Growth => _growthSettings != null ? _growthSettings.tuning : _growthDefaults;
    /// <summary>The roofless are let in: the technology allows it (allowVagrants) and the Edicts do not turn them away (Homes First).</summary>
    public bool VagrantsLetIn => allowVagrants && !EdictSystem.Levers.turnAwayRoofless;

    public int TotalPeople => (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, population) + Mathf.Max(0, vagrants));
    public int WaitingMigrants => _foundingMigrantsRemaining;
    /// <summary>Founding members still at the gates: each is let in as soon as their rations are gathered, and eats for free ever after.</summary>
    public int FoundersWaiting { get { int n = 0; foreach (var l in _gateLots) if (l.channel == FoundingChannel) n += System.Math.Max(0, l.people); return System.Math.Min(n, _foundingMigrantsRemaining); } }
    /// <summary>The founding is over: every founding member was let in (or this world never had them, an old save).</summary>
    public bool FoundingDone => _demographyInitialized && FoundersWaiting == 0;
    /// <summary>People fed by the daily food: those beyond the provisioned founders (<see cref="GrowthRules.Eating"/>).</summary>
    public int EatingPeople => GrowthRules.Eating(TotalPeople, Growth);
    private const string FoundingChannel = "founding";
    public float DailyRations => GrowthRules.RationsPerDay(Growth, demandModifier);
    private float SecondsPerSeventh => TimeSystemLogic.Instance != null ? Mathf.Max(0.01f, TimeSystemLogic.Instance.GetEffectiveSecondsPerSeventh()) : 180f;
    private bool CalendarRunning => TimeSystemLogic.Instance != null && TimeSystemLogic.Instance.canTrackTime && !TimeSystemLogic.Instance.isTimePaused;
    public int SupportedPeople
    {
        get
        {
            if (_food == null || GlobalProductionManager.Instance == null) return 0;
            float supply = GlobalProductionManager.Instance.GetNetProductionRate(_food.name) + _foodDemand
                - (Pantry.Instance != null ? Pantry.Instance.CoverRate : 0f);
            return GrowthRules.SupportedPeople(supply, SecondsPerSeventh, Growth, demandModifier);
        }
    }

    protected override void OnSingletonAwake()
    {
        _growthSettings = Resources.Load<GrowthSettings>("Population/Growth");
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
        TimeSystemLogic.WhenReady(this, time => { _calendar = time; time.OnSeventhChange += OnDemographicSeventh; });
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
        if (SaveSession.Restoring || SaveMenu.BlocksGameplay) return;
        if (IsPaused()) return;
        InitializeDemography();
        // The founders come in the moment their twelve are gathered. Later survivors are paced: the timer only restarts
        // when someone is let in, so after a wait the click that fills the ration lets the next one in at once.
        _arrivalElapsed = Mathf.Min(_arrivalElapsed + Time.deltaTime, 3600f);
        if (FoundersWaiting > 0) AdmitFoundingMigrants(FoundersWaiting);
        else if (_arrivalElapsed >= Mathf.Max(0.1f, Growth.arrivalIntervalSeconds) && AdmitFoundingMigrants(1) > 0) _arrivalElapsed = 0f;
        UpdateFoodDemand();
        RefreshHUD();
    }

    private static bool IsPaused() => EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive();

    // ===== FOOD & GROWTH =====

    /// <summary>Rations issued to an arriving survivor, not the cost of a birth.</summary>
    public float GetArrivalRations()
    {
        return Mathf.Max(1f, Growth.arrivalRations * Mathf.Max(0f, EdictSystem.Levers.arrivalRations));
    }

    /// <summary>Food can fill its stores even when housing is full.</summary>
    public float GetMaxUsefulFoodGain(float currentFood)
    {
        // Storage capacity, not housing, limits reserves. A full village still needs winter food.
        return float.MaxValue;
    }

    /// <summary>React to Food changing outside the per-frame check (clicks, production ticks, events).</summary>
    public void HandleExternalFoodChange(float oldAmount, float newAmount)
    {
        // While the founders wait, every twelve gathered lets one in at once (a click, a delivery). Other migration is
        // paced independently of clicks and frame rate.
        if (newAmount <= oldAmount || IsPaused() || SaveSession.Restoring || !_demographyInitialized) return;
        int founders = FoundersWaiting;
        if (founders > 0) AdmitFoundingMigrants(founders);
    }

    /// <summary>
    /// What the Food counter's bar measures: the next founder's rations while the founders wait (0 to 12), otherwise
    /// <paramref name="fallback"/> (its storage, or the Food kept in hand once surplus goes to the stores).
    /// </summary>
    public float FoodBarMaximum(float fallback) => FoundersWaiting > 0 ? GetArrivalRations() : fallback;

    /// <summary>Let in survivors waiting at the gates, each for its rations, while homes (or vagrancy) allow.</summary>
    private int AdmitFoundingMigrants(int maxSteps)
    {
        var foodSlot = GameUnitsLogic.Instance != null && _food != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(_food.name) : null;
        if (foodSlot == null) return 0;

        float threshold = GetArrivalRations();
        int grown = GrowthRules.Arrivals(_foundingMigrantsRemaining, housing - population, VagrantsLetIn, foodSlot.amount, threshold, maxSteps);
        if (grown > 0)
        {
            _foundingMigrantsRemaining -= grown;
            foodSlot.ChangeAmount(-grown * threshold);
            AddPeople(grown);
            ReportAdmissions(grown);
            UpdateResearchGenerationRate();
            GameLog.Event($"{grown} arrival(s) for {threshold:F1} Food each: population {population}, vagrants {vagrants}", Log);
            RefreshHUD();
        }
        return grown;
    }

    private void InitializeDemography()
    {
        if (_demographyInitialized) return;
        // Old populated saves must not receive the founding group twice.
        _foundingMigrantsRemaining = TotalPeople == 0 && trueDeaths == 0 && vagrantDeaths == 0 ? Mathf.Max(0, Growth.foundingMigrants) : 0;
        _gateLots.Clear();
        if (_foundingMigrantsRemaining > 0) _gateLots.Add(new ArrivalLot { people = _foundingMigrantsRemaining, channel = FoundingChannel });
        _demographyInitialized = true;
    }

    public void RestoreDemography(bool legacySave)
    {
        if (legacySave)
        {
            // Old saves have no source population for arrivals. Never inject survivors on load.
            _birthCarry = _naturalDeathCarry = _famineDeathCarry = 0d;
            _caravanProgress = 0d;
            _bandsOnTheRoad.Clear();
            _hungerDays = _arrivalElapsed = 0f;
            _foundingMigrantsRemaining = 0;
            _gateLots.Clear();
            _demographyInitialized = true;
        }
        UpdateFoodDemand();
        UpdateResearchGenerationRate();
    }

    private void AddPeople(int count)
    {
        count = Mathf.Min(Mathf.Max(0, count), int.MaxValue - TotalPeople);
        int housed = Mathf.Min(count, Mathf.Max(0, housing - population));
        population += housed;
        vagrants += count - housed;
        UpdateResearchGenerationRate();
        RefreshHUD();
    }

    private void OnDemographicSeventh(int seventh)
    {
        if (SaveSession.Restoring || SaveMenu.BlocksGameplay || IsPaused()) return;
        AdvanceBands();
        AdvanceCaravans();
        int people = TotalPeople;
        double years = 1d / GrowthRules.SeventhsPerYear;
        // Frontier pacing (a gameplay rule): a founding band's births come in minutes, fading to the historical rate by
        // the time it is a village. Deaths always follow the historical rate.
        int born = GrowthRules.Whole(GrowthRules.VitalEvents(people, Growth.birthsPerThousand, years) * GrowthRules.BirthPace(people, Growth) * Mathf.Max(0f, EdictSystem.Levers.births)
            * (isFoodScarce ? 0f : 1f) / Mathf.Max(1f, PopulationHealth.ThresholdFactor), ref _birthCarry);
        int died = GrowthRules.Whole(GrowthRules.VitalEvents(people, Growth.deathsPerThousand, years), ref _naturalDeathCarry);
        RemovePeople(died, "natural causes");
        // Children without a roof still exist, including before the vagrant UI is unlocked.
        AddPeople(born);
        if (born <= 0) return;
        bool first = _births == 0;
        _births = (int)System.Math.Min(int.MaxValue, (long)_births + born);
        GameLog.Event($"{born} birth(s): {_births} born since the founding.", Log);
        // The first child is its own story (StarterVolume first_birth: +1 Era Score, morale): check for it now.
        if (first) EventSystemLogic.Instance?.TriggerEventCheck();
        // Reported with every birth, not only the first: the award is idempotent and a failed profile write retries.
        Achievements.Report(AchievementEvent.Of(AchievementSignal.PeopleBorn, _births).From($"births:{_births}"));
    }

    // ===== SURVIVORS: THE GATES, BANDS ON THE ROAD, CARAVANS =====

    /// <summary>
    /// A band of <paramref name="people"/> survivors found out in the world (an expedition's find): they walk for
    /// <paramref name="sevenths"/> Sevenths, then wait at the gates for rations like everyone else (0: at the gates now).
    /// </summary>
    public void SurvivorsFound(int people, int sevenths, string source)
    {
        if (people <= 0) return;
        if (sevenths <= 0) { AtTheGates(people, source, "survivors"); return; }
        _bandsOnTheRoad.Add(new SurvivorBand { people = people, sevenths = sevenths, source = source });
        RefreshHUD();
    }

    /// <summary>People who join at once, housed if there is room (returning settlers, story arrivals): migration, not births.</summary>
    public void AddMigrants(int count) => AddMigrants(count, null, "story", -1);

    /// <summary>
    /// People who join <paramref name="settlement"/> at once (-1: not said), from <paramref name="origin"/> when known
    /// ("settlement:3"; null: unknown): the admission is reported with exactly that origin (<see cref="CultureSystem.Admitted"/>).
    /// </summary>
    public void AddMigrants(int count, string origin, string channel, int settlement)
    {
        int before = TotalPeople;
        AddPeople(count);
        int admitted = TotalPeople - before;
        if (admitted > 0) CultureSystem.Admitted(new ArrivalAdmission { key = "admission:" + (++_admissions), people = admitted, origin = origin, channel = channel, settlement = settlement });
    }

    // Survivors join the gates: the pool grows, and a lot remembers where they came from (null: unknown).
    private void AtTheGates(int people, string source, string channel)
    {
        int was = _foundingMigrantsRemaining;
        _foundingMigrantsRemaining = (int)System.Math.Min(int.MaxValue, (long)_foundingMigrantsRemaining + System.Math.Max(0, people));
        int added = _foundingMigrantsRemaining - was;
        if (added <= 0) return;
        var last = _gateLots.Count > 0 ? _gateLots[_gateLots.Count - 1] : null;
        if (last != null && last.source == source && last.channel == channel) last.people += added;
        else _gateLots.Add(new ArrivalLot { people = added, source = source, channel = channel });
    }

    // Survivors let in at the gates, oldest first: each lot's origin is reported as known; people waiting from before
    // the lots existed (an older save) are reported as of unknown origin. The lots never hold more than the waiting pool.
    private void ReportAdmissions(int admitted)
    {
        while (admitted > 0)
        {
            int unrecorded = System.Math.Max(0, _foundingMigrantsRemaining + admitted - LotPeople());
            var lot = _gateLots.Count > 0 ? _gateLots[0] : null;
            int take;
            ArrivalLot from = null;
            if (unrecorded > 0 || lot == null) take = System.Math.Min(admitted, System.Math.Max(1, unrecorded));
            else
            {
                take = System.Math.Min(admitted, lot.people);
                from = lot;
                lot.people -= take;
                if (lot.people <= 0) _gateLots.RemoveAt(0);
            }
            admitted -= take;
            CultureSystem.Admitted(new ArrivalAdmission { key = "admission:" + (++_admissions), people = take, origin = from?.source, channel = from?.channel ?? "arrivals", atGates = true });
        }
        // Lots beyond the waiting pool (it was reset) are dropped, oldest first.
        while (_gateLots.Count > 0 && LotPeople() > _foundingMigrantsRemaining)
        {
            int extra = LotPeople() - _foundingMigrantsRemaining;
            if (_gateLots[0].people <= extra) _gateLots.RemoveAt(0);
            else _gateLots[0].people -= extra;
        }
    }

    private int LotPeople()
    {
        long n = 0;
        foreach (var l in _gateLots) n += System.Math.Max(0, l.people);
        return (int)System.Math.Min(int.MaxValue, n);
    }

    private void AdvanceBands()
    {
        int arrived = 0;
        for (int i = _bandsOnTheRoad.Count - 1; i >= 0; i--)
        {
            var band = _bandsOnTheRoad[i];
            if (--band.sevenths > 0) continue;
            _bandsOnTheRoad.RemoveAt(i);
            arrived += band.people;
            AtTheGates(band.people, band.source, "survivors");
        }
        if (arrived <= 0) return;
        NotificationFeed.Push("Survivors at the gates", $"{arrived} survivors found by your expeditions reach the Capital. Each is let in for {GetArrivalRations():0} Food.", NotificationFeed.Topic.World);
    }

    // While the settlement is still a frontier band, its stores draw caravans of survivors (the more days of rations
    // stored, the sooner). Caravans fade out with frontier pacing: a village grows by its own births and the people
    // its expeditions bring home.
    private void AdvanceCaravans()
    {
        float pull = GrowthRules.CaravanPull(TotalPeople, _food != null ? GetResourceAmount(_food.name) : 0f, DailyRations, Growth);
        _caravanProgress += GrowthRules.CaravanProgress(pull, Growth) * Mathf.Max(0f, EdictSystem.Levers.caravans);
        if (_caravanProgress < 1d) return;
        _caravanProgress -= 1d;
        int size = GrowthRules.CaravanSize(_caravans++, Growth);
        // Drawn by word of the stores: where they came from is not known, and is not guessed.
        AtTheGates(size, null, "caravan");
        GameLog.Event($"Caravan {_caravans}: {size} survivors at the gates (pull {pull:P0}).", Log);
        NotificationFeed.Push("A caravan arrives", $"{size} survivors, drawn by word of your stores, wait at the gates. Each is let in for {GetArrivalRations():0} Food.", NotificationFeed.Topic.World);
    }

    private void RemovePeople(int count, string cause)
    {
        int total = TotalPeople;
        count = Mathf.Min(Mathf.Max(0, count), total);
        if (count == 0) return;
        int roofless = Mathf.Min(vagrants, (int)System.Math.Round(count * (double)vagrants / Mathf.Max(1, total)));
        int housed = Mathf.Min(population, count - roofless);
        roofless = Mathf.Min(vagrants, count - housed);
        if (roofless > 0) ModifyVagrants(-roofless);
        if (housed > 0) ProcessEventDeaths(housed, cause);
    }

    protected override void OnSingletonDestroy()
    {
        if (_calendar != null) _calendar.OnSeventhChange -= OnDemographicSeventh;
    }


    /// <summary>Technologies reduce distribution losses, never basic nutritional requirements.</summary>
    public void ReduceFoodDemand(float fraction) => demandModifier = Mathf.Max(0f, demandModifier - fraction);

    private void UpdateFoodDemand()
    {
        if (_food == null) return;
        // The provisioned founders eat nothing from the daily food: only the people beyond them do.
        _foodDemand = CalendarRunning ? GrowthRules.FoodDemand(EatingPeople, SecondsPerSeventh, Growth, demandModifier) : 0f;
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
        if (SaveSession.Restoring || SaveMenu.BlocksGameplay || IsPaused() || GlobalProductionManager.Instance == null || _food == null) return;
        UpdateFoodDemand();
        UpdateResearchGenerationRate();
        float net = GlobalProductionManager.Instance.GetNetProductionRate(_food.name);
        isFoodScarce = PopulationRules.IsStarving(net, GetResourceAmount(_food.name));
        if (CalendarRunning)
        {
            float days = Growth.daysPerYear / (GrowthRules.SeventhsPerYear * SecondsPerSeventh);
            if (isFoodScarce && TotalPeople > 0)
            {
                float before = _hungerDays;
                _hungerDays += days;
                float exposed = Mathf.Max(0f, _hungerDays - Growth.famineGraceDays) - Mathf.Max(0f, before - Growth.famineGraceDays);
                float deficit = _foodDemand > 0f ? Mathf.Clamp01(-net / _foodDemand) : 0f;
                int died = GrowthRules.Whole(EatingPeople * (double)Growth.famineDeathsPerTenThousandDay / 10000d * exposed * deficit, ref _famineDeathCarry);
                RemovePeople(died, "hunger");
            }
            else _hungerDays = Mathf.Max(0f, _hungerDays - days);
        }

        freeHousing = Mathf.Max(0, housing - population);
        if (vagrants > 0 && freeHousing > 0)
        {
            int moving = PopulationRules.VagrantsMovingIn(vagrants, freeHousing, Mathf.CeilToInt(Mathf.Max((int)vagrantToPopulationRate, Mathf.CeilToInt(vagrants * 0.05f)) * Mathf.Max(1f, EdictSystem.Levers.vagrantsHoused)));
            if (moving > 0)
            {
                vagrants -= moving;
                population += moving;
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

    /// <summary>Base housing one source gives now.</summary>
    public int GetBaseHousing(string source) => _baseHousingBySource.TryGetValue(source ?? "Other", out int current) ? current : 0;

    /// <summary>Set the base housing one source gives (a source that recomputes its total, like the outskirt tributaries).</summary>
    public void SetBaseHousing(int amount, string source) => AddBaseHousing(amount - GetBaseHousing(source), source);

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
        housing = PopulationRules.Housing(GetBaseHousing(), HousingModifiers.Total(HousingKey));
        if (housing < population)
        {
            int homeless = population - housing;
            population -= homeless;
            vagrants += homeless;
            UpdateResearchGenerationRate();
            GameLog.Event($"Housing fell to {housing}: {homeless} citizen(s) became vagrants.", Log);
        }
        freeHousing = Mathf.Max(0, housing - population);
        if (oldHousing != housing) GameLog.Event($"Housing {oldHousing} → {housing}", Log);
    }

    // ===== EVENT HOOKS =====

    public void LosePopulationPercent(float percent, string cause) => RemovePeople(GrowthRules.PercentOf(TotalPeople, percent), cause);


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
            trueDeaths += removed;
        }
        else
        {
            vagrants += Mathf.Min(change, int.MaxValue - TotalPeople);
        }
        RefreshHUD();
    }

    /// <summary>Citizens die in an event (or of <paramref name="cause"/>, such as a health pressure): recorded in both public and true death counts.</summary>
    /// <summary>
    /// People who leave to serve in a conscripted company (<see cref="ArmyRoster"/>): out of the population, not dead.
    /// The housed go first. Returns how many went.
    /// </summary>
    public int Enlist(int count, string company = null)
    {
        count = Mathf.Min(Mathf.Max(0, count), TotalPeople);
        if (count == 0) return 0;
        int housed = Mathf.Min(population, count);
        population -= housed;
        vagrants = Mathf.Max(0, vagrants - (count - housed));
        UpdateResearchGenerationRate();
        GameLog.Event($"{count} citizen(s) enlisted{(string.IsNullOrEmpty(company) ? string.Empty : " in " + company)}. Population {population}", Log);
        RefreshHUD();
        return count;
    }

    /// <summary>Soldiers who come home when their company is disbanded (<see cref="ArmyRoster.Disband"/>).</summary>
    public void Discharge(int count, string company = null)
    {
        if (count <= 0) return;
        AddPeople(count);
        GameLog.Event($"{count} soldier(s) came home{(string.IsNullOrEmpty(company) ? string.Empty : " from " + company)}. Population {population}", Log);
    }

    public void ProcessEventDeaths(int deathCount, string cause = null)
    {
        if (deathCount <= 0) return;
        int died = Mathf.Min(deathCount, population);
        population -= died;
        deaths += died;
        trueDeaths += died;
        UpdateResearchGenerationRate();
        GameLog.Event($"{died} citizen(s) died {(string.IsNullOrEmpty(cause) ? "in an event" : "of " + cause)}. Population {population}, deaths {deaths}", Log);
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
        RefreshPopulationTooltip();
    }

    // The population counter's tooltip (the scene's text) gains a line per active health pressure, and loses it when
    // the pressure falls dormant. Only rewritten when those lines change.
    private TooltipTrigger _populationTooltip;
    private string _populationTooltipText, _populationTooltipHealth;
    private bool _populationTooltipLooked;
    private float _populationTooltipAt;

    private void RefreshPopulationTooltip()
    {
        if (Time.unscaledTime < _populationTooltipAt) return;
        _populationTooltipAt = Time.unscaledTime + 0.5f;
        if (!_populationTooltipLooked)
        {
            _populationTooltipLooked = true;
            var near = populationText != null ? populationText.GetComponentInParent<TooltipTrigger>(true) : null;
            _populationTooltip = near != null && near.customTitle == "Population" ? near
                : System.Array.Find(FindObjectsByType<TooltipTrigger>(FindObjectsInactive.Include), t => t.useCustomTooltip && t.customTitle == "Population");
            if (_populationTooltip != null) _populationTooltipText = _populationTooltip.customDescription;
        }
        if (_populationTooltip == null) return;
        var rows = PopulationHealth.Instance != null ? PopulationHealth.Instance.TooltipRows() : new List<string>();
        rows.Insert(0, $"{TotalPeople:N0} people, including {vagrants:N0} without a home.");
        rows.Insert(1, $"Daily food: {EatingPeople * DailyRations:N0} rations (the first {Growth.provisionedPeople:N0}, the founders, were provisioned for good at the gates). Current supply supports {SupportedPeople:N0} people.");
        float pace = GrowthRules.BirthPace(TotalPeople, Growth);
        rows.Insert(2, $"Births / natural deaths per Cycle: {TotalPeople * Growth.birthsPerThousand * pace / 1000f:0.#} / {TotalPeople * Growth.deathsPerThousand / 1000f:0.#} before health pressures.");
        int at = 3;
        if (pace > 1.01f) rows.Insert(at++, $"Frontier: births come {pace:0}× faster than history's while you are fewer than {Growth.realismFrom:N0}; caravans are drawn by your stores.");
        if (FoundersWaiting > 0) rows.Insert(at++, $"{FoundersWaiting:N0} founders wait at the gates: each comes in, for good, the moment {GetArrivalRations():0} Food is gathered.");
        if (WaitingMigrants > FoundersWaiting) rows.Insert(at++, $"{WaitingMigrants - FoundersWaiting:N0} survivors wait at the gates for {GetArrivalRations():0} Food each.");
        if (_bandsOnTheRoad.Count > 0) rows.Insert(at++, $"{PeopleOnTheRoad:N0} survivors found by expeditions are on the road here.");
        string health = string.Join("\n", rows);
        if (health == (_populationTooltipHealth ?? string.Empty)) return;
        _populationTooltipHealth = health;
        string text = rows.Count == 0 ? _populationTooltipText : $"{_populationTooltipText}\n\n{TooltipText.Muted("The people")}\n{health}";
        _populationTooltip.SetCustom(_populationTooltip.customTitle, text, _populationTooltip.customType);
    }

    [ContextMenu("Print Population Info")]
    public void PrintPopulationInfo()
    {
        if (!GameLog.IsEnabled(Log)) return;
        GameLog.Event($"Population {population}, vagrants {vagrants}, housing {housing} (base {GetBaseHousing()}), free {freeHousing}", Log);
        GameLog.Event($"Arrival provisions {GetArrivalRations():F1} Food, demand {_foodDemand:F2}/s, vagrants allowed {allowVagrants}", Log);
        foreach (var pair in _baseHousingBySource) GameLog.Event($"  base housing {pair.Value:+#;-#;0} from {pair.Key}", Log);
        foreach (var pair in HousingModifiers.Sources(HousingKey)) GameLog.Event($"  housing {pair.Value.Flat:+0.##;-0.##;0} / {pair.Value.Percent:+0.##;-0.##;0}% from {pair.Key}", Log);
        GameLog.Event($"Deaths {deaths} (true {trueDeaths}), vagrant deaths {vagrantDeaths}", Log);
    }
}
