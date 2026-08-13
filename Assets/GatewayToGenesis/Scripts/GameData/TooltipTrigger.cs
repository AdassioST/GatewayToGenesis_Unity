using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool useCustomTooltip, isBreakdownDisplay, isProductionModifiers;

    public string customTitle, customDescription, customType;
    public string customStorageBreakdown;

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

            if (isBreakdownDisplay && !string.IsNullOrEmpty(customStorageBreakdown))
            {
                dynamicData.storageBreakdown = customStorageBreakdown;
            }

            return dynamicData;
        }

        // Check for government display components first
        LeaderSlotDisplay leaderSlot = GetComponent<LeaderSlotDisplay>();
        if (leaderSlot != null)
        {
            return CreateLegendTooltipData(leaderSlot.GetLegendData());
        }

        // Check for HeadOfState container FIRST (structure: HeadOfState -> Name/Sprite/Title)
        // Need to traverse up the hierarchy to find HeadOfState container
        Transform currentParent = transform.parent;
        bool isInHeadOfStateHierarchy = false;
        
        // Also check if this GameObject itself is part of Head of State
        if (gameObject.name.Contains("HeadOfState"))
        {
            isInHeadOfStateHierarchy = true;
        }
        
        // Traverse up to 3 levels to find HeadOfState container
        for (int i = 0; i < 3 && currentParent != null; i++)
        {
            if (currentParent.name.Contains("HeadOfState"))
            {
                isInHeadOfStateHierarchy = true;
                break;
            }
            currentParent = currentParent.parent;
        }
        
        if (isInHeadOfStateHierarchy)
        {
            // Check if GovernmentLogic is available
            if (GovernmentLogic.Instance == null)
            {
                return dynamicData;
            }
            
            // Get HeadOfState seat data (index -1)
            var headOfStateSeat = GovernmentLogic.Instance.GetCouncilSeat(-1);
            if (headOfStateSeat != null)
            {
                // Check if this is the sprite element (legend tooltip)
                if (gameObject.name.Contains("Sprite") || gameObject.GetComponent<Image>() != null)
                {
                    if (headOfStateSeat.assignedLegend != null)
                    {
                        return CreateLegendTooltipData(headOfStateSeat.assignedLegend);
                    }
                    // If no legend, show assignment prompt
                    dynamicData.tooltipTitle = "Click to assign a Legend";
                    return dynamicData;
                }
                
                // For title/text elements: ALWAYS show seat info (like regular seats do)
                // Title should show seat information, not legend information
                var (title, description, type, effects) = CreateHeadOfStateSeatTooltipData(headOfStateSeat);
                if (!string.IsNullOrEmpty(title))
                {
                    dynamicData.tooltipTitle = title;
                    dynamicData.tooltipDescription = description;
                    dynamicData.type = type;
                    dynamicData.productionEffects = effects;
                    return dynamicData;
                }
            }
        }

        // Check for SeatPositionDisplay in parent (for sprite/title child tooltips)
        // ONLY if we're NOT in Head of State hierarchy
        if (!isInHeadOfStateHierarchy)
        {
            SeatPositionDisplay seatDisplay = GetComponentInParent<SeatPositionDisplay>();
            if (seatDisplay != null)
            {
                // Check if this is the sprite element (legend tooltip)
                if (gameObject.name.Contains("Sprite") || gameObject.GetComponent<Image>() != null)
                {
                    LegendData legend = seatDisplay.GetLegendData();
                    if (legend != null)
                    {
                        return CreateLegendTooltipData(legend);
                    }
                    // If no legend, show assignment prompt
                    dynamicData.tooltipTitle = "Click to assign a Legend";
                    return dynamicData;
                }
                
                // For title/text elements: show seat info
                var (title, description, type, effects) = seatDisplay.GetSeatTooltipData();
                if (!string.IsNullOrEmpty(title))
                {
                    dynamicData.tooltipTitle = title;
                    dynamicData.tooltipDescription = description;
                    dynamicData.type = type;
                    dynamicData.productionEffects = effects;
                    return dynamicData;
                }
            }
        }



        CivicDisplay civicDisplay = GetComponent<CivicDisplay>();
        if (civicDisplay != null)
        {
            return CreateCivicTooltipData(civicDisplay.GetCivicData());
        }

        CivicDetailedDisplay civicDetailedDisplay = GetComponent<CivicDetailedDisplay>();
        if (civicDetailedDisplay != null)
        {
            // For CivicDetailedDisplay, we need to get the civic data from the seat title
            // This requires a lookup through GovernmentLogic
            return CreateCivicDetailedTooltipData(civicDetailedDisplay);
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

            // Add production and construction cost modifier information
            if (GameUnitsLogic.Instance != null)
            {
                string modifierInfo = FormatProductionAndConstructionModifiers(productionSlot);
                if (!string.IsNullOrEmpty(modifierInfo))
                {
                    dynamicData.productionEffects += "\n\n" + modifierInfo;
                }
            }

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

            dynamicData.resourceRequirements = dynamicData.FormatTechnologyResourceRequirements(technologySlot.technologyData.resourceRequirements, technologySlot.technologyData.resourceAmount, availableResources, resourceProgress, technologySlot.isUnlocked);

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

    /// <summary>
    /// Format production and construction cost modifier information for tooltips
    /// </summary>
    /// <param name="productionSlot">The production slot to get modifier info for</param>
    /// <returns>Formatted string showing active modifiers</returns>
    private string FormatProductionAndConstructionModifiers(GameProductionSlot productionSlot)
    {
        if (GameUnitsLogic.Instance == null || productionSlot?.gameUnit == null) return "";

        var modifierInfo = new List<string>();
        
        // Get production efficiency modifiers
        float productionModifier = GameUnitsLogic.Instance.GetProductionEfficiencyModifier(
            productionSlot.gameUnit.type, 
            productionSlot.gameUnit.section
        );
        
        if (productionModifier != 0f)
        {
            string sign = productionModifier > 0 ? "+" : "";
            string color = productionModifier > 0 ? "green" : "red";
            modifierInfo.Add($"<color={color}>Production Efficiency: {sign}{productionModifier:F1}%</color>");
        }
        
        // Get construction cost modifiers
        float costModifier = GameUnitsLogic.Instance.GetConstructionCostModifier(
            productionSlot.gameUnit.type, 
            productionSlot.gameUnit.section
        );
        
        if (costModifier != 0f)
        {
            string effect = costModifier > 0 ? "increases" : "reduces";
            string color = costModifier > 0 ? "red" : "green";
            modifierInfo.Add($"<color={color}>Construction Cost: {effect} by {Mathf.Abs(costModifier):F1}%</color>");
        }
        
        // Get detailed modifier breakdown
        var productionModifiers = GameUnitsLogic.Instance.GetAllProductionModifiers();
        var constructionModifiers = GameUnitsLogic.Instance.GetAllConstructionCostModifiers();
        
        var detailedModifiers = new List<string>();
        
        // Add production modifier details
        foreach (var category in productionModifiers)
        {
            if (category.Key.Contains(productionSlot.gameUnit.type) || 
                category.Key.Contains(productionSlot.gameUnit.section))
            {
                foreach (var modifier in category.Value)
                {
                    string sign = modifier.Value >= 0 ? "+" : "";
                    string color = modifier.Value >= 0 ? "green" : "red";
                    detailedModifiers.Add($"<color={color}>• {category.Key}: {sign}{modifier.Value:F1}% from {modifier.Key}</color>");
                }
            }
        }
        
        // Add construction cost modifier details
        foreach (var category in constructionModifiers)
        {
            if (category.Key.Contains(productionSlot.gameUnit.type) || 
                category.Key.Contains(productionSlot.gameUnit.section))
            {
                foreach (var modifier in category.Value)
                {
                    string effect = modifier.Value > 0 ? "increases" : "reduces";
                    string color = modifier.Value > 0 ? "red" : "green";
                    detailedModifiers.Add($"<color={color}>• {category.Key}: {effect} cost by {Mathf.Abs(modifier.Value):F1}% from {modifier.Key}</color>");
                }
            }
        }
        
        // Build the final modifier info string
        if (modifierInfo.Count > 0 || detailedModifiers.Count > 0)
        {
            var result = new List<string>();
            
            if (modifierInfo.Count > 0)
            {
                result.Add("<b>Active Modifiers:</b>");
                result.AddRange(modifierInfo);
            }
            
            if (detailedModifiers.Count > 0)
            {
                result.Add("");
                result.Add("<b>Modifier Sources:</b>");
                result.AddRange(detailedModifiers);
            }
            
            return string.Join("\n", result);
        }
        
        return "";
    }

    /// <summary>
    /// Create tooltip data for a legend (from LeaderSlotDisplay or SeatPositionDisplay)
    /// </summary>
    private TooltipData CreateLegendTooltipData(LegendData legend)
    {
        if (legend == null) return null;
        
        TooltipData tooltipData = ScriptableObject.CreateInstance<TooltipData>();
        tooltipData.sourceObject = gameObject;
        
        // Title: Legend name
        tooltipData.tooltipTitle = legend.legendName;
        
        // Description: Personal quote
        tooltipData.tooltipDescription = legend.personalQuote;
        
        // Type: Rarity and class information (e.g., "Mythic Vanguard") - bold
        tooltipData.type = $"<b>{legend.rarity} {legend.legendClass}</b>";
        
        // Check if this legend is assigned to Head of State for multiplier display
        // For tooltip purposes, show doubled bonuses if legend is assigned to Head of State, regardless of activation status
        bool isHeadOfState = false;
        if (GovernmentLogic.Instance != null)
        {
            var headOfStateSeat = GovernmentLogic.Instance.GetCouncilSeat(-1);
            isHeadOfState = headOfStateSeat?.assignedLegend == legend; // Remove IsActive() check for tooltip display
        }
        
        // Effects: Auto-generated descriptions of all bonuses with bold header
        if (legend.bonuses != null && legend.bonuses.Count > 0)
        {
            var effectDescriptions = new List<string>();
            foreach (var bonus in legend.bonuses)
            {
                string effectText = bonus.GetAutoDescription();
                
                // If this is Head of State, show the doubled effect
                if (isHeadOfState)
                {
                    // Calculate the doubled value based on modifier type
                    float doubledValue = bonus.modifierValue * 2f; // Head of State multiplier
                    string originalText = bonus.GetAutoDescription();
                    
                    // Replace the value in the description with the doubled value
                    if (bonus.modifierType == ModifierType.Percentage)
                    {
                        // For percentage bonuses, show both original and doubled
                        effectText = $"{originalText} (Doubled: +{doubledValue}%)";
                    }
                    else if (bonus.modifierType == ModifierType.Add)
                    {
                        // For additive bonuses, show both original and doubled
                        effectText = $"{originalText} (Doubled: +{doubledValue})";
                    }
                    else
                    {
                        // For other types, just indicate it's doubled
                        effectText = $"{originalText} (Doubled)";
                    }
                }
                
                effectDescriptions.Add("- " + effectText);
            }
            
            string effectsHeader = isHeadOfState ? "Assignment Effects (Doubled):" : "Assignment Effects:";
            tooltipData.productionEffects = "\n<b>" + effectsHeader + "</b>\n" + string.Join("\n", effectDescriptions);
        }
        
        // If this is Head of State, also show seat bonuses
        if (isHeadOfState && GovernmentLogic.Instance != null)
        {
            var headOfStateSeat = GovernmentLogic.Instance.GetCouncilSeat(-1);
            if (headOfStateSeat?.seatBonuses != null && headOfStateSeat.seatBonuses.Count > 0)
            {
                var seatEffectDescriptions = new List<string>();
                foreach (var bonus in headOfStateSeat.seatBonuses)
                {
                    seatEffectDescriptions.Add("- " + bonus.GetAutoDescription());
                }
                
                // Add seat effects to the existing effects
                if (tooltipData.productionEffects != null)
                {
                    tooltipData.productionEffects += "\n<b>Seat Effects:</b>\n" + string.Join("\n", seatEffectDescriptions);
                }
                else
                {
                    tooltipData.productionEffects = "\n<b>Seat Effects:</b>\n" + string.Join("\n", seatEffectDescriptions);
                }
            }
        }
        
        // Resource Requirements: Flavor text (council assignment description)
        if (!string.IsNullOrEmpty(legend.councilAssignmentDescription))
        {
            tooltipData.resourceRequirements = legend.councilAssignmentDescription;
        }
        
        return tooltipData;
    }
    
    /// <summary>
    /// Create tooltip data for a council seat (from SeatPositionDisplay)
    /// </summary>
    private TooltipData CreateSeatTooltipData(CouncilSeat seat)
    {
        if (seat == null) return null;
        
        // If seat has a legend, show legend info, otherwise show seat info
        if (seat.assignedLegend != null)
        {
            return CreateLegendTooltipData(seat.assignedLegend);
        }
        
        // Show seat information when no legend is assigned
        TooltipData tooltipData = ScriptableObject.CreateInstance<TooltipData>();
        tooltipData.sourceObject = gameObject;
        
        // Title: Seat title
        tooltipData.tooltipTitle = seat.GetEffectiveTitle();
        
        // Description: Seat description
        tooltipData.tooltipDescription = seat.roleplayDescription;
        
        // Type: Allowed classes with bold header and dash separation
        if (seat.allowedLegendClasses != null && seat.allowedLegendClasses.Count > 0)
        {
            tooltipData.type = "<b>Allowed Classes:</b>\n" + string.Join(" - ", seat.allowedLegendClasses);
        }
        else
        {
            tooltipData.type = "<b>Allowed Classes:</b>\nAny/All Classes";
        }
        
        // Effects: Seat bonuses with bold header
        if (seat.seatBonuses != null && seat.seatBonuses.Count > 0)
        {
            var effectDescriptions = new List<string>();
            foreach (var bonus in seat.seatBonuses)
            {
                effectDescriptions.Add("- " + bonus.GetAutoDescription());
            }
            tooltipData.productionEffects = "\n<b>Assignment Effects:</b>\n" + string.Join("\n", effectDescriptions);
        }
        
        return tooltipData;
    }
    
    /// <summary>
    /// Create tooltip data for a civic (from CivicDisplay)
    /// </summary>
    private TooltipData CreateCivicTooltipData(CivicData civic)
    {
        if (civic == null) return null;
        
        TooltipData tooltipData = ScriptableObject.CreateInstance<TooltipData>();
        tooltipData.sourceObject = gameObject;
        
        // Title: Civic name
        tooltipData.tooltipTitle = civic.civicName;
        
        // Description: Flavor description
        tooltipData.tooltipDescription = civic.description;
        
        // Type: Rarity, tier, and civic type (e.g., "Mythic Aeonic Civic") - bold
        tooltipData.type = $"<b>{civic.rarity} {civic.tier} Civic</b>";
        
        // Effects: Auto-generated descriptions of all effects with bold header
        if (civic.effects != null && civic.effects.Count > 0)
        {
            var effectDescriptions = new List<string>();
            foreach (var effect in civic.effects)
            {
                effectDescriptions.Add("- " + effect.GetAutoDescription());
            }
            tooltipData.productionEffects = "\n<b>Assignment Effects:</b>\n" + string.Join("\n", effectDescriptions);
        }
        
        // Resource Requirements: Council position info (if grants one)
        if (civic.grantsCouncilPosition && civic.councilPosition != null && !string.IsNullOrEmpty(civic.councilPosition.title))
        {
            tooltipData.resourceRequirements = $"Grants {civic.councilPosition.title} Council Position";
        }
        
        return tooltipData;
    }
    
    /// <summary>
    /// Create tooltip data for a civic detailed display (from CivicDetailedDisplay)
    /// </summary>
    private TooltipData CreateCivicDetailedTooltipData(CivicDetailedDisplay civicDetailed)
    {
        if (civicDetailed == null) return null;
        
        string seatTitle = civicDetailed.GetSeatTitle();
        if (string.IsNullOrEmpty(seatTitle)) return null;
        
        // Get seat information from GovernmentLogic
        if (GovernmentLogic.Instance != null)
        {
            var (title, effects, leaderClasses, icon) = GovernmentLogic.Instance.GetSeatDisplayInfo(seatTitle);
            
            TooltipData tooltipData = ScriptableObject.CreateInstance<TooltipData>();
            tooltipData.sourceObject = gameObject;
            
            // Title: Seat title
            tooltipData.tooltipTitle = title;
            
            // Try to get civic data if this is a civic seat
            var civicData = GovernmentLogic.Instance.GetCivicDataForSeatTitle(seatTitle);
            if (civicData != null)
            {
                // This is a civic seat, use civic data
                tooltipData.tooltipDescription = civicData.description;
                tooltipData.type = $"<b>{civicData.rarity} {civicData.tier} Civic</b>";
                
                if (civicData.grantsCouncilPosition && civicData.councilPosition != null && !string.IsNullOrEmpty(civicData.councilPosition.title))
                {
                    tooltipData.resourceRequirements = $"Grants {civicData.councilPosition.title} Council Position";
                }
            }
            else
            {
                // This is a default seat
                tooltipData.tooltipDescription = "Default council position";
                tooltipData.type = "Default Seat";
            }
            
            // Effects: Use the formatted effects from GetSeatDisplayInfo
            if (!string.IsNullOrEmpty(effects) && effects != "N/A")
            {
                tooltipData.productionEffects = effects;
            }
            
            // Tech Requirements: Show leader class restrictions
            if (!string.IsNullOrEmpty(leaderClasses) && leaderClasses != "N/A")
            {
                tooltipData.techRequirements = $"Allowed Classes: {leaderClasses}";
            }
            else
            {
                tooltipData.techRequirements = "Any/All Classes";
            }
            
            return tooltipData;
        }
        
        return null;
    }
    
    /// <summary>
    /// Create tooltip data for HeadOfState seat (similar to SeatPositionDisplay)
    /// </summary>
    private (string title, string description, string type, string effects) CreateHeadOfStateSeatTooltipData(CouncilSeat headOfStateSeat)
    {
        if (headOfStateSeat == null) return ("", "", "", "");
        
        // Always show seat information for title hover
        string allowedClasses;
        if (headOfStateSeat.allowedLegendClasses == null || headOfStateSeat.allowedLegendClasses.Count == 0)
        {
            allowedClasses = "Any/All Classes";
        }
        else if (headOfStateSeat.allowedLegendClasses.Count == 6) // All 6 classes
        {
            allowedClasses = "Any/All Classes";
        }
        else
        {
            allowedClasses = string.Join(" - ", headOfStateSeat.allowedLegendClasses);
        }
        
        // Add seat bonuses as Assignment Effects
        string assignmentEffects = "";
        if (headOfStateSeat.seatBonuses != null && headOfStateSeat.seatBonuses.Count > 0)
        {
            var effectDescriptions = new List<string>();
            foreach (var bonus in headOfStateSeat.seatBonuses)
            {
                effectDescriptions.Add("- " + bonus.GetAutoDescription());
            }
            assignmentEffects = "\n<b>Assignment Effects:</b>\n" + string.Join("\n", effectDescriptions);
        }
        
        return (headOfStateSeat.GetEffectiveTitle(), headOfStateSeat.roleplayDescription, $"<b>Allowed Classes:</b>\n{allowedClasses}", assignmentEffects);
    }

}
