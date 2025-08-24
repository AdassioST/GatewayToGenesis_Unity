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
    [SerializeField] private int aureus = 10;
    [SerializeField] private int regalia = 10;
    [SerializeField] private int waltz = 10;
    [SerializeField] private int chorus = 10;

    [Header("Pillar Multipliers")]
    [SerializeField] private float aureusMultiplier = 0.5f;
    [SerializeField] private float regaliaMultiplier = 0.5f;
    [SerializeField] private float waltzMultiplier = 0.5f;   
    [SerializeField] private float chorusMultiplier = 0.5f;

    [Header("Substats")]
    [SerializeField] private int innovation = 5;
    [SerializeField] private int piety = 5;
    [SerializeField] private int authority = 5;
    [SerializeField] private int ambition = 5;
    [SerializeField] private int symphony = 5;
    [SerializeField] private int euphony = 5;
    [SerializeField] private int arcane = 5;
    [SerializeField] private int secrecy = 5;

    [Header("Derived Stats")]
    [SerializeField] private float discoveryEfficiency;
    [SerializeField] private float savingRollChance;
    [SerializeField] private float legendEffectiveness;
    [SerializeField] private float expeditionCostMod;
    [SerializeField] private float expeditionTimeMod;
    [SerializeField] private float satisfactionEffectiveness;
    [SerializeField] private float moraleLossMod;
    [SerializeField] private float moraleRecoveryMod;
    [SerializeField] private float clickPowerBonus;
    [SerializeField] private float magicEffectiveness;
    [SerializeField] private int communionStage;
    
    [Header("Stat Caps Settings")]
    [Tooltip("Cap for Discovery Efficiency as a 0-1 fraction (0.45 = 45%)")]
    [SerializeField] private float discoveryEfficiencyCap01 = 0.45f;
    [SerializeField] private float savingRollChanceCap01 = 0.25f;
    
    [Header("Growth Tier Configuration")]
    [SerializeField] private int tier1Boundary = 42;   // Tier 1 boundary (0-42)
    [SerializeField] private int tier2Boundary = 86;   // Tier 2 boundary (43-86)
    [SerializeField] private int tier3Boundary = 150;  // Tier 3 boundary (87-150)
    [SerializeField] private int tier4Boundary = 220;  // Tier 4 boundary (151-220)
    [SerializeField] private float globalBalanceMultiplier = 1.0f; // Global balance adjustment

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
    [SerializeField] private float moraleGainBalanceFactor = 0.8f; // 80% of loss mitigation applied to gains

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
        {"waltz", new[] {"symphony", "euphony"}},
        {"chorus", new[] {"arcane", "secrecy"}}
    };



    // Pillar multipliers dictionary for easy lookup
    private Dictionary<string, float> pillarMultipliers;



    // Custom formula types for sophisticated stat calculations
    public enum CustomFormulaType
    {
        Linear,           // ax + b
        Quadratic,        // ax² + bx + c
        Logarithmic,      // a*log(bx + c) + d
        Static,           // Fixed value per level

    }

    // Custom formula configuration with parameters
    [System.Serializable]
    public struct CustomFormula
    {
        public CustomFormulaType type;
        public float a, b, c, d;  // Formula parameters

        
        public CustomFormula(CustomFormulaType type, float a = 0f, float b = 0f, float c = 0f, float d = 0f)
        {
            this.type = type;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;

        }
        

    }

    // Enhanced tier configuration with custom formulas
    [System.Serializable]
    public struct TierConfig
    {
        public CustomFormulaType formulaType;
        public CustomFormula formula;
        public float balanceModifier; // Multiplier for balance adjustments
        
        public TierConfig(CustomFormulaType formulaType, CustomFormula formula, float balanceModifier = 1f)
        {
            this.formulaType = formulaType;
            this.formula = formula;
            this.balanceModifier = balanceModifier;
        }
        
        // Constructor for simple cases
        public TierConfig(CustomFormulaType formulaType, float a = 0f, float b = 0f, float c = 0f, float d = 0f, float balanceModifier = 1f)
        {
            this.formulaType = formulaType;
            this.formula = new CustomFormula(formulaType, a, b, c, d);
            this.balanceModifier = balanceModifier;
        }
    }

    // Substat-derivation formulas using tiered growth system
    private Dictionary<string, System.Func<int, float>> derivationFormulas;

    // Enhanced tiered growth configurations with custom formulas
    private readonly Dictionary<string, TierConfig[]> tieredGrowthConfigs = new Dictionary<string, TierConfig[]>
    {
        {"discoveryEfficiency", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.1f, 8f, 0f, 0f, 1.0f),      // (-0.1x² + 8x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 1f, -3f, 280f, -1f, 0.8f),   // log₁₀(-3x + 280) - 1
            new TierConfig(CustomFormulaType.Static, 0.5f, 0f, 0f, 0f, 0.6f),          // 0.5 per level
            new TierConfig(CustomFormulaType.Static, 0.25f, 0f, 0f, 0f, 0.4f)         // 0.25 per level
        }},
        {"savingRollChance", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.05f, 4f, 0f, 0f, 1.0f),     // (-0.05x² + 4x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.5f, -2f, 200f, -0.5f, 0.8f), // 0.5*log₁₀(-2x + 200) - 0.5
            new TierConfig(CustomFormulaType.Static, 0.25f, 0f, 0f, 0f, 0.6f),        // 0.25 per level
            new TierConfig(CustomFormulaType.Static, 0.125f, 0f, 0f, 0f, 0.4f)        // 0.125 per level
        }},
        {"legendEffectiveness", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.1f, 8f, 0f, 0f, 1.0f),      // (-0.1x² + 8x)/100 for 0-42
            new TierConfig(CustomFormulaType.Logarithmic, 1f, -3f, 280f, -1f, 0.8f),   // log₁₀(-3x + 280) - 1 for 42-86
            new TierConfig(CustomFormulaType.Static, 0.5f, 0f, 0f, 0f, 0.6f),          // 0.5 per level for 86-150
            new TierConfig(CustomFormulaType.Static, 0.25f, 0f, 0f, 0f, 0.4f)         // 0.25 per level for 150+
        }},
        {"expeditionCostMod", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.02f, 1.6f, 0f, 0f, 1.0f),   // (-0.02x² + 1.6x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.2f, -1f, 100f, -0.2f, 0.8f), // 0.2*log₁₀(-x + 100) - 0.2
            new TierConfig(CustomFormulaType.Static, 0.1f, 0f, 0f, 0f, 0.6f),         // 0.1 per level
            new TierConfig(CustomFormulaType.Static, 0.05f, 0f, 0f, 0f, 0.4f)         // 0.05 per level
        }},
        {"expeditionTimeMod", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.03f, 2.4f, 0f, 0f, 1.0f),   // (-0.03x² + 2.4x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.3f, -1.5f, 150f, -0.3f, 0.8f), // 0.3*log₁₀(-1.5x + 150) - 0.3
            new TierConfig(CustomFormulaType.Static, 0.15f, 0f, 0f, 0f, 0.6f),        // 0.15 per level
            new TierConfig(CustomFormulaType.Static, 0.075f, 0f, 0f, 0f, 0.4f)        // 0.075 per level
        }},
        {"satisfactionEffectiveness", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.12f, 9.6f, 0f, 0f, 1.0f),  // (-0.12x² + 9.6x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 1.2f, -3.6f, 336f, -1.2f, 0.8f), // 1.2*log₁₀(-3.6x + 336) - 1.2
            new TierConfig(CustomFormulaType.Static, 0.6f, 0f, 0f, 0f, 0.6f),         // 0.6 per level
            new TierConfig(CustomFormulaType.Static, 0.3f, 0f, 0f, 0f, 0.4f)          // 0.3 per level
        }},
        {"moraleLossMod", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.04f, 3.2f, 0f, 0f, 1.0f),  // (-0.04x² + 3.2x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.4f, -1.2f, 112f, -0.4f, 0.8f), // 0.4*log₁₀(-1.2x + 112) - 0.4
            new TierConfig(CustomFormulaType.Static, 0.2f, 0f, 0f, 0f, 0.6f),         // 0.2 per level
            new TierConfig(CustomFormulaType.Static, 0.1f, 0f, 0f, 0f, 0.4f)          // 0.1 per level
        }},
        {"moraleRecoveryMod", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.06f, 4.8f, 0f, 0f, 1.0f),  // (-0.06x² + 4.8x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.6f, -1.8f, 168f, -0.6f, 0.8f), // 0.6*log₁₀(-1.8x + 168) - 0.6
            new TierConfig(CustomFormulaType.Static, 0.3f, 0f, 0f, 0f, 0.6f),         // 0.3 per level
            new TierConfig(CustomFormulaType.Static, 0.15f, 0f, 0f, 0f, 0.4f)         // 0.15 per level
        }},
        {"clickPowerBonus", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.08f, 6.4f, 0f, 0f, 1.0f),  // (-0.08x² + 6.4x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 0.8f, -2.4f, 224f, -0.8f, 0.8f), // 0.8*log₁₀(-2.4x + 224) - 0.8
            new TierConfig(CustomFormulaType.Static, 0.4f, 0f, 0f, 0f, 0.6f),         // 0.4 per level
            new TierConfig(CustomFormulaType.Static, 0.2f, 0f, 0f, 0f, 0.4f)          // 0.2 per level
        }},
        {"magicEffectiveness", new TierConfig[] {
            new TierConfig(CustomFormulaType.Quadratic, -0.2f, 16f, 0f, 0f, 1.0f),    // (-0.2x² + 16x)/100
            new TierConfig(CustomFormulaType.Logarithmic, 2f, -6f, 560f, -2f, 0.8f),  // 2*log₁₀(-6x + 560) - 2
            new TierConfig(CustomFormulaType.Static, 1f, 0f, 0f, 0f, 0.6f),           // 1.0 per level
            new TierConfig(CustomFormulaType.Static, 0.5f, 0f, 0f, 0f, 0.4f)          // 0.5 per level
        }}
    };

    /// <summary>
    /// Calculate tiered growth as incremental bonuses per level
    /// Each level adds its bonus to the base value
    /// </summary>
    /// <param name="level">Current stat level</param>
    /// <param name="tierConfigs">Array of tier configurations defining growth patterns</param>
    /// <returns>Total incremental bonus value</returns>
    private float CalculateTieredGrowth(int level, TierConfig[] tierConfigs)
    {
        if (level <= 0) return 0f;

        // Use configurable tier boundaries from Inspector
        int[] caps = { tier1Boundary, tier2Boundary, tier3Boundary, tier4Boundary, int.MaxValue };

        float totalBonus = 0f;
        int prevCap = 0;

        for (int i = 0; i < tierConfigs.Length; i++)
        {
            int currentCap = caps[i];
            int levelsInTier = Mathf.Clamp(level - prevCap, 0, currentCap - prevCap);

            if (levelsInTier > 0)
            {
                var config = tierConfigs[i];
                switch (config.formulaType)
                {
                    case CustomFormulaType.Linear:
                        // ax + b per level (incremental bonus)
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = prevCap + l;
                            float levelBonus = (config.formula.a * x + config.formula.b) * config.balanceModifier * globalBalanceMultiplier;
                            totalBonus += levelBonus;
                        }
                        break;

                    case CustomFormulaType.Quadratic:
                        // ax² + bx + c per level (incremental bonus)
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = prevCap + l;
                            float levelBonus = (config.formula.a * x * x + config.formula.b * x + config.formula.c) / 100f; // Divide by 100 for scaling
                            levelBonus *= config.balanceModifier * globalBalanceMultiplier;
                            totalBonus += levelBonus;
                        }
                        break;

                    case CustomFormulaType.Logarithmic:
                        // a*log₁₀(bx + c) + d per level (incremental bonus)
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = prevCap + l;
                            float logBase = config.formula.b * x + config.formula.c;
                            if (logBase > 0) // Ensure positive value for logarithm
                            {
                                float levelBonus = config.formula.a * Mathf.Log10(logBase) + config.formula.d;
                                levelBonus *= config.balanceModifier * globalBalanceMultiplier;
                                totalBonus += levelBonus;
                            }
                        }
                        break;

                    case CustomFormulaType.Static:
                        // Fixed value per level (incremental bonus)
                        float staticBonus = levelsInTier * config.formula.a * config.balanceModifier * globalBalanceMultiplier;
                        totalBonus += staticBonus;
                        break;


                }
            }

            prevCap = currentCap;

            if (level <= currentCap) break;
        }

        return totalBonus;
    }

    /// <summary>
    /// Adjust the balance modifier for a specific stat's growth tiers
    /// </summary>
    /// <param name="statName">Name of the stat to adjust</param>
    /// <param name="tierIndex">Which tier to adjust (0-3)</param>
    /// <param name="newBalanceModifier">New balance modifier value</param>
    public void AdjustStatBalanceModifier(string statName, int tierIndex, float newBalanceModifier)
    {
        if (tieredGrowthConfigs.TryGetValue(statName, out TierConfig[] configs))
        {
            if (tierIndex >= 0 && tierIndex < configs.Length)
            {
                // Create new config with updated balance modifier
                var oldConfig = configs[tierIndex];
                configs[tierIndex] = new TierConfig(oldConfig.formulaType, oldConfig.formula, newBalanceModifier);
                
                // Recalculate derived stats to apply the change immediately
                CalculateDerivedStats();
                
                Debug.Log($"[StatManager] Updated {statName} tier {tierIndex} balance modifier to {newBalanceModifier}");
            }
        }
    }

    /// <summary>
    /// Get the current balance modifier for a specific stat's growth tier
    /// </summary>
    /// <param name="statName">Name of the stat</param>
    /// <param name="tierIndex">Which tier to check (0-3)</param>
    /// <returns>Current balance modifier value, or 0 if not found</returns>
    public float GetStatBalanceModifier(string statName, int tierIndex)
    {
        if (tieredGrowthConfigs.TryGetValue(statName, out TierConfig[] configs))
        {
            if (tierIndex >= 0 && tierIndex < configs.Length)
            {
                return configs[tierIndex].balanceModifier;
            }
        }
        return 0f;
    }
    
    // Discovery Efficiency helpers (centralized cap)
    public float GetDiscoveryEfficiencyCap01()
    {
        return Mathf.Clamp01(discoveryEfficiencyCap01);
    }
    public void SetDiscoveryEfficiencyCap01(float newCap01)
    {
        discoveryEfficiencyCap01 = Mathf.Clamp01(newCap01);
    }
    // Returns efficiency as 0-1 fraction with cap applied
    public float GetDiscoveryEfficiency01Capped()
    {
        float raw01 = Mathf.Max(0f, discoveryEfficiency / 100f);
        return Mathf.Min(raw01, GetDiscoveryEfficiencyCap01());
    }
    // Returns efficiency as 0-100 percent with cap applied
    public float GetDiscoveryEfficiencyPercentCapped()
    {
        return GetDiscoveryEfficiency01Capped() * 100f;
    }

    // Saving Roll Chance helpers (centralized cap)
    public float GetSavingRollChanceCap01()
    {
        return Mathf.Clamp01(savingRollChanceCap01);
    }
    public void SetSavingRollChanceCap01(float newCap01)
    {
        savingRollChanceCap01 = Mathf.Clamp01(newCap01);
    }
    // Returns saving roll chance as 0-1 fraction with cap applied
    public float GetSavingRollChance01Capped()
    {
        float raw01 = Mathf.Max(0f, savingRollChance / 100f);
        return Mathf.Min(raw01, GetSavingRollChanceCap01());
    }
    // Returns saving roll chance as 0-100 percent with cap applied
    public float GetSavingRollChancePercentCapped()
    {
        return GetSavingRollChance01Capped() * 100f;
    }

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
        
        // Initialize pillar multipliers dictionary
        pillarMultipliers = new Dictionary<string, float>
        {
            {"aureus", aureusMultiplier},
            {"regalia", regaliaMultiplier},
            {"waltz", waltzMultiplier},
            {"chorus", chorusMultiplier}
        };
        
        // Initialize derivation formulas after tieredGrowthConfigs is ready
        // Add base values to incremental bonuses
        derivationFormulas = new Dictionary<string, System.Func<int, float>>
        {
            {"discoveryEfficiency", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["discoveryEfficiency"])},           // Base 0% + incremental bonus
            {"savingRollChance", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["savingRollChance"])},     // Base 0% + incremental bonus
            {"legendEffectiveness", val => 100f + CalculateTieredGrowth(val, tieredGrowthConfigs["legendEffectiveness"])}, // Base 100% + incremental bonus
            {"expeditionCostMod", val => 1f - CalculateTieredGrowth(val, tieredGrowthConfigs["expeditionCostMod"])},   // Base 100% - incremental penalty
            {"expeditionTimeMod", val => 1f - CalculateTieredGrowth(val, tieredGrowthConfigs["expeditionTimeMod"])},   // Base 100% - incremental penalty
            {"satisfactionEffectiveness", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["satisfactionEffectiveness"])}, // Base 0% + incremental bonus
            {"moraleLossMod", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["moraleLossMod"])},          // Base 0% + incremental bonus (mitigation)
            {"moraleRecoveryMod", val => 1f + CalculateTieredGrowth(val, tieredGrowthConfigs["moraleRecoveryMod"])},  // Base 100% + incremental bonus
            {"clickPowerBonus", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["clickPowerBonus"])},       // Base 0 + incremental bonus
            {"magicEffectiveness", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["magicEffectiveness"])}  // Base 0% + incremental bonus
        };
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
        
        // Only calculate derived stats if the tiered system is ready
        // This prevents NullReferenceException during editor validation
        if (tieredGrowthConfigs != null && derivationFormulas != null)
        {
        CalculateDerivedStats();
        }
        else
        {
            // Use simple fallback calculations
            CalculateSimpleDerivedStats();
        }

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
        substats["euphony"] = euphony;
        substats["arcane"] = arcane;
        substats["secrecy"] = secrecy;

        // Initialize globals
        globals["morale"] = Mathf.Clamp(morale, minMorale, maxMorale);
        globals["moralebalance"] = moraleBalance;
    }

    private void CalculateDerivedStats()
    {
        // Check if the tiered growth system is ready
        if (tieredGrowthConfigs == null || derivationFormulas == null)
        {
            // Fall back to simple calculations if the tiered system isn't ready yet
            CalculateSimpleDerivedStats();
            return;
        }



        // Use the new tiered growth system
        foreach (var formula in derivationFormulas)
        {
            string substatName = GetSubstatForDerived(formula.Key);
            if (substats.ContainsKey(substatName))
            {
                int substatValue = substats[substatName];
                float value = formula.Value(substatValue);
                derived[formula.Key.ToLower()] = value; // Store with lowercase key for consistency
                SetDerivedField(formula.Key, value);
                

            }
            else
            {

            }
        }

        // Special case for communion stage
        communionStage = Mathf.FloorToInt(substats["secrecy"] / 5f);
        derived["communionStage"] = communionStage;
    }

    /// <summary>
    /// Fallback method for calculating derived stats when tiered system isn't ready
    /// Uses same base value logic as tiered system
    /// </summary>
    private void CalculateSimpleDerivedStats()
    {
        // Simple linear calculations as fallback with base values
        if (substats.ContainsKey("innovation"))
        {
            derived["discoveryEfficiency".ToLower()] = 0f + (substats["innovation"] * 0.1f); // Base 0% + bonus
            SetDerivedField("discoveryEfficiency", derived["discoveryEfficiency".ToLower()]);
        }
        
        if (substats.ContainsKey("piety"))
        {
            derived["savingRollChance".ToLower()] = 0f + (substats["piety"] * 0.05f); // Base 0% + bonus
            SetDerivedField("savingRollChance", derived["savingRollChance".ToLower()]);
        }
        
        if (substats.ContainsKey("authority"))
        {
            derived["legendEffectiveness".ToLower()] = 100f + (substats["authority"] * 0.15f); // Base 100% + bonus
            SetDerivedField("legendEffectiveness", derived["legendEffectiveness".ToLower()]);
        }
        
        if (substats.ContainsKey("ambition"))
        {
            derived["expeditionCostMod".ToLower()] = 1f - (substats["ambition"] * 0.02f); // Base 100% - penalty
            derived["expeditionTimeMod".ToLower()] = 1f - (substats["ambition"] * 0.03f); // Base 100% - penalty
            SetDerivedField("expeditionCostMod", derived["expeditionCostMod".ToLower()]);
            SetDerivedField("expeditionTimeMod", derived["expeditionTimeMod".ToLower()]);
        }
        
        if (substats.ContainsKey("symphony"))
        {
            derived["satisfactionEffectiveness".ToLower()] = 0f + (substats["symphony"] * 0.12f); // Base 0% + bonus
            SetDerivedField("satisfactionEffectiveness", derived["satisfactionEffectiveness".ToLower()]);
        }
        
        if (substats.ContainsKey("euphony"))
        {
            derived["moraleLossMod".ToLower()] = 0f + (substats["euphony"] * 0.04f); // Base 0% + bonus (mitigation)
            derived["moraleRecoveryMod".ToLower()] = 1f + (substats["euphony"] * 0.06f); // Base 100% + bonus
            SetDerivedField("moraleLossMod", derived["moraleLossMod".ToLower()]);
            SetDerivedField("moraleRecoveryMod", derived["moraleRecoveryMod".ToLower()]);
        }
        
        if (substats.ContainsKey("arcane"))
        {
            derived["clickPowerBonus".ToLower()] = 0f + (substats["arcane"] * 0.08f); // Base 0 + bonus
            derived["magicEffectiveness".ToLower()] = 0f + (substats["arcane"] * 0.2f); // Base 0% + bonus
            SetDerivedField("clickPowerBonus", derived["clickPowerBonus".ToLower()]);
            SetDerivedField("magicEffectiveness", derived["magicEffectiveness".ToLower()]);
        }

        // Special case for communion stage
        communionStage = Mathf.FloorToInt(substats["secrecy"] / 5f);
        derived["communionStage"] = communionStage;
    }

    private string GetSubstatForDerived(string derivedStat)
    {
        var mapping = new Dictionary<string, string>
        {
            {"discoveryEfficiency", "innovation"},
            {"savingRollChance", "piety"},
            {"legendEffectiveness", "authority"},
            {"expeditionCostMod", "ambition"},
            {"expeditionTimeMod", "ambition"},
            {"satisfactionEffectiveness", "symphony"},
            {"moraleLossMod", "euphony"},
            {"moraleRecoveryMod", "euphony"},
            {"clickPowerBonus", "arcane"},
            {"magicEffectiveness", "arcane"}
        };
        return mapping.TryGetValue(derivedStat, out string result) ? result : "";
    }

    private void SetDerivedField(string fieldName, float value)
    {
        switch (fieldName)
        {
            case "discoveryEfficiency": discoveryEfficiency = value; break;
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
                    // Special handling for morale to ensure loss mitigation
        if (key == "morale")
        {
            int currentMorale = GetMorale();
            int delta = newValue - currentMorale;
            if (delta != 0)
            {
                Debug.Log($"[StatManager] Routing morale: {currentMorale} → {newValue} (delta: {delta})");
                // Apply the enhanced delta directly to avoid double-application
                int enhancedDelta = ApplyMoraleShiftAndReturnEnhanced(delta, "UpdateStat", false);
                UpdateGlobal("morale", currentMorale + enhancedDelta);
            }
            return;
        }
            
            UpdateGlobal(key, newValue);
            return;
        }
        
        // If it's a derived stat, we can't update it directly
        // Log a warning and ignore the update
        Debug.LogWarning($"Cannot update derived stat '{statName}' directly. Update the source substat instead.");
    }

    /// <summary>
    /// Update a global stat value (e.g., morale, moralebalance)
    /// </summary>
    /// <param name="globalName">Name of the global stat</param>
    /// <param name="newValue">New value to set</param>
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

    private void UpdatePillarSubstats(string pillarName)
    {
        if (pillarSubstats.TryGetValue(pillarName, out string[] affectedSubstats))
        {
            // Use configurable multiplier for this pillar
            float multiplier = pillarMultipliers.GetValueOrDefault(pillarName, 0.5f);
            int newSubstatValue = Mathf.Max(1, Mathf.RoundToInt(pillars[pillarName] * multiplier));
            
            Debug.Log($"[StatManager] {pillarName} pillar ({pillars[pillarName]}) × {multiplier:F2} = {newSubstatValue} for substats: {string.Join(", ", affectedSubstats)}");
            
            foreach (string substat in affectedSubstats)
            {
                UpdateSubstat(substat, newSubstatValue);
            }
        }
    }

    /// <summary>
    /// Update the multiplier for a specific pillar at runtime
    /// </summary>
    /// <param name="pillarName">Name of the pillar to update</param>
    /// <param name="newMultiplier">New multiplier value</param>
    public void UpdatePillarMultiplier(string pillarName, float newMultiplier)
    {
        string key = pillarName.ToLower();
        if (pillarMultipliers.ContainsKey(key))
        {
            pillarMultipliers[key] = newMultiplier;
            
            // Update the corresponding serialized field for persistence
            switch (key)
            {
                case "aureus": aureusMultiplier = newMultiplier; break;
                case "regalia": regaliaMultiplier = newMultiplier; break;
                case "waltz": waltzMultiplier = newMultiplier; break;
                case "chorus": chorusMultiplier = newMultiplier; break;
            }
            
            // Recalculate substats for this pillar
            UpdatePillarSubstats(key);
            
            Debug.Log($"[StatManager] Updated {pillarName} multiplier to {newMultiplier:F2}");
        }
        else
        {
            Debug.LogWarning($"[StatManager] Unknown pillar '{pillarName}' for multiplier update");
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
            case "euphony": euphony = value; break;
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
        Debug.Log($"Waltz: {waltz} (Symphony: {symphony}, Euphony: {euphony})");
        Debug.Log($"Chorus: {chorus} (Arcane: {arcane}, Secrecy: {secrecy})");
        Debug.Log($"Morale: {GetMorale()} (Balance: {GetMoraleBalance()})");
        Debug.Log($"Dark Morale: {EventSystemLogic.Instance?.GetEventScore("dark_morale")}");
        Debug.Log("=== DERIVED STATS ===");
        foreach (var kvp in derived)
        {
            // Display as decimal percentages (e.g., 101.145 instead of 10,114.5%)
            // Values are already percentages, don't multiply by 100
            Debug.Log($"{kvp.Key}: {kvp.Value:F3}");
        }
    }

    /// <summary>
    /// Test the new pillar multiplier system
    /// </summary>
    [ContextMenu("Test Pillar Multipliers")]
    public void TestPillarMultipliers()
    {
        Debug.Log("=== PILLAR MULTIPLIER TEST ===");
        Debug.Log($"Current multipliers:");
        Debug.Log($"  Aureus: {aureusMultiplier:F2} (Innovation, Piety)");
        Debug.Log($"  Regalia: {regaliaMultiplier:F2} (Authority, Ambition)");
        Debug.Log($"  Waltz: {waltzMultiplier:F2} (Symphony, Euphony)");
        Debug.Log($"  Chorus: {chorusMultiplier:F2} (Arcane, Secrecy)");
        
        Debug.Log($"\nCurrent pillar values:");
        Debug.Log($"  Aureus: {aureus} → Innovation: {innovation}, Piety: {piety}");
        Debug.Log($"  Regalia: {regalia} → Authority: {authority}, Ambition: {ambition}");
        Debug.Log($"  Waltz: {waltz} → Symphony: {symphony}, Euphony: {euphony}");
        Debug.Log($"  Chorus: {chorus} → Arcane: {arcane}, Secrecy: {secrecy}");
        
        Debug.Log($"\nExpected calculations:");
        Debug.Log($"  Aureus ({aureus}) × {aureusMultiplier:F2} = {Mathf.RoundToInt(aureus * aureusMultiplier)}");
        Debug.Log($"  Regalia ({regalia}) × {regaliaMultiplier:F2} = {Mathf.RoundToInt(regalia * regaliaMultiplier)}");
        Debug.Log($"  Waltz ({waltz}) × {waltzMultiplier:F2} = {Mathf.RoundToInt(waltz * waltzMultiplier)}");
        Debug.Log($"  Chorus ({chorus}) × {chorusMultiplier:F2} = {Mathf.RoundToInt(chorus * chorusMultiplier)}");
    }

    /// <summary>
    /// Test the morale system with Waltz and Euphony
    /// </summary>
    [ContextMenu("Test Morale System")]
    public void TestMoraleSystem()
    {
        Debug.Log("=== MORALE SYSTEM TEST ===");
        
        // Show current morale state
        Debug.Log($"Current Morale: {GetMorale()}");
        Debug.Log($"Morale Balance: {GetMoraleBalance()}");
        Debug.Log($"Morale Delta: {GetMoraleDeltaPercent()}");
        
        // Show Waltz and Euphony influence
        Debug.Log($"\nWaltz Pillar: {waltz}");
        Debug.Log($"Euphony Substat: {euphony}");
        Debug.Log($"Morale Loss Modifier: {moraleLossMod:P1} (mitigation)");
        Debug.Log($"Morale Recovery Modifier: {moraleRecoveryMod:F2} (recovery bonus)");
        
        // Test morale loss mitigation
        int testLoss = -10;
        float mitigationPercent = GetDerivedValue("moraleLossMod");
        float mitigationMultiplier = 1f - mitigationPercent;
        int mitigatedLoss = Mathf.RoundToInt(testLoss * mitigationMultiplier);
        
        Debug.Log($"\nMorale Loss Mitigation Test:");
        Debug.Log($"  Original Loss: {testLoss}");
        Debug.Log($"  Mitigation: {mitigationPercent:P1}");
        Debug.Log($"  Mitigation Multiplier: {mitigationMultiplier:F3}");
        Debug.Log($"  Actual Loss: {mitigatedLoss}");
        Debug.Log($"  Loss Reduced By: {Mathf.Abs(testLoss - mitigatedLoss)}");
        
        // Test morale recovery enhancement
        int baseRecovery = waltz;
        int enhancedRecovery = Mathf.RoundToInt(baseRecovery * moraleRecoveryMod);
        
        Debug.Log($"\nMorale Recovery Enhancement Test:");
        Debug.Log($"  Base Recovery (Waltz): {baseRecovery}");
        Debug.Log($"  Recovery Modifier: {moraleRecoveryMod:F2}");
        Debug.Log($"  Enhanced Recovery: {enhancedRecovery}");
        Debug.Log($"  Recovery Boost: +{enhancedRecovery - baseRecovery}");
        
        // Show tiered growth breakdown for Euphony
        if (tieredGrowthConfigs.ContainsKey("moraleLossMod"))
        {
            Debug.Log($"\nEuphony Tiered Growth Breakdown:");
            int[] caps = { tier1Boundary, tier2Boundary, tier3Boundary, tier4Boundary, int.MaxValue };
            float totalMitigation = 0f;
            int prevCap = 0;
            
            for (int i = 0; i < 4; i++)
            {
                int currentCap = caps[i];
                int levelsInTier = Mathf.Clamp(euphony - prevCap, 0, currentCap - prevCap);
                
                if (levelsInTier > 0)
                {
                    var config = tieredGrowthConfigs["moraleLossMod"][i];
                    string tierName = $"Tier {i + 1} (Levels {prevCap + 1}-{currentCap})";
                    
                    switch (config.formulaType)
                    {
                        case CustomFormulaType.Quadratic:
                            float quadValue = levelsInTier * ((config.formula.a * euphony * euphony + config.formula.b * euphony + config.formula.c) / 100f);
                            float quadBonus = quadValue * config.balanceModifier * globalBalanceMultiplier;
                            totalMitigation += quadBonus;
                            Debug.Log($"  {tierName}: Quadratic = {quadBonus:F4} mitigation");
                            break;
                        case CustomFormulaType.Logarithmic:
                            float logValue = levelsInTier * (config.formula.a * Mathf.Log10(config.formula.b * euphony + config.formula.c) + config.formula.d);
                            float logBonus = logValue * config.balanceModifier * globalBalanceMultiplier;
                            totalMitigation += logBonus;
                            Debug.Log($"  {tierName}: Logarithmic = {logBonus:F4} mitigation");
                            break;
                        case CustomFormulaType.Static:
                            float staticValue = levelsInTier * config.formula.a;
                            float staticBonus = staticValue * config.balanceModifier * globalBalanceMultiplier;
                            totalMitigation += staticBonus;
                            Debug.Log($"  {tierName}: Static = {staticBonus:F4} mitigation");
                            break;
                    }
                }
                prevCap = currentCap;
            }
            
            Debug.Log($"  Total Mitigation: {totalMitigation:F4} ({totalMitigation:P2})");
        }
    }

    /// <summary>
    /// Test morale loss mitigation by applying a test morale shift
    /// </summary>
    [ContextMenu("Test Morale Loss Mitigation")]
    public void TestMoraleLossMitigation()
    {
        Debug.Log("=== MORALE LOSS MITIGATION TEST ===");
        
        int currentMorale = GetMorale();
        int testLoss = -15;
        
        Debug.Log($"Current Morale: {currentMorale}");
        Debug.Log($"Applying Test Loss: {testLoss}");
        Debug.Log($"Expected Final Morale: {currentMorale + testLoss}");
        
        // Apply the morale shift (this will trigger mitigation)
        ApplyMoraleShift(testLoss, "test_mitigation", true, 1);
        
        int newMorale = GetMorale();
        int actualLoss = newMorale - currentMorale;
        
        Debug.Log($"\nResults:");
        Debug.Log($"  Final Morale: {newMorale}");
        Debug.Log($"  Actual Loss: {actualLoss}");
        Debug.Log($"  Loss Mitigated: {Mathf.Abs(testLoss - actualLoss)}");
        Debug.Log($"  Mitigation Effectiveness: {Mathf.Abs(testLoss - actualLoss) / (float)Mathf.Abs(testLoss):P1}");
        
        // Revert the test shift
        ApplyMoraleShift(-actualLoss, "test_mitigation", true, 1);
        Debug.Log($"\nReverted test shift. Current Morale: {GetMorale()}");
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

    /// <summary>
    /// Apply morale shift with modifiers and return the enhanced amount (for external routing)
    /// </summary>
    public int ApplyMoraleShiftAndReturnEnhanced(int amount, string source = null, bool temporary = false, int durationSevenths = 0)
    {
        // Apply morale modifiers for both gains and losses
        int actualAmount = amount;
        if (amount != 0)
        {
            if (amount < 0)
            {
                // Loss mitigation: reduce negative impact
                float mitigationPercent = GetDerivedValue("moraleLossMod");
                float mitigationMultiplier = 1f - mitigationPercent;
                actualAmount = Mathf.RoundToInt(amount * mitigationMultiplier);
                
                if (mitigationPercent > 0f)
                {
                    Debug.Log($"[StatManager] Loss: {amount} → {mitigationPercent:P1} mitigation → {actualAmount}");
                }
            }
            else
            {
                // Gain enhancement: boost positive impact (balanced by moraleGainBalanceFactor)
                float mitigationPercent = GetDerivedValue("moraleLossMod");
                float enhancementPercent = mitigationPercent * moraleGainBalanceFactor; // 80% of loss mitigation
                float enhancementMultiplier = 1f + enhancementPercent;
                actualAmount = Mathf.RoundToInt(amount * enhancementMultiplier);
                
                if (enhancementPercent > 0f)
                {
                    Debug.Log($"[StatManager] Gain: {amount} → +{enhancementPercent:P1} boost → {actualAmount}");
                }
            }
        }

        // Return the enhanced amount without applying it
        return actualAmount;
    }

    public void ApplyMoraleShift(int amount, string source = null, bool temporary = false, int durationSevenths = 0)
    {
        // Get the enhanced amount using the shared logic
        int actualAmount = ApplyMoraleShiftAndReturnEnhanced(amount, source, temporary, durationSevenths);

        // Immediate apply with mitigated amount
        int current = GetMorale();
        UpdateGlobal("morale", current + actualAmount);

        if (temporary && durationSevenths > 0 && !string.IsNullOrEmpty(source))
        {
            // Track timed shift by source (use original amount for tracking, but mitigated amount for effect)
            if (!_moraleTimedValueBySource.ContainsKey(source))
            {
                _moraleTimedValueBySource[source] = 0;
            }
            _moraleTimedValueBySource[source] += actualAmount; // Track the mitigated amount
            _moraleTimedRemainingBySource[source] = durationSevenths;
        }
        else if (!temporary && !string.IsNullOrEmpty(source))
        {
            // Track persistent shift by source (replace existing)
            int previous = 0;
            _moralePersistentBySource.TryGetValue(source, out previous);
            _moralePersistentBySource[source] = actualAmount; // Store the mitigated amount
            int delta = actualAmount - previous;
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
        // Natural oscillation toward balance controlled by Waltz and enhanced by Euphony
        int waltzValue = GetPillarValue("waltz");
        float moraleRecoveryMod = GetDerivedValue("moraleRecoveryMod"); // Get Euphony-based recovery bonus
        

        int currentMorale = GetMorale();
        int balance = GetMoraleBalance();

        if (currentMorale > balance)
        {
            // Morale above balance: natural decline toward balance
            int down = Mathf.CeilToInt(waltzValue * Mathf.Max(0f, aboveBalanceRecoveryFactor));
            int next = currentMorale - down;
            // Prevent overshoot across balance: snap to balance if crossing it
            if (next < balance) next = balance;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
        }
        else if (currentMorale < balance)
        {
            // Morale below balance: recovery toward balance enhanced by Euphony
            int baseRecovery = Mathf.Max(0, waltzValue);
            int enhancedRecovery = Mathf.RoundToInt(baseRecovery * moraleRecoveryMod);
            int next = currentMorale + enhancedRecovery;
            
            // Prevent overshoot across balance: snap to balance if crossing it
            if (next > balance) next = balance;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
            
            Debug.Log($"[StatManager] Morale recovery: {baseRecovery} (Waltz) × {moraleRecoveryMod:F2} (Euphony) = {enhancedRecovery} → {currentMorale} → {next}");
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
