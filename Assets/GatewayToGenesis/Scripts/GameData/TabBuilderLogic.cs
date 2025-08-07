using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TabBuilderLogic : MonoBehaviour
{
    public List<GameUnit> units = new List<GameUnit>();
    public List<GameObject> sections = new List<GameObject>(), slots = new List<GameObject>();

    [SerializeField] private GameObject tabContent, newSectionPrefab, newSlotPrefab;
    
    // Tab type identification
    [Header("Tab Configuration")]
    [SerializeField] public TabType tabType;
    
    // HUD Production Selection references (only for Storage tab)
    [Header("HUD Production Selection (Storage Only)")]
    [SerializeField] public GameObject productionSelectionSlotPrefab;
    [SerializeField] public Transform hudProductionSelectionContent;

    private GameObject section;

    public GameUnit initializationUnit;

    public bool hasInitializationUnit;

    // Enum to identify tab types
    public enum TabType
    {
        Storage,
        Production,
        Technology
    }

    private void Start()
    {
        if (hasInitializationUnit)
        {
            AddNewUnit(initializationUnit);
        }
        
        // Only create HUD production selection slots for existing resources if this is the Storage tab
        if (tabType == TabType.Storage)
        {
            CreateHUDProductionSelectionSlotsForExistingResources();
        }
    }

    public void AddNewUnit(GameUnit unit)
    {
        bool isSectionActive = sections.Any(x => x.name == unit.section);

        if (!isSectionActive)
        {
            GameObject newSection = Instantiate(newSectionPrefab);
            newSection.name = unit.section;
            newSection.transform.SetParent(tabContent.transform);

            sections.Add(newSection);
            section = newSection;

            Transform name = section.transform.Find("Banner/Name");
            name.GetComponent<TextMeshProUGUI>().text = unit.section;

            // Format tooltip trigger
            SectionData sectionData = SectionData.GetSectionData(unit.section);
            if (sectionData != null)
            {
                Transform banner = section.transform.Find("Banner");
                TooltipTrigger tooltipTrigger = banner.GetComponent<TooltipTrigger>();
                if (tooltipTrigger != null)
                {
                    tooltipTrigger.useCustomTooltip = true;
                    tooltipTrigger.customTitle = sectionData.title;
                    tooltipTrigger.customDescription = sectionData.description;
                    tooltipTrigger.customType = sectionData.name;
                }
            }
        }
        else
        {
            section = sections.Find((x) => x.name == unit.section);
        }

        bool isUnitPresent = units.Any(r => r.name == unit.name);

        if (!isUnitPresent)
        {
            GameObject newSlot = Instantiate(newSlotPrefab);
            newSlot.name = unit.name;

            Transform slotsParent = section.transform.Find("Slots");
            if (slotsParent != null)
            {
                newSlot.transform.SetParent(slotsParent, false);
            }
            else
            {
                newSlot.transform.SetParent(section.transform, false);
            }

            IGameUnitSlot slotComponent = newSlot.GetComponent<IGameUnitSlot>();
            if (slotComponent != null)
            {
                // Initialize the slot properly based on its type
                if (slotComponent is GameProductionSlot productionSlot)
                {
                    productionSlot.InitializeSlot(unit);
                }
                else
                {
                    slotComponent.gameUnit = unit;
                }

                AddSlotToGlobalManager(slotComponent);

                // Handle resource initialization and HUD creation for Storage tab
                if (slotComponent is GameResourceSlot resourceSlot)
                {
                    string resourceName = resourceSlot.gameUnit.name;

                    if (!GameUnitsLogic.Instance.storageBreakdown.ContainsKey(resourceName))
                    {
                        GameUnitsLogic.Instance.storageBreakdown[resourceName] = new Dictionary<string, float>{{ "Base", resourceSlot.maxAmount }};
                    }
                    
                    // Create HUD Production Selection Slot for new resources (only in Storage tab)
                    if (tabType == TabType.Storage)
                    {
                        CreateHUDProductionSelectionSlot(unit);
                        RefreshVitalSelectionWindowIfOpen(unit);
                    }
                }
            }

            units.Add(unit);
            slots.Add(newSlot);
        }
    }

    // Create ProductionSelectionSlot in HUD for new resources (Storage tab only)
    private void CreateHUDProductionSelectionSlot(GameUnit gameUnit)
    {
        if (tabType != TabType.Storage || productionSelectionSlotPrefab == null || hudProductionSelectionContent == null)
        {
            return;
        }

        string targetButton = GetTargetButtonForResource(gameUnit);
        if (targetButton == null) return;

        // Check if slot already exists to avoid duplicates
        Transform existingSlot = hudProductionSelectionContent.Find(gameUnit.name);
        if (existingSlot == null)
        {
            CreateNewHUDSelectionSlot(gameUnit, targetButton);
        }
    }

    private string GetTargetButtonForResource(GameUnit gameUnit)
    {
        if (gameUnit.type == "Building Material")
        {
            return "BuildingMaterial";
        }
        else if (gameUnit.type == "Vital Resource" || gameUnit.name == "Food")
        {
            return "VitalResource";
        }
        return null;
    }

    private void CreateNewHUDSelectionSlot(GameUnit gameUnit, string targetButton)
    {
        GameObject newSelectionSlot = Instantiate(productionSelectionSlotPrefab, hudProductionSelectionContent);
        newSelectionSlot.name = gameUnit.name;
        
        ProductionSelectionSlot selectionSlot = newSelectionSlot.GetComponent<ProductionSelectionSlot>();
        if (selectionSlot != null)
        {
            selectionSlot.InitializeSelectionSlot(gameUnit, targetButton);
        }
    }

    // Create HUD production selection slots for all existing resources (Storage tab only)
    private void CreateHUDProductionSelectionSlotsForExistingResources()
    {
        if (tabType != TabType.Storage || productionSelectionSlotPrefab == null || hudProductionSelectionContent == null)
        {
            return;
        }

        // Clear existing selection slots
        foreach (Transform child in hudProductionSelectionContent)
        {
            Destroy(child.gameObject);
        }
        
        // Create selection slots for all existing resources
        foreach (GameUnit unit in units)
        {
            CreateHUDProductionSelectionSlot(unit);
        }
    }

    private void AddSlotToGlobalManager(IGameUnitSlot slotComponent)
    {
        if (slotComponent is GameResourceSlot resourceSlot)
        {
            GlobalProductionManager.Instance.AddResourceSlot(resourceSlot);
        }
        else if (slotComponent is GameProductionSlot productionSlot)
        {
            GlobalProductionManager.Instance.AddProductionSlot(productionSlot);
        }
        else if (slotComponent is GameTechnologySlot technologySlot)
        {
            GlobalProductionManager.Instance.AddTechnologySlot(technologySlot);
        }
    }

    // Public method to get HUD production selection content
    public Transform GetHUDProductionSelectionContent()
    {
        return hudProductionSelectionContent;
    }

    private void RefreshVitalSelectionWindowIfOpen(GameUnit newResource)
    {
        VitalSelectionWindow vitalWindow = FindObjectOfType<VitalSelectionWindow>();
        if (vitalWindow != null && vitalWindow.gameObject.activeSelf)
        {
            // Directly create the new slot in the VitalSelectionWindow
            vitalWindow.CreateSlotForNewResource(newResource);
        }
    }
}