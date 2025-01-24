using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

[CreateAssetMenu(fileName = "New Tooltip Data", menuName = "UI Notifications/Tooltip Data", order = 1)]
public class TooltipData : ScriptableObject
{
    public GameObject sourceObject;

    public string tooltipTitle;
    public string tooltipDescription;

    public string type;

    //Usable for both Technology and ProductionUnits
    public string resourceRequirements; // E.g. 50: Gold, 75 Elderwood

    // Resource-specific fields
    public string productionModifiers; // E.g., "+200 Base, +50 Building Bonus"
    public string storageBreakdown; // E.g., "Max: 1000 (Building A: +500, Building B: +500)"

    // Production unit fields
    public string productionEffects; // E.g., "+5 Food per second"

    // Technology-specific fields

    public string techRequirements; // E.g., "Requires: Agriculture, Masonry"

    // Add any relevant data required for tooltips

    public string FormatResourceRequirements(List<string> resourceNames, List<float> resourceAmounts, List<GameResourceSlot> availableResources)
    {
        string formattedText = "Requires:\n";

        for (int i = 0; i < resourceNames.Count; i++)
        {
            string resourceName = resourceNames[i];
            float requiredAmount = resourceAmounts[i];

            // Check if the resource exists in the available slots
            GameResourceSlot resourceSlot = availableResources.Find(slot => slot.gameUnit.name == resourceName);

            if (resourceSlot != null)
            {
                bool hasEnough = resourceSlot.amount >= requiredAmount;
                string color = hasEnough ? "green" : "red";


                formattedText += $"<color={color}> {requiredAmount} <sprite=2> {resourceName}</color>\n";
            }

            else
            {
                // Resource is unavailable

                formattedText += $"<color=red> {requiredAmount} <sprite name=Grinning face> {resourceName}</color>\n";
            }
        }

        return formattedText;
    }

    public string FormatStorageBreakdown(Dictionary<string, Dictionary<string, float>> storageBreakdown, string resourceName)
    {
        if (!storageBreakdown.ContainsKey(resourceName))
        {
            return "";
        }

        var resourceBreakdown = storageBreakdown[resourceName];
        float totalStorage = 0;
        string formattedText = $"Total Storage: <color=yellow>{totalStorage:0.##}</color>\n\n"; // Always show Total Storage

        string breakdownText = "";

        // Iterate through the storage breakdown for the resource
        foreach (var entry in resourceBreakdown)
        {
            breakdownText += $"- {entry.Key}: <color=green>{entry.Value:0.##}</color>\n";
            totalStorage += entry.Value;
        }

        // Update the Total Storage text after the breakdown calculation
        formattedText = $"Total Storage: <color=yellow>{totalStorage:0.##}</color>\n\n";

        // Rename to "Detailed Breakdown" and append the breakdown
        formattedText += "<b>Detailed Breakdown:</b>\n" + breakdownText;

        return formattedText;
    }


    public string FormatProductionModifiers(string resourceName)
    {
        if (!GlobalProductionManager.Instance.netProductionRates.ContainsKey(resourceName))
        {
            Debug.LogError($"No production data found for resource: {resourceName}");
            return "";
        }

        // Extract data from GlobalProductionManager
        float netRate = GlobalProductionManager.Instance.netProductionRates[resourceName];
        string formattedText = $"Net Production Rate: <color=yellow>{netRate:0.##}</color>\n\n"; // Always show Net Production Rate, even if it's 0

        bool hasModifiers = false;
        string breakdownText = "";

        // Add breakdown from production and consumption slots
        foreach (var productionSlot in GlobalProductionManager.Instance.productionSlots)
        {
            var productionData = productionSlot.productionUnitData;

            // Check production
            if (productionData.producedResources.Contains(resourceName))
            {
                int index = productionData.producedResources.IndexOf(resourceName);
                float productionRate = productionData.productionRates[index] * productionSlot.amount;
                if (productionRate != 0)
                {
                    breakdownText += $"- {productionSlot.productionUnitData.name}: <color=green>{productionRate:0.##}</color>\n";
                    hasModifiers = true;
                }
            }

            // Check consumption
            if (productionData.consumedResources.Contains(resourceName))
            {
                int index = productionData.consumedResources.IndexOf(resourceName);
                float consumptionRate = productionData.consumeRates[index] * productionSlot.amount;
                if (consumptionRate != 0)
                {
                    breakdownText += $"- {productionSlot.productionUnitData.name} (Consumption): <color=red>{consumptionRate:0.##}</color>\n";
                    hasModifiers = true;
                }
            }
        }

        // Add persistent positive modifiers (from Technologies, Events, or Special Cases)
        if (GlobalProductionManager.Instance.persistentPositiveModifiers.TryGetValue(resourceName, out var persistentPositive) && persistentPositive != 0)
        {
            breakdownText += $"- {string.Join(", ", GlobalProductionManager.Instance.modifierSourceDict.ContainsKey(resourceName) ? GlobalProductionManager.Instance.modifierSourceDict[resourceName] : new List<string>())}: <color=green>+{persistentPositive:0.##}</color>\n";
            hasModifiers = true;
        }

        // Add persistent negative modifiers (from Technologies, Events, or Special Cases)
        if (GlobalProductionManager.Instance.persistentNegativeModifiers.TryGetValue(resourceName, out var persistentNegative) && persistentNegative != 0)
        {
            breakdownText += $"- {string.Join(", ", GlobalProductionManager.Instance.modifierSourceDict.ContainsKey(resourceName) ? GlobalProductionManager.Instance.modifierSourceDict[resourceName] : new List<string>())}: <color=red>{persistentNegative:0.##}</color>\n";
            hasModifiers = true;
        }

        // Add percentage positive modifiers (from Technologies, Events, or Special Cases)
        if (GlobalProductionManager.Instance.percentagePositiveModifiers.TryGetValue(resourceName, out var percentagePositive) && percentagePositive != 0)
        {
            breakdownText += $"- {string.Join(", ", GlobalProductionManager.Instance.modifierSourceDict.ContainsKey(resourceName) ? GlobalProductionManager.Instance.modifierSourceDict[resourceName] : new List<string>())}: <color=green>+{percentagePositive:0.##}%</color>\n";
            hasModifiers = true;
        }

        // Add percentage negative modifiers (from Technologies, Events, or Special Cases)
        if (GlobalProductionManager.Instance.percentageNegativeModifiers.TryGetValue(resourceName, out var percentageNegative) && percentageNegative != 0)
        {
            breakdownText += $"- {string.Join(", ", GlobalProductionManager.Instance.modifierSourceDict.ContainsKey(resourceName) ? GlobalProductionManager.Instance.modifierSourceDict[resourceName] : new List<string>())}: <color=red>{percentageNegative:0.##}%</color>\n";
            hasModifiers = true;
        }

        // Only show "Detailed Breakdown:" if there were any modifiers or production/consumption data
        if (hasModifiers)
        {
            formattedText += "<b>Detailed Breakdown:</b>\n" + breakdownText;
        }

        return formattedText;
    }



    public string FormatTechRequirements(List<string> techRequirements, TabBuilderLogic researchTab)
    {
        string formattedText = "Must Unlock:\n";

        foreach (string requiredTech in techRequirements)
        {
            // Find the required technology slot
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            bool isUnlocked = requiredTechObj != null && requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked;

            // Format the requirement text
            string color = isUnlocked ? "green" : "red";
            formattedText += $"<color={color}> {requiredTech}</color>\n";
        }

        return formattedText;
    }

    public string FormatProductionUnitEffects(ProductionUnitData productionUnitData)
    {
        string formattedText = "Effects:\n";

        // Add housing if applicable
        if (productionUnitData.housing > 0)
        {
            formattedText += $" Housing +{productionUnitData.housing}\n";
        }

        // Format produced resources
        if (productionUnitData.producedResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.producedResources.Count; i++)
            {
                string resourceName = productionUnitData.producedResources[i];
                float productionRate = productionUnitData.productionRates[i];
                formattedText += $" +{productionRate} {resourceName}\n";
            }
        }

        // Format consumed resources
        if (productionUnitData.consumedResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.consumedResources.Count; i++)
            {
                string resourceName = productionUnitData.consumedResources[i];
                float consumeRate = productionUnitData.consumeRates[i];
                formattedText += $" -{consumeRate} {resourceName}\n";
            }
        }

        // Format storage resources
        if (productionUnitData.storageResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.storageResources.Count; i++)
            {
                string resourceName = productionUnitData.storageResources[i];
                float storageAmount = productionUnitData.storageAmount[i];
                formattedText += $" Max +{storageAmount} {resourceName}\n";
            }
        }

        return formattedText;
    }

    public string FormatResourceRequirementsWithIncrementalCost(List<string> resourceNames,List<float> resourceAmounts,GameProductionSlot productionSlot)
    {
        string formattedText = "Requires:\n";

        for (int i = 0; i < resourceNames.Count; i++)
        {
            string resourceName = resourceNames[i];

            // Check available resources
            GameResourceSlot resourceSlot = GameUnitsLogic.Instance.GetResourceSlotFromName(resourceName);
            bool hasEnough = resourceSlot != null && resourceSlot.amount >= productionSlot.incrementalCost;

            string color = hasEnough ? "green" : "red";
            formattedText += $"<color={color}> {productionSlot.incrementalCost:0.##} {resourceName}</color>\n";
        }

        return formattedText;
    }

}