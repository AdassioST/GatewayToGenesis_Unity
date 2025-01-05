using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using static System.Collections.Specialized.BitVector32;

public class TabBuilderLogic : MonoBehaviour
{
    public List<GameUnit> units = new List<GameUnit>();

    public List<GameObject> sections = new List<GameObject>(), slots = new List<GameObject>();

    [SerializeField] private GameObject tabContent, newSectionPrefab, newSlotPrefab;

    private GameObject section;

    public GameUnit testUnit;
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

        //PREVENT DUPLICATES ON UNIT LISTS
        bool isUnitPresent = units.Any(r => r.name == unit.name);

        if (!isUnitPresent)
        {
            GameObject newSlot = Instantiate(newSlotPrefab);
            newSlot.name = unit.name;

            Transform slotsParent = section.transform.Find("Slots");

            if (slotsParent != null)
            {
                // Attach to "Slots" child if it exists
                newSlot.transform.SetParent(slotsParent, false);
            }
            else
            {
                // Attach directly to the section if "Slots" child doesn't exist
                newSlot.transform.SetParent(section.transform, false);
            }

            IGameUnitSlot slotComponent = newSlot.GetComponent<IGameUnitSlot>();

            if (slotComponent != null)
            {
                slotComponent.gameUnit = unit;
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
