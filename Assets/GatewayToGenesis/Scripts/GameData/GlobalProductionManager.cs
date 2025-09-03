using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GlobalProductionManager : MonoBehaviour
{
    public static GlobalProductionManager Instance { get; private set; }

    public List<GameResourceSlot> resourceSlots = new List<GameResourceSlot>();
    public List<GameProductionSlot> productionSlots = new List<GameProductionSlot>();
    public List<GameTechnologySlot> technologySlots = new List<GameTechnologySlot>();

    // Dictionaries to store global values for each resource type
    public Dictionary<string, float> netProductionRates = new Dictionary<string, float>();

    public Dictionary<string, float> positiveModifiers = new Dictionary<string, float>();
    public Dictionary<string, float> negativeModifiers = new Dictionary<string, float>();

    public Dictionary<string, float> persistentPositiveModifiers = new Dictionary<string, float>();
    public Dictionary<string, float> persistentNegativeModifiers = new Dictionary<string, float>();
    public Dictionary<string, float> percentagePositiveModifiers = new Dictionary<string, float>();
    public Dictionary<string, float> percentageNegativeModifiers = new Dictionary<string, float>();

    public Dictionary<string, List<string>> modifierSourceDict = new Dictionary<string, List<string>>();

    // Per-resource percentage modifiers tracked by source (event/tech/etc.).
    // Bonuses and maluses are tracked separately so a single source can contribute both.
    // Values are positive magnitudes. Only ONE entry per (resource, source) is stored per polarity.
    private Dictionary<string, Dictionary<string, float>> percentageBonusBySource = new Dictionary<string, Dictionary<string, float>>();
    private Dictionary<string, Dictionary<string, float>> percentageMalusBySource = new Dictionary<string, Dictionary<string, float>>();

    // Persistent flat modifiers tracked by source
    private Dictionary<string, Dictionary<string, float>> persistentBonusBySource = new Dictionary<string, Dictionary<string, float>>();
    private Dictionary<string, Dictionary<string, float>> persistentMalusBySource = new Dictionary<string, Dictionary<string, float>>();

    public float techTier = 1f, costBalance = 0.05f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GlobalProductionManager found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        //MOVE EVENTUALLY FROM HERE TO THE FUTURE GAME STATE MANAGER LOAD/ SAVES
        SectionData.InitializeSectionDataDictionary();
    }

    private void Start()
    {
        InitializeProductionRates();
        
        // Ensure all slots are registered after initialization
        StartCoroutine(EnsureSlotsRegisteredAfterStart());
    }
    
    private IEnumerator EnsureSlotsRegisteredAfterStart()
    {
        yield return null;
        EnsureAllSlotsRegistered();
    }

    private void Update()
    {
        // Pause production changes during active events
        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive())
            return;
            
        CalculateGlobalProductionRates();
        DisableProductionUnitsIfResourceDepleted();
        EnableProductionUnitsIfResourcesSufficient();
    }

    private void InitializeProductionRates()
    {
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            if (!netProductionRates.ContainsKey(resourceName))
            {
                netProductionRates[resourceName] = 0f;
                positiveModifiers[resourceName] = 0f;
                negativeModifiers[resourceName] = 0f;
                persistentPositiveModifiers[resourceName] = 0f;
                persistentNegativeModifiers[resourceName] = 0f;
                percentagePositiveModifiers[resourceName] = 0f;
                percentageNegativeModifiers[resourceName] = 0f;
                if (!percentageBonusBySource.ContainsKey(resourceName)) percentageBonusBySource[resourceName] = new Dictionary<string, float>();
                if (!percentageMalusBySource.ContainsKey(resourceName)) percentageMalusBySource[resourceName] = new Dictionary<string, float>();
                if (!persistentBonusBySource.ContainsKey(resourceName)) persistentBonusBySource[resourceName] = new Dictionary<string, float>();
                if (!persistentMalusBySource.ContainsKey(resourceName)) persistentMalusBySource[resourceName] = new Dictionary<string, float>();
            }
        }
    }

    public void AddResourceSlot(GameResourceSlot resourceSlot)
    {
        if (!resourceSlots.Contains(resourceSlot))
        {
            resourceSlots.Add(resourceSlot);

            string resourceName = resourceSlot.gameUnit.name;

            if (!netProductionRates.ContainsKey(resourceName))
            {
                netProductionRates[resourceName] = 0f;
                positiveModifiers[resourceName] = 0f;
                negativeModifiers[resourceName] = 0f;
                persistentPositiveModifiers[resourceName] = 0f;
                persistentNegativeModifiers[resourceName] = 0f;
                percentagePositiveModifiers[resourceName] = 0f;
                percentageNegativeModifiers[resourceName] = 0f;
                if (!percentageBonusBySource.ContainsKey(resourceName)) percentageBonusBySource[resourceName] = new Dictionary<string, float>();
                if (!percentageMalusBySource.ContainsKey(resourceName)) percentageMalusBySource[resourceName] = new Dictionary<string, float>();
                if (!persistentBonusBySource.ContainsKey(resourceName)) persistentBonusBySource[resourceName] = new Dictionary<string, float>();
                if (!persistentMalusBySource.ContainsKey(resourceName)) persistentMalusBySource[resourceName] = new Dictionary<string, float>();
            }
            
            // Apply any pending legend bonuses to this new resource
            if (GovernmentLogic.Instance != null)
            {
                GovernmentLogic.Instance.ApplyPendingBonusesToResource(resourceName, resourceSlot.gameUnit.section);
            }
        }
    }

    public void AddProductionSlot(GameProductionSlot productionSlot)
    {
        if (!productionSlots.Contains(productionSlot))
        {
            productionSlots.Add(productionSlot);
        }
    }

    public void AddTechnologySlot(GameTechnologySlot technologySlot)
    {
        if (!technologySlots.Contains(technologySlot))
        {
            technologySlots.Add(technologySlot);
        }
    }

    public void RemoveResourceSlot(GameResourceSlot resourceSlot)
    {
        if (resourceSlots.Contains(resourceSlot))
        {
            resourceSlots.Remove(resourceSlot);

            string resourceName = resourceSlot.gameUnit.name;

            netProductionRates.Remove(resourceName);
            positiveModifiers.Remove(resourceName);
            negativeModifiers.Remove(resourceName);
            persistentPositiveModifiers.Remove(resourceName);
            persistentNegativeModifiers.Remove(resourceName);
            percentagePositiveModifiers.Remove(resourceName);
            percentageNegativeModifiers.Remove(resourceName);
        }
    }

    public void RemoveProductionSlot(GameProductionSlot productionSlot)
    {
        if (productionSlots.Contains(productionSlot))
        {
            productionSlots.Remove(productionSlot);
        }
    }

    public void RemoveTechnologySlot(GameTechnologySlot technologySlot)
    {
        if (technologySlots.Contains(technologySlot))
        {
            technologySlots.Remove(technologySlot);
        }
    }

    public void AdjustResourceModifier(string resourceName, float modifierAmount, bool isPositive, bool isAdd, string modifierSource)
    {
        var targetModifiers = isPositive ? persistentPositiveModifiers : persistentNegativeModifiers;

        if (!targetModifiers.ContainsKey(resourceName))
        {
            targetModifiers[resourceName] = 0f;
        }

        targetModifiers[resourceName] += isAdd ? modifierAmount : -modifierAmount;

        // Track per-source magnitudes for persistent modifiers
        var perSourceMap = isPositive ? persistentBonusBySource : persistentMalusBySource;
        if (!perSourceMap.ContainsKey(resourceName)) perSourceMap[resourceName] = new Dictionary<string, float>();
        var map = perSourceMap[resourceName];
        float current = 0f;
        map.TryGetValue(modifierSource ?? "Unknown", out current);
        float next = isAdd ? current + modifierAmount : current - modifierAmount;
        if (next <= 0.0001f) { if (map.ContainsKey(modifierSource)) map.Remove(modifierSource); }
        else map[modifierSource] = next;

        // Store the source for later use
        if (!string.IsNullOrEmpty(modifierSource))
        {
            if (!modifierSourceDict.ContainsKey(resourceName))
            {
                modifierSourceDict[resourceName] = new List<string>();
            }

            // Ensure the source is added only once
            if (!modifierSourceDict[resourceName].Contains(modifierSource))
            {
                modifierSourceDict[resourceName].Add(modifierSource);
            }
        }

        CalculateGlobalProductionRates();
    }

    // Expose per-source maps for tooltips/UI
    public IReadOnlyDictionary<string, float> GetPercentageBonusBySource(string resourceName)
    {
        if (percentageBonusBySource.TryGetValue(resourceName, out var dict)) return dict;
        return new Dictionary<string, float>();
    }

    public IReadOnlyDictionary<string, float> GetPercentageMalusBySource(string resourceName)
    {
        if (percentageMalusBySource.TryGetValue(resourceName, out var dict)) return dict;
        return new Dictionary<string, float>();
    }

    public IReadOnlyDictionary<string, float> GetPersistentBonusBySource(string resourceName)
    {
        if (persistentBonusBySource.TryGetValue(resourceName, out var dict)) return dict;
        return new Dictionary<string, float>();
    }

    public IReadOnlyDictionary<string, float> GetPersistentMalusBySource(string resourceName)
    {
        if (persistentMalusBySource.TryGetValue(resourceName, out var dict)) return dict;
        return new Dictionary<string, float>();
    }

    // Remove ALL modifiers (percentage and persistent flat, bonus and malus) that originated from a specific source
    // across all resources and sections. Also cleans up modifierSourceDict entries.
    public void ClearAllModifiersFromSource(string modifierSource)
    {
        if (string.IsNullOrEmpty(modifierSource)) return;

        // Walk existing resource slots first (ensures aggregated maps remain consistent)
        foreach (var resourceSlot in resourceSlots)
        {
            if (resourceSlot == null || resourceSlot.gameUnit == null) continue;
            string resourceName = resourceSlot.gameUnit.name;

            // Percentage bonuses
            if (percentageBonusBySource.TryGetValue(resourceName, out var posMap))
            {
                posMap.Remove(modifierSource);
            }
            // Percentage maluses
            if (percentageMalusBySource.TryGetValue(resourceName, out var negMap))
            {
                negMap.Remove(modifierSource);
            }

            // Persistent flat bonuses
            if (persistentBonusBySource.TryGetValue(resourceName, out var flatPosMap))
            {
                if (flatPosMap.TryGetValue(modifierSource, out float flatBonus))
                {
                    if (persistentPositiveModifiers.ContainsKey(resourceName))
                    {
                        persistentPositiveModifiers[resourceName] = Mathf.Max(0f, persistentPositiveModifiers[resourceName] - flatBonus);
                    }
                    flatPosMap.Remove(modifierSource);
                }
            }
            // Persistent flat maluses
            if (persistentMalusBySource.TryGetValue(resourceName, out var flatNegMap))
            {
                if (flatNegMap.TryGetValue(modifierSource, out float flatMalus))
                {
                    if (persistentNegativeModifiers.ContainsKey(resourceName))
                    {
                        persistentNegativeModifiers[resourceName] = Mathf.Max(0f, persistentNegativeModifiers[resourceName] - flatMalus);
                    }
                    flatNegMap.Remove(modifierSource);
                }
            }

            // Cleanup source listing for UI/debug
            if (modifierSourceDict.TryGetValue(resourceName, out var srcList))
            {
                srcList.Remove(modifierSource);
            }
        }

        // Also scrub any entries for resources not yet instantiated (pre-registered by name)
        // This prevents stale modifiers from being applied when the resource is later added.
        foreach (var kv in percentageBonusBySource.ToList())
        {
            var perSource = kv.Value;
            if (perSource.ContainsKey(modifierSource)) perSource.Remove(modifierSource);
        }
        foreach (var kv in percentageMalusBySource.ToList())
        {
            var perSource = kv.Value;
            if (perSource.ContainsKey(modifierSource)) perSource.Remove(modifierSource);
        }
        foreach (var kv in persistentBonusBySource.ToList())
        {
            var perSource = kv.Value;
            if (perSource.ContainsKey(modifierSource)) perSource.Remove(modifierSource);
        }
        foreach (var kv in persistentMalusBySource.ToList())
        {
            var perSource = kv.Value;
            if (perSource.ContainsKey(modifierSource)) perSource.Remove(modifierSource);
        }

        // Recalculate after cleanup
        CalculateGlobalProductionRates();
    }

    public void AdjustPercentageModifier(string resourceName, float modifierAmount, bool isPositive, bool isAdd, string modifierSource)
    {
        if (string.IsNullOrEmpty(resourceName)) return;
        if (string.IsNullOrEmpty(modifierSource)) modifierSource = "Unknown";

        var dict = isPositive ? percentageBonusBySource : percentageMalusBySource;
        if (!dict.ContainsKey(resourceName)) dict[resourceName] = new Dictionary<string, float>();
        var perSource = dict[resourceName];
        float magnitude = Mathf.Abs(modifierAmount);
        if (isAdd)
        {
            if (!perSource.ContainsKey(modifierSource))
            {
                perSource[modifierSource] = magnitude;
            }
            // else: do not stack for the same source
        }
        else
        {
            if (perSource.ContainsKey(modifierSource)) perSource.Remove(modifierSource);
        }

        // Track sources for optional UI/debug
        if (!modifierSourceDict.ContainsKey(resourceName))
        {
            modifierSourceDict[resourceName] = new List<string>();
        }
        if (isAdd)
        {
            if (!modifierSourceDict[resourceName].Contains(modifierSource))
            {
                modifierSourceDict[resourceName].Add(modifierSource);
            }
        }
        else
        {
            modifierSourceDict[resourceName].Remove(modifierSource);
        }

        CalculateGlobalProductionRates();
    }

    // Apply a percentage modifier (bonus/malus) to all resources belonging to a section.
    public void AdjustPercentageModifierForSection(string sectionName, float modifierAmount, bool isPositive, bool isAdd, string modifierSource)
    {
        if (string.IsNullOrEmpty(sectionName)) return;
        foreach (var slot in resourceSlots)
        {
            if (slot != null && slot.gameUnit != null && string.Equals(slot.gameUnit.section, sectionName, System.StringComparison.OrdinalIgnoreCase))
            {
                AdjustPercentageModifier(slot.gameUnit.name, modifierAmount, isPositive, isAdd, modifierSource);
            }
        }
    }

    private void CalculateGlobalProductionRates()
    {
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            positiveModifiers[resourceName] = 0f;
            negativeModifiers[resourceName] = 0f;
        }

        // Apply workshop rates
        foreach (var productionSlot in productionSlots)
        {
            var productionUnitData = productionSlot.productionUnitData;

            for (int i = 0; i < productionUnitData.producedResources.Count; i++)
            {
                string producedResource = productionUnitData.producedResources[i];
                float productionRate = productionUnitData.productionRates[i] * productionSlot.amount;

                positiveModifiers[producedResource] += productionRate;
            }

            for (int i = 0; i < productionUnitData.consumedResources.Count; i++)
            {
                string consumedResource = productionUnitData.consumedResources[i];
                float consumptionRate = productionUnitData.consumeRates[i] * productionSlot.amount;

                negativeModifiers[consumedResource] += consumptionRate;
            }
        }

        // Apply persistent modifiers
        foreach (var resourceName in persistentPositiveModifiers.Keys)
        {
            positiveModifiers[resourceName] += persistentPositiveModifiers[resourceName];
        }

        foreach (var resourceName in persistentNegativeModifiers.Keys)
        {
            negativeModifiers[resourceName] += persistentNegativeModifiers[resourceName];
        }
        
        // Apply production scaling bonuses (bonus resources per production unit)
        if (GameUnitsLogic.Instance != null)
        {
            foreach (var resourceSlot in resourceSlots)
            {
                string resourceName = resourceSlot.gameUnit.name;
                float scalingBonus = GameUnitsLogic.Instance.CalculateProductionScalingBonus(resourceName);
                
                if (scalingBonus > 0f)
                {
                    positiveModifiers[resourceName] += scalingBonus;
                }
            }
        }

        // Apply percentage modifiers
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            float basePositiveRate = positiveModifiers[resourceName];
            float baseNegativeRate = negativeModifiers[resourceName];

            // Aggregate percentage modifiers from all sources for this resource
            float posPercent = 0f;
            float negPercent = 0f;
            if (percentageBonusBySource.TryGetValue(resourceName, out var posDict))
            {
                foreach (var kv in posDict) posPercent += Mathf.Max(0f, kv.Value);
            }
            if (percentageMalusBySource.TryGetValue(resourceName, out var negDict))
            {
                foreach (var kv in negDict) negPercent += Mathf.Max(0f, kv.Value);
            }
            // Keep legacy aggregated dictionaries up-to-date for UI/debug displays
            percentagePositiveModifiers[resourceName] = posPercent;
            percentageNegativeModifiers[resourceName] = negPercent;

            float totalPercent = posPercent - negPercent;
            // Apply morale-based global production modifier (difference from balance)
            int moraleDelta = 0;
            if (StatManager.Instance != null)
            {
                moraleDelta = Mathf.RoundToInt(StatManager.Instance.GetMoraleDeltaPercent());
            }
            totalPercent += moraleDelta;
            if (Mathf.Abs(totalPercent) > 0.001f && basePositiveRate != 0f)
            {
                basePositiveRate *= (1 + totalPercent / 100f);
            }

            float netRate = basePositiveRate - baseNegativeRate;

            netProductionRates[resourceName] = netRate;

            resourceSlot.productionRate = netRate;
        }
    }

    private void DisableProductionUnitsIfResourceDepleted()
    {
        foreach (var productionSlot in productionSlots)
        {
            var productionUnitData = productionSlot.productionUnitData;
            bool isResourceDepleted = false;

            foreach (var resourceName in productionUnitData.consumedResources)
            {
                var resourceSlot = resourceSlots.Find(slot => slot.gameUnit.name == resourceName);
                if (resourceSlot == null || resourceSlot.amount <= 0)
                {
                    isResourceDepleted = true;
                    break;
                }
            }

            productionSlot.insufficientProduction = isResourceDepleted;
        }
    }

    private void EnableProductionUnitsIfResourcesSufficient()
    {
        foreach (var productionSlot in productionSlots)
        {
            var productionUnitData = productionSlot.productionUnitData;
            bool areAllResourcesAvailable = true;

            foreach (var resourceName in productionUnitData.consumedResources)
            {
                var resourceSlot = resourceSlots.Find(slot => slot.gameUnit.name == resourceName);
                if (resourceSlot == null || resourceSlot.amount <= 0)
                {
                    areAllResourcesAvailable = false;
                    break;
                }
            }

            productionSlot.insufficientProduction = !areAllResourcesAvailable;
        }
    }

    public float GetNetProductionRate(string resourceName)
    {
        return netProductionRates.TryGetValue(resourceName, out var rate) ? rate : 0f;
    }
    
    public void EnsureAllSlotsRegistered()
    {
        if (GameUnitsLogic.Instance == null) return;
        
        // Ensure production slots are registered
        if (GameUnitsLogic.Instance.productionTab != null)
        {
            var allProductionSlots = GameUnitsLogic.Instance.productionTab.slots
                .Select(slot => slot.GetComponent<GameProductionSlot>())
                .Where(slot => slot != null);
            
            foreach (var productionSlot in allProductionSlots)
            {
                if (!productionSlots.Contains(productionSlot))
                {
                    AddProductionSlot(productionSlot);
                }
            }
        }
        
        // Ensure resource slots are registered
        if (GameUnitsLogic.Instance.storageTab != null)
        {
            var allResourceSlots = GameUnitsLogic.Instance.storageTab.slots
                .Select(slot => slot.GetComponent<GameResourceSlot>())
                .Where(slot => slot != null);
            
            foreach (var resourceSlot in allResourceSlots)
            {
                if (!resourceSlots.Contains(resourceSlot))
                {
                    AddResourceSlot(resourceSlot);
                }
            }
        }
    }

}
