using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The stores of food (roadmap S04 + the famine's "Diverse crops and stores"): earth-beans, bitter roots, grains, dried
/// peaches, ash-bread, game... each a resource of the Stored Food section with a food value (earth-beans 0.5: two
/// feed like one Food; Behemoth Meat 5) and a rate of spoilage (<see cref="PantrySettings"/>, Resources/Food/Pantry).
/// - Spoilage: each kind loses a share per Seventh, shown as a "Spoilage" line of its production; preservation
///   technologies slow it and add room.
/// - Reserve: whenever Food is falling (its net rate below zero), the stores feed in the shortfall as a "Food stores"
///   line of Food's production, drawing the most perishable kinds first, until they run out.
/// - Payment: claiming land is paid in food value from the stores (<see cref="TrySpend"/>).
/// - The famine reads the stores' value as reserve, their variety as preparation and a peach-only cellar as monocrop.
/// Numbers are proposals. Created by <see cref="GenesisLoop"/>.
/// </summary>
public class Pantry : SingletonBehaviour<Pantry>
{
    private const LogChannel Log = LogChannel.Population;
    public const string CoverSource = "Food stores", SpoilSource = "Spoilage";
    private const float TickSeconds = 0.5f;

    public PantrySettings Settings { get; private set; }

    /// <summary>Food value per second the stores are feeding into Food now (0 while Food is not falling).</summary>
    public float CoverRate { get; private set; }

    private float _tickAt, _elapsed;
    private bool _stocked;
    private readonly Dictionary<string, float> _spoilRates = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    protected override void OnSingletonAwake()
    {
        // A copy for this world: the kinds the people invent (AddKind) are added to it, never to the asset.
        var asset = Resources.Load<PantrySettings>("Food/Pantry");
        if (asset == null) GameLog.Warning("No Resources/Food/Pantry: there are no food stores.", Log);
        else Settings = Instantiate(asset);
    }

    protected override void OnSingletonDestroy()
    {
        if (Settings == null) return;
        if (Application.isPlaying) Destroy(Settings);
        else DestroyImmediate(Settings);
    }

    /// <summary>
    /// A kind of stored food made during play (a dish or drink the people invented): added to this world's stores, or
    /// brought up to date when it is already known. False with no stores.
    /// </summary>
    public bool AddKind(FoodKind kind)
    {
        if (Settings == null || kind == null || string.IsNullOrEmpty(kind.resource)) return false;
        Settings.kinds.RemoveAll(k => k != null && string.Equals(k.resource, kind.resource, StringComparison.OrdinalIgnoreCase));
        Settings.kinds.Add(kind);
        return true;
    }

    // ===== QUERIES =====

    /// <summary>Every kind and how much of it is held.</summary>
    public List<FoodStock> Stock()
    {
        var units = GameUnitsLogic.Instance;
        if (Settings == null) return new List<FoodStock>();
        return Settings.kinds.Where(k => k != null && !string.IsNullOrEmpty(k.resource))
            .Select(k => new FoodStock(k, units != null ? units.GetResourceAmountExact(k.resource) : 0f)).ToList();
    }

    public float Value => PantryRules.Value(Stock());

    /// <summary>The stores' food value (0 before the pantry exists).</summary>
    public static float StoredValue => Instance != null ? Instance.Value : 0f;

    public int Variety => Settings != null ? PantryRules.Variety(Stock(), Settings.varietyMinimumValue) : 0;

    public float PeachShare => PantryRules.PeachShare(Stock());

    /// <summary>Seconds the stores would last at the present draw (infinity when not drawn on).</summary>
    public float SecondsLeft => CoverRate > 0f ? Value / CoverRate : float.PositiveInfinity;

    /// <summary>Spoilage now multiplied by this (preservation technologies researched).</summary>
    public float PreservationMultiplier
    {
        get
        {
            float m = 1f;
            if (Settings != null) foreach (var p in Settings.preservation) if (p != null && Researched(p.technology)) m *= Mathf.Clamp01(p.spoilMultiplier);
            return m;
        }
    }

    public float Capacity
    {
        get
        {
            if (Settings == null) return 0f;
            float room = Settings.capacity;
            foreach (var p in Settings.preservation) if (p != null && Researched(p.technology)) room += Mathf.Max(0f, p.capacityBonus);
            return room;
        }
    }

    public static bool IsStoredFood(string resource) => Instance != null && Instance.Settings != null && Instance.Settings.Kind(resource) != null;

    /// <summary>What a resource is to the kitchen (Edible, Tea, Beverage, Ingredient, Spice), or null when the pantry does not know it.</summary>
    public static FoodClass? ClassOf(string resource)
    {
        var kind = Instance != null && Instance.Settings != null ? Instance.Settings.Kind(resource) : null;
        return kind != null ? kind.cuisine : (FoodClass?)null;
    }

    /// <summary>Raised when stored food is eaten to cover a shortfall of Food (resource, amount): the culture's foodways listen.</summary>
    public static event Action<string, float> Eaten;

    /// <summary>Why <paramref name="value"/> of food value cannot be paid now, or null.</summary>
    public static string WhyNotAfford(float value)
    {
        if (value <= 0f) return null;
        float held = StoredValue;
        return held + 1e-4f >= value ? null : $"Needs {value:0.#} food value from your stores ({held:0.#} stored).";
    }

    /// <summary>Pay <paramref name="value"/> of food value from the stores, the most perishable first. False (and nothing taken) when they hold less.</summary>
    public static bool TrySpend(float value)
    {
        if (value <= 0f) return true;
        if (Instance == null || WhyNotAfford(value) != null) return false;
        Instance.Take(value);
        return true;
    }

    /// <summary>For a stored food's tooltip: its food value, spoilage and the stores' total.</summary>
    public string Describe(string resource)
    {
        var kind = Settings != null ? Settings.Kind(resource) : null;
        if (kind == null) return null;
        float spoil = kind.spoilPerSeventh * PreservationMultiplier;
        var rows = new List<string>
        {
            TooltipText.Row("Kitchen", CultureRules.ClassName(kind.cuisine) + (kind.cuisine == FoodClass.Ingredient ? " (eaten raw only when the edibles run out)" : kind.cuisine == FoodClass.Spice ? " (never eaten to fill a belly)"
                : kind.cuisine == FoodClass.EleosTea ? " (drunk like food; a category of its own at the table)"
                : kind.cuisine == FoodClass.Beverage ? " (from the cellar: drunk for joy, opened for hunger only once all else is gone)" : string.Empty)),
            TooltipText.Row("Food value", kind.cuisine == FoodClass.Spice || kind.foodValue <= 0f ? "none" : kind.foodValue == 1f ? "1 (feeds like Food)" : kind.foodValue < 1f ? $"{kind.foodValue:0.##} ({1f / kind.foodValue:0.#} feed like one Food)" : $"{kind.foodValue:0.##} (one feeds like {kind.foodValue:0.#} Food)"),
            TooltipText.Row("Spoils", spoil <= 0f ? "never" : $"{spoil:P1} of the stock each Seventh"),
            TooltipText.Row("All stores", $"{Value:0.#} food value in {Variety} kind{(Variety == 1 ? "" : "s")}"),
        };
        rows.Add(TooltipText.Muted("Stored food feeds the people whenever Food is falling, and pays for new land."));
        return string.Join("\n", rows);
    }

    // ===== FRAME =====

    private void Update()
    {
        if (Settings == null) return;
        var units = GameUnitsLogic.Instance;
        if (units == null || units.storageTab == null) return;
        if (!_stocked) Stock(units);
        if (SaveMenu.BlocksGameplay || Time.timeScale <= 0f) return;
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive()) return;
        _elapsed += Time.deltaTime;
        if (Time.unscaledTime < _tickAt) return;
        _tickAt = Time.unscaledTime + TickSeconds;
        float seconds = _elapsed;
        _elapsed = 0f;
        // What the stores fed in since the last step is taken from them now.
        if (CoverRate > 0f) Take(CoverRate * seconds, eaten: true);
        Spoil(units);
        UpdateCover(units);
        Bank(units);
    }

    // ===== SURPLUS INTO THE STORES =====

    /// <summary>
    /// Food gathered beyond a hand's worth (<see cref="PantrySettings.keepInHand"/>) goes into the stores: only once the
    /// founders are all in (their rations come first) and the storage technology is known.
    /// </summary>
    public bool Banking => Settings != null && Settings.Kind(Settings.bankedKind) != null && Researched(Settings.bankTechnology)
        && (PopGrowthLogic.Instance == null || PopGrowthLogic.Instance.FoundingDone);

    /// <summary>Food kept in hand while the surplus is stored (0 when nothing is banked).</summary>
    public float KeptInHand => Banking ? Mathf.Max(0f, Settings.keepInHand) : 0f;

    private void Bank(GameUnitsLogic units)
    {
        if (!Banking) return;
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        var kind = Settings.Kind(Settings.bankedKind);
        if (food == null || kind.foodValue <= 0f) return;
        var slot = units.GetResourceSlotFromName(kind.resource);
        float room = kind.keepsOwnRoom && slot != null ? slot.maxAmount : Capacity;
        float take = PantryRules.Banked(units.GetResourceAmountExact(food), Settings.keepInHand, units.GetResourceAmountExact(kind.resource), room, kind.foodValue);
        if (take < 0.01f) return;
        units.ChangeResourceFromName(food, -take, false);
        units.ChangeResourceFromName(kind.resource, take / kind.foodValue, false);
    }

    /// <summary>For the Food counter's tooltip: where gathered Food goes now (the gates, the stores, or it piles up).</summary>
    public static string DescribeFood()
    {
        var pop = PopGrowthLogic.Instance;
        if (pop != null && pop.FoundersWaiting > 0)
            return TooltipText.Muted($"{pop.FoundersWaiting} founders wait at the gates. Every {pop.GetArrivalRations():0} Food gathered lets one in, provisioned for good: the founders never draw on the daily food.");
        var pantry = Instance;
        if (pantry == null || pantry.Settings == null) return null;
        if (pantry.Banking)
            return TooltipText.Muted($"Food beyond {pantry.Settings.keepInHand:0} is put into the stores as {pantry.Settings.bankedKind}, where it keeps (and slowly spoils) until it is needed.");
        return TooltipText.Muted($"Food piles up here until {pantry.Settings.bankTechnology} is known; then what is gathered beyond {pantry.Settings.keepInHand:0} goes into the stores.");
    }

    // The survivors' cellars: given once, when a world begins (a restored save overwrites the amounts).
    private void Stock(GameUnitsLogic units)
    {
        _stocked = true;
        if (SaveSession.Restoring) return;
        foreach (var a in Settings.startingStores.Where(a => a != null && a.amount > 0f)) units.ChangeResourceFromName(a.resource, a.amount, false);
    }

    private void Spoil(GameUnitsLogic units)
    {
        var production = GlobalProductionManager.Instance;
        if (production == null) return;
        float perSeventh = SecondsPerSeventh(), preservation = PreservationMultiplier, room = Capacity;
        foreach (var s in Stock())
        {
            var slot = units.GetResourceSlotFromName(s.kind.resource);
            if (slot != null && room > 0f && !s.kind.keepsOwnRoom && !Mathf.Approximately(slot.maxAmount, room)) slot.maxAmount = room;
            float rate = PantryRules.SpoilPerSecond(s.amount, s.kind.spoilPerSeventh, preservation, perSeventh);
            _spoilRates.TryGetValue(s.kind.resource, out float was);
            if (Mathf.Abs(rate - was) < 1e-5f && (rate > 0f || was == 0f)) continue;
            _spoilRates[s.kind.resource] = rate;
            if (slot != null || was != 0f) production.SetFlatRate(s.kind.resource, SpoilSource, -rate);
        }
    }

    private void UpdateCover(GameUnitsLogic units)
    {
        var production = GlobalProductionManager.Instance;
        string food = GameCatalog.ResourceNameFor(ResourceRole.Food);
        if (production == null || food == null) return;
        float netWithout = production.GetNetProductionRate(food) - CoverRate;
        float cover = PantryRules.Cover(netWithout, Value, Settings.maxCoverPerSecond, TickSeconds);
        if (Mathf.Abs(cover - CoverRate) < 1e-5f) return;
        if (cover > 0f && CoverRate <= 0f) GameLog.Event($"Food is falling ({netWithout:0.##}/s): the stores feed in {cover:0.##} food value per second.", Log);
        CoverRate = cover;
        production.SetFlatRate(food, CoverSource, cover);
    }

    // Draw food value from the stores. Eaten food (a shortfall covered) is told to the culture and leaves its
    // leftovers for the kitchen (Peach Pits); food paid for land is neither.
    private void Take(float value, bool eaten = false)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return;
        foreach (var (resource, amount) in PantryRules.Draw(Stock(), value))
        {
            if (amount <= 0f) continue;
            units.ChangeResourceFromName(resource, -amount, false);
            if (!eaten) continue;
            var left = PantryRules.Leftover(Settings.Kind(resource), amount);
            if (left.HasValue && units.GetResourceSlotFromName(left.Value.resource) != null) units.ChangeResourceFromName(left.Value.resource, left.Value.amount, false);
            Eaten?.Invoke(resource, amount);
        }
    }

    private static float SecondsPerSeventh()
    {
        var time = TimeSystemLogic.Instance;
        return time == null ? 180f : time.canTrackTime ? time.GetEffectiveSecondsPerSeventh() : time.BaseSecondsPerSeventh;
    }

    private static bool Researched(string technology) =>
        !string.IsNullOrEmpty(technology) && GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.IsTechnologyUnlocked(technology);
}
