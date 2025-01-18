using System.Collections;
using System.Collections.Generic;
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