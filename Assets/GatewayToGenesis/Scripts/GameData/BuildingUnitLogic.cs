using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingUnitLogic : MonoBehaviour
{
    public static BuildingUnitLogic Instance;

    public List<GameUnit> productionUnits = new List<GameUnit>();

    public List<GameObject> sections = new List<GameObject>(), productionSlots = new List<GameObject>();

    [SerializeField] private GameObject productionTabContent, newSectionPrefab, newResourceSlotPrefab;

    private GameObject section;

    public GameUnit testProductionUnit;

    private void Awake()
    {
        Instance = this;
    }

}
