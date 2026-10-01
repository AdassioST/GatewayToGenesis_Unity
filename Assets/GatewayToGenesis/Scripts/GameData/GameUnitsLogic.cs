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

    /// <summary>A technology by name; an old name of a renamed technology finds it too (<see cref="TechnologyAliases"/>).</summary>
    public GameTechnologySlot GetTechnologySlot(string name)
    {
        if (researchTab == null) return null;
        var slot = researchTab.GetSlot<GameTechnologySlot>(name);
        return slot != null || !TechnologyAliases.IsOldName(name) ? slot : researchTab.GetSlot<GameTechnologySlot>(TechnologyAliases.Resolve(name));
    }

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

    /// <summary>Technologies researched so far (every tree).</summary>
    public int CountUnlockedTechnologies()
    {
        if (researchTab == null) return 0;
        int count = 0;
        foreach (var slotObject in researchTab.slots)
            if (slotObject != null && slotObject.TryGetComponent(out GameTechnologySlot slot) && slot.isUnlocked) count++;
        return count;
    }

    /// <summary>Technologies that could be researched now: not yet researched, prerequisites met.</summary>
    public List<GameTechnologySlot> ResearchableTechnologies()
    {
        var result = new List<GameTechnologySlot>();
        if (researchTab == null) return result;
        foreach (var slotObject in researchTab.slots)
            if (slotObject != null && slotObject.TryGetComponent(out GameTechnologySlot slot) && !slot.isUnlocked && slot.technologyData != null && ArePrerequisitesUnlocked(slot.technologyData))
                result.Add(slot);
        return result;
    }

    /// <summary>The resource the active research is waiting on (none stored and some still owed), or null.</summary>
    public string ResearchStalledOn()
    {
        var slot = activeTechnologySlot;
        if (slot == null || slot.isUnlocked || slot.technologyData == null || !technologyProgress.TryGetValue(slot, out var progress)) return null;
        var required = GetAdjustedTechCosts(slot);
        var data = slot.technologyData;
        for (int i = 0; i < data.resourceRequirements.Count; i++)
        {
            string resource = data.resourceRequirements[i];
            float need = (i < required.Count ? required[i] : data.resourceAmount[i]) - (progress.TryGetValue(resource, out float paid) ? paid : 0f);
            if (need > 0.01f && GetResourceAmountExact(resource) < 0.01f) return resource;
        }
        return null;
    }

    /// <summary>Every production unit built, of every kind.</summary>
    public float CountAllBuildings()
    {
        if (productionTab == null) return 0f;
        float total = 0f;
        foreach (var slotObject in productionTab.slots)
            if (slotObject != null && slotObject.TryGetComponent(out GameProductionSlot slot)) total += slot.maxAmount;
        return total;
    }

    /// <summary>Everything in storage, summed across resources.</summary>
    public float TotalStoredResources()
    {
        float total = 0f;
        foreach (var slot in GetAvailableResources()) total += slot.amount;
        return total;
    }

    /// <summary>Some resource sits at its capacity.</summary>
    public bool AnyResourceFull()
    {
        foreach (var slot in GetAvailableResources())
            if (slot.maxAmount > 0f && slot.amount >= slot.maxAmount - 0.001f) return true;
        return false;
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
        return GameCatalog.Units.TryGet(unitName, out var found) || RuntimeUnits.TryGet(unitName, out found) ? found : null;
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
        return ProductionRules.ClickPower(slot.baseClickPower, GetPermanentClickPower(slot.gameUnit.name), modifiers, statBonus);
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
        var production = GlobalProductionManager.Instance;
        // Repeated homes and food facilities retain their material cost. An exponential price is not a physical population limit.
        string foodName = GameCatalog.ResourceNameFor(ResourceRole.Food);
        bool isBuilding = slot.gameUnit.type != "Unit" && data.housing <= 0 && !data.producedResources.Contains(foodName); // units cost the same however many you own
        return ProductionRules.BuildCost(baseCost, costPercent, minimumConstructionCostShare, isBuilding,
            production != null ? production.costBalance : 0.05f, production != null ? production.techTier : 1f, slot.maxAmount);
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

    // ----- The research plan -----

    /// <summary>
    /// The research plan: technologies to research in order, every prerequisite before what needs it
    /// (<see cref="TechTreeRules"/>). The active research is always the plan's first technology that can be researched
    /// now, so finishing one moves straight on to the next. Saved with the save document
    /// (<see cref="SaveDocument.researchPlan"/>) rather than the system snapshot, so older saves still load.
    /// </summary>
    [System.NonSerialized] private List<string> _researchPlan = new List<string>();

    /// <summary>The research plan or the active research changed.</summary>
    public event Action ResearchPlanChanged;

    public IReadOnlyList<string> ResearchPlan => _researchPlan;

    private static IEnumerable<string> Prerequisites(string technology) => TechnologyTreeLogic.Prerequisites(technology);

    private bool IsAvailable(string technology) => TechTreeRules.IsAvailable(technology, Prerequisites, IsTechnologyUnlocked);

    /// <summary>
    /// A click on a technology: research it now, or plan the way to it (every prerequisite still missing, in order) and
    /// start on the first step. <paramref name="append"/> (Shift) adds the way after the plan already made instead of
    /// replacing it. Research under way that is on the new way carries on; progress on every technology is always kept.
    /// Clicking the research under way changes nothing.
    /// </summary>
    public void PlanResearch(GameTechnologySlot technologySlot, bool append)
    {
        if (technologySlot == null || technologySlot.isUnlocked || technologySlot.gameUnit == null) return;
        if (technologySlot == activeTechnologySlot && !append) return;
        var path = TechTreeRules.PlanTo(technologySlot.gameUnit.name, Prerequisites, IsTechnologyUnlocked);
        if (path.Count == 0) return;
        string active = activeTechnologySlot != null && activeTechnologySlot.gameUnit != null ? activeTechnologySlot.gameUnit.name : null;
        _researchPlan = TechTreeRules.Merge(_researchPlan, path, append, active, IsAvailable);
        SyncResearch();
    }

    /// <summary>Take a technology, and everything planned that needs it, out of the plan (a right click). Its progress is kept.</summary>
    public void RemoveFromPlan(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null || technologySlot.gameUnit == null) return;
        if (TechTreeRules.PlanPosition(_researchPlan, technologySlot.gameUnit.name) == 0) return;
        _researchPlan = TechTreeRules.Without(_researchPlan, technologySlot.gameUnit.name, Prerequisites);
        SyncResearch();
    }

    /// <summary>Start (or switch to) researching a technology now, first in the plan. Progress on other technologies is kept.</summary>
    public void StartTechnologyProgress(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null || technologySlot.isUnlocked || technologySlot.technologyData == null || technologySlot.gameUnit == null) return;
        if (!ArePrerequisitesUnlocked(technologySlot.technologyData)) return;
        string name = technologySlot.gameUnit.name;
        _researchPlan.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        _researchPlan.Insert(0, name);
        SyncResearch();
    }

    /// <summary>Carry on with the plan after a save is loaded (restoring never starts anything by itself).</summary>
    public void ResumeResearch()
    {
        if (activeTechnologySlot != null && activeTechnologySlot.gameUnit != null && TechTreeRules.PlanPosition(_researchPlan, activeTechnologySlot.gameUnit.name) == 0)
            _researchPlan.Insert(0, activeTechnologySlot.gameUnit.name);
        SyncResearch();
    }

    /// <summary>The plan read from a save. Nothing starts here: loading never replays anything (<see cref="ResumeResearch"/> does).</summary>
    public void RestoreResearchPlan(IEnumerable<string> plan)
    {
        _researchPlan = plan != null ? plan.Where(n => !string.IsNullOrWhiteSpace(n)).Select(TechnologyAliases.Resolve).ToList() : new List<string>();
        ResearchPlanChanged?.Invoke();
    }

    /// <summary>
    /// A technology was researched, whether by its research, a story or a test: its research is done with, it leaves the
    /// plan and the plan moves on. Called by <see cref="GameTechnologySlot.UnlockTechnology"/>.
    /// </summary>
    public void OnTechnologyResearched(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null) return;
        technologyProgress.Remove(technologySlot);
        _adjustedTechCosts.Remove(technologySlot);
        technologySlot.alreadyClicked = false;
        if (activeTechnologySlot == technologySlot) StopResearch();
        // A technology already paid in full (an Enlightenment on top of earlier progress) is researched once its way is.
        foreach (var paid in technologyProgress.Keys.ToList())
        {
            if (paid != null && !paid.isUnlocked && paid.technologyData != null && ArePrerequisitesUnlocked(paid.technologyData) && IsPaid(paid))
            {
                paid.UnlockTechnology();
                return; // its own call moves the plan on
            }
        }
        SyncResearch();
    }

    // The active research follows the plan: its first technology that can be researched now (none when it is empty).
    private void SyncResearch()
    {
        _researchPlan.RemoveAll(n => string.IsNullOrWhiteSpace(n) || IsTechnologyUnlocked(n) || GetTechnologySlot(n) == null);
        string next = TechTreeRules.NextInPlan(_researchPlan, Prerequisites, IsTechnologyUnlocked);
        var slot = next != null ? GetTechnologySlot(next) : null;
        if (slot == null) StopResearch();
        else if (slot != activeTechnologySlot || _researchCoroutine == null) BeginResearch(slot);
        ResearchPlanChanged?.Invoke();
        TooltipSystemLogic.Instance?.RefreshAllTooltips();
    }

    private void StopResearch()
    {
        if (_researchCoroutine != null) StopCoroutine(_researchCoroutine);
        _researchCoroutine = null;
        if (activeTechnologySlot != null) activeTechnologySlot.alreadyClicked = false;
        activeTechnologySlot = null;
    }

    private void BeginResearch(GameTechnologySlot technologySlot)
    {
        if (technologySlot == null || technologySlot.isUnlocked || technologySlot.technologyData == null) return;
        if (!ArePrerequisitesUnlocked(technologySlot.technologyData)) return;

        StopResearch();
        activeTechnologySlot = technologySlot;
        ProgressOf(technologySlot);
        GetAdjustedTechCosts(technologySlot); // frozen from here on
        // Enlightened in an older save, before its gift was paid on the spot.
        if (technologySlot.enlightenedCompleted) ApplyEnlightenedBonus(technologySlot);
        technologySlot.alreadyClicked = true;
        _researchCoroutine = StartCoroutine(ProcessTechnologyProgress(technologySlot));
    }

    // Research paid so far per requirement, made on first use (research starting, or an Enlightenment's gift).
    private Dictionary<string, float> ProgressOf(GameTechnologySlot technologySlot)
    {
        if (!technologyProgress.TryGetValue(technologySlot, out var progress))
        {
            progress = technologySlot.technologyData.resourceRequirements.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(r => r, _ => 0f, StringComparer.OrdinalIgnoreCase);
            technologyProgress[technologySlot] = progress;
        }
        return progress;
    }

    private static float RequiredAt(TechnologyData data, List<float> required, int index) =>
        index < required.Count ? required[index] : index < data.resourceAmount.Count ? data.resourceAmount[index] : 0f;

    private static float Paid(Dictionary<string, float> progress, string resource) => progress != null && progress.TryGetValue(resource, out float paid) ? paid : 0f;

    /// <summary>Every requirement of a technology's research is paid.</summary>
    private bool IsPaid(GameTechnologySlot technologySlot)
    {
        var data = technologySlot.technologyData;
        technologyProgress.TryGetValue(technologySlot, out var progress);
        var required = GetAdjustedTechCosts(technologySlot);
        for (int i = 0; i < data.resourceRequirements.Count; i++)
        {
            if (Paid(progress, data.resourceRequirements[i]) < RequiredAt(data, required, i) - 0.0001f) return false;
        }
        return true;
    }

    private void UpdateResearchProgress(GameTechnologySlot technologySlot)
    {
        float total = Mathf.Max(0.0001f, GetAdjustedTechCosts(technologySlot).Sum());
        technologyProgress.TryGetValue(technologySlot, out var progress);
        technologySlot.researchProgress = Mathf.Clamp01((progress != null ? progress.Values.Sum() : 0f) / total);
        technologySlot.UpdateProgressUI();
    }

    private IEnumerator ProcessTechnologyProgress(GameTechnologySlot technologySlot)
    {
        var data = technologySlot.technologyData;
        var progress = ProgressOf(technologySlot);
        var required = GetAdjustedTechCosts(technologySlot);
        var wait = new WaitForSeconds(researchTickSeconds);

        while (!technologySlot.isUnlocked)
        {
            if (TimeSystemLogic.Instance != null && TimeSystemLogic.Instance.IsStopped)
            {
                yield return null;
                continue;
            }

            for (int i = 0; i < data.resourceRequirements.Count; i++)
            {
                string resourceName = data.resourceRequirements[i];
                float requiredAmount = RequiredAt(data, required, i);
                float remaining = requiredAmount - Paid(progress, resourceName);
                if (remaining <= 0f) continue;
                var resourceSlot = GetResourceSlotFromName(resourceName);
                if (resourceSlot == null) continue;
                float paid = Mathf.Min(resourceSlot.amount, remaining, requiredAmount * researchShareProcessedPerTick);
                if (paid <= 0f) continue;
                resourceSlot.ChangeAmount(-paid);
                progress[resourceName] = Paid(progress, resourceName) + paid;
            }

            UpdateResearchProgress(technologySlot);

            if (IsPaid(technologySlot))
            {
                // This coroutine ends here; researching it moves the plan on and starts the next one's own.
                _researchCoroutine = null;
                technologySlot.UnlockTechnology();
                TooltipSystemLogic.Instance?.RefreshAllTooltips();
                yield break;
            }
            yield return wait;
        }
        if (activeTechnologySlot == technologySlot) _researchCoroutine = null;
    }

    // ----- Enlightenment -----

    /// <summary>
    /// Enlighten a technology (its Enlightenment goals were met, or a story enlightened it): it is uncovered in its tree at
    /// once, even before its prerequisites, and <see cref="GameTechnologySlot.enlightenedBonusPercent"/> of every research
    /// requirement is paid on the spot. A technology that can be researched now and is fully paid by the gift is
    /// researched at once. False when it was researched or enlightened already.
    /// </summary>
    public bool EnlightenTechnology(GameTechnologySlot technologySlot, string reason)
    {
        if (technologySlot == null || technologySlot.gameUnit == null || technologySlot.isUnlocked || technologySlot.enlightenedCompleted) return false;
        if (technologySlot.technologyData == null) return false;

        technologySlot.enlightenedCompleted = true;
        ApplyEnlightenedBonus(technologySlot);
        technologySlot.RefreshTechnologyUI();
        GameLog.Event($"{technologySlot.gameUnit.name} enlightened{(string.IsNullOrEmpty(reason) ? string.Empty : ": " + reason)}", Log);
        technologySlot.NotifyEnlightened(reason);

        if (IsPaid(technologySlot) && ArePrerequisitesUnlocked(technologySlot.technologyData)) technologySlot.UnlockTechnology();
        TooltipSystemLogic.Instance?.RefreshAllTooltips();
        return true;
    }

    public bool EnlightenTechnology(string technologyName, string reason) => EnlightenTechnology(GetTechnologySlot(technologyName), reason);

    /// <summary>Technologies enlightened so far, researched since or not (every tree).</summary>
    public int CountEnlightenedTechnologies()
    {
        if (researchTab == null) return 0;
        int count = 0;
        foreach (var slotObject in researchTab.slots)
            if (slotObject != null && slotObject.TryGetComponent(out GameTechnologySlot slot) && slot.enlightenedCompleted) count++;
        return count;
    }

    // An Enlightenment's gift: a share of every research requirement, paid at once and only once per technology.
    private void ApplyEnlightenedBonus(GameTechnologySlot technologySlot)
    {
        if (technologySlot.enlightenedBonusApplied || technologySlot.technologyData == null) return;
        var data = technologySlot.technologyData;
        var progress = ProgressOf(technologySlot);
        var required = GetAdjustedTechCosts(technologySlot);
        float share = Mathf.Clamp01(technologySlot.enlightenedBonusPercent);
        for (int i = 0; i < data.resourceRequirements.Count; i++)
        {
            string resource = data.resourceRequirements[i];
            float need = RequiredAt(data, required, i);
            progress[resource] = Mathf.Min(need, Paid(progress, resource) + need * share);
        }
        technologySlot.enlightenedBonusApplied = true;
        UpdateResearchProgress(technologySlot);
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
            adjusted.Add(ProductionRules.ResearchCost(baseCost, efficiency));
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

            case TechUnlockableType.CouncilSeat:
                var government = GovernmentLogic.Instance;
                for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(unlockable.resourceModifier)) && government != null; i++) government.UnlockNextCouncilSeat();
                break;

            case TechUnlockableType.Arts:
                GameLog.Event($"Arts unit {unitName} unlocked.", Log);
                break;

            case TechUnlockableType.Special:
                HandleSpecialUnlockable(unlockable);
                break;

            // The Grimoire reads researched technologies itself (Grimoire.Current), so a load needs nothing here.
            case TechUnlockableType.GrimoireSeat:
            case TechUnlockableType.SymphonyCard:
            case TechUnlockableType.SpellWildcard:
                GameLog.Event($"Grimoire: {unlockable.name} ({techName}). {unlockable.effects}", Log);
                break;

            // Once, when researched: a load never replays it (restoration grants nothing).
            case TechUnlockableType.ScoreChange:
                if (!string.IsNullOrWhiteSpace(unlockable.score) && EventSystemLogic.Instance != null)
                    EventSystemLogic.Instance.ModifyEventScore(unlockable.score.Trim(), Mathf.RoundToInt(unlockable.resourceModifier));
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
            // Map units: the world reads the technology itself (WorldSettings.mapTechnology, units, expeditions); the card only announces them.
            case "Expeditions":
            case "Builders":
            case "Settlers":
            // The First Lean Season: Echoes of Hunger, the Act I pivot crisis (a tease of the Hunger, not the Age Crisis).
            // The Age asset does the work: it opens Act II, whose Act of Fate story tells it.
            case "The First Lean Season":
                GameLog.Event($"{unlockable.name}: {unlockable.effects}", Log);
                break;
            // Placeholder effects from the Act I tree sheet (Sept 27, 2026): their systems do not exist yet.
            case "Resource Shed":
            case "Resource Shed Capacity":
            case "Innovation Sanctums":
            case "Keynote Relics":
            case "Vital Winds":
            case "Resource Capacity":
            // Placeholders from the Act I rework (Docs/Planning/TECH_TREE_ACT_I.md): their systems do not exist yet.
            case "Bitter Roots Foraging":
            case "Earth-Beans Planting":
            case "Trapping Lines":
            case "Midwives":
            case "Wild Honey Stores":
            case "Old Roads Reclaimed":
            case "Moonlit Vigil Civic":
            case "Sky Glass Burials Civic":
            case "The Rhythm Ritual":
            case "Awakened Major Unison":
            case "Flow State":
                GameLog.Event($"{unlockable.name} (placeholder, no effect yet): {unlockable.effects}", Log);
                break;
            case "Building Material Button":
                if (buildingMaterialButton != null) buildingMaterialButton.SetActive(true);
                break;
            // The Bestiary: its HUD button reads the researched technology itself (BestiaryHud.Unlocked), so a load needs nothing here.
            case SpeciesKnowledge.BestiaryUnlock:
                GameLog.Event($"{unlockable.name}: {unlockable.effects}", Log);
                break;
            // Creature Studies: understanding reads the researched technology itself (SpeciesLoreKeeper.Understands).
            case SpeciesLore.StudiesUnlock:
                GameLog.Event($"{unlockable.name}: {unlockable.effects}", Log);
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
