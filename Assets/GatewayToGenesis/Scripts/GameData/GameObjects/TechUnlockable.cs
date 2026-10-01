using UnityEngine;

[CreateAssetMenu(fileName = "New Tech Unlockable", menuName = "Game Object/Tech Unlockable", order = 4)]
public class TechUnlockable : ScriptableObject
{
    public GameUnit gameUnit;

    public Sprite slotImage;

    public string description, effects;

    [Tooltip("ClickPower: flat click power added. Modifier: % output of the resource. DemandModifier: % reduction of population food demand. GrimoireSeat / SpellWildcard: how many. ScoreChange: points.")]
    public float resourceModifier;
    public TechUnlockableType unlockableType;

    [Header("Grimoire (Symphony Cards)")]
    [Tooltip("GrimoireSeat: a Symphony seat (the deck) or a Ceremony (three voice seats).")]
    public GrimoireSeat seat;
    [Tooltip("SymphonyCard: the scripted card granted.")]
    public SymphonyCardData symphonyCard;

    [Header("Score")]
    [Tooltip("ScoreChange: the event score changed by resourceModifier points (\"monocrop\").")]
    public string score;
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
    CouncilSeat,    // Opens the next council position (resourceModifier: how many, at least 1)
    GrimoireSeat,   // Opens Grimoire seats (seat: Symphony or Ceremony; resourceModifier: how many, at least 1)
    SymphonyCard,   // Grants a scripted Symphony Card (symphonyCard)
    SpellWildcard,  // Grants blank cards for the Spell Maker (resourceModifier: how many, at least 1)
    ScoreChange     // Changes an event score once, when researched (score, resourceModifier points)
}
