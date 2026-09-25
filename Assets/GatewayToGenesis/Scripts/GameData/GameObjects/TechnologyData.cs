using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Game Object/Technology Data", order = 3)]
public class TechnologyData : ScriptableObject
{
    public GameUnit gameUnit;

    [Tooltip("Unlocking this technology triggers an event check, so events conditioned on it can fire immediately.")]
    public bool isEventTech;

    public int tier;

    public int satisfactionPoints = 0;

    public List<string> techRequirements = new List<string>(); // Prerequisite technologies by name.

    public List<string> resourceRequirements = new List<string>();
    public List<float> resourceAmount = new List<float>();

    // Renamed from "eureka"; the attribute keeps conditions authored before the rename.
    [FormerlySerializedAs("eurekaConditions")]
    public List<EnlightenedCondition> enlightenedConditions = new List<EnlightenedCondition>();

    public List<TechUnlockable> techUnlockables = new List<TechUnlockable>();
}

public enum EnlightenedType
{
    GatherResourceFromClick,
    ConstructUnit,
    WitnessEvent,
    AccumulateResource,
    UnlockTechnology,
    ReachProductionRate
}

/// <summary>
/// A goal that enlightens a technology early (making it visible and granting bonus research progress).
/// Evaluated through <see cref="GameValues"/>: every type maps to one value domain.
/// </summary>
[System.Serializable]
public class EnlightenedCondition
{
    public EnlightenedType type;
    public string trigger;        // Resource, production unit, event or technology name.
    public float requiredAmount;  // Threshold for counting conditions.
    public string description;

    public bool IsMet()
    {
        switch (type)
        {
            case EnlightenedType.GatherResourceFromClick: return GameValues.Get("clicked", trigger) >= requiredAmount;
            case EnlightenedType.ConstructUnit: return GameValues.Get("building", trigger) >= Mathf.Max(1f, requiredAmount);
            case EnlightenedType.WitnessEvent: return GameValues.Get("event_completed", trigger) >= Mathf.Max(1f, requiredAmount);
            case EnlightenedType.AccumulateResource: return GameValues.Get("resource", trigger) >= requiredAmount;
            case EnlightenedType.UnlockTechnology: return GameValues.Get("technology", trigger) >= 1f;
            case EnlightenedType.ReachProductionRate: return GameValues.Get("production_rate", trigger) >= requiredAmount;
            default: return false;
        }
    }
}
