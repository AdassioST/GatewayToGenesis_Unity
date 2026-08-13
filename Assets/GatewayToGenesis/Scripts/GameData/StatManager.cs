using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

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
    [SerializeField] private float discoveryEfficiencyCap = 0.45f;
    [SerializeField] private float savingRollChanceCap = 0.25f;
    
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

    [Header("Satisfaction System")]
    [SerializeField] private int satisfactionPoints = 0; // Current satisfaction points (-75 to +100, total range 175)
    [SerializeField] private int satisfactionLevel = 3; // Current satisfaction level (0-6, default: Content)
    [SerializeField] private int moralePointThreshold = 25; // How much morale above/below balance needed per satisfaction point
    [SerializeField] private int satisfactionUpgradeThreshold = 100; // Points needed to upgrade tier (positive threshold)
    [SerializeField] private int satisfactionDowngradeThreshold = -75; // Points needed to downgrade tier (negative threshold)
    [SerializeField] private int satisfactionTierSize = 175; // Size of each tier (automatically calculated: upgradeThreshold + |downgradeThreshold|)
    [SerializeField] private int satisfactionGraceBuffer = 0; // Grace buffer for downgrades (negative value, makes downgrades harder)
    
    // Satisfaction level definitions
    private readonly string[] satisfactionLevelNames = {
        "Forsaken",      // 0: Dystopian misery, no trust in life
        "Decadent",      // 1: People endure, but with little hope
        "Discontent",    // 2: Cracks in faith, whispers of rebellion
        "Content",       // 3: People live tolerably, functional but uninspired
        "Harmonious",    // 4: A society where justice and dignity are reliable
        "Resplendent",   // 5: People believe in fairness and promise of their world
        "Utopian"        // 6: Near mythical perfect state of joy, fairness, and aligned purpose
    };
    
    // Satisfaction level descriptions for tooltips/UI
    private readonly string[] satisfactionLevelDescriptions = {
        "Dystopian misery, no trust in life",
        "People endure, but with little hope",
        "Cracks in faith, whispers of rebellion",
        "People live tolerably, functional but uninspired",
        "A society where justice and dignity are reliable",
        "People believe in fairness and promise of their world",
        "Near mythical perfect state of joy, fairness, and aligned purpose"
    };

    // Core dictionaries for easy access
    private Dictionary<string, int> pillars = new Dictionary<string, int>();
    private Dictionary<string, int> substats = new Dictionary<string, int>();
    private Dictionary<string, float> derived = new Dictionary<string, float>();
    // Global named stats (civilization-wide). Includes morale and moraleBalance.
    private Dictionary<string, int> globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    
    // Persistent bonus system for legends and civics
    private Dictionary<string, Dictionary<string, float>> pillarBonuses = new Dictionary<string, Dictionary<string, float>>();
    private Dictionary<string, Dictionary<string, float>> substatBonuses = new Dictionary<string, Dictionary<string, float>>();
    private Dictionary<string, Dictionary<string, float>> derivedStatBonuses = new Dictionary<string, Dictionary<string, float>>();
    private Dictionary<string, Dictionary<string, int>> globalBonuses = new Dictionary<string, Dictionary<string, int>>();
    
    // Track original base values for proper recalculation (updated when permanent changes occur)
    private Dictionary<string, int> originalBaseValues = new Dictionary<string, int>();
    private Dictionary<string, int> originalPillarValues = new Dictionary<string, int>();

    // Events for UI updates
    public System.Action<string, int> OnPillarChanged;
    public System.Action<string, int> OnSubstatChanged;
    public System.Action<string, float> OnDerivedChanged;
    public System.Action<int> OnMoraleChanged;
    public System.Action<int, int> OnSatisfactionChanged; // (level, points)
    public System.Action<int, int, bool> OnSatisfactionLevelChanged; // (oldLevel, newLevel, isUpgrade)

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
                
                GameLoggingSystem.Instance.LogEvent($"Updated {statName} tier {tierIndex} balance modifier to {newBalanceModifier}", "StatManager");
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
    public float GetDiscoveryEfficiencyCap()
    {
        return Mathf.Clamp01(discoveryEfficiencyCap);
    }
    public void SetDiscoveryEfficiencyCap(float newCap)
    {
        discoveryEfficiencyCap = Mathf.Clamp01(newCap);
    }
    // Returns efficiency as 0-1 fraction with cap applied
    public float GetDiscoveryEfficiencyCapped()
    {
        float raw01 = Mathf.Max(0f, discoveryEfficiency / 100f);
        return Mathf.Min(raw01, GetDiscoveryEfficiencyCap());
    }
    // Returns efficiency as 0-100 percent with cap applied
    public float GetDiscoveryEfficiencyPercentCapped()
    {
        return GetDiscoveryEfficiencyCapped() * 100f;
    }

    // Saving Roll Chance helpers (centralized cap)
    public float GetSavingRollChanceCap()
    {
        return Mathf.Clamp01(savingRollChanceCap);
    }
    public void SetSavingRollChanceCap(float newCap)
    {
        savingRollChanceCap = Mathf.Clamp01(newCap);
    }
    // Returns efficiency as 0-1 fraction with cap applied
    public float GetSavingRollChanceCapped()
    {
        float raw01 = Mathf.Max(0f, savingRollChance / 100f);
        return Mathf.Min(raw01, GetSavingRollChanceCap());
    }
    // Returns efficiency as 0-100 percent with cap applied
    public float GetSavingRollChancePercentCapped()
    {
        return GetSavingRollChanceCapped() * 100f;
    }

    // Satisfaction Effectiveness helpers (centralized bonus)
    public float GetSatisfactionEffectivenessPercent()
    {
        return satisfactionEffectiveness;
    }
    public void SetSatisfactionEffectivenessPercent(float newPercent)
    {
        satisfactionEffectiveness = newPercent;
    }
    // Returns effectiveness as percentage (e.g., 3.15 = 3.15% bonus)
    public float GetSatisfactionEffectivenessBonus()
    {
        return satisfactionEffectiveness / 100f; // Convert percentage to decimal (3.15% → 0.0315)
    }
    // Returns effectiveness multiplier (e.g., 3.15% → 1.0315x multiplier)
    public float GetSatisfactionEffectivenessMultiplier()
    {
        return 1f + GetSatisfactionEffectivenessBonus();
    }

    // Grace Buffer helpers (downgrade protection from Symphony substat)
    public int GetSatisfactionGraceBuffer()
    {
        return satisfactionGraceBuffer;
    }
    
    /// <summary>
    /// Calculate grace buffer from satisfaction effectiveness
    /// Rules: Half of effectiveness, rounded to nearest multiple of 5, converted to negative integer
    /// Example: 23.32643% → 23 → 20 (nearest 5) → 10 (half) → -10 (negative for downgrade protection)
    /// </summary>
    public void CalculateSatisfactionGraceBuffer()
    {
        float effectiveness = GetDerivedValue("satisfactionEffectiveness");
        
        // Step 1: Round to nearest integer
        int roundedEffectiveness = Mathf.RoundToInt(effectiveness);
        
        // Step 2: Find highest multiple of 5 (round down to 5)
        int multipleOf5 = (roundedEffectiveness / 5) * 5;
        
        // Step 3: Calculate grace buffer as half of the multiple of 5
        int graceBuffer = multipleOf5 / 2;
        
        // Step 4: Convert to negative (negative grace buffer makes downgrades harder)
        int newGraceBuffer = -graceBuffer;
        
        if (satisfactionGraceBuffer != newGraceBuffer)
        {
            int oldGraceBuffer = satisfactionGraceBuffer;
            satisfactionGraceBuffer = newGraceBuffer;

            // Recalculate tier size since grace buffer affects effective downgrade threshold
            RecalculateSatisfactionTierSize();
        }
    }
    
    /// <summary>
    /// Get the effective downgrade threshold (base threshold + grace buffer)
    /// This is what actually determines when downgrades occur
    /// </summary>
    public int GetEffectiveDowngradeThreshold()
    {
        return satisfactionDowngradeThreshold + satisfactionGraceBuffer;
    }
    
    /// <summary>
    /// Recalculate satisfaction tier size using effective downgrade threshold
    /// This ensures tier size accounts for grace buffer protection
    /// </summary>
    private void RecalculateSatisfactionTierSize()
    {
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        satisfactionTierSize = satisfactionUpgradeThreshold + Mathf.Abs(effectiveDowngradeThreshold);
        
        GameLoggingSystem.Instance.LogEvent($"Tier size recalculated: {satisfactionTierSize} (upgrade: {satisfactionUpgradeThreshold}, effective downgrade: {effectiveDowngradeThreshold} = base {satisfactionDowngradeThreshold} + grace {satisfactionGraceBuffer})", "StatManager");
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
        
        // PHASE 1: Initialize base systems only (no calculations yet)
        InitializeBaseSystems();
        
        Debug.Log("[StatManager] Initialized: Base Systems Ready");
    }
    
    /// <summary>
    /// Initialize base systems without calculations - called in Awake()
    /// </summary>
    private void InitializeBaseSystems()
    {
        // Initialize base dictionaries and values
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
        derivationFormulas = new Dictionary<string, System.Func<int, float>>
        {
            {"discoveryEfficiency", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["discoveryEfficiency"])},
            {"savingRollChance", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["savingRollChance"])},
            {"legendEffectiveness", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["legendEffectiveness"])},
            {"expeditionCostMod", val => 1f - CalculateTieredGrowth(val, tieredGrowthConfigs["expeditionCostMod"])},
            {"expeditionTimeMod", val => 1f - CalculateTieredGrowth(val, tieredGrowthConfigs["expeditionTimeMod"])},
            {"satisfactionEffectiveness", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["satisfactionEffectiveness"])},
            {"moraleLossMod", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["moraleLossMod"])},
            {"moraleRecoveryMod", val => 1f + CalculateTieredGrowth(val, tieredGrowthConfigs["moraleRecoveryMod"])},
            {"clickPowerBonus", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["clickPowerBonus"])},
            {"magicEffectiveness", val => 0f + CalculateTieredGrowth(val, tieredGrowthConfigs["magicEffectiveness"])}
        };
        
        // Initialize satisfaction tier size based on thresholds
        satisfactionTierSize = satisfactionUpgradeThreshold + Mathf.Abs(satisfactionDowngradeThreshold);
        
        // Calculate initial grace buffer
        CalculateSatisfactionGraceBuffer();
    }

    private void Start()
    {
        // PHASE 2: Subscribe to time system and trigger full stat recalculation
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange += OnSeventhTick;
        }
        else
        {
            // Attempt delayed subscription
            StartCoroutine(SubscribeToTimeWhenReady());
        }
        
        // Start the initialization sequence after all managers have had their Start() called
        StartCoroutine(CompleteInitializationSequence());
        
        GameLoggingSystem.Instance.LogEvent("Init: sequence start", "StatManager");
    }
    
    /// <summary>
    /// Complete the initialization sequence after all systems are ready
    /// This ensures all bonuses are applied and stats are calculated correctly
    /// </summary>
    private System.Collections.IEnumerator CompleteInitializationSequence()
    {
        // Wait one frame to ensure all other managers have completed their Start() methods
            yield return null;
        
        GameLoggingSystem.Instance.LogEvent("Init: full recalculation", "StatManager");
        
        // Force a complete recalculation of all stats from base values
        // This ensures any bonuses applied by other systems are properly integrated
        RecalculateAllStatsFromBase();
        
        // Calculate derived stats with the updated substat values
        CalculateDerivedStats();
        
        // Trigger events for all stats to ensure UI is synchronized
        TriggerAllStatEvents();
        
        GameLoggingSystem.Instance.LogEvent("Init: done", "StatManager");
    }
    
    /// <summary>
    /// Trigger events for all stats to ensure UI synchronization
    /// </summary>
    private void TriggerAllStatEvents()
    {
        // Create copies to avoid "Collection was modified during enumeration" errors
        // This happens when event handlers modify the same collections being iterated
        
        // Trigger pillar events
        var pillarsCopy = new Dictionary<string, int>(pillars);
        foreach (var pillar in pillarsCopy)
        {
            OnPillarChanged?.Invoke(pillar.Key, pillar.Value);
        }
        
        // Trigger substat events
        var substatsCopy = new Dictionary<string, int>(substats);
        foreach (var substat in substatsCopy)
        {
            OnSubstatChanged?.Invoke(substat.Key, substat.Value);
        }
        
        // Trigger derived stat events
        var derivedCopy = new Dictionary<string, float>(derived);
        foreach (var derivedStat in derivedCopy)
        {
            OnDerivedChanged?.Invoke(derivedStat.Key, derivedStat.Value);
        }
        
        // Trigger global stat events
        OnMoraleChanged?.Invoke(globals["morale"]);
        OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
    }
    
    /// <summary>
    /// Log current stat values for debugging
    /// </summary>
    private void LogCurrentStatValues()
    {
        GameLoggingSystem.Instance.LogEvent($"PILLARS: Aureus={aureus}, Regalia={regalia}, Waltz={waltz}, Chorus={chorus}", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"SUBSTATS: Innovation={innovation}, Authority={authority}, Piety={piety}, Symphony={symphony}", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"DERIVED: LegendEffectiveness={legendEffectiveness:F2}, DiscoveryEfficiency={discoveryEfficiency:F2}", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"GLOBALS: Morale={globals["morale"]}, MaxMorale={globals["maxmorale"]}, SatisfactionThreshold={globals["satisfactionupgradethreshold"]}", "StatManager");
    }
    
    /// <summary>
    /// Force a complete recalculation of all stats - can be called by other systems
    /// Useful for ensuring stats are updated after external changes
    /// </summary>
    public void ForceCompleteRecalculation()
    {
        // reduce noise: keep only completion
        
        RecalculateAllStatsFromBase();
        CalculateDerivedStats();
        TriggerAllStatEvents();
        
        GameLoggingSystem.Instance.LogEvent("Recalc complete", "StatManager");
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

    // REMOVED: OnValidate method - all initialization happens during runtime only

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
        globals["moraleBalance"] = moraleBalance;
        globals["maxmorale"] = maxMorale;
        globals["satisfactionPoints"] = satisfactionPoints;
        globals["satisfactionLevel"] = satisfactionLevel;
        globals["satisfactionupgradethreshold"] = satisfactionUpgradeThreshold;
        
        // Initialize original base values for proper recalculation
        InitializeOriginalBaseValues();
        InitializeOriginalPillarValues();
    }
    
    /// <summary>
    /// Initialize the original base values dictionary with current stat values.
    /// Call this during initialization and whenever permanent upgrades change base stats.
    /// </summary>
    private void InitializeOriginalBaseValues()
    {
        originalBaseValues["maxmorale"] = maxMorale;
        originalBaseValues["satisfactionupgradethreshold"] = satisfactionUpgradeThreshold;
        originalBaseValues["moraleBalance"] = moraleBalance;
        originalBaseValues["morale"] = 100; // Default starting morale
        originalBaseValues["satisfactionpoints"] = 0; // Default satisfaction points
    }
    
    /// <summary>
    /// Update the original base value for a specific global stat.
    /// Call this when permanent upgrades change the base value of a stat.
    /// </summary>
    /// <param name="globalKey">Global stat key (lowercase)</param>
    /// <param name="newBaseValue">New base value</param>
    public void UpdateOriginalBaseValue(string globalKey, int newBaseValue)
    {
        originalBaseValues[globalKey] = newBaseValue;
        
        // Also update the corresponding serialized field
        switch (globalKey)
        {
            case "maxmorale":
                maxMorale = newBaseValue;
                break;
            case "satisfactionupgradethreshold":
                satisfactionUpgradeThreshold = newBaseValue;
                break;
            case "moraleBalance":
                moraleBalance = newBaseValue;
                break;
        }
        
        // Recalculate the stat with bonuses to reflect the new base value
        RecalculateGlobalStatWithBonuses(globalKey);
        
        GameLoggingSystem.Instance.LogEvent($"Updated original base value for '{globalKey}' to {newBaseValue} (permanent upgrade)", "StatManager");
    }
    
    /// <summary>
    /// Initialize the original pillar values dictionary with current pillar values.
    /// </summary>
    private void InitializeOriginalPillarValues()
    {
        originalPillarValues["aureus"] = aureus;
        originalPillarValues["regalia"] = regalia;
        originalPillarValues["waltz"] = waltz;
        originalPillarValues["chorus"] = chorus;
    }
    
    /// <summary>
    /// Get the original pillar value without any bonuses
    /// </summary>
    private int GetOriginalPillarValue(string pillarKey)
    {
        // Use tracked original value if available
        if (originalPillarValues.ContainsKey(pillarKey))
        {
            return originalPillarValues[pillarKey];
        }
        
        // Fallback to current value
        return pillars.ContainsKey(pillarKey) ? pillars[pillarKey] : 10;
    }
    
    /// <summary>
    /// Calculate substat value from base pillar values (without bonuses)
    /// </summary>
    private int CalculateSubstatFromFinalPillars(string substatKey)
    {
        // Find which pillar affects this substat
        string parentPillar = GetPillarForSubstat(substatKey);
        if (string.IsNullOrEmpty(parentPillar)) return 1;
        
        // Get FINAL pillar value (including bonuses) and multiplier
        int finalPillarValue = GetPillarValue(parentPillar);
        float multiplier = pillarMultipliers.GetValueOrDefault(parentPillar, 0.5f);
        
        // DEBUG: Show corrected calculation
        if (substatKey == "authority")
        {
            int basePillarValue = GetOriginalPillarValue(parentPillar);
            int resultFromFinal = Mathf.Max(1, Mathf.RoundToInt(finalPillarValue * multiplier));
            
            GameLoggingSystem.Instance.LogEvent($"FIXED Authority Calculation: Parent Pillar: {parentPillar}, Base Pillar Value: {basePillarValue}, Final Pillar Value: {finalPillarValue}, Multiplier: {multiplier}, USING Final: {finalPillarValue} × {multiplier} = {resultFromFinal}", "StatManager");
        }
        
        return Mathf.Max(1, Mathf.RoundToInt(finalPillarValue * multiplier));
    }
    
    /// <summary>
    /// Get the pillar that affects a specific substat
    /// </summary>
    private string GetPillarForSubstat(string substatKey)
    {
        foreach (var kvp in pillarSubstats)
        {
            if (kvp.Value.Contains(substatKey))
            {
                return kvp.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Calculate derived stat from FINAL substat values (with bonuses applied)
    /// This is the correct approach - derived stats should reflect final substat values
    /// </summary>
    private float CalculateDerivedFromFinalSubstats(string derivedKey)
    {
        // Use current final substat values (base + bonuses already applied)
        Dictionary<string, int> finalSubstats = new Dictionary<string, int>();
        foreach (string substatKey in substats.Keys)
        {
            finalSubstats[substatKey] = substats[substatKey]; // These already include bonuses
        }
        
        // Calculate derived stat from final substat values
        return CalculateDerivedStatFromSubstats(derivedKey, finalSubstats);
    }
    
    /// <summary>
    /// Calculate a specific derived stat from provided substat values
    /// </summary>
    private float CalculateDerivedStatFromSubstats(string derivedKey, Dictionary<string, int> substatValues)
    {
        // Use the derivation formula if available
        if (derivationFormulas != null && derivationFormulas.ContainsKey(derivedKey))
        {
            string substatName = GetSubstatForDerived(derivedKey);
            if (substatValues.ContainsKey(substatName))
            {
                float result = derivationFormulas[derivedKey](substatValues[substatName]);
                // noisy per-stat calc removed
                return result;
            }
        }
        
        // Special case: Legend Effectiveness has recursive issues, silently use full recalculation
        if (derivedKey.ToLower() == "legendeffectiveness")
        {
            if (Application.isPlaying && derivationFormulas != null)
            {
                CalculateDerivedStats();
                return derived.GetValueOrDefault(derivedKey.ToLower(), 0f);
            }
            return 0f; // During initialization
        }
        
        // Safe fallback during initialization - trigger full recalculation instead
        GameLoggingSystem.Instance.LogEvent($"Cannot calculate individual derived stat '{derivedKey}' - triggering full recalculation", "StatManager");
        if (Application.isPlaying && derivationFormulas != null)
        {
            CalculateDerivedStats();
            return derived.GetValueOrDefault(derivedKey.ToLower(), 0f);
        }
        
        // During initialization, return safe default
        return 0f;
    }
    
    /// <summary>
    /// Recalculate all stats from base values in proper order
    /// This ensures clean calculation without cascading bonus effects
    /// </summary>
    private void RecalculateAllStatsFromBase()
    {
        // 1. Calculate substats from base pillars + substat bonuses
        foreach (string substatKey in substats.Keys.ToList())
        {
            int baseValue = CalculateSubstatFromFinalPillars(substatKey);
            float bonus = GetSubstatBonus(substatKey);
            int finalValue = Mathf.Max(1, baseValue + Mathf.RoundToInt(bonus));
            
            substats[substatKey] = finalValue;
            SetSubstatField(substatKey, finalValue);
        }
        
        // 2. Calculate derived stats using SINGLE SOURCE OF TRUTH (tiered formulas)
        CalculateDerivedStats();
    }

        private void CalculateDerivedStats()
    {
        // SINGLE SOURCE OF TRUTH: Only use tiered growth formulas
        if (tieredGrowthConfigs == null)
        {
            Debug.LogError("[StatManager] CRITICAL: tieredGrowthConfigs is null! Initialization failed.");
            return;
        }
        
        if (derivationFormulas == null)
        {
            Debug.LogError("[StatManager] CRITICAL: derivationFormulas is null! Initialization failed.");
            return;
        }

        // Use the tiered growth system - THE ONLY CALCULATION METHOD
        foreach (var formula in derivationFormulas)
        {
            string substatName = GetSubstatForDerived(formula.Key);
            if (substats.ContainsKey(substatName))
            {
                int substatValue = substats[substatName];
                float value = formula.Value(substatValue);
                derived[formula.Key.ToLower()] = value;
                SetDerivedField(formula.Key, value);
                
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"⚠️ Missing substat '{substatName}' for derived stat '{formula.Key}'", "StatManager");
            }
        }

        // Special case for communion stage
        communionStage = Mathf.FloorToInt(substats["secrecy"] / 5f);
        derived["communionStage"] = communionStage;
        
        // Recalculate grace buffer when satisfaction effectiveness changes
        CalculateSatisfactionGraceBuffer();
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
        switch (fieldName.ToLower())
        {
            case "discoveryefficiency": discoveryEfficiency = value; break;
            case "savingrollchance": savingRollChance = value; break;
            case "legendeffectiveness": legendEffectiveness = value; break;
            case "expeditioncostmod": expeditionCostMod = value; break;
            case "expeditiontimemod": expeditionTimeMod = value; break;
            case "satisfactioneffectiveness": satisfactionEffectiveness = value; break;
            case "moralelossmod": moraleLossMod = value; break;
            case "moralerecoverymod": moraleRecoveryMod = value; break;
            case "clickpowerbonus": clickPowerBonus = value; break;
            case "magiceffectiveness": magicEffectiveness = value; break;
        }
    }

    // Core public methods
    public void UpdatePillar(string pillarName, int newValue)
    {
        string key = pillarName.ToLower();
        if (pillars.ContainsKey(key))
        {
            // Store the base value (without bonuses)
            pillars[key] = newValue;
            
            // Recalculate with all active bonuses
            RecalculatePillarWithBonuses(key);
            
            // Update substats (this will use the final value with bonuses)
            UpdatePillarSubstats(key);
        }
    }

    public void UpdateSubstat(string substatName, int newValue)
    {
        string key = substatName.ToLower();
        if (substats.ContainsKey(key))
        {
            // Store the base value (without bonuses)
            substats[key] = newValue;
            
            // Recalculate with all active bonuses
            RecalculateSubstatWithBonuses(key);
            
            // Calculate derived stats (this will use the final values with bonuses)
            CalculateDerivedStats();
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

        // Check globals (e.g., morale, moraleBalance)
        if (globals.ContainsKey(key))
        {
                    // Special handling for morale to ensure loss mitigation
        if (key == "morale")
        {
            int currentMorale = GetMorale();
            int delta = newValue - currentMorale;
            if (delta != 0)
            {
                GameLoggingSystem.Instance.LogEvent($"Routing morale: {currentMorale} → {newValue} (delta: {delta})", "StatManager");
                // Apply the enhanced delta directly to avoid double-application
                int enhancedDelta = ApplyMoraleShiftAndReturnEnhanced(delta, "UpdateStat", false);
                UpdateGlobal("morale", currentMorale + enhancedDelta);
            }
            return;
        }
            
            // Special handling for satisfaction points
            if (key == "satisfactionpoints")
            {
                int currentPoints = GetSatisfactionPoints();
                int delta = newValue - currentPoints;
                if (delta != 0)
                {
                    GameLoggingSystem.Instance.LogEvent($"Routing satisfaction points: {currentPoints} → {newValue} (delta: {delta})", "StatManager");
                    ModifySatisfactionPoints(delta, "UpdateStat");
                }
                return;
            }
            
            // Special handling for satisfaction level
            if (key == "satisfactionlevel")
            {
                int currentLevel = GetSatisfactionLevel();
                if (newValue != currentLevel)
                {
                    GameLoggingSystem.Instance.LogEvent($"Routing satisfaction level: {currentLevel} → {newValue}", "StatManager");
                    SetSatisfactionLevel(newValue, "UpdateStat");
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
    /// Update a global stat value (e.g., morale, moraleBalance)
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
        if (key == "moraleBalance")
        {
            globals["moraleBalance"] = newValue;
            moraleBalance = newValue;
            return;
        }
        if (key == "maxmorale")
        {
            globals["maxmorale"] = newValue;
            maxMorale = newValue;
            GameLoggingSystem.Instance.LogEvent($"Updated maxMorale to {newValue}", "StatManager");
            return;
        }
        if (key == "satisfactionupgradethreshold")
        {
            globals["satisfactionupgradethreshold"] = newValue;
            satisfactionUpgradeThreshold = newValue;
            GameLoggingSystem.Instance.LogEvent($"Updated satisfactionUpgradeThreshold to {newValue}", "StatManager");
            return;
        }
        if (key == "satisfactionPoints")
        {
            satisfactionPoints = newValue;
            OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
            return;
        }
        if (key == "satisfactionLevel")
        {
            satisfactionLevel = newValue;
            OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
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
            
            // Get the final pillar value (base + bonuses) for substat calculation
            int finalPillarValue = GetPillarValue(pillarName);
            int newSubstatValue = Mathf.Max(1, Mathf.RoundToInt(finalPillarValue * multiplier));
            
            GameLoggingSystem.Instance.LogEvent($"Pillar→Substats: {pillarName} → {newSubstatValue}", "StatManager");
            
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
            
            GameLoggingSystem.Instance.LogEvent($"Updated {pillarName} multiplier to {newMultiplier:F2}", "StatManager");
        }
        else
        {
            Debug.LogWarning($"[StatManager] Unknown pillar '{pillarName}' for multiplier update");
        }
    }

    private void SetPillarField(string fieldName, int value)
    {
        switch (fieldName.ToLower())
        {
            case "aureus": aureus = value; break;
            case "regalia": regalia = value; break;
            case "waltz": waltz = value; break;
            case "chorus": chorus = value; break;
        }
    }

    private void SetSubstatField(string fieldName, int value)
    {
        switch (fieldName.ToLower())
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
    public int GetPillarValue(string pillarName) 
    {
        string key = pillarName.ToLower();
        if (pillars.TryGetValue(key, out int baseValue))
        {
            float totalBonus = GetPillarBonus(pillarName);
            return Mathf.Max(1, baseValue + Mathf.RoundToInt(totalBonus));
        }
        return 0;
    }
    
    public int GetSubstatValue(string substatName) 
    {
        string key = substatName.ToLower();
        if (substats.TryGetValue(key, out int baseValue))
        {
            float totalBonus = GetSubstatBonus(substatName);
            return Mathf.Max(1, baseValue + Mathf.RoundToInt(totalBonus));
        }
        return 0;
    }
    
    public float GetDerivedValue(string derivedName) 
    {
        string key = derivedName.ToLower();
        if (derived.TryGetValue(key, out float baseValue))
        {
            float totalBonus = GetDerivedStatBonus(derivedName);
            
            return baseValue + totalBonus;
        }
        return 0f;
    }
    
    public int GetGlobalValue(string globalName) 
    {
        string key = globalName.ToLower();
        if (globals.TryGetValue(key, out int baseValue))
        {
            int totalBonus = GetGlobalBonus(globalName);
            return baseValue + totalBonus;
        }
        return 0;
    }
    
    /// <summary>
    /// Get the base value of a pillar (without bonuses)
    /// </summary>
    public int GetBasePillarValue(string pillarName) => pillars.TryGetValue(pillarName.ToLower(), out int value) ? value : 0;
    
    /// <summary>
    /// Get the base value of a substat (without bonuses)
    /// </summary>
    public int GetBaseSubstatValue(string substatName) => substats.TryGetValue(substatName.ToLower(), out int value) ? value : 0;
    
    /// <summary>
    /// Get the base value of a derived stat (without bonuses)
    /// </summary>
    public float GetBaseDerivedValue(string derivedName) => derived.TryGetValue(derivedName.ToLower(), out float value) ? value : 0f;
    
    /// <summary>
    /// Get the base value of a global stat (without bonuses)
    /// </summary>
    public int GetBaseGlobalValue(string globalName)
    {
        string key = globalName.ToLower();
        bool found = globals.TryGetValue(key, out int value);
        
        // Enhanced debugging for satisfaction and morale stats
        if (key == "maxmorale" || key == "satisfactionupgradethreshold")
        {
            GameLoggingSystem.Instance.LogEvent($"GetBaseGlobalValue('{key}') - found: {found}, value: {value}", "StatManager");
            if (!found)
            {
                Debug.LogWarning($"[StatManager] '{key}' not found in globals dictionary! Available keys: {string.Join(", ", globals.Keys)}");
            }
        }
        
        return found ? value : 0;
    }

    // Event system compatibility
    public bool CheckStat(string statName, int requiredValue)
    {
        string key = statName.ToLower();
        
        // Special handling for satisfaction level checks
        if (key == "satisfactionlevel")
        {
            return GetSatisfactionLevel() >= requiredValue;
        }
        
        return (pillars.TryGetValue(key, out int pillarValue) && pillarValue >= requiredValue) ||
               (substats.TryGetValue(key, out int substatValue) && substatValue >= requiredValue) ||
               (globals.TryGetValue(key, out int globalValue) && globalValue >= requiredValue);
    }

    public int GetStatValue(string statName)
    {
        string key = statName.ToLower();
        
        // Special handling for satisfaction level
        if (key == "satisfactionlevel")
        {
            return GetSatisfactionLevel();
        }
        
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
        GameLoggingSystem.Instance.LogEvent("=== CIVILIZATION STATS ===", "StatManager");
        
        // Show pillars with base values and final values (including bonuses)
        GameLoggingSystem.Instance.LogEvent($"Aureus: Base {GetBasePillarValue("aureus")} → Final {GetPillarValue("aureus")} (Innovation: Base {GetBaseSubstatValue("innovation")} → Final {GetSubstatValue("innovation")}, Piety: Base {GetBaseSubstatValue("piety")} → Final {GetSubstatValue("piety")})", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"Regalia: Base {GetBasePillarValue("regalia")} → Final {GetPillarValue("regalia")} (Authority: Base {GetBaseSubstatValue("authority")} → Final {GetSubstatValue("authority")}, Ambition: Base {GetBaseSubstatValue("ambition")} → Final {GetSubstatValue("ambition")})", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"Waltz: Base {GetBasePillarValue("waltz")} → Final {GetPillarValue("waltz")} (Symphony: Base {GetBaseSubstatValue("symphony")} → Final {GetSubstatValue("symphony")}, Euphony: Base {GetBaseSubstatValue("euphony")} → Final {GetSubstatValue("euphony")})", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"Chorus: Base {GetBasePillarValue("chorus")} → Final {GetPillarValue("chorus")} (Arcane: Base {GetBaseSubstatValue("arcane")} → Final {GetSubstatValue("arcane")}, Secrecy: Base {GetBaseSubstatValue("secrecy")} → Final {GetSubstatValue("secrecy")})", "StatManager");
        
        // Show active bonuses
        GameLoggingSystem.Instance.LogEvent("=== ACTIVE BONUSES ===", "StatManager");
        foreach (var pillar in new[] { "aureus", "regalia", "waltz", "chorus" })
        {
            float bonus = GetPillarBonus(pillar);
            if (bonus > 0)
            {
                var sources = GetBonusSources("pillar", pillar);
                GameLoggingSystem.Instance.LogEvent($"  {pillar}: +{bonus:F1} from {string.Join(", ", sources.Select(kvp => $"{kvp.Key}(+{kvp.Value:F1})"))}", "StatManager");
            }
        }
        
        GameLoggingSystem.Instance.LogEvent($"Morale: {GetMorale()} (Balance: {GetMoraleBalance()})", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"Dark Morale: {EventSystemLogic.Instance?.GetEventScore("dark_morale")}", "StatManager");
        
        // Show detailed satisfaction information
        var tierInfo = GetSatisfactionTierInfo();
        var progress = GetSatisfactionProgress();
        GameLoggingSystem.Instance.LogEvent($"Satisfaction: {tierInfo.name} (Level {tierInfo.level}) - {GetSatisfactionPoints()} points", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"  Tier Range: {tierInfo.minPoints} to {tierInfo.maxPoints} points", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"  Progress in tier: {progress.progress:P1} ({progress.pointsInTier} points)", "StatManager");
        if (progress.pointsForNext > 0)
            GameLoggingSystem.Instance.LogEvent($"  Points needed for next tier: {progress.pointsForNext}", "StatManager");
        if (progress.pointsForPrevious > 0)
            GameLoggingSystem.Instance.LogEvent($"  Points to lose for previous tier: {progress.pointsForPrevious}", "StatManager");
        
        // Show satisfaction effectiveness information
        float satisfactionEffectiveness = GetDerivedValue("satisfactionEffectiveness");
        GameLoggingSystem.Instance.LogEvent($"  Satisfaction Effectiveness: {satisfactionEffectiveness:F2}% (Symphony: {GetSubstatValue("symphony")})", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"  Effectiveness Multiplier: {GetSatisfactionEffectivenessMultiplier():F3}x", "StatManager");
        
        GameLoggingSystem.Instance.LogEvent($"  Effectiveness Multiplier: {GetSatisfactionEffectivenessMultiplier():F3}x", "StatManager");
        
        // Show grace buffer information
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        GameLoggingSystem.Instance.LogEvent($"  Grace Buffer: {satisfactionGraceBuffer} (downgrade protection)", "StatManager");
        GameLoggingSystem.Instance.LogEvent($"  Effective Downgrade Threshold: {effectiveDowngradeThreshold} (base {satisfactionDowngradeThreshold} + grace {satisfactionGraceBuffer})", "StatManager");
        
        GameLoggingSystem.Instance.LogEvent("=== DERIVED STATS ===", "StatManager");
        foreach (var kvp in derived)
        {
            // Display as decimal percentages (e.g., 101.145 instead of 10,114.5%)
            // Values are already percentages, don't multiply by 100
            float baseValue = kvp.Value;
            float finalValue = GetDerivedValue(kvp.Key);
            if (Mathf.Abs(baseValue - finalValue) > 0.001f)
            {
                GameLoggingSystem.Instance.LogEvent($"{kvp.Key}: Base {baseValue:F3} → Final {finalValue:F3}", "StatManager");
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"{kvp.Key}: {finalValue:F3}", "StatManager");
            }
        }
    }



    public int GetMorale()
    {
        return GetGlobalValue("morale");
    }

    public int GetMoraleBalance()
    {
        return GetGlobalValue("moraleBalance");
    }
    
    /// <summary>
    /// Get the base morale balance without bonuses (for oscillation target)
    /// </summary>
    public int GetBaseMoraleBalance()
    {
        return GetBaseGlobalValue("moraleBalance");
    }

    public float GetMoraleDeltaPercent()
    {
        // Difference from equilibrium: positive => bonus, negative => malus
        return GetMorale() - GetMoraleBalance();
    }

    // Satisfaction System API
    public int GetSatisfactionPoints()
    {
        return GetGlobalValue("satisfactionPoints");
    }

    public int GetSatisfactionLevel()
    {
        return GetGlobalValue("satisfactionLevel");
    }

    public string GetSatisfactionLevelName()
    {
        int level = GetSatisfactionLevel();
        if (level >= 0 && level < satisfactionLevelNames.Length)
        {
            return satisfactionLevelNames[level];
        }
        return "Unknown";
    }

    public string GetSatisfactionLevelDescription()
    {
        int level = GetSatisfactionLevel();
        if (level >= 0 && level < satisfactionLevelDescriptions.Length)
        {
            return satisfactionLevelDescriptions[level];
        }
        return "Unknown satisfaction level";
    }

    /// <summary>
    /// Get information about the current satisfaction tier
    /// </summary>
    public (int level, string name, string description, int minPoints, int maxPoints) GetSatisfactionTierInfo()
    {
        int level = GetSatisfactionLevel();
        if (level >= 0 && level < satisfactionLevelNames.Length)
        {
            // Each tier spans from effective downgrade threshold to upgrade threshold
            int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
            int minPoints = effectiveDowngradeThreshold; // -75 + grace buffer
            int maxPoints = satisfactionUpgradeThreshold;   // +100
            
            return (level, satisfactionLevelNames[level], satisfactionLevelDescriptions[level], minPoints, maxPoints);
        }
        return (0, "Unknown", "Unknown satisfaction level", 0, 0);
    }

    /// <summary>
    /// Get progress information for the current satisfaction tier
    /// </summary>
    public (float progress, int pointsInTier, int pointsForNext, int pointsForPrevious) GetSatisfactionProgress()
    {
        var tierInfo = GetSatisfactionTierInfo();
        int currentPoints = GetSatisfactionPoints();
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        
        // Points within current tier (effective downgrade threshold to upgrade threshold)
        int pointsInTier = currentPoints - effectiveDowngradeThreshold; // Convert from effective range to 0-based
        float progress = (float)pointsInTier / satisfactionTierSize;
        
        // Points needed for next level (from current points to +100)
        int pointsForNext = satisfactionUpgradeThreshold - currentPoints;
        
        // Points to lose for previous level (from current points to effective downgrade threshold)
        int pointsForPrevious = currentPoints - effectiveDowngradeThreshold;
        
        return (progress, pointsInTier, pointsForNext, pointsForPrevious);
    }

    /// <summary>
    /// Centralized method to change satisfaction points from external systems
    /// Automatically handles level updates and provides detailed logging
    /// </summary>
    /// <param name="change">Amount to change (positive or negative)</param>
    /// <param name="source">Source of the change (e.g., "Building", "Event", "Crisis")</param>
    public void ChangeSatisfactionPoints(int change, string source = null)
    {
        if (change == 0) return;
        
        int oldPoints = satisfactionPoints;
        int oldLevel = satisfactionLevel;
        
        // Apply satisfaction effectiveness bonus from Symphony substat (only for positive changes)
        float satisfactionEffectiveness = GetDerivedValue("satisfactionEffectiveness");
        float effectivenessMultiplier = GetSatisfactionEffectivenessMultiplier(); // Use helper method
        
        // Calculate enhanced change with effectiveness bonus (only for positive changes)
        float enhancedChange = change;
        int finalChange = change;
        
        if (change > 0)
        {
            // Apply effectiveness bonus only to positive changes
            enhancedChange = change * effectivenessMultiplier;
            finalChange = Mathf.RoundToInt(enhancedChange);
            
            // Log effectiveness application
            GameLoggingSystem.Instance.LogEvent($"Satisfaction effectiveness: {change} × {effectivenessMultiplier:F3} (Symphony: {satisfactionEffectiveness:F2}%) = {enhancedChange:F2} → {finalChange}", "StatManager");
        }
        else
        {
            // Negative changes (penalties) are not affected by effectiveness
            finalChange = change;
        }
        
        // Apply the enhanced change using effective downgrade threshold (includes grace buffer)
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        satisfactionPoints = Mathf.Clamp(satisfactionPoints + finalChange, effectiveDowngradeThreshold, satisfactionUpgradeThreshold);
        
        // Update globals
        globals["satisfactionPoints"] = satisfactionPoints;
        
        // Check for level changes
        UpdateSatisfactionLevel();
        
        // Log the change
        string direction = finalChange > 0 ? "+" : "";
        string effectivenessNote = change > 0 && finalChange != change ? " (with effectiveness bonus)" : "";
        GameLoggingSystem.Instance.LogEvent($"Satisfaction points changed by {direction}{finalChange} from '{source}'{effectivenessNote} → {oldPoints} → {satisfactionPoints} (range: {effectiveDowngradeThreshold} to {satisfactionUpgradeThreshold}, grace buffer: {satisfactionGraceBuffer})", "StatManager");
        
        // Log level change if it occurred
        if (satisfactionLevel != oldLevel)
        {
            string levelChangeType = satisfactionLevel > oldLevel ? "UPGRADE" : "DOWNGRADE";
            GameLoggingSystem.Instance.LogEvent($"Level change triggered: {satisfactionLevelNames[oldLevel]} → {satisfactionLevelNames[satisfactionLevel]} ({levelChangeType})", "StatManager");
        }
        
        // Notify listeners
        OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
    }

    /// <summary>
    /// Apply direct satisfaction points change (for events, decisions, etc.)
    /// </summary>
    public void ModifySatisfactionPoints(int change, string source = null)
    {
        int oldPoints = satisfactionPoints;
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        satisfactionPoints = Mathf.Clamp(satisfactionPoints + change, effectiveDowngradeThreshold, satisfactionUpgradeThreshold); // Clamp to effective range including grace buffer
        
        if (satisfactionPoints != oldPoints)
        {
            globals["satisfactionPoints"] = satisfactionPoints;
            UpdateSatisfactionLevel();
            
            GameLoggingSystem.Instance.LogEvent($"Satisfaction points modified by {change} from '{source}' → {oldPoints} → {satisfactionPoints} (effective range: {effectiveDowngradeThreshold} to {satisfactionUpgradeThreshold}, grace buffer: {satisfactionGraceBuffer})", "StatManager");
            OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
        }
    }

    /// <summary>
    /// Set satisfaction level directly (for special events, civic system, etc.)
    /// </summary>
    public void SetSatisfactionLevel(int newLevel, string source = null)
    {
        if (newLevel < 0 || newLevel >= satisfactionLevelNames.Length)
        {
            Debug.LogWarning($"[StatManager] Invalid satisfaction level: {newLevel}. Must be 0-{satisfactionLevelNames.Length - 1}");
            return;
        }

        int oldLevel = satisfactionLevel;
        satisfactionLevel = newLevel;
        globals["satisfactionLevel"] = satisfactionLevel;
        
        // Set points to 0 (middle of the -75 to +100 range) for stability
        satisfactionPoints = 0;
        globals["satisfactionPoints"] = satisfactionPoints;
        
        GameLoggingSystem.Instance.LogEvent($"Satisfaction level set to {satisfactionLevelNames[newLevel]} ({newLevel}) with {satisfactionPoints} points from '{source}'", "StatManager");
        OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
    }

    /// <summary>
    /// Update the satisfaction upgrade threshold (points needed to upgrade from Content)
    /// </summary>
    public void SetSatisfactionUpgradeThreshold(int newThreshold, string source = null)
    {
        int oldThreshold = satisfactionUpgradeThreshold;
        satisfactionUpgradeThreshold = newThreshold;
        
        // Automatically recalculate tier size using new method
        RecalculateSatisfactionTierSize();
        
        GameLoggingSystem.Instance.LogEvent($"Satisfaction upgrade threshold changed from {oldThreshold} to {newThreshold} from '{source}'", "StatManager");
        
        // Recalculate satisfaction level with new thresholds
        UpdateSatisfactionLevel();
    }

    /// <summary>
    /// Update the satisfaction downgrade threshold (points needed to downgrade from Content)
    /// </summary>
    public void SetSatisfactionDowngradeThreshold(int newThreshold, string source = null)
    {
        int oldThreshold = satisfactionDowngradeThreshold;
        satisfactionDowngradeThreshold = newThreshold;
        
        // Automatically recalculate tier size using new method
        RecalculateSatisfactionTierSize();
        
        GameLoggingSystem.Instance.LogEvent($"Satisfaction downgrade threshold changed from {oldThreshold} to {newThreshold} from '{source}'", "StatManager");
        
        // Recalculate satisfaction level with new thresholds
        UpdateSatisfactionLevel();
    }

    /// <summary>
    /// Update the satisfaction tier size (points per tier) - READ ONLY, automatically calculated from thresholds
    /// </summary>
    public void SetSatisfactionTierSize(int newTierSize, string source = null)
    {
        Debug.LogWarning($"[StatManager] Tier size is automatically calculated from thresholds and cannot be set manually. Current: {satisfactionTierSize} (upgrade: {satisfactionUpgradeThreshold}, downgrade: {satisfactionDowngradeThreshold})");
        Debug.LogWarning($"[StatManager] Use SetSatisfactionUpgradeThreshold() or SetSatisfactionDowngradeThreshold() to modify tier size indirectly");
    }

    /// <summary>
    /// Get current satisfaction threshold configuration
    /// </summary>
    public (int upgradeThreshold, int downgradeThreshold, int tierSize) GetSatisfactionThresholds()
    {
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        return (satisfactionUpgradeThreshold, effectiveDowngradeThreshold, satisfactionTierSize);
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
                // Loss mitigation: reduce negative impact, but never convert to positive
                float mitigationPercent = GetDerivedValue("moraleLossMod");
                float mitigationMultiplier = 1f - mitigationPercent;
                actualAmount = Mathf.RoundToInt(amount * mitigationMultiplier);
                
                // Ensure losses remain negative (can't convert to positive)
                actualAmount = Mathf.Min(actualAmount, 0);
                
                if (mitigationPercent > 0f)
                {
                    GameLoggingSystem.Instance.LogEvent($"Loss: {amount} → {mitigationPercent:P1} mitigation → {actualAmount}", "StatManager");
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
                    GameLoggingSystem.Instance.LogEvent($"Gain: {amount} → +{enhancementPercent:P1} boost → {actualAmount}", "StatManager");
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
        GameLoggingSystem.Instance.LogEvent($"Seventh tick triggered: {newSeventh} - Processing morale and satisfaction updates...", "StatManager");

        // Natural oscillation toward balance controlled by Waltz and enhanced by Euphony
        int waltzValue = GetPillarValue("waltz");
        float moraleRecoveryMod = GetDerivedValue("moraleRecoveryMod"); // Get Euphony-based recovery bonus
        

        int currentMorale = GetMorale();
        int effectiveBalance = GetMoraleBalance(); // Final balance with bonuses (for display and other systems)
        int oscillationTarget = GetBaseMoraleBalance(); // Base balance without bonuses (for oscillation target)

        // CRITICAL FIX: Oscillate towards the BASE balance, not the modified balance
        // This ensures that morale balance modifiers create a permanent shift in the equilibrium
        if (currentMorale > effectiveBalance)
        {
            // Morale above effective balance: natural decline toward oscillation target
            int down = Mathf.CeilToInt(waltzValue * Mathf.Max(0f, aboveBalanceRecoveryFactor));
            int next = currentMorale - down;
            // Prevent overshoot past oscillation target: snap to oscillation target if crossing it
            if (next < oscillationTarget) next = oscillationTarget;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
            
            GameLoggingSystem.Instance.LogEvent($"Morale decline: {currentMorale} → {next} (target: {oscillationTarget}, effective balance: {effectiveBalance})", "StatManager");
        }
        else if (currentMorale < effectiveBalance)
        {
            // Morale below effective balance: recovery toward effective balance enhanced by Euphony
            int baseRecovery = Mathf.Max(0, waltzValue);
            int enhancedRecovery = Mathf.RoundToInt(baseRecovery * moraleRecoveryMod);
            int next = currentMorale + enhancedRecovery;
            
            // Allow recovery up to the effective balance (including bonuses)
            if (next > effectiveBalance) next = effectiveBalance;
            UpdateGlobal("morale", Mathf.Clamp(next, minMorale, maxMorale));
            
            GameLoggingSystem.Instance.LogEvent($"Morale recovery: {baseRecovery} (Waltz) × {moraleRecoveryMod:F2} (Euphony) = {enhancedRecovery} → {currentMorale} → {next} (effective balance: {effectiveBalance})", "StatManager");
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

        // Update satisfaction based on morale surplus
        UpdateSatisfactionFromMorale();
        
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        GameLoggingSystem.Instance.LogEvent($"Seventh {newSeventh} completed - Morale: {GetMorale()}, Balance: {GetMoraleBalance()}, Satisfaction: {GetSatisfactionLevelName()} (Level {GetSatisfactionLevel()}) with {GetSatisfactionPoints()} points (range: {effectiveDowngradeThreshold} to {satisfactionUpgradeThreshold}, grace buffer: {satisfactionGraceBuffer})", "StatManager");
    }

    private void UpdateSatisfactionFromMorale()
    {
        int moraleSurplus = GetMorale() - GetMoraleBalance();
        
        GameLoggingSystem.Instance.LogEvent($"Checking satisfaction from morale - Current: {GetMorale()}, Balance: {GetMoraleBalance()}, Surplus: {moraleSurplus}, Threshold: {moralePointThreshold}", "StatManager");

        if (moraleSurplus > moralePointThreshold)
        {
            // Morale surplus: increase satisfaction points
            GameLoggingSystem.Instance.LogEvent($"Morale surplus: {moraleSurplus} > {moralePointThreshold} → Satisfaction Points +1 (from {satisfactionPoints})", "StatManager");
            ChangeSatisfactionPoints(1, "Morale Surplus");
        }
        else if (moraleSurplus < -moralePointThreshold)
        {
            // Morale deficit: decrease satisfaction points
            GameLoggingSystem.Instance.LogEvent($"Morale deficit: {moraleSurplus} < -{moralePointThreshold} → Satisfaction Points -1 (from {satisfactionPoints})", "StatManager");
            ChangeSatisfactionPoints(-1, "Morale Deficit");
        }
        else
        {
            GameLoggingSystem.Instance.LogEvent($"Morale within threshold range: {moraleSurplus} (between -{moralePointThreshold} and +{moralePointThreshold}) → No satisfaction change", "StatManager");
        }
    }

    /// <summary>
    /// Update satisfaction level based on current points
    /// </summary>
    private void UpdateSatisfactionLevel()
    {
        int oldLevel = satisfactionLevel;
        int newLevel = satisfactionLevel;
        
        // Determine level based on point thresholds
        // Each tier spans from effective downgrade threshold to upgrade threshold
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        if (satisfactionPoints >= satisfactionUpgradeThreshold)
        {
            // Upgrade to next level
            if (satisfactionLevel < 6) // Can't go above Utopian
            {
                newLevel = satisfactionLevel + 1;
                GameLoggingSystem.Instance.LogEvent($"UPGRADE: {satisfactionLevelNames[satisfactionLevel]} → {satisfactionLevelNames[newLevel]} (reached {satisfactionPoints} points)", "StatManager");
            }
        }
        else if (satisfactionPoints <= effectiveDowngradeThreshold)
        {
            // Downgrade to previous level (using effective threshold with grace buffer)
            if (satisfactionLevel > 0) // Can't go below Forsaken
            {
                newLevel = satisfactionLevel - 1;
                GameLoggingSystem.Instance.LogEvent($"DOWNGRADE: {satisfactionLevelNames[satisfactionLevel]} → {satisfactionLevelNames[newLevel]} (reached {satisfactionPoints} points, effective threshold: {effectiveDowngradeThreshold} = base {satisfactionDowngradeThreshold} + grace {satisfactionGraceBuffer})", "StatManager");
            }
        }

        // Update level if it changed
        if (newLevel != satisfactionLevel)
        {
            satisfactionLevel = newLevel;
            globals["satisfactionLevel"] = satisfactionLevel;
            
            // Reset points to 0 when changing levels (middle of the -75 to +100 range)
            satisfactionPoints = 0;
            globals["satisfactionPoints"] = satisfactionPoints;
            
            // Determine if this is an upgrade or downgrade
            bool isUpgrade = newLevel > oldLevel;
            
            GameLoggingSystem.Instance.LogEvent($"Satisfaction Level changed from {satisfactionLevelNames[oldLevel]} ({oldLevel}) to {satisfactionLevelNames[satisfactionLevel]} ({satisfactionLevel}) - {(isUpgrade ? "UPGRADE" : "DOWNGRADE")} - Points reset to {satisfactionPoints}", "StatManager");
            
            // Trigger level change event for systems that need to react
            OnSatisfactionLevelChanged?.Invoke(oldLevel, satisfactionLevel, isUpgrade);
        }
    }

    /// <summary>
    /// Get comprehensive grace buffer information
    /// </summary>
    public (int graceBuffer, int baseDowngradeThreshold, int effectiveDowngradeThreshold, float satisfactionEffectiveness) GetGraceBufferInfo()
    {
        float satisfactionEffectiveness = GetDerivedValue("satisfactionEffectiveness");
        int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
        
        return (satisfactionGraceBuffer, satisfactionDowngradeThreshold, effectiveDowngradeThreshold, satisfactionEffectiveness);
    }

    // ===== PERSISTENT BONUS SYSTEM FOR LEGENDS AND CIVICS =====
    
    /// <summary>
    /// Add a persistent bonus to a pillar stat
    /// </summary>
    /// <param name="pillarName">Name of the pillar</param>
    /// <param name="bonusValue">Bonus value to add</param>
    /// <param name="source">Source of the bonus (e.g., "Legend: Vittoria", "Civic: Guild Masters")</param>
    public void AddPillarBonus(string pillarName, float bonusValue, string source)
    {
        string key = pillarName.ToLower();
        if (!pillarBonuses.ContainsKey(key))
        {
            pillarBonuses[key] = new Dictionary<string, float>();
        }
        
        pillarBonuses[key][source] = bonusValue;
        
        // Recalculate the pillar with bonuses
        RecalculatePillarWithBonuses(key);
        
        GameLoggingSystem.Instance.LogEvent($"Added pillar bonus: {pillarName} +{bonusValue} from {source}", "StatManager");
    }
    
    /// <summary>
    /// Remove a persistent bonus from a pillar stat
    /// </summary>
    /// <param name="pillarName">Name of the pillar</param>
    /// <param name="source">Source of the bonus to remove</param>
    public void RemovePillarBonus(string pillarName, string source)
    {
        string key = pillarName.ToLower();
        if (pillarBonuses.ContainsKey(key) && pillarBonuses[key].ContainsKey(source))
        {
            float removedBonus = pillarBonuses[key][source];
            pillarBonuses[key].Remove(source);
            
            // Recalculate the pillar with remaining bonuses
            RecalculatePillarWithBonuses(key);
            
            GameLoggingSystem.Instance.LogEvent($"Removed pillar bonus: {pillarName} -{removedBonus} from {source}", "StatManager");
        }
    }
    
    /// <summary>
    /// Add a persistent bonus to a substat
    /// </summary>
    /// <param name="substatName">Name of the substat</param>
    /// <param name="bonusValue">Bonus value to add</param>
    /// <param name="source">Source of the bonus</param>
    public void AddSubstatBonus(string substatName, float bonusValue, string source)
    {
        string key = substatName.ToLower();
        if (!substatBonuses.ContainsKey(key))
        {
            substatBonuses[key] = new Dictionary<string, float>();
        }
        
        substatBonuses[key][source] = bonusValue;
        
        // Recalculate the substat with bonuses
        RecalculateSubstatWithBonuses(key);
        
        GameLoggingSystem.Instance.LogEvent($"Added substat bonus: {substatName} +{bonusValue} from {source}", "StatManager");
    }
    
    /// <summary>
    /// Remove a persistent bonus from a substat
    /// </summary>
    /// <param name="substatName">Name of the substat</param>
    /// <param name="source">Source of the bonus to remove</param>
    public void RemoveSubstatBonus(string substatName, string source)
    {
        string key = substatName.ToLower();
        if (substatBonuses.ContainsKey(key) && substatBonuses[key].ContainsKey(source))
        {
            float removedBonus = substatBonuses[key][source];
            substatBonuses[key].Remove(source);
            
            // Recalculate the substat with remaining bonuses
            RecalculateSubstatWithBonuses(key);
            
            GameLoggingSystem.Instance.LogEvent($"Removed substat bonus: {substatName} -{removedBonus} from {source}", "StatManager");
        }
    }
    
    /// <summary>
    /// Add a persistent bonus to a derived stat
    /// </summary>
    /// <param name="derivedStatName">Name of the derived stat</param>
    /// <param name="bonusValue">Bonus value to add</param>
    /// <param name="source">Source of the bonus</param>
    public void AddDerivedStatBonus(string derivedStatName, float bonusValue, string source)
    {
        string key = derivedStatName.ToLower();
        if (!derivedStatBonuses.ContainsKey(key))
        {
            derivedStatBonuses[key] = new Dictionary<string, float>();
        }
        
        derivedStatBonuses[key][source] = bonusValue;
        
        // Recalculate the derived stat with bonuses
        RecalculateDerivedStatWithBonuses(key);
        
        GameLoggingSystem.Instance.LogEvent($"Added derived stat bonus: {derivedStatName} +{bonusValue} from {source}", "StatManager");
    }
    
    /// <summary>
    /// Remove a persistent bonus from a derived stat
    /// </summary>
    /// <param name="derivedStatName">Name of the derived stat</param>
    /// <param name="source">Source of the bonus to remove</param>
    public void RemoveDerivedStatBonus(string derivedStatName, string source)
    {
        string key = derivedStatName.ToLower();
        if (derivedStatBonuses.ContainsKey(key) && derivedStatBonuses[key].ContainsKey(source))
        {
            float removedBonus = derivedStatBonuses[key][source];
            derivedStatBonuses[key].Remove(source);
            
            // Recalculate the derived stat with remaining bonuses
            RecalculateDerivedStatWithBonuses(key);
            
            GameLoggingSystem.Instance.LogEvent($"Removed derived stat bonus: {derivedStatName} -{removedBonus} from {source}", "StatManager");
        }
    }
    
    /// <summary>
    /// Add a persistent bonus to a global stat
    /// </summary>
    /// <param name="globalStatName">Name of the global stat</param>
    /// <param name="bonusValue">Bonus value to add</param>
    /// <param name="source">Source of the bonus</param>
    public void AddGlobalBonus(string globalStatName, int bonusValue, string source)
    {
        string key = globalStatName.ToLower();
        
        // Enhanced debugging for satisfaction and morale stats
        if (key == "maxmorale" || key == "satisfactionupgradethreshold")
        {
            GameLoggingSystem.Instance.LogEvent($"AddGlobalBonus called for '{key}' with value {bonusValue} from source '{source}'", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"Current base value: {GetBaseGlobalValue(key)}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"Current total value: {GetGlobalValue(key)}", "StatManager");
        }
        
        if (!globalBonuses.ContainsKey(key))
        {
            globalBonuses[key] = new Dictionary<string, int>();
            if (key == "maxmorale" || key == "satisfactionupgradethreshold")
            {
                GameLoggingSystem.Instance.LogEvent($"Created new bonus dictionary for '{key}'", "StatManager");
            }
        }
        
        globalBonuses[key][source] = bonusValue;
        
        // Enhanced debugging after adding bonus
        if (key == "maxmorale" || key == "satisfactionupgradethreshold")
        {
            GameLoggingSystem.Instance.LogEvent($"Added bonus {bonusValue} from '{source}' to '{key}'", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"Total bonuses for '{key}': {GetGlobalBonus(key)}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"All bonus sources for '{key}': {string.Join(", ", globalBonuses[key].Select(kvp => $"{kvp.Key}:{kvp.Value}"))}", "StatManager");
        }
        
        // Recalculate the global stat with bonuses
        RecalculateGlobalStatWithBonuses(key);
        
        // Enhanced debugging after recalculation
        if (key == "maxmorale" || key == "satisfactionupgradethreshold")
        {
            GameLoggingSystem.Instance.LogEvent($"After recalculation - base: {GetBaseGlobalValue(key)}, total: {GetGlobalValue(key)}", "StatManager");
        }
        
        GameLoggingSystem.Instance.LogEvent($"Added global stat bonus: {globalStatName} +{bonusValue} from {source}", "StatManager");
    }
    
    /// <summary>
    /// Remove a persistent bonus from a global stat
    /// </summary>
    /// <param name="globalStatName">Name of the global stat</param>
    /// <param name="source">Source of the bonus to remove</param>
    public void RemoveGlobalBonus(string globalStatName, string source)
    {
        string key = globalStatName.ToLower();
        
        // Enhanced debugging for satisfaction and morale stats
        if (key == "maxmorale" || key == "satisfactionupgradethreshold")
        {
            GameLoggingSystem.Instance.LogEvent($"RemoveGlobalBonus called for '{key}' from source '{source}'", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"Current base value: {GetBaseGlobalValue(key)}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"Current total value: {GetGlobalValue(key)}", "StatManager");
        }
        
        if (globalBonuses.ContainsKey(key) && globalBonuses[key].ContainsKey(source))
        {
            int removedBonus = globalBonuses[key][source];
            globalBonuses[key].Remove(source);
            
            // Enhanced debugging after removing bonus
            if (key == "maxmorale" || key == "satisfactionupgradethreshold")
            {
                GameLoggingSystem.Instance.LogEvent($"Removed bonus {removedBonus} from '{source}' for '{key}'", "StatManager");
                GameLoggingSystem.Instance.LogEvent($"Remaining bonuses for '{key}': {GetGlobalBonus(key)}", "StatManager");
            }
            
            // Recalculate the global stat with remaining bonuses
            RecalculateGlobalStatWithBonuses(key);
            
            // Enhanced debugging after recalculation
            if (key == "maxmorale" || key == "satisfactionupgradethreshold")
            {
                GameLoggingSystem.Instance.LogEvent($"After removal recalculation - base: {GetBaseGlobalValue(key)}, total: {GetGlobalValue(key)}", "StatManager");
            }
            
            GameLoggingSystem.Instance.LogEvent($"Removed global stat bonus: {globalStatName} -{removedBonus} from {source}", "StatManager");
        }
        else
        {
            // Enhanced debugging for failed removals
            if (key == "maxmorale" || key == "satisfactionupgradethreshold")
            {
                Debug.LogWarning($"[StatManager] Failed to remove bonus for '{key}' from '{source}' - bonus not found");
                if (globalBonuses.ContainsKey(key))
                {
                    GameLoggingSystem.Instance.LogEvent($"Existing sources for '{key}': {string.Join(", ", globalBonuses[key].Keys)}", "StatManager");
                }
                else
                {
                    GameLoggingSystem.Instance.LogEvent($"No bonus dictionary exists for '{key}'", "StatManager");
                }
            }
        }
    }
    
    /// <summary>
    /// Get the total bonus for a pillar stat from all sources
    /// </summary>
    /// <param name="pillarName">Name of the pillar</param>
    /// <returns>Total bonus value</returns>
    public float GetPillarBonus(string pillarName)
    {
        string key = pillarName.ToLower();
        if (pillarBonuses.ContainsKey(key))
        {
            return pillarBonuses[key].Values.Sum();
        }
        return 0f;
    }
    
    /// <summary>
    /// Get the total bonus for a substat from all sources
    /// </summary>
    /// <param name="substatName">Name of the substat</param>
    /// <returns>Total bonus value</returns>
    public float GetSubstatBonus(string substatName)
    {
        string key = substatName.ToLower();
        if (substatBonuses.ContainsKey(key))
        {
            return substatBonuses[key].Values.Sum();
        }
        return 0f;
    }
    
    /// <summary>
    /// Get the total bonus for a derived stat from all sources
    /// </summary>
    /// <param name="derivedStatName">Name of the derived stat</param>
    /// <returns>Total bonus value</returns>
    public float GetDerivedStatBonus(string derivedStatName)
    {
        string key = derivedStatName.ToLower();
        if (derivedStatBonuses.ContainsKey(key))
        {
            return derivedStatBonuses[key].Values.Sum();
        }
        return 0f;
    }
    
    /// <summary>
    /// Get the total bonus for a global stat from all sources
    /// </summary>
    /// <param name="globalStatName">Name of the global stat</param>
    /// <returns>Total bonus value</returns>
    public int GetGlobalBonus(string globalStatName)
    {
        string key = globalStatName.ToLower();
        if (globalBonuses.ContainsKey(key))
        {
            return globalBonuses[key].Values.Sum();
        }
        return 0;
    }
    
    /// <summary>
    /// Get all bonus sources for a specific stat
    /// </summary>
    /// <param name="statType">Type of stat ("pillar", "substat", "derived", "global")</param>
    /// <param name="statName">Name of the stat</param>
    /// <returns>Dictionary of source names and their bonus values</returns>
    public Dictionary<string, float> GetBonusSources(string statType, string statName)
    {
        string key = statName.ToLower();
        
        switch (statType.ToLower())
        {
            case "pillar":
                return pillarBonuses.ContainsKey(key) ? pillarBonuses[key].ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value) : new Dictionary<string, float>();
            case "substat":
                return substatBonuses.ContainsKey(key) ? substatBonuses[key].ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value) : new Dictionary<string, float>();
            case "derived":
                return derivedStatBonuses.ContainsKey(key) ? derivedStatBonuses[key].ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value) : new Dictionary<string, float>();
            case "global":
                return globalBonuses.ContainsKey(key) ? globalBonuses[key].ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value) : new Dictionary<string, float>();
            default:
                return new Dictionary<string, float>();
        }
    }
    
    /// <summary>
    /// Clear all bonuses from a specific source (useful when removing legends/civics)
    /// </summary>
    /// <param name="source">Source name to clear</param>
    public void ClearBonusesFromSource(string source)
    {
        bool anyCleared = false;
        
        // Clear pillar bonuses
        foreach (var pillar in pillarBonuses.Keys.ToList())
        {
            if (pillarBonuses[pillar].ContainsKey(source))
            {
                pillarBonuses[pillar].Remove(source);
                RecalculatePillarWithBonuses(pillar);
                anyCleared = true;
            }
        }
        
        // Clear substat bonuses
        foreach (var substat in substatBonuses.Keys.ToList())
        {
            if (substatBonuses[substat].ContainsKey(source))
            {
                substatBonuses[substat].Remove(source);
                RecalculateSubstatWithBonuses(substat);
                anyCleared = true;
            }
        }
        
        // Clear derived stat bonuses
        foreach (var derived in derivedStatBonuses.Keys.ToList())
        {
            if (derivedStatBonuses[derived].ContainsKey(source))
            {
                derivedStatBonuses[derived].Remove(source);
                RecalculateDerivedStatWithBonuses(derived);
                anyCleared = true;
            }
        }
        
        // Clear global stat bonuses
        foreach (var global in globalBonuses.Keys.ToList())
        {
            if (globalBonuses[global].ContainsKey(source))
            {
                int removedValue = globalBonuses[global][source];
                globalBonuses[global].Remove(source);
                RecalculateGlobalStatWithBonuses(global);
                anyCleared = true;
                

            }
        }
        
        if (anyCleared)
        {
            GameLoggingSystem.Instance.LogEvent($"Cleared all bonuses from source: {source}", "StatManager");
        }
    }
    
    // ===== PRIVATE RECALCULATION METHODS =====
    
    /// <summary>
    /// Recalculate a pillar stat with all active bonuses
    /// Clean approach: Calculate base value + bonus without affecting formulas
    /// </summary>
    /// <param name="pillarKey">Pillar key (lowercase)</param>
    private void RecalculatePillarWithBonuses(string pillarKey)
    {
        if (!pillars.ContainsKey(pillarKey)) return;
        
        // Get the original base value (without any bonuses)
        int baseValue = GetOriginalPillarValue(pillarKey);
        float totalBonus = GetPillarBonus(pillarKey);
        int finalValue = Mathf.Max(1, baseValue + Mathf.RoundToInt(totalBonus));
        
        // Update the serialized field with final value
        SetPillarField(pillarKey, finalValue);
        
        // Trigger full stat chain recalculation using base values
        RecalculateAllStatsFromBase();
        
        // Trigger events
        OnPillarChanged?.Invoke(pillarKey, finalValue);
        
        GameLoggingSystem.Instance.LogEvent($"Recalculated {pillarKey}: {baseValue} + {totalBonus:F1} bonus = {finalValue}", "StatManager");
    }
    
    /// <summary>
    /// Recalculate a substat with all active bonuses
    /// Clean approach: Calculate from base pillars + substat bonus
    /// </summary>
    /// <param name="substatKey">Substat key (lowercase)</param>
    private void RecalculateSubstatWithBonuses(string substatKey)
    {
        if (!substats.ContainsKey(substatKey)) return;
        
        // Calculate base substat value from original pillar values (no bonuses)
        int baseValue = CalculateSubstatFromFinalPillars(substatKey);
        float totalBonus = GetSubstatBonus(substatKey);
        int finalValue = Mathf.Max(1, baseValue + Mathf.RoundToInt(totalBonus));
        
        // Update the substat dictionary and serialized field
        substats[substatKey] = finalValue;
        SetSubstatField(substatKey, finalValue);
        
        // Trigger full stat chain recalculation using base values
        RecalculateAllStatsFromBase();
        
        // Trigger events
        OnSubstatChanged?.Invoke(substatKey, finalValue);
        
        GameLoggingSystem.Instance.LogEvent($"Recalculated {substatKey}: {baseValue} + {totalBonus:F1} bonus = {finalValue}", "StatManager");
    }
    
    /// <summary>
    /// Recalculate a derived stat with all active bonuses
    /// Clean approach: Calculate from FINAL substats + derived bonus
    /// </summary>
    /// <param name="derivedKey">Derived stat key (lowercase)</param>
    private void RecalculateDerivedStatWithBonuses(string derivedKey)
    {
        if (!derived.ContainsKey(derivedKey)) return;
        
        // Calculate base derived value from FINAL substats (includes substat bonuses)
        float baseValue = CalculateDerivedFromFinalSubstats(derivedKey);
        float totalBonus = GetDerivedStatBonus(derivedKey);
        float finalValue = baseValue + totalBonus;
        
        // Update the derived dictionary and serialized field
        derived[derivedKey] = finalValue;
        SetDerivedField(derivedKey, finalValue);
        
        // Trigger events
        OnDerivedChanged?.Invoke(derivedKey, finalValue);
        
        GameLoggingSystem.Instance.LogEvent($"Recalculated {derivedKey}: {baseValue:F2} + {totalBonus:F2} bonus = {finalValue:F2}", "StatManager");
        
        // Enhanced debugging for Legend Effectiveness
        if (derivedKey.ToLower() == "legendeffectiveness")
        {
            int authorityValue = substats.GetValueOrDefault("authority", 0);
            float expectedBase = 100f + (authorityValue * 0.15f);
            GameLoggingSystem.Instance.LogEvent($"DEBUG Legend Effectiveness: Authority = {authorityValue}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"DEBUG Expected Base (100 + Auth*0.15) = {expectedBase:F5}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"DEBUG Actual Base Calc = {baseValue:F5}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"DEBUG Applied Bonus = {totalBonus:F5}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"DEBUG Final Result = {finalValue:F5}", "StatManager");
            GameLoggingSystem.Instance.LogEvent($"DEBUG Final Result = {finalValue:F5}", "StatManager");
            
            // Check if there are any bonuses being applied to Legend Effectiveness
            if (derivedStatBonuses.ContainsKey(derivedKey.ToLower()))
            {
                GameLoggingSystem.Instance.LogEvent($"DEBUG Legend Effectiveness has {derivedStatBonuses[derivedKey.ToLower()].Count} bonus sources:", "StatManager");
                foreach (var bonus in derivedStatBonuses[derivedKey.ToLower()])
                {
                    GameLoggingSystem.Instance.LogEvent($"DEBUG   - {bonus.Key}: {bonus.Value:F5}", "StatManager");
                }
            }
            else
            {
                GameLoggingSystem.Instance.LogEvent($"DEBUG Legend Effectiveness has NO bonuses applied", "StatManager");
            }
        }
    }
    
    /// <summary>
    /// Get the true base value for a global stat (without any applied bonuses)
    /// </summary>
    /// <param name="globalKey">Global stat key (lowercase)</param>
    /// <returns>The original base value</returns>
    private int GetTrueBaseGlobalValue(string globalKey)
    {
        // Use the tracked original base value if available
        if (originalBaseValues.ContainsKey(globalKey))
        {
            return originalBaseValues[globalKey];
        }
        
        // Fallback to current value for stats that don't have tracked base values
        return globals.ContainsKey(globalKey) ? globals[globalKey] : 0;
    }
    
    /// <summary>
    /// Recalculate a global stat with all active bonuses
    /// </summary>
    /// <param name="globalKey">Global stat key (lowercase)</param>
    private void RecalculateGlobalStatWithBonuses(string globalKey)
    {
        if (!globals.ContainsKey(globalKey)) return;
        
        // Get the true base value (without any bonuses)
        int baseValue = GetTrueBaseGlobalValue(globalKey);
        int totalBonus = GetGlobalBonus(globalKey);
        int finalValue = baseValue + totalBonus;
        
        // Apply appropriate clamping for specific stats
        switch (globalKey)
        {
            case "morale":
                finalValue = Mathf.Clamp(finalValue, minMorale, maxMorale);
                break;
            case "satisfactionpoints":
                int effectiveDowngradeThreshold = GetEffectiveDowngradeThreshold();
                finalValue = Mathf.Clamp(finalValue, effectiveDowngradeThreshold, satisfactionUpgradeThreshold);
                break;
        }
        
        // Update the globals dictionary
        globals[globalKey] = finalValue;
        
        // Update the serialized field
        UpdateGlobal(globalKey, finalValue);
        
        GameLoggingSystem.Instance.LogEvent($"Recalculated {globalKey}: {baseValue} + {totalBonus} bonus = {finalValue}", "StatManager");
    }

}
