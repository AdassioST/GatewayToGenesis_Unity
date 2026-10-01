using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One kind of stored food: a resource of the Stored Food section and what it is worth against Food.</summary>
[Serializable]
public class FoodKind
{
    [Tooltip("The resource (its GameUnit in Resources/GameUnits/GameResources, section Stored Food).")]
    public string resource;
    [Tooltip("Food it is worth: 0.5 means two of it feed like one Food; 5 means one feeds like five.")]
    public float foodValue = 1f;
    [Tooltip("Share of the stock that spoils each Seventh (0.02 = 2%).")]
    [Range(0f, 1f)] public float spoilPerSeventh = 0.02f;
    [Tooltip("An Auric peach food: stores made mostly of these count as monocrop drift for the famine.")]
    public bool peach;
    [Tooltip("What it is to the kitchen: Edible (actual food), Ingredient (oils, seeds, pits: eaten raw only once the edibles run out), Spice (some Eleos Blooms and other savours: never eaten to fill a belly) Tea (Eleos brews and everyday infusions: drunk like food, their own category) or Beverage (ales, meads, wines and spirits from the cellar, never from an Eleos Bloom: drunk for joy, opened for hunger last of all). The culture's foodways read it.")]
    public FoodClass cuisine = FoodClass.Edible;
    [Tooltip("A resource kept outside the Stored Food section (a spice gathered in the Great Fields): the pantry leaves its room alone.")]
    public bool keepsOwnRoom;
    [Tooltip("What eating it leaves behind for the kitchen (Dried Auric Peaches leave Peach Pits), or empty.")]
    public string leaves;
    [Tooltip("How much of that is left per unit eaten.")]
    public float leavesPerUnit;
}

/// <summary>A technology that keeps food longer or stores more of it.</summary>
[Serializable]
public class PreservationSpec
{
    public string technology;
    [Tooltip("Spoilage is multiplied by this once researched (0.5 halves it).")]
    [Range(0f, 1f)] public float spoilMultiplier = 1f;
    [Tooltip("Room added to every kind of stored food.")]
    public float capacityBonus;
}

/// <summary>
/// The civilization's stores of food (Resources/Food/Pantry), read by <see cref="Pantry"/>: the kinds, how much each
/// is worth and how fast it spoils, the room for each, what the survivors start with and the technologies that
/// preserve them. The kinds' names follow the vault (The Inescapable Hunger); every number is a proposal.
/// </summary>
[CreateAssetMenu(fileName = "Pantry", menuName = "Game Object/Pantry Settings", order = 13)]
public class PantrySettings : ScriptableObject
{
    public List<FoodKind> kinds = new List<FoodKind>();
    [Tooltip("Room for each kind before preservation technologies.")]
    public float capacity = 200f;
    public List<PreservationSpec> preservation = new List<PreservationSpec>();
    [Tooltip("What the survivors' cellars hold when a world begins.")]
    public List<ResourceAmount> startingStores = new List<ResourceAmount>();
    [Tooltip("Most food value drawn from the stores per second to cover a shortfall of Food.")]
    public float maxCoverPerSecond = 5f;
    [Tooltip("A kind counts toward the variety of the stores from this much food value.")]
    public float varietyMinimumValue = 5f;

    [Header("Surplus into the stores")]
    [Tooltip("Once the founders are all in and this technology is known, Food gathered beyond keepInHand goes into the stores.")]
    public string bankTechnology = "Ash-Cellars";
    [Tooltip("The kind of stored food the surplus Food becomes (at its food value: one Food makes 1 / foodValue of it).")]
    public string bankedKind = "Dried Auric Peaches";
    [Tooltip("Food kept in hand (the rest is stored): enough to let one survivor in at the gates.")]
    public float keepInHand = 12f;

    public FoodKind Kind(string resource) => kinds.Find(k => k != null && string.Equals(k.resource, resource, StringComparison.OrdinalIgnoreCase));
}
