using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Production", menuName = "Production", order = 1)]
public class ProductionSO : ScriptableObject
{
    //Datos del Excel

    public int id = 1, tier = 1, limit, productionCost;

    public string productionName = "", productionType = "", productionDescription = "", productionMaterial = "Elderwood";

    public bool hasSpecialProperties, hasLimit;

    //Datos del Gayvian
}
