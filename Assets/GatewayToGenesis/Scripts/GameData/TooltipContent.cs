using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Wording for every game-object tooltip: resources, buildings, technologies, unlockables, legends, council
/// seats and civics. Components pass their data here through <see cref="ITooltipSource"/>, so each kind of
/// thing is worded once and every place that shows it (tab slot, unlock preview, government tab) agrees.
/// Numbers come from the systems that own them (build costs from <see cref="GameUnitsLogic.GetBuildCost"/>,
/// rates from <see cref="GlobalProductionManager"/>), never recomputed here. Event wording is
/// <see cref="EventText"/>.
/// </summary>
public static class TooltipContent
{
    private const string Good = "green";
    private const string Bad = "red";

    // ===== RESOURCES =====

    /// <summary>A resource slot. The trigger picks the view: storage breakdown, production breakdown, or description.</summary>
    public static bool Resource(GameResourceSlot slot, TooltipTrigger trigger, TooltipData d)
    {
        if (slot == null || slot.gameUnit == null) return false;
        var unit = slot.gameUnit;
        d.title = unit.name;
        if (trigger != null && trigger.isBreakdownDisplay) d.breakdown = StorageBreakdown(unit.name);
        else if (trigger != null && trigger.isProductionModifiers) d.modifiers = ProductionBreakdown(unit.name);
        else
        {
            d.description = unit.description;
            d.type = unit.type;
        }
        return true;
    }

    public static string StorageBreakdown(string resourceName)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null || !units.storageBreakdown.TryGetValue(resourceName, out var sources)) return string.Empty;
        float total = 0f;
        var lines = new StringBuilder();
        foreach (var entry in sources)
        {
            lines.Append($"\n - {entry.Key}: <color={Good}>{entry.Value:0.##}</color>");
            total += entry.Value;
        }
        return $"Total Storage: <color=yellow>{total:0.##}</color>\n\n<b>Detailed Breakdown:</b>{lines}";
    }

    /// <summary>Net rate plus every contribution, straight from <see cref="GlobalProductionManager.GetBreakdown"/>.</summary>
    public static string ProductionBreakdown(string resourceName)
    {
        var production = GlobalProductionManager.Instance;
        if (production == null) return string.Empty;
        var sb = new StringBuilder($"Net Production Rate: <color=yellow>{production.GetNetProductionRate(resourceName):0.##}</color>");
        var lines = production.GetBreakdown(resourceName);
        if (lines.Count == 0) return sb.ToString();
        sb.Append("\n\n<b>Detailed Breakdown:</b>");
        foreach (var line in lines)
        {
            string sign = line.value > 0f ? "+" : "";
            sb.Append($"\n - {line.label}: <color={(line.value >= 0f ? Good : Bad)}>{sign}{line.value:0.##}{(line.isPercent ? "%" : "")}</color>");
        }
        return sb.ToString();
    }

    // ===== BUILDINGS AND UNITS =====

    /// <summary>A building or unit in the production tab: next price, effective output and active modifiers.</summary>
    public static bool Production(GameProductionSlot slot, TooltipData d)
    {
        if (slot == null || slot.gameUnit == null) return false;
        d.title = slot.gameUnit.name;
        d.description = slot.gameUnit.description;
        d.type = slot.gameUnit.type;
        var data = slot.productionUnitData;
        if (data == null) return true;

        var costs = new StringBuilder("Requires:");
        for (int i = 0; i < data.buildResourceRequirements.Count; i++)
        {
            string resource = data.buildResourceRequirements[i];
            costs.Append('\n').Append(CostLine(slot.GetEffectiveConstructionCost(i), resource, CanAfford(resource, slot.GetEffectiveConstructionCost(i)), false));
        }
        d.requirements = costs.ToString();

        d.effects = UnitEffects(data, slot);
        string modifiers = ModifierSummary(slot.gameUnit);
        if (!string.IsNullOrEmpty(modifiers)) d.effects += "\n\n" + modifiers;
        return true;
    }

    /// <summary>What one unit provides. With a built slot the rates include production efficiency.</summary>
    public static string UnitEffects(ProductionUnitData data, GameProductionSlot slot = null)
    {
        var sb = new StringBuilder("Effects:");
        if (data.housing > 0) sb.Append($"\n +{data.housing} Housing");
        for (int i = 0; i < data.producedResources.Count && i < data.productionRates.Count; i++)
        {
            float rate = slot != null ? slot.GetEffectiveProductionRate(i) : data.productionRates[i];
            sb.Append($"\n +{rate:0.##}/s {data.producedResources[i]}");
        }
        for (int i = 0; i < data.storageResources.Count && i < data.storageAmount.Count; i++)
        {
            sb.Append($"\n Max +{data.storageAmount[i]:0.##} {data.storageResources[i]}");
        }
        if (data.consumedResources.Count > 0)
        {
            sb.Append("\nConsumption:");
            for (int i = 0; i < data.consumedResources.Count && i < data.consumeRates.Count; i++)
            {
                sb.Append($"\n -{data.consumeRates[i]:0.##}/s {data.consumedResources[i]}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Production efficiency and construction cost affecting a unit, with every source and the scope it was
    /// registered on (the unit, its section, its type, or everything). Empty when nothing applies.
    /// </summary>
    public static string ModifierSummary(GameUnit unit)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null || unit == null) return string.Empty;

        var lines = new List<string>();
        float efficiency = units.GetProductionEfficiencyPercent(unit);
        if (efficiency != 0f) lines.Add($"<color={(efficiency > 0 ? Good : Bad)}>Production Efficiency: {efficiency:+0.#;-0.#}%</color>");
        float cost = units.GetConstructionCostPercent(unit);
        if (cost != 0f) lines.Add($"<color={(cost > 0 ? Bad : Good)}>Construction Cost: {(cost > 0 ? "increases" : "reduces")} by {Mathf.Abs(cost):0.#}%</color>");

        var sources = new List<string>();
        foreach (var key in new[] { unit.name, ModifierTargets.Section(unit.section), ModifierTargets.Type(unit.type), ModifierTargets.All })
        {
            if (key == null) continue;
            foreach (var source in units.ProductionEfficiency.Sources(key))
            {
                sources.Add($"<color={(source.Value.Percent >= 0 ? Good : Bad)}>• {source.Value.Percent:+0.#;-0.#}% output from {source.Key} ({ModifierTargets.Describe(key)})</color>");
            }
            foreach (var source in units.ConstructionCost.Sources(key))
            {
                sources.Add($"<color={(source.Value.Percent > 0 ? Bad : Good)}>• {source.Value.Percent:+0.#;-0.#}% cost from {source.Key} ({ModifierTargets.Describe(key)})</color>");
            }
        }

        if (lines.Count == 0 && sources.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        if (lines.Count > 0) sb.Append("<b>Active Modifiers:</b>\n").Append(string.Join("\n", lines));
        if (sources.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("<b>Modifier Sources:</b>\n").Append(string.Join("\n", sources));
        }
        return sb.ToString();
    }

    // ===== TECHNOLOGIES =====

    public static bool Technology(GameTechnologySlot slot, TooltipData d)
    {
        if (slot == null || slot.gameUnit == null) return false;
        d.title = slot.gameUnit.name;
        d.description = slot.gameUnit.description;
        string type = slot.gameUnit.type;
        d.type = type == "Event" ? $"<color=yellow>{type} Tech</color>" : type == "Crisis" ? $"<color={Bad}>{type} Tech</color>" : $"{type} Tech";

        var tech = slot.technologyData;
        if (tech == null) return true;
        var units = GameUnitsLogic.Instance;
        Dictionary<string, float> progress = null;
        units?.technologyProgress.TryGetValue(slot, out progress);

        var costs = new StringBuilder("Requires:");
        for (int i = 0; i < tech.resourceRequirements.Count && i < tech.resourceAmount.Count; i++)
        {
            string resource = tech.resourceRequirements[i];
            float remaining = tech.resourceAmount[i];
            if (progress != null && progress.TryGetValue(resource, out float paid)) remaining -= paid;
            remaining = slot.isUnlocked ? 0f : Mathf.Max(0f, remaining);
            bool met = slot.isUnlocked || CanAfford(resource, remaining);
            costs.Append('\n').Append(CostLine(remaining, resource, met, true));
        }
        d.requirements = costs.ToString();

        if (tech.techRequirements.Count > 0)
        {
            var prerequisites = new StringBuilder("Must Unlock:");
            foreach (string required in tech.techRequirements)
            {
                bool unlocked = units != null && units.IsTechnologyUnlocked(required);
                prerequisites.Append($"\n<color={(unlocked ? Good : Bad)}> {required}</color>");
            }
            d.prerequisites = prerequisites.ToString();
        }
        return true;
    }

    /// <summary>One reward on a technology card.</summary>
    public static bool Unlockable(TechUnlockable unlockable, TooltipData d)
    {
        if (unlockable == null) return false;
        var unit = unlockable.gameUnit;
        if (unit != null) d.title = unit.name;

        switch (unlockable.unlockableType)
        {
            case TechUnlockableType.ClickPower:
                if (unlockable.resourceModifier > 0 && unit != null)
                {
                    d.type = "Click Power Modifier";
                    d.effects = $"Increases the Click Power of {unit.name} by {unlockable.resourceModifier}";
                    d.description = "Carpal Tunnel Power!";
                }
                else if (unit != null)
                {
                    d.description = unit.description;
                    d.type = unit.type;
                }
                break;

            case TechUnlockableType.Building:
            case TechUnlockableType.Unit:
                d.type = unlockable.unlockableType.ToString();
                if (unit != null && GameCatalog.ProductionUnits.TryGet(unit.name, out ProductionUnitData production))
                {
                    d.description = unit.description;
                    var costs = new StringBuilder("Requires:");
                    for (int i = 0; i < production.buildResourceRequirements.Count && i < production.buildRequirementsAmount.Count; i++)
                    {
                        string resource = production.buildResourceRequirements[i];
                        float amount = production.buildRequirementsAmount[i];
                        costs.Append('\n').Append(CostLine(amount, resource, CanAfford(resource, amount), true));
                    }
                    d.requirements = costs.ToString();
                    d.effects = UnitEffects(production);
                }
                break;

            case TechUnlockableType.Modifier:
                d.type = "Bonus Modifier";
                d.effects = $"Boosts the efficiency of all workshops producing {unit?.name} by {unlockable.resourceModifier}%";
                d.description = "Upping the uppies 100% upper";
                break;

            case TechUnlockableType.DemandModifier:
                d.type = "Bonus Modifier";
                d.effects = $"Reduces the Food Demand of Population by {unlockable.resourceModifier}%";
                d.description = "Not starving anymore...";
                break;

            case TechUnlockableType.Arts:
                d.type = "Arts";
                d.description = $"Unlocks a unique Arts unit: {unit?.name}";
                break;

            case TechUnlockableType.Special:
                d.title = "Special Unlock";
                d.description = unlockable.description;
                d.effects = !string.IsNullOrEmpty(unlockable.effects) ? unlockable.effects : $"Unlocks a special unit: {unit?.name}";
                break;

            default:
                d.type = "Unknown";
                d.description = "Unknown unlockable type.";
                GameLog.Warning($"Unknown unlockable type {unlockable.unlockableType} on '{unlockable.name}'.", LogChannel.UI);
                break;
        }
        return true;
    }

    // ===== GOVERNMENT =====

    /// <summary>A legend. Seated as Head of State, its bonuses show the Head of State multiplier and the seat's own bonuses.</summary>
    public static bool Legend(LegendData legend, TooltipData d)
    {
        if (legend == null) return false;
        d.title = legend.legendName;
        d.description = legend.personalQuote;
        d.type = $"<b>{legend.rarity} {legend.legendClass}</b>";
        d.requirements = legend.councilAssignmentDescription;

        var government = GovernmentLogic.Instance;
        var headOfState = government != null ? government.GetCouncilSeat(GovernmentLogic.HeadOfStateIndex) : null;
        bool isHeadOfState = headOfState != null && headOfState.assignedLegend == legend;
        float multiplier = isHeadOfState ? government.HeadOfStateMultiplier : 1f;

        var sb = new StringBuilder();
        if (legend.bonuses != null && legend.bonuses.Count > 0)
        {
            sb.Append(isHeadOfState ? $"\n<b>Assignment Effects (×{multiplier:0.##} as Head of State):</b>" : "\n<b>Assignment Effects:</b>");
            foreach (var bonus in legend.bonuses)
            {
                if (bonus == null) continue;
                sb.Append("\n- ").Append(bonus.GetAutoDescription());
                if (isHeadOfState && !Mathf.Approximately(multiplier, 1f))
                {
                    string unit = bonus.modifierType == ModifierType.Percentage ? "%" : "";
                    sb.Append($" <color=yellow>(→ {bonus.modifierValue * multiplier:+0.##;-0.##}{unit})</color>");
                }
            }
        }
        if (isHeadOfState && headOfState.seatBonuses.Count > 0)
        {
            sb.Append("\n<b>Seat Effects:</b>").Append(SeatBonusLines(headOfState));
        }
        d.effects = sb.ToString();
        return true;
    }

    /// <summary>A council seat: title, role, allowed classes, its bonuses and whether it is active yet.</summary>
    public static bool Seat(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        d.title = seat.GetEffectiveTitle();
        d.description = seat.roleplayDescription;
        d.type = $"<b>Allowed Classes:</b>\n{AllowedClasses(seat)}";
        if (seat.seatBonuses.Count > 0) d.effects = "\n<b>Seat Effects</b> <color=#AAAAAA>(as soon as a Legend is seated)</color><b>:</b>" + SeatBonusLines(seat);
        d.prerequisites = SeatStatus(seat);
        return true;
    }

    /// <summary>A seat's portrait: its legend, or a prompt to assign one.</summary>
    public static bool SeatPortrait(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        if (seat.assignedLegend != null) return Legend(seat.assignedLegend, d);
        d.title = "Click to assign a Legend";
        d.prerequisites = SeatCooldown(seat);
        return true;
    }

    /// <summary>A seat's active indicator (green once its legend's bonuses apply).</summary>
    public static bool SeatActivity(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        d.title = seat.IsActive() ? "Seat Active" : "Seat Inactive";
        d.description = SeatStatus(seat);
        return true;
    }

    /// <summary>A seat's cooldown indicator (cyan when its legend can be changed).</summary>
    public static bool SeatCooldownIndicator(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        d.title = "Seat Cooldown";
        d.description = SeatCooldown(seat);
        return true;
    }

    /// <summary>"Active", "Seat effects active, Legend activates in N Sevenths" or "No Legend Assigned", plus any change cooldown.</summary>
    public static string SeatStatus(CouncilSeat seat)
    {
        if (seat == null) return string.Empty;
        string status = seat.assignedLegend == null ? $"<color={Bad}>No Legend Assigned</color>"
            : seat.IsActive() ? $"<color={Good}>Active</color>"
            : $"<color={Good}>Seat effects active</color>\n<color=yellow>Legend activates in {seat.seventhsUntilActive} Sevenths</color>";
        var government = GovernmentLogic.Instance;
        int cooldown = government != null ? government.GetSeatCooldownRemaining(seat.seatIndex) : 0;
        return cooldown > 0 ? $"{status}\n<color=#6699FF>Can change again in {cooldown} Sevenths</color>" : status;
    }

    /// <summary>Cooldown status only (the seat's cooldown indicator).</summary>
    public static string SeatCooldown(CouncilSeat seat)
    {
        var government = GovernmentLogic.Instance;
        int cooldown = seat != null && government != null ? government.GetSeatCooldownRemaining(seat.seatIndex) : 0;
        return cooldown > 0 ? $"<color=#6699FF>Can change again in {cooldown} Sevenths</color>" : $"<color=cyan>Ready to change</color>";
    }

    public static string AllowedClasses(CouncilSeat seat)
    {
        var classes = seat.allowedLegendClasses;
        int classCount = System.Enum.GetValues(typeof(LegendClass)).Length;
        return classes == null || classes.Count == 0 || classes.Count >= classCount ? "Any/All Classes" : string.Join(" - ", classes);
    }

    private static string SeatBonusLines(CouncilSeat seat)
    {
        var sb = new StringBuilder();
        foreach (var bonus in seat.seatBonuses)
        {
            if (bonus == null || bonus.bonusType == SeatBonusType.CivicBonus) continue;
            sb.Append("\n- ").Append(bonus.GetAutoDescription());
        }
        return sb.ToString();
    }

    public static bool Civic(CivicData civic, TooltipData d)
    {
        if (civic == null) return false;
        d.title = civic.civicName;
        d.description = civic.description;
        d.type = $"<b>{civic.rarity} {civic.tier} Civic</b>";
        if (civic.effects != null && civic.effects.Count > 0)
        {
            var sb = new StringBuilder("\n<b>Assignment Effects:</b>");
            foreach (var effect in civic.effects) if (effect != null) sb.Append("\n- ").Append(effect.GetAutoDescription());
            d.effects = sb.ToString();
        }
        if (civic.grantsCouncilPosition && civic.councilPosition != null && !string.IsNullOrEmpty(civic.councilPosition.title))
        {
            d.requirements = $"Grants {civic.councilPosition.title} Council Position";
        }
        return true;
    }

    /// <summary>A seat offered in the civic pool (default or civic-granted), by title.</summary>
    public static bool SeatOption(string seatTitle, TooltipData d)
    {
        var government = GovernmentLogic.Instance;
        if (string.IsNullOrEmpty(seatTitle) || government == null) return false;
        var (title, effects, leaderClasses, _) = government.GetSeatDisplayInfo(seatTitle);
        d.title = title;
        var civic = government.GetCivicDataForSeatTitle(seatTitle);
        if (civic != null)
        {
            d.description = civic.description;
            d.type = $"<b>{civic.rarity} {civic.tier} Civic</b>";
            if (civic.grantsCouncilPosition && civic.councilPosition != null && !string.IsNullOrEmpty(civic.councilPosition.title))
            {
                d.requirements = $"Grants {civic.councilPosition.title} Council Position";
            }
        }
        else
        {
            d.description = "Default council position";
            d.type = "Default Seat";
        }
        if (!string.IsNullOrEmpty(effects) && effects != "N/A") d.effects = effects;
        d.prerequisites = !string.IsNullOrEmpty(leaderClasses) && leaderClasses != "N/A" ? $"Allowed Classes: {leaderClasses}" : "Any/All Classes";
        return true;
    }

    // ===== SHARED =====

    /// <summary>One cost line, green when affordable. <paramref name="icon"/> keeps the inline sprite technology costs use.</summary>
    public static string CostLine(float amount, string resource, bool affordable, bool icon) =>
        $"<color={(affordable ? Good : Bad)}> {Amount(amount)}{(icon ? " <sprite=2>" : "")} {resource}</color>";

    public static bool CanAfford(string resource, float amount)
    {
        var slot = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(resource) : null;
        return slot != null && slot.amount >= amount;
    }

    /// <summary>Up to three decimals, truncated, never showing 0 for a small positive remainder.</summary>
    public static string Amount(float value)
    {
        float clamped = Mathf.Max(0f, value);
        float truncated = Mathf.Floor(clamped * 1000f) / 1000f;
        if (clamped > 0f && truncated <= 0f) truncated = 0.001f;
        return truncated.ToString("0.###");
    }
}
