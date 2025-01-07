using System.Collections.Generic;
using UnityEngine;

public class ResourceModifierLogic : MonoBehaviour
{
    private Dictionary<string, List<float>> positiveModifiers = new Dictionary<string, List<float>>();
    private Dictionary<string, List<float>> negativeModifiers = new Dictionary<string, List<float>>();

    public void AddModifier(string resourceName, float modifierValue, bool isPositive)
    {
        var targetList = isPositive ? positiveModifiers : negativeModifiers;

        if (!targetList.ContainsKey(resourceName))
        {
            targetList[resourceName] = new List<float>();
        }

        targetList[resourceName].Add(modifierValue);
    }

    public void RemoveModifier(string resourceName, float modifierValue, bool isPositive)
    {
        var targetList = isPositive ? positiveModifiers : negativeModifiers;

        if (targetList.ContainsKey(resourceName))
        {
            targetList[resourceName].Remove(modifierValue);
        }
    }

    public float GetTotalModifier(string resourceName, bool isPositive)
    {
        var targetList = isPositive ? positiveModifiers : negativeModifiers;

        if (!targetList.ContainsKey(resourceName)) return 0f;

        float totalModifier = 0f;
        foreach (var modifier in targetList[resourceName])
        {
            totalModifier += modifier;
        }
        return totalModifier;
    }

    public void ClearModifiers(string resourceName)
    {
        if (positiveModifiers.ContainsKey(resourceName))
        {
            positiveModifiers[resourceName].Clear();
        }

        if (negativeModifiers.ContainsKey(resourceName))
        {
            negativeModifiers[resourceName].Clear();
        }
    }
}

