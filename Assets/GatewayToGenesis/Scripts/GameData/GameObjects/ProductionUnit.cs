using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Production Unit", menuName = "Game Object/Production Unit", order = 2)]
public class ProductionUnit : GameUnit
{
    public List<string> producedResources = new List<string>();
    public List<string> inputResources = new List<string>();
    public List<string> buildResourceRequirements = new List<string>();

    public List<float> productionRates = new List<float>();
    public List<float> inputRates = new List<float>();
    public List<float> buildRequirementsAmount = new List<float>();
}
