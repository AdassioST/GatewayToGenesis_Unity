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

    public GameUnit testUnit;

    private GlobalProductionManager globalProductionManager;

    private void Start()
    {
        globalProductionManager = FindObjectOfType<GlobalProductionManager>();
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
                slotComponent.gameUnit = unit;

                // Directly add the slot to the GlobalProductionManager
                if (slotComponent is GameResourceSlot resourceSlot)
                {
                    globalProductionManager.AddResourceSlot(resourceSlot);
                }
                else if (slotComponent is GameProductionSlot productionSlot)
                {
                    globalProductionManager.AddProductionSlot(productionSlot);
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

    private void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            AddNewUnit(testUnit);
        }
    }
}

