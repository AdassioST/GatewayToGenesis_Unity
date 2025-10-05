using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Game Object/Technology Data", order = 3)]
public class TechnologyData : ScriptableObject
{
    public GameUnit gameUnit;

    public bool isEventTech;

    public int tier;

    public int satisfactionPoints = 0;

    public List<string> techRequirements = new List<string>(); // List of prerequisite technologies.

    public List<string> resourceRequirements = new List<string>();
    public List<float> resourceAmount = new List<float>();

    public List<EnlightenedCondition> enlightenedConditions = new List<EnlightenedCondition>();

    public List<TechUnlockable> techUnlockables = new List<TechUnlockable>(); // Unlockable items

}

// Enlightened Condition Enum
public enum EnlightenedType
{
    GatherResourceFromClick,
    ConstructUnit,
    WitnessEvent,
    AccumulateResource,
    UnlockTechnology,
    ReachProductionRate
}

// Enlightened Condition Class
[System.Serializable]
public class EnlightenedCondition
{
    public EnlightenedType type;
    public string trigger; // Trigger resource, unit, or event name.
    public float requiredAmount; // Amount required for conditions like gathering or accumulating.
    public string description;
}
