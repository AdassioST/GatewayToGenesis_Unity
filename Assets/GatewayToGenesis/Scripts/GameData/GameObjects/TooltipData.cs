using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

[CreateAssetMenu(fileName = "New Tooltip Data", menuName = "UI Notifications/Tooltip Data", order = 1)]
public class TooltipData : ScriptableObject
{
    public GameObject sourceObject;

    public string tooltipTitle, tooltipDescription, type;

    public string resourceRequirements, productionModifiers, storageBreakdown;

    public string productionEffects;

    public string techRequirements;

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

        return formattedText.Trim();
    }

    public string FormatTechnologyResourceRequirements(List<string> resourceNames,List<float> resourceAmounts,List<GameResourceSlot> availableResources,Dictionary<string, float> resourceProgress = null, bool isUnlocked = false)
    {
        string formattedText = "Requires:\n";

        for (int i = 0; i < resourceNames.Count; i++)
        {
            string resourceName = resourceNames[i];
            float requiredAmount = resourceAmounts[i];

            // Calculate the remaining amount based on progress (if provided)
            float remainingAmount = requiredAmount;
            if (resourceProgress != null && resourceProgress.ContainsKey(resourceName))
            {
                remainingAmount = requiredAmount - resourceProgress[resourceName];
            }

            // If the technology is unlocked, force the color to green
            string color = isUnlocked ? "green" : "red";

            if (!isUnlocked)
            {
                // Check if the resource exists in the available slots
                GameResourceSlot resourceSlot = availableResources.Find(slot => slot.gameUnit.name == resourceName);

                if (resourceSlot != null)
                {
                    // Check if the player has enough resources for the remaining amount
                    bool hasEnough = resourceSlot.amount >= remainingAmount;
                    color = hasEnough ? "green" : "red";
                }
            }

            // Display the remaining amount
            formattedText += $"<color={color}> {remainingAmount} <sprite=2> {resourceName}</color>\n";
        }

        return formattedText.Trim();
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
            breakdownText += $" - {entry.Key}: <color=green>{entry.Value:0.##}</color>\n";
            totalStorage += entry.Value;
        }

        formattedText = $"Total Storage: <color=yellow>{totalStorage:0.##}</color>\n\n";

        formattedText += "<b>Detailed Breakdown:</b>\n" + breakdownText;

        return formattedText.Trim();
    }


    public string FormatProductionModifiers(string resourceName)
    {
        if (!GlobalProductionManager.Instance.netProductionRates.ContainsKey(resourceName))
        {
            Debug.LogError($"No production data found for resource: {resourceName}");
            return "";
        }

        float netRate = GlobalProductionManager.Instance.netProductionRates[resourceName];
        string formattedText = $"Net Production Rate: <color=yellow>{netRate:0.##}</color>\n\n"; // Always show Net Production Rate, even if it's 0

        bool hasModifiers = false;
        string breakdownText = "";

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
                    breakdownText += $" - {productionSlot.productionUnitData.name}: <color=green>{productionRate:0.##}</color>\n";
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
                    breakdownText += $" - {productionSlot.productionUnitData.name} (Consumption): <color=red>{consumptionRate:0.##}</color>\n";
                    hasModifiers = true;
                }
            }
        }

        // Show persistent flat modifiers per source to avoid collapsing into one line and duplications
        var flatBonusBySource = GlobalProductionManager.Instance.GetPersistentBonusBySource(resourceName);
        foreach (var kv in flatBonusBySource)
        {
            if (kv.Value != 0)
            {
                breakdownText += $" - {kv.Key}: <color=green>+{kv.Value:0.##}</color>\n";
                hasModifiers = true;
            }
        }
        var flatMalusBySource = GlobalProductionManager.Instance.GetPersistentMalusBySource(resourceName);
        foreach (var kv in flatMalusBySource)
        {
            if (kv.Value != 0)
            {
                breakdownText += $" - {kv.Key}: <color=red>{kv.Value:0.##}</color>\n";
                hasModifiers = true;
            }
        }

        // Show percentage modifiers per source (bonuses and maluses separately)
        var pctBonusBySource = GlobalProductionManager.Instance.GetPercentageBonusBySource(resourceName);
        foreach (var kv in pctBonusBySource)
        {
            if (kv.Value != 0)
            {
                breakdownText += $" - {kv.Key}: <color=green>+{kv.Value:0.##}%</color>\n";
                hasModifiers = true;
            }
        }
        var pctMalusBySource = GlobalProductionManager.Instance.GetPercentageMalusBySource(resourceName);
        foreach (var kv in pctMalusBySource)
        {
            if (kv.Value != 0)
            {
                breakdownText += $" - {kv.Key}: <color=red>{kv.Value:0.##}%</color>\n";
                hasModifiers = true;
            }
        }

        // Morale global modifier (display even if not tracked per-source)
        int moraleDelta = 0;
        if (StatManager.Instance != null)
        {
            moraleDelta = Mathf.RoundToInt(StatManager.Instance.GetMoraleDeltaPercent());
        }
        if (moraleDelta != 0)
        {
            string color = moraleDelta > 0 ? "green" : "red";
            string sign = moraleDelta > 0 ? "+" : "";
            breakdownText += $" - Morale: <color={color}>{sign}{moraleDelta}%</color>\n";
            hasModifiers = true;
        }

        // Only show "Detailed Breakdown:" if there were any modifiers or production/consumption data
        if (hasModifiers)
        {
            formattedText += "<b>Detailed Breakdown:</b>\n" + breakdownText;
        }

        return formattedText.Trim();
    }



    public string FormatTechRequirements(List<string> techRequirements, TabBuilderLogic researchTab)
    {
        string formattedText = "Must Unlock:\n";

        foreach (string requiredTech in techRequirements)
        {
            GameObject requiredTechObj = researchTab.slots.Find(slot => slot.name == requiredTech);
            bool isUnlocked = requiredTechObj != null && requiredTechObj.GetComponent<GameTechnologySlot>().isUnlocked;

            // Format the requirement text
            string color = isUnlocked ? "green" : "red";
            formattedText += $"<color={color}> {requiredTech}</color>\n";
        }

        return formattedText.Trim();
    }

    public string FormatProductionUnitEffects(ProductionUnitData productionUnitData)
    {
        string formattedText = "Effects:\n";

        if (productionUnitData.housing > 0)
        {
            formattedText += $" +{productionUnitData.housing} Housing\n";
        }

        // Format produced resources
        if (productionUnitData.producedResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.producedResources.Count; i++)
            {
                string resourceName = productionUnitData.producedResources[i];
                float productionRate = productionUnitData.productionRates[i];
                formattedText += $" +{productionRate:0.##}/s {resourceName}\n";
            }
        }

        // Format storage resources
        if (productionUnitData.storageResources.Count > 0)
        {
            for (int i = 0; i < productionUnitData.storageResources.Count; i++)
            {
                string resourceName = productionUnitData.storageResources[i];
                float storageAmount = productionUnitData.storageAmount[i];
                formattedText += $" Max +{storageAmount:0.##} {resourceName}\n";
            }
        }

        // Format consumed resources
        if (productionUnitData.consumedResources.Count > 0)
        {
            formattedText += "Consumption:\n";
            for (int i = 0; i < productionUnitData.consumedResources.Count; i++)
            {
                string resourceName = productionUnitData.consumedResources[i];
                float consumeRate = productionUnitData.consumeRates[i];
                formattedText += $" -{consumeRate:0.##}/s {resourceName}\n";
            }
        }

        return formattedText.Trim();
    }


    public string FormatResourceRequirementsWithIncrementalCost(
        List<string> resourceNames,
        List<float> resourceAmounts,
        GameProductionSlot productionSlot)
    {
        string formattedText = "Requires:\n";

        for (int i = 0; i < resourceNames.Count; i++)
        {
            string resourceName = resourceNames[i];

            // Get the base requirement for the resource
            float baseAmount = resourceAmounts[i];

            // Calculate the incremental cost for this resource
            float incrementalCost = baseAmount;

            bool isUnit = productionSlot.gameUnit.type == "Unit";

            if (!isUnit)
            {
                incrementalCost *= Mathf.Exp((GlobalProductionManager.Instance.costBalance / GlobalProductionManager.Instance.techTier) * productionSlot.maxAmount);
            }

            // Check available resources
            GameResourceSlot resourceSlot = GameUnitsLogic.Instance.GetResourceSlotFromName(resourceName);
            bool hasEnough = resourceSlot != null && resourceSlot.amount >= incrementalCost;

            // Format the output with color coding
            string color = hasEnough ? "green" : "red";
            formattedText += $"<color={color}> {incrementalCost:0.##} {resourceName}</color>\n";
        }

        return formattedText.Trim();
    }

}