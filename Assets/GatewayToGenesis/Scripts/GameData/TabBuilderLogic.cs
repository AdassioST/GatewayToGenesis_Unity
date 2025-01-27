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

    private GameObject section;

    public GameUnit initializationUnit;

    public bool hasInitializationUnit;

    private void Start()
    {
        if (hasInitializationUnit)
        {
            AddNewUnit(initializationUnit);
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

            //FORMAT TOOLTIP TRIGGER

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

            else
            {
                Debug.LogWarning($"No SectionData found for section: {unit.section}");
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
                    productionSlot.InitializeSlot(unit); // Initialize with the GameUnit
                }
                else
                {
                    slotComponent.gameUnit = unit; // For other slot types
                }

                AddSlotToGlobalManager(slotComponent);

                // Handle resource initialization dynamically
                if (slotComponent is GameResourceSlot resourceSlot)
                {
                    string resourceName = resourceSlot.gameUnit.name;

                    if (!GameUnitsLogic.Instance.storageBreakdown.ContainsKey(resourceName))
                    {
                        GameUnitsLogic.Instance.storageBreakdown[resourceName] = new Dictionary<string, float>{{ "Base", resourceSlot.maxAmount }};

                    }
                }

            }
            else
            {
                Debug.LogError("The instantiated slot does not implement IGameUnitSlot");
            }

            units.Add(unit);
            slots.Add(newSlot);
        }
        else
        {
            Debug.LogWarning($"Unit {unit.name} is already in section {section.name}");
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
        else
        {
            Debug.LogWarning("Slot does not match any known slot types.");
        }
    }
}