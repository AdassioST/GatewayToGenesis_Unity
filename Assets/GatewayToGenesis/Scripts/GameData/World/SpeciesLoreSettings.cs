using UnityEngine;

/// <summary>Every number of the bestiary's memory (<see cref="SpeciesLore"/>), in Resources/World/SpeciesLore. All proposals (Canon Gaps).</summary>
[CreateAssetMenu(fileName = "SpeciesLore", menuName = "Gateway to Genesis/Species Lore")]
public class SpeciesLoreSettings : ScriptableObject
{
    public SpeciesLoreTuning tuning = new SpeciesLoreTuning();
}
