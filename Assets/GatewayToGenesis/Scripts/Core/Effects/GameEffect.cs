using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// The one shape every gameplay effect takes before it is applied, whatever authored it:
/// a legend bonus, a civic effect, a council seat bonus, a weather effect or code.
/// Data classes keep their own serialized fields and convert with <c>ToEffect()</c>
/// (see <see cref="GameEffectAdapters"/>), so descriptions, validation and application exist once.
/// </summary>
public struct GameEffect
{
    public GameEffectType type;
    public float value;
    public ModifierType modifierType;
    /// <summary>Stat, resource, section, unit type or production unit, depending on <see cref="type"/>.</summary>
    public string target;
    /// <summary>ProductionScalingBonus only: which production units count (unit, section or type).</summary>
    public string condition;
    public ScopeType scope;

    public GameEffect(GameEffectType type, float value, ModifierType modifierType, string target = null, ScopeType scope = ScopeType.Individual, string condition = null)
    {
        this.type = type;
        this.value = value;
        this.modifierType = modifierType;
        this.target = target;
        this.scope = scope;
        this.condition = condition;
    }

    public bool IsPercentage => modifierType == ModifierType.Percentage;

    /// <summary>
    /// Player-facing summary, e.g. "+10% Food production" or "+2 waltz". This is the only wording of an
    /// effect: legend, civic, seat and weather descriptions, tooltips and logs all come from here.
    /// </summary>
    public string Describe() => Sentence() ?? $"{Amount()} {Label(type)}";

    /// <summary>
    /// False while <paramref name="type"/> falls through to the generic "{amount} {Type Name}" wording.
    /// <c>CoreSystemsTests.EveryEffectTypeHasItsOwnSentence</c> fails until a new type is worded here.
    /// </summary>
    public static bool HasSentence(GameEffectType type) => new GameEffect(type, 1f, ModifierType.Add, "x").Sentence() != null;

    private string Amount() => (value > 0 ? "+" : "") + value.ToString("0.##", CultureInfo.InvariantCulture) + (IsPercentage ? "%" : "");

    /// <summary>The wording of each effect type; null for a type that has none yet.</summary>
    private string Sentence()
    {
        string amount = Amount();
        switch (type)
        {
            case GameEffectType.PillarBonus:
            case GameEffectType.SubstatBonus:
            case GameEffectType.DerivedStatBonus:
                return string.IsNullOrEmpty(target) ? $"{amount} {Label(type)}" : $"{amount} {target}";
            case GameEffectType.ResourceModifier:
                return $"{amount} {DescribeTarget()} production";
            case GameEffectType.ProductionModifier:
                return $"{amount} {DescribeTarget()} efficiency";
            case GameEffectType.ClickPowerBonus:
                return $"{amount} {DescribeTarget()} click power";
            case GameEffectType.ConstructionCostModifier:
                return $"{amount} {DescribeTarget()} construction cost";
            case GameEffectType.ProductionScalingBonus:
                return string.IsNullOrEmpty(condition) ? $"{amount} {target} per production unit" : $"{amount} {target} per {condition}";
            case GameEffectType.MaxMoraleModifier:
                return $"{amount} max morale";
            case GameEffectType.MoraleBalanceModifier:
                return $"Morale balance -{value.ToString("0.##", CultureInfo.InvariantCulture)}{(IsPercentage ? "%" : "")} (easier to stay positive)";
            case GameEffectType.SatisfactionThresholdModifier:
                return $"{amount} satisfaction threshold (easier upgrades)";
            case GameEffectType.HousingBonus:
                return $"{amount} housing capacity";
            case GameEffectType.MoraleModifier:
                return $"{amount} morale";
            case GameEffectType.SpecialAbility:
                return "Special Ability";
            default:
                return null;
        }
    }

    private string DescribeTarget()
    {
        if (scope == ScopeType.Global || string.IsNullOrEmpty(target)) return type == GameEffectType.ResourceModifier || type == GameEffectType.ClickPowerBonus ? "all resources" : "all buildings";
        if (scope == ScopeType.Section) return $"{target} section";
        return target;
    }

    private static string Label(GameEffectType type)
    {
        // "ProductionScalingBonus" -> "Production Scaling Bonus"
        var name = type.ToString();
        var chars = new List<char>(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) chars.Add(' ');
            chars.Add(name[i]);
        }
        return new string(chars.ToArray());
    }

    /// <summary>
    /// Structural problems (missing fields). Existence of the target is checked by
    /// <see cref="EffectRouter.Validate"/>, which needs the content catalogs.
    /// </summary>
    public (bool isValid, string errorMessage) ValidateShape()
    {
        switch (type)
        {
            case GameEffectType.PillarBonus:
            case GameEffectType.SubstatBonus:
            case GameEffectType.DerivedStatBonus:
                if (string.IsNullOrEmpty(target)) return (false, $"{type} requires a target stat");
                break;
            case GameEffectType.ResourceModifier:
            case GameEffectType.ProductionModifier:
            case GameEffectType.ClickPowerBonus:
            case GameEffectType.ConstructionCostModifier:
                if (string.IsNullOrEmpty(target) && scope == ScopeType.Individual) return (false, $"{type} requires a target or a Section/Global scope");
                if (string.IsNullOrEmpty(target) && scope == ScopeType.Section) return (false, $"{type} with Section scope requires the section name as target");
                break;
            case GameEffectType.ProductionScalingBonus:
                if (string.IsNullOrEmpty(target)) return (false, $"{type} requires a target resource (what is produced)");
                break;
        }
        if (float.IsNaN(value) || float.IsInfinity(value)) return (false, $"{type} has a non-finite value");
        return (true, "");
    }

    public override string ToString() => $"{type} {value}{(IsPercentage ? "%" : "")} → {target}{(string.IsNullOrEmpty(condition) ? "" : " per " + condition)} ({scope})";
}

/// <summary>Converts every authored effect format into a <see cref="GameEffect"/>.</summary>
public static class GameEffectAdapters
{
    public static GameEffect ToEffect(this LegendBonus bonus) =>
        new GameEffect(bonus.bonusType, bonus.modifierValue, bonus.modifierType, bonus.targetStat, bonus.scope, bonus.conditionStat);

    public static GameEffect ToEffect(this CivicEffect effect) =>
        new GameEffect(effect.effectType, effect.modifierValue, effect.modifierType, effect.targetStat, effect.scope, effect.conditionStat);

    public static GameEffect ToEffect(this WeatherProfileSO.WeatherEffect effect) =>
        new GameEffect(effect.effectType, effect.modifierValue, effect.modifierType, effect.targetStat, effect.scope, effect.conditionStat);

    /// <summary>Seat bonuses have no scope field: an empty target means everything.</summary>
    public static GameEffect ToEffect(this SeatBonus bonus) => FromSeat(bonus.bonusType, bonus.modifierValue, bonus.modifierType, bonus.targetStat);

    public static GameEffect ToEffect(this CivicSeatBonus bonus) => FromSeat(bonus.bonusType, bonus.modifierValue, bonus.modifierType, bonus.targetStat);

    private static GameEffect FromSeat(SeatBonusType seatType, float value, ModifierType modifierType, string target)
    {
        var scope = string.IsNullOrEmpty(target) ? ScopeType.Global : ScopeType.Individual;
        if (seatType == SeatBonusType.SatisfactionModifier)
        {
            // Legacy seat type: a Satisfaction Effectiveness bonus.
            return new GameEffect(GameEffectType.DerivedStatBonus, value, modifierType, "satisfactionEffectiveness");
        }
        return new GameEffect(ToEffectType(seatType), value, modifierType, target, scope);
    }

    /// <summary>Every SeatBonusType maps to a GameEffectType; CivicBonus is a marker with no effect of its own.</summary>
    public static GameEffectType ToEffectType(SeatBonusType seatType)
    {
        switch (seatType)
        {
            case SeatBonusType.PillarBonus: return GameEffectType.PillarBonus;
            case SeatBonusType.SubstatBonus: return GameEffectType.SubstatBonus;
            case SeatBonusType.DerivedStatBonus: return GameEffectType.DerivedStatBonus;
            case SeatBonusType.ResourceModifier: return GameEffectType.ResourceModifier;
            case SeatBonusType.ProductionModifier: return GameEffectType.ProductionModifier;
            case SeatBonusType.ClickPowerBonus: return GameEffectType.ClickPowerBonus;
            case SeatBonusType.MaxMoraleModifier: return GameEffectType.MaxMoraleModifier;
            case SeatBonusType.MoraleModifier: return GameEffectType.MoraleModifier;
            case SeatBonusType.MoraleBalanceModifier: return GameEffectType.MoraleBalanceModifier;
            case SeatBonusType.SatisfactionModifier: return GameEffectType.DerivedStatBonus;
            case SeatBonusType.SatisfactionThresholdModifier: return GameEffectType.SatisfactionThresholdModifier;
            case SeatBonusType.HousingBonus: return GameEffectType.HousingBonus;
            case SeatBonusType.ProductionScalingBonus: return GameEffectType.ProductionScalingBonus;
            case SeatBonusType.ConstructionCostModifier: return GameEffectType.ConstructionCostModifier;
            case SeatBonusType.SpecialAbility: return GameEffectType.SpecialAbility;
            case SeatBonusType.CivicBonus: return GameEffectType.SpecialAbility;
            default: throw new ArgumentOutOfRangeException(nameof(seatType), seatType, "SeatBonusType has no GameEffectType mapping");
        }
    }

    /// <summary>Civic effects shown on a civic's council seat use the seat vocabulary.</summary>
    public static SeatBonusType ToSeatBonusType(GameEffectType effectType)
    {
        switch (effectType)
        {
            case GameEffectType.PillarBonus: return SeatBonusType.PillarBonus;
            case GameEffectType.SubstatBonus: return SeatBonusType.SubstatBonus;
            case GameEffectType.DerivedStatBonus: return SeatBonusType.DerivedStatBonus;
            case GameEffectType.ResourceModifier: return SeatBonusType.ResourceModifier;
            case GameEffectType.ProductionModifier: return SeatBonusType.ProductionModifier;
            case GameEffectType.ClickPowerBonus: return SeatBonusType.ClickPowerBonus;
            case GameEffectType.ConstructionCostModifier: return SeatBonusType.ConstructionCostModifier;
            case GameEffectType.ProductionScalingBonus: return SeatBonusType.ProductionScalingBonus;
            case GameEffectType.MaxMoraleModifier: return SeatBonusType.MaxMoraleModifier;
            case GameEffectType.MoraleBalanceModifier: return SeatBonusType.MoraleBalanceModifier;
            case GameEffectType.SatisfactionThresholdModifier: return SeatBonusType.SatisfactionThresholdModifier;
            case GameEffectType.HousingBonus: return SeatBonusType.HousingBonus;
            case GameEffectType.MoraleModifier: return SeatBonusType.MoraleModifier;
            default: return SeatBonusType.SpecialAbility;
        }
    }
}
