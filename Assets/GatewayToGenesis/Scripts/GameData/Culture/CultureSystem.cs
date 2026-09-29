using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The culture of the nation: one per game, unique to the civilization (roadmap S16). Rules in <see cref="CultureRules"/>,
/// numbers in <see cref="CultureSettings"/> (Resources/Culture/Culture); created by <see cref="GenesisLoop"/>, saved whole
/// (<see cref="_state"/> in GameSnapshot.Schema).
///
/// - Founding: researching Horology tells the founding myth (Culture.ink, "Why were we founded?"). Its answer founds the
///   culture on a baseline of leanings and gives the myth's effects; then the people are asked their name, what they
///   are called and what their ways are called (Iridia / Citizens: Iridian / Culture: Iridian: <see cref="CultureNamingDialog"/>).
/// - Leanings: the ten ways of living (the vault's civic families). Each Seventh the culture drifts toward what it lives:
///   its myth, pillars, civics, what it eats and works, its land and districts. Its leading leaning is its character,
///   which gives a small effect ("Culture: Character").
/// - Foodways: what the people eat (the stores they hold and draw on; Edibles, Eleos Teas, Ingredients and Spices apart) grows
///   familiar; the first of a class, familiar for long enough, is offered as the national food (the story
///   culture_national_food). Embraced, it joins the heritage and its output grows ("Culture: National Foods").
/// - Spread: the culture takes root on held land, faster near settlements, and fades where land is lost (the Culture lens).
/// - Reform (Digestive Rebirth): shift the culture toward a family, digesting the ways of an investigated ruin, or at a
///   cost of presence when there is none.
/// Stories read it through the "culture" and "national_food" condition domains, change it with the "culture:"
/// consequence, and name it with {nation}, {citizens}, {people} and {culture} in their text.
/// </summary>
public partial class CultureSystem : SingletonBehaviour<CultureSystem>
{
    private const LogChannel Log = LogChannel.Civics;
    public const string MythSource = "Culture: Founding Myth", CharacterSource = "Culture: Character", NationalSource = "Culture: National Foods";

    public CultureSettings Settings { get; private set; }
    public CultureTuning Tuning => Settings != null && Settings.tuning != null ? Settings.tuning : _defaults;

    // Saved (GameSnapshot.Schema).
    private CultureState _state = new CultureState();

    private readonly CultureTuning _defaults = new CultureTuning();
    private readonly Dictionary<string, float> _usedThisSeventh = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private TimeSystemLogic _time;
    private CivicManager _civics;
    private bool _namingShown, _offerChecked;
    private string _appliedKey;
    private float _checkAt;

    /// <summary>Anything about the culture changed (a Seventh passed, a name, a food, a reform).</summary>
    public event Action Changed;
    public event Action<FoundingMyth> Founded;
    public event Action<CultureIdentity> Named;
    /// <summary>A food became national (<see cref="Foodway.cuisine"/> says whether food, ingredient or spice).</summary>
    public event Action<Foodway> NationalFoodAdopted;
    /// <summary>The culture reformed toward a family (true when it digested a ruin).</summary>
    public event Action<EnclaveFamily, bool> Reformed;

    // ===== STATIC READS (safe with no culture in the scene) =====

    public static bool IsFounded => Instance != null && Instance._state.founded;
    public static bool IsNamed => Instance != null && Instance._state.identity != null && Instance._state.identity.IsNamed;
    /// <summary>The nation's name, or "Your Nation" before it has one.</summary>
    public static string NationName => IsNamed ? Instance._state.identity.name : "Your Nation";
    /// <summary>What its people are called (Iridian), or "Survivors" before they are named.</summary>
    public static string Demonym => IsNamed ? Instance.Identity.demonym : "Survivors";
    /// <summary>What its ways are called (Iridian), or "Unnamed" before.</summary>
    public static string Adjective => IsNamed ? Instance.Identity.adjective : "Unnamed";
    /// <summary>Story text with the culture's words filled in ({nation}, {citizens}, {people}, {culture}, {myth}, {national_food}).</summary>
    public static string ExpandText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        var s = Instance != null ? Instance._state : null;
        var pending = s != null ? s.foodways.FirstOrDefault(f => string.Equals(f.resource, s.pendingFood, StringComparison.OrdinalIgnoreCase)) : null;
        text = CultureRules.Expand(text, s?.identity, Instance != null ? Instance.Myth?.title : null, s?.pendingFood, pending != null ? pending.cuisine : FoodClass.Edible);
        return Instance != null ? Instance.ExpandPublicMemory(text) : text;
    }

    // ===== READS =====

    public CultureState State => _state;
    public CultureIdentity Identity => _state.identity ?? (_state.identity = new CultureIdentity());
    public FoundingMyth Myth => FoundingMyths.Find(_state.myth);
    public bool NamingPending => _state.founded && _state.namingPending;
    /// <summary>Sevenths since the founding.</summary>
    public int Age => _state.sevenths;

    /// <summary>The leanings, one per family, summing to 1 (all zero before the founding).</summary>
    public IReadOnlyList<CultureLeaning> Leanings => CultureRules.ToList(CultureRules.ToArray(_state.leanings));
    public float Leaning(EnclaveFamily family) => CultureRules.ToArray(_state.leanings)[(int)family];
    /// <summary>The families from the strongest down.</summary>
    public List<EnclaveFamily> Ranked => CultureRules.Ranked(CultureRules.ToArray(_state.leanings));
    /// <summary>The culture's leading leaning, or null before the founding.</summary>
    public EnclaveFamily? Dominant => _state.founded && _state.leanings.Count > 0 ? Ranked[0] : (EnclaveFamily?)null;
    /// <summary>"Weaver" or "Weaver and Agromagical" when two run close; "Unformed" before the founding.</summary>
    public string Character => CultureRules.Character(CultureRules.ToArray(_state.leanings));
    /// <summary>What the culture is living now (where its leanings drift to), as leanings.</summary>
    public float[] TargetLeanings() => CultureRules.Target(GatherInputs(), Tuning);

    public IReadOnlyList<Foodway> Foodways => _state.foodways;
    public IEnumerable<Foodway> NationalFoods => _state.foodways.Where(f => f.national);
    public bool IsNationalFood(string resource) => _state.foodways.Any(f => f.national && string.Equals(f.resource, resource, StringComparison.OrdinalIgnoreCase));
    public Foodway FoodwayOf(string resource) => _state.foodways.FirstOrDefault(f => string.Equals(f.resource, resource, StringComparison.OrdinalIgnoreCase));
    /// <summary>What a resource is to the kitchen, from the pantry (null when it is not a food, ingredient or spice).</summary>
    public static FoodClass? ClassOf(string resource) => Pantry.ClassOf(resource);
    /// <summary>A food waiting for the people to decide whether it is theirs, or null.</summary>
    public string PendingFood => _state.pendingFood;

    /// <summary>The culture's presence in a world cell, 0-1.</summary>
    public float PresenceAt(int cell) => _state.presence.TryGetValue(cell, out float p) ? p : 0f;
    /// <summary>Cells where the culture is present at all.</summary>
    public int CellsReached => _state.presence.Count;
    /// <summary>How much of the land held the culture has become (mean presence over held cells), 0-1.</summary>
    public float Cohesion => CultureRules.Cohesion(HeldCells().Select(t => PresenceAt(t.index)));

    public IReadOnlyList<CultureMoment> Moments => _state.moments;
    public int Reforms => _state.reforms;
    public IReadOnlyList<string> FoundingCivics => _state.foundingCivics;

    // ===== LIFETIME =====

    protected override void OnSingletonAwake()
    {
        Settings = Resources.Load<CultureSettings>("Culture/Culture");
        if (Settings == null) GameLog.Warning("No Resources/Culture/Culture: the culture uses its default tuning.", Log);
        CultureField.Register(PresenceAt);
        GameTechnologySlot.Researched += OnResearched;
        Pantry.Eaten += OnEaten;
    }

    protected override void OnSingletonDestroy()
    {
        CultureField.Unregister(PresenceAt);
        GameTechnologySlot.Researched -= OnResearched;
        Pantry.Eaten -= OnEaten;
        if (_time != null) _time.OnSeventhChange -= OnSeventh;
        if (_civics != null) _civics.OnActiveCivicsChanged -= CheckFoundingCivics;
        UnhookWorld();
        UnhookMemory();
    }

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time =>
        {
            _time = time;
            _time.OnSeventhChange += OnSeventh;
        });
    }

    private void Update()
    {
        // A load (or the founding of this world) brings its inventions: their resources and pantry kinds are made again.
        CheckInventions();
        if (_civics == null && CivicManager.Instance != null)
        {
            _civics = CivicManager.Instance;
            _civics.OnActiveCivicsChanged += CheckFoundingCivics;
        }
        HookWorld();
        EnsureMemoryHooks();
        if (Time.unscaledTime < _checkAt || SaveMenu.BlocksGameplay) return;
        _checkAt = Time.unscaledTime + 0.5f;
        // A world loaded (or begun) after Horology still owes its founding myth.
        if (!_offerChecked && GameUnitsLogic.Instance != null && EventVolumeManager.Instance != null)
        {
            _offerChecked = true;
            if (!_state.founded && Researched(FoundingTechnology)) OfferFounding();
            if (!string.IsNullOrEmpty(_state.pendingFood)) OfferNationalFood(false);
            CheckLife();
        }
        // The rates of Unity and Faith follow the length of a Seventh (slow motion while an event waits).
        if (_state.founded) ApplyLife();
        // The name is asked once the founding story has closed.
        var events = EventSystemLogic.Instance;
        if (NamingPending && !_namingShown && (events == null || !events.IsEventActive()) && !CultureNamingDialog.IsOpen)
        {
            _namingShown = true;
            CultureNamingDialog.Open();
        }
    }

    private string FoundingTechnology => Settings != null && !string.IsNullOrEmpty(Settings.foundingTechnology) ? Settings.foundingTechnology : "Horology";
    private string FoundingStory => Settings != null && !string.IsNullOrEmpty(Settings.foundingStory) ? Settings.foundingStory : "culture_founding_myth";
    private string NationalFoodStory => Settings != null && !string.IsNullOrEmpty(Settings.nationalFoodStory) ? Settings.nationalFoodStory : "culture_national_food";

    private void OnResearched(GameTechnologySlot slot)
    {
        if (slot == null || slot.gameUnit == null || _state.founded) return;
        if (string.Equals(slot.gameUnit.name, FoundingTechnology, StringComparison.OrdinalIgnoreCase)) OfferFounding();
    }

    /// <summary>Tell the founding myth (unlock its story and let the events offer it). Founds at once, on the first myth, when the story is missing.</summary>
    public void OfferFounding()
    {
        if (_state.founded) return;
        var volumes = EventVolumeManager.Instance;
        var events = EventSystemLogic.Instance;
        if (events != null && events.GetCompletionCount(FoundingStory) > 0) return;
        if (volumes != null && volumes.UnlockStory(FoundingStory))
        {
            GameLog.Event("Horology: the founding myth waits to be told.", Log);
            events?.TriggerEventCheck();
            return;
        }
        GameLog.Warning($"No story '{FoundingStory}' in Resources/Events: the culture is founded on {FoundingMyths.Song.title} without it.", Log);
        Found(FoundingMyths.Song, "no founding story");
    }

    // ===== FOUNDING AND NAMING =====

    /// <summary>
    /// Found the culture on <paramref name="myth"/>: its baseline leanings, its effects (<see cref="MythSource"/>), the
    /// civics in force remembered, and the name asked for. False when it was founded already.
    /// </summary>
    public bool Found(FoundingMyth myth, string source = null)
    {
        if (myth == null || _state.founded) return false;
        _state.founded = true;
        _state.myth = myth.id;
        _state.foundedAge = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null;
        _state.sevenths = 0;
        _state.leanings = CultureRules.ToList(CultureRules.Baseline(myth));
        _state.foundingCivics = CivicManager.Instance != null ? CivicManager.Instance.GetAllActiveCivics().Where(c => c != null).Select(c => c.civicName).ToList() : new List<string>();
        _state.namingPending = !Identity.IsNamed;
        _namingShown = false;
        EffectRouter.ApplySet(MythSource, myth.effects);
        // The capital's land takes the culture at once.
        SeedPresence();
        Remember("founded", myth.title, myth.answer);
        GameLog.Event($"The culture is founded: {myth.title}{(source != null ? $" ({source})" : string.Empty)}.", Log);
        ApplyCharacter();
        CheckLife();
        ReconcileFeatures(CultureReconcileReason.Founded);
        Founded?.Invoke(myth);
        NotificationFeed.Push(myth.title, $"{myth.answer} The people's ways begin here.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:founded");
        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Name the nation, its people and its ways (the demonym and adjective are suggested from the name when empty). False
    /// with <paramref name="error"/> when a name cannot be used. Renaming later is allowed and remembered.
    /// </summary>
    public bool Name(string name, string demonym, string adjective, out string error)
    {
        string n = CultureRules.CleanName(name);
        string d = string.IsNullOrWhiteSpace(demonym) ? CultureRules.SuggestDemonym(n) : CultureRules.CleanName(demonym);
        string a = string.IsNullOrWhiteSpace(adjective) ? d : CultureRules.CleanName(adjective);
        error = CultureRules.WhyNotName(n, "The nation's name") ?? CultureRules.WhyNotName(d, "What its people are called") ?? CultureRules.WhyNotName(a, "What its culture is called");
        if (error != null) return false;
        bool renamed = Identity.IsNamed;
        string was = Identity.name;
        _state.identity = new CultureIdentity { name = n, demonym = d, adjective = a };
        _state.namingPending = false;
        if (renamed && string.Equals(was, n, StringComparison.Ordinal)) Remember("named", $"{d} ways", $"Its people are now called {d}, its culture {a}.");
        else Remember("named", renamed ? $"{was} becomes {n}" : $"The nation of {n}", $"Citizens: {d}. Culture: {a}.");
        GameLog.Event($"The nation is named {n} (citizens: {d}, culture: {a}).", Log);
        Named?.Invoke(_state.identity);
        NotificationFeed.Push(renamed ? $"{was} is now {n}" : $"The nation of {n}", $"Its people are {CultureRules.Plural(d)}, its ways {a}.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:named");
        RaiseChanged();
        return true;
    }

    // ===== THE SEVENTH =====

    private void OnSeventh(int seventh)
    {
        if (!_state.founded) return;
        Tick();
    }

    /// <summary>A Seventh of culture: spread over the land, a Seventh at the table, the drift of the leanings, national foods, effects.</summary>
    public void Tick()
    {
        if (!_state.founded) return;
        _state.sevenths++;
        _state.seventhsSinceReform++;
        Spread();
        // Its life first (the kitchen, luxuries, holidays, happiness), so what was cooked and enjoyed counts this Seventh.
        LifeSeventh();
        var diet = Diet();
        RecordTable(diet);
        CultureRules.Taste(_state.foodways, diet, Tuning);
        // The Seventh's committed occurrences go through the culture's features in their fixed phases (local life,
        // the traditions' lifecycle, social effects), before the drift reads what was lived.
        RunCulturePipeline();
        // What was eaten, used and lived this Seventh is read by the drift before it is forgotten.
        var target = TargetLeanings();
        _usedThisSeventh.Clear();
        _lived.Clear();
        _state.leanings = CultureRules.ToList(CultureRules.Drift(CultureRules.ToArray(_state.leanings), target, Tuning.driftPerSeventh));
        if (string.IsNullOrEmpty(_state.pendingFood))
        {
            var next = CultureRules.NextNational(_state.foodways, Tuning);
            if (next != null)
            {
                _state.pendingFood = next.resource;
                OfferNationalFood(true);
            }
        }
        ApplyCharacter();
        CheckFoundingCivics();
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        CultureField.Touch();
        Changed?.Invoke();
    }

    // ===== WHAT THE PEOPLE EAT AND USE =====

    /// <summary>Tell the culture the people ate or used <paramref name="amount"/> of a resource (cooking, recipes, rites). Its foodways and leanings listen.</summary>
    public void RecordUse(string resource, float amount)
    {
        if (string.IsNullOrEmpty(resource) || amount <= 0f) return;
        _usedThisSeventh.TryGetValue(resource, out float was);
        _usedThisSeventh[resource] = was + amount;
    }

    /// <summary><see cref="RecordUse"/> from anywhere (no-op with no culture in the scene).</summary>
    public static void Use(string resource, float amount) => Instance?.RecordUse(resource, amount);

    private void OnEaten(string resource, float amount) => RecordUse(resource, amount);

    // This Seventh at the table: every kind the pantry knows, held (food value for edibles and teas, amount for the rest) and drawn.
    private List<DietEntry> Diet()
    {
        var diet = new List<DietEntry>();
        var pantry = Pantry.Instance;
        if (pantry == null) return diet;
        foreach (var s in pantry.Stock())
        {
            _usedThisSeventh.TryGetValue(s.kind.resource, out float used);
            float held = s.kind.cuisine == FoodClass.Edible || s.kind.cuisine == FoodClass.EleosTea ? s.Value : Mathf.Max(0f, s.amount);
            diet.Add(new DietEntry(s.kind.resource, s.kind.cuisine, held, used, IsInvented(s.kind.resource)));
        }
        return diet;
    }

    // ===== LEANINGS =====

    /// <summary>What the culture lives now: myth, pillars, civics, what it eats and works, its land and districts.</summary>
    public CultureInputs GatherInputs()
    {
        var inputs = new CultureInputs { myth = Myth };
        var stats = StatManager.Instance;
        if (stats != null) foreach (var pillar in StatDefinitions.Pillars) inputs.pillars[pillar] = stats.GetPillarValue(pillar);
        if (CivicManager.Instance != null)
            foreach (var civic in CivicManager.Instance.GetAllActiveCivics())
            {
                var family = civic != null ? Tuning.FamilyOfCivic(civic.civicName) : null;
                if (family.HasValue) inputs.civics.Add(family.Value);
            }
        // What they work (production each Seventh) and what they ate or used.
        var production = GlobalProductionManager.Instance;
        float perSeventh = _time != null ? _time.GetEffectiveSecondsPerSeventh() : 180f;
        if (production != null)
            foreach (var unit in GameCatalog.Resources.All)
            {
                var family = unit != null ? Tuning.FamilyOfResource(unit.name) : null;
                if (!family.HasValue) continue;
                float rate = production.GetNetProductionRate(unit.name);
                if (rate > 0f) AddTo(inputs.used, family.Value, rate * perSeventh);
            }
        foreach (var pair in _usedThisSeventh)
        {
            // An invented dish leans the culture as its ingredients do, each by its share.
            var invented = InventedFood(pair.Key);
            if (invented != null)
            {
                foreach (var l in invented.leanings.Where(l => l != null && l.share > 0f)) AddTo(inputs.used, l.family, pair.Value * l.share);
                continue;
            }
            var family = Tuning.FamilyOfResource(pair.Key);
            if (family.HasValue) AddTo(inputs.used, family.Value, pair.Value);
        }
        // Its rites, festivals, landmarks and holidays.
        foreach (var pair in _lived) AddTo(inputs.used, pair.Key, pair.Value);
        // The land it lives on, weighted by presence.
        var world = WorldSystem.Instance;
        if (world != null && world.Map != null)
        {
            foreach (var t in HeldCells())
            {
                float p = Mathf.Max(0.05f, PresenceAt(t.index));
                if (t.sacred) AddTo(inputs.land, EnclaveFamily.Auric, p);
                if (t.river) AddTo(inputs.land, EnclaveFamily.Trading, p * 0.5f);
                var family = Tuning.FamilyOfLand(t.terrain, t.macroBiome, t.landform);
                if (family.HasValue) AddTo(inputs.land, family.Value, p);
            }
            foreach (var s in world.Map.Settlements)
            {
                if (s == null || string.IsNullOrEmpty(s.district)) continue;
                if (CultureRules.TryFamily(s.district, out var family) || CultureRules.TryFamily(world.Rules?.tributaries?.District(s.district)?.enclave, out family)) inputs.districts.Add(family);
            }
        }
        return inputs;
    }

    private static void AddTo(IDictionary<EnclaveFamily, float> map, EnclaveFamily family, float amount)
    {
        map.TryGetValue(family, out float was);
        map[family] = was + amount;
    }

    // The character's effect (what the leading leaning gives) and the national foods' output, re-applied from scratch
    // only when either changed (a restored save keeps them in the ledgers, so the first check may re-apply them once).
    private void ApplyCharacter()
    {
        var dominant = Dominant;
        string key = $"{dominant}|{string.Join(",", NationalFoods.Select(f => f.resource))}|{Tuning.nationalFoodBonusPercent}";
        if (key == _appliedKey) return;
        _appliedKey = key;
        var effects = dominant.HasValue ? Tuning.character.Where(c => c != null && c.family == dominant.Value).Select(c => c.ToEffect()).ToList() : new List<GameEffect>();
        EffectRouter.ApplySet(CharacterSource, effects);
        var national = NationalFoods.Where(f => Tuning.nationalFoodBonusPercent != 0f)
            .Select(f => new GameEffect(GameEffectType.ResourceModifier, Tuning.nationalFoodBonusPercent, ModifierType.Percentage, f.resource)).ToList();
        EffectRouter.ApplySet(NationalSource, national);
    }

    // ===== NATIONAL FOODS =====

    // Ask the people whether the waiting food is theirs: its story when there is one, else it is embraced at once.
    private void OfferNationalFood(bool announce)
    {
        var way = FoodwayOf(_state.pendingFood);
        if (way == null) { _state.pendingFood = null; return; }
        var volumes = EventVolumeManager.Instance;
        if (volumes != null && volumes.UnlockStory(NationalFoodStory))
        {
            if (announce) GameLog.Event($"{way.resource} waits to be called the {CultureRules.NationalLabel(way.cuisine)}.", Log);
            EventSystemLogic.Instance?.TriggerEventCheck();
            return;
        }
        EmbraceNationalFood("no national food story");
    }

    /// <summary>The people call the waiting food theirs: it becomes national (heritage, and its output grows). False when none waits.</summary>
    public bool EmbraceNationalFood(string source = null)
    {
        var way = FoodwayOf(_state.pendingFood);
        _state.pendingFood = null;
        if (way == null || way.national) return false;
        way.national = true;
        way.nationalAge = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null;
        way.nationalSeventh = _state.sevenths;
        string label = CultureRules.NationalLabel(way.cuisine);
        Remember("national-food", $"{way.resource}, the {label}", $"The {(IsNamed ? CultureRules.Plural(Demonym) : "people")} grew so used to {way.resource} that it became their {label}.");
        GameLog.Event($"{way.resource} becomes the {label}{(source != null ? $" ({source})" : string.Empty)}.", Log);
        // A nation that makes the Auric peach its own drifts deeper into the monocrop (The Inescapable Hunger).
        var kind = Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(way.resource) : null;
        if (kind != null && kind.peach) EventSystemLogic.Instance?.ModifyEventScore("monocrop", 1);
        // The embrace is the food's tradition record: where it came from, and whether it is still kept at the table.
        RecordNationalFood(way);
        ApplyCharacter();
        NationalFoodAdopted?.Invoke(way);
        NotificationFeed.Push($"{way.resource} is the {label}", $"The {(IsNamed ? Adjective : "people's")} table has made it its own: +{Tuning.nationalFoodBonusPercent:0}% {way.resource} output.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:food:" + way.resource);
        RaiseChanged();
        return true;
    }

    /// <summary>The people keep their table varied instead: the waiting food is not offered again until its familiarity falls and rises again.</summary>
    public bool DeclineNationalFood(string source = null)
    {
        var way = FoodwayOf(_state.pendingFood);
        _state.pendingFood = null;
        if (way == null) return false;
        way.declined = true;
        way.firstSevenths = 0;
        Remember("declined-food", $"{way.resource} is not made national", "The people chose to keep their table varied.");
        GameLog.Event($"{way.resource} is declined as {CultureRules.NationalLabel(way.cuisine)}{(source != null ? $" ({source})" : string.Empty)}.", Log);
        RaiseChanged();
        return true;
    }

    // ===== SPREAD =====

    /// <summary>The cells held by the civilization (its authority and its Outposts).</summary>
    public IEnumerable<WorldTile> HeldCells()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map == null) return Enumerable.Empty<WorldTile>();
        return map.Tiles.Where(t => t != null && !t.water && WorldAuthority.IsPlayers(t.authorityId));
    }

    private void SeedPresence()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        var capital = map?.Get(map.Capital);
        if (capital != null && !_state.presence.ContainsKey(capital.index)) _state.presence[capital.index] = 0.5f;
    }

    private void Spread()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map == null) return;
        var tuning = Tuning;
        var seats = map.Settlements.Where(s => s != null).Select(s => s.coord).ToList();
        if (seats.Count == 0) seats.Add(map.Capital);
        var held = new HashSet<int>();
        foreach (var t in HeldCells())
        {
            held.Add(t.index);
            int distance = seats.Min(c => HexCoord.Distance(c, t.coord));
            float next = CultureRules.Spread(PresenceAt(t.index), true, CultureRules.Nearness(distance, tuning.seatReach), tuning);
            _state.presence[t.index] = next;
        }
        foreach (int cell in _state.presence.Keys.Where(c => !held.Contains(c)).ToList())
        {
            float next = CultureRules.Spread(_state.presence[cell], false, 0f, tuning);
            if (next < tuning.forgetBelow) _state.presence.Remove(cell);
            else _state.presence[cell] = next;
        }
    }

    // ===== REFORM (DIGESTIVE REBIRTH) =====

    /// <summary>Ruins the culture could digest in a reform: investigated, their ways not taken in yet.</summary>
    public IEnumerable<Ruin> DigestibleRuins()
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map == null) return Enumerable.Empty<Ruin>();
        return map.Ruins.Where(r => r != null && r.investigated && !_state.digestedRuins.Contains(r.id));
    }

    /// <summary>Why the culture cannot reform now, or null.</summary>
    public string WhyNotReform()
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        int wait = Tuning.reformCooldownSevenths - _state.seventhsSinceReform;
        if (_state.reforms > 0 && wait > 0) return $"The last reform is still settling: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        return null;
    }

    /// <summary>What a reform by the people's own will costs now, in words (a ruin's ways are digested from Heritage, T07).</summary>
    public string ReformCostText()
    {
        int ruins = DigestibleRuins().Count();
        return $"By the people's own will, they follow slowly: the culture keeps {Tuning.reformPresenceKept:P0} of its presence on the land."
            + (ruins > 0 ? $" Or take in the ways a fallen people's ruins record (Heritage: {ruins} ruin{(ruins == 1 ? "" : "s")} to read), toward what those records support." : string.Empty);
    }

    /// <summary>
    /// Reform the culture: <see cref="CultureTuning.reformShift"/> of it moves to <paramref name="toward"/>. With
    /// <paramref name="ruinId"/>, it digests that ruin's ways (Digestive Rebirth), only toward a way of living the ruin's
    /// own records support (<see cref="WhyNotReformToward"/>, T07); without one, by the people's own will, it costs
    /// presence. False with the reason said when it cannot.
    /// </summary>
    public bool Reform(EnclaveFamily toward, int ruinId = -1)
    {
        if (ruinId >= 0) return ReformFromRuin(toward, ruinId);
        string why = WhyNotReform();
        if (why != null) { GameLog.Event("Reform refused: " + why, Log); return false; }
        ReformCore(toward, null, Tuning.reformShift);
        return true;
    }

    // The reform itself (checks done): the shift, the ruin digested once (or presence paid), the heritage, Digestive Rebirth.
    private void ReformCore(EnclaveFamily toward, Ruin ruin, float shift)
    {
        _state.leanings = CultureRules.ToList(CultureRules.Reform(CultureRules.ToArray(_state.leanings), toward, shift));
        _state.reforms++;
        _state.seventhsSinceReform = 0;
        if (ruin != null) _state.digestedRuins.Add(ruin.id);
        else foreach (int cell in _state.presence.Keys.ToList()) _state.presence[cell] *= Mathf.Clamp01(Tuning.reformPresenceKept);
        string text = ruin != null ? $"The ways of the ruins of {ruin.name} were taken in: the ruins of the past fuel the roots of the present." : "The people were asked to live otherwise, and slowly did.";
        Remember("reform", $"Reformed toward the {toward} ways", text);
        GameLog.Event($"The culture reforms toward {toward}{(ruin != null ? $", digesting the ruins of {ruin.name}" : string.Empty)}.", Log);
        ApplyCharacter();
        Achievements.Report(AchievementEvent.Of(AchievementSignal.CultureReformed, _state.reforms, ruin != null)
            .From(ruin != null ? $"culture-reform:ruin:{ruin.id}" : $"culture-reform:{_state.reforms}", toward.ToString()));
        Reformed?.Invoke(toward, ruin != null);
        NotificationFeed.Push($"The culture reforms", $"It leans toward the {toward} ways now.{(ruin != null ? $" The ruins of {ruin.name} are digested." : string.Empty)}", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:reform");
        RaiseChanged();
    }

    // ===== STORIES =====

    /// <summary>
    /// A story's "culture:" consequence (<see cref="CultureRules.ParseConsequence"/>): found on a myth, embrace or decline the
    /// waiting food, lean N% toward a family, or add N% presence on the held land. False when it could not apply.
    /// </summary>
    public bool ApplyConsequence(string target, int value, string story)
    {
        if (!CultureRules.ParseConsequence(target, out string verb, out string argument)) return false;
        switch (verb)
        {
            case "myth": return Found(FoundingMyths.Find(argument), story);
            case "embrace": return EmbraceNationalFood(story);
            case "decline": return DeclineNationalFood(story);
            case "account": return ApplyAccountConsequence(argument);
            case "presence":
                if (!_state.founded) return false;
                foreach (var t in HeldCells()) _state.presence[t.index] = Mathf.Clamp01(PresenceAt(t.index) + value / 100f);
                RaiseChanged();
                return true;
            case "unity":
                if (!_state.founded) return false;
                GameUnitsLogic.Instance?.ChangeResourceFromName(UnityResource, value, false);
                RaiseChanged();
                return true;
            case "joy":
                if (!_state.founded) return false;
                _state.joy = Mathf.Clamp01(_state.joy + value / 100f);
                RaiseChanged();
                return true;
            default:
                if (!_state.founded || !CultureRules.TryFamily(argument, out var family)) return false;
                var shares = value >= 0 ? CultureRules.Reform(CultureRules.ToArray(_state.leanings), family, value / 100f) : TakeFrom(CultureRules.ToArray(_state.leanings), family, -value / 100f);
                _state.leanings = CultureRules.ToList(shares);
                ApplyCharacter();
                RaiseChanged();
                return true;
        }
    }

    private static float[] TakeFrom(float[] shares, EnclaveFamily family, float amount)
    {
        var s = CultureRules.Normalize(shares);
        s[(int)family] = Mathf.Max(0f, s[(int)family] - amount);
        return CultureRules.Normalize(s);
    }

    /// <summary>A condition's value in the "culture" domain (GameValues): founded, named, cohesion, sevenths, reforms, national_foods, pending_food, unity, happiness, joy, luxury, holidays, festivals, landmarks, dishes, drinks, traditions, traditions_recognized, traditions_dormant, tradition_choices, tradition:&lt;id&gt; (0 none, 1 emerging, 2 dormant, 3 lived), myth:&lt;id&gt;, &lt;Family&gt; (its share, 0-100).</summary>
    public float Value(string target)
    {
        string t = (target ?? string.Empty).Trim().ToLowerInvariant();
        if (t.Length == 0 || t == "founded") return _state.founded ? 1f : 0f;
        if (t == "named") return Identity.IsNamed ? 1f : 0f;
        if (t == "cohesion") return Mathf.Round(Cohesion * 100f);
        if (t == "sevenths") return _state.sevenths;
        if (t == "reforms") return _state.reforms;
        if (t == "cells") return CellsReached;
        if (t == "national_foods") return NationalFoods.Count();
        if (t == "pending_food") return string.IsNullOrEmpty(_state.pendingFood) ? 0f : 1f;
        if (t.StartsWith("myth:")) return string.Equals(_state.myth, t.Substring(5).Trim(), StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
        float life = LifeValue(t);
        if (!float.IsNaN(life)) return life;
        float tradition = TraditionValue(t);
        if (!float.IsNaN(tradition)) return tradition;
        float observance = ObservanceValue(t);
        if (!float.IsNaN(observance)) return observance;
        float teaching = TeachingValue(t);
        if (!float.IsNaN(teaching)) return teaching;
        float publicMemory = PublicMemoryValue(t);
        if (!float.IsNaN(publicMemory)) return publicMemory;
        float syncretism = SyncretismValue(t);
        if (!float.IsNaN(syncretism)) return syncretism;
        if (t.StartsWith("leaning:")) t = t.Substring(8);
        return CultureRules.TryFamily(t, out var family) ? Mathf.Round(Leaning(family) * 100f) : 0f;
    }

    // ===== HISTORY =====

    private void Remember(string kind, string title, string text)
    {
        _state.moments.Add(new CultureMoment
        {
            kind = kind, title = title, text = text, seventh = _state.sevenths,
            ageId = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null,
        });
    }

    // Ship of Theseus: none of the civics in force at the founding is left.
    private void CheckFoundingCivics()
    {
        if (!_state.founded || _state.foundingCivicsGone || _state.foundingCivics.Count == 0 || CivicManager.Instance == null) return;
        if (_state.foundingCivics.Any(c => CivicManager.Instance.IsCivicActive(c))) return;
        _state.foundingCivicsGone = true;
        Remember("theseus", "None of the founding civics remains", "Wait, at what point did we change culture?");
        Achievements.Report(AchievementEvent.Of(AchievementSignal.FoundingCivicsGone, _state.foundingCivics.Count).From("culture-founding-civics-gone", string.Join(",", _state.foundingCivics)));
        RaiseChanged();
    }

    private static bool Researched(string technology) =>
        !string.IsNullOrEmpty(technology) && GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.IsTechnologyUnlocked(technology);
}
