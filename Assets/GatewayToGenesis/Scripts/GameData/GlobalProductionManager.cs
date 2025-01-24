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
    }

    private void Start()
    {
        InitializeProductionRates();
    }

    private void Update()
    {
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

    public void AdjustPercentageModifier(string resourceName, float modifierAmount, bool isPositive, bool isAdd, string modifierSource)
    {
        var targetModifiers = isPositive ? percentagePositiveModifiers : percentageNegativeModifiers;

        if (!targetModifiers.ContainsKey(resourceName))
        {
            targetModifiers[resourceName] = 0f;
        }

        targetModifiers[resourceName] += isAdd ? modifierAmount : -modifierAmount;

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



    private void CalculateGlobalProductionRates()
    {
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            positiveModifiers[resourceName] = 0f;
            negativeModifiers[resourceName] = 0f;
        }

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

        foreach (var resourceName in persistentPositiveModifiers.Keys)
        {
            positiveModifiers[resourceName] += persistentPositiveModifiers[resourceName];
        }

        foreach (var resourceName in persistentNegativeModifiers.Keys)
        {
            negativeModifiers[resourceName] += persistentNegativeModifiers[resourceName];
        }

        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            float baseNetRate = positiveModifiers[resourceName] - negativeModifiers[resourceName];

            if (percentagePositiveModifiers.TryGetValue(resourceName, out var positivePercent))
            {
                baseNetRate *= (1 + positivePercent / 100f);
            }

            if (percentageNegativeModifiers.TryGetValue(resourceName, out var negativePercent))
            {
                baseNetRate *= (1 - negativePercent / 100f);
            }

            netProductionRates[resourceName] = baseNetRate;

            resourceSlot.productionRate = baseNetRate;
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
}
