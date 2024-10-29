using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceCalculator
{
    public ResourceCalculator() { }
    public float CalculateClickPower(float baseClickPower)
    {
        //TODO: Add Bonuses
        return 2 * baseClickPower;
    }

    public Dictionary<string, float> CalculateResourceFactorByBuildingCount(float productionEntityAmount, Dictionary<string, float> resourcesDict)
    {
        if (resourcesDict == null) { return null; }
        //TODO: Add bonuses
        foreach (string resourceID in resourcesDict.Keys.ToList())
        {
            resourcesDict[resourceID] *= productionEntityAmount;
        }
        return resourcesDict;
    }

    public Dictionary<string, float> CalculateNextCost(float productionEntityAmount, Dictionary<string, float> baseCost)
    {
        Dictionary<string, float> newCost = new Dictionary<string, float>();
        foreach (string resourceID in baseCost.Keys.ToList())
        {
            float newCostByResource = baseCost[resourceID] * (1 + Mathf.Exp((0.05f / 1) * productionEntityAmount));
            newCost.Add(resourceID, newCostByResource);
        }
        return newCost;
    }
}
