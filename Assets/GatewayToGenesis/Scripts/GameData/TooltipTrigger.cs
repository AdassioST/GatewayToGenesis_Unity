using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Fallback Tooltip Settings")]
    public bool useCustomTooltip;

    public string customTitle;
    public string customDescription;

    public void OnPointerEnter(PointerEventData eventData)
    {
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

            return dynamicData;
        }

        // Check for GameResourceSlot
        GameResourceSlot resourceSlot = GetComponent<GameResourceSlot>();

        if (resourceSlot != null)
        {
            dynamicData.tooltipTitle = resourceSlot.gameUnit.name;
            dynamicData.tooltipDescription = resourceSlot.gameUnit.description;

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

            //ADD IF CHECK TO BUILD REQUIREMENTS DISPLAY.

            //ADD IF CHECK TO PRODUCTION EFFECTS.

            return dynamicData;
        }

        // Check for GameTechnologySlot
        GameTechnologySlot technologySlot = GetComponent<GameTechnologySlot>();
        if (technologySlot != null)
        {
            dynamicData.tooltipTitle = technologySlot.gameUnit.name;
            dynamicData.tooltipDescription = technologySlot.gameUnit.description;

            //ADD IF CHECK TO BUILD REQUIREMENTS DISPLAY.

            //ADD IF CHECK TO PREVIOUS TECH REQUIREMENTS DISPLAY.

            return dynamicData;
        }

        return dynamicData;
    }

    public string GetFormattedText()
    {
        string formattedText = customTitle + "\n" + customDescription;

        // Add extra information or breakdowns as needed
        if (!string.IsNullOrEmpty(customTitle))
        {
            formattedText += "\n" + customTitle;
        }

        return formattedText;
    }
}
