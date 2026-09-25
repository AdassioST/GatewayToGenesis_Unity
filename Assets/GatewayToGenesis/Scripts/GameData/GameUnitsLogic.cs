using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the three HUD tabs (storage, production, research) and every rule about units in them:
/// clicking resources, building production units, researching technologies and the per-unit
/// modifier ledgers (production efficiency, construction cost, click power, production scaling).
/// </summary>
public class GameUnitsLogic : SingletonBehaviour<GameUnitsLogic>
{
    private const LogChannel Log = LogChannel.Units;

    [SerializeField] public TabBuilderLogic storageTab;
    [SerializeField] public TabBuilderLogic productionTab;
    [SerializeField] public TabBuilderLogic researchTab;

    [SerializeField] private GameObject HUD, expandibleHUD, buildingMaterialButton;

    [Header("Research")]
    [Tooltip("Share of a technology's total cost paid per research tick.")]
    [SerializeField] private float researchShareProcessedPerTick = 0.1f;
    [SerializeField] private float researchTickSeconds = 1f;

    [Header("Construction")]
    [Tooltip("Construction cost modifiers can never push a cost below this share of its base.")]
    [SerializeField] private float minimumConstructionCostShare = 0.1f;

    [System.NonSerialized]
    public Dictionary<GameTechnologySlot, Dictionary<string, float>> technologyProgress = new Dictionary<GameTechnologySlot, Dictionary<string, float>>();

    public GameTechnologySlot activeTechnologySlot;

    /// <summary>Storage capacity contributions per resource, for tooltips ("Base", then each building).</summary>
    [System.NonSerialized]
    public Dictionary<string, Dictionary<string, float>> storageBreakdown = new Dictionary<string, Dictionary<string, float>>();

    /// <summary>Percent output of production units, keyed by unit name, "section:", "type:" or "*".</summary>
    public ModifierLedger ProductionEfficiency { get; } = new ModifierLedger();
    /// <summary>Percent construction cost of production units (positive = more expensive).</summary>
    public ModifierLedger ConstructionCost { get; } = new ModifierLedger();
    /// <summary>Flat and percent click power, keyed like resource targets.</summary>
    public ModifierLedger ClickPower { get; } = new ModifierLedger();
    /// <summary>Per-unit production bonuses, keyed by <see cref="ProductionScalingKey"/>.</summary>
    public ModifierLedger ProductionScaling { get; } = new ModifierLedger();

    // Permanent click upgrades (technologies, permanent event rewards) survive until the resource is discovered.
    private readonly Dictionary<string, float> _permanentClickPower = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _clickedTotals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GameTechnologySlot, List<float>> _adjustedTechCosts = new Dictionary<GameTechnologySlot, List<float>>();
    private Coroutine _researchCoroutine;

    /// <summary>Raised after a production unit is built (name, new count).</summary>
    public event Action<string, int> OnProductionUnitBuilt;

    private void Update()
    {
        if (InputUtils.DebugKeyDown(Key.M)) PrintAllModifiers();
    }

    // ===== LOOKUPS =====

    public GameResourceSlot GetResourceSlotFromName(string name) => storageTab != null ? storageTab.GetSlot<GameResourceSlot>(name) : null;

    public GameProductionSlot GetProductionSlot(string name) => productionTab != null ? productionTab.GetSlot<GameProductionSlot>(name) : null;

    public GameTechnologySlot GetTechnologySlot(string name) => researchTab != null ? researchTab.GetSlot<GameTechnologySlot>(name) : null;

    public int GetResourceAmount(string resourceName) => Mathf.RoundToInt(GetResourceAmountExact(resourceName));

    public float GetResourceAmountExact(string resourceName)
    {
        var slot = GetResourceSlotFromName(resourceName);
        return slot != null ? slot.amount : 0f;
    }

    /// <summary>Built count of a production unit, or of every unit in a section/type ("section:X", "type:Y").</summary>
    public float GetProductionUnitCount(string nameOrScope)
    {
        if (productionTab == null || string.IsNullOrEmpty(nameOrScope)) return 0f;
        var direct = GetProductionSlot(nameOrScope);
        if (direct != null) return direct.maxAmount;
        float total = 0f;
        foreach (var slotObject in productionTab.slots)
        {
            if (slotObject == null || !slotObject.TryGetComponent(out GameProductionSlot slot) || slot.gameUnit == null) continue;
            if (ModifierTargets.Covers(nameOrScope, slot.gameUnit.name, slot.gameUnit.section, slot.gameUnit.type)) total += slot.maxAmount;
        }
        return total;
    }

    public bool IsTechnologyUnlocked(string technologyName)
    {
        var slot = GetTechnologySlot(technologyName);
        return slot != null && slot.isUnlocked;
    }

    public float GetClickedTotal(string resourceName) => resourceName != null && _clickedTotals.TryGetValue(resourceName, out float total) ? total : 0f;

    public List<GameResourceSlot> GetAvailableResources()
    {
        var result = new List<GameResourceSlot>();
        if (storageTab == null) return result;
        foreach (var slot in storageTab.slots)
        {
            if (slot != null && slot.TryGetComponent(out GameResourceSlot resourceSlot)) result.Add(resourceSlot);
        }
        return result;
    }

    /// <summary>GameUnits of every production unit currently in the Production tab.</summary>
    public List<GameUnit> GetAvailableProductionUnits()
    {
        return productionTab != null ? new List<GameUnit>(productionTab.units) : new List<GameUnit>();
    }

    /// <summary>GameUnit by name: units already in a tab first, then the content catalog.</summary>
    public GameUnit GetGameUnitByName(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return null;
        foreach (var tab in new[] { storageTab, productionTab, researchTab })
        {
            if (tab == null) continue;
            foreach (var unit in tab.units)
            {
                if (unit != null && string.Equals(unit.name, unitName, StringComparison.OrdinalIgnoreCase)) return unit;
            }
        }
        return GameCatalog.Units.TryGet(unitName, out var found) ? found : null;
    }

    public Sprite GetGameUnitIconByName(string unitName) => GetGameUnitByName(unitName)?.icon;

    // ===== RESOURCES =====

    /// <summary>
    /// Add (or remove, when negative) an amount of a resource. With <paramref name="changeFromClickPower"/>
    /// the amount is the resource's click power. Unknown resources are discovered from the catalog first.
    /// </summary>
    public void ChangeResourceFromName(string name, float amount, bool changeFromClickPower)
    {
        var resourceSlot = GetResourceSlotFromName(name);
        if (resourceSlot == null)
        {
            var unit = GetGameUnitByName(name);
            if (unit == null || storageTab == null)
            {
                GameLog.Warning($"Resource '{name}' does not exist. Create its GameUnit under Resources/{GameCatalog.Resources.ResourcesPath}.", Log);
                return;
            }
            storageTab.AddNewUnit(unit);
            resourceSlot = GetResourceSlotFromName(name);
            if (resourceSlot == null)
            {
                GameLog.Error($"Storage tab could not create a slot for '{name}'.", Log);
                return;
            }
        }

        if (changeFromClickPower)
        {
            amount = GetEffectiveClickPower(resourceSlot);
            _clickedTotals.TryGetValue(name, out float clicked);
            _clickedTotals[name] = clicked + amount;
        }

        bool isFood = resourceSlot.gameUnit != null && resourceSlot.gameUnit.role == ResourceRole.Food;
        if (isFood && amount > 0f && PopGrowthLogic.Instance != null)
        {
            amount = Mathf.Min(amount, PopGrowthLogic.Instance.GetMaxUsefulFoodGain(resourceSlot.amount));
        }

        float oldAmount = resourceSlot.amount;
        resourceSlot.ChangeAmount(amount);

        if (isFood && PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.HandleExternalFoodChange(oldAmount, resourceSlot.amount);
        TooltipSystemLogic.Instance?.RefreshAllTooltips();
    }

    // ===== CLICK POWER =====

    /// <summary>
    /// (base + permanent upgrades + flat modifiers) × (1 + (percent modifiers + Click Power Bonus stat) / 100),
    /// never below the slot's base click power.
    /// </summary>
    public float GetEffectiveClickPower(GameResourceSlot slot)
    {
        if (slot == null || slot.gameUnit == null) return 0f;
        var modifiers = ModifierTargets.Resolve(ClickPower, slot.gameUnit);
        float statBonus = StatManager.Instance != null ? StatManager.Instance.GetDerivedValue("clickPowerBonus") : 0f;
        float value = (slot.baseClickPower + GetPermanentClickPower(slot.gameUnit.name) + modifiers.Flat) * (1f + (modifiers.Percent + statBonus) / 100f);
        return Mathf.Max(Mathf.Max(0.001f, slot.baseClickPower), value);
    }

    public float GetPermanentClickPower(string resourceName) => resourceName != null && _permanentClickPower.TryGetValue(resourceName, out float bonus) ? bonus : 0f;

    /// <summary>Permanently add click power to a resource (technology upgrades, permanent rewards).</summary>
    public void AddPermanentClickPower(string resourceName, float delta)
    {
        if (string.IsNullOrEmpty(resourceName) || delta == 0f) return;
        _permanentClickPower[resourceName] = GetPermanentClickPower(resourceName) + delta;
    }

    // ===== PRODUCTION UNITS =====

    /// <summary>
    /// Cost of the next unit for one build requirement:
    /// base × (1 + construction cost %) (floored at a share of base), × e^(costBalance / techTier × owned) for buildings.
    /// </summary>
    public float GetBuildCost(GameProductionSlot slot, int requirementIndex)
    {
        var data = slot != null ? slot.productionUnitData : null;
        if (data == null || requirementIndex < 0 || requirementIndex >= data.buildRequirementsAmount.Count) return 0f;
        float baseCost = data.buildRequirementsAmount[requirementIndex];
        float costPercent = GetConstructionCostPercent(slot.gameUnit);
        float cost = Mathf.Max(baseCost * (1f + costPercent / 100f), baseCost * minimumConstructionCostShare);
        if (slot.gameUnit.type != "Unit")
        {
            var production = GlobalProductionManager.Instance;
            float balance = production != null ? production.costBalance : 0.05f;
            float tier = production != null && production.techTier > 0f ? production.techTier : 1f;
            cost *= Mathf.Exp(balance / tier * slot.maxAmount);
        }
        return cost;
    }

    public float GetProductionEfficiencyPercent(GameUnit unit) => ModifierTargets.Resolve(ProductionEfficiency, unit).Percent;

    public float GetConstructionCostPercent(GameUnit unit) => ModifierTargets.Resolve(ConstructionCost, unit).Percent;

    public bool CanBuildProductionUnit(string productionUnitName)
    {
        var slot = GetProductionSlot(productionUnitName);
        var data = slot != null ? slot.productionUnitData : null;
        if (data == null) return false;
        for (int i = 0; i < data.buildResourceRequirements.Count; i++)
        {
            var resourceSlot = GetResourceSlotFromName(data.buildResourceRequirements[i]);
            if (resourceSlot == null || resourceSlot.amount < GetBuildCost(slot, i)) return false;
        }
        return true;
    }

    public bool BuildProductionUnit(string productionUnitName)
    {
        if (!CanBuildProductionUnit(productionUnitName)) return false;
        var slot = GetProductionSlot(productionUnitName);
        var data = slot.productionUnitData;

        // Price every requirement before paying, so the exponential cost uses the same owned count throughout.
        var costs = new float[data.buildResourceRequirements.Count];
        for (int i = 0; i < costs.Length; i++) costs[i] = GetBuildCost(slot, i);
        for (int i = 0; i < costs.Length; i++) ChangeResourceFromName(data.buildResourceRequirements[i], -costs[i], false);
        slot.incrementalCost = costs.Length > 0 ? costs[costs.Length - 1] : 0f;

        if (data.housing > 0 && PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.AddBaseHousing(data.housing, $"Building: {productionUnitName}");

        for (int i = 0; i < data.storageResources.Count && i < data.storageAmount.Count; i++)
        {
            AddStorageCapacity(data.storageResources[i], productionUnitName, data.storageAmount[i]);
        }

        ChangeProductionUnitFromName(productionUnitName, 1);

        if (data.satisfactionPoints != 0 && StatManager.Instance != null)
        {
            StatManager.Instance.ChangeSatisfactionPoints(data.satisfactionPoints, $"Building {productionUnitName}");
        }
        OnProductionUnitBuilt?.Invoke(productionUnitName, Mathf.RoundToInt(slot.maxAmount));
        return true;
    }

    private void AddStorageCapacity(string resourceName, string contributor, float amount)
    {
        var resourceSlot = GetResourceSlotFromName(resourceName);
        if (resourceSlot == null)
        {
            GameLog.Warning($"{contributor} adds storage to '{resourceName}', which is not discovered yet; the capacity is lost.", Log);
            return;
        }
        if (!storageBreakdown.TryGetValue(resourceName, out var breakdown))
        {
            breakdown = new Dictionary<string, float> { { "Base", resourceSlot.maxAmount } };
            storageBreakdown[resourceName] = breakdown;
        }
        resourceSlot.maxAmount += amount;
        resourceSlot.RefreshProductionAmount();
        breakdown.TryGetValue(contributor, out float current);
        breakdown[contributor] = current + amount;
    }

    /// <summary>Change how many of a production unit exist (events, scripts). Negative removes.</summary>
    public void ChangeProductionUnitFromName(string name, float amount)
    {
        var slot = GetProductionSlot(name);
        if (slot == null)
        {
            GameLog.Warning($"Production unit '{name}' is not in the Production tab.", Log);
            return;
        }
        slot.maxAmount = Mathf.Max(0f, slot.maxAmount + amount);
        TooltipSystemLogic.Instance?.RefreshAllTooltips();
    }

    // ===== RESEARCH =====

    public bool CanUnlockTechnology(string technologyName)
    {
        var slot = GetTechnologySlot(technologyName);
        var data = slot != null ? slot.technologyData : null;
        if (data == null || !ArePrerequisitesUnlocked(data)) return false;
        var costs = GetAdjustedTechCosts(slot);
        for (int i = 0; i < data.resourceRequirements.Count; i++)
        {
            float required = i < costs.Count ? costs[i] : data.resourceAmount[i];
            if (GetResourceAmountExact(data.resourceRequirements[i]) < required) return false;
        }
        return true;
    }

    public bool ArePrerequisitesUnlocked(TechnologyData data)
    {
        foreach (var requiredTech in data.techRequirements)
        {
            if (!IsTechnologyUnlocked(requiredTech)) return false;
        }
        return true;
    }

    /// <summary>Start (or switch to) researching a technology. Progress on other technologies is kept.</summary>
    public void StartTechnologyProgress(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null || technologySlot.isUnlocked || technologySlot.technologyData == null) return;
        if (!ArePrerequisitesUnlocked(technologySlot.technologyData)) return;

        if (activeTechnologySlot != null && activeTechnologySlot != technologySlot) activeTechnologySlot.alreadyClicked = false;
        if (_researchCoroutine != null) StopCoroutine(_researchCoroutine);

        activeTechnologySlot = technologySlot;
        if (!technologyProgress.ContainsKey(technologySlot))
        {
            technologyProgress[technologySlot] = technologySlot.technologyData.resourceRequirements.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(r => r, _ => 0f, StringComparer.OrdinalIgnoreCase);
        }
        GetAdjustedTechCosts(technologySlot);
        technologySlot.alreadyClicked = true;
        _researchCoroutine = StartCoroutine(ProcessTechnologyProgress(technologySlot));
    }

    private IEnumerator ProcessTechnologyProgress(GameTechnologySlot technologySlot)
    {
        var data = technologySlot.technologyData;
        var progress = technologyProgress[technologySlot];
        var required = GetAdjustedTechCosts(technologySlot);
        var wait = new WaitForSeconds(researchTickSeconds);

        while (!technologySlot.isUnlocked)
        {
            if (TimeSystemLogic.Instance != null && TimeSystemLogic.Instance.isTimePaused)
            {
                yield return null;
                continue;
            }

            bool complete = true;
            for (int i = 0; i < data.resourceRequirements.Count; i++)
            {
                string resourceName = data.resourceRequirements[i];
                float requiredAmount = i < required.Count ? required[i] : data.resourceAmount[i];
                float remaining = requiredAmount - progress[resourceName];
                if (remaining > 0f)
                {
                    var resourceSlot = GetResourceSlotFromName(resourceName);
                    if (resourceSlot != null)
                    {
                        float paid = Mathf.Min(resourceSlot.amount, remaining, requiredAmount * researchShareProcessedPerTick);
                        if (paid > 0f)
                        {
                            resourceSlot.ChangeAmount(-paid);
                            progress[resourceName] += paid;
                        }
                    }
                }
                if (progress[resourceName] < requiredAmount) complete = false;
            }

            float total = Mathf.Max(0.0001f, required.Sum());
            if (technologySlot.enlightenedCompleted && !technologySlot.enlightenedBonusApplied && technologySlot.enlightenedBonusPercent > 0f)
            {
                // Enlightened technologies receive a one-off share of their total cost, spread by requirement weight.
                float bonus = total * Mathf.Clamp01(technologySlot.enlightenedBonusPercent);
                for (int i = 0; i < data.resourceRequirements.Count; i++)
                {
                    float requiredAmount = i < required.Count ? required[i] : data.resourceAmount[i];
                    if (requiredAmount <= 0f) continue;
                    string resourceName = data.resourceRequirements[i];
                    progress[resourceName] = Mathf.Min(requiredAmount, progress[resourceName] + bonus * (requiredAmount / total));
                }
                technologySlot.enlightenedBonusApplied = true;
                complete = data.resourceRequirements.Select((r, i) => progress[r] >= (i < required.Count ? required[i] : data.resourceAmount[i])).All(done => done);
            }

            technologySlot.researchProgress = Mathf.Clamp01(progress.Values.Sum() / total);
            technologySlot.UpdateProgressUI();

            if (complete)
            {
                technologySlot.UnlockTechnology();
                technologyProgress.Remove(technologySlot);
                _adjustedTechCosts.Remove(technologySlot);
                if (activeTechnologySlot == technologySlot) activeTechnologySlot = null;
                _researchCoroutine = null;
                TooltipSystemLogic.Instance?.RefreshAllTooltips();
                yield break;
            }
            yield return wait;
        }
        _researchCoroutine = null;
    }

    /// <summary>
    /// Research cost per requirement after Discovery Efficiency (reduction rounded to whole units).
    /// Frozen once research starts, so progress and tooltips stay consistent.
    /// </summary>
    public List<float> GetAdjustedTechCosts(GameTechnologySlot techSlot)
    {
        if (techSlot == null || techSlot.technologyData == null) return new List<float>();
        if (_adjustedTechCosts.TryGetValue(techSlot, out var cached)) return cached;

        float efficiency = StatManager.Instance != null ? StatManager.Instance.GetDiscoveryEfficiencyCapped() : 0f;
        var adjusted = new List<float>(techSlot.technologyData.resourceAmount.Count);
        foreach (float baseCost in techSlot.technologyData.resourceAmount)
        {
            adjusted.Add(Mathf.Max(0f, baseCost - Mathf.RoundToInt(baseCost * efficiency)));
        }
        if (technologyProgress.ContainsKey(techSlot)) _adjustedTechCosts[techSlot] = adjusted;
        return adjusted;
    }

    // ===== TECHNOLOGY UNLOCKABLES =====

    public void HandleTechUnlockable(TechUnlockable unlockable, GameTechnologySlot techSlot)
    {
        if (unlockable == null) return;
        string techName = techSlot != null && techSlot.gameUnit != null ? techSlot.gameUnit.name : "Technology";
        string unitName = unlockable.gameUnit != null ? unlockable.gameUnit.name : null;

        switch (unlockable.unlockableType)
        {
            case TechUnlockableType.ClickPower:
                if (unlockable.resourceModifier > 0f) AddPermanentClickPower(unitName, unlockable.resourceModifier);
                else if (unlockable.gameUnit != null) storageTab.AddNewUnit(unlockable.gameUnit);
                break;

            case TechUnlockableType.Building:
            case TechUnlockableType.Unit:
                if (unlockable.gameUnit != null) productionTab.AddNewUnit(unlockable.gameUnit);
                break;

            case TechUnlockableType.Modifier:
                GlobalProductionManager.Instance?.ResourceModifiers.Add(unitName, $"Technology: {techName}", new ModifierValue(0f, unlockable.resourceModifier));
                break;

            case TechUnlockableType.DemandModifier:
                PopGrowthLogic.Instance?.ReduceFoodDemand(unlockable.resourceModifier / 100f);
                break;

            case TechUnlockableType.Arts:
                GameLog.Event($"Arts unit {unitName} unlocked.", Log);
                break;

            case TechUnlockableType.Special:
                HandleSpecialUnlockable(unlockable);
                break;

            default:
                GameLog.Error($"Unhandled unlockable type {unlockable.unlockableType} on '{unlockable.name}'.", Log);
                break;
        }
    }

    // Special unlockables are named hooks into scene objects; add new ones here.
    private void HandleSpecialUnlockable(TechUnlockable unlockable)
    {
        switch (unlockable.name)
        {
            case "Vagrants":
                if (PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.allowVagrants = true;
                break;
            case "Horology":
                TimeSystemLogic.Instance.canTrackTime = true;
                if (HUD != null && HUD.activeSelf) TimeSystemLogic.Instance.PauseTime(false);
                if (expandibleHUD != null) expandibleHUD.SetActive(true);
                break;
            case "Building Material Button":
                if (buildingMaterialButton != null) buildingMaterialButton.SetActive(true);
                break;
            default:
                if (unlockable.gameUnit != null && GameCatalog.IsProductionUnit(unlockable.gameUnit.name))
                {
                    // A production unit tagged Special still becomes buildable.
                    productionTab.AddNewUnit(unlockable.gameUnit);
                    GameLog.Warning($"Special unlockable '{unlockable.name}' is a production unit; set its type to Building or Unit.", Log);
                }
                else
                {
                    GameLog.Warning($"Special unlockable '{unlockable.name}' has no handler in GameUnitsLogic.HandleSpecialUnlockable.", Log);
                }
                break;
        }
    }

    // ===== FORMATTING & DEBUG =====

    private static readonly string[] MagnitudeSuffixes =
    {
        "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "O", "N", "D", "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Od", "Nd",
        "V", "Uv", "Dv", "Tv", "Qav", "Qiv", "Sxv", "Spv", "Ov", "Nv", "Tr", "Ut", "Dt", "G"
    };

    /// <summary>Compact number for the HUD: 1234 → "1.23K", -2500000 → "-2.5M".</summary>
    public string FormatValue(float value)
    {
        float magnitude = Mathf.Abs(value);
        int index = 0;
        while (magnitude >= 1000f && index < MagnitudeSuffixes.Length - 1)
        {
            magnitude /= 1000f;
            index++;
        }
        return $"{(value < 0f ? "-" : "")}{magnitude:0.##}{MagnitudeSuffixes[index]}";
    }

    [ContextMenu("Print All Modifiers")]
    public void PrintAllModifiers()
    {
        if (!GameLog.IsEnabled(Log)) return;
        PrintLedger("Production efficiency (%)", ProductionEfficiency);
        PrintLedger("Construction cost (%)", ConstructionCost);
        PrintLedger("Click power", ClickPower);
        PrintLedger("Production scaling (per unit)", ProductionScaling);
    }

    private static void PrintLedger(string title, ModifierLedger ledger)
    {
        GameLog.Event($"=== {title} ===", Log);
        foreach (var target in ledger.Targets)
        {
            foreach (var source in ledger.Sources(target))
            {
                GameLog.Event($"  {ModifierTargets.Describe(target)}: {source.Value.Flat:+0.##;-0.##;0} / {source.Value.Percent:+0.##;-0.##;0}% from {source.Key}", Log);
            }
        }
    }
}
