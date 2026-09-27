using UnityEngine;

[CreateAssetMenu(fileName = "New Tech Unlockable", menuName = "Game Object/Tech Unlockable", order = 4)]
public class TechUnlockable : ScriptableObject
{
    public GameUnit gameUnit;

    public Sprite slotImage;

    public string description, effects;

    [Tooltip("ClickPower: flat click power added. Modifier: % output of the resource. DemandModifier: % reduction of population food demand.")]
    public float resourceModifier;
    public TechUnlockableType unlockableType;
}

/// <summary>What a technology grants. Values are serialized by index: append new types at the end.</summary>
public enum TechUnlockableType
{
    Arts,           // Cultural unlock (no mechanical effect yet)
    Building,       // Adds a building to Production
    ClickPower,     // resourceModifier > 0: + click power; otherwise discovers the resource
    Modifier,       // + resourceModifier % output of gameUnit
    Unit,           // Adds a unit to Production
    Special,        // Named hook handled by GameUnitsLogic (e.g. "Horology", "Vagrants")
    DemandModifier, // - resourceModifier % population food demand
    CouncilSeat     // Opens the next council position (resourceModifier: how many, at least 1)
}
