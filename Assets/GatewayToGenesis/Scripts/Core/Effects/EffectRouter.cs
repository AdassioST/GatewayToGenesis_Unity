using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Applies <see cref="GameEffect"/>s to the game and removes them again, for every source kind.
///
/// Each effect type has exactly one handler; each handler writes into a source-tracked
/// <see cref="ModifierLedger"/> owned by the system that consumes the value. Consequences:
///   • removal is always <see cref="RemoveSource"/>, identical for legends, civics, seats, weather and events;
///   • <see cref="ApplySet"/> replaces a source's whole contribution, so re-applying never stacks;
///   • a target that does not exist yet (a resource discovered later) is stored by name or scope and
///     picked up automatically when it appears.
/// Adding an effect type: append it to <see cref="GameEffectType"/>, register a handler below, and
/// the startup check plus the EditMode test fail until you do.
///
/// Sign conventions (positive values are what the effect name promises):
///   MoraleBalanceModifier +X  lowers the morale balance by X (easier to stay above it)
///   SatisfactionThresholdModifier +X lowers the upgrade threshold by X (easier upgrades)
///   ConstructionCostModifier +X raises costs by X%; use negative values for discounts
/// </summary>
public static class EffectRouter
{
    private const LogChannel Log = LogChannel.Effects;

    private delegate bool Handler(in GameEffect effect, string source, float multiplier);

    private static readonly Dictionary<GameEffectType, Handler> Handlers = new Dictionary<GameEffectType, Handler>
    {
        { GameEffectType.PillarBonus, ApplyStat },
        { GameEffectType.SubstatBonus, ApplyStat },
        { GameEffectType.DerivedStatBonus, ApplyStat },
        { GameEffectType.ResourceModifier, ApplyResource },
        { GameEffectType.ProductionModifier, ApplyProductionEfficiency },
        { GameEffectType.ClickPowerBonus, ApplyClickPower },
        { GameEffectType.ConstructionCostModifier, ApplyConstructionCost },
        { GameEffectType.ProductionScalingBonus, ApplyScaling },
        { GameEffectType.MaxMoraleModifier, (in GameEffect e, string s, float m) => ApplyGlobal(StatDefinitions.MaxMorale, e, s, m, 1f) },
        { GameEffectType.MoraleBalanceModifier, (in GameEffect e, string s, float m) => ApplyGlobal(StatDefinitions.MoraleBalance, e, s, m, -1f) },
        { GameEffectType.SatisfactionThresholdModifier, (in GameEffect e, string s, float m) => ApplyGlobal(StatDefinitions.SatisfactionUpgradeThreshold, e, s, m, -1f) },
        { GameEffectType.MoraleModifier, (in GameEffect e, string s, float m) => ApplyGlobal(StatDefinitions.Morale, e, s, m, 1f) },
        { GameEffectType.HousingBonus, ApplyHousing },
        { GameEffectType.SpecialAbility, ApplySpecial },
    };

    /// <summary>Effect types with no handler. Must be empty; checked at startup and by tests.</summary>
    public static IEnumerable<GameEffectType> MissingHandlers() =>
        Enum.GetValues(typeof(GameEffectType)).Cast<GameEffectType>().Where(t => !Handlers.ContainsKey(t));

    // ===== APPLY / REMOVE =====

    /// <summary>
    /// Add one effect under <paramref name="source"/>. Multiple effects from one source accumulate.
    /// Prefer <see cref="ApplySet"/> unless you manage removal yourself.
    /// </summary>
    /// <param name="multiplier">Scales the value, e.g. Head of State × Legend Effectiveness.</param>
    public static bool Apply(in GameEffect effect, string source, float multiplier = 1f)
    {
        if (string.IsNullOrEmpty(source))
        {
            GameLog.Error($"Effect {effect} has no source and cannot be tracked; ignored.", Log);
            return false;
        }
        var (isValid, error) = effect.ValidateShape();
        if (!isValid)
        {
            GameLog.Warning($"{source}: {error}", Log);
            return false;
        }
        if (!Handlers.TryGetValue(effect.type, out var handler))
        {
            GameLog.Error($"No handler registered for {effect.type} ({source}).", Log);
            return false;
        }
        bool applied = handler(effect, source, multiplier);
        if (applied) GameLog.Event($"{source}: {effect.Describe()}{(Math.Abs(multiplier - 1f) > 0.0001f ? $" ×{multiplier:0.###}" : "")}", Log);
        return applied;
    }

    /// <summary>Replace everything <paramref name="source"/> contributes with exactly these effects.</summary>
    public static void ApplySet(string source, IEnumerable<GameEffect> effects, float multiplier = 1f)
    {
        RemoveSource(source);
        if (effects == null) return;
        foreach (var effect in effects) Apply(effect, source, multiplier);
    }

    /// <summary>Remove every contribution of <paramref name="source"/> from every system.</summary>
    public static void RemoveSource(string source)
    {
        if (string.IsNullOrEmpty(source)) return;
        int removed = 0;
        foreach (var ledger in Ledgers()) removed += ledger.RemoveSource(source);
        if (removed > 0) GameLog.Event($"Removed {removed} modifier target(s) from '{source}'", Log);
    }

    /// <summary>True when <paramref name="source"/> currently contributes anything anywhere.</summary>
    public static bool HasSource(string source) => Ledgers().Any(l => l.HasSource(source));

    /// <summary>Every ledger an effect can write to, skipping systems absent from the scene.</summary>
    public static IEnumerable<ModifierLedger> Ledgers()
    {
        if (StatManager.Instance != null) yield return StatManager.Instance.Modifiers;
        if (GlobalProductionManager.Instance != null) yield return GlobalProductionManager.Instance.ResourceModifiers;
        var units = GameUnitsLogic.Instance;
        if (units != null)
        {
            yield return units.ProductionEfficiency;
            yield return units.ConstructionCost;
            yield return units.ClickPower;
            yield return units.ProductionScaling;
        }
        if (PopGrowthLogic.Instance != null) yield return PopGrowthLogic.Instance.HousingModifiers;
    }

    // ===== HANDLERS =====

    private static ModifierValue ToModifier(in GameEffect effect, float multiplier, float sign = 1f)
    {
        float amount = effect.value * multiplier * sign;
        return effect.modifierType == ModifierType.Percentage ? new ModifierValue(0f, amount) : new ModifierValue(amount, 0f);
    }

    private static bool Missing(string system, in GameEffect effect, string source)
    {
        GameLog.Warning($"{source}: {effect.type} needs {system}, which is not in the scene.", Log);
        return false;
    }

    private static bool ApplyStat(in GameEffect effect, string source, float multiplier)
    {
        var stats = StatManager.Instance;
        if (stats == null) return Missing(nameof(StatManager), effect, source);
        if (!StatDefinitions.IsKnown(effect.target))
        {
            GameLog.Warning($"{source}: '{effect.target}' is not a stat.", Log);
            return false;
        }
        string key = StatDefinitions.Key(effect.target);
        ModifierValue value;
        if (effect.modifierType == ModifierType.SetValue)
        {
            float current = StatDefinitions.KindOf(key) == StatDefinitions.StatKind.Derived ? stats.GetBaseDerivedValue(effect.target) : stats.GetStatValue(effect.target);
            value = new ModifierValue(effect.value * multiplier - current, 0f);
        }
        else
        {
            value = ToModifier(effect, multiplier);
        }
        stats.Modifiers.Add(key, source, value);
        return true;
    }

    private static bool ApplyGlobal(string key, in GameEffect effect, string source, float multiplier, float sign)
    {
        var stats = StatManager.Instance;
        if (stats == null) return Missing(nameof(StatManager), effect, source);
        stats.Modifiers.Add(key, source, ToModifier(effect, multiplier, sign));
        return true;
    }

    private static bool ApplyResource(in GameEffect effect, string source, float multiplier)
    {
        var production = GlobalProductionManager.Instance;
        if (production == null) return Missing(nameof(GlobalProductionManager), effect, source);
        production.ResourceModifiers.Add(ResolveResourceTarget(effect.target, effect.scope), source, ToModifier(effect, multiplier));
        return true;
    }

    private static bool ApplyClickPower(in GameEffect effect, string source, float multiplier)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return Missing(nameof(GameUnitsLogic), effect, source);
        units.ClickPower.Add(ResolveResourceTarget(effect.target, effect.scope), source, ToModifier(effect, multiplier));
        return true;
    }

    private static bool ApplyProductionEfficiency(in GameEffect effect, string source, float multiplier)
    {
        // Efficiency is always a percentage of a unit's output.
        float percent = effect.value * multiplier;
        string target = ResolveUnitTarget(effect.target, effect.scope, out bool isResource);
        if (isResource)
        {
            // Authored against a resource ("Food"): boost that resource's output from every producer.
            var production = GlobalProductionManager.Instance;
            if (production == null) return Missing(nameof(GlobalProductionManager), effect, source);
            production.ResourceModifiers.Add(target, source, new ModifierValue(0f, percent));
            return true;
        }
        var units = GameUnitsLogic.Instance;
        if (units == null) return Missing(nameof(GameUnitsLogic), effect, source);
        units.ProductionEfficiency.Add(target, source, new ModifierValue(0f, percent));
        return true;
    }

    private static bool ApplyConstructionCost(in GameEffect effect, string source, float multiplier)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return Missing(nameof(GameUnitsLogic), effect, source);
        string target = ResolveUnitTarget(effect.target, effect.scope, out _);
        units.ConstructionCost.Add(target, source, new ModifierValue(0f, effect.value * multiplier));
        return true;
    }

    private static bool ApplyScaling(in GameEffect effect, string source, float multiplier)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null) return Missing(nameof(GameUnitsLogic), effect, source);
        string counter = string.IsNullOrEmpty(effect.condition) ? ProductionScalingKey.Producers : ResolveCounter(effect.condition);
        units.ProductionScaling.Add(ProductionScalingKey.Make(effect.target, counter), source, new ModifierValue(effect.value * multiplier, 0f));
        return true;
    }

    private static bool ApplyHousing(in GameEffect effect, string source, float multiplier)
    {
        var pop = PopGrowthLogic.Instance;
        if (pop == null) return Missing(nameof(PopGrowthLogic), effect, source);
        pop.HousingModifiers.Add(PopGrowthLogic.HousingKey, source, ToModifier(effect, multiplier));
        return true;
    }

    private static bool ApplySpecial(in GameEffect effect, string source, float multiplier)
    {
        GameLog.Event($"{source}: special ability '{effect.target}' has no generic behaviour; handle it in the owning system.", Log);
        return true;
    }

    // ===== TARGET RESOLUTION =====

    private static readonly HashSet<string> EverythingWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "all", "*", "everything", "all resources", "resources", "all buildings", "buildings", "building", "all units and buildings"
    };

    private static readonly HashSet<string> UnitWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "unit", "units", "all units" };

    /// <summary>Resource-facing target: a resource name, a section of resources, or everything.</summary>
    public static string ResolveResourceTarget(string target, ScopeType scope)
    {
        if (scope == ScopeType.Global || string.IsNullOrEmpty(target) || EverythingWords.Contains(target)) return ModifierTargets.All;
        if (scope == ScopeType.Section) return ModifierTargets.Section(target);
        if (!GameCatalog.IsResource(target) && GameCatalog.IsSection(target)) return ModifierTargets.Section(target);
        return target;
    }

    /// <summary>
    /// Production-unit target: a unit name, section, unit type or everything. A resource name resolves
    /// to itself with <paramref name="isResource"/> set, meaning "that resource's output".
    /// </summary>
    public static string ResolveUnitTarget(string target, ScopeType scope, out bool isResource)
    {
        isResource = false;
        if (scope == ScopeType.Global || string.IsNullOrEmpty(target) || EverythingWords.Contains(target)) return ModifierTargets.All;
        if (UnitWords.Contains(target)) return ModifierTargets.Type("Unit");
        if (scope == ScopeType.Section) return ModifierTargets.Section(target);
        if (GameCatalog.IsProductionUnit(target)) return target;
        if (GameCatalog.IsSection(target)) return ModifierTargets.Section(target);
        if (GameCatalog.IsUnitType(target)) return ModifierTargets.Type(target);
        if (GameCatalog.IsResource(target))
        {
            isResource = true;
            return target;
        }
        return target;
    }

    /// <summary>What a scaling bonus counts: production units (name/section/type) or a game value (housing, population, a stat).</summary>
    private static string ResolveCounter(string condition)
    {
        if (GameCatalog.IsProductionUnit(condition)) return condition;
        if (GameCatalog.IsSection(condition)) return ModifierTargets.Section(condition);
        if (GameCatalog.IsUnitType(condition)) return ModifierTargets.Type(condition);
        return ProductionScalingKey.ValuePrefix + condition;
    }

    // ===== VALIDATION =====

    /// <summary>Every problem with an effect, including targets that do not exist in the content catalogs.</summary>
    public static List<string> Validate(in GameEffect effect)
    {
        var problems = new List<string>();
        var (isValid, error) = effect.ValidateShape();
        if (!isValid) problems.Add(error);
        if (!Handlers.ContainsKey(effect.type)) problems.Add($"no handler for {effect.type}");
        if (!string.IsNullOrEmpty(error)) return problems;

        switch (effect.type)
        {
            case GameEffectType.PillarBonus:
                if (StatDefinitions.KindOf(effect.target) != StatDefinitions.StatKind.Pillar) problems.Add($"'{effect.target}' is not a pillar");
                break;
            case GameEffectType.SubstatBonus:
                if (StatDefinitions.KindOf(effect.target) != StatDefinitions.StatKind.Substat) problems.Add($"'{effect.target}' is not a substat");
                break;
            case GameEffectType.DerivedStatBonus:
                if (StatDefinitions.KindOf(effect.target) != StatDefinitions.StatKind.Derived) problems.Add($"'{effect.target}' is not a derived stat");
                break;
            case GameEffectType.ResourceModifier:
            case GameEffectType.ClickPowerBonus:
                if (effect.scope != ScopeType.Global && !string.IsNullOrEmpty(effect.target) && !EverythingWords.Contains(effect.target)
                    && !GameCatalog.IsResource(effect.target) && !GameCatalog.IsSection(effect.target))
                {
                    problems.Add($"'{effect.target}' is neither a resource nor a section");
                }
                break;
            case GameEffectType.ProductionModifier:
            case GameEffectType.ConstructionCostModifier:
                if (effect.scope != ScopeType.Global && !string.IsNullOrEmpty(effect.target) && !EverythingWords.Contains(effect.target) && !UnitWords.Contains(effect.target)
                    && !GameCatalog.IsProductionUnit(effect.target) && !GameCatalog.IsSection(effect.target) && !GameCatalog.IsUnitType(effect.target)
                    && !(effect.type == GameEffectType.ProductionModifier && GameCatalog.IsResource(effect.target)))
                {
                    problems.Add($"'{effect.target}' is not a production unit, section or unit type");
                }
                break;
            case GameEffectType.ProductionScalingBonus:
                if (!GameCatalog.IsResource(effect.target)) problems.Add($"'{effect.target}' is not a resource");
                if (!string.IsNullOrEmpty(effect.condition) && !GameCatalog.IsProductionUnit(effect.condition) && !GameCatalog.IsSection(effect.condition)
                    && !GameCatalog.IsUnitType(effect.condition) && !StatDefinitions.IsKnown(effect.condition) && !GameValues.IsKnownDomain(effect.condition))
                {
                    problems.Add($"condition '{effect.condition}' is not a production unit, section, unit type, stat or game value");
                }
                break;
        }
        if (effect.modifierType == ModifierType.SetValue && effect.type != GameEffectType.PillarBonus && effect.type != GameEffectType.SubstatBonus && effect.type != GameEffectType.DerivedStatBonus)
        {
            problems.Add($"SetValue is only supported for stat bonuses; {effect.type} treats it as Add");
        }
        return problems;
    }
}

/// <summary>Key format for production scaling rules stored in a <see cref="ModifierLedger"/>: "resource|counter".</summary>
public static class ProductionScalingKey
{
    public const char Separator = '|';
    /// <summary>Counter meaning "every production unit that already produces the target resource".</summary>
    public const string Producers = "@producers";
    /// <summary>Prefix for counters read from <see cref="GameValues"/> (housing, population, stats...).</summary>
    public const string ValuePrefix = "value:";

    public static string Make(string resource, string counter) => resource + Separator + counter;

    public static bool TryParse(string key, out string resource, out string counter)
    {
        int index = key?.IndexOf(Separator) ?? -1;
        if (index <= 0)
        {
            resource = null;
            counter = null;
            return false;
        }
        resource = key.Substring(0, index);
        counter = key.Substring(index + 1);
        return true;
    }
}
