using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Production Unit Data", menuName = "Game Object/Production Unit Data", order = 2)]
public class ProductionUnitData : ScriptableObject
{
    public GameUnit gameUnit;

    public bool isUnique;

    public int housing;

    public List<string> buildResourceRequirements = new List<string>();
    public List<float> buildRequirementsAmount = new List<float>();

    public List<string> producedResources = new List<string>();
    public List<float> productionRates = new List<float>();

    public List<string> consumedResources = new List<string>();
    public List<float> consumeRates = new List<float>();

    public List<string> storageResources = new List<string>();
    public List<float> storageAmount = new List<float>();
}