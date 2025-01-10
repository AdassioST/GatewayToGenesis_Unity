using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TechnologyTreeLogic : MonoBehaviour
{
    [SerializeField] private GameObject emptySlotPrefab, slots;

    [SerializeField] private List<TechnologyData> eraTechnologies = new List<TechnologyData>();

    private TabBuilderLogic tabBuilderLogic;

    private void Start()
    {
        tabBuilderLogic = GetComponentInParent<TabBuilderLogic>();

        if (tabBuilderLogic == null)
        {
            Debug.LogError("TabBuilderLogic component not found in parent");
        }

        InitializeTree(eraTechnologies);
    }

    public void InitializeTree(List<TechnologyData> technologies)
    {
        //ClearSlots();

        BuildTechTree(technologies);
    }

    private void ClearSlots()
    {
        foreach (Transform child in slots.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void BuildTechTree(List<TechnologyData> technologies)
    {
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyData techData = technologies[i];

            if (techData != null)
            {
                tabBuilderLogic.AddNewUnit(techData.gameUnit);
            }
            else
            {
                Instantiate(emptySlotPrefab, slots.transform);
            }
        }
    }
}

