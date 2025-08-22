using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Manages civilization stats with 4 Pillars, 8 Substats, and derived calculations
/// </summary>
public class StatManager : MonoBehaviour
{
    public static StatManager Instance { get; private set; }
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

    [Header("Morale System")]
    [SerializeField] private int morale = 100; // Integer morale (0-200), default at balance
    [SerializeField] private int moraleBalance = 100; // Equilibrium point
    [SerializeField] private int minMorale = 0, maxMorale = 200;
    [Tooltip("Above-balance recovery factor (Waltz * factor per seventh)")]
    [SerializeField] private float aboveBalanceRecoveryFactor = 0.5f; // X/2 when above
    [Tooltip("Dark morale increase per seventh factor when below balance (0.2 => +1 per 5 deficit)")]
    [SerializeField] private float darkMoraleIncreaseFactor = 0.2f;
    [Tooltip("Dark morale decrease per seventh factor when above balance (0.1 => -1 per 10 surplus)")]
    [SerializeField] private float darkMoraleDecreaseFactor = 0.1f;

    // Core dictionaries for easy access
    private Dictionary<string, int> pillars = new Dictionary<string, int>();
    private Dictionary<string, int> substats = new Dictionary<string, int>();
    private Dictionary<string, float> derived = new Dictionary<string, float>();
    // Global named stats (civilization-wide). Includes morale and moraleBalance.
    private Dictionary<string, int> globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Events for UI updates
    public System.Action<string, int> OnPillarChanged;
    public System.Action<string, int> OnSubstatChanged;
    public System.Action<string, float> OnDerivedChanged;
    public System.Action<int> OnMoraleChanged;

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
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate StatManager found, destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeStats();
        CalculateDerivedStats();
    }

    private void Start()
    {
        // Subscribe to time system seventh changes to apply morale oscillation
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange += OnSeventhTick;
        }
        else
        {
            // Attempt delayed subscription
            StartCoroutine(SubscribeToTimeWhenReady());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PrintAllStats();
        }
    }

    private System.Collections.IEnumerator SubscribeToTimeWhenReady()
    {
        while (TimeSystemLogic.Instance == null)
        {
            yield return null;
        }
        TimeSystemLogic.Instance.OnSeventhChange += OnSeventhTick;
    }

    private void OnDestroy()
    {
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange -= OnSeventhTick;
        }
    }

    private void OnValidate()
    {
        // Keep internal dictionaries in sync with serialized fields when values are edited in the Inspector.
        // Also broadcast changes at runtime so listeners (e.g., ChorusScreen) can refresh UI/logic.
        // Ensure base dictionaries exist before comparing
        if (pillars == null) pillars = new Dictionary<string, int>();
        if (substats == null) substats = new Dictionary<string, int>();
        if (globals == null) globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int oldAureus = pillars.ContainsKey("aureus") ? pillars["aureus"] : aureus;
        int oldRegalia = pillars.ContainsKey("regalia") ? pillars["regalia"] : regalia;
        int oldWaltz = pillars.ContainsKey("waltz") ? pillars["waltz"] : waltz;
        int oldChorus = pillars.ContainsKey("chorus") ? pillars["chorus"] : chorus;

        // Initialize dictionaries from current serialized values
        InitializeStats();
        CalculateDerivedStats();

        if (!Application.isPlaying)
        {
            // In edit mode (not playing), don't emit events
            return;
        }

        // During play, notify listeners for only the pillars that changed, and update dependent substats
        if (oldAureus != aureus)
        {
            UpdatePillar("aureus", aureus);
        }
        if (oldRegalia != regalia)
        {
            UpdatePillar("regalia", regalia);
        }
        if (oldWaltz != waltz)
        {
            UpdatePillar("waltz", waltz);
        }
        if (oldChorus != chorus)
        {
            UpdatePillar("chorus", chorus);
        }
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

        // Initialize globals
        globals["morale"] = Mathf.Clamp(morale, minMorale, maxMorale);
        globals["moralebalance"] = moraleBalance;
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

        // Check globals (e.g., morale, moralebalance)
        if (globals.ContainsKey(key))
        {
            UpdateGlobal(key, newValue);
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
    public int GetGlobalValue(string globalName) => globals.TryGetValue(globalName.ToLower(), out int value) ? value : 0;

    // Event system compatibility
    public bool CheckStat(string statName, int requiredValue)
    {
        string key = statName.ToLower();
        return (pillars.TryGetValue(key, out int pillarValue) && pillarValue >= requiredValue) ||
               (substats.TryGetValue(key, out int substatValue) && substatValue >= requiredValue) ||
               (globals.TryGetValue(key, out int globalValue) && globalValue >= requiredValue);
    }

    public int GetStatValue(string statName)
    {
        string key = statName.ToLower();
        if (pillars.TryGetValue(key, out int pillarValue)) return pillarValue;
        if (substats.TryGetValue(key, out int substatValue)) return substatValue;
        if (globals.TryGetValue(key, out int globalValue)) return globalValue;
        if (derived.TryGetValue(key, out float derivedValue)) return Mathf.RoundToInt(derivedValue);
        return 0;
    }

    // Utility methods
    public List<string> GetAllStatNames()
    {
        var allStats = new List<string>();
        allStats.AddRange(pillars.Keys);
        allStats.AddRange(substats.Keys);
        allStats.AddRange(globals.Keys);
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
        Debug.Log($"Morale: {GetMorale()} (Balance: {GetMoraleBalance()})");
        Debug.Log($"Dark Morale: {EventSystemLogic.Instance?.GetEventScore("dark_morale")}");
        Debug.Log("=== DERIVED STATS ===");
        foreach (var kvp in derived)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value:P1}");
        }
    }

    // ===== Morale API =====
    private void UpdateGlobal(string globalName, int newValue)
    {
        string key = globalName.ToLower();
        if (key == "morale")
        {
            int clamped = Mathf.Clamp(newValue, minMorale, maxMorale);
            globals["morale"] = clamped;
            morale = clamped;
            OnMoraleChanged?.Invoke(clamped);
            return;
        }
        if (key == "moralebalance")
        {
            globals["moralebalance"] = newValue;
            moraleBalance = newValue;
            return;
        }
        globals[key] = newValue;
    }

    public int GetMorale()
    {
        return GetGlobalValue("morale");
    }

    public int GetMoraleBalance()
    {
        return GetGlobalValue("moralebalance");
    }

    public float GetMoraleDeltaPercent()
    {
        // Difference from equilibrium: positive => bonus, negative => malus
        return GetMorale() - GetMoraleBalance();
    }

    public void ApplyMoraleShift(int amount, string source = null, bool temporary = false, int durationSevenths = 0)
    {
        // Immediate apply; optional timed reversion handled internally per seventh
        int current = GetMorale();
        UpdateGlobal("morale", current + amount);

        if (temporary && durationSevenths > 0 && !string.IsNullOrEmpty(source))
        {
            // Track timed shift by source
            if (!_moraleTimedValueBySource.ContainsKey(source))
            {
                _moraleTimedValueBySource[source] = 0;
            }
            _moraleTimedValueBySource[source] += amount;
            _moraleTimedRemainingBySource[source] = durationSevenths;
        }
        else if (!temporary && !string.IsNullOrEmpty(source))
        {
            // Track persistent shift by source (replace existing)
            int previous = 0;
            _moralePersistentBySource.TryGetValue(source, out previous);
            _moralePersistentBySource[source] = amount;
            int delta = amount - previous;
            if (delta != 0)
            {
                UpdateGlobal("morale", GetMorale() + delta);
            }
        }
    }

    // Timed and persistent morale shift tracking
    private Dictionary<string, int> _moraleTimedRemainingBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _moraleTimedValueBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _moralePersistentBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private void OnSeventhTick(int newSeventh)
    {
        // Natural oscillation toward balance controlled by Waltz
        int waltzValue = GetPillarValue("waltz");
        int currentMorale = GetMorale();
        int balance = GetMoraleBalance();

        if (currentMorale > balance)
        {
            int down = Mathf.CeilToInt(waltzValue * Mathf.Max(0f, aboveBalanceRecoveryFactor));
            int next = currentMorale - down;
            // Prevent overshoot across balance: snap to balance if crossing it
            if (next < balance) next = balance;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
        }
        else if (currentMorale < balance)
        {
            int up = Mathf.Max(0, waltzValue);
            int next = currentMorale + up;
            // Prevent overshoot across balance: snap to balance if crossing it
            if (next > balance) next = balance;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
        }

        // Expire timed shifts
        if (_moraleTimedRemainingBySource.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var kv in _moraleTimedRemainingBySource)
            {
                int remain = Mathf.Max(0, kv.Value - 1);
                _moraleTimedRemainingBySource[kv.Key] = remain;
                if (remain == 0)
                {
                    // Revert the shift value for this source
                    if (_moraleTimedValueBySource.TryGetValue(kv.Key, out int shiftVal))
                    {
                        UpdateGlobal("morale", Mathf.Clamp(GetMorale() - shiftVal, minMorale, maxMorale));
                    }
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var key in toRemove)
            {
                _moraleTimedRemainingBySource.Remove(key);
                _moraleTimedValueBySource.Remove(key);
            }
        }

        // Adjust dark_morale score based on deficit/surplus
        int diff = GetMoraleBalance() - GetMorale();
        if (diff > 0)
        {
            int inc = Mathf.CeilToInt(diff * Mathf.Max(0f, darkMoraleIncreaseFactor));
            if (inc > 0) EventSystemLogic.Instance?.ModifyEventScore("dark_morale", inc);
        }
        else if (diff < 0)
        {
            int dec = Mathf.CeilToInt((-diff) * Mathf.Max(0f, darkMoraleDecreaseFactor));
            if (dec > 0)
            {
                // decrease by dec but not below 0
                int current = EventSystemLogic.Instance?.GetEventScore("dark_morale") ?? 0;
                int actualDec = Mathf.Min(dec, current);
                if (actualDec > 0) EventSystemLogic.Instance?.ModifyEventScore("dark_morale", -actualDec);
            }
        }
    }
}
