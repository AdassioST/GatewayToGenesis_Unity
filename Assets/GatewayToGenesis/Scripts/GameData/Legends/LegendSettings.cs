using System;
using UnityEngine;

/// <summary>
/// The numbers behind a legend's Soul Leitmotif and Legend Traits. Proposals, like <see cref="ComposureTuning"/>:
/// the vault fixes the +5 of a primary binding and each origin trait's Binding Effect; the rest waits for the owner
/// (roadmap D08).
/// </summary>
[Serializable]
public class SoulTuning
{
    [Tooltip("Every binding's score before traits (5 is the vault's Apprentice: \"can cast Minor Notes of various rhythms\"; Age 0 is \"mostly Minor Note magic\").")]
    public int baseScore = 5;
    [Tooltip("The Soul Leitmotif's primary binding (vault: \"A primary binding always gives +5 of proficiency to said element\").")]
    public int leitmotifBonus = 5;
    [Tooltip("Each Ornament's binding.")]
    public int ornamentBonus = 3;
    [Tooltip("A personality trait that evolved along a binding (vault: traits \"solidify into a specific element, giving further mastery\").")]
    public int evolvedTraitBonus = 2;

    [Header("Origin trait budget by rarity (the highest Scaled Cost a drawn origin trait may have)")]
    public int commonBudget = 0;
    public int uncommonBudget = 25;
    public int rareBudget = 75;
    public int epicBudget = 125;
    public int mythicBudget = 150;

    public int BudgetFor(GameRarity rarity)
    {
        switch (rarity)
        {
            case GameRarity.Common: return commonBudget;
            case GameRarity.Uncommon: return uncommonBudget;
            case GameRarity.Rare: return rareBudget;
            case GameRarity.Epic: return epicBudget;
            default: return mythicBudget;
        }
    }
}

/// <summary>
/// Tuning for legends (Resources/Legends/LegendSettings.asset): Composure, the Soul Leitmotif and Lyrical Fragments. Read through
/// <see cref="LegendLore"/>; without the asset the code defaults apply.
/// </summary>
[CreateAssetMenu(fileName = "LegendSettings", menuName = "Game Object/Legend Settings", order = 7)]
public class LegendSettings : ScriptableObject
{
    public ComposureTuning composure = new ComposureTuning();
    public SoulTuning soul = new SoulTuning();
    public FragmentTuning fragments = new FragmentTuning();
}
