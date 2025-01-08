using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Game Object/Technology Data", order = 3)]
public class TechnologyData : ScriptableObject
{
    public GameUnit gameUnit;

    public bool isEventTech;

    public int tier;

    public List<string> techRequirements = new List<string>(); // List of prerequisite technologies.

    public List<string> resourceRequirements = new List<string>();
    public List<float> resourceAmount = new List<float>();

    public List<EurekaCondition> eurekaConditions = new List<EurekaCondition>();

    public List<TechUnlockable> techUnlockables = new List<TechUnlockable>(); // Unlockable items

}

// Eureka Condition Enum
public enum EurekaType
{
    GatherResourceFromClick,
    ConstructUnit,
    WitnessEvent,
    AccumulateResource,
    UnlockTechnology,
    ReachProductionRate
}

// Eureka Condition Class
[System.Serializable]
public class EurekaCondition
{
    public EurekaType type;
    public string trigger; // Trigger resource, unit, or event name.
    public float requiredAmount; // Amount required for conditions like gathering or accumulating.
    public string description;
}
