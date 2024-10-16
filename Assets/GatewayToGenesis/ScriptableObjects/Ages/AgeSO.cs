using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Age", menuName = "Age", order = 2)]
public class AgeSO : ScriptableObject
{
    public int id = 1, age = 1, goldenEraScore, darkEraScore;

    public string ageName, ageQuote = "", ageType = "", eraDescription = "", crisisName = "", crisisDescription = "";

    public bool hasOutcomes;
        
    public bool[] crisisTriggers, crisisRequirements;
}
