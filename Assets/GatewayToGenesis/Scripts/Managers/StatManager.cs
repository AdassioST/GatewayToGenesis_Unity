using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Manages civilization stats with 4 Pillars, 8 Substats, and derived calculations
/// </summary>
public class StatManager : MonoBehaviour
{
    [Header("Pillars")]
    [SerializeField] private int aureus = 10, regalia = 10, waltz = 10, chorus = 10;

    [Header("Substats")]
    [SerializeField] private int innovation = 5, piety = 5, authority = 5, ambition = 5;
    [SerializeField] private int symphony = 5, endurance = 5, arcane = 5, secrecy = 5;

    [Header("Derived Stats")]
    [SerializeField] private float discoveryRate, savingRollChance, legendEffectiveness;
    [SerializeField] private float expeditionCostMod, expeditionTimeMod, satisfactionEffectiveness;
    [SerializeField] private float moraleLossMod, moraleRecoveryMod, clickPowerBonus, magicEffectiveness;
    [SerializeField] private int communionStage;

    // Core dictionaries for easy access
    private Dictionary<string, int> pillars = new Dictionary<string, int>();
    private Dictionary<string, int> substats = new Dictionary<string, int>();
    private Dictionary<string, float> derived = new Dictionary<string, float>();

    // Events for UI updates
    public System.Action<string, int> OnPillarChanged;
    public System.Action<string, int> OnSubstatChanged;
    public System.Action<string, float> OnDerivedChanged;

    // Pillar-substat relationships
    private readonly Dictionary<string, string[]> pillarSubstats = new Dictionary<string, string[]>
    {
        {"aureus", new[] {"innovation", "piety"}},
        {"regalia", new[] {"authority", "ambition"}},
        {"waltz", new[] {"symphony", "endurance"}},
        {"chorus", new[] {"arcane", "secrecy"}}
    };

    // Substat-derivation formulas
    private readonly Dictionary<string, System.Func<int, float>> derivationFormulas = new Dictionary<string, System.Func<int, float>>
    {
        {"discoveryRate", val => val * 0.1f},
        {"savingRollChance", val => val * 0.05f},
        {"legendEffectiveness", val => val * 0.15f},
        {"expeditionCostMod", val => 1f - (val * 0.02f)},
        {"expeditionTimeMod", val => 1f - (val * 0.03f)},
        {"satisfactionEffectiveness", val => val * 0.12f},
        {"moraleLossMod", val => 1f - (val * 0.04f)},
        {"moraleRecoveryMod", val => 1f + (val * 0.06f)},
        {"clickPowerBonus", val => val * 0.08f},
        {"magicEffectiveness", val => val * 0.2f}
    };

    private void Awake()
    {
        InitializeStats();
        CalculateDerivedStats();
    }

    private void InitializeStats()
    {
        // Initialize pillars
        pillars["aureus"] = aureus;
        pillars["regalia"] = regalia;
        pillars["waltz"] = waltz;
        pillars["chorus"] = chorus;

        // Initialize substats
        substats["innovation"] = innovation;
        substats["piety"] = piety;
        substats["authority"] = authority;
        substats["ambition"] = ambition;
        substats["symphony"] = symphony;
        substats["endurance"] = endurance;
        substats["arcane"] = arcane;
        substats["secrecy"] = secrecy;
    }

    private void CalculateDerivedStats()
    {
        foreach (var formula in derivationFormulas)
        {
            string substatName = GetSubstatForDerived(formula.Key);
            if (substats.ContainsKey(substatName))
            {
                float value = formula.Value(substats[substatName]);
                derived[formula.Key] = value;
                SetDerivedField(formula.Key, value);
            }
        }

        // Special case for communion stage
        communionStage = Mathf.FloorToInt(substats["secrecy"] / 5f);
        derived["communionStage"] = communionStage;
    }

    private string GetSubstatForDerived(string derivedStat)
    {
        var mapping = new Dictionary<string, string>
        {
            {"discoveryRate", "innovation"},
            {"savingRollChance", "piety"},
            {"legendEffectiveness", "authority"},
            {"expeditionCostMod", "ambition"},
            {"expeditionTimeMod", "ambition"},
            {"satisfactionEffectiveness", "symphony"},
            {"moraleLossMod", "endurance"},
            {"moraleRecoveryMod", "endurance"},
            {"clickPowerBonus", "arcane"},
            {"magicEffectiveness", "arcane"}
        };
        return mapping.TryGetValue(derivedStat, out string result) ? result : "";
    }

    private void SetDerivedField(string fieldName, float value)
    {
        switch (fieldName)
        {
            case "discoveryRate": discoveryRate = value; break;
            case "savingRollChance": savingRollChance = value; break;
            case "legendEffectiveness": legendEffectiveness = value; break;
            case "expeditionCostMod": expeditionCostMod = value; break;
            case "expeditionTimeMod": expeditionTimeMod = value; break;
            case "satisfactionEffectiveness": satisfactionEffectiveness = value; break;
            case "moraleLossMod": moraleLossMod = value; break;
            case "moraleRecoveryMod": moraleRecoveryMod = value; break;
            case "clickPowerBonus": clickPowerBonus = value; break;
            case "magicEffectiveness": magicEffectiveness = value; break;
        }
    }

    // Core public methods
    public void UpdatePillar(string pillarName, int newValue)
    {
        string key = pillarName.ToLower();
        if (pillars.ContainsKey(key))
        {
            pillars[key] = newValue;
            SetPillarField(key, newValue);
            UpdatePillarSubstats(key);
            OnPillarChanged?.Invoke(key, newValue);
        }
    }

    public void UpdateSubstat(string substatName, int newValue)
    {
        string key = substatName.ToLower();
        if (substats.ContainsKey(key))
        {
            substats[key] = newValue;
            SetSubstatField(key, newValue);
            CalculateDerivedStats();
            OnSubstatChanged?.Invoke(key, newValue);
        }
    }

    /// <summary>
    /// Universal stat update method for backward compatibility with EventSystemLogic
    /// Automatically routes to appropriate update method based on stat type
    /// </summary>
    public void UpdateStat(string statName, int newValue)
    {
        string key = statName.ToLower();
        
        // Check if it's a pillar stat
        if (pillars.ContainsKey(key))
        {
            UpdatePillar(key, newValue);
            return;
        }
        
        // Check if it's a substat
        if (substats.ContainsKey(key))
        {
            UpdateSubstat(key, newValue);
            return;
        }
        
        // If it's a derived stat, we can't update it directly
        // Log a warning and ignore the update
        Debug.LogWarning($"Cannot update derived stat '{statName}' directly. Update the source substat instead.");
    }

    private void UpdatePillarSubstats(string pillarName)
    {
        if (pillarSubstats.TryGetValue(pillarName, out string[] affectedSubstats))
        {
            int newSubstatValue = Mathf.Max(1, pillars[pillarName] / 2);
            foreach (string substat in affectedSubstats)
            {
                UpdateSubstat(substat, newSubstatValue);
            }
        }
    }

    private void SetPillarField(string fieldName, int value)
    {
        switch (fieldName)
        {
            case "aureus": aureus = value; break;
            case "regalia": regalia = value; break;
            case "waltz": waltz = value; break;
            case "chorus": chorus = value; break;
        }
    }

    private void SetSubstatField(string fieldName, int value)
    {
        switch (fieldName)
        {
            case "innovation": innovation = value; break;
            case "piety": piety = value; break;
            case "authority": authority = value; break;
            case "ambition": ambition = value; break;
            case "symphony": symphony = value; break;
            case "endurance": endurance = value; break;
            case "arcane": arcane = value; break;
            case "secrecy": secrecy = value; break;
        }
    }

    // Getter methods
    public int GetPillarValue(string pillarName) => pillars.TryGetValue(pillarName.ToLower(), out int value) ? value : 0;
    public int GetSubstatValue(string substatName) => substats.TryGetValue(substatName.ToLower(), out int value) ? value : 0;
    public float GetDerivedValue(string derivedName) => derived.TryGetValue(derivedName.ToLower(), out float value) ? value : 0f;

    // Event system compatibility
    public bool CheckStat(string statName, int requiredValue)
    {
        string key = statName.ToLower();
        return (pillars.TryGetValue(key, out int pillarValue) && pillarValue >= requiredValue) ||
               (substats.TryGetValue(key, out int substatValue) && substatValue >= requiredValue);
    }

    public int GetStatValue(string statName)
    {
        string key = statName.ToLower();
        if (pillars.TryGetValue(key, out int pillarValue)) return pillarValue;
        if (substats.TryGetValue(key, out int substatValue)) return substatValue;
        if (derived.TryGetValue(key, out float derivedValue)) return Mathf.RoundToInt(derivedValue);
        return 0;
    }

    // Utility methods
    public List<string> GetAllStatNames()
    {
        var allStats = new List<string>();
        allStats.AddRange(pillars.Keys);
        allStats.AddRange(substats.Keys);
        allStats.AddRange(derived.Keys);
        return allStats;
    }

    // Civilization-specific helper methods
    public void UpdatePillarWithSubstats(string pillarName, int newValue)
    {
        UpdatePillar(pillarName, newValue);
    }

    public int GetPillarSubstatValue(string pillarName, int substatIndex)
    {
        if (pillarSubstats.TryGetValue(pillarName.ToLower(), out string[] substats))
        {
            if (substatIndex >= 0 && substatIndex < substats.Length)
            {
                return GetSubstatValue(substats[substatIndex]);
            }
        }
        return 0;
    }

    public float GetDerivedStatFromPillar(string pillarName, string derivedStatName)
    {
        if (pillarSubstats.TryGetValue(pillarName.ToLower(), out string[] substats))
        {
            // Find which substat controls this derived stat
            string controllingSubstat = GetSubstatForDerived(derivedStatName);
            if (Array.Exists(substats, s => s == controllingSubstat))
            {
                return GetDerivedValue(derivedStatName);
            }
        }
        return 0f;
    }

    public bool IsPillarHigh(string pillarName, int threshold = 8)
    {
        return GetPillarValue(pillarName) >= threshold;
    }

    public bool IsSubstatHigh(string substatName, int threshold = 5)
    {
        return GetSubstatValue(substatName) >= threshold;
    }

    public bool IsDerivedStatHigh(string derivedStatName, float threshold = 0.5f)
    {
        return GetDerivedValue(derivedStatName) >= threshold;
    }

    [ContextMenu("Print All Stats")]
    public void PrintAllStats()
    {
        Debug.Log("=== CIVILIZATION STATS ===");
        Debug.Log($"Aureus: {aureus} (Innovation: {innovation}, Piety: {piety})");
        Debug.Log($"Regalia: {regalia} (Authority: {authority}, Ambition: {ambition})");
        Debug.Log($"Waltz: {waltz} (Symphony: {symphony}, Endurance: {endurance})");
        Debug.Log($"Chorus: {chorus} (Arcane: {arcane}, Secrecy: {secrecy})");
        Debug.Log("=== DERIVED STATS ===");
        foreach (var kvp in derived)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value:P1}");
        }
    }
}
