using UnityEngine;

/// <summary>
/// A job a resource does for the population. Systems find the resource by its role
/// (<see cref="GameCatalog.ResourceFor"/>), never by name, so a resource can be renamed freely.
/// <see cref="ContentValidator"/> requires exactly one resource for each role. Append new values; never reorder.
/// </summary>
public enum ResourceRole
{
    None = 0,
    /// <summary>What the population eats: stored amount buys growth, demand is subtracted from it, running out starves.</summary>
    Food = 1,
    /// <summary>What every citizen generates (<see cref="PopGrowthLogic.researchPerPopulation"/> per citizen per second).</summary>
    Research = 2,
}

[CreateAssetMenu(fileName = "New Game Unit", menuName = "Game Object/Unit", order = 1)]
public class GameUnit : ScriptableObject
{
    public new string name = "";
    public string type = "", section = "", description = "";

    public Sprite icon;

    [Tooltip("Resources only: the job this resource does for the population. Exactly one resource per role.")]
    public ResourceRole role;
}
