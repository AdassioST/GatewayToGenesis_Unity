using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GlobalProductionManager : MonoBehaviour
{
    public List<GameResourceSlot> resourceSlots = new List<GameResourceSlot>();
    public List<GameProductionSlot> productionSlots = new List<GameProductionSlot>();
    public List<GameTechnologySlot> technologySlots = new List<GameTechnologySlot>();

    // Dictionaries to store global values for each resource type
    public Dictionary<string, float> netProductionRates = new Dictionary<string, float>();
    public Dictionary<string, float> positiveModifiers = new Dictionary<string, float>();
    public Dictionary<string, float> negativeModifiers = new Dictionary<string, float>();

    private ResourceModifierLogic modifierLogic;

    public float techTier = 1f, costBalance = 0.05f;

    private void Start()
    {
        modifierLogic = GetComponent<ResourceModifierLogic>();

        InitializeProductionRates();
    }

    private void InitializeProductionRates()
    {
        // Initialize dictionaries for resources already present in resourceSlots
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            if (!netProductionRates.ContainsKey(resourceName))
            {
                netProductionRates[resourceName] = 0f;
                positiveModifiers[resourceName] = 0f;
                negativeModifiers[resourceName] = 0f;
            }
        }
    }

    public void AddResourceSlot(GameResourceSlot resourceSlot)
    {
        if (!resourceSlots.Contains(resourceSlot))
        {
            resourceSlots.Add(resourceSlot);

            string resourceName = resourceSlot.gameUnit.name;

            // Ensure the resource has default entries in the dictionaries
            if (!netProductionRates.ContainsKey(resourceName))
            {
                netProductionRates[resourceName] = 0f;
                positiveModifiers[resourceName] = 0f;
                negativeModifiers[resourceName] = 0f;
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

    private void Update()
    {
        CalculateGlobalProductionRates();
        DisableProductionUnitsIfResourceDepleted();
        EnableProductionUnitsIfResourcesSufficient();
    }

    private void CalculateGlobalProductionRates()
    {
        // Reset the modifiers before recalculation
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;

            positiveModifiers[resourceName] = 0f;
            negativeModifiers[resourceName] = 0f;
        }

        // Calculate production and consumption rates
        foreach (var productionSlot in productionSlots)
        {
            var productionUnitData = productionSlot.productionUnitData;

            // Positive modifiers (produced resources)
            for (int i = 0; i < productionUnitData.producedResources.Count; i++)
            {
                string producedResource = productionUnitData.producedResources[i];
                float productionRate = productionUnitData.productionRates[i] * productionSlot.amount;

                if (positiveModifiers.ContainsKey(producedResource))
                {
                    positiveModifiers[producedResource] += productionRate;
                }
            }

            // Negative modifiers (consumed resources)
            for (int i = 0; i < productionUnitData.consumedResources.Count; i++)
            {
                string consumedResource = productionUnitData.consumedResources[i];
                float consumptionRate = productionUnitData.consumeRates[i] * productionSlot.amount;

                if (negativeModifiers.ContainsKey(consumedResource))
                {
                    negativeModifiers[consumedResource] += consumptionRate;
                }
            }
        }

        // Update net production rates and resource slots
        foreach (var resourceSlot in resourceSlots)
        {
            string resourceName = resourceSlot.gameUnit.name;
            float netRate = positiveModifiers[resourceName] - negativeModifiers[resourceName];

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

            // Check if any consumed resource is depleted
            foreach (var resourceName in productionUnitData.consumedResources)
            {
                var resourceSlot = resourceSlots.Find(slot => slot.gameUnit.name == resourceName);
                if (resourceSlot == null || resourceSlot.amount <= 0)
                {
                    isResourceDepleted = true;
                    break;
                }
            }

            if (isResourceDepleted)
            {
                productionSlot.insufficientProduction = true;
            }
        }
    }

    private void EnableProductionUnitsIfResourcesSufficient()
    {
        foreach (var productionSlot in productionSlots)
        {
            var productionUnitData = productionSlot.productionUnitData;
            bool areAllResourcesAvailable = true;

            // Check if all consumed resources are available
            foreach (var resourceName in productionUnitData.consumedResources)
            {
                var resourceSlot = resourceSlots.Find(slot => slot.gameUnit.name == resourceName);
                if (resourceSlot == null || resourceSlot.amount <= 0)
                {
                    areAllResourcesAvailable = false;
                    break;
                }
            }

            if (areAllResourcesAvailable)
            {
                productionSlot.insufficientProduction = false;
            }
        }
    }
}
