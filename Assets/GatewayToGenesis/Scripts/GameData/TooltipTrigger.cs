using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Fallback Tooltip Settings")]
    public bool useCustomTooltip;

    public string customTitle, customDescription, customType;
    private void Update()
    {

        if (TooltipSystemLogic.Instance.isTooltipActive && TooltipSystemLogic.Instance.currentTooltipData?.sourceObject == gameObject)
        {
            GameProductionSlot productionSlot = GetComponent<GameProductionSlot>();

            GameTechnologySlot technologySlot = GetComponent<GameTechnologySlot>();

            if (productionSlot != null || technologySlot != null)
            {
                TooltipData updatedData = CreateDynamicTooltipData();
                TooltipSystemLogic.Instance.RefreshTooltip(updatedData);
            }
        }
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

        if (resourceSlot != null)
        {
            dynamicData.tooltipTitle = resourceSlot.gameUnit.name;
            dynamicData.tooltipDescription = resourceSlot.gameUnit.description;

            dynamicData.type = resourceSlot.gameUnit.type;

            //ADD IF CHECK TO STORAGE DISPLAYS IF THE TAB HAS IT.

            //ADD IF CHECK TO NET PRODUCTION RATE BREAKDOWN IF THE TAB HAS IT.

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
            dynamicData.resourceRequirements = dynamicData.FormatResourceRequirements(technologySlot.technologyData.resourceRequirements, technologySlot.technologyData.resourceAmount, availableResources);

            if (technologySlot.technologyData.techRequirements.Count > 0)
            {
                TabBuilderLogic researchTab = GameUnitsLogic.Instance.researchTab;
                dynamicData.techRequirements = dynamicData.FormatTechRequirements(technologySlot.technologyData.techRequirements, researchTab);
            }

            return dynamicData;
        }

        return dynamicData;
    }

}
