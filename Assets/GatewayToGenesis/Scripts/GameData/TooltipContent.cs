using System.Collections.Generic;
using System.Linq;
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
    private const string Good = TooltipText.GoodHex;
    private const string Bad = TooltipText.BadHex;

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
            string stores = unit.role == ResourceRole.Food ? Pantry.DescribeFood() : Pantry.Instance != null ? Pantry.Instance.Describe(unit.name) : null;
            if (stores != null) d.summary = stores;
            string culture = CultureLifeRules.ResourceUse(CultureSystem.Instance != null ? CultureSystem.Instance.Life : new CultureLifeTuning(), unit.name);
            if (culture != null) d.summary = string.IsNullOrEmpty(d.summary) ? culture : d.summary + "\n" + culture;
            Keywords.AddLore(d, ResourceKeyword(unit.name));
        }
        return true;
    }

    /// <summary>The keyword id of a resource ("resource:aetherlight"), where its lore is authored.</summary>
    public static string ResourceKeyword(string resourceName) => "resource:" + resourceName.ToLowerInvariant();

    /// <summary>The keyword id of a technology ("tech:horology").</summary>
    public static string TechnologyKeyword(string technologyName) => "tech:" + technologyName.ToLowerInvariant();

    public static string StorageBreakdown(string resourceName)
    {
        var units = GameUnitsLogic.Instance;
        if (units == null || !units.storageBreakdown.TryGetValue(resourceName, out var sources)) return string.Empty;
        float total = 0f;
        var lines = new List<string>();
        foreach (var entry in sources)
        {
            lines.Add(TooltipText.Bullet(TooltipText.Row(entry.Key, TooltipText.Good($"+{entry.Value:0.##}"))));
            total += entry.Value;
        }
        return TooltipText.Row("Total Storage", TooltipText.Value($"{total:0.##}")) + "\n\n" + TooltipText.Heading("Breakdown") + "\n" + TooltipText.Lines(lines);
    }

    /// <summary>Net rate plus every contribution, straight from <see cref="GlobalProductionManager.GetBreakdown"/>.</summary>
    public static string ProductionBreakdown(string resourceName)
    {
        var production = GlobalProductionManager.Instance;
        if (production == null) return string.Empty;
        string net = TooltipText.Row("Net Production", TooltipText.Signed(production.GetNetProductionRate(resourceName), "/s"));
        var lines = production.GetBreakdown(resourceName);
        if (lines.Count == 0) return net;
        var rows = new List<string>();
        foreach (var line in lines)
        {
            rows.Add(TooltipText.Bullet(TooltipText.Row(line.label, TooltipText.Signed(line.value, line.isPercent ? "%" : "/s"))));
        }
        return net + "\n\n" + TooltipText.Heading("Breakdown") + "\n" + TooltipText.Lines(rows);
    }

    // ===== BUILDINGS AND UNITS =====

    /// <summary>A building or unit in the production tab: next price, effective output and active modifiers.</summary>
    public static bool Production(GameProductionSlot slot, TooltipData d)
    {
        if (slot == null || slot.gameUnit == null) return false;
        d.title = slot.gameUnit.name;
        d.description = slot.gameUnit.description;
        d.type = slot.gameUnit.type;
        Keywords.AddLore(d, slot.gameUnit.name);
        var data = slot.productionUnitData;
        if (data == null) return true;

        var costs = new StringBuilder(TooltipText.Heading("Requires"));
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
        var sb = new StringBuilder(TooltipText.Heading("Effects"));
        if (data.housing > 0) sb.Append("\n").Append(TooltipText.Bullet(TooltipText.Good($"+{data.housing}") + " Housing"));
        for (int i = 0; i < data.producedResources.Count && i < data.productionRates.Count; i++)
        {
            float rate = slot != null ? slot.GetEffectiveProductionRate(i) : data.productionRates[i];
            sb.Append("\n").Append(TooltipText.Bullet(TooltipText.Good($"+{rate:0.##}/s") + $" {data.producedResources[i]}"));
        }
        for (int i = 0; i < data.storageResources.Count && i < data.storageAmount.Count; i++)
        {
            sb.Append("\n").Append(TooltipText.Bullet("Storage " + TooltipText.Good($"+{data.storageAmount[i]:0.##}") + $" {data.storageResources[i]}"));
        }
        if (data.consumedResources.Count > 0)
        {
            sb.Append("\n").Append(TooltipText.Heading("Consumes"));
            for (int i = 0; i < data.consumedResources.Count && i < data.consumeRates.Count; i++)
            {
                sb.Append("\n").Append(TooltipText.Bullet(TooltipText.Bad($"-{data.consumeRates[i]:0.##}/s") + $" {data.consumedResources[i]}"));
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
        if (efficiency != 0f) lines.Add(TooltipText.Row("Production Efficiency", TooltipText.Signed(efficiency, "%")));
        float cost = units.GetConstructionCostPercent(unit);
        if (cost != 0f) lines.Add(TooltipText.Row("Construction Cost", TooltipText.Signed(cost, "%", higherIsBetter: false)));

        var sources = new List<string>();
        foreach (var key in new[] { unit.name, ModifierTargets.Section(unit.section), ModifierTargets.Type(unit.type), ModifierTargets.All })
        {
            if (key == null) continue;
            foreach (var source in units.ProductionEfficiency.Sources(key))
            {
                sources.Add(TooltipText.Bullet(TooltipText.Signed(source.Value.Percent, "%") + $" output from {source.Key} " + TooltipText.Muted($"({ModifierTargets.Describe(key)})")));
            }
            foreach (var source in units.ConstructionCost.Sources(key))
            {
                sources.Add(TooltipText.Bullet(TooltipText.Signed(source.Value.Percent, "%", higherIsBetter: false) + $" cost from {source.Key} " + TooltipText.Muted($"({ModifierTargets.Describe(key)})")));
            }
        }

        if (lines.Count == 0 && sources.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        if (lines.Count > 0) sb.Append(TooltipText.Heading("Active Modifiers")).Append("\n").Append(string.Join("\n", lines));
        if (sources.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(TooltipText.Heading("Sources")).Append("\n").Append(string.Join("\n", sources));
        }
        return sb.ToString();
    }

    // ===== TECHNOLOGIES =====

    /// <summary>
    /// A technology card: what its research still costs (after Discovery Efficiency, less what is paid), what it needs
    /// (a prerequisite not uncovered yet is only "undiscovered", never named), what it leads to (uncovered technologies
    /// only), its Enlightenment goals and gift, and where it stands in the research plan with what a click would do.
    /// </summary>
    public static bool Technology(GameTechnologySlot slot, TooltipData d)
    {
        if (slot == null || slot.gameUnit == null) return false;
        string name = slot.gameUnit.name;
        d.title = name;
        d.description = slot.gameUnit.description;
        string type = slot.gameUnit.type;
        d.type = type == "Event" ? $"<color={TooltipText.WarnHex}>{type} Tech</color>" : type == "Crisis" ? $"<color={Bad}>{type} Tech</color>" : $"{type} Tech";
        Keywords.AddLore(d, TechnologyKeyword(name));

        var tech = slot.technologyData;
        if (tech == null) return true;
        var units = GameUnitsLogic.Instance;
        Dictionary<string, float> progress = null;
        units?.technologyProgress.TryGetValue(slot, out progress);
        var adjusted = units != null ? units.GetAdjustedTechCosts(slot) : tech.resourceAmount;

        var costs = new StringBuilder(TooltipText.Heading("Requires"));
        for (int i = 0; i < tech.resourceRequirements.Count && i < tech.resourceAmount.Count; i++)
        {
            string resource = tech.resourceRequirements[i];
            float remaining = i < adjusted.Count ? adjusted[i] : tech.resourceAmount[i];
            if (progress != null && progress.TryGetValue(resource, out float paid)) remaining -= paid;
            remaining = slot.isUnlocked ? 0f : Mathf.Max(0f, remaining);
            bool met = slot.isUnlocked || CanAfford(resource, remaining);
            costs.Append('\n').Append(CostLine(remaining, resource, met, true));
        }
        d.requirements = costs.ToString();

        var links = new StringBuilder();
        if (tech.techRequirements.Count > 0)
        {
            links.Append(TooltipText.Heading("Must Unlock First"));
            foreach (string required in tech.techRequirements)
            {
                bool unlocked = units != null && units.IsTechnologyUnlocked(required);
                string line = unlocked || TechnologyTreeLogic.IsUncovered(required) ? TooltipText.Judge(required, unlocked) : TooltipText.Muted("An undiscovered technology");
                links.Append("\n").Append(TooltipText.Bullet(line, unlocked ? Good : Bad));
            }
        }
        var next = TechnologyTreeLogic.UncoveredSuccessors(name);
        if (next.Count > 0)
        {
            if (links.Length > 0) links.Append("\n\n");
            links.Append(TooltipText.Heading("Leads To"));
            foreach (string successor in next)
            {
                bool researched = units != null && units.IsTechnologyUnlocked(successor);
                links.Append("\n").Append(TooltipText.Bullet(researched ? TooltipText.Good(successor) : successor));
            }
        }
        if (links.Length > 0) d.prerequisites = links.ToString();

        var notes = new List<string>();
        string enlightenment = Enlightenment(slot);
        if (!string.IsNullOrEmpty(enlightenment)) notes.Add(enlightenment);
        string plan = ResearchPlan(slot, units);
        if (!string.IsNullOrEmpty(plan)) notes.Add(plan);
        if (notes.Count > 0) d.notes = string.Join("\n\n", notes);
        return true;
    }

    /// <summary>
    /// A technology's Enlightenment (its eureka): each goal with how far along it is, and the gift, a share of its research
    /// paid at once (and Era Score for an Event or Crisis Technology). Empty for a researched technology, or one with no
    /// goal that was never enlightened.
    /// </summary>
    public static string Enlightenment(GameTechnologySlot slot)
    {
        if (slot == null || slot.isUnlocked) return string.Empty;
        var goals = slot.technologyData != null ? slot.technologyData.enlightenedConditions.Where(g => g != null).ToList() : new List<EnlightenedCondition>();
        if (goals.Count == 0 && !slot.enlightenedCompleted) return string.Empty;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(slot.enlightenedBonusPercent) * 100f);
        var sb = new StringBuilder(slot.enlightenedCompleted
            ? TooltipText.Heading("Enlightened", TooltipText.Good($"{percent}% of its research done"))
            : TooltipText.Heading("Enlightenment", TooltipText.Muted($"{percent}% of its research")));
        foreach (var goal in goals)
        {
            bool met = slot.enlightenedCompleted || goal.IsMet();
            // A riddle is read, not tracked: no progress, a clue once the people are on the right path, the plain goal once answered.
            if (goal.IsRiddle && !met)
            {
                sb.Append("\n").Append(TooltipText.Bullet($"<i>{goal.riddle.Trim()}</i>", TooltipText.MutedHex));
                if (goal.ClueShown()) sb.Append("\n").Append(TooltipText.Muted($"   Clue: {goal.clue.Trim()}"));
                continue;
            }
            string progress = slot.enlightenedCompleted ? string.Empty : goal.ProgressText();
            string text = string.IsNullOrEmpty(progress) ? goal.Describe() : $"{goal.Describe()} {TooltipText.Muted(progress)}";
            if (goal.IsRiddle) text += TooltipText.Muted($" (the riddle: {goal.riddle.Trim()})");
            sb.Append("\n").Append(TooltipText.Bullet(met ? TooltipText.Good(text) : text, met ? Good : TooltipText.MutedHex));
        }
        if (!slot.enlightenedCompleted && goals.Any(g => g.IsRiddle && !g.IsMet()))
            sb.Append("\n").Append(TooltipText.Muted("A riddle names no goal and counts no progress: work out what it asks, and the technology is enlightened the moment your people do it."));

        // Event and Crisis Technologies also earn Era Score when enlightened (AgeProgression awards it).
        var age = AgeProgression.Instance != null ? AgeProgression.Instance.Current : null;
        int era = age != null ? TechTreeRules.EnlightenmentEraScore(slot.IsEventTechnology, slot.IsCrisisTechnology, age.enlightenedTechnologyEraScore) : 0;
        if (era > 0)
        {
            string kind = slot.IsCrisisTechnology ? "Crisis" : "Event";
            sb.Append("\n").Append(TooltipText.Muted(slot.enlightenedCompleted
                ? $"Its Enlightenment earned {era} Era Score ({kind} Technology)."
                : $"{(kind == "Event" ? "An Event" : "A Crisis")} Technology: enlightening it earns {era} Era Score."));
        }
        return sb.ToString();
    }

    /// <summary>Where a technology stands in the research plan, and what a click on it would do.</summary>
    public static string ResearchPlan(GameTechnologySlot slot, GameUnitsLogic units)
    {
        if (slot == null || slot.isUnlocked || units == null || slot.gameUnit == null) return string.Empty;
        string name = slot.gameUnit.name;
        var plan = units.ResearchPlan;
        int position = TechTreeRules.PlanPosition(plan, name);
        var lines = new List<string>();
        if (slot == units.activeTechnologySlot) lines.Add(TooltipText.Good("Researching now"));
        else if (position > 0) lines.Add(TooltipText.Value($"Planned: {Ordinal(position)} of {plan.Count}"));

        if (slot != units.activeTechnologySlot)
        {
            var path = TechTreeRules.PlanTo(name, TechnologyTreeLogic.Prerequisites, TechnologyTreeLogic.IsResearched);
            int before = path.Count - 1;
            int unknown = path.Count(step => !TechnologyTreeLogic.IsUncovered(step));
            string click = before <= 0 ? "Click to research it now." : $"Click to plan its way: {before} technolog{(before == 1 ? "y" : "ies")} first{(unknown > 0 ? $", {unknown} undiscovered" : string.Empty)}.";
            lines.Add(TooltipText.Muted(click + " Shift+click adds it to the plan."));
        }
        if (position > 0) lines.Add(TooltipText.Muted("Right click takes it out of the plan."));
        return string.Join("\n", lines);
    }

    private static string Ordinal(int n) => n + (n % 100 >= 11 && n % 100 <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

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
                    var costs = new StringBuilder(TooltipText.Heading("Requires"));
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
                d.effects = $"Reduces food distribution losses; never reduces basic nutritional needs ({unlockable.resourceModifier}% of the original loss allowance)";
                d.description = "Not starving anymore...";
                break;

            case TechUnlockableType.CouncilSeat:
                d.title = "A Council Seat";
                d.type = "Council";
                d.description = unlockable.description;
                d.effects = !string.IsNullOrEmpty(unlockable.effects) ? unlockable.effects : "Opens a new position on the council for a legend.";
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

            case TechUnlockableType.GrimoireSeat:
                int seats = Mathf.Max(1, Mathf.RoundToInt(unlockable.resourceModifier));
                d.title = unlockable.seat == GrimoireSeat.Ceremony ? "A Ceremony" : "A Symphony Seat";
                d.type = "Grimoire";
                d.description = unlockable.description;
                d.effects = !string.IsNullOrEmpty(unlockable.effects) ? unlockable.effects
                    : unlockable.seat == GrimoireSeat.Ceremony ? $"{seats * Grimoire.VoicesPerCeremony} voice seats: Resonance, Flux and Strand, one Unison each."
                    : $"{seats} Symphony seat{(seats == 1 ? string.Empty : "s")} in the deck, for battle and the field.";
                break;

            case TechUnlockableType.SymphonyCard:
                var card = unlockable.symphonyCard;
                d.title = card != null ? card.DisplayName : unlockable.name;
                d.type = card != null ? $"Symphony Card: {card.Form}" : "Symphony Card";
                d.description = card != null && !string.IsNullOrEmpty(card.description) ? card.description : unlockable.description;
                d.effects = card == null ? unlockable.effects : string.Join("\n", new[]
                {
                    string.IsNullOrEmpty(card.fieldEffect) ? null : $"Field: {card.fieldEffect}",
                    string.IsNullOrEmpty(card.battleEffect) ? null : $"Battle: {card.battleEffect}",
                    Grimoire.VoiceOf(card.binding) != CeremonyVoice.None ? $"Sings the {Grimoire.VoiceOf(card.binding)} of a Ceremony." : null,
                    $"Flickers {Mathf.RoundToInt(card.flickerChance * 100f)}% of the time in this Age.",
                }.Where(s => s != null));
                break;

            case TechUnlockableType.SpellWildcard:
                int blanks = Mathf.Max(1, Mathf.RoundToInt(unlockable.resourceModifier));
                d.title = blanks == 1 ? "A Wildcard" : $"{blanks} Wildcards";
                d.type = "Spell Maker";
                d.description = unlockable.description;
                d.effects = !string.IsNullOrEmpty(unlockable.effects) ? unlockable.effects : "A blank card to compose: a binding your people have heard, Minor or Major, and what it is for.";
                break;

            case TechUnlockableType.ScoreChange:
                d.title = unit != null ? unit.name : unlockable.name;
                d.type = "Consequence";
                d.description = unlockable.description;
                d.effects = unlockable.effects;
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
        // Greats are earned (LegendGreats): the type line shows what the legend has become, not the Great it leans toward.
        var standing = LegendProgress.Instance != null && LegendProgress.Instance.IsRecruited(legend.legendName) ? LegendProgress.Instance.GreatsTitle(legend.legendName) : LegendGreats.UnattunedTitle;
        d.type = $"{legend.rarity} {standing}";
        d.requirements = legend.councilAssignmentDescription;

        var government = GovernmentLogic.Instance;
        var headOfState = government != null ? government.GetCouncilSeat(GovernmentLogic.HeadOfStateIndex) : null;
        bool isHeadOfState = headOfState != null && headOfState.assignedLegend == legend;
        // A legend's own bonuses grow with its rank (LegendGrowthRules) and dim with its Composure, on top of the Head of State multiplier.
        var progress = LegendProgress.Instance;
        int rank = progress != null ? progress.Rank(legend.legendName) : 1;
        float growth = progress != null ? progress.Growth(legend.legendName) : 1f;
        float composure = progress != null ? progress.ComposureFactor(legend.legendName) : 1f;
        var soul = progress != null ? progress.Soul(legend.legendName) : null;
        float multiplier = (isHeadOfState ? government.HeadOfStateMultiplier : 1f) * growth * composure;
        var scaling = new List<string>();
        if (isHeadOfState) scaling.Add($"x{government.HeadOfStateMultiplier:0.##} as Head of State");
        if (!Mathf.Approximately(growth, 1f)) scaling.Add($"x{growth:0.##} at rank {rank}");
        string composureNote = LegendSoulText.CouncilNote(soul, composure, LegendLore.ComposureTuning);
        if (composureNote != null) scaling.Add(composureNote);

        // Who the legend is: its Soul Leitmotif and Composure up front, its Legend Traits and bindings behind "More...".
        if (soul != null && progress.IsRecruited(legend.legendName))
        {
            d.summary = LegendSoulText.Summary(soul, LegendLore.ComposureTuning);
            d.details = LegendSoulText.Details(soul, LegendLore.Traits, progress.Bindings(legend.legendName), LegendLore.Composure, LegendLore.ComposureTuning);
            d.details += "\n\n" + TooltipText.Heading("Relationships") + "\n" + progress.RelationshipText(legend.legendName);
        }

        var sb = new StringBuilder();
        if (legend.bonuses != null && legend.bonuses.Count > 0)
        {
            sb.Append("\n").Append(TooltipText.Heading("When Assigned", scaling.Count > 0 ? TooltipText.Warn(string.Join(", ", scaling)) : null));
            foreach (var bonus in legend.bonuses)
            {
                if (bonus == null) continue;
                sb.Append("\n").Append(TooltipText.Bullet(bonus.GetAutoDescription()));
                if (!Mathf.Approximately(multiplier, 1f))
                {
                    string unit = bonus.modifierType == ModifierType.Percentage ? "%" : "";
                    sb.Append($" <color={TooltipText.WarnHex}>(→ {bonus.modifierValue * multiplier:+0.##;-0.##}{unit})</color>");
                }
            }
        }
        if (isHeadOfState && headOfState.seatBonuses.Count > 0)
        {
            sb.Append("\n\n").Append(TooltipText.Heading("Seat Effects")).Append(SeatBonusLines(headOfState));
        }
        d.effects = sb.ToString();

        // Lyrical Fragments and the deeds that earned them: how the legend grows.
        if (progress != null && progress.IsRecruited(legend.legendName))
        {
            int next = LegendGrowthRules.NextThreshold(rank);
            int fragments = progress.Fragments(legend.legendName);
            var growthLines = new StringBuilder(TooltipText.Heading($"Rank {rank} of {LegendGrowthRules.MaxRank}",
                next > 0 ? $"{fragments} / {next} Lyrical Fragments" : $"{fragments} Lyrical Fragments"));
            string purse = LyricalFragments.Describe(progress.Purse(legend.legendName));
            if (purse.Length > 0) growthLines.Append("\n").Append(TooltipText.Muted(purse));
            if (progress.IsUnderdog(legend.legendName)) growthLines.Append("\n").Append(TooltipText.Bullet("Underdog: double fragments for deeds against the odds"));
            foreach (var deed in progress.Deeds(legend.legendName).Reverse().Take(4)) growthLines.Append("\n").Append(TooltipText.Bullet(deed));
            string history = progress.BalladHistory(legend.legendName)?.Describe();
            if (!string.IsNullOrEmpty(history)) growthLines.Append("\n\n").Append(history);
            d.notes = growthLines.ToString();
        }
        return true;
    }

    /// <summary>A council seat: title, role, allowed classes, its bonuses and whether it is active yet.</summary>
    public static bool Seat(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        d.title = seat.GetEffectiveTitle();
        d.description = seat.roleplayDescription;
        d.type = "Council Seat";
        Keywords.AddLore(d, d.title);
        d.requirements = TooltipText.Heading("Allowed Classes") + "\n" + AllowedClasses(seat);
        // The areas stories call on this seat for (a caravan at the gates asks for defense).
        if (seat.areas != null && seat.areas.Count > 0)
            d.requirements += "\n\n" + TooltipText.Heading("Answers For") + "\n" + string.Join(", ", seat.areas.Select(a => CouncilAreaRules.NameOf(a, CouncilAreaCatalog.Current)));
        if (seat.seatBonuses.Count > 0) d.effects = TooltipText.Heading("Seat Effects", TooltipText.Muted("once a Legend is seated")) + SeatBonusLines(seat);
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

    /// <summary>A seat's activity indicator: red empty, yellow seated (seat bonuses apply, legend activating), green fully active.</summary>
    public static bool SeatActivity(CouncilSeat seat, TooltipData d)
    {
        if (seat == null) return false;
        d.title = seat.assignedLegend == null ? "Seat Empty" : seat.IsActive() ? "Seat and Legend Active" : "Seat Active, Legend Activating";
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
            : $"<color={Good}>Seat effects active</color>\n<color={TooltipText.WarnHex}>Legend activates in {seat.seventhsUntilActive} Sevenths</color>";
        var government = GovernmentLogic.Instance;
        int cooldown = government != null ? government.GetSeatCooldownRemaining(seat.seatIndex) : 0;
        return cooldown > 0 ? $"{status}\n<color={TooltipText.MutedHex}>Can change again in {cooldown} Sevenths</color>" : status;
    }

    /// <summary>Cooldown status only (the seat's cooldown indicator).</summary>
    public static string SeatCooldown(CouncilSeat seat)
    {
        var government = GovernmentLogic.Instance;
        int cooldown = seat != null && government != null ? government.GetSeatCooldownRemaining(seat.seatIndex) : 0;
        return cooldown > 0 ? $"<color={TooltipText.MutedHex}>Can change again in {cooldown} Sevenths</color>" : $"<color={TooltipText.GoodHex}>Ready to change</color>";
    }

    public static string AllowedClasses(CouncilSeat seat)
    {
        return LegendGreats.Requirement(seat.allowedLegendClasses, seat.requiredStars);
    }

    private static string SeatBonusLines(CouncilSeat seat)
    {
        var sb = new StringBuilder();
        foreach (var bonus in seat.seatBonuses)
        {
            if (bonus == null || bonus.bonusType == SeatBonusType.CivicBonus) continue;
            sb.Append("\n").Append(TooltipText.Bullet(bonus.GetAutoDescription()));
        }
        return sb.ToString();
    }

    public static bool Civic(CivicData civic, TooltipData d)
    {
        if (civic == null) return false;
        d.title = civic.civicName;
        d.description = civic.description;
        d.type = $"{civic.rarity} {civic.tier} Civic";
        if (civic.effects != null && civic.effects.Count > 0)
        {
            var sb = new StringBuilder(TooltipText.Heading("When Assigned"));
            foreach (var effect in civic.effects) if (effect != null) sb.Append("\n").Append(TooltipText.Bullet(effect.GetAutoDescription()));
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
            d.type = $"{civic.rarity} {civic.tier} Civic";
            if (civic.grantsCouncilPosition && civic.councilPosition != null && !string.IsNullOrEmpty(civic.councilPosition.title))
            {
                d.requirements = $"Grants {civic.councilPosition.title} Council Position";
            }
        }
        else
        {
            d.type = "Council Seat";
            Keywords.AddLore(d, seatTitle);
        }
        if (!string.IsNullOrEmpty(effects) && effects != "N/A") d.effects = effects;
        d.prerequisites = TooltipText.Heading("Allowed Classes") + "\n" + (!string.IsNullOrEmpty(leaderClasses) && leaderClasses != "N/A" ? leaderClasses : "Any/All Classes");
        return true;
    }

    // ===== SHARED =====

    /// <summary>One cost bullet, good when affordable and bad otherwise. <paramref name="icon"/> keeps the inline sprite technology costs use.</summary>
    public static string CostLine(float amount, string resource, bool affordable, bool icon) =>
        TooltipText.Bullet(TooltipText.Judge($"{Amount(amount)}{(icon ? " <sprite=2>" : "")}", affordable) + $" {resource}", affordable ? Good : Bad);

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
