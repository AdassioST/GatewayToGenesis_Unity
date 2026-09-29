using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Civilization stats: 4 pillars, 8 substats, derived stats, morale and satisfaction.
///
/// Model (every value is derived, nothing is stored twice):
///   pillar    = (base + flat) × (1 + % / 100)                       base: inspector value + events
///   substat   = (round(pillar × multiplier) + event adjustments + flat) × (1 + %)
///   derived   = (StatGrowth curve(substat) + flat) × (1 + %)
///   thresholds (max morale, morale balance, satisfaction upgrade) = (base + flat) × (1 + %)
/// Flat and percent contributions live in <see cref="Modifiers"/>, keyed by stat name and source,
/// and any change there triggers one recalculation. Legends, civics, seats and weather therefore
/// never touch stat values directly; removing a source restores the exact previous value.
/// </summary>
public class StatManager : SingletonBehaviour<StatManager>
{
    private const LogChannel Log = LogChannel.Stats;

    [Header("Pillars (base values)")]
    [SerializeField] private int aureus = 10;
    [SerializeField] private int regalia = 10;
    [SerializeField] private int waltz = 10;
    [SerializeField] private int chorus = 10;

    [Header("Pillar Multipliers")]
    [SerializeField] private float aureusMultiplier = 0.5f;
    [SerializeField] private float regaliaMultiplier = 0.5f;
    [SerializeField] private float waltzMultiplier = 0.5f;
    [SerializeField] private float chorusMultiplier = 0.5f;

    [Header("Substats (read-only at runtime: derived from pillars)")]
    [SerializeField] private int innovation = 5;
    [SerializeField] private int piety = 5;
    [SerializeField] private int authority = 5;
    [SerializeField] private int ambition = 5;
    [SerializeField] private int symphony = 5;
    [SerializeField] private int euphony = 5;
    [SerializeField] private int arcane = 5;
    [SerializeField] private int secrecy = 5;

    [Header("Derived Stats (read-only at runtime)")]
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
    [SerializeField] private float communionEffectiveness;
    [SerializeField] private int communionStage;

    [Header("Stat Caps Settings")]
    [Tooltip("Cap for Discovery Efficiency as a 0-1 fraction (0.45 = 45%)")]
    [SerializeField] private float discoveryEfficiencyCap = 0.45f;
    [SerializeField] private float savingRollChanceCap = 0.25f;
    [Tooltip("Cap for Ambition's Expedition Cost reduction as a 0-1 fraction (0.25 = at most 25% cheaper)")]
    [SerializeField] private float expeditionCostReductionCap = 0.25f;
    [Tooltip("Cap for Ambition's Completion Time reduction as a 0-1 fraction (0.25 = at most 25% less time)")]
    [SerializeField] private float expeditionTimeReductionCap = 0.25f;

    [Header("Growth Tier Configuration")]
    [SerializeField] private int tier1Boundary = 42;
    [SerializeField] private int tier2Boundary = 86;
    [SerializeField] private int tier3Boundary = 150;
    [SerializeField] private int tier4Boundary = 220;
    [SerializeField] private float globalBalanceMultiplier = 1.0f;

    [Header("Morale System")]
    [SerializeField] private int morale = 100;
    [SerializeField] private int moraleBalance = 100;
    [SerializeField] private int minMorale = 0, maxMorale = 200;
    [Tooltip("Above-balance decline per seventh = ceil(Waltz × factor)")]
    [SerializeField] private float aboveBalanceRecoveryFactor = 0.5f;
    [Tooltip("Dark morale increase per seventh factor when below balance (0.2 => +1 per 5 deficit)")]
    [SerializeField] private float darkMoraleIncreaseFactor = 0.2f;
    [Tooltip("Dark morale decrease per seventh factor when above balance (0.1 => -1 per 10 surplus)")]
    [SerializeField] private float darkMoraleDecreaseFactor = 0.1f;
    [Tooltip("Share of Morale Loss Mitigation that also boosts morale gains")]
    [SerializeField] private float moraleGainBalanceFactor = 0.8f;

    [Header("Satisfaction System")]
    [SerializeField] private int satisfactionPoints = 0;
    [SerializeField] private int satisfactionLevel = 3;
    [SerializeField] private int moralePointThreshold = 25;
    [SerializeField] private int satisfactionUpgradeThreshold = 100;
    [SerializeField] private int satisfactionDowngradeThreshold = -75;
    [Tooltip("Read-only: upgrade threshold + |effective downgrade threshold|")]
    [SerializeField] private int satisfactionTierSize = 175;
    [Tooltip("Read-only: downgrade protection derived from Satisfaction Effectiveness")]
    [SerializeField] private int satisfactionGraceBuffer = 0;

    private static readonly string[] SatisfactionLevelNames =
    {
        "Forsaken", "Decadent", "Discontent", "Content", "Harmonious", "Resplendent", "Utopian"
    };

    private static readonly string[] SatisfactionLevelDescriptions =
    {
        "Dystopian misery, no trust in life",
        "People endure, but with little hope",
        "Cracks in faith, whispers of rebellion",
        "People live tolerably, functional but uninspired",
        "A society where justice and dignity are reliable",
        "People believe in fairness and promise of their world",
        "Near mythical perfect state of joy, fairness, and aligned purpose"
    };

    /// <summary>Source-tracked flat/percent bonuses, keyed by <see cref="StatDefinitions.Key"/>.</summary>
    public ModifierLedger Modifiers { get; } = new ModifierLedger();

    // Base values that runtime systems may change (events, upgrades).
    private readonly Dictionary<string, int> _basePillars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _substatAdjustments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _baseGlobals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Results of the last recalculation.
    private readonly Dictionary<string, int> _pillars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _substatBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _substats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _derivedBase = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _derived = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, float> _pillarMultipliers;

    // Morale shifts tracked by source.
    private readonly Dictionary<string, int> _moraleTimedRemainingBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _moraleTimedValueBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _moralePersistentBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;
    private bool _recalculating;

    public event Action<string, int> OnPillarChanged;
    public event Action<string, int> OnSubstatChanged;
    public event Action<string, float> OnDerivedChanged;
    public event Action<int> OnMoraleChanged;
    public event Action<int, int> OnSatisfactionChanged;                 // (level, points)
    public event Action<int, int, bool> OnSatisfactionLevelChanged;      // (oldLevel, newLevel, isUpgrade)
    /// <summary>Raised once after any recalculation that changed at least one value.</summary>
    public event Action OnStatsChanged;

    // ===== LIFECYCLE =====

    protected override void OnSingletonAwake()
    {
        _pillarMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "aureus", aureusMultiplier }, { "regalia", regaliaMultiplier }, { "waltz", waltzMultiplier }, { "chorus", chorusMultiplier }
        };
        _basePillars["aureus"] = aureus;
        _basePillars["regalia"] = regalia;
        _basePillars["waltz"] = waltz;
        _basePillars["chorus"] = chorus;
        foreach (var substat in StatDefinitions.Substats) _substatAdjustments[substat] = 0;

        _baseGlobals[StatDefinitions.MaxMorale] = maxMorale;
        _baseGlobals[StatDefinitions.MoraleBalance] = moraleBalance;
        _baseGlobals[StatDefinitions.SatisfactionUpgradeThreshold] = satisfactionUpgradeThreshold;

        Modifiers.Changed += _ => Recalculate();
        _initialized = true;
        Recalculate(forceEvents: true);
    }

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time => time.OnSeventhChange += OnSeventhTick);
    }

    protected override void OnSingletonDestroy()
    {
        if (TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventhTick;
    }

    // ===== RECALCULATION =====

    /// <summary>Recompute every stat from bases and modifiers. Cheap; called automatically on any change.</summary>
    public void Recalculate() => Recalculate(forceEvents: false);

    private void Recalculate(bool forceEvents)
    {
        if (!_initialized || _recalculating) return;
        _recalculating = true;
        bool anyChange = forceEvents;
        try
        {
            var bounds = new StatGrowth.Boundaries(tier1Boundary, tier2Boundary, tier3Boundary, tier4Boundary);

            foreach (var pillar in StatDefinitions.Pillars)
            {
                int value = StatRules.Pillar(_basePillars[pillar], Modifiers.Total(pillar));
                anyChange |= Store(_pillars, pillar, value, forceEvents, OnPillarChanged);
                SetPillarField(pillar, _basePillars[pillar]);
            }

            foreach (var substat in StatDefinitions.Substats)
            {
                string parent = StatDefinitions.ParentPillar(substat);
                int baseValue = StatRules.SubstatBase(_pillars[parent], _pillarMultipliers[parent], _substatAdjustments[substat]);
                _substatBase[substat] = baseValue;
                int value = StatRules.Substat(baseValue, Modifiers.Total(substat));
                anyChange |= Store(_substats, substat, value, forceEvents, OnSubstatChanged);
                SetSubstatField(substat, value);
            }

            foreach (var pair in StatDefinitions.DerivedSource)
            {
                float baseValue = StatGrowth.Evaluate(pair.Key, _substats[pair.Value], bounds, globalBalanceMultiplier);
                _derivedBase[pair.Key] = baseValue;
                float value = Modifiers.Total(StatDefinitions.Key(pair.Key)).ApplyTo(baseValue);
                anyChange |= StoreDerived(pair.Key, value, forceEvents);
                SetDerivedField(pair.Key, value);
            }
            communionStage = StatRules.CommunionStage(_substats["secrecy"]);
            _derivedBase[StatDefinitions.CommunionStage] = communionStage;
            anyChange |= StoreDerived(StatDefinitions.CommunionStage, communionStage, forceEvents);

            foreach (var pair in _baseGlobals)
            {
                int value = StatRules.Threshold(pair.Value, Modifiers.Total(pair.Key), atLeastOne: pair.Key == StatDefinitions.SatisfactionUpgradeThreshold);
                if (!_globals.TryGetValue(pair.Key, out int previous) || previous != value) anyChange = true;
                _globals[pair.Key] = value;
            }

            RecalculateSatisfactionBounds();
            int previousMorale = morale;
            morale = Mathf.Clamp(morale, minMorale, GetMaxMorale());
            if (forceEvents || previousMorale != morale) OnMoraleChanged?.Invoke(GetMorale());
            if (forceEvents) OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
        }
        finally
        {
            _recalculating = false;
        }
        if (anyChange) OnStatsChanged?.Invoke();
    }

    private static bool Store(Dictionary<string, int> table, string key, int value, bool forceEvent, Action<string, int> changed)
    {
        bool isChange = !table.TryGetValue(key, out int previous) || previous != value;
        table[key] = value;
        if (isChange || forceEvent) changed?.Invoke(key, value);
        return isChange;
    }

    private bool StoreDerived(string key, float value, bool forceEvent)
    {
        bool isChange = !_derived.TryGetValue(key, out float previous) || Mathf.Abs(previous - value) > 0.0001f;
        _derived[key] = value;
        if (isChange || forceEvent) OnDerivedChanged?.Invoke(key, value);
        return isChange;
    }

    private void RecalculateSatisfactionBounds()
    {
        // Grace buffer: half of Satisfaction Effectiveness rounded down to a multiple of 5, as protection against downgrades.
        int rounded = Mathf.RoundToInt(GetDerivedValue("satisfactionEffectiveness"));
        satisfactionGraceBuffer = -((rounded / 5) * 5 / 2);
        satisfactionTierSize = GetUpgradeThreshold() + Mathf.Abs(GetEffectiveDowngradeThreshold());
    }

    // ===== QUERIES =====

    public int GetPillarValue(string pillarName) => pillarName != null && _pillars.TryGetValue(pillarName, out int v) ? v : 0;

    public int GetSubstatValue(string substatName) => substatName != null && _substats.TryGetValue(substatName, out int v) ? v : 0;

    public float GetDerivedValue(string derivedName) => derivedName != null && _derived.TryGetValue(derivedName, out float v) ? v : 0f;

    public int GetBasePillarValue(string pillarName) => pillarName != null && _basePillars.TryGetValue(pillarName, out int v) ? v : 0;

    public int GetBaseSubstatValue(string substatName) => substatName != null && _substatBase.TryGetValue(substatName, out int v) ? v : 0;

    public float GetBaseDerivedValue(string derivedName) => derivedName != null && _derivedBase.TryGetValue(derivedName, out float v) ? v : 0f;

    /// <summary>Final value of a global (thresholds with modifiers, current morale and satisfaction state).</summary>
    public int GetGlobalValue(string globalName)
    {
        string key = StatDefinitions.Key(globalName);
        switch (key)
        {
            case StatDefinitions.Morale: return GetMorale();
            case StatDefinitions.SatisfactionPoints: return satisfactionPoints;
            case StatDefinitions.SatisfactionLevel: return satisfactionLevel;
        }
        return key != null && _globals.TryGetValue(key, out int v) ? v : 0;
    }

    /// <summary>Base (unmodified) value of a global.</summary>
    public int GetBaseGlobalValue(string globalName)
    {
        string key = StatDefinitions.Key(globalName);
        switch (key)
        {
            case StatDefinitions.Morale: return morale;
            case StatDefinitions.SatisfactionPoints: return satisfactionPoints;
            case StatDefinitions.SatisfactionLevel: return satisfactionLevel;
        }
        return key != null && _baseGlobals.TryGetValue(key, out int v) ? v : 0;
    }

    /// <summary>Final value of any stat by name: pillar, substat, derived (rounded) or global.</summary>
    public int GetStatValue(string statName)
    {
        switch (StatDefinitions.KindOf(statName))
        {
            case StatDefinitions.StatKind.Pillar: return GetPillarValue(statName);
            case StatDefinitions.StatKind.Substat: return GetSubstatValue(statName);
            case StatDefinitions.StatKind.Derived: return Mathf.RoundToInt(GetDerivedValue(statName));
            case StatDefinitions.StatKind.Global: return GetGlobalValue(statName);
            default: return 0;
        }
    }

    /// <summary>True when the final value of <paramref name="statName"/> is at least <paramref name="requiredValue"/>.</summary>
    public bool CheckStat(string statName, int requiredValue) => GetStatValue(statName) >= requiredValue;

    public List<string> GetAllStatNames()
    {
        var names = new List<string>(StatDefinitions.Pillars);
        names.AddRange(StatDefinitions.Substats);
        names.AddRange(StatDefinitions.DerivedSource.Keys);
        names.AddRange(StatDefinitions.Globals);
        return names;
    }

    /// <summary>Per-source contributions to one stat (flat and percent) for tooltips.</summary>
    public IReadOnlyDictionary<string, ModifierValue> GetBonusSources(string statName) => Modifiers.Sources(StatDefinitions.Key(statName));

    // Discovery Efficiency and Saving Roll are percentages with a design cap.
    public float GetDiscoveryEfficiencyCapped() => Mathf.Min(Mathf.Max(0f, GetDerivedValue("discoveryEfficiency") / 100f), Mathf.Clamp01(discoveryEfficiencyCap));

    public float GetDiscoveryEfficiencyPercentCapped() => GetDiscoveryEfficiencyCapped() * 100f;

    public float GetSavingRollChanceCapped() => Mathf.Min(Mathf.Max(0f, GetDerivedValue("savingRollChance") / 100f), Mathf.Clamp01(savingRollChanceCap));

    public float GetSavingRollChancePercentCapped() => GetSavingRollChanceCapped() * 100f;

    public float GetSatisfactionEffectivenessMultiplier() => 1f + GetDerivedValue("satisfactionEffectiveness") / 100f;

    // Ambition, capped like the Saving Roll: the share an expedition's cost (outfit, attrition, rations) and the time it
    // takes to advance are reduced, 0 up to the cap, and the multipliers built from it (1 - reduction).
    public float GetExpeditionCostReductionCapped() => StatRules.CappedReduction(GetDerivedValue("expeditionCostMod"), expeditionCostReductionCap);

    public float GetExpeditionTimeReductionCapped() => StatRules.CappedReduction(GetDerivedValue("expeditionTimeMod"), expeditionTimeReductionCap);

    public float GetExpeditionCostMultiplier() => 1f - GetExpeditionCostReductionCapped();

    public float GetExpeditionTimeMultiplier() => 1f - GetExpeditionTimeReductionCapped();

    // Arcane and Secrecy: open multipliers for the Magic Arts and Communion (read through CivilizationProperties).
    public float GetMagicEffectivenessMultiplier() => StatRules.EffectivenessMultiplier(GetDerivedValue("magicEffectiveness"));

    public float GetCommunionEffectivenessMultiplier() => StatRules.EffectivenessMultiplier(GetDerivedValue("communionEffectiveness"));

    public int GetCommunionStage() => Mathf.RoundToInt(GetDerivedValue(StatDefinitions.CommunionStage));

    // ===== MUTATION =====

    /// <summary>
    /// Change a stat's base by <paramref name="delta"/> (events, Ink, rewards).
    /// Pillars and thresholds change their base, substats get a permanent adjustment on top of their pillar,
    /// morale goes through <see cref="ApplyMoraleShift"/> and satisfaction through its own rules.
    /// Derived stats cannot be changed directly.
    /// </summary>
    public void ModifyStat(string statName, int delta)
    {
        if (delta == 0 || string.IsNullOrEmpty(statName)) return;
        string key = StatDefinitions.Key(statName);
        switch (StatDefinitions.KindOf(statName))
        {
            case StatDefinitions.StatKind.Pillar:
                _basePillars[key] = Mathf.Max(1, _basePillars[key] + delta);
                Recalculate();
                break;
            case StatDefinitions.StatKind.Substat:
                _substatAdjustments[key] += delta;
                Recalculate();
                break;
            case StatDefinitions.StatKind.Global:
                if (key == StatDefinitions.Morale) ApplyMoraleShift(delta);
                else if (key == StatDefinitions.SatisfactionPoints) ModifySatisfactionPoints(delta, "ModifyStat");
                else if (key == StatDefinitions.SatisfactionLevel) SetSatisfactionLevel(satisfactionLevel + delta, "ModifyStat");
                else
                {
                    _baseGlobals[key] += delta;
                    SyncGlobalField(key);
                    Recalculate();
                }
                break;
            case StatDefinitions.StatKind.Derived:
                GameLog.Warning($"Derived stat '{statName}' cannot be changed directly; change its substat or add a modifier.", Log);
                break;
            default:
                GameLog.Warning($"Unknown stat '{statName}'.", Log);
                break;
        }
    }

    private void SyncGlobalField(string key)
    {
        if (key == StatDefinitions.MaxMorale) maxMorale = _baseGlobals[key];
        else if (key == StatDefinitions.MoraleBalance) moraleBalance = _baseGlobals[key];
        else if (key == StatDefinitions.SatisfactionUpgradeThreshold) satisfactionUpgradeThreshold = _baseGlobals[key];
    }

    // ===== MORALE =====

    /// <summary>Current morale including morale modifiers, clamped to [min, max morale].</summary>
    public int GetMorale() => Mathf.Clamp(Mathf.RoundToInt(Modifiers.Total(StatDefinitions.Morale).ApplyTo(morale)), minMorale, GetMaxMorale());

    public int GetMaxMorale() => Mathf.Max(minMorale, GetGlobalValue(StatDefinitions.MaxMorale));

    /// <summary>Balance used for production and satisfaction deltas (base balance with modifiers).</summary>
    public int GetMoraleBalance() => GetGlobalValue(StatDefinitions.MoraleBalance);

    /// <summary>Natural resting point morale drifts towards each seventh.</summary>
    public int GetBaseMoraleBalance() => GetBaseGlobalValue(StatDefinitions.MoraleBalance);

    /// <summary>Morale minus balance: positive is a production bonus, negative a malus.</summary>
    public float GetMoraleDeltaPercent() => GetMorale() - GetMoraleBalance();

    /// <summary>
    /// Shift current morale. Losses are reduced by Morale Loss Mitigation, gains boosted by part of it.
    /// With a source, temporary shifts revert after <paramref name="durationSevenths"/>, and persistent
    /// shifts replace that source's previous shift instead of stacking.
    /// </summary>
    public void ApplyMoraleShift(int amount, string source = null, bool temporary = false, int durationSevenths = 0)
    {
        if (!string.IsNullOrEmpty(source) && !temporary)
        {
            _moralePersistentBySource.TryGetValue(source, out int previous);
            int enhanced = EnhanceMoraleDelta(amount);
            _moralePersistentBySource[source] = enhanced;
            SetMorale(morale + (enhanced - previous));
            return;
        }

        int actual = EnhanceMoraleDelta(amount);
        SetMorale(morale + actual);
        if (temporary && durationSevenths > 0 && !string.IsNullOrEmpty(source))
        {
            _moraleTimedValueBySource.TryGetValue(source, out int existing);
            _moraleTimedValueBySource[source] = existing + actual;
            _moraleTimedRemainingBySource[source] = durationSevenths;
        }
    }

    private int EnhanceMoraleDelta(int amount)
    {
        return StatRules.MoraleShift(amount, GetDerivedValue("moraleLossMod"), moraleGainBalanceFactor);
    }

    private void SetMorale(int value)
    {
        int clamped = Mathf.Clamp(value, minMorale, GetMaxMorale());
        if (clamped == morale) return;
        morale = clamped;
        OnMoraleChanged?.Invoke(GetMorale());
        OnStatsChanged?.Invoke();
    }

    private void OnSeventhTick(int newSeventh)
    {
        int waltzValue = GetPillarValue("waltz");
        int target = GetBaseMoraleBalance();

        // Morale drifts to its natural resting point: slowly from above, faster (Euphony-enhanced) from below.
        // Balance modifiers move the reference used for bonuses, not this resting point, which is what makes them permanent.
        SetMorale(StatRules.MoraleDrift(morale, target, waltzValue, aboveBalanceRecoveryFactor, GetDerivedValue("moraleRecoveryMod")));

        foreach (var source in _moraleTimedRemainingBySource.Keys.ToList())
        {
            int remaining = _moraleTimedRemainingBySource[source] - 1;
            if (remaining > 0)
            {
                _moraleTimedRemainingBySource[source] = remaining;
                continue;
            }
            if (_moraleTimedValueBySource.TryGetValue(source, out int shift)) SetMorale(morale - shift);
            _moraleTimedRemainingBySource.Remove(source);
            _moraleTimedValueBySource.Remove(source);
        }

        UpdateDarkMorale();
        UpdateSatisfactionFromMorale();
        GameLog.Event($"Seventh {newSeventh}: morale {GetMorale()} / balance {GetMoraleBalance()}, satisfaction {GetSatisfactionLevelName()} ({satisfactionPoints} pts)", Log);
    }

    private void UpdateDarkMorale()
    {
        var events = EventSystemLogic.Instance;
        if (events == null) return;
        int change = StatRules.DarkMoraleChange(GetMoraleBalance(), GetMorale(), darkMoraleIncreaseFactor, darkMoraleDecreaseFactor, events.GetEventScore("dark_morale"));
        if (change != 0) events.ModifyEventScore("dark_morale", change);
    }

    // ===== SATISFACTION =====

    public int GetSatisfactionPoints() => satisfactionPoints;

    public int GetSatisfactionLevel() => satisfactionLevel;

    public string GetSatisfactionLevelName() => satisfactionLevel >= 0 && satisfactionLevel < SatisfactionLevelNames.Length ? SatisfactionLevelNames[satisfactionLevel] : "Unknown";

    public string GetSatisfactionLevelDescription() => satisfactionLevel >= 0 && satisfactionLevel < SatisfactionLevelDescriptions.Length ? SatisfactionLevelDescriptions[satisfactionLevel] : "Unknown satisfaction level";

    public int GetSatisfactionGraceBuffer() => satisfactionGraceBuffer;

    private int GetUpgradeThreshold() => GetGlobalValue(StatDefinitions.SatisfactionUpgradeThreshold);

    /// <summary>Downgrade threshold including the grace buffer (negative number).</summary>
    public int GetEffectiveDowngradeThreshold() => satisfactionDowngradeThreshold + satisfactionGraceBuffer;

    public (int level, string name, string description, int minPoints, int maxPoints) GetSatisfactionTierInfo()
    {
        return (satisfactionLevel, GetSatisfactionLevelName(), GetSatisfactionLevelDescription(), GetEffectiveDowngradeThreshold(), GetUpgradeThreshold());
    }

    public (float progress, int pointsInTier, int pointsForNext, int pointsForPrevious) GetSatisfactionProgress()
    {
        int floor = GetEffectiveDowngradeThreshold();
        int pointsInTier = satisfactionPoints - floor;
        float progress = satisfactionTierSize > 0 ? (float)pointsInTier / satisfactionTierSize : 0f;
        return (progress, pointsInTier, GetUpgradeThreshold() - satisfactionPoints, satisfactionPoints - floor);
    }

    public (int upgradeThreshold, int downgradeThreshold, int tierSize) GetSatisfactionThresholds()
    {
        return (GetUpgradeThreshold(), GetEffectiveDowngradeThreshold(), satisfactionTierSize);
    }

    public (int graceBuffer, int baseDowngradeThreshold, int effectiveDowngradeThreshold, float satisfactionEffectiveness) GetGraceBufferInfo()
    {
        return (satisfactionGraceBuffer, satisfactionDowngradeThreshold, GetEffectiveDowngradeThreshold(), GetDerivedValue("satisfactionEffectiveness"));
    }

    /// <summary>Gameplay satisfaction change: gains are boosted by Satisfaction Effectiveness, losses are not.</summary>
    public void ChangeSatisfactionPoints(int change, string source = null)
    {
        if (change == 0) return;
        int finalChange = StatRules.SatisfactionGain(change, GetSatisfactionEffectivenessMultiplier());
        ApplySatisfactionPoints(finalChange, source);
    }

    /// <summary>Raw satisfaction change (penalties, scripted outcomes) with no effectiveness bonus.</summary>
    public void ModifySatisfactionPoints(int change, string source = null)
    {
        if (change == 0) return;
        ApplySatisfactionPoints(change, source);
    }

    private void ApplySatisfactionPoints(int change, string source)
    {
        int oldPoints = satisfactionPoints;
        satisfactionPoints = Mathf.Clamp(satisfactionPoints + change, GetEffectiveDowngradeThreshold(), GetUpgradeThreshold());
        UpdateSatisfactionLevel();
        GameLog.Event($"Satisfaction {oldPoints} → {satisfactionPoints} ({change:+#;-#;0}) from '{source}'", Log);
        OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
    }

    /// <summary>Set the satisfaction level directly; points reset to the middle of the tier.</summary>
    public void SetSatisfactionLevel(int newLevel, string source = null)
    {
        if (newLevel < 0 || newLevel >= SatisfactionLevelNames.Length)
        {
            GameLog.Warning($"Invalid satisfaction level {newLevel}; must be 0-{SatisfactionLevelNames.Length - 1}.", Log);
            return;
        }
        int oldLevel = satisfactionLevel;
        satisfactionLevel = newLevel;
        satisfactionPoints = 0;
        GameLog.Event($"Satisfaction level set to {GetSatisfactionLevelName()} from '{source}'", Log);
        if (oldLevel != newLevel) OnSatisfactionLevelChanged?.Invoke(oldLevel, newLevel, newLevel > oldLevel);
        OnSatisfactionChanged?.Invoke(satisfactionLevel, satisfactionPoints);
    }

    private void UpdateSatisfactionFromMorale()
    {
        int surplus = GetMorale() - GetMoraleBalance();
        if (surplus > moralePointThreshold) ChangeSatisfactionPoints(1, "Morale Surplus");
        else if (surplus < -moralePointThreshold) ChangeSatisfactionPoints(-1, "Morale Deficit");
    }

    private void UpdateSatisfactionLevel()
    {
        int oldLevel = satisfactionLevel;
        if (satisfactionPoints >= GetUpgradeThreshold() && satisfactionLevel < SatisfactionLevelNames.Length - 1) satisfactionLevel++;
        else if (satisfactionPoints <= GetEffectiveDowngradeThreshold() && satisfactionLevel > 0) satisfactionLevel--;
        if (satisfactionLevel == oldLevel) return;

        satisfactionPoints = 0;
        bool isUpgrade = satisfactionLevel > oldLevel;
        GameLog.Event($"Satisfaction {(isUpgrade ? "UPGRADE" : "DOWNGRADE")}: {SatisfactionLevelNames[oldLevel]} → {GetSatisfactionLevelName()}", Log);
        OnSatisfactionLevelChanged?.Invoke(oldLevel, satisfactionLevel, isUpgrade);
    }

    // ===== INSPECTOR MIRRORS =====

    private void SetPillarField(string pillar, int value)
    {
        switch (pillar)
        {
            case "aureus": aureus = value; break;
            case "regalia": regalia = value; break;
            case "waltz": waltz = value; break;
            case "chorus": chorus = value; break;
        }
    }

    private void SetSubstatField(string substat, int value)
    {
        switch (substat)
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

    private void SetDerivedField(string derived, float value)
    {
        switch (StatDefinitions.Key(derived))
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
            case "communioneffectiveness": communionEffectiveness = value; break;
        }
    }

    // ===== DEBUG =====

    [ContextMenu("Print All Stats")]
    public void PrintAllStats()
    {
        if (!GameLog.IsEnabled(Log)) return;
        GameLog.Event("=== CIVILIZATION STATS ===", Log);
        foreach (var pillar in StatDefinitions.Pillars)
        {
            var children = StatDefinitions.PillarSubstats[pillar];
            GameLog.Event($"{pillar}: base {GetBasePillarValue(pillar)} → {GetPillarValue(pillar)} | {children[0]} {GetSubstatValue(children[0])}, {children[1]} {GetSubstatValue(children[1])}", Log);
        }
        foreach (var derived in StatDefinitions.DerivedSource.Keys)
        {
            GameLog.Event($"{derived}: base {GetBaseDerivedValue(derived):F3} → {GetDerivedValue(derived):F3}", Log);
        }
        GameLog.Event($"Morale {GetMorale()} (balance {GetMoraleBalance()}, rest {GetBaseMoraleBalance()}, max {GetMaxMorale()})", Log);
        GameLog.Event($"Satisfaction {GetSatisfactionLevelName()} ({satisfactionPoints} pts, range {GetEffectiveDowngradeThreshold()}..{GetUpgradeThreshold()})", Log);
        foreach (var target in Modifiers.Targets)
        {
            foreach (var source in Modifiers.Sources(target))
            {
                GameLog.Event($"  {target}: {source.Value.Flat:+0.##;-0.##;0} / {source.Value.Percent:+0.##;-0.##;0}% from {source.Key}", Log);
            }
        }
    }
}
