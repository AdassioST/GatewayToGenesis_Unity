using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes the net production rate of every resource.
///
///   output(r)      = Σ producers: rate × active units × (1 + efficiency%)      efficiency from GameUnitsLogic.ProductionEfficiency
///                  + Σ scaling bonuses: per-unit bonus × counted units          GameUnitsLogic.ProductionScaling
///                  + positive flat modifiers                                    ResourceModifiers (per second)
///   output(r)     *= 1 + (percent modifiers + morale delta) / 100
///   consumption(r) = Σ consumers: rate × active units + negative flat modifiers
///   net(r)         = output(r) - consumption(r)
///
/// Every modifier is read through <see cref="ModifierTargets.Resolve"/> (resource, its section, its type,
/// everything), so bonuses registered before a resource is discovered apply the moment it appears.
/// Rates are recomputed only when something they depend on changes (plus a slow safety refresh).
/// </summary>
public class GlobalProductionManager : SingletonBehaviour<GlobalProductionManager>
{
    private const LogChannel Log = LogChannel.Units;

    public List<GameResourceSlot> resourceSlots = new List<GameResourceSlot>();
    public List<GameProductionSlot> productionSlots = new List<GameProductionSlot>();
    public List<GameTechnologySlot> technologySlots = new List<GameTechnologySlot>();

    [Tooltip("Divides the exponential building cost growth; raise it as tech tiers advance.")]
    public float techTier = 1f;
    [Tooltip("Exponential building cost growth per building owned.")]
    public float costBalance = 0.05f;
    [Tooltip("Seconds between safety recomputations when nothing reported a change.")]
    [SerializeField] private float refreshInterval = 0.25f;

    /// <summary>
    /// Flat (per second, signed: negative is consumption) and percent output modifiers on resources,
    /// keyed by resource name or scope (see <see cref="ModifierTargets"/>).
    /// </summary>
    public ModifierLedger ResourceModifiers { get; } = new ModifierLedger();

    private readonly Dictionary<string, float> _netRates = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _outputRates = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _consumptionRates = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _scratchKeys = new List<string>(8);
    private bool _dirty = true;
    private float _nextRefresh;
    private bool _unitsSubscribed;

    protected override void OnSingletonAwake()
    {
        ResourceModifiers.Changed += _ => MarkDirty();
    }

    private void Start()
    {
        if (StatManager.Instance != null) StatManager.Instance.OnMoraleChanged += OnMoraleChanged;
        SubscribeToUnitLedgers();
    }

    protected override void OnSingletonDestroy()
    {
        if (StatManager.Instance != null) StatManager.Instance.OnMoraleChanged -= OnMoraleChanged;
    }

    private void OnMoraleChanged(int _) => MarkDirty();

    private void SubscribeToUnitLedgers()
    {
        var units = GameUnitsLogic.Instance;
        if (_unitsSubscribed || units == null) return;
        units.ProductionEfficiency.Changed += _ => MarkDirty();
        units.ProductionScaling.Changed += _ => MarkDirty();
        _unitsSubscribed = true;
    }

    /// <summary>Request a recomputation (cheap; coalesced to at most one per frame).</summary>
    public void MarkDirty() => _dirty = true;

    private void Update()
    {
        UpdateProductionSufficiency();
        if (_dirty || Time.unscaledTime >= _nextRefresh) Recalculate();
    }

    // ===== SLOT REGISTRY =====

    public void AddResourceSlot(GameResourceSlot resourceSlot)
    {
        if (resourceSlot == null || resourceSlots.Contains(resourceSlot)) return;
        resourceSlots.Add(resourceSlot);
        MarkDirty();
    }

    public void AddProductionSlot(GameProductionSlot productionSlot)
    {
        if (productionSlot == null || productionSlots.Contains(productionSlot)) return;
        productionSlots.Add(productionSlot);
        MarkDirty();
    }

    public void AddTechnologySlot(GameTechnologySlot technologySlot)
    {
        if (technologySlot != null && !technologySlots.Contains(technologySlot)) technologySlots.Add(technologySlot);
    }

    public void RemoveResourceSlot(GameResourceSlot resourceSlot)
    {
        if (resourceSlots.Remove(resourceSlot) && resourceSlot != null && resourceSlot.gameUnit != null)
        {
            _netRates.Remove(resourceSlot.gameUnit.name);
            MarkDirty();
        }
    }

    public void RemoveProductionSlot(GameProductionSlot productionSlot)
    {
        if (productionSlots.Remove(productionSlot)) MarkDirty();
    }

    public void RemoveTechnologySlot(GameTechnologySlot technologySlot) => technologySlots.Remove(technologySlot);

    public List<GameResourceSlot> GetAllResourceSlots() => new List<GameResourceSlot>(resourceSlots);

    // ===== MODIFIER SHORTCUTS =====

    /// <summary>
    /// Set a system-owned flat rate on one resource (population food demand, citizen research...).
    /// Positive is output per second, negative is consumption; 0 removes it.
    /// </summary>
    public void SetFlatRate(string resourceName, string source, float perSecond)
    {
        ResourceModifiers.SetFlat(resourceName, source, perSecond);
    }

    /// <summary>Remove every production modifier from <paramref name="source"/>.</summary>
    public void ClearAllModifiersFromSource(string source) => ResourceModifiers.RemoveSource(source);

    // ===== QUERIES =====

    public float GetNetProductionRate(string resourceName)
    {
        if (_dirty) Recalculate();
        return resourceName != null && _netRates.TryGetValue(resourceName, out float rate) ? rate : 0f;
    }

    public bool HasProductionData(string resourceName) => resourceName != null && _netRates.ContainsKey(resourceName);

    public readonly struct BreakdownLine
    {
        public readonly string label;
        public readonly float value;
        public readonly bool isPercent;

        public BreakdownLine(string label, float value, bool isPercent)
        {
            this.label = label;
            this.value = value;
            this.isPercent = isPercent;
        }
    }

    /// <summary>Every contribution to a resource's rate, for tooltips. Consumption lines are negative.</summary>
    public List<BreakdownLine> GetBreakdown(string resourceName)
    {
        var lines = new List<BreakdownLine>();
        var units = GameUnitsLogic.Instance;
        foreach (var slot in productionSlots)
        {
            var data = slot != null ? slot.productionUnitData : null;
            if (data == null) continue;
            float efficiency = units != null ? ModifierTargets.Resolve(units.ProductionEfficiency, slot.gameUnit).Percent : 0f;
            for (int i = 0; i < data.producedResources.Count && i < data.productionRates.Count; i++)
            {
                if (!string.Equals(data.producedResources[i], resourceName, System.StringComparison.OrdinalIgnoreCase)) continue;
                float rate = ProductionRules.UnitOutput(data.productionRates[i], slot.amount, efficiency);
                if (rate != 0f) lines.Add(new BreakdownLine(slot.gameUnit.name, rate, false));
            }
            for (int i = 0; i < data.consumedResources.Count && i < data.consumeRates.Count; i++)
            {
                if (!string.Equals(data.consumedResources[i], resourceName, System.StringComparison.OrdinalIgnoreCase)) continue;
                float rate = ProductionRules.UnitConsumption(data.consumeRates[i], slot.amount);
                if (rate != 0f) lines.Add(new BreakdownLine($"{slot.gameUnit.name} (Consumption)", -rate, false));
            }
        }

        float scaling = CalculateScalingBonus(resourceName);
        if (scaling != 0f) lines.Add(new BreakdownLine("Scaling bonuses", scaling, false));

        FillScopeKeys(resourceName);
        foreach (var key in _scratchKeys)
        {
            foreach (var source in ResourceModifiers.Sources(key))
            {
                string label = key == resourceName ? source.Key : $"{source.Key} ({ModifierTargets.Describe(key)})";
                if (source.Value.Flat != 0f) lines.Add(new BreakdownLine(label, source.Value.Flat, false));
                if (source.Value.Percent != 0f) lines.Add(new BreakdownLine(label, source.Value.Percent, true));
            }
        }

        int moraleDelta = StatManager.Instance != null ? Mathf.RoundToInt(StatManager.Instance.GetMoraleDeltaPercent()) : 0;
        if (moraleDelta != 0) lines.Add(new BreakdownLine("Morale", moraleDelta, true));
        return lines;
    }

    // ===== CALCULATION =====

    private void Recalculate()
    {
        _dirty = false;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        SubscribeToUnitLedgers();

        _outputRates.Clear();
        _consumptionRates.Clear();
        var units = GameUnitsLogic.Instance;

        foreach (var slot in productionSlots)
        {
            var data = slot != null ? slot.productionUnitData : null;
            if (data == null || slot.gameUnit == null) continue;
            float efficiency = units != null ? ModifierTargets.Resolve(units.ProductionEfficiency, slot.gameUnit).Percent : 0f;
            float activeUnits = slot.amount;
            for (int i = 0; i < data.producedResources.Count && i < data.productionRates.Count; i++)
            {
                Accumulate(_outputRates, data.producedResources[i], ProductionRules.UnitOutput(data.productionRates[i], activeUnits, efficiency));
            }
            for (int i = 0; i < data.consumedResources.Count && i < data.consumeRates.Count; i++)
            {
                Accumulate(_consumptionRates, data.consumedResources[i], ProductionRules.UnitConsumption(data.consumeRates[i], activeUnits));
            }
        }

        float moraleDelta = StatManager.Instance != null ? Mathf.Round(StatManager.Instance.GetMoraleDeltaPercent()) : 0f;
        foreach (var slot in resourceSlots)
        {
            if (slot == null || slot.gameUnit == null) continue;
            string resource = slot.gameUnit.name;

            var rate = new ProductionRules.Rate
            {
                output = Read(_outputRates, resource) + CalculateScalingBonus(resource),
                consumption = Read(_consumptionRates, resource),
                percent = moraleDelta
            };

            FillScopeKeys(resource, slot.gameUnit.section, slot.gameUnit.type);
            foreach (var key in _scratchKeys)
            {
                foreach (var source in ResourceModifiers.Sources(key))
                {
                    rate.AddFlat(source.Value.Flat);
                    rate.percent += source.Value.Percent;
                }
            }

            float net = rate.Net;
            _netRates[resource] = net;
            slot.productionRate = net;
        }
    }

    private static void Accumulate(Dictionary<string, float> table, string key, float amount)
    {
        if (string.IsNullOrEmpty(key) || amount == 0f) return;
        table.TryGetValue(key, out float current);
        table[key] = current + amount;
    }

    private static float Read(Dictionary<string, float> table, string key) => table.TryGetValue(key, out float value) ? value : 0f;

    private void FillScopeKeys(string resourceName, string section = null, string type = null)
    {
        _scratchKeys.Clear();
        _scratchKeys.Add(resourceName);
        if (section == null || type == null)
        {
            var unit = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(resourceName)?.gameUnit : null;
            if (unit == null && !GameCatalog.Resources.TryGet(resourceName, out unit)) RuntimeUnits.TryGet(resourceName, out unit);
            if (unit != null)
            {
                section ??= unit.section;
                type ??= unit.type;
            }
        }
        if (!string.IsNullOrEmpty(section)) _scratchKeys.Add(ModifierTargets.Section(section));
        if (!string.IsNullOrEmpty(type)) _scratchKeys.Add(ModifierTargets.Type(type));
        _scratchKeys.Add(ModifierTargets.All);
    }

    /// <summary>Output granted by production scaling rules ("+X resource per counted unit").</summary>
    private float CalculateScalingBonus(string resourceName)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return 0f;
        float total = 0f;
        foreach (var key in units.ProductionScaling.Targets)
        {
            if (!ProductionScalingKey.TryParse(key, out string resource, out string counter)) continue;
            if (!string.Equals(resource, resourceName, System.StringComparison.OrdinalIgnoreCase)) continue;
            float perUnit = units.ProductionScaling.Total(key).Flat;
            if (perUnit != 0f) total += perUnit * CountFor(counter, resourceName);
        }
        return total;
    }

    private float CountFor(string counter, string resourceName)
    {
        if (counter.StartsWith(ProductionScalingKey.ValuePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            string name = counter.Substring(ProductionScalingKey.ValuePrefix.Length);
            if (GameValues.IsKnownDomain(name)) return GameValues.Get(name, string.Empty);
            return StatDefinitions.IsKnown(name) ? GameValues.Get("stat", name) : 0f;
        }

        float count = 0f;
        bool producersOnly = counter == ProductionScalingKey.Producers;
        foreach (var slot in productionSlots)
        {
            if (slot == null || slot.gameUnit == null || slot.productionUnitData == null) continue;
            bool counts = producersOnly
                ? slot.productionUnitData.producedResources.Exists(r => string.Equals(r, resourceName, System.StringComparison.OrdinalIgnoreCase))
                : ModifierTargets.Covers(counter, slot.gameUnit.name, slot.gameUnit.section, slot.gameUnit.type);
            if (counts) count += Mathf.Round(slot.maxAmount);
        }
        return count;
    }

    /// <summary>Units whose inputs ran dry drop to reduced output until every consumed resource is available again.</summary>
    private void UpdateProductionSufficiency()
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return;
        foreach (var slot in productionSlots)
        {
            if (slot == null || slot.productionUnitData == null) continue;
            bool depleted = false;
            foreach (var resourceName in slot.productionUnitData.consumedResources)
            {
                var resourceSlot = units.GetResourceSlotFromName(resourceName);
                if (resourceSlot == null || resourceSlot.amount <= 0f)
                {
                    depleted = true;
                    break;
                }
            }
            if (slot.insufficientProduction != depleted)
            {
                slot.insufficientProduction = depleted;
                slot.UpdateMaxAmount();
                MarkDirty();
            }
        }
    }
}
