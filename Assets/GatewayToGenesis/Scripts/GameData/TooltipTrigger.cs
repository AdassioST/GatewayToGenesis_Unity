using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool useCustomTooltip, isBreakdownDisplay, isProductionModifiers;

    public string customTitle, customDescription, customType;

    private void Update()
    {
        UpdateDynamicTooltipData();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Transform displayTransform = transform.Find("Display");
        if (displayTransform != null && !displayTransform.gameObject.activeSelf)
        {
            return;
        }

        TooltipData tooltipData = CreateDynamicTooltipData();
        TooltipSystemLogic.Instance.ShowTooltip(tooltipData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystemLogic.Instance.HideTooltip();

        GameObject hoveredObject = eventData.pointerCurrentRaycast.gameObject;

        if (hoveredObject != null)
        {
            TooltipTrigger newTrigger = hoveredObject.GetComponentInParent<TooltipTrigger>();

            if (newTrigger != null)
            {
                TooltipData newTooltipData = newTrigger.CreateDynamicTooltipData();
                TooltipSystemLogic.Instance.ShowTooltip(newTooltipData);
            }
        }
    }

    private void UpdateDynamicTooltipData()
    {
        if (TooltipSystemLogic.Instance.isTooltipActive && TooltipSystemLogic.Instance.currentTooltipData?.sourceObject == gameObject)
        {
            GameResourceSlot resourceSlot = GetComponent<GameResourceSlot>() ?? GetComponentInParent<GameResourceSlot>();

            GameProductionSlot productionSlot = GetComponent<GameProductionSlot>();

            GameTechnologySlot technologySlot = GetComponent<GameTechnologySlot>();

            // If a resource slot exists, refresh tooltip with dynamic data
            if (resourceSlot != null && isProductionModifiers)
            {
                TooltipData updatedData = CreateDynamicTooltipData();
                TooltipSystemLogic.Instance.RefreshTooltip(updatedData);
            }
            // If a production slot exists, refresh tooltip with dynamic data
            if (productionSlot != null)
            {
                TooltipData updatedData = CreateDynamicTooltipData();
                TooltipSystemLogic.Instance.RefreshTooltip(updatedData);
            }
            // If a technology slot exists, refresh tooltip with dynamic data
            if (technologySlot != null)
            {
                TooltipData updatedData = CreateDynamicTooltipData();
                TooltipSystemLogic.Instance.RefreshTooltip(updatedData);
            }
        }
    }

    private TooltipData CreateDynamicTooltipData()
    {
        TooltipData dynamicData = ScriptableObject.CreateInstance<TooltipData>();

        dynamicData.sourceObject = gameObject; // Associate TooltipData with the GameObject

        if (useCustomTooltip)
        {
            if (!string.IsNullOrEmpty(customTitle))
            {
                dynamicData.tooltipTitle = customTitle;
            }

            if (!string.IsNullOrEmpty(customDescription))
            {
                dynamicData.tooltipDescription = customDescription;
            }

            if (!string.IsNullOrEmpty(customType))
            {
                dynamicData.type = customType;
            }

            return dynamicData;
        }

        // Check for GameResourceSlot
        GameResourceSlot resourceSlot = GetComponent<GameResourceSlot>();

        if (resourceSlot == null) resourceSlot = GetComponentInParent<GameResourceSlot>();

        if (resourceSlot != null)
        {
            dynamicData.tooltipTitle = resourceSlot.gameUnit.name;

            if (isBreakdownDisplay)
            {
                string storageBreakdownText = dynamicData.FormatStorageBreakdown(GameUnitsLogic.Instance.storageBreakdown, resourceSlot.gameUnit.name);

                if (!string.IsNullOrEmpty(storageBreakdownText))
                {
                    dynamicData.storageBreakdown = storageBreakdownText;
                }
            }

            else if (isProductionModifiers)
            {
                string productionModifiersText = dynamicData.FormatProductionModifiers(resourceSlot.gameUnit.name);

                if (!string.IsNullOrEmpty(productionModifiersText))
                {
                    dynamicData.productionModifiers = productionModifiersText;
                }
            }

            else
            {
                dynamicData.tooltipDescription = resourceSlot.gameUnit.description;
                dynamicData.type = resourceSlot.gameUnit.type;
            }

            return dynamicData;
        }


        // Check for GameProductionSlot
        GameProductionSlot productionSlot = GetComponent<GameProductionSlot>();
        if (productionSlot != null)
        {
            dynamicData.tooltipTitle = productionSlot.gameUnit.name;
            dynamicData.tooltipDescription = productionSlot.gameUnit.description;

            dynamicData.type = productionSlot.gameUnit.type;

            List<GameResourceSlot> availableResources = GameUnitsLogic.Instance.GetAvailableResources();
            dynamicData.resourceRequirements = dynamicData.FormatResourceRequirementsWithIncrementalCost(productionSlot.productionUnitData.buildResourceRequirements,productionSlot.productionUnitData.buildRequirementsAmount, productionSlot);

            dynamicData.productionEffects = dynamicData.FormatProductionUnitEffects(productionSlot.productionUnitData);

            return dynamicData;
        }

        // Check for GameTechnologySlot
        GameTechnologySlot technologySlot = GetComponent<GameTechnologySlot>();

        if (technologySlot != null)
        {
            dynamicData.tooltipTitle = technologySlot.gameUnit.name;
            dynamicData.tooltipDescription = technologySlot.gameUnit.description;

            dynamicData.type = technologySlot.gameUnit.type + " Tech";

            if (dynamicData.type != "Normal Tech")
            {
                string typeColor = dynamicData.type == "Event Tech" ? "yellow" : "red";
                dynamicData.type = $"<color={typeColor}>{dynamicData.type}</color>\n";
            }

            List<GameResourceSlot> availableResources = GameUnitsLogic.Instance.GetAvailableResources();

            Dictionary<string, float> resourceProgress = null;

            if (GameUnitsLogic.Instance.technologyProgress.ContainsKey(technologySlot))
            {
                resourceProgress = GameUnitsLogic.Instance.technologyProgress[technologySlot];
            }

            dynamicData.resourceRequirements = dynamicData.FormatTechnologyResourceRequirements(technologySlot.technologyData.resourceRequirements, technologySlot.technologyData.resourceAmount, availableResources,resourceProgress, technologySlot.isUnlocked);

            if (technologySlot.technologyData.techRequirements.Count > 0)
            {
                TabBuilderLogic researchTab = GameUnitsLogic.Instance.researchTab;
                dynamicData.techRequirements = dynamicData.FormatTechRequirements(technologySlot.technologyData.techRequirements, researchTab);
            }

            return dynamicData;
        }

        TechUnlockableSlot unlockable = GetComponent<TechUnlockableSlot>();

        if (unlockable != null)
        {
            // Extract data from the associated TechUnlockable
            TechUnlockable unlockableData = unlockable.techUnlockableData;

            if (unlockableData != null)
            {
                if (unlockableData.gameUnit != null)
                dynamicData.tooltipTitle = unlockableData.gameUnit.name;

                // Handle unlockable type-specific logic
                switch (unlockableData.unlockableType)
                {
                    case TechUnlockableType.ClickPower:
                        if (unlockableData.resourceModifier > 0)
                        {
                            dynamicData.type = "Click Power Modifier";

                            dynamicData.productionEffects = $"Increases the Click Power of {unlockableData.gameUnit.name} by {unlockableData.resourceModifier}";

                            dynamicData.tooltipDescription = "Carpal Tunnel Power!";
                        }

                        else
                        {
                            dynamicData.tooltipDescription = unlockableData.gameUnit.description;

                            dynamicData.type = unlockableData.gameUnit.type;
                        }

                        break;

                    case TechUnlockableType.Building:
                    case TechUnlockableType.Unit:
                        GameProductionSlot.InitializeProductionUnitDataDictionary();

                        if (GameProductionSlot.productionUnitDataDictionary.TryGetValue(unlockableData.gameUnit.name, out ProductionUnitData productionData))
                        {
                            dynamicData.tooltipDescription = unlockableData.gameUnit.description;
                            dynamicData.resourceRequirements = dynamicData.FormatResourceRequirements(
                                productionData.buildResourceRequirements,
                                productionData.buildRequirementsAmount,
                                GameUnitsLogic.Instance.GetAvailableResources()
                            );
                            dynamicData.productionEffects = dynamicData.FormatProductionUnitEffects(productionData);
                        }
                        else
                        {
                            Debug.LogWarning($"Production data not found for unlockable: {unlockableData.gameUnit.name}");
                        }

                        dynamicData.type = unlockableData.unlockableType.ToString();
                        break;

                    case TechUnlockableType.Modifier:

                        if (unlockableData.name.StartsWith("Food D"))
                        {
                            dynamicData.productionEffects = $"Reduces the Food Demand of Population by {unlockableData.resourceModifier}%";

                            dynamicData.tooltipDescription = "Not starving anymore...";
                        }
                        else
                        {
                            dynamicData.productionEffects = $"Boosts the efficiency of all workshops producing {unlockableData.gameUnit.name} by {unlockableData.resourceModifier}%";

                            dynamicData.tooltipDescription = "Upping the uppies 100% upper";
                        }

                        //ADD COLORS TO TYPES "<color=green>Bonus Modifier</color>"
                        dynamicData.type = "Bonus Modifier";
                        break;

                    case TechUnlockableType.Arts:
                        dynamicData.tooltipDescription = $"Unlocks a unique Arts unit: {unlockableData.gameUnit.name}";
                        dynamicData.type = "Arts";
                        break;

                    case TechUnlockableType.Special:
                        if (!string.IsNullOrEmpty(unlockableData.description))
                        {
                            dynamicData.productionEffects = unlockableData.effects;
                        }
                        else
                        {
                            dynamicData.productionEffects = $"Unlocks a special unit: {unlockableData.gameUnit.name}";
                        }

                        dynamicData.tooltipTitle = "Special Unlock";

                        dynamicData.tooltipDescription = unlockableData.description;
                        break;

                    default:
                        Debug.LogWarning($"Unknown unlockable type: {unlockableData.unlockableType}");
                        dynamicData.tooltipDescription = "Unknown unlockable type.";
                        dynamicData.type = "Unknown";
                        break;
                }
            }

            return dynamicData;
        }



        return dynamicData;
    }

}
